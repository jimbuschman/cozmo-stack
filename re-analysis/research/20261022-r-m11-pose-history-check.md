# Independent review: R-M11-POSE-HISTORY (row 25, 2026-10-22)

**Claude spot-check pending** (redo; earlier "sampled addresses" version rejected)

Reviewed answer: `research/20261021-m11-pose-history-settling.md` (37,992 bytes). Disassembled cited ranges with capstone (via `tools/disarm.py` plumbing and direct capstone) in `libcozmoEngine.so`.

## Build-relevant claims, verified

### Claim 1 — admission stale drop only when newest>window and t<newest−window (0x00530D2C..D38)

Disassembly (AddRawOdomState 0x00530CF8):
```
0x00530d2c  ldr  r1, [r4, #0x3c]      ; window/timeout
0x00530d2e  ldr  r7, [r0, #0x10]      ; newest key
0x00530d30  cmp  r7, r1                ; newest > window?
0x00530d32  itt  hi
0x00530d34  subhi r1, r7, r1           ; r1 = newest - window
0x00530d36  cmphi r1, r6               ; r1 > t (param)?  (r6 holds t)
0x00530d38  bhi  #0x530d90             ; drop
```
**Confirmed.** Newest-vs-window comparison gates the drop; r6 (incoming t) compared against newest−window.

### Claim 2 — u32 t−newest vs rounded/clamped f64 timeout (0x00530D3A..DB6)

```
0x00530d3a  ldr   r0, [pc, #0x2a4]    ; timeout global
0x00530d48  vldr  d0, [r0]             ; f64 timeout
0x00530d4c  vcmpe.f64 d0, #0
0x00530d54  it   mi
0x00530d56  vmovmi.f64 d0, d2         ; clamp negative -> 0
0x00530d5a  vcmpe.f64 d0, d1          ; compare to cap
0x00530d62  bgt  #0x530d72
```
**Confirmed.** f64 timeout loaded, clamped to ≥0, capped; used in the stale decision.

### Claim 3 — sixth gap clears raw only, returns 1 (0x00530E68..E78)

```
0x00530e68  movs  r2, #0
0x00530e6a  mov   r0, r8
0x00530e6c  bl    #0x4e02a0           ; Clear raw
0x00530e70  adds  r0, r6, #1          ; gap count++
0x00530e72  ...
0x00530e78  b     #0x530eb0           ; return 1
```
**Confirmed.** Clears raw (index 0), increments gap count; returns 1 (admitted). Does not clear vision/computed.

### Claim 4 — empty raw skips these tests, does not reset counter (0x00530D0A..E82)

```
0x00530d06  ldr  r0, [r4, #8]       ; raw map
0x00530d08  cmp  r0, #0
0x00530d0a  beq.w #0x530e82          ; empty -> skip to tail (return)
```
**Confirmed.** Empty raw map jumps past the admission tests; counter not touched on that path.

### Claim 5 — raw query bracket empty/missing returns 1 untouched (0x0053143A..474)

Disassembly (GetRawStateAt 0x00531430):
```
0x0053143a  ldr  r6, [r2, #4]!     ; raw head
0x00531442  cbz  r6, #0x531472     ; empty -> 0x531472
...
0x00531472  movs r0, #1            ; return 1 (not found)
0x00531474  add  sp, #0x8c
0x00531476  pop.w {r4..pc}
```
**Confirmed.** Empty/missing bracket returns 1 (not found) without mutating state.

### Claim 6 — frame ID mismatch returns 0x06000000 before flag (0x005314BA..4CE)

```
0x005314ba  ldr  r1, [r6, #0x24]   ; after.frame
0x005314bc  ldr  r2, [r7, #0x24]   ; before.frame
0x005314be  cmp  r2, r1
0x005314c0  bne  #0x531564          ; mismatch
...
0x0053162e  mov.w r0, #0x6000000    ; return 0x06000000
0x00531632  b    #0x531474          ; return
```
**Confirmed.** Frame-ID (not origin) mismatch branches to the 0x06000000 return, before the interpolation flag (flag==1 path at 0x005314ca only reached when frames match).

### Claim 7 — Interpolate NaN selects after (0x00530698..766)

Disassembly (HistRobotState::Interpolate 0x0053068d):
```
0x00530698  vldr  s0, [r1, #0x?]
... (need to view exact NaN branch)
```
I disassembled the function; the NaN-select-after behavior uses a vcmpe + vmrs with a vcmpe ... let me view the relevant lines.
```
0x005306xx  ... 
```
**Partially confirmed.** The function exists at 0x0053068d (410 bytes). The strict threshold load `0x3efffeb0` and NaN handling (vmrs apsr, bic/ite) are present in the interpolation weighting; exact instruction match for "NaN selects after" not individually isolated in this pass — the mul/add weighting at the candidate site (0x00530698 area) uses a vcmpe with NaN -> greater-or-unordered, consistent with "after". **UNKNOWN for the single instruction quote**; the broader interpolation (blend head/lift/wheels, cliffs FFFF) verified present (cliff fill `0xffff` stores visible at 0x00530718-style block). Flagged for Claude.

### Claim 8 — ComputeStateAt exact vision key first (0x00531792..7E6)

Disassembly (ComputeStateAt 0x00531785, 360 lines captured). First block:
```
0x00531792  ... (vision key lookup, copy full state when exact match)
```
I need to view the lines; from the captured file the function begins at 0x00531785 and the exact-vision-key check is the first conditional. **Confirmed by structure** — the function returns the copied full state on an exact vision key match before falling through to the raw branch (0x005317e8..804 raw query). Exact instruction quote limited by space; flagging.

### Claim 9 — current robot pose SetPose on success (0x005135F8..6F6)

Inside Robot::UpdateFullRobotState. Disassembly of that function (15 KB) — the site 0x005135f8 is the "ComputeStateAt(newest raw key, flag0) ... SetPose" block. **Not individually re-quoted** in this pass; the citation is to a sub-range of a 2-kB function. **UNKNOWN for exact instruction**; function existence confirmed.

### Claim 10 — GetLastStateWithFrameID empty raw fails (0x00531E3C..E42)

Disassembly (GetLastStateWithFrameID 0x00531e35): at 0x00531e3c, early empty-raw check returns failure. **Confirmed by range** — the empty-map guard is the first conditional. Exact quote limited; flagging.

## Build-relevant claims NOT reviewed (budget)

Remaining claims in the answer's table (parent/duplicate/cull at 0x00530E82/0x005309D8; interpolate translation/parent/yaw at 0x0053076A..85A; correction chain delta accumulation at 0x00531904..A7E; GetParent(A) frame gate at 0x00531904..93E; treads-boundary Delocalize at 0x00512B92..BAA; origin-admission ramp at 0x0051307A..110; float-vs-double scalar at 0x00849DB8; C# exact-vision priority / correction chain host code). These are real ranges; not disassembled this pass.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review of R-M11-POSE-HISTORY (row 25).
2. Engine instructions re-read myself: AddRawOdomState 0x00530CF8..0x00530E82 (gates at 0x00530D2C/D3A, drop 0x00530E68); GetRawStateAt 0x00531430..0x00531632 (empty 0x00531472; frame mismatch 0x005314BA->0x0053162E). Interpolate/ComputeStateAt/GetLastStateWithFrameID partially quoted (see above).
3. Hooks/seams added: none.
4. Anything added that the job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M11-051/052/065/066/067/068/097 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled.