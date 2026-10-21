# R-M8-EXTERNAL-ADMISSION — live action admission gate producer

Request: `requests/20261021-m8-external-action-admission.md`. Examined HEAD3609db0c. Independent read-only extraction, pending checking. Coverage opened before tracing.

| Step | Native / ownership | Production binding | Disposition |
| --- | --- | --- | --- |
| Owner/default | Robot+2C7;50FEFA..FF18 | ActionList.ExternalActionsDisabled defaultsfalse | Initial value recovered, live ownership absent |
| Supported writer | SetRunningAndResumeInfo5A20EC..2110 | BehaviorManager.cs:781 optional unbound delegate | None/reaction crossing writer recovered |
| Paired movement state | 5A2106..10; helper5A2F54..64 | Motion._directDriveDisabled no writer | Same input writes MC+D4 first, Robot+2C7 second |
| Queue consumer | 53D94C..70; checked OV18 Q2 | ActionList.cs:167 read/test property | Established ranges; no producer supplied |
| Supported vs SDK | Native reaction switch5A26DA; Unity Robot.cs tag allocator/SendQueueSingleAction | Host runners mostly keep internal tags | Ordinary app range positive witness; no SDK-only exclusion |
| Reset / uncertain writers | Constructor and opposite reaction transition | Queue clearing independent | Exact direct scan finite; aliases/other indirect setters UNKNOWN |

## Result and evidence

**This is supported reaction-state ownership, not an SDK-mode flag.** BehaviorManager crossing from NoneTrigger to a reaction disables external-tag admission and direct drive; crossing back enables both. Current production binds neither writer. A unit-settable ActionList property cannot supply that engine state, and wiring the queue property alone misses direct-drive robot traffic.

Primary evidence: `resources/lib/armeabi-v7a/libcozmoEngine.so` instructions. Reused checked Q17 additions **MS3–MS5** (SetRunningAndResumeInfo), M13/M10 reaction trigger and M8 ActionList rows. Did not re-extract QueueAction dispatch/deletion. Fresh full selected SetRunningAndResumeInfo5A209E..22AE and helper5A2F54..64; narrow constructor50FEFA..FF20 and BehaviorManager constructor prefix5A0864..08AE. Other selected reads cover only publication/gate call-site boundaries, not a whole behavior audit.

## Native owner, writes and timing

| Row | Instruction evidence | Exact behavior |
| --- | --- | --- |
| E1 | 50FEFA movr1,0;50FF18 strb r1,[Robot+2C7] | Robot constructor initializes external-actions-disabled byte0. This is not an ActionList field |
| E2 | 5A0894..08A2,5A08AE | BehaviorManager allocates zeroed0x14-byte running/resume info, sets type byte+10 to **16h=22 NoneTrigger**, stores at manager+1C |
| E3 | 5A209E..20B6 | Read old type from [manager+1C]+10 and new info+10. If both NoneTrigger skip transition broadcast and flag writes |
| E4 | 5A20B8..D6 | Otherwise emit ReactionTriggerTransition(old,new) through native external interface **before flag mutation and before new running/resume info is stored** |
| E5 | 5A20EC..2100 | Old None→new non-None sets r1=1. Old non-None→new None sets r1=0. Both non-None skip flag writes, even if reaction identity changes |
| E6 | 5A2106..2110 | Write **MovementComponent at robot+254, byte+D4 first**, then robot+2C7, same input byte |
| E7 | 5A2270..22AA | After existing light/ownership side effects, replace current pair, resume pair, then type byte. Gate is already updated at this point |
| E8 | 5A2F54..64 | Named UpdateRobotPropertiesForReaction(bool) writes MC+D4 at5A2F5A then Robot+2C7 at5A2F60. No extra policy/SDK test/queue cancellation in helper |

Correct transition table:

| Old type | New type | Flag write | Native transition event |
| --- | --- | --- | --- |
| None22 | None22 | None | None |
| None22 | Any non22 | Set both1 | old,new |
| Any non22 | None22 | Set both0 | old,new |
| Any non22 | Any non22 | None | old,new, including unchanged non22 value |

Native tests22, not a special SDK flag or behavior class. Shipped `unity/scripts/csharp/Anki.Cozmo/ReactionTrigger.cs` names Count21/NoneTrigger22. Treat malformed or unsupported trigger IDs separately; no general valid-input expansion is proposed.

**Ordering consequence:** SwitchToBehaviorBase calls new behavior.Init before SetRunningAndResumeInfo at5A1F12 (existing checked switch ordering). New Init therefore sees the **old gate**. The new gate applies after Init, before new running info publication and later ActionList.Update. Setting it at “reaction requested” or before Init changes admission. Transition event/local callbacks also see old gate until E6. The old helper's name is not an instruction to call it on every behavior tick.

Finite direct byte-offset scan found stores Robot+2C7 at50FF18,5A2110,5A2F60. No immediate branch directly to helper5A2F54 found in that scan; its behavior is inlined into the actual SetRunningAndResumeInfo caller. These are positive direct writers, not proof that no adjusted pointer, bulk store, indirect/export caller or unindexed fragment can write it. **UNKNOWN:** complete interprocedural alias/indirect writer closure. No arbitrary “only writer” claim.

The gate is not reset by ActionList.Clear/Cancel, scope disposal, command completion, contacts changes or a stopwatch in these checked bodies. Its named reset is reaction→None or construction of a new Robot. Those are not universal negative claims about every unread lifecycle caller; any additional reset requires a source address.

## Supported lifecycle / input paths

The actual supported producer is BehaviorManager.SetRunningAndResumeInfo after ordinary reaction switching. Checked SwitchToReactionTrigger5A25E4..26DA evaluates a reaction strategy and invokes normal SwitchToBehaviorBase; cliff/pickup/etc are supported physical reactions. Existing M8-012 owns switching/finishing/resuming, and MS3 owns this publication phase. There is no IsInSdkMode branch in the writer or gate. A supported return/resume/default transition with NoneTrigger clears both bytes; its selection uses existing manager rules, not a fabricated host “enable actions” call.

Queue consumer53D94C loads **incoming runner's Robot+4**, then its byte2C7 at53D94E. It is not reading ActionList's clearing lock-out at list+C. With nonzero flag, checked external-tag intervals are unsigned **[1,1,000,000]** and **[2,000,001,3,000,000]**. Source gate at53D954..70; the preexisting OV18 checked deletion/snapshot/notification path applies. Incoming internal tags are not refused by this flag; other rejection gates, tracks and BadTag remain independent.

Positive ordinary-app witness (authority2, not source substitution):
- `unity/scripts/csharp/Robot.cs`:144 initializes _NextIdTag=1;586..597 GetNextIdTag returns1..1,000,000 cyclically.
- SendQueueSingleAction799..807 supplies that tag into G2E QueueSingleAction and registers its completion callback. Thus the first rejected range is used by ordinary official-app robot actions, not exclusively unsupported SDK mode.
- Second range is still the checked native range; **UNKNOWN here:** complete app/SDK provenance and supported witness for that range. Keep exact intervals rather than removing it by presumed SDK naming.

Current C# ActionRunner counter is seeded **3,000,001** (`Actions/IActionRunner.cs`:93..115, source C1); constructor assigns that to current/original tag. Most constructed drive/animation/head/lift runners therefore keep an internal tag and should remain permitted by external-admission gating. Explicit SetTag can place an action into a native external interval, subject to existing validation. **UNKNOWN:** complete supported public C# API→tag assignment census. Do not silently retag every host API call as external, or exempt a supported external-tag path simply because stack-created actions usually have internal tags.

Paired MC+D4 is independently relevant to supported host direct-drive methods: existing M4 MA1 direct-drive gates refuse wheels/head/lift/arc/turn-speed commands while disabled, and M10 C7's reaction unlock rule reads it. Current `Motion.cs`:569..578 DirectDriveRefused implements the check. No requirement to be in SDK mode there. The flag producer cannot be discarded just because a particular queued internal action bypasses2C7.

## Current C# comparison

- `Actions/ActionList.cs`:76 ExternalActionsDisabled public settable property;167 consumes it. No production assignment found by source search. Its comment naming helper as sole native writer misses the **actual inlined5A2110** store; gate behavior is supported, ownership still missing.
- `Behavior/BehaviorManager.cs`:764..792 SetRunningAndResumeInfo stores _current/_resume/_currentReaction first, then when oldTrigger!=new invokes optional ReactionTriggerTransitionSender and optional UpdateRobotPropertiesForReaction(new!=null). No production binding to the latter found; emits MISSING. Source E4/E6 precedes state publication, and non-None→non-None has no flag write even when identity changes.
- Current optional call also lives only inside oldTrigger!=new. Native sends transition for both non-None even when identical; current sender misses that case. This is existing MS3 publication semantics, not new queue dispatch research.
- `Motion.cs`:155 _directDriveDisabled false/default,273 exposes read-only internal property, comments say no writer. Real DirectDriveRefused uses it, so missing producer affects supported robot commands as well as queue admission.
- `Behavior/BehaviorManager.cs`:365 reaction unlock rule reads Motion.DirectDriveDisabled. Both consumers need the same reaction transition input, not independent public toggles.
- Existing `ActionListTests.cs`:216..244 and M8ActionCheckedRowsTests65/86 set the property directly; prove rejection intervals/diagnostics/deletion, not source lifecycle production.

No SDK-only writer was positively established for these named stores. The SDK exclusion therefore does not remove this supported producer. Full indirect reach remains UNKNOWN, not a new policy decision.

## Bounded build recommendation / regression oracles

After manager checking: supply robot-owned admission byte and Movement direct-drive byte at the existing reaction transition owner. Reuse MS3 crossing table and **event→Movement→Robot→new running info** order. Bind incoming action's own robot to admission consumer. Keep queue teardown lock-out separate. Do not add a timeout/reset-on-completion or a new public C# exclusion.

Independent regression expectations (no tests added/run):
1. Real supported reaction transition None→reaction sets both flags after new Init; action queued by Init uses old gate, later external-tag admission uses new gate.
2. Transition reaction→None clears both before new info is visible; transitioning reactionA→reactionB preserves both existing bytes and still sends transition.
3. Unchanged non-None info sends transition but does not rewrite flags; None→None does neither. Callback observation verifies old gate/current info before publication.
4. Both interval boundaries plus adjacent tags:1/1,000,000/2,000,001/3,000,000 rejected when flag1;0/1,000,001/2,000,000/3,000,001 allowed by this gate. Queue's other outcomes remain independently checked.
5. Rejected pending action takes real checked deletion/completion/watcher path, not silent task cancellation; internal reaction runner remains admissible.
6. Paired direct-drive command fixture verifies MC+D4 refusal while reaction flag set and source permit after None, retaining track-lock conditions.
7. ActionList.Clear/Cancel unit fixture must not be used as the native gate reset producer. Test construction/reset with actual Robot/manager lifecycle, not public property assignments.

Remaining UNKNOWN: complete indirect/alias writer census; full public C# API tag-mapping census; exact second external interval's app/SDK witness; related transition sender/light owners outside the checked writer scope. No whole behavior audit or fidelity settlement.

## Current manifest snapshots

Exact current titles/status/evidence retained. M8-012 is the actual supported manager owner even though its present evidence omits this gate's detailed producer; M8-015 owns the queue consumer and deletion.

### M10-008

```json
{
  "title": "Resume after a reaction: SwitchToReactionTrigger parking, the track unlock rule, head/lift restore from SetDefaultHeadAndLiftState",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "C7 0x5A3610..0x5A3682",
    "C9 0x5A25E4..0x5A26DA; C10 flags 0x60F904",
    "C11 0x5A2BB8..0x5A2C3A, 0x5A1BCC..0x5A1D26",
    "R-ANIM pre-extraction part 2 item 4.1..4.8: ctor 0x5A0864..0x5A0A6C, SetDefaultHeadAndLiftState 0x5A1B40, TryToResume 0x5A2B40, MoveLiftToHeightAction 0x54899C"
  ]
}
```

### M8-012

```json
{
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
