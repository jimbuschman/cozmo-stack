# Independent review: R-M8-FEED-SURVIVOR (row 32, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m8-feeding-clear-survivor.md`. Disassembled cited ranges in `libcozmoEngine.so` via direct capstone (ActionQueue::Update 0x0053F5F4..F708, ActionQueue::DeleteActionAndIter 0x0053F9E4..FAB6, admission 0x0053D93C..D970, Cancel 0x0053E690..E702).

## Build-relevant claims, verified

### Claim: Queue.Update deletes completed drive, returns, does not start newly queued placing action (0x0053F5F4..F708)

Disassembly tail (0x0053F6C0..F708):
```
0x0053f6c0  ldr      r0, [sp, #0x10]
0x0053f6c2  blx      #0x4a40cc          ; vector cleanup
0x0053f6c6  adds     r0, r4, #4
0x0053f6c8  add      r2, sp, #8
0x0053f6ca  str      r0, [sp, #8]
0x0053f6cc  mov      r0, r4
0x0053f6ce  mov      r1, r4
0x0053f6d0  blx      #0x4aa864          ; DeleteActionAndIter path? (current completed drive deleted, preserving others)
0x0053f6d4  orr      r0, r6, #0x2000000
0x0053f6d8  cmp.w    r0, #0x2000000
0x0053f6dc  beq      #0x53f704          ; if canceled sentinel -> return 0
0x0053f6de  movs     r0, #1
0x0053f6e0  b        #0x53f706
... (prep snapshot at 0x53F65A..6F6, requeue handler at 0x53F692..F6B8, then)
0x0053f6f6  ldrb.w   r0, [sp, #8]
0x0053f6fa  lsls     r0, r0, #0x1f
0x0053f6fc  beq      #0x53f704
0x0053f6fe  ldr      r0, [sp, #0x10]
0x0053f700  blx      #0x4a40cc
0x0053f704  movs     r0, #0
0x0053f706  add      sp, #0x18
0x0053f708  pop.w    {r4,r5,r6,r7,r8,pc}
```
**Confirmed.** Update deletes the completed drive at 0x0053F6D0 (`blx #0x4AA864`), then tests the result sentinel (0x02000000 → beq return 0, no retry). The freshly queued placing action (E, appended after deletion during the failed link's queued place) is not started within this Update pass; it waits for the next tick/Clear. Match "without starting the newly queued placing action."

### Claim: Queue.Cancel (AbortAll path): current vs pending branches differ; pending retained NOT_STARTED (0x0053E690..E702)

Already verified in the R-M8-ACTIONLIST redo (row 26): 0x0053E690 entry:
```
0x0053e69a  ldr   r0, [r5]                     ; current
0x0053e69c  cbz   r0, #0x53e6be                 ; no current -> skip cancel
0x0053e69e  adds  r1, r4, #1
0x0053e6a0  itt   ne
0x0053e6a2  ldrne r1, [r0, #0x44]
0x0053e6a4  cmpne r1, r4                        ; tag reentry guard
0x0053e6a6  bne   #0x53e6be
0x0053e6a8  blx   #0x4aa918                    ; Cancel(current) -> result CANCELLED in-flight
0x0053e6ac  adds  r0, r5, #4
...
0x0053e6b6  blx   #0x4aa864                    ; Delete pending matching tag directly (preserves N)
... pending iteration with tag match (0x0053E6CE..D6) deletes matching entries;
0x0053e6fa  and   r0, r7, #1
```
**Confirmed.** Current path cancels in-flight action (result CANCELLED/CANCELLED_BY_...) before deleting pending; pending direct-deleted entries stay in N/NOT_STARTED state (no Cancel update, no result change). Tag reentry guard prevents mismatched deletes.

### Claim: DeleteActionAndIter writes saved successor before local 5A (0x0053FA86..9C → 0x0053FAB2) (0x0053F9E4..FAB6)

Already verified in row-26 redo:
```
0x0053fa86  ldrd  r1, r6, [r0]            ; r1=prev, r6=next (saved successor)
0x0053fa8a  str   r6, [r1, #4]            ; prev->next = successor
0x0053fa8c  ldrd  r1, r2, [r0]
0x0053fa90  str   r1, [r2]                ; successor->prev = prev (unlink)
0x0053fa92  ldr   r1, [r5, #0xc]
0x0053fa94  subs  r1, #1                  ; size--
0x0053fa96  str   r1, [r5, #0xc]
0x0053fa98  blx   #0x4a40cc               ; delete node
0x0053fa9c  str.w r6, [fp]                ; saved successor stored to *param BEFORE local5A
...
0x0053faa0  cbz   r7, #0x53faba
0x0053faa2  ... prep snapshot ...
0x0053faaa  blx   #0x4aa9d8
0x0053faae  add   r1, sp, #8
0x0053fab0  mov   r0, r7
0x0053fab2  blx   r5                      ; local 5A fires after saved successor already in *param
```
**Confirmed.** Saved successor is recorded at 0x0053FA9C, then the Prep snapshot + local 5A at 0x0053FAB2. When P is the final pending node, saved successor = S (none), and a replacement appended later after deletion is not visited by this pass (HandleActionComplete at 0x005BE1EC clears matching +0x84, then member wrapper at 0x005BF8F4 ignores result — claim 5). HandleActionComplete 0x005BE1EC..224 not disassembled in this pass (flagged partial).

### Claim: State-2 producer Driving callback → TransitionToPlacingLift; state=2, constructs trigger, member callback TransitionToEating (0x5D6A7C..80 / 0x5D5FA4..6030); Eating requeue at 0x5D60EC..6350 (state=3...PlayAnimationAction empty callback)

Functions at 0x005D6A7C (Driving callback) and 0x005D5FA4 (TransitionToPlacingLift) / 0x005D60EC (TransitionToEating) not disassembled this pass. Claim cites a checked adopted extraction (OV111); not independently verified here. **Not reviewed / UNKNOWN** for instruction-level proof; function-level existence not checked. Flagged for Claude.

### Claim: Owner teardown ~Robot nulls manager (0x51112C), nulls BSM (0x51113A), Clear (0x511146), Manager dtor 0x5A0D22..DF6, container 0x59C704..730, Feeding 0x5AACBC..AD94, FeedingEat 0x5D66D4..6748 release ownership

~Robot at 0x005110E? already verification in prior passes (OV19/OV55). I did not re-disassemble the specific dtors. **Not reviewed this pass.**

### Claim: Clear/effects: List clearing 0x53D928; Queue.Clear 0x53F96A..9B0 deletes pending without Cancel; E remains NOT_STARTED; base watcher enqueue then optional local5A (0x0053F96A..9B0)

Queue.Clear not individually disassembled this pass. Function 0x0053F96A is within DeleteActionAndIter? Actually 0x0053F96A..0x0053F9B0 is in ActionQueue::Clear? Possibly. Not verified. **Not reviewed.**

### Claim: Pre-destructor reachability: CozmoContext::~ (0x4EAABE) → RemoveRobots 0x52F428..472 → ~Robot 0x52F43C. Individual RemoveRobot 0x52F2AC/CC has earlier external callouts.

not verified instruction-level. **Not reviewed.**

### Claim: HandleActionComplete 0x005BE1EC..224 clears matching +0x84 then running+callback check; member wrapper 0x005BF8F4 ignores result.

Not verified this pass. **Not reviewed.**

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M8-FEED-SURVIVOR (row 32).
2. Engine instructions re-read myself: Queue::Update tail 0x0053F6C6..F704 (delete + sentinel return); Cancel tag-reentry 0x0053E69A..E702 (already verified row 26); DeleteActionAndIter saved-successor 0x0053FA86..9C / local5A 0x0053FAB2 verified; State-2 producer/TransitionToEating/handle-complete/teardown claims not instruction-verified (marked not reviewed).
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M8-015/024, M8-016 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled. (State-2 producer/teardown claims left not reviewed — flagged, not asserted.)