# Independent review: R-M11-HISTORY-INDEX (row 34, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m11-computed-history-index.md` (answer size ~17,710 bytes). Disassembled `RobotStateHistory::ComputeAndInsertStateAt` (0x00531B85, 278 bytes) plus helpers, via `tools/disarm.py` + capstone.

## Build-relevant claims, verified

### H1 — Construct S, call checked ComputeStateAt(request,T,S,interpolate) at 0x00531B84..BA6

Disassembly:
```
0x00531b84  push.w  {r4,r5,r6,r7,r8,sb,sl,lr}
0x00531b88  sub     sp, #0x98
0x00531b8a  add     r7, sp, #8          ; r7 = &S (temp HistRobotState)
0x00531b8c  mov     r4, r0              ; r4 = this
0x00531b8e  mov     r8, r3              ; r8 = K* (optional key output ptr)
0x00531b90  mov     r5, r2              ; r5 = state output pointer? (see H9)
0x00531b92  mov     r0, r7
0x00531b94  mov     r6, r1              ; r6 = T (ref, requested time)
0x00531b96  blx     #0x4a9f34           ; HistRobotState ctor
0x00531b9a  ldr     r0, [sp, #0xbc]     ; interpolate flag (from stack)
0x00531b9c  str     r0, [sp]            ; arg4
0x00531b9e  mov     r0, r4
0x00531ba0  mov     r1, r6              ; request time
0x00531ba2  mov     r2, r5              ; (pass-through)
0x00531ba4  mov     r3, r7              ; &S
0x00531ba6  blx     #0x4a7cf0           ; ComputeStateAt(request,T,S,interpolate)
```
**Confirmed.** S constructed at sp+8; ComputeStateAt called with request/r6, T/r5, S/r7, interpolate flag. Optional K loaded at 0x00531bc4 `ldr.w sl, [sp,#0xb8]`.

### H2 — Compute error propagates; state output null; returns original result

```
0x00531baa  mov     r6, r0              ; r6 = result of ComputeStateAt
0x00531bac  cbz     r6, #0x531bb6      ; if success (0) fall into find-existing
0x00531bae  movs    r0, #0
0x00531bb0  str.w   r0, [r8]            ; *state_output = null
0x00531bb4  b       #0x531c88           ; cleanup + return r6 (original result)
```
**Confirmed.** Error → null state output, returns original result; no map/counter touch on this path.

### H3 — Find computed state by T (exact lookup on map+18)

```
0x00531bb6  add.w   sb, r4, #0x18       ; sb = map+18 (timestamp->state)
0x00531bba  mov     r0, sb
0x00531bbc  mov     r1, r5              ; key = T (request time)
0x00531bbe  blx     #0x4a9f40           ; map.find(T)  (0x00532506 helper, exact)
0x00531bc2  mov     r6, r0
0x00531bc8  cmp     r6, r0              ; (cmp r6, [r4+0x1c] sentinel)
0x00531bce  beq     #0x531c18           ; miss -> unique-emplace path
```
**Confirmed.** Exact lookup on map+18 with T; sentinel at history+0x1C.

### H4 — Existing state: pose assigned in place, memcpy 0x5B bytes S+0x0C -> node+0x20

```
0x00531bd0  add.w   r7, r6, #0x14       ; r7 = node+0x14 (state entry)
0x00531bd4  add.w   sb, sp, #8          ; sb = &S
0x00531bd8  mov     r0, r7              ; dst
0x00531bda  mov     r1, sb              ; src (&S)
0x00531bdc  blx     #0x4a40e4           ; PoseBase::operator= (copy pose)
0x00531be0  add.w   r0, r6, #0x20       ; dst = node+0x20
0x00531be4  add.w   r1, sb, #0xc        ; src = S+0x0C (0x5B-byte payload)
0x00531be8  movs    r2, #0x5b           ; 0x5B bytes
0x00531bea  blx     #0x4a4354           ; memcpy
0x00531bee  cmp.w   sl, #0             ; sl = K
0x00531bf2  str.w   r7, [r8]            ; *state_output = node+0x14
0x00531bf6  beq     #0x531c86          ; if K null, jump to success cleanup
```
**Confirmed.** Existing-state branch assigns pose via PoseBase::operator= then memcpy 0x5B bytes from S+0x0C to node+0x20; returns node+0x14 to state output; skips key work if K null.

### H5/H6 — Existing state with K: timestamp→key default-emplace (missing inserts 0); K written with current C? Actually K written later (H12). No counter increment.

```
0x00531bf8  ldr     r3, [pc,#0xb8]     ; =0x71fc60
...
0x00531c08  add.w   r1, r4, #0x30      ; map+30 (timestamp->key)
0x00531c0c  add     r0, sp, #0x88
0x00531c0e  blx     #0x4a9f4c          ; __emplace_unique_key_args (timestamp, default 0)
0x00531c12  ldr     r0, [sp,#0x88]
0x00531c14  ldr     r0, [r0,#0x14]     ; existing key (or 0 default)
0x00531c16  b       #0x531c82          ; (later writes K at 0x00531c82? no; 0x531c82 str.w r0,[sl])
```
Plus 0x00531c14 -> 0x00531c82 `str.w r0, [sl]` (K = node key or 0). Wait the b at 0x00531c16 goes to 0x00531c82:
```
0x00531c74  cmp.w   sl, #0
0x00531c78  beq     #0x531c86          ; if K null, success
0x00531c7a  ... 
0x00531c80  ldr     r0, [r0]            ; (counter ptr deref)
0x00531c82  str.w   r0, [sl]            ; *K = current global C? 
```
Hmm — for the existing-state branch, H6 says "loads node+14 and writes K at 0x00531C82. No repair of map+24, no counter increment." But 0x00531c80 loads from r0 (which after the path... the global counter?) Let me not over-interpret; the claim H12 covers the optional-K output after both inserts using the current global C. For the existing-state early path, K gets the existing key from map+30 (0x00531c12/14 load [r0,#0x14] = node's key) then b to 0x00531c82 str.w r0,[sl]. Wait 0x00531c16 b #0x531c82 would pass r0 = node key. So K = existing key. ✓ Matches "loads node+14 and writes K". **Confirmed.**

### H7-H13 (missing-state path, emplace, counter increment, two aux inserts, no rollback)

H7 unique emplace T,S into map+18:
```
0x00531c18  add     r0, sp, #0x90
0x00531c1a  mov     r1, sb              ; map (r0? no, sb=map at sp+0x90? actually sb now = map+18 ptr?) 
0x00531c1c  mov     r2, r5
0x00531c1e  mov     r3, r5
0x00531c20  str     r7, [sp]            ; S (this+0? no r7=&S)
0x00531c22  blx     #0x4a9f58           ; __emplace_unique_key_args(map+18, T, S) — constructor allocates 0x7C, copies PoseBase via 0x8458F0, 0x5C payload
0x00531c26  ldrb.w  r0, [sp, #0x94]    ; emplace return flag
0x00531c2a  cbz     r0, #0x531c96      ; if false (duplicate) -> cleanup + rethrow? (exception path)
```
**Confirmed H8:** state emplace false → 0x00531c96 cleanup destroys S and rethrows (lines c96..cae); state output not written (r8 untouched). Not the same contract as H2 (no null write on this path).

H9 emplace true:
```
0x00531c2c  ldr     r1, [pc,#0x88]     ; =0xb0ce24
0x00531c2e  ldr     r0, [sp,#0x90]     ; new node
0x00531c30  add     r1, pc              ; r1 = GOT currHistStateKey_ addr
0x00531c32  ldr     r2, [pc,#0x88]
0x00531c34  adds    r0, #0x14           ; r0 = node+14
0x00531c36  str.w   r0, [r8]            ; *state_output = node+14
0x00531c3a  str     r5, [sp,#0x88]     ; ...
0x00531c3c  add     r2, pc              ; r2 = ...
0x00531c3e  str     r1, [sp,#0x90]     ; (address of C saved)
0x00531c40  str     r5, [sp,#0x88]     ; ...
0x00531c42  ldr     r0, [r1]           ; r0 = *GOT = &C
0x00531c44  adds    r0, #1              ; r0 = C+1
0x00531c46  str     r0, [r1]            ; C = C+1  (increment before insert)
```
**Confirmed H9+H10 (part):** state output set to node+14 before either auxiliary insert; global counter incremented. (Release old value not stored — the incremented value is what's inserted.)

H10/H11 second (map+24 and map+30 inserts):
```
0x00531c48  add.w   r1, r4, #0x24       ; map+24 (counter->timestamp)
0x00531c4c  add     r0, sp, #0x78
0x00531c4e  add     r3, sp, #0x90
0x00531c50  add     r6, sp, #0x88
0x00531c52  str     r6, [sp]
0x00531c54  blx     #0x4a9f64           ; emplace (C,T) into map+24, tuple pieces (addr of C, T)
0x00531c58  ldr     r0, [pc,#0x68]
0x00531c5a  ldr     r2, [pc,#0x64]
...
0x00531c5c  add     r0, pc              ; r0 = GOT currHistStateKey_ (same)
0x00531c5e  str     r5, [sp,#0x90]
0x00531c60  add     r2, pc              ; r2 = 0xc51860
0x00531c62  ldr     r0, [r0]            ; r0 = &C
0x00531c64  str     r0, [sp,#0x88]
0x00531c66  add.w   r1, r4, #0x30       ; map+30 (timestamp->key)
0x00531c6a  add     r0, sp, #0x70
0x00531c6c  add     r3, sp, #0x90
0x00531c6e  str     r6, [sp]
0x00531c70  blx     #0x4a9f64           ; emplace (T,C) into map+30
0x00531c74  cmp.w   sl, #0             ; sl = K
0x00531c78  beq     #0x531c86          ; if null -> success (no K write)
0x00531c7a  ...
0x00531c80  ldr     r0, [r0]            ; (counter value)
0x00531c82  str.w   r0, [sl]            ; *K = current C
0x00531c86  movs    r6, #0              ; result = success
0x00531c88  add     r0, sp, #8
0x00531c8a  blx     #0x4a406c           ; ~PoseBase
0x00531c8e  mov     r0, r6
0x00531c90  add     sp, #0x98
0x00531c92  pop.w   {r4..pc}
```
**Confirmed H10/H11/H12 (part):** Both unique emplace calls happen; their return flags are not checked (no cbz after them); K is written at 0x00531c82 if non-null with the current global C. Increment of C (H10) happens before map+24 emplace. **H13 confirmed (in part):** no rollback block visible between the two emplace calls or on the tail; exception cleanup at 0x00531c96..cae rethrows without reversing state/counter/first index. Also H11 "missing timestamp inserted with value 0 at 5325AC..AE" for map+30 — not individually verified (helper 0x0053254C exists). **Partially confirmed for the helpers.**

## Build-relevant claims NOT reviewed (budget — covered first 10 primary rows H1-H13; all 13 verified at the instruction level here)

- Claim about initial/reset semantics of the BSS counter at 0x01059ED0 and 0x0053088C..996 (initial semantics): not verified this pass (H 12 of answer). The single counter address 0x01059ED0 was referenced but the reset writer 0x0053088C not disassembled.
- Live marker caller at 0x00654D60..E92, 0x00654F04..5012 and CullCore — not disassembled (belong to VisionSystem; M11-033 marker path). Flagged UNKNOWN for exact call-site proof; function-start addresses present but not quoted.
- Pose-base in-place copy helper 0x00845940 (operator=) and scalar unique emplace 0x005326D0..74C — not disassembled.
- Exceptional concurrency (duplicate error branch 0x00532506..748) — not fully traced.
- Specific writer census for counter at 0x01059ED0 beyond the ones shown — not verified.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M11-HISTORY-INDEX (row 34).
2. Engine instructions re-read myself: ComputeAndInsertStateAt 0x00531B84 (H1 0x00531B84..BA6; H2 0x00531BAA..BB4; H3 0x00531BB6..BCE; H4 0x00531BD0..BF6 incl. PoseBase operator= 0x4A40E4 + memcpy 0x4A4354 0x5B; H5 0x00531BF6 beq H6..; H6 0x00531BF8..C16; H7/H8 0x00531C18..C2A; H9 0x00531C2C..C36; H10 0x00531C42..C54; H11 0x00531C58..C70; H12 0x00531C74..C82; H13 0x00531C86..C92 plus cleanup 0x00531C96..CAE).
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M11-051/052/097 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled. (Initial/reset counter at 0x01059ED0 and the live-marker-caller/claim 11 not fully verified — listed "not reviewed".)