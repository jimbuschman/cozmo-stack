# FACE-ENROLLMENT-1

## Checked recognizer path

- The existing low-level stock VisionSystem.SetFaceEnrollmentMode input now reaches its current recognizer. FaceWorld::Enroll uses mode 4 for a selected nonzero ID and −1 for zero (0x004F5C9E–0x004F5CB2). The vision/tracker chain is 0x00657A58, 0x006B3394, 0x0086B276, 0x0086E372 and 0x008658AC. Zero resets mode/target and latches cancellation only for a prior incomplete selected enrollment (0x008658D8–0x00865932). A queued result invalidated by pose reset does not consume the cancellation latch (0x00863D0C–0x00863D56, 0x00864FD8–0x00865032).
- An eligible existing-entry update needs a nonzero count, a compatible target, strictly more than 999,999 µs since the last successful enrollment update and the native data/target gate (0x00865C04–0x00865CA4). Earlier last-seen/track maintenance still happens. Disabled/no-eligible paths return without changing enrollment count or the continuation timestamp (0x00865D0C, 0x00865D58, 0x00865D7A, 0x00865EE4). A successful eligible continuation updates the current album slot and timestamp (0x00865EB6–0x00865ED2, 0x008614F4–0x00861532) before decrementing a positive count (0x00865ED6–0x00865EE0). A full eligible album can keep its existing feature on a tie and still count. Existing session-only metadata stays consistent with the native current raw entry.
- After the queued result is consumed, looking up the current detection emits the configured enrollment count once only when remaining count is zero, mode is positive and the selected face ID matches (0x00863E22–0x00863E24, 0x00863EF8–0x00863F1C, 0x00863FAA–0x00863FB4). The positive tracked face carries that value (0x0086DD32–0x0086DD34). The Vision result applies all identity changes, then the FaceWorld sticky flag, before any face observation (0x006551E8–0x00655274). BehaviorEnrollFace Init owns clearing that flag (0x005FD094–0x005FD096). The low-level mode call does not clear it.

## Files and verification

- FaceRecognizer.cs implements the eligible-update continuation and one-shot lookup. Faces.cs carries the enrollment count and world flag. VisionSystem.cs connects the live owner and delivers its flag before observations. The stock mode setter defers recognizer logs until after the vision lock.
- FaceRecognizerTests.cs checks target, count, elapsed threshold and full-feature behavior. FaceIdentityTests.cs uses the stock recognizer and the drawn camera sequence to prove four correctly spaced feature updates and the flag visible before the observation, without a robot or user image.

## MISSING

- The original named 0x87 SetFaceToEnroll request only stages the behavior (0x005FF744–0x005FF890). MeetCozmoFindFaces selection and BehaviorEnrollFace are still open. TransitionToEnrolling requires TrackFaceAction and scan animation (0x005FEC40–0x005FED2C). Native name assignment at 0x005FDA7E–0x005FDA8A uses 0x008675C0, followed by name speech, save state/callbacks at 0x005FD494–0x005FD51E and final FaceEnrollmentCompleted at 0x005FE520–0x005FE528. The app requests fitted/canned name speech, save and music. Direct English Sentence support does not reproduce that flow.
- Positive-ID remap/merge retarget (0x00863E94–0x00863EAA), other pose selectors and full named enrollment/completion are still open. No new public headless enrollment or automatic save shortcut. No status, approval or hardware changes.

Readers requested gpt-6.1-sol medium. Builder and reviewer used configured defaults. Actual backends unobserved.

Final affected suite passed 337/337, including unchanged native OKAO and NV tests. The camera test also exports the updated named album after completion. Independent review passed after the stock setter lock and prior-current metadata corrections. Batch full gate pending.
