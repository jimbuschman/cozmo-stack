# Job X3 - M7-behaviour extraction, part 5: per-class run logic (first pass)

Agent: opencode-w1 (DeepSeek). Read-only extraction. Date: 2026-09-28.

This part reads the run logic of a first set of behaviour classes and records exactly what remains.
It is not the whole 79 classes; part 2 has the complete entry-point map, and the "not yet read" list
at the end names the address to read for each remaining class. Nothing here is guessed: a class not
read is listed as RECOVERABLE_GAP with its address, not described.

## Row table

| # | class | what the original does | citation | record | classification |
|---:|---|---|---|---|---|
| 1 | ReactToImpact (completes M7-003) | `InitInternal` takes `SmartDisableReactionsWithLock`, allocates a 5.0 s (`0x40a00000`) wait action and `StartActing(TransitionToPlayingAnim)`. `TransitionToPlayingAnim` only acts when `+0x11e` is set: it allocates a `TriggerAnimationAction(robot, 0x1a0, 1, true, 0, 60.0f, ...)` (trigger `0x1a0`, 60 s). `AlwaysHandle` switches on the engine-to-game tag at `msg+0x10`: tag `0x3b` (`FallingStopped`) sets `+0x11d = 1`, reads the FallingStopped field at `+4`, and sets `+0x11e = 1` only when it is `> 1000.0f` (constant at `0x606470`); tag `0x1e` sets `+0x11c = 1` when `Robot::IsHeadCalibrated()` and `Robot::IsLiftCalibrated()` are both 1; tag `0x3a` clears both `+0x11e` and `+0x11c`. | `0x006061f8`; `0x00606238 movt r3,#0x40a0`; `0x0060635a ldrb.w r0,[r4,#0x11e]`; `0x0060636e movt r0,#0x4270`; `0x0060637e mov.w r2,#0x1a0`; `0x00606384 blx TriggerAnimationAction::TriggerAnimationAction`; `0x00606408`; `0x00606418 beq #0x60642c`; `0x0060641a cmp r0,#0x3a`; `0x00606422 strb.w r0,[r4,#0x11e]`; `0x0060642c movs r0,#1`; `0x00606434 blx Get_FallingStopped`; `0x0060643e vldr s0,[pc,#0x30]` -> `0x606470` = 1000.0; `0x0060644a it gt`; `0x0060644e strb.w r0,[r4,#0x11e]`; `0x00606454 blx Robot::IsHeadCalibrated`; `0x00606460 blx Robot::IsLiftCalibrated`; `0x0060646a strbeq.w r0,[r4,#0x11c]` | M7-003 | EXACT_SOURCE |
| 2 | DriveOffCharger | `InitInternal` takes the reaction lock, sets `+0x120 = 0`, and when the AI process byte (`robot+0x264 -> +0x30 -> +0x14`) is 3 calls `DrivingAnimationHandler::PushDrivingAnimations(..., name)` and sets `+0x120 = 1`. If `robot+0x355` (on-treads/off-treads flag) is set it runs the `"WaitForOnTreads"` sub-behaviour; otherwise `TransitionToDrivingForward`. `UpdateInternal`: if `robot+0x34a` is set and `robot+0x355` is set, `StopActing` and re-run `"WaitForOnTreads"`; if `robot+0x34a` is clear and no current action, it stamps the AI state `robot+0x264->+0x18->+0x44 = now` and returns 2 (done); if `robot+0x355` is clear and no action, `TransitionToDrivingForward`. Returns 1 while acting. | `0x005c0b18`; `0x005c0b30 strb.w r0,[r4,#0x120]`; `0x005c0b3c cmp r0,#3`; `0x005c0b4a blx PushDrivingAnimations`; `0x005c0b54 ldrb.w r0,[r5,#0x355]`; `0x005c0b62 add r1,pc ; r1=0xbf2b55 "WaitForOnTreads"`; `0x005c0b8c blx TransitionToDrivingForward`; `0x005c0da8`; `0x005c0db0 ldrb.w r0,[r5,#0x34a]`; `0x005c0dc4 blx StopActing`; `0x005c0df4 ldr.w r0,[r4,#0x84]`; `0x005c0e08 str r0,[r1,#0x44]`; `0x005c0e0a movs r0,#2` | NEW | EXACT_SOURCE |
| 3 | ExpressNeeds | `InitInternal` builds a `CompoundActionSequential`: first a `TurnTowardsLastFacePoseAction` (constructed from a `TurnTowardsFaceAction` with `pi` radians, i.e. turn around), then for every need action id in the vector at `+0x128..+0x12c` a `TriggerAnimationAction(trigger, 1, true, 0, 60.0f, ...)`, and `StartActing` it. `GetCooldownSec` and `ResumeInternal` exist; `StopInternal` at `0x5ee7d8`. | `0x005ee67c`; `0x005ee6a4 mov.w r0,#0x1d8`; `0x005ee6ae movw r1,#0xfdb` / `0x005ee6b4 movt r1,#0x4049` (`pi`); `0x005ee6d0 blx TurnTowardsFaceAction::TurnTowardsFaceAction`; `0x005ee6f2 ldr.w r7,[r6,#0x128]`; `0x005ee6f8 ldr.w r6,[r6,#0x12c]`; `0x005ee72c blx TriggerAnimationAction::TriggerAnimationAction`; `0x005ee75e blx StartActing` | NEW | EXACT_SOURCE |
| 4 | Singing | `InitInternal` posts the audio switch state (`audioSwitchGroup`/`audioSwitch` from the config, `RobotAudioClient::PostRobotSwitchState`), takes the reaction lock, finds connected cubes through `BlockWorld::FindConnectedActiveMatchingObjects`, attaches a `ShakeListener` to each cube through `CubeAccelComponent::AddListener`, builds a `CompoundActionSequential` of three `TriggerAnimationAction`s, and `StartActing` it. `UpdateInternal` at `0x5ef0c8`, `StopInternal` at `0x5ef2b0` (body not read). | `0x005eeb30`; `0x005eeb4e blx PostRobotSwitchState`; `0x005eeb5e blx SmartDisableReactionsWithLock`; `0x005eec5c blx FindConnectedActiveMatchingObjects`; `0x005eed08 blx ShakeListener::ShakeListener`; `0x005eed56 blx CubeAccelComponent::AddListener`; `0x005eedb2 blx CompoundActionSequential::CompoundActionSequential`; `0x005eede0/0x005eee1a/0x005eee54 blx TriggerAnimationAction`; `0x005eee8c blx StartActing` | NEW | EXACT_SOURCE for `InitInternal`; `UpdateInternal`/`StopInternal` RECOVERABLE_GAP |
| 5 | AcknowledgeCubeMoved (ReactToCubeMoved) | `InitInternal` takes the reaction lock, clears `+0x12c`, and branches on `+0x128`: `1` -> `TransitionToTurningToLastLocationOfBlock`, else `TransitionToPlayingSenseReaction`. `UpdateInternal` (state 1, `+0x12c` set) stops acting, plays a `TriggerLiftSafeAnimationAction(trigger 3, ...)`, sets state `+0x128 = 3`, and calls the `"WaitForOnTreads"` helper; then calls the base `IBehavior::UpdateInternal`. Other transitions: `TransitionToReactingToBlockAbsence` (`0x6026a8`), `HandleObservedObject` (`0x6027d2`), `HandleWhileRunning` (`0x6027a8`). | `0x00602234`; `0x00602242 blx SmartDisableReactionsWithLock`; `0x00602246 ldr.w r0,[r5,#0x128]`; `0x0060224c strb.w r1,[r5,#0x12c]`; `0x00602258 blx TransitionToTurningToLastLocationOfBlock`; `0x00602262 blx TransitionToPlayingSenseReaction`; `0x006024f8`; `0x00602522 blx StopActing`; `0x00602546 blx TriggerLiftSafeAnimationAction`; `0x00602582 str.w r0,[r5,#0x128]`; `0x006025a0 blx IBehavior::UpdateInternal` | NEW | EXACT_SOURCE for the state entry; the remaining transitions RECOVERABLE_GAP |
| 6 | ReactToOnCharger | `InitInternal` takes the reaction lock, registers a going-to-sleep handler (`MessageEngineToGame::MessageEngineToGame(GoingToSleep)`), pushes idle animation trigger `0x23f`, plays a `TriggerLiftSafeAnimationAction(trigger 0x189, ...)`, sets the robot disconnect reason to `4` (`SetRobotDisconnectReason`), and registers the charger callbacks. `UpdateInternal` at `0x606e5c`, `HandleWhileRunning` at `0x606e70`. | `0x00606c94`; `0x00606cb0 blx SmartDisableReactionsWithLock`; `0x00606cc6 movw r2,#0x23f`; `0x00606cca blx SmartPushIdleAnimation`; `0x00606cec movw r2,#0x189`; `0x00606cf2 blx TriggerLiftSafeAnimationAction`; `0x00606d1a movs r1,#4`; `0x00606d20 blx SetRobotDisconnectReason` | NEW | EXACT_SOURCE for `InitInternal`; `UpdateInternal` RECOVERABLE_GAP |
| 7 | PlayAnimSequence | Already recorded: `StartPlayingAnimations`/`StartSequenceLoop` play every trigger in the config's order `num_loops` times. | `0x005c0158`, `0x005c0294` (M8-005) | M8-005 | EXACT_SOURCE |
| 8 | Wait | `BehaviorWait` has no `*Internal` overrides found in the dynamic symbol table; the `wait.json` config carries `"executableBehaviorType": "Wait"`, so it is driven by the executable-behaviour mechanism rather than a class override. The class body was not located. | `.../behaviors/wait.json`; no `BehaviorWait::` symbol | NEW | RECOVERABLE_GAP - locate `BehaviorWait` (or the executable-behaviour factory) |

## Not yet read (entry points from part 2)

These classes have a real entry point but no body was read in this pass. Each is RECOVERABLE_GAP for
its run logic; part 2's table gives the same addresses. Read `InitInternal`/`UpdateInternal`/
`StopInternal` at the address shown.

`Bouncer 0x5f14a0`, `BringCubeToBeacon` (engine `BehaviorExploreBringCubeToBeacon`),
`BuildPyramid 0x5dbefe`, `BuildPyramidBase 0x5dcb56`, `CantHandleTallStack 0x5ece3c`,
`CheckForStackAtInterval 0x5d7364`, `CubeLiftWorkout 0x5d7eb8`, `Dance 0x5ed460`,
`DevTurnInPlaceTest 0x5ca8fc`, `DockingTestSimple 0x5cb394`, `DriveInDesperation 0x5d8eca`,
`DrivePath 0x5c0f76`, `DriveToFace 0x5da668`, `EarnedSparks 0x5daecc`, `EnrollFace 0x5fcec0`,
`ExploreLookAroundInPlace 0x5e2684`, `ExploreVisitPossibleMarker 0x5e3d8c`, `FactoryCentroidExtractor
0x5cf8b4`, `FactoryTest 0x5d0c1c`, `FeedingEat 0x5d5904`, `FeedingSearchForCube 0x5d6c84`,
`FindFaces 0x5c1a34`, `FireTruckAlarm 0x5dafb4`, `FistBump 0x5f1ed8`, `GuardDog 0x5f2cfc`,
`InteractWithFaces 0x5c2154`, `KnockOverCubes 0x5c31a2`, `LiftLoadTest 0x5d410c`, `LookAround
0x5c465c`, `LookForFaceAndCube 0x5efd58`, `LookInPlaceMemoryMap 0x5e4ce0`, `OnConfigSeen 0x5db3ac`,
`OnboardingShowCube 0x600c7c`, `PeekABoo 0x5f66e4`, `PickUpAndPutDownCube 0x5db6b4`, `PickUpCube
0x5c6644`, `PlayAnim 0x5c0648`, `PlayAnimOnNeedsChange`, `PlayAnimWithFace`, `PlayArbitraryAnim
0x5c0896`, `PopAWheelie 0x5c7496`, `PounceOnMotion 0x5f8412`, `PutDownBlock 0x5c7fd0`,
`PyramidThankYou 0x5de0cc`, `RequestGameSimple`, `RespondPossiblyRoll 0x5de4ba`, `RespondToRenameFace
0x600848`, `RollBlock 0x5c8676`, `SearchForFace 0x5c9182`, `StackBlocks 0x5c9502`, `ThinkAboutBeacons
0x5e5cdc`, `TrackLaser 0x5fac10`, `TurnToFace 0x5ca438`, `VisitInterestingEdge 0x5e7018`,
`AcknowledgeFace 0x6028c8`, `AcknowledgeObject 0x603434`, `RamIntoBlock 0x604764`, `ReactToCliff
0x604d50`, `ReactToFrustration 0x605c0c`, `ReactToMotorCalibration 0x6065f0`, `ReactToPet 0x606efc`,
`ReactToPickup 0x607750`, `ReactToPlacedOnSlope 0x607fdc`, `ReactToPyramid 0x6083fc`,
`ReactToReturnedToTreads 0x608500`, `ReactToRobotOnBack 0x6087d4`, `ReactToRobotOnFace 0x608ab8`,
`ReactToRobotOnSide 0x608d5c`, `ReactToRobotShaken 0x609128`, `ReactToSparked 0x6097f8`,
`ReactToStackOfCubes 0x60996c`, `ReactToUnexpectedMovement 0x609ac0`.

Also unread within read classes: `BehaviorSinging::UpdateInternal 0x5ef0c8` and `StopInternal
0x5ef2b0`; `BehaviorReactToOnCharger::UpdateInternal 0x606e5c` and `HandleWhileRunning 0x606e70`;
`BehaviorAcknowledgeCubeMoved::TransitionToTurningToLastLocationOfBlock 0x602270`,
`TransitionToPlayingSenseReaction 0x6023f8`, `TransitionToReactingToBlockAbsence 0x6026a8`,
`HandleObservedObject 0x6027d2`, `HandleWhileRunning 0x6027a8`.

## Existing records, judged

- **M7-003** (EXACT_SOURCE, ReactToImpact): **now complete.** The "impact intensity over 1000"
  (constant `1000.0` at `0x606470`) and the head/lift recalibration completion (`0x606454`) are read;
  the earlier report's untaken branches are settled. The 5 s is the `InitInternal` wait-action timeout
  (`0x40a00000`), and the animation is trigger `0x1a0` with a 60 s argument (`0x4270`). Correct the
  record's evidence to name `InitInternal 0x006061f8` and the two branch addresses.
- No other M7 record claims a per-class run path, so nothing else changes.

## NEW steps no record covers

- N1. Every detailed row above (DriveOffCharger, ExpressNeeds, Singing, AcknowledgeCubeMoved,
  ReactToOnCharger, ReactToImpact's branches).
- N2. The behaviour classes' own `SmartDisableReactionsWithLock` tables (`0xc73d36`, `0xc6f590`,
  `0xc74182`, `0xc72bb2`, `0xc672f0`) differ per class; none is recorded.
- N3. The `"WaitForOnTreads"` sub-behaviour name shared by DriveOffCharger and AcknowledgeCubeMoved.
- N4. `ReactToOnCharger` sets the robot disconnect reason to 4 and pushes idle trigger `0x23f`.

## Open questions for the manager

- Q1. The remaining ~65 classes are the bulk of M7. Split them into batches by M12/M13/M14/M15
  ownership, as part 2 asked, or keep them RECOVERABLE_GAP for the integrator?
- Q2. `BehaviorWait` has no `*Internal` symbol; is it driven by `executableBehaviorType`? A focused
  pass should find the factory.
- Q3. The per-class reaction-lock tables are data addresses; record them per class, or one record?
