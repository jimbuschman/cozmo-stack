# R-M8-FEED-SURVIVOR — FeedingEat cancellation candidate

Request: `requests/20261021-m8-feeding-clear-survivor.md`. Examined HEAD **c334871d**. Independent native extraction/read-only production comparison, pending checking. Coverage was opened before tracing.

**Bounded result:** there is a concrete native **pending state-2** window in which its result-insensitive completion creates a new, unstarted eating action after Cancel's saved successor is already the sentinel. The queue/lifetime slices below leave that action for Clear; they do not prove every live robot disconnect reaches those prerequisites. A current state-2 action gives a different result: its replacement is caught by the pending pass. Full shipped animation/controller prerequisite satisfaction and the RCM-specific pre-destructor recipients remain explicit UNKNOWNs, not an unconditional hardware-survivor claim or a closure of M8-024.

| Step | Native instructions / prerequisite | Current production reach | Record | Disposition |
| --- | --- | --- | --- | --- |
| State2 producer | Driving callback SUCCESS5D6A7C..80→TransitionToPlacingLift5D5FA4..6030; state=2, constructs triggerAF/B0, member callback TransitionToEating. | `Behavior/BehaviorContainer.cs`:222 lists FeedingEat but no constructor in built Constructors map; class returns NotImplemented at310–338. | M15-065 | Native producer recovered/adopted OV111; concrete C# FeedingEat UNBUILT. |
| Tag/admission | StartActing gates5BDAD4..DAF4; assigns callback+88 before action tag+84 at5BDB46..50, QueueAction(position0, retries0)5BDB60..6C. | `Behavior/SteppedBehavior.Actions.cs`:15–57 implements owned adapter but visibly lacks native completion subscription/lifetime owner. | M8-015/024 | Tag/order checked; actual FeedingEat callback not bound. |
| Pending window | QueueNow uses key0 (53DCB4..DCD8), stores a pending list entry; Queue.Update53F6D0 deletes completed drive and returns53F704, without starting the newly queued placing action. | `Actions/ActionQueue.cs`:74–94 same one-update mechanics; not a FeedingEat integration witness. | M8-015/024 | Source-backed pending candidate. Need actual feeding state/listener/animation gates below, not a synthetic callback. |
| AbortAll / position | Robot~511120→AbortAll51194C; ActionList.Cancel(-1)51195A; Queue.Cancel current deletion53E6B6 before pending.front53E6C0; pending direct delete53E6E0. | `RobotLifetime.cs`:60–65; actual action queue code. | M8-015/024 | Current vs pending branches differ. Pending retains NOT_STARTED, not CANCELLED. |
| Saved successor / callback | DeleteActionAndIter erases pending node, writes saved successor53FA86..9C **before** local5A53FAB2. HandleActionComplete5BE1EC..224 clears matching+84, then running+callback check; member wrapper5BF8F4 ignores result. | `ActionQueue.cs`:250–264 unlink-before-send; `SteppedBehavior.Actions.cs`:100–109 tag/run/callback body, missing subscription. | M8-024/M15-065 | When P was final pending node, saved successor=S; replacement appended later is not visited. |
| Eating requeue | TransitionToEating5D60EC..6350: state3, disable reactions, EnableStopOnCliff0, choose/lookup clip, listener StartedEating, deadline; new PlayAnimationAction empty callback5D630C..633A. | No actual FeedingEat implementation; `CubeDisconnectUnits.cs`:86–163 is a checked partial ActivityFeeding disconnect unit, not this lifecycle. | M15-065/061 | Recoverable input-specific branch. Missing animation skips requeue; null clip/controller is M1-034, not positive survival. |
| Queue key / replacement | StartActing position0→default map key0; QueueNow53E34E..36E tail→8CB48C→PLT4AA8F4→QueueAtEnd53E16C; appends new E after old P removed. | `ActionList.cs`/`ActionQueue.cs` mechanics built. | M8-015/024 | No new parallel slot or native map clone required for this candidate. |
| Owner teardown | ~Robot nulls manager44 then dtor51112C; nulls BSM48 then dtor51113A; Clear511146. Manager5A0D22..DF6, container59C704..730, Feeding activity5AACBC..AD94 and FeedingEat5D66D4..6748 release ownership, not StopActing. | `RobotLifetime.cs`:20–21,63–64 explicitly marks full owners unbound; host Freeplay cleanup separate. | M8-016/023/024 | Concrete release route read with adopted OV19/OV55 dependencies; no owner Stop/delete of E established on this path. Unrelated callback census stays open. |
| Clear / effects | List clearing53D928; Queue.Clear53F96A..9B0 deletes pending without Cancel; E remains NOT_STARTED; base watcher enqueue then optional local5A. | `ActionQueue.cs`:43–60; typed payload and full owner lifetime still missing. | M8-015/024; M8-020 destructor effects | Candidate gets deleted at Clear on stated branch; no E Init/keyframe playback before that. No watcher drain at Clear. |
| Pre-destructor reach | Context~4EAABE→RemoveRobots52F428..472 directly calls~Robot52F43C. Individual RemoveRobot52F2AC/CC has earlier external callouts. | Host shutdown/RCM native-owner equivalence not proved by simple adapter tests. | M8-024 / M1 lifetime owners | Context removal provides a direct entry boundary; RCM-prefix recipient closure remains UNKNOWN. |

## Authority and scope

Read current M8-024/M2-022, M15-065, M8-016/023 and source first. Reused adopted `overnight/20261016-ov111-112-answer.md` F9/F10/F12/F13/F14–F20/F27; `20261016-m2-022-answer.md` synchronous local5A and teardown order; OV18/OV56 Clear/Cancel mechanics; `overnight/20261014-ov19-m8-016-check.md` and `overnight/20261015-ov53-55-check.md` owner/destructor/capture rows. `20261021-m8-actionlist-completion-settling.md` B5 is a pending lead, not an adopted survival finding.

Navigation used `re-analysis/decomp/libcozmoEngine/index.tsv`; actual citations are Thumb instructions in `resources/lib/armeabi-v7a/libcozmoEngine.so`, with PLT/GOT/ARM veneers resolved. Read the bounded producer/callback/queue/destructor ranges through their normal returns and the relevant cleanup. Didn't expand the complete behavior census, animation payload, streamer or all activity algorithms. No code/tests/manifest changes, hardware work or settlement.

## A native candidate with an actual pending position

Use symbolic tags **D** (driving), **P** (placing) and **E** (eating), not invented fixed native tag values. Require the ordinary allocator state where newly allocated tags are distinct (no forced tag collision/wrap); the allocator/init/setter remains the existing runner owner. These all use the **default ActionList map key0**, because IBehavior.StartActing calls QueueAction with position0/NOW and retries0, and QueueActionNow builds/uses key0 at53DCB4..DCD8. No parallel-queue assumption is needed.

1. FeedingEat is running (+A1=1), not in the +A0 refusal state and not blocked by a different acting tag. Its target cube is valid/located, ActivityFeeding's controller for that target is still present, and listener construction/registration is the existing adopted path. DrivingToFood actionD is current on key0, with no trailing pending action. This is the ordinary adopted F7/F9 path; a real SUCCESS is needed, not CANCELLED (F10 cancels mark the cube bad).
2. During ActionQueue.Update, D returns SUCCESS. DeleteActionAndIter Preps/snapshots/deletes/unlinks D and synchronously sends its completion. IBehavior's matching-tag handler clears+84, slot80(false), then invokes its callback only while running (5BE1F8..220). Driving result lambda5D6A62..AAA sees SUCCESS at5D6A7C and calls TransitionToPlacingLiftOnCube via the veneer at5D6A80.
3. Transition sets state2/+12C (5D5FC2..CC), selects trigger0xAF or0xB0 from Energy critical state, constructs TriggerAnimationAction with loops1, interrupttrue, tag argument0, timeout60.0f, strictfalse (5D5FDE..601A), and submits P via the member-callback wrapper (5D602A→5D6068→5BE0E4). This callback ignores ActionResult and calls TransitionToEating.
4. P is **queued pending**, not initialized inline. The existing queue's current pointer was nulled before D's local report. QueueNow has no current to cancel; its final tail53E36E resolves to **QueueAtEnd**, not QueueAtFront or direct assignment to current. QueueAtEnd appends a list node and stores retries0, returning0 (53E176..194). Back in the outer queue.Update, after its original deletion call53F6D0 it evaluates the saved resultD and returns at53F704..708. There is no branch back to promote P. Only the next queue tick could take it at53F604..60C.
5. Thus between that completed tick and the next one, **state2, +84=P, current=null, pending=[P], P.state=NOT_STARTED0x02000001** is a source-backed window. It does not require a cube disconnect or a fabricated completion callback. A robot/context removal before the next queue.Update may enter~Robot here. This is the candidate the earlier requeue-only lead lacked.

State2 after P has become current is a separate case below. Additional pending entries, duplicate tags, a prior Stop, off-treads transition, bad animation or missing controller can change this witness; none is silently assumed absent in every run.

## AbortAll and the saved successor

~Robot calls AbortAll before either behavior owner is destroyed (51111E..51113A). AbortAll starts ActionList.Cancel(-1) at511952..95A. List.Cancel visits signed map keys, invoking Queue.Cancel once for key0 (53DE20..54); it does not re-enter key0 after moving to its successor. List.Clear's guard is stillfalse during AbortAll.

For the candidate current=null, Queue.Cancel skips current cancellation and loads pending.front at53E6C0. It passes P's list payload to DeleteActionAndIter at53E6E0. **It does not call P.Cancel** in that pending loop. P's result therefore stays NOT_STARTED, and the normal external-interface/INTERRUPTED gate permits a game/local report (NOT_STARTED is not INTERRUPTED0x03000009).

DeleteActionAndIter:

- snapshots P before destructor, performs derived/base destructor and watcher enqueue;
- sets the caller action pointer0 at53FA7C;
- unlinks/frees P's node, decrements pending count and writes the saved **next node** to caller iterator at53FA86..9C;
- only then sends local RobotCompletedAction through external slot+1C at53FAB2.

Because P was the **last** pending entry, the saved successor is the sentinelS. During the synchronous local emit IBehavior matches P, clears+84 and invokes the member callback. The wrapper5BF8F4 loads the robot from the captured behavior and invokes the Robot& callable; result is not inspected. Therefore **NOT_STARTED triggers TransitionToEating too**. Describing this pending case as CANCELLED is wrong; result-insensitivity makes both cases enter the transition.

On the normal eating branch, a new E is created and IBehavior.StartActing replaces+88 with an **empty callback**, writes+84=E, calls slot80(true), and queues NOW on key0. The old P guard remains in DeleteActionAndIter's guard set until53FABE, but E has a distinct tag. QueueNow sees current=null and now an empty pending list (P was already unlinked), and appends E via QueueAtEnd53E16C. It doesn't update the outer Cancel's stack iterator, which is alreadyS. On return,53E6E8 reloadsS and53E6EC/53E6EE terminates the pending pass. List.Cancel moves past key0, never revisiting E. **After this ActionList.Cancel, pending=[E], current=null, E.state=NOT_STARTED.**

This establishes actual tag/slot/position/successor for one shipped callback body, rather than using a synthetic callback that arbitrarily inserts a survivor.

### Countercase: P was current

If P already started, current Cancel occurs at53E6A8 and its deletion/broadcast at53E6B6 **before** pending.front is loaded53E6C0. That completion may invoke the same transition and append E, but the subsequent pending scan sees E and directly deletes it. E's installed callback is empty, so it produces no further FeedingEat requeue. If P's current state is NOT_STARTED (just promoted), Cancel preserves that result; if started it becomes CANCELLED0x02000000; neither changes the result-insensitive transition. **This current-action branch alone does not prove a survivor at Clear.**

## Transition and required normal inputs

Fresh reads5D60EC..637A confirm adopted F14–F20:

1. Set behavior state3 and transition label, then SmartDisableReactionsWithLock using its name/table (5D611E..6144).
2. Send **EnableStopOnCliff{0}**, reliabletrue, hotfalse (5D6148..6178), before checking for a usable eating animation.
3. Choose trigger from needs/predicted Feed outcome (adopted F16). HasAnimationForTrigger must return1 (5D6196/19C). If not, return without listener, deadline or action allocation; there is **no E survivor** on that branch.
4. Obtain group/animation name and canned Animation (5D61A0..61CE). Missing lookup is unchecked at GetTrack/load5D61D6 and is the known M1-034/M15-069 case. No positive survivor proof uses that fault. Nonempty event track supplies duration=f32(last event trigger ms)/1000; empty track logs/error-flag and uses0 (5D61D8..62A6). Duration0 is still a defined requeue path.
5. Invoke StartedEating on listeners (5D62B4..62F4). The concrete ActivityFeeding listener at5AC8F0..C930 uses map[selected target] and SetControllerState(DrainCube=1,duration), increments cubes eaten; it doesn't Stop FeedingEat. Controller state1 at5A619C..61B2 stamps start/duration and state; it does not immediately run its later UpdateCubeDrain/light update. Missing controller is the known5A610C fault, not proof of survival; require present controller.
6. Store deadline duration+now (5D62F6..6308), allocate PlayAnimationAction with chosen name, loops1, interrupttrue, tag argument0, timeout60, then StartActing with empty callback at5D6330..633A. This is E, still uninitialized and not yet streaming.

Native source establishes the branch and its prerequisites. This answer did **not** extract shipped animation assets from Unity binary bundles to prove that every possible needs/RNG choice yields a valid canned name/event track/controller at the chosen shutdown instant. Exact selected clip, duration and RNG branch for an actual run remain **UNKNOWN**, dependent on those existing asset/needs/controller owners. It is not a claim that every state2 AbortAll requeues, nor permission to invent a test clip and call it a shipped witness. The real group/clip data is a required regression fixture/input.

## Owner teardown does not stop this pending action

The already adopted OV19/OV55 rows and fresh selected bodies establish the release route:

- ~Robot clears robot+44 before BehaviorManager dtor51112C, then clears+48 before BSM dtor51113A; neither is a call to IBehavior.Stop.
- BehaviorManager5A0D22..DF6 destroys reactions/bindings, releases behavior map references, BehaviorContainer, current/request/activity references and activity maps. BehaviorContainer59C704..730 destroys its BehaviorID shared-pointer tree; leaf59EBA0..BCA releases control blocks. It does not call Queue.Cancel or StopActing in these bodies.
- ActivityFeeding holds the manager's FeedingEat shared reference at+E4 and in its stage13 map (adopted OV103 C1/C2). Dtor5AACBC..AD94 destroys stage map, behavior shared references (including+E8 control block), chooser, subscription handles, ignore set and controller map, then IActivity. It does **not** call OnDeselected/SetControllerState/FeedingEat.Stop. Listener vector uses raw IFeedingListener*; destroying the activity does not cause another StartedEating call.
- FeedingEat's dtor5D66D4..6706 / deleting body5D6710..6748 releases bad-cube map, retained movement listener control and raw listener-vector storage, then IBehavior. No StopInternal/StopActing/EnableStopOnCliff{1} in those dtor bodies. IBehavior5BC75C..C81E destroys callback storage, WantsToRun strategy and subscription handle vector; it does not use the retained acting tag to cancel an action. Existing adopted OV55 closes ordinary strategy/chooser/capture destruction rows; don't reopen all unrelated behavior graphs here.
- BSM+0C activity is null on its live path per adopted OV19 (its InitConfiguration/Update/UpdateActiveBehavior have no caller). Its container is a **second behavior map**, not the manager's actively running FeedingEat instance; its destructor does not cancel E. No new SDK exclusion follows from that dead BSM activity path.

Thus this E is ActionList-owned after enqueue, not an owned child action automatically deleted by FeedingEat's wrapper destruction. Releasing the owners is not Stop. If another shared owner kept FeedingEat alive, that fact by itself still would not cancel E; E has an empty callback, so a matching later completion would clear tag/acting state and not run TransitionToEating again. Exact refcount/last-owner timing for all unrelated captures is not a universal claim here. The selected release path has no candidate cancellation, and no body read here supplies one.

AbortAll still continues after list.Cancel with Path.Abort, Docking.AbortDocking, SendAbortAnimation and StopAllMotors (51195E..978); it does not run ActionList.Update or set FeedingEat+A1 in its own body. Reuse adopted M13-029/OV09 planner cancellation/pose cleanup and existing docking/abort rows; this request doesn't re-extract their whole subsystems. Path status setter6492C4..931E logs/stores status rather than sending an action-completion event; the cleanup vector is pose destruction, not an arbitrary application callback. A complete census of other recipients that might independently cancel an action remains the wider M8-024 work, not contradicted by this narrow candidate.

For reach before ~Robot: **context destruction** calls RemoveRobots at4EAABE; RemoveRobots52F428..472 directly calls~Robot at52F43C for each robot. It doesn't run IndividualRemoveRobot's earlier external interface+30 / RobotDisconnected broadcast first. This provides a source entry route that doesn't assume an untraced disconnect handler preserved+A1. For an actual **RCM disconnect**, RemoveRobot52F29E..2CC performs those earlier external callouts before~Robot52F2F6; their complete effect on this candidate remains **UNKNOWN here**. Do not relabel the context/entry-conditional witness as a verified physical-disconnect sequence. Existing M2-022 proves removal/Clear is outside the robot-message emit; this is not OV111's same-drain cube-disconnect/falling fault window.

## Clear, observable ordering and limits

With E still pending,~Robot invokes ActionList.Clear511146 after the owner releases. List.Clear marks its guard53D928, destroys queues, and Queue.Clear marks its own guard53F978. Current isnull; it deletes pending E directly53F99E/A0 without Cancel. **E stays NOT_STARTED0x02000001**, not CANCELLED. No queue tick or E.Init occurs between creation and this deletion on the traced teardown call chain. Hence the eating action cannot start an animation stream, acquire its Init robot callbacks, or send its clip's keyframes on this branch.

Known candidate-specific effect order:

1. P Prep/game snapshot, P derived/base teardown and deferred watcher enqueue, then local/app P completion at53FAB2 (NOT_STARTED).
2. Synchronous FeedingEat handler enters state3, disable-reaction state, sends EnableStopOnCliff0, performs animation selection, StartedEating/controller state/timer/counter and deadline, queues E.
3. AbortAll's remaining path/docking/AbortAnimation/motor-stop operations occur; behavior owners then release. No FeedingEat.StopInternal restoration is invented during release.
4. Clear Preps/snapshots/deletes E, watcher enqueues it, unlinks, then optional local/app E completion (NOT_STARTED). There is no FeedingEat nonempty callback to requeue again.

Play/Trigger action destructor streamer effects are **conditional** and already owned by M8-020/M5-008/023: at543DC2..DC8 it compares robot's current stream pointer against cached Animation+90, and on equality calls SetStreamingAnimation(null,1,true,false)543E26. An uninitialized P/E has null cached pointer; null==null can therefore run its existing Abort path. A nonnull current idle does not satisfy that condition. Don't omit this tail merely because the queued action never Init'd, but don't assert an unconditional additional AbortAnimation count for every streamer state. The independent completion-payload report owns string variants; no scalar placeholder is blessed here.

Native watcher completions are only enqueued during destruction; there is **no watcher Update/drain** inside AbortAll/Clear. The local5A and app DeliverToGame order are synchronous/queued as adopted M2-022 and later delivery fixes establish. Host queued API delivery on final shutdown has its already named limitations; this research does not infer delivery after the host has destroyed the dispatcher. Scope above is engine emission/order, not a hardware receipt guarantee on a dropped connection.

## Current production and a regression that would prove the join

Current ActionList/Queue implements the saved-successor and unlink-before-send mechanism, but no configured FeedingEat is constructed by BehaviorContainer (entry present, implementation factory absent). The owned SteppedBehavior.StartActing adapter itself reports missing native completion subscription/lifetime ownership; HandleActionComplete must not be wired to deferred watcher callbacks as a substitute for local5A. RobotLifetime63–64 names manager/BSM finalization unbound. Partial FeedingDisconnectUnit/FeedingCubeControllerUnit cover separate checked disconnect/controller units; they are not a complete running FeedingEat/ActivityFeeding. Therefore current tests with a scripted requeue establish queue mechanics only, not this native production witness.

Independent future regression after those producers are built:

1. Use actual feeding activity/behavior, a located connected charged cube and **shipped** needs/trigger/group/clip data. Complete the driving action through its real action result, and run exactly that queue tick. Assert state2, matching P tag, default key0, currentnull, sole pendingP and P NOT_STARTED. Don't manually place an arbitrary action/callback there.
2. Initiate the supported context/robot destruction boundary before the next ActionList.Update. Observe local P report and transition; assert E exists after list.Cancel with the actual chosen name/tag and empty callback. Log source conditions/controller/needs/streamer state so the expected branch is reviewable. An absent clip/controller must select the explicit failure/M1-034 case, not quietly use a synthetic successful fallback.
3. Run the real owner release sequence, assert it doesn't perform StopActing on E; Clear deletes E and emits its NOT_STARTED report after owner teardown. Assert no E.Init/keyframe output, EnableStopOnCliff0 precedes AbortAll's remaining sends, no invented StopInternal cliff1 restoration, and conditional destructor Abort tail matches captured streamer identity. Assert watcher records weren't delivered by Clear.
4. Countercase: give P its next real queue tick so it becomes current, then destroy. Its callback-created E must be consumed by the ensuing pending pass in the same Cancel; it must not be counted as a survivor-at-Clear. Test extra pending nodes separately because successor location changes the outcome.

These are source-derived expectations, not implementation-shaped tests run in this research. A native capture/emulation fixture or independent checked row should supply the concrete chosen clip/duration/message bytes when available; passing a synthetic queue test cannot upgrade provenance.

## Disposition and remaining uncertainty

Keep M8-024 RECOVERABLE_GAP: one concrete **pending** mechanism is now traced through producer/tag/default key, saved successor, replacement, selected owner release and Clear, with normal-input and prefix limits stated. No absence of other survivors, no broad manager/destructor settlement and no native-container replication requirement follows. M15-065/061 own concrete feeding/listener/asset inputs; M8-015 mechanics; M8-016/023 owners/captures; M8-020 streamer teardown; M1/delivery owners emission and host shutdown behavior.

Remaining **UNKNOWN**: exact shipped animation/needs/RNG/controller combination at an actual removal; full RCM pre~Robot external+30/RobotDisconnected recipient effect; all other local5A recipients/other behavior survivors and retained capture graphs; final host shutdown delivery beyond engine emission. The current C# full feeding/owner joins are explicitly unbuilt. These unknowns prevent a universal disconnect-survivor claim, not recording the fully addressed conditional queue path. No policy decision, implementation, tests, manifest/inventory edit, approval or settlement was made. Only this named answer is committed/pushed under the queued research rule.

## Current record snapshot

Verbatim title/status/evidence at examined HEAD. Historical unresolved prose is not silently rewritten; notably prior cancellation-survivor wording alone isn't proof that this pending action's result is CANCELLED.

```json
{
  "id": "M2-022",
  "title": "Local E2G subscribers to AnimationEvent (tag 0x60) and FirmwareUpdateComplete (tag 0x81), and whether a robot-message dispatch can run nested inside ActionList::Clear",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261013-dispatch-residue-check.md (and re-analysis/research/20261013-dispatch-residue.md): \"Unread\" section: the handler bodies; the re-Init question; 0x004EE94A -> PLT 0x004A544C; ~Robot r5 at 0x0051120E; ProcessMessages with no robot 0x0069D8D8.",
    "re-analysis/research/20261013-small-reads-2-check.md: \"Still unread\" section. Read parts split to M2-023. Remaining: the vtable+0x28 Subscribe sites, rodata tag arrays, StrategyGeneric::ConfigureRelevantEvents 0x006137B6/0x0061389A, ReactionTriggerStrategyGeneric 0x0060F5D8; ActionList::Clear 0x0053D916..0x0053D93A with action dtors 0x00541084, 0x00543DAC.",
    "re-analysis/research/20261016-m2-022-answer.md: E2G 0x60 AnimationEvent ctor 0x725F7C..0x725F86; Broadcast&& DeliverToGame then local emit 0x6625BA..0x6625FA; map writer only Subscribe 0x6626F2; census of 48 E2G callback types (no 0x60 subscriber); robot dispatch single entry ProcessMessages (slot 0x10310E4) <- UpdateRobotConnection 0x52F850; ActionList::Clear 0x53D91A..0x53D938 (guard +0xC), ActionQueue::Clear 0x53F970..0x53F9A0 (guard +0x1C); ~Robot via RCM::Update 0x62FAF8 before the PopData loop."
  ]
}
```

```json
{
  "id": "M8-016",
  "title": "Behavior-container destruction and virtual children",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0051112C; 0x0051113A; 0x005A0D22..0x005A0DF6; 0x005A5886..0x005A58D2; BehaviorManager/BehaviorSystem shared/container destruction; virtual child call0x005A58AA and recursive effects UNKNOWN.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "re-analysis/research/overnight/20261014-ov19-m8-016-check.md: build rows (~Robot slots 0x00511124..0x00511142; BSM ctor 0x005A57E6..0x005A5832 and dtor 0x005A5886..0x005A58D2; BehaviorManager dtor 0x005A0D22..0x005A0DF6; container 0x0059C704..0x0059C730; trees 0x005A479C..0x005A486A; IActivity 0x005B2F8C..0x005B2FEC; Freeplay hash 0x005AED50..0x005AEDBC; IBehavior 0x005BC75C..0x005BC81E; IWantsToRun 0x006134C4, 0x00613C1C)."
  ]
}
```

```json
{
  "id": "M8-023",
  "title": "Behavior teardown recursive recipients not yet read: IActivity unique children and their strategy/chooser types, ActivityStrategy derived bodies, std::function captures, derived behaviour children, reaction-strategy bodies other than Hiccup, other ProtoHandle instantiations",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov19-m8-016-check.md: \"Still unread\" list: 0x005B2FB8/0x005B2FC4/0x005B2FD2 (producer 0x005B2A5C..0x005B2F8B); 0x005B419C, 0x005B456C, 0x005B4818, 0x005B4CCC, 0x005B4E50, 0x005B4E78, 0x005B4EB0, 0x005B528C, 0x005B4806; 0x005A7936, 0x005A7942, 0x005AAD6E, 0x005B14D2; 0x005BC7C4, 0x00613C40, 0x00613C58, 0x00613C7A; 0x005DC506, 0x005D7B7A, 0x00604322, 0x005D36B6; 0x0060C3C4, 0x0060D0E4, 0x0060EA30, 0x0060F880, 0x00611708, 0x00611FD4, 0x0060B722.",
    "re-analysis/research/overnight/20261015-ov53-55-check.md: IActivity dtor 0x005B2FAC..0x005B2FD2; ReadConfig 0x005B2D7E..0x005B2E34 (+0x24 producer 0x005B2D7E..0x005B2DC6, +0x28 0x005B2DC8..0x005B2E14); chooser factory 0x00609C88 (D1 0x0060A238, 0x0060AFA8, 0x0060B1F8); strategy factory tbb 0x005B3BE2, dtors 0x005B419C..0x005B5258; WantsToRun factory tbb 0x00614726 (also IBehavior::ReadFromJson 0x005BC24C -> +0x38, deleted 0x005BC7F0); BuildPyramid/Feeding/Sparked 0x005A7894, 0x005AACBC, 0x005B1484; Block_Cube1x1 0x004E772C, 0x004E6F80, EraseVisualization 0x004E6932..0x004E6936; FactoryTest 0x005D3692, 0x005D36B6, CloseLog 0x006717B8..0x00671848; reactions 0x0060B6F4..0x00611FD4; closures: StrategyGeneric function slots (writers 0x0060F4C6, 0x006137C0, 0x006138A4, 0x0061422C; destroys bx lr), BuildPyramidBase +0x140 (0x005DBCA2..0x005DBCB2, 0x005DC600), FactoryTest +0x180 (0x005D04E2, offset scan)."
  ]
}
```

```json
{
  "id": "M8-024",
  "title": "Pending actions surviving Cancel(-1) through ~Robot: which shipped completion callbacks re-queue after CANCELLED, IBehavior +0xA0..+0xA2 at teardown, other tag-0x5A subscribers, and whether ~BehaviorManager/~BSM queue actions",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov18-m8-015-check.md: mechanisms 0x0053FA86..0x0053FAB2, 0x0053E6E8..0x0053E6EE, 0x0053E2DA..0x0053E2F2, 0x0053DF48..0x0053DF8A; callback PLTs 0x004B01C8, 0x004B2184, 0x004B219C, 0x004B297C, 0x004B2B5C, 0x004B34B0; tag-0x5A subscribers 0x004F0742, 0x005277B0, 0x005E7B5E; ~BehaviorManager PLT 0x004A7978, ~BSM PLT 0x004A796C."
  ]
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
  ]
}
```

```json
{
  "id": "M15-065",
  "title": "BehaviorFeedingEat: the IFeedingListener callbacks (StartedEating, EatingComplete, EatingInterrupted) and the conditions under which they fire",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261016-ov102-104-answer.md: callers 0x005D5C5A..0x005D5C70 (interrupted), 0x005D5E1C..0x005D5E6C (StopInternal: interrupted if state >= 2, complete if now > +0x124), 0x005D62B4 (StartedEating); listener push_back 0x005D6754 into +0x130; body range 0x005D5904..0x005D64C6.",
    "re-analysis/research/overnight/20261016-ov111-112-answer.md: class 0x005D5794..0x005D6AD6 (vtable 0x010283B8): ctor 0x005D5794..0x005D5816; IsRunnable 0x005D5880..0x005D58CE; IsCubeBad 0x005D58D0..0x005D5900; Init 0x005D5904..0x005D5A02; DrivingToFood 0x005D5A58..0x005D5B36 and result 0x005D6A62..0x005D6AAA; PlacingLiftOnCube 0x005D5FA4..0x005D6030; Eating 0x005D60EC..0x005D6350 (StartedEating call 0x005D62E2); anim choice 0x005D64C8..0x005D65A4; Update 0x005D5B92..0x005D5BF8; ReactingToInterruption 0x005D5C3C..0x005D5D10; CubeMovementHandler 0x005D5D7C..0x005D5DE4; StopInternal 0x005D5DE8..0x005D5EF2; MarkCubeAsBad 0x005D662C..0x005D6688; member-callback wrapper 0x005BF8F4."
  ]
}
```
