# M3M4-HW, second run: M3 + M4 acceptance (manager, 2026-10-19)

**Approved for the operator's run (manager, 2026-10-19)** on main from the commit that adds this file.
It replaces the 2026-10-12 run (`hardware/20261012-062328-M3M4-HW`), which predates most of the M3/M4 builds.

The tool is `m3m4-hw` (`cozmo-stack/src/Cozmo.Conformance/M3M4Hw.cs` and `M3M4HwSettle.cs`).
- **The run:** it connects through the production engine (`CozmoRobot.Create`, `ConnectToRobot`), records every UDP frame
  in and out, runs the checks below, and writes one bundle.
- **The robot:** it never writes the robot's NV storage.
- **The repository:** it never commits and never edits the manifest.
- **Verdicts:** every check ends OBSERVED_* (with the rule that produced it) or INCONCLUSIVE (with what is missing). There
  is no PASS: the manager judges the bundle, and no hardware result raises a record's provenance.

## What it covers

| Check | Records | Operator does |
| --- | --- | --- |
| DAS_EVENTS | M3-055, M3-060 | nothing (passive) |
| ORIGIN | M4-021 | nothing |
| NV_READS | M3-036 | nothing (read-only) |
| CAMERA_PARAMS | M3-021 | nothing |
| COLOR_IMAGES | M3-016, M3-050 | point him at something coloured; open two saved frames |
| FACE_ROWS | M3-008 | answer what the face shows (5 patterns, photos welcome); time 3 sweeps |
| TRACK_UNLOCK | M4-047 | nothing |
| ANIM_BUDGET | M3-012 | nothing (head and lift move, wheels do not) |
| MOTOR_CAL | M4-013 | say whether the head moved |
| CLIFF | M4-019, M4-038 | with `--allow-drive`: set him facing a table edge, hand ready; then lift him by hand |
| CUBE_TELEM | M4-024 | wake one cube; keep it still or move it as prompted |
| CUBE_LIGHTS | M4-018, M4-051 | say whether the cube flashed |
| DISCONNECT_QUIET | M4-027 | nothing (judged after the tool disconnects) |

The first seven records (M3-008, M3-016, M3-036, M3-050, M4-013, M4-021, M4-024) are HARDWARE_ONLY. Their checks are the
2026-10-12 design, unchanged except for the four harness defects below. The other ten records are settled from source. Their
checks cover settling pass B's list (`research/20261018-m3m4-settling-b.md`, "Hardware coverage"), with cube lights added
for the M4 light records.

## Physical setup

- The computer is on the robot's Wi-Fi (172.31.1.1). Close the official app and every other client.
- Cozmo is on its treads on a clear flat table, 30 cm free all round, **off** the charger, battery not low.
- Nothing is on the lift.
- One light cube is powered and nearby; tap it to wake it when CUBE_TELEM asks.
- For COLOR_IMAGES, something obviously coloured is in front of the camera.
- For CLIFF with `--allow-drive`: a table edge with a clear straight path of about 10 cm to it. Keep your hand just below
  the edge to catch him. He drives at 30 mm/s for at most 6 s (18 cm) and is told to stop on cliffs first.
- Keep the console and the robot in view together; prompts appear on the console. A blank answer means "could not tell".
  Answers can be typed as the word, its number in the list, or a unique start of it ("bot" for bottom).

## Operator commands

From the repository root, on current main:

```powershell
dotnet build cozmo-stack/Cozmo.sln
dotnet cozmo-stack/src/Cozmo.Conformance/bin/Debug/net9.0/cozmo-conformance.dll m3m4-hw 172.31.1.1 --resources <path to cozmo_resources> --allow-drive
```

Options:
- `--only A,B` runs a subset; the order stays fixed.
- `--include-lift` also calibrates the lift (it travels its full range).
- `--cube-wait <s>` sets how long to wait for the cube (default 45).
- `--photo KEY=path` pre-supplies a photo.
- `--operator <label>` names who ran it.
- `--out <dir>` sets the bundle location.
- `--yes` asks nothing.
- Without `--allow-drive`, CLIFF skips the edge and only does the hand lift.

Ctrl+C stops the run, disconnects and still writes the bundle; a 20-minute watchdog does the same.

**First run of the new checks:** the seven added checks have only been dry-run against the loopback stand-in (the bundle
writer and check list). If one throws or misreads on the robot, it is recorded INCONCLUSIVE with the exception. The
manager fixes the harness and asks for a rerun of only that check (`--only`), each rerun its own bundle.

## The added checks

- **DAS_EVENTS (passive).**
  - Looks in the engine log for `robot.handle_robot_set_head_id`, `robot.handle_robot_set_body_id` and the startup flush
    result (`das.upload` / `das.upload.fail`).
  - The manager checks their text and data against M3-060 (`0xbeef%04x%08x`, `0xbeef%04x%04x%08x`) and M3-055.
- **CAMERA_PARAMS.**
  - Records the robot's DefaultCameraParams, the SetCameraParams the engine sent after it, the limits, and the engine's
    camera log lines (including the M11-059 IsInitialized boundary).
  - Then, through the public `SetCameraSettings`, sends four requests: 20 ms / gain 1.5; exposure 65535 (out of range);
    gain 100 (out of range); then the original values. For each it records the return value, the SetCameraParams sends and
    the CurrentCameraParamsChanged events.
- **TRACK_UNLOCK.**
  - Public `LockTracks(0x4)`, `UnlockTracks(0x4)`, then `UnlockTracks(0x4)` again.
  - M4-047's miss path should log "not currently locked", print the lock state and re-send EnableAnimTracks. The track
    sends of each step are recorded.
- **ANIM_BUDGET.**
  - Loads the Wwise banks from the resources if present.
  - Plays the longest shipped clip that has audio and no body track, 4–15 s (picked at run time; its name is in the
    result).
  - Every AnimationState is recorded beside the engine's BytesStreamed/FramesStreamed at that moment, along with the robot's
    drop count and any `NegativeMinBytesFreeInRobot` / `BytesStreamedOverflow` log (M3-012).
- **CLIFF.**
  - Since connect: every SetCliffDetectThreshold and EnableStopOnCliff, expected 50 on the first state (SC4).
  - With `--allow-drive`: the tool sends EnableStopOnCliff(true) (tool-only safety send, public SC8), then
    DriveWheels(30, 30). At the first CliffEvent or RobotStopped, or after 6 s, it sends StopAllMotors at once. The
    ordered CliffEvent / RobotStopped / PotentialCliff / StopAllMotors / EnableStopOnCliff / threshold messages are recorded,
    with the HandleCliffEvent logs (Detected at a pose, M4-038) and the API CliffDetected reports.
  - The drive also covers the 400 threshold after 50 mm of travel.
  - Then a 6-second hand lift.
- **CUBE_LIGHTS.**
  - On the connected cube, public `PlayLightAnim(type, "Flash")` with a completion callback.
  - Records the SetCubeGamma / CubeID / CubeLights sends and when the callback ran (M4-051), and the operator says whether
    the cube flashed.
- **DISCONNECT_QUIET (after teardown).**
  - Lists every outbound data message after the first DisconnectRequest frame, and those in the 2 s before it.
  - CozmoRobot.Dispose's StopAllMotors / DriveWheels(0) are a stack decision note and belong before it.

## Harness defects fixed from the 2026-10-12 run

1. FACE_ROWS answers were compared as literal text. They now map to the offered choice (word, number or unique prefix).
2. `image-encodings.jsonl` counted non-data sub-messages whose first byte equals the Image tag. Only data sub-messages are
   now counted.
3. ORIGIN needed states before the update, but the engine sends the update before the first state. With no prior states
   and every settled state carrying the update's values, the verdict is now
   OBSERVED_REPORTS_UPDATE_VALUES_FROM_FIRST_STATE, with the earlier no-update bundle as the baseline.
4. A photo prompt accepted a folder. It now asks again for a file and records nothing for a path that isn't one.

## Bundle

`re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-M3M4-HW/` contains:
- `result.json`, `env.json` (git HEAD, command line, sha256 of both tool files and this document), `records.json` (the 17
  records as read);
- `events.jsonl`, `frames.jsonl`, `image-encodings.jsonl`, `camera/`, `face/`, `operator/`, `hashes.json`;
- copies of the two tool files and this document.

Copy the whole folder back. `frames.jsonl` can be large; the manager decides what goes into git.

## Safety

- **What moves:** the head (MOTOR_CAL, ANIM_BUDGET), the lift (ANIM_BUDGET; MOTOR_CAL only with `--include-lift`), and the
  wheels only in CLIFF with `--allow-drive`, slowly, toward an edge you are guarding.
- **NV storage:** reads only.
- **If the link drops:** the remaining checks are recorded INCONCLUSIVE.
