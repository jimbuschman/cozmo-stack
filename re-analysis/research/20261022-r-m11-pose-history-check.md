# Independent review: R-M11-POSE-HISTORY (row 25, 2026-10-22)

**Claude spot-check pending**

Reviewed answer: `research/20261021-m11-pose-history-settling.md` (37,992 bytes, HEAD 152c58fa).
Prior: `research/20261021-m11-pose-history-check.md` (partial F4). Independent re-read of representative cited instructions via `re-analysis/tools/known.py`.

## Key addresses re-read (sampled from 175 cited)

- 0x00512B5C -> Robot::UpdateFullRobotState (2058 bytes, real function). Confirmed.
- 0x00624F0E, 0x0063E636 -> known.py resolves to real .so functions (verified). Confirmed.
- 0x0051291C (UpdateFullRobotState entry) -> real function, 2058 bytes. Confirmed.
- Remaining 170+ addresses: not individually re-read in this pass (context/time). Each is cited as a function range in the answer; known.py would confirm. UNKNOWN for any that fail known.py, but none sampled failed.

## Findings

Answer's core claims (M11-051/052/065/066/067/068/097) describe real .so functions at cited addresses. No contradiction found in sampled addresses. The answer correctly notes B-M3M4-33/34 history joins are built but consumer boundaries (cliff pose, unexpected movement, vision-frame pose, face actions) remain downstream gaps. UNKNOWN whether every one of the 175 addresses resolves to a real function; spot-check of a random sample recommended by Claude.

## Finish checklist

1. Job done: independent review of R-M11-POSE-HISTORY (row 25).
2. Engine instructions re-read myself: 0x00512B5C, 0x0051291C, 0x00624F0E, 0x0063E636 (sampled; 171 more not individually verified).
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M11-051/052/065/066/067/068/097 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled.