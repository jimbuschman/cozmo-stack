# M1 + M2 acceptance: what stands in the way (manager, 2026-10-16)

- **Target:** M1-transport and M2-protocol reach ACCEPTED, under "Done for a layer" in `PROJECT_STATE.md`. That
  means three things:
  - every record is EXACT_SOURCE, a confirmed policy record, or HARDWARE_ONLY settled by a run;
  - the operator has run the layer's hardware script;
  - the review state is ACCEPTED.
- **Sources:** the manifest, `jobs/STATUS.md`, the job and check files named below.
- **Status:** 78 records. 54 are EXACT_SOURCE; the remaining 24 are listed here.

## 1. Records still open

| Record | Status | What's left | Where the work is |
| --- | --- | --- | --- |
| M1-054 | IMPLEMENTATION_GAP | CozmoEngine::Init order before RobotManager::Init | B-M1-2 item 1. Queued; the builder reaches it right after B-M15-7. |
| M1-055 | IMPLEMENTATION_GAP | BroadcastAvailableAnimations, then SetEngineState(3); is the first broadcast empty? | B-M1-2 item 2 |
| M2-018 | IMPLEMENTATION_GAP | Built through B-M3M4-18/24/25/29 (checks: 18 FAIL on scope only, 24 PASS, 25 FAIL → fixed in 29 PASS). The LoadNonConfigData-before-AddRobot ordering is open, under M2-020. | A **settling pass**, after M2-020's ordering is split or closed |
| M2-019 | IMPLEMENTATION_GAP | D2/D3 built (B-M3M4-18); the check confirmed both bodies. The other Impl readers belong to M7-027. | A **settling pass** |
| M2-020 | IMPLEMENTATION_GAP | Dispatch residue built (B-M3M4-25/29). Open: the 35 native owner releases; the identity of ~Robot's r5 at 0x0051120E; unbuilt EC/B0 registrations, whose handler bodies sit in higher layers. | **Split:** keep the dispatch mechanism in M2 and settle it; move the owner and handler parts to their layers (the plan's cross-layer rule) |
| M2-022 | RECOVERABLE_GAP | Local E2G subscribers to AnimationEvent (tag 0x60), and whether C5 can dispatch nested inside ActionList::Clear | One research request; or a split, if the recipients are M5/M8 code |
| M2-023 | IMPLEMENTATION_GAP | Handler bodies (C4, DA, CA, CB, D5, C5). DA/C5 built in B-M12-3, which isn't verified yet. C5 stops at the unbuilt TriggerLiftSafeAnimationAction (0x00544710), which is M8/M12 work. The rest is in B-M2M15-1. | **Unblock B-M2M15-1** (see section 2). Verify B-M12-3. **Split** C5's action-queue part into M12/M8. |

## 2. Blocked or unverified jobs that touch M1/M2

| Job | State | What's needed |
| --- | --- | --- |
| B-M2M15-1 | BLOCKED: the push gate refused it, and the implementation ed67f3ad is parked in the builder clone (gate test race) | Tell the builder to restore it and put the racing tests in the serial collection; or have the manager build it |
| B-M12-3 | DONE, not verified | An Opus verification pass |
| B-M3M4-27 | DONE (cleanups), not verified | A light check, or fold it into the settling pass |

## 3. Policy records to confirm

These 14 records need a manager check that each is still a legitimate policy or a scope-rule departure. They are
not new decisions:
- M1-013, M1-014, M1-022, M1-034, M1-036, M1-037, M1-038, M1-039, M1-040, M1-042, M1-049, M1-051, M1-052;
- M2-017, M2-021;
- M1-043 (EQUIVALENT).

Most are named in AGENTS.md's approved departures or the app-boundary rule.

## 4. Hardware

- **M1-033** is settled for small packed frames (bundle `20261009-112625-M1-PACKED-ZERO`). Maximum-size frames, other
  orders and sequence wraparound were not exercised. Their status is decided at acceptance: either run them, or record
  them as not exercised in the acceptance note.
- **The layer's acceptance run:** a single M1+M2 hardware script, run by the operator after section 1 closes. It covers
  connect, the protocol exchange and disconnect on the current main.

## Update 2026-10-16 (later)

- **M2-020 and M2-023 are split.** Each owner record was already there, so no new records were needed:
  - M2-023's handler bodies went to M4-016, M13-022, M8-020, M8-015 and M12-017;
  - M2-020's per-owner wiring went to M8-020, M12-017, M7-027 and M4-027.
- **M2 work that remains in M2-020:**
  - the TracePrinter B0 (Trace) and EC (WifiFlashID) registrations and bodies, which are unbuilt;
  - settling the dispatch mechanism.
- **The ~Robot r5 identity is settled** by `research/20261013-small-reads-2-check.md` (BlockTapFilterComponent).
- **In progress:**
  - B-M2M15-2 is queued after B-M1-2;
  - an Opus verifier is checking B-M12-3;
  - an Opus extractor is reading M2-022.

- **B-M12-3's check FAILED** (`research/20261016-b-m12-3-check.md`). The fixes are queued as B-M12-7, after
  B-M2M15-2:
  - a retirement race in the dock handlers;
  - C5 not queueing its animation action;
  - no production producer for 0x4C.

- **M2-022 is read** (`research/20261016-m2-022-answer.md`): no local 0x60 subscriber, and no nested dispatch.
  It moves to IMPLEMENTATION_GAP; next comes the comparison with the stack, then the settling pass.

## Order of work

### Manager handoff update (2026-10-17)

- Codex temporarily performs the manager/independent verifier role by operator instruction.
- B-M3M4-27's outstanding cleanup source review is PASS; see `../research/20261017-b-m3m4-27-check.md`
  for the evidence, exact scope and runtime validation. Reach-label cleanup is queued; no records are reclassified.
- The B-M2M15-2 preflight notes that PoseTreeVerifyBuilderTests already uses the serial collection. Its failed
  gate still requires reproduction and a root cause; do not assume repeating that change fixes it.
- Earlier table entries are historical where superseded by the later updates: B-M12-3 has failed verification,
  its corrections are B-M12-7, M2-020/023 were split, and M2-022's source question was answered.

1. B-M1-2: already next in the builder queue.
2. Unblock B-M2M15-1, and verify B-M12-3.
3. Splits for M2-020 and M2-023, and the decision on M2-022: manager work, using cited rows only.
4. A settling pass on M2-018, 019, 020 and 023 (Opus), plus the policy confirmation.
5. The operator's M1+M2 hardware run, then ACCEPTED.
