# Claude check: R-M11-TREE-GEOMETRY (row 59, answer 8c56752b) — 2026-10-23

Manager spot-check of `research/20261023-m11-tree-insert-ray-geometry.md` (Codex), instructions re-read in
libcozmoEngine.so. **PASS.** No record settles.

The answer mostly cites earlier checked rows (`20261013-quadtree-rest.md` Q rows 1a-1c, 6a, 6c and its check;
`20261012-cliff-world-helpers-check.md` §2; R-VIS gap2 rows 1.3-1.18; B-M3M4-19 Q1-Q6), which is what the request
asked, and adds bindings and one correction.

## Re-read: GetOverlapType 0x00688290..0x0068836E in full

- Empty vertex list -> 3 (0x0068829E..0x006882A0 -> 0x00688324).
- Per vertex, inside = x within [bounds+0x1C - e, +0x24 + e] and y within [+0x20 - e, +0x28 + e], all f32, with
  `bhi`/`blt`/`bls`/`ge` (0x006882DA..0x006882D6). All inside -> 3; inside and outside both seen -> 1 at once
  (0x00688312..0x0068831E).
- None inside and fewer than 2 vertices -> 0 (0x0068832C..0x00688332).
- Each node diagonal (node+0x2C, stride 0x28, two of them) against each polygon edge (polygon+0x38), receiver is the
  polygon edge (r0 = r7) and the argument the diagonal (r1 = r6); any intersection -> 1 (0x0068833C..0x00688354).
- Finally FastPolygon::Contains (PLT 0x004BD08C) on node+0x1C, the minimum corner: true -> 2, else 0
  (0x0068835C..0x0068836E). This confirms the answer's correction (minimum corner, not centre).

## Consequence

The insert geometry (FastPolygon construction, node containment, GetOverlapType, Contains, LineSegment::IntersectsWith)
and the ray walk are all extracted and checked. The stack has the tree (`Vision/SourceQuadTree.cs`, with explicit
MISSINGs at the overlap and FastPolygon joins) and a `FastPolygonF` in `Manipulation/LatticePlanner.cs` that must be
compared with Q §1 before reuse. Builder job B-M13-19.

M11-048 is RECOVERABLE_GAP. Its insert/ray geometry items are now recovered; the record's status and wording are
updated by the manager after the build, not by the builder.
