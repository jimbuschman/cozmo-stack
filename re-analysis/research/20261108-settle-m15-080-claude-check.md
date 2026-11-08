# M15-080 BehaviorAcknowledgeFace: settled EQUIVALENT_IMPLEMENTATION (Claude check, manager, 2026-11-08)

**Verdict: M15-080 settled EQUIVALENT_IMPLEMENTATION.**

The body was built and checked on 2026-10-29 (`research/20261029-acknowledge-face-1-claude-check.md`), and left open
for one unknown (B5). VOICE-CANNED-NAME-DELIVERY-1 (merged 3b114ab3) closed it, and the settlement was missed in that
merge's check.

## B5, the greeting clock: closed

- **Native, re-disassembled:** the activity adapter tail at 0x005A4FA2..0x005A4FE0. For activity 1 (Freeplay):
  `vldr s0,[r4,#0x54]; vcmpe s0,#0; poppl` returns when +0x54 is not below zero. Otherwise it stores the current time
  at +0x54 (0x005A4FD6). For any other activity it writes 0x55 to +0x60 and +0x58.
- **Reader, re-disassembled:** BeginIteration at 0x006029B2..0x00602A10 computes `now - [+0x54] <= 60.0f`, `[+0x54] >= 0`,
  `!+0x12D` and `!HasTurnedTowardsFace`, as recorded.
- **Stack:** `SparkManagerInput.HighLevelAdapterTail` writes `GreetingTimeField54` only when it is below zero. NaN does
  not write, matching `poppl`. Production calls it from `NativeBehaviorRuntime` on every activity selection.
- **Test:** `CannedNameDeliveryTests.StockGreetingPlaysAuthoredNameThroughSharedNativeAudio`.

## Remaining items

- **libm:** GetRelativeBodyAngleToLookAtPose (0x0054B584) uses atan2f in the GetBestFaceToTrack cost, so the record is
  EQUIVALENT_IMPLEMENTATION, not EXACT_SOURCE.
- **Forced trigger, not supported (manager exclusion):** SetupForceTriggerBehavior 0x0060C744..0x0060C888 is reached only
  through the ExecuteReactionTrigger game message. Its only app sender is the debug BehaviorPane
  (`unity/scripts/csharp/BehaviorPane.cs:202` through `Robot.cs:1736`). That is phone-app debug machinery, which the
  manager may exclude.
- **Whole path:** every other part has its own record. These are TurnTowardsFaceAction CheckIfDone (M13-014), its frame
  counts (M14-002), speech (M14-006), the strategy (M10-003), the IBehavior base (M8-001), the objective packer (M7-019)
  and GreetingSayName (M7-020).

## Also in this commit

- **A one-off failure, queued for Codex:** `M12RVisBuildTests.M13_028_TheFlipNeverWaitsOnTheQueuedCarryLift` timed out
  once (2 min) in the full suite on d4cb953c. It passed 3/3 alone and 5/5 alone under CPU load. Codex should add the
  flip task's state to that test's timeout message and look for shared state with tests that run in parallel.

## Progress

23 records settled (was 22).
