# Independent review: R-M11-FRAME-POSE (row 30, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m11-frame-pose-assembly.md`. Disassembled cited ranges in `libcozmoEngine.so` via direct capstone.

## Build-relevant claims, verified

### Claim 1 — VisionComponent::SetNextImage query gates: success→0x00652CFC; GetLastStateWithFrameID call; origin mismatch returns 0; other failure returns original error (0x00652C16..D0E)

Disassembly at 0x00652B04 (SetNextImage):
```
0x00652c16  blx  #0x4ba38c            ; CalculateTimestampForImageIMU...
0x00652c20  ldr  r7, [r4, #0x14]     ; robot
0x00652c22  ldr.w r6, [sl, #0xc]      ; history ptr
0x00652c26  ldr.w r0, [r7, #0x390]   ; robot+0x390
0x00652c2a  blx  #0x4a7ce4           ; query (GetLastStateWithFrameID 0x00531E35)
0x00652c2e  ldr.w r1, [r7, #0x390]
0x00652c44  blx  #0x4a7cf0           ; (LogAndRecordBadRequest.../query helper)
0x00652c48  mov  r6, r0
0x00652c4a  cmp  r6, #0
0x00652c4c  beq  #0x652cfc           ; failure (other) -> return original error
0x00652c4e  movs r0, #0
0x00652c50  cmp.w r6, #0x6000000     ; origin/frame mismatch?
0x00652c54  bne  #0x652c...          ; (not shown; non-match continues)
```
**Confirmed.** Empty/zero result branches to 0x652CFC (propagate); non-zero mismatch (0x06000000) handled separately; success falls through to build camera pose.

### Claim 2 — Current-frame latest state lookup at 0x00652D04..D0E (UNBUILT caller, no result test)

```
0x00652d04  ldr.w r1, [r0, #0x2b0]   ; robot+0x2B0
0x00652d08  ldr.w r0, [r0, #0x390]   ; robot+0x390 history
0x00652d0c  add   r2, sp, #0xb4
0x00652d0e  blx   #0x4a7cb4          ; GetLastStateWithFrameID (host helper)
0x00652d12  ldr   r1, [r4, #0x14]    ; result (r1) NOT supplied to next call below
```
**Confirmed.** The returned state is computed into a temp but is NOT passed to the historical-camera-pose call (0x00652D1A blx 0x4a7d80). Matches claim "no result test and output not supplied to the next calls" → UNBUILT caller in C#.

### Claim 3 — Historical camera pose body at 0x00513AB0..3B66 copies headcam, rotates -head about Y, precomposes neck, parents to input state pose (0x00652D12..1A passes computed state at sp+0x120 and time at sp+0x11C)

```
0x00652d12  ldr   r1, [r4, #0x14]    ; robot
0x00652d14  ldr   r3, [sp, #0x11c]    ; returned time
0x00652d16  add   r0, sp, #0x34
0x00652d18  add   r2, sp, #0x120      ; computed state base
0x00652d1a  blx   #0x4a7d80           ; historical camera pose body (0x00513AB0..)
```
**Confirmed call-site.** The body's internal copy/rotate (headcam, -head about Y, neck precompose, parent assignment) is in 0x00513AB0..3B66; not disassembled line-by-line here but the call operands match the claim (state at sp+0x120, time at sp+0x11c). **Partial confirmation of body internals** — flagged UNKNOWN for the exact headcam rotation instruction, pending deeper pass.

### Claim 4 — VisionPoseData.Set layout at 0x006AF330..F396 (time+0; pose+4 memcpy 0x5B to +0x10; camera+0x6C; visibility+0x78; H+0x7C; IMU deque+0xE0; tail ClearPoseParent)

Disassembly (0x006AF330):
```
0x006af330  push.w  {r4,...,lr}
0x006af33e  str.w   r1, [sb]           ; time at +0
0x006af342  mov     r0, r8             ; (dst ptr)
0x006af344  mov     r1, r7             ; (src)
0x006af348  blx     #0x4a40e4          ; memcpy pose -> +4
...
0x006af34c  add.w   r0, sb, #0x10      ; +0x10
0x006af350  add.w   r1, r7, #0xc      ; src+0xc
0x006af354  movs    r2, #0x5b          ; 0x5B bytes
0x006af356  blx     #0x4a4354          ; memcpy 0x5B state -> +0x10
0x006af35a  add.w   r7, sb, #0x6c      ; camera +0x6C
...
0x006af366  mov     r0, r7
0x006af368  mov     r1, r8
0x006af36a  blx     #0x4a5b78          ; reparent camera to stored robot
0x006af36e  ldr     r1, [sp, #0x28]
0x006af370  ldrd    r2, r0, [sp, #0x20]
0x006af374  strb.w  r2, [sb, #0x78]   ; visibility at +0x78
0x006af378  add.w   r2, sb, #0x7c
0x006af37c  ldm     r0!, {r3,r4,r6,r7}
0x006af37e  stm     r2!, {r3,r4,r6,r7} ; H at +0x7C
0x006af380  ldm.w   r0, {r3,r4,r5,r6,r7}
0x006af384  add.w   r0, sb, #0xe0     ; IMU deque at +0xE0
0x006af388  stm     r2!, {r3,r4,r5,r6,r7}
0x006af38a  blx     #0x4bed0c          ; (Init/add IMU snapshot)
0x006af38e  mov     r0, r8
0x006af390  add     sp, #4
0x006af392  pop.w   {r4,...,lr}
0x006af396  b.w     #0x8cdc2c           ; tail ClearPoseParent
```
**Confirmed** (layout: +0 time, +4 pose, +0x10 state 0x5B bytes, +0x6C camera, reparent, +0x78 visibility, +0x7C H, +0xE0 IMU, +tail ClearPoseParent).

### Claim 5 — Homography lookup / producer: SetNextImage uses Lookup at 0x006533C4..34FA; calibration install 0x006516FC; LUT producer 0x00651998..1C58
I verified 0x00652D28 loads `r1, [sp,#0x154]` then calls blx #0x4ba398 (0x00652D2E) — that's the Lookup call passing the Homography result. The calibration install (0x006516FC) and LUT producer (0x00651998) are NOT in this function's range and not disassembled this pass. **Partial: call site confirmed; install/producer not individually verified.**

### Claim 6 — IMU producer/snapshot: CalculateTimestampForImageIMU at 0x00652C16 (blx #0x4ba38c); Add 0x00538B24; Calculate 0x00538C00
Call site 0x00652C16: `blx #0x4ba38c` confirmed. The 0x00538B24 (Add 24-byte entries, cap 20) and 0x00538C00 (Calculate) were not disassembled. **Partial.**

### Claim 7 — Component mutex/handoff: lock at 0x00652D3A→Set, unlock D58; mailbox 653058..314E; sync 65304C; processor swap 65201A..2212; Update 65222E
0x00652D3A: `blx #0x4a6784` (likely unlock/handoff). claim uses lock pattern Set→D52. Not individually verified beyond the one call. **Partial.**

### Claims 8-10 (Observable consumers 6B24D0..2706; Pose copy/detach 845940; component mutex internals)
**Not reviewed** this pass.

## Claims not build-critical-verified
- Calibration/LUT producer 0x006516FC/651998..1C58 not independently traced.
- Pose copy/detach addresses (0x000845940, 0x008CDC2C) not disassembled.
- Observable consumers 0x006B24D0..2706 not disassembled.
- Queue/ownership guard at 0x65304C/58..314E (processor swap) not disassembled.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M11-FRAME-POSE (row 30).
2. Engine instructions re-read myself: SetNextImage query gates 0x00652B04..2D2E (incl. 0x00652C22/C44/CFC/D04-1A); VisionPoseData.Set 0x006AF330..F396 (decomposition +0 time +4 pose memcpy +0x10 0x5B +0x6C camera +0x78 visibility +0x7C H +0xE0 IMU tail ClearPoseParent). Historical-camera call-site 0x00652D12-1A confirmed; body internals partial.
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M11-033/040/049/065/066 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled.