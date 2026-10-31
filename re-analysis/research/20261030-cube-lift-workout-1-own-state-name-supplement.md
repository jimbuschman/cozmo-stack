# CUBE-LIFT-WORKOUT-1 own state-name calls

Read-only extractor supplement, 2026-10-30. Answers the builder's narrow question whether transition method names imply native state-name calls. Original frozen reports remain unchanged. No code, official records, git, .NET or hardware changes.

Primary evidence is the already retained exact-symbol-size ARM/Thumb disassembly in `.scratch/workout-read/full.txt`, from `resources/lib/armeabi-v7a/libcozmoEngine.so`. `known.py SetStateName` and `known.py IBehavior::SetState` found no symbol; `known.py 0x005C0CA8` confirmed the established anonymous state-name helper address before comparison.

The own constructor/Init/Update/Stop, eight transition methods and EndIteration contain **no call to the established state-name helper 0x005C0CA8 and no own state-name string assignment**. There are no sourced per-transition state names to provide. Bounded transition ranges are PostLift 0x005D7F4C..0x005D8048, PickingUp 0x005D8048..0x005D816C, StrongLifts 0x005D83F4..0x005D8528, WeakPose 0x005D8528..0x005D85A4, WeakLifts 0x005D85A4..0x005D86D8, PuttingDown 0x005D86D8..0x005D875C, ManualPutDown 0x005D875C..0x005D885C and CheckPutDown 0x005D885C..0x005D89F4. These exact symbol ranges include literals/unwind tails; those are not extra executable behavior. The previously identified indirect calls are action insertion and temporary callback destruction, not an unclassified state-name virtual call.

Potentially misleading strings are diagnostics:

- 0x005D8420 references `BehaviorCubeLiftWorkout.TransitionToStrongLifts`, and 0x005D8424 references `%s: %d strong lifts remain`, passed to sChanneledInfoF at 0x005D8426.
- 0x005D85D0 references `BehaviorCubeLiftWorkout.TransitionToWeakLifts`, and 0x005D85D4 references `%s: %d weak lifts remain`, passed to sChanneledInfoF at 0x005D85D6.
- 0x005D8774 references `.ManualPutDown`; operator+ at 0x005D8778 creates a temporary behavior-name suffix for the diagnostic call at 0x005D879E, destroyed before action construction.
- 0x005D888A references `.StillCarryingCheck`; operator+ at 0x005D888E creates the analogous temporary diagnostic name for 0x005D88B4, destroyed before compound construction.

Transition method names and these log names do not establish API state-name updates. This is a bounded own-body absence claim; it does not remove inherited common state/API behavior or the separately established public-stage updates (3,2) and (4,0).
