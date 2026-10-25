# Research request R-M11-ROOT-GROWTH (M11-045, M11-048)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** a builder job that replaces the visible MISSINGs "M11-048 UpgradeRootLevel body 0x006876FA..0x006879C0; root
  unchanged" (`Vision/SourceQuadTree.cs:458`) and "M11-048 ShiftRoot 0x00686DF8..0x0068743E; return ignored, root
  unchanged" (`:493`). Today an insert outside the root is lost, so as Cozmo drives away from where the map started,
  later content never reaches the tree.
- **Context:** the current `unresolved` of M11-045 and M11-048 in `re-analysis/fidelity_manifest.json`;
  `research/20261023-m11-tree-insert-ray-geometry.md` and its check `20261023-r-m11-tree-geometry-claude-check.md`;
  `research/20261012-cliff-world-helpers-check.md` and `research/20261013-play-area-check.md` (the processor bookkeeping,
  ClearDescendants, SwapChildrenAndContent, DestroyNodes are already read: cite them, don't re-extract).
  Run `python re-analysis/tools/known.py <address|name>` before tracing anything.
- **Answer file:** `research/20261024-m11-root-growth.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **The narrow check first:** which of items 2-4 are already extracted and checked anywhere? Cite the file and row.
2. **QuadTreeNode::UpgradeRootLevel** (0x006876D8, 868 bytes): the condition to grow, the new root's centre, size and
   level, how the old root becomes a child (which quadrant, chosen how), the max-level limit and what happens at it,
   the content/data of the new siblings, the processor notifications, and the return value.
3. **QuadTreeNode::ShiftRoot** (0x00686DF8, 1532 bytes): when it is chosen over UpgradeRootLevel, the shift direction
   and distance, which children are kept, moved or destroyed, the content of the new ones, the processor
   notifications, and the return value.
4. **The callers** (QuadTree::Insert / Expand, the map wrapper): the order of the checks, when each of 2 and 3 is called,
   how their returns are used, and whether the insert is retried after a grow or shift.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
