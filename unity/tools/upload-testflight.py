"""Upload only Little Georgies builds through Apple's documented Build Upload API.

Requires Python 3.11+ and cryptography. Credentials remain outside the repository.
Without --upload or --commit-existing this only reads Apple status.
It never submits an App Store release.
"""

import argparse
import base64
import hashlib
import json
import plistlib
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec, utils

APP_ID = "6814987738"
BUNDLE_ID = "org.georgist.littlegeorgies"
API = "https://api.appstoreconnect.apple.com"


def encoded(data):
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")


def token(key, key_id, issuer):
    now = int(time.time())
    header = {"alg": "ES256", "kid": key_id, "typ": "JWT"}
    claims = {"iss": issuer, "iat": now, "exp": now + 600, "aud": "appstoreconnect-v1"}
    message = ".".join(encoded(json.dumps(x, separators=(",", ":")).encode()) for x in (header, claims))
    r, s = utils.decode_dss_signature(key.sign(message.encode(), ec.ECDSA(hashes.SHA256())))
    return message + "." + encoded(r.to_bytes(32, "big") + s.to_bytes(32, "big"))


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Apple:
    def __init__(self, key_path, key_id, issuer):
        self.key = serialization.load_pem_private_key(key_path.read_bytes(), password=None)
        if not isinstance(self.key, ec.EllipticCurvePrivateKey) or self.key.curve.name != "secp256r1":
            raise ValueError("Expected an Apple P-256 API key.")
        self.key_id, self.issuer = key_id, issuer
        self.http = urllib.request.build_opener(NoRedirect())

    def request(self, method, path, body=None):
        if not path.startswith("/v1/"):
            raise ValueError("Only App Store Connect v1 endpoints are allowed.")
        data = json.dumps(body).encode() if body is not None else None
        headers = {"Authorization": "Bearer " + token(self.key, self.key_id, self.issuer),
                   "Content-Type": "application/json"}
        request = urllib.request.Request(API + path, data=data, headers=headers, method=method)
        try:
            with self.http.open(request, timeout=90) as response:
                raw = response.read()
                return json.loads(raw) if raw else {}
        except urllib.error.HTTPError as error:
            # API error descriptions are useful; never print request headers or tokens.
            try:
                errors = json.loads(error.read()).get("errors", [])
                detail = "; ".join(str(e.get("detail", e.get("title", ""))) for e in errors)
            except (ValueError, TypeError):
                detail = "No JSON error detail."
            raise RuntimeError(f"Apple API HTTP {error.code}: {detail[:2000]}") from None
        except urllib.error.URLError:
            raise RuntimeError("Apple API connection failed; check status before retrying an upload.") from None


def ipa_info(path):
    with zipfile.ZipFile(path) as archive:
        names = [n for n in archive.namelist()
                 if n.startswith("Payload/") and n.count("/") == 2 and n.endswith(".app/Info.plist")]
        if len(names) != 1:
            raise ValueError("Expected exactly one main iOS app in the IPA.")
        info = plistlib.loads(archive.read(names[0]))
    if info.get("CFBundleIdentifier") != BUNDLE_ID:
        raise ValueError("This helper only uploads Little Georgies.")
    return {"version": info["CFBundleShortVersionString"], "build": info["CFBundleVersion"]}


def validate_operations(operations, size):
    ordered = sorted(operations, key=lambda op: op["offset"])
    offset = 0
    for op in ordered:
        url = urllib.parse.urlsplit(op["url"])
        host = url.hostname or ""
        trusted = any(host.endswith(suffix) for suffix in (".apple.com", ".icloud.com", ".icloud-content.com", ".amazonaws.com"))
        if url.scheme != "https" or url.username or url.password or not trusted:
            raise ValueError("Unexpected Apple upload destination; manual review required.")
        if op["method"] != "PUT" or op["offset"] != offset or op["length"] <= 0:
            raise ValueError("Invalid or non-contiguous Apple upload operations.")
        offset += op["length"]
    if offset != size:
        raise ValueError("Apple upload operations do not cover the whole file.")
    return ordered


def put_parts(apple, path, operations):
    with path.open("rb") as source:
        for index, op in enumerate(validate_operations(operations, path.stat().st_size), 1):
            source.seek(op["offset"])
            data = source.read(op["length"])
            headers = {h["name"]: h["value"] for h in op.get("requestHeaders", [])}
            # Signed storage requests deliberately do not receive the Apple API JWT.
            request = urllib.request.Request(op["url"], data=data, headers=headers, method="PUT")
            try:
                with apple.http.open(request, timeout=180) as response:
                    response.read()
            except (urllib.error.URLError, TimeoutError):
                raise RuntimeError(f"File transfer failed at part {index}; signed URL omitted from logs.") from None
            print(f"Uploaded part {index}/{len(operations)}", flush=True)


def record(path, report):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


def verify_parts(path, operations):
    ordered = validate_operations(operations, path.stat().st_size)
    with path.open("rb") as source:
        for index, op in enumerate(ordered, 1):
            source.seek(op["offset"])
            digest = hashlib.md5(source.read(op["length"]), usedforsecurity=False).hexdigest()
            if op.get("entityTag", "").strip('"').lower() != digest:
                raise ValueError(f"Apple's received part {index} does not match the local IPA.")
    return len(ordered)


def commit_file(apple, path, report):
    file_id = report["fileId"]
    asset = apple.request("GET", f"/v1/buildUploadFiles/{file_id}")["data"]
    attributes = asset["attributes"]
    if attributes["fileSize"] != path.stat().st_size:
        raise ValueError("Apple's reserved file size differs from the local IPA.")
    state = attributes.get("assetDeliveryState", {})
    if state.get("state") in ("UPLOAD_COMPLETE", "COMPLETE"):
        return state
    if state.get("state") != "AWAITING_UPLOAD":
        raise ValueError(f"Cannot commit file in state {state.get('state')}.")
    count = verify_parts(path, attributes["uploadOperations"])
    print(f"Verified all {count} received parts against the local IPA.", flush=True)
    # Checksums are optional in Apple's schema; received part ETags are verified above.
    body = {"data": {"type": "buildUploadFiles", "id": file_id,
                     "attributes": {"uploaded": True}}}
    committed = apple.request("PATCH", f"/v1/buildUploadFiles/{file_id}", body)["data"]
    return committed["attributes"].get("assetDeliveryState")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ipa", type=Path, required=True)
    parser.add_argument("--key", type=Path, required=True)
    parser.add_argument("--key-id", required=True)
    parser.add_argument("--issuer", required=True)
    parser.add_argument("--report", type=Path, required=True)
    action = parser.add_mutually_exclusive_group()
    action.add_argument("--upload", action="store_true", help="Explicitly authorize creating/uploading a build")
    action.add_argument("--commit-existing", action="store_true",
                        help="Verify and commit the already-transferred file in an existing report")
    args = parser.parse_args()
    info = ipa_info(args.ipa)
    with args.ipa.open("rb") as source:
        digest = hashlib.file_digest(source, "sha256").hexdigest()
    apple = Apple(args.key, args.key_id, args.issuer)
    app = apple.request("GET", f"/v1/apps/{APP_ID}")["data"]
    if app["attributes"]["bundleId"] != BUNDLE_ID:
        raise ValueError("Apple app record does not match the IPA.")
    print(f"Verified Apple app: {BUNDLE_ID}, {info['version']} ({info['build']})", flush=True)
    if args.report.exists():
        report = json.loads(args.report.read_text(encoding="utf-8"))
        if report["sha256"] != digest or report["appId"] != APP_ID:
            raise ValueError("Existing upload report belongs to a different app or file.")
        state = apple.request("GET", f"/v1/buildUploads/{report['uploadId']}?include=build")["data"]
        if (state["attributes"]["cfBundleVersion"] != info["build"] or
                state["attributes"]["cfBundleShortVersionString"] != info["version"]):
            raise ValueError("Apple upload version does not match the local IPA.")
        if args.commit_existing:
            if not report.get("fileId"):
                raise ValueError("The upload report has no file to commit.")
            report.update(stage="committed", fileState=commit_file(apple, args.ipa, report))
            record(args.report, report)
            state = apple.request("GET", f"/v1/buildUploads/{report['uploadId']}?include=build")["data"]
        report["state"] = state["attributes"]["state"]
        report["buildId"] = state.get("relationships", {}).get("build", {}).get("data")
        record(args.report, report)
        print(json.dumps(report, indent=2))
        if report["state"].get("state") == "FAILED":
            raise RuntimeError("Apple processing failed; see the upload report above.")
        return
    if args.commit_existing:
        raise ValueError("--commit-existing requires an existing upload report.")
    if not args.upload:
        print("Read-only preflight passed. No upload was created.")
        return
    existing = apple.request("GET", f"/v1/apps/{APP_ID}/buildUploads?limit=200")["data"]
    if any(x["attributes"]["cfBundleVersion"] == info["build"] and
           x["attributes"]["cfBundleShortVersionString"] == info["version"] for x in existing):
        raise ValueError("Apple already has an upload for this version/build; inspect it before retrying.")
    body = {"data": {"type": "buildUploads", "attributes": {
        "cfBundleShortVersionString": info["version"], "cfBundleVersion": info["build"], "platform": "IOS"},
        "relationships": {"app": {"data": {"type": "apps", "id": APP_ID}}}}}
    upload = apple.request("POST", "/v1/buildUploads", body)["data"]
    report = {"appId": APP_ID, **info, "sha256": digest, "uploadId": upload["id"], "stage": "reserved"}
    record(args.report, report)
    body = {"data": {"type": "buildUploadFiles", "attributes": {
        "assetType": "ASSET", "fileName": args.ipa.name, "fileSize": args.ipa.stat().st_size, "uti": "com.apple.ipa"},
        "relationships": {"buildUpload": {"data": {"type": "buildUploads", "id": upload["id"]}}}}}
    asset = apple.request("POST", "/v1/buildUploadFiles", body)["data"]
    report.update(fileId=asset["id"], stage="file_reserved")
    record(args.report, report)
    put_parts(apple, args.ipa, asset["attributes"]["uploadOperations"])
    report.update(stage="uploaded_parts")
    record(args.report, report)
    report.update(stage="committed", fileState=commit_file(apple, args.ipa, report))
    record(args.report, report)
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, RuntimeError, OSError) as error:
        print(f"Upload stopped: {error}", file=sys.stderr)
        sys.exit(1)
