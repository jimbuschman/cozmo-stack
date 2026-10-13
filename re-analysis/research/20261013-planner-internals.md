# R-PLANNERS — planner internals (2026-10-13)

## Item 1 coverage — SelectPlannerHelper

| Step | Recovered behavior | Primary address / boundary |
|---|---|---|
| S1 | Convert goal to Pose2d; obtain Robot pose, convert it relative to its root, then to Pose2d. | 0x0064A530..0x0064A55A; Robot pose helper 0x004EA398; root conversion 0x004DF628..0x004DF6DC |
| S2 | Subtract x/y in binary32; widen each difference to binary64, square separately, add, narrow sum to binary32. This is distance **squared**, not distance. | 0x0064A55E..0x0064A5A6 |
| S3 | Distance squared >=1600 selects lattice. For finite values <1600, compute absolute wrapped goal-heading difference and absolute wrapped initial bearing difference, using Robot's current pose for the latter. | 0x0064A5AA..0x0064A686 |
| S4 | Short MinimalAngle iff distance squared >1, absolute goal-heading delta <= float(pi/4), and absolute initial-turn delta > float(pi/16). Otherwise short FaceAndApproach. | 0x0064A65A..0x0064A686; three comparisons are `ble`, `bhi`, `ble` |
| S5 | Log one of ShortMinAngle, Long, Short, with exact formats listed below. | 0x0064A688..0x0064A7E0 |
| S6 | Copy selected shared-pointer pair from +18/+1C, +8/+C, or +10/+14 to current +20/+24; retain new control block before releasing old. | 0x0064A704, 0x0064A75C, 0x0064A7E0..0x0064A7F4 |
| S7 | Event `robot.planner_selected`: text is planner name; `$data` is decimal integer conversion of roundf(distance squared). | 0x0064A7F8..0x0064A86C |
| S8 | If current object pointer equals lattice pointer +8, clear fallback +28/+2C; otherwise copy lattice shared pair there, retaining new and releasing old. | 0x0064A8A0..0x0064A8C4 |
| S9 | No fourth planner branch in this helper. Original C++ names of unnamed pose helper functions are not recovered from symbols; their code/diagnostic names establish the operations above. | Entire 0x0064A530..0x0064A8DA; helper source identifiers UNKNOWN |

## Item 2 coverage — lattice entry points, status and worker

| Step | Recovered behavior | Primary address / boundary |
|---|---|---|
| L1 | ComputePath tries recursive context mutex. Acquired + pending F0 rejects request; failed try-lock + pending/running rejects; failed try-lock with neither flag waits for lock, then rechecks. | 0x004FE688..0x004FE748; wait 0x004FE7D0..0x004FE7D6 |
| L2 | Import obstacles with bool 0. Impl F4 true uses ComputePathHelper; false selects one closest acceptable goal, with penalty thresholds 0.01 then 1000, strict penalty `<` and strict distance improvement; preserves first tie. | 0x004FE74A..0x004FE9E2 |
| L3 | Candidate distance uses x/y/z binary32 differences, x² then +y² then +z². Discretize x/y by reciprocal resolution + roundf; heading uses repeated binary64 +/-2pi with binary32 narrowing, scale/round/modulo, then GetCollisionPenalty. | 0x004FE7F6..0x004FE976 |
| L4 | Helper copies goal poses to impl C0, clears previous plan/context goals, keeps GoalIsValid candidates with byte IDs, calls virtual ComputeNewPathIfNeeded(start,true); no valid goals returns 0. Lock/pending gates also apply. | 0x004FD1D0..0x004FD3EC; force argument 0x004FD32E..0x004FD330 |
| L5 | ComputeNewPathIfNeeded returns 2 on mutex try-lock failure; otherwise returns StartPlanning's code and releases mutex. | 0x004FEB00..0x004FEB34 |
| L6 | StartPlanning recursive try-lock failure logs an internal error and returns 0. Successful entry stores force bool at A1, current x/y/yaw as State_c. | 0x004FEB44..0x004FEC2A |
| L7 | Force true discards old plan. False imports tight obstacles and finds closest old-plan segment; distance >=20 discards old plan; otherwise PlanIsSafe(40,index,outputs) can return 2 without starting work. Unsafe prefix is retained for replan. | 0x004FEBAC..0x004FED48; safe return 0x004FF410..0x004FF430 |
| L8 | Store new start context; StartIsValid/GoalsAreValid failure returns 0 with respective logs. Import wide obstacles, PrepareForPlanning, optional context dump, then set F2=1. | 0x004FEEBC..0x004FF310; failures 0x004FF364..0x004FF3C8 |
| L9 | Async: pending F0=1 and status F8=1, internal CV mutex lock/unlock, notify_all. Sync: call DoPlanning directly. Successful dispatch returns 1 in both modes, independent of published search result. | 0x004FF30A..0x004FF362, 0x004FF432..0x004FF452 and return cleanup |
| L10 | StopPlanning clears F2 only if DC thread object exists AND F3 running is nonzero. Does not clear pending F0, change F8, notify or join. | Outer 0x004FD1A2..0x004FD1B8; impl 0x004FD1BA..0x004FD1CC |
| L11 | CheckPlanningStatus simply reads impl F8, without context lock. | 0x004FD192..0x004FD1A0 |
| L12 | Constructor initializes F8=0, F0/F1/F3/F5=0, F2/F4=1, artificial delay108=0; creates worker even after ordinary data-platform/asset error paths. | 0x004FCCC2..0x004FCCF6; joined constructor paths 0x004FCF1C..0x004FCF60 |
| L13 | std::thread packages member function and impl pointer, calls pthread_create. Thread proxy invokes member. Failure throws system_error. | 0x00500B88..0x00500BE4; proxy 0x00500D40..0x00500D82 |
| L14 | Worker locks E0, waits while no pending and no exit; pending sets F3=1, clears F0, calls DoPlanning under lock, clears F3 and unlocks. Exit rechecked afterward. | 0x005007E0..0x00500854; wait/unlock/relock adapter 0x0050086C onward |
| L15 | DoPlanning publishes 1; delay sleeps min(remaining,10) ms, polling F2. Leaving delay because F2=0 still calls Replan(30,000,000,&F2). | 0x00500090..0x00500102 |
| L16 | Replan failure publishes 0; successful empty search plan publishes 3; nonempty path continuation publishes 2. An old/new-plan mismatch additionally publishes a transient 0 before eventual 2. | 0x00500200..0x00500218 -> 0x00500590; mismatch store 0x005004DE; success 0x0050058E..0x00500590 |
| L17 | GetCompletePath rejects status0/1. For other published values, writes chosen goal ID, finds nearest plan action; offset >=20 clears old-plan actions and publishes 0. Success does not reset F8. | 0x004FFB64..0x004FFC28; error 0x004FFDCC..0x004FFE2A |
| L18 | Destructor sets exit F1, CV mutex lock/unlock and notify_all, logs before/after join, joins then destroys/deletes thread. Does not itself clear F2. | 0x005018E8..0x005019A2 |
| L19 | Safety wrapper delegates through xythetaPlanner/impl to environment PathIsSafe. Empty input returns false without clearing output; nonempty output starts cleared, accumulates penalties, appends only safe prefix, fails when sum >1e-5. | 0x004FF936..0x004FF940; 0x00858588..0x00858594; 0x00850E34..0x00850F16 |
| L20 | ApplyPathSegment's inner collision geometry and source obstacle import semantics remain dependencies, not newly certified by reading these wrappers. | Call 0x00850E96; M13-003 and existing navigation records; inner geometry UNKNOWN in this report |
| L21 | PathComponent observes status each update while planningActive; 1 waits, 2 completes, 3 reports ready/no plan, 0 uses fallback/fails. Restart passes force **false**, handles return1/0/other separately. | 0x006495E0..0x0064972E; 0x00649C14..0x00649D14, false at 0x00649C36 |

## Item 3 coverage — short planners

| Step | Recovered behavior | Primary address / boundary |
|---|---|---|
| F1 | Face ComputePath copies goal xyz and yaw, calls virtual ComputeNewPathIfNeeded(start,true), returns its result directly. | 0x004F355C..0x004F35A4 |
| F2 | Clear plan-present28 first. Force false immediately restores28=1, returns2, retains path. Force true returns1 after generation and restores28=1. | 0x004F35B4..0x004F35C0; 0x004F3806..0x004F380E; 0x004F38E8..0x004F38F6 |
| F3 | Line iff narrowed double sum of squared float x/y differences >100. Otherwise travel heading stays start heading. If line, bearing=atan2f(dy,dx). Final turn flag is computed before reverse adjustment; initial turn requires line and abs(delta)>double 5deg. | 0x004F35EA..0x004F3730 |
| F4 | If abs(initial delta)>double60deg, print backup diagnostic; add float pi using Radians to initial delta, final delta and travel heading; line speed=-30. Otherwise +30. No separate backup segment. | 0x004F3792..0x004F3834 |
| F5 | Clear and append optional initial point turn, line, optional final point turn. Turn speed signed1.5, accel/decel10, tolerance float2deg, shortest-direction flag1; line accel/decel200. No line/final-turn branch does not clear previous path. | 0x004F3810..0x004F38EE |
| A1 | Minimal ComputePath copies goal xyz/yaw, calls virtual ComputeNewPathIfNeeded(start,true), returns result directly. False returns2 retaining path; true clears path, generates and returns1. | 0x00503BC2..0x00503C62; 0x00503DBA..0x00503DC2; 0x0050419A..0x005041B2 |
| A2 | Compute backup amount using the literal/branch recipe below, cap75, append backup only >2.5; TranslateForward(-amount) on copied pose; line speed=-45, accel/decel200. | 0x00503C66..0x00503DB8 |
| A3 | From updated cursor compute atan2f, wrapped turn delta and distance (float subtraction, double squares/sum/sqrt, narrow). Distance>2.5 permits initial turn iff widened absolute delta>double0.01, then forward line. | 0x00503DDC..0x00503EA2; append 0x00503F06, 0x00503FD4 |
| A4 | Initial/final turns use signed1 rad/s, accel/decel10, tolerance float2deg, flag1. Forward line speed45, accel/decel200. Cursor is updated with Pose2d TranslateForward(distance), not assigned goal x/y. | 0x00503EA4..0x0050403C; final turn 0x00504040..0x0050412C |
| A5 | Final wrapped heading delta uses updated cursor; append iff absolute delta>double0.01. Logs Backup/Turn0/Straight/Turn1 and FinalPosition are conditional as detailed below. | 0x00503D50..0x00504174 |
| A6 | Pose TranslateForward separately invokes cosf and sinf on stored angle, multiplies amount and adds to x/y as binary32. | 0x0084B618 onward (saved native dump) |

## Item 4 coverage — IPathPlanner

| Step | Recovered behavior | Primary address / boundary |
|---|---|---|
| I1 | ComputeClosestGoalPose considers full xyz distance squared, binary32 operations x²,+y²,+z²; first candidate always seeds best, later strict `<` preserves first tie. Empty returns0; final index is byte. | 0x005087AC..0x0050886E; loop/return 0x0050890E..0x00508954 |
| I2 | Debug log each candidate with translation, heading degrees, rotation-axis components and squared distance. | 0x00508870..0x0050890A; format at 0x00BE5F54 |
| I3 | Base multi-goal ComputePath clears bytes28/29, selects/stores byte2A, tail-calls slot2C with chosen Pose3d. Empty vector is not protected before dereference. | 0x00508A60..0x00508A8E |
| I4 | Base CheckPlanningStatus: error29 ->0, present28 ->2, otherwise3. | 0x00508A94..0x00508AAA |
| I5 | GetCompletePath calls internal slot30 (slot34 for goal-index overload). Internal result must equal1. Optional profile transforms caller output into planner-owned path2C then copies back; absent profile copies output into owned path2C. | 0x00508AAC..0x00508AE2; 0x005091CC..0x00509200 |
| I6 | Base internal GetCompletePath copies owned path2C into output; index overload also copies byte2A; returns1. | 0x0050924E..0x00509286 |
| I7 | Lattice internal builds output from nearest old-plan action through AppendToPath, removes trailing point turns, and appends desired final-heading correction unconditionally for a nonempty generated path. | 0x004FFC68..0x004FFDC4 |
| I8 | ApplyMotionProfile clears destination, walks source backward, copies 40-byte segments; preserves direction from source speed sign; chooses linear/profile or angular/profile settings. | 0x00508AE4..0x00508C64 |
| I9 | Reverse pass performs binary32 deceleration propagation with tolerances1e-5 and1; possible sqrtf fallback; point turns break speed continuity. | 0x00508C14..0x00508D90 |
| I10 | Forward pass computes boundary speed, fixes signs/minimum20, may split a line's deceleration tail if tail length lies strictly within segment and at least5mm short of full length. | 0x00508D9A..0x0050906E |
| I11 | Unknown segment kind logs warning and returns0 with destination already cleared; ordinary completion returns1. Allocation/throw paths unwind. Caller does not test ApplyMotionProfile's return. | 0x00509070..0x005090DE; call sites 0x00508ACE, 0x005091F4 |
| I12 | Low-level Path capacity/AppendSegment failure behavior and all collision/primitive AppendToPath internals are not established by these entry-point reads. | Calls 0x00508F30, 0x00509062/0x0050906A, 0x004FFC70; dependency internals UNKNOWN here |

## Item 5 coverage — virtual slot +0x20

| Step | Recovered behavior | Primary address / boundary |
|---|---|---|
| V1 | Installed lattice vtable=0x0101F188, slot20 points Thumb0x00500AC3 (code0x00500AC2), returns1. | ELF vtable symbol header0x0101F180, relocation slot0x0101F1A8 |
| V2 | Installed base vtable=0x0101F6E4, Face=0x0101EB60, Minimal=0x0101F5C4; all slot20 point Thumb0x004F1A09 (code0x004F1A08), return0. | ELF vtable headers/relocations; 0x004F1A08..0x004F1A0A |
| V3 | HandlePlanComplete skips lattice safety validation if predicate true. False + nonempty path + existing lattice runs lattice CheckIsPathSafe; false safety falls back/fails. | 0x00649F86..0x00649FC2 and continuation |
| V4 | Source method identifier is **UNKNOWN**: relocation is anonymous, and these constant bodies have no exported name. Descriptive name: “planner already performs collision checking.” Meaning is evidenced by caller; descriptive wording is inference, not recovered C++ spelling. | Anonymous symbols at V1/V2; caller V3 |

## Item 6 coverage — current stack comparison

| Step | Current behavior / difference | Checked file and primary comparison |
|---|---|---|
| C1 | Drive action chooses lattice whenever `_m.Planner` exists; otherwise local StraightLinePlanner. No native distance/angle three-planner selector or fallback pair lifecycle. | `cozmo-stack/src/Cozmo.Robot/Manipulation/DriveActions.cs:149..158`; S1..S8 |
| C2 | Init invokes StartPlanning(true), then PlanTo/DoPlanning synchronously. No per-planner worker, pending/running flags, try-lock2, CV or status polling equivalent. | `DriveActions.cs:152..156`; `LatticePlanner.cs:973..1004,1376..1383`; L1,L5,L9,L12..L18 |
| C3 | Stack deliberately omits false reuse branch as allegedly unreachable. Native PathComponent restart supplies false. This is a concrete missing production path, not a proposed policy. | `LatticePlanner.cs:963..978`; native 0x00649C28..0x00649C3A |
| C4 | StopPlanning unconditionally clears RunFlag; native guards DC and F3. LastPlanningResult captures final host result, not native asynchronous F8 lifecycle. | `LatticePlanner.cs:952,954..959,1379`; L10..L17 |
| C5 | ToPath is expressly a stand-in: primitive merging plus supplied profile; omits source nearest-action splice, trailing-turn removal, unconditional correction and two-pass ApplyMotionProfile. | `LatticePlanner.cs:1308..1370`; I7..I10 |
| C6 | StraightLinePlanner uses double distance>=5mm, 2deg turn threshold, optional reverse only above90deg, profile speeds, and atan2(sin,cos) wrap. Neither recovered short planner follows this algorithm. | `RobotPath.cs:204..239`; F1..A6 |
| C7 | Current action's live CheckIfDone reports MISSING for PathComponent status and stays RUNNING absent supplied test input; empty host path also reports MISSING. Source state is not inferred from a terminal event. | `DriveActions.cs:131..138,159..160`; L21 and context P9/P27 |
| C8 | Stack correctly uses Thread.Sleep chunks in DoPlanning, not Task.Delay as current M13-018 unresolved prose says. Matching chunks does not establish worker scheduling/path fidelity. | `LatticePlanner.cs:987..1004`; 0x005000A2..0x005000EA |

## Evidence, scope and findings

Answers [requests/20261013-planner-internals.md](requests/20261013-planner-internals.md), using the manager's [path-status context](20261013-m12-032-path-status.md). Read PROJECT_STATE first; authorized pull fast-forwarded main from `63317ceb` to `2f28c531`. Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, app3.4.0-1204, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Addresses are ELF virtual addresses with Thumb bit stripped for code. Dynamic relocations establish vtable targets; installed pointer is symbol header+8. Used local LIEF/Capstone with `skipdata=True`; plausible instructions in literal pools are data, not behavior. No shipped binary was run or uploaded.

The six tables precede the analysis. These are independent extraction findings for manager checking, not fidelity upgrades. Named subordinate calls do not certify their entire internals. No implementation, inventory, manifest, state, branch, commit, build or hardware test was changed/run.

### 1. Selection arithmetic and logs

The helper actually starts at **0x0064A530**, not the request's interior0x0064A5BA. Its normal body ends0x0064A8DA; 0x0064AA18 starts SelectPlanner, a different function. Robot.GetPose helper returns the pose at Robot+298 after origin checks; root-relative conversion occurs before selection distance. Heading/bearing computations use Robot's direct current pose (0x005477A0 accessor) rather than the root-converted temporary. Preserve that distinction instead of substituting a generic distance helper.

For ordinary finite inputs, the selection is:

```text
dx = f32(goal2d.x - rootRobot2d.x)
dy = f32(goal2d.y - rootRobot2d.y)
d2 = f32(f64(dx)*f64(dx) + f64(dy)*f64(dy))
if d2 >= 1600: lattice
else:
    headingDelta = abs(Radians(goalYaw - robotYaw))
    initialDelta = abs(Radians(atan2f(f32(goalY-robotY),
                                    f32(goalX-robotX)) - robotYaw))
    if d2 > 1 and headingDelta <= f32(pi/4) and initialDelta > f32(pi/16):
        MinimalAngle
    else: FaceAndApproach
```

This pseudocode describes finite inputs; the table retains exact ARM branches for unordered inputs. The long comparison uses `bpl` (unordered follows long); heading-delta `bhi` also sends unordered to Face. Preserve native Radians normalization, not host trigonometric wrapping. Large/nonfinite roundf-to-int conversion and malformed pose graph behavior are not supplied with guessed managed semantics.

All three selection logs are **Info**, channel `Planner`:

| Key | Exact format | Call |
|---|---|---|
| `PathComponent.SelectPlanner.ShortMinAngle` | `distance^2 is %f, angleDelta is %f, intiialTurnAngle is %f, selecting short min_angle planner '%s'` | 0x0064A6DE |
| `PathComponent.SelectPlanner.Long` | `distance^2 is %f, selecting long planner '%s'` | 0x0064A736 |
| `PathComponent.SelectPlanner.Short` | `distance^2 is %f, angleDelta is %f, intiialTurnAngle is %f, selecting short planner '%s'` | 0x0064A7BA |

Short formats use distance squared, absolute heading delta, absolute initial delta, then selected planner name. `intiial` is shipped spelling. The event at0x0064A86C occurs after current-pointer replacement and before fallback replacement; its `$data` is **rounded squared distance**, not rounded distance. Selection does not stop the old planner here; it replaces shared ownership. Only these three choices occur in this helper.

### 2. Lattice dispatch, status publication and ticks

Keep two code domains separate:

| Code | ComputeNewPathIfNeeded / StartPlanning meaning | CheckPlanningStatus F8 meaning |
|---|---|---|
| 0 | Cannot start (invalid start/goals/internal entry failure) | Failed |
| 1 | Work dispatched, including synchronous work | Planning |
| 2 | No new work: outer lock unavailable OR old remaining plan safe | Finished with nonempty new search plan |
| 3 | No ordinary StartPlanning return3 found | Search succeeded with empty new search plan |

ComputePathHelper propagates the dispatch code, not a search-success Boolean. The async worker holds recursive E0 through DoPlanning. Main ComputeNewPathIfNeeded avoids waiting by returning2 on failed try-lock. ComputePath instead can block for unrelated lock ownership when F0/F3 are both clear. Do not replace these different contracts with one semaphore or one “already planning” test.

With F4 disabled, ComputePath's acceptance passes use penalty thresholds **0.01 then1000**, not a continuous ranking by collision cost. It chooses nearest accepted goal under the first threshold that accepts any goal. The loop's candidate IDs are bytes. ComputePathHelper's usual F4-enabled branch validates each goal before adding `(byteID,State_c)` to context. Discrete packing, obstacle source and GoalIsValid internals belong to their existing navigation records; this report establishes the calls/order, not a new whole-planner guarantee.

The false branch is live: RestartPlannerIfNeeded loads current planner+20, requires planningActive47=0, calls slot+C with `r2=0` (0x00649C36), and interprets1 as new work,0 as error/fallback, all other values as no action. With a lattice current planner this reaches0x004FEB22 then StartPlanning(false). False imports tight obstacles, locates the old plan, tests remaining safety at tolerance40, and can keep an old safe plan without scheduling. Distance>=20 empties old actions; the native code does not write A1=true there, so retain the actual subsequent false-branch execution rather than imagining a force-flag assignment.

Complete F8 writer inventory for the constructor/entry/worker/retrieval bodies examined:

| Store | Value and condition |
|---|---|
| 0x004FCCF2 | 0 at construction |
| 0x004FF43A | 1 on asynchronous dispatch, alongside F0=1 at0x004FF436 |
| 0x0050009A | 1 at DoPlanning entry, both async and sync |
| 0x00500590, reached from0x00500216/0x00500218 | 0 when Replan result is0 |
| 0x00500590, reached from0x00500212/0x00500214 | 3 when successful new search plan has no actions |
| 0x005004DE | 0 temporarily on the old/new plan mismatch diagnostic path; execution continues to append/report and publish2 |
| 0x00500590, from0x0050058E | 2 after nonempty successful search/append |
| 0x004FFE2A | 0 when GetCompletePath finds robot offset>=20, after RobotPositionError event and clearing old actions |

Stack stores `[sp+F8]` in ImportBlockworldObstacles/StartPlanning are **not** status writers. All three outcomes of DoPlanning share the final store0x00500590; 0x00500218 is a branch to that store, not a separate store. Normal successful GetCompletePath leaves F8 unchanged. StopPlanning, worker entry/exit flags, and destructor do not reset it. Literal-offset scan plus manual control/data-flow inspection was used; no claim is made that arbitrary computed-pointer stores elsewhere in the entire binary were exhaustively ruled out.

DoPlanning's **published status** is the stable recovered contract. Both callers discard the returned register. After the final F8 store, string cleanup uses/clobbers r0 (0x00500594..0x0050059E); therefore calling the values0/3/2 guaranteed C++ return values is unsupported by this assembly. The current stack enum can represent search outcomes, but must not be confused with StartPlanning's return codes or with a verified native DoPlanning return ABI.

Construction always creates one thread at0x004FCF40..0x004FCF60 on normal constructor continuation; asset errors do not skip that tail. pthread_create failure throws. Worker waits through condition_variable_any with recursive lock released/reacquired; pending work is honored even if exit became set while waiting, because F0 is checked first after wake. F3 marks only worker DoPlanning; the sync branch does not set it. Consequently native StopPlanning can leave a pending request untouched and cannot be modeled as unconditional cancellation of every request. Destructor sets F1 and joins; it waits for an ongoing plan rather than itself clearing run flag F2.

Main-thread status loads are plain loads, with no lock in CheckPlanningStatus. Native scheduling determines which ticks observe1, final0/2/3, or the transient mismatch0. The source does not establish a fixed tick delay. Per context P27/P29, the robot processes messages, then ActionList, then PathComponent; an action may see the preceding PathComponent state on its tick. Async dispatch publishes1 before notifying; sync dispatch can finish before returning1, after which the next PathComponent planning update observes final status directly. Status3's no-plan path is different from status2 even though the retrieval routine accepts both.

CheckIsPathSafe passes through planner/context to environment PathIsSafe. That routine returns0 immediately for empty input (destination untouched). For nonempty input it clears destination, seeds State_c from first segment start and supplied heading, applies each segment with Boolean1 and epsilon1e-5, sums penalties in binary32, and appends whole safe segments to destination until sum>1e-5. The offending segment is excluded. Collision log at0x00850EE6 is Info/`Unnamed`/`xythetaEnvironment.PathIsSafe`, format `Collision along path segment %lu with penalty %f, total penalty %f`. This is a safe-prefix API, not merely a boolean test. Inner ApplyPathSegment geometry remains a separately owned dependency.

### 3. Short planner paths and arithmetic

Segment kinds in these paths are line=1 and point turn=3; neither short planner appends an arc.

**FaceAndApproach.** Its >100 squared-distance test uses binary32 subtraction, binary64 squares/addition, then binary32 narrowing. The travel angle initially copies start heading; only a line replaces it with atan2f(dy,dx). Final turn delta is the negative of wrapped `(travelHeading - desiredGoalHeading)`; compare widened abs(delta) with double literal `0.0872664625997` at0x004F3928. Initial delta is wrapped `(travelHeading-startHeading)`, with the same 5deg comparison. Reverse threshold is double `1.0471975512` at0x004F3930, not a rounded float threshold. Reverse mode adds float pi0x40490FDB through Radians separately to initial delta, final delta, and travel heading. It does not recompute the previously chosen final-turn flag or recheck the initial-turn threshold afterward.

Append order: optional initial point turn at start x/y targeting travel heading; line from start x/y to goal x/y with speed+30 or-30mm/s and accel/decel200mm/s²; optional final point turn at goal x/y targeting stored goal heading. Turns use sign(delta)*1.5rad/s, accel/decel10rad/s², tolerance0x3D0EFA35, flagtrue. Source clears the path when building a line or a final-only turn. **If no line and no final turn, it returns1 and sets plan-present while retaining the old path.** With a fresh object this happens to be empty; with a reused object it need not be.

Info/`Unnamed` logs, emitted before segments:

| Condition | Key / format | Call |
|---|---|---|
| Line chosen | `FaceAndApproachPlanner.Straight` / `doing straight because distance^2 of %f > %f` | 0x004F3642 |
| Final flag chosen | `FaceAndApproachPlanner.FinalTurn` / `doing final turn because delta theta of %f > %f` | 0x004F36E6 |
| Initial flag chosen | `FaceAndApproachPlanner.InitialTurn` / `doing initial turn because delta theta of %f > %f` | 0x004F3762 |
| Reverse mode | stdout printf `FaceAndApproachPlanner: deltaTheta of %f above threshold, doing backup!\n` | 0x004F37A8 |

**MinimalAngle.** Preserve the shipped backup expression; replacing it with an intuitive projection is incorrect. In binary32, before any heading-based coordinate transform:

```text
q = (startY - goalY) / bitfloat(0x3ED413CD)
dx = startX - goalX
t = -q
if q > dx: t = q
b = dx + t
positive = 0
if b > 0: positive = b
backup = 75
if positive < backup: backup = positive
if backup > 2.5:
    nextPose = cursor copy; nextPose.TranslateForward(-backup)
    append line(cursor.xy, nextPose.xy, speed=-45, accel=200, decel=200)
    cursor = nextPose
```

Literal0x3ED413CD is approximately0.414213568. The exact branch expression is established by0x00503C66..0x00503CBC; do not rewrite it as abs(dy), a rotated local-frame formula, or a geometrically equivalent shortcut.

From updated cursor, compute float dx/dy, atan2f(dy,dx), and Radians bearing-minus-cursor-heading. Recompute float dx/dy for length; widen to double before both squares/addition and double sqrt; narrow result. A NaN sqrt result calls external sqrt before narrowing. If length>2.5, compare widened absolute delta against double0.01: optional initial point turn speed±1rad/s, accel/decel10, tolerancefloat2deg, flagtrue; update cursor angle to bearing. Copy cursor and TranslateForward(length) to construct line endpoint; append+45mm/s, accel/decel200, then copy updated pose back. Finally compare desired heading minus cursor heading against double0.01, append analogous final turn if needed, and update cursor angle. Even empty output returns1/present28=1.

Conditional Info/`Unnamed` logs are `MinimalAnglePlanner.Plan.Backup` (0x00503D62, signed negative distance), `.Turn0` (0x00503F26, delta radians), `.Straight` (0x00503FF0, length), `.Turn1` (in0x00504040..0x0050412C, delta radians), each `%f`. `MinimalAnglePlanner.FinalPosition` at0x00504174 is unconditional on forced generation, `(%f, %f, %fdeg)` from the updated cursor. No logs/generation occur on force=false early return2.

Both short ComputePath entry points call with force=true and return the resulting1 directly. The separate force=false virtual branch returns2; it is used by native restart, not by their initial ComputePath. The base planning-status accessor uses their28/29 flags, with no worker state1.

### 4. Closest goal, retrieval and motion profile

Closest-goal selection ignores yaw/collision cost; full xyz squared distance controls it. The last `%f` in its debug format is squared distance despite the label `dist`. Base multi-goal ComputePath's empty-vector dereference is an undefined path; do not invent a native empty-goals success/failure response. If exposed by implementation, record the address and smallest-unit skip under standing M1-034, with visible owning-record log.

GetCompletePath's optional profile is a **second stage after internal extraction**. Native internal must return exactly1; otherwise wrapper returns0 immediately. With profile, ApplyMotionProfile(source=caller output,profile,destination=owned path2C), then copy owned path back to caller; without it copy caller output to owned path. ApplyMotionProfile's own return is ignored. Both overloads follow this order. Base internals merely copy the existing owned short path to caller and optionally emit selected byte2A.

Lattice retrieval uses nearest-action index and distance, not the exact original start state used by stack ToPath. Offset<20 permits AppendToPath(old plan,output,index); offset>=20 publishes failed status0 and clears the old action vector. For a nonempty output, trailing point turns are popped; the remaining endpoint is read, and a final point turn to exact selected goal heading is appended **without an angle-tolerance gate**. Desired-minus-end delta chooses +2rad/s only if strictly positive, otherwise-2; accel/decel10, tolerance0x3DB2B8C2 (5deg), flagtrue. This differs from the stack's optional2deg correction. An entirely empty generated output returns1 without adding this turn. The AppendToPath geometry/primitive conversion body is not certified by this caller extraction.

ApplyMotionProfile profile offsets in the read body: linear speed+0, accel+4, decel+8; turn speed+C, accel+10, decel+14; reverse speed+24 is inspected for warning. **The body does not use reverseSpeed+24 to assign negative linear speed:** initial assignment uses sign(sourceSpeed)*profile.speed+0. Its reverse-zero warning says it uses speed_mmps instead; a nonzero reverseSpeed is also not read into the speed assignment here. Do not infer intended behavior from the warning text.

Exact reverse-pass recipe, 0x00508B38..0x00508D90:

1. Copy source segment from last to first into temporary40-byte value. Derive direction by replacing low31bits of source speed with1.0, then converting to signed integer (+1/-1). For non-point-turn initialize speed=direction*profile.linearSpeed, accel/decel from+4/+8.
2. Warn on negative initialized speed and abs(reverseSpeed)<=1e-5. For point turns use abs(profile.turnSpeed) with copied source sign bit, turn accel/decel+10/+14; set boundary flag and append to reverse vector.
3. For line/arc, downstream required speed is0 at a boundary or previous reverse-vector segment speed otherwise. The candidate upstream reference is profile.speed except that first original segment or a preceding original point turn uses0 when the full input has more than one segment; a single input segment uses profile.speed.
4. Square downstream speed and upstream reference separately; required decel=(downstream²-upstream²)/(length*-2), all binary32. If abs(required)<1e-5, preserve current speed and profile acceleration/deceleration. Otherwise compare required decel to profile.decel; only if greater and excess>1 recalculate speed=sqrt(downstream²+2*decel*length), preserving direction sign bit. Native order is `length*-2`, multiply by decel, subtract downstream², negate, sqrt; do not fuse/reassociate. NaN sqrt result calls sqrtf. Set current speed/profile accel/decel; append reverse vector. Point turns reset continuity.

Forward pass traverses that reverse vector in original path order. For each segment the next original segment supplies required downstream speed, except final segment/next point turn use0; if positivity of current and downstream speeds differs, copy current sign to downstream speed. For a line compute decel-tail distance `(downstream²-current²)/(decel*-2)`. Split only when distance>0, distance<length, and abs(distance-length)>=5. Otherwise apply the native boundary rules: clamp downstream magnitude to20 when below20 or at the final segment (preserve sign). With at least two input segments and a current nonturn, overwrite current speed with downstream speed except (a) a nonfinal segment immediately followed by a point turn, or (b) the final segment immediately preceded by a point turn. Single-segment input and current point turns keep their reverse-pass speed. Those exact branches are0x00508EA2..0x00508F28. Append original geometry with resulting profile.

For split line (0x00508F48..0x0050906E), distance from start to split is `length-tail`, sign taken from current speed; xsplit=startX+signedDistance*cosf(endHeading), ysplit=startY+signedDistance*sinf(endHeading), separately binary32 multiply/add. Define first line using current speed, second using downstream speed with current sign; when downstream equals0 second uses signed20. Preserve accel/decel in both; append first then second. This stage can change **segment count**, so directly applying profile speeds to primitive segments is not equivalent.

Unknown kind (outside1/2/3) warns `IPathPlanner.ApplyMotionProfile.UnknownSegment` / `Path has invalid segment`, returns0 during the reverse pass, and leaves the already-cleared destination empty. Ordinary return is1. No invented graceful handling is assigned to terminating/malformed memory paths; standing M1-034 applies if a port encounters them, with an owning-record unresolved entry.

### 5. Slot20 name and meaning

The behavior can be named descriptively as **already collision-checked planner**, but original identifier is UNKNOWN. The anonymous constant-return entries are not enough to claim a spelling such as ChecksCollisions, IsCollisionChecked, or IsLatticePlanner. Relocation and caller establish its meaning: lattice returns true and its completed path bypasses the additional lattice safety pass; short planners return false, so a nonempty completed short path is checked by existing lattice before execution, and an unsafe short path can trigger fallback. It is not “nonempty path,” “finished,” or “async”: those conditions are separately tested/read.

### 6. Manifest comparison and remaining boundary

Current checked manifest (post-pull2f28c531):

- **M13-033**, title “Planner internals behind PathComponent: SelectPlannerHelper, LatticePlanner ComputePath/ComputeNewPathIfNeeded/StopPlanning/CheckIsPathSafe and [impl+0xF8], FaceAndApproach/MinimalAngle ComputePath, ComputeClosestGoalPose, ApplyMotionProfile”; status **RECOVERABLE_GAP**. Evidence is the context's `Unread` section listing the request addresses. This report supplies additional primary reads for manager verification; status is unchanged.
- **M13-018**, title “LatticePlannerImpl's planning entry and worker, the Replan argument, the heuristic and the result codes”; status **IMPLEMENTATION_GAP**. Current evidence says: “the false branch is not reachable from any caller recovered from the shipped engine: StartPlanning has one caller, the ComputeNewPathIfNeeded thunk (0x004FEB22/0x004A66E8), and all three ComputePath implementations pass true ...”. **Contradicted:**0x00649C36 explicitly passes false to that thunk through current planner slot+C. Existing initial ComputePath calls being true do not exclude this other production caller.
- M13-018 also says “the only in-engine consumer of the status, GetCompletePath ...” while current context and0x004FD192/PathComponent UpdatePlanning establish a per-tick status consumer. The evidence's guaranteed “DoPlanning returns” wording should be checked against cleanup-clobbered r0; the status stores are recovered, the unused return contract is not. Its unresolved “sleep loop polls with Task.Delay” is stale relative to current Thread.Sleep implementation.

No record was changed. Manager should reconcile the false-branch reachability claim before treating the existing M13-018 build as covering the production path. The present stack explicitly exposes MISSING for several adjoining live-path steps, and M13-033 remains open; no whole-subsystem completion follows from matching A* results or its existing tests.

No regression test was added in this research lane. Useful manager-side verification cases after an approved inventory include: exact selector equality boundaries and squared-distance event; false restart with safe old plan/unsafe prefix/moved robot; pending versus running stop; async versus sync status observation; Face no-line/no-turn old-path retention; Minimal backup formula and45mm/s; profile reverseSpeed unequal to normalSpeed; line-tail splitting/count; lattice unconditional final correction. Scheduling-dependent tick counts should not be asserted as constants.

Remaining UNKNOWNs are explicitly bounded: original slot20 method spelling; full subordinate collision/import/AppendToPath/Path-capacity internals and malformed-input managed mapping; complete global computed-pointer alias proof for every conceivable F8 mutation; external scheduler tick count. The requested wrapper bodies, selection branches, short-planner generation and profile body were read; this is not approval of all their dependencies.

Local reproducible evidence: [disassembler](20261013-planner-internals-disasm.py), [core native reads](20261013-planner-internals-core.txt), [worker/short reads](20261013-planner-internals-worker-short.txt), [vtable/writer audit](20261013-planner-internals-audit.txt), [follow-up](20261013-planner-internals-followup.txt), [safety](20261013-planner-internals-safety.txt), [extra](20261013-planner-internals-extra.txt), [final checks](20261013-planner-internals-last.txt). None was uploaded. Research only; stopped after writing this answer.
