# VISION-INIT-SETTLEMENT-2 and VISION-INIT-CAMERA-GATE-1: Claude check (manager, 2026-11-10)

Built by Claude while Codex was out of usage: an extractor census, an implementer build, an independent verifier
(PASS, no blocking findings). **This settlement was made on Fable and is marked for an Opus spot-check.**

## M11-059 → EXACT_SOURCE

**The census (VISION-INIT-SETTLEMENT-2):** every step of VisionComponent::Init 0x00650D20..0x006510F6,
VisionSystem::Init 0x006B0658..0x006B141A and both album branches is read and built. The Codex jobs
VISION-INIT-SETTLEMENT-1, FACE-GUEST-INIT-1, FACE-ALBUM-FILE-BOUNDARY-1, FACE-NV-ALBUM-RESTORE-1 and
FACE-ALBUM-FILE-REQUEST-1 (checked 2026-11-10) built all but one step.

**The one gap, now built (VISION-INIT-CAMERA-GATE-1):**

| Step | Address | Behaviour |
| --- | --- | --- |
| The gate | 0x00657CA8..0x00657CB0 | `ldr r0,[r5,#0x1c]`; IsInitialized 0x006B2CD6 (`ldrb +0x58`); `beq 0x00657D62` |
| Not initialized | 0x00657D62..0x00657DA0 | sErrorF NotInitialized (0x00657D72), then the error tail 0x00657DE4; no SetCameraSettings, SetCameraExposureParams or SetNextCameraParams |
| The flag | 0x006B000A, 0x006B0678, 0x006B141A | Zeroed by the constructor, cleared on entry to Init, set only by Init's tail |
| The exposure | 0x00657CB2..0x00657CBA | The handler loads the configured u16 (0x01051054) and passes it to SetCameraSettings (0x00657CC8) |

I re-disassembled the gate and the tail. The verifier resolved the PLT stub to IsInitialized and scanned Init for
every write to +0x58.

**The stack:** `CameraSettings.VisionSystemInitialized` was the constant true. It now reads the current owner's
worker flag (`VisionSystem.NativeWorkerInitializedFor`). When the stack cannot know the flag (no owner, a superseded
or destroying owner, no VisionSystem, an Init whose outcome is unknown), it reports a visible MISSING and sends
nothing; it never answers true.

**Tests** (NativeVisionInitializationTests, 44/44; affected classes 263/263):
- a defined Init failure (two cases), then DefaultCameraParams: NotInitialized, the error flag, nothing on the wire,
  nothing queued, the constructor limits untouched;
- a successful Init passes the gate and sends {gain, 16, false};
- the configured exposure, changed to 17, reaches the wire (added by the manager: the verifier found the repair of
  M11DecodeBoundaryTests had dropped the only such check);
- the unknown state reports MISSING and sends nothing.

Four existing tests were repaired. Each had called the handler with no Init or a failed one; the verifier found none
circular.

**Inventory correction:** M11-059's evidence said the file branch and the Init body were unread. They are read, so
the provenance, authority and evidence were rewritten and M11-vision re-approved under the standing authorisation.

## Boundaries recorded in M11-059's `unresolved`

- **Drop stats (+0x98/+0x9C):** logging only. The sole consumer, FUN_00653E30, stores only its own counters.
- **NV-read failure warnings (0x0065108E..0x006510A2):** unreachable, since both tags are constants.
- **M1-034:** a bad InitialModeSchedules entry crashes the original at 0x006AF21A; the stack skips the Init.
- **Inputs never produced:** a non-string FaceAlbum, an unknown mode key, a non-finite profiler value.
- **CLAHE constants:** equivalent; +0x350/+0x354 are written only by Init and ApplyCLAHE 0x006B44E0.

## Manager decisions

1. **Calibration-image capture and tool-code reading are not supported** (manager exclusion, 2026-11-10, reversible):
   AddCalibrationImage 0x006B3C4C, ComputeCalibration 0x006B84D8, ReadToolCode 0x006B5B98, ClearCalibrationImages
   (G2E 0x005283F4). They are factory and app calibration machinery; M11-012 keeps the nominal calibration.
2. **A bad host config fails clearly** (recorded on M3-062): with no resources root or no readable vision_config.json
   the stack holds the album and backup reads and ready-to-stream, and sends no SetCameraParams. The original with
   no data platform skips Init and still reads backups.

**Hardware note:** the gate now needs the host's `config/engine/vision_config.json`. A hardware host without it sends
no camera parameters (the CAMERA_PARAMS conformance check depends on that reply).

## Queued cleanup (verifier, no behaviour)

- `ReportMissing` dedupes process-wide, so the gate's MISSING shows once per process, and two negative test
  assertions can pass vacuously.
- `Finish()` sets Initialized when a host fixture replaced a guest (after its own visible MISSING).
- Stale doc comments at CozmoEngine.cs:2034-2039.

**Records settled in this check: 1. Total: 37.**
