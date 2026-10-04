CLAIMED claude-sonnet 2026-10-03

Job R-FIX2: fix the defects in the `unresolved` of the records demoted by Corrections A2 (M12-001/002/007/010/012/017/019, M5-005/017), A3 (M10-002/009/010/011, M15-004/008/009/011/012/013/015), A5 (M3-024) and A7 (M6-003): 22 records. Rows: those texts plus research/20260929-audit-calibration.md, 20260930-reaudit-sonnet-layers.md, 20261001-reaudit-M3-M4.md, 20261001-reaudit-M5B-M6.md. Records stay IMPLEMENTATION_GAP, "built, awaiting strong verification" (CHECKLIST 6).

## Plan
- Stream A (M12 + M5 + the M15-012 hunk): M12-001/002/007/010/012/017/019, M5-005/017, M15-012.
- Stream B (M10/M15/M3/M6): M10-002/009/010/011, M15-004/008/009/011/013/015, M3-024, M6-003.
Both streams run in parallel in one tree (disjoint files); verifier passes per stream; one commit when both pass (hunks in shared files like CozmoEngine.cs may overlap).

## Progress (2026-10-03/04)
Streams A and B built in parallel in one tree (disjoint files), so one commit. Read-only Claude `cozmo-verifier` passes (the reports are the agents' messages):
- Stream A (M12-001/002/007/010/012/017/019, M5-005/017, M15-012): round 1 FAIL (5 blocking), round 2 FAIL (2 blocking + 1 narrow: the look-down carried-id release order, PickupObjectAction.Verify's order/results, the squint latch), round 3: no source-fidelity objections, 1 flaky test (fixed: the stamp comes from PickupObjectAction.VerifyStartedAt; the class ran 12 times clean). The final comment/MISSING-report hunks had no separate verifier pass.
- Stream B (M10-002/009/010/011, M15-004/008/009/011/013/015, M3-024, M6-003): round 1 FAIL (the TryToStackOn callback categories; the missing records), round 2 PASS.
- Seven new records M15-019..M15-025 (the floor-placement path M15-008/009 depended on; research/20261003-R-FIX2-M15-gap1-extraction.md; inventory Correction A4, M15-freeplay re-approved).
- Full suite 3273/3273 (several clean runs; two earlier failures happened with a stray test host running), `fidelity.py --check` clean (425 records).
All 22 records stay IMPLEMENTATION_GAP, unresolved "built, awaiting strong verification (R-FIX2, 2026-10-03): ..." with what remains. Nothing settled (CHECKLIST 6).
