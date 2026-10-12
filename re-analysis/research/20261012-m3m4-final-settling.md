# M3+M4 final settling pass (Opus, 2026-10-12)

The pass ran at HEAD 6db1209e, read-only.
- **Method:** a scan of every logger call (sEventF, sWarningF, sErrorF, sChanneled*) in each record's native functions,
  with each key checked against the C#.
- **Adopted:** by the manager on 2026-10-12. This file is the checked source for job B-M3M4-9.

## Settled (6)

- **M3-043:** Write's four checks, keys, formats, arguments and fail tail (0x006444FC..0x00644730); the ERASE dispatch
  order (0x0064507E..0x00645186); the write timeout (0x006456F4..0x00645758). The vector overload is dead.
- **M3-047:** 0x0051349C..0x00513526 (key, format, arguments, `$data` key 0x00513550); 73 call sites.
- **M3-048:** the log mechanism.
  - A 1024-byte buffer at every level (0x0080D014, 0x0080D13C, 0x0080D440).
  - The "(tc%04u) " then ": " prefix (0x0080DCC0..0x0080DD2E).
  - `$data` kept structured.
  - Missing call sites belong to their own records.
- **M3-034:** handoff only.
  - All six sinks report a named MISSING, owned by M15-033/034/035/036, M14-012 and M15-014. Each has its own record.
  - Cleanup: the callbacks sit at vtable slot +0x18, not the "+0x14" the evidence cites (+0x14 is the delete thunk).
- **M4-008:**
  - Event format "%u %.2fV (%d lost)" and `$data` "%.2f,%.2f" (0x005372B2..0x00537302).
  - Skip gate, map stores, broadcast.
  - The cliff store at 0x0063401A..0x00634036.
- **M4-035:**
  - Info, payload (persistent slots 0..4, skipping factoryID 0, +0x7C flag) and the 0x7E broadcast
    (0x0061AE54..0x0061AF10).
  - Order after ConnectToObjects, before the +0x74 store (0x0061A9BA..0x0061A9C4).

## Kept, with checked rows (B-M3M4-9)

**Logs emitted while a component lock is held** (the defect class B-M3M4-5/6 fixed elsewhere):
- **M3-012:** `StreamSendBuffer.Drain` (AnimationScheduler.cs:186-203) and `SendDirect` (:212-220) call `Send()`
  under `_gate`. A failed send's "Robot.SendMessage" warning then runs LogEntry subscribers inside the stream lock.
- **M3-021:** `Emit` runs under `_gate` at Camera.cs:682, 687, 690, 704, 736 and 742, and `_send` at :691.

**Logs never emitted:**
- **M3-024:** info "RobotIsPhysical", channel Unnamed, format "%d", argument isNull (0x00536942..0x00536956).
- **M3-028:** debug "NVStorageComponent.HandleNVOpResult.ResizeBlobToValidData" on the fits-in-first-blob path
  (0x006438E2..0x006438FC).
- **M3-032:** the Gate A debug "Robot.Update" is on channel "Unnamed" (0x00513DA6..0x00513DC2, 0x00BE3FEC).
  CozmoEngine.cs:1194 passes null.
- **M3-053:**
  - The null/blank-name diagnostic is a visible MISSING; its arguments are recoverable (libDAS 0x742E..0x75EE, format
    at file offset 0x32D3C).
  - The C# tests only `Length==0`, not blank.
- **M4-009:**
  - The warning ActiveObjectInDifferentFramesWithDifferentIDs (0x00534042..0x005340CC) is a stack MISSING
    (Cubes.cs:730) that fires on every unknown-id movement.
  - The UpAxis name prints a number where the engine prints "(null)" for values above 7.
- **M4-010:** AddObjectToPersistentPool's info and both error-plus-errG paths (0x0061ACB6, 0x0061AD04, 0x0061AD5E;
  CubeConnections.cs:552-563).
- **M4-017:**
  - The "InvalidLocator" info on the first StartLooping (0x006324D2..0x006324E4).
  - GetAnimation's "InvalidName" sErrorF with errG/break (0x00587446..0x0058749A).
- **M4-018:**
  - The Blend body and the SendTransitionMessage payload are still MISSING inside PlayLightAnim.
  - Logs:
    - Update 0x00637904: PlayingPrevious 0x00637AA4, SetLightsFailed ×3 (0x00637AFE / B34 / B7E), NoAnimsLeftOnLayer
      0x00637BD6.
    - SendTransitionMessage.NullObject 0x00638150.
    - StopAllAnimsOnLayer AllObjects / BadObject 0x00638DE2 / 0x00638E72.
    - StopLightAnim 0x00638F9C; StopLignAnim.Result 0x00639300.
    - ActiveObject.GetLEDState IndexTooLarge / NegativeIndex 0x0063958E / 0x006395CC.
    - SetObjectLights.NullActiveCube 0x0063A204.
    - EnableCubeSleep "%s cube sleep" 0x0063A4D0.
- **M4-019:**
  - SendCliffDetectThresholdToRobot info "...CliffDetectThresholdUpdated", "New cliff detect threshold %d (old threshold
    %d)" (0x00634280..0x0063429C). It fires on every 50/400 change; missing at Sensors.cs:546.
  - IncrementSuspiciousCliffCount sEventF ".Count" (0x00634534) and ".NewThreshold" (0x0063457A); Sensors.cs:475-489.
  - UpdateCliffDetectThreshold debug "...CliffValueWhileStopping" "%d - %d" per walked sample
    (0x0063442E..0x00634440).
  - HandleCliffEvent:
    - info "Detected at %.3f,%.3f. DetectedFlags = 0x%02X" (0x00535602);
    - info "Undetected" when flags == 0 (0x0053564C), which is live;
    - error "ComputeCliffPoseFailed" with errG/break (0x00535682..0x005356BC).
  - HandlePotentialCliffEvent: debug "OnChargerPlatform" (0x00535858) and the "not in drone mode" warning (0x00535948)
    before StopAllMotors; Sensors.cs:712-717.
  - `_cliffVariance` is read without the lock (Sensors.cs:823).
- **M4-027:** both remaining MISSINGs have no effect natively.
  - The cliff logger +0x44 is created only by LogRawData, which needs EnableRawDataLogging 0x00634848. That function
    has no caller.
  - FaceLayerToRemove +0x88 is filled only under +0xA8 != 0, and +0xA8 is never written nonzero (M4-016 C11.3).
  - Remove the runtime MISSING (Motion.cs:1121) and record both as natively dead.
- **M4-036:**
  - The JsonCpp reader-error text has no owner. It is now M5-051's G5b, a RECOVERABLE_GAP.
  - The loader parses with System.Text.Json (Lights.cs:112, `AllowTrailingCommas`, rejects trailing data) instead of
    the stack's JsonCpp port (FirmwareJson.cs `Json.TryParseFirst`), so it accepts different malformed files.
- **M3-045:** nine non-JPEG decoder bodies are unported (visible MISSING); the short-WebP over-read is UNKNOWN. That
  needs extraction, not this job.
- **M3-051:** stays RECOVERABLE_GAP.
  - libDAS.so ships, so sink (b)'s queue and file rolling (0xB47C; DASConfig limits 150000/5000/15) can be read.
  - Only the SQS upload is BLOCKED_EXTERNAL. The network enable comes from the app profile.
  - Split it into three records: local queue (recoverable), upload (BLOCKED_EXTERNAL), DASEnableNetwork input
    (COMPATIBILITY_POLICY).

## Logs missing from records already settled (reopen for these sites)

- **M3-029:** debug "NVStorageComponent.HandelNVData.ReceivedBlob" on every applied blob (0x006435D4).
- **NV InitSizeTable:** info "Max size of 0x%x: %u" (0x00643BCC). The owner is to be confirmed (M3-027).
- **M4-011:**
  - BlockFilter.Init (0x0061A220).
  - Load ×3 (0x0061A4BA, 0x0061A55E, 0x0061A5A0).
  - Save / Save.Error (0x0061B174, 0x0061B260).
  - RemoveObjectFromPersistentPool (0x0061B3DE, 0x0061B42E).
- **HandleActiveObjectAvailable:** info "ObjectAvailable" (0x005339C8) and warning "UnknownType" (0x00533A2A), on
  every advertisement. Owner M4-025 or M4-010.
- **M11-041:** sEventF "robot.accessory_connection" (0x00533BA2, 0x00533C72) and info "Recvd" (0x00533CD4).

## New-record candidates

1. **CliffSensorComponent::ComputeCliffPose and HandleCliffEvent's AddCliff** (0x00535548..0x005356E6): silent today.
2. **libDAS sink (b), split three ways** (see M3-051).
3. **The BlockFilter game messages 0xE6/0xE7/0xEA/0xEB:** they need an app-boundary ruling or a record.
4. **Unreachable, no record needed:** EnableRawDataLogging/LogRawData; the MoveHeadToAngleAction FaceLayerToRemove
   path.
