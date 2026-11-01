# ChatGPT check 8, concrete reaction behaviours: Claude check (manager, 2026-10-31)

**Verdict: eight groups of real, shipped-reachable differences, the largest check result by effect on reactions already built. One label was
wrong and is corrected below; two open items are closed. Nothing settled; no status changed.** The report is
`research/20261031-check-8-chatgpt-extraction.md`. Its 152 table lines are 68 real rows (9 DIFFERENT, 20 MISSING, 20 SAME, 19 UNKNOWN) plus
84 coverage, provenance and unchecked-path lines; rows 19..23 repeat earlier rows. Rows 3, 5..14, 15, 16, 17 and 18 were re-read in the `.so`
and the C#. SAME rows were not re-read.

## Real differences (for the fix job)

| Rows | Difference | Evidence | Records |
|---|---|---|---|
| 5..9, 11 | **Six objectives are logged, never reported.** 0x1C cliff (0x00605394), 0x21 on back (0x00608844), 0x22 on face (0x00608C76), 0x1D frustration (0x00605FDC), 0x19 cube moved and absent (0x00602734), 0x24 shaken (0x00609316), each with true. `ReportBehaviorObjective` exists and other behaviours use it. This loses the app broadcast, the local 0x8B delivery and the event log. Keep the native branch conditions in those rows. | `.so`, C# | M7-019 |
| 3 | **Slope ignores severe needs.** The selected need at [[robot+0x264]+0x30]+0x14 picks 0x145 for Repair (0), 0x139 for Energy (1), else 0x1A8 (0x006080B2..0x006080DE). `OffTreadsBehaviors.cs:308` always plays 0x1A8. | `.so`, C#, Unity `NeedId` | M7-019 |
| 12 | **Shaken does not clear the severe expression.** The PLT the report left open resolves: when the selected need != 3 (Count), Init calls `SevereNeedsComponent::ClearSevereNeedExpression` (0x00609150..0x00609154). The C# has no such step. | `.so` | M7-019 |
| 13, 23 | **DizzyShakeLoop restarts.** The engine starts it once with numLoops 0 (0x0060918C). The C# replays it from the completion callback, which adds EndOfAnimation and silence at each pass. The code calls this a recorded gap, but `AnimationScheduler.Play` now takes numLoops, so check whether the reason still holds. | `.so`, C# | M7-019, M5 |
| 17 | **Frustration draws in the wrong order.** See the correction below. | `.so` | M7-019 |
| 16, 21, 22 | **Driving animations are not pushed.** Init pushes, Stop removes (0x00605C20, 0x00605D8E). The table the C# calls undecoded derives from the code as 0x00C73C00 (literal 0x00605CA0): start 0x94 DriveStartAngry, loop 0x90 DriveLoopAngry, end 0x8C DriveEndAngry. | `.so`, C# | M5-063 |
| 10 | **AcknowledgeCubeMoved keeps its target.** StopInternal 0x0060261A always writes -1 to +0x124. `CubeReactions.cs:398` clears only on Completed, so an interrupted run stays runnable. | `.so`, C# | M7-019 |
| 14, 15, 18, 27 | **ReactToOnCharger.** Init calls `SmartPushIdleAnimation(0x23F)` (0x00606CCA); the C# only logs. Tag 84 CancelIdleTimeout ends the behaviour (0x00606E70..0x00606E78, Update 0x00606E5C..0x00606E6C); the public `CancelIdleTimeout` reaches `_idleMessages` but not this behaviour. IsRunnableInternal is always true (0x00606C90); the C# requires OnCharger. Its action is TriggerLiftSafeAnimationAction (0x00606CF2); confirm PlayTrigger is equivalent. | `.so`, C# | M7-019 |

## Corrected

**Row 17.** The order differs, but the report named the draws wrongly. The first RandDblInRange takes s0/s2 from +0x124/+0x128, so it is the
**angle**, kept in s16 (0x00605E28, 0x00605E40). Then RandDbl(1.0) gives the sign: a draw below 0.5 negates the angle (0x00605E5E..0x00605E96).
The second RandDblInRange uses +0x11C/+0x120, the **distance** (0x00605EA6). So the engine draws angle, sign, distance, and the C# draws
distance, angle, sign. The C# also takes `rnd.Next(2)` and `NextDouble` from `Context.Random`; confirm those are the engine generator and the
same draws.

**Check 7's note on objectives.** It said most behaviours only log them. `ReportBehaviorObjective` exists with about a dozen callers and
delivers 0x8B locally, so FISTBUMP-STRATEGY-1 has its input. It is the log-only sites that are missing.

## Open, for later jobs

- **Log-only objective sites:** `grep "objective achieved"` also finds `ChargerBehaviors.cs:232`, `FaceBehaviors.cs:477` (InteractedWithFace, a FistBump source) and older files. Compare each with the engine in the fix job; the report did not cover them.
- **Rows 24, 25, 26, 31 (AIWhiteboard +0x74):** Hiccup animation choices. They go with the Hiccup strategy job.
- **Rows 28, 30 (SparkGate):** go with the Sparked work.
- **Rows 4, 20 (SayTextAction):** naming the face on pickup needs an action that is not built. Real, but larger than this job.
- **Rows 29, 54:** DriveStraight's third bool; rows 53, 63, 65: charger and cliff messages left UNKNOWN, including whether GoingToSleep and StartIdleTimeout should be sent rather than kept in a local list.
- **The 84 coverage lines** are honest but empty: Update, Stop and callbacks of many classes remain unread, so a further pass on these behaviours has value.

## Method note

The requested list of MISSING notes was the most useful addition. Asking "does the input exist now" turned old notes into small fixes
(objectives, driving animations, loop). The report's labels still need reading against registers: row 17's quotes were right and its draw
names were not.
