# DRAFT — M3M4-HW: the six HARDWARE_ONLY records of M3-device and M4-control

Manager review required before the operator runs it. It has not been run on a robot (only against the loopback `fakerobot`, for the bundle writer). It never commits, never edits the manifest and never writes the robot's NV storage. Every check ends **OBSERVED_\*** (with the rule that produced it) or **INCONCLUSIVE** (with what is missing). There is no PASS, and no verdict settles a record: a hardware observation does not raise provenance (AGENTS.md).

Records (current manifest, status HARDWARE_ONLY, `hardware_required`, `live_path`): **M3-008** pair-bit to display-row mapping and playback period; **M3-016** colour frames and the robot's reaction to EnableColorImages; **M3-036** factory read with Length = 1 and the non-factory Length = size+16 re-request; **M4-013** StartMotorCalibration after the connection-time calibration; **M4-021** origin id/frame in RobotState after AbsoluteLocalizationUpdate; **M4-024** what makes the robot forward cube telemetry, and the effect of StreamObjectAccel. The tool reads `re-analysis/fidelity_manifest.json` first and stops (exit 2, before any network use) if any of the six is missing; it warns if one is no longer HARDWARE_ONLY and copies the six records into the bundle (`records.json`).

## Where it is, and how it is built

`cozmo-stack/src/Cozmo.Conformance/M3M4Hw.cs`, command `m3m4-hw` (registered in `Program.cs`). It connects with the production engine exactly as `control-check` does (`CozmoRobot.Create`, `ConnectToRobot`, the engine's ConnectionResponse; the engine then does time sync, the NV calibration read, the image request and the AbsoluteLocalizationUpdate), records every raw frame in and out from the transport's `FrameTrace`, then runs the selected checks. Device actions use the stack's own public APIs (`Display.SendRaw`, `CameraSettings.EnableColorImages`, `Motion.RequestMotorCalibration`, `CubeAccel.AddListener`, `NvStorageComponent.Read`), so what goes on the wire is what the production path sends. Structure test: `tests/Cozmo.Protocol.Tests/M3M4HwTests.cs` (six ids exist; the checks name exactly them; a missing id stops the run).

## Physical setup

- Computer on the robot's Wi-Fi (172.31.1.1 default; pass the IP otherwise). Close the official app and every other client.
- Cozmo on its treads, on a clear flat surface, 30 cm free all round, **off** the charger, battery not low. Head nods in MOTOR_CAL (lift only with `--include-lift`). No wheel motion in any check.
- The face must be visible; a phone or camera for the close-up photos in FACE_ROWS and COLOR_IMAGES.
- COLOR_IMAGES: point the robot at something obviously coloured (a toy, a coloured page) for the whole check; a white wall cannot answer the colour question.
- CUBE_TELEM: one powered light cube within 30 cm, awake (tap it) and known not to be connected to a phone.
- FACE_ROWS and CUBE_TELEM prompt on the console; keep the console and the robot in view together.

## Operator commands (after manager approval)

From the repository root:

```powershell
dotnet build cozmo-stack/Cozmo.sln
dotnet cozmo-stack/src/Cozmo.Conformance/bin/Debug/net9.0/cozmo-conformance.dll m3m4-hw 172.31.1.1
```

Options: `--only ORIGIN,NV_READS,COLOR_IMAGES,FACE_ROWS,MOTOR_CAL,CUBE_TELEM` (run a subset; order is fixed), `--include-lift` (also request lift calibration), `--cube-wait <s>` (default 45), `--photo KEY=path` (pre-supply a photo for a prompt key such as `FACE_ROWS.TOP_BAND`; otherwise the prompt asks for a path), `--operator <label>` (free text for result.json), `--out <dir>`, `--yes` (asks nothing; operator answers recorded as null, so the checks that need them come out INCONCLUSIVE). Ctrl+C stops, disposes the robot (bounded) and still writes the bundle. A 20-minute watchdog does the same.

Suggested order of runs: one full run; if a check is INCONCLUSIVE for a reason in its `missing` list, fix the setup and rerun only that check (`--only`), each rerun being its own bundle.

## Bundle

`re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-M3M4-HW/` (one directory per invocation): `result.json` (per check: verdict, verdictRule, measured data, operator answers, missing), `env.json` (git HEAD and status, command line, sha256 of the tool source, this document and the four assemblies, timings), `records.json` (the six manifest records as read), `events.jsonl` (connection, engine log lines, motor-calibration, cube, camera and operator events), `frames.jsonl` (every UDP frame in and out with full hex; inbound image-only frames outside the COLOR_IMAGES phases carry no hex), `hashes.json`, a copy of `M3M4Hw.cs` and this document, plus `camera/` (saved frames: `.jpg` and `.raw.bin`), `face/` (each pattern as text art and its payload) and `operator/` (the photos, copied and hashed). The operator copies the whole folder back; only the manager decides whether it goes into git.

## Per-check design

### ORIGIN (M4-021)

Question: does the robot report the origin id and frame from AbsoluteLocalizationUpdate in RobotState?
Trigger (from code, `CozmoEngine.SyncTime`, M4-020): the engine sends `AbsoluteLocalizationUpdate {timestamp 0, frame 0, origin 1, 0, 0, 0}` once per connection, right after the SyncTime/InitController/ImageRequest sends. It is the only production send of that message. No operator action.
Procedure: passive. After the connection settles, the tool reads the wire log: the update's send time, the RobotStates before it and from 500 ms after it (the margin leaves out states the robot built before the update arrived), and their (origin, frame) pairs, counted from raw frames so the engine's own M4-020 gate does not hide any.
Recorded: the update's bytes and time, the pair histogram before and after, the first 12 states after the update with robot timestamps.
Verdicts: `OBSERVED_REPORTS_UPDATE_VALUES` (all settled states carry the update's origin and frame, the earlier ones did not); `OBSERVED_UPDATE_NOT_REFLECTED` (settled states only carry pairs seen before); `OBSERVED_MIXED`; `INCONCLUSIVE` (no update sent; fewer than 3 states before or 20 after; the robot already reported the update's values before it).
**Existing evidence the manager should reconcile:** the manifest record cites an earlier bundle where this stack never sent the update (855 states, origin 0 frame 0). The newest CONTROL bundle `20260925-180557-CONTROL` already shows the update sent at 160 ms and all 688 states after it reporting origin 1, frame 0, with zero rejected states. That is a before/after pair across two bundles; this check makes it a within-run comparison. See open question 1.

### NV_READS (M3-036)

Question: the robot's reply to a factory read with Length = 1, and whether the non-factory `Length = size+16` re-request is answered from index 1 / byte size+16.
Procedure (read-only; the tool calls only `NvStorageComponent.Read`, never Write, Erase, WipeAll or WipeFactory): wait for the NV queue to be idle; read six factory tags whose size-table value is 1 (so the command goes out with Length = 1, M3-027): 0x80000001 CameraCalib, 0x80000000 BirthCertificate, 0x80000004 CalibMetaInfo, 0x80000006 IMUInfo, 0x80000011 FactoryLock, 0x80000012 VersionMagic. Then read the nine non-factory tags 0x180000..0x197000 (excluding the 0x198000 sentinel), each with the engine's Length = 0x400; if a header says the total exceeds the first blob, the engine itself issues the re-request with Length = total + 16 (M3-028), which is what the record asks about.
Recorded per read: terminal result, every NVCommand sent (tag, Length, op), every NVOpResult received (tag, op, result, the index field, data length, sha256 and up to 4096 bytes of data in hex), and the NV component's log lines (`ReadingRestOfData`, `InvalidHeader`, `TooLittleReadData`, retries). Full frames are in `frames.jsonl`.
Verdicts: `OBSERVED_FACTORY_LENGTH1_REPLIES_NO_REREQUEST_CASE` (the Length = 1 half is answered; no entry on this robot needed a re-request, so the size+16 half is unanswered); `OBSERVED_FACTORY_LENGTH1_REPLIES_AND_REREQUEST` (a re-request occurred; its command length and all replies are recorded); `INCONCLUSIVE` (no Length = 1 reply).
**By design the re-request half will usually be INCONCLUSIVE**: it needs a non-factory entry whose stored total exceeds about 1008 bytes (for example an enrolled face album at 0x184000 created earlier by an official-app session on this robot). This tool writes no NV, so it cannot create one; the `missing` field says so. The Length = 1 reply was already seen once at connect in `20260925-180557-CONTROL` (CameraCalib: 56 data bytes, result 0, index field 0), so the record's evidence text ("only Length = 1024") is stale against that bundle; this check adds the other five factory tags and the raw bytes. See open question 2.

### COLOR_IMAGES (M3-016)

Question: does firmware 2457 emit colour frames in this format, and how does the robot react to EnableColorImages?
Procedure: four 4-second phases, each with the camera stream running: P0 baseline (the stream the engine requested at SyncTime); P1 `CameraSettings.EnableColorImages(true)` alone (the engine path, M3-023); P2 `StartCamera(color: true)` (Restart + EnableColorImages(true) + ImageRequest Stream; **tool-only**, M3-044 COMPATIBILITY_POLICY, no engine counterpart); P3 `EnableColorImages(false)` then `StartCamera(false)` to leave the stream as the engine started it.
Recorded per phase: every completed frame (image id, encoding byte, colour flag, reported width/height, JPEG width, chunk count, raw payload bytes, decoded geometry and component count), the wire's chunk encodings and the first payload byte of each frame's first chunk (the A14 rule keys on encoding 8 with data[0] != 0), chunk status values, every other message the robot sent in the window, engine log lines about camera/image/colour, and two saved frames (`.jpg` and the raw payload). Operator prompt `COLOR_IMAGES.LOOKS_COLOUR` (with optional photo of the saved frame on screen) after opening the saved JPEGs.
Verdicts: `OBSERVED_COLOUR_FRAMES` (a frame with encoding 9 / the colour flag, or a three-component JPEG, arrived); `OBSERVED_NO_COLOUR_FRAMES` (frames completed with the flag on and none was colour); `INCONCLUSIVE` (no complete frame in P1 and P2). Reported with no judgement on whether a colour frame decodes correctly. The matching earlier observation is CONTROL's CAMERA check (0 colour frames, but colour was never enabled there).

### FACE_ROWS (M3-008)

Question: how does the firmware map pair bits to physical display rows, and what is its playback period?
Procedure, patterns (each shown 5 s by streaming the same encoded payload at the 33.3 ms engine pacing, held during the prompt; payloads are built with the engine's own `FaceBitmapCodec.EncodeCanvas`, so only the canvas content is new): `TOP_BAND` (canvas rows 0..7 lit), `BOTTOM_BAND` (rows 56..63), `DIAGONAL_BLOCKS` (top-left and bottom-right), `PARITY_STEP` (middle band, left half on even canvas rows, right half on odd rows: a one-row step between halves, best seen in a close-up photo), `EVEN_ROWS_ONLY` (pair bit 0 only, whole face). The text art and payload of each are saved under `face/`. Each prompt gives a choice list; blank means could-not-tell. The engine's model (pair bit 0 = row 2k, bit 1 = row 2k+1) is stated per pattern in result.json; each answer is compared with it.
Procedure, playback period: three sweeps of a moving bar (30 distinct frames) at host intervals 34, 67 and 133 ms; the operator types roughly how many seconds the bar took (stopwatch). The robot's AnimationState messages (robot timestamp, NumAnimBytesPlayed, audio frames, drop count) are recorded across each sweep.
Verdicts: `OBSERVED_ROW_MAPPING_MATCHES_ENGINE_MODEL` or `OBSERVED_ROW_MAPPING_DIFFERS_FROM_ENGINE_MODEL` only when all five patterns were answered; otherwise `INCONCLUSIVE`. The playback period is **INCONCLUSIVE by design**: whether `NumAnimBytesPlayed` counts the bytes of a bare FaceImage stream (no StartOfAnimation) is not established anywhere (the CONTROL FACE check says so itself), so the counters and the operator's seconds are recorded side by side with no derived period. What would settle it: a measurement tied to something the robot reports about a face frame, not a counter whose meaning is unknown.

### MOTOR_CAL (M4-013)

Question: does the robot honour StartMotorCalibration after its connection-time calibration?
Procedure: wait (up to 15 s) for the connection-time head and lift calibration to complete (`CalibrationComplete`); if it does not, no request is sent and the check is INCONCLUSIVE. Then 1.5 s of baseline states, then the console prints `ASKING NOW`, the tool sends `StartMotorCalibration {head 1, lift 0}` through `Motion.RequestMotorCalibration` (the same message `CalibrateMotorAction` sends, MA18/MA19), and watches 10 s. With `--include-lift` the lift request follows; it drives the lift through its full travel (keep hands clear, no cube on the lift).
Recorded: reports before the request, every MotorCalibration after it (motor, started, auto-started, host time), the head and lift angles and status flags of every third RobotState in the window, the joint angle range, the operator's yes/no on whether the head visibly moved.
Verdicts (head, and lift if run): `OBSERVED_REPORTS_STARTED_AND_FINISHED`; `OBSERVED_STARTED_NOT_FINISHED_IN_WINDOW`; `OBSERVED_MOTION_WITHOUT_REPORTS` (angle range > 0.05 rad or an operator yes, no reports); `OBSERVED_NO_REPORT_NO_MOTION_IN_WINDOW` (nothing in 10 s; not proof the request is ignored); `INCONCLUSIVE` (fewer than 20 states). The check's verdict is the head's. It repeats the older `calibrate` tool (hardware catalog check I) with raw frames and the motion trace added.

### CUBE_TELEM (M4-024)

Question: what makes the robot forward cube telemetry after connection, and what does StreamObjectAccel do on the robot side?
Procedure: the auto block pool is on after the connection (policy M1-042). Wait up to `--cube-wait` seconds (default 45) for a cube to connect, with prompts on the console. Then, with the cube connected: phase 1a 8 s still and phase 1b 8 s moved and tapped, **no StreamObjectAccel sent**; phase 2a 8 s still and 2b 8 s moved after `AddListener` sends `StreamObjectAccel {id, on}` (the production path, M9-017); phase 3, 5 s after `RemoveListener` sends the off message.
Recorded: every inbound cube telemetry message by tag (ObjectAccel 0xF5, Moved 0xB4, Stopped 0xB5, Tapped 0xB6, DataDump, PickAndPlaceResult, ObjectTappedFiltered, PowerLevel, UpAxisChanged) per window with counts, ObjectAccel rate and first-message latency, the StreamObjectAccel sends with times, and the ordered list of all non-state, non-image, non-animation messages from one second before the cube connected to the first telemetry message (what precedes it). Operator prompt: did you move the cube as asked.
Verdicts: `OBSERVED_ACCEL_FORWARDED_WITHOUT_REQUEST`; `OBSERVED_ACCEL_ONLY_WITH_STREAM_ON`; `OBSERVED_ACCEL_ON_AFTER_STREAM_REQUEST_CONTINUES_AFTER_OFF`; `OBSERVED_NO_ACCEL_EVER`; `INCONCLUSIVE` (no cube connected; operator did not move it). What triggers forwarding is not known from the record; the run does not assume a trigger, it records what precedes telemetry and what changes when StreamObjectAccel is sent and withdrawn.
**Existing evidence the manager should reconcile:** the record's text says no cube telemetry arrived after the connection. `20260925-180557-CONTROL` shows the opposite for that run: 76 ObjectAccel messages for the cube, the first about 12.5 s after the cube connected (after `AddListener`), plus one each of Moved, Stopped and Tapped earlier. See open question 3.

## Safety notes

- Motion is limited to: the head nod (MOTOR_CAL), and with `--include-lift` the lift travel. No wheels move; the face and lights are the only other outputs. The robot is on a flat surface; keep fingers out of the lift and head linkages during MOTOR_CAL.
- NV: read-only by construction (the tool calls no write, erase or wipe). Reads of calibration and factory entries only.
- If the link drops, the checks stop and the rest are recorded INCONCLUSIVE ("link lost"); Ctrl+C disposes the connection (DisconnectRequest) and writes the bundle.

## What is INCONCLUSIVE by design, and what is missing

| Record | By design | Missing |
|---|---|---|
| M3-036 (re-request half) | yes, unless an entry is large | a robot holding a non-factory entry with total > ~1008 bytes; the tool writes no NV |
| M3-008 (playback period) | yes | a reading of the robot's per-frame display time that does not depend on the unestablished byte-counting meaning of NumAnimBytesPlayed |
| M3-008 (row mapping) | only if prompts are unanswered | operator answers and photos |
| M3-016 | if no frame completes after EnableColorImages | frames; a coloured scene for the operator question |
| M4-013, M4-021, M4-024 | if their preconditions fail (calibration incomplete; too few states; no cube) | per the `missing` field in result.json |

## Open questions for the manager

1. M4-021: the record's evidence (855 states, no update sent) predates the stack sending the update; the newest CONTROL bundle shows origin 1 frame 0 on every state after the update. Is the record already answerable from that bundle, so this check is a within-run confirmation only? (Manager: the origin probe is dropped: it sent values the engine never sends.)
2. M3-036: the record's provenance names only a Length = 1024 reply; `20260925-180557-CONTROL` has the Length = 1 CameraCalib read (56 bytes, result 0). The record text may need the manager's re-read before this run is used.
3. M4-024: the record says no telemetry was forwarded; the newest CONTROL bundle shows ObjectAccel after `AddListener` and tap/move events. Is the record's evidence stale, or does it mean something narrower (forwarding with no listener)? This run's phase 1 answers the narrower reading.
4. The manifest `test` field for M4-021 and M4-024 is empty and for M3-008, M3-016, M4-013 is null; only M3-036 names `M3DeviceTests.M3_036_Hardware`. Should these records name `M3M4HwTests` (structure) once the run is approved?
5. `StartCamera(color: true)` (P2) is tool-only (M3-044). It is included to separate "EnableColorImages alone" from "EnableColorImages plus a fresh stream request". Acceptable?
6. MOTOR_CAL sends a request whose effect on a robot carrying a cube is M12's concern (MA20); the check assumes no cube is on the lift.
