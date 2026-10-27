# Pickup-pyramid-3 independent Codex build check

**Bounded BUILD PASS, 2026-10-27.** Independently compared the whole `d71e12c7..frozen candidate` delta, including the earlier unverified WIP and new test file, against the separately checked source inventory. Independently executed the final affected regression: **849/849 passed, zero skipped, 51 seconds, exit 0**. This is source/code and offline production-path verification, not hardware or whole-pickup acceptance. No manifest, inventory, REVIEW, Claude file or status was changed. Model/effort telemetry is unavailable; extractor A, builder and this checker were separate agents.

## Checked production path and source

The existing configured pickup factory/manager/helper path now handles defined PK9 `0300001D` with internal flag false by delegating to the actual native helper component, SearchForBlock(target, minSeen 0, maxStage 0), then retrying through the same parent-owned ActionList. Missing internal completion data is visibly unavailable, never an invented false. The original completion remains preserved through the response animation. Relevant caller/result anchors are `005B8286..005B8458`, success `005B8B7A` and failure `005B8BEA`; earlier P1–P4 source checks remain evidence for unchanged recipients.

Search constructor/Init/Search/Update/callback/lifetime (`005BAC7C..005BB830`) retain native44 throughout helper storage lifetime, including terminal held references. Target lookup precedes the signed stage limit. Stage0 constructs Nearby before the outer sequence, executes TurnObject first with ignoreFailure, then Nearby, and publishes stage1 after StartActing. A matching44 explicitly completes; normal specific-target exhaustion fails. Update stops the current parent action before the helper result is delivered. Last-owner release retires only the new Search helper's storage/subscription, not every existing helper implementation.

Nearby `005469C0..005470EE` uses the shared context RNG in the six recovered draws, including the coin, the first narrowed turn in the second-turn expression, and the repeated first wait. Seven real owned children execute the checked wait/drive+head/wait/turn/wait/turn/wait sequence. The default-speed negative drive uses -80 and its animation byte false; TurnInPlace retains its inherited defaults including D8 true. Embedded Update precedes atomic observation-latch consumption, and Init preserves that latch. TimedWait `0054C8FC/0054CA1C/0054CA3C` uses binary32 EngineTimer addition and strict ordered `now > deadline`; action clocks, robot-state clocks and UTC are not substituted.

The native cancellation omission is repaired at the actual owned-runner binding. Source `005BD3DA..005BD3FE` reads the tag after helper stop, preclears for keepAction false, calls actual ActionList.Cancel for both keep values, then clears only if the tag still matches. Captured list/runner identity and postcomparison preserve a replacement installed reentrantly. The legacy async adapter retains its separate cancellation mechanism; no helper-keepalive or global-clear substitute was added.

Native completion metadata was independently checked from original instructions: Nearby21 and Wait31 cases at `0053FEFB/0053FF0B` dispatch to `0053FF0E`, whose setter `0075C84C..0075C86A` selects defaultCompleted **tag6/no payload**; their virtual getters copy the own cached union. P5 no-proxy compounds similarly use type -2 (`0054E9B8`), table index0, and own-cache return `0054F27A..0054F3EA`. Only these native P5 objects are seeded. Legacy U32 adapters and explicit proxy selection remain unchanged.

## Concurrency and unavailable input

The offline public image entry can deliver real44 concurrently with helper ticking. Nearby's latch is atomic. Search protects its native status/seen/retired/unavailable state with a narrow gate, releasing it before World, ActionList, parent, logging or subscription disposal. The normal IHelper returned-status commit remains the default for other helpers; Search explicitly stores its guarded status and prevents a stale virtual return from overwriting a concurrent44. This is host scheduling machinery preserving the source result.

For S7, the exact INIT camera snapshot/frame/FOV/occlusion input to `005BB73C` remains unavailable. The first unseen unrelated44 with a present target therefore logs a named NativeActionUnavailable, preserves prior native status, suppresses later success, and retires only its owned subtree. It does not fabricate visibility, ignore the event or report a native failure/success. ConcurrentQueue enqueue avoids FrameGate→ActionList inversion. Cleanup runs after iteration with parent/tag identity checks. A postpublication recheck covers the old/zero-tag window; the owned sequence blocks child execution while availability cleanup is pending but permits Cancelled/terminal retirement. Its private-gate availability check is the tick linearization point, not a claim that already-in-flight work is instantaneously undone.

## Tests and limits of their reach

The configured positive test exercises shipped factory/admission, the original helper, an explicitly supplied missing-marker input, actual failed pickup/response/PK9, Search/Nearby, rendered-marker worker/world-confirmation native44, retry, real docking/load/carry and original typed callback. Its explicit ObjectPositionUpdated reaction lock is a fixture input. A separate unlocked natural-reaction test verifies the interrupted queued retry retires and cannot later dock/carry while an unrelated parallel action survives. KeepAction true/false and reentrant replacement use real owned native actions.

Component cases grade six RNG draws, strict timer equality, metadata6, retained subscriptions/last-owner disposal, missing-carrier handling and unavailable S7. A dedicated-thread public ProcessImage case exercises the actual rendered-marker producer concurrently with ticks. The default-null prepublication barrier case is explicitly a controlled recipient scheduling test, not a claim of upstream perception production. These tests do not establish the S7 visibility input, later search stages, DriveTo, speech/audio, or existing public manual adapters.

Current manifest claims were reread rather than inferred: **M12-010 IMPLEMENTATION_GAP**, “The search-for-block pattern, built,” cites ctor `005469C0`, Init `00546C14`, stage0 `005BAF9A/005BB024/005BB03E` and later stages. This build covers only the admitted stage0 slice, so no whole-record settlement is proposed. **M8-001 IMPLEMENTATION_GAP**, “The IBehavior lifecycle: constructor fields, Init, Update, Stop, StopActing, Resume, IsRunnable and IsRunnableScored,” explicitly cites StopActing `005BD34C..005BD3FE`; this repair is not whole lifecycle closure. **M11-042 IMPLEMENTATION_GAP**, “Still unread next to the BlockWorld observation path: IsVisibleFrom projection/occlusion, FindLocatedObjectHelper returnFirst=0 result, subscriber bodies behind 0x0064E704, calibration null at CheckForUnobservedObjects,” includes the existing visibility boundary; no new exclusion or policy is asserted here.

Builder history remains in `re-analysis/jobs/trial-m13/PICKUP-PYRAMID-3-builder.md`: failed configured-input attempts/diagnostic compile, actual reaction interruption, unsuccessful suppressed-event fixture attempts and the final assignability-only test correction are not erased by this pass. Builder's final 849/849 result is separate from the independent execution below. No full suite or hardware run was performed by this checker.

## Independent command, artifacts and frozen bytes

Ran the exact filter in `.scratch/pickup-pyramid-3/regression-command.txt` with `--no-build --no-restore`, explicit `C:/Program Files/dotnet/dotnet.exe`, project `cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj`, `--nologo -v:q`, logger `trx;LogFileName=independent-final.trx`, results directory `.scratch/pickup-pyramid-3`, console redirected to `independent-final.log`. Local test IPC used authorized escalation. TRX outcome **Completed**, total/executed/passed **849**, failed/error/timeout/aborted/notExecuted/disconnected all **0**. The process exited; exclusive test slot is released. `git diff --check d71e12c7` exited0.

All 12 raw SHA256 values below matched before and after execution. The complete UTF8/LF hash-list artifact `.scratch/pickup-pyramid-3/frozen-sha256.txt` has SHA256 **bb39d340c84f080488d8c314f9c175794843760fc85613e392f83c5de8c71a52**. Reports are outside this code/test hash scope.

```text
74f6bc1b5ceca43b0d8d38bd312a163babc0430f27dc46721fadb8c53f56278d  cozmo-stack/src/Cozmo.Robot/Actions/ActionList.cs
76000b5c6ebb29894fe75b2dc5d36312c3f2a25fc4a5f3b5ddcc517107dc00c9  cozmo-stack/src/Cozmo.Robot/Actions/IActionRunner.cs
0a114d2a5e98da3da966ba5a267dac2561b715f7fa34209cd2d31810b180abc6  cozmo-stack/src/Cozmo.Robot/Actions/RobotCompletedAction.cs
f0ce389120374cf371e9d91f74ae75dbeec61f1ebb8d14b72182e286f3995887  cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorHelperComponent.cs
b647061164945ea6396a6775b7e7def69b923aea156aefaf4d85e8d1e708f8be  cozmo-stack/src/Cozmo.Robot/Behavior/IHelper.cs
e0133fba546e874e8757e1781d996ae51b41ca483e56457fa2c7a6f2b47a44fe  cozmo-stack/src/Cozmo.Robot/Behavior/NativePickupBlockHelper.cs
87c1fcbd353989d0f6bd91b41f3b9c468592d485e15098ddc0eeb05745ba25ca  cozmo-stack/src/Cozmo.Robot/Behavior/NativeSearchForBlockHelper.cs
65f0cac191f862af8777a181234732f0f24fdd46d5b6a7e59541a4c9329515e9  cozmo-stack/src/Cozmo.Robot/Behavior/SteppedBehavior.Actions.cs
9098cfd8d3ab03accfff348d67f7a9be11d36308394a8af4667d31fc0e45173e  cozmo-stack/src/Cozmo.Robot/Behavior/SteppedBehavior.cs
0c6497959dfb72a3ae8197ae671d1599e6578c26f96156efa0bb02f8dbe5780e  cozmo-stack/src/Cozmo.Robot/Manipulation/NativeNearbySearchRunner.cs
dc568064647ed88991ff837495fc96ebea9c4615fb824e8aef788905ac947d45  cozmo-stack/tests/Cozmo.Protocol.Tests/PickupPyramidNativeExecutionTests.cs
8a61b19f779e37e77ba1c9adbf8c7b448aa5b0dd3e089b678717e15f140de06c  cozmo-stack/tests/Cozmo.Protocol.Tests/PickupSearchNativeTests.cs
```
