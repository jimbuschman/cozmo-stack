# Final gate attempt 1 — ABORTED, not a pass

Commit tested:02e83626, command `sh scripts/test-full.sh` with real Python313/dotnet PATH and local testhost escalation. Fidelity validation passed710 unchanged records. Test runner completed5009 tests (0 assertion failures,0 skipped) then host crashed; partial `Passed!` text is not a full-suite pass. Script exit1; no passed fingerprint recorded.

Fatal stack:

```
System.InvalidOperationException: MISSING: M1-034/M11-067 null pose node operation skipped
Pose3d.RequireNode Geometry.cs285 -> get_Translation249
LatticePlanner.ToStateC1365 -> Enumerable.ToArray
NativeLatticePlannerRuntime.DoPlanning121 -> Worker156
```

Blame current test: `M13NativeMapImportTests.ContactCreatedChargerIsExcludedThroughRealPathComponentDispatch`. Full raw log `.scratch/test-full.log`; Sequence attachment under `tests/Cozmo.Protocol.Tests/TestResults/7a3164ad-ce1e-43af-8cae-2a60251f02a3/Sequence_57d0e7906a494cb3b22d82899fca5474.xml` (local generated artifacts).

Bounded trace by builder and independent reviewer: test declares m, then goal, then drive; reverse using cleanup disposes drive then goal (Node=null) before m joins the native asynchronous planner. Runtime copies goal list but retains Pose3d wrappers; pending F0 can be handled before worker sees F1. Focused runs happened to complete worker first; full-suite scheduling exposed the race. Reviewer explicitly acknowledges this escaped initial review.

Correction limited to fixture lifetime: keep borrowed goal alive until native worker join, including assertion failure. No arbitrary sleep, catch/skip, production source or source-evidence change. Actual dispatch/import assertions retained. Affected regression and independent teardown review required before repeat full gate.

Separate production uncertainty remains: this fixture correction does not establish faithful native goal-copy/start snapshot ownership or certify early-disposal API behavior. M11-084 RECOVERABLE_GAP goal-copy window and M13-033 IMPLEMENTATION_GAP worker ownership remain real, high-priority follow-up. Existing native vector-copy prose is not treated as whole-path certification. No no-effect or unsupported-feature decision is made.
