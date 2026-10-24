# Research request R-M11-OBJECT-DATA (M11-074, M11-046)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** a builder job that puts cubes (content type 3) into the native map and removes them, replacing the
  B-M13-16 MISSINGs "MemoryMapData_ObservableObject constructor 0x00684A38" and "whole-map TransformContent
  0x0068140C -> 0x0068A414 and its derived content-3 ID adapter at 0x0068110E" (`research/20261023-b-m13-16-claude-check.md`).
- **Context:** the current `unresolved` of M11-074 and M11-046; `research/20261022-m13-017-groundwork.md` rows M7-M9
  (the add path up to the data constructor; the removal callback window 0x006810E0..0x00681178);
  `research/20261013-quadtree-rest.md` and its check (Transform). Run `python re-analysis/tools/known.py <address|name>`
  before tracing anything.
- **Answer file:** `research/20261024-m11-object-map-data.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **MemoryMapData_ObservableObject** constructor (0x00684A38, 72 bytes) as AddObservableObject's content-3 branch
   calls it (from 0x0067EF00): every field it stores (type, timestamps, ObjectID at +0x14, any pose or polygon), and its
   Clone and Equals (vtable slots) if the tree uses them.
2. **MemoryMap::TransformContent** (0x0068140C) and **QuadTreeProcessor::Transform** (0x0068A414): how the removal
   callback is applied over the whole map, in what node order, and what happens to a node whose content is replaced.
   Cite the Transform rows already checked; extract only what is not.
3. **The derived ID read** at 0x0068110E in the removal callback: which field of which data class it reads, and how a
   base-data node (type 4 charger) is told apart.
4. **The narrow check first:** cite whatever is already extracted and checked.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
