# PICKUP-PYRAMID-1 builder — bounded candidate

Base: 6286e9619043670f6160b10c8d03d5b79d2aa606, branch codex/pickup-pyramid-1. Source/tests frozen for independent BUILD review; no commit/full suite/hardware run. Actual model/effort/service telemetry is unobservable.

## Changes and evidence

| Files / production join | Checked evidence / resulting boundary |
| --- | --- |
| BehaviorContainer, NativePickUpCubeBehavior, NativeBehaviorRuntime, NativeBehaviorBaseInputs | Factory0059CE04, PickUp005C6868 core; actual Robot/M/config/cache/spark owners. Missing actual Robot or M produces visible NotImplemented. RequiredUnlock uses existing checked 81-name NativeUnlockNames mapping, actual progression owner, no synthetic unlock. |
| BackupFiles, NurtureRestoration | Progression64B86E..88C ctor-empty override; Init64B8AE..BA4C resets and classifies SAME parsed root. Missing/null/nonarray defined empty; nonempty override unavailable; retirement invalidates availability. |
| NativePickupTargetCache, NativeConfigurationGeometry | Intention0 checked cache/predicate; source570B44 DefaultBest, initial literal570D04 float.MaxValue, MAIN-first distance operands570BD6..BEA/570C74..C88; signed ordering/strict ties/epsilon/prior best. Actual native WB, raw carry, root geometry and explicit-pose height, not public adapter/standalone inputs. |
| NativeClosestSpatialQuery | 61F8EC /627F92..628064: copied mutable bounds, exact f32 norm, strict radius improvement, ties first; existing Vector failure logs and retains zero. Entire query/capture serialized by existing world pose mutation gate. |
| NativePickupBlockHelper, CompoundActions, AIComponent | PK1–10/A01–05, helper005B7A4C and actual parent84→StartActing→ActionList plus finite response director; supplied-child tests verify original callback payload/proxy. No native Task bridge remains. |
| BehaviorManager, SteppedBehavior | Configured switch availability checked BEFORE stopping/replacing prior; direct native host StartAsync fails clearly before BeginRun. Required concrete engine-ticked pickup/visual-positioning recipients remain MISSING, never fake completion or timeout. |
| DockActions, Docking, BlockWorld | Select553A4A..50 saves target wrt MAIN parent; height553A66..553B06 uses saved F8.Z minus MAIN.localZ. Verify553C06 false skips load/timers. Actual DA-owned load byte, StoredState raw timestamp, moving inclusive500/recency500 or2000. Turn553D94..9E uses original saved pose; reparent553EB2..EC8 only in post-turn comparison. Parent cancellation prevents repair; pose gate never spans await. Existing CopyWithNewPose/Delete/Unset actual tail retained. |

Source files additionally touched only to expose existing internal copy/unset/proxy operations and per-run verification cleanup. Public TurnTowardsObject two-argument record shape retained. Full changed-file list/raw byte hashes: ignored `.scratch/pickup-pyramid-1/source-hashes.txt` (22 files); LF UTF8 artifact SHA256 `32007745bc68d9021255322a1881025dbec694be1429f41715a6d339f4ada60c`.

Tests: new PickupPyramidPilotTests covers actual configured factory/eligibility/cache with explicitly admitted world entries, absent-native-owner factory guard, progression/standalone isolation, source clock/frame-offset/failed-dock boundaries, real manual Verify repair/delete/unset, cancellation/parent changes, and supplied-child response/ActionList path. This is NOT an engine-running pickup/output proof. Existing M12RFix2StreamA, M12RVisBuild and Manipulation positive fixtures now explicitly supply real DA load and separate constant action clocks. Moving test preserves exact 500/501 assertions and supplies DA false after its existing DA true check. One successful manual pickup stops its synthetic old-pose cube producer after dock; no world/cache/carry mutation was fabricated. Existing reaction/objective/one-attempt assertions retained. M7BatchThreeB constructor census adds source-bound PickUp; all79 enum cells retained.

## Actual command/run ledger

All commands ran from this worktree, explicit `C:/Program Files/dotnet/dotnet.exe`; PATH prepended real Python313/dotnet and UTF8 output. Artifacts below are `.scratch/pickup-pyramid-1/<stem>.log` and, where tests executed to completion, `<stem>.trx`. No overlapping totals are summed.

Restore command: `dotnet restore cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --source C:/Users/JimBu/.nuget/packages --ignore-failed-sources --nologo -v:q`: restore-1 EXIT0.

Build command: `dotnet build cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-restore --nologo -v:q -clp:ErrorsOnly`.

| Stem | Actual result |
| --- | --- |
| build-1 | sandbox EXIT1, zero errors/warnings printed; FAIL, not inferred PASS |
| build-2 | escalated EXIT1 CS0534 missing PickupAsyncRunner.CanInterrupt |
| build-3 | EXIT1 CS0030 turn facade bool→uint; corrected, later entire native Task bridge removed |
| build-4 | EXIT0, zero errors, 2141 warnings,30.40s |

Test template: `dotnet test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-restore --nologo -v:q -clp:ErrorsOnly --filter '<filter>' --logger 'trx;LogFileName=<stem>.trx' --results-directory .scratch/pickup-pyramid-1`. Diagnostic `--no-build` invocations omit `-clp:ErrorsOnly`. Filters below use FullyQualifiedName~ for each listed class/prefix, OR-separated; `manual-one` means exact FullyQualifiedName=Cozmo.Protocol.Tests.ManipulationTests.PickUpCubeReactsDrivesDocksAndCelebrates.

| Stem | Filter | Actual result |
| --- | --- | --- |
| focused-1 | PickupPyramidPilotTests | compile EXIT1 CS7036 DockResult arguments |
| focused-2 | PickupPyramidPilotTests | EXIT0 4P/0F/0skip,1s |
| focused-3 | PickupPyramidPilotTests OR PickupVerifyTests OR M12CheckedRowsBuilderTests | EXIT0 40P/0F/0skip,1s |
| regression-1 | PickupPyramidPilotTests OR PickupVerifyTests OR M12 OR ManipulationTests OR Unlock OR Nurture OR Backup | INTERRUPTED EXIT1, no final counters/TRX; four observed failures (M12 failed-dock diagnostic, moving load input, manual pickup missing DA, DriveToObjectGoes... waiting new state). No final pass claim. |
| diagnose-timers-1 | --no-build M12RFix2StreamATests.M12_007_ | EXIT1 0P/2F/0skip,675ms; correct failed-dock result lacked old diagnostic, moving fixture had DA true/C2=1 |
| diagnose-manual-1 | --no-build manual-one | EXIT1 0P/1F/0skip,1s; celebration missing after retry |
| diagnose-final-1 | PickupPyramidPilotTests OR M12RFix2StreamATests.M12_007_ OR manual-one | EXIT1 15P/1F/0skip,16total,2s; trace dock success→unknown load repeated Verify→30s timeout→retry BadObject. Temporary diagnostic assertion retained same predicate; exact original file bytes restored and compared equal. |
| focused-4 | PickupPyramidPilotTests OR M12RFix2StreamATests.M12_007_ OR ManipulationTests | EXIT1 39P/1F/0skip,40total,4s; real DA supplied, synthetic scene still reobserved old cube pose and correctly caused occupancy PickupRetry |
| regression-2 | regression-1 filter OR M7BatchThreeBTests | EXIT1 406P/6F/0skip,412total,33s; five standalone factory NativeRobot-null exceptions; constructor census missing PickUp entry |
| regression-final | same regression-2 filter | EXIT0 412P/0F/0skip,32s |
| availability-final | PickupPyramidPilotTests OR M7BatchThreeBTests | compile EXIT1 CS1503 negative fixture JSON array instead of keyed map |
| availability-final-2 | PickupPyramidPilotTests OR M7BatchThreeBTests OR M7ManagerLifetime OR M5X13 | EXIT0 61P/0F/0skip,17s; final frozen source |

Read-only `git diff --check` EXIT0 after freeze. A Python diagnostic print initially hit console cp1252 UnicodeEncodeError; rerun with PYTHONIOENCODING=utf-8, no source/test mutation. No old-code negative control executed; manager accepted source-boundary regressions rather than manufactured extra control. No test sleeps, timeout increases, assertion skips, or production default load success.

## Remaining gaps / claim limits

Whole PICKUP-PYRAMID-1 is incomplete. Native specialized BuildPyramid/activity admission and required concrete engine-ticked PickupObject/visual positioning child recipients remain MISSING; configured native guard retains prior selection. The finite visual child read is available but not fabricated with supplied executors. Helper retry/response scaffolding is not proof of complete native helper execution. Full pickup completion union byte15/public carrier remains named MISSING; other union variants unchanged. Passive footprint recipients/ambiguities remain guarded. CanInterrupt remains MISSING. Public/manual async dock API is a legacy adapter, not native per-action-tick scheduling/reentry; its30s host deadline remains, so no whole M12-007 settlement. No complete behavior, hardware, official status or acceptance claim. Candidate M15-044 is limited to its own checked 81-name table/readAsJson/InitConfig/default-caller claim; M15-034 override/IsUnlocked/NV-sink ownership is separate, with no whole progression-query closure claim.
## Final affected public-selection correction

Independent prior-frozen candidate: sandbox vstest IPC connection abort before executing tests (EXIT1, independent-regression.log/TRX); escalated no-build/no-restore same regression filter EXIT0 413P/0F/0skip32s, independent-regression-ipc.log/TRX, 22 hashes unchanged. No source behavior failure inferred from IPC abort.

B found actual public CreateNative.Manager.ChooseAndSwitch could stop prior action before native StartAsync availability throw. Added same winner preflight before replacement, retaining Current plus scores/Selected result; no eligibility/score change. New test uses actual CreateNative.Manager/Bound/native eligibility/world, explicitly supplies public host Score=1 (restored finally), and proves prior real ActionList runner survives. This is public host ranking proof, not native flat-score producer proof.

Affected command uses the above test template, filter FullyQualifiedName~PickupPyramidPilotTests. availability-public-guard EXIT1 15P/1F/0skip16total1s: new fixture omitted positive host score so Chosen=null, not intended winner. availability-public-guard-2 EXIT0 16P/0F/0skip1s after explicit host input; source/tests now frozen, no testhost active. Only BehaviorManager and PickupPyramidPilotTests changed after previous 413 independent pass. Final diffcheck EXIT0.

Independent final affected check: explicit dotnet test csproj --no-build --no-restore --nologo -v:q --filter FullyQualifiedName~PublicNativeObserverScoringRetainsPriorActionWhenPickupExecutionIsUnavailable --logger trx;LogFileName=independent-public-guard.trx --results-directory .scratch/pickup-pyramid-1; EXIT0 1P/0F/0skip448ms. B reports final22 raw-file hash rows unchanged before/after; bounded BUILD PASS, whole behavior still incomplete. Console/TRX independent-public-guard.{log,trx}.

## Manager full-gate correction investigation (2026-10-27)

Manager integrated candidate a074a2b3 full suite ABORTED EXIT1 at05:04:51UTC:4264P/4F/0skip/4268executed, not complete6377. Four Navigation positives failed; inactivity sequence had unfinished R2 shipped-binding case. No assertion that stderr state-dir message caused abort or exact await-stack cause. Primary A+B check confirmed no missing outbound production lift-load request: C1 travels through DockWithObject.Field8; Pickup InitInternal557412 MOV0/BXLR, C5 animation-only; Verify no separate4F. Firmware interpretation/timing remains unclaimed.

Test-only fixture patch: four named Navigation positives call local SupplyRequestedPickupLoad; R2 TheShippedBindingPlacesTheCubeThroughTheBehaviorManager admits equivalent local input. Compose existing OnDockResult first; inspect last actual pickup DockWithObject, assert Field8 and send DA loaded only if true. Constant standalone M action clock100 separates positive nonelapsed action from transport pumping. All old assertions/scenes/frames/manager clocks remain. Dedicated R2 100/129.9/130 and prior controlled-poll methods untouched. Clock audit: Workout uses behavior Clock for its own sequence; Bring M.ClockSec only admission/history/failure stamp, not active-phase mandatory wait. D1/D2 native owners unchanged.

Run ledger (same explicit dotnet/project/results directory as above):

| Stem | Filter/extra options | Actual result |
| --- | --- | --- |
| fullgate-nav-r2-1 | NavigationTests OR R2FloorPlacementTests; rebuild template | INTERRUPTED EXIT1 after about2min, no completed counters/finalTRX; only start banner. No pass/fail inferred. |
| fullgate-nav-stack-1 | --no-build, exact Navigation M15_008_TheStackBranchComesBeforeThePickupCubeReportAndReportsStackCube | EXIT0 1P/0F/0skip826ms |
| fullgate-r2-binding-1 | --no-build exact R2 TheShippedBindingPlacesTheCubeThroughTheBehaviorManager; --blame-hang-timeout60s --blame-hang-dump-type none | EXIT0 1P/0F/0skip1s |
| fullgate-nav-base-1 | --no-build exact Navigation BuildPyramidBasePicksUpOneCubeAndPlacesItBesideTheOther; same60s watchdog | EXIT0 1P/0F/0skip675ms |
| fullgate-nav-bring-1 | --no-build exact Navigation ThinkAboutBeaconsThenBringCubeToBeaconPlacesTheCubeInside; same watchdog | EXIT0 1P/0F/0skip966ms |
| fullgate-nav-workout-1 | --no-build exact Navigation TheWorkoutBehaviourLiftsStrongThenWeakAndPutsTheCubeDown; same watchdog | EXIT1 watchdog ABORT, unfinished Workout, no completed assertion result |
| diagnostic preparation1 | PowerShell temporary trace-hook command | PARSE EXIT1 before any mutation/test; missing expression after comma |
| fullgate-nav-workout-diagnostic | rebuild exact Workout; same watchdog, temporary UTF8 Step journal only | EXIT1 watchdog ABORT; exact Navigation file bytes restored finally and comparison TRUE. Journal: actual dockSucceeded/PickupVerifySuccess, five strong/one weak steps, WorkoutPutDown, start PlaceObjectOnGround, no completion. |

Actual missing positive input after pickup now exposed: shared Rig PlaceObjectOnGround response is PickAndPlaceResult only; no raw picking-status pulse. Constant action clock prevents the old timeout escape whose ignored result could masquerade as positive workout completion. No permanent further correction yet; require actual action latch high→latched→low and success proof, not timed pulse or advancing clock to accept timeout. Finite test-callsite scan found other direct Pickup positives already covered by previous focused classes/DA inputs; remaining constructor-only/helper tests do not run pickup. No other fixtures silently changed.
Final bounded workout correction: optional INTERNAL ManualPutDownStartedForTest observes the SAME locally created PlaceObjectOnGroundAction and SAME RunAsync Task, default null; no production-retained action/task, replacement executor, changed await/result-ignore policy or extra message. Test supplies high raw picking-status until ACTUAL StatusLatched, then low, and asserts actual Task result Success plus every old workout assertion. Frames are withheld only while actual floor Task is active to avoid Frame's default-low state destroying the latch input. Finally captures AsyncWorkCompletion before Stop, clears observer, cancels/stops, drains/joins original work and floor Task before Rig disposal. B checked this observation/lifetime design.

| Stem | Filter/extra options | Actual result |
| --- | --- | --- |
| fullgate-nav-workout-2 | rebuild exact Workout;60s testhost watchdog | EXIT1 watchdog ABORT/no completed result. Actual PlaceOnGround drops carrying, so the original frames predicate sent default-low before poll; positive high input was lost. |
| fullgate-nav-r2-2 | rebuild NavigationTests OR R2FloorPlacementTests;90s testhost inactivity watchdog, none dump | EXIT0 83P/0F/0skip13s after latch-driven frame hold and cleanup. This diagnostic watchdog is not an action timeout/oracle change. |

Final three-code/test-file hash artifact `.scratch/pickup-pyramid-1/gatefix-source-hashes.txt`, LF UTF8 SHA256 `ee30a556fbe055f54dcfc2d34132f31f72b3f01ed757026ef98484d37df4bde3`. R2 complete public-method body comparison against HEAD confirms ONLY TheShippedBindingPlacesTheCubeThroughTheBehaviorManager changed; all prior timeout/poll/status cases byte-equivalent after newline normalization. Diffcheck EXIT0. No active testhost; source/tests frozen for independent affected review. Manager full gate is still a recorded ABORT, not success/READY; no full suite rerun by builder.
B independent final gatefix check: explicit dotnet test csproj --no-build --no-restore --nologo -v:q --filter 'FullyQualifiedName~NavigationTests|FullyQualifiedName~R2FloorPlacementTests'; independent-gatefix-nav-r2.{log,trx}, EXIT0 83P/0F/0skip13s. All three raw-file hashes unchanged before/after; R2 outside shipped-binding method independently LF-normalized byte-equivalent. B bounded BUILD PASS, no whole-behavior/full-gate acceptance claim.
