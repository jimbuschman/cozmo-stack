# TRACK-FACE-LIFECYCLE-SETTLEMENT-1

## Source path

- The TrackFace constructor at 0x565ADC uses action type 0x23, default two-axis mode, 2-degree tolerances, 0.15-second tilt and 0.4-second pan. The action timeout is FLT_MAX. The native constructor and setter census found seven sites, including the two Interact children, enrollment, Come Here, RequestGameSimple, PeekABoo and the TrackToFace factory (0x56468A–0x564786, 0x565AD0–0x565B86, 0x5C25C0, 0x5C27DA, 0x5DABAA, 0x5EC154, 0x5F7ADC, 0x5FEC84, 0x52ACD4).
- These TrackFace sites leave update timeout, eyes, sound, duration criteria and driving animation disabled. The source-enabled settings belong to other derived classes. The Interact sites use 4-degree tolerances and the configured clamp interval. Enrollment uses 4 degrees with immediate clamp and selects head-only off treads or on a committed cliff event (0x5C25C8–0x5C2610, 0x5C27F4–0x5C281A, 0x5FEC88–0x5FECD0).
- Base Init saves and sets the eye-dart parameter, then fails with 0x03000000 if the configured companion tag is already absent (0x564DAC–0x564E3E). A companion that disappears after successful Init instead finishes tracking successfully (0x564F36–0x565028). No derived Init or tracking subscription runs on the early failure.
- Derived Init requires the original external interface and sets the engine error state on absence (0x565C24–0x565CE2). Success replaces the 0x47 subscription, retires the previous one, and updates the original movement face ID and observation stamp (0x565C64–0x565C7A, 0x565CEE–0x565CFC). Reinitialization after QueueAtFront interruption follows the same order. A matching 0x47 updates the action's signed face ID, which is emitted as completion variant 3 (0x566106–0x566108, 0x565DA4–0x565DB4).
- Tracking consumes a fresh face observation under the actual MAIN pose and uses the source clamp and motor limits (0x565DBC–0x565F76, 0x56502A–0x565104, 0x5655FC–0x5656DC, 0x56584C). A defined relative-transform failure sets the engine error state without producing a target. The existing head-before-body order preserves the later MAIN read and off-treads result.
- Native teardown clears the movement tracking ID and subscription, restores its saved idle parameter, stops its enabled axes for a started state and invokes the shared driving handler destruction path (0x565BE4–0x565C0C, 0x5647B4–0x56485C). The standalone host adapter retains its separately checked pending-admission isolation.

## Files and tests

- `NativeTrackFaceRunner.cs` owns the pre-Init companion result, source error state, re-Init subscription, original-owner motor sends and teardown. `Motion.cs` carries the current tracker guard through stop operations and still releases the original runner's exact track lock if a successor has started.
- `NativeTrackFaceTests.cs` drives the actual ActionList, original fake robot and configured profiles. It distinguishes early companion loss from late success, reinitializes through queue-front interrupt and resume, and counts the retained 0x47 handle. It exercises no external interface, unrelated MAIN transform, both motor-send failures, off-treads head-before-body order and the retained completion union.
- Two same-owner successor regressions enter through subscription release and the stop wire callback. The old tracker releases its own lock without clearing successor tracking state or sending a stale stop. Final targeted tests passed 25/25, and the final affected face, action, behavior, motion and oracle suite passed 327/327. The independent two-job and lifecycle integration run passed 329/329.

## MISSING

- The explicit TrackToFace request factory is recovered, but its separate command ingress is outside the installed retained TrackFace recipient. RequestGameSimple and PeekABoo have native TrackFace callers whose broader managed behavior owners are still missing.
- The native tracking pose carrier and recognition producer remain under M14-001. Physical motor timing remains operator evidence.
- The enabled generic criteria, eye, sound and driving-animation branches belong to other concrete tracking actions in this source census.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and reviewer used the configured default request. Runtime model identity was not observed. Review corrected same-owner teardown reentry, guard propagation through the motor stop and exact release of the old track lock. The reader identified the companion and re-Init gates and the manager independently recovered the source log literals. The off-treads fixture now publishes its observation after the transition changes origin.

## Records

- M14-003 owns the concrete TrackFace setup and tracking recipient. The installed retained profiles now include the companion gate, re-Init ownership and defined failure state. The narrow durations and starting-flags recipient is complete for the recovered profiles. The explicit command and unbuilt behavior producer boundaries remain listed above for Claude's settlement decision. No broader ingress or generic tracking action is claimed.
- M14-001 supplies the actual camera-frame tracking position and current-root face selection. M12-024 owns the shared driving-animation behavior and its other live callers. Neither broader record is settled here.
- M8-015 and M8-022 own the retained action list and watcher lifecycle. This job uses their interrupt/resume and completion behavior.
