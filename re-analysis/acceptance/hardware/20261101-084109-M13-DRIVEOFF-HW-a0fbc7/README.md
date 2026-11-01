# M13-DRIVEOFF-HW run 4, Freeplay route (operator, 2026-11-01 08:41): PASS, five graded checks MATCH

- **Harness:** `m13-driveoff-hw --freeplay` at ca5c1ff4. First robot run through canonical Freeplay: `FreeplayStack.CreateNative`, `ActivateHighLevelActivity("Freeplay")`, `ActivateSpark(9)`, then the engine's own chooser picks SparksRollBlock and DriveOffCharger.
- **Startup:** clean (readiness 602 ms, 0 error logs, no run errors).
- **Judgement:** the manager's, from the bundle.

| Check | Result | Evidence |
| --- | --- | --- |
| ON_CHARGER | MATCH | Two 50s before the drive; platform and contacts set at drive start. |
| HOLD_50 | MATCH | No threshold send until the crossing state (52.38 mm; previous 49.38). |
| DISTANCE_400 | MATCH | 400 at 14786 ms, 70 ms after the crossing, and the exit 400 at 15502 ms. |
| PLATFORM_EXIT | MATCH | One platform-false at 15496 ms; 93.3 mm forward, heading change 0.027 rad, against 85.9 mm. Observation only. |
| BEHAVIOUR_END | MATCH | Runnable, started, ended on its own, off the platform, no action running. |
| DRIVING_ANIMATIONS | COULD NOT CHECK | Expected on this route: public APIs do not show the calls. Graded MATCH on run 3. The bundle's text "The behaviour did not start" is a harness wording fault, corrected afterwards. |
| DRIVE_OBSERVATIONS | observation | Operator: drove all the way off the charger. |

## Differences from run 3

- **Spark get-in first.** `anim_sparking_getin_01` ran on the charger. Contacts dropped at 9611 ms and bounced at 10151 and 10391 ms, before the drive started at 13898 ms.
- **One robot stop mid-drive.** RobotStopped at 14896 ms, cliff events at 15384 ms (sensors 3, then 0), then `anim_reacttocliff_huh_01`. Run 3 had none. The stop cancelled the actions (0x02000000), and DriveOffCharger drove a second 156 mm line.
- **Finding:** at that stop the engine calls `RequestCurrentBehaviorEndImmediately`. The stack reported `MISSING: M8-019` and left the behaviour running, so it re-drove. The cause of the stop itself is not established from this bundle. Job M8-019-END-1.
- **After the drive:** the get-in repeated about every 4.6 s until the harness ended the run. On main b1c2f224, with SPARKED-LIFECYCLE-1, the same route offline plays one get-in and holds at a visible MISSING for unbuilt SparksLookInPlace.

No record's provenance is raised. M13-017, M13-040 and M7-026 gain this bundle as hardware verification.
