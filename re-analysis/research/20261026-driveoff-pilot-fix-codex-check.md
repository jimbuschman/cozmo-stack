# Drive-off pilot load-timeout FIX — independent Codex check

- Verdict: PASS for the bounded test-only correction. Reviewer: acceptance_blockers; different from builder map_import_builder. Requested Sol Medium; actual model/effort telemetry unavailable. No new native fidelity or hardware acceptance claim.
- Root cause: Rig.Pump advances transport/model time while PlaceObjectOnGroundAction awaits a real 1ms poll. A coupled action clock can exhaust its30s deadline before the continuation runs. Exact recurring work producer in Claude’s failures remains UNKNOWN; no concurrent native tick thread or lost one-shot status pulse established.
- Three positive status/verify/lock units now supply an explicit constant standalone action clock. Every original assertion remains; added actual status latch/clear, verify-once and lock-release checks. Dedicated100→129.9→130 timeout method is unchanged. No production/global harness/timeout change.
- New regression runs actual action initialization/polling/status, holding callbacks while actual transport advances>31s. Then supplied status high→latch→clear yields Success, one substituted verification and lock release. Constant clock, local reaction manager and substituted verify make this UNIT coverage, not full caller/hardware proof. Finally cancels and joins before disposal.
- Independent actual execution from isolated codex-pilot-load-timeout-fix:36executed/36passed/0failed/0skipped,3s,exit0. Command below; TRX .scratch/pilot-load-timeout-fix/independent-r2.trx. Raw R2 SHA256 before/after c6a292b613173f7265f549f4a800ce3b3d8132e7f4ad76573ac04ea9e066782d; diff-check PASS. No additional execution after integration.
- Builder old-clock negative control failed with Timeout at resumed-latch assertion; corrected bytes restored exactly. Reviewer inspected retained result, did not independently execute the negative control. This establishes the mechanism, not the precise original workload.
- Manager integration5c25c9a7; two consecutive clean full scripts/test-full.sh runs on b273fe6261f14b221797a025a7cd5a112764a22b PASS6362/6362 each,0failed/0skipped,6m20s then6m54s. Separate .scratch/pilot-fix-full-{1,2}.log. Unit review and full-load verification are distinct evidence.

```powershell
& 'C:/Program Files/dotnet/dotnet.exe' test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --no-build --no-restore --nologo -v:q --filter 'FullyQualifiedName~R2FloorPlacementTests' --logger 'trx;LogFileName=independent-r2.trx' --results-directory .scratch/pilot-load-timeout-fix
```
