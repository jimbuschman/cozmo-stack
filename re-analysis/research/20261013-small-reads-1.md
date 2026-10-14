# R-SMALL-1 — RobotDelocalized subscriptions and root-parent reads

## Item 1 coverage

| Step/question | Result | Primary citation |
| --- | --- | --- |
| BlockConfigurationManager registration | E2G 0x32, immediate subscription in its constructor | 0x00616B58..0x00616BB8; 0x00616C44..0x00616C78 |
| CubeLightComponent registration | E2G 0x32, second setup helper in its constructor | 0x006370E8..0x0063716A; 0x00637244..0x00637278 |
| Behaviour registration overlooked by a Get_\<50\> search | CantHandleTallStack subscribes to 0x32 | 0x005ECB5E..0x005ECB80; u16 at 0x00C6EF80; Subscribe at 0x005BC99A |
| Strategy registrations overlooked by that search | CubeMoved; FacePositionUpdated and ObjectPositionUpdated through their PositionUpdate base | 0x0060B810..0x0060B898; 0x006121BC..0x006121D8; 0x0060C610; 0x00611054 |
| Shared strategy subscription | Set iteration passes each u16 to external interface vt+0x28 | 0x0060B4DC..0x0060B4FA |
| Construction/registration order of these six recipients | Block manager → tall-stack behaviour → cube lights → face strategy → object strategy → cube-moved strategy, under the shipped successful initialization described below | 0x0050FC66, 0x0050FCAC, 0x0050FDDE, 0x00510396; reactionTrigger_behavior_map.json |
| Invocation order | Synchronous, forward subscription-list order; game delivery precedes in-engine dispatch | 0x00662508..0x00662572; 0x00662590..0x006625B0; 0x00663C74..0x00663CBA; append at 0x006646A6..0x006646B2 |
| CubeMoved::HandleRobotDelocalized reachability | Not called by its event dispatch; tag 0x32 falls through. The separately emitted method is an empty return | 0x0060BEA8..0x0060BF66; 0x0060C038 |
| Position-update effect | Calls ResetReactionData; that function clears a field of a temporary copy, not the stored map element | 0x00612214..0x00612238; 0x0061226C..0x006122A4 |
| Every possible Subscribe site, including indirect/dynamic tag producers | UNKNOWN as an exhaustive binary-wide negative claim. Six concrete recipients and their relative order are established; do not promote this to a proof that an additional dynamic subscriber is impossible | Search boundary and remaining work below |

## Item 2 coverage

| Step/question | Result | Primary citation |
| --- | --- | --- |
| GetParent root failure | Root's null parent is wrapped, then dereferenced at node+0x48 | 0x00845408..0x0084541A → 0x0084529C..0x008452C0, fault at 0x008452BA |
| FindRoot on a valid root | Safe: wraps the input node itself when its parent is null | 0x00845240..0x0084526E |
| Direct-call census in requested range | 55 direct calls to Pose3d GetParent/FindRoot PLT entries; all listed below | Call addresses in table; PLT 0x004A74D4 / 0x004A40F0 |
| Local guards | Explicit positive guards at history insertion, ToPoseStruct3d, IsSameAs_WithAmbiguity and SanityCheckOwnership; other calls require producer analysis | Individual rows below |
| History insertion as a universal non-root invariant | Disproved: insertion skips its own GetParent when input has no parent, then accepts that input subject to other checks | 0x00530E84..0x00530EB0; 0x005310B4..0x005310D8 |
| Robot::GetPose as a non-root guard | Disproved: checks root identity/equality, not HasParent; a world-origin root can satisfy those checks | 0x004EA398..0x004EA470 |
| Exhaustive list of normal-running root arrivals at unguarded GetParent sites | UNKNOWN. No concrete normal-running root-to-fault witness was established. The unresolved producer paths are named below; absence of a local guard is not a witness | Per-call table and normal-running boundary below |

Answers `requests/20261013-small-reads-1.md`, queue P2. Independent extraction from local ARMv7 `libcozmoEngine.so`, 3.4.0-1204, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Addresses are ELF virtual addresses with Thumb bit removed. Decompilation was used to navigate; the cited control flow, constants, callbacks and parent checks were checked against native instructions. No shipped files were uploaded, no production files or manifests changed, and no hardware run was started.

**This is a partial answer, not a closure of M11-072/M11-073.** It supplies additional recipients, ordering, and a call-site census, and records explicitly the two exhaustive reachability claims it cannot yet prove. This distinction matters: searching only for payload accessors misses subscribers which only inspect the tag, and checking only the immediate callers misses producer invariants.

## 1. The recovered subscription chain

### Direct component callbacks

BlockConfigurationManager's constructor at `0x00616B58` initializes its fields/containers and, when `Robot::HasExternalInterface()` returns 1, obtains the interface and calls `0x00616C44`. The helper writes `0x32` as a halfword at `0x00616C64`, loads vt+0x28 at `0x00616C5C`, and calls that slot at `0x00616C76`. Its captured-this functor address point is `0x0102D9E4`; its invocation slot at `0x0102D9FC` contains Thumb pointer `0x006172A7`. The callback at `0x006172A6` calls the tag-50 accessor at `0x006172AE`, then sets manager byte +0xC to 1.

CubeLightComponent at `0x006370E8` applies the same external-interface gate. Its helpers run `0x00637184`, `0x00637244`, then the later helpers. The first subscribes to tag 0xD; the second supplies 0x32 (`0x0063724C`, halfword store `0x00637264`) and calls vt+0x28 at `0x00637276`. The functor address point is `0x0102EC2C`; invocation slot `0x0102EC44` contains `0x0063AC47`. Callback `0x0063AC46` gets the payload at `0x0063AC4E`, then forwards to `0x0063A390`. That handler sets the component's +0x21 byte and visits its object entries, applying Connected light animation to non-carried objects, as separately extracted in the delocalize-callees report. These accessor callers do not exhaust the subscriber population.

### CantHandleTallStack is also a subscriber

`BehaviorCantHandleTallStack` inserts the halfword at `0x00C6EF80` (bytes `32 00`) into a tag set at `0x005ECB70`, then calls `IBehavior::SubscribeToTags` at `0x005ECB78`. The E2G overload is `0x005BC93C`, not the neighboring G2E overload `0x005BC82C`. It checks external-interface availability, loads each tag from tree-node +0xE, and calls interface vt+0x28 at `0x005BC99A`.

Its functor address point is `0x01026660`; the invocation thunk is `0x005BF23E`. That thunk loads captured this and branches through `0x008CBFDC` to the E2G HandleEvent path `0x005BF268`: AlwaysHandle at vt+0x5C first, then vt+0x68 or +0x74 according to running byte +0xA1. The actual tall-stack vtable is based at `0x01029FD8`, address point `0x01029FE0`. The relocation at `0x0102A03C` resolves AlwaysHandle to `0x005ED23D`; slot `0x0102A070` resolves ClearStack to `0x005ED1E5`.

On tag 0x32, `0x005ED240..0x005ED258` writes zero to behaviour byte +0x124 and dispatches vt+0x90, ClearStack. ClearStack `0x005ED1E4` clears the stored weak-pointer pair at +0x11C/+0x120, releasing its weak owner when present. This is a real omitted recipient, not a speculative consequence of the behaviour name. The ordinary shipped behaviour config is `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/cantHandleTallStack.json`, whose `behaviorClass` and `behaviorID` are both `CantHandleTallStack`.

### The three strategy instances

The CubeMoved constructor inserts five u16 tags from `0x00C759F0`:

`0x000F, 0x0010, 0x0044, 0x0011, 0x0032`.

The native loop at `0x0060B872..0x0060B882` consumes exactly ten bytes. The set deduplicates/sorts them; the E2G wrapper at `0x0060B49C` visits its ordered nodes, so it registers 0xF, 0x10, 0x11, **0x32**, 0x44. Its vt+0x28 call is `0x0060B4FA`. The functor captures the strategy pointer; address point `0x0102CDD0`, invocation slot `0x0102CDE8` contains `0x0060B7E3`. The thunk at `0x0060B7E2` loads captured strategy, loads its robot from +8, and tail-dispatches strategy vt+0x18.

CubeMoved's vtable base `0x0102CE08`, slot `0x0102CE28`, relocates to `0x0060BEA9` (AlwaysHandleInternal). It first requires reaction trigger 8 to be enabled (`0x0060BEB0..0x0060BEBC`), then switches on the event tag. The only handled values are 0xF, 0x10, 0x11 and 0x44. For 0x32, the comparisons at `0x0060BEF2..0x0060BEF8` reach return `0x0060BF62`. There is no call to HandleRobotDelocalized in this route. The named method at `0x0060C038` is `bx lr`, with no effect. Do not invent a map clear or reset for it.

There are also **two PositionUpdate-derived recipients**. `ReactionTriggerStrategyPositionUpdate` inserts the halfword at `0x00C769C0` (0x32) at `0x006121CC`, then calls the same E2G wrapper at `0x006121D4`. Both FacePositionUpdated (`0x0060C610`) and ObjectPositionUpdated (`0x00611054`) construct that base. Their subsequent extra tags are respectively 0x46 (`0x00C75A40`) and 0x44 (`0x00C76898`); these do not add a second tag-50 subscription.

The concrete vtable relocations are:

- Face table base `0x0102CE50`: slot `0x0102CE70` → `0x00612215`; pose-based handler slot `0x0102CE84` → `0x0060CF61`.
- Object table base `0x0102D2A0`: slot `0x0102D2C0` → `0x00612215`; pose-based handler slot `0x0102D2D4` → `0x0061153D`.

Thus each tag-50 event calls base AlwaysHandleInternal `0x00612214`, which calls ResetReactionData at `0x00612224`, then always dispatches the concrete vt+0x2C handler. Both derived handlers recognize 0x32 and return without their observed-face/object work (`0x0060CF6C..0x0060CF6E`, `0x0061154A..0x0061154C`).

**ResetReactionData does not reset the stored entries.** At `0x0061226C..0x00612296`, each map element's key and two poses are copied onto the stack; the remaining fields are copied with `ldrd`/`strd`, and zero is written to `[sp,#0x20]`. The two temporary poses are immediately destroyed at `0x0061229A` and `0x006122A0`. The loop advances the map iterator without writing that zero back to the node. Pose ownership temporarily increases/decreases through those copies; the persistent reaction-data value remains unchanged. A C# loop that mutates the stored value would not reproduce this code.

### Registration and invocation order

Robot construction creates BlockWorld at `0x0050FC66`; BlockWorld creates its BlockConfigurationManager at `0x0061BDC2`. It then creates BehaviorManager at `0x0050FCAC`. BehaviorManager creates BehaviorContainer at `0x005A08D6`, and that container creates configured behaviours while traversing its config map (`0x0059C34C` onward). CantHandleTallStack subscribes during its constructor, so its subscription precedes CubeLightComponent construction at `0x0050FDDE`. The config-map traversal is not itself claimed to be sorted; the relative ordering here follows from the enclosing component construction, not from an assumed ordering of that map.

Much later, Robot calls InitReactionTriggerMap at `0x00510396`. It obtains `reactionTriggerBehaviorMap`, iterates begin/end (`0x005A16F6..0x005A171C`), constructs one strategy per entry (`0x005A17B6`) and retains it only if its target behaviour exists (`0x005A17CC..0x005A17D6`, deletion branch `0x005A1872`). The shipped file `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/reactionTrigger_behavior_map.json` has FacePositionUpdated first, ObjectPositionUpdated fourth, CubeMoved sixth. The FistBump and Hiccup entries in between subscribe to other tags. With the shipped behaviours successfully loaded, the six recovered callbacks therefore run in this order:

1. BlockConfigurationManager.
2. CantHandleTallStack.
3. CubeLightComponent.
4. FacePositionUpdated's PositionUpdate-base subscription.
5. ObjectPositionUpdated's PositionUpdate-base subscription.
6. CubeMoved.

During Robot's earlier construction-time Delocalize call at `0x00510324`, the reaction-map initialization has not occurred. The first three have already been constructed; the last three do not yet exist. This distinction prevents applying the final six-entry list retroactively to that earlier broadcast. Missing external interface prevents the subscriptions; missing/failed behaviour creation or destroyed strategy handles removes the corresponding recipient rather than reserving an empty ordering slot.

This order comes from the native signal implementation. E2G Subscribe `0x006626CC` selects the signal by the zero-extended tag in interface+0x20 and calls `0x0066411C`. Its slot allocator `0x00664684` appends before the circular-list sentinel: `[oldTail]=new`, `new.next=sentinel`, `new.prev=oldTail`, `sentinel.prev=new` at `0x006646A6..0x006646B0`. Broadcast `0x00662508` invokes DeliverToGame (vt+0x48) first, creates its timestamped event, then dispatches through `0x00662590`. The emitter at `0x00663C74` walks forward, invokes nonempty callbacks at `0x00663C90`, obtains next after each callback at `0x00663C94`, and holds/releases traversal references. It is not an unordered-map iteration over subscribers: the hash lookup picks one signal; its linked list supplies order. Reentrant subscription/removal would have to follow that actual list traversal; no snapshot semantics are asserted here.

### Exhaustiveness boundary

The search included payload-accessor callers, tag-0x32 dispatch comparisons, direct constant subscription helpers, IBehavior's E2G set wrapper, IReactionTriggerStrategy's E2G wrapper, its PositionUpdate base, and IWantsToRunStrategy's wrapper. The reaction factory's generic event constants at `0x00C75AC0..0x00C75ACA` are 0x22, 0x34, 0x1E, 0x3A, 0x35, 0x3C, not 0x32. Other inspected strategy constants include FistBump 0x8B (`0x00C76710`), Hiccup E2G 0x35 (`0x00C76812`), and PlacedOnCharger 0x39 (`0x00C76FD0`). The IBehavior tag-table search identified CantHandleTallStack in addition to the original two accessor callbacks.

Nevertheless, this is not a complete proof over every indirect vt+0x28 call and every computed tag producer. In particular, the generic ConfigureRelevantEvents/SubscribeToTags families accept supplied sets, and this report has not closed every caller of those families and every interface-pointer alias. The six-recipient order is established for the recovered initialization paths; **global completeness remains UNKNOWN**. A follow-up must close those producer sets, including dynamically created behaviours and reconfiguration, before claiming a universally exhaustive subscriber list. No unchanged list is inferred merely from there being only two Get_\<50\> callers.

## 2. GetParent/FindRoot census

### What counts as a guard

`GetParent` at `0x008453D4` constructs an output Pose3d. If the input pose node exists, it passes that node's parent shared pointer to WrapExistingNode. The input-node null test at `0x0084540A` does **not** test the parent. WrapExistingNode writes the pointer to the output and unconditionally increments node+0x48 at `0x008452BA`. A valid root has a node and a null parent: it takes this failing route.

`PoseBase::FindRoot` at `0x0084520C` is different: after validating the parent pointer it tests it at `0x0084524E`. When null, `0x0084526A..0x0084526E` wraps the original input node, which is valid. A root need not be excluded from any of the public FindRoot sites. The lower-level `PoseTreeNode::FindRoot` at `0x008452C4` itself starts from the parent and is not independently root-safe; the public wrapper's test dominates its call at `0x00845254`. Do not confuse those two functions.

In the table, **F** means root-safe public FindRoot, **G** a positive guard, **P** a producer-dependent unguarded GetParent, and **U** an unresolved producer/alias. P/U do not mean a root is known to arrive during normal execution. All calls are to the public Pose3d specialization, through PLT 0x004A40F0 (F) or 0x004A74D4 (others). The census was a Thumb `.text` direct-branch scan of those targets, with native caller windows checked. It found 55 calls, not an assumed round count of 60; it is not a census of every possible indirect call or inlined implementation.

| Call address | Caller / input | Root guard and disposition |
| --- | --- | --- |
| 0x004DF674 | GetWithRespectToRoot helper, input pose | G/F: IsRoot at 0x004DF65E, compare/branch at 0x004DF662..664; root is copied at 0x004DF66A instead. |
| 0x0050DBE0 | PreActionPose constructor, marker pose | P: no local HasParent; requires marker pose's producer to parent it to the object. |
| 0x0050DDA8 | PreActionPose constructor, marker pose | P: IsChildOf check/log in this constructor is not a parent guard for this operand; failed assertion is not an unconditional return. |
| 0x0050DFFA | PreActionPose transformed-copy constructor, supplied Pose3d (r2 saved in sb at 0x0050DF16) | P: no local parent check; reads supplied transform pose's parent, not automatically the source PreActionPose's parent. |
| 0x0050F9FE | Ramp::IsPreActionPoseValid, preaction+8 | P: nonnull robot-pose pointer, base validity result and action type 3 gates at 0x0050F9BE..CA; these are not explicit parent tests. Base validity/producer proof remains open. |
| 0x0051275C | Robot::SendAbsLocalizationUpdate, flattened input | P: flatten helper preserves a root unchanged (0x004DF666..66A); flattening alone is not a guard. |
| 0x0051367C | Robot::UpdateCurrPoseFromHistory | F: callee safely accepts a root. |
| 0x0051587C | Robot::LocalizeToObject, first root lookup | F. |
| 0x00515902 | Robot::LocalizeToObject, second root lookup | F. |
| 0x00530804 | HistRobotState::Interpolate, first input pose | P: no local parent check. History-insertion checks do not prove every stored pose is non-root. |
| 0x00530E92 | RobotStateHistory::AddRawOdomState, supplied state pose | G: HasParent at 0x00530E84; root branches around GetParent at 0x00530E8A. |
| 0x005310C2 | RobotStateHistory::AddVisionOnlyState, supplied state pose | G: HasParent at 0x005310B4; root branches around GetParent at 0x005310BA. |
| 0x005315B8 | RobotStateHistory::GetRawStateAt, first root lookup | F. |
| 0x005315D2 | RobotStateHistory::GetRawStateAt, second root lookup | F. |
| 0x0053193E | RobotStateHistory::ComputeStateAt | P: stored/history-derived pose; no positive local parent guard established. |
| 0x00531A00 | RobotStateHistory::ComputeStateAt | P: stored/history-derived pose; no positive local parent guard established. |
| 0x00531AA0 | RobotStateHistory::ComputeStateAt | P: stored/history-derived pose; no positive local parent guard established. |
| 0x0053407C | HandleActiveObjectMoved, first root lookup | F. |
| 0x005340A4 | HandleActiveObjectMoved, second root lookup | F. |
| 0x00534888 | HandleActiveObjectStopped, first root lookup | F. |
| 0x005348B4 | HandleActiveObjectStopped, second root lookup | F. |
| 0x0054A514 | TurnTowardsObjectAction::Init | F. |
| 0x0054A964 | TurnTowardsPoseAction::Init | F. |
| 0x00550A5C | IDockAction::GetPreActionPoses, Robot::GetPose result | P: no local HasParent; robot-pose invariant required. |
| 0x00552780 | PopAWheelieAction::SelectDockAction, robot pose | P: same robot-pose dependency. |
| 0x00552D5C | FacePlantAction::SelectDockAction, robot pose | P. |
| 0x00553A42 | PickupObjectAction::SelectDockAction, robot pose | P. |
| 0x00553EBC | PickupObjectAction::Verify, robot pose | P. |
| 0x005568CC | RollObjectAction::SelectDockAction, robot pose | P. |
| 0x005590A4 | DriveToObjectAction::InitHelper, robot pose | P. |
| 0x00559252 | DriveToObjectAction::InitHelper, temporary at sp+0x74 | G by construction on the successful GetWithRespectTo route at 0x005590AE..BE, with the robot parent as reference; this does not protect the earlier 0x005590A4 call. |
| 0x00559934 | DriveToObjectAction::CheckIfDone, robot pose | P. |
| 0x00617BC8 | PyramidBase::GetBaseInteriorMidpoint | F. |
| 0x00617EDE | PyramidBase::ObjectIsOnTopOfBase | F. |
| 0x0061E27C | BlockWorld::CreateFixedCustomObject, robot pose | P: allocated-object nonnull check is unrelated to parent presence. |
| 0x006202C0 | BlockWorld::UpdateObjectOrigin | F. |
| 0x00620F1A | BlockWorld::AddAndUpdateObjects | F. |
| 0x006219A0 | BlockWorld::UpdatePoseOfStackedObjects, object pose obtained at 0x00621996 | P: the successful relative transform at 0x0062196E..74 concerns another operand; it does not independently establish this object's parent. |
| 0x00622416 | BlockWorld::AddMarkerlessObject, robot pose obtained at 0x0062240E | P. |
| 0x00622D96 | BlockWorld::AddLocatedObject | F. |
| 0x00624D1A | BlockWorld::SanityCheckBookkeeping | F. |
| 0x00625B14 | BlockWorld::CheckForCollisionWithRobot | F. |
| 0x0062A588 | ObservableObjectLibrary::PoseCluster::RecomputePose | P: cluster/object pose input; size/iteration conditions are not parent guards. |
| 0x0062BA30 | callback beginning 0x0062B9FC, captured pose pointer at closure+8 | U: no parent check before this read. Nearest-symbol attribution to operator delete at 0x0062B9F6 is wrong; this is a separate callback. Captured-pose producer/lifetime remains to be closed. |
| 0x00633CE4 | CarryingComponent::UnSetCarryingObjects, located object's pose | P: object-null and IsChildOf(lift) decisions precede it. The branch reaching GetParent does not establish HasParent; an unrelated root can fail IsChildOf and enter it. |
| 0x0064AD7A | PathComponent::StartDrivingToPose, first root lookup | F. |
| 0x0064AEC8 | PathComponent::StartDrivingToPose, second root lookup | F. |
| 0x00655034 | VisionComponent::UpdateVisionMarkers | F. |
| 0x00659F58 | callback beginning 0x00659E9C | F. Nearest-symbol attribution to preceding operator delete at 0x00659E96 is wrong. |
| 0x0067EB6A | MapComponent::UpdateObjectPose | F. |
| 0x00846D82 | Pose3d::ToPoseStruct3d, flattened temporary | G: original input's node and parent checks at 0x00846D70 / 0x00846D7C; ordinary valid non-root flattening produces a pose parented to its root. Invalid/cyclic flatten failure is outside this root-input claim. |
| 0x00846D92 | Pose3d::ToPoseStruct3d, same temporary, diagnostic mismatch path | G: same dominating checks; additionally parent-ID mismatch after the first call. |
| 0x00846FAA | Pose3d::IsSameAs_WithAmbiguity, this | G: node checks and parent-pointer cbz at 0x00846FA4; root takes GetWithRespectTo path at 0x00846FC4. |
| 0x0084803C | PoseOriginList::SanityCheckOwnership, an origin pose | G: HasParent at 0x00848030; compare/branch at 0x00848034..36. Ownership verification alone is not the guard. |
| 0x008775F6 | ObservableObject::GetZRotatedPointAboveObjectCenter | F. |

### Producer checks and the remaining normal-running question

The history checks are particularly easy to overstate. At `0x00530E8A`, !HasParent branches to `0x00530EA6`, which proceeds to insertion at `0x00530EB0`. AddVisionOnlyState similarly branches to its continuing insertion/time-check path at `0x005310D8`. They reject a **parented but non-flattened** pose when its parent is not root; they do not reject a root. Consequently, one cannot use these methods alone to certify Interpolate or ComputeStateAt against root input. A normal-running fault witness still requires tracing a real engine producer that supplies such a root and a subsequent consumer path that takes one of the unguarded calls. This report did not establish that witness.

The common robot-pose helper `0x004EA398` likewise does not prove non-root. It compares `_pose.GetRootID()` with the world origin ID (`0x004EA3A6..0x004EA3B4`) and checks HasSameRootAs(world origin) (`0x004EA3EC..0x004EA3FA`), with diagnostic reporting. Those facts can hold for the world origin itself. Robot construction calls Delocalize before ordinary updates; Delocalize sets up a new-origin child pose. SendSyncTime's zero pose is also explicitly constructed with the current origin as parent, rather than being a bare root. These positive producer examples do not settle every subsequent SetPose/assignment and exceptional path.

`Robot::SetPose` at `0x0051502C` checks HasSameRootAs(world origin), then assigns the supplied pose. That interface does not by itself exclude the world-origin root. A caller census, including all relative-pose results and copy/assignment paths into Robot+0x298, is still needed to turn the P rows into a universal non-root invariant or a concrete normal-running counterexample. An artificial call SetPose(worldOrigin), or directly passing a root to SendAbsLocalizationUpdate, is an API-level crash demonstration, **not** proof that the shipped normal producer chain does that.

The marker/preaction, located-object/stack/carrying, and pose-cluster families need corresponding producer closure. In particular, an object-null check is insufficient, IsChildOf(false) is insufficient, and a successful GetWithRespectTo on a different object is insufficient. The callback at `0x0062B9FC` needs its captured closure+8 input traced to its creator. These are recoverable source questions, not hardware-only uncertainty and not permission to substitute a plausible invariant.

Accordingly, the requested list of **demonstrated unguarded normal-running root faults is empty in this extraction**, but the proposition that no such faults exist remains UNKNOWN. The known safe public FindRoot calls and explicit guarded GetParent calls can be excluded from this particular root hazard; the P/U rows cannot yet be excluded. Under the standing M1-034 policy, a root-to-GetParent path, if established, would skip the smallest affected unit and log the owning record and fault at `0x008452BA`. This report does not choose an unproved skip boundary or change production behavior.

## Manifest and handoff

The current manifest was read, rather than inferred from older notes:

- **M11-072**, RECOVERABLE_GAP: “RobotDelocalized subscriber list and order: the Subscribe(tag 0x32) sites, and the reachability of ReactionTriggerStrategyCubeMoved::HandleRobotDelocalized”; its evidence cites `20261013-delocalize-callees-check.md` and the earlier two Get_\<50\> callers. The extra behaviour and two position strategies above must be included before any subscriber-list claim is settled. Global negative completeness is still open.
- **M11-073**, RECOVERABLE_GAP: “Reachability of PoseBase::GetParent() on a root pose (WrapExistingNode owners++ with no null guard faults at 0x008452BA)”; its evidence cites GetParent `0x008453D4..0x0084541E`, WrapExistingNode `0x0084529C..0x008452C0` and roughly 60 calls. The 55-call table distinguishes root-safe FindRoot from GetParent and records guarded/producer-dependent cases. Normal producer closure remains open.

No fidelity reclassification is proposed here. Validation was static disassembly, relocation/tag-table inspection, shipped-config inspection and a direct-call census. No implementation regression test was added because this request permits research only. The answer is self-contained; local scratch disassemblies are not required evidence attachments and are not included in the P2 commit.
