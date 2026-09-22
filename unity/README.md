# Little Georgies: Unity Prototype

An illustrated, animated 2D village with a policy-led economy. This project sits beside the existing web game; it does not replace or deploy it.

## Play

Open the **Little Georgies** desktop shortcut or `Builds/Windows/LittleGeorgies.exe` after building. Both start with one happy gatherer, no apples, and an orchard in an open field. There are no houses, baskets, workshop, market, or specialist controls in the opening. Starting another settlement asks for confirmation.

Run `tools/Install-DesktopShortcut.ps1` to install or repair the shortcut. Its icon is the existing happy Little Georgie portrait, also embedded as the Windows application's icon. The shortcut deliberately has no sandbox argument.

The eight-person specialist sandbox is an explicit debug option: launch the executable with `-lg-village`. The sandbox button is hidden during normal play.

- Pause/play and 1x, 3x, 6x controls set the pace. A day lasts 32 simulation seconds.
- Click a moving Georgie to inspect their mood, activity, personal apples, basket, home, and recent happiness.
- Policies are standing rules, not individual orders. They take effect next dawn.
- Space pauses; Escape closes the individual inspector; F3 exports a debug snapshot under the game's persistent-data folder.
- **Economy admin** (or F4) opens the individual-plot VCG land-auction sandbox. One tax-rate slider reduces value to bid, rounding tax to the nearest whole apple (halves up). The auction uses these integer bids, so rents and surplus are whole apples too. The middle editor shows value/tax/bid, individual results show value/tax/bid/rent/surplus, and the summary omits total bid. LVT is deferred. Edits refresh in the background, retaining the previous results until the complete replacement is ready. It pauses the village. [AUCTION.md](AUCTION.md) describes the rules and reproducible snapshots.
- Closing or restarting discards the settlement. Auction scenarios have their own independent save/load.

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
- **Specialists:** five gatherers and 30 harvested apples, with the same wellbeing conditions, unlock Chief Henry, farmers, and a builder. A simple outdoor weaving bench appears. No free apples, baskets, or homes are granted.
- **Housing:** equipping every farmer with a made basket unlocks house construction. No hut appears until builders finish its work. A built home belongs to an individual before it provides rest benefits.
- Gatherers produce 2 apples when happy or tired, 1 when broken.
- Farmers produce 3 when happy, 2 when tired, and 1 when broken. An individually owned basket adds 2, except when broken.
- Builders make 2 baskets when happy and 1 otherwise. A house needs 35 work units: 7 per happy day or 5 per tired day. Broken builders can only make baskets.
- Everyone consumes one apple at dinner when available. Fed workers become tired; fed resters or housed workers become happy; unfed Georgies become broken.
- Broken Georgies keep working at minimum output. There is no defeat state or day limit.
- Each Georgie retains up to ten completed days of happiness. Village happiness is the proportion of happy observations across those individual windows.
- Natural growth requires at least 50% recent happiness and cumulative harvest milestones. Henry retains his identity through specialization.
- Under shared storage, harvest goes directly to the common store. Under personal harvests, farmers retain output minus the working chief's levy. Fractions carry across days.
- Personal food is eaten first. Shared food is freely available under shared storage; under personal harvests it requires the hunger-relief policy and an active chief.
- Finished baskets and homes enter common inventory. The working chief assigns them to eligible individuals under the corresponding policies. Ownership, not aggregate inventory, grants benefits.

## Architecture

- `Assets/Scripts/Society.cs`: frame-independent C# model; snapshots policies at dawn and resolves production and dinner once.
- `VillageGame.cs`: simulation clock, input, inspection, debug export, and native smoke runner.
- `VillageView.cs`: painted scene, authored movement paths, sprite animation, and activity labels.
- `VillageHud.cs`: UGUI policy controls and individual inspection.
- `Assets/Scripts/Economy/LandAuction.cs`: value-tax-adjusted bids, portable exact single-plot assignment, and VCG rents/counterfactuals. CP-SAT combination research is Windows Editor only.
- `Assets/Scripts/Economy/IndividualLandValues.cs`: individual-plot mode and legacy value migration.
- `Assets/Scripts/Economy/AuctionTaxes.cs`: a single tax on value and exact individual/aggregate surplus accounting.
- `Assets/Scripts/Economy/AuctionDesk.cs`: independent editable auction UI and JSON snapshots.
- `Assets/Editor/AuctionChecks.cs`: auction checks against an independent exhaustive oracle.
- `Assets/Editor/PrototypeBuild.cs`: deterministic economy checks, scene setup, and Windows packaging.

## Validation

`tools/Smoke-Windows.ps1` runs both the specialist sandbox and the normal opening at desktop and 4:3 resolutions, capturing PNGs and JSON reports under `Artifacts`. Opening checks cover picking, eating, sleeping and resting outdoors, the named band, no free goods at specialization, and the first actually built house. Later progression is advanced programmatically through the same economy methods. Sandbox checks cover animation, movement, a day transition, pause, dropdown selection, checkbox changes, and levy changes. Pointer events are sent through UGUI; these are not physical mouse/touch actions. Images are rendered from the scene and canvas to a render texture because hidden-window screen capture is unreliable. Pixel checks reject blank or missing-shader frames, followed by visual inspection. This is automated runtime verification, not a human playtest or an actual tablet-device test.

The same smoke script also runs the auction desk at both resolutions. Use
`tools/Smoke-Windows.ps1 -AuctionOnly` for just those runs. They exercise values,
individual plot values, plot/Georgie editing, exact rents, tax-rate slider pointer
events, background re-auctions, split taxes, no double rent-tax deduction, zero bids,
continuous coherent results, latest-edit precedence, pending saves, scroll preservation, invalid input, legacy save/load, counterfactual
inspection, and isolation from the village. Slider bounds and pixels are checked
in addition to control state and tax accounting.

## Deliberate Limits

This is a first playable slice, not a full port. The village has no barter market, applied land/rent system, asset confiscation, specialist-stage population growth, settlement save/load, sound, or tablet build. Land auctions currently run only in the independent admin sandbox; their results do not change the village. Output levies here are **not** land-value taxation. The orchard background stays fixed; the weaving bench and up to eight visible huts are separate sprites revealed by actual progression and construction. Additional homes remain accounted for in the economy. All roles currently share a prototype animation sheet with role-colored markers.

See [DESIGN.md](DESIGN.md) for the design direction and [ART.md](ART.md) for image provenance.
