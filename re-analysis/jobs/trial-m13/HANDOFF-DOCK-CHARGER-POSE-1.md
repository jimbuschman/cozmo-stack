# DOCK-CHARGER-POSE-1 handoff

The 2026-11-19 evening recording produces the engine's `FAILED_TRAVERSING_PATH` verdict.
Image 18322 confirms a charger pose of (346.129, 24.070, 7.288) mm with 17.505° tilt. All 12 sightings
during path 15 update the sighting time and image rectangle while retaining that pose.
Image 18459 is the first steady sighting after arrival. It refreshes the pose to
(349.298, 13.788, -7.535) mm with flat rotation. The planner's goal was already copied.

| Step | Source and replay |
| --- | --- |
| Camera and marker | EncodedImage promotes wire encoding 8 with a nonzero first payload byte to 9 at 0x004F1D7A..0x004F1D9C. The captured frames use that color path. RGB-to-grey is 0x00872998..0x008729CA. Shipped ARM JPEG and marker oracles find code 2 in 18318/18322 and the closer frames. Rolling shutter compensation uses captured F4 via 0x00539258/0x005394BC. Historical F0 and camera construction use 0x00513AB8..0x00513B16. Calibration from NV 0x80000001 and body v4 use the cleared-distortion branch 0x0065AD54..0x0065AD9C. |
| Estimate and creation | Charger marker is 27 × 20 mm at (-90° Z, 86, 0, 22 mm), 0x004E9BBC..0x004E9C46. The solver at 0x0085D77E..0x0085D8E2 recovers the tilted view. Creation's 5° clamp (0x00625548..0x00625578 and 0x004E024E) retains it. |
| Stored pose and report | Charger localization vslot 0x004EA66E returns false. Insert discards the localization candidate and calls AddVisualObservation through 0x0050CFC4..0x0050CFE6 and 0x0050CDF6. The existing-object moving-camera guard 0x00505E98..0x00505EF8 suppresses the write. The 20° clamp at 0x00505F10..0x00505F22 requires DockingComponent+0xC, set by DockWithObject at 0x0063BAAC..0x0063BAB6 during alignment. Mount.Init stores the separate charger ID at Robot+0x334 (0x0054E120). Both stages follow the pre-dock drive. Broadcast at 0x006211DC takes the stored object. Projection and Pack at 0x0061FED8 and 0x007131E2 use its pose with the current camera. |
| Recovered difference | The commit used the worker's computed pose carrier for camera motion. The binary calls WasCameraMoving at the observation time (0x00620C7E / 0x0050CFD8 / 0x0050CEAA). The live path now queries interpolated raw history (0x00641C3C, 0x006418B2..0x006418C4) and treats a missing bracket as movement (0x0064194E). The status predicate is (status & 0x8200) != 0x200 at 0x00642802. All recorded approach brackets indicate moving wheels or a head still settling. |
| Path and arrival | GeneratePreActionPoses 0x004E9FB0 and copy constructor 0x0050DF00 produce the MAIN goal (194.433, 53.195, 62.769) mm. Planner inputs use the -20 mm drive center and zero Z at 0x0064AE68..0x0064AE9C. Firmware completed path 15. Native 0x0055ACA2..0x0055B05C accepts translation at the recorded arrival but rejects its full rotation, 0.30570 rad (17.52°) versus 10°. The logs at 0x0055AE94 and 0x0055B030 now use the native argument order, and MCP `why.snapshot.pathArrival` retains the actual comparison. The later visual verification at 0x00559A86..0x00559AE4 has its own yaw/offset check and was not reached. |

The marker on the charger has 86.4% of the area of a cube's 25 × 25 mm marker and is 20% shorter
in height. The detector uses common image-space gates (19-pixel minimum component area at
320 × 240, 0x008756E6..0x00875770, and a 25-pixel minimum quad cross product at 0x00892B5C).
In stationary frame 18174, the visible tag's surviving dark component is a 53-pixel partial contour,
approximately 26 × 13 px. Its traced 77-point boundary fails four-line corner extraction (0x008A5DB8).
Frame 18322 has a 150-pixel contour, 31 × 20 px, with a 103-point boundary that yields four corners. Native detector parity on four stationary frames places the loss before marker decode.
The recording establishes an image-dependent range for this setup.

The regression takes private frames via `COZMO_DOCK_POSE_BUNDLE`. It reassembles F2 and uses F4/F0,
feeds selected observations through BlockWorld, and runs the real queued mount planner and arrival
with recorded start/final odometry and C6 event kinds. The fixture supplies animation feedback and
rebases path and pose identifiers. The ARM arrival oracle uses the captured pose and contacts-free
arrival boundary. Offline evidence establishes the recorded failure and later flat update. Operator
hardware acceptance remains open. Fidelity references retain their existing scope and status.

Build `4d1ddd98` passed 150 focused tests, the full Debug suite (8,793 of 8,793 in
56.52 minutes), and `fidelity.py --check` for 764 records. The first full attempt
caught the restored log missing from the public action trace. The corrected run
verified the trace and both evening-session replays.
