# AI-BODY-TURN-CONTROL-1

## Explicit public turn

- The public request takes finite f32 angle, speed, acceleration and tolerance, and a relative/absolute flag. The native inner action tag 0x23 and direct request tag 0x2B both reach explicit factory 0x52AE74 (thunks 0x526184 and 0x52619A). It passes raw angle and the normalized boolean to 0x5459D4, then sets speed at 0x545C08, acceleration at 0x545D14 and Radians-normalized tolerance at 0x545D50. The stack uses the same original-owner readiness, external tag and AtEnd/zero retries as its other AI requests (0x527A74–0x527A8C, 0x53DB04, 0x53E16C–0x53E248).
- Both zero speed and acceleration leave the manual flag clear. When the original robot's custom PathComponent profile is active, the common update and Turn slot copy its point-turn speed and acceleration without re-clamping (0x5403B8–0x5403D4, 0x545D36–0x545D4E). A nonzero setter locks that override.
- The retained type 0x28 action owns body track 4. Init checks the actual off-treads state and pose frame, uses the checked MAIN heading, and applies relative travel or absolute heading rules with native f32 normalization (0x545FA0–0x5461AC). Relative turns preserve the request's direction in speed bit 31 and send floor(abs(expected / pi)) half revolutions. Absolute turns send zero half revolutions and retain the speed sign. The wire is reliable tag 0x39 with the actual shared motor counter (0x640898–0x6408F4, 0x7A296A–0x7A29DA).
- A matching C4 acknowledges only the retained send (0x54D3F8–0x54D460). Position checking normalizes tolerance plus epsilon, and CheckIfDone waits for ACK before counting pose-frame changes, progress and off-treads state (0x546374–0x546422, 0x54642C onward). The inherited timeout is 30 engine seconds (0x52B0C2, 0x540D1C, 0x540E80).
- The native eye shift is created after a successful send, removed normally at position or after half travel with duration 99, and removed in destruction with duration 0 if still owned (0x5461B8–0x546286, 0x5464C6 onward, 0x545B94–0x545BD4). Cancellation acts on the exact retained tag, disposes the original ACK and eye recipients, and uses the base owned track stop (0x53E83C–0x53E96E, 0x541084–0x54127A). Pending deletion can return NOT_STARTED with no motor command. Pre-admission rejection only releases the unqueued constructor resources.
- The exact completion is variant 5 with the unsigned pose-frame change count from +0xD4 (0x5469A8–0x5469BE, 0x75C7E2, 0x75C932–0x75C93C). The public result uses the prepared count and retained action result after the existing queue-unlink barrier (0x53FA22–0x53FA98).

## Files and checks

- Motion.cs adds QueueAiTurnInPlaceAsync and its result. NativeVisionRunners.cs, PanAndTiltActions.cs and ChargerActions.cs route the explicit factory order and scoped runtime into the existing retained action. RobotCompletedAction.cs and ActionCompletionReport.cs carry the sourced completion variant to the host.
- AiTurnControlTests.cs reaches the actual public entry and ActionList using a fake robot. It checks relative and absolute robot messages and independently expected headings, multi-half-revolution yaw accumulation, active profile input and explicit override, pending and active cancellation, same-owner ACK matching and wrong-ACK withholding, timeout, no-motion success, and original-owner replacement. The native localization recipient advances the actual frame, then matching firmware state and C4 produce a successful public result and serialized variant 5 with count 1. Existing M13 turn tests cover off-treads and eye removal at the underlying action.
- The manager independently disassembled the factory, custom-profile slot, relative angle operations, tolerance compare, and completion getter and packer.

## MISSING

- Nonzero turn variability, other callers' completion migration, and the generic action-owned profile-clear branch remain outside this job. The explicit turn reads the active profile without taking ownership. Public-path off-treads and eye cleanup are only covered at the underlying action level. Physical robot motion and original bionic transcendental rounding were not exercised. No fidelity status or approval changed.

Reader used requested gpt-6.1-sol medium. Builder and reviewer used configured defaults. Actual backends unobserved.

Affected tests passed 69/69. Independent manager affected run passed 75/75 with zero skips. Independent source review found no remaining blocker. Batch full gate pending.
