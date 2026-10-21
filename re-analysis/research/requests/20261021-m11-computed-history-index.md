# R-M11-HISTORY-INDEX — computed-state auxiliary index producer

Independent read-only job; answer `research/20261021-m11-computed-history-index.md`.
Read current M11-044 and search research/inventory first, including the 20260929 R-VIS reads and F9 in `20261021-m11-pose-history-settling.md`. Reuse adopted ComputeStateAt and culling logic; do not extract them again.

Trace only ComputeAndInsertStateAt continuation after0x00531BAA: computed-state insertion, timestamp/key allocation, both auxiliary index writes, duplicate/error outcomes, and returned key/state pointer. Identify the actual consumers that depend on the indices and the live UpdateVisionMarkers caller. Compare current production code and the tests which seed indices explicitly; seeded fixtures cannot prove a producer. Give exact arithmetic/wrap and overwrite/order semantics where native instructions establish them, otherwise UNKNOWN.

Return current record title/status/evidence, native row table, production joins, bounded build recommendations and independent regression oracles. No whole history/map audit, camera assembly or pose-serialization extraction (separate answers), no invented key allocator. Use decomp/libcozmoEngine as navigation and .so instructions as evidence. Research files only; no implementation/tests, approvals, manifest changes, hardware or settlement. Publish under research lane rules.
