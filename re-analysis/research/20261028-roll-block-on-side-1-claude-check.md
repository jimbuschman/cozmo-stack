# ROLL-BLOCK-ON-SIDE-1 and ROLL-BLOCK-ON-SIDE-LOW-SCORE-1 — Claude check (manager, 2026-10-28)

**Verdict: PASS for both. Nothing settled.** Merged Codex tip 253e23c9 (test-only deltas afb731b8 and 96e9aa12) into main.
No production change: both configured instances run the native Roll path checked in HIKING-ROLL-CUBE-1
(`research/20261028-hiking-roll-cube-1-claude-check.md`). Terra builder/reviewer, 0 reviewer fix rounds each.

## Checked

- Shipped configs `behaviors/freeplay/rollBlockOnSide.json` and `rollBlockOnSideLowScore.json`: class RollBlock,
  needs RollACube, unlock RollCube; neither has `isBlockRotationImportant` or `alwaysStreamline`.
- Ctor 0x005C85DC..0x005C85F4: `Json::Value(true)` default for the rotation key, asBool, stored at +0x128, so an
  omitted key means rotation important (cache 7). Matches `NativeRollBlockBehavior`.
- Parent scoring: playAlone.json and socialize.json give RollBlockOnSide flatScore 1.0; playWithHumans.json gives
  RollBlockOnSideLowScore flatScore 7. IBehavior::ReadFromScoredJson stores a non-null flatScore as asFloat at +0x100
  (0x005BC4CA..0x005BC4E0). The LowScore test supplies 7 from the loaded parent entry through the existing Score
  setter; the automatic parent reload and parent choice stay MISSING, as the handoff says.

## Records

No record settles. Both instances are callers of the Roll own path that M15-077 now names for Hiking_RollCube only;
widening M15-077's title and evidence to all three RollBlock instances is an operator checkpoint (proposed, not done).
