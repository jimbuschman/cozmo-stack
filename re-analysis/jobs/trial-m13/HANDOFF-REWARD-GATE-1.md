# REWARD-GATE-1 — WIP, NOT READY

Stopped at the operator's usage-limit request. Baseline is origin/main d45b08c3. Last checked checkpoint is38c22da9; the daily/weighted draft saved above it is untested. Nothing was discarded, no full gate was run for this job, and no statuses or approvals were changed.

Built and passing at the checked checkpoint:
- Original Inventory and ProgressionUnlock owners: subscriptions, mutations, caps/throttles, NV195000/182000 snapshots and reports;67 affected tests passed.
- Lab startup, retained lookup/assignment/signal state and actual NV196000 restore/writeback;332 affected tests passed. Whole M15-062 is independently recommended for Claude after the parent gate; its stale location/evidence/unresolved still need formal alignment.
- Full Needs metadata/candidate, signed microsecond clock, nine-case resolver, original NV owner and62/69/6B reports;396 affected tests passed.
- Shipped44 LocalNotifications, D4/D5/D6, persisted85-entry input, host C calendar, same context RNG,97/98/99 reports and native clock boundaries; final416/416 PASS,0 skipped,31s, compiler/test/shutdown/postflight0. Fidelity check passed753 records.

Half-done files:
- `Behavior/NeedsDailyRewards.cs`: new daily-star, reward traversal, report codecs and weighted producer.
- `Behavior/Needs.cs`, `NeedsMetadata.cs`, `NeedsNativePersistence.cs`: full level config, Register integration and metadata joins.
- `NativeNotificationCalendar.cs`: daily calendar extension.
- `NativeDailyRewardTests.cs`, `NativeRewardCanonicalTests.cs`: new, unrun component/canonical tests.
- `PickupPyramidNativeExecutionTests.cs`: fixture ingress/options extension; `NativeNotificationTests.cs`: related draft fixture changes.

Engine reading already complete:
- 0x696366..696536: state report → daily check → notification generation → weight/chance → award → old pending report → new amount/pending/key → common tails.
- 0x696AAC: prior/current Play bracket and local year/day gate; changed-day mutation,67, completion, forced write, notifications.
- 0x698740/0x6993F8/0x69CBB8: ordered prior/current reward traversal; counter limits the next prior level, not inner descriptors; SetStarLevel resets stars before updated66 payload.
- 0x696D74/0x697110: unclamped binary32 chance floor, strict context-RNG comparison, inclusive award draw and raw Add amount. Weight equality and unordered NaN proceed at0x69638A..A4.
- Native pack71732E/71715C/784414 establishes67/66/nested reward widths;68E650/7869D0 establishes ordered28-level config. ARM payload/arithmetic oracles and exact assets are in `.scratch/reward-gate-1/` alongside `daily-reward-build-read.md` and its independent review.

Left, in order:
1. Finish the draft and verify early reviewer findings: distinguish known M1 empty-level skips from unknown inputs; check/reload only each source-used recipient; stop unknown RNG at the first actual draw while preserving known prefix effects. Final review has not passed.
2. Finish component tests and canonical300s RollACube-at30s/natural EarnedSparks proof. The current canonical draft reseeds RNG after startup and has a weak continuation assertion; these are not an accepted production-path proof and must be corrected.
3. Run affected tests, classify failures before edits, obtain independent review, then commit and run the full gate. Only then READY FOR CLAUDE. Next job afterward is VISIT-EDGE-REAL-MAP-1, not started.

Failures/check limits: no current known failing test at38c22da9. Notification run1 was415/416 due a stale missing-profile diagnostic assertion; corrected416/416. Earlier metadata compile errors and seven fixture failures were corrected and logged. The current daily draft has not been compiled or tested; it may fail. No hardware run by Codex.

Boundaries: explicit persisted notification history is required; no invented zero profile or RNG recovery. Runtime tuning and nonshipped config/calendar domains remain named gaps. Standing M1-034 skips invalid version0 application at69A052, retaining prior state/rewrite; protected M15-018's old zero wording remains for Claude to reconcile.

Worker requests stayed unchanged: reader gpt-6.1-sol medium; builder/reviewer DEFAULT with overrides omitted, actual backend unobserved. Review rounds: writers1; Lab1; metadata1 pre-freeze batch+1 frozen correction, plus compiler/fixture repairs; notifications1 pre-freeze batch+1 fixture repair; daily early review unfinished. Builder MISSING concerns are owner/input/RNG-history boundaries above. Parent remains WIP, NOT READY.
