# HANDOFF LOCALIZE-MULTI-PAIR-CROSS-ORIGIN-TEST-1 (Claude worker, 2026-11-12)

This is a test job, so no production code changed. The census is the worker's local
`.scratch/LOCALIZE-MULTI-PAIR-CROSS-ORIGIN-TEST-1-census.md`.

## What the engine does

**One cube seen after a delocalization yields two localization pairs.**
- AddAndUpdateObjects inserts the other-origin pairs only after the current-origin Insert returns 1
  (0x0062115E..0x006211A4).
- Both pairs carry the same distance.

**LocalizeRobot (0x0050D1CC..0x0050D4D8) takes them in ascending origin-ID order:**
- The source map is walked in key order (0x0050D234..0x0050D2CE).
- In the farthest-first multimap, ties go right (0x0050D6F8..0x0050D700).
- Origin IDs only grow (0x00847B60..0x00847B6C).

**So the old-origin pair goes first.** Its LocalizeToObject rejiggers the origins, and UpdateObjectOrigins moves the cube.
For a unique type the counterpart keeps its ID (0x0062902E..0x00629038).

**X19 then re-finds the current-origin pair** by its stored ID in the new current origin:
- StoringMatchedObjectID at 0x0050D39C..0x0050D3B0;
- the lookup at 0x0061F3E0..0x0061F50A.

The second LocalizeToObject is same-origin, with no second Rejigger.

## The test

`LocalizeCrossOriginTests.LCO_MultiPair_TheRelocalizingFrameLocalizesTwiceAndReFindsTheCurrentPairByIdInTheNewOrigin`
(reach: production; real frames go through the queued worker and the tick).

**Substitutions, all stated in the test:**
- the direct Delocalize call;
- IsOfflineEngine cleared by reflection;
- the fixture's sim firmware report.

**On the relocalizing frame it checks:**
- one StoringMatchedObjectID line naming the cube, with no MissingMatched or CrossFrameLocalizeFailure line;
- one RejiggeringOrigins;
- one Robot.LocalizeToObject debug line (supporting only);
- PoseFrameId +2 (S10 0x00515152 twice);
- LocalizedToObjectId = the cube, and Robot2C4 = 1;
- exactly one 0x45: origin o1, frame before+2, the observation time;
- the cube in o1 is the same instance with the same ID, and nothing is left in o2.

**The frame-count check is live:** with +1 the test fails ("Expected: 3, Actual: 4").

**What the result shows:** the stack does hold both pairs on that frame and matches the engine. No defect was found, so
there is no FIX.

**Results:** affected classes 33/33. For the full gate, see the commit.

## Verifier: PASS

| Finding | Class | Resolution |
| --- | --- | --- |
| The reach label named only the Delocalize substitution, not the offline flag or the sim firmware | queued | The label now names all three, and why none affects the asserted values; also noted in M11-044. |
| F1: LocalizeRobot's three log lines are free text in World.Log; the StoringMatchedObjectID text is truncated; the key typo LocalizeToRobot | queued, log only | Recorded in M11-044. |
| F2: the NaN order (.NET puts NaN last; the engine keeps insertion order for two pairs) | observation | Already open in M11-044. |
| The +2 frame count is better supported than the census caveat: SetNewPose's only callers (0x00510B4C, 0x0051664E/0x00516672, 0x005168BA, 0x0063E87E) don't run on that frame | observation | Noted in M11-044. |

## Records

**M11-044:** `unresolved` and `test` only. X19 is now covered at production reach. No settlement: the guard waits on
RETIRE-LEGACY-DRIVE-API-1, and the NaN order and the other items in its `unresolved` remain.
