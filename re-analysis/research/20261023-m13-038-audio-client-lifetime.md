| Item | Coverage | What the original does / narrow relevance check | Address with 1–3 pasted instructions | Record |
| --- | --- | --- | --- | --- |
| 1. Client-pointer writers | CHECKED | Relevant: replacement would change recipients and cached posting decisions. Found construction, destruction and A's failed-construction cleanup; no live replacement/reset in the checked owner graph. | `0x005A091E: str r7,[r4,#0x68]`; `0x005A0ACC: str r0,[r4,#0x68]`; `0x005A588E: str r6,[r4,#0x10]`. Complete writer table below. | M13-038 |
| 2. Owner lifetime | CHECKED | Relevant: removing the Robot retires A then B; rebuilding a Robot creates fresh owners. No independent owner reload during an ongoing connected Robot lifetime. Context destruction also removes registered Robots, without requiring a disconnect first. | `0x0051112C: blx #0x4a7978`; `0x0051113A: blx #0x4a796c`; context teardown `0x004EAABE: blx #0x4a4e94`. | M13-038; M1-044 / M5-055 interfaces |
| 3. Unsubscribe and dispatch | CHECKED | Relevant: reverse handle release disconnects the public-state slot before deleting the client. Future valid traversals cannot invoke that retired slot. An already executing callback is not cancelled; node retention does not retain the client or guarantee safety for arbitrary reentrant owner destruction. | `0x004EAEAE: blx #0x4a4ef4`; `0x0051942E: str r1,[r4,#0x18]`; dispatch gate `0x0064E66A: cbz r0,#0x64e676`. | M13-038; M1-034 fault boundary |
| 4. Unlock writer and repeated activation | CHECKED | Relevant: ordinary Update stores the new unlock before copying/publishing it. With both clients eligible for unlock1D and a changed stage, nested activation order is A, B, then resumed outer A: three calls total, A twice. | `0x0064D610: str r1,[r0]`; `0x00592424: blx #0x4af034`; resumed activation `0x00592478: blx #0x4af040`. | M13-038 / M13-039 |

# R-M13-038-LIFETIME — queue row 58

Answers `re-analysis/research/requests/20261023-m13-038-audio-client-lifetime.md`. Extraction only. CHECKED is coverage of the requested ordinary native lifetime/dispatch/state-writer questions, not fidelity settlement, an implementation review, or a claim that every hypothetical reentrant destruction is safe.

## Source and limits

Read PROJECT_STATE.md, AGENTS.md, research/README.md and the complete request; pulled with rebase to `dc12b22c` before tracing. Re-read the row57 answer/check and the OV69/74/75/77 check as leads, then checked native instructions independently. Primary source throughout: `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. No decompiled statement is cited as evidence.

Ran `known.py` on each function address before tracing; also ran it for unindexed cleanup windows. Used the installed Python313 executable because the short `python` launcher was inaccessible earlier in this checkout. For the owner/state-writer census, all 76 non-thunk index entries whose names start exactly with `Anki::Cozmo::BehaviorManager::`, `BehaviorSystemManager::` or `PublicStateBroadcaster::` were individually queried, then native store/access candidates inspected. This includes the out-of-line FindBehaviorByIDAndDowncast template instances. Candidate discovery in pseudo-C was navigation only. The native census distinguishes stores to the owner, subobjects, event copies and stack locals; an equal numeric offset is not automatically the same field.

Important indexed boundaries, entry:size in bytes: BehaviorManager ctor `005A0864:524`, dtor `005A0D22:216`, InitConfiguration `005A0DFC:382`; BehaviorSystemManager ctor `005A57E6:78`, dtor `005A5886:78`, InitConfiguration `005A58D4:58`; Robot ctor `0050FBF0:2060`, dtor `005110D4:1318`, Update `00513BC8:2270`; AddRobot `0052EB70:980`, RemoveRobot `0052F1A4:450`, RemoveRobots `0052F428:76`, HandleDisconnectMessage `0062FA18:256`, context dtor `004EAAAC:362`; client ctor `00591E38:304`, Init `00591FC0:2`; vector dtor `004EAE94:52`, zero-shared callback `004EF184:20`, scoped-container dtor `004EF1B8:26`; signal disconnect `0064E618:62`, slot removal `0051940E:58`, node release `00519448:54`, emitter `0064E656:70`; broadcaster ctor `0064D56C:68`, Update `0064D5DC:718`, stage setter `0064DA44:328`; spark handler `005923D4:172`, activation `00592190:80`, main handler `005921EE:108`, round helper `00591FC4:70`, workout choice `00573E30:80`.

Known.py reports no function boundary for cleanup windows around `005A0AC8`, `005A5834`, `00591F68`, `00510910`, `005118B0`, or callback window `00592B1E`. These are instruction windows only. The valid cleanup instruction starts checked near the last two are `00510912` and `005118B2`; no boundary was inferred from a neighbouring odd symbol value. PLT veneers are ARM, bodies Thumb. Symbols such as `005A0D23` denote Thumb entry `005A0D22`, not an instruction one byte later.

Notation: R is the native Robot; A is BehaviorManager's client at manager+68; B is BehaviorSystemManager's client at manager+10. S is the broadcaster's pointed-to public state; C is one client's independent state. The broadcaster itself is R+40; its current S pointer is broadcaster+0.

## 1. Client pointers: all identified writers and ordinary persistence

| Step | Native action | Address with pasted instructions | Record |
| --- | --- | --- | --- |
| W1 | A constructs one 0x38-byte client with R, then writes the completed pointer to owner+68. | `0x005A0912: mov r1,r6`; `0x005A0914: blx #0x4b0dc8`; `0x005A091E: str r7,[r4,#0x68]`. | M13-038 |
| W2 | A destructor loads pointer and clears owner+68 before destroying client. r6 was set to zero at 005A0D42. | `0x005A0D8E: ldr r7,[r4,#0x68]`; `0x005A0D90: str r6,[r4,#0x68]`; `0x005A0D92: cbz r7,#0x5a0da2`. | M13-038 |
| W3 | A constructor cleanup also clears owner+68, releases client handles, then deletes client. This is not a normal reset/reload. | `0x005A0ACA: movs r0,#0`; `0x005A0ACC: str r0,[r4,#0x68]`; `0x005A0AD4: blx #0x4a4e4c`. | M13-038 |
| W4 | B constructs an independent client with the same R, then writes owner+10. | `0x005A5826: mov r1,r5`; `0x005A5828: blx #0x4b0dc8`; `0x005A582C: str r6,[r4,#0x10]`. | M13-038 |
| W5 | B destructor clears owner+10 before releasing handles/deleting client. | `0x005A588A: movs r6,#0`; `0x005A588C: ldr r5,[r4,#0x10]`; `0x005A588E: str r6,[r4,#0x10]`. | M13-038 |
| W6 | A InitConfiguration uses the retained client and calls Init; it does not replace it. | `0x005A0F68: ldr r0,[r4,#0x68]`; `0x005A0F6A: blx #0x4b0e94`; `0x00591FC0: bx lr`. | M13-038 |
| W7 | B InitConfiguration likewise calls the existing client's no-op Init. It can replace activity+0C, not client+10. | `0x005A58E2: ldr r0,[r6,#0x10]`; `0x005A58E4: blx #0x4b0e94`; `0x005A5900: str r1,[r6,#0xc]`. | M13-038 |

Writer inventory for the actual client-pointer fields is therefore A: `005A091E`, `005A0D90`, `005A0ACC`; B: `005A582C`, `005A588E`. No other writer found in the symbol-identified owner graph and checked cleanup/call-site census. In particular, B's `005A5818: strd r6,r7,[r4,#8]` writes owner+8/+C, not +10. A's shifted-register stores during construction initialize other fields, not +68. False immediate matches such as `005A148E: str r7,[sp,#0x68]` are stack locals, not owner writes.

B's completed client store is followed only by returning the owner (`005A582E: mov r0,r4; 005A5830: add sp,#4; 005A5832: pop {r4,r5,r6,r7,pc}`). Its constructor-failure cleanup `005A5834: mov r5,r0; 005A5836: mov r0,r6; 005A5838: blx #0x4a40cc` frees the failed allocation; there is no post-store partial B client to reset. Client constructor cleanup itself releases its already-built handle vector at `00591FA4: mov r0,r4; 00591FA6: blx #0x4a4e4c`, before unwinding (`00591FAC: blx #0x4a40a8`). Thus failed construction must not be counted as an additional persistent subscriber.

The narrow result for configuration reload is **no effect on client identity or subscription lifetime**: W6/W7 and the two-byte Init body settle that question without extracting every behaviour/activity configuration. This is not a claim that mode/activity changes have no behavioural or audio effect; their public-state inputs can still change what the same clients post.

## 2. Owners: construction, destruction, reconnect and shutdown

| Step | Native action | Address with pasted instructions | Record |
| --- | --- | --- | --- |
| O1 | R constructs broadcaster first, then manager A and manager B, passing the same R. | `0x0050FC9A: blx #0x4a766c`; `0x0050FCAC: blx #0x4a7678`; `0x0050FCBE: blx #0x4a7684`. | M13-038 |
| O2 | Retain A at R+44; B at R+48 through an adjusted r1=R+4C. | `0x0050FCB0: str r4,[r5,#0x44]`; `0x0050FCC6: str r0,[r1,#0x4c]!`; `0x0050FCD0: str r6,[r1,#-0x4]`. | M13-038 |
| O3 | R destructor clears R+44, destroys/deletes A, then clears R+48 and destroys/deletes B. | `0x00511128: str r5,[r4,#0x44]`; `0x0051112C: blx #0x4a7978`; `0x0051113A: blx #0x4a796c`. | M13-038 / M1-044 |
| O4 | Later member cleanup checks those already-null owner fields again, B then A. Ordinarily it skips, not a second destruction. | `0x0051157C: str r5,[r4,#0x48]`; `0x0051157E: cbz r0,#0x511588`; `0x0051158C: cbz r0,#0x511596`. | M13-038 |
| O5 | Constructor cleanup clears/destroys already-constructed B then A. | `0x00510916: str r1,[r5,#0x48]`; `0x0051091A: blx #0x4a796c`; `0x0051092A: blx #0x4a7978`. | M13-038 |
| O6 | Additional R cleanup window likewise clears B/A before invoking their destructors. This is cleanup, not reconstruction. | `0x005118B6: str r6,[r4,#0x48]`; `0x005118BA: blx #0x4a796c`; `0x005118C8: blx #0x4a7978`. | M13-038 |
| O7 | AddRobot with absent ID allocates a new R and invokes its constructor; the existing-ID branch bypasses that allocation/constructor. | `0x0052EBB2: beq.w #0x52ed28`; `0x0052EE92: blx #0x4a42a0`; `0x0052EE9C: blx #0x4a9c1c`. | M1-029 interface / M13-038 |
| O8 | Disconnect handler calls RemoveRobot when it has a current R. | `0x0062FAEA: cbz r6,#0x62fafc`; `0x0062FAEC: ldr r1,[r6,#0x10]`; `0x0062FAF8: blx #0x4b8c34`. | M1-015/025 interface |
| O9 | RemoveRobot's found-ID path destroys/deletes the registered R, then erases its membership. Its connection-handshake branch rejoins before this destruction. | `0x0052F2F2: ldr r0,[r5,#0x14]`; `0x0052F2F6: blx #0x4a9c94`; `0x0052F302: blx #0x4a9ca0`. | M1-044 interface / M13-038 |
| O10 | Context destructor unconditionally calls RemoveRobots; it need not first receive a disconnect message. RemoveRobots destroys each registered R. | `0x004EAABE: blx #0x4a4e94`; `0x0052F438: ldr r0,[r0,#0x14]`; `0x0052F43C: blx #0x4a9c94`. | M5-055 interface / M13-038 |

The complete native owner constructor/destructor direct-call census over `.text` finds:

- Owner construction only at `0050FCAC` (A) and `0050FCBE` (B).
- A destruction at `0051092A`, `0051112C`, `0051158E`, `005118C8`; B at `0051091A`, `0051113A`, `00511580`, `005118BA`.
- R construction only at `0052EE9C`; R destruction at `0052F2F6` and `0052F43C`.
- RemoveRobot caller `0062FAF8`; RemoveRobots caller `004EAABE`.

These were decoded halfword-aligned Thumb BL/BLX/B.W candidates to body entries and PLTs, with a separate word-aligned ARM branch scan (no additional caller). Relevant ELF PLT resolutions: `004A7678→005A0865`, `004A7684→005A57E7`, `004A7978→005A0D23`, `004A796C→005A5887`. The indexed functions and adjacent cleanup windows account for every identified owner destruction. No owner vtable was found in dynamic symbols; these are direct nonvirtual destruction paths, not a missed virtual reset interface.

This establishes the ordinary shipped lifetime: both clients persist through a live R's activity/configuration changes. Reconnect after removal constructs a new R, both new owners and fresh client caches/subscriptions. Shutdown can destroy the owners while a Robot is still registered (no prior physical-disconnect prerequisite at O10); it ends that engine/Robot lifetime. No path found rebuilds just the managers/clients while preserving the same continuing R connection. Failure cleanup is distinct from a successfully connected session. This is a typed native ownership/caller census, not a proof about arbitrary corrupt/externally written pointers.

## 3. Retained handles and what removal guarantees

| Step | Native action | Address with pasted instructions | Record |
| --- | --- | --- | --- |
| U1 | Client captures this in its unconditional broadcaster subscription, then moves returned handle into vector C+1C/+20/+24. | `0x00591EFC: str r5,[sp,#0x14]`; `0x00591F08: blx #0x4aef98`; `0x00591F1E: strd r0,r0,[sp,#8]` (zero moved-from handle). | M13-038 |
| U2 | A teardown clears owner pointer, destroys C+1C vector, then deletes C; B does the same. No separate out-of-line client destructor is required to see this compiled destructor body. | A `0x005A0D94: add.w r0,r7,#0x1c`; `0x005A0D98: blx #0x4a4e4c`; `0x005A0D9E: blx #0x4a40cc`. | M13-038 |
| U3 | B's corresponding sequence. | `0x005A5892: add.w r0,r5,#0x1c`; `0x005A5896: blx #0x4a4e4c`; `0x005A589C: blx #0x4a40cc`. | M13-038 |
| U4 | Vector destructor walks backward by 8-byte shared_ptr entries, releasing each control block, then frees vector storage. | `0x004EAEA2: sub.w r1,r0,#8`; `0x004EAEA8: ldr r0,[r0,#-0x4]`; `0x004EAEAE: blx #0x4a4ef4`. | M13-038 / M1-046 interface |
| U5 | Last-shared-ref callback destroys the ScopedHandleContainer; it calls ProtoHandle virtual+8 (disconnect), then virtual+4 (delete handle). | `0x004EF18A: bl #0x4ef1b8`; `0x004EF1C0: ldr r1,[r1,#8]`; `0x004EF1CC: blx r1` (delete after disconnect call at 004EF1C2). | M13-038 |
| U6 | Broadcaster ProtoHandle checks signal lifetime via weak lock, finds its exact slot, and tail-calls slot removal. If signal lifetime is gone it returns. | `0x0064E620: blx #0x4a556c`; `0x0064E642: cmp r2,r0`; `0x0064E652: b.w #0x51940e`. | M13-038 |
| U7 | Slot removal destroys callback, clears function pointer, unlinks both neighbours, then releases its node reference. | `0x0051942E: str r1,[r4,#0x18]` (r1=0); `0x00519434: str r1,[r0,#4]`; `0x0051943C: str r1,[r0]`. | M13-038 |
| U8 | Emitter checks callback presence immediately before calling; after callback it reads next and retains that node before releasing current. | `0x0064E66A: cbz r0,#0x64e676`; `0x0064E672: blx #0x4ba008`; `0x0064E676: ldr r7,[r6]`. | M13-038 |
| U9 | Current/next slot retention is node refcounting, not client ownership. Last node ref frees slot. | `0x0064E666: str r0,[r6,#0x20]`; `0x00519454: str r0,[r4,#0x20]`; `0x0051947A: b.w #0x8ca88c` (delete veneer). | M13-038 |

The public-state handle is appended after the optional G2E0xA3 handle in the client constructor, so reverse destruction disconnects public state first, then G2E if present. Client handles are retained by the client's vector, not handed to the application as another persistent owner. The subscription return temporary is moved/zeroed at U1; the growth branch is `00591F2E: blx #0x4a5008`. Failed client construction also releases the vector (W section).

Concrete vtable evidence: subscription handle creation stores signal pointer and exact slot (`0064E06C: strd r5,sl,[r7,#4]`) and installs ProtoHandle vtable via GOT `0103F8A8`, resolving vtable `0102F738`, address point `0102F740`. RELATIVE word `0102F748=0064E619` makes virtual+8 the checked disconnect routine. Virtual+4 word `0102F744=0064E5F5` is its deleting-destructor entry. Callback address-point slot `01023D98=00592B1F`; the callback window `00592B1E: ldr r0,[r0,#4]; 00592B20: adds r1,#0xc` loads a **raw client pointer**, not a shared client reference.

For a completed ordinary owner destruction, future valid public-state emissions do not call that destroyed client: its slot has been cleared and removed before client allocation deletion. An emitter already positioned on a retained but disconnected slot skips its empty function. Removing a future slot while the current slot remains linked updates the current ring link so it is bypassed.

There is no blanket in-flight lifetime guarantee. If a client is destroyed while its handler is already on the stack, unlinking cannot cancel the executing instructions or extend the raw client's lifetime. Further, an unlinked current node retains its old next link; if that successor is then disconnected/freed, `0064E676: ldr r7,[r6]` followed by `0064E678: ldr r0,[r7,#0x20]` can read a freed successor. U7 rewrites neighbours, not the removed node's own next field. This is undefined original behaviour, not a defined late notification to reproduce. M1-034's existing skip-and-log rule and M13-038's already-recorded freed-successor boundary apply.

**UNKNOWN: a shipped ordinary production trigger for deleting these owners from inside one of these audio callbacks.** Checked the identified owner/Robot destructor callers, the callback's raw capture and the ring removal/walk; no such trigger was found. This report does not turn that absence into a proof that arbitrary nested audio/host callbacks are safe, and does not invent a cancellation event. Normal lifetime/retirement above is settled at the subscriber boundary; exhaustive deep audio reentrancy is outside this request.

## 4. Broadcaster unlock writers and activation order without SDK mode

| Step | Native action | Address with pasted instructions | Record |
| --- | --- | --- | --- |
| S1 | Broadcaster ctor initializes S+0=55. Each client starts active-unlock+4 and cached-unlock+8 at55, music-switch+C=0. No constructor publication. | `0x0064D592: movs r1,#0x55`; `0x0064D59C: str r1,[r0]`; client `0x00591E56: strd r0,r1,[r5,#8]` (r0=55, r1=0). | M13-038 |
| S2 | Ordinary R::Update reaches broadcaster.Update with R. This is the normal tick, not an SDK-mode entry. | `0x00514474: ldr r0,[r4,#0x40]`; `0x00514476: mov r1,r4`; `0x00514478: blx #0x4a7f6c`. | M13-038 producer interface |
| S3 | Read current unlock C=manager+58 and requested Q=manager+60; compare current to S+0. | `0x0064D5F0: ldr r1,[r2,#0x58]`; `0x0064D5F2: ldr r2,[r2,#0x60]`; `0x0064D606: cmp r1,r3`. | M13-038 |
| S4 | If C differs from S+0 and NOT(Q==55 and C!=Q), write C to S+0 before logging and before publication. | `0x0064D60C: beq #0x64d65c`; `0x0064D60E: cbnz r6,#0x64d65c`; `0x0064D610: str r1,[r0]`. | M13-038 |
| S5 | If Q==55 and C!=Q, and S+0!=55, cancellation writes55 and marks dirty. This clears client spark state rather than activates it. | `0x0064D668: beq #0x64d6ba`; `0x0064D6B4: movs r1,#0x55`; `0x0064D6B8: str r1,[r0]`. | M13-038 |
| S6 | Dirty Update copies the now-current S, then emits synchronously. S+0 is copied, not replaced with an old unlock. | `0x0064D870: ldm r3!,{r7}`; `0x0064D872: stm r2!,{r7}`; `0x0064D8A0: bl #0x64de5e`. | M13-038 |
| S7 | Signal slots appended in construction order A then B; emit invokes callback before continuing to next. | `0x0064E5BC: str r5,[r0]`; `0x0064E5C0: str r5,[r4,#4]`; `0x0064E672: blx #0x4ba008`. | M13-038 |
| S8 | Handler compares incoming unlock to client's active+4. For a different non55 unlock, music-switch+C must be nonzero. | `0x005923E0: cmp r3,r1`; `0x005923F4: cmp r1,#0x55`; `0x00592408: cbz r3,#0x59242e`. | M13-038/039 |
| S9 | For unlock1D, shared workout choice is cached before returning, then stage(3,choice) is requested before Activate. | `0x00573E72: strb r0,[r4,#0x11]`; `0x00573E74: strb r5,[r4,#0x10]`; `0x00592424: blx #0x4af034`. | M13-038; M13-010 decision interface |
| S10 | If stage already equals requested tag/value, warn and do not emit. Otherwise update stage and create a fresh state snapshot; no S+0 write. | `0x0064DAAC: cmp r0,r8`; `0x0064DB3C: strb r5,[r1,#0x16]`; `0x0064DB86: bl #0x64de5e`. | M13-038 |
| S11 | Activate writes active-unlock+4 before round/switch/music requests. Outer handler does not reach this store until its stage publication returns. | `0x0059219A: str r1,[r5,#4]`; `0x005921A6: blx #0x4aefbc`; `0x005921C2: blx ip`. | M13-038/039 |
| S12 | Outer A resumes, reloads music-switch and incoming unlock, then activates without repeating active-unlock comparison. | `0x00592428: ldr r3,[r4,#0xc]`; `0x0059242A: ldr r1,[r5]`; `0x00592478: blx #0x4af040`. | M13-038 |
| S13 | Client cached-unlock+8 is written only at handler end, after all helpers. It is distinct from active-unlock+4. | `0x00592204: blx #0x4aefe0`; `0x00592254: ldr r0,[r5]`; `0x00592256: str r0,[r4,#8]`. | M13-039 |

All identified S+0 writers are ctor `0064D59C` and Update `0064D610/0064D6B8`. The other checked broadcaster methods write S+14 (activity), S+5 (requesting), S+6 (height), S+16..1A (stage), or only copy current state. For example `0064D9EC: strb r1,[r2,#0x14]`, `0064DC52: strb r1,[r2,#5]`, `0064DD24: strb r0,[r1,#6]`, `0064DB3E: str.w r0,[r1,#0x17]`. Their event-copy stores target a stack event, not S+0. No SDK-only unlock writer is required or extracted here. The manager's current/requested unlock input producers remain their own interfaces; the narrow question is which actual S writer feeds both subscribers.

Under these explicit preconditions: Update accepts new current unlock1D through S4, both clients' active-unlock differs from1D, both music switches are nonzero, both remain subscribed, and requested stage(3,shared workout choice) differs from S's current stage:

| Order | What happens | Native witness | Record |
| --- | --- | --- | --- |
| 1 | Outer event E0 starts A with new unlock1D and old stage. | `0x0064D610: str r1,[r0]`; `0x0064D8A0: bl #0x64de5e`. | M13-038 |
| 2 | A publishes changed stage before storing its active unlock; nested E1 copies the already-new S+0=1D. | `0x00592424: blx #0x4af034`; `0x0064DB56: ldm r3!,{r7}`; `0x0064DB86: bl #0x64de5e`. | M13-038 |
| 3 | Nested A requests identical stage (no further publish), then activates A. | `0x0064DAAC: cmp r0,r8`; `0x0064DAC2: blx #0x4a4540`; `0x00592478: blx #0x4af040`. | M13-038 |
| 4 | Nested B does the same and activates B. Both nested handlers finish. | `0x0064E676: ldr r7,[r6]`; `0x0064E68A: bne #0x64e668`; `0x00592478: blx #0x4af040`. | M13-038 |
| 5 | Original A resumes S12 and activates A again. There is no second eligibility test. | `0x00592428: ldr r3,[r4,#0xc]`; `0x00592478: blx #0x4af040`. | M13-038 |
| 6 | Outer B now sees active unlock1D. It does not Activate, but may send the old outer snapshot's round switch and overwrite its nested round/cache. | `0x005923E0: cmp r3,r1`; `0x005923EE: blx #0x4aefbc`; `0x00591FCE: str r2,[r0,#0x14]`. | M13-038/039 |

Thus the record's "double" is A's two activations; the two-client total is **three**, ordered **A, B, A**. Each activation's request order is round update first, then switch(group03B760EF, music switch, object1), then music-state093E7E04 with false/0 (`005921A6`, `005921C2`, `005921D4`). Round switch group is972DC63C (`00591FF4: movw r1,#0xc63c; 00591FF8: movt r1,#0x972d`). These are interface requests, not proof of downstream audible output.

If stage already matches there is no nested publication: eligible A then B activate once each. A normal different non1D unlock likewise has no workout-stage recursion. If a client's music switch is0, it logs InvalidMusicState instead of activating (`00592442: blx #0x4a505c`). Cancellation55 clears active flag, music switch and round (`005923FC: strb r0,[r4,#0x10]; 005923FE: str r0,[r4,#0xc]; 00592402: str r0,[r4,#0x14]`). A height notification itself changes no unlock; it delivers the snapshot already selected by these writers and can therefore reach the same eligibility logic, not necessarily an activation.

## Current record, validation and remaining uncertainty

Re-read the current manifest, not a remembered version: **M13-038 / IMPLEMENTATION_GAP**, title **BehaviorAudioClient instances and lifetime: two clients (BehaviorManager, BehaviorSystemManager) subscribe to the public-state broadcaster; whether both stay subscribed; the re-entrant double ActivateSparkedMusic**. Its evidence names ctor subscription `00591F08`, owner construction `005A0914/005A5828`, lifetime `00511120..0051113E`, owner audio teardown `005A0D8E..005A0DA2` / `005A5886..005A589C`, Update state writer `0064D5DC..0064D6B8`, stage setter `0064DA4E..0064DB86`, and activation `00592190..005921DE`.

Its unresolved starts with the old recoverable-lifetime wording but then records the adopted 2026-10-16 result and B-M13-8: two clients until Robot teardown, A/B/A recursion, partial surrounding owners, and M1-034 freed-successor guard. The checked normal lifetime and activation conclusions agree with that adopted result. No contradicted settled status or new fidelity record is established here. M13-039 remains IMPLEMENTATION_GAP and owns handler/backend/input details; M13-010 owns the workout choice's deeper mood/RNG provenance. The current M13-038 test field is `BehaviorAudioClientCheckedRowsTests`; it was not run or used to prove source fidelity.

Changed only this research report. No code, manifest, inventory, queue or PROJECT_STATE edits; no hardware run or regression-test change. Validation: native instruction/literal/relocation checks, all 76 typed-method known.py queries, owner store census, Thumb/ARM direct-call census and report diff checks. No full production build or test suite is appropriate to this extraction-only change.

Remaining limits: no exhaustive deep-audio trigger proof for arbitrary destruction during an active callback; no claim that node references retain clients; no complete surrounding manager/container/broadcaster teardown implementation claim; no new Wwise/audio-output result, native initial needs-cache values, or manager input-producer recovery. None is reclassified as no effect. The ordinary connected-Robot ownership and callback retirement, and the exact conditional repeated activation, are the bounded results. Publication commit is reported in chat after committing this file alone; then this job stops.
