# M10 classifier — partial independent constants check

**Codex review; Claude spot-check pending**

Revision50138aea, 2026-10-21. Independent check of F1 in
20261021-m10-classifier-settling.md against shipped native instructions and
current OffTreads.cs. PASS for the six reported constant mismatches. Other
classifier/report rows remain pending; this is not whole-path settlement.

Current M10-001: IMPLEMENTATION_GAP, “Off-treads classifier
CheckAndUpdateTreadsState: gate, inputs, thresholds, branches, 250 ms debounce
commit and every consequence”. Current evidence includes A1..A7
0x511E00..0x5121F8, threshold pools0x5122A0..0x5122BC and Radians operators
0x84CC90/0x84CC0A/0x84CD12. M10 is INVENTORY_APPROVED (2026-10-16).
The frozen inventory prints rounded decimals; its cited native thresholds
already own this obligation. This correction implements those thresholds,
without changing inventory, evidence, policy or record status.

| Constant | Native exact bits | Current decimal |
| --- | --- | --- |
| Face high f32 | 3FF5BE0B, movw/movt511E66/6A | 1.91986f |
| Face low f32 | BFB2B8C2, movw/movt511EE6/EA | -1.39626f |
| Level f32 | 3F490FDB, pool5122BC | 0.785398f |
| Sim back center f64 | 3FFAEB8260000000, pool5122A0 | 1.68250 |
| Physical back center f64 | 3FF4CDE840000000, pool5122A8 | 1.30027 |
| Back half width f64 | 3FD0C15240000000, pool5122B0 | 0.261799 |

Read literal bytes directly using ELF address mapping. Physical selection is
511E80..E8C (nonzero flag advances eight bytes). Pitch widens f32->f64 at E7C;
subtraction E96, absolute EFC, half-width load/compare511FAE..FBA preserve
f64. Level loads5122BC at51222C and compares absolute f32 at512230. Face
threshold construction uses the checked Radians owner, not plain comparisons.
Current OffTreads.cs54..66 uses the decimals above; its arithmetic consumers
219..225 preserve the relevant widths, so only literals need correction here.

Independent arithmetic witness: pitch bits3FC7F1E1 gives1.562069058418274.
abs(pitch-native physical center)<=native half width is true; the current
center/width comparison is false. This confirms a branch-input defect, without
claiming a full packet/hardware experiment or all downstream results. Builder
must add literal-bit boundary regressions and retain the existing branch/timing
logic. The report's broader cancel/detach/owner findings are NOT released here.
No test run, production edit, inventory change or acceptance by this review.
