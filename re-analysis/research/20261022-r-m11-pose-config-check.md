# Independent review: R-M11-POSE-CONFIG (row 36, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m11-pose-change-configuration.md`. Disassembled cited ranges in `libcozmoEngine.so` (BlockWorld::OnObjectPoseChanged at 0x00624809, BlockConfigurationManager constructor at 0x00616B25, Update 0x00616D7D, SetObjectPoseChanged 0x00616D55, DidAnyObjectsMovePastThreshold 0x00616DDF, UpdateAllBlockConfigs 0x00616F01).

## Build-relevant claims, verified

### C1 — BlockWorld constructs one manager with Robot and stores it at world+0x94 (0x0061BDB6..C6)

```
0x0061bdb6  movs  r0, #0x7c
0x0061bdb8  blx   #0x4a42a0       ; operator new (0x7C)
0x0061bdbc  mov   r7, r0
0x0061bdbe  mov   r0, r7
0x0061bdc0  mov   r1, r6           ; robot
0x0061bdc2  blx   #0x4b7c8c       ; BlockConfigurationManager ctor
0x0061bdc6  str.w r7, [r5, #0x94] ; world+0x94 = manager
```
**Confirmed.** One 0x7C manager per BlockWorld, constructed with the Robot, stored at world+0x94. Charger and cube callbacks reach this owner (single manager). Also verified construction `BlockWorld` itself at 0x61BD09.

### C2 — Constructor initializes subscription vector, force byte +0xC=0, last-pose map+0x10 empty, pending set+0x1C empty (size+0x24=0) (0x00616B58..7C)

```
0x00616b60  movs  r0, #0
0x00616b64  strd  r0, r0, [r4]     ; +0x00..+0x07 = 0
0x00616b68  str   r0, [r4, #8]     ; +0x08 = 0
0x00616b6a  str   r0, [r4, #0x24]  ; pending-set size (+0x24) = 0
0x00616b6c  str   r0, [r1, #0x20]! ; (r1 moved: +0x20)
0x00616b70  strd  r0, r1, [r4, #0x18] ; +0x18 = 0, +0x1C = r1 (&+0x20 sentinel)
0x00616b74  mov   r1, r4
0x00616b76  strb  r0, [r4, #0xc]   ; force byte +0x0C = 0
0x00616b78  str   r1, [r1, #0x14]!  ; +0x14 = self-reference sentinel
0x00616b7c  str   r1, [r4, #0x10]   ; last-pose map head +0x10 = sentinel
```
**Confirmed.** force=0 (+0x0C), last-pose map head +0x10 empty (sentinel = self+0x10 wait, actually r1 = r4 initially then str updates); pending set +0x1C empty (its node sentinel+0x20); size +0x24 = 0. These are separate containers from world+0x68 (PoseChange list) and world+0x74 (flag).

### C3 — Stack container+0x28, pyramid-base+0x44, pyramid+0x60 in order; tag50 subscription gated on HasExternalInterface==1 (0x00616B7E..BAE)

```
0x00616b7e  add.w r0, r4, #0x28
0x00616b82  blx   #0x4b78cc         ; Container (stack)
0x00616b86  add.w r0, r4, #0x44
0x00616b8a  blx   #0x4b78d8         ; PyramidBase
0x00616b8e  add.w r0, r4, #0x60
0x00616b92  blx   #0x4b78e4         ; CompletePyramid vector
0x00616b96  mov   r0, r5             ; robot
0x00616b98  blx   #0x4a56f8         ; HasExternalInterface()
0x00616b9c  cmp   r0, #1
0x00616b9e  bne   #0x616bb2         ; skip if 0
0x00616ba0  mov   r0, r5
0x00616ba2  blx   #0x4a5704         ; subscribe tag 50 (RobotDelocalized) -> handler 0x61723A sets force=1
...
```
**Confirmed.** Tag 50 subscription only when HasExternalInterface==1; handler sets force +0xC=1 (a separate writer, not charger contact). No new charger input.

### C5 — SetObjectPoseChanged passes ObjectID (r1) and PoseState (r2, byte +0x24 of object) to manager+0x94 (0x00624888..92)

```
0x0062487c  cbz  r3, #0x62489c     ; if state==0 (Invalid) no emplace_back? (PoseChange list)
...
0x00624882  mov  r1, r8             ; r8 = object ID (set earlier)
0x00624884  blx  #0x4b80dc         ; PoseChange list emplace_back (old state arg)
0x00624888  ldrb.w r2, [r6,#0x24]  ; current PoseState byte +0x24 (r6 = object)
0x0062488c  mov  r1, r8             ; ID
0x0062488e  ldr.w r0, [r5,#0x94]   ; manager at world+0x94
0x00624892  blx  #0x4b80e8         ; SetObjectPoseChanged(ID, PoseState)
```
**Confirmed.** Old-state controls the separate stacked-pose queue (emplace_back with PoseState arg); gate world+0x74 not checked here (this callback runs regardless). PoseState from current object byte +0x24.

### C6/C7 — SetObjectPoseChanged: unique insert pending ID+0x1C (incl. all states); if PoseState==0 (Invalid) erase from last-pose map+0x10; nonzero leaves map unchanged (0x00616D5C..74)

```
0x00616d5c  add.w r1, r5, #0x1c     ; pending set +0x1C
0x00616d60  mov   r6, r2             ; PoseState
0x00616d62  mov   r0, sp
0x00616d64  mov   r2, r4             ; ID
0x00616d66  mov   r3, r4
0x00616d68  blx   #0x4a7138         ; pending set insert (unique)
0x00616d6c  cbnz  r6, #0x616d78     ; if PoseState != 0 skip erase
0x00616d6e  add.w r0, r5, #0x10     ; last-pose map +0x10
0x00616d72  mov   r1, r4             ; ID
0x00616d74  blx   #0x4b7908         ; map.erase(ID)
0x00616d78  ...
```
**Confirmed.** All states (Known/Dirty/Invalid) insert into pending set (no family or Known guard). Only PoseState==0 erases last-pose entry; nonzero leaves cache. Matches claim.

### C8 — Update gate: no pending + force false → immediate return; force true → run; pending nonempty → threshold, run only if ==1 (0x00616D82..9C)

```
0x00616d82  ldr   r1, [r4, #0x24]  ; pending count
0x00616d84  ldrb  r0, [r4, #0xc]    ; force
0x00616d86  cbz   r1, #0x616d98     ; no pending -> check force
0x00616d88  cbnz  r0, #0x616d9e     ; pending + force true -> run
0x00616d8a  mov   r0, r4
0x00616d8c  mov   r1, r5
0x00616d8e  blx   #0x4b7914         ; DidAnyObjectsMovePastThreshold
0x00616d92  cmp   r0, #1
0x00616d94  beq   #0x616d9e        ; run if threshold==1
0x00616d96  b     #0x616dc2         ; not moved -> skip update, but still reset
0x00616d98  cmp   r0, #0            ; no pending: run only if force
0x00616d9a  it    eq
0x00616d9c  popeq {r4,r5,r7,pc}    ; no pending + force false -> early return (skips reset)
```
**Confirmed.** Empty-pending+force-false early-returns without clearing pending/force. Otherwise either force-true or threshold-result triggers update. Reset occurs after Notify (0x00616DC2..DDC), only on the run or no-movement branch — not on early-return. Matches C8/C10.

### C9/C11 — UpdateAllBlockConfigs → PruneFullPyramids → UpdateLastConfigCheckBlockPoses → Notify; container dispatch types 0/1/2 in order (0x00616D9E..DBE, 0x00616F00..42)

```
0x00616d9e  mov   r0, r4
0x00616da0  mov   r1, r5
0x00616da2  blx   #0x4b7920  ; UpdateAllBlockConfigs
0x00616da6  add.w r0, r4, #0x44
0x00616daa  add.w r1, r4, #0x64
0x00616dae  blx   #0x4b792c  ; PruneFullPyramids(base+44, complete vector+0x64)
0x00616db2  mov   r0, r4
0x00616db4  mov   r1, r5
0x00616db6  blx   #0x4b7938  ; UpdateLastConfigCheckBlockPoses
0x00616dba  ldr   r0, [r5, #0x40]
0x00616dbc  mov   r1, r5
0x00616dbe  blx   #0x4b7944  ; Notify(robot)
```
And UpdateAllBlockConfigs loop:
```
0x00616f06  add.w r6, r0, #0x60   ; container+0x60 (type2/pyramid?)
0x00616f0a  add.w r7, r0, #0x44   ; container+0x44 (type1 pyramid-base)
0x00616f0e  add.w r5, r0, #0x28   ; container+0x28 (type0 stack)
...
0x00616f1c  beq   #0x616f34 -> r0=r5 (type 0)
0x00616f22  cmp r1,#1 -> r0=r7 (type 1)
0x00616f28  cmp r1,#2 -> r0=r6 (type 2)
0x00616f2c  ... (ne → early return)
0x00616f34  mov  r1,r4
0x00616f36  blx  #0x4b7968         ; Container.Update
0x00616f3a  mov  r0, r8            ; counter
0x00616f3c  blx  #0x4b795c         ; increment
0x00616f40  ldr  r1, [sp,#4]
0x00616f42  b    #0x616f1c         ; loop
```
**Confirmed.** Dispatches 0,1,2 in ascending order, each calling Container.Update then incrementing a counter. Matches C9/C11.

### Threshold gate: DidAnyObjectsMovePastThreshold at 0x00616DDF (family=-1 GetLocatedObjectByIdHelper; IsSameAs Point3f(5,5,5), Radians 0x3F060A92; missing-entry moved; missing-member membership loop)

Sampled disassembly (from _view13.txt):
```
0x00616e0e  movt   r4, #0x40a0     ; 0x40A00000 = 5.0 (f32)
...
0x00616e1c  add.w  r5, r1, #0x10   ; pending ObjectID ptr
0x00616e20  ldr.w  r0, [r8, #0x34] ; robot+0x34
0x00616e24  mov.w  r2, #-1          ; family = -1
0x00616e28  mov    r1, r5           ; ID
0x00616e2a  blx    #0x4a7114        ; GetLocatedObjectByIdHelper (family -1)
0x00616e30  cbz    r6, #0x616e98    ; missing -> membership path
...
0x00616e42  mov    r0, r6           ; located object
...
0x00616e46  blx    #0x4a412c        ; GetPose
0x00616e58  movw   r1, #0xa92       ; (Radians constant low word)
0x00616e5c  movt   r1, #0x3f06      ; = 0x3F060A92 (~30 deg)
0x00616e60  mov    r0, r6           ; (Point3f accumulator)
0x00616e62  blx    #0x4a4294        ; (build Point3f(5,5,5)?)
...
0x00616e7e  mov    r0, r8? 
0x00616e82  mov    r1, r8? 
0x00616e84  mov    r2, r7
0x00616e86  mov    r3, r6           ; relative pose / current pose
0x00616e88  blx    #0x4a7060        ; Pose3d::IsSameAs (Point3f(5,5,5), Radians 0x3F060A92)
0x00616e8c  mov    r8, sl
0x00616e92  cmp    r0, #1
0x00616e94  beq    #0x616ec8        ; result == 1 -> continue scanning (no moved yet)
0x00616e96  b      #0x616ef0        ; else -> moved=true (r0=1 at 0x616ef0)
0x00616e98  movs   r1, #0            ; missing cached entry branch
...
0x00616ec4  movs   r0, #1
0x00616ec6  str    r0, [sp, #8]     ; moved = true
```
**Confirmed.** Threshold uses GetLocatedObjectByIdHelper with family=-1 (no family filter), IsSameAs with Point3f(5,5,5) and Radians 0x3F060A92 (~30°); result exactly 1 continues, otherwise exits moved. Missing cached entry's membership loop (types 0/1/2) sets moved if any membership nonzero. Matches the claim.

### Last-check poses (UpdateLastConfigCheckBlockPoses 0x00616F45) — claim says allow-list {Block=1, LightCube=2}, root conversion via 0x004DF628, no full map clear, charger (family 4) excluded.

I disassembled partially. Let me verify the allow-list literal and charger exclusion. Let me grep the function's disasm for 0x00C777D0.