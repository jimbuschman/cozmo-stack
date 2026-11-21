# MAP-STATE-MCP-1

`get_state.pose` now reports the current world X/Y/Z, heading, origin, localization object, pose frame, and age of the last odometry estimate applied in that frame. `get_map` reads the current native map's actual leaf cells, projects bounds into robot-relative coordinates, and can include a 192 × 192 PNG. The PNG shows the selected spatial window even when the JSON lists only the nearest cells.

| Field | Source |
| --- | --- |
| World position, origin, and heading | Robot::GetRobotState calls GetPose and ToPoseStruct3d at `0x005180E2..0x0051810C`, then GetAngleAroundZaxis at `0x0051814A..0x00518160` (M1-053). The bridge uses the same current-pose and angle suppliers. |
| Localization object, frame, and age | The state publication reads the localized object at `0x00518230..0x0051823C`. The pose frame belongs to EngineRobot's existing localization owner (M11-053/097). UpdateCurrPoseFromHistory uses the newest sample at `0x005135F8..0x00513612`. The host timestamps only a successful pose join using the existing engine clock (M1-024) and returns null for age and sample timestamp after an origin or frame change until another successful join. |
| Map selection, content, and world bounds | GetCurrentMemoryMapHelper `0x0067EA5C..0x0067EA88` selects the current origin. Leaf traversal and content/depth come from QuadTreeNode::AddQuadsToSendDebugViz `0x00687B80..0x00687BEA`. The content table is at `0x00BFFAC8`; the unmodified node bounds use center and half-side at `0x00686AFC..0x00686B5C`. The host returns the memory leaf's original type, preserving distinctions the compact native game broadcast remaps at `0x00687B26..0x00687B58`. |
| Robot-relative bounds and PNG markers | The bridge transforms the same leaf square by the inverse planar robot pose and returns its enclosing bounds. Markers are for the robot's current pose and the located objects in that origin, using the existing BlockWorld poses (`ObservableObject::GetPose`, M11-004). These are presentation projections. |

The session 4 replay feeds the recorded F0 states and C0 cliff report for **15:52:49 UTC** through the live state history and cliff handler. It rebases capture timestamps and the origin/frame identifiers into the offline fixture. The original logged cliff `(-83.047, -322.345)` is inside a returned `Cliff` cell before the first docking request at capture time 602292 ms. The native path is `0x00535548..0x005356E6` → cliff pose from historical state at `0x00634980..0x006349AA` → BlockWorld markerless insertion `0x00622380` → type 8 at `0x00684984..0x006849A4`.

The tool captures a detached snapshot and has no map writer or retention. The fixture also checks the state pose, age, world/robot coordinates, PNG markers, response limits, and origin replacement. Hardware recordings stayed local.

## Verification

Build `f28c8896` passed all 8,837 tests in 58.98 minutes with the local recordings enabled. All 33 focused map, MCP and cliff replay cases passed. The fidelity check passed for 765 records.
