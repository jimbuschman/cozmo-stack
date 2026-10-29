# PUTDOWN-PYRAMID-1 — Claude check (manager, 2026-10-29)

**Verdict: PASS. Nothing settled.** Merged 794976a1 (code 4d69a394).

Four tests drive the shipped PyramidPutDownBlock instance (ID 68, class 44) through the existing native PutDownBlock
path (M15-075, already EXACT_SOURCE). The one production change adds a visible M8-012 MISSING diagnostic before the
manager's transition hook: the typed BehaviorTransition report carrying displayNameKey is not built. That is an honest
gap marker and changes nothing observable.

## Checked by Claude

- `behaviors/freeplay/buildPyramid/pyramidPutDownBlock.json`: class PutDownBlock, ID PyramidPutDownBlock,
  displayNameKey `behavior.PrepareCubes`, with no scoring and no needs override.
- `activities/freeplay/buildPyramid.json`: PyramidPutDownBlock appears in a plain StrictPriority list (DriveOffCharger,
  KnockOverCubes, PyramidPutDownBlock, SparksLookInPlace) with no scoring entry. So the instance keeps score 0, not
  NothingToDo's 1.1. The tests assert exactly that.

The other two callers (Sparks BuildPyramid, Feeding), the StrictPriority ctor and the display path rest on Codex's
independent checks. M8-012 stays IMPLEMENTATION_GAP.
