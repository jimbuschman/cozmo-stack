# Claude check: B-M15-12 (the live SevereNeedsComponent and the NotifyCozmoWakeup input)

- **Checked:** 6cd86f8a, e859a87f, 3210923d, 5fe5401b (status 3beed79a; full suite 6,217/6,217) against
  `jobs/B-M15-12.md` and `research/20261025-r-m15-severe-input-claude-check.md` with both addenda. The manager (Opus)
  read the production diff and spot-checked the tests.
- **Verdict: PASS.** No record settles. M15-072, M15-073, M13-017 and M8-006 stay IMPLEMENTATION_GAP.

## Supported

- **Component** (`AIComponent.cs`):
  - Selected is 3 at construction;
  - Set pushes the driving triplet (Energy 0x134/0x133/0x132, Repair 0x140/0x13F/0x13E), then the idle trigger
    (0x137/0x143), then the reaction table (`011111011011101111010`, or all false for Play) with stop = true, and only
    then stores the need; there is no Clear first and no same-value guard;
  - Clear removes driving, then idle, then the reaction lock, then stores 3, including from 3;
  - Update returns at 3, otherwise clears when the selected need is not Critical, and never selects;
  - Wake queries both needs and prefers Repair.

  All of this matches the check's disassembly table.
- **Threading:**
  - Nested Set/Clear inside Wake/Update re-enter `InvokeEngineRequest`, which runs inline on the engine thread or under
    the tick lock (`CozmoEngine.cs:2256`), so there is no deadlock.
  - `NeedsManager.State` is get-only (`Needs.cs:820`), so the bound `IsAtBracket` cannot go stale.
- **AI phase:** `AIComponent.Update` runs the analyzer, the whiteboard no-op, SevereNeeds, then the helper
  (0x00569F38). Without a Freeplay handle the engine keeps a SevereNeeds-only AI hook.
- **Consumers all read the one owner:**
  - StrategyExpressNeedsTransition (0x006136FC);
  - the activity SevereNeedTransition (0x005B4D88 → 0x006134B4 → 0x006136D8, confirmed by the builder);
  - the cliff reader;
  - ManipulationSystem;
  - the drive-off gate, which now pushes its own triplet under its own name when Selected == 3 and has no MISSING;
  - the IBehavior required-severe gate.

  The required-severe gate was re-read here: 0x005BD7E6 reads own+0x74, skips at 3, otherwise `bne` to reject when it
  differs from AI+0x30→+0x14 (0x005BD7F6). The stack changed from a bracket test to equality, which is correct.
- **Callers:**
  - PlayAnimOnNeedsChange sets on any stop reason when (different AND Critical);
  - ExpressNeeds loads both flags (default false), clears on stop when `shouldClearExpressedState` is set, and its
    runnable check matches 0x005EE57A..0x005EE5B0.
- **HashSet retired**, together with its per-bracket removal.
- **Auto-wake (M15-073):** sent once per connection, when Success and `IsReadyDelivered` both hold, through the G2E 166
  path. The guard resets on native robot construction. The host API call is kept.
- **Tests:** the expected triplets, idles, mask and lock name are literals from the check, not from the code. The
  builder shows 27 of the new and changed cases failing on the pre-job code. Production reach for the wake API,
  auto-wake, the AI clear and drive-off.

## Queued (not blocking)

- **Q1:** the reaction-lock recipient exists only while a Freeplay stack exists. Native BehaviorManager lives with the
  robot. A wake or Set before Freeplay is created stores Selected and pushes the animations, but skips
  DisableReactionsWithLock. This is a visible MISSING (0x005731A0 / 0x00572D56), and the lock is not applied later when
  Freeplay binds. It follows the existing host-ownership gap (M15-027/M15-045). Fix when the behaviour manager gets a
  robot-lifetime owner.
- **Q2:** the get-out action (0x0054504C / 0x005451C8) is still absent. It is a visible MISSING, as the job directed.
