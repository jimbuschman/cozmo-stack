# Claude check: B-M13-19 (07de2898, fix-up 6343ddf5, status d71d4338) — 2026-10-23

Manager review of `Vision/SourceMapGeometry.cs`, the SourceQuadTree/MemoryMap joins and the fix-up, against
`research/20261023-m11-tree-insert-ray-geometry.md` and the rows it cites. **PASS.** No record settles (M11-045,
M11-046, M11-048, M14-007 unchanged). Builder: full suite 6099/6099 after the fix-up.

## Checked against the cited rows

- FastPolygon: vertex order kept; cyclic edges; normal (-(inv*dy), inv*dx) with single-rounded products; centre
  accumulated per vertex as F(old + F(inv*v)), inv = F(1/n); four separate bounds scans (min on <, max on >, from
  the largest finite f32); outer radius from f32 differences squared and summed in f64 then narrowed, updated on <;
  inner radius F(dot*dot) from the f32 normal dot, updated on >; n = 1/2 give inner 0 and the vertex0-to-vertex0/1
  outer; the error diagnostic only for ordered inner > outer; n = 0 skip-and-log under M1-034.
- Contains: bounds without epsilon, r^2 = F(F(dy*dy) + F(dx*dx)), strict outer reject, strict inner accept, normals in
  order continuing on LE (including unordered).
- LineSegment: t = F(F(dy*x) - F(dx*y)); |t| < e -> 0, t >= -e -> 1, else (and NaN) 2; on-segment uses the receiver's
  unexpanded inclusive bounds; the early-return order B.a, B.b, equal -> false, A.a, A.b, differ.
- GetOverlapType: matches the instructions re-read in `20261023-r-m11-tree-geometry-claude-check.md` (3 / early 1 /
  n < 2 -> 0 / diagonals (min,min)-(max,max) and (max,min)-(min,max) against each polygon edge as receiver / minimum
  corner Contains -> 2).
- Ray query from the native tree with the eleven-type mask.

## Fix-up

`HasCollisionRayWithTypes` answers true if the tree ray is true, otherwise falls back to the legacy regions of a
selected type, with a ReportMissing naming the legacy producers (M11-089 overhead edges, M8-026 robot-pose squares).
This keeps the freeplay overhead-edge and robot-pose content blocking until those writers move to the tree
(R-M11-MAP-PRODUCERS, row 60). The two stale tests were updated to the new signature and to the native cliff insert.

## Open (not blocking this job)

`M3M4ReviewFixTests.PoseStateHostHandlerCanCommandLightsAndFrameGateMisuseFailsImmediately` timed out once on the
builder's PC at the setup's **15 s** wait for ConnectionState 2 (status line 257), after 0f8f68a5 raised it from 5 s.
A 15 s stall of one engine tick is more than slowness: it may be a starvation or lock wait in the simulated connect.
Under AGENTS.md races and deadlocks block, so this needs a root cause: a manager investigation item, not a timeout bump.
