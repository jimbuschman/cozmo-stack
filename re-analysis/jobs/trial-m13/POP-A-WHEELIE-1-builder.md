# POP-A-WHEELIE-1 builder ledger

## Startup and preparation boundary

- Actor: /root/pop_wheelie_build; fresh isolated builder, fork_none.
- Requested startup model/effort: DEFAULT, both overrides omitted. Operator reports default gpt-6.1-terra; actual backend telemetry unavailable. No inference and no mid-job change.
- Worktree: C:/Users/JimBu/source/repos/jimbuschman/cozmo-stack-codex2/.scratch/worktrees/codex-pop-a-wheelie-1
- Branch: codex/pop-a-wheelie-1; starting HEAD a3b8145f4bdc6942531073957af62196dec536d1.
- Initial git status --porcelain=v1: empty (clean), before this ledger.
- PROJECT_STATE.md read first. AGENTS, job, AUTONOMY, PLAN-BEHAVIOURS, BEHAVIOURS, REVIEW and research README read. Applicable manager-scoped builder authorization supersedes the background research-only lane for this task.
- PREPARATION ONLY. No C#/test edits, native extraction, build/test or Git mutations before separate coherent source/check/root hash-freeze release.
- Exact ordinary PopAWheelie only. SparksPopAWheelie is the next separate job.
- Reviewer required corrective batches: 0. Self corrections: 0. Source-only questions: 0. No builder-raised MISSING yet; inherited/source frontiers pending released packet.
- Root authorized sole .NET preparation window after ignored OBB junction, SO and marker prepared. One local-cache restore across all five solution projects; no repeat restore.

## Commands saved before invocation

Preparation command P1, solution restore (one attempt):
```powershell
& 'C:/Program Files/dotnet/dotnet.exe' restore 'cozmo-stack/Cozmo.sln' --source 'C:/Users/JimBu/.nuget/packages' --disable-parallel *> '.scratch/pop-a-wheelie-1-builder-prep-restore-01.log'
$restoreExit = $LASTEXITCODE
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-restore-01.exit' -Value $restoreExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-restore-01.log'
exit $restoreExit
```

Preparation command P2, actual shutdown:
```powershell
& 'C:/Program Files/dotnet/dotnet.exe' build-server shutdown *> '.scratch/pop-a-wheelie-1-builder-prep-shutdown-01.log'
$shutdownExit = $LASTEXITCODE
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-shutdown-01.exit' -Value $shutdownExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-shutdown-01.log'
exit $shutdownExit
```

Preparation command P3, elevated native CIM inventory, explicit array/counts (no process killing):
```powershell
$processes = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -match '^(dotnet|testhost|VBCSCompiler|MSBuild)(\.exe)?$' -or $_.CommandLine -match 'vstest.console|testhost.dll|VBCSCompiler.dll|MSBuild.dll' } | Select-Object ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine)
$ownProcesses = @($processes | Where-Object { $_.CommandLine -like '*codex-pop-a-wheelie-1*' })
$inventory = [ordered]@{ processes = @($processes); totalCount = $processes.Count; ownCount = $ownProcesses.Count }
ConvertTo-Json -InputObject @($processes) -Depth 4 | Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-processes-01.json'
ConvertTo-Json -InputObject $inventory -Depth 5 | Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-inventory-01.json'
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-processes-01.json'
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-inventory-01.json'
```

### P1 pre-invocation failure

PowerShell redirection failed before dotnet launch because this isolated worktree had no .scratch directory. Output: `Out-File: Could not find a part of the path .../.scratch/pop-a-wheelie-1-builder-prep-restore-01.log`; Set-Content and Get-Content likewise failed. Wrapper exit0 is not a restore result: LASTEXITCODE was null because dotnet never started. No restore occurred. Created ignored .scratch; command P1b below is the single actual restore, with unique log/exit.

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' restore 'cozmo-stack/Cozmo.sln' --source 'C:/Users/JimBu/.nuget/packages' --disable-parallel *> '.scratch/pop-a-wheelie-1-builder-prep-restore-01b.log'
$restoreExit = $LASTEXITCODE
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-restore-01b.exit' -Value $restoreExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-restore-01b.log'
exit $restoreExit
```

### Preparation results and inventory self-match correction

- P1b actual restore exit0 (literal command ran before later parent suggestion to add ErrorActionPreference; all five projects explicitly restored). Exit artifact contains 0. No repeat restore.
- P2 actual elevated build-server shutdown exit0; both compiler and MSBuild servers report successful shutdown.
- P3 inventory matched its own pwsh command text, yielding one pwsh row and total1/own1. This is a query self-match, not a .NET process. Preserve that artifact. Self-discovered query correction P3b below excludes the current query PID; no process killing.

```powershell
$ErrorActionPreference = 'Stop'
$processes = @(Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -ne $PID -and ($_.Name -match '^(dotnet|testhost|VBCSCompiler|MSBuild)(\.exe)?$' -or $_.CommandLine -match 'vstest.console|testhost.dll|VBCSCompiler.dll|MSBuild.dll') } | Select-Object ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine)
$ownProcesses = @($processes | Where-Object { $_.CommandLine -like '*codex-pop-a-wheelie-1*' })
$inventory = [ordered]@{ processes = @($processes); totalCount = $processes.Count; ownCount = $ownProcesses.Count }
ConvertTo-Json -InputObject @($processes) -Depth 4 | Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-processes-01b.json'
ConvertTo-Json -InputObject $inventory -Depth 5 | Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-inventory-01b.json'
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-processes-01b.json'
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-prep-inventory-01b.json'
```

P3b elevated CIM result: explicit `[]`, totalCount0, ownCount0. .NET preparation ownership relinquished; WAITING for coherent source/check/root release. Asset hashes independently verified: SO 02263C07F6BB60F4D7F351A3667DCA84FD3E6C0FC6F838E18B5DE7CF4E2989E1; marker 1B7166AC37B166975F0E5313A3D4486EBAE73010E42FFB89BBD0D4536A29C81D. No C#/test/native/build/test/Git mutation occurred. Preparation self-discovered corrections2 (missing log directory before dotnet launch; inventory query self-match); reviewer corrective batches0, source-only questions0.

## Implementation release and coherent draft preparation

Root brought worktree forward to f3211b0c685dd40f844bfeb3b92c6661edc47f82 preserving preparation ledger and code baseline. Root explicitly released implementation against source BBB5628D6B7F7367CABB3BE3EAB36D0AD6C1B806880E4F36B49F7A01CD0E65AF/check A7C9D7C099555DB4F9EA414672E1C7C24709C4235C36AEEC708EB832C81AA43A and source-release.md. All3 published LF hashes independently matched source-freeze.json. No frozen source edits; harmless packet extra EOF blank retained.

D5 guard frozen by root before code: preserve full scalar/type1D/actual leaf+proxy tags/defined target,count1,false/local carrier/proxy/callback; IsPayloadDefinedfalse withholds only undefined fullbody/public dependent report and emits M1-034 + proposed Pop own-path/completion owner POP-A-WHEELIE-1 D5 at55268E..269C. No fake ID, zero padding policy or overwritten first candidate.

Draft production: configured NativePopAWheelieBehavior/cache8/factory/native DriveToPop composite/Pop leaf through checked shared IDrive+IDock. Source owns retry/result/reset via real M13-015 tag; broader lifecycle/cache/leaf still proposed unallocated owner. Legacy public/manual adapters retained with explicit partial/MISSING attribution; no API excluded or disabled. No builds/tests yet.

### Builder source question1 (pending)

P8 finaltrue tuple vs existing Flip LastFace false: does Pop actual shared IDrive constructor set LastFace sayName true? Need exact argument mapping; actual ordinary PI/2 face/object children retained, never dropped. Also confirm Pop generic IDock C0=true empty-space verification prefix and flag95=false; current draft reuses checked Roll generic body as released D3. Sent to source reader and root; additive checked followup required, frozen source unchanged. This is ONE raised builder source-only clarification, not a reviewer corrective batch.

### Legacy public/manual precise retained gap

ShippedBehaviors.Navigation Behaviors.cs creates public ordinary/Sparks PopAWheelieBehavior; conformance ManipTool.cs136 constructs ordinary public adapter (HardwareCatalog judge1086). Its async split drive/pop, immediate failure Finish, lack of cache refresh/typed callback/real scope and unconditional clifftrue Stop remain partial and visibly diagnosed. No blanket exclusion or fabricated bridge. Native configured M13-015 proposal requires Claude implementation/test pointer correction; root/reviewer must assess retained manual own-contract implications. Official files unchanged.

### Self review before coherent draft

No compile or test invocation. Corrected draft references to existing Action.ActionType, protected StopActing invocation via test reflection (actual cancellation input, no state injection), real DidFailToUse overload, source Select dock9 store before ordered-height branch, and fixed callback capturing the concrete interaction tag. These are initial construction/self-review corrections, no reviewer batches or source resolution attributed to tests. Preparation self-corrections remain separately recorded2.

### Q1 resolved (source-only, before coherent submission)

Root approved additive followup AAF2EB813530346D6FA834ADE8A275132FB217301E81A205E226DD521577E145 and independent check B4B1011F962DE8043C790830FE6B3E11E42C4146A449CEC05C0E0CAD9B9AF1C6 at root f9286331. Read both manager-worktree absolute files. Exact caller stack offsets5C7844/55D214/55B3B2 ->54B90A/54B7A8 establish finaltrue is face sayName+191; +193 is independently ctorzero. Updated initial unsubmitted draft to LastFace(...,true); test asserts actual live child SayName. Pop95false is independent C0true/C1false/C8true550382..3BA, so cloned shared VerifyNoObject/markerTurn prefix confirmed. Original frozen packet unchanged. No uncertainty remains for Q1; inherited named speech/face producer gaps remain precise. One builder source question, resolved by additive primary evidence/independent check; ZERO implementation reviewer corrective batches so far.

Coherent draft ready for independent read-only review before first build/test. New factory guards exact ordinary ID so unreleased Sparks sibling remains visible MISSING/NotImplemented, not excluded. Legacy public manual actual paths identified to root; no guessed bridge or API exclusion. No .NET since preparation; host ownership available for draft review only.

## Independent coherent draft review corrective batch1

Reviewer /root/pop_wheelie_review required change: NativeGetInAndHardSparkSkipRetainActualCompositeChildren used exact IsType<CompoundActionSequential> for actual subclass PopSequence. Changed to IsAssignableFrom, preserving real get-in turn+18A/order and all other assertions. Counts ONE required implementation reviewer corrective batch/round, including this draft. Reviewer found no production correction in finite review so far. Queued nonbehavioral comment encoding issue (new emdash byte97 invalid UTF8) corrected to ASCII dash in CubeGameBehaviors and NativePopAWheelieBehavior. Whole M13-015 remains partial due actual retained public/manual differences, not just pointer; no guessed bridge/exclusion. No build/test yet. Coherent draft resubmission1 follows.

## Corrected coherent draft PASS and first .NET commands

Independent reviewer corrected coherent draft READ-ONLY PASS after required batch1. Root conditional sole elevated .NET release applies. No restore repeated. Literal commands saved BEFORE invocation below.

B1 first solution build, all5 projects:
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' build 'cozmo-stack/Cozmo.sln' --no-restore -m:1 -nr:false -p:UseSharedCompilation=false *> '.scratch/pop-a-wheelie-1-builder-build-01.log'
$buildExit=$LASTEXITCODE
if($null -eq $buildExit){throw 'No dotnet build exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-01.exit' -Value $buildExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-01.log' -Tail 35
exit $buildExit
```

T1 focused filter, exact new suite (no build/restore):
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' test 'cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj' --no-restore --no-build --filter 'FullyQualifiedName~PopAWheelieNativeTests' --logger 'trx;LogFileName=pop-focused-01.trx' --results-directory '.scratch/pop-a-wheelie-1-builder-test-focused-01' *> '.scratch/pop-a-wheelie-1-builder-test-focused-01.log'
$testExit=$LASTEXITCODE
if($null -eq $testExit){throw 'No dotnet test exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-test-focused-01.exit' -Value $testExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-test-focused-01.log' -Tail 80
exit $testExit
```

T2 affected filter (new native plus legacy Navigation, shared cache callers, factory/container regressions):
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' test 'cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj' --no-restore --no-build --filter 'FullyQualifiedName~PopAWheelieNativeTests|FullyQualifiedName~NavigationTests|FullyQualifiedName~HikingRollCubeNativeTests|FullyQualifiedName~StackBlocksNativeTests|FullyQualifiedName~SparksRollNativeTests|FullyQualifiedName~SparksStackNativeTests|FullyQualifiedName~PickupPyramidPilotTests|FullyQualifiedName~M7BatchThreeBTests|FullyQualifiedName~KnockOverNativeTests' --logger 'trx;LogFileName=pop-affected-01.trx' --results-directory '.scratch/pop-a-wheelie-1-builder-test-affected-01' *> '.scratch/pop-a-wheelie-1-builder-test-affected-01.log'
$testExit=$LASTEXITCODE
if($null -eq $testExit){throw 'No dotnet test exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-test-affected-01.exit' -Value $testExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-test-affected-01.log' -Tail 60
exit $testExit
```

### B1 outcome / compile self-correction1

First actual solution build B1 exit1; production projects compile, test compilation failed with CS1061 at PopAWheelieNativeTests141/149: native SteppedBehavior has EngineRunning, not Status. Full log/exit preserved. No test launched on failed compilation. Corrected both assertions to Assert.True(pop.EngineRunning), preserving running-lifecycle observation rather than introducing a fake status. This is builder compile-discovered self-correction, not reviewer round2. Reviewer required batches remains1.

B2 corrected solution build literal, saved before invocation:
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' build 'cozmo-stack/Cozmo.sln' --no-restore -m:1 -nr:false -p:UseSharedCompilation=false *> '.scratch/pop-a-wheelie-1-builder-build-02.log'
$buildExit=$LASTEXITCODE
if($null -eq $buildExit){throw 'No dotnet build exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-02.exit' -Value $buildExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-02.log' -Tail 12
exit $buildExit
```

### B2/T1 outcomes / fixture self-correction2

B2 corrected build exit0, zero errors. T1 first actual focused test exit1: 18 passed/2 failed/0 skipped (20 total). Full TRX/log/exit preserved. Failures:
1. Cache oracle expected side7 eligible but test elevated cube9 atx60/z70 overlapped side7atx100 and was correctly treated as an object on top by CanInteract. Test scene bug: moved independently supplied elevated invalid candidate to x10/z70, keeping it closer than side7 but geometrically separate. Predicate expectations unchanged; no production-output-derived scene.
2. Leaf/proxy test incorrectly expected child leaf completion on E2G localhub, while native ActionWatcher delivers child destruction callbacks and the root proxy alone broadcasts on E2G. Added actual ActionList.RegisterActionEndedCallback observer for leaf; retain scalar/type/tag/defined fields comparisons against actual E2G root proxy and unavailable-fullbody checks. Did not weaken expected result or invent a completion.
These are builder-discovered focused fixture/oracle corrections, not reviewerrequired2; production unchanged. Reviewerrequired count1, sourcequestion1resolved, prep self2, compile self1, fixture corrective batch1 (two findings) now recorded.

B3 build literal saved before invocation:
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' build 'cozmo-stack/Cozmo.sln' --no-restore -m:1 -nr:false -p:UseSharedCompilation=false *> '.scratch/pop-a-wheelie-1-builder-build-03.log'
$buildExit=$LASTEXITCODE
if($null -eq $buildExit){throw 'No dotnet build exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-03.exit' -Value $buildExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-03.log' -Tail 8
exit $buildExit
```
T3 corrected focused literal saved before invocation:
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' test 'cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj' --no-restore --no-build --filter 'FullyQualifiedName~PopAWheelieNativeTests' --logger 'trx;LogFileName=pop-focused-02.trx' --results-directory '.scratch/pop-a-wheelie-1-builder-test-focused-02' *> '.scratch/pop-a-wheelie-1-builder-test-focused-02.log'
$testExit=$LASTEXITCODE
if($null -eq $testExit){throw 'No dotnet test exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-test-focused-02.exit' -Value $testExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-test-focused-02.log' -Tail 80
exit $testExit
```

### B3/T3 outcomes / fixture self-correction3

B3 build exit0. T3 focused exit1:19 passed/1 failed/0 skipped (20 total). Corrected geometry/cache and actual watcher-leaf/root-typed-LOCAL proxy assertions now pass. Remaining failure was diagnostic observation installed after constructing composite: SetNativeCompletionProxy already queries leaf carrier and emits deduplicated M1-034. Moved MissingReported observer before constructing that same actual composite. No production or oracle change; diagnostic must still be observed with exact M1-034/POP-A-WHEELIE-1 D5 text. Preserved log/TRX/exit. This is builder self-discovered observation ordering, not reviewer round2. Clarification: child ActionWatcher callback and root typed LOCAL E2G hub remain distinct from undefined raw/public body and body-dependent app report withheld under D5.

B4/T4 literals saved before invocation:
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' build 'cozmo-stack/Cozmo.sln' --no-restore -m:1 -nr:false -p:UseSharedCompilation=false *> '.scratch/pop-a-wheelie-1-builder-build-04.log'
$buildExit=$LASTEXITCODE
if($null -eq $buildExit){throw 'No dotnet build exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-04.exit' -Value $buildExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-04.log' -Tail 8
exit $buildExit
```
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' test 'cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj' --no-restore --no-build --filter 'FullyQualifiedName~PopAWheelieNativeTests' --logger 'trx;LogFileName=pop-focused-03.trx' --results-directory '.scratch/pop-a-wheelie-1-builder-test-focused-03' *> '.scratch/pop-a-wheelie-1-builder-test-focused-03.log'
$testExit=$LASTEXITCODE
if($null -eq $testExit){throw 'No dotnet test exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-test-focused-03.exit' -Value $testExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-test-focused-03.log' -Tail 80
exit $testExit
```

### B4 outcome / before T4 invocation diagnostic isolation self-check

B4 build exit0. Before invoking saved T4, inspected ReportMissing: MissingSeen is PROCESS-static, not just constructor-local. Moving observer alone cannot isolate the diagnostic against prior tests. Existing ResetMissingForTests is the explicit diagnostic-only test seam; this class owns the serial missing-report collection. Added that reset before constructing the diagnostic probe, without touching source action state/goal/result or its scalar/carrier oracle. T4 was NOT invoked on B4; preserved its saved literal as planned-not-run, no fabricated outcome. Rebuild B5 required after this self-discovered diagnostic isolation correction; no production change and no reviewer round added.

B5 build and T5 focused literals saved before invocation:
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' build 'cozmo-stack/Cozmo.sln' --no-restore -m:1 -nr:false -p:UseSharedCompilation=false *> '.scratch/pop-a-wheelie-1-builder-build-05.log'
$buildExit=$LASTEXITCODE
if($null -eq $buildExit){throw 'No dotnet build exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-05.exit' -Value $buildExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-05.log' -Tail 8
exit $buildExit
```
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' test 'cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj' --no-restore --no-build --filter 'FullyQualifiedName~PopAWheelieNativeTests' --logger 'trx;LogFileName=pop-focused-04.trx' --results-directory '.scratch/pop-a-wheelie-1-builder-test-focused-04' *> '.scratch/pop-a-wheelie-1-builder-test-focused-04.log'
$testExit=$LASTEXITCODE
if($null -eq $testExit){throw 'No dotnet test exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-test-focused-04.exit' -Value $testExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-test-focused-04.log' -Tail 80
exit $testExit
```

B5 build exit0. T5 (focused-04) exit0:20/20 passed,0failed,0skipped. No production changes after initial coherent source correction. Prior B1 compilefail, B2–B5 buildpass, T1(18/20), T3(19/20), T4 planned-not-run and T5(20/20) all retained separately. Proceeding to already-saved T2 affected filter on this exact code. Saving all changed tracked+untracked raw/LF pre-affected snapshot; later ledger-only outcomes are separately frozen for independent reviewer.

## Affected run / final integration maintenance

T2 actual affected exit1:253passed/1failed/0skipped,total254. Sole failure M7BatchThreeBTests.TheFactoryTableIsTheExtractionsTable implemented-class equality omitted newly released PopAWheelie. Full all10 pre/post-affected raw/LF/bytes compare EXACTLY equal, including ledger (pre/post manifests preserved). Root authorized exact source-backed class addition case0059CEA6/ctor005C7430; equality, all79 rows and every remaining assertion retained. This is builder-discovered integration test maintenance, not reviewerbatch2. Final paths now11 including M7BatchThreeBTests.

Read-only tracked diffcheck had passed, but untracked-file scanner found NativePopAWheelieRunner81 trailing8spaces and PopFirmwareScene extraEOFblank. Root authorized precisely those two cosmetic removals; preserved complete before/after bytes and unified deltas in .scratch/*pre-cosmetic/*post-cosmetic and pop-a-wheelie-1-builder-cosmetic-delta.json. No claim all10 still match the PRE-affected hashes AFTER these two whitespace-only source changes, M7 oracle maintenance and this ledger append. Independent reviewer must check exact final11 and execute final affected254 including M7; builder affected outcome remains FAILED until that independent corrected run. No builder test rerun solely for cosmetics/ledger.

B6 single final build literal, saved before invocation:
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' build 'cozmo-stack/Cozmo.sln' --no-restore -m:1 -nr:false -p:UseSharedCompilation=false *> '.scratch/pop-a-wheelie-1-builder-build-06-final.log'
$buildExit=$LASTEXITCODE
if($null -eq $buildExit){throw 'No dotnet build exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-06-final.exit' -Value $buildExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-build-06-final.log' -Tail 8
exit $buildExit
```
S1 final actual shutdown literal, saved before invocation:
```powershell
$ErrorActionPreference='Stop'
& 'C:/Program Files/dotnet/dotnet.exe' build-server shutdown *> '.scratch/pop-a-wheelie-1-builder-final-shutdown-01.log'
$shutdownExit=$LASTEXITCODE
if($null -eq $shutdownExit){throw 'No dotnet shutdown exit code'}
Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-final-shutdown-01.exit' -Value $shutdownExit
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-final-shutdown-01.log'
exit $shutdownExit
```
P4 final actual elevated native CIM literal, saved before invocation (excludes self-query PID, explicit array):
```powershell
$ErrorActionPreference='Stop'
$processes = @(Get-CimInstance Win32_Process | Where-Object { $_.ProcessId -ne $PID -and ($_.Name -match '^(dotnet|testhost|VBCSCompiler|MSBuild)(\.exe)?$' -or $_.CommandLine -match 'vstest.console|testhost.dll|VBCSCompiler.dll|MSBuild.dll') } | Select-Object ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine)
$ownProcesses = @($processes | Where-Object { $_.CommandLine -like '*codex-pop-a-wheelie-1*' })
$inventory = [ordered]@{ processes = @($processes); totalCount = $processes.Count; ownCount = $ownProcesses.Count }
ConvertTo-Json -InputObject @($processes) -Depth 4 | Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-final-processes-01.json'
ConvertTo-Json -InputObject $inventory -Depth 5 | Set-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-final-inventory-01.json'
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-final-processes-01.json'
Get-Content -LiteralPath '.scratch/pop-a-wheelie-1-builder-final-inventory-01.json'
```

## Final builder boundary (before independent run)

B6 final build exit0,0errors. All five solution projects built from final code, including exact source-backed M7 expected binding and two cosmetic whitespace corrections. No further builder test run. Final builder outcomes remain focused20/20 PASS0skip before final whitespace/oracle maintenance; affected253/254 FAIL1factoryoracle,0skip. Final independent B must execute the same saved T2 exact filter, including M7BatchThreeBTests, on the final binaries/final11 paths.

Required implementation reviewer batches:1 (draft get-in exact-type assertion -> assignable preserving source child/order checks). Builder source-only questions:1 resolved by checked additive constructor evidence; reader's earlier source-only packet correction is separate. Self outcomes retained: prep2; compile1; focused harness batch1(two findings); diagnostic-observation batch2; pre-invocation static-dedup isolation batch3; affected factory-oracle maintenance1; cosmetic cleanup1(two files). No source/production correction was discovered by tests. Original source/guard/Q1 releases unchanged.

All builder-raised MISSING/source questions retained: Q1 resolved (sayName191true/C0true independent95false). No other newly raised source unknown. Inherited frontiers remain visible: PlayAlone parent chooser/reload/admission and actual producer inputs (progression, face cache/recognition/calibration, named speech) stop only affected recipients; no fabricated face, winner, goal, status, terminal or hardware outcome. Ordinary factory release does not claim Sparks sibling; sibling returns visible NotImplemented/MISSING pending next job, not an exclusion. Firmware behavior remains offline input/hardware uncertainty.

M13-015 current whole record remains PARTIAL: native configured retry/full scalar category/result/failure/refresh/reset path built here, but public PopAWheelieBehavior via ShippedBehaviors.Navigation and ManipTool retains real async driving/typed-callback/refresh/scope/immediate-Finish/unconditional-reset differences. Labels visibly distinguish that adapter. No API removal, new exclusion, guessed routing bridge or claim whole record settled. Claude may update official implementation/test pointers when evaluating scope; no official fields changed here.

Proposed coherent unallocated owner: configured BehaviorPopAWheelie lifecycle/intention8 cache/ground target/get-in/pre-dock cliff ownership plus DriveToPopAWheelieAction and PopAWheelieAction Select/Verify/guarded completion (P1-P9/D1-D5). Explicitly EXCLUDES existing M13-015 retry/result/failure/reset ownership (P10-P13); shared IDrive/IDock/framework/geometry records retain theirs. D5 proposed owner's unresolved must name undefined unused words0055268E..269C and smallest fullbody/public dependent report skip under standing M1-034; exact local scalar/type1D/actual leaf+proxy tags/defined target,count1,false and lifecycle retained. No new fake ID, policy or official record assigned.

No official manifest/inventory/approval/PROJECT_STATE/REVIEW/Claude edits; no commit/push/main/hardware. Root owns integration/full gate. Final all11 raw/LF/UTF8/new-file/staged-equivalent whitespace freeze follows this complete ledger. Saved S1 actual elevated shutdown and P4 native explicit[]total/own counts follow freeze; their literal log/exit/inventory artifacts carry actual results. No file edits or .NET launches after exclusive independent reviewer release.

Final freeze attempt1 audit-tool failure: tracked gitdiffcheck and UTF8/newfile whitespace scanner found no error, but assertion wrongly required `git diff --no-index --check empty newfile` exit0; Git returns1 for a clean differing file with empty stdout (stderr only CRLF conversion warning). No freeze was produced, no code edit. Corrected audit assertion accepts0/1 with empty whitespace stdout; retains strict UTF8/trailing-space/extraEOF checks. This is a final audit-tool self-correction, not a test/production failure or reviewer batch.
