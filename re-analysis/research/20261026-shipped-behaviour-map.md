# Shipped behaviour map for the operator BEHAVIOURS scoreboard — 2026-10-26

Research input only, requested by the manager for the operator's BEHAVIOURS.md scoreboard. No binary tracing or new gap/subsystem audit. Actual model/effort telemetry unobservable. All whole-behavior scoreboard states are **not started**, except **DriveOffCharger — in progress**. Existing source notes, implementations, partial pilot helpers, manifests or tests do not make another behavior finished. Manager owns scoreboard publication/review.

## Corpus and exact meaning

Read all 294 `.json` files under shipped `re-analysis/obb/assets/cozmo_resources/config/` with string-aware comment/trailing-comma parsing; zero parse failures. Behavior definitions are exactly the files under `engine/behaviorSystem/behaviors/` with top-level behaviorID/behaviorClass: **178 configs, 178 unique IDs, 76 classes**. This matches checked M7 inventory Appendix C's178/178/76 census. No inferred enum-only behavior row was fabricated; M7-001 separately names179 enum IDs/79 enum classes.

Explicit references below mean `behaviorID` fields outside each definition, or values under actual chooser `behaviors` arrays. This finds **273 reference occurrences to 127 IDs**. Locations are exact asset paths plus JSON pointers; they establish configured inputs, not native admission, actual runtime selection or reachability. 299 other exact-name matches (class/activity/unlock/animation/needs/log names etc.) are retained separately in the machine corpus and **not** called behavior references. All shippable definitions remain listed, including developer tests and IDs with no explicit reference in this corpus. Native/app hard-coded references outside this config survey remain UNKNOWN.

Paths in definition table are relative to `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/`. Reference table paths are relative to `.../config/`. References preserve repeated occurrences and chooser positions, not deduplicated sets or sorted runtime order. Table sorting is for planning only.

## Shared driving-to-cube / docking / pickup dependency index first

This is a source index of already checked shared action interfaces, **not** a claim that every cube-named behavior calls every item. The actual behavior→interface edge stays UNKNOWN unless explicitly evidenced below. No dependency was inferred from a class name or copied from the authority6 generated behavior classifier (M7 inventory expressly warns against that).

| Shared source unit | Existing checked evidence to reuse | Boundary |
|---|---|---|
| M12 pre-action poses + driving goal + object selection | M12 inventory M12-001 /003 /017 /022 /031; specific current record quotes below | Bind action admission, target/root/pose/obstacle inputs and owning behavior call chain before whole-behavior claim. Not a universal behavior dependency. |
| M12 carrying / pickup-result / release | M12-006: pickup verification and BehaviorPutDownBlock callsites; M12 inventory action-entry interface00553648/0055C464 | Shared concrete action/helper source index; PickUpCube's complete production chain remains UNKNOWN in this asset map. |
| M12 roll retry | M12-016 RollBlockHelper→DriveToRollObjectAction005B9EF8..9F20 | This proves helper dependency only. A config class named RollBlock does not alone prove the calling edge. |
| M13 knock-over/flip route | M13-014 checked inventory names actual BehaviorKnockOverCubes and downstream DriveAndFlipBlockAction/IDriveToInteractWithObject | Explicit existing source edge, not entire behavior acceptance. |
| M12 stack/pyramid placement parameters | M12-028 explicit named BehaviorStackBlocks/BuildPyramid/BuildPyramidBase producer addresses | Remaining parameter/callback recovery/binding is not hidden by mapping. |
| Current DriveOff pilot and typed losing Look candidate |20261026-driveoff-pilot-source-handoff.md, main read C7/C7.1, independent checks | DriveOff is in progress only; Look's NONRUNNING candidate is not its selected action body. |

The first definition group below is the operator's requested pickup/driving/docking planning focus; this ordering itself is **not source evidence**. All unspecified complete dependency chains are UNKNOWN until a whole-behavior read.

## Every shipped behavior definition

| Behavior ID | Class | Shipped path | Scoreboard state | Exact references | Known source slice / remaining dependency scope |
|---|---|---|---|---|---|
| `SparksPickupSingleCubeForPyramid` | `PickUpCube` | `freeplay/sparkable/sparksPickupSingleCubeForPyramid.json` | not started | R004, R148 | UNKNOWN until whole-behavior read. |
| `SparksPickupSingleCubeToStack` | `PickUpCube` | `freeplay/sparkable/sparksPickupSingleCubeToStack.json` | not started | R233 | UNKNOWN until whole-behavior read. |
| `PutDownBlock` | `PutDownBlock` | `freeplay/putDownBlock.json` | not started | R019, R035, R040, R043, R068, R084, R132, R247 | M12 carrying/release: BehaviorPutDownBlock→SetCarriedObjectAsUnattached(false)005C84CE (M12-006, checked M12 inventory). Rest UNKNOWN. |
| `PutDownBlockNothingToDo` | `PutDownBlock` | `freeplay/putDownBlockNothingToDo.json` | not started | R050 | M12 carrying/release: BehaviorPutDownBlock→SetCarriedObjectAsUnattached(false)005C84CE (M12-006, checked M12 inventory). Rest UNKNOWN. |
| `PyramidPutDownBlock` | `PutDownBlock` | `freeplay/buildPyramid/pyramidPutDownBlock.json` | not started | R014, R152, R250 | M12 carrying/release: BehaviorPutDownBlock→SetCarriedObjectAsUnattached(false)005C84CE (M12-006, checked M12 inventory). Rest UNKNOWN. |
| `SparksPutDownBlock` | `PutDownBlock` | `freeplay/sparkable/sparksPutDownBlock.json` | not started | R194, R205, R208, R215, R219, R223, R228, R236 | M12 carrying/release: BehaviorPutDownBlock→SetCarriedObjectAsUnattached(false)005C84CE (M12-006, checked M12 inventory). Rest UNKNOWN. |
| `KnockOverCubes` | `KnockOverCubes` | `freeplay/knockOverCubes.json` | not started | R002, R013, R017, R049, R061, R076, R130, R146, R151, R197, R212, R218, R226, R231, R239, R249 | M13-014 checked M13 inventory names BehaviorKnockOverCubes→DriveAndFlipBlockAction/IDriveToInteractWithObject. Full behavior still not started. |
| `SparksKnockOverCubes` | `KnockOverCubes` | `freeplay/sparkable/sparksKnockOverCubes.json` | not started | R203 | M13-014 checked M13 inventory names BehaviorKnockOverCubes→DriveAndFlipBlockAction/IDriveToInteractWithObject. Full behavior still not started. |
| `SparksStackBlock` | `StackBlocks` | `freeplay/sparkable/sparksStackBlock.json` | not started | R232 | M12-028 explicit placement-parameter producer005C9588 (M12 inventory); producer completeness UNKNOWN. Rest UNKNOWN. |
| `StackBlocks` | `StackBlocks` | `freeplay/stackBlocks.json` | not started | R064 | M12-028 explicit placement-parameter producer005C9588 (M12 inventory); producer completeness UNKNOWN. Rest UNKNOWN. |
| `Hiking_RollCube` | `RollBlock` | `freeplay/hiking/Hiking_rollCube.json` | not started | R023, R198 | UNKNOWN until whole-behavior read. |
| `RollBlockOnSide` | `RollBlock` | `freeplay/rollBlockOnSide.json` | not started | R063, R131 | UNKNOWN until whole-behavior read. |
| `RollBlockOnSideLowScore` | `RollBlock` | `freeplay/rollBlockOnSideLowScore.json` | not started | R077 | UNKNOWN until whole-behavior read. |
| `SparksRollBlock` | `RollBlock` | `freeplay/sparkable/sparksRollBlock.json` | not started | R227 | UNKNOWN until whole-behavior read. |
| `PopAWheelie` | `PopAWheelie` | `freeplay/popAWheelie.json` | not started | R066 | UNKNOWN until whole-behavior read. |
| `SparksPopAWheelie` | `PopAWheelie` | `freeplay/sparkable/sparksPopAWheelie.json` | not started | R220 | UNKNOWN until whole-behavior read. |
| `BuildPyramidBase` | `BuildPyramidBase` | `freeplay/buildPyramid/buildPyramidBase.json` | not started | NONE IN THIS CORPUS | M12-028 explicit placement-parameter producer005DCE44. Whole helper/callback chain UNKNOWN. |
| `BuildPyramid` | `BuildPyramid` | `freeplay/buildPyramid/buildPyramid.json` | not started | NONE IN THIS CORPUS | M12-028 explicit placement-parameter producer005DC266; M12-049 callbacks/cache split remains. Rest UNKNOWN. |
| `DockingTestSimple` | `DockingTestSimple` | `devBehaviors/dockingTestSimple.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `AcknowledgeFace` | `AcknowledgeFace` | `reactions/acknowledgeFace.json` | not started | R252 | UNKNOWN until whole-behavior read. |
| `AcknowledgeObject` | `AcknowledgeObject` | `reactions/acknowledgeObject.json` | not started | R267 | UNKNOWN until whole-behavior read. |
| `Bouncer` | `Bouncer` | `freeplay/userInteractive/bouncer.json` | not started | R078 | UNKNOWN until whole-behavior read. |
| `Hiking_BringCubeToBeacon` | `BringCubeToBeacon` | `freeplay/hiking/Hiking_bringCubeToBeacon.json` | not started | R025 | UNKNOWN until whole-behavior read. |
| `SparksBringCubeToBeacon` | `BringCubeToBeacon` | `freeplay/sparkable/sparksBringCubeToBeacon.json` | not started | R200 | UNKNOWN until whole-behavior read. |
| `CantHandleTallStack` | `CantHandleTallStack` | `freeplay/cantHandleTallStack.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `SparksCheckForStackAtInterval` | `CheckForStackAtInterval` | `freeplay/sparkable/sparksCheckForStackAtInterval.json` | not started | R204 | UNKNOWN until whole-behavior read. |
| `CubeLiftWorkout` | `CubeLiftWorkout` | `freeplay/cubeLiftWorkout.json` | not started | R065 | UNKNOWN until whole-behavior read. |
| `SparksCubeLiftWorkout` | `CubeLiftWorkout` | `freeplay/sparkable/sparksCubeLiftWorkout.json` | not started | R240 | UNKNOWN until whole-behavior read. |
| `Dance_Mambo` | `Dance` | `voiceCommands/dance_mambo.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `DevTurnInPlaceTest` | `DevTurnInPlaceTest` | `devBehaviors/devTurnInPlaceTest.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `Needs_SevereLowEnergyState` | `DriveInDesperation` | `freeplay/needs/needs_SevereLowEnergyState.json` | not started | R037 | UNKNOWN until whole-behavior read. |
| `Needs_SevereLowRepairState` | `DriveInDesperation` | `freeplay/needs/needs_SevereLowRepairState.json` | not started | R045 | UNKNOWN until whole-behavior read. |
| `DriveOffCharger` | `DriveOffCharger` | `freeplay/driveOffCharger.json` | in progress | R001, R012, R034, R039, R042, R047, R059, R075, R128, R143, R145, R150, R190, R193, R196, R202, R207, R211, R217, R222, R225, R230, R235, R238, R242, R248 | Current pilot source ledger: M8→plain DriveStraight/custom path M12/M13, M5/M7 mood/animation, M11 ingress/vision, M12 configurations, M6/M9 audio STOP. Full whole-behavior verification pending. |
| `Hiking_DriveOffCharger` | `DriveOffCharger` | `freeplay/hiking/Hiking_driveOffCharger.json` | not started | R020 | Current pilot source ledger: M8→plain DriveStraight/custom path M12/M13, M5/M7 mood/animation, M11 ingress/vision, M12 configurations, M6/M9 audio STOP. Full whole-behavior verification pending. |
| `VC_ComeHere` | `DriveToFace` | `voiceCommands/VC_ComeHere.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `EarnedSparks` | `EarnedSparks` | `freeplay/EarnedSparks.json` | not started | R006, R028, R053, R069, R085, R127, R135 | UNKNOWN until whole-behavior read. |
| `EnrollFace` | `EnrollFace` | `meetCozmo/enrollFace.json` | not started | R244 | UNKNOWN until whole-behavior read. |
| `Hiking_LookInPlace360` | `ExploreLookAroundInPlace` | `freeplay/hiking/Hiking_lookInPlace360.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `SparksLookInPlace` | `ExploreLookAroundInPlace` | `freeplay/sparkable/sparksLookInPlace.json` | not started | R005, R015, R149, R153, R192, R201, R206, R216, R221, R229, R234, R241 | UNKNOWN until whole-behavior read. |
| `SparksVisitPossibleMarker` | `ExploreVisitPossibleMarker` | `freeplay/sparkable/sparksVisitPossibleMarker.json` | not started | R214 | UNKNOWN until whole-behavior read. |
| `Needs_MildLowEnergyRequest` | `ExpressNeeds` | `freeplay/needs/needs_MildLowEnergyRequest.json` | not started | R010, R032, R057, R073, R089, R139 | UNKNOWN until whole-behavior read. |
| `Needs_MildLowPlayRequest` | `ExpressNeeds` | `freeplay/needs/needs_MildLowPlayRequest.json` | not started | R011, R033, R058, R074, R090, R140 | UNKNOWN until whole-behavior read. |
| `Needs_MildLowRepairRequest` | `ExpressNeeds` | `freeplay/needs/needs_MildLowRepairRequest.json` | not started | R009, R031, R056, R072, R088, R138 | UNKNOWN until whole-behavior read. |
| `Needs_SevereLowEnergyForcedGetOut` | `ExpressNeeds` | `freeplay/needs/needs_SevereLowEnergyForcedGetOut.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `Needs_SevereLowPlayBored` | `ExpressNeeds` | `freeplay/needs/needs_SevereLowPlayBored.json` | not started | R007, R029, R054, R070, R086, R136 | UNKNOWN until whole-behavior read. |
| `Needs_SevereLowPlayRequest` | `ExpressNeeds` | `freeplay/needs/needs_SevereLowPlayRequest.json` | not started | R008, R030, R055, R071, R087, R137 | UNKNOWN until whole-behavior read. |
| `FactoryCentroidExtractor` | `FactoryCentroidExtractor` | `devBehaviors/factoryCentroidExtractor.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FactoryTest` | `FactoryTest` | `devBehaviors/factoryTest.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FeedingEat` | `FeedingEat` | `feeding/feedingEat.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FeedingSearchForCube` | `FeedingSearchForCube` | `feeding/feedingSearchForCube.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FeedingFindFacesSevere` | `FindFaces` | `feeding/feedingFindFacesSevere.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FindFaces_socialize` | `FindFaces` | `freeplay/findFaces_socialize.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `MeetCozmo_FindFaces_Socialize` | `FindFaces` | `meetCozmo/meetCozmo_findFaces_socialize.json` | not started | R246 | UNKNOWN until whole-behavior read. |
| `SparksFindFaces` | `FindFaces` | `freeplay/sparkable/sparksFindFaces.json` | not started | R210 | UNKNOWN until whole-behavior read. |
| `SparksFireTruckAlarm` | `FireTruckAlarm` | `freeplay/sparkable/sparksFireTruckAlarm.json` | not started | R067, R191 | UNKNOWN until whole-behavior read. |
| `FistBump` | `FistBump` | `freeplay/userInteractive/fistBump.json` | not started | R253 | UNKNOWN until whole-behavior read. |
| `SparksFistBump` | `FistBump` | `freeplay/sparkable/sparksFistBump.json` | not started | R195 | UNKNOWN until whole-behavior read. |
| `GuardDog` | `GuardDog` | `freeplay/userInteractive/guardDog.json` | not started | R062 | UNKNOWN until whole-behavior read. |
| `InteractWithFaces` | `InteractWithFaces` | `freeplay/interactWithFaces.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `MeetCozmo_InteractWithFaces` | `InteractWithFaces` | `meetCozmo/meetCozmo_interactWithFaces.json` | not started | R245 | UNKNOWN until whole-behavior read. |
| `LiftLoadTest` | `LiftLoadTest` | `devBehaviors/liftLoadTest.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `PutDownDispatch_LookForFaceAndCube` | `LookForFaceAndCube` | `freeplay/putDownDispatch/PutDownDispatch_LookForFaceAndCube.json` | not started | R144 | C7/C7.1: actual common M8 NONRUNNING gate/score/subscriptions/RNG only. Selected action/body UNKNOWN. |
| `Hiking_LookInPlaceForUnknown` | `LookInPlaceMemoryMap` | `freeplay/hiking/Hiking_lookInPlaceForUnknown.json` | not started | R018 | UNKNOWN until whole-behavior read. |
| `RespondToPyramidBase` | `OnConfigSeen` | `freeplay/buildPyramid/respondToPyramidBase.json` | not started | R003, R147 | UNKNOWN until whole-behavior read. |
| `OnboardingShowCube` | `OnboardingShowCube` | `onboarding/onboardingShowCube.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FPPeekABoo` | `PeekABoo` | `freeplay/FPpeekAboo.json` | not started | R134, R142 | UNKNOWN until whole-behavior read. |
| `SparksPeekABoo` | `PeekABoo` | `freeplay/sparkable/sparksPeekAboo.json` | not started | R209 | UNKNOWN until whole-behavior read. |
| `SparksPickUpCube` | `PickUpAndPutDownCube` | `freeplay/sparkable/sparksPickupCube.json` | not started | R213 | UNKNOWN until whole-behavior read. |
| `FeedingReactCubeShake` | `PlayAnim` | `feeding/feedingAnims/feedingReactCubeShake.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FeedingReactCubeShake_Severe` | `PlayAnim` | `feeding/feedingAnims/feedingReactCubeShake_Severe.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FeedingReactFullCube` | `PlayAnim` | `feeding/feedingAnims/feedingReactFullCube.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FeedingReactFullCube_Severe` | `PlayAnim` | `feeding/feedingAnims/feedingReactFullCube_Severe.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FeedingReactSeeCharged` | `PlayAnim` | `feeding/feedingAnims/feedingReactSeeCharged.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FeedingReactSeeCharged_Severe` | `PlayAnim` | `feeding/feedingAnims/feedingReactSeeCharged_Severe.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `Hiccup` | `PlayAnim` | `reactions/hiccup.json` | not started | R264 | UNKNOWN until whole-behavior read. |
| `Hiking_FirstLookIntro` | `PlayAnim` | `freeplay/hiking/Hiking_firstLookIntro.json` | not started | R021 | UNKNOWN until whole-behavior read. |
| `Hiking_FirstLookWakeUp` | `PlayAnim` | `freeplay/hiking/Hiking_firstLookWakeUp.json` | not started | R022 | UNKNOWN until whole-behavior read. |
| `NothingToDo_BoredAnim` | `PlayAnim` | `freeplay/NothingToDo_BoredAnim.json` | not started | R052 | UNKNOWN until whole-behavior read. |
| `NothingToDo_Idle` | `PlayAnim` | `freeplay/NothingToDo_Idle.json` | not started | R051 | UNKNOWN until whole-behavior read. |
| `ReactToObstacle` | `PlayAnim` | `reactions/reactToObstacle.json` | not started | R016, R048, R060, R129 | UNKNOWN until whole-behavior read. |
| `VC_Refuse_Energy` | `PlayAnim` | `voiceCommands/VC_Refuse_Energy.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `VC_Refuse_Repair` | `PlayAnim` | `voiceCommands/VC_Refuse_Repair.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `VC_Refuse_Sparks` | `PlayAnim` | `voiceCommands/VC_Refuse_Sparks.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `Needs_SevereLowEnergyGetIn` | `PlayAnimOnNeedsChange` | `freeplay/needs/needs_SevereLowEnergyGetIn.json` | not started | R036, R251 | UNKNOWN until whole-behavior read. |
| `Needs_SevereLowPlayGetIn` | `PlayAnimOnNeedsChange` | `freeplay/needs/needs_SevereLowPlayGetIn.json` | not started | R041 | UNKNOWN until whole-behavior read. |
| `Needs_SevereLowRepairGetIn` | `PlayAnimOnNeedsChange` | `freeplay/needs/needs_SevereLowRepairGetIn.json` | not started | R044 | UNKNOWN until whole-behavior read. |
| `FeedingPlayRequestAtFace` | `PlayAnimWithFace` | `feeding/feedingAnims/feedingPlayRequestAtFace.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `FeedingPlayRequestAtFace_Severe` | `PlayAnimWithFace` | `feeding/feedingAnims/feedingPlayRequestAtFace_Severe.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `VC_AlrightyResponse` | `PlayAnimWithFace` | `voiceCommands/VC_AlrightyResponse.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `VC_HowAreYouDoing_AllGood` | `PlayAnimWithFace` | `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_AllGood.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `VC_HowAreYouDoing_Energy` | `PlayAnimWithFace` | `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_Energy.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `VC_HowAreYouDoing_Play` | `PlayAnimWithFace` | `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_Play.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `VC_HowAreYouDoing_Repair` | `PlayAnimWithFace` | `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_Repair.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `PlayArbitraryAnim` | `PlayArbitraryAnim` | `playArbitraryAnim.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `Hiking_PounceOnMotion` | `PounceOnMotion` | `freeplay/hiking/Hiking_pounceOnMotion.json` | not started | R026 | UNKNOWN until whole-behavior read. |
| `PounceOnMotion_Socialize` | `PounceOnMotion` | `freeplay/pounceOnMotion_socialize.json` | not started | R133, R141 | UNKNOWN until whole-behavior read. |
| `SparksPounceOnMotion` | `PounceOnMotion` | `freeplay/sparkable/sparksPounceOnMotion.json` | not started | R224 | UNKNOWN until whole-behavior read. |
| `VC_PounceOnMotion` | `PounceOnMotion` | `voiceCommands/VC_PounceOnMotion.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `PyramidThankYou` | `PyramidThankYou` | `freeplay/buildPyramid/pyramidThankYou.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `RamIntoBlock` | `RamIntoBlock` | `reactions/ramIntoBlock.json` | not started | R273 | UNKNOWN until whole-behavior read. |
| `ReactToCliff` | `ReactToCliff` | `reactions/reactToCliff.json` | not started | R268 | UNKNOWN until whole-behavior read. |
| `ReactToCubeMoved` | `ReactToCubeMoved` | `reactions/reactToCubeMoved.json` | not started | R269 | UNKNOWN until whole-behavior read. |
| `ReactToFrustrationMajor` | `ReactToFrustration` | `reactions/reactToFrustrationMajor.json` | not started | R271 | UNKNOWN until whole-behavior read. |
| `ReactToFrustrationMinor` | `ReactToFrustration` | `reactions/reactToFrustrationMinor.json` | not started | R270 | UNKNOWN until whole-behavior read. |
| `ReactToImpact` | `ReactToImpact` | `reactions/reactToImpact.json` | not started | R256 | UNKNOWN until whole-behavior read. |
| `ReactToMotorCalibration` | `ReactToMotorCalibration` | `reactions/reactToMotorCalibration.json` | not started | R272 | UNKNOWN until whole-behavior read. |
| `ReactToOnCharger` | `ReactToOnCharger` | `reactions/reactToOnCharger.json` | not started | R254 | UNKNOWN until whole-behavior read. |
| `VC_GoToSleep` | `ReactToOnCharger` | `voiceCommands/VC_GoToSleep.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `ReactToPet` | `ReactToPet` | `reactions/reactToPet.json` | not started | R255 | UNKNOWN until whole-behavior read. |
| `ReactToPickup` | `ReactToPickup` | `reactions/reactToPickup.json` | not started | R257 | UNKNOWN until whole-behavior read. |
| `ReactToPlacedOnSlope` | `ReactToPlacedOnSlope` | `reactions/reactToPlacedOnSlope.json` | not started | R258 | UNKNOWN until whole-behavior read. |
| `ReactToPyramid` | `ReactToPyramid` | `reactions/reactToPyramid.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `ReactToReturnedToTreads` | `ReactToReturnedToTreads` | `reactions/reactToReturnedToTreads.json` | not started | R259 | UNKNOWN until whole-behavior read. |
| `ReactToRobotOnBack` | `ReactToRobotOnBack` | `reactions/reactToRobotOnBack.json` | not started | R260 | UNKNOWN until whole-behavior read. |
| `ReactToRobotOnFace` | `ReactToRobotOnFace` | `reactions/reactToRobotOnFace.json` | not started | R261 | UNKNOWN until whole-behavior read. |
| `ReactToRobotOnSide` | `ReactToRobotOnSide` | `reactions/reactToRobotOnSide.json` | not started | R262 | UNKNOWN until whole-behavior read. |
| `ReactToRobotShaken` | `ReactToRobotShaken` | `reactions/reactToRobotShaken.json` | not started | R263 | UNKNOWN until whole-behavior read. |
| `ReactToSparked` | `ReactToSparked` | `reactions/reactToSparked.json` | not started | R265 | UNKNOWN until whole-behavior read. |
| `ReactToStackOfCubes` | `ReactToStackOfCubes` | `reactions/reactToStackOfCubes.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `ReactToUnexpectedMovement` | `ReactToUnexpectedMovement` | `reactions/reactToUnexpectedMovement.json` | not started | R266 | UNKNOWN until whole-behavior read. |
| `RequestCozmoPerforms` | `RequestGameSimple` | `freeplay/requestGame/requestCozmoPerforms.json` | not started | R079 | UNKNOWN until whole-behavior read. |
| `RequestDroneMode` | `RequestGameSimple` | `freeplay/requestGame/requestDroneMode.json` | not started | R080 | UNKNOWN until whole-behavior read. |
| `RequestKeepAway` | `RequestGameSimple` | `freeplay/requestGame/requestKeepAway.json` | not started | R083 | UNKNOWN until whole-behavior read. |
| `RequestMemoryMatch` | `RequestGameSimple` | `freeplay/requestGame/requestMemoryMatch.json` | not started | R082 | UNKNOWN until whole-behavior read. |
| `RequestSpeedTap` | `RequestGameSimple` | `freeplay/requestGame/requestSpeedTap.json` | not started | R081 | UNKNOWN until whole-behavior read. |
| `VC_RequestCozmoPerforms` | `RequestGameSimple` | `voiceCommands/VC_RequestCozmoPerforms.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `VC_RequestDroneMode` | `RequestGameSimple` | `voiceCommands/VC_RequestDroneMode.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `VC_RequestKeepAway` | `RequestGameSimple` | `voiceCommands/VC_RequestKeepAway.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `VC_RequestMemoryMatch` | `RequestGameSimple` | `voiceCommands/VC_RequestMemoryMatch.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `VC_RequestSpeedTap` | `RequestGameSimple` | `voiceCommands/VC_RequestSpeedTap.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `PyramidRespondPossiblyRoll` | `RespondPossiblyRoll` | `freeplay/buildPyramid/pyramidRespondPossiblyRoll.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `RespondToRenameFace` | `RespondToRenameFace` | `meetCozmo/respondToRenameFace.json` | not started | R243 | UNKNOWN until whole-behavior read. |
| `VC_SearchForFace` | `SearchForFace` | `voiceCommands/VC_SearchForFace.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `Singing_AbaDaba` | `Singing` | `freeplay/singing/Singing_AbaDaba.json` | not started | R091, R154 | UNKNOWN until whole-behavior read. |
| `Singing_BeautifulDreamer` | `Singing` | `freeplay/singing/Singing_BeautifulDreamer.json` | not started | R092, R155 | UNKNOWN until whole-behavior read. |
| `Singing_Beethovens5th` | `Singing` | `freeplay/singing/Singing_Beethovens5th.json` | not started | R103, R166 | UNKNOWN until whole-behavior read. |
| `Singing_Bingo` | `Singing` | `freeplay/singing/Singing_Bingo.json` | not started | R114, R177 | UNKNOWN until whole-behavior read. |
| `Singing_BuffaloGals` | `Singing` | `freeplay/singing/Singing_BuffaloGals.json` | not started | R121, R184 | UNKNOWN until whole-behavior read. |
| `Singing_Camptown` | `Singing` | `freeplay/singing/Singing_Camptown.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `Singing_CanCan1` | `Singing` | `freeplay/singing/Singing_CanCan1.json` | not started | R122, R185 | UNKNOWN until whole-behavior read. |
| `Singing_CanCan2` | `Singing` | `freeplay/singing/Singing_CanCan2.json` | not started | R123, R186 | UNKNOWN until whole-behavior read. |
| `Singing_DannyBoy` | `Singing` | `freeplay/singing/Singing_DannyBoy.json` | not started | R124, R187 | UNKNOWN until whole-behavior read. |
| `Singing_EntryOfTheGladiators` | `Singing` | `freeplay/singing/Singing_EntryOfTheGladiators.json` | not started | R125, R188 | UNKNOWN until whole-behavior read. |
| `Singing_FarmerInTheDell` | `Singing` | `freeplay/singing/Singing_FarmerInTheDell.json` | not started | R126, R189 | UNKNOWN until whole-behavior read. |
| `Singing_FrereJacques` | `Singing` | `freeplay/singing/Singing_FrereJacques.json` | not started | R093, R156 | UNKNOWN until whole-behavior read. |
| `Singing_HelloMyBaby` | `Singing` | `freeplay/singing/Singing_HelloMyBaby.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `Singing_ItsyBitsySpider` | `Singing` | `freeplay/singing/Singing_ItsyBitsySpider.json` | not started | R094, R157 | UNKNOWN until whole-behavior read. |
| `Singing_LaPaloma` | `Singing` | `freeplay/singing/Singing_LaPaloma.json` | not started | R095, R158 | UNKNOWN until whole-behavior read. |
| `Singing_LondonBridge` | `Singing` | `freeplay/singing/Singing_LondonBridge.json` | not started | R096, R159 | UNKNOWN until whole-behavior read. |
| `Singing_MaryHadALittleLamb` | `Singing` | `freeplay/singing/Singing_MaryHadALittleLamb.json` | not started | R097, R160 | UNKNOWN until whole-behavior read. |
| `Singing_MountainKing` | `Singing` | `freeplay/singing/Singing_MountainKing.json` | not started | R098, R161 | UNKNOWN until whole-behavior read. |
| `Singing_MuffinMan` | `Singing` | `freeplay/singing/Singing_MuffinMan.json` | not started | R099, R162 | UNKNOWN until whole-behavior read. |
| `Singing_MulberryBush` | `Singing` | `freeplay/singing/Singing_MulberryBush.json` | not started | R100, R163 | UNKNOWN until whole-behavior read. |
| `Singing_MussIDenn` | `Singing` | `freeplay/singing/Singing_MussIDenn.json` | not started | R101, R164 | UNKNOWN until whole-behavior read. |
| `Singing_OdeToJoy` | `Singing` | `freeplay/singing/Singing_OdeToJoy.json` | not started | R102, R165 | UNKNOWN until whole-behavior read. |
| `Singing_PachebelCanon` | `Singing` | `freeplay/singing/Singing_PachebelCanon.json` | not started | R104, R167 | UNKNOWN until whole-behavior read. |
| `Singing_PopGoesTheWeasel` | `Singing` | `freeplay/singing/Singing_PopGoesTheWeasel.json` | not started | R105, R168 | UNKNOWN until whole-behavior read. |
| `Singing_RowYourBoat` | `Singing` | `freeplay/singing/Singing_RowYourBoat.json` | not started | R106, R169 | UNKNOWN until whole-behavior read. |
| `Singing_Sakura` | `Singing` | `freeplay/singing/Singing_Sakura.json` | not started | R107, R170 | UNKNOWN until whole-behavior read. |
| `Singing_SilveryMoon` | `Singing` | `freeplay/singing/Singing_SilveryMoon.json` | not started | R108, R171 | UNKNOWN until whole-behavior read. |
| `Singing_TaRaRaBoom` | `Singing` | `freeplay/singing/Singing_TaRaRaBoom.json` | not started | R110, R173 | UNKNOWN until whole-behavior read. |
| `Singing_TakeMeOutToTheBallgame` | `Singing` | `freeplay/singing/Singing_TakeMeOutToTheBallgame.json` | not started | R109, R172 | UNKNOWN until whole-behavior read. |
| `Singing_TisketTasket` | `Singing` | `freeplay/singing/Singing_TisketTasket.json` | not started | R111, R174 | UNKNOWN until whole-behavior read. |
| `Singing_Toccata` | `Singing` | `freeplay/singing/Singing_Toccata.json` | not started | R112, R175 | UNKNOWN until whole-behavior read. |
| `Singing_TurkeyInTheStraw` | `Singing` | `freeplay/singing/Singing_TurkeyInTheStraw.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |
| `Singing_TwinkleTwinkle` | `Singing` | `freeplay/singing/Singing_TwinkleTwinkle.json` | not started | R113, R176 | UNKNOWN until whole-behavior read. |
| `Singing_VivaldiSpring` | `Singing` | `freeplay/singing/Singing_VivaldiSpring.json` | not started | R115, R178 | UNKNOWN until whole-behavior read. |
| `Singing_WaterMusic` | `Singing` | `freeplay/singing/Singing_WaterMusic.json` | not started | R116, R179 | UNKNOWN until whole-behavior read. |
| `Singing_WildAboutHarry` | `Singing` | `freeplay/singing/Singing_WildAboutHarry.json` | not started | R117, R180 | UNKNOWN until whole-behavior read. |
| `Singing_WilliamTell` | `Singing` | `freeplay/singing/Singing_WilliamTell.json` | not started | R118, R181 | UNKNOWN until whole-behavior read. |
| `Singing_YankeeDoodle` | `Singing` | `freeplay/singing/Singing_YankeeDoodle.json` | not started | R119, R182 | UNKNOWN until whole-behavior read. |
| `Singing_YellowRose` | `Singing` | `freeplay/singing/Singing_YellowRose.json` | not started | R120, R183 | UNKNOWN until whole-behavior read. |
| `Hiking_ThinkAboutBeacons` | `ThinkAboutBeacons` | `freeplay/hiking/Hiking_thinkAboutBeacons.json` | not started | R024 | UNKNOWN until whole-behavior read. |
| `SparksThinkAboutBeacons` | `ThinkAboutBeacons` | `freeplay/sparkable/sparksThinkAboutBeacons.json` | not started | R199 | UNKNOWN until whole-behavior read. |
| `SparksTrackLaser` | `TrackLaser` | `freeplay/sparkable/sparksTrackLaser.json` | not started | R237 | UNKNOWN until whole-behavior read. |
| `Hiking_VisitInterestingEdge` | `VisitInterestingEdge` | `freeplay/hiking/Hiking_visitInterestingEdge.json` | not started | R027 | UNKNOWN until whole-behavior read. |
| `Needs_Wait` | `Wait` | `freeplay/needs/needs_Wait.json` | not started | R038, R046 | UNKNOWN until whole-behavior read. |
| `Wait` | `Wait` | `wait.json` | not started | NONE IN THIS CORPUS | UNKNOWN until whole-behavior read. |

## Exact activity / freeplay / reaction / spark behavior references

`source activity` is the nearest enclosing activity definition in that asset, not a guessed runtime owner. A spark/freeplay label comes from the actual asset directory; inline subactivity definitions retain their true enclosing activity ID and file. Reaction source shows the actual `reactionTrigger`. Required-objective behavior fields are mapped separately from choosers. Duplicate occurrences remain separate.

| Ref | Target behavior ID | Config kind | Source activity / reaction | Shipped config path | JSON pointer |
|---|---|---|---|---|---|
| R001 | `DriveOffCharger` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/buildChooser/behaviors/0` |
| R002 | `KnockOverCubes` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/buildChooser/behaviors/1` |
| R003 | `RespondToPyramidBase` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/buildChooser/behaviors/2` |
| R004 | `SparksPickupSingleCubeForPyramid` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/buildChooser/behaviors/3` |
| R005 | `SparksLookInPlace` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/buildChooser/behaviors/4` |
| R006 | `EarnedSparks` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/interludeBehaviorChooser/behaviors/0` |
| R007 | `Needs_SevereLowPlayBored` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/interludeBehaviorChooser/behaviors/1` |
| R008 | `Needs_SevereLowPlayRequest` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/interludeBehaviorChooser/behaviors/2` |
| R009 | `Needs_MildLowRepairRequest` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/interludeBehaviorChooser/behaviors/3` |
| R010 | `Needs_MildLowEnergyRequest` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/interludeBehaviorChooser/behaviors/4` |
| R011 | `Needs_MildLowPlayRequest` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/interludeBehaviorChooser/behaviors/5` |
| R012 | `DriveOffCharger` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/setupChooser/behaviors/0` |
| R013 | `KnockOverCubes` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/setupChooser/behaviors/1` |
| R014 | `PyramidPutDownBlock` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/setupChooser/behaviors/2` |
| R015 | `SparksLookInPlace` | freeplay activity | `BuildPyramid` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/setupChooser/behaviors/3` |
| R016 | `ReactToObstacle` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/0/behaviorID` |
| R017 | `KnockOverCubes` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/1/behaviorID` |
| R018 | `Hiking_LookInPlaceForUnknown` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/10/behaviorID` |
| R019 | `PutDownBlock` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/11/behaviorID` |
| R020 | `Hiking_DriveOffCharger` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/2/behaviorID` |
| R021 | `Hiking_FirstLookIntro` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/3/behaviorID` |
| R022 | `Hiking_FirstLookWakeUp` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/4/behaviorID` |
| R023 | `Hiking_RollCube` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/5/behaviorID` |
| R024 | `Hiking_ThinkAboutBeacons` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/6/behaviorID` |
| R025 | `Hiking_BringCubeToBeacon` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/7/behaviorID` |
| R026 | `Hiking_PounceOnMotion` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/8/behaviorID` |
| R027 | `Hiking_VisitInterestingEdge` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/behaviorChooser/behaviors/9/behaviorID` |
| R028 | `EarnedSparks` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/interludeBehaviorChooser/behaviors/0` |
| R029 | `Needs_SevereLowPlayBored` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/interludeBehaviorChooser/behaviors/1` |
| R030 | `Needs_SevereLowPlayRequest` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/interludeBehaviorChooser/behaviors/2` |
| R031 | `Needs_MildLowRepairRequest` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/interludeBehaviorChooser/behaviors/3` |
| R032 | `Needs_MildLowEnergyRequest` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/interludeBehaviorChooser/behaviors/4` |
| R033 | `Needs_MildLowPlayRequest` | freeplay activity | `Hiking` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/interludeBehaviorChooser/behaviors/5` |
| R034 | `DriveOffCharger` | freeplay activity | `NeedsSevereLowEnergy` | `engine/behaviorSystem/activities/freeplay/needsSevereLowEnergy.json` | `/behaviorChooser/behaviors/0` |
| R035 | `PutDownBlock` | freeplay activity | `NeedsSevereLowEnergy` | `engine/behaviorSystem/activities/freeplay/needsSevereLowEnergy.json` | `/behaviorChooser/behaviors/1` |
| R036 | `Needs_SevereLowEnergyGetIn` | freeplay activity | `NeedsSevereLowEnergy` | `engine/behaviorSystem/activities/freeplay/needsSevereLowEnergy.json` | `/behaviorChooser/behaviors/2` |
| R037 | `Needs_SevereLowEnergyState` | freeplay activity | `NeedsSevereLowEnergy` | `engine/behaviorSystem/activities/freeplay/needsSevereLowEnergy.json` | `/behaviorChooser/behaviors/3` |
| R038 | `Needs_Wait` | freeplay activity | `NeedsSevereLowEnergy` | `engine/behaviorSystem/activities/freeplay/needsSevereLowEnergy.json` | `/behaviorChooser/behaviors/4` |
| R039 | `DriveOffCharger` | freeplay activity | `NeedsSevereLowPlayGetIn` | `engine/behaviorSystem/activities/freeplay/needsSevereLowPlayGetIn.json` | `/behaviorChooser/behaviors/0` |
| R040 | `PutDownBlock` | freeplay activity | `NeedsSevereLowPlayGetIn` | `engine/behaviorSystem/activities/freeplay/needsSevereLowPlayGetIn.json` | `/behaviorChooser/behaviors/1` |
| R041 | `Needs_SevereLowPlayGetIn` | freeplay activity | `NeedsSevereLowPlayGetIn` | `engine/behaviorSystem/activities/freeplay/needsSevereLowPlayGetIn.json` | `/behaviorChooser/behaviors/2` |
| R042 | `DriveOffCharger` | freeplay activity | `NeedsSevereLowRepair` | `engine/behaviorSystem/activities/freeplay/needsSevereLowRepair.json` | `/behaviorChooser/behaviors/0` |
| R043 | `PutDownBlock` | freeplay activity | `NeedsSevereLowRepair` | `engine/behaviorSystem/activities/freeplay/needsSevereLowRepair.json` | `/behaviorChooser/behaviors/1` |
| R044 | `Needs_SevereLowRepairGetIn` | freeplay activity | `NeedsSevereLowRepair` | `engine/behaviorSystem/activities/freeplay/needsSevereLowRepair.json` | `/behaviorChooser/behaviors/2` |
| R045 | `Needs_SevereLowRepairState` | freeplay activity | `NeedsSevereLowRepair` | `engine/behaviorSystem/activities/freeplay/needsSevereLowRepair.json` | `/behaviorChooser/behaviors/3` |
| R046 | `Needs_Wait` | freeplay activity | `NeedsSevereLowRepair` | `engine/behaviorSystem/activities/freeplay/needsSevereLowRepair.json` | `/behaviorChooser/behaviors/4` |
| R047 | `DriveOffCharger` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/behaviorChooser/behaviors/0/behaviorID` |
| R048 | `ReactToObstacle` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/behaviorChooser/behaviors/1/behaviorID` |
| R049 | `KnockOverCubes` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/behaviorChooser/behaviors/2/behaviorID` |
| R050 | `PutDownBlockNothingToDo` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/behaviorChooser/behaviors/3/behaviorID` |
| R051 | `NothingToDo_Idle` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/behaviorChooser/behaviors/4/behaviorID` |
| R052 | `NothingToDo_BoredAnim` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/behaviorChooser/behaviors/5/behaviorID` |
| R053 | `EarnedSparks` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/interludeBehaviorChooser/behaviors/0` |
| R054 | `Needs_SevereLowPlayBored` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/interludeBehaviorChooser/behaviors/1` |
| R055 | `Needs_SevereLowPlayRequest` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/interludeBehaviorChooser/behaviors/2` |
| R056 | `Needs_MildLowRepairRequest` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/interludeBehaviorChooser/behaviors/3` |
| R057 | `Needs_MildLowEnergyRequest` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/interludeBehaviorChooser/behaviors/4` |
| R058 | `Needs_MildLowPlayRequest` | freeplay activity | `NothingToDo` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/interludeBehaviorChooser/behaviors/5` |
| R059 | `DriveOffCharger` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/behaviorChooser/behaviors/0/behaviorID` |
| R060 | `ReactToObstacle` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/behaviorChooser/behaviors/1/behaviorID` |
| R061 | `KnockOverCubes` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/behaviorChooser/behaviors/2/behaviorID` |
| R062 | `GuardDog` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/behaviorChooser/behaviors/3/behaviorID` |
| R063 | `RollBlockOnSide` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/behaviorChooser/behaviors/4/behaviorID` |
| R064 | `StackBlocks` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/behaviorChooser/behaviors/5/behaviorID` |
| R065 | `CubeLiftWorkout` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/behaviorChooser/behaviors/6/behaviorID` |
| R066 | `PopAWheelie` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/behaviorChooser/behaviors/7/behaviorID` |
| R067 | `SparksFireTruckAlarm` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/behaviorChooser/behaviors/8/behaviorID` |
| R068 | `PutDownBlock` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/behaviorChooser/behaviors/9/behaviorID` |
| R069 | `EarnedSparks` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/interludeBehaviorChooser/behaviors/0` |
| R070 | `Needs_SevereLowPlayBored` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/interludeBehaviorChooser/behaviors/1` |
| R071 | `Needs_SevereLowPlayRequest` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/interludeBehaviorChooser/behaviors/2` |
| R072 | `Needs_MildLowRepairRequest` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/interludeBehaviorChooser/behaviors/3` |
| R073 | `Needs_MildLowEnergyRequest` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/interludeBehaviorChooser/behaviors/4` |
| R074 | `Needs_MildLowPlayRequest` | freeplay activity | `PlayAlone` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/interludeBehaviorChooser/behaviors/5` |
| R075 | `DriveOffCharger` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/behaviorChooser/behaviors/0/behaviorID` |
| R076 | `KnockOverCubes` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/behaviorChooser/behaviors/1/behaviorID` |
| R077 | `RollBlockOnSideLowScore` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/behaviorChooser/behaviors/2/behaviorID` |
| R078 | `Bouncer` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/behaviorChooser/behaviors/3/behaviorID` |
| R079 | `RequestCozmoPerforms` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/behaviorChooser/behaviors/4/behaviorID` |
| R080 | `RequestDroneMode` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/behaviorChooser/behaviors/5/behaviorID` |
| R081 | `RequestSpeedTap` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/behaviorChooser/behaviors/6/behaviorID` |
| R082 | `RequestMemoryMatch` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/behaviorChooser/behaviors/7/behaviorID` |
| R083 | `RequestKeepAway` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/behaviorChooser/behaviors/8/behaviorID` |
| R084 | `PutDownBlock` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/behaviorChooser/behaviors/9/behaviorID` |
| R085 | `EarnedSparks` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/interludeBehaviorChooser/behaviors/0` |
| R086 | `Needs_SevereLowPlayBored` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/interludeBehaviorChooser/behaviors/1` |
| R087 | `Needs_SevereLowPlayRequest` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/interludeBehaviorChooser/behaviors/2` |
| R088 | `Needs_MildLowRepairRequest` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/interludeBehaviorChooser/behaviors/3` |
| R089 | `Needs_MildLowEnergyRequest` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/interludeBehaviorChooser/behaviors/4` |
| R090 | `Needs_MildLowPlayRequest` | freeplay activity | `PlayWithHumans` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/interludeBehaviorChooser/behaviors/5` |
| R091 | `Singing_AbaDaba` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/0/behaviorID` |
| R092 | `Singing_BeautifulDreamer` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/1/behaviorID` |
| R093 | `Singing_FrereJacques` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/10/behaviorID` |
| R094 | `Singing_ItsyBitsySpider` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/11/behaviorID` |
| R095 | `Singing_LaPaloma` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/12/behaviorID` |
| R096 | `Singing_LondonBridge` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/13/behaviorID` |
| R097 | `Singing_MaryHadALittleLamb` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/14/behaviorID` |
| R098 | `Singing_MountainKing` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/15/behaviorID` |
| R099 | `Singing_MuffinMan` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/16/behaviorID` |
| R100 | `Singing_MulberryBush` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/17/behaviorID` |
| R101 | `Singing_MussIDenn` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/18/behaviorID` |
| R102 | `Singing_OdeToJoy` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/19/behaviorID` |
| R103 | `Singing_Beethovens5th` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/2/behaviorID` |
| R104 | `Singing_PachebelCanon` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/20/behaviorID` |
| R105 | `Singing_PopGoesTheWeasel` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/21/behaviorID` |
| R106 | `Singing_RowYourBoat` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/22/behaviorID` |
| R107 | `Singing_Sakura` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/23/behaviorID` |
| R108 | `Singing_SilveryMoon` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/24/behaviorID` |
| R109 | `Singing_TakeMeOutToTheBallgame` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/25/behaviorID` |
| R110 | `Singing_TaRaRaBoom` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/26/behaviorID` |
| R111 | `Singing_TisketTasket` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/27/behaviorID` |
| R112 | `Singing_Toccata` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/28/behaviorID` |
| R113 | `Singing_TwinkleTwinkle` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/29/behaviorID` |
| R114 | `Singing_Bingo` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/3/behaviorID` |
| R115 | `Singing_VivaldiSpring` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/30/behaviorID` |
| R116 | `Singing_WaterMusic` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/31/behaviorID` |
| R117 | `Singing_WildAboutHarry` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/32/behaviorID` |
| R118 | `Singing_WilliamTell` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/33/behaviorID` |
| R119 | `Singing_YankeeDoodle` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/34/behaviorID` |
| R120 | `Singing_YellowRose` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/35/behaviorID` |
| R121 | `Singing_BuffaloGals` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/4/behaviorID` |
| R122 | `Singing_CanCan1` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/5/behaviorID` |
| R123 | `Singing_CanCan2` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/6/behaviorID` |
| R124 | `Singing_DannyBoy` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/7/behaviorID` |
| R125 | `Singing_EntryOfTheGladiators` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/8/behaviorID` |
| R126 | `Singing_FarmerInTheDell` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/behaviorChooser/behaviors/9/behaviorID` |
| R127 | `EarnedSparks` | freeplay activity | `Singing` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/interludeBehaviorChooser/behaviors/0` |
| R128 | `DriveOffCharger` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/behaviorChooser/behaviors/0/behaviorID` |
| R129 | `ReactToObstacle` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/behaviorChooser/behaviors/1/behaviorID` |
| R130 | `KnockOverCubes` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/behaviorChooser/behaviors/2/behaviorID` |
| R131 | `RollBlockOnSide` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/behaviorChooser/behaviors/3/behaviorID` |
| R132 | `PutDownBlock` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/behaviorChooser/behaviors/4/behaviorID` |
| R133 | `PounceOnMotion_Socialize` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/behaviorChooser/behaviors/5/behaviorID` |
| R134 | `FPPeekABoo` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/behaviorChooser/behaviors/6/behaviorID` |
| R135 | `EarnedSparks` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/interludeBehaviorChooser/behaviors/0` |
| R136 | `Needs_SevereLowPlayBored` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/interludeBehaviorChooser/behaviors/1` |
| R137 | `Needs_SevereLowPlayRequest` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/interludeBehaviorChooser/behaviors/2` |
| R138 | `Needs_MildLowRepairRequest` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/interludeBehaviorChooser/behaviors/3` |
| R139 | `Needs_MildLowEnergyRequest` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/interludeBehaviorChooser/behaviors/4` |
| R140 | `Needs_MildLowPlayRequest` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/interludeBehaviorChooser/behaviors/5` |
| R141 | `PounceOnMotion_Socialize` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/requiredObjectives/0/behaviorID` |
| R142 | `FPPeekABoo` | freeplay activity | `Socialize` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/requiredObjectives/1/behaviorID` |
| R143 | `DriveOffCharger` | activity/chooser | `PutDownDispatch` | `engine/behaviorSystem/activities/putDownDispatch.json` | `/behaviorChooser/behaviors/0/behaviorID` |
| R144 | `PutDownDispatch_LookForFaceAndCube` | activity/chooser | `PutDownDispatch` | `engine/behaviorSystem/activities/putDownDispatch.json` | `/behaviorChooser/behaviors/1/behaviorID` |
| R145 | `DriveOffCharger` | spark activity | `BuildPyramid` | `engine/behaviorSystem/activities/sparks/sparksBuildPyramid.json` | `/subActivityDelegate/buildChooser/behaviors/0` |
| R146 | `KnockOverCubes` | spark activity | `BuildPyramid` | `engine/behaviorSystem/activities/sparks/sparksBuildPyramid.json` | `/subActivityDelegate/buildChooser/behaviors/1` |
| R147 | `RespondToPyramidBase` | spark activity | `BuildPyramid` | `engine/behaviorSystem/activities/sparks/sparksBuildPyramid.json` | `/subActivityDelegate/buildChooser/behaviors/2` |
| R148 | `SparksPickupSingleCubeForPyramid` | spark activity | `BuildPyramid` | `engine/behaviorSystem/activities/sparks/sparksBuildPyramid.json` | `/subActivityDelegate/buildChooser/behaviors/3` |
| R149 | `SparksLookInPlace` | spark activity | `BuildPyramid` | `engine/behaviorSystem/activities/sparks/sparksBuildPyramid.json` | `/subActivityDelegate/buildChooser/behaviors/4` |
| R150 | `DriveOffCharger` | spark activity | `BuildPyramid` | `engine/behaviorSystem/activities/sparks/sparksBuildPyramid.json` | `/subActivityDelegate/setupChooser/behaviors/0` |
| R151 | `KnockOverCubes` | spark activity | `BuildPyramid` | `engine/behaviorSystem/activities/sparks/sparksBuildPyramid.json` | `/subActivityDelegate/setupChooser/behaviors/1` |
| R152 | `PyramidPutDownBlock` | spark activity | `BuildPyramid` | `engine/behaviorSystem/activities/sparks/sparksBuildPyramid.json` | `/subActivityDelegate/setupChooser/behaviors/2` |
| R153 | `SparksLookInPlace` | spark activity | `BuildPyramid` | `engine/behaviorSystem/activities/sparks/sparksBuildPyramid.json` | `/subActivityDelegate/setupChooser/behaviors/3` |
| R154 | `Singing_AbaDaba` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/0/behaviorID` |
| R155 | `Singing_BeautifulDreamer` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/1/behaviorID` |
| R156 | `Singing_FrereJacques` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/10/behaviorID` |
| R157 | `Singing_ItsyBitsySpider` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/11/behaviorID` |
| R158 | `Singing_LaPaloma` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/12/behaviorID` |
| R159 | `Singing_LondonBridge` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/13/behaviorID` |
| R160 | `Singing_MaryHadALittleLamb` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/14/behaviorID` |
| R161 | `Singing_MountainKing` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/15/behaviorID` |
| R162 | `Singing_MuffinMan` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/16/behaviorID` |
| R163 | `Singing_MulberryBush` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/17/behaviorID` |
| R164 | `Singing_MussIDenn` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/18/behaviorID` |
| R165 | `Singing_OdeToJoy` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/19/behaviorID` |
| R166 | `Singing_Beethovens5th` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/2/behaviorID` |
| R167 | `Singing_PachebelCanon` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/20/behaviorID` |
| R168 | `Singing_PopGoesTheWeasel` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/21/behaviorID` |
| R169 | `Singing_RowYourBoat` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/22/behaviorID` |
| R170 | `Singing_Sakura` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/23/behaviorID` |
| R171 | `Singing_SilveryMoon` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/24/behaviorID` |
| R172 | `Singing_TakeMeOutToTheBallgame` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/25/behaviorID` |
| R173 | `Singing_TaRaRaBoom` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/26/behaviorID` |
| R174 | `Singing_TisketTasket` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/27/behaviorID` |
| R175 | `Singing_Toccata` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/28/behaviorID` |
| R176 | `Singing_TwinkleTwinkle` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/29/behaviorID` |
| R177 | `Singing_Bingo` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/3/behaviorID` |
| R178 | `Singing_VivaldiSpring` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/30/behaviorID` |
| R179 | `Singing_WaterMusic` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/31/behaviorID` |
| R180 | `Singing_WildAboutHarry` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/32/behaviorID` |
| R181 | `Singing_WilliamTell` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/33/behaviorID` |
| R182 | `Singing_YankeeDoodle` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/34/behaviorID` |
| R183 | `Singing_YellowRose` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/35/behaviorID` |
| R184 | `Singing_BuffaloGals` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/4/behaviorID` |
| R185 | `Singing_CanCan1` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/5/behaviorID` |
| R186 | `Singing_CanCan2` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/6/behaviorID` |
| R187 | `Singing_DannyBoy` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/7/behaviorID` |
| R188 | `Singing_EntryOfTheGladiators` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/8/behaviorID` |
| R189 | `Singing_FarmerInTheDell` | spark activity | `Singing` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate/behaviorChooser/behaviors/9/behaviorID` |
| R190 | `DriveOffCharger` | spark activity | `SparksFireTruckAlarm` | `engine/behaviorSystem/activities/sparks/sparksFireTruckAlarm.json` | `/behaviorChooser/behaviors/0` |
| R191 | `SparksFireTruckAlarm` | spark activity | `SparksFireTruckAlarm` | `engine/behaviorSystem/activities/sparks/sparksFireTruckAlarm.json` | `/behaviorChooser/behaviors/1` |
| R192 | `SparksLookInPlace` | spark activity | `SparksFireTruckAlarm` | `engine/behaviorSystem/activities/sparks/sparksFireTruckAlarm.json` | `/behaviorChooser/behaviors/2` |
| R193 | `DriveOffCharger` | spark activity | `SparksFistBump` | `engine/behaviorSystem/activities/sparks/sparksFistBump.json` | `/behaviorChooser/behaviors/0` |
| R194 | `SparksPutDownBlock` | spark activity | `SparksFistBump` | `engine/behaviorSystem/activities/sparks/sparksFistBump.json` | `/behaviorChooser/behaviors/1` |
| R195 | `SparksFistBump` | spark activity | `SparksFistBump` | `engine/behaviorSystem/activities/sparks/sparksFistBump.json` | `/behaviorChooser/behaviors/2` |
| R196 | `DriveOffCharger` | spark activity | `GatherCubes` | `engine/behaviorSystem/activities/sparks/sparksGatherCubes.json` | `/subActivityDelegate/behaviorChooser/behaviors/0` |
| R197 | `KnockOverCubes` | spark activity | `GatherCubes` | `engine/behaviorSystem/activities/sparks/sparksGatherCubes.json` | `/subActivityDelegate/behaviorChooser/behaviors/1` |
| R198 | `Hiking_RollCube` | spark activity | `GatherCubes` | `engine/behaviorSystem/activities/sparks/sparksGatherCubes.json` | `/subActivityDelegate/behaviorChooser/behaviors/2` |
| R199 | `SparksThinkAboutBeacons` | spark activity | `GatherCubes` | `engine/behaviorSystem/activities/sparks/sparksGatherCubes.json` | `/subActivityDelegate/behaviorChooser/behaviors/3` |
| R200 | `SparksBringCubeToBeacon` | spark activity | `GatherCubes` | `engine/behaviorSystem/activities/sparks/sparksGatherCubes.json` | `/subActivityDelegate/behaviorChooser/behaviors/4` |
| R201 | `SparksLookInPlace` | spark activity | `GatherCubes` | `engine/behaviorSystem/activities/sparks/sparksGatherCubes.json` | `/subActivityDelegate/behaviorChooser/behaviors/5` |
| R202 | `DriveOffCharger` | spark activity | `SparksKnockOverCubes` | `engine/behaviorSystem/activities/sparks/sparksKnockOverCubes.json` | `/behaviorChooser/behaviors/0` |
| R203 | `SparksKnockOverCubes` | spark activity | `SparksKnockOverCubes` | `engine/behaviorSystem/activities/sparks/sparksKnockOverCubes.json` | `/behaviorChooser/behaviors/1` |
| R204 | `SparksCheckForStackAtInterval` | spark activity | `SparksKnockOverCubes` | `engine/behaviorSystem/activities/sparks/sparksKnockOverCubes.json` | `/behaviorChooser/behaviors/2` |
| R205 | `SparksPutDownBlock` | spark activity | `SparksKnockOverCubes` | `engine/behaviorSystem/activities/sparks/sparksKnockOverCubes.json` | `/behaviorChooser/behaviors/3` |
| R206 | `SparksLookInPlace` | spark activity | `SparksKnockOverCubes` | `engine/behaviorSystem/activities/sparks/sparksKnockOverCubes.json` | `/behaviorChooser/behaviors/4` |
| R207 | `DriveOffCharger` | spark activity | `SparksPeekABoo` | `engine/behaviorSystem/activities/sparks/sparksPeekABoo.json` | `/behaviorChooser/behaviors/0` |
| R208 | `SparksPutDownBlock` | spark activity | `SparksPeekABoo` | `engine/behaviorSystem/activities/sparks/sparksPeekABoo.json` | `/behaviorChooser/behaviors/1` |
| R209 | `SparksPeekABoo` | spark activity | `SparksPeekABoo` | `engine/behaviorSystem/activities/sparks/sparksPeekABoo.json` | `/behaviorChooser/behaviors/2` |
| R210 | `SparksFindFaces` | spark activity | `SparksPeekABoo` | `engine/behaviorSystem/activities/sparks/sparksPeekABoo.json` | `/behaviorChooser/behaviors/3` |
| R211 | `DriveOffCharger` | spark activity | `SparksPickUpCube` | `engine/behaviorSystem/activities/sparks/sparksPickupCube.json` | `/behaviorChooser/behaviors/0` |
| R212 | `KnockOverCubes` | spark activity | `SparksPickUpCube` | `engine/behaviorSystem/activities/sparks/sparksPickupCube.json` | `/behaviorChooser/behaviors/1` |
| R213 | `SparksPickUpCube` | spark activity | `SparksPickUpCube` | `engine/behaviorSystem/activities/sparks/sparksPickupCube.json` | `/behaviorChooser/behaviors/2` |
| R214 | `SparksVisitPossibleMarker` | spark activity | `SparksPickUpCube` | `engine/behaviorSystem/activities/sparks/sparksPickupCube.json` | `/behaviorChooser/behaviors/3` |
| R215 | `SparksPutDownBlock` | spark activity | `SparksPickUpCube` | `engine/behaviorSystem/activities/sparks/sparksPickupCube.json` | `/behaviorChooser/behaviors/4` |
| R216 | `SparksLookInPlace` | spark activity | `SparksPickUpCube` | `engine/behaviorSystem/activities/sparks/sparksPickupCube.json` | `/behaviorChooser/behaviors/5` |
| R217 | `DriveOffCharger` | spark activity | `SparksPopAWheelie` | `engine/behaviorSystem/activities/sparks/sparksPopAWheelie.json` | `/behaviorChooser/behaviors/0` |
| R218 | `KnockOverCubes` | spark activity | `SparksPopAWheelie` | `engine/behaviorSystem/activities/sparks/sparksPopAWheelie.json` | `/behaviorChooser/behaviors/1` |
| R219 | `SparksPutDownBlock` | spark activity | `SparksPopAWheelie` | `engine/behaviorSystem/activities/sparks/sparksPopAWheelie.json` | `/behaviorChooser/behaviors/2` |
| R220 | `SparksPopAWheelie` | spark activity | `SparksPopAWheelie` | `engine/behaviorSystem/activities/sparks/sparksPopAWheelie.json` | `/behaviorChooser/behaviors/3` |
| R221 | `SparksLookInPlace` | spark activity | `SparksPopAWheelie` | `engine/behaviorSystem/activities/sparks/sparksPopAWheelie.json` | `/behaviorChooser/behaviors/4` |
| R222 | `DriveOffCharger` | spark activity | `SparksPounceOnMotion` | `engine/behaviorSystem/activities/sparks/sparksPounceOnMotion.json` | `/behaviorChooser/behaviors/0` |
| R223 | `SparksPutDownBlock` | spark activity | `SparksPounceOnMotion` | `engine/behaviorSystem/activities/sparks/sparksPounceOnMotion.json` | `/behaviorChooser/behaviors/1` |
| R224 | `SparksPounceOnMotion` | spark activity | `SparksPounceOnMotion` | `engine/behaviorSystem/activities/sparks/sparksPounceOnMotion.json` | `/behaviorChooser/behaviors/2` |
| R225 | `DriveOffCharger` | spark activity | `SparksRollBlock` | `engine/behaviorSystem/activities/sparks/sparksRollBlock.json` | `/behaviorChooser/behaviors/0` |
| R226 | `KnockOverCubes` | spark activity | `SparksRollBlock` | `engine/behaviorSystem/activities/sparks/sparksRollBlock.json` | `/behaviorChooser/behaviors/1` |
| R227 | `SparksRollBlock` | spark activity | `SparksRollBlock` | `engine/behaviorSystem/activities/sparks/sparksRollBlock.json` | `/behaviorChooser/behaviors/2` |
| R228 | `SparksPutDownBlock` | spark activity | `SparksRollBlock` | `engine/behaviorSystem/activities/sparks/sparksRollBlock.json` | `/behaviorChooser/behaviors/3` |
| R229 | `SparksLookInPlace` | spark activity | `SparksRollBlock` | `engine/behaviorSystem/activities/sparks/sparksRollBlock.json` | `/behaviorChooser/behaviors/4` |
| R230 | `DriveOffCharger` | spark activity | `SparksStackBlock` | `engine/behaviorSystem/activities/sparks/sparksStackBlock.json` | `/behaviorChooser/behaviors/0` |
| R231 | `KnockOverCubes` | spark activity | `SparksStackBlock` | `engine/behaviorSystem/activities/sparks/sparksStackBlock.json` | `/behaviorChooser/behaviors/1` |
| R232 | `SparksStackBlock` | spark activity | `SparksStackBlock` | `engine/behaviorSystem/activities/sparks/sparksStackBlock.json` | `/behaviorChooser/behaviors/2` |
| R233 | `SparksPickupSingleCubeToStack` | spark activity | `SparksStackBlock` | `engine/behaviorSystem/activities/sparks/sparksStackBlock.json` | `/behaviorChooser/behaviors/3` |
| R234 | `SparksLookInPlace` | spark activity | `SparksStackBlock` | `engine/behaviorSystem/activities/sparks/sparksStackBlock.json` | `/behaviorChooser/behaviors/4` |
| R235 | `DriveOffCharger` | spark activity | `SparksTrackLaser` | `engine/behaviorSystem/activities/sparks/sparksTrackLaser.json` | `/behaviorChooser/behaviors/0` |
| R236 | `SparksPutDownBlock` | spark activity | `SparksTrackLaser` | `engine/behaviorSystem/activities/sparks/sparksTrackLaser.json` | `/behaviorChooser/behaviors/1` |
| R237 | `SparksTrackLaser` | spark activity | `SparksTrackLaser` | `engine/behaviorSystem/activities/sparks/sparksTrackLaser.json` | `/behaviorChooser/behaviors/2` |
| R238 | `DriveOffCharger` | spark activity | `SparksWorkout` | `engine/behaviorSystem/activities/sparks/sparksWorkout.json` | `/behaviorChooser/behaviors/0` |
| R239 | `KnockOverCubes` | spark activity | `SparksWorkout` | `engine/behaviorSystem/activities/sparks/sparksWorkout.json` | `/behaviorChooser/behaviors/1` |
| R240 | `SparksCubeLiftWorkout` | spark activity | `SparksWorkout` | `engine/behaviorSystem/activities/sparks/sparksWorkout.json` | `/behaviorChooser/behaviors/2` |
| R241 | `SparksLookInPlace` | spark activity | `SparksWorkout` | `engine/behaviorSystem/activities/sparks/sparksWorkout.json` | `/behaviorChooser/behaviors/3` |
| R242 | `DriveOffCharger` | activity/chooser | `MeetCozmo` | `engine/behaviorSystem/activities_config.json` | `/1/behaviorChooser/behaviors/0/behaviorID` |
| R243 | `RespondToRenameFace` | activity/chooser | `MeetCozmo` | `engine/behaviorSystem/activities_config.json` | `/1/behaviorChooser/behaviors/1/behaviorID` |
| R244 | `EnrollFace` | activity/chooser | `MeetCozmo` | `engine/behaviorSystem/activities_config.json` | `/1/behaviorChooser/behaviors/2/behaviorID` |
| R245 | `MeetCozmo_InteractWithFaces` | activity/chooser | `MeetCozmo` | `engine/behaviorSystem/activities_config.json` | `/1/behaviorChooser/behaviors/3/behaviorID` |
| R246 | `MeetCozmo_FindFaces_Socialize` | activity/chooser | `MeetCozmo` | `engine/behaviorSystem/activities_config.json` | `/1/behaviorChooser/behaviors/4/behaviorID` |
| R247 | `PutDownBlock` | activity/chooser | `MeetCozmo` | `engine/behaviorSystem/activities_config.json` | `/1/behaviorChooser/behaviors/5/behaviorID` |
| R248 | `DriveOffCharger` | activity/chooser | `Feeding` | `engine/behaviorSystem/activities_config.json` | `/2/universalChooser/behaviors/0` |
| R249 | `KnockOverCubes` | activity/chooser | `Feeding` | `engine/behaviorSystem/activities_config.json` | `/2/universalChooser/behaviors/1` |
| R250 | `PyramidPutDownBlock` | activity/chooser | `Feeding` | `engine/behaviorSystem/activities_config.json` | `/2/universalChooser/behaviors/2` |
| R251 | `Needs_SevereLowEnergyGetIn` | activity/chooser | `Feeding` | `engine/behaviorSystem/activities_config.json` | `/2/universalChooser/behaviors/3` |
| R252 | `AcknowledgeFace` | reaction | `FacePositionUpdated` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/0/behaviorID` |
| R253 | `FistBump` | reaction | `FistBump` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/1/behaviorID` |
| R254 | `ReactToOnCharger` | reaction | `PlacedOnCharger` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/10/behaviorID` |
| R255 | `ReactToPet` | reaction | `PetInitialDetection` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/11/behaviorID` |
| R256 | `ReactToImpact` | reaction | `RobotFalling` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/12/behaviorID` |
| R257 | `ReactToPickup` | reaction | `RobotPickedUp` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/13/behaviorID` |
| R258 | `ReactToPlacedOnSlope` | reaction | `RobotPlacedOnSlope` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/14/behaviorID` |
| R259 | `ReactToReturnedToTreads` | reaction | `ReturnedToTreads` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/15/behaviorID` |
| R260 | `ReactToRobotOnBack` | reaction | `RobotOnBack` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/16/behaviorID` |
| R261 | `ReactToRobotOnFace` | reaction | `RobotOnFace` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/17/behaviorID` |
| R262 | `ReactToRobotOnSide` | reaction | `RobotOnSide` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/18/behaviorID` |
| R263 | `ReactToRobotShaken` | reaction | `RobotShaken` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/19/behaviorID` |
| R264 | `Hiccup` | reaction | `Hiccup` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/2/behaviorID` |
| R265 | `ReactToSparked` | reaction | `Sparked` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/20/behaviorID` |
| R266 | `ReactToUnexpectedMovement` | reaction | `UnexpectedMovement` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/21/behaviorID` |
| R267 | `AcknowledgeObject` | reaction | `ObjectPositionUpdated` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/3/behaviorID` |
| R268 | `ReactToCliff` | reaction | `CliffDetected` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/4/behaviorID` |
| R269 | `ReactToCubeMoved` | reaction | `CubeMoved` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/5/behaviorID` |
| R270 | `ReactToFrustrationMinor` | reaction | `Frustration` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/6/behaviorID` |
| R271 | `ReactToFrustrationMajor` | reaction | `Frustration` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/7/behaviorID` |
| R272 | `ReactToMotorCalibration` | reaction | `MotorCalibration` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/8/behaviorID` |
| R273 | `RamIntoBlock` | reaction | `NoPreDockPoses` | `engine/behaviorSystem/reactionTrigger_behavior_map.json` | `/reactionTriggerBehaviorMap/9/behaviorID` |

## Activity definitions and links (asset composition only)

Definitions are not collapsed: the same activity ID may be declared inline and in a separate asset. This report does not guess native load/override precedence. Activity ID strings are not miscounted as behavior references, even where the spellings overlap.

| Activity ID | Type | requireSpark input | Config path | JSON pointer |
|---|---|---|---|---|
| `BuildPyramid` | `BuildPyramid` | `None` | `engine/behaviorSystem/activities/freeplay/buildPyramid.json` | `/` |
| `Hiking` | `BehaviorsOnly` | `None` | `engine/behaviorSystem/activities/freeplay/hiking.json` | `/` |
| `NeedsSevereLowEnergy` | `NeedsExpression` | `None` | `engine/behaviorSystem/activities/freeplay/needsSevereLowEnergy.json` | `/` |
| `NeedsSevereLowPlayGetIn` | `NeedsExpression` | `None` | `engine/behaviorSystem/activities/freeplay/needsSevereLowPlayGetIn.json` | `/` |
| `NeedsSevereLowRepair` | `NeedsExpression` | `None` | `engine/behaviorSystem/activities/freeplay/needsSevereLowRepair.json` | `/` |
| `NothingToDo` | `BehaviorsOnly` | `None` | `engine/behaviorSystem/activities/freeplay/nothingToDo.json` | `/` |
| `PlayAlone` | `BehaviorsOnly` | `None` | `engine/behaviorSystem/activities/freeplay/playAlone.json` | `/` |
| `PlayWithHumans` | `BehaviorsOnly` | `None` | `engine/behaviorSystem/activities/freeplay/playWithHumans.json` | `/` |
| `Singing` | `BehaviorsOnly` | `None` | `engine/behaviorSystem/activities/freeplay/singing.json` | `/` |
| `Socialize` | `Socialize` | `None` | `engine/behaviorSystem/activities/freeplay/socialize.json` | `/` |
| `PutDownDispatch` | `BehaviorsOnly` | `None` | `engine/behaviorSystem/activities/putDownDispatch.json` | `/` |
| `SparksBuildPyramid` | `Sparked` | `BuildPyramid` | `engine/behaviorSystem/activities/sparks/sparksBuildPyramid.json` | `/` |
| `BuildPyramid` | `BuildPyramid` | `None` | `engine/behaviorSystem/activities/sparks/sparksBuildPyramid.json` | `/subActivityDelegate` |
| `SparksCozmoSings` | `Sparked` | `CozmoSings` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/` |
| `Singing` | `BehaviorsOnly` | `None` | `engine/behaviorSystem/activities/sparks/sparksCozmoSings.json` | `/subActivityDelegate` |
| `SparksFireTruckAlarm` | `Sparked` | `FireTruckAlarm` | `engine/behaviorSystem/activities/sparks/sparksFireTruckAlarm.json` | `/` |
| `SparksFistBump` | `Sparked` | `FistBump` | `engine/behaviorSystem/activities/sparks/sparksFistBump.json` | `/` |
| `SparksGatherCubes` | `Sparked` | `SparksGatherCubes` | `engine/behaviorSystem/activities/sparks/sparksGatherCubes.json` | `/` |
| `GatherCubes` | `GatherCubes` | `None` | `engine/behaviorSystem/activities/sparks/sparksGatherCubes.json` | `/subActivityDelegate` |
| `SparksKnockOverCubes` | `Sparked` | `KnockOverThreeCubeStack` | `engine/behaviorSystem/activities/sparks/sparksKnockOverCubes.json` | `/` |
| `SparksPeekABoo` | `Sparked` | `PeekABoo` | `engine/behaviorSystem/activities/sparks/sparksPeekABoo.json` | `/` |
| `SparksPickUpCube` | `Sparked` | `PickupCube` | `engine/behaviorSystem/activities/sparks/sparksPickupCube.json` | `/` |
| `SparksPopAWheelie` | `Sparked` | `PopAWheelieAction` | `engine/behaviorSystem/activities/sparks/sparksPopAWheelie.json` | `/` |
| `SparksPounceOnMotion` | `Sparked` | `PounceOnMotionAction` | `engine/behaviorSystem/activities/sparks/sparksPounceOnMotion.json` | `/` |
| `SparksRollBlock` | `Sparked` | `RollCube` | `engine/behaviorSystem/activities/sparks/sparksRollBlock.json` | `/` |
| `SparksStackBlock` | `Sparked` | `StackTwoCubes` | `engine/behaviorSystem/activities/sparks/sparksStackBlock.json` | `/` |
| `SparksTrackLaser` | `Sparked` | `TrackLaser` | `engine/behaviorSystem/activities/sparks/sparksTrackLaser.json` | `/` |
| `SparksWorkout` | `Sparked` | `Workout` | `engine/behaviorSystem/activities/sparks/sparksWorkout.json` | `/` |
| `Selection` | `BehaviorsOnly` | `None` | `engine/behaviorSystem/activities_config.json` | `/0` |
| `MeetCozmo` | `BehaviorsOnly` | `None` | `engine/behaviorSystem/activities_config.json` | `/1` |
| `Feeding` | `Feeding` | `None` | `engine/behaviorSystem/activities_config.json` | `/2` |
| `Freeplay` | `Freeplay` | `None` | `engine/behaviorSystem/activities_config.json` | `/3` |
| `StrictPriorityFreeplay` | `StrictPriority` | `None` | `engine/behaviorSystem/behavior_system_config.json` | `/` |

Explicit parent/subactivity ID references:

| Parent activity | Referenced activity | Config path | JSON pointer |
|---|---|---|---|
| `Freeplay` | `SparksFireTruckAlarm` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/0/activityID` |
| `Freeplay` | `SparksRollBlock` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/1/activityID` |
| `Freeplay` | `SparksStackBlock` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/2/activityID` |
| `Freeplay` | `SparksPeekABoo` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/3/activityID` |
| `Freeplay` | `SparksPounceOnMotion` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/4/activityID` |
| `Freeplay` | `SparksPopAWheelie` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/5/activityID` |
| `Freeplay` | `SparksKnockOverCubes` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/6/activityID` |
| `Freeplay` | `SparksPickUpCube` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/7/activityID` |
| `Freeplay` | `SparksWorkout` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/8/activityID` |
| `Freeplay` | `SparksBuildPyramid` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/9/activityID` |
| `Freeplay` | `SparksFistBump` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/10/activityID` |
| `Freeplay` | `SparksGatherCubes` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/11/activityID` |
| `Freeplay` | `SparksTrackLaser` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/12/activityID` |
| `Freeplay` | `SparksCozmoSings` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/13/activityID` |
| `Freeplay` | `NeedsSevereLowRepair` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/14/activityID` |
| `Freeplay` | `NeedsSevereLowEnergy` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/15/activityID` |
| `Freeplay` | `NeedsSevereLowPlayGetIn` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/16/activityID` |
| `Freeplay` | `PutDownDispatch` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/17/activityID` |
| `Freeplay` | `Socialize` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/18/activityID` |
| `Freeplay` | `Singing` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/19/activityID` |
| `Freeplay` | `PlayWithHumans` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/20/activityID` |
| `Freeplay` | `BuildPyramid` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/21/activityID` |
| `Freeplay` | `PlayAlone` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/22/activityID` |
| `Freeplay` | `Hiking` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/23/activityID` |
| `Freeplay` | `NothingToDo` | `engine/behaviorSystem/activities_config.json` | `/3/subActivities/24/activityID` |
| `StrictPriorityFreeplay` | `Socialize` | `engine/behaviorSystem/behavior_system_config.json` | `/subActivities/0/activityID` |
| `StrictPriorityFreeplay` | `Singing` | `engine/behaviorSystem/behavior_system_config.json` | `/subActivities/1/activityID` |
| `StrictPriorityFreeplay` | `PlayWithHumans` | `engine/behaviorSystem/behavior_system_config.json` | `/subActivities/2/activityID` |
| `StrictPriorityFreeplay` | `BuildPyramid` | `engine/behaviorSystem/behavior_system_config.json` | `/subActivities/3/activityID` |
| `StrictPriorityFreeplay` | `PlayAlone` | `engine/behaviorSystem/behavior_system_config.json` | `/subActivities/4/activityID` |
| `StrictPriorityFreeplay` | `Hiking` | `engine/behaviorSystem/behavior_system_config.json` | `/subActivities/5/activityID` |
| `StrictPriorityFreeplay` | `NothingToDo` | `engine/behaviorSystem/behavior_system_config.json` | `/subActivities/6/activityID` |

## Duplicates and unreferenced definitions

Behavior-ID duplicates: **0**. Paths were enumerated directly; no duplicate ID was silently resolved by this report. Multiple configs sharing a class are separate behavior IDs, not duplicate definitions. Native first-ID-wins loader evidence remains M7-018/pilotS1; no new loader trace was done.

No explicit behavior reference found in the surveyed config corpus for **51 IDs** (this is not a no-effect, exclusion, unreachable, dead-code or unsupported-feature finding):

`BuildPyramid`, `BuildPyramidBase`, `CantHandleTallStack`, `Dance_Mambo`, `DevTurnInPlaceTest`, `DockingTestSimple`, `FactoryCentroidExtractor`, `FactoryTest`, `FeedingEat`, `FeedingFindFacesSevere`, `FeedingPlayRequestAtFace`, `FeedingPlayRequestAtFace_Severe`, `FeedingReactCubeShake`, `FeedingReactCubeShake_Severe`, `FeedingReactFullCube`, `FeedingReactFullCube_Severe`, `FeedingReactSeeCharged`, `FeedingReactSeeCharged_Severe`, `FeedingSearchForCube`, `FindFaces_socialize`, `Hiking_LookInPlace360`, `InteractWithFaces`, `LiftLoadTest`, `Needs_SevereLowEnergyForcedGetOut`, `OnboardingShowCube`, `PlayArbitraryAnim`, `PyramidRespondPossiblyRoll`, `PyramidThankYou`, `ReactToPyramid`, `ReactToStackOfCubes`, `Singing_Camptown`, `Singing_HelloMyBaby`, `Singing_TurkeyInTheStraw`, `VC_AlrightyResponse`, `VC_ComeHere`, `VC_GoToSleep`, `VC_HowAreYouDoing_AllGood`, `VC_HowAreYouDoing_Energy`, `VC_HowAreYouDoing_Play`, `VC_HowAreYouDoing_Repair`, `VC_PounceOnMotion`, `VC_Refuse_Energy`, `VC_Refuse_Repair`, `VC_Refuse_Sparks`, `VC_RequestCozmoPerforms`, `VC_RequestDroneMode`, `VC_RequestKeepAway`, `VC_RequestMemoryMatch`, `VC_RequestSpeedTap`, `VC_SearchForFace`, `Wait`.

Repeated class use (all configs still listed separately):

| Class | Definition count | IDs |
|---|---|---|
| `BringCubeToBeacon` | 2 | `Hiking_BringCubeToBeacon`, `SparksBringCubeToBeacon` |
| `CubeLiftWorkout` | 2 | `CubeLiftWorkout`, `SparksCubeLiftWorkout` |
| `DriveInDesperation` | 2 | `Needs_SevereLowEnergyState`, `Needs_SevereLowRepairState` |
| `DriveOffCharger` | 2 | `DriveOffCharger`, `Hiking_DriveOffCharger` |
| `ExploreLookAroundInPlace` | 2 | `Hiking_LookInPlace360`, `SparksLookInPlace` |
| `ExpressNeeds` | 6 | `Needs_MildLowEnergyRequest`, `Needs_MildLowPlayRequest`, `Needs_MildLowRepairRequest`, `Needs_SevereLowEnergyForcedGetOut`, `Needs_SevereLowPlayBored`, `Needs_SevereLowPlayRequest` |
| `FindFaces` | 4 | `FeedingFindFacesSevere`, `FindFaces_socialize`, `MeetCozmo_FindFaces_Socialize`, `SparksFindFaces` |
| `FistBump` | 2 | `FistBump`, `SparksFistBump` |
| `InteractWithFaces` | 2 | `InteractWithFaces`, `MeetCozmo_InteractWithFaces` |
| `KnockOverCubes` | 2 | `KnockOverCubes`, `SparksKnockOverCubes` |
| `PeekABoo` | 2 | `FPPeekABoo`, `SparksPeekABoo` |
| `PickUpCube` | 2 | `SparksPickupSingleCubeForPyramid`, `SparksPickupSingleCubeToStack` |
| `PlayAnim` | 15 | `FeedingReactCubeShake`, `FeedingReactCubeShake_Severe`, `FeedingReactFullCube`, `FeedingReactFullCube_Severe`, `FeedingReactSeeCharged`, `FeedingReactSeeCharged_Severe`, `Hiccup`, `Hiking_FirstLookIntro`, `Hiking_FirstLookWakeUp`, `NothingToDo_BoredAnim`, `NothingToDo_Idle`, `ReactToObstacle`, `VC_Refuse_Energy`, `VC_Refuse_Repair`, `VC_Refuse_Sparks` |
| `PlayAnimOnNeedsChange` | 3 | `Needs_SevereLowEnergyGetIn`, `Needs_SevereLowPlayGetIn`, `Needs_SevereLowRepairGetIn` |
| `PlayAnimWithFace` | 7 | `FeedingPlayRequestAtFace`, `FeedingPlayRequestAtFace_Severe`, `VC_AlrightyResponse`, `VC_HowAreYouDoing_AllGood`, `VC_HowAreYouDoing_Energy`, `VC_HowAreYouDoing_Play`, `VC_HowAreYouDoing_Repair` |
| `PopAWheelie` | 2 | `PopAWheelie`, `SparksPopAWheelie` |
| `PounceOnMotion` | 4 | `Hiking_PounceOnMotion`, `PounceOnMotion_Socialize`, `SparksPounceOnMotion`, `VC_PounceOnMotion` |
| `PutDownBlock` | 4 | `PutDownBlock`, `PutDownBlockNothingToDo`, `PyramidPutDownBlock`, `SparksPutDownBlock` |
| `ReactToFrustration` | 2 | `ReactToFrustrationMajor`, `ReactToFrustrationMinor` |
| `ReactToOnCharger` | 2 | `ReactToOnCharger`, `VC_GoToSleep` |
| `RequestGameSimple` | 10 | `RequestCozmoPerforms`, `RequestDroneMode`, `RequestKeepAway`, `RequestMemoryMatch`, `RequestSpeedTap`, `VC_RequestCozmoPerforms`, `VC_RequestDroneMode`, `VC_RequestKeepAway`, `VC_RequestMemoryMatch`, `VC_RequestSpeedTap` |
| `RollBlock` | 4 | `Hiking_RollCube`, `RollBlockOnSide`, `RollBlockOnSideLowScore`, `SparksRollBlock` |
| `Singing` | 39 | `Singing_AbaDaba`, `Singing_BeautifulDreamer`, `Singing_Beethovens5th`, `Singing_Bingo`, `Singing_BuffaloGals`, `Singing_Camptown`, `Singing_CanCan1`, `Singing_CanCan2`, `Singing_DannyBoy`, `Singing_EntryOfTheGladiators`, `Singing_FarmerInTheDell`, `Singing_FrereJacques`, `Singing_HelloMyBaby`, `Singing_ItsyBitsySpider`, `Singing_LaPaloma`, `Singing_LondonBridge`, `Singing_MaryHadALittleLamb`, `Singing_MountainKing`, `Singing_MuffinMan`, `Singing_MulberryBush`, `Singing_MussIDenn`, `Singing_OdeToJoy`, `Singing_PachebelCanon`, `Singing_PopGoesTheWeasel`, `Singing_RowYourBoat`, `Singing_Sakura`, `Singing_SilveryMoon`, `Singing_TaRaRaBoom`, `Singing_TakeMeOutToTheBallgame`, `Singing_TisketTasket`, `Singing_Toccata`, `Singing_TurkeyInTheStraw`, `Singing_TwinkleTwinkle`, `Singing_VivaldiSpring`, `Singing_WaterMusic`, `Singing_WildAboutHarry`, `Singing_WilliamTell`, `Singing_YankeeDoodle`, `Singing_YellowRose` |
| `StackBlocks` | 2 | `SparksStackBlock`, `StackBlocks` |
| `ThinkAboutBeacons` | 2 | `Hiking_ThinkAboutBeacons`, `SparksThinkAboutBeacons` |
| `Wait` | 2 | `Needs_Wait`, `Wait` |

## Explicit supported-scope policies — preserve, never silently drop a definition

- AGENTS.md “Scope: the app boundary” (operator2026-10-09): SDK mode is not supported, including external SDK programs and internal CodeLab/Edu SDK mode. This excludes those engine entry paths, **not every behavior that could also be used outside SDK mode**. Keep the complete asset rows; per-behavior exclusion requires actual call/input proof and the recorded decision.
- Same operator app-boundary decision replaces Unity UI/flows, profiles, Android/game socket machinery with the standalone host; engine-to-game events/API and required app input policies remain in scope. An app-sounding class/ID is not evidence of a feature exclusion.
- AGENTS.md operator2026-10-14: robot firmware update unsupported; M1-040 operator2026-10-09 accepts any firmware. No config behavior row is removed on that basis.
- Developer-test configs remain present. M7-022 says DockingTestSimple developer state machine not exhaustively recovered/not on production live path; this does not make the asset nonexistent or the whole ID automatically operator-excluded.
- Audio ADP-1 and M6-023 supported API policy remain authoritative. A Singing class, audioSwitch field or spark reference is not permission to drop audio/control/RNG semantics. Current pilot active-audio STOP remains visible in its handoff.

This report assigns no fidelity classifications, subsystem acceptance or completion. Every supported behavior still needs its whole production path, including shared inputs/RNG, state and API effects; a catalog link or a helper already built is insufficient.

## Current record quotations for this map's limited source index

Literal current title/status/first entire evidence item, reread from the manifest. These are evidence slices, not whole-record completion claims.

```json
{
  "id": "M7-001",
  "title": "The shipped AnimationTrigger, ReactionTrigger, BehaviorClass and BehaviorID enums",
  "status": "EXACT_SOURCE",
  "evidence": [
    "unity/scripts/csharp/Anki.Cozmo/AnimationTrigger.cs"
  ]
}
```

```json
{
  "id": "M7-002",
  "title": "The shipped animation/reaction maps, behaviour configs and activity tree",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/obb/assets/cozmo_resources/assets/animationGroupMaps/AnimationTriggerMap.json (573 pairs)"
  ]
}
```

```json
{
  "id": "M7-018",
  "title": "Shipped behaviour configuration and BehaviorClass factory binding",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "178-config table in M7 inventory Appendix C"
  ]
}
```

```json
{
  "id": "M12-001",
  "title": "Pre-action pose types, their per-type ctor distances and the distance threshold",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "dispatch on actionType 0..5: 0x004E5958 cmp r5,#5 / 0x004E595E tbh / table 0x004E5962"
  ]
}
```

```json
{
  "id": "M12-003",
  "title": "The dock-message order: CheckIfDone -> DockWithObject -> builder -> send",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x005521AC IDockAction::CheckIfDone; 0x005522AE blx 0x4AB9F8 (DockWithObject)"
  ]
}
```

```json
{
  "id": "M12-017",
  "title": "IDockAction::Init setup, handler registration and the docking squint",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005514FC Init; 0x00551598 GetLocatedObjectByIdHelper; 0x00551640 GetPreActionPoses; 0x005516EA movs r3,#0xc5; 0x00551750 movs r3,#0xda"
  ]
}
```

```json
{
  "id": "M12-022",
  "title": "DriveToObjectAction::InitHelper's DriveToPoseAction goal",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005590AE objectPose.GetWithRespectTo(robot.parent, local_5c)"
  ]
}
```

```json
{
  "id": "M12-031",
  "title": "IDockAction::GetPreActionPoses and SelectObject/GetObstacles",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005508C8..0x00551200"
  ]
}
```

```json
{
  "id": "M12-006",
  "title": "Letting go of a carried object leaves it Dirty where the lift left it",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x006333D4 SetCarriedObjectAsUnattached; 0x00633458 AddRobotRelativeObservation; 0x00633930 DeleteLocatedObjects"
  ]
}
```

```json
{
  "id": "M12-016",
  "title": "A roll gets three attempts",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x005B9EF8 StartRollingAction; 0x005B9F0A ldr.w r0,[r4,#0x124]; 0x005B9F0E cmp r0,#3; 0x005B9F10 blo 0x5b9f20; 0x005B9F16 blx 0x4B26AC (MarkTargetAsFailedToRoll)"
  ]
}
```

```json
{
  "id": "M12-028",
  "title": "The behaviour-owned PlaceRelObjectParameters producers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0055E2CA, 0x0055E3E0, 0x0055EB74, 0x0055EC2A, 0x005B58C2 the installers; 0x005C9588, 0x005DC266, 0x005DCE44 the parameter producers"
  ]
}
```

```json
{
  "id": "M13-014",
  "title": "Knock over a stack: BehaviorKnockOverCubes flow, DriveAndFlipBlockAction and IDriveToInteractWithObject",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorKnockOverCubes body 0x005C2EA0..0x005C3E2A; IsRunnableInternal 0x005C314C needs StackOfCubes::GetStackHeight() >= +0x124 (minimumStackHeight default 3); InitInternal 0x005C31A2 runs the reach unless +0xD9 (alwaysStreamline) or +0xD8 (the soft/hard switch flag) is set (0x005C31B4/0x005C31BA)"
  ]
}
```

```json
{
  "id": "M12-049",
  "title": "BehaviorBuildPyramid helper callbacks and the pyramid cache contents (Container::Update, CastNewEntryToBestSharedPointer, PruneFullPyramids, PyramidBase::operator==)",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261016-ov69-74-75-77-check.md: callbacks vtables 0x01029068 / 0x010290A8 (built 0x005DC16C..0x005DC17C, passed at 0x005DC298); cache UpdateAllBlockConfigs 0x00616F00 -> Container::Update 0x0061682C -> AddAllConfigsWithObjectToCache 0x0062C56E -> CastNewEntryToBestSharedPointer 0x0062C68C; PruneFullPyramids 0x0062C72E."
  ]
}
```

```json
{
  "id": "M7-022",
  "title": "DockingTestSimple developer-test state machine is not exhaustively recovered",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "BehaviorDockingTestSimple::UpdateInternal 0x005cbc10..0x005ccc3f"
  ]
}
```


## Validation and reproducibility

Direct asset census was compared with the existing checked M7 inventory Appendix C: **all178 path/ID/class tuples match**, zero added/missing tuples. All273 R-codes are unique and target a defined ID;127 distinct IDs have explicit references,51 have none within the config corpus. No duplicate behavior ID and no byte-identical definition file groups were found. State counts are177 not started /1 in progress (DriveOffCharger). These checks validate this catalog, not native behavior or feature acceptance.

Ignored machine data: `.scratch/shipped-behaviour-map.json` retains every definition path/state/top-level key list, exact273 references,33 activity definitions,32 activity links,299 non-reference same-name collisions and the51-ID no-reference list. No binary/manifest/queue/source/test/git mutation was performed. The literal-name collisions are deliberately not promoted into behavior dependencies.
