# Drive-off pilot load-timeout test fix

Base: `5dd557dab3ceabe4211e4330390c2206990c1945`, isolated `codex/pilot-load-timeout-fix`. Actual model/effort/service telemetry is unobservable. No production, global harness, official records, or timeout changes; no full suite, hardware or push. Manager authorized a normal-hook checkpoint commit after independent PASS.

## Change and evidence

Only `R2FloorPlacementTests.cs` changes. Approved manager FIX/A+B read identifies virtual transport pumping outrunning the real 1ms PlaceObjectOnGroundAction poll; CheckIfDone timeout runs before status inspection. The two lock tests and adjacent positive verify-result test now explicitly supply a constant standalone action clock. Existing assertions remain; lock tests additionally grade real status high/latch/clear, verify exactly once, and lock removal. The resource-free canonical manager has no reaction entries: the bare test retains its original actual-recipient checks, while held-lock assertions use the existing explicit local manager/override.

The new regression executes the real action RunAsync/poll with existing SignalTestContext, holds ALL fixture callbacks while actual Rig transport time advances over31s, proves pending/held lock/no verify, then supplies real status high and clear and proves Success/one verify/unlock. It uses a local21-node manager and substituted verify executor: unit scheduling/status coverage, not hardware. It cancels and drains/joins in finally before manager/rig/context/input disposal. No callback identity claim, sleeps or fabricated completion.

Dedicated `M15_022_InitSendsTheMessageThenStopsMotorsAndTheActionWaitsForTheStatusGate` is unchanged against HEAD (LF-normalized method UTF8 SHA256 `dd2ba2a3a7aa6c36e81c69e51bc4185a79f0c29918f13be07cc99091ac06f217`); exact100/129.9/130 expectations retained. Source file raw SHA256 `c6a292b613173f7265f549f4a800ce3b3d8132e7f4ad76573ac04ea9e066782d`.

## Actual run ledger

All commands ran from this worktree. Explicit executable `C:/Program Files/dotnet/dotnet.exe`; real Python313/dotnet prepended to PATH, PYTHONIOENCODING=utf-8. Logs/TRX under ignored `.scratch/pilot-load-timeout-fix/`. Escalated local testhost IPC authorized. Setup local junction creation first failed AccessDenied in sandbox; escalation succeeded. Existing local assets only.

Restore command: `dotnet restore cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --source C:/Users/JimBu/.nuget/packages --ignore-failed-sources --nologo -v:q`: exit0, `restore-1.log`.

Each test command used `dotnet test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-restore --nologo -v:q -clp:ErrorsOnly --filter FILTER --logger trx;LogFileName=STEM.trx --results-directory .scratch/pilot-load-timeout-fix`, console redirected to STEM.log (logger argument shell quoted).

| STEM | FILTER | Actual result |
|---|---|---|
| focused-m15-022-1 | FullyQualifiedName~R2FloorPlacementTests.M15_022_ | exit1,5P/2F/0skip,7total,1s. New delayed-poll and new bare canonical-held assertions failed Assert.True expectedTrue actualFalse (lines651/977): canonical map empty. Corrected new fixture to local manager; omitted only new invalid canonical-held assertion, preserving original assertions. |
| focused-r2-2 | FullyQualifiedName~R2FloorPlacementTests | exit0,36P/0F/0skip,4s. Before released adjacent verify-clock addition. |
| negative-coupled-1 | FullyQualifiedName~M15_022_ADelayedPollDoesNotSpendTheControlledActionClockWhileTransportAdvances | exit1,0P/1F/0skip,360ms. Only regression clock changed to old rig.Clock.NowMs/1000; failure: The resumed status poll must latch before completion; result=Timeout (line656). Action completed/joined safely. Exact corrected file bytes restored in finally and compared equal to saved bytes. |
| focused-r2-final | FullyQualifiedName~R2FloorPlacementTests | exit0,36P/0F/0skip,4s, all final edits included. |

The full-load mechanism is now deterministically reproduced and detected; repeated whole-suite load verification remains manager-owned. This does not claim whole-pilot completion, new production fidelity, physical validation or exact recurring wire producer identification. Independent affected review PASS: reviewer verified the exact source hash before/after, preserved timeout boundary/all original assertions, and executed36/36PASS,0fail,0skip,3s,exit0 (`independent-r2.trx`). Testhost stopped and source stable. Manager owns integration and both sequential full gates.

Independent exact command (console returned directly; no separate saved log): `C:/Program Files/dotnet/dotnet.exe test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-build --no-restore --nologo -v:q --filter FullyQualifiedName~R2FloorPlacementTests --logger trx;LogFileName=independent-r2.trx --results-directory .scratch/pilot-load-timeout-fix` (filter/logger shell quoted). TRX total36/executed36/passed36/failed0/notExecuted0. Separate HEAD method comparison and diff-check exit0.
