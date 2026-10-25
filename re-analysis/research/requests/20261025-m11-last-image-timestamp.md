# Research request R-M11-LAST-IMAGE-TS (M11-017, M11-037)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** a builder job that replaces the visible MISSING "M11-017/M11-037 Robot::GetLastImageTimeStamp 0x00516EC0 at
  0x00680396: native camera getter owner is unbuilt" (`cozmo-stack/src/Cozmo.Robot/Behavior/FreeplayStack.cs:324`).
  Without it the overhead writer's type-10 border stamp is null, so no not-interesting edges reach the map. The explicit
  type-10 writer FlagQuadAsNotInterestingEdges (0x0067E6B0) reads the same getter.
- **Context:** the current `unresolved` of M11-017 and M11-037 in `re-analysis/fidelity_manifest.json`;
  `research/20261023-m11-map-legacy-producers.md` and its check; `research/20261024-m11-fillborder.md` and its check.
  Run `python re-analysis/tools/known.py <address|name>` before tracing anything.
- **Answer file:** `research/20261025-m11-last-image-timestamp.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **The narrow check first:** what of items 2-4 is already extracted and checked? Cite it.
2. **The getter** 0x00516EC0 (10 bytes): which object and field it reads, and its width.
3. **Every writer of that field:** each call site, the value written (which image's timestamp: the raw chunk's, the
   completed image's, the processed frame's), when it runs relative to image-chunk assembly, vision processing and the
   overhead-edge callback, the thread it runs on, and the initial value at robot construction.
4. **The readers on the map path:** 0x00680396 (the overhead border stamp) and FlagQuadAsNotInterestingEdges, and what
   each does with the value (both stamps, a comparison, nothing else).

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
