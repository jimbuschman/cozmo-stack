# ChatGPT check 7, reaction trigger strategies: Claude check (manager, 2026-10-31)

**Verdict: three small differences are real and shipped-reachable, and one of them means a shipped reaction can never fire. One reported
difference is wrong. Most of the rest is the three unbuilt strategies (FistBump, Hiccup, Sparked), already on M10-003 and M10-016. Nothing
settled; no status changed; no new record needed.** The report is `research/20261031-check-7-chatgpt-extraction.md`. Its 68 rows are 2
DIFFERENT, 22 MISSING, 28 SAME, 16 UNKNOWN, plus one provenance row. Rows 3, 5, 7, 8, 9, 10 and 23 were re-read in the `.so`, and the C#
behind rows 3, 7..11, 13, 38 and 42. SAME rows were not re-read.

## Real differences (for the fix job)

| Rows | Difference | Evidence | Records |
|---|---|---|---|
| 10 | **PlacedOnCharger can never fire.** `PlacedOnChargerStrategy.HandleChargerEvent` has no caller. Its comment says the broadcast is unknown, but that is stale: `Sensors.cs:753` now raises `ChargerEventInternal`. Bind it. The report's quote at 0x0061440E is the constructor zeroing +0x15, not the handler; the handler's rule rests on the C# note (vtable 0x102D688 slot +0xC), so confirm it there. | `.so`, C# | M10-003 |
| 3 | **Cliff ignores RobotStopped.** The factory's set holds tags 34 and 52 (0x0060D6BE..0x0060D6CE loop, two u16s), and the filter at 0x0060DC7C runs for both. The strategy hooks only `CliffDetectedInternal`. The MISSING comment is stale too: `Sensors.cs:936` raises `RobotStoppedInternal`, and `ReactToCliffBehavior` already uses it. | `.so`, C# | M10-003 |
| 8, 9 | **Carried and docked cubes are not excluded.** The engine refuses while carrying: [robot+0x284]+8 != -1 (0x006113F2..0x006113FC). The observed handler treats the carried and docking IDs as already reacted (0x006114D8..0x0061150C). `ObjectPositionUpdatedStrategy` has both inputs, and `Behaviors.cs:737..741` binds neither, so the carrying gate is always false and Cozmo can acknowledge a cube it is holding. `InitializeRobotReactions` has no manipulation system; `BindFreeplayInputs` does, so bind them there. | `.so`, C# | M10-003, M15-081 |

## Rejected

**Row 7 is SAME, not DIFFERENT.** The report says the engine drops a stale event before consuming the latch. It does not. After the timeout
scan, 0x0060F43E tests r8, the runnable result, and calls WantsToRun at 0x0060F448 whatever the scan found; the scan result in r7 is only
ANDed in at 0x0060F44C. The latch is consumed either way, as in the C#. Row 14 is also not a difference on shipped data: no row in the map
sets interrupt-other. Whether the factory ever passes false in code is left open.

## Unbuilt strategies (already recorded)

All three are in M10-003's title; `Behaviors.cs:669..671` says they are not registered.

- **FistBump** (rows 4..6, 11, 12, 22): reachable in normal play, since six shipped objectives feed it. The engine also skips the cooldown
  when +0x4C == 0.0 (0x0060E900..0x0060E908), which the report omits; each passing objective takes one RandDbl(1.0), so an absent strategy
  also shifts the shared RNG. It needs BehaviorObjectiveAchieved delivered locally; today most behaviours only log it. Check that against
  `NativeActivityObjectiveOwner` first.
- **Hiccup** (rows 15..19, 23..25): feature 6 is `Hiccups` (Unity `FeatureType`), and `features.json` enables it. Reach then depends on the
  `DroneModeGame` unlock at 0x006106B8, so on the unlock path M15-095 and the stored state.
- **Sparked** (rows 20, 21, 26): reach depends on a spark being requested; see M15-100.

## Not blockers

- **Row 38, closed:** `BindFreeplayInputs` registers NoPreDockPoses late when RamIntoBlock is bound.
- **Row 35, closed:** the code notes +0x2A has no reader.
- **Row 42:** the code records that ResetReactionData changes nothing for faces; objects are still open.
- **Row 33:** log text only.
- **Rows 36, 39, 40, 41:** force paths. Nothing found writes +0x29; no writer was established, so they stay open.
- **Rows 27..32:** thresholds match where read; left UNKNOWN by the report for frames and float boundaries.

## Method note

The check's best result is that two "unknown input" MISSING comments went stale when the sensors gained those events, so a reaction stayed
dead while its cause had been fixed. Older MISSING notes are worth re-testing. It also shows that a report can misread flow: the row 7 quotes
were accurate, but the conclusion needed the register, not the order of the calls.
