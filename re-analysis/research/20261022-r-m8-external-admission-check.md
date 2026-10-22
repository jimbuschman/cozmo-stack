# Independent review: R-M8-EXTERNAL-ADMISSION (row 40, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m8-external-action-admission.md`. Disassembled cited ranges in `libcozmoEngine.so` (Robot constructor 0x0050FEFA..FF20, SetRunningAndResumeInfo 0x005A209E..2112, UpdateRobotPropertiesForReaction 0x005A2F54..64, BehaviorManager constructor 0x005A0864..8AE, Queue consumer 0x0053D94C..70).

## Build-relevant claims, verified

### E1 — Robot constructor initializes external-actions-disabled byte (Robot+0x2C7) to 0 (0x0050FEFA..FF18..20)

Disassembly (Robot ctor 0x0050FEF0..FF22):
```
0x0050fef0  adds  r1, #0xff
0x0050fef2  movs  r2, #1
0x0050fef4  add   r0, pc
...
0x0050fefa  movs  r1, #0
0x0050fefe  movt  sb, #0xbf80
0x0050ff00  ldr   r0, [r0]
0x0050ff02  str.w r1, [r5, #0x2B0]
0x0050ff06  adds  r0, #8
0x0050ff08  str.w r1, [r5, #0x2C0]
0x0050ff0c  strb.w r1, [r5, #0x2BC]
0x0050ff10  strb.w r2, [r5, #0x2C4]
0x0050ff14  strb.w r1, [r5, #0x2C6]
0x0050ff18  strb.w r1, [r5, #0x2C7]    ; byte+0x2C7 = 0
0x0050ff1c  str.w sb, [r5, #0x2C8]
0x0050ff20  str.w r0, [r5, #0x2B4]
```
**Confirmed.** Robot+0x2C7 (external-actions-disabled) initialized to 0 at constructor. Additional neighboring fields: +0x2BC=0, +0x2C0=0, +0x2C4=1, +0x2C6=0, +0x2C7=0, +0x2C8=sb (~-0x003F8000? actually sb = sb reg, wait movt sb,#0xBF80), +0x2B0=0, +0x2B4=... . Note: r5 here is the Robot (this) — assumed from constructor prologue; not independently verified but the address +0x2C7 match is the claim. Confirmed initialization to 0 for byte +0x2C7.

### E3 (old/new type read) + E4 (broadcast before mutation) + E5 (None→non-None sets r1=1; non-None→None sets r1=0; both non-None skip flag writes) + E6 (MC+D4 first, then Robot+0x2C7) — 0x005A209E..2112

Disassembly 0x005A209E..0x005A2112:
```
0x005a209e  push.w  {r4,r5,r6,r7,r8,sb,sl,fp,lr}
0x005a20a2  sub     sp, #0x14
0x005a20a4  mov     r5, r0               ; r5 = this (BehaviorManager)
0x005a20a6  mov     fp, r1               ; r1 = new info (fp)
0x005a20a8  ldr     r0, [r5, #0x1c]
0x005a20aa  ldrb    r0, [r0, #0x10]     ; old type byte at [manager+1C]+0x10
0x005a20ac  cmp     r0, #0x16            ; 0x16=22 NoneTrigger
0x005a20ae  itt     eq
0x005a20b0  ldrbeq.w r0, [fp, #0x10]    ; if old==None: new type byte
0x005a20b4  cmpeq   r0, #0x16             ; and new==None?
0x005a20b6  beq     #0x5a20e2            ; both None -> skip transition broadcast AND flag writes (E3)
0x005a20b8  ldr     r0, [r5, #4]
0x005a20ba  blx     #0x4a5704           ; (ReactionTriggerTransition(old,new) broadcast — pre-existing helper) E4
0x005a20be  ldr     r1, [r5, #0x1c]
0x005a20c0  add.w   r2, sp, #0x12
0x005a20c4  ldrb    r1, [r1, #0x10]
0x005a20c6  strb.w  r1, [sp, #0x13]      ; old type on stack
0x005a20ca  ldrb.w  r1, [fp, #0x10]
0x005a20ce  strb.w  r1, [sp, #0x12]      ; new type on stack
0x005a20d2  add.w   r1, sp, #0x13
0x005a20d6  bl      #0x5a2ee8            ; (additional notified side effects)
0x005a20da  ldrb.w  r0, [fp, #0x10]       ; new type
0x005a20de  cmp     r0, #0x16
0x005a20e0  bne     #0x5a20ec            ; new != None -> 0x5a20ec
0x005a20e2  ldr     r0, [r5, #0x1c]      ; (both None or new==None path)
0x005a20e4  ldrb    r0, [r0, #0x10]      ; old type again
0x005a20e6  cmp     r0, #0x16
0x005a20e8  bne     #0x5a2102            ; old != None -> 0x5a2102 -> movs r1,#0 (non-None->None sets 0) at 0x5a2104
0x005a20ea  b       #0x5a2114            ; (both None) -> skip flag writes
0x005a20ec  ldr     r0, [r5, #0x1c]
0x005a20ee  ldrb    r0, [r0, #0x10]      ; old type
0x005a20f0  cmp     r0, #0x16
0x005a20f2  bne     #0x5a20fa            ; old != None -> 0x5a20fa (both non-None: re-check new)
0x005a20f4  ldr     r0, [r5, #4]
0x005a20f6  movs    r1, #1              ; old None -> new non-None sets r1 = 1
0x005a20f8  b       #0x5a2106            ; -> write flags
0x005a20fa  ldrb.w  r0, [fp, #0x10]
0x005a20fe  cmp     r0, #0x16
0x005a2100  bne     #0x5a2114            ; both non-None (new != None) -> skip flag writes (E5 row: both non-None skip)
0x005a2102  ldr     r0, [r5, #4]        ; robot (manager+4)
0x005a2104  movs    r1, #0              ; old non-None -> new None sets r1 = 0
0x005a2106  ldr.w   r0, [r0, #0x254]    ; MovementComponent at robot+0x254
0x005a210a  strb.w  r1, [r0, #0xd4]      ; MC byte+0xD4 = r1 (FIRST)
0x005a210e  ldr     r0, [r5, #4]
0x005a2110  strb.w  r1, [r0, #0x2c7]    ; robot byte+0x2C7 = r1 (SECOND) — E6
; 0x5a2112? function continues to E7...
                                    ; gate is already updated at this point (E7 half confirmed)
```
**Confirmed E1-E6 (instruction-quoted).** Transition broadcast at 0x005A20B8 runs before the flag writes at 0x005A2106/10 (E4 ordering). None→non-None sets r1=1 (0x005A20F6); non-None→None sets r1=0 (0x005A2104); both non-None (and both None) skip flag writes (0x005A20EA, 0x005A2100). E6: writes MC+0xD4 first then robot+0x2C7 (0x005A2106..10).

### E7 — after light/ownership side effects, replace current pair/resume pair/type byte; gate already updated (0x005A2270..22AA)

Did not disassemble 0x005A2270..22AA this pass. The answer claims this block runs after gate mutation; I verified 0x005A20E2..2112 only covers up to r1 flag writes. **UNKNOWN for E7 body specifics** (not instructions quoted). Function existence + flow confirmed up to 0x005A2112 only.

### E8 — UpdateRobotPropertiesForReaction (0x005A2F54..64) writes MC+D4 then Robot+2C7 (helper)

Disassembly:
```
0x005a2f54  ldr   r2, [r0, #4]
0x005a2f56  ldr.w r2, [r2, #0x254]       ; MC
0x005a2f5a  strb.w r1, [r2, #0xd4]       ; MC+D4 first
0x005a2f5e  ldr   r0, [r0, #4]           ; robot
0x005a2f60  strb.w r1, [r0, #0x2c7]      ; robot+0x2C7 second
0x005a2f64  bx    lr
```
**Confirmed E8.** Helper writes MC+0xD4 then robot+0x2C7 same input byte r1; no extra policy/SDK test/queue cancellation in helper. Matches claim.

### E2 — BehaviorManager allocates zeroed 0x14 running/resume info; sets type byte+10 to 0x16 (22=NoneTrigger); stores at manager+1C (0x005A0894..8A2,0x005A08AE)

BehaviorManager constructor 0x? Not independently disassembled in this pass. The runtime evidence (SetRunningAndResumeInfo at 0x005A20A8 reads [r5,#0x1C] and tests byte+0x10 against 0x16) is consistent with "current running/resume info at manager+1C, type at +10", but the constructor's zero/allocate/initialize at 0x005A0894..8A2 was **not instruction-verified this pass** (flagged UNKNOWN for the writer). The existence of the pattern in SetRunningAndResumeInfo supports but does not prove the constructor behavior claimed.

### "SwitchToBehaviorBase calls new behavior.Init before SetRunningAndResumeInfo at 5A1F12; new Init sees old gate" (ordering consequence, 0x005A1F12 not opened)

Not verified this pass (M8-012 owns switching order). **UNKNOWN — not traced.**

### Queue consumer 0x0053D94C..70 (external-tag ranges [1,1,000,000] & [2,000,001,3,000,000])

Previously partially verified (row-26 redo): 0x0053D94C `ldr r0,[r5,#4]`; 0x0053D94E `ldrb.w r0,[r0,#0x2C7]` (reads incoming runner's robot+0x2C7). 0x0053D954 `movw r3,#0x4240`; 0x0053D958 `ldr r2,[r5,#0x60]` (incoming tag); 0x0053D95A `movt r3,#0xF` (=0xF4240=1,000,000). Comparable against r4=r2-1 (0x0053D95E subs r4,r2,#1). Additional BAD_TAG filter for [2,000,001..3,000,000] not displayed in the earlier disasm view (cut at 0x0053D972). **Partial (first threshold 0xF4240 confirmed at 0x0053D954/5A, second excluded range not independently quoted this pass — flagged).** Later branches in QueueActionNow (0x0053DCB4..) not traced for the second range.

### Reset/uncertain writers (constructor + opposite reaction transition); finite direct scan stores (0x50FF18, 0x5A2110, 0x5A2F60)

I verified 0x50FF18 and 0x5A2110 and 0x5A2F60 above (those are the same E1/E6/E8 writes). No other Reset writer introduced by these. But the claim "never reset by ActionList.Clear/Cancel/scope/etc." is a universal negative — **UNKNOWN** (not exhaustive; no source address given for other potential writers; finite direct scan method relies on immediate BR, not whole-program). Flagged.

### Positive ordinary-app witness (Unity Robot.cs) — authority 2, not source substitution

Not a native claim. Out of scope for disassembly verification; retained as evidence note only.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M8-EXTERNAL-ADMISSION (row 40).
2. Engine instructions re-read myself: Robot constructor 0x0050FEF0..FF22 (0x0050FF18 strb.w r1,[r5,#0x2C7] confirmed); SetRunningAndResumeInfo 0x005A209E..2112 (0x005A20A8 ldr r0,[r5,#0x1C]; 0x005A20AA ldrb r0,[r0,#0x10]; 0x005A20AC cmp r0,#0x16; 0x005A20AE itt eq; 0x005A20B0 ldrbeq.w r0,[fp,#0x10]; 0x005A20B4 cmpeq r0,#0x16; 0x005A20B6 beq skip; 0x005A20B8 ldr r0,[r5,#4]; 0x005A20BA blx ReactionTriggerTransition at 0x5A20B8 (before flag mutation); 0x005A20EC..F8 None→non-None sets r1=1; 0x005A20FA..F2: both non-None → skip; 0x005A2102/04: non-None→None sets r1=0; 0x005A2106 ldr.w r0,[r0,#0x254] MC ptr; 0x005A210A strb.w r1,[r0,#0xD4] MC first; 0x005A210E ldr r0,[r5,#4]; 0x005A2110 strb.w r1,[r0,#0x2C7] robot second). Helper 0x005A2F54..64 (0x005A2F54 ldr r2,[r0,#4]; 0x005A2F56 ldr.w r2,[r2,#0x254]; 0x005A2F5A strb.w r1,[r2,#0xD4]; 0x005A2F5E ldr r0,[r0,#4]; 0x005A2F60 strb.w r1,[r0,#0x2C7]; 0x005A2F64 bx lr). Queue consumer already verified in row 26 (0x0053D94C ldr r0,[r5,#4]; 0x0053D94E ldrb.w r0,[r0,#0x2C7]; threshold 0xF4240 at 0x0053D954/5A). E2 constructor (0x005A0894..8A2, 0x005A08AE), E7 body (0x005A2270..22AA), SwitchToBehaviorBase ordering at 0x005A1F12, second external-tag range [2,000,001..3,000,000], and complete indirect-writer closure remain **UNKNOWN/not verified** this pass.
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M8-007/012/015 remain IMPLEMENTATION_GAP. The answer's supported-reaction framing (not SDK flag) is instruction-supported for writers verified (E1, E3-E6, E8); recovery of the BehaviorManager constructor allocation (E2) and the exact second excluded range aren't proven here.

No new research queued. No code edited. No record settled. (E7/side-effects and E2 construction details left as UNKNOWN; see file.)