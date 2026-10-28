# STACK-BLOCKS-1 — Claude check

Manager, 2026-10-28. Handoff `jobs/trial-m13/HANDOFF-STACK-BLOCKS-1.md` (branch 243ee31c; implementation adc91a08
integrated as 70c3d660; Codex full gate 6589/6589 on 9e525950). Verdict: **PASS. Nothing settled.** Built on Sol
medium; it predates the Terra worker default.

## Spot checks

- **GetRotatedParentAxis<'Z'> 0x005507A0..0x00550854.** It reads matrix row 2 as f32 (x, y, z). The candidate is
  ±1 by the sign of x, replaced by ±2 (sign of y) when |y| > |x|, and by ±3 (sign of z) when |z| exceeds the chosen
  component's magnitude. The comparisons are strict, so ties keep the earlier axis, and NaN takes the negative sign.
  `RotatedParentAxis.GetNativeZ` matches.
- **Top, carrying (axis variant) 0x005705D0..0x00570628.** When carrying ([robot+0x284]+8 != -1), the located
  carried object must be this object and its local pose axis must be 3, both evaluated. When not carrying, it tail
  calls the no-carry check.
- **Top, not carrying 0x0057052C..0x0057058E.** CanPickupNoAxisCheck must be 1. Then IsUnlocked(9) != 1 returns true;
  otherwise the root-relative pose axis must be 3. `Top(obj, true)` matches, including the literal unlock direction.
- **Bottom 0x00570632..0x0057066A.** CanUseAsStackBottomHelper(obj, intention 4) must be 1, then the local axis must
  be 3. `Bottom(obj, 4, true)` matches.

## Records

No existing record proposed. The handoff asks that the ordinary 4/2 instance join the Stack lifecycle record rather
than get its own ID. M15-076, added earlier today, is scoped to the SparksStackBlock instance only, and its title is
frozen. Widening it to the whole shipped class needs the operator. Until then, the ordinary instance's own path has
no record.

Inherited MISSING items (PlayAlone admission and scoring, parent transitions, executable finder) and the UNKNOWN
startup and manual Navigation faults carry forward.

## Addendum: M15-076 widened (operator approved here, 2026-10-28)

M15-076's title, evidence and `unresolved` now cover both shipped instances (SparksStackBlock and ordinary
StackBlocks). M15-freeplay was re-approved. The record is still IMPLEMENTATION_GAP.
