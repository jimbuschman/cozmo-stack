# DEV-TURN-IN-PLACE-TEST-1: Claude check (manager, 2026-10-30)

**Verdict: FIX REQUIRED. Not merged.** Handoff e37e847d (full gate on 2cb53ca1, 7003 PASS). **Nothing settled.**

## Blocking: actions are constructed in the wrong order, so the delivered tags are wrong

The original constructs TurnInPlaceAction (0x005CAA32), then WaitAction (0x005CAA8C), then CompoundActionSequential
(0x005CAA9A), and only then adds the children (0x005CAAB2, 0x005CAACC). Each constructor draws a tag from the shared
counter (IActionRunner 0x0053FDB0). So the original gives turn T, wait T+1, compound T+2.

`NativeDevTurnInPlaceTestBehavior.cs:76-79` builds turn, compound, then wait, giving turn T, compound T+1, wait T+2. The
compound's tag and its sub-action tags reach the C# API (`ActionWatcher.BuildCompletedAction`,
`ActionCompletionReport.cs:21`), so the payload differs: original T+2 [T, T+1], stack T+1 [T, T+2].

It is reachable: `BehaviorManager::SwitchToBehaviorBase` logs a non-runnable behaviour and starts it anyway
(0x005A1E76..94), and the stack reproduces that, so public `Manager.StartAsync("DevTurnInPlaceTest", now)` runs it.

**Fix:** construct the wait before the compound. Add a test asserting the delivered idTag and subActionTags order, and
one production-entry test through `Manager.StartAsync`. Correct the line 40 message and the handoff: the manager gate
does not gate that entry; only the chooser route does (0x0060AD92..96). No native caller is recovered.

## Checked and agreeing (verifier, in disassembly)

- **Config and gates:** ctor defaults 0x005CA57A..7C; parse order 0x005CA598..6FE; the gates at +0x20/+0x24/+0x28 return 0.
- **Locks and Init:** lock table 0xC60D40 (21 pairs); Init 0x005CA918..946.
- **Generate:** factor 0x3C8EFA35; relative turn; the setter order; both AddAction calls; the wait on the final test too.
- **Callback 0x005CAC54..A6:** u8 wrap, remainder on the wrapped byte, index reset only on loop, no result branch.
- **Contract correction:** confirmed. 0x005CA902..916 and Reset 0x005CA94C zero only the index and the counters; the
  vector is kept.
- **Empty tests:** 0x005CA984 is a null dereference, so M1-034 applies.
- **nRuns 0:** 0x005CAC60 calls imported `__aeabi_uidivmod` with no zero check. Its implementation is in the phone's libc,
  which isn't shipped, so UNKNOWN is honest. The stack increments first, logs a visible MISSING, schedules nothing and
  ends. Shipped nRuns is 1.

No uncaught NativeActionUnavailable through a callback, no re-selection, and no regression from the additive
NativeVisionRunners overload.

## Queued

1. Add the Info logs at 0x005CA9F4 and 0x005CAC2E.
2. Add tests for the empty-tests skip and the nRuns 0 branch.
3. The lifecycle record goes to the operator. Its `unresolved` must name M1-034 and 0x005CA984. Inventory row 950 still
   says "clears vector" (manager, with the record work).

## FIX-DRIVE-IN-DESPERATION-ENERGY-1 (manager, 2026-10-30): PASS, merge pending

a3b2cc1d. I read the production diff. DriveToCube catches only NativeActionUnavailable from the query, logs a visible
M12-036 MISSING on every occurrence, and continues to LookAtCube with no invented result. The search MISSING now logs
every occurrence and says the motions are skipped. The tests cover a located charger, a later completion subscriber
and the deletion tag. Nothing settled.
