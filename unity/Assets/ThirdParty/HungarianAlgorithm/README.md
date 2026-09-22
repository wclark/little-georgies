# Managed Assignment Solver

Source: https://github.com/vivet/HungarianAlgorithm

Pinned upstream commit: `c73520ac92a246e149bf66aae174841436d6bfc3`.
File: `HungarianAlgorithm/HungarianAlgorithm.cs`.
License: MIT, retained in `Assets/StreamingAssets/ThirdParty/HungarianAlgorithm-LICENSE.txt`
so every player includes it.

Only adaptation: a block namespace instead of C# 10's file-scoped namespace,
for Unity's C# 9 compiler. No algorithm changes. The library mutates its cost
matrix; the game supplies a fresh matrix for each call. The adapter adds dummy
columns for unassigned bidders and explicit canonical tie-breaking. Integer
costs stay within the game's validated one-million-apple per-offer bound.

The game uses this managed solver on all player platforms. Native OR-Tools is
retained only for Windows Editor combination-auction research and cross-checks.
