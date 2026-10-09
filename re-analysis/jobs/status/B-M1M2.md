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


## Resumed M1-029 claim (2026-10-05)

CLAIMED Codex: pulled; manager-adopted item 6 and Item 6 corrections in the rows-check are build authority, with corrections winning. Port the known firmware byte reader; exception destination stays MISSING. Additional checked limitation: exact real conversion/locale at 0x008E22C8/0x008E22F8 is UNVERIFIABLE and cannot authorize .NET parsing. This path will remain MISSING pending checked num_get/rounding/locale rows. No record settles; research ADP-1 file remains outside build commits.

## Batch 5: M1-029 checked byte reader (partial)

- The existing firmware production reader is replaced, not duplicated: FirmwareHeader.Parse/LoadAsync, RobotInitialConnection.HandleFirmwareVersion and EngineRobot.HandleFirmwareVersion now enter FirmwareJson.cs; System.Text.Json is removed from this path. Header bounds/thread/callback ordering remain in the existing checked entries.
- Built checked Item 6 grammar and Item 6 corrections: comments, four-byte whitespace set, literal/root suffixes, empty-last-key object close, array empty peek and separator gates, raw byte strings, Unicode surrogate quirks, duplicate replacement, partial mutation, root-inclusive >1000 depth throw, and sign-specific integer widths with positive INT32_MAX typing cutoff. Byte FACTORY/F comparisons avoid Unicode replacement decoding.
- MISSING: exact real conversion uses shipped libc++ num_get/current locale at 0x008E22C8/0x008E22F8 (including decimal/exponent/overflow); no .NET conversion substituted. It throws explicit JsonMissingSource. Real asString formatting also remains MISSING. Typed real asUInt tests validate the already-checked binary64 range/truncation independently of this unrecovered parsing.
- MISSING: final exception destination. Parse propagates typed depth/access errors instead of converting them to parse-false. Existing Isolated/LoadAsync host containment remains an unbuilt candidate and is explicitly labeled; no guessed native terminate/catch route implemented.
- M1-029 remains IMPLEMENTATION_GAP; unresolved starts "built, awaiting strong verification:" and states the partial scope. No frozen evidence/status changed, no record settled.
- CHECKLIST self-review: both live signature handlers/header entry, byte gates/order/failures, integer ranges, Unicode output, partial-state ownership and source depth checked against adopted rows. At least one test uses the real firmware handshake; expected bytes/types/depth/real bits come from source. Required read-only verifier: PASS for the checked subset, no blocking defects/circular tests. Focused EngineAppLayer regression: 137 passed. Full suite, fidelity gate and publication results follow.
- Final self-review/verifier follow-up: PASS for the checked subset, no blocking defects or circular tests. fidelity --check and diff --check pass. Full suite: 3,928 passed, zero failed/skipped (2 m 26 s).

BLOCKED 2026-10-05: the checked byte-reader subset is built; M1-029 is not complete. Needed checked rows: shipped real-number num_get/decimal rounding/overflow/current process locale (0x008E22C8/0x008E22F8), plus real asString formatting. Final exception destination stays MISSING as explicitly required. No research or higher-layer build started. Publication hash and gate result follow after push.
- Batch 5 commit: 2167663 (pushed to main). Independent push gate passed fidelity and all 3,928 tests, zero failed/skipped (2 m 40 s). Checked subset only; BLOCKED/MISSING details above remain authoritative. No settlement or hardware acceptance.


## Batch 6 claim: M1-029 real conversion (2026-10-05)

CLAIMED Codex: pulled/rebased. Operator adopts converter build rows and fixes nearest rounding / C decimal point as EQUIVALENT_IMPLEMENTATION runtime assumptions. Build the row-faithful converter; shipped-emulator fixtures provide expected bits. Formatting and exception destination stay MISSING. No record settles.

- Built FirmwareJsonDouble S1-S21/W1 and wired Reader.DecodeNumber. Preserved binary unadjusted normalized ratio (0x7EE9E), ordered binary64 arithmetic and source failure gates. Runtime nearest/C-locale assumptions recorded as EQUIVALENT_IMPLEMENTATION in M1-029 unresolved; shipped conversion remains source-faithful and record stays IMPLEMENTATION_GAP.
- Checked-in expected bits/success are copied from the shipped-converter emulator corpus, with hashes and fixture provenance. All 37,771 decimal fixtures across eight categories pass, plus firmware Reader and Header.Parse production-entry tests and numeric overflow fallback fixtures. Expected values never come from C#.
- .gitignore excludes research .so builds, qemu executable, local dependency folders and downloaded lineage inputs. No unrelated research files staged.
- CHECKLIST self-review: production entry, integer-to-real fallback, errno/end gates, underflow/overflow, sign-zero, exact tables/width/order checked. Required read-only verifier PASS: native correction gates/order checked; independently compared every fixture to committed emulator output, no defects or circular tests. Manifest test names updated.
- MISSING retained: real asString formatting, final escaping exception destination, allocation-failure/extreme-length effects. No settlement, no hardware. Full-suite/publication results follow.

- Full suite: 3,938 passed, zero failed/skipped (3 m 32 s). Fidelity and staged diff checks pass. Required verifier PASS. Publication on 2026-10-06 follows; requested real-conversion scope DONE, whole M1-029 remains incomplete due to the documented MISSINGs.

- Batch 6 commit: d90e4c0 (pushed to main on 2026-10-06). Independent push gate passed fidelity and all 3,938 tests, zero failed/skipped (3 m 37 s).

DONE 2026-10-06 for the requested real-conversion batch. The row-faithful port and shipped-emulator regression fixtures are published; round-to-nearest and C decimal point are explicitly EQUIVALENT_IMPLEMENTATION runtime assumptions in M1-029 unresolved. Research binaries/dependencies/lineage inputs are ignored. M1-029 remains IMPLEMENTATION_GAP, built awaiting strong verification; formatting, escaping exception destination, allocation-failure and extreme-length effects stay MISSING. No settlement or hardware acceptance. Stop here as directed.

## Manager follow-up claim (2026-10-07)

CLAIMED Codex after Q13: confirm E3 stored f32 time and nonzero deadline gate, check T4 reorder against primary instructions, document external snprintf and retained M1-034 catches, make CORE003 signal-driven. Leave higher-layer recipients untouched. No records settle.

- Confirmed E3 recipient instructions at 0x00695EAA..0x00695F6A: stored binary32 tick, f32 pause duration/stores, NE-gated deadline/start update. Corrected the existing NeedsManager.SetPaused; six shipped-emulator fragment fixtures pass through ExitSdkMode. No new production binding.
- T4 review reorder rejected by primary code: WriteToDevice at 0x0069592A before robot-pointer clear at 0x00695932; retained production order and added an observation assertion. Higher-layer recipients unchanged.
- Real formatter core is undefined snprintf from phone libc; all 28 shipped libraries checked. Wrapper remains shipped and formatting build remains MISSING. No formatter port started.
- M1-029 unresolved now explicitly cites the M1-034 operator-approved catch/log departure. Catches unchanged; statuses/evidence/inventories unchanged; derived FIDELITY_GAPS regenerated.
- CORE003 now uses dedicated workers and explicit cancellation-start signal with finally-release, preserving its assertions. Targeted tests 94 passed; final full suite 3,952 passed, zero failed/skipped (DOTNET_PROCESSOR_COUNT=4). Fidelity and diff checks pass. CHECKLIST self-review completed; report/native evidence under research/20261007-M1-manager-decisions.md and -native.txt.
- Requested scope DONE pending publication. Existing missing higher-layer recipients, formatting wrapper/runtime policy, allocation/extreme-input and original exception-disposition uncertainty remain; no settlement or hardware run.

- Published a61d80dc16734e82622aaed58389a157bf9ea90f to main on 2026-10-07. Independent pre-push gate passed fidelity and all 3,952 tests, zero failed/skipped (4 m 35 s), with DOTNET_PROCESSOR_COUNT=4 and the SDK-supported ThreadPoolMinThreads=32 test-host setting. No test was disabled or assertion weakened. Earlier gate attempts hit unrelated navigation timeout (passed alone) and CORE006 under the single-processor setting; the unchanged commit passed after restoring concurrency and raising the worker minimum.
- DONE and published for this manager follow-up. Q10-Q13 are already published; their documented PARTIAL coverage and MISSING items remain. Stop after logging; no later queue started.
## Inventory correction (2026-10-09)

- Applied the manager and policy dispositions from `jobs/B-M1M2.md` as one cited inventory correction. Added the four M1 records (S13, E6, E7, P2), created/updated higher-layer recipients for transfers, rewrote transferred residuals, and narrowed the five policy records to their engine-owned decisions.
- Re-approved all 13 touched subsystems. Follow-up source-boundary review narrowed M3-040 to NV idle callback scheduling/predicate/invocation (M1-041 retains the ready-byte writer) and M1-042 to Unity A1/A3/A4 producers/gates (M1-026 retains A2 engine conversion/send).
- `fidelity.py --check`: passed, 465 records. `git diff --check`: passed. Full suite: 3,954 passed, zero failed/skipped. No records settled.
- Published inventory correction: `3d2018c`; manager row/ownership correction: `5456b3f`.

## Manager row check (2026-10-09)

The manager reopened the native call targets and extracted APK sources for M1-029 J1/J7, M1-046 Q1-Q3, M1-050 S13, M1-051 E6, M1-052 E7, M1-053 P2, and the narrowed policy records before any new implementation. Detailed checked boundaries and UNKNOWNs are in `jobs/B-M1M2.md` and the M1/M7 inventory appendices. M1-052's concrete callback target is unknown and cannot be implemented generically; M1-050's external sink, M1-051's +0xE1 writers/callbacks, M1-046's unsubscribe descendants, and M1-053's publication sink remain gaps. The ownership split between M1-023 reset-flag setter, M1-048 consumer, and M1-049 Android/JNI source is corrected and both affected subsystems re-approved. Full suite and publication follow.

## Batch 7: M1-029 checked J1/J7 subset

- Implemented finite-real `Json::Value::asString` through the existing live accessor: precision-17 general formatting under the approved phone `snprintf` runtime assumption, then the checked `.0` gate and comma rewrite. Added source-shaped finite formatting cases.
- Added a maximum reachable u16-length Reader case (65,535 bytes) using leading zeroes, with expected integer value from the checked integer parse behavior.
- Reopened wrapper and Reader/converter call targets from the shipped engine before the change. M1-029 remains IMPLEMENTATION_GAP; non-finite `useSpecialFloats=0` mapping, allocator failure/extreme-input effects, and final escaping exception destination remain MISSING. No status settlement.
- Focused tests: 5 passed. `fidelity.py --check` was refreshed after regenerating `FIDELITY_GAPS.md`; full-suite and publication results follow.
- `fidelity.py --check` and `git diff --check`: passed. Full suite: 3,959 passed, zero failed/skipped. M1-029 remains IMPLEMENTATION_GAP with the above MISSING boundaries.

## Batch 8: M1-048 UDP decision ownership

- Rechecked the current UDP production path against reopened U1-U7 targets. Its existing code implements the manager-checked ordering and gates: open/close bookkeeping, bind-in-use handling, send accounting/rate limit, receive drain/error choices, and pending-reset consumer. Host syscall/resolver and Windows errno mappings remain M1-022/M1-039 policy; reset signal remains M1-023.
- Added M1-048 fidelity ownership to those existing production methods and listed their source-shaped regression cases in the record. No socket behavior changed. M1-048 remains IMPLEMENTATION_GAP, built awaiting strong verification.
- `fidelity.py --check` and `git diff --check` passed; full-suite and publication results follow.
- Full suite: 3,959 passed, zero failed/skipped. M1-048 remains IMPLEMENTATION_GAP; no records settled.

## Stop point (2026-10-09): remaining M1 items blocked on missing path details

The requested CODEX-BUILDER remainder is not complete. The checked buildable slices were committed and pushed above. Do not fill these gaps with a generic callback, host substitute, or invented configuration:

- **M1-029 J1:** finite-real wrapper and J7 maximum u16-length input are built in `21b8738`; non-finite `useSpecialFloats=0` output mapping, allocation-failure/extreme-input behavior, and final escaping exception destination remain MISSING. Record remains IMPLEMENTATION_GAP.
- **M1-046 Q1-Q3:** owner/member retirement sequence is present in `RobotLifetime`, but the shared-handle vector's concrete unsubscribe target and descendant effects are UNKNOWN. No equivalent Messaging owner exists to safely bind; record remains RECOVERABLE_GAP.
- **M1-047:** H1/H2 decisions were checked, but the selected constructor priority/call-site value is not in the checked rows. Actual scheduler range, permissions and effect are external M1-014 policy. Do not invent a default priority.
- **M1-049:** WifiUtil bind/unbind and JNI gates are source-checked, but this checkout has no Android/JNI bridge to receive these callbacks. Its Windows address-change event remains the host policy seam; no fake Android gate was wired into the live path.
- **M1-050 S13:** external slot +0x30 is pure virtual in the shipped base and no runtime target was found; sink and report payload remain UNKNOWN.
- **M1-051 E6:** local SDK writers are identified, but +0xE1 writer census and callback descendants are UNKNOWN; do not substitute mode booleans or infer the callback tail.
- **M1-052 E7:** the virtual +0x24 callback target/body is UNKNOWN; no generic transport callback can be built.
- **M1-053 P2:** UpdateAllRobots gate/projection/publication call are checked, but the game sink and exhaustive caller set remain UNKNOWN. The non-live docking-test getter is M7-022.
- **M1-042:** Unity producers and the `!FirstTimeUserFlow` gate are source-checked, but this checkout has no Unity connection-flow/profile producer; stored-volume and profile inputs cannot be assigned a live value here. M1-026 still owns engine conversion/send.

M1-048 U1-U7 is present in the existing live UDP path and was assigned to its own record in `d42130f`, awaiting strong verification. M1-023/M1-048/M1-049 ownership boundaries are documented in the approved inventory. No additional record is settled. No hardware run was started.
- Stop-point check: full suite passed, 3,959 passed, zero failed/skipped; fidelity check passed. No code changed in this status-only checkpoint.

## App-boundary build: M1-050 (2026-10-09)

- Pulled the operator's app-boundary decision (`2f2d6e1`). The previous Unity/Android/SDK blockers above are superseded by that decision; M1-042/-049/-051/-052 are now policy records.
- Reopened RemoveRobot's external call at `0x0052F29E..0x0052F2AC`: the argument is the robot id; the call precedes the separate RobotDisconnected notification and session clear. Built public `CozmoEngine.RobotDisconnectReported` on this existing live path. The membership and pending-handshake gates are unchanged. API subscriber exceptions use the approved M1-034 isolation.
- Updated existing live removal order and handshake tests to use the public event; added subscriber isolation, unsubscribe and no-subscriber coverage. M1-050 stays IMPLEMENTATION_GAP, built awaiting strong verification. No hardware run.
- CHECKLIST review: source entry/gates/order and uint payload match S13; no copied removal path or guessed app recipient. Fidelity and whitespace checks passed. Full-suite result and publication are recorded in the next status checkpoint.
- **MISSING M1-053:** checked P2 describes the getter/publication order but does not enumerate the outgoing payload's component-derived fields or resolve the getter's direct/virtual targets. Reopening exposes a distinct ExternalInterface.RobotState; forwarding the incoming Protocol.RobotState would be unsupported. Detailed projection rows are being prepared for the manager check required by CODEX-BUILDER rule 2. No partial outgoing event path was installed.
- Full solution suite: 3,960 passed, zero failed/skipped (TRX: .scratch/m1-api-events/m1-api-events.trx). Fidelity and whitespace checks passed.

## Final extraction handoff (2026-10-09)

- M1-050 implementation commit: `75bb3df` (publication result follows).
- Submitted unchecked U1–U8 / P1–P4 in `research/20261009-M1-final-extraction.md`, with fresh native instructions, named relocation identities and vtable-slot addends in the companion native file. M1-046 Messaging unsubscribe is `0x0051D8F8`, Idle through shipped UiMessageHandler is `0x00664EB8`; unlink/callback destruction/deleting targets were reopened. M1-047's actual transport priority is integer **3**, selected at **0x008367C0**, passed through Dispatch::Create into TaskExecutor, then requested on both threads.
- Submitted additional M1-053 P2a–P2m in `research/20261009-M1-053-projection-rows.md` and companion native file. The complete outgoing schema, projection field addresses and direct/virtual getter targets are recovered. Corrected the older unnamed-PLT GOT annotations. These are new extraction rows, not checked implementation authority.
- **Waiting for manager check:** M1-046/-047 rows; M1-053 detailed projection rows and supplier ownership. M1-053's engine root pose, tracked-object ID and last processed image timestamp do not have complete checked live supplier paths. The existing image timestamp stand-in cannot be used. CODEX-BUILDER rule 2 forbids building these newly extracted rows in the same work; the manager must check them and scope the remaining supplier obligations. No guessed outgoing payload or scheduler request was installed.
- Existing M1-029 open MISSING remains outside this requested build batch. No fidelity record was settled and no hardware acceptance was started.

- Publication checks: initial pre-commit suite passed 3,960. First push gate failed the unchanged NavigationTests.PopAWheelieRetriesWithTheRetryAnimationWhenTheDockFails (retry count 0 instead of 1); isolated recheck passed. A four-processor gate retry failed unchanged RFixBatch2Tests.M4_019_M13_017_TheBehaviourStopsRedrivingOnceTheEngineTickClearsThePlatformFlag. Full gate is retried using DOTNET_PROCESSOR_COUNT=4 and SDK test-host ThreadPoolMinThreads=32; no test/assertion changes or hook bypass.
- The worker-minimum gate retry failed unchanged M12RFix2StreamATests.M12_007_AStillMovingObjectKeepsVerifyRunningUntilTheAllowanceThenFails with its existing 8-second condition timeout. The implementation commit remains locally committed; publication has not yet succeeded. Extraction reports and packet are prepared; do not bypass the push gate.

- Published M1-050 `75bb3df` and extraction handoff `4aaf1ab` to main. Final pre-push gate passed fidelity and all 3,960 tests, zero failed/skipped, with DOTNET_PROCESSOR_COUNT=4 and SDK ThreadPoolMinThreads=32. Earlier test failures above are retained.

## M1 verification packet (2026-10-09)

- Prepared `research/20261009-M1-verify-packet/` from implementation snapshot `75bb3df`: 48 current record files, five explicit exclusions (M1-029/-033/-046/-047/-053), verbatim manifests and checked rows, exact implementation histories, native instruction/byte transcripts and recorded full-suite TRX. No verdict or settlement.
- Packet transcription checks: 48 manifests, 1,333 source lines, 131 exact Git histories, 248,105 native instruction/data rows, 1,953 cited intervals; zero errors or unmatched declared test names.
- M1-046/-047 final rows and M1-053 detailed rows/supplier ownership await manager check. M1-053 was not built from unchecked new extraction. Existing M1-029 MISSING remains visible. No hardware run.
- Non-behavioral extraction transcript trailing whitespace cleaned; native instruction text and bytes are unchanged. Packet Git patch transcripts preserve historical whitespace, including blank context lines; stripping it would invalidate the exact-history requirement. Packet transcription validation checks those histories verbatim. Whitespace checks apply to the non-packet edits.
