# Behaviours

Whole-behavior scoreboard, 2026-10-26. **0 finished · 3 in progress · 175 not started.** These states track complete behavior work, independently of fidelity-record status, old code, helper progress or test counts. Exact shipped IDs `DriveOffCharger` `SparksPickupSingleCubeForPyramid` and `SparksPickupSingleCubeToStack` are in progress; other configurations remain not started.

This is the operator-requested asset-based planning document, checked by the manager against all178 source-map rows. It introduces no feature exclusion, fidelity/status change, new source recovery, implementation or acceptance.

## Sources and interpretation

[Shipped map](../../research/20261026-shipped-behaviour-map.md) contains all178 exact ID/class/path tuples,273 explicit reference locations/R-codes, repeated class use and51 IDs with no reference in the surveyed config corpus. Its178 tuples match the existing checked [M7 inventory Appendix C](../../inventory/M7-behaviour.md). All14 current-record title/status/literal first-evidence quotes in that map were rechecked against the current manifest for this draft; zero mismatches. A map/config reference proves configured input, not runtime admission or full source coverage.

`UNKNOWN` means the complete behavior dependency chain has not been established in this scoreboard. It is not “no dependency,” “unsupported,” or “unnecessary.” Established slices below cite checked inventories/pilot packets; unmentioned body, input, state, API and wire joins remain UNKNOWN. No dependency is inferred from a class name or the generated authority6 classifier.

## Shared driving, docking and pickup planning first

| Shared work to reuse when a real caller is established | Checked source index | Limit |
|---|---|---|
| Drive-to-object/pre-action goal/docking/selection | [M12 inventory](../../inventory/M12-manipulation.md), M12-001/003/017/022/031; current literal quotes in shipped map | This is shared action source, not a claim that every cube-named behavior calls it. Exact per-behavior caller/input joins remain required. |
| Pickup/release and carrying | M12-006; checked action interfaces00553648/0055C464; BehaviorPutDownBlock caller005C84CE | PickUpCube's whole production chain stays UNKNOWN here. |
| Roll helper/attempts | M12-016, RollBlockHelper005B9EF8 | A helper's source is not proof of a same-named behavior's complete call chain. |
| Knock-over/drive-flip | [M13 inventory](../../inventory/M13-navigation.md), M13-014 | Existing explicit BehaviorKnockOverCubes edge; whole behavior still not started. |
| Stack/pyramid placement and caches | M12-028/049, source handoff | Common cache producer does not recover BuildPyramid helper callbacks. |

The first group of IDs below follows the operator's pickup/driving/docking planning priority. That grouping is not an assertion of recovered dependencies. Remaining rows follow the shipped map's class/ID order.

## Shipped behavior IDs

Contexts below are literal asset-derived owners, with the first existing R-code and reference count as a compact locator. The [map's full reference table](../../research/20261026-shipped-behaviour-map.md#exact-activity--freeplay--reaction--spark-behavior-references) retains every file, pointer and ordered chooser position. `activity` labels also cover inline definitions; `spark` and `freeplay` reflect actual asset directories. No references are silently deduplicated in the source map.

| Shipped ID | Class | State | Established subsystem slice; remaining dependencies | Configured contexts / map locator |
|---|---|---|---|---|
| `SparksPickupSingleCubeForPyramid` | `PickUpCube` | in progress | PICKUP-PYRAMID-1 READY; factory/cache/eligibility/manual Verify checked, full6378PASS; concrete native children/activity admission MISSING | freeplay: BuildPyramid; spark: BuildPyramid. 2 refs; R004+ in map |
| `SparksPickupSingleCubeToStack` | `PickUpCube` | in progress | PICKUP-STACK-1 READY; owned visual subtree independently checked, two full6387PASS; other native children MISSING | spark: SparksStackBlock. 1 refs; R233+ in map |
| `PutDownBlock` | `PutDownBlock` | not started | M12 carrying/release: M12-006; rest UNKNOWN | freeplay: Hiking; freeplay: NeedsSevereLowEnergy; freeplay: NeedsSevereLowPlayGetIn; freeplay: NeedsSevereLowRepair; freeplay: PlayAlone; freeplay: PlayWithHumans; freeplay: Socialize; activity: MeetCozmo. 8 refs; R019+ in map |
| `PutDownBlockNothingToDo` | `PutDownBlock` | not started | M12 carrying/release: M12-006; rest UNKNOWN | freeplay: NothingToDo. 1 refs; R050+ in map |
| `PyramidPutDownBlock` | `PutDownBlock` | not started | M12 carrying/release: M12-006; rest UNKNOWN | freeplay: BuildPyramid; spark: BuildPyramid; activity: Feeding. 3 refs; R014+ in map |
| `SparksPutDownBlock` | `PutDownBlock` | not started | M12 carrying/release: M12-006; rest UNKNOWN | spark: SparksFistBump; spark: SparksKnockOverCubes; spark: SparksPeekABoo; spark: SparksPickUpCube; spark: SparksPopAWheelie; spark: SparksPounceOnMotion; spark: SparksRollBlock; spark: SparksTrackLaser. 8 refs; R194+ in map |
| `KnockOverCubes` | `KnockOverCubes` | not started | M13 knock-over/drive-flip: M13-014; full chain UNKNOWN | freeplay: BuildPyramid; freeplay: Hiking; freeplay: NothingToDo; freeplay: PlayAlone; freeplay: PlayWithHumans; freeplay: Socialize; spark: BuildPyramid; spark: GatherCubes; spark: SparksPickUpCube; spark: SparksPopAWheelie; spark: SparksRollBlock; spark: SparksStackBlock; spark: SparksWorkout; activity: Feeding. 16 refs; R002+ in map |
| `SparksKnockOverCubes` | `KnockOverCubes` | not started | M13 knock-over/drive-flip: M13-014; full chain UNKNOWN | spark: SparksKnockOverCubes. 1 refs; R203+ in map |
| `SparksStackBlock` | `StackBlocks` | not started | M12 placement producer M12-028; full chain UNKNOWN | spark: SparksStackBlock. 1 refs; R232+ in map |
| `StackBlocks` | `StackBlocks` | not started | M12 placement producer M12-028; full chain UNKNOWN | freeplay: PlayAlone. 1 refs; R064+ in map |
| `Hiking_RollCube` | `RollBlock` | not started | UNKNOWN | freeplay: Hiking; spark: GatherCubes. 2 refs; R023+ in map |
| `RollBlockOnSide` | `RollBlock` | not started | UNKNOWN | freeplay: PlayAlone; freeplay: Socialize. 2 refs; R063+ in map |
| `RollBlockOnSideLowScore` | `RollBlock` | not started | UNKNOWN | freeplay: PlayWithHumans. 1 refs; R077+ in map |
| `SparksRollBlock` | `RollBlock` | not started | UNKNOWN | spark: SparksRollBlock. 1 refs; R227+ in map |
| `PopAWheelie` | `PopAWheelie` | not started | UNKNOWN | freeplay: PlayAlone. 1 refs; R066+ in map |
| `SparksPopAWheelie` | `PopAWheelie` | not started | UNKNOWN | spark: SparksPopAWheelie. 1 refs; R220+ in map |
| `BuildPyramidBase` | `BuildPyramidBase` | not started | M12 placement producer M12-028; full chain UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `BuildPyramid` | `BuildPyramid` | not started | M12 placement/config callbacks M12-028/049; full chain UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `DockingTestSimple` | `DockingTestSimple` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `AcknowledgeFace` | `AcknowledgeFace` | not started | UNKNOWN | reaction: FacePositionUpdated. 1 refs; R252+ in map |
| `AcknowledgeObject` | `AcknowledgeObject` | not started | UNKNOWN | reaction: ObjectPositionUpdated. 1 refs; R267+ in map |
| `Bouncer` | `Bouncer` | not started | UNKNOWN | freeplay: PlayWithHumans. 1 refs; R078+ in map |
| `Hiking_BringCubeToBeacon` | `BringCubeToBeacon` | not started | UNKNOWN | freeplay: Hiking. 1 refs; R025+ in map |
| `SparksBringCubeToBeacon` | `BringCubeToBeacon` | not started | UNKNOWN | spark: GatherCubes. 1 refs; R200+ in map |
| `CantHandleTallStack` | `CantHandleTallStack` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `SparksCheckForStackAtInterval` | `CheckForStackAtInterval` | not started | UNKNOWN | spark: SparksKnockOverCubes. 1 refs; R204+ in map |
| `CubeLiftWorkout` | `CubeLiftWorkout` | not started | UNKNOWN | freeplay: PlayAlone. 1 refs; R065+ in map |
| `SparksCubeLiftWorkout` | `CubeLiftWorkout` | not started | UNKNOWN | spark: SparksWorkout. 1 refs; R240+ in map |
| `Dance_Mambo` | `Dance` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `DevTurnInPlaceTest` | `DevTurnInPlaceTest` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `Needs_SevereLowEnergyState` | `DriveInDesperation` | not started | UNKNOWN | freeplay: NeedsSevereLowEnergy. 1 refs; R037+ in map |
| `Needs_SevereLowRepairState` | `DriveInDesperation` | not started | UNKNOWN | freeplay: NeedsSevereLowRepair. 1 refs; R045+ in map |
| `DriveOffCharger` | `DriveOffCharger` | in progress | Driving/contact/action; needs/mood/animation; neighboring vision/audio gaps tracked separately | freeplay: BuildPyramid; freeplay: NeedsSevereLowEnergy; freeplay: NeedsSevereLowPlayGetIn; freeplay: NeedsSevereLowRepair; freeplay: NothingToDo; freeplay: PlayAlone; freeplay: PlayWithHumans; freeplay: Socialize; activity: PutDownDispatch; spark: BuildPyramid; spark: SparksFireTruckAlarm; spark: SparksFistBump; spark: GatherCubes; spark: SparksKnockOverCubes; spark: SparksPeekABoo; spark: SparksPickUpCube; spark: SparksPopAWheelie; spark: SparksPounceOnMotion; spark: SparksRollBlock; spark: SparksStackBlock; spark: SparksTrackLaser; spark: SparksWorkout; activity: MeetCozmo; activity: Feeding. 26 refs; R001+ in map |
| `Hiking_DriveOffCharger` | `DriveOffCharger` | not started | Driving/contact/action; needs/mood/animation; neighboring vision/audio gaps tracked separately | freeplay: Hiking. 1 refs; R020+ in map |
| `VC_ComeHere` | `DriveToFace` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `EarnedSparks` | `EarnedSparks` | not started | UNKNOWN | freeplay: BuildPyramid; freeplay: Hiking; freeplay: NothingToDo; freeplay: PlayAlone; freeplay: PlayWithHumans; freeplay: Singing; freeplay: Socialize. 7 refs; R006+ in map |
| `EnrollFace` | `EnrollFace` | not started | UNKNOWN | activity: MeetCozmo. 1 refs; R244+ in map |
| `Hiking_LookInPlace360` | `ExploreLookAroundInPlace` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `SparksLookInPlace` | `ExploreLookAroundInPlace` | not started | UNKNOWN | freeplay: BuildPyramid; spark: BuildPyramid; spark: SparksFireTruckAlarm; spark: GatherCubes; spark: SparksKnockOverCubes; spark: SparksPickUpCube; spark: SparksPopAWheelie; spark: SparksRollBlock; spark: SparksStackBlock; spark: SparksWorkout. 12 refs; R005+ in map |
| `SparksVisitPossibleMarker` | `ExploreVisitPossibleMarker` | not started | UNKNOWN | spark: SparksPickUpCube. 1 refs; R214+ in map |
| `Needs_MildLowEnergyRequest` | `ExpressNeeds` | not started | UNKNOWN | freeplay: BuildPyramid; freeplay: Hiking; freeplay: NothingToDo; freeplay: PlayAlone; freeplay: PlayWithHumans; freeplay: Socialize. 6 refs; R010+ in map |
| `Needs_MildLowPlayRequest` | `ExpressNeeds` | not started | UNKNOWN | freeplay: BuildPyramid; freeplay: Hiking; freeplay: NothingToDo; freeplay: PlayAlone; freeplay: PlayWithHumans; freeplay: Socialize. 6 refs; R011+ in map |
| `Needs_MildLowRepairRequest` | `ExpressNeeds` | not started | UNKNOWN | freeplay: BuildPyramid; freeplay: Hiking; freeplay: NothingToDo; freeplay: PlayAlone; freeplay: PlayWithHumans; freeplay: Socialize. 6 refs; R009+ in map |
| `Needs_SevereLowEnergyForcedGetOut` | `ExpressNeeds` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `Needs_SevereLowPlayBored` | `ExpressNeeds` | not started | UNKNOWN | freeplay: BuildPyramid; freeplay: Hiking; freeplay: NothingToDo; freeplay: PlayAlone; freeplay: PlayWithHumans; freeplay: Socialize. 6 refs; R007+ in map |
| `Needs_SevereLowPlayRequest` | `ExpressNeeds` | not started | UNKNOWN | freeplay: BuildPyramid; freeplay: Hiking; freeplay: NothingToDo; freeplay: PlayAlone; freeplay: PlayWithHumans; freeplay: Socialize. 6 refs; R008+ in map |
| `FactoryCentroidExtractor` | `FactoryCentroidExtractor` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FactoryTest` | `FactoryTest` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FeedingEat` | `FeedingEat` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FeedingSearchForCube` | `FeedingSearchForCube` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FeedingFindFacesSevere` | `FindFaces` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FindFaces_socialize` | `FindFaces` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `MeetCozmo_FindFaces_Socialize` | `FindFaces` | not started | UNKNOWN | activity: MeetCozmo. 1 refs; R246+ in map |
| `SparksFindFaces` | `FindFaces` | not started | UNKNOWN | spark: SparksPeekABoo. 1 refs; R210+ in map |
| `SparksFireTruckAlarm` | `FireTruckAlarm` | not started | UNKNOWN | freeplay: PlayAlone; spark: SparksFireTruckAlarm. 2 refs; R067+ in map |
| `FistBump` | `FistBump` | not started | UNKNOWN | reaction: FistBump. 1 refs; R253+ in map |
| `SparksFistBump` | `FistBump` | not started | UNKNOWN | spark: SparksFistBump. 1 refs; R195+ in map |
| `GuardDog` | `GuardDog` | not started | UNKNOWN | freeplay: PlayAlone. 1 refs; R062+ in map |
| `InteractWithFaces` | `InteractWithFaces` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `MeetCozmo_InteractWithFaces` | `InteractWithFaces` | not started | UNKNOWN | activity: MeetCozmo. 1 refs; R245+ in map |
| `LiftLoadTest` | `LiftLoadTest` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `PutDownDispatch_LookForFaceAndCube` | `LookForFaceAndCube` | not started | M8 NONRUNNING candidate only (C7/C7.1); selected body UNKNOWN | activity: PutDownDispatch. 1 refs; R144+ in map |
| `Hiking_LookInPlaceForUnknown` | `LookInPlaceMemoryMap` | not started | UNKNOWN | freeplay: Hiking. 1 refs; R018+ in map |
| `RespondToPyramidBase` | `OnConfigSeen` | not started | UNKNOWN | freeplay: BuildPyramid; spark: BuildPyramid. 2 refs; R003+ in map |
| `OnboardingShowCube` | `OnboardingShowCube` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FPPeekABoo` | `PeekABoo` | not started | UNKNOWN | freeplay: Socialize. 2 refs; R134+ in map |
| `SparksPeekABoo` | `PeekABoo` | not started | UNKNOWN | spark: SparksPeekABoo. 1 refs; R209+ in map |
| `SparksPickUpCube` | `PickUpAndPutDownCube` | not started | UNKNOWN | spark: SparksPickUpCube. 1 refs; R213+ in map |
| `FeedingReactCubeShake` | `PlayAnim` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FeedingReactCubeShake_Severe` | `PlayAnim` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FeedingReactFullCube` | `PlayAnim` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FeedingReactFullCube_Severe` | `PlayAnim` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FeedingReactSeeCharged` | `PlayAnim` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FeedingReactSeeCharged_Severe` | `PlayAnim` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `Hiccup` | `PlayAnim` | not started | UNKNOWN | reaction: Hiccup. 1 refs; R264+ in map |
| `Hiking_FirstLookIntro` | `PlayAnim` | not started | UNKNOWN | freeplay: Hiking. 1 refs; R021+ in map |
| `Hiking_FirstLookWakeUp` | `PlayAnim` | not started | UNKNOWN | freeplay: Hiking. 1 refs; R022+ in map |
| `NothingToDo_BoredAnim` | `PlayAnim` | not started | UNKNOWN | freeplay: NothingToDo. 1 refs; R052+ in map |
| `NothingToDo_Idle` | `PlayAnim` | not started | UNKNOWN | freeplay: NothingToDo. 1 refs; R051+ in map |
| `ReactToObstacle` | `PlayAnim` | not started | UNKNOWN | freeplay: Hiking; freeplay: NothingToDo; freeplay: PlayAlone; freeplay: Socialize. 4 refs; R016+ in map |
| `VC_Refuse_Energy` | `PlayAnim` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `VC_Refuse_Repair` | `PlayAnim` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `VC_Refuse_Sparks` | `PlayAnim` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `Needs_SevereLowEnergyGetIn` | `PlayAnimOnNeedsChange` | not started | UNKNOWN | freeplay: NeedsSevereLowEnergy; activity: Feeding. 2 refs; R036+ in map |
| `Needs_SevereLowPlayGetIn` | `PlayAnimOnNeedsChange` | not started | UNKNOWN | freeplay: NeedsSevereLowPlayGetIn. 1 refs; R041+ in map |
| `Needs_SevereLowRepairGetIn` | `PlayAnimOnNeedsChange` | not started | UNKNOWN | freeplay: NeedsSevereLowRepair. 1 refs; R044+ in map |
| `FeedingPlayRequestAtFace` | `PlayAnimWithFace` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `FeedingPlayRequestAtFace_Severe` | `PlayAnimWithFace` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `VC_AlrightyResponse` | `PlayAnimWithFace` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `VC_HowAreYouDoing_AllGood` | `PlayAnimWithFace` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `VC_HowAreYouDoing_Energy` | `PlayAnimWithFace` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `VC_HowAreYouDoing_Play` | `PlayAnimWithFace` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `VC_HowAreYouDoing_Repair` | `PlayAnimWithFace` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `PlayArbitraryAnim` | `PlayArbitraryAnim` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `Hiking_PounceOnMotion` | `PounceOnMotion` | not started | UNKNOWN | freeplay: Hiking. 1 refs; R026+ in map |
| `PounceOnMotion_Socialize` | `PounceOnMotion` | not started | UNKNOWN | freeplay: Socialize. 2 refs; R133+ in map |
| `SparksPounceOnMotion` | `PounceOnMotion` | not started | UNKNOWN | spark: SparksPounceOnMotion. 1 refs; R224+ in map |
| `VC_PounceOnMotion` | `PounceOnMotion` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `PyramidThankYou` | `PyramidThankYou` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `RamIntoBlock` | `RamIntoBlock` | not started | UNKNOWN | reaction: NoPreDockPoses. 1 refs; R273+ in map |
| `ReactToCliff` | `ReactToCliff` | not started | UNKNOWN | reaction: CliffDetected. 1 refs; R268+ in map |
| `ReactToCubeMoved` | `ReactToCubeMoved` | not started | UNKNOWN | reaction: CubeMoved. 1 refs; R269+ in map |
| `ReactToFrustrationMajor` | `ReactToFrustration` | not started | UNKNOWN | reaction: Frustration. 1 refs; R271+ in map |
| `ReactToFrustrationMinor` | `ReactToFrustration` | not started | UNKNOWN | reaction: Frustration. 1 refs; R270+ in map |
| `ReactToImpact` | `ReactToImpact` | not started | UNKNOWN | reaction: RobotFalling. 1 refs; R256+ in map |
| `ReactToMotorCalibration` | `ReactToMotorCalibration` | not started | UNKNOWN | reaction: MotorCalibration. 1 refs; R272+ in map |
| `ReactToOnCharger` | `ReactToOnCharger` | not started | UNKNOWN | reaction: PlacedOnCharger. 1 refs; R254+ in map |
| `VC_GoToSleep` | `ReactToOnCharger` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `ReactToPet` | `ReactToPet` | not started | UNKNOWN | reaction: PetInitialDetection. 1 refs; R255+ in map |
| `ReactToPickup` | `ReactToPickup` | not started | UNKNOWN | reaction: RobotPickedUp. 1 refs; R257+ in map |
| `ReactToPlacedOnSlope` | `ReactToPlacedOnSlope` | not started | UNKNOWN | reaction: RobotPlacedOnSlope. 1 refs; R258+ in map |
| `ReactToPyramid` | `ReactToPyramid` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `ReactToReturnedToTreads` | `ReactToReturnedToTreads` | not started | UNKNOWN | reaction: ReturnedToTreads. 1 refs; R259+ in map |
| `ReactToRobotOnBack` | `ReactToRobotOnBack` | not started | UNKNOWN | reaction: RobotOnBack. 1 refs; R260+ in map |
| `ReactToRobotOnFace` | `ReactToRobotOnFace` | not started | UNKNOWN | reaction: RobotOnFace. 1 refs; R261+ in map |
| `ReactToRobotOnSide` | `ReactToRobotOnSide` | not started | UNKNOWN | reaction: RobotOnSide. 1 refs; R262+ in map |
| `ReactToRobotShaken` | `ReactToRobotShaken` | not started | UNKNOWN | reaction: RobotShaken. 1 refs; R263+ in map |
| `ReactToSparked` | `ReactToSparked` | not started | UNKNOWN | reaction: Sparked. 1 refs; R265+ in map |
| `ReactToStackOfCubes` | `ReactToStackOfCubes` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `ReactToUnexpectedMovement` | `ReactToUnexpectedMovement` | not started | UNKNOWN | reaction: UnexpectedMovement. 1 refs; R266+ in map |
| `RequestCozmoPerforms` | `RequestGameSimple` | not started | UNKNOWN | freeplay: PlayWithHumans. 1 refs; R079+ in map |
| `RequestDroneMode` | `RequestGameSimple` | not started | UNKNOWN | freeplay: PlayWithHumans. 1 refs; R080+ in map |
| `RequestKeepAway` | `RequestGameSimple` | not started | UNKNOWN | freeplay: PlayWithHumans. 1 refs; R083+ in map |
| `RequestMemoryMatch` | `RequestGameSimple` | not started | UNKNOWN | freeplay: PlayWithHumans. 1 refs; R082+ in map |
| `RequestSpeedTap` | `RequestGameSimple` | not started | UNKNOWN | freeplay: PlayWithHumans. 1 refs; R081+ in map |
| `VC_RequestCozmoPerforms` | `RequestGameSimple` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `VC_RequestDroneMode` | `RequestGameSimple` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `VC_RequestKeepAway` | `RequestGameSimple` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `VC_RequestMemoryMatch` | `RequestGameSimple` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `VC_RequestSpeedTap` | `RequestGameSimple` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `PyramidRespondPossiblyRoll` | `RespondPossiblyRoll` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `RespondToRenameFace` | `RespondToRenameFace` | not started | UNKNOWN | activity: MeetCozmo. 1 refs; R243+ in map |
| `VC_SearchForFace` | `SearchForFace` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `Singing_AbaDaba` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R091+ in map |
| `Singing_BeautifulDreamer` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R092+ in map |
| `Singing_Beethovens5th` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R103+ in map |
| `Singing_Bingo` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R114+ in map |
| `Singing_BuffaloGals` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R121+ in map |
| `Singing_Camptown` | `Singing` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `Singing_CanCan1` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R122+ in map |
| `Singing_CanCan2` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R123+ in map |
| `Singing_DannyBoy` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R124+ in map |
| `Singing_EntryOfTheGladiators` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R125+ in map |
| `Singing_FarmerInTheDell` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R126+ in map |
| `Singing_FrereJacques` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R093+ in map |
| `Singing_HelloMyBaby` | `Singing` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `Singing_ItsyBitsySpider` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R094+ in map |
| `Singing_LaPaloma` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R095+ in map |
| `Singing_LondonBridge` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R096+ in map |
| `Singing_MaryHadALittleLamb` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R097+ in map |
| `Singing_MountainKing` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R098+ in map |
| `Singing_MuffinMan` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R099+ in map |
| `Singing_MulberryBush` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R100+ in map |
| `Singing_MussIDenn` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R101+ in map |
| `Singing_OdeToJoy` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R102+ in map |
| `Singing_PachebelCanon` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R104+ in map |
| `Singing_PopGoesTheWeasel` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R105+ in map |
| `Singing_RowYourBoat` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R106+ in map |
| `Singing_Sakura` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R107+ in map |
| `Singing_SilveryMoon` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R108+ in map |
| `Singing_TaRaRaBoom` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R110+ in map |
| `Singing_TakeMeOutToTheBallgame` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R109+ in map |
| `Singing_TisketTasket` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R111+ in map |
| `Singing_Toccata` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R112+ in map |
| `Singing_TurkeyInTheStraw` | `Singing` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |
| `Singing_TwinkleTwinkle` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R113+ in map |
| `Singing_VivaldiSpring` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R115+ in map |
| `Singing_WaterMusic` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R116+ in map |
| `Singing_WildAboutHarry` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R117+ in map |
| `Singing_WilliamTell` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R118+ in map |
| `Singing_YankeeDoodle` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R119+ in map |
| `Singing_YellowRose` | `Singing` | not started | UNKNOWN | freeplay: Singing; spark: Singing. 2 refs; R120+ in map |
| `Hiking_ThinkAboutBeacons` | `ThinkAboutBeacons` | not started | UNKNOWN | freeplay: Hiking. 1 refs; R024+ in map |
| `SparksThinkAboutBeacons` | `ThinkAboutBeacons` | not started | UNKNOWN | spark: GatherCubes. 1 refs; R199+ in map |
| `SparksTrackLaser` | `TrackLaser` | not started | UNKNOWN | spark: SparksTrackLaser. 1 refs; R237+ in map |
| `Hiking_VisitInterestingEdge` | `VisitInterestingEdge` | not started | UNKNOWN | freeplay: Hiking. 1 refs; R027+ in map |
| `Needs_Wait` | `Wait` | not started | UNKNOWN | freeplay: NeedsSevereLowEnergy; freeplay: NeedsSevereLowRepair. 2 refs; R038+ in map |
| `Wait` | `Wait` | not started | UNKNOWN | No explicit config reference; native/app use UNKNOWN |

## Enclosing activity contexts — not additional behavior rows

These33 definitions come from the same asset survey; repeated activity IDs/inline definitions remain separate. They are enclosing selection contexts, not fake behavior IDs, completed activities, or proof that a specialized Update/getter is built. The map also preserves32 explicit parent/subactivity links and native load/precedence remains outside this document.

Paths are relative to shipped `config/engine/behaviorSystem/`; `/` means the asset root. `—` means no requireSpark field, not a proven absence of every spark input.

| Activity ID | Type | requireSpark | Asset / exact JSON pointer |
|---|---|---|---|
| `BuildPyramid` | `BuildPyramid` | — | `activities/freeplay/buildPyramid.json` `/` |
| `Hiking` | `BehaviorsOnly` | — | `activities/freeplay/hiking.json` `/` |
| `NeedsSevereLowEnergy` | `NeedsExpression` | — | `activities/freeplay/needsSevereLowEnergy.json` `/` |
| `NeedsSevereLowPlayGetIn` | `NeedsExpression` | — | `activities/freeplay/needsSevereLowPlayGetIn.json` `/` |
| `NeedsSevereLowRepair` | `NeedsExpression` | — | `activities/freeplay/needsSevereLowRepair.json` `/` |
| `NothingToDo` | `BehaviorsOnly` | — | `activities/freeplay/nothingToDo.json` `/` |
| `PlayAlone` | `BehaviorsOnly` | — | `activities/freeplay/playAlone.json` `/` |
| `PlayWithHumans` | `BehaviorsOnly` | — | `activities/freeplay/playWithHumans.json` `/` |
| `Singing` | `BehaviorsOnly` | — | `activities/freeplay/singing.json` `/` |
| `Socialize` | `Socialize` | — | `activities/freeplay/socialize.json` `/` |
| `PutDownDispatch` | `BehaviorsOnly` | — | `activities/putDownDispatch.json` `/` |
| `SparksBuildPyramid` | `Sparked` | `BuildPyramid` | `activities/sparks/sparksBuildPyramid.json` `/` |
| `BuildPyramid` | `BuildPyramid` | — | `activities/sparks/sparksBuildPyramid.json` `/subActivityDelegate` |
| `SparksCozmoSings` | `Sparked` | `CozmoSings` | `activities/sparks/sparksCozmoSings.json` `/` |
| `Singing` | `BehaviorsOnly` | — | `activities/sparks/sparksCozmoSings.json` `/subActivityDelegate` |
| `SparksFireTruckAlarm` | `Sparked` | `FireTruckAlarm` | `activities/sparks/sparksFireTruckAlarm.json` `/` |
| `SparksFistBump` | `Sparked` | `FistBump` | `activities/sparks/sparksFistBump.json` `/` |
| `SparksGatherCubes` | `Sparked` | `SparksGatherCubes` | `activities/sparks/sparksGatherCubes.json` `/` |
| `GatherCubes` | `GatherCubes` | — | `activities/sparks/sparksGatherCubes.json` `/subActivityDelegate` |
| `SparksKnockOverCubes` | `Sparked` | `KnockOverThreeCubeStack` | `activities/sparks/sparksKnockOverCubes.json` `/` |
| `SparksPeekABoo` | `Sparked` | `PeekABoo` | `activities/sparks/sparksPeekABoo.json` `/` |
| `SparksPickUpCube` | `Sparked` | `PickupCube` | `activities/sparks/sparksPickupCube.json` `/` |
| `SparksPopAWheelie` | `Sparked` | `PopAWheelieAction` | `activities/sparks/sparksPopAWheelie.json` `/` |
| `SparksPounceOnMotion` | `Sparked` | `PounceOnMotionAction` | `activities/sparks/sparksPounceOnMotion.json` `/` |
| `SparksRollBlock` | `Sparked` | `RollCube` | `activities/sparks/sparksRollBlock.json` `/` |
| `SparksStackBlock` | `Sparked` | `StackTwoCubes` | `activities/sparks/sparksStackBlock.json` `/` |
| `SparksTrackLaser` | `Sparked` | `TrackLaser` | `activities/sparks/sparksTrackLaser.json` `/` |
| `SparksWorkout` | `Sparked` | `Workout` | `activities/sparks/sparksWorkout.json` `/` |
| `Selection` | `BehaviorsOnly` | — | `activities_config.json` `/0` |
| `MeetCozmo` | `BehaviorsOnly` | — | `activities_config.json` `/1` |
| `Feeding` | `Feeding` | — | `activities_config.json` `/2` |
| `Freeplay` | `Freeplay` | — | `activities_config.json` `/3` |
| `StrictPriorityFreeplay` | `StrictPriority` | — | `behavior_system_config.json` `/` |

## Current pilot

- [Drive-off handoff](HANDOFF-DRIVE-OFF-PILOT.md): externally MERGED on main4fb2908b; five own records settled by Claude. M12-043/M8-027 remain open; no new hardware acceptance/finished-behavior claim.
- Operator scope: what changes the behavior's own observable result while it runs. Neighboring audio/ROI/other-activity MISSING paths are leftovers, not reasons to hold it. No outward pilot-2 reading job.
- D1 configuration APIs and D2 controlled-clock whiteboard remain isolated standalone extensions; no status or support decision changes here.

## Supported-scope policies

[AGENTS.md](../../../AGENTS.md) remains authoritative: operator2026-10-09 replaces app/Unity/Android/socket machinery while preserving engine API/output/state and named host inputs; SDK mode (including CodeLab/Edu) is not supported. Operator2026-10-14 excludes firmware update; M1-040 accepts firmware per2026-10-09 decision. These exclude named entry paths, not every behavior ID that could share one. All178 asset rows remain, including developer tests and the51 IDs without an explicit config reference. No class/ID is excluded merely for its name, folder or current lack of a caller.

ADP-1 changes only the approved per-sample DSP arithmetic boundary; other audio behavior/RNG/timing remains exact. “No effect” still needs source evidence; “not supported” needs the applicable recorded decision. This scoreboard creates neither.
