# M13-DRIVEOFF-HW run 3 (operator, 2026-10-26 07:28): PASS, all six graded checks MATCH

- **Harness:** `m13-driveoff-hw` at a7af18cd, after B-M11-19..21, B-M15-12/13, B-M7-4 and B-M13-29/30/30a, plus the
  DRIVING_ANIMATIONS grading fix. Guide `acceptance/hardware/scripts/m13-driveoff-hw-1.md`.
- **Startup:** clean (readiness 4626 ms, 0 error logs, no run errors).
- **Judgement:** the manager's, from the bundle.
- **Purpose:** confirm that root growth, the type-10 border stamp, the live SevereNeeds owner with the automatic
  wakeup, the robot-lifetime BehaviorManager and the planner import gate did not change the drive-off, and grade the
  driving-animation push for the first time.

| Check | Result | Evidence |
| --- | --- | --- |
| ON_CHARGER | MATCH | Two 50s at 9786 ms (wire 172, 173); platform and contacts set at the drive start. |
| HOLD_50 | MATCH | Contacts off at 2.9 mm of travel (13549 ms). No threshold message until the crossing state (51.33 mm; previous 48.14). |
| DISTANCE_400 | MATCH | 400 at 14230 ms (wire 1062, 79 ms after the crossing state) and the exit 400 at 14503 ms. Exactly two. |
| PLATFORM_EXIT | MATCH | One platform-false at 14500 ms, after the contacts dropped. The exit is at 90.8 mm forward, heading change 0.0007 rad, against 85.9 mm. Observation only (run 1 90.8, run 2 87.0). |
| BEHAVIOUR_END | MATCH | Runnable; drive Success; ended on its own, off the platform, with no action running. |
| DRIVING_ANIMATIONS | MATCH | First graded run. Selected severe need 3 before and after Init. One push `DriveOffCharger 97,93,8F`, +0x120 = 1, one remove `DriveOffCharger` at stop (M13-017 0x005C0B34..0x005C0B50, 0x005C0D90..0x005C0DA4). |
| DRIVE_OBSERVATIONS | observation | One 156 mm path line; no robot cliff, PotentialCliff or RobotStopped. The action sequence is the same as runs 1 and 2: 26:0, 26:0x02000000, 26:0, 8:0 (sub 0x02000000). Operator: drove off, yes; stopped on the charger, no. |

## Compared with run 2

- **Root growth:** `MISSING: M11-048 UpgradeRootLevel/ShiftRoot`, which fired 21 times in run 2, is gone. B-M11-19
  holds on hardware: the tree grows past the initial root during the drive.
- **New MISSINGs, all expected:** the five unbound reaction rows from B-M7-4 (FistBump, Hiccup, ReactToSparked; Major
  Frustration and Ram before Freeplay), each logged once at robot construction.
- **Automatic wakeup:** it has no trace, as expected. No need was Critical, so the wake handler queried both needs and
  selected nothing (the selected need stayed 3, as the DRIVING_ANIMATIONS row shows).

## Found in review (queued, not a run failure)

`MISSING: M11-037/M11-044 map stamp input ... Robot::GetLastImageTimeStamp 0x00516EC0` (`BlockWorld.cs:832`). The
object map add/remove stamp (0x0067EFCE/0x0067F0A6/0x0067F32C) still reads a host stand-in:
`VisionSystem.cs:100`, the max of the raw camera frame and the frame clocks. It should read VisionComponent+0xE8, which
B-M11-21 bound for the overhead border. Job B-M11-22.

No record's provenance is raised and nothing settles. M13-017 and M13-040 stay IMPLEMENTATION_GAP.
