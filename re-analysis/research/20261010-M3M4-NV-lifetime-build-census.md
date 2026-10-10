# NV lifetime and idle build — call accounting

| Record | Built slice | Open boundary |
|---|---|---|
| M3-040 | I1–I9: synchronous registration, one entry predicate, log/invoke/front-pop/drain order | Frozen manifest still RECOVERABLE_GAP; manager inventory update required. Captured callback bodies retain their owning records. |
| M3-038 | N1–N8: unsubscribe on Dispose, drop idle/queued/active functions without invocation | Saved command vector follows adopted N rows in the next batch; backup destruction is M15. Frozen manifest still RECOVERABLE_GAP. |
| M3-030 | R2/R3: active request remains present through callback and broadcast, then idle | Persistent command data/normalized-name closure follows N/R batch. |
| M3-031 | N11: timeout callback executes while the request remains active, then idle | Write dispatch/persistent data and M15 completion remain in subsequent batch. |

Authority: manager adoption in jobs/B-M3M4.md, 2026-10-10, of research/20261009-M3M4-remaining-rows.md; N11/R2/R3 adopted 2026-10-09 from research/20261006-M3M4-rows-extraction.md. No record settled. No unchecked extraction was used to change behavior.

Production path: CozmoRobot's NVStorageComponent receives NVOpResult through its Message subscription. EngineRobot.Update calls Update; terminal callback/broadcast execute before _inFlight is cleared. OnIdle appends and immediately calls ProcessOnIdle. RemoveRobot→ResetDevices→OnDisconnected discards functions; Dispose removes the live Message subscription before discarding functions.

## Calls and counterparts

The I/N windows and every call target were reopened from the shipped engine using the native reader again on 2026-10-10. Their complete instruction text/call census is Appendix B of the adopted remaining-rows report (I1–I9, N1–N8). R4–R6 normalization/static targets were also reopened for the next N/R batch. Supplemental reopening is attached below. No body was changed from an unapproved target inspection.

| Native calls | C# counterpart / boundary |
|---|---|
| 528A6E AddOneShot; 645C28 deque append-copy; 645C32 tail ProcessOnIdle | Existing OnIdle registration, retained delegate in _onIdle.Add, synchronous ProcessOnIdle. Allocation/copy is managed-runtime plumbing. |
| 645B56 sChanneledDebugF | Exact debug event NVStorageComponent.ProcessOnIdleCallbacks.ProcessingCallback, empty format, before every callback. No error store/debug-break in this range. |
| 645B9A std::function::operator()→virtual+18 | Invoke the front delegate. Exceptions propagate; do not clear or pop before invocation. Callback bodies remain their M1/M3/M15/action owners. |
| 645BA0 deque pop→destroy slots10/14 | _onIdle.RemoveAt(0) after invocation, no snapshot batch, no NV predicate recheck. Reference release is distinct from callback invocation. |
| 6456CC ProcessRequest; conditional tail ProcessOnIdle | Existing Update state-0 send/admission, then ProcessOnIdle. In-flight/queued predicates block dispatch of idle functions. |
| NV D0→D1→delete; shared handle release/unsubscribe; idle/request deque destroy | Dispose unsubscribes Message, then OnDisconnected releases idle delegates, queued requests, active request. No synthetic failure/timeout callbacks. Native inline/heap callback destruction is managed reference release; arbitrary captured native object destructors belong to their producer layer. |
| 643F40 saved command allocation free | MISSING pending N1–N13 saved-command build; no saved persistent vector exists in the prior implementation. |
| 643F48 RobotDataBackupManager destructor | MISSING M15 recipient. Boundary only; no invented backup deletion callback. |
| 6437EA SetState(0); timeout 6457BC SetState(0) | Clear active request only after callback/broadcast returns; never run idle callbacks from completion. Exceptions before this point leave the request active. |
| String/vector/node allocate/deallocate; function copy/destroy; unwind | Managed references and collection storage; native target windows retained for review. No additional wire messages, stream/file opens/closes, or static counters in the claimed idle/NV destructor slices. |

Same-thread idle dispatch remains reentrant. A nonblocking Monitor gate prevents competing host threads from invoking the executing front twice; they append without waiting for the callback. Native has one engine executor. As in native, callbacks are invoked while still at the front; an empty callback/reentrant destruction is not translated into a fabricated NV result. Calls run outside _gate; collection accesses are synchronized. The existing terminal identity check prevents a retained host owner's cleanup from clearing a replacement request. A registration racing the dispatch exit may wait until the next Update; that host interleaving has no native single-executor counterpart and is not claimed as a recovered engine branch.

## Regressions

M3_040_IdleDrainKeepsItsEntryPredicateAcrossCallbacks drives the live NV reply entry and checks terminal-before-idle, in-flight during terminal callback, and all three idle callbacks despite the first enqueueing work. Expected order comes from 00645B08..00645BAE and 006437EA.

M3_040_ThrowingIdleCallbackRemainsAtTheFront checks invoke-before-pop by invoking the same throwing front twice. M3_038_DisposeDropsPendingFunctionsAndUnsubscribesTheLiveReplyEntry checks no callbacks after owner disposal. No time waits or implementation-derived expected values.

M3_040_CompetingHostRegistrationDoesNotInvokeTheExecutingFrontTwice uses explicit TaskCompletionSource entry/release signals to check that a second host registration returns without invoking the executing front again, then runs once in the active drain. No sleeps or elapsed-time assertion.

Final full-suite result on the local candidate: 4,003 passed, zero failed/skipped (3m06s), 2026-10-10. The candidate is not committed: CODEX-BUILDER rule 5 requires IMPLEMENTATION_GAP, but the approved M3 snapshot still freezes M3-038/M3-040 as RECOVERABLE_GAP. Manager inventory/manifest refresh is required before publication. No snapshot was rewritten by the worker. The candidate diff is in 20261010-M3M4-NV-lifetime-candidate.patch for review; it includes the unresolved-only metadata draft and generated report, whose status conversion is pending.

<details>
<summary>Reopened native targets</summary>

```text

RANGE 004D7F40..004D7FC0
004D7F40: push       {r4, r5, r6, lr}
004D7F42: ldr        r0, [pc, #0xa0] ; literal[004D7FE4]=00B67924
004D7F44: movs       r5, #0
004D7F46: ldr        r6, [pc, #0xa4] ; literal[004D7FEC]=007A90C6
004D7F48: add        r0, pc
004D7F4A: add        r6, pc
004D7F4C: ldr        r0, [r0]
004D7F4E: mov        r1, r0
004D7F50: str        r5, [r0, #8]
004D7F52: str        r5, [r1, #4]!
004D7F56: str        r1, [r0]
004D7F58: ldr        r0, [pc, #0x8c] ; literal[004D7FE8]=00B67912
004D7F5A: add        r0, pc
004D7F5C: ldr        r4, [r0]
004D7F5E: adds       r2, r6, r5
004D7F60: adds       r1, r4, #4
004D7F62: mov        r0, r4
004D7F64: mov        r3, r2
004D7F66: blx        #0x4b9c18 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo9NVStorage10NVEntryTagEjEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_jEEEEENS_15__tree_iteratorIS6_PNS_11__tree_nodeIS6_PvEEiEENS_21__tree_const_iteratorIS6_SO_iEERKT_DpOT0_ -> 00646D96 size=38
004D7F6A: adds       r5, #8
004D7F6C: cmp        r5, #0x50
004D7F6E: bne        #0x4d7f5e
004D7F70: ldr        r1, [pc, #0x80] ; literal[004D7FF4]=00B678F8
004D7F72: ldr        r0, [pc, #0x84] ; literal[004D7FF8]=0016A8BD
004D7F74: add        r1, pc
004D7F76: ldr        r2, [pc, #0x84] ; literal[004D7FFC]=00B79080
004D7F78: add        r0, pc
004D7F7A: ldr        r1, [r1]
004D7F7C: add        r2, pc
004D7F7E: blx        #0x4a4024 ; __cxa_atexit IMPORT (resolve packaged dependencies before calling external)
004D7F82: ldr        r0, [pc, #0x7c] ; literal[004D8000]=00B678E8
004D7F84: movs       r5, #0
004D7F86: ldr        r6, [pc, #0x80] ; literal[004D8008]=007A90D6
004D7F88: add        r0, pc
004D7F8A: add        r6, pc
004D7F8C: ldr        r0, [r0]
004D7F8E: mov        r1, r0
004D7F90: str        r5, [r0, #8]
004D7F92: str        r5, [r1, #4]!
004D7F96: str        r1, [r0]
004D7F98: ldr        r0, [pc, #0x68] ; literal[004D8004]=00B678D6
004D7F9A: add        r0, pc
004D7F9C: ldr        r4, [r0]
004D7F9E: adds       r2, r6, r5
004D7FA0: adds       r1, r4, #4
004D7FA2: mov        r0, r4
004D7FA4: mov        r3, r2
004D7FA6: blx        #0x4b9c18 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo9NVStorage10NVEntryTagEjEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE30__emplace_hint_unique_key_argsIS5_JRKNS_4pairIKS5_jEEEEENS_15__tree_iteratorIS6_PNS_11__tree_nodeIS6_PvEEiEENS_21__tree_const_iteratorIS6_SO_iEERKT_DpOT0_ -> 00646D96 size=38
004D7FAA: adds       r5, #8
004D7FAC: cmp        r5, #0xb8
004D7FAE: bne        #0x4d7f9e
004D7FB0: ldr        r1, [pc, #0x5c] ; literal[004D8010]=00B678BC
004D7FB2: ldr        r0, [pc, #0x60] ; literal[004D8014]=0016A87D
004D7FB4: add        r1, pc
004D7FB6: ldr        r2, [pc, #0x60] ; literal[004D8018]=00B79042
004D7FB8: add        r0, pc
004D7FBA: add        r2, pc
004D7FBC: ldr        r1, [r1]

RANGE 00643C76..00643CF6
00643C76: mov.w      sb, #0xde000
00643C7A: add        r8, pc
00643C7C: mov        r2, r7
00643C7E: ldr        r6, [r0]
00643C80: add        r0, sp, #0x18
00643C82: mov        r3, r8
00643C84: str.w      sb, [sp, #0x14]
00643C88: str        r7, [sp, #0x28]
00643C8A: add        r4, sp, #0x28
00643C8C: mov        r1, r6
00643C8E: strd       r4, r5, [sp]
00643C92: blx        #0x4b9930 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo9NVStorage10NVEntryTagEjEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE25__emplace_unique_key_argsIS5_JRKNS_21piecewise_construct_tENS_5tupleIJOS5_EEENSI_IJEEEEEENS_4pairINS_15__tree_iteratorIS6_PNS_11__tree_nodeIS6_PvEEiEEbEERKT_DpOT0_ -> 00647134 size=7C
00643C96: ldr        r0, [sp, #0x18]
00643C98: movs       r1, #0x30
00643C9A: mov        r2, r7
00643C9C: mov        r3, r8
00643C9E: str        r1, [r0, #0x14]
00643CA0: add        r0, sp, #0x18
00643CA2: mov        r1, r6
00643CA4: str.w      sb, [sp, #0x14]
00643CA8: str        r7, [sp, #0x28]
00643CAA: strd       r4, r5, [sp]
00643CAE: blx        #0x4b9930 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo9NVStorage10NVEntryTagEjEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE25__emplace_unique_key_argsIS5_JRKNS_21piecewise_construct_tENS_5tupleIJOS5_EEENSI_IJEEEEEENS_4pairINS_15__tree_iteratorIS6_PNS_11__tree_nodeIS6_PvEEiEEbEERKT_DpOT0_ -> 00647134 size=7C
00643CB2: ldr        r0, [sp, #0x18]
00643CB4: mov        r1, r6
00643CB6: mov        r2, r5
00643CB8: mov        r3, r8
00643CBA: ldr        r7, [r0, #0x14]
00643CBC: movw       r0, #0xe030
00643CC0: movt       r0, #0xd
00643CC4: str        r5, [sp, #0x28]
00643CC6: str        r0, [sp, #0x10]
00643CC8: add        r0, sp, #0x24
00643CCA: strd       r4, r0, [sp]
00643CCE: add        r0, sp, #0x18
00643CD0: blx        #0x4b9930 ; _ZNSt6__ndk16__treeINS_12__value_typeIN4Anki5Cozmo9NVStorage10NVEntryTagEjEENS_19__map_value_compareIS5_S6_NS_4lessIS5_EELb1EEENS_9allocatorIS6_EEE25__emplace_unique_key_argsIS5_JRKNS_21piecewise_construct_tENS_5tupleIJOS5_EEENSI_IJEEEEEENS_4pairINS_15__tree_iteratorIS6_PNS_11__tree_nodeIS6_PvEEiEEbEERKT_DpOT0_ -> 00647134 size=7C
00643CD4: ldr        r0, [pc, #0x190] ; literal[00643E68]=009FBB94
00643CD6: rsb.w      r2, r7, #0x1e000
00643CDA: ldr        r1, [sp, #0x18]
00643CDC: add        r0, pc
00643CDE: ldr        r0, [r0]
00643CE0: str        r2, [r1, #0x14]
00643CE2: ldr        r4, [r0], #4
00643CE6: cmp        r4, r0
00643CE8: beq        #0x643da4
00643CEA: ldr        r0, [pc, #0x18c] ; literal[00643E78]=009FBB7A
00643CEC: movw       sl, #0
00643CF0: ldr.w      r8, [pc, #0x178] ; literal[00643E6C]=005B7749
00643CF4: add        r7, sp, #0x18

RANGE 006441F8..006443F4 _ZNK4Anki5Cozmo18NVStorageComponent15GetBaseEntryTagEj
006441F8: push       {r4, r5, r7, lr}
006441FA: sub        sp, #0x10
006441FC: mov        r5, r1
006441FE: cmp.w      r5, #-1
00644202: ble        #0x644280
00644204: lsrs       r0, r5, #0xf
00644206: cmp        r0, #0x32
00644208: bhi        #0x644268
0064420A: ldr        r0, [pc, #0x160] ; literal[0064436C]=009FB660
0064420C: add        r0, pc
0064420E: ldr        r0, [r0]
00644210: ldr.w      ip, [r0]
00644214: ldr        r0, [pc, #0x158] ; literal[00644370]=009FB656
00644216: add        r0, pc
00644218: ldr        r0, [r0]
0064421A: adds       r0, #4
0064421C: cmp        ip, r0
0064421E: beq        #0x644268
00644220: ldr        r0, [pc, #0x150] ; literal[00644374]=009FB64A
00644222: add        r0, pc
00644224: ldr        r0, [r0]
00644226: adds       r1, r0, #4
00644228: ldr        r2, [r1]
0064422A: cmp        r2, #0
0064422C: mov        r0, r2
0064422E: beq        #0x64423a
00644230: mov        r4, r0
00644232: ldr        r0, [r4, #4]
00644234: cmp        r0, #0
00644236: bne        #0x644230
00644238: b          #0x644246
0064423A: mov        r0, r1
0064423C: ldr        r4, [r0, #8]
0064423E: ldr        r3, [r4]
00644240: cmp        r3, r0
00644242: mov        r0, r4
00644244: beq        #0x64423c
00644246: ldr        r0, [r4, #0x10]
00644248: cmp        r0, r5
0064424A: bls        #0x644322
0064424C: cbz        r2, #0x644258
0064424E: mov        r1, r2
00644250: ldr        r2, [r1, #4]
00644252: cmp        r2, #0
00644254: bne        #0x64424e
00644256: b          #0x644264
00644258: mov        r0, r1
0064425A: ldr        r1, [r0, #8]
0064425C: ldr        r2, [r1]
0064425E: cmp        r2, r0
00644260: mov        r0, r1
00644262: beq        #0x64425a
00644264: cmp        ip, r1
00644266: bne        #0x644228
00644268: ldr        r2, [pc, #0x10c] ; literal[00644378]=005B71D0
0064426A: movs       r0, #0
0064426C: strd       r0, r0, [sp, #4]
00644270: add        r2, pc
00644272: str        r0, [sp, #0xc]
00644274: adr        r0, #0x104 ; ADR[0064437C]=b'NVStorageComponent.GetBaseEntryTag.TagIsTooSmall'
00644276: add        r1, sp, #4
00644278: mov        r3, r5
0064427A: blx        #0x4a4540 ; _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D2B4 size=80
0064427E: b          #0x6442f6
00644280: ldr        r0, [pc, #0x12c] ; literal[006443B0]=009FB5EE
00644282: add        r0, pc
00644284: ldr        r0, [r0]
00644286: ldr        r2, [r0], #4
0064428A: cmp        r2, r0
0064428C: beq        #0x6442e0
0064428E: movs       r1, #0
00644290: movw       r0, #0xffff
00644294: movt       r1, #0x7fff
00644298: and.w      ip, r5, r1
0064429C: ldr        r1, [pc, #0x114] ; literal[006443B4]=009FB5CE
0064429E: bic.w      r0, r5, r0
006442A2: add        r1, pc
006442A4: ldr        r1, [r1]
006442A6: add.w      lr, r1, #4
006442AA: mov        r1, r2
006442AC: ldr        r2, [r2, #0x10]
006442AE: cmp        r2, r5
006442B0: beq        #0x64431c
006442B2: cmp.w      r0, #-0x40000000
006442B6: it         ne
006442B8: cmpne.w    ip, #0
006442BC: beq        #0x6442c2
006442BE: cmp        r0, r2
006442C0: beq        #0x64433c
006442C2: ldr        r4, [r1, #4]
006442C4: cbz        r4, #0x6442d0
006442C6: mov        r2, r4
006442C8: ldr        r4, [r2]
006442CA: cmp        r4, #0
006442CC: bne        #0x6442c6
006442CE: b          #0x6442da
006442D0: ldr        r2, [r1, #8]
006442D2: ldr        r4, [r2]
006442D4: cmp        r4, r1
006442D6: mov        r1, r2
006442D8: bne        #0x6442d0
006442DA: cmp        r2, lr
006442DC: mov        r1, r2
006442DE: bne        #0x6442ac
006442E0: ldr        r2, [pc, #0xd4] ; literal[006443B8]=005B7158
006442E2: movs       r0, #0
006442E4: strd       r0, r0, [sp, #4]
006442E8: add        r2, pc
006442EA: str        r0, [sp, #0xc]
006442EC: adr        r0, #0xcc ; ADR[006443BC]=b'NVStorageComponent.GetBaseEntryTag.FactoryTagNotFound'
006442EE: add        r1, sp, #4
006442F0: mov        r3, r5
006442F2: blx        #0x4a4540 ; _ZN4Anki4Util9sWarningFEPKcRKNSt6__ndk16vectorINS3_4pairIS2_S2_EENS3_9allocatorIS6_EEEES2_z -> 0080D2B4 size=80
006442F6: ldr        r0, [sp, #4]
006442F8: cbz        r0, #0x644318
006442FA: ldr        r1, [sp, #8]
006442FC: cmp        r1, r0
006442FE: itttt      ne
00644300: subne.w    r2, r1, #8
00644304: subne      r2, r2, r0
00644306: mvnne      r3, #7
0064430A: bicne.w    r2, r3, r2
0064430E: itt        ne
00644310: addne      r1, r1, r2
00644312: strne      r1, [sp, #8]
00644314: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00644318: mov.w      r5, #0x198000
0064431C: mov        r0, r5
0064431E: add        sp, #0x10
00644320: pop        {r4, r5, r7, pc}
00644322: cbz        r2, #0x64432e
00644324: mov        r0, r2
00644326: ldr        r2, [r0, #4]
00644328: cmp        r2, #0
0064432A: bne        #0x644324
0064432C: b          #0x644338
0064432E: ldr        r0, [r1, #8]
00644330: ldr        r2, [r0]
00644332: cmp        r2, r1
00644334: mov        r1, r0
00644336: beq        #0x64432e
00644338: ldr        r5, [r0, #0x10]
0064433A: b          #0x64431c
0064433C: mov        r5, r0
0064433E: b          #0x64431c
00644340: b          #0x644342
00644342: mov        r4, r0
00644344: ldr        r0, [sp, #4]
00644346: cbz        r0, #0x644364
00644348: ldr        r1, [sp, #8]
0064434A: cmp        r1, r0
0064434C: beq        #0x644360
0064434E: sub.w      r2, r1, #8
00644352: mvn        r3, #7
00644356: subs       r2, r2, r0
00644358: bic.w      r2, r3, r2
0064435C: add        r1, r2
0064435E: str        r1, [sp, #8]
00644360: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00644364: mov        r0, r4
00644366: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
0064436A: nop
0064436C: cpsie      none
0064436E: lsls       r7, r3, #2

RANGE 007CEE38..007CF144 _ZN4Anki5Cozmo9NVStorage12EnumToStringENS1_10NVEntryTagE
007CEE38: movw       r1, #0xffff
007CEE3C: movt       r1, #0x8010
007CEE40: cmp        r0, r1
007CEE42: bgt        #0x7cee8a
007CEE44: movw       r1, #0xffff
007CEE48: movt       r1, #0x8000
007CEE4C: cmp        r0, r1
007CEE4E: bgt        #0x7ceef8
007CEE50: add.w      r0, r0, #-0x80000000
007CEE54: cmp        r0, #0x12
007CEE56: bhi.w      #0x7cf0a6
007CEE5A: tbh        [pc, r0, lsl #1] ; literal[007CEE5C]=0013F010
007CEE5E: movs       r3, r2
007CEE60: lsls       r6, r2, #3
007CEE62: lsls       r1, r3, #3
007CEE64: lsls       r4, r3, #3
007CEE66: lsls       r7, r3, #3
007CEE68: lsls       r2, r4, #3
007CEE6A: lsls       r5, r4, #3
007CEE6C: lsls       r0, r5, #3
007CEE6E: lsls       r3, r5, #3
007CEE70: lsls       r4, r4, #4
007CEE72: lsls       r4, r4, #4
007CEE74: lsls       r4, r4, #4
007CEE76: lsls       r4, r4, #4
007CEE78: lsls       r4, r4, #4
007CEE7A: lsls       r4, r4, #4
007CEE7C: lsls       r4, r4, #4
007CEE7E: lsls       r6, r5, #3
007CEE80: lsls       r1, r6, #3
007CEE82: lsls       r4, r6, #3
007CEE84: ldr        r0, [pc, #0x288] ; literal[007CF110]=00452616
007CEE86: add        r0, pc
007CEE88: bx         lr
007CEE8A: cmp.w      r0, #0x182000
007CEE8E: blt        #0x7ceebc
007CEE90: cmp.w      r0, #0x196000
007CEE94: bge        #0x7cef2e
007CEE96: cmp.w      r0, #0x184000
007CEE9A: blt        #0x7cef96
007CEE9C: beq.w      #0x7cf04c
007CEEA0: cmp.w      r0, #0x194000
007CEEA4: beq.w      #0x7cf052
007CEEA8: movw       r1, #0x5000
007CEEAC: movt       r1, #0x19
007CEEB0: cmp        r0, r1
007CEEB2: bne.w      #0x7cf0a6
007CEEB6: ldr        r0, [pc, #0x270] ; literal[007CF128]=0045255C
007CEEB8: add        r0, pc
007CEEBA: bx         lr
007CEEBC: cmp.w      r0, #-1
007CEEC0: blt        #0x7cef4e
007CEEC2: movw       r1, #0xe02f
007CEEC6: movt       r1, #0xd
007CEECA: cmp        r0, r1
007CEECC: ble        #0x7cefae
007CEECE: movw       r1, #0xe030
007CEED2: movt       r1, #0xd
007CEED6: cmp        r0, r1
007CEED8: beq.w      #0x7cf058
007CEEDC: cmp.w      r0, #0x180000
007CEEE0: beq.w      #0x7cf05e
007CEEE4: movw       r1, #0x1000
007CEEE8: movt       r1, #0x18
007CEEEC: cmp        r0, r1
007CEEEE: bne.w      #0x7cf0a6
007CEEF2: ldr        r0, [pc, #0x248] ; literal[007CF13C]=004524B0
007CEEF4: add        r0, pc
007CEEF6: bx         lr
007CEEF8: movw       r1, #0xffff
007CEEFC: movt       r1, #0x8003
007CEF00: cmp        r0, r1
007CEF02: ble        #0x7cef6e
007CEF04: movw       r1, #0xffff
007CEF08: movt       r1, #0x8005
007CEF0C: cmp        r0, r1
007CEF0E: bgt        #0x7cefbe
007CEF10: movs       r1, #0
007CEF12: movt       r1, #0x8004
007CEF16: cmp        r0, r1
007CEF18: beq.w      #0x7cf064
007CEF1C: movs       r1, #0
007CEF1E: movt       r1, #0x8005
007CEF22: cmp        r0, r1
007CEF24: bne.w      #0x7cf0a6
007CEF28: ldr        r0, [pc, #0x1a4] ; literal[007CF0D0]=004526C9
007CEF2A: add        r0, pc
007CEF2C: bx         lr
007CEF2E: cmp.w      r0, #0x198000
007CEF32: blt        #0x7cefd8
007CEF34: beq.w      #0x7cf06a
007CEF38: cmp.w      r0, #0x1c0000
007CEF3C: beq.w      #0x7cf070
007CEF40: cmp.w      r0, #0x1de000
007CEF44: bne.w      #0x7cf0a6
007CEF48: ldr        r0, [pc, #0x1c8] ; literal[007CF114]=00452538
007CEF4A: add        r0, pc
007CEF4C: bx         lr
007CEF4E: cmp.w      r0, #-0x40000000
007CEF52: bgt        #0x7ceff0
007CEF54: movs       r1, #0
007CEF56: movt       r1, #0x8011
007CEF5A: cmp        r0, r1
007CEF5C: beq.w      #0x7cf076
007CEF60: cmp.w      r0, #-0x40000000
007CEF64: bne.w      #0x7cf0a6
007CEF68: ldr        r0, [pc, #0x154] ; literal[007CF0C0]=004526E6
007CEF6A: add        r0, pc
007CEF6C: bx         lr
007CEF6E: movs       r1, #0
007CEF70: movt       r1, #0x8001
007CEF74: cmp        r0, r1
007CEF76: beq.w      #0x7cf07c
007CEF7A: movs       r1, #0
007CEF7C: movt       r1, #0x8002
007CEF80: cmp        r0, r1
007CEF82: beq        #0x7cf082
007CEF84: movs       r1, #0
007CEF86: movt       r1, #0x8003
007CEF8A: cmp        r0, r1
007CEF8C: bne.w      #0x7cf0a6
007CEF90: ldr        r0, [pc, #0x144] ; literal[007CF0D8]=00452639
007CEF92: add        r0, pc
007CEF94: bx         lr
007CEF96: cmp.w      r0, #0x182000
007CEF9A: beq        #0x7cf088
007CEF9C: movw       r1, #0x3000
007CEFA0: movt       r1, #0x18
007CEFA4: cmp        r0, r1
007CEFA6: bne        #0x7cf0a6
007CEFA8: ldr        r0, [pc, #0x188] ; literal[007CF134]=00452425
007CEFAA: add        r0, pc
007CEFAC: bx         lr
007CEFAE: adds       r1, r0, #1
007CEFB0: beq        #0x7cf08e
007CEFB2: cmp.w      r0, #0xde000
007CEFB6: bne        #0x7cf0a6
007CEFB8: ldr        r0, [pc, #0xf8] ; literal[007CF0B4]=004526E0
007CEFBA: add        r0, pc
007CEFBC: bx         lr
007CEFBE: movs       r1, #0
007CEFC0: movt       r1, #0x8006
007CEFC4: cmp        r0, r1
007CEFC6: beq        #0x7cf094
007CEFC8: movs       r1, #0
007CEFCA: movt       r1, #0x8010
007CEFCE: cmp        r0, r1
007CEFD0: bne        #0x7cf0a6
007CEFD2: ldr        r0, [pc, #0xf4] ; literal[007CF0C8]=00452647
007CEFD4: add        r0, pc
007CEFD6: bx         lr
007CEFD8: cmp.w      r0, #0x196000
007CEFDC: beq        #0x7cf09a
007CEFDE: movw       r1, #0x7000
007CEFE2: movt       r1, #0x19
007CEFE6: cmp        r0, r1
007CEFE8: bne        #0x7cf0a6
007CEFEA: ldr        r0, [pc, #0x134] ; literal[007CF120]=00452455
007CEFEC: add        r0, pc
007CEFEE: bx         lr
007CEFF0: movs       r1, #1
007CEFF2: movt       r1, #0xc000
007CEFF6: cmp        r0, r1
007CEFF8: beq        #0x7cf0a0
007CEFFA: movs       r1, #4
007CEFFC: movt       r1, #0xc000
007CF000: cmp        r0, r1
007CF002: bne        #0x7cf0a6
007CF004: ldr        r0, [pc, #0xb0] ; literal[007CF0B8]=00452680
007CF006: add        r0, pc
007CF008: bx         lr
007CF00A: ldr        r0, [pc, #0x100] ; literal[007CF10C]=004524A9
007CF00C: add        r0, pc
007CF00E: bx         lr
007CF010: ldr        r0, [pc, #0xf4] ; literal[007CF108]=004524B7
007CF012: add        r0, pc
007CF014: bx         lr
007CF016: ldr        r0, [pc, #0xec] ; literal[007CF104]=004524C6
007CF018: add        r0, pc
007CF01A: bx         lr
007CF01C: ldr        r0, [pc, #0xe0] ; literal[007CF100]=004524D2
007CF01E: add        r0, pc
007CF020: bx         lr
007CF022: ldr        r0, [pc, #0xd8] ; literal[007CF0FC]=004524E2
007CF024: add        r0, pc
007CF026: bx         lr
007CF028: ldr        r0, [pc, #0xcc] ; literal[007CF0F8]=004524F5
007CF02A: add        r0, pc
007CF02C: bx         lr
007CF02E: ldr        r0, [pc, #0xc4] ; literal[007CF0F4]=004524FF
007CF030: add        r0, pc
007CF032: bx         lr
007CF034: ldr        r0, [pc, #0xb8] ; literal[007CF0F0]=00452510
007CF036: add        r0, pc
007CF038: bx         lr
007CF03A: ldr        r0, [pc, #0xb0] ; literal[007CF0EC]=00452523
007CF03C: add        r0, pc
007CF03E: bx         lr
007CF040: ldr        r0, [pc, #0xa4] ; literal[007CF0E8]=00452538
007CF042: add        r0, pc
007CF044: bx         lr
007CF046: ldr        r0, [pc, #0x9c] ; literal[007CF0E4]=00452546
007CF048: add        r0, pc
007CF04A: bx         lr
007CF04C: ldr        r0, [pc, #0xe0] ; literal[007CF130]=00452398
007CF04E: add        r0, pc
007CF050: bx         lr
007CF052: ldr        r0, [pc, #0xd8] ; literal[007CF12C]=004523A8
007CF054: add        r0, pc
007CF056: bx         lr
007CF058: ldr        r0, [pc, #0x54] ; literal[007CF0B0]=00452657
007CF05A: add        r0, pc
007CF05C: bx         lr
007CF05E: ldr        r0, [pc, #0xe0] ; literal[007CF140]=0045232C
007CF060: add        r0, pc
007CF062: bx         lr
007CF064: ldr        r0, [pc, #0x6c] ; literal[007CF0D4]=00452579
007CF066: add        r0, pc
007CF068: bx         lr
007CF06A: ldr        r0, [pc, #0xb0] ; literal[007CF11C]=004523EA
007CF06C: add        r0, pc
007CF06E: bx         lr
007CF070: ldr        r0, [pc, #0xa4] ; literal[007CF118]=004523F6
007CF072: add        r0, pc
007CF074: bx         lr
007CF076: ldr        r0, [pc, #0x4c] ; literal[007CF0C4]=004525BD
007CF078: add        r0, pc
007CF07A: bx         lr
007CF07C: ldr        r0, [pc, #0x60] ; literal[007CF0E0]=00452525
007CF07E: add        r0, pc
007CF080: bx         lr
007CF082: ldr        r0, [pc, #0x58] ; literal[007CF0DC]=00452533
007CF084: add        r0, pc
007CF086: bx         lr
007CF088: ldr        r0, [pc, #0xac] ; literal[007CF138]=00452331
007CF08A: add        r0, pc
007CF08C: bx         lr
007CF08E: ldr        r0, [pc, #0x1c] ; literal[007CF0AC]=004522EC
007CF090: add        r0, pc
007CF092: bx         lr
007CF094: ldr        r0, [pc, #0x34] ; literal[007CF0CC]=00452571
007CF096: add        r0, pc
007CF098: bx         lr
007CF09A: ldr        r0, [pc, #0x88] ; literal[007CF124]=0045238E
007CF09C: add        r0, pc
007CF09E: bx         lr
007CF0A0: ldr        r0, [pc, #0x18] ; literal[007CF0BC]=004525C8
007CF0A2: add        r0, pc
007CF0A4: bx         lr
007CF0A6: movs       r0, #0
007CF0A8: bx         lr
007CF0AA: nop
007CF0AC: movs       r2, #0xec
007CF0AE: lsls       r5, r0, #1
007CF0B0: movs       r6, #0x57
007CF0B2: lsls       r5, r0, #1
007CF0B4: movs       r6, #0xe0
007CF0B6: lsls       r5, r0, #1
007CF0B8: movs       r6, #0x80
007CF0BA: lsls       r5, r0, #1
007CF0BC: movs       r5, #0xc8
007CF0BE: lsls       r5, r0, #1
007CF0C0: movs       r6, #0xe6
007CF0C2: lsls       r5, r0, #1
007CF0C4: movs       r5, #0xbd
007CF0C6: lsls       r5, r0, #1
007CF0C8: movs       r6, #0x47
007CF0CA: lsls       r5, r0, #1
007CF0CC: movs       r5, #0x71
007CF0CE: lsls       r5, r0, #1
007CF0D0: movs       r6, #0xc9
007CF0D2: lsls       r5, r0, #1
007CF0D4: movs       r5, #0x79
007CF0D6: lsls       r5, r0, #1
007CF0D8: movs       r6, #0x39
007CF0DA: lsls       r5, r0, #1
007CF0DC: movs       r5, #0x33
007CF0DE: lsls       r5, r0, #1
007CF0E0: movs       r5, #0x25
007CF0E2: lsls       r5, r0, #1
007CF0E4: movs       r5, #0x46
007CF0E6: lsls       r5, r0, #1
007CF0E8: movs       r5, #0x38
007CF0EA: lsls       r5, r0, #1
007CF0EC: movs       r5, #0x23
007CF0EE: lsls       r5, r0, #1
007CF0F0: movs       r5, #0x10
007CF0F2: lsls       r5, r0, #1
007CF0F4: movs       r4, #0xff
007CF0F6: lsls       r5, r0, #1
007CF0F8: movs       r4, #0xf5
007CF0FA: lsls       r5, r0, #1
007CF0FC: movs       r4, #0xe2
007CF0FE: lsls       r5, r0, #1
007CF100: movs       r4, #0xd2
007CF102: lsls       r5, r0, #1
007CF104: movs       r4, #0xc6
007CF106: lsls       r5, r0, #1
007CF108: movs       r4, #0xb7
007CF10A: lsls       r5, r0, #1
007CF10C: movs       r4, #0xa9
007CF10E: lsls       r5, r0, #1
007CF110: movs       r6, #0x16
007CF112: lsls       r5, r0, #1
007CF114: movs       r5, #0x38
007CF116: lsls       r5, r0, #1
007CF118: movs       r3, #0xf6
007CF11A: lsls       r5, r0, #1
007CF11C: movs       r3, #0xea
007CF11E: lsls       r5, r0, #1
007CF120: movs       r4, #0x55
007CF122: lsls       r5, r0, #1
007CF124: movs       r3, #0x8e
007CF126: lsls       r5, r0, #1
007CF128: movs       r5, #0x5c
007CF12A: lsls       r5, r0, #1
007CF12C: movs       r3, #0xa8
007CF12E: lsls       r5, r0, #1
007CF130: movs       r3, #0x98
007CF132: lsls       r5, r0, #1
007CF134: movs       r4, #0x25
007CF136: lsls       r5, r0, #1
007CF138: movs       r3, #0x31
007CF13A: lsls       r5, r0, #1
007CF13C: movs       r4, #0xb0
007CF13E: lsls       r5, r0, #1
007CF140: movs       r3, #0x2c
007CF142: lsls       r5, r0, #1

RANGE 00645B08..00645C20 _ZN4Anki5Cozmo18NVStorageComponent22ProcessOnIdleCallbacksEv
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
00645BAE: mov        r4, r0
00645BB0: ldr        r0, [sp]
00645BB2: cbz        r0, #0x645bd0
00645BB4: ldr        r1, [sp, #4]
00645BB6: cmp        r1, r0
00645BB8: beq        #0x645bcc
00645BBA: sub.w      r2, r1, #8
00645BBE: mvn        r3, #7
00645BC2: subs       r2, r2, r0
00645BC4: bic.w      r2, r3, r2
00645BC8: add        r1, r2
00645BCA: str        r1, [sp, #4]
00645BCC: blx        #0x4a40cc ; _ZdlPv IMPORT (resolve packaged dependencies before calling external)
00645BD0: mov        r0, r4
00645BD2: blx        #0x4a40a8 ; _Unwind_Resume IMPORT (resolve packaged dependencies before calling external)
00645BD6: nop
00645BD8: b          #0x646364
00645BDA: lsls       r1, r3, #1
00645BDC: ldrsb      r6, [r1, r1]
00645BDE: strb       r3, [r2, #0x11]
00645BE0: strb       r7, [r5, #9]
00645BE2: str        r1, [r4, #0x74]
00645BE4: muls       r5, r4, r5
00645BE6: ldr        r7, [r5, #0x54]
00645BE8: ldr        r0, [r6, #0x74]
00645BEA: str        r6, [r5, #0x54]
00645BEC: strb       r6, [r5, #0x11]
00645BEE: str        r6, [r5, r0]
00645BF0: ldr        r2, [r6, #0x74]
00645BF2: str        r3, [r4, #0x54]
00645BF4: strb       r3, [r6, #0xd]
00645BF6: ldr        r7, [r1, #0x64]
00645BF8: str        r1, [r1, #0x44]
00645BFA: str        r4, [r5, #0x54]
00645BFC: str        r3, [r0, #0x14]
00645BFE: ldr        r4, [r5, #0x44]
00645C00: str        r2, [r4, #0x14]
00645C02: ldr        r3, [r4, #0x34]
00645C04: cmp        r6, #0x73
00645C06: strb       r0, [r2, #9]
00645C08: str        r7, [r5, #0x34]
00645C0A: strb       r5, [r4, #0xd]
00645C0C: ldr        r3, [r6, #0x14]
00645C0E: str        r6, [r5, #0x74]
00645C10: str        r3, [r0, #0x14]
00645C12: ldr        r4, [r5, #0x44]
00645C14: str        r2, [r4, #0x14]
00645C16: ldr        r3, [r4, #0x34]
00645C18: movs       r0, r0
00645C1A: movs       r0, r0
00645C1C: ldr        r2, [r4, r3]
00645C1E: lsls       r3, r3, #1

RANGE 00645C20..00645C36 _ZN4Anki5Cozmo18NVStorageComponent24AddOneShotOnIdleCallbackENSt6__ndk18functionIFvvEEE
00645C20: push       {r4, lr}
00645C22: mov        r4, r0
00645C24: add.w      r0, r4, #0x110
00645C28: blx        #0x4b9a38 ; _ZNSt6__ndk15dequeINS_8functionIFvvEEENS_9allocatorIS3_EEE12emplace_backIJRS3_EEEvDpOT_ -> 00648034 size=76
00645C2C: mov        r0, r4
00645C2E: pop.w      {r4, lr}
00645C32: b.w        #0x8cce6c

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

RANGE 00643EC4..00643F94 _ZN4Anki5Cozmo18NVStorageComponentD1Ev
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
00643F8E: nop
00643F90: cbnz       r2, #0x643fb8
00643F92: lsls       r7, r3, #2

RANGE 00643F94..00643FA2 _ZN4Anki5Cozmo18NVStorageComponentD0Ev
00643F94: push       {r7, lr}
00643F96: blx        #0x4b9948 ; _ZN4Anki5Cozmo18NVStorageComponentD2Ev -> 00643EC4 size=D0
00643F9A: pop.w      {r7, lr}
00643F9E: b.w        #0x8ca88c

```

</details>
