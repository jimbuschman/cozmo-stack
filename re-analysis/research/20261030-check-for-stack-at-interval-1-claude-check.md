# CHECK-FOR-STACK-AT-INTERVAL-1: Claude check (manager, 2026-10-30)

**Verdict: FIX REQUIRED. Not merged.** Handoff `codex-m13-manager-2` 8a6fa50b (gate on e1bb5581, 6905 PASS). Code diff
`git diff 3adec271 8a6fa50b -- cozmo-stack`. **Nothing settled.** It also sits on top of the unmerged
SPARKS-BRING-CUBE-TO-BEACON-1 (FIX), so it merges only after that fix passes.

## Blocking: the missing custom-marker path re-selects the behaviour every tick

- **The stack:** `NativeCheckForStackAtIntervalBehavior.cs` `Unavailable`/`OnStop` stops the behaviour Interrupted when the M12-033
  custom-object input is missing. The deadline stays at 0, so readiness passes again at once (the test
  `MissingDiagnosticReentryAndDeferredReplacementCannotStopNewManagerRun` asserts `IsRunnable` is true right after Stop).
  StrictPriority has no repetition penalty. Whenever production reaches the SparksKnockOverCubes chooser, the robot
  repeatedly starts Init and turns towards `targets[0]`, then stops. SparksPutDownBlock/SparksLookInPlace are starved.
  `ReportMissing` deduplicates per process, so only the first pass is logged.
- **The original:** the above action's callback ignores the result (0x005D7CB2..7CD4): index++, then FacingBlock or
  ReturnToSearch. The return's callback always stamps f32(now + delay) (0x005D7D36..7D52). So the scan makes one
  face/above/return pass, then waits 15 s.
- **Fix** (same shape as FIX-ACKNOWLEDGE-OBJECT-GHOST-1): when the custom-marker input is missing, skip **only** the
  above (ghost) action, with a visible MISSING naming M12-033 and the address, then run the above callback's own
  continuation: advance the index, face the next cached target or start the real PanAndTilt return, whose callback
  stamps the deadline. No synthesized native result, no early deadline stamp, no Interrupted stop. Add a
  production-chooser test showing one full pass, the deadline stamped by the return, and no re-selection before 15 s.

## Checked and agreeing (verifier, Capstone + relocations)

- **Construction:** factory 0x0059C9F0..CA0A; ctor and config 0x005D720C..7290 (delay 15, deadline 0, ghost type 4).
- **Runnable 0x005D732E..735E:**
  - strict `vcmpe`/`ble`, so equality and NaN reject;
  - the vtable+0x90 slot (0x010286E8) is UpdateTargetBlocks 0x005D7934;
  - the zero gate slots.
- **UpdateTargetBlocks:** families {2,1} from 0x00C6B390, in robot frame; it appends with no clear and no dedupe.
- **The other steps:**
  - Init 0x005D736A..7390 (ObjectID consumed even when the cache is empty);
  - Setup 0x005D7398..73B6;
  - Stop 0x005D73BA..73E0 (the deadline is kept);
  - face 0x005D73F6..743C (Pi, false, false; the callback ignores the result);
  - missing target 0x005D7618..7674;
  - return 0x005D785A..78C8 = PanAndTilt(yaw, 0, true, false), through the void-callback wrapper 0x005BF790, so any
    result stamps;
  - deadline 0x005D7D36..7D52;
  - Update/Resume are IBehavior's (0x005BDA56).

## Queued (not blocking)

1. **Before anything settles:**
   - the new class needs a `// fidelity:` tag;
   - the six tests need `reach:` labels.

   No production reach exists for any named record: the chooser tests set Spark State=2 by reflection, and the ghost
   child is a test seam.
2. **The `IsRunnable` override** skips the base `Animations.Library is null` → `MarkReadinessUnavailable` check
   (SteppedBehavior.cs:218-225). Keep the base check.
3. **A parallel copy:** the legacy `CheckForStackAtIntervalBehavior` is still registered under the same ID
   (Behaviors.cs:570, CubeGameBehaviors.cs:972). This is the same precedent as KnockOverCubes. Retire it with the
   cleanup.
4. **The return yaw f32 versus double:** this belongs to the shared M11-066.
5. **The lifecycle record** Codex proposes goes to the operator, as for M15-086.
