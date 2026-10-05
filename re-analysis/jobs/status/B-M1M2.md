CLAIMED Codex 2026-10-05. Claim commit: 4ab6a2c (pushed).

Trial: M1-015, M1-024, M1-025, M1-041, M2-002. M1-029 and M1-031/044/045 deferred by the operator pending the manager's checked rows. The research row check and instruction companion are saved locally in re-analysis/research/20261005-B-M1M2-rows-check.md and 20261005-B-M1M2-native.txt; they are not build authority and are not staged in this job.

## Batch 1: M1-041 and M2-002

- M1-041: restored the Opus-confirmed missing warning and info events in the existing live handlers; self-review reopened their cited addresses to preserve ordering. SendingSyncTime follows SyncTime; the idle callback stores ready before its info log. Failure warning is limited to SyncTime and InitController, preserving short-circuit sends. No wire or timer changes.
- M2-002: the Opus-confirmed PlaceObjectOnGround gate, 30-second timeout and verify-result defects were already repaired by cf36fb5. Added regression through RunAsync for an unlatched action after BlockPlaced, the exact binary32 deadline and result 0x03000018; extended the gate test with the moving bit. Corrected stale unresolved text, without claiming the whole lifecycle recovered.
- CHECKLIST self-review: live mfgId/response, SyncTimeAck and NV-idle entries; success/failure gates; log order; no new parallel component; no float arithmetic changes; timeout expected words from the checked binary; no circular expected constants. Both records remain IMPLEMENTATION_GAP, unresolved begins "built, awaiting strong verification:". The full queue-driven PlaceObject lifecycle remains unverified.
- Targeted tests: 17 passed. Fidelity check and diff whitespace check: passed. Full suite: 3,832 passed, 0 failed, 0 skipped (2 m 50 s). Commit: 327f7fc (pushed to main). Push gate independently passed fidelity and all 3,832 tests (3 m 13 s).

## MISSING items

- MISSING M1-015: its Opus-noted remaining defect is Robot teardown through M1-044 (0x0052F2F6, 0x005110D4, thread join 0x0065257C). The operator explicitly deferred M1-031/044/045 and the only new detailed rows here are this builder's own unchecked extraction. No teardown changes or settlement.
- MISSING M1-024: Opus establishes the single-rate/double/clamp/elapsed-gate contradictions, but its unresolved text and verification report do not supply the precise threshold-walk branch conditions, crossing-time calculation and multiplier association for NeedsState::ApplyDecay 0x0069C4AC..0x0069C4EE. Implementing a plausible piecewise integrator from that summary would violate CODEX-BUILDER rule 2. Needs a checked build row for that routine, including edge/failure behavior. Existing contradicted decay remains visible in the unchanged record.
- MISSING M1-025: E7..E9 establish subscriber ordering and the disconnect/send FIFO effect, but do not establish what UiMessageHandler::OnExitSdkMode does or the predicates and mask by which MovementComponent "may" send EnableAnimTracks. No live ExitSdkMode dispatch exists in this stack. Porting an invented UI callback or unconditional unlock would violate CODEX-BUILDER rule 2. Needs checked bodies for 0x00661538..0x00661566 and 0x0064053A..0x00640570 / 0x0063FE92..0x0063FFB4. Its teardown dependency on M1-044 is also deferred above.
- M2-002 remaining open scope: Delocalize bit 0x2/GetRobotState callers and the queue-driven lifecycle/clock bridge have no checked correction in this trial. Retained explicitly in unresolved; no new behavior invented.

BLOCKED 2026-10-05: batch 1 is committed and pushed; M1-015, M1-024 and M1-025 cannot be completed within the checked-row trial scope. Required manager action: supply checked decay/ExitSdkMode rows and, for teardown, approve the deferred M1-031/044/045 split. No records were settled. No higher-layer job or hardware run was started.

## Resumed claim 2026-10-05

CLAIMED Codex: manager-approved Rows checked at dee950e unblocks decay D1–D18, ExitSdkMode E1–E14, and the cited M1/higher-layer teardown and sleep split. One batch at a time; M1-029 stays deferred. PARTIAL descendant effects/FP environment/nested SDK handlers stay MISSING. No records settle.

## Batch 2: checked Needs decay (M1-024)

- Built manager-adopted D1–D18 on the live NeedsManager/NeedsState path: f32 current-floor integration, inclusive ordered threshold scan, per-pass multipliers in need order, unordered compare branches, min-only final clamp, no elapsed gate and caller-clock advancement.
- D17 invokes the explicit repair collaborator after store/dirty; absent collaborator logs MISSING. Repair mutation/RNG, FP environment/NaN payloads and exception catch disposition remain MISSING/UNKNOWN. These were not built.
- CHECKLIST self-review: existing live entry, tick ordering retained, separate binary32 multiply/subtract, no FMA/double integration, source-derived bit oracles and failure-edge gates; tests include the live engine tick. Required read-only cozmo-verifier: PASS, no circular tests or blocking defects; no settlement.
- Focused/regression tests: 104 passed. fidelity --check and diff --check pass. Full suite: 3,846 passed, zero failed/skipped (3 m 34 s). M1-024 remains IMPLEMENTATION_GAP with built/awaiting-strong-verification text. Commit follows this entry; its hash will be logged after push.

- Batch 2 commit: 8c266d7 (pushed); independent push gate passed all 3,846 tests (3 m 40 s).

## Batch 3: checked ExitSdkMode (M1-025)

- Built the live game-drain entry and ordered UI, MessageHandler and per-Robot Movement subscribers from manager-adopted E1–E14. Edu gates unpause, external gates disconnect, charger gates unlock; SDK status flags and the communication byte remain distinct. Preserved synchronous reset-message handoffs and unchanged-connection callbacks.
- Fixed track multiset semantics needed by this entry: retain duplicates, erase one owner, inspect resulting size even when the owner is absent; existing MA3 sends one combined empty-track mask. No recipient or telemetry payload was guessed.
- MISSING: EnterSdkMode writers, concrete SDK connections, nested SDK recipients/telemetry fields, PrintLockState. Existing removal/teardown correction follows in the next batch. All records stay IMPLEMENTATION_GAP.
- CHECKLIST self-review and required read-only verifier: PASS after removing an unsupported duration-state mutation and its assertion. Focused regression: 101 passed. fidelity --check and diff --check passed. Full-suite result and commit follow below.
- Full suite: 3,856 passed, zero failed/skipped (3 m 8 s). Commit follows; hash/push result logged in the next entry.

- Batch 3 commit: 0ea935d (pushed). Independent push gate: 3,856 passed, zero failed/skipped (3 m 35 s).

## Batch 4: checked M1 teardown/sleep boundaries and ownership split

- Built M1-015/M1-044 T2–T9 on the existing live RemoveRobot path: membership lookup distinguishes absent entry from stored null; chosen external/report/session-clear path precedes Needs, Perf and DAS(0); nullable Robot lifetime and storage release finish before map/first-id/RIC erase, then $phys/$group clears. The first matching id removal preserves survivors. No reconnect added.
- RobotLifetime is constructed with each EngineRobot and called from live removal. Its checked T6–T8 ledger preserves event/ForceUpdate/AbortAll order, owner null stores, release ordering, normal repeated-owner skips and final base/member handoff. The existing ActionList is cleared, nulled and disposed in source order; Mood sees an already-null queue. Missing owners remain UNKNOWN with explicit MISSING diagnostics, distinct from an explicitly known null owner.
- Needs disconnect now enters through T4, before destruction, instead of the later host RobotRemoved event. Recipient persistence/telemetry remains higher-owned. Retained managed ResetDevices/reference cleanup is an unverified higher-layer candidate, not native destructor proof.
- M1-031 timing/trigger is retained. M1-045 now hands a required higher-layer factory result to the existing ActionList, position NOW=0/retries=0, ignoring the queue return. Missing factory remains MISSING; the diagnostic event runs only on that missing path. No sleep tree or recursive teardown body was built.
- Correction A2 quotes the previous M1 records before narrowing and gives 18 explicit obligations their own citations: M1-046; M3-038; M4-026/027/028; M5-037/038; M7-023; M8-015/016/017; M10-014; M11-055; M12-040; M13-029; M14-013; M15-027/028. All new records are RECOVERABLE_GAP because recipient effects remain incomplete. Existing build records stay IMPLEMENTATION_GAP; none settled.
- Authorized cited corrections approved with fidelity.py --approve M1-transport and the affected higher inventories. Their premature source_investigation_exhausted=true flags were quoted and changed to false to expose the newly named PARTIAL gaps. Policy/hardware records and PROJECT_STATE unchanged.
- MISSING retained: all unbound lifetime/service/external/telemetry recipients and recursive effects; concrete channel unsubscription; higher sleep factory/children; SDK nested handlers/connection writers/telemetry/PrintLockState; decay repair mutation/RNG and FP/exception environment. M1-029 remains explicitly deferred. No higher-layer implementation or hardware acceptance started.
- CHECKLIST self-review and required read-only verifier: PASS; no blocking defects/circular tests. Focused regression: 24 passed. fidelity --check and diff --check pass. Full suite/commit/push result follow below.
- First full suite found one obsolete Freeplay test that invoked the late host reset callback. Replaced it with the live DisconnectCurrent entry and checked Needs persistence before the destructor event. Affected-test verifier PASS; five targeted tests passed. Rerunning the full suite after this test correction.

- Corrected full suite: 3,860 passed, zero failed/skipped (2 m 59 s). Commit follows; hash and push gate will be logged after publication.

- Batch 4 commit: f9857c0 (pushed to main). Independent push gate passed fidelity and all 3,860 tests, zero failed/skipped (3 m 25 s).

DONE 2026-10-05: the manager-adopted checked-row builder scope is built, self-reviewed, independently verified, fully tested, committed and pushed in batches 8c266d7 (M1-024), 0ea935d (M1-025), and f9857c0 (M1-015/031/044/045 boundaries and cited ownership correction). Batch 1 remains 327f7fc. No record settled; manager/Opus verification and layer acceptance remain separate. The PARTIAL recipient/FP/recursive items listed above stay MISSING, and M1-029 remains deferred. No further job started.

The separate ADP-1 research task is saved locally at re-analysis/research/20261005-adp1-triage.md, outside these build commits under the research-lane rules. It covers all 48 non-EXACT_SOURCE M6/M9 records plus C30–C35 and the previous check's remaining items; mixed coefficient-design/matrix boundaries are VERIFY, with DROP restricted to named pure PCM arithmetic.
