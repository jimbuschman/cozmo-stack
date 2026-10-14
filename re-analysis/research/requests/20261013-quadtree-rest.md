# Research request R-QUADTREE (M11-048)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261012-cliff-world-helpers-check.md` (section 2), `research/20261013-play-area-check.md`
  (section P), `research/20261013-blockworld-flag-and-map-helper.md` (Q2), and the M11-048, M11-045, M11-046, M11-017
  and M13-032 records in `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-quadtree-rest.md`.
- **Format:** one coverage table per item, first. Give each step's exact formula in evaluation order, the f32/f64 width,
  and the address, or UNKNOWN. Use a disassembler that skips undecodable data.

## Items

1. **QuadTreeNode::GetOverlapType, and FastPolygon** (0x00688290 and its callees: the FastPolygon constructor and
   Contains): the overlap classes it returns, and how.
2. **The UpgradeRootLevel body** (0x006876FA..0x006879C0): the quadrant choice (with the −1e-5), recentring, the Unknown
   fill, and its ForceSet calls and their area effect.
3. **ShiftRoot** (0x00686DF8..0x0068743E): the whole body, including its 19 ForceSet sites, the logs at 0x00686EE0 /
   0x00686F08 / 0x00686F58, and its area effect.
4. **QuadTreeNode::Transform** (0x0068846E).
5. **The MemoryMap merge** (vtable +0xC, called from UpdateMapOrigins at 0x0067DF80), and its handling of a null
   argument.
6. **The map's query side,** used by the stack's callers today (HasCollisionRayWithTypes, HasContentType; FaceBehaviors
   and the overhead edges): which MemoryMap and QuadTree functions answer these, read completely. Name which existing
   records (M11-046, M11-017, M14-007) they belong to, so all map content can move into the tree.
