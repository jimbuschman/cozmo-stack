| M5-055 row | Coverage: engine member order / relevant nested lifetime | Native citation / destructor destination |
|---|---|---|
| E01 | Reset engine vptr; clear gTickTimeProvider only if it is the timer singleton; remove timer; disconnect context's VizManager. | `0x004EC68C..0x004EC6BE`; these precede all member destruction below. |
| E02 | First owned member: engine+0x64 AnimationTransfer, exchange to null and virtual deleting destructor. | `0x004EC6C0..0x004EC6CC`; vtable `0x01023A0C` → `0x0057FC58` → body `0x0057FC10`, then delete. |
| E03 | In-place DebugConsoleManager member at engine+0x40: release subscription vector at +0x50 first, then vector at +0x40. Scalars require no destruction. | `0x004EC6CE..0x004EC6DA`; both call vector destructor `0x004EAE94`. Constructor identifies in-place member: `0x004EB2A8..0x004EB2BC`, DebugConsoleManager::Init call in `0x004EB21C` constructor. |
| E04 | engine+0x3C DeviceDataManager: exchange to null, delete through vtable. | `0x004EC6DE..0x004EC6E8`; slot `0x01030454` → `0x0066FEF4` → body `0x0066FEC0`, then delete. |
| E05 | engine+0x38 nullable owned pointer: exchange to null; if nonnull virtual deleting destructor. Concrete member type/target UNKNOWN. Constructor sets it null. | `0x004EC6EA..0x004EC6F6`, constructor zero paired with context store `0x004EB290..0x004EB294`. |
| E06 | engine+0x34 CozmoContext: exchange to null, virtual deleting destructor, completely before E11. | `0x004EC6F8..0x004EC702`; slot `0x0101E45C` → `0x004EAD74` → body `0x004EAAAC`, then delete. |
| E07 | Context first calls RobotManager::RemoveRobots, before destroying any context member. Visits robot map in ascending native tree order, destroys each nonnull Robot, deletes it, then clears map nodes. | context `0x004EAAB8..0x004EAABE`; RemoveRobots `0x0052F428..0x0052F472`; Robot body `0x005110D4`. |
| E08 | During each Robot teardown, destroy ActionList (+0x250), then DrivingAnimationHandler (+0x24C) subscriptions, handler animation/string vector, handler allocation; then in-place streamer (+0x60). | `0x00511512..0x0051154A`; handler vector +0x4C → `0x004EAE94`; pair vector +8 → `0x004F090C`; streamer → `0x0057AF48`. |
| E09 | Streamer releases subscription vector +0x1B4, then remaining members; tag-B4 base subscription is released last in normal tail. | `0x0057AF6C..0x0057AFD8`; vector release `0x004EAE94..0x004EAEC6` walks back to front. |
| E10 | Context destroys its RobotManager (+0x20) before RobotDataLoader (+0x1C). Loader aborts/joins before disposing its loaded member data. | manager call `0x004EABC2` → `0x0052E6D8`; loader call `0x004EABD0` → `0x0051F0C6`; loader's nonzero thread-handle gate `0x0051F0D0..0x0051F0EA`, dmb/store/dmb +0x278 and join at `0x0051F0D8..0x0051F0E6`. |
| E11 | Only after context returns: engine+0x30 UiMessageHandler exchanged to null and destroyed. | `0x004EC704..0x004EC710`; slot `0x0102FE20` → `0x006601A0` → body `0x0066011C`, then delete. |
| E12 | UiMessageHandler's G2E signal map dies before E2G signal map; both die after all the Robots and loader. Signal destruction releases its lifetime-token shared owner. | maps UI+0x38 and +0x20 at `0x0066016E..0x00660184`; E2G node destruction `0x0066349A..0x006634E4`, G2E counterpart `0x00663620..0x0066364C`. |
| E13 | engine+0x18 Json::Value destroyed, then inherited subscription vector engine+4; no other destructible base/member remains in normal return. | `0x004EC712..0x004EC722`; Json destructor `0x008E8456`, vector `0x004EAE94`. |
| E14 | Deleting engine destructor calls complete destructor, then frees engine storage. | `0x004EC79C..0x004EC7AA`. |

## M5-055 answer

**The UiMessageHandler and its signals are still alive when the Robots' DrivingAnimationHandler and AnimationStreamer subscriptions are released. The weak locks succeed on this normal engine shutdown path, and the subscribed nodes are unlinked.** This includes the streamer's tag-B4 subscription in its base tail. The signal's guard is its own lifetime token, rather than a weak pointer to the UiMessageHandler itself; M5-056 below establishes that distinction.

Answers item 1 of `re-analysis/research/requests/20261012-m5-teardown-reads.md`. Updated with the requested `git pull` first (fast-forward to `7655408`). Read PROJECT_STATE, research rules, the current three records, `20261012-m5-settling-2.md`, and `20261012-m5-x9-extraction.md` §4a/§4b. This report assigns no fidelity status and changes no manifest, inventory, production code or project state.

The engine's complete ordered destructible-member list is E02 → E03 (+0x50, then +0x40) → E04 → E05 → E06 → E11 → E13 (+0x18, then inherited +4). This includes compiler-emitted member/base cleanup after the body; ignoring the final base subscription vector would omit a release. Virtual owner slots are nulled before the call and skipped when null. The deleting wrapper E14 runs only after this list.

Within E06, the context first removes all Robots. Only later does it dispose context members in descending ownership order: +0x40 allocation; +0x3C PerfMetric; +0x38 experiments object and its inlined member cleanup; +0x34 NeedsManager; virtual-owned +0x30, +0x2C, +0x28; +0x24 VizManager's subscription vector/UdpClient and allocation; +0x20 RobotManager; +0x1C RobotDataLoader; +0x18 and +0x14 allocations; +0x10 string→bool feature tree and allocation; +0x0C virtual-owned object. The entire sequence is `0x004EAAC2..0x004EAC14`. This is relevant ordering evidence, not a recovery of every higher-layer destructor's internals. Notably, RobotManager's destructor is **not** the call that deletes the Robots here: RemoveRobots already did so, before the context's member cleanup.

Loader condition: when loader+0x274 thread handle is nonzero, store 1 to loader+0x278 between dmb instructions and join that thread; then destroy thread object, mutex +0x26C, and loaded data members (`0x0051F0EA..0x0051F238`). If the handle is zero, no abort store or join occurs, but member cleanup still does. This happens **after Robot deletion and RobotManager destruction**, with UiMessageHandler still alive. The engine does not begin shutdown by setting the loader abort flag.

UiMessageHandler's own normal destructor first deletes its +4/+8/+0xC owned interfaces, then +0x68 member, +0x64 mutex, +0x58 E2G queued-message vector, +0x4C G2E queued-message vector, G2E base map +0x38, E2G base map +0x20, and inherited subscription vector +0x10 (`0x0066011C..0x00660192`). Neither signal map is destroyed by VizManager::Disconnect or by context deletion. After map destruction, an independently retained subscription can only take the failed weak-lock path; that is a different lifetime case from the Robot releases above.

**UNKNOWN:** E05's unused-looking engine+0x38 member's concrete type and virtual target. The constructor and inspected engine paths keep it null; the destructor's conditional slot and its position are established regardless. No conclusion about hypothetical external mutation of that pointer is made. Higher-layer context destructor internals and exceptional/concurrent shutdown are outside this bounded normal-order answer. There is no remaining signal-lifetime ordering ambiguity for the requested Robot shutdown path.

| M5-056 row | Coverage: E2G factory and last release | Native citation |
|---|---|---|
| S01 | E2G Subscribe widens its uint16 tag, looks up/creates a signal in UI+0x20, copies the callback, calls container factory. | `0x006626CC..0x0066271A` → map lookup `0x00663F98`, factory `0x0066411C`. |
| S02 | Signal layout: +0 sentinel/list anchor, +4/+8 shared lifetime-token pointer/control block. Token is nonnull 0x12345678, with a 16-byte control block. | signal construction `0x006642E6..0x00664318`; token allocation `0x0066432C..0x0066434E`; token deleter `0x0066446A: bx lr`. |
| S03 | Lazy empty-list initialization allocates 0x28-byte sentinel, empty callback, refcount 2 and self links. Existing anchor unchanged. | `0x0066436C..0x006643C4`. |
| S04 | Factory allocates 4-byte ScopedHandleContainer, 0x14-byte ProtoHandle, and callback node (0x28 bytes). | `0x0066412C..0x00664140` → `0x00664684..0x006646B2`. |
| S05 | Callback node has next +0, previous +4, function +8, node refcount +0x20=1; inserts before sentinel (at list tail). | `0x00664692..0x006646B0`. |
| S06 | Handle layout: vptr +0, signal address +4, node +8, weak-token pointer +0xC, weak control +0x10. Temporarily add shared token owner, add weak for handle, construct shared container, release temporary shared owner. | `0x00664144..0x00664188`; ProtoHandle vptr `0x0102FFB8`. |
| S07 | Returned shared_ptr is pointer/control pair. Its 16-byte control block stores container at +0xC, owners-minus-one at +4/+8 initialized zero. | `0x004EF088..0x004EF0AC`; vptr `0x0101E8F8`, on_zero_shared slot `0x0101E900` → `0x004EF184`. |
| S08 | Last shared release decrements shared owners; old==0 invokes on_zero_shared, then releases implicit weak owner, and if last weak calls on_zero_shared_weak. | shipped libc++_shared.so `0x0005ED6C..0x0005EDC0`; weak release `0x0005EDC2..0x0005EDE6`; engine weak-zero target `0x004EF1B4` → delete. |
| S09 | on_zero_shared calls container destructor then frees container. Container destructor calls handle vtable+8 unlink, then handle vtable+4 deleting destructor. | `0x004EF184..0x004EF196`, container `0x004EF1B8..0x004EF1D0`. |
| S10 | Unlink tests weak control, locks it, copies token pointer and immediately releases temporary shared lock; then checks nonnull token, signal anchor, and searches list for exact node identity. | `0x00664708..0x0066473C`; lock semantics libc++ `0x0005EDE8..0x0005EE22`. |
| S11 | On matching node: destroy callback first, clear function owner, patch next.previous then previous.next, decrement node refcount, free on zero. | `0x0066473E..0x00664742` → `0x006634F4..0x00663562`. |
| S12 | Handle destructor releases token weak reference and frees handle; it does not itself repeat unlink. Nondeleting destructor releases weak only. | deleting `0x006646E4..0x00664702`; nondeleting `0x006646C4..0x006646DC`. |
| S13 | Signal destruction walks/unlinks nodes, releases sentinel twice, then its token shared owner; only then hash node storage is freed. | `0x006634B8..0x006634E4`, hash deallocate `0x0066349A..0x006634B6`. |
| S14 | G2E has same allocation/link/release algorithm, different typed callbacks, map, vtable and specialized helper addresses. | Subscribe `0x00662750` → `0x006648CC`; sentinel `0x00664B1C`; node `0x00664E34`; unlink `0x00664EB8..0x00664EF4`; node release `0x0066365C..0x006636CA`; destructor `0x00664E74` / `0x00664E94`. |

## M5-056 answer

The returned container is an independently shared owner of a ProtoHandle, which owns a **weak lifetime token plus raw signal/node addresses**. It does not hold the signal alive and is not itself a linked signal node. Container, ProtoHandle, callback node and container control block are four distinct allocations (4, 20, 40, 16 bytes), plus the lazy sentinel/token allocations when a signal is created.

Signal linkage, with `H` the sentinel and `N` the new callback node: set N.next=H, N.previous=H.previous, previous.next=N, H.previous=N. This preserves callback registration order. The sentinel starts with refcount 1 and receives another increment in lazy initialization, giving 2. Ordinary appended callback node starts at 1; no node-refcount increment is added just for its ProtoHandle.

The token's +0xC copied into a handle is the dummy nonnull pointer 0x12345678, **not** a UiMessageHandler or signal pointer. Its custom deleter is a no-op. It exists to make weak-lock success correspond to signal lifetime. libc++ weak lock refuses shared-owners value -1; otherwise atomically increments and returns the control block. E2G unlink drops that temporary strong owner **before** dereferencing the raw signal address. Preserve that order in the extracted inventory; replacing it with a guard held throughout traversal would assert different synchronization.

On last container shared release, S08–S12 execute in order: invoke container on_zero_shared → unlink → destroy handle's weak token reference/free handle → free four-byte container → release container control block's implicit weak owner/free control block if no weak owners remain. If token/control is absent, expired, signal anchor absent, list empty, or node already absent, unlink returns without touching a node. Handle destruction still releases any weak reference. The handle destructor alone does not unlink; the container's explicit virtual unlink call is essential.

Node removal destroys the callback before patching its neighbors. Function destruction chooses inline vtable+0x10 versus heap vtable+0x14, sets the stored function pointer null, then repairs next.previous and previous.next. Node refcount decrement/free follows. The same node-release helper can destroy a remaining function when needed, but normal unlink has already cleared it. This ordering also accounts for callback-capture destructors having effects before list unlink finishes.

E2G versus G2E:

| Property | E2G | G2E |
|---|---|---|
| UiMessageHandler map | +0x20 | +0x38 |
| Factory | 0x0066411C | 0x006648CC |
| ProtoHandle vptr | 0x0102FFB8 | 0x01030000 |
| Unlink virtual target | 0x00664708 | 0x00664EB8 |
| Node unlink/deref helpers | 0x006634F4 / 0x0066352E | 0x0066365C / 0x00663696 |
| Deleting handle destructor | 0x006646E4 | 0x00664E94 |
| Callback payload | AnkiEvent<MessageEngineToGame> const& | AnkiEvent<MessageGameToEngine> const& |

Allocation sizes, token ownership, pointer layouts, list insertion and weak-lock/unlink/free order match. The E2G factory is not missing a lifetime mechanism found only in G2E.

**UNKNOWN:** no unresolved normal factory/last-release step remains in this bounded chain. Arbitrary callback-capture destructor behavior belongs to each subscribing caller. No concurrency/lifetime-race guarantee or allocation-failure policy is inferred from the single-thread shutdown trace. No runtime/hardware test was performed.

| M5-058 row | Coverage: construction, copies and in-place mutation | Native citation / scan result |
|---|---|---|
| F01 | Constructor zeroes vector +0x18, frame index +0x24 and override byte +0x28 together: memclr4(object+0x18,0x11). | `0x005642C6..0x005642CC`; base constructor `0x004F8A70..0x004F8A80` writes only +0/+4/+8. |
| F02 | JSON and FlatBuf track entries construct through F01, then define members and copy into a list. No flag assignment from either input. | JSON `0x00577328..0x00577374`; FlatBuf `0x00576678..0x005766C4`; members `0x004F9434..0x004F94D4`, `0x004F96C4..0x004F9762`. |
| F03 | Process changes animation-name string (+0xC) and resets frame index +0x24; never enables override. | `0x004F9574..0x004F9644`. |
| F04 | Copy-to-new-list-node writer: object is node+8; copies source flag +0x28 to node+0x30. | `0x00564610..0x0056461A`, specifically `0x00564616: strb.w r1,[r4,#0x30]`. |
| F05 | Second copy-to-node writer preserves same source flag. | `0x0057E38C..0x0057E396`, specifically `0x0057E392: strb.w r1,[r4,#0x30]`. |
| F06 | AppendTrack copies node+0x30 flag into temporary frame+0x28 via an adjusted address; adds only trigger-time offset before copy helper. | `0x00578282` forms temp+0x24; `0x00578310` reads node+0x30; `0x00578314: strb r1,[r2,#4]` writes temp+0x28; `0x00578318..0x00578324` changes trigger then appends. |
| F07 | SetFaceAction bitmap path registers named procedural image, constructs a zero-flag FaceAnimationKeyFrame, appends it without modifying flag. ProceduralFace branch uses another keyframe type. | `0x00563E1E..0x00563E52`; constructor call `0x00563E48`; cleanup vtable sites `0x00563E58` / `0x00563FD2`. |
| F08 | CannedAnimationContainer's procedural seed constructs through F01 then appends; no flag assignment. | `0x00587CC4..0x00587CD4`; cleanup sites `0x00587D26` / `0x00587D82`. |
| F09 | IsDone/GetStreamMessage read flag. Stream operation changes vector contents/frame index, not flag; no exposed toggle here. | `0x004F976C..0x004F97AE`, `0x004F97C8..0x004F98C8`. |
| F10 | GetCurrentStreamingMessage/MoveToNext/ClearAfterTime mutate track cursor, call virtual frame consumers or erase nodes, not override flag. | `0x0057CD64..0x0057CDA0`, `0x0057F260..0x0057F27E`, `0x00577CD2..0x00577CF2`. |
| F11 | List clear/pop/erase destroy owned members and free node; list copying uses F05; splice rewires node links/counts, preserves frame bytes. | `0x00523AB8..0x00523B1E`, `0x005794F4..0x00579544`, `0x00579D54..0x00579DA4`, `0x0057E2F8` → F05, `0x00588DFE..0x00588E3E`. |
| F12 | Reproduced all 17 GOT references over .text; literal raw vptr-word matches occur only in relocation metadata, not code. | `.text 0x004D6860..0x00AE3682`; GOT `0x0103E9D8` resolves vtable base `0x0101F0B0`, installed pointer `0x0101F0B8`; scan artifacts below. |
| F13 | Whole-text direct +0x28 str/strb/strh candidate scan (624 raw candidates) plus 56 named/reference-owner/direct-caller disassembly entries; relevant candidates are unrelated objects/stack or data. Shifted-pointer writes checked separately, including F06. | `20261012-m5-face-writer-scan.json`; examples `0x00563AD0` is cv::Mat zero cleanup; `0x00578A5C` is literal-pool data; `0x00580B22` is middle of the instruction beginning `0x00580B20`, not a store. |

## M5-058 answer

**Never written non-zero natively on the recovered, well-formed FaceAnimationKeyFrame production paths.** Beyond constructor initialization there are three flag-copy stores: F04, F05 and F06. They preserve an existing source byte; none originates a true/nonzero value. Calling the field “never written” without this qualification would be wrong.

The propagation argument is source-based: every fresh native face frame in JSON loading, FlatBuf loading, SetFaceAction and the canned procedural seed uses constructor F01, which zeroes +0x28. Parsing/process/stream consumers do not change the byte. Node copy and AppendTrack preserve it. Splice transfers nodes unchanged. Clear/pop/erase destroy objects rather than reinitialize the flag. Inductively, copying/appending these natively constructed frames cannot enable the override. `GetStreamMessage`'s +0x28 branch remains present, but native reachable frames use named FaceAnimationManager lookup at `0x004F97EA..0x004F9816`. The existence of the branch or the stored vector cache is not evidence of a native enabling writer.

All 17 GOT reference loads, with role:

| Load instruction | Owner / role |
|---|---|
| 0x00523ADC | list clear, destructor vptr reset |
| 0x00563E58, 0x00563FD2 | SetFaceAction::Init normal/exception cleanup |
| 0x0056429A | sole fresh constructor |
| 0x005645C2 | list emplace copy |
| 0x005766C6, 0x005766FE | FlatBuf AddKeyFrame normal/exception cleanup |
| 0x00577376, 0x005773AE | JSON AddKeyFrame normal/exception cleanup |
| 0x0057828E, 0x005782A6, 0x005783D8 | AppendTrack copied temporary and normal/exception cleanup |
| 0x005794F8 | pop_back destructor |
| 0x00579D58 | erase destructor |
| 0x0057E33E | list push_back copy |
| 0x00587D26, 0x00587D82 | CannedAnimationContainer seed normal/exception cleanup |

The scan independently reconstructs PC-relative literal-load/add references rather than trusting Ghidra's vtable names. Installed vptr is **0x0101F0B8**, not the vtable symbol's ABI header at 0x0101F0B0. Aligned raw matches for 0x0101F0B8/0x0101F0C0 at file offsets 0x00442B58/0x00474798 are entries in `.rel.dyn` (file range 0x0043D6C8..0x00482540), not additional construction sites.

The direct-store scan is a candidate inventory, not a proof by itself: it deliberately includes literal data and second halves of 32-bit Thumb instructions, and +0x28 is common in unrelated classes. The selected-function scan can also cross decompiler's fragmented extents or stop at data. Manual instruction-aligned reads above settle the relevant candidates and the adjusted-base stores that a textual `#0x28` search would miss. In particular F06 is a real writer missed by such a textual search, and F04/F05 write +0x30 relative to a list node. The argument does not rely on finding no literal `strb [rX,#0x28]`.

**UNKNOWN / limits:** no native enabling writer remains unidentified in the traced construction/copy/consumer closure. This is not a claim about corrupted memory, malformed unchecked FlatBufs, externally fabricated objects, or arbitrary writes through invalid pointers. Those cases cannot justify a valid native flag-setting path. No differential emulator execution or hardware test was performed; the conclusion rests on static native control/data flow and the reproducible scan, for manager checking.

## Evidence and disposition

All primary addresses above refer to `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`, except explicitly labeled libc++ addresses. Shipped `libc++_shared.so` SHA256 is `8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a`. Thumb entry bits are cleared. Native evidence: `20261012-m5-teardown-native.txt`, `20261012-m5-teardown-libcpp-native.txt`, and resolved slots `20261012-m5-teardown-vtables.txt`; helper scripts and JSON/text scan outputs share this report's research directory. Disassembly of pools is diagnostic noise, not evidence for behavior.

Current records were read at pulled HEAD: M5-055 “~CozmoEngine member destruction order (RobotDataLoader abort flag, robot and streamer teardown, weak-signal unlinks)”; M5-056 “The engine-to-game subscription container factory and its ProtoHandle (UiMessageHandler::Subscribe(EngineToGameTag))”; M5-058 “FaceAnimationKeyFrame +0x28 writers (the face-animation override)”. All three are currently RECOVERABLE_GAP. Their evidence respectively leaves engine order unread, factory/release unread, and an in-place writer not ruled out. This report supplies the bounded reads; it does not change those classifications or claim the stack reproduces them.

No current-record contradiction is asserted. One navigation limitation: the requested settling-2 document has no actual D7 paragraph/site list in this checkout; the current M5-058 manifest quotes D7's result. The independent scan reproduced its 17-site count and the report enumerates those sites directly.

No implementation, regression test, branch, worktree, commit, shipped-file upload or hardware run was made. Research stops with this answer.
