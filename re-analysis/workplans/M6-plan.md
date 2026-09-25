# M6 Wwise/audio runtime implementation plan

**Status:** documentation-only implementation handoff, derived from the frozen M6 inventory and the read-only comparison completed after `fee8724`.

**Authority:** [`re-analysis/inventory/M6-wwise-bank.md`](../inventory/M6-wwise-bank.md) and its approved snapshot. This plan does not replace or amend the frozen inventory. If this plan and the frozen inventory differ, the frozen inventory wins.

## Executive conclusion

The current M6 candidate is not a mostly complete Wwise runtime with a few missing algorithms. Its central architecture is different from the recovered engine:

- the current path resolves an event immediately into an offline tree, decodes complete media, lays out complete voices, mixes a whole buffer, and only then hands 22,320 Hz PCM to the animation scheduler;
- the recovered Wwise engine queues events and actions, assigns playing IDs, creates live voices, advances them in sink-driven 48 kHz/1,024-sample frames, mixes through live bus connections and effects, and lets the Hijack plug-in resample the bus output into 744-sample robot chunks.

Bank/media loading, much of the shipped-data hierarchy model, media metadata, codebook/rebuilder support, effect-parameter parsing, M3 robot framing, and the M5 scheduler boundary are reusable. `WwisePlayback`, `WwiseAudioSource.Produce`, `WwiseAudioSource.ToRobotRate`, and the production role of `WwiseBusChain` are not a suitable architecture for the recovered runtime and are better replaced than incrementally extended.

All M6-001 through M6-018 remain `IMPLEMENTATION_GAP` in the approved manifest. Audible output, whole-library decoding, or shipped-bank consumption tests do not by themselves settle source fidelity.

Requirement labels used below:

- **Arbitrary:** general playback of shipped Wwise events.
- **Animation:** source-correct Anki animation SFX and voice.
- **Singing:** also required by M9 singing/music, although not necessarily unique to M9.
- **Fidelity-only:** needed to settle the complete record but not required for the current common shipped case to produce sound.

## Record-by-record implementation status

### M6-001 — Bank and HIRC readers

- **Capability:** reads Event, Action, Sound/source, Random/Sequence, Switch, ActorMixer, Bus, Layer, NodeBase, RTPC, and STMG data in Wwise 2016.2 runtime order.
- **Current status: partial.** `WwiseBank.Parse` and `ReadHirc` are reusable. Much of `WwiseHierarchy` consumes shipped objects correctly. However, Event/Action access remains offset-oriented; `ReadRtpc` reads a one-byte parameter ID rather than a varint; STMG defaults are absent; positioning, source-plugin, BKHD-feedback, and bus conditional branches are simplified; the native Bus and Layer readers have not been reproduced as frozen.
- **Files/functions:**
  - `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBank.cs`: `Parse`, `ReadHirc`, `EventActions`, `PlayActionTarget`, `SoundMediaId`.
  - `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseHierarchy.cs`: `TryRead`, `ReadNodeParams`, `ReadRtpc`, `ReadSound`, `ReadRandomSequence`, `ReadSwitch`, `ReadActorMixer`, `ReadBus`.
  - `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBus.cs`: bus/effect data types.
  - `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSoundLibrary.cs`: bank aggregation and the likely home for STMG defaults.
- **Dependencies:** foundation for every later M6 batch and for the M9 hierarchy consumer.
- **Needed for:** Arbitrary, Animation, Singing. Conditional branches unused by shipped banks are Fidelity-only for current assets, but are part of this frozen record.
- **Reuse/rebuild:** retain the chunk/DIDX/HIRC envelope and node data model where it can represent the native fields. Correct or replace the per-type readers and the shortcut accessors.

### M6-002 — Native Wwise Vorbis decoder

- **Capability:** decodes Wwise's Tremor-lowmem fork: stripped setup, built-in codebooks, one-bit mode, integer residue/coupling/dequantization, Tremor floor table, float IMDCT/windows, planar float output, and native skip/trim.
- **Current status: wrong.** `WwiseVorbis.Decode` rebuilds standard Ogg and delegates to NVorbis. It successfully decodes the asset library, but it is not the source-backed decoder and does not reproduce the native arithmetic and trim path.
- **Files/functions:**
  - `WwiseVorbis.cs`: `Decode` is the decoder core to replace.
  - `WwiseVorbisRebuilder.cs` and `WwiseCodebookLibrary.cs`: retain as diagnostic/cross-check utilities and possible sources for bitstream/codebook parsing, not as proof of native decoding.
  - `WwiseMedia.cs`: retain media/header parsing, correcting it only where Batch 5 requires native header fields.
- **Dependencies:** M6-001 media metadata; the live voice and resampler interfaces from Batches 2 and 3.
- **Needed for:** Arbitrary, Animation, Singing. It cannot wait for M9 because ordinary animation voice assets use Vorbis extensively.
- **Reuse/rebuild:** rebuild the decoder core from the frozen evidence; do not wrap NVorbis and classify it as equivalent.

### M6-003 — Wwise IMA ADPCM

- **Capability:** exact mono/stereo Wwise IMA decoding: predictor emitted as sample 0, then 63 nibbles, unused final high nibble, exact multiply-based difference, and stereo `[ch0 36 B][ch1 36 B]` layout.
- **Current status: wrong.** Stereo block placement is reusable, but the decoder does not emit the predictor, decodes 64 nibbles, consumes the unused high nibble, and uses the shift-sum IMA formula rather than `((2*(n&7)+1)*step)>>3`.
- **Files/functions:** `WwiseAdpcm.cs`: `Decode`, `Step`.
- **Dependencies:** M6-001 media parsing; then M6-004 and M6-010 through M6-012.
- **Needed for:** Arbitrary and Animation; some non-singing music assets also use it.
- **Reuse/rebuild:** small contained correction, not an architectural rebuild.

### M6-004 — CAkResampler and pitch ramp

- **Capability:** source-rate-to-mix-rate int16 Q16 linear voice resampling, 48 kHz-to-22,320 Hz float Hijack resampling, native step formula, bypass scaling, and 0x400-phase pitch ramps.
- **Current status: wrong.** Media is converted directly to 22,320 Hz through a Lanczos sinc, then pitch is applied by nearest-sample stepping. The two native stages, Q16 phase, linear kernels, bypass scale, and pitch ramp do not exist.
- **Files/functions:** `WwiseAudioSource.cs`: `DecodeMedia`, `Produce`, `ToRobotRate`, `Lanczos`; replacement live voice and Hijack resampler classes will likely be new files.
- **Dependencies:** M6-018 constants, codecs, voice mixer, and M6-015 Hijack output.
- **Needed for:** Arbitrary, Animation, Singing.
- **Reuse/rebuild:** remove `ToRobotRate` from the production Wwise path. A diagnostic/offline helper may remain only if clearly non-production.

### M6-005 — Wwise name hash

- **Capability:** FNV-1 32-bit with ASCII lowercasing over at most 0x103 bytes.
- **Current status: partial.** FNV-1 order and ASCII lowercasing are correct. The 0x103-byte bound is absent, and the method simply narrows .NET `char` values to bytes. Known short shipped names hash correctly.
- **Files/functions:** `WwiseHash.cs`: `Of`.
- **Dependencies:** configuration/name entry points and M9 switch/song configuration. Numeric animation keyframe event IDs do not require it.
- **Needed for:** Arbitrary name APIs and Singing configuration. The missing length bound is Fidelity-only for known shipped names.
- **Reuse/rebuild:** retain the function and add the exact bounded byte semantics established by the inventory.

### M6-006 — Queued event/action control path

- **Capability:** queued `PostEvent`, atomic playing IDs, action-order execution, scope rules, frame/remainder delays, FIFO pending actions, Play/Stop/Seek, and switch resolution.
- **Current status: wrong.** `WwisePlayback.Resolve` immediately returns an offline play tree. There is no queue, playing-ID lifetime, delayed pending list, action scope, frame scheduling, or native Stop/Seek execution. Switch default/list resolution is partially reusable.
- **Files/functions:**
  - `WwisePlayback.cs`: `Resolve`, `Walk` and plan records.
  - `WwiseAudioSource.cs`: `IsStopEvent`, `StopAffects`, `GetPcm`.
  - likely new event queue, playing-event, pending-action, and runtime-state files.
- **Dependencies:** M6-001 and M6-018; it underpins M6-007 through M6-010 and M6-016/M6-017.
- **Needed for:** Arbitrary, Animation, Singing.
- **Reuse/rebuild:** replace the offline plan as the production control path. It may remain as a diagnostic enumerator if it is not used to claim runtime fidelity.

### M6-007 — Random and sequence selection

- **Capability:** native time-seeded 64-bit LCG, shared selection state, eligible-item choice, shuffle bits, avoid list, weights, wrap, and ping-pong.
- **Current status: wrong.** The implementation uses `System.Random`, Fisher-Yates shuffling, one remembered item, and a modulo cursor.
- **Files/functions:** `WwisePlayback.Shuffle`, `NextInSequence`, `WeightedPick`; `WwiseAudioSource` fields `_eventRandom`, `_eventCursor`, `_eventLastPick`; M9 has duplicated reduced selection in `WwiseSongRenderer`.
- **Dependencies:** M6-006 live event/container state and M6-001 parsed fields.
- **Needed for:** Arbitrary, Animation, Singing.
- **Reuse/rebuild:** replace the selection engine. Preserve only neutral parsed playlist data.

### M6-008 — Continuous containers

- **Capability:** native loop counts, selection when the current voice starts, modes 1/2 cross-fades, mode 4 same-voice chaining, mode 5 periodic triggering, and final-PBI completion.
- **Current status: wrong.** Continuous containers are flattened into a sequence; random continuous containers are shuffled wholesale. The recovered loop, timing, cross-fade, chaining, and completion behavior is absent.
- **Files/functions:** `WwisePlayback.Walk` continuous branch; `WwiseAudioSource.Produce` sequence layout; replacement live voice/PBI continuation state.
- **Dependencies:** M6-006, M6-007, M6-017, live voices and resampling.
- **Needed for:** Arbitrary, Animation, Singing. This record is not safely deferrable to M9 because ordinary shipped events also use continuous containers.
- **Reuse/rebuild:** rebuild on the live runtime; do not extend the whole-buffer `WwisePlaySequence` representation as the production implementation.

### M6-009 — RTPC curves and value store

- **Capability:** every recovered curve shape, post-curve scaling, sum/product accumulation, playing-ID/game-object/root/STMG precedence, empty-key bus lookup, and immediate ramps.
- **Current status: partial.** RTPC records, linear/constant interpolation, and an event-volume lookup exist. Other shapes are reduced to linear, scaling is explicitly omitted, and accumulation, scoped values, STMG fallback, bus semantics, and playing-ID precedence are absent.
- **Files/functions:** `WwiseHierarchy.cs`: `WwiseRtpc.Evaluate`, `ReadRtpc`; `WwiseAudioSource.cs`: `SetParameter`, `GainForKeyframeVolume`, `EventVolumeBinding`; new scoped RTPC store/runtime classes.
- **Dependencies:** M6-001 and M6-006; feeds gain/filter behavior and M9 modulators.
- **Needed for:** Arbitrary, Animation, Singing.
- **Reuse/rebuild:** retain parsed curve data after M6-001 fixes; replace evaluation and add the source-backed value store.

### M6-010 — Gain, hierarchy, and aux routing

- **Capability:** node/parent/output-bus accumulation, one-time randomizers, fast dB conversion, mute/fade product, game-defined aux send, muted dry route, and recovered bus-volume placement.
- **Current status: partial.** Node volume/pitch are summed down an offline walk and converted with ordinary `Math.Pow`. The voice randomizer, source-backed fast power, aux decision, dry/output connection model, bus gain placement, and collapsed buses are missing.
- **Files/functions:** `WwisePlayback.Walk`; `WwiseAudioSource.Produce`; `WwiseNodeParams`; replacement live voice parameter and connection classes.
- **Dependencies:** M6-007, M6-009, and the live voice/bus graph.
- **Needed for:** Arbitrary, Animation, Singing.
- **Reuse/rebuild:** retain node properties; rebuild the production parameter accumulation and routing path.

### M6-011 — Voice LPF/HPF

- **Capability:** pre-aux Butterworth Filter A with recovered cutoff mapping and eight-step ramps; dry-only Filter B.
- **Current status: missing.** `WwiseBusChain.Biquad` is bus EQ, not the voice filter. No voice filter state or mapping exists, and the shipped top mixer LPF 15 is ignored.
- **Files/functions:** new voice-DSP code integrated between voice resampling/gain and the aux sends; `WwiseBusChain.Biquad` is related arithmetic but not a correct implementation of this record.
- **Dependencies:** M6-009, M6-010, M6-018.
- **Needed for:** Arbitrary, Animation, Singing; affects ordinary animation tone, not only music.
- **Reuse/rebuild:** implement as a live per-voice filter; do not reuse the offline bus biquad blindly.

### M6-012 — Frame mixer and channel routing

- **Capability:** live per-connection linear frame gain ramps, no ramp on first update, unity mono routing, and 0.70710677-per-channel stereo-to-mono routing.
- **Current status: wrong.** Current whole-buffer summing has no connection state or frame ramps. Stereo is averaged at 0.5 per channel, and pitch stepping is nearest-neighbor.
- **Files/functions:** `WwiseAudioSource.Produce`, `ToRobotRate`; replacement connection/mixer classes.
- **Dependencies:** M6-004, M6-010, M6-011, M6-017, M6-018.
- **Needed for:** Arbitrary, Animation, Singing.
- **Reuse/rebuild:** rebuild as a frame mixer.

### M6-013 — Robot bus EQ and limiter

- **Capability:** two Parametric EQs and Peak Limiter in bank slot order before Hijack, with recovered settings and algorithms.
- **Current status: partial.** Bus/effect parsing and slot ordering are reusable. DSP is a generic double-precision approximation at 22,320 Hz. The limiter deliberately omits the native delay, peak hold, and tail, and EQ output ramps/native arithmetic are absent.
- **Files/functions:** `WwiseBusChain.For`, `Add`, `ProcessBlock`, `ApplyLimiter`, `Biquad.Design`, `Biquad.Process`; `WwiseEffectNode.ParametricEq`, `PeakLimiter`.
- **Dependencies:** M6-012, M6-014, M6-018.
- **Needed for:** Arbitrary robot-routed playback, Animation, Singing.
- **Reuse/rebuild:** retain parameter parsing and effect order; rebuild the production DSP/lifetime implementation.

### M6-014 — Bus and Hijack lifetime

- **Capability:** on-demand bus creation, lazy effect construction, idle-frame destruction, and one empty-input tail/zero-length chunk.
- **Current status: missing.** `WwiseBusChain` is a persistent offline object and resets at the start of a buffer. There is no activity-scoped bus/FX lifetime or final tail protocol.
- **Files/functions:** `WwiseBusChain.Process`, `ProcessBlock`, `Reset`; replacement mix-bus and effect-instance lifetime code.
- **Dependencies:** M6-013, M6-015, M6-017.
- **Needed for:** Arbitrary, Animation, Singing. Some open/close and tail details are more fidelity-critical than basic audibility.
- **Reuse/rebuild:** rebuild the production lifecycle; keep the existing chain only as a diagnostic if clearly separated.

### M6-015 — Anki Hijack plug-in

- **Capability:** registration, last-caller callback selection, 1,024-float/channel allocation, linear resampling to 22,320 Hz, 744-sample updates, and prepare/close callbacks.
- **Current status: missing.** The current Hijack stage is a no-op label. M3/M5 can send prepared 744-sample frames, but the Wwise plug-in lifecycle that produces those frames does not exist.
- **Files/functions:** `WwiseBusChain.Add` Hijack branch; new Hijack plug-in/runtime code; downstream reusable endpoint `CozmoAudio.ToFrames` and `AnimationScheduler` buffering.
- **Dependencies:** M3's settled 22,320 Hz/744-sample output, M6-004, M6-013, M6-014, M6-017.
- **Needed for:** Arbitrary robot-routed playback, Animation, Singing.
- **Reuse/rebuild:** implement the plug-in boundary; do not move M3's robot transport semantics into M6.

### M6-016 — Anki robot-audio integration

- **Capability:** up-front keyframe alternative draws, wall-clock event posting, playing-ID event volume, game objects 7 through 10 and their buses, queued callbacks, loading/readiness/pop states, abort, and the source-backed silent Nurture event.
- **Current status: partial.** M5 chooses alternatives, requests PCM, streams 744-sample frames, stops the current stream, and aborts. It draws when a keyframe starts rather than during animation initialization, and lacks playing-ID parameters, game-object routing, Wwise callbacks, native loading state, and the recovered silence behavior.
- **Files/functions:** `AnimationScheduler.AudioAnimation.Start`, `Pop`; `WwiseAudioSource.GetPcm`, `SetParameter`, `IsStopEvent`; `IAnimationAudioSource`. A new M6 runtime-backed animation audio source/adapter is likely.
- **Dependencies:** accepted M5 scheduler, M3 robot stream, and all preceding M6 runtime/signal records.
- **Needed for:** Animation directly and the animation portion of Singing.
- **Reuse/rebuild:** preserve the M5 scheduler and output seam; replace the whole-PCM M6 side with readiness/pop behavior backed by the live runtime.

### M6-017 — Audio-thread frame model

- **Capability:** sink-driven frame execution ordered as messages, pending-action drain, render/bus/notification flush, then tick; source-backed EndOfEvent timing.
- **Current status: missing.** M5 has robot-output pacing, but M6 has no Wwise audio-thread model or internal 48 kHz frame pump. Whole PCM is produced synchronously before robot framing.
- **Files/functions:** new Wwise runtime/frame driver replacing the production roles of `WwisePlayback` and `WwiseAudioSource.Produce`; downstream `AnimationScheduler.Update` and `StreamSendBuffer` remain separate M5/M3 consumers.
- **Dependencies:** M6-006, M6-008, M6-012 through M6-016, M6-018.
- **Needed for:** Arbitrary, Animation, Singing.
- **Reuse/rebuild:** new central runtime architecture required.

### M6-018 — Internal mix rate and frame size

- **Capability:** this stack's forced internal Wwise policy: 48,000 Hz and 1,024 samples per engine frame, kept distinct from the robot's 22,320 Hz/744-sample endpoint.
- **Current status: wrong.** Current Wwise rendering is centered directly on `CozmoAudio.SampleRate` and arbitrary whole-buffer sizes. No separate internal domain exists.
- **Files/functions:** `WwiseAudioSource` constructor, `DecodeMedia`, `Produce`, `ToRobotRate`; `WwiseSongRenderer.Rate`; `WwiseBusChain.For`; introduce a single M6 runtime settings/constants owner rather than changing `CozmoAudio.SampleRate`.
- **Dependencies:** foundational for M6-004, M6-006, M6-008, and M6-011 through M6-017.
- **Needed for:** Arbitrary, Animation, Singing.
- **Reuse/rebuild:** add the distinct internal domain. Do not alter M3's correct 22,320 Hz/744-sample constants.

## Capability boundaries

### Minimum M6 subset for correct Anki animation audio

Source-correct playback across arbitrary shipped animation SFX and voice requires **M6-001 through M6-004 and M6-006 through M6-018**.

M6-005 is the only record outside the strict minimum because animation keyframes already carry numeric event IDs. Its current short-name behavior is enough for known shipped configuration strings, but the length-bound gap still prevents the record from being settled.

The large minimum is intentional. Correct animation audio means correct event/action/container selection, live voice timing, RTPC/gain/filter/mix behavior, bus effects, Hijack conversion, robot chunking, and completion—not merely finding a `.wem` and producing audible PCM.

### What can wait for M9

No complete M6 record is purely singing/music-only. Singing consumes the same codecs, selection, RTPC, voice, mixer, bus, Hijack, and timing runtime.

The following M9-specific work can wait until M6 is settled:

- MIDI parsing and dispatch;
- music switch/playlist/segment/track scheduling;
- note-on/note-off sampler behavior;
- singing vibrato and behavior orchestration;
- song prewarming/streaming policies.

Do not defer M6-008 wholesale: ordinary shipped events also use continuous containers. Do not defer M6-002: ordinary animation voice assets use Vorbis.

### M3 and M5 dependencies

- No M6 gap blocks M3's settled ability to send arbitrary caller-provided PCM. M6 consumes M3's output endpoint; it does not require reopening M3.
- M5 motion, face, scheduling, budgeting, and cancellation remain settled.
- M5's **shipped Wwise audio fidelity** is blocked by M6:
  - keyframe volume needs M6-009/M6-010;
  - audio readiness and completion need M6-016/M6-017;
  - event-internal alternatives need M6-007;
  - decoded samples and tone need M6-002 through M6-004 and M6-010 through M6-015.

The old `anim_bored_01` hardware pass establishes that the offline candidate can locate, decode, and transmit an audible shipped sound. It does not validate the frozen M6 runtime contract and predates the native-runtime inventory.

## Reuse map

### Retain and correct

- `WwiseBank.Parse`, bank chunk handling, HIRC envelope reading, DIDX/media extraction.
- `WwiseMedia` and `WwiseSoundLibrary` asset lookup.
- The existing node data model where it can represent the frozen fields.
- Much of the shipped-data hierarchy reader after correcting the conditional and omitted paths.
- `WwiseCodebookLibrary` and `WwiseVorbisRebuilder` as diagnostics/cross-checks.
- `WwiseBusNode`/`WwiseEffectNode` parameter parsing and effect slot ordering.
- M3 robot framing and transport.
- M5 animation scheduling and the animation-audio boundary.
- `WwiseAdpcm` and `WwiseHash` as small, contained corrections.

### Replace as production architecture

- `WwisePlayback`'s offline plan as the event runtime.
- `WwiseAudioSource.Produce` whole-buffer layout and mixing.
- `WwiseAudioSource.ToRobotRate` on the Wwise production path.
- `WwiseBusChain` as the production bus/effect lifetime implementation.
- `WwiseVorbis.Decode`'s NVorbis core.

These existing components may remain as explicitly diagnostic/offline tools if they are not used to make runtime-fidelity claims.

## Ordered repair batches

### Batch 1 — Bank contract and runtime constants

**Records:** M6-001, M6-005, M6-018.

Correct the bank readers, STMG/default representation, bounded hash, and the distinct 48 kHz/1,024-sample Wwise domain. This must come first because every later batch depends on the parsed fields and timebase.

### Batch 2 — Event, container, RTPC, and frame runtime

**Records:** M6-006, M6-007, M6-008, M6-009, M6-017.

Replace the offline play plan as the production control path with queued events, playing IDs, pending actions, native selection state, continuous-play state, scoped RTPC values, live voices/callbacks, and the recovered audio-frame order.

These records form one state model. Splitting them further would require temporary invented interfaces between queueing, selection, continuation, parameter lookup, and completion.

### Batch 3 — ADPCM voice and mixer path

**Records:** M6-003, M6-004, M6-010, M6-011, M6-012.

Implement the exact ADPCM decoder, native voice resampling/pitch, gain and aux parameters, voice filters, channel routing, and live frame mixer. ADPCM provides a bounded exact codec for exercising the runtime before the larger Vorbis port.

### Batch 4 — Robot bus, Hijack, and animation bridge

**Records:** M6-013, M6-014, M6-015, M6-016.

Implement the recovered EQ/limiter, bus/effect activity lifetime, Hijack resampling/chunk callbacks, and the M5-facing loading/readiness/pop/abort bridge. This completes the first end-to-end hardware-capable ADPCM animation path.

### Batch 5 — Native Vorbis port

**Record:** M6-002.

Port the frozen Tremor-lowmem behavior behind the already-established live voice interface. Keeping the large codec port separate prevents it from obscuring control, scheduling, and signal-path defects.

M9 singing/music work follows these batches and must not be folded into them.

## Frozen-evidence cautions

- The approved M6 inventory supersedes older `WWISE_AUDIO.md`, `WWISE_MUSIC.md`, `HANDOFF.md`, and `ACCEPTANCE.md` statements that the Wwise runtime does not ship.
- The runtime is statically linked into `libcozmoEngine.so`; do not restore `BLOCKED_EXTERNAL` classifications for behaviors recovered in M6.
- The inventory's bounded residuals remain unresolved and must remain explicit: mode-4 failure fallbacks, the continuous-container child case, several notification/prefetch timing details, and exact float-IMDCT normalization.
- Hardware success cannot raise provenance. Whole-library decode success cannot establish native codec arithmetic. Exact asset consumption cannot establish source-correct field order.

# NEXT IMPLEMENTER: START HERE

Implement **Batch 1 only: M6-001, M6-005, and M6-018**. Do not begin the event runtime, codecs, DSP, Hijack, or M9 work in this batch.

## 1. Read and preserve the frozen contract

Use the following frozen rows and appendices as the implementation specification:

- M6-001: inventory record table plus gapA 2.1 through 2.9, gapD D6.1 through D6.3, and gapF 1.2.
- M6-005: M6 0.10 / native `GetIDFromString` at 0x0099DB84 through 0x0099DC40.
- M6-018: MD1, gapB P3/P4, gapD D1.1 through D1.3, and gapE section 4.

Do not amend the frozen inventory to fit the current implementation. If a necessary behavior is not established there, stop and report it rather than supplying a plausible value.

## 2. Exact files and functions likely to change

### M6-001

- `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBank.cs`
  - `Parse`
  - `ReadHirc`
  - `EventActions`
  - `PlayActionTarget`
  - the Action/Event data model that replaces offset-only access where necessary
- `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseHierarchy.cs`
  - `TryRead`
  - `ReadNodeParams`
  - `ReadRtpc`
  - `ReadSound`
  - `ReadRandomSequence`
  - `ReadSwitch`
  - `ReadActorMixer`
  - `ReadBus`
  - add the frozen Layer reader rather than treating Layer as a music-only omission
  - extend `Reader` with the exact Wwise varint operation
- `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseBus.cs`
  - adjust bus fields/types only as required by the native field order
- `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSoundLibrary.cs`
  - load/expose STMG defaults and any bank-global structures required by the frozen record

Do not rewrite music scheduling or M9 node semantics. M6-001 owns the listed reader contracts, not an M9 re-audit.

### M6-005

- `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseHash.cs`
  - `Of`

Keep FNV-1 multiply-then-XOR and ASCII A-Z lowercasing. Add the recovered input bound exactly; do not replace it with culture-aware lowercasing or FNV-1a.

### M6-018

- Introduce one clear owner for the internal Wwise runtime format, likely a new file under `Animation/Wwise`.
- Audit only the places that currently conflate the internal Wwise domain with the robot endpoint:
  - `WwiseAudioSource` constructor and production-rate assumptions
  - `WwiseSongRenderer.Rate`
  - `WwiseBusChain.For` callers
- Do **not** change `CozmoAudio.SampleRate` or `CozmoAudio.SamplesPerFrame`. Those are M3's correct 22,320 Hz and 744-sample robot-output constants.
- Later batches will consume the new 48,000/1,024 settings. Batch 1 must establish the distinction without prematurely implementing resampling, mixing, or Hijack behavior.

## 3. Source-backed requirements

### M6-001

- Event action IDs are read in native bank order.
- Action common fields and action-type-specific fields must follow the native readers, including the data later needed for Play, Stop, and Seek.
- Sound source fields precede NodeBase exactly as frozen.
- Random/Sequence, Switch, ActorMixer, Bus, and Layer readers must follow the cited runtime order.
- `NodeBase` must implement the complete conditional field consumption, not merely the byte pattern used by shipped nodes.
- RTPC parameter ID is a Wwise varint. A one-byte read is only accidentally correct for current shipped IDs below 0x80.
- Source-plugin Sound parameters include both the parameter-block size and that many parameter bytes. Current shipped sizes being zero is not permission to omit the branch.
- Positioning branches, the BKHD feedback flag, and bus A/B conditional bits must be represented/read exactly even though current banks do not exercise all of them.
- STMG defaults at entry+8 must be parsed and exposed for Batch 2's RTPC value-store fallback.
- Every reader must reject truncation/overrun rather than silently realigning to a plausible object.

### M6-005

- Initial value `0x811C9DC5`.
- For each input byte: lowercase ASCII `A` through `Z`, multiply by `16777619`, then XOR the byte.
- Apply the recovered maximum of 0x103 bytes. Preserve the inventory's exact NUL/bound interpretation; do not infer Unicode or culture semantics beyond the recovered byte-oriented path.

### M6-018

- Internal M6 mix rate: 48,000 Hz.
- Internal M6 samples per engine frame: 1,024.
- These are a recorded compatibility policy for a stack with no phone, derived from the original's `min(native output rate, 48000)` and hardware-buffer rounding.
- Keep the internal settings separate from the robot endpoint: 22,320 Hz, 744 samples.
- Derived runtime calculations in later batches must originate from these internal settings rather than scattered literals or `CozmoAudio.SampleRate`.

## 4. Existing tests: valid, stale, and insufficient

### Valid regression evidence

- `WwiseTests.ABankParsesItsHeaderAndHierarchy` and `ABankWhoseLengthsDoNotAddUpIsRefused` validly protect the bank/chunk envelope and length rejection.
- `WwiseTests.EveryShippedBankParsesExactly` validly protects loading all six shipped v120 banks.
- `WwiseMusicTests.EveryHierarchyObjectInTheShippedBanksConsumesExactly` validly protects shipped-asset consumption and object counts.
- `WwiseHashTests.TheHashReproducesTheShippedBankIds`, `TheHashIgnoresCase`, `ItIsFnv1RatherThanFnv1a`, and `EveryShippedBankIdIsItsNameHashed` validly protect FNV-1 order, ASCII case behavior on known names, and shipped IDs.
- `DeviceTests.TheSampleRateIsTheEnginesAudioSampleRate` and the corresponding M3 tests validly protect the **robot endpoint** at 22,320 Hz/744 samples.

Keep these where their assertions remain in scope.

### Stale or source-insufficient for Batch 1 claims

- Synthetic Event/Action/Sound fixtures in `WwiseTests` encode the current simplified layouts. They may remain useful smoke tests, but they are not an oracle for native field order and must be updated or supplemented with source-shaped fixtures.
- “Every shipped object consumes exactly” is not proof of M6-001 source fidelity. The frozen inventory already established cases where the current parser consumes every shipped object but omits conditional native branches.
- Existing hash tests use short names. They do not exercise the 0x103-byte boundary.
- Tests that construct `WwiseBusChain` or render Wwise audio directly at `CozmoAudio.SampleRate` reflect the old whole-buffer architecture. They must not be cited as proof of M6-018's internal domain.
- Old acceptance and documentation claims that the Wwise runtime is unavailable are superseded by the frozen M6 inventory.

### Circularity assessment

No existing Batch 1 test was established as strictly circular: shipped bank IDs and asset bytes are independent data. However, synthetic fixtures copied from the current parser's assumed layout are self-consistent rather than source-proving. Do not use those fixtures alone to settle M6-001.

## 5. Focused tests that should exist

Add focused tests only for Batch 1 behavior:

### M6-001 reader tests

- Event and each shipped Action form consume a source-shaped payload and expose the native fields in bank order.
- RTPC parameter IDs exercise both a one-byte value and a multi-byte varint value above 0x7F.
- Positioning fixtures exercise each frozen conditional branch, including combinations not present in shipped banks.
- A source-plugin Sound with a non-zero parameter-block size consumes the block before NodeBase.
- BKHD feedback-flag branches consume/reject fields exactly as frozen.
- Bus A/B conditional-bit fixtures consume their conditional fields.
- Layer payloads parse in the frozen native order.
- STMG defaults are parsed and retrievable by parameter ID.
- Every conditional fixture has a truncated counterpart that fails rather than silently shifting subsequent fields.
- The existing whole-library exact-consumption tests still pass after the source-shaped readers replace shortcuts.

### M6-005 hash tests

- Exact FNV-1 known vectors remain unchanged.
- ASCII uppercase and lowercase produce the same ID.
- Inputs at, below, and above the recovered 0x103-byte boundary pin the exact truncation behavior.
- A vector distinguishes FNV-1 from FNV-1a.
- Do not add culture-sensitive or speculative non-ASCII expectations unless primary evidence establishes them.

### M6-018 settings tests

- Internal Wwise rate is exactly 48,000 and frame size exactly 1,024.
- Robot output remains exactly 22,320 and 744.
- The two domains are represented by different settings/types or otherwise cannot be accidentally substituted.
- Derived values that Batch 1 chooses to expose match the inventory table—for example `msPerFrame` and the 128-sample LPF chunk—without implementing later DSP.
- No Batch 1 test should require a phone or infer a different native rate.

## 6. Unresolved facts that must not be guessed

- The original phone's actual native output rate and hardware buffer size are device-dependent. MD1 already resolves this stack's policy as 48,000/1,024; do not reopen it or invent adaptive host behavior.
- Do not infer unrecorded Unicode, locale, embedded-NUL, or public-SDK hash behavior beyond the frozen byte-oriented evidence. If the 0x103-byte/NUL interpretation proves insufficient to implement deterministically, stop and report the exact ambiguity.
- Do not infer additional HIRC/music types from public Wwise documentation. Implement only the frozen M6-001 readers; M9 owns its later music inventory.
- Do not turn the inventory's bounded residuals into defaults. None is needed to complete Batch 1.
- Do not treat current shipped banks' zero/unset conditional fields as evidence that those native branches may be omitted.

## 7. Batch 1 completion criteria

Batch 1 is complete only when all of the following are true:

1. M6-001's listed readers and conditional branches match the frozen native field order, including RTPC varints, non-zero source-plugin parameter blocks, Bus, Layer, and STMG defaults.
2. The runtime-facing data model exposes all fields later batches require; later code will not need to reparse opaque payload offsets.
3. All six shipped banks and every supported shipped hierarchy object still parse and consume exactly.
4. Truncated and malformed conditional payloads fail closed.
5. `WwiseHash.Of` implements the frozen bounded lowercase FNV-1 behavior and boundary tests exist.
6. A single M6 settings owner establishes 48,000 Hz/1,024 samples without changing M3's 22,320 Hz/744-sample endpoint.
7. Tests distinguish source-shaped native layouts from synthetic fixtures that merely mirror the former implementation.
8. No event queue, codec, resampler, mixer, bus DSP, Hijack, animation integration, or M9 behavior is implemented as part of this batch.
9. Fidelity records and tags accurately describe the production path; no record is upgraded solely because shipped assets parse or tests pass.
10. The focused Batch 1 tests, fidelity checker, and the repository's required pre-commit suite pass before the implementation commit.

After reporting the Batch 1 production path, evidence, exact changes, tests, and remaining uncertainty, stop before Batch 2.
