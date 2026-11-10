# M11-044-LOCALIZATION-MARKER-CAMERA-1

## Claims and evidence

- The existing explicit attached BlockWorld.SetLocalizedTo entry uses the current outer camera pose. The original reads VisionComponent+0x30 at 0x005123EE–0x005123FA. The constructor initializes accepted head to 0xBEDF66F3 at 0x0051007C–0x00510086 and installs the camera pose at 0x0051039A–0x005103B0. The neck is parented to MAIN at 0x0050FF5A–0x0050FF6A and the default camera to the neck at 0x0050FFB6–0x0050FFBA. GetCameraPose at 0x00510FFC–0x00511036 copies the camera and rotates for the accepted head angle. The calibrated state writer at 0x00513358–0x00513404 retains or refreshes this angle and pose. The managed call reconstructs the same checked geometry from the current accepted head and original live MAIN under owner and pose gates.
- The loop starts at the object's marker list and reads each original marker pose and unsigned timestamp at 0x005123A4–0x005123C2. The known-marker constructor zeros the timestamp at 0x0087E276–0x0087E27C. The accepted observation and merge paths write it at 0x00876F86 and 0x00876CB4–0x00876CB6. Existing native cube and charger creation and observation paths supply these owners. The attached transform at 0x005123EE–0x00512400 requires actual common root identity.
- The per-call timestamp threshold starts at zero and changes only on a winning squared distance. The original computes separate f32 products/adds at 0x0051240A–0x00512428, and writes on a negative prior or strictly lower result at 0x0051242A–0x00512446. Later transform failure at 0x0051254E preserves earlier writes and skips the success flags. Different roots take the native error path. Missing native owner/node/time is visible as MISSING.
- Null follows 0x005124EE–0x005124F6, unassigned ID fails at 0x0051239C–0x005123A0, and empty attached lists take the success tail at 0x005123A4–0x005123AC without requesting a camera. Success stores ID, localized flag and clear moved flag at 0x00512458–0x00512468. Offline flattened fixtures and explicit supplied loop adapters retain their previous contract.

## Files and tests

- NativeDelocalization.cs owns a scoped current-camera copy with a live neck parent. NativeCubeObservation.cs reuses it for the same existing report path. CozmoRobot.cs and VisionSystem.cs bind current owner and generation. BlockWorld.cs joins the exact marker loop for the attached explicit call and keeps automatic partial localization separate.
- LocalizationMarkerCameraTests drives the public attached call with a real fake-robot owner, canonical cube, accepted current head and separate older history state. It checks distinct current geometry, a deliberately foreign root with the same numeric ID after a winning marker, and null/empty calls with camera access forbidden. The pre-calibration constructor head edge is verified from source.
- The manager executed the shipped ARM loop with controlled marker transforms and linked list. It verified the exact camera operand, skip after a winning timestamp, persisted prior minimum and earlier partial write retained on later failure. The independent verifier rechecked the scoped production binding and empty branch.

- The first affected run exposed five flattened offline fixture regressions. The corrected affected filter passed 287/287. After the empty-list correction, the final focused filter passed 18/18 and the manager independently ran 157/157 covering localization, charger, cube report and docking readers.

## MISSING and records

- Automatic LocalizeToObject must call HistRobotState::SetPose when available and successfully update current MAIN before the same-root recipient at 0x005159B6–0x005159D8. Cross-root additionally requires Rejigger, object/map/face origin changes and Flatten at 0x0051590C–0x005159B2. LocalizeToMat has its own history join at 0x0051612C–0x00516142. The automatic stack path remains its explicit partial adapter. AI relocalization at 0x0051246C and visualization remain outside this binding.
- M11-044's explicit attached marker-camera recipient is checked. The remaining automatic path prevents whole-record settlement. No status, approval or hardware flag changed.
