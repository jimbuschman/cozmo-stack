# M15-071 reward gates: Claude check of the third ChatGPT extraction (manager, 2026-10-31)

**Verdict: citations PASS. Nothing settled; no status changed.** The report is
`research/20261031-m15-071-gates-chatgpt-extraction.md`. Part A was done; part B (M12-049) was not, because the question
allowed it to stop after A.

## Checked directly in the `.so`

- **A1 ShouldRewardSparksForFreeplay:** 0x00696D7C..0x00696E00 matches in full.
- **A2 UpdateStarsState:** matches at 0x00696AB2..0x00696AEC, the calendar compare 0x00696B5A..0x00696B6E and the transition
  0x00696BBA..0x00696BE2.
- **StarUnlocked:** 0x006986DA..0x00698704 and the tag 0x67 (103) constructor 0x00726624..34 match. Payload order: +0x54,
  +0x5C, +0x58.
- **StarLevelCompleted:** the order at 0x00698792..0x006987D4 and SetStarLevel 0x0069CBBC..0x0069CBC8 match. The serialisers
  (A2.14g, A2.14h) and the constructor A2.14f were not re-read.
- **A3 selection:** the default path 0x0052282C..0x00522856 and the variation paths at 0x00697256 and 0x0069735A resolve to the
  named strings. The A3.4-A3.10 branch detail was only sampled.

## What this settles

**The random gate.** `chance = 1 - (f32)GetInventoryAmount(1) / (f32)freeplayTargetSparksTotal`, kept in f32; if below
`freeplayMinSparksRewardPct` (+0x2C), use that. Return `RandDbl(1.0) < (double)chance`, on the context's generator
(context +0x14). This answers the +0x2C question.

**The complete reward gate order in RegisterNeedsActionCompleted:** UpdateStarsState(false) returns 0; then
|weight| not below 0x3727C5AC; then ShouldRewardSparksForFreeplay == 1; then RewardSparksForFreeplay.

**The date gate.** UpdateStarsState(false) returns 1 only on a calendar-day change (tm_yday, tm_year, localtime_r), and then
it takes the star transition. So the reward is skipped for the action that crosses the day. Its earlier exits return 0, so
those allow the reward.

**Config selection.** The default load is the unsuffixed `needs_level_config.json`. SetTuningTestVariation appends only the
string it is given, for needs_config, needs_level_config and needs_action_config. Its caller and the +0x3F8 writer are unread,
so whether any variant can be active is open.

## Stack differences (Needs.cs)

- Line 1895 sets the pending flag on every action with weight > 0. There is no random gate, no star gate and no amount, and
  the threshold differs: the engine tests the absolute value against 0x3727C5AC. This is a real difference in API delivery
  (item 3): the stack over-reports rewards.
- Line 632 calls UpdateStarsState and RewardSparksForFreeplay "the app's". Both are engine code, so the comment is contradicted,
  like the one at line 837.
- GetInventoryAmount(1) has no native owner in the stack at this point. That needs reading for the amount and the gate.

## New gap with no record

The daily star machine has no record. M15-014 only mentions UpdateStarsState as a caller. Its scope is: the NeedsManager fields
+0x54, +0x58, +0x5C, +0x60, the calendar compare, StarUnlocked (tag 103), StarLevelCompleted (tag 102),
SendStarLevelCompletedToGame, ProcessLevelRewards 0x006993F8 and SetStarLevel. It touches items 3 and 4. It needs its own
record before M15-071 can settle, under the rule that a settled record owns its whole path. Proposal goes to the operator.

## Still open

ProcessLevelRewards; the helper 0x0069CB20; the caller of SetTuningTestVariation and the writer of +0x3F8; the key at
0x0105C1F0 (the shipped root key is `unlockLevels`); InventoryComponent's owner for type 1; M12-049.

## Method note

Citations exact, UNKNOWNs honest. It stopped cleanly rather than giving thin answers on B. Three items filled a reply, so a long
queue needs several replies, not one.
