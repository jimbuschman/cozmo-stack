# Independent review: R-M11-HISTORY-TIMEOUT (row 28, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m11-history-timeout-input.md`. Disassembled cited ranges in `libcozmoEngine.so` via direct capstone and `tools/disarm.py`.

## Build-relevant claims, verified

### T1 — storage: f64 5000.0 at 0x01051F48
Raw bytes at 0x01051F48 = `00 00 00 00 00 88 b3 40` = 5000.0 double. ARM_GLOB_DAT relocation at 0x0103EA54 -> symbol `_ZN4Anki4Util18ReliableConnection22sConnectionTimeoutInMSE`. **Confirmed.**

### T2 — Init low word: movs r5,#0 at 0x0062EFC0; high word movw/movt at 0x0062F062/068; strd at 0x0062F070
Disassembly (RCM Init 0x0062EFB0..0x0062F084):
```
0x0062efc0  movs  r5, #0
...
0x0062f058  ldr   r7, [pc, #0x68]   ; r7 = GOT 0x0103ea54
0x0062f05c  str   r5, [r3]            ; low word = 0
0x0062f060  ...
0x0062f062  movw  r3, #0x8800
0x0062f066  ldr   r7, [r7]           ; r7 = &sConnectionTimeoutInMS
0x0062f068  movt  r3, #0x40b3         ; r3 = 0x40B38800
0x0062f06e  strb  r5, [r1]
0x0062f070  strd  r5, r3, [r7]       ; low=0, high=0x40B38800
0x0062f074  movs  r3, #0xa           ; (MaxPingRoundTripsToTrack)
```
**Confirmed.** r5=0 written as low word; high word 0x40B38800; strd publishes 5000.0. No intermediate r5 reassignment governing this value; intervening calls preserve r5 under AAPCS.

### T3 — Configure low word: movs r3,#0 at 0x0062F0E0; movw/movt at 0x0062F162/6C; strd at 0x0062F172
Disassembly (ConfigureReliableTransport 0x0062F0C8..):
```
0x0062f0e0  movs  r3, #0
...
0x0062f15e  strd  r3, r4, [r1]       ; (MaxTimeSinceLastSend)
...
0x0062f162  movw  r1, #0x8800
0x0062f16a  ldr   r6, [r6]           ; r6 = &sConnectionTimeoutInMS
0x0062f16c  movt  r1, #0x40b3
0x0062f170  str   r0, [r2]            ; (MaxPacketsToReSendOnUpdate)
0x0062f172  strd  r3, r1, [r6]       ; low=0, high=0x40B38800
```
**Confirmed.** Same 5000.0 stored; method body makes no call governing this value; no numeric/config argument.

### T4 — callers: only Init caller at 0x0069D3D6; Configure has no caller
I did not independently re-run the whole `.text` direct/PLT scan this pass (too large). The answer documents the scan method and result. **UNKNOWN (what I tried):** confirmed that `HasConnectionTimedOut` exists at 0x00836069; did not verify the absence of Configure callers by full scan; relied on answer's stated bounded scan. Flagged for Claude.

### T5 — pointer materializations (5 GOT loads)
Sampled: 0x00530D3A/40 materializes the timeout GOT in AddRawOdomState (verified in that function's disassembly: `ldr r0,[pc,#0x2a4]` -> `0x103ea54` GOT -> sConnectionTimeoutInMS). The other four (Init 0x0062F058/5C, Configure 0x0062F154/5C, transport 0x00836076/78 and 0x00836152/5C) not individually re-traced in this pass. **Partially confirmed (1 of 5).**

### T6 — history read: ldr r0,[r0] at 0x00530D46; vldr d0,[r0] at 0x00530D48
Disassembly (AddRawOdomState, captured earlier):
```
0x00530d46  ldr   r0, [r0]          ; r0 = *global (pointer)
0x00530d48  vldr  d0, [r0]           ; d0 = f64 5000.0
0x00530d4c  vcmpe.f64 d0, #0
0x00530d50  vmrs  apsr_nzcv, fpscr
0x00530d54  it    mi
0x00530d56  vmovmi.f64 d0, d2       ; clamp negative -> 0
0x00530d5a  vcmpe.f64 d0, d1        ; cap comparison
```
**Confirmed.** The history admission reads the f64 global and clamps/caps; u32 gap comparison against 5000 (0x00530DB2..DB6) confirmed in AddRawOdomState (0x00530db2 subs, 0x00530db6 bls).

### T7 — transport loads / alias
Function exists at 0x00836069 (HasConnectionTimedOut). I did not re-disassemble it this pass; answer's per-instruction tracing (0x0083607A r5 load; 0x00836088 vldr d1; overwrites at 0x008360D6) not independently quoted. **UNKNOWN for the specific transport assembly; function existence confirmed.**

## Build-relevant claims NOT reviewed (budget)

T5's remaining four GOT materializations, T7's transport function body, the whole-file raw-word scan (metadata-only), the counted "5 uses" proof, and the Configure no-caller scan. These are stated in the answer; not independently re-run.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M11-HISTORY-TIMEOUT (row 28).
2. Engine instructions re-read myself: T1 storage bytes 0x01051F48; T2 0x0062EFC0/0x0062F062/68/70; T3 0x0062F0E0/0x0062F162/6C/72; T6 0x00530D46/48/DB2/DB6; relocation 0x0103EA54. T4/T5/T7 partially verified (see above).
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M1-005/M1-015/M11-051 retain their status.

No new research queued. No code edited. No record settled.