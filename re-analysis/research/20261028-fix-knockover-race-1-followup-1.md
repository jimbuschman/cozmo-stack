# FIX-KNOCKOVER-RACE-1 — finite followup 1 (2026-10-28)

Addendum to `20261028-fix-knockover-race-1-read.md`; original frozen report unchanged. Root requested one finite static check of actual action/motor counters, reset seams and xUnit isolation after Stage D. Same reader request gpt-6.1-sol/medium; no model switch/backend claim. No .NET, Git operation, hardware, code/test or metadata edit; only this additive research report written.

## Finding

**UNKNOWN remains correct.** No global-tag-reset interference or unprotected counter-container corruption is established on this path. The named wrap/reset suspicion is ruled out by the actual ownership and test seams below. This is a narrow result, not proof that all background activity, lifecycle races or order-dependent state are impossible. No semantic fix is justified. The exact blocker remains the absent historical inner exception/throw stack; the finite21-attempt experiment captured none.

## Actual ownership and mutating tests

- `Actions/IActionRunner.cs:96..145`: ActionRunnerTagCounter.Global is static readonly. `_next` and `_inUse` are private instance fields, guarded by the counter's single gate in NextIdTag, TryReserve, Release and IsInUse. No Reset method, mutable Global setter or test-only Global seed exists. Constructor196..197 obtains/reserves each generated tag; WatcherEnding545 releases current/original tags. CozmoMotion.NextActionTag202 uses the same locked allocator for animation lock-owner names; it increments rather than resets it.
- `ActionListTests.cs:519..527` creates **private counter instances**, including `new ActionRunnerTagCounter(0xFFFFFFFFu)` for the wrap test. It never moves Global to that boundary. C2_SetTagRules532..553 creates actual ConcreteRunners, reserving generated tags, then tests explicit0x123456/collision/zero. Unretired local runners can retain reservations, but no reset/reseed occurs and Navigation uses generated tags rather than that explicit low tag. No evidence relates retained reservations to this failure.
- `ActionRunner.SetTag:478..501` is a multi-call check/store/reserve, not one atomic transaction across IsInUse and TryReserve. This report does not certify arbitrary simultaneous explicit SetTag calls as atomic. The traced Navigation route has no explicit SetTag call. Other relevant tests: SourceLogMigrationTests74 tests invalid0; PickupStackNativeVisionTests62/159 uses987654/987655; direct Global references otherwise inspect own runner reservations in M13TurnEyeLifetimeTests/PutDownBlockNativeTests. These do not reset Global. Exact class/private-field names searched under the test tree revealed no reflection reset of `_next`, `_inUse`, `_actionIdCounter` or the four motor-debug counters. No unrelated-static audit performed.
- `Motion.cs:93,172..192,209..213`: motor ActionId is a **byte instance field per CozmoMotion/robot**. Increment and ResetToConstructed's store0 both hold that owner's gate. Robot removal resets only that owner's state, cancels its ActionList and completes its waits. Each Rig constructs a new offline robot/motion; another test's reset does not reset this Rig. Head/lift/body share its counter intentionally. Same-robot wrap/late-ack interference is not demonstrated by AggregateException; success journals do not expose enough IDs to assert all historical counter values.
- `Motion.cs:1219..1228,1368..1406`: four process-wide ushort debug counters hold DebugCounterGate and control only eleventh-call WaitingForAck/NotInPosition debug-log cadence; reset0 follows each log. They do not select motor IDs, mutate Acked, complete tasks or select results. Source-log subscribers can execute during logging, but no evidence identifies a throwing subscriber here. They are not action timers.
- `ActionList.cs:121,159,232`: Update, QueueAction and Cancel take the list's own gate. Action state/tag fields belong to runners. A locked allocator does not certify every lifecycle operation thread-safe; no such broader claim is made.

## xUnit isolation

NavigationTests14 belongs to `SteppedBehavior missing-report statics`; BehaviorFrameworkTests2406 defines that exact collection with DisableParallelization=true. Project uses xunit2.9.2 and runner.visualstudio2.8.2; xunit.runner.json caps maxParallelThreads2 without overriding that collection.

Official [xUnit parallelism documentation](https://xunit.net/docs/running-tests-in-parallel), “Parallel mode: collections” and “Selectively opting out of parallelism”, says an opted-out collection's test cases do not overlap other test cases. This is stronger than merely putting two classes in one collection. Therefore ordinary xUnit execution of ActionListTests' default class collection, SourceLogMigrationTests/PickupStackNativeVisionTests' same named collection, and M13TurnEyeLifetimeTests' M5 process statics collection does **not** concurrently run those test cases against this Navigation case. M5 is separately opted out at M5AnimationTests2173.

This does not join an unobserved application task/timer or kill a worker leaked by an already-finished test. No such leftover worker is demonstrated here. Disabling all suite parallelism would address no proven defect in this already opted-out case; no isolation change proposed.

## Stage D provenance and actual execution

Read actual files under builder `../codex-fix-knockover-race-1/.scratch/fix-knockover-race-1/artifacts/`. Directly parsed21TRXs: affected0011659/1659PASS plus20 separate target1/1PASS; target named and passed in every invocation. Final freeze records every actual exit0, no skipped/not-executed result. No failure snapshot exists because success intentionally writes none.

`run-diagnostics-001.ps1` saves each literal command before execution, builds affected001 with --no-restore, then20 separate exact-target processes with --no-build --no-restore/normal runner settings. Nonzero affected/target exit stops later planned attempts. This is declared reproduction evidence, not acceptance retries. Exact target filter: FullyQualifiedName=Cozmo.Protocol.Tests.NavigationTests.KnockOverCubesReachesFlipsTheBottomBlockAndCelebrates.

Independently re-read target sourceSHA256 `7AF86AB37DEAD199BFA79F77EF738D5EC3ED9C01A6E54E69C076134C89C465CB` agrees with pretest/finalfreeze. Finalfreeze ledgerSHA256 `8E044DBC0435E37AACEB481D4B19FDABD0E06E3EA58317EF6C6F4F7A9FBAB93D`; pretest ledger differs because results were appended later, not a test-image change. Source fingerprints retained in diagnostic-pretest-hashes.json. Diagnostic process artifact contains literal processes[],total0,ownProcesses[],own0; shutdown exit0. These are builder execution records, not reader executions.

Real-body evidence beyond PASS count:

- The Lib-null early return remains. MarkerLibrary.LoadBytes69..114 checks env override, embedded resource, then sidecar; a present wrong blob throws through Verified rather than returning null.
- Read-only byte search found the **complete621384-byte native marker blob** inside actual test-output `bin/Debug/net9.0/Cozmo.Robot.dll`, offset4802020. BlobSHA256 `1b7166ac37b166975f0e5313a3d4486ebae73010e42ffb89bbd0d4536a29c81d` equals the expected library fingerprint. Current outputDLL SHA256 `283279de9649561aded134e333bf8371b6869ec0d57f01467158f8240e182b9b`. Thus present compiled output has a valid embedded fallback and supplies nonnull library. This after-run artifact check is not a directly captured loader event for each past invocation.
- Affected target duration1.6937949s; twenty exact-target durations1.7989311..2.0706748s. Consistent with marker/action execution plus unchanged real0.5s success delay (`CubeGameBehaviors.cs:138`). Duration alone does not prove a particular event; combined with verified embedded library, unchanged body/assertions it supports real body execution rather than Lib-null returns. No success trace was exported; internal steps cannot be independently reconstructed from a success journal.
- Executed body asserts three observed objects, ignored-drive and terminal Flip success trace, tipped success/objective, sourced reach geometry. Stage D preserves these predicates; diagnostic Step/HandlerFaulted capture does not fabricate them or substitute terminal results.

TRX target duration/start table (timezone-05:00; no phone timing claim):
| Run | Outcome | Duration | Start |
| --- | --- | --- | --- |
| diagnostic-affected-001 | Passed | 00:00:01.6937949 | 2026-10-28T21:04:30.4962305-05:00 |
| diagnostic-target-001 | Passed | 00:00:01.8629002 | 2026-10-28T21:05:21.3408435-05:00 |
| diagnostic-target-002 | Passed | 00:00:01.9031910 | 2026-10-28T21:05:25.2791291-05:00 |
| diagnostic-target-003 | Passed | 00:00:01.9674777 | 2026-10-28T21:05:29.2967149-05:00 |
| diagnostic-target-004 | Passed | 00:00:01.8175668 | 2026-10-28T21:05:33.1411366-05:00 |
| diagnostic-target-005 | Passed | 00:00:01.9064660 | 2026-10-28T21:05:36.9880011-05:00 |
| diagnostic-target-006 | Passed | 00:00:01.9654257 | 2026-10-28T21:05:41.0081610-05:00 |
| diagnostic-target-007 | Passed | 00:00:02.0706748 | 2026-10-28T21:05:45.2433508-05:00 |
| diagnostic-target-008 | Passed | 00:00:01.9642119 | 2026-10-28T21:05:49.3693690-05:00 |
| diagnostic-target-009 | Passed | 00:00:01.9549636 | 2026-10-28T21:05:53.3790355-05:00 |
| diagnostic-target-010 | Passed | 00:00:01.9006424 | 2026-10-28T21:05:57.3318109-05:00 |
| diagnostic-target-011 | Passed | 00:00:01.9328186 | 2026-10-28T21:06:01.2817775-05:00 |
| diagnostic-target-012 | Passed | 00:00:01.8946703 | 2026-10-28T21:06:05.2111204-05:00 |
| diagnostic-target-013 | Passed | 00:00:01.9209038 | 2026-10-28T21:06:09.1770022-05:00 |
| diagnostic-target-014 | Passed | 00:00:01.8616655 | 2026-10-28T21:06:13.1313964-05:00 |
| diagnostic-target-015 | Passed | 00:00:01.8813534 | 2026-10-28T21:06:16.9235464-05:00 |
| diagnostic-target-016 | Passed | 00:00:01.8229911 | 2026-10-28T21:06:20.6487820-05:00 |
| diagnostic-target-017 | Passed | 00:00:01.8082154 | 2026-10-28T21:06:24.3675289-05:00 |
| diagnostic-target-018 | Passed | 00:00:01.8327938 | 2026-10-28T21:06:28.1082931-05:00 |
| diagnostic-target-019 | Passed | 00:00:01.7989311 | 2026-10-28T21:06:31.8137765-05:00 |
| diagnostic-target-020 | Passed | 00:00:01.8701108 | 2026-10-28T21:06:35.6051529-05:00 |

No new deterministic fault proved. Keep historical failure UNKNOWN and retain improved failure diagnostics for the next actual occurrence. No production correction or meaningful fault-specific regression can be specified without its throw site. No further stress/full-gate hunting authorized by this addendum.
