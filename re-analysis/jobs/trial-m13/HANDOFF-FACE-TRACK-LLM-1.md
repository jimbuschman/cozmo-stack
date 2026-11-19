# FACE-TRACK-LLM-1 handoff

SCOPE.md A covers turning to and tracking a face. The captured-frame replay uses the in-process MCP client and the production action queue. The operator's robot check remains pending.

| Path | Native evidence and observed result |
| --- | --- |
| F2 camera chunks to observation | The fixture decodes the recorded grayscale JPEG and supplies its pixels and recorded camera NV calibration to the production camera/vision path. The shipped Okao detector produces the face. Camera-relative projection at 0x0087DE24–0x0087E068, historical camera pose at 0x00513AB0–0x00513B16, and pose flattening at 0x006B37B4–0x006B37EC feed FaceWorld at 0x004F42B4–0x004F42F2. The local observation and E2G 0x46 feed the host view, including a fresh signed ID in get_state. |
| Turn request to motor wire | Factory 0x0052AECA–0x0052AF56 takes Robot, signed ID, Radians maximum and sayName. The MCP profile sets sayName false and the six default motor overrides to zero. Init 0x0054BD66–0x0054BE30 resolves the current face, converts its goal to Robot MAIN, subscribes for verification and enters TurnTowardsPose. Pan/tilt child creation is at 0x00549C48–0x00549D60. The turn sends the head command toward the observed face, verifies it in later frames, fine-tunes and reaches SUCCESS. |
| Tracking and later frames | Factory 0x0052ACC0–0x0052ACE6 constructs TrackFace(Robot, signed ID) and sets head-only mode only when requested. 0x0056465C–0x0056478A sets the two-degree tolerances and 0.15 s tilt / 0.4 s pan durations. 0x00565DBC–0x00565F76 filters stale timestamps and computes MAIN-relative tilt and absolute pan. 0x005650E2–0x00565104 sends head and 0x005655FC–0x005656B4 sends body. The replay sees fresh centered observations without repeated angle commands, then uses later lateral images for body correction. |
| Identity and end | Matching 0x47 remaps the tracker’s ID at 0x005660BC–0x00566108. A missing or stale observation waits with the default zero update timeout (0x00565428–0x00565454). The client cancels the active request and receives CANCELLED_WHILE_RUNNING. TrackFace’s native variant 3 includes the final ID (0x00565DA4–0x00565DB4). TurnTowardsFace has native default variant 6, so the host captures the selected or verified ID before releasing the retained references. A missing explicit target can finish successfully without motion (0x0054BEB6–0x0054BEBC). |
| MCP report | Both tools return requestedFaceId and endReason beside the selected/final faceId. Caller cancellation is reported separately from the native outcome so a concurrent cancellation request does not replace the action result. The turn also reports facePoseAvailable. |

The replay uses the locally available AI-COMMANDS-HW-1 bundle 20261117-052405, checking the messages.jsonl SHA256 before extracting frames. The test uses the early off-center window near robot timestamp 88245, frame 178770 for centered observations and the lateral sequence 230005–231510. It feeds unaltered decoded pixels, with fixture motor acknowledgments and modeled head/body states. The recorded images are separate phases of the replay and do not model camera feedback from simulated movement. The fresh-frame settling and wire-angle checks therefore verify the engine decisions. Continuous physical centering remains an operator observation.

The run logs retain existing vision mailbox, image-cache and profiler gaps (M11-033/055/058/078/080), object collision (M11-037), action diagnostics and cleanup (M8-015/023/027) and needs/notification (M15-014/093). The test checks for a native tracking pose and no tracking-position MISSING on the exercised command path. No fidelity record or scope status was changed. M14-001/002/003 and M13-020 remain the face geometry, verification and pan/tilt references.

Operator check, about two minutes

1. Sit in front of Cozmo with his camera aimed high enough to see your face. Ask Claude to look at the camera and get a fresh signed face ID from get_state.
2. Move slightly off center and ask Claude to turn Cozmo toward that face. Check the selected ID, facePoseAvailable and native result. Observe whether he turns toward you.
3. Ask Claude to track the same face with head and body, then move slowly sideways. Step out of view briefly. Ask Claude to cancel the tracking request and read back the ending reason and final ID.


The focused run passed 68/68. In its replay, the turn sent head targets of 0.60594 and 0.71134 rad. Tracking moved to 0.75494 rad and then issued no angle commands across eight fresh centered frames. The later sequence produced a body correction of 0.16368 rad. The refreshed lateral target's first body command was 0.38281274 rad against the source-derived bearing of 0.38281277 rad. The active tracking request retained the native remap from -1 to 1 and returned CANCELLED_WHILE_RUNNING after caller cancellation.

The 2026-11-19 session notes added to main report that the existing robot turn succeeded and the existing track was cancelled near a MeetCozmo behavior transition. The operator observed possible following. The new MCP report fields are verified offline, with the robot check above available for a follow-up.
