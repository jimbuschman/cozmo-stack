BLOCKED 2026-09-29 10:25 - part of the scope is built and pushed, but the completion bar is not met: the robot's audio still does not come from the engine's path, because the bridge from a posted Play to audible output still needs unread engine bodies. This is the "build a verified part, name the rest precisely" outcome, not a failure of a built part.

## Committed and pushed (main)

| commit | scope |
| --- | --- |
| `b926d00` | Part 1: M6-023 app audio-input dispatch + M6-024 bank/scene loading. Verifier PASS (one blocking flags defect found and fixed). Full suite 1857/1857, 0 skipped. |
| `ca3a969` | Part 2: M6-025 the Play -> PBI -> voice -> source bridge. Verifier PASS after two fix rounds (6 blocking defects found and fixed). Full suite 1879/1879, 0 skipped. |

`fidelity.py --check` passes. The pre-push gate ran the full suite green on both pushes.

## What was built

**M6-023** (`WwiseEventRuntime.cs`): the `MessageGameToEngine` PostAudioEvent wire (`u32,u32,u16`, size 10), the envelope tags 1..6, the app play-id ++ / skip-0 and `callbackId = flag != EventNone ? id : 0`, the native dispatch into `WwiseEventRuntime.PostEvent`, the `flags = 1 | (ctx&2)<<1 | (ctx&1)<<3` formula (null context -> 0, app context 0xff -> 13). Tests `WwiseAppAudioInputTests`.

**M6-024** (`WwiseSoundLibrary.cs`): the recovered six-bank list in order (`Init, Music, UI, SFX, Cozmo, Dev_Debug`), `InitScene`, and `LoadScene`/the scene-ordered production `Load`. Tests `WwiseAudioSceneTests`.

**M6-025** (new `WwisePlayParams.cs`, `WwisePlayingInstance.cs`, `WwiseVoiceSources.cs`, `WwisePlaybackBridge.cs`): the node `PlayInternal` dispatch (Sound normal path), the node `vt+0x14` PBI creator and the PBI ctor fields, the start list `0x9D3558`/drain `0x9D3644` (with the `+0x154` keep rule), the voice ctor/attach (`0xA4304C`, `0xA548B8`, `0xA42DEC` fold), `AddSrc`/the source factory (Vorbis + ADPCM adapters), and the fade-in branch. Tests `WwisePlaybackBridgeTests` (19). Wired to the control path through a nullable `WwiseEventRuntime.PlaybackBridge` seam that is null in production, so the M6-006 path is unchanged.

## Why it is blocked

The job's bar is "the robot's audio comes from the engine's path". The path is still not complete: from a created voice to audible output, the following behaviour-changing bodies are **unread in the frozen inventory** (the implementer refused to guess them, and the verifier confirmed the refusal is correct):

1. **The per-voice bus/connection creation.** C21 creates a voice but no row attaches it to a `WwiseMixBus` (the connection list `voice+0x28` and the aux/dry entries). Without it the voice has nowhere to mix.
2. **`0x9BEB30`** (PBI parameter/source init, result must be 1), **`node->vt+0x90`**, and **`0xA00618`** (writes `pbi+0x1b8`/`+0x1bd`, calls `0x9FB994`/`0x9FF0D8`).
3. **The `0x9CD340` media-format/stream descriptor** (the `pbi+0x158` source-format word and the plugin/mode refinement).
4. **The fade-in setup bodies `0xA36268`/`0xA366F4` and `vt+0x50`.**
5. **`0xA42DEC`/`0x9D40C4`** (the voice link/teardown bodies) and the RanSeq/Switch/ActorMixer/Layer `+0x128` PlayInternal bodies (only Sound is read).
6. The Sound `PlayInternal` special branch (`params+0x84==0x90`) semantics, `params+0x14`, the PBI `+0x1F8`/`+0x1E4`/`+0x14C` meanings, `0x9BC90C`, and the `params+0x28..+0x6B` block initialiser.

A `WwiseEngineAudioSource` written now would render silence or require inventing those steps - the exact "unverified assumption between source-backed pieces" failure mode AGENTS.md warns against.

## Records

**No M6 record is settled.** M6-023, M6-024 and M6-025 all stay `IMPLEMENTATION_GAP`:
- M6-023's app-side A1/A2 callers are not modelled and the flags/callback are computed but not delivered to the core (no flags seam on `WwiseEventRuntime.PostEvent`).
- M6-024's loader is built but not wired into a live engine construction path (the engine construction path is the wiring job).
- M6-025 is a partial (Sound path only; Part B not done).
The M6 inventory gained corrections **C20** (the M6-023 flags) and **C21** (record M6-025) and **C22** (the play-params struct and the ADPCM type-1 source format), each re-approved.

## Operator listening check (for when the wiring lands - not runnable now)

Nothing audible comes from the engine's path yet, so there is nothing to listen to. When the wiring job lands:
1. Load the OBB's `AudioAssets.zip` and set the animation `AudioSource` to the engine-backed source.
2. Run `anim_bored_01` (the old hardware pass) and confirm the SFX/voice is audible and matches the original app.
3. Run the singing behaviour (M9) and confirm the song still plays.
4. Confirm a Stop event (`Stop__Robot_Sfx__Scan_Loop_Stop`) actually stops the loop.
Write the bundle to `re-analysis/acceptance/hardware/<stamp>-M6-engine-audio/` and bring it back.

## Note for the integrator

- The full suite was green at the commit gate, but `EngineAppLayerTests.M1_025_M1_015_CC27_AfterRemovalASecondConnectStartsFromAFreshRobot` failed once in a full run and passed in isolation and on the next full run. It looks flaky; it is not touched by this job.
- Queued cleanup: `IWwiseVoiceSource.StartStreamSucceeded` is documented as `vt+0x4C` (`[PBI+0x1BE]` bit6) but M6-025 uses it as the source's `[source+0x10]` bit0; the two native fields are distinct. Non-behavioural in this diff.
- Recommended next jobs: (1) an extraction pass for the six unread bodies above; (2) a build job wiring the engine-backed `IAnimationAudioSource` and switching the conformance tools off `WwiseAudioSource`; (3) then settle M6-023/024/025 and run the listening check.