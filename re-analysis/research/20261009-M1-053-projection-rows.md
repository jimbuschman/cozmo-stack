# M1-053: detailed engine-to-game RobotState projection rows

2026-10-09. Read-only extraction for manager check. These rows are **not yet checked build authority**. No production or manifest edit accompanies them.

## Current record checked before extraction

Current title: **Outgoing RobotState publication from the engine tick**. Current status: **IMPLEMENTATION_GAP**.

Current evidence:

> UpdateAllRobots enters from engine tick 0x004ED648; iterates robots, calls Robot::Update then HasReceivedRobotState, and only when true calls GetRobotState, constructs MessageEngineToGame::RobotState and invokes external slot +0x1C (0x0052F6C0..0x0052F7B0; getter 0x005180D8..0x00518252). External sink remains UNKNOWN.

> A second caller exists: BehaviorDockingTestSimple::UpdateInternal calls GetRobotState at 0x005CC31E, copies state to +0x270 and enters state 3 (0x005CC310..0x005CC32C); this non-live developer behavior belongs to M7-022. Remaining callers are not proven exhaustive. Source: 20261006-M1M2-missing-triage.md P2; reopened libcozmoEngine.so.

The operator's app-boundary decision replaces the external sink with a C# event. It does not establish that incoming `Cozmo.Protocol.RobotState` is the outgoing payload. The checked P2 row establishes the caller/gate but does not list the getter's field-level projection. A complete payload build needs the rows below checked and its supplier boundaries accepted.

## Primary source and target reopening

Primary binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`. Instruction text and reopened PLT relocation identities: `20261009-M1-053-projection-native.txt`.

The older `20261006-M1M2-triage-native.txt` has wrong GOT comments on unnamed PLTs: an ARM rotated immediate was not interpreted correctly. Reopened actual slots:

| Call instruction / PLT | Actual GOT | Relocation / target |
|---|---|---|
| 0051814E / 004A4048 | 010402A4 | PoseBase<Pose3d,Transform3d>::GetTransform, 0084499A; reads pose+4 |
| 00518156 / 004A4150 | 010402FC | Rotation3d::GetAngleAroundZaxis, 0084AA1C |
| 0051817A / 004A4168 | 01040304 | sinf (phone system libm, imported) |
| 005180F0 / 004A5BF0 | 01040BDC | Pose3d::ToPoseStruct3d, 00846D38 |
| 00518164 / 004A5A64 | 01040B58 | Radians copy constructor, 0084C84C |
| 00518248 / 004A5C68 | 01040C04 | VisionComponent::GetLastProcessedImageTimeStamp, 006527A2; reads component+E8 |

All four virtual integer reads in this getter are ObjectID value extraction: address point 0101E9B8 of ObjectID vtable 0101E9B0 contains Thumb pointer 004EF773; target 004EF772 reads `[this+4]`, then returns. In particular, robot+254 is MovementComponent, not an unidentified head-tracking component: Robot ctor 0050FD3C..0050FD4E allocates D8, calls MovementComponent ctor through PLT004A76CC and stores it at robot+254. Movement ctor 0063DA5C..0063DA94 initializes its ObjectID at +1C (value+20) to -1 and stores ObjectID vtable+8 there.

## Payload schema

Native `ExternalInterface::RobotState::Pack` 00711970..00711A62 confirms the ordered field layout below. Authority-2 generated Unity schema corroboration:
`C:/Users/JimBu/Downloads/com.anki.cozmo_3.4.0-1204_minAPI21(armeabi-v7a)(nodpi)_apkmirror.com.apk_Decompiler.com/unity/scripts/csharp/Anki.Cozmo.ExternalInterface/RobotState.cs`;
pose schema in sibling `Anki/PoseStruct3d.cs`.

The outgoing state is 109 packed bytes: PoseStruct3d (32 bytes), seven floats, two three-float IMU vectors, four signed int32 IDs, uint32 image timestamp, uint32 status, byte gameStatus. It has **no robot timestamp, pose frame ID, lift angle or cliff sensor array**. Do not forward the incoming packet or silently change the schema.

## Proposed detailed rows

| Row | Exact projected behavior | Native citation | Existing supplier / open boundary |
|---|---|---|---|
| P2a | After each Robot::Update, test HasReceivedRobotState; if false no getter or publication. If true construct game RobotState and invoke external interface slot+1C in that robot's iteration. | 0052F6C0..0052F7B0 (reopened text) | Existing FirstFullStateHandled is set after time-sync gate, before origin validation; checked CD23. |
| P2b | Call helper004EA398 returning robot+298 pose; helper verifies root ID/world-origin consistency and returns this pose even after logged verify failures. Call ToPoseStruct3d with PoseOriginList robot+294. | 005180E0..00518106; helper004EA398..004EA470; converter00846D38..00846DEE | Current engine pose/world-origin path is not simply StoredState.Pose. Existing higher-layer pose-tree fidelity remains open. |
| P2c | Pose struct is x,y,z float32, q0,q1,q2,q3 float32 cast from native float64 quaternion, originID uint32. Converter reads root transform; verifies root origin membership and parent-root invariants. | 00846D38..00846DEE, particularly00846DB6..00846DE4 | Full root transform supplier must be stated; do not replace quaternion with incoming planar yaw without supporting rows. |
| P2d | poseAngle_rad comes from GetTransform of the same robot pose, then Rotation3d::GetAngleAroundZaxis. Its choice computes float32 matrix terms from float64 quaternion products, compares two squared projected norms, selects atan2f pair, then Radians constructor/rescale. | 00518148..00518160; 0084499A..0084499C; 0084AA1C..0084AAB8 | Native algorithm is not established by merely reading incoming PoseAngleRad. System atan2f is imported; shipped arithmetic remains exact. Radians rescale target0084C87C needs checking if this projection is built. |
| P2e | posePitch_rad from robot+304; leftWheelSpeed_mmps from+30C; rightWheelSpeed_mmps from+310; headAngle_rad from+2FC. | 00518162..00518176 | Stored synced RobotState contains these pre-origin-gate fields (existing RS rows); incoming field names must be mapped individually. |
| P2f | liftHeight_mm = float32(float32(66.0f * sinf(robot+300)) +45.0f) +0.0f, in displayed native operation order. Constants42840000,42340000,00000000. | 0051817A..005181A2 | Existing RobotState.LiftHeightMmFromAngle is a candidate, not new extraction approval. Preserve the final +0.0 operation, including signed-zero behavior. |
| P2g | accel is three float32 at robot+360..368; gyro is three float32 at+36C..374; batteryVoltage comes from+33C. | 005181A6..005181B8; 0051823E..00518242 | These are stored synced sensor values, not a pose-gated state copy. |
| P2h | Start status from robot+350. If animation tag+248 !=0 OR0x40; if tag==FF instead OR0x840 into the original status. If carried-object value !=-1 additionally OR0x2. Preserve all original bits. | 005181BA..005181E8 | EngineRobot.AnimationTag exists. CarryingComponent.CarriedObjectId exists; using it requires wiring its real live owner. |
| P2i | If CarryingComponent+8 !=-1, return carried ID through ObjectID object at+4 and top ID through ObjectID at+10. Otherwise both projected IDs are -1, regardless of top-ID field. | 005181DA..0051820E; virtual004EF772 | Existing CarryingState.CarriedObjectId/CarriedOnTopId in Manipulation/Docking.cs are candidates. Do not project top ID independently when no carried ID. |
| P2j | gameStatus initially0; set1 iff robot+2C4 is nonzero AND off-treads byte+355 is zero. | 00518208..0051821E | BlockWorld.Robot2C4 and off-treads committed state exist, but full higher-layer localization supplier is explicitly incomplete. |
| P2k | headTrackingObjectID reads ObjectID at MovementComponent+1C, signed value+20. | 00518222..0051822E; 0050FD3C..0050FD4E; 0063DA5C..0063DA94;004EF772 | No live C# supplier for the native tracked-object field was found in bounded Motion/Actions search. **UNKNOWN** production supplier, not permission to hard-code -1. |
| P2l | localizedToObjectID reads robot's ObjectID at+2B4, signed value+2B8. | 00518230..0051823C;004EF772 | BlockWorld.LocalizedToObjectId maps this field, null=-1. Its larger localization path is separately open in M11. |
| P2m | lastImageTimeStamp is VisionComponent+E8, returned by GetLastProcessedImageTimeStamp. | 00518244..0051824C;006527A2..006527A6 | VisionSystem.LastRawFrameTimestamp is only a candidate. World.LastImageTimestamp is explicitly an unsourced max(received camera frame, processing frame) stand-in and **must not** be substituted for this getter. Exact write/commit point of +E8 remains unextracted here. |

## Manager check needed

The checked high-level P2 row cannot by itself justify a full live outgoing projection. The field schema and direct getter targets above are source-recovered; the live suppliers for engine root pose, head-tracking object ID, and last-processed image timestamp are not closed by the present checked inventory. The gameStatus and localization inputs already have higher-layer fidelity gaps. Manager needs to approve the boundary and decide whether supplier obligations transfer to their own records before M1-053 can claim its whole path.

No second-caller or exhaustive-caller audit is claimed. BehaviorDockingTestSimple stays with M7-022. No tests, implementation, manifest reclassification, or settlement performed.
