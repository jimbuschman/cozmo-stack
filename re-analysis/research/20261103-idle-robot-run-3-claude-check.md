# Third left-alone robot run: Claude check (manager, 2026-11-03)

Bundle `20261103-081926-M13-IDLE-HW-27a00c` (kept by the operator, not in git), commit `d64a2ed6`, 240 s recorded.
Startup ready after 666 ms, 0 error-level engine logs, no run errors, no exception in the stack log. The operator moved
the robot away from the desk edge by hand a few times; the pickup reactions below are from that.

**Verdict: idle 1.4 of 240 s (run 2: 165.7 s). The VisitInterestingEdge close branch is reached from real frames. Every
cliff stopped the robot and every cliff marker was later removed by the predicate built in `d64a2ed6`. No record settled:
a hardware run does not raise provenance.**

The run before this one (same day, commit `0dae2043`) ended at 88.9 s with an unhandled NotSupportedException from the
UpdateMarkerlessObjects footprint stub right after ReactToCliff. That is fixed in `d64a2ed6` (M11-037 note).

## Behaviours

VisitInterestingEdge 84.4 s (11 entries), FindFaces_socialize 71.6 s, PounceOnMotion 31.9 s (3), ReactToCliff 11.2 s (6),
LookInPlaceForUnknown 8.7 s, ThinkAboutBeacons 6.7 s, BringCubeToBeacon 6.3 s (3), FirstLookIntro, AcknowledgeObject,
PutDownDispatch, DriveOffCharger; reactions to the operator's handling: ReactToReturnedToTreads (2), ReactToRobotOnSide,
ReactToUnexpectedMovement, ReactToMotorCalibration.

## VisitInterestingEdge on real frames

Five gathers reached S2. Four ended `GatheringAccurateEdge.Close` then S3 "Observing edges from close distance", with
"moving closer" 5.06, 3.31, -3.53 and 4.04 mm (55.3, 149.4, 164.7, 244.3 s). One ended `Done` after four
`RegionTooSmall` (213.2..215.8 s). This is the branch the POSE-SOLVEPNP-1 check could not establish from simulated frames
(that harness's run-to-run variation is still a queued item). The negative "moving closer" is an observation: the formula
was not re-checked here.

## Cliffs

Six `HandleCliffEvent.Detected` with flags 0x03 (73.0, 113.7, 117.0, 138.3, 224.8, 227.9 s), each followed by
`BehaviorReactToCliff.GotCliff`. Six `UpdateMarkerlessObjects.RemovingIntersectWithRobot` (objects 2, 4..8).

## Still open

- `M8-013 scored chooser input unavailable` logged 296 times (run 2: 3271), not a stall here.
- 170 distinct MISSING texts reported; none new against the vision path changed this week.
- The unhandled-exception path of the previous run: the processor thread catches exceptions (`VisionSystem.cs:1187`),
  so the route by which the stub's exception escaped is not established. The operator's console trace was truncated.

## Pounce

Three PounceOnMotion entries (36.5, 121.8 s and later), each through `ag_pounceonmotionpounce`, `ag_pounceonmotionsuccess` and `ag_pouncedriveface_getout`, every action SUCCESS, no Pounce MISSING beyond the factory note for other configurations. This meets the condition in `20261102-pounce-body-claude-check.md`: **Hiking_PounceOnMotion is the second finished behaviour.**

## Progress

19 records settled across 43 start-path jobs, 2 of 178 finished.
