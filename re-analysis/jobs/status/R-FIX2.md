CLAIMED claude-sonnet 2026-10-03

Job R-FIX2: fix the defects in the `unresolved` of the records demoted by Corrections A2 (M12-001/002/007/010/012/017/019, M5-005/017), A3 (M10-002/009/010/011, M15-004/008/009/011/012/013/015), A5 (M3-024) and A7 (M6-003): 22 records. Rows: those texts plus research/20260929-audit-calibration.md, 20260930-reaudit-sonnet-layers.md, 20261001-reaudit-M3-M4.md, 20261001-reaudit-M5B-M6.md. Records stay IMPLEMENTATION_GAP, "built, awaiting strong verification" (CHECKLIST 6).

## Plan
- Stream A (M12 + M5 + the M15-012 hunk): M12-001/002/007/010/012/017/019, M5-005/017, M15-012.
- Stream B (M10/M15/M3/M6): M10-002/009/010/011, M15-004/008/009/011/013/015, M3-024, M6-003.
Both streams run in parallel in one tree (disjoint files); verifier passes per stream; one commit when both pass (hunks in shared files like CozmoEngine.cs may overlap).
