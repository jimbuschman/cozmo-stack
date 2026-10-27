# PICKUP-STACK-1 builder

Candidate based on approved read commit4f57da8e, isolated codex/pickup-stack-1. Actual model/effort/service telemetry unobservable. No official records, acceptance state, REVIEW, Claude files, hardware, full suite, commit or push changed/run by builder.

## Built boundary

| Changed files | Checked production behavior |
| --- | --- |
| NativeVisionRunners; NativeBehaviorRuntime; NativeConfigurationGeometry | Real runtime-owner visual factory copies raised ROOT target+ROOT dimensions/half-size/ignoreID. Owned Visual30/5→parallel TurnPose2D/5+existing lift preset3(-1),tol5; separate initially unstarted Wait32. Source569014/5695BE/569724,551FE6..5520BA. Success consumes positioning transition tick; every wait update is followed by native closest query; match0300001D overrides any wait result. Query held under existing world pose mutation gate. |
| Motion; ChargerActions; PanAndTiltActions | Actual unqueued MoveAction children, no duplicate motor queue; Turn28/4 core borrows its wrapper's CURRENT tag at Init, public constructor still owns its own tag, embedded compound retains separate tag. Actual MAIN yaw instead of History on native route, real calibrated Motion head, actual Vision retained calibration (API and NV alias). PanTilt14/5 ctor54962C; actual-pose TurnPose overrides2D54B3D8; embedded56 at549D48/569668; all five interrupt slots→52B0B2 false. Normal head9C1/9D0 reuse has no extra observable effect: settled M4-016 C11.3 A8 alwayszero, consumer5485F8 gated by A8. |
| VisionSystem; PanAndTiltActions | Radians GT/LE and repeated-original GT(min)/LT(max) clamp54AA70..54ACBA; no extra output rescale. Native helper requires nonnull live MAIN; neck always parented to that actual owner(50FE9C/50FF66/50FF6A,51838E), not input.parent. Camera(17.52f,0,-8f), checked pose/scalar order518344..620. Null calibration result1+engine error/fallback; bad roots06000000. k25 convergence writes angle BEFORE result1;25 nonconverged returns0 without output assignment. |
| NativeVisionRunners | Actual retained local43 subscription, strict timestamp/mode/count/no dedup. Replacement creates new subscription before releasing old; success/dtor release; direct Cancel only changes existing base state. Visual Prep(position),Prep(wait),delete wait before position; owned pose disposed, Turn core eye/ack cleanup, parent compound/MoveAction teardown reused. Sources54CB68/54CC1C/54DD88,569460..950E,54D0A4..D6,54D18C..1FC. |

Public signatures/adapters retained. Existing public static ComputeHead helper stays a separately labelled M11-066 flattened-input adapter/gap: only both legacy+both parentless may reuse its existing WithRespectTo fallback/log. It never enters the required-owner native overload or enables native fallback. Private scalar implementation is shared; native entry explicitly disables legacy fallback. No new compatibility policy or whole pose-math claim.

New PickupStackNativeVisionTests: actual ActionList motor wire/ack/status, real runtime factory, actual camera NV install and API calibration, real worker→result drain→processed43 completion, transition tick, while-wait match override, mode/timestamp/repeated-message boundaries, Cancel versus destructor, current-tag reservation release, live MAIN move/reInit, unrelated roots; supplied ComputeHead-output unit tests independently assert epsilon clamp limits. Existing assertions/tests unchanged. No fake frames/executor/History Task bridge/CompoundUpdate is used in the native path; gray images, known located targets and robot states are explicit fixture inputs. Existing P1 configured guard tests remain separate from queued finite-child proof.

## Actual run ledger

Explicit `C:/Program Files/dotnet/dotnet.exe`, real Python313 prepended PATH, UTF8 file I/O/output. Workdir isolated builder; artifacts `.scratch/pickup-stack-1/<stem>.log` and completed-test `<stem>.trx`. Test project `cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj`.

- Initial `git merge --ff-only4f57da8e` default sandbox EXIT128 denied ORIG_HEAD.lock; approved escalated identical FF EXIT0. No overwrite/reset/rebase.
- Default asset-junction attempt printed AccessDenied twice (PowerShell process exit0, NOT success); no links created. Escalated existing resources/decomp junction creation EXIT0, no downloads/tracked binaries. Existing obb junction retained.

Build command: `dotnet build <project> --nologo -v:q -clp:ErrorsOnly` (build2 adds --no-restore).

| Stem | Actual result |
| --- | --- |
| build-1 | EXIT1 four compile errors: compound ctor overload twice, wrong geometry class, readonly translation |
| build-2 | EXIT0 zero errors,2147warnings,38.57s |

Test rebuild template: `dotnet test <project> --no-restore --nologo -v:q -clp:ErrorsOnly --filter '<filter>' --logger 'trx;LogFileName=<stem>.trx' --results-directory .scratch/pickup-stack-1`. No-build template adds --no-build and omits -clp. Each class below means FullyQualifiedName~name, OR-separated.

A=PickupStackNativeVisionTests|M13RVisBuildTests|M12CheckedRowsBuilderTests|M12RVisBuildTests|CompoundActionTests|M8CompoundCheckedRowsTests|PickupPyramidPilotTests|M4ControlTests.
B=NavigationTests|R2FloorPlacementTests.
C=PickupStackNativeVisionTests|R2FloorPlacementTests.M13_021_ComputeHeadAngleToSeePoseHasTheEnginesResults.

| Stem | Filter/template | Actual result |
| --- | --- | --- |
| focused-1 | new class,rebuild | EXIT0 5P/0F/0skip1s; early asset-incomplete prototype, not marker-pipeline closure |
| regression-1 | A,no-build | EXIT1 367P/12F/0skip379total4s; ALL12 exact failure text missing embedded marker_nn_library.bin; no source assertion failure inferred |
| regression-2 | A,rebuild after assets and added epsilon/tag tests | EXIT0 382P/0F/0skip14s |
| regression-navigation-r2 | B,no-build | EXIT1 82P/1F/0skip83total13s; existing public flat helper regression (early06000000), fixed by preserving only the existing labelled adapter; old assertions untouched |
| focused-2 | C,rebuild | EXIT0 9P/0F/0skip713ms |
| old-clamp-control | new RadiansClampUsesEpsilonAndCopiesTheAlreadyConstructedOutput only,rebuild | EXIT1 EXPECTED0P/2F/0skip13ms with actual previous Math.MinMax production clamp; both exact bit equality assertions fail; candidate source bytes restored finally, comparison TRUE |
| regression-final | A OR B,rebuild after native MAIN neck receiver/foreign-root negatives | EXIT0 466P/0F/0skip29s |
| receiver-boundary-final | C,rebuild after explicit nonnull-native/public-adapter entry separation | EXIT0 10P/0F/0skip653ms |

No overlapping counts summed. No test skips/sleeps/timeout increases. Diffcheck EXIT0. Frozen8 code/test raw hashes `.scratch/pickup-stack-1/source-hashes.txt`, LF UTF8 artifact SHA25669b01b58a71b09e04ed037d2555641610b1c2624b68b7c9b7a7641f8c5da82aa. No testhost active.

## Remaining / conditional proposals

Finite subtree is built; whole PICKUP behavior is NOT complete. Existing native Pickup/IDock/first-attempt turns/DriveTo/Search/specialized activity admission guards remain explicit MISSING. Public async/spec adapters are retained and not certified as the new native runner. Full completion-union/carrier and other broader record gaps remain as before. M13-020/M13-026 own-record proposals require independent final comparison; M12-037 does not close from this child alone. M11-065 full arithmetic/M11-066 public flat adapter remain separately labelled gaps. Host libm boundary is not a phone-last-bit claim. k25 convergence-write edge is instruction-verified in code but no fixed genuine input was executed for that exact iteration; the existing unchanged fixed engine head-angle/exhaustion tests pass. No injected loop fake used to manufacture coverage. Hardware/script/full-gate acceptance is manager/operator work.
Final post-test changes are comments/fidelity ownership labels only: native M13-026 versus public M11-066, accurate k25/write/arithmetic and adapter/native-runner descriptions. No behavioral changes after receiver-boundary-final.

Independent final bounded BUILD PASS: B ran the complete A OR B affected filter with explicit dotnet --no-build --no-restore --nologo -v:q, EXIT0 466P/0F/0skip26s; independent-regression-final.{log,trx}. All8 frozen raw-file hash rows matched before/after (69b01b58...82aa artifact). No source edits/rebuild by reviewer, no timeouts/errors/abort. Scope limits above unchanged; no further builder tests.

## N5 shared Rig clock correction (2026-10-27)

New isolated codex/pickup-stack-n5-clock atb94c1c3dc5a7b5b34e0d9a80835a9b2a592a927a; A+B independent host-read PASS before code, REVIEW N5 and job N5 section read. Actual model/effort telemetry unobservable. ONLY ManipRig.cs and existing R2 delayed-poll method changed: atomic Volatile action-clock property snapshots initial ManualClock.NowMs/1000 ONCE (currently1sec); standalone M uses that independent held value. Pump/HoldClock/transport/animations/engine Timer/raw state/native500ms remain unchanged. Existing explicit M.ClockSec overrides still work; private WB/host configuration follow their existing delegate. No production code/API/policy changed.

Delayed-poll removes its redundant100 override and now grades the shared default. All original assertions remain, including >30s transport advance, pending/lock state, actual high→latched→low, one verify, Success and lock removal. Initial snapshot equality is checked; held-clock equality comes AFTER the old behavioral assertions. All R2 text outside this method compared equal to HEAD after LF normalization, including100/129.9/130 and binary32 deadline cases. Diffcheck EXIT0.

Explicit C:/Program Files/dotnet/dotnet.exe, real Python313/dotnet PATH and UTF8. Artifacts `.scratch/pickup-stack-n5/`. Test project unchanged. Test rebuild template: `dotnet test <project> --no-restore --nologo -v:q -clp:ErrorsOnly --filter '<filter>' --logger 'trx;LogFileName=<stem>.trx' --results-directory .scratch/pickup-stack-n5`.

| Stem | Exact filter / command distinction | Actual result |
| --- | --- | --- |
| focused-1 | ManipulationTests OR R2FloorPlacementTests OR NavigationTests OR M12RVisBuildTests OR M12RFix2StreamATests OR PickupPyramidPilotTests OR PickupStackNativeVisionTests (FullyQualifiedName~ each) | EXIT1 NETSDK1004 missing new-worktree project.assets.json; no tests executed |
| restore-1 | `dotnet restore <project> --source C:/Users/JimBu/.nuget/packages --ignore-failed-sources --nologo -v:q` | EXIT0 local cache only |
| focused-2 | same focused-1 filter/rebuild | EXIT0 287P/0F/0skip27s |
| old-coupled-control | ONLY FullyQualifiedName~R2FloorPlacementTests.M15_022_ADelayedPollDoesNotSpendTheControlledActionClockWhileTransportAdvances; restore ONLY old constructor Clock.NowMs/1000 delegate | EXIT1 EXPECTED0P/1F/0skip377ms. Original action assertion failed: "The resumed status poll must latch before completion; result=Timeout". Added held-property equality was not reached. Exact candidate bytes restored finally, comparison TRUE. |
| corrected-final | FullyQualifiedName~R2FloorPlacementTests OR FullyQualifiedName~ManipulationTests.PlaceOnGroundNeedsACarriedObjectAndReleasesIt; rebuild after restored control | EXIT0 37P/0F/0skip4s |

Frozen2 test-file raw hashes `.scratch/pickup-stack-n5/source-hashes.txt`, LF UTF8 artifact SHA25635cb33b83ed6b8be07b252bb0f28c8ff7fa2d49e133fff2b100936eedfe9cdca. No skipped/assertion-relaxed tests, sleeps or timeout increases. No active testhost; frozen for independent affected BUILD check. No builder full gate/commit/push; manager owns two consecutive full gates after integration. N5 is host scheduling only, no engine fidelity/whole pickup/hardware/official status claim.
Independent N5 bounded BUILD PASS: B ran the exact focused-1 OR filter with explicit dotnet --no-build --no-restore --nologo -v:q and logger independent-n5-final.trx, results-directory .scratch/pickup-stack-n5; EXIT0 287P/0F/0skip24s. Artifacts independent-n5-final.{log,trx}. Both frozen raw file hashes matched before/after; R2 outside the delayed-poll method remained LF-identical. Reviewer made no source edits/rebuild. No further builder tests.
