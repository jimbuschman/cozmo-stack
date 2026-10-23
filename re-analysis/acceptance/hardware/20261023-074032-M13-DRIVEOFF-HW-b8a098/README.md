# M13-DRIVEOFF-HW run 1 (operator, 2026-10-23 07:40): PASS

Harness `m13-driveoff-hw` at d2c72b24, guide `acceptance/hardware/scripts/m13-driveoff-hw-1.md`, check
`research/20261023-m13-driveoff-hw-claude-check.md`. No run errors; startup clean (readiness 3604 ms, 0 error logs).
Manager judgement from the bundle.

| Check | Result | Evidence |
| --- | --- | --- |
| ON_CHARGER | MATCH | Two 50s at 12648 ms (wire 160, 161), platform set, contacts on. |
| HOLD_50 | MATCH | Contacts off at 5.2 mm of travel (42790 ms); no threshold message until the crossing state. |
| DISTANCE_400 | MATCH | Crossing state 51.13 mm (previous 47.93, well clear of the limit); 400 on the wire 63 ms later; exit 400 at 43699 ms; exactly two. |
| PLATFORM_EXIT | MATCH | One platform-false at 43666 ms, after the contacts dropped. Exit at 90.8 mm forward (heading change -0.0016 rad), against the 85.9 mm straight-drive figure: an observation, not graded. |
| BEHAVIOUR_END | MATCH | Runnable, drive Success, ended on its own off the platform with no action (45116 ms). |
| DRIVING_ANIMATIONS | KNOWN_GAP | SevereNeeds writer MISSING reported (R-M15-SEVERE-INPUT). |
| DRIVE_OBSERVATIONS | observation | One path line of 156 mm (96 + 60) at 100 mm/s, accel 200, decel 500 (the 0x005470F0 constructor values from the joins check); no robot cliff, PotentialCliff or RobotStopped. Three default driving-animation plays completed. Operator: drove off yes, stopped on the charger no. |

**What this shows.** On hardware, the run-3 false cliff stop is gone: the threshold stayed 50 off the contacts until
more than 50 mm of travel, then 400 once, then 400 at the footprint exit, as M4-019 C7/C8 and M13-040 require.

**What it does not do.** It raises no record's provenance (hardware success never does) and settles nothing. M13-017
and M13-040 stay IMPLEMENTATION_GAP: the shared configuration/map/cache pieces (B-M13-15..18), the SevereNeeds writer
(M15) and freeplay selection of the behaviour remain. The 4.9 mm difference from the 85.9 mm exit figure is recorded
for the footprint records (M13-024) without a claim either way.
