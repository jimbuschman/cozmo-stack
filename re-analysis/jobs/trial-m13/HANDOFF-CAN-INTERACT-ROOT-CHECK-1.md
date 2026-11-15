# HANDOFF CAN-INTERACT-ROOT-CHECK-1 (Claude worker, 2026-11-14)

Job 28 (my proposal from M13-007-CALLER-CENSUS-1). Census and implementation reports are worker-local:
`.scratch/CAN-INTERACT-ROOT-CHECK-1-census.md`, `.scratch/CAN-INTERACT-ROOT-CHECK-1-impl.md`. Verifier: PASS (every address
re-read).

## The defect

CanInteractWithObjectHelper (0x0063C654) checks GetWithRespectTo(obj pose, robot pose, out) before the on-top find and
returns false when the roots differ or a node is null (0x0063C6A4..0x0063C6BE; 0x00845A46..0x00845A62). The stack had
no such step, so CanStackOnTopOfObject / CanPickUpObject threw from WithRespectTo where the engine returns false. It was
reachable: the stack's FindFreeCubeToStackOn and FindUsableCubesOutOfBeacons walked every origin, where the engine
walks only the current one, so a flat cube left in an old origin could throw.

## The change

- **Docking.CanInteractWithObjectHelper(obj, relPose)** in the engine order: family gate, resting flat, carried compare,
  the root check (false on failure), then the 15.0 on-top find (0x0063C730).
- **CanStackOnTopOfObject / CanPickUpObject** pass that out pose to IsPoseTooHigh (1.0/15/0.5 and 2.0/15/0.5), and
  return false without calling it when the helper fails (0x0063C622, 0x0063C84E).
- **Current origin only:** FindFreeCubeToStackOn (families {1,2}, mode 0 at 0x005E0188, returnFirst; M15-008) and
  FindUsableCubesOutOfBeacons (families {2,1}, mode 0 at 0x0056AF64; M15-020).
- **Neighbor:** a candidate with a null pose node is rejected through GetWithRespectTo's VERIFY-and-false
  (0x00845A46..0x00845A58), not thrown.
- The stack-only History.Latest gate and parentless fallback robot pose stay, now inside the helper (recorded in
  M12-012).

## Tests

`CanInteractRootCheckTests` (reach: unit; substitutions named): a cube under a second root gives false from all three
Can* functions with no throw (the old code threw); IsPoseTooHigh thresholds from the cited constants (z > 37.00001 and
81.00001); the two searches skip old-origin cubes (empty / null, no throw); Neighbor skips a null-node candidate.
Affected classes 704/704 (implementer), 357/357 (verifier).

## Open

- An object whose pose node is null still throws earlier, at IsRestingFlat (0x0087751C), before the root check. This
  predates the job; what the engine does there is not read. No production writer that nulls a node was found. Recorded
  in M12-012.
- CanPickUpObjectFromGround is not added to Docking: NativePickupTargetCache.CanInteract(obj, .5, 10, 0) is the stack's
  form of it.
- Queued (verifier): the Docking.cs:612-613 doc says that form "is" 0x0063C880; it uses Neighbor for the on-top step.
  FindUsableCubesOutOfBeacons uses FindLocatedMatchingObjects where the engine uses FindLocatedObjectHelper with a pushed
  predicate; same list, same order. Other engine callers of CanStack/CanPickUp (0x005715B6, 0x0057042C, 0x00570894,
  0x00570B3A, 0x005E949E) are not wired in the stack (pre-existing, out of scope).

`unresolved`/`test` only on M12-012, M13-007, M15-020, M15-008. No status changed. No record settled.
