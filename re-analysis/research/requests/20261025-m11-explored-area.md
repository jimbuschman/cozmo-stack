# Research request R-M11-EXPLORED-AREA (M11-045, M11-064)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** a decision on the visible MISSING "M11-045 explored-area helper+0x38: tree does not yet contain all map
  content" (`cozmo-stack/src/Cozmo.Robot/Vision/MemoryMap.cs:101`, `ExploredRegionAreaM2` returns NaN). It is either
  built, or recorded as "no effect" with evidence.
- **Context:** the current `unresolved` of M11-045 and M11-064 in `re-analysis/fidelity_manifest.json`;
  `research/20261013-play-area-check.md` (the processor's area bookkeeping: the type-change callback and the cached area
  sums); `research/20261024-m11-root-growth.md` (growth and shift keep the credit through the callbacks). AGENTS.md
  "What we reproduce" items 1-4 and "Narrow checks". Run `python re-analysis/tools/known.py <address|name>` before
  tracing anything.
- **Answer file:** `research/20261025-m11-explored-area.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **The narrow question first:** list every reader of the explored-area value (GetExploredRegionAreaM2 and helper
   +0x38), with its caller chain up to an engine event. For each one, say whether the value reaches the robot (a
   message), a behaviour or action decision, a game-facing message or event, or state that later feeds one of those; or
   only a DAS/analytics event. Cite the instructions that show it. An "only DAS" answer must show the value goes nowhere
   else.
2. **Only for readers that do reach any of those:** the getter's body, which cached sums it adds (types and order), the
   unit conversion and the f32/f64 widths. Cite the play-area rows where they already cover it.
3. **The robot.play_area_size event** (M11-064, the 60 s gate): only whether its payload or timing feeds anything other
   than DAS.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
