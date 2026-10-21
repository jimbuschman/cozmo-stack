# R-M13-DRIVEOFF-JOINS — drive-off behavior remaining joins

Request: `requests/20261021-m13-driveoff-remaining-joins.md`. Examined HEAD152c58fa. Independent read-only production comparison, pending checking. Coverage opened before tracing.

| Step | Native / checked source | Production binding | Disposition |
| --- | --- | --- | --- |
| Animation push input/triplet | 5C0B34..50; tableC6731C | ChargerBehaviors.cs:70 MISSING | Producer argument recovered; severe-need live owner unbuilt |
| Push/pop ownership | 5C0B30/50,5C0D90..A4; handler M5-063 checked | Handler Push/Remove exists; behavior has no matching pop override | Small caller/cleanup gap, not missing handler bodies |
| Wait/update/re-entry | 5C0DA8..E34; G7/Q6 | ChargerBehaviors.cs:109..135 branches present | No new timeout; action-tag/cancel join incomplete |
| Cancellation / completion | checked IBehavior/ActionList;5BD34C..43C | Concrete DriveStraight reaches ActionList; host handle/async behavior adapter remains | Cancellation outputs partly built; native tag/subscription ordering absent |
| Old unresolved claims | current M13-017 | B-M13-9 now supplies contact observation | Stale contact claim; remaining profile/geometry/action owners retain gaps |

## Evidence / bounded result

Primary source: actual instructions in `resources/lib/armeabi-v7a/libcozmoEngine.so`. Reused M13 inventory G7b/Q6, correcting their stale “contacts” terminology with the checked platform interpretation; M5-063 checked handler rows; M8 checked StartActing/StopActing/ActionList ordering. Did not re-extract charger-contact observation or mount.

Fresh reads: Init5C0B18..BA8 plus literals; Transition5C0BB8..C86 plus literals; StopInternal5C0D90..A4; Update5C0DA8..E34 plus literals; StopActing5BD34C..43C (following strings excluded). For the named missing constructor boundary, DriveStraight two-argument constructor5470F0..725E including cleanup was read; its Init/Check bodies remain existing action obligations, not newly certified here.

**Remaining work is not simply an absent wait branch.** The branch/control inputs already exist in C#. The missing work is the behavior's synchronous driving-animation override ownership, the live severe-need input, and its real action-tag/completion/cancel recipient. A built handler and a queued concrete drive do not automatically join the host behavior handle to that runner.

## Animation producer and lifetime

| Row | Instructions | Exact source behavior |
| --- | --- | --- |
| D1 | 5C0B2E..30 | Reset behavior+120=0 each Init; this is “this behavior pushed an override”, not the handler's animation state |
| D2 | 5C0B34..3E | Read robot+264 AI→+30 SevereNeeds→+14, compare exactly3. This is **current severe NeedId**, with3=none from its constructor572A0E. It is not animation state3 |
| D3 | 5C0B40..4A | When value==3, pass literal triplet at **C6731C** and behavior name string at this+40 to robot+24C DrivingAnimationHandler.PushDrivingAnimations4F0944 |
| D4 | C6731C..24;5C0B4E..50 | Triplet u32s **0x97,0x93,0x8F** (151,147,143; generated enum DriveStartLaunch, DriveLoopLaunch, DriveEndLaunch). Set behavior+120=1 only after push returns |
| D5 | 5C0D90..A4 | StopInternal tests+120; if set, passes the same name string this+40 to handler RemoveDrivingAnimations4F0A94 through veneer8CABCC→PLT4A5818; else returns. It does not itself zero+120 |
| D6 | checked IBehavior.Stop5BD10C..126 | Clear running flag, invoke behavior StopInternal (pop), then StopActing(false,false). Thus behavior pop precedes cancel/destruction of its current drive |

The triplet is a direct ROM pointer passed to a built handler, not an unresolved animation group selection. Verify symbolic names against shipped/generated enum; native numeric words above are the source of behavior.

Reuse M5-063: Push appends (triplet,name), warns if state!=5, and **does not immediately change Current**. Current is selected by handler.Init, with newest override taking precedence. Remove searches by name and removes first match; warning/empty/not-found behavior stays handler-owned. The behavior's +120 decides whether to remove. Waiting off treads does not call StopInternal, so it keeps the override for a later re-drive; behavior Stop removes it. No invented idle/animation timeout is necessary.

Current `Animation/DrivingAnimationHandler.cs`:Push/Remove implement those checked bodies; Init selects override before drive state stores; concrete action completion is bound through M5-063 to real ActionList. `Animation/CozmoAnimations.cs`:253 public PushDrivingAnimations posts a G2E B5 message; Remove posts B6. Those are host-entry delivery methods. The native **internal** caller D3 invokes handler directly before drive construction/Init. Queuing a host B5/B6 after the current tick can change which triplet the drive selects; bind the internal call at the checked order.

Current `Behavior/ChargerBehaviors.cs`:OnStart70 emits MISSING and never pushes. There is no DriveOffCharger OnStop override storing/testing matching ownership or removing that name. The inherited ActionBehavior.OnStop only cancels its host task. Thus both ends remain actual producer gaps despite M5-063 now EXACT_SOURCE.

The severe-need field is named, not an “AI process input”: current `Behavior/IBehavior.cs`:69 and `Manipulation/ManipulationSystem.cs`:76 expose AiExpressedNeedValue delegates. Other callers visibly report absent SevereNeeds ownership; no live SetSevereNeedExpression/ClearSevereNeedExpression counterpart was found. A supplied delegate or assumed3 is not proof of the production writer. **UNKNOWN here:** full current severe-need writer/recovery closure. Next exact boundary is the existing SevereNeeds owner and SetSevereNeedExpression/ClearSevereNeedExpression (known Set call lead573152), not the driving handler. Constructor3 alone does not authorize treating it as immutable.

## WaitForOnTreads and re-entry

Native Init after D1–D4:
-5C0B54 reads committed OffTreadsState robot+355. Nonzero names WaitForOnTreads and returns0; zero calls TransitionToDrivingForward5C0B8C. Q6 establishes enum OnTreads=0 and other committed states nonzero. Reuse checked classifier producer instead of a new wait flag.

Complete Update:
| Condition | Instructions | Result |
| --- | --- | --- |
| Platform true and off-treads nonzero | 5C0DB0..DC4 | StopActing(false,false), then set state name WaitForOnTreads5C0DE0; return1. Not guarded on whether an action exists |
| Platform true and OnTreads | 5C0E0E..18 | If behavior+84 has current tag, keep running. If0, TransitionToDrivingForward. Return1 |
| Platform false and current tag nonzero | 5C0DF4..F8 | Keep running; no stamp yet |
| Platform false and current tag0 | 5C0DFA..E0A | Sample f32 BaseStationTimer, write whiteboard+44, return2. OffTreadsState is **not checked** on this branch |

A drive can finish while platform remains true; next Update can re-drive. Waiting preserves behavior running state/reaction lock/animation override. Becoming OnTreads while still platform true and actionless starts another drive. No timeout, timer or polling WaitAction is present in this behavior. No “must be OnTreads to finish” condition exists once platform is false and the action tag is0.

Current ChargerBehaviors73 and109..135 implement these conditions using Sensors.OnChargerPlatform, Sensors.OffTreadsState and HasCurrentAction, plus KeepsRunningWithoutAction=true. The f32 whiteboard stamp is already stored (Context.LastDriveOffChargerSec); LeftChargerOnTreads is a host result describing the eventual state, not the native completion gate. “WaitForOnTreads path unbuilt” is too broad: control branches are present, cancellation and current-tag correctness are the remaining join.

## Real cancellation / action ownership boundary

Native TransitionToDrivingForward5C0BB8..C86:
set DrivingForward, then recheck robot+34A platform at5C0BF4. If true allocate concrete DriveStraightAction, call **two-argument** constructor5470F0 at5C0C08, then IBehavior.StartActing(action,member callback)5C0C26. No separate host handle is created.

Fresh constructor boundary:
547138 compares distance;547154 stores distance;547158 stores accel200/decel500;547160 zeroes flags+88/+89;54716A sets play-animation+8A=1;54716E..7E chooses speed table547268 **-80.0f** or54726C **100.0f**, nonnegative ordered distance chooses100. For this behavior's positive96+extra shipped distances the default speed100 and play-animation=true agree with current constructor defaults. Manual-speed flag+88 is false, permitting shared profile override (profile-lifetime answer); the host signature alone does not reproduce that distinction. This narrow source read removes the “default positive speed entirely unknown” claim; it does not settle all path construction/CheckIfDone.

Reuse checked StopActing:
5BD358..360 clears scored bonus; when viaCallback=false stop live helper first; read tag+84 at5BD3DA. With keep=false clear it **before** ActionList.Cancel(tag)5BD3E4..F0; after call clear only if unchanged5BD3F4..FE. The synchronous local completed-action callback therefore cannot mistake the cancelled action as still owned by this behavior. Cancel still produces normal queue snapshots/destructor/game/local/watcher effects under M8-015; “without callback” suppresses this behavior member callback by tag handling, not all engine completion traffic.

Current actual joins:
- `Manipulation/DriveActions.cs`:1026..1033 queues concrete DriveStraightAction into the actual EngineRobot.ActionList and registers cancellation token to **Cancel(real runner Tag)**. Dispose1036..1048 conditionally aborts active path, calls handler.ActionIsBeingDestroyed, then base destruction/completes task. Later action/handler builds therefore invalidate any claim that this drive never reaches ActionList or that driving-animation teardown is entirely absent.
- `Behavior/ChargerBehaviors.cs`:82 calls parameterless StartActing producing a **host handle**, then creates/runs drive in an async closure.88..93 clears that handle and posts member completion through host task scheduling.
- `Behavior/FaceBehaviors.cs`:20..38 ActionBehavior.RunAction owns CancellationTokenSource; cancellation invokes the drive's registered real-Tag cancel; its completion is posted and filtered by token identity.
- `Behavior/ChargerBehaviors.cs`:116 calls CancelAction **before** StopActing. Thus real queue cancellation can occur before the host handle is cleared. Native source clears the real matching tag before Cancel. Do not claim equivalent synchronous callback ordering from eventual host suppression.
- `Behavior/SteppedBehavior.cs`:738..755 StopActing clears host tag/epochs but StopOwnAnimation is the host animation-ticket recipient, not Cancel(real drive Tag). The separate CancelAction is what reaches the concrete drive today.
- `Behavior/SteppedBehavior.Actions.cs`:15..71 has a concrete-owned StartActing overload, but DriveOffCharger does not use it;45 explicitly reports missing behavior constructor completion subscription/lifetime owner.100..110 implements HandleActionComplete without proving that local0x5A recipient is bound.
- Native global off-treads consequences may already cancel actions before this Update; reuse classifier/M8 ordering. This does not eliminate the need for the behavior's own matching tag/reset on its later update.

**UNKNOWN:** complete production binding of behavior instance constructor local5A subscription through5BBB74→5BE1E6 to the concrete drive, and all exposed host callback/API consequences of replacing the task adapter. This is an existing M8-015 owner gap, not permission to wire ActionWatcher as a deferred substitute. Profile-clear/destruction details remain the separate action-profile answer; M8-024 teardown survivors are not extracted here.

## Current claim correction / minimal build units

Current M13-017 snapshot below still says every-contact-true located-charger observation is unbuilt. **Stale:** B-M13-9 now builds that join; its status explicitly retains these separate behavior owners. Do not retrace contact observation or infer M13-017 settlement.

Small independent units after checking:
1. Bind severe-need current value, literal launch triplet and direct handler Push/Remove with behavior+120 ownership. Stop order pop before current drive cancellation; wait/re-entry retains override.
2. Use the actual concrete runner tag for this behavior's current action, checked completion subscription and StopActing(false,false) cancellation order. Existing action/handler/queue can be reused; no new wait timer.
3. Reconcile this positive two-argument DriveStraight default with source, retaining manual/custom-profile and pose/path gaps. Objective-achieved/mood recipients remain existing obligations outside this request.
4. Correct stale contacts/platform and “animation state3” prose only when manager adopts findings. Markerless/Ramp/HumanHead footprint, double-matrix cast and degenerate-hull claims in old unresolved are separate geometry owners; not newly certified here.

Tests: `NavigationTests.cs`:426..467 DriveOffChargerDrivesTheChargerLengthPlusTheExtraAndFiresTheEvent labels **unit reach**, substitutes task scheduling/model state, checks positive100 speed and eventual completion. It does not verify wait/push/pop/current real tag/local5A order; its “two-argument default is MISSING” comment is now narrower than the freshly read constructor evidence. M7BatchThreeATests lock/state-name cases do not close these joins.

Independent regression oracles (no tests added/run):
- Given native severeNeed3 vs0/1/2, same literal triplet/name push iff3, owning bit only when pushed; Stop removes by same name iff owned, before real drive cancel.
- Init off treads while platform true: no drive, override retained. OnTreads transition starts one concrete runner; repeated running updates do not duplicate. After complete while platform still true, next eligible Update re-drives.
- Off-treads while driving: actual behavior tag clears before local5A of cancellation; queue publishes completion/destructor effects but member success callback/objective/mood does not run. Next OnTreads re-drive uses a new real tag.
- Platform false with pending/current action waits; platform false with tag0 stamps/returns2 even when OffTreadsState nonzero. No invented timeout or “OnTreads required” completion.
- Handler Current at drive.Init selects pushed triplet synchronously; a queued B5 arriving a tick later is a failing ordering oracle.
- Unit two-argument constructor fixture checks positive100/negative-80, accel200/decel500, play-animation and manual flag; full real-drive regression must retain the existing M13 path limits.

No code/tests, inventory/manifest changes, approvals, hardware or settlement. Every open boundary above is assigned a current owner/address or UNKNOWN.

## Current manifest snapshot

Exact title/status/evidence fields follow; M13-017 also includes current unresolved for the stale-note comparison. Statuses retained unchanged.

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
  ],
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): BehaviorDriveOffCharger per the binary: runnable on OnChargerPlatform (+0x34A, 0x005C0B10, 0x005C0DA8), redrives until the platform flag clears, no 5 s timeout, the whiteboard +0x44 stamp is float and written only by the behaviour (0x005C0E02..0x005C0E08; the local FreeplaySystem writer is removed), IsRunnableBase reads it in float (0x005BD826..0x005BD962); the Robot::Update platform step (0x00513CD8..0x00513E2A) clears the flag from the charger and robot quads (minAreaRect built from the validated float32 port). Still open: the every-contact-true located-charger observation is unbuilt (explicit owner M13-040), so live the step takes the no-charger branch and clears the flag as soon as the contacts drop (the engine holds it until the footprint leaves the charger quad: reported MISSING); PushDrivingAnimations and the WaitForOnTreads path; the footprint for Markerless/Ramp/HumanHead; the rotation matrix is cast from a double; the GetBoundingQuad catch-all (0x004E65C6) and hulls of fewer than three points (reported MISSING)."
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
