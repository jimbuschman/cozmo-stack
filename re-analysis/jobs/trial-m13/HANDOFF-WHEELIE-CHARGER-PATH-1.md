# WHEELIE-CHARGER-PATH-1 handoff

| Recorded input | Checked path | Visible result |
| --- | --- | --- |
| F19 wheelie on cube 1 | PopAWheelie handler 0x0052A8F8 → DriveToPopAWheelieAction 0x0055D1E8 → rolling approach and IDock setup → dock mode 9 | The robot performs the lift and drive sequence. The final action result is SUCCESS. |
| F19 firmware lifts, drives and returns B8 success | B8 0x00533780 stores success at DockingComponent+5 → IDock waits for docking and movement to stop at 0x005522C0–0x005523D8 → PopAWheelie Verify 0x0055295C–0x00552A72 reads that flag and the latest F0 pitch | About 76° satisfies the 1 radian minimum. The separate treads classification commits OnBack about 420 ms after completion. |
| F20 mount on charger 0, origin 7 | MountCharger handler 0x0052A292–0x0052A2AE → DriveToAndMountChargerAction 0x0054E7F8 → charger pre-dock goal 0x004E9FB0 → PathComponent 0x0064AE68 → path 15 | The final point turn faces −2.081285 rad, the heading of the tilted charger report. |
| F20 firmware completes path 15 | C6 Completed → DriveToPose Ready compares MAIN to the copied pre-dock goal at 0x0055ACA2–0x0055B05C → drive child result propagates to MCP | FAILED_TRAVERSING_PATH. The existing arrival diagnostic names the rejected full rotation. The mount child does not start. |

## F19 wheelie

IDock starts the firmware sequence with 0x42 (0x005522AE → 0x0063BA44 → 0x0063BD50). The captured payload is (0, 60 mm/s, 200 mm/s², 500 mm/s², action 9, false, 0, 0, false). Between this packet and B8 there is no separate host lift-height or drive-wheels command. F0 shows the lift rising to 46.5° while the wheels are still stopped, then an approach near 60 mm/s. C5 carries action 9. The post-dock push reaches about 150–160 mm/s as the lift comes back down and the pitch rises. The measured motion describes this robot run. The engine supplies the mode and its profile.

The B8 report has success true, signed result 2 and block status 0. Verify reads pitch from Robot+0x304 (0x0051237E, F0 writer 0x00512974–0x00512982) and returns success when the native Radians comparison against 1.0 is false. It has no upper pitch or later balance check. The physical treads classifier uses the 1.30027 rad center at 0x00511E80–0x00511E8C and a 1 s OnBack debounce at 0x00511FAE–0x00511FEE. The capture shows leaf completion at host 934412.912 ms and OnBack at 934832.691 ms. The replay uses the actual 0x42 body, firmware signature, C5, B8 and F0 reports after an offline simulated approach, then checks SUCCESS followed by OnBack. It preserves the native success decision. The existing M1-034 guard on Pop's undefined full completion payload remains part of this path (M13-041).

## F20 charger

The observed charger at robot timestamp 1013145 (image 34124) was at (−64.702, −133.164, 12.369) mm with 14.83° tilt. Its pre-dock pose is approximately (12.201, 3.573, 60.612) mm. The immediate neighbors at 1013075 and 1013210 were flat with headings near −1.914 and −1.921 rad. The native heading projection 0x0084AA1C gives −2.081285 rad for the tilted pose, matching path 15's final 0x3F at (20, 20) after conversion to drive-center coordinates and lattice quantization. The flatter reports arrive around or after the drive has selected its goal and do not rewrite it.

Path 15's C6 Started and Completed both arrived. The last segment was the final point turn, and the body stopped at (13.435, 6.174, 0) mm with approximately the requested heading. The translation passed the native arrival test. Full rotation differed by about 14.8° against 10° (0x0055ADA8 → Pose3d::IsSameAs 0x00846EA4). The drive's arrival check failed after firmware declared traversal complete. The archived report predates the arrival diagnostic restored in DOCK-CHARGER-POSE-1. The morning replay starts from the captured published world pose and F0 states, verifies the recorded target heading, then checks the live MCP `why.snapshot.pathArrival` and its rotation-failure hint. The companion evening replay still checks the upstream F2/F4 camera and pose guard.

## Side-facing pickup

Yes. Pickup asks for Docking type 0 at 0x00551640. `Block::GeneratePreActionPoses` 0x004E5808 tests the face and rotation mask at 0x004E5970–0x004E597A. The LIGHTCUBE1 table at 0x00C45C40 contains masks 05/05/05/05/00/0F. The world-facing candidate must pass the native upright tolerance and obstacle sweep (0x004DF2F2–0x004DF318 and 0x004DF3C2–0x004DF5DA). When Top faces the robot it offers no near candidate, but Bottom can yield a far-side one, as PICKUP-PREDOCK-SIDE-1 established for LIGHTCUBE2's identical masks. The 15:56 LIGHTCUBE1 report was rotated onto its side and the final action returned DID_NOT_REACH_PREACTION_POSE, a later in-position result (0x00559880), distinct from NO_PREACTION_POSES. This row adds no pickup changes.

The existing engine results match both captures. The only changes add recorded replay coverage. Fidelity records and statuses retain their existing scope. No hardware run was performed for this handoff.

## Verification

Build `89b761da` passed all 8,814 tests in 58.17 minutes with the local recordings enabled. All 39 focused flip, wheelie and charger tests passed. The fidelity check passed for 765 records.

Set `COZMO_WHEELIE_CHARGER_BUNDLE` to the local morning bundle to run both new recorded cases. `COZMO_DOCK_POSE_BUNDLE` continues to select the evening capture. The first full run exposed a mambo fixture stall when offline Wwise chose larger sound packets. That test now uses the recorded F1 frame consumption and acknowledges the actual offline byte stream. The change is confined to the replay fixture.
