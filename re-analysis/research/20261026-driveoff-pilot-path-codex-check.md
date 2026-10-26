# Whole drive-off pilot: current C# path and evidence reconciliation

READ-ONLY path ledger, 2026-10-26, active HEAD8a4ca625 after origin/main25107ead rebase. This is NOT the independent whole-native read PASS; that verdict follows the extractor graph in a separate report. PROJECT_STATE, AGENTS, PLAN-BEHAVIOURS, AUTONOMY and REVIEW read. R1 MERGED supersedes FIX. CM13-05 draft is paused and is not current implementation. Actual model/reasoning telemetry is unobservable.

## Current whole production route

1. Startup resources/host behavior map are loaded by CozmoEngine.LoadHostBehaviorMap (CozmoEngine.cs2186). The native BehaviorFactory constructor dictionary (BehaviorContainer.cs282) does NOT construct DriveOffCharger from JSON. FreeplayStack.Create loads supported factory classes then adds ShippedBehaviors.Navigation; that list manually constructs DriveOffCharger60 and Hiking45 (Behaviors.cs576–577), binding direct animation push/remove. This supplies usable objects but leaves the drive-off config/base-input/native admission path to reconcile. Base IsRunnableBase/InitLifecycle still report missing robot34A/base74/unlock70/spark inputs and several virtual gates; the drive-off-specific gate correctly consults platform, not contacts.
2. Native robot lifetime creates BehaviorManager and SevereNeeds bindings in CozmoRobot.BindNativeBehaviorManager/BindNativeSevereNeeds. Reaction scope takes the DriveOff table; shared severe selected3 admits the drive-off97/93/8F push and flag, otherwise existing severe animations remain. Update waits off-treads, stops current action and redrives while platform remains; it stamps float last-drive-off only after platform false and no current action. No behavior-owned5s timeout exists.
3. TransitionToDrivingForward calls parameterless StartActing handle plus ActionBehavior.RunAction host closure (ChargerBehaviors.cs107–116). The source concrete IActionRunner admission bridge is visibly M8-015 unbound. RunAction retains full fault/cancel diagnostics and Post/tick/stale suppression (R1); host-thread journal correction remains merged. This stack-owned error handling does not prove native callback timing/admission.
4. The closure creates plain DriveStraightAction, NOT DriveOffChargerContactsAction and NOT DriveToPoseAction. Current constructor defaults speed100/animationtrue; Init creates a line with host double pose arithmetic/default profile, then NativePathComponent.ExecuteCustomPath. RunAsync queues the ActionRunner NOW into native Robot.ActionList, with30s timeout and ActionList cancellation. CheckIfDone uses real native path flags, starts driving animation once, waits for active traversal/end animation and returns Success; destruction aborts active path then destroys handler actions. Two-argument source defaults/line geometry and defined failure handling require final native reconciliation. Current M13-013 title names the OTHER contacts action, while its unresolved owns these plain DriveStraight slices; do not settle the whole titled record from one slice.
5. Custom path clears prior path, increments pathID, doles AppendPathSegmentLine, then ExecutePath and engine-time status store. PathFollowingEvent routes into the same PathComponent under ActionList gate; matching start/completion/interrupt updates received IDs/status. ActionList tick invokes action completion; host Post later enters drive-off callback. This route does NOT select lattice, so CM13-01/03/04 lattice ownership/import/origin changes are not direct drive-off execution requirements. No planner job is justified merely by shared ManipulationSystem ownership.
6. DrivingAnimationHandler selects top pushed triplet, creates TriggerLiftSafeAnimationAction in parallel through ActionList, handles real CA/CB/D5/5F callbacks and tag state, then unlocks/relocks tracks and ends. Trigger group selection still calls AnimationLibrary.GetAnimationNameFromGroup: SimpleMood is absent/default and cooldown is Engine.Timer rather than verified Mood+130. Trigger/action completion string/DAS/cached-pointer lifecycle diagnostics remain; their observable parts must be distinguished from memory-only mechanics. Animation output/streaming and robot callback payloads stay their existing exact/shared records, not assumed closed by the handler unit.
7. Received frame-matched/synchronized RobotState writes contact and off-treads state. SetOnCharger(true) observes a located current-origin Known charger on EVERY true before edge/store; pose changes invalidate actionable cache and notify world/map/state/light in order. EngineRobot.Update runs robot map update then platform quad intersection using actual charger/robot footprint, then AI/ActionList/streamer. Platform false changes threshold/pause/event state and ultimately permits behavior completion. M4-019 threshold50/400 behavior has hardware evidence but is not proof of every upstream/downstream record.
8. Success callback alone emits objective4 through ReportBehaviorObjective: external-interface gate, enqueue E2G8B u32LE then synchronous local emit; diagnostics follow. Local specialized M15-059 objective recipients remain visibly unbuilt. Narrow relevance/filter must be established for objective4 before recovering unrelated ActivitySparked behavior. Next is Mood event DriveOffCharger. The helper uses Context.Mood and Clock()/1000; current native EngineRobot.Update has no Mood.Advance call, while FreeplaySystem.Tick does. FreeplayStack attaches actual native MoodOwner and clock only when that host stack exists. Whole robot-lifetime mood init/update/trigger/history/API and animation SimpleMood/cache clock are therefore not established by manual drive-off success.
9. OnStop removes its pushed driving entry when flag set; base Stop then stops action and scope restoration is manager/host scope disposal. Reaction restoration, cancellation/destruction and treads interruptions are part of the supported path, not only nominal success. Assess owner mechanisms by effects on traffic/results/API/state; exact pointer retain counts or allocation identity alone are not a new job. Immutable stable ID snapshots/GC can implement memory-only mechanics when consumer/value/lifetime analysis establishes equal observable results.

## Configuration producer: concrete conditional dependency

For the requested FULL contact-ingress pilot, configuration production is necessary conditionally, not as a motor/planner call. Changed contact observation -> SetPose/SetObjectPoseChanged pending -> successful observed-marker frame tail -> existing configuration gate -> native containers/tallest -> BehaviorPublicState.UpdateStackHeight -> BOTH BehaviorAudioClient recipients. HandleWorldEventUpdates (BehaviorAudioClient.cs70–77) skips Activity2; otherwise a stacked boolean transition posts eventEC3490EC or3BA32BC6 via robot audio slot8. This changes robot audio output1/downstream state4 for supported cube scenes. The existing hardware run's empty/unchanged cube state cannot validate that producer. Under a deliberately pre-existing-platform-only seam this dependency would not follow, but that is not the requested whole ingress graph.

CM13-05 source reading is reusable; paused code is not verified/current and must be reconsidered inside the ONE whole pilot. Actual query/geometry/processed-ID/order/tallest and stable snapshot content matter. Old native shared-pointer identity/retain counts require a concrete observable consumer before demanding exact replication. M12-049's separate BuildPyramid callbacks are not automatically included. Preserve source/native cache versus existing public host ConfigurationSeen/FirstSeen adapter boundaries; do not silently certify host all-pairs as native state.

## Hardware reconciliation

Latest bundle20261026-072854-M13-DRIVEOFF-HW-310c47 ran olda7af18cd, before several merged fixes. It contains67 unique MISSING texts/70 occurrences. Exact observed texts/counts are recorded below. Hardware checks passed six sourced expectations, but do not establish whole loader/admission/mood/API completion.

Current hardware helper M13DriveOffHw.cs584–619 manually obtains ShippedBehaviors.Navigation(m) DriveOffCharger, creates Context with only Robot/Triggers (NO Mood or ClockSec), starts/ticks itself every33ms with30s host limit, and uses a separate BehaviorScope. Behavior Clock remains its Environment.TickCount64 fallback. Thus its mood callback logs no-mood, last-drive-off uses host clock era, and it bypasses config factory/native behavior selection. This is a reach limitation and an actual manual-composition binding concern, not evidence that native mood/event time has no effect.

Historical MISSING classifications:

- DIRECT/TRANSITIVE gaps still requiring reconciliation: generic runnable/spark gates, M8-015 host-action admission, plain DriveStraight geometry/defaults, concrete trigger action lifecycle, group SimpleMood/Mood+130, M13-038 native public-state input joins, Vision UpdateAllResults result gate, and live contact/shared-world/cached-pose ownership where it changes state. M7-026 group selection remains reached even though the explicit drive-off triplet bypasses DrivingHandler's no-entry SimpleMood branch.
- NOW SUPERSEDED specific diagnostics: old map-image clock input was built B-M11-22; objective trace-only was replaced CM13-02 by raw/local delivery. Their current wider records may still have other residuals. Do not manufacture duplicate jobs from the old bundle.
- DIAGNOSTIC/INTERNAL leads: PathSegment.Print/DrawPath/Viz/SDK status text are not by themselves build blockers under AGENTS; SDK paths are excluded, ordinary Viz portions need their existing no-effect evidence. M8-027 current unresolved says all signal callable destruction is memory-only and nothing behavioral remains to build; confirm current code cleanup then it is a whole-record settlement candidate, not an allocation-counter job. Pose names/timing profiler formatting need an observable effect before any implementation demand.
- NOT EVIDENCE OF DRIVE-OFF REACH: startup LiftLoadTest, unrelated FistBump/Hiccup/Frustration/NoPreDockPoses/Sparked bindings, stats/album/Lab/cube-light/teardown-specific messages. They stay their records; absence of direct drive-off use is not a blanket no-effect claim. Narrow relevance must be established before adding them to the pilot.

## Record completion/settlement reconciliation

The literal current record quotations follow. This ledger offers candidates and precise partial boundaries; it does not change status or promise a whole-record PASS before independent native graph review.

- Whole behavior M13-017: NOT complete today: config factory/common base/admission plus action/animation/mood/clock/API/dependency closure remain. Existing distance/platform/wait/redrive/stamp/push/remove slices have checked source/hardware regressions; unresolved contains stale historical fragments that need exact residual reconciliation in the final handoff.
- M13-040: checked charger observation/identity/ordered effects are built, but normal complete shared configuration/map/pose effects and unavailable-input handling must be accounted before whole-record settlement. M13-013: plain DriveStraight slice is in pilot; contacts-action-specific residuals cannot be silently dismissed/settled.
- M12-032/M12-002: custom-path/doler/feedback slice is reached; planner/root/drive-center/replan and other path types remain precise leftovers if not needed by plain DriveStraight. No whole-record upgrade from the custom route alone.
- M5-063/M8-020/M8-021/M7-026/M7-012/013/020/023: driving-group, concrete animation and mood chains cross each other. Whole evidence/code comparison must establish the relevant units and retain unrelated remaining states/actions; unit passes do not settle the whole animation subsystem.
- M7-018/M8-001/M8-015/M7-014/M7-019/M15-059: behavior config/base/start/locks/objective delivery ownership must be checked through actual producers/recipients. M15-059 starts with objective4 recipient relevance rather than the entire Sparked module.
- M15-072/073 severe/wakeup shared units are built and source-checked by prior jobs; candidate whole-record confirmation belongs to final read/code review. Do not repeat completed reading based on old freeplay-only reaction-lock warnings.
- M13-038/039/M11-037/M12-049/M12-043: conditional contact->configuration->audio closure is real; broad record titles still include nonpilot recipients/geometry/callbacks. Keep exact leftovers. Pointer identity tests are not automatically necessary under the new observable-work standard.
- Existing EXACT_SOURCE transport/state/threshold/E2G signal records are reused at their approved boundary. The pilot must not downgrade them because their unresolved histories contain old prose, nor treat their status as evidence for higher-layer handlers.

No source/tests/git/manifest/REVIEW/Claude edits, no robot or test execution. Only authorized research report/scratch. Pending extractor's WHOLE native graph and independent read verdict; one pilot build follows only that full PASS.

## Literal current manifest record inventory

### M13-017

```json
{
  "title": "BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ctor 0x005C0980 stores 96.0 + json extraDistanceToDrive_mm at +0x11C (0x005C09D4/0x005C09E2/0x005C09E6); IsRunnableInternal 0x005C0B10 returns robot+0x34A (the on-charger-platform flag; contacts are +0x338)",
    "InitInternal 0x005C0B18 takes the reaction lock, pushes driving animations when the animation state is 3 (0x005C0B4A) and transitions only when robot+0x355 == 0 (0x005C0B54); +0x355 is the robot's OffTreadsState (OnTreads = 0), written only by Robot::CheckAndUpdateTreadsState 0x00512088/0x0051208E and named by OffTreadsStateFromString 0x0078DF58 (OnTreads 0, InAir 1, OnBack 2, OnLeftSide 3, OnRightSide 4, OnFace 5, Falling 6)",
    "TransitionToDrivingForward 0x005C0BB8 drives +0x11C (0x005C0C02/0x005C0C08)",
    "UpdateInternal 0x005C0DA8: once off the platform (robot+0x34A == 0) it records the time at robot->[+0x264]->[+0x18]+0x44 and returns 2 (0x005C0DF4..0x005C0E0A); while still on the platform and robot+0x355 != 0 it calls StopActing(false,false) (0x005C0DB6/0x005C0DC4) and waits; on the platform with +0x355 == 0 it transitions to driving forward (0x005C0E18)"
  ]
}
```

### M13-013

```json
{
  "title": "DriveOffChargerContactsAction: a drive straight that retries while still on the charger",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "DriveOffChargerContactsAction ctor 0x00558228: DriveStraightAction(10 mm, 20 mm/s, false) (0x00558232/0x00558236/0x0055823E); then +0x44 = 7 (0x00558276/0x00558278), which is IActionRunner's RobotActionType (stored at +0x44 by the IActionRunner ctor 0x0053FDCC) and 7 = DRIVE_OFF_CHARGER_CONTACTS (RobotActionTypeFromString 0x0075A448 maps the string at 0x00C1604E to 7)",
    "in SDK mode only, the constructor clears the required-track lock: CozmoContext::IsInSdkMode 0x0055827C, cmp #1 0x00558280, then IActionRunner::SetTracksToLock(0) 0x00558286/0x00558288; SetTracksToLock 0x00540918 writes the argument to IActionRunner+0x54 only when the action state (+0x18) is 0x2000001 (not started), else warns; it is a local pre-run flag consumed by IActionRunner::Update 0x00540438 (MovementComponent::AreAnyTracksLocked), not a robot message",
    "Init 0x005582D0 copies robot+0x338 into action+0x8B (0x005582D2/0x005582D6) and returns 0 when not on contacts",
    "CheckIfDone 0x005582E4 retries while the drive is still running and fails 0x04000009 if still on the contacts (0x00558344)"
  ]
}
```

### M13-040

```json
{
  "title": "Contact-true charger observation: live ingress, located identity, Known pose and ordered world/contact effects",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "RobotState sync gate 0x0051293C..942, contact bit12 0x00512AAC/AAE and SetOnCharger call 0x00512AB4; SetOnCharger 0x00511990..0x00511C2D observes on every true input before old-contact gate 0x00511BA8/BB0. Contacts +0x338; platform +0x34A.",
    "ChargerDockPose 0x004EA304..0x004EA35A: Z rotation pi bits 0x40490FDB, translation (30,0,0), robot pose parent and ChargerDockPose name. Located family4/type13 first/current-origin query 0x00511A1C..A52, 0x0061EBF2..0x0061EC26; factory and connected identity/unique SetID 0x00511AD6..0x00511B40, 0x004EF468..0x004EF528.",
    "Known observation 0x00511B4A -> 0x00506B70..0x00506C48: reparent/root flatten, existing/new branch, SetPoseHelper 0x0050609C, distance -1, confirmer count1/reference/existing timestamp. Actionable cache invalidation 0x004E01E4..226; common SetPose 0x004EF590..646. AddLocatedObject 0x00622AC0..EB0 includes obstacle deletion, VizManager pointer store obj+0x54 (0x00622D24..0x00622D30; CozmoContext+0x24, ctor 0x004EA734; read by EraseVisualization 0x004EA5E8..0x004EA5F2): viz, no effect on 1-4 and notification.",
    "Notifications 0x00506F98/FA8/FB8 world/map/state-change; unchanged return 0x00506296..29A; changed nonzero new state map 0x005062B4 and active light recipient. 0x00639840 compares NEW state r3 with1: Known trigger0x25/layer2, other changed trigger1/layer2; unchanged no request.",
    "Contact edge/store ordering 0x00511A66..ACC, 0x00511B50..0x00511C14; platform setter 0x00511D44..DCA; existing footprint tick 0x00513CD8..0x00513E2A and M4-019 threshold send 0x0063427E, 0x006342C8..CE, 0x00634364..368.",
    "re-analysis/research/20261021-m13-017-located-charger-check.md: independent check of A1-A11/P1-P4 with corrected A9; supplementary mount and exhaustive caller census excluded."
  ]
}
```

### M12-002

```json
{
  "title": "Path segment, ExecutePath and ClearPath packings; the path-motion-profile defaults and their wiring",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PathDolerOuter::Dole 0x00507E4C: 0x00507F40/0x00507F44/0x00507F48 cmp r1,#1/2/3; line 0x00507FE0 blx 0x4A7234; arc 0x00508036 blx 0x4A7240; point turn 0x00507F8E blx 0x4A7228",
    "PathComponent::ExecutePath 0x0064A340: 0x0064A426 ldrh r0,[r5,#-0x12]; 0x0064A430 strh.w r0,[sp,#8]; 0x0064A42A strb.w r6,[sp,#0xa]; 0x0064A436 blx 0x4B9D8C; 0x0064A442 blx 0x4A5368",
    "PathComponent::ClearPath 0x00649220: 0x00649268 movs r0,#0; 0x0064926A strh.w r0,[sp]; 0x00649234 ldrh.w r0,[r4,#0x42]; 0x0064923A strh.w r0,[r4,#0x4a]",
    "SpeedChooser::GetPathMotionProfile 0x0053B798 copies the 11-word table 0x00C535E0 = 100,200,500,2,10,10,60,200,500,80,0",
    "DriveToPoseAction::Init 0x0055A86C; 0x0055A998 blx 0x4ABEE4 (StartDrivingToPose); 0x0064ADE0 blx 0x4B9DBC (GetPathMotionProfile); 0x0064ADD2/0x0064ADE4 copy into [this+0x4C]",
    "PathComponent::UpdatePlanning 0x006495E0 tail-calls HandlePlanComplete on status 2: 0x006496DE b.w veneer -> 0x4B9D2C",
    "HandlePlanComplete reads [this+0x4C] and passes it to IPathPlanner::GetCompletePath: 0x00649F76 ldr r1,[r4,#0x4c]; 0x00649F82 blx 0x4B9D68",
    "the packing citation 0x00632BAE..0x00632BEE is the PlaceObjectOnGround builder (0x00632B88), not path packing"
  ]
}
```

### M12-032

```json
{
  "title": "PathComponent drive-to-pose status machine: fields, SetDriveToPoseStatus, predicates, StartDrivingToPose, Update/UpdatePlanning/HandlePlanComplete, ExecutePath, the 0xC6 PathFollowingEvent handler, the RobotState segment and doling",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0064941C, 0x0064937C, 0x00649390, 0x0064B370, 0x0064B61C, 0x006493B4, 0x006494CC",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q8; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q8",
    "0x0064A5AE..0x0064A5B6, 0x004FE8AE..0x004FE8D4, 0x00858F90..0x0085913C, 0x008515F8..0x008516E8, 0x00841BA0..0x00841C68, 0x00846F3C..0x00847198",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q8; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md Q8",
    "re-analysis/research/20261013-m12-032-path-status.md: rows P1-P30 (ctor 0x00648B14..0x00648B6C, SetDriveToPoseStatus 0x006492C4..0x0064931A, predicates, StartDrivingToPose 0x0064ACC4..0x0064AF30, StartPlanner/fallback, Update 0x006494CC..0x00649586, UpdatePlanning 0x006495E0..0x0064972E, HandlePlanComplete 0x00649F60..0x0064A214, ExecutePath 0x0064A340..0x0064A48E, ExecuteCustomPath, UpdateCurrentPathSegment 0x006493B4..0x00649434 with the +0x42/+0x44 gate, Doler.Update 0x005082CE..0x00508326, 0xC6 lambda 0x0064B370..0x0064B4FC and helper 0x0064B620..0x0064B6A0, origin changes 0x0064980C..0x00649D14, tick order 0x005140BC/0x00514170). OnPathComplete 0x00649390 and AbortAndSetFailure 0x0064937C are dead code."
  ]
}
```

### M8-001

```json
{
  "title": "The IBehavior lifecycle: constructor fields, Init, Update, Stop, StopActing, Resume, IsRunnable and IsRunnableScored",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::IBehavior 0x005bbc38 stores behaviourID at +0x3c, behaviourClass at +0x64, needsActionID at +0x68; zeroes +0x24/+0x28, the flat score at +0x100 and +0x104 (strd r8,r8,[r4,#0x100] 0x005bbd28); sets +0x108 = 0, +0x10c = 0x29 (invalid behaviour objective), and +0x110 = +0x111 = 1 (movw r0,#0x101 0x005bbd30, strh.w r0,[r4,#0x110] 0x005bbd34); constructs the repetition graph at +0xe8 and the running-penalty graph at +0xf4 (GraphEvaluator2d::GraphEvaluator2d 0x005bbd12/0x005bbd22); calls ReadFromJson 0x005bbd40 and ScoredConstructor 0x005bbe32.",
    "IBehavior::Init 0x005bcb54 walks the robot's ActionList at robot+0x250. action+0x60 is the action's 32-bit tag; the guard sets its flag when a tag is > 0x2dc6c0 (movw r6,#0xc6c0 0x005bcbd0 / movt r6,#0x2d 0x005bcbda; cmp r2,r6 0x005bcbec; movhi r5,#1 0x005bcbf0). sTagCounter at 0x01051020 starts at 0x002dc6c1, so every engine-constructed action tag is >= 0x2dc6c1, while an action tagged through the game path (IActionRunner::SetTag 0x00540098, called from HandleActionEvents 0x00525ab0 and the QueueSingleAction/QueueCompoundAction handlers 0x00527a78/0x00527c16) carries a tag <= 0x2dc6c0. The warning IBehavior.Init.ActionsInQueue at 0x005bcc84 fires only when at least one engine-tagged action is present; its count is the main queue's (map key 0) current action plus queued actions (ldr r1,[r0,#0x14] 0x005bcd64, ldr r0,[r0,#0x20] 0x005bcd66, addne r0,#1 0x005bcd6c).",
    "IBehavior::Init spark gate +0xd8: ldr r0,[r0,#0x44] 0x005bccac; if [r0+0x58] != 0x55 then +0xd8 = ! [r0+0x5c] else 0 (0x005bccb0..0x005bccc2). It then clears +0x98, writes halfword 0x0100 at +0xa0 (so +0xa1 = running = 1, +0xa0 = 0), stamps the running-penalty clock +0x34 = BaseStationTimer::GetCurrentTimeInSeconds(), clears the idle-animation flag +0xb0, and calls vtable+0x48 (IsRunnableInternal): non-zero clears +0xa1, otherwise +0x80++ (0x005bccbe..0x005bcd0c). If the unlock id +0x70 is not 0x55 and equals robot+0x44->0x58 it takes the SparkBehaviorDisables lock through SmartDisableReactionsWithLock and clears +0x114 (0x005bcd16..0x005bcd58).",
    "IBehavior::Update 0x005bd074 calls vtable+0xc (UpdateInternal) unless byte +0xa0 is set and the current-action handle +0x84 is zero, in which case it returns 2 (ldrb.w r1,[r0,#0xa0] 0x005bd074; cbz r1 0x005bd078; ldr.w r1,[r0,#0x84] 0x005bd07a; cbz r1 0x005bd07e; movs r0,#2 0x005bd088).",
    "IBehavior::Stop 0x005bd08c logs, stops any helper, clears +0xa1 (0x005bd10c), calls vtable+0x54 (StopInternal, 0x005bd110/0x005bd114), stamps the last-run clock +0x30 = BaseStationTimer::GetCurrentTimeInSeconds() (0x005bd11a/0x005bd11e), calls StopActing(0,0) (0x005bd126), then undoes the scope in this order: (a) remove disable-reactions locks while +0xac != 0 (0x005bd12c/0x005bd136), (b) remove the idle animation if +0xb0 (0x005bd148), (c) clear the motion profile if +0xc0 (0x005bd158), (d) walk the track-lock map at +0xb4 and MovementComponent::UnlockTracks(mask, name) each, then destroy the map (0x005bd16c/0x005bd174).",
    "IBehavior::StopActing 0x005bd34c calls vtable+0x80(0) (blx r2 0x005bd360); when viaCallback is false and a helper is live it logs \"Stopping behavior helper because action stopped without callback\" and calls StopHelperWithoutCallback (0x005bd39c/0x005bd3d6); then, if +0x84 is set, cancels it in ActionList (0x005bd3f0) and clears +0x84 unless keepAction.",
    "IBehavior::Resume 0x005bceac: for any trigger other than 0 (CliffDetected) or 0x14 (UnexpectedMovement) it takes the normal resume path (cmp r5,#0x14 0x005bcf16; cmpne r5,#0 0x005bcf1a; bne 0x5bcf7e 0x005bcf1c). For those two it reads +0x114, increments it and takes the normal path while the pre-increment value is < 1 (ldr.w r0,[r4,#0x114] 0x005bcf1e; cmp r0,#1 0x005bcf22; str.w r1,[r4,#0x114] 0x005bcf28; blt 0x5bcf7e 0x005bcf2c); otherwise it sets +0x118 = now + 15.0, builds \"TooManyResumesCliffOrMovement\" and calls MoodManager::TriggerEmotionEvent(name, now), returning 1 (0x005bcf36..0x005bcf7a). The normal path sets +0xa2 = 1, stamps +0x34 = now, calls vtable+0x4c (ResumeInternal), clears +0xa2; on a zero result sets +0xa1 = 1 and, when +0x70 != 0x55 and equals robot+0x44->0x58, takes the SparkBehaviorDisables lock; on a non-zero result clears +0xa1 (0x005bcf80..0x005bcfde).",
    "IBehavior::IsRunnable 0x005bd750 calls IsRunnableBase(this->robot); a result other than 1 returns 0, otherwise it tail-calls vtable+0x50 with the caller's robot (0x005bd756..0x005bd770). IBehavior::IsRunnableScored 0x005bda28 returns 1 when the current time is >= the suppression stamp +0x118, else 0 (0x005bda38/0x005bda3e/0x005bda46/0x005bda48). IBehavior::IsRunnableBase 0x005bd778: +0xa1 (running) returns true; otherwise the AI process (robot+0x264), the robot state byte +0x74 against 3, the unlock id +0x70 (0x55 bypasses) through ProgressionUnlockComponent::IsUnlocked(robot+0x448, id, true) (0x005bd810), and the float timers at +0x78.",
    "ReactionTrigger ordinals (table 0x10337c0; EnumToString 0x0077065c): 0 = CliffDetected, 0x14 = UnexpectedMovement, 0x15 = Count, 0x16 = NoneTrigger (ReactionTriggerFromString 0x00770674 confirms the same mapping).",
    "StopWithoutImmediateRepetitionPenalty 0x005beea0 has exactly three callers, all concrete-behaviour UpdateInternal methods, none of them IBehavior::Stop/StopActing/FinishCurrentBehavior: BehaviorPickUpCube::UpdateInternal (blx #0x4b3384 0x005c685c, then movs r0,#2 0x005c6860), BehaviorStackBlocks::UpdateInternal (0x005c991c/0x005c9920), BehaviorBuildPyramidBase::UpdateInternal (0x005dd110/0x005dd114). Those behaviours are M7/M15, not M8.",
    "IncreaseScoreWhileActing 0x005bf02c has ten callers, all concrete behaviours (KnockOverCubes, PopAWheelie, PutDownBlock, RollBlock, StackBlocks and the FUN_0059ec54 helper that pairs it with StartActing); ScoredActingStateChanged 0x005bf044 (clears +0x104) has no engine caller at all (exhaustive bl/blx and data-word scan). IBehavior::IsRunnableScored 0x005bda28 likewise has no engine caller and is not in the IBehavior vtable (0x010264e0); only its .dynsym st_value 0x005bda29 exists."
  ]
}
```

### M8-004

```json
{
  "title": "The engine's behaviour score default is zero; this stack's code-built behaviours carry 1.0/5.0",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "The engine has no in-code default score. IBehavior::IBehavior 0x005bbb74 writes zero to the flat score at +0x100 and to +0x104 (0x005bbd1c mov.w r8,#0; 0x005bbd28 strd r8,r8,[r4,#0x100]). EvaluateScoreInternal 0x005beec2 returns that float when the mood-scorer vector at +0xdc is empty (0x005beec2 ldr r2,[r0,#0xdc]!; 0x005beec6 ldr r3,[r0,#4]; 0x005beed4 vldr s0,[r0,#0x24] -> +0x100). The only non-zero writer is ReadFromScoredJson 0x005bc488 from the optional flatScore key (0x005bc4e0 str.w r0,[r4,#0x100]); there is no hard-coded default.",
    "Selection is by the activity chooser or the reaction map, not a flat ranking: the app's scored behaviours are the ones an activity config lists with a flatScore, and ScoringBSRunnableChooser 0x0060a44a skips a score <= 0 (0x0060a452). The policy review of 2026-09-29 (verdict C) found this divergence; @cozmo-verifier re-read 0x005bbd28 and 0x005beec2 and confirmed it.",
    "This stack's ScoredBehaviorEntry.FromJson already defaults flatScore to zero, so the config-driven chooser path matches. The divergence is on behaviours built in code: PlayAnimBehavior's constructor defaults score 1.0, ReactBehavior 5.0, SteppedBehavior.Score 5.0, PlayArbitraryAnimBehavior.Score 1.0 and SingingBehavior 1.0. Those feed this stack's BehaviorManager.ChooseAndSwitch, which ranks every behaviour by IBehavior.EvaluateScore; the engine has no such path."
  ]
}
```

### M8-007

```json
{
  "title": "An action's track mask is a lock, taken while it runs",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Every action carries a track mask at +0x54. IActionRunner::Update 0x00540370 reads it and asks MovementComponent::AreAnyTracksLocked(mask) at 0x00540440: while any of those tracks is held it warns \"Action %s [%d] not running because required tracks are locked\" (0x005404A8) and does not run the action - it stays queued and is tried again on the next tick. Otherwise it locks them through the helper at 0x004F0F4C, which calls MovementComponent::LockTracks(mask, tag, name), and then runs the action.",
    "LockTracks 0x00640098 walks the bits of the mask and inserts one LockInfo - the owner's name and reason - per track into a multiset at component+0x30; UnlockTracks 0x0063FE5C removes them. So a track is held by a set of owners and is free when the last one lets go.",
    "The mask is therefore mutual exclusion, not muting: the animation plays its tracks and nothing else may drive them while it does. This stack claims the clip's tracks on the behaviour's scope and the scheduler refuses a play whose tracks are owned; the refusal waits and retries, as IActionRunner does, instead of treating the play as done.",
    "re-analysis/research/20261012-m8-play-animation-actions-check.md correction: TRACKS_LOCKED (0x03000019, stored at 0x0054057C) goes straight to completion (0x005405A4); no wait, no retry. The earlier \"stays queued and is tried again\" is wrong."
  ]
}
```

### M8-015

```json
{
  "title": "ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0053E6A8..0x0053E6E0; 0x005409E2..0x00540A40; 0x00541146..0x00541238; 0x0053FAB2; Current Cancel then delete; pending direct deletion preserves existing result. Cancelled0x02000000 except NotStarted0x02000001; stop before unlock, watcher then broadcast. Existing B-ACTIONS candidate must be checked for full teardown effects and concrete descendants.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "P3/P4 full ActionList/ActionQueue/IAction lifecycle: ActionList 0x0053F580..0x0053F5F2, ActionQueue 0x0053F5F4..0x0053F70C, IActionRunner 0x00540370..0x0054063E, clock/timeout 0x00540D4A..0x00540E80, CheckIfDone 0x005549B8..0x005549F4 (20261006-M1M2-missing-triage.md P3/P4).",
    "re-analysis/research/20261012-m8-play-animation-actions.md rows C2..C7, L14..L23, D5, S2..S4 and re-analysis/research/20261012-m8-play-animation-actions-check.md (the +0x57 gate on L17/L18/L20, the DeleteActionAndIter guard set at queue+0x10, ActionWatcher Start/EndUpdating 0x00540386/0x0054057E/0x00540630, the UpdateInternal status string 0x00540D46..0x00540F1C).",
    "re-analysis/research/overnight/20261014-ov18-m8-015-check.md: L1-L9 0x00540370..0x0054063A, 0x00540D1C..0x00540F9C, 0x00540750..0x005407C0, 0x005409DC..0x00540A44; Q1-Q7 0x0053F580..0x0053F708, 0x0053DCE4..0x0053DD92, 0x0053E24C..0x0053E2F6, 0x0053DF2C..0x0053DFC8, 0x0053DE10..0x0053DE5C, 0x0053E690..0x0053E700; D1-D6, D12 0x0053F9E4..0x0053FB06, 0x00541084..0x00541286, 0x00541BB4..0x00541C04, 0x0054187E..0x005418DE; T2 List Clear 0x0053D916..0x0053D93A, 0x0053F938..0x0053F950, 0x0053FB5C..0x0053FB82; QueueAction front end 0x0053D93C..0x0053DAC8; position table 0x0053DA24..0x0053DB70.",
    "re-analysis/research/overnight/20261015-ov56-58-check.md: teardown mechanics from OV56: ~Robot order 0x005110EE..0x00511156; AbortAll 0x00511952..0x0051198C (0x0063BE2A..0x0063BE36, 0x00517DFE..0x00517E0A); ActionList::Cancel clearing gate and slot order 0x0053DE18..0x0053DE54; ActionQueue::Cancel 0x0053E69A..0x0053E6FA (early exit 0x0053E6E4); QueueNow/QueueAtEnd 0x0053E25A..0x0053E36E; DeleteActionAndIter 0x0053F9E4..0x0053FAB2 (re-entrancy set, INTERRUPTED/no-interface gates); result codes 0x0053FDBA..0x0053FDD4, 0x005409E2..0x00540A40; Clear lock-out 0x0053D91A..0x0053D938, 0x0053DCEA..0x0053DD00, 0x0053DCAA, 0x0053DF40, 0x0053F93C, 0x0053F970..0x0053F9AC; ~ActionList double Clear 0x0053D8F6..0x0053D90E; IBehavior StartActing reject-delete 0x005BDC04, Init failure 0x005BCCC6..0x005BCD06."
  ]
}
```

### M8-019

```json
{
  "title": "BehaviorManager::RequestCurrentBehaviorEndImmediately, as HandleRobotStopped calls it",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Called at 0x00535454 (blx 0x4AA1C8) from Robot::HandleRobotStopped when the cliff sensor is enabled (gate 0x00535436..0x00535438; M4-019 H4 0x00535432..0x00535480)",
    "re-analysis/research/overnight/20261014-ov20-21-check.md: R2/R3 0x005A3930..0x005A39A6; caller R1 0x00535432..0x00535486 (interface to M4-019 H4)."
  ]
}
```

### M8-020

```json
{
  "title": "PlayAnimationAction: construction, Init, robot/game animation handlers, CheckIfDone, animationCompleted, destructor streamer stop",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m8-play-animation-actions.md rows C1, C8..C11, P1..P12, D1..D3, L2..L8: ctor 0x00543C08..0x00543CBE (+0x84 loops, +0x8B 0, +0x8C interrupt, +0x90 null, +0x94 timeout; 60.0f -> FLT_MAX only when loops == 0), Init 0x00543F20 (cozmo_sings_custom remap 0x0054417C/0x00544190, lookup robot[0]->+0x20->+0x48, SetStreamingAnimation(robot+0x60, anim, loops, interrupt, flag) -> +0x8B, tag 0 -> failed, 0x03000001; R2E 0xCA/0xCB/0xD5 via 0x00519F8C and E2G 0x5F via external-interface vtable +0x28; callbacks 0x005452F1, 0x0054541D, 0x0054561D, 0x005457F5 and AnimationEvent forward 0x00545750), CheckIfDone 0x005441C0, GetCompletionUnion 0x005441DE (0x0075C544, 0x006C22DC), destructor 0x00543DAC (StillStreaming info 0x00543DF2 on robot+0x98 == +0x90; SetStreamingAnimation(null, 1, true, false) at 0x00543E26; releases +0xB4, +0xAC, +0xA4, +0x9C), vtable 0x01021584 slots +0x1C/+0x20/+0x2C (0x00545210).",
    "re-analysis/research/20261012-m8-play-animation-actions-check.md additions 2 (the null SetStreamingAnimation tail: +0x73 store and the idle Abort)."
  ]
}
```

### M8-021

```json
{
  "title": "TriggerAnimationAction: trigger to group, Init NO_ANIM_NAME paths, the group choice hand-off, the DAS blacklist gate and the robot.play_animation event",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m8-play-animation-actions.md rows T1..T12, D4, L11..L13: ctor 0x00544228, SetAnimGroupFromTrigger 0x0054432C (Has == 1 before Get; empty-group warning TriggerAnimationAction.EmptyAnimGroupNameForTrigger \"Event: %s\" at 0x005443AC), Init 0x0054443C (NO_ANIM_NAME 0x0300000C; GetAnimationNameFromGroup with the robot and strictCooldown; blacklist set walk +0x8C/+0x90; sEvent robot.play_animation, value +0x78, $data trigger + \":\" + group at 0x005445BC), vtable 0x010215BC."
  ]
}
```

### M8-023

```json
{
  "title": "Behavior teardown recursive recipients not yet read: IActivity unique children and their strategy/chooser types, ActivityStrategy derived bodies, std::function captures, derived behaviour children, reaction-strategy bodies other than Hiccup, other ProtoHandle instantiations",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov19-m8-016-check.md: \"Still unread\" list: 0x005B2FB8/0x005B2FC4/0x005B2FD2 (producer 0x005B2A5C..0x005B2F8B); 0x005B419C, 0x005B456C, 0x005B4818, 0x005B4CCC, 0x005B4E50, 0x005B4E78, 0x005B4EB0, 0x005B528C, 0x005B4806; 0x005A7936, 0x005A7942, 0x005AAD6E, 0x005B14D2; 0x005BC7C4, 0x00613C40, 0x00613C58, 0x00613C7A; 0x005DC506, 0x005D7B7A, 0x00604322, 0x005D36B6; 0x0060C3C4, 0x0060D0E4, 0x0060EA30, 0x0060F880, 0x00611708, 0x00611FD4, 0x0060B722.",
    "re-analysis/research/overnight/20261015-ov53-55-check.md: IActivity dtor 0x005B2FAC..0x005B2FD2; ReadConfig 0x005B2D7E..0x005B2E34 (+0x24 producer 0x005B2D7E..0x005B2DC6, +0x28 0x005B2DC8..0x005B2E14); chooser factory 0x00609C88 (D1 0x0060A238, 0x0060AFA8, 0x0060B1F8); strategy factory tbb 0x005B3BE2, dtors 0x005B419C..0x005B5258; WantsToRun factory tbb 0x00614726 (also IBehavior::ReadFromJson 0x005BC24C -> +0x38, deleted 0x005BC7F0); BuildPyramid/Feeding/Sparked 0x005A7894, 0x005AACBC, 0x005B1484; Block_Cube1x1 0x004E772C, 0x004E6F80, EraseVisualization 0x004E6932..0x004E6936; FactoryTest 0x005D3692, 0x005D36B6, CloseLog 0x006717B8..0x00671848; reactions 0x0060B6F4..0x00611FD4; closures: StrategyGeneric function slots (writers 0x0060F4C6, 0x006137C0, 0x006138A4, 0x0061422C; destroys bx lr), BuildPyramidBase +0x140 (0x005DBCA2..0x005DBCB2, 0x005DC600), FactoryTest +0x180 (0x005D04E2, offset scan)."
  ]
}
```

### M8-027

```json
{
  "title": "ProtoHandle subscriber-callable census: which callables the 8 ProtoHandle kinds hold at teardown and what destroying them does",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov53-55-check.md: ProtoHandle vtables (8) confirmed; EngineToGame release 0x00664708, unlink 0x006634F4 (nulls the callable, decrements node refcount +0x20, frees at zero 0x0066352E..0x00663560).",
    "re-analysis/research/overnight/20261016-ov105-107-108-answer.md: void() signal 0x0105DF8C: one subscriber via RegisterTransport 0x0083B9A0..0x0083BE4C (bind vt 0x0103795C trivial); Dispatch signal TransferQueueMgr+0xC: two RegisterTask::$_0 (vt 0x01031418, destroy memory-only) from TransferTask::Init 0x006A4358..0x006A438E; release bodies 0x004EF138, 0x0051D8F8, 0x00591550, 0x0064E618, 0x00664708, 0x00664EB8, 0x006A3EFC, 0x0083BED0 identical; ~TransferQueueMgr 0x006A3860..0x006A3898."
  ]
}
```

### M7-002

```json
{
  "title": "The shipped animation/reaction maps, behaviour configs and activity tree",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/obb/assets/cozmo_resources/assets/animationGroupMaps/AnimationTriggerMap.json (573 pairs)",
    "re-analysis/obb/assets/cozmo_resources/assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json (40 pairs)",
    "re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/reactionTrigger_behavior_map.json (22 rows over 21 triggers)",
    "RobotDataLoader::LoadReactionTriggerMap 0x00520bc8",
    "BehaviorManager::InitReactionTriggerMap 0x005a16e4",
    "178 shipped behaviour configs enumerated in re-analysis/inventory/M7-behaviour.md Appendix C"
  ]
}
```

### M7-014

```json
{
  "title": "Reaction-lock manager lifetime and concrete per-class lock tables",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::SmartDisableReactionsWithLock 0x005bce3c",
    "BehaviorManager::DisableReactionsWithLock 0x005a27e8",
    "BehaviorManager::RemoveDisableReactionsLock 0x005a3a48",
    "13 concrete 21-entry tables in M7 inventory Appendix F"
  ]
}
```

### M7-018

```json
{
  "title": "Shipped behaviour configuration and BehaviorClass factory binding",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "178-config table in M7 inventory Appendix C",
    "79-class entry-point table in Appendix B",
    "BehaviorContainer::CreateBehavior 0x0059c888",
    "class 0x39 BehaviorWait case constructs base IBehavior and installs the BehaviorWait vtable"
  ]
}
```

### M7-019

```json
{
  "title": "M7 concrete reaction-behaviour state machines",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "reaction-class rows in M7 inventory Appendices E and F",
    "corrected ReactToCliff mood-event sequence 0x00604d70..0x00604d84",
    "ReactToImpact 0x006061f8..0x0060647c",
    "ReactToOnCharger 0x00606c94..0x00606e86",
    "ReactToPickup 0x00607750..0x00607d9c"
  ]
}
```

### M7-020

```json
{
  "title": "Mood event production, repetition penalty and affector application",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "MoodManager::TriggerEmotionEvent 0x0067b85c",
    "MoodManager::AddToEmotion 0x0067bdca",
    "MoodManager::HandleActionEnded 0x0067c770",
    "EmotionAffector::ReadFromJson 0x00679910",
    "MoodManager::SendEmotionsToGame 0x0067b724"
  ]
}
```

### M7-012

```json
{
  "title": "Mood schema, decay graphs, action-result events, affectors, update and game output",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/obb/assets/cozmo_resources/config/engine/mood_config.json",
    "StaticMoodData::ReadFromJson 0x0067ce6c",
    "MoodManager::Update 0x0067b5d4",
    "MoodManager::SendEmotionsToGame 0x0067b724..0x0067b7f8"
  ]
}
```

### M7-013

```json
{
  "title": "Mood clamp, graph evaluation and decay-clock restart arithmetic",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Emotion::Add 0x00679618",
    "Emotion::Update 0x006795a4",
    "old<=1e-5 raw-new multiply at 0x006795ee..0x006795fc",
    "GraphEvaluator2d::EvaluateY 0x00804bd0",
    "mood_config.json decayGraphs"
  ]
}
```

### M7-021

```json
{
  "title": "Live reaction robot fields and the WaitForLambda/state-name helpers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "robot+0x355 seven-valued OffTreadsState: parser 0x0078df58, ctor 0x005100ea, commit 0x0051208e",
    "robot+0x338 on-charger-contacts boolean: init 0x005100c4, SetOnCharger store 0x00511c14, IS_ON_CHARGER extract 0x00512aae, call 0x00512ab4",
    "robot+0x300 raw lift angle: store 0x0051296a; on-face threshold 45.0f literal 0x00608b54, compare 0x00608af4, select 0x00608b1e/0x00608b20",
    "robot+0x37c filtered accel magnitude: 0.05 at 0x00512d04, 0.95 at 0x00512d08, store 0x00512a2e",
    "BehaviorReactToPickup::UpdateInternal 0x00607bc4..0x00607c08 (+0x338 gate, return 2)",
    "WaitForLambdaAction ctor 0x0055b554, runtime CheckIfDone 0x0055dade, timeout getter 0x0055daf6",
    "behaviour state-name setter 0x005c0ca8, channel Behaviors 0x00bee2c1, event Behavior.TransitionToState 0x005c0d4c, format 0x005c0d68; M7 callers ReactToCliff 0x00604f90/0x00605110 and DriveOffCharger 0x005c0b74/0x005c0be2/0x005c0de0"
  ]
}
```

### M7-023

```json
{
  "title": "Mood destruction with already-null ActionList dependency",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0067AE14..0x0067AE74; 0x00511150; 0x005111B6; Callback-id/Robot/ActionList gates precede unregister; normal Robot teardown already nulled ActionList, so skips unregister. Remaining destructor children UNKNOWN.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "re-analysis/research/overnight/20261015-ov34-35-check.md: L3 0x005110D4..0x005111C4 (owner nulled after delete 0x005111C4); gates 0x0067AE18..0x0067AE30; children 0x0067AE34..0x0067AE74 (0x004EAE94, 0x0067C0CC, 0x0067C0FC); subscription retirement 0x004EF184..0x004EF1CE, 0x00664EB8, 0x0066365C, 0x00663696, closure destroy 0x0067C228; sole caller BLX 0x005111B6 via PLT 0x004A7A80."
  ]
}
```

### M7-026

```json
{
  "title": "MoodManager::GetSimpleMood and the mood time MoodManager+0x130 (inputs to the animation group draw)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m5-x9-extraction.md section 4f: GetSimpleMood 0x0067C084 (Default; Happy > 0.33f -> Happy; Confident < -0.29f -> Sad; Happy < -0.33f -> Sad; constants 0x0067C0C0..0x0067C0C8); the group draw reads MoodManager+0x130 (0x0058A95C), written only by MoodManager::Update(t) 0x0067B67E and Reset/ctor."
  ]
}
```

### M5-063

```json
{
  "title": "DrivingAnimationHandler bodies: HandleActionCompleted (E2G 0x5A), PushDrivingAnimations (G2E 0xB5), RemoveDrivingAnimations (G2E 0xB6)",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m5-teardown-members.md: callbacks 0x004F110E -> HandleActionCompleted 0x004F0D78 (engine sender: ActionQueue 0x0053FAAA); 0x004F1186 -> PushDrivingAnimations 0x004F0944; 0x004F120E -> RemoveDrivingAnimations 0x004F0A94; registered only if HasExternalInterface() == 1 (0x004F072C..0x004F0732).",
    "re-analysis/research/20261012-m5-driving-handler-read.md: handler state ctor 0x004F06B0..0x004F0878 / Init 0x004F0FC4..0x004F0FF4; Push gate warning 0x004F094C..0x004F0964 and push_back 0x004F0988..0x004F09FA; Remove gate 0x004F0A9E..0x004F0AB4, empty-stack warning 0x004F0ADA..0x004F0C22 (0xBE4672), first-match erase 0x004F0AE4..0x004F0C6C, LoopBoundOverflow drivingAnimationHandler.cpp:108 0x004F0B5A..0x004F0BB6; UpdateCurrDrivingAnimations only from Init (0x004F0D2C..0x004F0D74: back(), else Sad 0x2C, else needs-met 0x38, else default 0x20); HandleActionCompleted 0x004F0D78..0x004F0E2A with PlayEndAnim 0x004F0EFA..0x004F0F4A, PlayDrivingLoopAnim 0x004F0E58..0x004F0EA0, PlayDrivingEndAnim 0x004F106C..0x004F10C0, lock/unlock helpers 0x004F0EB2..0x004F0EE2 / 0x004F0F4C; ActionIsBeingDestroyed 0x004F0F94..0x004F0FC0."
  ]
}
```

### M5-039

```json
{
  "title": "Ready-to-stream animation consumer lifecycle",
  "status": "EXACT_SOURCE",
  "evidence": [
    "Ready store 0x0052C3A6; SyncTimeAck 0x0053667E..0x005366AC; streaming gate 0x00513BF2..0x00514470; exact consumer span UNKNOWN (triage N2).",
    "re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-039 (Opus settling pass, manager-adopted 2026-10-11).",
    "re-analysis/research/20261011-m5-fix-rows.md section 21 (Opus fix rows, manager-adopted 2026-10-11)."
  ]
}
```

### M5-056

```json
{
  "title": "The engine-to-game subscription container factory and its ProtoHandle (UiMessageHandler::Subscribe(EngineToGameTag))",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m5-x9-extraction.md section 4a: UiMessageHandler::Subscribe(EngineToGameTag) 0x006626CC creates the container through 0x0066411C; its ProtoHandle and release are not read.",
    "re-analysis/research/20261012-m5-teardown-reads.md item 2 (Opus-checked 14/14): Subscribe 0x006626CC..0x0066271A (map +0x20, 16-bit tag); factory 0x0066411C..0x0066418C; lazy sentinel 0x0066436C..0x006643C4; node 0x00664684..0x006646B2 (refcount +0x20 = 1, tail insert); handle {vptr 0x0102FFB8, signal, node, token, ctrl}; token 0x006642E6..0x0066434E (0x12345678, deleter 0x0066446A bx lr); container control 0x004EF088..0x004EF0AC, on_zero_shared 0x004EF184, destructor 0x004EF1B8..0x004EF1D0; unlink 0x00664708..0x00664742 (weak lock, token release before deref, identity search) -> node unlink 0x006634F4..0x00663562; handle destructors 0x006646C4..0x00664702; libc++ __release_shared 0x5ED6C, __release_weak 0x5EDC2, lock 0x5EDE8. G2E (0x00662750 -> 0x006648CC, node 0x00664E34, unlink 0x00664EB8) is the same algorithm."
  ]
}
```

### M15-059

```json
{
  "title": "ActivitySparked lifecycle: OnSelected (HardSparkStartedByEngine tag 131), OnDeselected (StopAllAnims), objective/observed, Update/checks/end/interrupt, get-in/out, Complete (meta.upgrade_replay_* events, HardSparkEndedByEngine tag 132)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov65-67-check.md: OnSelected 0x005B1518..0x005B16DC, 0x005B171C; OnDeselected 0x005B1780..0x005B179E; objective/observed 0x005B181E..0x005B1866; Update/check/end/interrupt 0x005B186C..0x005B1A16; get-in/out 0x005B1D18..0x005B1F2A, hard max 0x005B1F94..0x005B2066; Complete 0x005B1A90..0x005B1C2E, 0x005B2094."
  ]
}
```

### M15-072

```json
{
  "title": "SevereNeedsComponent live selected need (AI+0x30, +0x14): Set/Clear side effects and order, AI-phase Update clear, and its native callers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261021-m15-severe-need-input.md (row 39), checked by re-analysis/research/20261025-r-m15-severe-input-claude-check.md.",
    "ctor stores 3 to +0x14 at 0x00572A0E; AIComponent::Update 0x00569F24..0x00569F4C calls AIInformationAnalyzer, AIWhiteboard, SevereNeedsComponent::Update (0x00569F38), BehaviorHelperComponent, FreeplayDataTracker in that order.",
    "Update 0x00572BD0..0x00572C3E: selected == 3 returns; else IsNeedAtBracket(selected, Critical 3) (0x00572BEA); false logs and calls Clear (0x00572C38).",
    "Clear 0x00572C8C..0x00572D6E: RemoveDrivingAnimations (robot+0x24C, 0x00572CF6), AnimationStreamer::RemoveIdleAnimation (robot+0x60, 0x00572D26), BehaviorManager::RemoveDisableReactionsLock (0x00572D56), each with 'severe_need_component_lock' (0x00BEE67C, 26 bytes); then +0x14 = 3 (0x00572D6A).",
    "Set 0x00572E48..0x005731C0: idle map from 0x00C596C4 (Energy 1 -> 0x137, Repair 0 -> 0x143); driving triplets Energy 1 -> 0x134/0x133/0x132 (0x0105A9F8), Repair 0 -> 0x140/0x13F/0x13E (0x0105AA08); PushDrivingAnimations 0x005730E2, PushIdleAnimation 0x00573152, DisableReactionsWithLock(lock, table, true) 0x005731A0 with the table from map 0x0105A9D8 (init 0x004D71B4; pairs at 0x010237A0: Energy and Repair -> 0x00C59670, Play -> 0x00C5969A all false); then +0x14 = need (0x005731B4).",
    "Callers: wake handler 0x00572E00..0x00572E42 (Repair Critical -> Set 0, else Energy Critical -> Set 1, else nothing); BehaviorPlayAnimOnNeedsChange ShouldGetInBePlayed 0x005BFECA..0x005BFEF6 and StopInternal 0x005BFEF8..0x005BFF16 (Set(config need) when it differs from the selected need and is Critical, regardless of stop reason); BehaviorExpressNeeds::StopInternal 0x005EE7D8..0x005EE7E8 (Clear when own+0x134); PlayNeedsGetOutAnimIfNeeded destructor 0x0054504C..0x0054505E and CheckIfDone 0x005451C8..0x005451EE (Clear under the own+0xC9/+0x88/trigger 0x23F guards)."
  ]
}
```

### M15-073

```json
{
  "title": "App input: NotifyCozmoWakeup (G2E 166) to the SevereNeedsComponent wake handler",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261021-m15-severe-need-input.md (row 39), checked by re-analysis/research/20261025-r-m15-severe-input-claude-check.md.",
    "SevereNeedsComponent::Init 0x00572A64..0x00572A86 subscribes only when HasExternalInterface(); helper 0x00572B10..0x00572B96 registers G2E 0xA6 (166); the callback 0x005734F2..0x00573504 forwards to the wake handler 0x00572E00.",
    "App sender: unity/scripts/csharp/ConnectionFlowController.cs WakeupSequence 600..611 (wake animation, then NotifyCozmoWakeup); Cozmo.Hub/NeedsHub.cs:95."
  ]
}
```

### M13-038

```json
{
  "title": "BehaviorAudioClient instances and lifetime: two clients (BehaviorManager, BehaviorSystemManager) subscribe to the public-state broadcaster; whether both stay subscribed; the re-entrant double ActivateSparkedMusic",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov48-49-check.md: Subscribe 0x00591F08 in the ctor; constructed by BehaviorManager 0x005A0914 and BehaviorSystemManager 0x005A5828, both built in Robot::Robot (0x0050FCAC, 0x0050FCBE); emitter 0x0064DE5E..0x0064DE7A -> 0x0064E656..0x0064E668 (synchronous, skips empty slots); re-entrance via the unlock 0x1D path and UpdateBroadcastBehaviorStage 0x0064DA44..0x0064DB86 (broadcaster state+0 copied at 0x0064DB44..0x0064DB58).",
    "re-analysis/research/overnight/20261016-ov69-74-75-77-check.md: construction 0x0050FC90..0x0050FCD0; clients 0x005A0908..0x005A091E, 0x005A581C..0x005A582C; ctor 0x00591E50..0x00591F08 (unconditional broadcaster subscribe); emit 0x0064E594..0x0064E69A; lifetime 0x00511120..0x0051113E, 0x005A0D8E..0x005A0DA2, 0x005A5886..0x005A589C; state+0 0x0064D5DC..0x0064D6B8; stage setter 0x0064DA4E..0x0064DB86; snapshots 0x0064D85C..0x0064D8A0; spark branch 0x005923D4..0x00592478; Activate 0x00592190..0x005921DE."
  ]
}
```

### M13-039

```json
{
  "title": "BehaviorAudioClient::HandleRobotPublicStateChange and helpers: world events, round, spark, needs, dim, activity music, GuardDog and Feeding stages, unlock cache; SetSparkedMusicState input",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov48-49-check.md: handler 0x005921EE..0x00592258; helpers 0x0059225C..0x00592720, 0x00591FC4..0x00592190 (round table 0x00C5B65C, activity map 0x00C5B5F0, eighties 0x00573B70..0x00573B9A, 0x00573E30..0x00573E7E); handler lambda 0x01023D98 -> 0x00592B1E -> 0x004AF07C; SetSparkedMusicState (G2E 0xA3) lambda 0x00592A9E..0x00592ABA, client+0xC initial 0 (0x00591E56)."
  ]
}
```

### M12-049

```json
{
  "title": "BehaviorBuildPyramid helper callbacks and the pyramid cache contents (Container::Update, CastNewEntryToBestSharedPointer, PruneFullPyramids, PyramidBase::operator==)",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261016-ov69-74-75-77-check.md: callbacks vtables 0x01029068 / 0x010290A8 (built 0x005DC16C..0x005DC17C, passed at 0x005DC298); cache UpdateAllBlockConfigs 0x00616F00 -> Container::Update 0x0061682C -> AddAllConfigsWithObjectToCache 0x0062C56E -> CastNewEntryToBestSharedPointer 0x0062C68C; PruneFullPyramids 0x0062C72E."
  ]
}
```

### M12-043

```json
{
  "title": "PyramidBase interior geometry: corners and midpoint for the pyramid top placement (GetBoundingQuad<float> corner order)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: corners/midpoint 0x006177F0..0x00617A08, 0x00617A8C..0x00617C32, multimap 0x00618A4C (equal keys keep insertion order, NaN right); unread GetBoundingQuad<float> 0x004E6490..0x004E6598 and Block::GetBoundingQuadXY 0x004E62A2.",
    "re-analysis/research/overnight/20261015-ov45-47-check.md: quad call 0x00617818..0x00617824; GetCanonicalCorners 0x004E60D8..0x004E6146 (8 corners at +-0.5: (-,-,+),(+,-,+),(-,-,-),(+,-,-),(-,+,+),(+,+,+),(-,+,-),(+,+,-)); pad/scale/rotate 0x004E62C0..0x004E6386; minAreaRect points opencv_core 0x00086188..0x0008627A; Quad 0x004E64FE..0x004E655A; centroid 0x004DF7BE..0x004DF84C; sort 0x004E7F24..0x004E7F96, 0x004E8508..0x004E8642; translation 0x004E639A..0x004E63B2; insertion 0x00617826..0x006179BC; midpoint 0x00617B98..0x00617BF6; container 0x006168EC..0x006169C4; builder 0x0061830A..0x006183B4; cache 0x0062C5A2..0x0062C616."
  ]
}
```

### M11-037

```json
{
  "title": "The BlockWorld frame sequence for observed markers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BlockWorld::UpdateObservedMarkers 0x00624EE8: non-empty path is ClearOccluders 0x00624F98, AddLiftOccluder 0x00624FA4, CreateObjectsFromMarkers 0x00624FC4, AddAndUpdateObjects 0x0062505A, then (on its success) CheckForUnobservedObjects 0x006250CA, UpdatePoseOfStackedObjects 0x006250D0, BlockConfigurationManager::Update 0x0062520C, UpdateMarkerlessObjects 0x0062521A; the empty-list branch 0x625020 does ClearOccluders, AddLiftOccluder, CheckForUnobservedObjects 0x0062504E and skips the stacked-pose update. AddLiftOccluder body 0x6564D8 (raw state at the timestamp, Robot::GetLiftTransformWrtCamera, Transform3d::ApplyTo, Camera::Project3dPoints, Camera::AddOccluder with the transform z scale); BlockConfigurationManager::Update body 0x616D7C (DidAnyObjectsMovePastThreshold gate, UpdateAllBlockConfigs, PruneFullPyramids, UpdateLastConfigCheckBlockPoses, NotifyBroadcasterOfConfigurationManagerUpdate). UpdatePoseOfStackedObjects 0x621794 and UpdateMarkerlessObjects 0x625704 are cross-layer entry points (M10/M12)",
    "re-analysis/research/20260929-R-VIS-verify-pre-items4-5.md (items 4a, 4b PASS)",
    "re-analysis/research/20260929-R-VIS-M11-gap1-extraction.md Q3, Q5; re-analysis/research/20260929-R-VIS-verify-M11-gap1.md (objections 2 and 5 corrected)",
    "0x00507824..0x0050783A, 0x00621D54..0x00621D88, 0x0062206E..0x006220DA, 0x00622106..0x0062218E, 0x006211E6..0x00621214, 0x00625028, 0x0062521A, 0x00620BE2",
    "re-analysis/research/20260929-R-VIS-verify-M11-batchA.md objections 2, 5, 8, 9, 10"
  ]
}
```

### M11-074

```json
{
  "title": "Memory-map producers and readers not yet read: MapComponent::UpdateRobotPose (explored area), UpdateObjectPose, RemoveObservableObject, FlagInterestingEdgesAsUseless, ReviewInterestingEdges, ClearRobotToMarkers, FindContentIf/FindIf",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-quadtree-rest-check.md: UpdateRobotPose 0x0067E224..0x0067E50B; UpdateObjectPose 0x0067EA90..0x0067ECFB; RemoveObservableObject 0x0067F2C4..0x0067F58B; FlagInterestingEdgesAsUseless 0x0067E778..0x0067E85B; ReviewInterestingEdges/ClearRobotToMarkers 0x0067F58C..0x0067F7AB; FindContentIf 0x00681674..0x006816E7 and QuadTreeNode::FindIf 0x006884FC..",
    "re-analysis/research/overnight/20261015-ov42-44-check.md: R1-R6 0x0067E23A..0x0067E486 (cliff byte writer 0x005356C6..0x005356D4); O1-O8 0x0067EA9A..0x0067F0BC; caller order 0x00506F98..0x00506FB8, 0x00506296..0x005062D6 (double Add); D1-D3 0x0067F4C4..0x0067F544, 0x0067F2DE..0x0067F368, 0x006810E0..0x00681172; C1-C5 0x0067F5B8..0x0067F714, caller 0x00506A86..0x00506A98; Q1-Q6 0x00681674..0x006816DC, 0x006884FC..0x006886F4."
  ]
}
```

### M11-048

```json
{
  "title": "Unread quad-tree, OpenCV-numeric and vision-handler bodies",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "re-analysis/research/20260929-R-VIS-M11-gap2-extraction.md 'not done'; re-analysis/research/20260929-R-VIS-verify-M11-gap2.md 'Open'; re-analysis/research/20260929-R-VIS-verify-M11-gap1.md 'Still open'"
  ]
}
```

### M11-044

```json
{
  "title": "PotentialObjectsForLocalizingTo::Insert, UseDiscardedObservation, LocalizeRobot and the confirmed-object pose refresh",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0050CD70..0x0050CDFC ctor and UseDiscardedObservation; 0x0050CE00..0x0050D170 Insert; 0x0050D170..0x0050D1C6 CouldUseObjectForLocalization; 0x0050D1CC..0x0050D4DA LocalizeRobot; 0x00620AD4..0x00621330 AddAndUpdateObjects (A1..A8)",
    "re-analysis/research/20260929-R-VIS-M11-gap3-insert.md (whole file)",
    "0x00531CCC..0x00531D06, 0x00516EC0, 0x005062E0..0x00506310, 0x00512458, 0x00512468, 0x00512B56..0x00512B8E",
    "0x00512112..0x00512188, 0x00510A24..0x00510A46, 0x0050609C..0x005060C4, 0x0050612C..0x0050614A, 0x0051238C..0x0051254E, 0x00512B56..0x00512BA6, 0x00531785..0x00531C86, 0x005309D1..0x00530BBE, 0x00654D92..0x0065507E (round-2 verifier)"
  ]
}
```

### M11-035

```json
{
  "title": "The VisionComponent per-mode result handlers and the frame broadcast",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionComponent::UpdateAllResults 0x006542EC runs markers 0x006544A2, faces 0x00654510, pets 0x0065457E, motion 0x006545E8, overhead edges 0x00654652, tool code 0x006546BC, computed calibration 0x00654726, image quality 0x00654790, laser points 0x006547FA",
    "then CheckMailbox 0x00654A74 and the RobotProcessedImage broadcast 0x00654A14/0x00654A1C"
  ]
}
```

### M11-065

```json
{
  "title": "Pose3d / Rotation3d / Transform3d arithmetic: double unit quaternion, float translation, float point rotation, renormalised composition (rows R1-R34, R38-R40)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-pose3d-arithmetic.md: rows R1-R34 and R38-R40 (storage 0x0084A656.., Normalize 0x00849DB8..0x00849E52, product 0x00849EF4..0x00849F8E, Rotation3d*Point3f 0x0084ACA6 -> UnitQuaternion<float>* 0x00849C1C..0x00849CF8, GetAngleDiffFrom 0x0084A694..0x0084A746, GetAngleAroundZaxis 0x0084AA1C..0x0084AAB8, GetRotationMatrix 0x0084AAE0..0x0084ABEC, Renormalize 0x008494E0..0x0084969E, Transform3d ops 0x0084B92A..0x0084BB98, Pose3d ctors 0x0084683C..0x00846DEE, PoseBase ops 0x0084505C..0x008451EA, IsSameAs_WithAmbiguity 0x00846EA4..0x008471A6, RotationMatrix3d ops 0x0084B0E4..0x0084B1B4)."
  ]
}
```

### M11-066

```json
{
  "title": "The pose tree: GetWithRespectTo (null/alias checks, depth walk, Tt^-1*Tf, 1000-step LoopBoundOverflow) and GetWithRespectToRoot (rows R35-R37)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-pose3d-arithmetic.md: rows R35-R37: GetWithRespectTo 0x00845A34..0x00845DFE (FromEqualsTo warning, chain cases a-d, LoopBoundOverflow poseBase_impl.h lines 0x1A0/0x1B6/0x1D4), GetTreeDepth 0x00845E8C..0x00845F02, HasSameParentAs 0x00844D44..0x00844D7C, GetWithRespectToRoot 0x004DF628..0x004DF6DC."
  ]
}
```

### M11-067

```json
{
  "title": "cv::Rodrigues (OpenCV 3.1.0, both directions, CV_32F in, double inside, Jacobi SVD) and the pose-tree copy semantics (deep copy ctor, in-place operator=, shared-node SetParent, \"_COPY\" names)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-pose3d-arithmetic.md: \"Not read\" section: cv::Rodrigues via RenormalizeHelper 0x008499B0..0x00849A10, Rotation3d(RotationMatrix3d) 0x0084A3F4..0x0084A45C, RotationMatrix3d(angle, axis) 0x0084ADA4..0x0084AF34; PoseBase::operator= 0x00845940, SetParent 0x00844A14, PoseTreeNode ctors 0x00845F44/0x00845FE4; UnitQuaternion<double>*Point3d 0x00849F90..0x0084A062; Euler/matrix X/Y 0x0084AF98..0x0084B424; IsValid 0x008497D0..0x008498E4; Abs 0x0084B51E; ApplyTo(vector) 0x0065671C; the Pose2d/Transform2d family 0x0084B619..0x0084B92A.",
    "re-analysis/research/20261013-rodrigues-and-pose-tree.md: cvRodrigues2 calib3d 0xF28E..0xFB9A (vector->matrix in double, DBL_EPSILON identity, R = c1*rrt + c*I + s*[r]x; matrix->vector cvCheckArr [-100,100) -> zero, cvSVD -> JacobiSVDImpl_<double> core 0x595C0..0x59B56 with OpenCV hypot 0x56EB0, GEMM U*Vt, s<1e-5 and 180-degree branches, acos), wrapper 0x111F8..0x1133E; PoseBase copy ctor 0x008458F0..0x0084592A, node copy ctor 0x00845FE4..0x00846044, operator= 0x00845940..0x00845A2E, SetParent 0x00844A14..0x00844A7A, node SetParent 0x008449EA..0x00844A10, WrapExistingNode 0x0084529C..0x008452C0, GetWithRespectTo write order 0x00845A34..0x00845E04."
  ]
}
```

### M12-001

```json
{
  "title": "Pre-action pose types, their per-type ctor distances and the distance threshold",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "dispatch on actionType 0..5: 0x004E5958 cmp r5,#5 / 0x004E595E tbh / table 0x004E5962",
    "type 0 Docking 75.0 (0x004E5A36 movt r1,#0x4296); angle pi/2 (0x004E5998 vadd d10=pi/2); translation {0,-65,-22} (0x004E59B4/0x004E59C0/0x004E59D4)",
    "type 1 PlaceRelative 40.0 (0x004E5ADA movt r1,#0x4220); angle pi/2 (0x004E5A4E); translation {0,-100,-22} (0x004E5A70 movt r0,#0xc2c8 / 0x004E5A7E)",
    "type 2 PlaceOnGround 4th arg 0 (0x004E5B5A/0x004E5B80); angle pi/2 (0x004E5AF4); translation {0,-49,-22} (0x004E5B0C movt r0,#0xc244 / 0x004E5B16)",
    "type 3 Entry: 0x004E5DA2 (loop tail), no pose",
    "type 4 Rolling 75.0 (0x004E5C50 movt r1,#0x4296); angle pi/2 (0x004E5BA6/0x004E5BB4); translation {0,-65,-22} (0x004E5BE2/0x004E5BE8)",
    "type 5 Flipping 4th arg 0 (0x004E5D04/0x004E5D28); angle 3pi/4 (0x004E5C9C vadd d11); translation {dimX/2+56.5771,-56.5771,-22} (0x004E5CC0 vstr s18 / 0x004E5CB8 movt r0,#0xc262 (0xC2624EEF) / 0x004E5CC6 vstr s16)",
    "PreActionPose ctor 0x0050DCFC stores +0x18 at 0x0050DD46; second ctor 0x0050DB14",
    "SetHeightTolerance 0x0050DAA4 writes +0x14 at 0x0050DB06 (0.5773503 at 0x0050DB10)",
    "the +0x18 value consumer: ActionableObject::GetCurrentPreActionPoses 0x004DF850, 0x004DF9DE vldr s28,[sl,#0x18], stride 0x004DFDC4",
    "ComputePreActionPoseDistThreshold 0x00550098: 3-D distance (0x00550102 vldr s0,[r0,#0x20]; 0x00550122 vsqrt.f32); out[0]=2*dist*sin at 0x00550164/0x005501A4, out[1]=dist*sin at 0x005501A8",
    "the only tolerance branch is angleTolerance > Radians(0): 0x005500BC blx 0x4A4528 / 0x005500C0 cmp r0,#0 / 0x005500C2 beq 0x5501AE; operator> 0x0084CC90 with the ~1e-5 epsilon 0x3727C5AC; false writes -1.0f at 0x005501B6; a GetWithRespectTo failure writes -1.0f at 0x005501C0; no floor",
    "Block::LookupBlockInfo symbol 0x004E4C8C; 0x00503DC8 (75.0) is inside MinimalAnglePlanner::ComputeNewPathIfNeeded 0x00503C18 and unrelated",
    "the world-pose ctor PreActionPose 0x0050DF00 (PLT 0x4A4144): 0x0050DF54 vstr s16,[sl,#0x18] (a); 0x0050DF6C Point3::MakeUnitLength 0x0050E0C0 returns |t|; 0x0050DF7C vadd (|t|+b); 0x0050DFE4 operator*(Pose3d,Pose3d) 0x0084782C; SetHeightTolerance 0x0050E036",
    "GetCurrentPreActionPoses 0x004DF850: first ctor call 0x004DF99A; param_7 from 0x00550984 ([robot+0x10], named preDockPoseOffset_mm at 0x0055137A); param_8 = 0 on this path (0x00550A2A); second ctor call 0x004DFBE6 with b = min(dist2, d) (0x004DFB88 vldr s0,[sl,#0x18]; 0x004DFB94 bpl); the 0x004DFCFC..0x004DFD00 store is the param_8==1 viz block (0x004DFC5E)",
    "all three production threshold callers read both outputs with one ldrd (see M12-020)",
    "preDockPoseOffset_mm: 0x00550984 vldr s16,[r7,#0x10] (r7=r1=PreActionPoseInput); IDockAction+0x10 left 0 by IActionRunner 0x0053FDD0 strd r6,r6,[r5,#0x10]; input+0x10 from IDockAction+0xBC (0x0055161A, 0 at ctor 0x005503AA) and DriveToObjectAction+0x88 (ctor 0x005585A4; no setter; direct callers pass 0); offset-0 branch b=min(dist2,element+0x18)",
    "GetCurrentPreActionPoses param_2: 0x00550A00 bl 0x004EA398 -> &(Robot+0x298); FUN_004df628 0x004DF628 = pose.GetWithRespectTo(pose.FindRoot()) (0x004DF674/0x004DF67E); objectPose = ObservableObject::GetPose 0x00876840 = &(ObservableObject+4)",
    "sb rotation: 0x004E5870/0x004E5890/0x004E58B2/0x004E58D4 blx 0x4A47B0 (Y_AXIS_3D); base 0x010590AC (0x004E5876/0x004E587A); angles 0x3FC90FDB/0x40490FDB/0xBFC90FDB; applied with Transform3d::RotateBy 0x0084BB3A (0x004E5A1C/0x004E5AC0/0x004E5B60/0x004E5C36/0x004E5D0A)",
    "Pose3d(angle, Z_AXIS, translation) with the marker as 4th ctor arg (parent): 0x004E59EE strd r0,r6,[sp]; precompute 0x004E592C..0x004E5938 s16=-size[8]/2, s18=size[4]/2+56.5771; PreActionPose ctor re-roots 0x0050DDA2..0x0050DDB2 passedPose.GetWithRespectTo(marker.parent)",
    "0x004DF88A..0x004DF8E8 collection; 0x004DF94A..0x004DF99A filters; 0x004DF9B8..0x004DFBE6 the b computation; 0x004DFC1A..0x004DFC58 the validity push; 0x004DFDD4 the return byte",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q2; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md (2.3 formula, 2.2b)"
  ]
}
```

### M12-045

```json
{
  "title": "UnSetCarryingObjects: ascending-ID walk, NullObject error, StillAttached / StillAttachedToSomething warnings, SendSetCarryState(CARRY_NONE) tag 0x49 reliable, carry field resets; and AddRobotRelativeObservation (re-parent to robot, flatten to root, SetPoseHelper state 2 with the broadcast, confirmer entry)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261014-m12-006-unattach.md: U1-U5 0x00633C0C..0x00633D80, SendSetCarryState 0x00632C44..0x00632C6E (tag 0x49); B1-B7 AddRobotRelativeObservation 0x00506B78..0x00506C48, SetPoseHelper 0x0050609C..0x005060FA, ActionableObject::SetPose 0x004E01E4..0x004E0226, Vision SetPose 0x004EF590..0x004EF646; callers C1-C9."
  ]
}
```

### M4-019

```json
{
  "title": "CliffSensorComponent: defaults, CliffEvent/PotentialCliff, the EnableStopOnCliff senders, the 50/400/150 schedule with its running stats, HandleRobotStopped's Evaluate and broadcast gate",
  "status": "EXACT_SOURCE",
  "evidence": [
    "SC1..SC8, SC4a..SC4j, SC6, C2, C7, C8 as before",
    "H1 HandleRobotStopped is subscribed for robot tag 0xD4 (0x00532B6E, GOT 0x0103EA8C)",
    "H2 0x0053539C..0x0053542E body: Get_robotStopped, sEventF, EvaluateCliffSuspiciousnessWhenStopped",
    "H3 0x00634840..0x00634846 Evaluate = comp+0x1C := robot+0x2C",
    "H4 0x00535432..0x00535480 if the sensor is enabled: RequestCurrentBehaviorEndImmediately, ActionList::Cancel(-1), broadcast RobotStopped",
    "S1 0x00512FB0..0x00512F22 stats/threshold run for frame-mismatched states for the first 100",
    "S2 0x006343C6..0x006343D6 gates +0x1C != 0 and MC+0xC == 0",
    "S3 0x006343D8..0x00634404 std::map::lower_bound(comp+0x1C) over RobotStateHistory at robot+0x390",
    "S4 0x0063448E..0x006344AA in-order successor walk",
    "S5 0x00634420..0x0063448A min(cliff[0]); Increment when variance > 10000.0f and min+15 < cliff",
    "S6 0x006344AC..0x006344AE clears +0x1C at the end of the walk",
    "S7 0x0063451A..0x006345AA Increment: cache < 151 returns; else cache = max(cache-250,150), send, count=0",
    "S8 senders: 0x00511DA0, 0x00512DA2 (50), 0x00512EA0 (400), 0x006345A4, 0x006347F8",
    "W1 0x00634636..0x00634656 sample gate: moving, robot+0x355 == 0, state+0x50 > cache",
    "W3 0x0063466A/0x006346D0..0x006346FC add-only Welford with n = size",
    "W4 0x0063466C..0x006346CE removal with N = the constant 100.0f (0x00634728), not the deque size",
    "W5 0x00634700..0x0063471E M2 += s0; variance = M2/(size-1) once size >= 2",
    "W6 0x00634668 the window is 100 samples",
    "C12.3 robot+0x2C0 is per-mismatch-run: 0 at the ctor 0x0050FF08, 0 on a frame match 0x00512EAE, 0 on the treads Delocalize 0x00512B9C, old+1 on a mismatch 0x00512F1C, 0 after 0x65 0x00512F88",
    "C12.4 the stats gate r6=0 is false on the AddRobotStateToHistory failure (0x00512BEE/0x00512BF4/0x00512C38), the GetLastStateWithFrameID failure (0x00513088/0x00513110) and the origin miss (0x00512F0C)",
    "C13.2 the treads-change Delocalize stores +0x2C0 = 0 at 0x00512B9C then b 0x512FB4, skipping the frame compare, the frame-match reset and the mismatch increment; the stats run unconditionally",
    "C13.3 RobotStateHistory ctor sets a 3000 ms retention window at +0x3c (0x0053088D..0x005308C6); the stack's parallel cliff history mirrors it",
    "Handoffs in HandleRobotStopped: the gate ldrb r0,[r0,#4]; cbz 0x00535436..0x00535438; BehaviorManager::RequestCurrentBehaviorEndImmediately blx 0x4AA1C8 0x00535454 (M8-019); ldr.w r0,[r4,#0x250]; mov.w r1,#-1; blx 0x4A7ABC ActionList::Cancel(-1) 0x00535466..0x0053546E (M8-015); the RobotStopped game message blx 0x4AA1D4 0x00535478 and Robot::Broadcast 0x00535480. In UpdateFullRobotState: ldr.w r0,[r4,#0x27C]; blx 0x4A7C6C RobotGyroDriftDetector::DetectGyroDrift 0x00512FB4..0x00512FBA and blx 0x4A7C78 DetectBias 0x00512FC4 (M10-015). The history lookups (C12.4): M11-051/M11-053; the charger geometry: M13-009",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 6 (Opus extraction, manager-adopted 2026-10-11): the full log rows (level, channel, key, format, arguments, gates, errG/break and order) and the differences from the C#."
  ]
}
```

### M10-001

```json
{
  "title": "Off-treads classifier CheckAndUpdateTreadsState: gate, inputs, thresholds, branches, 250 ms debounce commit and every consequence",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "A1..A7 CheckAndUpdateTreadsState 0x511E00..0x5121F8 (thresholds 0x5121FC, 0x5122A0..0x5122BC)",
    "A8..A15 consequences: Falling DAS + ActionList::Cancel(-1) (0x511FF0..0x512084; gap1 3a..3e), RobotOffTreadsStateChanged broadcast 0x512092, OnTreads 0x512112..0x512184, carried 0x512100, SetOnChargerPlatform 0x512188, pause flag 0x5121E2",
    "gap1 5a IMU filter state zeroed (0x5100E0..0x5100FE)",
    "R-ANIM pre-extraction part 2 item 1 O1..O4: Radians operator> 0x84CC90, IsNear 0x84CC0A, operator< 0x84CD12, rescale 0x84C87C"
  ]
}
```

### M12-006

```json
{
  "title": "Letting go of a carried object leaves it Dirty where the lift left it",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x006333D4 SetCarriedObjectAsUnattached; 0x00633458 AddRobotRelativeObservation; 0x00633930 DeleteLocatedObjects",
    "BehaviorPutDownBlock passes false: 0x005C84CE",
    "PickupObjectAction::Verify passes true: 0x00553CAA, 0x00553D2A",
    "HandlePickAndPlaceResult 0x00533780: release only when blockStatus==1 (0x005337A8 cmp r0,#1 / 0x005337AA beq) and success!=0 (0x00533898 ldrb r0,[r5,#4] / 0x0053389A cbz 0x5338a6), with bool false (0x005338A0 movs r1,#0 / 0x005338A2 blx 0x4A7B7C SetCarriedObjectAsUnattached)",
    "attach only when blockStatus==2 (0x005337A4/0x005337A6) and success!=0 (0x00533848 ldrb / 0x0053384A cbz), via SetDockObjectAsAttachedToLift (0x00533850 blx 0x4AA06C)",
    "PickAndPlaceResult layout: +4 success (0x00533794 ldrb r1,[r5,#4]), +5 dockingResult (0x005337F8 ldrsb), +6 blockStatus (0x0053379E ldrb r0,[r5,#6])",
    "VisionComponent::EnableMode(1,true) runs unconditionally in the BlockPlaced branch (0x005338AE blx 0x4A91CC)",
    "re-analysis/research/20261014-m12-006-unattach.md: A1-A12 SetCarriedObjectAsUnattached 0x006333D4..0x006339A2 (warnings/errors CarryingObjectNotSpecified, CarryingObjectDoesNotExist, OriginMisMatch, Top*, Info ObjectPlaced; the robot-relative pose w.r.t. Robot::GetPose 0x004EA398; AddRobotRelativeObservation Dirty; GetCarryingObjects before UnSet; deleteLocated filter); callers C1-C9."
  ]
}
```

### M2-013

```json
{
  "title": "FallingStopped 0xDE is {u32 timestamp, u32 duration_ms, f32 impactIntensity}",
  "status": "EXACT_SOURCE",
  "evidence": [
    "Unpack 0x007B0EB6: three ReadBytes(4) at +0, +4, +8",
    "HandleFallingStopped 0x00535040: logs \"timestamp: %u, duration (ms): %u, intensity %.1f\" (0x00535274) from ldrd r1,r2,[r5] (0x0053506C) and vldr s0,[r5,#8] (0x00535068); [r5+8] > 1000.0f triggers NeedsManager action 0x11 (0x005350AA..0x005350C2); +4 to to_string(unsigned) (0x005350CC); the game message {duration_ms, impactIntensity} from ldrd [r5,#4] (0x00535186)"
  ]
}
```

### M2-020

```json
{
  "title": "Robot-message dispatch residue: wildcard ID 0x101CF (all-signals branch unreachable), handle reset/unlink, re-Init appends before release, ~Messaging retirement order, TracePrinter B0/EC registrations; each owner's own handle wiring is owned by that owner's record",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261013-robot-message-subscribers-check.md: Emitter 0x0069DE18: robot ID == MessageHandler+0x1C (0x1CF, set 0x0069D004/0x0069D00E) emits every robot signal for the tag (0x0069DE32..0x0069DE4E); otherwise the robot signal, then the 0x1CF signal (0x0069DE50..0x0069DE74). LoadBehaviors is called from LoadNonConfigData 0x0051F452; its order against AddRobot is unread.",
    "re-analysis/research/20261013-dispatch-residue-check.md: wildcard 0x101CF at 0x0069D004..0x0069D00E; all-signals 0x0069DE32..0x0069DE4E unreachable (only caller 0x0069DABA with robot 1, 0x004ED07A); Reset 0x0051D8F8..0x0051D932, unlink 0x0051D338..0x0051D3A4; owner releases as tabled; re-Init order 0x00543FCA..0x00543FE2, 0x00551706..0x00551784."
  ]
}
```

### M1-056

```json
{
  "title": "CozmoEngine::Update states 1 and 2: HasDesiredNumUiDevices, SendSupportInfo, SetEngineState(2); the per-tick loading poll and EngineLoadingDataStatus; delivery of the Init-time broadcast with no UI device",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/overnight/20261017-ov124-check.md: switch 0x004ED5C4 (engine+0x62), tbb 0x004ED5CC; state 1: HasDesiredNumUiDevices == 1 at 0x004ED5D8, SendSupportInfo 0x004ED5E2, SetEngineState(2) 0x004ED5EA; state 2: loading case 0x004ED5F0, then always 0x004ED612..0x004ED618 -> 0x004ED7C0 (EngineLoadingDataStatus ctor 0x004ED7DE, E2G slot +0x1C 0x004ED7E6; context+4 dereferenced without a null check 0x004ED7D2); progress out-parameter written every call before the done test 0x00523576..0x0052357E; UiMessageHandler::Broadcast 0x006625B0 calls [vtbl+0x48] with r2 = -1 (0x006625CA..0x006625CE), then local dispatch 0x006625FA.",
    "re-analysis/research/overnight/20261017-ov125-ov126-check.md: StartEngine handler 0x004ECBDA (seed, SetLocale 0x004ECBE2, SetEngineState(1) 0x004ECC40..0x004ECC4A); gate 0x006632F4..0x0066330E (desired 0, 0x0065DC50); Direct auto-connect 0x00662ACE/0x00662AEE, 0x0065C4C4..0x0065C4D0, first receive 0x0065C44C..0x0065C458 via ProcessMessages 0x00662860 (UI+0xF1); transition 0x004ED5D8..0x004ED5EE; SetEngineState 0x004ECCBC: store +0x62 0x004ECCD2, E2G UpdateEngineState tag 0x7B {old,new} (0x004EDC12..0x004EDC2E, 0x007276F8, 0x00719F7E..0x00719F9E), telemetry 0x004ECD2A; progress writers 0x0051EF3E/46, 0x0051F9B0..C0, 0x00521684/0x005216A6/0x005216D8, 0x0051F3F2/0x0051F424, 0x0051F480..8C, 0x005203E8; poll 0x00523576..0x005235E2; null DataPlatform 0x0051F394 (no 1.0: hang)."
  ]
}
```

## Exact historical bundle MISSING observations

These are observations at old bundle commit, not current defect classifications.

- 1× `MISSING: M1-058 StartEngine.SetLocale0x004ECBE2: host locale language/country not supplied; no invented locale default`
- 1× `MISSING: M13-038 PublicStateBroadcaster::Update0x0064D5DC native manager/current-requested/carrying/reaction/needs input joins are unbound; checked broadcaster and both client owners are built, no host Freeplay state substituted`
- 1× `MISSING: M7-027/M12-017: concrete PlayAnimation CA/CB/D5/5F and dock C5/DA/+E4 slots are built; complete native dock scheduler/destruction remains M12-017/M12-034. Dev-behavior container destruction points remain unbuilt; TracePrinter B0/EC diagnostics and CF CrashReport are bound`
- 1× `MISSING: M7-028 LiftLoadTest[0] IBehavior base constructor 0x005D3F18, 0x35/0x95 callback bodies and subscription destructor ordering remain unread; managed subscription storage retires after robot lifetime teardown`
- 1× `MISSING: M7-028 LiftLoadTest[1] IBehavior base constructor 0x005D3F18, 0x35/0x95 callback bodies and subscription destructor ordering remain unread; managed subscription storage retires after robot lifetime teardown`
- 1× `MISSING: M7-002/M7-014 InitReactionTriggerMap 0x005A1842/0x005A1864: FistBump -> FistBump has no constructed strategy/behavior binding before Freeplay; trigger entry retained, no placeholder behavior`
- 1× `MISSING: M7-002/M7-014 InitReactionTriggerMap 0x005A1842/0x005A1864: Hiccup -> Hiccup has no constructed strategy/behavior binding before Freeplay; trigger entry retained, no placeholder behavior`
- 1× `MISSING: M7-002/M7-014 InitReactionTriggerMap 0x005A1842/0x005A1864: Frustration -> ReactToFrustrationMajor has no constructed strategy/behavior binding before Freeplay; trigger entry retained, no placeholder behavior`
- 1× `MISSING: M7-002/M7-014 InitReactionTriggerMap 0x005A1842/0x005A1864: NoPreDockPoses -> RamIntoBlock has no constructed strategy/behavior binding before Freeplay; trigger entry retained, no placeholder behavior`
- 1× `MISSING: M7-002/M7-014 InitReactionTriggerMap 0x005A1842/0x005A1864: Sparked -> ReactToSparked has no constructed strategy/behavior binding before Freeplay; trigger entry retained, no placeholder behavior`
- 1× `MISSING: M11-075 MapComponent ctor 0x0067D9FE..0x0067DB48 G2E144/145 scoped subscription producer/retirement is not bound; raw handler bodies exist but no public map-frequency API or substitute callbacks are installed`
- 1× `MISSING: M11-072: precise native component/container subscription retirement points are not supplied by the checked recipient rows; managed token storage retires at Robot destruction end. Full teardown ordering remains M8-023/M7-018; no native destructor ordering is claimed for these new UI handles`
- 1× `MISSING: M11-066 Robot ctor main (+0x298) and forward (+0x2A4) pose names have no checked row; empty names retained`
- 1× `MISSING: M11-077: RemoveOwner is built (0x008448B4..0x00844914); NotLastOwner 0x0084510E..0x0084515E needs native shared-control use_count, and final-node destruction/bookkeeping 0x0084605C..0x0084609C -> 0x008D148C remains unbuilt; C# reachability is not used as a native reference count`
- 1× `MISSING: M11-038 BroadcastLocatedObjectStates 0x006249EC -> 0x0061E6C0 payload/recipient remains unbuilt; tail call accounted`
- 1× `MISSING: M11-069 AIComponent.OnRobotDelocalized body/recipient is not built here (0x00510D66..0x00510D80)`
- 1× `MISSING: M11-069 BehaviorManager.OnRobotDelocalized body/recipient is not built here (0x00510D66..0x00510D80)`
- 1× `MISSING: M11-069 MovementComponent.OnRobotDelocalized body/recipient is not built here (0x00510D66..0x00510D80)`
- 1× `MISSING: M15-035 Inventory subscription helpers 0x0063CB8C..0x0063CDCC are unread; no guessed command handlers registered`
- 1× `MISSING: M11-059 | the FaceAlbum/Enrollment read pair is queued only past the branch at 0x00650E3C (VisionSystem::Init's outcome, the album load-or-erase branch 0x00650FB4..0x0065108A); that outcome is unread, so this stack queues the pair unconditionally`
- 1× `MISSING: M15-039: writeAsJson 0x0084C328..0x0084C4B6 calls StyledStreamWriter(tab). Its shipped formatting/comment/escaping body has no checked builder row; stats serialization/open/write/close and result-dependent WriteStatsFailed are not built`
- 3× `MISSING: CozmoAPI.CozmoInstanceRunner.overtime (key 0x0065B76C): debug call arguments unported`
- 1× `MISSING: M15-052/M15-062 NeedsState+8 before NeedsManager::Init at0x006A5328, owner M15-062`
- 1× `MISSING: M11-059 | VisionComponent::HandleDefaultCameraParams: VisionSystem::IsInitialized and InitialExposureTime_ms come from VisionComponent::Init / VisionSystem::Init (checked, not yet built: B-M11-7); system readiness remains unbound; the initial-exposure config prefix is built`
- 1× `MISSING: M12-006 C5 HandleMotorCalibration: checked sEventF before SetCarriedObjectAsUnattached 0x00536BAE..0x00536BC4 has no bound exact event payload in this handler; no fabricated native event`
- 1× `MISSING: M11-037 BlockConfigurationManager::SetObjectPoseChanged 0x00624892: configuration manager owner has not been attached`
- 1× `MISSING: M11-037/M11-044 map stamp input 0x0067EFCE/0x0067F0A6/0x0067F32C -> Robot::GetLastImageTimeStamp 0x00516EC0: existing VisionSystem last-image clock is an unsourced stand-in; its camera getter join remains unchecked`
- 1× `MISSING: M8-026 ProcessClearQuad 0x0067E47E: native robot footprint is built; AIWhiteboard owner is unbound`
- 1× `MISSING: M13-038 PublicStateBroadcaster Update0x0064D5DC at Robot::Update: native input owner/caller join is absent; checked Update(input) is built, no publication or tick ordering invented`
- 1× `MISSING: M11-075/M11-046 aging at 0x0067DCF4 -> whole-map TransformContent 0x0068140C/0x0068A414: root geometry, node Transform and subtype comparisons are not integrated; checked aging callback is not applied to legacy polygon regions`
- 1× `MISSING: M11-035 (owner of the UpdateAllResults result; the gate is M3-032) | Robot::Update 0x00513C76..0x00513C9A tests the VisionComponent::UpdateAllResults result and returns when it is non-zero, and 0x00513C6E..0x00513C74 tests [[robot+0x258]+0x28] != 0 before it: the real calibration gate is applied; M11-035 has no UpdateAllResults status hook`
- 1× `MISSING: M11-040 SetNextImage timestamp rollback 0x00652B36..0x00652B76: last-received timestamp +0xE4 owner is unbuilt; rollback of +0xE4/+0xE8 is not substituted with the processed clock`
- 1× `MISSING: M11-055 Processor VisionPoseData/IMU/Mat/pose temporary copy/swap/release 0x0065204A..0x00652200 remains M11-040/M11-077/M11-078; the mailbox carries a host CameraFrame, not native shared-control ownership`
- 1× `MISSING: M11-033/M11-078 native ImageCache ResetHelper size-tree and image shared-control ownership 0x008744E6..0x0087465C are not replaced by host buffer assignment; bounded decoded image/valid flags only`
- 1× `MISSING: M11-058/M11-055 DecodeJPEG Tic/Toc 0x006B4B76/0x006B4C6C native timing producer unbuilt; no host timer substituted`
- 1× `MISSING: M15-036 | the Lab read's callback 0x006A6486 into CozmoExperiments::RestoreLoadedActiveExperiments 0x006A5C34: this stack has no component for it, so the read completes and its result is dropped`
- 1× `MISSING: M11-080 detector rectangle producers (DetectMarkersWithCLAHE/DetectFaces/DetectPets -> 0x006B2D4C): which rectangles are appended is not checked; no empty/guessed ROI vector supplied`
- 1× `MISSING: M15-030/M15-014 needs robot persistence skipped: system_clock epoch producer 0x00694D9C/0x006954F8/0x0069555C and economy fields 0x006955F2..0x0069560C are unbuilt; robot copy/resolver and NV erase/write 0x194000 are gated; also skipped: M15-050 Lab writeback join 0x00694622, SendNeedsStateToGame (case 1 NoAction, cases 5-7 Decay), RobotChangedFromLastSession (cases 3, 5-7)`
- 1× `MISSING: M11-013 / M11-041 | CozmoCubes host ByObjectId/ConnectionChanged still expose reported radio slots, including synthetic slots above4; source HandleActiveObjectConnectionState 0x00533B3C uses the Robot-owned BlockWorld returned ID and its own slot gate. Host-view migration remains unbuilt.`
- 1× `MISSING: M4-040 | EnableCubeLightsStateTransitionMessages ingress (0x0063A2EA) is not wired; comp+0x20 remains false`
- 1× `MISSING: M8-026 Robot::GetWorldOrigin 0x004A4C54 to AIWhiteboard 0x0056AC92: native world-origin capture provider is not bound; ProcessClearQuad production unit is built`
- 1× `MISSING: IBehavior::IsRunnableBase robot+0x34a (0x005bd876): no source for the byte; the vtable+0x24 gate is not consulted`
- 1× `MISSING: IBehavior::IsRunnableBase +0x74 state (0x005bd7e6) and +0x70 unlock (0x005bd810) inputs: no source in this stack; not tested`
- 1× `MISSING: Robot::Update's behaviour debug string (robot+0x4C = activity + ' ' + BehaviorID + '-' + IBehavior+0x58, 0x00513F6A..0x00513F9C) sent to VizManager::SetText and SetSdkStatus(Behavior): the sender and the SDK-status path are M11/M12 and not built`
- 1× `MISSING: M8-015 host action producer at IBehavior::StartActing 0x005BDACC supplies a handle/async closure instead of a concrete IActionRunner; owned-runner StartActing is built, concrete host-action construction remains unwired`
- 1× `MISSING: DriveStraightAction(Robot&, float) 0x005C0C08: the two-argument constructor's speed and animation defaults and its Init/CheckIfDone are not in the M13 inventory; this stack's DriveStraightAction default speed is used`
- 1× `MISSING: IBehavior::Init/Resume 'SparkBehaviorDisables' lock (0x005BCD16..0x005BCD44, 0x005BCFAC..0x005BCFDE): this+0x70 and [[this+0x2C]+0x44]+0x58 (the active spark) have no source in this stack (M15 spark layer); the lock is not taken`
- 1× `MISSING: M13-013 | AppendLine failure branch 0x005476AE remains unbuilt on the host Path geometry candidate`
- 1× `MISSING: M12-002 PathSegment.Print 0x00507F36 -> 0x0085C034 text/body has no checked row; segment print is unbuilt`
- 1× `MISSING: M12-032 VizManager.DrawPath 0x0064A474 recipient/color handoff is unbuilt`
- 1× `MISSING: M13-013 | CheckIfDone info/debug arguments at 0x00547904/0x0054794E/0x005479AE are unread`
- 1× `MISSING: M8-021/M8-005 concrete TriggerLiftSafeAnimationAction callback lifecycle is incomplete: completion string union, trigger DAS blacklist/event and cached-pointer retry/destruction semantics remain unbuilt; checked CA/CB/D5/5F bodies use robot events rather than playback-ticket completion`
- 1× `MISSING: M8-015 Queue.Update RUNNING VizManager.SetText0x0053F65C and CozmoContext.SetSdkStatus(type1,name)0x0053F6F2 recipients are not bound; normal queue path is not classified SDK-only`
- 1× `MISSING: M7-026 SimpleMood; Default retained because GetSimpleMood is not represented`
- 1× `MISSING: M7-026 MoodManager+0x130 clock input (0x0058A95C/0x0067B67E); existing BaseStationTimer stand-in remains unverified`
- 1× `MISSING: RobotAudioClient::CreateAudioAnimation post-construction check 0x0059A0BA: a constructed animation whose state byte is 4 or 5 (AnimationCompleted / AnimationError, set by InitAnimation 0x00596948 / 0x00596972) is deleted, logged and [client+0x38] set to 0; the stand-in has no state byte, so the check is not made`
- 1× `MISSING: M8-026 GetWorldOrigin 0x004A4C54 at 0x0056AC92 has no bound Robot/world-origin provider; ProcessClearQuad unit is built`
- 1× `MISSING: M8-015 Queue.Update terminal VizManager.SetText(empty)0x0053F696 / CozmoContext.SetSdkStatus(type1,empty)0x0053F6B4 recipients before Delete are not bound`
- 2× `MISSING: M8-023/M8-027 ProtoHandle callable destruction0x0066350E and node+0x20 release0x0066352E..0x00663560: native captured-owner finalization/producer census is unbuilt; managed delegate retirement does not certify no side effects`
- 1× `MISSING: M8-015 destructor owns-profile byte+0x58/ClearCustomMotionProfile0x005410F6 recipient is not bound; Path.HasCustomMotionProfile is a different input; no invented profile reset`
- 1× `MISSING: IBehavior::BehaviorObjectiveAchieved(BehaviorObjective, bool) body is not in the inventory; BehaviorDriveOffCharger's completion callback (0x005C0EAC) only traces the call`
- 1× `MISSING: M4-018 | Update.NoAnimsLeftOnLayer 0x00637BD6: native ObjectID unavailable`
- 1× `MISSING: M11-004 | CubeLightComponent::PickNextAnimForDefaultLayer reads the located object's +0x24 (0x00637DA6..0x00637E02); no BlockWorld hook is wired (IsVisible), so the Visible trigger is never chosen`
- 1× `MISSING: M11-055 V3-V7 0x00652580..0x00652718 / 0x006B1E92..0x006B2142: native VisionSystem/member ownership and recursive recipient teardown are not built; host subscriptions are detached after join`
- 1× `MISSING: M11-078/M11-090 Vision teardown: native result lists/deque 0x006586EC..0x00658A92/0x006B9F68..0x006BA092, ImageCache null-before-tree 0x006B1E9C..0x006B2142, Camera/ImageCache/CLAHE, FaceRecognizer trees, Block/MarkerlessObject and ProtoHandle reset/remove need their owned native recipients; host caches/GC are not those owners. Capture finalizers and allocator recipients remain unread; SDK CustomObject is unreachable`
- 1× `MISSING: M11-055 FaceRecognizer::Shutdown 0x00862852..0x00862866, 0x008628C6..0x00862982: native worker/merge-feature/feature ownership has no producer in the stack; only the owned album recipient is built`
- 1× `MISSING: M11-055 Profiler timer producers/Tic/Toc and VisionSystem::Init PerformanceLogging binding 0x006B0E5C..0x006B0F2A are not built; native timer input boundary only, no host Stopwatch data substituted`

## Whole-pilot clarification after initial ledger (2026-10-26)

The earlier CM13-05 preservation recommendation was a bounded, no-status-upgrade architecture, not source recovery or operator approval of a standalone API policy. Whole-pilot closure cannot inherit that assumption. A narrow current manifest/inventory search finds no ConfigurationSeen/FirstSeenSec policy or record. The event is public and invoked by the contact-triggered frame-tail rebuild, so its output3 provenance must be resolved, or the operator must explicitly decide its standalone-extension/replacement/exclusion contract. No manager/Codex exclusion is implied. Independent native reading of BehaviorOnConfigSeen005DB2D4 confirms per-type cache COUNT growth stamps the behavior's floatclock, with evaluationdt<4.99999 and abs(stamp-now)<1e-5; it does not establish per-entry first-seen history or this public event. The source behavioral consumer should not remain on the old per-entry heuristic under a whole-pilot completeness claim.

Preliminary independent new-primary checks (known.py before relevant function reading) while extractor graph remains open:

- DriveStraight.Init00547628..6AA reads heading from checked MAIN pose through004EA398; XY comes from005477A0's stored Robot+2A4 drive-center pose. f32distance*cosf/sinf then f32center-additions feed AppendLine; action+7C/+80/+84 supply speed/accel/decel. Current host line geometry using a single main-pose double calculation is a real mismatch candidate.
- Robot.SetPose0051502C..C0 copies Main, then reads raw CarryingComponent+8:FFFFFFFF selects-20; all other values literal+0 at00515138. MoveRobotPoseForward writes Robot+2A4. ComputeDriveCenterPose005135D0 has the same rawcarry branch and+0literal005135F4. This is separate from CM13-04 ComputeOriginPose's+20/-0.
- Actual TWO-argument constructor005470F0 uses speedtable00547268=-80 and0054726C=100 according to floatdistance sign, accelbits43480000=200/decel43FA0000=500, started/speedexplicit flags zero and animationflag1. The four-argument overload delegates to it and is not the drive-off constructor call.
- GetSimpleMood0067C084 starts Default2, setsHappy0 forHappy>.33, then Sad1 overrides forConfident<-.29 orHappy<-.33. Exact literalbits verified. GetCurrentTimeInSeconds0067ADA8 resolves BaseStationTimer through its veneer; full mood input/update binding remains to finish.

These are independently checked witnesses, not a whole-native read PASS or authorization to implement fragments. Pending A's complete graph, API decision/reconciliation and separate final whole read check. No code/tests run or changed.

### Incremental whole-pilot caller checks (2026-10-26; no whole-read verdict)

Independent known.py preflights and primary ELF reads confirm the final IBehavior completed-action subscriber: vtable010265A0+18 contains005BF0BF;005BF0BE loads captured behavior+4,005BF0C2 adds message+10,005BF0C6 unwraps RobotCompletedAction, then005BF0D2 tails to008CBFAC. Its bytes7847c046 switch Thumb to ARM;008CBFB0 loads the literalFFBE6A44 and008CBFB4 adds PC008CBFBC, reaching PLT004B2A00 HandleActionComplete. This closes that dispatch join; it does not certify generic behavior admission/factory state.

AnimationGroup0058A954 calls real GetSimpleMood and0058A95C passes raw MoodManager+130 as the animation draw clock. RobotUpdate00513E84..00513E8A passes its frame-clock r5 into Robot+440 Mood.Update. These establish the real owner/cache bindings required instead of the current host-facade Timer input. Existing model-body evidence remains separate from live attachment correctness.

ActivitySocialize constructor005AFD1C does not initialize phase+94: the memclr at005AFD38..3C covers68..83; explicit later stores cover98/9C/A0/A4/A8/AC/B0/B4. A claim that the fresh object starts at phase0 is unsupported. Post-OnSelected/GetDesired phase invariants must be distinguished from any pre-selection subscriber invocation. An actual uninitialized native read, if reached, is an M1-034 boundary rather than an invented initial enum value. Extractor is narrowing that call availability; no blanket objective4 no-effect conclusion is made here.

Current C# consumer check: CubeGameBehaviors Recent reads Cache and FirstSeenSec as one host adapter; independent cache-count consumers (including1021), membership and tallest require the source cache. PyramidBase.InteriorMidpoint is a nonnullable cached host Vec3, while the native base contains IDs and a separately failing live midpoint query. The whole adapter decision in trial-m13/BLOCKED.md remains pending; no changed API or native projection policy is assumed. No code, tests, status or acceptance changes were made.

### Defined OpenCV failure: independently checked catch coverage

After known.py preflight, actual libopencv_imgproc instructions0009CA1E..0009CA7A compare the f64 orientation cross with zero and exhaust the scan when all are zero.0009CA7C..0009CAB4 builds cv::error with code-215 and line166; PLT00017170 names cv::error(int,cv::String const&,char const*,char const*,int). This is a defined failure route, not evidence for false or a port-unavailable result.

Crucially, the engine exception-table mapping was independently decoded rather than inferred from a nearby handler: .ARM.exidx entry00BAD934 names function004E6490 and extab00AE4AE4. Generic LSDA starts00AE4AEC, LPStart omitted, typeencoding00, typebase00AE4BE4, callsiteencoding03 and234-byte table at00AE4AF4. The callsite row start0x64,length0x0E,landing0x136,action1 covers004E64F4..004E6502 (minAreaRect BLX004E64FE) and targets004E65C6. Action00AE4BDE bytes0100 means typefilter1/no next action; its type entry00AE4BE0 is null, establishing catch(...). There is no unresolved cv exception-type filter for this call.

The decoded handler begins __cxa_begin_catch004E65C6, logs004E672C, stores error flag004E6762, calls Rectangle::InitFromPointContainer004E6778, computes center/size in separate f32 instructions004E6794..004E67B4 with angle+0, ends catch004E67C0 and rejoins RotatedRect::points004E6512. The already checked numerical RectangleFallback helper is reusable. The missing production integration for this known throw is a build gap; it must not be converted to native no-hit, UNKNOWN, or M1-034 skip. Other managed port-unavailability exceptions remain separate and cannot automatically use this catch. No build/status claim is made.

### Vision result-drain gate: independent bounded confirmation

The current M11-035 title/status/evidence quoted above remains the owner of this result; M3-032 owns the downstream gate. Actual006542EC instructions initialize aggregate sp+88 to0 at00654480/482, reload it per result, promote to1 on a nonzero marker handler result at006544EE/500, and repeat only while CheckMailbox returns1 at00654A74/78. Exit00654AC0..CE reads sp+88, truncates low byte and normalizes nonzero to1. This is a fresh per-engine-drain aggregate (empty drain=false), not the last-ever VisionFrame.Status. The actual C# CozmoEngine gate exists but is unbound; its MISSING path continues the tick. A source-backed producer therefore remains necessary for the whole pilot because native failure skips later platform/mood/AI/action/path work. Other handler error branches remain governed by the extractor's complete row, not newly inferred here. ActualProcessedImage00654A02..0A conditionally stores r5 when mask0x100 is set; its byteC8 producer must remain explicit in the final inventory.
