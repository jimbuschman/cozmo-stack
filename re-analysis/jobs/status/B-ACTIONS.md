CLAIMED opencode-deepseek 2026-10-05 00:00

# B-ACTIONS status

Job file: `re-analysis/jobs/B-ACTIONS.md`. Rows: `re-analysis/research/20261004-actionlist-extraction.md`
(Codex). This job never settles a record (CHECKLIST.md section 6).

## Row verification (before building)

`cozmo-verifier` checked batch-1 rows A1-A9, T1-T12, Q1-Q15, C1-C6 against the binary:
41 HOLDS, 1 PARTIAL (T10: the robot+0x484 loop is an object-undiscovery/`ObjectUnavailable` hash-table
iteration, not a virtual component Update loop; addresses/order after it are right). T10 is out of scope for
this job. Q6/Q7/S5 were checked by the manager earlier. No row failed.

## Batch 1 - the queue and the tick (rows A, T, Q, C)

- **Built.** `cozmo-stack/src/Cozmo.Robot/Actions/`: `ActionList.cs`, `ActionQueue.cs`, `IActionRunner.cs`
  (with `EngineActionResult`, `QueueActionPosition`, `ActionRunnerTagCounter`, abstract `ActionRunner`),
  `ActionWatcher.cs` (batch-1 calls only). `CozmoEngine.cs`: engine-owned `ActionList`, ticked at the
  0x005140BC position (after AIComponent, before the animation streamer). Tests:
  `cozmo-stack/tests/Cozmo.Protocol.Tests/ActionListTests.cs` (30 tests).
- **Verified.** `cozmo-verifier` PASS on the diff against the rows and the binary. First pass FAILed on four
  blocking findings (Q2 read the tag instead of the state +0x18; the Q2 discard did not emplace the key-0
  queue; `SetTag` did not write the BAD_TAG state; a circular Q2 test). All four fixed and re-verified PASS.
- **Gates.** `fidelity.py --check` clean (427 records). Full suite `dotnet test cozmo-stack/Cozmo.sln`:
  **3353 passed, 0 failed, 0 skipped**.
- **Commit.** (this commit)

### Queued (non-blocking, from the verifier; fix in a later batch)
- Position > 5 is tested before the disabled/BAD_TAG block; native tests it after (0x0053DA24). Differs only
  for an invalid position combined with a discard.
- T7/T8 Viz text / SDK status presentation is not built (known M11/M12 gap).
- A9 clearing path calls `WatcherEnding` instead of the virtual delete; the queue deletion-set tag is
  released before D7. Both batch 4.
- `ExternalActionsDisabled` (robot+0x2C7) has no production writer yet; its writer is
  `BehaviorManager::UpdateRobotPropertiesForReaction` 0x005A2F54 (M8/M10, not built).

### MISSING (still open)
- What writes robot+0x2C7. The Q2 gate is built but inert in production until that writer exists.

## Remaining batches
2 (IActionRunner/IAction lifecycle, move Motion onto it), 3 (compounds), 4 (completion to the game).
Records stay IMPLEMENTATION_GAP until a strong verifier settles them.
