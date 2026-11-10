# CAMERA-CALIBRATION-INSTALL-SETTLEMENT-1

## Claims and evidence

- Native NV callback 0x0065AB68 constructs and passes a nonnull calibration at 0x0065ADE0 before its enable store at 0x0065AE80. The outer camera gate at 0x0065171C and 0x0065172A reaches the inner UpdateCameraCalibration at 0x0065174C only on successful pointer replacement.
- Camera::SetCalibration at 0x0085DEDC compares identity. Null with no stored calibration and the same pointer return false. A distinct nonnull pointer is installed (0x0085DEFA–0x0085DF08). Only a successful inner install reaches MarkerDetector::Init with rows and columns (0x006B1E6C–0x006B1E78). It resets parameters at 0x008752E6, then fixed scratch storage at 0x008752EA–0x008752F4 via 0x00874E20. Native Detect resets that scratch again at 0x008753C2–0x008753C8. Managed per-call scratch gives the same observable result.
- Null with a stored calibration invokes VERIFY at 0x0085DF12. The original error log, global error flag and conditional logger flush are at 0x0080D9C2, 0x0080D9F4 and 0x0080DA28. The retained path now emits the error, sets the flag, flushes the existing logger and preserves the public vision log. It keeps the stored calibration and parameters.
- The stock constructor starts with no calibration and does not call Init (0x006B0016 and 0x006B00F0). A host-supplied constructor calibration keeps its existing semantics. The NV recipient now checks its captured owner inside the same gate as the nonnull calibration install and detector reset. Retiring the old owner cannot reset the replacement's calibration.

## Files and tests

- Vision/VisionSystem.cs implements the VERIFY effect and binds the calibration write/reset to the captured owner. Camera.cs updates the NV install comment.
- M3DeviceTests passes actual fake-transport NV replies to an attached vision owner. It checks that a successful reply resets changed detector parameters before enable and that a failed reply does not reset them.
- RFixBatch3Tests checks null and identity branches, the source error and preserved detector values, and rejects a stale owner's outer install after replacement. The stale-owner test supplies the late callback at the recipient and does not force the old scheduling window.
- Affected calibration, camera, owner and frame suite passed 55/55. The batch gate is recorded in QUEUE.md.

## MISSING

- The separate factory-test calibration producer, broader NV protocol, image readiness, historical camera and ground LUT remain with their existing records. No hardware check is claimed.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager independently reopened both Camera gates and the VERIFY call in the ELF. Review found the old-owner window, which was closed without placing diagnostic callbacks under the owner gate.

## Records

- **M11-039.** Proposed for Claude settlement for the native calibration identity, reset and defined error path.
- M3-022 and M11-049 own the NV and enable behavior. M11-050 and the ground calibration records own the historical camera and LUT. Their statuses are separate.
