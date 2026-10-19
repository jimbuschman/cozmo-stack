# M3M4-HW robot run, 2026-10-19 (manager's judgement)

- **Run:** the operator's robot (firmware 2457, serial 0x0240e889, body hardware 4), stack at e95d4acd (clean apart from
  untracked bundles), script `scripts/m3m4-hw-2.md`, `--allow-drive`. No exceptions; every check ran.
- **What is in git:** every file. `frames.jsonl` (36 MB, sha256 8e573929636dcf3a6c6f90ccbd2d6512bc1ed50282582f6c716e74caefe40c50)
  and `image-encodings.jsonl` (sha256 d08d1f4ee1bf33350126eadd7379143b4478b02950358b4919858a2cd35bf452) are stored gzipped. Git stores the text files with LF line endings; `hashes.json` lists the run's originals.
- **Verdict:** M3-device and M4-control are ACCEPTED on this run. Everything observed matches the source-settled records,
  and the seven HARDWARE_ONLY records repeat the 2026-10-12 observations. The three harness problems below change no
  reading.

| check | script verdict | manager reading |
|---|---|---|
| DAS_EVENTS (M3-055, M3-060) | OBSERVED_PARTIAL | Head `0xbeef` `0000` `0c50465e` and body `0xbeef` `0000` `0004` `0240e889`: M3-060's formats, with this robot's serial and body hardware 4. The startup flush event fires before this tool subscribes (harness); the M1M2-HW bundle, which attaches before start, has `das.upload.fail: live` (M3-051/M3-055). |
| ORIGIN (M4-021) | OBSERVED_REPORTS_UPDATE_VALUES_FROM_FIRST_STATE | All 133 settled states report origin 1, frame 0 after the update. |
| NV_READS (M3-036) | OBSERVED_FACTORY_LENGTH1_REPLIES_NO_REREQUEST_CASE | Six Length = 1 factory reads, each answered once (CameraCalib 56 bytes, BirthCertificate 12, CalibMetaInfo 4, IMUInfo 4, FactoryLock and VersionMagic NOT_FOUND). The size+16 half is still unanswerable without a large entry on the robot, as on 2026-10-12. |
| CAMERA_PARAMS (M3-021) | OBSERVED_VALID_SENT_INVALID_REFUSED | The connection send `{0, 0, auto 1}` (0x00658414, policy M3-019), then DefaultCameraParams. The limits come from the robot's DefaultCameraParams (1–67 ms, gain 0.1–3.984), followed by `16 ms / 2.0, auto 0`. Valid 20 ms / 1.5 is sent; exposure 65535 and gain 100 are refused with nothing sent; the restore is sent. The M11-059 IsInitialized boundary prints its MISSING (M11's, cross-layer). |
| COLOR_IMAGES (M3-016, M3-050) | OBSERVED_COLOUR_FRAMES | P1 57/59 and P2 43/43 colour frames; P0 and P3 grey. Operator: colour and black-and-white pictures. All 13,106 ImageChunks are encoding 8 (the misparse is fixed). |
| FACE_ROWS (M3-008) | OBSERVED_ROW_MAPPING_DIFFERS (script) | **All five agree with the engine model.** The answers were typed with spaces ("top left bottom right", "left higher", "fine lines whole face"), which the normaliser does not map to the hyphenated choices. The PARITY_STEP grader also expects "right-higher", where the model it states (right half one row lower) means "left-higher", which is what was answered. Harness defects. |
| TRACK_UNLOCK (M4-047) | OBSERVED_EXTRA_ENABLE_ANIM_TRACKS | Lock → DisableAnimTracks 0x04; unlock → EnableAnimTracks 0x04; the second unlock → Info "Tracks 0x4 are not currently locked by m3m4-hw", then PrintLockState and another EnableAnimTracks 0x04. As 0x0063FE5C..0x0063FFBA. |
| ANIM_BUDGET (M3-012) | OBSERVED_NO_BUDGET_WARNING_NO_DROPS | `anim_gotosleep_sleeping_01` (14.7 s, Wwise audio) completed. Audio frames streamed − played stays at 11–12 (bound 14); bytes streamed − played stays under 8192; no budget warning, no robot drops over 536 AnimationStates. |
| MOTOR_CAL (M4-013) | OBSERVED_REPORTS_STARTED_AND_FINISHED | Started 37 ms, finished 912 ms after the request. The head was at its lower stop (0.167 rad range), as on 2026-10-12. |
| CLIFF (M4-019, M4-038) | OBSERVED_CLIFF_EVENTS_AND_API_REPORTS | See "Cliff" below. |
| CUBE_TELEM (M4-024) | OBSERVED_ACCEL_ONLY_WITH_STREAM_ON | ObjectAccel 0/0 without the stream, 135/49 with it, 0 after the off request. Tap, power and up-axis events arrive without it. |
| CUBE_LIGHTS (M4-018, M4-051) | OBSERVED_CUBE_LIGHTS_SENT | Flash: CubeID, then CubeLights with all four LEDs 0xFE73 / 0x8000, 4/4 frames (shipped `flash.json` with the 0.6 white balance). The completion callback ran at 553 ms. NoAnimsLeftOnLayer goes back to the State layer, and PlayingPrevious resends `connected` (0xEF81). The operator saw the flash. |
| DISCONNECT_QUIET (M4-027) | INCONCLUSIVE (script) | See "Teardown" below. |

## Cliff

- **Threshold sends since connect:**
  - 50 on the first state (SC4).
  - 400 from Delocalize's ClearCliffRunningStats with the "RestoringCliffDetectThreshold" event, when the operator lifted
    him for the face photos (OnTreads → InAir).
  - 400 again on the treads change back to OnTreads. It equals the cache, so there is no Updated log, and the send is
    unconditional (SC3).
- **The edge, with `--allow-drive`:**
  - Tool-only EnableStopOnCliff(1), then DriveWheels 30/30. The wheels were confirmed at 21/19 mm/s.
  - The robot sent RobotStopped(1) at 3.19 s, and the tool sent StopAllMotors 40 ms later.
  - CliffEvent flags 0x03 (front left and right) came at 3.70 s. The engine logged HandleRobotStopped, the
    CliffValueWhileStopping debug rows, BlockWorld.AddLocatedObject "CliffDetection" at (92.5, 9.6, 25.0) in Origin21,
    `robot.object_located`, and HandleCliffEvent.Detected at 92.510, 9.632.
  - The public CliffDetected API reported "FrontLeft, FrontRight, robot stopped".
  - This is M4-038's path through the built ComputeStateAt (M11-097).
- **Behaviour end:** HandleRobotStopped's behaviour-end recipient prints M8-019's MISSING. No behaviour manager is running
  in this tool (M8's, cross-layer).
- **Not exercised:** no suspicious-cliff 150 send happened (count 0), and the hand lift produced no cliff message.

## Teardown

- **The frames:** the last outbound frame is the robot destructor's batch (clearPath, abortDocking, abortAnimation, stop,
  clearPath), sent by an ordinary 34 ms tick. The DisconnectRequest attempt came about 2 ms later, and the socket closed.
- **Why no DisconnectRequest:** the transport's send rule (`ReliableConnection.SendOptimalUnAckedPackets`, M1-006) sends
  nothing then, because the oldest pending message was sent 2 ms before and is not yet due for resend. The connection is
  deleted at once (M1-019: one send attempt, nothing waits).
- **The M1M2-HW comparison:** the same batch was still queued and left in one frame with the DisconnectRequest.
- **Reading:** both outcomes are the source rule's, decided by timing. Nothing went out after the teardown batch: no motor,
  light or other message (M4-027). The script's INCONCLUSIVE only reflects that it keys on a DisconnectRequest frame.

## Harness defects, queued

1. Answers typed with spaces are not mapped to hyphenated choices, and the PARITY_STEP grader's expected answer is
   reversed.
2. DAS_EVENTS subscribes after engine start; use the `BeforeStart` observers as M1M2-HW does.
3. DISCONNECT_QUIET should judge "no data after the teardown batch / socket close", not the presence of a DisconnectRequest.

No verdict settles a record. HARDWARE_VERIFIED records what the robot did, not where the behaviour came from.
