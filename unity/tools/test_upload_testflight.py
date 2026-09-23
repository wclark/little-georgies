"""Offline upload-helper checks. Never contacts Apple or reads real credentials."""

import base64
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.asymmetric import ec, utils

spec = importlib.util.spec_from_file_location("uploader", Path(__file__).with_name("upload-testflight.py"))
uploader = importlib.util.module_from_spec(spec)
spec.loader.exec_module(uploader)


class UploadChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / "fixture.ipa"
        self.path.write_bytes(b"abcdefghij")
        self.operations = [
            {"url": "https://storage.apple.com/part1", "method": "PUT", "offset": 0, "length": 4,
             "entityTag": hashlib.md5(b"abcd").hexdigest().upper()},
            {"url": "https://storage.apple.com/part2", "method": "PUT", "offset": 4, "length": 6,
             "entityTag": '"' + hashlib.md5(b"efghij").hexdigest() + '"'},
        ]

    def test_token_signature(self):
        key = ec.generate_private_key(ec.SECP256R1())
        token = uploader.token(key, "fixture", "fixture")
        header, claims, signature = token.split(".")
        raw = base64.urlsafe_b64decode(signature + "==")
        self.assertEqual(len(raw), 64)
        der = utils.encode_dss_signature(int.from_bytes(raw[:32], "big"), int.from_bytes(raw[32:], "big"))
        key.public_key().verify(der, f"{header}.{claims}".encode(), ec.ECDSA(hashes.SHA256()))
        fields = json.loads(base64.urlsafe_b64decode(claims + "=="))
        self.assertEqual(fields["exp"] - fields["iat"], 600)

    def test_part_integrity(self):
        self.assertEqual(uploader.verify_parts(self.path, list(reversed(self.operations))), 2)

    def test_invalid_operations(self):
        for change in (
            {"url": "https://apple.com.attacker.invalid/a"},
            {"url": "http://storage.apple.com/a"},
            {"url": "https://name:password@storage.apple.com/a"},
            {"method": "POST"}, {"offset": 1}, {"length": 0}, {"length": 11},
        ):
            with self.subTest(change=change):
                operations = copy.deepcopy(self.operations)
                operations[0].update(change)
                with self.assertRaises(ValueError):
                    uploader.validate_operations(operations, 10)

    def test_incomplete_or_corrupt_parts(self):
        for tag in ("", "0" * 32):
            with self.subTest(tag=tag):
                operations = copy.deepcopy(self.operations)
                operations[0]["entityTag"] = tag
                with self.assertRaises(ValueError):
                    uploader.verify_parts(self.path, operations)

    def fake_apple(self, state="AWAITING_UPLOAD"):
        class FakeApple:
            calls = None

            def request(inner, method, path, body=None):
                inner.calls.append((method, path, body))
                return {"data": {"attributes": {
                    "fileSize": 10,
                    "uploadOperations": self.operations,
                    "assetDeliveryState": {"state": state if method == "GET" else "COMPLETE"},
                }}}

        apple = FakeApple()
        apple.calls = []
        return apple

    def test_commit_omits_optional_checksum(self):
        apple = self.fake_apple()
        self.assertEqual(uploader.commit_file(apple, self.path, {"fileId": "fixture"})["state"], "COMPLETE")
        self.assertEqual(apple.calls[-1][2]["data"]["attributes"], {"uploaded": True})

    def test_completed_file_not_recommitted(self):
        apple = self.fake_apple("COMPLETE")
        uploader.commit_file(apple, self.path, {"fileId": "fixture"})
        self.assertEqual(len(apple.calls), 1)

    def test_failed_file_not_committed(self):
        apple = self.fake_apple("FAILED")
        with self.assertRaises(ValueError):
            uploader.commit_file(apple, self.path, {"fileId": "fixture"})
        self.assertEqual(len(apple.calls), 1)


if __name__ == "__main__":
    unittest.main()
