# Claude check: M13-017 groundwork (ec4dc4a8), drive-off threshold tests (7cfb711d), B-M13-14 tests (2026-10-23)

Read-only cozmo-verifier pass, instructions read in libcozmoEngine.so; focused tests 13/13. **All three PASS.**

1. **Groundwork** `research/20261022-m13-017-groundwork.md` (Codex): narrowing N1-N4 confirmed (family {4} filter,
   literal at 0x00C4A878; vtbl+0x50 quad; robot quad +0x298 and Intersects; Charger ctor -> ActionableObject at
   0x004E9B76, vtable slot 0x0101E214 -> ObservableObject::GetBoundingQuadXY). Rows C2, C4, C5, M5, W1, L1 confirmed.
   Its conclusion holds: configuration (pending/rebuild/notify), the charger map content (feeds
   BehaviorInteractWithFaces::CanDriveIdealDistanceForward 0x005C2420) and the SetPose cache invalidation each have an
   effect; none may be recorded as no effect. No Thumb-bit or boundary errors.
2. **Drive-off threshold tests** `M13DriveOffThresholdTests.cs` (Codex): real RobotState bytes through the port,
   admission and frame match asserted, thresholds read from the wire. Engine values independently derived (accumulator
   -1 -> 0 and 50; 50.0f at 0x00512E8A, `ble`, 400 once at 0x00512E9C; platform setter 0x00511D44..DC4; footprint exit
   at x > 85.9 mm from the charger corners and robot quad 0x00514DB2..DD8). Both fail on the run-3 bug (400 at contact
   off). Test-only commit.
3. **B-M13-14** `M13DriveOffDrivingAnimationBindingTests.cs` (free model): production binding from
   ShippedBehaviors.Navigation, synchronous push of (0x97, 0x93, 0x8F) under "DriveOffCharger", removal on Stop; fails
   on d67b73bd^.

Queued (non-blocking): B-M13-14 comments say the flag "stayed 0" on the old code (it was set; the missing entry is what
fails) and that the SevereNeeds=2 control fails on the old code (it passes; it is a negative control). The threshold
tests check the platform-true 50 by state, not by its wire send (0x00511DA0). Uncovered ordering (footprint exit with
the one-shot unused) is reachable only through the no-charger branch.

Still open for M13-017: the configuration, map and cache pieces above are unbuilt or unclosed; SevereNeedsState has no
production writer (R-M15-SEVERE-INPUT, M15). The threshold contract is now covered through production.
