# Job B-TESTHANG: the test host that never exits

- **Agent:** Codex, as builder. Follow `jobs/CODEX-BUILDER.md` rules 1–10.
- **Status file:** `status/B-TESTHANG.md`.
- **Runs after B-M3M4-4.** Test and resource hygiene only: no engine behaviour, no manifest change.
- **Symptom (2026-10-11):** `dotnet test` reported Cozmo.Protocol.Tests "Passed 3345" in 6m38s, then testhost.exe kept
  one core busy at about 9 GB for 25+ minutes and never exited, so the push gate hung. Intermittent: earlier pushes on
  near-identical code took about 5 minutes. A read-only search found no foreground thread that can outlive a passing
  test, so the candidates below explain the CPU and memory, not yet the non-exit.

## 1. Find the stuck thread first

Run the suite with `dotnet test cozmo-stack/Cozmo.sln --blame-hang-timeout 15m --blame-hang-dump-type full` (or take a
`dotnet-stack report` of a stuck testhost). Run it up to three times. Report what the dump or stacks show. If it doesn't
reproduce, say so and do section 2 anyway.

## 2. The candidates (fix each)

1. **`tests/Cozmo.Protocol.Tests/CorrectionTests.cs:511-529`.** The reader loops on a plain captured `bool stop`, and
   `reader.Wait(TimeSpan.FromSeconds(10))` ignores its result. Use `Volatile.Read`/`Volatile.Write` or a
   CancellationTokenSource, and `Assert.True(reader.Wait(...))`.
2. **Robots and transports created in tests without Dispose:** `BehaviorFrameworkTests.cs:1550` (`Offline()`),
   `M8BatchThreeCTests.cs:95` (`Ctx()`), `ControlTests.cs:741`, and the Rig classes in `DerivedStateTests.cs:66`,
   `SourceFidelityTests.cs:266`, `VisionTests.cs:800`, `RFixBatch3Tests.cs:202`, `M11RVisBuildTests.cs:2055`,
   `BehaviorHardeningTests.cs:317`. Each one stays reachable through the static `NetworkChange.NetworkAddressChanged`
   (`ReliableTransport.cs:319-320`) and `SteppedBehavior.MissingReported` (`CozmoEngine.cs:1796-1797`) subscriptions.
   Dispose them (`using`), and make the helpers return something disposable.
   - `TransportHardeningTests.cs:271-285` completes the executor before Dispose, so Dispose can't post its closure
     (`ReliableTransport.cs:1137`) and the dispatch thread leaks. Fix the test's order. If Dispose's behaviour with a
     completed executor looks wrong, report it as `MISSING:`; don't change the transport.
3. **`src/Cozmo.Robot/Animation/Wwise/WwiseMusicStream.cs:399-427`.** The BeginPlayback worker polls every 10 ms until
   cancelled or fully rendered, and holds the whole song's buffers. Make sure `WwiseAudioSource.Dispose`
   (`WwiseAudioSource.cs:247-248`) disposes its streams and prewarms, and that the tests (`WwiseSongTests.cs:524-545`)
   dispose what they create. Whether the worker should end once playback stops: check its fidelity records first. If
   that changes rendering behaviour, report it as `MISSING:` instead.
4. Lower priority: `HardwareRunnerTests.cs:1234` leaves a never-completing task behind (cancellation `None`).

**Report:** what section 1 found; each fix; the suite's wall time before and after; `fidelity.py --check`. Then stop.
