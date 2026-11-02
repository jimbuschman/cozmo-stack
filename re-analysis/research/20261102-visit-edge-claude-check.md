# VISIT-EDGE-REAL-MAP-1: Claude check (manager, 2026-11-02)

**Verdict: PASS for the loop, merged with no record settled. In simulation Hiking no longer holds once the map has content, and
VisitInterestingEdge runs and ends. Its close branch cannot run on a real robot until M11-054 is fixed. No real robot has run this.**

Built by a Claude implementer from Codex's reading `research/20261102-visit-edge-reading-codex.md`, checked by a separate verifier,
who found no error in the notes where the binary was opened.

## Result

Canonical robot through `HardwareEngineOptions`, 300 s, map content through the production path.

| Scenario | Total idle | Longest idle in Hiking | Hiking visits | Visit starts / completions |
| --- | --- | --- | --- | --- |
| Content, no border | 4.1 s | 0.1 s | 10 | 0 / 0 |
| Painted edge | 2.3 s | 0.1 s | 4 | 11 / 9 |
| Close edge, chain supplied at `ApplyOverhead` | 3.4 s | 0.2 s | 7 | 4 / 4 |

The robot run this answers: `research/20261102-idle-robot-run-2-claude-check.md`, 45 to 60 s idle on each Hiking visit after the first.

## Verifier

Clean where opened: border regions against the notes' rows, caller order, aging cadence and callback (0x0067DC4C..0x0067DD20,
0x006808F4), Init, Stop, Update, far, close and NaN branches, category handling, the sort main body instruction by instruction
including the missing `i++` the implementer fixed (0x005E83DE..0x005E8400), the chain filter, and the -1.0 whiteboard default.
Cliffs now age out at 30 s, which is engine behaviour that was missing before.

**Not read:** common Resume 0x005BDA94, sort3/4/5, the detector's real image path, whether `Dispose` runs at retirement.

## M11-054

`HeadGeometry.HeadCamPositionMm` z is 17.52; native is -8.0 (0x0050FF8A). The camera sits 25.5 mm too high in the vision model. At
head -0.2618 rad the first visible floor reads about 89 mm from the robot where the engine sees about 49 mm. So on a real robot:

- the close branch (60 mm, 0x005E775A) can't be reached: Visit creeps until the edge leaves the image and ends not-found;
- FlagVisitedQuad 0x005E8038 and the Observe animation don't run, so an edge can be chosen again for up to 120 s;
- distances to edges come out about 1.6 times too far, and marker poses carry the same error.

Fixing it needs the exact solvePnP (M11-054). It doesn't hold the loop. `CloseEdgeFlagsTheVisitedConeAndObserves` substitutes an
input production can't make, and the test header now says so.

## Needs read protection

Operator's approved departure, M15-014 in AGENTS.md. The first build leaked on reconnects and on a dropped link, so the rule became:
from the serial edge until the copies resolve, nothing is saved under that serial, and a serial that was never earned is taken back.
New tests cover an unusable read over two runs, a reconnect with a bad then a good read, and a disconnect during the read. A bad header
or size that the stack reports as NV_NOT_FOUND now counts as a failed read. A true NV_NOT_FOUND keeps the engine's first write.

**Open:** what the firmware itself returns as NV_NOT_FOUND is not established. Host API Inventory and Unlock writes can still write
after failed reads of those blocks, which is engine behaviour outside this change.

## Other open items

- Seed 6 is always empty: the stack has no ObstacleProx producer, and AddProxObstacle 0x00624A0A has no direct caller found.
- `OverheadEdges.cs` stays the M11-017 equivalent detector: filter2D and the other named bodies are UNKNOWN in the notes.
- Whiteboard +0x50 reset paths 0x0056A686..A69A and 0x0056AA28..AA36 are UNKNOWN.
- The canonical run has no cubes, so other Hiking behaviours with real cubes are not covered.

## Records

No status changes. M11-079, M11-089 and M11-092 stay IMPLEMENTATION_GAP with these limits named: settling them waits on M11-054 and a
direct read of the rest. M15-014 keeps its status.

## Progress

17 records settled across 41 jobs, 1 of 178 finished.
