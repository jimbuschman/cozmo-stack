# AI-BODY-OBSERVED-FACE-REPORT-1

## Claims and evidence

- FaceWorld updates the accepted entry and increments its observation count at 0x004F47B2–0x004F47CC. The native report gate at 0x004F4956–0x004F496E is signed count greater than 3 or a nonempty accepted name. ID and timestamp come from the entry at 0x004F4B0A–0x004F4B0E, pose from the accepted transform and original origin list at 0x004F4B12–0x004F4B1A, and rect from the entry at 0x004F4B26–0x004F4B28. The earlier no-parts translation retention is at 0x004F4784–0x004F47A8. The managed snapshot stays within the accepted update's lock and carries the retained native transform rather than the legacy flat HeadPose.
- The constructor and broadcast are at 0x004F4B7A and 0x004F4B82 (union tag 0x46 at 0x00724B0C–0x00724B0E). Robot::Broadcast 0x00511DE8 gates both recipients on the external interface. The UI queues the app view at 0x00662526 before emitting the synchronous local signal at 0x00662552. Existing local consumers keep their ID/timestamp projection, including TurnTowardsFace 0x0054C050 and LookForFaceAndCube 0x005F08A0. The bounded app report uses original-frame owner/lifetime checks around callbacks.
- CLAD Pack 0x007135BC–0x0071371C writes signed ID, u32 timestamp, XYZ/WXYZ f32 plus u32 origin, f32 XYWH rect, byte-length UTF-8 name, i8 expression, Smile/Gaze/Blink valid-byte and f32 pairs, five expression bytes, and four byte-counted XY vectors in left eye/right eye/nose/mouth order. The default invalid/zero fields come from 0x0087DB7C–0x0087DBAE, and the strict maximum scan at 0x0087E08C–0x0087E0B6 gives Neutral (0) for all-zero scores. Stock detector flags are cleared at 0x0086B2BE–0x0086B2C2 and set by the mode transitions at 0x006B1A2A/0x006B1B5C/0x006B1BAC/0x006B1BFC. Eligibility for the complete app report uses the mode mask at detector execution.
- The existing PT12 call at 0x0086CD46 supplies the signed points. Native group tables at 0x00C98068–0x00C98088 and 0x0086D110–0x0086D146 produce left eye [5,1,6], right eye [9,10], nose [7,11,8,2,7], mouth [4,0,3]. Any coordinate -1 empties that group. The port reads the existing guest output before scratch reuse with no extra guest calls. PT_DetectPoint_GRAY -8 at 0x0086CD28–0x0086CD32 returns silently before feature writes, leaving the fresh constructor vectors empty (0x0087DBA8–0x0087DBAE). The face continues to recognition data at 0x0086DA14–0x0086DA16 and 0x0086DBB6–0x0086DBC2. Other incomplete inputs still withhold the app report.
- The stock recognizer supplies the resolved name at 0x0086DD24–0x0086DD2E; the negative track ID route at 0x0086DD4E–0x0086DD52 retains the fresh empty name. The raw report is withheld when the managed entry name diverges from the incoming name or is outside the supported UTF-8 wire domain. Existing identity and local delivery remain available.

## Files and tests

- Emulation/OkaoFaceDetection.cs captures the guest points. Vision/NativeFaceFeatures.cs maps the lists. Vision/Faces.cs captures the accepted entry. Vision/VisionSystem.cs bounds the source-backed publication and retains the local recipient. RobotObservedFaceReport.cs packs the message and exposes the typed app delivery.
- RobotObservedFaceReportTests checks the feature map and field layout. FaceIdentityTests drives real stock OKAO through camera chunks with raw and typed subscribers and the existing local signal. It checks the named report and raw ID/timestamp, app enqueue before local emit, optional-mode local-only delivery, the fourth unnamed no-parts observation, and owner replacement during the identity-change precursor. Full live payload comparison against independent PT scratch words, standalone external-interface suppression and replacement during the new local report callback have no dedicated regressions. Affected 99/99 passed, final owner-guard subset 5/5 passed, manager integration including unchanged NativeOkaoOracleTests 70/70 passed, and manager rebuilt the final app-order assertion and ran 5/5. An overlapping test-only rebuild briefly hit a locked test DLL and was rerun serially.

## MISSING

- Optional expression, smile, gaze and blink modes, supplied or offline detectors and recognizers, missing pose or feature inputs, and name divergence do not publish partial raw payloads. Local delivery still applies when eligible. Wider FaceWorld identity/deletion/report lifecycle and stock recognition cadence remain open.

## Workers

- Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. Manager reopened the native eligibility, recipient order and no-parts branches. One source correction round closed the accepted-entry snapshot race and the source-defined empty-feature branch. Builder raised the unknown PT status, resolved by the reader as the exact -8 case. Other incomplete carriers remain MISSING.

## Records

- M14-008 owns the face handoff and report, M14-010 owns the PT and optional mode producers, and M14-011 owns the recognizer. M14-001 and M14-003 own adjacent tracking and pose behavior. No whole record settlement is proposed.
