# FACE-ENROLLMENT-CHOREOGRAPHY-1

## Stock app and engine flow

- The shipped Unity method FaceEnrollmentGame.StartFaceEnrollmentBehavior, token 0x06003894, activates high-level activity 3. Shipped activities_config.json maps it to MeetCozmo scoring. The listed priorities are DriveOffCharger 1000, RespondToRenameFace 3, EnrollFace 2, Interact 1, Socialize 0.5 and PutDown 0.1. The current native high-level adapter withholds MeetCozmoFindFaces, so direct admission would bypass those gates.
- FaceEnrollInstructionState.SendEnrollFace, token 0x060038CC IL+0x19–0x1C, always supplies save, sayName and useMusic as true. Robot.SetFaceToEnroll, token 0x060047B1, sends 0x87 with name, literal observed ID zero, requested save ID and those flags. Native 0x005FF744–0x005FF890 stages a nonempty request while stopped. It refuses replacement while running (0x005FF960–0x005FFA8A). The app sends empty 0x88 when an in-flight enrollment goes to sleep (tokens 0x060038CF and 0x060047B2), which selects running state 11 for normal stop cleanup.
- The engine initializes the request and clears the completion pulse (0x005FCD30–0x005FCE2A, 0x005FD094–0x005FD09C). It uses a turn and three-image wait before the scan (0x005FD824 onward), with native recent-face selection and loss gates (0x005FDC84 onward, 0x005FDAAA–0x005FDB82). The scan transition calls Enroll(selected ID) and starts parallel TrackFace in its 4° enrollment profile and MeetCozmoScanningIdle 0x10E (0x005FEC40–0x005FED2C). These are separate from the live low-level recognizer mode setter.
- The pulse is consumed before name assignment and the authored name sequence (0x005FD9AE–0x005FD9B6, 0x005FDA7E–0x005FDA8A). A fresh name and a merge use distinct assignment and speech paths (0x008675C0, 0x005FD210–0x005FD3F8). The quiet branch is present at 0x005FD2AA but is not used by the checked stock request.
- On successful authored action completion, the save flag selects durable saving (0x00600200–0x00600244). Native saves separate album and enrollment callbacks (0x005FD494–0x005FD51E, 0x006003DE, 0x0060036E), then waits up to five engine seconds (0x005FD55C–0x005FD5AA). Predicate 0x00600450 waits while either slot is NOT_STARTED, completes when both are successful, and treats completed errors differently for a merge request. The wait callback selects state 8 on failure or 0 on success (0x006005D8). The final 0x56 report belongs to StopInternal (0x005FE2F4–0x005FE528), with its own result mapping and CLAD body (0x005FE584–0x005FE593, 0x0071568E–0x007156C8).

## Bounded live identity correction

- Native GetRecognitionData compares the current mapped face to the nonzero active enrollment target. If the target equals that entry's previous face ID and differs from its current ID, it writes only the new target before evaluating the one-shot pulse (0x00863E94–0x00863EAA, 0x00863EF8–0x00863F1C). Track, count and mode keep their existing roles. Current and previous identifiers are normalized after copying (0x00863F00, 0x00860F0C).
- The tracker passes the previous face ID through when positive and uses the negated raw track for a first association (0x0086DBCE–0x0086DC3C). The existing stock detector consumes this result and publishes the local 0x47 change before the enrollment pulse and observations (0x006551E8–0x00655274). The correction keeps the existing recognized-entry merge and normalizes the one-shot publication afterward.

## MISSING

- Full stock activation, chooser and scan ownership, name assignment, persistent authored speech and music, behavior-specific save callbacks and final 0x56 report remain open. No public named request or quiet substitute was added. The tracking-only identity publication and full raw external 0x47 layout remain separate.
- Existing default album save and low-level enrollment are verified at their existing boundaries. No robot hardware was used, no shipped assets were added, and no fidelity status or approval changed.

## Files and verification

- FaceRecognizer.cs updates the existing stock lookup. The previous FaceID survives one publication, and only the matching nonzero enrollment target changes. Normalization follows the pulse decision. The distinct direct track-map replacement and tracking-only event list remain at their existing gaps.
- FaceRecognizerTests.cs supplies deterministic feature matches and exercises real merge/lookup methods, including the matching, zero and unrelated nonzero targets. It checks count, mode and track retention, and combines the remap with a completed count on one lookup without changing private state.
- FaceIdentityTests.cs loads a two-entry album with local shipped OKAO, feeds the drawn images through the real camera/worker/result path, and uses the existing MergeFaces operation. It observes one positive local 0x47 for 7→8 before the later enrollment pulse and its FaceObserved delivery. This frame test verifies the normal recipient and later completion. The simultaneous remap/pulse case is covered at the recognizer boundary.

Reader used requested gpt-6.1-sol medium. Builder and reviewer used configured defaults. Actual backends unobserved.
Affected recognition, face, NV and OKAO tests passed 283/283, final focused additions 5/5, and independent manager rerun 43/43. Independent source review passed for the bounded identity correction. Batch full gate passed at e3bed520 with 7890/7890 and zero skips. READY FOR CLAUDE for the bounded slice.
