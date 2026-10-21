# R-M8-PROFILE — action-owned custom motion profile lifetime

Request: `requests/20261021-m8-action-motion-profile-lifetime.md`. Examined HEAD 166a2e6a. Independent extraction/read-only comparison, pending manager checking. Coverage table opened before tracing; its OPEN entries are replaced below.

| Step | Native instructions / writer / caller | Production binding | Record | Disposition |
| --- | --- | --- | --- | --- |
| Runner+58 ownership | Constructor word store 53FE18..1C zeroes byte58; SetForAction64AC3A..48 sets it; destructor5410EC..F6 consumes it | IActionRunner.cs:528 explicitly reports missing recipient | M8-015 | Recovered narrow lifetime; global alias/writer completeness UNKNOWN |
| Path current flag | Set64AB10..ABA6 copies41 bytes and sets Path+48; Clear64AC4A..50 clears only flag | Motion.cs:1517/1550 Field48 reset, NativePathComponent.cs:144 missing handoff | M12-032/M13-033 | Not the action ownership flag |
| Behavior ownership | SmartSet5BE518..53E and SmartClear5BD584..5A6 use behavior+C0 | IBehavior.cs:362..387 delegates have no production callers | M8-011/M8-009 | Distinct owner, repeated-call behavior differs |
| Install/use/destruct | DriveToPose factory52A11C..214, Init55A86C..AAA0, destructor55A5FC..686, base5410EC | Existing async/manual profiles do not bind native ownership | M8-015/M12-032 | Supported driving feature, native path recovered; exact app sender UNKNOWN |
| Overlap / downstream | Last setter copies shared bytes, every marked owner destruction clears flag; StartDriving64ADD2..ADF2 and runner5403B8..3D6 read flag | No bound shared profile lifecycle found | M13-033/M12-032 | Concrete effects below, no profile restoration |

## Evidence boundary

Primary evidence is Thumb instructions in `resources/lib/armeabi-v7a/libcozmoEngine.so`. Decompiled files navigate symbols only. Reused queue deletion/destructor ordering from the ActionList answer and checked OV18; reused M12 gap2 F1 for the planner-to-wire handoff. This is not a new whole-planner or whole-behavior extraction. No production code, tests, manifest or inventory changed.

The old profile lead in `20261021-m8-actionlist-completion-settling.md` is confirmed: Path.HasCustomMotionProfile supplies Update, while runner+58 supplies destruction. Neither can substitute for the other.

## Flags and mutations

1. **Runner ownership, byte+58.** Constructor's little-endian word 0x00010000 at runner+55 (53FE18..1C) initializes bytes55=0,56=0,57=1,58=0. SetCustomMotionProfileForAction64AC3A saves action r2, calls ordinary SetCustomMotionProfile at64AC3E, then writes action+58=1 at64AC44. There is no action-owned profile copy in this routine. Base destructor5410EC tests that byte and, if nonzero, calls this robot's Path.ClearCustomMotionProfile at5410F6. This precedes tag release and later track/watcher teardown; it is not conditional on the path currently being marked custom.
2. **Path flag and storage.** Path+48 denotes current custom-profile selection; Path+4C points at shared profile storage. Set64AB18..54 warns when already selected but continues. Input profile byte+28 being zero warns64AB54..90 but also continues.64AB92..98 copy ten words;64AB9A..9C copy the eleventh field, one byte;64ABA0 sets Path+48=1. Thus the transferred object is ten f32 fields plus a byte, 41 bytes, not an eleven-float object. Clear64AC4A..50 writes only Path+48=0. It neither clears/replaces stored bytes, restores an older profile, nor clears any runner/behavior ownership byte. Has64AC34..38 reads Path+48; Get64AC54..6C verifies when false but still returns Path+4C.
3. **Behavior ownership, byte+C0.** SmartSet5BE51E..53E verifies C0==0, calls Path.Set, then sets C0=1. The VERIFY return value is ignored; repeated Set still overwrites. SmartClear5BD588..5A6 verifies C0!=0, calls Path.Clear, then C0=0; repeated Clear still clears Path. These are native void helpers. They do not set/clear runner+58. The repeated-call discrepancy is already described by Q17 SS3; this pass freshly confirms instructions, not a new adopted finding.

Finite direct-call scan: SetForAction body64AC3A / PLT4A92EC has direct calls at529CAE,529CF0,529FB4,529FE2,52A074,52A0FE,52A206,52A2E6,52A3E2,52A424,52A604,52A712,52A90A,52A938,52AA6E,52AAB0,52AE4E. These are native action factories; one complete factory is below. Ordinary Set body64AB10 / PLT4B2994 has direct calls5BE534 and64AC3E. A linear Thumb immediate-branch scan establishes these direct sites, not completeness against indirect calls, arbitrary aliases or unrecognized code/data. **UNKNOWN:** exhaustive runner+58 writer census beyond the constructor, named helper and destructor traced here. Do not promote this finite scan to a whole-program no-other-writer proof.

## One install/use/destruction path

DriveToPose is a supported ordinary robot-motion feature; receipt through the engine game interface does not establish SDK-only provenance.

- Native factory entry52A11C constructs the target pose from message x/y and angle using the robot's world origin (52A128..16C), names the pose (52A186..196), then constructs DriveToPoseAction55A3D4 at52A1F2. The optional **message+34** flag is read52A1F6; if nonzero, it calls Path.SetCustomMotionProfileForAction with **message+0C** and the new runner at52A1FC..206. This installs immediately, before the factory returns the runner52A210..214; ownership does not wait for Init or successful queue admission. Do not use the other factories' message+2C flag/+4 profile offsets for this message.
- Admission/tick is the already checked ActionList path (53D93C..DB70,53F580..F708). On initialization the common runner samples the **path flag**, not its ownership byte:5403B8..3C2; if true, it gets the shared profile pointer and calls virtual SetMotionProfile(profile) through vtable+18 at5403C4..3D4. False only takes the Unused diagnostic and continues. This hook is a consumer, not an ownership setter.
- Full DriveToPose.Init55A86C..AA66 plus exception cleanupAA6A..AAA0 was read. It initializes driving-animation handling; validates/reexpresses goal poses; on the successful-goals branch calls Path.StartDrivingToPose at55A998. It does not clear custom ownership on a failure return. Error returns and optional head movement do not turn its ownership byte into the path-selection flag.
- StartDrivingToPose64ADD2 reads Path+48. When false,64ADE0 calls SpeedChooser.GetPathMotionProfile53B9C0 and64ADE4..ADF0 copies41 bytes into Path+4C. When true, the copy is skipped, retaining the custom profile. This is where clearing can change subsequent planning input.
- HandlePlanComplete649F76..82 reads Path+4C and passes it to IPathPlanner.GetCompletePath5091CC. That complete wrapper first calls the concrete path getter; failure returns false5091DA..E0. Success with a non-null profile calls ApplyMotionProfile508AE4 at5091EC before copying the adjusted path back5091F0..FA. Its individual segment calculations are the existing M13-033 obligation, not extracted again here. The reused M12 F1 handoff isPath.Update/UpdatePlanning → HandlePlanComplete649F60 → ExecutePath64A0A4 → ExecutePath64A340 (Dole call64A436) → PathDolerOuter.Dole507E4C → segment wire messages.
- Cancel/delete uses the checked queue path: current action Cancel then Delete, pending direct Delete; Delete snapshots game completion then virtual destruction (53E690..E702,53F9E4..FB06). Thus even a never-initialized pending owner reaches the clear on destruction. Cancel alone is not the ownership-clear point.
- DriveToPose destructor55A5FC..660 calls Path.IsActive at55A60E, conditionally Abort at55A61A, erases planner visualization, informs DrivingAnimationHandler, and releases pose/shared/vector members. Its final branch55A660 goes through Thumb/ARM interworking veneer8CB50C; ARM8CB510..514 resolves to PLT4AAB4C, IActionRunner destructor541084. The exceptional cleanup also calls that base at55A680. Base5410EC..F6 then performs the ownership clear described above.

**UNKNOWN:** the precise supported official-app sender(s) and stored app setting that produce this optional custom-profile message. The native engine factory and supported motion semantics are established; a complete Unity sender census is not. Do not exclude this branch as SDK-only based on transport alone. Also not claimed: a complete host factory/admission failure ownership equivalence or full concrete planner extraction.

## Overlap and downstream consequences

There is a single mutable path profile with independent boolean lifetime markers, not a stack of profiles or an owner identity:

| Sequence | Result proved by setters/clear |
| --- | --- |
| SetForAction(A,P), SetForAction(B,Q) | Shared bytes=Q, Path+48=1, A+58=B+58=1; second Set warns but succeeds |
| Destroy A while B survives | Path+48=0; shared bytes still Q; B+58 still1 |
| Behavior.SmartSet(P), action.SetForAction(Q), behavior.SmartClear() | Path+48=0 despite action still owning its destructor clear |
| Action.SetForAction(P), behavior.SmartSet(Q), destroy action | Path+48=0; behavior+C0 remains1 |
| Clear while a path is already planned | No immediate replan, stop or wire send in Clear; already copied path/runner fields are untouched |
| Next StartDrivingToPose after Clear | SpeedChooser overwrites shared profile; before Clear, that copy would be skipped |
| Runner initial profile sampling after Clear | Virtual SetMotionProfile is skipped; owner+58 can still be1 and will clear again on destruction |

These are instruction-derived conditional sequences, not a claim that each exact overlap occurs in shipped behavior selection. **UNKNOWN:** concrete shipped overlap schedules beyond the native entry points; no whole-behavior census was requested. Defined behavior must not be replaced by an invented owner stack or “clear only if I remain current” guard.

The runner hook can also affect direct motion. Read complete narrow overrides:
- TurnInPlace.SetMotionProfile545D36..4E: manual-speed flag+CC rejects; otherwise profile+C (point-turn speed) and+10 (point-turn acceleration) populate action+C4/+C8, returns true.
- DriveStraight.SetMotionProfile54759E..5D4: manual flag+88 rejects; otherwise distance+78 sign selects profile+0 speed versus negated profile+24 reverse speed; profile+4/+8 become accel/decel at action+80/+84, returns true. Preserve native VFP condition behavior; this report does not prescribe a new NaN policy.
- IDockAction.SetMotionProfile55043E..45C: manual flag+B8 rejects; otherwise profile+18/+1C/+20 populate dock speed/accel/decel at action+AC/+B0/+B4, returns true.

Clearing changes whether these hooks are invoked at common runner initialization. It does not retroactively undo values already copied into an action. Exact direct-motion wire consumers are outside this bounded profile-lifetime trace; existing action records remain responsible, **UNKNOWN here**, rather than claiming whole direct-action settlement.

A supported behavior caller is BehaviorDriveInDesperation.Init5D8ECA..: it passes its config profile at config+4 to SmartSet at5D8ED8. This short caller prefix was read only through the call; the entire behavior state machine is not newly extracted. Its current C# OnStart does not install a scope profile.

## Current implementation comparison and build boundary

Repository paths below are under `cozmo-stack/src/Cozmo.Robot/`.

- `Actions/IActionRunner.cs`:257 HasCustomMotionProfile defaults false;268 SetMotionProfile has **no profile argument** and defaults false;304 uses those virtuals. Search found no production overrides/bindings in Actions.528 explicitly emits MISSING for the runner+58 destructor recipient. This is a visible implementation gap, not an implemented clear.
- `Motion.cs`:1517 Field48 exists,1550 resets it. `NativePathComponent.cs`:144 explicitly emits MISSING for the SpeedChooser/current-profile join. A flag field alone is not the robot-owned storage/set/get/clear API.
- `Behavior/IBehavior.cs`:362 SmartSet accepts arbitrary set/clear delegates; no production SmartSet caller was found in robot source.367 returns false on repeat, preventing the native overwrite.382 returns false on redundant Clear, preventing the native unconditional path clear. Delegates or unit tests supplying them do not establish live path ownership.
- `Behavior/ExplorerBehaviors.cs`:327..337 stores a PathMotionProfile;347 OnStart only TransitionToIdle. Later DriveToPose/DriveToObject receive Profile explicitly. That may carry local speeds but does not reproduce native5D8ED8 shared selection/lifetime or effects on other runners.
- `Manipulation/RobotPath.cs`:PathMotionProfile has ten float properties, lacks the native+28 byte. Supplying default/manual action profiles is separate from binding the optional native custom-profile input and its lifetime.

Recommended bounded implementation after manager checking: bind robot-owned profile storage and selection separately from action and behavior lifetime bits; bind SetForAction, runner initialization's profile argument and the destructor clear at the checked point; bind behavior SmartSet/Clear to that same recipient. Preserve last-write-wins and unconditional clears. Resolve the profile byte's meaning/production constructor before inventing its value; this pass recovers transfer/warning behavior, **not the byte's full semantic producer**. Do not silently treat the existing ten-float record as complete. Reuse checked segment and queue machinery; no broad planner rewrite warranted.

Independent regression expectations (not tests added/run by research):
1. Two owners with distinct profile values; destroy the earlier owner, then initialize another runner/drive: no restoration and custom selection is false.
2. Pending owner deleted before Init clears selection; Cancel without destruction does not.
3. A runner with ownership false still samples a selected shared profile at Init; an owner with selection false skips the hook but clears again at destruction.
4. Native fixture confirms repeated behavior Set overwrites, repeated Clear clears a profile installed by another owner; stale behavior ownership is not synchronized with action clear.
5. Wire/plan oracle compares next-drive SpeedChooser selection after clear against retained custom selection before clear. Merely asserting a boolean/delegate call is insufficient.
6. Profile fixture covers ten f32s plus byte+28 and verifies Clear preserves stored bytes; manual-speed overrides reject profile use without changing ownership.
No hardware acceptance, production edits or regression execution performed. Findings remain unadopted; no fidelity status or approval changed.

## Current manifest snapshot

The following exact title/status/evidence fields were read from the current manifest, rather than recalled. These records retain their present scope; this answer does not settle them.

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

### M8-009

```json
{
  "title": "Behaviour scope undo order: the engine releases in a fixed order (reaction locks, idle, motion profile, track locks, light patterns)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::Stop 0x005bd08c releases the scope in a fixed order regardless of acquisition order: (a) remove every disable-reactions lock while +0xac != 0 (0x005bd12c ldr.w r0,[r4,#0xa4]; 0x005bd136 blx SmartRemoveDisableReactionsLock; 0x005bd13a/0x005bd13e/0x005bd140), (b) remove the idle animation if +0xb0 (0x005bd142 ldrb.w r0,[r4,#0xb0]; 0x005bd14c blx SmartRemoveIdleAnimation), (c) clear the motion profile if +0xc0 (0x005bd150 ldrb.w r0,[r4,#0xc0]; 0x005bd158 blx SmartClearMotionProfile), (d) walk the track-lock map at +0xb4 and MovementComponent::UnlockTracks(mask, name) each, then destroy the map (0x005bd15c/0x005bd16e/0x005bd174 blx 0x4a5794; 0x005bd19e blx __tree::destroy), then clear the custom light-pattern vector at +0xcc/+0xd0 (0x005bd1a2 ldrd r1,r0,[r4,#0xcc] ... 0x005bd1c6 strne.w r0,[r4,#0xd0]).",
    "This stack's BehaviorScope groups its undos by the same categories and releases them in that order (IBehavior.cs, BehaviorScope.Dispose): reaction locks (SmartDisableReactionsWithLock/DisableReactions), idle (SmartPushIdleAnimation), motion profile (SmartSetMotionProfile), track locks (LockTracks/SmartLockTracks), custom light patterns (SmartSetCustomLightPattern). OnRelease hooks have no engine counterpart and run last. Within a category the undos run most-recent-first, which is required by the track-lock undos' saved-mask restore; the engine unlocks each map entry independently, so only the category order is observable.",
    "The policy review of 2026-09-29 (verdict C) found the divergence; @cozmo-verifier re-read all five release sites and the light-pattern tail (0x005bd1a2..0x005bd1c6), which the review had not read past 0x5bd180."
  ]
}
```

### M8-011

```json
{
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

### M13-033

```json
{
  "title": "Planner side of PathComponent: planner construction, SelectPlannerHelper, lattice ComputePath/ComputeNewPathIfNeeded/StartPlanning (both force values, safe-prefix retention), async worker and F0-F8 flags, StopPlanning guard, GetCompletePath, the short planners, IPathPlanner wrappers, slot 0x20/0x18 safety dispatch",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-m12-032-path-status.md: \"Unread\" section: SelectPlannerHelper 0x0064A5BA..0x0064AA18; LatticePlanner 0x004FE688, 0x004FEB00, 0x004FD1A2, 0x004FF936; FaceAndApproach/MinimalAngle vtbl+0x2C; ComputeClosestGoalPose 0x005087AC; ApplyMotionProfile 0x00508AE4; planner status base 0x00508A94..0x00508AAA.",
    "re-analysis/research/20261013-planner-internals-check.md: ctor 0x00648AE0..0x00648C54; SelectPlannerHelper 0x0064A530..0x0064A8DA (not 0x0064A5BA..0x0064AA18); ComputeNewPathIfNeeded 0x004FEB00..0x004FEB34; StartPlanning 0x004FEB44..0x004FF452 (false branch, prefix 0x004FECDE..0x004FECF6, Append 0x00500540); worker 0x005007E0..0x00500854; DoPlanning 0x00500090..0x00500590; StopPlanning guard 0x004FD1A2..0x004FD1CC; lattice GetCompletePath 0x004FFB64..0x004FFE2E; FaceAndApproach 0x004F35A8..0x004F38F6; MinimalAngle 0x00503C18..0x005041B2; IPathPlanner 0x005087AC..0x00508AE2, 0x005091CC..0x0050928A (base internal returns 0 when +0x28 == 0); safety dispatch slot 0x18 0x00649FB6 -> 0x00509202 -> slot 0x1C."
  ]
}
```
