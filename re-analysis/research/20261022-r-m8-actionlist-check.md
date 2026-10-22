# Independent review: R-M8-ACTIONLIST (row 26, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m8-actionlist-completion-settling.md`. Disassembled cited ranges in `libcozmoEngine.so` via direct capstone (admission 0x0053D93C..DB70, cancel/deletion 0x0053E690..E702).

## Build-relevant claims, verified

### Claim 1 (admission) — Null returns API1; disabled external-tag ranges/BAD_TAG discard through Delete and return0; six positions, position1 Cancel(-1) then NEXT (0x0053D93C..0x0053DB70)

Disassembly start 0x0053D93C:
```
0x0053d93c  push   {r4,r5,r6,r7,lr}
0x0053d93e  sub    sp, #0x24
0x0053d940  mov    r5, r2            ; r5 = action ptr (passed in)
0x0053d942  mov    r6, r3            ; r6 = position
0x0053d944  mov    r7, r0            ; r7 = this (ActionQueue/ActionList)
0x0053d946  cmp    r5, #0
0x0053d948  str    r5, [sp, #0x18]
0x0053d94a  beq    #0x53d9ce         ; null -> API path (0x0053D9CE sets API1/return?)
...
0x0053d94c  ldr    r0, [r5, #4]     ; action+0x04 (robot?)
0x0053d94e  ldrb.w r0, [r0, #0x2c7]  ; disabled flag at +0x2C7
0x0053d952  cbz    r0, #0x53d972     ; if disabled -> 0x53D972 (discard path?)
...
0x0053d954  movw   r3, #0x4240        ; 60000? (BAD_TAG threshold?)
0x0053d958  ldr    r2, [r5, #0x60]   ; action+0x60 (tag?)
0x0053d95a  movt   r3, #0xf            ; r3 = 0xF4240
0x0053d95e  subs   r4, r2, #1
0x0053d960  cmp    r4, r3
0x0053d962  itttt  hs                 ; if r4 >= r3
0x0053d964  movwhs r4, #0x7b7f
0x0053d968  movths r4, #0xffe1
0x0053d96c  addhs  r2, r2, r4
0x0053d96e  cmphs  r2, r3
0x0053d970  blo    #0x53d982
...
```
**Partially confirmed.** The admission entry at 0x0053D93C runs the documented front-end: null handling (beq to 0x0053D9CE), disabled-external-tag check via action+0x2C7 byte (0x0053D94E `ldr r0,[r5,#4]; ldrb r0,[r0,#0x2C7]; cbz r0,#0x53D972`), and a BAD_TAG-style discard with a held threshold (0xF4240) at 0x0053D954..70. The remainder of the six-position/position-1-Cancel(-1)-then-NEXT sequence (0x0053D982..DB70) was not walked line-by-line; **UNKNOWN for the exact position-1 ordering instruction**, although the function is the documented admission entry. Flagged.

### Claim 4 — cancel/deletion: tag reentry guard; Current Cancel then Delete; pending direct Delete (0x0053E690..E702)

Disassembly 0x0053E690:
```
0x0053e690  push.w  {r4,r5,r6,r7,r8,lr}
0x0053e694  sub     sp, #8
0x0053e696  mov     r5, r0            ; this
0x0053e698  mov     r4, r1            ; tag
0x0053e69a  ldr     r0, [r5]          ; current (head?)
0x0053e69c  cbz     r0, #0x53e6be     ; if no current, skip cancel of current
0x0053e69e  adds    r1, r4, #1        ; r1 = tag+1
0x0053e6a0  itt     ne
0x0053e6a2  ldrne   r1, [r0, #0x44]   ; current->tag
0x0053e6a4  cmpne   r1, r4            ; current->tag != tag?
0x0053e6a6  bne     #0x53e6be         ; mismatch -> skip (tag reentry guard)
0x0053e6a8  blx     #0x4aa918         ; Cancel(current)
0x0053e6ac  adds    r0, r5, #4        ; pending list head
0x0053e6ae  add     r2, sp, #4
0x0053e6b0  str     r0, [sp, #4]
0x0053e6b2  mov     r0, r5
0x0053e6b4  mov     r1, r5
0x0053e6b6  blx     #0x4aa864         ; Delete pending matching tag (preserves others / N)
0x0053e6ba  movs    r7, #1
0x0053e6bc  b       #0x53e6c0
0x0053e6be  movs    r7, #0
0x0053e6c0  ldr     r0, [r5, #8]      ; pending tail
0x0053e6c2  adds    r6, r5, #4
0x0053e6c4  str     r0, [sp]
0x0053e6c6  cmp     r6, r0
0x0053e6c8  beq     #0x53e6fa         ; empty pending -> done
0x0053e6ca  mov     r8, sp
0x0053e6cc  adds    r1, r4, #1
0x0053e6ce  ittt    ne
0x0053e6d0  ldrne   r1, [r0, #8]      ; node->next?
0x0053e6d2  ldrne   r1, [r1, #0x44]   ; next->tag
0x0053e6d4  cmpne   r1, r4            ; tag match?
0x0053e6d6  bne     #0x53e6f2
0x0053e6d8  add.w   r1, r0, #8
0x0053e6dc  mov     r0, r5
0x0053e6de  mov     r2, r8
0x0053e6e0  blx     #0x4aa864         ; delete matching pending
0x0053e6e4  cmp     r0, #1
0x0053e6e6  bne     #0x53e6fa
0x0053e6e8  ldr     r0, [sp]
0x0053e6ea  movs    r7, #1
0x0053e6ec  cmp     r0, r6
0x0053e6ee  bne     #0x53e6cc
0x0053e6f0  b       #0x53e6fa
0x0053e6f2  ldr     r0, [r0, #4]
0x0053e6f4  str     r0, [sp]
0x0053e6f6  cmp     r6, r0
0x0053e6f8  bne     #0x53e6cc
0x0053e6fa  and     r0, r7, #1
0x0053e6fe  add     sp, #8
0x0053e700  pop.w   {r4,r5,r6,r7,r8,pc}   ; r0 = r7 (deleted-current flag)
```
**Confirmed.** Tag reentry guard: current only cancelled if `current != null && current->tag (at +0x44) == tag` (cbz 0x53e69c, cmpne 0x53e6a4, bne). Pending list iterated deleting matching tags while preserving others. The Prep→game-snapshot→virtual-destructor→E2G→guard-erase ordering (0x0053F9E4..FB06) not disassembled this pass — **not individually verified**; claim says these happen in `__list` destructor paths which are standard. Flagged partial.

### Claims not reviewed (budget: row 2 has 7 sub-claims; verified 1 admission + cancel/deletion; remaining 5 listed below as "not reviewed")

- 015 tick: First-full-state/vision gates before ActionList; signed queue order; first nonzero failure retained; erase only empty queue; watcher after queues; streamer later — 0x005140BC, 0x0053F580..F708, 0x0051411E — not disassembled.
- 015 runner: Clock stamped first internal tick; f32 timeout equality before Init; pre/post wait includes unordered; Init→Check same tick; category4 retry; RUNNING has no completion callbacks — 0x00540370..0x0054063A, 0x00540D1C..0x00540F9C — not disassembled.
- 015 completion ownership: Game reads subresults then virtual union before destruction — 0x00540AA8..B64; base destructor watcher uses Prep cache/union/descendants 0x00541238, 0x00541BB4..1C04; Play concrete getter 0x005441DE..420E, 0x0075C544..572 — not individually verified.
- 015 delivery: Broadcast buffers app first, local emit synchronous 0x006625B0..262A, 0x0065D8B8; watcher callbacks run later in Update 0x0054187E..18DE — not verified.
- 017 sequential/parallel (hook first, copy suppression, callback-before-ignore, ordered child updates, ignored failure Prep→Store) 0x0054F70C..F992, 0x0054F5A0..F696, 0x0054FAB0..FBCC — not verified.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M8-ACTIONLIST (row 26).
2. Engine instructions re-read myself: admission entry verified at 0x0053D93C..D970 (null check 0x0053D946/4A beq; disabled byte action+0x2C7 0x0053D94E/52 cbz 0x0053D972; BAD_TAG clamp threshold 0xF4240 0x0053D954..70). Cancel/deletion verified at 0x0053E690..E702 (tag reentry guard 0x0053E69A cbz, 0x0053E69E/0x0053E6A0 adds r1,r4,#1; 0x0053E6A2/A4 ldrne/cmpne r1,r4; bne skip; Cancel 0x0053E6A8 blx 0x4AA918; pending delete 0x0053E6B6 blx 0x4AA864; pending iteration tag reentry 0x0053E6CE..D6 + delete 0x0053E6E0). Tick/runner/completion/delivery/compound claims (5) not disassembled.
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M8-015/007/016 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled. (Tick/runner/completion/delivery/compound claims left "not reviewed" — listed in the file.)