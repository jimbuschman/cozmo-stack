# Independent review: R-M11-POSE-SERIAL (row 31, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m11-pose-serialization-callers.md`. Disassembled cited ranges in `libcozmoEngine.so` via direct capstone and `tools/disarm.py`.

## Build-relevant claims, verified

### Claim 1 — Inbound converter Pose3d(PoseStruct3d, origins) at 0x00846C70..D16 (UNBUILT in C#)

Disassembly (0x00846C70):
```
0x00846c70  push.w  {r4..lr}
0x00846c7a  vldr    s0, [r7, #0xc]
0x00846c7e  ...
0x00846c86  vcvt.f64.f32 d0, s0          ; f32 quat -> f64 (x)
0x00846c8a  ...
0x00846c90  vldr    s6, [r7, #0x18]      ; (quat w at +0x18? store)
0x00846c94  vldr    s4, [r7, #0x14]
0x00846c98  vcvt.f64.f32 d0, s6
0x00846c9c  vcvt.f64.f32 d1, s2
0x00846ca0  vcvt.f64.f32 d2, s4
0x00846ca4  vstmia  sp, {d1,d2}
0x00846ca8  vstr    d0, [sp,#0x10]
0x00846cac  blx     #0x4a93dc             ; UnitQuaternion_(f64 quat)
0x00846cc2  ldr     r1, [r7, #0x1c]       ; input+0x1C
0x00846cc4  ldr     r6, [r7, #8]          ; translation z (3rd word)
...
0x00846cc6  blx     #0x4a5b6c             ; PoseOriginList::GetOriginByID(input+0x1C)
...
0x00846cbc  ldrd    sb,sl,[r7]            ; translation x,y (first two words)
0x00846cf6  blx     #0x4ce1f8             ; PoseBase constructor
```
**Confirmed.** f32 quaternion words at +0x0C/+0x10/+0x14/+0x18 → vcvt to f64 → UnitQuaternion_; 3 f32 translation words (ldrd first two + ldr #8); GetOriginByID(input+0x1C); parented PoseBase. Matches claim.

### Claim 2 — Outbound converter ToPoseStruct3d at 0x00846D38..DEE (UNBUILT in C#)

Disassembly (0x00846D38):
```
0x00846d38  push    {r4,r5,r6,r7,lr}
0x00846d3c  mov     r4, r0
0x00846d3e  add     r0, sp, #0x20
...
0x00846d44  bl      #0x4df628             ; root conversion (GetRootID semantics), origin mismatch path
0x00846d48  ldr     r0, [sp,#0x24]
0x00846d4a  cbz     r0, #0x846d54        ; cbz -> r5=0
0x00846d4c  blx     #0x4ce0d8             ; GetRootID
0x00846d50  mov     r5, r0
...
0x00846d56  blx     #0x4a7bc4             ; ContainsOriginID(r5); cbnz guard at 0x846d5e
0x00846d6c  ldr     r6, [r6,#4]          ; parent ptr
0x00846d6e  ldr     r7, [sp,#0x24]
0x00846d70  cbz     r6, #0x846db6        ; null parent -> skip invariant
0x00846d72  ldr     r0, [r6,#0x30]
0x00846d74  ... Dev_AssertIsValidParentPointer
0x00846d7e  add     r0, sp, #0x14
0x00846d80  add     r1, sp, #0x20
0x00846d82  blx     #0x4a74d4             ; GetParent
0x00846d86  ldr     r0, [sp,#0x18]
0x00846d88  ldr     r0, [r0,#0x44]
0x00846d8a  cmp     r0, r5
0x00846d8c  beq     #0x846db0            ; parent origin == root -> pass
... (verify fail path "BadParent")
0x00846db6  vldmia  r7, {d0,d1,d2,d3}    ; PoseBase transform/translation as doubles
0x00846dba  add.w   r2, r7, #0x20
0x00846dbe  vcvt.f32.f64 s0, d0          ; narrow quaternion/translation doubles -> f32
...
0x00846dc4  stm.w   r4, {r0,r1,r2}       ; translation words written
0x00846dca  str     r5, [r4,#0x1c]       ; ID written (root origin)
0x00846dcc  vcvt.f32.f64 s2/s4/s6       ; quaternions
0x00846dd8  vstr    s0,[r4,#0xc] ...     ; quat words at +0xC/+0x10/+0x14/+0x18
0x00846de8  blx     #0x4a406c           ; ~PoseBase
```
**Confirmed.** Flatten to root (GetRootID + ContainsOriginID), verify membership and parent invariant (GetParent, origin == root, else "BadParent" sVerifyFailedReturnFalse), writes translation (stm r4), narrows f64→f32 quaternion (vcvt.f32.f64 x4), writes ID (str r5, [r4,#0x1c]). Matches claim, including "VERIFYs continue to output."

### Claim 3 — Robot state publication: GetRobotState at 0x005180E0..8106 calls Robot::GetPose → ToPoseStruct3d (0x00846D39) with robot+0x294 origins; copies 32-byte struct; yaw/lift override; UnknownOrigin warning.

Disassembly (0x005180D8):
```
0x005180dc  mov   r7, r0            ; output
0x005180de  mov   r0, r5? (r5=r1)   ; robot
0x005180e0  mov   r0, r5
0x005180e2  bl    #0x4ea398         ; Robot::GetPose
0x005180e6  ldr.w r2, [r5, #0x294]  ; origins
0x005180ea  mov   r6, sp             ; temp struct
0x005180ec  mov   r1, r0             ; robot pose
0x005180ee  mov   r0, r6
0x005180f0  blx   #0x4a5bf0          ; ToPoseStruct3d (PoseOriginList const&)
0x005180f4..0x00518106  ldm/stm 4x4 words (32-byte struct copy into output r7)
0x0051810c  cmp   r0, ip             ; ip = UnknownOriginID
0x00518110  bne   #0x518148          ; if equal -> warning "UnknownOriginId"
...
0x00518148  mov   r0, r5
0x0051814a  bl    #0x4ea398          ; GetPose again
0x0051814e  blx   #0x4a4048          ; GetTransform
0x00518156  blx   #0x4a4150          ; GetAngleAroundZaxis
0x00518160  str   r0, [r7, #0x20]    ; yaw override at output+0x20
0x00518162  ...
0x0051816e  ldrd  r2,r3,[r5,#0x30c] ; lift (radians?)
0x00518172  ...
```
**Confirmed.** Uses ToPoseStruct3d with robot+0x294 origins, copies 32 bytes into the 32-byte engine RobotState, warns when origin==UnknownOriginID, then overrides yaw (+0x20) and reads lift (r2,r3 from robot+0x30c). Matches "SUPPORTED live boundary needing full quaternion and root-ID input, not only yaw/current origin."

### Claims 4-16
Not individually verified this pass (ObservableObject report at 0x61FF7C; LocatedObjectStates 0x61E6C0; Face report 0x4F4B06; whiteboard 0x56C448/C48A; object-position reaction 0x61153C; custom object 0x61E1EC; and outbound/inbound C# suppliers). Marked **not reviewed** below.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M11-POSE-SERIAL (row 31).
2. Engine instructions re-read myself: inbound converter 0x00846C70..D16 (quat f32->f64, translation 3 words, GetOriginByID(input+0x1C), PoseBase); outbound converter 0x00846D38..DEE (root, GetRootID, ContainsOriginID, parent invariant, vcvt.f32.f64 x4, stm translation, str ID +0x1C); GetRobotState 0x005180D8.. (GetPose, ToPoseStruct, 32-byte copy, UnknownOrigin warn, yaw/lift override). Claims 4-16 not reviewed.
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M1-053, M11-038/042/065/066, M14-008, M8-014, M10-003 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled.