# R-SUBSCRIBERS — robot-message subscribers

## Item 1 — coverage: every subscriber

All addresses are ELF virtual addresses in the shipped ARMv7 engine. `R` means the supplied robot ID, not a hard-coded wildcard ID. Lifecycle keys are expanded below. Each row is evidence for part of **M2-018, currently RECOVERABLE_GAP**; this report does not assign or change a manifest status. A lambda address identifies the installed callable's operator, not an inferred handler name.

| # / call to 0x00519F8C | Enclosing function (entry) / registration phase | Tag(s) | Bound handler / callable | ID | Removal coverage |
|---|---|---|---|---|---|
| 1 / 0x00519B78 | RobotDataBackupManager constructor, 0x00519A5C | C9 | RobotConnected, GOT 0x0103E98C | R | Backup |
| 2 / 0x0052D1DE | RobotInitialConnection constructor, 0x0052D168 | D2 | HandleFactoryFirmware | R | Initial |
| 3 / 0x0052D236 | Same constructor | EE | HandleFirmwareVersion | R | Initial |
| 4 / 0x0052D28E | Same constructor | C9 | HandleRobotAvailable | R | Initial |
| 5 / 0x0052DE34 | RobotInitialConnection::OnNotified, 0x0052DD98; result argument 0 | ED | Manufacturing-ID lambda, 0x0052E2F8 | R | Initial |
| 6 / 0x00532C84 | RobotToEngineImplMessaging::InitRobotMessageComponent, 0x00532A7C | D3 | HandleDockingStatus | R | Impl |
| 7 / 0x00532D22 | Same Init | F0 | state lambda, 0x00537F52 | R | Impl |
| 8 / 0x00532D88 | Same Init | F1 | animState lambda, 0x00537FCA | R | Impl |
| 9 / 0x00532DEE | Same Init | BA | lambda 0x00538088 | R | Impl |
| 10 / 0x00532E54 | Same Init | BB | lambda 0x0053819C | R | Impl |
| 11 / 0x00532EBA | Same Init | BC | lambda 0x005382B4 | R | Impl |
| 12 / 0x00532F1E | Same Init | BD | lambda 0x005383C0 | R | Impl |
| 13 / 0x00532F82 | Same Init | B2 | mainCycleTimeError lambda, 0x005384CC | R | Impl |
| 14 / 0x00532FE6 | Same Init | B7 | dataDump lambda, 0x0053866A | R | Impl |
| 15 / 0x0053304A | Same Init | DC | imuTemperature lambda, 0x0053880A | R | Impl |
| 16 / 0x0053327A | Reusable Impl member-bind helper, 0x00533234; called by Init | 27 tags listed below | Supplied member function, bound to Robot; generic bind operator 0x005379F2 | R | Impl |
| 17 / 0x0053C180 | Reusable TracePrinter member-bind helper, 0x0053C144; called by constructor 0x0053BC20 | B0, CF, EC | HandleTrace, HandleCrashReport, HandleWiFiFlashID | R | Trace |
| 18 / 0x00543FCA | PlayAnimationAction::Init, 0x00543F20 | CA | animStarted lambda, 0x005452F0 | R | Animation |
| 19 / 0x00544032 | Same Init | CB | animEnded lambda, 0x0054541C | R | Animation |
| 20 / 0x00544092 | Same Init | D5 | animEvent lambda, 0x0054561C | R | Animation |
| 21 / 0x00545ADC | TurnInPlaceAction constructor, 0x005459D4 | C4 | motorActionAck lambda, 0x0054D3F8 | R | Turn |
| 22 / 0x005481BC | MoveHeadToAngleAction constructor, 0x00547E40 | C4 | motorActionAck lambda, 0x0054D624 | R | Head |
| 23 / 0x00548AAC | MoveLiftToHeightAction constructor, 0x0054899C | C4 | motorActionAck lambda, 0x0054D748 | R | Lift |
| 24 / 0x00551706 | IDockAction::Init, 0x005514FC | C5 | movingLiftPostDock lambda, 0x00557C4C | R | Dock |
| 25 / 0x0055176C | Same Init | DA | liftLoad lambda, 0x00557E82 | R | Dock |
| 26 / 0x005BCAA0 | IBehavior::SubscribeToTags(robot ID, RobotToEngineTag set), 0x005BCA4C | Set contents; concrete caller here supplies DA | lambda 0x005BF2CE → IBehavior::HandleEvent<RobotToEngine>, 0x005BF2F8 | Parameter R | Behavior |
| 27 / 0x005CB1BE | BehaviorDockingTestSimple constructor, 0x005CAFB0 | D3 | HandleDockingStatus, GOT 0x0103F284 | R | DockingTest |
| 28 / 0x005D07CE | Reusable BehaviorFactoryTest member-bind helper, 0x005D0798; constructor calls below | D6, F3, B8, EE, D2 | Five member handlers listed below | R | FactoryTest |
| 29 / 0x006308BA | BlockTapFilterComponent constructor, 0x00630844 | B6 | HandleActiveObjectTapped, GOT 0x0103F808 | R | Tap |
| 30 / 0x00630934 | Same constructor | B4 | HandleActiveObjectMoved, GOT 0x0103F80C | R | Tap |
| 31 / 0x006309AE | Same constructor | B5 | HandleActiveObjectStopped, GOT 0x0103F810 | R | Tap |
| 32 / 0x0063526C | CubeAccelComponent constructor, 0x00635204 | F5 | ReceiveObjectAccelData, GOT 0x0103F840 | R | Accel |
| 33 / 0x0064294C | NVStorageComponent constructor, 0x00642848 | CD | HandleNVOpResult, GOT 0x0103F868 | R | NV |
| 34 / 0x00648C8C | PathComponent constructor, 0x00648AA0 | C6 | pathFollowingEvent lambda, 0x0064B370 | R | Path |
| 35 / 0x0067838C | FirmwareUpdater::InitUpdate, 0x006781E0; once per robot-map entry | EF | OTA acknowledgment lambda 0x00678C0A → HandleFlashWriteAck | R | Firmware |

## Item 2 — coverage: registration order at connection

| Step | Recovered order / behavior | Native citation | Record / gap |
|---|---|---|---|
| Construct Robot before initial-connection object | AddRobot calls Robot constructor first; later constructs the RobotInitialConnection map node | 0x0052EE9C, 0x0052EEF0..0x0052EF24; node constructor 0x0052FEAC..0x0052FF04 | M2-018; native read |
| First component subscription | PathComponent: C6 | Robot constructor 0x0050FCF6 → constructor registration 0x00648C8C | M2-018; native read |
| Next component subscriptions | NVStorage's embedded BackupManager: C9 when external interface exists; then NVStorage: CD when context exists | 0x0050FD8C; 0x006428A4 → 0x00519B78; 0x0064294C | M2-018; native read |
| Next | CubeAccelComponent: F5 | 0x0050FE08 → 0x0063526C | M2-018; native read |
| Next | BlockTapFilterComponent: B6, B4, B5 | 0x00510198 → 0x006308BA, 0x00630934, 0x006309AE | M2-018; native read |
| Next | ImplMessaging constructor constructs TracePrinter: B0, CF, EC | 0x00510210; 0x00532944..0x00532A0E; 0x0053BC7E, 0x0053BC90, 0x0053BCA2 | M2-018; native read |
| Next | ImplMessaging::Init: 37 subscriptions in the sequence below | Robot constructor 0x00510348; 0x00532AB2..0x0053304A | M2-018; native read |
| Next possible raw subscribers | Behavior configuration/init can create the factory/docking/lift-load test behaviors, after component Init | 0x00510388; constructor subscription sites in item 1 | M2-018; relative placement read; exact configured creation order UNKNOWN |
| After Robot constructor returns | InitialConnection constructor: D2, EE, C9, if MessageHandler is non-null | 0x0052EE9C before node construction; 0x0052D1DE, 0x0052D236, 0x0052D28E | M2-018; native read |
| Later successful notification | InitialConnection appends ED, then requests manufacturing info | 0x0052DDAA..0x0052DE90, especially 0x0052DE34 | M2-018; native read |
| Shared B4 | BlockTapFilter::HandleActiveObjectMoved → Impl::HandleActiveObjectMoved | 0x00510198 before 0x00510348; 0x00630934, 0x00532B0C | M2-018; native read |
| Shared B5 | BlockTapFilter::HandleActiveObjectStopped → Impl::HandleActiveObjectStopped | Same constructor order; 0x006309AE, 0x00532B1E | M2-018; native read |
| Shared C9 | BackupManager::RobotConnected → Impl::HandleRobotSetHeadID → InitialConnection::HandleRobotAvailable; omit gated/removed entries | 0x0050FD8C before 0x00510348 before 0x0052FEAC; 0x00519B78, 0x00532C1A, 0x0052D28E | M2-018; native read |
| Shared D2 / EE | Impl handlers precede InitialConnection handlers. A FactoryTest constructed during Robot configuration lies between them; one constructed later appends later | 0x00510348 before 0x00510388 before 0x0052FEAC; 0x00532AC4 / 0x00532C2C, 0x005D068A / 0x005D067A, 0x0052D1DE / 0x0052D236 | M2-018; conditional order read; exact live FactoryTest instance presence UNKNOWN |
| Shared ED | Impl::HandleRobotSetBodyID → InitialConnection manufacturing-ID lambda while both remain subscribed | 0x00532CDA before later 0x0052DE34 | M2-018; native read |
| Shared B8 / F3 / D3 | Impl handler precedes corresponding FactoryTest / DockingTest handler created after component Init | 0x00510348 before 0x00510388; 0x00532AD6 / 0x00532AE8 / 0x00532C84; 0x005D066A / 0x005D065A / 0x005CB1BE | M2-018; relative order read |
| C0 | Only Impl::HandleCliffEvent among the recovered concrete raw registrations | 0x00532B80..0x00532B8A plus the complete call inventory and wrapper expansion | M2-018; enumeration result, bounded by the scan described below |

## Item 3 — coverage: behaviors and actions

| Step | Recovered result | Native citation | Record / gap |
|---|---|---|---|
| Raw behavior overload | Receives robot ID and iterates a set of byte-sized RobotToEngine tags; binds HandleEvent and stores handles | 0x005BCA4C..0x005BCB56, direct subscribe 0x005BCAA0 | M2-018; native read |
| Actual raw overload caller | BehaviorLiftLoadTest constructor subscribes DA using Robot's ID; not OnActivated | 0x005D401A..0x005D4038; byte 0xDA at ELF 0x00C6AAE8; PLT 0x004B3E34 | M2-018; native read |
| Behavior event routing | Raw callback invokes generic virtual event handling, then selects inactive or active virtual handler from byte +0xA1 | 0x005BF2CE..0x005BF2F6 → 0x005BF2F8..0x005BF322 | M2-018; native read |
| Other similarly named overloads | EngineToGame and GameToEngine subscriptions are separate interfaces; they do not imply a raw RobotToEngine subscriber for the same numeric tag | LiftLoad constructor: GameToEngine PLT 0x004B3E10 vs raw PLT 0x004B3E34; caller expansion artifact | M2-018; overload distinction read |
| Behavior/component overlaps | FactoryTest: B8, F3, EE, D2. DockingTestSimple: D3. These append after the component when constructed after Robot component Init | 0x005D065A..0x005D068A, 0x005CB1BE; component sites in items 1–2 | M2-018; native read |
| Raw overload/component overlap | LiftLoadTest DA has no component subscriber in this inventory | 0x005D4038 / 0x00C6AAE8 compared with all component registrations | M2-018; enumeration result |
| Animation action | CA, CB, D5 have no component subscriber in this inventory | 0x00543FCA, 0x00544032, 0x00544092 | M2-018; enumeration result |
| Motor actions | C4 has no component subscriber; Turn/Head/Lift action callbacks sharing C4 run in their actual construction order | 0x00545ADC, 0x005481BC, 0x00548AAC; tail insertion 0x0051D874..0x0051D8A2 | M2-018; native read; concrete scheduling order UNKNOWN |
| Docking action | C5 and DA have no component subscriber. DA overlaps LiftLoadTest, so callback order depends on which subscription was made first | 0x00551706, 0x0055176C vs 0x005D4038; tail insertion 0x0051D874..0x0051D8A2 | M2-018; native read; concrete action/behavior instance order UNKNOWN |
| C0 behavior/action overlap | None in recovered raw registrations; behavior names or app-facing cliff notifications do not establish an additional raw C0 registration | Complete inventory and concrete wrapper expansion; sole C0 site 0x00532B8A | M2-018; bounded enumeration result |

## Item 4 — coverage: removal during emit

| Step / case | What the shipped emitter does | Native citation | Record / gap |
|---|---|---|---|
| Registration order | Appends a new node immediately before the circular-list sentinel | 0x0051D874..0x0051D8A2 | M2-018; native read |
| Start emission | Loads sentinel; returns if null; increments sentinel node refcount at +0x20 | 0x0069E27C..0x0069E284 | M2-018; native read |
| Invoke | Calls the node's callable synchronously only if its function manager at +0x18 is non-null | 0x0069E286..0x0069E290 | M2-018; native read |
| Advance | After callback returns, loads current.next, increments that next node's refcount, releases current, re-reads the signal's sentinel, and compares next with it | 0x0069E294..0x0069E2A8; final release tail branch 0x0069E2B2 | M2-018; native read |
| Scoped unsubscribe | Locks weak owner and, if still live, searches the currently linked list for the handle's node; removes it only if found | 0x0051D8F8..0x0051D964 | M2-018; native read |
| Unlink | Destroys callable immediately, clears its manager, patches next.prev and prev.next, and releases the list's node ref | 0x0051D338..0x0051D370 | M2-018; native read |
| Node lifetime | Decrements +0x20; frees only at zero. Unlink does not clear the removed node's own next/prev fields | 0x0051D338..0x0051D3AA | M2-018; native read |
| Remove a future node while current remains linked | Current's reachable successor chain is patched; removed future callback is skipped | Link writes 0x0051D354..0x0051D366 plus late next read 0x0069E294 | M2-018; instruction-derived consequence |
| Remove self only | Invocation already underway continues; current node survives via emitter ref; its retained next pointer permits advancing after callback | 0x0069E296..0x0069E29E, 0x0051D338..0x0051D3AA | M2-018; instruction-derived consequence; callable/capture destruction caveat below |
| Remove an already delivered node / remove twice | Cannot undo delivery; second removal finds no linked matching node and does nothing | List search 0x0051D934..0x0051D964 | M2-018; instruction-derived consequence |
| Append during callback | May become reachable in this same emission, because next is read after callback; not a snapshot of subscribers | 0x0051D874..0x0051D8A2; 0x0069E294 | M2-018; instruction-derived consequence |
| Self-remove then remove former immediate successor | Potential stale successor: self's next is retained, successor can be freed before emitter reads/increments it | 0x0051D338..0x0051D3AA; 0x0069E294..0x0069E29A | M2-018; conditional source-derived hazard; occurrence in production handlers UNKNOWN |
| Destroy owner/signal during callback, exceptions, cross-thread mutation | UNKNOWN for the complete surrounding lifetime/error/concurrency path; node refs and sentinel re-read alone do not establish safety | Emitter and unlink read; owner destruction/caller paths not fully traced for these cases | M2-018 / RECOVERABLE_GAP |

## Evidence and scope

Answers `requests/20261013-robot-message-subscribers.md`, engine 3.4.0-1204, `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.

This is independent native extraction. LIEF maps ELF virtual addresses to file offsets and resolves relocation-backed bound functions/callable vtables. Capstone uses Thumb decoding with `skipdata=True`. The call scan examines each aligned halfword candidate for direct BL/BLX/B.W targets rather than stopping a linear decode on data. The 35 contexts were then inspected. Ghidra's index/decompilation is navigation only; native PC-relative literals and relocations establish tags, handlers and argument origins. ARM PLT/interworking stubs must not be interpreted as Thumb bodies.

No additional direct target aliases were found by following the standard Thumb-to-ARM PC-relative veneer pattern to 0x00519F8C. No named dynamic-symbol PLT entry for this unnamed helper adds another caller. The reusable helpers and the raw IBehavior overload were expanded through their direct/veneer/PLT references. This establishes the enumerated direct call sites; it is not a proof that no computed indirect reference to an unnamed internal function exists anywhere in the binary.

The subscription path is 0x00519F8C → 0x0051CD2A → 0x0051D060. Callers pass a MessageHandler, robot ID, tag-byte address and callable; a scoped handle is returned. Concrete callers recover Robot's ID (field +0x10), the initial-connection ID (+0x0C), or the supplied ID argument. None of the concrete registrations found hard-codes a wildcard. This does not assert that the supplied ID could never equal the emitter's configured wildcard ID. Broadcast uses the robot-specific signal first and wildcard signal second at 0x0069DE50..0x0069DE74; registering earlier on the wildcard does not put it before the per-robot signal.

The behavior/component ordering statements are conditional on construction through the post-component configuration/runtime path. The Robot constructor proves the component Init precedes BehaviorManager::InitConfiguration; an exhaustive proof that every possible behavior creation path follows that ordering remains UNKNOWN.

Supporting local artifacts:

- `20261013-robot-message-subscribers-calls.json`: the 35 direct sites and indexed function boundaries.
- `20261013-robot-message-subscribers-call-focus.txt`: native tag, ID and callable construction at those sites.
- `20261013-robot-message-subscribers-expanded.json` and `-expanded-focus.txt`: reusable helper and overloaded behavior caller expansion.
- `20261013-robot-message-subscribers-native.txt`, `-lifecycle.txt`, `-order-extra.txt`, `-final-focus.txt`, `-release-focus.txt`, `-signal-core.txt`, `-robot-dtor.txt`, `-dtors.txt`: annotated native reads. Disassembly beyond a function boundary can include data; claims here use the specified ranges only.
- `20261013-robot-message-subscribers-lambdas.txt`: resolved lambda operator bodies; corrected generic member-bind operator at 0x005379F2. Its callable vtable is 0x01021200, reached through GOT 0x0103EAD4; the GOT address itself is not the vtable.

## Item 1 details — helper expansion and unsubscribe lifetimes

The 27 calls to the Impl helper 0x00533234, in native execution order, bind these Robot member handlers:

```text
call        tag  handler
00532AB2    B1   HandlePrint
00532AC4    D2   HandleFWVersionInfo
00532AD6    B8   HandlePickAndPlaceResult
00532AE8    F3   HandleActiveObjectAvailable
00532AFA    D0   HandleActiveObjectConnectionState
00532B0C    B4   HandleActiveObjectMoved
00532B1E    B5   HandleActiveObjectStopped
00532B30    D7   HandleActiveObjectUpAxisChanged
00532B42    DD   HandleFallingStarted
00532B54    DE   HandleFallingStopped
00532B66    B3   HandleGoalPose
00532B78    D4   HandleRobotStopped
00532B8A    C0   HandleCliffEvent
00532B9C    C1   HandlePotentialCliffEvent
00532BAE    F2   HandleImageChunk
00532BC0    F4   HandleImageImuData
00532BD2    BF   HandleImuData
00532BE4    C7   HandleImuRawData
00532BF6    C2   HandleSyncTimeAck
00532C08    C3   HandleRobotPoked
00532C1A    C9   HandleRobotSetHeadID
00532C2C    EE   HandleFirmwareVersion
00532C3E    D1   HandleMotorCalibration
00532C50    D8   HandleMotorAutoEnabled
00532CDA    ED   HandleRobotSetBodyID
00532CEC    C8   HandleDefaultCameraParams
00532CFE    CE   HandleObjectPowerLevel
```

The first 24 are followed by direct D3 at 0x00532C84, then the final three, then direct F0, F1, BA, BB, BC, BD, B2, B7, DC. Thus **37 Impl Init registrations**, not 37 calls to the outer helper. For each member bind, the tag immediate, relocated member pointer and zero member-pointer adjustment are visible before the call in `-expanded-focus.txt`; the callable operator at 0x005379F2 dispatches that bound member.

TracePrinter helper expansion: 0x0053BC7E/B0/HandleTrace; 0x0053BC90/CF/HandleCrashReport; 0x0053BCA2/EC/HandleWiFiFlashID. The constructor checks MessageHandler at 0x0053BC64 before these calls and uses Robot ID at 0x0053BC66.

FactoryTest helper expansion: 0x005D064A/D6/HandleFactoryTestParameter; 0x005D065A/F3/HandleActiveObjectAvailable; 0x005D066A/B8/HandlePickAndPlaceResult; 0x005D067A/EE/HandleFirmwareVersion; 0x005D068A/D2/HandleFactoryFirmwareVersion. Handler GOT entries are 0x0103F29C, 0x0103F2A0, 0x0103F2A4, 0x0103F2A8, 0x0103F2AC respectively. These are constructor registrations, not activation-time registrations.

Unsubscribe here means **release of the last shared ownership of a ScopedHandleContainer**, which resets/destroys its handle. It does not mean that each temporary shared_ptr release immediately unsubscribes. The zero-shared path is 0x004EF184 → 0x004EF1B8; container cleanup invokes the handle's reset path, 0x0051D8F8, which searches/unlinks the node. The following owner-release sites were recovered:

- **Backup:** handles vector at owner +8; destructor releases it at 0x0051A62C..0x0051A630. Constructor registration is conditional on HasExternalInterface. NVStorage embeds the manager at +0x80 and constructs it at 0x006428A4.
- **Initial:** constructor and OnNotified retain handles in the object's vector at +0 via helper 0x0052D44C. Erasing its map node destroys that vector at 0x00530322..0x00530326. **UNKNOWN:** the complete success/failure/disconnect conditions and caller timing that erase each initial-connection entry. No assertion that subscription ends as soon as C9 is delivered: HandleRobotAvailable at 0x0052DC64 only sets a flag.
- **Impl:** vector at +0; destructor 0x00532A10..0x00532A78 releases it after other members. **Trace:** embedded TracePrinter's handles reach vector +0xF8 of Impl; same destructor releases that vector before Impl's own handles. Trace registrations are made in the Impl constructor, earlier than Init.
- **Animation:** Init stores the CA/CB/D5 handles; destructor 0x00543DAC releases owned shared handles (including controls at +0x9C/+0xA4/+0xAC/+0xB4). The member replacement path in Init releases displaced ownership; a temporary callable's destruction is not removal of the installed signal callback.
- **Turn:** destructor 0x00545B94 releases its handle control at +0xE4. **Head:** destructor 0x00548478 releases +0xB4. **Lift:** native destructor body 0x0054D138..0x0054D15A releases the subscription control at 0x0054D14C.
- **Dock:** retained handles vector +0xE4; cleanup at 0x00557582..0x00557586 releases the vector. **UNKNOWN:** every earlier clear/reset path and action-result condition that could remove a docking subscription before object destruction. The nominal IDockAction destructor symbol at 0x00550414 alone does not establish that cleanup; the vtable cleanup body must be read.
- **Behavior:** raw overload pushes into IBehavior vector +0x20. IBehavior destructor 0x005BC75C releases its owned subscription vectors. **UNKNOWN:** exhaustive deactivation/early-clear paths. Registration is demonstrably in LiftLoadTest construction, and inactive-event dispatch exists; do not substitute an activation-scoped lifetime.
- **DockingTest:** constructor retains the D3 handle at +0x12C/+0x130; destructor 0x005CEC56..0x005CEC5C releases its shared control. **FactoryTest:** helper retains handles in vector +0x11C; destructor 0x005D36C4..0x005D36C8 destroys that vector. **UNKNOWN:** exhaustive earlier owner-specific reset paths for these test behaviors.
- **Tap:** retained vector +4 (end at +8); Robot destructor's component cleanup releases it at 0x0051120C..0x00511210. **Accel:** handles list +0x14; destructor clears it at 0x0063542A..0x0063542E. **NV:** handles vector +0x128; destructor's release loop 0x00643ED2..0x00643EF6 releases the controls. **Path:** destructor 0x00648FFC starts Abort then shared-handle release at 0x00649006..0x0064900A. **UNKNOWN:** exhaustive non-destructor clear/reset routes in these components.
- **Firmware:** InitUpdate first clears prior handle ownership at 0x00678324..0x0067834C; it then subscribes EF per map entry and appends to vector +0x30 at 0x00678390..0x006783B2. Destructor 0x006763DC releases the vector. Lambda 0x00678C0A reads otaAck then tails through interworking veneer 0x008CD63C to PLT 0x004BC36C, HandleFlashWriteAck(robot-map key, ack). **UNKNOWN:** exhaustive update-completion/error states that may clear handles earlier.

These UNKNOWNs concern complete early-removal conditions, not the existence of the recovered owner/destructor release paths. They remain recoverable from shipped caller code and are not BLOCKED_EXTERNAL or hardware-only findings.

## Items 2–3 details — consequences of the sequence

The unconditional component registration order in the ordinary non-null-context/handler construction path is:

```text
Path C6
Backup C9 [external-interface gate], NV CD [context gate]
CubeAccel F5
BlockTap B6, B4, B5
Trace B0, CF, EC [handler gate]
Impl Init: B1 D2 B8 F3 D0 B4 B5 D7 DD DE B3 D4 C0 C1 F2 F4 BF C7
           C2 C3 C9 EE D1 D8 D3 ED C8 CE F0 F1 BA BB BC BD B2 B7 DC
Behavior configuration / any constructors it creates
InitialConnection D2, EE, C9 [handler gate]
InitialConnection ED [later OnNotified(result=0)]
```

Only list members that are actually instantiated, enabled by their constructor gate, and still subscribed participate. The complete BehaviorFactory/ActivityFactory creation paths and creation/configuration ordering among different optional behavior instances were not traced to shipped JSON, so they are **UNKNOWN**; constructor address order is not execution order. Likewise action order must come from actual action construction/Init calls, not a fixed Turn → Head → Lift type ordering.

The raw IBehavior overload uses the set's ordered traversal, with each byte tag loaded from a tree node at +0x0D before subscribing at 0x005BCAA0. For the concrete LiftLoadTest caller the set has one tag, DA. Its neighboring GameToEngine subscription uses a different overload; many other behavior constructors similarly call external-interface overloads. Treating all `SubscribeToTags` references as raw robot subscriptions would falsely add cliff/other listeners.

For C0 specifically, the exhaustive recovered concrete direct/wrapper call inventory adds no second raw subscriber after HandleCliffEvent. This resolves the particular uncertainty in `20261013-play-area-check.md` section D for this enumerated path. It does not establish the downstream ordering of app-facing events, behavior state transitions, or any hypothetical computed indirect subscription.

## Item 4 details — precise mutation boundary

The emitter retains **one current node**, not a copy of the subscriber list and not advance ownership of every successor. It begins with the empty sentinel, then traverses its next link. Unsubscription destroys the removed callable immediately even when the emitter's node ref prevents that node's memory from being freed. The callback already entered is not canceled, but arbitrary use of destroyed callable captures after self-removal is not guaranteed safe by the node refcount.

For an ordinary callback A that stays linked and removes pending B, unlink patches the live neighbors, so traversal skips B. If A removes itself only, its own next link remains available while the emitter ref keeps A alive. After return the emitter retains that next node before releasing A. A handler removed after it has already run is simply absent from later emissions.

A newly appended node can run in the same emission when the current live traversal reaches it; the code does not freeze the initial tail. This is conditional: if a formerly-last A self-removes, its retained next still points to the sentinel. Appending a new node afterward does not update that detached A.next, so this traversal stops at the sentinel instead of reaching the new node.

There is a narrower unsafe combined mutation, derived directly from the link/refcount writes: A self-removes, leaving detached A.next=B; A then removes B. B's live predecessor is now another node/sentinel, so unlink does not update detached A.next. Unless some separate reference protects B, it can be freed before A returns. The emitter then reads stale A.next and accesses B's +0x20 refcount. **UNKNOWN:** whether any recovered production handler performs that exact mutation sequence. The native mechanism must not be described as a universally safe removal snapshot.

## Manifest disposition and remaining scope

Current **M2-018** is **RECOVERABLE_GAP**, titled “Robot-message subscriber dispatch: per-robot signal before wildcard, registration order, synchronous; which subscribers take each tag (0xC0 first)”. Its evidence points to checked `20261013-play-area-check.md` section D and the ProcessMessages/Broadcast/connect/emit/HandleCliffEvent ranges. This report adds the missing direct caller inventory, concrete wrapper expansion, component registration order and mutation semantics. It does not contradict the existing per-robot-before-wildcard/FIFO/synchronous claim, nor justify promoting the whole record while its requested stack comparison and lifetime unknowns remain unverified.

Remaining recoverable questions: complete optional-behavior creation paths, order and configuration reachability; initial-connection erase timing and exhaustive early handle clears; concrete action instance scheduling for shared C4/DA; occurrence of combined unsafe removal; complete signal-owner destruction/exception/concurrency behavior; any computed indirect subscriptions beyond the direct-reference scan. No manifest, inventory, production code, project state, branch or commit was changed. No shipped files were uploaded, no engine code or hardware test was run, and no network endpoint was contacted. Regression test and commit: not applicable to this research-only request.
