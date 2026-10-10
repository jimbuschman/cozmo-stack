# M3/M4 remaining build rows — 2026-10-09

## Coverage table

| Record | Rows below | Coverage / manager handoff |
|---|---|---|
| M4-026 | A1–A10 | Abort order, return aggregation, direct-drive unlocks, four wire message types and logs recovered. Action cancellation terminates at M8; planning/viz terminates at M11. |
| M4-027 | T1–T27 | Owner-null order, subscriptions, containers, movement locks, light callbacks and conditional logger flushing/closing recovered. Captured recipient destruction stops at its owning layer. |
| M4-028 | L1–L15 | Preset-0 child, fields, command/ack, completion and logs recovered. Parent/action scheduler and track ownership stop at M8. Platform `asinf` rounding remains a named input assumption. |
| M4-031 | D1–D3 | Nonempty-track order, tree traversal, field order and debug output recovered. Bit 7 has no enum name; populated invalid track behavior is not replaced with a guessed label. |
| M3-038 | N1–N8 | Concrete NV deleting destructor, disposal order, queued/active function destruction recovered. Backup manager stops at M15; captured-object destruction stops at its owner. |
| M3-040 | I1–I9 | Registration, append-copy, exact idle predicate, synchronous invoke-before-pop, repeated draining and update entry recovered. Ready callback body remains M1-041. |
| M3-039 | S1–S3 | SDK ResetRobot cube-sleep dispatch confirmed SDK-scoped. The shared recipient is not itself SDK-exclusive; no recipient body extracted. |
| M4-029 | S1–S4 | SDK light/lift dispatches confirmed SDK-scoped. **Only light-state is under +0x79; lift-power is outside that gate.** Shared recipient bodies not extracted. |
| M4-030 | S1–S4 | StopRobotForSdk ResetRobot dispatch confirmed SDK-scoped. A receiver-level SDK-only predicate is not established; no recipient body extracted. |

Research only. These are proposed extraction rows for manager checking, not an approved inventory, implementation or settlement. All nine current records remain RECOVERABLE_GAP. No production code, manifest, approvals or project state changed. Explicit operator instructions authorize this report's commit/push despite the default research lane's no-commit rule.

## Evidence and interpretation

Pulled main before extraction: already current at `e60205a2ecf960a30a113d0fc054412f2bbb01fd`. Read PROJECT_STATE.md, AGENTS.md (including app boundary), research/README.md and CODEX-BUILDER. Authority 1 is the actual shipped ARMv7 ELF, not prior notes or C# tests:

| Image | SHA256 |
|---|---|
| `resources/lib/armeabi-v7a/libcozmoEngine.so` | `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1` |
| `resources/lib/armeabi-v7a/libc++_shared.so` | `8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a` |

Addresses are image VAs; Thumb bit is removed. All ranges below are half-open. The appendix reopens the call targets rather than relying on symbol names. ARM PLT and Thumb→ARM tail veneers are resolved; imported definitions are searched in the packaged ELFs before a platform boundary is declared. Dynamic calls retain their slot/predicate and identified owner, or UNKNOWN. Inline literal pools and exception cleanup are not mistaken for ordinary executable fall-through.

The row labels A/T/L/D/N/I also identify the call census windows. Each census entry is accounted for by its semantic row, the call-effect ledger or an explicit layer/runtime boundary. The census is of these stated windows, not a claim that every higher-layer descendant has been extracted. Full target openings are included for checking interfaces; opening a boundary target does not extend this task into its body inventory.

## M4-026 — AbortAll

| Row | Exact recovered behavior | Primary source / boundary |
|---|---|---|
| A1 | Call **Cancel(-1), Path.Abort, Dock.AbortDocking, SendAbortAnimation, Movement.StopAllMotors**, in that order, without short circuit. Ignore Cancel's result and StopAllMotors' void result. Return `(pathResult OR dockingResult OR animationResult) != 0`; do not reinterpret as “any action canceled.” | `0051194C..0051198E`; call sites `51195A,511960,51196A,511972,51197C`. |
| A2 | Cancel recipient returns 1 immediately when ActionList+0x0C is nonzero; otherwise visits its queue tree and ORs queue cancellation results. Actual runner deletion/cancellation callbacks are M8 action ownership. | `0053DE10..0053DE5E`; reopened ActionQueue.Cancel `0053E690`. **M8 boundary**, not a blanket callback-free reset. |
| A3 | Path.Abort first emits Planner INFO `PathComponent.Abort`, `Aborting from status '%s'`. Optional planner+0x20 receives virtual +0x10; only that branch clears byte+0x47. Release shared path pair+0x30/+0x34 after nulling it, then ClearPath. For status 0/1/4 request status 4; for 2/3 request 5; above 4 no such change. Clear+0x46, delete stored planner objects in reverse, return ClearPath result. | `00649100..006491BC`; strings `006491EC,00649200`. Planner/status transition internals and planner object destructors are **M11 boundary**. |
| A4 | ClearPath copies nonzero u16+0x42 to+0x4A, calls VizManager.ErasePath and optional PathDolerOuter.ClearPath, sets byte+0x40=FF and timestamp+0x3C to BaseStationTimer seconds. Send ClearPath with zero u16 payload, reliable=1/hot=0; return send result. | `00649220..006492A0`; send `64927C→0051349C`; visualization/path-doler recipients reopened, **M11 boundary**. |
| A5 | Construct/send empty AbortDocking, reliable=1/hot=0, clear temporary message and return send result. No docking target reset is present here. | `0063BE10..0063BE5E`, send `63BE36`. |
| A6 | Construct/send empty AbortAnimation, reliable=1/hot=0, clear temporary message and return send result. This does not establish a separate animation-state reset. | `00517DE4..00517E32`, send `517E0A`; animation engine **M5 boundary**. |
| A7 | If any direct-drive flag+0xB8/+0xB9/+0xBA is set and disable byte+0xD4 is zero, perform **five** zero-speed helper calls: HEAD mask1/B9, LIFT mask2/BA, BODY mask4/B8, BODY mask4/B8, BODY mask4/B8. Name/debug sources respectively C0/C0, C4/C4, BC/BC, BC/C8, BC/CC. All five execute once this outer gate passes. Always perform A8 afterward. | `0063FBD8..0063FE1A`; calls `63FC4C,63FCB6,63FD1E,63FD86,63FDEE`. |
| A8 | Send empty StopAllMotors, reliable=1/hot=0; ignore result. | `0064099C..006409EA`, send `6409C2`. |
| A9 | Zero-speed path: `abs(speed) < f32(3727C5AC)` clears its direct-drive flag. If AreAllTracksLocked(mask), call UnlockTracks. If that returns true, emit ERROR `MovementComponent.DirectDriveCheckSpeedAndLockTracks`, `Locks left on tracks %s [0x%x] after %s[%s] unlocked`; set `_errG`=1, then gated debug-break helper (shipped helper is a no-op). Nonzero-speed locking branch exists in reopened target but is unreachable for these zero calls. | `0063EFB0..0063F0BC`; ERROR `63F048`, error store `63F08C`, optional `63F090→0080DAB4`. |
| A10 | For each selected track bit, find/remove one lock matching the supplied key. Missing key emits INFO `MovementComponent.UnlockTracks`, `Tracks 0x%x are not currently locked by %s`, then PrintLockState. Tracks becoming empty contribute to one EnableAnimTracks mask, sent reliable=1/hot=0 before A8. Return reflects remaining locks on selected tracks (used by A9). | `0063FE5C..0063FFDC`; diagnostic `63FF76`; EngineToRobot EnableAnimTracks tag 9E, u8 mask. D1–D3 own diagnostic. |

Wire boundary: all four outer sends and any EnableAnimTracks use Robot.SendMessage `0051349C`; serializers/message cleanup were reopened. M1/M2 own transport/admission/serialization; this task does not recover their descendants anew. Failed sends do not skip later outer calls. No file open/close or independent global counter appears in A1–A10; A9's error flag and all logging calls are material effects.

## M4-027 — component teardown

The Robot destructor already calls AbortAll before these deletion windows (`00511120`). Parent destructor event, freeplay and the rest of Robot lifetime remain M1/M7 ownership. Retained host ResetDevices is a candidate, not primary evidence.

| Row | Exact recovered behavior | Source |
|---|---|---|
| T1 | Tap+0x450, when nonnull: clear ObjectTapped list+0x2C, destroy DoubleTapInfo tree+0x20, release shared filter control block+0x14, destroy subscription vector+4, delete component, then null Robot+0x450. Last-owner shared filter destruction remains conditional. | `005111EA..00511224`; `005196B4,005196E4,004EAE94` reopened. |
| T2/T5/T6 | Null Robot Touch+0x28C and Cliff+0x288 **before** destructor/free. Touch destructor nulls owned RollingFileLogger+0x0C before virtual deleting destructor. Cliff does the same at+0x44, then destroys u16 deque+0x20. | Robot `005113D4..005113FA`; Touch `0064E746..0064E75C`; Cliff `00633FF8..00634016`. Logger ownership established by constructor/assignment, not just slot guess. |
| T3/T7/T17 | Null CubeAccel+0x278 before virtual delete. Deleting destructor clears subscription list+0x14, destroys ObjectID→AccelHistory tree+8, then frees component. Each history destroys listener set+0x34 and ActiveAccel history tree+0x20; shared listeners release conditionally on last owner. | Robot `00511428..0051143A`; D0 `00635444..0063546E`; tree `00635D84..00635DBC`, descendants `00635DFC,00635E34`. CubeAccel vtable address point `0102EA48`, delete slot+4. Recipient listener lifetimes stop at their owning layer. |
| T3/T15 | Null BackpackLights+0x274 first. Release weak handles+0x30 then+0x20, destroy layer tree+0x10, clear subscription list+4, free component. Tree destruction releases BackpackLightData shared lists. Weak release is not an explicit unsubscribe or a light-off send. | `0051143E..0051146A`; tree `005195B6..005195E2`, list `00517F50`. |
| T3/T16/T24/T25 | Null CubeLights+0x270 first; clear subscription list+0x14, destroy ObjectID tree+8, free component. Tree destroys three LightPattern lists and three CurrentAnimInfo lists in reverse index order. CurrentAnimInfo function objects are **destroyed**, not invoked. | `0051146E..0051148C`; `005194E2..005195B6`; function destroy `0051959A`, virtual+0x10 inline/+0x14 heap. |
| T4/T8/T9/T18 | Null Movement+0x254 first; virtual deleting destructor destroys FaceLayerToRemove tree+0x88, eight LockInfo trees in descending track order (+0x7C down to+0x28), subscription list+0x10, then component allocation. No StopAllMotors/UnlockTracks/EnableAnimTracks call is inside this destructor. Those effects belong to earlier AbortAll. | `00511500..00511512`; D1 `00641CFC..00641D38`, D0 `00641D3C..00641D4A`; tree `00641D6E..00641DAA`; Movement vtable point `0102EFBC`. |
| T12–T14/T19/T21/T22 | Handle vector releases in reverse element order; handle list clears forward after detaching its list. Shared-count release calls the container's on-zero hook only on last owner. Scoped container calls handle virtual+8 unsubscribe, then virtual+4 deleting destructor, then frees itself. Robot signal handle uses weak-lock/search/unlink: destroy saved function (inline+0x10/heap+0x14), clear function target, reconnect list links, release node references/free on zero. **No function invocation on unsubscribe.** | `004EAE94..004EAEC8`, `005194A8..005194E2`, `004EF184..004EF198`, `004EF1B8..004EF1D2`; robot handle `0051D8F8..0051D936`, unlink `0051D338..0051D3A6`; vtable point `0101FE20`, unsubscribe slot+8. Other signal families use their own concrete handles; arbitrary captured-object destructors are an owner boundary. |
| T10/T11/T20/T23 | RollingFileLogger D0 calls D1 then delete. D1 stops/releases queue+8 if present, closes filebuf+0x3C, applies ios state OR4 (failbit) when close returns null, destroys filebuf/ios/strings, and runs queue cleanup again. Touch/Cliff logger constructors supply null queue; this queue cleanup is a no-op there. | D1 `0080E090..0080E120`, D0 `0080E15C..0080E16A`; queue helper `00801E70..00801E88`; file close `0050111C..00501158`. |
| T23/T26/T27 | With FILE*+0x40 nonnull, close calls virtual sync+0x18 then fclose. fclose failure returns null retaining FILE*, so filebuf destruction can attempt close again; do not force exactly one close. On successful fclose clear FILE*; sync failure still returns null. Sync can flush buffered output through overflow/fwrite and fflush; read-mode path can seek back through fseeko. Codecvt virtual calls and bad_cast exception path remain explicit runtime interfaces. No new file open occurs in teardown. | filebuf vtable `0101F2BC`, address point `0101F2C4`, sync slot `0101F2DC` relocation→`00501398`; sync `00501398..005014C8`, overflow `00501684..005017B0`; filebuf D2 `005010B4`. |

There is no “reset all device values” store or synthetic completion event in these windows. Container node/string frees, callback object destruction, weak/shared reference changes and file flush/close are side effects, not ignorable cleanup. Final-owner destruction of a captured action/listener is not proof that its higher-layer body was extracted here.

## M4-028 — sleep lift child

| Row | Exact recovered behavior | Source / boundary |
|---|---|---|
| L1 | Sleep sequence constructs an A4-byte MoveLiftToHeightAction with preset 0 and tolerance f32 bits 40A00000 (5), adds it to the parallel parent with flags/extra argument zero, then releases returned weak handle. Animation siblings and parent queue/scheduler remain M5/M8. | `0052CF8A..0052CFC2`; ctor call `52CFA2→00548B84`; virtual AddAction `52CFB0`; parallel vtable `01022074`, slot+0x20 at `0102209C` relocation→`0054EC7C` (reopened). |
| L2–L4 | Preset0 height is f32 bits42000000 (32mm), name LowDock, action name MoveLiftToLowDock. Static map uses guarded one-time initialization/atexit destruction. Literal height pairs: (0,42000000),(1,42980000),(2,42B80000),(3,BF800000), at 00C54684. Other presets/fallback are not sleep inputs. | `00548B84..00548BD6`, `00548C14..00548C8A`, `00548CD4..00548E48`; prefix MoveLiftTo at 00BEA3A6. |
| L5 | Base type13hex, track mask2; fields+78 height32,+7C tolerance5,+80 variability0,+88 duration0,+8C speed10,+90 accel20. Flags+95..+98 zero; subscription pair+9C/+A0 initialized then installed for RobotToEngine MotorActionAck tag C4, robot ID from Robot+10. Base constructor/action tag/track lifecycle is M8 boundary. | `0054899C..00548B16`; subscribe `548AAC→00519F8C`. |
| L6/L8 | Init clears sent/ack (+95/+96) and saw-moving+98, sets target+84=32 (no random with variability0), computes height→angle and angle tolerance using clamped height32..92 and `asinf((height−45)/66)`. IsLiftInPosition requires **strict** abs(height error)<tolerance and Movement+0x0B not moving. Store latch+97. If already in position return0 without motor send; otherwise send L9, nonzero send result→03000016, success sets sent+95=1 and returns0. | Init `0054903C..0054933A`; predicate `00548FEA..0054903A`; conversion `005170B0..005170F4`. Platform `asinf` imported, not in packaged ELFs: phone libm rounding assumption must be recorded, not silently promoted to recovered arithmetic. |
| L9/L12 | Increment Movement byte+8 (u8 wrap) and write action ID through output pointer to action+94, even on failed send. SetLiftHeight payload: height32,speed10,accel20,duration0,ID; reliable=1/hot=0. | `00640700..00640742`; sender `00640744..006407AE`, send `640786→0051349C`. |
| L13 | Ack callback only acts when sent+95 is nonzero and message ID equals action+94. Emit Actions INFO `MoveLiftToHeightAction.MotorActionAcked`, `[%d] ActionID: %d`, then set ack+96=1. M2 owns event decoding/delivery. | Concrete callback `0054D748..0054D7DC`; callback vtable base `01021D44`, point `01021D4C`, invoke+18 at `01021D64` contains Thumb `0054D749`; info `54D784`, flag store `54D7AC`. |
| L7 | CheckIfDone uses in-position latch and ack gate, refreshes position when needed, records saw-moving, returns Running01000000 while moving/waiting; success0 when complete. Not in position and now stopped after saw-moving→warning and04000004. Waiting-for-ack global u16 counter105102C and not-in-position counter105102E increment/wrap, log on reaching11 then reset0; these are not per-action timers/counters. | `005493F0..0054956E`; full branches in appendix. |
| L11/L14/L15 | Lift destructor releases subscription before tailing IActionRunner D2. M8 owns when deletion runs, ownership check and unlocking. Reopened base destructor calls StopLift only on its track ownership path. StopLift's direct-drive gate matches A7, but performs one zero helper for lift mask2, then always sends MoveLift(0), reliable=1/hot=0. Do not replace this with StopAllMotors. | `0054D138..0054D15A→00541084` (M8 interface); StopLift `00640B3C..00640C00`; MoveLift sender `00640C00..00640C68`, send `640C2A`. |

All lift logs (including generic branches in the reopened Init target):

| Level/channel | Event; format | Reachability / call |
|---|---|---|
| WARNING/Actions | `MoveLiftToHeightAction.Init.InvalidHeight`; `%f mm. Clipping to be in range.` | Generic invalid-height path `549090`; preset32 is in range. |
| WARNING/Actions | `MoveLiftToHeightAction.Init.TolTooSmall`; `HeightTol %f mm == AngleTol %f rad near height of %f mm. Clipping tol to %f mm` | `5492C8`; requires computed angle tolerance below f32 bits3CD67750. With height32/tolerance5, upper37 bound yields larger angle; this branch is not selected by the sleep child (subject to named libm assumption). |
| DEBUG/Actions | `MoveLiftToHeightAction.CheckIfDone.WaitingForAck`; `[%d] ActionID: %d` | L7 ack counter threshold11; format00BE9DE8. |
| DEBUG/Actions | `MoveLiftToHeightAction.CheckIfDone.NotInPosition`; `[%d] Waiting for lift to get in position: %.1fmm vs. %.1fmm (tol: %f)` | L7 position counter threshold11; format00BEA400; args tag/current/target/tolerance. |
| WARNING/Actions | `MoveLiftToHeightAction.CheckIfDone.StoppedMakingProgress`; `[%d] giving up since we stopped moving` | L7 saw-moving/stopped gate; event00BEA446, format00BEA37F. |
| INFO/Actions | `MoveLiftToHeightAction.MotorActionAcked`; `[%d] ActionID: %d` | L13, after matched sent command, before ack flag. |

No file effects or `_errG` store in lift-specific rows. StopLift's shared A9 helper retains A9's possible error log/store. Static maps/guards/atexit, subscription release, native message destruction, exception unwinding and command ID advancement are also accounted for. Parent compound action start/lock/timeout/completion callbacks are M8 boundaries, not claimed settled by these child rows.

## M4-031 — PrintLockState

| Row | Exact recovered behavior | Source |
|---|---|---|
| D1 | Empty ostringstream; visit track indices0..7 ascending. Skip empty trees. Per nonempty tree append track name, `:`, unsigned-decimal tree count, space; traverse native ordered tree from begin using in-order successor. Each node prints **string+1C**, `[`, **string+10**, `] `; append newline after track. Emit one debug record even if the complete string is empty. No wire/file open/close. | `006410D8..006413CA`; treebase+28+12*i/count+30+12*i; unsigned insertion `0051778C`; log `64134A→0080D818`. Native locale/string/stream allocation/destruction targets reopened. |
| D2/D3 | Name is AnimTrackFlagsToString(u8(1<<index))→enum string for single bit. Names bits0..6: HEAD_TRACK,LIFT_TRACK,BODY_TRACK,FACE_IMAGE_TRACK,EVENT_TRACK,BACKPACK_LIGHTS_TRACK,AUDIO_TRACK. Bit7 returns null from enum helper and the caller then uses strlen; no eighth guessed track label/fallback is supported. | `006305E8..00630788`; enum `007BC250` (full target opened); strings00C20053,00C2005E,00C20069,00C20074,00C20085,00C20091,00C200A7; null `007BC2B0`. |

DEBUG channel Unnamed (00BE3FEC), event `MovementComponent.LockState` (006414C8), format `%s` (006414E4). A10's missing-key INFO precedes each diagnostic call. Traversal uses native comparator order/duplicate entries, not a sorted host dictionary approximation. Bit7-populated reachability is UNKNOWN at this boundary; do not silently sanitize it. Locale formatting belongs to the reopened C++ runtime interface; this is not proof of a whole host formatter's fidelity.

## M3-038 — NV destruction

| Row | Exact recovered behavior | Source |
|---|---|---|
| N1/N3 | Robot nulls+260 before virtual deleting destructor. Constructor stores NV vtable point0102F388; delete slot0102F38C resolves643F94, calls D1 then delete. | Robot `005112D6..005112E6`; D0 `00643F94..00643FA2`; constructor identity `0050FD8C..0050FD90`. |
| N2 | D1 order: reverse-release subscription vector+128, destroy hashset+144, free vector+138, destroy/free now-empty subscription vector+128, destroy idle-function deque+110, destroy request deque+F8, free saved command bytes+E8, destroy backup manager+80, destroy active write request+50, destroy active read function+28, free read buffer+18 and null it. | `00643EC4..00643F8E`; backup call643F48→0051A5F0 **M15 boundary**, reopened only for interface. |
| N4 | Active write request: free owned data/vector only if request+20 says owned. Destroy completion function through inline+10/heap+14 slot; never invoke it. Borrowed data must not be freed. | `00643E80..00643EC4`. |
| N5/N7 | Idle deque destroys each 24-byte function object (target+10; inline10/heap14), clears count and frees deque blocks/backing storage. **No virtual+18 invocation**. | clear `00646B8A..00646C42`; deque dtor `00646B64..00646B8A`. |
| N6/N8 | Request deque destroys each72-byte entry: write function+20 first, read function+8 second. It does not free queue request data as though it were active owned data. Clear count, free storage. | clear `00646C92..00646D6C`; deque dtor `00646C6C..00646C92`. |

No cancellation/timeout callback invocation, idle transition, readiness publication or NV wire send in these destructor windows. std::function destruction may release captured shared objects; their final destructors are the producer's owning-layer boundary. M3-035's pending-read discard claim is narrower than the full owner destruction recovered here. No manifest change is inferred from it.

## M3-040 — idle callback scheduling

| Row | Exact recovered behavior | Source |
|---|---|---|
| I1/I8 | RobotConnectionResponse handler copies its void ready lambda, registers with Robot+260 NV component, then destroys temporary function. Ready store/log body remains M1-041. | `00528A5A..00528A80`; copy `00528C0C..00528C36`; call528A6E→00645C20; M1 body0052C3A6..0052C3B6. |
| I2/I9 | AddOneShot copies callback to deque+110 then tails ProcessOnIdleCallbacks **synchronously**. Grow deque if needed; increment count only after function copy. No deferred thread/task/timer. | `00645C20..00645C36`; append `00648034..006480AA`; veneer008CCE6C resolves00645B08. |
| I5 | Entry requires request deque count+10C==0 and state+8==0; if either nonzero return. Empty idle deque count+124 also returns. Check NV queue/state **once on entry**, then drain until idle deque empty. | `00645B08..00645BAE`. |
| I5/I6/I7 | For each front emit NVStorage DEBUG `NVStorageComponent.ProcessOnIdleCallbacks.ProcessingCallback`, empty format; invoke front function, **then pop/destroy it**. No queue/state recheck between callbacks. Newly appended callbacks remain eligible in this drain. Registration during an executing callback can reenter before front is popped; no added guard is supported. | debug645B56, invoke645B9A→005BF7BC, pop645BA0→006486C2; function invoke virtual+18, pop destroy inline10/heap14. |
| I3/I4 | Update's state0 path first calls ProcessRequest, then tails ProcessOnIdleCallbacks, whose predicate observes post-dispatch state/count. Empty request queue returns without request dispatch. SetState is not a separate synchronous idle-callback trigger. | `006456BC..006456F4`; call6456CC→00644FD4, empty-request return00645488; conditional tail veneer008CCE6C. Other Update states/timeouts belong existing NV queue records. |

Empty std::function throws shipped bad_function_call; callback exceptions propagate before pop. No catch/rollback, synthetic result, global timing counter, file effect or error flag store is present here. M1-034 handler isolation is a separate approved host policy, not evidence for rewriting NV's loop. Captured callback bodies (ready, backup/action recipients) remain their existing owner records.

## SDK dispatch reachability — no recipient extraction

AGENTS.md app boundary says SDK mode is unsupported. That makes the following **SdkStatus-produced dispatches** unreachable in the stack. It does not make every shared external-interface recipient SDK-exclusive.

| Row | Source reachability fact | Evidence |
|---|---|---|
| S1 | Native named-Thumb call-site search finds ResetRobot called from SdkStatus.EnterMode and OnDisconnect. EnterMode sets external+79 or internal+7A/+78 state before its reset. OnDisconnect gates on connected+78 and stop-on-disconnect+7C before reset. | Reset0065DCF8; EnterMode0065E104/call0065E1F8; OnDisconnect0065E4D8/call0065E5D0. Caller disassembly and search results appended. |
| S2 | EnableCubeSleep(1,1) dispatch is in ResetRobot's +79 branch; no recipient body needed for the unsupported SDK reset path. | Gate0065DE88; constructor0065DEA2→0074F576, external-interface virtual+0C dispatch0065DEAA. **M3-039**. |
| S3 | EnableLightStates(false,-1) dispatch shares that +79 branch. | constructor0065DECA→0074F4DA, dispatch0065DED2. **M4-029 light slice**. |
| S4 | StopRobotForSdk and EnableLiftPower(true) occur **outside** +79 gate, but inside ResetRobot. Their reset dispatches are still SDK paths. | constructor0065DF6A→00751702/dispatch0065DF72; constructor0065DF8C→0074B794/dispatch0065DF94. **M4-030 / M4-029 lift slice**. |

Manager correction needed: current M4-029 evidence's `+0x79 gate` cannot apply to lift-power. Also, current recipient-wide titles/live_path are broader than what the SDK producer proof establishes. Shared EnableCubeSleep/EnableLightStates recipients have live external-interface callers already covered in the checked M3/M4 rows (C10.3, C12/C13); their bodies are deliberately not extracted here. The StopRobotForSdk receiver entry/dispatch is not evidence of an intrinsic SDK check. This report does **not** assert arbitrary host-supplied external messages can never reach that receiver. It confirms SDK mode confinement of the **identified reset production paths**, and leaves broader recipient admission UNKNOWN for manager scoping. The named Thumb caller search is corroboration, not proof against every possible function-pointer/manual message producer. Unity/app source is not present in this clone; no app exclusivity proof is invented.

## Rule 10 call/effect ledger and remaining boundaries

Every call instruction in the claimed windows is listed with its reopened target in Appendix B. The following accounts for the call classes without discarding non-wire side effects:

| Calls / windows | Effect retained or explicit boundary |
|---|---|
| Channeled info/debug, sErrorF/sWarningF, debug-break | Exact strings, levels, gates, order and A9 error store listed above. Logs in reopened generic lift branches recorded even when dead for preset0. |
| Robot.SendMessage, EngineToRobot constructors/ClearCurrent | Ordered sends/payloads/flags and result handling above; native temporary destruction/EH retained. M1/M2 owns lower-layer wire lifecycle. |
| ActionList.Cancel, IActionRunner constructor/destructor, ICompoundAction.AddAction | Reopened; M8 owns recursive cancellation/deletion, tags/locks/timeout/parent completion. Child-owned M4 sender/predicates separately extracted. |
| Planner virtual abort/destruction, PathDoler, VizManager | Reopened interfaces; M11 owns their bodies and callbacks. No guessed planner reset. |
| Backup manager destructor | Reopened target0051A5F0; M15 owns contents. No callback invocation claimed from its name alone. |
| Shared/weak release, weak lock, ScopedHandle unsubscribe/delete | Packaged count implementation reopened; last-owner predicates matter. Concrete robot signal target/slot bound above. Unidentified captured recipient destruction remains UNKNOWN at owner boundary. |
| Function copy/invoke/destroy, deque append/pop/clear | Distinguish invocation+18 from destroy10/14 and copy; exact NV ordering/reentrancy above. Alloc/grow/dealloc and count changes retained. |
| Tree/list/vector/hash destruction, string allocation/copy/append, stream/locale | Reopened targets; preserve traversal, final-owner effects and allocation/destruction. Local entries lacking a symbol are explicitly marked entry-only in appendix; no behavior beyond their inspected window is promoted to a build fact. Generic allocation/string implementation is runtime plumbing, not a replacement behavior assumption. |
| Guard acquire/release, atexit, exceptions/unwind/stack guard | Packaged targets reopened where present. Preset-map one-time/global lifetime and exceptional cleanup remain explicit. No invented catch or success conversion. |
| Rolling logger queue stop/release, filebuf sync/overflow/close, fwrite/fflush/fseeko/fclose | Conditional null-queue and FILE gates retained; flush before close, failure retention and later close retry recorded. Codecvt virtual slots are a locale/runtime boundary; file system calls have no packaged definitions. |
| asinf, strlen, memcpy/memmove/memset, file/OS libc calls without packaged definitions | Named platform interface; no unavailable shipped body claimed recovered. `asinf` rounding is specifically material to lift arithmetic. |

This is **not** a whole-layer approval packet: manager must check rows and route M8/M11/M15/producer-lifetime boundaries to those layers. Arbitrary captured callback/destructor targets, invalid track7 reachability, full external-message SDK admission and platform arithmetic/locale assumptions are not guessed. No behavior is reclassified by tests or by this extraction. No tests were added/run for this research-only report.

## Appendix A — current manifest records (verbatim JSON)

These snapshots were read from the current manifest, not recalled from older notes. Their titles/status/evidence/unresolved provide the baseline for the manager corrections above.

```json
{
  "id": "M3-038",
  "subsystem": "M3-device",
  "title": "NV component destruction and pending callback lifetime",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs",
  "status": "RECOVERABLE_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "0x0050FD8C..0x0050FD90; 0x005112D6..0x005112E4; Virtual NV deletion, pending requests and callbacks. Constructor/store identity is checked; descendant destructor effects UNKNOWN.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u00e2\u20ac\u201cT9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt"
  ],
  "effect": "A removed Robot or queued sleep action retains state, emits different messages or invokes callbacks in a different order.",
  "provenance": "Cited cross-layer inventory correction authorized by B-M1M2 Rows checked (manager, 2026-10-05). Interfaces/known effects only; no higher-layer build or settlement.",
  "unresolved": "S1: NV owner/delete boundary 0x005112D6..0x005112E4; recipient identity 0x0050FD8C..0x0050FD90. M3-035 remains specifically pending-read callback discard. Virtual delete descendants UNKNOWN. Source triage S1. Source: re-analysis/research/20261006-M1M2-missing-triage.md.",
  "hardware_required": false,
  "live_path": true,
  "test": ""
}
{
  "id": "M4-026",
  "subsystem": "M4-control",
  "title": "Robot AbortAll ordered control and wire effects",
  "location": "cozmo-stack/src/Cozmo.Robot/Motion.cs",
  "status": "RECOVERABLE_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "0x0051194C..0x0051198C; 0x00649268..0x0064927C; 0x0063BE36; 0x00517E0A; 0x006409C2; Cancel type -1, Path Abort, Dock Abort, AbortAnimation, StopAllMotors in order; OR results without short circuit. Nested stop/direct-drive predicates and recursive callbacks remain required.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u00e2\u20ac\u201cT9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt"
  ],
  "effect": "A removed Robot or queued sleep action retains state, emits different messages or invokes callbacks in a different order.",
  "provenance": "Cited cross-layer inventory correction authorized by B-M1M2 Rows checked (manager, 2026-10-05). Interfaces/known effects only; no higher-layer build or settlement.",
  "unresolved": "MISSING: Cancel type -1, Path Abort, Dock Abort, AbortAnimation, StopAllMotors in order; OR results without short circuit. Nested stop/direct-drive predicates and recursive callbacks remain required. Bind the native owner to its actual higher-layer production component and recover/check the complete recipient path before implementation or settlement.",
  "hardware_required": false,
  "live_path": true,
  "test": ""
}
{
  "id": "M4-027",
  "subsystem": "M4-control",
  "title": "Sensor/light/movement/CubeAccel teardown and retained host reset candidate",
  "location": "cozmo-stack/src/Cozmo.Robot/Motion.cs",
  "status": "RECOVERABLE_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "0x005111EA..0x0051121A; 0x005113E0..0x005113F6; 0x00511428..0x00511488; 0x00511500..0x00511510; Tap filter, Touch/Cliff, CubeAccel, backpack/cube lights and Movement ownership effects. Existing ResetDevices is a candidate, not evidence of these native effects; queues/subscriptions/virtual descendants UNKNOWN.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u00e2\u20ac\u201cT9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "S2 ResetDevices transfer: Robot::AbortAll 0x0051194C..0x0051198C and owned component boundaries 0x005111EA..0x0051121A, 0x005113E0..0x005113F6, 0x00511428..0x00511488, 0x00511500..0x00511510 (20261006-M1M2-missing-triage.md S2)."
  ],
  "effect": "A removed Robot or queued sleep action retains state, emits different messages or invokes callbacks in a different order.",
  "provenance": "Cited cross-layer inventory correction authorized by B-M1M2 Rows checked (manager, 2026-10-05). Interfaces/known effects only; no higher-layer build or settlement.",
  "unresolved": "S2: Tap filter, Touch/Cliff, CubeAccel, backpack/cube lights and Movement ownership. ResetDevices candidate maps to native AbortAll 0x0051194C..0x0051198C and component boundaries 0x005111EA..0x0051121A, 0x005113E0..0x005113F6, 0x00511428..0x00511488, 0x00511500..0x00511510; it is not proof of equivalent ownership. Queues/subscriptions/virtual descendants UNKNOWN. Source: 20261006-M1M2-missing-triage.md S2.",
  "hardware_required": false,
  "live_path": true,
  "test": ""
}
{
  "id": "M4-028",
  "subsystem": "M4-control",
  "title": "Go-to-sleep lift child construction and motor effects",
  "location": "cozmo-stack/src/Cozmo.Robot/Motion.cs",
  "status": "RECOVERABLE_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "0x0052CF8E..0x0052CFB0; MoveLiftToHeightAction preset0, tolerance f32 0x40A00000; exact child initialization, locking, stop and completion must be verified in the control layer.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u00e2\u20ac\u201cT9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt"
  ],
  "effect": "A removed Robot or queued sleep action retains state, emits different messages or invokes callbacks in a different order.",
  "provenance": "Cited cross-layer inventory correction authorized by B-M1M2 Rows checked (manager, 2026-10-05). Interfaces/known effects only; no higher-layer build or settlement.",
  "unresolved": "S14: lift child MoveLiftToHeightAction preset0, tolerance bits 0x40A00000; construction/locking/stop/completion remain this M4 gap. Caller 0x0052CF8E..0x0052CFB0. Source: re-analysis/research/20261006-M1M2-missing-triage.md.",
  "hardware_required": false,
  "live_path": true,
  "test": ""
}
{
  "id": "M3-039",
  "subsystem": "M3-device",
  "title": "SDK cube-sleep recipient",
  "location": "cozmo-stack/src/Cozmo.Robot/RobotLifetime.cs",
  "status": "RECOVERABLE_GAP",
  "effect": "SDK ResetRobot can omit/reorder cube-sleep request or invoke a wrong recipient.",
  "provenance": "Engine dispatch known; concrete receiver UNKNOWN.",
  "authority": "libcozmoEngine.so 3.4.0-1204 ResetRobot.",
  "evidence": [
    "Dispatch EnableCubeSleep(1,1) at 0x0065DEA2; +0x79 SDK send gate (triage E3)."
  ],
  "unresolved": "Recover concrete cube-sleep receiver and preserve +0x79/order; receiver internals UNKNOWN.",
  "hardware_required": false,
  "live_path": true,
  "test": "M3DeviceTests SDK reset cube-sleep cases after extraction."
}
{
  "id": "M3-040",
  "subsystem": "M3-device",
  "title": "NV idle callback scheduling and invocation predicate",
  "location": "cozmo-stack/src/Cozmo.Robot/NvStorage.cs",
  "status": "RECOVERABLE_GAP",
  "effect": "Ready byte can be written before NV work is idle or missed when queue drains.",
  "provenance": "Current callback candidate not fully compared.",
  "authority": "libcozmoEngine.so 3.4.0-1204 NV queue and Robot lambda.",
  "evidence": [
    "Registration 0x00528A5A..0x00528A6E; idle invocation boundary 0x00645C20..0x00645C32 (triage N1). M1-041 retains the ready-byte store/log body at 0x0052C3A6..0x0052C3B6."
  ],
  "unresolved": "Recover NV queue-idle predicate/order and callback invocation scheduling. The callback body that stores/logs ready is owned by M1-041; M3-035 separately owns disconnect/destruction callback discard.",
  "hardware_required": false,
  "live_path": true,
  "test": "M3DeviceTests.M3_035_ADisconnectDiscardsTheReadWithNoCallbackOrTimeout plus idle cases after extraction."
}
{
  "id": "M4-029",
  "subsystem": "M4-control",
  "title": "SDK light-state and lift-power recipients",
  "location": "cozmo-stack/src/Cozmo.Robot/Motion.cs",
  "status": "RECOVERABLE_GAP",
  "effect": "SDK reset may send wrong light/lift command or use the wrong gate/order.",
  "provenance": "Dispatcher known; receivers UNKNOWN.",
  "authority": "libcozmoEngine.so 3.4.0-1204 ResetRobot.",
  "evidence": [
    "EnableLightStates(false,-1) at 0x0065DECA; EnableLiftPower(true) at 0x0065DF8C; +0x79 gate (triage E3)."
  ],
  "unresolved": "Recover concrete receivers and exact message values/order/gates.",
  "hardware_required": false,
  "live_path": true,
  "test": "M4ControlTests SDK reset light/lift cases after extraction."
}
{
  "id": "M4-030",
  "subsystem": "M4-control",
  "title": "StopRobotForSdk action/control recipient",
  "location": "cozmo-stack/src/Cozmo.Robot/Motion.cs",
  "status": "RECOVERABLE_GAP",
  "effect": "SDK reset can stop the robot through a different action/control path.",
  "provenance": "Dispatcher known; receiver/action effects UNKNOWN.",
  "authority": "libcozmoEngine.so 3.4.0-1204 ResetRobot.",
  "evidence": [
    "StopRobotForSdk dispatch at 0x0065DF6A; receiver effects UNKNOWN (triage E5)."
  ],
  "unresolved": "Identify/recover receiver, action construction, gates and downstream effects before implementation.",
  "hardware_required": false,
  "live_path": true,
  "test": "M4ControlTests StopRobotForSdk source-derived case after extraction."
}
{
  "id": "M4-031",
  "subsystem": "M4-control",
  "title": "Track-lock diagnostic enumeration",
  "location": "cozmo-stack/src/Cozmo.Robot/Motion.cs",
  "status": "RECOVERABLE_GAP",
  "effect": "Lock diagnostics can enumerate tracks or ownership names in a different order.",
  "provenance": "Entry/caller known; traversal and names not verified.",
  "authority": "libcozmoEngine.so 3.4.0-1204 PrintLockState.",
  "evidence": [
    "PrintLockState 0x006410D8..0x006413CA; lock/unlock caller 0x0063FF02..0x0063FF24 (triage E9)."
  ],
  "unresolved": "Recover tree traversal, ownership names and diagnostic order; do not substitute M4-003/M4-016 action semantics.",
  "hardware_required": false,
  "live_path": true,
  "test": "M4ControlTests PrintLockState diagnostic case after extraction."
}
```

## Appendix B — call census, row instructions and reopened targets

383 call/return-dispatch/out-of-window branch entries; 113 distinct engine target openings; 26 imported target names. This includes conditional BLX, indirect calls and conditional wide branches. A branch to another window of the same function is a continuation, not a new call (notably ProcessRequest). Full named target windows may contain literal tables or EH blocks: those are shown for review, not asserted to execute as normal code. Entry-only unnamed windows are explicitly labeled and do not settle any uninspected behavior. SDK recipient bodies are excluded from the row ranges and body inventory.

<details>
<summary>Call census and target instructions</summary>

```text
Every call instruction in the half-open row ranges; tail dispatch included. Dynamic calls are not silently dropped.
L13 motor ack callback | 0054D75C | blx 004AB44C | _ZNK4Anki5Cozmo14RobotInterface13RobotToEngine18Get_motorActionAckEv
L13 motor ack callback | 0054D784 | blx 004A505C | _ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
L13 motor ack callback | 0054D7A6 | blx 004A40CC | _ZdlPv
L13 motor ack callback | 0054D7D2 | blx 004A40CC | _ZdlPv
L13 motor ack callback | 0054D7D8 | blx 004A40A8 | _Unwind_Resume
L14 StopLift receiver | 00640B6A | blx 004A44E0 | strlen
L14 StopLift receiver | 00640B78 | bl 004E02B2 | local 004E02B2
L14 StopLift receiver | 00640B88 | blx 004A44E0 | strlen
L14 StopLift receiver | 00640B94 | bl 004E02B2 | local 004E02B2
L14 StopLift receiver | 00640BA6 | blx 004B9654 | _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_
L14 StopLift receiver | 00640BB4 | blxne 004A40CC | _ZdlPv
L14 StopLift receiver | 00640BC2 | blxne 004A40CC | _ZdlPv
L14 StopLift receiver | 00640BCE | bl 00640C00 | local 00640C00
L14 StopLift receiver | 00640BE4 | blxne 004A40CC | _ZdlPv
L14 StopLift receiver | 00640BF6 | blxne 004A40CC | _ZdlPv
L14 StopLift receiver | 00640BFC | blx 004A40A8 | _Unwind_Resume
L15 MoveLift sender | 00640C1E | blx 004B9690 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_8MoveLiftE
L15 MoveLift sender | 00640C2A | blx 004A5368 | _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb
L15 MoveLift sender | 00640C32 | blx 004A5200 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv
L15 MoveLift sender | 00640C4E | blx 004A4FE4 | __stack_chk_fail
L15 MoveLift sender | 00640C52 | bl 004E39F8 | local 004E39F8
L15 MoveLift sender | 00640C5A | blx 004A5200 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv
L15 MoveLift sender | 00640C60 | blx 004A40A8 | _Unwind_Resume
L15 MoveLift sender | 00640C64 | bl 004E39F8 | local 004E39F8
T26 file sync | 005013E6 | blx r2 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T26 file sync | 00501406 | blx r7 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T26 file sync | 00501416 | blx 004A6A18 | fwrite
T26 file sync | 00501428 | blx 004A6A24 | fflush
T26 file sync | 00501434 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T26 file sync | 00501454 | blx 004A69F4 | fseeko
T26 file sync | 0050149E | blx r6 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T26 file sync | 005014B0 | blx 004A42B8 | __cxa_allocate_exception
T26 file sync | 005014B4 | blx 004A6A0C | _ZNSt8bad_castC1Ev
T26 file sync | 005014C4 | blx 004A42D0 | __cxa_throw
T27 file overflow | 00501696 | blx 004A6A48 | _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE12__write_modeEv
T27 file overflow | 005016D8 | blx 004A6A18 | fwrite
T27 file overflow | 0050172E | blx 004A6A18 | fwrite
T27 file overflow | 00501760 | blx r7 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T27 file overflow | 00501782 | blx 004A6A18 | fwrite
T27 file overflow | 00501790 | blx 004A42B8 | __cxa_allocate_exception
T27 file overflow | 00501794 | blx 004A6A0C | _ZNSt8bad_castC1Ev
T27 file overflow | 005017A4 | blx 004A42D0 | __cxa_throw
T21 unsubscribe robot signal | 0051D900 | blx 004A556C | _ZNSt6__ndk119__shared_weak_count4lockEv
T21 unsubscribe robot signal | 0051D908 | blx 004A4EF4 | _ZNSt6__ndk119__shared_weak_count16__release_sharedEv
T22 signal callback unlink | 0051D352 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T22 signal callback unlink | 0051D39C | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T23 logger close | 0050112A | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T23 logger close | 00501130 | blx 004A69DC | fclose
T23 logger close | 0050114E | blx 004A69DC | fclose
T23 logger close | 00501154 | blx 004A40A8 | _Unwind_Resume
T24 cube animation lists | 0051959A | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T24 cube animation lists | 005195A4 | blxne 004A40CC | _ZdlPv
T24 cube animation lists | 005195AA | blx 004A40CC | _ZdlPv
T25 light patterns | 0051954E | blxne 004A40CC | _ZdlPv
T25 light patterns | 00519554 | blx 004A40CC | _ZdlPv
I8 callback copy | 00528C1C | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
I8 callback copy | 00528C30 | blx r2 | dynamic: bound by semantic row, or UNKNOWN closure recipient
I9 callback append | 00648062 | blx 004B9BA0 | _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE19__add_back_capacityEv
I9 callback append | 0064809C | blx 004A922C | _ZNSt6__ndk18functionIFvvEEC2ERKS2_
T19 scoped handle teardown | 004EF1C2 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T19 scoped handle teardown | 004EF1CC | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T20 logger queue stop | 00801E78 | blx 004AF3DC | _ZN4Anki4Util8Dispatch4StopEPNS1_5QueueE
T20 logger queue stop | 00801E82 | b.w 008D0E2C | _ZN4Anki4Util8Dispatch7ReleaseERPNS1_5QueueE
L12 SetLiftHeight sender | 0064077A | blx 004B96FC | _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_13SetLiftHeightE
L12 SetLiftHeight sender | 00640786 | blx 004A5368 | _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb
L12 SetLiftHeight sender | 0064078E | blx 004A5200 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv
L12 SetLiftHeight sender | 006407AA | blx 004A4FE4 | __stack_chk_fail
A1 AbortAll | 0051195A | blx 004A7ABC | _ZN4Anki5Cozmo10ActionList6CancelENS0_15RobotActionTypeE
A1 AbortAll | 00511960 | blx 004A7AC8 | _ZN4Anki5Cozmo13PathComponent5AbortEv
A1 AbortAll | 0051196A | blx 004A7AD4 | _ZNK4Anki5Cozmo16DockingComponent12AbortDockingEv
A1 AbortAll | 00511972 | blx 004A7AE0 | _ZN4Anki5Cozmo5Robot18SendAbortAnimationEv
A1 AbortAll | 0051197C | blx 004A7AEC | _ZN4Anki5Cozmo17MovementComponent13StopAllMotorsEv
A2 Cancel boundary | 0053DE30 | blx 004AA8DC | _ZN4Anki5Cozmo11ActionQueue6CancelENS0_15RobotActionTypeE
A3 Path Abort boundary | 00649124 | blx 004A505C | _ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
A3 Path Abort boundary | 00649146 | blx 004A40CC | _ZdlPv
A3 Path Abort boundary | 00649152 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
A3 Path Abort boundary | 00649164 | blx 004A4EF4 | _ZNSt6__ndk119__shared_weak_count16__release_sharedEv
A3 Path Abort boundary | 0064916A | blx 004B9CCC | _ZN4Anki5Cozmo13PathComponent9ClearPathEv
A3 Path Abort boundary | 00649188 | blx 004B9CD8 | _ZN4Anki5Cozmo13PathComponent20SetDriveToPoseStatusENS0_23ERobotDriveToPoseStatusE
A3 Path Abort boundary | 006491A4 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
A4 ClearPath | 00649246 | blx 004A41A4 | _ZN4Anki5Cozmo10VizManager9ErasePathEj
A4 ClearPath | 0064924E | blx 004B9CE4 | _ZN4Anki5Cozmo14PathDolerOuter9ClearPathEv
A4 ClearPath | 00649258 | blx 004A4F6C | _ZN4Anki16BaseStationTimer11getInstanceEv
A4 ClearPath | 0064925C | blx 004A50B0 | _ZNK4Anki16BaseStationTimer23GetCurrentTimeInSecondsEv
A4 ClearPath | 00649270 | blx 004B9CF0 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_9ClearPathE
A4 ClearPath | 0064927C | blx 004A5368 | _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb
A4 ClearPath | 00649284 | blx 004A5200 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv
A5 Dock abort | 0063BE2A | blx 004B94C8 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS0_12AbortDockingE
A5 Dock abort | 0063BE36 | blx 004A5368 | _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb
A5 Dock abort | 0063BE3E | blx 004A5200 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv
A5 Dock abort | 0063BE5A | blx 004A4FE4 | __stack_chk_fail
A6 animation abort | 00517DFE | blx 004A8158 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_14AbortAnimationE
A6 animation abort | 00517E0A | blx 004A5368 | _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb
A6 animation abort | 00517E12 | blx 004A5200 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv
A6 animation abort | 00517E2E | blx 004A4FE4 | __stack_chk_fail
A7 StopAllMotors | 0063FC10 | blx 004A44E0 | strlen
A7 StopAllMotors | 0063FC1E | bl 004E02B2 | local 004E02B2
A7 StopAllMotors | 0063FC2E | blx 004A44E0 | strlen
A7 StopAllMotors | 0063FC3A | bl 004E02B2 | local 004E02B2
A7 StopAllMotors | 0063FC4C | blx 004B9654 | _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_
A7 StopAllMotors | 0063FC5A | blxne 004A40CC | _ZdlPv
A7 StopAllMotors | 0063FC68 | blxne 004A40CC | _ZdlPv
A7 StopAllMotors | 0063FC7A | blx 004A44E0 | strlen
A7 StopAllMotors | 0063FC88 | bl 004E02B2 | local 004E02B2
A7 StopAllMotors | 0063FC98 | blx 004A44E0 | strlen
A7 StopAllMotors | 0063FCA4 | bl 004E02B2 | local 004E02B2
A7 StopAllMotors | 0063FCB6 | blx 004B9654 | _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_
A7 StopAllMotors | 0063FCC4 | blxne 004A40CC | _ZdlPv
A7 StopAllMotors | 0063FCD2 | blxne 004A40CC | _ZdlPv
A7 StopAllMotors | 0063FCE4 | blx 004A44E0 | strlen
A7 StopAllMotors | 0063FCF2 | bl 004E02B2 | local 004E02B2
A7 StopAllMotors | 0063FD02 | blx 004A44E0 | strlen
A7 StopAllMotors | 0063FD0E | bl 004E02B2 | local 004E02B2
A7 StopAllMotors | 0063FD1E | blx 004B9654 | _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_
A7 StopAllMotors | 0063FD2C | blxne 004A40CC | _ZdlPv
A7 StopAllMotors | 0063FD3A | blxne 004A40CC | _ZdlPv
A7 StopAllMotors | 0063FD4C | blx 004A44E0 | strlen
A7 StopAllMotors | 0063FD5A | bl 004E02B2 | local 004E02B2
A7 StopAllMotors | 0063FD6A | blx 004A44E0 | strlen
A7 StopAllMotors | 0063FD76 | bl 004E02B2 | local 004E02B2
A7 StopAllMotors | 0063FD86 | blx 004B9654 | _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_
A7 StopAllMotors | 0063FD94 | blxne 004A40CC | _ZdlPv
A7 StopAllMotors | 0063FDA2 | blxne 004A40CC | _ZdlPv
A7 StopAllMotors | 0063FDB4 | blx 004A44E0 | strlen
A7 StopAllMotors | 0063FDC2 | bl 004E02B2 | local 004E02B2
A7 StopAllMotors | 0063FDD2 | blx 004A44E0 | strlen
A7 StopAllMotors | 0063FDDE | bl 004E02B2 | local 004E02B2
A7 StopAllMotors | 0063FDEE | blx 004B9654 | _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_
A7 StopAllMotors | 0063FDFC | blxne 004A40CC | _ZdlPv
A7 StopAllMotors | 0063FE0A | blxne 004A40CC | _ZdlPv
A7 StopAllMotors | 0063FE10 | bl 0064099C | local 0064099C
A8 stop sender | 006409B6 | blx 004B9720 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_13StopAllMotorsE
A8 stop sender | 006409C2 | blx 004A5368 | _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb
A8 stop sender | 006409CA | blx 004A5200 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv
A8 stop sender | 006409E6 | blx 004A4FE4 | __stack_chk_fail
A9 direct zero unlock | 0063EFEA | blx 004B9660 | _ZNK4Anki5Cozmo17MovementComponent18AreAllTracksLockedEh
A9 direct zero unlock | 0063EFF8 | blx 004A5794 | _ZN4Anki5Cozmo17MovementComponent12UnlockTracksEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE
A9 direct zero unlock | 0063F010 | blx 004AAA98 | _ZN4Anki5Cozmo16AnimTrackHelpers22AnimTrackFlagsToStringEh
A9 direct zero unlock | 0063F048 | blx 004A4108 | _ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
A9 direct zero unlock | 0063F056 | blxne 004A40CC | _ZdlPv
A9 direct zero unlock | 0063F078 | blx 004A40CC | _ZdlPv
A9 direct zero unlock | 0063F090 | blx 004A4114 | _ZN4Anki4Util18sDebugBreakOnErrorEv
A9 direct zero unlock | 0063F09E | blx 004B9660 | _ZNK4Anki5Cozmo17MovementComponent18AreAllTracksLockedEh
A9 direct zero unlock | 0063F0B8 | b.w 008CCDBC | _ZN4Anki5Cozmo17MovementComponent10LockTracksEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEESA_
A10 UnlockTracks | 0063FEBE | bl 004E02B2 | local 004E02B2
A10 UnlockTracks | 0063FED2 | bl 004E02B2 | local 004E02B2
A10 UnlockTracks | 0063FEE0 | blx 004B969C | _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE4findIS4_EENS_15__tree_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEERKT_
A10 UnlockTracks | 0063FEF0 | blxne 004A40CC | _ZdlPv
A10 UnlockTracks | 0063FEFE | blxne 004A40CC | _ZdlPv
A10 UnlockTracks | 0063FF0E | blx 004B96A8 | _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE5eraseENS_21__tree_const_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEE
A10 UnlockTracks | 0063FF4A | blx 004A505C | _ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
A10 UnlockTracks | 0063FF70 | blx 004A40CC | _ZdlPv
A10 UnlockTracks | 0063FF76 | blx 004B96B4 | _ZNK4Anki5Cozmo17MovementComponent14PrintLockStateEv
A10 UnlockTracks | 0063FFA8 | blx 004B96C0 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS0_12AnimKeyFrame16EnableAnimTracksE
A10 UnlockTracks | 0063FFB4 | blx 004A5368 | _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb
A10 UnlockTracks | 0063FFBA | blx 004A5200 | _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv
T1 Tap delete | 005111F8 | blx 004A7A98 | _ZNSt6__ndk110__list_impIN4Anki5Cozmo12ObjectTappedENS_9allocatorIS3_EEE5clearEv
T1 Tap delete | 00511202 | blx 004A7AA4 | _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo23BlockTapFilterComponent13DoubleTapInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
T1 Tap delete | 0051120A | blx 004A4EF4 | _ZNSt6__ndk119__shared_weak_count16__release_sharedEv
T1 Tap delete | 00511210 | blx 004A4E4C | _ZNSt6__ndk113__vector_baseINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEED2Ev
T1 Tap delete | 00511216 | blx 004A40CC | _ZdlPv
T2 Touch/Cliff delete | 005113E0 | blx 004A78E8 | _ZN4Anki5Cozmo20TouchSensorComponentD1Ev
T2 Touch/Cliff delete | 005113E4 | blx 004A40CC | _ZdlPv
T2 Touch/Cliff delete | 005113F2 | blx 004A78F4 | _ZN4Anki5Cozmo20CliffSensorComponentD1Ev
T2 Touch/Cliff delete | 005113F6 | blx 004A40CC | _ZdlPv
T3 CubeAccel/backpack/cube delete | 00511436 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T3 CubeAccel/backpack/cube delete | 00511448 | blx 004A5560 | _ZNSt6__ndk119__shared_weak_count14__release_weakEv
T3 CubeAccel/backpack/cube delete | 00511450 | blx 004A5560 | _ZNSt6__ndk119__shared_weak_count14__release_weakEv
T3 CubeAccel/backpack/cube delete | 0051145A | blx 004A7900 | _ZNSt6__ndk16__treeINS_12__value_typeIiNS_4listINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS7_EEEEEENS_19__map_value_compareIiSB_NS_4lessIiEELb1EEENS8_ISB_EEE7destroyEPNS_11__tree_nodeISB_PvEE
T3 CubeAccel/backpack/cube delete | 00511460 | blx 004A790C | _ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv
T3 CubeAccel/backpack/cube delete | 00511466 | blx 004A40CC | _ZdlPv
T3 CubeAccel/backpack/cube delete | 00511478 | blx 004A790C | _ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv
T3 CubeAccel/backpack/cube delete | 00511482 | blx 004A7918 | _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeLightComponent10ObjectInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
T3 CubeAccel/backpack/cube delete | 00511488 | blx 004A40CC | _ZdlPv
T4 Movement delete | 00511510 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T5 Touch dtor | 0064E756 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T6 Cliff dtor | 00634008 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
T6 Cliff dtor | 0063400E | blx 004B8F28 | _ZNSt6__ndk112__deque_baseItNS_9allocatorItEEED2Ev
T7 CubeAccel deleting dtor | 00635456 | blx 004A790C | _ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv
T7 CubeAccel deleting dtor | 00635460 | blx 004B8FC4 | _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeAccelComponent12AccelHistoryEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
T7 CubeAccel deleting dtor | 0063546A | b.w 008CA88C | _ZdlPv
T8 Movement dtor | 00641D12 | blx 004B95F4 | _ZNSt6__ndk16__treeINS_12__value_typeIhN4Anki5Cozmo17MovementComponent17FaceLayerToRemoveEEENS_19__map_value_compareIhS6_NS_4lessIhEELb1EEENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE
T8 Movement dtor | 00641D20 | blx 004B9600 | _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE
T8 Movement dtor | 00641D30 | blx 004A790C | _ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv
T9 Movement deleting dtor | 00641D3E | bl 00641CFC | local 00641CFC
T9 Movement deleting dtor | 00641D46 | b.w 008CA88C | _ZdlPv
T10 RollingFileLogger dtor | 0080E0A6 | bl 00801E70 | local 00801E70
T10 RollingFileLogger dtor | 0080E0B0 | blx 004A69C4 | _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE5closeEv
T10 RollingFileLogger dtor | 0080E0C8 | blx 004A4594 | _ZNSt6__ndk18ios_base5clearEj
T10 RollingFileLogger dtor | 0080E0E4 | blx 004A6760 | _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEED2Ev
T10 RollingFileLogger dtor | 0080E0EA | blx 004A44B0 | _ZNSt6__ndk18ios_baseD2Ev
T10 RollingFileLogger dtor | 0080E0F8 | blxne 004A40CC | _ZdlPv
T10 RollingFileLogger dtor | 0080E104 | blxne 004A40CC | _ZdlPv
T10 RollingFileLogger dtor | 0080E110 | blxne 004A40CC | _ZdlPv
T10 RollingFileLogger dtor | 0080E116 | bl 00801E70 | local 00801E70
T11 RollingFileLogger deleting dtor | 0080E15E | blx 004CB84C | _ZN4Anki4Util17RollingFileLoggerD2Ev
T11 RollingFileLogger deleting dtor | 0080E166 | b.w 008CA88C | _ZdlPv
T12 shared handle zero | 004EF18A | bl 004EF1B8 | local 004EF1B8
T12 shared handle zero | 004EF192 | b.w 008CA88C | _ZdlPv
T13 handle vector | 004EAEAE | blx 004A4EF4 | _ZNSt6__ndk119__shared_weak_count16__release_sharedEv
T13 handle vector | 004EAEC0 | blx 004A40CC | _ZdlPv
T14 handle list | 005194D0 | blx 004A4EF4 | _ZNSt6__ndk119__shared_weak_count16__release_sharedEv
T14 handle list | 005194D6 | blx 004A40CC | _ZdlPv
T15 backpack tree | 005195C2 | blx 004A7900 | _ZNSt6__ndk16__treeINS_12__value_typeIiNS_4listINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS7_EEEEEENS_19__map_value_compareIiSB_NS_4lessIiEELb1EEENS8_ISB_EEE7destroyEPNS_11__tree_nodeISB_PvEE
T15 backpack tree | 005195CA | blx 004A7900 | _ZNSt6__ndk16__treeINS_12__value_typeIiNS_4listINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS7_EEEEEENS_19__map_value_compareIiSB_NS_4lessIiEELb1EEENS8_ISB_EEE7destroyEPNS_11__tree_nodeISB_PvEE
T15 backpack tree | 005195D2 | blx 004A8254 | _ZNSt6__ndk110__list_impINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS5_EEE5clearEv
T15 backpack tree | 005195DC | b.w 008CA88C | _ZdlPv
T16 cube lights tree | 005194EE | blx 004A7918 | _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeLightComponent10ObjectInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
T16 cube lights tree | 005194F6 | blx 004A7918 | _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeLightComponent10ObjectInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
T16 cube lights tree | 005194FE | blx 004A823C | _ZNSt6__ndk110__list_impIN4Anki5Cozmo12LightPatternENS_9allocatorIS3_EEE5clearEv
T16 cube lights tree | 0051950C | blx 004A8248 | _ZNSt6__ndk110__list_impIN4Anki5Cozmo18CubeLightComponent15CurrentAnimInfoENS_9allocatorIS4_EEE5clearEv
T16 cube lights tree | 0051951C | b.w 008CA88C | _ZdlPv
T17 cube accel history tree | 00635D90 | blx 004B8FC4 | _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeAccelComponent12AccelHistoryEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
T17 cube accel history tree | 00635D98 | blx 004B8FC4 | _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeAccelComponent12AccelHistoryEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
T17 cube accel history tree | 00635DA2 | blx 004B9000 | _ZNSt6__ndk16__treeINS_10shared_ptrIN4Anki5Cozmo18CubeAccelListeners18ICubeAccelListenerEEENS_4lessIS6_EENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE
T17 cube accel history tree | 00635DAC | blx 004B900C | _ZNSt6__ndk16__treeINS_12__value_typeIjN4Anki5Cozmo11ActiveAccelEEENS_19__map_value_compareIjS5_NS_4lessIjEELb1EEENS_9allocatorIS5_EEE7destroyEPNS_11__tree_nodeIS5_PvEE
T17 cube accel history tree | 00635DB6 | b.w 008CA88C | _ZdlPv
T18 Movement lock tree | 00641D7A | blx 004B9600 | _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE
T18 Movement lock tree | 00641D82 | blx 004B9600 | _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE
T18 Movement lock tree | 00641D8E | blxne 004A40CC | _ZdlPv
T18 Movement lock tree | 00641D9A | blxne 004A40CC | _ZdlPv
T18 Movement lock tree | 00641DA4 | b.w 008CA88C | _ZdlPv
L1 sleep lift child | 0052CF90 | blx 004A42A0 | _Znwj
L1 sleep lift child | 0052CFA2 | blx 004A9A48 | _ZN4Anki5Cozmo22MoveLiftToHeightActionC1ERNS0_5RobotENS1_6PresetEf
L1 sleep lift child | 0052CFB0 | blx r7 | dynamic: bound by semantic row, or UNKNOWN closure recipient
L1 sleep lift child | 0052CFB6 | blx 004A5560 | _ZNSt6__ndk119__shared_weak_count14__release_weakEv
L2 preset ctor | 00548B92 | blx 004AB1A0 | _ZN4Anki5Cozmo22MoveLiftToHeightAction15GetPresetHeightENS1_6PresetE
L2 preset ctor | 00548BA2 | blx 004AB1AC | _ZN4Anki5Cozmo22MoveLiftToHeightActionC2ERNS0_5RobotEfff
L2 preset ctor | 00548BA8 | blx 004AB1B8 | _ZN4Anki5Cozmo22MoveLiftToHeightAction13GetPresetNameENS1_6PresetE
L2 preset ctor | 00548BB4 | blx 004A7330 | _ZNSt6__ndk1plIcNS_11char_traitsIcEENS_9allocatorIcEEEENS_12basic_stringIT_T0_T1_EEPKS6_RKS9_
L2 preset ctor | 00548BBE | bl 004E462A | local 004E462A
L2 preset ctor | 00548BCC | blxne 004A40CC | _ZdlPv
L3 preset height | 00548C30 | blx 004A472C | __cxa_guard_acquire
L3 preset height | 00548C56 | blx 004AB1C4 | _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_fEEEEENS_15__tree_iteratorIS6_PNS_11__tree_nodeIS6_PvEEiEENS_21__tree_const_iteratorIS6_SO_iEERKT_DpOT0_
L3 preset height | 00548C6C | blx 004A4024 | __cxa_atexit
L3 preset height | 00548C74 | blx 004A475C | __cxa_guard_release
L3 preset height | 00548C80 | blx 004AB1D0 | _ZNKSt6__ndk13mapIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfNS_4lessIS4_EENS_9allocatorINS_4pairIKS4_fEEEEE2atERS9_
L4 preset names | 00548CEE | blx 004A472C | __cxa_guard_acquire
L4 preset names | 00548D0A | bl 004E02B2 | local 004E02B2
L4 preset names | 00548D22 | bl 004E02B2 | local 004E02B2
L4 preset names | 00548D3C | bl 004E02B2 | local 004E02B2
L4 preset names | 00548D54 | bl 004E02B2 | local 004E02B2
L4 preset names | 00548D74 | blx 004AB1E8 | _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetENS_12basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEEEEENS_19__map_value_compareIS5_SC_NS_4lessIS5_EELb1EEENS9_ISC_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_SB_EEEEENS_15__tree_iteratorISC_PNS_11__tree_nodeISC_PvEEiEENS_21__tree_const_iteratorISC_ST_iEERKT_DpOT0_
L4 preset names | 00548D8C | blxne 004A40CC | _ZdlPv
L4 preset names | 00548DA4 | blx 004A4024 | __cxa_atexit
L4 preset names | 00548DAC | blx 004A475C | __cxa_guard_release
L4 preset names | 00548DC4 | blx 004A472C | __cxa_guard_acquire
L4 preset names | 00548DDA | bl 004E02B2 | local 004E02B2
L4 preset names | 00548DEC | blx 004A4024 | __cxa_atexit
L4 preset names | 00548DF4 | blx 004A475C | __cxa_guard_release
L5 lift ctor | 005489C0 | blx 004A48E8 | _ZNSt6__ndk19to_stringEf
L5 lift ctor | 005489D0 | bl 004E80B8 | local 004E80B8
L5 lift ctor | 005489F2 | bl 004E8048 | local 004E8048
L5 lift ctor | 00548A14 | blx 004AADD4 | _ZN4Anki5Cozmo7IActionC2ERNS0_5RobotENSt6__ndk112basic_stringIcNS4_11char_traitsIcEENS4_9allocatorIcEEEENS0_15RobotActionTypeEh
L5 lift ctor | 00548A22 | blxne 004A40CC | _ZdlPv
L5 lift ctor | 00548A30 | blxne 004A40CC | _ZdlPv
L5 lift ctor | 00548A4A | blxne 004A40CC | _ZdlPv
L5 lift ctor | 00548A88 | blx 004A82D8 | _ZN4Anki5Cozmo5Robot22GetRobotMessageHandlerEv
L5 lift ctor | 00548AAC | bl 00519F8C | local 00519F8C
L5 lift ctor | 00548AC4 | blx 004A4EF4 | _ZNSt6__ndk119__shared_weak_count16__release_sharedEv
L5 lift ctor | 00548ACC | blx 004A4EF4 | _ZNSt6__ndk119__shared_weak_count16__release_sharedEv
L5 lift ctor | 00548AE2 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
L5 lift ctor | 00548B02 | blx 004A4FE4 | __stack_chk_fail
L6 lift Init | 00549090 | blx 004A4540 | _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
L6 lift Init | 005490B2 | blx 004A40CC | _ZdlPv
L6 lift Init | 00549106 | blx 004AB200 | _ZNK4Anki5Cozmo5Robot13GetLiftHeightEv
L6 lift Init | 00549110 | blx 004AB1A0 | _ZN4Anki5Cozmo22MoveLiftToHeightAction15GetPresetHeightENS1_6PresetE
L6 lift Init | 0054911A | blx 004AB1A0 | _ZN4Anki5Cozmo22MoveLiftToHeightAction15GetPresetHeightENS1_6PresetE
L6 lift Init | 00549156 | blx 004AB008 | _ZNK4Anki5Cozmo7IAction6GetRNGEv
L6 lift Init | 00549172 | blx 004AA5F4 | _ZNK4Anki4Util15RandomGenerator14RandDblInRangeEdd
L6 lift Init | 005491E6 | blx 004AB20C | _ZN4Anki5Cozmo5Robot31ConvertLiftHeightToLiftAngleRadEf
L6 lift Init | 005491EE | blx 004AB20C | _ZN4Anki5Cozmo5Robot31ConvertLiftHeightToLiftAngleRadEf
L6 lift Init | 0054921A | blx 004AB20C | _ZN4Anki5Cozmo5Robot31ConvertLiftHeightToLiftAngleRadEf
L6 lift Init | 0054925C | blx 004A9E98 | _ZN4Anki5Cozmo5Robot30ConvertLiftAngleToLiftHeightMMEf
L6 lift Init | 0054926A | blx 004A9E98 | _ZN4Anki5Cozmo5Robot30ConvertLiftAngleToLiftHeightMMEf
L6 lift Init | 005492C8 | blx 004A4540 | _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
L6 lift Init | 005492EA | blx 004A40CC | _ZdlPv
L6 lift Init | 005492F4 | blx 004AB218 | _ZNK4Anki5Cozmo22MoveLiftToHeightAction16IsLiftInPositionEv
L6 lift Init | 0054931C | blx 004AB224 | _ZN4Anki5Cozmo17MovementComponent16MoveLiftToHeightEffffPh
L7 lift CheckIfDone | 00549412 | blx 004AB218 | _ZNK4Anki5Cozmo22MoveLiftToHeightAction16IsLiftInPositionEv
L7 lift CheckIfDone | 00549454 | blx 004AB200 | _ZNK4Anki5Cozmo5Robot13GetLiftHeightEv
L7 lift CheckIfDone | 0054948A | blx 004A5B84 | _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
L7 lift CheckIfDone | 005494AC | blx 004A40CC | _ZdlPv
L7 lift CheckIfDone | 005494DC | blx 004A4540 | _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
L7 lift CheckIfDone | 005494FE | blx 004A40CC | _ZdlPv
L7 lift CheckIfDone | 00549538 | blx 004A5B84 | _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
L7 lift CheckIfDone | 0054955A | blx 004A40CC | _ZdlPv
L8 lift in position | 00548FF8 | blx 004AB200 | _ZNK4Anki5Cozmo5Robot13GetLiftHeightEv
L9 lift movement | 0064073A | bl 00640744 | local 00640744
L10 lift conversion | 005170F0 | b.w 008CAE7C | asinf
L11 lift dtor | 0054D14C | blx 004A4EF4 | _ZNSt6__ndk119__shared_weak_count16__release_sharedEv
L11 lift dtor | 0054D156 | b.w 008CB50C | _ZN4Anki5Cozmo13IActionRunnerD2Ev
D1 PrintLockState | 00641110 | blx 004A445C | _ZNSt6__ndk18ios_base4initEPv
D1 PrintLockState | 00641144 | blx 004A4468 | _ZNSt6__ndk16localeC1Ev
D1 PrintLockState | 00641150 | blx 004A403C | __aeabi_memclr4
D1 PrintLockState | 00641172 | bl 004E4598 | local 004E4598
D1 PrintLockState | 00641182 | blxne 004A40CC | _ZdlPv
D1 PrintLockState | 006411C4 | blx 004AAA98 | _ZN4Anki5Cozmo16AnimTrackHelpers22AnimTrackFlagsToStringEh
D1 PrintLockState | 006411DE | blx 004A4474 | _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j
D1 PrintLockState | 006411E6 | blx 004A4474 | _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j
D1 PrintLockState | 006411EC | blx 004A8134 | _ZNSt6__ndk113basic_ostreamIcNS_11char_traitsIcEEElsEj
D1 PrintLockState | 006411FA | blx 004A4474 | _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j
D1 PrintLockState | 0064120A | blxne 004A40CC | _ZdlPv
D1 PrintLockState | 00641240 | bl 004E02B2 | local 004E02B2
D1 PrintLockState | 00641268 | bl 004E02B2 | local 004E02B2
D1 PrintLockState | 00641284 | blx 004A4474 | _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j
D1 PrintLockState | 0064128C | blx 004A4474 | _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j
D1 PrintLockState | 006412A4 | blx 004A4474 | _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j
D1 PrintLockState | 006412AC | blx 004A4474 | _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j
D1 PrintLockState | 006412BA | blxne 004A40CC | _ZdlPv
D1 PrintLockState | 006412C8 | blxne 004A40CC | _ZdlPv
D1 PrintLockState | 00641300 | blx 004A4474 | _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j
D1 PrintLockState | 0064132A | blx 004A448C | _ZNKSt6__ndk115basic_stringbufIcNS_11char_traitsIcEENS_9allocatorIcEEE3strEv
D1 PrintLockState | 0064134A | blx 004A5B84 | _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
D1 PrintLockState | 00641358 | blxne 004A40CC | _ZdlPv
D1 PrintLockState | 0064137A | blx 004A40CC | _ZdlPv
D1 PrintLockState | 006413AA | blxne 004A40CC | _ZdlPv
D1 PrintLockState | 006413BA | blx 004A44A4 | _ZNSt6__ndk16localeD1Ev
D1 PrintLockState | 006413C0 | blx 004A44B0 | _ZNSt6__ndk18ios_baseD2Ev
D2 track strings | 006305FC | blx 004B8CAC | _ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE
D2 track strings | 0063060C | blx 004B8CAC | _ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE
D2 track strings | 0063061C | blx 004A44E0 | strlen
D2 track strings | 00630626 | bl 004E02B2 | local 004E02B2
D2 track strings | 0063065E | blx 004A445C | _ZNSt6__ndk18ios_base4initEPv
D2 track strings | 00630692 | blx 004A4468 | _ZNSt6__ndk16localeC1Ev
D2 track strings | 006306A0 | blx 004A403C | __aeabi_memclr4
D2 track strings | 006306C2 | bl 004E4598 | local 004E4598
D2 track strings | 006306D8 | blxne 004A40CC | _ZdlPv
D2 track strings | 0063070C | blx 004A4474 | _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j
D2 track strings | 00630712 | blx 004B8CAC | _ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE
D2 track strings | 0063071A | blx 004A44E0 | strlen
D2 track strings | 00630724 | blx 004A4474 | _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j
D2 track strings | 00630734 | blx 004A448C | _ZNKSt6__ndk115basic_stringbufIcNS_11char_traitsIcEENS_9allocatorIcEEE3strEv
D2 track strings | 00630768 | blxne 004A40CC | _ZdlPv
D2 track strings | 00630778 | blx 004A44A4 | _ZNSt6__ndk16localeD1Ev
D2 track strings | 0063077E | blx 004A44B0 | _ZNSt6__ndk18ios_baseD2Ev
N1 NV delete | 005112E4 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
N2 NV dtor | 00643EEA | blx 004A4EF4 | _ZNSt6__ndk119__shared_weak_count16__release_sharedEv
N2 NV dtor | 00643EFE | blx 004B9870 | _ZNSt6__ndk112__hash_tableIhNS_4hashIhEENS_8equal_toIhEENS_9allocatorIhEEED2Ev
N2 NV dtor | 00643F14 | blx 004A40CC | _ZdlPv
N2 NV dtor | 00643F1A | blx 004A4E4C | _ZNSt6__ndk113__vector_baseINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEED2Ev
N2 NV dtor | 00643F22 | blx 004B987C | _ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEED2Ev
N2 NV dtor | 00643F2A | blx 004B9888 | _ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEED2Ev
N2 NV dtor | 00643F40 | blx 004A40CC | _ZdlPv
N2 NV dtor | 00643F48 | blx 004B9894 | _ZN4Anki5Cozmo22RobotDataBackupManagerD1Ev
N2 NV dtor | 00643F50 | bl 00643E80 | local 00643E80
N2 NV dtor | 00643F6A | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
N2 NV dtor | 00643F7C | blx 004A40CC | _ZdlPv
N2 NV dtor | 00643F82 | blx 004A40CC | _ZdlPv
N3 NV deleting dtor | 00643F96 | blx 004B9948 | _ZN4Anki5Cozmo18NVStorageComponentD2Ev
N3 NV deleting dtor | 00643F9E | b.w 008CA88C | _ZdlPv
N4 active request dtor | 00643E9A | blx 004A40CC | _ZdlPv
N4 active request dtor | 00643EA0 | blx 004A40CC | _ZdlPv
N4 active request dtor | 00643EBE | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
N5 idle deque clear | 00646BF2 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
N5 idle deque clear | 00646C1A | blx 004A40CC | _ZdlPv
N6 requests deque clear | 00646D04 | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
N6 requests deque clear | 00646D1C | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
N6 requests deque clear | 00646D44 | blx 004A40CC | _ZdlPv
N7 idle deque dtor | 00646B68 | blx 004B9A8C | _ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEE5clearEv
N7 idle deque dtor | 00646B78 | blx 004A40CC | _ZdlPv
N7 idle deque dtor | 00646B86 | b.w 008CCE8C | _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEENS_9allocatorIS4_EEED2Ev
N8 requests deque dtor | 00646C70 | blx 004B9AA4 | _ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEE5clearEv
N8 requests deque dtor | 00646C80 | blx 004A40CC | _ZdlPv
N8 requests deque dtor | 00646C8E | b.w 008CCE9C | _ZNSt6__ndk114__split_bufferIPN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS5_EEED2Ev
I1 ready callback registration | 00528A64 | blx 004A922C | _ZNSt6__ndk18functionIFvvEEC2ERKS2_
I1 ready callback registration | 00528A6E | blx 004A9238 | _ZN4Anki5Cozmo18NVStorageComponent24AddOneShotOnIdleCallbackENSt6__ndk18functionIFvvEEE
I2 AddOneShot | 00645C28 | blx 004B9A38 | _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE12emplace_backIJRS3_EEEvDpOT_
I2 AddOneShot | 00645C32 | b.w 008CCE6C | _ZN4Anki5Cozmo18NVStorageComponent22ProcessOnIdleCallbacksEv
I3 Update idle | 006456CC | blx 004B9A14 | _ZN4Anki5Cozmo18NVStorageComponent14ProcessRequestEv
I3 Update idle | 006456EC | beq.w 008CCE6C | _ZN4Anki5Cozmo18NVStorageComponent22ProcessOnIdleCallbacksEv
I3 Update idle | 006456F0 | blx 004A4FE4 | __stack_chk_fail
I4 ProcessRequest idle gates | 00644FF4 | beq.w 00645488 | local 00645488
I4 ProcessRequest idle gates | 00645028 | bhi.w 00645326 | local 00645326
I5 ProcessOnIdleCallbacks | 00645B56 | blx 004A5B84 | _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
I5 ProcessOnIdleCallbacks | 00645B78 | blx 004A40CC | _ZdlPv
I5 ProcessOnIdleCallbacks | 00645B9A | blx 004B2A3C | _ZNKSt6__ndk18functionIFvvEEclEv
I5 ProcessOnIdleCallbacks | 00645BA0 | blx 004B9A2C | _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE9pop_frontEv
I6 callback invoke | 005BF7C4 | bx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
I6 callback invoke | 005BF7CA | blx 004A42B8 | __cxa_allocate_exception
I6 callback invoke | 005BF7E8 | blx 004A42D0 | __cxa_throw
I7 callback pop | 006486FC | blx r1 | dynamic: bound by semantic row, or UNKNOWN closure recipient
I7 callback pop | 00648716 | blx 004A40CC | _ZdlPv

ROW-RANGE INSTRUCTIONS
L13 motor ack callback

RANGE 0054D748..0054D7DC
0054D748: push       {r4, r5, r7, lr}
0054D74A: sub        sp, #0x18
0054D74C: ldr        r4, [r0, #4]
0054D74E: ldrb.w     r0, [r4, #0x95]
0054D752: cbz        r0, #0x54d7b0
0054D754: add.w      r0, r1, #0xc
0054D758: ldrb.w     r5, [r4, #0x94]
0054D75C: blx        #0x4ab44c ; _ZNK4Anki5Cozmo14RobotInterface13RobotToEngine18Get_motorActionAckEv -> 007B385C size=4
0054D760: ldrb       r0, [r0]
0054D762: cmp        r5, r0
0054D764: bne        #0x54d7b0
0054D766: movs       r0, #0
0054D768: ldr        r3, [pc, #0x74] ; literal[0054D7E0]=0069C674
0054D76A: strd       r0, r0, [sp, #0xc]
0054D76E: str        r0, [sp, #0x14]
0054D770: add        r3, pc
0054D772: ldr        r0, [pc, #0x68] ; literal[0054D7DC]=0069C26A
0054D774: ldr        r1, [r4, #0x60]
0054D776: add        r0, pc
0054D778: ldrb.w     r2, [r4, #0x94]
0054D77C: strd       r1, r2, [sp]
0054D780: adr        r1, #0x60 ; ADR[0054D7E4]=b'MoveLiftToHeightAction.MotorActionAcked'
0054D782: add        r2, sp, #0xc
0054D784: blx        #0x4a505c ; _ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D42C size=14
0054D788: ldr        r0, [sp, #0xc]
0054D78A: cbz        r0, #0x54d7aa
0054D78C: ldr        r1, [sp, #0x10]
0054D78E: cmp        r1, r0
0054D790: itttt      ne
0054D792: subne.w    r2, r1, #8
0054D796: subne      r2, r2, r0
0054D798: mvnne      r3, #7
0054D79C: bicne.w    r2, r3, r2
0054D7A0: itt        ne
0054D7A2: addne      r1, r1, r2
0054D7A4: strne      r1, [sp, #0x10]
0054D7A6: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0054D7AA: movs       r0, #1
0054D7AC: strb.w     r0, [r4, #0x96]
0054D7B0: add        sp, #0x18
0054D7B2: pop        {r4, r5, r7, pc}
0054D7B4: mov        r4, r0
0054D7B6: ldr        r0, [sp, #0xc]
0054D7B8: cbz        r0, #0x54d7d6
0054D7BA: ldr        r1, [sp, #0x10]
0054D7BC: cmp        r1, r0
0054D7BE: beq        #0x54d7d2
0054D7C0: sub.w      r2, r1, #8
0054D7C4: mvn        r3, #7
0054D7C8: subs       r2, r2, r0
0054D7CA: bic.w      r2, r3, r2
0054D7CE: add        r1, r2
0054D7D0: str        r1, [sp, #0x10]
0054D7D2: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0054D7D6: mov        r0, r4
0054D7D8: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
L14 StopLift receiver

RANGE 00640B3C..00640C00 _ZN4Anki5Cozmo17MovementComponent8StopLiftEv
00640B3C: push.w     {r4, r5, r6, r7, r8, lr}
00640B40: sub        sp, #0x28
00640B42: mov        r4, r0
00640B44: ldrb.w     r0, [r4, #0xb8]
00640B48: cbnz       r0, #0x640b56
00640B4A: ldrb.w     r0, [r4, #0xb9]
00640B4E: cbnz       r0, #0x640b56
00640B50: ldrb.w     r0, [r4, #0xba]
00640B54: cbz        r0, #0x640bc6
00640B56: ldrb.w     r0, [r4, #0xd4]
00640B5A: cbnz       r0, #0x640bc6
00640B5C: ldr.w      r6, [r4, #0xc4]
00640B60: movs       r5, #0
00640B62: str        r5, [sp, #0x20]
00640B64: strd       r5, r5, [sp, #0x18]
00640B68: mov        r0, r6
00640B6A: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
00640B6E: add.w      r8, sp, #0x18
00640B72: mov        r2, r0
00640B74: mov        r1, r6
00640B76: mov        r0, r8
00640B78: bl         #0x4e02b2
00640B7C: ldr.w      r7, [r4, #0xc4]
00640B80: str        r5, [sp, #0x10]
00640B82: strd       r5, r5, [sp, #8]
00640B86: mov        r0, r7
00640B88: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
00640B8C: mov        r2, r0
00640B8E: add        r6, sp, #8
00640B90: mov        r1, r7
00640B92: mov        r0, r6
00640B94: bl         #0x4e02b2
00640B98: add.w      r2, r4, #0xba
00640B9C: mov        r0, r4
00640B9E: movs       r1, #0
00640BA0: movs       r3, #2
00640BA2: strd       r8, r6, [sp]
00640BA6: blx        #0x4b9654 ; _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_ -> 0063EFB0 size=1C4
00640BAA: ldrb.w     r0, [sp, #8]
00640BAE: lsls       r0, r0, #0x1f
00640BB0: itt        ne
00640BB2: ldrne      r0, [sp, #0x10]
00640BB4: blxne      #0x4a40cc
00640BB8: ldrb.w     r0, [sp, #0x18]
00640BBC: lsls       r0, r0, #0x1f
00640BBE: itt        ne
00640BC0: ldrne      r0, [sp, #0x20]
00640BC2: blxne      #0x4a40cc
00640BC6: ldr        r0, [r4, #4]
00640BC8: movs       r1, #0
00640BCA: str        r1, [sp, #0x18]
00640BCC: add        r1, sp, #0x18
00640BCE: bl         #0x640c00
00640BD2: add        sp, #0x28
00640BD4: pop.w      {r4, r5, r6, r7, r8, pc}
00640BD8: mov        r4, r0
00640BDA: ldrb.w     r0, [sp, #8]
00640BDE: lsls       r0, r0, #0x1f
00640BE0: itt        ne
00640BE2: ldrne      r0, [sp, #0x10]
00640BE4: blxne      #0x4a40cc
00640BE8: b          #0x640bec
00640BEA: mov        r4, r0
00640BEC: ldrb.w     r0, [sp, #0x18]
00640BF0: lsls       r0, r0, #0x1f
00640BF2: itt        ne
00640BF4: ldrne      r0, [sp, #0x20]
00640BF6: blxne      #0x4a40cc
00640BFA: mov        r0, r4
00640BFC: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
L15 MoveLift sender

RANGE 00640C00..00640C68
00640C00: push       {r4, r5, r7, lr}
00640C02: sub.w      sp, sp, #0x410
00640C06: mov        r4, r0
00640C08: ldr        r0, [pc, #0x5c] ; literal[00640C68]=009FDC3C
00640C0A: add        r5, sp, #4
00640C0C: add        r0, pc
00640C0E: ldr        r0, [r0]
00640C10: ldr        r0, [r0]
00640C12: str.w      r0, [sp, #0x40c]
00640C16: ldr        r0, [r1]
00640C18: mov        r1, sp
00640C1A: str        r0, [sp]
00640C1C: mov        r0, r5
00640C1E: blx        #0x4b9690 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_8MoveLiftE -> 007A8430 size=A
00640C22: mov        r0, r4
00640C24: mov        r1, r5
00640C26: movs       r2, #1
00640C28: movs       r3, #0
00640C2A: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
00640C2E: mov        r4, r0
00640C30: add        r0, sp, #4
00640C32: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
00640C36: ldr        r0, [pc, #0x34] ; literal[00640C6C]=009FDC0C
00640C38: ldr.w      r1, [sp, #0x40c]
00640C3C: add        r0, pc
00640C3E: ldr        r0, [r0]
00640C40: ldr        r0, [r0]
00640C42: subs       r0, r0, r1
00640C44: ittt       eq
00640C46: moveq      r0, r4
00640C48: addeq.w    sp, sp, #0x410
00640C4C: popeq      {r4, r5, r7, pc}
00640C4E: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
00640C52: bl         #0x4e39f8
00640C56: mov        r4, r0
00640C58: add        r0, sp, #4
00640C5A: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
00640C5E: mov        r0, r4
00640C60: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
00640C64: bl         #0x4e39f8
T26 file sync

RANGE 00501398..005014C8 _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE4syncEv
00501398: push.w     {r4, r5, r6, r7, r8, lr}
0050139C: sub        sp, #0x18
0050139E: mov        r4, r0
005013A0: ldr        r0, [r4, #0x40]
005013A2: cmp        r0, #0
005013A4: beq        #0x50147c
005013A6: ldr        r0, [r4, #0x44]
005013A8: cmp        r0, #0
005013AA: beq.w      #0x5014ae
005013AE: ldr        r1, [r4, #0x5c]
005013B0: tst.w      r1, #0x10
005013B4: bne        #0x5013d0
005013B6: lsls       r1, r1, #0x1c
005013B8: bpl        #0x50147c
005013BA: ldrd       r1, r2, [r4, #0x50]
005013BE: strd       r1, r2, [sp, #8]
005013C2: ldrb.w     r1, [r4, #0x62]
005013C6: cbz        r1, #0x501430
005013C8: ldrd       r0, r1, [r4, #0xc]
005013CC: subs       r5, r1, r0
005013CE: b          #0x50144c
005013D0: ldrd       r0, r1, [r4, #0x14]
005013D4: cmp        r1, r0
005013D6: beq        #0x5013ec
005013D8: ldr        r0, [r4]
005013DA: mov.w      r1, #-1
005013DE: mov.w      r5, #-1
005013E2: ldr        r2, [r0, #0x34]
005013E4: mov        r0, r4
005013E6: blx        r2
005013E8: adds       r0, #1
005013EA: beq        #0x50147e
005013EC: add.w      r5, r4, #0x48
005013F0: add.w      r8, sp, #0x14
005013F4: ldr        r0, [r4, #0x44]
005013F6: ldr        r2, [r4, #0x20]
005013F8: ldr        r1, [r4, #0x34]
005013FA: ldr        r3, [r0]
005013FC: ldr        r7, [r3, #0x14]
005013FE: adds       r3, r2, r1
00501400: mov        r1, r5
00501402: str.w      r8, [sp]
00501406: blx        r7
00501408: mov        r6, r0
0050140A: ldr        r0, [r4, #0x20]
0050140C: ldr        r1, [sp, #0x14]
0050140E: ldr        r3, [r4, #0x40]
00501410: subs       r7, r1, r0
00501412: movs       r1, #1
00501414: mov        r2, r7
00501416: blx        #0x4a6a18 ; fwrite IMPORT (resolve packaged dependencies before calling external)
0050141A: cmp        r0, r7
0050141C: bne        #0x50145a
0050141E: cmp        r6, #1
00501420: beq        #0x5013f4
00501422: cmp        r6, #2
00501424: beq        #0x50145a
00501426: ldr        r0, [r4, #0x40]
00501428: blx        #0x4a6a24 ; fflush IMPORT (resolve packaged dependencies before calling external)
0050142C: cbnz       r0, #0x50145a
0050142E: b          #0x50147c
00501430: ldr        r1, [r0]
00501432: ldr        r1, [r1, #0x18]
00501434: blx        r1
00501436: ldrd       r3, r1, [r4, #0x24]
0050143A: cmp        r0, #1
0050143C: sub.w      r5, r1, r3
00501440: blt        #0x501486
00501442: ldrd       r1, r2, [r4, #0xc]
00501446: subs       r1, r2, r1
00501448: mla        r5, r1, r0, r5
0050144C: movs       r6, #0
0050144E: ldr        r0, [r4, #0x40]
00501450: rsbs       r1, r5, #0
00501452: movs       r2, #1
00501454: blx        #0x4a69f4 ; fseeko IMPORT (resolve packaged dependencies before calling external)
00501458: cbz        r0, #0x501460
0050145A: mov.w      r5, #-1
0050145E: b          #0x50147e
00501460: cmp        r6, #1
00501462: itt        eq
00501464: ldrdeq     r0, r1, [sp, #8]
00501468: strdeq     r0, r1, [r4, #0x48]
0050146C: ldr        r0, [r4, #0x20]
0050146E: movs       r1, #0
00501470: strd       r1, r1, [r4, #8]
00501474: str        r1, [r4, #0x10]
00501476: str        r1, [r4, #0x5c]
00501478: strd       r0, r0, [r4, #0x24]
0050147C: movs       r5, #0
0050147E: mov        r0, r5
00501480: add        sp, #0x18
00501482: pop.w      {r4, r5, r6, r7, r8, pc}
00501486: ldrd       r1, r0, [r4, #0xc]
0050148A: cmp        r1, r0
0050148C: beq        #0x50144c
0050148E: ldr        r0, [r4, #0x44]
00501490: ldr        r7, [r4, #8]
00501492: ldr        r2, [r4, #0x20]
00501494: ldr        r6, [r0]
00501496: subs       r1, r1, r7
00501498: ldr        r6, [r6, #0x20]
0050149A: str        r1, [sp]
0050149C: add        r1, sp, #8
0050149E: blx        r6
005014A0: ldrd       r1, r2, [r4, #0x20]
005014A4: subs       r0, r5, r0
005014A6: add        r0, r2
005014A8: movs       r6, #1
005014AA: subs       r5, r0, r1
005014AC: b          #0x50144e
005014AE: movs       r0, #4
005014B0: blx        #0x4a42b8 ; __cxa_allocate_exception IMPORT (resolve packaged dependencies before calling external)
005014B4: blx        #0x4a6a0c ; _ZNSt8bad_castC1Ev IMPORT (resolve packaged dependencies before calling external)
005014B8: ldr        r1, [pc, #0xc] ; literal[005014C8]=00B3D450
005014BA: ldr        r2, [pc, #0x10] ; literal[005014CC]=00B3D452
005014BC: add        r1, pc
005014BE: add        r2, pc
005014C0: ldr        r1, [r1]
005014C2: ldr        r2, [r2]
005014C4: blx        #0x4a42d0 ; __cxa_throw IMPORT (resolve packaged dependencies before calling external)
T27 file overflow

RANGE 00501684..005017B0 _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE8overflowEi
00501684: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
00501688: sub        sp, #0x24
0050168A: mov        r5, r0
0050168C: mov        fp, r1
0050168E: ldr        r0, [r5, #0x40]
00501690: cmp        r0, #0
00501692: beq        #0x50176c
00501694: mov        r0, r5
00501696: blx        #0x4a6a48 ; _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE12__write_modeEv -> 005017F8 size=38
0050169A: ldrd       sb, r3, [r5, #0x14]
0050169E: adds.w     sl, fp, #1
005016A2: ldr        r4, [r5, #0x1c]
005016A4: beq        #0x5016c2
005016A6: cbnz       r3, #0x5016b4
005016A8: add.w      r3, sp, #0x23
005016AC: adds       r0, r3, #1
005016AE: strd       r3, r3, [r5, #0x14]
005016B2: str        r0, [r5, #0x1c]
005016B4: strb.w     fp, [r3]
005016B8: ldrd       r2, r0, [r5, #0x14]
005016BC: adds       r3, r0, #1
005016BE: str        r3, [r5, #0x18]
005016C0: b          #0x5016c4
005016C2: mov        r2, sb
005016C4: cmp        r3, r2
005016C6: beq        #0x5016e6
005016C8: ldrb.w     r0, [r5, #0x62]
005016CC: cbz        r0, #0x5016f2
005016CE: subs       r6, r3, r2
005016D0: ldr        r3, [r5, #0x40]
005016D2: mov        r0, r2
005016D4: movs       r1, #1
005016D6: mov        r2, r6
005016D8: blx        #0x4a6a18 ; fwrite IMPORT (resolve packaged dependencies before calling external)
005016DC: cmp        r0, r6
005016DE: bne        #0x50176c
005016E0: strd       sb, sb, [r5, #0x14]
005016E4: str        r4, [r5, #0x1c]
005016E6: cmp.w      sl, #0
005016EA: it         eq
005016EC: moveq.w    fp, #0
005016F0: b          #0x501770
005016F2: str        r4, [sp, #0x14]
005016F4: ldr        r1, [r5, #0x20]
005016F6: str        r1, [sp, #0x1c]
005016F8: ldr        r0, [r5, #0x44]
005016FA: cmp        r0, #0
005016FC: beq        #0x50178e
005016FE: ldr        r7, [r0]
00501700: add.w      ip, sp, #0x1c
00501704: ldr        r6, [r5, #0x34]
00501706: add.w      r8, r5, #0x48
0050170A: add        r4, sp, #0x18
0050170C: ldr        r7, [r7, #0xc]
0050170E: add        r6, r1
00501710: strd       r4, r1, [sp]
00501714: strd       r6, ip, [sp, #8]
00501718: b          #0x50175e
0050171A: cmp        r7, #3
0050171C: beq        #0x501778
0050171E: cmp        r7, #1
00501720: bhi        #0x50176c
00501722: ldr        r1, [sp, #0x1c]
00501724: ldr        r0, [r5, #0x20]
00501726: ldr        r3, [r5, #0x40]
00501728: subs       r6, r1, r0
0050172A: movs       r1, #1
0050172C: mov        r2, r6
0050172E: blx        #0x4a6a18 ; fwrite IMPORT (resolve packaged dependencies before calling external)
00501732: cmp        r0, r6
00501734: bne        #0x50176c
00501736: cmp        r7, #1
00501738: bne        #0x50178a
0050173A: ldr        r2, [sp, #0x18]
0050173C: ldr        r3, [r5, #0x18]
0050173E: str        r2, [r5, #0x14]
00501740: str        r3, [r5, #0x1c]
00501742: ldr        r0, [r5, #0x44]
00501744: cmp        r0, #0
00501746: beq        #0x50178e
00501748: ldr        r7, [r0]
0050174A: add        r4, sp, #0x18
0050174C: ldr        r1, [r5, #0x20]
0050174E: ldr        r6, [r5, #0x34]
00501750: ldr        r7, [r7, #0xc]
00501752: add        r6, r1
00501754: strd       r4, r1, [sp]
00501758: str        r6, [sp, #8]
0050175A: add        r1, sp, #0x1c
0050175C: str        r1, [sp, #0xc]
0050175E: mov        r1, r8
00501760: blx        r7
00501762: mov        r7, r0
00501764: ldr        r0, [r5, #0x14]
00501766: ldr        r1, [sp, #0x18]
00501768: cmp        r1, r0
0050176A: bne        #0x50171a
0050176C: mov.w      fp, #-1
00501770: mov        r0, fp
00501772: add        sp, #0x24
00501774: pop.w      {r4, r5, r6, r7, r8, sb, sl, fp, pc}
00501778: ldr        r1, [r5, #0x18]
0050177A: ldr        r3, [r5, #0x40]
0050177C: subs       r6, r1, r0
0050177E: movs       r1, #1
00501780: mov        r2, r6
00501782: blx        #0x4a6a18 ; fwrite IMPORT (resolve packaged dependencies before calling external)
00501786: cmp        r0, r6
00501788: bne        #0x50176c
0050178A: ldr        r4, [sp, #0x14]
0050178C: b          #0x5016e0
0050178E: movs       r0, #4
00501790: blx        #0x4a42b8 ; __cxa_allocate_exception IMPORT (resolve packaged dependencies before calling external)
00501794: blx        #0x4a6a0c ; _ZNSt8bad_castC1Ev IMPORT (resolve packaged dependencies before calling external)
00501798: ldr        r1, [pc, #0xc] ; literal[005017A8]=00B3D170
0050179A: ldr        r2, [pc, #0x10] ; literal[005017AC]=00B3D172
0050179C: add        r1, pc
0050179E: add        r2, pc
005017A0: ldr        r1, [r1]
005017A2: ldr        r2, [r2]
005017A4: blx        #0x4a42d0 ; __cxa_throw IMPORT (resolve packaged dependencies before calling external)
005017A8: bne        #0x50188c
005017AA: lsls       r3, r6, #2
005017AC: bne        #0x501894
005017AE: lsls       r3, r6, #2
T21 unsubscribe robot signal

RANGE 0051D8F8..0051D936
0051D8F8: push       {r4, r5, r7, lr}
0051D8FA: mov        r4, r0
0051D8FC: ldr        r0, [r4, #0x10]
0051D8FE: cbz        r0, #0x51d92c
0051D900: blx        #0x4a556c ; _ZNSt6__ndk119__shared_weak_count4lockEv IMPORT (resolve packaged dependencies before calling external)
0051D904: cbz        r0, #0x51d92c
0051D906: ldr        r5, [r4, #0xc]
0051D908: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
0051D90C: cbz        r5, #0x51d92c
0051D90E: ldr        r0, [r4, #4]
0051D910: ldr        r1, [r0]
0051D912: cbz        r1, #0x51d92c
0051D914: ldr        r0, [r1]
0051D916: cmp        r0, #0
0051D918: it         eq
0051D91A: moveq      r0, r1
0051D91C: cmp        r0, r1
0051D91E: beq        #0x51d92c
0051D920: ldr        r2, [r4, #8]
0051D922: cmp        r2, r0
0051D924: beq        #0x51d92e
0051D926: ldr        r0, [r0]
0051D928: cmp        r0, r1
0051D92A: bne        #0x51d922
0051D92C: pop        {r4, r5, r7, pc}
0051D92E: pop.w      {r4, r5, r7, lr}
0051D932: b.w        #0x51d338
T22 signal callback unlink

RANGE 0051D338..0051D3A6
0051D338: push       {r4, lr}
0051D33A: mov        r4, r0
0051D33C: add.w      r1, r4, #8
0051D340: ldr        r0, [r4, #0x18]
0051D342: cmp        r1, r0
0051D344: beq        #0x51d34e
0051D346: cbz        r0, #0x51d354
0051D348: ldr        r1, [r0]
0051D34A: ldr        r1, [r1, #0x14]
0051D34C: b          #0x51d352
0051D34E: ldr        r1, [r0]
0051D350: ldr        r1, [r1, #0x10]
0051D352: blx        r1
0051D354: ldr        r0, [r4]
0051D356: movs       r1, #0
0051D358: str        r1, [r4, #0x18]
0051D35A: cbz        r0, #0x51d360
0051D35C: ldr        r1, [r4, #4]
0051D35E: str        r1, [r0, #4]
0051D360: ldr        r0, [r4, #4]
0051D362: cbz        r0, #0x51d368
0051D364: ldr        r1, [r4]
0051D366: str        r1, [r0]
0051D368: mov        r0, r4
0051D36A: pop.w      {r4, lr}
0051D36E: b.w        #0x51d372
0051D372: push       {r4, lr}
0051D374: mov        r4, r0
0051D376: cmp        r4, #0
0051D378: ldr        r0, [r4, #0x20]
0051D37A: sub.w      r0, r0, #1
0051D37E: str        r0, [r4, #0x20]
0051D380: beq        #0x51d396
0051D382: cbnz       r0, #0x51d396
0051D384: ldr        r0, [r4, #0x18]
0051D386: add.w      r1, r4, #8
0051D38A: cmp        r1, r0
0051D38C: beq        #0x51d398
0051D38E: cbz        r0, #0x51d39e
0051D390: ldr        r1, [r0]
0051D392: ldr        r1, [r1, #0x14]
0051D394: b          #0x51d39c
0051D396: pop        {r4, pc}
0051D398: ldr        r1, [r0]
0051D39A: ldr        r1, [r1, #0x10]
0051D39C: blx        r1
0051D39E: mov        r0, r4
0051D3A0: pop.w      {r4, lr}
T23 logger close

RANGE 0050111C..00501158 _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE5closeEv
0050111C: push       {r4, r5, r6, lr}
0050111E: mov        r4, r0
00501120: ldr        r5, [r4, #0x40]
00501122: cbz        r5, #0x50113c
00501124: ldr        r0, [r4]
00501126: ldr        r1, [r0, #0x18]
00501128: mov        r0, r4
0050112A: blx        r1
0050112C: mov        r6, r0
0050112E: mov        r0, r5
00501130: blx        #0x4a69dc ; fclose IMPORT (resolve packaged dependencies before calling external)
00501134: mov        r1, r0
00501136: movs       r0, #0
00501138: cbz        r1, #0x501140
0050113A: pop        {r4, r5, r6, pc}
0050113C: movs       r0, #0
0050113E: pop        {r4, r5, r6, pc}
00501140: str        r0, [r4, #0x40]
00501142: cmp        r6, #0
00501144: it         eq
00501146: moveq      r0, r4
00501148: pop        {r4, r5, r6, pc}
0050114A: mov        r4, r0
0050114C: mov        r0, r5
0050114E: blx        #0x4a69dc ; fclose IMPORT (resolve packaged dependencies before calling external)
00501152: mov        r0, r4
00501154: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
T24 cube animation lists

RANGE 00519560..005195B6 _ZNSt6__ndk110__list_impIN4Anki5Cozmo18CubeLightComponent15CurrentAnimInfoENS_9allocatorIS4_EEE5clearEv
00519560: push       {r4, r5, r6, lr}
00519562: mov        r4, r0
00519564: ldr        r0, [r4, #8]
00519566: cbz        r0, #0x5195b4
00519568: ldrd       r0, r5, [r4]
0051956C: cmp        r5, r4
0051956E: ldr        r1, [r5]
00519570: ldr        r2, [r0, #4]
00519572: str        r2, [r1, #4]
00519574: ldr        r0, [r0, #4]
00519576: ldr        r1, [r5]
00519578: str        r1, [r0]
0051957A: mov.w      r0, #0
0051957E: str        r0, [r4, #8]
00519580: beq        #0x5195b4
00519582: ldr        r6, [r5, #4]
00519584: add.w      r1, r5, #0x28
00519588: ldr        r0, [r5, #0x38]
0051958A: cmp        r1, r0
0051958C: beq        #0x519596
0051958E: cbz        r0, #0x51959c
00519590: ldr        r1, [r0]
00519592: ldr        r1, [r1, #0x14]
00519594: b          #0x51959a
00519596: ldr        r1, [r0]
00519598: ldr        r1, [r1, #0x10]
0051959A: blx        r1
0051959C: ldrb       r0, [r5, #8]
0051959E: lsls       r0, r0, #0x1f
005195A0: itt        ne
005195A2: ldrne      r0, [r5, #0x10]
005195A4: blxne      #0x4a40cc
005195A8: mov        r0, r5
005195AA: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005195AE: cmp        r6, r4
005195B0: mov        r5, r6
005195B2: bne        #0x519582
005195B4: pop        {r4, r5, r6, pc}
T25 light patterns

RANGE 00519522..00519560 _ZNSt6__ndk110__list_impIN4Anki5Cozmo12LightPatternENS_9allocatorIS3_EEE5clearEv
00519522: push       {r4, r5, r6, lr}
00519524: mov        r4, r0
00519526: ldr        r0, [r4, #8]
00519528: cbz        r0, #0x51955e
0051952A: ldrd       r0, r5, [r4]
0051952E: cmp        r5, r4
00519530: ldr        r1, [r5]
00519532: ldr        r2, [r0, #4]
00519534: str        r2, [r1, #4]
00519536: ldr        r0, [r0, #4]
00519538: ldr        r1, [r5]
0051953A: str        r1, [r0]
0051953C: mov.w      r0, #0
00519540: str        r0, [r4, #8]
00519542: beq        #0x51955e
00519544: ldrb       r0, [r5, #8]
00519546: ldr        r6, [r5, #4]
00519548: lsls       r0, r0, #0x1f
0051954A: itt        ne
0051954C: ldrne      r0, [r5, #0x10]
0051954E: blxne      #0x4a40cc
00519552: mov        r0, r5
00519554: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00519558: cmp        r6, r4
0051955A: mov        r5, r6
0051955C: bne        #0x519544
0051955E: pop        {r4, r5, r6, pc}
I8 callback copy

RANGE 00528C0C..00528C36 _ZNSt6__ndk18functionIFvvEEC2ERKS2_
00528C0C: push       {r4, lr}
00528C0E: mov        r4, r0
00528C10: ldr        r0, [r1, #0x10]
00528C12: cbz        r0, #0x528c20
00528C14: cmp        r1, r0
00528C16: beq        #0x528c26
00528C18: ldr        r1, [r0]
00528C1A: ldr        r1, [r1, #8]
00528C1C: blx        r1
00528C1E: b          #0x528c22
00528C20: movs       r0, #0
00528C22: str        r0, [r4, #0x10]
00528C24: b          #0x528c32
00528C26: str        r4, [r4, #0x10]
00528C28: ldr        r0, [r1, #0x10]
00528C2A: ldr        r1, [r0]
00528C2C: ldr        r2, [r1, #0xc]
00528C2E: mov        r1, r4
00528C30: blx        r2
00528C32: mov        r0, r4
00528C34: pop        {r4, pc}
I9 callback append

RANGE 00648034..006480AA _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE12emplace_backIJRS3_EEEvDpOT_
00648034: push.w     {r4, r5, r6, r7, r8, lr}
00648038: mov        r4, r0
0064803A: movs       r7, #0xaa
0064803C: ldrd       r0, r2, [r4, #4]
00648040: mov        r8, r1
00648042: ldrd       ip, r3, [r4, #0x10]
00648046: movs       r1, #0
00648048: subs       r5, r2, r0
0064804A: asrs       r6, r5, #2
0064804C: cmp.w      r1, r5, asr #2
00648050: mul        r7, r6, r7
00648054: add.w      r1, r3, ip
00648058: it         ne
0064805A: subne      r6, r7, #1
0064805C: cmp        r6, r1
0064805E: bne        #0x64806e
00648060: mov        r0, r4
00648062: blx        #0x4b9ba0 ; _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE19__add_back_capacityEv -> 006480AA size=22A
00648066: ldrd       r0, r2, [r4, #4]
0064806A: ldrd       ip, r3, [r4, #0x10]
0064806E: cmp        r2, r0
00648070: beq        #0x648098
00648072: movw       r2, #0xc0c1
00648076: add.w      r1, ip, r3
0064807A: movt       r2, #0xc0c0
0064807E: umull      r2, r3, r1, r2
00648082: lsrs       r2, r3, #7
00648084: movs       r3, #0xaa
00648086: mls        r1, r2, r3, r1
0064808A: ldr.w      r0, [r0, r2, lsl #2]
0064808E: add.w      r1, r1, r1, lsl #1
00648092: add.w      r0, r0, r1, lsl #3
00648096: b          #0x64809a
00648098: movs       r0, #0
0064809A: mov        r1, r8
0064809C: blx        #0x4a922c ; _ZNSt6__ndk18functionIFvvEEC2ERKS2_ -> 00528C0C size=2A
006480A0: ldr        r0, [r4, #0x14]
006480A2: adds       r0, #1
006480A4: str        r0, [r4, #0x14]
006480A6: pop.w      {r4, r5, r6, r7, r8, pc}
T19 scoped handle teardown

RANGE 004EF1B8..004EF1D2
004EF1B8: push       {r4, lr}
004EF1BA: mov        r4, r0
004EF1BC: ldr        r0, [r4]
004EF1BE: ldr        r1, [r0]
004EF1C0: ldr        r1, [r1, #8]
004EF1C2: blx        r1
004EF1C4: ldr        r0, [r4]
004EF1C6: cbz        r0, #0x4ef1ce
004EF1C8: ldr        r1, [r0]
004EF1CA: ldr        r1, [r1, #4]
004EF1CC: blx        r1
004EF1CE: mov        r0, r4
004EF1D0: pop        {r4, pc}
T20 logger queue stop

RANGE 00801E70..00801E88
00801E70: push       {r4, lr}
00801E72: mov        r4, r0
00801E74: ldr        r0, [r4]
00801E76: cbz        r0, #0x801e86
00801E78: blx        #0x4af3dc ; _ZN4Anki4Util8Dispatch4StopEPNS1_5QueueE -> 007FB900 size=6
00801E7C: mov        r0, r4
00801E7E: pop.w      {r4, lr}
00801E82: b.w        #0x8d0e2c
00801E86: pop        {r4, pc}
L12 SetLiftHeight sender

RANGE 00640744..006407AE
00640744: push       {r4, r5, r7, lr}
00640746: sub.w      sp, sp, #0x420
0064074A: mov        r4, r0
0064074C: ldr        r0, [pc, #0x74] ; literal[006407C4]=009FE0F6
0064074E: ldr.w      r5, [sp, #0x434]
00640752: add        r0, pc
00640754: ldr.w      ip, [sp, #0x430]
00640758: ldr        r0, [r0]
0064075A: ldr        r0, [r0]
0064075C: str.w      r0, [sp, #0x41c]
00640760: ldr        r1, [r1]
00640762: ldrb       r0, [r5]
00640764: ldr.w      r5, [ip]
00640768: ldr        r3, [r3]
0064076A: ldr        r2, [r2]
0064076C: stm.w      sp, {r1, r2, r3, r5}
00640770: add        r5, sp, #0x14
00640772: mov        r1, sp
00640774: strb.w     r0, [sp, #0x10]
00640778: mov        r0, r5
0064077A: blx        #0x4b96fc ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_13SetLiftHeightE -> 007A8558 size=12
0064077E: mov        r0, r4
00640780: mov        r1, r5
00640782: movs       r2, #1
00640784: movs       r3, #0
00640786: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
0064078A: mov        r4, r0
0064078C: add        r0, sp, #0x14
0064078E: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
00640792: ldr        r0, [pc, #0x34] ; literal[006407C8]=009FE0B0
00640794: ldr.w      r1, [sp, #0x41c]
00640798: add        r0, pc
0064079A: ldr        r0, [r0]
0064079C: ldr        r0, [r0]
0064079E: subs       r0, r0, r1
006407A0: ittt       eq
006407A2: moveq      r0, r4
006407A4: addeq.w    sp, sp, #0x420
006407A8: popeq      {r4, r5, r7, pc}
006407AA: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
D3 enum names

RANGE 007BC250..007BC262 _ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE
007BC250: mov        r1, r0
007BC252: cmp        r1, #0xf
007BC254: bgt        #0x7bc272
007BC256: cmp        r1, #8
007BC258: bhi        #0x7bc2b0
007BC25A: ldr        r0, [pc, #0x58] ; literal[007BC2B4]=00463DE9
007BC25C: add        r0, pc
007BC25E: tbb        [pc, r1] ; literal[007BC260]=0528F001
A1 AbortAll

RANGE 0051194C..0051198E _ZN4Anki5Cozmo5Robot8AbortAllEv
0051194C: push       {r4, r5, r6, r7, lr}
0051194E: sub        sp, #4
00511950: mov        r4, r0
00511952: mov.w      r1, #-1
00511956: ldr.w      r0, [r4, #0x250]
0051195A: blx        #0x4a7abc ; _ZN4Anki5Cozmo10ActionList6CancelENS0_15RobotActionTypeE -> 0053DE10 size=4E
0051195E: ldr        r0, [r4, #0x5c]
00511960: blx        #0x4a7ac8 ; _ZN4Anki5Cozmo13PathComponent5AbortEv -> 00649100 size=120
00511964: mov        r5, r0
00511966: ldr.w      r0, [r4, #0x280]
0051196A: blx        #0x4a7ad4 ; _ZNK4Anki5Cozmo16DockingComponent12AbortDockingEv -> 0063BE10 size=6C
0051196E: mov        r6, r0
00511970: mov        r0, r4
00511972: blx        #0x4a7ae0 ; _ZN4Anki5Cozmo5Robot18SendAbortAnimationEv -> 00517DE4 size=6C
00511976: mov        r7, r0
00511978: ldr.w      r0, [r4, #0x254]
0051197C: blx        #0x4a7aec ; _ZN4Anki5Cozmo17MovementComponent13StopAllMotorsEv -> 0063FBD8 size=282
00511980: orr.w      r0, r6, r5
00511984: orrs       r0, r7
00511986: it         ne
00511988: movne      r0, #1
0051198A: add        sp, #4
0051198C: pop        {r4, r5, r6, r7, pc}
A2 Cancel boundary

RANGE 0053DE10..0053DE5E _ZN4Anki5Cozmo10ActionList6CancelENS0_15RobotActionTypeE
0053DE10: push       {r4, r5, r6, r7, lr}
0053DE12: sub        sp, #4
0053DE14: mov        r5, r0
0053DE16: mov        r4, r1
0053DE18: ldrb       r0, [r5, #0xc]
0053DE1A: cbz        r0, #0x53de20
0053DE1C: movs       r6, #1
0053DE1E: b          #0x53de56
0053DE20: ldr        r0, [r5], #4
0053DE24: movs       r6, #0
0053DE26: cmp        r0, r5
0053DE28: beq        #0x53de56
0053DE2A: mov        r7, r0
0053DE2C: adds       r0, #0x14
0053DE2E: mov        r1, r4
0053DE30: blx        #0x4aa8dc ; _ZN4Anki5Cozmo11ActionQueue6CancelENS0_15RobotActionTypeE -> 0053E690 size=74
0053DE34: ldr        r1, [r7, #4]
0053DE36: orrs       r6, r0
0053DE38: cmp        r1, #0
0053DE3A: beq        #0x53de46
0053DE3C: mov        r0, r1
0053DE3E: ldr        r1, [r0]
0053DE40: cmp        r1, #0
0053DE42: bne        #0x53de3c
0053DE44: b          #0x53de50
0053DE46: ldr        r0, [r7, #8]
0053DE48: ldr        r1, [r0]
0053DE4A: cmp        r1, r7
0053DE4C: mov        r7, r0
0053DE4E: bne        #0x53de46
0053DE50: cmp        r0, r5
0053DE52: mov        r7, r0
0053DE54: bne        #0x53de2c
0053DE56: and        r0, r6, #1
0053DE5A: add        sp, #4
0053DE5C: pop        {r4, r5, r6, r7, pc}
A3 Path Abort boundary

RANGE 00649100..006491BC _ZN4Anki5Cozmo13PathComponent5AbortEv
00649100: push       {r4, r5, r6, lr}
00649102: sub        sp, #0x10
00649104: mov        r4, r0
00649106: movs       r0, #0
00649108: ldr        r1, [pc, #0xd8] ; literal[006491E4]=009E64FC
0064910A: strd       r0, r0, [sp, #4]
0064910E: str        r0, [sp, #0xc]
00649110: add        r1, pc
00649112: ldr        r0, [pc, #0xd4] ; literal[006491E8]=0059C3F6
00649114: ldr        r2, [r4, #0x38]
00649116: add        r0, pc
00649118: ldr.w      r1, [r1, r2, lsl #2]
0064911C: add        r2, sp, #4
0064911E: str        r1, [sp]
00649120: adr        r1, #0xc8 ; ADR[006491EC]=b'PathComponent.Abort'
00649122: adr        r3, #0xdc ; ADR[00649200]=b"Aborting from status '%s'"
00649124: blx        #0x4a505c ; _ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D42C size=14
00649128: ldr        r0, [sp, #4]
0064912A: cbz        r0, #0x64914a
0064912C: ldr        r1, [sp, #8]
0064912E: cmp        r1, r0
00649130: itttt      ne
00649132: subne.w    r2, r1, #8
00649136: subne      r2, r2, r0
00649138: mvnne      r3, #7
0064913C: bicne.w    r2, r3, r2
00649140: itt        ne
00649142: addne      r1, r1, r2
00649144: strne      r1, [sp, #8]
00649146: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0064914A: ldr        r0, [r4, #0x20]
0064914C: cbz        r0, #0x64915a
0064914E: ldr        r1, [r0]
00649150: ldr        r1, [r1, #0x10]
00649152: blx        r1
00649154: movs       r0, #0
00649156: strb.w     r0, [r4, #0x47]
0064915A: ldr        r0, [r4, #0x34]
0064915C: movs       r1, #0
0064915E: strd       r1, r1, [r4, #0x30]
00649162: cbz        r0, #0x649168
00649164: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
00649168: mov        r0, r4
0064916A: blx        #0x4b9ccc ; _ZN4Anki5Cozmo13PathComponent9ClearPathEv -> 00649220 size=A4
0064916E: mov        r5, r0
00649170: ldr        r0, [r4, #0x38]
00649172: cmp        r0, #4
00649174: bhi        #0x64918c
00649176: movs       r1, #1
00649178: lsl.w      r0, r1, r0
0064917C: tst.w      r0, #0x13
00649180: ite        eq
00649182: moveq      r1, #5
00649184: movne      r1, #4
00649186: mov        r0, r4
00649188: blx        #0x4b9cd8 ; _ZN4Anki5Cozmo13PathComponent20SetDriveToPoseStatusENS0_23ERobotDriveToPoseStatusE -> 006492C4 size=B8
0064918C: ldr        r6, [r4, #0x50]
0064918E: movs       r0, #0
00649190: strb.w     r0, [r4, #0x46]
00649194: ldr        r4, [r6]
00649196: b          #0x6491a6
00649198: sub.w      r0, r1, #0xc
0064919C: str        r0, [r6, #4]
0064919E: ldr        r1, [r1, #-0xc]
006491A2: ldr        r1, [r1]
006491A4: blx        r1
006491A6: ldr        r1, [r6, #4]
006491A8: cmp        r1, r4
006491AA: bne        #0x649198
006491AC: ldr        r0, [pc, #0x6c] ; literal[0064921C]=009F57C6
006491AE: add        r0, pc
006491B0: ldr        r0, [r0]
006491B2: ldr        r0, [r0]
006491B4: str        r0, [r6, #0xc]
006491B6: mov        r0, r5
006491B8: add        sp, #0x10
006491BA: pop        {r4, r5, r6, pc}
A4 ClearPath

RANGE 00649220..006492A0 _ZN4Anki5Cozmo13PathComponent9ClearPathEv
00649220: push       {r4, r5, r7, lr}
00649222: sub.w      sp, sp, #0x410
00649226: mov        r4, r0
00649228: ldr        r0, [pc, #0x90] ; literal[006492BC]=009F561E
0064922A: add        r0, pc
0064922C: ldr        r0, [r0]
0064922E: ldr        r0, [r0]
00649230: str.w      r0, [sp, #0x40c]
00649234: ldrh.w     r0, [r4, #0x42]
00649238: cbz        r0, #0x64923e
0064923A: strh.w     r0, [r4, #0x4a]
0064923E: ldr        r0, [r4, #0x54]
00649240: ldr        r2, [r0]
00649242: ldr        r1, [r0, #0x10]
00649244: ldr        r0, [r2, #0x24]
00649246: blx        #0x4a41a4 ; _ZN4Anki5Cozmo10VizManager9ErasePathEj -> 006BFACE size=1A
0064924A: ldr        r0, [r4, #4]
0064924C: cbz        r0, #0x649252
0064924E: blx        #0x4b9ce4 ; _ZN4Anki5Cozmo14PathDolerOuter9ClearPathEv -> 005082B0 size=18
00649252: movs       r0, #0xff
00649254: strb.w     r0, [r4, #0x40]
00649258: blx        #0x4a4f6c ; _ZN4Anki16BaseStationTimer11getInstanceEv -> 0084BB9C size=48
0064925C: blx        #0x4a50b0 ; _ZNK4Anki16BaseStationTimer23GetCurrentTimeInSecondsEv -> 0084BCA8 size=4
00649260: ldr        r5, [r4, #0x54]
00649262: mov        r1, sp
00649264: str        r0, [r4, #0x3c]
00649266: add        r4, sp, #4
00649268: movs       r0, #0
0064926A: strh.w     r0, [sp]
0064926E: mov        r0, r4
00649270: blx        #0x4b9cf0 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_9ClearPathE -> 007A891C size=A
00649274: mov        r0, r5
00649276: mov        r1, r4
00649278: movs       r2, #1
0064927A: movs       r3, #0
0064927C: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
00649280: mov        r4, r0
00649282: add        r0, sp, #4
00649284: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
00649288: ldr        r0, [pc, #0x34] ; literal[006492C0]=009F55BA
0064928A: ldr.w      r1, [sp, #0x40c]
0064928E: add        r0, pc
00649290: ldr        r0, [r0]
00649292: ldr        r0, [r0]
00649294: subs       r0, r0, r1
00649296: ittt       eq
00649298: moveq      r0, r4
0064929A: addeq.w    sp, sp, #0x410
0064929E: popeq      {r4, r5, r7, pc}
A5 Dock abort

RANGE 0063BE10..0063BE5E _ZNK4Anki5Cozmo16DockingComponent12AbortDockingEv
0063BE10: push       {r4, r5, r7, lr}
0063BE12: sub.w      sp, sp, #0x410
0063BE16: ldr        r1, [pc, #0x5c] ; literal[0063BE74]=00A02A2E
0063BE18: add        r5, sp, #4
0063BE1A: add        r1, pc
0063BE1C: ldr        r1, [r1]
0063BE1E: ldr        r1, [r1]
0063BE20: str.w      r1, [sp, #0x40c]
0063BE24: mov        r1, sp
0063BE26: ldr        r4, [r0]
0063BE28: mov        r0, r5
0063BE2A: blx        #0x4b94c8 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS0_12AbortDockingE -> 007A8DF4 size=6
0063BE2E: mov        r0, r4
0063BE30: mov        r1, r5
0063BE32: movs       r2, #1
0063BE34: movs       r3, #0
0063BE36: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
0063BE3A: mov        r4, r0
0063BE3C: add        r0, sp, #4
0063BE3E: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
0063BE42: ldr        r0, [pc, #0x34] ; literal[0063BE78]=00A02A00
0063BE44: ldr.w      r1, [sp, #0x40c]
0063BE48: add        r0, pc
0063BE4A: ldr        r0, [r0]
0063BE4C: ldr        r0, [r0]
0063BE4E: subs       r0, r0, r1
0063BE50: ittt       eq
0063BE52: moveq      r0, r4
0063BE54: addeq.w    sp, sp, #0x410
0063BE58: popeq      {r4, r5, r7, pc}
0063BE5A: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
A6 animation abort

RANGE 00517DE4..00517E32 _ZN4Anki5Cozmo5Robot18SendAbortAnimationEv
00517DE4: push       {r4, r5, r7, lr}
00517DE6: sub.w      sp, sp, #0x410
00517DEA: mov        r4, r0
00517DEC: ldr        r0, [pc, #0x58] ; literal[00517E48]=00B26A56
00517DEE: add        r5, sp, #4
00517DF0: mov        r1, sp
00517DF2: add        r0, pc
00517DF4: ldr        r0, [r0]
00517DF6: ldr        r0, [r0]
00517DF8: str.w      r0, [sp, #0x40c]
00517DFC: mov        r0, r5
00517DFE: blx        #0x4a8158 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_14AbortAnimationE -> 007AA392 size=6
00517E02: mov        r0, r4
00517E04: mov        r1, r5
00517E06: movs       r2, #1
00517E08: movs       r3, #0
00517E0A: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
00517E0E: mov        r4, r0
00517E10: add        r0, sp, #4
00517E12: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
00517E16: ldr        r0, [pc, #0x34] ; literal[00517E4C]=00B26A2C
00517E18: ldr.w      r1, [sp, #0x40c]
00517E1C: add        r0, pc
00517E1E: ldr        r0, [r0]
00517E20: ldr        r0, [r0]
00517E22: subs       r0, r0, r1
00517E24: ittt       eq
00517E26: moveq      r0, r4
00517E28: addeq.w    sp, sp, #0x410
00517E2C: popeq      {r4, r5, r7, pc}
00517E2E: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
A7 StopAllMotors

RANGE 0063FBD8..0063FE1A _ZN4Anki5Cozmo17MovementComponent13StopAllMotorsEv
0063FBD8: push.w     {r4, r5, r6, r7, r8, sb, lr}
0063FBDC: sub        sp, #0x24
0063FBDE: mov        r4, r0
0063FBE0: mov        sb, r4
0063FBE2: ldrb       r0, [sb, #0xb8]!
0063FBE6: cbnz       r0, #0x63fbf8
0063FBE8: ldrb.w     r0, [r4, #0xb9]
0063FBEC: cbnz       r0, #0x63fbf8
0063FBEE: ldrb.w     r0, [r4, #0xba]
0063FBF2: cmp        r0, #0
0063FBF4: beq.w      #0x63fe0e
0063FBF8: ldrb.w     r0, [r4, #0xd4]
0063FBFC: cmp        r0, #0
0063FBFE: bne.w      #0x63fe0e
0063FC02: ldr.w      r7, [r4, #0xc0]
0063FC06: movs       r5, #0
0063FC08: str        r5, [sp, #0x20]
0063FC0A: strd       r5, r5, [sp, #0x18]
0063FC0E: mov        r0, r7
0063FC10: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FC14: add.w      r8, sp, #0x18
0063FC18: mov        r2, r0
0063FC1A: mov        r1, r7
0063FC1C: mov        r0, r8
0063FC1E: bl         #0x4e02b2
0063FC22: ldr.w      r6, [r4, #0xc0]
0063FC26: str        r5, [sp, #0x10]
0063FC28: strd       r5, r5, [sp, #8]
0063FC2C: mov        r0, r6
0063FC2E: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FC32: mov        r2, r0
0063FC34: add        r7, sp, #8
0063FC36: mov        r1, r6
0063FC38: mov        r0, r7
0063FC3A: bl         #0x4e02b2
0063FC3E: add.w      r2, r4, #0xb9
0063FC42: mov        r0, r4
0063FC44: movs       r1, #0
0063FC46: movs       r3, #1
0063FC48: strd       r8, r7, [sp]
0063FC4C: blx        #0x4b9654 ; _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_ -> 0063EFB0 size=1C4
0063FC50: ldrb.w     r0, [sp, #8]
0063FC54: lsls       r0, r0, #0x1f
0063FC56: itt        ne
0063FC58: ldrne      r0, [sp, #0x10]
0063FC5A: blxne      #0x4a40cc
0063FC5E: ldrb.w     r0, [sp, #0x18]
0063FC62: lsls       r0, r0, #0x1f
0063FC64: itt        ne
0063FC66: ldrne      r0, [sp, #0x20]
0063FC68: blxne      #0x4a40cc
0063FC6C: ldr.w      r7, [r4, #0xc4]
0063FC70: movs       r5, #0
0063FC72: str        r5, [sp, #0x20]
0063FC74: strd       r5, r5, [sp, #0x18]
0063FC78: mov        r0, r7
0063FC7A: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FC7E: add.w      r8, sp, #0x18
0063FC82: mov        r2, r0
0063FC84: mov        r1, r7
0063FC86: mov        r0, r8
0063FC88: bl         #0x4e02b2
0063FC8C: ldr.w      r6, [r4, #0xc4]
0063FC90: str        r5, [sp, #0x10]
0063FC92: strd       r5, r5, [sp, #8]
0063FC96: mov        r0, r6
0063FC98: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FC9C: mov        r2, r0
0063FC9E: add        r7, sp, #8
0063FCA0: mov        r1, r6
0063FCA2: mov        r0, r7
0063FCA4: bl         #0x4e02b2
0063FCA8: add.w      r2, r4, #0xba
0063FCAC: mov        r0, r4
0063FCAE: movs       r1, #0
0063FCB0: movs       r3, #2
0063FCB2: strd       r8, r7, [sp]
0063FCB6: blx        #0x4b9654 ; _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_ -> 0063EFB0 size=1C4
0063FCBA: ldrb.w     r0, [sp, #8]
0063FCBE: lsls       r0, r0, #0x1f
0063FCC0: itt        ne
0063FCC2: ldrne      r0, [sp, #0x10]
0063FCC4: blxne      #0x4a40cc
0063FCC8: ldrb.w     r0, [sp, #0x18]
0063FCCC: lsls       r0, r0, #0x1f
0063FCCE: itt        ne
0063FCD0: ldrne      r0, [sp, #0x20]
0063FCD2: blxne      #0x4a40cc
0063FCD6: ldr.w      r7, [r4, #0xbc]
0063FCDA: movs       r5, #0
0063FCDC: str        r5, [sp, #0x20]
0063FCDE: strd       r5, r5, [sp, #0x18]
0063FCE2: mov        r0, r7
0063FCE4: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FCE8: add.w      r8, sp, #0x18
0063FCEC: mov        r2, r0
0063FCEE: mov        r1, r7
0063FCF0: mov        r0, r8
0063FCF2: bl         #0x4e02b2
0063FCF6: ldr.w      r6, [r4, #0xbc]
0063FCFA: str        r5, [sp, #0x10]
0063FCFC: strd       r5, r5, [sp, #8]
0063FD00: mov        r0, r6
0063FD02: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FD06: mov        r2, r0
0063FD08: add        r7, sp, #8
0063FD0A: mov        r1, r6
0063FD0C: mov        r0, r7
0063FD0E: bl         #0x4e02b2
0063FD12: mov        r0, r4
0063FD14: movs       r1, #0
0063FD16: mov        r2, sb
0063FD18: movs       r3, #4
0063FD1A: strd       r8, r7, [sp]
0063FD1E: blx        #0x4b9654 ; _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_ -> 0063EFB0 size=1C4
0063FD22: ldrb.w     r0, [sp, #8]
0063FD26: lsls       r0, r0, #0x1f
0063FD28: itt        ne
0063FD2A: ldrne      r0, [sp, #0x10]
0063FD2C: blxne      #0x4a40cc
0063FD30: ldrb.w     r0, [sp, #0x18]
0063FD34: lsls       r0, r0, #0x1f
0063FD36: itt        ne
0063FD38: ldrne      r0, [sp, #0x20]
0063FD3A: blxne      #0x4a40cc
0063FD3E: ldr.w      r7, [r4, #0xbc]
0063FD42: movs       r5, #0
0063FD44: str        r5, [sp, #0x20]
0063FD46: strd       r5, r5, [sp, #0x18]
0063FD4A: mov        r0, r7
0063FD4C: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FD50: add.w      r8, sp, #0x18
0063FD54: mov        r2, r0
0063FD56: mov        r1, r7
0063FD58: mov        r0, r8
0063FD5A: bl         #0x4e02b2
0063FD5E: ldr.w      r6, [r4, #0xc8]
0063FD62: str        r5, [sp, #0x10]
0063FD64: strd       r5, r5, [sp, #8]
0063FD68: mov        r0, r6
0063FD6A: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FD6E: mov        r2, r0
0063FD70: add        r7, sp, #8
0063FD72: mov        r1, r6
0063FD74: mov        r0, r7
0063FD76: bl         #0x4e02b2
0063FD7A: mov        r0, r4
0063FD7C: movs       r1, #0
0063FD7E: mov        r2, sb
0063FD80: movs       r3, #4
0063FD82: strd       r8, r7, [sp]
0063FD86: blx        #0x4b9654 ; _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_ -> 0063EFB0 size=1C4
0063FD8A: ldrb.w     r0, [sp, #8]
0063FD8E: lsls       r0, r0, #0x1f
0063FD90: itt        ne
0063FD92: ldrne      r0, [sp, #0x10]
0063FD94: blxne      #0x4a40cc
0063FD98: ldrb.w     r0, [sp, #0x18]
0063FD9C: lsls       r0, r0, #0x1f
0063FD9E: itt        ne
0063FDA0: ldrne      r0, [sp, #0x20]
0063FDA2: blxne      #0x4a40cc
0063FDA6: ldr.w      r7, [r4, #0xbc]
0063FDAA: movs       r5, #0
0063FDAC: str        r5, [sp, #0x20]
0063FDAE: strd       r5, r5, [sp, #0x18]
0063FDB2: mov        r0, r7
0063FDB4: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FDB8: add.w      r8, sp, #0x18
0063FDBC: mov        r2, r0
0063FDBE: mov        r1, r7
0063FDC0: mov        r0, r8
0063FDC2: bl         #0x4e02b2
0063FDC6: ldr.w      r6, [r4, #0xcc]
0063FDCA: str        r5, [sp, #0x10]
0063FDCC: strd       r5, r5, [sp, #8]
0063FDD0: mov        r0, r6
0063FDD2: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FDD6: mov        r2, r0
0063FDD8: add        r7, sp, #8
0063FDDA: mov        r1, r6
0063FDDC: mov        r0, r7
0063FDDE: bl         #0x4e02b2
0063FDE2: mov        r0, r4
0063FDE4: movs       r1, #0
0063FDE6: mov        r2, sb
0063FDE8: movs       r3, #4
0063FDEA: strd       r8, r7, [sp]
0063FDEE: blx        #0x4b9654 ; _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_ -> 0063EFB0 size=1C4
0063FDF2: ldrb.w     r0, [sp, #8]
0063FDF6: lsls       r0, r0, #0x1f
0063FDF8: itt        ne
0063FDFA: ldrne      r0, [sp, #0x10]
0063FDFC: blxne      #0x4a40cc
0063FE00: ldrb.w     r0, [sp, #0x18]
0063FE04: lsls       r0, r0, #0x1f
0063FE06: itt        ne
0063FE08: ldrne      r0, [sp, #0x20]
0063FE0A: blxne      #0x4a40cc
0063FE0E: ldr        r0, [r4, #4]
0063FE10: bl         #0x64099c
0063FE14: add        sp, #0x24
0063FE16: pop.w      {r4, r5, r6, r7, r8, sb, pc}
A8 stop sender

RANGE 0064099C..006409EA
0064099C: push       {r4, r5, r7, lr}
0064099E: sub.w      sp, sp, #0x410
006409A2: mov        r4, r0
006409A4: ldr        r0, [pc, #0x58] ; literal[00640A00]=009FDE9E
006409A6: add        r5, sp, #4
006409A8: mov        r1, sp
006409AA: add        r0, pc
006409AC: ldr        r0, [r0]
006409AE: ldr        r0, [r0]
006409B0: str.w      r0, [sp, #0x40c]
006409B4: mov        r0, r5
006409B6: blx        #0x4b9720 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_13StopAllMotorsE -> 007A88A8 size=6
006409BA: mov        r0, r4
006409BC: mov        r1, r5
006409BE: movs       r2, #1
006409C0: movs       r3, #0
006409C2: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
006409C6: mov        r4, r0
006409C8: add        r0, sp, #4
006409CA: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
006409CE: ldr        r0, [pc, #0x34] ; literal[00640A04]=009FDE74
006409D0: ldr.w      r1, [sp, #0x40c]
006409D4: add        r0, pc
006409D6: ldr        r0, [r0]
006409D8: ldr        r0, [r0]
006409DA: subs       r0, r0, r1
006409DC: ittt       eq
006409DE: moveq      r0, r4
006409E0: addeq.w    sp, sp, #0x410
006409E4: popeq      {r4, r5, r7, pc}
006409E6: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
A9 direct zero unlock

RANGE 0063EFB0..0063F0BC _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_
0063EFB0: push.w     {r4, r5, r6, r7, r8, lr}
0063EFB4: sub        sp, #0x28
0063EFB6: vmov       s0, r1
0063EFBA: mov        r4, r3
0063EFBC: mov        r7, r0
0063EFBE: vcmpe.f32  s0, #0
0063EFC2: vmrs       apsr_nzcv, fpscr
0063EFC6: vneg.f32   s2, s0
0063EFCA: it         mi
0063EFCC: vmovmi.f32 s0, s2
0063EFD0: vldr       s2, [pc, #0x124] ; literal[0063F0F8]=3727C5AC
0063EFD4: ldrd       r6, r5, [sp, #0x40]
0063EFD8: vcmpe.f32  s0, s2
0063EFDC: vmrs       apsr_nzcv, fpscr
0063EFE0: bpl        #0x63f096
0063EFE2: movs       r0, #0
0063EFE4: mov        r1, r4
0063EFE6: strb       r0, [r2]
0063EFE8: mov        r0, r7
0063EFEA: blx        #0x4b9660 ; _ZNK4Anki5Cozmo17MovementComponent18AreAllTracksLockedEh -> 0063EB40 size=B4
0063EFEE: cmp        r0, #1
0063EFF0: bne        #0x63f0a4
0063EFF2: mov        r0, r7
0063EFF4: mov        r1, r4
0063EFF6: mov        r2, r6
0063EFF8: blx        #0x4a5794 ; _ZN4Anki5Cozmo17MovementComponent12UnlockTracksEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE -> 0063FE5C size=23C
0063EFFC: cmp        r0, #1
0063EFFE: bne        #0x63f0a4
0063F000: movs       r0, #0
0063F002: strd       r0, r0, [sp, #0x1c]
0063F006: str        r0, [sp, #0x24]
0063F008: add.w      r8, sp, #0x10
0063F00C: mov        r1, r4
0063F00E: mov        r0, r8
0063F010: blx        #0x4aaa98 ; _ZN4Anki5Cozmo16AnimTrackHelpers22AnimTrackFlagsToStringEh -> 006305E8 size=25C
0063F014: ldrb       r7, [r6]
0063F016: ldr        r1, [r6, #8]
0063F018: ldrb       r0, [r5]
0063F01A: tst.w      r7, #1
0063F01E: ldr        r3, [sp, #0x18]
0063F020: ldrb.w     ip, [sp, #0x10]
0063F024: ldr        r2, [r5, #8]
0063F026: it         eq
0063F028: addeq      r1, r6, #1
0063F02A: tst.w      r0, #1
0063F02E: it         eq
0063F030: addeq      r2, r5, #1
0063F032: strd       r4, r2, [sp]
0063F036: adr        r0, #0xc4 ; ADR[0063F0FC]=b'MovementComponent.DirectDriveCheckSpeedAndLockTracks'
0063F038: str        r1, [sp, #8]
0063F03A: add        r1, sp, #0x1c
0063F03C: adr        r2, #0xf4 ; ADR[0063F134]=b'Locks left on tracks %s [0x%x] after %s[%s] unlocked'
0063F03E: tst.w      ip, #1
0063F042: it         eq
0063F044: orreq      r3, r8, #1
0063F048: blx        #0x4a4108 ; _ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D13C size=80
0063F04C: ldrb.w     r0, [sp, #0x10]
0063F050: lsls       r0, r0, #0x1f
0063F052: itt        ne
0063F054: ldrne      r0, [sp, #0x18]
0063F056: blxne      #0x4a40cc
0063F05A: ldr        r0, [sp, #0x1c]
0063F05C: cbz        r0, #0x63f07c
0063F05E: ldr        r1, [sp, #0x20]
0063F060: cmp        r1, r0
0063F062: itttt      ne
0063F064: subne.w    r2, r1, #8
0063F068: subne      r2, r2, r0
0063F06A: mvnne      r3, #7
0063F06E: bicne.w    r2, r3, r2
0063F072: itt        ne
0063F074: addne      r1, r1, r2
0063F076: strne      r1, [sp, #0x20]
0063F078: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0063F07C: ldr        r0, [pc, #0xec] ; literal[0063F16C]=009FF706
0063F07E: movs       r2, #1
0063F080: ldr        r1, [pc, #0xec] ; literal[0063F170]=009FF708
0063F082: add        r0, pc
0063F084: add        r1, pc
0063F086: ldr        r0, [r0]
0063F088: ldr        r1, [r1]
0063F08A: ldrb       r0, [r0]
0063F08C: strb       r2, [r1]
0063F08E: cbz        r0, #0x63f0a4
0063F090: blx        #0x4a4114 ; _ZN4Anki4Util18sDebugBreakOnErrorEv -> 0080DAB4 size=2
0063F094: b          #0x63f0a4
0063F096: movs       r0, #1
0063F098: mov        r1, r4
0063F09A: strb       r0, [r2]
0063F09C: mov        r0, r7
0063F09E: blx        #0x4b9660 ; _ZNK4Anki5Cozmo17MovementComponent18AreAllTracksLockedEh -> 0063EB40 size=B4
0063F0A2: cbz        r0, #0x63f0aa
0063F0A4: add        sp, #0x28
0063F0A6: pop.w      {r4, r5, r6, r7, r8, pc}
0063F0AA: mov        r0, r7
0063F0AC: mov        r1, r4
0063F0AE: mov        r2, r6
0063F0B0: mov        r3, r5
0063F0B2: add        sp, #0x28
0063F0B4: pop.w      {r4, r5, r6, r7, r8, lr}
0063F0B8: b.w        #0x8ccdbc
A10 UnlockTracks

RANGE 0063FE5C..0063FFDC _ZN4Anki5Cozmo17MovementComponent12UnlockTracksEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE
0063FE5C: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
0063FE60: sub.w      sp, sp, #0x428
0063FE64: sub        sp, #4
0063FE66: mov        r4, r2
0063FE68: ldr        r2, [pc, #0x1d0] ; literal[0064003C]=009FE9D6
0063FE6A: add.w      r8, r0, #0x30
0063FE6E: eor        sl, r1, #0xff
0063FE72: add        r2, pc
0063FE74: mov.w      r7, #-1
0063FE78: movs       r5, #1
0063FE7A: movs       r6, #0
0063FE7C: ldr        r2, [r2]
0063FE7E: ldr        r2, [r2]
0063FE80: str.w      r2, [sp, #0x428]
0063FE84: str        r0, [sp, #0x10]
0063FE86: add        r0, sp, #0x20
0063FE88: adds       r0, #0xc
0063FE8A: str        r0, [sp, #0x18]
0063FE8C: movs       r0, #0
0063FE8E: str        r1, [sp, #0xc]
0063FE90: str        r0, [sp, #0x14]
0063FE92: adds       r7, #1
0063FE94: lsl.w      sb, r5, r7
0063FE98: tst.w      sb, sl
0063FE9C: bne        #0x63ff8a
0063FE9E: movs       r0, #0
0063FEA0: str        r0, [sp, #0x28]
0063FEA2: strd       r0, r0, [sp, #0x20]
0063FEA6: ldrb       r0, [r4]
0063FEA8: lsls       r0, r0, #0x1f
0063FEAA: bne        #0x63feb8
0063FEAC: mov        r0, r4
0063FEAE: add        r1, sp, #0x20
0063FEB0: ldm.w      r0, {r2, r3, r5}
0063FEB4: stm        r1!, {r2, r3, r5}
0063FEB6: b          #0x63fec2
0063FEB8: ldrd       r2, r1, [r4, #4]
0063FEBC: add        r0, sp, #0x20
0063FEBE: bl         #0x4e02b2
0063FEC2: ldr        r0, [sp, #0x18]
0063FEC4: movs       r1, #0
0063FEC6: strd       r1, r1, [r0]
0063FECA: str        r1, [r0, #8]
0063FECC: ldr        r1, [pc, #0x1c0] ; literal[00640090]=005A402C
0063FECE: movs       r2, #0
0063FED0: add        r1, pc
0063FED2: bl         #0x4e02b2
0063FED6: sub.w      fp, r8, #8
0063FEDA: mov        r5, sl
0063FEDC: add        r1, sp, #0x20
0063FEDE: mov        r0, fp
0063FEE0: blx        #0x4b969c ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE4findIS4_EENS_15__tree_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEERKT_ -> 00642508 size=72
0063FEE4: mov        sl, r0
0063FEE6: ldrb.w     r0, [sp, #0x2c]
0063FEEA: lsls       r0, r0, #0x1f
0063FEEC: itt        ne
0063FEEE: ldrne      r0, [sp, #0x34]
0063FEF0: blxne      #0x4a40cc
0063FEF4: ldrb.w     r0, [sp, #0x20]
0063FEF8: lsls       r0, r0, #0x1f
0063FEFA: itt        ne
0063FEFC: ldrne      r0, [sp, #0x28]
0063FEFE: blxne      #0x4a40cc
0063FF02: sub.w      r0, r8, #4
0063FF06: cmp        r0, sl
0063FF08: beq        #0x63ff26
0063FF0A: mov        r0, fp
0063FF0C: mov        r1, sl
0063FF0E: blx        #0x4b96a8 ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE5eraseENS_21__tree_const_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEE -> 006425E4 size=58
0063FF12: ldr.w      r0, [r8]
0063FF16: mov        sl, r5
0063FF18: movs       r5, #1
0063FF1A: cmp        r0, #0
0063FF1C: mov        r1, r0
0063FF1E: it         ne
0063FF20: movne      r1, #1
0063FF22: orrs       r6, r1
0063FF24: b          #0x63ff7e
0063FF26: movs       r0, #0
0063FF28: strd       r0, r0, [sp, #0x20]
0063FF2C: str        r0, [sp, #0x28]
0063FF2E: ldrb       r1, [r4]
0063FF30: ldr        r0, [r4, #8]
0063FF32: tst.w      r1, #1
0063FF36: it         eq
0063FF38: addeq      r0, r4, #1
0063FF3A: ldr        r1, [sp, #0xc]
0063FF3C: add        r2, sp, #0x20
0063FF3E: strd       r1, r0, [sp]
0063FF42: adr        r1, #0xfc ; ADR[00640040]=b'MovementComponent.UnlockTracks'
0063FF44: ldr        r0, [pc, #0x14c] ; literal[00640094]=005A40A0
0063FF46: adr        r3, #0x118 ; ADR[00640060]=b'Tracks 0x%x are not currently locked by %s'
0063FF48: add        r0, pc
0063FF4A: blx        #0x4a505c ; _ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D42C size=14
0063FF4E: ldr        r0, [sp, #0x20]
0063FF50: mov        sl, r5
0063FF52: movs       r5, #1
0063FF54: cbz        r0, #0x63ff74
0063FF56: ldr        r1, [sp, #0x24]
0063FF58: cmp        r1, r0
0063FF5A: itttt      ne
0063FF5C: subne.w    r2, r1, #8
0063FF60: subne      r2, r2, r0
0063FF62: mvnne      r3, #7
0063FF66: bicne.w    r2, r3, r2
0063FF6A: itt        ne
0063FF6C: addne      r1, r1, r2
0063FF6E: strne      r1, [sp, #0x24]
0063FF70: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0063FF74: ldr        r0, [sp, #0x10]
0063FF76: blx        #0x4b96b4 ; _ZNK4Anki5Cozmo17MovementComponent14PrintLockStateEv -> 006410D8 size=428
0063FF7A: ldr.w      r0, [r8]
0063FF7E: cbnz       r0, #0x63ff8a
0063FF80: ldr        r0, [sp, #0x14]
0063FF82: uxtb       r0, r0
0063FF84: orr.w      r0, r0, sb
0063FF88: str        r0, [sp, #0x14]
0063FF8A: add.w      r8, r8, #0xc
0063FF8E: cmp        r7, #7
0063FF90: blt.w      #0x63fe92
0063FF94: ldr        r1, [sp, #0x14]
0063FF96: lsls       r0, r1, #0x18
0063FF98: beq        #0x63ffbe
0063FF9A: ldr        r0, [sp, #0x10]
0063FF9C: add        r5, sp, #0x20
0063FF9E: ldr        r4, [r0, #4]
0063FFA0: mov        r0, r5
0063FFA2: strb.w     r1, [sp, #0x1c]
0063FFA6: add        r1, sp, #0x1c
0063FFA8: blx        #0x4b96c0 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS0_12AnimKeyFrame16EnableAnimTracksE -> 007AAC24 size=A
0063FFAC: mov        r0, r4
0063FFAE: mov        r1, r5
0063FFB0: movs       r2, #1
0063FFB2: movs       r3, #0
0063FFB4: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
0063FFB8: add        r0, sp, #0x20
0063FFBA: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
0063FFBE: ldr        r0, [pc, #0xcc] ; literal[0064008C]=009FE884
0063FFC0: ldr.w      r1, [sp, #0x428]
0063FFC4: add        r0, pc
0063FFC6: ldr        r0, [r0]
0063FFC8: ldr        r0, [r0]
0063FFCA: subs       r0, r0, r1
0063FFCC: itttt      eq
0063FFCE: andeq      r0, r6, #1
0063FFD2: addeq.w    sp, sp, #0x428
0063FFD6: addeq      sp, #4
0063FFD8: popeq.w    {r4, r5, r6, r7, r8, sb, sl, fp, pc}
T1 Tap delete

RANGE 005111EA..00511224
005111EA: ldr.w      r5, [r4, #0x450]
005111EE: str.w      r6, [r4, #0x448]
005111F2: cbz        r5, #0x51121a
005111F4: add.w      r0, r5, #0x2c
005111F8: blx        #0x4a7a98 ; _ZNSt6__ndk110__list_impIN4Anki5Cozmo12ObjectTappedENS_9allocatorIS3_EEE5clearEv -> 005196B4 size=30
005111FC: ldr        r1, [r5, #0x24]
005111FE: add.w      r0, r5, #0x20
00511202: blx        #0x4a7aa4 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo23BlockTapFilterComponent13DoubleTapInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 005196E4 size=24
00511206: ldr        r0, [r5, #0x14]
00511208: cbz        r0, #0x51120e
0051120A: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
0051120E: adds       r0, r5, #4
00511210: blx        #0x4a4e4c ; _ZNSt6__ndk113__vector_baseINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEED2Ev -> 004EAE94 size=34
00511214: mov        r0, r5
00511216: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0051121A: ldr.w      r5, [r4, #0x44c]
0051121E: movs       r6, #0
00511220: str.w      r6, [r4, #0x450]
T2 Touch/Cliff delete

RANGE 005113D4..005113FA
005113D4: ldr.w      r0, [r4, #0x28c]
005113D8: movs       r5, #0
005113DA: str.w      r5, [r4, #0x28c]
005113DE: cbz        r0, #0x5113e8
005113E0: blx        #0x4a78e8 ; _ZN4Anki5Cozmo20TouchSensorComponentD1Ev -> 0064E746 size=16
005113E4: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005113E8: ldr.w      r0, [r4, #0x288]
005113EC: str.w      r5, [r4, #0x288]
005113F0: cbz        r0, #0x5113fa
005113F2: blx        #0x4a78f4 ; _ZN4Anki5Cozmo20CliffSensorComponentD1Ev -> 00633FF8 size=1E
005113F6: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
T3 CubeAccel/backpack/cube delete

RANGE 00511428..0051148C
00511428: ldr.w      r0, [r4, #0x278]
0051142C: str.w      r5, [r4, #0x278]
00511430: cbz        r0, #0x511438
00511432: ldr        r1, [r0]
00511434: ldr        r1, [r1, #4]
00511436: blx        r1
00511438: ldr.w      r5, [r4, #0x274]
0051143C: movs       r6, #0
0051143E: str.w      r6, [r4, #0x274]
00511442: cbz        r5, #0x51146a
00511444: ldr        r0, [r5, #0x30]
00511446: cbz        r0, #0x51144c
00511448: blx        #0x4a5560 ; _ZNSt6__ndk119__shared_weak_count14__release_weakEv IMPORT (resolve packaged dependencies before calling external)
0051144C: ldr        r0, [r5, #0x20]
0051144E: cbz        r0, #0x511454
00511450: blx        #0x4a5560 ; _ZNSt6__ndk119__shared_weak_count14__release_weakEv IMPORT (resolve packaged dependencies before calling external)
00511454: ldr        r1, [r5, #0x14]
00511456: add.w      r0, r5, #0x10
0051145A: blx        #0x4a7900 ; _ZNSt6__ndk16__treeINS_12__value_typeIiNS_4listINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS7_EEEEEENS_19__map_value_compareIiSB_NS_4lessIiEELb1EEENS8_ISB_EEE7destroyEPNS_11__tree_nodeISB_PvEE -> 005195B6 size=2C
0051145E: adds       r0, r5, #4
00511460: blx        #0x4a790c ; _ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv -> 005194A8 size=3A
00511464: mov        r0, r5
00511466: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0051146A: ldr.w      r5, [r4, #0x270]
0051146E: str.w      r6, [r4, #0x270]
00511472: cbz        r5, #0x51148c
00511474: add.w      r0, r5, #0x14
00511478: blx        #0x4a790c ; _ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv -> 005194A8 size=3A
0051147C: ldr        r1, [r5, #0xc]
0051147E: add.w      r0, r5, #8
00511482: blx        #0x4a7918 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeLightComponent10ObjectInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 005194E2 size=40
00511486: mov        r0, r5
00511488: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
T4 Movement delete

RANGE 00511500..00511512
00511500: ldr.w      r0, [r4, #0x254]
00511504: movs       r5, #0
00511506: str.w      r5, [r4, #0x254]
0051150A: cbz        r0, #0x511512
0051150C: ldr        r1, [r0]
0051150E: ldr        r1, [r1, #4]
00511510: blx        r1
T5 Touch dtor

RANGE 0064E746..0064E75C _ZN4Anki5Cozmo20TouchSensorComponentD2Ev
0064E746: push       {r4, lr}
0064E748: mov        r4, r0
0064E74A: movs       r1, #0
0064E74C: ldr        r0, [r4, #0xc]
0064E74E: str        r1, [r4, #0xc]
0064E750: cbz        r0, #0x64e758
0064E752: ldr        r1, [r0]
0064E754: ldr        r1, [r1, #4]
0064E756: blx        r1
0064E758: mov        r0, r4
0064E75A: pop        {r4, pc}
T6 Cliff dtor

RANGE 00633FF8..00634016 _ZN4Anki5Cozmo20CliffSensorComponentD2Ev
00633FF8: push       {r4, lr}
00633FFA: mov        r4, r0
00633FFC: movs       r1, #0
00633FFE: ldr        r0, [r4, #0x44]
00634000: str        r1, [r4, #0x44]
00634002: cbz        r0, #0x63400a
00634004: ldr        r1, [r0]
00634006: ldr        r1, [r1, #4]
00634008: blx        r1
0063400A: add.w      r0, r4, #0x20
0063400E: blx        #0x4b8f28 ; _ZNSt6__ndk112__deque_baseItNS_9allocatorItEEED2Ev -> 00633FD2 size=26
00634012: mov        r0, r4
00634014: pop        {r4, pc}
T7 CubeAccel deleting dtor

RANGE 00635444..0063546E _ZN4Anki5Cozmo18CubeAccelComponentD0Ev
00635444: push       {r4, lr}
00635446: mov        r4, r0
00635448: ldr        r0, [pc, #0x24] ; literal[00635470]=00A0A3EA
0063544A: add        r0, pc
0063544C: ldr        r0, [r0]
0063544E: adds       r0, #8
00635450: str        r0, [r4]
00635452: add.w      r0, r4, #0x14
00635456: blx        #0x4a790c ; _ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv -> 005194A8 size=3A
0063545A: ldr        r1, [r4, #0xc]
0063545C: add.w      r0, r4, #8
00635460: blx        #0x4b8fc4 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeAccelComponent12AccelHistoryEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 00635D84 size=38
00635464: mov        r0, r4
00635466: pop.w      {r4, lr}
0063546A: b.w        #0x8ca88c
T8 Movement dtor

RANGE 00641CFC..00641D38
00641CFC: push       {r4, r5, r7, lr}
00641CFE: mov        r4, r0
00641D00: ldr        r0, [pc, #0x34] ; literal[00641D38]=009FDB56
00641D02: ldr.w      r1, [r4, #0x8c]
00641D06: add        r0, pc
00641D08: ldr        r0, [r0]
00641D0A: adds       r0, #8
00641D0C: str        r0, [r4]
00641D0E: add.w      r0, r4, #0x88
00641D12: blx        #0x4b95f4 ; _ZNSt6__ndk16__treeINS_12__value_typeIhN4Anki5Cozmo17MovementComponent17FaceLayerToRemoveEEENS_19__map_value_compareIhS6_NS_4lessIhEELb1EEENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE -> 00641D4A size=24
00641D16: movs       r5, #0
00641D18: adds       r0, r4, r5
00641D1A: ldr.w      r1, [r0, #0x80]
00641D1E: adds       r0, #0x7c
00641D20: blx        #0x4b9600 ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE -> 00641D6E size=3C
00641D24: subs       r5, #0xc
00641D26: adds.w     r0, r5, #0x60
00641D2A: bne        #0x641d18
00641D2C: add.w      r0, r4, #0x10
00641D30: blx        #0x4a790c ; _ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv -> 005194A8 size=3A
00641D34: mov        r0, r4
00641D36: pop        {r4, r5, r7, pc}
T9 Movement deleting dtor

RANGE 00641D3C..00641D4A
00641D3C: push       {r7, lr}
00641D3E: bl         #0x641cfc
00641D42: pop.w      {r7, lr}
00641D46: b.w        #0x8ca88c
T10 RollingFileLogger dtor

RANGE 0080E090..0080E120 _ZN4Anki4Util17RollingFileLoggerD2Ev
0080E090: push       {r4, r5, r6, r7, lr}
0080E092: sub        sp, #4
0080E094: mov        r4, r0
0080E096: ldr        r0, [pc, #0xbc] ; literal[0080E154]=00831B3E
0080E098: mov        r5, r4
0080E09A: add        r0, pc
0080E09C: ldr        r0, [r0]
0080E09E: adds       r0, #8
0080E0A0: str        r0, [r5], #8
0080E0A4: mov        r0, r5
0080E0A6: bl         #0x801e70
0080E0AA: add.w      r6, r4, #0x3c
0080E0AE: mov        r0, r6
0080E0B0: blx        #0x4a69c4 ; _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE5closeEv -> 0050111C size=3C
0080E0B4: cbnz       r0, #0x80e0cc
0080E0B6: add.w      r0, r4, #0x38
0080E0BA: ldr        r1, [r0]
0080E0BC: ldr        r1, [r1, #-0xc]
0080E0C0: add        r0, r1
0080E0C2: ldr        r1, [r0, #0x10]
0080E0C4: orr        r1, r1, #4
0080E0C8: blx        #0x4a4594 ; _ZNSt6__ndk18ios_base5clearEj IMPORT (resolve packaged dependencies before calling external)
0080E0CC: ldr        r0, [pc, #0x88] ; literal[0080E158]=0083082C
0080E0CE: mov        r7, r4
0080E0D0: add        r0, pc
0080E0D2: ldr        r0, [r0]
0080E0D4: add.w      r1, r0, #0x20
0080E0D8: str        r1, [r7, #0xa0]!
0080E0DC: adds       r0, #0xc
0080E0DE: str        r0, [r7, #-0x68]
0080E0E2: mov        r0, r6
0080E0E4: blx        #0x4a6760 ; _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEED2Ev -> 005010B4 size=68
0080E0E8: mov        r0, r7
0080E0EA: blx        #0x4a44b0 ; _ZNSt6__ndk18ios_baseD2Ev IMPORT (resolve packaged dependencies before calling external)
0080E0EE: ldrb       r0, [r7, #-0x7c]
0080E0F2: lsls       r0, r0, #0x1f
0080E0F4: itt        ne
0080E0F6: ldrne      r0, [r4, #0x2c]
0080E0F8: blxne      #0x4a40cc
0080E0FC: ldrb       r0, [r4, #0x18]
0080E0FE: lsls       r0, r0, #0x1f
0080E100: itt        ne
0080E102: ldrne      r0, [r4, #0x20]
0080E104: blxne      #0x4a40cc
0080E108: ldrb       r0, [r4, #0xc]
0080E10A: lsls       r0, r0, #0x1f
0080E10C: itt        ne
0080E10E: ldrne      r0, [r4, #0x14]
0080E110: blxne      #0x4a40cc
0080E114: mov        r0, r5
0080E116: bl         #0x801e70
0080E11A: mov        r0, r4
0080E11C: add        sp, #4
0080E11E: pop        {r4, r5, r6, r7, pc}
T11 RollingFileLogger deleting dtor

RANGE 0080E15C..0080E16A _ZN4Anki4Util17RollingFileLoggerD0Ev
0080E15C: push       {r7, lr}
0080E15E: blx        #0x4cb84c ; _ZN4Anki4Util17RollingFileLoggerD2Ev -> 0080E090 size=CC
0080E162: pop.w      {r7, lr}
0080E166: b.w        #0x8ca88c
T12 shared handle zero

RANGE 004EF184..004EF198 _ZNSt6__ndk120__shared_ptr_pointerIPN6Signal3Lib21ScopedHandleContainerENS_14default_deleteIS3_EENS_9allocatorIS3_EEE16__on_zero_sharedEv
004EF184: ldr        r0, [r0, #0xc]
004EF186: cbz        r0, #0x4ef196
004EF188: push       {r7, lr}
004EF18A: bl         #0x4ef1b8
004EF18E: pop.w      {r7, lr}
004EF192: b.w        #0x8ca88c
004EF196: bx         lr
T13 handle vector

RANGE 004EAE94..004EAEC8 _ZNSt6__ndk113__vector_baseINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEED2Ev
004EAE94: push       {r4, r5, r7, lr}
004EAE96: mov        r4, r0
004EAE98: ldr        r5, [r4]
004EAE9A: cbz        r5, #0x4eaec4
004EAE9C: ldr        r0, [r4, #4]
004EAE9E: cmp        r0, r5
004EAEA0: beq        #0x4eaebe
004EAEA2: sub.w      r1, r0, #8
004EAEA6: str        r1, [r4, #4]
004EAEA8: ldr        r0, [r0, #-0x4]
004EAEAC: cbz        r0, #0x4eaeb6
004EAEAE: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
004EAEB2: ldr        r0, [r4, #4]
004EAEB4: b          #0x4eaeb8
004EAEB6: mov        r0, r1
004EAEB8: cmp        r0, r5
004EAEBA: bne        #0x4eaea2
004EAEBC: ldr        r5, [r4]
004EAEBE: mov        r0, r5
004EAEC0: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
004EAEC4: mov        r0, r4
004EAEC6: pop        {r4, r5, r7, pc}
T14 handle list

RANGE 005194A8..005194E2 _ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv
005194A8: push       {r4, r5, r6, lr}
005194AA: mov        r4, r0
005194AC: ldr        r0, [r4, #8]
005194AE: cbz        r0, #0x5194e0
005194B0: ldrd       r0, r5, [r4]
005194B4: cmp        r5, r4
005194B6: ldr        r1, [r5]
005194B8: ldr        r2, [r0, #4]
005194BA: str        r2, [r1, #4]
005194BC: ldr        r0, [r0, #4]
005194BE: ldr        r1, [r5]
005194C0: str        r1, [r0]
005194C2: mov.w      r0, #0
005194C6: str        r0, [r4, #8]
005194C8: beq        #0x5194e0
005194CA: ldr        r0, [r5, #0xc]
005194CC: ldr        r6, [r5, #4]
005194CE: cbz        r0, #0x5194d4
005194D0: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
005194D4: mov        r0, r5
005194D6: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005194DA: cmp        r6, r4
005194DC: mov        r5, r6
005194DE: bne        #0x5194ca
005194E0: pop        {r4, r5, r6, pc}
T15 backpack tree

RANGE 005195B6..005195E2 _ZNSt6__ndk16__treeINS_12__value_typeIiNS_4listINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS7_EEEEEENS_19__map_value_compareIiSB_NS_4lessIiEELb1EEENS8_ISB_EEE7destroyEPNS_11__tree_nodeISB_PvEE
005195B6: push       {r4, r5, r7, lr}
005195B8: mov        r4, r1
005195BA: mov        r5, r0
005195BC: cbz        r4, #0x5195e0
005195BE: ldr        r1, [r4]
005195C0: mov        r0, r5
005195C2: blx        #0x4a7900 ; _ZNSt6__ndk16__treeINS_12__value_typeIiNS_4listINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS7_EEEEEENS_19__map_value_compareIiSB_NS_4lessIiEELb1EEENS8_ISB_EEE7destroyEPNS_11__tree_nodeISB_PvEE -> 005195B6 size=2C
005195C6: ldr        r1, [r4, #4]
005195C8: mov        r0, r5
005195CA: blx        #0x4a7900 ; _ZNSt6__ndk16__treeINS_12__value_typeIiNS_4listINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS7_EEEEEENS_19__map_value_compareIiSB_NS_4lessIiEELb1EEENS8_ISB_EEE7destroyEPNS_11__tree_nodeISB_PvEE -> 005195B6 size=2C
005195CE: add.w      r0, r4, #0x14
005195D2: blx        #0x4a8254 ; _ZNSt6__ndk110__list_impINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS5_EEE5clearEv -> 005195E2 size=3A
005195D6: mov        r0, r4
005195D8: pop.w      {r4, r5, r7, lr}
005195DC: b.w        #0x8ca88c
005195E0: pop        {r4, r5, r7, pc}
T16 cube lights tree

RANGE 005194E2..00519522 _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeLightComponent10ObjectInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
005194E2: push       {r4, r5, r7, lr}
005194E4: mov        r4, r1
005194E6: mov        r5, r0
005194E8: cbz        r4, #0x519520
005194EA: ldr        r1, [r4]
005194EC: mov        r0, r5
005194EE: blx        #0x4a7918 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeLightComponent10ObjectInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 005194E2 size=40
005194F2: ldr        r1, [r4, #4]
005194F4: mov        r0, r5
005194F6: blx        #0x4a7918 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeLightComponent10ObjectInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 005194E2 size=40
005194FA: movs       r5, #0x5c
005194FC: adds       r0, r4, r5
005194FE: blx        #0x4a823c ; _ZNSt6__ndk110__list_impIN4Anki5Cozmo12LightPatternENS_9allocatorIS3_EEE5clearEv -> 00519522 size=3E
00519502: subs       r5, #0xc
00519504: cmp        r5, #0x38
00519506: bne        #0x5194fc
00519508: movs       r5, #0x38
0051950A: adds       r0, r4, r5
0051950C: blx        #0x4a8248 ; _ZNSt6__ndk110__list_impIN4Anki5Cozmo18CubeLightComponent15CurrentAnimInfoENS_9allocatorIS4_EEE5clearEv -> 00519560 size=56
00519510: subs       r5, #0xc
00519512: cmp        r5, #0x14
00519514: bne        #0x51950a
00519516: mov        r0, r4
00519518: pop.w      {r4, r5, r7, lr}
0051951C: b.w        #0x8ca88c
00519520: pop        {r4, r5, r7, pc}
T17 cube accel history tree

RANGE 00635D84..00635DBC _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeAccelComponent12AccelHistoryEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
00635D84: push       {r4, r5, r7, lr}
00635D86: mov        r4, r1
00635D88: mov        r5, r0
00635D8A: cbz        r4, #0x635dba
00635D8C: ldr        r1, [r4]
00635D8E: mov        r0, r5
00635D90: blx        #0x4b8fc4 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeAccelComponent12AccelHistoryEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 00635D84 size=38
00635D94: ldr        r1, [r4, #4]
00635D96: mov        r0, r5
00635D98: blx        #0x4b8fc4 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeAccelComponent12AccelHistoryEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 00635D84 size=38
00635D9C: ldr        r1, [r4, #0x2c]
00635D9E: add.w      r0, r4, #0x28
00635DA2: blx        #0x4b9000 ; _ZNSt6__ndk16__treeINS_10shared_ptrIN4Anki5Cozmo18CubeAccelListeners18ICubeAccelListenerEEENS_4lessIS6_EENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE -> 00635DBC size=2C
00635DA6: ldr        r1, [r4, #0x20]
00635DA8: add.w      r0, r4, #0x1c
00635DAC: blx        #0x4b900c ; _ZNSt6__ndk16__treeINS_12__value_typeIjN4Anki5Cozmo11ActiveAccelEEENS_19__map_value_compareIjS5_NS_4lessIjEELb1EEENS_9allocatorIS5_EEE7destroyEPNS_11__tree_nodeIS5_PvEE -> 00635DE8 size=24
00635DB0: mov        r0, r4
00635DB2: pop.w      {r4, r5, r7, lr}
00635DB6: b.w        #0x8ca88c
00635DBA: pop        {r4, r5, r7, pc}
T18 Movement lock tree

RANGE 00641D6E..00641DAA _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE
00641D6E: push       {r4, r5, r7, lr}
00641D70: mov        r4, r1
00641D72: mov        r5, r0
00641D74: cbz        r4, #0x641da8
00641D76: ldr        r1, [r4]
00641D78: mov        r0, r5
00641D7A: blx        #0x4b9600 ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE -> 00641D6E size=3C
00641D7E: ldr        r1, [r4, #4]
00641D80: mov        r0, r5
00641D82: blx        #0x4b9600 ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE -> 00641D6E size=3C
00641D86: ldrb       r0, [r4, #0x1c]
00641D88: lsls       r0, r0, #0x1f
00641D8A: itt        ne
00641D8C: ldrne      r0, [r4, #0x24]
00641D8E: blxne      #0x4a40cc
00641D92: ldrb       r0, [r4, #0x10]
00641D94: lsls       r0, r0, #0x1f
00641D96: itt        ne
00641D98: ldrne      r0, [r4, #0x18]
00641D9A: blxne      #0x4a40cc
00641D9E: mov        r0, r4
00641DA0: pop.w      {r4, r5, r7, lr}
00641DA4: b.w        #0x8ca88c
00641DA8: pop        {r4, r5, r7, pc}
L1 sleep lift child

RANGE 0052CF8A..0052CFC2
0052CF8A: ldr        r0, [r6]
0052CF8C: ldr        r7, [r0, #0x20]
0052CF8E: movs       r0, #0xa4
0052CF90: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
0052CF94: mov        r5, r0
0052CF96: movs       r3, #0
0052CF98: mov        r1, sb
0052CF9A: movt       r3, #0x40a0
0052CF9E: movs       r2, #0
0052CFA0: movs       r4, #0
0052CFA2: blx        #0x4a9a48 ; _ZN4Anki5Cozmo22MoveLiftToHeightActionC1ERNS0_5RobotENS1_6PresetEf -> 00548B84 size=90
0052CFA6: add        r0, sp, #0x14
0052CFA8: mov        r1, r6
0052CFAA: mov        r2, r5
0052CFAC: movs       r3, #0
0052CFAE: str        r4, [sp]
0052CFB0: blx        r7
0052CFB2: ldr        r0, [sp, #0x18]
0052CFB4: cbz        r0, #0x52cfba
0052CFB6: blx        #0x4a5560 ; _ZNSt6__ndk119__shared_weak_count14__release_weakEv IMPORT (resolve packaged dependencies before calling external)
0052CFBA: mov        r0, r6
0052CFBC: add        sp, #0x3c
0052CFBE: pop.w      {r4, r5, r6, r7, r8, sb, pc}
L2 preset ctor

RANGE 00548B84..00548BD6 _ZN4Anki5Cozmo22MoveLiftToHeightActionC2ERNS0_5RobotENS1_6PresetEf
00548B84: push       {r4, r5, r6, r7, lr}
00548B86: sub        sp, #0x14
00548B88: mov        r6, r2
00548B8A: mov        r4, r0
00548B8C: mov        r0, r6
00548B8E: mov        r5, r3
00548B90: mov        r7, r1
00548B92: blx        #0x4ab1a0 ; _ZN4Anki5Cozmo22MoveLiftToHeightAction15GetPresetHeightENS1_6PresetE -> 00548C14 size=C0
00548B96: mov        r2, r0
00548B98: movs       r0, #0
00548B9A: str        r0, [sp]
00548B9C: mov        r0, r4
00548B9E: mov        r1, r7
00548BA0: mov        r3, r5
00548BA2: blx        #0x4ab1ac ; _ZN4Anki5Cozmo22MoveLiftToHeightActionC2ERNS0_5RobotEfff -> 0054899C size=1E8
00548BA6: mov        r0, r6
00548BA8: blx        #0x4ab1b8 ; _ZN4Anki5Cozmo22MoveLiftToHeightAction13GetPresetNameENS1_6PresetE -> 00548CD4 size=278
00548BAC: mov        r2, r0
00548BAE: ldr        r1, [pc, #0x5c] ; literal[00548C0C]=006A17F2
00548BB0: add        r1, pc
00548BB2: add        r0, sp, #8
00548BB4: blx        #0x4a7330 ; _ZNSt6__ndk1plIcNS_11char_traitsIcEENS_9allocatorIcEEEENS_12basic_stringIT_T0_T1_EEPKS6_RKS9_ -> 0050B190 size=5C
00548BB8: add.w      r0, r4, #0x48
00548BBC: add        r1, sp, #8
00548BBE: bl         #0x4e462a
00548BC2: ldrb.w     r0, [sp, #8]
00548BC6: lsls       r0, r0, #0x1f
00548BC8: itt        ne
00548BCA: ldrne      r0, [sp, #0x10]
00548BCC: blxne      #0x4a40cc
00548BD0: mov        r0, r4
00548BD2: add        sp, #0x14
00548BD4: pop        {r4, r5, r6, r7, pc}
L3 preset height

RANGE 00548C14..00548C8A _ZN4Anki5Cozmo22MoveLiftToHeightAction15GetPresetHeightENS1_6PresetE
00548C14: push       {r4, r5, r6, lr}
00548C16: sub        sp, #8
00548C18: ldr        r1, [pc, #0x88] ; literal[00548CA4]=00B11CF2
00548C1A: strb.w     r0, [sp, #7]
00548C1E: add        r1, pc
00548C20: ldrb       r0, [r1]
00548C22: dmb        ish
00548C26: tst.w      r0, #1
00548C2A: bne        #0x548c78
00548C2C: ldr        r0, [pc, #0x78] ; literal[00548CA8]=00B11CE2
00548C2E: add        r0, pc
00548C30: blx        #0x4a472c ; __cxa_guard_acquire IMPORT (resolve packaged dependencies before calling external)
00548C34: cbz        r0, #0x548c78
00548C36: ldr        r0, [pc, #0x74] ; literal[00548CAC]=00B11CC6
00548C38: movs       r5, #0
00548C3A: ldr        r6, [pc, #0x74] ; literal[00548CB0]=0070BA40
00548C3C: ldr        r4, [pc, #0x74] ; literal[00548CB4]=00B11CC2
00548C3E: add        r0, pc
00548C40: add        r6, pc
00548C42: add        r4, pc
00548C44: mov        r1, r0
00548C46: str        r5, [r0, #8]
00548C48: str        r5, [r1, #4]!
00548C4C: str        r1, [r0]
00548C4E: adds       r2, r6, r5
00548C50: adds       r1, r4, #4
00548C52: mov        r0, r4
00548C54: mov        r3, r2
00548C56: blx        #0x4ab1c4 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_fEEEEENS_15__tree_iteratorIS6_PNS_11__tree_nodeIS6_PvEEiEENS_21__tree_const_iteratorIS6_SO_iEERKT_DpOT0_ -> 0054D830 size=38
00548C5A: adds       r5, #8
00548C5C: cmp        r5, #0x20
00548C5E: bne        #0x548c4e
00548C60: ldr        r0, [pc, #0x5c] ; literal[00548CC0]=000002E3
00548C62: ldr        r1, [pc, #0x60] ; literal[00548CC4]=00B11C9C
00548C64: ldr        r2, [pc, #0x60] ; literal[00548CC8]=00B08392
00548C66: add        r0, pc
00548C68: add        r1, pc
00548C6A: add        r2, pc
00548C6C: blx        #0x4a4024 ; __cxa_atexit IMPORT (resolve packaged dependencies before calling external)
00548C70: ldr        r0, [pc, #0x58] ; literal[00548CCC]=00B11C9E
00548C72: add        r0, pc
00548C74: blx        #0x4a475c ; __cxa_guard_release IMPORT (resolve packaged dependencies before calling external)
00548C78: ldr        r0, [pc, #0x54] ; literal[00548CD0]=00B11C86
00548C7A: add.w      r1, sp, #7
00548C7E: add        r0, pc
00548C80: blx        #0x4ab1d0 ; _ZNKSt6__ndk13mapIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfNS_4lessIS4_EENS_9allocatorINS_4pairIKS4_fEEEEE2atERS9_ -> 00548F5C size=80
00548C84: ldr        r0, [r0]
00548C86: add        sp, #8
00548C88: pop        {r4, r5, r6, pc}
L4 preset names

RANGE 00548CD4..00548E48 _ZN4Anki5Cozmo22MoveLiftToHeightAction13GetPresetNameENS1_6PresetE
00548CD4: push       {r4, r5, r6, r7, lr}
00548CD6: sub        sp, #0x44
00548CD8: mov        r4, r0
00548CDA: ldr        r0, [pc, #0x1d8] ; literal[00548EB4]=00B11C44
00548CDC: add        r0, pc
00548CDE: ldrb       r0, [r0]
00548CE0: dmb        ish
00548CE4: tst.w      r0, #1
00548CE8: bne        #0x548db0
00548CEA: ldr        r0, [pc, #0x1cc] ; literal[00548EB8]=00B11C34
00548CEC: add        r0, pc
00548CEE: blx        #0x4a472c ; __cxa_guard_acquire IMPORT (resolve packaged dependencies before calling external)
00548CF2: cmp        r0, #0
00548CF4: beq        #0x548db0
00548CF6: movs       r5, #0
00548CF8: str        r5, [sp, #0x10]
00548CFA: strd       r5, r5, [sp, #8]
00548CFE: strb.w     r5, [sp, #4]
00548D02: add        r6, sp, #4
00548D04: adr        r1, #0x1b4 ; ADR[00548EBC]=b'LowDock'
00548D06: adds       r0, r6, #4
00548D08: movs       r2, #7
00548D0A: bl         #0x4e02b2
00548D0E: movs       r0, #1
00548D10: str        r5, [sp, #0x20]
00548D12: strd       r5, r5, [sp, #0x18]
00548D16: strb.w     r0, [sp, #0x14]
00548D1A: add.w      r0, r6, #0x14
00548D1E: adr        r1, #0x1a4 ; ADR[00548EC4]=b'HighDock'
00548D20: movs       r2, #8
00548D22: bl         #0x4e02b2
00548D26: movs       r5, #0
00548D28: movs       r0, #2
00548D2A: str        r5, [sp, #0x30]
00548D2C: strd       r5, r5, [sp, #0x28]
00548D30: strb.w     r0, [sp, #0x24]
00548D34: add.w      r0, r6, #0x24
00548D38: adr        r1, #0x194 ; ADR[00548ED0]=b'HeightCarry'
00548D3A: movs       r2, #0xb
00548D3C: bl         #0x4e02b2
00548D40: movs       r0, #3
00548D42: str        r5, [sp, #0x40]
00548D44: strd       r5, r5, [sp, #0x38]
00548D48: strb.w     r0, [sp, #0x34]
00548D4C: add.w      r0, r6, #0x34
00548D50: adr        r1, #0x188 ; ADR[00548EDC]=b'OutOfFOV'
00548D52: movs       r2, #8
00548D54: bl         #0x4e02b2
00548D58: ldr        r0, [pc, #0x18c] ; literal[00548EE8]=00B11BB6
00548D5A: movs       r7, #0
00548D5C: ldr        r5, [pc, #0x18c] ; literal[00548EEC]=00B11BB4
00548D5E: add        r0, pc
00548D60: add        r5, pc
00548D62: mov        r1, r0
00548D64: str        r7, [r0, #8]
00548D66: str        r7, [r1, #4]!
00548D6A: str        r1, [r0]
00548D6C: adds       r2, r6, r7
00548D6E: adds       r1, r5, #4
00548D70: mov        r0, r5
00548D72: mov        r3, r2
00548D74: blx        #0x4ab1e8 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetENS_12basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEEEEENS_19__map_value_compareIS5_SC_NS_4lessIS5_EELb1EEENS9_ISC_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_SB_EEEEENS_15__tree_iteratorISC_PNS_11__tree_nodeISC_PvEEiEENS_21__tree_const_iteratorISC_ST_iEERKT_DpOT0_ -> 0054D976 size=32
00548D78: adds       r7, #0x10
00548D7A: cmp        r7, #0x40
00548D7C: bne        #0x548d6c
00548D7E: movs       r5, #0
00548D80: adds       r0, r6, r5
00548D82: ldrb.w     r1, [r0, #0x34]
00548D86: lsls       r1, r1, #0x1f
00548D88: itt        ne
00548D8A: ldrne      r0, [r0, #0x3c]
00548D8C: blxne      #0x4a40cc
00548D90: subs       r5, #0x10
00548D92: adds.w     r0, r5, #0x40
00548D96: bne        #0x548d80
00548D98: ldr        r0, [pc, #0x15c] ; literal[00548EF8]=0000023B
00548D9A: ldr        r1, [pc, #0x160] ; literal[00548EFC]=00B11B74
00548D9C: ldr        r2, [pc, #0x160] ; literal[00548F00]=00B0825A
00548D9E: add        r0, pc
00548DA0: add        r1, pc
00548DA2: add        r2, pc
00548DA4: blx        #0x4a4024 ; __cxa_atexit IMPORT (resolve packaged dependencies before calling external)
00548DA8: ldr        r0, [pc, #0x158] ; literal[00548F04]=00B11B76
00548DAA: add        r0, pc
00548DAC: blx        #0x4a475c ; __cxa_guard_release IMPORT (resolve packaged dependencies before calling external)
00548DB0: ldr        r0, [pc, #0x154] ; literal[00548F08]=00B11B7E
00548DB2: add        r0, pc
00548DB4: ldrb       r0, [r0]
00548DB6: dmb        ish
00548DBA: tst.w      r0, #1
00548DBE: bne        #0x548df8
00548DC0: ldr        r0, [pc, #0x148] ; literal[00548F0C]=00B11B6E
00548DC2: add        r0, pc
00548DC4: blx        #0x4a472c ; __cxa_guard_acquire IMPORT (resolve packaged dependencies before calling external)
00548DC8: cbz        r0, #0x548df8
00548DCA: ldr        r0, [pc, #0x144] ; literal[00548F10]=00B11B56
00548DCC: movs       r1, #0
00548DCE: add        r0, pc
00548DD0: strd       r1, r1, [r0]
00548DD4: str        r1, [r0, #8]
00548DD6: adr        r1, #0x13c ; ADR[00548F14]=b'UnknownPreset'
00548DD8: movs       r2, #0xd
00548DDA: bl         #0x4e02b2
00548DDE: ldr        r0, [pc, #0x148] ; literal[00548F28]=00AF5B56
00548DE0: ldr        r1, [pc, #0x148] ; literal[00548F2C]=00B11B3E
00548DE2: add        r0, pc
00548DE4: ldr        r2, [pc, #0x148] ; literal[00548F30]=00B08212
00548DE6: add        r1, pc
00548DE8: ldr        r0, [r0]
00548DEA: add        r2, pc
00548DEC: blx        #0x4a4024 ; __cxa_atexit IMPORT (resolve packaged dependencies before calling external)
00548DF0: ldr        r0, [pc, #0x140] ; literal[00548F34]=00B11B3E
00548DF2: add        r0, pc
00548DF4: blx        #0x4a475c ; __cxa_guard_release IMPORT (resolve packaged dependencies before calling external)
00548DF8: ldr        r0, [pc, #0x13c] ; literal[00548F38]=00B11B1A
00548DFA: add        r0, pc
00548DFC: ldr        r2, [r0, #4]
00548DFE: cmp        r2, #0
00548E00: beq        #0x548e40
00548E02: ldr        r0, [pc, #0x138] ; literal[00548F3C]=00B11B10
00548E04: add        r0, pc
00548E06: adds       r0, #4
00548E08: mov        r1, r2
00548E0A: ldrb       r2, [r1, #0x10]
00548E0C: cmp        r2, r4
00548E0E: bhs        #0x548e18
00548E10: ldr        r1, [r1, #4]
00548E12: cmp        r1, #0
00548E14: bne        #0x548e0a
00548E16: b          #0x548e22
00548E18: ldr        r2, [r1]
00548E1A: mov        r0, r1
00548E1C: cmp        r2, #0
00548E1E: bne        #0x548e08
00548E20: b          #0x548e24
00548E22: mov        r1, r0
00548E24: ldr        r0, [pc, #0x118] ; literal[00548F40]=00B11AEE
00548E26: add        r0, pc
00548E28: adds       r0, #4
00548E2A: cmp        r1, r0
00548E2C: beq        #0x548e40
00548E2E: ldr        r0, [pc, #0x114] ; literal[00548F44]=00B11AF2
00548E30: ldrb       r2, [r1, #0x10]
00548E32: add        r0, pc
00548E34: cmp        r2, r4
00548E36: it         ls
00548E38: addls.w    r0, r1, #0x14
00548E3C: add        sp, #0x44
00548E3E: pop        {r4, r5, r6, r7, pc}
00548E40: ldr        r0, [pc, #0x104] ; literal[00548F48]=00B11AE2
00548E42: add        r0, pc
00548E44: add        sp, #0x44
00548E46: pop        {r4, r5, r6, r7, pc}
L5 lift ctor

RANGE 0054899C..00548B16 _ZN4Anki5Cozmo22MoveLiftToHeightActionC2ERNS0_5RobotEfff
0054899C: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
005489A0: sub        sp, #4
005489A2: vpush      {d8, d9, d10}
005489A6: sub        sp, #0x50
005489A8: mov        r4, r0
005489AA: ldr        r0, [pc, #0x1c0] ; literal[00548B6C]=00AF5E98
005489AC: add        r6, sp, #4
005489AE: mov        sl, r2
005489B0: add        r0, pc
005489B2: mov        sb, r1
005489B4: mov        r1, sl
005489B6: mov        r8, r3
005489B8: ldr        r0, [r0]
005489BA: ldr        r0, [r0]
005489BC: str        r0, [sp, #0x4c]
005489BE: mov        r0, r6
005489C0: blx        #0x4a48e8 ; _ZNSt6__ndk19to_stringEf IMPORT (resolve packaged dependencies before calling external)
005489C4: ldr        r2, [pc, #0x1a8] ; literal[00548B70]=006A19DC
005489C6: add        r2, pc
005489C8: mov        r0, r6
005489CA: movs       r1, #0
005489CC: movs       r3, #0xa
005489CE: movs       r7, #0
005489D0: bl         #0x4e80b8
005489D4: add.w      ip, sp, #0x10
005489D8: mov        r1, r0
005489DA: ldm.w      r1, {r2, r5, r6}
005489DE: mov        r3, ip
005489E0: stm        r3!, {r2, r5, r6}
005489E2: strd       r7, r7, [r0]
005489E6: str        r7, [r0, #8]
005489E8: adr        r1, #0x188 ; ADR[00548B74]=b'mm'
005489EA: mov        r0, ip
005489EC: movs       r2, #2
005489EE: mov.w      fp, #2
005489F2: bl         #0x4e8048
005489F6: add        r2, sp, #0x20
005489F8: mov        r1, r0
005489FA: ldm.w      r1, {r5, r6, r7}
005489FE: movs       r1, #0
00548A00: mov        r3, r2
00548A02: stm        r3!, {r5, r6, r7}
00548A04: strd       r1, r1, [r0]
00548A08: str        r1, [r0, #8]
00548A0A: mov        r0, r4
00548A0C: mov        r1, sb
00548A0E: movs       r3, #0x13
00548A10: str.w      fp, [sp]
00548A14: blx        #0x4aadd4 ; _ZN4Anki5Cozmo7IActionC2ERNS0_5RobotENSt6__ndk112basic_stringIcNS4_11char_traitsIcEENS4_9allocatorIcEEEENS0_15RobotActionTypeEh -> 00540C44 size=A4
00548A18: ldrb.w     r0, [sp, #0x20]
00548A1C: lsls       r0, r0, #0x1f
00548A1E: itt        ne
00548A20: ldrne      r0, [sp, #0x28]
00548A22: blxne      #0x4a40cc
00548A26: ldrb.w     r0, [sp, #0x10]
00548A2A: lsls       r0, r0, #0x1f
00548A2C: itt        ne
00548A2E: ldrne      r0, [sp, #0x18]
00548A30: blxne      #0x4a40cc
00548A34: ldrb.w     r0, [sp, #4]
00548A38: vmov       s16, r8
00548A3C: vldr       s18, [sp, #0x90]
00548A40: vmov       s20, sl
00548A44: lsls       r0, r0, #0x1f
00548A46: itt        ne
00548A48: ldrne      r0, [sp, #0xc]
00548A4A: blxne      #0x4a40cc
00548A4E: ldr        r0, [pc, #0x128] ; literal[00548B78]=00AF60F4
00548A50: movs       r2, #0
00548A52: movs       r1, #0
00548A54: movt       r2, #0x4120
00548A58: add        r0, pc
00548A5A: vstr       s20, [r4, #0x78]
00548A5E: vstr       s16, [r4, #0x7c]
00548A62: ldr        r0, [r0]
00548A64: vstr       s18, [r4, #0x80]
00548A68: str.w      r1, [r4, #0x88]
00548A6C: adds       r0, #8
00548A6E: str.w      r2, [r4, #0x8c]
00548A72: movs       r2, #0
00548A74: movt       r2, #0x41a0
00548A78: str.w      r2, [r4, #0x90]
00548A7C: strd       r1, r1, [r4, #0x9c]
00548A80: str.w      r1, [r4, #0x95]
00548A84: str        r0, [r4]
00548A86: ldr        r0, [r4, #4]
00548A88: blx        #0x4a82d8 ; _ZN4Anki5Cozmo5Robot22GetRobotMessageHandlerEv -> 005182EA size=E
00548A8C: mov        r1, r0
00548A8E: ldr        r0, [r4, #4]
00548A90: add        r6, sp, #0x30
00548A92: movs       r3, #0xc4
00548A94: ldr        r2, [r0, #0x10]
00548A96: ldr        r0, [pc, #0xe4] ; literal[00548B7C]=00AD92A6
00548A98: str        r4, [sp, #0x34]
00548A9A: add        r0, pc
00548A9C: strb.w     r3, [sp, #4]
00548AA0: adds       r0, #8
00548AA2: str        r6, [sp, #0x40]
00548AA4: str        r0, [sp, #0x30]
00548AA6: add        r0, sp, #0x10
00548AA8: add        r3, sp, #4
00548AAA: str        r6, [sp]
00548AAC: bl         #0x519f8c
00548AB0: movs       r0, #0
00548AB2: ldrd       r1, r2, [sp, #0x10]
00548AB6: strd       r0, r0, [sp, #0x10]
00548ABA: ldr.w      r0, [r4, #0xa0]
00548ABE: strd       r1, r2, [r4, #0x9c]
00548AC2: cbz        r0, #0x548ad0
00548AC4: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
00548AC8: ldr        r0, [sp, #0x14]
00548ACA: cbz        r0, #0x548ad0
00548ACC: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
00548AD0: ldr        r0, [sp, #0x40]
00548AD2: cmp        r6, r0
00548AD4: beq        #0x548ade
00548AD6: cbz        r0, #0x548ae4
00548AD8: ldr        r1, [r0]
00548ADA: ldr        r1, [r1, #0x14]
00548ADC: b          #0x548ae2
00548ADE: ldr        r1, [r0]
00548AE0: ldr        r1, [r1, #0x10]
00548AE2: blx        r1
00548AE4: ldr        r0, [pc, #0x98] ; literal[00548B80]=00AF5D60
00548AE6: ldr        r1, [sp, #0x4c]
00548AE8: add        r0, pc
00548AEA: ldr        r0, [r0]
00548AEC: ldr        r0, [r0]
00548AEE: subs       r0, r0, r1
00548AF0: itttt      eq
00548AF2: moveq      r0, r4
00548AF4: addeq      sp, #0x50
00548AF6: vpopeq     {d8, d9, d10}
00548AFA: addeq      sp, #4
00548AFC: it         eq
00548AFE: popeq.w    {r4, r5, r6, r7, r8, sb, sl, fp, pc}
00548B02: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
00548B06: mov        r5, r0
00548B08: ldr        r0, [sp, #0x40]
00548B0A: cmp        r6, r0
00548B0C: bne        #0x548b14
00548B0E: ldr        r1, [r0]
00548B10: ldr        r1, [r1, #0x10]
00548B12: b          #0x548b1a
00548B14: cbz        r0, #0x548b20
L6 lift Init

RANGE 0054903C..0054933A _ZN4Anki5Cozmo22MoveLiftToHeightAction4InitEv
0054903C: push       {r4, r5, r6, lr}
0054903E: vpush      {d8, d9, d10, d11}
00549042: sub        sp, #0x30
00549044: mov        r4, r0
00549046: movs       r0, #0
00549048: vldr       s0, [r4, #0x78]
0054904C: strb.w     r0, [r4, #0x98]
00549050: vcmpe.f32  s0, #0
00549054: strh.w     r0, [r4, #0x95]
00549058: vmrs       apsr_nzcv, fpscr
0054905C: blt        #0x5490fa
0054905E: vldr       s16, [pc, #0x304] ; literal[00549364]=42000000
00549062: vcmpe.f32  s0, s16
00549066: vmrs       apsr_nzcv, fpscr
0054906A: bmi        #0x54907a
0054906C: vldr       s2, [pc, #0x2f8] ; literal[00549368]=42B80000
00549070: vcmpe.f32  s0, s2
00549074: vmrs       apsr_nzcv, fpscr
00549078: ble        #0x5490fa
0054907A: movs       r0, #0
0054907C: vcvt.f64.f32 d0, s0
00549080: strd       r0, r0, [sp, #0x24]
00549084: str        r0, [sp, #0x2c]
00549086: adr        r0, #0x2e4 ; ADR[0054936C]=b'MoveLiftToHeightAction.Init.InvalidHeight'
00549088: add        r1, sp, #0x24
0054908A: adr        r2, #0x30c ; ADR[00549398]=b'%f mm. Clipping to be in range.'
0054908C: vstr       d0, [sp]
00549090: blx        #0x4a4540 ; _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D2B4 size=80
00549094: ldr        r0, [sp, #0x24]
00549096: cbz        r0, #0x5490b6
00549098: ldr        r1, [sp, #0x28]
0054909A: cmp        r1, r0
0054909C: itttt      ne
0054909E: subne.w    r2, r1, #8
005490A2: subne      r2, r2, r0
005490A4: mvnne      r3, #7
005490A8: bicne.w    r2, r3, r2
005490AC: itt        ne
005490AE: addne      r1, r1, r2
005490B0: strne      r1, [sp, #0x28]
005490B2: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005490B6: vldr       s2, [pc, #0x2b0] ; literal[00549368]=42B80000
005490BA: vmov.f32   s0, s16
005490BE: vldr       s4, [r4, #0x78]
005490C2: vcmpe.f32  s4, s2
005490C6: vmrs       apsr_nzcv, fpscr
005490CA: vcmpe.f32  s4, s16
005490CE: it         pl
005490D0: vmovpl.f32 s0, s2
005490D4: vmrs       apsr_nzcv, fpscr
005490D8: vcmpe.f32  s4, s2
005490DC: it         gt
005490DE: vmovgt.f32 s16, s0
005490E2: vmov.f32   s0, s16
005490E6: it         gt
005490E8: vmovgt.f32 s0, s4
005490EC: vmrs       apsr_nzcv, fpscr
005490F0: it         pl
005490F2: vmovpl.f32 s0, s16
005490F6: vstr       s0, [r4, #0x78]
005490FA: vcmpe.f32  s0, #0
005490FE: vmrs       apsr_nzcv, fpscr
00549102: bpl        #0x549142
00549104: ldr        r0, [r4, #4]
00549106: blx        #0x4ab200 ; _ZNK4Anki5Cozmo5Robot13GetLiftHeightEv -> 00516F64 size=38
0054910A: vmov       s16, r0
0054910E: movs       r0, #0
00549110: blx        #0x4ab1a0 ; _ZN4Anki5Cozmo22MoveLiftToHeightAction15GetPresetHeightENS1_6PresetE -> 00548C14 size=C0
00549114: vmov       s18, r0
00549118: movs       r0, #2
0054911A: blx        #0x4ab1a0 ; _ZN4Anki5Cozmo22MoveLiftToHeightAction15GetPresetHeightENS1_6PresetE -> 00548C14 size=C0
0054911E: vmov       s2, r0
00549122: vsub.f32   s0, s16, s18
00549126: vsub.f32   s4, s2, s16
0054912A: vabs.f32   s0, s0
0054912E: vabs.f32   s4, s4
00549132: vcmpe.f32  s0, s4
00549136: vmrs       apsr_nzcv, fpscr
0054913A: it         mi
0054913C: vmovmi.f32 s2, s18
00549140: b          #0x5491ce
00549142: vldr       s2, [r4, #0x80]
00549146: vstr       s0, [r4, #0x84]
0054914A: vcmpe.f32  s2, #0
0054914E: vmrs       apsr_nzcv, fpscr
00549152: ble        #0x54918e
00549154: mov        r0, r4
00549156: blx        #0x4ab008 ; _ZNK4Anki5Cozmo7IAction6GetRNGEv -> 00540D10 size=A
0054915A: vldr       s0, [r4, #0x80]
0054915E: vneg.f32   s2, s0
00549162: vcvt.f64.f32 d0, s0
00549166: vstr       d0, [sp]
0054916A: vcvt.f64.f32 d1, s2
0054916E: vmov       r2, r3, d1
00549172: blx        #0x4aa5f4 ; _ZNK4Anki4Util15RandomGenerator14RandDblInRangeEdd -> 0082FA48 size=2C
00549176: vldr       s0, [r4, #0x84]
0054917A: vmov       d1, r0, r1
0054917E: vcvt.f64.f32 d0, s0
00549182: vadd.f64   d0, d1, d0
00549186: vcvt.f32.f64 s0, d0
0054918A: vstr       s0, [r4, #0x84]
0054918E: vldr       s4, [pc, #0x1d8] ; literal[00549368]=42B80000
00549192: vldr       s6, [pc, #0x1d0] ; literal[00549364]=42000000
00549196: vcmpe.f32  s0, s4
0054919A: vmrs       apsr_nzcv, fpscr
0054919E: vmov.f32   s2, s6
005491A2: vcmpe.f32  s0, s6
005491A6: it         pl
005491A8: vmovpl.f32 s2, s4
005491AC: vmrs       apsr_nzcv, fpscr
005491B0: vcmpe.f32  s0, s4
005491B4: it         gt
005491B6: vmovgt.f32 s6, s2
005491BA: vmov.f32   s2, s6
005491BE: it         gt
005491C0: vmovgt.f32 s2, s0
005491C4: vmrs       apsr_nzcv, fpscr
005491C8: it         pl
005491CA: vmovpl.f32 s2, s6
005491CE: vmov       r0, s2
005491D2: vstr       s2, [r4, #0x84]
005491D6: vldr       s0, [r4, #0x7c]
005491DA: vsub.f32   s20, s2, s0
005491DE: vadd.f32   s22, s2, s0
005491E2: vmov       r5, s20
005491E6: blx        #0x4ab20c ; _ZN4Anki5Cozmo5Robot31ConvertLiftHeightToLiftAngleRadEf -> 005170B0 size=58
005491EA: mov        r6, r0
005491EC: mov        r0, r5
005491EE: blx        #0x4ab20c ; _ZN4Anki5Cozmo5Robot31ConvertLiftHeightToLiftAngleRadEf -> 005170B0 size=58
005491F2: vldr       s2, [pc, #0x170] ; literal[00549364]=42000000
005491F6: vmov       r1, s22
005491FA: vmov       s0, r0
005491FE: vldr       s16, [pc, #0x1b8] ; literal[005493B8]=7F7FFFFF
00549202: vcmpe.f32  s20, s2
00549206: vmrs       apsr_nzcv, fpscr
0054920A: vmov       s18, r6
0054920E: vsub.f32   s0, s18, s0
00549212: mov        r0, r1
00549214: it         gt
00549216: vmovgt.f32 s16, s0
0054921A: blx        #0x4ab20c ; _ZN4Anki5Cozmo5Robot31ConvertLiftHeightToLiftAngleRadEf -> 005170B0 size=58
0054921E: vldr       s0, [pc, #0x148] ; literal[00549368]=42B80000
00549222: vcmpe.f32  s22, s0
00549226: vmrs       apsr_nzcv, fpscr
0054922A: bpl        #0x549242
0054922C: vmov       s0, r0
00549230: vsub.f32   s0, s0, s18
00549234: vcmpe.f32  s0, s16
00549238: vmrs       apsr_nzcv, fpscr
0054923C: it         mi
0054923E: vmovmi.f32 s16, s0
00549242: vldr       s20, [pc, #0x178] ; literal[005493BC]=3CD67750
00549246: vcmpe.f32  s16, s20
0054924A: vmrs       apsr_nzcv, fpscr
0054924E: bpl        #0x5492f2
00549250: vldr       s0, [pc, #0x16c] ; literal[005493C0]=BCD67750
00549254: vadd.f32   s0, s18, s0
00549258: vmov       r0, s0
0054925C: blx        #0x4a9e98 ; _ZN4Anki5Cozmo5Robot30ConvertLiftAngleToLiftHeightMMEf -> 00516F9C size=34
00549260: vadd.f32   s0, s18, s20
00549264: mov        r5, r0
00549266: vmov       r0, s0
0054926A: blx        #0x4a9e98 ; _ZN4Anki5Cozmo5Robot30ConvertLiftAngleToLiftHeightMMEf -> 00516F9C size=34
0054926E: vmov       s0, r0
00549272: vldr       s2, [r4, #0x78]
00549276: vmov       s6, r5
0054927A: movs       r0, #0
0054927C: vsub.f32   s0, s0, s2
00549280: vldr       s4, [r4, #0x7c]
00549284: vsub.f32   s18, s2, s6
00549288: strd       r0, r0, [sp, #0x24]
0054928C: str        r0, [sp, #0x2c]
0054928E: vcvt.f64.f32 d2, s4
00549292: ldr        r2, [pc, #0x130] ; literal[005493C4]=006A1119
00549294: add        r2, pc
00549296: vldr       s2, [r4, #0x84]
0054929A: vcmpe.f32  s18, s0
0054929E: vmrs       apsr_nzcv, fpscr
005492A2: it         mi
005492A4: vmovmi.f32 s18, s0
005492A8: vcvt.f64.f32 d0, s2
005492AC: vcvt.f64.f32 d1, s16
005492B0: vcvt.f64.f32 d3, s18
005492B4: adr        r0, #0x110 ; ADR[005493C8]=b'MoveLiftToHeightAction.Init.TolTooSmall'
005492B6: add        r1, sp, #0x24
005492B8: vstr       d2, [sp]
005492BC: vstr       d1, [sp, #8]
005492C0: vstr       d0, [sp, #0x10]
005492C4: vstr       d3, [sp, #0x18]
005492C8: blx        #0x4a4540 ; _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D2B4 size=80
005492CC: ldr        r0, [sp, #0x24]
005492CE: cbz        r0, #0x5492ee
005492D0: ldr        r1, [sp, #0x28]
005492D2: cmp        r1, r0
005492D4: itttt      ne
005492D6: subne.w    r2, r1, #8
005492DA: subne      r2, r2, r0
005492DC: mvnne      r3, #7
005492E0: bicne.w    r2, r3, r2
005492E4: itt        ne
005492E6: addne      r1, r1, r2
005492E8: strne      r1, [sp, #0x28]
005492EA: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005492EE: vstr       s18, [r4, #0x7c]
005492F2: mov        r0, r4
005492F4: blx        #0x4ab218 ; _ZNK4Anki5Cozmo22MoveLiftToHeightAction16IsLiftInPositionEv -> 00548FEA size=50
005492F8: cmp        r0, #0
005492FA: strb.w     r0, [r4, #0x97]
005492FE: bne        #0x549330
00549300: ldr        r0, [r4, #4]
00549302: add.w      r6, r4, #0x94
00549306: ldrd       r2, r3, [r4, #0x8c]
0054930A: ldr.w      r1, [r4, #0x84]
0054930E: ldr.w      r0, [r0, #0x254]
00549312: vldr       s0, [r4, #0x88]
00549316: str        r6, [sp, #4]
00549318: vstr       s0, [sp]
0054931C: blx        #0x4ab224 ; _ZN4Anki5Cozmo17MovementComponent16MoveLiftToHeightEffffPh -> 00640700 size=42
00549320: cbz        r0, #0x54932a
00549322: movs       r0, #0x16
00549324: movt       r0, #0x300
00549328: b          #0x549332
0054932A: movs       r0, #1
0054932C: strb.w     r0, [r4, #0x95]
00549330: movs       r0, #0
00549332: add        sp, #0x30
00549334: vpop       {d8, d9, d10, d11}
00549338: pop        {r4, r5, r6, pc}
L7 lift CheckIfDone

RANGE 005493F0..0054956E _ZN4Anki5Cozmo22MoveLiftToHeightAction11CheckIfDoneEv
005493F0: push       {r4, r5, r6, lr}
005493F2: sub        sp, #0x30
005493F4: mov        r4, r0
005493F6: ldrb.w     r0, [r4, #0x95]
005493FA: cbz        r0, #0x549406
005493FC: ldrb.w     r0, [r4, #0x96]
00549400: cmp        r0, #0
00549402: beq.w      #0x54950a
00549406: ldrb.w     r0, [r4, #0x97]
0054940A: cbz        r0, #0x549410
0054940C: movs       r1, #1
0054940E: b          #0x54941c
00549410: mov        r0, r4
00549412: blx        #0x4ab218 ; _ZNK4Anki5Cozmo22MoveLiftToHeightAction16IsLiftInPositionEv -> 00548FEA size=50
00549416: mov        r1, r0
00549418: strb.w     r1, [r4, #0x97]
0054941C: ldr        r0, [r4, #4]
0054941E: ldr.w      r2, [r0, #0x254]
00549422: ldrb       r5, [r2, #0xb]
00549424: cbz        r5, #0x54942c
00549426: movs       r2, #1
00549428: strb.w     r2, [r4, #0x98]
0054942C: cbz        r1, #0x54943a
0054942E: cmp        r5, #0
00549430: it         ne
00549432: movne.w    r5, #0x1000000
00549436: mov        r0, r5
00549438: b          #0x54956a
0054943A: ldr        r1, [pc, #0x1a4] ; literal[005495E0]=00B07BEE
0054943C: add        r1, pc
0054943E: ldrh       r2, [r1]
00549440: adds       r2, #1
00549442: strh       r2, [r1]
00549444: uxth       r1, r2
00549446: cmp        r1, #0xb
00549448: blo        #0x5494b8
0054944A: movs       r1, #0
0054944C: strd       r1, r1, [sp, #0x24]
00549450: str        r1, [sp, #0x2c]
00549452: ldr        r6, [r4, #0x60]
00549454: blx        #0x4ab200 ; _ZNK4Anki5Cozmo5Robot13GetLiftHeightEv -> 00516F64 size=38
00549458: vmov       s0, r0
0054945C: ldr        r0, [pc, #0x184] ; literal[005495E4]=006A057A
0054945E: vldr       s2, [r4, #0x7c]
00549462: vldr       s4, [r4, #0x84]
00549466: add        r0, pc
00549468: ldr        r3, [pc, #0x17c] ; literal[005495E8]=006A0F8E
0054946A: vcvt.f64.f32 d0, s0
0054946E: add        r3, pc
00549470: vcvt.f64.f32 d2, s4
00549474: vcvt.f64.f32 d1, s2
00549478: adr        r1, #0x170 ; ADR[005495EC]=b'MoveLiftToHeightAction.CheckIfDone.NotInPosition'
0054947A: add        r2, sp, #0x24
0054947C: str        r6, [sp]
0054947E: vstr       d0, [sp, #8]
00549482: vstr       d2, [sp, #0x10]
00549486: vstr       d1, [sp, #0x18]
0054948A: blx        #0x4a5b84 ; _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D818 size=7C
0054948E: ldr        r0, [sp, #0x24]
00549490: cbz        r0, #0x5494b0
00549492: ldr        r1, [sp, #0x28]
00549494: cmp        r1, r0
00549496: itttt      ne
00549498: subne.w    r2, r1, #8
0054949C: subne      r2, r2, r0
0054949E: mvnne      r3, #7
005494A2: bicne.w    r2, r3, r2
005494A6: itt        ne
005494A8: addne      r1, r1, r2
005494AA: strne      r1, [sp, #0x28]
005494AC: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005494B0: ldr        r0, [pc, #0x16c] ; literal[00549620]=00B07B76
005494B2: movs       r1, #0
005494B4: add        r0, pc
005494B6: strh       r1, [r0]
005494B8: mov.w      r0, #0x1000000
005494BC: cmp        r5, #0
005494BE: bne        #0x54956a
005494C0: ldrb.w     r1, [r4, #0x98]
005494C4: cmp        r1, #0
005494C6: beq        #0x54956a
005494C8: movs       r0, #0
005494CA: ldr        r2, [pc, #0x15c] ; literal[00549628]=006A0EA9
005494CC: strd       r0, r0, [sp, #0x24]
005494D0: str        r0, [sp, #0x2c]
005494D2: add        r2, pc
005494D4: ldr        r0, [pc, #0x14c] ; literal[00549624]=006A0F6A
005494D6: ldr        r3, [r4, #0x60]
005494D8: add        r0, pc
005494DA: add        r1, sp, #0x24
005494DC: blx        #0x4a4540 ; _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D2B4 size=80
005494E0: ldr        r0, [sp, #0x24]
005494E2: cbz        r0, #0x549502
005494E4: ldr        r1, [sp, #0x28]
005494E6: cmp        r1, r0
005494E8: itttt      ne
005494EA: subne.w    r2, r1, #8
005494EE: subne      r2, r2, r0
005494F0: mvnne      r3, #7
005494F4: bicne.w    r2, r3, r2
005494F8: itt        ne
005494FA: addne      r1, r1, r2
005494FC: strne      r1, [sp, #0x28]
005494FE: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00549502: movs       r0, #4
00549504: movt       r0, #0x400
00549508: b          #0x54956a
0054950A: ldr        r0, [pc, #0x90] ; literal[0054959C]=00B07B1C
0054950C: add        r0, pc
0054950E: ldrh       r1, [r0]
00549510: adds       r1, #1
00549512: strh       r1, [r0]
00549514: uxth       r0, r1
00549516: cmp        r0, #0xb
00549518: blo        #0x549566
0054951A: movs       r0, #0
0054951C: ldr        r3, [pc, #0x84] ; literal[005495A4]=006A08C0
0054951E: strd       r0, r0, [sp, #0x24]
00549522: str        r0, [sp, #0x2c]
00549524: add        r3, pc
00549526: ldr        r0, [pc, #0x78] ; literal[005495A0]=006A04B6
00549528: ldr        r1, [r4, #0x60]
0054952A: add        r0, pc
0054952C: ldrb.w     r2, [r4, #0x94]
00549530: strd       r1, r2, [sp]
00549534: adr        r1, #0x70 ; ADR[005495A8]=b'MoveLiftToHeightAction.CheckIfDone.WaitingForAck'
00549536: add        r2, sp, #0x24
00549538: blx        #0x4a5b84 ; _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D818 size=7C
0054953C: ldr        r0, [sp, #0x24]
0054953E: cbz        r0, #0x54955e
00549540: ldr        r1, [sp, #0x28]
00549542: cmp        r1, r0
00549544: itttt      ne
00549546: subne.w    r2, r1, #8
0054954A: subne      r2, r2, r0
0054954C: mvnne      r3, #7
00549550: bicne.w    r2, r3, r2
00549554: itt        ne
00549556: addne      r1, r1, r2
00549558: strne      r1, [sp, #0x28]
0054955A: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0054955E: ldr        r0, [pc, #0x7c] ; literal[005495DC]=00B07AC6
00549560: movs       r1, #0
00549562: add        r0, pc
00549564: strh       r1, [r0]
00549566: mov.w      r0, #0x1000000
0054956A: add        sp, #0x30
0054956C: pop        {r4, r5, r6, pc}
L8 lift in position

RANGE 00548FEA..0054903A _ZNK4Anki5Cozmo22MoveLiftToHeightAction16IsLiftInPositionEv
00548FEA: push       {r4, lr}
00548FEC: vpush      {d8}
00548FF0: mov        r4, r0
00548FF2: ldr        r0, [r4, #4]
00548FF4: vldr       s16, [r4, #0x84]
00548FF8: blx        #0x4ab200 ; _ZNK4Anki5Cozmo5Robot13GetLiftHeightEv -> 00516F64 size=38
00548FFC: vmov       s0, r0
00549000: movs       r0, #0
00549002: vsub.f32   s0, s16, s0
00549006: vcmpe.f32  s0, #0
0054900A: vmrs       apsr_nzcv, fpscr
0054900E: vneg.f32   s2, s0
00549012: it         mi
00549014: vmovmi.f32 s0, s2
00549018: vldr       s2, [r4, #0x7c]
0054901C: vcmpe.f32  s0, s2
00549020: vmrs       apsr_nzcv, fpscr
00549024: bpl        #0x549034
00549026: ldr        r1, [r4, #4]
00549028: ldr.w      r1, [r1, #0x254]
0054902C: ldrb       r1, [r1, #0xb]
0054902E: cmp        r1, #0
00549030: it         eq
00549032: moveq      r0, #1
00549034: vpop       {d8}
00549038: pop        {r4, pc}
L9 lift movement

RANGE 00640700..00640742 _ZN4Anki5Cozmo17MovementComponent16MoveLiftToHeightEffffPh
00640700: push       {r7, lr}
00640702: sub        sp, #0x18
00640704: str        r1, [sp, #0x14]
00640706: mov        ip, r0
00640708: strd       r3, r2, [sp, #0xc]
0064070C: ldrb.w     r1, [ip, #8]
00640710: ldr        r2, [sp, #0x24]
00640712: ldr.w      r0, [ip, #4]
00640716: adds       r1, #1
00640718: cmp        r2, #0
0064071A: strb.w     r1, [ip, #8]
0064071E: beq        #0x640726
00640720: strb       r1, [r2]
00640722: ldrb.w     r1, [ip, #8]
00640726: strb.w     r1, [sp, #0xb]
0064072A: add.w      r1, sp, #0xb
0064072E: add        r2, sp, #0x20
00640730: add        r3, sp, #0xc
00640732: strd       r2, r1, [sp]
00640736: add        r1, sp, #0x14
00640738: add        r2, sp, #0x10
0064073A: bl         #0x640744
0064073E: add        sp, #0x18
00640740: pop        {r7, pc}
L10 lift conversion

RANGE 005170B0..005170F4 _ZN4Anki5Cozmo5Robot31ConvertLiftHeightToLiftAngleRadEf
005170B0: vldr       s0, [pc, #0x40] ; literal[005170F4]=42000000
005170B4: vmov       s2, r0
005170B8: vldr       s4, [pc, #0x40] ; literal[005170FC]=42840000
005170BC: vcmpe.f32  s2, s0
005170C0: vldr       s6, [pc, #0x40] ; literal[00517104]=3F364D93
005170C4: vmrs       apsr_nzcv, fpscr
005170C8: it         gt
005170CA: vmovgt.f32 s0, s2
005170CE: vldr       s2, [pc, #0x28] ; literal[005170F8]=C2340000
005170D2: vadd.f32   s2, s0, s2
005170D6: vdiv.f32   s2, s2, s4
005170DA: vldr       s4, [pc, #0x24] ; literal[00517100]=42B80000
005170DE: vcmpe.f32  s0, s4
005170E2: vmrs       apsr_nzcv, fpscr
005170E6: it         mi
005170E8: vmovmi.f32 s6, s2
005170EC: vmov       r0, s6
005170F0: b.w        #0x8cae7c
L11 lift dtor

RANGE 0054D138..0054D15A
0054D138: push       {r4, lr}
0054D13A: mov        r4, r0
0054D13C: ldr        r0, [pc, #0x1c] ; literal[0054D15C]=00AF1A0E
0054D13E: add        r0, pc
0054D140: ldr        r1, [r0]
0054D142: ldr.w      r0, [r4, #0xa0]
0054D146: adds       r1, #8
0054D148: str        r1, [r4]
0054D14A: cbz        r0, #0x54d150
0054D14C: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
0054D150: mov        r0, r4
0054D152: pop.w      {r4, lr}
0054D156: b.w        #0x8cb50c
D1 PrintLockState

RANGE 006410D8..006413CA _ZNK4Anki5Cozmo17MovementComponent14PrintLockStateEv
006410D8: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
006410DC: sub        sp, #0xd4
006410DE: mov        sb, r0
006410E0: ldr.w      r0, [pc, #0x3bc] ; literal[006414A0]=009FD6CE
006410E4: ldr.w      r1, [pc, #0x3bc] ; literal[006414A4]=009FD6CE
006410E8: movs       r4, #0
006410EA: add        r0, pc
006410EC: add        r6, sp, #0x40
006410EE: add        r1, pc
006410F0: str        r4, [sp, #0x44]
006410F2: ldr        r0, [r0]
006410F4: add.w      r7, r6, #0xc
006410F8: ldr        r1, [r1]
006410FA: add.w      r2, r0, #0xc
006410FE: adds       r0, #0x20
00641100: str        r2, [sp, #0x40]
00641102: adds       r1, #0x20
00641104: str        r1, [sp, #0x48]
00641106: str        r0, [sp, #0x80]
00641108: add.w      r0, r6, #0x40
0064110C: mov        r1, r7
0064110E: str        r0, [sp, #0x10]
00641110: blx        #0x4a445c ; _ZNSt6__ndk18ios_base4initEPv IMPORT (resolve packaged dependencies before calling external)
00641114: ldr        r0, [pc, #0x390] ; literal[006414A8]=009FD6A0
00641116: mov.w      r2, #-1
0064111A: ldr        r1, [pc, #0x390] ; literal[006414AC]=009FD6A0
0064111C: add        r0, pc
0064111E: str        r2, [sp, #0xcc]
00641120: add        r1, pc
00641122: str        r4, [sp, #0xc8]
00641124: ldr        r0, [r0]
00641126: ldr        r1, [r1]
00641128: add.w      r2, r0, #0x34
0064112C: str        r2, [sp, #0x80]
0064112E: add.w      r2, r0, #0xc
00641132: adds       r0, #0x20
00641134: str        r2, [sp, #0x40]
00641136: str        r0, [sp, #0x48]
00641138: add.w      r0, r1, #8
0064113C: str        r0, [sp, #0x4c]
0064113E: add.w      r0, r6, #0x10
00641142: str        r0, [sp, #0xc]
00641144: blx        #0x4a4468 ; _ZNSt6__ndk16localeC1Ev IMPORT (resolve packaged dependencies before calling external)
00641148: add.w      r0, r6, #0x14
0064114C: movs       r1, #0x18
0064114E: movs       r5, #0x18
00641150: blx        #0x4a403c ; __aeabi_memclr4 IMPORT (resolve packaged dependencies before calling external)
00641154: ldr        r0, [pc, #0x358] ; literal[006414B0]=009FD66C
00641156: str        r4, [sp, #0x6c]
00641158: add        r0, pc
0064115A: ldr        r0, [r0]
0064115C: adds       r0, #8
0064115E: str        r0, [sp, #0x4c]
00641160: strd       r4, r4, [sp, #0x70]
00641164: strd       r4, r5, [sp, #0x78]
00641168: str        r4, [sp, #0x30]
0064116A: strd       r4, r4, [sp, #0x28]
0064116E: add        r1, sp, #0x28
00641170: mov        r0, r7
00641172: bl         #0x4e4598
00641176: ldrb.w     r0, [sp, #0x28]
0064117A: str        r7, [sp, #8]
0064117C: lsls       r0, r0, #0x1f
0064117E: itt        ne
00641180: ldrne      r0, [sp, #0x30]
00641182: blxne      #0x4a40cc
00641186: add        r0, sp, #0x28
00641188: adr        r7, #0x32c ; ADR[006414B8]=b':'
0064118A: orr        r1, r0, #1
0064118E: add.w      r5, r0, #0xc
00641192: str        r1, [sp, #0x14]
00641194: mov.w      sl, #0
00641198: movs       r1, #1
0064119A: mov.w      fp, #0x20
0064119E: mov.w      r8, #0
006411A2: add.w      r0, r6, #8
006411A6: str        r0, [sp, #0x18]
006411A8: add.w      r0, r8, r8, lsl #1
006411AC: add.w      r4, sb, r0, lsl #2
006411B0: mov        r6, r4
006411B2: ldr        r0, [r6, #0x30]!
006411B6: cmp        r0, #0
006411B8: beq.w      #0x64130e
006411BC: lsl.w      r0, r1, r8
006411C0: uxtb       r1, r0
006411C2: add        r0, sp, #0x28
006411C4: blx        #0x4aaa98 ; _ZN4Anki5Cozmo16AnimTrackHelpers22AnimTrackFlagsToStringEh -> 006305E8 size=25C
006411C8: ldrd       r2, r1, [sp, #0x2c]
006411CC: ldrb.w     r0, [sp, #0x28]
006411D0: ands       r3, r0, #1
006411D4: ldr        r3, [sp, #0x14]
006411D6: itt        eq
006411D8: moveq      r1, r3
006411DA: lsreq      r2, r0, #1
006411DC: ldr        r0, [sp, #0x18]
006411DE: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
006411E2: mov        r1, r7
006411E4: movs       r2, #1
006411E6: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
006411EA: ldr        r1, [r6]
006411EC: blx        #0x4a8134 ; _ZNSt6__ndk113basic_ostreamIcNS_11char_traitsIcEEElsEj -> 0051778C size=10C
006411F0: strb.w     fp, [sp, #0xd3]
006411F4: add.w      r1, sp, #0xd3
006411F8: movs       r2, #1
006411FA: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
006411FE: ldrb.w     r0, [sp, #0x28]
00641202: mov        r7, sb
00641204: lsls       r0, r0, #0x1f
00641206: itt        ne
00641208: ldrne      r0, [sp, #0x30]
0064120A: blxne      #0x4a40cc
0064120E: ldr.w      sb, [r4, #0x28]
00641212: add.w      r6, r4, #0x2c
00641216: cmp        sb, r6
00641218: beq        #0x6412f4
0064121A: mov        fp, sb
0064121C: str.w      sl, [sp, #0x30]
00641220: mov        r0, sb
00641222: strd       sl, sl, [sp, #0x28]
00641226: ldrb       r1, [r0, #0x10]!
0064122A: tst.w      r1, #1
0064122E: bne        #0x64123a
00641230: add        r1, sp, #0x28
00641232: ldm.w      r0, {r2, r3, r4}
00641236: stm        r1!, {r2, r3, r4}
00641238: b          #0x641244
0064123A: ldrd       r2, r1, [sb, #0x14]
0064123E: add        r0, sp, #0x28
00641240: bl         #0x4e02b2
00641244: strd       sl, sl, [r5]
00641248: mov        r0, sb
0064124A: str.w      sl, [r5, #8]
0064124E: ldrb       r1, [r0, #0x1c]!
00641252: tst.w      r1, #1
00641256: bne        #0x641262
00641258: mov        r1, r5
0064125A: ldm.w      r0, {r2, r3, r4}
0064125E: stm        r1!, {r2, r3, r4}
00641260: b          #0x64126c
00641262: ldrd       r2, r1, [sb, #0x20]
00641266: mov        r0, r5
00641268: bl         #0x4e02b2
0064126C: ldrd       r2, r1, [sp, #0x38]
00641270: ldrb.w     r0, [sp, #0x34]
00641274: ands       r3, r0, #1
00641278: add        r3, sp, #0x28
0064127A: itt        eq
0064127C: addeq.w    r1, r3, #0xd
00641280: lsreq      r2, r0, #1
00641282: ldr        r0, [sp, #0x18]
00641284: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
00641288: adr        r1, #0x230 ; ADR[006414BC]=b'['
0064128A: movs       r2, #1
0064128C: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
00641290: ldrd       r2, r1, [sp, #0x2c]
00641294: ldrb.w     r3, [sp, #0x28]
00641298: ands       r4, r3, #1
0064129C: ldr        r4, [sp, #0x14]
0064129E: itt        eq
006412A0: moveq      r1, r4
006412A2: lsreq      r2, r3, #1
006412A4: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
006412A8: adr        r1, #0x214 ; ADR[006414C0]=b'] '
006412AA: movs       r2, #2
006412AC: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
006412B0: ldrb.w     r0, [sp, #0x34]
006412B4: lsls       r0, r0, #0x1f
006412B6: itt        ne
006412B8: ldrne      r0, [sp, #0x3c]
006412BA: blxne      #0x4a40cc
006412BE: ldrb.w     r0, [sp, #0x28]
006412C2: lsls       r0, r0, #0x1f
006412C4: itt        ne
006412C6: ldrne      r0, [sp, #0x30]
006412C8: blxne      #0x4a40cc
006412CC: ldr.w      r0, [fp, #4]
006412D0: cmp        r0, #0
006412D2: beq        #0x6412e0
006412D4: mov        sb, r0
006412D6: ldr.w      r0, [sb]
006412DA: cmp        r0, #0
006412DC: bne        #0x6412d4
006412DE: b          #0x6412ee
006412E0: ldr.w      sb, [fp, #8]
006412E4: ldr.w      r0, [sb]
006412E8: cmp        r0, fp
006412EA: mov        fp, sb
006412EC: bne        #0x6412e0
006412EE: cmp        sb, r6
006412F0: mov        fp, sb
006412F2: bne        #0x64121c
006412F4: movs       r0, #0xa
006412F6: strb.w     r0, [sp, #0x1c]
006412FA: ldr        r0, [sp, #0x18]
006412FC: add        r1, sp, #0x1c
006412FE: movs       r2, #1
00641300: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
00641304: mov        sb, r7
00641306: adr        r7, #0x1b0 ; ADR[006414B8]=b':'
00641308: movs       r1, #1
0064130A: mov.w      fp, #0x20
0064130E: add.w      r0, r8, #1
00641312: cmp.w      r8, #7
00641316: mov        r8, r0
00641318: blt.w      #0x6411a8
0064131C: movs       r0, #0
0064131E: strd       r0, r0, [sp, #0x28]
00641322: str        r0, [sp, #0x30]
00641324: add        r4, sp, #0x1c
00641326: ldr        r1, [sp, #8]
00641328: mov        r0, r4
0064132A: blx        #0x4a448c ; _ZNKSt6__ndk115basic_stringbufIcNS_11char_traitsIcEENS_9allocatorIcEEE3strEv -> 004E4784 size=4A
0064132E: ldr        r0, [pc, #0x194] ; literal[006414C4]=005A2CB2
00641330: ldrb.w     r2, [sp, #0x1c]
00641334: ldr        r1, [sp, #0x24]
00641336: add        r0, pc
00641338: tst.w      r2, #1
0064133C: it         eq
0064133E: orreq      r1, r4, #1
00641342: str        r1, [sp]
00641344: adr        r1, #0x180 ; ADR[006414C8]=b'MovementComponent.LockState'
00641346: add        r2, sp, #0x28
00641348: adr        r3, #0x198 ; ADR[006414E4]=b'%s'
0064134A: blx        #0x4a5b84 ; _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D818 size=7C
0064134E: ldrb.w     r0, [sp, #0x1c]
00641352: lsls       r0, r0, #0x1f
00641354: itt        ne
00641356: ldrne      r0, [sp, #0x24]
00641358: blxne      #0x4a40cc
0064135C: ldr        r0, [sp, #0x28]
0064135E: cbz        r0, #0x64137e
00641360: ldr        r1, [sp, #0x2c]
00641362: cmp        r1, r0
00641364: itttt      ne
00641366: subne.w    r2, r1, #8
0064136A: subne      r2, r2, r0
0064136C: mvnne      r3, #7
00641370: bicne.w    r2, r3, r2
00641374: itt        ne
00641376: addne      r1, r1, r2
00641378: strne      r1, [sp, #0x2c]
0064137A: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0064137E: ldr        r0, [pc, #0x174] ; literal[006414F4]=009FD43A
00641380: ldr        r1, [pc, #0x174] ; literal[006414F8]=009FD43C
00641382: add        r0, pc
00641384: ldrb.w     r2, [sp, #0x6c]
00641388: add        r1, pc
0064138A: ldr        r0, [r0]
0064138C: ldr        r1, [r1]
0064138E: add.w      r3, r0, #0x34
00641392: str        r3, [sp, #0x80]
00641394: add.w      r3, r0, #0xc
00641398: adds       r0, #0x20
0064139A: str        r3, [sp, #0x40]
0064139C: str        r0, [sp, #0x48]
0064139E: add.w      r0, r1, #8
006413A2: str        r0, [sp, #0x4c]
006413A4: lsls       r0, r2, #0x1f
006413A6: itt        ne
006413A8: ldrne      r0, [sp, #0x74]
006413AA: blxne      #0x4a40cc
006413AE: ldr        r0, [pc, #0x14c] ; literal[006414FC]=009FD410
006413B0: add        r0, pc
006413B2: ldr        r0, [r0]
006413B4: adds       r0, #8
006413B6: str        r0, [sp, #0x4c]
006413B8: ldr        r0, [sp, #0xc]
006413BA: blx        #0x4a44a4 ; _ZNSt6__ndk16localeD1Ev IMPORT (resolve packaged dependencies before calling external)
006413BE: ldr        r0, [sp, #0x10]
006413C0: blx        #0x4a44b0 ; _ZNSt6__ndk18ios_baseD2Ev IMPORT (resolve packaged dependencies before calling external)
006413C4: add        sp, #0xd4
006413C6: pop.w      {r4, r5, r6, r7, r8, sb, sl, fp, pc}
D2 track strings

RANGE 006305E8..00630788 _ZN4Anki5Cozmo16AnimTrackHelpers22AnimTrackFlagsToStringEh
006305E8: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
006305EC: sub        sp, #0xac
006305EE: mov        r7, r1
006305F0: mov        r6, r0
006305F2: cmp        r7, #0xff
006305F4: beq        #0x63060a
006305F6: cbnz       r7, #0x630630
006305F8: movs       r0, #0
006305FA: movs       r5, #0
006305FC: blx        #0x4b8cac ; _ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE -> 007BC250 size=88
00630600: mov        r4, r0
00630602: strd       r5, r5, [r6]
00630606: str        r5, [r6, #8]
00630608: b          #0x63061a
0063060A: movs       r0, #0xff
0063060C: blx        #0x4b8cac ; _ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE -> 007BC250 size=88
00630610: mov        r4, r0
00630612: movs       r0, #0
00630614: strd       r0, r0, [r6]
00630618: str        r0, [r6, #8]
0063061A: mov        r0, r4
0063061C: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
00630620: mov        r2, r0
00630622: mov        r0, r6
00630624: mov        r1, r4
00630626: bl         #0x4e02b2
0063062A: add        sp, #0xac
0063062C: pop.w      {r4, r5, r6, r7, r8, sb, sl, fp, pc}
00630630: ldr        r0, [pc, #0x1e0] ; literal[00630814]=00A0E17C
00630632: add.w      sb, sp, #0x10
00630636: ldr        r1, [pc, #0x1e0] ; literal[00630818]=00A0E17A
00630638: add.w      sl, sb, #0x40
0063063C: add        r0, pc
0063063E: add.w      fp, sb, #0xc
00630642: add        r1, pc
00630644: movs       r5, #0
00630646: ldr        r0, [r0]
00630648: ldr        r1, [r1]
0063064A: add.w      r2, r0, #0xc
0063064E: adds       r0, #0x20
00630650: adds       r1, #0x20
00630652: str        r5, [sp, #0x14]
00630654: str        r2, [sp, #0x10]
00630656: str        r1, [sp, #0x18]
00630658: str        r0, [sp, #0x50]
0063065A: mov        r0, sl
0063065C: mov        r1, fp
0063065E: blx        #0x4a445c ; _ZNSt6__ndk18ios_base4initEPv IMPORT (resolve packaged dependencies before calling external)
00630662: ldr        r0, [pc, #0x1b8] ; literal[0063081C]=00A0E14E
00630664: mov.w      r2, #-1
00630668: ldr        r1, [pc, #0x1b4] ; literal[00630820]=00A0E14E
0063066A: add.w      r4, sb, #0x10
0063066E: add        r0, pc
00630670: str        r2, [sp, #0x9c]
00630672: add        r1, pc
00630674: str        r5, [sp, #0x98]
00630676: ldr        r0, [r0]
00630678: ldr        r1, [r1]
0063067A: add.w      r2, r0, #0x34
0063067E: str        r2, [sp, #0x50]
00630680: add.w      r2, r0, #0xc
00630684: adds       r0, #0x20
00630686: str        r2, [sp, #0x10]
00630688: str        r0, [sp, #0x18]
0063068A: add.w      r0, r1, #8
0063068E: str        r0, [sp, #0x1c]
00630690: mov        r0, r4
00630692: blx        #0x4a4468 ; _ZNSt6__ndk16localeC1Ev IMPORT (resolve packaged dependencies before calling external)
00630696: add.w      r0, sb, #0x14
0063069A: movs       r1, #0x18
0063069C: mov.w      r8, #0x18
006306A0: blx        #0x4a403c ; __aeabi_memclr4 IMPORT (resolve packaged dependencies before calling external)
006306A4: ldr        r0, [pc, #0x17c] ; literal[00630824]=00A0E11C
006306A6: str        r5, [sp, #0x3c]
006306A8: add        r0, pc
006306AA: ldr        r0, [r0]
006306AC: adds       r0, #8
006306AE: str        r0, [sp, #0x1c]
006306B0: strd       r5, r5, [sp, #0x40]
006306B4: strd       r5, r8, [sp, #0x48]
006306B8: str        r5, [sp, #0xa8]
006306BA: strd       r5, r5, [sp, #0xa0]
006306BE: add        r1, sp, #0xa0
006306C0: mov        r0, fp
006306C2: bl         #0x4e4598
006306C6: strd       fp, r4, [sp, #4]
006306CA: ldrb.w     r0, [sp, #0xa0]
006306CE: str.w      sl, [sp, #0xc]
006306D2: lsls       r0, r0, #0x1f
006306D4: itt        ne
006306D6: ldrne      r0, [sp, #0xa8]
006306D8: blxne      #0x4a40cc
006306DC: add.w      r5, sb, #8
006306E0: add.w      fp, sp, #0xa0
006306E4: mov.w      r8, #1
006306E8: mov.w      sb, #-1
006306EC: mov.w      sl, #0x2b
006306F0: movs       r0, #1
006306F2: add.w      sb, sb, #1
006306F6: lsl.w      r4, r8, sb
006306FA: tst        r4, r7
006306FC: beq        #0x63072a
006306FE: lsls       r0, r0, #0x1f
00630700: bne        #0x630710
00630702: strb.w     sl, [sp, #0xa0]
00630706: mov        r0, r5
00630708: mov        r1, fp
0063070A: movs       r2, #1
0063070C: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
00630710: uxtb       r0, r4
00630712: blx        #0x4b8cac ; _ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE -> 007BC250 size=88
00630716: mov        r4, r0
00630718: mov        r0, r4
0063071A: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063071E: mov        r2, r0
00630720: mov        r0, r5
00630722: mov        r1, r4
00630724: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
00630728: movs       r0, #0
0063072A: cmp.w      sb, #7
0063072E: blt        #0x6306f2
00630730: ldr        r1, [sp, #4]
00630732: mov        r0, r6
00630734: blx        #0x4a448c ; _ZNKSt6__ndk115basic_stringbufIcNS_11char_traitsIcEENS_9allocatorIcEEE3strEv -> 004E4784 size=4A
00630738: ldr        r0, [pc, #0xfc] ; literal[00630838]=00A0E080
0063073A: ldr        r1, [pc, #0x100] ; literal[0063083C]=00A0E082
0063073C: add        r0, pc
0063073E: ldrb.w     r2, [sp, #0x3c]
00630742: add        r1, pc
00630744: ldr        r0, [r0]
00630746: ldr        r1, [r1]
00630748: add.w      r3, r0, #0x34
0063074C: str        r3, [sp, #0x50]
0063074E: add.w      r3, r0, #0xc
00630752: adds       r0, #0x20
00630754: str        r3, [sp, #0x10]
00630756: str        r0, [sp, #0x18]
00630758: add.w      r0, r1, #8
0063075C: str        r0, [sp, #0x1c]
0063075E: lsls       r0, r2, #0x1f
00630760: ldrd       r5, r4, [sp, #8]
00630764: itt        ne
00630766: ldrne      r0, [sp, #0x44]
00630768: blxne      #0x4a40cc
0063076C: ldr        r0, [pc, #0xd0] ; literal[00630840]=00A0E052
0063076E: add        r0, pc
00630770: ldr        r0, [r0]
00630772: adds       r0, #8
00630774: str        r0, [sp, #0x1c]
00630776: mov        r0, r5
00630778: blx        #0x4a44a4 ; _ZNSt6__ndk16localeD1Ev IMPORT (resolve packaged dependencies before calling external)
0063077C: mov        r0, r4
0063077E: blx        #0x4a44b0 ; _ZNSt6__ndk18ios_baseD2Ev IMPORT (resolve packaged dependencies before calling external)
00630782: add        sp, #0xac
00630784: pop.w      {r4, r5, r6, r7, r8, sb, sl, fp, pc}
N1 NV delete

RANGE 005112D6..005112E6
005112D6: ldr.w      r0, [r4, #0x260]
005112DA: str.w      r5, [r4, #0x260]
005112DE: cbz        r0, #0x5112e6
005112E0: ldr        r1, [r0]
005112E2: ldr        r1, [r1, #4]
005112E4: blx        r1
N2 NV dtor

RANGE 00643EC4..00643F8E _ZN4Anki5Cozmo18NVStorageComponentD1Ev
00643EC4: push       {r4, r5, r6, lr}
00643EC6: mov        r4, r0
00643EC8: ldr        r0, [pc, #0xc4] ; literal[00643F90]=009FB992
00643ECA: add.w      r5, r4, #0x128
00643ECE: add        r0, pc
00643ED0: ldr        r1, [r0]
00643ED2: ldrd       r6, r0, [r4, #0x128]
00643ED6: adds       r1, #8
00643ED8: str        r1, [r4]
00643EDA: b          #0x643ef6
00643EDC: sub.w      r1, r0, #8
00643EE0: str.w      r1, [r4, #0x12c]
00643EE4: ldr        r0, [r0, #-0x4]
00643EE8: cbz        r0, #0x643ef4
00643EEA: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
00643EEE: ldr.w      r0, [r4, #0x12c]
00643EF2: b          #0x643ef6
00643EF4: mov        r0, r1
00643EF6: cmp        r0, r6
00643EF8: bne        #0x643edc
00643EFA: add.w      r0, r4, #0x144
00643EFE: blx        #0x4b9870 ; _ZNSt6__ndk112__hash_tableIhNS_4hashIhEENS_8equal_toIhEENS_9allocatorIhEEED2Ev -> 00646B40 size=24
00643F02: ldr.w      r0, [r4, #0x138]
00643F06: cbz        r0, #0x643f18
00643F08: ldr.w      r1, [r4, #0x13c]
00643F0C: cmp        r1, r0
00643F0E: it         ne
00643F10: strne.w    r0, [r4, #0x13c]
00643F14: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643F18: mov        r0, r5
00643F1A: blx        #0x4a4e4c ; _ZNSt6__ndk113__vector_baseINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEED2Ev -> 004EAE94 size=34
00643F1E: add.w      r0, r4, #0x110
00643F22: blx        #0x4b987c ; _ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEED2Ev -> 00646B64 size=26
00643F26: add.w      r0, r4, #0xf8
00643F2A: blx        #0x4b9888 ; _ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEED2Ev -> 00646C6C size=26
00643F2E: ldr.w      r0, [r4, #0xe8]
00643F32: cbz        r0, #0x643f44
00643F34: ldr.w      r1, [r4, #0xec]
00643F38: cmp        r1, r0
00643F3A: it         ne
00643F3C: strne.w    r0, [r4, #0xec]
00643F40: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643F44: add.w      r0, r4, #0x80
00643F48: blx        #0x4b9894 ; _ZN4Anki5Cozmo22RobotDataBackupManagerD1Ev -> 0051A5F0 size=7C
00643F4C: add.w      r0, r4, #0x50
00643F50: bl         #0x643e80
00643F54: ldr        r0, [r4, #0x38]
00643F56: add.w      r1, r4, #0x28
00643F5A: cmp        r1, r0
00643F5C: beq        #0x643f66
00643F5E: cbz        r0, #0x643f6c
00643F60: ldr        r1, [r0]
00643F62: ldr        r1, [r1, #0x14]
00643F64: b          #0x643f6a
00643F66: ldr        r1, [r0]
00643F68: ldr        r1, [r1, #0x10]
00643F6A: blx        r1
00643F6C: ldr        r5, [r4, #0x18]
00643F6E: cbz        r5, #0x643f86
00643F70: ldr        r0, [r5]
00643F72: cbz        r0, #0x643f80
00643F74: ldr        r1, [r5, #4]
00643F76: cmp        r1, r0
00643F78: it         ne
00643F7A: strne      r0, [r5, #4]
00643F7C: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643F80: mov        r0, r5
00643F82: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643F86: movs       r0, #0
00643F88: str        r0, [r4, #0x18]
00643F8A: mov        r0, r4
00643F8C: pop        {r4, r5, r6, pc}
N3 NV deleting dtor

RANGE 00643F94..00643FA2 _ZN4Anki5Cozmo18NVStorageComponentD0Ev
00643F94: push       {r7, lr}
00643F96: blx        #0x4b9948 ; _ZN4Anki5Cozmo18NVStorageComponentD2Ev -> 00643EC4 size=D0
00643F9A: pop.w      {r7, lr}
00643F9E: b.w        #0x8ca88c
N4 active request dtor

RANGE 00643E80..00643EC4
00643E80: push       {r4, r5, r7, lr}
00643E82: mov        r4, r0
00643E84: ldrb.w     r0, [r4, #0x20]
00643E88: cbz        r0, #0x643ea8
00643E8A: ldr        r5, [r4, #4]
00643E8C: cbz        r5, #0x643ea4
00643E8E: ldr        r0, [r5]
00643E90: cbz        r0, #0x643e9e
00643E92: ldr        r1, [r5, #4]
00643E94: cmp        r1, r0
00643E96: it         ne
00643E98: strne      r0, [r5, #4]
00643E9A: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643E9E: mov        r0, r5
00643EA0: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643EA4: movs       r0, #0
00643EA6: str        r0, [r4, #4]
00643EA8: ldr        r0, [r4, #0x18]
00643EAA: add.w      r1, r4, #8
00643EAE: cmp        r1, r0
00643EB0: beq        #0x643eba
00643EB2: cbz        r0, #0x643ec0
00643EB4: ldr        r1, [r0]
00643EB6: ldr        r1, [r1, #0x14]
00643EB8: b          #0x643ebe
00643EBA: ldr        r1, [r0]
00643EBC: ldr        r1, [r1, #0x10]
00643EBE: blx        r1
00643EC0: mov        r0, r4
00643EC2: pop        {r4, r5, r7, pc}
N5 idle deque clear

RANGE 00646B8A..00646C42 _ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEE5clearEv
00646B8A: push       {r4, r5, r6, r7, lr}
00646B8C: sub        sp, #4
00646B8E: mov        r4, r0
00646B90: movw       r3, #0xc0c1
00646B94: ldrd       r0, r7, [r4, #4]
00646B98: movt       r3, #0xc0c0
00646B9C: ldr        r1, [r4, #0x10]
00646B9E: cmp        r7, r0
00646BA0: umull      r2, r6, r1, r3
00646BA4: lsr.w      r2, r6, #7
00646BA8: add.w      r5, r0, r2, lsl #2
00646BAC: beq        #0x646bda
00646BAE: ldr        r7, [r4, #0x14]
00646BB0: add        r7, r1
00646BB2: umull      r3, r6, r7, r3
00646BB6: lsrs       r3, r6, #7
00646BB8: movs       r6, #0xaa
00646BBA: mls        r7, r3, r6, r7
00646BBE: ldr.w      r0, [r0, r3, lsl #2]
00646BC2: mls        r1, r2, r6, r1
00646BC6: ldr        r2, [r5]
00646BC8: add.w      r3, r7, r7, lsl #1
00646BCC: add.w      r6, r0, r3, lsl #3
00646BD0: add.w      r0, r1, r1, lsl #1
00646BD4: add.w      r7, r2, r0, lsl #3
00646BD8: b          #0x646c04
00646BDA: movs       r7, #0
00646BDC: movs       r6, #0
00646BDE: b          #0x646c04
00646BE0: ldr        r0, [r7, #0x10]
00646BE2: cmp        r7, r0
00646BE4: beq        #0x646bee
00646BE6: cbz        r0, #0x646bf4
00646BE8: ldr        r1, [r0]
00646BEA: ldr        r1, [r1, #0x14]
00646BEC: b          #0x646bf2
00646BEE: ldr        r1, [r0]
00646BF0: ldr        r1, [r1, #0x10]
00646BF2: blx        r1
00646BF4: ldr        r0, [r5]
00646BF6: adds       r7, #0x18
00646BF8: subs       r0, r7, r0
00646BFA: cmp.w      r0, #0xff0
00646BFE: it         eq
00646C00: ldreq      r7, [r5, #4]!
00646C04: cmp        r6, r7
00646C06: bne        #0x646be0
00646C08: ldrd       r0, r1, [r4, #4]
00646C0C: movs       r2, #0
00646C0E: str        r2, [r4, #0x14]
00646C10: subs       r1, r1, r0
00646C12: asrs       r1, r1, #2
00646C14: cmp        r1, #3
00646C16: blo        #0x646c2e
00646C18: ldr        r0, [r0]
00646C1A: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646C1E: ldrd       r0, r1, [r4, #4]
00646C22: adds       r0, #4
00646C24: str        r0, [r4, #4]
00646C26: subs       r1, r1, r0
00646C28: asrs       r1, r1, #2
00646C2A: cmp        r1, #2
00646C2C: bhi        #0x646c18
00646C2E: cmp        r1, #2
00646C30: beq        #0x646c3a
00646C32: cmp        r1, #1
00646C34: bne        #0x646c3e
00646C36: movs       r0, #0x55
00646C38: b          #0x646c3c
00646C3A: movs       r0, #0xaa
00646C3C: str        r0, [r4, #0x10]
00646C3E: add        sp, #4
00646C40: pop        {r4, r5, r6, r7, pc}
N6 requests deque clear

RANGE 00646C92..00646D6C _ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEE5clearEv
00646C92: push       {r4, r5, r6, r7, lr}
00646C94: sub        sp, #4
00646C96: mov        r4, r0
00646C98: movw       r3, #0x4925
00646C9C: ldrd       r1, r7, [r4, #4]
00646CA0: movt       r3, #0x2492
00646CA4: ldr        r0, [r4, #0x10]
00646CA6: cmp        r7, r1
00646CA8: lsr.w      r2, r0, #3
00646CAC: umull      r6, r2, r2, r3
00646CB0: add.w      r5, r1, r2, lsl #2
00646CB4: beq        #0x646ce8
00646CB6: ldr        r7, [r4, #0x14]
00646CB8: add        r7, r0
00646CBA: lsrs       r6, r7, #3
00646CBC: umull      r3, r6, r6, r3
00646CC0: ldr        r3, [r5]
00646CC2: ldr.w      r1, [r1, r6, lsl #2]
00646CC6: rsb        r6, r6, r6, lsl #3
00646CCA: sub.w      r7, r7, r6, lsl #3
00646CCE: add.w      r7, r7, r7, lsl #3
00646CD2: add.w      r6, r1, r7, lsl #3
00646CD6: rsb        r1, r2, r2, lsl #3
00646CDA: sub.w      r0, r0, r1, lsl #3
00646CDE: add.w      r0, r0, r0, lsl #3
00646CE2: add.w      r7, r3, r0, lsl #3
00646CE6: b          #0x646d2e
00646CE8: movs       r7, #0
00646CEA: movs       r6, #0
00646CEC: b          #0x646d2e
00646CEE: ldr        r0, [r7, #0x30]
00646CF0: add.w      r1, r7, #0x20
00646CF4: cmp        r1, r0
00646CF6: beq        #0x646d00
00646CF8: cbz        r0, #0x646d06
00646CFA: ldr        r1, [r0]
00646CFC: ldr        r1, [r1, #0x14]
00646CFE: b          #0x646d04
00646D00: ldr        r1, [r0]
00646D02: ldr        r1, [r1, #0x10]
00646D04: blx        r1
00646D06: ldr        r0, [r7, #0x18]
00646D08: add.w      r1, r7, #8
00646D0C: cmp        r1, r0
00646D0E: beq        #0x646d18
00646D10: cbz        r0, #0x646d1e
00646D12: ldr        r1, [r0]
00646D14: ldr        r1, [r1, #0x14]
00646D16: b          #0x646d1c
00646D18: ldr        r1, [r0]
00646D1A: ldr        r1, [r1, #0x10]
00646D1C: blx        r1
00646D1E: ldr        r0, [r5]
00646D20: adds       r7, #0x48
00646D22: subs       r0, r7, r0
00646D24: cmp.w      r0, #0xfc0
00646D28: it         eq
00646D2A: ldreq      r7, [r5, #4]!
00646D2E: cmp        r6, r7
00646D30: bne        #0x646cee
00646D32: ldrd       r0, r1, [r4, #4]
00646D36: movs       r2, #0
00646D38: str        r2, [r4, #0x14]
00646D3A: subs       r1, r1, r0
00646D3C: asrs       r1, r1, #2
00646D3E: cmp        r1, #3
00646D40: blo        #0x646d58
00646D42: ldr        r0, [r0]
00646D44: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646D48: ldrd       r0, r1, [r4, #4]
00646D4C: adds       r0, #4
00646D4E: str        r0, [r4, #4]
00646D50: subs       r1, r1, r0
00646D52: asrs       r1, r1, #2
00646D54: cmp        r1, #2
00646D56: bhi        #0x646d42
00646D58: cmp        r1, #2
00646D5A: beq        #0x646d64
00646D5C: cmp        r1, #1
00646D5E: bne        #0x646d68
00646D60: movs       r0, #0x1c
00646D62: b          #0x646d66
00646D64: movs       r0, #0x38
00646D66: str        r0, [r4, #0x10]
00646D68: add        sp, #4
00646D6A: pop        {r4, r5, r6, r7, pc}
N7 idle deque dtor

RANGE 00646B64..00646B8A _ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEED2Ev
00646B64: push       {r4, r5, r6, lr}
00646B66: mov        r4, r0
00646B68: blx        #0x4b9a8c ; _ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEE5clearEv -> 00646B8A size=B8
00646B6C: ldrd       r5, r6, [r4, #4]
00646B70: cmp        r5, r6
00646B72: beq        #0x646b80
00646B74: ldr        r0, [r5], #4
00646B78: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646B7C: cmp        r6, r5
00646B7E: bne        #0x646b74
00646B80: mov        r0, r4
00646B82: pop.w      {r4, r5, r6, lr}
00646B86: b.w        #0x8cce8c
N8 requests deque dtor

RANGE 00646C6C..00646C92 _ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEED2Ev
00646C6C: push       {r4, r5, r6, lr}
00646C6E: mov        r4, r0
00646C70: blx        #0x4b9aa4 ; _ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEE5clearEv -> 00646C92 size=DA
00646C74: ldrd       r5, r6, [r4, #4]
00646C78: cmp        r5, r6
00646C7A: beq        #0x646c88
00646C7C: ldr        r0, [r5], #4
00646C80: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646C84: cmp        r6, r5
00646C86: bne        #0x646c7c
00646C88: mov        r0, r4
00646C8A: pop.w      {r4, r5, r6, lr}
00646C8E: b.w        #0x8cce9c
I1 ready callback registration

RANGE 00528A5A..00528A80
00528A5A: ldr.w      r6, [r5, #0x260]
00528A5E: add        r5, sp, #0x28
00528A60: add        r0, sp, #0x10
00528A62: mov        r1, r5
00528A64: blx        #0x4a922c ; _ZNSt6__ndk18functionIFvvEEC2ERKS2_ -> 00528C0C size=2A
00528A68: add        r7, sp, #0x10
00528A6A: mov        r0, r6
00528A6C: mov        r1, r7
00528A6E: blx        #0x4a9238 ; _ZN4Anki5Cozmo18NVStorageComponent24AddOneShotOnIdleCallbackENSt6__ndk18functionIFvvEEE -> 00645C20 size=16
00528A72: ldr        r0, [sp, #0x20]
00528A74: cmp        r7, r0
00528A76: beq        #0x528ab8
00528A78: cbz        r0, #0x528abe
00528A7A: ldr        r1, [r0]
00528A7C: ldr        r1, [r1, #0x14]
00528A7E: b          #0x528abc
I2 AddOneShot

RANGE 00645C20..00645C36 _ZN4Anki5Cozmo18NVStorageComponent24AddOneShotOnIdleCallbackENSt6__ndk18functionIFvvEEE
00645C20: push       {r4, lr}
00645C22: mov        r4, r0
00645C24: add.w      r0, r4, #0x110
00645C28: blx        #0x4b9a38 ; _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE12emplace_backIJRS3_EEEvDpOT_ -> 00648034 size=76
00645C2C: mov        r0, r4
00645C2E: pop.w      {r4, lr}
00645C32: b.w        #0x8cce6c
I3 Update idle

RANGE 006456BC..006456F4
006456BC: ldr        r3, [r4, #8]
006456BE: cmp        r3, #2
006456C0: beq        #0x64575a
006456C2: cmp        r3, #1
006456C4: beq        #0x6456f4
006456C6: cmp        r3, #0
006456C8: bne        #0x6457c2
006456CA: mov        r0, r4
006456CC: blx        #0x4b9a14 ; _ZN4Anki5Cozmo18NVStorageComponent14ProcessRequestEv -> 00644FD4 size=628
006456D0: ldr.w      r0, [pc, #0x41c] ; literal[00645AF0]=009F9170
006456D4: ldr.w      r1, [sp, #0x434]
006456D8: add        r0, pc
006456DA: ldr        r0, [r0]
006456DC: ldr        r0, [r0]
006456DE: subs       r0, r0, r1
006456E0: itttt      eq
006456E2: moveq      r0, r4
006456E4: addeq.w    sp, sp, #0x438
006456E8: popeq.w    {r4, r5, r6, r7, r8, lr}
006456EC: beq.w      #0x8cce6c
006456F0: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
I4 ProcessRequest idle gates

RANGE 00644FD4..0064503A _ZN4Anki5Cozmo18NVStorageComponent14ProcessRequestEv
00644FD4: push.w     {r4, r5, r6, r7, r8, sb, lr}
00644FD8: sub.w      sp, sp, #0x450
00644FDC: sub        sp, #4
00644FDE: mov        r4, r0
00644FE0: ldr.w      r0, [pc, #0x560] ; literal[00645544]=009F9864
00644FE4: add        r0, pc
00644FE6: ldr        r0, [r0]
00644FE8: ldr        r0, [r0]
00644FEA: str.w      r0, [sp, #0x450]
00644FEE: ldr.w      r0, [r4, #0x10c]
00644FF2: cmp        r0, #0
00644FF4: beq.w      #0x645488
00644FF8: ldr.w      r1, [r4, #0x108]
00644FFC: movw       r3, #0x4925
00645000: movt       r3, #0x2492
00645004: ldr.w      r0, [r4, #0xfc]
00645008: lsrs       r2, r1, #3
0064500A: umull      r2, r3, r2, r3
0064500E: ldr.w      r2, [r0, r3, lsl #2]
00645012: rsb        r0, r3, r3, lsl #3
00645016: sub.w      r0, r1, r0, lsl #3
0064501A: add.w      r1, r0, r0, lsl #3
0064501E: ldrb.w     r0, [r2, r1, lsl #3]
00645022: add.w      r5, r2, r1, lsl #3
00645026: cmp        r0, #3
00645028: bhi.w      #0x645326
0064502C: mov        r8, r5
0064502E: ldr        sb, [r8, #4]!
00645032: tbh        [pc, r0, lsl #1] ; literal[00645034]=0004F010
00645036: movs       r4, r0
00645038: lsls       r1, r1, #4
I5 ProcessOnIdleCallbacks

RANGE 00645B08..00645BAE _ZN4Anki5Cozmo18NVStorageComponent22ProcessOnIdleCallbacksEv
00645B08: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
00645B0C: sub        sp, #0xc
00645B0E: mov        r4, r0
00645B10: ldr.w      r0, [r4, #0x10c]
00645B14: cbnz       r0, #0x645b1a
00645B16: ldr        r0, [r4, #8]
00645B18: cbz        r0, #0x645b20
00645B1A: add        sp, #0xc
00645B1C: pop.w      {r4, r5, r6, r7, r8, sb, sl, fp, pc}
00645B20: ldr.w      r0, [r4, #0x124]
00645B24: cmp        r0, #0
00645B26: beq        #0x645b1a
00645B28: ldr.w      sb, [pc, #0xac] ; literal[00645BD8]=0059E3C4
00645B2C: movw       fp, #0xc0c1
00645B30: add.w      r5, r4, #0x110
00645B34: addw       sl, pc, #0xa4 ; ADDW[00645BDC]=b'NVStorageComponent.ProcessOnIdleCallbacks.ProcessingCallback'
00645B38: add        sb, pc
00645B3A: movs       r6, #0
00645B3C: mov        r7, sp
00645B3E: movt       fp, #0xc0c0
00645B42: mov.w      r8, #0xaa
00645B46: strd       r6, r6, [sp]
00645B4A: str        r6, [sp, #8]
00645B4C: ldr        r0, [pc, #0xcc] ; literal[00645C1C]=005B58E2
00645B4E: mov        r1, sl
00645B50: mov        r2, r7
00645B52: mov        r3, sb
00645B54: add        r0, pc
00645B56: blx        #0x4a5b84 ; _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D818 size=7C
00645B5A: ldr        r0, [sp]
00645B5C: cbz        r0, #0x645b7c
00645B5E: ldr        r1, [sp, #4]
00645B60: cmp        r1, r0
00645B62: itttt      ne
00645B64: subne.w    r2, r1, #8
00645B68: subne      r2, r2, r0
00645B6A: mvnne      r3, #7
00645B6E: bicne.w    r2, r3, r2
00645B72: itt        ne
00645B74: addne      r1, r1, r2
00645B76: strne      r1, [sp, #4]
00645B78: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00645B7C: ldr.w      r1, [r4, #0x120]
00645B80: ldr.w      r0, [r4, #0x114]
00645B84: umull      r2, r3, r1, fp
00645B88: lsrs       r2, r3, #7
00645B8A: mls        r1, r2, r8, r1
00645B8E: ldr.w      r0, [r0, r2, lsl #2]
00645B92: add.w      r1, r1, r1, lsl #1
00645B96: add.w      r0, r0, r1, lsl #3
00645B9A: blx        #0x4b2a3c ; _ZNKSt6__ndk18functionIFvvEEclEv -> 005BF7BC size=3C
00645B9E: mov        r0, r5
00645BA0: blx        #0x4b9a2c ; _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE9pop_frontEv -> 006486C2 size=68
00645BA4: ldr.w      r0, [r4, #0x124]
00645BA8: cmp        r0, #0
00645BAA: bne        #0x645b46
00645BAC: b          #0x645b1a
I6 callback invoke

RANGE 005BF7BC..005BF7F8 _ZNKSt6__ndk18functionIFvvEEclEv
005BF7BC: ldr        r0, [r0, #0x10]
005BF7BE: cbz        r0, #0x5bf7c6
005BF7C0: ldr        r1, [r0]
005BF7C2: ldr        r1, [r1, #0x18]
005BF7C4: bx         r1
005BF7C6: push       {r7, lr}
005BF7C8: movs       r0, #4
005BF7CA: blx        #0x4a42b8 ; __cxa_allocate_exception IMPORT (resolve packaged dependencies before calling external)
005BF7CE: ldr        r1, [pc, #0x1c] ; literal[005BF7EC]=00A7F1E2
005BF7D0: ldr        r2, [pc, #0x1c] ; literal[005BF7F0]=00A7F1E4
005BF7D2: ldr.w      ip, [pc, #0x20] ; literal[005BF7F4]=00A7F1E6
005BF7D6: add        r1, pc
005BF7D8: add        r2, pc
005BF7DA: add        ip, pc
005BF7DC: ldr        r3, [r1]
005BF7DE: ldr        r1, [r2]
005BF7E0: ldr.w      r2, [ip]
005BF7E4: adds       r3, #8
005BF7E6: str        r3, [r0]
005BF7E8: blx        #0x4a42d0 ; __cxa_throw IMPORT (resolve packaged dependencies before calling external)
I7 callback pop

RANGE 006486C2..0064872A _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE9pop_frontEv
006486C2: push       {r4, lr}
006486C4: mov        r4, r0
006486C6: movw       r2, #0xc0c1
006486CA: ldr        r1, [r4, #0x10]
006486CC: movt       r2, #0xc0c0
006486D0: ldr        r0, [r4, #4]
006486D2: umull      r2, r3, r1, r2
006486D6: lsrs       r2, r3, #7
006486D8: movs       r3, #0xaa
006486DA: mls        r1, r2, r3, r1
006486DE: ldr.w      r0, [r0, r2, lsl #2]
006486E2: add.w      r1, r1, r1, lsl #1
006486E6: add.w      r1, r0, r1, lsl #3
006486EA: ldr        r0, [r1, #0x10]
006486EC: cmp        r1, r0
006486EE: beq        #0x6486f8
006486F0: cbz        r0, #0x6486fe
006486F2: ldr        r1, [r0]
006486F4: ldr        r1, [r1, #0x14]
006486F6: b          #0x6486fc
006486F8: ldr        r1, [r0]
006486FA: ldr        r1, [r1, #0x10]
006486FC: blx        r1
006486FE: ldrd       r0, r1, [r4, #0x10]
00648702: subs       r1, #1
00648704: adds       r0, #1
00648706: cmp.w      r0, #0x154
0064870A: strd       r0, r1, [r4, #0x10]
0064870E: it         lo
00648710: poplo      {r4, pc}
00648712: ldr        r0, [r4, #4]
00648714: ldr        r0, [r0]
00648716: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0064871A: ldr        r0, [r4, #4]
0064871C: ldr        r1, [r4, #0x10]
0064871E: adds       r0, #4
00648720: str        r0, [r4, #4]
00648722: sub.w      r0, r1, #0xaa
00648726: str        r0, [r4, #0x10]
00648728: pop        {r4, pc}

REOPENED ENGINE TARGETS (symbol-size windows may end with inline data; data is not a behavior claim)
local 004E02B2; bounded native string construct

RANGE 004E02B2..004E0308
004E02B2: push       {r4, r5, r6, r7, lr}
004E02B4: sub        sp, #4
004E02B6: mov        r4, r2
004E02B8: mov        r6, r1
004E02BA: mov        r5, r0
004E02BC: cmn.w      r4, #0x10
004E02C0: blo        #0x4e02ca
004E02C2: mov        r0, r5
004E02C4: bl         #0x4e0308
004E02C8: b          #0x4e02dc
004E02CA: cmp        r4, #0xb
004E02CC: bhs        #0x4e02dc
004E02CE: lsls       r0, r4, #1
004E02D0: cmp        r4, #0
004E02D2: strb       r0, [r5], #1
004E02D6: mov        r0, r5
004E02D8: bne        #0x4e02f6
004E02DA: b          #0x4e02fe
004E02DC: add.w      r0, r4, #0x10
004E02E0: bic        r7, r0, #0xf
004E02E4: mov        r0, r7
004E02E6: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
004E02EA: orr        r1, r7, #1
004E02EE: strd       r1, r4, [r5]
004E02F2: str        r0, [r5, #8]
004E02F4: mov        r5, r0
004E02F6: mov        r1, r6
004E02F8: mov        r2, r4
004E02FA: blx        #0x4a42ac ; __aeabi_memcpy IMPORT (resolve packaged dependencies before calling external)
004E02FE: movs       r0, #0
004E0300: strb       r0, [r5, r4]
004E0302: add        sp, #4
004E0304: pop        {r4, r5, r6, r7, pc}
004E0306: movs       r0, r0
local 004E39F8; bounded terminate helper

RANGE 004E39F8..004E3A02
004E39F8: push       {r7, lr}
004E39FA: blx        #0x4a45a0 ; __cxa_begin_catch IMPORT (resolve packaged dependencies before calling external)
004E39FE: blx        #0x4a45d0 ; _ZSt9terminatev IMPORT (resolve packaged dependencies before calling external)
_ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j

RANGE 004E428C..004E437C _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j
004E428C: push.w     {r4, r5, r6, r7, r8, sb, sl, lr}
004E4290: sub        sp, #0x18
004E4292: mov        r4, r0
004E4294: movs       r0, #0
004E4296: str        r4, [sp, #0x10]
004E4298: mov        r5, r1
004E429A: strb.w     r0, [sp, #0xc]
004E429E: mov        sb, r2
004E42A0: ldr        r1, [r4]
004E42A2: ldr        r0, [r1, #-0xc]
004E42A6: add        r0, r4
004E42A8: ldr        r2, [r0, #0x10]
004E42AA: cbnz       r2, #0x4e4328
004E42AC: ldr        r0, [r0, #0x48]
004E42AE: cbz        r0, #0x4e42b6
004E42B0: bl         #0x4e44be
004E42B4: ldr        r1, [r4]
004E42B6: movs       r0, #1
004E42B8: strb.w     r0, [sp, #0xc]
004E42BC: ldr        r0, [r1, #-0xc]
004E42C0: adds       r7, r4, r0
004E42C2: ldr        r6, [r7, #0x4c]
004E42C4: ldr.w      sl, [r7, #4]
004E42C8: ldr.w      r8, [r7, #0x18]
004E42CC: adds       r0, r6, #1
004E42CE: bne        #0x4e42f6
004E42D0: add        r0, sp, #0x14
004E42D2: mov        r1, r7
004E42D4: blx        #0x4a457c ; _ZNKSt6__ndk18ios_base6getlocEv IMPORT (resolve packaged dependencies before calling external)
004E42D8: ldr        r0, [pc, #0x9c] ; literal[004E4378]=00B5A4F2
004E42DA: add        r0, pc
004E42DC: ldr        r1, [r0]
004E42DE: add        r0, sp, #0x14
004E42E0: blx        #0x4a4588 ; _ZNKSt6__ndk16locale9use_facetERNS0_2idE IMPORT (resolve packaged dependencies before calling external)
004E42E4: ldr        r1, [r0]
004E42E6: ldr        r2, [r1, #0x1c]
004E42E8: movs       r1, #0x20
004E42EA: blx        r2
004E42EC: mov        r6, r0
004E42EE: add        r0, sp, #0x14
004E42F0: blx        #0x4a44a4 ; _ZNSt6__ndk16localeD1Ev IMPORT (resolve packaged dependencies before calling external)
004E42F4: str        r6, [r7, #0x4c]
004E42F6: and        r0, sl, #0xb0
004E42FA: add.w      r3, r5, sb
004E42FE: sxtb       r1, r6
004E4300: mov        r2, r3
004E4302: strd       r7, r1, [sp]
004E4306: cmp        r0, #0x20
004E4308: it         ne
004E430A: movne      r2, r5
004E430C: mov        r0, r8
004E430E: mov        r1, r5
004E4310: bl         #0x4e43a2
004E4314: cbnz       r0, #0x4e4328
004E4316: ldr        r0, [r4]
004E4318: ldr        r0, [r0, #-0xc]
004E431C: add        r0, r4
004E431E: ldr        r1, [r0, #0x10]
004E4320: orr        r1, r1, #5
004E4324: blx        #0x4a4594 ; _ZNSt6__ndk18ios_base5clearEj IMPORT (resolve packaged dependencies before calling external)
004E4328: add        r0, sp, #0xc
004E432A: bl         #0x4e4460
004E432E: mov        r0, r4
004E4330: add        sp, #0x18
004E4332: pop.w      {r4, r5, r6, r7, r8, sb, sl, pc}
004E4336: b          #0x4e4346
004E4338: mov        r5, r0
004E433A: b          #0x4e434e
004E433C: mov        r5, r0
004E433E: add        r0, sp, #0x14
004E4340: blx        #0x4a44a4 ; _ZNSt6__ndk16localeD1Ev IMPORT (resolve packaged dependencies before calling external)
004E4344: b          #0x4e4348
004E4346: mov        r5, r0
004E4348: add        r0, sp, #0xc
004E434A: bl         #0x4e4460
004E434E: mov        r0, r5
004E4350: blx        #0x4a45a0 ; __cxa_begin_catch IMPORT (resolve packaged dependencies before calling external)
004E4354: ldr        r0, [r4]
004E4356: ldr        r0, [r0, #-0xc]
004E435A: add        r0, r4
004E435C: blx        #0x4a45ac ; _ZNSt6__ndk18ios_base33__set_badbit_and_consider_rethrowEv IMPORT (resolve packaged dependencies before calling external)
004E4360: blx        #0x4a45b8 ; __cxa_end_catch IMPORT (resolve packaged dependencies before calling external)
004E4364: b          #0x4e432e
004E4366: mov        r4, r0
004E4368: blx        #0x4a45b8 ; __cxa_end_catch IMPORT (resolve packaged dependencies before calling external)
004E436C: mov        r0, r4
004E436E: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
004E4372: bl         #0x4e39f8
004E4376: nop
004E4378: adr        r4, #0x3c8 ; ADR[004E4744]=b'\xbf\xf7\xb2\xed\xa7\xeb\t\x07\xb7\xeb\n\x02\x07\xd0'
004E437A: lsls       r5, r6, #2
local 004E4598; bounded stringbuf string setter

RANGE 004E4598..004E462A
004E4598: push       {r4, r5, r6, lr}
004E459A: mov        r4, r0
004E459C: add.w      r5, r4, #0x20
004E45A0: mov        r0, r5
004E45A2: bl         #0x4e462a
004E45A6: ldr        r0, [r4, #0x30]
004E45A8: movs       r1, #0
004E45AA: str        r1, [r4, #0x2c]
004E45AC: tst.w      r0, #8
004E45B0: beq        #0x4e45d0
004E45B2: ldrb       r2, [r5]
004E45B4: tst.w      r2, #1
004E45B8: bne        #0x4e45c2
004E45BA: adds       r1, r5, #1
004E45BC: add.w      r2, r1, r2, lsr #1
004E45C0: b          #0x4e45c8
004E45C2: ldrd       r2, r1, [r4, #0x24]
004E45C6: add        r2, r1
004E45C8: str        r2, [r4, #0x2c]
004E45CA: strd       r1, r1, [r4, #8]
004E45CE: str        r2, [r4, #0x10]
004E45D0: lsls       r0, r0, #0x1b
004E45D2: it         pl
004E45D4: poppl      {r4, r5, r6, pc}
004E45D6: ldrb       r0, [r5]
004E45D8: tst.w      r0, #1
004E45DC: bne        #0x4e45ec
004E45DE: add.w      r1, r5, r0, lsr #1
004E45E2: lsrs       r6, r0, #1
004E45E4: adds       r1, #1
004E45E6: str        r1, [r4, #0x2c]
004E45E8: movs       r1, #0xa
004E45EA: b          #0x4e45fc
004E45EC: ldrd       r0, r6, [r4, #0x20]
004E45F0: ldr        r1, [r4, #0x28]
004E45F2: bic        r0, r0, #1
004E45F6: add        r1, r6
004E45F8: str        r1, [r4, #0x2c]
004E45FA: subs       r1, r0, #1
004E45FC: mov        r0, r5
004E45FE: movs       r2, #0
004E4600: bl         #0x4e41de
004E4604: ldrb       r0, [r5]
004E4606: tst.w      r0, #1
004E460A: itte       eq
004E460C: lsreq      r1, r0, #1
004E460E: addeq      r0, r5, #1
004E4610: ldrdne     r1, r0, [r4, #0x24]
004E4614: ldrb.w     r2, [r4, #0x30]
004E4618: add        r1, r0
004E461A: strd       r0, r0, [r4, #0x14]
004E461E: str        r1, [r4, #0x1c]
004E4620: lsls       r1, r2, #0x1e
004E4622: itt        ne
004E4624: addne      r0, r0, r6
004E4626: strne      r0, [r4, #0x18]
004E4628: pop        {r4, r5, r6, pc}
local 004E462A; bounded native string assign

RANGE 004E462A..004E4652
004E462A: push       {r4, lr}
004E462C: mov        r4, r0
004E462E: cmp        r4, r1
004E4630: beq        #0x4e464e
004E4632: ldrd       r2, r3, [r1, #4]
004E4636: ldrb.w     ip, [r1]
004E463A: ands       r0, ip, #1
004E463E: mov        r0, r4
004E4640: itt        eq
004E4642: addeq      r3, r1, #1
004E4644: lsreq.w    r2, ip, #1
004E4648: mov        r1, r3
004E464A: bl         #0x4e4652
004E464E: mov        r0, r4
004E4650: pop        {r4, pc}
_ZNKSt6__ndk115basic_stringbufIcNS_11char_traitsIcEENS_9allocatorIcEEE3strEv

RANGE 004E4784..004E47CE _ZNKSt6__ndk115basic_stringbufIcNS_11char_traitsIcEENS_9allocatorIcEEE3strEv
004E4784: push       {r7, lr}
004E4786: ldr        r2, [r1, #0x30]
004E4788: tst.w      r2, #0x10
004E478C: bne        #0x4e479c
004E478E: lsls       r2, r2, #0x1c
004E4790: bmi        #0x4e47b8
004E4792: movs       r1, #0
004E4794: strd       r1, r1, [r0]
004E4798: str        r1, [r0, #8]
004E479A: pop        {r7, pc}
004E479C: ldr        r3, [r1, #0x18]
004E479E: ldr        r2, [r1, #0x2c]
004E47A0: cmp        r2, r3
004E47A2: itt        lo
004E47A4: strlo      r3, [r1, #0x2c]
004E47A6: movlo      r2, r3
004E47A8: ldr        r1, [r1, #0x14]
004E47AA: movs       r3, #0
004E47AC: strd       r3, r3, [r0]
004E47B0: str        r3, [r0, #8]
004E47B2: blx        #0x4a460c ; _ZNSt6__ndk112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEE6__initIPcEENS_9enable_ifIXsr21__is_forward_iteratorIT_EE5valueEvE4typeES9_S9_ -> 004E47CE size=62
004E47B6: pop        {r7, pc}
004E47B8: ldr.w      ip, [r1, #8]
004E47BC: movs       r3, #0
004E47BE: ldr        r2, [r1, #0x10]
004E47C0: strd       r3, r3, [r0]
004E47C4: mov        r1, ip
004E47C6: str        r3, [r0, #8]
004E47C8: blx        #0x4a460c ; _ZNSt6__ndk112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEE6__initIPcEENS_9enable_ifIXsr21__is_forward_iteratorIT_EE5valueEvE4typeES9_S9_ -> 004E47CE size=62
004E47CC: pop        {r7, pc}
local 004E8048; bounded native string append

RANGE 004E8048..004E80B8
004E8048: push       {r4, r5, r6, r7, lr}
004E804A: sub        sp, #0x14
004E804C: mov        r4, r0
004E804E: mov        r5, r2
004E8050: ldrb       r0, [r4]
004E8052: tst.w      r0, #1
004E8056: bne        #0x4e805c
004E8058: movs       r3, #0xa
004E805A: b          #0x4e8064
004E805C: ldr        r0, [r4]
004E805E: bic        r2, r0, #1
004E8062: subs       r3, r2, #1
004E8064: lsls       r2, r0, #0x1f
004E8066: ite        eq
004E8068: ubfxeq     r6, r0, #1, #7
004E806C: ldrne      r6, [r4, #4]
004E806E: subs       r0, r3, r6
004E8070: cmp        r0, r5
004E8072: bhs        #0x4e808e
004E8074: movs       r0, #0
004E8076: strd       r6, r0, [sp]
004E807A: subs       r0, r5, r3
004E807C: adds       r2, r0, r6
004E807E: strd       r5, r1, [sp, #8]
004E8082: mov        r1, r3
004E8084: mov        r0, r4
004E8086: mov        r3, r6
004E8088: bl         #0x4e46be
004E808C: b          #0x4e80b2
004E808E: cbz        r5, #0x4e80b2
004E8090: cmp        r2, #0
004E8092: ite        eq
004E8094: addeq      r7, r4, #1
004E8096: ldrne      r7, [r4, #8]
004E8098: mov        r2, r5
004E809A: adds       r0, r7, r6
004E809C: blx        #0x4a42ac ; __aeabi_memcpy IMPORT (resolve packaged dependencies before calling external)
004E80A0: ldrb       r1, [r4]
004E80A2: adds       r0, r6, r5
004E80A4: lsls       r1, r1, #0x1f
004E80A6: itte       eq
004E80A8: lsleq      r1, r0, #1
004E80AA: strbeq     r1, [r4]
004E80AC: strne      r0, [r4, #4]
004E80AE: movs       r1, #0
004E80B0: strb       r1, [r7, r0]
004E80B2: mov        r0, r4
004E80B4: add        sp, #0x14
004E80B6: pop        {r4, r5, r6, r7, pc}
local 004E80B8; bounded native string insert

RANGE 004E80B8..004E8168
004E80B8: push.w     {r4, r5, r6, r7, r8, sb, sl, lr}
004E80BC: sub        sp, #0x10
004E80BE: mov        r4, r0
004E80C0: mov        r5, r3
004E80C2: ldrb       r0, [r4]
004E80C4: mov        r8, r2
004E80C6: mov        r7, r1
004E80C8: tst.w      r0, #1
004E80CC: ite        eq
004E80CE: lsreq      r6, r0, #1
004E80D0: ldrne      r6, [r4, #4]
004E80D2: cmp        r6, r7
004E80D4: bhs        #0x4e80de
004E80D6: mov        r0, r4
004E80D8: bl         #0x4e8168
004E80DC: ldrb       r0, [r4]
004E80DE: lsls       r1, r0, #0x1f
004E80E0: bne        #0x4e80e6
004E80E2: movs       r1, #0xa
004E80E4: b          #0x4e80ee
004E80E6: ldr        r0, [r4]
004E80E8: bic        r1, r0, #1
004E80EC: subs       r1, #1
004E80EE: subs       r2, r1, r6
004E80F0: cmp        r2, r5
004E80F2: bhs        #0x4e810c
004E80F4: movs       r0, #0
004E80F6: mov        r3, r6
004E80F8: strd       r7, r0, [sp]
004E80FC: adds       r0, r6, r5
004E80FE: subs       r2, r0, r1
004E8100: mov        r0, r4
004E8102: strd       r5, r8, [sp, #8]
004E8106: bl         #0x4e46be
004E810A: b          #0x4e815e
004E810C: cbz        r5, #0x4e815e
004E810E: lsls       r0, r0, #0x1f
004E8110: ite        eq
004E8112: addeq.w    sl, r4, #1
004E8116: ldrne.w    sl, [r4, #8]
004E811A: subs       r2, r6, r7
004E811C: add.w      sb, sl, r7
004E8120: beq        #0x4e8140
004E8122: add.w      r0, sb, r5
004E8126: mov        r1, sb
004E8128: blx        #0x4a4600 ; __aeabi_memmove IMPORT (resolve packaged dependencies before calling external)
004E812C: add.w      r0, sl, r6
004E8130: mov        r1, r8
004E8132: cmp        r0, r8
004E8134: it         hi
004E8136: addhi      r1, r1, r5
004E8138: cmp        sb, r8
004E813A: it         hi
004E813C: movhi      r1, r8
004E813E: b          #0x4e8142
004E8140: mov        r1, r8
004E8142: mov        r0, sb
004E8144: mov        r2, r5
004E8146: blx        #0x4a4600 ; __aeabi_memmove IMPORT (resolve packaged dependencies before calling external)
004E814A: ldrb       r1, [r4]
004E814C: adds       r0, r6, r5
004E814E: lsls       r1, r1, #0x1f
004E8150: itte       eq
004E8152: lsleq      r1, r0, #1
004E8154: strbeq     r1, [r4]
004E8156: strne      r0, [r4, #4]
004E8158: movs       r1, #0
004E815A: strb.w     r1, [sl, r0]
004E815E: mov        r0, r4
004E8160: add        sp, #0x10
004E8162: pop.w      {r4, r5, r6, r7, r8, sb, sl, pc}
004E8166: movs       r0, r0
_ZNSt6__ndk113__vector_baseINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEED2Ev; bounded T13 handle vector

RANGE 004EAE94..004EAEC8 _ZNSt6__ndk113__vector_baseINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEED2Ev
004EAE94: push       {r4, r5, r7, lr}
004EAE96: mov        r4, r0
004EAE98: ldr        r5, [r4]
004EAE9A: cbz        r5, #0x4eaec4
004EAE9C: ldr        r0, [r4, #4]
004EAE9E: cmp        r0, r5
004EAEA0: beq        #0x4eaebe
004EAEA2: sub.w      r1, r0, #8
004EAEA6: str        r1, [r4, #4]
004EAEA8: ldr        r0, [r0, #-0x4]
004EAEAC: cbz        r0, #0x4eaeb6
004EAEAE: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
004EAEB2: ldr        r0, [r4, #4]
004EAEB4: b          #0x4eaeb8
004EAEB6: mov        r0, r1
004EAEB8: cmp        r0, r5
004EAEBA: bne        #0x4eaea2
004EAEBC: ldr        r5, [r4]
004EAEBE: mov        r0, r5
004EAEC0: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
004EAEC4: mov        r0, r4
004EAEC6: pop        {r4, r5, r7, pc}
local 004EF1B8; bounded T19 scoped handle teardown

RANGE 004EF1B8..004EF1D2
004EF1B8: push       {r4, lr}
004EF1BA: mov        r4, r0
004EF1BC: ldr        r0, [r4]
004EF1BE: ldr        r1, [r0]
004EF1C0: ldr        r1, [r1, #8]
004EF1C2: blx        r1
004EF1C4: ldr        r0, [r4]
004EF1C6: cbz        r0, #0x4ef1ce
004EF1C8: ldr        r1, [r0]
004EF1CA: ldr        r1, [r1, #4]
004EF1CC: blx        r1
004EF1CE: mov        r0, r4
004EF1D0: pop        {r4, pc}
_ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEED2Ev

RANGE 005010B4..0050111C _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEED2Ev
005010B4: push       {r4, r5, r7, lr}
005010B6: mov        r4, r0
005010B8: ldr        r0, [pc, #0x58] ; literal[00501114]=00B3D84A
005010BA: add        r0, pc
005010BC: ldr        r0, [r0]
005010BE: adds       r0, #8
005010C0: str        r0, [r4]
005010C2: mov        r0, r4
005010C4: blx        #0x4a69c4 ; _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE5closeEv -> 0050111C size=3C
005010C8: ldrb.w     r0, [r4, #0x60]
005010CC: cbz        r0, #0x5010d6
005010CE: ldr        r0, [r4, #0x20]
005010D0: cbz        r0, #0x5010d6
005010D2: blx        #0x4a69d0 ; _ZdaPv IMPORT (resolve packaged dependencies before calling external)
005010D6: ldrb.w     r0, [r4, #0x61]
005010DA: cbz        r0, #0x5010e4
005010DC: ldr        r0, [r4, #0x38]
005010DE: cbz        r0, #0x5010e4
005010E0: blx        #0x4a69d0 ; _ZdaPv IMPORT (resolve packaged dependencies before calling external)
005010E4: ldr        r0, [pc, #0x30] ; literal[00501118]=00B3D6DA
005010E6: add        r0, pc
005010E8: ldr        r0, [r0]
005010EA: add.w      r1, r0, #8
005010EE: mov        r0, r4
005010F0: str        r1, [r0], #4
005010F4: blx        #0x4a44a4 ; _ZNSt6__ndk16localeD1Ev IMPORT (resolve packaged dependencies before calling external)
005010F8: mov        r0, r4
005010FA: pop        {r4, r5, r7, pc}
005010FC: blx        #0x4a45a0 ; __cxa_begin_catch IMPORT (resolve packaged dependencies before calling external)
00501100: blx        #0x4a45b8 ; __cxa_end_catch IMPORT (resolve packaged dependencies before calling external)
00501104: b          #0x5010c8
00501106: mov        r5, r0
00501108: mov        r0, r4
0050110A: bl         #0x4e405c
0050110E: mov        r0, r5
00501110: bl         #0x4e39f8
00501114: bhi        #0x5011ac
00501116: lsls       r3, r6, #2
00501118: bvs        #0x5010d0
0050111A: lsls       r3, r6, #2
_ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE5closeEv; bounded T23 logger close

RANGE 0050111C..00501158 _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE5closeEv
0050111C: push       {r4, r5, r6, lr}
0050111E: mov        r4, r0
00501120: ldr        r5, [r4, #0x40]
00501122: cbz        r5, #0x50113c
00501124: ldr        r0, [r4]
00501126: ldr        r1, [r0, #0x18]
00501128: mov        r0, r4
0050112A: blx        r1
0050112C: mov        r6, r0
0050112E: mov        r0, r5
00501130: blx        #0x4a69dc ; fclose IMPORT (resolve packaged dependencies before calling external)
00501134: mov        r1, r0
00501136: movs       r0, #0
00501138: cbz        r1, #0x501140
0050113A: pop        {r4, r5, r6, pc}
0050113C: movs       r0, #0
0050113E: pop        {r4, r5, r6, pc}
00501140: str        r0, [r4, #0x40]
00501142: cmp        r6, #0
00501144: it         eq
00501146: moveq      r0, r4
00501148: pop        {r4, r5, r6, pc}
0050114A: mov        r4, r0
0050114C: mov        r0, r5
0050114E: blx        #0x4a69dc ; fclose IMPORT (resolve packaged dependencies before calling external)
00501152: mov        r0, r4
00501154: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
_ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE12__write_modeEv

RANGE 005017F8..00501830 _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE12__write_modeEv
005017F8: ldrb.w     r1, [r0, #0x5c]
005017FC: lsls       r1, r1, #0x1b
005017FE: it         mi
00501800: bxmi       lr
00501802: ldr        r2, [r0, #0x34]
00501804: movs       r1, #0
00501806: movs       r3, #0
00501808: strd       r1, r1, [r0, #8]
0050180C: cmp        r2, #9
0050180E: str        r1, [r0, #0x10]
00501810: blo        #0x501824
00501812: ldrb.w     r1, [r0, #0x62]
00501816: cmp        r1, #0
00501818: ite        eq
0050181A: ldrdeq     r1, r2, [r0, #0x38]
0050181E: ldrne      r1, [r0, #0x20]
00501820: add        r2, r1
00501822: subs       r3, r2, #1
00501824: strd       r1, r1, [r0, #0x14]
00501828: movs       r1, #0x10
0050182A: str        r3, [r0, #0x1c]
0050182C: str        r1, [r0, #0x5c]
0050182E: bx         lr
_ZN4Anki5Cozmo14PathDolerOuter9ClearPathEv

RANGE 005082B0..005082C8 _ZN4Anki5Cozmo14PathDolerOuter9ClearPathEv
005082B0: push       {r4, lr}
005082B2: mov        r4, r0
005082B4: blx        #0x4a5848 ; _ZN4Anki8Planning4Path5ClearEv -> 0085C748 size=6
005082B8: movw       r0, #0xffff
005082BC: strh.w     r0, [r4, #0xfc]
005082C0: movs       r0, #0
005082C2: str.w      r0, [r4, #0xf8]
005082C6: pop        {r4, pc}
_ZNSt6__ndk1plIcNS_11char_traitsIcEENS_9allocatorIcEEEENS_12basic_stringIT_T0_T1_EEPKS6_RKS9_

RANGE 0050B190..0050B1EC _ZNSt6__ndk1plIcNS_11char_traitsIcEENS_9allocatorIcEEEENS_12basic_stringIT_T0_T1_EEPKS6_RKS9_
0050B190: push       {r4, r5, r6, r7, lr}
0050B192: sub        sp, #4
0050B194: mov        r4, r0
0050B196: movs       r0, #0
0050B198: mov        r7, r1
0050B19A: strd       r0, r0, [r4]
0050B19E: str        r0, [r4, #8]
0050B1A0: mov        r0, r7
0050B1A2: mov        r5, r2
0050B1A4: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0050B1A8: mov        r2, r0
0050B1AA: ldrb       r0, [r5]
0050B1AC: ldr        r6, [r5, #4]
0050B1AE: tst.w      r0, #1
0050B1B2: it         eq
0050B1B4: lsreq      r6, r0, #1
0050B1B6: adds       r3, r6, r2
0050B1B8: mov        r0, r4
0050B1BA: mov        r1, r7
0050B1BC: blx        #0x4a733c ; _ZNSt6__ndk112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEE6__initEPKcjj -> 0050B792 size=52
0050B1C0: ldrb       r0, [r5]
0050B1C2: ldr        r1, [r5, #8]
0050B1C4: tst.w      r0, #1
0050B1C8: it         eq
0050B1CA: addeq      r1, r5, #1
0050B1CC: mov        r0, r4
0050B1CE: mov        r2, r6
0050B1D0: bl         #0x4e8048
0050B1D4: add        sp, #4
0050B1D6: pop        {r4, r5, r6, r7, pc}
0050B1D8: mov        r5, r0
0050B1DA: ldrb       r0, [r4]
0050B1DC: lsls       r0, r0, #0x1f
0050B1DE: itt        ne
0050B1E0: ldrne      r0, [r4, #8]
0050B1E2: blxne      #0x4a40cc
0050B1E6: mov        r0, r5
0050B1E8: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
_ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb

RANGE 0051349C..00513598 _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb
0051349C: push       {r4, r5, r6, r7, lr}
0051349E: sub        sp, #0x1c
005134A0: mov        r7, r0
005134A2: mov        r4, r2
005134A4: ldr        r0, [r7]
005134A6: mov        r6, r1
005134A8: ldr        r1, [r7, #0x10]
005134AA: ldr        r0, [r0, #0x20]
005134AC: ldr        r0, [r0, #0x60]
005134AE: ldr        r2, [r0]
005134B0: ldr        r5, [r2, #0x10]
005134B2: mov        r2, r6
005134B4: str        r3, [sp]
005134B6: mov        r3, r4
005134B8: blx        r5
005134BA: mov        r4, r0
005134BC: cbz        r4, #0x513522
005134BE: ldrb       r0, [r6]
005134C0: blx        #0x4a7cc0 ; _ZN4Anki5Cozmo14RobotInterface24EngineToRobotTagToStringENS1_16EngineToRobotTagE -> 007AF8D0 size=24
005134C4: mov        r6, r0
005134C6: adr        r0, #0x88 ; ADR[00513550]=b'$data'
005134C8: str        r6, [sp, #0xc]
005134CA: str        r0, [sp, #8]
005134CC: movs       r0, #0
005134CE: str        r0, [sp, #0x18]
005134D0: strd       r0, r0, [sp, #0x10]
005134D4: movs       r0, #8
005134D6: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
005134DA: ldr        r2, [sp, #0xc]
005134DC: add.w      r3, r0, #8
005134E0: ldr        r1, [sp, #8]
005134E2: str        r0, [sp, #0x10]
005134E4: str        r0, [sp, #0x14]
005134E6: str        r3, [sp, #0x18]
005134E8: strd       r1, r2, [r0]
005134EC: ldr        r0, [sp, #0x14]
005134EE: adds       r0, #8
005134F0: str        r0, [sp, #0x14]
005134F2: ldr        r3, [r7, #0x10]
005134F4: adr        r0, #0x60 ; ADR[00513558]=b'Robot.SendMessage'
005134F6: add        r1, sp, #0x10
005134F8: adr        r2, #0x70 ; ADR[0051356C]=b'Robot %d failed to send a message type %s'
005134FA: str        r6, [sp]
005134FC: blx        #0x4a4540 ; _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D2B4 size=80
00513500: ldr        r0, [sp, #0x10]
00513502: cbz        r0, #0x513522
00513504: ldr        r1, [sp, #0x14]
00513506: cmp        r1, r0
00513508: itttt      ne
0051350A: subne.w    r2, r1, #8
0051350E: subne      r2, r2, r0
00513510: mvnne      r3, #7
00513514: bicne.w    r2, r3, r2
00513518: itt        ne
0051351A: addne      r1, r1, r2
0051351C: strne      r1, [sp, #0x14]
0051351E: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00513522: mov        r0, r4
00513524: add        sp, #0x1c
00513526: pop        {r4, r5, r6, r7, pc}
00513528: mov        r4, r0
0051352A: ldr        r0, [sp, #0x10]
0051352C: cbz        r0, #0x51354a
0051352E: ldr        r1, [sp, #0x14]
00513530: cmp        r1, r0
00513532: beq        #0x513546
00513534: sub.w      r2, r1, #8
00513538: mvn        r3, #7
0051353C: subs       r2, r2, r0
0051353E: bic.w      r2, r3, r2
00513542: add        r1, r2
00513544: str        r1, [sp, #0x14]
00513546: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0051354A: mov        r0, r4
0051354C: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
00513550: str        r4, [r4, #0x40]
00513552: strb       r1, [r4, #0x11]
00513554: lsls       r1, r4, #1
00513556: movs       r0, r0
00513558: ldr        r2, [r2, #0x74]
0051355A: ldr        r2, [r4, #0x74]
0051355C: cmp        r6, #0x74
0051355E: str        r3, [r2, #0x54]
00513560: str        r6, [r5, #0x44]
00513562: str        r5, [r1, #0x54]
00513564: strb       r3, [r6, #0xd]
00513566: str        r1, [r4, #0x74]
00513568: lsls       r5, r4, #1
0051356A: movs       r0, r0
0051356C: ldr        r2, [r2, #0x74]
0051356E: ldr        r2, [r4, #0x74]
00513570: movs       r0, #0x74
00513572: str        r5, [r4, #0x40]
00513574: str        r0, [r4, #0x60]
00513576: ldr        r1, [r4, #0x14]
00513578: str        r4, [r5, #0x54]
0051357A: movs       r0, #0x64
0051357C: ldr        r4, [r6, #0x74]
0051357E: strb       r0, [r4, #0xc]
00513580: ldr        r5, [r4, #0x64]
00513582: movs       r0, #0x64
00513584: movs       r0, #0x61
00513586: str        r5, [r5, #0x54]
00513588: strb       r3, [r6, #0xd]
0051358A: str        r1, [r4, #0x74]
0051358C: movs       r0, #0x65
0051358E: ldrb       r4, [r6, #5]
00513590: str        r0, [r6, #0x54]
00513592: movs       r5, #0x20
00513594: lsls       r3, r6, #1
00513596: movs       r0, r0
_ZNK4Anki5Cozmo5Robot13GetLiftHeightEv

RANGE 00516F64..00516F9C _ZNK4Anki5Cozmo5Robot13GetLiftHeightEv
00516F64: push       {r7, lr}
00516F66: ldr.w      r0, [r0, #0x300]
00516F6A: blx        #0x4a4168 ; sinf IMPORT (resolve packaged dependencies before calling external)
00516F6E: vldr       s0, [pc, #0x20] ; literal[00516F90]=42840000
00516F72: vmov       s2, r0
00516F76: vmul.f32   s0, s2, s0
00516F7A: vldr       s2, [pc, #0x18] ; literal[00516F94]=42340000
00516F7E: vadd.f32   s0, s0, s2
00516F82: vldr       s2, [pc, #0x14] ; literal[00516F98]=00000000
00516F86: vadd.f32   s0, s0, s2
00516F8A: vmov       r0, s0
00516F8E: pop        {r7, pc}
00516F90: movs       r0, r0
00516F92: cmp        r4, r0
00516F94: movs       r0, r0
00516F96: tst        r4, r6
00516F98: movs       r0, r0
00516F9A: movs       r0, r0
_ZN4Anki5Cozmo5Robot30ConvertLiftAngleToLiftHeightMMEf

RANGE 00516F9C..00516FD0 _ZN4Anki5Cozmo5Robot30ConvertLiftAngleToLiftHeightMMEf
00516F9C: push       {r7, lr}
00516F9E: blx        #0x4a4168 ; sinf IMPORT (resolve packaged dependencies before calling external)
00516FA2: vldr       s0, [pc, #0x20] ; literal[00516FC4]=42840000
00516FA6: vmov       s2, r0
00516FAA: vmul.f32   s0, s2, s0
00516FAE: vldr       s2, [pc, #0x18] ; literal[00516FC8]=42340000
00516FB2: vadd.f32   s0, s0, s2
00516FB6: vldr       s2, [pc, #0x14] ; literal[00516FCC]=00000000
00516FBA: vadd.f32   s0, s0, s2
00516FBE: vmov       r0, s0
00516FC2: pop        {r7, pc}
00516FC4: movs       r0, r0
00516FC6: cmp        r4, r0
00516FC8: movs       r0, r0
00516FCA: tst        r4, r6
00516FCC: movs       r0, r0
00516FCE: movs       r0, r0
_ZN4Anki5Cozmo5Robot31ConvertLiftHeightToLiftAngleRadEf; bounded L10 lift conversion

RANGE 005170B0..005170F4 _ZN4Anki5Cozmo5Robot31ConvertLiftHeightToLiftAngleRadEf
005170B0: vldr       s0, [pc, #0x40] ; literal[005170F4]=42000000
005170B4: vmov       s2, r0
005170B8: vldr       s4, [pc, #0x40] ; literal[005170FC]=42840000
005170BC: vcmpe.f32  s2, s0
005170C0: vldr       s6, [pc, #0x40] ; literal[00517104]=3F364D93
005170C4: vmrs       apsr_nzcv, fpscr
005170C8: it         gt
005170CA: vmovgt.f32 s0, s2
005170CE: vldr       s2, [pc, #0x28] ; literal[005170F8]=C2340000
005170D2: vadd.f32   s2, s0, s2
005170D6: vdiv.f32   s2, s2, s4
005170DA: vldr       s4, [pc, #0x24] ; literal[00517100]=42B80000
005170DE: vcmpe.f32  s0, s4
005170E2: vmrs       apsr_nzcv, fpscr
005170E6: it         mi
005170E8: vmovmi.f32 s6, s2
005170EC: vmov       r0, s6
005170F0: b.w        #0x8cae7c
_ZNSt6__ndk113basic_ostreamIcNS_11char_traitsIcEEElsEj

RANGE 0051778C..00517898 _ZNSt6__ndk113basic_ostreamIcNS_11char_traitsIcEEElsEj
0051778C: push.w     {r4, r5, r6, r7, r8, sb, sl, lr}
00517790: sub        sp, #0x10
00517792: mov        r4, r0
00517794: movs       r0, #0
00517796: str        r4, [sp, #8]
00517798: mov        sb, r1
0051779A: strb.w     r0, [sp, #4]
0051779E: ldr        r1, [r4]
005177A0: ldr        r0, [r1, #-0xc]
005177A4: add        r0, r4
005177A6: ldr        r2, [r0, #0x10]
005177A8: cmp        r2, #0
005177AA: bne        #0x51783c
005177AC: ldr        r0, [r0, #0x48]
005177AE: cbz        r0, #0x5177b6
005177B0: bl         #0x4e44be
005177B4: ldr        r1, [r4]
005177B6: movs       r0, #1
005177B8: strb.w     r0, [sp, #4]
005177BC: ldr        r0, [r1, #-0xc]
005177C0: adds       r1, r4, r0
005177C2: add        r0, sp, #0xc
005177C4: blx        #0x4a457c ; _ZNKSt6__ndk18ios_base6getlocEv IMPORT (resolve packaged dependencies before calling external)
005177C8: ldr        r0, [pc, #0xc4] ; literal[00517890]=00B26FFE
005177CA: add        r0, pc
005177CC: ldr        r1, [r0]
005177CE: add        r0, sp, #0xc
005177D0: blx        #0x4a4588 ; _ZNKSt6__ndk16locale9use_facetERNS0_2idE IMPORT (resolve packaged dependencies before calling external)
005177D4: mov        sl, r0
005177D6: add        r0, sp, #0xc
005177D8: blx        #0x4a44a4 ; _ZNSt6__ndk16localeD1Ev IMPORT (resolve packaged dependencies before calling external)
005177DC: ldr        r0, [r4]
005177DE: ldr        r0, [r0, #-0xc]
005177E2: adds       r7, r4, r0
005177E4: ldr        r5, [r7, #0x4c]
005177E6: ldr.w      r8, [r7, #0x18]
005177EA: adds       r0, r5, #1
005177EC: bne        #0x517814
005177EE: add        r0, sp, #0xc
005177F0: mov        r1, r7
005177F2: blx        #0x4a457c ; _ZNKSt6__ndk18ios_base6getlocEv IMPORT (resolve packaged dependencies before calling external)
005177F6: ldr        r0, [pc, #0x9c] ; literal[00517894]=00B26FD4
005177F8: add        r0, pc
005177FA: ldr        r1, [r0]
005177FC: add        r0, sp, #0xc
005177FE: blx        #0x4a4588 ; _ZNKSt6__ndk16locale9use_facetERNS0_2idE IMPORT (resolve packaged dependencies before calling external)
00517802: ldr        r1, [r0]
00517804: ldr        r2, [r1, #0x1c]
00517806: movs       r1, #0x20
00517808: blx        r2
0051780A: mov        r5, r0
0051780C: add        r0, sp, #0xc
0051780E: blx        #0x4a44a4 ; _ZNSt6__ndk16localeD1Ev IMPORT (resolve packaged dependencies before calling external)
00517812: str        r5, [r7, #0x4c]
00517814: ldr.w      r0, [sl]
00517818: ldr        r6, [r0, #0x18]
0051781A: sxtb       r3, r5
0051781C: mov        r0, sl
0051781E: mov        r1, r8
00517820: mov        r2, r7
00517822: str.w      sb, [sp]
00517826: blx        r6
00517828: cbnz       r0, #0x51783c
0051782A: ldr        r0, [r4]
0051782C: ldr        r0, [r0, #-0xc]
00517830: add        r0, r4
00517832: ldr        r1, [r0, #0x10]
00517834: orr        r1, r1, #5
00517838: blx        #0x4a4594 ; _ZNSt6__ndk18ios_base5clearEj IMPORT (resolve packaged dependencies before calling external)
0051783C: add        r0, sp, #4
0051783E: bl         #0x4e4460
00517842: mov        r0, r4
00517844: add        sp, #0x10
00517846: pop.w      {r4, r5, r6, r7, r8, sb, sl, pc}
0051784A: b          #0x51785e
0051784C: mov        r5, r0
0051784E: b          #0x517866
00517850: b          #0x517852
00517852: mov        r5, r0
00517854: add        r0, sp, #0xc
00517856: blx        #0x4a44a4 ; _ZNSt6__ndk16localeD1Ev IMPORT (resolve packaged dependencies before calling external)
0051785A: b          #0x517860
0051785C: b          #0x51785e
0051785E: mov        r5, r0
00517860: add        r0, sp, #4
00517862: bl         #0x4e4460
00517866: mov        r0, r5
00517868: blx        #0x4a45a0 ; __cxa_begin_catch IMPORT (resolve packaged dependencies before calling external)
0051786C: ldr        r0, [r4]
0051786E: ldr        r0, [r0, #-0xc]
00517872: add        r0, r4
00517874: blx        #0x4a45ac ; _ZNSt6__ndk18ios_base33__set_badbit_and_consider_rethrowEv IMPORT (resolve packaged dependencies before calling external)
00517878: blx        #0x4a45b8 ; __cxa_end_catch IMPORT (resolve packaged dependencies before calling external)
0051787C: b          #0x517842
0051787E: mov        r4, r0
00517880: blx        #0x4a45b8 ; __cxa_end_catch IMPORT (resolve packaged dependencies before calling external)
00517884: mov        r0, r4
00517886: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
0051788A: bl         #0x4e39f8
0051788E: nop
00517890: ldr        r6, [r7, #0x7c]
00517892: lsls       r2, r6, #2
00517894: ldr        r4, [r2, #0x7c]
00517896: lsls       r2, r6, #2
_ZN4Anki5Cozmo5Robot18SendAbortAnimationEv; bounded A6 animation abort

RANGE 00517DE4..00517E32 _ZN4Anki5Cozmo5Robot18SendAbortAnimationEv
00517DE4: push       {r4, r5, r7, lr}
00517DE6: sub.w      sp, sp, #0x410
00517DEA: mov        r4, r0
00517DEC: ldr        r0, [pc, #0x58] ; literal[00517E48]=00B26A56
00517DEE: add        r5, sp, #4
00517DF0: mov        r1, sp
00517DF2: add        r0, pc
00517DF4: ldr        r0, [r0]
00517DF6: ldr        r0, [r0]
00517DF8: str.w      r0, [sp, #0x40c]
00517DFC: mov        r0, r5
00517DFE: blx        #0x4a8158 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_14AbortAnimationE -> 007AA392 size=6
00517E02: mov        r0, r4
00517E04: mov        r1, r5
00517E06: movs       r2, #1
00517E08: movs       r3, #0
00517E0A: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
00517E0E: mov        r4, r0
00517E10: add        r0, sp, #4
00517E12: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
00517E16: ldr        r0, [pc, #0x34] ; literal[00517E4C]=00B26A2C
00517E18: ldr.w      r1, [sp, #0x40c]
00517E1C: add        r0, pc
00517E1E: ldr        r0, [r0]
00517E20: ldr        r0, [r0]
00517E22: subs       r0, r0, r1
00517E24: ittt       eq
00517E26: moveq      r0, r4
00517E28: addeq.w    sp, sp, #0x410
00517E2C: popeq      {r4, r5, r7, pc}
00517E2E: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
_ZN4Anki5Cozmo5Robot22GetRobotMessageHandlerEv

RANGE 005182EA..005182F8 _ZN4Anki5Cozmo5Robot22GetRobotMessageHandlerEv
005182EA: ldr        r0, [r0]
005182EC: ldr        r0, [r0, #0x20]
005182EE: cmp        r0, #0
005182F0: ite        ne
005182F2: ldrne      r0, [r0, #0x60]
005182F4: moveq      r0, #0
005182F6: bx         lr
_ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv; bounded T14 handle list

RANGE 005194A8..005194E2 _ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv
005194A8: push       {r4, r5, r6, lr}
005194AA: mov        r4, r0
005194AC: ldr        r0, [r4, #8]
005194AE: cbz        r0, #0x5194e0
005194B0: ldrd       r0, r5, [r4]
005194B4: cmp        r5, r4
005194B6: ldr        r1, [r5]
005194B8: ldr        r2, [r0, #4]
005194BA: str        r2, [r1, #4]
005194BC: ldr        r0, [r0, #4]
005194BE: ldr        r1, [r5]
005194C0: str        r1, [r0]
005194C2: mov.w      r0, #0
005194C6: str        r0, [r4, #8]
005194C8: beq        #0x5194e0
005194CA: ldr        r0, [r5, #0xc]
005194CC: ldr        r6, [r5, #4]
005194CE: cbz        r0, #0x5194d4
005194D0: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
005194D4: mov        r0, r5
005194D6: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005194DA: cmp        r6, r4
005194DC: mov        r5, r6
005194DE: bne        #0x5194ca
005194E0: pop        {r4, r5, r6, pc}
_ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeLightComponent10ObjectInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE; bounded T16 cube lights tree

RANGE 005194E2..00519522 _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeLightComponent10ObjectInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
005194E2: push       {r4, r5, r7, lr}
005194E4: mov        r4, r1
005194E6: mov        r5, r0
005194E8: cbz        r4, #0x519520
005194EA: ldr        r1, [r4]
005194EC: mov        r0, r5
005194EE: blx        #0x4a7918 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeLightComponent10ObjectInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 005194E2 size=40
005194F2: ldr        r1, [r4, #4]
005194F4: mov        r0, r5
005194F6: blx        #0x4a7918 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeLightComponent10ObjectInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 005194E2 size=40
005194FA: movs       r5, #0x5c
005194FC: adds       r0, r4, r5
005194FE: blx        #0x4a823c ; _ZNSt6__ndk110__list_impIN4Anki5Cozmo12LightPatternENS_9allocatorIS3_EEE5clearEv -> 00519522 size=3E
00519502: subs       r5, #0xc
00519504: cmp        r5, #0x38
00519506: bne        #0x5194fc
00519508: movs       r5, #0x38
0051950A: adds       r0, r4, r5
0051950C: blx        #0x4a8248 ; _ZNSt6__ndk110__list_impIN4Anki5Cozmo18CubeLightComponent15CurrentAnimInfoENS_9allocatorIS4_EEE5clearEv -> 00519560 size=56
00519510: subs       r5, #0xc
00519512: cmp        r5, #0x14
00519514: bne        #0x51950a
00519516: mov        r0, r4
00519518: pop.w      {r4, r5, r7, lr}
0051951C: b.w        #0x8ca88c
00519520: pop        {r4, r5, r7, pc}
_ZNSt6__ndk110__list_impIN4Anki5Cozmo12LightPatternENS_9allocatorIS3_EEE5clearEv; bounded T25 light patterns

RANGE 00519522..00519560 _ZNSt6__ndk110__list_impIN4Anki5Cozmo12LightPatternENS_9allocatorIS3_EEE5clearEv
00519522: push       {r4, r5, r6, lr}
00519524: mov        r4, r0
00519526: ldr        r0, [r4, #8]
00519528: cbz        r0, #0x51955e
0051952A: ldrd       r0, r5, [r4]
0051952E: cmp        r5, r4
00519530: ldr        r1, [r5]
00519532: ldr        r2, [r0, #4]
00519534: str        r2, [r1, #4]
00519536: ldr        r0, [r0, #4]
00519538: ldr        r1, [r5]
0051953A: str        r1, [r0]
0051953C: mov.w      r0, #0
00519540: str        r0, [r4, #8]
00519542: beq        #0x51955e
00519544: ldrb       r0, [r5, #8]
00519546: ldr        r6, [r5, #4]
00519548: lsls       r0, r0, #0x1f
0051954A: itt        ne
0051954C: ldrne      r0, [r5, #0x10]
0051954E: blxne      #0x4a40cc
00519552: mov        r0, r5
00519554: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00519558: cmp        r6, r4
0051955A: mov        r5, r6
0051955C: bne        #0x519544
0051955E: pop        {r4, r5, r6, pc}
_ZNSt6__ndk110__list_impIN4Anki5Cozmo18CubeLightComponent15CurrentAnimInfoENS_9allocatorIS4_EEE5clearEv; bounded T24 cube animation lists

RANGE 00519560..005195B6 _ZNSt6__ndk110__list_impIN4Anki5Cozmo18CubeLightComponent15CurrentAnimInfoENS_9allocatorIS4_EEE5clearEv
00519560: push       {r4, r5, r6, lr}
00519562: mov        r4, r0
00519564: ldr        r0, [r4, #8]
00519566: cbz        r0, #0x5195b4
00519568: ldrd       r0, r5, [r4]
0051956C: cmp        r5, r4
0051956E: ldr        r1, [r5]
00519570: ldr        r2, [r0, #4]
00519572: str        r2, [r1, #4]
00519574: ldr        r0, [r0, #4]
00519576: ldr        r1, [r5]
00519578: str        r1, [r0]
0051957A: mov.w      r0, #0
0051957E: str        r0, [r4, #8]
00519580: beq        #0x5195b4
00519582: ldr        r6, [r5, #4]
00519584: add.w      r1, r5, #0x28
00519588: ldr        r0, [r5, #0x38]
0051958A: cmp        r1, r0
0051958C: beq        #0x519596
0051958E: cbz        r0, #0x51959c
00519590: ldr        r1, [r0]
00519592: ldr        r1, [r1, #0x14]
00519594: b          #0x51959a
00519596: ldr        r1, [r0]
00519598: ldr        r1, [r1, #0x10]
0051959A: blx        r1
0051959C: ldrb       r0, [r5, #8]
0051959E: lsls       r0, r0, #0x1f
005195A0: itt        ne
005195A2: ldrne      r0, [r5, #0x10]
005195A4: blxne      #0x4a40cc
005195A8: mov        r0, r5
005195AA: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005195AE: cmp        r6, r4
005195B0: mov        r5, r6
005195B2: bne        #0x519582
005195B4: pop        {r4, r5, r6, pc}
_ZNSt6__ndk16__treeINS_12__value_typeIiNS_4listINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS7_EEEEEENS_19__map_value_compareIiSB_NS_4lessIiEELb1EEENS8_ISB_EEE7destroyEPNS_11__tree_nodeISB_PvEE; bounded T15 backpack tree

RANGE 005195B6..005195E2 _ZNSt6__ndk16__treeINS_12__value_typeIiNS_4listINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS7_EEEEEENS_19__map_value_compareIiSB_NS_4lessIiEELb1EEENS8_ISB_EEE7destroyEPNS_11__tree_nodeISB_PvEE
005195B6: push       {r4, r5, r7, lr}
005195B8: mov        r4, r1
005195BA: mov        r5, r0
005195BC: cbz        r4, #0x5195e0
005195BE: ldr        r1, [r4]
005195C0: mov        r0, r5
005195C2: blx        #0x4a7900 ; _ZNSt6__ndk16__treeINS_12__value_typeIiNS_4listINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS7_EEEEEENS_19__map_value_compareIiSB_NS_4lessIiEELb1EEENS8_ISB_EEE7destroyEPNS_11__tree_nodeISB_PvEE -> 005195B6 size=2C
005195C6: ldr        r1, [r4, #4]
005195C8: mov        r0, r5
005195CA: blx        #0x4a7900 ; _ZNSt6__ndk16__treeINS_12__value_typeIiNS_4listINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS7_EEEEEENS_19__map_value_compareIiSB_NS_4lessIiEELb1EEENS8_ISB_EEE7destroyEPNS_11__tree_nodeISB_PvEE -> 005195B6 size=2C
005195CE: add.w      r0, r4, #0x14
005195D2: blx        #0x4a8254 ; _ZNSt6__ndk110__list_impINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS5_EEE5clearEv -> 005195E2 size=3A
005195D6: mov        r0, r4
005195D8: pop.w      {r4, r5, r7, lr}
005195DC: b.w        #0x8ca88c
005195E0: pop        {r4, r5, r7, pc}
_ZNSt6__ndk110__list_impINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS5_EEE5clearEv

RANGE 005195E2..0051961C _ZNSt6__ndk110__list_impINS_10shared_ptrIN4Anki5Cozmo17BackpackLightDataEEENS_9allocatorIS5_EEE5clearEv
005195E2: push       {r4, r5, r6, lr}
005195E4: mov        r4, r0
005195E6: ldr        r0, [r4, #8]
005195E8: cbz        r0, #0x51961a
005195EA: ldrd       r0, r5, [r4]
005195EE: cmp        r5, r4
005195F0: ldr        r1, [r5]
005195F2: ldr        r2, [r0, #4]
005195F4: str        r2, [r1, #4]
005195F6: ldr        r0, [r0, #4]
005195F8: ldr        r1, [r5]
005195FA: str        r1, [r0]
005195FC: mov.w      r0, #0
00519600: str        r0, [r4, #8]
00519602: beq        #0x51961a
00519604: ldr        r0, [r5, #0xc]
00519606: ldr        r6, [r5, #4]
00519608: cbz        r0, #0x51960e
0051960A: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
0051960E: mov        r0, r5
00519610: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00519614: cmp        r6, r4
00519616: mov        r5, r6
00519618: bne        #0x519604
0051961A: pop        {r4, r5, r6, pc}
_ZNSt6__ndk110__list_impIN4Anki5Cozmo12ObjectTappedENS_9allocatorIS3_EEE5clearEv

RANGE 005196B4..005196E4 _ZNSt6__ndk110__list_impIN4Anki5Cozmo12ObjectTappedENS_9allocatorIS3_EEE5clearEv
005196B4: push       {r4, r5, r7, lr}
005196B6: mov        r4, r0
005196B8: ldr        r0, [r4, #8]
005196BA: cbz        r0, #0x5196e2
005196BC: ldrd       r1, r0, [r4]
005196C0: cmp        r0, r4
005196C2: ldr        r2, [r0]
005196C4: ldr        r3, [r1, #4]
005196C6: str        r3, [r2, #4]
005196C8: ldr        r1, [r1, #4]
005196CA: ldr        r2, [r0]
005196CC: str        r2, [r1]
005196CE: mov.w      r1, #0
005196D2: str        r1, [r4, #8]
005196D4: beq        #0x5196e2
005196D6: ldr        r5, [r0, #4]
005196D8: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005196DC: cmp        r5, r4
005196DE: mov        r0, r5
005196E0: bne        #0x5196d6
005196E2: pop        {r4, r5, r7, pc}
_ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo23BlockTapFilterComponent13DoubleTapInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE

RANGE 005196E4..00519708 _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo23BlockTapFilterComponent13DoubleTapInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
005196E4: push       {r4, r5, r7, lr}
005196E6: mov        r4, r1
005196E8: mov        r5, r0
005196EA: cbz        r4, #0x519706
005196EC: ldr        r1, [r4]
005196EE: mov        r0, r5
005196F0: blx        #0x4a7aa4 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo23BlockTapFilterComponent13DoubleTapInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 005196E4 size=24
005196F4: ldr        r1, [r4, #4]
005196F6: mov        r0, r5
005196F8: blx        #0x4a7aa4 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo23BlockTapFilterComponent13DoubleTapInfoEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 005196E4 size=24
005196FC: mov        r0, r4
005196FE: pop.w      {r4, r5, r7, lr}
00519702: b.w        #0x8ca88c
00519706: pop        {r4, r5, r7, pc}
local 00519F8C; bounded robot signal subscription entry; ownership binding

RANGE 00519F8C..0051A080
00519F8C: push.w     {r4, r5, r6, r7, r8, lr}
00519F90: sub        sp, #0x28
00519F92: mov        r7, r0
00519F94: ldr        r0, [pc, #0x70] ; literal[0051A008]=00B248AE
00519F96: mov        r6, r1
00519F98: ldr        r1, [sp, #0x40]
00519F9A: add        r0, pc
00519F9C: add.w      r8, sp, #8
00519FA0: mov        r4, r2
00519FA2: ldr        r0, [r0]
00519FA4: ldr        r0, [r0]
00519FA6: str        r0, [sp, #0x24]
00519FA8: mov        r0, r8
00519FAA: ldrb       r5, [r3]
00519FAC: blx        #0x4a8344 ; _ZNSt6__ndk18functionIFvRKN4Anki5Cozmo9AnkiEventINS2_14RobotInterface13RobotToEngineEEEEEC2ERKSA_ -> 0051CD50 size=2A
00519FB0: adds       r1, r6, #4
00519FB2: mov        r0, r7
00519FB4: mov        r2, r4
00519FB6: mov        r3, r5
00519FB8: str.w      r8, [sp]
00519FBC: bl         #0x51cd2a
00519FC0: ldr        r0, [sp, #0x18]
00519FC2: cmp        r8, r0
00519FC4: beq        #0x519fce
00519FC6: cbz        r0, #0x519fd4
00519FC8: ldr        r1, [r0]
00519FCA: ldr        r1, [r1, #0x14]
00519FCC: b          #0x519fd2
00519FCE: ldr        r1, [r0]
00519FD0: ldr        r1, [r1, #0x10]
00519FD2: blx        r1
00519FD4: ldr        r0, [pc, #0x34] ; literal[0051A00C]=00B24870
00519FD6: ldr        r1, [sp, #0x24]
00519FD8: add        r0, pc
00519FDA: ldr        r0, [r0]
00519FDC: ldr        r0, [r0]
00519FDE: subs       r0, r0, r1
00519FE0: itt        eq
00519FE2: addeq      sp, #0x28
00519FE4: popeq.w    {r4, r5, r6, r7, r8, pc}
00519FE8: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
00519FEC: mov        r4, r0
00519FEE: ldr        r0, [sp, #0x18]
00519FF0: cmp        r8, r0
00519FF2: bne        #0x519ffa
00519FF4: ldr        r1, [r0]
00519FF6: ldr        r1, [r1, #0x10]
00519FF8: b          #0x51a000
00519FFA: cbz        r0, #0x51a002
00519FFC: ldr        r1, [r0]
00519FFE: ldr        r1, [r1, #0x14]
0051A000: blx        r1
0051A002: mov        r0, r4
0051A004: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
0051A008: ldr        r0, [pc, #0x2b8] ; literal[0051A2C4]=E008EF04
0051A00A: lsls       r2, r6, #2
0051A00C: ldr        r0, [pc, #0x1c0] ; literal[0051A1D0]=44784871
0051A00E: lsls       r2, r6, #2
0051A010: push       {r4, r5, r6, lr}
0051A012: sub        sp, #0x78
0051A014: mov        r4, r0
0051A016: add.w      r0, r1, #0xc
0051A01A: blx        #0x4a8350 ; _ZNK4Anki5Cozmo14RobotInterface13RobotToEngine18Get_robotAvailableEv -> 007B3BA2 size=4
0051A01E: ldr        r1, [r0]
0051A020: add        r0, sp, #0x6c
0051A022: blx        #0x4a5c74 ; _ZNSt6__ndk19to_stringEj IMPORT (resolve packaged dependencies before calling external)
0051A026: add        r0, sp, #0x50
0051A028: movs       r1, #0
0051A02A: blx        #0x4a4f30 ; _ZN4Json5ValueC1ENS_9ValueTypeE -> 008E7F48 size=5C
0051A02E: ldr        r2, [pc, #0x2c4] ; literal[0051A2F4]=00B3FD18
0051A030: add.w      r5, r4, #0x48
0051A034: add        r2, pc
0051A036: add        r0, sp, #0x40
0051A038: mov        r1, r5
0051A03A: blx        #0x4a835c ; _ZNSt6__ndk1plIcNS_11char_traitsIcEENS_9allocatorIcEEEENS_12basic_stringIT_T0_T1_EERKS9_SB_ -> 0051ABBE size=68
0051A03E: add        r0, sp, #0x40
0051A040: blx        #0x4a82e4 ; _ZN4Anki4Util9FileUtils10FileExistsERKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE -> 0080342A size=28
0051A044: mov        r6, r0
0051A046: ldrb.w     r0, [sp, #0x40]
0051A04A: lsls       r0, r0, #0x1f
0051A04C: itt        ne
0051A04E: ldrne      r0, [sp, #0x48]
0051A050: blxne      #0x4a40cc
0051A054: cmp        r6, #1
0051A056: bne        #0x51a0c4
0051A058: ldr        r0, [r4, #4]!
0051A05C: blx        #0x4a82cc ; _ZN4Anki5Cozmo5Robot22GetContextDataPlatformEv -> 00516BD2 size=6
0051A060: ldr        r2, [pc, #0x298] ; literal[0051A2FC]=00B3FCEA
0051A062: add        r2, pc
0051A064: add        r0, sp, #0x40
0051A066: mov        r1, r5
0051A068: blx        #0x4a835c ; _ZNSt6__ndk1plIcNS_11char_traitsIcEENS_9allocatorIcEEEENS_12basic_stringIT_T0_T1_EERKS9_SB_ -> 0051ABBE size=68
0051A06C: add        r0, sp, #0x40
0051A06E: add        r1, sp, #0x50
0051A070: blx        #0x4a82f0 ; _ZN4Anki4Util4Data12DataPlatform10readAsJsonERKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEERN4Json5ValueE -> 0084C01C size=30C
0051A074: mov        r5, r0
0051A076: ldrb.w     r0, [sp, #0x40]
0051A07A: lsls       r0, r0, #0x1f
0051A07C: itt        ne
0051A07E: ldrne      r0, [sp, #0x48]
_ZN4Anki5Cozmo22RobotDataBackupManagerD1Ev

RANGE 0051A5F0..0051A66C _ZN4Anki5Cozmo22RobotDataBackupManagerD1Ev
0051A5F0: push       {r4, r5, r7, lr}
0051A5F2: mov        r4, r0
0051A5F4: ldr        r0, [pc, #0x70] ; literal[0051A668]=00B2438A
0051A5F6: add        r0, pc
0051A5F8: ldr        r0, [r0]
0051A5FA: adds       r0, #8
0051A5FC: str        r0, [r4]
0051A5FE: mov        r0, r4
0051A600: blx        #0x4a8380 ; _ZN4Anki5Cozmo22RobotDataBackupManager15WriteBackupFileEv -> 0051A66C size=29C
0051A604: ldrb.w     r0, [r4, #0x48]
0051A608: lsls       r0, r0, #0x1f
0051A60A: itt        ne
0051A60C: ldrne      r0, [r4, #0x50]
0051A60E: blxne      #0x4a40cc
0051A612: ldr        r1, [r4, #0x40]
0051A614: add.w      r0, r4, #0x3c
0051A618: blx        #0x4a4264 ; _ZNSt6__ndk16__treeIjNS_4lessIjEENS_9allocatorIjEEE7destroyEPNS_11__tree_nodeIjPvEE -> 004E0BE4 size=24
0051A61C: add.w      r0, r4, #0x28
0051A620: blx        #0x4a832c ; _ZNSt6__ndk112__hash_tableINS_17__hash_value_typeIjNS_6vectorIhNS_9allocatorIhEEEEEENS_22__unordered_map_hasherIjS6_NS_4hashIjEELb1EEENS_21__unordered_map_equalIjS6_NS_8equal_toIjEELb1EEENS3_IS6_EEED2Ev -> 0051D936 size=1A
0051A624: add.w      r0, r4, #0x14
0051A628: blx        #0x4a8338 ; _ZNSt6__ndk112__hash_tableINS_17__hash_value_typeIjNS_6vectorINS2_IhNS_9allocatorIhEEEENS3_IS5_EEEEEENS_22__unordered_map_hasherIjS8_NS_4hashIjEELb1EEENS_21__unordered_map_equalIjS8_NS_8equal_toIjEELb1EEENS3_IS8_EEED2Ev -> 0051D976 size=1A
0051A62C: add.w      r0, r4, #8
0051A630: blx        #0x4a4e4c ; _ZNSt6__ndk113__vector_baseINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEED2Ev -> 004EAE94 size=34
0051A634: mov        r0, r4
0051A636: pop        {r4, r5, r7, pc}
0051A638: mov        r5, r0
0051A63A: add.w      r0, r4, #0x48
0051A63E: blx        #0x4a490c ; _ZNSt6__ndk112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEED2Ev -> 004E5398 size=14
0051A642: add.w      r0, r4, #0x3c
0051A646: bl         #0x51a5e0
0051A64A: add.w      r0, r4, #0x28
0051A64E: blx        #0x4a832c ; _ZNSt6__ndk112__hash_tableINS_17__hash_value_typeIjNS_6vectorIhNS_9allocatorIhEEEEEENS_22__unordered_map_hasherIjS6_NS_4hashIjEELb1EEENS_21__unordered_map_equalIjS6_NS_8equal_toIjEELb1EEENS3_IS6_EEED2Ev -> 0051D936 size=1A
0051A652: add.w      r0, r4, #0x14
0051A656: blx        #0x4a8338 ; _ZNSt6__ndk112__hash_tableINS_17__hash_value_typeIjNS_6vectorINS2_IhNS_9allocatorIhEEEENS3_IS5_EEEEEENS_22__unordered_map_hasherIjS8_NS_4hashIjEELb1EEENS_21__unordered_map_equalIjS8_NS_8equal_toIjEELb1EEENS3_IS8_EEED2Ev -> 0051D976 size=1A
0051A65A: add.w      r0, r4, #8
0051A65E: blx        #0x4a4e4c ; _ZNSt6__ndk113__vector_baseINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEED2Ev -> 004EAE94 size=34
0051A662: mov        r0, r5
0051A664: bl         #0x4e39f8
0051A668: bics       r2, r1
0051A66A: lsls       r2, r6, #2
_ZNSt6__ndk18functionIFvvEEC2ERKS2_; bounded I8 callback copy

RANGE 00528C0C..00528C36 _ZNSt6__ndk18functionIFvvEEC2ERKS2_
00528C0C: push       {r4, lr}
00528C0E: mov        r4, r0
00528C10: ldr        r0, [r1, #0x10]
00528C12: cbz        r0, #0x528c20
00528C14: cmp        r1, r0
00528C16: beq        #0x528c26
00528C18: ldr        r1, [r0]
00528C1A: ldr        r1, [r1, #8]
00528C1C: blx        r1
00528C1E: b          #0x528c22
00528C20: movs       r0, #0
00528C22: str        r0, [r4, #0x10]
00528C24: b          #0x528c32
00528C26: str        r4, [r4, #0x10]
00528C28: ldr        r0, [r1, #0x10]
00528C2A: ldr        r1, [r0]
00528C2C: ldr        r2, [r1, #0xc]
00528C2E: mov        r1, r4
00528C30: blx        r2
00528C32: mov        r0, r4
00528C34: pop        {r4, pc}
_ZN4Anki5Cozmo10ActionList6CancelENS0_15RobotActionTypeE; bounded A2 Cancel boundary

RANGE 0053DE10..0053DE5E _ZN4Anki5Cozmo10ActionList6CancelENS0_15RobotActionTypeE
0053DE10: push       {r4, r5, r6, r7, lr}
0053DE12: sub        sp, #4
0053DE14: mov        r5, r0
0053DE16: mov        r4, r1
0053DE18: ldrb       r0, [r5, #0xc]
0053DE1A: cbz        r0, #0x53de20
0053DE1C: movs       r6, #1
0053DE1E: b          #0x53de56
0053DE20: ldr        r0, [r5], #4
0053DE24: movs       r6, #0
0053DE26: cmp        r0, r5
0053DE28: beq        #0x53de56
0053DE2A: mov        r7, r0
0053DE2C: adds       r0, #0x14
0053DE2E: mov        r1, r4
0053DE30: blx        #0x4aa8dc ; _ZN4Anki5Cozmo11ActionQueue6CancelENS0_15RobotActionTypeE -> 0053E690 size=74
0053DE34: ldr        r1, [r7, #4]
0053DE36: orrs       r6, r0
0053DE38: cmp        r1, #0
0053DE3A: beq        #0x53de46
0053DE3C: mov        r0, r1
0053DE3E: ldr        r1, [r0]
0053DE40: cmp        r1, #0
0053DE42: bne        #0x53de3c
0053DE44: b          #0x53de50
0053DE46: ldr        r0, [r7, #8]
0053DE48: ldr        r1, [r0]
0053DE4A: cmp        r1, r7
0053DE4C: mov        r7, r0
0053DE4E: bne        #0x53de46
0053DE50: cmp        r0, r5
0053DE52: mov        r7, r0
0053DE54: bne        #0x53de2c
0053DE56: and        r0, r6, #1
0053DE5A: add        sp, #4
0053DE5C: pop        {r4, r5, r6, r7, pc}
_ZN4Anki5Cozmo11ActionQueue6CancelENS0_15RobotActionTypeE

RANGE 0053E690..0053E704 _ZN4Anki5Cozmo11ActionQueue6CancelENS0_15RobotActionTypeE
0053E690: push.w     {r4, r5, r6, r7, r8, lr}
0053E694: sub        sp, #8
0053E696: mov        r5, r0
0053E698: mov        r4, r1
0053E69A: ldr        r0, [r5]
0053E69C: cbz        r0, #0x53e6be
0053E69E: adds       r1, r4, #1
0053E6A0: itt        ne
0053E6A2: ldrne      r1, [r0, #0x44]
0053E6A4: cmpne      r1, r4
0053E6A6: bne        #0x53e6be
0053E6A8: blx        #0x4aa918 ; _ZN4Anki5Cozmo13IActionRunner6CancelEv -> 005409DC size=CC
0053E6AC: adds       r0, r5, #4
0053E6AE: add        r2, sp, #4
0053E6B0: str        r0, [sp, #4]
0053E6B2: mov        r0, r5
0053E6B4: mov        r1, r5
0053E6B6: blx        #0x4aa864 ; _ZN4Anki5Cozmo11ActionQueue19DeleteActionAndIterERPNS0_13IActionRunnerERNSt6__ndk115__list_iteratorIS3_PvEE -> 0053F9E4 size=178
0053E6BA: movs       r7, #1
0053E6BC: b          #0x53e6c0
0053E6BE: movs       r7, #0
0053E6C0: ldr        r0, [r5, #8]
0053E6C2: adds       r6, r5, #4
0053E6C4: str        r0, [sp]
0053E6C6: cmp        r6, r0
0053E6C8: beq        #0x53e6fa
0053E6CA: mov        r8, sp
0053E6CC: adds       r1, r4, #1
0053E6CE: ittt       ne
0053E6D0: ldrne      r1, [r0, #8]
0053E6D2: ldrne      r1, [r1, #0x44]
0053E6D4: cmpne      r1, r4
0053E6D6: bne        #0x53e6f2
0053E6D8: add.w      r1, r0, #8
0053E6DC: mov        r0, r5
0053E6DE: mov        r2, r8
0053E6E0: blx        #0x4aa864 ; _ZN4Anki5Cozmo11ActionQueue19DeleteActionAndIterERPNS0_13IActionRunnerERNSt6__ndk115__list_iteratorIS3_PvEE -> 0053F9E4 size=178
0053E6E4: cmp        r0, #1
0053E6E6: bne        #0x53e6fa
0053E6E8: ldr        r0, [sp]
0053E6EA: movs       r7, #1
0053E6EC: cmp        r0, r6
0053E6EE: bne        #0x53e6cc
0053E6F0: b          #0x53e6fa
0053E6F2: ldr        r0, [r0, #4]
0053E6F4: str        r0, [sp]
0053E6F6: cmp        r6, r0
0053E6F8: bne        #0x53e6cc
0053E6FA: and        r0, r7, #1
0053E6FE: add        sp, #8
0053E700: pop.w      {r4, r5, r6, r7, r8, pc}
_ZN4Anki5Cozmo7IActionC2ERNS0_5RobotENSt6__ndk112basic_stringIcNS4_11char_traitsIcEENS4_9allocatorIcEEEENS0_15RobotActionTypeEh

RANGE 00540C44..00540CE8 _ZN4Anki5Cozmo7IActionC2ERNS0_5RobotENSt6__ndk112basic_stringIcNS4_11char_traitsIcEENS4_9allocatorIcEEEENS0_15RobotActionTypeEh
00540C44: push.w     {r4, r5, r6, r7, r8, lr}
00540C48: sub        sp, #0x18
00540C4A: mov        r4, r0
00540C4C: movs       r0, #0
00540C4E: str        r0, [sp, #0x10]
00540C50: mov        r5, r3
00540C52: strd       r0, r0, [sp, #8]
00540C56: mov        r6, r1
00540C58: ldrb       r0, [r2]
00540C5A: ldr.w      r8, [sp, #0x30]
00540C5E: lsls       r0, r0, #0x1f
00540C60: bne        #0x540c6c
00540C62: add        r0, sp, #8
00540C64: ldm.w      r2, {r1, r3, r7}
00540C68: stm        r0!, {r1, r3, r7}
00540C6A: b          #0x540c78
00540C6C: ldrd       r3, r1, [r2, #4]
00540C70: add        r0, sp, #8
00540C72: mov        r2, r3
00540C74: bl         #0x4e02b2
00540C78: add        r2, sp, #8
00540C7A: mov        r0, r4
00540C7C: mov        r1, r6
00540C7E: mov        r3, r5
00540C80: str.w      r8, [sp]
00540C84: blx        #0x4aab34 ; _ZN4Anki5Cozmo13IActionRunnerC2ERNS0_5RobotENSt6__ndk112basic_stringIcNS4_11char_traitsIcEENS4_9allocatorIcEEEENS0_15RobotActionTypeEh -> 0053FDB0 size=2D4
00540C88: ldrb.w     r0, [sp, #8]
00540C8C: lsls       r0, r0, #0x1f
00540C8E: itt        ne
00540C90: ldrne      r0, [sp, #0x10]
00540C92: blxne      #0x4a40cc
00540C96: ldr        r0, [pc, #0x4c] ; literal[00540CE4]=00AFDE7C
00540C98: movs       r1, #0
00540C9A: strb.w     r1, [r4, #0x70]
00540C9E: movs       r1, #0
00540CA0: add        r0, pc
00540CA2: movt       r1, #0xbf80
00540CA6: str        r1, [r4, #0x74]
00540CA8: ldr        r0, [r0]
00540CAA: adds       r0, #8
00540CAC: str        r0, [r4]
00540CAE: mov        r0, r4
00540CB0: blx        #0x4aab40 ; _ZN4Anki5Cozmo13IActionRunner12UnlockTracksEv -> 005408EC size=2A
00540CB4: movs       r0, #1
00540CB6: movt       r0, #0x200
00540CBA: str        r0, [r4, #0x18]
00540CBC: mov        r0, r4
00540CBE: add        sp, #0x18
00540CC0: pop.w      {r4, r5, r6, r7, r8, pc}
00540CC4: mov        r5, r0
00540CC6: mov        r0, r4
00540CC8: blx        #0x4aab4c ; _ZN4Anki5Cozmo13IActionRunnerD2Ev -> 00541084 size=344
00540CCC: b          #0x540cde
00540CCE: mov        r5, r0
00540CD0: ldrb.w     r0, [sp, #8]
00540CD4: lsls       r0, r0, #0x1f
00540CD6: beq        #0x540cde
00540CD8: ldr        r0, [sp, #0x10]
00540CDA: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00540CDE: mov        r0, r5
00540CE0: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
00540CE4: udf        #0x7c
00540CE6: lsls       r7, r5, #2
_ZNK4Anki5Cozmo7IAction6GetRNGEv

RANGE 00540D10..00540D1A _ZNK4Anki5Cozmo7IAction6GetRNGEv
00540D10: push       {r7, lr}
00540D12: ldr        r0, [r0, #4]
00540D14: blx        #0x4aa6a8 ; _ZN4Anki5Cozmo5Robot6GetRNGEv -> 005126B6 size=6
00540D18: pop        {r7, pc}
_ZN4Anki5Cozmo13IActionRunnerD2Ev

RANGE 00541084..005413C8 _ZN4Anki5Cozmo13IActionRunnerD1Ev
00541084: push       {r4, r5, r6, lr}
00541086: sub        sp, #0x30
00541088: mov        r4, r0
0054108A: ldr        r0, [pc, #0x290] ; literal[0054131C]=00AFDA84
0054108C: ldrb.w     r1, [r4, #0x55]
00541090: add        r0, pc
00541092: cmp        r1, #0
00541094: ldr        r0, [r0]
00541096: add.w      r0, r0, #8
0054109A: str        r0, [r4]
0054109C: bne        #0x5410ec
0054109E: movs       r0, #0
005410A0: strd       r0, r0, [sp, #0x24]
005410A4: str        r0, [sp, #0x2c]
005410A6: ldr        r3, [r4, #0x60]
005410A8: adr        r0, #0x274 ; ADR[00541320]=b'IActionRunner.Destructor.NotPreppedForCompletion'
005410AA: add        r1, sp, #0x24
005410AC: adr        r2, #0x2a4 ; ADR[00541354]=b'[%d]'
005410AE: blx        #0x4a4108 ; _ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D13C size=80
005410B2: ldr        r0, [sp, #0x24]
005410B4: cbz        r0, #0x5410d4
005410B6: ldr        r1, [sp, #0x28]
005410B8: cmp        r1, r0
005410BA: itttt      ne
005410BC: subne.w    r2, r1, #8
005410C0: subne      r2, r2, r0
005410C2: mvnne      r3, #7
005410C6: bicne.w    r2, r3, r2
005410CA: itt        ne
005410CC: addne      r1, r1, r2
005410CE: strne      r1, [sp, #0x28]
005410D0: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005410D4: ldr        r0, [pc, #0x284] ; literal[0054135C]=00AFD6AE
005410D6: movs       r2, #1
005410D8: ldr        r1, [pc, #0x284] ; literal[00541360]=00AFD6B0
005410DA: add        r0, pc
005410DC: add        r1, pc
005410DE: ldr        r0, [r0]
005410E0: ldr        r1, [r1]
005410E2: ldrb       r0, [r0]
005410E4: strb       r2, [r1]
005410E6: cbz        r0, #0x5410ec
005410E8: blx        #0x4a4114 ; _ZN4Anki4Util18sDebugBreakOnErrorEv -> 0080DAB4 size=2
005410EC: ldrb.w     r0, [r4, #0x58]
005410F0: cbz        r0, #0x5410fa
005410F2: ldr        r0, [r4, #4]
005410F4: ldr        r0, [r0, #0x5c]
005410F6: blx        #0x4aab58 ; _ZN4Anki5Cozmo13PathComponent24ClearCustomMotionProfileEv -> 0064AC4A size=8
005410FA: ldr        r0, [pc, #0x268] ; literal[00541364]=00AFDA1C
005410FC: add        r0, pc
005410FE: ldr        r0, [r0]
00541100: add.w      r1, r4, #0x60
00541104: blx        #0x4aaa68 ; _ZNSt6__ndk16__treeIjNS_4lessIjEENS_9allocatorIjEEE14__erase_uniqueIjEEjRKT_ -> 0054150A size=1E
00541108: ldr        r0, [pc, #0x25c] ; literal[00541368]=00AFDA0A
0054110A: add.w      r5, r4, #0x5c
0054110E: add        r0, pc
00541110: ldr        r0, [r0]
00541112: mov        r1, r5
00541114: blx        #0x4aaa68 ; _ZNSt6__ndk16__treeIjNS_4lessIjEENS_9allocatorIjEEE14__erase_uniqueIjEEjRKT_ -> 0054150A size=1E
00541118: ldr        r0, [r4, #4]
0054111A: ldr        r1, [r4, #0x60]
0054111C: ldr.w      r6, [r0, #0x254]
00541120: add        r0, sp, #0x24
00541122: blx        #0x4a5c74 ; _ZNSt6__ndk19to_stringEj IMPORT (resolve packaged dependencies before calling external)
00541126: movs       r0, #0
00541128: str        r0, [sp, #0x20]
0054112A: strd       r0, r0, [sp, #0x18]
0054112E: ldrb       r0, [r6, #0xa]
00541130: cbz        r0, #0x541154
00541132: add        r2, sp, #0x24
00541134: mov        r0, r6
00541136: movs       r1, #1
00541138: blx        #0x4aab64 ; _ZNK4Anki5Cozmo17MovementComponent20AreAllTracksLockedByEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE -> 0064030C size=150
0054113C: cmp        r0, #1
0054113E: bne        #0x541154
00541140: ldr        r0, [r4, #4]
00541142: ldr.w      r0, [r0, #0x254]
00541146: blx        #0x4aab70 ; _ZN4Anki5Cozmo17MovementComponent8StopHeadEv -> 00640A08 size=C4
0054114A: add        r0, sp, #0x18
0054114C: adr        r1, #0x21c ; ADR[0054136C]=b'HEAD_TRACK, '
0054114E: movs       r2, #0xc
00541150: bl         #0x4e8048
00541154: ldrb       r0, [r6, #0xb]
00541156: cbz        r0, #0x54117a
00541158: add        r2, sp, #0x24
0054115A: mov        r0, r6
0054115C: movs       r1, #2
0054115E: blx        #0x4aab64 ; _ZNK4Anki5Cozmo17MovementComponent20AreAllTracksLockedByEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE -> 0064030C size=150
00541162: cmp        r0, #1
00541164: bne        #0x54117a
00541166: ldr        r0, [r4, #4]
00541168: ldr.w      r0, [r0, #0x254]
0054116C: blx        #0x4aab7c ; _ZN4Anki5Cozmo17MovementComponent8StopLiftEv -> 00640B3C size=C4
00541170: add        r0, sp, #0x18
00541172: adr        r1, #0x208 ; ADR[0054137C]=b'LIFT_TRACK, '
00541174: movs       r2, #0xc
00541176: bl         #0x4e8048
0054117A: ldrb       r0, [r6, #0xc]
0054117C: cbz        r0, #0x5411a0
0054117E: add        r2, sp, #0x24
00541180: mov        r0, r6
00541182: movs       r1, #4
00541184: blx        #0x4aab64 ; _ZNK4Anki5Cozmo17MovementComponent20AreAllTracksLockedByEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE -> 0064030C size=150
00541188: cmp        r0, #1
0054118A: bne        #0x5411a0
0054118C: ldr        r0, [r4, #4]
0054118E: ldr.w      r0, [r0, #0x254]
00541192: blx        #0x4aab88 ; _ZN4Anki5Cozmo17MovementComponent8StopBodyEv -> 00640C70 size=1AA
00541196: add        r0, sp, #0x18
00541198: adr        r1, #0x1f0 ; ADR[0054138C]=b'BODY_TRACK, '
0054119A: movs       r2, #0xc
0054119C: bl         #0x4e8048
005411A0: ldrb.w     r2, [sp, #0x18]
005411A4: ldr        r1, [sp, #0x1c]
005411A6: ands       r0, r2, #1
005411AA: it         eq
005411AC: lsreq      r1, r2, #1
005411AE: cbz        r1, #0x54120c
005411B0: movs       r2, #0
005411B2: cmp        r0, #0
005411B4: strd       r2, r2, [sp, #0x10]
005411B8: add        r3, sp, #0x18
005411BA: ldr        r0, [pc, #0x1e0] ; literal[0054139C]=006A881E
005411BC: str        r2, [sp, #0xc]
005411BE: mov        r2, r4
005411C0: ldr        r1, [sp, #0x20]
005411C2: add        r0, pc
005411C4: ldrb       r6, [r2, #0x48]!
005411C8: it         eq
005411CA: orreq      r1, r3, #1
005411CE: ldr        r3, [pc, #0x1d0] ; literal[005413A0]=006A880C
005411D0: tst.w      r6, #1
005411D4: ite        eq
005411D6: addeq      r2, #1
005411D8: ldrne      r2, [r4, #0x50]
005411DA: ldr        r6, [r5]
005411DC: add        r3, pc
005411DE: stm.w      sp, {r1, r2, r6}
005411E2: adr        r1, #0x1c0 ; ADR[005413A4]=b'IActionRunner.Destroy.StopMovement'
005411E4: add        r2, sp, #0xc
005411E6: blx        #0x4a505c ; _ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D42C size=14
005411EA: ldr        r0, [sp, #0xc]
005411EC: cbz        r0, #0x54120c
005411EE: ldr        r1, [sp, #0x10]
005411F0: cmp        r1, r0
005411F2: itttt      ne
005411F4: subne.w    r2, r1, #8
005411F8: subne      r2, r2, r0
005411FA: mvnne      r3, #7
005411FE: bicne.w    r2, r3, r2
00541202: itt        ne
00541204: addne      r1, r1, r2
00541206: strne      r1, [sp, #0x10]
00541208: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0054120C: ldrb.w     r0, [r4, #0x56]
00541210: cbnz       r0, #0x54122e
00541212: ldr        r0, [r4, #0x18]
00541214: movs       r1, #1
00541216: movt       r1, #0x200
0054121A: cmp        r0, r1
0054121C: beq        #0x54122e
0054121E: ldr        r0, [r4, #4]
00541220: ldrb.w     r1, [r4, #0x54]
00541224: ldr        r2, [r4, #0x60]
00541226: ldr.w      r0, [r0, #0x254]
0054122A: bl         #0x4f0eb2
0054122E: ldr        r0, [r4, #4]
00541230: ldr.w      r0, [r0, #0x250]
00541234: ldr        r0, [r0, #0x10]
00541236: mov        r1, r4
00541238: blx        #0x4aab94 ; _ZN4Anki5Cozmo13ActionWatcher12ActionEndingEPKNS0_13IActionRunnerE -> 00541BB4 size=2F8
0054123C: ldrb.w     r0, [sp, #0x18]
00541240: lsls       r0, r0, #0x1f
00541242: itt        ne
00541244: ldrne      r0, [sp, #0x20]
00541246: blxne      #0x4a40cc
0054124A: ldrb.w     r0, [sp, #0x24]
0054124E: lsls       r0, r0, #0x1f
00541250: itt        ne
00541252: ldrne      r0, [sp, #0x2c]
00541254: blxne      #0x4a40cc
00541258: add.w      r0, r4, #0x64
0054125C: blx        #0x4aaa5c ; _ZNSt6__ndk110__list_impINS_8functionIFvN4Anki5Cozmo12ActionResultEEEENS_9allocatorIS6_EEE5clearEv -> 005413CE size=46
00541260: ldrb.w     r0, [r4, #0x48]
00541264: lsls       r0, r0, #0x1f
00541266: itt        ne
00541268: ldrne      r0, [r4, #0x50]
0054126A: blxne      #0x4a40cc
0054126E: add.w      r0, r4, #0x1c
00541272: blx        #0x4aa9f0 ; _ZN4Anki5Cozmo20ActionCompletedUnion12ClearCurrentEv -> 0075C29C size=1C
00541276: ldrb       r0, [r4, #0xc]
00541278: lsls       r0, r0, #0x1f
0054127A: itt        ne
0054127C: ldrne      r0, [r4, #0x14]
0054127E: blxne      #0x4a40cc
00541282: mov        r0, r4
00541284: add        sp, #0x30
00541286: pop        {r4, r5, r6, pc}
00541288: mov        r5, r0
0054128A: ldr        r0, [sp, #0x24]
0054128C: cbz        r0, #0x5412f6
0054128E: ldr        r1, [sp, #0x28]
00541290: cmp        r1, r0
00541292: beq        #0x5412f2
00541294: sub.w      r2, r1, #8
00541298: mvn        r3, #7
0054129C: subs       r2, r2, r0
0054129E: bic.w      r2, r3, r2
005412A2: add        r1, r2
005412A4: str        r1, [sp, #0x28]
005412A6: b          #0x5412f2
005412A8: mov        r5, r0
005412AA: ldr        r0, [sp, #0xc]
005412AC: cbz        r0, #0x5412da
005412AE: ldr        r1, [sp, #0x10]
005412B0: cmp        r1, r0
005412B2: beq        #0x5412c6
005412B4: sub.w      r2, r1, #8
005412B8: mvn        r3, #7
005412BC: subs       r2, r2, r0
005412BE: bic.w      r2, r3, r2
005412C2: add        r1, r2
005412C4: str        r1, [sp, #0x10]
005412C6: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005412CA: b          #0x5412da
005412CC: bl         #0x4e39f8
005412D0: mov        r5, r0
005412D2: b          #0x5412f6
005412D4: mov        r5, r0
005412D6: b          #0x5412f6
005412D8: mov        r5, r0
005412DA: ldrb.w     r0, [sp, #0x18]
005412DE: lsls       r0, r0, #0x1f
005412E0: itt        ne
005412E2: ldrne      r0, [sp, #0x20]
005412E4: blxne      #0x4a40cc
005412E8: ldrb.w     r0, [sp, #0x24]
005412EC: lsls       r0, r0, #0x1f
005412EE: beq        #0x5412f6
005412F0: ldr        r0, [sp, #0x2c]
005412F2: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
005412F6: add.w      r0, r4, #0x64
005412FA: blx        #0x4aaa5c ; _ZNSt6__ndk110__list_impINS_8functionIFvN4Anki5Cozmo12ActionResultEEEENS_9allocatorIS6_EEE5clearEv -> 005413CE size=46
005412FE: add.w      r0, r4, #0x48
00541302: blx        #0x4a490c ; _ZNSt6__ndk112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEED2Ev -> 004E5398 size=14
00541306: add.w      r0, r4, #0x1c
0054130A: bl         #0x540084
0054130E: add.w      r0, r4, #0xc
00541312: blx        #0x4a490c ; _ZNSt6__ndk112basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEED2Ev -> 004E5398 size=14
00541316: mov        r0, r5
00541318: bl         #0x4e39f8
0054131C: bge        #0x541228
0054131E: lsls       r7, r5, #2
00541320: adcs       r1, r1
00541322: strb       r3, [r4, #0x11]
00541324: ldr        r1, [r5, #0x74]
00541326: strh       r6, [r5, r1]
00541328: ldr        r5, [r6, #0x64]
0054132A: str        r6, [r5, #0x54]
0054132C: cmp        r6, #0x72
0054132E: str        r4, [r0, #0x54]
00541330: strb       r3, [r6, #0x11]
00541332: strb       r2, [r6, #0x15]
00541334: strb       r3, [r4, #0x11]
00541336: strb       r7, [r5, #9]
00541338: ldr        r6, [pc, #0xb8] ; literal[005413F4]=4281686E
0054133A: strb       r7, [r5, #0x11]
0054133C: strb       r0, [r2, #9]
0054133E: strb       r5, [r4, #1]
00541340: str        r0, [r6, #0x54]
00541342: mov        r4, ip
00541344: strb       r7, [r5, #9]
00541346: ldr        r3, [r0, #0x74]
00541348: strb       r5, [r5, #1]
0054134A: str        r4, [r5, #0x54]
0054134C: ldr        r4, [r6, #0x14]
0054134E: ldr        r7, [r5, #0x64]
00541350: movs       r0, r0
00541352: movs       r0, r0
00541354: movs       r5, #0x5b
00541356: ldrb       r4, [r4, r5]
00541358: movs       r0, r0
0054135A: movs       r0, r0
0054135C: bvs        #0x5412bc
0054135E: lsls       r7, r5, #2
00541360: bvs        #0x5412c4
00541362: lsls       r7, r5, #2
00541364: bge        #0x5413a0
00541366: lsls       r7, r5, #2
00541368: bge        #0x541380
0054136A: lsls       r7, r5, #2
0054136C: cmp        r0, sb
0054136E: add        r1, r8
00541370: strb       r7, [r3, r1]
00541372: adcs       r2, r2
00541374: ldr        r3, [pc, #0x10c] ; literal[00541484]=F8F0F79F
00541376: movs       r0, #0x2c
00541378: movs       r0, r0
0054137A: movs       r0, r0
0054137C: ldr        r1, [pc, #0x130] ; literal[005414B0]=60054479
0054137E: strb       r6, [r0, r1]
00541380: strb       r7, [r3, r1]
00541382: adcs       r2, r2
00541384: ldr        r3, [pc, #0x10c] ; literal[00541494]=4478480E
00541386: movs       r0, #0x2c
00541388: movs       r0, r0
0054138A: movs       r0, r0
0054138C: ldr        r7, [pc, #0x108] ; literal[00541498]=F1006800
0054138E: ldr        r4, [r0, r5]
00541390: strb       r7, [r3, r1]
00541392: adcs       r2, r2
00541394: ldr        r3, [pc, #0x10c] ; literal[005414A4]=4621EF0A
00541396: movs       r0, #0x2c
00541398: movs       r0, r0
0054139A: movs       r0, r0
0054139C: ldrh       r6, [r3]
0054139E: lsls       r2, r5, #1
005413A0: ldrh       r4, [r1]
005413A2: lsls       r2, r5, #1
005413A4: adcs       r1, r1
005413A6: strb       r3, [r4, #0x11]
005413A8: ldr        r1, [r5, #0x74]
005413AA: strh       r6, [r5, r1]
005413AC: ldr        r5, [r6, #0x64]
005413AE: str        r6, [r5, #0x54]
005413B0: cmp        r6, #0x72
005413B2: str        r4, [r0, #0x54]
005413B4: strb       r3, [r6, #0x11]
005413B6: ldr        r2, [r6, #0x74]
005413B8: cmp        r6, #0x79
005413BA: strb       r3, [r2, #0x11]
005413BC: strb       r7, [r5, #1]
005413BE: ldr        r5, [r1, #0x74]
005413C0: str        r6, [r6, #0x54]
005413C2: str        r5, [r5, #0x54]
005413C4: strb       r6, [r5, #0x11]
005413C6: movs       r0, r0
_ZN4Anki5Cozmo22MoveLiftToHeightActionC2ERNS0_5RobotEfff; bounded L5 lift ctor

RANGE 0054899C..00548B16 _ZN4Anki5Cozmo22MoveLiftToHeightActionC2ERNS0_5RobotEfff
0054899C: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
005489A0: sub        sp, #4
005489A2: vpush      {d8, d9, d10}
005489A6: sub        sp, #0x50
005489A8: mov        r4, r0
005489AA: ldr        r0, [pc, #0x1c0] ; literal[00548B6C]=00AF5E98
005489AC: add        r6, sp, #4
005489AE: mov        sl, r2
005489B0: add        r0, pc
005489B2: mov        sb, r1
005489B4: mov        r1, sl
005489B6: mov        r8, r3
005489B8: ldr        r0, [r0]
005489BA: ldr        r0, [r0]
005489BC: str        r0, [sp, #0x4c]
005489BE: mov        r0, r6
005489C0: blx        #0x4a48e8 ; _ZNSt6__ndk19to_stringEf IMPORT (resolve packaged dependencies before calling external)
005489C4: ldr        r2, [pc, #0x1a8] ; literal[00548B70]=006A19DC
005489C6: add        r2, pc
005489C8: mov        r0, r6
005489CA: movs       r1, #0
005489CC: movs       r3, #0xa
005489CE: movs       r7, #0
005489D0: bl         #0x4e80b8
005489D4: add.w      ip, sp, #0x10
005489D8: mov        r1, r0
005489DA: ldm.w      r1, {r2, r5, r6}
005489DE: mov        r3, ip
005489E0: stm        r3!, {r2, r5, r6}
005489E2: strd       r7, r7, [r0]
005489E6: str        r7, [r0, #8]
005489E8: adr        r1, #0x188 ; ADR[00548B74]=b'mm'
005489EA: mov        r0, ip
005489EC: movs       r2, #2
005489EE: mov.w      fp, #2
005489F2: bl         #0x4e8048
005489F6: add        r2, sp, #0x20
005489F8: mov        r1, r0
005489FA: ldm.w      r1, {r5, r6, r7}
005489FE: movs       r1, #0
00548A00: mov        r3, r2
00548A02: stm        r3!, {r5, r6, r7}
00548A04: strd       r1, r1, [r0]
00548A08: str        r1, [r0, #8]
00548A0A: mov        r0, r4
00548A0C: mov        r1, sb
00548A0E: movs       r3, #0x13
00548A10: str.w      fp, [sp]
00548A14: blx        #0x4aadd4 ; _ZN4Anki5Cozmo7IActionC2ERNS0_5RobotENSt6__ndk112basic_stringIcNS4_11char_traitsIcEENS4_9allocatorIcEEEENS0_15RobotActionTypeEh -> 00540C44 size=A4
00548A18: ldrb.w     r0, [sp, #0x20]
00548A1C: lsls       r0, r0, #0x1f
00548A1E: itt        ne
00548A20: ldrne      r0, [sp, #0x28]
00548A22: blxne      #0x4a40cc
00548A26: ldrb.w     r0, [sp, #0x10]
00548A2A: lsls       r0, r0, #0x1f
00548A2C: itt        ne
00548A2E: ldrne      r0, [sp, #0x18]
00548A30: blxne      #0x4a40cc
00548A34: ldrb.w     r0, [sp, #4]
00548A38: vmov       s16, r8
00548A3C: vldr       s18, [sp, #0x90]
00548A40: vmov       s20, sl
00548A44: lsls       r0, r0, #0x1f
00548A46: itt        ne
00548A48: ldrne      r0, [sp, #0xc]
00548A4A: blxne      #0x4a40cc
00548A4E: ldr        r0, [pc, #0x128] ; literal[00548B78]=00AF60F4
00548A50: movs       r2, #0
00548A52: movs       r1, #0
00548A54: movt       r2, #0x4120
00548A58: add        r0, pc
00548A5A: vstr       s20, [r4, #0x78]
00548A5E: vstr       s16, [r4, #0x7c]
00548A62: ldr        r0, [r0]
00548A64: vstr       s18, [r4, #0x80]
00548A68: str.w      r1, [r4, #0x88]
00548A6C: adds       r0, #8
00548A6E: str.w      r2, [r4, #0x8c]
00548A72: movs       r2, #0
00548A74: movt       r2, #0x41a0
00548A78: str.w      r2, [r4, #0x90]
00548A7C: strd       r1, r1, [r4, #0x9c]
00548A80: str.w      r1, [r4, #0x95]
00548A84: str        r0, [r4]
00548A86: ldr        r0, [r4, #4]
00548A88: blx        #0x4a82d8 ; _ZN4Anki5Cozmo5Robot22GetRobotMessageHandlerEv -> 005182EA size=E
00548A8C: mov        r1, r0
00548A8E: ldr        r0, [r4, #4]
00548A90: add        r6, sp, #0x30
00548A92: movs       r3, #0xc4
00548A94: ldr        r2, [r0, #0x10]
00548A96: ldr        r0, [pc, #0xe4] ; literal[00548B7C]=00AD92A6
00548A98: str        r4, [sp, #0x34]
00548A9A: add        r0, pc
00548A9C: strb.w     r3, [sp, #4]
00548AA0: adds       r0, #8
00548AA2: str        r6, [sp, #0x40]
00548AA4: str        r0, [sp, #0x30]
00548AA6: add        r0, sp, #0x10
00548AA8: add        r3, sp, #4
00548AAA: str        r6, [sp]
00548AAC: bl         #0x519f8c
00548AB0: movs       r0, #0
00548AB2: ldrd       r1, r2, [sp, #0x10]
00548AB6: strd       r0, r0, [sp, #0x10]
00548ABA: ldr.w      r0, [r4, #0xa0]
00548ABE: strd       r1, r2, [r4, #0x9c]
00548AC2: cbz        r0, #0x548ad0
00548AC4: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
00548AC8: ldr        r0, [sp, #0x14]
00548ACA: cbz        r0, #0x548ad0
00548ACC: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
00548AD0: ldr        r0, [sp, #0x40]
00548AD2: cmp        r6, r0
00548AD4: beq        #0x548ade
00548AD6: cbz        r0, #0x548ae4
00548AD8: ldr        r1, [r0]
00548ADA: ldr        r1, [r1, #0x14]
00548ADC: b          #0x548ae2
00548ADE: ldr        r1, [r0]
00548AE0: ldr        r1, [r1, #0x10]
00548AE2: blx        r1
00548AE4: ldr        r0, [pc, #0x98] ; literal[00548B80]=00AF5D60
00548AE6: ldr        r1, [sp, #0x4c]
00548AE8: add        r0, pc
00548AEA: ldr        r0, [r0]
00548AEC: ldr        r0, [r0]
00548AEE: subs       r0, r0, r1
00548AF0: itttt      eq
00548AF2: moveq      r0, r4
00548AF4: addeq      sp, #0x50
00548AF6: vpopeq     {d8, d9, d10}
00548AFA: addeq      sp, #4
00548AFC: it         eq
00548AFE: popeq.w    {r4, r5, r6, r7, r8, sb, sl, fp, pc}
00548B02: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
00548B06: mov        r5, r0
00548B08: ldr        r0, [sp, #0x40]
00548B0A: cmp        r6, r0
00548B0C: bne        #0x548b14
00548B0E: ldr        r1, [r0]
00548B10: ldr        r1, [r1, #0x10]
00548B12: b          #0x548b1a
00548B14: cbz        r0, #0x548b20
_ZN4Anki5Cozmo22MoveLiftToHeightActionC1ERNS0_5RobotENS1_6PresetEf; bounded L2 preset ctor

RANGE 00548B84..00548BD6 _ZN4Anki5Cozmo22MoveLiftToHeightActionC2ERNS0_5RobotENS1_6PresetEf
00548B84: push       {r4, r5, r6, r7, lr}
00548B86: sub        sp, #0x14
00548B88: mov        r6, r2
00548B8A: mov        r4, r0
00548B8C: mov        r0, r6
00548B8E: mov        r5, r3
00548B90: mov        r7, r1
00548B92: blx        #0x4ab1a0 ; _ZN4Anki5Cozmo22MoveLiftToHeightAction15GetPresetHeightENS1_6PresetE -> 00548C14 size=C0
00548B96: mov        r2, r0
00548B98: movs       r0, #0
00548B9A: str        r0, [sp]
00548B9C: mov        r0, r4
00548B9E: mov        r1, r7
00548BA0: mov        r3, r5
00548BA2: blx        #0x4ab1ac ; _ZN4Anki5Cozmo22MoveLiftToHeightActionC2ERNS0_5RobotEfff -> 0054899C size=1E8
00548BA6: mov        r0, r6
00548BA8: blx        #0x4ab1b8 ; _ZN4Anki5Cozmo22MoveLiftToHeightAction13GetPresetNameENS1_6PresetE -> 00548CD4 size=278
00548BAC: mov        r2, r0
00548BAE: ldr        r1, [pc, #0x5c] ; literal[00548C0C]=006A17F2
00548BB0: add        r1, pc
00548BB2: add        r0, sp, #8
00548BB4: blx        #0x4a7330 ; _ZNSt6__ndk1plIcNS_11char_traitsIcEENS_9allocatorIcEEEENS_12basic_stringIT_T0_T1_EEPKS6_RKS9_ -> 0050B190 size=5C
00548BB8: add.w      r0, r4, #0x48
00548BBC: add        r1, sp, #8
00548BBE: bl         #0x4e462a
00548BC2: ldrb.w     r0, [sp, #8]
00548BC6: lsls       r0, r0, #0x1f
00548BC8: itt        ne
00548BCA: ldrne      r0, [sp, #0x10]
00548BCC: blxne      #0x4a40cc
00548BD0: mov        r0, r4
00548BD2: add        sp, #0x14
00548BD4: pop        {r4, r5, r6, r7, pc}
_ZN4Anki5Cozmo22MoveLiftToHeightAction15GetPresetHeightENS1_6PresetE; bounded L3 preset height

RANGE 00548C14..00548C8A _ZN4Anki5Cozmo22MoveLiftToHeightAction15GetPresetHeightENS1_6PresetE
00548C14: push       {r4, r5, r6, lr}
00548C16: sub        sp, #8
00548C18: ldr        r1, [pc, #0x88] ; literal[00548CA4]=00B11CF2
00548C1A: strb.w     r0, [sp, #7]
00548C1E: add        r1, pc
00548C20: ldrb       r0, [r1]
00548C22: dmb        ish
00548C26: tst.w      r0, #1
00548C2A: bne        #0x548c78
00548C2C: ldr        r0, [pc, #0x78] ; literal[00548CA8]=00B11CE2
00548C2E: add        r0, pc
00548C30: blx        #0x4a472c ; __cxa_guard_acquire IMPORT (resolve packaged dependencies before calling external)
00548C34: cbz        r0, #0x548c78
00548C36: ldr        r0, [pc, #0x74] ; literal[00548CAC]=00B11CC6
00548C38: movs       r5, #0
00548C3A: ldr        r6, [pc, #0x74] ; literal[00548CB0]=0070BA40
00548C3C: ldr        r4, [pc, #0x74] ; literal[00548CB4]=00B11CC2
00548C3E: add        r0, pc
00548C40: add        r6, pc
00548C42: add        r4, pc
00548C44: mov        r1, r0
00548C46: str        r5, [r0, #8]
00548C48: str        r5, [r1, #4]!
00548C4C: str        r1, [r0]
00548C4E: adds       r2, r6, r5
00548C50: adds       r1, r4, #4
00548C52: mov        r0, r4
00548C54: mov        r3, r2
00548C56: blx        #0x4ab1c4 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_fEEEEENS_15__tree_iteratorIS6_PNS_11__tree_nodeIS6_PvEEiEENS_21__tree_const_iteratorIS6_SO_iEERKT_DpOT0_ -> 0054D830 size=38
00548C5A: adds       r5, #8
00548C5C: cmp        r5, #0x20
00548C5E: bne        #0x548c4e
00548C60: ldr        r0, [pc, #0x5c] ; literal[00548CC0]=000002E3
00548C62: ldr        r1, [pc, #0x60] ; literal[00548CC4]=00B11C9C
00548C64: ldr        r2, [pc, #0x60] ; literal[00548CC8]=00B08392
00548C66: add        r0, pc
00548C68: add        r1, pc
00548C6A: add        r2, pc
00548C6C: blx        #0x4a4024 ; __cxa_atexit IMPORT (resolve packaged dependencies before calling external)
00548C70: ldr        r0, [pc, #0x58] ; literal[00548CCC]=00B11C9E
00548C72: add        r0, pc
00548C74: blx        #0x4a475c ; __cxa_guard_release IMPORT (resolve packaged dependencies before calling external)
00548C78: ldr        r0, [pc, #0x54] ; literal[00548CD0]=00B11C86
00548C7A: add.w      r1, sp, #7
00548C7E: add        r0, pc
00548C80: blx        #0x4ab1d0 ; _ZNKSt6__ndk13mapIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfNS_4lessIS4_EENS_9allocatorINS_4pairIKS4_fEEEEE2atERS9_ -> 00548F5C size=80
00548C84: ldr        r0, [r0]
00548C86: add        sp, #8
00548C88: pop        {r4, r5, r6, pc}
_ZN4Anki5Cozmo22MoveLiftToHeightAction13GetPresetNameENS1_6PresetE; bounded L4 preset names

RANGE 00548CD4..00548E48 _ZN4Anki5Cozmo22MoveLiftToHeightAction13GetPresetNameENS1_6PresetE
00548CD4: push       {r4, r5, r6, r7, lr}
00548CD6: sub        sp, #0x44
00548CD8: mov        r4, r0
00548CDA: ldr        r0, [pc, #0x1d8] ; literal[00548EB4]=00B11C44
00548CDC: add        r0, pc
00548CDE: ldrb       r0, [r0]
00548CE0: dmb        ish
00548CE4: tst.w      r0, #1
00548CE8: bne        #0x548db0
00548CEA: ldr        r0, [pc, #0x1cc] ; literal[00548EB8]=00B11C34
00548CEC: add        r0, pc
00548CEE: blx        #0x4a472c ; __cxa_guard_acquire IMPORT (resolve packaged dependencies before calling external)
00548CF2: cmp        r0, #0
00548CF4: beq        #0x548db0
00548CF6: movs       r5, #0
00548CF8: str        r5, [sp, #0x10]
00548CFA: strd       r5, r5, [sp, #8]
00548CFE: strb.w     r5, [sp, #4]
00548D02: add        r6, sp, #4
00548D04: adr        r1, #0x1b4 ; ADR[00548EBC]=b'LowDock'
00548D06: adds       r0, r6, #4
00548D08: movs       r2, #7
00548D0A: bl         #0x4e02b2
00548D0E: movs       r0, #1
00548D10: str        r5, [sp, #0x20]
00548D12: strd       r5, r5, [sp, #0x18]
00548D16: strb.w     r0, [sp, #0x14]
00548D1A: add.w      r0, r6, #0x14
00548D1E: adr        r1, #0x1a4 ; ADR[00548EC4]=b'HighDock'
00548D20: movs       r2, #8
00548D22: bl         #0x4e02b2
00548D26: movs       r5, #0
00548D28: movs       r0, #2
00548D2A: str        r5, [sp, #0x30]
00548D2C: strd       r5, r5, [sp, #0x28]
00548D30: strb.w     r0, [sp, #0x24]
00548D34: add.w      r0, r6, #0x24
00548D38: adr        r1, #0x194 ; ADR[00548ED0]=b'HeightCarry'
00548D3A: movs       r2, #0xb
00548D3C: bl         #0x4e02b2
00548D40: movs       r0, #3
00548D42: str        r5, [sp, #0x40]
00548D44: strd       r5, r5, [sp, #0x38]
00548D48: strb.w     r0, [sp, #0x34]
00548D4C: add.w      r0, r6, #0x34
00548D50: adr        r1, #0x188 ; ADR[00548EDC]=b'OutOfFOV'
00548D52: movs       r2, #8
00548D54: bl         #0x4e02b2
00548D58: ldr        r0, [pc, #0x18c] ; literal[00548EE8]=00B11BB6
00548D5A: movs       r7, #0
00548D5C: ldr        r5, [pc, #0x18c] ; literal[00548EEC]=00B11BB4
00548D5E: add        r0, pc
00548D60: add        r5, pc
00548D62: mov        r1, r0
00548D64: str        r7, [r0, #8]
00548D66: str        r7, [r1, #4]!
00548D6A: str        r1, [r0]
00548D6C: adds       r2, r6, r7
00548D6E: adds       r1, r5, #4
00548D70: mov        r0, r5
00548D72: mov        r3, r2
00548D74: blx        #0x4ab1e8 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetENS_12basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEEEEENS_19__map_value_compareIS5_SC_NS_4lessIS5_EELb1EEENS9_ISC_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_SB_EEEEENS_15__tree_iteratorISC_PNS_11__tree_nodeISC_PvEEiEENS_21__tree_const_iteratorISC_ST_iEERKT_DpOT0_ -> 0054D976 size=32
00548D78: adds       r7, #0x10
00548D7A: cmp        r7, #0x40
00548D7C: bne        #0x548d6c
00548D7E: movs       r5, #0
00548D80: adds       r0, r6, r5
00548D82: ldrb.w     r1, [r0, #0x34]
00548D86: lsls       r1, r1, #0x1f
00548D88: itt        ne
00548D8A: ldrne      r0, [r0, #0x3c]
00548D8C: blxne      #0x4a40cc
00548D90: subs       r5, #0x10
00548D92: adds.w     r0, r5, #0x40
00548D96: bne        #0x548d80
00548D98: ldr        r0, [pc, #0x15c] ; literal[00548EF8]=0000023B
00548D9A: ldr        r1, [pc, #0x160] ; literal[00548EFC]=00B11B74
00548D9C: ldr        r2, [pc, #0x160] ; literal[00548F00]=00B0825A
00548D9E: add        r0, pc
00548DA0: add        r1, pc
00548DA2: add        r2, pc
00548DA4: blx        #0x4a4024 ; __cxa_atexit IMPORT (resolve packaged dependencies before calling external)
00548DA8: ldr        r0, [pc, #0x158] ; literal[00548F04]=00B11B76
00548DAA: add        r0, pc
00548DAC: blx        #0x4a475c ; __cxa_guard_release IMPORT (resolve packaged dependencies before calling external)
00548DB0: ldr        r0, [pc, #0x154] ; literal[00548F08]=00B11B7E
00548DB2: add        r0, pc
00548DB4: ldrb       r0, [r0]
00548DB6: dmb        ish
00548DBA: tst.w      r0, #1
00548DBE: bne        #0x548df8
00548DC0: ldr        r0, [pc, #0x148] ; literal[00548F0C]=00B11B6E
00548DC2: add        r0, pc
00548DC4: blx        #0x4a472c ; __cxa_guard_acquire IMPORT (resolve packaged dependencies before calling external)
00548DC8: cbz        r0, #0x548df8
00548DCA: ldr        r0, [pc, #0x144] ; literal[00548F10]=00B11B56
00548DCC: movs       r1, #0
00548DCE: add        r0, pc
00548DD0: strd       r1, r1, [r0]
00548DD4: str        r1, [r0, #8]
00548DD6: adr        r1, #0x13c ; ADR[00548F14]=b'UnknownPreset'
00548DD8: movs       r2, #0xd
00548DDA: bl         #0x4e02b2
00548DDE: ldr        r0, [pc, #0x148] ; literal[00548F28]=00AF5B56
00548DE0: ldr        r1, [pc, #0x148] ; literal[00548F2C]=00B11B3E
00548DE2: add        r0, pc
00548DE4: ldr        r2, [pc, #0x148] ; literal[00548F30]=00B08212
00548DE6: add        r1, pc
00548DE8: ldr        r0, [r0]
00548DEA: add        r2, pc
00548DEC: blx        #0x4a4024 ; __cxa_atexit IMPORT (resolve packaged dependencies before calling external)
00548DF0: ldr        r0, [pc, #0x140] ; literal[00548F34]=00B11B3E
00548DF2: add        r0, pc
00548DF4: blx        #0x4a475c ; __cxa_guard_release IMPORT (resolve packaged dependencies before calling external)
00548DF8: ldr        r0, [pc, #0x13c] ; literal[00548F38]=00B11B1A
00548DFA: add        r0, pc
00548DFC: ldr        r2, [r0, #4]
00548DFE: cmp        r2, #0
00548E00: beq        #0x548e40
00548E02: ldr        r0, [pc, #0x138] ; literal[00548F3C]=00B11B10
00548E04: add        r0, pc
00548E06: adds       r0, #4
00548E08: mov        r1, r2
00548E0A: ldrb       r2, [r1, #0x10]
00548E0C: cmp        r2, r4
00548E0E: bhs        #0x548e18
00548E10: ldr        r1, [r1, #4]
00548E12: cmp        r1, #0
00548E14: bne        #0x548e0a
00548E16: b          #0x548e22
00548E18: ldr        r2, [r1]
00548E1A: mov        r0, r1
00548E1C: cmp        r2, #0
00548E1E: bne        #0x548e08
00548E20: b          #0x548e24
00548E22: mov        r1, r0
00548E24: ldr        r0, [pc, #0x118] ; literal[00548F40]=00B11AEE
00548E26: add        r0, pc
00548E28: adds       r0, #4
00548E2A: cmp        r1, r0
00548E2C: beq        #0x548e40
00548E2E: ldr        r0, [pc, #0x114] ; literal[00548F44]=00B11AF2
00548E30: ldrb       r2, [r1, #0x10]
00548E32: add        r0, pc
00548E34: cmp        r2, r4
00548E36: it         ls
00548E38: addls.w    r0, r1, #0x14
00548E3C: add        sp, #0x44
00548E3E: pop        {r4, r5, r6, r7, pc}
00548E40: ldr        r0, [pc, #0x104] ; literal[00548F48]=00B11AE2
00548E42: add        r0, pc
00548E44: add        sp, #0x44
00548E46: pop        {r4, r5, r6, r7, pc}
_ZNKSt6__ndk13mapIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfNS_4lessIS4_EENS_9allocatorINS_4pairIKS4_fEEEEE2atERS9_

RANGE 00548F5C..00548FDC _ZNKSt6__ndk13mapIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfNS_4lessIS4_EENS_9allocatorINS_4pairIKS4_fEEEEE2atERS9_
00548F5C: push       {r4, r5, r7, lr}
00548F5E: ldr        r0, [r0, #4]
00548F60: cbz        r0, #0x548f78
00548F62: ldrb       r1, [r1]
00548F64: ldrb       r2, [r0, #0x10]
00548F66: cmp        r1, r2
00548F68: bhs        #0x548f6e
00548F6A: ldr        r0, [r0]
00548F6C: b          #0x548f74
00548F6E: cmp        r2, r1
00548F70: bhs        #0x548fa2
00548F72: ldr        r0, [r0, #4]
00548F74: cmp        r0, #0
00548F76: bne        #0x548f64
00548F78: movs       r0, #8
00548F7A: blx        #0x4a42b8 ; __cxa_allocate_exception IMPORT (resolve packaged dependencies before calling external)
00548F7E: mov        r4, r0
00548F80: adr        r1, #0x34 ; ADR[00548FB8]=b'map::at:  key not found'
00548F82: blx        #0x4a42c4 ; _ZNSt11logic_errorC2EPKc IMPORT (resolve packaged dependencies before calling external)
00548F86: ldr        r0, [pc, #0x48] ; literal[00548FD0]=00AF587C
00548F88: ldr        r1, [pc, #0x48] ; literal[00548FD4]=00AF587E
00548F8A: ldr        r2, [pc, #0x4c] ; literal[00548FD8]=00AF5880
00548F8C: add        r0, pc
00548F8E: add        r1, pc
00548F90: add        r2, pc
00548F92: ldr        r0, [r0]
00548F94: ldr        r1, [r1]
00548F96: ldr        r2, [r2]
00548F98: adds       r0, #8
00548F9A: str        r0, [r4]
00548F9C: mov        r0, r4
00548F9E: blx        #0x4a42d0 ; __cxa_throw IMPORT (resolve packaged dependencies before calling external)
00548FA2: cmp        r0, #0
00548FA4: beq        #0x548f78
00548FA6: adds       r0, #0x14
00548FA8: pop        {r4, r5, r7, pc}
00548FAA: mov        r5, r0
00548FAC: mov        r0, r4
00548FAE: blx        #0x4a42dc ; __cxa_free_exception IMPORT (resolve packaged dependencies before calling external)
00548FB2: mov        r0, r5
00548FB4: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
00548FB8: str        r5, [r5, #0x14]
00548FBA: subs       r2, #0x70
00548FBC: str        r2, [r7, #0x10]
00548FBE: subs       r2, #0x74
00548FC0: movs       r0, #0x20
00548FC2: str        r3, [r5, #0x54]
00548FC4: movs       r0, #0x79
00548FC6: ldr        r6, [r5, #0x74]
00548FC8: movs       r0, #0x74
00548FCA: ldr        r6, [r4, #0x74]
00548FCC: ldr        r5, [r6, #0x64]
00548FCE: lsls       r4, r4, #1
00548FD0: ldr        r4, [r7, r1]
00548FD2: lsls       r7, r5, #2
00548FD4: ldr        r6, [r7, r1]
00548FD6: lsls       r7, r5, #2
00548FD8: ldr        r0, [r0, r2]
00548FDA: lsls       r7, r5, #2
_ZNK4Anki5Cozmo22MoveLiftToHeightAction16IsLiftInPositionEv; bounded L8 lift in position

RANGE 00548FEA..0054903A _ZNK4Anki5Cozmo22MoveLiftToHeightAction16IsLiftInPositionEv
00548FEA: push       {r4, lr}
00548FEC: vpush      {d8}
00548FF0: mov        r4, r0
00548FF2: ldr        r0, [r4, #4]
00548FF4: vldr       s16, [r4, #0x84]
00548FF8: blx        #0x4ab200 ; _ZNK4Anki5Cozmo5Robot13GetLiftHeightEv -> 00516F64 size=38
00548FFC: vmov       s0, r0
00549000: movs       r0, #0
00549002: vsub.f32   s0, s16, s0
00549006: vcmpe.f32  s0, #0
0054900A: vmrs       apsr_nzcv, fpscr
0054900E: vneg.f32   s2, s0
00549012: it         mi
00549014: vmovmi.f32 s0, s2
00549018: vldr       s2, [r4, #0x7c]
0054901C: vcmpe.f32  s0, s2
00549020: vmrs       apsr_nzcv, fpscr
00549024: bpl        #0x549034
00549026: ldr        r1, [r4, #4]
00549028: ldr.w      r1, [r1, #0x254]
0054902C: ldrb       r1, [r1, #0xb]
0054902E: cmp        r1, #0
00549030: it         eq
00549032: moveq      r0, #1
00549034: vpop       {d8}
00549038: pop        {r4, pc}
_ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_fEEEEENS_15__tree_iteratorIS6_PNS_11__tree_nodeIS6_PvEEiEENS_21__tree_const_iteratorIS6_SO_iEERKT_DpOT0_

RANGE 0054D830..0054D868 _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_fEEEEENS_15__tree_iteratorIS6_PNS_11__tree_nodeIS6_PvEEiEENS_21__tree_const_iteratorIS6_SO_iEERKT_DpOT0_
0054D830: push       {r4, r5, r6, r7, lr}
0054D832: sub        sp, #4
0054D834: mov        r5, r3
0054D836: mov        r3, r2
0054D838: mov        r2, sp
0054D83A: mov        r4, r0
0054D83C: blx        #0x4ab470 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE12__find_equalIS5_EERPNS_16__tree_node_baseIPvEENS_21__tree_const_iteratorIS6_PNS_11__tree_nodeIS6_SG_EEiEESJ_RKT_ -> 0054D868 size=A6
0054D840: mov        r6, r0
0054D842: ldr        r7, [r6]
0054D844: cbnz       r7, #0x54d862
0054D846: movs       r0, #0x18
0054D848: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
0054D84C: mov        r7, r0
0054D84E: ldrd       r0, r1, [r5]
0054D852: strd       r0, r1, [r7, #0x10]
0054D856: mov        r0, r4
0054D858: ldr        r1, [sp]
0054D85A: mov        r2, r6
0054D85C: mov        r3, r7
0054D85E: blx        #0x4ab47c ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetEfEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE16__insert_node_atEPNS_16__tree_node_baseIPvEERSH_SH_ -> 0054D90E size=2A
0054D862: mov        r0, r7
0054D864: add        sp, #4
0054D866: pop        {r4, r5, r6, r7, pc}
_ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetENS_12basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEEEEENS_19__map_value_compareIS5_SC_NS_4lessIS5_EELb1EEENS9_ISC_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_SB_EEEEENS_15__tree_iteratorISC_PNS_11__tree_nodeISC_PvEEiEENS_21__tree_const_iteratorISC_ST_iEERKT_DpOT0_

RANGE 0054D976..0054D9A8 _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetENS_12basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEEEEENS_19__map_value_compareIS5_SC_NS_4lessIS5_EELb1EEENS9_ISC_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_SB_EEEEENS_15__tree_iteratorISC_PNS_11__tree_nodeISC_PvEEiEENS_21__tree_const_iteratorISC_ST_iEERKT_DpOT0_
0054D976: push       {r4, r5, r6, lr}
0054D978: sub        sp, #0x10
0054D97A: mov        r5, r3
0054D97C: mov        r3, r2
0054D97E: add        r2, sp, #0xc
0054D980: mov        r4, r0
0054D982: blx        #0x4ab494 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetENS_12basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEEEEENS_19__map_value_compareIS5_SC_NS_4lessIS5_EELb1EEENS9_ISC_EEE12__find_equalIS5_EERPNS_16__tree_node_baseIPvEENS_21__tree_const_iteratorISC_PNS_11__tree_nodeISC_SL_EEiEESO_RKT_ -> 0054D9A8 size=A6
0054D986: mov        r6, r0
0054D988: ldr        r0, [r6]
0054D98A: cbnz       r0, #0x54d9a4
0054D98C: mov        r0, sp
0054D98E: mov        r1, r4
0054D990: mov        r2, r5
0054D992: blx        #0x4ab4a0 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetENS_12basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEEEEENS_19__map_value_compareIS5_SC_NS_4lessIS5_EELb1EEENS9_ISC_EEE16__construct_nodeIJRKNS_4pairIKS5_SB_EEEEENS_10unique_ptrINS_11__tree_nodeISC_PvEENS_22__tree_node_destructorINS9_ISS_EEEEEEDpOT_ -> 0054DA4E size=60
0054D996: ldr        r3, [sp]
0054D998: mov        r0, r4
0054D99A: ldr        r1, [sp, #0xc]
0054D99C: mov        r2, r6
0054D99E: blx        #0x4ab4ac ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo22MoveLiftToHeightAction6PresetENS_12basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEEEEENS_19__map_value_compareIS5_SC_NS_4lessIS5_EELb1EEENS9_ISC_EEE16__insert_node_atEPNS_16__tree_node_baseIPvEERSM_SM_ -> 0054DAAE size=2A
0054D9A2: ldr        r0, [sp]
0054D9A4: add        sp, #0x10
0054D9A6: pop        {r4, r5, r6, pc}
_ZNKSt6__ndk18functionIFvvEEclEv; bounded I6 callback invoke

RANGE 005BF7BC..005BF7F8 _ZNKSt6__ndk18functionIFvvEEclEv
005BF7BC: ldr        r0, [r0, #0x10]
005BF7BE: cbz        r0, #0x5bf7c6
005BF7C0: ldr        r1, [r0]
005BF7C2: ldr        r1, [r1, #0x18]
005BF7C4: bx         r1
005BF7C6: push       {r7, lr}
005BF7C8: movs       r0, #4
005BF7CA: blx        #0x4a42b8 ; __cxa_allocate_exception IMPORT (resolve packaged dependencies before calling external)
005BF7CE: ldr        r1, [pc, #0x1c] ; literal[005BF7EC]=00A7F1E2
005BF7D0: ldr        r2, [pc, #0x1c] ; literal[005BF7F0]=00A7F1E4
005BF7D2: ldr.w      ip, [pc, #0x20] ; literal[005BF7F4]=00A7F1E6
005BF7D6: add        r1, pc
005BF7D8: add        r2, pc
005BF7DA: add        ip, pc
005BF7DC: ldr        r3, [r1]
005BF7DE: ldr        r1, [r2]
005BF7E0: ldr.w      r2, [ip]
005BF7E4: adds       r3, #8
005BF7E6: str        r3, [r0]
005BF7E8: blx        #0x4a42d0 ; __cxa_throw IMPORT (resolve packaged dependencies before calling external)
_ZN4Anki5Cozmo16AnimTrackHelpers22AnimTrackFlagsToStringEh; bounded D2 track strings

RANGE 006305E8..00630788 _ZN4Anki5Cozmo16AnimTrackHelpers22AnimTrackFlagsToStringEh
006305E8: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
006305EC: sub        sp, #0xac
006305EE: mov        r7, r1
006305F0: mov        r6, r0
006305F2: cmp        r7, #0xff
006305F4: beq        #0x63060a
006305F6: cbnz       r7, #0x630630
006305F8: movs       r0, #0
006305FA: movs       r5, #0
006305FC: blx        #0x4b8cac ; _ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE -> 007BC250 size=88
00630600: mov        r4, r0
00630602: strd       r5, r5, [r6]
00630606: str        r5, [r6, #8]
00630608: b          #0x63061a
0063060A: movs       r0, #0xff
0063060C: blx        #0x4b8cac ; _ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE -> 007BC250 size=88
00630610: mov        r4, r0
00630612: movs       r0, #0
00630614: strd       r0, r0, [r6]
00630618: str        r0, [r6, #8]
0063061A: mov        r0, r4
0063061C: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
00630620: mov        r2, r0
00630622: mov        r0, r6
00630624: mov        r1, r4
00630626: bl         #0x4e02b2
0063062A: add        sp, #0xac
0063062C: pop.w      {r4, r5, r6, r7, r8, sb, sl, fp, pc}
00630630: ldr        r0, [pc, #0x1e0] ; literal[00630814]=00A0E17C
00630632: add.w      sb, sp, #0x10
00630636: ldr        r1, [pc, #0x1e0] ; literal[00630818]=00A0E17A
00630638: add.w      sl, sb, #0x40
0063063C: add        r0, pc
0063063E: add.w      fp, sb, #0xc
00630642: add        r1, pc
00630644: movs       r5, #0
00630646: ldr        r0, [r0]
00630648: ldr        r1, [r1]
0063064A: add.w      r2, r0, #0xc
0063064E: adds       r0, #0x20
00630650: adds       r1, #0x20
00630652: str        r5, [sp, #0x14]
00630654: str        r2, [sp, #0x10]
00630656: str        r1, [sp, #0x18]
00630658: str        r0, [sp, #0x50]
0063065A: mov        r0, sl
0063065C: mov        r1, fp
0063065E: blx        #0x4a445c ; _ZNSt6__ndk18ios_base4initEPv IMPORT (resolve packaged dependencies before calling external)
00630662: ldr        r0, [pc, #0x1b8] ; literal[0063081C]=00A0E14E
00630664: mov.w      r2, #-1
00630668: ldr        r1, [pc, #0x1b4] ; literal[00630820]=00A0E14E
0063066A: add.w      r4, sb, #0x10
0063066E: add        r0, pc
00630670: str        r2, [sp, #0x9c]
00630672: add        r1, pc
00630674: str        r5, [sp, #0x98]
00630676: ldr        r0, [r0]
00630678: ldr        r1, [r1]
0063067A: add.w      r2, r0, #0x34
0063067E: str        r2, [sp, #0x50]
00630680: add.w      r2, r0, #0xc
00630684: adds       r0, #0x20
00630686: str        r2, [sp, #0x10]
00630688: str        r0, [sp, #0x18]
0063068A: add.w      r0, r1, #8
0063068E: str        r0, [sp, #0x1c]
00630690: mov        r0, r4
00630692: blx        #0x4a4468 ; _ZNSt6__ndk16localeC1Ev IMPORT (resolve packaged dependencies before calling external)
00630696: add.w      r0, sb, #0x14
0063069A: movs       r1, #0x18
0063069C: mov.w      r8, #0x18
006306A0: blx        #0x4a403c ; __aeabi_memclr4 IMPORT (resolve packaged dependencies before calling external)
006306A4: ldr        r0, [pc, #0x17c] ; literal[00630824]=00A0E11C
006306A6: str        r5, [sp, #0x3c]
006306A8: add        r0, pc
006306AA: ldr        r0, [r0]
006306AC: adds       r0, #8
006306AE: str        r0, [sp, #0x1c]
006306B0: strd       r5, r5, [sp, #0x40]
006306B4: strd       r5, r8, [sp, #0x48]
006306B8: str        r5, [sp, #0xa8]
006306BA: strd       r5, r5, [sp, #0xa0]
006306BE: add        r1, sp, #0xa0
006306C0: mov        r0, fp
006306C2: bl         #0x4e4598
006306C6: strd       fp, r4, [sp, #4]
006306CA: ldrb.w     r0, [sp, #0xa0]
006306CE: str.w      sl, [sp, #0xc]
006306D2: lsls       r0, r0, #0x1f
006306D4: itt        ne
006306D6: ldrne      r0, [sp, #0xa8]
006306D8: blxne      #0x4a40cc
006306DC: add.w      r5, sb, #8
006306E0: add.w      fp, sp, #0xa0
006306E4: mov.w      r8, #1
006306E8: mov.w      sb, #-1
006306EC: mov.w      sl, #0x2b
006306F0: movs       r0, #1
006306F2: add.w      sb, sb, #1
006306F6: lsl.w      r4, r8, sb
006306FA: tst        r4, r7
006306FC: beq        #0x63072a
006306FE: lsls       r0, r0, #0x1f
00630700: bne        #0x630710
00630702: strb.w     sl, [sp, #0xa0]
00630706: mov        r0, r5
00630708: mov        r1, fp
0063070A: movs       r2, #1
0063070C: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
00630710: uxtb       r0, r4
00630712: blx        #0x4b8cac ; _ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE -> 007BC250 size=88
00630716: mov        r4, r0
00630718: mov        r0, r4
0063071A: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063071E: mov        r2, r0
00630720: mov        r0, r5
00630722: mov        r1, r4
00630724: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
00630728: movs       r0, #0
0063072A: cmp.w      sb, #7
0063072E: blt        #0x6306f2
00630730: ldr        r1, [sp, #4]
00630732: mov        r0, r6
00630734: blx        #0x4a448c ; _ZNKSt6__ndk115basic_stringbufIcNS_11char_traitsIcEENS_9allocatorIcEEE3strEv -> 004E4784 size=4A
00630738: ldr        r0, [pc, #0xfc] ; literal[00630838]=00A0E080
0063073A: ldr        r1, [pc, #0x100] ; literal[0063083C]=00A0E082
0063073C: add        r0, pc
0063073E: ldrb.w     r2, [sp, #0x3c]
00630742: add        r1, pc
00630744: ldr        r0, [r0]
00630746: ldr        r1, [r1]
00630748: add.w      r3, r0, #0x34
0063074C: str        r3, [sp, #0x50]
0063074E: add.w      r3, r0, #0xc
00630752: adds       r0, #0x20
00630754: str        r3, [sp, #0x10]
00630756: str        r0, [sp, #0x18]
00630758: add.w      r0, r1, #8
0063075C: str        r0, [sp, #0x1c]
0063075E: lsls       r0, r2, #0x1f
00630760: ldrd       r5, r4, [sp, #8]
00630764: itt        ne
00630766: ldrne      r0, [sp, #0x44]
00630768: blxne      #0x4a40cc
0063076C: ldr        r0, [pc, #0xd0] ; literal[00630840]=00A0E052
0063076E: add        r0, pc
00630770: ldr        r0, [r0]
00630772: adds       r0, #8
00630774: str        r0, [sp, #0x1c]
00630776: mov        r0, r5
00630778: blx        #0x4a44a4 ; _ZNSt6__ndk16localeD1Ev IMPORT (resolve packaged dependencies before calling external)
0063077C: mov        r0, r4
0063077E: blx        #0x4a44b0 ; _ZNSt6__ndk18ios_baseD2Ev IMPORT (resolve packaged dependencies before calling external)
00630782: add        sp, #0xac
00630784: pop.w      {r4, r5, r6, r7, r8, sb, sl, fp, pc}
_ZNSt6__ndk112__deque_baseItNS_9allocatorItEEED2Ev

RANGE 00633FD2..00633FF8 _ZNSt6__ndk112__deque_baseItNS_9allocatorItEEED2Ev
00633FD2: push       {r4, r5, r6, lr}
00633FD4: mov        r4, r0
00633FD6: blx        #0x4b8f10 ; _ZNSt6__ndk112__deque_baseItNS_9allocatorItEEE5clearEv -> 00634B28 size=94
00633FDA: ldrd       r5, r6, [r4, #4]
00633FDE: cmp        r5, r6
00633FE0: beq        #0x633fee
00633FE2: ldr        r0, [r5], #4
00633FE6: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00633FEA: cmp        r6, r5
00633FEC: bne        #0x633fe2
00633FEE: mov        r0, r4
00633FF0: pop.w      {r4, r5, r6, lr}
00633FF4: b.w        #0x8ccc8c
_ZN4Anki5Cozmo20CliffSensorComponentD1Ev; bounded T6 Cliff dtor

RANGE 00633FF8..00634016 _ZN4Anki5Cozmo20CliffSensorComponentD2Ev
00633FF8: push       {r4, lr}
00633FFA: mov        r4, r0
00633FFC: movs       r1, #0
00633FFE: ldr        r0, [r4, #0x44]
00634000: str        r1, [r4, #0x44]
00634002: cbz        r0, #0x63400a
00634004: ldr        r1, [r0]
00634006: ldr        r1, [r1, #4]
00634008: blx        r1
0063400A: add.w      r0, r4, #0x20
0063400E: blx        #0x4b8f28 ; _ZNSt6__ndk112__deque_baseItNS_9allocatorItEEED2Ev -> 00633FD2 size=26
00634012: mov        r0, r4
00634014: pop        {r4, pc}
_ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeAccelComponent12AccelHistoryEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE; bounded T17 cube accel history tree

RANGE 00635D84..00635DBC _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeAccelComponent12AccelHistoryEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE
00635D84: push       {r4, r5, r7, lr}
00635D86: mov        r4, r1
00635D88: mov        r5, r0
00635D8A: cbz        r4, #0x635dba
00635D8C: ldr        r1, [r4]
00635D8E: mov        r0, r5
00635D90: blx        #0x4b8fc4 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeAccelComponent12AccelHistoryEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 00635D84 size=38
00635D94: ldr        r1, [r4, #4]
00635D96: mov        r0, r5
00635D98: blx        #0x4b8fc4 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki8ObjectIDENS2_5Cozmo18CubeAccelComponent12AccelHistoryEEENS_19__map_value_compareIS3_S7_NS_4lessIS3_EELb1EEENS_9allocatorIS7_EEE7destroyEPNS_11__tree_nodeIS7_PvEE -> 00635D84 size=38
00635D9C: ldr        r1, [r4, #0x2c]
00635D9E: add.w      r0, r4, #0x28
00635DA2: blx        #0x4b9000 ; _ZNSt6__ndk16__treeINS_10shared_ptrIN4Anki5Cozmo18CubeAccelListeners18ICubeAccelListenerEEENS_4lessIS6_EENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE -> 00635DBC size=2C
00635DA6: ldr        r1, [r4, #0x20]
00635DA8: add.w      r0, r4, #0x1c
00635DAC: blx        #0x4b900c ; _ZNSt6__ndk16__treeINS_12__value_typeIjN4Anki5Cozmo11ActiveAccelEEENS_19__map_value_compareIjS5_NS_4lessIjEELb1EEENS_9allocatorIS5_EEE7destroyEPNS_11__tree_nodeIS5_PvEE -> 00635DE8 size=24
00635DB0: mov        r0, r4
00635DB2: pop.w      {r4, r5, r7, lr}
00635DB6: b.w        #0x8ca88c
00635DBA: pop        {r4, r5, r7, pc}
_ZNSt6__ndk16__treeINS_10shared_ptrIN4Anki5Cozmo18CubeAccelListeners18ICubeAccelListenerEEENS_4lessIS6_EENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE

RANGE 00635DBC..00635DE8 _ZNSt6__ndk16__treeINS_10shared_ptrIN4Anki5Cozmo18CubeAccelListeners18ICubeAccelListenerEEENS_4lessIS6_EENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE
00635DBC: push       {r4, r5, r7, lr}
00635DBE: mov        r4, r1
00635DC0: mov        r5, r0
00635DC2: cbz        r4, #0x635de6
00635DC4: ldr        r1, [r4]
00635DC6: mov        r0, r5
00635DC8: blx        #0x4b9000 ; _ZNSt6__ndk16__treeINS_10shared_ptrIN4Anki5Cozmo18CubeAccelListeners18ICubeAccelListenerEEENS_4lessIS6_EENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE -> 00635DBC size=2C
00635DCC: ldr        r1, [r4, #4]
00635DCE: mov        r0, r5
00635DD0: blx        #0x4b9000 ; _ZNSt6__ndk16__treeINS_10shared_ptrIN4Anki5Cozmo18CubeAccelListeners18ICubeAccelListenerEEENS_4lessIS6_EENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE -> 00635DBC size=2C
00635DD4: ldr        r0, [r4, #0x14]
00635DD6: cbz        r0, #0x635ddc
00635DD8: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
00635DDC: mov        r0, r4
00635DDE: pop.w      {r4, r5, r7, lr}
00635DE2: b.w        #0x8ca88c
00635DE6: pop        {r4, r5, r7, pc}
_ZNSt6__ndk16__treeINS_12__value_typeIjN4Anki5Cozmo11ActiveAccelEEENS_19__map_value_compareIjS5_NS_4lessIjEELb1EEENS_9allocatorIS5_EEE7destroyEPNS_11__tree_nodeIS5_PvEE

RANGE 00635DE8..00635E0C _ZNSt6__ndk16__treeINS_12__value_typeIjN4Anki5Cozmo11ActiveAccelEEENS_19__map_value_compareIjS5_NS_4lessIjEELb1EEENS_9allocatorIS5_EEE7destroyEPNS_11__tree_nodeIS5_PvEE
00635DE8: push       {r4, r5, r7, lr}
00635DEA: mov        r4, r1
00635DEC: mov        r5, r0
00635DEE: cbz        r4, #0x635e0a
00635DF0: ldr        r1, [r4]
00635DF2: mov        r0, r5
00635DF4: blx        #0x4b900c ; _ZNSt6__ndk16__treeINS_12__value_typeIjN4Anki5Cozmo11ActiveAccelEEENS_19__map_value_compareIjS5_NS_4lessIjEELb1EEENS_9allocatorIS5_EEE7destroyEPNS_11__tree_nodeIS5_PvEE -> 00635DE8 size=24
00635DF8: ldr        r1, [r4, #4]
00635DFA: mov        r0, r5
00635DFC: blx        #0x4b900c ; _ZNSt6__ndk16__treeINS_12__value_typeIjN4Anki5Cozmo11ActiveAccelEEENS_19__map_value_compareIjS5_NS_4lessIjEELb1EEENS_9allocatorIS5_EEE7destroyEPNS_11__tree_nodeIS5_PvEE -> 00635DE8 size=24
00635E00: mov        r0, r4
00635E02: pop.w      {r4, r5, r7, lr}
00635E06: b.w        #0x8ca88c
00635E0A: pop        {r4, r5, r7, pc}
_ZNK4Anki5Cozmo16DockingComponent12AbortDockingEv; bounded A5 Dock abort

RANGE 0063BE10..0063BE5E _ZNK4Anki5Cozmo16DockingComponent12AbortDockingEv
0063BE10: push       {r4, r5, r7, lr}
0063BE12: sub.w      sp, sp, #0x410
0063BE16: ldr        r1, [pc, #0x5c] ; literal[0063BE74]=00A02A2E
0063BE18: add        r5, sp, #4
0063BE1A: add        r1, pc
0063BE1C: ldr        r1, [r1]
0063BE1E: ldr        r1, [r1]
0063BE20: str.w      r1, [sp, #0x40c]
0063BE24: mov        r1, sp
0063BE26: ldr        r4, [r0]
0063BE28: mov        r0, r5
0063BE2A: blx        #0x4b94c8 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS0_12AbortDockingE -> 007A8DF4 size=6
0063BE2E: mov        r0, r4
0063BE30: mov        r1, r5
0063BE32: movs       r2, #1
0063BE34: movs       r3, #0
0063BE36: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
0063BE3A: mov        r4, r0
0063BE3C: add        r0, sp, #4
0063BE3E: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
0063BE42: ldr        r0, [pc, #0x34] ; literal[0063BE78]=00A02A00
0063BE44: ldr.w      r1, [sp, #0x40c]
0063BE48: add        r0, pc
0063BE4A: ldr        r0, [r0]
0063BE4C: ldr        r0, [r0]
0063BE4E: subs       r0, r0, r1
0063BE50: ittt       eq
0063BE52: moveq      r0, r4
0063BE54: addeq.w    sp, sp, #0x410
0063BE58: popeq      {r4, r5, r7, pc}
0063BE5A: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
_ZNK4Anki5Cozmo17MovementComponent18AreAllTracksLockedEh

RANGE 0063EB40..0063EBF4 _ZNK4Anki5Cozmo17MovementComponent18AreAllTracksLockedEh
0063EB40: push       {r4, lr}
0063EB42: sub        sp, #0x10
0063EB44: cbz        r1, #0x63eb64
0063EB46: adds       r0, #0x30
0063EB48: mov.w      r2, #-1
0063EB4C: lsls       r3, r1, #0x1f
0063EB4E: beq        #0x63eb54
0063EB50: ldr        r3, [r0]
0063EB52: cbz        r3, #0x63eb9a
0063EB54: adds       r2, #1
0063EB56: adds       r0, #0xc
0063EB58: ubfx       r1, r1, #1, #7
0063EB5C: cmp        r2, #7
0063EB5E: blt        #0x63eb4c
0063EB60: movs       r0, #1
0063EB62: b          #0x63eb9c
0063EB64: ldr        r2, [pc, #0x60] ; literal[0063EBC8]=005BC8A6
0063EB66: movs       r0, #0
0063EB68: strd       r0, r0, [sp, #4]
0063EB6C: add        r2, pc
0063EB6E: str        r0, [sp, #0xc]
0063EB70: adr        r0, #0x58 ; ADR[0063EBCC]=b'MovementComponent.AreAllTracksLocked'
0063EB72: add        r1, sp, #4
0063EB74: blx        #0x4a4540 ; _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D2B4 size=80
0063EB78: ldr        r0, [sp, #4]
0063EB7A: cbz        r0, #0x63eb9a
0063EB7C: ldr        r1, [sp, #8]
0063EB7E: cmp        r1, r0
0063EB80: itttt      ne
0063EB82: subne.w    r2, r1, #8
0063EB86: subne      r2, r2, r0
0063EB88: mvnne      r3, #7
0063EB8C: bicne.w    r2, r3, r2
0063EB90: itt        ne
0063EB92: addne      r1, r1, r2
0063EB94: strne      r1, [sp, #8]
0063EB96: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0063EB9A: movs       r0, #0
0063EB9C: add        sp, #0x10
0063EB9E: pop        {r4, pc}
0063EBA0: mov        r4, r0
0063EBA2: ldr        r0, [sp, #4]
0063EBA4: cbz        r0, #0x63ebc2
0063EBA6: ldr        r1, [sp, #8]
0063EBA8: cmp        r1, r0
0063EBAA: beq        #0x63ebbe
0063EBAC: sub.w      r2, r1, #8
0063EBB0: mvn        r3, #7
0063EBB4: subs       r2, r2, r0
0063EBB6: bic.w      r2, r3, r2
0063EBBA: add        r1, r2
0063EBBC: str        r1, [sp, #8]
0063EBBE: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0063EBC2: mov        r0, r4
0063EBC4: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
0063EBC8: ldm        r0!, {r1, r2, r5, r7}
0063EBCA: lsls       r3, r3, #1
0063EBCC: ldr        r5, [r1, #0x74]
0063EBCE: str        r6, [r6, #0x54]
0063EBD0: str        r5, [r5, #0x54]
0063EBD2: strb       r6, [r5, #0x11]
0063EBD4: ldr        r3, [r0, #0x74]
0063EBD6: strb       r5, [r5, #1]
0063EBD8: ldr        r7, [r5, #0x64]
0063EBDA: ldr        r5, [r4, #0x64]
0063EBDC: cmp        r6, #0x74
0063EBDE: strb       r1, [r0, #9]
0063EBE0: adcs       r5, r4
0063EBE2: ldr        r4, [r5, #0x44]
0063EBE4: strb       r4, [r2, #9]
0063EBE6: str        r1, [r4, #0x34]
0063EBE8: strb       r3, [r5, #0xd]
0063EBEA: ldr        r4, [r1, #0x74]
0063EBEC: ldr        r3, [r4, #0x34]
0063EBEE: str        r5, [r4, #0x44]
0063EBF0: movs       r0, r0
0063EBF2: movs       r0, r0
_ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_; bounded A9 direct zero unlock

RANGE 0063EFB0..0063F0BC _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_
0063EFB0: push.w     {r4, r5, r6, r7, r8, lr}
0063EFB4: sub        sp, #0x28
0063EFB6: vmov       s0, r1
0063EFBA: mov        r4, r3
0063EFBC: mov        r7, r0
0063EFBE: vcmpe.f32  s0, #0
0063EFC2: vmrs       apsr_nzcv, fpscr
0063EFC6: vneg.f32   s2, s0
0063EFCA: it         mi
0063EFCC: vmovmi.f32 s0, s2
0063EFD0: vldr       s2, [pc, #0x124] ; literal[0063F0F8]=3727C5AC
0063EFD4: ldrd       r6, r5, [sp, #0x40]
0063EFD8: vcmpe.f32  s0, s2
0063EFDC: vmrs       apsr_nzcv, fpscr
0063EFE0: bpl        #0x63f096
0063EFE2: movs       r0, #0
0063EFE4: mov        r1, r4
0063EFE6: strb       r0, [r2]
0063EFE8: mov        r0, r7
0063EFEA: blx        #0x4b9660 ; _ZNK4Anki5Cozmo17MovementComponent18AreAllTracksLockedEh -> 0063EB40 size=B4
0063EFEE: cmp        r0, #1
0063EFF0: bne        #0x63f0a4
0063EFF2: mov        r0, r7
0063EFF4: mov        r1, r4
0063EFF6: mov        r2, r6
0063EFF8: blx        #0x4a5794 ; _ZN4Anki5Cozmo17MovementComponent12UnlockTracksEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE -> 0063FE5C size=23C
0063EFFC: cmp        r0, #1
0063EFFE: bne        #0x63f0a4
0063F000: movs       r0, #0
0063F002: strd       r0, r0, [sp, #0x1c]
0063F006: str        r0, [sp, #0x24]
0063F008: add.w      r8, sp, #0x10
0063F00C: mov        r1, r4
0063F00E: mov        r0, r8
0063F010: blx        #0x4aaa98 ; _ZN4Anki5Cozmo16AnimTrackHelpers22AnimTrackFlagsToStringEh -> 006305E8 size=25C
0063F014: ldrb       r7, [r6]
0063F016: ldr        r1, [r6, #8]
0063F018: ldrb       r0, [r5]
0063F01A: tst.w      r7, #1
0063F01E: ldr        r3, [sp, #0x18]
0063F020: ldrb.w     ip, [sp, #0x10]
0063F024: ldr        r2, [r5, #8]
0063F026: it         eq
0063F028: addeq      r1, r6, #1
0063F02A: tst.w      r0, #1
0063F02E: it         eq
0063F030: addeq      r2, r5, #1
0063F032: strd       r4, r2, [sp]
0063F036: adr        r0, #0xc4 ; ADR[0063F0FC]=b'MovementComponent.DirectDriveCheckSpeedAndLockTracks'
0063F038: str        r1, [sp, #8]
0063F03A: add        r1, sp, #0x1c
0063F03C: adr        r2, #0xf4 ; ADR[0063F134]=b'Locks left on tracks %s [0x%x] after %s[%s] unlocked'
0063F03E: tst.w      ip, #1
0063F042: it         eq
0063F044: orreq      r3, r8, #1
0063F048: blx        #0x4a4108 ; _ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D13C size=80
0063F04C: ldrb.w     r0, [sp, #0x10]
0063F050: lsls       r0, r0, #0x1f
0063F052: itt        ne
0063F054: ldrne      r0, [sp, #0x18]
0063F056: blxne      #0x4a40cc
0063F05A: ldr        r0, [sp, #0x1c]
0063F05C: cbz        r0, #0x63f07c
0063F05E: ldr        r1, [sp, #0x20]
0063F060: cmp        r1, r0
0063F062: itttt      ne
0063F064: subne.w    r2, r1, #8
0063F068: subne      r2, r2, r0
0063F06A: mvnne      r3, #7
0063F06E: bicne.w    r2, r3, r2
0063F072: itt        ne
0063F074: addne      r1, r1, r2
0063F076: strne      r1, [sp, #0x20]
0063F078: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0063F07C: ldr        r0, [pc, #0xec] ; literal[0063F16C]=009FF706
0063F07E: movs       r2, #1
0063F080: ldr        r1, [pc, #0xec] ; literal[0063F170]=009FF708
0063F082: add        r0, pc
0063F084: add        r1, pc
0063F086: ldr        r0, [r0]
0063F088: ldr        r1, [r1]
0063F08A: ldrb       r0, [r0]
0063F08C: strb       r2, [r1]
0063F08E: cbz        r0, #0x63f0a4
0063F090: blx        #0x4a4114 ; _ZN4Anki4Util18sDebugBreakOnErrorEv -> 0080DAB4 size=2
0063F094: b          #0x63f0a4
0063F096: movs       r0, #1
0063F098: mov        r1, r4
0063F09A: strb       r0, [r2]
0063F09C: mov        r0, r7
0063F09E: blx        #0x4b9660 ; _ZNK4Anki5Cozmo17MovementComponent18AreAllTracksLockedEh -> 0063EB40 size=B4
0063F0A2: cbz        r0, #0x63f0aa
0063F0A4: add        sp, #0x28
0063F0A6: pop.w      {r4, r5, r6, r7, r8, pc}
0063F0AA: mov        r0, r7
0063F0AC: mov        r1, r4
0063F0AE: mov        r2, r6
0063F0B0: mov        r3, r5
0063F0B2: add        sp, #0x28
0063F0B4: pop.w      {r4, r5, r6, r7, r8, lr}
0063F0B8: b.w        #0x8ccdbc
_ZN4Anki5Cozmo17MovementComponent13StopAllMotorsEv; bounded A7 StopAllMotors

RANGE 0063FBD8..0063FE1A _ZN4Anki5Cozmo17MovementComponent13StopAllMotorsEv
0063FBD8: push.w     {r4, r5, r6, r7, r8, sb, lr}
0063FBDC: sub        sp, #0x24
0063FBDE: mov        r4, r0
0063FBE0: mov        sb, r4
0063FBE2: ldrb       r0, [sb, #0xb8]!
0063FBE6: cbnz       r0, #0x63fbf8
0063FBE8: ldrb.w     r0, [r4, #0xb9]
0063FBEC: cbnz       r0, #0x63fbf8
0063FBEE: ldrb.w     r0, [r4, #0xba]
0063FBF2: cmp        r0, #0
0063FBF4: beq.w      #0x63fe0e
0063FBF8: ldrb.w     r0, [r4, #0xd4]
0063FBFC: cmp        r0, #0
0063FBFE: bne.w      #0x63fe0e
0063FC02: ldr.w      r7, [r4, #0xc0]
0063FC06: movs       r5, #0
0063FC08: str        r5, [sp, #0x20]
0063FC0A: strd       r5, r5, [sp, #0x18]
0063FC0E: mov        r0, r7
0063FC10: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FC14: add.w      r8, sp, #0x18
0063FC18: mov        r2, r0
0063FC1A: mov        r1, r7
0063FC1C: mov        r0, r8
0063FC1E: bl         #0x4e02b2
0063FC22: ldr.w      r6, [r4, #0xc0]
0063FC26: str        r5, [sp, #0x10]
0063FC28: strd       r5, r5, [sp, #8]
0063FC2C: mov        r0, r6
0063FC2E: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FC32: mov        r2, r0
0063FC34: add        r7, sp, #8
0063FC36: mov        r1, r6
0063FC38: mov        r0, r7
0063FC3A: bl         #0x4e02b2
0063FC3E: add.w      r2, r4, #0xb9
0063FC42: mov        r0, r4
0063FC44: movs       r1, #0
0063FC46: movs       r3, #1
0063FC48: strd       r8, r7, [sp]
0063FC4C: blx        #0x4b9654 ; _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_ -> 0063EFB0 size=1C4
0063FC50: ldrb.w     r0, [sp, #8]
0063FC54: lsls       r0, r0, #0x1f
0063FC56: itt        ne
0063FC58: ldrne      r0, [sp, #0x10]
0063FC5A: blxne      #0x4a40cc
0063FC5E: ldrb.w     r0, [sp, #0x18]
0063FC62: lsls       r0, r0, #0x1f
0063FC64: itt        ne
0063FC66: ldrne      r0, [sp, #0x20]
0063FC68: blxne      #0x4a40cc
0063FC6C: ldr.w      r7, [r4, #0xc4]
0063FC70: movs       r5, #0
0063FC72: str        r5, [sp, #0x20]
0063FC74: strd       r5, r5, [sp, #0x18]
0063FC78: mov        r0, r7
0063FC7A: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FC7E: add.w      r8, sp, #0x18
0063FC82: mov        r2, r0
0063FC84: mov        r1, r7
0063FC86: mov        r0, r8
0063FC88: bl         #0x4e02b2
0063FC8C: ldr.w      r6, [r4, #0xc4]
0063FC90: str        r5, [sp, #0x10]
0063FC92: strd       r5, r5, [sp, #8]
0063FC96: mov        r0, r6
0063FC98: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FC9C: mov        r2, r0
0063FC9E: add        r7, sp, #8
0063FCA0: mov        r1, r6
0063FCA2: mov        r0, r7
0063FCA4: bl         #0x4e02b2
0063FCA8: add.w      r2, r4, #0xba
0063FCAC: mov        r0, r4
0063FCAE: movs       r1, #0
0063FCB0: movs       r3, #2
0063FCB2: strd       r8, r7, [sp]
0063FCB6: blx        #0x4b9654 ; _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_ -> 0063EFB0 size=1C4
0063FCBA: ldrb.w     r0, [sp, #8]
0063FCBE: lsls       r0, r0, #0x1f
0063FCC0: itt        ne
0063FCC2: ldrne      r0, [sp, #0x10]
0063FCC4: blxne      #0x4a40cc
0063FCC8: ldrb.w     r0, [sp, #0x18]
0063FCCC: lsls       r0, r0, #0x1f
0063FCCE: itt        ne
0063FCD0: ldrne      r0, [sp, #0x20]
0063FCD2: blxne      #0x4a40cc
0063FCD6: ldr.w      r7, [r4, #0xbc]
0063FCDA: movs       r5, #0
0063FCDC: str        r5, [sp, #0x20]
0063FCDE: strd       r5, r5, [sp, #0x18]
0063FCE2: mov        r0, r7
0063FCE4: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FCE8: add.w      r8, sp, #0x18
0063FCEC: mov        r2, r0
0063FCEE: mov        r1, r7
0063FCF0: mov        r0, r8
0063FCF2: bl         #0x4e02b2
0063FCF6: ldr.w      r6, [r4, #0xbc]
0063FCFA: str        r5, [sp, #0x10]
0063FCFC: strd       r5, r5, [sp, #8]
0063FD00: mov        r0, r6
0063FD02: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FD06: mov        r2, r0
0063FD08: add        r7, sp, #8
0063FD0A: mov        r1, r6
0063FD0C: mov        r0, r7
0063FD0E: bl         #0x4e02b2
0063FD12: mov        r0, r4
0063FD14: movs       r1, #0
0063FD16: mov        r2, sb
0063FD18: movs       r3, #4
0063FD1A: strd       r8, r7, [sp]
0063FD1E: blx        #0x4b9654 ; _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_ -> 0063EFB0 size=1C4
0063FD22: ldrb.w     r0, [sp, #8]
0063FD26: lsls       r0, r0, #0x1f
0063FD28: itt        ne
0063FD2A: ldrne      r0, [sp, #0x10]
0063FD2C: blxne      #0x4a40cc
0063FD30: ldrb.w     r0, [sp, #0x18]
0063FD34: lsls       r0, r0, #0x1f
0063FD36: itt        ne
0063FD38: ldrne      r0, [sp, #0x20]
0063FD3A: blxne      #0x4a40cc
0063FD3E: ldr.w      r7, [r4, #0xbc]
0063FD42: movs       r5, #0
0063FD44: str        r5, [sp, #0x20]
0063FD46: strd       r5, r5, [sp, #0x18]
0063FD4A: mov        r0, r7
0063FD4C: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FD50: add.w      r8, sp, #0x18
0063FD54: mov        r2, r0
0063FD56: mov        r1, r7
0063FD58: mov        r0, r8
0063FD5A: bl         #0x4e02b2
0063FD5E: ldr.w      r6, [r4, #0xc8]
0063FD62: str        r5, [sp, #0x10]
0063FD64: strd       r5, r5, [sp, #8]
0063FD68: mov        r0, r6
0063FD6A: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FD6E: mov        r2, r0
0063FD70: add        r7, sp, #8
0063FD72: mov        r1, r6
0063FD74: mov        r0, r7
0063FD76: bl         #0x4e02b2
0063FD7A: mov        r0, r4
0063FD7C: movs       r1, #0
0063FD7E: mov        r2, sb
0063FD80: movs       r3, #4
0063FD82: strd       r8, r7, [sp]
0063FD86: blx        #0x4b9654 ; _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_ -> 0063EFB0 size=1C4
0063FD8A: ldrb.w     r0, [sp, #8]
0063FD8E: lsls       r0, r0, #0x1f
0063FD90: itt        ne
0063FD92: ldrne      r0, [sp, #0x10]
0063FD94: blxne      #0x4a40cc
0063FD98: ldrb.w     r0, [sp, #0x18]
0063FD9C: lsls       r0, r0, #0x1f
0063FD9E: itt        ne
0063FDA0: ldrne      r0, [sp, #0x20]
0063FDA2: blxne      #0x4a40cc
0063FDA6: ldr.w      r7, [r4, #0xbc]
0063FDAA: movs       r5, #0
0063FDAC: str        r5, [sp, #0x20]
0063FDAE: strd       r5, r5, [sp, #0x18]
0063FDB2: mov        r0, r7
0063FDB4: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FDB8: add.w      r8, sp, #0x18
0063FDBC: mov        r2, r0
0063FDBE: mov        r1, r7
0063FDC0: mov        r0, r8
0063FDC2: bl         #0x4e02b2
0063FDC6: ldr.w      r6, [r4, #0xcc]
0063FDCA: str        r5, [sp, #0x10]
0063FDCC: strd       r5, r5, [sp, #8]
0063FDD0: mov        r0, r6
0063FDD2: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0063FDD6: mov        r2, r0
0063FDD8: add        r7, sp, #8
0063FDDA: mov        r1, r6
0063FDDC: mov        r0, r7
0063FDDE: bl         #0x4e02b2
0063FDE2: mov        r0, r4
0063FDE4: movs       r1, #0
0063FDE6: mov        r2, sb
0063FDE8: movs       r3, #4
0063FDEA: strd       r8, r7, [sp]
0063FDEE: blx        #0x4b9654 ; _ZN4Anki5Cozmo17MovementComponent34DirectDriveCheckSpeedAndLockTracksEfRbhRKNSt6__ndk112basic_stringIcNS3_11char_traitsIcEENS3_9allocatorIcEEEESB_ -> 0063EFB0 size=1C4
0063FDF2: ldrb.w     r0, [sp, #8]
0063FDF6: lsls       r0, r0, #0x1f
0063FDF8: itt        ne
0063FDFA: ldrne      r0, [sp, #0x10]
0063FDFC: blxne      #0x4a40cc
0063FE00: ldrb.w     r0, [sp, #0x18]
0063FE04: lsls       r0, r0, #0x1f
0063FE06: itt        ne
0063FE08: ldrne      r0, [sp, #0x20]
0063FE0A: blxne      #0x4a40cc
0063FE0E: ldr        r0, [r4, #4]
0063FE10: bl         #0x64099c
0063FE14: add        sp, #0x24
0063FE16: pop.w      {r4, r5, r6, r7, r8, sb, pc}
_ZN4Anki5Cozmo17MovementComponent12UnlockTracksEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE; bounded A10 UnlockTracks

RANGE 0063FE5C..0063FFDC _ZN4Anki5Cozmo17MovementComponent12UnlockTracksEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE
0063FE5C: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
0063FE60: sub.w      sp, sp, #0x428
0063FE64: sub        sp, #4
0063FE66: mov        r4, r2
0063FE68: ldr        r2, [pc, #0x1d0] ; literal[0064003C]=009FE9D6
0063FE6A: add.w      r8, r0, #0x30
0063FE6E: eor        sl, r1, #0xff
0063FE72: add        r2, pc
0063FE74: mov.w      r7, #-1
0063FE78: movs       r5, #1
0063FE7A: movs       r6, #0
0063FE7C: ldr        r2, [r2]
0063FE7E: ldr        r2, [r2]
0063FE80: str.w      r2, [sp, #0x428]
0063FE84: str        r0, [sp, #0x10]
0063FE86: add        r0, sp, #0x20
0063FE88: adds       r0, #0xc
0063FE8A: str        r0, [sp, #0x18]
0063FE8C: movs       r0, #0
0063FE8E: str        r1, [sp, #0xc]
0063FE90: str        r0, [sp, #0x14]
0063FE92: adds       r7, #1
0063FE94: lsl.w      sb, r5, r7
0063FE98: tst.w      sb, sl
0063FE9C: bne        #0x63ff8a
0063FE9E: movs       r0, #0
0063FEA0: str        r0, [sp, #0x28]
0063FEA2: strd       r0, r0, [sp, #0x20]
0063FEA6: ldrb       r0, [r4]
0063FEA8: lsls       r0, r0, #0x1f
0063FEAA: bne        #0x63feb8
0063FEAC: mov        r0, r4
0063FEAE: add        r1, sp, #0x20
0063FEB0: ldm.w      r0, {r2, r3, r5}
0063FEB4: stm        r1!, {r2, r3, r5}
0063FEB6: b          #0x63fec2
0063FEB8: ldrd       r2, r1, [r4, #4]
0063FEBC: add        r0, sp, #0x20
0063FEBE: bl         #0x4e02b2
0063FEC2: ldr        r0, [sp, #0x18]
0063FEC4: movs       r1, #0
0063FEC6: strd       r1, r1, [r0]
0063FECA: str        r1, [r0, #8]
0063FECC: ldr        r1, [pc, #0x1c0] ; literal[00640090]=005A402C
0063FECE: movs       r2, #0
0063FED0: add        r1, pc
0063FED2: bl         #0x4e02b2
0063FED6: sub.w      fp, r8, #8
0063FEDA: mov        r5, sl
0063FEDC: add        r1, sp, #0x20
0063FEDE: mov        r0, fp
0063FEE0: blx        #0x4b969c ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE4findIS4_EENS_15__tree_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEERKT_ -> 00642508 size=72
0063FEE4: mov        sl, r0
0063FEE6: ldrb.w     r0, [sp, #0x2c]
0063FEEA: lsls       r0, r0, #0x1f
0063FEEC: itt        ne
0063FEEE: ldrne      r0, [sp, #0x34]
0063FEF0: blxne      #0x4a40cc
0063FEF4: ldrb.w     r0, [sp, #0x20]
0063FEF8: lsls       r0, r0, #0x1f
0063FEFA: itt        ne
0063FEFC: ldrne      r0, [sp, #0x28]
0063FEFE: blxne      #0x4a40cc
0063FF02: sub.w      r0, r8, #4
0063FF06: cmp        r0, sl
0063FF08: beq        #0x63ff26
0063FF0A: mov        r0, fp
0063FF0C: mov        r1, sl
0063FF0E: blx        #0x4b96a8 ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE5eraseENS_21__tree_const_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEE -> 006425E4 size=58
0063FF12: ldr.w      r0, [r8]
0063FF16: mov        sl, r5
0063FF18: movs       r5, #1
0063FF1A: cmp        r0, #0
0063FF1C: mov        r1, r0
0063FF1E: it         ne
0063FF20: movne      r1, #1
0063FF22: orrs       r6, r1
0063FF24: b          #0x63ff7e
0063FF26: movs       r0, #0
0063FF28: strd       r0, r0, [sp, #0x20]
0063FF2C: str        r0, [sp, #0x28]
0063FF2E: ldrb       r1, [r4]
0063FF30: ldr        r0, [r4, #8]
0063FF32: tst.w      r1, #1
0063FF36: it         eq
0063FF38: addeq      r0, r4, #1
0063FF3A: ldr        r1, [sp, #0xc]
0063FF3C: add        r2, sp, #0x20
0063FF3E: strd       r1, r0, [sp]
0063FF42: adr        r1, #0xfc ; ADR[00640040]=b'MovementComponent.UnlockTracks'
0063FF44: ldr        r0, [pc, #0x14c] ; literal[00640094]=005A40A0
0063FF46: adr        r3, #0x118 ; ADR[00640060]=b'Tracks 0x%x are not currently locked by %s'
0063FF48: add        r0, pc
0063FF4A: blx        #0x4a505c ; _ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D42C size=14
0063FF4E: ldr        r0, [sp, #0x20]
0063FF50: mov        sl, r5
0063FF52: movs       r5, #1
0063FF54: cbz        r0, #0x63ff74
0063FF56: ldr        r1, [sp, #0x24]
0063FF58: cmp        r1, r0
0063FF5A: itttt      ne
0063FF5C: subne.w    r2, r1, #8
0063FF60: subne      r2, r2, r0
0063FF62: mvnne      r3, #7
0063FF66: bicne.w    r2, r3, r2
0063FF6A: itt        ne
0063FF6C: addne      r1, r1, r2
0063FF6E: strne      r1, [sp, #0x24]
0063FF70: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0063FF74: ldr        r0, [sp, #0x10]
0063FF76: blx        #0x4b96b4 ; _ZNK4Anki5Cozmo17MovementComponent14PrintLockStateEv -> 006410D8 size=428
0063FF7A: ldr.w      r0, [r8]
0063FF7E: cbnz       r0, #0x63ff8a
0063FF80: ldr        r0, [sp, #0x14]
0063FF82: uxtb       r0, r0
0063FF84: orr.w      r0, r0, sb
0063FF88: str        r0, [sp, #0x14]
0063FF8A: add.w      r8, r8, #0xc
0063FF8E: cmp        r7, #7
0063FF90: blt.w      #0x63fe92
0063FF94: ldr        r1, [sp, #0x14]
0063FF96: lsls       r0, r1, #0x18
0063FF98: beq        #0x63ffbe
0063FF9A: ldr        r0, [sp, #0x10]
0063FF9C: add        r5, sp, #0x20
0063FF9E: ldr        r4, [r0, #4]
0063FFA0: mov        r0, r5
0063FFA2: strb.w     r1, [sp, #0x1c]
0063FFA6: add        r1, sp, #0x1c
0063FFA8: blx        #0x4b96c0 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS0_12AnimKeyFrame16EnableAnimTracksE -> 007AAC24 size=A
0063FFAC: mov        r0, r4
0063FFAE: mov        r1, r5
0063FFB0: movs       r2, #1
0063FFB2: movs       r3, #0
0063FFB4: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
0063FFB8: add        r0, sp, #0x20
0063FFBA: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
0063FFBE: ldr        r0, [pc, #0xcc] ; literal[0064008C]=009FE884
0063FFC0: ldr.w      r1, [sp, #0x428]
0063FFC4: add        r0, pc
0063FFC6: ldr        r0, [r0]
0063FFC8: ldr        r0, [r0]
0063FFCA: subs       r0, r0, r1
0063FFCC: itttt      eq
0063FFCE: andeq      r0, r6, #1
0063FFD2: addeq.w    sp, sp, #0x428
0063FFD6: addeq      sp, #4
0063FFD8: popeq.w    {r4, r5, r6, r7, r8, sb, sl, fp, pc}
_ZN4Anki5Cozmo17MovementComponent10LockTracksEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEESA_

RANGE 00640098..006401F4 _ZN4Anki5Cozmo17MovementComponent10LockTracksEhRKNSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEESA_
00640098: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
0064009C: sub.w      sp, sp, #0x418
006400A0: sub        sp, #4
006400A2: mov        fp, r2
006400A4: ldr        r2, [pc, #0x144] ; literal[006401EC]=009FE79A
006400A6: add.w      r7, r0, #0x30
006400AA: eor        sl, r1, #0xff
006400AE: add        r2, pc
006400B0: mov        r5, r3
006400B2: movs       r6, #0
006400B4: mov.w      r8, #-1
006400B8: ldr        r2, [r2]
006400BA: ldr        r2, [r2]
006400BC: str.w      r2, [sp, #0x418]
006400C0: movs       r2, #1
006400C2: str        r0, [sp]
006400C4: add        r0, sp, #0x10
006400C6: add.w      r4, r0, #0xc
006400CA: movs       r0, #0
006400CC: strd       r5, r0, [sp, #4]
006400D0: add.w      r8, r8, #1
006400D4: lsl.w      sb, r2, r8
006400D8: tst.w      sb, sl
006400DC: bne        #0x640160
006400DE: str        r6, [sp, #0x18]
006400E0: strd       r6, r6, [sp, #0x10]
006400E4: ldrb.w     r0, [fp]
006400E8: lsls       r0, r0, #0x1f
006400EA: bne        #0x6400fa
006400EC: mov        r1, fp
006400EE: add        r0, sp, #0x10
006400F0: ldm.w      r1, {r2, r3, r5}
006400F4: stm        r0!, {r2, r3, r5}
006400F6: ldr        r5, [sp, #4]
006400F8: b          #0x640104
006400FA: ldrd       r2, r1, [fp, #4]
006400FE: add        r0, sp, #0x10
00640100: bl         #0x4e02b2
00640104: strd       r6, r6, [r4]
00640108: str        r6, [r4, #8]
0064010A: ldrb       r0, [r5]
0064010C: lsls       r0, r0, #0x1f
0064010E: bne        #0x64011e
00640110: mov        r1, r5
00640112: mov        r0, r4
00640114: ldm.w      r1, {r2, r3, r5}
00640118: stm        r0!, {r2, r3, r5}
0064011A: ldr        r5, [sp, #4]
0064011C: b          #0x640128
0064011E: ldrd       r2, r1, [r5, #4]
00640122: mov        r0, r4
00640124: bl         #0x4e02b2
00640128: sub.w      r0, r7, #8
0064012C: add        r1, sp, #0x10
0064012E: blx        #0x4b96cc ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE15__emplace_multiIJS4_EEENS_15__tree_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEEDpOT_ -> 006423D8 size=86
00640132: ldrb.w     r0, [sp, #0x1c]
00640136: lsls       r0, r0, #0x1f
00640138: itt        ne
0064013A: ldrne      r0, [sp, #0x24]
0064013C: blxne      #0x4a40cc
00640140: ldrb.w     r0, [sp, #0x10]
00640144: lsls       r0, r0, #0x1f
00640146: itt        ne
00640148: ldrne      r0, [sp, #0x18]
0064014A: blxne      #0x4a40cc
0064014E: ldr        r0, [r7]
00640150: cmp        r0, #1
00640152: bne        #0x64015e
00640154: ldr        r0, [sp, #8]
00640156: uxtb       r0, r0
00640158: orr.w      r0, r0, sb
0064015C: str        r0, [sp, #8]
0064015E: movs       r2, #1
00640160: adds       r7, #0xc
00640162: cmp.w      r8, #7
00640166: blt        #0x6400d0
00640168: ldr        r1, [sp, #8]
0064016A: lsls       r0, r1, #0x18
0064016C: beq        #0x640192
0064016E: ldr        r0, [sp]
00640170: add        r5, sp, #0x10
00640172: ldr        r4, [r0, #4]
00640174: mov        r0, r5
00640176: strb.w     r1, [sp, #0xc]
0064017A: add        r1, sp, #0xc
0064017C: blx        #0x4b96d8 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS0_12AnimKeyFrame17DisableAnimTracksE -> 007AAB9E size=A
00640180: mov        r0, r4
00640182: mov        r1, r5
00640184: movs       r2, #1
00640186: movs       r3, #0
00640188: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
0064018C: add        r0, sp, #0x10
0064018E: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
00640192: ldr        r0, [pc, #0x5c] ; literal[006401F0]=009FE6B0
00640194: ldr.w      r1, [sp, #0x418]
00640198: add        r0, pc
0064019A: ldr        r0, [r0]
0064019C: ldr        r0, [r0]
0064019E: subs       r0, r0, r1
006401A0: ittt       eq
006401A2: addeq.w    sp, sp, #0x418
006401A6: addeq      sp, #4
006401A8: popeq.w    {r4, r5, r6, r7, r8, sb, sl, fp, pc}
006401AC: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
006401B0: bl         #0x4e39f8
006401B4: mov        r4, r0
006401B6: add        r0, sp, #0x10
006401B8: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
006401BC: b          #0x6401e4
006401BE: bl         #0x4e39f8
006401C2: mov        r4, r0
006401C4: b          #0x6401d6
006401C6: mov        r4, r0
006401C8: ldrb.w     r0, [sp, #0x1c]
006401CC: lsls       r0, r0, #0x1f
006401CE: itt        ne
006401D0: ldrne      r0, [sp, #0x24]
006401D2: blxne      #0x4a40cc
006401D6: ldrb.w     r0, [sp, #0x10]
006401DA: lsls       r0, r0, #0x1f
006401DC: itt        ne
006401DE: ldrne      r0, [sp, #0x18]
006401E0: blxne      #0x4a40cc
006401E4: mov        r0, r4
006401E6: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
006401EA: nop
006401EC: b          #0x640124
006401EE: lsls       r7, r3, #2
006401F0: b          #0x63ff54
006401F2: lsls       r7, r3, #2
_ZN4Anki5Cozmo17MovementComponent16MoveLiftToHeightEffffPh; bounded L9 lift movement

RANGE 00640700..00640742 _ZN4Anki5Cozmo17MovementComponent16MoveLiftToHeightEffffPh
00640700: push       {r7, lr}
00640702: sub        sp, #0x18
00640704: str        r1, [sp, #0x14]
00640706: mov        ip, r0
00640708: strd       r3, r2, [sp, #0xc]
0064070C: ldrb.w     r1, [ip, #8]
00640710: ldr        r2, [sp, #0x24]
00640712: ldr.w      r0, [ip, #4]
00640716: adds       r1, #1
00640718: cmp        r2, #0
0064071A: strb.w     r1, [ip, #8]
0064071E: beq        #0x640726
00640720: strb       r1, [r2]
00640722: ldrb.w     r1, [ip, #8]
00640726: strb.w     r1, [sp, #0xb]
0064072A: add.w      r1, sp, #0xb
0064072E: add        r2, sp, #0x20
00640730: add        r3, sp, #0xc
00640732: strd       r2, r1, [sp]
00640736: add        r1, sp, #0x14
00640738: add        r2, sp, #0x10
0064073A: bl         #0x640744
0064073E: add        sp, #0x18
00640740: pop        {r7, pc}
local 00640744; bounded L12 SetLiftHeight sender

RANGE 00640744..006407AE
00640744: push       {r4, r5, r7, lr}
00640746: sub.w      sp, sp, #0x420
0064074A: mov        r4, r0
0064074C: ldr        r0, [pc, #0x74] ; literal[006407C4]=009FE0F6
0064074E: ldr.w      r5, [sp, #0x434]
00640752: add        r0, pc
00640754: ldr.w      ip, [sp, #0x430]
00640758: ldr        r0, [r0]
0064075A: ldr        r0, [r0]
0064075C: str.w      r0, [sp, #0x41c]
00640760: ldr        r1, [r1]
00640762: ldrb       r0, [r5]
00640764: ldr.w      r5, [ip]
00640768: ldr        r3, [r3]
0064076A: ldr        r2, [r2]
0064076C: stm.w      sp, {r1, r2, r3, r5}
00640770: add        r5, sp, #0x14
00640772: mov        r1, sp
00640774: strb.w     r0, [sp, #0x10]
00640778: mov        r0, r5
0064077A: blx        #0x4b96fc ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_13SetLiftHeightE -> 007A8558 size=12
0064077E: mov        r0, r4
00640780: mov        r1, r5
00640782: movs       r2, #1
00640784: movs       r3, #0
00640786: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
0064078A: mov        r4, r0
0064078C: add        r0, sp, #0x14
0064078E: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
00640792: ldr        r0, [pc, #0x34] ; literal[006407C8]=009FE0B0
00640794: ldr.w      r1, [sp, #0x41c]
00640798: add        r0, pc
0064079A: ldr        r0, [r0]
0064079C: ldr        r0, [r0]
0064079E: subs       r0, r0, r1
006407A0: ittt       eq
006407A2: moveq      r0, r4
006407A4: addeq.w    sp, sp, #0x420
006407A8: popeq      {r4, r5, r7, pc}
006407AA: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
local 0064099C; bounded A8 stop sender

RANGE 0064099C..006409EA
0064099C: push       {r4, r5, r7, lr}
0064099E: sub.w      sp, sp, #0x410
006409A2: mov        r4, r0
006409A4: ldr        r0, [pc, #0x58] ; literal[00640A00]=009FDE9E
006409A6: add        r5, sp, #4
006409A8: mov        r1, sp
006409AA: add        r0, pc
006409AC: ldr        r0, [r0]
006409AE: ldr        r0, [r0]
006409B0: str.w      r0, [sp, #0x40c]
006409B4: mov        r0, r5
006409B6: blx        #0x4b9720 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_13StopAllMotorsE -> 007A88A8 size=6
006409BA: mov        r0, r4
006409BC: mov        r1, r5
006409BE: movs       r2, #1
006409C0: movs       r3, #0
006409C2: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
006409C6: mov        r4, r0
006409C8: add        r0, sp, #4
006409CA: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
006409CE: ldr        r0, [pc, #0x34] ; literal[00640A04]=009FDE74
006409D0: ldr.w      r1, [sp, #0x40c]
006409D4: add        r0, pc
006409D6: ldr        r0, [r0]
006409D8: ldr        r0, [r0]
006409DA: subs       r0, r0, r1
006409DC: ittt       eq
006409DE: moveq      r0, r4
006409E0: addeq.w    sp, sp, #0x410
006409E4: popeq      {r4, r5, r7, pc}
006409E6: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
local 00640C00; bounded L15 MoveLift sender

RANGE 00640C00..00640C68
00640C00: push       {r4, r5, r7, lr}
00640C02: sub.w      sp, sp, #0x410
00640C06: mov        r4, r0
00640C08: ldr        r0, [pc, #0x5c] ; literal[00640C68]=009FDC3C
00640C0A: add        r5, sp, #4
00640C0C: add        r0, pc
00640C0E: ldr        r0, [r0]
00640C10: ldr        r0, [r0]
00640C12: str.w      r0, [sp, #0x40c]
00640C16: ldr        r0, [r1]
00640C18: mov        r1, sp
00640C1A: str        r0, [sp]
00640C1C: mov        r0, r5
00640C1E: blx        #0x4b9690 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_8MoveLiftE -> 007A8430 size=A
00640C22: mov        r0, r4
00640C24: mov        r1, r5
00640C26: movs       r2, #1
00640C28: movs       r3, #0
00640C2A: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
00640C2E: mov        r4, r0
00640C30: add        r0, sp, #4
00640C32: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
00640C36: ldr        r0, [pc, #0x34] ; literal[00640C6C]=009FDC0C
00640C38: ldr.w      r1, [sp, #0x40c]
00640C3C: add        r0, pc
00640C3E: ldr        r0, [r0]
00640C40: ldr        r0, [r0]
00640C42: subs       r0, r0, r1
00640C44: ittt       eq
00640C46: moveq      r0, r4
00640C48: addeq.w    sp, sp, #0x410
00640C4C: popeq      {r4, r5, r7, pc}
00640C4E: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
00640C52: bl         #0x4e39f8
00640C56: mov        r4, r0
00640C58: add        r0, sp, #4
00640C5A: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
00640C5E: mov        r0, r4
00640C60: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
00640C64: bl         #0x4e39f8
_ZNK4Anki5Cozmo17MovementComponent14PrintLockStateEv; bounded D1 PrintLockState

RANGE 006410D8..006413CA _ZNK4Anki5Cozmo17MovementComponent14PrintLockStateEv
006410D8: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
006410DC: sub        sp, #0xd4
006410DE: mov        sb, r0
006410E0: ldr.w      r0, [pc, #0x3bc] ; literal[006414A0]=009FD6CE
006410E4: ldr.w      r1, [pc, #0x3bc] ; literal[006414A4]=009FD6CE
006410E8: movs       r4, #0
006410EA: add        r0, pc
006410EC: add        r6, sp, #0x40
006410EE: add        r1, pc
006410F0: str        r4, [sp, #0x44]
006410F2: ldr        r0, [r0]
006410F4: add.w      r7, r6, #0xc
006410F8: ldr        r1, [r1]
006410FA: add.w      r2, r0, #0xc
006410FE: adds       r0, #0x20
00641100: str        r2, [sp, #0x40]
00641102: adds       r1, #0x20
00641104: str        r1, [sp, #0x48]
00641106: str        r0, [sp, #0x80]
00641108: add.w      r0, r6, #0x40
0064110C: mov        r1, r7
0064110E: str        r0, [sp, #0x10]
00641110: blx        #0x4a445c ; _ZNSt6__ndk18ios_base4initEPv IMPORT (resolve packaged dependencies before calling external)
00641114: ldr        r0, [pc, #0x390] ; literal[006414A8]=009FD6A0
00641116: mov.w      r2, #-1
0064111A: ldr        r1, [pc, #0x390] ; literal[006414AC]=009FD6A0
0064111C: add        r0, pc
0064111E: str        r2, [sp, #0xcc]
00641120: add        r1, pc
00641122: str        r4, [sp, #0xc8]
00641124: ldr        r0, [r0]
00641126: ldr        r1, [r1]
00641128: add.w      r2, r0, #0x34
0064112C: str        r2, [sp, #0x80]
0064112E: add.w      r2, r0, #0xc
00641132: adds       r0, #0x20
00641134: str        r2, [sp, #0x40]
00641136: str        r0, [sp, #0x48]
00641138: add.w      r0, r1, #8
0064113C: str        r0, [sp, #0x4c]
0064113E: add.w      r0, r6, #0x10
00641142: str        r0, [sp, #0xc]
00641144: blx        #0x4a4468 ; _ZNSt6__ndk16localeC1Ev IMPORT (resolve packaged dependencies before calling external)
00641148: add.w      r0, r6, #0x14
0064114C: movs       r1, #0x18
0064114E: movs       r5, #0x18
00641150: blx        #0x4a403c ; __aeabi_memclr4 IMPORT (resolve packaged dependencies before calling external)
00641154: ldr        r0, [pc, #0x358] ; literal[006414B0]=009FD66C
00641156: str        r4, [sp, #0x6c]
00641158: add        r0, pc
0064115A: ldr        r0, [r0]
0064115C: adds       r0, #8
0064115E: str        r0, [sp, #0x4c]
00641160: strd       r4, r4, [sp, #0x70]
00641164: strd       r4, r5, [sp, #0x78]
00641168: str        r4, [sp, #0x30]
0064116A: strd       r4, r4, [sp, #0x28]
0064116E: add        r1, sp, #0x28
00641170: mov        r0, r7
00641172: bl         #0x4e4598
00641176: ldrb.w     r0, [sp, #0x28]
0064117A: str        r7, [sp, #8]
0064117C: lsls       r0, r0, #0x1f
0064117E: itt        ne
00641180: ldrne      r0, [sp, #0x30]
00641182: blxne      #0x4a40cc
00641186: add        r0, sp, #0x28
00641188: adr        r7, #0x32c ; ADR[006414B8]=b':'
0064118A: orr        r1, r0, #1
0064118E: add.w      r5, r0, #0xc
00641192: str        r1, [sp, #0x14]
00641194: mov.w      sl, #0
00641198: movs       r1, #1
0064119A: mov.w      fp, #0x20
0064119E: mov.w      r8, #0
006411A2: add.w      r0, r6, #8
006411A6: str        r0, [sp, #0x18]
006411A8: add.w      r0, r8, r8, lsl #1
006411AC: add.w      r4, sb, r0, lsl #2
006411B0: mov        r6, r4
006411B2: ldr        r0, [r6, #0x30]!
006411B6: cmp        r0, #0
006411B8: beq.w      #0x64130e
006411BC: lsl.w      r0, r1, r8
006411C0: uxtb       r1, r0
006411C2: add        r0, sp, #0x28
006411C4: blx        #0x4aaa98 ; _ZN4Anki5Cozmo16AnimTrackHelpers22AnimTrackFlagsToStringEh -> 006305E8 size=25C
006411C8: ldrd       r2, r1, [sp, #0x2c]
006411CC: ldrb.w     r0, [sp, #0x28]
006411D0: ands       r3, r0, #1
006411D4: ldr        r3, [sp, #0x14]
006411D6: itt        eq
006411D8: moveq      r1, r3
006411DA: lsreq      r2, r0, #1
006411DC: ldr        r0, [sp, #0x18]
006411DE: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
006411E2: mov        r1, r7
006411E4: movs       r2, #1
006411E6: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
006411EA: ldr        r1, [r6]
006411EC: blx        #0x4a8134 ; _ZNSt6__ndk113basic_ostreamIcNS_11char_traitsIcEEElsEj -> 0051778C size=10C
006411F0: strb.w     fp, [sp, #0xd3]
006411F4: add.w      r1, sp, #0xd3
006411F8: movs       r2, #1
006411FA: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
006411FE: ldrb.w     r0, [sp, #0x28]
00641202: mov        r7, sb
00641204: lsls       r0, r0, #0x1f
00641206: itt        ne
00641208: ldrne      r0, [sp, #0x30]
0064120A: blxne      #0x4a40cc
0064120E: ldr.w      sb, [r4, #0x28]
00641212: add.w      r6, r4, #0x2c
00641216: cmp        sb, r6
00641218: beq        #0x6412f4
0064121A: mov        fp, sb
0064121C: str.w      sl, [sp, #0x30]
00641220: mov        r0, sb
00641222: strd       sl, sl, [sp, #0x28]
00641226: ldrb       r1, [r0, #0x10]!
0064122A: tst.w      r1, #1
0064122E: bne        #0x64123a
00641230: add        r1, sp, #0x28
00641232: ldm.w      r0, {r2, r3, r4}
00641236: stm        r1!, {r2, r3, r4}
00641238: b          #0x641244
0064123A: ldrd       r2, r1, [sb, #0x14]
0064123E: add        r0, sp, #0x28
00641240: bl         #0x4e02b2
00641244: strd       sl, sl, [r5]
00641248: mov        r0, sb
0064124A: str.w      sl, [r5, #8]
0064124E: ldrb       r1, [r0, #0x1c]!
00641252: tst.w      r1, #1
00641256: bne        #0x641262
00641258: mov        r1, r5
0064125A: ldm.w      r0, {r2, r3, r4}
0064125E: stm        r1!, {r2, r3, r4}
00641260: b          #0x64126c
00641262: ldrd       r2, r1, [sb, #0x20]
00641266: mov        r0, r5
00641268: bl         #0x4e02b2
0064126C: ldrd       r2, r1, [sp, #0x38]
00641270: ldrb.w     r0, [sp, #0x34]
00641274: ands       r3, r0, #1
00641278: add        r3, sp, #0x28
0064127A: itt        eq
0064127C: addeq.w    r1, r3, #0xd
00641280: lsreq      r2, r0, #1
00641282: ldr        r0, [sp, #0x18]
00641284: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
00641288: adr        r1, #0x230 ; ADR[006414BC]=b'['
0064128A: movs       r2, #1
0064128C: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
00641290: ldrd       r2, r1, [sp, #0x2c]
00641294: ldrb.w     r3, [sp, #0x28]
00641298: ands       r4, r3, #1
0064129C: ldr        r4, [sp, #0x14]
0064129E: itt        eq
006412A0: moveq      r1, r4
006412A2: lsreq      r2, r3, #1
006412A4: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
006412A8: adr        r1, #0x214 ; ADR[006414C0]=b'] '
006412AA: movs       r2, #2
006412AC: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
006412B0: ldrb.w     r0, [sp, #0x34]
006412B4: lsls       r0, r0, #0x1f
006412B6: itt        ne
006412B8: ldrne      r0, [sp, #0x3c]
006412BA: blxne      #0x4a40cc
006412BE: ldrb.w     r0, [sp, #0x28]
006412C2: lsls       r0, r0, #0x1f
006412C4: itt        ne
006412C6: ldrne      r0, [sp, #0x30]
006412C8: blxne      #0x4a40cc
006412CC: ldr.w      r0, [fp, #4]
006412D0: cmp        r0, #0
006412D2: beq        #0x6412e0
006412D4: mov        sb, r0
006412D6: ldr.w      r0, [sb]
006412DA: cmp        r0, #0
006412DC: bne        #0x6412d4
006412DE: b          #0x6412ee
006412E0: ldr.w      sb, [fp, #8]
006412E4: ldr.w      r0, [sb]
006412E8: cmp        r0, fp
006412EA: mov        fp, sb
006412EC: bne        #0x6412e0
006412EE: cmp        sb, r6
006412F0: mov        fp, sb
006412F2: bne        #0x64121c
006412F4: movs       r0, #0xa
006412F6: strb.w     r0, [sp, #0x1c]
006412FA: ldr        r0, [sp, #0x18]
006412FC: add        r1, sp, #0x1c
006412FE: movs       r2, #1
00641300: blx        #0x4a4474 ; _ZNSt6__ndk124__put_character_sequenceIcNS_11char_traitsIcEEEERNS_13basic_ostreamIT_T0_EES7_PKS4_j -> 004E428C size=F0
00641304: mov        sb, r7
00641306: adr        r7, #0x1b0 ; ADR[006414B8]=b':'
00641308: movs       r1, #1
0064130A: mov.w      fp, #0x20
0064130E: add.w      r0, r8, #1
00641312: cmp.w      r8, #7
00641316: mov        r8, r0
00641318: blt.w      #0x6411a8
0064131C: movs       r0, #0
0064131E: strd       r0, r0, [sp, #0x28]
00641322: str        r0, [sp, #0x30]
00641324: add        r4, sp, #0x1c
00641326: ldr        r1, [sp, #8]
00641328: mov        r0, r4
0064132A: blx        #0x4a448c ; _ZNKSt6__ndk115basic_stringbufIcNS_11char_traitsIcEENS_9allocatorIcEEE3strEv -> 004E4784 size=4A
0064132E: ldr        r0, [pc, #0x194] ; literal[006414C4]=005A2CB2
00641330: ldrb.w     r2, [sp, #0x1c]
00641334: ldr        r1, [sp, #0x24]
00641336: add        r0, pc
00641338: tst.w      r2, #1
0064133C: it         eq
0064133E: orreq      r1, r4, #1
00641342: str        r1, [sp]
00641344: adr        r1, #0x180 ; ADR[006414C8]=b'MovementComponent.LockState'
00641346: add        r2, sp, #0x28
00641348: adr        r3, #0x198 ; ADR[006414E4]=b'%s'
0064134A: blx        #0x4a5b84 ; _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D818 size=7C
0064134E: ldrb.w     r0, [sp, #0x1c]
00641352: lsls       r0, r0, #0x1f
00641354: itt        ne
00641356: ldrne      r0, [sp, #0x24]
00641358: blxne      #0x4a40cc
0064135C: ldr        r0, [sp, #0x28]
0064135E: cbz        r0, #0x64137e
00641360: ldr        r1, [sp, #0x2c]
00641362: cmp        r1, r0
00641364: itttt      ne
00641366: subne.w    r2, r1, #8
0064136A: subne      r2, r2, r0
0064136C: mvnne      r3, #7
00641370: bicne.w    r2, r3, r2
00641374: itt        ne
00641376: addne      r1, r1, r2
00641378: strne      r1, [sp, #0x2c]
0064137A: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0064137E: ldr        r0, [pc, #0x174] ; literal[006414F4]=009FD43A
00641380: ldr        r1, [pc, #0x174] ; literal[006414F8]=009FD43C
00641382: add        r0, pc
00641384: ldrb.w     r2, [sp, #0x6c]
00641388: add        r1, pc
0064138A: ldr        r0, [r0]
0064138C: ldr        r1, [r1]
0064138E: add.w      r3, r0, #0x34
00641392: str        r3, [sp, #0x80]
00641394: add.w      r3, r0, #0xc
00641398: adds       r0, #0x20
0064139A: str        r3, [sp, #0x40]
0064139C: str        r0, [sp, #0x48]
0064139E: add.w      r0, r1, #8
006413A2: str        r0, [sp, #0x4c]
006413A4: lsls       r0, r2, #0x1f
006413A6: itt        ne
006413A8: ldrne      r0, [sp, #0x74]
006413AA: blxne      #0x4a40cc
006413AE: ldr        r0, [pc, #0x14c] ; literal[006414FC]=009FD410
006413B0: add        r0, pc
006413B2: ldr        r0, [r0]
006413B4: adds       r0, #8
006413B6: str        r0, [sp, #0x4c]
006413B8: ldr        r0, [sp, #0xc]
006413BA: blx        #0x4a44a4 ; _ZNSt6__ndk16localeD1Ev IMPORT (resolve packaged dependencies before calling external)
006413BE: ldr        r0, [sp, #0x10]
006413C0: blx        #0x4a44b0 ; _ZNSt6__ndk18ios_baseD2Ev IMPORT (resolve packaged dependencies before calling external)
006413C4: add        sp, #0xd4
006413C6: pop.w      {r4, r5, r6, r7, r8, sb, sl, fp, pc}
local 00641CFC; bounded T8 Movement dtor

RANGE 00641CFC..00641D38
00641CFC: push       {r4, r5, r7, lr}
00641CFE: mov        r4, r0
00641D00: ldr        r0, [pc, #0x34] ; literal[00641D38]=009FDB56
00641D02: ldr.w      r1, [r4, #0x8c]
00641D06: add        r0, pc
00641D08: ldr        r0, [r0]
00641D0A: adds       r0, #8
00641D0C: str        r0, [r4]
00641D0E: add.w      r0, r4, #0x88
00641D12: blx        #0x4b95f4 ; _ZNSt6__ndk16__treeINS_12__value_typeIhN4Anki5Cozmo17MovementComponent17FaceLayerToRemoveEEENS_19__map_value_compareIhS6_NS_4lessIhEELb1EEENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE -> 00641D4A size=24
00641D16: movs       r5, #0
00641D18: adds       r0, r4, r5
00641D1A: ldr.w      r1, [r0, #0x80]
00641D1E: adds       r0, #0x7c
00641D20: blx        #0x4b9600 ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE -> 00641D6E size=3C
00641D24: subs       r5, #0xc
00641D26: adds.w     r0, r5, #0x60
00641D2A: bne        #0x641d18
00641D2C: add.w      r0, r4, #0x10
00641D30: blx        #0x4a790c ; _ZNSt6__ndk110__list_impINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEE5clearEv -> 005194A8 size=3A
00641D34: mov        r0, r4
00641D36: pop        {r4, r5, r7, pc}
_ZNSt6__ndk16__treeINS_12__value_typeIhN4Anki5Cozmo17MovementComponent17FaceLayerToRemoveEEENS_19__map_value_compareIhS6_NS_4lessIhEELb1EEENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE

RANGE 00641D4A..00641D6E _ZNSt6__ndk16__treeINS_12__value_typeIhN4Anki5Cozmo17MovementComponent17FaceLayerToRemoveEEENS_19__map_value_compareIhS6_NS_4lessIhEELb1EEENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE
00641D4A: push       {r4, r5, r7, lr}
00641D4C: mov        r4, r1
00641D4E: mov        r5, r0
00641D50: cbz        r4, #0x641d6c
00641D52: ldr        r1, [r4]
00641D54: mov        r0, r5
00641D56: blx        #0x4b95f4 ; _ZNSt6__ndk16__treeINS_12__value_typeIhN4Anki5Cozmo17MovementComponent17FaceLayerToRemoveEEENS_19__map_value_compareIhS6_NS_4lessIhEELb1EEENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE -> 00641D4A size=24
00641D5A: ldr        r1, [r4, #4]
00641D5C: mov        r0, r5
00641D5E: blx        #0x4b95f4 ; _ZNSt6__ndk16__treeINS_12__value_typeIhN4Anki5Cozmo17MovementComponent17FaceLayerToRemoveEEENS_19__map_value_compareIhS6_NS_4lessIhEELb1EEENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE -> 00641D4A size=24
00641D62: mov        r0, r4
00641D64: pop.w      {r4, r5, r7, lr}
00641D68: b.w        #0x8ca88c
00641D6C: pop        {r4, r5, r7, pc}
_ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE; bounded T18 Movement lock tree

RANGE 00641D6E..00641DAA _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE
00641D6E: push       {r4, r5, r7, lr}
00641D70: mov        r4, r1
00641D72: mov        r5, r0
00641D74: cbz        r4, #0x641da8
00641D76: ldr        r1, [r4]
00641D78: mov        r0, r5
00641D7A: blx        #0x4b9600 ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE -> 00641D6E size=3C
00641D7E: ldr        r1, [r4, #4]
00641D80: mov        r0, r5
00641D82: blx        #0x4b9600 ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE7destroyEPNS_11__tree_nodeIS4_PvEE -> 00641D6E size=3C
00641D86: ldrb       r0, [r4, #0x1c]
00641D88: lsls       r0, r0, #0x1f
00641D8A: itt        ne
00641D8C: ldrne      r0, [r4, #0x24]
00641D8E: blxne      #0x4a40cc
00641D92: ldrb       r0, [r4, #0x10]
00641D94: lsls       r0, r0, #0x1f
00641D96: itt        ne
00641D98: ldrne      r0, [r4, #0x18]
00641D9A: blxne      #0x4a40cc
00641D9E: mov        r0, r4
00641DA0: pop.w      {r4, r5, r7, lr}
00641DA4: b.w        #0x8ca88c
00641DA8: pop        {r4, r5, r7, pc}
_ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE4findIS4_EENS_15__tree_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEERKT_

RANGE 00642508..0064257A _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE4findIS4_EENS_15__tree_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEERKT_
00642508: push.w     {r4, r5, r6, r7, r8, lr}
0064250C: mov        r4, r0
0064250E: mov        r6, r1
00642510: ldr        r2, [r4, #4]!
00642514: mov        r3, r4
00642516: blx        #0x4b9834 ; _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE13__lower_boundIS4_EENS_15__tree_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEERKT_SF_SF_ -> 0064257A size=6A
0064251A: mov        r8, r0
0064251C: cmp        r8, r4
0064251E: beq        #0x64256a
00642520: mov        r3, r8
00642522: ldrb       r1, [r6]
00642524: ldrb       r0, [r3, #0x10]!
00642528: ldr        r7, [r6, #4]
0064252A: ands       ip, r1, #1
0064252E: ldr        r5, [r3, #4]
00642530: it         eq
00642532: lsreq      r7, r1, #1
00642534: ands       lr, r0, #1
00642538: it         eq
0064253A: lsreq      r5, r0, #1
0064253C: mov        r2, r7
0064253E: cmp        r5, r7
00642540: it         lo
00642542: movlo      r2, r5
00642544: cmp        r2, #0
00642546: beq        #0x64256e
00642548: ldr        r0, [r6, #8]
0064254A: cmp.w      ip, #0
0064254E: ldr.w      r1, [r8, #0x18]
00642552: it         eq
00642554: addeq      r0, r6, #1
00642556: cmp.w      lr, #0
0064255A: it         eq
0064255C: addeq      r1, r3, #1
0064255E: blx        #0x4a5728 ; memcmp IMPORT (resolve packaged dependencies before calling external)
00642562: cbz        r0, #0x64256e
00642564: cmp.w      r0, #-1
00642568: bgt        #0x642574
0064256A: mov        r8, r4
0064256C: b          #0x642574
0064256E: cmp        r7, r5
00642570: it         lo
00642572: movlo      r8, r4
00642574: mov        r0, r8
00642576: pop.w      {r4, r5, r6, r7, r8, pc}
_ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE5eraseENS_21__tree_const_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEE

RANGE 006425E4..0064263C _ZNSt6__ndk16__treeIN4Anki5Cozmo17MovementComponent8LockInfoENS_4lessIS4_EENS_9allocatorIS4_EEE5eraseENS_21__tree_const_iteratorIS4_PNS_11__tree_nodeIS4_PvEEiEE
006425E4: push       {r4, r5, r7, lr}
006425E6: mov        r4, r1
006425E8: ldr        r1, [r4, #4]
006425EA: cbz        r1, #0x6425f6
006425EC: mov        r5, r1
006425EE: ldr        r1, [r5]
006425F0: cmp        r1, #0
006425F2: bne        #0x6425ec
006425F4: b          #0x642602
006425F6: mov        r1, r4
006425F8: ldr        r5, [r1, #8]
006425FA: ldr        r2, [r5]
006425FC: cmp        r2, r1
006425FE: mov        r1, r5
00642600: bne        #0x6425f8
00642602: ldr        r1, [r0]
00642604: cmp        r1, r4
00642606: it         eq
00642608: streq      r5, [r0]
0064260A: ldrd       r1, r2, [r0, #4]
0064260E: subs       r2, #1
00642610: str        r2, [r0, #8]
00642612: mov        r0, r1
00642614: mov        r1, r4
00642616: blx        #0x4a5d64 ; _ZNSt6__ndk113__tree_removeIPNS_16__tree_node_baseIPvEEEEvT_S5_ -> 004F6310 size=260
0064261A: ldrb       r0, [r4, #0x1c]
0064261C: lsls       r0, r0, #0x1f
0064261E: itt        ne
00642620: ldrne      r0, [r4, #0x24]
00642622: blxne      #0x4a40cc
00642626: ldrb       r0, [r4, #0x10]
00642628: lsls       r0, r0, #0x1f
0064262A: itt        ne
0064262C: ldrne      r0, [r4, #0x18]
0064262E: blxne      #0x4a40cc
00642632: mov        r0, r4
00642634: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00642638: mov        r0, r5
0064263A: pop        {r4, r5, r7, pc}
local 00643E80; bounded N4 active request dtor

RANGE 00643E80..00643EC4
00643E80: push       {r4, r5, r7, lr}
00643E82: mov        r4, r0
00643E84: ldrb.w     r0, [r4, #0x20]
00643E88: cbz        r0, #0x643ea8
00643E8A: ldr        r5, [r4, #4]
00643E8C: cbz        r5, #0x643ea4
00643E8E: ldr        r0, [r5]
00643E90: cbz        r0, #0x643e9e
00643E92: ldr        r1, [r5, #4]
00643E94: cmp        r1, r0
00643E96: it         ne
00643E98: strne      r0, [r5, #4]
00643E9A: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643E9E: mov        r0, r5
00643EA0: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643EA4: movs       r0, #0
00643EA6: str        r0, [r4, #4]
00643EA8: ldr        r0, [r4, #0x18]
00643EAA: add.w      r1, r4, #8
00643EAE: cmp        r1, r0
00643EB0: beq        #0x643eba
00643EB2: cbz        r0, #0x643ec0
00643EB4: ldr        r1, [r0]
00643EB6: ldr        r1, [r1, #0x14]
00643EB8: b          #0x643ebe
00643EBA: ldr        r1, [r0]
00643EBC: ldr        r1, [r1, #0x10]
00643EBE: blx        r1
00643EC0: mov        r0, r4
00643EC2: pop        {r4, r5, r7, pc}
_ZN4Anki5Cozmo18NVStorageComponentD2Ev; bounded N2 NV dtor

RANGE 00643EC4..00643F8E _ZN4Anki5Cozmo18NVStorageComponentD1Ev
00643EC4: push       {r4, r5, r6, lr}
00643EC6: mov        r4, r0
00643EC8: ldr        r0, [pc, #0xc4] ; literal[00643F90]=009FB992
00643ECA: add.w      r5, r4, #0x128
00643ECE: add        r0, pc
00643ED0: ldr        r1, [r0]
00643ED2: ldrd       r6, r0, [r4, #0x128]
00643ED6: adds       r1, #8
00643ED8: str        r1, [r4]
00643EDA: b          #0x643ef6
00643EDC: sub.w      r1, r0, #8
00643EE0: str.w      r1, [r4, #0x12c]
00643EE4: ldr        r0, [r0, #-0x4]
00643EE8: cbz        r0, #0x643ef4
00643EEA: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
00643EEE: ldr.w      r0, [r4, #0x12c]
00643EF2: b          #0x643ef6
00643EF4: mov        r0, r1
00643EF6: cmp        r0, r6
00643EF8: bne        #0x643edc
00643EFA: add.w      r0, r4, #0x144
00643EFE: blx        #0x4b9870 ; _ZNSt6__ndk112__hash_tableIhNS_4hashIhEENS_8equal_toIhEENS_9allocatorIhEEED2Ev -> 00646B40 size=24
00643F02: ldr.w      r0, [r4, #0x138]
00643F06: cbz        r0, #0x643f18
00643F08: ldr.w      r1, [r4, #0x13c]
00643F0C: cmp        r1, r0
00643F0E: it         ne
00643F10: strne.w    r0, [r4, #0x13c]
00643F14: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643F18: mov        r0, r5
00643F1A: blx        #0x4a4e4c ; _ZNSt6__ndk113__vector_baseINS_10shared_ptrIN6Signal3Lib21ScopedHandleContainerEEENS_9allocatorIS5_EEED2Ev -> 004EAE94 size=34
00643F1E: add.w      r0, r4, #0x110
00643F22: blx        #0x4b987c ; _ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEED2Ev -> 00646B64 size=26
00643F26: add.w      r0, r4, #0xf8
00643F2A: blx        #0x4b9888 ; _ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEED2Ev -> 00646C6C size=26
00643F2E: ldr.w      r0, [r4, #0xe8]
00643F32: cbz        r0, #0x643f44
00643F34: ldr.w      r1, [r4, #0xec]
00643F38: cmp        r1, r0
00643F3A: it         ne
00643F3C: strne.w    r0, [r4, #0xec]
00643F40: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643F44: add.w      r0, r4, #0x80
00643F48: blx        #0x4b9894 ; _ZN4Anki5Cozmo22RobotDataBackupManagerD1Ev -> 0051A5F0 size=7C
00643F4C: add.w      r0, r4, #0x50
00643F50: bl         #0x643e80
00643F54: ldr        r0, [r4, #0x38]
00643F56: add.w      r1, r4, #0x28
00643F5A: cmp        r1, r0
00643F5C: beq        #0x643f66
00643F5E: cbz        r0, #0x643f6c
00643F60: ldr        r1, [r0]
00643F62: ldr        r1, [r1, #0x14]
00643F64: b          #0x643f6a
00643F66: ldr        r1, [r0]
00643F68: ldr        r1, [r1, #0x10]
00643F6A: blx        r1
00643F6C: ldr        r5, [r4, #0x18]
00643F6E: cbz        r5, #0x643f86
00643F70: ldr        r0, [r5]
00643F72: cbz        r0, #0x643f80
00643F74: ldr        r1, [r5, #4]
00643F76: cmp        r1, r0
00643F78: it         ne
00643F7A: strne      r0, [r5, #4]
00643F7C: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643F80: mov        r0, r5
00643F82: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00643F86: movs       r0, #0
00643F88: str        r0, [r4, #0x18]
00643F8A: mov        r0, r4
00643F8C: pop        {r4, r5, r6, pc}
_ZN4Anki5Cozmo18NVStorageComponent14ProcessRequestEv; bounded I4 ProcessRequest idle gates

RANGE 00644FD4..0064503A _ZN4Anki5Cozmo18NVStorageComponent14ProcessRequestEv
00644FD4: push.w     {r4, r5, r6, r7, r8, sb, lr}
00644FD8: sub.w      sp, sp, #0x450
00644FDC: sub        sp, #4
00644FDE: mov        r4, r0
00644FE0: ldr.w      r0, [pc, #0x560] ; literal[00645544]=009F9864
00644FE4: add        r0, pc
00644FE6: ldr        r0, [r0]
00644FE8: ldr        r0, [r0]
00644FEA: str.w      r0, [sp, #0x450]
00644FEE: ldr.w      r0, [r4, #0x10c]
00644FF2: cmp        r0, #0
00644FF4: beq.w      #0x645488
00644FF8: ldr.w      r1, [r4, #0x108]
00644FFC: movw       r3, #0x4925
00645000: movt       r3, #0x2492
00645004: ldr.w      r0, [r4, #0xfc]
00645008: lsrs       r2, r1, #3
0064500A: umull      r2, r3, r2, r3
0064500E: ldr.w      r2, [r0, r3, lsl #2]
00645012: rsb        r0, r3, r3, lsl #3
00645016: sub.w      r0, r1, r0, lsl #3
0064501A: add.w      r1, r0, r0, lsl #3
0064501E: ldrb.w     r0, [r2, r1, lsl #3]
00645022: add.w      r5, r2, r1, lsl #3
00645026: cmp        r0, #3
00645028: bhi.w      #0x645326
0064502C: mov        r8, r5
0064502E: ldr        sb, [r8, #4]!
00645032: tbh        [pc, r0, lsl #1] ; literal[00645034]=0004F010
00645036: movs       r4, r0
00645038: lsls       r1, r1, #4
local 00645326 (entry reopened; bound below before using as row)

RANGE 00645326..00645346
00645326: movs       r0, #0
00645328: strd       r0, r0, [sp, #0x48]
0064532C: str        r0, [sp, #0x50]
0064532E: ldrb       r0, [r5]
00645330: blx        #0x4b98ac ; _ZN4Anki5Cozmo9NVStorage12EnumToStringENS1_11NVOperationE -> 007D005C size=18
00645334: mov        r3, r0
00645336: ldr        r0, [pc, #0x2b8] ; literal[006455F0]=005B62B0
00645338: add        r0, pc
0064533A: add        r1, sp, #0x48
0064533C: adr        r2, #0x2b4 ; ADR[006455F4]=b'%s'
0064533E: blx        #0x4a4540 ; _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D2B4 size=80
00645342: ldr        r0, [sp, #0x48]
00645344: cmp        r0, #0
local 00645488; bounded empty request return

RANGE 00645488..006454AA
00645488: ldr        r0, [pc, #0x16c] ; literal[006455F8]=009F93BA
0064548A: ldr.w      r1, [sp, #0x450]
0064548E: add        r0, pc
00645490: ldr        r0, [r0]
00645492: ldr        r0, [r0]
00645494: subs       r0, r0, r1
00645496: ittt       eq
00645498: addeq.w    sp, sp, #0x450
0064549C: addeq      sp, #4
0064549E: popeq.w    {r4, r5, r6, r7, r8, sb, pc}
006454A2: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
006454A6: mov        r4, r0
006454A8: ldr        r0, [sp, #0x14]
_ZN4Anki5Cozmo18NVStorageComponent22ProcessOnIdleCallbacksEv; bounded I5 ProcessOnIdleCallbacks

RANGE 00645B08..00645BAE _ZN4Anki5Cozmo18NVStorageComponent22ProcessOnIdleCallbacksEv
00645B08: push.w     {r4, r5, r6, r7, r8, sb, sl, fp, lr}
00645B0C: sub        sp, #0xc
00645B0E: mov        r4, r0
00645B10: ldr.w      r0, [r4, #0x10c]
00645B14: cbnz       r0, #0x645b1a
00645B16: ldr        r0, [r4, #8]
00645B18: cbz        r0, #0x645b20
00645B1A: add        sp, #0xc
00645B1C: pop.w      {r4, r5, r6, r7, r8, sb, sl, fp, pc}
00645B20: ldr.w      r0, [r4, #0x124]
00645B24: cmp        r0, #0
00645B26: beq        #0x645b1a
00645B28: ldr.w      sb, [pc, #0xac] ; literal[00645BD8]=0059E3C4
00645B2C: movw       fp, #0xc0c1
00645B30: add.w      r5, r4, #0x110
00645B34: addw       sl, pc, #0xa4 ; ADDW[00645BDC]=b'NVStorageComponent.ProcessOnIdleCallbacks.ProcessingCallback'
00645B38: add        sb, pc
00645B3A: movs       r6, #0
00645B3C: mov        r7, sp
00645B3E: movt       fp, #0xc0c0
00645B42: mov.w      r8, #0xaa
00645B46: strd       r6, r6, [sp]
00645B4A: str        r6, [sp, #8]
00645B4C: ldr        r0, [pc, #0xcc] ; literal[00645C1C]=005B58E2
00645B4E: mov        r1, sl
00645B50: mov        r2, r7
00645B52: mov        r3, sb
00645B54: add        r0, pc
00645B56: blx        #0x4a5b84 ; _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D818 size=7C
00645B5A: ldr        r0, [sp]
00645B5C: cbz        r0, #0x645b7c
00645B5E: ldr        r1, [sp, #4]
00645B60: cmp        r1, r0
00645B62: itttt      ne
00645B64: subne.w    r2, r1, #8
00645B68: subne      r2, r2, r0
00645B6A: mvnne      r3, #7
00645B6E: bicne.w    r2, r3, r2
00645B72: itt        ne
00645B74: addne      r1, r1, r2
00645B76: strne      r1, [sp, #4]
00645B78: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00645B7C: ldr.w      r1, [r4, #0x120]
00645B80: ldr.w      r0, [r4, #0x114]
00645B84: umull      r2, r3, r1, fp
00645B88: lsrs       r2, r3, #7
00645B8A: mls        r1, r2, r8, r1
00645B8E: ldr.w      r0, [r0, r2, lsl #2]
00645B92: add.w      r1, r1, r1, lsl #1
00645B96: add.w      r0, r0, r1, lsl #3
00645B9A: blx        #0x4b2a3c ; _ZNKSt6__ndk18functionIFvvEEclEv -> 005BF7BC size=3C
00645B9E: mov        r0, r5
00645BA0: blx        #0x4b9a2c ; _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE9pop_frontEv -> 006486C2 size=68
00645BA4: ldr.w      r0, [r4, #0x124]
00645BA8: cmp        r0, #0
00645BAA: bne        #0x645b46
00645BAC: b          #0x645b1a
_ZN4Anki5Cozmo18NVStorageComponent24AddOneShotOnIdleCallbackENSt6__ndk18functionIFvvEEE; bounded I2 AddOneShot

RANGE 00645C20..00645C36 _ZN4Anki5Cozmo18NVStorageComponent24AddOneShotOnIdleCallbackENSt6__ndk18functionIFvvEEE
00645C20: push       {r4, lr}
00645C22: mov        r4, r0
00645C24: add.w      r0, r4, #0x110
00645C28: blx        #0x4b9a38 ; _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE12emplace_backIJRS3_EEEvDpOT_ -> 00648034 size=76
00645C2C: mov        r0, r4
00645C2E: pop.w      {r4, lr}
00645C32: b.w        #0x8cce6c
_ZNSt6__ndk112__hash_tableIhNS_4hashIhEENS_8equal_toIhEENS_9allocatorIhEEED2Ev

RANGE 00646B40..00646B64 _ZNSt6__ndk112__hash_tableIhNS_4hashIhEENS_8equal_toIhEENS_9allocatorIhEEED2Ev
00646B40: push       {r4, r5, r7, lr}
00646B42: mov        r4, r0
00646B44: ldr        r0, [r4, #8]
00646B46: cbz        r0, #0x646b54
00646B48: ldr        r5, [r0]
00646B4A: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646B4E: cmp        r5, #0
00646B50: mov        r0, r5
00646B52: bne        #0x646b48
00646B54: ldr        r0, [r4]
00646B56: movs       r1, #0
00646B58: str        r1, [r4]
00646B5A: cbz        r0, #0x646b60
00646B5C: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646B60: mov        r0, r4
00646B62: pop        {r4, r5, r7, pc}
_ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEED2Ev; bounded N7 idle deque dtor

RANGE 00646B64..00646B8A _ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEED2Ev
00646B64: push       {r4, r5, r6, lr}
00646B66: mov        r4, r0
00646B68: blx        #0x4b9a8c ; _ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEE5clearEv -> 00646B8A size=B8
00646B6C: ldrd       r5, r6, [r4, #4]
00646B70: cmp        r5, r6
00646B72: beq        #0x646b80
00646B74: ldr        r0, [r5], #4
00646B78: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646B7C: cmp        r6, r5
00646B7E: bne        #0x646b74
00646B80: mov        r0, r4
00646B82: pop.w      {r4, r5, r6, lr}
00646B86: b.w        #0x8cce8c
_ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEE5clearEv; bounded N5 idle deque clear

RANGE 00646B8A..00646C42 _ZNSt6__ndk112__deque_baseINS_8functionIFvvEEENS_9allocatorIS3_EEE5clearEv
00646B8A: push       {r4, r5, r6, r7, lr}
00646B8C: sub        sp, #4
00646B8E: mov        r4, r0
00646B90: movw       r3, #0xc0c1
00646B94: ldrd       r0, r7, [r4, #4]
00646B98: movt       r3, #0xc0c0
00646B9C: ldr        r1, [r4, #0x10]
00646B9E: cmp        r7, r0
00646BA0: umull      r2, r6, r1, r3
00646BA4: lsr.w      r2, r6, #7
00646BA8: add.w      r5, r0, r2, lsl #2
00646BAC: beq        #0x646bda
00646BAE: ldr        r7, [r4, #0x14]
00646BB0: add        r7, r1
00646BB2: umull      r3, r6, r7, r3
00646BB6: lsrs       r3, r6, #7
00646BB8: movs       r6, #0xaa
00646BBA: mls        r7, r3, r6, r7
00646BBE: ldr.w      r0, [r0, r3, lsl #2]
00646BC2: mls        r1, r2, r6, r1
00646BC6: ldr        r2, [r5]
00646BC8: add.w      r3, r7, r7, lsl #1
00646BCC: add.w      r6, r0, r3, lsl #3
00646BD0: add.w      r0, r1, r1, lsl #1
00646BD4: add.w      r7, r2, r0, lsl #3
00646BD8: b          #0x646c04
00646BDA: movs       r7, #0
00646BDC: movs       r6, #0
00646BDE: b          #0x646c04
00646BE0: ldr        r0, [r7, #0x10]
00646BE2: cmp        r7, r0
00646BE4: beq        #0x646bee
00646BE6: cbz        r0, #0x646bf4
00646BE8: ldr        r1, [r0]
00646BEA: ldr        r1, [r1, #0x14]
00646BEC: b          #0x646bf2
00646BEE: ldr        r1, [r0]
00646BF0: ldr        r1, [r1, #0x10]
00646BF2: blx        r1
00646BF4: ldr        r0, [r5]
00646BF6: adds       r7, #0x18
00646BF8: subs       r0, r7, r0
00646BFA: cmp.w      r0, #0xff0
00646BFE: it         eq
00646C00: ldreq      r7, [r5, #4]!
00646C04: cmp        r6, r7
00646C06: bne        #0x646be0
00646C08: ldrd       r0, r1, [r4, #4]
00646C0C: movs       r2, #0
00646C0E: str        r2, [r4, #0x14]
00646C10: subs       r1, r1, r0
00646C12: asrs       r1, r1, #2
00646C14: cmp        r1, #3
00646C16: blo        #0x646c2e
00646C18: ldr        r0, [r0]
00646C1A: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646C1E: ldrd       r0, r1, [r4, #4]
00646C22: adds       r0, #4
00646C24: str        r0, [r4, #4]
00646C26: subs       r1, r1, r0
00646C28: asrs       r1, r1, #2
00646C2A: cmp        r1, #2
00646C2C: bhi        #0x646c18
00646C2E: cmp        r1, #2
00646C30: beq        #0x646c3a
00646C32: cmp        r1, #1
00646C34: bne        #0x646c3e
00646C36: movs       r0, #0x55
00646C38: b          #0x646c3c
00646C3A: movs       r0, #0xaa
00646C3C: str        r0, [r4, #0x10]
00646C3E: add        sp, #4
00646C40: pop        {r4, r5, r6, r7, pc}
_ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEENS_9allocatorIS4_EEED2Ev

RANGE 00646C42..00646C6C _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEENS_9allocatorIS4_EEED2Ev
00646C42: push       {r4, lr}
00646C44: mov        r4, r0
00646C46: ldrd       r1, r0, [r4, #4]
00646C4A: cmp        r0, r1
00646C4C: itttt      ne
00646C4E: subne      r2, r0, #4
00646C50: subne      r1, r2, r1
00646C52: mvnne      r2, #3
00646C56: bicne.w    r1, r2, r1
00646C5A: itt        ne
00646C5C: addne      r0, r0, r1
00646C5E: strne      r0, [r4, #8]
00646C60: ldr        r0, [r4]
00646C62: cbz        r0, #0x646c68
00646C64: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646C68: mov        r0, r4
00646C6A: pop        {r4, pc}
_ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEED2Ev; bounded N8 requests deque dtor

RANGE 00646C6C..00646C92 _ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEED2Ev
00646C6C: push       {r4, r5, r6, lr}
00646C6E: mov        r4, r0
00646C70: blx        #0x4b9aa4 ; _ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEE5clearEv -> 00646C92 size=DA
00646C74: ldrd       r5, r6, [r4, #4]
00646C78: cmp        r5, r6
00646C7A: beq        #0x646c88
00646C7C: ldr        r0, [r5], #4
00646C80: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646C84: cmp        r6, r5
00646C86: bne        #0x646c7c
00646C88: mov        r0, r4
00646C8A: pop.w      {r4, r5, r6, lr}
00646C8E: b.w        #0x8cce9c
_ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEE5clearEv; bounded N6 requests deque clear

RANGE 00646C92..00646D6C _ZNSt6__ndk112__deque_baseIN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS4_EEE5clearEv
00646C92: push       {r4, r5, r6, r7, lr}
00646C94: sub        sp, #4
00646C96: mov        r4, r0
00646C98: movw       r3, #0x4925
00646C9C: ldrd       r1, r7, [r4, #4]
00646CA0: movt       r3, #0x2492
00646CA4: ldr        r0, [r4, #0x10]
00646CA6: cmp        r7, r1
00646CA8: lsr.w      r2, r0, #3
00646CAC: umull      r6, r2, r2, r3
00646CB0: add.w      r5, r1, r2, lsl #2
00646CB4: beq        #0x646ce8
00646CB6: ldr        r7, [r4, #0x14]
00646CB8: add        r7, r0
00646CBA: lsrs       r6, r7, #3
00646CBC: umull      r3, r6, r6, r3
00646CC0: ldr        r3, [r5]
00646CC2: ldr.w      r1, [r1, r6, lsl #2]
00646CC6: rsb        r6, r6, r6, lsl #3
00646CCA: sub.w      r7, r7, r6, lsl #3
00646CCE: add.w      r7, r7, r7, lsl #3
00646CD2: add.w      r6, r1, r7, lsl #3
00646CD6: rsb        r1, r2, r2, lsl #3
00646CDA: sub.w      r0, r0, r1, lsl #3
00646CDE: add.w      r0, r0, r0, lsl #3
00646CE2: add.w      r7, r3, r0, lsl #3
00646CE6: b          #0x646d2e
00646CE8: movs       r7, #0
00646CEA: movs       r6, #0
00646CEC: b          #0x646d2e
00646CEE: ldr        r0, [r7, #0x30]
00646CF0: add.w      r1, r7, #0x20
00646CF4: cmp        r1, r0
00646CF6: beq        #0x646d00
00646CF8: cbz        r0, #0x646d06
00646CFA: ldr        r1, [r0]
00646CFC: ldr        r1, [r1, #0x14]
00646CFE: b          #0x646d04
00646D00: ldr        r1, [r0]
00646D02: ldr        r1, [r1, #0x10]
00646D04: blx        r1
00646D06: ldr        r0, [r7, #0x18]
00646D08: add.w      r1, r7, #8
00646D0C: cmp        r1, r0
00646D0E: beq        #0x646d18
00646D10: cbz        r0, #0x646d1e
00646D12: ldr        r1, [r0]
00646D14: ldr        r1, [r1, #0x14]
00646D16: b          #0x646d1c
00646D18: ldr        r1, [r0]
00646D1A: ldr        r1, [r1, #0x10]
00646D1C: blx        r1
00646D1E: ldr        r0, [r5]
00646D20: adds       r7, #0x48
00646D22: subs       r0, r7, r0
00646D24: cmp.w      r0, #0xfc0
00646D28: it         eq
00646D2A: ldreq      r7, [r5, #4]!
00646D2E: cmp        r6, r7
00646D30: bne        #0x646cee
00646D32: ldrd       r0, r1, [r4, #4]
00646D36: movs       r2, #0
00646D38: str        r2, [r4, #0x14]
00646D3A: subs       r1, r1, r0
00646D3C: asrs       r1, r1, #2
00646D3E: cmp        r1, #3
00646D40: blo        #0x646d58
00646D42: ldr        r0, [r0]
00646D44: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646D48: ldrd       r0, r1, [r4, #4]
00646D4C: adds       r0, #4
00646D4E: str        r0, [r4, #4]
00646D50: subs       r1, r1, r0
00646D52: asrs       r1, r1, #2
00646D54: cmp        r1, #2
00646D56: bhi        #0x646d42
00646D58: cmp        r1, #2
00646D5A: beq        #0x646d64
00646D5C: cmp        r1, #1
00646D5E: bne        #0x646d68
00646D60: movs       r0, #0x1c
00646D62: b          #0x646d66
00646D64: movs       r0, #0x38
00646D66: str        r0, [r4, #0x10]
00646D68: add        sp, #4
00646D6A: pop        {r4, r5, r6, r7, pc}
_ZNSt6__ndk114__split_bufferIPN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS5_EEED2Ev

RANGE 00646D6C..00646D96 _ZNSt6__ndk114__split_bufferIPN4Anki5Cozmo18NVStorageComponent16NVStorageRequestENS_9allocatorIS5_EEED2Ev
00646D6C: push       {r4, lr}
00646D6E: mov        r4, r0
00646D70: ldrd       r1, r0, [r4, #4]
00646D74: cmp        r0, r1
00646D76: itttt      ne
00646D78: subne      r2, r0, #4
00646D7A: subne      r1, r2, r1
00646D7C: mvnne      r2, #3
00646D80: bicne.w    r1, r2, r1
00646D84: itt        ne
00646D86: addne      r0, r0, r1
00646D88: strne      r0, [r4, #8]
00646D8A: ldr        r0, [r4]
00646D8C: cbz        r0, #0x646d92
00646D8E: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00646D92: mov        r0, r4
00646D94: pop        {r4, pc}
_ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE12emplace_backIJRS3_EEEvDpOT_; bounded I9 callback append

RANGE 00648034..006480AA _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE12emplace_backIJRS3_EEEvDpOT_
00648034: push.w     {r4, r5, r6, r7, r8, lr}
00648038: mov        r4, r0
0064803A: movs       r7, #0xaa
0064803C: ldrd       r0, r2, [r4, #4]
00648040: mov        r8, r1
00648042: ldrd       ip, r3, [r4, #0x10]
00648046: movs       r1, #0
00648048: subs       r5, r2, r0
0064804A: asrs       r6, r5, #2
0064804C: cmp.w      r1, r5, asr #2
00648050: mul        r7, r6, r7
00648054: add.w      r1, r3, ip
00648058: it         ne
0064805A: subne      r6, r7, #1
0064805C: cmp        r6, r1
0064805E: bne        #0x64806e
00648060: mov        r0, r4
00648062: blx        #0x4b9ba0 ; _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE19__add_back_capacityEv -> 006480AA size=22A
00648066: ldrd       r0, r2, [r4, #4]
0064806A: ldrd       ip, r3, [r4, #0x10]
0064806E: cmp        r2, r0
00648070: beq        #0x648098
00648072: movw       r2, #0xc0c1
00648076: add.w      r1, ip, r3
0064807A: movt       r2, #0xc0c0
0064807E: umull      r2, r3, r1, r2
00648082: lsrs       r2, r3, #7
00648084: movs       r3, #0xaa
00648086: mls        r1, r2, r3, r1
0064808A: ldr.w      r0, [r0, r2, lsl #2]
0064808E: add.w      r1, r1, r1, lsl #1
00648092: add.w      r0, r0, r1, lsl #3
00648096: b          #0x64809a
00648098: movs       r0, #0
0064809A: mov        r1, r8
0064809C: blx        #0x4a922c ; _ZNSt6__ndk18functionIFvvEEC2ERKS2_ -> 00528C0C size=2A
006480A0: ldr        r0, [r4, #0x14]
006480A2: adds       r0, #1
006480A4: str        r0, [r4, #0x14]
006480A6: pop.w      {r4, r5, r6, r7, r8, pc}
_ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE19__add_back_capacityEv

RANGE 006480AA..006482D4 _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE19__add_back_capacityEv
006480AA: push.w     {r4, r5, r6, r7, r8, lr}
006480AE: sub        sp, #0x18
006480B0: mov        r4, r0
006480B2: ldr        r3, [r4, #0x10]
006480B4: cmp        r3, #0xaa
006480B6: blo        #0x648110
006480B8: ldrd       r1, r2, [r4, #4]
006480BC: subs       r3, #0xaa
006480BE: ldr        r0, [r4, #0xc]
006480C0: str        r3, [r4, #0x10]
006480C2: ldr        r6, [r1], #4
006480C6: cmp        r2, r0
006480C8: str        r1, [r4, #4]
006480CA: bne.w      #0x648292
006480CE: ldr        r3, [r4]
006480D0: cmp        r1, r3
006480D2: bls.w      #0x64821a
006480D6: subs       r0, r1, r3
006480D8: movs       r3, #1
006480DA: subs       r2, r2, r1
006480DC: add.w      r0, r3, r0, asr #2
006480E0: movs       r3, #0
006480E2: asrs       r7, r2, #2
006480E4: cmp.w      r3, r2, asr #2
006480E8: add.w      r0, r0, r0, lsr #31
006480EC: sub.w      r8, r3, r0, asr #1
006480F0: asr.w      r0, r0, #1
006480F4: sub.w      r5, r1, r0, lsl #2
006480F8: beq        #0x648102
006480FA: mov        r0, r5
006480FC: blx        #0x4a6eec ; __aeabi_memmove4 IMPORT (resolve packaged dependencies before calling external)
00648100: ldr        r1, [r4, #4]
00648102: add.w      r0, r1, r8, lsl #2
00648106: add.w      r2, r5, r7, lsl #2
0064810A: str        r0, [r4, #4]
0064810C: str        r2, [r4, #8]
0064810E: b          #0x648292
00648110: ldrd       r0, r7, [r4]
00648114: ldrd       r1, r3, [r4, #8]
00648118: subs       r2, r3, r0
0064811A: subs       r0, r1, r7
0064811C: cmp        r0, r2
0064811E: bhs        #0x648186
00648120: mov.w      r0, #0xff0
00648124: cmp        r3, r1
00648126: bne        #0x64820a
00648128: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
0064812C: add        r1, sp, #4
0064812E: str        r0, [sp, #4]
00648130: mov        r0, r4
00648132: blx        #0x4b9bac ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEENS_9allocatorIS4_EEE10push_frontEOS4_ -> 006483AE size=D8
00648136: ldrd       r1, r2, [r4, #4]
0064813A: ldr        r0, [r4, #0xc]
0064813C: ldr        r6, [r1], #4
00648140: cmp        r2, r0
00648142: str        r1, [r4, #4]
00648144: bne.w      #0x648292
00648148: ldr        r3, [r4]
0064814A: cmp        r1, r3
0064814C: bhi        #0x6480d6
0064814E: subs       r0, r0, r3
00648150: movs       r2, #0
00648152: add.w      r3, r4, #0xc
00648156: asrs       r1, r0, #1
00648158: cmp.w      r2, r0, asr #1
0064815C: it         eq
0064815E: moveq      r1, #1
00648160: add        r0, sp, #4
00648162: lsrs       r2, r1, #2
00648164: blx        #0x4b9bb8 ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEERNS_9allocatorIS4_EEEC2EjjS7_ -> 00648488 size=8C
00648168: ldrd       r1, r0, [r4, #4]
0064816C: cmp        r1, r0
0064816E: beq        #0x648256
00648170: ldr        r2, [sp, #0xc]
00648172: ldr        r3, [r1], #4
00648176: str        r3, [r2]
00648178: cmp        r0, r1
0064817A: ldr        r2, [sp, #0xc]
0064817C: add.w      r2, r2, #4
00648180: str        r2, [sp, #0xc]
00648182: bne        #0x648172
00648184: b          #0x648250
00648186: movs       r3, #0
00648188: cmp.w      r3, r2, asr #1
0064818C: asr.w      r1, r2, #1
00648190: asr.w      r2, r0, #2
00648194: add.w      r3, r4, #0xc
00648198: add        r0, sp, #4
0064819A: it         eq
0064819C: moveq      r1, #1
0064819E: blx        #0x4b9bb8 ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEERNS_9allocatorIS4_EEEC2EjjS7_ -> 00648488 size=8C
006481A2: mov.w      r0, #0xff0
006481A6: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
006481AA: mov        r5, r0
006481AC: str        r5, [sp]
006481AE: add        r0, sp, #4
006481B0: mov        r1, sp
006481B2: blx        #0x4b9bc4 ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEERNS_9allocatorIS4_EEE9push_backEOS4_ -> 00648514 size=D8
006481B6: ldr        r5, [r4, #8]
006481B8: add        r6, sp, #4
006481BA: ldr        r1, [r4, #4]
006481BC: cmp        r5, r1
006481BE: beq        #0x6481cc
006481C0: subs       r5, #4
006481C2: mov        r0, r6
006481C4: mov        r1, r5
006481C6: blx        #0x4b9bd0 ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEERNS_9allocatorIS4_EEE10push_frontERKS4_ -> 006485EC size=D6
006481CA: b          #0x6481ba
006481CC: ldr        r2, [sp, #4]
006481CE: ldr        r0, [r4]
006481D0: str        r2, [r4]
006481D2: ldr        r2, [sp, #8]
006481D4: str        r0, [sp, #4]
006481D6: str        r2, [r4, #4]
006481D8: ldr        r3, [sp, #0xc]
006481DA: str        r1, [sp, #8]
006481DC: ldr        r2, [r4, #8]
006481DE: str        r3, [r4, #8]
006481E0: ldr        r3, [sp, #0x10]
006481E2: cmp        r2, r5
006481E4: str        r2, [sp, #0xc]
006481E6: ldr        r7, [r4, #0xc]
006481E8: str        r3, [r4, #0xc]
006481EA: str        r7, [sp, #0x10]
006481EC: itttt      ne
006481EE: subne      r3, r2, #4
006481F0: subne      r1, r3, r1
006481F2: mvnne      r3, #3
006481F6: bicne.w    r1, r3, r1
006481FA: itt        ne
006481FC: addne      r1, r1, r2
006481FE: strne      r1, [sp, #0xc]
00648200: cmp        r0, #0
00648202: beq        #0x64829a
00648204: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00648208: b          #0x64829a
0064820A: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
0064820E: add        r1, sp, #4
00648210: str        r0, [sp, #4]
00648212: mov        r0, r4
00648214: blx        #0x4b9bdc ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEENS_9allocatorIS4_EEE9push_backEOS4_ -> 006482D4 size=DA
00648218: b          #0x64829a
0064821A: subs       r0, r0, r3
0064821C: movs       r2, #0
0064821E: add.w      r3, r4, #0xc
00648222: asrs       r1, r0, #1
00648224: cmp.w      r2, r0, asr #1
00648228: it         eq
0064822A: moveq      r1, #1
0064822C: add        r0, sp, #4
0064822E: lsrs       r2, r1, #2
00648230: blx        #0x4b9bb8 ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEERNS_9allocatorIS4_EEEC2EjjS7_ -> 00648488 size=8C
00648234: ldrd       r1, r0, [r4, #4]
00648238: cmp        r1, r0
0064823A: beq        #0x648256
0064823C: ldr        r2, [sp, #0xc]
0064823E: ldr        r3, [r1], #4
00648242: str        r3, [r2]
00648244: cmp        r0, r1
00648246: ldr        r2, [sp, #0xc]
00648248: add.w      r2, r2, #4
0064824C: str        r2, [sp, #0xc]
0064824E: bne        #0x64823e
00648250: ldr        r3, [r4, #4]
00648252: ldr        r1, [r4, #8]
00648254: b          #0x64825a
00648256: ldr        r2, [sp, #0xc]
00648258: mov        r3, r1
0064825A: ldr        r7, [sp, #4]
0064825C: cmp        r1, r3
0064825E: ldr        r0, [r4]
00648260: str        r7, [r4]
00648262: ldr        r7, [sp, #8]
00648264: str        r0, [sp, #4]
00648266: str        r7, [r4, #4]
00648268: str        r3, [sp, #8]
0064826A: str        r2, [r4, #8]
0064826C: ldr        r7, [sp, #0x10]
0064826E: str        r1, [sp, #0xc]
00648270: ldr        r5, [r4, #0xc]
00648272: str        r7, [r4, #0xc]
00648274: str        r5, [sp, #0x10]
00648276: itttt      ne
00648278: subne      r7, r1, #4
0064827A: subne      r3, r7, r3
0064827C: mvnne      r7, #3
00648280: bicne.w    r3, r7, r3
00648284: itt        ne
00648286: addne      r1, r1, r3
00648288: strne      r1, [sp, #0xc]
0064828A: cbz        r0, #0x648292
0064828C: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00648290: ldr        r2, [r4, #8]
00648292: str        r6, [r2]
00648294: ldr        r0, [r4, #8]
00648296: adds       r0, #4
00648298: str        r0, [r4, #8]
0064829A: add        sp, #0x18
0064829C: pop.w      {r4, r5, r6, r7, r8, pc}
006482A0: mov        r4, r0
006482A2: mov        r0, r5
006482A4: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
006482A8: b          #0x6482ae
006482AA: b          #0x6482ac
006482AC: mov        r4, r0
006482AE: ldrd       r1, r0, [sp, #8]
006482B2: cmp        r0, r1
006482B4: beq        #0x6482c6
006482B6: subs       r2, r0, #4
006482B8: subs       r1, r2, r1
006482BA: mvn        r2, #3
006482BE: bic.w      r1, r2, r1
006482C2: add        r0, r1
006482C4: str        r0, [sp, #0xc]
006482C6: ldr        r0, [sp, #4]
006482C8: cbz        r0, #0x6482ce
006482CA: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
006482CE: mov        r0, r4
006482D0: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
_ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE9pop_frontEv; bounded I7 callback pop

RANGE 006486C2..0064872A _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE9pop_frontEv
006486C2: push       {r4, lr}
006486C4: mov        r4, r0
006486C6: movw       r2, #0xc0c1
006486CA: ldr        r1, [r4, #0x10]
006486CC: movt       r2, #0xc0c0
006486D0: ldr        r0, [r4, #4]
006486D2: umull      r2, r3, r1, r2
006486D6: lsrs       r2, r3, #7
006486D8: movs       r3, #0xaa
006486DA: mls        r1, r2, r3, r1
006486DE: ldr.w      r0, [r0, r2, lsl #2]
006486E2: add.w      r1, r1, r1, lsl #1
006486E6: add.w      r1, r0, r1, lsl #3
006486EA: ldr        r0, [r1, #0x10]
006486EC: cmp        r1, r0
006486EE: beq        #0x6486f8
006486F0: cbz        r0, #0x6486fe
006486F2: ldr        r1, [r0]
006486F4: ldr        r1, [r1, #0x14]
006486F6: b          #0x6486fc
006486F8: ldr        r1, [r0]
006486FA: ldr        r1, [r1, #0x10]
006486FC: blx        r1
006486FE: ldrd       r0, r1, [r4, #0x10]
00648702: subs       r1, #1
00648704: adds       r0, #1
00648706: cmp.w      r0, #0x154
0064870A: strd       r0, r1, [r4, #0x10]
0064870E: it         lo
00648710: poplo      {r4, pc}
00648712: ldr        r0, [r4, #4]
00648714: ldr        r0, [r0]
00648716: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0064871A: ldr        r0, [r4, #4]
0064871C: ldr        r1, [r4, #0x10]
0064871E: adds       r0, #4
00648720: str        r0, [r4, #4]
00648722: sub.w      r0, r1, #0xaa
00648726: str        r0, [r4, #0x10]
00648728: pop        {r4, pc}
_ZN4Anki5Cozmo13PathComponent5AbortEv; bounded A3 Path Abort boundary

RANGE 00649100..006491BC _ZN4Anki5Cozmo13PathComponent5AbortEv
00649100: push       {r4, r5, r6, lr}
00649102: sub        sp, #0x10
00649104: mov        r4, r0
00649106: movs       r0, #0
00649108: ldr        r1, [pc, #0xd8] ; literal[006491E4]=009E64FC
0064910A: strd       r0, r0, [sp, #4]
0064910E: str        r0, [sp, #0xc]
00649110: add        r1, pc
00649112: ldr        r0, [pc, #0xd4] ; literal[006491E8]=0059C3F6
00649114: ldr        r2, [r4, #0x38]
00649116: add        r0, pc
00649118: ldr.w      r1, [r1, r2, lsl #2]
0064911C: add        r2, sp, #4
0064911E: str        r1, [sp]
00649120: adr        r1, #0xc8 ; ADR[006491EC]=b'PathComponent.Abort'
00649122: adr        r3, #0xdc ; ADR[00649200]=b"Aborting from status '%s'"
00649124: blx        #0x4a505c ; _ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D42C size=14
00649128: ldr        r0, [sp, #4]
0064912A: cbz        r0, #0x64914a
0064912C: ldr        r1, [sp, #8]
0064912E: cmp        r1, r0
00649130: itttt      ne
00649132: subne.w    r2, r1, #8
00649136: subne      r2, r2, r0
00649138: mvnne      r3, #7
0064913C: bicne.w    r2, r3, r2
00649140: itt        ne
00649142: addne      r1, r1, r2
00649144: strne      r1, [sp, #8]
00649146: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0064914A: ldr        r0, [r4, #0x20]
0064914C: cbz        r0, #0x64915a
0064914E: ldr        r1, [r0]
00649150: ldr        r1, [r1, #0x10]
00649152: blx        r1
00649154: movs       r0, #0
00649156: strb.w     r0, [r4, #0x47]
0064915A: ldr        r0, [r4, #0x34]
0064915C: movs       r1, #0
0064915E: strd       r1, r1, [r4, #0x30]
00649162: cbz        r0, #0x649168
00649164: blx        #0x4a4ef4 ; _ZNSt6__ndk119__shared_weak_count16__release_sharedEv IMPORT (resolve packaged dependencies before calling external)
00649168: mov        r0, r4
0064916A: blx        #0x4b9ccc ; _ZN4Anki5Cozmo13PathComponent9ClearPathEv -> 00649220 size=A4
0064916E: mov        r5, r0
00649170: ldr        r0, [r4, #0x38]
00649172: cmp        r0, #4
00649174: bhi        #0x64918c
00649176: movs       r1, #1
00649178: lsl.w      r0, r1, r0
0064917C: tst.w      r0, #0x13
00649180: ite        eq
00649182: moveq      r1, #5
00649184: movne      r1, #4
00649186: mov        r0, r4
00649188: blx        #0x4b9cd8 ; _ZN4Anki5Cozmo13PathComponent20SetDriveToPoseStatusENS0_23ERobotDriveToPoseStatusE -> 006492C4 size=B8
0064918C: ldr        r6, [r4, #0x50]
0064918E: movs       r0, #0
00649190: strb.w     r0, [r4, #0x46]
00649194: ldr        r4, [r6]
00649196: b          #0x6491a6
00649198: sub.w      r0, r1, #0xc
0064919C: str        r0, [r6, #4]
0064919E: ldr        r1, [r1, #-0xc]
006491A2: ldr        r1, [r1]
006491A4: blx        r1
006491A6: ldr        r1, [r6, #4]
006491A8: cmp        r1, r4
006491AA: bne        #0x649198
006491AC: ldr        r0, [pc, #0x6c] ; literal[0064921C]=009F57C6
006491AE: add        r0, pc
006491B0: ldr        r0, [r0]
006491B2: ldr        r0, [r0]
006491B4: str        r0, [r6, #0xc]
006491B6: mov        r0, r5
006491B8: add        sp, #0x10
006491BA: pop        {r4, r5, r6, pc}
_ZN4Anki5Cozmo13PathComponent9ClearPathEv; bounded A4 ClearPath

RANGE 00649220..006492A0 _ZN4Anki5Cozmo13PathComponent9ClearPathEv
00649220: push       {r4, r5, r7, lr}
00649222: sub.w      sp, sp, #0x410
00649226: mov        r4, r0
00649228: ldr        r0, [pc, #0x90] ; literal[006492BC]=009F561E
0064922A: add        r0, pc
0064922C: ldr        r0, [r0]
0064922E: ldr        r0, [r0]
00649230: str.w      r0, [sp, #0x40c]
00649234: ldrh.w     r0, [r4, #0x42]
00649238: cbz        r0, #0x64923e
0064923A: strh.w     r0, [r4, #0x4a]
0064923E: ldr        r0, [r4, #0x54]
00649240: ldr        r2, [r0]
00649242: ldr        r1, [r0, #0x10]
00649244: ldr        r0, [r2, #0x24]
00649246: blx        #0x4a41a4 ; _ZN4Anki5Cozmo10VizManager9ErasePathEj -> 006BFACE size=1A
0064924A: ldr        r0, [r4, #4]
0064924C: cbz        r0, #0x649252
0064924E: blx        #0x4b9ce4 ; _ZN4Anki5Cozmo14PathDolerOuter9ClearPathEv -> 005082B0 size=18
00649252: movs       r0, #0xff
00649254: strb.w     r0, [r4, #0x40]
00649258: blx        #0x4a4f6c ; _ZN4Anki16BaseStationTimer11getInstanceEv -> 0084BB9C size=48
0064925C: blx        #0x4a50b0 ; _ZNK4Anki16BaseStationTimer23GetCurrentTimeInSecondsEv -> 0084BCA8 size=4
00649260: ldr        r5, [r4, #0x54]
00649262: mov        r1, sp
00649264: str        r0, [r4, #0x3c]
00649266: add        r4, sp, #4
00649268: movs       r0, #0
0064926A: strh.w     r0, [sp]
0064926E: mov        r0, r4
00649270: blx        #0x4b9cf0 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_9ClearPathE -> 007A891C size=A
00649274: mov        r0, r5
00649276: mov        r1, r4
00649278: movs       r2, #1
0064927A: movs       r3, #0
0064927C: blx        #0x4a5368 ; _ZNK4Anki5Cozmo5Robot11SendMessageERKNS0_14RobotInterface13EngineToRobotEbb -> 0051349C size=FC
00649280: mov        r4, r0
00649282: add        r0, sp, #4
00649284: blx        #0x4a5200 ; _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv -> 007A6F04 size=4C
00649288: ldr        r0, [pc, #0x34] ; literal[006492C0]=009F55BA
0064928A: ldr.w      r1, [sp, #0x40c]
0064928E: add        r0, pc
00649290: ldr        r0, [r0]
00649292: ldr        r0, [r0]
00649294: subs       r0, r0, r1
00649296: ittt       eq
00649298: moveq      r0, r4
0064929A: addeq.w    sp, sp, #0x410
0064929E: popeq      {r4, r5, r7, pc}
_ZN4Anki5Cozmo13PathComponent20SetDriveToPoseStatusENS0_23ERobotDriveToPoseStatusE

RANGE 006492C4..0064937C _ZN4Anki5Cozmo13PathComponent20SetDriveToPoseStatusENS0_23ERobotDriveToPoseStatusE
006492C4: push       {r4, r5, r7, lr}
006492C6: sub        sp, #0x18
006492C8: mov        r5, r0
006492CA: mov        r4, r1
006492CC: ldr        r1, [r5, #0x38]
006492CE: cmp        r1, r4
006492D0: beq        #0x64931c
006492D2: ldr        r0, [pc, #0x74] ; literal[00649348]=0059C234
006492D4: movs       r3, #0
006492D6: ldr        r2, [pc, #0x74] ; literal[0064934C]=009E632E
006492D8: add        r0, pc
006492DA: strd       r3, r3, [sp, #0xc]
006492DE: add        r2, pc
006492E0: str        r3, [sp, #0x14]
006492E2: ldr.w      r1, [r2, r1, lsl #2]
006492E6: adr        r3, #0x88 ; ADR[00649370]=b'%s -> %s'
006492E8: ldr.w      r2, [r2, r4, lsl #2]
006492EC: strd       r1, r2, [sp]
006492F0: adr        r1, #0x5c ; ADR[00649350]=b'PathComponent.TransitionStatus'
006492F2: add        r2, sp, #0xc
006492F4: blx        #0x4a505c ; _ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D42C size=14
006492F8: ldr        r0, [sp, #0xc]
006492FA: cbz        r0, #0x64931a
006492FC: ldr        r1, [sp, #0x10]
006492FE: cmp        r1, r0
00649300: itttt      ne
00649302: subne.w    r2, r1, #8
00649306: subne      r2, r2, r0
00649308: mvnne      r3, #7
0064930C: bicne.w    r2, r3, r2
00649310: itt        ne
00649312: addne      r1, r1, r2
00649314: strne      r1, [sp, #0x10]
00649316: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0064931A: str        r4, [r5, #0x38]
0064931C: add        sp, #0x18
0064931E: pop        {r4, r5, r7, pc}
00649320: mov        r4, r0
00649322: ldr        r0, [sp, #0xc]
00649324: cbz        r0, #0x649342
00649326: ldr        r1, [sp, #0x10]
00649328: cmp        r1, r0
0064932A: beq        #0x64933e
0064932C: sub.w      r2, r1, #8
00649330: mvn        r3, #7
00649334: subs       r2, r2, r0
00649336: bic.w      r2, r3, r2
0064933A: add        r1, r2
0064933C: str        r1, [sp, #0x10]
0064933E: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00649342: mov        r0, r4
00649344: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
00649348: stm        r2!, {r2, r4, r5}
0064934A: lsls       r1, r3, #1
0064934C: str        r6, [r5, #0x30]
0064934E: lsls       r6, r3, #2
00649350: str        r0, [r2, #0x14]
00649352: ldr        r4, [r6, #4]
00649354: ldr        r3, [r0, #0x74]
00649356: strb       r5, [r5, #1]
00649358: ldr        r7, [r5, #0x64]
0064935A: ldr        r5, [r4, #0x64]
0064935C: cmp        r6, #0x74
0064935E: strb       r4, [r2, #9]
00649360: ldr        r1, [r4, #0x64]
00649362: ldr        r3, [r6, #0x14]
00649364: ldr        r4, [r6, #0x14]
00649366: ldr        r7, [r5, #0x64]
00649368: strb       r3, [r2, #0x11]
0064936A: strb       r1, [r4, #0x11]
0064936C: strb       r5, [r6, #0xd]
0064936E: movs       r0, r0
00649370: strb       r5, [r4, #0xc]
00649372: cmp        r5, #0x20
00649374: movs       r0, #0x3e
00649376: strb       r5, [r4, #0xc]
00649378: movs       r0, r0
0064937A: movs       r0, r0
_ZN4Anki5Cozmo20TouchSensorComponentD1Ev; bounded T5 Touch dtor

RANGE 0064E746..0064E75C _ZN4Anki5Cozmo20TouchSensorComponentD2Ev
0064E746: push       {r4, lr}
0064E748: mov        r4, r0
0064E74A: movs       r1, #0
0064E74C: ldr        r0, [r4, #0xc]
0064E74E: str        r1, [r4, #0xc]
0064E750: cbz        r0, #0x64e758
0064E752: ldr        r1, [r0]
0064E754: ldr        r1, [r1, #4]
0064E756: blx        r1
0064E758: mov        r0, r4
0064E75A: pop        {r4, pc}
_ZN4Anki5Cozmo10VizManager9ErasePathEj

RANGE 006BFACE..006BFAE8 _ZN4Anki5Cozmo10VizManager9ErasePathEj
006BFACE: push       {r7, lr}
006BFAD0: sub        sp, #0x78
006BFAD2: str        r1, [sp, #4]
006BFAD4: add        r0, sp, #8
006BFAD6: add        r1, sp, #4
006BFAD8: blx        #0x4bf8f4 ; _ZN4Anki5Cozmo12VizInterface10MessageVizC1EONS1_9ErasePathE -> 007E7EEE size=A
006BFADC: blx        #0x4a8290 ; _ZN4Anki5Cozmo12VizInterface10MessageViz12ClearCurrentEv -> 007E5D10 size=B2
006BFAE0: add        sp, #0x78
006BFAE2: pop        {r7, pc}
006BFAE4: bl         #0x4e39f8
_ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv

RANGE 007A6F04..007A6F50 _ZN4Anki5Cozmo14RobotInterface13EngineToRobot12ClearCurrentEv
007A6F04: push       {r4, lr}
007A6F06: mov        r4, r0
007A6F08: ldrb       r0, [r4]
007A6F0A: cmp        r0, #0x96
007A6F0C: bgt        #0x7a6f24
007A6F0E: cmp        r0, #0xf
007A6F10: beq        #0x7a6f2c
007A6F12: cmp        r0, #0x81
007A6F14: bne        #0x7a6f4a
007A6F16: ldr        r0, [r4, #0x10]
007A6F18: cbz        r0, #0x7a6f4a
007A6F1A: ldr        r1, [r4, #0x14]
007A6F1C: cmp        r1, r0
007A6F1E: it         ne
007A6F20: strne      r0, [r4, #0x14]
007A6F22: b          #0x7a6f46
007A6F24: cmp        r0, #0x97
007A6F26: beq        #0x7a6f3a
007A6F28: cmp        r0, #0xa4
007A6F2A: bne        #0x7a6f4a
007A6F2C: ldr        r0, [r4, #8]
007A6F2E: cbz        r0, #0x7a6f4a
007A6F30: ldr        r1, [r4, #0xc]
007A6F32: cmp        r1, r0
007A6F34: it         ne
007A6F36: strne      r0, [r4, #0xc]
007A6F38: b          #0x7a6f46
007A6F3A: ldr        r0, [r4, #4]
007A6F3C: cbz        r0, #0x7a6f4a
007A6F3E: ldr        r1, [r4, #8]
007A6F40: cmp        r1, r0
007A6F42: it         ne
007A6F44: strne      r0, [r4, #8]
007A6F46: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
007A6F4A: movs       r0, #0xff
007A6F4C: strb       r0, [r4]
007A6F4E: pop        {r4, pc}
_ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_8MoveLiftE

RANGE 007A8430..007A843A _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC2EONS1_8MoveLiftE
007A8430: movs       r2, #0x34
007A8432: ldr        r1, [r1]
007A8434: strb       r2, [r0]
007A8436: str        r1, [r0, #4]
007A8438: bx         lr
_ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_13SetLiftHeightE

RANGE 007A8558..007A856A _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC2EONS1_13SetLiftHeightE
007A8558: push       {r4, r5, r7, lr}
007A855A: ldm.w      r1, {r3, r4, r5, ip, lr}
007A855E: adds       r2, r0, #4
007A8560: movs       r1, #0x36
007A8562: stm.w      r2, {r3, r4, r5, ip, lr}
007A8566: strb       r1, [r0]
007A8568: pop        {r4, r5, r7, pc}
_ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_13StopAllMotorsE

RANGE 007A88A8..007A88AE _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC2EONS1_13StopAllMotorsE
007A88A8: movs       r1, #0x3b
007A88AA: strb       r1, [r0]
007A88AC: bx         lr
_ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_9ClearPathE

RANGE 007A891C..007A8926 _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC2EONS1_9ClearPathE
007A891C: movs       r2, #0x3c
007A891E: ldrh       r1, [r1]
007A8920: strb       r2, [r0]
007A8922: strh       r1, [r0, #4]
007A8924: bx         lr
_ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS0_12AbortDockingE

RANGE 007A8DF4..007A8DFA _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC2EONS0_12AbortDockingE
007A8DF4: movs       r1, #0x43
007A8DF6: strb       r1, [r0]
007A8DF8: bx         lr
_ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS1_14AbortAnimationE

RANGE 007AA392..007AA398 _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC2EONS1_14AbortAnimationE
007AA392: movs       r1, #0x8d
007AA394: strb       r1, [r0]
007AA396: bx         lr
_ZN4Anki5Cozmo14RobotInterface13EngineToRobotC1EONS0_12AnimKeyFrame16EnableAnimTracksE

RANGE 007AAC24..007AAC2E _ZN4Anki5Cozmo14RobotInterface13EngineToRobotC2EONS0_12AnimKeyFrame16EnableAnimTracksE
007AAC24: movs       r2, #0x9e
007AAC26: ldrb       r1, [r1]
007AAC28: strb       r2, [r0]
007AAC2A: strb       r1, [r0, #4]
007AAC2C: bx         lr
_ZNK4Anki5Cozmo14RobotInterface13RobotToEngine18Get_motorActionAckEv

RANGE 007B385C..007B3860 _ZNK4Anki5Cozmo14RobotInterface13RobotToEngine18Get_motorActionAckEv
007B385C: adds       r0, #4
007B385E: bx         lr
_ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE; bounded D3 enum names

RANGE 007BC250..007BC262 _ZN4Anki5Cozmo12EnumToStringENS0_13AnimTrackFlagE
007BC250: mov        r1, r0
007BC252: cmp        r1, #0xf
007BC254: bgt        #0x7bc272
007BC256: cmp        r1, #8
007BC258: bhi        #0x7bc2b0
007BC25A: ldr        r0, [pc, #0x58] ; literal[007BC2B4]=00463DE9
007BC25C: add        r0, pc
007BC25E: tbb        [pc, r1] ; literal[007BC260]=0528F001
_ZN4Anki4Util8Dispatch4StopEPNS1_5QueueE

RANGE 007FB900..007FB906 _ZN4Anki4Util8Dispatch4StopEPNS1_5QueueE
007FB900: ldr        r1, [r0]
007FB902: ldr        r1, [r1, #0x18]
007FB904: bx         r1
_ZN4Anki4Util8Dispatch7ReleaseERPNS1_5QueueE

RANGE 007FB906..007FB91A _ZN4Anki4Util8Dispatch7ReleaseERPNS1_5QueueE
007FB906: push       {r4, lr}
007FB908: mov        r4, r0
007FB90A: ldr        r0, [r4]
007FB90C: cbz        r0, #0x7fb914
007FB90E: ldr        r1, [r0]
007FB910: ldr        r1, [r1, #4]
007FB912: blx        r1
007FB914: movs       r0, #0
007FB916: str        r0, [r4]
007FB918: pop        {r4, pc}
local 00801E70; bounded T20 logger queue stop

RANGE 00801E70..00801E88
00801E70: push       {r4, lr}
00801E72: mov        r4, r0
00801E74: ldr        r0, [r4]
00801E76: cbz        r0, #0x801e86
00801E78: blx        #0x4af3dc ; _ZN4Anki4Util8Dispatch4StopEPNS1_5QueueE -> 007FB900 size=6
00801E7C: mov        r0, r4
00801E7E: pop.w      {r4, lr}
00801E82: b.w        #0x8d0e2c
00801E86: pop        {r4, pc}
_ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z

RANGE 0080D13C..0080D1BC _ZN4Anki4Util7sErrorFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
0080D13C: sub        sp, #4
0080D13E: push       {r4, r5, r6, r7, lr}
0080D140: sub.w      sp, sp, #0x410
0080D144: mov        r5, r0
0080D146: ldr        r0, [pc, #0x68] ; literal[0080D1B0]=0083281C
0080D148: mov        r4, r1
0080D14A: ldr        r1, [pc, #0x68] ; literal[0080D1B4]=008316F8
0080D14C: add        r0, pc
0080D14E: mov        r6, r2
0080D150: add        r1, pc
0080D152: ldr        r0, [r0]
0080D154: ldr        r1, [r1]
0080D156: ldr        r0, [r0]
0080D158: ldr        r1, [r1]
0080D15A: cmp        r0, #0
0080D15C: str.w      r3, [sp, #0x424]
0080D160: str.w      r1, [sp, #0x40c]
0080D164: beq        #0x80d18e
0080D166: add        r7, sp, #8
0080D168: mov.w      r1, #0x400
0080D16C: mov        r0, r7
0080D16E: blx        #0x4a48ac ; __aeabi_memclr8 IMPORT (resolve packaged dependencies before calling external)
0080D172: addw       r3, sp, #0x424
0080D176: mov        r0, r7
0080D178: mov.w      r1, #0x400
0080D17C: mov        r2, r6
0080D17E: str        r3, [sp, #4]
0080D180: blx        #0x4a7fc0 ; vsnprintf IMPORT (resolve packaged dependencies before calling external)
0080D184: mov        r0, r5
0080D186: mov        r1, r4
0080D188: mov        r2, r7
0080D18A: bl         #0x80d1bc
0080D18E: ldr        r0, [pc, #0x28] ; literal[0080D1B8]=008316B4
0080D190: ldr.w      r1, [sp, #0x40c]
0080D194: add        r0, pc
0080D196: ldr        r0, [r0]
0080D198: ldr        r0, [r0]
0080D19A: subs       r0, r0, r1
0080D19C: itttt      eq
0080D19E: addeq.w    sp, sp, #0x410
0080D1A2: popeq.w    {r4, r5, r6, r7, lr}
0080D1A6: addeq      sp, #4
0080D1A8: bxeq       lr
0080D1AA: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
0080D1AE: nop
0080D1B0: cmp        r0, #0x1c
0080D1B2: lsls       r3, r0, #2
0080D1B4: asrs       r0, r7, #0x1b
0080D1B6: lsls       r3, r0, #2
0080D1B8: asrs       r4, r6, #0x1a
0080D1BA: lsls       r3, r0, #2
_ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z

RANGE 0080D2B4..0080D334 _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
0080D2B4: sub        sp, #4
0080D2B6: push       {r4, r5, r6, r7, lr}
0080D2B8: sub.w      sp, sp, #0x410
0080D2BC: mov        r5, r0
0080D2BE: ldr        r0, [pc, #0x68] ; literal[0080D328]=008326A4
0080D2C0: mov        r4, r1
0080D2C2: ldr        r1, [pc, #0x68] ; literal[0080D32C]=00831580
0080D2C4: add        r0, pc
0080D2C6: mov        r6, r2
0080D2C8: add        r1, pc
0080D2CA: ldr        r0, [r0]
0080D2CC: ldr        r1, [r1]
0080D2CE: ldr        r0, [r0]
0080D2D0: ldr        r1, [r1]
0080D2D2: cmp        r0, #0
0080D2D4: str.w      r3, [sp, #0x424]
0080D2D8: str.w      r1, [sp, #0x40c]
0080D2DC: beq        #0x80d306
0080D2DE: add        r7, sp, #8
0080D2E0: mov.w      r1, #0x400
0080D2E4: mov        r0, r7
0080D2E6: blx        #0x4a48ac ; __aeabi_memclr8 IMPORT (resolve packaged dependencies before calling external)
0080D2EA: addw       r3, sp, #0x424
0080D2EE: mov        r0, r7
0080D2F0: mov.w      r1, #0x400
0080D2F4: mov        r2, r6
0080D2F6: str        r3, [sp, #4]
0080D2F8: blx        #0x4a7fc0 ; vsnprintf IMPORT (resolve packaged dependencies before calling external)
0080D2FC: mov        r0, r5
0080D2FE: mov        r1, r4
0080D300: mov        r2, r7
0080D302: bl         #0x80d334
0080D306: ldr        r0, [pc, #0x28] ; literal[0080D330]=0083153C
0080D308: ldr.w      r1, [sp, #0x40c]
0080D30C: add        r0, pc
0080D30E: ldr        r0, [r0]
0080D310: ldr        r0, [r0]
0080D312: subs       r0, r0, r1
0080D314: itttt      eq
0080D316: addeq.w    sp, sp, #0x410
0080D31A: popeq.w    {r4, r5, r6, r7, lr}
0080D31E: addeq      sp, #4
0080D320: bxeq       lr
0080D322: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
0080D326: nop
0080D328: movs       r6, #0xa4
0080D32A: lsls       r3, r0, #2
0080D32C: asrs       r0, r0, #0x16
0080D32E: lsls       r3, r0, #2
0080D330: asrs       r4, r7, #0x14
0080D332: lsls       r3, r0, #2
_ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z

RANGE 0080D42C..0080D440 _ZN4Anki4Util15sChanneledInfoFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
0080D42C: push       {r7, lr}
0080D42E: sub        sp, #8
0080D430: add.w      ip, sp, #0x10
0080D434: strd       ip, ip, [sp]
0080D438: blx        #0x4cb7e0 ; _ZN4Anki4Util15sChanneledInfoVEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_St9__va_list -> 0080D440 size=80
0080D43C: add        sp, #8
0080D43E: pop        {r7, pc}
_ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z

RANGE 0080D818..0080D894 _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z
0080D818: push.w     {r4, r5, r6, r7, r8, lr}
0080D81C: sub.w      sp, sp, #0x410
0080D820: mov        r6, r0
0080D822: ldr        r0, [pc, #0x64] ; literal[0080D888]=00832140
0080D824: mov        r5, r1
0080D826: ldr        r1, [pc, #0x64] ; literal[0080D88C]=0083101C
0080D828: add        r0, pc
0080D82A: mov        r7, r3
0080D82C: add        r1, pc
0080D82E: mov        r8, r2
0080D830: ldr        r0, [r0]
0080D832: ldr        r1, [r1]
0080D834: ldr        r0, [r0]
0080D836: ldr        r1, [r1]
0080D838: cmp        r0, #0
0080D83A: str.w      r1, [sp, #0x40c]
0080D83E: beq        #0x80d86a
0080D840: add        r4, sp, #8
0080D842: mov.w      r1, #0x400
0080D846: mov        r0, r4
0080D848: blx        #0x4a48ac ; __aeabi_memclr8 IMPORT (resolve packaged dependencies before calling external)
0080D84C: add.w      r3, sp, #0x428
0080D850: mov        r0, r4
0080D852: mov.w      r1, #0x400
0080D856: mov        r2, r7
0080D858: str        r3, [sp, #4]
0080D85A: blx        #0x4a7fc0 ; vsnprintf IMPORT (resolve packaged dependencies before calling external)
0080D85E: mov        r0, r6
0080D860: mov        r1, r5
0080D862: mov        r2, r8
0080D864: mov        r3, r4
0080D866: bl         #0x80d894
0080D86A: ldr        r0, [pc, #0x24] ; literal[0080D890]=00830FD8
0080D86C: ldr.w      r1, [sp, #0x40c]
0080D870: add        r0, pc
0080D872: ldr        r0, [r0]
0080D874: ldr        r0, [r0]
0080D876: subs       r0, r0, r1
0080D878: itt        eq
0080D87A: addeq.w    sp, sp, #0x410
0080D87E: popeq.w    {r4, r5, r6, r7, r8, pc}
0080D882: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
0080D886: nop
0080D888: movs       r1, #0x40
0080D88A: lsls       r3, r0, #2
0080D88C: asrs       r4, r3, #0x20
0080D88E: lsls       r3, r0, #2
0080D890: lsrs       r0, r3, #0x1f
0080D892: lsls       r3, r0, #2
_ZN4Anki4Util18sDebugBreakOnErrorEv

RANGE 0080DAB4..0080DAB6 _ZN4Anki4Util18sDebugBreakOnErrorEv
0080DAB4: bx         lr
_ZN4Anki4Util17RollingFileLoggerD2Ev; bounded T10 RollingFileLogger dtor

RANGE 0080E090..0080E120 _ZN4Anki4Util17RollingFileLoggerD2Ev
0080E090: push       {r4, r5, r6, r7, lr}
0080E092: sub        sp, #4
0080E094: mov        r4, r0
0080E096: ldr        r0, [pc, #0xbc] ; literal[0080E154]=00831B3E
0080E098: mov        r5, r4
0080E09A: add        r0, pc
0080E09C: ldr        r0, [r0]
0080E09E: adds       r0, #8
0080E0A0: str        r0, [r5], #8
0080E0A4: mov        r0, r5
0080E0A6: bl         #0x801e70
0080E0AA: add.w      r6, r4, #0x3c
0080E0AE: mov        r0, r6
0080E0B0: blx        #0x4a69c4 ; _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE5closeEv -> 0050111C size=3C
0080E0B4: cbnz       r0, #0x80e0cc
0080E0B6: add.w      r0, r4, #0x38
0080E0BA: ldr        r1, [r0]
0080E0BC: ldr        r1, [r1, #-0xc]
0080E0C0: add        r0, r1
0080E0C2: ldr        r1, [r0, #0x10]
0080E0C4: orr        r1, r1, #4
0080E0C8: blx        #0x4a4594 ; _ZNSt6__ndk18ios_base5clearEj IMPORT (resolve packaged dependencies before calling external)
0080E0CC: ldr        r0, [pc, #0x88] ; literal[0080E158]=0083082C
0080E0CE: mov        r7, r4
0080E0D0: add        r0, pc
0080E0D2: ldr        r0, [r0]
0080E0D4: add.w      r1, r0, #0x20
0080E0D8: str        r1, [r7, #0xa0]!
0080E0DC: adds       r0, #0xc
0080E0DE: str        r0, [r7, #-0x68]
0080E0E2: mov        r0, r6
0080E0E4: blx        #0x4a6760 ; _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEED2Ev -> 005010B4 size=68
0080E0E8: mov        r0, r7
0080E0EA: blx        #0x4a44b0 ; _ZNSt6__ndk18ios_baseD2Ev IMPORT (resolve packaged dependencies before calling external)
0080E0EE: ldrb       r0, [r7, #-0x7c]
0080E0F2: lsls       r0, r0, #0x1f
0080E0F4: itt        ne
0080E0F6: ldrne      r0, [r4, #0x2c]
0080E0F8: blxne      #0x4a40cc
0080E0FC: ldrb       r0, [r4, #0x18]
0080E0FE: lsls       r0, r0, #0x1f
0080E100: itt        ne
0080E102: ldrne      r0, [r4, #0x20]
0080E104: blxne      #0x4a40cc
0080E108: ldrb       r0, [r4, #0xc]
0080E10A: lsls       r0, r0, #0x1f
0080E10C: itt        ne
0080E10E: ldrne      r0, [r4, #0x14]
0080E110: blxne      #0x4a40cc
0080E114: mov        r0, r5
0080E116: bl         #0x801e70
0080E11A: mov        r0, r4
0080E11C: add        sp, #4
0080E11E: pop        {r4, r5, r6, r7, pc}
_ZNK4Anki4Util15RandomGenerator14RandDblInRangeEdd

RANGE 0082FA48..0082FA74 _ZNK4Anki4Util15RandomGenerator14RandDblInRangeEdd
0082FA48: push       {r7, lr}
0082FA4A: vpush      {d8, d9}
0082FA4E: vldr       d0, [sp, #0x18]
0082FA52: vmov       d8, r2, r3
0082FA56: vsub.f64   d9, d0, d8
0082FA5A: blx        #0x4ccecc ; _ZNK4Anki4Util15RandomGenerator10GetNextDblEv -> 0082F9B0 size=78
0082FA5E: vmov       d0, r0, r1
0082FA62: vmul.f64   d0, d9, d0
0082FA66: vadd.f64   d0, d0, d8
0082FA6A: vmov       r0, r1, d0
0082FA6E: vpop       {d8, d9}
0082FA72: pop        {r7, pc}
_ZN4Anki16BaseStationTimer11getInstanceEv

RANGE 0084BB9C..0084BBE4 _ZN4Anki16BaseStationTimer11getInstanceEv
0084BB9C: ldr        r0, [pc, #0x38] ; literal[0084BBD8]=007F42DA
0084BB9E: add        r0, pc
0084BBA0: ldr        r0, [r0]
0084BBA2: ldr        r0, [r0]
0084BBA4: cbz        r0, #0x84bba8
0084BBA6: bx         lr
0084BBA8: push       {r7, lr}
0084BBAA: movs       r0, #0x28
0084BBAC: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
0084BBB0: ldr        r1, [pc, #0x28] ; literal[0084BBDC]=007F42C2
0084BBB2: movs       r3, #0
0084BBB4: ldr        r2, [pc, #0x28] ; literal[0084BBE0]=007F42C0
0084BBB6: add        r1, pc
0084BBB8: strd       r3, r3, [r0, #0x18]
0084BBBC: add        r2, pc
0084BBBE: strd       r3, r3, [r0, #0x20]
0084BBC2: ldr        r1, [r1]
0084BBC4: ldr        r2, [r2]
0084BBC6: strd       r3, r3, [r0, #8]
0084BBCA: str        r3, [r0, #0x10]
0084BBCC: adds       r2, #8
0084BBCE: str        r2, [r0]
0084BBD0: str        r0, [r1]
0084BBD2: pop.w      {r7, lr}
0084BBD6: bx         lr
0084BBD8: cmn        r2, r3
0084BBDA: lsls       r7, r7, #1
0084BBDC: cmn        r2, r0
0084BBDE: lsls       r7, r7, #1
0084BBE0: cmn        r0, r0
0084BBE2: lsls       r7, r7, #1
_ZNK4Anki16BaseStationTimer23GetCurrentTimeInSecondsEv

RANGE 0084BCA8..0084BCAC _ZNK4Anki16BaseStationTimer23GetCurrentTimeInSecondsEv
0084BCA8: ldr        r0, [r0, #0x10]
0084BCAA: bx         lr

REOPENED PACKAGED IMPORT TARGETS / PLATFORM BOUNDARY
_Unwind_Resume: resources\lib\armeabi-v7a\libacattsandroid.so SHA256 73c4db9455e1cc0235f731ab110194dbfb7aa44974af7ac74c11de382f135698 VA 000409FC size 22
000409FC: mov        ip, sp
000409FE: push       {lr}
00040A00: push.w     {ip, lr}
00040A04: push.w     {r0, r1, r2, r3, r4, r5, r6, r7, r8, sb, sl, fp, ip}
00040A08: mov.w      r3, #0
00040A0C: push.w     {r2, r3}
00040A10: add        r1, sp, #4
00040A12: bl         #0x402c2
00040A16: ldr.w      lr, [sp, #0x40]
00040A1A: add        sp, #0x48
00040A1C: bx         lr
_ZNSt6__ndk119__shared_weak_count14__release_weakEv: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 0005EDC2 size 26
0005EDC2: add.w      ip, r0, #8
0005EDC6: dmb        ish
0005EDCA: ldrex      r2, [ip]
0005EDCE: subs       r3, r2, #1
0005EDD0: strex      r1, r3, [ip]
0005EDD4: cmp        r1, #0
0005EDD6: bne        #0x5edca
0005EDD8: cmp        r2, #0
0005EDDA: dmb        ish
0005EDDE: beq        #0x5ede2
0005EDE0: bx         lr
0005EDE2: ldr        r1, [r0]
0005EDE4: ldr        r1, [r1, #0x10]
0005EDE6: bx         r1
_ZNSt6__ndk119__shared_weak_count16__release_sharedEv: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 0005ED6C size 56
0005ED6C: push       {r4, r5, r7, lr}
0005ED6E: add        r7, sp, #8
0005ED70: mov        r4, r0
0005ED72: adds       r0, r4, #4
0005ED74: dmb        ish
0005ED78: ldrex      r1, [r0]
0005ED7C: subs       r2, r1, #1
0005ED7E: strex      r3, r2, [r0]
0005ED82: cmp        r3, #0
0005ED84: bne        #0x5ed78
0005ED86: cmp        r1, #0
0005ED88: dmb        ish
0005ED8C: bne        #0x5edb4
0005ED8E: mov        r5, r4
0005ED90: ldr        r0, [r5], #8
0005ED94: ldr        r1, [r0, #8]
0005ED96: mov        r0, r4
0005ED98: blx        r1
0005ED9A: dmb        ish
0005ED9E: ldrex      r0, [r5]
0005EDA2: subs       r1, r0, #1
0005EDA4: strex      r2, r1, [r5]
0005EDA8: cmp        r2, #0
0005EDAA: bne        #0x5ed9e
0005EDAC: cmp        r0, #0
0005EDAE: dmb        ish
0005EDB2: beq        #0x5edb6
0005EDB4: pop        {r4, r5, r7, pc}
0005EDB6: ldr        r0, [r4]
0005EDB8: ldr        r1, [r0, #0x10]
0005EDBA: mov        r0, r4
0005EDBC: pop.w      {r4, r5, r7, lr}
0005EDC0: bx         r1
_ZNSt6__ndk119__shared_weak_count4lockEv: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 0005EDE8 size 3C
0005EDE8: ldr        r3, [r0, #4]
0005EDEA: dmb        ish
0005EDEE: adds       r1, r3, #1
0005EDF0: beq        #0x5ee1a
0005EDF2: add.w      ip, r0, #4
0005EDF6: ldrex      r2, [ip]
0005EDFA: cmp        r2, r3
0005EDFC: bne        #0x5ee0c
0005EDFE: adds       r3, #1
0005EE00: dmb        ish
0005EE04: strex      r1, r3, [ip]
0005EE08: cbnz       r1, #0x5ee10
0005EE0A: b          #0x5ee1e
0005EE0C: clrex
0005EE10: adds       r1, r2, #1
0005EE12: mov        r3, r2
0005EE14: dmb        ish
0005EE18: bne        #0x5edf6
0005EE1A: movs       r0, #0
0005EE1C: bx         lr
0005EE1E: dmb        ish
0005EE22: bx         lr
_ZNSt6__ndk16localeC1Ev: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 00056828 size 1A
00056828: push       {r4, r6, r7, lr}
0005682A: add        r7, sp, #8
0005682C: mov        r4, r0
0005682E: blx        #0x32684
00056832: ldr        r0, [r0]
00056834: str        r0, [r4]
00056836: blx        #0x31a00
0005683A: mov        r0, r4
0005683C: pop        {r4, r6, r7, pc}
0005683E: bl         #0x38d6c
_ZNSt6__ndk16localeD1Ev: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 00056854 size 10
00056854: push       {r4, r6, r7, lr}
00056856: add        r7, sp, #8
00056858: mov        r4, r0
0005685A: ldr        r0, [r4]
0005685C: blx        #0x31a0c
00056860: mov        r0, r4
00056862: pop        {r4, r6, r7, pc}
_ZNSt6__ndk18ios_base4initEPv: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 0003BE10 size 3C
0003BE10: push       {r4, r6, r7, lr}
0003BE12: add        r7, sp, #8
0003BE14: movs       r2, #0
0003BE16: mov        r4, r0
0003BE18: cmp        r1, #0
0003BE1A: mov.w      r0, #0
0003BE1E: it         eq
0003BE20: moveq      r2, #1
0003BE22: movw       r3, #0x1002
0003BE26: mov.w      ip, #6
0003BE2A: strd       r3, ip, [r4, #4]
0003BE2E: strd       r0, r2, [r4, #0xc]
0003BE32: strd       r0, r1, [r4, #0x14]
0003BE36: add.w      r0, r4, #0x20
0003BE3A: movs       r1, #0x28
0003BE3C: blx        #0x31814
0003BE40: add.w      r0, r4, #0x1c
0003BE44: pop.w      {r4, r6, r7, lr}
0003BE48: b.w        #0x872ac
_ZNSt6__ndk18ios_base5clearEj: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 0003B6D0 size 7C
0003B6D0: push       {r4, r5, r7, lr}
0003B6D2: add        r7, sp, #8
0003B6D4: ldrd       r2, r3, [r0, #0x14]
0003B6D8: cmp        r3, #0
0003B6DA: it         eq
0003B6DC: orreq      r1, r1, #1
0003B6E0: tst        r1, r2
0003B6E2: str        r1, [r0, #0x10]
0003B6E4: it         eq
0003B6E6: popeq      {r4, r5, r7, pc}
0003B6E8: movs       r0, #0x10
0003B6EA: blx        #0x3188c
0003B6EE: mov        r4, r0
0003B6F0: blx        #0x31b80
0003B6F4: ldr        r2, [pc, #0x34]
0003B6F6: add        r2, pc
0003B6F8: adr        r3, #0x34
0003B6FA: mov        r0, r4
0003B6FC: movs       r1, #1
0003B6FE: blx        #0x31b8c
0003B702: ldr        r0, [pc, #0x3c]
0003B704: ldr        r1, [pc, #0x3c]
0003B706: ldr        r2, [pc, #0x40]
0003B708: add        r0, pc
0003B70A: add        r1, pc
0003B70C: add        r2, pc
0003B70E: ldr        r0, [r0]
0003B710: ldr        r1, [r1]
0003B712: ldr        r2, [r2]
0003B714: adds       r0, #8
0003B716: str        r0, [r4]
0003B718: mov        r0, r4
0003B71A: blx        #0x318a4
0003B71E: mov        r5, r0
0003B720: mov        r0, r4
0003B722: blx        #0x319ac
0003B726: mov        r0, r5
0003B728: bl         #0x85740
0003B72C: add        r1, sp, #0x38
0003B72E: movs       r6, r0
0003B730: ldr        r1, [r5, #0x74]
0003B732: ldrsh      r3, [r6, r5]
0003B734: str        r2, [r4, #0x14]
0003B736: str        r3, [r6, #0x54]
0003B738: subs       r2, #0x3a
0003B73A: ldr        r3, [r4, #0x44]
0003B73C: str        r5, [r4, #0x14]
0003B73E: lsls       r2, r6, #1
0003B740: str        r6, [sp, #0x70]
0003B742: movs       r6, r0
0003B744: str        r6, [sp, #0x78]
0003B746: movs       r6, r0
0003B748: str        r6, [sp, #0x80]
0003B74A: movs       r6, r0
_ZNSt6__ndk18ios_baseD2Ev: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 0003B818 size 5C
0003B818: push       {r4, r5, r7, lr}
0003B81A: add        r7, sp, #8
0003B81C: mov        r4, r0
0003B81E: ldr        r0, [pc, #0x50]
0003B820: add        r0, pc
0003B822: ldr        r1, [r0]
0003B824: ldr        r0, [r4, #0x28]
0003B826: adds       r1, #8
0003B828: str        r1, [r4]
0003B82A: cbz        r0, #0x3b846
0003B82C: subs       r5, r0, #1
0003B82E: ldrd       r0, r1, [r4, #0x20]
0003B832: ldr.w      r2, [r1, r5, lsl #2]
0003B836: ldr.w      r3, [r0, r5, lsl #2]
0003B83A: movs       r0, #0
0003B83C: mov        r1, r4
0003B83E: blx        r3
0003B840: subs       r5, #1
0003B842: adds       r0, r5, #1
0003B844: bne        #0x3b82e
0003B846: add.w      r0, r4, #0x1c
0003B84A: blx        #0x31ba4
0003B84E: ldr        r0, [r4, #0x20]
0003B850: blx        #0x31808
0003B854: ldr        r0, [r4, #0x24]
0003B856: blx        #0x31808
0003B85A: ldr        r0, [r4, #0x30]
0003B85C: blx        #0x31808
0003B860: ldr        r0, [r4, #0x3c]
0003B862: blx        #0x31808
0003B866: mov        r0, r4
0003B868: pop        {r4, r5, r7, pc}
0003B86A: bl         #0x38d6c
0003B86E: nop
0003B870: str        r5, [sp, #0x50]
0003B872: movs       r6, r0
_ZNSt6__ndk19to_stringEf: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 000664F4 size DC
000664F4: push       {r4, r5, r6, r7, lr}
000664F6: add        r7, sp, #0xc
000664F8: push.w     {r8, sb, fp}
000664FC: vpush      {d8}
00066500: sub        sp, #0x18
00066502: mov        r8, r0
00066504: ldr        r0, [pc, #0xbc]
00066506: mov        r4, r1
00066508: movs       r1, #0
0006650A: add        r0, pc
0006650C: str        r1, [sp, #0x10]
0006650E: ldr        r0, [r0]
00066510: ldr        r0, [r0]
00066512: str        r0, [sp, #0x14]
00066514: strd       r1, r1, [sp, #8]
00066518: add        r5, sp, #8
0006651A: movs       r1, #0xa
0006651C: movs       r2, #0
0006651E: mov        r0, r5
00066520: blx        #0x32090
00066524: vmov       s0, r4
00066528: ldrb.w     r1, [sp, #8]
0006652C: ldr        r6, [sp, #0xc]
0006652E: adr        r4, #0x98
00066530: vcvt.f64.f32 d8, s0
00066534: tst.w      r1, #1
00066538: it         eq
0006653A: lsreq      r6, r1, #1
0006653C: mov.w      sb, #1
00066540: b          #0x66558
00066542: mov        r6, r1
00066544: b          #0x6654a
00066546: orr.w      r6, sb, r6, lsl #1
0006654A: mov        r0, r5
0006654C: mov        r1, r6
0006654E: movs       r2, #0
00066550: blx        #0x32090
00066554: ldrb.w     r1, [sp, #8]
00066558: lsls       r1, r1, #0x1f
0006655A: ldr        r0, [sp, #0x10]
0006655C: add.w      r1, r6, #1
00066560: vstr       d8, [sp]
00066564: it         eq
00066566: orreq      r0, r5, #1
0006656A: mov        r2, r4
0006656C: bl         #0x7e270
00066570: mov        r1, r0
00066572: cmp        r1, #0
00066574: blt        #0x66546
00066576: cmp        r1, r6
00066578: bhi        #0x66542
0006657A: add        r4, sp, #8
0006657C: movs       r2, #0
0006657E: mov        r0, r4
00066580: blx        #0x32090
00066584: ldm.w      r4, {r0, r1, r2}
00066588: stm.w      r8, {r0, r1, r2}
0006658C: ldr        r0, [pc, #0x3c]
0006658E: ldr        r1, [sp, #0x14]
00066590: add        r0, pc
00066592: ldr        r0, [r0]
00066594: ldr        r0, [r0]
00066596: subs       r0, r0, r1
00066598: itttt      eq
0006659A: addeq      sp, #0x18
0006659C: vpopeq     {d8}
000665A0: popeq.w    {r8, sb, fp}
000665A4: popeq      {r4, r5, r6, r7, pc}
000665A6: blx        #0x31778
000665AA: b          #0x665ae
000665AC: b          #0x665ae
000665AE: mov        r4, r0
000665B0: ldrb.w     r0, [sp, #8]
000665B4: lsls       r0, r0, #0x1f
000665B6: itt        ne
000665B8: ldrne      r0, [sp, #0x10]
000665BA: blxne      #0x31730
000665BE: mov        r0, r4
000665C0: bl         #0x85740
000665C4: b          #0x66564
000665C6: movs       r3, r0
000665C8: str        r5, [r4, #0x60]
000665CA: movs       r0, r0
000665CC: b          #0x66460
000665CE: movs       r3, r0
_ZNSt8bad_castC1Ev: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 0007D658 size 10
0007D658: ldr        r1, [pc, #8]
0007D65A: add        r1, pc
0007D65C: ldr        r1, [r1]
0007D65E: adds       r1, #8
0007D660: str        r1, [r0]
0007D662: bx         lr
0007D664: ldrb       r6, [r1, #5]
0007D666: movs       r2, r0
_ZdlPv: resources\lib\armeabi-v7a\libBlueDoveMediaRender.so SHA256 2524d61faec2013daab4036db590bdb3c3af18aacd9c82def0cb434520a99a07 VA 0000C784 size 8
0000C784: cbz        r0, #0xc78a
0000C786: b.w        #0xed48
0000C78A: bx         lr
_Znwj: resources\lib\armeabi-v7a\libBlueDoveMediaRender.so SHA256 2524d61faec2013daab4036db590bdb3c3af18aacd9c82def0cb434520a99a07 VA 0000D018 size 70
0000D018: push       {r3, r4, r5, lr}
0000D01A: mov        r5, r0
0000D01C: ldr        r4, [pc, #0x58]
0000D01E: add        r4, pc
0000D020: b          #0xd03c
0000D022: dmb        sy
0000D026: ldrex      r3, [r4]
0000D02A: adds       r1, r3, #0
0000D02C: strex      r2, r1, [r4]
0000D030: cmp        r2, #0
0000D032: bne        #0xd026
0000D034: dmb        sy
0000D038: cbz        r3, #0xd048
0000D03A: blx        r3
0000D03C: mov        r0, r5
0000D03E: blx        #0x7b54
0000D042: cmp        r0, #0
0000D044: beq        #0xd022
0000D046: pop        {r3, r4, r5, pc}
0000D048: movs       r0, #4
0000D04A: bl         #0xc52c
0000D04E: mov        r4, r0
0000D050: bl         #0xdc10
0000D054: ldr        r3, [pc, #0x24]
0000D056: mov        r0, r4
0000D058: ldr        r1, [pc, #0x24]
0000D05A: ldr        r2, [pc, #0x28]
0000D05C: add        r3, pc
0000D05E: adds       r3, #8
0000D060: add        r1, pc
0000D062: add        r2, pc
0000D064: str        r3, [r4]
0000D066: bl         #0xc5a8
0000D06A: adds       r1, #1
0000D06C: beq        #0xd072
0000D06E: bl         #0xd770
0000D072: bl         #0xd880
0000D076: nop
0000D078: str        r2, [r3, #0x10]
0000D07A: movs       r0, r0
0000D07C: ldrsb      r0, [r5, r2]
0000D07E: movs       r0, r0
0000D080: ldrsb      r0, [r6, r1]
0000D082: movs       r0, r0
0000D084: mrc2       p15, #6, apsr_nzcv, c3, c15, #7
__aeabi_memclr4: no definition in packaged ELF libraries; platform/runtime boundary, not a recovered shipped body
__cxa_allocate_exception: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 0007A8D2 size 28
0007A8D2: push       {r4, r5, r7, lr}
0007A8D4: add        r7, sp, #8
0007A8D6: add.w      r4, r0, #0x80
0007A8DA: mov        r0, r4
0007A8DC: bl         #0x7c3f4
0007A8E0: mov        r5, r0
0007A8E2: cbz        r5, #0x7a8f2
0007A8E4: mov        r0, r5
0007A8E6: mov        r1, r4
0007A8E8: blx        #0x317e4
0007A8EC: add.w      r0, r5, #0x80
0007A8F0: pop        {r4, r5, r7, pc}
0007A8F2: blx        #0x316f4
0007A8F6: blx        #0x33e9c
__cxa_atexit: no definition in packaged ELF libraries; platform/runtime boundary, not a recovered shipped body
__cxa_guard_acquire: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 0007AF30 size E8
0007AF30: push       {r4, r5, r6, r7, lr}
0007AF32: add        r7, sp, #0xc
0007AF34: str        fp, [sp, #-0x4]!
0007AF38: mov        r4, r0
0007AF3A: ldr        r0, [pc, #0x68]
0007AF3C: add        r0, pc
0007AF3E: blx        #0x316c4
0007AF42: cbnz       r0, #0x7af94
0007AF44: ldrb       r0, [r4]
0007AF46: movs       r5, #0
0007AF48: cmp        r0, #0
0007AF4A: it         eq
0007AF4C: moveq      r5, #1
0007AF4E: bne        #0x7af82
0007AF50: ldr        r5, [pc, #0x80]
0007AF52: ldr        r6, [pc, #0x84]
0007AF54: add        r5, pc
0007AF56: add        r6, pc
0007AF58: ldr        r0, [r4]
0007AF5A: tst.w      r0, #0xff00
0007AF5E: beq        #0x7af72
0007AF60: mov        r0, r5
0007AF62: mov        r1, r6
0007AF64: blx        #0x317a8
0007AF68: cmp        r0, #0
0007AF6A: beq        #0x7af58
0007AF6C: adr        r0, #0x6c
0007AF6E: bl         #0x68a7c
0007AF72: uxtb       r0, r0
0007AF74: movs       r5, #0
0007AF76: cmp        r0, #0
0007AF78: ittt       eq
0007AF7A: moveq      r5, #1
0007AF7C: moveq.w    r0, #0x100
0007AF80: streq      r0, [r4]
0007AF82: ldr        r0, [pc, #0x8c]
0007AF84: add        r0, pc
0007AF86: blx        #0x316d0
0007AF8A: cbnz       r0, #0x7af9a
0007AF8C: mov        r0, r5
0007AF8E: ldr        fp, [sp], #4
0007AF92: pop        {r4, r5, r6, r7, pc}
0007AF94: adr        r0, #0x10
0007AF96: bl         #0x68a7c
0007AF9A: ldr        r0, [pc, #0x78]
0007AF9C: add        r0, pc
0007AF9E: bl         #0x68a7c
0007AFA2: nop
0007AFA4: ldm        r1!, {r2, r5, r7}
0007AFA6: movs       r2, r0
0007AFA8: ldrsh      r7, [r3, r5]
0007AFAA: ldrb       r3, [r4, #1]
0007AFAC: ldrsh      r1, [r4, r5]
0007AFAE: strb       r7, [r4, #0x15]
0007AFB0: strb       r1, [r4, #9]
0007AFB2: ldrsh      r4, [r4, r5]
0007AFB4: str        r1, [r4, #0x34]
0007AFB6: strb       r1, [r6, #0x15]
0007AFB8: strb       r1, [r5, #9]
0007AFBA: movs       r0, #0x65
0007AFBC: str        r6, [r4, #0x14]
0007AFBE: ldr        r1, [r5, #0x44]
0007AFC0: str        r5, [r4, #0x44]
0007AFC2: strb       r0, [r4, #0x10]
0007AFC4: movs       r0, #0x6f
0007AFC6: str        r1, [r4, #0x34]
0007AFC8: strb       r1, [r6, #0x15]
0007AFCA: strb       r1, [r5, #9]
0007AFCC: movs       r0, #0x65
0007AFCE: strb       r5, [r5, #0x15]
0007AFD0: str        r4, [r6, #0x54]
0007AFD2: lsls       r0, r7, #1
0007AFD4: ldm        r1!, {r4, r7}
0007AFD6: movs       r2, r0
0007AFD8: ldm        r1, {r1, r3, r7}
0007AFDA: movs       r2, r0
0007AFDC: ldrsh      r7, [r3, r5]
0007AFDE: ldrb       r3, [r4, #1]
0007AFE0: ldrsh      r1, [r4, r5]
0007AFE2: strb       r7, [r4, #0x15]
0007AFE4: strb       r1, [r4, #9]
0007AFE6: ldrsh      r4, [r4, r5]
0007AFE8: str        r1, [r4, #0x34]
0007AFEA: strb       r1, [r6, #0x15]
0007AFEC: strb       r1, [r5, #9]
0007AFEE: movs       r0, #0x65
0007AFF0: ldr        r3, [r4, #0x74]
0007AFF2: str        r6, [r5, #0x44]
0007AFF4: strb       r1, [r5, #0x11]
0007AFF6: ldr        r1, [r5, #0x74]
0007AFF8: movs       r0, #0x6e
0007AFFA: str        r6, [r6, #0x14]
0007AFFC: ldr        r2, [r6, #0x14]
0007AFFE: str        r1, [r4, #0x24]
0007B000: str        r4, [r5, #0x54]
0007B002: strb       r0, [r4, #0x1c]
0007B004: ldr        r1, [r4, #0x14]
0007B006: movs       r0, #0x74
0007B008: str        r6, [r4, #0x14]
0007B00A: ldr        r1, [r5, #0x44]
0007B00C: str        r5, [r4, #0x44]
0007B00E: movs       r0, r0
0007B010: ldm        r1!, {r2, r3, r4, r6}
0007B012: movs       r2, r0
0007B014: stc2       p0, c0, [pc, #4]!
__cxa_guard_release: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 0007B018 size A8
0007B018: push       {r4, r6, r7, lr}
0007B01A: add        r7, sp, #8
0007B01C: mov        r4, r0
0007B01E: ldr        r0, [pc, #0x38]
0007B020: add        r0, pc
0007B022: blx        #0x316c4
0007B026: cbnz       r0, #0x7b042
0007B028: ldr        r0, [pc, #0x5c]
0007B02A: movs       r1, #1
0007B02C: str        r1, [r4]
0007B02E: add        r0, pc
0007B030: blx        #0x316d0
0007B034: cbnz       r0, #0x7b048
0007B036: ldr        r0, [pc, #0x80]
0007B038: add        r0, pc
0007B03A: blx        #0x3179c
0007B03E: cbnz       r0, #0x7b04e
0007B040: pop        {r4, r6, r7, pc}
0007B042: adr        r0, #0x18
0007B044: bl         #0x68a7c
0007B048: adr        r0, #0x40
0007B04A: bl         #0x68a7c
0007B04E: ldr        r0, [pc, #0x6c]
0007B050: add        r0, pc
0007B052: bl         #0x68a7c
0007B056: nop
0007B058: ldm        r0!, {r6, r7}
0007B05A: movs       r2, r0
0007B05C: ldrsh      r7, [r3, r5]
0007B05E: ldrb       r3, [r4, #1]
0007B060: ldrsh      r1, [r4, r5]
0007B062: strb       r7, [r4, #0x15]
0007B064: strb       r1, [r4, #9]
0007B066: ldrsh      r4, [r4, r5]
0007B068: str        r2, [r6, #0x54]
0007B06A: str        r4, [r5, #0x54]
0007B06C: strb       r1, [r4, #0xd]
0007B06E: movs       r0, #0x65
0007B070: str        r6, [r4, #0x14]
0007B072: ldr        r1, [r5, #0x44]
0007B074: str        r5, [r4, #0x44]
0007B076: strb       r0, [r4, #0x10]
0007B078: movs       r0, #0x6f
0007B07A: str        r1, [r4, #0x34]
0007B07C: strb       r1, [r6, #0x15]
0007B07E: strb       r1, [r5, #9]
0007B080: movs       r0, #0x65
0007B082: strb       r5, [r5, #0x15]
0007B084: str        r4, [r6, #0x54]
0007B086: lsls       r0, r7, #1
0007B088: ldm        r0!, {r1, r4, r5, r7}
0007B08A: movs       r2, r0
0007B08C: ldrsh      r7, [r3, r5]
0007B08E: ldrb       r3, [r4, #1]
0007B090: ldrsh      r1, [r4, r5]
0007B092: strb       r7, [r4, #0x15]
0007B094: strb       r1, [r4, #9]
0007B096: ldrsh      r4, [r4, r5]
0007B098: str        r2, [r6, #0x54]
0007B09A: str        r4, [r5, #0x54]
0007B09C: strb       r1, [r4, #0xd]
0007B09E: movs       r0, #0x65
0007B0A0: str        r6, [r4, #0x14]
0007B0A2: ldr        r1, [r5, #0x44]
0007B0A4: str        r5, [r4, #0x44]
0007B0A6: strb       r0, [r4, #0x10]
0007B0A8: movs       r0, #0x6f
0007B0AA: str        r2, [r6, #0x54]
0007B0AC: str        r4, [r5, #0x54]
0007B0AE: strb       r1, [r4, #0xd]
0007B0B0: movs       r0, #0x65
0007B0B2: strb       r5, [r5, #0x15]
0007B0B4: str        r4, [r6, #0x54]
0007B0B6: lsls       r0, r7, #1
0007B0B8: ldm        r0!, {r2, r3, r5, r7}
0007B0BA: movs       r2, r0
0007B0BC: stc2       p0, c0, [r7, #-4]!
__cxa_throw: resources\lib\armeabi-v7a\libc++_shared.so SHA256 8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a VA 0007A92C size 68
0007A92C: push       {r4, r5, r6, r7, lr}
0007A92E: add        r7, sp, #0xc
0007A930: str        r8, [sp, #-0x4]!
0007A934: mov        r8, r2
0007A936: mov        r6, r1
0007A938: mov        r4, r0
0007A93A: blx        #0x33eb4
0007A93E: mov        r5, r0
0007A940: blx        #0x33ec0
0007A944: str        r0, [r4, #-0x74]
0007A948: blx        #0x33ecc
0007A94C: movw       r1, #0x2b00
0007A950: movt       r1, #0x432b
0007A954: str        r1, [r4, #-0x58]!
0007A958: movw       r1, #0x4e47
0007A95C: movt       r1, #0x434c
0007A960: str        r1, [r4, #4]
0007A962: sub.w      r1, r4, #0x28
0007A966: str        r0, [r4, #-0x18]
0007A96A: movs       r0, #1
0007A96C: stm.w      r1, {r0, r6, r8}
0007A970: ldr        r0, [r5, #4]
0007A972: ldr        r1, [pc, #0x1c]
0007A974: adds       r0, #1
0007A976: str        r0, [r5, #4]
0007A978: mov        r0, r4
0007A97A: add        r1, pc
0007A97C: str        r1, [r4, #8]
0007A97E: bl         #0x85564
0007A982: mov        r0, r4
0007A984: blx        #0x316e8
0007A988: ldr        r0, [r4, #-0x18]
0007A98C: bl         #0x7b208
0007A990: movs       r7, r2
0007A992: movs       r0, r0
__stack_chk_fail: no definition in packaged ELF libraries; platform/runtime boundary, not a recovered shipped body
asinf: no definition in packaged ELF libraries; platform/runtime boundary, not a recovered shipped body
fclose: no definition in packaged ELF libraries; platform/runtime boundary, not a recovered shipped body
fflush: no definition in packaged ELF libraries; platform/runtime boundary, not a recovered shipped body
fseeko: no definition in packaged ELF libraries; platform/runtime boundary, not a recovered shipped body
fwrite: no definition in packaged ELF libraries; platform/runtime boundary, not a recovered shipped body
strlen: no definition in packaged ELF libraries; platform/runtime boundary, not a recovered shipped body
```

</details>

## Appendix C — caller/ownership supplemental openings

<details>
<summary>Caller gates, vtables, literals and ownership evidence</summary>

```text
SDK caller cross-references: named Thumb functions 004E0000..007A0000; immediate calls matched after PLT resolution.
_ZN4Anki5Cozmo17RobotEventHandler13HandleMessageINS0_17ExternalInterface23RobotConnectionResponseEEEvRKT_ 005289AC call 00528A6E -> 00645C20
_ZN4Anki5Cozmo9SdkStatus10ResetRobotEb 0065DCF8 call 0065DEA2 -> 0074F576
_ZN4Anki5Cozmo9SdkStatus10ResetRobotEb 0065DCF8 call 0065DECA -> 0074F4DA
_ZN4Anki5Cozmo9SdkStatus10ResetRobotEb 0065DCF8 call 0065DF6A -> 00751702
_ZN4Anki5Cozmo9SdkStatus10ResetRobotEb 0065DCF8 call 0065DF8C -> 0074B794
_ZN4Anki5Cozmo9SdkStatus9EnterModeEb 0065E104 call 0065E1F8 -> 0065DCF8
_ZN4Anki5Cozmo9SdkStatus12OnDisconnectEb 0065E4D8 call 0065E5D0 -> 0065DCF8

Producer gates only; SDK recipient bodies intentionally not opened here.

RANGE 0065DE80..0065DED6
0065DE80: beq        #0x65de88
0065DE82: ldr        r0, [sp, #8]
0065DE84: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0065DE88: ldrb.w     r0, [r4, #0x79]
0065DE8C: cbz        r0, #0x65deda
0065DE8E: ldr        r5, [r4, #0x48]
0065DE90: add        r6, sp, #0x38
0065DE92: mov        r1, sp
0065DE94: ldr        r0, [r5]
0065DE96: ldr        r7, [r0, #0xc]
0065DE98: movw       r0, #0x101
0065DE9C: strh.w     r0, [sp]
0065DEA0: mov        r0, r6
0065DEA2: blx        #0x4bae3c ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngineC1EONS1_15EnableCubeSleepE -> 0074F576 size=A
0065DEA6: mov        r0, r5
0065DEA8: mov        r1, r6
0065DEAA: blx        r7
0065DEAC: add        r0, sp, #0x38
0065DEAE: blx        #0x4ae0b0 ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngine12ClearCurrentEv -> 00745B80 size=25C
0065DEB2: ldr        r5, [r4, #0x48]
0065DEB4: add        r6, sp, #0x38
0065DEB6: mov        r1, sp
0065DEB8: ldr        r0, [r5]
0065DEBA: ldr        r7, [r0, #0xc]
0065DEBC: mov.w      r0, #-1
0065DEC0: str        r0, [sp, #4]
0065DEC2: movs       r0, #0
0065DEC4: strb.w     r0, [sp]
0065DEC8: mov        r0, r6
0065DECA: blx        #0x4bae48 ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngineC1EONS1_17EnableLightStatesE -> 0074F4DA size=E
0065DECE: mov        r0, r5
0065DED0: mov        r1, r6
0065DED2: blx        r7
0065DED4: add        r0, sp, #0x38

RANGE 0065DF4C..0065DF9A
0065DF4C: mov        r0, r6
0065DF4E: blx        #0x4bae78 ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngineC1EONS1_22DeleteAllCustomObjectsE -> 0074CA80 size=6
0065DF52: mov        r0, r5
0065DF54: mov        r1, r6
0065DF56: blx        r7
0065DF58: add        r0, sp, #0x38
0065DF5A: blx        #0x4ae0b0 ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngine12ClearCurrentEv -> 00745B80 size=25C
0065DF5E: ldr        r5, [r4, #0x48]
0065DF60: add        r6, sp, #0x38
0065DF62: mov        r1, sp
0065DF64: ldr        r0, [r5]
0065DF66: ldr        r7, [r0, #0xc]
0065DF68: mov        r0, r6
0065DF6A: blx        #0x4bae84 ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngineC1EONS1_15StopRobotForSdkE -> 00751702 size=6
0065DF6E: mov        r0, r5
0065DF70: mov        r1, r6
0065DF72: blx        r7
0065DF74: add        r0, sp, #0x38
0065DF76: blx        #0x4ae0b0 ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngine12ClearCurrentEv -> 00745B80 size=25C
0065DF7A: ldr        r4, [r4, #0x48]
0065DF7C: add        r5, sp, #0x38
0065DF7E: mov        r1, sp
0065DF80: ldr        r0, [r4]
0065DF82: ldr        r6, [r0, #0xc]
0065DF84: movs       r0, #1
0065DF86: strb.w     r0, [sp]
0065DF8A: mov        r0, r5
0065DF8C: blx        #0x4bae90 ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngineC1EONS1_15EnableLiftPowerE -> 0074B794 size=A
0065DF90: mov        r0, r4
0065DF92: mov        r1, r5
0065DF94: blx        r6
0065DF96: add        r0, sp, #0x38

RANGE 0065E104..0065E202 _ZN4Anki5Cozmo9SdkStatus9EnterModeEb
0065E104: push.w     {r4, r5, r6, r7, r8, lr}
0065E108: sub.w      sp, sp, #0x440
0065E10C: mov        r4, r0
0065E10E: ldr        r0, [pc, #0x170] ; literal[0065E280]=009E0738
0065E110: add        r0, pc
0065E112: ldr        r0, [r0]
0065E114: ldr        r0, [r0]
0065E116: str.w      r0, [sp, #0x43c]
0065E11A: ldrb.w     r0, [r4, #0x79]
0065E11E: cbz        r0, #0x65e124
0065E120: movs       r5, #1
0065E122: b          #0x65e12e
0065E124: ldrb.w     r5, [r4, #0x7a]
0065E128: cmp        r5, #0
0065E12A: it         ne
0065E12C: movne      r5, #1
0065E12E: cmp        r1, #1
0065E130: bne        #0x65e170
0065E132: ldr        r2, [pc, #0x150] ; literal[0065E284]=00585DC2
0065E134: movs       r0, #0
0065E136: strd       r0, r0, [sp, #0x20]
0065E13A: add        r2, pc
0065E13C: str        r0, [sp, #0x28]
0065E13E: adr        r0, #0x148 ; ADR[0065E288]=b'robot.sdk_mode_on'
0065E140: add        r1, sp, #0x20
0065E142: blx        #0x4a4f90 ; _ZN4Anki4Util7sEventFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D004 size=8C
0065E146: ldr        r0, [sp, #0x20]
0065E148: cbz        r0, #0x65e168
0065E14A: ldr        r1, [sp, #0x24]
0065E14C: cmp        r1, r0
0065E14E: itttt      ne
0065E150: subne.w    r2, r1, #8
0065E154: subne      r2, r2, r0
0065E156: mvnne      r3, #7
0065E15A: bicne.w    r2, r3, r2
0065E15E: itt        ne
0065E160: addne      r1, r1, r2
0065E162: strne      r1, [sp, #0x24]
0065E164: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
0065E168: movs       r0, #1
0065E16A: strb.w     r0, [r4, #0x79]
0065E16E: b          #0x65e17a
0065E170: movs       r0, #1
0065E172: strb.w     r0, [r4, #0x78]
0065E176: strb.w     r0, [r4, #0x7a]
0065E17A: cmp        r5, #0
0065E17C: bne        #0x65e208
0065E17E: ldr        r5, [r4, #0x48]
0065E180: mov        r6, sp
0065E182: ldr        r1, [pc, #0x118] ; literal[0065E29C]=005A03F5
0065E184: movs       r7, #0
0065E186: movs       r2, #0x12
0065E188: ldr        r0, [r5]
0065E18A: add        r1, pc
0065E18C: ldr.w      r8, [r0, #0xc]
0065E190: mov        r0, r6
0065E192: str        r7, [sp, #8]
0065E194: strd       r7, r7, [sp]
0065E198: bl         #0x4e02b2
0065E19C: movw       r0, #0x23f
0065E1A0: str        r0, [sp, #0x10]
0065E1A2: add        r0, sp, #0x10
0065E1A4: strd       r7, r7, [sp, #0x18]
0065E1A8: adds       r0, #4
0065E1AA: ldrb.w     r1, [sp]
0065E1AE: str        r7, [sp, #0x14]
0065E1B0: lsls       r1, r1, #0x1f
0065E1B2: bne        #0x65e1bc
0065E1B4: ldm.w      r6, {r1, r2, r3}
0065E1B8: stm        r0!, {r1, r2, r3}
0065E1BA: b          #0x65e1c4
0065E1BC: ldrd       r2, r1, [sp, #4]
0065E1C0: bl         #0x4e02b2
0065E1C4: add        r0, sp, #0x20
0065E1C6: add        r1, sp, #0x10
0065E1C8: blx        #0x4bae9c ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngineC1EONS1_17PushIdleAnimationE -> 0074EA9A size=24
0065E1CC: add        r1, sp, #0x20
0065E1CE: mov        r0, r5
0065E1D0: blx        r8
0065E1D2: add        r0, sp, #0x20
0065E1D4: blx        #0x4ae0b0 ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngine12ClearCurrentEv -> 00745B80 size=25C
0065E1D8: ldrb.w     r0, [sp, #0x14]
0065E1DC: lsls       r0, r0, #0x1f
0065E1DE: itt        ne
0065E1E0: ldrne      r0, [sp, #0x1c]
0065E1E2: blxne      #0x4a40cc
0065E1E6: ldrb.w     r0, [sp]
0065E1EA: lsls       r0, r0, #0x1f
0065E1EC: itt        ne
0065E1EE: ldrne      r0, [sp, #8]
0065E1F0: blxne      #0x4a40cc
0065E1F4: mov        r0, r4
0065E1F6: movs       r1, #0
0065E1F8: blx        #0x4baea8 ; _ZN4Anki5Cozmo9SdkStatus10ResetRobotEb -> 0065DCF8 size=40C
0065E1FC: blx        #0x4a6d84 ; _ZN4Anki4Util4Time13UniversalTime23GetCurrentTimeInSecondsEv -> 00833D80 size=40

RANGE 0065E4D8..0065E4FA _ZN4Anki5Cozmo9SdkStatus12OnDisconnectEb
0065E4D8: push       {r4, r5, r6, r7, lr}
0065E4DA: sub.w      sp, sp, #0x430
0065E4DE: sub        sp, #4
0065E4E0: mov        r4, r0
0065E4E2: ldr        r0, [pc, #0x194] ; literal[0065E678]=009E0362
0065E4E4: mov        r5, r1
0065E4E6: add        r0, pc
0065E4E8: ldr        r0, [r0]
0065E4EA: ldr        r0, [r0]
0065E4EC: str.w      r0, [sp, #0x430]
0065E4F0: ldrb.w     r0, [r4, #0x78]
0065E4F4: cmp        r0, #0
0065E4F6: beq.w      #0x65e604

RANGE 0065E5B8..0065E604
0065E5B8: ldrb.w     r0, [sp, #4]
0065E5BC: lsls       r0, r0, #0x1f
0065E5BE: itt        ne
0065E5C0: ldrne      r0, [sp, #0xc]
0065E5C2: blxne      #0x4a40cc
0065E5C6: ldrb.w     r0, [r4, #0x7c]
0065E5CA: cbz        r0, #0x65e5d4
0065E5CC: mov        r0, r4
0065E5CE: mov        r1, r5
0065E5D0: blx        #0x4baea8 ; _ZN4Anki5Cozmo9SdkStatus10ResetRobotEb -> 0065DCF8 size=40C
0065E5D4: ldrb.w     r0, [r4, #0x7e]
0065E5D8: cbz        r0, #0x65e5fe
0065E5DA: ldr        r5, [r4, #0x48]
0065E5DC: add        r6, sp, #0x18
0065E5DE: add        r1, sp, #4
0065E5E0: ldr        r0, [r5]
0065E5E2: ldr        r7, [r0, #0xc]
0065E5E4: mov.w      r0, #0x100
0065E5E8: strh.w     r0, [sp, #4]
0065E5EC: mov        r0, r6
0065E5EE: blx        #0x4baed8 ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngineC1EONS1_21BlockPoolResetMessageE -> 00751162 size=A
0065E5F2: mov        r0, r5
0065E5F4: mov        r1, r6
0065E5F6: blx        r7
0065E5F8: add        r0, sp, #0x18
0065E5FA: blx        #0x4ae0b0 ; _ZN4Anki5Cozmo17ExternalInterface19MessageGameToEngine12ClearCurrentEv -> 00745B80 size=25C
0065E5FE: movs       r0, #0
0065E600: strb.w     r0, [r4, #0x78]

RANGE 0050FD70..0050FD94
0050FD70: mov        r0, r4
0050FD72: mov        r1, r5
0050FD74: blx        #0x4a76e4 ; _ZN4Anki5Cozmo12MapComponentC1EPNS0_5RobotE -> 0067D99C size=E4
0050FD78: str.w      r4, [r5, #0x25c]
0050FD7C: mov.w      r0, #0x160
0050FD80: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
0050FD84: mov        r6, r0
0050FD86: ldr        r2, [r5]
0050FD88: mov        r0, r6
0050FD8A: mov        r1, r5
0050FD8C: blx        #0x4a76f0 ; _ZN4Anki5Cozmo18NVStorageComponentC1ERNS0_5RobotEPKNS0_12CozmoContextE -> 00642848 size=2C4
0050FD90: str.w      r6, [r5, #0x260]

RANGE 00641D4A..00641D6E _ZNSt6__ndk16__treeINS_12__value_typeIhN4Anki5Cozmo17MovementComponent17FaceLayerToRemoveEEENS_19__map_value_compareIhS6_NS_4lessIhEELb1EEENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE
00641D4A: push       {r4, r5, r7, lr}
00641D4C: mov        r4, r1
00641D4E: mov        r5, r0
00641D50: cbz        r4, #0x641d6c
00641D52: ldr        r1, [r4]
00641D54: mov        r0, r5
00641D56: blx        #0x4b95f4 ; _ZNSt6__ndk16__treeINS_12__value_typeIhN4Anki5Cozmo17MovementComponent17FaceLayerToRemoveEEENS_19__map_value_compareIhS6_NS_4lessIhEELb1EEENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE -> 00641D4A size=24
00641D5A: ldr        r1, [r4, #4]
00641D5C: mov        r0, r5
00641D5E: blx        #0x4b95f4 ; _ZNSt6__ndk16__treeINS_12__value_typeIhN4Anki5Cozmo17MovementComponent17FaceLayerToRemoveEEENS_19__map_value_compareIhS6_NS_4lessIhEELb1EEENS_9allocatorIS6_EEE7destroyEPNS_11__tree_nodeIS6_PvEE -> 00641D4A size=24
00641D62: mov        r0, r4
00641D64: pop.w      {r4, r5, r7, lr}
00641D68: b.w        #0x8ca88c
00641D6C: pop        {r4, r5, r7, pc}

RANGE 00635DFC..00635E72
00635DFC: blx        #0x4b900c ; _ZNSt6__ndk16__treeINS_12__value_typeIjN4Anki5Cozmo11ActiveAccelEEENS_19__map_value_compareIjS5_NS_4lessIjEELb1EEENS_9allocatorIS5_EEE7destroyEPNS_11__tree_nodeIS5_PvEE -> 00635DE8 size=24
00635E00: mov        r0, r4
00635E02: pop.w      {r4, r5, r7, lr}
00635E06: b.w        #0x8ca88c
00635E0A: pop        {r4, r5, r7, pc}
00635E0C: push       {r4, r5, r7, lr}
00635E0E: mov        r4, r1
00635E10: mov        r5, r0
00635E12: cbz        r4, #0x635e2e
00635E14: ldr        r1, [r4]
00635E16: mov        r0, r5
00635E18: blx        #0x4b909c ; _ZNSt6__ndk16__treeINS_12__value_typeIjN4Anki5Cozmo10ObjectTypeEEENS_19__map_value_compareIjS5_NS_4lessIjEELb1EEENS_9allocatorIS5_EEE7destroyEPNS_11__tree_nodeIS5_PvEE -> 00635E0C size=24
00635E1C: ldr        r1, [r4, #4]
00635E1E: mov        r0, r5
00635E20: blx        #0x4b909c ; _ZNSt6__ndk16__treeINS_12__value_typeIjN4Anki5Cozmo10ObjectTypeEEENS_19__map_value_compareIjS5_NS_4lessIjEELb1EEENS_9allocatorIS5_EEE7destroyEPNS_11__tree_nodeIS5_PvEE -> 00635E0C size=24
00635E24: mov        r0, r4
00635E26: pop.w      {r4, r5, r7, lr}
00635E2A: b.w        #0x8ca88c
00635E2E: pop        {r4, r5, r7, pc}
00635E30: b.w        #0x8ca88c
00635E34: push       {r4, lr}
00635E36: mov        r4, r0
00635E38: movs       r0, #0x10
00635E3A: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
00635E3E: ldr        r1, [pc, #0x18] ; literal[00635E58]=00A099F6
00635E40: mov        ip, r0
00635E42: add        r1, pc
00635E44: ldr        r1, [r1]
00635E46: adds       r1, #8
00635E48: str        r1, [ip], #4
00635E4C: adds       r1, r4, #4
00635E4E: ldm.w      r1, {r2, r3, r4}
00635E52: stm.w      ip, {r2, r3, r4}
00635E56: pop        {r4, pc}
00635E58: ldr        r1, [sp, #0x3d8]
00635E5A: lsls       r0, r4, #2
00635E5C: ldr        r2, [pc, #0x14] ; literal[00635E74]=00A099D8
00635E5E: adds       r0, #4
00635E60: add        r2, pc
00635E62: ldr        r2, [r2]
00635E64: adds       r2, #8
00635E66: str        r2, [r1], #4
00635E6A: ldm.w      r0, {r2, r3, ip}
00635E6E: stm.w      r1, {r2, r3, ip}

RANGE 006480AA..00648202 _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE19__add_back_capacityEv
006480AA: push.w     {r4, r5, r6, r7, r8, lr}
006480AE: sub        sp, #0x18
006480B0: mov        r4, r0
006480B2: ldr        r3, [r4, #0x10]
006480B4: cmp        r3, #0xaa
006480B6: blo        #0x648110
006480B8: ldrd       r1, r2, [r4, #4]
006480BC: subs       r3, #0xaa
006480BE: ldr        r0, [r4, #0xc]
006480C0: str        r3, [r4, #0x10]
006480C2: ldr        r6, [r1], #4
006480C6: cmp        r2, r0
006480C8: str        r1, [r4, #4]
006480CA: bne.w      #0x648292
006480CE: ldr        r3, [r4]
006480D0: cmp        r1, r3
006480D2: bls.w      #0x64821a
006480D6: subs       r0, r1, r3
006480D8: movs       r3, #1
006480DA: subs       r2, r2, r1
006480DC: add.w      r0, r3, r0, asr #2
006480E0: movs       r3, #0
006480E2: asrs       r7, r2, #2
006480E4: cmp.w      r3, r2, asr #2
006480E8: add.w      r0, r0, r0, lsr #31
006480EC: sub.w      r8, r3, r0, asr #1
006480F0: asr.w      r0, r0, #1
006480F4: sub.w      r5, r1, r0, lsl #2
006480F8: beq        #0x648102
006480FA: mov        r0, r5
006480FC: blx        #0x4a6eec ; __aeabi_memmove4 IMPORT (resolve packaged dependencies before calling external)
00648100: ldr        r1, [r4, #4]
00648102: add.w      r0, r1, r8, lsl #2
00648106: add.w      r2, r5, r7, lsl #2
0064810A: str        r0, [r4, #4]
0064810C: str        r2, [r4, #8]
0064810E: b          #0x648292
00648110: ldrd       r0, r7, [r4]
00648114: ldrd       r1, r3, [r4, #8]
00648118: subs       r2, r3, r0
0064811A: subs       r0, r1, r7
0064811C: cmp        r0, r2
0064811E: bhs        #0x648186
00648120: mov.w      r0, #0xff0
00648124: cmp        r3, r1
00648126: bne        #0x64820a
00648128: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
0064812C: add        r1, sp, #4
0064812E: str        r0, [sp, #4]
00648130: mov        r0, r4
00648132: blx        #0x4b9bac ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEENS_9allocatorIS4_EEE10push_frontEOS4_ -> 006483AE size=D8
00648136: ldrd       r1, r2, [r4, #4]
0064813A: ldr        r0, [r4, #0xc]
0064813C: ldr        r6, [r1], #4
00648140: cmp        r2, r0
00648142: str        r1, [r4, #4]
00648144: bne.w      #0x648292
00648148: ldr        r3, [r4]
0064814A: cmp        r1, r3
0064814C: bhi        #0x6480d6
0064814E: subs       r0, r0, r3
00648150: movs       r2, #0
00648152: add.w      r3, r4, #0xc
00648156: asrs       r1, r0, #1
00648158: cmp.w      r2, r0, asr #1
0064815C: it         eq
0064815E: moveq      r1, #1
00648160: add        r0, sp, #4
00648162: lsrs       r2, r1, #2
00648164: blx        #0x4b9bb8 ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEERNS_9allocatorIS4_EEEC2EjjS7_ -> 00648488 size=8C
00648168: ldrd       r1, r0, [r4, #4]
0064816C: cmp        r1, r0
0064816E: beq        #0x648256
00648170: ldr        r2, [sp, #0xc]
00648172: ldr        r3, [r1], #4
00648176: str        r3, [r2]
00648178: cmp        r0, r1
0064817A: ldr        r2, [sp, #0xc]
0064817C: add.w      r2, r2, #4
00648180: str        r2, [sp, #0xc]
00648182: bne        #0x648172
00648184: b          #0x648250
00648186: movs       r3, #0
00648188: cmp.w      r3, r2, asr #1
0064818C: asr.w      r1, r2, #1
00648190: asr.w      r2, r0, #2
00648194: add.w      r3, r4, #0xc
00648198: add        r0, sp, #4
0064819A: it         eq
0064819C: moveq      r1, #1
0064819E: blx        #0x4b9bb8 ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEERNS_9allocatorIS4_EEEC2EjjS7_ -> 00648488 size=8C
006481A2: mov.w      r0, #0xff0
006481A6: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
006481AA: mov        r5, r0
006481AC: str        r5, [sp]
006481AE: add        r0, sp, #4
006481B0: mov        r1, sp
006481B2: blx        #0x4b9bc4 ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEERNS_9allocatorIS4_EEE9push_backEOS4_ -> 00648514 size=D8
006481B6: ldr        r5, [r4, #8]
006481B8: add        r6, sp, #4
006481BA: ldr        r1, [r4, #4]
006481BC: cmp        r5, r1
006481BE: beq        #0x6481cc
006481C0: subs       r5, #4
006481C2: mov        r0, r6
006481C4: mov        r1, r5
006481C6: blx        #0x4b9bd0 ; _ZNSt6__ndk114__split_bufferIPNS_8functionIFvvEEERNS_9allocatorIS4_EEE10push_frontERKS4_ -> 006485EC size=D6
006481CA: b          #0x6481ba
006481CC: ldr        r2, [sp, #4]
006481CE: ldr        r0, [r4]
006481D0: str        r2, [r4]
006481D2: ldr        r2, [sp, #8]
006481D4: str        r0, [sp, #4]
006481D6: str        r2, [r4, #4]
006481D8: ldr        r3, [sp, #0xc]
006481DA: str        r1, [sp, #8]
006481DC: ldr        r2, [r4, #8]
006481DE: str        r3, [r4, #8]
006481E0: ldr        r3, [sp, #0x10]
006481E2: cmp        r2, r5
006481E4: str        r2, [sp, #0xc]
006481E6: ldr        r7, [r4, #0xc]
006481E8: str        r3, [r4, #0xc]
006481EA: str        r7, [sp, #0x10]
006481EC: itttt      ne
006481EE: subne      r3, r2, #4
006481F0: subne      r1, r3, r1
006481F2: mvnne      r3, #3
006481F6: bicne.w    r1, r3, r1
006481FA: itt        ne
006481FC: addne      r1, r1, r2
006481FE: strne      r1, [sp, #0xc]
00648200: cmp        r0, #0

RANGE 0054EC7C..0054ED64 _ZN4Anki5Cozmo15ICompoundAction9AddActionEPNS0_13IActionRunnerEbb
0054EC7C: push.w     {r4, r5, r6, r7, r8, sb, lr}
0054EC80: sub        sp, #0x54
0054EC82: mov        r7, r0
0054EC84: ldr        r0, [pc, #0xd0] ; literal[0054ED58]=00AEFBBE
0054EC86: mov        sb, r2
0054EC88: mov        r6, r1
0054EC8A: add        r0, pc
0054EC8C: cmp        r3, #1
0054EC8E: ldr        r0, [r0]
0054EC90: ldr        r0, [r0]
0054EC92: str        r0, [sp, #0x50]
0054EC94: mov.w      r0, #0
0054EC98: str        r0, [sp, #0x30]
0054EC9A: bne        #0x54ecc4
0054EC9C: ldr        r0, [pc, #0xbc] ; literal[0054ED5C]=00AD3410
0054EC9E: add        r4, sp, #0x38
0054ECA0: add        r1, sp, #0x20
0054ECA2: str        r4, [sp, #0x48]
0054ECA4: add        r0, pc
0054ECA6: adds       r0, #8
0054ECA8: str        r0, [sp, #0x38]
0054ECAA: mov        r0, r4
0054ECAC: blx        #0x4ab7a0 ; _ZNSt6__ndk18functionIFbN4Anki5Cozmo12ActionResultEPKNS2_13IActionRunnerEEE4swapERS8_ -> 0054FD58 size=C4
0054ECB0: ldr        r0, [sp, #0x48]
0054ECB2: cmp        r4, r0
0054ECB4: beq        #0x54ecbe
0054ECB6: cbz        r0, #0x54ecc4
0054ECB8: ldr        r1, [r0]
0054ECBA: ldr        r1, [r1, #0x14]
0054ECBC: b          #0x54ecc2
0054ECBE: ldr        r1, [r0]
0054ECC0: ldr        r1, [r1, #0x10]
0054ECC2: blx        r1
0054ECC4: ldr        r0, [r6]
0054ECC6: ldr        r5, [r0, #0x1c]
0054ECC8: add.w      r8, sp, #0x20
0054ECCC: add        r0, sp, #8
0054ECCE: mov        r1, r8
0054ECD0: blx        #0x4ab7ac ; _ZNSt6__ndk18functionIFbN4Anki5Cozmo12ActionResultEPKNS2_13IActionRunnerEEEC2ERKS8_ -> 0054ED64 size=2A
0054ECD4: ldr        r0, [sp, #0x70]
0054ECD6: add        r4, sp, #8
0054ECD8: str        r0, [sp]
0054ECDA: mov        r0, r7
0054ECDC: mov        r1, r6
0054ECDE: mov        r2, sb
0054ECE0: mov        r3, r4
0054ECE2: blx        r5
0054ECE4: ldr        r0, [sp, #0x18]
0054ECE6: cmp        r4, r0
0054ECE8: beq        #0x54ecf2
0054ECEA: cbz        r0, #0x54ecf8
0054ECEC: ldr        r1, [r0]
0054ECEE: ldr        r1, [r1, #0x14]
0054ECF0: b          #0x54ecf6
0054ECF2: ldr        r1, [r0]
0054ECF4: ldr        r1, [r1, #0x10]
0054ECF6: blx        r1
0054ECF8: ldr        r0, [sp, #0x30]
0054ECFA: cmp        r8, r0
0054ECFC: beq        #0x54ed06
0054ECFE: cbz        r0, #0x54ed0c
0054ED00: ldr        r1, [r0]
0054ED02: ldr        r1, [r1, #0x14]
0054ED04: b          #0x54ed0a
0054ED06: ldr        r1, [r0]
0054ED08: ldr        r1, [r1, #0x10]
0054ED0A: blx        r1
0054ED0C: ldr        r0, [pc, #0x50] ; literal[0054ED60]=00AEFB38
0054ED0E: ldr        r1, [sp, #0x50]
0054ED10: add        r0, pc
0054ED12: ldr        r0, [r0]
0054ED14: ldr        r0, [r0]
0054ED16: subs       r0, r0, r1
0054ED18: itt        eq
0054ED1A: addeq      sp, #0x54
0054ED1C: popeq.w    {r4, r5, r6, r7, r8, sb, pc}
0054ED20: blx        #0x4a4fe4 ; __stack_chk_fail IMPORT (resolve packaged dependencies before calling external)
0054ED24: mov        r5, r0
0054ED26: ldr        r0, [sp, #0x18]
0054ED28: cmp        r4, r0
0054ED2A: bne        #0x54ed32
0054ED2C: ldr        r1, [r0]
0054ED2E: ldr        r1, [r1, #0x10]
0054ED30: b          #0x54ed38
0054ED32: cbz        r0, #0x54ed3e
0054ED34: ldr        r1, [r0]
0054ED36: ldr        r1, [r1, #0x14]
0054ED38: blx        r1
0054ED3A: b          #0x54ed3e
0054ED3C: mov        r5, r0
0054ED3E: ldr        r0, [sp, #0x30]
0054ED40: cmp        r8, r0
0054ED42: bne        #0x54ed4a
0054ED44: ldr        r1, [r0]
0054ED46: ldr        r1, [r1, #0x10]
0054ED48: b          #0x54ed50
0054ED4A: cbz        r0, #0x54ed52
0054ED4C: ldr        r1, [r0]
0054ED4E: ldr        r1, [r1, #0x14]
0054ED50: blx        r1
0054ED52: mov        r0, r5
0054ED54: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)

RANGE 00642B0C..00642B60 _ZN4Anki5Cozmo18NVStorageComponent8SetStateENS1_9NVSCStateE
00642B0C: push       {r4, r5, r7, lr}
00642B0E: sub        sp, #0x18
00642B10: mov        r5, r0
00642B12: movs       r0, #0
00642B14: strd       r0, r0, [sp, #0xc]
00642B18: mov        r4, r1
00642B1A: str        r0, [sp, #0x14]
00642B1C: ldr        r0, [pc, #0x70] ; literal[00642B90]=005B8916
00642B1E: ldr        r1, [r5, #8]
00642B20: add        r0, pc
00642B22: strd       r1, r4, [sp]
00642B26: adr        r1, #0x6c ; ADR[00642B94]=b'NVStorageComponent.SetState'
00642B28: add        r2, sp, #0xc
00642B2A: adr        r3, #0x84 ; ADR[00642BB0]=b'PrevState: %d, NewState: %d'
00642B2C: blx        #0x4a5b84 ; _ZN4Anki4Util16sChanneledDebugFEPKcS2_RKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D818 size=7C
00642B30: ldr        r0, [sp, #0xc]
00642B32: cbz        r0, #0x642b52
00642B34: ldr        r1, [sp, #0x10]
00642B36: cmp        r1, r0
00642B38: itttt      ne
00642B3A: subne.w    r2, r1, #8
00642B3E: subne      r2, r2, r0
00642B40: mvnne      r3, #7
00642B44: bicne.w    r2, r3, r2
00642B48: itt        ne
00642B4A: addne      r1, r1, r2
00642B4C: strne      r1, [sp, #0x10]
00642B4E: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00642B52: cbnz       r4, #0x642b60
00642B54: movs       r0, #0
00642B56: strb.w     r0, [r5, #0x48]
00642B5A: strb       r0, [r5, #0x1c]
00642B5C: strb.w     r0, [r5, #0x78]
Ownership vtable bindings: ARM_ABS32 references resolve to named function; relative entries retain Thumb bit.
address point 0101FE20
0101FE20 +00 raw=0051D8B5 relocation=0x101fe20 ARM_RELATIVE (32) 0x0000 0x00
0101FE24 +04 raw=0051D8D5 relocation=0x101fe24 ARM_RELATIVE (32) 0x0000 0x00
0101FE28 +08 raw=0051D8F9 relocation=0x101fe28 ARM_RELATIVE (32) 0x0000 0x00
0101FE2C +0C raw=00000008 relocation=0x101fe2c ARM_ABS32 (32) 0x0000 0x101 _ZTVN10__cxxabiv120__si_class_type_infoE
0101FE30 +10 raw=00000000 relocation=0x101fe30 ARM_ABS32 (32) 0x0000 0x988 _ZTSN6Signal3Lib11ProtoSignalIFvRKN4Anki5Cozmo9AnkiEventINS3_14RobotInterface13RobotToEngineEEEENS0_16CollectorDefaultIvEEE11ProtoHandleE
0101FE34 +14 raw=00000000 relocation=0x101fe34 ARM_ABS32 (32) 0x0000 0x418 _ZTIN6Signal3Lib10HandleBaseE
0101FE38 +18 raw=00000000 relocation=None
0101FE3C +1C raw=00000000 relocation=0x101fe3c ARM_ABS32 (32) 0x0000 0x989 _ZTINSt6__ndk110__function6__funcINS_6__bindIMN4Anki5Cozmo22RobotDataBackupManagerEFvRKNS4_9AnkiEventINS4_14RobotInterface13RobotToEngineEEEEJPS5_RNS_12placeholders4__phILi1EEEEEENS_9allocatorISJ_EEFvSB_EEE
0101FE40 +20 raw=0051DB41 relocation=0x101fe40 ARM_RELATIVE (32) 0x0000 0x00
address point 0102EA48
0102EA48 +00 raw=00000000 relocation=0x102ea48 ARM_ABS32 (32) 0x0000 0x2a6b _ZN4Anki5Cozmo18CubeAccelComponentD2Ev
0102EA4C +04 raw=00000000 relocation=0x102ea4c ARM_ABS32 (32) 0x0000 0x2a6c _ZN4Anki5Cozmo18CubeAccelComponentD0Ev
0102EA50 +08 raw=00000008 relocation=0x102ea50 ARM_ABS32 (32) 0x0000 0x163 _ZTVN10__cxxabiv121__vmi_class_type_infoE
0102EA54 +0C raw=00000000 relocation=0x102ea54 ARM_ABS32 (32) 0x0000 0x2a92 _ZTSN4Anki5Cozmo18CubeAccelComponentE
0102EA58 +10 raw=00000000 relocation=None
0102EA5C +14 raw=00000001 relocation=None
0102EA60 +18 raw=0101E480 relocation=0x102ea60 ARM_RELATIVE (32) 0x0000 0x00
0102EA64 +1C raw=00000000 relocation=None
0102EA68 +20 raw=00000000 relocation=None
address point 0102EFBC
0102EFBC +00 raw=00641CFD relocation=0x102efbc ARM_RELATIVE (32) 0x0000 0x00
0102EFC0 +04 raw=00641D3D relocation=0x102efc0 ARM_RELATIVE (32) 0x0000 0x00
0102EFC4 +08 raw=00000000 relocation=None
0102EFC8 +0C raw=00000000 relocation=None
0102EFCC +10 raw=00000000 relocation=None
0102EFD0 +14 raw=00000008 relocation=0x102efd0 ARM_ABS32 (32) 0x0000 0x163 _ZTVN10__cxxabiv121__vmi_class_type_infoE
0102EFD4 +18 raw=00000000 relocation=0x102efd4 ARM_ABS32 (32) 0x0000 0x2b62 _ZTSN4Anki5Cozmo17MovementComponentE
0102EFD8 +1C raw=00000000 relocation=None
0102EFDC +20 raw=00000001 relocation=None
address point 0102F388
0102F388 +00 raw=00000000 relocation=0x102f388 ARM_ABS32 (32) 0x0000 0x2b7d _ZN4Anki5Cozmo18NVStorageComponentD2Ev
0102F38C +04 raw=00000000 relocation=0x102f38c ARM_ABS32 (32) 0x0000 0x2b7e _ZN4Anki5Cozmo18NVStorageComponentD0Ev
0102F390 +08 raw=00000008 relocation=0x102f390 ARM_ABS32 (32) 0x0000 0x163 _ZTVN10__cxxabiv121__vmi_class_type_infoE
0102F394 +0C raw=00000000 relocation=0x102f394 ARM_ABS32 (32) 0x0000 0x2bcf _ZTSN4Anki5Cozmo18NVStorageComponentE
0102F398 +10 raw=00000000 relocation=None
0102F39C +14 raw=00000001 relocation=None
0102F3A0 +18 raw=0101E480 relocation=0x102f3a0 ARM_RELATIVE (32) 0x0000 0x00
0102F3A4 +1C raw=00000000 relocation=None
0102F3A8 +20 raw=00000000 relocation=None
address point 0101F2C4
0101F2C4 +00 raw=00000000 relocation=0x101f2c4 ARM_ABS32 (32) 0x0000 0x64a _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEED2Ev
0101F2C8 +04 raw=00000000 relocation=0x101f2c8 ARM_ABS32 (32) 0x0000 0x67d _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEED0Ev
0101F2CC +08 raw=00000000 relocation=0x101f2cc ARM_ABS32 (32) 0x0000 0x67e _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE5imbueERKNS_6localeE
0101F2D0 +0C raw=00000000 relocation=0x101f2d0 ARM_ABS32 (32) 0x0000 0x67f _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE6setbufEPci
0101F2D4 +10 raw=00000000 relocation=0x101f2d4 ARM_ABS32 (32) 0x0000 0x680 _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE7seekoffExNS_8ios_base7seekdirEj
0101F2D8 +14 raw=00000000 relocation=0x101f2d8 ARM_ABS32 (32) 0x0000 0x681 _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE7seekposENS_4fposI9mbstate_tEEj
0101F2DC +18 raw=00000000 relocation=0x101f2dc ARM_ABS32 (32) 0x0000 0x682 _ZNSt6__ndk113basic_filebufIcNS_11char_traitsIcEEE4syncEv
0101F2E0 +1C raw=004E3E2D relocation=0x101f2e0 ARM_RELATIVE (32) 0x0000 0x00
0101F2E4 +20 raw=004E3E31 relocation=0x101f2e4 ARM_RELATIVE (32) 0x0000 0x00
address point 01021D4C
01021D4C +00 raw=0051DB41 relocation=0x1021d4c ARM_RELATIVE (32) 0x0000 0x00
01021D50 +04 raw=0054D70D relocation=0x1021d50 ARM_RELATIVE (32) 0x0000 0x00
01021D54 +08 raw=0054D711 relocation=0x1021d54 ARM_RELATIVE (32) 0x0000 0x00
01021D58 +0C raw=0054D72D relocation=0x1021d58 ARM_RELATIVE (32) 0x0000 0x00
01021D5C +10 raw=0054D741 relocation=0x1021d5c ARM_RELATIVE (32) 0x0000 0x00
01021D60 +14 raw=0054D743 relocation=0x1021d60 ARM_RELATIVE (32) 0x0000 0x00
01021D64 +18 raw=0054D749 relocation=0x1021d64 ARM_RELATIVE (32) 0x0000 0x00
01021D68 +1C raw=0054D80D relocation=0x1021d68 ARM_RELATIVE (32) 0x0000 0x00
01021D6C +20 raw=0054D825 relocation=0x1021d6c ARM_RELATIVE (32) 0x0000 0x00
address point 0102207C
0102207C +00 raw=00000000 relocation=0x102207c ARM_ABS32 (32) 0x0000 0xe07 _ZN4Anki5Cozmo15ICompoundActionD2Ev
01022080 +04 raw=0054FCA3 relocation=0x1022080 ARM_RELATIVE (32) 0x0000 0x00
01022084 +08 raw=00000000 relocation=0x1022084 ARM_ABS32 (32) 0x0000 0xee9 _ZN4Anki5Cozmo15ICompoundAction5ResetEb
01022088 +0C raw=00000000 relocation=0x1022088 ARM_ABS32 (32) 0x0000 0xea6 _ZNK4Anki5Cozmo15ICompoundAction18GetCompletionUnionERNS0_20ActionCompletedUnionE
0102208C +10 raw=00000000 relocation=0x102208c ARM_ABS32 (32) 0x0000 0xefe _ZN4Anki5Cozmo22CompoundActionParallel14UpdateInternalEv
01022090 +14 raw=0052B0B3 relocation=0x1022090 ARM_RELATIVE (32) 0x0000 0x00
01022094 +18 raw=0052B0B7 relocation=0x1022094 ARM_RELATIVE (32) 0x0000 0x00
01022098 +1C raw=00000000 relocation=0x1022098 ARM_ABS32 (32) 0x0000 0xea8 _ZN4Anki5Cozmo15ICompoundAction9AddActionEPNS0_13IActionRunnerENSt6__ndk18functionIFbNS0_12ActionResultEPKS2_EEEb
0102209C +20 raw=00000000 relocation=0x102209c ARM_ABS32 (32) 0x0000 0xe16 _ZN4Anki5Cozmo15ICompoundAction9AddActionEPNS0_13IActionRunnerEbb
Preset/data/log literal reopenings
Preset height pairs 00000000000000420100000000009842020000000000b84203000000000080bf
006414B8: b':'
006414C8: b'MovementComponent.LockState'
006414E4: b'%s'
00BE3FEC: b'Unnamed'
00BE9DE8: b'[%d] ActionID: %d'
00BEA400: b'[%d] Waiting for lift to get in position: %.1fmm vs. %.1fmm (tol: %f)'
00BEA446: b'MoveLiftToHeightAction.CheckIfDone.StoppedMakingProgress'
00BEA37F: b'[%d] giving up since we stopped moving'
00BEA3A6: b'MoveLiftTo'
00BEA3B1: b'HeightTol %f mm == AngleTol %f rad near height of %f mm. Clipping tol to %f mm'
005495A8: b'MoveLiftToHeightAction.CheckIfDone.WaitingForAck'
005495EC: b'MoveLiftToHeightAction.CheckIfDone.NotInPosition'
0054D7E4: b'MoveLiftToHeightAction.MotorActionAcked'
00645BDC: b'NVStorageComponent.ProcessOnIdleCallbacks.ProcessingCallback'
00BE3F00: b''
00C20053: b'HEAD_TRACK'
00C2005E: b'LIFT_TRACK'
00C20069: b'BODY_TRACK'
00C20074: b'FACE_IMAGE_TRACK'
00C20085: b'EVENT_TRACK'
00C20091: b'BACKPACK_LIGHTS_TRACK'
00C200A7: b'AUDIO_TRACK'
Logger concrete ownership/null dispatch queue provenance (not an extraction of logging producer bodies):

RANGE 0064E738..0064E880 _ZN4Anki5Cozmo20TouchSensorComponentC2ERNS0_5RobotE
0064E738: movs       r2, #0
0064E73A: strd       r1, r2, [r0]
0064E73E: str        r2, [r0, #0xc]
0064E740: strb       r2, [r0, #0x10]
0064E742: str        r2, [r0, #0x14]
0064E744: bx         lr
0064E746: push       {r4, lr}
0064E748: mov        r4, r0
0064E74A: movs       r1, #0
0064E74C: ldr        r0, [r4, #0xc]
0064E74E: str        r1, [r4, #0xc]
0064E750: cbz        r0, #0x64e758
0064E752: ldr        r1, [r0]
0064E754: ldr        r1, [r1, #4]
0064E756: blx        r1
0064E758: mov        r0, r4
0064E75A: pop        {r4, pc}
0064E75C: ldr        r2, [r1]
0064E75E: str        r2, [r0, #4]
0064E760: ldrh.w     r1, [r1, #0x58]
0064E764: strh       r1, [r0, #8]
0064E766: b.w        #0x8ccfcc
0064E76A: movs       r0, r0
0064E76C: push       {r4, r5, r6, r7, lr}
0064E76E: sub        sp, #0x4c
0064E770: mov        r7, r0
0064E772: ldrb       r0, [r7, #0x10]
0064E774: cmp        r0, #0
0064E776: beq.w      #0x64e982
0064E77A: blx        #0x4a4f6c ; _ZN4Anki16BaseStationTimer11getInstanceEv -> 0084BB9C size=48
0064E77E: blx        #0x4a50b0 ; _ZNK4Anki16BaseStationTimer23GetCurrentTimeInSecondsEv -> 0084BCA8 size=4
0064E782: vldr       s0, [r7, #0x14]
0064E786: mov        r1, r0
0064E788: vmov       s4, r1
0064E78C: ldr        r0, [r7, #0xc]
0064E78E: vcmpe.f32  s0, #0
0064E792: vmrs       apsr_nzcv, fpscr
0064E796: vneg.f32   s2, s0
0064E79A: vcmpe.f32  s4, s0
0064E79E: it         mi
0064E7A0: vmovmi.f32 s0, s2
0064E7A4: vmrs       apsr_nzcv, fpscr
0064E7A8: blt        #0x64e7d0
0064E7AA: cbz        r0, #0x64e7d0
0064E7AC: vldr       s2, [pc, #0x260] ; literal[0064EA10]=3727C5AC
0064E7B0: vcmpe.f32  s0, s2
0064E7B4: vmrs       apsr_nzcv, fpscr
0064E7B8: bmi        #0x64e7d0
0064E7BA: blx        #0x4ba020 ; _ZN4Anki4Util17RollingFileLogger5FlushEv -> 0080E8EC size=8C
0064E7BE: ldr        r0, [r7, #0xc]
0064E7C0: movs       r5, #0
0064E7C2: str        r5, [r7, #0xc]
0064E7C4: cbz        r0, #0x64e7cc
0064E7C6: ldr        r1, [r0]
0064E7C8: ldr        r1, [r1, #4]
0064E7CA: blx        r1
0064E7CC: strb       r5, [r7, #0x10]
0064E7CE: b          #0x64e982
0064E7D0: cmp        r0, #0
0064E7D2: bne        #0x64e87a
0064E7D4: ldr        r0, [r7]
0064E7D6: blx        #0x4a82cc ; _ZN4Anki5Cozmo5Robot22GetContextDataPlatformEv -> 00516BD2 size=6
0064E7DA: ldr        r3, [pc, #0x238] ; literal[0064EA14]=00A0CD88
0064E7DC: mov        r1, r0
0064E7DE: movs       r0, #2
0064E7E0: add        r2, sp, #0x14
0064E7E2: str        r0, [sp, #0x14]
0064E7E4: add        r3, pc
0064E7E6: add        r0, sp, #0x30
0064E7E8: blx        #0x4a64fc ; _ZNK4Anki4Util4Data12DataPlatform14pathToResourceERKNS1_5ScopeERKNSt6__ndk112basic_stringIcNS6_11char_traitsIcEENS6_9allocatorIcEEEE -> 0084BDBC size=1FC
0064E7EC: movs       r0, #0xf0
0064E7EE: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
0064E7F2: mov        r5, r0
0064E7F4: ldr        r0, [pc, #0x220] ; literal[0064EA18]=009F0A7E
0064E7F6: movs       r1, #0
0064E7F8: str        r1, [sp, #0x48]
0064E7FA: add        r0, pc
0064E7FC: strd       r1, r1, [sp, #0x40]
0064E800: ldr        r0, [r0]
0064E802: ldr        r6, [r0]
0064E804: mov        r0, r6
0064E806: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
0064E80A: mov        r2, r0
0064E80C: add        r0, sp, #0x40
0064E80E: mov        r1, r6
0064E810: bl         #0x4e02b2
0064E814: mov.w      r0, #0x1400000
0064E818: add        r2, sp, #0x30
0064E81A: add        r3, sp, #0x40
0064E81C: str        r0, [sp]
0064E81E: mov        r0, r5
0064E820: movs       r1, #0
0064E822: blx        #0x4b3828 ; _ZN4Anki4Util17RollingFileLoggerC1EPNS0_8Dispatch5QueueERKNSt6__ndk112basic_stringIcNS5_11char_traitsIcEENS5_9allocatorIcEEEESD_j -> 0080DEFC size=154
0064E826: ldrb.w     r0, [sp, #0x40]
0064E82A: lsls       r0, r0, #0x1f
0064E82C: itt        ne
0064E82E: ldrne      r0, [sp, #0x48]
0064E830: blxne      #0x4a40cc
0064E834: ldr        r0, [r7, #0xc]
0064E836: str        r5, [r7, #0xc]
0064E838: cbz        r0, #0x64e840
0064E83A: ldr        r1, [r0]
0064E83C: ldr        r1, [r1, #4]
0064E83E: blx        r1
0064E840: ldrb.w     r0, [sp, #0x30]
0064E844: lsls       r0, r0, #0x1f
0064E846: itt        ne
0064E848: ldrne      r0, [sp, #0x38]
0064E84A: blxne      #0x4a40cc
0064E84E: add        r6, sp, #0x20
0064E850: movs       r0, #0
0064E852: adr        r1, #0x1c8 ; ADR[0064EA1C]=b'timestamp_ms, touchIntensity\n'
0064E854: ldr        r5, [r7, #0xc]
0064E856: str        r0, [sp, #0x28]
0064E858: movs       r2, #0x1d
0064E85A: strd       r0, r0, [sp, #0x20]
0064E85E: mov        r0, r6
0064E860: bl         #0x4e02b2
0064E864: mov        r0, r5
0064E866: mov        r1, r6
0064E868: blx        #0x4b3870 ; _ZN4Anki4Util17RollingFileLogger5WriteENSt6__ndk112basic_stringIcNS2_11char_traitsIcEENS2_9allocatorIcEEEE -> 0080E17C size=CC
0064E86C: ldrb.w     r0, [sp, #0x20]
0064E870: lsls       r0, r0, #0x1f
0064E872: itt        ne
0064E874: ldrne      r0, [sp, #0x28]
0064E876: blxne      #0x4a40cc
0064E87A: movs       r0, #0
0064E87C: str        r0, [sp, #0x48]

RANGE 00633FA0..00633FF8 _ZN4Anki5Cozmo20CliffSensorComponentC2ERNS0_5RobotE
00633FA0: push       {r4, lr}
00633FA2: mov        r4, r0
00633FA4: movs       r0, #1
00633FA6: str        r1, [r4]
00633FA8: mov.w      r1, #0x190
00633FAC: strh       r0, [r4, #4]
00633FAE: movs       r0, #0
00633FB0: strb       r0, [r4, #6]
00633FB2: str        r0, [r4, #8]
00633FB4: strh       r1, [r4, #0xc]
00633FB6: movs       r1, #0x31
00633FB8: str        r0, [r4, #0x4c]
00633FBA: add.w      r0, r4, #0x18
00633FBE: blx        #0x4a403c ; __aeabi_memclr4 IMPORT (resolve packaged dependencies before calling external)
00633FC2: mov.w      r0, #-1
00633FC6: str.w      r0, [r4, #0x12]
00633FCA: str.w      r0, [r4, #0xe]
00633FCE: mov        r0, r4
00633FD0: pop        {r4, pc}
00633FD2: push       {r4, r5, r6, lr}
00633FD4: mov        r4, r0
00633FD6: blx        #0x4b8f10 ; _ZNSt6__ndk112__deque_baseItNS_9allocatorItEEE5clearEv -> 00634B28 size=94
00633FDA: ldrd       r5, r6, [r4, #4]
00633FDE: cmp        r5, r6
00633FE0: beq        #0x633fee
00633FE2: ldr        r0, [r5], #4
00633FE6: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00633FEA: cmp        r6, r5
00633FEC: bne        #0x633fe2
00633FEE: mov        r0, r4
00633FF0: pop.w      {r4, r5, r6, lr}
00633FF4: b.w        #0x8ccc8c

RANGE 00634016..00634130 _ZN4Anki5Cozmo20CliffSensorComponent15UpdateRobotDataERKNS0_10RobotStateE
00634016: push       {r4, lr}
00634018: mov        r4, r0
0063401A: ldr        r0, [r1, #0x50]
0063401C: ldr        r2, [r1, #0x54]
0063401E: str.w      r2, [r4, #0x12]
00634022: str.w      r0, [r4, #0xe]
00634026: ldr        r0, [r1, #0x4c]
00634028: ldrb.w     r2, [r4, #0x48]
0063402C: ubfx       r0, r0, #0xe, #1
00634030: strb       r0, [r4, #6]
00634032: ldr        r0, [r1]
00634034: cmp        r2, #0
00634036: str        r0, [r4, #8]
00634038: beq        #0x634066
0063403A: blx        #0x4a4f6c ; _ZN4Anki16BaseStationTimer11getInstanceEv -> 0084BB9C size=48
0063403E: blx        #0x4a50b0 ; _ZNK4Anki16BaseStationTimer23GetCurrentTimeInSecondsEv -> 0084BCA8 size=4
00634042: vldr       s0, [r4, #0x4c]
00634046: vmov       s2, r0
0063404A: vcmpe.f32  s2, s0
0063404E: vmrs       apsr_nzcv, fpscr
00634052: bpl        #0x63405e
00634054: mov        r0, r4
00634056: pop.w      {r4, lr}
0063405A: b.w        #0x8ccc9c
0063405E: movs       r0, #0
00634060: str        r0, [r4, #0x4c]
00634062: strb.w     r0, [r4, #0x48]
00634066: pop        {r4, pc}
00634068: push       {r4, r5, r6, r7, lr}
0063406A: sub        sp, #0x3c
0063406C: mov        r7, r0
0063406E: ldr        r0, [r7, #0x44]
00634070: cbnz       r0, #0x6340ec
00634072: ldr        r0, [r7]
00634074: blx        #0x4a82cc ; _ZN4Anki5Cozmo5Robot22GetContextDataPlatformEv -> 00516BD2 size=6
00634078: ldr        r3, [pc, #0x1e4] ; literal[00634260]=00A273BA
0063407A: mov        r1, r0
0063407C: movs       r0, #2
0063407E: add        r2, sp, #0x14
00634080: str        r0, [sp, #0x14]
00634082: add        r3, pc
00634084: add        r0, sp, #0x20
00634086: blx        #0x4a64fc ; _ZNK4Anki4Util4Data12DataPlatform14pathToResourceERKNS1_5ScopeERKNSt6__ndk112basic_stringIcNS6_11char_traitsIcEENS6_9allocatorIcEEEE -> 0084BDBC size=1FC
0063408A: movs       r0, #0xf0
0063408C: blx        #0x4a42a0 ; _Znwj IMPORT (resolve packaged dependencies before calling external)
00634090: mov        r5, r0
00634092: ldr        r0, [pc, #0x1d0] ; literal[00634264]=00A0B1E0
00634094: movs       r1, #0
00634096: str        r1, [sp, #0x38]
00634098: add        r0, pc
0063409A: strd       r1, r1, [sp, #0x30]
0063409E: ldr        r0, [r0]
006340A0: ldr        r6, [r0]
006340A2: mov        r0, r6
006340A4: blx        #0x4a44e0 ; strlen IMPORT (resolve packaged dependencies before calling external)
006340A8: mov        r2, r0
006340AA: add        r0, sp, #0x30
006340AC: mov        r1, r6
006340AE: bl         #0x4e02b2
006340B2: mov.w      r0, #0x1400000
006340B6: add        r2, sp, #0x20
006340B8: add        r3, sp, #0x30
006340BA: str        r0, [sp]
006340BC: mov        r0, r5
006340BE: movs       r1, #0
006340C0: blx        #0x4b3828 ; _ZN4Anki4Util17RollingFileLoggerC1EPNS0_8Dispatch5QueueERKNSt6__ndk112basic_stringIcNS5_11char_traitsIcEENS5_9allocatorIcEEEESD_j -> 0080DEFC size=154
006340C4: ldrb.w     r0, [sp, #0x30]
006340C8: lsls       r0, r0, #0x1f
006340CA: itt        ne
006340CC: ldrne      r0, [sp, #0x38]
006340CE: blxne      #0x4a40cc
006340D2: ldr        r0, [r7, #0x44]
006340D4: str        r5, [r7, #0x44]
006340D6: cbz        r0, #0x6340de
006340D8: ldr        r1, [r0]
006340DA: ldr        r1, [r1, #4]
006340DC: blx        r1
006340DE: ldrb.w     r0, [sp, #0x20]
006340E2: lsls       r0, r0, #0x1f
006340E4: itt        ne
006340E6: ldrne      r0, [sp, #0x28]
006340E8: blxne      #0x4a40cc
006340EC: movs       r0, #0
006340EE: str        r0, [sp, #0x38]
006340F0: strd       r0, r0, [sp, #0x30]
006340F4: ldr        r1, [r7, #8]
006340F6: add        r0, sp, #0x14
006340F8: blx        #0x4a5c74 ; _ZNSt6__ndk19to_stringEj IMPORT (resolve packaged dependencies before calling external)
006340FC: add        r0, sp, #0x14
006340FE: adr        r1, #0x168 ; ADR[00634268]=b', '
00634100: movs       r2, #2
00634102: bl         #0x4e8048
00634106: add        r3, sp, #0x20
00634108: mov        r1, r0
0063410A: ldm.w      r1, {r4, r5, r6}
0063410E: movs       r1, #0
00634110: mov        r2, r3
00634112: stm        r2!, {r4, r5, r6}
00634114: strd       r1, r1, [r0]
00634118: str        r1, [r0, #8]
0063411A: ldrd       r2, r1, [sp, #0x24]
0063411E: ldrb.w     r0, [sp, #0x20]
00634122: ands       r6, r0, #1
00634126: itt        eq
00634128: orreq      r1, r3, #1
0063412C: lsreq      r2, r0, #1
0063412E: add        r0, sp, #0x30
```

</details>
