# M1 final extraction rows (2026-10-09)

Read-only extractor report for the manager. These rows are **unchecked** and are not implementation authorization. Primary authority is the shipped `resources/lib/armeabi-v7a/libcozmoEngine.so`. Companion [native instructions](20261009-M1-final-extraction-native.txt) contains fresh disassembly, binary SHA256, relocation identities and vtable words. Addresses are ELF virtual addresses; function-symbol low bit 1 means Thumb.

## Current manifest, read before extraction

The following is the current manifest text at extraction, not an earlier title/status recalled from notes.

```json
{
  "id": "M1-046",
  "subsystem": "M1-transport",
  "title": "Channel/member subscription retirement recipients",
  "location": "cozmo-stack/src/Cozmo.Robot/RobotLifetime.cs",
  "status": "RECOVERABLE_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "Robot constructs RobotIdleTimeoutComponent at +0x51C and RobotToEngineImplMessaging at +0x518 (0x0051020A..0x0051022E). Robot dtor clears +0x51C, releases its shared-handle vector, then clears +0x518 and calls RobotToEngineImplMessaging::~ (0x005112E6..0x00511308).",
    "RobotToEngineImplMessaging dtor releases members +0x144/+0x138/+0x110/+0x104, handle vector +0xF8, filebuf/ios, then its base (0x00532A10..0x00532A74). Last shared-handle release invokes virtual slot +8 then deleting-dtor slot +4 (0x004EF184..0x004EF1CC); concrete target and behavior-changing descendants UNKNOWN. (MISSING triage Q1-Q3.)"
  ],
  "effect": "A removed Robot or queued sleep action retains state, emits different messages or invokes callbacks in a different order.",
  "provenance": "Cited cross-layer inventory correction authorized by B-M1M2 Rows checked (manager, 2026-10-05). Interfaces/known effects only; no higher-layer build or settlement.",
  "unresolved": "RECOVERABLE_GAP: build the confirmed owner/member retirement order for RobotIdleTimeoutComponent (+0x51C) and RobotToEngineImplMessaging (+0x518), including its member/shared-handle vector order. The concrete ScopedHandleContainer virtual +8 unsubscribe target and any behavior-changing descendants remain UNKNOWN; do not invent their effects. Source: 20261006-M1M2-missing-triage.md Q1-Q3.",
  "hardware_required": false,
  "live_path": true,
  "test": ""
}
```

```json
{
  "id": "M1-047",
  "subsystem": "M1-transport",
  "title": "Transport executor topology and shipped priority request",
  "location": "cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs",
  "status": "IMPLEMENTATION_GAP",
  "effect": "Wrong executor thread count/order or omitted priority changes message timing.",
  "provenance": "C# uses host executor threads and currently does not request native SCHED_RR priority.",
  "authority": "libcozmoEngine.so 3.4.0-1204 executor constructors/priority helper.",
  "evidence": [
    "H1 creates two threads and calls SetThreadPriority on both only for priority !=2 (0x007FBDBA,0x007FBDEC,0x007FBE02..0x007FBE14).",
    "H2 priority3 selects policy2/SCHED_RR and requests min+trunc_f32((max-min)*0.75), bits0x3F400000; EPERM/result1 skips error path (0x008334CC..0x00833532).",
    "H3 FIFO dispatch stays M1-024; T3 M1-021; T4 M1-024."
  ],
  "unresolved": "Compare/build exact thread count/work split and priority gates. Actual host limits/permissions/effect remain M1-014 host policy; dispatch/timing stay M1-010/-021/-024.",
  "hardware_required": false,
  "live_path": true,
  "test": "TransportRepairTests executor topology/priority source-derived cases to add."
}
```

## M1-046: retirement targets

| Row | Extracted behavior | Primary evidence / reopened target |
|---|---|---|
| U1 | Shared-handle vector releases entries in **reverse insertion order**. Each non-null control block receives `__release_shared`; the vector storage is freed after the loop. | `0x004EAE94..0x004EAEC6`: end decreases by 8 before release. Robot owner order and Messaging member order remain the checked Q1–Q3 rows. |
| U2 | On the last shared owner, the ScopedHandleContainer contains a HandleBase pointer. It calls that object's virtual **+8** unsubscribe method, then virtual **+4** deleting destructor if the pointer is still non-null, then frees the container. | `0x004EF184..0x004EF196`, reopened `0x004EF1B8..0x004EF1CE`. |
| U3 | Messaging's handle is concretely the `ProtoHandle` for `AnkiEvent<RobotInterface::RobotToEngine>`. Vtable `0x0101FE18`, address point `0x0101FE20`; +8 is relocation `0x0101FE28`, ARM_RELATIVE addend `0x0051D8F9`, hence Thumb target **0x0051D8F8**. Deleting destructor +4 is `0x0101FE24` -> **0x0051D8D4**. | Registration helper `0x00533234..0x005332A0` calls `0x00519F8C`; reopened subscription chain `0x00519FBC -> 0x0051CD2A -> 0x0051D060`. Creation `0x0051D09C..0x0051D0B0` loads GOT `0x0103E990` whose named symbol is this exact vtable, then adds 8. |
| U4 | Messaging unsubscribe does nothing if its weak control block is absent, cannot lock, its saved signal data is null, its list head is null, or its saved node is not found. Otherwise it walks the circular list by next links until the saved node and tail-calls **0x0051D338**. | Reopened target `0x0051D8F8..0x0051D932`. The weak-lock temporary is released before the saved data/list checks (`0x0051D908`). |
| U5 | Removing a found Messaging subscription first destroys its stored callback object (inline function virtual +0x10, heap function +0x14), zeros callback pointer +0x18, reconnects previous.next and next.previous when present, then decrements node reference count +0x20. It frees the node only if that count reaches zero. No callback invocation or robot/game message emission occurs on this path. | Reopened `0x0051D338..0x0051D3A4`. The bound Messaging callback vtable is `0x01021200` (GOT `0x0103EAD4` at `0x00533258..0x00533268`). Its destroy slots resolve by named relocations `0x01021218 -> 0x005379ED`, `0x0102121C -> 0x005379EF`; reopened **0x005379EC** is `bx lr`, **0x005379EE** tails to delete veneer. |
| U6 | Idle's external-interface registration is conditional on `HasExternalInterface()==1`, subscribes game-to-engine tag **0x54**, then retains the returned handle. For the shipped `UiMessageHandler` external-interface implementation, this is the `MessageGameToEngine` ProtoHandle, vtable **0x0102FFF8**, address point `0x01030000`; +8 relocation `0x01030008` ARM_RELATIVE addend `0x00664EB9` -> **0x00664EB8**. Deleting destructor +4: `0x01030004` -> **0x00664E94**. | `0x0052CC5A..0x0052CC9C`, helper `0x0052CCB0..0x0052CD06`; virtual external slot +0x2C reopened from UiMessageHandler vtable `0x0102FE14`: relocation `0x0102FE48` names `UiMessageHandler::Subscribe(MessageGameToEngineTag,...)` at **0x00662750**. It calls `0x006648CC`. Handle creation `0x00664908..0x0066491C` loads GOT `0x0103F93C`, named exact vtable above, and adds 8. |
| U7 | Idle's game-to-engine unsubscribe has the same weak-lock/saved-data/list-membership gates, finds its saved node and tails to **0x0066365C**. It destroys the stored callback, clears pointer, unlinks the node and decrements reference count, freeing only at zero. It sends no message and invokes no callback. | Reopened `0x00664EB8..0x00664EF2`, `0x0066365C..0x006636C8`. Idle helper constructs inline callback at vtable **0x01020D00**, address point +8 (`0x0052CCCA..0x0052CCD8`). Destroy slots +0x10/+0x14 are REL addends `0x0052D0AD`/`0x0052D0AF`; reopened **0x0052D0AC** `bx lr`, **0x0052D0AE** tail delete. |
| U8 | Both ProtoHandle deleting destructors release their weak control block (if non-null), then delete the handle itself. Their ordinary destructors only release that weak control block. | Messaging `0x0051D8B4..0x0051D8CC`, `0x0051D8D4..0x0051D8F0`; game-to-engine `0x00664E74..0x00664E8C`, `0x00664E94..0x00664EB0`. |

### Scope and unresolved boundary

The concrete handle families and unsubscribe effects are established above. U6 explicitly names the shipped UiMessageHandler implementation; `SimpleExternalInterface` itself declares that subscribe slot pure virtual. These rows do not assert that an arbitrary external-interface subclass has the same target. Under the operator's app-boundary decision, the C# host API must provide its subscription ownership endpoint while reproducing the engine's subscribe/retire timing exactly. No production edits were made; manager must check these rows before a builder uses them. Broader Messaging/Idle behavior outside retirement is not extracted here.

## M1-047: constructor priority selected on the transport production path

| Row | Extracted behavior | Primary evidence / reopened target |
|---|---|---|
| P1 | `ReliableTransport` selects priority integer **3**, not default integer 2. **0x008367C0: movs r1,#3**; **0x008367C2: blx 0x004AF454** calls `Dispatch::Create(name,ThreadPriority)`. It retains the returned queue at +0x1C. | Constructor symbol `ReliableTransportC1(...)` starts **0x00836790**; disassembly `0x00836790..0x008367EE`. PLT relocation names `Dispatch6CreateEPKc...ThreadPriority`, target **0x007FB878**. |
| P2 | `Dispatch::Create` preserves incoming priority (`0x007FB87E mov r5,r1`), passes it as TaskExecutor ctor argument r2 (`0x007FB898 mov r2,r5`), and calls at **0x007FB89A** via PLT **0x004CAFD0**. | Reopened `0x007FB878..0x007FB8A0`; GOT `0x0104D27C` names `TaskExecutorC1EPKc...ThreadPriority`, symbol **0x007FBC4D**, actual Thumb start **0x007FBC4C**. |
| P3 | The constructor saves priority r2 in fp, creates its two threads, then compares fp against 2. For transport's actual 3, it calls SetThreadPriority for **both**, in first-thread then second-thread order. | Reopened ctor `0x007FBC4C..0x007FBE14`; thread calls **0x007FBDBA** and **0x007FBDEC**; compare **0x007FBE02**, setters **0x007FBE0C** and **0x007FBE14**, PLT **0x004CB030** / GOT **0x0104D29C**. |
| P4 | Thread overload reads native thread handle and transfers priority to the long-handle overload. For integer3 it selects policy2 (SCHED_RR), obtains min/max, selects f32 0.75 (bits **0x3F400000**), computes `min + trunc_f32((max-min)*0.75)` and calls pthread_setschedparam. Result0 succeeds; result1 (EPERM) skips the error path. | Reopened thread overload **0x00833688..0x0083368C** and long-handle overload **0x008334C8..0x00833532**. TBB priority3 goes to **0x00833500**. |

Host permission and effective scheduling remain the current M1-014 boundary. No inference from host thread defaults substitutes for the shipped request. No changes to M1-047 status, evidence or implementation were made.

## Review handoff

Manager row check requested for U1–U8 and P1–P4. This report supplies evidence, not a settlement verdict. No emulator, tests, hardware run, or production implementation was performed by this extractor.
