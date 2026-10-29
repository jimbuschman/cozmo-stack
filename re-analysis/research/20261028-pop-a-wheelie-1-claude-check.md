# POP-A-WHEELIE-1 — Claude check (manager, 2026-10-28)

**Verdict: PASS. Nothing settled.** Merged ad24c6c7 (code b7190188). Terra builder/reviewer.

## Checked

PopAWheelieAction::Verify 0x0055295C..0x00552A72 against `NativePopAWheelieRunner.Verify`:
- selected dock action +0x80 != 9 → warning, 0x0300001A;
- docking component (+0xCC)+5 == 0 → 0x04000003;
- Robot::GetPitchAngle < Radians(1.0f) (Anki::operator< on Radians, 0x00552994) → 0x0400000A (0x04000003 + 7);
- else 0. Matches. The C# reads the stored robot-state pitch for GetPitchAngle.

Shared changes: cache entry 8 added with `PopEligible`; the legacy `PopAWheelieBehavior` adapter now reports a visible
MISSING on start and loses its M13-015 method tags (kept on the class as a location pointer). No change to other
behaviours' behaviour.

Not re-checked by me: Select 0x00552730..0x0055287E, DriveToPop 0x0055D1E8..0x0055D26C, the class lifecycle
0x005C7430..0x005C7F66 and the completion getter 0x0055268E..0x0055269C (Codex's independent checks).

## Records

None settled; I agree M13-015 stays partial while the manual/public callers use the legacy adapter. Codex proposes
one new record for the ordinary configured Pop path (lifecycle, cache 8, get-in, pre-dock cliff, DriveToPop, Pop
Select/Verify/completion), excluding M13-015's retry/result/reset. Adding it is an operator checkpoint.

## Gate note

The first full gate on 584a29b5 failed one test, NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates
(DriveAndFlipBlockAction(7) faulted: AggregateException). It passed 5/5 alone and the full rerun passed 6661/6661.
Not caused by this job (legacy manual KnockOver path). Queued as FIX-KNOCKOVER-RACE-1 for Codex.
