# CONTROL hardware run: FAIL

Robot 172.31.1.1:5551, firmware 2457 (robot firmware 2457 is not 2381, the build the protocol was recovered from (policy M1-040: accepted)), started 2026-09-24 20:27:48 -05:00. Command: `cozmo-conformance control-check 172.31.1.1 --obb C:\Users\JimBu\source\repos\jimbuschman\cozmo-stack\cozmo-stack\re-analysis\obb --allow-drive`.

## What was run

`control-check` (cozmo-stack/src/Cozmo.Conformance/ControlCheck.cs) connects through the app layer: `CozmoRobot.Create`, the ConnectToRobot game message and the engine's RobotConnectionResponse, which is the body of `CozmoRobot.ConnectAsync` done step by step so the handshake is traced. Then it runs the checks below in order, through the public device APIs only. Stop-on-cliff is enabled right after CONNECT, before anything moves. Each check is judged from telemetry against the numeric criteria in `result.json`.

| check | status | criteria (all must hold) | human |
|---|---|---|---|
| CONNECT | PASS | RobotConnectionResponse with Result Success; the engine logged the robot's firmware version (a "robot firmware: <v>" line, G5.7 / policy M1-040); SyncTimeAck received: Robot 1 time synced; the first full RobotState after time sync handled; ready to stream set; all of the above within 10000 ms of the ConnectToRobot call |  |
| STATE | PASS | RobotState rate from the robot's own timestamps, (n-1)*1000/(ts_last-ts_first) over the 5000 ms window, within 25..40 Hz. The band is this test's tolerance, not source: no engine source gives the rate; the capture re-analysis/captures/2026-09-18_fw2457_hw1.5_smoke_head0.4.console.txt measured 33.5 Hz; every battery voltage in the window within 3.0..4.5 V; no transport timeout (timed-out flag clear), no disconnect event in the window, and the engine still connected (state 2) |  |
| HEAD | PASS | RobotState head angle within 0.05 rad of +0.3 rad within 2000 ms of the command; RobotState head angle within 0.05 rad of -0.3 rad within 2000 ms of the command |  |
| LIFT | PASS | RobotState lift height (45 + 66 sin(liftAngle)) within 5 mm of 50 mm within 3000 ms of the command; RobotState lift height (45 + 66 sin(liftAngle)) within 5 mm of 32 mm within 3000 ms of the command |  |
| DRIVE | PASS | the pose moved +20..+40 mm along the starting heading, pose origin unchanged; after the stop command, a RobotState with both wheel speeds at most 1 mm/s and AreWheelsMoving clear within 2000 ms; the pose moved -40..-20 mm along the starting heading, pose origin unchanged; after the stop command, a RobotState with both wheel speeds at most 1 mm/s and AreWheelsMoving clear within 2000 ms |  |
| FACE | PASS | AnimationState NumAnimBytesPlayed after the hold (+500 ms) is greater than before it; AnimationState ClientDropCount unchanged across the hold | a test pattern (the art in face-test-pattern.txt: a border, a cross and blocks) appeared on Cozmo's face for about 3 s |
| AUDIO | PASS | AnimationState NumAudioFramesPlayed rises by at least 25 over the play; AnimationState ClientDropCount unchanged across the play | you heard exactly 2 separate beeps, evenly spaced |
| ANIM | PASS | the animation ended with AnimationEndReason.Completed; keyframes fired == the clip's keyframes (54); no stall: no gap over 250 ms between consecutive animation audio frames sent, and StartOfAnimation to EndOfAnimation within the clip's 1089 ms + 1000 ms; no animation message (audio, face, head, lift, body, start/end) first sent more than 100 ms after the EndOfAnimation, over a 1000 ms watch; the animation loop no longer ticking |  |
| ANIM_CANCEL | PASS | the animation was streaming before the cancel: StartOfAnimation and at least one audio frame sent; the animation ended with AnimationEndReason.Cancelled; no animation message first sent more than 100 ms after the cancel, over a 1500 ms watch |  |
| CUBES | FAIL | the auto block pool was enabled after the Success response (policy M1-042); at least one light cube heard (ObjectAvailable or ObjectConnectionState); one cube reported Connected by ObjectConnectionState within 20000 ms; at least 1 ObjectAccel for that cube in the 3000 ms after StreamObjectAccel on (CubeAccelStreams.AddListener) |  |
| CAMERA | PASS | at least 30 complete frames reassembled in 3000 ms. The threshold is the test spec's, not source: no record gives the stream's frame rate; the measured rate is framesPerSecond; every frame in the window that is not colour-flagged decodes as a JPEG of 320x240 with reported resolution 320x240; colour-flagged frames are left out (M3-016 is HARDWARE_ONLY; their geometry is in the observation); 3 frames saved to the bundle |  |
| DISCONNECT | PASS | exactly one outbound frame carrying a DisconnectRequest after Dispose; no outbound frame after that DisconnectRequest (watched 1000 ms after Dispose returned); Dispose returned without an exception within 5000 ms | the command returned to the prompt by itself after printing the bundle path (the process cannot observe its own exit) |

## Files

- `result.json`: `overall` (PASS only if every check passed; INCOMPLETE if none failed but some were skipped; FAIL otherwise). Each check has `status` (PASS, FAIL, SKIPPED with `skipReason`), `records` (fidelity ids with their manifest status), `prerequisites`, `criteria` (each with `expected`, `measured`, `pass`), `measured`, `warnings`, and, where a person has to judge, `humanNote` with `humanVerdict: null` for the operator to fill in. `observation` (CAMERA) is the M3-016 colour observation, without a verdict. `hardwareOnlyUncertainty` lists the HARDWARE_ONLY records the run depends on.
- `frames.jsonl`: every datagram the transport's frame trace reported, both directions: time `t` (ms since start), `phase`, header, sub-messages with CLAD tag and name, and the raw datagram as `hex`. Inbound frames holding only camera image chunks outside the CAMERA window carry `hexOmitted` instead of `hex`, to keep the bundle small.
- `events.jsonl`: phases, check verdicts, the engine's log, transport warnings, the connection response, every handled RobotState (pose, head, lift, wheels, battery), every AnimationState, other robot messages, cube, cliff and calibration events.
- `env.json`: git HEAD and dirty state, SHA-256 of the tool and the stack sources and of the assemblies that ran, the robot's firmware (connection response and firmwareVersion JSON, policy M1-040), OS, .NET, the fixed criteria.
- `face-test-pattern.txt`: the image FACE showed. `camera-08679.jpg`, `camera-08680.jpg`, `camera-08681.jpg`: camera frames saved by CAMERA.

## Limits of what the bundle shows

- An outbound time is taken just after sendto returns; a robot message's time is when the engine's 60 ms tick handed it to the devices.
- "First send" of a reliable message is the first frame carrying its sequence id; resends are not counted as new sends.
- ANIM's "no stall" and DRIVE's distance are measured as described in each check's `note`; the public API has no stall report, and the pose is the robot's own frame.
- The engine's live (keep-alive) animation stream is not exercised by this run.
- A PASS is hardware verification. It does not raise the provenance of any record.
