# Codex marker diagnostic, pre-dock side, dock overshoot, cube world state, TrackFace lifecycle: Claude check (manager, 2026-11-09)

Merged from `codex-m13-manager-2` at 90c8718c. Codex's batch gate on 906abe9d passed 8094/8094.

## PICKUP-PREDOCK-SIDE-1: PASS, no stack bug

- **The binary:** the LIGHTCUBE face-def table at 0x00C45CA0 was re-read from the shipped ELF. Its per-face entries carry
  the docking masks (05 / 0F bytes at +0x0C), matching the handoff and M12-021 (already EXACT_SOURCE).
- **The capture (run 014f35):** the stack's observed cube pose has Q0 = 0.7067 and Q2 = -0.7067, a 90° rotation about Y.
  The cube's top face points at the robot, so the cube was lying on its side.
- **The operator confirmed it, 2026-11-09:** the cube was on its side ("side").
- **Conclusion:** the native filter leaves only the Bottom face. On a cube lying on its side that face is on the far
  side, so the far-side approach is the engine's own defined result.
- **The BLOCKED.md policy question is closed without a policy.** The native result is kept. The hardware guide and
  harness prompt now require the cube upright with its lights on top, and the drive round stays opt-in.
- **Records:** no settlement. M12-021 and M12-029 stay as they are.

## DOCK-OVERSHOOT-1: PASS (harness tag fix plus numerical replay)

- The captured request body and the 14 pre-result 0x48 corrections reproduce the native geometry to within 2e-5 mm.
- The dock in run e9520f was against the cube's top face, on a cube lying on its side. That alone can explain the
  NoLoadDetected (the lift's fork has nothing to engage on the lit face). It is not proven.
- **Next step:** the upright-cube rerun decides whether any overshoot remains.
- **Records:** no settlement.

## MARKER-LIBRARY-DIAG-1: PASS

- A once-only startup warning was added. No robot traffic and no init-order change.
- **Records:** no settlement. M11-001 is already EXACT_SOURCE.

## AI-BODY-CUBE-WORLD-STATE-1 and TRACK-FACE-LIFECYCLE-SETTLEMENT-1: merged

- Both are merged on the strength of their tests and Codex's gate.
- Settling M11-038 and M14-003 needs a separate source check of the cited ranges, and is queued as
  TRACK-FACE-AND-WORLD-STATE-SETTLE-CHECK. Neither record changes status here.
