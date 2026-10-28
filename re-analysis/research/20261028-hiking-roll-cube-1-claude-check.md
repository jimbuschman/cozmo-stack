# HIKING-ROLL-CUBE-1 — Claude check (manager, 2026-10-28)

**Verdict: PASS. Nothing settled.** Merged d9d84dab (code b69ddafd/e14692b4) into main.

First job with builder and reviewer workers on the Terra default (reader explicitly Sol/medium). One reviewer fix round
(three Roll IDock corrections); builder-raised MISSINGs were resolved by source addenda.

## Checked against libcozmoEngine.so

**BehaviorRollBlock::UpdateInternal 0x005C8A54..0x005C8B24** against `NativeRollBlockBehavior.OnUpdate`:
- located target missing (0x5C8A70) or state +0x130 != 0 (0x5C8A78) → base IBehavior::UpdateInternal only. Matches.
- GetRotatedParentAxis<'Z'> of the target (0x5C8A96) equal to +0x12C → no action. Matches `StartAxis`.
- ComputeDistanceSQBetween(robot pose, target pose) must return true (0x5C8ABE) and the result must be > 14400.0f
  (vcmpe/ble 0x5C8ACA..0x5C8AD2; NaN does not pass). C# `!(distance>14400f)` return matches, NaN included.
- then UpdateTargetsUpAxis (0x5C8AD8), StopActing(false,false) (0x5C8AE2); pre-dock latch +0x129 set →
  TransitionToRollSuccess (0x5C8AF0); else vtable +0x90 target refresh, +0x124 == -1 → return 2 without base update
  (0x5C8B08/0x5C8B22), else TransitionToPerformingAction(robot,false) and base update. Matches.

**TransitionToPerformingAction 0x005C86E8..0x005C8938** against `Perform`:
- state +0x130 = 0 and debug name before the target check; target -1 → sWarningF and return (0x5C879E..0x5C87BE).
- max face-turn angle: +0xD9 set → 0.0f; else +0xD8 (hard spark) set → 0.0f; else 1.5707964f (0x5C8734..0x5C8744,
  0x5C8934). C# `_always||HardSpark?0f:0x3FC90FDB` matches.
- helper params triggers 0x1F6/0x1F7 (0x5C87F0..0x5C87F6), pre-dock callback, CreateRollBlockHelper(..., target, true,
  params) (0x5C8814), SmartDelegateToHelper with success/failure functors (0x5C8850). Matches.
- IncreaseScoreWhileActing 0x3F4CCCCD (0.8f) (0x5C888E).
- one BehaviorStateLightInfo {target +0x124, 25, false, zeroed modifier} and SetBehaviorStateLights(vec, false)
  (0x5C88A6..0x5C88CE). Matches the C# call.

Not re-checked by me (Codex's independent check covers them): the Roll helper body 005B98D4..005BAC54, PlaceBlockHelper,
DriveToRoll/RollObject action, and the shared BehaviorManager state-light setter 005A4320..005A45F2.

## Records

No existing record settles from this delta; I agree with the handoff. M12-016 is already EXACT_SOURCE.
Codex proposes three new ownership scopes (Roll own path; generic PlaceBlockHelper lifecycle; shared BehaviorManager
state-light setter and bindings). Adding records is an operator checkpoint; not done here.

Queued cleanup (non-behavioural): trailing whitespace NativeRollObjectRunner.cs:85; extra EOF blank lines in
HikingRollCubeNativeTests.cs and RollFirmwareScene.cs.
