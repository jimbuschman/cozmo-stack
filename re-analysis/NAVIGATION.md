# M13 — navigation, advanced cube manipulation and charger docking (2026-09-20)

What M13 added to `cozmo-stack/`, where each piece came from in `libcozmoEngine.so` 3.4.0-1204, the OBB's
configs and the decompiled Unity enums, and what is labelled rather than recovered. Code:
`src/Cozmo.Robot/Manipulation/` (`LatticePlanner.cs`, `FlipBlockAction.cs`, `ChargerActions.cs`,
`BlockConfigurations.cs`, `AIWhiteboard.cs`, `Workouts.cs`), `src/Cozmo.Robot/Vision/ChargerGeometry.cs`,
`src/Cozmo.Robot/Behavior/CubeGameBehaviors.cs`, `ChargerBehaviors.cs`, the Major frustration drive in
`OffTreadsBehaviors.cs`; tests `tests/Cozmo.Protocol.Tests/NavigationTests.cs` (26) on the shared fake robot
side `ManipRig.cs`. Read `MANIPULATION.md` first: everything here sits on the M12 docking exchange.

## 1. What was added, with provenance

| piece | engine source | label |
| --- | --- | --- |
| **Motion primitives** `MotionPrimitiveSet` | ASSET `config/engine/cozmo_mprim.json`: 10 mm cells, 16 lattice headings (0, atan½, 45°, atan2, 90°, …), 9 actions with cost factors (short straight 1.0001, long straight, slight/hard left/right, in-place ±1 at 2.0, backwards short straight 1.2), per-heading primitives with end cells and 0.5 mm intermediate poses; the keys `xythetaEnvironment::ParseMotionPrims` reads | ASSET |
| **Lattice environment** `LatticeEnvironment` | `Anki::Planning::xythetaEnvironment` exports: `AddObstacleWithExpansion` (hard polygon + soft ring), `IsInCollision` / `IsInSoftCollision` / `GetCollisionPenalty`, `GetSuccessors`, `ExpandCSpace`; `LatticePlannerImpl::ImportBlockworldObstaclesIfNeeded` builds each located object's `GetBoundingQuadXY`, `ConvexPolygon::RadialExpand`s it and adds it ("robot padding %f, obstacle padding %f") | NATIVE structure; robot radius 45 mm, soft ring 20 mm, soft penalty 100 INFERRED (the padding floats were not read) |
| **Planner** `LatticePlanner` | `xythetaPlannerImpl::ComputePath` / `ExpandState` / `InitializeHeuristic` / `CheckGoal`, `StartIsValid`, `GoalsAreValid`, `LatticePlannerImpl::StartPlanning` ("Current state is %f away from the plan … forcing replan", `PlanIsSafe`) | A* over (x, y, θ) cells with the Euclidean heuristic (INFERRED: the engine's heuristic table was not transcribed); multiple goals as `GoalsAreValid` implies; 60 000-expansion bound LOCAL |
| **Plan → path** `LatticePlanner.ToPath` | `MotionPrimitive::AddSegmentsToPath`, `PathSegment::DefineArc`, `GetCompletePath` | INFERRED reconstruction: straights merge into one line, in-place turns into one point turn, a turning primitive into a line then an arc whose sweep is the heading change and whose radius lands on the end cell exactly; a final point turn to the exact goal heading |
| **Integration** | `PathComponent::SelectPlanner` keeps more than one planner | `DriveToPoseAction` plans with the lattice when `ManipulationSystem.Planner` is set (obstacles imported from `BlockWorld`, the target and the carried cube excluded) and falls back to the straight-line planner when no plan is found — LOCAL_POLICY |
| **Flip** `FlipBlockAction` | 0x0055EC80..0x0055F1C0: constructor members 150.0 / 20.0 / 40.0 / 45.0 (+0x12C..+0x138); `Init`: Flipping pre-action pose within 5° (0.0872665), `DriveStraightAction(dist + 20, 150)` and `MoveLiftToHeightAction(40, 5.0)`; `CheckIfDone`: within 45 mm → `MoveLiftToHeightAction(preset 2, 5.0)` IN_PARALLEL (QueueActionPosition 5) and `ObjectPoseConfirmer::MarkObjectUnknown` | NATIVE values; member roles INFERRED from use |
| **Drive and flip** `DriveAndFlipBlockAction` | 0x0055E208, `IDriveToInteractWithObject<FlipBlockAction>`; `GetPossiblePoses` prefers the pose near `FaceWorld::GetLastObservedFace` | closest pose used (LOCAL_POLICY, no face tracking yet) |
| **Charger** `ChargerGeometry` | `Charger::Charger` 0x004E9B6C: 96 × 80 × 31 (0x42C00000 / 0x42A00000 / 0x41F80000 at +0xF0), marker code 2 at −90° about Z, (86, 0, 11) mm, 20 × 27 mm; `GetRobotDockedPose` 0x004EA1A0: π about Z, 30 mm | NATIVE; size order INFERRED; pre-dock pose INFERRED (the static `Pose2d` and −15.5 were not fully read) |
| **Rectangular markers** | `KnownMarker.HeightMm`; `CubeGeometry.LookupMarker` knows the charger; `BlockWorld` accepts passive objects unconnected (id 100 LOCAL) | NATIVE geometry |
| **Align** `AlignWithObjectAction` | constructor offsets LIFT_FINGER 0, LIFT_PLATE 6, BODY −15, CUSTOM = distance − 27; `AlignmentType` UNITY; firmware `ALIGN` | NATIVE |
| **Mount** `MountChargerAction` | 0x0054E018: object type 0xD; `ConfigureAlignWithChargerAction`: align 120 mm (0x42F00000) CUSTOM at 30 mm/s (0x41F00000), head 0 ± 2°; `ConfigureTurnAndMountAction`: `TurnInPlaceAction` (atan2 to the docked pose, max 1.74533, accel 5.23599), lift to 45 mm (0x42340000) if above, `DriveStraightAction(−120, 30)`; "Turning and mounting the charger failed … Driving forward to position for a retry": `DriveStraightAction(120, 100)` | NATIVE; success on IS_ON_CHARGER and 2 retries INFERRED |
| **Drive off** `DriveOffChargerContactsAction` | 0x00558228: a `DriveStraightAction(10, 20)` given its distance by the behaviour; "StillOnCharger" | NATIVE |
| **Block configurations** `BlockConfigurationManager` | `BlockConfigurations::StackOfCubes::BuildTallestStackForObject` (0x0061907C, `FindObjectOnTopOrUnderneathHelper`, "LoopBoundOverflow" at 8), `PyramidBase::BlocksFormPyramidBase` (0x00617500: same height within 10, 3600 mm² = 60 mm), `ObjectIsOnTopOfBase` (0x00617CC8: 15 mm, ±0.5, −5), `Pyramid::BuildAllPyramidsForBlock`, `BlockConfigurationManager::GetCacheByType` / `IsObjectPartOfConfigurationType` / `DidAnyObjectsMovePastThreshold` / `CheckForPyramidBaseBelowObject` | NATIVE rules and constants; the on-top planar tolerance 15 mm INFERRED |
| **Whiteboard** `AIWhiteboard`, `AIBeacon` | exports `AddBeacon`, `GetActiveBeacon`, `ClearAllBeacons`, `FindCubesInBeacon`, `FindUsableCubesOutOfBeacons`, `AreAllCubesInBeacons`, `SetFailedToUse`, `DidFailToUse`, `GetObjectFailureTable`, `OnRobotDelocalized`; `AIBeacon::IsLocWithinBeacon`, `FailedToFindLocation` | NATIVE interface; one active beacon INFERRED; possible-object tracking DEFERRED (explorer, M15) |
| **Workouts** `WorkoutComponent`, `WorkoutConfig` | ASSET `behaviorSystem/workout_config.json` (4 entries), `WorkoutConfig::GetNumStrongLifts` over `Confident` score graphs, `WorkoutComponent::CompleteCurrentWorkout` | ASSET; the current-workout choice LOCAL_POLICY (medium by default) |

## 2. The behaviours (25 shipped configs)

| class (configs) | engine | what was read |
| --- | --- | --- |
| `KnockOverCubesBehavior` (KnockOverCubes 3, SparksKnockOverCubes 2) | 0x005C2EA0..0x005C3C00 | `TransitionToReachingForBlock`: `TurnTowardsObjectAction`, `DriveStraightAction` to 85 mm (0x42AA0000) at 60 mm/s (0x42700000), the reach trigger; `TransitionToKnockingOverStack` → `DriveAndFlipBlockAction`; `TransitionToBlindlyFlipping` → `FlipBlockAction` with the check off + `WaitAction(0.5)`; `TransitionToPlayingReaction`; `HandleObjectUpAxisChanged` |
| `PopAWheelieBehavior` (PopAWheelie, SparksPopAWheelie) | 0x005C74C0.. | `TransitionToReactingToBlock` 0x18A, `TransitionToPerformingAction` → `DriveToPopAWheelieAction`, `SetupRetryAction` "Retry %d of %d" with 0x18D / 0x18E, `EnableStopOnCliff` on stop; retry limit INFERRED (3) |
| `RamIntoBlockBehavior` (RamIntoBlock) | reactions/ramIntoBlock.json | carrying → turn + `PlaceObjectOnGround`; else `TurnTowardsObject` + lift preset, `DriveStraight(distance, 100)` ∥ 0x20B, `DriveStraight(−100)` |
| `CubeLiftWorkoutBehavior` (CubeLiftWorkout, SparksCubeLiftWorkout) | 0x005D7E50..0x005D8A80 | `TransitionToPickingUpCube` → strong lifts → weak pose → weak lifts → putting down → check put down → manual put down → post-lift → `EndIteration`; idle 0x23F |
| `BuildPyramidBaseBehavior` (BuildPyramidBase; BuildPyramid with the top) | 0x005DCA41.., 0x005DBC89.. | `UpdatePyramidTargets` (`GetBestObjectForIntention` ×3), states DrivingToBaseBlock / PlacingBaseBlock (`UpdateBlockPlacementOffsets`: dimension + 12) / ObservingBase (`DriveStraight(−40, 40)`, 0x14) / DrivingToTopBlock / PlacingTopBlock (`GetBaseInteriorMidpoint`) / ReactingToPyramid (objective 3, 0x17) |
| `RespondPossiblyRollBehavior` (PyramidRespondPossiblyRoll) | 0x005DE4B6..0x005DEB90 | `DetermineNextResponse` on `GetRotatedParentAxis<Z>`, `TurnAndRespondPositively` / `Negatively` with the trigger from a table indexed by pyramid progress, `DelegateToRollHelper`; result 5 → respond again |
| `OnConfigSeenBehavior` (RespondToPyramidBase) | 0x005DB0xx..0x005DB420 | `configTriggers` / `animTriggers` parsing, `GetCacheByType`, 4.99999 s recency (0x409FFFEB) |
| `CantHandleTallStackBehavior` (CantHandleTallStack) | 0x005ECD64..0x005ED370 | config keys, `GetTallestStack`, `GetStackHeight`, `TransitionToLookingUpAndDown` (−0.436332, +0.785398, 2° tolerance, the three waits), `TransitionToDisapointment`, `AlwaysHandle` ObjectMoved |
| `CheckForStackAtIntervalBehavior` (SparksCheckForStackAtInterval) | 0x005D7328..0x005D7BD0 | `delayBetweenChecks`, `TransitionToSetup` / `FacingBlock` / `CheckingAboveBlock` (`SetGhostObjectPose` one dimension up, `UseCustomObject`) / `ReturnToSearch` (`PanAndTiltAction`) |
| `ReactToConfigurationBehavior` (ReactToPyramid, ReactToStackOfCubes) | 0x0060832C, 0x00609898 | `IsRunnableInternal` reads the cache and the timer; `InitInternal` stores now + 100 (0x42C80000) and nothing else |
| `ThinkAboutBeaconsBehavior` (Hiking_ 175, Sparks 75) | exports + config | `SelectNewBeacon` → `AddBeacon`, `newAreaAnimTrigger` |
| `BringCubeToBeaconBehavior` (Hiking_ 45 s, Sparks 5 s) | `BehaviorExploreBringCubeToBeacon` exports + config | `GetCandidate`, `TransitionToPickUpObject`, `TransitionToObjectPickedUp`, `FindFreeCubeToStackOn` / `TryToStackOn`, `FindFreePoseInBeacon` / `TryToPlaceAt`, `recentFailureCooldown_sec`; the free-pose search and the emotion event names are LOCAL / DEFERRED |
| `DriveOffChargerBehavior` (DriveOffCharger 60, Hiking_DriveOffCharger 45) | 0x005C09xx..0x005C0C10 | 96 + `extraDistanceToDrive_mm`, `WaitForOnTreads`, emotion event "DriveOffCharger" (charger_events.json) |
| `ReactToOnChargerBehavior` (ReactToOnCharger, VC_GoToSleep) | 0x00606C94.. | idle 0x23F, 0x189, `timeTilSleepAnimation_s` / `timeTilDisconnection_s`, `GoingToSleep` / `StartIdleTimeout` broadcasts (engine-to-app; logged here) |
| `MountChargerBehavior` (DockingTestSimple) | dev config | `MountChargerAction` on a located charger |
| `ReactToFrustrationBehavior.Major` (ReactToFrustrationMajor) | 0x00605904.., config | the random `DriveToPoseAction` in [150, 400] mm at ±[80°, 180°] |

## 3. What is not native, and is labelled

* The planner's heuristic, expansion bound, padding values, soft penalty and the straight-line fallback.
* The arc reconstruction of turning primitives (end poses exact; the engine's segment tables not read).
* The charger's pre-dock pose and size order; mount success by the IS_ON_CHARGER flag; 2 retries.
* Flip member roles; the wheelie retry limit; the ram's lift preset; the workout selection; one active beacon;
  the beacon free-pose search; BringCubeToBeacon's emotion events; the stack on-top tolerance;
  BuildPyramid's target selection (LOCAL in place of `ObjectInteractionInfoCache`); the CantHandleTallStack
  trigger id (by name).
* **Correction found while testing** (`SOURCE_FIDELITY_AUDIT.md` §14): the M12 dock base verified every target
  visually before docking. The engine's `IDockAction::SetupTurnAndVerifyAction` uses
  `VisuallyVerifyNoObjectAtPoseAction` for the place actions; from the 40 mm place-relative pose a cube's side
  marker is not fully in the camera's view, so the M12 transcription would have refused every stack and every
  pyramid placement. `PlaceRelObjectAction` now verifies the placement pose is clear and docks on the marker
  facing the robot.

## 4. Offline evidence

`NavigationTests` (26): the primitives parse as the engine reads them; an empty world plans six long straights
(ties broken by the 1.0001 factor); a turning primitive becomes a line and an arc that lands on its end cell;
the planner routes around a cube and refuses a goal inside one; `DriveToPoseAction` with the planner drives
the fake robot through arcs to within tolerance; the target is not an obstacle; the charger's rectangular
marker, lookup and docked pose; a rendered charger is localised unconnected with id 100 and family Charger;
`MountChargerAction` aligns (firmware `ALIGN`), turns, backs at −30 mm/s and succeeds when the fake robot's
footprint reports the contacts; DriveOffCharger drives 156 mm at 20 mm/s and fires the event; ReactToOnCharger
announces sleep at 300 s and disconnect at 330 s and ends when the charger flag clears; the flip drives
distance + 20 at 150 with the lift at 40 then carry height and forgets the cube; stacks, bases and pyramids
from poses; the configuration manager's first-seen stamps, OnConfigSeen's 5 s window and the 100 s reaction
cooldown; beacons and failure memory; the workouts' lift counts from Confident; the workout behaviour's
5 strong + 1 weak lifts; KnockOverCubes reaching, flipping and celebrating on a rendered stack of three;
PopAWheelie's dock and stop-on-cliff, and its retries; RamIntoBlock's two drives; CantHandleTallStack's head
angles; CheckForStackAtInterval's ghost look and interval; RespondPossiblyRoll's two responses (the roll docks
`ROLL_LOW`); BuildPyramidBase's pick-up and beside-placement; the Major frustration drive's range; the 25-config
set. Regression: the full suite, 606 tests, passes (578 after M12).

## 5. Hardware pending

Items S–X in `HARDWARE_TEST_PLAN.md`: `manip <ip> --flip`, `--knockover`, `--wheelie`, `--mount`, `--driveoff`,
and `--driveto --obb <obb>` (the lattice planner's arcs on the real robot). `manip --plan x y deg --obb <obb>
--obstacle x y` prints a lattice plan offline.

## 6. Next

M14: faces (`FaceWorld`, `TrackedFace`, `TurnTowardsFaceAction`, `PlayAnimWithFace`, the face-dependent
behaviours) around the OKAO boundary; then M15: the freeplay / explorer layer (`activities_config.json`,
choosers, `AIWhiteboard`'s possible objects, `ExploreLookAroundInPlace`, `DriveInDesperation`, memory-map
behaviours), which the planner and whiteboard now support.
