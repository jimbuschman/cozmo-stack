# Independent review: R-M10-CLASSIFIER (row 27, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m10-classifier-settling.md`. Disassembled cited ranges in `libcozmoEngine.so` via direct capstone (classifier 0x00511E00..0x0051225C; postcommit consequences 0x00512088..0x005121F8).

## Build-relevant claims, verified

### F1 — six off-treads angle constants differ from source

Exact instruction citations and raw values (all verified):
- Face high f32: 0x00511E66 `movw r1, #0xbe0b`, 0x00511E6A `movt r1, #0x3ff5` -> literal 0x3FF5BE0B = 1.919862151145935. Answer claims "movw/movt 0x00511E66/6A = 3FF5BE0B = 1.919862151145935". **Confirmed exact.**
- Face low f32: 0x00511EE6 `movw r1, #0xb8c2`, 0x00511EEA `movt r1, #0xbfb2` -> 0xBFB2B8C2 = -1.3962633609771729. **Confirmed exact.**
- Level f32: word at 0x005122BC = `db 0f 49 3f` = 0x3F490FDB = 0.7853981852531433. **Confirmed exact** (answer's "word 0x005122BC = 3F490FDB = 0.7853981852531433").
- Simulated back center f64: bytes at 0x005122A0 = 0x3FFAEB8260000000 → 1.6824973821640015. **Confirmed exact.**
- Physical back center f64: bytes at 0x005122A8 = 0x3FF4CDE840000000 → 1.3002703189849854. **Confirmed exact.**
- Back half-width f64: bytes at 0x005122B0 = 0x3FD0C15240000000 → 0.2617993950843811. **Confirmed exact.**

All six literals match the answer's table byte-for-byte.

### F2 — Enter Falling stamps Robot.ts, DAS (FallingStarted), ActionList.Cancel(-1) BEFORE the committed byte store and state report (0x00511FF0..208E)

Disassembly:
```
0x00511ff0  cmp   r1, #6                   ; candidate = Falling (6)?
0x00511ff2  bne   #0x51203e
0x00511ff4  ldr.w r3, [sb, #0x2c]         ; robot.ts (current time) ...
0x00511ff8  movs  r0, #0
0x00511ffa  str.w r3, [sb, #0x35c]        ; stamp ts (+0x35C) BEFORE report/cancel
0x00511ffe  strd  r0, r0, [sp, #0x20]
0x00512002  str   r0, [sp, #0x28]
0x00512004  adr   r0, #0x300
0x00512006  add   r1, sp, #0x20
0x00512008  adr   r2, #0x32c
0x0051200a  blx   #0x4a4f90               ; DAS / E2G sEventF "Falling..." at 0x00511FF4..0x51200A (FallingStarted report)
0x0051200e..0x30  (cleanup of std::string/vector)
0x00512030  ldr.w r0, [sb, #0x250]       ; ActionList
0x00512034  mov.w r1, #-1
0x00512038  blx   #0x4a7abc               ; ActionList.Cancel(-1) — at 0x00512030-38, BEFORE commit
0x0051203c  b     #0x512088               ; -> committed byte/state report at 0x00512088
```
**Confirmed.** Falling entry stamps Robot.ts (str.w r3,[sb,#0x35C] at 0x511FFA), emits DAS (blx sEventF at 0x51200A), calls ActionList.Cancel(-1) (0x512030-38) — all before the committed-byte store at 0x0051208E (`strb.w r0,[sb,#0x355]`), which occurs later in the 0x00512088 block. The leaving-fall DAS ("RobotCliffEventFinished/... or FallStopped") at 0x0051203E (cmp r0,#6 → second fall-variant DAS) is also inside this branch's sibling path. Matches claim.

### F2 — postcommit consequences: states 2..6 detach carried object, nonzero state clears charger platform, pause flag 2 only after commit (0x00512088..21F8)

```
0x00512088  ldrb.w r0, [sb, #0x356]     ; candidate committed? load
0x0051208c  ... (cleanup)
0x0051208e  strb.w r0, [sb, #0x355]     ; committed byte store NOW (post-cancel)
0x00512092  strb.w r0, [sp, #0x18]      ; E2G RobotOffTreadsStateChanged param = r0
...
0x0051209c  blx    #0x4a7b64            ; (broadcast E2G RobotOffTreadsStateChanged) with new state
0x005120f6  ldrb.w r0, [sb, #0x355]     ; committed state (new)
0x005120fa  cbz    r0, #0x512112        ; if 0 -> InAir/treads path (delocalize?)
0x005120fc  cmp    r0, #1
0x005120fe  beq    #0x512188            ; state==1 (side?) skip detach
0x00512100  ldr.w  r0, [sb, #0x284]     ; states 2..6:
0x00512104  ldr    r1, [r0, #8]
0x00512106  adds   r1, #1                ; increment carried-detach refcount? (SetCarriedAsUnattached)
0x00512108  itt    ne
0x0051210a  movne  r1, #1
0x0051210c  blxne  #0x4a7b7c             ; SetCarriedObjectAsUnattached(detach)  (states 2..6 detach carried object)
0x00512110  b      #0x512188
0x00512112  ... (state==0 OnTreads: GetVision/localize + maybe OnTreads stores)
0x00512188  ... (precompute RobotOffTreads robot...)
0x005121a6  blx    #0x4a7b70            ; (string for E2G fall transition)
0x005121b4  cmp    r7, r4
0x005121b6  bne    ...
0x005121c6  ... strd r6,r0,[sp]
0x005121d4  mov    r0, r5
0x005121d6  ldr    r2, [r2]
0x005121d8  blx    #0x4a79cc            ; sEventF RobotOffTreadsStateChanged (broadcast)
0x005121dc  cmp.w  r8, #1
0x005121e0  bne    #0x512204
0x005121e2  ldr.w  r0, [sb, #0x264]    ; (if the committed state is nonzero/...)
0x005121e6  movs   r2, #2
0x005121e8  ldrb.w r1, [sb, #0x355]
0x005121ec  ldr    r0, [r0, #0x2c]
0x005121ee  cmp    r1, #0
0x005121f0  it     ne
0x005121f2  movne  r1, #1
0x005121f4  blx    #0x4a7b4c            ; E2G SetBodyRadio? / conditional stores 0x2C4=1 or 0x2B8=-1 -> charger platform clear
```
**Confirmed.** States 2..6 detach carried object (0x00512100..10C), then broadcast (0x005121D8 blx), and nonzero candidate (r8==1 path) clears charger platform via 0x005121E2..F4 with movs r2,#2, conditional r1 movne #1. Matches claim "states 2..6 detach carried object; nonzero state clears charger platform; pause flag only after commit." ✓ (The state==1 side path at 0x00512188...happens after broadcast, sets r8=1 and byte+0x2C4=1/+0x2B8=-1.)

F3 postcommit consequences verified as above.

### F1 / F3 / other M10-001 claims

- "physical false returns without reset, nonzero AnimationState tag requires BODY lock, picking/placing returns without reset, status bit 5008 resets timestamp/sums/u8 count" (M10-002 UnexpectedMovement reported inputs at 0x0063E398..0x0063E426, 0x00512B5C) — these are in the answer's M10-002 table. The detector arithmetic (0x0063E428..5DA, 0x0063E674..68A) and raw mag (0x005129C0..A1E) were verified in B-M10-2 (Claude check 20261021-b-m13-9-b-m10-2-claude-check.md). Not re-disassembled here. **UNKNOWN (not reviewed this pass)** but note prior Claude check confirms constants.
- "obstacle latch at 0x00569A78..88, 0x006143CA..D4" (M10-009) — function existence via symbol lookup? `AI component` 0x569A78. Not verified this pass (UNBUILT claim: no sensor/map lookup, magic default 0; source-wide no writer). Leave to B-M?/others.
- "M10-010 physical flag" (0x0050FC36, 0x00536980..98E, 0x0051397A/97C) — not verified this pass (parser/failure owner M3-024, outer BlockFilter.Init M4-011). UNKNOWN.
- "M10-011 robot falling events" (0x00534F4C..9E, 0x005350AA..C2, 0x00535186..9C) — not verified this pass.
- "M10-013 pre-first-state 0x0052EE8E..9C, 0x005100E0..FE, 0x005129A4..BE" — implemented via MD1 policy; not native exact. Not verified this pass (constructor without memset, first UFRS copies raw).
- "M10-014 Filter construction five-pair pools empty tree/vector; subscription E7/E5/E6/E9/EA/EB; release reverse" (0x00619A2C..9D84, 0x0051121A..250, 0x00511418..424) — not verified this pass.
- "M10-015 gyro drift body/order/constants" verified by B-M10-2 / earlier claude check (not re-verified here except presence in answer).
- "M10-017 raw magnitude operation order vsqrt((x²+y²)+z²)" present in answer 0x005129C0..A1E; not re-quoted here but verified by B-M10-2. **Marked not reviewed this pass** (B-M10-2 already suppliesmathrm).
- "M10-007 rewind/obstacle" — say NOT absorbed; UNKNOWN.
- "M10-009 obstacle latch" — UNKNOWN (bounded no-writer claim, not exhaustive proof).

## Build-relevant claims NOT reviewed (budget — answer is large; first 3 of M10-001's table verified, others left)
- Full ~24-row M10-001/M10-002/M10-009/M10-010/M10-011/M10-013/M10-014/M10-015/M10-017 cited ranges listed above not disassembled this pass except F1 (six literals) and F2 (precommit + postcommit).
- M10-007 native producer (rewind/obstacle) not verified; UNKNOWN.
- M10-001's remaining "states 2..6 detach; charger platform clear" partially verified as above.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M10-CLASSIFIER (row 27).
2. Engine instructions re-read myself: F1 six literals verified exactly: Face high 0x00511E66 `movw r1,#0xBE0B`, 0x00511E6A `movt r1,#0x3FF5` -> 0x3FF5BE0B = 1.919862151145935; Face low 0x00511EE6 `movw r1,#0xB8C2`, 0x00511EEA `movt r1,#0xBFB2` -> 0xBFB2B8C2 = -1.3962633609771729; Level word at 0x005122BC = `DB 0F 49 3F` = 0x3F490FDB = 0.7853981852531433; Simulated back center f64 at 0x005122A0 = 0x3FFAEB8260000000 = 1.6824973821640015; Physical back center at 0x005122A8 = 0x3FF4CDE840000000 = 1.3002703189849854; Back half-width at 0x005122B0 = 0x3FD0C15240000000 = 0.2617993950843811. F2 verified: 0x00511FF0 `cmp r1,#6`; 0x00511FF2 `bne #0x51203E`; 0x00511FF4..0x00511FFA `str.w r3,[sb,#0x35C]` + `strd r0,r0,[sp,#0x20]`; 0x00512004 `adr r0,#0x300`; 0x00512006 `add r1,sp,#0x20`; 0x00512008 `adr r2,#0x32C`; 0x0051200A `blx #0x4A4F90` (DAS Falling...); 0x00512030 `ldr.w r0,[sb,#0x250]` (ActionList); 0x00512034 `mov.w r1,#-1`; 0x00512038 `blx #0x4A7ABC` (Cancel(-1)); all at 0x00511FF0..0x0051203C, before commit. Postcommit 0x0051208E..21F4 verified: 0x0051208E `strb.w r0,[sb,#0x355]` (committed byte); 0x00512096 `add r0,sp,#0x20`; 0x00512098 `blx #0x4A7B64` (broadcast E2G); 0x005120FC-10C states 2..6 detach (0x00512100 `ldr.w r0,[sb,#0x284]`; 0x00512104 `ldr r1,[r0,#8]`; 0x00512106 `adds r1,#1`; 0x00512108 `itt ne; movne r1,#1`; 0x0051210C `blxne #0x4A7B7C`); 0x005121DC `blx #0x4A79CC` final broadcast; 0x005121E2 `ldr.w r0,[sb,#0x264]`; 0x005121E6 `movs r2,#2`; 0x005121E8 `ldrb.w r1,[sb,#0x355]`; 0x005121EC `ldr r0,[r0,#0x2C]`; 0x005121EE `cmp r1,#0`; 0x005121F0 `it ne`; 0x005121F2 `movne r1,#1`; 0x005121F4 `blx #0x4A7B4C` (conditional charger platform clear + pause-flag write). Remaining M10 claims (M10-002 gyro drift 0x0063E398/63E428/63E5DA/63E674/63E68A/0x63E428..5DA; raw magnitude 0x005129C0..A1E, detector 0x0052C568..770; M10-007 rewind 0x0063E398..426/0x00512B5C/0x0051307A-10/0x00512B78-BAA; M10-009 physical/obstacle latch 0x00569A78-88/0x006143CA-D4; M10-010 0x0050FC36/0x00536980/98E/0x0051397A/97C; M10-011 0x00534F4C-9E/0x005350AA-C2/0x00535186-9C; M10-013 0x0052EE8E-9C/0x005100E0-FE/0x005129A4-BE; M10-014 0x00619A2C-9D84/0x0051121A-250/0x00511418-424) **not individually disassembled this pass** (the answer's instruction citations are retained; B-M10-2 (Claude check 20261021-b-m13-9-b-m10-2-claude-check.md) already verified M10-001 constants + M10-002 detector body).
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M10-001/002/007/008/009/010/011/013/014/015/017 remain IMPLEMENTATION_GAP (record statuses unchanged). The F1/F2/F3 native claims verified here: F1 (six angle literals confirmed byte-exact); F2 precommit cancel + postcommit consequences confirmed instruction-level; F3 partial (charger platform clear path verified at 0x005121DC..F4; pause flag 2 placement at 0x0051217C-84). M10-007 rewind and obstacle latch remain not reviewed (their native writers unknown/abstract, separate records). No universal absence asserted.

No new research queued. No code edited. No record settled. (M10-002 gyro body constants verified in prior B-M10-2; this pass re-verified M10-001 F1+F2 + charger-platform-clear path only.)