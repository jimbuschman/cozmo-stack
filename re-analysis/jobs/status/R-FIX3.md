CLAIMED claude-sonnet 2026-10-04

Job R-FIX3 (loop): fix the next group of defects recorded by the Opus verifications (research/20261002-B-CORE2-verify.md, 20261003-R-BEH2-verify-1-2.md, 20261003-R-BEH2-verify-3.md) as written in each record's `unresolved`; skipping anything that needs the ActionList, the game channel or ProceduralLive. Records stay IMPLEMENTATION_GAP (CHECKLIST 6).

## Round 1 (2026-10-04)
- Stream X (M7/M8): ScoringChooser (f32, RandDbl(0.1) from the robot RNG, 0.01f floor), M8-003 per-class score overrides, M8-009 plain track-lock names, M8-001 base runnable slots, M7-013 NaN Add and BadTimeStep text, log texts (M7-017, M8-002/003/008, M7-021), M7-003 InitInternal return 0, M7-014 no arbiter-wide fallback, M8-008 CalibrateHead through the action runner if it can host it.
- Stream Y (M1/M3/M4): M3-031, M3-026, M3-030, M1-041, M4-020, M4-001, M1-024: log texts/formats, the deleted `Robot.SendSyncTime.FailedToSend`, the piecewise per-minute Needs decay, the NV resend count and the action-timeout slots; the +0xE8 dependency on M3-027 stays MISSING.
Both run in parallel in one tree (disjoint files); verifier passes, then one commit.
