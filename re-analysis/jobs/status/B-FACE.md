CLAIMED deepseek 2026-10-04

Job B-FACE: build the needs-driven face distortion (DesiredFaceDistortionComponent) and the live-idle selection from
Codex's `research/20261004-procedural-live-extraction.md` (P01-P10, D01-D18). Records stay IMPLEMENTATION_GAP
(CHECKLIST 6).

## Log

- 2026-10-04: claimed.
- 2026-10-04: rows checked. `cozmo-verifier` independently re-read every P01-P10 and D01-D18 row in the binary
  (SHA256 02263c07…989e1) and returned PASS; the manager had already re-checked D07/D08/D18 and P04. The config
  graph nodes and multipliers were checked against `needs_handlers_config.json`.
- 2026-10-04 batch 1 (DesiredFaceDistortionComponent, D01-D17): new
  `src/Cozmo.Robot/Behavior/DesiredFaceDistortion.cs`; `Needs.cs` owns it at +0x3D4 and inits it from the shipped
  `needsBasedFaceDistortion` block. Getter in the engine's order: tick cache on BaseStationTimer::GetTickCount, the
  -1 sentinels, params/RNG/pause gates, deadline, repair-level degree graph with the 0.1f threshold, degree draw
  before cooldown draw (float32 bounds, f64 RandDblInRange, float32 conversion), deadline = float32(now+cooldown).
  The manager corrected the implementer's parse-abort to the engine's fall-through (0x0063b478 bne 0x63b488; the
  null branch b 0x63b4ec), including the shipped degree-parser-checks-cooldown-graph quirk.
- 2026-10-04 batch 2 (wiring, D07/D08/D18, P07): `TrackLayers.cs` unset seam is the engine's -1.0f sentinel and the
  compare is the exact 0x3727C5AC; `FreeplayStack.Create` wires `Scheduler.DesiredFaceDistortion`,
  `needs.TickCount = robot.Engine.Timer.TickCount` and `needs.DistortionRng = ContextRandom`.
- 2026-10-04 batch 3 (live-idle selection/failure, P01-P06, P08): P01/P02/P04/P06/P08 were already built by R-BEH2
  batch 2; added the P05 `_errG` write (GOT 0x0103E790 = 0x0105DD34) on the LiveUpdateFailed path.
- 2026-10-04: `cozmo-verifier` PASS on the whole diff (no behavioural contradiction, no circular test). Non-blocking
  findings fixed: the `_errG` citation address (0x0105DD34, not 0x0106DD34). Remaining non-blocking: the
  `GraphEvaluator2d::EvaluateY` of a 0-node graph and `asFloat` of a non-number key are UNKNOWN (non-live; the
  shipped config has numbers and non-empty graphs); the RNG seam passes ContextRandom after Init, no live-path
  divergence.
- 2026-10-04: correction C3 recorded in `inventory/M7-behaviour.md`; M7-005/007/008/009/010/016/017 `unresolved`
  corrected (distortion built; M7-017's "DesiredFaceDistortion has no source" replaced); M7-017 evidence gained the
  D01-D18 and failure-path citations; M7-behaviour re-approved.
- Gates: `fidelity.py --check` exit 0; full suite `dotnet test cozmo-stack/Cozmo.sln` 3323/3323 passed, 0 skipped.

## For the integrator

- Records M7-005/007/008/009/010/016/017 stay IMPLEMENTATION_GAP, each `unresolved` starting "built, awaiting strong
  verification:". A strong (Opus) verifier settles them.
- M15-014's `unresolved` still names the DesiredFaceDistortionComponent as unbuilt; it now exists, but that record is
  owned by M15 and also names StarRewardsConfig/LocalNotifications, so it was left for the M15 owner.
- Out of scope, left as the visible gap: no fixed engine producer pushes 0x198; it arrives through the
  PushIdleAnimation message (tag 0xAE, P09/P10) from the app.

DONE 2026-10-04