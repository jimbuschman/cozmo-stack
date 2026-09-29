DONE 2026-09-29 00:41:00 -05:00 — M9-wwise-music built and verified: 19 records settled EXACT_SOURCE, 3 IMPLEMENTATION_GAP cross-layer, 4 RECOVERABLE_GAP, 1 HARDWARE_ONLY, 1 COMPATIBILITY_POLICY; verifier PASS; full suite 1790/1790; commit 11ddd4a.

## Commit
- 11ddd4a — B-M9: build M9-wwise-music from its inventory; settle 19 records (verifier PASS, suite 1790/1790). 32 files, 1327 insertions / 702 deletions.

## Records settled EXACT_SOURCE (19)
M9-001, M9-002, M9-003, M9-004, M9-005, M9-006, M9-007, M9-008, M9-009, M9-010, M9-012, M9-015, M9-017, M9-018, M9-019, M9-020, M9-021, M9-022, M9-026.

The two blocking contradictions were fixed:
- M9-015/M9-022: the live singing/event path now uses the recovered Wwise 64-bit LCG and the eligibility/blocked-list algorithm (`WwiseSelection`), not `System.Random` and "weighted avoiding last".
- M9-007/M9-009: modulator bindings apply the RTPC scaling AFTER the curve (`EvaluateScaled`, the ±20·log10 map) and accumulate via the recovered store, not raw one-decibel curves.
- M9-026: the exact recovered EQ/limiter DSP is wired into the live `Robot_Bus_1` chain; the stand-in biquad/limiter removed.
- M9-002: the per-step 60 s `TriggerAnimationAction` timeout (Correction C1a) and the switch→reaction-lock→listeners→compound order.
- M9-003: the per-cube running mean/reset, the 500 ms shake-duration log, the `+0x84` acting-state return (Correction C1b) and the Stop order.

## Gaps left
- **M9-011** — IMPLEMENTATION_GAP, `unresolved` names M6-017/M6-018: the exact chain (two EQs, limiter, Hijack order) is wired, but the engine runs the bus at the 48000 Hz Wwise mix rate and the Hijack resamples to 22320, while this stack runs the chain at 22320.
- **M9-027** — IMPLEMENTATION_GAP, `unresolved` names M6-017/M6-018: the recovered EQ path is wired; at the stack's 22320 rate the shipped 14298 Hz low-pass is capped, where at the engine's 48000 mix it is in-band.
- **M9-028** — IMPLEMENTATION_GAP, `unresolved` names M6-016: on-robot game object 7 is built and wired; off-robot game object 6 needs M6-016's OnDevice route.
- **M9-013, M9-014, M9-024, M9-025** — RECOVERABLE_GAP (unchanged; source_investigation_exhausted false).
- **M9-023** — HARDWARE_ONLY; **M9-016** — COMPATIBILITY_POLICY (66 ms stack lead).

## Inventory correction
Correction C1 (job B-M9) was added to `re-analysis/inventory/M9-wwise-music.md` from an extractor pass and re-approved (M9-wwise-music.approved.json updated). C1a fixes M9-002's evidence address (`0x005EEDCA`, not `0x005EEE0C`) and its wording (per-step 60 s timeout, compound fails on expiry). C1b names `IBehavior+0x84` and the 0/1/2 consumers. C1c records the mix-rate consequence for M9-027.

## Files outside the layer's usual folders
- `cozmo-stack/src/Cozmo.Conformance/Wwise.cs` (caller-only, to compile after the `WwisePlayback.Resolve`/`WwiseAudioSource` signature changes).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/HardwareCatalogFidelityTests.cs` (two status rows: A2 M9-022→M9-027, A3 M9-026→M9-011, to match the new statuses).
- `cozmo-stack/tests/Cozmo.Protocol.Tests/CoreReviewTests.cs` (call sites for the changed selection API).

## Queued non-blocking (manager, for the integrator)
- `WwiseAudioSource.GainForKeyframeVolume` still uses the unscaled `rtpc.Evaluate` for `Event_Volume`, contradicting approved M6 gapA 5.5 (scaling 2, `20·log10(sin(v·π/2))`). This is M6-009's unwired store, not an M9 record.
- `WwiseAudioSource.cs` had a second stale `(M3-024)` comment occurrence left in scope.

## Robot check the operator should run (not waited on)
Singing, to exercise M9-001..003, 017 and the wired Wwise path on hardware:
1. Cozmo on the floor, off the charger, one cube powered and within ~30 cm. PC on the robot's Wi-Fi.
2. On the Cozmo machine, from `cozmo-stack`: `git pull`, then `dotnet run --project src/Cozmo.Conformance -- control-check 172.31.1.1 --obb "<unpacked OBB dir>" --allow-drive` (the A/A2/A3 singing checks and the CUBES check cite M9-017).
3. Copy the printed `re-analysis/acceptance/hardware/<stamp>-CONTROL/` bundle back and tell the manager.