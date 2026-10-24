# Project state

Read first in every session. The manager keeps this file current; the process it follows is the Process section of `AGENTS.md`.

## The plan (operator, 2026-10-05): one layer at a time

**Why:** five workers in parallel produced merge conflicts, a long checking queue and no finish line. Nothing has been
ACCEPTED yet.

**The order**, bottom-up. Each layer is finished all the way to ACCEPTED before the next starts:
1. M1 + M2: connecting and protocol.
2. M3 + M4: devices and motor control.
3. M5: animation and the face.
4. M10–M15 and M7/M8: vision, cubes, navigation, behaviours.
5. The sound layers, M6 + M9, **last** (operator, 2026-10-07). Sound already plays, approximately, through the direct
   decoder. Even under ADP-1 its decision rows run to about 140 obligations (queue 5, Q14), so the robot's behaviour
   layers are finished first.

**Done for a layer means** (revised 2026-10-17 under AGENTS.md "What we reproduce"):
- every live-path record is reproduced (its observable behaviour verified against source, EXACT_SOURCE or a confirmed
  EQUIVALENT_IMPLEMENTATION), not supported (a recorded decision naming who decided and why), or HARDWARE_ONLY settled
  by a robot run; every other record is a confirmed policy;
- the production path the layer serves actually runs that way (fewer listed blockers is not acceptance);
- the layer's hardware script has been run by the operator;
- the review state is ACCEPTED.

**The cross-layer rule.** A lower-layer record whose path runs into a higher layer is split. The lower-layer part is
finished and accepted now. The higher-layer part becomes a record in that higher layer, with its own citation, and is
finished when that layer's turn comes. Examples: M1-044 destroys the behaviour layer; M1-045's go-to-sleep runs on the
ActionList.

**Two lanes at a time:**
- **one builder** (DeepSeek, or a Sonnet when the allowance allows);
- **one checker:** Codex finds defects, then a single Opus pass settles the layer.

Work already in flight (B-ACTIONS, B-FACE, B-M6b-4, R-FIX3 and the parked branches) is finished or parked as it
stands. Nothing new starts outside the current layer.

**Audio (operator, 2026-10-05):** ADP-1 in AGENTS.md. Exact decisions, equivalent per-sample DSP, with thresholds measured against the emulator (`jobs/B-ADP-HARNESS.md`). No bulk reclassification.

## Where things stand (2026-10-16)

- **M3M4ReviewFixTests connection stall: root cause (manager, 2026-10-24).** Test-harness race, not a stack defect. RCM::Connect sets state 1 at once, but the transport posts its ConnectionRequest (and creates the connection) on its executor; a ConnectionResponse injected first is dropped as "unconnected source" and never retried, so no timeout was long enough. Five classes (EngineLatencyBuilder, M3M4CallbackBuilder, M3M4ReviewFix, M3M4SettlingBuilder, M3M4WorldOwnership) now also wait for `Transport.Connection`, as four others already did. B-M13-27 (`B-M13-27-wip` 88c46297) hit this race in its full suite.
- **B-M13-27 merged (manager, 2026-10-24).** SyncFromWorld's all-objects legacy mirror is gone; the native object add owns cubes and the charger. Claude check PASS (`research/20261024-b-m13-27-claude-check.md`). No record settled. Queued: Q1, remove the now-unwritten types 6/7/10 legacy ray fallback; Q2, a host-constant unit test; Q3, CORE-007 grades on legacy `Regions` the tree writers never fill (misleading hardware verdict; fix before any map robot check); Q4, no ReportMissing for the type-7 producer.
- **Map writers on the native tree (manager, 2026-10-24).** B-M13-19 (tree insert geometry and ray query), B-M13-20 (robot-pose writes; fix-up), B-M13-21 (overhead-edge writes), B-M13-22 (charger stored pre-action elements; fix-up): each Claude check PASS (`research/20261023-b-m13-19-claude-check.md`, `20261023-b-m13-20-21-22-claude-check.md`). HasCollisionRayWithTypes keeps a legacy-region fallback while SyncFromWorld still writes legacy regions. No record settled. Open: the border processor, content-3 payload, charger world constructor and SevereNeeds writer MISSINGs. A drive-off robot re-run is due to confirm nothing regressed.
- **M13-017 shared pieces built (manager, 2026-10-23).** B-M13-15 (configuration gate/cache/notify), B-M13-18 (notify to both audio clients), B-M13-16 (charger map add/cache/remove), B-M13-17 (SetPose pre-action cache clear): each Claude check PASS (`research/20261023-b-m13-1{5,6,7,8}-claude-check.md`). No record settled. Left on the drive-off path: the M15 SevereNeeds writer; visible MISSINGs in the native map tree (overlap adapter, border processor, tree-to-ray join for CanDriveIdealDistanceForward), the configuration-container producer and the charger pre-action element adapter. Builder lane has no open job.
- **Drive-off robot run 1 PASSES (manager, 2026-10-23; `acceptance/hardware/20261023-074032-M13-DRIVEOFF-HW-b8a098`).** All five graded checks MATCH: 50,50 parked; contacts off at 5.2 mm; 400 only after the 51.1 mm state; one platform exit after the contacts (90.8 mm, observation); behaviour ended off the platform. Driving animations a known gap. The run-3 false cliff stop is gone on hardware. No record settles; M13-017/M13-040 stay IMPLEMENTATION_GAP pending B-M13-15..18 (Codex builder, in order 15, 18, 16, 17) and the M15 SevereNeeds writer.
- **Drive-off robot check ready (manager, 2026-10-23).** `m13-driveoff-hw` (guide `acceptance/hardware/scripts/m13-driveoff-hw-1.md`): Cozmo starts on the charger, the shipped DriveOffCharger drives him off; graded on the 0x54 threshold wire (50,50 parked; nothing before >50 mm of recomputed travel; then two 400s), the single platform exit after the contacts drop, and the behaviour end. Verifier FAIL (grading window) fixed and re-verified PASS (`research/20261023-m13-driveoff-hw-claude-check.md`); 15/15 harness tests. **Operator runs it next.** Codex queued 2026-10-23: B-M13-15, B-M13-16, B-M13-17 (M13-017 shared pieces, one at a time) and research row 57 (R-M11-CONFIG-SUBSCRIBERS); each build gets a Claude check before acceptance.
- **Resume point (manager, 2026-10-23).** Claude check PASS on the M13-017 groundwork (ec4dc4a8), the drive-off
  threshold tests (7cfb711d) and B-M13-14 (`research/20261023-m13-017-groundwork-tests-claude-check.md`). The threshold
  contract (50 until footprint exit or >50 mm travel) is covered through production. Next, in a **fresh session**:
  1. Write the dock-and-drive-off robot check (plain M5 format, every expectation cited: 0x00512E8A/0x00512E9C,
     0x00511D44..DC4, 0x00513CD8..0x00513E2A; footprint exit at x > 85.9 mm). The operator runs it.
  2. Builder jobs for M13-017's shared pieces, from the groundwork: configuration pending/rebuild/notify (C2-C6),
     charger map add/cache/remove (M1-M9), SetPose cache invalidation (L1-L2). SevereNeedsState writer waits for M15.
  Workers: Codex is back; the free model stays on small builder jobs only.
- **2026-10-21 configuration recipient review:** held row36 bounded native check recorded in `research/20261021-m11-pose-change-configuration-check.md`. Pending-ID/threshold/reset findings confirmed; root-pose cache correction prepared but not approved or applied. No build released; container and public-state subscriber gaps remain. Codex review; Claude spot-check pending. Totals unchanged:2 builds verified,0 records settled. Next ordered review:row44 retained pose owner. No new research or hardware check.

- **2026-10-21 B-M13-11:** shared row2 height selector and empty/singleton/line minAreaRect branches built in6a8d507f; separate Codex bounded review PASS. Focused73/73, full6036/6036, zero skipped (6m17s), fidelity707 PASS. Codex review; Claude spot-check pending. Session totals:2 builds verified,0 records settled. Backlog review continues; M13-017 not hardware-ready. B-M11-18 new-record inventory approval and bounded M15 row39 hold exception have been asked asynchronously; neither is assumed approved. No new research queued and no robot check written.

- **2026-10-21 B-M13-10:** Claude-authorized false insertion stamp correction implemented and separate Codex bounded review PASS; focused53/53, fidelity707 PASS, M13-navigation correction re-approved. Full suite6019/6019 PASS, zero skipped (6m25s); implementation7f42b427, claim reconciliation0d0ccc1c. Codex review; Claude spot-check pending. Builds with bounded review this session:1; records settled:0. Next: held backlog review row37 then38 and remaining M13-017; no new research, no hardware script.

- **2026-10-21, operator review-first hold:** finish only already-open research51–53, then stop reading;54/55 held. Review M13 drive-off/charger-map/rest of017, then M11, M8, M10. Other answers wait. All Codex reviews are marked Claude spot-check pending; required build findings need that check before acceptance. One builder at a time toward M13-017; no dock/drive-off hardware before it finishes. Monitor updated to this order.

- **2026-10-21, B-M10-2 bounded review PASS:** e332207b fixes six native classifier threshold literals; independent source/diff review `research/20261021-b-m10-2-check.md`. Builder reports focused65/full6019 passing; reviewer did not rerun. M10-001 remains IMPLEMENTATION_GAP; recipient joins and whole-layer acceptance remain open.

- **2026-10-21, B-M13-9 built and bounded review PASS:** contact-true charger observation is implemented in0138af17, fixtures corrected in3f54bccc; independent review `research/20261021-b-m13-9-check.md`, focused53/53 pass. Builder reports full6009/6009. M13-040 remains IMPLEMENTATION_GAP with shared configuration/map/cache lifetime gaps; M13-017 and hardware remain unfinished. No change to M1-M5 acceptance.

- **M5 run 4 (`acceptance/hardware/20261020-211745-M5-HW-87b83f`, 2026-10-20): hardware run PASSES** (judgement in its
  README). All sourced checks MATCH; LAYERS regraded MATCH after the lifecycle rule was limited to the animation stream
  (backpack-light 0x03/0x11 had followed the End). SLEEP known gap; LIVE_IDLE observation.
- **M5-animation ACCEPTED (manager, 2026-10-20)** on run 4. Every M5 record was already settled on 2026-10-19
  (53 EXACT_SOURCE, 8 COMPATIBILITY_POLICY, 3 EQUIVALENT_IMPLEMENTATION; `acceptance/M5-blockers.md`'s "23
  IMPLEMENTATION_GAP" count was stale). M5-036 (HARDWARE_ONLY) is HARDWARE_VERIFIED by run 4: echoes, Abort reaction,
  Start-without-End stream, locked-head suppression. Missing `// fidelity:` tags added for M5-036/042/057/061.
  **Next layer: M10–M15 and M7/M8** per "The plan". Known lead for M13: M13-017's located charger on SetOnCharger
  (it caused run 3's false cliff stop).

- **M5 run 3 (`20261020-200624-M5-HW-97161e`, operator, 2026-10-20 evening): NOT ACCEPTED; no stack defect found.**
  OK: TAGS, ABORT, AUDIO, LOCKED_HEAD, PARAMETERS; SLEEP known gap. The four incomplete parts were harness setup or
  grading: KEEP_ALIVE/LAYERS blocked by the engine-faithful lingering LiveIdleTurn eye shift
  (`research/20261020-m5-liveidleturn-linger.md`); TEARDOWN lacked enqueue origins for sends queued before Dispose
  (`research/20261020-m5-teardown-sends.md`); DRIVING was cancelled by a false cliff stop because Cozmo was left on the
  charger, through the recorded M13-017 located-charger gap (`research/20261020-m5-drive-cliff-stop.md`; M13 work, not
  built now). Harness batch (implementer, verifier FAIL on one overclaiming DRIVING explanation, fixed by manager to the
  verifier's wording): LAYERS before KEEP_ALIVE, face-only window before live idle, TEARDOWN origins from the send gate,
  a reason on every incomplete row, an off-charger check. Harness tests 110/110. Ready for the operator's run 4.

- **M5 harness wording rewrite (manager, 2026-10-20 evening; operator: the prompts were confusing and vague).**
  Operator-facing text only: an up-front list of the 11 parts, a `PART n of 11` header per part (what Cozmo does,
  what is checked, your job, duration), WATCH NOW / STOP WATCHING markers, six plain questions
  (1 yes / 2 no / 3 not sure / 4 missed; blinks as a count), plain result lines (OK / PROBLEM / COULD NOT CHECK /
  KNOWN MISSING PIECE / SAVED FOR REVIEW) and one OVERALL line. Removed the two unanswerable questions
  (animation stopped; eyes match the reference SVG; recorded NOT_ASKED) and ABORT's duplicate watch window.
  Grading, verdicts and evidence are unchanged. Codex's uncommitted partial edit of the same file was folded in.
  Ready for the operator rerun; M5 remains NOT ACCEPTED.

- **M5 second hardware run / B-M5-35 (2026-10-20):** operator supplied
  `20261020-180933-M5-HW-bde6cd`; judged **NOT ACCEPTED** in
  `research/20261020-m5-hw-2-judgement.md`. Confirmed harness startup defect:
  M5 connected before resources finished loading, so the once-per-Robot neutral
  lookup failed at 3467 ms; its group loaded at 3884 ms. The run's summary omitted
  those startup errors. Previous "ready for operator run" statements below are
  superseded by this finding. Claude left partial harness edits when usage ended;
  Codex is completing the existing M5 manager handoff with separate extraction,
  implementation and verification. B-M5-35 repairs delivered-readiness gating,
  startup diagnostics and early termination under existing M2-021/M5-010 evidence;
  no engine retry or fidelity reclassification. Independent affected-diff review
  PASS; focused 131/131, fidelity 706, offline validate (11 checks/45 records) and
  preview PASS. Full suite **5993/5993 PASS**, zero failed/skipped, 6 m 6 s,
  run once before commit; report `research/20261020-b-m5-35-check.md`.
  Implementation **893e1027**. Corrected harness ready for operator rerun;
  M5 remains NOT ACCEPTED.
  The handoff's "drive succeeded" statement is contradicted by the bundle:
  `FailedTraversingPath`, successfulTraversalCoverage=false. M12/M13 remains open.
  The reported early-connect logs in the accepted M3/M4 run need a separate bounded
  impact check; no acceptance is silently rejudged here. Physical face symptoms
  and the LAYERS/KEEP_ALIVE incomplete coverage remain unproven beyond this setup bug.

- **M5 decisions before the rerun (operator, 2026-10-20; manager review of Codex's M5 work):**
  - Codex's run-1 judgement and B-M5-24..31 reviewed and adopted (no stack behaviour bug; harness bugs fixed;
    reports `research/20261020-m5-hw-1-judgement.md` and `research/20261020-b-m5-24-check.md`..`-31-check.md`).
    M5 harness tests 62/62, fidelity check passes. Spot-checked: live-idle FF restart `0x57D3F0..D44C`.
  - **Diagnostic hooks approved.** LAYERS, PARAMETERS and AUDIO could never be conclusive on hardware: their graders'
    evidence (per-frame face flag, KeepFaceAlive blink-timer steps, send-admission allowance) was supplied only by unit
    fixtures. Read-only diagnostic observation in the stack touches none of "What we reproduce" 1-4, so it is allowed
    instrumentation, not invented behaviour. Job **B-M5-32**.
  - **DRIVING split.** M5-063 is judged on whichever phase path the drive's action results select (successful Start ->
    Loop -> End, or failed Start -> End cleanup). Successful traversal moves to the M12/M13 acceptance, which must observe
    a successful DriveToPose; FailedTraversingPath stays unexplained there. Job **B-M5-32**.
  - **Superseded by operator, 2026-10-20, AGENTS.md Hardware runs:** KEEP_ALIVE's resting-eye
    comparison is retained as an observation, not a verdict gate. B-M5-34 now includes a per-check audit:
    every automatic behavioral expectation needs an engine record and native address/file; unsupported
    appearance, random timing and firmware expectations become observations. Earlier resting-eye gate
    approval is withdrawn. B-M5-34 completes this correction, as recorded below.
  - **B-M5-34 correction (Codex manager, 2026-10-20):** both bounded native verdict audits adopted
    (`research/20261020-m5-verdict-audit-a.md`, `-b.md`). A separate implementer corrected the
    harness comparisons and instructions; the review is
    `research/20261020-b-m5-34-verdict-check.md`. Every check now has a purpose and cited comparison,
    with physical observations and capture completeness kept explicit. Independent affected-diff review
    PASS; focused tests 90/90; offline preview/validate PASS (11 checks, 44 records);
    fidelity 706 PASS; full suite 5979/5979 PASS, zero skips, run once before commit.
    Implementation `4333d6ee`. The corrected harness is ready for the operator run. No production-engine or manifest change,
    no hardware run and no M5 acceptance.
  - **M5 is NOT ACCEPTED.** Next: operator rerun; B-M5-32/33 hooks and harness corrections are built and independently verified.
  - **Handoff (2026-10-20):** B-M5-32 was dispatched to cozmo-implementer, and the session may end before it reports.
    **Built (implementer reported 2026-10-20):** M5Hw tests 72/72, wider filter 1532/1532, fidelity check passes,
    full suite not run; report in `jobs/status/B-M5-32.md`, ten choices listed there for the manager to confirm
    (public `AnimationScheduler.Diagnostic` surface, `StreamSendBuffer.Buffer` now returns long, 1000 ms join margin).
    **Codex independent review FAIL (2026-10-20):** `research/20261020-b-m5-32-check.md`.
    F1: LAYERS can pass despite an explicit unjoined-frame problem. F2: AUDIO assumes zero earlier spending when
    the window cuts a budget epoch, falsely reporting an accounting mismatch. Both reproduced; focused tests 72/72.
    Ten choices ruled on in the report; no engine behavior defect established. No full-suite run or commit.
    **Operator queued fixes (2026-10-20): B-M5-33**, next builder pickup. The initial local-only queue was not
    visible in the builder clone; corrected for publication with `jobs/B-M5-32-baseline.patch` carrying the rejected
    implementation, its new files and tests. The job gives apply-check instructions and the patch hash. The builder
    applies it, fixes F1/F2, and publishes the combined work under normal fidelity/full-suite gates. The baseline
    is not approved production code. Independent affected-diff review remains required before the operator rerun.
    No hardware run or settlement by the builder.
    **B-M5-33 complete and independently verified (Codex, 2026-10-20):** implementation `c984cc1f`,
    validation publication `cb0f657c`; `research/20261020-b-m5-33-check.md` PASS closes F1/F2.
    Reviewer focused tests 79/79, fidelity 706 records PASS; production hooks match the previously reviewed
    B-M5-32 baseline. Builder recorded full suite 5968/5968 PASS; not repeated for this bounded re-review.
    Local workspace fast-forwarded to the published implementation after verifying/removing only its identical
    baseline patch. The operator rerun is ready; no records settled, no hardware run by Codex.

- **Temporary M5 handoff (operator, 2026-10-20):** Codex is manager and independent verifier for M5 until Claude
  returns; evidence, role separation and approval checkpoints are unchanged. First robot run judged in
  `re-analysis/research/20261020-m5-hw-1-judgement.md`: **NOT ACCEPTED; operator rerun required**.
  Native live idle permits FF Start/End bursts; the first-read no-End lead was a harness error, not a proven
  scheduler defect. Other confirmed harness errors concern abort ordering, conditional face output, observation
  phases, mixed-epoch counters, teardown interval and failed-drive phase expectations. Jobs **B-M5-24..31** queued,
  starting with the live-idle oracle. No stack behavior fix is justified yet, no manifest status changed.
  Physical observations, exact finite-glitch trace and the M12/M13 drive failure remain bounded uncertainties.
  **Before rerun (operator, 2026-10-20):** B-M5-27 covers clear, expected-appearance descriptions and explicitly bounded
  observation windows for every prompt, retaining unexplained observations. FailedTraversingPath is still unexplained;
  a successful drive must be observed in a later run. Publishing these jobs does not unblock M5 acceptance.

- **Temporary manager handoff (operator, 2026-10-17):** Codex takes over Claude's manager and independent
  verification duties while Claude is unavailable; the operator has restarted the separate builder and research
  lanes. This supersedes the Claude/Opus-only role restrictions for this handoff, not the evidence standard,
  inventory approval checkpoints, or separation of extraction, implementation and verification. Reviews identify
  the actual verifier as Codex. First bounded review: B-M3M4-27 (788a04b3), the outstanding cleanup check on the
  M1+M2 acceptance path. No subsystem is accepted by this handoff.
  **Ended (operator, 2026-10-17):** Claude resumes manager and Opus verification; Codex returns to the research and
  builder lanes. Builder commits published during the handoff (B-M15-7, 2fc72bc3) still get the Opus verifier.

- **Now (2026-10-18, evening): M1-transport and M2-protocol are ACCEPTED**, the first layers of the project to get
  there (accepted commit 4f184775; bundle `acceptance/hardware/20261018-170546-M1M2-HW`, manager judgement in its
  README). Every check was as source once two tool comparison bugs were set aside (fixed in the tool). The robot has no
  needs record (case 1): the stack writes nothing where the original would write one (B-M1-10 gate, M15-014/M15-030).
  **Next layer: M3 + M4** (devices and motor control), per "The plan". Plan and blockers:
  `re-analysis/acceptance/M3M4-blockers.md` (2026-10-18): both Opus settling passes adopted (FAIL; 10 records settle
  after cleanup); builders B-M3M4-30..33 done; checks: 31 PASS (relative cube lights wait on
  research OV129), 32 and 33 FAIL (2026-10-19), fixes in B-M3M4-34 (review PASS 2026-10-19); OV129 adopted, B-M3M4-35 (marker normal,
  completes relative cube lights) done, check PASS 2026-10-19. **Settle readiness (manager, 2026-10-19; `research/20261019-m3m4-settle.md`):** M3-012, M3-055, M3-060, M4-043, M4-044, M4-047, M4-050 settled EXACT_SOURCE; the other 16 are source-supported but run only with the world model built. **Operator decision (2026-10-19): CozmoRobot always builds its world model (VisionSystem/BlockWorld)**, as the engine does: job B-M3M4-36 done, check PASS (`research/20261019-b-m3m4-36-check.md`); the 16 settled (M4-052 as EQUIVALENT_IMPLEMENTATION: bionic atan2f). **Every M3/M4 record is now settled, policy, equivalent or hardware-only.** **M3-device and M4-control are ACCEPTED (2026-10-19)** on the operator's run `acceptance/hardware/20261019-182707-M3M4-HW` (manager judgement in its README; script `scripts/m3m4-hw-2.md`, tool at e95d4acd). **Next layer: M5** (animation and the face), per "The plan". **M5 started (2026-10-19):** plan and blockers in
  `re-analysis/acceptance/M5-blockers.md`; B-M5-22 check PASS; both settling passes adopted (2026-10-19): 20 records settled, M5-020
  corrected and M5 re-approved; B-M5-23 (head input) check PASS, M5-011/014/030 settled: **M5 holds no gap
  records**; hardware script `m5-hw` approved (c2f2dfbd); **the operator's first run looked wrong to them**: judging
  procedure and handoff in `re-analysis/acceptance/M5-blockers.md` (2026-10-20). B-M3M4-33 built the robot-state history (pulled forward by the operator 2026-10-18); OV128 adopted with corrections
  (`research/overnight/20261018-ov128-check.md`); operator decisions recorded on M3-004/017,
  M4-006/039/040/042.
- **Numbers:** `re-analysis/FIDELITY_PROGRESS.md`, generated from the manifest by
  `python -I re-analysis/tools/status_views.py`. All 15 engine subsystems are INVENTORY_APPROVED; **M1-transport and M2-protocol are ACCEPTED** (2026-10-18).
- **Jobs:** `re-analysis/jobs/STATUS.md` (generated) shows, per builder job, the queue note, the status-file state and any
  verification evidence file. Implemented (status file DONE), verified (a check file exists; read it for the verdict) and
  accepted (subsystem review state) are separate and are never inferred from one another.
- **Lanes as run since 2026-10-11:**
  - one Codex builder (clone `cozmo-stack-codex2`) works `re-analysis/jobs/QUEUE.md` under `jobs/CODEX-BUILDER.md`;
  - research requests are listed in `re-analysis/research/OVERNIGHT-QUEUE.md`. Codex answers them, or the manager runs
    read-only Opus extractors and adopts the answers as checked evidence;
  - the manager writes jobs, adopts checked research, and has each builder commit checked by a read-only Opus verifier.
- **Standard (operator, 2026-10-17):** AGENTS.md "What we reproduce" replaces "Exact, always". Observable behaviour
  (robot wire, robot behaviour, C# API delivery and readiness, downstream state) comes from source; implementation is
  free where the result is the same; logs are best effort; exclusions are recorded decisions; narrow checks before
  full extractions; applied layer by layer, never in bulk.
- **Direction (operator, 2026-10-16; standard superseded 2026-10-17):** reduce repetition. In order:
  1. reconciled, generated state views (this revision);
  2. the next acceptance target and its concrete blockers, wherever they live;
  3. regression tests that reach behaviour through the real production wiring, or say that they don't;
  4. a narrow pilot of a mechanical log check, kept only if it catches real defects cheaply.
- **Manager (operator, 2026-10-17):** Opus stays technical manager; no separate lighter coordinator for now
  (revisit if routine management becomes a substantial share). Fresh manager sessions at natural checkpoints, once
  findings are adopted and this file and `acceptance/M1M2-blockers.md` carry the handoff; the saving is unmeasured.
  Fix reviews resume the original verifier where practical, scoped to the changed code and affected behaviour, with
  all gates kept. Routine bookkeeping uses `status_views.py` and `fidelity.py`.
- **History** up to this revision: `re-analysis/archive/PROJECT_STATE-history-to-20261016.md`.

## Decision notes (operator, 2026-09-24: "just make notes of stuff like this; test later and pick the best choice")

Small behaviour choices are noted here rather than put to the operator. Each keeps its current behaviour until a test settles it.

- **M6-042 N12, decided (operator, 2026-10-16): skip and log.** "generally we don't want to reproduce errors". AGENTS.md's M1-034 standing rule now also covers hangs and deadlocks of the original; defined error handling stays exact.

- **Process guard (manager, 2026-09-26, prompted by the NV correction).** AGENTS.md now carries two manager rules: a settled record owns its whole production path (an unrecorded part gets its own gap record before the claiming record is settled; no deferral to another record, a comment or prose), and a claim about an existing record is checked against the current manifest rather than recalled. Origin: M3-022 was EXACT_SOURCE while the NV wire had no records and the gap lived in `NvStorage.cs`'s summary, and the NV pass 4 report called M11-011 contradicted from an earlier revision of its text.

- **Motor stop on shutdown.** CozmoRobot.Dispose sends StopAllMotors and DriveWheels(0) before disconnecting. The original only sends the DisconnectRequest (B33, CC29). Kept for now as a probable safety behaviour. Test later: does the robot stop on its own when the link drops?

- **SyncTime stamp without AbsoluteLocalizationUpdate (obsolete since the M4 batch, 2026-09-25).** The stack now sends AbsoluteLocalizationUpdate {0, frame 0, origin 1} at SyncTime and stamps +0x520 only when that send succeeds, as the engine does (M1-041, M4-020).

- **Wwise mix rate (M6, 2026-09-24).** In the original the Wwise mix rate is the phone's native output rate, capped at 48000: min(AudioTrack.getNativeOutputSampleRate, 48000), cached at 0x0108DF90 (0x00A56E80..0x00A56EA4). The Hijack plug-in then resamples to 22320 Hz by linear interpolation. This stack has no phone, so it uses 48000, which is what the cap gives on typical phones. This will be recorded as a COMPATIBILITY_POLICY in the M6 inventory. Revisit if a capture from an original phone ever shows another rate.
- **Wwise runtime found (M6 extraction, 2026-09-24).** The Wwise 2016.2 runtime is statically linked into libcozmoEngine.so, as symbol-less ARM code at 0x0095E540..0x00AE2E40. Anything earlier marked BLOCKED_EXTERNAL for "Wwise runtime semantics" is therefore RECOVERABLE_GAP, not blocked, and gets re-classified as each layer (M6, M9) is inventoried.

## Cleanup queue (non-behavioural; fold into the next batch)

- **M3M4-HW harness (2026-10-19; bundle README):** answers typed with spaces don't map to hyphenated choices and the PARITY_STEP grader's expected answer is reversed; DAS_EVENTS should attach through `BeforeStart`; DISCONNECT_QUIET should judge "no data after the teardown batch" rather than key on a DisconnectRequest frame (M1-006 pacing can hold it back). Conformance tools set the nominal calibration after construction instead of through `VisionCalibration` (B-M3M4-36 check).

- **Flake (2026-10-18):** `NeedsPersistenceGateTests.RealConnectNeverRestoresOrWritesUnbuiltNeedsPersistence(hasV5: False)`
  failed once in the push gate ("connect state 2": no ConnectionState 2 within 10 s after the injected ConnectionResponse,
  `NeedsPersistenceGateTests.cs`:116/177) on a memory-starved machine; passed 3/3 alone and in the next full run
  (5751/5751). Builder to check whether the injected response can race the transport's connect state, or only load. A second gate run (2026-10-18) failed the hasV5 case with the MISSING counted twice: the class read the process-static `SteppedBehavior.MissingReported` outside its serial collection. The manager put it in the "SteppedBehavior missing-report statics" collection (test-only); recheck whether that also explains the timeout.

- **B-M1-11 check (2026-10-18):** name the skipped AttemptReadFromDevice success tail (NeedsState Decay 0x006936C6, +0x30
  0x006936CE, DAS 0x006936D2) in the device MISSING (`Needs.cs`:1386) and on M15-014/M15-030; relabel or rebase the
  `{1,1,1}` expectation in `NeedsPersistenceGateTests.cs`:281,286; cite the restored Lab test method on M15-050.

- **B-M1-9 check (2026-10-18; research/20261018-b-m1-9-check.md):** Q2 the new Impl handlers also reach
  `Engine.DeviceRoute` (`RobotSubscriptions.cs`:201-207; no matching device handler, comment or `routeDevice:false`); Q3
  image-latency update position comment (`CozmoEngine.cs`:1337 vs native 0x00514386); Q4 cite the robot+0x31C writer in
  the BA warning comment (`SettlingMessageHandlers.cs`:21-22). From the B-M1-8 check: Q1 raw-tap re-parse cost and
  drain-time timestamps in Conformance tools (the hardware script takes timestamps accordingly); Q2 GetBlockPool position.

- **B-M2M15-2 Opus check (2026-10-17; research/20261017-b-m2m15-2-check.md):** PASS with queued items; finding 1
  (inline app delivery under the streamer lock) raised to blocking, fixed in B-DELIV-1. Queued: relabel
  `NoRobotDrops…` as unit; the TriggerLiftSafe loop-count re-Init (unreachable); C5's widened B-M12-7 race (B-M12-7);
  the Turn C4 log.

- **B-M1-3 Opus fix review PASS with queued items (2026-10-17; research/20261017-b-m1-3-check.md):** dispose the
  `ReliableTransport` in `CozmoRobot`'s constructor when `new CozmoEngine` throws for a bad config (N1; background
  threads leak, nothing sent). B-M1-2 Q4/Q5 go to the next M1 job.

- **B-M15-11 Opus check PASS with queued items (2026-10-17; research/20261017-b-m15-11-check.md):** relabel
  `LabDefinitionsCheckedRowsTests` (its enable-log part is unit: replayed Init) and correct M15-049's "no replay" note;
  make `VisionConfigCheckedRowsTests.cs`:65 assert something startup-dependent (e.g. the single "Enabling the lab"
  entry). The EngineConfigJson startup stop is M1-054's (OV126).

- **B-M15-7 Opus check FAIL (2026-10-17; research/20261017-b-m15-7-check.md):** three blocking (Lab Init aborts on an
  AB-disable MISSING the adopted evidence settles as always-enabled; Feeding deselect keeps its E2G 0x0D subscription;
  Unicode locale lowercasing). Fixes and queued items in B-M15-11, after B-M1-2. Manager to add: a COMPATIBILITY_POLICY
  record for the locale app input (native Locale enum at context+0x18, 0x006A53D2..0x006A53DC; check Q6).

- **OV124 adopted (manager, 2026-10-17; research/overnight/20261017-ov124-check.md):** after B-M1-2 publishes, fold
  the answer into M1-055's unresolved (engine side: the fresh Init-time broadcast sends zero AnimationAvailable, then
  EndOfMessage(0), then Debug; whether the game receives it is M1-056). New M1-056 (RECOVERABLE_GAP, OV125 queued) and
  M1-057 (0xB1/0xB2/0xB3 unreachable). M1-transport re-approved for the two new records.

- **B-M3M4-27 Codex source check (2026-10-17):** add retrospective `reach:` labels to its changed/new
  M3M4X21/M3M4X17 regressions and per-record coverage to its status report, following
  `re-analysis/research/20261017-b-m3m4-27-check.md`. The label rule postdates the implementation. Source review
  passed the five items; M4-044/M4-045 remain IMPLEMENTATION_GAP and no subsystem is accepted.
  Manager adopted the bounded PASS after fidelity check (690 records) and full suite (5569/5569, zero skipped).

- **From the B-M8-7/B-M8-8 verifier (2026-10-16, PASS):** (1) narrow the file-wide `// fidelity: M8-015` tag in `SteppedBehavior.Actions.cs` (M8-015 cites only 0x005BDC04), or extend the record; (2) B-M8-7 status MISSING group 1 needs addresses (concrete IActionRunner classes exist); (3) `AIWhiteboardHandlers.cs:105,166` raise `PossibleObjectsRedrawn` instead of calling the built render unit (native 0x0056C692; not live); (4) the latent StopActing cancel is recorded on M8-015 and must be built before the owned StartActing is wired.
- **From the M6-003 batch (2026-09-26):** `WwiseAdpcm` clamps the header step index to 0..88, which the source does not (unreachable on all 227 shipped ADPCM media); `WwiseAdpcm.Decode` refuses channels outside 1–2 where the runtime decoder is channel-generic (no shipped file has ≥3); the `WwiseAudioSource` comment still lists "seven stereo ADPCM undecodable"; `fmt+0x14` byte-1 semantics are UNKNOWN (byte 0 == nChannels in all 227 files, so moot). **Flake:** one full-suite run after the M6-003 change reported 1 failure of 1465 that did not reproduce in two subsequent runs; if it recurs, capture the test name.
- **From the NV batch A verifier (2026-09-26, non-blocking):** the stack's `NVStorageOpResult` record orders the fields `(tag, op, result, index)` where the native `BroadcastNVStorageOpResult` is `(tag, result, op, index)`; it is an internal event accessed by name, so no wire impact. The engine's `ReadSuccess`/`ReadEntryNotFound`/`ReadFailed` completion log lines are not emitted. Test coverage: `M3_028_TheRestIsReRequestedWithoutReArming` does not assert the "no re-arm"; `M3_026` does not exercise FIFO/one-in-flight; the timeout test uses a state clock only. When M3-032 is built, re-check the connection read's arm against the gating (a `RobotState` with clock > 5000 arriving before the reply would now time it out).
- **From the M10 test-oracle fix (2026-09-26):** `SteppedBehavior.IsRunnable` (`SteppedBehavior.cs:96-97`) gates on `context.Robot.Animations.Library is not null`, which the engine's `IBehavior::IsRunnableBase` (0x5BD778) does not. It is inert on the live path (the library is loaded in production), it is not claimed by any M10 record, and MD2 defers "the behaviours' IsRunnable" to M8. Decide whether to record or remove it when M8 is inventoried.
- The earlier queue was cleared by batch 4(i): the `using` isolation, TransportConstants LF, T_k2, stale MISSING and verifier-reading comments, and the M1-005 doc.
- LinkCheck (Cozmo.Conformance): its warning histogram matches "sendto failed" by prefix; the message is now "UDPTransport.SendFailed: sendto failed ...". Update the key after batch 3 merges; pass/fail is unaffected.
- From the device-reset re-verify (2026-09-24):
  - Label the 2 s vision removal wait as local, citing 0x0065257C.
  - Label the test's `Enabled`-kept assertion as policy, not a primary-source oracle.
  - Make the release in the frame-in-flight test deterministic; the 300 ms timer is a flake risk.
  - PetWorld has no lock (unreachable: OKAO pets are unavailable).
  - The implementer's note that "ReadCalibrationAsync has no caller" is wrong: six Conformance tools call it. What is missing is the connection-time read.
  - `_generation` in AnimationScheduler has no COMPATIBILITY record.
  - FakePort `Calls` is written without a lock.
- fidelity.py `--check` does not enforce FidelityManifestTests' rule that every non-EXACT record has an `effect`. Align the two.
- From the control-check review, after the run: pin the ANIM wheel figure (-75 mm/s for 264 ms) against the clip itself, and exercise the abort path.
- From the M2 batch verifier (2026-09-24, non-blocking):
  - MessageBase.Parse: the doc comment ("short body → RawRobotMessage") is stale and its catch blocks are dead.
  - PROTOCOL_STATUS.md and MessageCatalog label FallingStopped `hardware_refined`, but the fix is engine-derived.
  - gen_protocol.py `ReadStructArray` does not stop at the first failed element (D10). No inbound message has a counted struct array today.
  - CLAD strings are decoded as UTF-8, where the engine reads i8 chars. Nothing in Cozmo.Robot consumes them.
  - The PrintTrace format id uses the low 16 bits, which is not established. It is used for logs only.
- **For the M12 inventory (behavioural, found while reading consumers):**
  - Docking.cs:353 releases the carried object on BlockPlaced without the engine's success gate (R-P1).
  - Docking.cs:357 treats MovingLiftPostDock as `!= 0`, where the engine compares that byte for equality with IDockAction+0x80 (R-P4).
- For M4: the MessageExtras default arguments (SetHeadAngle 10/10, SetLiftHeight 3/20, RobotLink.cs:100) have no engine counterpart (M2-015). The only callers that rely on them are CozmoRobot.cs:499 (a public API) and Probe.cs:92.
- **For the M5 batch (found by the M3 verifier): done in the M5 batch (2026-09-25).** Kept for the record:
  - After a cancel (SetStreamingAnimation(null)), the engine replays the neutral face (A6, A31). The stack has no neutral replay.
  - StreamLive runs outside an Update, so its timing differs from the engine's (A29, 0x0057D080).
  - A Play then Stop inside one tick: in the engine the live stream continues rather than re-initialising.
  - RobotAnimationSink.Finished sends a BodyStop on cancel and can send one after EndOfAnimation. The stack also sends its own BodyStop before EndOfAnimation. None of these has an engine counterpart (A20, A24).
  - `_lastFace` is resent without the layered face flag (A16(9)).
  - Stream error paths: a failed send re-runs `AudioFramesSent++` and KeyframeFired on retry. A throwing send stays at the front of the buffer and faults every tick.
  - The AbortAnimation 0x8D send (M5-023).
  - The production test does not exercise the budget-stop/refill path.
- **control-check, before the next robot run:**
  - ANIM_CANCEL's "nothing after cancel within 100 ms" criterion and its M5-023 note contradict A25: a cancelled clip's pending frame can go out at the next tick that has budget. Rework the criterion or mark it not judged.
  - CUBES should cite M9-017 and M4-024.
  - CONNECT should record the origin and frame the robot reports (M4-021), and whether DefaultCameraParams 0xC8 arrives (M3-019).
  - CAMERA should record image brightness (M3-019).
- **From the M5 verifier (2026-09-25, non-blocking):**
  - **LiveUpdateFailed:** when UpdateLiveAnimation returns 1 the engine logs and returns (0x0057D086..0x0057D0E6); the code logs and carries on to the tail. Unreachable: an add fails only above 1000 keyframes.
  - **control-check keep-alive: done (2026-09-25).** ANIM's `streamingStops` became `clipStreamEnds`, and ANIM_CANCEL's leftover and End criteria now stop at KeepAliveQuietMs = 400 ms, before the keep-alive block's 0.5 s (A31). `abortAnimationSent` is now a criterion (exactly one 0x8D). The keep-alive stream and the neutral replay are recorded as the observations `keepAliveStream` and `neutralReplay`, and M5-036 is in the run's hardwareOnlyUncertainty.
- **From the M4 verifier (2026-09-25):**
  - **M10-007** is EXACT_SOURCE, but Apply has no production caller. The engine's CheckForUnexpectedMovement calls SetNewPose (0x0063E87E), and through F1..F3 that sends an AbsoluteLocalizationUpdate. Put this in M10-007's `unresolved` when M10 is inventoried; check the config flag kCreateUnexpectedMovementObstacles (0x00C7F620).
  - **SetBodyRadioMode order:** the stack sends it before SetOnCharger's threshold; the engine sends SetOnCharger's threshold (0x00512AB4) before SetBodyRadioMode (0x00512B46). This only matters if both fire in the same state.
  - **Charger-platform clearing:** without Robot::Update's clearing (M11 geometry), a robot that starts on the charger keeps the platform flag after it drives off. Until the flag clears, a PotentialCliff is ignored. The flag clears only on a committed off-treads change.
  - **B5:** callers of PlayLightAnim can still finish in a different order from the one they decided in. No caller off the engine thread exists today.
  - **VisionSystem Stopped path:** done (Q-c).
- Tests recommended by batch 4(i): one pinning the 14 Init tunables against the CA/B13 table (M1-005), and a dedicated R43 test for an unsent seq-0 entry at the front (M1-016).

## Layer order and review state

**Scope: FINAL (operator, 2026-09-24). Every layer is in scope, with the same cycle for each (see "Scope decision" above).** The order is bottom-up by dependency. M5's Wwise-driven audio is an interface to M6. Vision comes before manipulation and navigation, and the behaviour framework before the behaviours that run on it.

Review state per subsystem is in `re-analysis/fidelity_manifest.json`; `re-analysis/FIDELITY_PROGRESS.md` (generated) renders the current states and counts. The order of layers is the plan above.

## Hardware

- Only the operator's machine reaches a robot. The manager never plans a step that assumes otherwise.
- New runs go to `re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-<test-id>/` (AGENTS.md, "Hardware runs").
- **Existing tooling, not yet reviewed under this process:**
  - `hardware-test` in `Cozmo.Conformance` asks the operator for a verdict per check, where this process wants the result judged from a machine-readable bundle.
  - Earlier bundles are in `cozmo-stack/re-analysis/acceptance/` and `cozmo-stack/cozmo-acceptance-*.json`.
  - `HardwareCatalog.cs` maps checks to fidelity ids.
  - Each is to be reviewed alongside the layer it tests.

## Legacy documents

`re-analysis/HANDOFF.md`, `NEXT_MILESTONE.md`, the milestone documents and the claims of "complete offline" predate this process. They are notes, the lowest authority. Where they disagree with an approved inventory, the inventory wins.

The user-level agents in `~/.claude/agents/` (`cozmo-m1-transport-auditor`, `cozmo-m2-protocol-auditor`, `cozmo-proof-auditor`, `cozmo-adversarial-reviewer`) come from an earlier salvage audit and point at a stale scratch directory. The process uses the project agents in `.claude/agents/` instead.

## Accepted commits

| commit | subsystem | what it accepted |
| --- | --- | --- |
| 7aba301 | M1-transport | M1-004, M1-011, M1-012, M1-017 settled EXACT_SOURCE with evidence-derived tests; LINK check names M1-033. M1-003 and M1-008 tagged but held (DeleteConnection semantics; clock) |
| e6f2ed7, 8c9f405, 0691429, ed2729e | M1-transport | repair batches 2a, 2b-i, 2b-ii, 2c (receive path; clock/scheduler; connection lifetime; socket/addressing) |
| fef527c, 224accb | M1-transport | m1-link-check; closure pass re-approved; M1-LINK passed on hardware (20260924-112412-M1-LINK) |
| 3af197f | M1-transport | batch 4(i): transport completion, M1-039, 16 records settled |
| 518b730, 67d4bc2 | M1-transport | batch 3 app layer; settle to 26 EXACT_SOURCE |
| 4b89c79, abea3ce | tools | control-check self-judging hardware run, and its review fixes |
| (this commit) | M1-transport | device reset on RemoveRobot; M1-025, M1-015 settled EXACT_SOURCE |
| ce859bf | M1-transport | M1-024 decay bracket snapshot; M1-044/M1-015 warning log level; regression coverage |
| c8af57e | M1/M2 and transferred recipients | Cited manager/policy disposition; four new M1 gap records; re-approved 13 affected inventories |
| 4f184775 | M1-transport, M2-protocol | **ACCEPTED** (2026-10-18): all records settled, policy or equivalent; M1-033 by robot runs; M1M2-HW bundle 20261018-170546 judged as source |
| e95d4acd | M3-device, M4-control | **ACCEPTED** (2026-10-19): all 113 records settled, policy, equivalent or hardware-only; M3M4-HW bundle 20261019-182707 judged as source; the seven HARDWARE_ONLY records re-observed |

## Open decisions for the operator

- **M6-002 Vorbis IMDCT (correction C8).** The engine's inverse MDCT is a float NEON transform whose per-lane arithmetic and table indexing are RECOVERABLE_GAP (ranges and the 615-float master table are named in `re-analysis/evidence/m6-vorbis/vorbis-imdct.md`). **Decided (operator, 2026-09-27): exact.** Transliterate the lane-level arithmetic and vendor the 2460 table bytes. The RECOVERABLE_GAP rows go back to the extractor. The kernel stays fail-closed until then.
