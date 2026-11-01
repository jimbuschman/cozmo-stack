# Claude check: five start-path jobs and the first robot run through Freeplay

Date: 2026-11-01. Branch `codex-m13-manager-2` at `111c8481`, merged to main at `b1c2f224`. Full suite on the merge:
7314/7314, 0 skipped. Jobs: LOOK-FACE-CUBE-EXECUTION-1, M8-CHOOSER-DISPATCH-1, ORDINARY-FREEPLAY-1,
NEEDS-EXPRESSION-ADMISSION-1, SPARKED-LIFECYCLE-1.

**Verdict: PASS for all five, with one reach finding.** Ordinary Freeplay now starts a behaviour on its own, which was
the point of the batch. It then holds in PlayAlone, so ORDINARY-FREEPLAY-1's last done-test is only partly met.

## Plain probe (temporary, never committed)

Resource-backed `CozmoRobot`, ordinary Freeplay, no pickup or forced activity, 5 simulated minutes, a simulated robot
that acknowledges motors and completes animations. Motion is instant, so counts are not real pace.

| Scene | Result |
|---|---|
| Off charger, empty | Socialize, FindFaces_socialize at once. After 2 scan passes, Socialize gives up (phase 6) and Freeplay picks PlayAlone. FindFaces keeps running to the end, 334 passes. |
| Off charger, one cube | Same, 335 passes. One cube cannot satisfy KnockOver. |
| On charger | DriveOffCharger at once. The rig does not move the body along the path, so the frustration loop is a rig artefact. |
| Spark 9, off charger | One get-in, then a hold at `M8-013 strict priority reached unavailable readiness: SparksLookInPlace`. |

Without motor completion the same run looks hung in FindFaces; that was the rig, not the stack.

## Reach: where it holds now

PlayAlone's Scoring chooser lists `SparksFireTruckAlarm`, unbuilt: `M8-003 configured scored owner/input unavailable`.
`FreeplaySystem` then returns `current` (`_childUnavailable`), so FindFaces stays running under PlayAlone past its 25 s
`activityShouldEndDurationSecs`. The engine asks the new child at 0x005AE518 and returns its answer, so a null there
stops FindFaces and the picker moves on. The stack cannot know that answer until FireTruck's readiness exists. This is a
visible MISSING and not invented behaviour, but a robot left alone scans forever and never reaches Hiking or NothingToDo.

## Checked in the .so and the C#

| Path | Addresses | Result |
|---|---|---|
| Socialize phase table | 0x005B02DC..0x005B0682, table 0x005B02E6 | Six cases match `NativeActivityObjectiveOwner.Desired`. Nonnull base result returns first. Phase 1 exit: iterations minus snapshot >= +0x80 (unsigned), null result, phase 6. Phase 2 fallback to search, phase 3 random pick, phase 4 run-count exit, phase 5 identity and running. |
| Socialize population | 0x005AFF6C..0x005B0170 | Map cleared; unlock gate only when not 0x55; probability below 1 draws RandDbl and skips on >=; count drawn and summed per objective. |
| Socialize objective recipient | 0x005B07BC.. | Phase 2 with objective 12 goes to 3. Otherwise a found count decrements and erases at zero, remaining defaults to map size, phase 4 with none left stops next action and goes to 5. |
| Parent picker | 0x005AE29C..0x005AE762 | Read for the reach finding; the null-child retry matches the C# `pick` path. |
| ExpressNeeds | ctor/loader 0x005EE144..0x005EE3FC; predicate 0x005EE570..0x005EE5DC; graph 0x005EE5E4; Init 0x005EE690..0x005EE788; Resume 0x005EE7D4; Stop 0x005EE7D8; dtor 0x005EE7EC; stamp 0x005EE89E | `shouldClearExpressedState` to +0x134 (Stop) and `caresAboutExpressedState` to +0x135 (predicate). Critical is 3. Strict f32 cooldown plus last success below now. Turn with PI, ignored failure, then plain triggers (1, true, 0, 60 s, false). Resume returns 1. Stamp only when the result byte 3 is 0. All match. |
| FindFaces look-up | 0x005C1DB6..0x005C1E0E | Image time before the face read; turn when stamp nonzero and (maxAge 0 or image <= stamp+maxAge, unsigned); otherwise base class. |
| FindFaces scan | S1 0x005E28D2..0x005E2942, S3 0x005E2C66..0x005E2CD2, helper 0x005E29B8.., S7 0x005E35A2 | S1 uses opposite direction; S3 main; direction 0 is negative; body draw before head draw; head-change count drawn after the action is built; iteration increment. All match. |

Read in the handoffs and tests, not in the .so here: Look lifecycle, chooser dispatch, Sparked lifecycle, GetIn.

## Robot run

`re-analysis/acceptance/hardware/20261101-084109-M13-DRIVEOFF-HW-a0fbc7`, at ca5c1ff4, first run through canonical
Freeplay. Five graded checks MATCH; DRIVING_ANIMATIONS could not be checked on this route, as expected; operator confirms
Cozmo drove all the way off. Full judgement in the bundle's README.md.

**One defect shown by the run:** a RobotStopped arrived mid-drive and the stack hit `MISSING: M8-019`, so DriveOffCharger
was not ended and drove a second line. The cause of that stop is not established.

## Records

- **M15-082 settled,** EXACT_SOURCE on its own lifecycle, both owners.
- **M15-101 to M15-104 added** as IMPLEMENTATION_GAP: Look, ActivitySocialize, FindFaces with scan core, GetIn. These were
  proposed in the handoffs. None settled: Socialize and FindFaces have unbuilt branches and no robot run; Look and GetIn
  were not read here.
- M13-017, M13-040 and M7-026 gain the bundle as hardware verification. Provenance does not change.
- M8-013, M15-006, M15-008 and M15-059 stay open, as the handoffs say.

## Progress

**DriveOffCharger counts as the first finished behaviour:** path read, built, checked, four robot runs, records settled,
with what is left named (M8-019 at a robot stop, driving animations graded on run 3 only). 1 of 178.
Records settled across the 23 start-path jobs: 6.
