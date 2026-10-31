# ChatGPT check 2, activity strategies: Claude check (manager, 2026-10-31)

**Verdict: the method works. 13 of its rows are real differences the shipped configs reach, 5 more wait on one operator decision, and
none was a false alarm about the code. Nothing settled; no status changed.** The report is
`research/20261031-check-2-chatgpt-extraction.md`, read against `Activities.cs` blob c7e69672, the same file as main. It covers 48 rows:
26 DIFFERENT, MISSING or EXTRA, 16 SAME, 6 UNKNOWN. Every row below was re-read in the `.so` and the C#. Rows not named were not re-read.

This code is live: both `FreeplayStack.Create` and `NativeBehaviorRuntime` (line 124) load through `ActivityTreeLoader`, and
`FreeplaySystem` calls WantsToStart and WantsToEnd.

## Real differences (for the fix job)

| Rows | Difference | Shipped reach | Records |
|---|---|---|---|
| 9, 10 | `Activities.cs:809` draws RNG and re-rolls on every WantsToStart that passes. The engine draws only after a positive cooldown has really been waited out (branches to 0x005B5364 skip it). | All activities: extra RNG use, and a never-run activity with randomness rolls early | M15-003 |
| 15 | No test that the on-treads event is later than the activity's last start (0x005B53B0..0x005B53BC). | PutDownDispatch (5 s) can re-fire on the same event | M15-003 |
| 23 | NeedBasedCooldown computes at roll time. The engine subscribes to NeedsState (tag 0x62) and calls SetCooldown on each message, which also overwrites the cooldown in force. | Singing | M15-098 |
| 24 | `Activities.cs:821` adds a Critical check at start that the engine's Needs start does not have. | NeedsSevereLowEnergy | M15-099 |
| 25, 26 | Needs end has no higher-priority path and no ClearSevereNeedExpression. Only the nested `need` is parsed, not the strategy. | NeedsSevereLowEnergy (Repair Critical) | M15-099 |
| 27, 28, 29 | Pyramid start has no unlock test, no base-cache test, no 0.4 roll every 300 s. | BuildPyramid | M15-097 |
| 30 | Pyramid end never fires on BuiltPyramid. `NativeActivityObjectiveOwner` subscribes to 0x8B but is not a strategy recipient. | BuildPyramid runs on to the 300 s limit | M15-097 |
| 31 | SevereNeedTransition end is always true after the floor. The engine returns not WantsToRun. | The three severe activities | M15-099 |
| 32 | Spark keeps the default 60 s should-end. The engine sets FLT_MAX (0x005B4E98..0x005B4EA2), and no shipped spark config sets the key. So a spark activity older than 60 s ends once its behaviour finishes. | All 14 sparks | No record of its own |

Its other Spark claim makes no difference: `FreeplaySystem.cs:405` only offers a spark's bucket when that spark is requested, so the
start result is the same.

## Waits on an operator decision

Rows 18..22, PlayWithHumans: no rejection keys, RequestGameStart, DenyGameStart or predicates; `Activities.cs:834` always returns false
with the comment "the game request component is the app's". That is an unrecorded exclusion of a robot behaviour, which needs the
operator. M15-096 stays open until then. **Decided 2026-10-31: not supported.** M15-096 is COMPATIBILITY_POLICY with live_path false, on this check's evidence.

## Not blockers

- **Row 11:** already recorded on M15-003 and M15-005.
- **Rows 12, 16:** a 1e-5 boundary only. Fix them with the rest; they cost nothing.
- **Rows 13, 14:** real differences in the code, not reachable by shipped data. MoodScorer does the mean and the 60-tick delta
  (0x0067C9EE..0x0067CA72), but the only shipped scorer (Socialize) has one entry, `trackDelta` false, and sets both keys. The sum and
  mean agree, and the near-zero veto (1e-5) gives the same 0. Record this on the fix; do not widen it.
- **Rows 17, 33, 34, 54:** inputs this system never produces (no Features owner, unknown type, null strategy). Not built, per "What we
  reproduce".

## Corrections to the report

- **Row 21:** FPPlayWithHumans +0x4C is [Robot+0x264]+0x18, not RequestGameComponent, which row 19 itself places at +0x20. The queue 5
  note's AIWhiteboard reading stands.
- **Row 30:** "game-to-engine" for tag 0x8B was not confirmed.

## Records

Row 32 has no record of its own. M15-100, the Spark strategy, was added with operator approval on 2026-10-31.

## Method note

Comparing built code, with a repo it can read, gave more than any reading queue. Weakness: it does not weigh shipped reachability, so
ask for it per row. It correctly marked its limits as UNKNOWN.
