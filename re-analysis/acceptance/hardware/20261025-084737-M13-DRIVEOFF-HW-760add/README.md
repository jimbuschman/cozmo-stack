# M13-DRIVEOFF-HW run 2 (operator, 2026-10-25 08:47): PASS, no regression

Harness `m13-driveoff-hw` at df0d13b2 (after B-M13-19..28), guide `acceptance/hardware/scripts/m13-driveoff-hw-1.md`.
No run errors; startup clean (readiness 4865 ms, 0 error logs). Manager judgement from the bundle. Purpose: confirm the
native map writers and the removal of the legacy map sync did not change the drive-off.

| Check | Result | Evidence |
| --- | --- | --- |
| ON_CHARGER | MATCH | Two 50s at 13275 ms (wire 208, 209), platform set, contacts on. |
| HOLD_50 | MATCH | Contacts off at 1.6 mm of travel (21301 ms); no threshold message until the crossing state. |
| DISTANCE_400 | MATCH | Crossing state 50.07 mm (previous 46.81); 400 on the wire 22 ms later; exit 400 at 22341 ms; exactly two. |
| PLATFORM_EXIT | MATCH | One platform-false at 22310 ms, after the contacts dropped. Exit at 87.0 mm forward (heading change -0.0045 rad) against the 85.9 mm figure (run 1: 90.8 mm); observation, not graded. |
| BEHAVIOUR_END | MATCH | Runnable, drive Success, ended on its own off the platform with no action. |
| DRIVING_ANIMATIONS | KNOWN_GAP | SevereNeeds writer MISSING reported (R-M15-SEVERE-INPUT). |
| DRIVE_OBSERVATIONS | observation | One 156 mm path line; no robot cliff, PotentialCliff or RobotStopped; same action sequence as run 1 (26:0, 26:0x02000000, 26:0, 8:0). Operator: drove off yes, stopped on the charger no. |

**New since run 1, not graded:** `MISSING: M11-048 UpgradeRootLevel` and `ShiftRoot` fired 21 times each between 22131
and 23270 ms, just after the footprint exit. The native tree writers (B-M13-19..21) now run on the live path. Their
inserts that fall outside the initial root are dropped, where the engine would grow or shift the root. The drive-off
wire is unaffected. The map content after about 9 cm of travel is incomplete until M11-048 is built (research row 65,
R-M11-ROOT-GROWTH, answered 817d279f).

No record's provenance is raised and nothing settles. M13-017/M13-040 stay IMPLEMENTATION_GAP.
