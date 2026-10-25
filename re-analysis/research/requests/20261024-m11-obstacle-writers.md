# Research request R-M11-OBSTACLE-WRITERS (M11-074, M13-030, M11-046)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** builder jobs for the three memory-map writers that the stack still lacks
  (`research/20261024-b-m13-27-claude-check.md`, Q1 and Q4). None of these content types reaches the native tree today, so
  the ray checks (CanDriveIdealDistanceForward, the face behaviour's drive-in check) never see them:
  - type 6 ObstacleProx: the markerless prox payload and writer, 0x00622646..0x006226E6 (M13-030; MISSING at
    `cozmo-stack/src/Cozmo.Robot/Vision/BlockWorld.cs:1263`);
  - type 7 ObstacleUnrecognized: no producer is bound at all;
  - type 10 not-interesting quads: FlagQuadAsNotInterestingEdges, 0x0067E6B0/0x0067E71C (MISSING through
    `Behavior/VisitInterestingEdgeBehavior.cs:22`). The overhead border writer, type 10 through FillBorder, is already
    built and is not part of this request.
- **Context:** the current `unresolved` of M11-074, M13-030 and M11-046 in `re-analysis/fidelity_manifest.json`;
  `research/20261023-m11-map-legacy-producers.md` and its check (the writers already found);
  `research/20261024-m11-object-map-data.md` (the object payload, the pattern to follow).
  Run `python re-analysis/tools/known.py <address|name>` before tracing anything.
- **Answer file:** `research/20261024-m11-obstacle-writers.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **The narrow check first:** what of items 2-4 is already extracted and checked? Cite it.
2. **Type 6:** the markerless prox obstacle path, from the prox reading to the map insert: the trigger condition, the
   quad (size and placement relative to the robot pose), the data object and its payload, the insert entry point, and
   the removal or expiry.
3. **Type 7:** every call site in the binary that inserts or sets content type 7 (search the data constructors and the
   insert wrappers for the constant), each with its caller chain up to an engine event, and whether that event can occur
   with a real robot (not SDK mode, not the app).
4. **Type 10 explicit quad:** FlagQuadAsNotInterestingEdges's body and its callers (VisitInterestingEdge, per PC48), the
   quad, the data, the insert entry point.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
