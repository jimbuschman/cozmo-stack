# HIKING-ROLL-CUBE-1  -  complete bounded state-light recipient

2026-10-28, root explicitly authorized the necessary H7 continuation after the narrow boundary report established real robot traffic and no whole owning record. This supersedes that report's unrecovered/unbuilt recipient frontier with a complete SOURCE contract, not an implementation claim. Original frozen artifacts unchanged. Read-only, no production/tests/official metadata/git edits. Requested reader gpt-6.1-sol/medium; actual telemetry unavailable. Known.py preceded SetBehaviorStateLights, BehaviorManager ctor/dtor, SetRunningAndResumeInfo and StopAndPlayLightAnim original ELF reads; SHA02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Scratch `.scratch/hiking-state-light.txt` and `.scratch/hiking-light-init.txt`.

Scope is exactly Roll caller, common state-light setter and retained-state cleanup joins; lower cube-light engine reused; no sibling/activity light policy audit. Current record snapshots below were read before ownership reliance.

## Inputs and retained state

|Step|Exact source contract|Primary evidence|
|---|---|---|
| L1 caller | Roll constructs one BehaviorStateLightInfo, stride0x90: ObjectID prefix/vptr+value at0/+4; trigger0x19 at+8; modifier flagfalse at+C; zero ObjectLights at+10. IBehavior wrapper forwards its actual classbyte64, vector and boolfalse to actual Robot+44 manager. No connected/located or trigger-equality guard in wrapper. |5C8892..88CE;5BEE38..46 via8CBF7C -> PLT4B29DC -> 5A4320.|
| L2 initial retained owner | BehaviorManager is actual robot-lifetime owner (already checked manager lifetime). Ctor zeroes6C..83, therefore state vector78/7C/80 empty; writes halfword0039 at84, so savedclass84=39h and flag85=false. Setters do not own a private per-Roll copy. |5A0918..922 memclr length18h;5A0926..92A.|
| L3 function entry | SetBehaviorStateLights(manager, BehaviorClass class, vector const& input, bool flag) immediately stores flag to manager85, before logging or light calls; saves class for final store. Existing state vector is manager78/7C/80. Temporary updated-ID set starts empty. |5A4320..434C;5A4372..4398.|

## Complete normal setter algorithm

1. **Old-entry pass**, strictly existing vector order. For each OLD entry, scan the NEW input vector from its beginning for the first equal ObjectID value (compare old+4/new+4). Entry trigger equality is irrelevant: even the same trigger is replaced. With no match, call actual Robot+270 CubeLightComponent.StopLightAnimAndResumePrevious(old.trigger, old.ObjectID), discard its result, then advance old. This includes empty NEW input. `5A4384..43C6`, old stride90h at`5A44A2..A6`; new scan`5A43A2..B2`.
2. **Matched old entry**, use the FIRST matching NEW entry. Build a temporary ObjectLights all zero. If NEW.modifier(+C) !=0, copy ObjectLights semantic bytes:117bytes (+10..84) and its relative-point two words at+88/+8C (ObjectLights offsets78/7C); pass modifier=true. If false pass zero ObjectLights and modifier=false. Call StopAndPlayLightAnim(NEW.ObjectID, OLD.trigger, NEW.trigger, empty callback, chosen modifier, chosen ObjectLights). The bool function argument85 is independent of this per-entry modifier. `5A43C8..441A`.
3. The matching replacement's false return emits channeled INFO `Behaviors`, key `SetBehaviorStateLights.UpdatingStateLights.FailedToPlayLights`, format at`5A46CC` (object/trigger). It DOES NOT prevent state retention or a later entry. Insert the NEW object's virtual unsigned ID value into the temporary updated-ID set **even on play failure**. No insertion is done for an unmatched old entry. Advance OLD; each duplicate old ID causes another StopAndPlay call against the same FIRST matching NEW entry. `5A441E..449A`.
4. **New-entry pass**, strictly input order. For each NEW entry, get its virtual unsigned ID; if that ID is in the updated-ID set, skip this entry. Otherwise build zero/default ObjectLights or copy the NEW modifier payload exactly as above, then call actual CubeLightComponent.PlayLightAnim(NEW.ObjectID, NEW.trigger, empty callback, NEW.modifier!=0, chosen ObjectLights, integer0). If play returnsfalse, emit the same INFO diagnostic and continue. This pass does NOT insert IDs into updated-ID set: duplicate NEW IDs with no matching OLD ID each produce a Play call, in input order. `5A44AA..4556`, result/log`5A455A..45B4`, increment/loop`5A45B4..BE`.
5. After all stop/replacement/new plays, assign the COMPLETE input vector to manager78 (copy all entries including duplicates, failed plays and modifier payloads). Skip assign only when caller passed the same vector object as manager78. Then store classbyte to manager84, destroy the temporary updated-ID set, returnvoid. manager85 was already stored at entry. No rollback on a normal false lower-level return; no callback is retained because every light call supplies empty callback. `5A45C2..45DE`; class store`5A45D2..D6`.

ObjectLights padding is not behavioral data: native clears117 bytes and two point words; the lower existing CubeLight fields are colors/periods/offset/rotation/mode/relativePoint. No use of uninitialized padding is introduced. Roll supplies modifierfalse, but the bounded shared setter algorithm above also establishes modifiertrue semantic copies; no generic true exclusion is inferred. Existing M4-046 owns the lower ApplyAnimModifier operation/order.

## Direct stop/play and ownership recipients

- StopAndPlayLightAnim is not StopResume then Play. Its exact bounded wrapper `6396AC..639722` calls StopLightAnim(oldtrigger, explicit layer1, objectID, **false**) at`6396CE..D8`, discards that result, then copies callback and calls PlayLightAnim(objectID,newtrigger,callback,modifier,ObjectLights,integer0) at`6396DE..6F6`. Return ONLY Play'sbool. Thus no intermediate resume/default-layer pick is invented for a matching replacement. This is the directly required126-byte recipient; lower Play/Stop internals and their existing M4-018/M4-041/M4-045 source contracts are reused, including per-ID lookup/error gates and actual wire group.
- Unmatched removal and switch cleanup call StopLightAnimAndResumePrevious, which uses the existing checked layer1/resume behavior. It differs from the matching replacement wrapper's Stop(false). Reuse actual cube-light engine owner, not facade CubeType remapping or a behavior-local animation list.
- Saved state remains manager-owned across interruption/resume. Existing instruction-checked MS4 (`20261007-q17-checked-additions.md`,5A2114..226E) applies: require retained vector nonempty AND new current behavior nonnull AND new behavior.class != savedclass84. Then clear iff (new behavior object differs from BOTH old current and old resume objects) OR (old running/resume-info byte+10==16h and new info byte+10!=16h AND savedflag85==0). If not clear, retain vector. Clearing calls StopLightAnimAndResumePrevious(trigger,ObjectID) for EVERY retained entry in order (5A2230..2246), then sets end7C=begin78 (5A2248..226E). It does not reset class84 or flag85. Null new behavior does not satisfy the class-difference gate. Caller identities/class/info-byte inputs remain the existing checked SetRunningAndResumeInfo owner; do not reduce to `StopInternal` unconditionally.
- Native normal destructor5A0D7E..D82 destroys the state vector's storage; it does not call a state-light Stop function there. Existing robot teardown/light engine lifetime stays exact. No extra Stop/Play is added on data-vector retirement. The setter itself has no retained child handles/callbacks/timers; its allocation/unwind machinery is an internal host implementation choice, normal lower-level false results retain native behavior above.

The Play overload used here takes a trailing integer0, NOT a layer argument. It is `6389FC`: `638A14 movs r0,#1`, store atsp14, `638A24 r3=&sp14`, `638A34` calls the explicit-layer PlayLightAnim with layer1. Both new plays and StopAndPlay replacement therefore use engine layer1; the trailing integer0 remains separately passed unchanged. This is already the current M4-046 evidence. Earlier narrow-boundary wording calling that integer layer0 is superseded here.

## Roll result and proposed owner

Roll's one entry causes actual trigger25 on its actual target after helper delegation/score update. Re-entering PerformingAction calls the setter again; because the object ID matches, it uses StopAndPlay even if trigger remains25. Retargeting stops unmatched old ID first, then plays new ID; later manager switching uses MS4. Modifierfalse and callerboolfalse have distinct meanings. No traffic-free proof or authorized exclusion exists for H7.

Proposed NEW shared-recipient record (NO ID allocated): **BehaviorManager state cube lights: SetBehaviorStateLights ordered old/new ObjectID diff, replacement and new-play arguments, modifier payload, retained vector/class/flag, IBehavior forwarding and switch cleanup applicability**. Own full5A4320..45F2 plus wrapper5BEE38..46, initial78/84/85 state5A0918..92A and its directly required StopAndPlay wrapper6396AC..722; reuse the existing MS4 cleanup body and lower light owners as cited recipients, without duplicating their ownership. Its unresolved should name any implementation discrepancy until built and checked; source packet settles algorithm, not status. This is a shared observable step, not a per-Roll configuration fragment. Neither M4-046's modifier census, M8-011's custom-Smart lights nor M8-012's broad switching title alone owns this whole setter in current evidence. Whole Roll cannot hide the shared bridge in a code comment or claim complete until actual state-light behavior executes.

## Current full title/status/evidence quotations

```json
{
  "id": "M4-046",
  "title": "ApplyAnimModifier body and the PlayLightAnim callers that pass modifier = true",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m4-cube-lights-check.md: ApplyAnimModifier 0x00638A8C..0x00638B3A (only caller 0x006384F0) is unread; the callers of PlayLightAnim 0x006382D0 and its overload 0x006389FC that pass modifier = true are unscanned.",
    "re-analysis/research/20261012-m4-046-anim-modifier.md: ApplyAnimModifier 0x00638A8C..0x00638B3A: copy src to dst; per node, i = 0..3: on/off colours OR, the four periods and offset wrapping 32-bit add; rotationPeriod += mod+0x70 inside the i-loop (4x, 0x00638B04..0x00638B0E); mode byte := mod+0x74 (0x00638B1A); relativePoint := mod+0x78/+0x7C raw copy; duration, canBeOverridden and name unchanged; no logs. PlayLightAnim call 0x006384F0 and the modified list into Blend (0x00638502..0x00638506); the overload 0x006389FC forwards with layer 1; every caller passes int 0; callers table in the research file."
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
  ]
}
```

```json
{
  "id": "M4-018",
  "title": "CubeLightComponent: layers, PlayLightAnim gates, SetObjectLights/SetLEDs, the SetLights wire group (SetCubeGamma, CubeID, CubeLights, white balance), WakeUp, EnableGameLayerOnly, default-layer anims, the +0x2C4 hold",
  "status": "EXACT_SOURCE",
  "evidence": [
    "S5..S8, LC1..LC8, LC6, C10.3 rows S3..S7, W1..W5, P1..P5, D1..D5 as before",
    "C11.2 robot+0x2C4 is the robot-is-localized flag: default 1 (0x0050FF10), 0 by Robot::Delocalize (0x00510A4A), 1 by SetLocalizedTo (0x0051245E, 0x005124EE) and CheckAndUpdateTreadsState (0x0051217C); the RobotDelocalized +0x21 refresh is held while delocalized and applied when it returns to 1 (gate 0x00637930)",
    "S3 the cube-sleep flags are written only by EnableCubeSleep; the equivalent public API is Robot.cs:2090",
    "C12.1 the static ObjectLights for EnableGameLayerOnly(enable=1) is 0x0105B4A0, filled by _INIT_36 (0x004D7EB4..0x004D7EE5): eight u32 0x000000FF at +0x00..+0x1F, the rest zero",
    "C12.2 the enable=1 branch is live: game tag 0xBB EnableLightStates -> 0x0063A48A..0x0063A496 and 0x0063AFF8..0x0063B006 pass enable = (msg.byte0 == 0); the app sends it from Robot.cs:1902-1907",
    "C13.1 EnableGameLayerOnly branch detail: all-objects enable=0 stops layer 0 once (0x00639ADA/DE, 0x00639AE6) and sets comp+0x22 := 0 (0x00639B32); single-object enable=0 stops layers 0 and 2 (0x00639AA2, 0x00639AB0) and does not touch comp+0x22; enable=1 sets ObjectInfo+0x1C := 1 and (all-objects only) comp+0x22 := 1",
    "C13.4 EnableGameLayerOnly returns without acting when comp+0x22 (all-objects, 0x00639A16..0x00639A1C) or ObjectInfo+0x1C (single-object, 0x006399DC..0x006399E0) already equals enable",
    "Handoffs: PickNextAnimForDefaultLayer's BlockWorld::GetLocatedObjectByIdHelper blx 0x4A7114 at 0x00637DB4 (Visible needs the located object's +0x24 == 1), SetObjectLights' located lookup blx 0x4A7114 0x00637E82 and connected lookup blx 0x4A6FB8 0x00637E96, ActiveCube::MakeStateRelativeToXY blx 0x4B9288 0x00637EE8: M11-004; the +0x2C4 writers: Robot::Delocalize M11-053, SetLocalizedTo M11-044, CheckAndUpdateTreadsState M10-001",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 5 (Opus extraction, manager-adopted 2026-10-11): the full log rows (level, channel, key, format, arguments, gates, errG/break and order) and the differences from the C#.",
    "re-analysis/research/20261011-m3m4-fix-rows.md section 13.2 (follow-up Opus extraction, manager-adopted 2026-10-11)."
  ]
}
```

```json
{
  "id": "M4-041",
  "title": "BlendAnimWithCurLights body, the StopLignAnim.Result serializer and the GetLEDState callers",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m3m4-settling-2.md: PlayLightAnim blend body (unread); StopLignAnim.Result current-state serializer (0x00639300); ActiveObject::GetLEDState IndexTooLarge/NegativeIndex (0x0063958E/0x006395CC) and its callers.",
    "re-analysis/research/20261012-m4-cube-lights-check.md section M4-041: Blend 0x00638B3C..0x00638BF0 (NullActiveObject channeled info 0x638C24/0xBFAA4C returns 0; per i=0..3 GetLEDState 0x00638B7C, zero on/off word -> rev of the LED word, flag; returns flag&1); StopLignAnim.Result debug 0x006390BA..0x00639300 (grammar and literals 0x6394EC..0x639524, layer table 0x0102EE64, found/did not find 0x639538/0x639528); GetLEDState 0x0063956C..0x00639600 (callers only 0x00638B7C and 0x0063A642; both warnings unreachable)."
  ]
}
```

```json
{
  "id": "M4-045",
  "title": "PlayLightAnim: the blended / non-blended branch after BlendAnimWithCurLights",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m4-cube-lights-check.md: branch condition (modifierArg | BlendReturn) & !backBlended (0x00638590..0x0063859A; backBlended from the stack back +0x40, 0x0063830C..0x00638326); blended: per-layer slot node+0x44+12*layer := blended list (0x006385B6), name back.name + \"+\" (0x6389AC) + animName or \"\" + animName on an empty stack (0x006385C0..0x0063860C), info +0x10/+0x14 -> slot, +0x38 blended = 1 (0x00638698..0x006386AE), no zero-duration clear; non-blended: name animName, container list, stack cleared if the first pattern duration_ms == 0 (0x006385EA..0x006385F4). Shipped DanceFadeOff, Flash and SpeedTapWin hold [0,0,0,0] colours, so they take the blended branch with a connected object."
  ]
}
```
