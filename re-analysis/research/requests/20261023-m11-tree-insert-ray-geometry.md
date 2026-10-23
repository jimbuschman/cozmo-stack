# Research request R-M11-TREE-GEOMETRY (M11-048, M11-046, M14-007)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** a builder job that inserts object footprints into the native quad tree and answers
  `MemoryMap::HasCollisionRayWithTypes` (0x0068176E) from it, so that BehaviorInteractWithFaces::CanDriveIdealDistanceForward
  (0x005C2420) sees the charger (B-M13-16 MISSINGs 1 and 5; `research/20261023-b-m13-16-claude-check.md`).
- **Context:** the current `unresolved` of M11-048, M11-046 and M14-007 in `re-analysis/fidelity_manifest.json`;
  `research/20261013-quadtree-rest.md` and its check `20261013-quadtree-rest-check.md` (Transform and the ray walk);
  `research/20261022-m13-017-groundwork.md` rows M7 and W1. Run `python re-analysis/tools/known.py <address|name>`
  before tracing anything.
- **Answer file:** `research/20261023-m11-tree-insert-ray-geometry.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **Insert's geometry:** MemoryMap::Insert (0x006817C6) -> FastPolygon construction (0x008416F0 and its callees) ->
   QuadTreeNode::GetOverlapType (0x00688290). What FastPolygon precomputes from the four corners (order, edges, bounds,
   any normalisation), and how GetOverlapType classifies a node against it (inside, partial, outside), including the
   float operations and comparisons exactly (strict or not, f32 or f64).
2. **FastPolygon::Contains** and any other polygon test the insert path calls, the same way.
3. **The ray query:** HasCollisionRayWithTypes (0x0068176E) down to the leaf test, and LineSegment::IntersectsWith
   (PLT 0x004BD080, the body it resolves to). Compare with the ray walk already extracted in
   `20261013-quadtree-rest.md`: say what is already covered there (cite its row) and extract only what is not.
4. **For each step, the narrow check first:** is it already extracted and checked in an earlier answer? If so, cite that
   row and don't re-extract.

Do not extract UpgradeRootLevel, ShiftRoot, the border processor or ClearDescendants unless the insert or ray path
above calls them; if it does, name the call site and stop there.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
