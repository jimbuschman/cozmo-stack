# CUBE-LIFT-WORKOUT-1: Claude check (manager, 2026-10-30)

**Verdict: FIX REQUIRED. Not merged.** Handoff `codex-m13-manager-2` 0a8626dc (gate on 211471e9, 6959 PASS). Code diff
`git diff 2cb68fa8 0a8626dc -- cozmo-stack`. **M13-010 is not settled.** The verifier reran 71/71 and 193/193 focused
tests on an export of 0a8626dc, plus two probes; it did not rerun the full suite.

## Blocking: a missing workout input is reported runnable, then fails Init repeatedly

- **The stack:** `NativeCubeLiftWorkoutBehavior.cs` `Runnable()` tests only the carry and target gates. It never tests
  `Workout()` or the MoodManager, but `OnStart` throws on both and sets InitFailed. Readiness stays available and
  runnable. The verifier's probe (AI.Workout=null, carrying, unlocked, a test activity returning the behaviour) gave
  15 `BehaviorManager.SetCurrentBehavior.InitFailed` lines and 1 MISSING over 30 ticks. The InitFailed path has no
  repetition stamp, because `_penalty.Ran` is only in `StopAndNullifyLocked`.
- **The original:** with a null current, GetNumStrongLifts reads through it (`ldrd r2,r3,[r1]`, 0x00573B70, called from
  0x005D7EE6). That is a crash, so M1-034 applies: skip the smallest unit, log it, carry on. A retry every other tick is
  not sourced.
- **Scope:** this needs a missing or invalid `workout_config.json` or a missing MoodManager, and nothing admits the
  behaviour yet. A missing file is a failure AGENTS.md says can happen here, and this is the same failure class as
  CHECK-FOR-STACK-AT-INTERVAL-1. The InitFailed lines are visible, so it is not silent.
- **Fix:** the unavailable workout or mood input reports readiness unavailable and not runnable, with the visible
  MISSING. Test it through the chooser: no Init attempt, no re-selection. Cover the other null-current points
  0x00573B70 (via Init and via 0x00573E42) under M1-034 by name.

## M13-010: not settled

Current title: "The workouts run in file order, the last one repeats, and ShouldPlayEightiesMusic rolls against the
mood score". Status IMPLEMENTATION_GAP, live_path true. Every traced step of its evidence holds in the .so, but:

1. **Neither caller is reached in production.** The handoff's "both real callers are exercised" means unit reach only.
   - 0x00592416: the test calls `BehaviorPublicState.Update` directly; `src` has no caller of it (M13-038).
   - 0x005D8014: `AdmitActivity` refuses PlayAlone, and the tests enter through StartAsync.
2. **A settled record owns its whole path,** and this record's evidence names both callers. It stays open until they
   are joined. This is the same reason as M15-025 and its neighbours.
3. **When it does settle,** `unresolved` must state the 0x00573DFC skip (confirmed: `ldrb r2,[r5,#0x30]!` with no null
   check, and skipping the whole completion call is the smallest unit), the other null-current points, that
   CompletedWorkouts is a host counter, and the signed/refused-range boundary.

## Checked and agreeing (verifier, in disassembly)

- **Score and scorer:** 0x00573B64..A4, 0x0067C9B8..CA72, the graph 0x00804BD0..C44, history 0x006794F8..9524.
- **Complete and music:** 0x00573DEC..E2E, 0x00573E30..7E (RandDbl(1.0) against 0x3FB999999999999A).
- **Loader and stages:** 0x00573BA8..D52; stage calls 0x00592416..24 and 0x005D8014..22.
- **Lifecycle:** the gates and Init 0x005D7E88..7F44; PickingUp 0x005D8064..8104; PostLift 0x005D7F54..8026; the lift and
  put-down triggers 0x005D844C..8744; ManualPutDown 0x005D8764..87FE; CheckPutDown 0x005D8866..8986; EndIteration
  0x005D89FA..8A3E; Stop 0x005D8174..8198; Update 0x005D81A8..8226.

No regression was found for existing callers of the shared helpers, and no circular test.

## Queued (with the fix)

1. Keep the base `Animations.Library is null` readiness check in the IsRunnable override.
2. Add a `// fidelity:` tag and `reach:` labels.
3. Tests:
   - one that enters `IsRunnable`;
   - assertions on the light play/stop, lift child, reaction table and idle;
   - the normal still-carrying finish and detach.
4. `ClockSec` uses `mood.LastUpdateSeconds`; every other caller uses `Timer.SecondsF`. The value is the same on the live
   path, but use the same source.
5. `NativePickupBlockHelper.Unavailable` labels every error "M12-031 visual pre-action".
6. Public `ShouldPlayEightiesMusic` used to return false on a null current and now throws; say which is intended.
7. The legacy `CubeLiftWorkoutBehavior` and its `M.Workouts` remain for CreateStandalone only: a retirement candidate.
8. The handoff's MISSING list should name the M13-038 `Update` caller.
9. The proposed lifecycle record goes to the operator.

## Returned fix (manager, 2026-10-30): PASS, merge pending

Fix 1cc9fb23, handoff 422d19b1 (full gate on 7be1d7ba, 6992 PASS). I read the production diff.
- **The blocking item is closed.** `Runnable()` now tests the workout and mood inputs and reports readiness
  unavailable, so Init is not reached. The test runs both aliases through three missing-input cases for 30 ticks with
  StopOnUnavailable: no current, no InitFailed, no reaction lock held.
- **The other null-current points are named under M1-034:** 0x00573B70 via 0x005D7EE6 and via 0x00573E42 (public music
  now returns a documented host false without an RNG or cache write) and 0x00573DFC.
- **Queued items done:** `ClockSec` uses `Timer.SecondsF`; the pickup diagnostic keeps the actual reason; the base
  Library-null check and the `// fidelity:` tag are in. The two removed `current()` guards in `CompleteCurrentWorkout`
  are correct: the original always advances after the event.
- **M13-010:** Codex withdrew the settlement proposal. It stays open.
