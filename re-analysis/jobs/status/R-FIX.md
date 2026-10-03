CLAIMED claude-sonnet 2026-10-03

Job R-FIX: fix the defects recorded in the `unresolved` of the M11, M13 and M14 records demoted by corrections A3, A4 and A6 (31 records: M11-002, 003, 006, 008, 010, 015, 019, 020, 022, 023, 029, 031, 036, 039; M13-002, 003, 004, 005, 008, 009, 011, 012, 013, 015, 016, 017, 018; M14-001, 003, 004, 005). Rows: those texts plus research/20260930-reaudit-M11.md, 20261001-reaudit-M7-M8-M9-M13.md, 20260930-reaudit-sonnet-layers.md. Records stay IMPLEMENTATION_GAP, "built, awaiting strong verification:" (CHECKLIST 6).

## Plan
- Batch 1: float-bit and width fixes (M11-002/003/006/008/010/015/020/022, M13-002, M14-001/003/004/005).
- Batch 2: the f32 lattice planner and path defects in M13 (M13-003/004/005/009/011/018 and the charger/behaviour path defects 008/012/013/015/016/017).
- Batch 3: the M11 path defects (M11-019/023/029/031/036/039).
