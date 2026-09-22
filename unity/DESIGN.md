# Direction: A Society You Can Watch

## Presentation

The chosen direction is an illustrated 2D village with animated Georgies, desktop first and landscape tablets later. The village itself is the primary screen. Panels appear for policies and inspection; cards are no longer the organizing metaphor.

The first settlement begins with a single Georgie in an orchard and open meadow. No buildings, equipment, market, or specialist controls are present. Population milestones introduce a named gathering band, then specialization introduces weaving and collective policy. Equipping the farmers with baskets unlocks housing, and only completed construction places a hut in the scene. Chief Henry remains recognizable as the society grows, while group summaries help the player understand the whole economy.

## Core Loop

1. Observe daily work, deliveries, dinner, rest, and the distribution of resources.
2. Change a small number of standing policies.
3. Watch several days unfold at a controllable pace.
4. Inspect an individual's circumstances when the aggregate result needs explaining.

The player steers institutions, not every footstep. Rest, food access, equipment, and housing should visibly change how people live.

## First Playable Question

Can the village produce enough food yet still leave people hungry?

Shared storage and personal harvests provide a first comparison. An apple levy and hunger relief make transfers explicit. Baskets and housing belong to individuals: a surplus in a warehouse is not the same thing as useful access.

The prototype simplifies production and transfers to make that causal loop testable. Every day's policy is captured at dawn; animation then presents the scheduled work, and the economy resolves delivery and dinner at consistent times.

## Next Design Decisions

- Replace output-levy experiments with a fuller choice of property and land-use institutions. Distinguish land rent from tax on productive work.
- Define trade, prices, offers, and the relationship between private and common basket production before adding a market.
- Decide whether work priorities are village-wide, role-wide, or individually overridable.
- Extend the visible hut and weaving-bench sprites into a fuller equipment/ownership presentation; give specialists distinct animation art.
- Add new residents, capacity constraints, saved games, and a readable multi-day ledger.
- Test touch selection, hit areas, text, performance, and safe areas on actual tablets before promising tablet readiness.

## Guardrails

No fixed day limit or all-broken defeat. No invisible common inventory bonus: a person must hold a basket or home to benefit. Recent happiness must recover after bad policy. Aggregate statistics must remain traceable to individual histories.

Art, pathfinding, and economic state are separate so the simulation can be tested without the editor. For this authored, obstacle-free clearing, movement uses Unity transforms and fixed waypoints; a larger village should use a supported navigation solution instead of extending this into a custom pathfinding engine.
