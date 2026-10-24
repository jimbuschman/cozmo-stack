# Research request R-M11-FILLBORDER (M11-046, M11-047, M11-074, M11-089)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** a builder job that replaces the MISSING "FillBorder 0x0067F0BC -> 0x006813B0 -> 0x00689FAC: generic tree
  border callback/consumer closure" (B-M13-16, `research/20261023-b-m13-16-claude-check.md`) and the type-10 stage of
  the overhead writer (B-M13-21, row 1i of `research/20261023-m11-map-legacy-producers.md`).
- **Context:** the current `unresolved` of M11-046, M11-047, M11-074 and M11-089 in `re-analysis/fidelity_manifest.json`;
  `research/overnight/20261014-ov11-m11-047.md` and its check `20261014-ov11-m11-047-check.md` (side-neighbour seeds,
  one-point centre inserts, insertion-order policy); `research/20261023-m11-tree-insert-ray-geometry.md` and its check.
  Run `python re-analysis/tools/known.py <address|name>` before tracing anything.
- **Answer file:** `research/20261024-m11-fillborder.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **MemoryMap::FillBorderInternal** (0x006813B0, 84 bytes): the data it builds and what it passes to the processor.
2. **QuadTreeProcessor::FillBorder** (0x00689FAC, 812 bytes): how it finds the border nodes of the inner type next to
   the outer types (the mask), the neighbour walk, the order, deduplication, and the inserts it makes (polygon, data,
   entry point). Where the OV11 answer and its check already cover a step, cite the row and don't re-extract.
3. **Its callees** only as far as needed to state the inserts exactly (neighbour lookup, node centre, the insert entry
   that bypasses Expand).
4. **The narrow check first:** what of items 1-3 is already extracted and checked? Cite it.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
