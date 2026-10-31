# ChatGPT check 6, needs: Claude check (manager, 2026-10-31)

**Verdict: the decay, bracket and damage arithmetic holds across 23 SAME rows. Four differences are new, three of them silent; most of the
rest is unbuilt work the code already reports and the records already hold. Nothing settled; no status changed.** The report is
`research/20261031-check-6-chatgpt-extraction.md`. Its 56 rows are 25 DIFFERENT or MISSING, 23 SAME, 8 UNKNOWN. Rows 6, 7, 8, 15, 26 and
27 were re-read in the `.so` and the C#. Other rows were not re-read in the `.so`, only matched to records.

## Real differences (for the fix job)

| Rows | Difference | Evidence | Records |
|---|---|---|---|
| 15 | **RNG drift, silent.** `Needs.cs` `Apply` returns when delta and range are both 0. The engine calls ApplyDelta for all three needs (0x00696890..0x00696904), and each call draws. 16 of 85 shipped actions have a zero pair, so every later draw shifts. | `.so` | M15-046 |
| 8 | **Action cooldown missing, silent.** The engine rejects while now < +0x244 + 4 x action (0x00696146..0x0069615A), then sets the next deadline when abs(cooldownSecs) >= 1e-5 (0x006961A6..0x006961DA). The stack never checks. Shipped: SayName and SeeFace, 60 s, so they can apply their deltas repeatedly. | `.so`, config | M15-046 |
| 7 | **Repair actions do not clear parts, silent.** Actions 2, 3 and 4 (RepairHead, RepairLift, RepairTreads) set parts 0, 1, 2 to undamaged (0x006967DE..0x00696878). No stack code does. Nothing in the stack calls these actions; they reach it only through the public API, as the app's repair game did. | `.so` | M15-029 |
| 6 | **Fullness cooldown never starts.** The return consumer at 0x006968E2..0x006968EE calls StartFullnessCooldownForNeed when not predicting. The stack logs MISSING and calls this unread; it is read now. Needs that reach Full should hold for the configured 1200 s, so the stack decays them too soon. | `.so` | M15-046 |

Rows 26 and 27: the pause and no-robot guards at the head (0x006960E0..0x006960F8) are also absent. Today's callers make this rare; add
them in the same patch.

## Already recorded

Rows 5, 9..14, 16, 18..22 are unbuilt work on M15-001, M15-014, M15-016, M15-071 and M15-093, and the code logs most of them as MISSING.
One is worth flagging: row 5, the NeedsState message, is what NeedBasedCooldown's handler consumes (check 2 row 23), so
STRATEGIES-FIX-1 needs a local recipient for it.

## Open

Row 17 (the whitelist at +0x1DF that defers deltas), rows 24, 25, 28, 29, and rows 53..60 (config key requiredness), as reported.

## Method note

The check again separated solid arithmetic from missing logic around it. It also gave exact rules for unread paths that the code had
marked as unread, which closes M15-046's named open point.
