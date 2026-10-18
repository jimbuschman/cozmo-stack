# M1 + M2 acceptance: what stands in the way (manager, 2026-10-16)

- **Target:** M1-transport and M2-protocol reach ACCEPTED, under "Done for a layer" in `PROJECT_STATE.md`. That
  means three things:
  - every record is EXACT_SOURCE, a confirmed policy record, or HARDWARE_ONLY settled by a run;
  - the operator has run the layer's hardware script;
  - the review state is ACCEPTED.
- **Sources:** the manifest, `jobs/STATUS.md`, the job and check files named below.
- **Status:** 78 records. 54 are EXACT_SOURCE; the remaining 24 are listed here.

# Revised plan under "What we reproduce" (operator, 2026-10-17)

This section is current. The assessment below it (same date) is its evidence; where they disagree, this section wins.
AGENTS.md "What we reproduce" now governs: observable behaviour (robot traffic, robot behaviour, C# API delivery and
readiness, downstream state) from source; implementation free where the result is the same; logs best effort;
exclusions are recorded decisions; narrow checks before full extractions. Acceptance depends on the resulting
production path and its verification, not on the length of this list.

## Decisions recorded (on the records' `unresolved`)

- **M1-055:** not supported: the Init-time animation broadcast (operator). Its delivery is unresolved, not disproved.
  Correction (same day): it is not established that no internal effect is lost. The sends pass through
  UiMessageHandler::Broadcast, which also runs a local dispatch (0x006625FA). OV126 routine 6 checks for a required
  internal consumer of this early EndOfMessage; any required effect is kept without the public event. Not supported: the raw
  CLAD serializer for AnimationAvailable (manager; the C# API is typed). **Required:** a reliable animation list after
  loading, with a clear completion indication.
- **M1-056:** the C# API reports loading, ready and failed, with meaningful progress where readily available
  (operator). The original's percentage cadence is not required. **Required from source (narrow):** how the 1 → 2 gate
  is met with no UI device; what SetEngineState changes (OV125, trimmed).
- **M1-054:** the startup order is required where it changes 1–4, and production must run it. Host-config failures are
  handled as stack config errors (manager). UiMessageHandler::Init, InitInternal and the manager MessageHandler Init
  get narrow checks (OV126). Each stays required until answered.
- **M2-020:** the robot's Trace (0xB0) is decoded and logged as diagnostics (manager). WifiFlashID (0xEC) gets a
  narrow check (OV126).

## What stays, what goes

| Item | Supported function it serves | Now |
| --- | --- | --- |
| Verify B-M1-2 | Startup order, the animation list | Stays (Opus, new criteria) |
| Verify B-M15-11 | Lab startup state before the hardware run | Stays |
| M1-055 loading gate (OV124 rows 8, 12) and list after loading | API: animation list, readiness | Stays (build) |
| M1-056 1 → 2 gate, SetEngineState | Readiness; loading starts at all | Stays (OV125 narrow, then build) |
| Loading/ready/failed + progress | API readiness | Stays (build; stack design for "failed") |
| UiMessageHandler::Init, InitInternal, the MessageHandler Init, SendSupportInfo, WifiFlashID | Unknown | Narrow check (OV126), not full extraction |
| Internal consumers of the Init-time EndOfMessage | Possibly readiness or downstream state | Narrow check (OV126 routine 6); a required effect is kept without the public event |
| Trace (0xB0) | Diagnostics | Decode and log (build) |
| B-M2M15-2 | Robot-message dispatch with no robot (B9) | Released; build, then verify |
| M2 settling pass, policy confirmation | Dispatch order; confirmed policies | Stays (Opus) |
| M1+M2 hardware script and run | Acceptance | Stays (script by the manager; run by the operator) |
| Init-time broadcast; raw AnimationAvailable bytes | — | Gone (recorded exclusions) |
| Full extraction of the five routines | — | Gone (narrow checks instead) |
| OV125 old Q3 (no-UI-device delivery) | — | Withdrawn |
| Malformed-config branches, the "CozomEngine.Init" typo as a requirement | — | Gone (sensible stack errors; logs best effort) |
| B-LOG-1 | Logs | Cleanup, held off the path |

The higher-layer leaves of M1-054 (LoadRobotConfigs pieces, NeedsManager::Init, the animation-container population)
stay required with their owners; the new standard does not dismiss them.

## Sequence

**Update (2026-10-17, night):** B-M1-4 (3c7b7969) and B-DELIV-1 (33b80553) both Opus FAIL
(`research/20261017-b-m1-4-check.md`, `research/20261017-b-deliv-1-check.md`). B-M1-4: startup with the shipped
assets never reaches ready (a >1-name MISSING), so no hardware tool can connect; the public API cannot observe 0→1.
B-DELIV-1: other game broadcasts (RobotStopped, cube reports, NV results) still run inline, so host order changed; one
production label rests on a second-thread Tick. Operator decision: animation names in stack load order (AGENTS.md,
M11-047 extension). All fixes in B-M1-5 (open). Until it lands, main cannot drive a robot.

**Update (2026-10-17, evening):** B-M1-3 PASS (fix review), B-M2M15-2 PASS with one finding raised to blocking
(B-DELIV-1). OV125 and OV126 adopted (`research/overnight/20261017-ov125-ov126-check.md`): readiness needs StartEngine
and the engine's own UI connection, which the stack supplies as host inputs (M1-058); game-socket plumbing not supported
(M1-059). M1 has no live research gap left. Next build: B-M1-4 (readiness through the real startup), then B-DELIV-1.
Operator decisions (same evening): SupportInfo not supported (intentional API exclusion); game-posted audio supported,
split so M1 owns the startup connection and the public entry, and M6 owns execution and fidelity. M1+M2 acceptance
needs a request to reach the M6-023 entry, not the sound system finished. Then the M2 settling pass, policy
confirmation and the hardware script.

**Update (2026-10-17, later):** B-M15-11 PASS with queued items (`research/20261017-b-m15-11-check.md`). B-M1-2 FAIL
(`research/20261017-b-m1-2-check.md`): a supplied engine config left the engine silently dead; an unsupported public
carry setter; the Init-time broadcast still wired to public delivery. Fixes in B-M1-3, after B-M2M15-2. The verifier
also confirmed that production today runs the old startup and delivers no animation list after loading; that is the
next M1 job's work (after OV125/OV126).

1. Now: Opus verifiers on B-M1-2 and B-M15-11. The builder takes B-M2M15-2. Research takes OV125 (narrow), then OV126.
2. The manager adopts OV125 and OV126, then writes one builder job: the startup order where it changes 1–4, the
   1 → 2 gate, the loading gate, loading/ready/failed and progress, Trace logging, and whatever OV126 marks REQUIRED.
3. An Opus check of that job and of B-M2M15-2.
4. The M2 settling pass and the policy confirmation.
5. The manager writes the M1+M2 hardware script. It covers connect, startup to ready (with the loading states), the
   animation list, robot-message dispatch and disconnect. Then the operator runs it on the final main.
6. Settle the records and set ACCEPTED, if the production path runs as recorded and verification passed.

[U] Still unknown: what OV125 and OV126 find (each REQUIRED item adds build work); why B-M2M15-1's gate failed;
whether LoadRobotConfigs loads anything no record owns.

# Current assessment (manager, 2026-10-17)

This section is the evidence for the revised plan above. Everything from "1. Records still open" down is the 2026-10-16 assessment and its updates,
kept as history; where it disagrees with this section, this section wins.

- **Examined:** `main` at 9457a424. Uncommitted: `relmap.pkl` (untracked, unrelated). No code, classification,
  requirement or job scope was changed by this pass.
- **Builder:** B-M1-2 is published (c18cde5a). B-M15-11 is claimed and running. Every later builder row is HELD in
  `jobs/QUEUE.md` (9457a424); none reads `open`. The research lane is not held.
- **Labels:** **[F]** a fact checked against the current files; **[E]** an estimate; **[U]** unknown.

## A. All subsystems (from `FIDELITY_PROGRESS.md`, generated)

[F] All 15 engine subsystems are INVENTORY_APPROVED and none is ACCEPTED. 692 records: 184 EXACT_SOURCE, 404
IMPLEMENTATION_GAP, 20 RECOVERABLE_GAP. Live-path open records per subsystem: M1 3, M2 5, M3 4, M4 17, M5 23,
M6 40, M7 24, M8 26, M9 38, M10 14, M11 79, M12 38, M13 33, M14 11, M15 64. Hardware bundles exist for M1 (M1-LINK
2026-09-24, M1-PACKED-ZERO 2026-10-09), M3/M4 (M3M4-HW 2026-10-12) and the control check (2026-09-25). The other
subsystems were not assessed in detail here, and this documentation review does not claim to have found every defect.

## B. M1 + M2: what blocks acceptance now

[F] 80 records (M1 57, M2 23). Open: M1-054, M1-055 (IMPLEMENTATION_GAP), M1-056 (RECOVERABLE_GAP), M2-018, M2-019,
M2-020, M2-022, M2-023 (IMPLEMENTATION_GAP). Requirement for every row: "Done for a layer", first bullet (each record
EXACT_SOURCE, a confirmed policy, or settled HARDWARE_ONLY).

| # | Record | Evidence now | Still missing | Covered by | Closes when | Kind |
| --- | --- | --- | --- | --- | --- | --- |
| B1 | M1-054 engine Init | batch-check rows; B-M1-2 built the gates, logs and call order (c18cde5a) | [F] Not verified. [F] Native Init stops at MISSING leaves: UiMessageHandler::Init 0x006601B0, InitInternal 0x004EDC6C, full LoadRobotConfigs 0x00522280, NeedsManager::Init 0x004ECA0A, manager MessageHandler vslot 0x0052E76A, the Init tail after 0x004ECA1A. [F] Production with no `EngineConfigJson` still runs the pre-existing host bootstrap (`CozmoEngine.cs`:1950-1965), which is not the native order. | Verify: none yet. Leaves: none (see B6) | The leaves are split or built, the production startup runs the native order, and an Opus check passes | Open-ended until B6 is settled |
| B2 | M1-055 broadcast and SetEngineState(3) | OV124 adopted (20261017-ov124-check.md); B-M1-2 built the broadcast | [F] Not verified. [F] OV124 not yet folded into the record (pending since B-M1-2 is now published). [F] MISSING: the container population producer (0x00588190, M5-001's loader), the loading-state entry 0x004ED5F0 / DoNonConfigDataLoading 0x00523570 (rows exist in OV124 rows 8 and 12), the SetEngineState body 0x004ECCBC, and the AnimationAvailable raw serializer 0x0071624E. | Verify: none. SetEngineState: OV125 Q1. Loader gate: rows exist, no job | Verified; loading gate built from OV124 rows; SetEngineState built from OV125; serializer decided (B7) | Clear once OV125 lands |
| B3 | M1-056 states 1 and 2 | OV124 check; RECOVERABLE_GAP | [F] Bodies unread: HasDesiredNumUiDevices, SendSupportInfo, SetEngineState, EngineLoadingDataStatus, progress writers, Broadcast with no UI device | OV125 (queued, research lane) | OV125 answered, checked, adopted; then built and verified | Clear |
| B4 | M2-018, M2-019 | B-M3M4-18/24/25/29 checks (24 and 29 PASS) | [F] No settling pass. [E] M2-018's "LoadNonConfigData before AddRobot" ordering looks answered by M2-021's policy; confirm in the settling pass | Settling pass: no job | An Opus settling pass moves them to EXACT_SOURCE | Clear |
| B5 | M2-020 residue | dispatch-residue check; B-M3M4-29 | [F] TracePrinter 0xB0 Trace and 0xEC WifiFlashID registrations and bodies unbuilt. [F] A search of `research/` found no checked body rows for them. | None | Rows extracted, built, verified; then the settling pass | **Missing coverage** (research and job) |
| B6 | M1-054/055 leaves owned by higher layers | the cross-layer rule | [F] LoadRobotConfigs body pieces have owners (M8-022, M11-059, M11-082, M15-049); NeedsManager::Init (M15-001/014/016/050); the animation container population (M5-001). [U] Whether LoadRobotConfigs loads anything no record owns. [F] UiMessageHandler::Init, InitInternal 0x004EDC6C and the MessageHandler vslot 0x0052E76A have no owner but M1-054 and no checked rows. | None | The split is recorded (M1 keeps the call order and gates); the three engine-level bodies are extracted | **Missing coverage**: a split pass (manager) and research for the three bodies |
| B7 | AnimationAvailable raw serializer 0x0071624E | B-M1-2 report group 8 | [U] Whether the stack needs raw CLAD bytes for engine-to-game messages, or typed C# delivery is the whole interface under the app-boundary rule | None | A manager decision citing the scope rule, recorded on M1-055 | Decision, not research |
| B8 | M2-022 | M2-022 answer (no local 0x60 subscriber; no nested dispatch) | [F] Comparison with the stack not done | Settling pass: no job | The stack has no local 0x60 subscriber and no nested path; settled | Clear |
| B9 | M2-023 dispatch remainder | split 2026-10-16 | [F] The bodies went to owners. M2 keeps mid-emit safety, ConnectToRobot registration and ProcessMessages with no robot. [F] The no-robot drop (0x0069D8BC..0x0069D8D8) is in the parked B-M2M15-1 implementation (`CozmoEngine.cs`:493 there), which the push gate refused (PoseTreeVerifyBuilderTests, cause unknown). | B-M2M15-2 (HELD) | B-M2M15-2 publishes and is verified; then the settling pass | Clear, but the gate failure is unexplained |
| B10 | Policy confirmation | 2026-10-16 list, plus M1-057 | [F] Not done: M1-013, -014, -022, -034, -036, -037, -038, -039, -040, -042, -043, -049, -051, -052, -057; M2-017, M2-021 | Manager task, no job | Each confirmed against AGENTS.md | Clear |
| B11 | Hardware | M1-LINK (2026-09-24), M1-PACKED-ZERO (2026-10-09) | [F] No M1+M2 acceptance script exists. `LinkCheck.cs` (M1-LINK) was last changed 2026-09-24 and names 15 M1 records and no M2 record; it does not cover engine startup, the animation broadcast, states 1-2 or robot-message dispatch. [F] M1-033 still lacks maximum-size frames, other orders and sequence wraparound. | None | The manager writes the script (maintenance, separate from the run); the operator runs it on final main | **Missing coverage**; content criteria are undocumented beyond "connect, protocol exchange, disconnect" |

## C. Implementation, verification, acceptance

| Job | Builder says | Independent check | Counts toward M1+M2? |
| --- | --- | --- | --- |
| B-M1-2 (M1-054, M1-055, M12-046) | DONE, 12 MISSING | **none yet** | Yes (M1 part) |
| B-M3M4-18 / 24 / 25 / 29 | DONE | 18 FAIL (scope), 24 PASS, 25 FAIL, then 29 PASS | Yes: M2-018/019/020 are ready for a settling pass |
| B-M3M4-27 | DONE | PASS (Codex, 2026-10-17) | Cleanup only |
| B-M2M15-1 / -2 | BLOCKED (gate), parked; -2 HELD | none | Yes: carries B9's no-robot drop |
| B-M12-3 / B-M12-7 | DONE / HELD | 12-3 FAIL | **No longer**: the DA/C5 bodies moved to M12-017 (split 2026-10-16). It stays required for M12. |
| B-M15-7 / B-M15-11 | DONE / CLAIMED | 15-7 FAIL (20261017-b-m15-7-check.md) | See D2 |

## D. Splits and couplings

- **D1. B-M2M15-2 carries an M2 piece (B9) behind M15/M8 work.** [F] The no-robot drop does not depend on the action
  bodies. This is scheduling coupling, not a path dependency. Options: run B-M2M15-2 first when the hold lifts, or
  split the M2 piece out if the gate refuses it again. [E] Running it first is cheaper; the gate failure's cause is [U].
- **D2. B-M15-11 (the startup regression).** [F] InitExperiments (0x004EC9D6) is a step of M1-054's path, but its body
  belongs to M15-049/M15-052. By the cross-layer rule, M1 needs only the call at the right place, which the B-M15-7 check
  confirmed. So B-M15-11 is not an M1+M2 acceptance gate. [E] It still has to land before the M1+M2 hardware run,
  because the startup log the run records would otherwise show the lab disabled. It is already claimed, so this costs
  nothing.
- **D3. M1-054/055's higher-layer leaves (B6).** [F] They are genuine steps of the startup path. The cross-layer rule
  permits keeping the call order in M1 and moving each body to its owner. That removes the M4/M5/M8/M11/M15 coupling, but
  not the three engine-level bodies, which are M1's own.
- **D4. B-M12-7** no longer blocks M2 (C). It stays required for M12.

## E. Recommended next action, then the sequence

**Next (both read-only, can run now, in parallel with B-M15-11):**
1. An Opus verifier on B-M1-2 (c18cde5a). Resolves: verification of M1-054/055's built units. Leaves: the leaves.
2. A manager split pass for M1-054/M1-055 (D3), and the B7 decision, and folding OV124 into M1-055. Resolves: what
   M1 itself still owes. Leaves: the three engine-level bodies.

**Then:**
3. Release B-M2M15-2 only (D1). Resolves: B9's build. Leaves: its verification.
4. Research requests (none exist yet): UiMessageHandler::Init 0x006601B0, InitInternal 0x004EDC6C, the MessageHandler
   vslot 0x0052E76A; the TracePrinter 0xB0/0xEC bodies. Alongside OV125. [E] Three to four requests; the bodies are
   unread, so this is evidence that new research is required, not a documentation gap.
5. A builder job for the M1 leaves (M1-055's loading gate from OV124 rows 8 and 12, SetEngineState and M1-056 from
   OV125, the engine-level bodies, B0/EC), then its Opus check.
6. An Opus settling pass over M2-018, 019, 020, 022 and 023, plus the policy confirmation (B10).
7. The manager writes the M1+M2 acceptance script (B11). Separately, the operator runs it on the final main, with
   B-M15-11 landed.
8. Settle M1-054/055/056, and set ACCEPTED.

**[U] Unknowns that could lengthen this:** whether LoadRobotConfigs loads anything no record owns; why
PoseTreeVerifyBuilderTests failed in B-M2M15-1's gate; what OV125 finds in UiMessageHandler's no-device path (if the
Init-time broadcast is queued, M1-055's delivery semantics change); whether the robot exercises B0/EC at all on the
operator's firmware.

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
