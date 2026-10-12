# Request: M11-062 and M13-030, the BlockWorld and MemoryMap helpers on the cliff path

- **Answer file:** `re-analysis/research/20261012-cliff-world-helpers.md`.
- **Read first:**
  - the current manifest records M11-062, M13-030 and M4-038;
  - `research/20261012-m4-cliff-and-names-check.md`, section M4-038. It is the checked version: the Codex report it
    checks had the intersection filter backwards.
- **Rules:**
  - research only;
  - **one coverage table per item, first**;
  - no fidelity claims;
  - nothing leaves this machine.

## 1. M11-062: BlockWorld helpers reached by AddMarkerlessObject

For each helper: its full body, every branch, every log (key, format, channel, level), errG, every message sent, and
what it returns for a type-15 cliff object (family 6).
- `IsSameAs`, as called by the FindOverlappingObjects predicate (0x0061FD8C): the distance and angle tests, and how the
  tolerances (size·0.5, pi/4) are applied.
- The FindLocatedIntersectingObjects predicate (0x00622908..0x0062297C) with padding 0.0f: the intersection test itself.
- `GetBoundingQuad<float>`.
- `ObservableObject::SetID`: the ID allocator, its starting value, and whether IDs are shared with cubes.
- `InitPose` / `SetPose` with state 1.
- The tail call 0x008CAD1C (BroadcastObjectPoseStateChanged?): what is broadcast to the game, and its payload.
- `DeleteIntersectingObjects` with allowed types {14} and padding 0 (0x00622C68..0x00622C84), including what it deletes,
  logs and broadcasts.

## 2. M13-030: the MemoryMap insert of cliff data

- What `MemoryMapData_Cliff` holds (0x00684984), and the insert through helper vtable+8: the QuadTree insert, how it
  merges with existing content, and any log or broadcast.
- helper vtable+0x38 (the timestamp used for BlockWorld+0x58).

## Report

- Per item: the coverage table, the answer, and what stays UNKNOWN.
