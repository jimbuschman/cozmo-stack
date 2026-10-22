# Independent review: R-M10-OBSTACLE-ALIAS (row 35, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m10-obstacle-flag-alias.md`. Disassembled cited ranges in `libcozmoEngine.so` via direct capstone (Robot+AI publication 0x0050FD80..FDA8; AI ctor 0x00569A78..88; predicate 0x006143CA..D0; slot clear 0x0051126A..78, 0x005114B8..CC).

## Build-relevant claims, verified

### A1 — AI is separately allocated 0x34-byte object, published to Robot+0x264 only after its constructor returns (0x0050FD80..FDA8; the answer cites 50FD94..FDA4, 50FF18 ctor return etc.)

Disassembly 0x0050FD80..FDA8:
```
0x0050fd80  blx   #0x4a42a0            ; operator new (0x34 bytes, earlier `movs r0,#0x34` at 0x50FD94)
0x0050fd84  mov   r6, r0               ; r6 = new AI* (allocated, pre-constructor)
0x0050fd86  ldr   r2, [r5]             ; ...
0x0050fd88  mov   r0, r6               ; this = new AI
0x0050fd8a  mov   r1, r5               ; r1 = robot (r5)
0x0050fd8c  blx   #0x4a76f0            ; AI constructor(AI*, robot) — runs before publication
0x0050fd90  str.w r6, [r5, #0x260]     ; (MemoryMap/aux object at +0x260, size 0x34? actually earlier 0x50FD80 alloc maybe 0x28; line not needed)
0x0050fd94  movs  r0, #0x34            ; (size 0x34 for the AI object — a later alloc; line number cited by answer)
0x0050fd96  blx   #0x4a42a0            ; operator new (0x34)
0x0050fd9a  mov   r4, r0               ; r4 = new AI* (second alloc for the publish)
0x0050fd9c  mov   r0, r4
0x0050fd9e  mov   r1, r5
0x0050fda0  blx   #0x4a76fc            ; AI constructor again? (ctor code r1=robot)
0x0050fda4  str.w r4, [r5, #0x264]    ; Robot+0x264 = AI (published AFTER ctor returns — A1/A11 publication order)
```
Hmm — the above shows two allocs of 0x28? and 0x34. Actually line 0x0050FD80 region isn't fully captured; 0x0050FD94 "movs r0,#0x34" appears at 0x0050FD94 (after `str.w r6,[r5,#0x260]`). So one object (likely the Vision? no, MemoryMap maybe) at +0x260 size 0x?? and AI at +0x264 size 0x34. The "str.w r4,[r5,#0x264] at 0x0050FDA4" confirms AI published to Robot+0x264 AFTER its constructor (0x0050FDA0 blx AI ctor) returns. **Confirmed** for A1 (alloc 0x34 at 0x50FD94, publish at 0x50FDA4) and A11 (clear before destructor): Robot clears slot+0x264 (str.w r5,#0 = 0) before deleting the AI.

### A1/A11 — clear of Robot+0x264 before AI destructor

Disassembly 0x0051126A..78 and 0x005114B8..CC:
```
0x0051126a  ldr.w  r0, [r4, #0x264]       ; AI pointer (if any)
0x0051126e  movs   r5, #0
0x00511270  str.w  r5, [r4, #0x264]       ; clear slot -> 0 BEFORE delete
0x00511274  cbz    r0, #0x51127e          ; if null, skip delete
0x00511276  blx    #0x4a7930              ; delete AI (dtor)
0x0051127a  blx    #0x4a40cc              ; delete memory
0x0051127e  ...
```
and
```
0x005114b8  ldr.w  r0, [r4, #0x264]
0x005114bc  movs   r5, #0
0x005114be  str.w  r5, [r4, #0x264]
0x005114c2  cbz    r0, #0x5114cc
0x005114c4  blx    #0x4a7930
0x005114c8  blx    #0x4a40cc
0x005114cc  ldr.w  r0, [r4, #0x260]
0x005114d0  str.w  r5, [r4, #0x260]
```
**Confirmed.** Robot clears the AI slot (0x264 → 0) before destroying the AI (dtor 0x4A7930) and frees it; the second site (teardown) also clears +0x260/+0x264. Matches claim A11.

### A6 — AI constructor default (AI byte+4 = 0) and predicate (no sensor/map lookup) at 0x00569A78..88 / 0x006143CA..D4

AI constructor (0x00569A78..88):
```
0x00569a78  push {r4,r5,r6,r7,lr}
0x00569a7a  sub sp, #4
0x00569a7c  mov  r4, r0                ; this = AI
0x00569a7e  movs r0, #0
0x00569a80  strb r0, [r4, #4]          ; AI byte+4 = 0  (default)
0x00569a82  mov r5, r1                 ; r5 = robot
0x00569a84  movs r0, #0x18
0x00569a86  str  r5, [r4]              ; AI+0 = AI's parent Robot pointer (this IS the AI+0 == r4[0])
0x00569a88  blx #0x4a42a0             ; allocate inner 0x18 object at AI...
```
Predicate getter at 0x006143CA (no sensor/map lookup):
```
0x006143ca  ldr.w r0, [r1, #0x264]    ; r0 = robot.AI
0x006143ce  ldrb  r0, [r0, #4]        ; r0 = AI byte+4
0x006143d0  bx    lr                  ; return
```
**Confirmed.** Ctor byte+4 starts 0 and the predicate is a one-instruction load (robot.AI → byte+4), no sensor/map lookups. ✓

### A2-A12 child alias publication (AI +2C etc., retained Robot refs in Whiteboard/SevereNeeds/ObjectInteractionInfoCache; Update/init/relocation/child-slot clearing)

Answer lists these with ranges. I verified A1 only for Robot+0x264 publication ordering (0x50FDA0 ctor then 0x50FDA4 str publish), and A11 slot-clear before delete (0x51126A..78, 0x5114B8..CC). The full A2-A10/A12 alias census (ObjectInteractionInfoCache retaining Robot in fp→cache+0; Whiteboard+4 store and its consumer boundary; SevereNeeds+4 store; Init's Whiteboard/SevereNeeds/Workout init parameter passing; Update's analyzer + helper receives Robot, Whiteboard/SevereNeeds/tracker updates; delocalized relocation stubs A9 at 510D6E/72 and 512464/6C; AI destructor A12 clearing child slots +30,+2C,+28,+24,+20,+1C,+18,+14,+10,+C,+8 before deleting) are described but **not instruction-verified** in this pass (cite them as not reviewed: the answer cites specific ranges, A2 0x569ABE..AC0/AD0/AD2/AD6, A3 0x569B38..3E→56F60C, etc.; I did not disassemble each). Also F4 (direct Robot+0x264 loads count 154) and cache/helper tail callees remain UNKNOWN interprocedural alias closures. Per the answer, "No universal no-writer census is established".

## Build-relevant claims NOT reviewed (budget — listed as "not reviewed"; most cited ranges beyond A1/A6/A11 were not disassembled)

A2 (0x569ABE..AC0/AD0..AD2/AD6), A3 (0x569B38..3E, 0x56F60C), A4 (0x569AD0..AD6, 0x56A272), A5 (0x569B38..3E, 0x5729FE), A7 (0x569F24..F46?), A8 (0x56DE20..34, 0x56DE36..70), A9/A10 (0x510D6E..72, 0x512464..6C, 0x56A686..69C, 0x56AA28..38), A12 (constructor cleanup 0x569B46..CB0, virtual/text calls 0x569CC2/9D1C/9D8A/9D4E/9D66), A11 Clear slot+0x260 boundary, child visibility/class census, escaped alias/root-parent coupling (manager refs etc.).

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M10-OBSTACLE-ALIAS (row 35).
2. Engine instructions re-read myself:
   - AI publication+clear: `0x0050FD80` alloc 0x34 (0x0050FD94 `movs r0,#0x34`, 0x50FD96 `blx #0x4A42A0`), `mov r4,r0`, ctor 0x0050FDA0 `blx #0x4A76FC`, publish `0x0050FDA4 str.w r4,[r5,#0x264]`; clear 0x0051126A `ldr.w r0,[r4,#0x264]`, 0x0051126E `movs r5,#0`, 0x00511270 `str.w r5,[r4,#0x264]`, 0x00511274 `cbz r0,#0x51127E`, 0x00511276 `blx #0x4A7930` delete, 0x0051127A `blx #0x4A40CC`. Same clear shape at 0x005114B8 (`ldr.w r0,[r4,#0x264]`, 0x005114BC `movs r5,#0`, 0x005114BE `str.w r5,[r4,#0x264]`, 0x005114C2 `cbz`, 0x005114C4 `blx #0x4A7930`, 0x005114C8 `blx #0x4A40CC`, then `0x005114CC ldr.w r0,[r4,#0x260]`, 0x005114D0 `str.w r5,[r4,#0x260]`).
   - AI ctor byte+4 default + predicate: ctor 0x00569A7E `movs r0,#0`; 0x00569A80 `strb r0,[r4,#4]`; 0x00569A82 `mov r5,r1`; 0x00569A84 `movs r0,#0x18`; 0x00569A86 `str r5,[r4]`. Predicate 0x006143CA `ldr.w r0,[r1,#0x264]`; 0x006143CE `ldrb r0,[r0,#4]`; 0x006143D0 `bx lr`.
3. Hooks/seams added: none. (The C# default read at OffTreads is pre-existing, not added by this review.)
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M10-009 retains IMPLEMENTATION_GAP; the answer's own stated caveat (escaped-alias/transitive-writer/interprocedural closure remain UNKNOWN) retained; I did not over-claim absence beyond those instructions verified.

No new research queued. No code edited. No record settled. (A2-A12 child-alias census and the full Robot+0x264 read-site census (OD3's 154 syntactic matches) left not reviewed; see answer body.)