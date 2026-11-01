# Claude check: seven-job start-path fix batch

Date: 2026-10-31. Branch `origin/codex-m13-manager-2` at `aba05e45`; Codex gate 7087/7087 at `af83ed19`.
Covers SCORING-FIX-1, NEEDS-FIX-1, REACTION-FIX-1, STRATEGIES-FIX-1, FREEPLAY-FIX-1, REACTION-FIX-2 and
REACTION-BEHAVIOURS-FIX-1.

**Verdict: fix first. Not merged.** One production defect blocks the batch. Nothing is settled here.

## Blocking

**ReactToMotorCalibration cannot start in production.** `Behaviors.cs:714` passes `m` to the new constructor, but the
only production call of `ShippedBehaviors.Reactions` (`BehaviorManager.cs:136`) supplies no `m`, and
`BindFreeplayInputs` does not rebind this behaviour. `OnStart` therefore always takes the
`_manipulation is null` branch: MISSING and InitFailed. The old polling worked without manipulation, so the fix turned
a working stand-in into a reaction that never runs. Both tests (`DerivedStateTests.cs:920`,
`ReactionBehaviorsFixTests.cs:222`) build the behaviour by hand, which is why the gate passed.

The same cause, found by check 11 rows 12 and 13: `CozmoRobot.cs:212` runs `InitializeRobotReactions` before
`NativeBehaviorRuntime` creates Manipulation, and only `FreeplayStack.cs:197` calls `BindFreeplayInputs`. Without a
FreeplayStack the canonical robot has no Cliff back-up drive, no carried or docking IDs on ObjectPositionUpdated (so
REACTION-FIX-2's IDs reach only that path), no FrustrationMajor and no RamIntoBlock reaction.

## Checked and holding

| Item | Engine | Result |
|---|---|---|
| Transition sent unless old and new are both 0x16 | 0x005A20AA..B6 | same |
| 0x88 payload old then new; None is 0x16 | 0x005A20C4..D6, Unity ReactionTrigger | same |
| Robot+0x2C7 and MC+0xD4 written on entry (1) and exit (0) only | 0x005A20DA..2110 | same |
| Whiteboard +0x48 stored only when state byte is 0 (OnTreads) | 0x0056CCE8..F6 | same |
| Needs: paused and no-robot return; deadline; cooldown test including NaN | 0x006960E0..F8, 0x0069614A..5A, 0x006961A6..DA | same |
| Repair actions 2, 3, 4 clear parts 0, 1, 2; Repair level set when no parts remain damaged | 0x006967DE..0x00696878, 0x00696906..3E | same |
| MotorCalibration tag 30 tests head then lift, then StopActing(1,0) | 0x00606742..98 | same; C# StopActing cancels with that first argument |
| Shaken loop action is cancelled by StopActing before DizzyShakeStop | C# only | correct |
| Spark ctor 0x7F7FFFFF; start 1, end 0 | 0x005B4E90..A2, 0x005B4EBE, 0x005B4EC2 | same |
| ActivityID table of 32 names; tags 0x62, 0x88, 0x8B; payload types on local subscriptions | Unity CLAD, C# | same |

The three test corrections in `af83ed19` are sound: each changes an expected value towards a cited engine fact and
weakens no other assertion.

M15-098 and M15-100 are ready to settle when this code reaches main, as Codex proposed: both paths hold, and
GraphEvaluator is f32. Neither file carries its `// fidelity:` tag yet, so the fix job adds them.

## Not blocking, queued in REVIEW.md

- **Pyramid can no longer start.** `PyramidCandidates`, `PyramidBases` and `PyramidFull` have no production assignment
  and `Inputs.LocatedCubes` has no writer, so shipped BuildPyramid always stops at "caches unavailable" with a visible
  MISSING. `ManipulationSystem.NativePyramidBases` and `NativePyramids` exist. The old rule was wrong, so this is an
  honest gap, but it is the next thing to wire.
- **A requested spark freezes Freeplay selection.** `FreeplayStack.ActivateSpark` is public. Any raw request now hits
  `FreeplaySystem.cs:218`: MISSING, keep the current selection, and with no current activity nothing runs. Before the
  batch the raw request was ignored. The raw ID to activity join is on the shared start path.
- **Needs step not built:** the engine zeroes NeedsManager+0x68 for actions 0x0D (CozmoSings) and 0x55
  (CozmoSingsCompleted_Sparked) at 0x00696138..42. It is probably forceNextSong (not confirmed), which has no C# writer
  in this path. The +0x39C allow-list gate at 0x006960FA.. is also absent; confirm its setter is app-only.
- **Hardware note:** PlacedOnCharger can now really fire, so sitting on the charger starts the idle timeout: reason 4,
  face off at 300 s and disconnect at 330 s unless cancelled. That matches the engine, but it is new visible behaviour
  on the first hardware run with a charger.
