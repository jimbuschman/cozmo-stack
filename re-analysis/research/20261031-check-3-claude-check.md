# ChatGPT check 3, freeplay selection: Claude check (manager, 2026-10-31)

**Verdict: 10 of its 15 flagged rows are real and shipped-reachable, and four of those it had rated UNKNOWN; earlier reads settle them.
Nothing settled; no status changed.** The report is `research/20261031-check-3-chatgpt-extraction.md`. It has 31 rows: 15 DIFFERENT,
MISSING or EXTRA, 10 SAME, 6 UNKNOWN. Every row named here was read in the C#; the engine side was re-read in the `.so` or taken from
the cited check note. Rows not named were not re-read.

## Real differences (for the fix job)

| Rows | Difference | Evidence | Records |
|---|---|---|---|
| 17 (raised from UNKNOWN) | **When the desired activity is computed.** `FreeplaySystem.cs:49,239,403` computes it on the first pick and after each put-down. The engine's `CalculateActivityFreeplayFromObjects` has two static callers, both in BehaviorLookForFaceAndCube (TransitionToS6_Done 0x005F0DBC, StopBehaviorOnFaceIfNeeded 0x005F0C78; queue 3 rows 3.10..3.15). So the first pick goes by priority, and a put-down does not force the desired activity, so PutDownDispatch gets its turn. `DesiredActivityFromObjects` has no other stack caller. | Queue 3; indirect callers not proven absent | M15-006 |
| 20 (raised) | **Stop order.** `EndActivity` stops the behaviour first (`FreeplaySystem.cs:361`). The engine chooses first (0x005A2AA4), which is where the activity deselect and select happen, and only then switches (SwitchToBehaviorBase 0x005A2AB4). So the new activity's chooser sees the old behaviour still running, and a behaviour both activities name keeps running. The stack's comment at `:259` documents its own consequence. | `.so` | M15-006, M8-012 |
| 21 (raised) | **Retry bound.** The stack retries up to the tree size in one tick. The engine re-picks at most once (queue 4), so further attempts fall to later ticks, with select and deselect events and RNG on a different schedule. | Queue 4 | M15-006 |
| 19 (raised) | **Interlude.** `FreeplaySystem.cs:298-314` is not the engine's rule. The engine (0x005B387C..0x005B3922) asks the interlude chooser whenever the candidate differs from the current behaviour, nothing running included, passes the candidate to it, and caches the result at +0x2C. The stack passes null, requires a previous behaviour, adds id exclusions, and keeps a deferred behaviour the engine does not. Seven shipped activities configure one. | `.so` | M15-006 |
| 11 | **Put-down kick-out.** The stack ends any current activity on a later tick. The engine deselects at once and only a non-spark activity with ID not 7 or 8 (the severe-need activities). M15-060 already records this. | `.so` | M15-060 |
| 12 | **Public state.** `Activities.cs:1179` binds 10 ids, so Socialize, Hiking, PlayAlone, BuildPyramid and the rest log MISSING and publish nothing. | `.so` | M15-006 |
| 13, 14 | **Hiking driving animations.** Only SparksGatherCubes is bound. Hiking is the shipped no-face no-cube choice, and it logs MISSING. | `.so`, `hiking.json:4-6` | M15-006, M5-063 |
| 15 | **Reward order.** `EndActivity` runs subclass deselect effects before SparksRewardCommunicatedToUser. The engine does the reverse (0x005B3548 before 0x005B365C). | `.so` | M15-006, M15-071 |
| 16 | **Face evidence.** `HasAnyFaces` stands in for GetLastObservedFace(pose, true) plus IsPoseInWorldOrigin (0x005ADF8C..0x005ADFA4). | `.so` | M15-006 |

Rows 12 and 13 at least log MISSING, so they were visible gaps. Rows 17, 19, 20 and 21 were not: M15-006's `unresolved` says the
selection, interlude and null-pick logic is built.

## Not blockers or still open

- **Row 25:** idle triggers, unreachable. No shipped activity has one.
- **Row 18:** likely the same. A chosen behaviour equal to the manager's current one is the running one. No case was found.
- **Row 22:** +0x92 and the state name. Whether they feed 1-4 is unread.
- **Row 23:** SetActivityStrategyCooldown has no stack caller, and its engine callers were not found. Open.
- **Row 24:** `ScoringChooser.Ran` is owned by M15-002's penalty trace.

## Corrections to the report

- **Row 36:** "constants 4 and 8" is wrong. r7 is the table offset (0, 4); the families are 1 and 2 (queue 4). Whether the stack's
  `LocatedObjects` cube count equals that filter stays open.
- **Row 21:** a re-pick loop with no numeric count is not unbounded; see queue 4.

## Method note

Checks are finding what reading could not, because they compare what was built. Its reach column works but is cautious: it marks UNKNOWN
where an earlier read would settle the case, and it does not use the earlier notes, which is right for independence. So the check note
is where reach is decided.
