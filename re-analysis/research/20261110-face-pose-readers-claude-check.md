# FACE-POSE-READERS-NATIVE-1: Claude check (manager, 2026-11-10)

Built by Claude while Codex was out of usage: an implementer build (on Fable, finished on Opus), an independent
verifier (FAIL on two points), manager corrections, and the affected classes re-run (316 + 92 + 83 passed).

## What was wrong in production (confirmed by test against the old code)

NativeLastFaceRunner, the native TurnTowardsFaceAction, read the flat host face projection whenever it was built
without an owner. That is every needs and social behaviour that uses it: FindFacesSocialize, NeedsGetIn, ExpressNeeds,
LookForFaceAndCube (explicit id), DriveAndFlip, PopAWheelie, PutDown, DriveInDesperation. The projection has no parent,
so the with-respect-to step failed and **a stock face was never verified after the turn**: no SeeFace, no fine-tune
turn, no LookAtFaceVerified, no SetTurnedTowardsFace. Step 0's test reproduced it (VerifiedId stayed 0) before the fix.

## The change

| Reader | Native | Record |
| --- | --- | --- |
| NativeLastFaceRunner (Init, the 0x46 handler, FineTune) | Entry+0xF4 with respect to Robot::GetPose through the in-origin GetFace (0x0054BD7C..0x0054BD94, 0x0054C096..0x0054C114, 0x0054C2D4..0x0054C39C; gate 0x004F59B6..0x004F59C2) | M13-014 |
| FacePositionUpdatedStrategy | ComputeDistanceBetween(Robot::GetPose, entry+0xF4) in f32 (0x0060CC28..0x0060CC32; 0x00847780..0x008477BC), in-origin GetFace (0x004F597E), the 0x46 count/name gate (0x004F4956..0x004F496E) | M10-003 |
| FistBump state 2 recency | GetLastObservedFace(pose, true) stamp (0x004F5AE2..0x004F5B38) | M7-018 |

## Verifier findings and how they were resolved

1. **Blocking, fixed:** the strategy computed the distance in double. The engine uses f32 (x², +y², +z², vsqrt.f32)
   and an f32 compare, and the two disagree at the thresholds: (148.63052368164062, 260.5934753417969, 0) is exactly
   300.0f, not close, but 299.9999866 in double. Now `MathF.Sqrt(NormSquared(d))` against f32 thresholds; the case
   is a test.
2. **Blocking, fixed:** the strategy test fed unnamed faces seen 1 to 3 times, which the engine never delivers to this
   handler (0x46 is broadcast only above 3 or named). The strategy now applies that gate, the test feeds three gated
   observations first and asserts they have no effect, and the reach label says the entry is the host event.
3. **Fixed:** the handler used the ungated host GetFace; it now uses the in-origin native lookup and returns silently
   for a face in another origin, as the engine does.
4. **Recorded, visible:**
   - The strategy is still fed by the host FaceObserved event rather than the 0x46 recipient (no seam; the local 0x46
     also requires HasExternalInterface). It reports MISSING and is noted on M10-003.
   - FistBump's last-face turn still runs the legacy action: the gate is native, the turn is not, because the legacy
     behaviour has no owner-bound action seam. It reports MISSING and is noted on M7-018.

## Queued (no behaviour change, or pre-existing)

- The strategy's 4 s cooldown is in double (native f32, 0x0060CD60..0x0060CD82); pre-existing.
- `_robotPose` is now unused in the strategy constructor.
- The runner's say-name branch uses the ungated GetFace when no owner is bound; it is reachable only through a
  construction that throws first.

**No record settles:** M13-014, M10-003 and M7-018 each keep other open items. The production defect (faces never
verified after the turn) is fixed.
