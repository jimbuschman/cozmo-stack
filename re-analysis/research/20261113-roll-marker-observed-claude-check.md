# ROLL-MARKER-OBSERVED-1 merged (manager, 2026-11-13)

Worker job commit 74de8b30 (full gate 8383/8383; verifier FAIL on one premise, fixed). Handoff:
`re-analysis/jobs/trial-m13/HANDOFF-ROLL-MARKER-OBSERVED-1.md`.

**Re-read in the `.so`:** SetMarkerAsObserved's gate: GetWithRespectTo (`blx 0x4A40FC` at 0x00876E5E), `cmp r0,#1`,
`bne.w 0x00876F76` (0x00876E64): a failed re-expression skips that KnownMarker. Matches the handoff.

**Verdict: PASS.** The roll's dock stop (IDockAction::Init 0x0300001D) in the whole-stack test is a rig artifact: the
Freeplay rig gives the offline VisionSystem a parentless camera pose, so the gate fails. The engine and the stack's
queued frame path parent the camera to the historical robot state in the world origin, and the production-form
ingress tests reach DockWithObject. Nothing to build on the engine path.

The verifier's correction (production frames do carry `HasLegacyFrameInput`; the withhold branch is armed but does not
fire because the roots match) is in M11-013 and the test comment.

**M12-036** gains a unit-reach test with a located charger inside the obstacle Z band; inclusion rests on the hand
derivation, not on an observed list (noted in the record).

**Released:** MARKER-WITHHOLD-PER-MARKER-1: on a failed gate the engine skips only that marker; the stack withholds the
whole selection. Unreachable in production as far as traced, so low priority.

No record settled; no status changed.
