# ChatGPT check 4, BehaviorManager: Claude check (manager, 2026-10-31)

**Verdict: the tick loop mostly holds. Of 33 rows, 18 are SAME, 4 are real gaps, and all 4 already log MISSING in the code. Unlike checks
2 and 3, it found nothing silent. Nothing settled; no status changed.** The report is
`research/20261031-check-4-chatgpt-extraction.md`. Its 33 rows are 10 DIFFERENT or MISSING, 18 SAME, 5 UNKNOWN. Every row named here was
read in the C#, and rows 3, 4 and 5 were re-read in the `.so`. A search of `cozmo-stack/src` confirms no production code assigns
`ActionListIsEmpty`, `ReactionTriggerTransitionSender` or `UpdateRobotPropertiesForReaction`. Rows not named were not re-read.

## Real differences (for the fix job)

| Rows | Difference | Evidence | Records |
|---|---|---|---|
| 3 | **First-action gate not applied.** The engine returns from the reaction check until the first action has been queued: `+0x4C |= !IsEmpty` (0x005A3558..0x005A357A). `ActionListIsEmpty` is unbound, so the stack logs MISSING once and reacts. | `.so`, `BehaviorManager.cs:483` | M10-004 |
| 12 | **Default head and lift restore never runs.** The same unbound delegate blocks it. It needs SetDefaultHeadAndLiftState to have been sent, and that path also logs MISSING at `:597`. | `BehaviorManager.cs:1104-1122` | M8-012 |
| 4 | **ReactionTriggerTransition is never sent** to the C# API (0x005A20B8..0x005A20D6). It fires whenever the trigger changes. | `.so`, `:1014` | M8-012 |
| 5 | **Robot +0x2C7 and MovementComponent +0xD4 are not written** when a reaction starts or ends (0x005A20DA..0x005A2110: 1 on entry, 0 on exit, nothing on reaction to reaction). `:547` shows the stack treats +0xD4 as always false, so row 25's unlock decision can differ during a reaction. What reads +0x2C7 is unread. | `.so`, `:1016` | M8-012, M10-004 |

Rows 3 and 12 share one cause: bind `ActionListIsEmpty` to the real action list.

## Not new or not blockers

- **Row 9:** the spark id join to freeplay, already on M15-006 and M15-058.
- **Rows 10, 11:** the UI game request. The report calls this excluded by the app boundary, but no such exclusion is recorded. It belongs
  with the operator's PlayWithHumans decision.
- **Rows 7, 8:** a log difference and DAS telemetry; neither blocks.
- **Row 6:** stopping a reaction through a lock skips the info path. `IActivity::SmartDisableReactionsWithLock` passes stopCurrent 1
  (0x005B3B56), but activities are chosen only with no reaction running, so reach is doubtful. Open.
- **Rows 15, 25, 28, 30, 34:** UNKNOWN as reported. Row 25 follows row 5.

## Agreement with check 3

Rows 16, 17 and 20 confirm the check 3 stop-order finding: the manager passes the current behaviour to the chooser, and
SwitchToBehaviorBase stops it afterwards. So the fault is in `FreeplaySystem.EndActivity`, not the manager.

## Method note

Clean rows here cover the stack's most reworked code. Silent errors were in older code whose records say built; aim checks there.
