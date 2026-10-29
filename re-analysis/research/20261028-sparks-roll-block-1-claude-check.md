# SPARKS-ROLL-BLOCK-1 — Claude check (manager, 2026-10-28)

**Verdict: PASS. Settled: M12-051, M7-030 (EXACT_SOURCE). M15-077 not settled.** Merged ae6ffa06 (code 3abe05c7).

## Shared fix: Roll failure suppression thresholds

The Hiking build passed `DidFailToUse(id, {3}, 60f, pose, 30f, PI)`. The native call at 0x00570692..0x005706AC passes
r2 = 3, r3 = 0x41F00000 (30.0, time), stack: pose, 0x42700000 (60.0, distance), radians. Signature
`DidFailToUse(int, ObjectActionFailure, float time, const Pose3d&, float dist, const Radians&)`. Codex's swap to
time 30 / distance 60 is correct. My HIKING-ROLL-CUBE-1 check did not cover the cache predicate and missed this.

## Binding

SparksRollBlock is added to the native Sparked owner bindings (activity 26, objective BlockRolled -> 2) and the
executable type RollBlock -> 8. The Sparked owner itself was checked in SPARKS-KNOCKOVER-1.

## M12-051 PlaceBlockHelper (settled)

- Init 0x005B8CB0..0x005B8D50: Robot::GetRNG, RandDblInRange(0.785398..., 1.570796...), then RandDbl(1.0); `vcmpe`/`mi`
  (< 0.5) selects the negated value; vcvt.f32.f64 once; TurnInPlaceAction(robot, angle, false); IHelper::StartActing.
- RespondToTurnAction 0x005B8DF4..0x005B8E44: CompoundActionSequential (0xAC) + PlaceObjectOnGroundAction (0x88),
  AddAction via vtable+0x20 with (false, true); StartActing with RespondToPlacedAction. The turn result is ignored.
- Update 0x005B8E60 returns +0x1C; RespondToPlacedAction 0x005B8E64 stores 2.
- Matches NativePlaceBlockHelper.cs. Ground, Turn and RNG bodies are other records.

## M7-030 BehaviorManager::SetBehaviorStateLights (settled)

- 0x005A433C stores the flag at +0x85 first; class at +0x84 last (0x005A45D6).
- Old entries (+0x78, stride 0x90): search the new vector from the start for the first equal object ID (+4).
  Not found: StopLightAnimAndResumePrevious(old trigger, old ID) 0x005A43C2. Found: modifier copied only when the new
  entry's +0xC is set; StopAndPlayLightAnim(new ID, old trigger, new trigger, empty callback, hasModifier, modifier)
  0x005A441A; failure logs; the ID goes into the set either way (0x005A449A).
- New entries not in the set: PlayLightAnim 0x005A4556; failure logs. Duplicates in the input both play.
- Vector assigned only when the input is a different vector (0x005A45C8).
- StopAndPlay 0x006396AC..0x00639722: StopLightAnim(old, layer 1, id, false), then PlayLightAnim(new, ..., 0).
- MS4 0x005A2114..0x005A226E: nothing when empty or no incoming behaviour; incoming class byte +0x64 equal to +0x84 →
  nothing; clear when the incoming behaviour is neither the old current nor the old resume, or when the new trigger
  is not None (0x16), the old one is None and the flag +0x85 is 0. Clearing stops-and-resumes each entry and empties
  the vector; class and flag are kept.
- Matches BehaviorStateLights.cs and Lights.cs Stop/StopAndPlay/Play.

## M15-077 not settled

Two things are outside the current record: (1) its title names only Hiking_RollCube, while RollBlockOnSide,
RollBlockOnSideLowScore and SparksRollBlock now run the same path (widening is an operator checkpoint); (2) the
PlaceObjectOnGroundAction::GetCompletionUnion producer (getter 0x00554AE0..0x00554AFA, ctor 0x00554638..0x0055469C,
Init 0x00554794) on its API path has no record. Codex proposes a new record for it. Also, my own coverage of the
Roll helper, DriveToRoll and RollObject bodies is Codex's check only.
