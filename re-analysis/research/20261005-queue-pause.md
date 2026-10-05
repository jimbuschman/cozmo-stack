# Research queue paused

Paused at the operator request on 2026-10-05. All edits are saved under re-analysis/research/. No commits, code, manifest, inventory, state, tests or hardware changes.

Latest saved work:
- 20261004-solvepnp-extraction.md: added F35-F40 for normalization/refinement/restoration/extraction ordering; normalized mixed text encoding to UTF-8. Full corner/OpenCV dependency closure remains PARTIAL.
- 20261004-R-FIX-review.md: checked the bounded M11-054 camera translation addition against Robot construction; expanded M15-026 pickup selection/callback comparison, M15-009 AreAllCubesInBeacons predicate comparison, and M15-024 failure-table dispatch. Remaining review entries retain their coverage limits. Six recorded defects remain in this report.
- 20261004-R-FIX-native.txt: saved newly inspected pickup and AreAllCubesInBeacons instruction ranges.
- Audio verification report retains four recorded defects and remaining PARTIAL rows.
- ActionList, procedural-live, game-channel, M10 and helpers reports are present; their own coverage tables govern their completion claims.

Immediate next lead, not yet a finding: AIWhiteboard::AreAllCubesInBeacons compares its in-beacon count against BlockWorld+0x48 family2 map value. The C# explicitly declares the identity/writers of this map unknown. BlockWorld constructor 0x0061BD08 initializes that map and calls DefineObject for three active-cube templates and a charger. Read DefineObject 0x0061BFC0..0x0061C2CA (navigation aid re-analysis/decomp/libcozmoEngine/0061/0061bfc0.c), then verify instructions and writers before drawing any conclusion. This investigation was paused before reading that body.

Research is not declared finished. Resume from the saved coverage tables; do not repeat completed extraction or treat declared gaps as recovered behavior.
