# Build and Test Pipeline

The website/S3 workflow is separate and unchanged. The Unity pipeline has three
gates: portable model tests, a Windows player with runtime checks, and a signed
iOS build followed by a real TestFlight device test. Passing the first two does
not establish that the third works.

## Current Setup (2026-09-23)

The owner has now authorized a manual signed iOS build and TestFlight delivery
of the dossier revision for their iPad. This supersedes setup-only mode for this
test build; it does not authorize a public App Store submission or release.
Auto-build, scheduled builds, and automatic GitHub triggers must stay off.
`VERIFICATION.md` distinguishes the local revision checks from earlier runs.

The first manual iOS build, **#1**, succeeded on 2026-09-23 from commit `8bdeaee`.
Its IPA is available in Unity's artifacts, but automated browser download was
blocked. Apple sign-in, upload setup, TestFlight processing, and device testing
remain pending. The setup facts below describe the earlier saved configuration;
the build outcome is recorded in `VERIFICATION.md`.

- Unity Cloud project: [Little Georgies](https://cloud.unity.com/organizations/1375991457548/projects/22a9185f-609f-4365-b527-d62132adc8a8).
- Organization: `1375991457548`; project: `22a9185f-609f-4365-b527-d62132adc8a8`.
- Audience: general audience, not primarily directed at children, confirmed by the owner.
- Local Windows pipeline passed; see `VERIFICATION.md` and `Artifacts/pipeline.json`.
- Unity source and the dormant workflow are published on
  [`setup/unity-ios-pipeline`](https://github.com/wclark/little-georgies/tree/setup/unity-ios-pipeline),
  starting at commit `ead5042`. `main` and the website deployment are unchanged.
  GitHub reported no workflow runs on this branch after publication.
- Build Automation's free tier is available. After anonymous HTTPS failed,
  the owner approved a repository-only SSH deploy key. GitHub key `164131885`
  (`Unity Build Automation (read-only)`) is verified `read_only: true`; Unity
  saved `git@github.com:wclark/little-georgies.git` successfully. No account-wide
  OAuth access or write access was granted.
- Apple Developer membership is active. The explicit App ID
  `org.georgist.littlegeorgies` is registered as Little Georgies. No optional
  capabilities were enabled. The owner accepted App Store Connect's separate
  agreement. [Little Georgies](https://appstoreconnect.apple.com/apps/6814987738/distribution)
  now exists there as app `6814987738` (iOS, English US, SKU `little-georgies`),
  in Prepare for Submission, with no uploaded build or submitted release.
- The [iOS TestFlight - Manual target](https://cloud.unity.com/organizations/1375991457548/projects/22a9185f-609f-4365-b527-d62132adc8a8/cloud-build/setup/buildTarget/ios-testflight-manual)
  is saved as `ios-testflight-manual`, using **Save configuration**, not
  Save and build. Its saved settings and signing credential were reopened and
  verified. Auto-build, auto-cancel, and repeating schedules are off; Unity's
  project build history is empty.
- With owner approval, Unity now stores the `Little Georgies App Store 2026`
  signing credential. The saved credential displays the correct app bundle ID,
  App Store profile type, and matching Apple Distribution certificate. Both
  expire on 2027-09-22. The password-protected P12 was reopened locally to verify
  its matching private key before upload.
  The original key and P12 password
  are stored with Windows DPAPI CurrentUser protection, with exactly one
  owner-only directory access rule. No plaintext password file was created.
  The downloaded profile's CMS signature, app ID, certificate, expiry, and
  non-debug distribution settings are verified. The owner entered the password
  and selected both files manually. Actual build signing and TestFlight device
  acceptance are unverified; upload automation is not configured.

## Windows: One Command

Close this project's Unity Editor, but the normal game may remain open:

```powershell
.\unity\tools\Test-Windows.ps1
```

Run from the repository root. Requires Unity **6000.6.0f1**, Windows Build
Support, an activated Unity license, and .NET SDK **10**. The script also finds
a project-local SDK in `unity/.tools/dotnet`; that folder is not committed.
It restores hash-pinned native dependencies for Windows Editor reference tests
only. The player itself uses the managed assignment solver on every platform.

The default test build goes to `unity/Builds/PipelineCheck/Windows`, separate
from the desktop shortcut's build. It does not close an existing game or change
the shortcut. To rebuild the normal executable, first close it and use:

```powershell
.\unity\tools\Test-Windows.ps1 -Output Builds/Windows/LittleGeorgies.exe
```

The gate fails on model assertions, compilation/build failure, missing/failing
runtime reports, or native solver DLLs accidentally included in the player.
Runtime tests use isolated smoke save folders, not the user's scenario.
`unity/Artifacts/pipeline.json` records the source revision, assembly hash, build
identity, model results, and all nine runtime reports. PNGs and logs are beside
the reports. Every build embeds its identity in `Resources/BuildInfo.json` and
logs it at startup. Local dirty revisions are labeled as such.

## Fast Checks and GitHub

```powershell
dotnet run --project unity/tests/CoreChecks --configuration Release -- unity/Artifacts/core-checks.json
```

The console runner compiles the actual game model sources, not a separate
implementation. It tests society progression, integer tax/bid/rent accounting,
VCG externalities, canonical ties, zero bids, maximum sizes, and rejection of
invalid input. Seeded small cases use an independent exhaustive oracle.
The Windows Editor also runs existing serialization/migration checks and
compares the managed solver with OR-Tools at all 101 tax rates.

`.github/workflows/unity-checks.yml` defines the license-free core suite on
Windows, Linux, and macOS, retaining JSON evidence. It is **manual-only**, with
an explicit `run_checks` confirmation defaulting to false. Pushes and pull
requests do not run it. It needs no Unity license or signing secrets. Once the
owner authorizes running CI, automatic triggers and required merge checks can
be enabled separately. Do not give public pull requests a self-hosted Windows
runner or Apple credentials.

The workflow is currently only on the setup branch. GitHub's manual-dispatch
interface requires the workflow on the default branch; merging it is a separate
future action, not part of setup-only publication.

## Unity Build Automation: First iOS Target

Use the existing Unity Cloud project and saved read-only SSH connection above.
The Unity directory is `unity`, not the repository root. The following settings
were saved and reopened in the cloud UI on 2026-09-22. They are also a recovery
recipe for target `ios-testflight-manual`, not evidence of a successful build.
Do not enable paid services or a new cloud plan without owner approval.

| Setting | Value |
| --- | --- |
| Target name | `iOS TestFlight - Manual` |
| Repository | `git@github.com:wclark/little-georgies.git` |
| Branch | `setup/unity-ios-pipeline` |
| Project subdirectory | `unity` |
| Platform | iOS, device build (not Simulator) |
| Unity | `6000.6.0f1`, matching `ProjectVersion.txt` |
| Builder | macOS Tahoe, Xcode `26.5.0`, Apple-Silicon editor |
| Machine | STANDARD: 4 vCPU, 16 GB RAM, 512 GB storage |
| Scene | `Assets/Scenes/Settlement.unity` |
| Pre-export method | `CloudBuild.PreExport` |
| Development build | Off for the signed TestFlight target |
| Signing/export | Apple Distribution, App Store Connect distribution |
| Saved credential | `Little Georgies App Store 2026` |
| Auto-build / repeating schedule | Both off |
| Auto-cancel | Off |
| Upload XCArchive / Fastlane upload hooks | Off / empty |
| Unity Test Framework option | Off; shared model checks run in the pre-export hook |

The exact Unity version was available and selected explicitly, with automatic
version detection off. Do not silently substitute a different editor. Xcode
26.5.0 meets the Xcode 26-or-later floor in
[Apple's current upload requirements](https://developer.apple.com/news/upcoming-requirements/).
Recheck SDK requirements before the first actual upload.

Saved non-secret target environment variables and the cloud-provided number:

- `LG_BUNDLE_ID`: the registered App ID, `org.georgist.littlegeorgies`.
- `LG_VERSION`: marketing version, initially `0.1.0`.
- `BUILD_NUMBER`: provided by Build Automation; never reuse an uploaded number
  for the same version. Coordinate numbering if a cloud target is recreated.

The pre-export hook requires a bundle ID and build number, configures IL2CPP,
iPhone/iPad, full-screen landscape rotation, and iOS 15 minimum, prepares the bootstrap scene,
runs shared model and migration checks, then embeds revision/build metadata.
New builds open the dossier-based settlement on both platforms. The separate
Economy Admin remains available inside it. Windows-native plugins are restricted to Windows Editor
and cannot be shipped in the iOS player. The cloud build needs no OR-Tools restore.

Hook configuration follows [Unity's build-script documentation](https://docs.unity.com/en-us/build-automation/advanced-build-configuration/run-custom-scripts-during-the-build-process).

## Apple Signing and TestFlight

The Developer membership, bundle ID, and App Store Connect app record are ready.
The new App Store record has Apple's initial 1.0 store-version draft; the first
TestFlight marketing version is planned as 0.1.0. Store submission is separate.
Unity's saved signing credential contains the distribution certificate **with
its private key** (`.p12`), its password, and the matching App Store distribution
provisioning profile. A certificate alone without its private key is insufficient.
Never paste keys into chat or commit them. Ignore rules cover `.p12`, `.p8`, and
`.mobileprovision` files.

Obtain explicit owner approval before creating a signing identity or granting
Unity access to its private key. Store local signing material outside Git and
restrict access to the owner. The target was saved using **Save configuration**;
never use **Save and build** during setup-only mode. Keep automatic and
scheduled triggers off. Saving a target does not
verify signing, compilation, or device behavior.

The owner approved this signing identity and Unity storage on 2026-09-22.
The owner could not access the agent-created AppData path from their PowerShell
session. The verified owner-only handoff directory is now
`%USERPROFILE%/Documents/LittleGeorgies-Signing`. It contains the Unity
handoff files: `LittleGeorgies-Distribution.p12`,
`LittleGeorgies-AppStore.mobileprovision`, and `p12-password.dpapi`.
The original private-key backup was not copied to Desktop or Documents.
DPAPI recovery requires the same Windows account; this is not yet an independent
credential backup. Do not print the password into logs/chat, create a plaintext
password file, or move private signing material into the repository.

For manual Unity password entry, the owner can run this in their own Windows
PowerShell session. It decrypts directly to the clipboard, without printing the
password or writing a plaintext file. Paste only into Unity's P12 password field,
then clear the clipboard and any clipboard-history entry containing the password.
Do not run this in a shared session or send the result to chat.

```powershell
Add-Type -AssemblyName System.Security
[Text.Encoding]::UTF8.GetString([Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes("$env:USERPROFILE\Documents\LittleGeorgies-Signing\p12-password.dpapi"), $null, 'CurrentUser')) | Set-Clipboard
```

Follow [Unity's signing guide](https://docs.unity.com/en-us/build-automation/sign-build-artifacts/sign-an-ios-application)
and [Apple's upload requirements](https://developer.apple.com/help/app-store-connect/manage-builds/upload-builds).
The first signed IPA completed Apple processing on 2026-09-23. The owner approved
a Developer-role App Store Connect API key named `Little Georgies Upload` for
uploads from this PC only. Apple team keys cover all apps in the account; the
local helper additionally restricts uploads to this app's ID and bundle ID.
The `.p8` is in the same owner-only signing directory, not in GitHub or Unity.
Moving that key into a cloud service requires separate owner approval.
No script submits an app for App Store review or publishes a release.

### Manual Upload from Windows

`tools/upload-testflight.py` uses Apple's documented
[Build Upload API](https://developer.apple.com/documentation/appstoreconnectapi/builduploads).
It needs Python 3.11+ and `cryptography`, not Xcode on Windows. Obtain the IPA
from the successful Unity build's artifact menu and keep it under ignored
`Builds/iOS/<build-number>`. Never reuse an uploaded version/build number.

Example from the repository root, replacing the key ID and issuer with the
values in App Store Connect's API page (neither is the private key):

```powershell
python -B unity/tools/upload-testflight.py `
  --ipa unity/Builds/iOS/1/LittleGeorgies-0.1.0-1.ipa `
  --key "$env:USERPROFILE/Documents/LittleGeorgies-Signing/AuthKey_<KEY_ID>.p8" `
  --key-id <KEY_ID> --issuer <ISSUER_ID> `
  --report unity/Artifacts/ios-upload-1.json
```

The default is a read-only preflight or status check. Add `--upload` once to
create and transfer a new build. Keep the generated report: it records upload
IDs and the local SHA-256 without credentials or signed storage URLs. Subsequent
runs with that report read status instead of making duplicate uploads.

The helper verifies each server-received part's MD5 ETag before committing the
file. Apple's optional `sourceFileChecksums` field is omitted; the first upload
rejected a SHA-256 value there. If all parts transferred but final commit failed,
`--commit-existing` verifies and commits that same file. Partial-transfer and
lost-report recovery require inspection, not deleting the report and retrying.
Do not log a JWT, private key, or signed upload URL.

Run offline helper tests with `python -B unity/tools/test_upload_testflight.py`.
After processing, use App Store Connect's TestFlight page to complete any
owner-approved export-compliance answer and assign the build to the intended
internal group. The existing `Clarks` group contains the owner's account.
TestFlight delivery is separate from App Store release submission.

## Device Acceptance Gate

Record build number and device/OS with every result. Install from TestFlight on
both iPad and iPhone before calling a mobile build verified:

- Cold launch and landscape rotation; content respects cutouts and safe areas.
- Select Georgies/plots, edit values with the software keyboard, dismiss it,
  move the tax slider, and scroll without losing focus or input.
- Confirm example totals: at 10% tax, value 46, tax 4, rent 16, surplus 26.
- Rapid edits retain coherent results and the last edit wins.
- Save, suspend/resume, force-close, relaunch, and verify saved values/tax.
- Test offline; inspect device logs for IL2CPP, file IO, or background-task errors.
- Check small text, touch targets, memory, and sustained input responsiveness.

The existing three-column admin UI is a desktop prototype, not yet an approved
phone layout. Desktop keyboard shortcuts and the Files-folder action are not a
mobile sharing experience. Dossier/hex-map redesign, touch/safe-area refinement,
mobile diagnostics sharing, App Store icon review, privacy/export-compliance
answers, and store metadata remain separate work. Automated desktop aspect-ratio
checks do not replace these device checks.
