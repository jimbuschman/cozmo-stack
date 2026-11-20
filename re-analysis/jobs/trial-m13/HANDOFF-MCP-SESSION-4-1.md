# MCP-SESSION-4-1 handoff

| Session 4 request | Source and result |
| --- | --- |
| Identify the lift in the returned image | `get_camera_image` reports `liftInView` and an image-clipped `liftRegion` in the same pixel coordinates as the face and object rectangles. It looks up the selected image's timestamp in the current robot's raw pose history. AddLiftOccluder supplies interpolate=false at 0x006564EE–0x006564F8, which selects the nearest sample in a valid bracket. The camera and lift share the robot frame. GetLiftTransformWrtCamera composes their relative transform at 0x00516FD0–0x0051706C, using ComputeLiftPose (0x005151AC, pivot −41/0/45 mm and arm 66 mm), GetCameraPose (0x00510FFC, neck −13/0/49 mm, camera 17.52/0/−8 mm, rotation at 0x00C4A854). SetPhysicalRobot 0x00657F3C gives eight lift points with X −1 or 4, Y ±15 and Z −20 or −36.5 mm on the physical robot (−28.5 mm in sim). The source applies the transform before Camera::Project3dPoints at 0x0065653E/0x00656550 and uses the installed camera calibration (NV 0x80000001; 0x0065AB68–0x0065AE80). The MCP estimate bounds the projected pixels and reports null when the required calibration or historical pose is unavailable. |
| Quick moves | Shipped `DriveStraight.cs` defaults its trailing boolean to true and packs it after speed and distance. 0x00529EF6–0x00529F1A passes that byte to DriveStraightAction. MCP already forwards it, so the guide shows how to set it to false and the test checks both inputs reach the action. Shipped `TurnInPlace.cs` packs 17 bytes, four floats and an absolute-heading byte. Its factory 0x0052AE74–0x0052AEBA reads those same five fields. The only animation-related behavior found on that entry is the constructor's eye-shift default at +0xD8 (0x005459D4–0x00545B32, tested by Init near 0x0054620C). The shipped turn request exposes no animation toggle. |
| Interpret detection gaps and cliffs | The guide cites the session's roughly 15 cm charger sighting and the 27 × 20 mm marker, the cube's 25 × 25 mm marker and the 26 × 13 versus 31 × 20 px contour examples from DOCK-CHARGER-POSE-1. C0 bits use the enum at 0x007D292C / table 0x01034C50 and the mapping used by 0x00634980. Values 1/2/4/8 identify front left, front right, back left and back right. |

The first camera test exposed a coordinate-space difference. The live BlockWorld occluder helper passes a camera-relative point to the world-space overload of `CameraModel.Project`. The MCP estimate uses `ProjectCameraPoint` after the relative transform, matching the call order at 0x0065653E/0x00656550. The BlockWorld path is outside this MCP/host row.

## Session 4 push evidence

The archived notification replay contains six face-seen and two face-recognised pushes, across temporary IDs −1, −2, −3 and the named ID 1. It contains 23 reaction changes, with native new-trigger values corresponding to CliffDetected, ObjectPositionUpdated, MotorCalibration, ReturnedToTreads, FacePositionUpdated, PetInitialDetection, UnexpectedMovement, PlacedOnCharger and the none sentinel. get_state already reported CliffDetected and PlacedOnCharger by name. The requested per-approach deduplication and named push payload replay belong to EVENT-FILTER-2, as this row specifies for event filtering. This handoff carries the archived baseline into that row.

The four focused MCP checks cover exposure-time visibility, image bounds, a stale pose boundary, the shipped straight-drive default and explicit false, and the turn schema. Camera and MCP regression checks passed with the focused run. Robot acceptance remains with the operator.

## Verification

Build `6f2aff4d` passed all 8,824 tests in 50.33 minutes with the local recordings enabled. All 30 focused camera and MCP tests passed. The fidelity check passed for 765 records.
