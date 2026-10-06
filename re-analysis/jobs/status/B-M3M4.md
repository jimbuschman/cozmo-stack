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
