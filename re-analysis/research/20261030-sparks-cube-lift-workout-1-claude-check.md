# SPARKS-CUBE-LIFT-WORKOUT-1: Claude check (manager, 2026-10-30)

**Verdict: PASS at 422d19b1 (with FIX-CUBE-LIFT-WORKOUT-1). Merge pending.** **Nothing settled.** Alias diff
`git diff 0a8626dc 34a59d52 -- cozmo-stack`.

The verifier's one blocking item was the inherited missing-workout-input loop, which also held the Spark's hard outputs.
1cc9fb23 closes it for both aliases; `CubeLiftWorkoutNativeTests` line 70 onward covers `SparksCubeLiftWorkout`.

**Agreeing in the .so and config:** ID142/class6 (0x0059CA12); needs 45 versus 44 (0x005BE40C..16); unlock 29; objective
0x29 default (0x005BBD26..2C); activity 28 config (strict order, 0/60/1, objective 18, soft 517, no delegate); objective
18 reaching the counter (0x005BE22E..246, 0x005BE3A8..CE, 0x005B181E..32); Reset first-name removal (0x005B17AC..1804).
No regression to other Sparks.

## Queued

1. Add `reach:` labels to `SparksCubeLiftWorkoutNativeTests.cs`.
2. The retire test uses a null CarryingOwner, which production nulls only at teardown. The realistic retire route (an
   unbound SparksLookInPlace) has no alias test.
3. **Lead to trace:** on the native runtime path nothing parses the behaviour objective key, so the ordinary
   CubeLiftWorkout may stay at 41 where its JSON gives 18 (+0x10C). This is unverified.
4. The legacy `CubeLiftWorkoutBehavior("SparksCubeLiftWorkout")` at Behaviors.cs:564 is a retirement candidate.
5. The proposed lifecycle record goes to the operator.
