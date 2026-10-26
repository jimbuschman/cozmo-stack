# Claude check: B-M11-22 (BlockWorld map stamp and the object-moved strategy read VisionComponent+0xE8)

- **Checked:** 27316c63 (full suite 6,243/6,243) against `jobs/B-M11-22.md`.
- **Verdict: PASS.** No record settles.

## Supported

- **`World.LastImageTimestamp`** now reads `CameraSettings.LastProcessedImageTimestamp` (VisionComponent+0xE8,
  written after the result handlers by B-M11-21; `VisionSystem.cs:100`). It replaces the max-of-frame-clocks stand-in.
- **`Maps.ImageTimestamp`** defers to it, and the "unsourced stand-in" MISSING is gone. The no-vision case keeps its
  visible MISSING.
- **The map stamp calls** at 0x0067EFCE, 0x0067F0A6 and 0x0067F32C call the PLT for `Robot::GetLastImageTimeStamp`
  (0x004AC6F4); re-disassembled here.
- **Extra reader changed**, as the job allowed: ObjectPositionUpdated ShouldReactToTarget 0x00612584 and
  RobotReactedToId 0x00612A9E call the same PLT 0x004AC6F4, re-disassembled here. So `Behaviors.cs:739` moving from
  `LastRawFrameTimestamp` to +0xE8 is source-backed.
- **No other `World.LastImageTimestamp` reader** remains in `cozmo-stack/src`.
