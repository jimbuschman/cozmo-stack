# Pickup-pyramid-2 builder — frozen for independent check

Base fa68d7944d1fc5dad331d30402be829ae77acebb; isolated codex/pickup-pyramid-2. Actual model/effort telemetry unavailable. No extraction, hardware, full suite, official-record changes or commit.

Source boundary: checked P1/P2/P3 reuse plus 20261027-pickup-pyramid-2-read/read-codex-check and released constructor/query deltas. Concrete owned native pickup/TurnObject/VerifyObject, actual local44 after canonical object/marker/occluder update, retained native Docking target/marker and immutable completion snapshot integration. Public legacy U32/async adapters/D1/D2 stay separate. Full host44, native computed-state/IMU/clamp error signal, named speech, DriveTo/Search and unsupported passive inputs remain named gaps. Passing supplied-scene/firmware tests are not hardware or general activity admission proof.

## Actual commands/results

All commands ran in this isolated worktree. Executable `C:/Program Files/dotnet/dotnet.exe`; project `cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj`. Console logs and TRX under `.scratch/pickup-pyramid-2/`. Restore/build/test local IPC escalated after the default restore failed. Tests use no skips, sleeps or timeout changes.

| Artifact | Exact arguments after executable | Observed result |
|---|---|---|
|restore-1.log|`restore <project> --ignore-failed-sources --nologo -v:q`|Default sandbox exit1, empty log; no inferred success.|
|restore-2.log|same restore|Escalated exit0.|
|build-1.log|`build <project> --no-restore --nologo -v:q`|Exit1 CS1736 nonconstant optional Radians.Pi default.|
|build-2.log|same build|Exit1 CS0246 AnimationTrigger namespace.|
|build-3.log|same build|Exit1 CS0246 persisted: enum belongs to Behavior, not Protocol.|
|build-4.log|same build|Exit0, 2154 warnings/0 errors,29.01s.|
|existing-1.log/.trx|`test <project> --no-build --no-restore --nologo -v:q --filter 'FullyQualifiedName~PickupPyramidPilotTests|FullyQualifiedName~PutDownBlockNativeTests|FullyQualifiedName~PickupStackNativeVisionTests' --logger 'trx;LogFileName=existing-1.trx' --results-directory .scratch/pickup-pyramid-2`|43P/0F/0skip,5s,exit0.|
|focused-1.log/.trx|`test <project> --no-restore --nologo -v:q --filter 'FullyQualifiedName~PickupPyramidNativeExecutionTests' --logger 'trx;LogFileName=focused-1.trx' --results-directory .scratch/pickup-pyramid-2`|2P/2F/0skip,701ms,exit1: queued dock0300001D and specific Verify unseen; fixture omitted observed-marker data.|
|focused-2.log/.trx|same test arguments with focused-2 names|3P/1F/0skip,1s,exit1: queued dock0300001D; fixture supplied marker codes but real worker calibration missing.|
|focused-3.log/.trx|same test arguments with focused-3 names|3P/1F/0skip,1s,exit1: actual NV calibration installed;87mm scene no detected44; no assertions changed.|
|focused-4.log/.trx|same test arguments with focused-4 names|3P/1F/0skip,5s,exit1:150mm rendered scene yields87 actual44 reports, owned TurnObject remainsRunning; diagnostic trace retained.|

Reviewer confirmed/fixed during build: actual type2E/mask1; whole prepared compound retains both children; refine tolerance assignment; normal carry flag0; remove unsupported global named-face guard; native marker timestamps/query; reset host sent phase; ANY closest failure maps BAD_MARKER; native ctor marker timestamp0. These are affected source corrections, not blanket acceptance.

|focused-5.log/.trx|same focused arguments with focused-5 names|3P/1F/0skip,1s,exit1: real8 local44 reports, specific marker timestamp notaccepted. Trace identifies actual marker-camera frame-conversion frontier; no fake stamps injected.|

|focused-6.log/.trx|same focused arguments with focused-6 names|5P/2F/0skip,2s,exit1: existing marker-camera failure plus actual raw DockingOwner.Active writer missing (expected1/actual0); source question reported, not fixture weakened.|

|receiver-1.log/.trx|focused filter `FullyQualifiedName~PickupPyramidNativeExecutionTests&FullyQualifiedName!~QueuedPickup&FullyQualifiedName!~RealOwnedRetirement`, remaining test flags unchanged|5P/1F/0skip,1s,exit1: late-name fixture never verified face due source frame mismatch; no speech branch reached.|
|receiver-2.log/.trx|focused filter `FullyQualifiedName~PickupPyramidNativeExecutionTests&FullyQualifiedName!~QueuedPickup`, remaining test flags unchanged|6P/1F/0skip,1s,exit1: raw mirror repair makes retirement test PASS; late-name fixture still used captured old origin (verified0, NoCommonParent). Corrected new observation input to current source origin; all assertions retained.|

|focused-7.log/.trx|same full focused arguments with focused-7 names|7P/1F/0skip,2s,exit1: real queued pickup PASS after checked historical-camera/raw mirror binding; late-name fixture repeated no-eye observations stripped its supplied parent through existing face adapter, so no verified face. Corrected supplied detector input to real eye-parts branch; no face production repair/claim.|

|focused-8.log/.trx|same full focused arguments with focused-8 names|11P/2F/0skip,4s,exit1: actual configured pickup/carry/full callback passed but fixture stopped before helper-success reaction; late-name reached real unsupported speech but pending cleanup did not retire queue.|
|focused-9.log/.trx|same full focused arguments with focused-9 names|13P/2F/0skip,4s,exit1: configured full sequence/reaction and real-worker callback gate PASS; both UI-mode late-name cases found parent Stop did not cancel native queue.|
|focused-10.log/.trx|same full focused arguments with focused-10 names|14P/1F/0skip,3s,exit1: both UI-mode teardown assertions PASS; false-mode diagnostic collector missed process-deduplicated message emitted in true case.|
|focused-11.log/.trx|same full focused arguments with focused-11 names|15P/0F/0skip,4s,exit0; existing ResetMissingForTests isolates diagnostic collector; unrelated real Wait43 remains Running after own error cleanup.|
|regression-1.log/.trx|exact affected command below|699P/1F/0skip,700total,38s,exit1: sole obsolete actual-native constructor oracle expected nullable unknown instead of checked timestamp0.|
|marker-final.log/.trx|full focused command with filter `FullyQualifiedName~MarkerObservationTimeCheckedRowsTests|FullyQualifiedName~PickupPyramidNativeExecutionTests` and marker-final names|33P/0F/0skip,4s,exit0; source-backed native constructor assertion0 only, public unknown-input/null/frame diagnostics retained.|

Exact affected regression command (PowerShell; executable/project as above):

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-build --no-restore --nologo -v:q --filter 'FullyQualifiedName~PickupPyramidNativeExecutionTests|FullyQualifiedName~PickupPyramidPilotTests|FullyQualifiedName~PickupStackNativeVisionTests|FullyQualifiedName~PutDownBlockNativeTests|FullyQualifiedName~PickupVerifyTests|FullyQualifiedName~ManipulationTests|FullyQualifiedName~NavigationTests|FullyQualifiedName~R2FloorPlacementTests|FullyQualifiedName~M12|FullyQualifiedName~CompoundActionTests|FullyQualifiedName~M8CompoundCheckedRowsTests|FullyQualifiedName~M8ActionCheckedRowsTests|FullyQualifiedName~ActionListTests|FullyQualifiedName~ActionCompletionTests|FullyQualifiedName~MarkerObservationTimeCheckedRowsTests|FullyQualifiedName~M11CheckedObservationRowsTests|FullyQualifiedName~RobotStateHistoryAdmissionTests|FullyQualifiedName~M4ControlTests|FullyQualifiedName~DriveOffPilotVisionTests|FullyQualifiedName~M11ProcessedImageTimestampTests' --logger 'trx;LogFileName=regression-1.trx' --results-directory .scratch/pickup-pyramid-2 *> .scratch/pickup-pyramid-2/regression-1.log
```

## Changes and source reach

| Files under `cozmo-stack/src/Cozmo.Robot/` | Claim / primary contract |
|---|---|
|Actions/ObjectInteractionCompletion.cs (new), IActionRunner.cs, RobotCompletedAction.cs, CompoundActions.cs, ActionWatcher.cs; ActionCompletionReport.cs|Immutable fiveU32/count/flag22-byte carrier through LIVE getter/current-tag cache/Prep/proxy/watcher/original callback/API; normal carried iteration overwrites word0; unknown public body skipped while internalflag/scalar/local completion survives. 55381C..55386A,75B4F0,75C460,75C8D8; checked P1/P2 completion contract. Existing legacy U32/animation/default variants preserved.|
|Manipulation/NativePickupObjectRunner.cs (new), DockActions.cs (narrow internal Select/Verify exposure)|Engine-ticked actual Pickup/IDock, every-tick target check, retained prepared children/real motors and Verify; type15→16/17/tracks7, true lift-load request, rawDA/C5/4C ownership, source-ordered destructor. 551528,551F9C,5520E0..E6,5521AC,553648..553FEE.|
|Vision/NativeObjectRunners.cs (new), NativeObservedMarkers.cs (new); PanAndTiltActions.cs, NativeVisionRunners.cs|Owned TurnObject/VerifyObject, inherited one-tag TurnPose, actual per-marker unsigned stamp query, precise tolerance/reset/two-update branch. 549FE0..54B0A8,54AE58..66,5686D8..568B1E,876C50..96. Verified type2E/mask1 and CanInterrupt false.|
|Behavior/NativePickUpCubeBehavior.cs, NativePickupBlockHelper.cs; Vision/NativeLastFaceRunner.cs|Actual configured pickup bound to engine owners; real helper first turns with supplied max/104 flags, parent84, retry/response original-report capture; actual LastFace ctor args/copied Radians preserve PutDown. PK1–10/5B7A4C,5B7482..7524,5C60B8..5C697C. Unsupported speech throws visibly at real branch.|
|Manipulation/Docking.cs; Lights.cs|Actual canonical target/marker request ownership and late B8 behavior, interacting owner forwarding, Abort source caller. 63BA44..63BD50,63BE10/2A/36. Explicit legacy async request mode preserved separately; native error/clamp remains MISSING.|
|Vision/NativeObservedObject.cs (new), BlockWorld.cs, VisionSystem.cs|Canonical typed LOCAL44 after object/marker/occluder update, actual external gate; full host report remains MISSING. NEW native markers initialize timestamp0 only (87E27C); public nullable unknown preserved; observed stamps use existing producer.|
|NativeDelocalization.cs, Vision/NativeRobotStateHistory.cs, VisionSystem.cs|Historical native camera from SAME retained full computed history/head; checked engine headcam17.52,0,-8/neck/Rotate/PreCompose, parent exact Hist.Pose. 513A66..513B16/654E82..E8E. No second history query/current-origin guess; owned state disposed before optional host callbacks. Public legacy camera helpers preserved.|
|Sensors.cs|Actual synchronized raw docking bit2 writer after OffTreads consequences and before charger recipient, including unknown-origin states. 512A96..512AA0.|
|Actions/NativeActionUnavailable.cs (new), ActionList.cs; ActionWatcher.cs, NativePickupBlockHelper.cs|Host error machinery: unwind only interrupted watcher updates, clear own callbacks, defer cleanup after queue iteration/watcher independently of UI input, verify parent/tag then Stop and Cancel only owned tag. No native failure/result fabricated or globally cleared actions.|

Tests changed: new `PickupPyramidNativeExecutionTests.cs` (15 cases) and exact native-constructor oracle in `MarkerObservationTimeCheckedRowsTests.cs`. New coverage distinguishes supplied component/callback units from the actual shipped configured instance → real manager Start/parent84/helper(normal104true) → actual ActionList first turns/worker44/dock raw/load/Verify/carry → original immutable typed callback and initial/success real animation CA/CB. The scene/calibration/pre-action pose and firmware messages are explicit fixture inputs, not hardware evidence. Real producer carrier test selects target7 with actual carrying-set9 and asserts exact22 bytes/normalflag0; real NoObject failure proves undefined-public/internalflag1. UI true/false late-name tests preserve unrelated native Wait43, verify exception details, owned destruction, no docking continuation, no locks/watcher stack leak. Real worker test verifies FrameProcessed and LOCAL43 are outside frame/pose gates. Native Abort/late B8, marker older/reordered unsigned timestamps, and same-history camera lifetime have actual owner tests. No injected5A in configured execution.

## Frozen handoff and limits

27 code/test raw byte hashes: `.scratch/pickup-pyramid-2/frozen-source-sha256.txt`; UTF8/LF sorted `sha256  path` rows aggregate `68abd699a6b20bfc6da3823d45616dd31abcfaa7948de165bc668c8c8f0868e2`. `git diff --check` exit0. No running builder testhost; source/tests frozen after marker-final. No production-negative-control byte mutation or restore was performed; failed real-path fixtures and actual getter/output regressions retained above. No existing production assertion weakened; constructor0 is newly primary-proven, all public unknown assertions remain.

Remaining MISSING: native computed-state/IMU/Rodrigues clamp error signal (M12-019), named speech recipient, DriveTo/Search, unsupported passive geometry, proprietary face/marker/PnP inputs and full host44 report. Direct configured execution is exercised, not enclosing Spark/BuildPyramid activity admission or hardware. Reset/re-entry source branches are implemented but not all have new independent execution coverage. No official fidelity status changes; M12-018 Abort and M13-020 own completion are conditional proposals only after independent verification/manager gate. Legacy public manual paths retain their separate recorded gaps. No full subsystem/phone libm/hardware claim.


## Independent verification

B independently ran the exact affected regression command above with `--no-build --no-restore`, logger `trx;LogFileName=independent-final.trx` and console `independent-final.log` in the same artifact folder: exit0,700/700PASS,0skip,35s; TRX error/timeout/aborted/disconnected all0. All27 frozen raw hashes matched before/after. Bounded BUILD PASS; no full suite or hardware run. Current M13-020 still has its separate public PanAndTilt setter scope remainder; no whole-record settlement is claimed here. B source/build check is `re-analysis/research/20261027-pickup-pyramid-2-codex-check.md`.


## Post-check affected camera correction (after5287ada4)

Root/B found the marker historical-camera replacement also changed the later face `UpdateTranslation` input through shared `processingCamera`. Checked513AB0/654E82 evidence establishes the marker/World join only; this does not recover a different face camera. Isolated correction uses separate `markerCamera`, retaining the original processing camera for faces, same owned history/native camera and pose-gate release. No broad face geometry claim. Added mixed real worker marker+face regression with explicitly supplied proprietary detector output: compare existing face camera output with marker processing off/on while asserting real LOCAL44 and native observed-marker stamps. No build/test while manager full gate owned the host; root later released the sole slot after cancelling that confirmed-defect full run (no full-suite result claim).


Affected actual ledger (same executable/project/working directory/artifact folder; commands use `test <project> --no-restore --nologo -v:q --filter '<filter>' --logger 'trx;LogFileName=<name>.trx' --results-directory .scratch/pickup-pyramid-2`, console `<name>.log`):

|Name|Exact filter|Actual result|
|---|---|---|
|camera-scope-final|`FullyQualifiedName~PickupPyramidNativeExecutionTests\|FullyQualifiedName~PutDownBlockNativeTests\|FullyQualifiedName~DriveOffPilotVisionTests\|FullyQualifiedName~FacesF23Tests`|42P/1F/0skip,43total,7s,exit1; last filter token has no matching class. Mixed face-equality passed, actual44 absent.|
|camera-mixed-2|`FullyQualifiedName~PickupPyramidNativeExecutionTests.MixedRealWorkerFrame`|0P/1F/0skip,657ms,exit1; fresh actual rawstate before second image did not resolve missing44.|
|camera-mixed-diagnose|same one-case filter|0P/1F/0skip,581ms,exit1; actual1marker; World NewEntry/NonConfirmingObservation proves existing pose confirmer needs repeated observations, not missing rendering or fake marker data.|
|camera-scope-final-2|`FullyQualifiedName~PickupPyramidNativeExecutionTests\|FullyQualifiedName~PutDownBlockNativeTests\|FullyQualifiedName~DriveOffPilotVisionTests\|FullyQualifiedName~FaceTests`|92P/0F/0skip,10s,exit0. Real repeated frames until actual confirmation; face-equality every mixed frame, actual44 ID7 and native timestamp query.|

Source/tests frozen again after the92-case run; no active builder host. Raw two-file rows `.scratch/pickup-pyramid-2/frozen-camera-scope-sha256.txt`. No source changes beyond the marker-only camera binding; existing face adapter/proprietary detector limitations unchanged. This correction requires a separate commit after independent affected verification, preserving prior5287ada4.

Independent affected check: B ran the same92-case filter with `--no-build --no-restore` (exact invocation/artifacts in updated Codex check), exit0,92/92PASS,0skip,10s; TRX error/abort/timeout/disconnected0. Both replacement raw hashes before/after match aggregate45760425e20c69333b6572e3bbe8e388a5dd62e27ad1d31147aeb36334bd60ea; other25 files retain original raw hashes. No repeated700 run or hardware claim.
