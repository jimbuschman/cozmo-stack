# ROBOT-STATE-RECIPIENTS-1

The private hardware acceptance directory contained 40 files matching `*log*.txt`. The ProtoHandle callable diagnostic appeared in 39. None contained the other five listed messages. The gyro wording was a source comment.

| Site | Production path and engine evidence |
| --- | --- |
| Gyro drift | After admitting the state into history and updating its pose, Sensors calls the robot's constructed detector. At 0x00512FB4 and 0x00512FBE the engine loads the same +0x27C owner for the two calls. Corrected the obsolete comment. |
| Pick and place | DockingSystem attaches OnNativePickAndPlace to PickAndPlaceRoute. At 0x00533790 the engine reads Robot+0x280 and stores the result success byte there. The robot constructor installs docking and carrying at 0x0050FE34 and 0x0050FE48. |
| SetPose | At 0x0051508C..0x0051509E the carried ID controls the forward offset. SetPose reads the constructed CarryingOwner at the pose write. |
| Ground placement | The public call takes the current runtime's ManipulationSystem, whose action list owns NativePlaceGroundRunner. GroundPlacementCallerTests exercise the retained runner. A caller without an active owner gets an availability exception. |
| UI subscriptions | The weak lock is dropped at 0x00664718. The remove body destroys the callable at 0x0066350E, unlinks neighbors, then releases the node at 0x0066352E. The manager-adopted OV107 callable census established memory-only destruction, including the dispatch and void cases (research/overnight/20261016-ov105-107-108-answer.md). Removed the extra stack diagnostic from real unlink, signal destruction and shutdown. Tests cover both signal directions, expiry and silent shutdown. |
| Disconnect | At 0x0052F2DC..0x0052F2E0 the engine calls its needs owner. EngineNeedsOwner binds Needs.OnRobotDisconnected on initialization and replacement. The other Required call passes a concrete DAS clear action. |

ConnectedRobotConstructsStateRecipients checks the owners and routes on a robot created through the regular offline startup and binding path.
