# CANT-HANDLE-TALL-STACK-1: Claude check (manager, 2026-10-30)

**Verdict: PASS. Merged** from `codex-m13-manager-2` 2cb68fa8 (full gate 6931 PASS). **Nothing settled.**
Code diff `git diff 15ad5626 2cb68fa8 -- cozmo-stack`. Built on the corrected M7 rows 856/857 (manager, 2026-10-30).

## Checked in the .so (verifier, lief + Capstone Thumb + relocations)

- **The vtable (address point 0x01029FE0):** +0x90 is ClearStack 0x005ED1E4 and +0x94 is UpdateTargetStack 0x005ED1FA.
  The gates at +0x20/+0x24/+0x28 (0x005BF04C, 0x0059EC12, 0x005ED384) return 0. Update and Resume are IBehavior's.
- **The ctor 0x005ECB10:** four asFloat config waits/thresholds go to +0x138..+0x144, and minimumStackHeight goes
  through asInt into byte +0x134, with a default of 3.
- **Runnable 0x005ECD64:**
  - IsUnlocked(5) gives 0;
  - then the vtable+0x94 refresh;
  - then the weak lock, the stack pointer and the unsigned height `bhs`, then the located object;
  - with no stored pose the result is 1; otherwise it is `!IsSameAs(stored, pose, (t,t,10.0), PI)`.
- **Init 0x005ECE3C:** it locks with no refresh, copies the pose to +0x128, sets +0x124 = 1, then transitions.
- **Looking 0x005ECEA4:** the sequence is Wait, head 0xBEDF66F3, Wait, head 0x3F490FDB (clamped to 0x3F46D3F2 at
  0x00547FC2..D4), Wait. The head moves use speed 15 and accel 20.
- **Disappointment 0x005ED0F0:** it agrees with M15-012.
- **Stop:** it calls ClearStack and keeps the pose.
- **AlwaysHandle 0x005ED240..258:** tag 0x32 clears +0x124 and then calls ClearStack.

The weak expiry is not faked: there is no GC WeakReference and no snapshot membership. Every unavailable input
reports not-runnable, so nothing is re-selected and nothing holds.

## No production reach

TallStackWeakInput, TallStackOwnerInput and BehaviorFactoryContext.TallStackData are null in production. No shipped
activity or reaction config names CantHandleTallStack, so its automatic caller is still UNKNOWN (visible MISSING).
No record settles. The proposed lifecycle record goes to the operator.

## Queued for Codex (not blocking)

1. **Init/Resume unavailability:** outside a readiness frame, unavailability at Init or Resume reaches the manager as an
   ordinary Init failure or a Resume result of 1 (SteppedBehavior.cs:206-210). The handoff overstates the propagation;
   correct its wording, or route it through readiness.
2. **Dispose:** `NativeBehaviorRuntime.Dispose` doesn't dispose this class. If it did, it would null StoredPose while
   HasStoredPose stays true. Make the two consistent.
3. **Tests:**
   - assert the disappointment parameters (0x1B, 1, true, 0, 60 s, false);
   - cite 0x00547FC2..D4 in the clamp test;
   - add `reach:` labels.
4. **Readiness check:** keep the base `Animations.Library is null` readiness check in the IsRunnable override, as was
   fixed for CheckForStackAtInterval.
