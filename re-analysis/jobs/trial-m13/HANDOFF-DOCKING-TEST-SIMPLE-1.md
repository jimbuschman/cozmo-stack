# DOCKING-TEST-SIMPLE-1 — verification in progress

The historical standalone driver exposes a custom MountCharger adapter under the DockingTestSimple label. Its XML incorrectly describes the native developer test, and starting it lacks a visible boundary diagnostic. This job corrects that description and diagnostic; it does not implement the original native state machine.

## Evidence and scope

- Native ID9/class9: enum-after-string0x0076B632..0x0076B664 and0x0076A430..0x0076A462. Factory entry0x0059CA78..0x0059CA92 allocates0x290 and calls native ctor0x005CAFB0. Exact shipped devBehaviors/dockingTestSimple.json has only class and ID.
- ExecuteBehaviorByID handler0x0060AA6A..0x0060AA88 accepts a payload ID through manager lookup. This prefix has no SDK guard. Existing M7-022's supported-entry exclusion is a recorded manager decision, not a newly proved instruction-level gate. Cited app files/global caller negatives were unavailable for fresh certification; config-reference absence alone proves no exclusion.
- Canonical NativeBehaviorRuntime/CreateNative binds configured factory outputs and has no DockingTestSimple constructor. Historical FreeplayStack.Create/CreateStandalone instead includes ShippedBehaviors.Navigation's MountCharger adapter. Public standalone manager/chooser calls can select that legacy label, so the old blanket statement that no arbitrary developer-ID API exists needs this qualification.
- Finite inactive D3 callback0x005CB368..0x005CB382 changes owner counters/flag only. Its construction/subscription and logger ownership remain separate M7-027/M7-028 live gaps. This does not settle those records or the original state loop.

Corrected source C44C481A8F1F04E399169736B6393AB4CB6EEC6D7048847C3486D710F6060590; independent bounded PASS7F914A46C7B0D2F5057B30179D71F2E0586968E6C1190091EE1E6272EF0FCB9F. All six whole current record objects match the manifest. One source-report instruction citation correction, semantics unchanged; initial report retained in ignored scratch.

## Records and remaining uncertainty

No new record, widening or settlement proposed. Retain M7-022 RECOVERABLE_GAP/live_pathfalse and its existing decision; no new policy, exclusion or unsupported API decision. Formal reachability wording qualification is for Claude. Original Init/Update/callback/random/action/report/cleanup, PrintStats0x005CD180..0x005CD6DB and RecordAttempt0x005CE3E0..0x005CEAB3 remain unread frontiers. Existing M1-034 points remain recorded, not claimed implemented. M7-018/027/028 and M8-013/M15-002 retain their wider gaps. No behavior finished or hardware claim.

## Change and verification

The released four-file scope is ChargerBehaviors.cs XML and one visible M7-022/custom-adapter diagnostic before the existing lock/action, Behaviors.cs Navigation documentation, a bounded regression file and builder ledger. Public type/IDs/class, standalone admission, runnable predicate, action arguments/order/results and Finish are preserved. Tests must identify custom-adapter coverage, not native conformance. The traced custom path is Navigation entry → OnStart diagnostic → unchanged reaction lock and MountChargerAction → completion queue → Update result/Finish. Two bounded custom/unit regressions verify diagnostic ordering and existing missing-charger BadObject completion, plus canonical factory non-substitution. Narrow M13-008 Init0x0054E114/0x0054E140/0x0054E166..0x0054E168 independently establishes0x03000004; no full M13-008 settlement is claimed.

| Worker | Requested model / effort |
| --- | --- |
| docking_simple_read | gpt-6.1-sol / medium |
| docking_simple_build | DEFAULT / DEFAULT; overrides omitted |
| docking_simple_review | DEFAULT / DEFAULT; overrides omitted |

No worker switches or backend inference. Source-report citation corrections1; reviewer-required metadata/test-plan batch1 (combined filter initially omitted the new class); compiler-origin fix1 (missing Cozmo.Robot.Vision test import). Behavioral/self-test corrections0. Builder newly raised MISSING/source questions0 beyond inherited frontiers. Research/inspection tooling attempts are preserved separately. No official status, approval, inventory, PROJECT_STATE, REVIEW or hardware changes.

Failed first build exit1/2260warnings/one CS0103 retained; no tests launched. One-line import independently checked before corrected build2 exit0/570warnings/zeroerrors. FIRST focused2/2 and FIRST four-class combined128/128 PASS0skip; independent ONE exactcombined128/128 PASS0skip, all full names/outcomes/multiplicities match directly. Actual class counts boundary2/Navigation47/ActionDiagnostic10/Freeplay69. Exact4 raw/staged-equivalent/UTF8LF/noBOM files and133compiled artifacts stable pre/post; only final ledger outcome append differed from pre-test freeze. Actual shutdown0/native+own arrays/counts0 before integration.

Workerda30716711810d0742c83e3a73ef9874fde9a418 integrated1322c412; entire code tree6c3c03dbfea8770035ba13d9adb04be55f55ebb9 matches. Code freeze260B0E653C6790959C83B8BCE9017789EAAFE834176069FD5DB9769E20B91D3A; final independent611F1737CBDA6B45FF585647502FC34C73D2664A60993327971472627EFBF0A5. Current720records, fidelity/queue/protected checks passed. Root full gate next; no full result claimed yet.
