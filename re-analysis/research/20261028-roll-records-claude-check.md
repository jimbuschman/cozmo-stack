# Roll records — Claude check (manager, 2026-10-28)

Operator approved (2026-10-28, "Do it"): widen M15-077 to all four RollBlock instances and add M12-052 for the
PlaceObjectOnGround completion getter. M15-freeplay and M12-manipulation re-approved.

## M12-052 added (IMPLEMENTATION_GAP)

PlaceObjectOnGroundAction::GetCompletionUnion 0x00554AE0..0x00554AFA: `ldr r0,[r0,#0x7C]` into the first ID word,
`strh` 0x0001 at +0x14 (count 1, flag false), Set_objectInteractionCompleted. The other four ID words are never
written. The ctor stores +0x7C at 0x0055467C. NativePlaceGroundRunner returns (saved, 0, 0, 0, 0, 1, false) and
withholds the full public payload under M1-034 with a visible MISSING. Not settled.

## M15-077 settled EQUIVALENT_IMPLEMENTATION

Only the imported atan2f numeric body is equivalent (existing libm policy). Checked by Claude across today's checks:
- TransitionToPerformingAction 0x005C86E8..0x005C8938 and UpdateInternal 0x005C8A54..0x005C8B24
  (`research/20261028-hiking-roll-cube-1-claude-check.md`);
- rotation default 0x005C85DC and flatScore 0x005BC4E0 (`research/20261028-roll-block-on-side-1-claude-check.md`);
- failure filter 0x005706AC, time 30 / distance 60 (`research/20261028-sparks-roll-block-1-claude-check.md`);
- RollBlockHelper::DetermineAppropriateAction 0x005B9A30..0x005B9BD8: carrying ID (robot+0x284)+8 != -1 → tail to
  DelegateToPutDown; located target absent → status store, failure; RollObjectAction::CanActionRollObject 0x0055673C
  (robot+0x280 → CanPickUpObjectFromGround) != 1 → tail to UnableToRollDelegate; IsAtPreActionPoseWithVisualVerification
  == 0 → StartRollingAction 0x005B9BD8; else GetRollToUprightApproachAngle when upright, CreateDriveToHelper. Matches
  NativeRollBlockHelper.Determine.
The remaining helper bodies (UnableToRollDelegate, StartRollingAction beyond M12-016, response), DriveToRoll and
RollObject rest on Codex's independent checks. The undefined full-public RollObject payload stays skipped under M1-034.
