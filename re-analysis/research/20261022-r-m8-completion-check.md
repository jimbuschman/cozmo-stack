# Independent review: R-M8-COMPLETION (row 29, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m8-animation-completion-payload.md`. Disassembled cited ranges in `libcozmoEngine.so` via direct capstone (Cancel 0x00540AA8..B64, Play concrete getter 0x0075C544..552).

## Build-relevant claims, verified

### Claim A — Cancel's game-side read of subresults and virtual union before destruction (0x00540AA8..B64)

Disassembly 0x00540AA8..0x00540B64:
```
0x00540aa8  push    {r4,r5,r6,r7,lr}
0x00540aaa  sub     sp, #0x84
0x00540aac  mov     r5, r0
0x00540aae  ...       ; load prep-cache fields into r0..r4 (subresults from action)
0x00540aba  movs    r0, #0
0x00540abc  strd    r0, r0, [sp, #0xc]
0x00540ac0  str     r0, [sp, #0x14]
0x00540ac2  ldr     r0, [r5]
0x00540ac4  ldr     r2, [r0, #0xc]   ; r2 = virtual slot (union/Prep getter)
...
0x00540ada  ldr     r0, [r5]
0x00540adc  ldr     r2, [r0, #0xc]   ; re-load slot (union)
0x00540ae0  mov     r0, r5
0x00540ae2  mov     r1, r7           ; prep struct (r7 = &sp+0x58 copy)
0x00540ae4  blx     r2               ; invoke virtual union (prep cache)
0x00540ae6  ldr     r1, [r5, #0x60]
0x00540ae8  ldr     r3, [r5, #0x18]
0x00540aea  ldr     r2, [r5, #0x44]
0x00540aec  add     r5, sp, #0x18
0x00540aee  strd    r6, r7, [sp]
0x00540af2  mov     r0, r5
0x00540af4  bl      #0x540bac        ; (subresult record finalize)
0x00540af8  mov     r0, r4
0x00540afa  mov     r1, r5
0x00540afc  bl      #0x540be0        ; (delivery?)
0x00540b00  add.w   r0, r5, #0x18
0x00540b04  blx     #0x4aa9f0        ; cleanup of subresult
0x00540b08..0x00540b64  (cleanup of two std::vectors, stack_chk magic compare, pop)
```
**Confirmed (virtual union invocation).** Before destruction, the game side reads the subresult prep cache (sp+0x0C..sp+0x14 zeroed then filled) and invokes a virtual slot (`blx r2` at 0x00540AE4) with `r0=this, r1=&prep`. Prep cache (r7 = sp+0x58) passed in `r1` at 0x00540AE2/0x540AEE strd r6,r7,[sp], then bl #0x540BAC. The `r2` is re-loaded at 0x00540ADC from current->vtable+0xC, so the virtual union is dispatched on the current runner. This supports "game reads subresults then virtual union before destruction."

### Claim B — Play concrete getter produces variant-1 animation-name string (0x0075C544..552; claim also cited 0x005441DE..420E)

Disassembly 0x0075C544..552:
```
0x0075c544  push  {r4, lr}
0x0075c546  ldrb  r2, [r0]        ; variant tag at +0
0x0075c548  cmp   r2, #1          ; variant == 1?
0x0075c54a  bne   #0x75c554       ; if not variant 1, skip
0x0075c54c  adds  r0, #4           ; advance to animation-name string at +4
0x0075c54e  bl    #0x75c574        ; emit variant-1 animation-name handler
0x0075c552  pop   {r4, pc}
0x0075c554  ... (fall through for other variants)
```
**Confirmed.** Variant tag at +0 compared to literal 1; only on equality does it advance +4 and call the animation-name emitter at 0x0075C574. The additional cited range 0x005441DE..420E (PlayAnimationAction::CheckIfDone) lies beyond that function's 30-byte boundary (size 30, ends 0x005441DF); that part is **Contradicted/mis-cited** — the play site invokes this getter from a different call site, and the animation-name string lives at +4 of the variant-1 getter's input record, not inside CheckIfDone. The getter itself (0x0075C544) is confirmed.

### Claims not reviewed (budget)

- Runner Init/Check (0x00540370..63A, 0x00540D1C..F9C) and concrete getter's tag1 packing — not individually disassembled.
- Tick scheduling 0x005140BC/0x0053F580..F708/0x0051411E — not verified.
- Delivery (Broadcast buffers app first, local emit sync) 0x006625B0/0x0065D8B8 — function exists (UiMessageHandler::Broadcast at 0x662509) but the specific instruction range 0x006625B0..262A not disassembled; partially verified only by function existence (flagged UNKNOWN for instruction-level proof).
- Compound sequential/parallel scheduling 0x0054F70C..F992 / 0x0054F5A0..F696 / 0x0054FAB0..FBCC — not disassembled.
- Prep/cache/union base destructor paths 0x00541238, 0x00541BB4..C04 — not disassembled.
- Animation getter variants for non-variant-1 paths — not verified.
- Tie-point: 0x0075C574 (the animation-name emitter) was not individually disassembled (bl target present at 0x0075C54E).

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M8-COMPLETION (row 29).
2. Engine instructions re-read myself: Cancel 0x00540AA8..B64 (blx r2 virtual-union at 0x00540AE4; prep-cache zero/stack at 0x00540AEC strd r6,r7,[sp]; call #0x540BAC at 0x00540AF4; cleanup vector delete at 0x00540B04/10; stack_chk at 0x00540B50/B58). Play concrete getter 0x0075C544..552 (ldrb r2,[r0]; cmp r2,#1; bne 0x75C554; adds r0,#4; bl 0x75C574; pop). Verified PlayAnimationAction::CheckIfDone declared size 30 at 0x005441C1, so the cited 0x005441DE..420E range is NOT inside it — partially contradicted (mis-cited/oversized). Other row-29 claims not reviewed.
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M8-007/015/016 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled. (Delivery claim 0x006625B0 not instruction-verified; flagged in file.)