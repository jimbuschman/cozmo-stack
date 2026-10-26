# Whole drive-off pilot: source read and dependency graph

Native address/field offsets are hexadecimal unless stated otherwise; ordinary counts and real values are decimal. Actual selected model/reasoning telemetry is unobservable; requested gpt-6.1-sol medium. Read-only extractor, 2026-10-26. PLAN-BEHAVIOURS supersedes fragment queue. Current worktree rebased; CM13-05 source draft is paused on safetybranch, not production. No code/tests/git/manifest edits.

**Complete submitted source inventory. Independent engine read PASS:** `20261026-driveoff-pilot-read-codex-check.md`. Public configuration adapter policy remains pending. This is source-reading verification, not build verification, behavior completion or subsystem acceptance. Previously source-checked pieces are reused; tests and hardware success do not upgrade provenance.

| Pilot path | Current extraction coverage |
|---|---|
| Shipped config and IBehavior construction/lifecycle | READ: source factory/base config/runnable/lifecycle/selection reused; current attachment gaps inventoried |
| Direct DriveStraight constructor/Init/check/destruction | Read; main heading versus stored drive-center XY discovered; existing ForwardPose producer identified; actual carrying-owner binding distinguished |
| Custom path, path status, wire and sensor completion | Existing adopted P1–P31 source; exact direct path checked; no lattice planning |
| Contact/platform/off-treads/cliff clock | Existing checked ingress/threshold/classifier source; current binding comparison |
| Reaction locks, severe need, driving animation | Prior checked source and built owners; native group/last-update clock read closed; current attachment remains implementation gap |
| Objective4→E2G/local recipients | READ: producer/serializer/delivery; shipped Sparked filter; selected Socialize invariant and pre-selection M1-034 boundary |
| Mood config/startup/watcher/trigger/update/API | Source checked EM/AM rows reused; engine owner/update/clock currently unbound |
| Contact→world/map/config→tallest/audio | Causal conditional path established; common container part necessary, BuildPyramid callbacks excluded |
| Entire read | Complete submitted source inventory; independent engine read PASS in 20261026-driveoff-pilot-read-codex-check.md; public adapter policy pending; no build verification/acceptance |

## Current authoritative record quotes

Literal current title/status/evidence follow before relying on them. Record statuses are unchanged. Relevant unresolved fields are read directly and discussed per path; quotes do not claim entire-record completeness.

```json
{
  "id": "M13-017",
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

```json
{
  "id": "M13-040",
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

```json
{
  "id": "M12-002",
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

```json
{
  "id": "M12-032",
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

```json
{
  "id": "M12-024",
  "title": "DrivingAnimationHandler PlayStartAnim / PlayEndAnim",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0055AC08 PlayStartAnim call; 0x0055B0AA..0x0055B0CC PlayEndAnim call and its return handling",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1c",
    "re-analysis/research/20261013-m12-024-driving-lifecycle.md: DriveToPose::Init calls handler Init first (0x0055A86C..0x0055A888; tracks +0x54, tag +0x60, suppress +0x56, repeat 0); CheckIfDone state-3 gate 0x0055AB5A..0x0055AB6A; status 3 PlayStartAnim every tick and the every-10th WaitingForPathCompletion info (0x0055AC06..0x0055ACA0, 0xBECBFB/0xBECC32); tail 0x0055B0AA..0x0055B0CC; status 0 and 2 return directly; the result is recomputed after the end animation (state 4); destructor 0x0055A5FC..0x0055A63C (Abort, EraseAllPlannerObstacles(1)/(0), ActionIsBeingDestroyed); no Cancel override; IAction order (TIMEOUT before CheckIfDone, retry re-runs Init) 0x00540D1C..0x00540F9E."
  ]
}
```

```json
{
  "id": "M8-001",
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

```json
{
  "id": "M8-015",
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

```json
{
  "id": "M8-007",
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

```json
{
  "id": "M8-020",
  "title": "PlayAnimationAction: construction, Init, robot/game animation handlers, CheckIfDone, animationCompleted, destructor streamer stop",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m8-play-animation-actions.md rows C1, C8..C11, P1..P12, D1..D3, L2..L8: ctor 0x00543C08..0x00543CBE (+0x84 loops, +0x8B 0, +0x8C interrupt, +0x90 null, +0x94 timeout; 60.0f -> FLT_MAX only when loops == 0), Init 0x00543F20 (cozmo_sings_custom remap 0x0054417C/0x00544190, lookup robot[0]->+0x20->+0x48, SetStreamingAnimation(robot+0x60, anim, loops, interrupt, flag) -> +0x8B, tag 0 -> failed, 0x03000001; R2E 0xCA/0xCB/0xD5 via 0x00519F8C and E2G 0x5F via external-interface vtable +0x28; callbacks 0x005452F1, 0x0054541D, 0x0054561D, 0x005457F5 and AnimationEvent forward 0x00545750), CheckIfDone 0x005441C0, GetCompletionUnion 0x005441DE (0x0075C544, 0x006C22DC), destructor 0x00543DAC (StillStreaming info 0x00543DF2 on robot+0x98 == +0x90; SetStreamingAnimation(null, 1, true, false) at 0x00543E26; releases +0xB4, +0xAC, +0xA4, +0x9C), vtable 0x01021584 slots +0x1C/+0x20/+0x2C (0x00545210).",
    "re-analysis/research/20261012-m8-play-animation-actions-check.md additions 2 (the null SetStreamingAnimation tail: +0x73 store and the idle Abort)."
  ]
}
```

```json
{
  "id": "M8-021",
  "title": "TriggerAnimationAction: trigger to group, Init NO_ANIM_NAME paths, the group choice hand-off, the DAS blacklist gate and the robot.play_animation event",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m8-play-animation-actions.md rows T1..T12, D4, L11..L13: ctor 0x00544228, SetAnimGroupFromTrigger 0x0054432C (Has == 1 before Get; empty-group warning TriggerAnimationAction.EmptyAnimGroupNameForTrigger \"Event: %s\" at 0x005443AC), Init 0x0054443C (NO_ANIM_NAME 0x0300000C; GetAnimationNameFromGroup with the robot and strictCooldown; blacklist set walk +0x8C/+0x90; sEvent robot.play_animation, value +0x78, $data trigger + \":\" + group at 0x005445BC), vtable 0x010215BC."
  ]
}
```

```json
{
  "id": "M7-014",
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

```json
{
  "id": "M7-012",
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

```json
{
  "id": "M7-013",
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

```json
{
  "id": "M7-020",
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

```json
{
  "id": "M7-023",
  "title": "Mood destruction with already-null ActionList dependency",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0067AE14..0x0067AE74; 0x00511150; 0x005111B6; Callback-id/Robot/ActionList gates precede unregister; normal Robot teardown already nulled ActionList, so skips unregister. Remaining destructor children UNKNOWN.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "re-analysis/research/overnight/20261015-ov34-35-check.md: L3 0x005110D4..0x005111C4 (owner nulled after delete 0x005111C4); gates 0x0067AE18..0x0067AE30; children 0x0067AE34..0x0067AE74 (0x004EAE94, 0x0067C0CC, 0x0067C0FC); subscription retirement 0x004EF184..0x004EF1CE, 0x00664EB8, 0x0066365C, 0x00663696, closure destroy 0x0067C228; sole caller BLX 0x005111B6 via PLT 0x004A7A80."
  ]
}
```

```json
{
  "id": "M7-026",
  "title": "MoodManager::GetSimpleMood and the mood time MoodManager+0x130 (inputs to the animation group draw)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m5-x9-extraction.md section 4f: GetSimpleMood 0x0067C084 (Default; Happy > 0.33f -> Happy; Confident < -0.29f -> Sad; Happy < -0.33f -> Sad; constants 0x0067C0C0..0x0067C0C8); the group draw reads MoodManager+0x130 (0x0058A95C), written only by MoodManager::Update(t) 0x0067B67E and Reset/ctor."
  ]
}
```

```json
{
  "id": "M15-059",
  "title": "ActivitySparked lifecycle: OnSelected (HardSparkStartedByEngine tag 131), OnDeselected (StopAllAnims), objective/observed, Update/checks/end/interrupt, get-in/out, Complete (meta.upgrade_replay_* events, HardSparkEndedByEngine tag 132)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov65-67-check.md: OnSelected 0x005B1518..0x005B16DC, 0x005B171C; OnDeselected 0x005B1780..0x005B179E; objective/observed 0x005B181E..0x005B1866; Update/check/end/interrupt 0x005B186C..0x005B1A16; get-in/out 0x005B1D18..0x005B1F2A, hard max 0x005B1F94..0x005B2066; Complete 0x005B1A90..0x005B1C2E, 0x005B2094."
  ]
}
```

```json
{
  "id": "M15-072",
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

```json
{
  "id": "M15-073",
  "title": "App input: NotifyCozmoWakeup (G2E 166) to the SevereNeedsComponent wake handler",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261021-m15-severe-need-input.md (row 39), checked by re-analysis/research/20261025-r-m15-severe-input-claude-check.md.",
    "SevereNeedsComponent::Init 0x00572A64..0x00572A86 subscribes only when HasExternalInterface(); helper 0x00572B10..0x00572B96 registers G2E 0xA6 (166); the callback 0x005734F2..0x00573504 forwards to the wake handler 0x00572E00.",
    "App sender: unity/scripts/csharp/ConnectionFlowController.cs WakeupSequence 600..611 (wake animation, then NotifyCozmoWakeup); Cozmo.Hub/NeedsHub.cs:95."
  ]
}
```

```json
{
  "id": "M5-063",
  "title": "DrivingAnimationHandler bodies: HandleActionCompleted (E2G 0x5A), PushDrivingAnimations (G2E 0xB5), RemoveDrivingAnimations (G2E 0xB6)",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m5-teardown-members.md: callbacks 0x004F110E -> HandleActionCompleted 0x004F0D78 (engine sender: ActionQueue 0x0053FAAA); 0x004F1186 -> PushDrivingAnimations 0x004F0944; 0x004F120E -> RemoveDrivingAnimations 0x004F0A94; registered only if HasExternalInterface() == 1 (0x004F072C..0x004F0732).",
    "re-analysis/research/20261012-m5-driving-handler-read.md: handler state ctor 0x004F06B0..0x004F0878 / Init 0x004F0FC4..0x004F0FF4; Push gate warning 0x004F094C..0x004F0964 and push_back 0x004F0988..0x004F09FA; Remove gate 0x004F0A9E..0x004F0AB4, empty-stack warning 0x004F0ADA..0x004F0C22 (0xBE4672), first-match erase 0x004F0AE4..0x004F0C6C, LoopBoundOverflow drivingAnimationHandler.cpp:108 0x004F0B5A..0x004F0BB6; UpdateCurrDrivingAnimations only from Init (0x004F0D2C..0x004F0D74: back(), else Sad 0x2C, else needs-met 0x38, else default 0x20); HandleActionCompleted 0x004F0D78..0x004F0E2A with PlayEndAnim 0x004F0EFA..0x004F0F4A, PlayDrivingLoopAnim 0x004F0E58..0x004F0EA0, PlayDrivingEndAnim 0x004F106C..0x004F10C0, lock/unlock helpers 0x004F0EB2..0x004F0EE2 / 0x004F0F4C; ActionIsBeingDestroyed 0x004F0F94..0x004F0FC0."
  ]
}
```

```json
{
  "id": "M4-019",
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

```json
{
  "id": "M10-005",
  "title": "The off-treads debounce clock is BaseStationTimer ms (engine tick-start time since run start, per tick)",
  "status": "EXACT_SOURCE",
  "evidence": [
    "A2 now = GetCurrentTimeStamp 0x511E2A..0x511E36, 0x84BCC0..0x84BCD0",
    "gap1 6a..6c UpdateTime only from CozmoEngine::Update 0x4ED626; ns = tickStart - runStart 0x65B3B6..0x65B420"
  ]
}
```

```json
{
  "id": "M11-037",
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
  "id": "M13-038",
  "title": "BehaviorAudioClient instances and lifetime: two clients (BehaviorManager, BehaviorSystemManager) subscribe to the public-state broadcaster; whether both stay subscribed; the re-entrant double ActivateSparkedMusic",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov48-49-check.md: Subscribe 0x00591F08 in the ctor; constructed by BehaviorManager 0x005A0914 and BehaviorSystemManager 0x005A5828, both built in Robot::Robot (0x0050FCAC, 0x0050FCBE); emitter 0x0064DE5E..0x0064DE7A -> 0x0064E656..0x0064E668 (synchronous, skips empty slots); re-entrance via the unlock 0x1D path and UpdateBroadcastBehaviorStage 0x0064DA44..0x0064DB86 (broadcaster state+0 copied at 0x0064DB44..0x0064DB58).",
    "re-analysis/research/overnight/20261016-ov69-74-75-77-check.md: construction 0x0050FC90..0x0050FCD0; clients 0x005A0908..0x005A091E, 0x005A581C..0x005A582C; ctor 0x00591E50..0x00591F08 (unconditional broadcaster subscribe); emit 0x0064E594..0x0064E69A; lifetime 0x00511120..0x0051113E, 0x005A0D8E..0x005A0DA2, 0x005A5886..0x005A589C; state+0 0x0064D5DC..0x0064D6B8; stage setter 0x0064DA4E..0x0064DB86; snapshots 0x0064D85C..0x0064D8A0; spark branch 0x005923D4..0x00592478; Activate 0x00592190..0x005921DE."
  ]
}
```

```json
{
  "id": "M13-039",
  "title": "BehaviorAudioClient::HandleRobotPublicStateChange and helpers: world events, round, spark, needs, dim, activity music, GuardDog and Feeding stages, unlock cache; SetSparkedMusicState input",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov48-49-check.md: handler 0x005921EE..0x00592258; helpers 0x0059225C..0x00592720, 0x00591FC4..0x00592190 (round table 0x00C5B65C, activity map 0x00C5B5F0, eighties 0x00573B70..0x00573B9A, 0x00573E30..0x00573E7E); handler lambda 0x01023D98 -> 0x00592B1E -> 0x004AF07C; SetSparkedMusicState (G2E 0xA3) lambda 0x00592A9E..0x00592ABA, client+0xC initial 0 (0x00591E56)."
  ]
}
```

```json
{
  "id": "M12-043",
  "title": "PyramidBase interior geometry: corners and midpoint for the pyramid top placement (GetBoundingQuad<float> corner order)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: corners/midpoint 0x006177F0..0x00617A08, 0x00617A8C..0x00617C32, multimap 0x00618A4C (equal keys keep insertion order, NaN right); unread GetBoundingQuad<float> 0x004E6490..0x004E6598 and Block::GetBoundingQuadXY 0x004E62A2.",
    "re-analysis/research/overnight/20261015-ov45-47-check.md: quad call 0x00617818..0x00617824; GetCanonicalCorners 0x004E60D8..0x004E6146 (8 corners at +-0.5: (-,-,+),(+,-,+),(-,-,-),(+,-,-),(-,+,+),(+,+,+),(-,+,-),(+,+,-)); pad/scale/rotate 0x004E62C0..0x004E6386; minAreaRect points opencv_core 0x00086188..0x0008627A; Quad 0x004E64FE..0x004E655A; centroid 0x004DF7BE..0x004DF84C; sort 0x004E7F24..0x004E7F96, 0x004E8508..0x004E8642; translation 0x004E639A..0x004E63B2; insertion 0x00617826..0x006179BC; midpoint 0x00617B98..0x00617BF6; container 0x006168EC..0x006169C4; builder 0x0061830A..0x006183B4; cache 0x0062C5A2..0x0062C616."
  ]
}
```

```json
{
  "id": "M13-023",
  "title": "Footprint corner ordering: SortCornersClockwise (centroid, atan2f, __sort4/sort3 ties, Quadrilateral slots) and the cv::Rectangle fallback (empty input, bounds, center); corner writers read and unreachable/out of scope",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00502DD8, 0x004EF78A the corner getters; 0x0084AAE0, 0x008494E0, 0x008499B0 the rotation-matrix path; 0x004E7EFC SortCornersClockwise; 0x004E6778 InitFromPointContainer",
    "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md open questions; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md 'What stays open'",
    "re-analysis/research/overnight/20261014-ov08-m13-023-check.md: S1-S6 0x004DF7BE..0x004DF84C, 0x004E7EFC..0x004E7F9C, 0x004E8260..0x004E8342, 0x004E847E..0x004E84E8, 0x004E8508..0x004E8642, 0x004E7E9C..0x004E7EFA; R1-R3 0x004E81B4..0x004E825E, 0x004E676A..0x004E67C4. Corner writers: MatPiece 0x00502DD8..0x00502ECE, FlatMat 0x004F6B58..0x004F70C0 (no constructor caller outside CloneType); CustomObject 0x004EF790..0x004F0340, clone 0x004F0470..0x004F04DC (callers only BlockWorld DefineCustom*/CreateFixedCustomObject handlers 0x0061E1EC..0x0061E6xx and DockingTestSimple 0x005CC1C0)."
  ]
}
```

```json
{
  "id": "M13-024",
  "title": "The exact footprint: cv::minAreaRect, convexHull, Sklansky_ and RotatedRect::points",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x004E6490..0x004E6570, 0x004E65C6..0x004E67C4, 0x0009C760..0x0009CEAC, 0x00039174..0x005395E0, 0x0003781A..0x00037968, 0x00086188..0x00086276",
    "re-analysis/evidence/m13-minarearect/port.py, emu.py, emu2.py, emu4.py, cmp2.py",
    "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q1; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q1"
  ]
}
```

```json
{
  "id": "M7-002",
  "title": "The shipped animation/reaction maps, behaviour configs and activity tree",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/obb/assets/cozmo_resources/assets/animationGroupMaps/AnimationTriggerMap.json (573 pairs)",
    "re-analysis/obb/assets/cozmo_resources/assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json (40 pairs)",
    "re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/reactionTrigger_behavior_map.json (22 rows over 21 triggers)",
    "RobotDataLoader::LoadReactionTriggerMap 0x00520bc8",
    "BehaviorManager::InitReactionTriggerMap 0x005a16e4",
    "178 shipped behaviour configs enumerated in re-analysis/inventory/M7-behaviour.md Appendix C"
  ],
  "unresolved": "Opus verification of R-BEH2 batch 3 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-3.md): NOT YET. The reaction-map parameter blocks are parsed but the strategies are built from literals (stand-in); the failure log lacks the event name RobotDataLoader.ReactionTriggerMap (0x00520D48); the FistBump, Hiccup and Sparked rows are unbound (their M10 strategies are unbuilt). Before: built, awaiting strong verification: RobotDataLoader::LoadBehaviors (0x005206bc) is built (recursive read of config/engine/behaviorSystem/behaviors/, first duplicate ID wins silently; the shipped corpus loads as 178 configs); a missing or empty reaction map registers nothing (no fallback; LoadReactionTriggerMap 0x00520c2e..0x00520c8a). Still open: the map's parameters (genericStrategyParams, frustrationParams, behaviorObjectiveTriggerParams, hiccupParams) are parsed but the strategies are built from literals (M10 owns the strategies; the rows do not settle which parameter feeds which field); the failure line lacks the engine's event name 'RobotDataLoader.ReactionTriggerMap' (0x00520d48); directory enumeration order is platform-dependent (BLOCKED_EXTERNAL, affects log order and duplicate arbitration only).\nB-M5-20 (2026-10-15), built, awaiting strong verification: host cleanup: FreeplayStack restores the previous driving trigger-map input on Dispose. CozmoAnimations.LoadFrom catches malformed AnimationTriggerMap JsonException/InvalidDataException/InvalidOperationException, visibly logs M1-034/M7-002 on the stack channel, retains any prior map and continues clip loading. Native malformed-map parser crash address is not in the checked cleanup note; this is the expressly requested host-file isolation, not a new native parser/log recovery. Native lookup consumers remain the checked T5 manager+0x54 Has/Get response paths 0x0052FA70/76 -> 0x00670AD0 / 0x00670810. No shipped map reaches this malformed-input guard.\nB-M5-21 (2026-10-15), built, awaiting strong verification: corrected the B-M5-20 malformed-map catch. Engine LoadFrom now uses the shared M5-051 readAsJson adapter: false returns a new nonnull empty map with keyed DataPlatform Error/errG, not M1-034 and not the prior map; a missing Pairs member/null root yields a silent empty map. String-key pairs retain JsonCpp asString integer conversion, no Enum.TryParse numeric alias: numeric CladEvent 1 is key \"1\", not the named enum value 1. The library receives the same parsed pairs, avoiding its second legacy read on the live engine path. Only non-object nonnull isMember termination (call 0x006704A0 -> find throw 0x008EB06E) retains prior map and logs M1-034/M7-002 visibly. Freeplay publishes DrivingTriggerMap and AreNeedsMet only at successful creation end, after all throwing loads; it registers both restores first and restores the previous delegate as well as map on Dispose. Broader reader formatting/schema/standalone library fallback gaps are not settled by this cleanup.\n\nB-M5-22 (2026-10-15), built, awaiting strong verification: Trigger-map host view now shares the complete resource-directory container with M5-034, including raw empty/unknown string keys and atomic per-file M1-034 skips. Explicit-file and direct-file-directory Load remain host tool conveniences; live CozmoAnimations never uses those to select a root-level or unrelated file. M5X20 synthetic resource fixture now puts its map in animationGroupMaps, matching the native production input. Renamed stale test reference corrected per B-M5-22 item4. Freeplay publication/restore is unchanged."
}
```

```json
{
  "id": "M13-013",
  "title": "DriveOffChargerContactsAction: a drive straight that retries while still on the charger",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "DriveOffChargerContactsAction ctor 0x00558228: DriveStraightAction(10 mm, 20 mm/s, false) (0x00558232/0x00558236/0x0055823E); then +0x44 = 7 (0x00558276/0x00558278), which is IActionRunner's RobotActionType (stored at +0x44 by the IActionRunner ctor 0x0053FDCC) and 7 = DRIVE_OFF_CHARGER_CONTACTS (RobotActionTypeFromString 0x0075A448 maps the string at 0x00C1604E to 7)",
    "in SDK mode only, the constructor clears the required-track lock: CozmoContext::IsInSdkMode 0x0055827C, cmp #1 0x00558280, then IActionRunner::SetTracksToLock(0) 0x00558286/0x00558288; SetTracksToLock 0x00540918 writes the argument to IActionRunner+0x54 only when the action state (+0x18) is 0x2000001 (not started), else warns; it is a local pre-run flag consumed by IActionRunner::Update 0x00540438 (MovementComponent::AreAnyTracksLocked), not a robot message",
    "Init 0x005582D0 copies robot+0x338 into action+0x8B (0x005582D2/0x005582D6) and returns 0 when not on contacts",
    "CheckIfDone 0x005582E4 retries while the drive is still running and fails 0x04000009 if still on the contacts (0x00558344)"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): DriveOffChargerContactsAction as native ctor/init/check on the tick model with the SDK-only SetTracksToLock(0) gate, robot+0x338 capture and 0x04000009 (0x00558228..0x00558344), 30.0f timeout. Still open: SmartRemoveDisableReactionsLock on a non-zero result; DriveStraightAction Init/CheckIfDone and the two-argument constructor defaults (speed 100/-80, accel 0x43480000, decel 0x43FA0000) unread; IsInSdkMode's source; no production caller (only the ManipTool). M12-024 read (re-analysis/research/20261013-m12-024-driving-lifecycle.md S1/S2): DriveStraightAction::Init calls handler Init (repeat 0) at 0x005475F6; CheckIfDone 0x005478C0..0x005479EE (state-3 gate, LastPathFailed 0x04000002 without PlayEndAnim, PlayStartAnim once when the path starts if +0x8A, PlayEndAnim at the end, Success after the end animation); destructor 0x00547540..0x00547558. Log strings 0x005478F0..0x005479AE unread.\nbuilt, awaiting strong verification: B-M5-17 existing DriveStraightAction is an ActionRunner using handler Init repeat 0, state-3 hold, failed path retry without end, start once, active hold, end then Success; cleanup IsActive/Abort then shared destroy. Concrete timeout, native LastPathFailed/HasPathToFollow/IsActive and CanInterrupt inputs remain MISSING. Info/debug calls 0x00547904/0x0054794E/0x005479AE remain MISSING, exact strings/arguments unread. Existing geometry path creation is not reclassified; concrete DriveOffChargerContactsAction/native derived result mapping remains incomplete.\nB-M5-17 host cancellation adapter: stored Cancel skips UpdateInternal callbacks; the host task completes at the existing ActionQueue WatcherEnding/destruction boundary, without adding an end animation or changing ActionList order. Regression fixtures now tick ActionList for start, planner failure and cancellation.\nbuilt, awaiting strong verification: B-M5-18 uses native base timeout 30.0f and CanInterrupt=false, ActionList cancellation, real DriveStraight name (13 bytes at 0xBEA0DC), native float |d|<0x3727C5AC early return with started=true/no path. AppendLine/ExecuteCustomPath failure branches 0x005476AE..0x005476D2 are visibly MISSING, not fabricated. Path-follower status/IsActive stand-in is explicit M12-032; geometry candidate and unread diagnostic arguments retained.\nB-M12-1 built, awaiting strong verification: DriveStraight ExecuteCustomPath failure returns 0x03000016; CheckIfDone uses real LastPathFailed/HasPathToFollow/IsActive and cleanup globally aborts active PC. Existing AppendLine/motion-profile gaps remain. OV09 check (re-analysis/research/overnight/20261014-ov09-m13-029-check.md): DriveStraightAction's dtor Abort is gated on PathComponent::IsActive (0x00547540..0x0054754E).\nB-M5-20 (2026-10-15), built, awaiting strong verification: bounded HostDriveStraightTick adapter fix: a nonzero PathRun.Result from StartPath returns 0x03000016 before waiting; a null wait after a successful start retains the prior cancellation mapping. Native ExecuteCustomPath failure return 0x005476C6..0x005476D0. The host adapter remains explicitly unported as a whole; this does not claim full native DriveStraight Init/CheckIfDone.\n\nB-M13-4 (2026-10-15), partial build, awaiting strong verification; BLOCKED: OV09 DriveStraightAction destructor active-status guard confirmed after Path Abort: no extra ClearPath. No change to existing guard or legacy/live drive scheduling and traversal gaps; the test does not certify the complete native DriveStraight lifecycle."
}
```

```json
{
  "id": "M15-003",
  "title": "Activity start/end predicates and cooldown lifecycle",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IActivityStrategy constructor 0x005B4EC8",
    "IActivityStrategy::WantsToStart 0x005B529C",
    "IActivityStrategy::RandomizeCooldown 0x005B5408",
    "IActivityStrategy::WantsToEnd 0x005B5444",
    "IActivityStrategy::SetCooldown 0x005B54E4"
  ],
  "unresolved": "Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. InCooldown uses the same fixed TickSec as M15-005."
}
```

```json
{
  "id": "M15-006",
  "title": "Freeplay activity selection and selected/deselected lifecycle",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ActivityFreeplay::PickNewActivityForSpark 0x005ADC44",
    "ActivityFreeplay::GetDesiredActiveBehaviorInternal 0x005AE29C",
    "IActivity::OnSelected 0x005B312C",
    "IActivity::OnDeselected 0x005B33B8",
    "IActivity::GetDesiredActiveBehavior 0x005B387C"
  ],
  "unresolved": "The inbound ActivateSpark message (sets BehaviorManager+0x60/+0x65, 0x005A3C92..0x005A3C9A) and the requested-activity message (+0x90) are unbuilt, so SetRequestedSpark/RequestNewActivity have no production caller. The selection/re-selection, interlude, null-pick, OnSelected/OnDeselected, reward-pending and the reselect logic are built.\nB-M5-16 caller gaps now visible: Activity OnSelected/OnDeselected report M5-063/M15-006 driving Push/Remove callsites; FreeplaySystem routes them to the real engine stack-log recipient. Existing activity clock/strategy behavior retained; driving triplet/lock lifecycle remains unbuilt.\nbuilt, awaiting strong verification: B-M5-17 places selected lifecycle MISSING calls after time store/strategy then UpdateActivity, and deselected RemoveIdle before RemoveDriving after time/strategy. Driving/idle calls gated on explicit trigger !=0x23F. PublicStateBroadcaster UpdateActivity 0x005B3154, SmartPushIdleAnimation 0x005B31BC, RemoveIdleAnimation 0x005B340C and Push/Remove driving 0x005B319A/0x005B3450 remain unbuilt handoffs; no fabricated handler stack mutation.\nB-M5-18 corrects the OnSelected comment to actual time-store/strategy/public-state/driving/idle ordering. No lifecycle behavior added or record settled. Operator decision 2026-10-15 (\"yes probably\", answering research/overnight/20261015-ov36-37-check.md question 1): the stack API passes the app's spark request (ActivateSpark) to the engine handler, as an app input under the app-boundary rule. The ActivateSpark handler (seam FreeplaySystem.SetRequestedSpark) is still unbuilt; its handler rows beyond case tag 0 (0x005A3C92..0x005A3C9A) need extraction before it is built. OV67 check (re-analysis/research/overnight/20261015-ov65-67-check.md): the ActivateSpark handler is M15-057, the spark fields/setter/switch M15-058, the ActivitySparked lifecycle M15-059; this record keeps the Freeplay pick and switch rows (0x005AE29C..0x005AE762). B-M15-6 (2026-10-16) updates prior inbound-unbuilt note: supported app ActivateSpark now queues to actual manager tag96/case0 handler and native raw-ID field stores (M15-057/058). Legacy FreeplaySystem.SetRequestedSpark(string) is explicitly a host seam, not that handler. Native raw-ID picker/successful-switch/specialized ActivitySparked ownership remains MISSING; new API does not set host string input, select a generic substitute activity, clear requests or fabricate start/end replies."
}
```

```json
{
  "id": "M15-013",
  "title": "Activity tree construction, desired names and discarded activityPriority",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ActivityFreeplay::CreateFromConfig 0x005AD478",
    "ActivityStrictPriority constructor 0x005B23DC",
    "activities_config.json"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): PickNewActivity is the engine's PickNewActivityForSpark (0x005ADC44): activities bucketed by required spark in config order (0x005AD676..0x005AD69E), only the requested spark's bucket is walked, +0x91 (forced) then +0x90 (desired from objects) take only that activity id without asking a strategy (0x005ADC6A..0x005ADC7E), otherwise the first activity whose strategy WantsToStart; ActivityIDFromString returns 0 for an unknown name (0x0076706C..0x007670D4); a missing bucket warns (0x005ADCD8). Still open: the engine's askCurrent argument is not a parameter here; FreeplaySystem.GetDesiredActiveBehavior loops up to SubActivities.Count passes where the engine re-picks at most once per tick (M15-002's)."
}
```

```json
{
  "id": "M8-002",
  "title": "The repetition penalty graph and its suppression window",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "config/engine/mood_config.json:88-99 ships defaultRepetitionPenalty with the nodes (0.0, 0.0) and (30.0, 1.0).",
    "IBehavior::ReadFromScoredJson 0x005bc488 reads repetitionPenalty into the GraphEvaluator2d at +0xe8; a missing or unreadable graph warns (IScoredBehavior.BadRepetitionPenalty) and leaves it empty, and an empty graph gets the single node AddNode(0, 1, true) at 0x005bc566 (0x005bc678).",
    "IBehavior::EvaluateRepetitionPenalty 0x005beee6 returns 1.0 when the last-run stamp +0x30 <= 0 (vldr s0,[r4,#0x30] 0x005beeea; movle.w r0,#0x3f800000 0x005beef8), otherwise evaluates the graph at +0xe8 at now - +0x30 (add.w r0,r4,#0xe8 0x005bef0e; b.w 0x8cbf9c 0x005bef1e).",
    "The +0x30 stamp is written by IBehavior::Stop 0x005bd11a/0x005bd11e as BaseStationTimer::GetCurrentTimeInSeconds().",
    "The +0x108 suppression threshold: StopWithoutImmediateRepetitionPenalty 0x005beea0 sets +0x108 = now + 1.0 (vmov.f32 s0,#1.0 0x005beeb0; vadd.f32 0x005beeb8; vstr s0,[r4,#0x108] 0x005beebc); EvaluateScore applies the penalty only when now >= +0x108 (vldr s0,[r4,#0x108] 0x005befe2; vcmpe.f32 0x005befea; bpl 0x005beff2).",
    "StopWithoutImmediateRepetitionPenalty's only three callers are BehaviorPickUpCube::UpdateInternal 0x005c685c, BehaviorStackBlocks::UpdateInternal 0x005c991c and BehaviorBuildPyramidBase::UpdateInternal 0x005dd110 (all M7/M15); IBehavior::Stop does not call it."
  ],
  "unresolved": "Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. an unreadable repetitionPenalty warns IScoredBehavior.BadRepetitionPenalty \"Behavior '%s': %s failed to read\" (0x5BC524..0x5BC52E); Graph2d.FromJson returns null silently (Activities.cs:162). StopWithoutImmediateRepetitionPenalty uses an unsourced BehaviorStopReason.Interrupted (SteppedBehavior.cs:341). The arithmetic (0x5BEEE6, 0x5BEEA0) holds. Before: built, awaiting strong verification: flat (0, 1.0) default graph, stamp <= 0 or NaN gives 1.0, float stamp/subtraction/compare (0x5bef12, 0x5befea), +0x108 = now + 1.0f, StopWithoutImmediateRepetitionPenalty calls Stop first. Still open: no stack behaviour calls StopWithoutImmediateRepetitionPenalty (callers are M7/M15); the internal Stop reason is not in the inventory."
}
```

```json
{
  "id": "M8-003",
  "title": "Scored selection: emotion scorers or the flat score, times the repetition and running penalties",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::ReadFromScoredJson 0x005bc488 reads five keys, not four: \"emotionScorers\" into a MoodScorer at +0xdc, \"flatScore\" into the float at +0x100 (0x005bc4ca/0x005bc4e0), \"repetitionPenalty\" into the graph at +0xe8 (0x005bc4ee), \"considerThisHasRunForBehaviorObjective\" into a behaviour objective at +0x10c through BehaviorObjectiveFromString (call 0x005bc5a0), and \"runningPenalty\" into the graph at +0xf4 (string 0x005bc602; add.w r6,r4,#0xf4 0x005bc5f8; AddNode(0,1,true) 0x005bc678).",
    "IBehavior::EvaluateScoreInternal 0x005beec2: if the MoodScorer vector at +0xdc/+0xe0 is non-empty it tail-calls MoodScorer::EvaluateEmotionScore (0x8cbf8c, with robot+0x440); otherwise it returns the flat score at +0x100 (vldr s0,[r0,#0x24] after the pre-index 0x005beed4). The two are alternatives, not addends.",
    "MoodScorer::EvaluateEmotionScore 0x0067c9b8 walks the scorers, evaluates each one's graph at its emotion's value or at the value minus the value sixty ticks ago when trackDelta is set (Emotion::GetHistoryValueTicksAgo(60) 0x0067c9f4), and returns the mean (0x0067ca6e..0x0067ca72); a result within 1e-05 of zero ends the walk and the score is zero (0x0067ca50).",
    "IBehavior::EvaluateScore 0x005bef60: while running it uses EvaluateScoreInternal plus the float at +0x104 (vldr s2,[r4,#0x104] 0x005bef80; vadd.f32 s16,s0,s2 0x005bef88) and multiplies by EvaluateRunningPenalty when +0x111 is set (ldrb.w r0,[r4,#0x111] 0x005bef84; cbz 0x005bef8c; blx EvaluateRunningPenalty 0x005bef90; vmul.f32 0x005bef98). Otherwise it scores only a behaviour that IsRunnableBase and passes the vtable+0x50 test, applies the repetition penalty only when +0x110 is set and now >= +0x108 (ldrb.w r0,[r4,#0x110] 0x005befd4; cbz 0x005befd8).",
    "IBehavior::EvaluateRunningPenalty 0x005bef22: returns 1.0 when +0x34 <= 0, otherwise evaluates the graph at +0xf4 at now - +0x34 (vldr s0,[r4,#0x34] 0x005bef26; add.w r0,r4,#0xf4 0x005bef4a; b.w 0x8cbf9c 0x005bef5a).",
    "The four constructor fields: +0x104 is the running-score bonus (IncreaseScoreWhileActing 0x005bf02c adds to it only while the current-action handle +0x84 is non-zero; ScoredActingStateChanged 0x005bf046 clears it); +0x108 is the repetition-penalty suppression threshold; +0x110 enables the repetition penalty; +0x111 enables the running penalty (both set to 1 by the halfword 0x0101 at 0x005bbd34, with no other writer).",
    "EmotionScorer::ReadFromJson 0x0067aabc names emotionType, scoreGraph and trackDelta. No shipped behaviour or activity config carries an emotionScorers block, so every scored behaviour in the app is scored by its flatScore alone.",
    "IncreaseScoreWhileActing 0x005bf02c is called only from concrete behaviour transitions (ten call sites, M7/M15); ScoredActingStateChanged 0x005bf044 has no engine caller, so +0x104 is never cleared in-engine."
  ],
  "unresolved": "Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. EvaluateScore calls virtual slots +0x84/+0x88 (0x5BEF72, 0x5BEFC6), which five classes override (BehaviorPounceOnMotion::EvaluateScoreInternal 0x5F83BE, used by socialize.json); ScoredBehaviorEntry.Evaluate hardcodes the base (Activities.cs:84-120). The runningPenalty bad-read warning (0x5BC638..0x5BC640) is missing (Activities.cs:163). The text saying trackDelta throws is stale. Before: built, awaiting strong verification: scored selection in float with the engine's order (EmotionScore 0x67c9b8, flat score, running bonus, repetition and running penalties). Still open: trackDelta GetHistoryValueTicksAgo(60) needs the Emotion history ring (M7-013) and throws NotSupportedException; MoodState holds emotions as double where the engine loads a float. OV20/21 check (re-analysis/research/overnight/20261014-ov20-21-check.md): contradicted: ScoredActingStateChanged (slot +0x80, 0x005BF045 in every behaviour vtable) is called from StopActing 0x005BD35A, StartActing 0x005BDB54 and HandleActionComplete 0x005BE200; its body stores 0 to +0x104 and ignores the bool.\nB-M8-3 (2026-10-15): checked +0x104 zero-on-Stop/StartActing/completion behavior already implemented; existing BehaviorFrameworkTests.ScoredActingStateChangedClearsTheBonusOnActionStartStopAndCompletion confirms all three paths. No scoring arithmetic/status changed."
}
```

```json
{
  "id": "M8-012",
  "title": "BehaviorManager::Update's tick order, behaviour switching, finishing and the reaction gate",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorManager::Update 0x005a2f70 returns immediately when the manager's init byte at +0 is zero, logging \"BehaviorManager.Update.NotInitialized\" (0x005a2f70/0x005a2f72/0x005a2fea).",
    "It ticks the current activity first: GetCurrentActivity then the activity's virtual Update at vtable+0x20 (0x005a2f78/0x005a2f80/0x005a2f84).",
    "If robot+0x355 is non-zero, or manager+0x20 == 1, it calls EnsureRequestGameIsClear (0x005a2f8e/0x005a2f94/0x005a2f9e).",
    "If manager+0x38 is set it calls SelectUIRequestGameBehavior, clears the flag, and when the previous current behaviour's class is 0x2e (cmp r7,#0x2e at 0x005a2fe0) sets byte +0x220 on the object at manager+0x30 (strbeq.w r1,[r0,#0x220] 0x005a305c).",
    "It calls CheckReactionTriggerStrategies 0x005a3060; a non-zero result suppresses the scored choice and the UI-request switch for this tick (cbnz r5 0x005a3068).",
    "The reaction list is evaluated only when the robot's action list robot+0x250 is empty: the manager latch at +0x4c is ORed with !IsEmpty and CheckReactionTriggerStrategies returns false when the list is empty (0x005a3558..0x005a356c).",
    "If no reaction fired, nothing occupies manager+0x30, and the current behaviour's class is 0x16, it calls ChooseNextScoredBehaviorAndSwitch (ldrb r0,[r0,#0x10] 0x005a3070; cmp r0,#0x16 0x005a3072; blxeq 0x005a3078).",
    "If no reaction fired and manager+0x30 holds a behaviour different from the current one, it calls SwitchToUIGameRequestBehavior (0x005a307e/0x005a3082/0x005a309a).",
    "It ticks the current behaviour with IBehavior::Update (0x005a30bc). Return 0 logs \"BehaviorManager.Update.FailedUpdate\" (0x005a3148) and return 2 logs a debug line; both then call FinishCurrentBehavior(behavior, class != 0x16) (0x005a3128). Return 1 keeps it (cmp r0,#0 0x005a30c0; cmp r0,#2 0x005a30c4).",
    "SwitchToBehaviorBase 0x005a1e6a stops the current behaviour (StopAndNullifyCurrentBehavior), calls IBehavior::IsRunnable(robot) and logs \"BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable\" on false, then IBehavior::Init (0x005a1e94); an Init failure logs \"BehaviorManager.SetCurrentBehavior.InitFailed\" (0x005a1eae), clears the behaviour, and still sets the running/resume info (SetRunningAndResumeInfo 0x005a1f12) and sends the DAS transition (SendDasTransitionMessage 0x005a1f1c).",
    "FinishCurrentBehavior 0x005a38ca: immediate == 1 tail-branches to 0x8cbc9c; otherwise, if manager+0x30 equals the finishing behaviour, it calls EnsureRequestGameIsClear (0x005a38e6) and switches to a default class-0x16 BehaviourRunningAndResumeInfo (movs r0,#0x16 0x005a38f4; SwitchToBehaviorBase 0x005a38fe).",
    "SwitchToReactionTrigger 0x005a25e4 builds a resume info and calls the strategy's ShouldTriggerBehavior (vtable+8) (0x005a262a); only when it returns 1 does it switch (cmp r0,#1 0x005a262c; SwitchToBehaviorBase 0x005a26da).",
    "TryToResumeBehavior 0x005a2b48 bails unless the stored resume head angle differs from a constant and the robot's action list is empty (vldr s0,[pc] 0x005a2b48; ActionList::IsEmpty 0x005a2b60), logging \"Resuming behavior and don't have an action, so setting head angle %f, lift height %f\" (0x005a2b72)."
  ],
  "unresolved": "Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. NotSupportedException seams (BehaviorManager.cs:632/649); no ActionList (MISSING at :311, :414, :873); the sends at :788-792 are MISSING. Before: built, awaiting strong verification: phantom 0x16 behaviour removed (running info = {current, resume, trigger}); tick order per Correction A2 (activity Update, EnsureRequestGameIsClear, UI request, reactions every tick, scored switch behind three gates, IBehavior::Update then FinishCurrentBehavior(trigger != None)); TryToResume with head/lift restore and no manager IsRunnable pre-check; failure switches to empty; running info stored on Init failure; per-trigger disable-count skip. Still open (seams): the meaning of manager+0x20, SelectUIRequestGameBehavior, SwitchToUIGameRequestBehavior and the +0x220 flag (no test for the class-test order before settling), EnsureRequestGameIsClear and SendDasTransitionMessage bodies, ActionList-is-empty is not wired in production, the concrete activity Update bodies (M7/M15); the stack's ChooseAndSwitch is a test-only parallel path. OV20/21 check (re-analysis/research/overnight/20261014-ov20-21-check.md): owns R4-R7, T1-T4, E1-E5; the SendDasTransitionMessage body 0x005A22B4..0x005A24AC is now read (tags 0x87/0x88, E3 triggers 0x15 Count / 0x16 NoneTrigger); EnsureRequestGameIsClear is not. Citation: SwitchToBehaviorBase is 0x005A1E20 (0x005A1E6A is a call site). Batch check 2026-10-16 (re-analysis/research/overnight/20261016-batch-check.md): BehaviorManager::Update discards the activity Update result (0x005A2F70..0x005A2F8A); a missing current activity is a null dereference at 0x005A2F7E (M1-034; reachable only if the app requests an undefined HighLevelActivity). OV108 answer (re-analysis/research/overnight/20261016-ov105-107-108-answer.md): correction: an app-requested undefined activity (v >= 4) faults first at 0x005B3140 inside SetCurrentActivity (OnSelected(null) from 0x005A11F4), not at 0x005A2F7E; the stack API cannot send v >= 4."
}
```

```json
{
  "id": "M8-013",
  "title": "The behaviour choosers and the activity selection dispatch",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060a44a walks its behaviour map, calls EvaluateScore(robot) on each (0x0060a44a), skips scores <= 0 (vcmpe s0,#0 0x0060a452), adds a running-duration graph bonus for a running behaviour (GraphEvaluator2d::EvaluateY 0x0060a474) and a RandomGenerator::RandDbl tie-break for a non-running one (0x0060a4a8), and keeps the maximum. It logs \"behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f\" (string 0x0060a60a; sChanneledInfoF 0x0060a61c) and warns \"Looks like more than one behavior returned IsRunning(). One of them is '%s'\" (0x0060a550).",
    "SelectionBSRunnableChooser::GetDesiredActiveBehavior 0x0060ad64 chooses between the behaviour at +0x2c and the one at +0x34, gated by a countdown at +0x3c and a latch at +0x40; a running behaviour short-circuits the countdown (movs r6,#1 0x0060ad84; subs r1,#1 0x0060adbe; str r1,[r5,#0x3c] 0x0060adc0; strb.w r0,[r5,#0x40] 0x0060adc8).",
    "StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior 0x0060b23e walks its behaviour vector at +0x1c..+0x20; a behaviour with +0xa1 (running) non-zero is returned immediately, otherwise the first behaviour for which IBehavior::IsRunnable(robot) == 1 is returned; if none, it returns a null shared_ptr (0x0060b242..0x0060b266). No scoring and no tie-break.",
    "IActivity::GetDesiredActiveBehavior 0x005b387c returns the activity's current behaviour when it equals the caller's; otherwise it calls the activity's GetDesiredActiveBehaviorInternal at vtable+0x30 (0x005b38c2) and, when the activity holds an interlude behaviour at +0x28 and the result differs, calls the interlude's method at vtable+0x28 (0x005b38f6), logging \"Activity %s is inserting interlude %s between behaviors %s and %s\" (0x005b399c).",
    "IActivity::GetDesiredActiveBehaviorInternal 0x005b3a6c reads the chooser at +0x24; a null chooser warns \"VERIFY(%s): ChooseNextBehaviorInternal called without behavior chooser overwritten\" and returns a null behaviour; otherwise it calls the chooser's vtable+0x28 (0x005b3a70/0x005b3aa2/0x005b3a94).",
    "The concrete activities implement GetDesiredActiveBehaviorInternal: ActivityFreeplay 0x005ae29c, ActivitySocialize 0x005b023c, ActivitySparked 0x005b1d18, ActivityStrictPriority 0x005b267c, ActivityBuildPyramid 0x005a8854, ActivityFeeding 0x005abdd8. Their bodies belong to M7/M15.",
    "SelectionBSRunnableChooser::GetDesiredActiveBehavior 0x0060ad64 full algorithm: +0x2c is the behaviour named by the last ExecuteBehaviorByID/ByExecutableType game message (set 0x0060ac2c/0x0060ac30), +0x34 is the Wait behaviour (BehaviorID 0xb2 resolved in the ctor 0x0060a988/0x0060a99a), +0x3c is the message's numRuns (ctor writes -1 unlimited, 0x0060a880/0x0060a884; message stores 0x0060aa88/0x0060aac8), +0x40 is a byte latch holding the previous call's running state (0x0060adc8/0x0060ae3a). It returns +0x2c while it is running or IsRunnable (0x0060ad96) and numRuns has not run out, decrementing +0x3c once per running->stopped edge; otherwise the same for +0x34 (0x0060ae08) but its countdown block runs only when +0x34 == +0x2c (0x0060ae10/0x0060ae14); otherwise a null shared_ptr (0x0060ae64).",
    "The selection chooser calls IBehavior::IsRunnable (PLT 0x004b0f54), not IsRunnableScored; IsRunnableScored has no engine caller."
  ],
  "unresolved": "Opus verification of R-BEH2 batch 3 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-3.md): NOT YET. No production game-to-engine channel (no IChooserExternalInterface); FindBehaviorByExecutableType and the AIInformationAnalyzer seam are unbuilt. New: ScoringChooser (Activities.cs:310-332) adds Random.NextDouble() in [0,1); the engine adds Robot::GetRNG()->RandDbl(0x3FB99999A0000000) in [0,0.1) (0x0060A3FC..0x0060A524), computes in f32 (vadd.f32 with 0x3DCCCCCD, floor 0x3C23D70A at 0x0060A482..0x0060A498, vcvt.f32.f64 after the draw at 0x0060A4BC) and keeps 0.01f on NaN (vmovgt); the C# uses double and decimal literals. CorrectionTests.cs:584-585 and FreeplayTests.cs:303 pin RandomDraw = 0.0, hiding the range error. Before: built, awaiting strong verification: the two GameToEngine messages (union tag uint16 0x0093 ExecuteBehaviorByExecutableType and 0x0094 ExecuteBehaviorByID; payload uint8 + int32 LE, no padding; unpack reads 1 then 4 bytes without a length check) and SelectionChooser's handler per 0x0060aa58..0x0060ac45 (numRuns stored at +0x3C on a hit and a miss, null on a miss, the SetProcessEnabled handoff only when the pointer differs, the unknown-tag path, the constructor subscription gate and order 0x94 then 0x93); RequestBehavior goes through the same tail. Still open: production wiring (the stack has no game-to-engine channel: nothing implements IChooserExternalInterface, so SelectionChooser is built without one); BehaviorManager::FindBehaviorByExecutableType's body and an ExecutableBehaviorType name table (the engine prints the name); the AIInformationAnalyzer enable requests (a MISSING seam); the engine's +0x1C EProcess id (HasProcessField defaults to RequiredProcessRunning != null, which nothing sets); unsubscription handles; the underflow value in the unpack is the stack's M2-017 approximation. OV51 check (re-analysis/research/overnight/20261015-ov50-52-check.md): the +0x1C required process: the IBehavior ctor zeroes it (0x005BBBEE/0x005BBC08); BehaviorVisitInterestingEdge sets 1 (0x005E5FB2/0x005E5FB4); IsRunnableBase 0x005BD7D4..0x005BD930: set and not running -> sErrorF \"IBehavior.IsRunnable.RequiredProcessNotFound\" \"Required process '%s' is not enabled for '%s'\", _errG, break, not runnable. SetProcessEnabled callers 0x0060ABF0/0x0060AC10, 0x0060AF3E, 0x0060AF82. B-M11-12 built, awaiting strong verification: IBehavior +0x1C is an explicit zero-initialized RequiredProcessId; nonzero gates through the owned analyzer before state/unlock checks, preserves running short circuit, and reports exact RequiredProcessNotFound Error then errG/debug-break/false (0x005BD7D4..0x005BD930). Selection default reads nonzero ID instead of callback presence; FreeplayStack binds Add/RemoveEnableRequest under SelectionBSRunnableChooser. MISSING: VisitInterestingEdge native constructor writes1 at0x005E5FB2/0x005E5FB4 but its concrete class and consumers remain unbuilt; actual factory now reports this rather than constructing a stand-in or fake disable lifecycle. Other chooser/native activity ownership gaps remain."
}
```

```json
{
  "id": "M8-027",
  "title": "ProtoHandle subscriber-callable census: which callables the 8 ProtoHandle kinds hold at teardown and what destroying them does",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov53-55-check.md: ProtoHandle vtables (8) confirmed; EngineToGame release 0x00664708, unlink 0x006634F4 (nulls the callable, decrements node refcount +0x20, frees at zero 0x0066352E..0x00663560).",
    "re-analysis/research/overnight/20261016-ov105-107-108-answer.md: void() signal 0x0105DF8C: one subscriber via RegisterTransport 0x0083B9A0..0x0083BE4C (bind vt 0x0103795C trivial); Dispatch signal TransferQueueMgr+0xC: two RegisterTask::$_0 (vt 0x01031418, destroy memory-only) from TransferTask::Init 0x006A4358..0x006A438E; release bodies 0x004EF138, 0x0051D8F8, 0x00591550, 0x0064E618, 0x00664708, 0x00664EB8, 0x006A3EFC, 0x0083BED0 identical; ~TransferQueueMgr 0x006A3860..0x006A3898."
  ],
  "unresolved": "RECOVERABLE_GAP: enumerate the subscriber callables held by ProtoHandles at teardown and read each destroy body (expected mostly bx lr). B-M8-6 (2026-10-16): UiSubscriptionHub actual nonnull callback unlink and signal-owner destruction visibly report unbuilt native captured-owner finalization0x0066350E/node+20 release0x0066352E..0x00663560 on stack channel, deduplicated per hub after storage lock release. This is not subscriber operator() invocation, a native empty-capture proof, or a complete eight-kind callback census. M8-024 +88 remains separate. Engine shutdown detaches its global missing sink before destruction; CozmoEngine now forwards the already-reported hub gap to its own stack log after hub retirement, so the diagnostic remains visible without reordering destruction or adding a source log. Batch check 2026-10-16 (re-analysis/research/overnight/20261016-batch-check.md): E2G signal mechanism read (dtor 0x006634B8..0x006634E4, unlink 0x006634F4..0x0066352C, node release 0x0066352E..0x00663562, dispatch 0x00663C74..0x00663CB8); the E2G ProtoHandle release 0x00664708 IS reached via the ScopedHandleContainer dtor. Release slots of the 8 kinds: 0x004EF138, 0x0051D8F8, 0x00591550, 0x0064E618, 0x00664708, 0x00664EB8, 0x006A3EFC, 0x0083BED0. The OV81 census (six signal kinds: 23 E2G, 22 G2E, 21 R2E, 1 audio, 1 public-state, 1 AssignmentDef) finds every callable trivial. Open: which of the 9 non-trivial void() callables the void()/Dispatch signals hold. OV87 is not evidence. Read 2026-10-16 (re-analysis/research/overnight/20261016-ov105-107-108-answer.md): SETTLED in substance: no signal holds any of the 10 (not 9) non-trivial void() callables; every release/remove is memory-only with no log, message or invocation. IMPLEMENTATION_GAP only so the existing code is confirmed; nothing behavioural left to build. B-M8-6's \"unbuilt captured-owner finalization\" diagnostic can be removed."
}
```

## Submitted dependency inventory — primary-supported rows

Each row states the observable consequence (AGENTS 1–4). Checked source inventories below are reused as instruction evidence, not as evidence that their current C# join is bound. Raw instruction output is only in ignored `.scratch/driveoff-pilot/`. No claim that all fields of a referenced record are settled.

| Row | Observable / source behavior and exact witness | Current C# / boundary |
|---|---|---|
| D1 | 1,2,4: DriveOff constructor 005C0980..005C0A3C invokes IBehavior base, reads extraDistance via JSON asFloat, adds f32 96 and stores +11C (005C09C0/005C09E2). Shipped freeplay distance156, hiking141. IsRunnable005C0B10 reads platform Robot+34A, not charger contact+338. | BehaviorFactory omits class; hand-built ShippedBehaviors route uses fixed config values. Need native configuration/factory attachment, not a second behavior implementation. |
| D2 | 1,2,4: Init005C0B18 reaction lock table C672F0; flag+120=0. Shared selected severe need [[Robot+264]+30]+14==3 pushes name=this+40, driving triplet97/93/8F from C6731C then sets flag. Off-treads+355 selects waiting versus driving. Stop005C0D90 removes only if flag. | Native robot BehaviorManager/severe selection/triplet stack already built; verify source call order and real owner, do not rebuild retained-pointer mechanics. |
| D3 | 1,2,4: Transition005C0BB8 sets DrivingForward, rechecks platform then allocates two-argument DriveStraight at005C0C08 and delegates through StartActing005C0C26. Update005C0DA8 waits/redrives based on platform, off-treads, real action tag; off-platform with no tag stores native float Timer seconds in Whiteboard+44 and returns2. | Host action handle/closure route remains a join gap. No direct lattice goal planning/import exists here. |
| D4 | 1–4: Generic StartActing005BDACC rejects A0 finish-request/non-running/non-resuming/busy; copies completion functor at005BDB46 BEFORE real action tag+60→behavior+84 store005BDB50, then virtual busy notification and ActionList Queue NOW/retries0 (005BDB5C/6C). IBehavior ctor005BBB74 subscribes E2G5A when external interface exists (005BBD5E..7A). Receiver005BE1E6 matches nonzero tag, clears it BEFORE busy(false), then rereads running+callback and invokes synchronously. | Existing owned overload has much of this order, but actual native-robot 5A subscription and DriveOff caller still report MISSING. Functor adapter final hop is source-closed in S5 below. Callback copied before queue admission matters for synchronous completion; queue failure does not roll back native tag. Native terminating/UAF paths use M1-034, not intentional host UAF. |
| D5 | 1,2,4: DriveStraight two-arg ctor005470F0..0054725E gives distance-dependent speed(-80 or100), accel200/decel500, manual=false, anim=true, started=false. SetMotionProfile0054759E..D6 can override speed/accel/decel. Init005475D8..7732 calls handler init, abs(distance)<epsilon3727C5AC completes without a path, otherwise builds ONE line. AppendLine false→03000013; ExecuteCustomPath nonzero→03000016. | Existing host direct default100/true is incomplete for negative distance; source f32 line/profile/error behavior needed. M13-013 title is derived retry action but current unresolved also owns plain DriveStraight defaults/geometry: do not settle whole derived action from this pilot. |
| D6 | 1,2,4: Init0054762A reads MAIN pose yaw via GetPose004EA398;0054763C gets STORED drive-center pose through005477A0 (Robot+2A4; main pose+298). StartXY=center; endpoint separate f32 cos/sin*distance then +start at0054767A/686 and00547692/696. SetPose0051502C copies main incoming pose00515080 then recomputes stored center0051508C..AC: raw CarryingComponentRobot+284,+8==FFFFFFFF→-20; every other rawID→+0 (literal00515138 bits00000000). MoveRobotPoseForward writes +2A4. | Current Init reads one public/main history pose for yaw AND XY and double arithmetic. Existing EngineRobot.ForwardPose already owns candidate stored center (NativeDelocalization.SetPose115..126); reuse/check it and its actual raw-owner input rather than create another pose owner or compute from current carry at Init: carry may have changed since last SetPose. ComputeDriveCenterPose005135D0 has same -20/+0 rule but is separate from stored producer. Root check in getter logs VERIFY but returns stored pose. Existing CM13-04 checked forward/pose helpers reusable; its planner ComputeOriginPose is not this offset and must not be applied twice. |
| D7 | 1,2,4: AppendLine0085CE88 countbyte>=0 defines line then increments count; false only negative signed count. ExecuteCustomPath0064B1C6..1FA stops active planning, clears flag, releases selected planner then tailcalls ExecutePath; no lattice admission. | Existing custom-path owner is actual production route. Existing CM13-03/04 lattice joins are OUT of this direct path. If another planner existed before drive-off, stop/lifetime behavior remains necessary; no new planner computation is requested. |
| D8 | 1–4: Adopted 20261013-m12-032-path-status P1–P31: ExecutePath0064A340 clears old path, incrementsu16ID, Dole(6), sets manual=false, sends ExecutePath then stores Timer floatclock/statusWaiting on success. P19 Doler00507E28/005082CE sends initial6 and RobotState segment-driven continuation. C6 started/interrupted/completed0064B37A..6A0 correlate IDs and statuses; RobotState currentsegment0051299A. ActionList tick005140BC precedes Path tick00514170. Custom path's planner-null means possible-origin-change handler returns without replan. | Already source-checked path/status/doler bodies reused. Need retain exact line serializer/transport input binding; no simulator timing expectation replaces firmware C6. Which C6 follows ClearPath remains HARDWARE_ONLY P31. |
| D9 | 1–4: CheckIfDone005478C0..79F0 handlerstate3 waits; LastPathFailed→04000002; started flag takes HasPathToFollow, start anim only on exact1 and playflag; IsActive waits; end animation nonzero waits; otherwiseSUCCESS0. Dtor00547530 IsActive==1→Abort then handler.ActionIsBeingDestroyed before base. | No new drive-off-specific timeout. Common ActionRunner timer/prepostwait/retry/track policy is reused from checked OV18/M8 framework, including default30sec. Need real action owner, clock, locks and completion order bound. |
| D10 | 1–4: Callback005C0E9C compares FULL result word with0; success sends objective4 first005C0EAC then native MoodOwnerRobot+440 and Timer seconds005C0ED8→TriggerEmotionEvent DriveOffCharger005C0EE2. Failure does neither. | Objective emitter built CM13-02; live MoodOwner/clock context missing in manual robot drive route. Current ActionBehavior EmotionEvent Clock()/1000 is not native Timer.SecondsF binding. |
| O1 | 3,4: CM13-02 primary check:005BE228 external/flag gate→adapter005BE3A8 E2G8B, serializer007285FE/0071BEAE payload04000000; enum name DroveAsIntended from01033150→00C19BAC. Rvalue broadcaster006625B0 delivers app/API first then local synchronous subscribers006625FA. | Actual bytes/order are built. Existing unconditional M15-059 MISSING must be narrowed to real affected recipients, not used as a substitute activity. |
| O2 | 4: ActivitySparked005B181E compares objective with configured+A0 before increment+8C. Reload005B10F0..1132 reads behaviorObjective. Shipped engine behavior configs have no DroveAsIntended objective. | Shipped objective4 is filtered here; do not extract whole Sparked callbacks or declare all M15-059 complete. Host-supplied altered config is a separate input contract, not assumed absent silently. |
| O3 | 4: Socialize005B07BC special case only objective12/phase2. Otherwise objective map lookup005B081C; no-match leaves count inr6; tail005B085A only mutates when count0 AND phase4. GetDesired005B039A..3A0 rejects empty map before selecting behavior,005B05B2 sets4 only after actual chosen match; no-choice→6 at005B067A. OnSelected005AFF60 writesphase0 BEFORE populate. Shipped Socialize required keys are PouncedAndCaught/PeekABooComplete, not4. | Selected-path invariant and pre-selection undefined-read boundary are closed in the objective supplement below; do not apply that invariant to a fresh uninitialized recipient. Do not replace conditional tail with an invented blanket no-effect claim. No need full other-behavior/RNG extraction if invariant closes. |
| M1 | 1–4: Reuse checked 20261021-m7-mood-completion-owner-join, mood-init-input, Q17 EM/AM rows: Robot ctor00510134..146 allocates one MoodOwner+440; Init00510356..35E gated by DataPlatform context+8. Loads config/engine/mood_config.json then emotion-event files. Init0067AEBC registers ActionWatcher once with real Robot/ActionList, external only gates C3 subscription. Handler0067B318 one-shot ignore then map(type,result>>24), dispatchTimerfloat0067B3C6. | Freeplay-facade newMoodState/unconditional watcher registration is not native Robot owner; actual EngineRobot.Update does not advance MoodOwner. Must bind once at robot lifetime and source tick, not only FreeplaySystem.Tick. Native destructor observer retirement guards null ActionList; GC/lifetime mechanism free if no stale callbacks/API effects. |
| M2 | 1–4: SuccessDriveOff event affects Confident+0.3 per shipped charger_events; default repetitiongraph(0,0),(30,1). Trigger0067B85C eventlookup, elapsed0067B8D8→penalty0067B8E0→ordered affectors f32multiply0067B8F6→EmotionAdd0067B902. Update0067B5D4 dtfloor1e-4, histories/decay, then SendEmotionsToGame0067B724 tag61/count9+9f32. GetCurrentTime0067ADA8 real BaseStationTimer seconds, not Mood+130 animation cache. DRIVE_STRAIGHT result mapping has RETRY/ABORT events, no SUCCESS/CANCELLED entry. | Native owner/update/APIclock missing on manual route makes group selection/history/state/API wrong even when callback event implementation exists. Reuse previously source-checked Mood model/effectors, do not invent a second event model. |
| M3 | 1,2,4: GetSimpleMood0067C084..BE initialDefault2; Happy0 if Happy>.33; Sad1 overrides if Confident<-.29 OR Happy<-.33. Sourcef32 pool0067C0C0=-.33, C4=.33,C8=-.29. Own drive-off severe triplet bypasses DrivingHandler simple-mood triplet choice but AnimationLibrary group name selection still consumes SimpleMood and cooldown clock/cache. | M7-026 default mood/null callback and host Timer stand-in remain behavior-changing. Need source library input attachment including cached Mood+130 semantics, not claim severe push makes all mood irrelevant. |
| W1 | 1,2,4: Reuse checked normal contact ingress M13-040/M10-005/M4-019: Sensor RobotState contact→SetOnCharger→ObserveCharger/SetPose→BlockWorld.SetObjectPoseChanged. The frame-tail pending/force gate rebuilds actual configurations, caches last checked root poses, publishes tallest/PublicState then audio clients A-before-B. Concrete downstream source M13-038/039 world-event client emits EC3490EC or3BA32BC6 on stacks>1 transition when Activity!=2. | This makes CM13-05 common producer conditionally necessary for full normal ingress/shared world pilot, not a direct DriveStraight motor prerequisite and not merely a globaltick association. No BuildPyramid callback extraction is justified by that chain. |
| W2 | 3,4→1: Reuse independently checked CM13-05 30-row inventory and supplements, actual00616F00→0061682C all3type inputs, currentorigin/family/state admission, processedIDs/oldcache/sourceequality/candidatecast/prune0062C72E, exact geometry. Native Stack neighbor FIRST match0062610C r3=1, reference flattened at00626048→004DF628; candidate relative flatref.parent then temporary z replacement for footprint, restoredheight. PyramidBase first geometry operand signedMAX+C then signedMIN+14; ID outputMINthenMAX. | Current BuildLocked allpairs/stand-in geometry cannot feed certified native cache/tallest. Preserve ordered IDs/results, immutable snapshot content/readiness. Oldshared_ptridentity not automatically required by these consumers. Do not turn unsupported Footprint into sourcefalse/no-hit or publish an unsupported empty native cache. Missing root/rawcarry likewise explicitUNKNOWN, notabsence. |
| W3 | 3,4: Fresh primary OnConfigSeen005DB2D4: clockseconds→dt and previous+138 store005DB2FA/304; GetCacheByType005DB316, virtualCOUNT005DB31C..322 compares map-node+14 at324/326; grows→behavior+13C=now at32A; storescurrentcount336.358..396 returns dt<4.99999 AND abs(stamp-now)<1e-5. No entry pointeridentity, per-entry first-seen timestamp or API ConfigurationSeen emitter in this path. | Existing public BlockConfigurationManager.FirstSeenSec/ConfigurationSeen derives hostcanonical arrangement key and hostclock, C# OnConfigSeen uses reacted-ID set and5sec age: this is NOT recovered native semantics. No approved compatibility policy found. Output3 contract needs manager/operator disposition or explicitly recorded recovery gap. Native cache producer alone cannot settle it. This is not an exclusion/no-effect claim. |

## Required scope boundaries and resolved caller-check history

The direct drive needs the custompath/wire/status route, not the lattice planning pipeline. Platform/contact ingress can modify world/config/audio state conditionally, so those observed dependencies remain included. Other behavior callbacks (BuildPyramid, all ActivitySparked lifecycle, arbitrary scene behaviors) are not automatically extracted from global engine construction.

Native configuration pointer retain counts and native vector capacity are internal for the proven cache-count/tallest/ordered-ID consumers; immutable retained snapshots can preserve their observed values/order without reproducing native allocation. This is a consumer-scoped statement, not a claim that every possible configuration consumer is identity-independent. Public host firstseen/event API is not dismissed.

Initial draft questions (a)–(f) are answered by the caller/downstream and final binding supplements below. The public configuration adapter decision remains pending. Independent reviewer still must check the whole inventory and any explicit remaining source bound; this report never authorizes code by itself.



## Additional current record quotes before the following source reuse

```json
{
  "id": "M5-008",
  "title": "One streaming animation: refuse or interrupt, tags 1..0xFE returned, loops with the same tag, live forced to 1 loop, ReplayLastAnimation",
  "status": "EXACT_SOURCE",
  "evidence": [
    "A4 0x0057B17E..0x0057B1B2",
    "A5 0x0057B1DE..0x0057B24A",
    "A6 0x0057B2FC..0x0057B304",
    "A8 0x0057B29C..0x0057B2C8",
    "A9 0x0057B2D4..0x0057B2F6, 0x0057B660..0x0057B672",
    "A13 0x0057CFF6..0x0057D036",
    "A27 0x0057ED0E..0x0057ED2A",
    "re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-008 (Opus settling pass, manager-adopted 2026-10-11).",
    "re-analysis/research/20261011-m5-fix-rows.md section 5 (Opus fix rows, manager-adopted 2026-10-11).",
    "re-analysis/research/20261012-m5-x9-extraction.md section 4c: sVerifyFailedReturnFalse 0x0080D9A4 logs sErrorV \"VERIFY(%s): \", sets errG with no debug break; sDumpCallstack 0x0080DA08 is a no-op (0x0080B0A6 bx lr); the flush 0x0080DA24 goes MultiLoggerProvider::Flush 0x006673BA -> DasLoggerProvider::Flush 0x006671F4 -> libDAS DASForceFlushNow (M3-051); returns 0.",
    "re-analysis/research/20261012-m8-play-animation-actions-check.md addition 2: SetStreamingAnimation(null) stores +0x73 = 1 when an old stream existed (0x0057B2FC..0x0057B300), and with an idle streamer (+0x38 == 0) still calls Abort() (0x0057B1DE -> 0x0057B242).",
    "re-analysis/research/20261012-m9-029-custom-song.md check: SetStreamingAnimation stores +0x38 = anim at 0x0057B24A before the custom branch; the live/tag/InitStream tail (0x0057B29C..0x0057B2F6) runs on whatever +0x38 holds; ReplayLastAnimation (A27, 0x0057ED0E) replays by the name at +0x54 through the string overload with custom = false (0x0057B160..0x0057B16C)."
  ]
}
```

```json
{
  "id": "M5-011",
  "title": "Group choice: mood, head window and cooldown filters; RandDbl(sum w) weighted draw; fallback to the Default mood",
  "status": "EXACT_SOURCE",
  "evidence": [
    "D4 0x0058C4C0..0x0058C7BE",
    "D5 0x0058A946..0x0058AB2E",
    "D6 0x0058AB30..0x0058AE2A",
    "R-ANIM pre-extraction part 1 item 4 4.1..4.12: group loop 0x0058A622..0x0058A73C, entry DefineFromJson 0x0058C4CC..0x0058C7D2, ctor 0x0058C4B0, backup 0x0058AC2A",
    "re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-011 (Opus settling pass, manager-adopted 2026-10-11).",
    "re-analysis/research/20261011-m5-fix-rows.md section 8 (Opus fix rows, manager-adopted 2026-10-11).",
    "re-analysis/research/20261012-m5-x9-extraction.md section 4f: MoodManager::GetSimpleMood 0x0067C084 (Default; Happy > 0.33f -> Happy; Confident < -0.29f -> Sad; Happy < -0.33f -> Sad) is an M7 input; the group draw time is MoodManager+0x130 (0x0058A95C), the last MoodManager::Update(t) time (0x0067B67E), not BaseStationTimer."
  ]
}
```

```json
{
  "id": "M5-014",
  "title": "Cooldown keyed by clip name across groups; the Default-mood backup rule within +-0.05 rad, else the first entry",
  "status": "EXACT_SOURCE",
  "evidence": [
    "D5 cooldown set on a pick; MoodManager::Update 0x0067B5E6",
    "D6 0x0058AB30..0x0058AE2A",
    "D7 0x0058BA28..0x0058BA98",
    "R-ANIM pre-extraction part 1 item 4 4.8..4.12: CooldownTime default 0x0058C868, UseHeadAngle 0x0058C734, HeadAngle 0x0058C770, backup 0x0058AC2A",
    "re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-014 (Opus settling pass, manager-adopted 2026-10-11).",
    "re-analysis/research/20261011-m5-fix-rows.md sections 8-9 (Opus extraction, manager-adopted 2026-10-11): G1 0x0058A95C, 0x0067B67E, 0x00513BE6..0x00513BEA, 0x00513E8A; B1-B6 0x0058ABBA..0x0058ADBA, 0x0058BA54..0x0058BA7C, 0x0058C70C..0x0058C726, 0x0058A680, 0x0058C76E, 0x0058A6F2..0x0058A70C."
  ]
}
```

```json
{
  "id": "M7-003",
  "title": "ReactToImpact FallingStopped, intensity, recalibration and animation state machine",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorReactToImpact::InitInternal 0x006061f8",
    "TransitionToPlayingAnim 0x00606348",
    "AlwaysHandle 0x00606408",
    "1000.0 threshold literal at 0x00606470",
    "5.0 second wait literal 0x40a00000 in InitInternal"
  ]
}
```

```json
{
  "id": "M7-004",
  "title": "All 30 live-idle tunables and defaults",
  "status": "EXACT_SOURCE",
  "evidence": [
    "SetDefaultParams 0x0057db40..0x0057dcd0; decoded values in M7 inventory Appendix A row 4"
  ]
}
```

```json
{
  "id": "M12-012",
  "title": "What makes a cube a valid bottom to stack on: resting flat within ten degrees",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0063C5C4 CanStackOnTopOfObject; 0x0063C654 CanInteractWithObjectHelper",
    "0x0063C66C movw r1,#0xB8C2 + 0x0063C670 movt r1,#0x3E32 = 0.174533 rad = 10 deg; 0x0087751C IsRestingFlat",
    "the 15.0 is owned by M13-007",
    "IsPoseTooHigh 0x00877954: 0x00877978 D=GetDimInParentFrame<'Z'> (0x00557794); 0x00877990 s0=D*f1; 0x00877994 s16=D*f3; 0x00877998 s18=D*f1+f2; 0x008779A0 pose.z ([r0+0x28]); 0x008779AA D*f3+pose.z; 0x008779AE +1e-5 (0x3727C5AC); 0x008779B2 vcmpe; 0x008779BA movmi r0,#1",
    "0x0063C6C0..0x0063C730, 0x0063C78A..0x0063C792, 0x0063C5FE..0x0063C61C, 0x0063C840, 0x0063C8C2..0x0063C8D2",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q7"
  ]
}
```

```json
{
  "id": "M12-033",
  "title": "The object-pose queries the placement path uses: GetZRotatedPointAboveObjectCenter, GetClosestMarkerPose and the Radians comparisons",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00877574, 0x00877774, 0x0084C87C, 0x0084CC0A, 0x0084CC90, 0x0084AA1C, 0x00557794",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q1-Q3; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q1-Q3 (PASS). Caveat: x, y are parent-frame values while z and yaw are root-relative, and the parent is set to root: consistent only when the object's parent is the root"
  ]
}
```

```json
{
  "id": "M7-018",
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

```json
{
  "id": "M7-019",
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

```json
{
  "id": "M5-023",
  "title": "Abort: AnimationAborted broadcast -> AbortAnimation 0x8D reliable; state cleared; buffer kept and flushed by the next no-animation Update unless InitStream drops it; no EndOfAnimation",
  "status": "EXACT_SOURCE",
  "evidence": [
    "A22 0x0057B3F2..0x0057B426",
    "A23 0x005276E8..0x0052771A, 0x0052C22E -> 0x00517DDE..0x00517E0A",
    "A24 0x0057B442..0x0057B58A",
    "A25 0x0057D122..0x0057D1DE",
    "R-ANIM pre-extraction part 1 item 8 8a..8c: choice 0x0057B418..0x0057B430, reset 0x0057B562",
    "re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-023 (Opus settling pass, manager-adopted 2026-10-11).",
    "re-analysis/research/20261011-m5-fix-rows.md section 12 (Opus fix rows, manager-adopted 2026-10-11).",
    "re-analysis/research/20261012-m8-play-animation-actions-check.md addition 2: SetStreamingAnimation(null) stores +0x73 = 1 when an old stream existed (0x0057B2FC..0x0057B300), and with an idle streamer (+0x38 == 0) still calls Abort() (0x0057B1DE -> 0x0057B242)."
  ]
}
```

```json
{
  "id": "M2-009",
  "title": "Outbound layouts and sizes of the 42 engine-to-robot messages this stack sends",
  "status": "EXACT_SOURCE",
  "evidence": [
    "the per-message Pack/Size/operator== addresses in re-analysis/inventory/M2-protocol.md Appendix A section 1 (tags 03 05 08 0A 0B 25 32 34 35 36 37 39 3B 3C 3D 3E 3F 41 42 43 44 45 48 4A 4B 4C 58 60 64 66 80 81 8E 8F 93 94 97 99 9A 9B 9F A0)",
    "type-only differences from the protocol definition: SyncTime word 1 f32; DockWithObject field0 f32; bool (not u8) in SetBodyAngle isAbsolute (0x007A29BE), PointTurn useShortestDirection (0x007A343C), DockWithObject fields 5 and 8 (0x007C06B4, 0x007C06DC), PlaceObjectOnGround field 6 (0x007C0986), DockingErrorSignal fields 5 and 6 (0x007C0B78, 0x007C0B80)"
  ]
}
```

```json
{
  "id": "M5-034",
  "title": "AnimationTrigger to group name from assets/animationGroupMaps Pairs; first entry wins; unknown gives a warning and \"\"",
  "status": "EXACT_SOURCE",
  "evidence": [
    "gap1 C7 0x00670438..0x0067075C; GetResponse 0x00670810..0x00670920",
    "re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-034 (Opus settling pass, manager-adopted 2026-10-11).",
    "re-analysis/research/20261011-m5-fix-rows.md section 20 (Opus fix rows, manager-adopted 2026-10-11)."
  ]
}
```

```json
{
  "id": "M11-035",
  "title": "The VisionComponent per-mode result handlers and the frame broadcast",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionComponent::UpdateAllResults 0x006542EC runs markers 0x006544A2, faces 0x00654510, pets 0x0065457E, motion 0x006545E8, overhead edges 0x00654652, tool code 0x006546BC, computed calibration 0x00654726, image quality 0x00654790, laser points 0x006547FA",
    "then CheckMailbox 0x00654A74 and the RobotProcessedImage broadcast 0x00654A14/0x00654A1C"
  ]
}
```

```json
{
  "id": "M8-009",
  "title": "Behaviour scope undo order: the engine releases in a fixed order (reaction locks, idle, motion profile, track locks, light patterns)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::Stop 0x005bd08c releases the scope in a fixed order regardless of acquisition order: (a) remove every disable-reactions lock while +0xac != 0 (0x005bd12c ldr.w r0,[r4,#0xa4]; 0x005bd136 blx SmartRemoveDisableReactionsLock; 0x005bd13a/0x005bd13e/0x005bd140), (b) remove the idle animation if +0xb0 (0x005bd142 ldrb.w r0,[r4,#0xb0]; 0x005bd14c blx SmartRemoveIdleAnimation), (c) clear the motion profile if +0xc0 (0x005bd150 ldrb.w r0,[r4,#0xc0]; 0x005bd158 blx SmartClearMotionProfile), (d) walk the track-lock map at +0xb4 and MovementComponent::UnlockTracks(mask, name) each, then destroy the map (0x005bd15c/0x005bd16e/0x005bd174 blx 0x4a5794; 0x005bd19e blx __tree::destroy), then clear the custom light-pattern vector at +0xcc/+0xd0 (0x005bd1a2 ldrd r1,r0,[r4,#0xcc] ... 0x005bd1c6 strne.w r0,[r4,#0xd0]).",
    "This stack's BehaviorScope groups its undos by the same categories and releases them in that order (IBehavior.cs, BehaviorScope.Dispose): reaction locks (SmartDisableReactionsWithLock/DisableReactions), idle (SmartPushIdleAnimation), motion profile (SmartSetMotionProfile), track locks (LockTracks/SmartLockTracks), custom light patterns (SmartSetCustomLightPattern). OnRelease hooks have no engine counterpart and run last. Within a category the undos run most-recent-first, which is required by the track-lock undos' saved-mask restore; the engine unlocks each map entry independently, so only the category order is observable.",
    "The policy review of 2026-09-29 (verdict C) found the divergence; @cozmo-verifier re-read all five release sites and the light-pattern tail (0x005bd1a2..0x005bd1c6), which the review had not read past 0x5bd180."
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
  "id": "M12-040",
  "title": "Carrying/docking lifetime after preceding control abort",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0063BE10..0x0063BE5C; 0x005113FA..0x00511414; Dock Abort precedes carrying/docking raw owner deletes; do not add another destructor/send at the raw-delete sites. Remaining owned state/subscription effects UNKNOWN.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "re-analysis/research/overnight/20261014-ov06-m12-040-check.md: R1 ctor order 0x0050FE24..0x0050FE4A; R2 Carrying ctor 0x00632A68..0x00632A82; R3 Docking ctor 0x0063BA1C..0x0063BA3E; R4 teardown 0x005113FA..0x00511418 (ranges corrected)."
  ]
}
```

```json
{
  "id": "M1-056",
  "title": "CozmoEngine::Update states 1 and 2: HasDesiredNumUiDevices, SendSupportInfo, SetEngineState(2); the per-tick loading poll and EngineLoadingDataStatus; delivery of the Init-time broadcast with no UI device",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/overnight/20261017-ov124-check.md: switch 0x004ED5C4 (engine+0x62), tbb 0x004ED5CC; state 1: HasDesiredNumUiDevices == 1 at 0x004ED5D8, SendSupportInfo 0x004ED5E2, SetEngineState(2) 0x004ED5EA; state 2: loading case 0x004ED5F0, then always 0x004ED612..0x004ED618 -> 0x004ED7C0 (EngineLoadingDataStatus ctor 0x004ED7DE, E2G slot +0x1C 0x004ED7E6; context+4 dereferenced without a null check 0x004ED7D2); progress out-parameter written every call before the done test 0x00523576..0x0052357E; UiMessageHandler::Broadcast 0x006625B0 calls [vtbl+0x48] with r2 = -1 (0x006625CA..0x006625CE), then local dispatch 0x006625FA.",
    "re-analysis/research/overnight/20261017-ov125-ov126-check.md: StartEngine handler 0x004ECBDA (seed, SetLocale 0x004ECBE2, SetEngineState(1) 0x004ECC40..0x004ECC4A); gate 0x006632F4..0x0066330E (desired 0, 0x0065DC50); Direct auto-connect 0x00662ACE/0x00662AEE, 0x0065C4C4..0x0065C4D0, first receive 0x0065C44C..0x0065C458 via ProcessMessages 0x00662860 (UI+0xF1); transition 0x004ED5D8..0x004ED5EE; SetEngineState 0x004ECCBC: store +0x62 0x004ECCD2, E2G UpdateEngineState tag 0x7B {old,new} (0x004EDC12..0x004EDC2E, 0x007276F8, 0x00719F7E..0x00719F9E), telemetry 0x004ECD2A; progress writers 0x0051EF3E/46, 0x0051F9B0..C0, 0x00521684/0x005216A6/0x005216D8, 0x0051F3F2/0x0051F424, 0x0051F480..8C, 0x005203E8; poll 0x00523576..0x005235E2; null DataPlatform 0x0051F394 (no 1.0: hang)."
  ]
}
```

```json
{
  "id": "M11-070",
  "title": "Delocalize callees: AnyRemainingLocalizableObjects(uint), SetNewPose/SetPose/AddVisionOnlyStateToHistory with GetDefaultRobotState, the AddInExistingPose default entry, and VizManager (no output)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-delocalize-and-update-object-origin.md: \"Unread\" section: 0x0051502C, 0x0051513C, 0x00626EF4, PLT 0x004A6FE8, 0x00510D94, GOT 0x0103E7DC.",
    "re-analysis/research/20261013-delocalize-callees-check.md: A1-A10 (0x00626EF4.., 0x0061EB98..0x0061EDE4, 0x006270F4), P1-P10 (0x005126EC..0x00512722, 0x00515034..0x005150AC, 0x00515144..0x0051518A, 0x005182B0..0x005182E8, 0x005310A8..0x0053127C), C1-C4 (0x00507B14..0x00507C50, 0x00506F50..0x00506F80), V1-V4 (VizManager SendMessage 0x006BE610 and SendVizMessage 0x006C05EC are bx lr; no transport)."
  ]
}
```

```json
{
  "id": "M11-074",
  "title": "Memory-map producers and readers not yet read: MapComponent::UpdateRobotPose (explored area), UpdateObjectPose, RemoveObservableObject, FlagInterestingEdgesAsUseless, ReviewInterestingEdges, ClearRobotToMarkers, FindContentIf/FindIf",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-quadtree-rest-check.md: UpdateRobotPose 0x0067E224..0x0067E50B; UpdateObjectPose 0x0067EA90..0x0067ECFB; RemoveObservableObject 0x0067F2C4..0x0067F58B; FlagInterestingEdgesAsUseless 0x0067E778..0x0067E85B; ReviewInterestingEdges/ClearRobotToMarkers 0x0067F58C..0x0067F7AB; FindContentIf 0x00681674..0x006816E7 and QuadTreeNode::FindIf 0x006884FC..",
    "re-analysis/research/overnight/20261015-ov42-44-check.md: R1-R6 0x0067E23A..0x0067E486 (cliff byte writer 0x005356C6..0x005356D4); O1-O8 0x0067EA9A..0x0067F0BC; caller order 0x00506F98..0x00506FB8, 0x00506296..0x005062D6 (double Add); D1-D3 0x0067F4C4..0x0067F544, 0x0067F2DE..0x0067F368, 0x006810E0..0x00681172; C1-C5 0x0067F5B8..0x0067F714, caller 0x00506A86..0x00506A98; Q1-Q6 0x00681674..0x006816DC, 0x006884FC..0x006886F4."
  ]
}
```

```json
{
  "id": "M11-088",
  "title": "ObservableObject virtual +0x5C / +0x50 overrides per concrete class (map Add/Remove and quad)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov42-44-check.md: base slot 0x004E02AE returns 0; ActiveObject-family slot 0x004E3882 returns 1; CustomObject 0x004F04FA returns byte +0x9C; callers 0x0067EB38..0x0067EC2E (+0x5C), 0x0067ED06..0x0067F0BC (+0x50).",
    "re-analysis/research/overnight/20261016-batch-check.md: slots by relocation: Block quad thunk 0x004E690D, base quad 0x0087713B, markerless 0x00502C33; +0x5C 0x004E3883 (1), 0x004E02AF (0), 0x004F04FB (+0x9C); Block quad body 0x004E62A2..0x004E6418; base quad 0x0087713A..0x008772D2; UpdateObjectPose 0x0067EA9A..0x0067EC2E (invalid->invalid error 0x0067EAF6, invalid->valid Add 0x0067EACA, valid->invalid Remove 0x0067EC3C..0x0067EC4C); AddObservableObject family table 0x0067F4CC, IsPoseTooHigh(1.0, 15.0, 0.0) on the robot-relative pose 0x0067EDA2..0x0067EDC0."
  ]
}
```

```json
{
  "id": "M11-094",
  "title": "Robot::GetBoundingQuadXY(atPose, pad): static robot footprint quad, padding, 2D rotation by the Z angle, translation (no re-root)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261016-ov109-110-answer.md: 0x00514D74..0x00514ECE (static quad guard 0x01059D48, data 0x01059D28: (22.0999985, -27.1), (22.0999985, 27.1), (-55.9, -27.1), (-55.9, 27.1); front 0x41B0CCCC; pad != 0 adds (pad,-pad),(pad,pad),(-pad,-pad),(-pad,pad); x' = x*m0 + y*m1, y' = x*m2 + y*m3; translate 0x004E68CC). Callers: 0x005F40xx (GuardDog), 0x005C48xx (LookAround), Robot::Update 0x00513E0E/0x005142DE, UpdateMarkerlessObjects 0x006257D6, CheckForCollisionWithRobot 0x00625A82, MapComponent 0x0067E41E/0x0067F60A, LatticePlannerImpl 0x004FE0A4."
  ]
}
```

```json
{
  "id": "M11-097",
  "title": "RobotStateHistory query and admission results: ComputeStateAt, GetLastStateWithFrameID and AddRobotStateToHistory's result",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261018-m3m4-settling-b.md: no record owned these; consumers M4-019, M4-038, M10-007, M10-015, M11-044, M13-030, M14-005. Prior reads: ComputeStateAt 0x00531784..0x00531808 (research/20260929-R-VIS-pre-extraction.md); AddRobotStateToHistory 0x00512B9C..0x00512F0C (on-ramp 0x00512BE2, ContainsOriginID 0x00512C44, failure 0x00512C38); the C12.4 gate 0x00513088..0x00513110.",
    "re-analysis/research/overnight/20261018-ov128-m3m4-settling-narrow.md, confirmed with corrections by re-analysis/research/overnight/20261018-ov128-check.md: ComputeStateAt vision-only exact key first (H1, 0x00531792..0x005317E6), raw query (H2), vision correction chain (H9, 0x00531808..0x00531AC6; the frame-increase delta accumulates before the stop test 0x00531A7A..0x00531A7E); GetLastStateWithFrameID raw then vision-only, newest first (H10, H11, 0x00531E34..0x00532090); UpdateFullRobotState admission and result paths (A2-A5, A8; 0x00512BAC..0x0051313C, AddRobotStateToHistory 0x00513598..0x005135C0; x, y from the flattened incoming pose; tail returns UpdateCurrPoseFromHistory ^ 1).",
    "re-analysis/research/20261019-b-m3m4-33-check.md, SetNextImage caller re-read by the manager 2026-10-19: r6 = image timestamp [sl,#0xC] (0x00652C22); GetNewestTimeStamp(robot+0x390) (0x00652C2A); `cmp r0,r6; it lo; movlo r6,r0` (0x00652C34..0x00652C3A): the query time is min(imageTs, newest raw key); ComputeStateAt(t, &state sp+0x11C, &time sp+0x120, flag 1) (0x00652C44). Result 0 -> 0x00652CFC (pose data built, frame handed on). Result 0x06000000 -> a log (0x00652C58..0x00652C78), return 0 (0x00653158). Any other result -> a log with the history's sizes and newest/oldest times (0x00652DD6..0x00652E32), return the result (0x0065315A). Neither failure hands the frame on."
  ]
}
```

## Caller and downstream closure supplement

| Row | Observable and native instruction contract | Current binding / precise disposition |
|---|---|---|
| S1 | 1–4: Config loader005206BC recursively loads shipped behaviors;00520754/75E rejects failed/empty reads, first duplicate ID wins via00520788/792. ExtractBehaviorID005BB9D4 parses behaviorID then strict generated name map. Container0059C34C..428 builds entries; actual DriveOff class11 case0059CABC..CADA alloc0x128, passes SAME Robot and JSON0059CAC8/CA to ctor005C0980, retains behavior. Q17 factory row11 and fresh case instructions agree. Shipped freeplay/driveOffCharger.json has class/id DriveOffCharger/displayNameKey/extra60; hiking/Hiking_driveOffCharger.json same class/idHiking_DriveOffCharger/extra45. | Register this existing behavior class with actual config+Robot context. Current factory has metadata row but Construct absent; do not hand-build a shadow alongside container instance. Existing resource loader JSON-comments/first-ID semantics reused. Missing extraDistance key uses native double+0 literal005C0A88→total96, distinct from shipped explicit60/45. Only the drive-off construction row is required; 76 other classes are not automatically a pilot job. |
| S2 | 1–4: Generic IBehavior ctor/read/scored rows already checked in M8 inventory/Q17. Constructor initializes flatscore0 and supplied metadata, reads optional configs and default repetition/running graphs(0,1). Q17GP3 preserves absent flatScore and reads scored overrides in order; no shipped emotionScorers. RunnableRB1–RB5 exact source: already-running bypass; required process/severe; unlock/recent-interaction/time-since-manager-switch; off-treads/platform/rawcarry; strategy; suppression-time. DriveOff vtable01026BF0 slots20/24/28→005C0E4A/4E/52 gives1,1,0, so waiting off-treads allowed but carrying rejects. | Bind actual AI/rawcarry/whiteboard/timer/manager inputs. Manual context defaults or nullable fallback do not recover real native eligibility. Shipped drive-off config adds no strategy/scoring gate, but chooser can apply scored config separately. |
| S3 | 1,2,4: Shipped activities_config MeetCozmo chooser Scoring assigns DriveOff1000; Feeding universal StrictPriority lists it first; activities/putDownDispatch.json Scoring assigns1000. Q17CS1–CS6 at0060A3D8..A64C exact ordered score traversal, positive-score gate, running graph+0.1 f32/floor0.01 versus nonrunning double RNG tie term then f32, strict winner comparison; StrictPriority0060B242 returns running/first runnable. | Actual activity chooser/BehaviorManager switch supplies selection, not global ranking of every hand-built behavior. Reuse checked existing chooser units and manager source inventory; pilot does not recover competing behavior bodies simply because their config is in this list. |
| S4 | 1–4: Generic lifecycle source M8 framework: Init005BCB54 sets clocks/running/callback state before concrete Init; Update005BD074 terminal acts-without-tag rule; Stop005BD08C clears running→StopInternal(pop)→last-run Timer clock→StopActing(false,false)→scope reaction/idle/profile/tracks cleanup. StopActing005BD34C notifies busy(false), stops helper without callback, cancels real tag. Resume005BCEAC establishes resume/suppression/reaction policy. | Preserve source-native immediate callbacks and real-tag cancellation. Existing deferred host `_pending` completion is not proof of synchronous E2G5A behavior. Profiles only where an actual SmartSetMotionProfile scope supplied them; no invented drive-specific profile. |
| S5 | 1–4: Final 5A dispatcher personally closed: ctor005BBD62..72 resolves functor addresspoint010265A0; invoke cell+18=005BF0BF. Invoker005BF0BE gets capturedthis+4, unwraps message+10 with Get_RobotCompletedAction, tail005BF0D2→008CBFAC. Interworking bxpc/nop then ARM008CBFB0 ldrip,[pc]/FB4 addpc,ip,pc and literalFFBE6A44→PLT004B2A00 HandleActionComplete→005BE1E6. | No scheduling queue belongs between native completion emit and matching-tag clear/callback. Local handle may safely use host ownership but preserve unsubscribe/readiness and synchronous ordering. This OPEN dispatcher row is now READ. |
| S6 | 1–4: Adopted OV18check L/Q/D/R rows settle action admission/tick/completion/cancel. Queue NOW0/retry0 does not Init immediately. List0053F580..5F2 visits queues in key order then watcher drain. Delete0053F9E4 guards tag, prepares game payload before dtor, clears current before synchronous E2G5A; watcher ActionEnding captures after derived destruction and later0054187E drains records/callbacks in handle order. R1–R6 explain synchronous Cancel loop→handler callback→single end creation and same-tick laterqueue execution; no fixed number RUNNING ticks. | Use real runtime rather than synthetic host callback. Native game and watcher payloads are separate snapshots, not one reused payload; this changes API3 and mood watcher4. Default30-second clock/track-locked terminal result already source-settled, not new timeout guess. |
| S7 | 1–4: Reuse adopted 20261013-m12-024-driving-lifecycle and M5-063 handler read. Init stores drive mask/tag/suppress/repeat; current newestoverride selected only Init, not Push. Start creates lift-safe(start,1,true,0,60,false), queueposition5. Completion success starts/repeats loop only appropriate path/repeat gate. End cancels start+loop, unlocks, creates one end action; end completion state4 relocks. Destruction state5 then cancel3tags, no fabricated end. Exact cancel reentrancy from OV18R1–R6. Fresh LiftSafe00544710..52 ORmask2 only rawcarry!=FFFFFFFF AND OffTreads==0, then passes all arguments to Trigger ctor; static00544758..70 same rule. PlainStraight base00547116..22 type8, tracks4. | Preserve current real action producers/handler; rawcarrying actual source is needed even when initial drive-off runnable prohibited carrying, because state can change during an ongoing action. Current actual owner versus old Docking/publication capture must not be assumed aliases. |
| S8 | 1–4: Concrete animation previously checked 20261012-m8-play-animation-actions with check corrections: Trigger00544228 resolves group, Init0054443C chooses every attempt; Play base00543F30 clears started/ended/failed, caches animation on first lookup00543F6E..7E and reuses on retry. Selected-name can update independently of old cached clip. Nonzero streamer tag→CA/CB/D5 then5F subscriptions; CAstarted, CB finite-loop count/ended, D5 app/API event,5Ffailed. Check ended wins. Completion getter005441DE..20E emits unionvariant1/string name (0075C544/0075B7D6/006C22DC). Dtor00543DB2..C8 compares streamer cached pointer including null==null; equality requests null stream before reverse subscription release/base. | Remaining cachedclip/union gap is real output1/3/4: retain semantic animation identity and retry behavior, not native pointer arithmetic; emit actual completion-name payload. Existing CA/CB/D5/5F bindings are newer built source pieces, not evidence that missing cached semantics can be ignored. Empty group/name NO_ANIM_NAME distinction retained. CodeLab custom-song remap is existing policy, not new pilot feature. |
| S9 | 1–4: Group inputs fresh0058A954 GetSimpleMood,0058A95C raw f32Mood+130→group draw. Clock producer exact: Robot.Update00513BE6 timerseconds savedr5 atBEA; Mood.Update00513E84..8A receives SAME capturedtime; EM3 stores130 at0067B67E. Mood.Update precedes AI/BehaviorManager and ActionList005140BC. Drive completion triggers emotion later in same tick; updated emotion may be visible before next periodic emotion API send, while cooldown input remains captured last-update130. | Bind CurrentMood to nativeowner.GetSimpleMood and CooldownTimeSec to owner last-update field, not fresh Timer per draw. One native mood update per engine tick, no duplicate Freeplay.Advance. All per-caller clocks keep source capture/order. |
| S10 | 1–4: Wire direct line source M12 inventoryG3.1–G3.6: Dole00507FB6..FC4 loads endpoints,00507FE0 AppendsLine. Generated source serializer recordM2-009 gives3D payload28=f32startXY/endXY/speed/accel/decel; Execute41={u16ID,boolmanual}; Clear3C={u16zero}. Initial Dole6 suffices one segment, but native ordering Clear→append→Execute remains. | Reuse generated serializer/Robot.SendMessage reliable owner and existing transport, no duplicate host packet format. Fresh line build must deliver exact f32words and profile/signedspeed. Firmware start/end C6 still input boundary; host success is not invented. |
| S11 | 1–4: Contact/platform source already independently checked 20261021-m13-017-located-charger-check A1–A11/P1–P4: synchronized fullstate statusbit12→SetOnCharger before incoming pose admission; everytrue observes old-owned Robot pose; dockPosepi,(30,0,0), currentorigin family4/type13 lookup/createuniqueID; Known observation flattened to world; existing/new branches preserve oldpose and broadcast world→map→state. Rising only after cleanup publishes platformtrue→threshold50→tracker, ChargerEventtrue then contactsstore. Falsecontact publishesedge but does not clearplatform/deletecharger. Later OnTreads platform tick queries ONLY family4 and intersects actual Charger basequad with Robotquad; outside/nocharger requestsfalse, setterORcontacts; transitionfalse sendsplatformevent/threshold400. Offtreads classifier/other50/400/150 travel schedule remain checked M10-005/M4-019. | Reuse actual Charger ObjectFootprint/Robot quad. 20261022REST claim chargerMarkerless/zero fallback was rejected by Claude and is NOT reused. Neither lattice importer nor Markerless/Ramp/HumanHead footprints enter this platform predicate. Frame pose/history current writer sourceM11-097→UpdateCurrPoseFromHistory005135F8..136F6→SetPose00513664; do not use new incoming statepose prematurely at contacts. |
| S12 | 4→1–3: Source map reuse OV42checkedR/O rows and 20261021charger-map-add-check: UpdateObjectPose0067EA90..EC4C family6return, old/newstate cases, perobject/rootIDcache with addedfalse retry and addedtrue Pose.IsSameAs(5,10deg), uniqueobject remove+add. Chargerfamily4 addcontent4 (removecontent5), actualrobot-relative IsPoseTooHigh(1,15,0), rootpose→slot50/pad0→image timestamp→map insert; cache commits afterinsert independent return. Match-ID type3 removal does NOT match chargerbase content4. UpdateRobotPose0067E23A..486 real Robot quad/cliff/clear/persistedpose. | Source callchain is available/recovered; current MapTiming has positive cache/relative/insert bodies and actual bindings (final binding table); historical blanket-unbound M11-074 prose must not be repeated as a current finding. Contactduplicate Add order is real. Existing tree insert/rootgrowth/aging/type callbacks/border algorithm checked inventories must be reused at exact branches, not legacy polygon lists. Numeric/exception/unsupported input boundaries remain explicit; no native cache complete flag on host missing path. |

## New precise source boundary: vision aggregate

Current M11-035 quote above still says these bodies are unread; Q19A02 and prior independent checks already read the mode/loop source, and this pass rechecked the behavior-changing aggregate. This is recovered-but-unreconciled evidence, not permission to alter status.

UpdateAllResults006542EC: null VisionSystem+1C returns0 (00654306/30C→654AC4). Otherwise initializes aggregate stack+88=0 at00654480/482, drains CheckMailbox at00654A74 while return exactly1, retains aggregate across results. Marker handler006544A2 nonzero setsfp=1 at6544EE/500; later per-mode failure sites similarly set1 and continue, then0065486A storesfp intoaggregate. End00654AC0 loadsaggregate,00654AC6 narrowsu8,654ACA..CE normalizes nonzero→1. It is not the last individual handler code, a sticky last-error flag, or 'newframepresent'. Robot.Update's real calibrationpointer gate precedes call; nonzero skips remaining platform/mood/AI/action/path tick. Per-result timestamp storesE8/EC at6549A2..AA and RobotProcessedImage emission654A14/1C occur even after handler failure before next mailbox result. Its bool is `(mask & 0x100) ? resultbyteC8 : 0` (654A02..0A); old Q19A03 shorthand omits that byte.

Current VisionSystem processes world/face/pet receivers on its frame path, logs nonzero worldFrame.Status1120..1130, but no production writer supplies Engine.VisionUpdateAllResultsFailed. Existing Engine gate therefore reports MISSING then continues. Minimum genuine correction needs engine-phase result-drain aggregate/reset/readiness and source mode ordering; no-result is0. A historical asynchronous LastResult cannot be turned into this synchronous drain result. Other unsupported per-mode processors remain named gaps; they cannot silently report native success.

## Objective4 recipient closure / fault boundary

For initialized/selected Socialize, no shipped key4 and the proven map/phase invariant give no state mutation: GetDesired rejects empty map before selection005B039A; only chosen-map path setsphase4 at005B05B2. OnSelected005AFF60 setsphase0 before Populate005AFF66; a matched objective decrementzero changesphase5 at005B08AC/AE, so phase4 is not retained after that transition. Objective4 miss retains mapcount and cannot pass empty-map+phase4 tail on this defined path. This does not extract unrelated behavior/RNG algorithms.

**Pre-selection exception to that invariant:** constructor005AFD1C does not initialize+94 (clears68..83, separately98/9C/A0/A4/A8/AC/B0/B4), but subscribes objective8B005AFDF6..DFC before OnSelected. Recipient005B07C2 readsuninitialized94. A fresh never-selected Socialize could therefore reach undefined state comparisons on an empty map. M1-034 applies to this exact pre-initialization recipient unit: skip/log it rather than invent ctorphase0 or suppress all objective subscribers. Manager needs a NEW owning Socialize-objective record identifying005B07C2 and this skip; M15-059 names Sparked and does not own this different class. No new official ID allocated here; no additional operator permission for standing M1-034.

## Geometry guard recovery; do not retain the old fragment boundary as a finish claim

After known.py minAreaRect/cv::error preflights, personally read shipped libopencv_imgproc calipers0009CA1E..CA7A: double cross-products of f32edges select firstnonzeroorientation; exhaustingallzero calls cv::error(-215,"orientation != 0",rotatingCalipers,line166) at0009CAB4 viaPLT17170. StringsDF204/F4BA8/DF215 and relocation prove the assert, not a guessed no-hit. Core cv::error000C0970 constructs cv::Exception then calls000C0804;000C08C4..C091A allocates/copies/throws exception (or optional debug-break null-write000C08C0, M1-034). Engine GetBoundingQuad landing004E65C6 beginscatch; diagnostic004E672C, errorflag004E6762, then RectangleFallback004E6778; separate f32halfwidth/halfheightadd004E6794..B4, angle+0; endcatch004E67C0→rejoin normal RotatedRect.points004E6512. Existing adoptedOV08R1–R3 checks Rectangle min/max004E81B4..825E and this fallback sequence.

Thus current MinAreaRectF orient==0 NotSupportedException→Footprintfalse does NOT reproduce native output. Need actual defined native Rectanglefallback integration for that condition, not an unavailable-geometry guard counted as completed cache. EH catch selection/type coverage is now personally confirmed by the exception-table witness below; do not claim M1-034 for a normally caught cv::Exception. No claim that finite nondegenerate nativefamily1/2 inputs can never reach this assert is needed or made. Noncube actual virtualdispatch remains separate: family1passiveCube1x1/Block2x1 and family2ActiveCube slot50 resolve to Block quad (OV68tables0101D574/0101D8A8/0101CDEC→004E690C→004E62A2); chargerfamily4 is outsideconfigquery. CurrentFootprint.TypeIsCube gate cannot be used as sourceabsence; source-backed Block dimension/corner helper can be reused where actual admitted type/pose/dim binding exists. Any unproved input availability stays UNKNOWN, not a successful no-configuration update.

## Public adapter decision is pending, not native behavior

Manager has asked the operator about the WHOLE public configuration adapter (ConfigurationSeen, FirstSeenSec, public cache/MID timing). No policy answer is assumed. Native-ID cache can serve membership, count, tallest and downstream audio independently of legacy host cache. OnConfigSeen's recovered count-only slice can consume nativecounts without claiming that whole different behavior complete. Existing public MID is a cached host snapshot; native GetBaseInteriorMidpoint00617A8C computes live from looked-up objects and mayfail. Replacing snapshotMID with livequery/zero/throw silently changes API3. Until disposition, preserve public adapter and explicitly keep it out of certified native consumers; no whole-pilot finished claim while supported API provenance remains unresolved.


## Current quotes for final narrow reuse

```json
{
  "id": "M13-011",
  "title": "The three path segment messages are a Planning::PathSegment copied field for field",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PathDolerOuter::Dole 0x00507E4C switches on the segment type at PathSegment+0 (1 line, 2 arc, 3 point turn; 0x00507F40/0x00507F44/0x00507F48) and copies words at +4, +8, +0xC, +0x10 (arc adds +0x14), then the speed profile at +0x1C, +0x20, +0x24; a point turn also copies the byte at +0x14 (line 0x00507FB6..0x00507FD2; arc 0x00508008..0x00508028; point turn 0x00507F5E..0x00507F7E)",
    "the field definers: PathSegment::DefineLine 0x0085BC00, DefineArc 0x0085BC98, DefinePointTurn 0x0085BD28, SetSpeedProfile 0x0085BC90"
  ]
}
```

```json
{
  "id": "M13-034",
  "title": "Planner dependencies: DidObjectsChange (+0x54) and its writers, FindClosestPlanSegmentToPose, PlanIsSafe/PathIsSafe/ApplyPathSegment, xythetaPlan::Append, AppendToPath and Path::Append*, ApplyMotionProfile sub-ranges, MinimalAngle compares (the ComputePath F4==0 goal loop unreachable)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-planner-internals-check.md (and re-analysis/research/20261013-planner-internals.md): \"Unread\" section: 0x00623020, 0x00855D90, 0x00850E34, 0x00850610, 0x0085CE88, 0x0085D3C8, 0x0085D46C, 0x00851870, 0x00856310, 0x004FE7F6..0x004FE9E2, 0x00508C14..0x00508D90, 0x00508F48..0x0050906E, 0x00509070..0x005090DE, 0x00503E7A..0x00503EA2.",
    "re-analysis/research/20261013-planner-dependencies-check.md (and re-analysis/research/20261013-planner-dependencies.md): DidObjectsChange 0x00623020 and writers 0x0061E2D8/0x006211E6/0x00622596/0x00625E82/0x00624FAC; FindClosestPlanSegmentToPose 0x00856310..0x008569BA; PlanIsSafe 0x00850B88..0x00850D88 (startIndex >= count -> 1; old penalty pushed); PathIsSafe 0x00850E34..0x00850EB0; ApplyPathSegment 0x00850610..0x00850A70 (returns the accumulated penalty, 0x008509F2 skips 0x008509F4); Append 0x00851870..0x00851896; AppendToPath 0x00855D90..0x00855E74; AddSegmentsToPath 0x0085595A..0x00855AAE; Path ctor 0x0085C714..0x0085C746; Append* 0x0085CE88/0x0085D3C8/0x0085D46C; ApplyMotionProfile 0x00508C14..0x005090DE; MinimalAngle 0x00503E7A..0x00504174; goal loop 0x004FE75A..0x004FE9DC unreachable (+0xF4 written only by the ctor, 0x004FCCE2)."
  ]
}
```

```json
{
  "id": "M11-090",
  "title": "Vision/world teardown residue: std::function capture finalizers in the eight ProtoHandle remove routines, StdMatAllocator::deallocate, and whether Ramp/Bridge/FlatMat/Platform/HumanHead/Block_* are ever instantiated",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov50-52-check.md: remove routines vt+0x10/+0x14 (per OV50); core getDefaultAllocator 0x000815C0 -> 0x00081568; Ramp::Ramp only from Ramp::CloneType 0x0050F89A (direct-call scan; vtable/VTT references not scanned).",
    "re-analysis/research/overnight/20261016-batch-check.md: ProtoHandle removes 0x004EB090, 0x0051D338, 0x004EE634, 0x0051940E, 0x006634F4, 0x0066365C, 0x006A3BA4, 0x0083BC90; Mat route core 0x00024090 -> 0x000816B4 -> 0x0007F8E6 -> 0x00080CB4; Block_Cube1x1 ctors 0x005D725A, 0x0060301C; other six classes only from their own clones."
  ]
}
```

## Final primary witnesses and current positive bindings

| Row | Exact checked source / current correspondence | Build or boundary |
|---|---|---|
| G1 | Fresh Path ctor0085C714 count+4=0 at0085C73E/740; DriveStraight00547666 makes a fresh Path and calls AppendLine exactlyonce005476AA. AppendLine0085CE88 rejects only negative signedcount; count0 succeeds. Direct linewire NativePathComponent.SendNative71→PathSender.Send112..115→CozmoRobot.SendToHandler for segment, SendMessage for Clear/Execute; native sender split already source-cited in these methods. Motion80..82 attaches SAME path's C6 owner and path tick; Sensors1009 supplies currSegment; EngineRobot.Update1341 runs path after ActionList. | Existing direct wire/status plumbing is positive source-shaped, not a missing synthetic sender. AppendLinefailure negativecount cannot occur on fresh-one-line producer without memoryfault; manager may omit unreachable input branch under AGENTS and remove stale unconditional diagnostic. Do not manufacture a negativecount production abstraction. Fresh typed line still needs exact mainyaw/storedcenter/f32/profile correction. |
| G2 | Current ExecuteCustomPath clears `_computeModel` and activeplan/stopdelegate, HandlePossibleOriginChanges249 returns when thatmodelnull. This corresponds native planner-pointer-null early return after0064B1E6 clears sharedpointer and1FA tails ExecutePath through008CCF8C. | No custompath replan/source importer is introduced. NativePlannerSet may be retained as host storage only if stopped and not consulted by this custom path; no pointeridentity job without an observed effect. |
| G3 | SetPose0051508C raw Robot+284,+8 is authoritative. Current NativeDelocalization115..126 owns ForwardPose, but reads PublicationCarrying fallback(-1,-1). CozmoRobot382..385 captures its Carrying property; native-construction callback adopts owner. CM13-04 already proved old Docking.Carrying may be a separate construction-time fallback. | Bind producer to current actual EngineRobot.CarryingOwner and preserve rawFFFFFFFF sentinel; absent owner is unavailable, not no-carry. Reuse checked MoveRobotPoseForward00517EC0 assignment/rotationparent/z0/f32math rather than parallel double formula. Existing field storage is enough; no second ForwardPose owner. |
| G4 | Native currentpose sourceM11-097 ComputeStateAt/newest→wrtcurrentorigin→SetPose00513664; current NativeStateHistoryAdmission77 calls EngineRobot.SetPose. CozmoRobot349 feeds World.CurrentRobotPose=Engine.Robot.MainPose; BlockWorld829 Map.RobotPose forwards it. VisionSystem100 gives World.LastImageTimestamp=CameraSettings.LastProcessedImageTimestamp; BlockWorld831 forwards it to map. B-M11-21/22 checked+E8 source/tail, no hostframe clock. MapTiming64..149 implements actual cache/relative/height/quad/insert flow; nullable test recipient hooks fall back to these production bodies. | Earlier remaining-map reports' blanket missing cache/relative/add warnings are historical. Keep any specific numeric/callback/availability gap only if reached in current path. Current source map can be reused; no parallel polygon-list update. |
| G5 | BlockWorld.OnObjectPoseChanged2447 routes to Configurations.SetObjectPoseChanged; ManipulationSystem39/40 attaches that plus frame-tail Configurations.Update to SAME Vision.World/Robot. Frame-tail1610 corresponds0062520C; gate/cache/reset and A-before-B audio owners already checked B-M13-15/18. | New actual all3type producer substitutes the unverified allpairs builder; it must serve native membership/tallest/count readers. Host adapter question is separate; do not reroute engine consumers through host allpairs to preserve an unsourced API. |
| G6 | Source whiteboard+44 initialization-1 at0056A2B6; writer005C0E02/06/08 loads RobotAI264→Whiteboard18 and stores floatTimer seconds; runnable005BD93C reads this global field. Current DriveOff sets BehaviorContext.LastDriveOffChargerSec153. Native manager creates one context in CozmoRobot198..202; Freeplay/manual callers can have different contexts. | Need one robot-lifetime timestamp scalar and source getters for every context reading it. No full Whiteboard redesign: native container/allocation can differ, value/lifetime/readiness cannot. Do not let a facade disposal or context switch erase later eligibility state4. |
| G7 | Root-rotation center helper now personally rechecked (knownGetZRotatedPointAboveObjectCenter):0087758E root-flatten,7598 stores rootZ;75A6 obtains rootrotationmatrix;75AE GetDimZ(ROOTmatrix);75BE rootyaw. Original localXY loaded75CE/75DA.75E0 f32dim*scalar then75E8 f32rootZ+product;outputparentFindRoot(flat)75F6,Pose3dctor761C;release root/flat7630/36. | CM13-05row20 uses scalar+0.5. Existing PlacementProducerGeometry.Center scalar0 cannot be reused unchanged, nor ObjectPoseQueries' local-rotation dimension. Use actual passed scalar, rootdimensions, localXY/rootZ+yaw/rootparent as source specifies. This is source-settled, not an extra recovery quota. |
| G8 | Personally decoded EH coverage after knownpreflights: exidx00BAD934 PREL31 function004E6490, nextword→genericextab00AE4AE4. LSDA00AE4AEC startsff00; typebase00AE4BE4; callsiteencoding03, table234bytes at00AE4AF4. Entry(start64,len0E,landing136,action1) covers004E64F4..6502 including minAreaRect BLX004E64FE and targets004E65C6. Action00AE4BDE bytes0100→filter1,next0; type1 absoluteword00AE4BE0=0 meanscatch(...). | All cv::error exceptions from this call reach defined Rectanglefallback. No remaining EHtypeUNKNOWN and no M1-034skip for this caught normalassert. Independent B decoded same primary data. Existing source helper fallback can be integrated as a real behavior correction; other managed gap exceptions are not automatically native CV failures. |
| G9 | Adopted20261016-batch-check narrows passive world objects: Block_Cube1x1 made as reference in BehaviorCheckForStack005D725A and AcknowledgeObject0060301C; no passive-world witness; Block2x1/Ramp/etc ctoronlyownClone unreachable. CurrentM11-088 records this bounded passive-world absence; native familyfilter still{1,2}, no new cubeonly predicate. ActiveCube and passiveBlock vtable slot50 sources already mapped to Block quad inOV68. | Do not infer from nativequeryfamily1 that some arbitrary unsupported concrete type must be silently skipped. Normal supported lightcube footprint is sourced and its orientationzero assertion now has fallback. Existing passive-world scope/census is reused, not a new operator feature exclusion or universal geometric nondegeneracy proof. If a future supported producer admits a new family1type, its dimensions/quad must be bound explicitly before claiming that input supported. |

M13-023's current `unresolved` explicitly answers part(e): unshipped bioniclibm calls are EQUIVALENT_IMPLEMENTATION, operands/widths/order exact. Whole record remains IMPLEMENTATION_GAP for otherparts. Existing code comment alone never grants that status to the complete SetPose/line/quad path; no new equivalence policy is proposed here.

## Bounded dependency graph

Normal received fullstate→sync/contactbit→SetOnCharger(currentownedmainpose)→chargerlookup/create/uniqueID→Known rootobservation→worldpending + mapcache/quadinsert + state/light recipients→contact/platform edges. Incoming history admission separatelyupdatesMainPose→storedForwardPose. Vision mailbox's per-drain failure gates the following engine tick: platform/overlap→Mood.Update(capturedfloatnow)→AI/severeneeds→BehaviorManager configuredselection/baseeligibility→DriveOff.Init(reactionlock,conditionaltriplet)→StartActing(realtag/5Asubscription)→ActionList.Tick→DriveStraight.Init(mainyaw,storedcenter,profile,freshline)→custompath(Clear,Append3D,Execute41)→firmwareC6/currentsegment→Pathstatus→driving start/loop/end actions/group(mood,cachedclock,RNG,head)/streamer→real5A completion→DriveOffsuccess objective8B(APIthenlocals)→MoodTrigger, followed by watcherdrain(actionresult-mapped mood). Subsequent off-platform/noaction behaviorupdate stamps robotwhiteboard andcompletes; Stop pops drivingoverride before cancel and releases reaction scope. Later accepted imageframe consumes pending configrebuild→all3sourcecontainers/prune→tallest/publicstate→A/Baudio only on world-predicate transition. Public host firstseen/MID/event adapter remains pending separate operator decision.

Robot traffic/API/later state crossings are explicitly 1–4 above. Already settled lower transport/CLAD/streamer bodies are reuse boundaries, not new unreviewed assumptions. Imported libm operands/width/order retain current part(e) boundary. Firmware-supplied C6/animation acknowledgments remain inputs/HARDWARE_ONLY where already recorded; camera recognizer/preprocessing internals of other supported features retain their existing records and are not newly certified from this read. This graph does not expand unrelated behavior bodies or global constructor diagnostics into the pilot.

## Settlement inventory after implementation and independent whole check

- M13-017 is the WHOLE pilot target, not settled now. Source logic and required joins inventoried above; after build/check it must not retain generic historical 'unknown contact/default/5A/mood' prose when those have actually been closed. Supported public adapter decision remains outside a false finished claim.
- M13-040 can be a complete confirmation candidate only with actual normal ingress/map/configpositivepath and retained fault/availability diagnostics verified; contactidentity alone is insufficient. Current B-M13-9/10, B-M11-21/22 and map/root work supersede exact historical diagnostics named above.
- M7-026 whole row can be a confirmation candidate after actual MoodOwner GetSimpleMood/raw130 attachment and tick semantics pass; M7-012/013/020/023 broader model/actions/history/config/lifetime need their current whole scope compared, not assumed accepted. No M7 subsystem acceptance claimed.
- M13-013 contains plainStraight pilot slice but title/other residuals cover DriveOffContacts retry subclass; leave exact nonpilot remainder. M12-002/032 own additional path types/planning: direct custompath slice does not settle all planner branches. M7-018 owns other factories/request-list; drive-off binding closes only its named case. M8-001/015 common source is recovered; other real consumers/teardown edges retain exact leftovers.
- M8-020/021 need actual cachedclip/union/deletion semantics plus existing callbacks verified on reached driving actions; abstract 'bridge works' is not a complete concrete-action claim. M5-063 already EXACT_SOURCE is reused; missing producers/inputs stay their own rows.
- M11-037/M12-049/M12-043 commonproducer/count/tallest slice is necessary, but BuildPyramid callbacks remain separate. NEW Socialize-objective record is necessary for currently unowned recipient/faultboundary before wholeclaim. No IDs/statuses changed by this extractor.
- M8-027's current unresolved says capture destruction is memory-only, no behavioral work left. It is a confirmation/settlement candidate for manager/Claude against current record, not a new retaincount implementation task. Snapshot content/stability remains required wherever a consumer reads it.

No tests or hardware run; no source, manifest or git mutation. This is a complete bounded inventory offered for independent whole-path review, not self-certified PASS, build authorization, accepted subsystem or finished behavior. Any review-discovered unread observable step must be recovered before code. Public adapter policy remains pending and cannot be answered by elapsed time or assumed approval.


## O3 narrow inactive/reselection clarification (manager follow-up)

The no-mutation result for shipped objective4 extends to an **initialized then deselected** Socialize; it is not limited to currently selected activities. Known preflights and primary checks of the exact mutation join establish:

- IActivity.OnDeselected005B33B8..005B3662 changes base lifecycle/scope/chooser ownership, but never Socialize phase+94 or objective-map+A0/A4/A8. Its final virtual+2C call005B3658/365C resolves through actual Socialize addresspoint01025690+2C to005A7380, an empty `bx lr`. No Socialize-specific deselect map clear exists.
- The bounded native call census for ReadPotentialObjectives005AFC28/PLT004B1B3C, PopulatePotentialObjectives005AFF6C/PLT004B1B54/veneer008CBE1C and IActivity.ReadConfig005B2A5C/PLT004B1D4C finds only005AFD4C (Socialize constructor→ReadPotential),005AFF66 (OnSelected→Populate) and005B2924 (base constructor→ReadConfig). No native Socialize ReloadFromConfig routine or reload map-clear caller exists in that census; the similarly named005B0EAC belongs to ActivitySparked. Base ReadConfig was inspected only for this mutation question: it handles base metadata/chooser/strategy slots, not Socialize phase/objective map, and its native caller is construction.
- Re-selection always writes phase0 at005AFF60/62 BEFORE tailing to the map-clearing/repopulation routine at005AFF66. Thus repopulation cannot leave a formerly phase4 recipient with an empty map and phase4. Deselecting does not alter the previously established map/phase invariant; matching objective completion already moves phase5 when its zero count is erased. Shipped objective4 remains a map miss and cannot reach the StopOnNextActionComplete tail005B089E..08AE through a newly emptied phase4 map on these native lifecycle paths.

The exact pre-first-selection undefined94 exception remains unchanged and needs its new owning record/M1-034 boundary. No guessed phase0 constructor, inactive-recipient suppression or general reload API behavior is added. This is a narrow closure of the questioned lifecycle mutation join; existing M15-006 current title/status/evidence and base lifecycle sources are quoted earlier. No production/tests/git edits.


## Narrow pre-build clarification: completion name, activity entry and objective4 owners

**Completion is current animation-name+78, not cached clip+90 or base debug-name+48.** Fresh known preflights005441DE/004E462A and actual instructions:005441EC passes action+78 into string-copy004E462A at005441F2; helper004E4632..464A reads source short/long string length/chars and copies them.005441FA installs animationCompleted variant1. Trigger.Init00544472..447A overwrites+78 from every newly selected name before calling Play.Init. Play.Init00543F3A..3F42 reuses nonnull+90 cached clip without changing+78. Thus retry may **play retained old clip but report newly selected current animation name**. Keep these semantic values separate. Any builder contract saying payload is cached-clip.Name is corrected by this primary witness. Existing S8's 'name' means+78.

**Activity.OnSelected outputs are on configured admission, conditional on which entry is claimed.** S3 identifies real configured chooser routes; if this pilot builds/claims transition into one of those activities, its common base OnSelected005B312C is on the production path and cannot be treated as diagnostic-only. Order: capturedfloatnow→start54 at005B3142, existingchooser OnSelected005B314A, **unconditional** PublicState.UpdateActivity005B3154 with activity byte+34, then conditional driving/idle and process enable, then concrete hook005B324A/324E. NativeActivityID EnumToString00766C50 resolves table01033070: Feeding=2 (string00BF0EAC), MeetCozmo=6 (00C19928), PutDownDispatch=13 (00C19981). This state feeds already checked A-before-B audio recipients; Feeding2 additionally suppresses their world-event branch. Current BehaviorPublicState.UpdateActivity exists but has no production caller; Activities.OnSelected only emits M15-006 MISSING. Therefore it is a real common caller gap to build for claimed configured admission.

For those exact three shipped activity configs, **driving/idle push and matching removal do not execute**: activities_config.json MeetCozmo/Feeding and activities/putDownDispatch.json omit driveStartAnimTrigger/driveLoopAnimTrigger/driveEndAnimTrigger/idleAnimTrigger. Previously checked Q17AT1 base constructor initializes all four38/3C/40/44=23F; absent optional keys preserve those sentinels. Native005B3158..3160 checks start38==23F before push005B319A;005B31AC..31B6 checks idle44==23F before SmartPush005B31BC. Deselected005B33D8..33E0 and005B341E..3422 use same idle/start sentinel guards before corresponding removal; it does **not** publish UpdateActivity(0) or another activity itself. The next selected activity publishes its own ID. No invented activity triplet/idle or extra clear event belongs to these routes. Other activities with configured triggers retain their own records; not automatically included.

This does not certify the whole Feeding/MeetCozmo lifecycle: S3 is selection-route evidence. An already-selected activity is a possible explicit admitted-state boundary; if admission includes its transition, common base state publication above is included. Do not silently claim newly entered configured activity while preserving a missing state publication, and do not expand unrelated concrete activity internals from its name alone. M15-006, M5-063 and M13-038/039 current records are quoted earlier; no new record status is asserted.

**Objective4 attachment is semantic owner-scoped, not full specialized activity implementation.** Current Activities.cs925 and loader1055 have generic Activity nodes, not recovered ActivitySparked/Socialize runtimes. Source-proven no-op Sparked4 processing may be omitted after checking actual shipped raw configured objective!=4; no fake counter/state or complete specialized class is required. A generic node's absence is not proof that native subscribers do not exist. Socialize pre-first-selection M1-034 skip/log requires the actual source-equivalent constructed activity owner and its external-interface/subscription lifetime, not a global observer or invented initialized phase0. A narrowly attached unit can observe the actual source OnSelectedInternal phase0 initialization point (005AFF60/62), retain that initialization fact across deselection, and for shipped objective4 use the proven all-defined-state no-mutation invariant. The unit need not simulate objective maps or other activity behavior solely to handle4. It must retire with its actual owner. Late host Freeplay-only construction must not be represented as native construction if native owner already exists. If source owner/construction readiness cannot be bound, keep that exact ownership MISSING; do not construct a global substitute or manufacture native phase0. Non4/changed-config behavior remains its separately named unresolved scope. No full ActivitySparked/Socialize feature claim follows from this objective4 slice.

## Pre-build refinement U/V/Q — serializer and input joins reopened after initial read PASS

These are newly made explicit details of already quoted M8-015/020/021, M11-035 and M8-001/003/004/013 owners, not new settlement claims. The earlier whole-engine read PASS did not independently establish these omitted byte/input details. Independent per-unit checking is required before adoption. Actual model/effort telemetry remains unavailable. known.py was run for IActionRunner0053FDB0, RobotProcessedImage,0072483C,00730D6E, Vision.Update006B4D5C, CheckMailbox006B2AD4, ComputeMean006B2CDC, IBehavior005BBB74/ReadFromJson005BBFB4/ScoredConstructor005BC388 before tracing.

| Unit | Exact instruction witness / adopted packet | Production consequence |
|---|---|---|
| U1 plain DriveStraight default | IActionRunner0053FECA loads type, FECC adds2, FED4 TBB base53FED8. Type8 index10 reads byte1B at53FEE2, reaching53FF0E;53FF14 invokes Set_defaultCompleted. Base getter52B0A6 copies cache+1C. Existing20261021-m8-animation-completion-payload.md establishes setter75C84C..C86C/tag6. | Plain DriveStraight completion union is variant6, no payload. Wire literal `06`, never a rawU32 placeholder. Concrete animation getter remains variant1/current selected name+78, separate from retained clip+90. |
| U2 string and default wire | Reuse checked payload packet: Pack75C8D8..C94E writes U8tag75C8EE. Tag1 branch75C90E..C916 calls6C22DC..C2326; length narrows with UXTB6C2304 before WriteBytes. Default6/invalidFF write tag only. | Animation bytes are `01`, U8 byte count, that narrowed count of stored name bytes, no terminator. 256-byte name =>0100;257=>0101 then first byte. Size reports full string length+2, not narrowed serialized length. Preserve SafeMessageBuffer failure handling; no invented clamp/reject rule. |
| V1 C8 producer: image mean, not bool | Vision.Update6B5120..2A admits mode8 only for ShouldProcessVisionMode==1.6B5136 passes current gray image r4, step10 at5138 to ComputeMean6B2CDC via513A.6B5142 stores returned u8 at VisionSystem+38C,5146/4A sets mask+37C bit100. Headerbase+378 is explicit6B4E34/38; queued reference6B5680/86. CheckMailbox6B2B3A..40 copies five words and byte at header+14 to output. | **Correction to earlier wording:** resultbyteC8 is an imageMean **byte**, not bool or handler success/image quality. Never normalize it. |
| V2 ComputeMean arithmetic |6B2CE0 rows/cols;6B2CEE sum0;6B2CF8..2D0E sample row/column indices0,10,... using row stride (6B2CF4/6 stride*10); add pixel u8 to signed32 sum.6B2D14..30 divides by ceil(rows/10)*ceil(cols/10) with signed division.6B2D34..44 clamps below0 to0, above255 to255 then UXTB. | Native sampling mean is independent of image-quality classification. Normal nonempty admitted gray images have positive divisor; zero-size/division failure is not manufactured into a success value. |
| V3 ProcessedImage construction |6549B8..E6 visits VisionMode ordinals0..15, appends present mask bits in ascending order.6549EE loads raw C8;654A02/08/0A chooses mask100 ? rawC8 :0. Timestamp6549F4/F8. Robot.Broadcast654A1C follows constructor654A14. | Emission includes raw mean byte; mode8 absent emits mean0 regardless old stored mean. Frame failure aggregate and mean have separate meanings. |
| V4 ProcessedImage wire | RobotProcessedImage.Pack712FE0..13018 writes timestamp4bytes at712FF0/FF2;71301A..4C writes U8 vectorcount (element count from bytes/4), then730D6E..9C writes every VisionMode as4bytes;712FFE..1300C writes raw byte field+10. Set_RobotProcessedImage72483C..7E stores U16tag43. | Body is U32LE timestamp,U8count, each mode U32LE, U8imageMean; outer union U16LE0043. Count overlength behavior is not exercised by bounded16 native modes; no bool conversion. |
| Q1 base gate defaults/keys |5BBC08 process+1C=0;5BBCAC..BE unlock+70=55,severe+74=3,recent+78=-1f,maxswitch+7C=-1f. ReadFromJson5BBFB4..C26C: requiredUnlockId literal5BC2D4 writes+70 at5BC118; requiredSevereNeedsState literal5BC348 calls NeedIdFromString5BC138/writes+74; requiredRecentDriveOffCharger_sec5BC17E..196→+78; requiredRecentSwitchToParent_sec5BC198..1B0→+7C. Absent optional entries preserve defaults. | Shipped drive-off omits these keys, so process/severe/unlock/recent/max-switch bypass follows exact parsed state, not fabricated true delegates. Actual nondefault consumers retain RB2/RB3 input requirements; this is no license to supply guessed writer values. |
| Q2 exact existing packet location |20261007-M7-M8-rows.md RB1–5 lines747–751; CS1–4 lines879–882,CS5 line1061,CS6 line1095; GP1–5 lines1431–1435. | GP3 repetition and running default empty graphs to(0,1,true), not0. Missing flatScore preserves prior (constructor0). Running chooser does binary32 graph+score then+0.1f and nativeGT floor0.01f; nonrunning RandDbl limit bits3FB99999A0000000, widen f32 score/addf64/narrow once; ordered traversal/ties and NaN as packet. No extra RNG draw for running branch. |

Mood host-input boundary: existing20261021-m7-mood-init-input.md explicitly establishes DataPlatform pointer context+8 (4EA742); null skips Init/load at510352/354, not periodic Mood.Update513E84..8A. With pointer present, missing-file loader logs/sets error then continues522364. Init67AED4 StaticMoodData.Init then67AEE4 action-map load, without parser-result gate before Robot12C registration. Full null/partial JSON model outcome remains UNKNOWN in that packet; do not advertise initialized-success from a constructor-model fallback. Manager's host DataPlatform/filesystem-provider choice is separate from native pointer evidence.

## Mood missing-file/null-JSON refinement N — defined initialization, not a no-update fallback

This closes the narrow null-input UNKNOWN above for fresh construction and the missing/unopenable/empty main file. It does not certify arbitrary partial/wrong-kind JSON. M7-012/013/020/026 current title/status/evidence are quoted earlier in this report; no status changes. known.py preflights:67ADB6,67AEBC,67CD4C,67CDC2,67CE6C,67AFB4,67B5D4,679406,804BD0,67A278,67A2F0,8E5F14,8EAF7C,679BC0,67A478,84C01C,67B85C,51ED30,8E0938,8E082C,8E09A4. Primary scratch: mood-null.txt, mood-null-events.txt, mood-null-input.txt, mood-null-loader.txt under .scratch/driveoff-pilot/.

| Row | Instruction witness / already checked source | Exact outcome |
|---|---|---|
| N1 input value | RobotDataLoader51EE42 sets r0=loader+C0;51EE48 sets sl=r0+10;51EE52..56 constructs Json::Value(type0) at loader+D0. Reuse checked20261013-small-reads-2.md missing/unopenable/empty stream paragraph and20261012-m5-x9-extraction.md G5a: readAsJson84C090..C0C6 fopen/parse; false logs then returns false. Reader8E09A4 default branch8E0ADE.. stores only root diagnostic offsets+10/+14 (8E0AFE/8E0B20), not value payload/type. | A fresh missing main file leaves the mood-config slot **JSON null**, with visible read error. A pre-existing destination is not erased merely by read failure. This is not a null DataPlatform pointer. No M1-034 fault occurs in this defined missing-file path. |
| N2 null key access | Const Json::Value::find8EAF84..8C accepts type0 or7; type0→8EAFC2 returnsnull. Const operator[]8E5F26..36 returns nullSingleton on miss. | Config[decayGraphs] and config[actionResultEmotionEvents] are safely null for null root; no guessed object materialization or terminating wrong-kind access. |
| N3 constructor model | StaticMoodData ctor67CD54..60 constructs9emptygraphs;67CD68 event mapper;67CD74 empty default repetition graph;67CD78 InitDecayGraphs. Init67CE5C calls InitDecayGraphs again, adding only to independently empty curves. EM7 existing checked pairs, allf32: (00000000,3F800000),(41700000,3F800000),(42700000,3F666666),(43160000,3F19999A),(43960000,00000000), booltrue. | Nine default curves are actual source state. Current host missing-file fallback must retain these curves, not merely rely on present emotion values being zero. StaticMoodData is source-global; native pointer identity/container choice is not required here. |
| N4 ReadFromJson(null) |67CE88 Clear event mapper;67CE92 Clear default repetition graph.67CE9E reads decayGraphs,CEA4 isNull,CEA8/AA selects warning branch;CEC8 warns;CEEE resultfalse,CF00..04 returns. | Returnsfalse **early**, leaving nine existing decay curves unchanged, mapperempty, default repetition graphempty. It never reaches later default-repetition AddNode fallback67D1B4. Do not invent that fallback on the null-root early return. On a reread this clears events/default repetition while retaining existing nonempty decay curves. |
| N5 Init continuation |67AED4 calls StaticMoodData.Init and ignores returnedfalse;67AEDC safely gets null actionResultEmotionEvents;67AEE4 loads it. LoadActionCompletedEventMap67AFC0/C4 null→67AFE6..B028 logs/returns without inserts or clear.67AEE8 Robot12C gate;AF0E..AF38 registers all-action-ended watcher even if external interface missing. Separate game handlers are externally gatedAFEE..AF0A. | Fresh action-result map remains empty; existing map is retained on null reload. Init still establishes watcher ownership, rather than skipping the entire owner or update. No separate source mood-ready bit is exposed here; keep resource failure diagnostically visible, not silently report a complete configured asset model. |
| N6 owner and normal Update | Mood ctor67ADBE..CA constructs9Emotions; each679412/14 stores value18=+0,decayclock1C=+0.67ADE2 lasttime130=+0; actionmap/event-time/ignore containers empty. Existing EM3/4:67B67E writes now130;B682..6A0 gets each nonempty decay curve and updates9 emotions;B6A4 sends mood. | Periodic Update remains defined and active with null main JSON. Normal history/time/decay arithmetic and MoodState API output still run; initially nine emotion values remain +0, SimpleMoodDefault2 until actual supported inputs change them. Missing resources do not justify suppressing periodic output or group-selection clock advancement. |
| N7 absent versus surviving event files | Init subsequently Robot.LoadEmotionEvents510EE0..FBC (existing checked packet) loads only nonempty loader entries. If no event documents exist, mapper staysempty. TriggerEmotionEvent67B874 FindEvent,67B87C/7E miss→67BA88 warning→67BACC return, before event timestamp/affector path. If a valid separate document survives, EmotionEvent.ReadFromJson679D8C..9E supplies per-event(0,1,true) when its own penalty graph is empty. | All resources absent: watcher map misses and named DriveOffCharger event warns/no emotion mutation, while Mood.Update continues. Missing main file alone does not exclude surviving separate event files; they can create real effects. Per-event default graph is distinct from still-empty global default repetition graph. |
| N8 precise non-pilot fault boundary | GraphEvaluator2d.EvaluateY804BD0 loadsbegin;804BD6 dereferences it before size check. A fresh empty global repetition graph has nullbegin. Existing UpdateEventTimeAndCalculateRepetitionPenalty uses global+78 only on event-map miss; AddToEmotion uses that path. | Do not fabricate a global no-penalty graph or claim all Mood APIs safe from N6. A direct AddToEmotion for an unknown name after this null init would reach the empty-graph undefined read804BD6, covered by M1-034 at the smallest affector operation, preserving prior emotion state and visibly naming M7-020. DriveOff's TriggerEmotionEvent miss returns before that path, so it does not need that skip. Other public input paths are not silently certified by this narrow read. |

Host binding consequence: manager has supplied a filesystem DataPlatform provider even when ResourcesPath=null. For that input, initialize the actual mood owner with null main JSON/default source curves, perform the continued source registration, process whatever separate event documents are actually available, and keep periodic Update. A visible missing-resource diagnostic is required; treating it as null platform or silently substituting the fully configured shipped model is unsupported. Correct malformed/wrong-kind JSON handling remains its own M1-034/loader boundary, not equivalent to this defined null-root path.

V4 control-flow clarification:712FF6 computes vector field+4;712FF8 sets buffer; **712FFA BL71301A** serializes count then modes and returns; **then**712FFE loads raw mean and71300C writes it. Thus physical helper placement after the caller does not change execution order timestamp,count,modes,mean. Independent nonbool fixture: timestamp01020304, modes1and8, meanA7 => outer/body `43 00 04 03 02 01 02 01 00 00 00 08 00 00 00 A7`.

## Stack DOWN carried-ID clarification (narrow row16 check)

Re-ran known.py6192A8/619338 (BuildTallestStackForObject61907C),626010,62B9D8,61EB78 before the bounded primary reread; current M13-007 title/status/evidence are already quoted in this report/container-read. The source is asymmetric. UP checks carried rawID at6192A8..B4; seed admission separately checks61943A..448. DOWN calls neighborhelper at619358(onTop0), checks onlynull61935C/360, and appends immediately619362..378 before continuing61937C..386. It has **no carried-ID stop/reject**. Caller neighborfilter6191DA..278 has empty ID/predicate sets and allowed families{2,1}; helper62601C..2610E copies that filter, adds exclusion of **reference object's own ID only**6260AC..B8, adds root/quad/height predicate6260D4..DC, then first-result query62610C/10E. FindLocatedObjectHelper61EB78..EF28 performs origin/family/type/ID/predicate checks, not an implicit carried filter; its Robot-origin lookup is+294 at61EBFA, not CarryingOwner+284. Therefore a carried object that passes ordinary DOWN geometry may be appended. Do not add a symmetric carried exclusion. Existing row16's explicit UP-only wording is correct; the older shorthand 'carried exclusion' must not be generalized to DOWN. No geometry/cache/lifecycle expansion or code changes.

Current records newly relied on in the additional joins (literal manifest fields):

```json
{
  "id": "M6-006",
  "title": "Control path: queued PostEvent, ExecuteEvent, EnqueueOrExecute (frames + remainder; PlayAndContinue one frame early), drain order, Play/Stop/Seek execute, switch resolution",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PostEvent 0x009A6704 / core 0x9A0EF8; pump 0x9AE0B0; ExecuteEvent 0x9AA3DC",
    "EnqueueOrExecute 0x9AA0FC; drain 0x9A9F88; Perform 0x9AF8A8",
    "Play 0xA62D38 / 0xA62A1C; Stop 0xA663C8; Seek 0xA645C8",
    "Switch PlayInternal 0xA2C730"
  ],
  "unresolved": "compare the code against the inventory rows (the step after approval) The Play target's PlayInternal creation path (node vt+0x128, the PBI creator and the voice/source attach) is a separate record, M6-025 (correction C21)."
}
```

```json
{
  "id": "M6-023",
  "title": "The app audio-input dispatch: Unity PostAudioEvent -> AudioUnityInput -> AudioMuxInput -> AudioMultiplexer -> AudioEngineController::PostAudioEvent -> Wwise PostEvent",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Unity: unity/scripts/csharp/Anki.Cozmo.Audio/PlaySound.cs:29,72-82; GameAudioClient.cs:17-45; UnityAudioClient.cs:224-237,279-287; Anki.AudioEngine.Multiplexer/PostAudioEvent.cs:89-107; Anki.Cozmo.ExternalInterface/MessageGameToEngine.cs:14-16",
    "native: AudioUnityInput ctor 0x00591590; HandleGameEvents 0x005919B8; vtable 0x1023CA8 reloc 0x1023CBC -> AudioMuxInput::HandleMessage 0x008DFC4C; AudioMultiplexer::ProcessMessage 0x008DED14; AudioEngineController::PostAudioEvent 0x008D1F20 -> 0x008D8CE4 -> Wwise PostEvent 0x009A6704"
  ],
  "unresolved": "the dispatch is built (WwiseAppAudioInputTests) but it is not wired into the live app/engine path; the Wwise core PostEvent it reaches is M6-006. The computed PostEvent flags are not delivered to the core: WwiseEventRuntime.PostEvent has no flags seam (its third argument is targetPlayingId) and no callback/cookie seam either, so the callback id and the flags are recorded but not passed; delivering them is a later M6-006 wiring step. The callback context's first word is read as 0xff (0x008DED46/0x008DED4C, with 0 at +0x38 queued at 0x008DED48), so the app-path flags are 1|4|8 = 13; the null-context branch sets the flags to 0 (0x008D8CEC -> 0x008D8D30). A1's PlaySound.Play/Update and A2's GameAudioClient.PostAudioEvent are not modelled (the app side starts at A3). Operator decision 2026-10-17: game-posted audio is a supported C# API feature; the absence of Unity is not a reason to remove it. The public C# entry and the AudioUnityInput registration are M1-054's (startup connection); this record keeps command execution from AudioUnityInput through the multiplexer to AudioEngineController and the Wwise core (M6-006). Its existing gaps (live wiring; the flags/callback seam) stay explicit and are finished with M6, not with M1+M2.\nB-M1-5 (2026-10-17), built, awaiting strong verification: Posted from the registered host dispatch now visibly reports the absent robot audio-output owner once through SteppedBehavior.ReportMissing. Posted means event submission, not audible playback. M6 execution/flags/callback/output gaps are not settled by M1 startup wiring."
}
```

```json
{
  "id": "M15-061",
  "title": "ActivityFeeding lifecycle beyond the cube-connection subscription, SetCubeLights, and CubeAccel RemoveListener 0x006356AC",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261016-batch-check.md: OnSelectedInternal 0x005AADAC (rest), OnDeselected tail after 0x005ABC46 (UpdateBroadcastBehaviorStage(4,0), sEvent with need level), SetCubeLights, RemoveListener 0x006356AC.",
    "re-analysis/research/overnight/20261016-ov102-104-answer.md: layout ctor 0x005AA51C..0x005AAA1C; dtor 0x005AACBC..0x005AADA8; OnSelectedInternal 0x005AADAC..0x005AB282; SetupSevereAnims/ClearSevereAnims 0x005AB450..0x005AB514; SetActivityStage 0x005AB530..0x005AB6C4; TransitionToBestActivityStage 0x005AB7AC..0x005ABA8E; OnDeselectedInternal tail 0x005ABB5C..0x005ABD48; GetDesiredActiveBehaviorInternal 0x005ABDD8..0x005ABF30; Update/UpdateCubeToEat/UpdateCurrentStage/HasFaceToTurnTo 0x005ABFDE..0x005AC8EC; listener 0x005AC8F0..0x005AC9C6; SetIdleForCurrentStage 0x005ACB70..0x005ACC96; FeedingCubeController 0x005A6038..0x005A6EF4; SetCubeLights 0x005A65AC..0x005A66BE; RemoveListener 0x006356AC..0x006357B0; AddListener tail 0x006355BE..0x006355CA."
  ],
  "unresolved": "RECOVERABLE_GAP: read the listed bodies. Read 2026-10-16 (re-analysis/research/overnight/20261016-ov102-104-answer.md): IMPLEMENTATION_GAP for C1-C3, O1-O10, SV1-SV2, ST1-ST4, T1-T5, D1-D8, G1-G2, U1-U3, E1-E3, P1-P2, F1, L1-L2, I1-I2, K0-K12, R1-R2. Evidence correction: the deselect tail also sends meta.feeding_cubes_per_feeding. M1-034 point: L1 StartedEating with no controller inserts null and dereferences it at 0x005A610C; reachability UNKNOWN (M15-065); the stack makes it a visible skip. FeedingSFX output: M15-064. Interfaces owned elsewhere: CubeLightComponent (M4), ShakeListener and AddListener head (M9-017), BlockWorld lookups (M11), FaceWorld, NeedsState, reaction locks (M7-014). OV111 answer (re-analysis/research/overnight/20261016-ov111-112-answer.md): L1 is reachable only through R5 (a same-drain disconnect then a Falling Cancel(-1) during PlacingLiftOnCube); the visible M1-034 skip stays. FeedingEat's listener calls and their conditions are M15-065 F19, F23, F26."
}
```


## Additional pre-build joins: chooser defaults, activity Update, count initialization, audio sink

The chooser's continuation bonus graph at+28 is NOT the behavior's running multiplierF4. Existing checked20261007-M10-M15-rows.md CH2 (609F8C..A144,A2B8..A33E) installs chooser fallback **(0,0,true)** when scoreBonusForCurrentBehavior is absent/empty. GP3's **(0,1,true)** is the behaviorF4 multiplier. Earlier messages referring to 'running graph default1' apply only toF4, and must not overwrite the chooser default.

BehaviorsOnly Update: known5A7180/5A7372 before fresh primary. Actual vtable `_ZTV...ActivityBehaviorsOnly`10252D0,addresspoint+8,slot20 cell10252F8=raw5A7373; ctor5A7190..98 installs that table.5A7372 MOVS r0,0;5A7374 BXLR. Thus actual MeetCozmo/PutDownDispatch Update slot20 is a source no-op, not an inferred missing-delegate no-op.

Feeding is different: known5ABDD8/5ABFDE before fresh instructions. GetDesired5ABDE8 loads universalchooser+B8, calls slot28 at5ABE0C and nonnull result returns5ABE24→ABE96 **before** the specialized getter path. But the real manager calls Activity.Update before GetDesired. Feeding.Update5ABFDE..C062 checks severe1/flagA1 and calls SetupSevereAnims/ClearSevereAnims, iterates FeedingCubeController.Update at5AC024, SendCubeDasEvents atC04A, UpdateCubeToEat atC052, UpdateCurrentStage atC05A. Therefore universal DriveOff choice does not establish a no-effect preceding Feeding.Update. Reuse existing M15-061 adopted OV102–104 U1–U3 and named interfaces; a generic activity node with unboundUpdate cannot certify that route. This is an actual admission/lifecycle boundary, not justification for a new full Feeding body audit.

Smallest scoring-candidate irrelevance proof is conditional: platform34A=true AND each competing behavior not running. Shipped MeetCozmo assets bind RespondToRenameFace,EnrollFace,InteractWithFaces,FindFaces,PutDownBlock; Q17 RV59/RV58/RV10/RV09/RV15 all have slot24=0. PutDownDispatch's competing LookForFaceAndCube is RV51 with slot24=0. RB4 rejects these before their derived scoring; CS1 returns/skips score0 before random draw. Under that state, nonrunning DriveOff consumes one context RNG draw; already-running DriveOff consumes none. RB1's running bypass prevents generalizing this to an arbitrarily already-running competitor. The actual previous-activity Stop/lifetime join must establish that state before using the proof; highest flatScore1000 alone never proves draw count. Asset classes were read from behaviorSystem/behaviors/meetCozmo/{respondToRenameFace,enrollFace,meetCozmo_interactWithFaces,meetCozmo_findFaces_socialize}.json and freeplay/putDownDispatch/PutDownDispatch_LookForFaceAndCube.json, plus exact activity lists already cited.

OnConfigSeen initial values: known5DB0E8/5DB14C before primary reread.5DB0F8 points+128,5DB10A sets length18hex,5DB112/114 memclr covers128..13F: previous-eval138 and growthstamp13C are both+0f. Tree11C is initialized empty5DB0FC..106. ReadJson5DB1A2 sb=0,5DB1C8 emplaces each configured type,5DB1CC/CE writes its node+14 count0 (including duplicate keys). No invented -1 initialization. Existing RB3 binary64 negative threshold is BEE4F8B588E368F1. The manager+50 writer/binding for nondefault max-switch configurations is not newly recovered here; shipped drive-off's-1 bypass does not authorize using host `_startedSec` for those other configurations.

Audio sink correction/hold: W1's earlier statement that the requests already prove robot PCM/output1 is too strong. Current M13-039 is an interface inventory, not downstream audio execution. Fresh known58FBB4/58FF40 before source shows Robot+58 AudioEngineClient facade: PostEvent58FBB4 tests messagehandler+1C;58FD7C..92 builds/broadcasts PostAudioEvent(event,object,callbackID). PostGameState58FF40..62 similarly broadcasts a state command. Existing20261023-m13-038-audio-client-lifetime.md explicitly calls these interface requests, not downstream audible-output proof. Current production BehaviorAudioRecipient remains unbound (manager code finding), so client-request-only tests do not close that sink.

Existing checked bank census20261007-sound-reachability-assets.json identifies Music.bnk EC3490EC Play__Cue_World_Event__Cubes_Stacked→action641563732 and3BA32BC6 Stop__Cue_World_Event__Cubes_Stacked→action657957303. Both actions are1901, **object-scoped SetSwitch**, not direct Play. M6 inventory1.4/1.8 establishes object scope/factoryA659DC; cannot reroute object1 to robot object7. This proves neither no effect on supported downstream state nor direct robot PCM. Audio object1's actual subsequent consumer/routing, any shared-state/RNG effect, and the supported host-audio interface remain necessary boundaries before a phone-only/no-effect exclusion. No approved object1 exclusion was found. M6-023 explicitly records operator2026-10-17 game-posted audio as supported C# API, so absence of Unity is not itself an exclusion. M6-006 control execution remains IMPLEMENTATION_GAP, not a newly proven BLOCKED_EXTERNAL dependency. Minimal source-backed recipient shape is the same per-engine audio input/multiplexer/controller path with original event/object/state/parameter values and ordering; merely forwarding into an unverified facade is not full audio closure. No broad audio/DSP extraction, new sound policy or fake successful sink was performed here.


## Authorized narrow audio control read A — current owners before reliance

```json
{
  "id": "M6-001",
  "title": "Bank and HIRC readers in the runtime field order (Event, Action, Sound, RanSeq, Switch, ActorMixer, Bus, Layer, NodeBase, RTPC varint, STMG)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "HIRC dispatch 0x009B338C; Event 0x9CD01C; Action 0xA613B0 / factory 0xA60C1C; Sound 0xA1DA08; RanSeq 0xA0828C; Switch 0xA2F1D0; ActorMixer 0xA669EC; NodeBase 0x9F6EF8",
    "Bus 0x9C3FFC; Layer 0x9D24D4; RTPC entry 0x9F7254..0x9F72EC (varint param id); STMG 0x9B0B14",
    "conditional branches the shipped banks never exercise (3D positioning 0x9ECF44, bus A/B bits, the LayerCntr layer body): the rows are partial and the code fails closed (throws) rather than guessing"
  ],
  "unresolved": "compare the code against the inventory rows; the unread conditional branches (positioning, bus A/B bits, the LayerCntr layer body) stay gaps"
}
```

```json
{
  "id": "M9-004",
  "title": "Music switch containers, decision trees, meters and MIDI target",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/inventory/M6-wwise-bank.md",
    "Cozmo.bnk objects 914766641, 139286641, 602865028 and MIDI target 110896138"
  ],
  "unresolved": "Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. EffectiveMidiTarget starts at the segment and takes the nearest property 56; the runtime starts at the track and walks by the override bit, failing on 0 (0x00A3BDFC..0x00A3BEE4, 0x00A3CC48), and ignores the table-select flag."
}
```

```json
{
  "id": "M6-024",
  "title": "Bank and scene loading call sites: the CozmoAudioController six-bank list and InitScene, LoadAudioScene/LoadSoundbank, AddZipFiles",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "CozmoAudioController ctor 0x00592BB0; InitializeAudioEngine 0x008D1D1E; RegisterAudioScene 0x005935E2; LoadAudioScene 0x005935EA; banks Init.bnk/Music.bnk/UI.bnk/SFX.bnk/Cozmo.bnk/Dev_Debug.bnk and scene InitScene",
    "LoadAudioScene 0x008D2EE8 -> LoadSoundbank 0x008D2FE4; AddZipFiles 0x008D1E3E feeds the OBB archives"
  ],
  "unresolved": "the six-bank list, InitScene and the scene loader (WwiseSoundLibrary.LoadScene, and Load keeps the scene set when the six are present) are built (WwiseAudioSceneTests), but the loader is not wired into the live engine construction path. Load's 'apply the scene order only when all six banks are present' is a test-compatibility fallback, not a source rule: the native ctor builds the six-bank list unconditionally (0x00592BB0). InitializeAudioEngine and SetupPlugins are not this record's path; the LoadAudioScene/LoadSoundbank/AddZipFiles internals are not read (the call sites only). Bank parsing is M6-001."
}
```


```json
{
  "id": "M6-022",
  "title": "The live voice and bus engine: the 0xA57FF8 wrapper and 0xA44D4C render body, the voice pass, the bus pass, idle removal, the per-voice DSP chain, the four Perform group members, the PBI flush, the 0x108DAE8 output-device state and its three gate bytes, and the JNI audio-route poll",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "wrapper 0xA57FF8 -> 0xA57D64 + 0xA44D4C; render body 0xA44D4C pre-loop over the output-device list 0x108DB04 (0xA44D5C..0xA44DA0); throttle 0x108DA9C/0x108D870+0x4c (0xA44DAC..0xA44DD4)",
    "voice pass 0xA44948 (pre-pass 0x9D3CC0/0xA43D24/0xA39564; voice list base 0x108DF54, head +0x14, next +0xD0, state +0xDC); bus pass 0xA44C18; idle removal 0xA43F64",
    "voice mix dispatcher 0xA44630; params fill 0xA54F1C; source execute 0xA548C0; pitch 0xA53134; resampler execute 0xA52D4C; mix-in 0xA4FBEC; mixer 0xA45E9C",
    "callee bodies 0xA4C60C -> 0xA766B8 (0xA766F0 LPF / 0xA77480 HPF), 0xA56E00 -> 0xA56A7C, 0xA03E8C, 0xA05574, 0xA56650, 0xA4F9E0, 0x9E9E78, 0x9E9F08, 0xA55750, 0xA4AF50, 0x9D3CC0",
    "group members 0xA36AC4, 0x9FF308, 0x9D3C98, 0x9E6D2C; Perform call order 0x9AFA4C..0x9AFAA8; PBI flush 0xA38420",
    "output-device state struct 0x108DAE8 (gate1 0x108DAF0 at +8, 0x108DAFC at +0x14, list 0x108DB04 at +0x1C, gate2 0x108DB08 at +0x20, 0x108DB0C at +0x24, 0x108DB18 at +0x30); gate3 0x1052430",
    "gate writers: gate1 0x9EB090/0x9EBBA8/0x9EC00C/0x9EC1B4; gate2 0x9EAE18/0x9EC47C/0x9EC4A0; gate3 0x9EAE24/0x9EBBAC/0x9EC488/0x9EC4A8; module init 0x9EADE8; term 0x9EAF90; device advance 0x9EBA54; SetOutputDevice 0x9EC418 (call site 0x9AE3B0); frame count 0x9D4778 -> 0x9EC38C -> 0x9EBE6C",
    "JNI audio-route poll 0xA57D64..0xA5803C (GetEnv/AttachCurrentThread, FindClass, NewStringUTF, GetMethodID, CallObjectMethodV 0x593E58, CallBooleanMethodV 0xA58008, result 0x108DF98, notify 0x9EA66C)",
    "C12 voice callees (report 20260928-B-M6b-voice-callees.md): device node vt+0x2c=0x9E935C / vt+0x30=0x9E9420, node vtable 0x103B498 (0xA44D4C pre-loop); 0xA43D24 ducking/volume pre-pass (0xA43D24..0xA43EFC; 0xA55750 -> bus +0x88/+0x8C -> 0xA4AF50 -> 0xA437E0 -> 0xA4B4B0; container 0x108DF50, bus count 0x108DF54, voice count 0x108DF5C, voice head 0x108DF64); 0xA39564 node cleanup (0xA39564..0xA395FC); 0xA54F1C full state machine (0xA54F1C..0xA5574C) with 0xA4B4B0 and 0xA4BC58 (0xA4BC58..0xA4C000); 0xA44630 full order (0xA44630..0xA44938); 0xA548C0 and bus vt+0x58 = 0x9C07C4 (0x103ACE0); 0xA52D4C / 0xA5268C / 0xA4721C / 0xA47224 (0xA52D4C..0xA53134); 0xA55D04 / 0xA55A84 / 0xA54A30 and the state-0x11 tail (0xA44A94..0xA44BFC); 0xA01BD8 (0xA01BD8..0xA01CA0); 0x9E84C8 (0x9E84C8..0x9E859C)",
    "C12 bus-output/group callees (report 20260928-B-M6b-bus-group-callees.md): 0xA4FEF8 branch/mask and non-FX path 0xA50120 (0xA4FEF8..0xA50174, 0xA50EA4..0xA50F04); 0xA4F754 SetInsertFx and 0xA4E974 per-slot create/init/reset (0xA4F754..0xA4F9AB, 0xA4E974..0xA4ECD4) and 0xA4E7CC slot drop; 0xA38420 PBI flush and 0x9D3470 (0xA38420..0xA385D7), PBI vtable 0x103B768 (+0x10=0xA029DC, +4=0x009FF54C), ContinuousPBI 0x103D3B0 (+0x10=0xA6ACC0), queue 0xA38600/0xA01800; 0xA3587C (0xA3587C..0xA358B0); 0x9FDD90 (0x9FDD90..0x9FDE48); 0x9D3644/0x9D3864/0xA01800/0xA41854/0xA431A8/0xA54480 (0x9D3644..0x9D3830, 0x9D3864..0x9D3C58); 0x9E6D2C purge (0x9E6D2C..0x9E6E10, 0x9E2AE4..0x9E2BC8, 0x9E21FC..0x9E24AC, 0x9E2510..0x9E2550, 0x9D8A24..0x9D8F54)",
    "C12 bus-output tail (report 20260928-B-M6b-bus-metering.md): 0xA4FEF8 tail 0xA4FF40..0xA50FD0 is the bus level-analysis/metering stage (min/max peak 0xA50D48, RMS 0xA50C84, filtered peak 0xA501B0, 0xA52164 at 0xA500B0; flag byte at [out+0x18]+0x20; buffer fields +0/+4/+0xc/+0xe/+0x10/+0x14/+0x18); 0xA4D994 bus gain/param update (0xA4D994..0xA4DD43) with 0xA25FF8; 0x9C806C callback dispatch (0x9C806C..0x9C8104); 0xA52164 biquad+loudness (0xA52164..0xA5266B)",
    "C12 source classes (report 20260928-B-M6b-source-classes.md): factory 0xA562B8 dispatch on (mode, plugin>>16); shipped plugin ids 0x00040001 Vorbis / 0x00020001 ADPCM; Vorbis via registered-plugin 0x9CC3EC (vtables 0x103E0B8 streamed / 0x103E138 in-memory); real render slots 0xA73D34 (ADPCM t1) / 0xA72554 (ADPCM t3) / 0xAB0448 (Vorbis streamed) / 0xAB1550 (Vorbis in-memory); vt+0x4C = 0xA72B14 (bit6 of [[source+0xC]+0x1BE]); [source+0xC] is the PBI, not the bus; source-class field layouts",
    "C12 modulator evaluator (report 20260928-B-M6b-modulator-evaluator.md): 0x9E2BD0 (0x9E2BD0..0x9E52F3) per-voice LFO (five shapes 0..4) + transition evaluator and pool allocator; type-0 records 0x4C (list [arg+0x20]) and type-1 records 0x30 (list [arg+0x14]); 0x9E52F8 (0x9E52F8..0x9E5E8B) five-segment ramp"
  ],
  "unresolved": "not built and not wired into WwisePlayback/WwiseAudioSource/WwiseSongRenderer/AnimationScheduler (the wiring is B-M6b-3). C12/C15 supply the callee bodies. Remaining gaps: the bus metering DSP identity and per-lane mapping 0xA50044..0xA50FD0 (scalar flow, offsets, loop bounds, gains and store targets are EXACT_SOURCE; the filter identity is RECOVERABLE_GAP); 0xA25FF8 (called by 0xA4D994) RECOVERABLE_GAP; the 0xA4B93C sub-callees 0x9BE28C/0x9BDA88/0x9BD368/0x9BF8E4/0xA5E694 and 0x9EEDA4 RECOVERABLE_GAP; whether the shipped media exercises the 0xA022E8/0xA0228C/0xA01768 states UNKNOWN; the bus insert-FX effect object class (registry-assigned via 0x9CC2AC) and the [node+0x70] device sub-object identity UNKNOWN; the callback registry 0x0108D95C owner RECOVERABLE_GAP; the source-class, voice/bus insert-FX and four group members' Wwise class names UNKNOWN. HARDWARE_ONLY: the native device frame count 0x9EBE6C's sink value, the OpenSL pacing, and the runtime value of the gate bytes (the bus-pass arg (gate2==0)?1:gate3 and the frames-per-Perform branch); the code paths and writers are settled. The downstream node of the voice+0x1C0 wrapper ([voice+0x1C4]) and the meaning of the V7 r2/E4/E0 values are UNKNOWN; whether the shipped media exercises the 0xA552F0 paths is UNKNOWN; the writer of [r6+0x164] is RECOVERABLE_GAP. Also UNKNOWN/RECOVERABLE_GAP: the [device+0x70] sub-object identity and its vt+0x24 body and the [device+0x80] object identity (C8/C17, modelled as the Route7C seam); the semantic names of E4/E0/P2F and the four ramp fields; whether the shipped media exercises the 0xA5532C P2F==0 path; the modulator transition-ramp per-sample order (0x9E52F8 stage-B/C first-sample convention; the settled segment boundaries/slopes are EXACT_SOURCE); the writer of [r6+0x164] (sample scale). Also UNKNOWN: the Wwise meaning of the [sp+0x2e] flag, the P2F byte and the connection bit [conn+0x6C]&2; whether the shipped media exercises the [sp+0x2e]==0 copy-skip path. The Play -> PBI -> voice -> source creation bridge that creates the voices this engine renders is a separate record, M6-025 (correction C21). OV97 answer (re-analysis/research/overnight/20261016-ov97-99-answer.md): the bus-pass argument (gate2 == 0) ? 1 : gate3 is constant 1 (0x108DB08 stays 0: its only setter path, message 0x35, is unreachable); gate1 (0x108DAF0) stays dynamic."
}
```

This is the manager-authorized next narrow audio unit, source extraction only, no new audio policy. Actual M13-039/M6-006/M6-023 quotes precede above; these additional records remain IMPLEMENTATION_GAP. Existing keep-row source claims below are reuse of their primary companions, not status changes or acceptance of M6/M9. Newly examined native anchors were preflighted with known.py:58FA7C/58FAF0,5905E4,5908F4,590BEC,594410,8D243C,8D9108,A659DC/A65940,A61260,A32ED0,99D3C8,9994F0,8D74BC/8D7510/8D7570. Thumb/ARM scratch:audio-control.txt/audio-switch-recipients.txt. No DSP/voice census was performed.

| Row | Source/input witness | Behavior and exact boundary |
|---|---|---|
| A1 actual handler attachment | Existing checked keep-rows SH1–SH8 and20261007-sound-handler-subscription-native.txt:4ED23A..278 creates handler+AudioEngineInput, optionally registers it with context mux, THEN SetMessageHandler(robot58,input18). Fresh58FAF0..FB08 confirms handler publication toclient1C. Input5905E4..5907F0 subscribes native tags0..5 ascending; handler590BEC copies payload then signal. | Native engine-origin messages are synchronous handler inputs, distinct from app/game envelope tags1..6 and their queued host Post wrapper. Null mux suppresses registration, not client-handler binding. Handler owned by input; client borrows handler and retains subscription handles. Do not route these through a host-next-tick app submission and change order. |
| A2 dispatcher and ready gates | Keep rows UC30–35,UE2–7:590944..99E selects typed payload+input virtual; switch8DFE38→8DEF20→controller.SetSwitchState8D243C. Fresh8D2446/52 enabledbyte4 gate,8D245C helper8D9108; helperbyte0 gate then native99FE1C. Event58FD7C..92→8DED14→8D1F20→8D8CE4→9A6704. | Missing handler/mux/disabled controller/helper are distinct defined failures, not silent successful state writes. Event callbackempty => callbackID0/nullcontext/nativeflags0; no fabricated callback/API event. Successful native switch queues tag8,size14hex,object4/groupC/value10. It does not immediately call a renderer or return audible success. |
| A3 concrete source assets and delay | Full Music.bnk at re-analysis/obb/sound_meta/Music.bnk,1071008bytes,SHA256424eace23a070f216ea81572fbfbf2f35cf0248d26287e26a9b72c3d39b4a77c matches checked census. EventEC3490EC action641563732 payloadoffset1069745,len21:`547c3d2601190000000000000005f5798353dd7c81`; event3BA32BC6 action657957303 offset1066518,len21:`b7a1372701190000000000000005f579836ab6f827`. AF406C native1901 readerA658BC..E4 reads trailing rawgroup/value words. | Both type1901,target0,isBus0,propertycount0,range-count0. Both group8379F505; truevalue817CDD53,falsevalue27F8B66A. No authored delay/range. Native GetDelayA61260..A6139C uses default108DB20+3C and no RNG with absent range; personally reread A6131C/20 zero-span bypass too. Existing PDI2 initializer4DE020..13C explicitly initializes3C=0; thus source-initialized default delay0. This is initial-default evidence, not an exhaustive no-later-writer claim. The pair cannot be turned into direct Play/Stop or object7 posts. |
| A4 execution and actual stored state | Existing AH413E,AF406A–E,QC2/3/6,SX1–4 companions. Fresh ARM A65940..A659A0: capture action24/group,28/value,request34/object; enter nestingA32DE8; get/creategroupA30D98; nonnullA32ED0(group,value,object); leaveA32E08;return1. Direct tag8 consumer9AEFC4..9AF038 finds/retains registered object, performs same nesting/store/notify, then releases it. | Event and direct switch enter the SAME per-object switch-state owner. A32ED0 stores incoming value in group-scoped object record before notifying observers. Same-value writes are not suppressed by setter; observer may suppress. Missing object consumes command without switch mutation. Preserve FIFO across A/B event requests and direct activity switch posts; preserve playing-ID allocation even for events containing only SetSwitch. |
| A5 actual future consumer and active notification | Existing source SO1/CS4/SO4/SC1 in20261007-singing-decision-rows.md: A2BD58→A347A8 scoped switch query supplies selected state, default-track fallback only on query0. Changed observer state99D3C8 storesnew18/old1C, rebuilds99D088, then calls9994F0(parent,observer). Fresh ARM99D3C8..F4 and9994F0..9550 confirms schedule48/null andactive54 gates before real position/mode/schedule processing. | This state has a real selection/scheduling consumer. It is not a label or unused diagnostic dictionary. When active observer/schedule exists, a no-op notification or cache-only substitute loses behavior; downstream schedule/voice execution remains M9-004/M6 obligations. No phone-only/no-effect exclusion is proved. Source available, not BLOCKED_EXTERNAL. |
| A6 fresh audio context versus live music | Fresh MusicConductor8D74BC clears playingbyte4,pendingword18,deadline10,etc;8D751E/20 skips UpdateMusicState when pending0. Checked SL1/SJ487E InitScene event vector is empty; its six bank names are Init,Music,UI,SFX,Cozmo,Dev_Debug. Controller object-registration594432..BA registers IDs1..10 after scene load, even if load resultfalse (SL2). | In a fresh context with no app/music-state/play input, stack SetSwitch events and BehaviorsOnly direct activity switches alone do NOT start a music voice. An existing active music input is a different supported state, not excluded. Future nonzero MusicState goes through UpdateMusicState8D75AA..B4 starting Play__Music__Play on configuredobject1, then8D7630..36 SetState. That is a real subsequent consumer boundary, not evidence that such input already occurred in the fresh pilot. |
| A7 engine phase | Existing UC20:4ED6BE..CE→8DF3DA→controller8D2928; conductor.UpdateTick first, then helper.RenderAudio(1), then callback flush. Wwise queue drain/control executes under that render lifecycle. | Delivering a command into a facade is not completion of queued switch state. A standalone implementation may use its own containers/threads, but must preserve actual ordering/readiness against this owner; do not simply move control processing to an invented arbitrary tick or silently omit state when rendering is absent. |

Current C# capability, personally read: EngineAppAudio.cs only constructs app subscriptions when Options.AudioInputDispatch is supplied; no default audio controller is supplied. It queues app-envelope submission through host Post, and reports submission as distinct from audible output. WwiseAudioInputDispatch.HandleGameEvents only handles event tag1;2..6 return SubscribedNotHandled. WwiseAction.TryRead supports04/01/1E only, refusing1901; WwiseEventRuntime.ExecuteAction1901 reaches NotParsed, and PumpMessages has no queued switch-command case/scoped switch owner. It does have event FIFO, playing-ID accounting, registered-object lookup and pending-action machinery. Its GetDelay currently substitutes absent default0 and would consume RNG even for an explicitly zero-span range: those generic differences cannot be silently certified; the authored pair here has no range. WwiseBankLoader states it has no production constructor/caller; M6-024 scene/loader is not bound to live engine initialization. WwiseAudioSource.SetSwitch only accepts singing object7/6 and writes a global dictionary; WwiseSoundLibrary.ResolveMusic accepts an explicitly supplied global dictionary. Neither is the actual per-object1 production switch-state/query/observer owner required here. Reusing singing's object7 path would change source scope.

Smallest source-settled control inventory is: actual per-engine handler/input ownership and gates; source bank/registration readiness; native event and direct-switch FIFO;1901 reader+executor; same registered-object scoped state manager; actual selected-state query and eligible observer delivery; source engine audio-tail processing. Those controls have source evidence above and require independent B checking before implementation. A private facade-only adapter or an unused dictionary is not the unit.

**STOP boundary for full producer-to-sink completion:** the present production stack lacks the source bank/controller readiness owner, scoped switch state/query/observer owner and audio-tail drain binding. Closing those together is already wider than 'set BehaviorAudioRecipient to an existing method'. If an active music schedule is admitted, actual notification reaches9994F0 and the retained M9-004 scheduling/voice path; a control-only build cannot call that supported path complete. Source startup through M6-024/M6-001 and active render/control consumer M6-006/M6-022/M9-004 must be scoped explicitly before a complete audio claim. This is a concrete implementation/source-join boundary, not unavailable binary evidence, an external blocker, an exclusion, or permission to suppress audio. Fresh no-music-input control state may be a bounded build slice, but is not full general audio acceptance and must not erase those reachable-owner diagnostics. No broader DSP/Play recovery was started.

IActivity getter clarification (builder's separate narrow question): fresh known5B387C then full primary reread.2C/30 is the **cached interlude result**, not the normal chooser winner. Entry5B388C..92 returns it only when nonnull and equal to caller's current behavior. Otherwise clear/release at3894..9E and call concrete chooser slot30 at38C2. Only nonnull interlude28 AND chosen!=caller current38CC..D8 calls interlude slot28 at38F6 and stores its result2C at3904. No-interlude MeetCozmo/PutDownDispatch therefore call chooser every tick, even while DriveOff is running. The charging-gate competitor proof above ceases to apply once platform becomesfalse; it does not settle all final-tick derived eligibility/RNG merely because DriveOff's score1000 wins.


## C6 — Platform-false chooser while DriveOff remains running (bounded gate inventory)

Actual model/effort telemetry remains unobservable. This addendum is source extraction, not implementation/acceptance. known.py preflight covered six class names, SelectFaceToTrack, GetBestFaceToTrack, GetFaceIDsObservedSince, 005FF744 and 005C1EE8 before these reads. Primary: same checked ARM ELF; scratch disassembler .scratch/cm13-03/read.py. Earlier Q17 ES1–4/CS1–4/GP/IL4 and Q16 remain the checked common contracts, not replacements for the derived gates below.

Current relevant records (literal current title/status/evidence/unresolved; no status changes):

```json
{
  "id": "M8-002",
  "title": "The repetition penalty graph and its suppression window",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "config/engine/mood_config.json:88-99 ships defaultRepetitionPenalty with the nodes (0.0, 0.0) and (30.0, 1.0).",
    "IBehavior::ReadFromScoredJson 0x005bc488 reads repetitionPenalty into the GraphEvaluator2d at +0xe8; a missing or unreadable graph warns (IScoredBehavior.BadRepetitionPenalty) and leaves it empty, and an empty graph gets the single node AddNode(0, 1, true) at 0x005bc566 (0x005bc678).",
    "IBehavior::EvaluateRepetitionPenalty 0x005beee6 returns 1.0 when the last-run stamp +0x30 <= 0 (vldr s0,[r4,#0x30] 0x005beeea; movle.w r0,#0x3f800000 0x005beef8), otherwise evaluates the graph at +0xe8 at now - +0x30 (add.w r0,r4,#0xe8 0x005bef0e; b.w 0x8cbf9c 0x005bef1e).",
    "The +0x30 stamp is written by IBehavior::Stop 0x005bd11a/0x005bd11e as BaseStationTimer::GetCurrentTimeInSeconds().",
    "The +0x108 suppression threshold: StopWithoutImmediateRepetitionPenalty 0x005beea0 sets +0x108 = now + 1.0 (vmov.f32 s0,#1.0 0x005beeb0; vadd.f32 0x005beeb8; vstr s0,[r4,#0x108] 0x005beebc); EvaluateScore applies the penalty only when now >= +0x108 (vldr s0,[r4,#0x108] 0x005befe2; vcmpe.f32 0x005befea; bpl 0x005beff2).",
    "StopWithoutImmediateRepetitionPenalty's only three callers are BehaviorPickUpCube::UpdateInternal 0x005c685c, BehaviorStackBlocks::UpdateInternal 0x005c991c and BehaviorBuildPyramidBase::UpdateInternal 0x005dd110 (all M7/M15); IBehavior::Stop does not call it."
  ],
  "unresolved": "Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. an unreadable repetitionPenalty warns IScoredBehavior.BadRepetitionPenalty \"Behavior '%s': %s failed to read\" (0x5BC524..0x5BC52E); Graph2d.FromJson returns null silently (Activities.cs:162). StopWithoutImmediateRepetitionPenalty uses an unsourced BehaviorStopReason.Interrupted (SteppedBehavior.cs:341). The arithmetic (0x5BEEE6, 0x5BEEA0) holds. Before: built, awaiting strong verification: flat (0, 1.0) default graph, stamp <= 0 or NaN gives 1.0, float stamp/subtraction/compare (0x5bef12, 0x5befea), +0x108 = now + 1.0f, StopWithoutImmediateRepetitionPenalty calls Stop first. Still open: no stack behaviour calls StopWithoutImmediateRepetitionPenalty (callers are M7/M15); the internal Stop reason is not in the inventory."
}
```

```json
{
  "id": "M8-003",
  "title": "Scored selection: emotion scorers or the flat score, times the repetition and running penalties",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::ReadFromScoredJson 0x005bc488 reads five keys, not four: \"emotionScorers\" into a MoodScorer at +0xdc, \"flatScore\" into the float at +0x100 (0x005bc4ca/0x005bc4e0), \"repetitionPenalty\" into the graph at +0xe8 (0x005bc4ee), \"considerThisHasRunForBehaviorObjective\" into a behaviour objective at +0x10c through BehaviorObjectiveFromString (call 0x005bc5a0), and \"runningPenalty\" into the graph at +0xf4 (string 0x005bc602; add.w r6,r4,#0xf4 0x005bc5f8; AddNode(0,1,true) 0x005bc678).",
    "IBehavior::EvaluateScoreInternal 0x005beec2: if the MoodScorer vector at +0xdc/+0xe0 is non-empty it tail-calls MoodScorer::EvaluateEmotionScore (0x8cbf8c, with robot+0x440); otherwise it returns the flat score at +0x100 (vldr s0,[r0,#0x24] after the pre-index 0x005beed4). The two are alternatives, not addends.",
    "MoodScorer::EvaluateEmotionScore 0x0067c9b8 walks the scorers, evaluates each one's graph at its emotion's value or at the value minus the value sixty ticks ago when trackDelta is set (Emotion::GetHistoryValueTicksAgo(60) 0x0067c9f4), and returns the mean (0x0067ca6e..0x0067ca72); a result within 1e-05 of zero ends the walk and the score is zero (0x0067ca50).",
    "IBehavior::EvaluateScore 0x005bef60: while running it uses EvaluateScoreInternal plus the float at +0x104 (vldr s2,[r4,#0x104] 0x005bef80; vadd.f32 s16,s0,s2 0x005bef88) and multiplies by EvaluateRunningPenalty when +0x111 is set (ldrb.w r0,[r4,#0x111] 0x005bef84; cbz 0x005bef8c; blx EvaluateRunningPenalty 0x005bef90; vmul.f32 0x005bef98). Otherwise it scores only a behaviour that IsRunnableBase and passes the vtable+0x50 test, applies the repetition penalty only when +0x110 is set and now >= +0x108 (ldrb.w r0,[r4,#0x110] 0x005befd4; cbz 0x005befd8).",
    "IBehavior::EvaluateRunningPenalty 0x005bef22: returns 1.0 when +0x34 <= 0, otherwise evaluates the graph at +0xf4 at now - +0x34 (vldr s0,[r4,#0x34] 0x005bef26; add.w r0,r4,#0xf4 0x005bef4a; b.w 0x8cbf9c 0x005bef5a).",
    "The four constructor fields: +0x104 is the running-score bonus (IncreaseScoreWhileActing 0x005bf02c adds to it only while the current-action handle +0x84 is non-zero; ScoredActingStateChanged 0x005bf046 clears it); +0x108 is the repetition-penalty suppression threshold; +0x110 enables the repetition penalty; +0x111 enables the running penalty (both set to 1 by the halfword 0x0101 at 0x005bbd34, with no other writer).",
    "EmotionScorer::ReadFromJson 0x0067aabc names emotionType, scoreGraph and trackDelta. No shipped behaviour or activity config carries an emotionScorers block, so every scored behaviour in the app is scored by its flatScore alone.",
    "IncreaseScoreWhileActing 0x005bf02c is called only from concrete behaviour transitions (ten call sites, M7/M15); ScoredActingStateChanged 0x005bf044 has no engine caller, so +0x104 is never cleared in-engine."
  ],
  "unresolved": "Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. EvaluateScore calls virtual slots +0x84/+0x88 (0x5BEF72, 0x5BEFC6), which five classes override (BehaviorPounceOnMotion::EvaluateScoreInternal 0x5F83BE, used by socialize.json); ScoredBehaviorEntry.Evaluate hardcodes the base (Activities.cs:84-120). The runningPenalty bad-read warning (0x5BC638..0x5BC640) is missing (Activities.cs:163). The text saying trackDelta throws is stale. Before: built, awaiting strong verification: scored selection in float with the engine's order (EmotionScore 0x67c9b8, flat score, running bonus, repetition and running penalties). Still open: trackDelta GetHistoryValueTicksAgo(60) needs the Emotion history ring (M7-013) and throws NotSupportedException; MoodState holds emotions as double where the engine loads a float. OV20/21 check (re-analysis/research/overnight/20261014-ov20-21-check.md): contradicted: ScoredActingStateChanged (slot +0x80, 0x005BF045 in every behaviour vtable) is called from StopActing 0x005BD35A, StartActing 0x005BDB54 and HandleActionComplete 0x005BE200; its body stores 0 to +0x104 and ignores the bool.\nB-M8-3 (2026-10-15): checked +0x104 zero-on-Stop/StartActing/completion behavior already implemented; existing BehaviorFrameworkTests.ScoredActingStateChangedClearsTheBonusOnActionStartStopAndCompletion confirms all three paths. No scoring arithmetic/status changed."
}
```

```json
{
  "id": "M14-009",
  "title": "Face return eligibility and enrollment forwarding",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x004F55B8..0x004F55E2 rejects a stale entry, optionally rejects id<1, and requires Robot::IsPoseInWorldOrigin on the face pose.",
    "0x004F5C9E..0x004F5CB2 selects mode 4 for a nonzero id and -1 for zero; the tail-call 0x8CAC3C resolves through VisionComponent 0x00657A58 / VisionSystem 0x006B3394 / FaceTracker 0x0086B276 / Impl 0x0086E372 to FaceRecognizer::SetAllowedEnrollments 0x008658AC, which stores the mode at +0x108/+0x10c, the id at +0x100 and the entry data at +0x104 (C1-F11)."
  ],
  "unresolved": "ShouldReturnFace (F10) and Enroll's mode selection (4 for a nonzero id, -1 for zero) are built. The enrollment target forward is VisionSystem.SetFaceEnrollmentMode -> FaceRecognizer.SetAllowedEnrollments (M14-011, built), which stores the mode/id/track. The M14-011 recognizer is the album owner."
}
```

M14-009 does **not** by its quoted scope establish the complete InteractWithFaces selector or own all six concrete behavior gates. known.py's incidental M13-024 census hits do not confer ownership. No new fidelity ID is allocated here; manager must identify/name any otherwise unowned gate/selector production unit before claiming settlement.

| Case | Primary instructions / input producer | Observable consequence and exact integration boundary |
|---|---|---|
| Repeated chooser admission | 005B387C..39E8, specifically cached interlude pair+2C/+30 at3886..389E and interlude+28 condition38CC..D8 | Cache is interlude result, not ordinary winner. No-interlude MeetCozmo/PutDown invokes chooser every manager tick; running DriveOff does not suppress these gates after platform false. |
| FindFaces | 005C1A34 movs r0,1 / bx lr | Derived eligibility always true. Asset maxFaceAgeToLook_ms=10000 is NOT a runnable gate. Common RB gates and score/repetition still apply. |
| LookForFaceAndCube | 005EFD54 movs r0,1 / bx lr | Derived eligibility always true. No face/cube availability input for this gate. |
| PutDownBlock | 005C7FB0..7FCE reads Robot+284 -> raw carrying ID+8; add1; if nonzero true; otherwise tests action handle this+84 | True for raw carry ID !=FFFFFFFF OR retained action handle nonnull. Do not substitute nullable-ID presence or freeze carry state at DriveOff entry. Stored action handle comes from real common StartActing/StopActing lifetime, not a derived synthetic running flag. |
| RespondToRenameFace | 0060082E..0848 tests inline std::string this+11C empty/nonempty | Constructor00600694..0076A initializes empty string (006006AC..6C0). HandleWhileNotRunning0060080C..082C unwraps UpdateEnrolledFaceByID then copies payload name+10 into+11C (081A..822), ID word0 into+128 (0826/28). Thus no request => false for fresh owner; real name input cannot be replaced with face visibility. |
| EnrollFace | 005FCD00..CD16 tests std::string at pointed settings this+17C | Constructor005FCB08..26 allocates/zeroes0x18-byte settings and stores pointer17C, so fresh request name empty. HandleWhileNotRunning005FF744: tag87 SetFaceToEnroll atF750..75A; empty incoming name rejectedF760..76E/F858; nonempty string copied to settings atF7C8..7D0, settings scalars copiedF7D4..7E2; tag88 branchF7E6..86E only logs here, does not erase this name in the checked handler. Do not conflate pointer existence with nonempty request. Full enrollment action is not extracted. |
| InteractWithFaces gate mutation | 005C23AC..23C6 clears target11C, calls SelectFaceToTrack23B8, then returns target!=0 | Gate has state effect even when its repetition score later becomes zero. Current C# FaceBehaviors.cs SelectFaceToTrack uses recent10sec/most-recent shortcut and IsRunnable does not preserve this target mutation; this is not the source selector. |
| Interact selector inputs | 005C23C8..23FE passes this+120 to Robot+38 FaceWorld.GetFaceIDsObservedSince, namedOnly=false; gets Robot+264 AI+18 Whiteboard; GetBestFaceToTrack(set,true) at23EA; stores returned target11C23EE | Constructor005C1EF8 clears target11C and timestamp120. StopInternal005C240E..241C writes Robot.GetLastImageTimeStamp to120 at2414/18. Timestamp is per-behavior last-stop image stamp, not now-minus-window. Live FaceWorld/current Robot pose/head-angle/name state needed. |
| Face candidate set | 004F59EE..5A70: face last observed timestamp+1C >= supplied threshold unsigned (5A1E..22); optional positive ID only when namedOnly==1 (5A24..2E); Robot.IsPoseInWorldOrigin(face pose+108) must return1 (5A30..3E); signed-ID ordered unique tree insertion (5A40..4A) | At this call namedOnly=false. Zero candidates => target0; one candidate bypasses geometric ranking in Whiteboard. FaceWorld producer/current origin readiness must be actual source data, not an empty stand-in. |
| Best face ranking, exact dependency boundary | 0056C110..C280: empty set returns0(C124..26/C272), singleton returns stored ID(C12A..34); multi walks ordered IDs(C14A..), GetFace(C166..174), face pose+F4 wrt Robot.GetPose(C1AC..C1C0), absolute head/body angle helpers(C1C4..C1E0), subtract Robot+2FC head angle and absolute(C1E4..C206), cost = abs(headDifference)+3*abs(bodyAngle)+unnamed surcharge(C20A..22E); strict MI lower cost updates winner(C232..244), invalid/missing face/failed transform skipped | No RNG in this selector body. Multi-face numeric helper semantics are not newly certified by these call sites alone: use previously checked pose/angle helpers if actually supported; otherwise precise remaining M14 selector/transform boundary. Body is370bytes, not a reason to recover unrelated face actions. |
| Score virtual dispatch (all six) | ELF vtable symbol headers0102B458/0102B19C/01026F80/01026E40/01027674/0102A474; actual address point=header+8. Header+58 resolves six gates above; header+8C and+90 resolve base005BEEDE and005BEEC2 respectively (dynamic relocations, Thumb bit stripped) | All six use base running and ordinary score functions, not one of ES5–9 overrides. Do not interpret header+84/+88 as the virtual offsets: those precede the address point. |
| Prior-run state | Q17 IL4 005BD0F4..D1CC clears A1 before derived StopInternal and writes binary32 stop time30; MS2 005A2028..209C stops existing current only if nonnull/A1, then nulls/release | Fresh ctor state is insufficient after reuse: preserve per-instance stop stamp30, run stamp34, repetition suppression108/graphs, action handle84, request/name state and Interact120. Running candidates use ES1 and do not call the nonrunning gate. Manager current/lifetime must make any assertion 'the other six are nonrunning' true; do not reset them opportunistically. |

Shipped scoring inputs: activities_config.json MeetCozmo has DriveOff1000, Respond3, Enroll2, Interact1 with repetition nodes(8,0),(8,1), FindFaces0.5, PutDown0.1; activities/putDownDispatch.json has DriveOff1000 and LookForFaceAndCube1 with nodes(.2,0),(.2,1). No emotionScorers in these entries. Actual stored tree order, common RB/GP gates and existing evaluated graph semantics remain required. There is no source justification for skipping low scores merely because DriveOff1000 wins.

Exact RNG effect: after ES eligibility/repetition, CS1 0060A45E..466 skips score<=0; a nonrunning positive candidate reaches CS3 0060A49E..4BC and consumes one RandDbl call from Robot.GetRNG, even if never selected. The draw limit is binary64 bits3FB99999A0000000, add to widened f32 score in f64 then narrow once. Running DriveOff uses continuation graph/+0.1 CS2 and no nonrunning draw. Thus on a fresh no-name/no-carry/no-faces state, once platform=false and common gates permit, MeetCozmo FindFaces can consume one additional draw each chooser tick; PutDown Look can consume one (unless its real repetition history makes its evaluated score<=0). With faces/names/carry, further actual gates can admit candidates. Gate and score state must be evaluated before declaring exact number of draws; winner alone is insufficient. Shared RNG advancement affects later animation/group choices (output/state1–4).

Bounded result: constant gates, string emptiness, raw carry/handle gate and base score dispatch are source-settled small joins; no competing action body is necessary merely to compute this tick's losing candidates. Exact general MeetCozmo chooser closure still requires real request-state owners and the Interact target-selection/FaceWorld/pose-angle input chain above, plus preserved prior-run lifecycle. Current most-recent-face stand-in is not certified. If those underlying M14 inputs/helpers are not already faithfully bound, stop at that named production boundary rather than hardcode gate=false, assume no faces, choose a different timestamp, or silently omit the candidate. This addendum does not authorize selecting/starting a competing action after DriveOff stops; that is outside this bounded running-DriveOff question.


## IQ-A — independent narrow quality-handler check (producer verdict pending)

Actual model/effort telemetry unavailable. known.py before anchors00655578,006560D8,00650130,007277B0,0071A0C2,006B55E6. Personally compared full00655578..006556EA,006560C8..006561CE,006500A8..00650188,007277B0..CE,0071A0C2..E4,006B55E4..006B5690 against B findings. Current records before reliance:

```json
{
  "id": "M11-035",
  "title": "The VisionComponent per-mode result handlers and the frame broadcast",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionComponent::UpdateAllResults 0x006542EC runs markers 0x006544A2, faces 0x00654510, pets 0x0065457E, motion 0x006545E8, overhead edges 0x00654652, tool code 0x006546BC, computed calibration 0x00654726, image quality 0x00654790, laser points 0x006547FA",
    "then CheckMailbox 0x00654A74 and the RobotProcessedImage broadcast 0x00654A14/0x00654A1C"
  ],
  "unresolved": "build the M11-owned dispatch order and the VisionProcessingResult field mapping. Still unread and M11-owned: the loop head 0x006542EC..0x00654486 and the bodies of UpdateToolCode 0x00655421, UpdateComputedCalibration 0x006554C5, UpdateImageQuality 0x00655579, UpdateLaserPoints 0x00655795 and UpdateOverheadMap 0x00655909 (M11-048 lists them); the face/pet handlers are M14, motion M10, and the message layouts are M2/M10. M1-053 reads the owned +0xE8 last processed image timestamp (getter 0x006527A2); its result timestamp commit is a candidate in VisionSystem, and its exactness remains this record.\nB-M11-6 (2026-10-15), partial integration, awaiting strong verification: detection work is staged before existing world/face/pet recipients so a known mode7 quality failure drops all frame recipients/publication, rather than mutating them then dropping the final event. Host frame can expose a bounded ImageQualityProposal; this is explicitly NOT native processed-mask/header/deque serialization, UpdateImageQuality debounce/EngineErrorCode/event/broadcast, or RobotProcessedImage layout. Those native recipients remain visible MISSING and unbuilt; no whole dispatch or wire lifecycle claim.\nManager correction (2026-10-18; research/20261018-b-m1-7-check-delivery.md finding 2): this record owns the RobotProcessedImage game report (producer 0x00654A14, Robot::Broadcast 0x00654A1C). The B-M1-6/B-M1-7 census row for VisionSystem.FrameProcessed named M13-020 and called 0x00654A14/0x00654A1C a consumer tail; both wrong (M13-020 only subscribes to tag 0x43). FrameProcessed is a local observation; the host report is MISSING here.\nNot supported: VisionComponent::FakeImageProcessed 0x00657C24 (its +0xE8 store at 0x00657C2E), decided by the manager 2026-10-25 (research/20261025-b-m11-19-20-row-71-claude-check.md): no caller in the shipped engine (no branch, relocation or pointer; row 71, reproduced) or in the shipped app/libraries, and the stack provides none. Not a no-effect claim; reversible. The stack's +0xE8 writers are the reset (0), the result tail after the handlers (B-M11-21, 04417900) and the SetNextImage rollback, which is a visible MISSING until the +0xE4 owner exists."
}
```

```json
{
  "id": "M11-080",
  "title": "CheckImageQuality: ROI weighting and MeterFromDetections, the proposal stage with NaN/boundary rules, the quality 1/2/3 decisions, the ImageQuality statics from vision_config, and Update's mode-7 block",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov14-m11-057-check.md: ROI head 0x006B2D4C..0x006B2F58; proposal 0x006B2F5A..0x006B30AC; statics 0x01051088..0x01051094 loaded at 0x006B07D8..0x006B0E3E (SetAutoExposureParams call 0x006B0A14); Update mode-7 block 0x006B55E2..0x006B5690."
  ],
  "unresolved": "IMPLEMENTATION_GAP: port per the check file. ComputeExposureAdjustment, the histogram and ComputePercentile are the companion RECOVERABLE_GAP record.\nB-M11-6 (2026-10-15), bounded build, awaiting strong verification; BLOCKED: ImageQualityProcessor ports signed-int32 ROI area/double counting, f32 weights and complement mask for in-bounds rectangles, exact proposal threshold/NaN/order/min-max/roundf/vcvt rules, Good/TooBright/TooDark percentile decisions, six compiled/shipped ImageQuality statics and missing/null-key Error/errG handling. Existing live VisionSystem producer work is staged before result recipients: a known nonzero quality Result emits the native Warning then Update Error/errG and publishes none of that frame; missing inputs stop a live frame through stack MISSING without fabricated native failure. The decoded-pixel offline ProcessImage helper retains explicitly labelled bypass for unknown quality inputs. BLOCKER: OV14 explicitly leaves unchecked WHICH detector rectangles are appended; input seam is unknown by default, not an empty guess. Mutable ImageBase.GetROI 0x006BA380..0x006BA45E edge/clipping recipient is also not checked here; out-of-bounds input stops as MISSING rather than invented clipping. M11-081 is now checked but excluded/pending B-M11-11, not unread: no approximate factor/percentile. Profiler Tic/Toc, full Init gates/config ownership, native result header processed-bit/deque serialization and M11-035 image-quality recipient remain visible MISSING. Host ImageQualityProposal is not that native result/debounce/game report. See jobs/status/B-M11-6.md for files/lines/ranges and call accounting."
}
```

**Independent PASS for these exact rows only:** physical Robot+14 and raw quality+8 nonzero gate all handler effects; auto byte329 controls SetCameraSettings before debounce and its result is ignored (65558C..55A6). Changed quality or zero anchor resets anchor, F5=0, first threshold; unchanged quality with nonzero anchor tests F5!=1 and unsigned(timestamp-anchor)>threshold. Outgoing code q1=6,q2=4,q3=5,otherwise7 (6555A8..55F6/561A). Broadcast occurs before F5=old rawF4, new anchor, repeated threshold and currentF4 store (6556B6..56E8→55F6..FC). RawF5 must not become normalized bool: q2/q3 do not meet suppression F5==1. Every normal return0 (655600..5612), not an aggregate failure substitute.

Ctor: r5=0 at6500B4/650110; STRD650130 setsF8/FC=0; STRH1 at650142 setsF4=1/F5=0; STRH100 at65017C sets328=0/329=1. Compiled first threshold105104C=3000, repeated1051050=5000. Exact PIC witnesses: literal65578C=009FBA60 + ADD-PC6555EC =105104C; literal655788=009FB96E + ADD-PC6556E2 =1051050. The second ADD uses unaligned PC6556E2; aligning it gives the wrong variable. Existing checked OV16C1 config reads TimeBeforeErrorMessage_ms then literal-spelled RepeatedErrorMessageInverval_ms and can partially update process globals; shipped vision_config.json supplies3000/300000. Do not replace missing/failed init with shipped constants or per-owner independent copies.

Error API: Set_EngineErrorCodeMessage7277B0..CE stores U16 variant7C and payloadbyte; Pack71A0C2..E4 writes one rawU8. Thus frame payload is7C00 + onecodebyte (outer interface framing retains existing M1/M2 owner).

Camera callee6560C8..61CE validates exposure==1 then gain==1, valid path sends reliable1/hot0 robot57, Viz, queues original int32/f32 nextparams, broadcastsAPI2F in order (6166/6176/6180/61A4), irrespective SendMessage return. Both payloads gainf32/exposurelowU16/bool, robotboolfalse versusAPIlive329; exactserializer/tag witnesses already checked OV14 packet reused. This check does not turn deferred pending apply into immediate current params.

Mode7 caller6B55E4..5690 requires ShouldProcess==1; CheckImageQuality nonzero logs and skips deque entirely; success ORs80 into header mask then enqueues at5686. A skipped mode reaches queue with its existing mask unchanged. This proves transport/caller semantics, **not** a fabricated source quality value.

**Still pending / no whole-producer PASS:** B's complete new packet and actual producer/input readiness comparison. Current M11-080 explicitly leaves which detection rectangles are collected and out-of-bounds GetROI behavior unresolved in that build; M11-081's later checked status alone does not establish those joins. Do not infer emptyROI, synthetic qualityGood, hostproposal=fullnativeheader, or wholepilot acceptance from this handler check. B has been asked to identify latest checked producer/input evidence or retain a precise source/implementation STOP. No production/test/git/status changes.


### IQ-A final per-unit verdict — independent source PASS

Read complete submitted `20261026-driveoff-image-quality-read.md` (IQ1–IQ8, constants/config, caller/header, API/camera joins, source-derived cases and explicit producer boundary). **PASS for that bounded receiver/caller contract.** This supersedes only IQ-A's pending-verdict paragraph, not its remaining producer boundaries. No behavior correction required. Personally preflighted/reread enum007C6700/table01034A94 (Unchecked/Good/TooDark/TooBright), error007749C8/table01033910 (4Dark/5Bright/6Good/7Count), and CurrentCameraParams setter00723ACA..FA (tag2F); primary data agrees. Exact field order/payload matches prior checked OV14 serializer evidence. No code/test/build/hardware acceptance implied.

Mandatory execution-order precision: 'oldF4' in earlier notes means a **LIVE post-Broadcast reload** at6556D6, storedF5 at6556DA; result.timestamp likewise LIVE reload6556E0 before F8 store6556E2. Receiver implementation must not freeze these before synchronous API/local callbacks. Final B report states this correctly. Mutable-global reads occur after the relevant branch/callback at source positions; no additional atomic-snapshot/reentrancy policy is inferred.

Phase2 may implement this defined receiver and actual available result transport, but this PASS does not authorize guessing missing CheckImageQuality detector rectangles/ROI/clipping or marking a missing producer ready. Those M11-080/M11-081 interfaces stay visible; whole default-frame producer remains unavailable unless separately supported by actual current inputs. A typed EngineErrorCodeMessage7C API plus existing camera57/2F/pending state must be production-bound, not private diagnostic-only. No source/tests/git/manifest mutation by checker.


## C7 — Typed nonrunning PutDownDispatch_LookForFaceAndCube losing candidate

Scope: actual PutDownDispatch chooser while DriveOff remains running, including platform-false ticks, and this competing owner remains NONRUNNING. Not a claim that selecting/starting the next LookForFaceAndCube action is implemented. Actual model/effort unobservable. known.py before005EF804,0059CD44,BehaviorLookForFaceAndCube::LoadConfig,0059EC48,005BBB74,005BC388,005F0878. Current record before relying on factory ownership:

```json
{
  "id": "M7-018",
  "title": "Shipped behaviour configuration and BehaviorClass factory binding",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "178-config table in M7 inventory Appendix C",
    "79-class entry-point table in Appendix B",
    "BehaviorContainer::CreateBehavior 0x0059c888",
    "class 0x39 BehaviorWait case constructs base IBehavior and installs the BehaviorWait vtable"
  ],
  "unresolved": "Opus verification of R-BEH2 batch 3 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-3.md): NOT YET. 76 of 79 classes report \"class not implemented\"; the second BehaviorContainer at robot+0x48 is unbuilt (use UNKNOWN); RequestAllBehaviorsList (0x0059C44C) is unbuilt; FistBump's seams are unset in production although SparksFistBump is bound; Behaviors.cs:67 uses Enum.TryParse, which accepts names the engine's map rejects; Implementable() builds duplicate objects. Before: built, awaiting strong verification: the container loop (0x0059c34c..0x0059c428), BehaviorClassFromString (cerr text, 0 = Bouncer), BehaviorIDFromString (cerr text, 0 = AcknowledgeFace; default 'IBeh.NoBehaviorIdSpecified'), AnimationTriggerFromString (cerr text, 0, only Count skipped), AddToFactory, the 79-way table (all 79 rows checked against the binary), FistBump (full state machine incl. the +0x84 gate, state-2 empty-callback turn, wrapped scan index N=2, terminal order, thresholds as bit patterns), ReactToSparked, and Hiccup's PlayAnim half are built. Config-built constructors exist for only PlayAnim, FistBump and ReactToSparked; the other 76 classes report 'class not implemented'. Still open: the second BehaviorContainer (robot+0x48) is not built and its use is UNKNOWN; RequestAllBehaviorsList (0x0059c44c..0x0059c676) is not built; FistBump's seams are MISSING in production (SmartPushIdleAnimation(0x23F = Count), EnableLiftPower/EnableHeadPower bodies, a live PanAndTiltAction (M13-020), BehaviorObjectiveAchieved's body and ordinals 7/8/9, the carry source); SparksFistBump is bound and can run live with those seams unset; ReactionTriggerStrategyFistBump/Hiccup/Sparked (M10) are not built, so the map rows for FistBump, Hiccup and Sparked remain unbound; Implementable() still hand-builds Hiccup, ReactToObstacle and six Feeding* PlayAnim objects that the container also builds (shadowed, dead in production); Behaviors.cs:67 Enum.TryParse is looser than the engine's name map. R-SUBSCRIBERS check (re-analysis/research/20261013-robot-message-subscribers-check.md): the second BehaviorContainer is BehaviorSystemManager's (0x0050FCBE -> 0x005A5812), built from the same map, so every behaviour is instantiated twice; the dev test behaviours' constructor subscriptions are M7-027. OV19 check (re-analysis/research/overnight/20261014-ov19-m8-016-check.md): settled: the robot+0x48 container is BehaviorSystemManager's; its behaviours are constructed and destroyed but never initialised or ticked (the BSM activity path has no caller).\nB-M3M4-25 (2026-10-15), built, awaiting strong verification: added a resource-root entry to the existing behavior-config loader, sharing its unchanged parsing/filter/first-ID implementation with the OBB-root API. The host retains that map before allowing ConnectToRobot (M2-021). Existing factory, ordering, constructor/activation and full loader gaps remain unchanged; no source row settled.\nB-M3M4-29 (2026-10-15), built, awaiting strong verification: retained configured resource-root map loading, but removed unsupported construction/connect exceptions tied to HostBehaviorMapLoaded. Null resource input is supported and has nothing to load; no live behavior factory/completeness claim is made by retaining this host map.\nB-M11-2 (2026-10-15), built, awaiting strong verification: retained host config map now supplies configured CantHandleTallStack owner presence and reaction-target existence for the M11-072 tag32 callbacks at native Robot construction. Two data owners correspond to the two checked native containers; this does not build a second behavior runner or certify the whole factory. Host Freeplay binds its existing manager Cant behavior to the first owner. Native weak-expiry, complete container behavior construction/other producers and exact native dtor points remain gaps, explicitly not inferred from these callbacks.\nManager (2026-10-18; research/overnight/20261018-ov127-check.md): LoadRobotConfigs (0x00522280..0x00523042) loads behavior_system_config.json (+0x100); this record owns its consumer (M1-054 keeps the load order)."
}
```

C6's literal current M8-002/M8-003 quotes remain applicable to scored/lifecycle pieces, not proof of a whole concrete behavior. M7-018 names the factory/config binding, not a fully implemented competing action. New owning-record proposal for manager allocation if absent: “PutDownDispatch LookForFaceAndCube nonrunning admission/scored-candidate construction and retained state”; evidence rows below; whole selected action remains a separately visible MISSING. No ID/status assigned by extractor.

| Row | Primary instruction witness | Exact bounded behavior / build input |
|---|---|---|
| LC1 factory actual inputs | CreateBehavior0059C888..8A4 saves classr2→r5,Robot&r3→r6,stackJSON→sb; class31 tablehalfword0059C8E6 routes0059CD44..64 | Allocates0x1A0bytes(CD44/48), passes SAME Robotr6 and JSONsb to derived ctor(CD50/52/54), constructs owning shared pointer(CD5E), common tail. Do not instantiate separate fake Robot or use generic zero-scored placeholder. |
| LC2 derived ctor / base first | 005EF804..F818 saves inputJSONr2→r5, calls IBehavior ctorF80C with callerRobot/JSON unchanged, then installs derived vtable addresspoint | Header0102A474+8 is actual addresspoint; concrete type is required for virtual behavior, though host memory/container mechanics may differ. |
| LC3 common actual defaults | 005BBBFA storesRobot+2C and stopstamp30=0;5BBC02 runstamp34/strategy38=0;C08 process1C=0;BCB6 unlock70=55,severe74=3,recent78=-1;BCBE max7C=-1;BCC4 action84=0;BCD2 halfwordA0=0=>A1=0;BBD28 flat100/bonus104=0;BBD2C suppress108=0/objective10C=41;BBD34 flags110/111=1;BBD38 resume114/cooldown118=0;BBD40 ReadFromJson | Preserve shared checked common config/readiness/lifetime owner. Shipped Look JSON root has only ID/class/displayName/params, so no added process/unlock/severe/recent/max/strategy root gate; use checked missing-key behavior rather than delegate `()=>true`. Flat0 constructor is NOT final activity score. |
| LC4 typed gate / raw robot flags / scores | Header+8+20→005BF04C returns0; +24→0059EC12 returns0; +28→005F0DC0 returns0 (existing Q17 RV51). +50→005EFD54 movs1,bxlr; +84/+88→base005BEEDE/005BEEC2 (C6 relocation check) | Common RB4 rejects nonrunning candidate when off-treads355!=0, platform34A!=0, or actual carrying[Robot284]+8!=-1. When those permit, derived gate always1; no face/cube-visibility dependency. Base score implementation, not an override or missing-class score0. |
| LC5 exact activity score/state | Actual activities/putDownDispatch.json chooser entry scoring{flatScore:1,repetitionPenalty:nodes[(.2,0),(.2,1)]}; checked Q17GP3 ReadFromScoredJson005BC488..684/ES1–4/PN3 | Attach scored JSON to SAME retained candidate, preserving equal-X nodes/insertion order. No emotion scorers; ordinary score is flat1 times native repetition when applicable. Running graph absent gets checked(0,1); no objective key preserves invalid41. Shared chooser continuation graph is a DIFFERENT field and retains its checked defaults. |
| LC6 prior run / action lifetime | Common Q17 IL4 Stop005BD0F4..D1CC clearsA1 before virtual54, stamps30, StopActing; derived StopInternal005F0878 is BXLR | Retain common stopstamp30/runstamp34/suppress108/graphs/cooldown118/action84, not per-evaluation reconstruction/reset. Fresh ctor30=0 means repetition1 (PN3); previously run stopped candidate must evaluate true persisted stop history. If owner is actually running, this bounded NONRUNNING assumption is false and full competing execution cannot be implied. No actionhandle84 read in derived constgate; common lifecycle still owns the handle for safe disposal/completion. |
| LC7 complete derived config write-set | LoadConfig005EF92C..FBD8 parses scalar/radian parameters into11C,124,12C,134,13C,144,14C,150,158,160,168,170,14D,178,179; parses animation name→174. Common prefix40 is read only for debug context. No stores to common RB/ES fields or scoring graphs; no callback to a competing action body here. | No effect on admission/scoring within this NONRUNNING interval: const gate and base ES/score do not read any of these >=11C fields. Preserve loaded asset/config identity for later implementation; do not advertise their selected action execution as complete. No need recover turns/search/face-verification action bodies to evaluate a losing candidate. |
| LC8 remaining derived ctor state | F81A..F846 initializes sixRadians11C..144;F84A/4C zeros14C/14D;F850..F86C initializesRadians150..168;F870..7E zeros174/170/178/179;F882..886 initializesRadians17C;F894..F89E zeros184/198,sets188=6,empty tree18C/190/194;F8A2..AE passes root[params] to LoadConfig | All are concrete derived search/action state; no additional common-scoring mutation. Within this interval they are not read by gate/score, so deferring their action use has no effect on this candidate's losing-score/RNG contribution. This limited no-effect proof does not cover starting/resuming/updating the selected behavior. |
| LC9 subscription boundary while inactive | F8B2..C2 tests14D/178/179; any nonzero builds/subscribes tag atF8D6/DE. LiteralF928=0067FF7C+PCF8D4→00C6F850 holds U16tag46. Shipped verifySeenFaces=true makes branch active. Vtable addresspoint+6C (HandleWhileNotRunning engine-to-game) raw0059EC49→0059EC48 BXLR; actual running handler+68 is005F087C and is NOT extracted | Registering this inactive recipient cannot mutate candidate state or consume chooser/group RNG: actual dispatch target returns immediately. Running handler/action transitions are deferred only under explicit NONRUNNING bound. Common constructor5BBD5E subscribes5A; ScoredConstructor5BC388..426 subscribes8B when interfaceexists, retaining handles; reuse checked common completion/objective filtering/lifetime, not globalfake observer or stale callback after disposal. |

Exact per-tick effect: after common RB gates and repetition evaluation, CS1 skips only score<=0. Otherwise nonrunning LC candidate consumes one shared Robot.GetRNG RandDbl call via0060A49E..4BC even if DriveOff remains winner1000. Candidate draw is f64limit3FB99999A0000000 added to widened f32score then narrowed once. Preserve stored ordered chooser traversal and actual timer history; do not assume alwayspositive after a previous stop, or skip it because lower score. During a fresh never-run candidate, stopstamp30=0 returns penalty1, so platformfalse + on-treads + rawcarry sentinel + eligible common gates gives score1 and one draw. If any common gate rejects, no derived score/draw; actual state, not class-not-implemented, must decide.

Smallest source-supported unit is therefore a real typed nonrunning candidate using actual Robot/common config/score/lifecycle and shared RNG, with constant derived gate and actual raw-state flags. No actionbody required for that losing interval. Once DriveOff ends or Look wins/needs Init/Resume, stop with explicit owning-record MISSING for unimplemented selected behavior; do not return fake success, fake running lifetime, reset stop stamps, or present absent candidate as score0. This is a bounded implementation inventory, not a feature exclusion or whole LookForFaceAndCube settlement. Different checker PASS required before adoption.
