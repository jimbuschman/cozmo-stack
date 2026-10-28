# SPARKS-KNOCKOVER-1 — Claude check

Manager, 2026-10-28. Handoff `jobs/trial-m13/HANDOFF-SPARKS-KNOCKOVER-1.md` (branch 53ad547a; implementation
7e3b096a; Codex full gate 6535/6535 on 8eae8413). Verdict: **PASS. Nothing proposed or settled.**

## Spot checks

- **OnSelected 0x005B1518..0x005B16D6.** The hard branch runs when requested spark +0x60 == 0x55 or the requested
  soft byte +0x64 == 0 (0x005B15D8..0x005B15E2), which matches the corrected +0xD8 reading (M13 Q8). It pushes the
  driving animations, then SmartPushIdleAnimation 0x213, then StartLoopingBackpackLights, sets +0xB5 = 1 and sends
  HardSparkStartedByEngine with the u32 from +0x60 (wrapper 0x005B171C). After that come
  SmartDisableReactionsWithLock and the optional delegate OnSelected. `NativeSparkedActivity.Selected` has the same
  order.
- **Backpack pattern (static at 0x005B156A..0x005B15C8).** It sets colours 0 and 4 to one literal and 1–3 to the
  other, with all off colours the first. On periods are 0/360/360/360/0 and off periods 0/1110/1110/1110/0.
  Transitions are zero and the offsets are 0/0/120/240/0. `HardLights()` matches.
- **Update 0x005B186C..0x005B18CC.** A reaction moves state 0/1 to 2. In state 2 or 3 it runs CheckIfSparkShouldEnd.
  In state 3 with a reaction it runs CompleteSparkLogic, then ResetLightsAndAnimations, and sets state 5; the
  delegate Update follows. `NativeSparkedActivity.Update` matches. The delegate is refused before admission
  (`CanExecute`), so its absence is visible.
- **End deadline 0x005B18EE..0x005B190E.** `start + window + (-1e-5f)` is compared `<= now` as two f32 additions.
  This matches `Deadline` (bits 0xB727C5AC).

## Not settled

The handoff makes no whole-record proposal. M15-059 (other Spark owners, the Wait/hard-max/delegate branches),
M13-014, M8-005, M15-006/058 (the parent picker after Complete) and M15-071 (reward amount and raw broadcast) stay
open.

Named uncertainties carried forward:
- The builder's affected run failed once on the old startup `RobotValidated` 5 s wait. The cause is unknown.
- The earlier manual Navigation KnockOver AggregateException also has no known cause.

Behaviour not finished: parent activity picking after Complete (M15-006) is still MISSING.
