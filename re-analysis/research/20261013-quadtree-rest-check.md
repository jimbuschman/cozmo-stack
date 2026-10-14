# Opus check of R-QUADTREE (2026-10-13)

- **Checked:** `research/20261013-quadtree-rest.md` (Codex, overnight P1), against libcozmoEngine.so. Read-only
  cozmo-extractor.
- **Adopted:** by the manager on 2026-10-13. Use the Codex report together with the corrections and additions below. The
  build rows go to M11-045, M11-046 and M13-032.
- **Result:** items 1–6 are confirmed instruction by instruction. The quad-tree subset of M11-048 is now fully read.

## Corrections to the request and report

- **ShiftRoot** has 20 direct ForceSet sites (not 19), plus 12 Swap sites.
- **Transform's entry** is 0x00688378. (0x0068846E is the ForceSet inside it.)

## Omitted facts (now read)

1. **ForceSet** converts an incoming type 5 to a new base type-1 data, both stamps = the incoming +0xC, before the
   snapshot and the callback (0x0068755E..0x006875A0).
2. **ResetBoundingBox:**
   - h = F(side·0.5);
   - min = (F(cx−h), F(cy−h)), max = (F(h+cx), F(h+cy)), through AxisAlignedQuad (fminf/fmaxf), which sets the bounds
     and both diagonals (0x0068698C..0x00686A7E).
3. **Expand** ignores ShiftRoot's return and re-tests Contains, up to `budget` times. It always writes QuadTree+0 = 1
   (0x00685312..0x00685438).
4. **TransformContent:** the whole-map version (vt+0x10, 0x0068140C) → QuadTreeProcessor::Transform 0x0068A414 →
   MakeQuadXY(0) → ImportQuad2d → FastPolygon → root Transform; a null root is skipped. Its caller is
   FlagInterestingEdgesAsUseless (0x0067E7D8).
5. **SortCornersClockwise:**
   - atan2f per corner about the quad centroid (c = p0, then + p2, + p1, + p3, then × 0.25);
   - libc++ __sort4 with a strict `<` (NaN is never less);
   - Quadrilateral(p[asc0], p[asc3], p[asc1], p[asc2]);
   - after ImportQuad2d, the polygon order is asc0, asc3, asc2, asc1.

## The build rows (by owning record)

- **M11-045, the tree geometry:**
  - **FastPolygon:** ctor, CreateEdgeVectors, centroid, bounds, ComputeCircles, Contains
    (0x008416F0..0x00841C70, 0x0050238A..0x005023D6);
  - **LineSegment:** ctor and IntersectsWith (0x00842740..0x008427A2, 0x0084292C..0x00842B64);
  - **GetOverlapType** (0x00688290..0x0068836E: per-vertex tests, then diagonal × polygon-edge intersection with the
    polygon edge as the receiver, then Contains(centre));
  - **the AxisAlignedQuad diagonals,** and ResetBoundingBox;
  - **UpgradeRootLevel:** the quadrant from (centroid − root centre) against −1e-5; the children Q0..Q3; ForceSets in
    index order; the area and the log;
  - **the Expand loop;**
  - **ShiftRoot:** the flags, the refusals, the AllPointsIn errG, the recentre, the 8 cases, Swap, DestroyNodes, the
    success log.
- **M11-046, Transform and the queries:**
  - QuadTreeNode::Transform and the NodeContent `!=` (0x0068C4E4..0x0068C514);
  - both wrappers (no dirty byte); the ray walk (per cell);
  - HasContentType, answered from the cached set {3,6,7,8,9,10} (mask 0x7C8).
- **M13-032, the merge** (0x006813A8 → 0x00685504..0x00685720):
  - leaf collection; MakeQuadXY; ApplyTo (visiting 0,2,1,3); ImportQuad2d;
  - Rotation3d·point (f32, in M11-065's order);
  - the budget = ceilf(dist/1280.0f).
  - **The null old map** (operator[] at 0x0067DE8E) crashes at 0x00688254 in the original. That falls under the
    **M1-034 standing rule** if reachable.

## Still unread (new records)

- **The map producers and readers:**
  - MapComponent::UpdateRobotPose (the explored area);
  - UpdateObjectPose, RemoveObservableObject;
  - FlagInterestingEdgesAsUseless, ReviewInterestingEdges, ClearRobotToMarkers;
  - FindContentIf/FindIf.

  These stand behind the stack's `_regions` writers. **Moving all map content into the tree waits for them.**
- **The map broadcast:** MapComponent::Update, BroadcastMap, QuadTree::Broadcast (MemoryMapQuadInfo). Possibly an
  engine-to-game message.
- **Already open elsewhere:** the border walk (M11-047), and M11-048's remaining non-quad-tree items.

## Contradicted

- **M11-017's evidence** ("not observable"): the ray answers per cell, and HasContentType(ClearOfCliff) is always false
  in the engine.
- **M14-007's evidence** ("answers the same ray exactly"): it answers per cell.
