# Little Georgies: Unity Prototype

A dossier-driven settlement with a hexagonal land table and a policy-led VCG economy. This project sits beside the existing web game; it does not replace or deploy it.

## Play

Open the **Little Georgies** desktop shortcut or `Builds/Windows/LittleGeorgies.exe` after building. A new settlement starts with one happy gatherer, no apples, and only orchards and open fields. There are no houses, baskets, or market. The new policy-village save is restored automatically on later launches. Starting another settlement asks for confirmation.

Run `tools/Install-DesktopShortcut.ps1` to install or repair the shortcut. Its icon is the existing happy Little Georgie portrait, also embedded as the Windows application's icon. The shortcut deliberately has no sandbox argument.

- **Land** shows seven selectable hex plots and their current daily assignments. No walking or sprite animation is created during normal play.
- **Georgies** shows compact portrait rows. Five fit without scrolling at the tested desktop and tablet-shaped sizes. Selecting one opens their dossier.
- **Policies** sets the tax rate, automatic rest, and (after specialization) food sharing, relief, baskets, and housing.
- Each Georgie can follow policy or retain a personal work/rest plan. Builders can choose baskets or, once unlocked, houses. Broken Georgies cannot rest or build houses.
- **Next day** settles one day. **Run days** lets standing policies advance a day every four seconds; **Pause days** stops it. Opening Auction admin pauses automatic days.
- **Land values** in a dossier edits that Georgie's potential productivity for each plot, in small whole-apple amounts. The plot dossier compares values, taxes, and bids. Non-harvesters do not bid.
- **Auction admin** (or F4) retains the separate, full-size experimental auction editor. Its scenarios and saves remain independent. [AUCTION.md](AUCTION.md) describes its controls and snapshots.
- The policy village saves automatically under `Application.persistentDataPath/PolicyVillage/settlement.json`, with the previous save in `.bak`. Existing `EconomyAdmin` saves are not migrated or overwritten.

## Open in Unity

See [BUILDING.md](BUILDING.md) for the Windows test gate, GitHub model checks,
iOS cloud-build settings, and the signing/TestFlight checklist.

First run `tools/Install-AuctionSolver.ps1` to restore the pinned solver dependencies. In Unity Hub, choose **Add > Add project from disk** and select this `unity` directory. Use the installed **6000.6.0f1** editor, open `Assets/Scenes/Settlement.unity`, and press Play.

The **Little Georgies** editor menu has economy validation and Windows build commands. The scene contains a bootstrap component; the village and UI are created at runtime.

For a reproducible command-line build:

```powershell
.\tools\Build-Windows.ps1
```

Unity must have access to the existing desktop license. Close this project's editor before invoking the batch build. Windows Editor reference checks use hash-pinned OR-Tools/Protobuf binaries; players use the vendored MIT managed assignment solver and include its license. The cloud macOS editor does not need the Windows dependencies.

## Rules in This Slice

- **Lone gatherer:** pick apples, eat in the clearing, and sleep outdoors. Rough overnight sleep does not replace taking a rest day.
- **Gathering band:** new happy gatherers arrive at cumulative harvest milestones of 4, 10, 18, and 26 apples, provided recent happiness is at least 50% and nobody went hungry. The first Georgie becomes Henry when the second arrives.
- **Specialists:** five gatherers and 30 harvested apples, with the same wellbeing conditions, unlock Chief Henry, farmers, and a builder. No free apples, baskets, or homes are granted.
- **Housing:** equipping every farmer with a made basket unlocks house construction. A built home belongs to an individual before it provides rest benefits.
- Gatherers' base productivity is 2 apples when happy or tired, 1 when broken. Farmers' base is 3 happy, 2 tired, and 1 broken; their own basket adds 2 except when broken.
- Each plot's value is daily productive output, adjusted for that Georgie's affinity, terrain, and editable preference. Current values are capped at 9, and broken output at 1. Open fields have lower default productivity than orchards. Changes in mood or equipment can therefore change values.
- The existing exact solver allocates at most one plot per harvesting Georgie and at most one Georgie per plot. Resting Georgies, chiefs, and builders do not compete for harvest land in this slice.
- Whole-apple tax is rounded from value (halves up); bid equals value minus tax. VCG is solved on those bids. The winner harvests the value, tax and rent go to common apples, and surplus is value minus tax minus rent. There is no LVT or private landowner model.
- Zero bids can remain unallocated. A 100% tax can stop land production. Surplus is before food consumption, not a prediction of saved apples.
- Builders make 2 baskets when happy and 1 otherwise. A house needs 35 work units: 7 per happy day or 5 per tired day. Broken builders can only make baskets.
- Everyone consumes one apple at dinner when available. Fed workers become tired; fed resters or housed workers become happy; unfed Georgies become broken.
- Broken Georgies keep working at minimum output. There is no defeat state or day limit.
- Each Georgie retains up to ten completed days of happiness. Village happiness is the proportion of happy observations across those individual windows.
- Natural growth requires at least 50% recent happiness and cumulative harvest milestones. Henry retains his identity through specialization.
- Under shared storage, the entire harvest is pooled. Under personal harvests, Georgies retain their after-tax, after-rent output. The former fractional output-levy path remains only in the legacy regression model and is not applied again to land-auction production.
- Personal food is eaten first. Shared food is freely available under shared storage; under personal harvests it requires the hunger-relief policy and an active chief.
- Finished baskets and homes enter common inventory. The working chief assigns them to eligible individuals under the corresponding policies. Ownership, not aggregate inventory, grants benefits.

## Architecture

- `Assets/Scripts/Society.cs`: frame-independent C# model; snapshots policies at dawn and resolves production and dinner once.
- `SettlementDesk.cs`: the normal entry screen, safe-area-aware UGUI, hex selection, dossiers, background forecasts, and local saves.
- `Economy/SettlementEconomy.cs`: connects the society model to land values, VCG allocation, and daily tax/rent settlement.
- `VillageGame.cs`: bootstrap and native smoke capture. `VillageView.cs` and `VillageHud.cs` are retained only for explicit legacy regression runs; normal play does not instantiate them.
- `Assets/Scripts/Economy/LandAuction.cs`: value-tax-adjusted bids, portable exact single-plot assignment, and VCG rents/counterfactuals. CP-SAT combination research is Windows Editor only.
- `Assets/Scripts/Economy/IndividualLandValues.cs`: individual-plot mode and legacy value migration.
- `Assets/Scripts/Economy/AuctionTaxes.cs`: a single tax on value and exact individual/aggregate surplus accounting.
- `Assets/Scripts/Economy/AuctionDesk.cs`: independent editable auction UI and JSON snapshots.
- `Assets/Editor/AuctionChecks.cs`: auction checks against an independent exhaustive oracle.
- `Assets/Editor/PrototypeBuild.cs`: deterministic economy checks, scene setup, and Windows packaging.

## Validation

`tools/Smoke-Windows.ps1 -DossierOnly` runs the new interface at 1600 x 900,
1280 x 960, and 1600 x 740. It exercises hex/dossier navigation, editable values,
continuous tax refresh, manual and automatic days, personal plans, earned
specialization, five-row layout, save/resume, and admin isolation. The portable
and Unity checks also validate settlement accounting at all 101 tax rates.
These are automated Windows checks, not physical iPhone/iPad playtests.

The full `tools/Smoke-Windows.ps1` also retains the legacy animated-village regression runs and the independent auction desk runs at desktop and 4:3 resolutions. Captures and reports are under `Artifacts`. Pointer events are sent through UGUI; these are not physical mouse/touch actions. Images are rendered from the scene and canvas to a render texture because hidden-window screen capture is unreliable. Pixel checks reject blank or missing-shader frames, followed by visual inspection.

The same smoke script also runs the auction desk at both resolutions. Use
`tools/Smoke-Windows.ps1 -AuctionOnly` for just those runs. They exercise values,
individual plot values, plot/Georgie editing, exact rents, tax-rate slider pointer
events, background re-auctions, split taxes, no double rent-tax deduction, zero bids,
continuous coherent results, latest-edit precedence, pending saves, scroll preservation, invalid input, legacy save/load, counterfactual
inspection, and isolation from the village. Slider bounds and pixels are checked
in addition to control state and tax accounting.

## Deliberate Limits

This is a first playable dossier slice. Land allocation now affects the settlement,
but there is no private landownership, residential-lot bidding, barter market,
asset confiscation, specialist-stage population growth, or sound. The hex layout
is fixed; neighbors do not yet affect value and there are no bundle bids. Standing
rest policy uses the existing conservative food heuristic, not a strategic bidder
AI. The small-number balance and policy incentives need human playtesting. The
saved Unity iOS configuration remains dormant; no iOS build or physical-device
validation was performed for this revision.

See [DESIGN.md](DESIGN.md) for the design direction and [ART.md](ART.md) for image provenance.
