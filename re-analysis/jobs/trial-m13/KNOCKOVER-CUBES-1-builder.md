# KNOCKOVER-CUBES-1 builder - frozen for independent review

Isolated worktree codex-knockover-cubes-1, checked baseline ecb33f52. Read PROJECT_STATE, AGENTS, implementer, job and whole K1-K23 packet/independent READ PASS before code. Manager separately released K24 exact E2G11 pack/publication/guard and common required-unlock/spark lock binding. No native extraction by builder, hardware, full suite, commit/push, official status/evidence/inventory/REVIEW/Claude edits. Sole builder/test runner. Actual model/effort telemetry unavailable.

Current records were re-read before the implementation/whole-record assessment; exact quotes follow. The frozen older M13-014 D8=soft phrase is explicitly contradicted by the independently checked/operator-approved K6 hard-spark correction; no silent official rewrite.

```json
{"id": "M12-035", "title": "The flip-block +0x150 installers and DriveAndFlipBlockAction::GetPossiblePoses", "status": "IMPLEMENTATION_GAP", "evidence": ["0x0055E208..0x0055E32A, 0x0055F366..0x0055F464, 0x0055E3B0..0x0055E3F6, 0x0055F4C6..0x0055F52E, 0x0055EB04..0x0055EB8A, 0x0055F592..0x0055F5A4, 0x0055EC04..0x0055EC2E, 0x0055F60E..0x0055F624, 0x0055E438..0x0055E91A", "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q3; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md Q3 (PASS)", "0x00559A86..0x00559AE6 the non-type-6 CheckIfDone tail (manager read)", "0x0055B2C8..0x0055B438 the IDriveToInteractWithObject ctor compound construction and its AddAction flags (manager read)"]}
```

```json
{"id": "M13-014", "title": "Knock over a stack: BehaviorKnockOverCubes flow, DriveAndFlipBlockAction and IDriveToInteractWithObject", "status": "IMPLEMENTATION_GAP", "evidence": ["BehaviorKnockOverCubes body 0x005C2EA0..0x005C3E2A; IsRunnableInternal 0x005C314C needs StackOfCubes::GetStackHeight() >= +0x124 (minimumStackHeight default 3); InitInternal 0x005C31A2 runs the reach unless +0xD9 (alwaysStreamline) or +0xD8 (the soft/hard switch flag) is set (0x005C31B4/0x005C31BA)", "TransitionToReachingForBlock 0x005C3254: TurnTowardsObjectAction(max pi), then DriveStraightAction(x-85, 60) when the block x+10 > 85.0, then TriggerLiftSafeAnimationAction(+0x150), then TransitionToKnockingOverStack regardless of result", "TransitionToKnockingOverStack 0x005C34A8: TurnTowardsObjectAction(max pi), DriveAndFlipBlockAction with maxTurn = pi/2 on the first attempt (0x005C36BC) and 0.0 once +0x140 > 0 (0x005C36C0); +0xD9 or +0xD8 also forces 0.0 (0x005C34EA..0x005C34F8); WaitAction(0.5); the callback 0x005C3DCE writes AIWhiteboard+0x70 on NoPreActionPoses (0x03000010), re-runs while +0x140 <= 1 on a Retry result and otherwise blind-flips, incrementing +0x140 either way", "success goes to TransitionToPlayingReaction 0x005C3908: it sets robot->[+0x34]->[+0x94]->[+0xC] = 1 (0x005C3946..0x005C394E), i.e. the BlockWorld's BlockConfigurationManager dirty flag (BlockWorld+0x94 is the manager; BlockConfigurationManager+0xC is read by its Update 0x00616D84) forcing all block configurations to recompute; the tipped-object set size at +0x14C selects the success trigger +0x15C with BehaviorObjectiveAchieved(0xD, true) and NeedActionCompleted(0) (0x005C3950..0x005C3964) or the failure trigger +0x160 (0x005C396E); when +0xD9 or +0xD8 is set the reaction animation is skipped (0x005C3972/0x005C3978)", "IDriveToInteractWithObject 0x0055B1F4 adds TWO actions when maxTurn > 0 (0x0055B37C..0x0055B392): a TurnTowardsLastFacePoseAction (vtable overwritten from TurnTowardsFaceAction at 0x0055B3C4..0x0055B3D8) and a TurnTowardsObjectAction (0x0055B42E/0x0055B43C), both with failure ignored; the trailing float 20.0 is not read by DriveAndFlipBlockAction ctor 0x0055E208", "+0xD9 is the JSON config key alwaysStreamline (IBehavior::ReadFromJson 0x005BC208/0x005BC216/0x005BC21E); +0xD8 is computed by IBehavior::Init 0x005BCCAA..0x005BCCC2 as 1 when the behaviour was entered by a soft spark switch and 0 for a hard switch or no current behaviour", "0x0055B3C0..0x0055B3CC, 0x005DE160..0x005DE16C, 0x005B7DB2..0x005B7DC2 the vtable overwrite; vtable 0x010204F4 slots 2, 4, 6, 9, 10", "0x0054B7B6 the ctor store of +0x193; 0x005C229C, 0x005DE170, 0x005F20B8, 0x005F6942 the behaviour stores", "0x0054B978..0x0054B9F4 SetSayNameAnimationTrigger; 0x0054C606..0x0054C61E and 0x0054C2D0..0x0054C2E6 the NeedsManager registrations", "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q4; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md objection A8 (the class adds no logic of its own)", "re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q2, Q3, Q6; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md Q2/Q3, Q6 (PASS)", "0x0055B2C8..0x0055B438 the IDriveToInteractWithObject ctor compound construction and its AddAction flags (manager read)"]}
```

```json
{"id": "M13-028", "title": "FlipBlockAction::Init and DriveAndFlipBlockAction's constructor guards", "status": "IMPLEMENTATION_GAP", "evidence": ["0x0055EED6..0x0055EEFA, 0x0055EF3A..0x0055EF70, 0x0055ECE0, 0x0055EEC6, 0x0055EF7E, 0x0055B258..0x0055B264, 0x0055B38C", "re-analysis/research/20260929-R-VIS-verify-M12-fix2.md objections 2, 3, 9", "0x0055F074..0x0055F17A FlipBlockAction::CheckIfDone; 0x0055EF3A..0x0055EF4A; 0x00548A54..0x00548A78 (fix-round-3 verifier)", "0x0055F074..0x0055F208 CheckIfDone; 0x0055ED54..0x0055EDA0 destructor (round-4 verifier, .scratch/m12v5_cid.txt)"]}
```

## Draft changes

Configured factory/runtime constructs and retires NativeKnockOverCubesBehavior. It uses actual native configuration identity and deterministic weak-owner expiry, cached IDs, byte-height/config triggers and canonical hard-spark state. Refresh occurs only in IsRunnable; Init/Resume only lock the cached weak owner. Stop clears weak and IDs. Common native base requiredUnlock is read-only and binds only this new configured owner's existing shared SparkBehaviorDisables helper to canonical manager state; public/pickup bindings are preserved.

NativeDriveAndFlipRunner builds actual Drive(type5 input)/WaitLambda/face/object/Flip compounds with ignore-failure/proxy semantics, source current-origin last-face cache and full native selected world pose. NativeDriveToObjectRunner gains only a constructor overload for source pre-action type; original pickup constructor remains unchanged. NativeFlipBlockRunner owns the embedded lift/drive, constructs drive before lift but executes lift then drive, real concurrent carry-lift tag and exact cancellation/reaction-lock/world-unknown recipients. Source pending tag persists across reinit. Public async/manual APIs remain explicit separate paths.

E2G11 actual cube handler now enqueues ONE source-packed nine-byte raw+typed API publication before synchronous local11, gated on actual external interface. It preserves original API event snapshots and real resolved world ID; no new robot wire tag. Native tipped set accepts only frozen stack members and unique IDs, not pose-derived changes. Retry transitions construct with old count before increment. Final recipient order is force configuration dirty, objective13, configured needs27, then optional source lift-safe reaction. No extra carry-release/putdown child.

## Actual run ledger

All dotnet commands use C:/Program Files/dotnet/dotnet.exe, project cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj, current isolated worktree. Local IPC escalation was used as authorized. Artifacts under .scratch/knockover-cubes-1. Test commands use --no-restore --nologo -v:q, exact filter plus trx logger/name and results-directory. No full suite. Failed attempts retained.

| Artifact | Actual outcome |
|---|---|
| restore-1 (console) | Output redirection failed because scratch directory did not exist. Restore did NOT execute; shell exit0 was only the earlier Python exit and is not a restore success. |
| restore-2.log | Created scratch artifact directory; restore --ignore-failed-sources exit0. |
| build-1.log | build --no-restore exit0, 2203 warnings/0 errors, 53.29s. |
| focused-1.log | Class filter FullyQualifiedName~KnockOverNativeTests. Compile exit1 before tests: newly authored request Trigger/member/type/UIcount and nonexistent queue-inspection method errors. Corrected only test member/types/inspection. |
| focused-2.log | Same class. Compile exit1 before tests: test ObjectUpAxisChanged.ObjectID cast was int instead of generated uint. Corrected test cast; no production change. |
| focused-3.log/.trx | Exit1, 10PASS/2FAIL/0skip, 12total,1s. Exposed interaction Type is source-proxied Flip0F, not constructor base5. Moving only top away leaves a valid two-cube stack; EmptySnapshot oracle wrong. Corrected source oracle and weak test to explicit scoped Init without manager IsRunnable refresh. |
| focused-4.log/.trx | Exit1,15PASS/1FAIL/0skip,16total,2s. Unit/event/weak/retry/flip/cancel and reaction output cases pass. Configured positive advanced simulated time before async worker completed, causing genuine4s planning timeout. Added existing pickup offline worker watchdog; no timeout/assertion/production weakening. |
| configured-1.log/.trx | Configured-only filter FullyQualifiedName~KnockOverNativeTests.ConfiguredNativeRun; exit1,0PASS/1FAIL/0skip,1total,1s. Actual retries then blind/failure reaction. No source code correction or target/goal rewrite: tracing original goal versus supplied MAIN/wire path/Flip pre-action before determining fixture or inherited frontier. |
| focused-5.log/.trx | Same whole class, with source-approved common spark binding tests, true missing-while-running case and unchanged-input diagnostic trace; exit1,19PASS/1FAIL/0skip,20total,3s. All19 unit/recipient cases pass. Sole failure was diagnostic output path relative to test bin cwd; corrected artifact path only, no pose/goal input changed. |

## Named boundaries

Inherited M13-033/034 cached path/geometry/reconstruction/short safety remain candidate frontiers, not recovered by this caller work. Native LastFace/Turn/Wait/PanTilt/motor/goal scheduler bodies are reused; generic detector/PnP, full face payload/recognition and true-sayName speech remain their existing records. Specialized activity selection/admission is not invented. Native MarkObjectUnknown(obj,true) is actually called, while its broader world/map/report uncertainty remains M11-038/042; no local MarkUnknown replacement. Existing public/manual async behavior, DriveToFlipBlockPoseAction/closest-only/nonempty-external pose-function routes are not certified as approved extensions or automatically closed under M12-035. No whole-record settlement claim until implementation/independent review proves the actual current claim.

| configured-diagnose-1.log/.trx | Exit1,0PASS/1FAIL/0skip,1total,1s. Unchanged-input saved trace shows MAIN supplied from drive-center endpoints (reach195 instead of source MAIN215; selected MAINgoal221.42/78.58 vs supplied207.28/92.72 at yaw-.7854, exactly rotated20mm). First attempt candidate yaw is separate inherited frontier. Source blind Flip actually completes; fixture tip watcher incorrectly observes stale normal Interaction.Flip rather than live blind child. No source change endorsed by this trace. Unique failed trace configured-input-trace-failed-1.txt preserved. |

| focused-units-1.log/.trx | Filter FullyQualifiedName~KnockOverNativeTests&FullyQualifiedName!~ConfiguredNativeRun; exit1,20PASS/1FAIL/0skip,21total,2s. Carried reaction/no extra release passes. Four unnamed no-eye-parts observations lose pose parent in the inherited FaceWorld entry-update adapter (new Pose3d(rotationFrom,translation) is parentless), so required-current-origin cache correctly rejects it. Bounded original C2-F7c parent contract returned to A/B before any production fix or weakened oracle. |


| focused-6.log | Compile exit1 before tests: new firmware receive helper captured the paths variable declared later. Corrected test-local declaration order; no production behavioral change. |
| focused-7.log/.trx | Exit1,22PASS/1FAIL/0skip,23total,3s. K27 parent/rotation and unnamed/current-origin tests pass. All complete wire segments/CurrPathSegment and source drive-center-to-MAIN feedback now execute the NORMAL Flip successfully, attempt0, member11 and success reaction. Sole failure: expected embedded Interaction tag on external5A; source shared M8 boundary is under independent confirmation before changing the completion oracle. Unique normal-success trace retained separately. |

K27 manager-released consumer correction preserves the full already prepared incoming face pose (parent/rotation), then overwrites only prior translation at the existing/no-eye branch; no global pose constructor or require-current-origin change. Full no-eye regression checks literal oldtranslation, exact incoming quaternion and parent identity, independent source/cache frame acceptance/rejection. Upstream flattened/rootless UpdateTranslation/M11-066 producer uncertainty remains explicit.

The configured firmware simulator no longer labels drive-center coordinates as MAIN, omits the doler tail, or watches a stale normal Flip during a blind phase. It consumes actual sent segment bytes/event IDs, reports real CurrPathSegment progress, waits for complete received count before C6, applies the checked unloaded inverse20mm in actual reported heading, and supplies member rawaxis for the live initialized/running normal or blind Flip. No target/original-goal/yaw override or new line atan2 was added. Candidate path/geometry boundaries remain unchanged.


## Additional current dependency records (no status edits)

```json
{"id": "M7-014", "title": "Reaction-lock manager lifetime and concrete per-class lock tables", "status": "IMPLEMENTATION_GAP", "evidence": ["IBehavior::SmartDisableReactionsWithLock 0x005bce3c", "BehaviorManager::DisableReactionsWithLock 0x005a27e8", "BehaviorManager::RemoveDisableReactionsLock 0x005a3a48", "13 concrete 21-entry tables in M7 inventory Appendix F"]}
```

```json
{"id": "M14-001", "title": "TrackedFace geometry, FaceWorld match and forgetting rules", "status": "IMPLEMENTATION_GAP", "evidence": ["0x0086B244 is `movs r0,#1; bx lr`, so 0x004F43D6 `blx IsRecognitionSupported` / 0x004F43DE `cbz r0,#0x004F440C` never branches: the pose/overlap loop 0x004F440C..0x004F4536 is dead, and the reachable match is the map at FaceWorld+4 searched by lower_bound on the TrackedFace id at +0 (0x004F43EC..0x004F4556); an absent id creates an entry keyed by that id (0x004F45EA/0x004F45FC). The 220^2 constant 0x004F4428 and the overlap-0.5 instructions are on the dead branch.", "0x004F5380..0x004F53AC removes an unnamed entry when lastObserved+15000 is behind the last image timestamp.", "0x0087DE3C tests the parts-detected byte +0x30; 0x0087DE42..0x0087DE5E takes the eye centres +0x34..+0x40 through GetIntraEyeDistance 0x0087DC68 (sqrt(dx^2+dy^2)/cos(+0xec), divisor 1.0 when |cos|<1e-5, 6.0 when dist<1e-5); 0x0087DE60..0x0087DF1C derives the box eye distance |0.5*w| floored at 6.0; 0x0087DF20..0x0087E068 halves the (zeroed in the box branch) eye-slot sum for the midpoint, scales fx*62.0/eyeDistance (0x42780000) and parents the pose to Camera+0xc.", "0x004F4596 WasRotatingTooFast is on the new-entry path only; the found path 0x004F4558..0x004F456C is not gated. On the found path, a no-parts observation (TrackedFace+0x30 == 0) replaces the local pose translation with the entry's existing translation (0x004F4784..0x004F47A8) before writing it back to entry+0xf4 (0x004F47BE); the rotation stays from the TrackedFace pose."]}
```

```json
{"id": "M14-008", "title": "Vision face-result handoff and FaceWorld lifecycle broadcasts", "status": "IMPLEMENTATION_GAP", "evidence": ["0x006551E8..0x00655202 walks the std::list<UpdatedFaceID> at VisionProcessingResult+0x54 ({int oldID, int newID, string name}) and calls FaceWorld::ChangeFaceID 0x004F3DB4 for each before the rest; 0x00655204..0x0065526C marks FaceWorld+0x24 when a tracked-face node has +0x14>0; 0x00655274 calls FaceWorld::Update.", "0x004F5316..0x004F535C calls AddOrUpdateFace per tracked face and 0x004F5380..0x004F54A8 expires unnamed faces after 15000 ms.", "0x004F4B7A/0x004F4B82 broadcasts RobotObservedFace; 0x004F3C2C..0x004F3C64 broadcasts RobotDeletedFace, erases visualization and erases the map node."]}
```

## Subsequent final verification ledger

| Artifact | Actual outcome |
|---|---|
| focused-8.log/.trx | Exit0,23/23PASS,0skip,3s. Source-correct internal Watcher0F/0/6 and queued root external5A(-2/0/6), full ordered unique subresults including ignored04000001, raw bytes and local/raw/typed delivery order. Actual configured normal Flip success, attempts0, no supplied original-goal or yaw override. |
| affected-1.log/.trx | Exact affected-filter.txt below; exit1,1633PASS/1FAIL/0skip,1634total,1m8s. Sole failure existing NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates public/manual async fixture, AggregateException in DriveAndFlip task, before expected ignored-drive trace. No source correction inferred. |
| legacy-knock-diagnose-1.log/.trx | Only FullyQualifiedName~NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates, exit0,1/1PASS,0skip,1s. The old assertion predicate is unchanged; its failure message now includes full trace instead of truncated collection display. Failure remains unresolved/nonreproduced, not claimed fixed. |
| affected-2.log/.trx | Same affected filter, --no-build; exit0,1634/1634PASS,0skip,1m6s. The earlier legacy async exception remains retained/unresolved; no claim it was fixed. |
| fidelity.py --check | Exit0,711 records;249 EXACT_SOURCE,68 COMPATIBILITY_POLICY,14 HARDWARE_ONLY,7 EQUIVALENT_IMPLEMENTATION,348 IMPLEMENTATION_GAP,23 RECOVERABLE_GAP,2 BLOCKED_EXTERNAL. No official record changes. |

Affected filter (not full suite):

```text
FullyQualifiedName~KnockOverNativeTests|FullyQualifiedName~PickupChildrenNativeTests|FullyQualifiedName~PickupMotionTimestampTests|FullyQualifiedName~PickupSearchNativeTests|FullyQualifiedName~PickupPyramidNativeExecutionTests|FullyQualifiedName~PickupPyramidPilotTests|FullyQualifiedName~PickupStackNativeVisionTests|FullyQualifiedName~PutDownBlockNativeTests|FullyQualifiedName~SharedChildrenNativeTests|FullyQualifiedName~PickupVerifyTests|FullyQualifiedName~ManipulationTests|FullyQualifiedName~NavigationTests|FullyQualifiedName~M12|FullyQualifiedName~M13|FullyQualifiedName~M14|FullyQualifiedName~M11|FullyQualifiedName~M4ControlTests|FullyQualifiedName~M3M4X16BuilderTests|FullyQualifiedName~M3M4X23BuilderTests|FullyQualifiedName~CompoundActionTests|FullyQualifiedName~M8CompoundCheckedRowsTests|FullyQualifiedName~M8ActionCheckedRowsTests|FullyQualifiedName~ActionListTests|FullyQualifiedName~ActionCompletionTests|FullyQualifiedName~M7BatchThreeATests|FullyQualifiedName~BehaviorFrameworkTests|FullyQualifiedName~SparkManagerInputCheckedRowsTests|FullyQualifiedName~FaceTests|FullyQualifiedName~VisionTests
```

Only one redundant post-retirement virtual completion getter assertion was removed after focused8; actual nested watcher snapshot remains the source-backed completion oracle and is rechecked in affected runs. No production behavior changed for that cleanup. Navigation test diagnostic uses the identical Contains predicate under Any, preserving its old pass/fail condition and full original assertions.


| last-cases-1.log/.trx | Exact filter FullyQualifiedName~KnockOverNativeTests.ManagerReplacement OR FullyQualifiedName~KnockOverNativeTests.NoPreactionOrOther; exit0,3/3PASS,0skip,896ms. Actual manager replacement retires running owned blind Flip and pending carry-lift while unrelated parallel survives, clears IDs/locks and preserves constructor11 lease. Source NoPreAction sets whiteboard/no increment; other nonretry constructs no next action. |

## Whole-record assessment for independent review / Claude

M13-002 is the only proposed whole-record settlement candidate. Its current title/status/evidence were directly re-read: “FlipBlockAction's constants, their roles and IAction type”, IMPLEMENTATION_GAP. Native55EC80/55ECA8..ED12 metadata/defaults now feed actual150 drive, +20 distance,45 initial lift, strict40 carry-lift trigger, persistent queuedtag/-1 sentinel and cancellation, pre-action flag and type0F; actual native constructor/child/body/queue paths and the preserved public constants agree with the checked source. The old evidence wording “speed5” is contradicted by the separately checked tolerance5/defaultspeed10+accel20; official evidence change remains Claude's.

M13-014, M13-028 and M12-035 remain PARTIAL whole records. The configured native own route/Flip/IDrive is built and regressed, but live public/manual async KnockOver/Flip/DriveAndFlip, generic/closest/L3-L4 pose-function paths and their existing frame/scheduler/guard/frontier claims are not certified by this job. They are not silently renamed approved extensions. M12-035's public named-only choice and other old adapter bounds remain explicit; the new native actual-current-origin cache consumer is source-correct. No new record is proposed for already owned steps. No official status/evidence/inventory/approval/REVIEW edits.

Known unresolved run limitation: affected1's one old public/manual async exception did not reproduce in the isolated diagnostic or identical affected2 rerun. The exact old expectation remains; its full diagnostic is now preserved on future failures. This is exposed honestly, not classified as a proven implementation or fixture fix.

Final code/test files:

- cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorContainer.cs
- cozmo-stack/src/Cozmo.Robot/Behavior/NativeBehaviorBaseInputs.cs
- cozmo-stack/src/Cozmo.Robot/Behavior/NativeBehaviorRuntime.cs
- cozmo-stack/src/Cozmo.Robot/Behavior/NativeKnockOverCubesBehavior.cs
- cozmo-stack/src/Cozmo.Robot/Behavior/SteppedBehavior.cs
- cozmo-stack/src/Cozmo.Robot/Cubes.cs
- cozmo-stack/src/Cozmo.Robot/Manipulation/NativeDriveAndFlipRunner.cs
- cozmo-stack/src/Cozmo.Robot/Manipulation/NativeDriveToObjectRunner.cs
- cozmo-stack/src/Cozmo.Robot/Manipulation/NativeFlipBlockRunner.cs
- cozmo-stack/src/Cozmo.Robot/Vision/Faces.cs
- cozmo-stack/src/Cozmo.Robot/Vision/NativeLastFaceRunner.cs
- cozmo-stack/tests/Cozmo.Protocol.Tests/KnockOverNativeTests.cs
- cozmo-stack/tests/Cozmo.Protocol.Tests/NavigationTests.cs

Raw-byte freeze: 13 C# files, `.scratch/knockover-cubes-1/frozen-sha256.txt`, aggregate SHA256 `3a0c3509b2c1699644d9040e1306d133446e31d82c023eb0bec1347c3c4cb49c`. No further code/test edits planned. Independent B execution and manager integration/full gate remain required; no builder full suite/commit/push.

shutdown-1.log: exit0; MSBuild and compiler servers report successful shutdown. All13 code/test raw hashes rechecked unchanged afterward. git diff --check passes (only existing line-ending normalization warnings). Sole test runner released to independent B; no builder test/build/process work after this freeze unless manager requests it.

Current whole-record candidate quote (re-read directly, no official edits):

```json
{"id": "M13-002", "title": "FlipBlockAction's constants, their roles and IAction type", "status": "IMPLEMENTATION_GAP", "evidence": ["FlipBlockAction ctor 0x0055EC80: IAction type 0xF (0x0055ECA8/0x0055ECAA); +0x12C=150.0 (0x43160000), +0x130=20.0 (0x41A00000) at 0x0055ECF6/0x0055ECFA; +0x134=45.0 (0x42340000), +0x138=40.0 (0x42200000), +0x13C=-1 at 0x0055ECFE..0x0055ED0E; +0x140=1 at 0x0055ED10/0x0055ED12", "+0x12C is the drive speed: Init 0x0055EF18 loads it into r3 and passes it to DriveStraightAction 0x0055EF2C; the r3 argument is stored at DriveStraightAction+0x7C (0x005472A4)", "+0x130 is the drive-past distance: Init computes the robot-to-object distance (vsqrt.f32 0x0055EEFA), adds +0x130 (0x0055EF14/0x0055EF1C) and passes the sum as DriveStraightAction's distance argument 0x0055EF20 (stored at DriveStraightAction+0x78)", "+0x134 is the approach lift height: Init 0x0055EF3A loads it into r2 for MoveLiftToHeightAction(45.0, 5.0, 0) 0x0055EF4A (added before the drive)", "+0x138 is the lift trigger distance: CheckIfDone 0x0055F124 vldr s0,[r4,#0x138] / vcmpe / bpl 0x0055F130; when the object is closer than it and +0x13C is still -1 it queues MoveLiftToHeightAction(preset 2, speed 5.0) 0x0055F14E and stores that action's id in +0x13C (0x0055F15C) through ActionList::QueueAction position 5 (0x0055F16A)", "+0x13C is the queued lift action's id, -1 when none; the destructor cancels it on robot+0x250 ActionList (0x0055ED6C/0x0055ED70/0x0055ED7A)", "+0x140 is shouldCheckPreActionPose: Init reads it (0x0055EE10) into the PreActionPoseInput byte at sp+0x64 (0x0055EE1C) passed to IDockAction::GetPreActionPoses with ActionType 5 (0x0055EE14/0x0055EE5E); SetShouldCheckPreActionPose writes it (0x0055EDC2)"], "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): the 5 passed to MoveLiftToHeightAction(45, 5, 0) is a tolerance: the lift now goes out with the constructor defaults (speed 10, accel 20; 0x00548A6E..0x00548A78); the distance is computed in binary32 (0x0055EED6..0x0055EF1C, 0x0055F0E6..0x0055F10A). Still open: DriveAndFlipBlockAction.GetPossiblePoses (M12-035) still casts a double sum (0x0055E688 unread)."}
```
