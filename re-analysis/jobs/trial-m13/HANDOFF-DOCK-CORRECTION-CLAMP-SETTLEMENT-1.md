# DOCK-CORRECTION-CLAMP-SETTLEMENT-1

## Claims and evidence

- The native correction sends unchanged relative XYZ from 0x0063C14A–0x0063C180, then constructs the binary32 40-degree limit 0x3F32B8C2 at 0x0063C182–0x0063C188 and calls ClampPoseToFlat at 0x0063C194. Corrected local yaw is obtained as f32, widened for pi/2 and the angle slot, and narrowed once at 0x0063C1A2–0x0063C1CE.
- The clamp selects the signed dominant column of the root Z row (0x0087733C–0x0087736C, 0x005507A0–0x00550852). The native Radians comparison rejects at the bound and within its near epsilon (0x0087737E). A passing case uses the corresponding local column, checks angle and cross length, and left-multiplies the correction into the local quaternion (0x00877388–0x008774EC). The canonical 0x48 path already used this helper. The live legacy DockAsync path used its local frame for the gate. It now passes the relative quaternion and the marker-world quaternion to the same helper. The legacy marker-world transform is flat by construction.
- This change does not alter marker choice, pose estimation, translation or offsets. The separate 20-degree world pose-confirmation limit belongs to M11-006.

## Files and tests

- Manipulation/Docking.cs shares the existing NativeDockingGeometry clamp with the legacy OnFrame recipient.
- NativeDockingCorrectionTests.RealFrameSendsNativeOracleClampAngle reaches the actual camera frame, detector, retained dock target and 0x48 send with an unrelated rendered marker, a supplied target pose and same-time robot history. Mixed yaw/tilt cases cover positive and negative dominant Z inside, at and outside the gate, including the Radians epsilon. Original ARM 0x00877330/0x0084AA1C under declared host libm supplied independent angle expectations. The test also checks XYZ, timestamp and flags.
- LegacyDockFrameUsesMarkerRootForGateAndLocalForCorrection opens public DockAsync and supplies projected corners directly at the legacy frame recipient. It reaches the existing PnP and actual wire send. The native root/local case expects 1.95477 rad; the old local-only gate was restored temporarily and failed at 1.81459 rad. The final test awaits cancellation before fixture teardown.
- Affected 178/178 and final focused 28/28 passed. The batch gate is recorded in QUEUE.md.

## MISSING

- Original phone libm rounding remains under M13-023(e). The pose inputs and full correction producer and physical docking remain separate from the verified clamp.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager independently ran the shipped ARM clamp/yaw against mixed threshold and parented cases. Review checked the legacy live path and the separation of input geometry from the clamp. The manager requested awaited test cleanup.

## Records

- **M12-019.** The bounded live 40-degree clamp is complete on both correction paths. Proposed for Claude settlement with only the existing host libm equivalence boundary.
- M12-013/M12-014 own the 0x48 layout and policy, M11-006 the 20-degree world clamp, and M11-003/M11-050/M11-052 the marker and historical inputs. No status changes.
