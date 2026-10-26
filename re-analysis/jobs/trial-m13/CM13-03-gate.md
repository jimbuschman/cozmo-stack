# CM13-03 full gate

Code: b1c1169d4a8291b9f00b5d007a86dcc7912e12aa on codex-m13-manager-2, after independent build PASS.

Ran `sh scripts/test-full.sh` exactly once on committed code. Local Python313/dotnet were first on PATH; local testhost IPC used authorized escalation. Fidelity validation PASS710; no record/status changes.

**Full-suite result: FAIL. 6274 passed, 1 failed, 0 skipped, total6275, duration6m48s.** No passing content fingerprint recorded. No source/test changes after this run.

Failure: `NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates`, NavigationTests.cs:976.

```text
Assert.Contains() Failure: Filter not matched in collection
... reach for block 7 -> Success ...
... start DriveAndFlipBlockAction(7) ...
... DriveAndFlipBlockAction(7) -> Abort ...
```

The missing expected trace is `FlipBlockAction.CheckIfDone: compound result Success; object 7 marked Unknown`. The earlier assertion of the ignored DidNotReachPreActionPose drive result passed. All tests finished; no testhost crash/hang.

Original local log is preserved in `.scratch/test-full-attempt-1.log` (the script's `.scratch/test-full.log` is also retained). The failure is not treated as a pass or hidden by rerunning the full suite.

Bounded investigation: the test never loads a lattice planner. The new runtime/backend code is not on this execution path; the added upstream Configure MISSING may be reached. Outer Abort can also represent an async fault/cancellation; it does not identify a root cause. Focused current/baseline comparison and final disposition are recorded in the handoff when complete. No unrelated correction or weaker assertion is authorized by this evidence.
