# Job B-ACTIONS: build the engine's ActionList, ActionQueue and compound actions

- **Agent:** a Sonnet worker in Claude Code, as manager of this job.
- **Type:** build.
- **Rules:** `CHECKLIST.md` applies, and **this job never settles a record** (section 6). Records stay
  IMPLEMENTATION_GAP, with `unresolved` starting "built, awaiting strong verification:".

## Why

The Opus verifications of 2026-10-02/03 found that the missing ActionList blocks the most records:
- M4-003's QueueNow;
- M4-016's first-Update timer;
- M7-020's ActionWatcher and RobotCompletedAction;
- M8-012's action paths;
- M8-008's calibration, which bypasses the runner;
- M13's charger actions and compound sequences;
- the dock and flip compounds.

Today the stack runs actions as host `Task`s, with timers and waits the engine doesn't have.

## Rows

`re-analysis/research/20261004-actionlist-extraction.md` (Codex). The manager re-checked three central rows in the
binary and all three held:
- Q6: QueueNow deletes the running action without Cancel (0x0053E2D6);
- Q7: the empty-pending branch cancels (0x0053E350) before Delete (0x0053E35E), then QueueAtEnd;
- S5: the sequence runs its callbacks (0x0054F80C) before the ignore-failure predicate (0x0054F81A).

The other rows are unchecked. Have `cozmo-verifier` check every row a batch builds on before building it. A row that
fails goes back to `cozmo-extractor`.

## Batches, with their production paths

1. **The queue and the tick** (rows A, T, Q, C).
   - **Robot::Update calls ActionList::Update** at robot+0x250 (0x005140BC): after the BehaviorManager, and before
     the AnimationStreamer and NVStorage (T1–T9).
   - **The queues tick in signed key order** (T5). The first nonzero result is kept, and the watcher updates after
     all of them.
   - **Every QueueActionPosition is implemented:** NOW, NOW_AND_CLEAR_REMAINING, NOW_AND_RESUME, NEXT, AT_END and
     IN_PARALLEL (Q4–Q15). So is Cancel by id, tag and type (C).
   - **C# host:** a new `ActionList` owned by the engine robot and ticked from `CozmoEngine`'s Robot update, in
     place of the host-`Task` action runs.
2. **The IActionRunner/IAction lifecycle** (rows L1–L17).
   - Init on the first Update, with the timer started then (L7).
   - The 0x03000018 timeout, checked before Init and CheckIfDone.
   - Track lock take and release, the +0x56 suppression, and destruction as stop-before-unlock.
   - Move the head, lift and dock actions onto it. `Motion.RunAsync` becomes queueing plus completion.
3. **The compounds** (rows P, S, R).
   - CompoundActionSequential: the one-child-per-tick rule with the immediate next child, delay, retry, and the
     callbacks-before-predicate order.
   - CompoundActionParallel.
   - Replace the stack's async sequences in FlipBlockAction, ChargerActions and DockActions with these, where the
     engine builds a compound.
4. **Completion to the game** (rows D, W).
   - RobotCompletedAction: its fields, order and the game send.
   - ActionWatcher's ParentActionUpdating and the ended callbacks that M7-020 and MoodManager::HandleActionEnded need.

## Records it builds on

M4-003, M4-016, M7-020, M8-008 and M8-012. Plus the charger records M13-008/012/013 and the flip and dock compounds
(M13-028, M12-017), as far as their action path goes. Each record's `unresolved` names what remains.

## Gates

Before each commit:
- `cozmo-verifier` gives PASS on the diff, against the checked rows and the binary;
- `python re-analysis/tools/fidelity.py --check` passes;
- the full suite passes once.

Log each batch in `status/B-ACTIONS.md`. Commit each verified batch and carry on. Stop only when DONE or blocked.
