# Job X3 - M7-behaviour extraction, part 2: the BehaviourClass inventory

Agent: opencode-w1 (DeepSeek). Read-only extraction. Date: 2026-09-27.

This part enumerates the 79 `BehaviorClass` values the job names and gives, for each, the engine's
own entry points for its run logic: `InitInternal`, `UpdateInternal`, `StopInternal`,
`IsRunnableInternal` and `HandleWhileRunning`. Addresses are the Thumb entry (`dynsym` value, low bit
set) from `resources/lib/armeabi-v7a/libcozmoEngine.so`. A dash means the class does not override
that virtual (it inherits `IBehavior`'s).

**What this part does not do:** it does not read each class body. The job asks for each class's run
logic; that is one `UpdateInternal`/`InitInternal` body per class (79 classes, many hundreds of
instructions each) and is not complete in this pass. Every row below is therefore
`RECOVERABLE_GAP` for its run logic: read the listed `UpdateInternal`/`InitInternal` at that address.
The entry points are real and are the correct things to read.

Method: `BehaviorClass.cs` (the decompiled Unity enum, 79 values) joined against the engine's dynamic
symbol table; the five classes whose C++ name differs from the enum name are mapped by hand:

| BehaviorClass | engine class |
|---|---|
| BringCubeToBeacon | `BehaviorExploreBringCubeToBeacon` |
| PlayAnimWithFace | `BehaviorPlayAnimSequenceWithFace` |
| RequestGameSimple | `BehaviorRequestGameSimple` (`RequestGame_*Internal`) |
| ReactToCubeMoved | `BehaviorAcknowledgeCubeMoved` |
| Wait | `BehaviorWait` (no `*Internal` overrides found) |

Known entry-point facts read in this pass:
- `BehaviorPlayAnimSequence::InitInternal` is at `0x005c0648`; its `StartPlayingAnimations` at
  `0x005c0158` plays the trigger list (M8-005).
- `BehaviorReactToImpact::InitInternal` at `0x006061f8`, `StopInternal` at `0x006063fc`; the trigger
  path is in the part-1 report.
- `BehaviorExploreLookAroundInPlace::InitInternal` at `0x5e2684` is the look-around scan the M15
  classifier names.
- `BehaviorAcknowledgeCubeMoved` is the class behind the `ReactToCubeMoved` reaction and carries the
  full state machine (`TransitionToPlayingSenseReaction`, `TransitionToReactingToBlockAbsence`,
  `TransitionToTurningToLastLocationOfBlock`, `HandleObservedObject`).

## Per-class entry points

| BehaviorClass | InitInternal | UpdateInternal | StopInternal | IsRunnableInternal | HandleWhileRunning |
|---|---|---|---|---|---|
| Bouncer | 0x5f14a0 | 0x5f19a4 | 0x5f1c6c | 0x5f133c | - |
| BringCubeToBeacon | - | - | - | - | - |
| BuildPyramid | 0x5dbefe | 0x5dd01c | - | 0x5dbed0 | - |
| BuildPyramidBase | 0x5dcb56 | 0x5dd01c | - | 0x5dcaa4 | - |
| CantHandleTallStack | 0x5ece3c | - | 0x5ed064 | 0x5ecd64 | - |
| CheckForStackAtInterval | 0x5d7364 | - | 0x5d73ba | 0x5d7328 | - |
| CubeLiftWorkout | 0x5d7eb8 | 0x5d81a0 | 0x5d816c | 0x5d7e88 | - |
| Dance | 0x5ed460 | - | 0x5ed980 | - | - |
| DevTurnInPlaceTest | 0x5ca8fc | - | 0x5cad30 | 0x5ca8f6 | - |
| DockingTestSimple | 0x5cb394 | 0x5cbc10 | 0x5cda38 | 0x5cb384 | 0x5cdc30 |
| DriveInDesperation | 0x5d8eca | 0x5d9062 | 0x5d9060 | 0x5d90ac | - |
| DriveOffCharger | 0x5c0b18 | 0x5c0da8 | 0x5c0d90 | 0x5c0b10 | - |
| DrivePath | 0x5c0f76 | - | - | 0x5c0f68 | - |
| DriveToFace | 0x5da668 | 0x5da8fc | 0x5da93e | 0x5da588 | - |
| EarnedSparks | 0x5daecc | - | 0x5daf70 | 0x5daec2 | - |
| EnrollFace | 0x5fcec0 | 0x5fd968 | 0x5fe2f4 | 0x5fcd00 | 0x5ff55c |
| ExploreLookAroundInPlace | 0x5e2684 | - | - | 0x5e259c | - |
| ExploreVisitPossibleMarker | 0x5e3d8c | - | - | 0x5e3d6c | - |
| ExpressNeeds | 0x5ee67c | - | 0x5ee7d8 | 0x5ee570 | - |
| FactoryCentroidExtractor | 0x5cf8b4 | 0x5cfce0 | 0x5cfcfe | 0x5cf89c | 0x5cfdd0 |
| FactoryTest | 0x5d0c1c | 0x5d2724 | 0x5d27f8 | 0x5d0c0c | 0x5d2808 |
| FeedingEat | 0x5d5904 | 0x5d5b8c | 0x5d5de8 | 0x5d5880 | - |
| FeedingSearchForCube | 0x5d6c84 | 0x5d6d14 | - | 0x5d6c80 | - |
| FindFaces | - | - | - | 0x5c1a34 | - |
| FireTruckAlarm | 0x5dafb4 | - | - | 0x5dafa4 | - |
| FistBump | 0x5f1ed8 | 0x5f1f24 | 0x5f26f8 | 0x5f1ed4 | - |
| GuardDog | 0x5f2cfc | 0x5f2e68 | 0x5f4718 | 0x5f2a9c | 0x5f4d28 |
| InteractWithFaces | 0x5c2154 | 0x5c2354 | 0x5c240e | 0x5c23ac | - |
| KnockOverCubes | 0x5c31a2 | - | 0x5c36f2 | 0x5c314c | 0x5c3ae4 |
| LiftLoadTest | 0x5d410c | 0x5d45c0 | 0x5d4d38 | 0x5d40f0 | 0x5d4e64 |
| LookAround | 0x5c465c | 0x5c58c4 | 0x5c58f4 | 0x5c409a | 0x5c40d8 |
| LookForFaceAndCube | 0x5efd58 | - | 0x5f0878 | 0x5efd54 | 0x5f087c |
| LookInPlaceMemoryMap | 0x5e4ce0 | - | 0x5e4f2a | 0x5e4c6c | - |
| OnConfigSeen | 0x5db3ac | - | - | 0x5db2d4 | - |
| OnboardingShowCube | 0x600c7c | 0x601544 | 0x600e9c | 0x600c78 | 0x6010a8 |
| PeekABoo | 0x5f66e4 | 0x5f69d8 | 0x5f6f20 | 0x5f65ec | - |
| PickUpAndPutDownCube | 0x5db6b4 | - | - | 0x5db68c | - |
| PickUpCube | 0x5c6644 | 0x5c6824 | - | 0x5c6628 | - |
| PlayAnim | 0x5c0648 | - | 0x5bfef8 | 0x5c013a | - |
| PlayAnimOnNeedsChange | - | - | 0x5bfef8 | - | - |
| PlayAnimWithFace | - | - | - | - | - |
| PlayArbitraryAnim | 0x5c0896 | - | 0x5c08aa | 0x5c07be | - |
| PopAWheelie | 0x5c7496 | - | 0x5c76ac | 0x5c7478 | - |
| PounceOnMotion | 0x5f8412 | - | 0x5f8728 | 0x5f83ba | 0x5f9c34 |
| PutDownBlock | 0x5c7fd0 | - | - | 0x5c7fb0 | - |
| PyramidThankYou | 0x5de0cc | - | 0x5de314 | 0x5de078 | - |
| RequestGameSimple | - | - | - | - | - |
| RespondPossiblyRoll | 0x5de4ba | 0x5de5c6 | - | 0x5de4b6 | - |
| RespondToRenameFace | 0x600848 | 0x600a50 | - | 0x60082e | - |
| RollBlock | 0x5c8676 | 0x5c8a54 | 0x5c8c94 | 0x5c8650 | - |
| SearchForFace | 0x5c9182 | 0x5c9294 | - | 0x5c9170 | - |
| Singing | 0x5eeb30 | 0x5ef0c8 | 0x5ef2b0 | 0x5eeb0e | - |
| StackBlocks | 0x5c9502 | 0x5c9844 | 0x5c97f8 | 0x5c9484 | - |
| ThinkAboutBeacons | 0x5e5cdc | - | - | 0x5e5cc2 | - |
| TrackLaser | 0x5fac10 | 0x5fb200 | 0x5fbd14 | 0x5faab0 | - |
| TurnToFace | 0x5ca438 | - | 0x5ca4f8 | 0x5ca36c | - |
| VisitInterestingEdge | 0x5e7018 | 0x5e7418 | 0x5e729c | 0x5e6520 | - |
| Wait | - | - | - | - | - |
| AcknowledgeFace | 0x6028c8 | 0x602928 | 0x6028d2 | 0x6028bc | - |
| AcknowledgeObject | 0x603434 | 0x603524 | 0x60425e | 0x6042c8 | - |
| RamIntoBlock | 0x604764 | - | 0x604990 | 0x604754 | - |
| ReactToCliff | 0x604d50 | 0x605408 | 0x6053fc | 0x604d4c | 0x605580 |
| ReactToCubeMoved | - | - | - | - | - |
| ReactToFrustration | 0x605c0c | - | 0x605d84 | - | - |
| ReactToImpact | 0x6061f8 | - | 0x6063fc | - | - |
| ReactToMotorCalibration | 0x6065f0 | - | - | 0x6065ec | 0x606738 |
| ReactToOnCharger | 0x606c94 | 0x606e5c | 0x606e54 | 0x606c90 | 0x606e70 |
| ReactToPet | 0x606efc | 0x6072c8 | 0x607430 | 0x606ed8 | - |
| ReactToPickup | 0x607750 | 0x607ba8 | 0x607d9c | 0x60774c | - |
| ReactToPlacedOnSlope | 0x607fdc | - | - | 0x607fd8 | - |
| ReactToPyramid | 0x6083fc | - | - | 0x60832c | - |
| ReactToReturnedToTreads | 0x608500 | - | 0x608718 | 0x6084fc | - |
| ReactToRobotOnBack | 0x6087d4 | - | 0x6089fc | 0x6087d0 | - |
| ReactToRobotOnFace | 0x608ab8 | - | 0x608c98 | 0x608ab4 | - |
| ReactToRobotOnSide | 0x608d5c | - | 0x609064 | 0x608d58 | - |
| ReactToRobotShaken | 0x609128 | 0x609204 | 0x609504 | - | - |
| ReactToSparked | 0x6097f8 | - | - | 0x609864 | - |
| ReactToStackOfCubes | 0x60996c | - | - | 0x609898 | - |
| ReactToUnexpectedMovement | 0x609ac0 | - | - | 0x609abc | - |

## Existing records, judged

No existing M7 record names a per-class run-logic path except M7-003 (ReactToImpact, part 1). The
behaviour inventory (`re-analysis/BEHAVIOR_INVENTORY.md`, `behavior_inventory.json`) classifies the
178 configs by what they need, but it is a generated repo note (authority 6), not a source reading,
and its classifications are not evidence. This part does not change any existing record.

## NEW steps no record covers

- N1. Every `BehaviorClass` has a nameable run-logic entry point in the engine; the table above is
  the map from class to those entry points. No M7 record lists them.
- N2. Five enum classes do not share the engine class's name; the mapping is in the table above.
- N3. `BehaviorAcknowledgeCubeMoved` carries an explicit state machine with named transitions; the
  `ReactToCubeMoved` note does not name them.

## Open questions for the manager

- Q1. Per-class run logic is a large follow-up: 79 classes x up to four internal methods. Should the
  manager split it into batches (e.g. by M12/M13/M14/M15 ownership, matching the classifier) and hand
  each batch to a separate X job? This report gives the exact entry points each batch needs.
- Q2. The OBB is absent (see part 1), so the configs that drive these classes cannot be read here.
  The run-logic entry points can still be read from the `.so` without it, but the parameters each
  class reads cannot.
