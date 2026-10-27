# Drive-off full-gate 1: bounded read/classification and D2 design — 2026-10-26

Read-only findings/design requested by manager/root for the completed full gate:42failed/6312passed/0skipped (6354total). Failure sources `.scratch/driveoff-pilot-full-gate-1-failures.json` and `.scratch/test-full.log`; integrated source reported d3d373ab. No tests, source/test/manifest/queue edits, hardware, git operations, full Face extraction or repository-wide audit. This new research file is the only write in this task. Actual model/effort telemetry unavailable.

B independently agrees the manager/AI/Spark ownership classifications and independently **PASSed the concrete D2 design**, not implementation. Subsequent affected diff/production/test verification is still required. Parent manages implementation release. Operator D2 is answered: preserve controlled-clock standalone capability; no policy question remains pending.

**Current D1/D2 contract clarification (supersedes the original overbroad no-World-callback proposal):** manual construction retains the two existing HOST-ONLY `SetObjectPoseChanged` and `BlockConfigurationManagerUpdate` single slots for the public configuration extension. Native publication runs first; host extension runs second. Cleanup clears only its still-matching installed delegate; no fanout, prior-callback restoration stack or native-owner write. The precise current contract below matches B's build-check clarification at “D2/D1 clarification.” D2 remains an operator-approved standalone extension, not native engine/hardware equivalence.

## Current records before relying on scopes

Literal current title/status/evidence entries below. These do not upgrade statuses or settle whole records. Source-specific constraints in the following sections reuse the checked pilot/framework/M15 packets; only the bounded Face distance/straight caller was freshly inspected, after known.py on005DAA20 and005DA950. Existing main-pose/storedForward source and scope remain the checked pilotG2/G3 rows, not new firmware evidence.

```json
{
  "id": "M13-013",
  "title": "DriveOffChargerContactsAction: a drive straight that retries while still on the charger",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "DriveOffChargerContactsAction ctor 0x00558228: DriveStraightAction(10 mm, 20 mm/s, false) (0x00558232/0x00558236/0x0055823E); then +0x44 = 7 (0x00558276/0x00558278), which is IActionRunner's RobotActionType (stored at +0x44 by the IActionRunner ctor 0x0053FDCC) and 7 = DRIVE_OFF_CHARGER_CONTACTS (RobotActionTypeFromString 0x0075A448 maps the string at 0x00C1604E to 7)"
  ]
}
```

```json
{
  "id": "M12-032",
  "title": "PathComponent drive-to-pose status machine: fields, SetDriveToPoseStatus, predicates, StartDrivingToPose, Update/UpdatePlanning/HandlePlanComplete, ExecutePath, the 0xC6 PathFollowingEvent handler, the RobotState segment and doling",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0064941C, 0x0064937C, 0x00649390, 0x0064B370, 0x0064B61C, 0x006493B4, 0x006494CC"
  ]
}
```

```json
{
  "id": "M8-011",
  "title": "The Smart* scope helpers: idle animation, motion profile, track locks, custom light patterns, helper delegation and the reaction locks",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "SmartPushIdleAnimation 0x005be41c: if the idle-animation flag +0xb0 is already set it fails through sVerifyFailedReturnFalse (0x005be448 b.w 0x8cbe5c); otherwise AnimationStreamer::PushIdleAnimation(trigger, name) and +0xb0 = 1 (0x005be464..0x005be478).",
    "SmartRemoveIdleAnimation 0x005bd4c8: when +0xb0 is clear it raises VERIFY(%s): Behavior %s is trying to remove an idle, but none is currently set through sVerifyFailedReturnFalse (cbz r0,#0x5bd508 0x005bd4d4; string 0x005bd51e; b.w 0x8cbe5c 0x005bd526); otherwise AnimationStreamer::RemoveIdleAnimation(name) and +0xb0 = 0 (0x005bd4ec/0x005bd500).",
    "SmartSetMotionProfile 0x005be518 verifies +0xc0 == 0 (sVerifyFailedReturnFalse 0x005be52a), calls PathComponent::SetCustomMotionProfile (0x005be534) and sets +0xc0 = 1. SmartClearMotionProfile 0x005bd584 verifies +0xc0 != 0, calls PathComponent::ClearCustomMotionProfile (0x005bd59c) and clears +0xc0.",
    "SmartLockTracks 0x005be5bc emplaces (name -> mask) into the map at +0xb4; on a new key it calls MovementComponent::LockTracks(mask, name, reason) and returns 1; on an existing key it warns \"Attempted to lock tracks with key named %s but key already exists\" and returns 0 (0x005be600/0x005be624/0x005be636). SmartUnLockTracks 0x005be6e0 looks the name up, calls MovementComponent::UnlockTracks(mask, name), erases the entry and returns 1, or warns and returns 0 (0x005be6ee..0x005be716).",
    "SmartSetCustomLightPattern 0x005be7f0: if the ObjectID is already in the vector at +0xcc it logs on the \"Unnamed\" channel and returns 0; otherwise CubeLightComponent::PlayLightAnim(objectID, trigger, callback, true, lights, timeout) and appends the ObjectID to the vector at +0xd0, returning 1 (0x005be806..0x005be8cc).",
    "SmartRemoveCustomLightPattern 0x005be9b0 searches the vector at +0xcc for the element whose +4 equals objectID+4; not found logs \"No custom light pattern is set for object %d\" and returns 0 (0x005bea4e/0x005bea74); found calls CubeLightComponent::StopLightAnimAndResumePrevious(trigger, objectID) for each trigger and erases the element, returning 1 (0x005be9e8/0x005bea34).",
    "SmartDelegateToHelper 0x005beb10 logs on the \"Behaviors\" channel, warns and calls StopHelperWithoutCallback() when the current helper +0xc8 is already live (0x005beba4/0x005bebe8), then calls BehaviorHelperComponent::DelegateToHelper on [robot+0x264]+0x10 (0x005bec2c); on success it stores the helper as a weak ref at +0xc4/+0xc8 through __add_weak at 0x005bec6e, otherwise logs \"SmartDelegateToHelper.Failed\" (0x005bec8c/0x005becb2).",
    "SmartDisableReactionsWithLock 0x005bce3c appends \"_behaviorLock\" to the name, calls BehaviorManager::DisableReactionsWithLock(manager, name+\"_behaviorLock\", table, true) (0x005bce62) and inserts the original name into the per-behaviour set at +0xa4 (0x005bce7e). SmartRemoveDisableReactionsLock 0x005bd470 appends the same suffix, calls BehaviorManager::RemoveDisableReactionsLock (0x005bd48c) and erases the name from +0xa4 (0x005bd4a4). The manager side is M7-014.",
    "SmartDelegateToHelper 0x005beb10 calls BehaviorHelperComponent::DelegateToHelper, whose body is 0x0056dad8..0x0056db37 (PLT 0x004b29d0) at AIComponent+0x10 ([robot+0x264]+0x10, AIComponent constructed at 0x00569aae/0x00569ab2): it clears stack-maintenance vars, copies the two callbacks to +0x10/+0x28, and on an empty helper stack pushes the helper and stores Robot::GetWorldOriginID at +0x40, returning 1. R-BEH C2 read the component and its helper-stack runtime in full (report re-analysis/research/20260929-R-BEH-M8-framework-gap1-extraction.md); the component and the M7/M15 helper subclasses are still unbuilt, so the record stays IMPLEMENTATION_GAP."
  ]
}
```

```json
{
  "id": "M8-012",
  "title": "BehaviorManager::Update's tick order, behaviour switching, finishing and the reaction gate",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorManager::Update 0x005a2f70 returns immediately when the manager's init byte at +0 is zero, logging \"BehaviorManager.Update.NotInitialized\" (0x005a2f70/0x005a2f72/0x005a2fea)."
  ]
}
```

```json
{
  "id": "M7-014",
  "title": "Reaction-lock manager lifetime and concrete per-class lock tables",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::SmartDisableReactionsWithLock 0x005bce3c"
  ]
}
```

```json
{
  "id": "M3-032",
  "title": "The NV dispatch gates in Robot::Update: Gate A (a first full state after SyncTimeAck), the SyncTime watchdog and Gate B (calibration and UpdateAllResults)",
  "status": "EXACT_SOURCE",
  "evidence": [
    "Engine-state part, owned by M1-024 (state 3 Running, the UI return); the watchdog is this record's: CozmoEngine::Update returns early unless engine+0x10 != 0 and UiMessageHandler::Update == 0 (0x4ED4DE..0x4ED51C); UpdateAllRobots runs only in state 3 Running (0x4ED5C4..0x4ED6BE); SyncTime watchdog 5 s (0x513BF6..0x513C5A)"
  ]
}
```

```json
{
  "id": "M15-019",
  "title": "FindFreePoseInBeacon: the candidate frame, grid, order and the three tests",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorExploreBringCubeToBeacon::FindFreePoseInBeacon 0x005E0378..0x005E0666 (static: r0 object, r1 beacon, r2 robot, r3 &out, float on the stack; the rest of 0x005E0378..0x005E139F is helpers and lambdas); frame = rotation about Z from the nearest cube in the beacon (CalculateDirectionalityClosest 0x005E07D8), else the robot-to-beacon heading via acosf, else angle 0"
  ]
}
```

```json
{
  "id": "M15-020",
  "title": "CalculateDirectionalityClosest, the FindCubesInBeacon predicate and AIBeacon::IsLocWithinBeacon's formula",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "CalculateDirectionalityClosest 0x005E07D8: the rotation about Z from the nearest cube in the beacon; AIBeacon::IsLocWithinBeacon 0x0059C24C: x*x + y*y + z*z in f32 against (radius - margin)^2 plus 0x3727C5AC, `ge`; used only by FindCubesInBeacon (with extra 0)"
  ]
}
```

```json
{
  "id": "M15-023",
  "title": "The NoFreePoses branch: FailedToFindLocationInBeacon stamp, the log, the 'HikingNoLocationAtBeacon' event and the IsRunnable cooldown",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005DF69C..0x005DF73A: AIBeacon::FailedToFindLocation (0x0059C314..0x0059C322) stamps [beacon+0x10] (an f32 time) with BaseStationTimer::GetCurrentTimeInSeconds(); then the 'all poses failed' log, then the mood event 'HikingNoLocationAtBeacon' (24 chars), no action started; IsRunnableInternal (vldr s16,[r0,#0x10] 0x005DF148) then blocks for recentFailureCooldown_sec (45.0f hiking, 5.0f sparks)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ]
}
```

```json
{
  "id": "M15-024",
  "title": "The whiteboard failure memory: tables, caps, FailureInfo and EntryMatches",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "AIWhiteboard::DidFailToUse / SetFailedToUse with a pose: per-object-id tables with caps {1, 1, 10, 1} (PlaceObjectAt keeps the last 10); EntryMatches expiry is strict with 1e-5f slack; id -1 means any object; the floor path's DidFailToUse is 0x005E0B78 (failure 2, id -1, 100.0f, pi) and the stack-on filter's is 0x005E1D24 (failure 1, 20.0f, pi/8)",
    "Pose3d::IsSameAs_WithAmbiguity 0x00846F3C (the per-axis and angle test) is unread: RECOVERABLE_GAP",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ]
}
```

```json
{
  "id": "M15-057",
  "title": "BehaviorManagerMessage handler (G2E 0x96): ActivateSpark, SparkUnlocked, DoATrickRequest (RequestATrick, cost 3, AddInventoryAmount), PlayAGameRequest, bad-tag error",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov65-67-check.md: subscription 0x005A15AC..0x005A15CE, adapter 0x005A5136..0x005A514A; dispatch 0x005A3C70..0x005A3C78; case 0 0x005A3C80..0x005A3C9A; case 1 0x005A3DB4..0x005A3DC6; case 2 0x005A3CA0..0x005A3D5A (cost table 0x00C91424, 0x0077E0CC..0x0077E0DE); RequestATrick 0x0056E6D0..0x0056E838; case 3 0x005A3DCA..0x005A3DD6; bad tag 0x005A3D5C..0x005A3DAE. App side: MessageGameToEngine.cs:163, BehaviorManagerMessageUnion.cs:8-15, ActivateSpark.cs:68, Robot.cs:1670-1698."
  ]
}
```

```json
{
  "id": "M15-058",
  "title": "BehaviorManager spark fields: init, SetRequestedSpark, SwitchToRequestedSpark, high-level-activity adapter clear",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov65-67-check.md: field init 0x005A08DA..0x005A0900; setter 0x005A3EAC..0x005A3F0E (\"BehaviorManager.SetRequestedSpark\" \"requested %s spark is '%s'\"); switch 0x005A4198..0x005A422C (Info first; +0x60 not cleared); adapter 0x005A4FA2..0x005A4FE0 (SetCurrentActivity body 0x005A106C unread, overlaps M15-031); writers ctor, 0x005A3C9A, 0x005A3EB2/0x005A3EB6, 0x005A4220/0x005A4224/0x005A4226, 0x005A4FDC/0x005A4FDE."
  ]
}
```

## Face regression: coordinate conflation; physical oracle still UNKNOWN

Failure `FaceTests.DriveToFaceDrivesUntilTwoHundredMillimetresAway`, FaceTests.cs522: expected physical `rig.X` in390..410, actual379.999695. Actual supplied input is fake detector face7 at(600,0,250), default main robot pose0, no carried object; test creates a standalone DriveToFaceBehavior, performs turn→visual verify→turn, measures face distance, runs plain DriveStraight then tracks. It is not the configured DriveOff behavior or physical firmware.

Actual C# path: FaceTests.RunToEnd→DriveToFaceBehavior.OnStart (FaceBehaviors.cs341+)→face/head pose and latest RobotPose planar distance→DriveStraightAction(distance−200,speed60)→ActionList→DriveStraight.Init(DriveActions.cs995+)→Motion.Path.ExecuteCustomPath→wireClear/AppendLine/Execute→ManipRig.FollowPath380+→current segment/RobotState/C6→action completion→face tracking/Idle→assertion. ManipRig400 directly writes `X=ln.XEndMm;Y=ln.YEndMm`, then State182 emits those as RobotState MAINpose. This is the exact coordinate conflation causing the displayed20mm shift; it is not evidence of a physical robot stopping20mm short.

| Bounded primary claim | Instructions | Meaning / limit |
|---|---|---|
| Face distance reference |005DAA20..AA9C gets face viaRobot+38, copies face pose+F4;AA58/5A callsRobot.GetPose004EA398, uses itsZ AA62/68 in face copy;AA7A/7C gets MAINpose,AA86 ComputeDistanceBetween | For the admitted common-origin fixture this measures planar face distance relative to main robot pose. It does not switch the distance reference to storedForwardPose. Full arbitrary FaceWorld/transform/admission path is not recovered by these callsites. |
| Requested straight distance |005DA994..99E callsCalculateDistance;A9A8 loads literal005DAA18=-200.0;A9AE/B2 performs f32distance+(-200);A9B6 passesr2 | This is distance-minus200, not a compensating20mm correction. |
| Actual caller speed/deceleration (separate existing mismatch) |A9BA r3=0;A9C0 MOVT435C=>435C0000=220;A9BE stackbool1;A9C8 DriveStraight ctor;A9CC/D2 forms4326AAAB=166.6666717529;A9D6 SetDecel | Current C# caller/test use60, while its166.667 constant is unused and current Straight line emits500. These are pre-existing caller/input discrepancies, not consequences of the storedForward correction. Do not silently “fix” them during a broader Face implementation or claim currentFace whole source-exact. Owning Face production unit needs manager scope if absent. |
| Source straight endpoint |Previously checked0054762A/3C: main yaw separately from storedRobot+2A4 ForwardXY; acceptedSetPose0051508C..515138 reads actualRobot284/rawID8, chooses−20 forFFFFFFFF, literal+0 otherwise;MoveRobotPoseForward00517EC0 | Initial mainX0/noncarry gives storedForwardX−20. A roughly400mm requestedline therefore sends roughly−20→380. Store timing matters: no recomputation from current carry at Init, no second pose owner. |
| Fixture consequence |ManipRig400 lineendpoint→X, State182 X→RobotPose MAIN | It reports wireForwardendpoint380 as main380. Old endpoint400 expectations implicitly relied on main==wireframe. Actual379.999695 is consistent with that substitution plus existing fake face projection precision. Do not adopt this float as a native golden bit pattern. |

**Initial bounded disposition (history; later tests-only wire-characterization narrowing below governs the usable fixture claim):** test/harness coordinate-contract defect identified; real physical-position regression unproven. Keep physical390..410 assertion/meaning intact, and do not lower it to380, revert sourceStraight, add a blind+20, backsolve state from sent endpoint, or guess firmware geometry. An independently supported plant/state-report contract is required for the physical assertion. Source permits an independent wire assertion (actualstoredForward start, requested f32 displacement/profile, exact ordering), but that cannot replace the physical obligation. Engine main/forward offset alone is not firmware reference-point proof. No whole Face behavior expansion was done or authorized by this classification.

## R2FloorPlacement: all seven failures share a split-clock API regression

New public `ManipulationSystem(robot,vision)` aliases actual `NativeBehaviorRuntime.Manipulation.Whiteboard` (ManipulationSystem21/28), whose readonly `_clockSec` closure captures the canonical manipulation owner. Native runtime37 binds that owner to `Engine.Timer.SecondsF`. A public facade's writable `ClockSec` is a different property; changing it now changes Bring behavior time but not shared whiteboard time. The property's current documentation explicitly promises component stamps/tests-injected clock. This is a valid standalone capability regression, not permission to rewrite expectedtimestamps to0.

Actual producers/consumers: AIWhiteboard.SetFailedToUse290 stamps `(float)_clockSec()`; EntryMatches355 reads that same closure; FailedToFindLocationInBeacon395 stamps it. BringCubeToBeaconBehavior1175 uses `M.ClockSec()` for cooldown;1212 compares `M.ClockSec()` against beacon stamp to determine InitFailed when no action starts. With customMclock500 and canonicalWBclock0, a same-call NoFreePoses stamp0 becomes an apparent failed Init. Native source M15-023 requires the same BaseStationTimer domain, not two clocks. M15-024 expiry/caps and M15-019 grid geometry remain unchanged.

| Failing case / line | Expected vs actual / diagnosed effect |
|---|---|
|M15_019 strict radius/failure sphere231|Afterclock45, expected first candidate accepted /onevisited; actual4visited because failure never expires inWBclock. Do not weaken sphere/radius assertions.|
|M15_024 caps301|Expected retained entry'sTimeSec1; actual0. Cap count/pose eviction checks pass before this assertion; this is a clock-stamp failure, not evidence caps changed.|
|M15_024 expiry334|Expected false atexact45-secondexpiry, actualtrue; facadeclock advances,WBclockdoesnot.|
|M15_023 cooldown406|Expectedfailurestamp1000, actual0.|
|M15_023/025 NoFreePoses451|Expectedfailurestamp500, actual0; preserve log/event/no-action/carrying expectations.|
|M15_020 candidate filter828|Expectedrunnable after44.9→45expiry, actualfalse; same staleWBclock. Keep20mm/pi8/failurekind filter checks.|
|M15_020 carriedInit1091|ExpectedInitFailedfalse after same-callNoFreePoses, actualtrue; behaviorclock500 minusWBstamp0 failssame-clock test.|

Operator D2 explicitly resolves preservation via an isolated controlled-clock standalone extension. Native timers/whiteboard remain canonical. Correct fixtures choose the explicit manual owner and retain every old controlled value, strict boundary, cap, state/result and output expectation. Merely calling these tests stale, setting canonicalclock through a facade, substituting0, relaxing expiry, or discarding the public setter is not acceptable.

## D2 concrete construction / ownership contract (B design PASS)

Proposed exact API selection: `ManipulationSystem.CreateStandalone(robot,vision,clockSec)` creates the controlled-clock manual facade. Required input is an actual borrowable native service owner; no replacement Docking/Carrying/Path owner is constructed as a convenience. If that actual owner is unavailable, fail clearly at construction/robot-backed access instead of creating a hidden native owner or pretending native readiness. No absent-owner fallback is certified here. Existing direct `new AIWhiteboard(world,clock)` remains a component-level manual input.

| Mode | Docking/Carrying, Paths/Follower | Whiteboard / clock | Global source hooks |
|---|---|---|---|
|Canonical native owner (internal source construction)|Construct/own actual services once; dispose only at actual owner retirement|Own nativeWB; source engine clock binding|Own/install actual world/device/path producers once, with identity-safe detach|
|Borrowed native view (existing public facade)|Borrow canonical services, do not dispose them|Borrow canonicalWB; ClockSec getter reads canonical time; setter must fail clearly and direct callers toCreateStandalone, not silently diverge|No new native owner/replacement; preserve existing D1hostextension single-slot registration/identity cleanup; no fanout|
|Explicit standalone/manual D2|Borrow actualDocking/Carrying AND canonicalPaths/Follower|OWN NEW privateWB over same read-only queryworld; closure `()=>ClockSec()` dynamically reads thismanualfacade clock; sameclock used by manualbehaviors, stamps, expiry/caps/beacons|No source World.MapClearQuad/native writers/device/nativeWB/force/clock takeover. Retain the two existing HOST-ONLY configuration slots with last-registration semantics and exact identity cleanup|

Ownership fields/flags must distinguish `ownsWhiteboard` from `ownsNativeServices`/`installsNativeHooks`; also explicit path/follower ownership if not included in native-services mode. Do not let “owns privateWB” activate the old `_ownsSharedServices` block. A mode/booleans may implement this; exact storage names are not source behavior.

**Constructor hook inventory / does it matter:**

- Existing Whiteboard.Init (AIWhiteboard115) calls three external subscribers only when `ExternalInterface` is nonnull; current freshWB defaultnull calls onlyWarn. Thus manual creation need not install live native44/45/35 recipients. It may keep the null/manualInit diagnostic path, but must not set/borrow actualnativeexternal to duplicate subscriptions. Interface returns no unsubscribehandle; a laterexplicitcustomExternalInterface/Init remains caller-owned, not an implicit D2live binding. Native subscriptions remain untouched. This is a manual extension boundary, not exclusion of native handlers.
- Existing World.MapClearQuad assignment atManipulationSystem29 is a single source callback; privateWB ownership must NOT assign/restore/clear it. Manual clients can explicitly call their privateWB operations. Existingnative source handler remains the same reference before/after construction, clock mutation and disposal.
- **Current accepted clarification:** existing `World.SetObjectPoseChanged` / `BlockConfigurationManagerUpdate` are HOST-ONLY slots, distinct from `World.NativeConfigurations`. Manual mode retains assignment to its private public configuration adapter so `ConfigurationSeen`/`FirstSeen` continue receiving frame inputs on its controlled clock. Personally compared BlockWorld2449→2450 (native SetObjectPoseChanged, then host slot) and1611→1612 (native Update, then host slot) with B's build-check clarification. Preserve existing single-slot/last-registration behavior. Retain each installed delegate and clear only if the slot still equals that delegate; later replacements survive. Do not add multicast fanout or a previous-callback restoration stack. `Docking.Configurations=Configurations` on BORROWED actualDocking remains forbidden. Historical first proposal prohibited both host slots; that wording was overbroad and is superseded, not an outstanding implementation requirement.
- Existing native-worldUnsetCarrying/CarriedPrimary and five device delegates (Motion carrying/place, Sensors unattach, Cubes moved exclusion, Lights carried) belong only to canonical source service owner. Do not install duplicateOnFrame/OnMessage/Carrying hooks through `new DockingSystem` in manual construction. Borrow actualDocking for queries/explicit commands.
- **Important additional concrete source-writer hazard:** PathSender constructor RobotPath105..108 assigns `robot.Motion.Path.SendPathMessage=Send`; PathFollower193 subscribes `robot.MessageInternal`. Recreating these for a manual facade replaces a native producer/adds a handler. Borrow canonicalPaths/Follower and do not dispose them from manual/native-view retirement. Only canonical source owner constructs/retires their native binding. A helper counter or duplicate callback does not prove equivalent ownership.
- Docking.Log currently has an anonymous facade-capturing relay; retain that delegate and unsubscribe on the facade's idempotentDispose even whenDockingborrowed, so the actual owner does not retain retired manual handles. Existing builder lifetime correction already addresses this; D2 must preserve it.
- PrivateWB hooks `IsCarryingObject`, `CarriedObjectId`, `CanPickUpObject` bind to BORROWED actualDocking/Carrying queries. This preserves R2 predicate inputs without a second source carry owner. Shared world/robot observations are borrowed inputs, not copied/default fabricated state.

**Disposal:** retire only manual-owned whiteboard state (beacons, failure entries/owned pose copies and any private handler/event references) and manual facade-owned resources/retained log relay. For the two HOST-ONLY configuration slots, clear only callbacks still identical to this facade's own retained installed delegates; preserve later replacements, restore no prior delegate and introduce no fanout. The design installs no native producer/global-owner slots. Do not clear/dispose sharednativeWB, sharedDocking, canonicalPaths/Follower, world, origin poses, manager, force-update state or nativeclock. Do not reuse `_ownsSharedServices` to clear a sharedboard or to leave a privateboard unretired. Existing legacy failure-pose cleanup details are host storage mechanics; release owned copied wrappers safely without adding state changes during the active lifetime or broad pose re-recovery. Private/public snapshots must not be silently mutated while still supported by their documented lifetime.

**Fixture/observable obligations:** R2unitfixtures explicitly requestmanual construction with their controlledclock (or assign the writableClockSec of thatmanualmode); allseven old expectedvalues500/1000/1, exact45expiry, cap/order, radius/sphere/angle/failurekind, logs/events, no-action/carrying and InitFailedfalse remain. Native source-style fixtures continue using actualengine inputtime and actualcanonicalboard. Construct two manual owners to establish isolation; mutate oneclock/privatebeacon/failurememory, verify the other and canonical clock/WB/service references unchanged. Dispose eithermanual/view, verify native callbacks,sharedcarry/docking/path/follower/manager continue valid. This is D2extension behavior, never a claim customtime equalsnativeBaseStationTimer.

B independently PASSed this concrete design; implementation/diff/test execution is still PENDING. No code was changed by this report.

## M8 helper tick failures: wrong test owner, preserve meaningful gate/order assertions

Fourtests fail: M8_011_TheEngineTickRunsTheHelperStackBeforeTheActionListAndTheStreamer860 (expected2calls,actual1); TheGatesAroundTheHelperTick887 (1vs0); NothingRunsBeforeTheFirstFullState915 (1vs0); FreeplayStackWiresTheAIComponentIntoTheEngineTick957 (2vs1).

Actual EngineRobot.Update1311 invokes `NativeBehaviorRuntime.UpdateAI` whenever canonicalruntime exists; internal `Engine.AIComponentUpdate` is now fallback only. UpdateAI101 invokes actualAI.Update, whose order is Analyzer→Whiteboard no-op→SevereNeeds→Helpers (AIComponent64+). Existing tests instead registerTestHelper on `new AIComponent` or customFreeplay.Create.AI and overwrite the fallback hook; the pushed helper's immediateupdate happens once, but nativeengine correctly ticks a DIFFERENT actualcomponent. They therefore observe wrongowner, not a proved missingnativehelper tick.

Production source contract remains Robot.Update first-full-state gate00513C5C..62, calibratedvisiongate00513C76..9A, AIUpdate00513EAC beforeActionList005140BC and streamer0051410C; helperdelegation targets [Robot264]+10, not anyhostAI instance. Fixture correction: registerProbe/TestHelper on actualNativeBehaviorRuntime.AI.Helpers and recordAI phase in thathelperUpdateInternal; preserve expectedtwo calls and ordering/gates. For fullconfiguredcomposition useCreateNative + actualresource-readyowner/context. StandaloneAI/helper tests explicitly call their manualAI.Update and retain their separate input/lifetime contract; don't makeEngine.Update tick arbitrarymanualAI or reintroduce globalowner replacement just for thesefixtures. Their legitimate standalone control API is not excluded.

## M7 manager lifetime failures: distinguish canonical observation from manual Create

RealReactionEntriesAndLocksSurviveTheFreeplayHandle38 and AnEmptyStartupMapDoesNotSaveLocksForLaterConfiguration78 fail `Same(nativeManager,stack.Manager)` because current customFreeplayStack.Create creates `new BehaviorManager(ctx,...bindSensorOwner:false)`. Actual native owner was constructed earlier inCozmoRobot.BindNativeBehaviorManager199..207, stores native.BehaviorManagerOwner and NativeBehaviorRuntime, and owns reaction entries/severe locks. CreateNative observes those exact canonical instances; independent manualCreate must not hijack them.

For native lock/strategy lifetime fixture, useCreateNative and retain same manager/strategy/reaction entries/lock across handle disposal; actualRobot removal/reconstruction must still retire/recreate native owner. For empty-start/later-map unit, explicitly invoke existing `manager.InitializeRobotReactions(resources,vision)`85 on SAME native manager; this routine clears its initial map, installs the actual loaded entries and does not replay locks from an empty map. This is an explicit unit/configuration input, not proof that customCreate auto-loads/replaces native composition or that whole live late-reconfiguration API is recovered. Do not weaken same-owner/lock/retirement assertions or silently copy states between managers.

## Spark input failure: assertion reads independent manager, not actual96 recipient

SparkManagerInputCheckedRowsTests.LiveAppApiQueuesFullWidthIDAndDoesNotPretendToSelectActivity50 expects RequestedSparkId=int.MinValue on customstack.Manager; actual85(default0x55). Test already confirms exactlyone global96subscriber. Actual CozmoRobot205 attaches input only on actualnative manager whenHasExternalInterface; custommanager does not attachit. FreeplayStack.ActivateSpark327 posts the appmessage then globalAnimationMessages.Deliver96; canonical recipient's setter storesfullrequestedID/soft before optional nameInfo. Reading unrelatedmanualmanager85 is not proof of droppedpayload.

Preserve app-input API and exact native M15-057/058 semantics: assert actualcanonicalmanager RequestedSparkId=int.MinValue, RequestedSoft0, ActiveIdunchanged55 and no fake selection/ack. Continue cancel55→latch1 and laternoncancel retaininglatch. Inspect raw/report diagnostics on actualhandler; don't add asecond globalnative recipient, sendtoindependentmanager, normalizeIDs, attach fakeActivitySparked or eliminate legitimate standalone directmanager tests. Source subscription005A15AC..CE/adapter005A5136..14A, dispatch005A3C70..9A, setter005A3EAC..F0E are existingcheckedcontracts, not newly traced here.

## Handoff verdict

D2privatecontrolledclock behavior is approved operator extension with an independently checked source/ownership design; exactsource does not permit a borrowed facade to mutate native clock. Other reviewed group fixes are specific wrongowner/implicitfixture corrections, not justification to delete expectations. Native production reach, valid manual custominputs, lifetime isolation and source-specific values all remain required. Parent/B must verify the affected builder diff/tests; neither this classification nor a prior whole-read PASS makes the gate green. No new fullsuite was run and no remaining failure was excluded.


### D2 independent-check clarification: current accepted D1 host-slot contract

B's precise clarification in `20261026-driveoff-pilot-build-codex-check.md` supersedes both the original broad no-World-callback proposal and the subsequent explicit-Update-only/fanout caveat. Those two earlier proposals are retained here as history only; neither is the current contract.

The private BlockConfigurationManager constructor storesworld/clock. Its existing automatic pending/frame-tail inputs are the two HOST-ONLY `World.SetObjectPoseChanged` / `BlockConfigurationManagerUpdate` slots, separate from `NativeConfigurations`. **Manual SHOULD retain those two registrations**, preserving the complete D1 `ConfigurationSeen`/`FirstSeen` frame-driven extension semantics, alongside still-usable explicit Update. Existing native-first ordering is2449→2450 and1611→1612. Assignment remains single-slot/last-registration; each facade retains its exact installed delegates and clears them on disposal ONLY while still matching. Preserve later replacements; do not restore prior callbacks, create a restoration stack, or add multicast/fanout. A newer live manual registration supersedes the earlier host registration under existing semantics; disposing an older facade cannot clear it, and disposing the newer one does not resurrect a retired predecessor.

Native `MapClearQuad` / `UnsetCarryingObjects` / `CarriedPrimaryId`, device/AI/vision callbacks, actual docking/path/whiteboard owners, native force flags/cache/clock remain untouched. Host extension state cannot feed native decisions. This clarification was independently checked by B and accepted by root; it is a host API/lifetime design correction, not new native behavior, feature exclusion, physical proof or implementation PASS.

### Later Face oracle narrowing (history preserved)

The initial Face analysis above correctly identifies the fixture's wire-forward-endpoint→main-pose conflation and does not prove physical firmware geometry. B's later build-check summary narrows the usable regression test to an explicitly labelled **tests-only wire characterization** from independent prestate, while retaining the native Face220-speed/deceleration contradictions as visible gaps. No guessed+20 firmware conversion, replacement physical380 grade, or claim of whole Face behavior recovery is authorized. This narrows what the offline fixture can establish; it does not turn the earlier unknown physical oracle into native/hardware evidence.

### Extension provenance after reconciliation

D1 public configuration APIs and D2 controlled-clock manual ownership are approved standalone extensions. Their controlled-clock values, host callback order/lifetime and isolation are API requirements; they do not claim that custom clocks, host per-arrangement first-seen/cache semantics or manual initialization match native engine/hardware behavior. Native clock/state/source paths retain their separately cited contracts. This documentation reconciliation changed no source, tests, statuses, queue or other report file.

