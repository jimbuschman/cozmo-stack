# Scope: what "done" contains (draft for the operator, 2026-11-15)

Goal (operator, 2026-11-03): Cozmo is the body for an outside LLM and keeps his own personality. The LLM decides what
he does; the stack does it the way the engine does.

State labels:
- **robot**: seen working on the operator's robot;
- **sim**: built from the binary and passing simulated tests, never (or not recently) run on the robot;
- **partial**: some code, known holes;
- **none**: no code.

The 78 behaviour classes cover all 178 shipped behaviour IDs. States come from the code and the robot bundles in
`re-analysis/acceptance/hardware/`, not from record counts. Drafted in one pass; a state marked sim may turn out
partial when it is run.

## A. Commands and events for the LLM (required)

Robot script: `m15-cube-hw --commands` (built 2026-11-16) runs every command below once and records the events.

| Item | State |
| --- | --- |
| Head, lift, drive straight, turn in place | sim (motion itself: robot) |
| Turn to a face, turn to a cube, track a face | sim |
| Play an authored animation | sim (animations: robot) |
| Say a sentence in his voice | robot (2026-11-09; maybe slightly slow) |
| Pick up a cube, put it down | robot (2026-11-09; again 2026-11-15, run 20261115-162141) |
| Events: face seen / recognised / lost, cube seen / moved / tapped, object pose lost | sim |
| Teach, rename and erase a face | sim |
| Go to a place (QueueAiGotoPoseAsync) | sim (built 2026-11-15; go-to round in `m15-cube-hw --skills`) |
| Return to and mount the charger (QueueAiMountChargerAsync) | sim (built 2026-11-15; SetPoseOnCharger built 2026-11-16; charger round in `m15-cube-hw --skills`) |
| Roll a cube (QueueAiRollCubeAsync) | robot (2026-11-15, run 20261115-162141: drove to the cube and rolled it) |
| Stack a cube (QueueAiStackOnCubeAsync) | sim (built 2026-11-15; robot run needs a second live cube) |
| Charger seen report (operator 2026-11-15) | sim (E2G 0x44 for the charger built 2026-11-16; shown in the --skills charger round) |
| Freeplay on/off, and returning to Freeplay after a command | sim (SetEnableFreeplayActivity built 2026-11-15; `m15-cube-hw --freeplay-control`) |
| The connection for an LLM (bridge) | none |

## B. Personality (required)

Robot script: `m15-cube-hw --personality` (built 2026-11-15) walks through every group below with the engine's own reports.

| Group | Classes | State |
| --- | --- | --- |
| Freeplay loop, activities, needs, mood | (system) | robot (2026-11-03, 4 min; 15 min check Z2 not run) |
| Physical reactions | ReactToCliff, ReactToPickup, ReactToRobotOnBack/OnFace/OnSide, ReactToRobotShaken, ReactToReturnedToTreads, ReactToImpact, ReactToPlacedOnSlope, ReactToUnexpectedMovement, ReactToMotorCalibration, ReactToOnCharger, ReactToFrustration | sim (cliff: robot; checks G, C, H, J exist) |
| Needs | ExpressNeeds, PlayAnimOnNeedsChange, DriveInDesperation, EarnedSparks | sim (DriveInDesperation's cube search built 2026-11-16; untargeted search never succeeds, as in the engine) |
| Idle life | PlayAnim, PlayAnimWithFace, PlayArbitraryAnim, Wait, ExploreLookAroundInPlace, FindFaces, LookForFaceAndCube, VisitInterestingEdge, PounceOnMotion, ThinkAboutBeacons | robot for Pounce, VisitInterestingEdge, FindFaces; sim for the rest |
| Faces | AcknowledgeFace, InteractWithFaces, SearchForFace, DriveToFace, EnrollFace, RespondToRenameFace, ReactToPet | sim |
| Cubes | AcknowledgeObject, ReactToCubeMoved, PickUpCube, PutDownBlock, RollBlock, StackBlocks, DriveOffCharger | robot for pickup, put-down, drive-off; sim for the rest |

## C. Playful extras (wanted, after A and B)

Commands QueueAiFlipCubeAsync and QueueAiPopAWheelieAsync: sim (built 2026-11-15; rounds in `m15-cube-hw --skills`).
Behaviours PopAWheelie, KnockOverCubes, FistBump, Singing, Dance, CubeLiftWorkout, FireTruckAlarm, BringCubeToBeacon,
RamIntoBlock, Bouncer: sim. PeekABoo, TrackLaser, GuardDog: none or partial.

## D. Deferred (not worked unless the operator moves them)

BuildPyramid, BuildPyramidBase, PyramidThankYou, ReactToPyramid, RespondPossiblyRoll, OnConfigSeen,
CheckForStackAtInterval, CantHandleTallStack, ReactToStackOfCubes, ReactToSparked and the Sparks flows,
RequestGameSimple (10 IDs), FeedingEat, FeedingSearchForCube, OnboardingShowCube, LookInPlaceMemoryMap,
ExploreVisitPossibleMarker, PickUpAndPutDownCube.

## E. Excluded

FactoryTest, FactoryCentroidExtractor, LiftLoadTest, DevTurnInPlaceTest, DockingTestSimple (as a behaviour; the
charger mount action stays in A).

## Count

Required (A + B): 12 command items and about 40 behaviour classes. On the robot today: 3 command items and about 8
classes. No code at all: 5 command items and 1 class.
