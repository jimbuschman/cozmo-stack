# Independent review: R-M11-FRAME-POSE (row 30, 2026-10-22)

**Claude spot-check pending**

Reviewed answer: `research/20261021-m11-frame-pose-assembly.md` (35,125 bytes). Independent re-read of representative cited instructions via known.py.

## Key addresses re-read (sampled from 77 cited)

- 0x00512B9C -> inside Robot::UpdateFullRobotState (real function). Confirmed.
- 0x00513AB0, 0x00531784 -> resolve to real .so functions. Confirmed.
- Remaining 74 addresses: not individually verified in this pass.

## Findings

Answer's caller/Set/LUT/IMU review cites real .so functions at sampled addresses. No contradiction found. Full carrier/metadata/owners remain open per answer. Record M11-097 and related remain IMPLEMENTATION_GAP.

## Finish checklist

1. Job done: independent review of R-M11-FRAME-POSE (row 30).
2. Engine instructions re-read myself: 0x00512B9C, 0x00513AB0, 0x00531784 (sampled; 74 more not individually verified).
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M11-097 remains IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled.