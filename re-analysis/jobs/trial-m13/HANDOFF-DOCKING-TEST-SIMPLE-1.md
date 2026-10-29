# DOCKING-TEST-SIMPLE-1 — READY FOR CLAUDE

The historical standalone driver exposes a custom MountCharger adapter under the DockingTestSimple label. This job corrects its misleading documentation and adds a visible M7-022 boundary diagnostic before the unchanged lock/action sequence. It does not implement the native developer state machine. After merging main077c83f5, the requested full gate passed **6723/6723, zero failed/skipped**.

## Path, evidence and exact change

- Historical FreeplayStack.Create/CreateStandalone → ShippedBehaviors.Navigation → MountChargerBehavior.OnStart → diagnostic → existing reaction lock/MountChargerAction → completion queue → Update result/Finish. Public type/default ID/class, admission, runnable predicate, action arguments/order/results are preserved. ChargerBehaviors.cs changes XML and one ReportMissing call; Behaviors.cs changes Navigation XML. Two new custom/unit tests and the builder ledger complete the four-file scope.
- Native ID9/class9: enum-after-string0x0076B632..0x0076B664 and0x0076A430..0x0076A462; factory0x0059CA78..0x0059CA92 calls ctor0x005CAFB0. Canonical NativeBehaviorRuntime/CreateNative has no constructor for this body.
- ExecuteBehaviorByID0x0060AA6A..0x0060AA88 has no local SDK guard. Existing M7-022's non-live manager ruling is retained as a scope decision; unavailable app/global caller negatives were not freshly certified. Its blanket no-arbitrary-API wording needs the historical standalone caveat above. No new exclusion or policy.
- Finite inactive D3 callback0x005CB368..0x005CB382 changes owner counters/flag only; its constructor/subscriptions and logger stay separate M7-027/M7-028 live gaps. The diagnostic tests do not certify native behavior. The missing-charger unit's BadObject0x03000004 oracle is separately checked at0x0054E114/0x0054E140/0x0054E166..0x0054E168; no whole M13-008 claim.

Checked source C44C481A8F1F04E399169736B6393AB4CB6EEC6D7048847C3486D710F6060590; source check7F914A46C7B0D2F5057B30179D71F2E0586968E6C1190091EE1E6272EF0FCB9F. All six whole source-record snapshots still match current. Workerda30716711810d0742c83e3a73ef9874fde9a418 integrated1322c412. Final code freeze260B0E653C6790959C83B8BCE9017789EAAFE834176069FD5DB9769E20B91D3A; independent final611F1737CBDA6B45FF585647502FC34C73D2664A60993327971472627EFBF0A5.

## Checks and retained failure

Corrected build passed; focused2/2 and affected128/128 PASS0skip. Independent one exact128/128 PASS0skip matched full names/outcomes/multiplicities (Boundary2/Navigation47/ActionDiagnostic10/Freeplay69); four files and133compiled artifacts stayed stable, shutdown/native arrays0. Only ledger outcomes differed from its pre-test freeze.

The first full gate failed6710P1F/6711 on efee57f5, with the captured M11-066 unrelated-pose-root exception in FlipBlockAction.CheckIfDoneTick. Its complete trace and gate metadata remain committed. Main4ed2f340 fixed the real implementation bug: the engine ignores GetWithRespectTo's result at0x0055F0E0 and reads the zero local. That checked fix and the WwiseSongTests race fix were imported verbatim with main077c83f5; no local speculative fix or acceptance retry.

Requested full rerun on c8395b0c3968c48111e00058a12c39a1f4bbc943: **6723/6723 PASS0skip**,5m38s, nativeexit0. UTC2026-10-29T18:24:48.9035741Z..18:31:42.6653680Z; fingerprintd1ce10b481296ad4470ec1839a840c8b24906f27. Actual postgate native/own arrays/counts0; original four frozen files unchanged. gate-result.json preserves both attempts.

## Records, models and uncertainty

No new record, widening or settlement proposed. M7-022 stays RECOVERABLE_GAP/live_pathfalse; its original Init/Update/callback/random/action/report/cleanup/PrintStats/RecordAttempt and existing M1-034 frontier remain unrecovered. M7-018/027/028, M8-013/M15-002 and whole M13-008 retain their residuals. Formal M7-022 wording correction is left to Claude. No native body, hardware or finished-behavior claim.

| Worker | Requested model / effort |
| --- | --- |
| docking_simple_read | gpt-6.1-sol / medium |
| docking_simple_build | DEFAULT / DEFAULT; overrides omitted |
| docking_simple_review | DEFAULT / DEFAULT; overrides omitted |

No worker switches/backend inference. Source citation correction1; reviewer-required plan round1 (new class omitted from combined filter); compiler correction1 (missing Vision import, first build1error/2260warnings retained and no tests launched); behavioral/self-test corrections0. Builder new MISSING/source questions0 beyond recorded frontiers. Research/tooling attempts are separately preserved. No official record/inventory/approval/PROJECT_STATE/REVIEW changes locally. Next ACKNOWLEDGE-OBJECT-1, using its checked read and operator decisions.
