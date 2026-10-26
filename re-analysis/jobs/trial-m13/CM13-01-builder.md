# CM13-01 builder report

State: DONE for the bounded build, awaiting independent review; final focused run passed. No record settled. Commit: none (manager owns integration).

Configuration: this is the assigned Codex builder. Manager reports requested model gpt-6.1-sol, reasoning medium for the builder spawn. Actual selected runtime model/configuration and service tier are unobservable from this worker runtime.

## Scope and production path

Only `Vision/SourceQuadTree.cs`, `Vision/MemoryMap.cs`, `Manipulation/LatticePlanner.cs` the new `M13NativeMapImportTests.cs` and one necessary source-corrected expectation/name in `NavigationTests.cs` are changed. No manifest, inventory, evidence, Claude-check, queue, project-state or git mutations. No physical commands or hardware acceptance. Final full suite intentionally left to manager after independent review.

The actual path is admitted RobotState dispatch -> contact observation -> located charger creation -> native MapComponent object add -> base type4 cells; `DriveToPoseAction.Init` -> `PathComponent.StartDrivingToPose` -> actual planner selection/StartPlanner -> `NativeLatticePlannerRuntime.ComputePath` -> planner import mutex/cache gate -> current MemoryMap -> FindContentIf -> native tree FindIf -> type3 predicate -> stored polygon copy -> existing expansion pipeline. The dispatch regression supplies a synthetic prior obstacle snapshot to establish that the real import replaces it. It claims production-path dispatch, not untouched startup end-to-end or physical path-following acceptance.

Root cause: the previous import enumerated located objects, recomputed footprints, included the charger and imposed carried/ignore-ID filters. This bypassed source map admission and ignored the stored data+0x18 polygon.

## Adopted evidence and exact change

Construction uses only CM13-01's adopted contract: OV42 Q1-Q6 and the 20261015 adopted check, observable payload from B-M13-24 and its Claude check, retained B-M13-30/30a gate. Current M13-003/M11-074/M13-030 statuses remain IMPLEMENTATION_GAP; no claim of whole-record completion.

- MemoryMap wrapper 0x00681674..DC forwards predicate/output without clearing.
- Processor 0x0068A5F8..A7D2 constructs MakeQuadXY(root,0), ImportQuad2d and FastPolygon, using the existing tree geometry gate/helpers.
- Node 0x006884FC..88754 visits overlapping children in physical order, then own nonnull data, requires predicate result exactly1, reloads own data after callback and deduplicates data references. Preexisting output remains; first-found order follows the approved M11-047 departure.
- Consumer 0x004FDF18..8C / predicate 0x0050241C..2C selects only type3, then copies stored polygon vertices into unchanged ConvexPolygon/radial/C-space expansion. No world footprint fallback and no ObjectId admission filter. Public carried/ignore arguments remain for signature compatibility and are documented.
- Existing mutex, unsigned timestamp cache comparisons and tight/non-tight paths remain. Absent map null dereference 0x004FD50C is a visible M1-034 skip retaining snapshot/cache. Forged base3 layout at 0x004FDF64 skips only that entry with M1-034/M13-003/M11-074 diagnostic; valid entries remain. No base3 production producer added.
- Robot quad Pose3d roundtrip and unrelated path algorithms untouched.

Call/side-effect accounting in adopted ranges: predicate copies/destructors and shared-pointer refcounts are managed delegates/references; wrapper forwards output unchanged. MakeQuadXY, polygon import and FastPolygon calls use existing geometry. Recursive FindIf and predicate calls are built in the live tree; insertion is reference identity into the retained output with approved order. Consumer ConvexPolygon fast/slow-emplace paths both feed the unchanged existing polygon pipeline; shared-pointer temporary lifetimes are managed references. No sends, engine timers or native state stores added by the query. Existing gate's cache write remains before rebuild. Diagnostic policy handling is on the stack channel, never engine log MISSING entries.

## Test reach

M11-074 query unit tests substitute topology/payloads: physical postorder/internal-node data; no-overlap rejection before callback; reference dedup versus separate equal-ID payloads; preserved existing output; exact1; callback reload; root-generated geometry.

M13-003/M11-074 import unit tests substitute stored payload, and prove stored geometry governs inclusion even with no located object, carried/ignore do not exclude type3 and snapshot survives payload/map mutation. Normal-height22/high200 cube tests substitute object pose but use actual AddLocatedObject and native height admission before import; explicitly not camera-observation coverage. Base types4/6/7/8 excluded. Forged-layout policy and absent-current-map snapshot/cache retention covered.

Production dispatch test `ContactCreatedChargerIsExcludedThroughRealPathComponentDispatch` uses admitted real RobotState messages, contact-created charger, live PathComponent selection and native planner dispatch, with a synthetic prior environment snapshot. No substituted import helper.

M13-030 retained `M13RootTimestampImportTests` cover timestamp construction, unsigned older/equal/wrap cache, newer import, repeat tight retention, non-tight unconditional rebuild, per-planner cache and mutex contention.

## Commands and actual results

All commands run in the manager-supplied isolated worktree. Python executable: `C:/Users/JimBu/AppData/Local/Programs/Python/Python313/python.exe`; dotnet executable: `C:/Program Files/dotnet/dotnet.exe`. Build PATH prepended those real executable directories, avoiding denied shims.

1. Read PROJECT_STATE.md, AGENTS.md, CODEX-BUILDER.md, CHECKLIST.md and CM13-01.md. Initial combined output truncated; checklist was separately read again.
2. Local `known.py` calls 0x00681674, 0x006884FC and 0x004FDF18 failed because the original evidence junction lacks index.tsv. Manager supplied its already-prepared copy: absolute `.../.scratch/worktrees/codex-m13-manager/re-analysis/tools/known.py` on those three targets succeeded. Also ran known.py for 0x0068A5F8 and 0x0050241C. Reopened .so instructions with local capstone/lief for wrapper, processor, node, consumer and predicate, using resources/lib/armeabi-v7a/libcozmoEngine.so. No new extraction beyond the adopted contract.
3. Initial `dotnet test ... --no-restore --filter FullyQualifiedName~M13NativeMapImportTests|FullyQualifiedName~M13RootTimestampImportTests` failed NETSDK1004 (new worktree had no assets). Default restore/test quiet and normal returned failure without diagnostic errors. Retried `-m:1 -nr:false`, restore succeeded using cached packages; NU1900 audit-network warning remained.
4. First compile found a builder-introduced namespace qualification error for StackDiagnostics; corrected to the existing Cozmo.Robot owner. Next compile succeeded; sandbox test run aborted after90s unable to connect local testhost, no assertions executed. Manager authorized required offline test execution outside sandbox.
5. Escalated `dotnet test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-restore --filter 'FullyQualifiedName~M13NativeMapImportTests|FullyQualifiedName~M13RootTimestampImportTests' --verbosity quiet -m:1 -nr:false -clp:ErrorsOnly`: PASS25/25, zero skipped, test duration1s.
6. `python -I re-analysis/tools/logkey_check.py` on the three touched production files: PASS15 EXACT; COMPOSED0 PART0 ABSENT0. No new engine log keys added.
7. `git diff --check`: PASS (line-ending normalization warnings only).
8. Old-loop negative control: Python temporarily substituted ONLY the baseline environment import method body, using read-only `git show HEAD:.../LatticePlanner.cs`, ran tests, and restored corrected bytes in `finally`. Filter covered charger dispatch, real cube height admission and stored-polygon ownership. Result expected FAIL3/PASS1/skip0: charger dispatch, high200 cube and stored-polygon tests FAIL; admitted normal22 cube control PASS. No helper absence/compile failure caused these failures. Test exit1, wrapper exit0 after restoration. Corrected source restored byte-for-byte.
9. Broader corrected focused run (native/timestamp/observable/X1/X2/Navigation/float-lint classes): FAIL1/PASS96/skip0, duration46s. Existing `NavigationTests.DriveToObjectWithThePlannerTreatsOtherCubesAsObstaclesButNotTheTarget` failed only at line234 (expected0, actual1); confirmed by an escalated `--no-build --no-restore` single-test rerun with console verbosity normal. Its target-exclusion oracle contradicted adopted predicate0x00502424 and stored-polygon reader0x004FDF64 (no target-ID filter). Manager approved source-based correction: renamed `DriveToObjectWithThePlannerIncludesTheAdmittedTargetPolygon`, count1, explanatory native citations; retained action success and chosen-pose arrival assertions. This repairs a contradicted expectation rather than weakening coverage.
10. Final corrected focused run including the above classes plus `NoSourceFileWritesAMissingIntoTheEngineLogStream`: PASS98/98, zero skipped, duration39s. Exact filter: `FullyQualifiedName~M13NativeMapImportTests|FullyQualifiedName~M13RootTimestampImportTests|FullyQualifiedName~M13ObservableMapTests|FullyQualifiedName~M13X1BuilderTests|FullyQualifiedName~M13X2BuilderTests|FullyQualifiedName~NavigationTests|FullyQualifiedName~FidelityLiteralLintTests|FullyQualifiedName~NoSourceFileWritesAMissingIntoTheEngineLogStream`; same dotnet project and `--no-restore --verbosity quiet -m:1 -nr:false -clp:ErrorsOnly`, escalated local testhost. Final source/test bytes stable; no full suite run.

## Changed locations

- SourceQuadTree.cs: node FindIf near314, tree FindContentIf near432.
- MemoryMap.cs: wrapper near113.
- LatticePlanner.cs: source summary near549, native stored-polygon import near771, absent-map policy near968.
- M13NativeMapImportTests.cs: focused new query/import/policy/admission/dispatch tests.
- NavigationTests.cs: source-corrected target expectation/name near221..236.

## Remaining uncertainty

No new MISSING within this narrow adopted join. Whole M13-003 and M11-074 remain incomplete under their unchanged manifests. Camera observations and broader native object admissions are not proven by substituted cube fixtures. Unrelated robot-quad Pose3d roundtrip and remaining planner paths are outside job scope. No hardware fidelity claim, status reclassification or acceptance.

## Full-gate correction (2026-10-26)

Before relying on the current records, their current titles/status/evidence were reread and are quoted here. No record changes.

```json
{
  "id": "M13-033",
  "title": "Planner side of PathComponent: planner construction, SelectPlannerHelper, lattice ComputePath/ComputeNewPathIfNeeded/StartPlanning (both force values, safe-prefix retention), async worker and F0-F8 flags, StopPlanning guard, GetCompletePath, the short planners, IPathPlanner wrappers, slot 0x20/0x18 safety dispatch",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-m12-032-path-status.md: \"Unread\" section: SelectPlannerHelper 0x0064A5BA..0x0064AA18; LatticePlanner 0x004FE688, 0x004FEB00, 0x004FD1A2, 0x004FF936; FaceAndApproach/MinimalAngle vtbl+0x2C; ComputeClosestGoalPose 0x005087AC; ApplyMotionProfile 0x00508AE4; planner status base 0x00508A94..0x00508AAA.",
    "re-analysis/research/20261013-planner-internals-check.md: ctor 0x00648AE0..0x00648C54; SelectPlannerHelper 0x0064A530..0x0064A8DA (not 0x0064A5BA..0x0064AA18); ComputeNewPathIfNeeded 0x004FEB00..0x004FEB34; StartPlanning 0x004FEB44..0x004FF452 (false branch, prefix 0x004FECDE..0x004FECF6, Append 0x00500540); worker 0x005007E0..0x00500854; DoPlanning 0x00500090..0x00500590; StopPlanning guard 0x004FD1A2..0x004FD1CC; lattice GetCompletePath 0x004FFB64..0x004FFE2E; FaceAndApproach 0x004F35A8..0x004F38F6; MinimalAngle 0x00503C18..0x005041B2; IPathPlanner 0x005087AC..0x00508AE2, 0x005091CC..0x0050928A (base internal returns 0 when +0x28 == 0); safety dispatch slot 0x18 0x00649FB6 -> 0x00509202 -> slot 0x1C."
  ]
}
```

```json
{
  "id": "M11-067",
  "title": "cv::Rodrigues (OpenCV 3.1.0, both directions, CV_32F in, double inside, Jacobi SVD) and the pose-tree copy semantics (deep copy ctor, in-place operator=, shared-node SetParent, \"_COPY\" names)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-pose3d-arithmetic.md: \"Not read\" section: cv::Rodrigues via RenormalizeHelper 0x008499B0..0x00849A10, Rotation3d(RotationMatrix3d) 0x0084A3F4..0x0084A45C, RotationMatrix3d(angle, axis) 0x0084ADA4..0x0084AF34; PoseBase::operator= 0x00845940, SetParent 0x00844A14, PoseTreeNode ctors 0x00845F44/0x00845FE4; UnitQuaternion<double>*Point3d 0x00849F90..0x0084A062; Euler/matrix X/Y 0x0084AF98..0x0084B424; IsValid 0x008497D0..0x008498E4; Abs 0x0084B51E; ApplyTo(vector) 0x0065671C; the Pose2d/Transform2d family 0x0084B619..0x0084B92A.",
    "re-analysis/research/20261013-rodrigues-and-pose-tree.md: cvRodrigues2 calib3d 0xF28E..0xFB9A (vector->matrix in double, DBL_EPSILON identity, R = c1*rrt + c*I + s*[r]x; matrix->vector cvCheckArr [-100,100) -> zero, cvSVD -> JacobiSVDImpl_<double> core 0x595C0..0x59B56 with OpenCV hypot 0x56EB0, GEMM U*Vt, s<1e-5 and 180-degree branches, acos), wrapper 0x111F8..0x1133E; PoseBase copy ctor 0x008458F0..0x0084592A, node copy ctor 0x00845FE4..0x00846044, operator= 0x00845940..0x00845A2E, SetParent 0x00844A14..0x00844A7A, node SetParent 0x008449EA..0x00844A10, WrapExistingNode 0x0084529C..0x008452C0, GetWithRespectTo write order 0x00845A34..0x00845E04."
  ]
}
```

```json
{
  "id": "M11-084",
  "title": "Pose wrapper lifetimes in production: whether any flow leaves a child under an unowned parent or drops an origin's last wrapper outside ~PoseOriginList (UnownedParent, NotLastOwner, ZeroOwners, ZeroOwnersWithID VERIFYs on a running robot)",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "re-analysis/research/20261014-pose-tree-verifies.md: \"Still unread\": parent-pose lifetimes (robot +0x298, origins, object poses, temporaries), PoseOriginList erase/rejigger paths, inline wrapper attaches writing PoseBase+4 without +0x48, cross-thread pose-tree mutation.",
    "re-analysis/research/overnight/20261016-ov121-answer.md: AddRobotRelativeObservation 0x00506B70..0x00506C48; SetPoseHelper 0x0050609C..0x00506116; SetCarriedObjectAsUnattached 0x006333D4..0x00633930; MarkObjectUnobserved/Unknown 0x00506FBC..0x005073FC; CheckForUnobservedObjects filter 0x00621C6C..0x00621D88; LocalizeToObject rejigger 0x005158D8..0x005159B2; UpdateObjectOrigins 0x00620534..0x006206F0 and lambda1 0x00628E18..0x006292F6; CopyWithNewPose 0x00506EF8..0x00506F40; ~ObservableObject 0x004E0D78..0x004E0DA0; DriveToPoseAction::Init 0x0055A8A6..0x0055A91E."
  ]
}
```


### Failure classification and corrected fixture lifetime

Manager full-suite gate aborted after5009 passed tests, not a suite pass. Blame identified `ContactCreatedChargerIsExcludedThroughRealPathComponentDispatch`; worker exception was Pose3d.RequireNode -> Translation -> LatticePlanner.ToStateC -> goal enumeration -> NativeLatticePlannerRuntime.DoPlanning -> Worker. Read manager `.scratch/test-full.log` and traced the current complete C# path before editing.

Actual path: DriveToPoseAction.Init stores its Goal wrapper in NativePlannerSet.Configure; ComputePath copies the goal list (`ToArray`) while retaining each wrapper reference, and schedules F0 work. Worker later enumerates `_goals` through ToStateC, which reads Pose3d.Translation/RequireNode. Drive.Dispose aborts/stops but does not join. ManipulationSystem.Dispose -> NativePlannerSet.Dispose -> NativeLatticePlannerRuntime.Dispose setsF1/wakes and joins the worker. Worker can execute pendingF0 work before observingF1. Pose3d.Dispose clears its wrapper Node. These are the current implementation facts; no new native ownership semantics inferred from them.

Original test declarations disposed in reverse order: drive -> goal (Node=null) -> manipulation system/worker join -> rig. This is a test/harness lifetime bug: the test explicitly disposed a borrowed asynchronous input before ending its consumer. It is not evidence that a speculative production clone or worker catch is source-faithful. Current M11-084 production lifetime questions remain RECOVERABLE_GAP, unchanged. Separately, the current worker retains Pose3d references: this test cleanup correction does NOT establish faithful native goal-copy or start-snapshot ownership and does not make early-disposal API behavior safe. M11-084 goal-copy window remains a real production boundary, not excluded or claimed to have no effect.

Exact correction: declare the goal before the manipulation system and keep drive after it, giving deterministic reverse cleanup drive -> manipulation system/worker join -> goal -> rig, on both normal return and assertion exceptions. Only the focused test declaration location and explanatory comment changed; all dispatch, selection, map, stale-snapshot and count assertions remain. No production code, hardware harness, manifest, inventory, evidence or status changed. No blanket worker exception handler, pose copy or source workaround.

Focused regression: explicit dotnet test same project, `--no-restore --filter FullyQualifiedName~M13NativeMapImportTests|FullyQualifiedName~M13RootTimestampImportTests --verbosity quiet -m:1 -nr:false -clp:ErrorsOnly`: PASS25/25, zero skipped, duration1s.

Bounded cleanup stress: Python subprocess loop20 times ran explicit dotnet test same project with `--no-build --no-restore --filter FullyQualifiedName~ContactCreatedChargerIsExcludedThroughRealPathComponentDispatch --verbosity quiet -m:1 -nr:false`; PASS20/20 separate testhost runs, each one passed/zero skipped, no host abort. No artificial delay, worker catch or source substitution. Both commands used authorized escalation for offline local testhost IPC and real Python313/dotnet paths as earlier in this report. `git diff --check` PASS; logkey precheck on sole changed test file finds0keys/0ABSENT (no production source changed).

No full suite run by builder; manager reruns gate after independent affected-diff review. No commit or git mutation. Correction is complete for review.
