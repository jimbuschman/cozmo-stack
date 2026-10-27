# PICKUP-STACK-1 Claude check (2026-10-27): PASS

Branch `codex-m13-manager-2` at baa24a33 (handoff `jobs/trial-m13/HANDOFF-PICKUP-STACK-1.md`, gate candidate e6973c7a).
Spot-checked against `libcozmoEngine.so` 3.4.0-1204 (Capstone Thumb).

## Settled

**M13-026** (GetAbsoluteHeadAngleToLookAtPose and Robot::ComputeHeadAngleToSeePose): IMPLEMENTATION_GAP ->
EQUIVALENT_IMPLEMENTATION. Location moved to `Vision/VisionSystem.cs` (the native overload carries the tag).

| Step | Binary | C# |
|---|---|---|
| BadProjectedZ | 0x0051848C vcmpe q.z, 1e-5; 0x00518494 `bls`: an unordered (NaN) compare does not branch, so NaN q.z continues | `qz <= eps` (false for NaN): match. The record's authority text ("not q.z > 1e-5") is wrong on NaN; the code follows the binary. |
| Convergence | 0x005184AA vcmpe threshold, \|dv\|; 0x005184B2 `bge` (NaN iterates) | `threshold >= MathF.Abs(dv)`: match |
| Step | 0x005184BC atan2f(dv, fy) (PLT 0x004A4510), * -0.8f (s22), add | match |
| Loop count | `cmp sl,#0x19; blt` 0x005184D0/DE, sl from 1 | k = 1..25 |
| Write before MaxIterations | 0x005185B6 Radians::operator= then 0x005185C6 `cmp r0,#0x19; bne` | write, then k==25 -> MaxIterations, 1: match (the old C# returned before the write) |
| Exhaustion | falls out with sl = 26, returns 0, no write | angle stays the caller's 0: match |
| Fallback 0x0054B428..0x0054B55A | each `it pl` / `it gt` takes the flags of the latest `vmrs`, interleaved with the next `vcmpe` | all nine picks, the sqrtf NaN fallback (0x0054B466 `bvc`), atan2f(h, D) and the final adds match `AbsoluteHeadAngleToLookAtPose` |

Boundary: only bionic atan2f and the sqrtf fallback are EQUIVALENT_IMPLEMENTATION (unshipped system library, M13-023 (e),
precedent M5-021, M4-052). Pose algebra runs on the native Transform3d port; the native overload parents the neck under
the live MAIN pose. The public flat-input static adapter stays M11-066's labelled gap and is not this record's path.

## Accepted without settlement

- Owned native visual subtree (PanTilt/TurnPose/Turn/Wait runners): records M13-020 and M12-037 stay open, as the handoff says.
- N5: the shared Rig now has its own action clock, snapshot from the transport clock once (`ManipRig.cs`). Test-only; assertions unchanged.

## Queued cleanup

- `PanAndTiltActions.cs` AbsoluteHeadAngleToLookAtPose still carries `// fidelity: M13-021`; it belongs to M13-026. **Done** in 959bbc26 (the tag now reads M13-026; checked by the manager 2026-10-27).
