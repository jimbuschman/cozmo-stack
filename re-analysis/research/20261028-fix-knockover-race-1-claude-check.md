# FIX-KNOCKOVER-RACE-1 — Claude check (manager, 2026-10-28)

**Verdict: PASS as a diagnostic package. The original fault stays UNKNOWN.** Merged 324e61db (code 1b93722d).
**Settled: M13-041 and M12-052 (EXACT_SOURCE).**

## The fix job

The intermittent AggregateException in NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates did not
recur (one 1659-test affected run, 20 targeted runs). Codex did not invent a cause. The test keeps its unchanged
success predicate and now saves the full fault trace, the handler journal and the inner exception to a failure
artifact before teardown. No production behaviour, timing or timeout changed; the five production files change only
fidelity tags and two diagnostic strings. Accepted: the failure will be diagnosable when it recurs.

## M13-041 (both Pop instances) settled

Checked by Claude:
- predicate 0x00570928..0x00570980: DidFailToUse(id, 3, time 30.0f, pose, distance 60.0f, PI), then
  DockingComponent (robot+0x280) CanPickUpObjectFromGround. Matches `PopEligible`.
- get-in 0x005C7496..0x005C757A: +0xD9 or +0xD8 set, or target +0x120 == previous +0x128 → perform; else
  sequential {TurnTowardsObject(target, PI, false, false), TriggerLiftSafe(0x18A, 1, true, 0, 60.0f, false)};
  callback 0x005C7B52 sets previous = target only on result 0. Matches.
- Select 0x00552730..0x0055287E: target-in-robot-parent pose fails → warning, 0x03000004; selected dock action
  +0x80 = 9; target z − robot z > 33.85f → 0x03000004 (NaN continues); carrying (+0xD0)+8 != −1 → 0x03000017;
  else 0. Matches.
- Verify 0x0055295C..0x00552A72 (`research/20261028-pop-a-wheelie-1-claude-check.md`).
- completion 0x0055267E..0x0055269C: first ID +0x7C, halfword 0x0001 (count 1, flag false), four ID words unwritten;
  the M1-034 guard withholds the full public payload and keeps the defined fields. Matches.
- SparksPopAWheelie binding (`research/20261028-sparks-pop-a-wheelie-1-claude-check.md`).
DriveToPop 0x0055D1E8..0x0055D26C, the pre-dock lock/cliff 0x005C7BC8..0x005C7C10 and the reset 0x005C76B0..0x005C76F0
rest on Codex's independent checks. M13-015 (retry/result/reset) stays partial.

## M12-052 settled

GetCompletionUnion 0x00554AE0..0x00554AFA and the ctor store 0x0055467C, checked in
`research/20261028-roll-records-claude-check.md`. NativePlaceGroundRunner now carries the `// fidelity: M12-052` tag.
