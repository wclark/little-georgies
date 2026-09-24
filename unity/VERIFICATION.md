# Verification

## 2026-09-23: TestFlight Upload

- The owner downloaded Unity's IPA to the configured Downloads location on
  `D:`. Copied it to ignored `Builds/iOS/1/LittleGeorgies-0.1.0-1.ipa`; both
  copies have SHA-256
  `326bc93973a431b0fb0153f821cb6bad17c3d43fd70dac95818f620c42e25f69`.
- Inspected the IPA's actual Info.plist: bundle `org.georgist.littlegeorgies`,
  version `0.1.0`, build `1`, iPhone/iPad, iOS 15 minimum, full-screen landscape,
  Xcode 26.5 / iPhoneOS 26.5 SDK. The embedded profile matches the app/team,
  is App Store distribution with debugging disabled, and expires 2027-09-22.
  Its CMS signature verified cryptographically; this was not an independent
  Mach-O code-signature or certificate-chain validation.
- With explicit approval, created the Developer-role `Little Georgies Upload`
  API key. The owner personally enabled API access and accepted Apple's terms.
  Moved the downloaded private key into the owner-only signing folder outside
  Git; no key was sent to Unity, GitHub, or another cloud service.
- Uploaded all 55,343,657 bytes in 11 parts through Apple's public Build Upload
  API. Apple's received ETags match local MD5s for every part. The optional
  SHA-256 checksum field was rejected on initial commit; the same upload was
  successfully committed without that optional field after part verification.
  No duplicate upload or cloud rebuild was needed.
- Apple reports upload **COMPLETE**, with no errors, warnings, or informational
  issues. Upload/build ID: `4ec56dbd-7575-44cd-8e40-7bda6c3ee186`. The owner
  selected and saved "None of the algorithms mentioned above" in Apple's
  encryption form; the earlier **Missing Compliance** gate is cleared.
- Added a repeatable PC-only upload helper with read-only default, app/bundle
  restrictions, part validation, explicit commit recovery, and local report.
  **Seven offline test cases passed**, including signature checks, destination
  and range rejection, corrupt/missing ETags, and safe commit behavior.
- Verified version **0.1.0 (1)** in the existing `Clarks` internal group with
  status **Testing**, one tester, and a 90-day expiration. The owner's tester
  entry is **Invited**. Saved playtest notes covering touch/layout, dossier and
  hex selection, work/rest, economy controls, progression, and save/resume.
- No public release or external testing was submitted. No extra cloud build
  was run. Physical iPad/iPhone installation and gameplay remain unverified.

Evidence: `Artifacts/ios-upload-1.json`, the local IPA, and the App Store Connect
TestFlight build page. This supersedes the upload-pending status below.

## 2026-09-23: First iOS Cloud Build

- The owner requested TestFlight playtesting on their iPad. Published the dossier
  revision to `setup/unity-ios-pipeline` as
  `8bdeaee37c26532e67d11166c28c7a6ccd19e2bf`; left the unrelated root README edit
  unstaged. No main merge, website deployment, or GitHub Action was requested.
- Reran portable preflight: **6,577 assertions passed**. Evidence is
  `Artifacts/core-checks-ios-preflight.json`.
- Reopened the saved target and verified manual-only scheduling, the exact
  editor/Xcode versions, STANDARD Mac machine, and saved App Store credential.
  Added full-screen landscape configuration and opaque icon import for iOS;
  build metadata now also accepts Unity's `BUILD_REVISION` variable.
- Manually triggered [iOS build #1](https://cloud.unity.com/organizations/1375991457548/projects/22a9185f-609f-4365-b527-d62132adc8a8/cloud-build/buildtargets/ios-testflight-manual/builds/1)
  pinned to that commit. Unity reports **Success**, completed September 23 at
  15:08 Pacific, 6m23s including a 3m03s wait; billable build time 3m19s.
  Output is 52.78 MB, bundle ID `org.georgist.littlegeorgies`.
- The artifact menu offers an IPA, build reports, full Xcode log, and dSYM.
  Chrome blocked the automated IPA download (`ERR_BLOCKED_BY_CLIENT`), so the
  package has not yet been inspected locally or independently signature-checked.
- App Store Connect requires a fresh owner sign-in. No TestFlight upload has
  occurred; upload credentials/automation, Apple processing, tester assignment,
  and physical-device acceptance remain pending. Cloud build success does not
  establish that the app is installable from TestFlight yet.

## 2026-09-22: Dossier Village (Desktop Updated)

- After signing setup, the owner authorized closing/rebuilding the local Windows
  game. No cloud build, GitHub Action, TestFlight upload, or website deployment
  was run. These game changes are local working-tree changes, not a cloud release.
- Normal startup now creates a dossier UI and selectable seven-hex land table,
  with no `VillageView`, animated actors, or legacy HUD. Reused the supplied
  mood/role portrait assets without image edits.
- Connected the existing society model to the exact VCG solver. Each harvesting
  Georgie bids for at most one plot; integer value tax reduces bids, tax and rent
  enter common apples once, and daily harvest/food conservation is verified.
  Per-person plot preferences are editable in small whole-apple amounts.
- Added persistent individual plans, group work/rest controls, manual or automatic
  days, and separate automatic policy-village save/resume. Existing auction-admin
  scenarios remain isolated. No private landownership or LVT was added.
- Preserved one-person opening, earned growth and specialization, rolling happy
  histories, broken-worker restrictions, baskets, and individual housing benefits.
- `tools/Test-Windows.ps1 -Output Builds/Windows/LittleGeorgies.exe` passed on
  the installed desktop build. Portable checks passed **6,577 assertions**:
  538 society, 5,616 auction, and 423 settlement integration checks. Unity also
  passed 538 society, 12,360 auction, and 423 settlement checks (**13,321 total**).
- All **nine** native runtime reports passed: six existing village/auction
  regressions and the new dossier screen at 1600 x 900, 1280 x 960, and 1600 x 740.
  Dossier checks cover hex and individual navigation, value edits, continuous
  background results, latest tax edit, next-day and automatic progression,
  personal plans, five compact rows, save restoration and another settled day,
  admin isolation, text bounds, and tax-slider handle containment.
- Screenshots of the opening, policy desk, and five-Georgie view were inspected.
  A slider handle/text overlap found visually was corrected and reverified.
  This is automated Windows verification, not human balance testing or a
  physical iPad/iPhone playtest. Mobile navigation and touch refinement remain.

Evidence: `Artifacts/pipeline.json`, `Artifacts/core-checks.json`,
`Artifacts/build.log`, `Artifacts/DossierDesktop`, `Artifacts/DossierTablet`,
`Artifacts/DossierWide`, and the six legacy regression folders.

Desktop game assembly SHA-256:

`20012873B88EA02A191E518B203DBBFA0424F3C6DFA589180CDD7F82DA6B73B1`

## 2026-09-22: Setup-Only Publication

- With owner approval, published the Unity project and pipeline on
  `setup/unity-ios-pipeline` at `ead5042`; did not merge or change `main`.
- Removed automatic triggers from the new model-check workflow. It accepts
  manual dispatch only, with an explicit `run_checks` input defaulting to false.
  GitHub returned no runs for the setup branch after the push.
- Saved the Unity Cloud iOS target `ios-testflight-manual` using only
  **Save configuration**: exact Unity 6000.6.0f1, Xcode 26.5.0,
  macOS Tahoe, Apple-Silicon, project folder `unity`, and `CloudBuild.PreExport`.
  Reopened the saved target and verified its branch, scene, environment values,
  and signing credential. Auto-build, auto-cancel, and repeating schedules are
  off. Unity's project build history reports no builds, with 0 total rows.
  The settings and target link are recorded in `BUILDING.md`.
- Verified active Apple Developer membership and registered the explicit
  `org.georgist.littlegeorgies` App ID. No optional capabilities were selected.
  The owner accepted the separate App Store Connect agreement. Created app
  `6814987738`, Little Georgies (iOS, English US, SKU `little-georgies`), and
  verified its Prepare for Submission status in the Apps list.
- The owner approved Apple Distribution signing setup and Unity credential
  storage. Generated the RSA key and public CSR outside Git, protecting the
  private key with Windows DPAPI CurrentUser and an owner-only directory ACL.
  After the owner uploaded the CSR and downloaded the certificate, verified
  that its RSA public key matches the original request. Apple issued an active
  App Store provisioning profile scoped to `org.georgist.littlegeorgies` and
  this distribution certificate; both expire on 2027-09-22.
- Created and locally reopened a password-protected P12 containing the matching
  certificate and private key. The random password is stored only in Windows
  DPAPI-encrypted form. Verified the signing directory's ACL has exactly one
  full-control entry for the owner, no inherited entries, and the same owner.
  No plaintext password file was created. After the owner downloaded the profile,
  verified its CMS signature, exact app ID, matching certificate, future expiry,
  disabled debugging, and absence of ad-hoc/all-device provisioning. Kept a copy
  in the protected signing directory. Unity file upload automation could not open
  its picker, so the owner selected both files and entered the password manually.
- The owner's PowerShell could not resolve the AppData path. Prepared an
  owner-only `Documents/LittleGeorgies-Signing` handoff folder with only the
  encrypted P12, profile, and DPAPI-encrypted password, verifying file hashes and
  the exact ACL. The private-key backup was not copied. No plaintext secret was
  printed, written, or added to Git.
- Saved Unity credential `Little Georgies App Store 2026`. After saving the
  target, Unity displays the correct bundle ID, App Store profile type, matching
  Apple Distribution certificate, and 2027-09-22 expiries. Actual artifact signing
  is still untested. No TestFlight upload integration is configured.
- Static inspection only in this setup-only follow-up: no local compilation,
  model test run, Unity/player build, runtime smoke run, cloud build, TestFlight
  upload, or app release. Earlier test results below were not rerun.
- No website/S3 deployment or desktop executable changed. The unrelated root
  README edit remains outside the setup commit.

## Earlier 2026-09-22: Build Pipeline Foundation

- Created the Little Georgies Unity Cloud project for the owner's confirmed
  general-audience classification. No paid service, signing credential, app
  release, or TestFlight upload was enabled.
- With explicit owner approval, added a read-only repository deploy key and
  verified Unity saved the GitHub SSH connection. No repository write access
  or account-wide OAuth grant was needed.
- Added `tools/Test-Windows.ps1`: portable model gate, Unity validation/build,
  six player smoke runs, native-dependency exclusion check, and a combined
  JSON evidence manifest. Ran this exact command successfully end to end.
- Portable .NET runner passed **6,154 assertions**: 538 society and 5,616
  auction assertions. It compiles the actual production models without Unity
  or native DLLs and includes 200 seeded scenarios plus all 101 tax rates.
- Unity 6000.6.0f1 passed **12,898 assertions**: 538 society and 12,360 auction
  checks, retaining legacy migration/combination checks and comparing managed
  results against native OR-Tools at all 101 tax rates.
- The active single-plot auction now uses a pinned MIT managed Hungarian
  assignment solver. Whole-apple tax, bid-based VCG rent, canonical ties, and
  coherent background refresh are unchanged. Native OR-Tools plugins are
  Windows Editor only. The built player contains no OR-Tools/Protobuf DLLs;
  the managed solver's license is included in StreamingAssets.
- All six runtime smoke reports passed at 1600 x 900 and 1280 x 960: specialist
  village, orchard opening, and auction desk. Desktop and tablet-aspect auction
  captures were visually inspected. No exceptions were found in the run logs.
- Build identity is embedded and logged at startup; the combined report records
  revision `fb08651420863f946696c9723cc6a3fdc179ec91-dirty`, build timestamp,
  assembly SHA-256, and model/runtime results.
- Built separately at `Builds/PipelineCheck/Windows/LittleGeorgies.exe` without
  replacing the normal shortcut's executable or closing the user's game.
- Added GitHub Actions for license-free model checks on Windows/Linux/macOS,
  plus an iOS IL2CPP cloud pre-export hook and setup/device checklist in
  `BUILDING.md`. The workflow has not been pushed or run on GitHub. The hook
  compiles in the local editor, but no iOS export, Xcode archive, signing,
  TestFlight upload, or physical-device test has run. No mobile-readiness claim.
- The web/S3 deployment is unchanged. The planned dossier/hex-map redesign
  was intentionally not started as part of pipeline work.

Evidence: `Artifacts/pipeline.json`, `Artifacts/core-checks.json`,
`Artifacts/build.log`, `Artifacts/Desktop`, `Artifacts/TabletAspect`,
`Artifacts/OpeningDesktop`, `Artifacts/OpeningTablet`,
`Artifacts/AuctionDesktop`, and `Artifacts/AuctionTablet`.

Validation-build assembly SHA-256:

`48D0F04C35CE45138E3BD751A23011F47AF24896C234BA6D4996C88AA9ADBEA5`

## 2026-09-20: Whole Apples (Desktop Updated)

- Removed Total Bid from the summary, leaving a two-column layout of total
  value, total tax, total rent, and surplus. Per-plot previews and individual
  result rows retain Bid.
- Tax rounds per Georgie and plot to the nearest apple, with halves up. The
  auction and every counterfactual use value minus rounded tax as the actual
  integer bid. VCG rent is an exact integer difference, not a rounded display
  of fractional rent. Surplus and aggregate figures are whole apples too.
- **538 society assertions and 6,643 auction assertions passed (7,181 total).**
  The suite verifies all 101 rates against an independent exhaustive oracle,
  rounding boundaries, rounding-created ties, zero bids, maximum values,
  per-person accounting, and sums of individually rounded taxes.
- Both targeted Windows auction smoke runs passed at 1600 x 900 and
  1280 x 960. Checks include the four-summary layout, integer report amounts,
  Value/Tax/Bid column order, continuous background results, latest edits,
  scroll preservation, save/load, invalid input, and village isolation.
- Legacy format-2/3/4/5 inputs and rates load without rewriting their files;
  stored fractional results are ignored and recomputed. New snapshots use
  format 6 and auction AmountScale 1. Legacy report unit field names remain,
  with values that are exact multiples of 100 or 10,000.
- The 10% example now totals value **46**, tax **4**, rent **16**, surplus **26**.
  Screenshots were inspected at both resolutions. Native pixel/shader/slider
  checks passed, with no managed exceptions in the auction smoke logs.
- Unity 6000.6.0f1 rebuilt the existing desktop executable after confirming
  the game was closed. These are automated checks, not human/tablet playtests.
  No web or S3 deployment changed.
- The updated game reopened on the economy screen, responded normally, and
  produced a fresh solved format-6/scale-1 snapshot. Saved scenario inputs
  retained the same SHA-256 hash across startup.

Evidence: `Artifacts/build.log`, `Artifacts/whole-apple-checks.log`,
`Artifacts/AuctionDesktop`, and `Artifacts/AuctionTablet`.

Current desktop-build assembly SHA-256:

`1AD6319D187B96A5BF3FC30345FBEA2EB5F5548FD061C10E301F1CA9D5CA23BC`

## Earlier 2026-09-20: Single Tax (Desktop Updated)

- The middle editor shows **Value / Tax / Bid**; results show
  **Value / Tax / Bid / Rent / Surplus**. There is one **Tax rate** slider.
  Tax is based on value, bid is value minus tax, and surplus is bid minus rent.
  LVT controls, settings, and report calculations have been removed.
- **538 society assertions and 3,716 auction assertions passed (4,254 total)**,
  including all 101 tax rates, exact
  bid-based allocations and VCG counterfactuals, fractional amounts, 64-bit
  totals, and 120 seeded exhaustive-oracle scenarios.
- After the user closed the game and approved the update, Unity 6000.6.0f1
  rebuilt `Builds/Windows/LittleGeorgies.exe`. The existing portrait desktop
  shortcut was verified to target this updated build.
- All six automated native runs passed on the installed desktop build: the opening,
  specialist village, and auction desk at 1600 x 900 and 1280 x 960.
- Auction tests verify column order, preview calculations, a single tax slider,
  no LVT labels, and coherent results throughout background refresh: 3,397
  observed refresh frames on desktop and 3,499 on the tablet-shaped viewport.
  Latest-edit precedence, editing during solves, invalid-input recovery,
  scroll preservation, pending saves, and village isolation also passed.
- Format-5 snapshots contain only the value-based tax rate and a single tax
  amount. Version-2/3/4 scenarios with a legacy 99% rent-tax setting load and
  recompute correctly, ignoring LVT without changing the source save file.
- The 10% example is value **46**, tax **4.6**, bid **41.4**, rent **17.1**,
  surplus **24.3**. Screenshots were inspected at both resolutions, including
  the edited values and counterfactual detail. Nonblank, shader, slider pixel,
  and bounds checks passed. Tests use isolated save folders.
- This is scripted native Unity verification, not human or physical-tablet
  playtesting. No web source or S3 deployment changed.
- The updated game was reopened on the economy screen and verified responding.
  It published a fresh solved format-5 snapshot. The saved `scenario.json` hash
  was unchanged across startup. Final smoke logs had no managed exceptions.

Evidence: `Artifacts/build.log`, `Artifacts/AuctionDesktop`,
`Artifacts/AuctionTablet`, `Artifacts/Desktop`, `Artifacts/TabletAspect`,
`Artifacts/OpeningDesktop`, and `Artifacts/OpeningTablet`. The earlier separate
build's reports remain under `Artifacts/TaxPreview*`. Rules are in `AUCTION.md`.

Prior desktop-build assembly SHA-256:

`5CAEC19DF43F354D90807A7FA2A1F1A92EE6ED9CAB4C249903C47513D37DB7F0`

## Earlier 2026-09-20: Split Taxes and Continuous Background Refresh

- Unity 6000.6.0f1 built the Windows x64 game with separate **Value tax** and
  **Rent tax** columns and totals. Surplus is `value - value tax - rent`,
  equivalently `bid - rent`. Rent tax is a reported part of rent, not another
  bidder deduction. No landowner model was added.
- **538 society assertions and 3,667 auction assertions passed (4,205 total).**
  The 120 seeded oracle scenarios still verify exact allocation and counterfactual
  bids/rents. New checks cover all 101 rent-tax percentages, verifying unchanged
  bidder surplus and exact tax shares, including fractional rents and large totals.
- At 10% value tax and 20% rent tax, the sample has value 46, bid 41.4, rent 17.1,
  value tax 4.6, rent tax 3.42, and surplus **24.3**. Raising rent tax to 100%
  leaves surplus at 24.3. The prior additional-deduction result is superseded.
- All six Windows smoke runs passed: normal opening, specialist village, and
  auction desk, each at 1600 x 900 and 1280 x 960.
- Auction smoke runs inspect every yielded refresh frame for exactly one active
  results snapshot and tax/surplus cells consistent with its report. Results are
  retained during edits, solves, and invalid-input errors; no blank intermediate
  results panel is accepted.
- Native tests edit values during an active solve and supersede its captured
  input before it can publish. Only the newest value reaches the display.
  Controls remain interactable, and the results scroll position is preserved.
  Values, value taxes, and rent taxes all refresh automatically after debounce.
- Pending-save tests verify current inputs are saved as unsolved without an older
  result attached, and the last successful run stays intact until a current solve
  finishes. Completed version-4 snapshots carry coherent inputs, rates, bids,
  results, and tax reports. Legacy version-2/3 scenarios re-solve under the new
  rule without rewriting their source file.
- Other UI checks cover separate tax cells, no double rent-tax deduction,
  invalid-input recovery, zero-bid allocation, bid previews, plot/Georgie edits,
  deletion confirmation, plot-kind menus, save/load, counterfactuals, and village
  isolation. All smoke saves use isolated folders, not the user's scenarios.
- Eight current screenshots per auction run cover overview, refresh pending,
  split taxes, zero bids, individual edits, counterfactuals, inventory, and plot
  menus. Screenshots were visually inspected at desktop and 4:3 resolutions.
  Slider bounds, slider pixels, and nonblank/missing-shader checks passed.
- Inputs are exercised through UGUI field bindings and pointer events. This is
  automated native runtime verification, not physical mouse/touch playtesting.
- The executable location and portrait desktop shortcut behavior are unchanged.
  Updated `AUCTION.md` accompanies the build. No web source or S3 deployment changed.

Evidence: `Artifacts/build.log`, `Artifacts/AuctionDesktop`,
`Artifacts/AuctionTablet`, and the four village scenario folders below.
Fresh `smoke.json` reports include continuous-results, latest-edit, editability,
pending-save, scroll-preservation, and rent-tax inclusion flags. Older PNGs may remain.

Earlier game assembly SHA-256 (`Builds/Windows/LittleGeorgies_Data/Managed/Assembly-CSharp.dll`):

`8E75A764023AD9524D74F7E14DB176B4FAD8281487282C1784D0C400BD035CB3`

The auction is still independent of the live village. It has no landowner income
or treasury distribution model. Windows x64 is the packaged solver target;
human playtesting and physical-tablet testing remain open. Final smoke logs
contain no managed application exceptions or failed pixel checks.

## 2026-09-13 Baseline

### Completed

- Unity 6000.6.0f1 imported and compiled the project and built the Windows x64 player successfully.
- 538 model assertions passed, including the asset-free opening, growth without free grants, basket-first housing unlock, completed house construction, food conservation, broken-worker restrictions, rolling happiness recovery, and two 120-day policy runs.
- The actual Windows player passed four automated runs: the specialist sandbox and normal opening each at 1600 x 900 and 1280 x 960. Sandbox runs covered movement, animation, a day transition, pause, dropdown/checkbox/levy changes, and next-dawn policy snapshots.
- Opening runs verified one gatherer, no buildings or specialist controls, picking and eating apples, sleeping/resting outdoors, a named band, specialization without gifted goods, and the first built home. Later stage transitions use programmatic day advancement through the production model; they are not long-session human playtests.
- Captured and visually inspected the empty orchard opening, outdoor sleep, specialist and first-house scenes at desktop and 4:3 aspect ratios. Pixel checks reject blank and magenta missing-shader frames.
- The desktop shortcut resolves to the local Windows build in Bill's session, uses the Little Georgie portrait ICO, and has no sandbox argument. The ICO decodes at the portrait's original 128 x 128 dimensions. The same portrait is assigned as the Windows application icon.
- Existing web tests: 19 passed. Existing web content check passed. No web source, S3 objects, or deployment configuration changed.

Evidence is generated locally under `Artifacts/Desktop`, `Artifacts/TabletAspect`, `Artifacts/OpeningDesktop`, `Artifacts/OpeningTablet`, and `Artifacts/build.log`. Run the scripts in `tools` to recreate it. Native smoke screenshots render the scene and UGUI canvas offscreen; scripted UGUI pointer events are not physical mouse/touch playtesting.

Baseline game assembly SHA-256 (superseded by the build above):

`69460D04F98FB68E88CBB82AFF7F802585700B8E14DC3B8A45F60E565F0648EA`

### Remaining At That Date

Human playtesting, long-session native balance testing, actual touch-device testing, distinct specialist animation art, save/load, trade, land/rent rules, and expansion beyond the first specialist village remain open.

The graphics driver logs an unavailable D3D12 info-queue interface; rendering and runtime checks nevertheless pass. There were no managed application exceptions in the final smoke logs.
