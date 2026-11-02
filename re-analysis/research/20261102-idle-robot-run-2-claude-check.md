# Second left-alone robot run: Claude check (manager, 2026-11-02)

Bundle `20261102-060600-M13-IDLE-HW-e7b1ac` (kept by the operator, not in git), commit `0a6b6792`, 240 s recorded.
Startup ready after 3242 ms, 0 error-level engine logs, no run errors.

**Verdict: Pounce works on real frames. Hiking now stalls on VisitInterestingEdge instead, and that is most of the idle
time. Idle 165.7 of 240 s (first run: 176 of 240 s).**

## Timeline

| Time (s) | Activity | Behaviour |
| --- | --- | --- |
| 25.6 | Socialize | FindFaces_socialize, 17.7 s |
| 43.4 | Hiking | FirstLookIntro 1.4 s, ThinkAboutBeacons 2.8 s, PounceOnMotion 10.6 s |
| 58.2 to 103.4 | Hiking | nothing (45 s) |
| 103.4 | Socialize | FindFaces 20 s |
| 123.7 to 183.7 | Hiking | nothing (60 s) |
| 183.7 | Socialize | FindFaces 20 s |
| 204.4 to 264.5 | Hiking | nothing (60 s) |

PlayAlone is entered three times for 0.1 s each and passes straight to Hiking.

## Pounce on real frames

PounceOnMotion ran its whole sequence on the robot: point turns toward motion, `anim_pounce_success_02` (52.7 s),
`anim_pounce_lookgetout_01` (58.2 s), then a normal end. No Pounce MISSING was logged. This is the first real-frame run of
M11-098 and M15-107. M15-107 still needs its positive body read before it is settled.

## The stall

From 49.2 s to the end, `MISSING: M8-013 scored chooser input unavailable; retaining current behavior` is logged 3271 times,
only while Hiking is the activity. At the same moment the log shows, once each (ReportMissing logs a text only once):

- `M11-079/M11-089 CalculateInterestingRegions: original map or nonempty border producer unavailable`
  (`AIInformationAnalyzer.cs:80`);
- `M11-079/M11-089 original analyzer border result unavailable` (`VisitInterestingEdgeBehavior.cs:28`).

Path: Hiking's scored chooser evaluates `Hiking_VisitInterestingEdge` (`hiking.json` line 89). Its `IsRunnableInternal`
accepts only an analyzer result known to be empty. After the robot turns during Pounce, the memory map has borders, so the
analyzer has no result, readiness is marked unavailable, and `Activities.cs:447` returns `InputUnavailable`. The
FreeplaySystem keeps the current behaviour (none) until Hiking's 60 s end.

Classification: implementation gap. Nonempty border generation (M11-079), the type-9 data class (M11-089) and the
VisitInterestingEdge lifecycle (M11-092) are named IMPLEMENTATION_GAPs. Only the empty-border branch was built in
PLAYALONE-MOVES-ON-1.

**Why simulation missed it:** the rig's memory map stays empty, so the analyzer always reports empty and the behaviour reads
as not runnable. A real robot fills the map as it moves.

## Not shown by this bundle

Whether cubes were seen: the bundle does not log observations. The operator noted that Cozmo hardly moved during PlayAlone
and Hiking, which matches the timeline.

## Records

No status changes. M15-107 keeps its gap until its body is read. M11-079, M11-089 and M11-092 now block the left-alone loop on a
real robot.

## Next

VISIT-EDGE-REAL-MAP-1 is ordered after REWARD-GATE-1 as one job: nonempty border generation, type-9 data and the
VisitInterestingEdge lifecycle. Its done test is a left-alone probe whose memory map has content.
