# Economy Admin: Value, Tax, Bid, Rent, and Surplus

Open **Economy admin** in the Windows game, press **F4**, or launch
`LittleGeorgies.exe -lg-economy`. The normal desktop shortcut still starts the
one-Georgie orchard game. The admin desk pauses it without changing its state.
This is an independent economic sandbox. Allocations, rents, and taxes are not
yet applied to the live village.

## Values and Results

- Select a Georgie at left and edit their whole-apple value for each plot.
  A Georgie can receive **at most one plot**, even if they value several.
  Zero means no value for that plot. Combination values are not enabled here.
- **Plots** edits land names/types and adds or removes plots. Types are descriptive:
  preferences come from each Georgie's values, not automatic type bonuses.
  New plots and Georgies start with zero values. Removal asks for confirmation.
- The middle editor shows **Value / Tax / Bid** for each plot. Tax and Bid are
  read-only previews; editing Value updates both.
- **Run auction** finds the exact maximum-bid allocation and each Georgie's rent.
  Value or inventory edits also refresh automatically. Select a result row to inspect the
  rent calculation, individual taxes, and allocation without that Georgie.
- One **Tax rate** slider sets a whole-percentage rate from 0 to 100. Values and
  tax-rate changes trigger a background auction after a short debounce.
- The summary shows **Total value / Total tax / Total rent / Surplus**, with no
  Total Bid figure. Individual rows retain **Value / Tax / Bid / Rent / Surplus**.
  Tax is subtracted from value to determine the bid; rent is subtracted from bid
  to determine surplus. There is no LVT control or rent-tax calculation.
- Current results stay visible while refreshing. Editing remains enabled, the
  complete results panel swaps in one frame, and its scroll position is preserved.
  Superseded solves never overwrite newer inputs. The status identifies pending
  updates or invalid inputs; on an error, the last successful snapshot remains.
  Figures, names, counterfactuals, and tax rates within that snapshot stay coherent.

## Tax Accounting

Georgies bid their value net of tax. The input values are not overwritten.
The auction and every counterfactual run on these adjusted bids, not gross values.
At zero tax, bids equal values. Tax is charged only on awarded plots, based on
their gross value. Private land ownership and LVT are deferred.

```text
tax_i_for_plot = round_half_up(value_i_for_plot * tax_rate)
bid_i_for_plot = value_i_for_plot - tax_i_for_plot
surplus_i = value_i - tax_i - rent_i
          = bid_i - rent_i
```

Unallocated Georgies have zero value, rent, and tax. Aggregate figures are the
sums of individual figures. Total value is the value of the selected allocation,
not the sum of all cells in the value editor. "Surplus" is the Georgies' net amount,
not social welfare after transfers.
The sandbox models no public spending benefit.

Tax is rounded per Georgie and plot to the nearest whole apple, with halves
rounded up. For example, a value of 15 at 10% tax produces tax 2 and bid 13.
The auction and all counterfactuals use these actual integer bids. VCG rent is
then an exact integer difference; it is not independently rounded afterward.
Surplus and every summary amount are whole apples too. Totals sum the individual
amounts, rather than rounding the aggregate. Negative surplus is not clamped.
The same tax rate applies to every Georgie and plot. Rounding can create new
ties, change the selected allocation, or reduce small bids to zero before 100%.
At 100%, all bids are zero and no plots are allocated, so value, rent, tax, and
surplus totals are zero. This is a specified bidding model with entered gross
values and at most one awarded plot per Georgie, not a full ownership economy.
There is no hard apple budget, recurring collection, or treasury destination here.

## Auction Mechanism

Each plot is indivisible, with at most one assigned Georgie. Each Georgie receives at most
one plot. An empty allocation is permitted, and zero bids remain unused.
Production winner determination uses the MIT-licensed managed
[HungarianAlgorithm](https://github.com/vivet/HungarianAlgorithm) implementation,
pinned in `Assets/ThirdParty`. The same code runs in every player platform.
Google's OR-Tools 9.15.6755 CP-SAT remains a Windows Editor reference for parity
tests and combination-auction research. The admin desk only sends singleton offers.

The objective is total adjusted bid, not gross value or total rent. If `B` is
the optimum total bid, `b_i` is the winning bid of Georgie `i`, and
`B_without_i` is the best total bid achievable with that Georgie absent:

```text
rent_i = B_without_i - (B - b_i)
```

This is the Clarke-pivot VCG external-cost amount, labeled **rent** in the game.
It does not charge the winning value or run independent auctions for each plot.
See the [Northwestern VCG example](https://www.kellogg.northwestern.edu/faculty/weber/decs-452/vcg.htm).

Every solve must establish the exact optimum; timeouts produce an error and no
new results, retaining the previous successful display. The managed assignment
algorithm is exact; the editor-only CP-SAT path additionally requires `Optimal`.
Counterfactuals remove all offers of the absent Georgie. For a losing Georgie,
the existing optimum remains feasible and optimal, so it is reused exactly.

Ties preserve maximum total bid, then favor earlier ordinal Georgie IDs and
offer IDs (the plot IDs in this editor). Names, plot types, and input list ordering do not break ties.
Counterfactuals use the same canonical rule.

## Example

Four plots: Hilltop (`R1`), Riverside (`R2`), West orchard (`O1`), East orchard (`O2`).
Unlisted values below are zero. Example starts at zero tax.

| Georgie | R1 | R2 | O1 | O2 | Wins | Value | Rent | Surplus |
| --- | ---: | ---: | ---: | ---: | --- | ---: | ---: | ---: |
| Mara | 12 | 10 | 0 | 0 | R2 | 10 | 7 | 3 |
| Ada | 11 | 8 | 0 | 0 | R1 | 11 | 9 | 2 |
| Nia | 8 | 7 | 0 | 0 | None | 0 | 0 | 0 |
| Sol | 0 | 0 | 15 | 12 | O2 | 12 | 0 | 12 |
| Ivo | 0 | 0 | 13 | 9 | O1 | 13 | 3 | 10 |

At zero tax, total value is **46**, total tax **0**, total rent **19**, and surplus **27**.
With 10% tax, the allocation is unchanged:

| Georgie | Value | Tax | Bid | Rent | Surplus |
| --- | ---: | ---: | ---: | ---: | ---: |
| Mara | 10 | 1 | 9 | 6 | 3 |
| Ada | 11 | 1 | 10 | 8 | 2 |
| Nia | 0 | 0 | 0 | 0 | 0 |
| Sol | 12 | 1 | 11 | 0 | 11 |
| Ivo | 13 | 1 | 12 | 2 | 10 |
| **Total** | **46** | **4** | - | **16** | **26** |

## Saves and Reproducibility

**Save** writes `scenario.json`; **Load** reads it and re-solves, ignoring stored
results. The game loads saved inputs on its next launch. **Files** opens the folder:

`%USERPROFILE%\AppData\LocalLow\Georgist_org\Little Georgies\EconomyAdmin`

Successful runs also write `last-run.json`, with all input values, the tax rate,
adjusted winning bids, allocations, counterfactual bids, tax accounting, solver version, and timing.
Only complete results for the latest inputs refresh this snapshot. Edits do not overwrite
`scenario.json` until Save. Files are atomically replaced, retaining a `.bak`.
A solved snapshot can be loaded as `scenario.json` to reproduce the experiment.

Documents use `FormatVersion: 6`, with `Taxes` and `TaxReport`.
`Result.AmountScale` is 1 for admin auctions. `LandAward.Value` is the original
whole-apple valuation; `LandAward.Bid` is the actual whole-apple bid.
The solver's legacy names `Welfare`, `Payment`, and `Revenue` mean total bid,
rent, and total rent, in those same scaled units. Its `Utility` is bid minus rent,
equivalent to the UI surplus after converting units. `Result.ValueTaxPercent` records the bid basis.
`TaxReport` provides the final accounting: `Value`/`TotalValue` are whole apples;
`Hundredths` fields are bids/rents; `TenThousandths` fields are taxes/net surplus.
These legacy report units are retained, but all amounts are exact multiples of
100 or 10,000 respectively. Total bid remains in the audit data, not the summary UI.
`TaxReport.TotalTaxTenThousandths` sums individual `TaxTenThousandths` amounts.
`Taxes.ValuePercent` retains its original field name for saved-input compatibility;
it is the single tax rate. New documents contain no rent-tax settings or amounts.
Reports have explicit `Solved` and per-Georgie `Won` flags because Unity represents
null inline objects as empty objects. Ignore results when `Solved` is false.
Saving while changed inputs are still pending saves those inputs with `Solved: false`,
without attaching the older displayed result. `last-run.json` stays at the last
complete result until the current refresh succeeds.

Old version-0/1 documents are migrated in memory. Explicit single-plot values are
retained, missing values become zero, and duplicate singleton offers use their
highest value. Combination bids are excluded with a visible notice; their values
are never split or inferred. The original saved file is untouched unless Save
is pressed, which also retains it as the backup. Legacy rates default to zero.
Version-2/3/4/5 inputs and value-based tax rates also remain loadable. Old rent-tax
settings are ignored. All saved results are ignored and recomputed under the
current whole-apple rules. Loading does not rewrite the source file.

## Limits and Verification

- Windows x64 player integration is tested. The active auction no longer needs
  native solver packaging. iOS IL2CPP, signing, and real-device testing still
  require a cloud build; see [BUILDING.md](BUILDING.md).
- Up to 12 plots and 16 Georgies, with values 0-1,000,000. Names are 1-32
  characters; stable IDs use letters/numbers/underscores/hyphens up to 40.
- A 15-second total solve budget prevents indefinite admin freezes. The solver
  runs off the main thread. Editing remains enabled; one solve runs at a time and
  pending edits coalesce to the newest inputs. Obsolete results are discarded.
- `Assets/Editor/AuctionChecks.cs` compares outcomes and rents to an independent
  exhaustive oracle on 120 seeded cases (80 generic and 40 individual-plot),
  including adjusted-bid allocations and counterfactuals, plus known examples,
  ties, invalid input, timeout, migration, exact net accounting, and all 101
  tax percentages to verify adjusted bids, rents, and individual accounting.
  Boundary cases cover below/at/above half an apple and rounding-created ties.
- `tools/Build-Windows.ps1` restores pinned dependencies and runs society and
  auction checks. `tools/Smoke-Windows.ps1 -AuctionOnly` exercises native UGUI
  controls, including continuous coherent results during refresh, latest-edit
  precedence, editing during solves, scroll preservation, pending saves, ordered
  value/tax/bid columns, one tax slider, automatic edits, zero bids, legacy save/load, and
  village isolation, with rendered screenshots and slider bounds/pixel checks.
  These are automated checks, not human or physical-tablet playtesting.
