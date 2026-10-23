# Research request R-M11-MAP-PRODUCERS (M11-074, M11-089, M8-026, M14-007)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** a builder job that moves the stack's remaining legacy-list map writers onto the native quad tree, so that
  `MemoryMap.HasCollisionRayWithTypes` can drop its interim legacy-region check (B-M13-19 fix-up).
- **Context:** the current `unresolved` of M11-074, M11-089, M8-026 and M14-007 in `re-analysis/fidelity_manifest.json`;
  `research/20261023-m11-tree-insert-ray-geometry.md` and its check (the tree insert geometry is settled there);
  `research/20261022-m13-017-groundwork.md` row M7 (how an object reaches Insert). In the stack, the legacy writers are
  `MemoryMap.AddVisionOverheadEdges` (called from `Behavior/FreeplayStack.cs:313`) and the robot-pose square in
  `Vision/MemoryMap.cs` (~180-200). Run `python re-analysis/tools/known.py <address|name>` before tracing anything.
- **Answer file:** `research/20261023-m11-map-legacy-producers.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **MapComponent::AddVisionOverheadEdges** (0x0067F814, 3138 bytes): every call it makes into the map (Insert or
   another writer), in order. For each: the polygon it passes (how built from the edge frame and robot pose), the
   content type, the data class and its fields (timestamps, any type-specific field), and the expansion budget.
   Include the clear-ground runs, the interesting edges and the not-interesting quads. Where an earlier checked answer
   already covers a call (for example the type-9 InterestingEdge data in B-M11-12 or the M11-089 answers), cite it.
2. **MapComponent::UpdateRobotPose** (0x0067E224, 626 bytes): the robot-pose map writes (the ClearOfObstacle/explored
   quad, any cliff/clear-of-cliff square) and the ProcessClearQuad join at 0x0067E47E: what is inserted, with which
   polygon, type, data and budget, and under which gates (movement threshold, cliff flag).
3. **For each writer, the narrow check first:** is it already extracted and checked? Cite that row and don't
   re-extract.

Do not extract AIWhiteboard::ProcessClearQuad's PossibleObjects predicate (M8-026) beyond naming the call; it is a
separate item.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
