# Pickup-pyramid-2 independent Codex build check

**Bounded BUILD PASS, 2026-10-27.** Independently compared the frozen changed production paths against the already independently checked P1–P12/A44 and finite receiver deltas. No remaining blocking defect found. This is source/code verification plus real offline production-path execution, not whole pickup/activity completion, hardware acceptance or official record settlement. Extractor A, implementer and this checker are separate agents; actual model/effort telemetry is unavailable.

## Frozen bytes and independent execution

Builder base `fa68d7944d1fc5dad331d30402be829ae77acebb`. All **27** raw SHA256 rows in `.scratch/pickup-pyramid-2/frozen-source-sha256.txt` matched before and after execution (zero mismatches). UTF8/LF hash-list aggregate: `68abd699a6b20bfc6da3823d45616dd31abcfaa7948de165bc668c8c8f0868e2`. That artifact supplies the complete per-file list, including new untracked source files; git's tracked diff alone is not the inventory. `git diff --check` exited 0.

Actual independent command in this isolated worktree, with authorized escalated local testhost IPC:

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-build --no-restore --nologo -v:q --filter 'FullyQualifiedName~PickupPyramidNativeExecutionTests|FullyQualifiedName~PickupPyramidPilotTests|FullyQualifiedName~PickupStackNativeVisionTests|FullyQualifiedName~PutDownBlockNativeTests|FullyQualifiedName~PickupVerifyTests|FullyQualifiedName~ManipulationTests|FullyQualifiedName~NavigationTests|FullyQualifiedName~R2FloorPlacementTests|FullyQualifiedName~M12|FullyQualifiedName~CompoundActionTests|FullyQualifiedName~M8CompoundCheckedRowsTests|FullyQualifiedName~M8ActionCheckedRowsTests|FullyQualifiedName~ActionListTests|FullyQualifiedName~ActionCompletionTests|FullyQualifiedName~MarkerObservationTimeCheckedRowsTests|FullyQualifiedName~M11CheckedObservationRowsTests|FullyQualifiedName~RobotStateHistoryAdmissionTests|FullyQualifiedName~M4ControlTests|FullyQualifiedName~DriveOffPilotVisionTests|FullyQualifiedName~M11ProcessedImageTimestampTests' --logger 'trx;LogFileName=independent-final.trx' --results-directory .scratch/pickup-pyramid-2 *> .scratch/pickup-pyramid-2/independent-final.log
```

Exit **0**, **700 passed / 0 failed / 0 skipped**, duration **35 seconds**. TRX executed=700; error, timeout, aborted, disconnected and notExecuted all zero. Artifacts are `independent-final.log` and `independent-final.trx` in that directory. No parallel host or new full-suite/hardware run. Test slot released after process exit.

Builder history is preserved in `PICKUP-PYRAMID-2-builder.md`: the earlier broad run was actually 699P/1F, not a pass. The only subsequent test correction changes fresh canonical KnownMarker timestamps from null to source-proven zero (`87E276..27C` clears through +48); public unknown/null and frame-MISSING assertions remain. Independent final execution includes that correction. No production negative-control mutation was performed in this job; earlier failed fixture attempts are not source-fidelity proof.

## Changed path verified

- Actual shipped factory/runtime manager/parent +84/helper, normal first-face flag104=true, owned first turns, ActionList motor children, engine-drained worker LOCAL44, raw docking/load/B8, carry verification and original typed completion/success reaction are executed with explicit scene/calibration/firmware fixture inputs. Direct-runner and synthetic carrier tests remain component evidence, not substitutes for this configured test.
- TurnObject uses ANY32767, f32 MAIN-relative marker selection, mandatory refinement/tolerance and second cycle; VerifyObject type2E/tracks1 uses real retained44 and per-marker unsigned timestamp queries. Native constructors alone initialize marker stamps0. No Task bridge.
- IDock/Pickup type15→16/17/tracks7, F7=false, retained prepared compound, zero-delay head path, stored raw-state timestamp gates, success-prefix routing, canonical request owner and source-ordered retirement match the checked contract. Abort sends only; destructor separately resets marker3/target−1. Late B8 cannot reuse a retired native tuple; existing public legacy mode stays separate.
- Immutable object-interaction body is captured before descendants retire, cached by current tag, retained through watcher/API/proxy/response animation, including source word0 overwrite. Defined normal flag is zero; undefined full public bodies are visibly skipped under M1-034 while defined internal flag/scalar completion survives. Legacy U32/other variants remain.
- Raw Active bit2 is stored after synchronized off-treads handling and before charger/origin rejection (`512A96..AA0`). Historical camera uses the SAME successful retained computed history/head and parents to that exact pose (`513AB0..513B16`, caller `654E82..E8E`), with checked engine camera geometry and pose operations. Frame/pose scopes end before host FrameProcessed/LOCAL43 callbacks.
- Late unsupported speech clears callbacks, unwinds its watcher depth and defers only its own parent/tag Stop plus ActionList.Cancel until after iteration, independently of the UI gate. Both UI modes execute teardown tests; unrelated native Wait43 survives. No fabricated failure/success or global action clear.

## Current own-record recommendations and limits

Current **M12-018 IMPLEMENTATION_GAP**, title **“The dock abort path”**, evidence **`0x0063BE10 AbortDocking; 0x0063BE2A blx 0x4B94C8; 0x0063BE36 blx 0x4A5368`**: propose own-record completion for Claude review. Send-only/raw-return behavior, genuinely pending request, real native destructor caller and separate request reset are covered; no own residual identified.

Current **M13-020 IMPLEMENTATION_GAP**, title **“PanAndTiltAction, TurnTowardsPoseAction::CheckIfDone and WaitForImagesAction”**, evidence **`549C48..549D60`, `54B010..54B01C`, `54CA64..54CBEC/54CC1C..54CC4C/54DD88..54DE38`**: native owned Init/Check/Wait execution, actual MAIN/head/calibration inputs, real parallel motor children and LOCAL43 lifecycle are now code/source verified and executed. Retain the existing M13-026 equivalent-libm boundary; no phone-last-bit claim. Whole-record settlement still needs Claude's own-scope decision on the four explicitly unbuilt PanAndTilt setters `5498A0/5499AC/549AE8/549B0C`, still stated in current code/record. This is a record recommendation issue, not a blocker in the built source-supported caller path.

Whole M12-007/017/034/037 remain partial: existing supported public/manual Verify, general IDock setup/cleanup and marker-query adapters still have their own recorded differences; native external4C producer remains visibly unavailable. Native error-signal IMU/computed/clamp inputs (M12-019), named speech, DriveTo/Search, unsupported passive geometry and full host44 payload remain named MISSING. Typed LOCAL44 is only the recovered consumed prefix, never fake truncated wire. Enclosing Spark/BuildPyramid activity admission, proprietary detector/PnP input fidelity and hardware are not certified. No manifest/status/REVIEW/Claude files changed.

## Final affected camera-scope correction (supersedes two earlier hashes)

**Affected BUILD PASS.** A later narrow review found that assigning the historical marker camera to shared `processingCamera` also changed face translation on mixed marker/face frames. The checked `513AB0..513B16` / `654E82..E8E` contract established the marker recipient only; P3's face attachment/cache checks did not establish that face-input change. The manager stopped its in-progress full gate; no completed full-gate pass is claimed here.

The exact three-line production correction keeps original `processingCamera` for faces, initializes separate `markerCamera`, replaces only that value with the historical native camera and supplies it solely to `World.UpdateObservedMarkers`. Historical-state ownership and released callback/pose gates are unchanged. The mixed actual-worker regression preserves exact face Translation/Quaternion on EVERY marker+face frame while requiring real LOCAL44 ID7 and actual observed-marker timestamps. Repeated real frames satisfy the existing pose confirmer; no fabricated44 or marker-stamp writes. The supplied face detector remains the explicit proprietary input boundary, not detector-source proof.

Frozen replacement raw hashes, verified before AND after independent execution:

- `Vision/VisionSystem.cs`: `caa0aeafcb4af52c77508a68d10667ba7eaa94e4212d0cd8f38875c6c63b0477`
- `PickupPyramidNativeExecutionTests.cs`: `a32a48802cf8a6681d381d41058b7b9b0e782a33ad5a45858b6cdf000a123935`

Full paths are in `.scratch/pickup-pyramid-2/frozen-camera-scope-sha256.txt`, UTF8/LF aggregate `45760425e20c69333b6572e3bbe8e388a5dd62e27ad1d31147aeb36334bd60ea`. All other25 original raw hashes still match. The earlier700/700 execution remains evidence for the prior candidate/unchanged paths, not a new700 run on these replacement bytes.

Actual independent authorized escalated command:

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-build --no-restore --nologo -v:q --filter 'FullyQualifiedName~PickupPyramidNativeExecutionTests|FullyQualifiedName~PutDownBlockNativeTests|FullyQualifiedName~DriveOffPilotVisionTests|FullyQualifiedName~FaceTests' --logger 'trx;LogFileName=independent-camera-scope.trx' --results-directory .scratch/pickup-pyramid-2 *> .scratch/pickup-pyramid-2/independent-camera-scope.log
```

Exit0: **92 passed /0 failed /0 skipped,10 seconds**. TRX executed92; error/timeout/aborted/disconnected/notExecuted all0. Builder's preceding42P1F and individual fixture failures remain in its ledger; they were not passes. No production change beyond this camera separation, no repeat700/full suite, no hardware or broader face claim. Final bounded BUILD PASS applies to the combined original25 hashes plus these two replacements. Record recommendations and named gaps above remain unchanged. Own test process exited; exclusive slot released.
