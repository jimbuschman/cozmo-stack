# Test-runtime audit

Date: 2026-09-25
Scope: read-only audit of `Cozmo.sln` at `main` commit
`2ad71a340543d3be32857a8244e90d2d6d889296`, checked out into the
`codex/test-runtime-audit` worktree. No production or test source was changed.

## Executive result

The audit is worth acting on. The suite is not broadly slow; one sequential test
class dominates it.

- Asset-backed warm suite: **1,360/1,360 passed**.
- Actual `dotnet test` wall clock: **617.876 s (10:17.876)**.
- TRX run window: **616.545 s (10:16.545)**.
- Sum of the 1,360 recorded test durations: **817.574 s (13:37.574)**.
- `IdleFaceTests` alone records **590.250 s** across six tests, or **72.2%** of all
  recorded test time and **95.7%** of the suite's TRX wall window. Tests in one
  xUnit class execute sequentially, so this class is the critical path.
- The first two `IdleFaceTests` independently execute the exact same
  `RunIdle(240_000, seed: 9)` simulation. Sharing that immutable result would
  remove roughly one 190-208 second test body without changing either assertion.
- The next material costs are whole-library Wwise checks: two separate passes over
  all 39 shipped songs cost **42.895 s** and **38.581 s**, and an all-event
  resolution sweep costs **10.371 s**.
- There is no unexplained positive wall-time gap. The recorded durations exceed
  wall time by **201.029 s** because xUnit runs up to two test classes concurrently.
  Only **1.331 s** lies outside the TRX window.

The high-value change is to keep focused regressions normal, move true endurance /
whole-library passes to an `Exhaustive` category, and give idle/timer tests a
lightweight deterministic face/timer seam. Do not shorten assertions merely to
make the run faster.

## Measurement method

1. Read `PROJECT_STATE.md` before inspecting the repository.
2. Created a separate `codex/test-runtime-audit` worktree from clean `main`.
3. Restored packages outside the timed measurement.
4. Built and ran once to establish the test binary and a baseline TRX.
5. The clean worktree could not discover the gitignored unpacked OBB, so that
   baseline let asset-dependent tests return early. For the final measurement, a
   temporary directory junction exposed the existing unpacked OBB to the audit
   worktree. It was removed immediately after the run. The active checkout and
   OBB contents were never written.
6. Ran the final suite warm so compilation and restore would not be confused with
   test runtime:

   `dotnet test cozmo-stack/Cozmo.sln --no-build --no-restore --logger trx`

7. Timed the whole process with `Stopwatch`, then parsed every
   `UnitTestResult.duration` from the TRX.

Environment observed by the run: .NET SDK 9.0.201, VSTest 17.13.0 x64, xUnit
2.9.2 with xUnit Visual Studio runner 2.8.2. The runner configuration caps
parallelism at two threads.

The final TRX was written outside the repository at
`%TEMP%\cozmo-test-runtime-audit\full-suite-assets.trx`; it is measurement output,
not a proposed repository artifact.

## Timing totals and concentration

The cumulative shares below use the **summed recorded test time** denominator.
That is the only additive denominator: shares of wall time would exceed 100% once
parallel tests overlap.

| Set | Cumulative recorded time | Share of summed recorded time |
| --- | ---: | ---: |
| Top 5 | 594.644 s | 72.733% |
| Top 10 | 721.918 s | 88.300% |
| Top 20 | 769.012 s | 94.060% |
| Top 50 | 804.889 s | 98.449% |

This is an unusually concentrated profile. Optimizing tests outside the top 20
cannot materially change the normal suite until the idle-face critical path is
addressed.

## Top 30 tests

Recommendations use exactly the requested dispositions. “Deterministic/fake
time” includes replacing the expensive face/display or asynchronous producer
with a controlled test seam while retaining separate renderer/producer integration
coverage.

| # | Test | Duration | What it is doing | Recommendation |
| ---: | --- | ---: | --- | --- |
| 1 | `IdleFaceTests.EyeScalesStayBoundedAndDoNotGrowCumulatively` | 207.610 s | 240 simulated seconds in 20 ms steps, cloning every pose and repeatedly rendering full faces | **share immutable fixture/setup** with #2; they call the identical seeded simulation |
| 2 | `IdleFaceTests.FacePositionStaysBoundedAcrossHundredsOfDarts` | 190.158 s | Same `RunIdle(240_000, seed 9)` as #1 | **share immutable fixture/setup** with #1 |
| 3 | `IdleFaceTests.ABlinkRestoresTheStableFaceRatherThanAMutatedDartPose` | 104.145 s | 120 simulated seconds with real procedural-face rendering | **convert to deterministic/fake-time execution** using a lightweight face/display sink |
| 4 | `IdleFaceTests.TheFaceIsAlwaysTheBasePlusOneGazeAndNeverASumOfThem` | 49.837 s | 60 simulated seconds with real rendering and per-tick assertions | **convert to deterministic/fake-time execution** |
| 5 | `WwiseBusTests.EverySongLeavesTheChainAtAboutTheSameLevel` | 42.895 s | Fully renders all 39 shipped songs through the bus chain | **move to an `Exhaustive` category while preserving it** |
| 6 | `KeepAliveTests.TheBodyShuffleIsDrivenAsAStraightOrATurnInPlace` | 39.823 s | 60 simulated seconds; idle face work renders along the way although the assertion is about body events | **convert to deterministic/fake-time execution** |
| 7 | `WwiseSongTests.EveryShippedSongRendersCompletely` | 38.581 s | Fully renders all 39 shipped songs, including very long source sequences | **move to an `Exhaustive` category while preserving it** |
| 8 | `IdleFaceTests.IdleForgetsItsBaseWhenSomethingElseTakesTheFace` | 26.841 s | 30 simulated seconds with procedural rendering | **convert to deterministic/fake-time execution** |
| 9 | `IdleFaceTests.ADartRampsToItsGazeOverItsDurationAndThenHoldsIt` | 11.657 s | 20 simulated seconds sampled every 10 ms with rendering | **convert to deterministic/fake-time execution** |
| 10 | `WwisePlaybackTests.EveryEventResolvesToAPlanDrawnFromItsOwnReachableRecordings` | 10.371 s | Resolves every event and independently computes each event's reachable media | **move to an `Exhaustive` category while preserving it** |
| 11 | `BehaviorHardeningTests.IdleStillBlinksWhenMotorsAreNotPermitted` | 9.179 s | 20 simulated seconds with live face rendering | **convert to deterministic/fake-time execution** |
| 12 | `KeepAliveTests.TheKeepAliveSendsTheHeadAngleTheRobotIsAlreadyAt` | 7.540 s | 10 simulated seconds; face work is incidental to a head-event assertion | **convert to deterministic/fake-time execution** |
| 13 | `WwiseSongTests.TheSingingStopEventEndsTheSongOnTheScheduler` | 5.942 s | Polls a real background renderer with `Thread.Sleep(1)` while pacing scheduler frames | **convert to deterministic/fake-time execution** with a controlled block-ready producer |
| 14 | `SearchForBlockTests.TheNearbySearchBacksOffAndLooksBothWays` | 5.062 s | Polls real time; the fake robot path is immediate, but the head/action completion path consumes about its five-second allowance | **convert to deterministic/fake-time execution** |
| 15 | `AnimationAssetTests.AClipWithAFaceAnimationTrackStreamsItsFrames` | 3.587 s | Reopens animation and face-animation libraries, streams and re-encodes a real clip | **share immutable fixture/setup** for the read-only asset libraries |
| 16 | `WwiseLiveVibratoTests.AVibratoPostedAfterTheSongHasBegunChangesTheRestOfIt` | 3.272 s | Renders the same shipped song twice around a parameter change | **share immutable fixture/setup** for immutable reference renders where seeds match |
| 17 | `CoreReviewTests.CORE006_AParameterPostedWhileTheRobotIsStalledStillReachesTheAudio` | 3.182 s | Includes a real 300 ms stall and waits for a background render, then renders the remainder | **convert to deterministic/fake-time execution** |
| 18 | `WwiseLiveVibratoTests.RenderingASongInBlocksGivesTheSameSongAsRenderingItWhole` | 3.173 s | Produces whole and block-wise renders of the same song | **share immutable fixture/setup** for the immutable whole-render reference |
| 19 | `MoodTests.AnimationSelectionIsMoodInvariantInThisBuild` | 3.084 s | Recursively parses every animation-group JSON and more than 1,000 entries | **move to an `Exhaustive` category while preserving it** |
| 20 | `WwiseLiveVibratoTests.TheChangeIsThePitchModulationTheBindingDescribes` | 3.073 s | Performs quiet and vibrato renders of the same full song | **share immutable fixture/setup** for the quiet reference |
| 21 | `WwiseSongTests.TheRenderSaysHowManyVoicesEachBranchOfTheSamplerContributed` | 2.798 s | Two deliberately different full-song renders exercise an important branch exclusion seam | **keep in normal regression suite** |
| 22 | `WwiseSongTests.AbaDabaRendersToTwelveSecondsOfSungNotes` | 2.765 s | Renders the same seeded song twice to assert determinism | **share immutable fixture/setup** with other identical seeded reference renders |
| 23 | `DerivedStateTests.OnBackFlipsWhileOnBackAndStopsWhenRighted` | 2.539 s | Loads shipped animations and waits for asynchronous animation stop/continuation observation | **convert to deterministic/fake-time execution** |
| 24 | `CoreReviewTests.CORE006_EveryFrameOfAPlayingSongCarriesSound` | 2.193 s | Polls up to two seconds per frame for a real background renderer | **convert to deterministic/fake-time execution** |
| 25 | `WwiseSongTests.EachPlayOfASongDrawsItsRecordingsAfresh` | 1.764 s | Two renders are intrinsic to the fresh-random-draw assertion | **keep in normal regression suite** |
| 26 | `WwiseSongTests.TheAudioSourcePlaysTheSongTheSwitchSelects` | 1.583 s | Prewarms and compares default and selected songs | **keep in normal regression suite** |
| 27 | `FreeplayTests.ASevereNeedTakesPriorityOverFreeplayAndTheGetInPlaysOnce` | 1.575 s | Fake manager time but 90 real `Thread.Sleep(5)` calls plus asset setup | **convert to deterministic/fake-time execution** |
| 28 | `WwiseClipWindowTests.ANoteHeldAtTheClipEndIsReleasedThereAndTheSongEndsAtTheSameInstant` | 1.545 s | One full shipped-song render pins a core clip-window rule | **keep in normal regression suite** |
| 29 | `DerivedStateTests.EveryShippedPlayAnimConfigLoadsAndResolves` | 1.522 s | Loads all 15 shipped PlayAnim configurations and resolves them through newly loaded assets | **share immutable fixture/setup** |
| 30 | `CoreReviewTests.CORE006_TheRenderStaysWithinALeadOfWhatHasBeenHeard` | 1.479 s | Real 400 ms stall plus polling up to two seconds for background progress | **convert to deterministic/fake-time execution** |

## Exhaustive whole-library and asset sweeps

These are the tests whose coverage intentionally grows with the shipped library,
not just tests that happen to open an asset once.

| Test or family | Measured time | Sweep | Disposition |
| --- | ---: | --- | --- |
| `WwiseBusTests.EverySongLeavesTheChainAtAboutTheSameLevel` | 42.895 s | Full render of 39 songs through the bus/effects/limiter chain | `Exhaustive` |
| `WwiseSongTests.EveryShippedSongRendersCompletely` | 38.581 s | Full render and validation of 39 songs | `Exhaustive` |
| `WwisePlaybackTests.EveryEventResolvesToAPlanDrawnFromItsOwnReachableRecordings` | 10.371 s | Every Wwise event; more than 550 must produce plans | `Exhaustive` |
| `MoodTests.AnimationSelectionIsMoodInvariantInThisBuild` | 3.084 s | Every animation-group JSON; more than 1,000 entries | `Exhaustive` |
| `DerivedStateTests.EveryShippedPlayAnimConfigLoadsAndResolves` | 1.522 s | All 15 shipped PlayAnim configs | Normal after shared fixture |
| `AnimationAssetTests.TheShippedClipsAllDecode` | 0.480 s | All indexed shipped clips from 289 animation files | Keep normal: cheap and broad |
| `AnimationAssetTests.EveryProceduralFaceKeyframeCarriesNineteenParametersPerEye` | 0.430 s | Every procedural-face keyframe in all clips | Keep normal: cheap and source-fidelity-critical |
| `AnimationAssetTests.TheBodyRadiusTokenIsKeptAsWrittenBecauseItIsAString` | 0.383 s | Every body keyframe in all clips | Keep normal |
| `TriggerTests.EveryShippedTriggerResolvesToAnAnimationThatExists` | 0.100 s | Every shipped trigger mapping | Keep normal |
| `AnimationAssetTests.EveryClipInsideAFileIsReachableNotJustTheOneNamedAfterIt` | 0.090 s | Every file and every clip indexed from it | Keep normal |
| `BehaviorFrameworkTests.EveryBuiltBehaviourMatchesAShippedConfig` | 0.030 s | Every built behavior JSON | Keep normal |
| `WwiseMusicTests.EveryHierarchyObjectInTheShippedBanksConsumesExactly` | 0.010 s | Every hierarchy object in loaded banks | Keep normal |

There is an important measurement caveat in the older `WwiseTests` class. Its
nominal `EveryShippedBankParsesExactly`, `EveryShippedAdpcmFile...`, and
`EveryShippedVorbisFile...` sweeps look specifically for a sibling
`re-analysis/obb/sound_meta` directory. The available unpacked OBB has the six
banks, WEM files, and `AudioAssets.zip` under
`assets/cozmo_resources/sound`, but no `sound_meta`. Those tests therefore return
early and report as passing in near-zero time. The newer `WwiseAssets` loader does
see and use the available sound directory, which is why the song sweeps above ran.
This audit does not propose changing that behavior, but its apparent zero cost
must not be interpreted as proof that those older exhaustive sweeps executed.

## Tests that wait for real engine/timer behavior

The suite mixes simulated timestamps with real sleeps, polling, thread-pool
continuations, and background audio production. The material cases are:

- `WwiseSongTests.TheSingingStopEventEndsTheSongOnTheScheduler` (**5.942 s**):
  waits in 1 ms increments for each next audio frame to become ready.
- `SearchForBlockTests.TheNearbySearchBacksOffAndLooksBothWays` (**5.062 s**):
  uses a wall-clock polling loop while the offline transport and motion tasks
  settle; the observed duration is essentially a five-second completion bound.
- `CoreReviewTests.CORE006_AParameterPostedWhileTheRobotIsStalledStillReachesTheAudio`
  (**3.182 s**): explicit 300 ms real stall plus worker completion.
- `CoreReviewTests.CORE006_EveryFrameOfAPlayingSongCarriesSound` (**2.193 s**):
  polls background rendering with per-frame two-second safety bounds.
- `DerivedStateTests.OnBackFlipsWhileOnBackAndStopsWhenRighted` (**2.539 s**):
  polls asynchronous animation shutdown/behavior continuation.
- `CoreReviewTests.CORE006_TheRenderStaysWithinALeadOfWhatHasBeenHeard`
  (**1.479 s**): explicit 400 ms real stall plus a two-second progress poll.
- `FreeplayTests.ASevereNeedTakesPriorityOverFreeplayAndTheGetInPlaysOnce`
  (**1.575 s**): advances a fake clock but also sleeps 5 ms on each of 90 ticks.
- `M3DeviceTests.M3_017_C9_APlayNeverSendsPastTheBudget` (**1.031 s**) and
  `M5AnimationTests.M3_013_C9_A18_TheProductionStreamStopsAtTheBudgetAndRefills`
  (**1.021 s**): exercise production tick/budget behavior on real scheduling.
- `EngineAppLayerTests.M1_025_M1_015_CB33_AFrameInFlightAtTheRemovalLeavesNothingBehind`
  (**0.327 s**): deliberately delays release of an in-flight detector by 300 ms.
- Several transport/control tests deliberately sleep 30-200 ms or wait for real
  timeout transitions. Individually they are below 0.3 s and are not meaningful
  contributors until the larger items above are fixed.

Not every long “simulated time” test is sleeping. The idle/keep-alive tests pass
timestamps directly, but each face change calls the real procedural renderer and
display path. Their minutes are primarily repeated rendering/encoding work, not
waiting for their nominal 20-240 simulated seconds to elapse.

## Repeated expensive initialization and loading

Static call-site counts in the test project:

- `AnimationLibrary.Open`: **18** sites.
- `robot.Animations.LoadFrom`: **19** sites.
- `AnimationTriggerMap.Load`: **24** sites.
- `MoodModel.Load`: **11** sites.
- `WwiseSoundLibrary.Load`: **13** sites.
- `MotionPrimitiveSet.FromObb`: **2** sites.

The animation library, trigger map, and mood model are immutable readers of the
same unpacked OBB for these tests. They are repeatedly reopened by asset,
behavior, freeplay, derived-state, trigger, and correction tests. A process-owned
read-only fixture per asset root would preserve coverage while removing repeated
index and JSON construction. `AnimationAssetTests` is the clearest local example:
several methods separately call `AnimationLibrary.Open`, and one separately opens
the face-animation library as well.

The main Wwise song classes already do the right thing: `WwiseAssets` owns a
thread-safe lazy `WwiseSoundLibrary` so the 141 MB `AudioAssets.zip` and six banks
are indexed once rather than once per class. Preserve that arrangement. The
direct loads in the older `WwiseTests` class are currently mostly latent because
of the missing `sound_meta` path described above; if that path is restored, those
loads should also use a shared immutable fixture unless a test specifically
verifies library construction or disposal.

## Serialization and parallelism

The suite is partly serialized for explicit reasons:

- `xunit.runner.json` sets `maxParallelThreads` to **2**. Its comment says the cap
  prevents four-core oversubscription from starving wall-clock-driven behavior
  rigs while whole songs are decoded and mixed. Raising the cap before removing
  wall-clock sensitivity would trade runtime for flakiness.
- xUnit runs tests within a class serially. All six `IdleFaceTests` therefore form
  one **590.250 s** chain. This one class accounts for almost the entire wall-clock
  critical path even though another class can run beside it.
- `M5 process statics` is a collection with `DisableParallelization = true`
  because those tests mutate process-wide animation state. Its measured cost is
  small (the whole `M5AnimationTests` class records 1.38 s), so it is justified and
  not a priority.
- The shared Wwise `Lazy` intentionally serializes first library initialization.
  That prevents multiple simultaneous 141 MB loads; it is a useful serialization,
  not a bottleneck to remove.

The correct order is therefore: remove real-time sensitivity and redundant idle
rendering first, then remeasure. Only after that should the two-thread cap be
revisited.

## Wall time versus summed durations

| Quantity | Seconds |
| --- | ---: |
| Measured `dotnet test` process wall | 617.876 |
| TRX `start` to `finish` | 616.545 |
| Sum of all per-test durations | 817.574 |
| Sum minus TRX wall (parallel overlap) | 201.029 |
| Process wall minus TRX wall (runner startup/reporting) | 1.331 |

The sum being larger than the wall clock is expected with two parallel class
workers. Average recorded concurrency over the TRX window is about **1.33**
(`817.574 / 616.545`). There is no unexplained idle interval: the only positive
outside-TRX gap is 1.331 seconds, which covers test-host startup, discovery, TRX
serialization, and process shutdown.

A first clean-worktree attempt also established that restore/build time must be
kept separate: no test could run before restore created `project.assets.json`,
and a build-producing run included compiler work outside the test durations. The
final result intentionally excludes restore and compilation so the report does
not mislabel them as test cost.

## Recommended order, without weakening coverage

1. Share the identical 240-second idle fixture used by the top two tests.
2. Add a deterministic lightweight face/display seam for idle and keep-alive
   state-machine tests; retain focused `ProceduralFaceRenderer` and display codec
   tests as the renderer integration coverage.
3. Put the three expensive whole-Wwise sweeps and the whole animation-group JSON
   sweep in `Exhaustive`; preserve them unchanged and run that category on a
   deliberate exhaustive lane.
4. Replace real sleeps/polling in audio, search, derived-state, freeplay, and
   production tick tests with controlled clocks/producers.
5. Share immutable animation library, trigger-map, mood-model, and face-animation
   fixtures.
6. Rerun the same asset-backed TRX audit. Reconsider `maxParallelThreads` only
   after the wall-clock-sensitive tests no longer depend on scheduler luck.

No test or coverage was changed by this audit.
