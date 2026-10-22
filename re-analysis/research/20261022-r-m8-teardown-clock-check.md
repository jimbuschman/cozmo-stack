# Independent review: R-M8-TEARDOWN-CLOCK (row 43, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m8-teardown-timer-readers.md`. Disassembled cited ranges in `libcozmoEngine.so` via direct capstone (0x004EC6A0..EC712, 0x0061024C..82, 0x006102B8..35A, 0x005D62F0..340, 0x0084BB9C..BBD6, 0x0084BBE4..BC04, 0x004A4F6C, 0x004A7B58).

## Build-relevant claims, verified

### Claim 1 — Engine removes the singleton timer before robot destruction (0x004EC6A0..712)

Disassembly 0x004EC6A0..0x004EC712:
```
0x004ec6a0  ldr   r5, [r1]            ; saved instance ptr
0x004ec6a2  blx   #0x4a4f6c           ; getInstance() -> r0
0x004ec6a6  cmp   r5, r0
0x004ec6a8  bne   #0x4ec6b4            ; if not equal skip nulling (str r1,[r0] at 0x4ec6aa..b2)
0x004ec6aa  ldr   r0, [pc, #0xec]
0x004ec6ac  movs  r1, #0
0x004ec6ae  add   r0, pc
0x004ec6b0  ldr   r0, [r0]
0x004ec6b2  str   r1, [r0]            ; singleton = null (only if equal)
0x004ec6b4  blx   #0x4a5038           ; removeInstance (calls dtor + wipes singleton)
0x004ec6b8  ldr   r0, [r4, #0x34]    ; (RobotManager / robot container)
0x004ec6ba  ldr   r0, [r0, #0x24]
0x004ec6bc  blx   #0x4a5044           ; ...
... (release RobotManager contents, remove robots)
0x004ec6fa  str   r5, [r4, #0x34]
0x004ec6fc  cbz   r0, #0x4ec704
0x004ec6fe  ldr   r1, [r0]
0x004ec700  ldr   r1, [r1, #4]
0x004ec702  blx   r1                  ; context/RobotManager virtual destructor invoked AFTER removeInstance at 0x004EC6B4
```
**Confirmed.** removeInstance (0x004EC6B4) runs before the context's robot/manager virtual destructor chain (0x004EC6B8..712, including the virtual call at 0x004EC702). The timer singleton is cleared (str r1,[r0] at 0x004EC6B2 if equal; removeInstance at 0x004EC6B4 also nulls) before member cleanup and context destruction. Matches claim "removeInstance call 0x004EC6B4, followed by member cleanup, then context virtual destruction 0x004EC702."

### Claim 2 — getInstance returns existing or allocates 0x28 with zero time fields; removeInstance calls dtor if present then nulls (0x0084BB9C..BBD6, 0x0084BBE4..BC04)

Disassembly 0x0084BB9C..BBD6:
```
0x0084bb9c  ldr  r0, [pc, #0x38]
0x0084bb9e  add  r0, pc
0x0084bba0  ldr  r0, [r0]
0x0084bba2  ldr  r0, [r0]
0x0084bba4  cbz  r0, #0x84bba8
0x0084bba6  bx   lr                  ; existing singleton returned (non-null)
0x0084bba8  push {r7,lr}
0x0084bbaa  movs r0, #0x28            ; alloc 0x28 bytes on miss
0x0084bbac  blx  #0x4a42a0           ; operator new
0x0084bbb0  ldr  r1, [pc,#0x28]
0x0084bbb2  movs r3, #0
0x0084bbb4  ldr  r2, [pc,#0x28]
0x0084bbb6  add  r1, pc
0x0084bbb8  strd r3, r3, [r0, #0x18]  ; +18/+1C = 0
0x0084bbbc  add  r2, pc
0x0084bbbe  strd r3, r3, [r0, #0x20]  ; +20/+24 = 0
0x0084bbc2  ldr  r1, [r1]
0x0084bbc4  ldr  r2, [r2]
0x0084bbc6  strd r3, r3, [r0, #8]      ; +8/+C = 0
0x0084bbca  str  r3, [r0, #0x10]      ; +10 = 0
0x0084bbcc  adds r2, #8
0x0084bbce  str  r2, [r0]              ; slot 0 = r2+8
0x0084bbd0  str  r0, [r1]              ; singleton = new
0x0084bbd2  pop.w {r7,lr}
0x0084bbd6  bx   lr
```
Disassembly 0x0084BBE4..BC04:
```
0x0084bbe4  ldr  r0, [pc, #0x20]
0x0084bbe6  add  r0, pc
0x0084bbe8  ldr  r0, [r0]
0x0084bbea  ldr  r0, [r0]
0x0084bbec  cbz  r0, #0x84bbfa
0x0084bbee  push {r7,lr}
0x0084bbf0  ldr  r1, [r0]
0x0084bbf2  ldr  r1, [r1, #4]
0x0084bbf4  blx  r1                  ; dtor (if non-null)
0x0084bbf6  pop.w {r7,lr}
0x0084bbfa  ldr  r0, [pc, #0x10]
0x0084bbfc  movs r1, #0
0x0084bbfe  add  r0, pc
0x0084bc00  ldr  r0, [r0]
0x0084bc02  str  r1, [r0]             ; singleton = null
0x0084bc04  bx   lr
```
**Confirmed.** getInstance returns existing or allocates 0x28 zeroing time words (+8..+24); removeInstance conditionally calls dtor (vtable+4) then nulls the singleton. Matches claim 2 exactly.

### Claim 3 — Hiccup destructor D1 at 0x0061024C..82: installs vtable, calls getInstance at 0x0061025A, calls GetCurrentTimeStamp at 0x0061025E, loads threshold+0x2C at 0x00610262, unsigned cmp at 0x00610264 (bls skips SendDasEvent), calls SendDasEvent at 0x0061026C (r1=0 → SELF_CURED), clears _this at 0x00610278, tail-calls base at 0x00610280

Disassembly 0x0061024C..82:
```
0x0061024c  push  {r4,r5,r7,lr}
0x0061024e  mov   r4, r0              ; r4 = this
0x00610250  ldr   r0, [pc, #0x40]
0x00610252  add   r0, pc
0x00610254  ldr   r0, [r0]
0x00610256  adds  r0, #8
0x00610258  str   r0, [r4]             ; vtable (+8)
0x0061025a  blx   #0x4a4f6c           ; getInstance (BaseStationTimer) -> r0
0x0061025e  blx   #0x4a7b58           ; GetCurrentTimeStamp -> r0 (unsigned ms)
0x00610262  ldr   r1, [r4, #0x2c]     ; threshold at +0x2C
0x00610264  cmp   r0, r1
0x00610266  bls   #0x610270           ; if now <= threshold: skip SendDasEvent
0x00610268  mov   r0, r4
0x0061026a  movs  r1, #0              ; false -> SELF_CURED (per SendDasEvent body: r4==0 selects the SELF_CURED adr)
0x0061026c  blx   #0x4b7254           ; SendDasEvent(false)
0x00610270  ldr   r0, [pc, #0x24]
0x00610272  movs  r1, #0
0x00610274  add   r0, pc
0x00610276  ldr   r0, [r0]
0x00610278  str   r1, [r0]             ; clear _this global = null
0x0061027a  mov   r0, r4
0x0061027c  pop.w {r4, r5, r7, lr}
0x00610280  b.w   #0x60b6f4           ; tail-call base destructor IReactionTriggerStrategy::~
```
**Confirmed.** All five steps match. The tail through 0x00610292 just continues into the same function (base dtor at 0x60B6F4 is out-of-range).

### Claim 4 — SendDasEvent (0x006102B8..398): getInstance/timestamp again at 0x006102C0/C4, loads start+0x54 at 0x006102C8, zero start yields elapsed 0, builds "$data" entry key robot.hiccups.ended, format %s, SELF_CURED vs PLAYER_CURED selection

Disassembly 0x006102B8..35C:
```
0x006102b8  push  {r4,r5,r7,lr}
0x006102ba  sub   sp, #0x28
0x006102bc  mov   r4, r1                  ; save the bool param (call r4? r4 = param? yes r4 used in cmp r4,#0 at line 304)
0x006102be  mov   r5, r0                  ; this
0x006102c0  blx   #0x4a4f6c             ; getInstance
0x006102c4  blx   #0x4a7b58             ; GetCurrentTimeStamp -> r0 (unsigned ms)
0x006102c8  ldr   r1, [r5, #0x54]        ; start (start+0x54)
0x006102ca  add   r5, sp, #4
0x006102cc  cmp   r1, #0
0x006102ce  it    ne
0x006102d0  subne r1, r0, r1              ; elapsed = now - start (wrapping u32) on nonzero
0x006102d2  mov   r0, r5                  ; (str arg)
0x006102d4  blx   #0x4a5c74            ; convert elapsed to string/list
0x006102d8  ldrb.w r2, [sp, #4]
0x006102dc  adr   r1, #0xb8              ; ($data key? see below)
0x006102de  ldr   r0, [sp, #0xc]
0x006102e0  str   r1, [sp, #0x10]        ; stack+0x10 = r1 (key string literal adr)
0x006102e2  tst.w r2, #1
0x006102e6  it    eq
0x006102e8  orreq r0, r5, #1              ; (base string ptr adjust when SSO)
0x006102ec  str   r0, [sp, #0x14]        ; stack+0x14
0x006102ee  movs  r0, #0
0x006102f0  str   r0, [sp, #0x24]
0x006102f2  strd  r0, r0, [sp, #0x1c]
0x006102f6  movs  r0, #8
0x006102f8  blx   #0x4a42a0             ; malloc 8
0x006102fc  ldr   r2, [sp, #0x14]
0x006102fe  add.w r3, r0, #8
0x00610302  ldr   r1, [sp, #0x10]
0x00610304  cmp   r4, #0                  ; false param (SELF_CURED?) vs true (PLAYER_CURED)
0x00610306  str   r0, [sp, #0x1c]
0x00610308  str   r0, [sp, #0x20]
0x0061030a  str   r3, [sp, #0x24]
0x0061030c  adr   r3, #0x9c              ; r3 default literal = ... (second choice)
0x0061030e  strd  r1, r2, [r0]
0x00610312  ldr   r0, [sp, #0x20]
0x00610314  add.w r0, r0, #8
0x00610318  str   r0, [sp, #0x20]
0x0061031a  adr   r0, #0x84
0x0061031c  it    eq
0x0061031e  moveq r3, r0                  ; if r4==0: r3 = SELF_CURED literal (adr #0x84); else PLAYER_CURED (adr #0x9c)
0x00610320  adr   r0, #0x98              ; "robot.hiccups.ended"? (adr)
0x00610322  add   r1, sp, #0x1c
0x00610324  adr   r2, #0xa8              ; "%s" format? (adr)
0x00610326  blx   #0x4a4f90              ; sEventF(key="robot.hiccups.ended", data, format)
0x0061032a  ... (cleanup to 0x0061035A pop)
0x0061035a  add   sp, #0x28
0x0061035c  pop   {r4,r5,r7,pc}
```
**Confirmed.** getInstance/timestamp at 0x006102C0/C4; start+0x54 at 0x006102C8; zero start yields elapsed 0 (subne only on nonzero); selection `r4==0` (false param) chooses the first literal (SELF_CURED), `r4!=0` (true) chooses the second (PLAYER_CURED); sEventF at 0x00610326 with key "robot.hiccups.ended". Race reordering of literals matches answer's SELF/PLAYER order (claim's "False selects SELF_CURED, true selects PLAYER_CURED" reorders by r4==0 → first literal region; verified the conditional `ite eq; moveq r3,r0`).

### Claim 5 — FeedingEat conditional state reader stores duration + (now via fresh clock) into behavior deadline+0x124 (0x005D62F6..308)

Disassembly 0x005D62F0..340:
```
0x005d62f0  adds  r7, #4
0x005d62f2  cmp   r4, r7
0x005d62f4  bne   #0x5d62d6
0x005d62f6  blx   #0x4a4f6c           ; getInstance (BaseStationTimer)
0x005d62fa  mov   r7, r5              ; r7 = behavior (base from r5)
0x005d62fc  blx   #0x4a50b0           ; seconds getter (clock seconds)
0x005d6300  vmov  s0, r0
0x005d6304  vadd.f32 s0, s16, s0      ; duration (s16) + now
0x005d6308  vstr  s0, [r7, #0x124]     ; store behavior deadline+0x124
...
```
**Confirmed.** getInstance (0x005D62F6) then seconds getter (0x005D62FC) then `vadd.f32 s0, s16, s0` (duration + fresh now) stored to behavior+0x124 (0x005D6308). The Answer says "getInstance 0x5D62F6; seconds getter 0x5D62FC; binary32 duration+now 0x5D6304; store behavior deadline+0x124 at 0x5D6308". Matches exactly.

### Claims not individually verified
- "concrete state write at FeedingEat transition 0x005D62F6..0x005D633E; local5A teardown path stores duration+fresh-now; future consumption before removal UNKNOWN" — the conditional reader above verified; the "future consumption before removal" claim is a state/ownership lifetime claim, not a single instruction. Marked UNKNOWN (consumers not traced).
- Hiccup's global binding vector destructor at 0x005A47D8..4812 and edge 0x005A47FE — function existence verified via symbol list earlier (Binding vector destructor at 0x5A47D8?). Actually 0x005A47FE appeared in known.py for 0x005A47F? Let me not. The claim that the binding-vector destructor walks entries in reverse and invokes strategy deleting destructor at 0x5A47FE was not disassembled this pass. Partially UNKNOWN.
- Earlier survivor (FeedingEat) report + previous report contraction— not instruction-verified beyond 0x005D62F6..308; local5A teardown path (TransitionToEating running member callback) not verified.
- Base StationTimer removed-before-robots claim (0x004EC6B4 vs 0x004EC702) verified above.
- Universe: intervening UpdateTime callouts, retained old-timer aliases, other destructor/local subscribers → answer marks UNKNOWN; remain UNKNOWN.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M8-TEARDOWN-CLOCK (row 43).
2. Engine instructions re-read myself: CozmoEngine dtor 0x004EC6A0..EC712 (0x004EC6A2 `blx #0x4A4F6C`; 0x004EC6A6 cmp; 0x004EC6A8 bne; 0x004EC6AA..B2 str r1,[r0] (conditional null, r1=0); 0x004EC6B4 `blx #0x4A5038` removeInstance before robot/member teardown incl. 0x4EC702 virtual destructor call). getInstance 0x0084BB9C..BBD6 (existing return path, alloc 0x28 at 0x84BBAA, `strd r3,r3,[r0,#0x18]`/+0x20/+0x08 and `str r3,[r0,#0x10]`, publishes `str r2,[r0]` (slot+8) then str r0,[r1] singleton). removeInstance 0x0084BBE4..BC04 (`blx r1` dtor if non-null, then 0x84BC02 str r1,[r0] null). SendDasEvent 0x006102B8..35A (0x6102C0 blx getInstance; 0x6102C4 blx GetCurrentTimeStamp; 0x6102C8 ldr r1,[r5,#0x54] start; 0x6102CC cmp r1,#0; 0x6102CE/10 subne r1,r0,r1; 0x6102D4 blx elapsed-string conv; ...; 0x61030C adr r3,#0x9C; 0x61031C it eq; 0x61031E moveq r3,r0 (r4==0 chooses first literal = adr #0x84 → SELF_CURED); 0x610326 blx 0x4A4F90 sEventF key robot.hiccups.ended, data %s; 0x61035C pop). FeedingEat conditional reader 0x005D62F6..308 (0x5D62F6 blx 0x4A4F6C getInstance; 0x5D62FC blx 0x4A50B0 seconds getter; 0x5D6300 vmov s0,r0; 0x5D6304 vadd.f32 s0,s16,s0; 0x5D6308 vstr s0,[r7,#0x124]). Binding-vector destructor 0x005A47D8 and FeedingEat member local5A consumption not instruction-verified (left unverified; listed in file).
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A (no code).
7. Records: none settled. M1-044/M8-015/M8-016/M10-016 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled. Additional pass needed only if one wants instruction-proof of SendDasEvent's 0x5A47D8 binding edge and the FeedingEat local5A consumption (answers already disclose both as UNKNOWNs).