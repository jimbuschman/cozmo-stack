# CUBEMOVED-PRESENCE-1

`ReactToCubeMoved` uses the native action list for its turn and for the lift-safe animation when it sees the target. The native runnable gate permits the reaction while carrying. The lift-safe constructor adds the lift mask when a carried ID is present and the robot is on its treads.

| Path | Native evidence |
| --- | --- |
| Carrying gate | The base checks carried ID at `0x005BD888..0x005BD89C`. `BehaviorAcknowledgeCubeMoved` vptr +`0x28` (slot `0x0102B768`) points to the true return at `0x006027F8..0x006027FA`. |
| Turn | `0x006022AA..0x006022BE` looks up the target with family `-1` and reads its pose in the current origin. `0x006022D2..0x006022E8` constructs `TurnTowardsPose(robot, copiedPose, π)` with f32 bits `0x40490FDB`. The ctor at `0x0054B344..0x0054B3E0` copies the pose and sets type `0x2D`. `0x006022FA..0x00602328` adds `Wait(robot, 0.5f)` second, makes the parallel root and calls native StartActing with the absence callback (GOT `0x0103F568` to `0x006026A8`). A missing pose warns and returns at `0x00602334..0x00602370`. |
| Compound action result | The shared constructor passes type `-2` at `0x0054E9B8..0x0054E9BE`. Table index 0 at `0x0053FED8` selects `Set_defaultCompleted` (`0x0053FF0E..0x0053FF14`, variant 6 at `0x0075C868..0x0075C86A`). The unproxied getter copies the base union at `0x0054F3E4..0x0054F3EA`. Both the sense and turn roots seed it. |
| Target sighting | `0x006027A8..0x006027E8` latches the matching `0x44` ID. In turning state the update calls `StopActing(false,false)` at `0x00602522`, constructs `TriggerLiftSafeAnimationAction(robot,3,1,true,0,60.0f,false)` at `0x00602526..0x00602546`, and submits it with the empty callable at `0x0060254A..0x00602554`. It then stores presence state at `0x00602582` and publishes its name at `0x0060258A`. The lift-safe ctor adds mask bit 2 at `0x00544714..0x00544732`. |

The 09:48:33 replay rebuilds the CubeMoved entry from the archived radio message and captured robot pose, compares both `0x88` payloads and the `0x8B` objective, and checks the successful animation completion type/result against the captured `0x5A`. It exercises the absence branch because the capture contains no `0x44` reacquisition within that reaction. The paired presence fixtures use the actual runner, verify the turn cancellation and animation results, and check the firmware lift-disable/enable messages with a carried cube and with an empty lift. Motor and animation acknowledgments are simulated in these offline tests.

The shipped path uses a scoped native owner. The standalone `ICubeLocator` constructor still supports its host adapter. Manifest classifications are unchanged. The shared M8-021 trigger diagnostic remains visible. Claude's source spot-check is pending.

## Verification

Build `c3942d40` passed all 8,831 tests in 59.65 minutes with the local recordings enabled. All 7 focused cube-moved and reaction-lock cases passed. The fidelity check passed for 765 records.
