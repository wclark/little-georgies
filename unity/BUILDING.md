# Build and Test Pipeline

The website/S3 workflow is separate and unchanged. The Unity pipeline has three
gates: portable model tests, a Windows player with runtime checks, and a signed
iOS build followed by a real TestFlight device test. Passing the first two does
not establish that the third works.

## Current Setup (2026-09-22)

**Setup-only mode:** the owner requested no builds or test compilations.
Do not run local build/test scripts, dispatch GitHub checks, trigger Unity Cloud
builds, or upload a TestFlight artifact until explicitly authorized. Auto-build,
scheduled builds, and automatic GitHub triggers must stay off. The successful
test results below are from the earlier pipeline implementation, not a new run.

- Unity Cloud project: [Little Georgies](https://cloud.unity.com/organizations/1375991457548/projects/22a9185f-609f-4365-b527-d62132adc8a8).
- Organization: `1375991457548`; project: `22a9185f-609f-4365-b527-d62132adc8a8`.
- Audience: general audience, not primarily directed at children, confirmed by the owner.
- Local Windows pipeline passed; see `VERIFICATION.md` and `Artifacts/pipeline.json`.
- GitHub workflow is authored but not yet published/run remotely. Unity source
  is still local-only pending permission to publish it on a new repository branch.
- Build Automation's free tier is available. After anonymous HTTPS failed,
  the owner approved a repository-only SSH deploy key. GitHub key `164131885`
  (`Unity Build Automation (read-only)`) is verified `read_only: true`; Unity
  saved `git@github.com:wclark/little-georgies.git` successfully. No account-wide
  OAuth access or write access was granted.
- Cloud build target, Apple signing, upload automation,
  and TestFlight device acceptance are not configured/verified yet.

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
identity, model results, and all six runtime reports. PNGs and logs are beside
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

## Unity Build Automation: First iOS Target

Use the existing Unity Cloud project above, then connect this GitHub repository using
read-only repository access where supported. The Unity directory is `unity`,
not the repository root. Start with manual cloud triggers to control cost.
Do not enable paid services or a new cloud plan without owner approval.

| Setting | Value |
| --- | --- |
| Repository | `https://github.com/wclark/little-georgies.git` |
| Branch | The reviewed branch containing the Unity project |
| Project subdirectory | `unity` |
| Platform | iOS, device build (not Simulator) |
| Unity | `6000.6.0f1`, matching `ProjectVersion.txt` |
| Builder | macOS with a currently Apple-supported Xcode/iOS SDK |
| Scene | `Assets/Scenes/Settlement.unity` |
| Pre-export method | `CloudBuild.PreExport` |
| Development build | Off for the signed TestFlight target |
| Signing/export | Apple Distribution, App Store Connect distribution |
| Trigger | Manual until the first device build is verified |

Confirm that the exact Unity version is available in the cloud. If not, choose
one supported version for both local and cloud builds and re-run Windows tests;
do not silently substitute a different editor. Record the chosen Xcode version
after checking Apple's current upload requirements.

Set non-secret target environment variables:

- `LG_BUNDLE_ID`: the exact registered App ID, proposed `org.georgist.littlegeorgies`.
- `LG_VERSION`: marketing version, initially `0.1.0`.
- `BUILD_NUMBER`: provided by Build Automation; never reuse an uploaded number
  for the same version. Coordinate numbering if a cloud target is recreated.

The pre-export hook requires a bundle ID and build number, configures IL2CPP,
iPhone/iPad, landscape rotation, and iOS 15 minimum, prepares the bootstrap scene,
runs shared model and migration checks, then embeds revision/build metadata.
Cloud test builds open directly in Economy Admin. Windows normal builds still
open in the orchard. Windows-native plugins are restricted to Windows Editor
and cannot be shipped in the iOS player. The cloud build needs no OR-Tools restore.

Hook configuration follows [Unity's build-script documentation](https://docs.unity.com/en-us/build-automation/advanced-build-configuration/run-custom-scripts-during-the-build-process).

## Apple Signing and TestFlight

The account owner must complete Apple activation/agreements and confirm the
team. Register the bundle ID and create the Little Georgies App Store Connect
record. For the first signed build, put the distribution certificate **with
its private key** (`.p12`), its password, and the matching App Store distribution
provisioning profile in Unity's signing credential storage. A certificate alone
without its private key is insufficient. Never paste keys into chat or commit
them. Ignore rules cover `.p12`, `.p8`, and `.mobileprovision` files.

Follow [Unity's signing guide](https://docs.unity.com/en-us/build-automation/sign-build-artifacts/sign-an-ios-application)
and [Apple's upload requirements](https://developer.apple.com/help/app-store-connect/manage-builds/upload-builds).
First verify that Build Automation creates a correctly signed `.ipa`. Then
configure a TestFlight upload step with an App Store Connect API key in cloud
secret storage. Upload automation and tester distribution are intentionally not
enabled until the account, app record, signing, and first artifact are verified.
No script submits an app for App Store review or publishes a release.

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
