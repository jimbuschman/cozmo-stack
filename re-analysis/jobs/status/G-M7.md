DONE 2026-09-29 06:35 -05:00

Job G-M7: folded the M7-021 reaction-field extraction into M7-behaviour and built it.

## Commits
- `7f0863d` G-M7: fold the M7-021 extraction into M7-behaviour; build the on-charger pickup gate (verifier PASS, suite 1845/1845). (Rebased onto the M9 research push; the pre-push gate then ran the rebased suite green at 1859/1859.)
- `40b2702` G-M7: claim job.

## Record
**M7-021 stays IMPLEMENTATION_GAP** (moved from RECOVERABLE_GAP; the source read is now exhausted).
- Built: `CozmoSensors.OnChargerContacts` (robot+0x338) and the ReactToPickup `+0x338` gate (`ReactBehavior.preempt` = `ShippedBehaviors.PickupOnChargerGate`): the reaction completes without playing and logs `BehaviorReactToPickup.OnCharger` when the robot is still on the charger contacts, while the strategy still triggers on `InAir` alone (factory lambda 0x0060DDCE). The seven-valued `OffTreadsState`, the on-face lift angle against 45.0f, the filtered accel magnitude (`0x37C = 0.05*new + 0.95*old`) and `WaitForLambdaAction` (the stack's `WaitUntil`) were already correct and were not changed.
- Open (named in the record's `unresolved`): the log-only state-name helper `0x005c0ca8` (its `+0x58` store and `Behavior.TransitionToState` Info log line are not reproduced; a full `+0x58` scan found no behaviour-changing reader) and its M7 live users ReactToCliff (0x00604f90/0x00605110, M7-019) and DriveOffCharger (0x005c0b74/0x005c0be2/0x005c0de0, M13-017).

## Inventory correction C2 (Appendix I)
Rows M7-021-1..7 were checked in the shipped `.so` (verifier: rows 1-7 + a 27-address sample). Rows **2, 3 and 4 failed** and were corrected by a read-only extraction:
- row 2: shaken read is 0x00609284 (not 0x006092B8); DriveOffCharger read is 0x005C0DB6 (not 0x005C0DB8); on-side read 0x00608D88 was omitted.
- row 3: the IS_ON_CHARGER extraction is 0x00512AAE and the `SetOnCharger` call 0x00512AB4 (not 0x00512A6C..0x00512A8C); `SetOnCharger` calls `SetOnChargerPlatform`, which writes `+0x34A`, not `+0x338`.
- row 4: the on-face reaction compares `+0x300` against **45.0f** (literal 0x00608B54), not zero; `+0x300` is the raw `RobotState+0x2c` stored straight at 0x0051296A. The stack's existing 45.0f comparison is correct.
New row C2h records the `BehaviorReactToPickup::UpdateInternal` 1/2 return contract and the `+0x338` gate. `source_investigation_exhausted` is now true for M7-behaviour (no live RECOVERABLE_GAP left; M7-022 is non-live).

## Files in the job's write scope
- `cozmo-stack/src/Cozmo.Robot/Sensors.cs` (the robot-state derivation for +0x338).
- `cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs` (the record's `location`; pickup gate).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/M7BehaviorTests.cs`.
- `re-analysis/inventory/M7-behaviour.md` and `.approved.json`, `re-analysis/fidelity_manifest.json` (M7-021 + the review block), `re-analysis/FIDELITY_GAPS.md`.
- The input report `re-analysis/research/20260929-M7-021-reaction-fields-extraction.md` is unchanged; the corrections live in inventory Appendix I.

## Gates
- `python re-analysis/tools/fidelity.py --check`: PASS (371 records).
- `@cozmo-verifier`: code diff PASS after the record was kept IMPLEMENTATION_GAP (the first pass FAILed only the `EXACT_SOURCE` raise); the fixed record re-verified PASS.
- full suite `dotnet test cozmo-stack/Cozmo.sln`: **1845 passed / 0 failed / 0 skipped**; `AssetPresenceTests` green. (One earlier full run flaked on `NavigationTests.PopAWheelieRetriesWithTheRetryAnimationWhenTheDockFails`, which passes in isolation and on the re-run; not related to this diff.)

## For the integrator
- **Stale cross-references:** M7-015 and M7-019 `unresolved` still call M7-021 a "live RECOVERABLE_GAP". M7-021 is now IMPLEMENTATION_GAP; update those two records' `unresolved` at the next M7-behaviour pass (they are outside this job's write scope).
- **Cleanup queue:** the state-name helper `0x005c0ca8`'s `+0x58` store and `Behavior.TransitionToState` Info log line on channel `"Behaviors"` (non-behavioural; queued). M7-019's row for `0x6050f0` omits the `"PlayingStopReaction"` name at `0x6050d0`.
- **M7-019/M7-015 dependencies on M7-021 are resolved** (the field names and the pickup gate are recorded and built).