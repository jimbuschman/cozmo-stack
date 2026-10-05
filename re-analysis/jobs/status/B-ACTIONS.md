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

## Batch 2 - the IActionRunner/IAction lifecycle (rows L1-L18)

- **Built.** `cozmo-stack/src/Cozmo.Robot/Actions/IActionRunner.cs`: the full `ActionRunner` lifecycle -
  ActionStartUpdating/ActionEndUpdating, the start branch's custom motion profile and track lock (L4/L5), the
  float timer with the timeout checked first and at equality (L7/L8/L10), Init/CheckIfDone on the same tick (L9),
  the category-4 retry (L11), the completion callbacks and Prep (L12/L13), Reset (L2), UnlockTracks (L14), the
  destruction stop-before-unlock with the signed lock owner vs unsigned stop owner (L15/L16), and
  ForceComplete/TakeRetry (L18). `CozmoMotion.MoveAction` is now an `ActionRunner` subclass with concrete
  Init/CheckIfDone, the engine clock, and the MovementComponent primitives; `SetHeadAngleAsync`/`SetLiftHeightAsync`
  build the action and queue it at NOW (or a caller position), returning `Done.Task`, which maps the stored engine
  result back to `MotionOutcome`. The per-tick `ActionRunnerUpdate` hook and `UpdateActions` are removed; the
  ActionList tick drives the actions. `NextActionTag` now draws from `ActionRunnerTagCounter.Global` (C1/U5).
- **Row verification.** `cozmo-verifier` checked L1-L18: all HOLDS; two citation corrections only (L1's
  type->union cache is 0x0053FECA..0x0053FF9C; L7's +0x70 branch is 0x00540D76..0x00540D8A).
- **Verified.** `cozmo-verifier` PASS after two blocking fixes: (1) `BehaviorManager.QueueHeadAndLift` queued the
  lift `AtEnd`, which serialised it; it now queues `InParallel` (key 1) after the head `Now` (key 0), the batch-3
  `CompoundActionParallel` stand-in (0x5A1C24..0x5A1C9C, `movs r2,#2` 0x5A1C80); (2) `ActionEndUpdating()` was
  skipped on RUNNING; native calls it on every Update (0x005405A2 `beq #0x54062c` -> 0x54062C), only `Prep` is
  conditional (0x540626). The verifier found no weakened test assertion.
- **Choices not settled by the L rows (manager to decide):**
  - the head/lift `RobotActionType` (head 0x12, lift 0x13) comes from M13-022/R-ANIM, not the L rows;
  - `GetCompletionUnion()` on the base returns the +0x1C cache (0): the concrete head/lift union is M13-022
    (RECOVERABLE_GAP) and batch 4's game snapshot;
  - M13-028's carry lift is queued `InParallel` (position 5) per its record.
- **Test changes.** The head/lift send now happens on the ActionList tick, so the tests that read the wire
  immediately after `SetHeadAngleAsync`/`SetLiftHeightAsync` tick first; the timeout tests tick once to send and
  then advance to time out. `ControlTests`, `HardeningTests` and `ManipRig` pump the engine (and ack reliable
  frames) as a live 60 ms loop would; `ManipRig` reacts to each decoded frame inside its pump and keeps pumping
  while the ActionList has work (T8: a terminal current does not promote the next action in the same tick).
  `M8BatchThreeDTests` observes the ActionList step with a queued `OrderAction` instead of the removed hook.
- **Gates.** `fidelity.py --check` clean (427 records). Full suite: **3827 passed, 0 failed, 0 skipped**.
- **Commit.** (this commit)

### Queued (non-blocking, from the batch-2 verifier; fix in a later batch)
- `WatcherEnding` never releases the runner's tags (`Tag`/`OriginalTag`); native erases both at 0x005410FA/
  0x00541104. Tags leak and a later `SetTag` to a used id is refused. No live effect in batch 2.
- The completion union is never initialised by type (L1, 0x0053FECA..0x0053FF9C); `Prep` self-assigns.
- The terminal/timeout result log is missing (L10/L13).
- `NextActionTag` on the global counter makes `BehaviorFrameworkTests`' tag assertion order-dependent.
- `Vision/FaceActions.cs:591` still says the stack has no ActionList.
- Batch-3 nuance: the compound stops later children on a non-ignored child failure (R3); two separate queues do
  not, until the compound is built.

## Batch 3a - the compounds (rows P, S, R)

- **Row verification.** `cozmo-verifier` checked P1-P8, S1-S9, R1-R6: all HOLDS. Two shared mechanisms are on the
  paths but in no row; the verifier read them in the binary and they were built from that reading:
  `ICompoundAction::Reset(bool)` 0x0054EC56 and `ClearActions()` 0x0054EFA4.
- **Built.** `cozmo-stack/src/Cozmo.Robot/Actions/CompoundActions.cs`: `CompoundAction` (P1-P8),
  `CompoundActionSequential` (S1-S9), `CompoundActionParallel` (R1-R6). `BehaviorManager.QueueHeadAndLift` now
  queues one `CompoundActionParallel` at NOW with children {head, lift}, replacing the batch-2 stand-in.
  Tests: `CompoundActionTests.cs` (19 tests).
- **Verified.** `cozmo-verifier` PASS after two blocking fixes: (1) the compound parent `Type` (+0x44) was not
  written (native writes it in `StoreUnionAndDelete` 0x0054F0D8/0x0054F0DA and `SetProxyTag`
  0x0054F23C/0x0054F23E, 0x0054F262/0x0054F264); (2) `CanInterrupt` returned `true` with no source. The fix
  resolved the vtable slot `+0x14` for the compounds and for the head/lift actions (all -> 0x0052B0B2
  `movs r0,#0; bx lr`), so a compound and a head/lift move both refuse Q14 and fall back to QueueNow (Q15).
  The head/lift `CanInterrupt` was a batch-2 defect found here; a new M4ControlTests test pins the Q15 fallback.
- **Gates.** `fidelity.py --check` clean (427 records). Full suite: **3827 passed, 0 failed, 0 skipped**.
- **Commit.** (this commit)

### MISSING (still open)
- The producer of `CompoundActionSequential`'s +0x9C delay; no row says which live function sets a nonzero delay.
- `ICompoundAction`'s `Init`/`CheckIfDone` bodies are unreachable (the compound overrides `UpdateInternal`);
  implemented as `Init()==0` and `CheckIfDone()==State`.
- The concrete compound that sets the completion-union proxy (+0x94/+0x98) and its child tag; P8 settles the
  mechanism, not its caller.
- The S2 derived +0x24 hook's concrete body (S2 itself says UNKNOWN absent subclass).

### Queued (non-blocking, from the batch-3a verifier)
- `CompoundActions.cs`'s file-level `// fidelity: M10-008, M13-028` tag overclaims ownership of the generic base;
  move the tags to the concrete classes or add a base record.
- `AddAction` with ignoreFailure=false erases a keyed predicate; native leaves the map unchanged.
- `DeleteActions`/`ClearActions` clear the completion cache; native clears it only in the destruction tail.

## Remaining batch
3b (replace the stack's async sequences in FlipBlockAction, ChargerActions and DockActions with compounds) and
4 (completion to the game: RobotCompletedAction and ActionWatcher). Records stay IMPLEMENTATION_GAP until a
strong verifier settles them.
