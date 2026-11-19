# LLM-TEACH-FACE-1 — build; full validation pending

SCOPE A: teach, rename and erase a face. The recorded-pixel integration reaches
native enrollment success, rename confirmation and erase confirmation through the
serialized in-process MCP client. No detector, face result, enrollment count,
behavior phase or terminal action result is injected. Hardware acceptance remains
for the operator; no scope/manifest/inventory status is changed.

The bridge adds `faceOperation`: enrolled/failed from the native 0x56 result,
renamed from 0x1D, erased from 0x57, with face ID/name. Erase's native report has an
empty name; it remains empty. `lastKnownName` is explicitly a pre-request recognizer
snapshot. `enroll-face` aliases the existing `teach-face` to match the requested
MCP flow. Existing status/result/report fields and engine traffic stay unchanged.

## Complete flow and source

| Step | Primary evidence and exercised production path |
| --- | --- |
| App request fields | Shipped Unity `Robot.cs:1369` and FaceEnrollment/FaceEnrollInstructionState.cs:131: name, observedID=0, saveID (0 for fresh), saveToRobot=true, sayName=true, useMusic=true. Bridge posts the same SetFaceToEnroll G2E 0x87 after selecting Meet. The generated message's bare constructor defaults are not substituted for the real Robot wrapper defaults. |
| Request/admission and target | BehaviorEnrollFace constructor 0x005FCAD4, InitEnrollmentSettings 0x005FCD30, InitInternal 0x005FCEC0; UpdateFaceToEnroll 0x005FDC84. The native positive-ID test is signed BLE at 0x005FDD6E; tracking-only negative IDs are skipped (log at 0x005FDE9E). No relaxed gate was added. |
| Actual camera perception | Captured F2 chunks -> production camera assembly/decode -> grayscale camera input -> real OKAO DT/PT/FR and native feature extraction worker -> FaceRecognizer -> FaceWorld. SetNextFaceToRecognize 0x00865A1C admits a qualifying image/track before consuming the completed slot, as the existing implementation does. Default IsEnrollable case at 0x0086E0A8..0x0086E0CC checks coarse direction 0, intra-eye distance >=16 and facing-camera; alternate pose modes remain separate. |
| Face identity and observations | RegisterNewUser 0x00865658; UpdateFaces 0x006551E8..0x00655274; ChangeFaceID loads old/new words at 0x004F3DCC and broadcasts 0x47 at 0x004F3E64. RobotObservedFace 0x46 is built/broadcast at 0x004F4B7A/0x004F4B82. The test observes positive identity and named Robin/Riley frames through these real consumers. |
| Look, scan, named speech | Looking transition 0x005FD824; turn construction 0x005FE80C; enrollment scan 0x005FEC24 (normal tracking plus authored scan); saying-name construction 0x005FD210..0x005FD3A8. App-default fresh flow uses the authored get-out, Name_FirstIntroduction_1/trigger 0x10F, Name_FirstIntroduction_2/0x110 and celebration/0x111. Actual recognizer updates exhaust the four requested enrollments; no ApplyEnrollmentCount/phase override is used. |
| Audio gate | MCP Program uses HardwareEngineOptions.CreateWithAnimationBudget, like m15-cube-hw. Production StartProduction starts runtime audio; the replay's StartOffline invokes that same runtime initialization via RuntimeAudioSinkForTest. It asserts RuntimeWwiseAudio.Ready and runs the shared owner (no standalone CannedSpeechSinkForTest bypass). Only the PC device is silent/modeled: 48 kHz stereo s16, matching WinMmPcmSink. Fresh-name animation/audio frames go out and no M14-006 MISSING is accepted. Audibility is not claimed. |
| Save and enrollment completion | TransitionToSavingToRobot 0x005FD494; M14-012 SaveFaceAlbumToRobot 0x00657168..0x00657600; actual NV command/reply handlers for tags 0x184000 and 0x183000. StopInternal builds/broadcasts FaceEnrollmentCompleted 0x56 at 0x005FE520/0x005FE528. The fresh-flow success is observed only after both modeled NV replies; independent field packing checks result, ID, UTF-8 length/name. |
| Rename | G2E 0x8C; VisionComponent::RenameFace 0x00657B24 takes ID, old UTF-8 name and new name. It locks, calls the recognizer at 0x00657B5A, unlocks, and on result 0 calls SaveFaceAlbumToRobot at 0x00657B6A, constructs 0x1D at 0x00657B74 and broadcasts at 0x00657B7C. No NV wait precedes the confirmation. The test checks the exact body and a subsequent observed Riley frame. |
| Spoken rename response | Separate request staging/behavior, 0x00600848: SayTextAction(robot, stagedName, intent 3 / Name_Normal) at 0x006008D4; configured trigger and tracks=0 at 0x006008E2; StartActing with empty callback at 0x006008F0. Update 0x00600A50 returns running while +0x84 is nonzero. Its admission/completion is an observation, not the 0x1D rename success signal or an invented 30 s deadline. Existing Meet chooser/reaction and configured rename-speech tests remain in the affected group. |
| Erase | G2E 0x89; VisionComponent::EraseFace 0x00657A60 calls the recognizer under the vision mutex, returns without a report on failure, attempts the save at 0x00657A94, constructs ID + empty name at 0x00657A98..0x00657AB6 and broadcasts 0x57 at 0x00657ABE. The test keeps the empty native name, exposes the labeled host snapshot, checks the body/order and drains real NV callbacks. |
| After erase | GetNextFaceID 0x0086561C..0x00865654 returns a free current ID without advancing it; the increment only occurs on an occupied ID. Continued images can register an unnamed guest with the same numeric ID. The sourced expectation is that the named enrollment is gone, not that the person disappears or the number can never recur. |
| MCP result | Existing immutable engine report FIFO -> matching face request -> unchanged native report plus host `faceOperation` -> MCP text/structuredContent/isError. Engine failure codes/names remain the cause; a no-report result says no matching confirmation was received. The return does not claim rename/erase NV completion or that a spoken response already finished. |

## Replay boundary

Private input: local copy of 20261117-052405-AI-COMMANDS-HW-1-bdcbda.
`messages.jsonl` SHA256:
`c67ba13dec4eae4538958e12cad294883f7eb0c441a8a829e4dcd29bdb33ad63`.
Original camera calibration is restored from its actual NV reply. Selected camera
frames have robot timestamps 230005..231510 (recorded face-seen window around
251566..253426 ms). The first actual recognizer registration selects an admitted
captured image to repeat as a stationary scene; the callback observes the feature
and never supplies an identity. Original pixel bytes remain unchanged. The app RNG
input is the recording's 4069123563.

The robot starts with an explicitly **fresh/empty album**, rather than loading the
later recorded named album. This is a controlled new input scene, not a claim to
reproduce that historical storage or camera/motor trajectory. The normal offline
host-ready/connection fixture supplies motor acknowledgments and state at requested
angles, path-start/completion feedback, animation-start/end feedback after outbound
messages, and successful NV replies. The real controller constructs/actions,
streamer, shared audio, native detector/recognizer, save callbacks and report FIFO
execute. Physical motion/timing and local audibility are not certified by this model.
No raw images or hardware bundle are committed.

Current fidelity records are unchanged: M14-008 and M14-012 EXACT_SOURCE;
M14-010/M14-011 IMPLEMENTATION_GAP; M14-006 BLOCKED_EXTERNAL (current title
`Text to speech is not implemented`). The replay demonstrates this particular
shared-runtime name path; it does not settle those broader records. Existing
non-name-path/ownership/profiler MISSING diagnostics remain visible in the run log.

Validation so far: affected face/MCP groups **107/107**, including the recorded-pixel
flow and the separately configured spoken-rename behavior. Exact wire-body/order
assertions were then added; final build/full suite and gate key pending.

## Next operator LLM session — about two minutes

1. One person sits in front, with a clear, steady face. Ask Claude to show the
   annotated camera image; raise the head if the face is not visible. Keep a cursor
   from read_events so later checks use new events.
2. Ask: "Enroll my face as Test Robin using enroll-face, and wait for the result."
   Expected signal: faceOperation.outcome=enrolled, engineResult=0, a positive face
   ID and name Test Robin. Record the ID. Ask for a fresh face-recognised event with
   that ID/name (this also covers the event kind absent from the previous LLM run).
3. Ask: "Rename that ID from Test Robin to Test Riley." Expected signal: renamed
   with that ID/new name; request a new camera/state/event observation of Test Riley.
   A rename report is independent of when the spoken response is admitted/finishes.
4. Ask: "Erase that face enrollment." Expected signal: erased with the same ID and
   lastKnownName=Test Riley; the native erased report name remains empty. A continued
   sighting as an unnamed guest, even under the same ID, is expected engine behavior.
5. Record whether the names were spoken and any motion as observations. Keep the
   session evidence for the manager; these instructions do not run hardware or commit
   its bundle.
