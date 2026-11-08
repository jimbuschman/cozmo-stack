# AI-BODY-LIFT-CONTROL-1

## Claims and source

- Motion.QueueAiLiftHeightAsync submits ordinary finite lift heights through the same original-owner readiness, external tag, AtEnd and zero-retry admission as the existing AI head command. Native RobotActionUnion uses inner SetLiftHeight tag 0x1D (0x0070CCEA–0x0070CD10), factory table entry 0x01020338 and lookup 0x0052796C–0x005279A8. The thunk at 0x00526080–0x00526092 enters the shared factory 0x0052AC40. Queue position and zero retries flow at 0x00527A74–0x00527A8C, and the reaction gate remains at 0x0053D94C–0x0053D970.
- The factory compares the original f32 request to exactly 32 mm before any clamp and reads the original carrying owner (0x0052AC40–0x0052AC76). Carrying selects the separate PlaceObjectOnGroundAction. This scoped AI entry visibly refuses that branch and refuses exact 32 when carrying state is unavailable. It does not consult legacy placement callbacks. Nearby and negative inputs stay ordinary lift actions even when their eventual target is 32. The constructor at 0x0052AC78–0x0052ACA0 receives 5 mm tolerance, zero variability and the supplied speed/acceleration/duration. The API uses the existing 10/20/0 defaults.
- The unchanged MoveAction owns actual clamp and negative preset selection at Init (0x0054903C–0x005491D2, strict nearer-low at 0x00549104–0x0054913C). It uses the normal lift track, motor ID, in-position and stopped checks, and requires the matching lift ack handler's sent/id gate at 0x0054D748–0x0054D766 before completion. Completion variant 6 follows action type 0x13 at 0x0053FEED → 0x0053FF0E. A token requests native cancellation only for the exact original runner (0x0053E83C–0x0053E96E). Pending runners can end NOT_STARTED and active runners CANCELLED. Terminal results become visible after the existing owner barrier.
- A host source-log callback during lift clipping can destroy the original owner. Motion.cs checks the exact AI owner after the diagnostic and before a motor send. IActionRunner.cs and ActionQueue.cs skip continuation on a destroyed or replaced runner under M1-034, around the existing 0x00540592–0x0054062C and 0x0053F5F4–0x0053F708 tails. No new owner gets the old command or a second completion.

## Files and verification

- Motion.cs adds the lift request and shares the head admission/cancellation logic without changing the public head signature. Actions/IActionRunner.cs and Actions/ActionQueue.cs contain the reentrant destruction guards.
- AiLiftControlTests.cs uses the canonical fake transport. It checks genuine lift wire and variant 6, wrong ack before matching ack, carrying/unknown refusal at 32, an adjacent carrying request taking the ordinary path, negative preset, reaction refusal, pending and active public token cancellation, and replacing the robot during the clamp diagnostic. Existing head/action/compound/control coverage runs alongside it.
- Final affected suite passed 216/216. Independent review checked factory source, exact owner, queue/callback guards and test reach.

## MISSING

The carrying-low placement action remains open. The existing legacy callback does not expose an exact original-owner native runner/cancellation contract, and the native placement runner has separate path, reaction-lock and completion payload ownership. No permanent exclusion, hardware run, manifest status or approval change.

Reader requested gpt-6.1-sol medium. Builder and reviewer used configured defaults. Actual backends unobserved. Batch full gate pending.
