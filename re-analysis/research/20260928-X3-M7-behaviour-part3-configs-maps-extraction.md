# Job X3 - M7-behaviour extraction, part 3: the shipped behaviour configs, maps and activity tree

Agent: opencode-w1 (DeepSeek). Read-only extraction. Date: 2026-09-28.

This part resumes X3 after the unpacked OBB was added to the clone. It reads the authority-3 artifacts
the 2026-09-27 part-1 report could not: the 178 behaviour configs, the reaction-trigger map, the
animation-trigger maps and the activity tree. It does not read per-class run logic (part 2 mapped the
entry points; part 5 does the run logic).

## Sources

- `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/` (behaviour configs, activity
  configs, the reaction map, the activity tree).
- `re-analysis/obb/assets/cozmo_resources/assets/animationGroupMaps/AnimationTriggerMap.json` and
  `.../cubeAnimationGroupMaps/CubeAnimationTriggerMap.json`.
- `resources/lib/armeabi-v7a/libcozmoEngine.so` for the loader addresses and the paths it reads.

The engine's own path strings (read from the `.so`) match the OBB tree exactly:
`config/engine/behaviorSystem/behaviors/` (`0x5208bc`), `config/engine/behaviorSystem/activities/`
(`0x520b58`), `config/engine/behaviorSystem/reactionTrigger_behavior_map.json` (`0x520ce8`),
`config/engine/behaviorSystem/activities_config.json` (`0xbe7e35`),
`config/engine/behaviorSystem/behavior_system_config.json` (`0xbe7ecc`) and
`config/engine/mood_config.json` (`0x523130`).

## Row table

| # | step | what the original does | citation | record | classification |
|---:|---|---|---|---|---|
| 1 | Behaviour configs loaded | `RobotDataLoader::LoadBehaviors` reads every file under `config/engine/behaviorSystem/behaviors/` (the engine's path string is at `0x5208bc`), each a JSON with `behaviorClass` and `behaviorID`. | `0x005206bc RobotDataLoader::LoadBehaviors`; `0x005208bc "config/engine/behaviorSystem/behaviors/"` | M7-002 (related) | EXACT_SOURCE |
| 2 | Config census | 178 config files, all JSON. Every one carries `behaviorClass` (76 distinct) and `behaviorID` (178 distinct). 39 are `Singing`, 15 `PlayAnim`, 10 `RequestGameSimple`, 7 `PlayAnimWithFace`, 6 `ExpressNeeds`, 4 each `FindFaces`/`PounceOnMotion`/`PutDownBlock`/`RollBlock`. The config tree also has `devBehaviors/` (5), `feeding/` (3) + `feeding/feedingAnims/` (8), `freeplay/` (17, with `buildPyramid/`, `hiking/`, `needs/`, `putDownDispatch/`, `requestGame/`, `singing/` 39, `sparkable/` 20, `userInteractive/` 3), `meetCozmo/` (4), `onboarding/` (1), `reactions/` (24), `voiceCommands/` (14 + `howAreYouDoing/` 4). | the tree under `re-analysis/obb/.../behaviorSystem/behaviors/`; the full table below | M7-002 | EXACT_SOURCE |
| 3 | Config keys | The top-level keys are `behaviorClass` and `behaviorID` on all 178; then `displayNameKey` (139), `requiredUnlockId` (76), `audioSwitchGroup`/`audioSwitch` (39 each, all Singing), `animTriggers` (33), `needsActionID` (22), `params` (15), `executableBehaviorType` (12), `zero_block_config`/`one_block_config` and the pickup/place motion profiles (10 and 5, RequestGameSimple), `need`/`needBracket`/`cooldown` (ExpressNeeds), and per-class keys such as `maxNoGroundMotionBeforeBored_*` (PounceOnMotion) and `minTimesPeekBeforeQuit` (PeekABoo). 126 distinct keys in all. | e.g. `.../behaviors/wait.json` (`executableBehaviorType`), `.../freeplay/requestGame/requestCozmoPerforms.json` (block configs), `.../freeplay/pounceOnMotion_socialize.json` | NEW | EXACT_SOURCE |
| 4 | class vs ID | `behaviorClass` selects the engine class; `behaviorID` is the config identity. They differ where several configs share a class: e.g. `Singing_AbaDaba` -> class `Singing`; `FPPeekABoo` -> class `PeekABoo`; `ReactToFrustrationMinor`/`ReactToFrustrationMajor` -> class `ReactToFrustration`; the 39 `Singing_*` and 10 `Request*` IDs. | table below | NEW | EXACT_SOURCE |
| 5 | Reaction map loaded | `RobotDataLoader::LoadReactionTriggerMap` reads `config/engine/behaviorSystem/reactionTrigger_behavior_map.json` (`0x520ce8`) through `DataPlatform::readAsJson`, and on failure logs `"Failed to read '%s'"` via `sErrorF`. `BehaviorManager::InitReactionTriggerMap` iterates the map's array and builds the trigger -> behaviour dispatch. | `0x00520bc8`; `0x00520c2e blx DataPlatform::readAsJson`; `0x00520c54 blx sErrorF`; `0x00520c3c add r2,pc ; r2=0xbe7d67 "Failed to read '%s'"`; `0x005a16e4`; `0x005a16f6` | M7-002 | EXACT_SOURCE |
| 6 | Reaction map contents | 21 entries. `FacePositionUpdated`->`AcknowledgeFace`; `FistBump`->`FistBump` (six objective params); `Hiccup`->`Hiccup` (`hiccupParams`: min/max occurrence 300/3300 s, 5..10 hiccups, 4500..8000 ms spacing, 600 s after cure, unlock `DroneModeGame`); `ObjectPositionUpdated`->`AcknowledgeObject`; `CliffDetected`->`ReactToCliff` (`shouldResumeLast: true`); `CubeMoved`->`ReactToCubeMoved`; `Frustration`->`ReactToFrustrationMinor` (`maxConfidence -0.6`, `cooldownTime_s 60.0`) and `Frustration`->`ReactToFrustrationMajor` (`maxConfidence -0.9`, no cooldown); `MotorCalibration`->`ReactToMotorCalibration` (`shouldResumeLast: true`); `NoPreDockPoses`->`RamIntoBlock`; `PlacedOnCharger`->`ReactToOnCharger` (`strategyType PlacedOnCharger`); `PetInitialDetection`->`ReactToPet`; `RobotFalling`->`ReactToImpact` (`shouldResumeLast: false`); `RobotPickedUp`->`ReactToPickup` (`false`); `RobotPlacedOnSlope`->`ReactToPlacedOnSlope` (`strategyType RobotPlacedOnSlope`); `ReturnedToTreads`->`ReactToReturnedToTreads` (`false`); `RobotOnBack`/`RobotOnFace`/`RobotOnSide` -> the matching `ReactTo*` (`false`); `RobotShaken`->`ReactToRobotShaken` (`strategyType RobotShaken`); `Sparked`->`ReactToSparked`; `UnexpectedMovement`->`ReactToUnexpectedMovement` (`shouldResumeLast: true`). | `config/engine/behaviorSystem/reactionTrigger_behavior_map.json:1-209` | M7-002 / M7-011 | EXACT_SOURCE |
| 7 | AnimationTriggerMap | `assets/animationGroupMaps/AnimationTriggerMap.json` has `Pairs`: 573 entries, each `{CladEvent, AnimName}`; all 573 `CladEvent` names are distinct. This is the shipped trigger -> animation-group map. | `.../assets/animationGroupMaps/AnimationTriggerMap.json:1` | M7-002 | EXACT_SOURCE |
| 8 | CubeAnimationTriggerMap | `assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json` has `Pairs`: 40 entries of the same shape, for cube animations. | `.../assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json` | M7-002 | EXACT_SOURCE |
| 9 | Activity tree (freeplay) | `behavior_system_config.json` names the top activity `StrictPriorityFreeplay` of type `StrictPriority`, with sub-activities in priority order: `Socialize` 11, `Singing` 12, `PlayWithHumans` 13, `BuildPyramid` 14, `PlayAlone` 15, `Hiking` 16, `NothingToDo` 17. | `config/engine/behaviorSystem/behavior_system_config.json:1-33` | NEW | EXACT_SOURCE |
| 10 | Activity definitions | `activities_config.json` defines `Selection` (`BehaviorsOnly`, chooser type `Selection`), `MeetCozmo` (`BehaviorsOnly`, chooser type `Scoring`, six behaviours with flat scores), `Feeding` (`Feeding`, universal chooser type `StrictPriority`, four behaviours), and `Freeplay` (`Freeplay`, sub-activities: 14 Spark activities at priority 0, then needs, then `PutDownDispatch` 10 .. `NothingToDo` 17; `desiredActivityNames` maps face/cube presence to `Socialize`/`PlayAlone`/`Hiking`). | `config/engine/behaviorSystem/activities_config.json:1-170` | NEW | EXACT_SOURCE |
| 11 | Per-activity configs | `behaviorSystem/activities/` holds one file per named activity; the clone ships `putDownDispatch.json` (`BehaviorsOnly`, chooser type `Scoring` with `DriveOffCharger` flat 1000 and `PutDownDispatch_LookForFaceAndCube` flat 1.0 with a `(0.2,0)->(0.2,1)` repetition penalty; `activityStrategy.requiredRecentOnTreadsEventSecs 5.0`). | `config/engine/behaviorSystem/activities/putDownDispatch.json:1-37` | NEW | EXACT_SOURCE |

### The 178 configs

| path under `behaviors/` | behaviorID | behaviorClass | animTriggers |
|---|---|---:|---:|
| `playArbitraryAnim.json` | PlayArbitraryAnim | PlayArbitraryAnim | 0 |
| `wait.json` | Wait | Wait | 0 |
| `devBehaviors/devTurnInPlaceTest.json` | DevTurnInPlaceTest | DevTurnInPlaceTest | 0 |
| `devBehaviors/dockingTestSimple.json` | DockingTestSimple | DockingTestSimple | 0 |
| `devBehaviors/factoryCentroidExtractor.json` | FactoryCentroidExtractor | FactoryCentroidExtractor | 0 |
| `devBehaviors/factoryTest.json` | FactoryTest | FactoryTest | 0 |
| `devBehaviors/liftLoadTest.json` | LiftLoadTest | LiftLoadTest | 0 |
| `feeding/feedingEat.json` | FeedingEat | FeedingEat | 0 |
| `feeding/feedingFindFacesSevere.json` | FeedingFindFacesSevere | FindFaces | 0 |
| `feeding/feedingSearchForCube.json` | FeedingSearchForCube | FeedingSearchForCube | 0 |
| `feeding/feedingAnims/feedingPlayRequestAtFace.json` | FeedingPlayRequestAtFace | PlayAnimWithFace | 1 |
| `feeding/feedingAnims/feedingPlayRequestAtFace_Severe.json` | FeedingPlayRequestAtFace_Severe | PlayAnimWithFace | 1 |
| `feeding/feedingAnims/feedingReactCubeShake.json` | FeedingReactCubeShake | PlayAnim | 1 |
| `feeding/feedingAnims/feedingReactCubeShake_Severe.json` | FeedingReactCubeShake_Severe | PlayAnim | 1 |
| `feeding/feedingAnims/feedingReactFullCube.json` | FeedingReactFullCube | PlayAnim | 1 |
| `feeding/feedingAnims/feedingReactFullCube_Severe.json` | FeedingReactFullCube_Severe | PlayAnim | 1 |
| `feeding/feedingAnims/feedingReactSeeCharged.json` | FeedingReactSeeCharged | PlayAnim | 1 |
| `feeding/feedingAnims/feedingReactSeeCharged_Severe.json` | FeedingReactSeeCharged_Severe | PlayAnim | 1 |
| `freeplay/cantHandleTallStack.json` | CantHandleTallStack | CantHandleTallStack | 0 |
| `freeplay/cubeLiftWorkout.json` | CubeLiftWorkout | CubeLiftWorkout | 0 |
| `freeplay/driveOffCharger.json` | DriveOffCharger | DriveOffCharger | 0 |
| `freeplay/EarnedSparks.json` | EarnedSparks | EarnedSparks | 0 |
| `freeplay/findFaces_socialize.json` | FindFaces_socialize | FindFaces | 0 |
| `freeplay/FPpeekAboo.json` | FPPeekABoo | PeekABoo | 0 |
| `freeplay/interactWithFaces.json` | InteractWithFaces | InteractWithFaces | 0 |
| `freeplay/knockOverCubes.json` | KnockOverCubes | KnockOverCubes | 0 |
| `freeplay/NothingToDo_BoredAnim.json` | NothingToDo_BoredAnim | PlayAnim | 3 |
| `freeplay/NothingToDo_Idle.json` | NothingToDo_Idle | PlayAnim | 1 |
| `freeplay/popAWheelie.json` | PopAWheelie | PopAWheelie | 0 |
| `freeplay/pounceOnMotion_socialize.json` | PounceOnMotion_Socialize | PounceOnMotion | 0 |
| `freeplay/putDownBlock.json` | PutDownBlock | PutDownBlock | 0 |
| `freeplay/putDownBlockNothingToDo.json` | PutDownBlockNothingToDo | PutDownBlock | 0 |
| `freeplay/rollBlockOnSide.json` | RollBlockOnSide | RollBlock | 0 |
| `freeplay/rollBlockOnSideLowScore.json` | RollBlockOnSideLowScore | RollBlock | 0 |
| `freeplay/stackBlocks.json` | StackBlocks | StackBlocks | 0 |
| `freeplay/buildPyramid/buildPyramid.json` | BuildPyramid | BuildPyramid | 0 |
| `freeplay/buildPyramid/buildPyramidBase.json` | BuildPyramidBase | BuildPyramidBase | 0 |
| `freeplay/buildPyramid/pyramidPutDownBlock.json` | PyramidPutDownBlock | PutDownBlock | 0 |
| `freeplay/buildPyramid/pyramidRespondPossiblyRoll.json` | PyramidRespondPossiblyRoll | RespondPossiblyRoll | 0 |
| `freeplay/buildPyramid/pyramidThankYou.json` | PyramidThankYou | PyramidThankYou | 0 |
| `freeplay/buildPyramid/respondToPyramidBase.json` | RespondToPyramidBase | OnConfigSeen | 1 |
| `freeplay/hiking/Hiking_bringCubeToBeacon.json` | Hiking_BringCubeToBeacon | BringCubeToBeacon | 0 |
| `freeplay/hiking/Hiking_driveOffCharger.json` | Hiking_DriveOffCharger | DriveOffCharger | 0 |
| `freeplay/hiking/Hiking_firstLookIntro.json` | Hiking_FirstLookIntro | PlayAnim | 1 |
| `freeplay/hiking/Hiking_firstLookWakeUp.json` | Hiking_FirstLookWakeUp | PlayAnim | 1 |
| `freeplay/hiking/Hiking_lookInPlace360.json` | Hiking_LookInPlace360 | ExploreLookAroundInPlace | 0 |
| `freeplay/hiking/Hiking_lookInPlaceForUnknown.json` | Hiking_LookInPlaceForUnknown | LookInPlaceMemoryMap | 0 |
| `freeplay/hiking/Hiking_pounceOnMotion.json` | Hiking_PounceOnMotion | PounceOnMotion | 0 |
| `freeplay/hiking/Hiking_rollCube.json` | Hiking_RollCube | RollBlock | 0 |
| `freeplay/hiking/Hiking_thinkAboutBeacons.json` | Hiking_ThinkAboutBeacons | ThinkAboutBeacons | 0 |
| `freeplay/hiking/Hiking_visitInterestingEdge.json` | Hiking_VisitInterestingEdge | VisitInterestingEdge | 0 |
| `freeplay/needs/needs_MildLowEnergyRequest.json` | Needs_MildLowEnergyRequest | ExpressNeeds | 1 |
| `freeplay/needs/needs_MildLowPlayRequest.json` | Needs_MildLowPlayRequest | ExpressNeeds | 1 |
| `freeplay/needs/needs_MildLowRepairRequest.json` | Needs_MildLowRepairRequest | ExpressNeeds | 1 |
| `freeplay/needs/needs_SevereLowEnergyForcedGetOut.json` | Needs_SevereLowEnergyForcedGetOut | ExpressNeeds | 1 |
| `freeplay/needs/needs_SevereLowEnergyGetIn.json` | Needs_SevereLowEnergyGetIn | PlayAnimOnNeedsChange | 1 |
| `freeplay/needs/needs_SevereLowEnergyState.json` | Needs_SevereLowEnergyState | DriveInDesperation | 0 |
| `freeplay/needs/needs_SevereLowPlayBored.json` | Needs_SevereLowPlayBored | ExpressNeeds | 3 |
| `freeplay/needs/needs_SevereLowPlayGetIn.json` | Needs_SevereLowPlayGetIn | PlayAnimOnNeedsChange | 1 |
| `freeplay/needs/needs_SevereLowPlayRequest.json` | Needs_SevereLowPlayRequest | ExpressNeeds | 1 |
| `freeplay/needs/needs_SevereLowRepairGetIn.json` | Needs_SevereLowRepairGetIn | PlayAnimOnNeedsChange | 1 |
| `freeplay/needs/needs_SevereLowRepairState.json` | Needs_SevereLowRepairState | DriveInDesperation | 0 |
| `freeplay/needs/needs_Wait.json` | Needs_Wait | Wait | 0 |
| `freeplay/putDownDispatch/PutDownDispatch_LookForFaceAndCube.json` | PutDownDispatch_LookForFaceAndCube | LookForFaceAndCube | 0 |
| `freeplay/requestGame/requestCozmoPerforms.json` | RequestCozmoPerforms | RequestGameSimple | 0 |
| `freeplay/requestGame/requestDroneMode.json` | RequestDroneMode | RequestGameSimple | 0 |
| `freeplay/requestGame/requestKeepAway.json` | RequestKeepAway | RequestGameSimple | 0 |
| `freeplay/requestGame/requestMemoryMatch.json` | RequestMemoryMatch | RequestGameSimple | 0 |
| `freeplay/requestGame/requestSpeedTap.json` | RequestSpeedTap | RequestGameSimple | 0 |
| `freeplay/singing/Singing_AbaDaba.json` | Singing_AbaDaba | Singing | 0 |
| `freeplay/singing/Singing_BeautifulDreamer.json` | Singing_BeautifulDreamer | Singing | 0 |
| `freeplay/singing/Singing_Beethovens5th.json` | Singing_Beethovens5th | Singing | 0 |
| `freeplay/singing/Singing_Bingo.json` | Singing_Bingo | Singing | 0 |
| `freeplay/singing/Singing_BuffaloGals.json` | Singing_BuffaloGals | Singing | 0 |
| `freeplay/singing/Singing_Camptown.json` | Singing_Camptown | Singing | 0 |
| `freeplay/singing/Singing_CanCan1.json` | Singing_CanCan1 | Singing | 0 |
| `freeplay/singing/Singing_CanCan2.json` | Singing_CanCan2 | Singing | 0 |
| `freeplay/singing/Singing_DannyBoy.json` | Singing_DannyBoy | Singing | 0 |
| `freeplay/singing/Singing_EntryOfTheGladiators.json` | Singing_EntryOfTheGladiators | Singing | 0 |
| `freeplay/singing/Singing_FarmerInTheDell.json` | Singing_FarmerInTheDell | Singing | 0 |
| `freeplay/singing/Singing_FrereJacques.json` | Singing_FrereJacques | Singing | 0 |
| `freeplay/singing/Singing_HelloMyBaby.json` | Singing_HelloMyBaby | Singing | 0 |
| `freeplay/singing/Singing_ItsyBitsySpider.json` | Singing_ItsyBitsySpider | Singing | 0 |
| `freeplay/singing/Singing_LaPaloma.json` | Singing_LaPaloma | Singing | 0 |
| `freeplay/singing/Singing_LondonBridge.json` | Singing_LondonBridge | Singing | 0 |
| `freeplay/singing/Singing_MaryHadALittleLamb.json` | Singing_MaryHadALittleLamb | Singing | 0 |
| `freeplay/singing/Singing_MountainKing.json` | Singing_MountainKing | Singing | 0 |
| `freeplay/singing/Singing_MuffinMan.json` | Singing_MuffinMan | Singing | 0 |
| `freeplay/singing/Singing_MulberryBush.json` | Singing_MulberryBush | Singing | 0 |
| `freeplay/singing/Singing_MussIDenn.json` | Singing_MussIDenn | Singing | 0 |
| `freeplay/singing/Singing_OdeToJoy.json` | Singing_OdeToJoy | Singing | 0 |
| `freeplay/singing/Singing_PachebelCanon.json` | Singing_PachebelCanon | Singing | 0 |
| `freeplay/singing/Singing_PopGoesTheWeasel.json` | Singing_PopGoesTheWeasel | Singing | 0 |
| `freeplay/singing/Singing_RowYourBoat.json` | Singing_RowYourBoat | Singing | 0 |
| `freeplay/singing/Singing_Sakura.json` | Singing_Sakura | Singing | 0 |
| `freeplay/singing/Singing_SilveryMoon.json` | Singing_SilveryMoon | Singing | 0 |
| `freeplay/singing/Singing_TakeMeOutToTheBallgame.json` | Singing_TakeMeOutToTheBallgame | Singing | 0 |
| `freeplay/singing/Singing_TaRaRaBoom.json` | Singing_TaRaRaBoom | Singing | 0 |
| `freeplay/singing/Singing_TisketTasket.json` | Singing_TisketTasket | Singing | 0 |
| `freeplay/singing/Singing_Toccata.json` | Singing_Toccata | Singing | 0 |
| `freeplay/singing/Singing_TurkeyInTheStraw.json` | Singing_TurkeyInTheStraw | Singing | 0 |
| `freeplay/singing/Singing_TwinkleTwinkle.json` | Singing_TwinkleTwinkle | Singing | 0 |
| `freeplay/singing/Singing_VivaldiSpring.json` | Singing_VivaldiSpring | Singing | 0 |
| `freeplay/singing/Singing_WaterMusic.json` | Singing_WaterMusic | Singing | 0 |
| `freeplay/singing/Singing_WildAboutHarry.json` | Singing_WildAboutHarry | Singing | 0 |
| `freeplay/singing/Singing_WilliamTell.json` | Singing_WilliamTell | Singing | 0 |
| `freeplay/singing/Singing_YankeeDoodle.json` | Singing_YankeeDoodle | Singing | 0 |
| `freeplay/singing/Singing_YellowRose.json` | Singing_YellowRose | Singing | 0 |
| `freeplay/sparkable/sparksBringCubeToBeacon.json` | SparksBringCubeToBeacon | BringCubeToBeacon | 0 |
| `freeplay/sparkable/sparksCheckForStackAtInterval.json` | SparksCheckForStackAtInterval | CheckForStackAtInterval | 0 |
| `freeplay/sparkable/sparksCubeLiftWorkout.json` | SparksCubeLiftWorkout | CubeLiftWorkout | 0 |
| `freeplay/sparkable/sparksFindFaces.json` | SparksFindFaces | FindFaces | 0 |
| `freeplay/sparkable/sparksFireTruckAlarm.json` | SparksFireTruckAlarm | FireTruckAlarm | 0 |
| `freeplay/sparkable/sparksFistBump.json` | SparksFistBump | FistBump | 0 |
| `freeplay/sparkable/sparksKnockOverCubes.json` | SparksKnockOverCubes | KnockOverCubes | 0 |
| `freeplay/sparkable/sparksLookInPlace.json` | SparksLookInPlace | ExploreLookAroundInPlace | 0 |
| `freeplay/sparkable/sparksPeekAboo.json` | SparksPeekABoo | PeekABoo | 0 |
| `freeplay/sparkable/sparksPickupCube.json` | SparksPickUpCube | PickUpAndPutDownCube | 0 |
| `freeplay/sparkable/sparksPickupSingleCubeForPyramid.json` | SparksPickupSingleCubeForPyramid | PickUpCube | 0 |
| `freeplay/sparkable/sparksPickupSingleCubeToStack.json` | SparksPickupSingleCubeToStack | PickUpCube | 0 |
| `freeplay/sparkable/sparksPopAWheelie.json` | SparksPopAWheelie | PopAWheelie | 0 |
| `freeplay/sparkable/sparksPounceOnMotion.json` | SparksPounceOnMotion | PounceOnMotion | 0 |
| `freeplay/sparkable/sparksPutDownBlock.json` | SparksPutDownBlock | PutDownBlock | 0 |
| `freeplay/sparkable/sparksRollBlock.json` | SparksRollBlock | RollBlock | 0 |
| `freeplay/sparkable/sparksStackBlock.json` | SparksStackBlock | StackBlocks | 0 |
| `freeplay/sparkable/sparksThinkAboutBeacons.json` | SparksThinkAboutBeacons | ThinkAboutBeacons | 0 |
| `freeplay/sparkable/sparksTrackLaser.json` | SparksTrackLaser | TrackLaser | 0 |
| `freeplay/sparkable/sparksVisitPossibleMarker.json` | SparksVisitPossibleMarker | ExploreVisitPossibleMarker | 0 |
| `freeplay/userInteractive/bouncer.json` | Bouncer | Bouncer | 0 |
| `freeplay/userInteractive/fistBump.json` | FistBump | FistBump | 0 |
| `freeplay/userInteractive/guardDog.json` | GuardDog | GuardDog | 0 |
| `meetCozmo/enrollFace.json` | EnrollFace | EnrollFace | 0 |
| `meetCozmo/meetCozmo_findFaces_socialize.json` | MeetCozmo_FindFaces_Socialize | FindFaces | 0 |
| `meetCozmo/meetCozmo_interactWithFaces.json` | MeetCozmo_InteractWithFaces | InteractWithFaces | 0 |
| `meetCozmo/respondToRenameFace.json` | RespondToRenameFace | RespondToRenameFace | 0 |
| `onboarding/onboardingShowCube.json` | OnboardingShowCube | OnboardingShowCube | 0 |
| `reactions/acknowledgeFace.json` | AcknowledgeFace | AcknowledgeFace | 0 |
| `reactions/acknowledgeObject.json` | AcknowledgeObject | AcknowledgeObject | 0 |
| `reactions/hiccup.json` | Hiccup | PlayAnim | 1 |
| `reactions/ramIntoBlock.json` | RamIntoBlock | RamIntoBlock | 0 |
| `reactions/reactToCliff.json` | ReactToCliff | ReactToCliff | 0 |
| `reactions/reactToCubeMoved.json` | ReactToCubeMoved | ReactToCubeMoved | 0 |
| `reactions/reactToFrustrationMajor.json` | ReactToFrustrationMajor | ReactToFrustration | 0 |
| `reactions/reactToFrustrationMinor.json` | ReactToFrustrationMinor | ReactToFrustration | 0 |
| `reactions/reactToImpact.json` | ReactToImpact | ReactToImpact | 0 |
| `reactions/reactToMotorCalibration.json` | ReactToMotorCalibration | ReactToMotorCalibration | 0 |
| `reactions/reactToObstacle.json` | ReactToObstacle | PlayAnim | 1 |
| `reactions/reactToOnCharger.json` | ReactToOnCharger | ReactToOnCharger | 0 |
| `reactions/reactToPet.json` | ReactToPet | ReactToPet | 0 |
| `reactions/reactToPickup.json` | ReactToPickup | ReactToPickup | 0 |
| `reactions/reactToPlacedOnSlope.json` | ReactToPlacedOnSlope | ReactToPlacedOnSlope | 0 |
| `reactions/reactToPyramid.json` | ReactToPyramid | ReactToPyramid | 0 |
| `reactions/reactToReturnedToTreads.json` | ReactToReturnedToTreads | ReactToReturnedToTreads | 0 |
| `reactions/reactToRobotOnBack.json` | ReactToRobotOnBack | ReactToRobotOnBack | 0 |
| `reactions/reactToRobotOnFace.json` | ReactToRobotOnFace | ReactToRobotOnFace | 0 |
| `reactions/reactToRobotOnSide.json` | ReactToRobotOnSide | ReactToRobotOnSide | 0 |
| `reactions/reactToRobotShaken.json` | ReactToRobotShaken | ReactToRobotShaken | 0 |
| `reactions/reactToSparked.json` | ReactToSparked | ReactToSparked | 0 |
| `reactions/reactToStackOfCubes.json` | ReactToStackOfCubes | ReactToStackOfCubes | 0 |
| `reactions/reactToUnexpectedMovement.json` | ReactToUnexpectedMovement | ReactToUnexpectedMovement | 0 |
| `voiceCommands/dance_mambo.json` | Dance_Mambo | Dance | 1 |
| `voiceCommands/VC_AlrightyResponse.json` | VC_AlrightyResponse | PlayAnimWithFace | 1 |
| `voiceCommands/VC_ComeHere.json` | VC_ComeHere | DriveToFace | 0 |
| `voiceCommands/VC_GoToSleep.json` | VC_GoToSleep | ReactToOnCharger | 0 |
| `voiceCommands/VC_PounceOnMotion.json` | VC_PounceOnMotion | PounceOnMotion | 0 |
| `voiceCommands/VC_Refuse_Energy.json` | VC_Refuse_Energy | PlayAnim | 1 |
| `voiceCommands/VC_Refuse_Repair.json` | VC_Refuse_Repair | PlayAnim | 1 |
| `voiceCommands/VC_Refuse_Sparks.json` | VC_Refuse_Sparks | PlayAnim | 1 |
| `voiceCommands/VC_RequestCozmoPerforms.json` | VC_RequestCozmoPerforms | RequestGameSimple | 0 |
| `voiceCommands/VC_RequestDroneMode.json` | VC_RequestDroneMode | RequestGameSimple | 0 |
| `voiceCommands/VC_RequestKeepAway.json` | VC_RequestKeepAway | RequestGameSimple | 0 |
| `voiceCommands/VC_RequestMemoryMatch.json` | VC_RequestMemoryMatch | RequestGameSimple | 0 |
| `voiceCommands/VC_RequestSpeedTap.json` | VC_RequestSpeedTap | RequestGameSimple | 0 |
| `voiceCommands/VC_SearchForFace.json` | VC_SearchForFace | SearchForFace | 0 |
| `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_AllGood.json` | VC_HowAreYouDoing_AllGood | PlayAnimWithFace | 1 |
| `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_Energy.json` | VC_HowAreYouDoing_Energy | PlayAnimWithFace | 1 |
| `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_Play.json` | VC_HowAreYouDoing_Play | PlayAnimWithFace | 1 |
| `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_Repair.json` | VC_HowAreYouDoing_Repair | PlayAnimWithFace | 1 |

## Existing records, judged

- **M7-001** (EXACT_SOURCE, enums): confirmed. The Unity enums are present with the counts the job
  names (AnimationTrigger ~575 values, ReactionTrigger 21, BehaviorClass 79, BehaviorID 179).
- **M7-002** (EXACT_SOURCE, "AnimationTriggerMap.json and reactionTrigger_behavior_map.json are the
  shipped maps"): **now fully confirmed.** Both files are in the OBB, the engine's path strings match,
  and the loaders are real. The record should also name `assets/animationGroupMaps/` and the 573/40
  pair counts.
- **M7-011** (EXACT_SOURCE, reaction cooldowns): the map half is now confirmed from
  `reactionTrigger_behavior_map.json`. `FrustrationMinor` is the only frustration entry with a
  `cooldownTime_s` (60.0); `FrustrationMajor` has none and the code returns 1.0 for a non-positive
  stamp. The FistBump/Hiccup entries carry their own cooldowns.
- **M7-002's** `RobotDataLoader::LoadReactionTriggerMap 0x00520BC8` citation is real; add
  `BehaviorManager::InitReactionTriggerMap 0x005a16e4`.

## NEW steps no record covers

- N1. The 178-config table above (path, ID, class, animTrigger count) - no record lists them.
- N2. `behaviorClass` vs `behaviorID`: 76 classes for 178 IDs; the mapping is in the table.
- N3. The full 21-entry reaction map, including the FistBump objective params and the Hiccup params.
- N4. The animation-trigger maps: 573 pairs and 40 cube pairs.
- N5. The activity tree: `StrictPriorityFreeplay` and the `activities_config.json` activities, plus the
  per-activity `activities/` files and `requiredRecentOnTreadsEventSecs`.
- N6. The engine path strings for every one of these artifacts.

## Open questions for the manager

- Q1. The config table is 178 rows. Does the manager want it as a manifest record (one record per
  behaviour) or one record for the config set?
- Q2. `AnimationTriggerMap.json` is under `assets/`, not `config/engine/`; M7-002's title names it
  without a directory. Correct the record's location?
- Q3. The per-class run logic (part 2's entry points) is still not read. Split into batches, or keep
  RECOVERABLE_GAP?
