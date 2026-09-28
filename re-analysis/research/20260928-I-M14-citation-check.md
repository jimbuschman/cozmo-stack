# I-M14 citation check

Role: `cozmo-verifier` (performed by the integration manager). Date: 2026-09-28.
Primary source: `resources/lib/armeabi-v7a/libcozmoEngine.so`, opened directly with LIEF and Capstone in Thumb mode. The Ghidra output was navigation only.

## Existing/status-changing records

| record | verdict | instructions opened |
| --- | --- | --- |
| M14-001 | PASS, evidence replacement required | 0x004F4428 loads 0x473D1000 (48400.0), 0x004F44DA compares squared distance; 0x004F5380 loads 15000 and 0x004F53A4..0x004F53AC compares last-observed+15000; 0x0087DE3C..0x0087DF1C contains the recognition branch, 0.5/0.25/-0.125 construction and 6.0 floor. |
| M14-002 | PASS | 0x0056873E/0x00568742 and 0x0054B798/0x0054B79E store 10. |
| M14-003 | PASS | 0x005646BC..0x00564768 establishes the constructor constants/flags; 0x005650E2..0x00565104 divides the absolute head delta by +0xD0 and calls MoveHeadToAngle with 10000 acceleration. |
| M14-004 | PASS | 0x00611AE4..0x00611CB6 filters known ids and requires count >2; 0x00611DD0..0x00611E14 applies the 60.0 s cooldown; 0x00611E1C..0x00611F28 maintains the reacted-id set and timestamp. |
| M14-005 | PASS with citation typo corrected | The two `atan2f` calls are 0x0051886C and 0x0051888E, after ComputeStateAt at 0x00518800. The old record says 0x0054886C for the first call; that address is a typo. 0x0054B664..0x0054B712 installs the returned pan/tilt angles and enters PanAndTiltAction. |
| M14-006 | PASS as BLOCKED_EXTERNAL | `resources/lib/armeabi-v7a/libacattsandroid.so` is a separate shipped third-party binary; `re-analysis/obb/sound_meta/PluginInfo.xml:6` names the Anki Wave Portal plug-in. The proprietary voice model/plug-in semantics are outside `libcozmoEngine.so`. |
| M14-007 | PASS | 0x005C2420..0x005C24AA constructs the 40 mm ray and negates the memory-map query; 0x005C254E..0x005C258E selects -15 or 40 and speed 40; 0x0068176E..0x006817A4 folds eleven enabled types into a mask; 0x0067F4C0..0x0067F548 maps object families; 0x0067ECFC onward inserts observable objects; 0x00622380..0x0062261A distinguishes markerless object types. |

## X2 production rows sampled

Twenty-nine rows were sampled (more than the required 20). F18 and F19 failed on the word “threshold”; the corrected rows are in gap pass 1. All other listed claims below passed.

| row | verdict | instructions opened |
| --- | --- | --- |
| F1 | PASS | 0x00654504..0x00654514 gates and calls UpdateFaces. |
| F2-F4 | PASS | 0x006551E8..0x0065528C walks changed ids and tracked faces, sets FaceTracker+0x24, calls FaceWorld::Update, warns on nonzero. |
| F5-F6 | PASS | 0x004F5316..0x004F535C calls AddOrUpdateFace per element; 0x004F5380..0x004F53AC performs the unnamed-face 15000 ms expiry test. |
| F7 | PASS | 0x004F4428 and 0x004F44BA..0x004F44E8 compute squared distance and compare to 48400.0; 0x004F444C calls overlap scoring. |
| F8-F9 | PASS | 0x004F4B7A/0x004F4B82 constructs and broadcasts RobotObservedFace; 0x004F3C2C..0x004F3C64 constructs/broadcasts RobotDeletedFace, erases viz, then erases the map node. |
| F10 | PASS | 0x004F55B8..0x004F55E2 implements the timestamp/id gates and world-origin predicate. |
| F11 | PASS | 0x004F5C9E..0x004F5CB2 selects mode 4 or -1 and tail-calls the VisionComponent veneer. |
| F12-F15 | PASS | 0x0086D740..0x0086DBC2 contains detection, result enumeration, square conversion, TrackedFace append, detector order and recognizer calls. |
| F16 | PASS | 0x0086CCC6, 0x0086CD28, 0x0086CD46 and 0x0086CF32 are the four cited OKAO parts calls. |
| F17 | PASS | 0x0086D1F8, 0x0086D24C and 0x0086D2A2 are SetPoint, Estimate and GetResult. |
| F18 | **FAIL** | 0x0086D474..0x0086D4A0 converts OKAO integer outputs, sets validity byte +0xB8, and multiplies the two outputs by 0.01 and 0.001 before storing +0xBC/+0xC0. They are scales, not thresholds or gates. |
| F19 | **FAIL** | 0x0086D604..0x0086D61E stores gaze outputs unscaled when enabled; 0x0086D658..0x0086D680 sets blink validity and multiplies both close ratios by 0.001. Again 0.001 is a scale, not a threshold. |
| F20-F21 | PASS | 0x0086E074..0x0086E1AC contains the stated 16/64/128/32 and 25/45/10 gates; 0x0086E36C returns 0x41800000 (16.0). |
| F22 | PASS | 0x0086DBA6 and 0x0086DBC2 call SetNextFaceToRecognize and GetRecognitionData. |
| F23 | PASS | 0x0087DE3C..0x0087DF1C, as checked for M14-001. |
| F24-F25 | PASS | 0x006512EA/0x00651330 read 0x184000 and 0x183000; 0x0065A876..0x0065A896 locks, installs serialized data, unlocks; the later callback broadcasts loaded names/ids. |
| F26 | PARTIAL, closed in gap pass 1 | 0x0065718C gets serialized data and 0x006571DC..0x0065720E checks both tag capacities. X2 did not transcribe the write calls. |
| F27 | PASS | 0x00651594/0x0065159C broadcasts RobotErasedAllEnrolledFaces first; 0x0065160A/0x00651612 broadcasts each LoadedKnownFace. |
| F28 | PASS | 0x005FE520/0x005FE528 constructs and broadcasts FaceEnrollmentCompleted. |
| F29 | PASS | 0x00655360..0x00655366 forwards the tracked-pet list to PetWorld::Update. |

## Verdict

**FAIL for X2 rows F18 and F19 as written.** The corrected detector-output rows in `20260928-I-M14-gap1-extraction.md` replace them. F26's missing tail is also supplied there. Every other checked row is supported by the cited native instructions.
