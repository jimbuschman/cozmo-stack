# Independent review: R-M11-RETAINED-OWNER (row 44, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m11-retained-pose-owner-comparison.md`. Disassembled cited ranges in `libcozmoEngine.so` via direct capstone (`tools/disarm.py`).

## Build-relevant claims, verified

### Claim 1 — SetPose (ObservableObject) at 0x004EF590..646 selects embedded node +4, runs PoseBase::operator= into it, stores state +0x24, name, nonnegative fromDistance; returns 0x004EF646

Disassembly 0x004EF590 (verbatim key instructions):
```
0x004ef590  push.w  {r4,r5,r6,r7,r8,sb,sl,lr}
0x004ef594  sub     sp, #0x30
0x004ef596  mov     sl, r0
0x004ef598  add.w   sb, sl, #4            ; sb = object+4 (embedded node)
0x004ef59a  mov     r7, r3
0x004ef59c  mov     r8, r2
0x004ef5a0  mov     r0, sb
0x004ef5a2  blx     #0x4a40e4            ; PoseBase::operator= into embedded node
...
0x004ef5a8  strb.w  r7, [sl, #0x24]      ; store state at +0x24
...
0x004ef5bc  ... name (basic_string at sp+0x20)
...
0x004ef5f6  strb.w  r0, [sp, #0x10]?       ; ...
0x004ef60e  ldrb.w  r0, [sp, #4]
...
0x004ef624  vmov    s0, r8
0x004ef628  vcmpe.f32 s0, #0
0x004ef62c  vmrs    apsr_nzcv, fpscr
0x004ef630  it      ge
0x004ef632  vstrge  s0, [sl, #0x10]       ; fromDistance stored if >=0 (nonnegative)
...
0x004ef636  ldrb.w  r0, [sp, #0x20]
...
0x004ef644  add     sp, #0x30
0x004ef646  pop.w   {r4,r5,r6,r7,r8,sb,sl,pc}  ; return
```
(small correction at tail: 0x004EF636 loads the name flag for cleanup; 0x004EF646 return.) 
**Confirmed.** Embedded object+4, PoseBase::operator= into it, state byte+0x24 stored, nonnegative fromDistance stored at +0x10, returns 0x004EF646. Matches claim "supported parent identity is carried through assignment, not by replacing the object wrapper or flattening its pose."

### Claim 2 — CopyWithNewPose at 0x00506EF8..F42: dest slot+0x28 called with newPose+source distance/state; confirmation entry+0x18 PoseBase::operator= with newPose; returns 0; two independent owned nodes sharing newPose's parent (0x00506EF8..F42)

```
0x00506ef8  push.w  {r4,r5,r6,r7,r8,lr}
...
0x00506f02  ldr     r0, [r6], #0x14      ; (dest parent list?)
0x00506f06  mov     r4, r2
0x00506f08  ldrb.w  r5, [r3, #0x24]     ; source state
0x00506f0c  ldr     r2, [r3, #0x10]     ; source fromDistance
...
0x00506f0e  ldr     r7, [r0, #0x28]     ; dest vtable slot +0x28
0x00506f10  mov     r0, r1              ; newPose
0x00506f12  mov     r1, r4              ; source object
0x00506f14  mov     r3, r5              ; source state
0x00506f16  blx     r7                   ; call -> Pose at destination with newPose (preserving new parent)
...
0x00506f1e  str     r6, [sp, #0xc]
0x00506f20  strd    r1, r0, [sp]
0x00506f26  add.w   r1, r8, #4          ; +4
0x00506f2a  add     r0, sp, #0x10
0x00506f2c  mov     r2, r6
0x00506f2e  blx     #0x4a6fe8           ; obtains confirmation entry into sp+0x10
0x00506f32  ldr     r0, [sp, #0x10]     ; confirmation entry
0x00506f34  mov     r1, r4              ; newPose
0x00506f36  adds    r0, #0x18           ; entry + 0x18
0x00506f38  blx     #0x4a40e4           ; PoseBase::operator= (with newPose)
0x00506f3c  movs    r0, #0              ; return 0
0x00506f3e  add     sp, #0x18
0x00506f40  pop.w   {r4,r5,r6,r7,r8,pc}
```
**Confirmed.** Dest slot+0x28 called with newPose + source distance/state; confirmation entry at +0x18 gets PoseBase::operator= (newPose, preserving newPose's parent); returns 0. Two independent owned nodes sharing newPose's parent. Matches claim.

### Claim 3 — SetObjectAsAttachedToLift at 0x00632FA2..3020; C's pose becomes child of lift (SetParent at 0x0063300E) — only C half verified

Disassembly 0x00632FA2..301E:
```
0x00632fa2  mov   r0, r8
0x00632fa4  blx   #0x4a412c             ; C = r8 GetPose -> r5
0x00632fa8  mov   r5, r0
0x00632faa  mov   r0, sb                ; sb = lift
0x00632fac  blx   #0x4a412c             ; lift GetPose -> r0
0x00632fb0  mov   r1, r0
0x00632fb2  add   r2, sp, #0x20
0x00632fb4  mov   r0, r5
0x00632fb6  blx   #0x4a40fc             ; PoseBase::GetWithRespectTo? / operator= (C rel lift) into sp+0x20
...
0x00633006  mov   r0, sb
0x00633008  blx   #0x4a412c            ; sb GetPose again
0x0063300c  mov   r1, r0
0x0063300e  add   r0, sp, #0x20
0x00633010  blx   #0x4a5b78            ; sp+0x20 SetParent(lift pose)  -> C's native pose becomes child of lift
0x00633014  ldr   r0, [r4]
0x00633016  ldr.w r0, [r0, #0x26c]
0x0063301a  add   r2, sp, #0x20
0x0063301c  mov   r1, r8
0x0063301e  mov   r3, sb
0x00633020  blx   #0x4b8004            ; virtual slot 0x26C(this, C ID r8, C-relative-pose, sb lift) — stores carrying state
```
**Partially confirmed (C half).** C's native pose is re-parented to lift via SetParent at 0x0063300E..10. The T (top object) half (T.NativePose parent = C.NativePose) is not individually present in this tail (handled earlier/later in CarryingComponent). Claim's "A1/A2" partially verified; **not fully verified for T**.

### Claims 4-15 (OV110 A3/A5/A6/A8-A15, Pose copy 0x008458F0/59D0, lambda1 transfer 0x00628E18..92F6, LocalizeToObject 0x005158D8..59B2, UpdateObjectOrigins 0x00620534..6F0, UnSetCarryingObjects 0x00633C00..3D80, A8-15 NotLastOwner, M11-095 race)
**Not reviewed** this pass (budget). The SetPose + CopyWithNewPose reopen is verified; the carried-stack rejigger production entry (LocalizeToObject) and origin transfer remain UNBUILT in C# (MoveObjectToNewOrigin uses flattened o.Pose).

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M11-RETAINED-OWNER (row 44).
2. Engine instructions re-read myself: SetPose 0x004EF590..646 confirmed (embedded node+4 PoseBase::operator= 0x4A40E4; state strb.w r7,[sl,#0x24]; fromDistance vstrge s0,[sl,#0x10] under vcmpe s0,#0; return 0x004EF646). CopyWithNewPose 0x00506EF8..F42 confirmed (vtable slot+0x28 blx r7 with newPose+source args; confirmation entry +0x18 PoseBase::operator= 0x4A40E4 newPose; movs r0,#0 return; pop). SetObjectAsAttachedToLift 0x00632FA2..3020 partially (C NativePose SetParent lift at 0x0063300E..10; T-half not in this tail). Claims A3/A5/A6/A8-A15 and UpdateObjectOrigins/LocalizeToObject/UnSet/lambda1/not-last-owner left not reviewed.
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M11-073/077/084 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled. (Parent identity through assignment confirmed; rejigger transfer stays UNBUILT in C# production code.)