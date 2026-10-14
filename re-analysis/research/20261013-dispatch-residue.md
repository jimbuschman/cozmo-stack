# R-DISPATCH-REST — dispatch residue

Answers `requests/20261013-dispatch-residue.md`. Independent native extraction from shipped ARMv7 `libcozmoEngine.so` 3.4.0-1204. One coverage table per item precedes the detailed results. Addresses are ELF virtual addresses, not Ghidra-generated data-address guesses. Conclusions about conditional reentrancy distinguish an instruction-proven consequence from a demonstrated production trigger.

## Coverage 1 — wildcard robot ID and Broadcast callers

| Step | Result | Native address / boundary |
|---|---|---|
| Correct wildcard constant | Constructor writes **0x000101CF**, not 0x000001CF: MOVW 0x1CF followed by MOVT 1. | `0x0069D004..0x0069D00E` |
| Core emitter gate | Receives subobject at MessageHandler+4; compares supplied ID with subobject+0x18 (=MessageHandler+0x1C). Equal emits every linked robot signal for that tag. | `0x0069DE18..0x0069DE4E` |
| Ordinary branch | Matching robot signal first, then signal registered under actual wildcard 0x101CF. ID 0x1CF takes this branch. | `0x0069DE50..0x0069DE74` |
| Direct core callers | Rvalue Broadcast at 0x69DC6C and const-reference Broadcast at 0x69DD94 forward their supplied ID unchanged. | Calls `0x0069DCAE`, `0x0069DDD6` |
| Rvalue production caller | ProcessMessages obtains ID from first Robot+0x10; does not obtain a robot ID from payload bytes. | `0x0069D8D2..0x0069D8E2`, `0x0069DAB8..0x0069DABA` |
| Constant-ID caller search | No recovered shipped caller explicitly passes 0x1CF or 0x101CF. Only direct caller of either public Broadcast overload is ProcessMessages→rvalue overload. No matching veneers or non-PLT function-pointer relocations found. | Whole-executable branch scan `20261013-dispatch-residue-scan.py`, results `...-calls.txt` |
| Normal app connection ID | Connect handler builds robot ID **1**. Thus normal input dispatch uses ID 1, not all-signals branch. | `0x004ED07A..0x004ED07C` |
| General reachability | Exported Broadcast can receive an arbitrary supplied ID; when a caller supplies 0x101CF, all-signals branch executes. A defined shipped production trigger for that value is **UNKNOWN / not found**. | Public bodies above; no invented timer/disconnect trigger |

## Coverage 2 — all 35 subscriber sites and owning releases

Rows use the manager check's corrected connection order, not the earlier report's rejected ordering. “No native callback witness” means the recovered release is an owner destructor or Init replacement, with no demonstrated raw callback invoking that release; it is not a general promise that reentrant destruction cannot happen. Last-shared-owner release is required for disconnect; releasing a temporary shared_ptr does not suffice.

| # / subscribe call | Raw tag(s), owner | Exact retained-owner release/reset | During raw emit? |
|---|---|---|---|
| 1 / `0x00519B78` | C9, Backup | Vector +8 destructor `0x0051A62C..0x0051A630`. | No native callback witness; owner destruction can remove future subscriber (§3). |
| 2 / `0x0052D1DE` | D2, RIC | Vector +0 clear `0x0052DFA4..0x0052DFC8`; map-node destruction `0x00530322..0x00530326`. | Yes, shared response cleanup path; also ordinary destruction. |
| 3 / `0x0052D236` | EE, RIC | Same clear `0x0052DFA4..0x0052DFC8`; same node destruction. | Yes, same response cleanup. |
| 4 / `0x0052D28E` | C9, RIC | Same clear `0x0052DFA4..0x0052DFC8`; same node destruction. | Removed by response cleanup; C9 handler itself merely sets flag. |
| 5 / `0x0052DE34` | ED, RIC | SendConnectionResponse calls clear `0x0052DF34` before app broadcast; ED lambda reaches response `0x0052E3A2`. | **Demonstrated self-removal** on manufacturing-ID response. |
| 6 / `0x00532C84` | D3, Impl | Own vector +0 destructor tail `0x00532A6E..0x00532A74` → veneer 0x8CB3AC → vector destructor PLT 0x4A4E4C. | No native callback witness. |
| 7 / `0x00532D22` | F0, Impl | Same own-vector tail `0x00532A6E..0x00532A74`. | No native callback witness. |
| 8 / `0x00532D88` | F1, Impl | Same own-vector tail `0x00532A6E..0x00532A74`. | No native callback witness. |
| 9 / `0x00532DEE` | BA, Impl | Same own-vector tail `0x00532A6E..0x00532A74`. | No native callback witness. |
| 10 / `0x00532E54` | BB, Impl | Same own-vector tail `0x00532A6E..0x00532A74`. | No native callback witness. |
| 11 / `0x00532EBA` | BC, Impl | Same own-vector tail `0x00532A6E..0x00532A74`. | No native callback witness. |
| 12 / `0x00532F1E` | BD, Impl | Same own-vector tail `0x00532A6E..0x00532A74`. | No native callback witness. |
| 13 / `0x00532F82` | B2, Impl | Same own-vector tail `0x00532A6E..0x00532A74`. | No native callback witness. |
| 14 / `0x00532FE6` | B7, Impl | Same own-vector tail `0x00532A6E..0x00532A74`. | No native callback witness. |
| 15 / `0x0053304A` | DC, Impl | Same own-vector tail `0x00532A6E..0x00532A74`. | No native callback witness. |
| 16 / `0x0053327A` | 27 member-bound tags, Impl | Same own-vector tail `0x00532A6E..0x00532A74`; each helper pushes ownership into this vector. | No native callback witness; Init called again appends rather than clearing old vector. |
| 17 / `0x0053C180` | B0,CF,EC, TracePrinter | Embedded vector Impl+0xF8 destroyed `0x00532A44..0x00532A48` before own Impl vector. | No native callback witness. |
| 18 / `0x00543FCA` | CA, PlayAnimationAction | Pair +0x98/+0x9C: Init replaces/releases old control `0x00543FD8..0x00543FE2`; destructor `0x00543E48..0x00543E4E`. | Re-Init/destruction; no callback witness for immediate release. |
| 19 / `0x00544032` | CB, PlayAnimationAction | Pair +0xA0/+0xA4: Init `0x00544040..0x0054404A`; destructor `0x00543E3E..0x00543E44`. | Same distinction. |
| 20 / `0x00544092` | D5, PlayAnimationAction | Pair +0xA8/+0xAC: Init `0x005440A0..0x005440AA`; destructor `0x00543E34..0x00543E3A`. | Same distinction. |
| 21 / `0x00545ADC` | C4, Turn | Control +0xE4 destructor `0x00545BC4..0x00545BCA`. | No native callback witness; normal ack records state rather than deleting action. |
| 22 / `0x005481BC` | C4, Head | Control +0xB4 destructor `0x005484B4..0x005484BA`. | Same distinction. |
| 23 / `0x00548AAC` | C4, Lift | Control **+0xA0**, destructor `0x0054D142..0x0054D14C`. | Same distinction. |
| 24 / `0x00551706` | C5, IDock | Pair **+0xD4/+0xD8**: Init replacement `0x00551714..0x0055171E`; concrete cleanup `0x00557594..0x0055759A`. | Re-Init/destruction; no callback witness for immediate release. |
| 25 / `0x0055176C` | DA, IDock | Pair **+0xDC/+0xE0**: Init replacement `0x0055177A..0x00551784`; concrete cleanup `0x0055758A..0x00557590`. | Same distinction. |
| 26 / `0x005BCAA0` | DA set caller, IBehavior raw overload | Shared subscription vector **+0x20**, IBehavior destructor `0x005BC7FE..0x005BC802`. | Inactive dispatch remains installed; deactivation alone is not unsubscribe. No destructor-in-raw-callback witness. |
| 27 / `0x005CB1BE` | D3, DockingTestSimple | Control +0x130 destructor `0x005CEC56..0x005CEC5C`. | No early clear found in owner-method scan; no callback witness. |
| 28 / `0x005D07CE` | D6,F3,B8,EE,D2, FactoryTest helper | Vector +0x11C destructor `0x005D36C4..0x005D36C8`. | EndTest does not clear this vector in recovered paths; no callback witness for destruction. |
| 29 / `0x006308BA` | B6, BlockTapFilter | Vector +4, Robot's embedded component cleanup `0x0051120E..0x00511210`. | No explicit early clear found; owner-destruction consequence §3. |
| 30 / `0x00630934` | B4, BlockTapFilter | Same vector +4 cleanup `0x0051120E..0x00511210`. | Same distinction. |
| 31 / `0x006309AE` | B5, BlockTapFilter | Same vector +4 cleanup `0x0051120E..0x00511210`. | Same distinction. |
| 32 / `0x0063526C` | F5, CubeAccel | List +0x14 clear in destructor `0x0063542A..0x0063542E`. | No explicit early clear found; no callback witness. |
| 33 / `0x0064294C` | CD, NVStorage | Vector +0x128 destructor controls released `0x00643ED2..0x00643EF6`. | No explicit early clear found; no callback witness. |
| 34 / `0x00648C8C` | C6, Path | Pair +0x58/+0x5C destructor `0x00649006..0x0064900A`, after Abort. | Abort's other shared-pointer release is a planner, **not this raw handle**. No callback witness. |
| 35 / `0x0067838C` | EF per robot, FirmwareUpdater | Init clears old vector +0x30 `0x00678324..0x0067834C`; Update terminal state 1 or 2 calls CleanupOnExit `0x006785DC`, reverse clear `0x006785F6..0x0067861A`; destructor `0x006763DC`. | Normal cleanup on subsequent Update, not immediate HandleFlashWriteAck. Reentrant Update/destruction remains conditional. |

## Coverage 3 — owner/signal destruction during emit

| Case | Instruction-derived consequence | Native address / boundary |
|---|---|---|
| Destroy another future subscriber's owner | Last handle release removes callable immediately, patches reachable predecessor/successor; its callback is skipped when current is still linked. | Scoped reset `0x0051D8F8..0x0051D932`, unlink `0x0051D338..0x0051D36E`, emitter late next read `0x0069E294` |
| Destroy already-delivered owner | Cannot undo delivery; normal linked-list removal. | Same unlink and late advance |
| Self-only unlink | Node lives through emitter reference; own next/prev retained. Callable/capture is destroyed immediately, so callback must not read destroyed captures afterward. | `0x0051D340..0x0051D358`, `0x0069E280..0x0069E29E` |
| Self unlink plus former successor deletion | Retained next may point at freed successor; late load/ref increment accesses freed memory. No guaranteed skip-next or snapshot semantics. | `0x0069E294..0x0069E29A`, retained own links in unlink |
| Signal destructor | Unlinks all callback nodes, releases sentinel twice, releases lifetime-token shared control. No wait-for-emit/deferred destruction. | `0x0051D2FC..0x0051D326` |
| Signal destroyed during callback | Emitter retains current node only; r5 signal address unprotected, successor may be freed; after callback read can use freed node/signal/sentinel. | `0x0069E294..0x0069E2A8`; destructor above |
| Destroy complete MessageHandler | Destroys its own handles/connection manager and then event-map signals. Active outer unordered-map iteration also has no lifetime guard. | `0x0069D034..0x0069D06C`, all-signals next read `0x0069DE48` |
| Actual production destruction witness | RIC handle clear during ED emit is demonstrated. A shipped raw callback deleting the active signal/MessageHandler was **not found**; no assertion that it happens routinely. | ED `0x0052E3A2` → response `0x0052DF34`; remaining arbitrary reentrant ownership is UNKNOWN |
| Policy | Undefined reads/crashes use existing M1-034; no new approval required and no safe original continuation invented. | Current AGENTS.md; first possible reads above |

## Coverage 4 — loader completion versus Robot construction

| Step | Exact result | Native address / boundary |
|---|---|---|
| Engine Update entry | After initialization checks, UiMessageHandler::Update runs **before** engine-state dispatch. | `0x004ED4D4..0x004ED514`, state read `0x004ED5C4` |
| State sequence | WaitingForUIDevices(1)→LoadingData(2); loader completion→Running(3); UpdatingFirmware(4) has own update route. | `0x004ED5D4..0x004ED66C`; enum cross-check `unity/scripts/csharp/Anki.Cozmo/EngineState.cs:3` |
| Start worker | DoNonConfigDataLoading creates thread targeting LoadNonConfigData, returns false. | `0x005235CE..0x005235F8` |
| Finish gate | Reads atomic progress float; when not <1, joins thread, sets loaded byte +0x268=1, returns true. Existing loaded byte bypasses work. | `0x00523570..0x005235CC` |
| Behavior load order | After animations/lights, LoadEmotionEvents then **LoadBehaviors**, other config-free data, clear scratch map, progress=1 store. | `0x0051F450..0x0051F488`; LoadBehaviors call `0x0051F452` |
| Native Connect gate | Checks DoesRobotExist(1), not loading state. If absent: AddRobotConnection then AddRobot(1). | `0x004ED020..0x004ED07C` |
| AddRobot gate | CozmoEngine wrapper calls RobotManager unconditionally; manager builds Robot then RIC. No loader completion check in wrapper. | `0x004ED1E0..0x004ED1F2`, Robot construction `0x0052EE9C`, RIC `0x0052EF08` |
| Message ingress | Ordinary UI message path has socket/SDK gates, no EngineState LoadingData check; dispatches Broadcast through virtual +8. | HandleProcessedMessage `0x00661FD8..0x0066213A`, ProcessMessages `0x00662800`, Update `0x006629E8` |
| App method | RobotEngineManager.ConnectToRobot validates ID/IP and sends; no engine-ready wait in this method. | `unity/scripts/csharp/RobotEngineManager.cs:510..528` |
| “Always loaded first?” | **No enforced native guarantee.** Normal Running transition waits for completed worker, but an ordinary early connect message can construct Robot before that transition. Exact shipped-screen scheduling of that message is UNKNOWN. | Ordering/gates above |

## Coverage 5 — Impl firmware-copy and mismatch-field readers

| Reader/write | Fields and observed effect | Native address / boundary |
|---|---|---|
| Constructor | Flags +0x38/+0x39 zeroed together; memset only first 0x18 bytes, so copy +0x18..+0x37 is not initialized here. | `0x00532944..0x00532960` |
| D2 handler | Writes full 44-byte copy at +0x0C..+0x37; compares hashes, sets sticky flags and emits mismatch message. | `0x00533348..0x005335C4`, prior M2-019 extraction checked |
| DockingTestSimple InitInternal | Reads three firmware u32s and both 16-byte hashes for test log; no flag read. | Impl-pointer load `0x005CB516`; u32 loads `0x005CB5E6/0x005CB616/0x005CB646`; hash loops `0x005CB658..0x005CB6E0` |
| LiftLoadTest InitInternal | Same five payload fields read for test log; no flag read. | Impl-pointer load `0x005D419C`; u32 loads `0x005D4290/0x005D42BE/0x005D42EC`; hash loops `0x005D4306..0x005D4380`; Write `0x005D4398` |
| Other Robot accesses | Constructor stores Impl pointer/initializes it; destructor destroys it; Update reads +0x130 (different field). These do not read requested copy/flags. | Robot+0x518 references listed in `...-impl-crossrefs.txt` |
| Other flag consumers | No other recovered direct Impl member method or Robot/behavior cross-reference reads +0x38/+0x39. No additional production rejection gate established from these flags. | Scoped native/decompiled owner-method and +0x518-reference scan; indirect/arbitrary externally held-pointer accesses **UNKNOWN**, not an absolute binary-wide no-reader proof |

## 1. The request's wildcard premise is contradicted by the constructor

The missing instruction is `movt r3,#1` at **0x0069D008**. The sequence `movw r3,#0x1cf; movt r3,#1; strd r3,r2,[r4,#0x1c]` stores 65999 (`0x101CF`) at MessageHandler+0x1C. This matters to both the all-signals condition and the final wildcard signal lookup. The previous manager-adopted check and current manifest prose say 0x1CF; that statement should be corrected after independent verification of this instruction. This report does not edit them.

The core event helper takes `MessageHandler+4`, so its own +0x18 refers to the same field. It first finds the outer map entry keyed by event tag. No entry means return. Equal wildcard ID traverses the **inner unordered-map node chain**, emitting each node's signal, with `next` loaded after each emit. This is not numerical robot-ID order or subscriber registration order across robots. An empty inner map emits nothing. Ordinary ID first finds its own inner signal, emits if present, then separately finds the actual wildcard-key signal, emits if present; a missing own signal does not suppress wildcard dispatch.

The whole-executable direct branch scan found only the two core callers, forwarding the original API ID unchanged. Both create a timestamped event with current BaseStationTimer seconds (f32 widened to f64), one moving and one copying the robot union, then clear their temporary union. ProcessMessages is the only recovered direct/PLT caller of either public overload; its ID is **GetFirstRobot()->+0x10** (`0x0069D8D2..0x0069D8E2`), not a packet header's robot ID. No first Robot means the popped data is skipped. It calls ShouldFilterMessage with that ID/tag, unpacks and validates consumed size, then rvalue Broadcast at `0x0069DABA`. Fatal robot-error disconnect handling occurs after Broadcast returns (`0x0069DAC2..0x0069DACA`).

Normal ConnectToRobot always calls AddRobot(1). Thus nothing in this recovered production ingress calls the all-signals branch. A manually created first Robot with ID 0x101CF, or another caller to the exported API supplying that value, would satisfy the branch; that is a conditional API consequence, not an observed shipped automatic event. ID 0x1CF satisfies neither the wildcard comparison nor wildcard lookup key. No literal wildcard-emitter call was recovered. Direct branch scanning, standard interworking veneers and dynamic relocation inspection were used; unknown computed indirect use is not claimed disproven.

## 2. Early unsubscribe behavior and corrections to prior ownership notes

The scoped container's zero-shared path (`0x004EF184..0x004EF1B8`, previously extracted) destroys/resets its handle. The raw handle reset at `0x0051D8F8` locks the weak lifetime token; if expired it returns. Otherwise it searches the **currently linked** circular list for its exact node address; if absent, return; if found, tail-call unlink. It does not dereference a retired node's callable merely to determine membership. The weak lock is released before the search, so it is not a long-lived signal ownership guard across user callbacks.

All 35 registration sites are mapped above, including helpers expanding to several actual tags/instances. FactoryTest, DockingTestSimple and LiftLoadTest instances exist twice at normal Robot construction as established by the manager check. Their constructor subscriptions persist when inactive; IBehavior raw callbacks route to inactive/active virtual handlers. Deactivating behavior is therefore not a general instruction to unsubscribe. A test ending likewise does not imply that its constructor subscription vector was cleared.

The owner-method scan inspected each named owner class's functions for shared release, handle-container vector/list clear/destruction and concrete holder replacement. It separated raw holder releases from unrelated shared data/planners/helper behaviors. Additional callable thunks/destructor aliases were read from installed vtables/prior native extracts. No further explicit raw-holder early clear was found beyond RIC response clear, action Init replacement and firmware Init/terminal cleanup. This is bounded source evidence; reentrant app callbacks can still destroy owners through external paths, as §3 explains. It is not a claim that every possible external lifetime schedule has been recovered.

**RIC:** SendConnectionResponse marks completion byte +0x10, reverse-clears all retained handles, then constructs/delivers the app response (`0x0052DF2E..0x0052DF64`). Manufacturing-ID lambda reaches it at `0x0052E3A2`, so self-removal is an actual native emit-time path. The reverse clear releases ED first, then C9, EE, D2; removing ED's preceding RIC nodes does not delete ED's former successor, which is the order-sensitive safety condition. Any response path using this helper gets the same clear before app delivery. Map-node destruction also frees the vector later; completion does not require preserving subscriptions until map erasure. C9 handler setting its “available” flag is not itself unsubscribe.

**Animation:** Init creates/appends the new signal subscription before replacing the old holder pair and releasing its old shared control. It does this independently for CA, CB and D5. At completion/destruction, the raw holders are released in reverse field order D5, CB, CA (after a separate engine-to-game holder +0xB4). Re-init is not “clear all then resubscribe.” If it is invoked reentrantly during emission, newly appended nodes may be visited that same emission and old nodes retired according to current links.

**Dock correction:** The raw C5 and DA holders are individual pairs +0xD4/+0xD8 and +0xDC/+0xE0. The earlier report's “vector +0xE4 owns both” statement is inaccurate: that vector is a separate subscription family. Concrete cleanup at `0x00557582` first destroys +0xE4 vector, then releases DA control +0xE0 at 0x557590 and C5 control +0xD8 at 0x55759A. The nominal IDockAction::~IDockAction symbol is a bare return; it cannot establish these releases alone. The concrete derived cleanup body must be followed. Repeated Init installs replacement handles before releasing old controls. Lift's raw subscription control is +0xA0, as its actual vtable destructor at 0x54D138 confirms.

**Path:** C6 pair is +0x58/+0x5C. Destructor calls Abort then releases +0x5C. Abort clears shared pair +0x30/+0x34 and calls ClearPath; those are planning-operation objects, not the C6 handle. Replan/SelectPlanner shared-pointer releases likewise do not unsubscribe C6. Do not infer early raw disconnect from every __release_shared call.

**Firmware:** InitUpdate reverse-clears previous +0x30 vector before installing per-robot EF subscriptions. GotoFailedState writes state +0x58=2 and reports failure, but does not immediately clear the vector. Update checks terminal state: 1 reports successful completion first, then CleanupOnExit; 2 goes straight to CleanupOnExit; both return true. Active state zero calls UpdateSubState and returns false. Cleanup reverse-clears vector while retaining allocation. EF acknowledgment handler updates updater state; the resulting release occurs in a subsequent normal Update, rather than being invented at the end of the ack callback. Destructor also releases retained ownership. This extraction changes no firmware policy and executes no update.

For motor actions/components, no handler-local immediate destruction was recovered. Normal action result handling leads to cleanup through owner/action-list update, not a universal “unsubscribe on first ack” rule. The exact raw-handle ownership paths and Init substitutions are sufficient to reproduce known releases; arbitrary external reentrant owner destruction remains the explicit lifetime boundary in §3.

## 3. Why destroying an owner and destroying the signal are different

Emit increments the current node's +0x20 refcount, checks callable manager +0x18, calls synchronously, then reads current.next **after** callback. It increments the next node, releases current, reloads `signal.sentinel` from r5, compares next to sentinel, repeats. The initial sentinel ref is used while the sentinel is current; it is released during the first advance. There is **no permanent sentinel ref held for the entire emission**. There is also no signal-object shared_ptr held across callback invocation.

Unlink destroys the installed callable first (small-buffer manager slot +0x10 or heap manager slot +0x14), clears manager, patches next.prev and prev.next, and releases the list's node reference. It leaves the removed node's own links intact. If a handler destroys a different owner whose nodes lie in the future of a still-linked current node, those future nodes are removed from the live successor chain and not called. Past delivery is unchanged. An owner-bound callback uses raw owner captures/member pointers; node retention does not keep the captured owner alive.

Self-only removal keeps current node storage alive via emitter ref, but destroys callable/capture immediately. A callback that continues using that storage/owner after self-destruction has no source-established lifetime protection. A further deletion of current's formerly immediate successor can free that successor, while detached current still points at it. The first emitter access after return, `0x0069E294`/`0x0069E296`, can therefore be a freed-memory access. A managed implementation must not present a stable subscriber snapshot as exact original behavior; current standing M1-034 handles the undefined tail visibly.

Signal destruction uses `0x0051D2FC`: repeatedly unlink sentinel.next until sentinel.next==sentinel, then release sentinel twice, then release its lifetime-token shared control. It neither waits for active emission nor clears and preserves a protected sentinel for the emitter. If the current handler's signal is destroyed, current callback node can remain via its emitter ref while its retained next points at freed storage. r5 may also refer to freed signal/map-node storage. The emitter's successor ref increment and subsequent `[r5]` sentinel reload can fault or read undefined memory. Destruction of the entire MessageHandler additionally invalidates outer/inner unordered-map traversal storage; in the all-signals branch `0x0069DE48` reads the current map node after its signal callback returns. No safe native continuation is recovered.

These are **instruction-derived conditional hazards**. RIC reverse clear is the positive native self-removal witness; a handler destroying the active signal or whole MessageHandler was not identified. Ordinary fatal robot-error processing disconnects only after Broadcast returns, so that path alone is not proof of signal destruction mid-emit. If host delivery causes reentrant destruction, the engine's raw pointers and callable captures provide no blanket guarantee. Existing M1-034 applies to actual undefined/terminating tails: skip the smallest lost operation, keep prior state as applicable, visibly log M1-034 plus M2-020/owner. Defined future-owner unsubscribe remains exact.

## 4. Loading is joined before Running; connecting is not gated by Running

Engine state byte +0x62 has Stopped=0, WaitingForUIDevices=1, LoadingData=2, Running=3, UpdatingFirmware=4. Update calls UiMessageHandler::Update at 0x4ED514 before reading state at 0x4ED5C4. In state 1, desired UI count triggers SendSupportInfo and state 2. In state 2, DoNonConfigDataLoading completion triggers BroadcastAvailableAnimations then state 3; loading progress report follows. State 3 alone performs normal Robot connection/update work; state 4 has firmware-update work.

DoNonConfigDataLoading reads progress at loader+0x270 with memory barrier and copies it to the caller. If loaded byte +0x268 is already set, returns true. With no thread handle at +0x274, it starts a worker for LoadNonConfigData and returns false. With a thread handle and progress<1, returns false. Otherwise it joins the worker, verifies handle empty (nonempty reaches std::terminate), sets loaded byte and returns true. The join means that normal transition to Running follows worker completion, not merely a progress number posted before the end of the worker. Its VFP less-than check uses BMI; unordered NaN does not take that less-than branch, but join still waits for the thread.

LoadNonConfigData itself returns immediately if its required loader field +4 is null; otherwise it VERIFYs if on main thread and continues. After animation/lights and an atomic progress increment, it calls LoadEmotionEvents, **LoadBehaviors at 0x0051F452**, LoadActivities, reaction/animation/cube responses, SayText metadata, clears scratch animation-file map and stores progress=1 with barriers (`0x0051F482..0x0051F488`). The normal loaded result thus includes behavior loading. Missing-resource/worker-termination cases must not be changed into successful loading.

The crucial separate ingress is game-to-engine ConnectToRobot. At 0x4ED020 it checks only whether robot **1** already exists. Existing robot logs `.AlreadyConnected` (`Robot already connected`) and returns. If absent it calls AddRobotConnection(message), then CozmoEngine::AddRobot(1) at 0x4ED07C. That wrapper immediately calls RobotManager::AddRobot (`0x004ED1E0..0x004ED1F2`), without inspecting initialized/loaded flags or +0x62. Manager creates Robot at 0x52EE9C, then RIC at 0x52EF08, as the check established. No loading completion gate was found in this route.

UiMessageHandler ordinary ingress checks socket connection status and SDK restrictions. HandleProcessedMessage eventually sends non-Ping messages through virtual Broadcast +8 at 0x66213A; its flags +0xE1/+0xE2 are SDK filtering/status, not engine LoadingData state. Its message handling contains no reference to CozmoEngine+0x62 or loader+0x268. Unity RobotEngineManager.ConnectToRobot validates arguments then immediately sends; it does not wait for engine-ready in that method. SDK branches are out of scope under the operator's app-boundary decision; they are not required to exhibit the early ordinary message route.

Therefore the native statement **“LoadNonConfigData always finishes before AddRobot can construct a Robot” is false as a gate claim**. A host can send ordinary ConnectToRobot early during state 1/2 and the earlier UI Update can construct Robot before the state's loading work/join occurs. The ordinary official screen flow may wait for a readiness event, but complete screen scheduling of the first connection request was not established here. No source-faithful engine restriction should be invented from a UI scheduling assumption. Normal connection-time behavior construction order remains valid when the loaded map exists; universal preloaded dev-behavior presence cannot be inferred solely from the loader's Running transition.

## 5. Firmware-copy readers missed by the earlier extraction

The Impl pointer is stored at Robot+0x518. Scoped cross-reference search found seven mentions: Robot constructor store/Init call, Robot destructor load/reset, Robot::Update +0x130 timestamp read, DockingTestSimple::InitInternal load, and LiftLoadTest::InitInternal load. Named Impl-method reads were separately inspected for the requested offsets. No separate rejection/transport gate reading +0x38/+0x39 was recovered.

DockingTestSimple::InitInternal loads Impl at 0x5CB516, builds a stringstream and reads: Body Version from +0x14, RTIP Version from +0x10, Wifi Version from +0x0C; then **ToEngineCLADHash from +0x28..+0x37** and **ToRobotCLADHash from +0x18..+0x27**, each 16 bytes in increasing order. Numeric formatting is stream hex (`flags=(flags&~0x4A)|8`), not the D2 handler's uppercase leading-space `XX` conversion helper. Each hash byte is widened to int, inserted in hex and followed by a space; no setw(2), no leading-zero padding and no uppercase manipulation appears. Thus byte 0x0A prints `a ` here, not ` 0A`. Labels/newlines and accumulated string go through the behavior's Write/rolling logger path. This is a payload-copy consumer; it does not consult mismatch flags.

LiftLoadTest::InitInternal does the same five field reads and formatting, at the addresses in coverage 5. Its final stringbuffer extraction and Write occur at 0x5D438C..0x5D4398. Start-test banners, robot serial number and battery precede the firmware block; those are other inputs, not fields of this copy. Neither test waits for a fresh D2 message in this formatting block. Constructor initializes flags +0x38/+0x39 to zero and only memset(this,0x18), leaving hash-copy region +0x18..+0x37 uninitialized until D2. A test log that reads those bytes before D2 has undefined uninitialized payload data; do not silently fill it as recovered zero hashes. Existing M1-034 governs that undefined-input case with the owning test record.

D2 remains the one recovered writer of the full copy and one recovered reader/writer of sticky mismatch bytes. Its own two hash comparisons, warnings and app mismatch-report behavior belong to current M2-019; this request adds the omitted test-log consumers and bounds the search. The Robot Update reference to +0x130 is a different timestamp and must not be mistaken for these flags. No additional flag consumer was found by the scoped scans; computed pointer escapes/external invocation are not claimed exhaustively disproven.

## Evidence and manager handoff

Engine SHA-256: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Local evidence is `20261013-dispatch-residue-{signals,loading,removal,dtors,more,final-focus,last,tail,calls,owner-scan,impl-navigation,impl-crossrefs}.txt`, with extraction/scan scripts beside the report. Decompilation was navigation; constants, ownership offsets, control flow and reads were checked in native instructions. No network endpoint/robot was contacted, shipped code executed or shipped file uploaded.

Current M2-020 title is **“Robot-message dispatch residue: Broadcast callers passing robot ID 0x1CF, early-unsubscribe paths, destruction during an emit, load completion before AddRobot”**, status **RECOVERABLE_GAP**; its current evidence repeats the manager check's 0x1CF constructor interpretation and LoadNonConfigData call ordering. The MOVT at **0x0069D008** contradicts the literal value in that evidence/title; manager should verify and correct it before a builder treats 0x1CF as wildcard. This report also corrects raw Dock/Lift handle ownership, supplies every subscriber release row, signal-destruction consequences, early-connect gate behavior and additional firmware-copy consumers. It does not alter any manifest, inventory, implementation or approval.

Remaining UNKNOWNs are demonstrated shipped-screen scheduling of an early connect, a positive ordinary shipped trigger for wildcard emission/active-signal destruction, and arbitrary computed/externally driven lifetime schedules. These are stated separately from the recovered gates, releases and conditional undefined-memory paths. Suggested later regressions: distinguish 0x1CF/0x101CF/1; separate missing robot and wildcard signal lookups; all 35 lifecycle owners; action re-Init append-before-retire; RIC reverse clear; firmware terminal cleanup; future-owner unsubscribe and M1-034 destruction tails; connect received during LoadingData; exact test-log hex formatting. No tests, branch, worktree, commit or push were performed.
