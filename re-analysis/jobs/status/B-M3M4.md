CLAIMED Codex 2026-10-06

Operator-scoped build under CODEX-BUILDER.md: only M3-device/M4-control records whose unresolved starts with Opus verification of B-CORE/B-CORE2. User prompt is this job's scope; no separate B-M3M4 job file exists. Checked verification text wins; no unchecked extraction or record settlement. Existing unrelated research files remain unstaged.

Matching records: M3-001,013,021,023,025,026,027,029,030,031,032,035,037; M4-001,008,010,011,020,025.

Plan: one batch at a time, cited live-path/log corrections and regressions; self-review CHECKLIST, fidelity check, full suite, commit, push, log. Existing SETTLED records remain unchanged except any specifically cited test gap; do not downgrade/re-settle them.

Initial MISSING triage:
- M3-023 StartCamera, M3-027 READ/retry/re-request +0xE8 and M3-037 buffer ownership: explicitly NEEDS EXTRACTION; no guessed implementation.
- M3-013 audio readiness (M6/M5); M3-021 vision init/config/pending application and M3-032 Gate B (M11): other-layer dependencies remain MISSING.
- M3-001 shipped libjpeg9 codec replacement and raw empty/error paths: missing checked complete codec/exception port rows; no stand-in treated as exact.
- M4-008 IMU/cube-battery units: missing citations; cannot invent evidence or alter frozen title.
- M3-026/031 retain M3-027 dependency while their independently checked log defects may be built.

Batch outcomes and any additional MISSING follow below.

## Batch 1 — checked logging corrections and live regressions

- M3-026: Read.InvalidTag uses %x; valid Read logs EnumToString(tag) before enqueue (00644E3A..00644E4C). Send remains on Update.
- M3-030: ReadEntryNotFound uses %x; ExecutingReadCallback is logged only for a present read callback, after outcome and before invocation (006436DA), not for MORE or an absent callback.
- M3-031: checked %x fields corrected; write/erase/wipe share Retry and NumRetriesExceeded logs. ResendLastCommand Retry precedes send; caller ResentFailedRead/Write follows send. Write caller fields come from received tag/result/op (006431AA..006431E0), helper fields from saved command. Seven resends/eight transmissions retained.
- M4-001: checked literal clip text, degrees of rescaled target via f32 multiply bits42652EE1 before clipping, one decimal; normal numeric text invariant. No motion/gate/math changes.
- M4-020: Robot.SendSyncTime event name on Setting pose restored (0051531E). Early FailedToSend warning already repaired in M1 and preserved; late-send failure branches retained.
- M3-032: missing fidelity tag added at built Gate A; no missing Gate B status invented.
- M4-010: new live-entry regression reaches pending request, advertisement, SetPropSlot, mismatched/matched connection response and requested disconnect. Does not claim every timer/diagnostic path verified.
- M4-011: live firmware entry tests Load grammar/five-entry bound and partial id mutation on failed type; live advertisements/tick test exercises equal-RSSI engine container order and native lower-case hex save. Existing helper grammar/save/rehash tests retained. Settled record unchanged, not re-settled.

CHECKLIST self-review: production paths are NvStorage.Read -> queued Update -> routed NVOpResult, Motion.SetHeadAngleAsync -> live builder, firmware SetPhysicalRobot -> cube Init, routed ObjectAvailable -> Robot.Update cube step, and SyncTime. Native checked boundaries reopened for gates, log strings, fields and order. Tests use independently stated native formats, retry limit8, literal angles and container order, not code-generated expected values. Callback logging uses the existing lock and invokes the user callback outside it; no new worker/thread or lock ordering. Read/send/deadline/sink semantics and other-layer recipients remain unchanged. No hardware run. Manifest edits are unresolved-only for the seven IMPLEMENTATION_GAP records; no frozen evidence/status/title changes or settlement.

Additional MISSING (not hidden by repaired logs/tests):
- M3-026/030: phone NULL-%s rendering for valid unnamed tags; visible MISSING logging rather than invented output.
- M4-001: phone printf exceptional-number spelling/rounding is not established by normal one-decimal log tests; no complete formatting claim.
- M4-010: complete timers, duplicate-type branch, all slots, expiry/reconnect/diagnostic coverage not supplied by the bounded new test; checked complete behavioral rows/production dependencies need manager disposition.
- M4-011: game-facing pool removal/clear/save-empty path is absent in the live integration; its message-channel/handler binding needs checked rows (cross-layer M1 game channel). No private-reflection test passed off as a production path.
- M3-032: non-Running engine state/UI return dependency belongs to M1, calibration/result Gate B to M11; existing MISSING seam retained.
- M4-020: higher-layer history/pose recipient semantics remain beyond these checked log rows.

Publication and final suite results follow after checks complete.

## Coverage of the initial matching set

| Record | Disposition |
|---|---|
| M3-001 | MISSING complete checked libjpeg9/exception rows; no codec substituted |
| M3-013 | MISSING M5/M6 audio-animation readiness |
| M3-021 | MISSING M11 vision init/config/result application |
| M3-023 | MISSING explicitly NEEDS EXTRACTION StartCamera |
| M3-025 | Already Opus SETTLED; unchanged |
| M3-026 | Checked logging built; +0xE8 and NULL-%s remain MISSING |
| M3-027 | MISSING explicitly NEEDS EXTRACTION +0xE8 lifecycle |
| M3-029 | Already Opus SETTLED; unchanged |
| M3-030 | Checked logging built; command-data dependency remains MISSING |
| M3-031 | Checked logging built; +0xE8 and upper write recipient remain MISSING |
| M3-032 | Tag defect repaired; M11 Gate B / M1 state/UI dependencies MISSING |
| M3-035 | Already Opus SETTLED; unchanged |
| M3-037 | MISSING explicitly NEEDS EXTRACTION buffer ownership |
| M4-001 | Checked clip-warning repair built; external exceptional formatting not established |
| M4-008 | MISSING checked IMU/cube-battery unit citations; cliff code already holds |
| M4-010 | Checked connection-boundary live tests added; complete-path coverage remains MISSING |
| M4-011 | Grammar/save/tie live tests added; existing EXACT_SOURCE record unchanged; absent game removal binding needs manager handling |
| M4-020 | Checked pose-log event repair built; already repaired early warnings retained |
| M4-025 | Already Opus SETTLED; unchanged |

Eligibility is captured at claim time; the built records' leading unresolved text now changes as CODEX-BUILDER requires. M3-018/022/033/034 and M4-003/016 do not have the requested leading Opus text and were not added to scope.

- Final focused checks: 320 passed, zero failed/skipped. Full suite: 3,946 passed, zero failed/skipped (4m20s); fidelity --check and staged diff --check pass. CHECKLIST self-review PASS for the built subset only.
- Batch commit and push-hook result follow in a publication entry. Remaining items above require checked extraction/other-layer integration or manager disposition; no additional build from unchecked rows is authorized in this job.

## Publication

- Claim: 5552aa0, pushed to main.
- Batch 1: 16a9210, pushed to main on 2026-10-06. Push gate independently passed fidelity and all 3,946 tests, zero failed/skipped (4m29s). No hook bypass or force push.
- Built/annotated records: M3-026/030/031/032, M4-001/010/020. Added live M4-011 coverage without changing its settled record. No record settled and no hardware run.

BLOCKED 2026-10-06: all independent checked corrections in this batch are published. Remaining MISSINGs are enumerated above: NEEDS EXTRACTION records, absent complete checked codec/format/unit/connection rows, and other-layer production dependencies. The layer is not complete or accepted. Requires manager-checked rows/integration or disposition before further building; stop here under the operator's scope.

## Operator slice — checked M3/M4 lower paths, 2026-10-09

Pulled main (4534941, already current). Operator authorizes only the build-plan checked slices: M4-016 defect, M4-009/017/018/019 and M3-010. October6 research and IMU candidate rows are not adopted or built.

- M4-016: the requested first-update stamp was already repaired by ActionRunner/ActionList integration. Retained it and added a live regression delaying the first tick31 s, then checking the inclusive30 s timeout/result03000018. Restored all reachable head/lift CheckIfDone diagnostics and four static u16 eleven-count counters. Dead eye-shift block remains absent.
- M4-009: confirmed the existing real Docking target writer/retention and translated activeID exclusion; existing live dock/abort test retained. No duplicate dock field or carried-ID substitution.
- M4-017: SetHeadlight queues mode14 on the actual installed VisionSystem before reliable/not-hot send. Absent recipient prints the empty-format error, stores process _errG and evaluates the shipped no-op debug-break. Image update drains enable-before-pop, logs before mask mutation, preserves idempotence and Idle fallback. New queue is cleared by the existing retained-owner removal reset, and an in-progress image pops only the same request it applied, never a later owner's entry. This is not a settlement of the M4-027 whole-component teardown candidate.
- M4-018: when vision exists, refresh now reads actual BlockWorld.Robot2C4 (SetLocalizedTo/publication owner). Existing fallback, held refresh, all/single gates and static off payload retained. Corrected all-objects disable to clear/repick each object then store comp+22 last, from reopened00639AFA..00639B32.
- M4-019: confirmed existing match/101 resets, treads-change zero-counter/unconditional stats and3000 ms retention. Corrected mismatch sErrorF level, unsigned frame fields, _errG/debug-break, then reset0 before Delocalize trigger.
- M3-010: scheduler retains actual float vector input and reaches Encode(float), including fractional/clamped/NaN warning/zero-tail behavior. Readiness, stop, replacement and completion include floats. No guessed short normalization or volume multiplication. M6 producer remains missing.

[Call accounting](../../research/20261009-M3M4-checked-build-census.md) lists counterparts, dead branches and every missing recipient. [Native reopening](../../research/20261009-M3M4-checked-build-native.txt) lists104 call instructions and43 resolved/reopened targets, names and literals. No file open/close in this bounded slice. The native complete VisionSystem destructor was also reopened while diagnosing queue lifetime; its other descendants remain outside approved build authority.

MISSING retained:
- M3-010: actual M6 Wwise f32 buffer producer; short seam is a candidate.
- M4-009: complete ObjectID/slot ownership and Cubes.Handle slot admission, M11.
- M4-016: phone diagnostic float formatter exceptional spelling/rounding/locale; normal invariant text is tested, not complete runtime equivalence.
- M4-017: E9 LimitedExposure reader/effect and other mode producers/special recipients, M11.
- M4-018: full default carried/visible selection and ObjectID traversal, M11; missing ObjectInfo single-object API branch needs checked disposition, not an app-unreachability assumption.
- M4-019: actual M11 history add/frame lookup failure results, Delocalize/origin/charger geometry; M7 behavior end/M8 Cancel(-1); DetectGyroDrift00512FBA and DetectBias00512FC4 have no live counterpart and need manager allocation/checked owning-layer rows.

CHECKLIST self-review: live entries and checked boundaries named in call accounting. f32 widths/getDegrees bits, literal log fields/levels and order reviewed against reopened targets. Queue synchronization does not acquire the image-processing lock from SetHeadlight, and does not invoke callbacks under its queue lock; removal invalidates queued entries without popping a new owner's request. Counters are process static and wrap u16, not reset on robot removal. Source-derived tests cover real entries, no generated expectations. No hardware run. Six records remain IMPLEMENTATION_GAP with built/awaiting-strong-verification unresolved; only unresolved fields and generated gap report changed, no frozen title/evidence/inventory or status approval.

Focused run75 passed, zero failed/skipped. Initial full run exposed queued-request retention on removal; stopped that failed run, repaired the queue lifetime, and the focused removal regression now passes. Final full run and publication follow below. Rule9 whole-layer packet is not due: this bounded slice leaves same-layer unchecked rows/gaps; no whole-layer completion claim.

Final checks: second full run passed3995 and exposed only the stale RFixBatch3 log assertion (missing native error level). Corrected that expectation from00512F40/00BE697E; final focused run76 passed, zero failed/skipped. Final full suite3996 passed, zero failed/skipped (3m07s). Fidelity --check and diff --check pass. Commit/push publication follows.

### Checked-slice publication

- Build commit: 4ae4d32f5f3253f60e704e8bd5f80ccadab5b6c4, pushed to main on 2026-10-09.
- Push gate independently passed fidelity and all 3,996 tests, zero failed/skipped (3m27s). No hook bypass or force push.
- DONE for the operator-authorized checked slice. M3-010 and M4-009/016/017/018/019 remain IMPLEMENTATION_GAP, built and awaiting strong verification, with the MISSING dependencies above retained. The whole M3/M4 layer is not complete or accepted. Stop here.

## Adopted-row continuation — 2026-10-10

CLAIMED Codex 2026-10-10. Operator authorizes B-M3M4 adopted groups and corrections, including the remaining-record adoption. JPEG decode stays held; default-table and Save encoder extraction goes to the manager. Each implementation batch runs the fidelity check and full suite before commit/push; no record is settled. Base ad8fa6c. Existing unrelated untracked research files are preserved.

### NV lifetime candidate and inventory blocker — 2026-10-10

Claim commit 90cbd70 was pushed. Reopened the adopted idle/destruction and NV completion call targets. Local candidate repairs M3-040 front-preserving idle dispatch and exact debug log, M3-038 unsubscribe/discard order, and M3-030/M3-031 active-request lifetime through terminal callback. Four source-derived/live-entry regressions cover entered-drain order, throwing-front retention, disposal, and signal-based competing host registration. Full suite: 4,003 passed, zero failed/skipped (3m06s). fidelity --check and diff --check pass with statuses preserved. No hardware run or settlement.

Candidate code/tests remain uncommitted locally. Reviewable diff: research/20261010-M3M4-NV-lifetime-candidate.patch. Native target text, complete call accounting and remaining MISSINGs: research/20261010-M3M4-NV-lifetime-build-census.md. Persistent saved-command destruction (00643F40) awaits the adopted N implementation; backup destructor (00643F48) is M15. Write completion broadcast/callback/backup order remains for the W batch. Same-thread idle reentry is retained; competing host callers use a nonblocking execution gate, with the host exit-race limitation recorded in the census.

BLOCKED: the manager adopted build rows in B-M3M4.md, but the inventory/approved snapshots still freeze M3-038/039/040/041 and M4-026/027/028/029/030/031 as RECOVERABLE_GAP. CODEX-BUILDER rule 5 requires built records to stay IMPLEMENTATION_GAP; fidelity.py's approval check rejects RECOVERABLE_GAP-to-IMPLEMENTATION_GAP changes without a new approval. M3-037's requested policy narrowing also needs refreshed frozen evidence. The worker has not rewritten inventories or approval snapshots. Manager must integrate the adopted rows, set buildable records to IMPLEMENTATION_GAP and the three unsupported SDK recipients to COMPATIBILITY_POLICY, narrow M3-037 as directed, and refresh the two approvals. The candidate is concrete and tested; publication waits on that manager-owned step.

Remaining adopted groups and JPEG default-table/Save research are not complete. A local JPEG default-table draft is uncommitted and is not build authority. Rule 9 whole-layer verification packet is not due: same-layer work remains. Stop at the inventory blocker; no claim that this layer or adopted slice is done.

Blocker/census publication: 4c8b135, pushed to main. Candidate patch uses zero-context hunks to avoid whitespace-only context in the report file; validated with git apply --reverse --unidiff-zero --check against the local candidate. Documentation-only pushes skip the suite under the normal hook. Production candidate remains uncommitted.

### Operator-authorized NV candidate publication — 2026-10-10

The operator explicitly directed committing/pushing the tested NV candidate now and then stopping, with the manager taking over M3/M4. This instruction supersedes the publication hold above. Publish only NvStorage.cs, its four M3DeviceTests regressions, unresolved-only M3-038/M3-040 metadata and the generated gap report. Current frozen statuses/evidence and inventories remain unchanged; no record is settled. The candidate passed all 4,003 tests before this commit; the normal push gate checks the committed content again. Existing native census and candidate patch remain the review evidence. All MISSINGs and the host dispatch limitation remain recorded above. No further adopted-group builds or JPEG research in this handoff.

Published NV candidate: 7dc053f35fcc01180517b927b1fa32099e13bed5, pushed to main after rebasing onto manager research commits. Normal push gate passed fidelity and all 4,003 tests, zero failed/skipped (3m13s). STOPPED at the operator's direction; manager owns M3/M4 continuation until this evening. This completes the requested NV publication only, not the layer build.
