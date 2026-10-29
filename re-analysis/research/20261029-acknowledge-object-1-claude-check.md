# ACKNOWLEDGE-OBJECT-1: Claude check (manager, 2026-10-29)

Branch `codex-m13-manager-2` at 83b77741 (build 5791affd). Read: `research/20261029-acknowledge-object-1-read.md`.
Record: M15-081 (added with operator approval in 3aef7206, before this check).

**Verdict: FIX REQUIRED. Not merged. Nothing settled.**

## Checked and agreeing with the read

- **Targets:** a signed int set, lowest id first (B1). NullObject erases the target and moves on, with no listener call
  and no objective (B2).
- **High flag:** relative z > 0x42076666 = 33.85 mm, with the GetWithRespectTo result ignored (B3).
- **Initial compound:** turn with tolerances, then verify with +0x138 images, then the lift-safe animation unless
  alwaysStreamline or hard spark (B4..B7).
  - The hard-spark flag matches IBehavior +0xD8 at 0x005BCCAA..0x005BCCC2
    (`[[robot+0x2C]+0x44]+0x58 != 0x55 ? !+0x5C : 0`), the same as the other built behaviours.
- **Result:** SUCCESS clears +0x14A and goes to LookForStackedCubes; any other result goes to FinishIteration (B8).
- **L1..L6 branch order:** the threshold 0x3F1A266B = 0.6021487 rad and the -25 mm / 100 mm/s backup (G3) match.
- **FinishIteration:**
  - listener slot 0, then erase, then, if targets remain, objective 0x1B and BeginIteration;
  - after the last target, the final turn has no tolerances, and the objective and BeginIteration follow whatever the
    turn's result (F1/F2).
- **Stop:** listener slot 1 ClearDesiredTargets, then the target set is cleared. +0x149 and +0x14A are kept (S1).
- **Runnable gates:** +0x20/+0x24 false, +0x28 true (runnable while carrying; R1), and the target set non-empty (R2).
- **Ghost:** consumes one process-wide ObjectID at the first Init and keeps it (I1/I2, operator decision N2).
- **Engine-owner lock** (`EnterSynchronousOwner` on `_tickLock`): reviewed by Codex in three batches, with dedicated
  lock-order and reentrancy tests and the full gate (6746/6746). Not blocking.
  - **Queued cleanup:** BehaviorManager now has AcknowledgeObject-specific generation checks. A generic hook would be
    cleaner.

## Blocking finding: the production stacked-cube path holds the reaction forever

In production none of the `Supplied*` helper delegates is set.

1. After a successful acknowledgement of an ordinary ground cube, B3 gives `_highTarget = false` (z of about 22 mm),
   so L3 is skipped.
2. L4 calls `GhostVisibility`, which finds `SuppliedVisibilityReason` null.
3. `MissingGhost` sets `_waitingMissing = true`.
4. `KeepsRunningWithoutAction` then stays true with no action queued.

AcknowledgeObject therefore never finishes. There is no listener call, no objective 0x1B, no next target and no
turn-back, and freeplay is stuck in the reaction until another reaction interrupts it. The builder's ledger confirms
this was intended ("production M11-010/M12-033 visible hold").

This blocks under "What we reproduce" 1–4: the robot and the API stall in a state the engine never enters. Leaving the
iteration unfinished is not "no fabricated result"; it is a different, unsourced result.

The engine's own branch makes the answer clear. When the ghost is not visible and not outside the FOV, and the target
is not high, LookForStackedCubes goes straight to FinishIteration (L4). Skipping only the ghost look is the smallest
unit the stack cannot reproduce. This is the same skip-and-log treatment the standing rule gives hangs.

## FIX-ACKNOWLEDGE-OBJECT-GHOST-1 (Codex)

1. **Skip the look, don't hold.** Wherever the stacked-cube path lacks an input, skip the ghost look:
   - the M11-010 visibility reason;
   - the M12-033 target dimensions, closest marker or ghost marker geometry (including `MissingGhostTurn` inside
     `GhostLookSequence`);
   - the M13-026 head angle.

   Instead, report a visible MISSING naming M11-010/M12-033/M13-026 and M15-081, then continue with
   FinishIteration(generation). That gives the listener call, the erase, then the objective and next target, or the
   final turn-back. Do not invent a visibility reason, and do not loop back into LookForStackedCubes on a skipped look.
2. **Host-owner gaps end clearly.** The paths that mean a host-owner gap (no BehaviorManager, no
   ManipulationSystem/robot) should end the behaviour with a visible error rather than hold forever. Production
   reaches neither path.
3. **Keep the existing tests.** The supplied-helper unit tests stay as they are.
4. **Add a production-wired regression:** a configured ground cube with no supplied helpers is acknowledged, the
   MISSING is logged, objective 0x1B is delivered, a second target is processed, and the final turn-back runs and the
   behaviour completes.
5. **Gate and hand back.** Re-verify the affected diff and tests only, run the full gate once, and hand back as READY
   FOR CLAUDE.

## Re-check of FIX-ACKNOWLEDGE-OBJECT-GHOST-1 (b8b35a77, handoff a546fe07): PASS, merged

- **The hold is gone:** `_waitingMissing` is removed. A missing M11-010 visibility, M12-033 dimension or marker, or
  M13-026 head-angle input logs `M15-081 M11-010/M12-033/M13-026 stacked-cube look unavailable: ...` and continues to
  FinishIteration(generation). No visibility reason is invented, and nothing loops back into LookForStackedCubes.
- **Unavailable custom turn inside the look sequence:** the diagnostic is logged, then deferred ActionList cleanup
  runs StopActing and then FinishIteration under the generation check.
- **Host-owner gaps** (no manager, no manipulation/robot) now end the behaviour with a visible diagnostic.
- **Production-wired regression:**
  `ConfiguredGroundTargetsSkipMissingGhostThenNotifyAndFinishInOrder` runs the configured container, the registered
  strategy and the manager with no supplied helpers, on two ground cubes. Its expected order comes from the read (F1):
  - listener, then objective 0x1B for the first cube;
  - the second cube's compound, then its listener;
  - the final turn, whose result is ignored, then objective 0x1B;
  - the behaviour ends.

  Per-input and reentrant-Stop tests cover the other paths.
- Codex reported independent 239/239 and full 6754/6754 PASS. The merged main is gated here.

**Nothing settled.** M15-081 stays IMPLEMENTATION_GAP: the stacked-cube look is skipped in production until
M11-010 ghost visibility and the M12-033 ghost marker geometry are built.
