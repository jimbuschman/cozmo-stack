# HANDOFF LOCALIZE-CROSS-ORIGIN-1 (Claude worker, 2026-11-11)

Census: `.scratch/LOCALIZE-CROSS-ORIGIN-1-census.md` (rows X0..X20, the failure exits, section 2's prerequisites,
section 6's follow-up reads).

## What changed

Robot::LocalizeToObject's cross-origin branch is built in the engine order. The stack's early stop before S2 is gone,
and the same-origin/cross-origin split is back at its engine position after S10.

| Step | Address | Stack |
| --- | --- | --- |
| X0/X1 split | 0x0051577A, 0x0051583C..0x00515850 (HasSameRootAs(E, current origin)) | BlockWorld.cs `LocalizeToObject` → `LocalizeAcrossOrigins` |
| X2 RejiggeringOrigins event | 0x00515854..0x005158D0 | same |
| X3..X5 old ID, transform, new root | 0x005158D8; T = T_N·inv(T_robot) 0x005158DC..0x005158F6; FindRoot(P) 0x00515902 | same |
| X6/X7 Rejigger | 0x00847D7C..0x00847E84: both VERIFY guards; T into the OLD origin; SetParent(old, new); name += "_REJ"; current := new | NativeDelocalization.cs `RejiggerOrigins`; PoseOriginNames.cs |
| X8 RejiggerFailed | 0x0051591A..0x0051592A: VERIFY, then on to S12 | BlockWorld.cs |
| X10 UpdateObjectOrigins | 0x00620534..0x006206F4, lambda1 0x00628E18..0x00629300 | BlockWorld.cs `UpdateObjectOrigins` |
| X11/X11a map | 0x0051599E; UpdateOriginsOfObjects 0x0067E058..0x0067E1A8 | MapTiming.cs `UpdateOriginsOfObjects` |
| X12 faces | 0x005159A8; 0x004F5644..0x004F5866 (with the last-observed pose, `_nativeLastPose`) | NativeFaceCache.cs `UpdateFaceOrigins`; bound in VisionSystem.cs |
| X13 Flatten | 0x00847F34..0x00847FBE | NativeDelocalization.cs `FlattenOrigins` |

X10 now:
- converts each object through the pose tree;
- has the OriginFail path, with the erase rule;
- has the UnknownOriginID guard (0);
- searches non-unique objects for a counterpart (half-size f32 tolerance, 0x00502B06; `NativeClosestSpatialQuery`, which is 0x0061F8EC with the 0x00627F9A predicate);
- calls SetObservationTimes (0x00877706).

**Decided by the manager:**
- **OnRobotRelocalized (0x0051246C):** it reaches only VizManager rendering, so it has no effect on 1–4 and is not
  built (recorded in M11-044).
- **RejiggerFailed:** built.
- **Face origin update:** tagged M11-044 until a FaceWorld record is assigned.

## Tests

- **`LocalizeCrossOriginTests` (new):**
  - unit tests: X6, X7, X13, X11a, X12;
  - `LCO_X0_X16` (unit): the whole branch, through the next tick's 0x45 in the new origin;
  - `LCO_Production_ADelocalizedRobotThatSeesAKnownCubeRelocalizesIntoItsOrigin(pathInFlight)` (production): rendered
    frames; Delocalize is a direct call, which is labelled.
- **`M11RVisBuildTests` M11_043_*:** rewritten on real origin trees. They cover OriginFail, the guard,
  SetObservationTimes, the non-unique match/no-match and the tolerance bits.
- **`LocalizeToObjectSameOriginTests`:** the three cross-origin tests are rewritten to the engine path. They use a
  non-list root, so they take the UnknownOrigin → RejiggerFailed route; they are labelled unit, and that root's
  reachability is UNKNOWN.
- **`M3M4X23BuilderTests`, `ObjectStateReportTests`:** signature update only.
- **Results:** affected classes 901/901 (implementer). The verifier's run gave 154/154, and 20/20 passed after the F1
  change. The full gate: see the commit.

## Verifier findings

The verifier re-disassembled every step; it found the order, the arguments and the failure exits all match the
binary, and no lock inversion.

| Finding | Classification | Resolution |
| --- | --- | --- |
| **F1:** a path in flight is not rejiggered or replanned after the origin change (HandlePossibleOriginChanges 0x0064980C → RejiggerTargetsAndReplan 0x006499D0; the stack's origin inputs are unbound, M12-032) | BLOCKING | Made visible. At the end of the branch, a MISSING naming M12-032 and the origin change fires when `Motion.Path.IsActive` (BlockWorld `PathInFlight`, bound in CozmoRobot.cs). It is tested both ways by the production theory. The build is queued as PATH-ORIGIN-CHANGE-REPLAN-1. I did not bind the inputs, because ParamsOrigin's writer is not censused and an unset value would take the abort branch. This is a diagnostic-only change, so there was no re-verify round. |
| **Q1:** a non-unique non-MarkerlessObject still throws, after Rejigger | queued | It has no producer. Recorded in M11-043. |
| **Q2:** the UnknownOriginID VERIFY key | queued | Unreachable. Recorded in M11-043. |
| **Q3:** the retired-owner message cited M1-034 | queued | Relabelled "stack re-entrancy guard". |
| **Q4:** there is no multi-pair X19 test with a list origin | queued | LOCALIZE-MULTI-PAIR-CROSS-ORIGIN-TEST-1. |
| **Q5:** FaceEntry.LegacyProjection is not moved | observation | Recorded in M11-044; the same holds after Delocalize. |

Also fixed: the stale "Rejigger join MISSING" comments (M11MapMergeTests, M3M4X16BuilderTests), and the `unresolved`
text of M11-043, M11-044 and M13-032.

## What stays open

| Item | Owner |
| --- | --- |
| The null-O branch body (0x00515686..0x005156AC); the visible stop is kept and no caller passes a null O | M11-044 |
| Path replan on an origin change; other per-tick origin pollers | PATH-ORIGIN-CHANGE-REPLAN-1 (M12-032) |
| GetSameDistanceTolerance for other non-unique types; the UnknownOriginID VERIFY text; the lambda1 log formats | M11-043 |
| Other writers of the PoseInMapInfo+0x20 byte; the InvalidRemovalType warning; the removal functor body | M13-032 / M11-074 |
| A FaceWorld record for UpdateFaceOrigins | the manager |
| LocalizeToMat | LOCALIZE-TO-MAT-HISTORY-1 |

## Records

- **M11-044:** no settlement. Still open: the null-O body, LocalizeToMat, the path replan join and the LocalizeRobot
  NaN order.
- **M11-043:** no settlement, because of the non-MarkerlessObject tolerance, the VERIFY text and the log formats.
  Every reachable step of its path is now built.
- **M13-032:** no settlement. The Robot destructor teardown order and the other items in its `unresolved` remain.
