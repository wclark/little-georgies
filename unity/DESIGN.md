# Direction: A Society You Can Inspect

## Presentation

The updated direction is a dossier/card-driven game with a hexagonal land grid,
desktop first, then iPad and iPhone. Animated walking and task scenes are no
longer the primary experience. The player changes policy, reads the resulting
allocation and wellbeing, and can override an individual's plan. Portraits retain
the supplied Little Georgie identity and mood expressions.

The first settlement begins with a single Georgie among orchards and open fields.
No buildings, equipment, or market are present. Population milestones introduce a
named gathering band; specialization introduces weaving and collective policy.
Equipping farmers with baskets unlocks housing. Houses and baskets must be built
and assigned to an individual before they confer benefits. Chief Henry remains
recognizable as society grows.

## Core Loop

1. Inspect the current day, people, land allocation, and a small set of totals.
2. Change a small number of standing policies.
3. Advance one day or let standing policies run several days automatically.
4. Inspect an individual's circumstances when the aggregate result needs explaining.

The player steers institutions, not footsteps. Rest, food access, equipment, and
housing change the portraits, individual records, and economic outcomes.

## First Playable Question

Can the village produce enough food yet still leave people hungry?

Shared storage and personal harvests provide a first comparison. Value tax, VCG
rent, and hunger relief make transfers explicit. Baskets and housing belong to
individuals: a surplus in a warehouse is not the same as useful access.

The first dossier slice uses the existing exact single-plot VCG engine: potential
daily output is value, rounded value tax reduces the bid, and each harvesting
Georgie can win at most one plot. Rent is the externality on other bidders. All
figures are whole apples. Surplus is value minus tax minus rent, before dinner.
Tax and rent enter common apples; there is no separate LVT or private landowner
income. A coherent forecast must finish before the day can settle.

## Next Design Decisions

- Add role-level aggregate dossiers with median-mood portraits, while retaining individual drill-down.
- Explore tighter land supply and meaningful differences in plot suitability before expanding the fixed seven-hex map.
- Introduce residential-lot values and landownership deliberately; do not treat current house inventory as privately owned land.
- Define trade, prices, offers, and the relationship between private and common basket production before adding a market.
- Add richer policy effects and autonomous responses beyond the current food/rest heuristic and persistent personal work overrides.
- Add specialist-stage growth, a readable multi-day economic ledger, and explicit house/basket transfer controls.
- Refine phone-specific navigation, touch targets, and diagnostic sharing, then test on real iPad/iPhone hardware.

## Guardrails

No fixed day limit or all-broken defeat. No invisible common inventory bonus: a person must hold a basket or home to benefit. Recent happiness must recover after bad policy. Aggregate statistics must remain traceable to individual histories.

The economic state stays independent of Unity so accounting and progression can
be tested without graphics. No custom auction algorithm or navigation engine is
introduced. The original animated presentation remains only as legacy regression
coverage, not the normal game screen. Windows checks do not establish iOS readiness.
