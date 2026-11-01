# ChatGPT check 9, depth pass on four reactions: Claude check (manager, 2026-10-31)

**Verdict: the depth pass mostly held, with 20 of 33 rows SAME, covering ReactToCliff, ReactToPickup and ReactToUnexpectedMovement. One new
behavioural difference, in ReactToMotorCalibration. Nothing settled; no status changed.** The report is
`research/20261031-check-9-chatgpt-extraction.md`. Its 33 rows are 9 MISSING, 3 DIFFERENT, 1 EXTRA, 20 SAME. Rows 11, 13, 14 and 35 were
re-read in the `.so` and the C#. SAME rows were not re-read.

## Real difference (added to REACTION-BEHAVIOURS-FIX-1)

| Rows | Difference | Evidence | Records |
|---|---|---|---|
| 13, 14, 35 | **ReactToMotorCalibration ends on the wrong signal.** The engine ends only when a tag 30 message arrives and IsHeadCalibrated and IsLiftCalibrated are both true, through StopActing(true, false) on a 5 s WaitAction (0x00606742..0x00606758, 0x00606798, 0x00606656). The C# polls four flags each tick, including two `Calibrating` flags the handler never reads. Whether this changes timing is hardware-dependent, but the rule is defined, so follow it. | `.so`, C# | M7-019 |

The not-tag-30 path at 0x0060679E is a failed check for a tag the behaviour does not subscribe to, so it is unreachable.

## Closed

**Row 11 is SAME on the production path.** `DriveStraightAction` defaults `shouldPlayAnimation` to true, and `BindFreeplayInputs` builds it
with distance and speed, matching the engine's true (0x0060533E, 0x00605344). The remaining MISSING is only the unbound test path.

## Not blockers

- **Rows 3, 5, 6:** DAS events `robot.cliff_detected`, the motor calibration start event and `CalibratingHead`. Log only under AGENTS.md; add them with `EmitSourceLog` if cheap.
- **Row 4:** already in check 8.
- **Rows 15, 17:** the AIWhiteboard and SparkGate rows, already routed to Hiccup and Sparked.
- **Row 18:** SayTextAction, already listed; it also needs named faces, which depend on the face recognition boundary.
- **Row 12:** the local RobotCliffEventFinished recipient stays open.
- **Row 19:** host-readiness guards; unreachable in production.

## Method note

The "no placeholder rows" rule removed the padding, and the end list named what was left unread. The result is a useful stop signal: these
three behaviours are in better shape than the older reactions around them, so the next check should move to another area.
