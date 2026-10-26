# Drive-off-charger test — operator guide

## What this test is for

Cozmo starts on his charger and drives off it. The program records every message the stack sends him and checks
them against the original Cozmo engine. **That checking is automatic.** You watch once and answer two questions;
your answers are saved as notes and never make anything pass or fail.

This is the check for the run-3 false cliff stop (`research/20261020-m5-drive-cliff-stop.md`): leaving the charger
must not raise the cliff setting too early.

## Setup (before you start)

1. Put Cozmo **ON the charger**, backed on so the contacts touch (his backpack lights show he is charging).
2. The charger is on the **floor**, with at least **30 cm** of clear, flat floor in front of it.
3. Don't touch Cozmo from the start until `TEST FINISHED` is printed, including while you answer the questions.
   If he is moved or lifted, the run can't be checked.
4. If he keeps driving towards an edge or an obstacle, press Ctrl+C. The run is still saved.

If Cozmo is not on the contacts when the program checks, nothing drives: put him on the charger and run again.

## Run

PC on Cozmo's Wi-Fi. From the repository root:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m13-driveoff-hw --resources re-analysis/obb/assets/cozmo_resources
```

To see what the run will show you, without a robot:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m13-driveoff-hw --preview
```

## The parts

| # | Part | Cozmo does | Your job |
| --- | --- | --- | --- |
| 1 | ON_CHARGER | sits on the charger (about 3 s) | nothing |
| 2 | DRIVE_OFF | drives forward off the charger, about 16 cm, and stops on the floor (up to 30 s) | **watch him**, then answer 2 questions |

Under a minute, plus about a minute to connect.

## Reading the results

Seven result lines, then one `OVERALL:` line, also in `SUMMARY.txt`:

- **OK:** the stack did what the original engine does.
- **PROBLEM:** the stack did something the engine would not. Needs review.
- **COULD NOT CHECK:** setup or evidence was missing; the reason is shown. Not a failure by itself.
- **SAVED FOR REVIEW:** recorded only (DRIVE_OBSERVATIONS).

Send the results folder back for review either way:
`re-analysis/acceptance/hardware/<date-time>-M13-DRIVEOFF-HW-<suffix>/`.

## Reference for review: what each check compares

The cliff-detect threshold is message 0x54 (`SetCliffDetectThreshold {u16}`). The travel is recomputed by the harness
from the received RobotStates, as the engine computes it: the sum of the XY displacement of
MoveRobotPoseForward(pose, −20 mm) between states, in f32, from the identity pose (M4-019 C7).

| Check | Automatic comparison | Engine source |
| --- | --- | --- |
| ON_CHARGER | On the contacts at the drive start, the platform flag is set and the threshold messages so far are exactly 50, 50 (first state; platform change). | M4-019 0x00512D80..0x00512DA2, 0x00511D44..0x00511DCA; M13-040 0x00512AAC..0x00512AB4, 0x00511990..0x00511C2D; M13-017 0x005C0B10 |
| HOLD_50 | After the contacts drop, no threshold message before the first state whose travel is more than 50 mm. | M4-019 0x00512E2A..0x00512E96 (`ble` at 0x00512E96), 0x00511D4C..0x00511D6A; M13-040 0x00513CD8..0x00513E2A |
| DISTANCE_400 | After that state, exactly two 400s (distance one-shot, platform exit), the first message being 400; one if the platform never cleared. | M4-019 0x00512E84..0x00512EA6, 0x00511DA0 |
| PLATFORM_EXIT | The platform clears once, and only after a state with the contacts off. The exit position is recorded against the 85.9 mm straight-drive figure, not graded. | M4-019 0x00511D4C..0x00511D6A; M13-040 0x00513CD8..0x00513E2A, 0x00514DB2..0x00514DD8; M13-024 |
| BEHAVIOUR_END | DriveOffCharger is runnable on the platform and ends on its own only off the platform with no action running. | M13-017 0x005C0B10, 0x005C0DA8..0x005C0E32 |
| DRIVING_ANIMATIONS | With no severe need selected (3) at Init, exactly one push (DriveOffCharger, 0x97,0x93,0x8F), +0x120 = 1, and one remove by name at stop; with a need selected (for example Repair or Energy Critical at connection, through the automatic wakeup), no push, flag 0, no remove. The push/remove calls are recorded where the behaviour makes them. | M13-017 0x005C0B34..0x005C0B50, 0x005C0D90..0x005C0DA4; M15-072 (selected need); M5-063 (what then plays, recorded only) |
| DRIVE_OBSERVATIONS | Recorded only: path lines (expected 156 mm = 96.0 + 60.0), robot cliff/stop reports, API events, your answers. | M13-017 0x005C09D4..0x005C09E6, 0x005C0C02..0x005C0C08, `driveOffCharger.json`; M13-013; M4-019 SC7 |

**Limits.** The behaviour is started and ticked by the harness (every 33 ms), not chosen by freeplay; its selection
is not covered. Grading stops 1.5 s after the behaviour ends (or at Ctrl+C), so later evidence is saved but not graded; the
operator is still asked not to touch Cozmo until TEST FINISHED. A
RobotStopped during the drive would end the behaviour in the engine (H4, RequestCurrentBehaviorEndImmediately
0x00535432..0x00535480); the harness-ticked behaviour keeps re-driving, so such a run is judged from the bundle. A check is COULD NOT CHECK when the robot's pose frame or origin changes (a localization), when a
delocalization restores 400, when the travel crossing falls within 0.01 mm of 50 or depends on the first state's term
from the identity pose, when the contacts did not drop before 50 mm, or when the platform cleared before 50 mm.
Wire timing is host capture time; only the order of the 400 against the crossing state is graded, and the lag is
recorded (`distanceSendLagMs`). No record is settled and no acceptance is performed by the run.
