# LIFT-OCCLUDER-PROJECT-1 handoff

BlockWorld transformed the eight lift points into camera space, then projected them through a method that transformed world points into camera space again. The lift path now calls `ProjectCameraPoint` after the one transform. Its depth remains the binary32 length of the lift transform's translation.

| Native path | Stack behavior and evidence |
| --- | --- |
| Lift region | BlockWorld clears the list and calls AddLiftOccluder with a nonempty marker list at 0x00624F98–0x00624FA4, or with a nonzero last image timestamp at 0x00625028–0x00625046. AddLiftOccluder reads the historical state at 0x006564EE–0x006564F8, uses the lift/camera transform at 0x0065652A → 0x00516FD0, applies it at 0x0065653E, projects at 0x00656550 and computes the translation length at 0x00656554–0x00656598. The vector Camera::AddOccluder at 0x0085E134 forwards to the camera's list at +0x18. |
| Occluder storage | Both vector and marker-quad inputs produce an axis-aligned Rectangle<float> (0x00878128 → 0x004E81B4, 0x0087804C → 0x0087855C). An equal-depth key keeps the larger area and retains the first on an equal-area tie (0x00878174–0x008781A6). The stack now stores those bounds rather than walking the unordered lift points as a polygon. |
| Marker visibility | The direct call sites for both list queries are in KnownMarker::IsVisibleFrom. Its four corner checks call IsOccluded (0x0087E80A, 0x0087E816, 0x0087E822, 0x0087E82E). The list tests only strictly nearer depths with half-open bounds (0x00878294–0x008782E4). For callers requesting a background, the four depths are averaged at 0x0087E844–0x0087E868 before IsAnythingBehind at 0x0087E870. That starts at the first strictly deeper entry at 0x008783E0–0x008783EC and requires positive rectangle intersection area at 0x00878418–0x00878444. The previous stack query considered nearer entries. |
| Owning callers | ObservableObject's marker loops at 0x00876678, 0x00876774 and 0x008767D4 reach the same marker classifier. SearchForBlockHelper (0x005BB768), AcknowledgeObject's ghost check (0x00603E54), and the CubeMoved reaction strategy (0x0060BD08) use visibility without requiring a background. CheckForUnobservedObjects (0x00622102) uses the overload that also reports NothingBehind. It counts eligible visible misses or a Dirty pose with NothingBehind (0x00622106–0x0062218E), while an occluded marker does not itself count as visible or NothingBehind. MarkObjectUnobserved at 0x00506FBC invokes MarkObjectUnknown on the second miss at 0x0050702E. Pose-unknown E2G 0x52 is emitted at 0x005073AA–0x005073B8. |

## Replay and verification

- In the November 19 morning pickup, processed image timestamp 350160 selects F0 sample 350155. Head −0.35052 rad and lift −0.02548 rad project the archived physical lift footprint to approximately (−25.5, 113.2)–(311.9, 266.6) px at 25.5 mm depth with the recorded NV calibration. The test uses the native body-v4 cleared-distortion branch and the analytic projection of all eight known points. The sample has firmware IsMoving/IsPickingOrPlacing set, so the original unobserved check would gate off during this image.
- The November 20 cube-loss replay starts at the recorded confirmed-pose boundary. It supplies the captured 0x44 pose, matching F0 head/lift and position/yaw samples, and the two processed-image timestamps where the capture logged NothingBehind. The first eligible miss retains the Dirty cube. The second removes it and emits the same raw 0x52 body plus the typed report for the recorded ID. The fixture rebases the pose origin. It drives the empty-marker branch at the recorded decision boundary.
- A held-pose regression covers a Dirty cube retained while the lift occludes its facing marker, then removed after two eligible frames once the lift is lowered. Unit checks also cover the pixel bounds, equal-depth handling, and positive-area background query.

The change covers M11-010, M11-037, M11-007 and M11-008 behavior in this path. Their existing fidelity classifications stay in place. The existing native pose-owner binding gap in the BlockWorld visibility path is still reported. These are offline checks using local captures.

## Verification

Build `f2dc71c7` passed all 8,829 tests in 48.57 minutes with the local recordings enabled. All 119 focused vision and MCP tests passed. The fidelity check passed for 765 records.
