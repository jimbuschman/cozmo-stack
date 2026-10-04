CLAIMED claude-sonnet 2026-10-04

Job R-FIX3 (loop): fix the next group of defects recorded by the Opus verifications (research/20261002-B-CORE2-verify.md, 20261003-R-BEH2-verify-1-2.md, 20261003-R-BEH2-verify-3.md) as written in each record's `unresolved`; skipping anything that needs the ActionList, the game channel or ProceduralLive. Records stay IMPLEMENTATION_GAP (CHECKLIST 6).

## Round 1 (2026-10-04)
- Stream X (M7/M8): ScoringChooser (f32, RandDbl(0.1) from the robot RNG, 0.01f floor), M8-003 per-class score overrides, M8-009 plain track-lock names, M8-001 base runnable slots, M7-013 NaN Add and BadTimeStep text, log texts (M7-017, M8-002/003/008, M7-021), M7-003 InitInternal return 0, M7-014 no arbiter-wide fallback, M8-008 CalibrateHead through the action runner if it can host it.
- Stream Y (M1/M3/M4): M3-031, M3-026, M3-030, M1-041, M4-020, M4-001, M1-024: log texts/formats, the deleted `Robot.SendSyncTime.FailedToSend`, the piecewise per-minute Needs decay, the NV resend count and the action-timeout slots; the +0xE8 dependency on M3-027 stays MISSING.
Both run in parallel in one tree (disjoint files); verifier passes, then one commit.

### Round 1 result (2026-10-04)
Built (read-only Claude `cozmo-verifier` passes; the reports are the agents' messages, not files):
- Stream Y (M3-031, M3-026, M3-030, M1-041, M4-020, M4-001, M1-024, M4-016, the M15-014 throttle): verifier round 1 PASS (no blocking), 7 queued items. NvStorage log texts/formats and levels from the binary (lowercase %x, QueueingReadRequest, ExecutingReadCallback, the shared resend: 7 resends and 8 transmissions), Robot.SendSyncTime.FailedToSend and the sync/ready-to-stream info logs, the head clip warnings (getDegrees f32), NeedsState.ApplyDecay piecewise per minute in f32, the action start stamp at the first UpdateActions pass (two ticks for a short timeout), the 61 s throttle anchor.
- Stream X (M7/M8): verifier round 1 FAIL (1 blocking: Graph2d.ReadFails stricter than ReadFromJson), round 2 FAIL (3 blocking: AddNode's order check, the running short-circuit before the gates, the PutDown back-up draw's f32 width), then fixed; the last hunks had no separate verifier pass. ScoringChooser (RandDbl(0.1), f32, 0.01f floor), the five per-class score overrides (none has a C# class), plain track-lock names, the 79-class runnable-gate table (verified with 0 mismatches) with production-source inputs including the charger platform, Emotion::Add NaN, the log texts, M7-003, M7-014 (no arbiter-wide fallback), the PlayAnim exact trigger lookup, CalibrateMotorAction texts, the ReadFromJson failure conditions with its warnings and AddNode's OORange drop.
- One flaky test made deterministic: M7BatchThreeBTests.WhileCarryingFistBumpPutsTheObjectDownFirst (waits for the action instead of one fixed Step; 10 of 10 clean).
- Full suite 3356/3356, `fidelity.py --check` clean (427 records). All records stay IMPLEMENTATION_GAP, unresolved starts "R-FIX3 round 1 (2026-10-04), built, awaiting strong verification:".

Live consequences to know: the runnable gates now refuse, as the engine does, the classes whose slot is 0 off treads, on the charger platform (all but DriveOffCharger, ReactToOnCharger, OnboardingShowCube) and while carrying; DriveStraightAction's distance is a float for every caller.

### Round 2 candidates (queued)
- NvStorage.cs: X8 -> %x and the engine formats for the lines outside the 7 records (Write.InvalidTag, AckdTagNeverRequested/AckdTagBaseTagWasNeverSent 0xBFB6EB, TooLittleReadData, InvalidHeader 0xBFB9C5, InvalidDataSize, ReadingRestOfData, HandleNVOpResult.Recvd 0x00642FFE, WriteSuccess/WriteFailed 0xBFB81C, ExecutingWriteCallback and the RobotDataBackupManager call 0x006433F6..0x0064341C): M3-028/M3-029 are EXACT_SOURCE but their log texts contradict the binary: these records need to be re-checked.
- NvStorage: a visible MISSING marker for the +0xE8 payload; NumRetriesExceeded's global error byte (0x645D62..0x645D76).
- The M15-014 anchor test; Needs clock mixing; M4-016's Init-in-first-UpdateInternal structure.
- M8-008: host CalibrateMotorAction in the action runner (Motion.cs), M7-008: the BaseStationTimer clock, M8-001: SparkBehaviorDisables inputs, StartActing's QueueAction failure.
- M7-014: lock tables for the 11 classes that still call Scope.DisableReactions().
