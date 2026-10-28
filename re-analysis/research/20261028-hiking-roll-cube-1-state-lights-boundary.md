# HIKING-ROLL-CUBE-1 — state-light ownership boundary

2026-10-28, root finite available-evidence question. Original frozen artifacts unchanged. No production/tests/official metadata/git edits. Known.py SetBehaviorStateLights preceded narrow original-ELF wrapper/effect checks (same SHA02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1). Full light-manager extraction is not claimed.

Current records below were read before this ownership finding. M4-046 owns modifier arithmetic and caller modifier flags, including the recorded state-light modifier=false census (`20261012-m4-046-anim-modifier.md`); it does not certify the manager's state-light diff/play/retention body. M8-011's SmartSetCustomLightPattern is a different function and ownership mechanism. M8-012 owns manager switching; existing checked `20261007-q17-checked-additions.md` MS4 /5A2114..226E establishes later state-light cleanup during switches, not SetBehaviorStateLights itself. A direct manifest search found no dedicated current record whose title/evidence covers the whole5A4320 setter body. This is an existing shared observable gap, not permission to duplicate or silently narrow another record.

The caller join is concrete: Roll5C88A6..CE supplies one90-byte entry {actual target ObjectID, trigger0x19, modifierfalse} and boolfalse. IBehavior wrapper5BEE38..46 forwards the vector and bool unchanged plus classbyte64 into actualRobot+44 BehaviorManager. The bool is retained atmanager85 (5A433C), not an enable/disable gate around all light effects. The narrow checked effect points are sufficient to rule out no-effect: old unmatched entries callStopLightAnimAndResumePrevious at5A43C2; matching entries callStopAndPlayLightAnim at5A441A; a new entry calls realCubeLightComponent.PlayLightAnim at5A4556 with supplied target/trigger and emptycallback/modifierfalse/layer0;5A45CE copies the new vector into retainedmanagerstate. Existing MS4 later reads this retainedvector during switches.

The lower CubeLight animation/send recipient is already available; the manager's full state-light diff/play/retention/cancellation bridge is not implemented in the current canonical checkout. Source search for state-light names/diagnostics found only `CubeGameBehaviors.cs:656`, an explicit M12-028 state-light-body MISSING, and no live SetBehaviorStateLights implementation. That is candidate-code availability evidence only; native instructions above establish that the missing bridge matters.

Disposition: Roll supplies an exact implementable CALLER tuple, but cannot wire it into a currently built manager recipient. Preserve explicit shared-recipient MISSING and keep whole Roll unsettled until that bridge is recovered/implemented/checked or an authorized feature decision applies. Do not send a one-off PlayLightAnim and call the manager bridge complete: old-entry replacement, retainedvector and subsequent switch cleanup also feed robot traffic. No new record/status/approval decision is made here.

## Current title/status/evidence snapshots

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
