# M6 Wwise inventory (bank reading, codecs, the Wwise runtime path to the robot's audio)

**State:** approved by the manager on 2026-09-25 under the operator's standing authorisation, and frozen with `python re-analysis/tools/fidelity.py --approve M6-wwise-bank`.
- The standing authorisation covers source-derived inventories and ordinary source-fidelity decisions.
- The one real choice left (the phone-dependent mix rate) is a recorded forced policy (MD1).

## The finding that reshapes this layer

**The Wwise 2016.2 sound engine is statically linked into `libcozmoEngine.so`**, as symbol-less ARM code at 0x0095E540..0x00AE2E40. That region includes its Vorbis and ADPCM decoders, resampler, mixer, RTPC manager, containers and the effect plug-ins.

The earlier repo position was that "Wwise runtime semantics" were BLOCKED_EXTERNAL. That position is contradicted: everything below is primary native source.

## Where this comes from

- **Source:** `libcozmoEngine.so` 3.4.0-1204, disassembled in ARM and Thumb modes. The shipped banks in `re-analysis/obb/` were parsed the way the runtime reads them. Unity C# was used as lower authority. Public Wwise names are labels only.
- **Read-only extractor passes** (Appendices A–H, in order):
  - **M6:** the runtime's presence, entry points and the engine's robot-audio path (A1..A25).
  - **gap A:** PostEvent to Play, HIRC readers, random/sequence selection, switch, RTPC curves.
  - **gap B:** the Vorbis decoder, ADPCM, resampler kernels, mix rate, bus FX order, Hijack registration, sink timing.
  - **gap C:** gain composition, aux sends, bus FX loop, the Parametric EQ and Peak Limiter, fades. Its addendum confirms the EQ and limiter registration.
  - **gap D:** timing globals, the pending-action drain, bus and Hijack lifetime, EndOfEvent, Stop/Seek, Bus and Layer readers.
    - Its D2.3 is **wrong**: the EQ and limiter are registered (see Corrections).
  - **gap E:** the voice LPF/HPF filter, mixer ramp and pan, pitch, frame globals, Anki callback dispatch, RTPC lookup.
  - **gap F:** RTPC store and STMG defaults (no voice goes silent), continuous containers, pan and channel config.
  - **gap G:** mode-4 chaining, PlayAndContinue delay, EndOfEvent for continuous plays, the source-start latch, stereo routing, Vorbis internals (a Tremor-lowmem fork).
- **Manager spot-check:** `.init_array` entries 0x103E560 → 0x4DEB18 and 0x103E564 → 0x4DEB8C are ARM constructors. Each prepends {next, type 3, company 0, id 0x6E / 0x69, create, params} to g_pAKPluginList. So the Peak Limiter and Parametric EQ are registered, which settles gap D against gap C.
- **The C# was never evidence.**

## How to read the statuses

- **IMPLEMENTATION_GAP:** established from source, to be compared and built.
- **COMPATIBILITY_POLICY:** a forced or deliberate choice.
- **EQUIVALENT_IMPLEMENTATION:** allowed only where the algorithm is read exactly but bit-identical output depends on the phone's libm or on NEON operation order (MD3).
- **No RECOVERABLE_GAP remains that changes shipped behaviour.** The bounded residuals are listed in their own section.

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M6-001 | IMPLEMENTATION_GAP | Bank and HIRC readers in the runtime's field order: <br>- **Nodes:** Event, Action (with per-type params), Sound (with source), RanSeq, Switch, ActorMixer, Bus, Layer order, NodeBase. <br>- **Other data:** RTPC entry with a varint param id, the STMG defaults. <br>- **Conditional branches the shipped banks never exercise:** 3D positioning, source-plugin params, the BKHD feedback flag, bus A/B bits. They are read exactly anyway. | gapA 2.1..2.9, gapD D6.1..D6.3, gapF 1.2 |
| M6-002 | IMPLEMENTATION_GAP | Vorbis decoding is **Wwise's own Tremor-lowmem fork**: <br>- **Setup:** the Wwise-stripped setup header, codebooks by id from the built-in library, and a 1-bit mode number. <br>- **Integer arithmetic:** residue and coupling (point −8), and dequantisation as Tremor decode_map type 1 with no q_seq. <br>- **Float arithmetic:** the Tremor floor table /2^15 multiplies the integer residue, then a float NEON IMDCT (×2^−24) and libvorbis float windows. <br>- **Output:** planar float, with start-skip and end-trim from the vorb header. <br>- **Deviations:** the type-0 residue path and the 2-channel-only type 2 are inert on every shipped file (mono is type 1, stereo is type 2, mode count is always 2). | gapB V1..V8, gapG 6.1..6.12 |
| M6-003 | IMPLEMENTATION_GAP | IMA ADPCM exactly: <br>- The block header predictor is **output sample 0**, followed by 63 nibbles; the high nibble of the last byte is unused. <br>- diff = ((2·(n&7)+1)·step)>>3, clamped to int16, index clamped 0..88. <br>- Stereo is [ch0 36 B][ch1 36 B], interleaved into int16, 64 frames per block. | M6 0.8, gapB D1..D2 |
| M6-004 | IMPLEMENTATION_GAP | Resampling is Wwise's CAkResampler. <br>- **Linear interpolation at both stages.** The voice stage converts source rate to mix rate: an int16 Q16 kernel, and bypass is ×1/32768. The Hijack stage converts mix rate to 22320 Hz, float mono. <br>- step = u32(ratio·powf(2, cents/1200)·65536 + 0.5). <br>- Pitch changes ramp over 0x400 phase units (mode 2). | M6 0.11, gapB P1..P2, P8, gapE 3.4..3.6 |
| M6-005 | IMPLEMENTATION_GAP | FNV-1 32-bit hash with ASCII lowercasing, over at most 0x103 bytes. | M6 0.10 |
| M6-006 | IMPLEMENTATION_GAP | The control path. <br>- PostEvent is **queued**, and the playing id is an atomic ++. <br>- ExecuteEvent walks the actions in bank order and skips object-scope actions when there is no game object. <br>- EnqueueOrExecute delays by frames, carrying the remainder in samples; PlayAndContinue runs one lookahead frame early. <br>- The pending list drains in launch order, FIFO for equal launch times. <br>- Play execute: the Probability prop (absent in the shipped banks), fade-in, initial delay, then PlayInternal. Stop and Seek execute as read. <br>- **Switch containers** resolve by state group or by the game object's switch, fall back to the default, and play every node in the list. | gapA 1.1..1.11, 4.1..4.2, gapD D1.4..D1.6, D5.1..D5.5, gapG 2.1..2.5 |
| M6-007 | IMPLEMENTATION_GAP | Random/sequence step selection exactly: <br>- **RNG:** a global 64-bit LCG seeded with time(NULL) at SoundEngine::Init. <br>- **State:** shared across game objects when bank bit4 is set, which is true for every shipped container. <br>- **Selection:** a single item needs no draw; otherwise the k-th eligible item. Shuffle uses played bits; the blocked list holds min(avoid, len−1) items; weights are used when any weight ≠ 50000. <br>- **Sequence:** step forward, with wrap or ping-pong. | gapA 3.1..3.9 |
| M6-008 | IMPLEMENTATION_GAP | Continuous containers. <br>- **Loop count:** loop 0 is infinite; loop 1 is a single pass. <br>- **Next item:** chosen at the current voice's start, with fresh state per play. <br>- **Mode 1** is a linear cross-fade and **mode 2** a sine/cosine cross-fade. For both, the next item starts xfade = min(transition, length/2) before the estimated end; below 50 ms it chains at the end. <br>- **Mode 4:** chained into the same voice as a pending source, first sample right after the last. <br>- **Mode 5:** a fixed-period trigger of max(transition, 22 ms), with no cross-fade. <br>- EndOfEvent fires in the last item's PBI Term. | gapD D4.1..D4.2, gapF 2.1..2.9, gapG 1.1..1.9, 3.1..3.5 |
| M6-009 | IMPLEMENTATION_GAP | RTPC. <br>- **Curve evaluation:** all interpolation shapes, with scaling applied **after** the curve. Scaling 2 is dB (±20·log10(1∓\|y\|)); 3 and 4 are fast pow. <br>- **Accumulation:** product when acc = 2, otherwise sum. <br>- **Value store precedence:** playing id, then game object, then root, then the STMG default at entry+8. So event_volume set per playing id applies to that voice, and other voices use the default 1.0 (0 dB). <br>- **Bus RTPCs** use the empty key, so robot_volume set on game object 7 does not reach the bus curve. <br>- Ramps are immediate for the Cozmo params. | gapA 5.1..5.6, gapE 7.1..7.4, gapF 1.1..1.10 |
| M6-010 | IMPLEMENTATION_GAP | Gain composition. <br>- **GetAudioParameters:** parent at +0x34 and output bus at +0x38. Volume, pitch, LPF and HPF are summed in dB/cents; the randomizer is drawn once per voice. <br>- **Conversion:** dBToLin is the fast pow (below −37·20 dB gives 0). The mute/fade product is applied on top. <br>- **Aux send:** game-defined aux is decided at the first deciding node. Send gain = dBToLin(GameAuxSendVolume) × the game object's send value. It does **not** include OutputBusVolume or the game object's output-bus volume. <br>- **Dry path:** muted, SetGameObjectOutputBusVolume 0.0. <br>- **Bus Volume** (param 5, robot_volume) is applied after that bus's FX and only on Cozmo_Robot, so **robot_volume never reaches the Hijack**. <br>- Collapsed buses fold into the dry path only. | gapC 1.1..1.11, 2.1..2.8, 3.2, gapE 1.1 |
| M6-011 | IMPLEMENTATION_GAP | Voice filter A (LPF/HPF) runs **before the aux sends**, so it shapes the robot signal. <br>- **Filter:** a 2nd-order Butterworth biquad in DF-I form. <br>- **Cutoff map:** v < 30 → 7000 + (30−v)·433.33; otherwise 16.797·fastpow2((100−v)·…). HPF uses the same map on (100−v). The result is capped at 0.45·rate. <br>- **Ramp:** 8 steps per N-sample chunk; bypassed when ≤ 0.1. <br>- Filter B (output-bus LPF/HPF) is dry-path only and bypassed for every shipped sound. <br>- Shipped: the top mixer has LPF 15 → 13.5 kHz. | gapE 1.1..1.9 |
| M6-012 | IMPLEMENTATION_GAP | Mixer. <br>- The per-connection gain ramps linearly from start to end over one bus frame. <br>- The first update is not ramped. <br>- Mono to mono is 1.0. **Stereo to mono is 0.70710677 per channel**; all 18 robot-routed stereo items take this. | gapE 2.1..2.7, gapF 3.1, gapG 5.1..5.3 |
| M6-013 | IMPLEMENTATION_GAP | The Robot_Bus_1..4 FX chain runs in slot order when the bus state is 1: EQ 0x6767FC1F → EQ 0x174901C6 → Peak Limiter 0xDF2230FF → Hijack. <br>- **EQ:** Butterworth LP/HP and RBJ peak/shelf/BP/notch coefficients; DF-I biquad; output-gain ramp, including its NEON-tail quirk. The ShareSet settings are in gapC 4.2. <br>- **Limiter:** threshold −1 dB, ratio 10.8, lookahead L = u32(float(sr)·0.009), release 0.041 s. Peak-hold detector, attack/release in dB, fast-pow gain, and an L-sample tail. | gapC 3.1, 4.1..4.9, gapC addendum, gapE §0 |
| M6-014 | IMPLEMENTATION_GAP | Bus and Hijack lifetime. <br>- **Creation:** a bus node is created on demand; its FX are instantiated lazily at its first GetResultingBuffer, so Hijack Init runs just before its first Execute. <br>- **Destruction:** after a frame with state ≠ 1, no connections and b0 clear, which means per contiguous voice activity. <br>- **Tail:** one frame with an empty input flushes a partial chunk, possibly 0 frames; UpdateBuffer accepts length 0. | gapD D2.1..D2.9 |
| M6-015 | IMPLEMENTATION_GAP | The Hijack plug-in. <br>- **Registration:** a static registration plus RegisterPlugin; the last caller wins, which is plug-in B's CozmoAudioController callbacks. <br>- **Init:** allocates 1024 floats per channel, then CAkResampler Init(fmt, 22320) and SetPitch(0), then the create callback → PrepareAudioBuffer. <br>- **Execute:** resample to 744-sample chunks → UpdateBuffer. <br>- **Term:** → CloseAudioBuffer. | M6 A13..A18, gapB R1..R5 |
| M6-016 | IMPLEMENTATION_GAP | The engine's robot-audio path (Anki side). <br>- **Draws:** InitAnimation draws every keyframe's alternative up front. <br>- **Posting:** BeginBuffering posts the events at wall-clock offsets (Dispatch::After) through PostCozmoEvent, with event_volume set per playing id. <br>- **Routing:** game objects 7..10 go to Robot_Bus_1..4 with an aux send of 1.0 and the dry path muted. <br>- **Callbacks:** queued (ctx+0x38 = 0) and drained at the end of CozmoEngine::Update. <br>- **States:** as read, with UpdateLoading and UpdateAudioFramesReady; PopRobotAudioMessage; abort. <br>- Play__Robot_VO__Nurture_Play_Concern_Short has use-game-aux 0, so **it sends nothing to the robot**. | M6 A1..A25, gapC 2.8, gapE 6.1..6.4 |
| M6-017 | IMPLEMENTATION_GAP | Audio-thread frame model. <br>- **Perform order:** messages, then drain, then render (buses, LEngine, PBI-notification flush), then tick++. <br>- **Driver:** the sink signals when there is room for a frame. <br>- **EndOfEvent** fires after the bus pass of the last voice's final frame, and before the next frame's partial flush. | gapD D1.6..D1.8, D3.1..D3.5 |
| M6-018 | IMPLEMENTATION_GAP (a forced policy, to build; COMPATIBILITY_POLICY once built) | Mix rate 48000 Hz and frame 1024 samples. <br>- In the original both depend on the phone: rate = min(native output rate, 48000), and the frame size is rounded to the hardware buffer. <br>- Everything derived uses these values: the ms→samples conversions, msPerFrame 21, the LPF chunk 128, the limiter L, and the Hijack resampling step. See MD1. | gapB P3..P4, gapD D1.1..D1.3, gapE §4 |

## Decisions (the manager's, recorded for audit)

- **MD1: mix rate and frame size (M6-018, a forced policy).**
  - This stack has no phone, so it uses 48000 Hz and 1024 samples. That is the cap, and the typical native rate.
  - It follows the decision note of 2026-09-24. Revisit it if a capture from an original phone ever shows another rate.
- **MD2: the RNG seed is reproduced, not a policy.**
  - The original seeds the Wwise LCG with time(NULL) at init. The stack does the same with the host clock.
  - Draws therefore match in distribution and algorithm, never in sequence, and that is also true of the original across runs.
- **MD3: bit-identical output where it depends on the phone.**
  - Some results depend on bionic libm (sinf, cosf, tanf, powf in EQ coefficients and cutoff maps), on NEON non-fused multiply-add ordering (the biquad block matrix, the mixer lanes), or on the float IMDCT.
  - Where the algorithm is read exactly and implemented in the same arithmetic order but .NET math cannot guarantee identical bits, the record becomes EQUIVALENT_IMPLEMENTATION.
  - Everything integer or table-driven is exact: ADPCM, the integer Vorbis stages, resampler phase, the LCG, the hash, and the container selection.
- **MD4: routing silence is the engine behaviour.** Nurture_Play_Concern_Short reaching the robot silent is what the engine does, and it is reproduced.
- **MD5: scope of the repair.** The existing stack Wwise code has these parts:
  - WwiseHierarchy, WwisePlayback, WwiseAudioSource, WwiseBus and WwiseBusChain;
  - WwiseVorbis and WwiseVorbisRebuilder;
  - WwiseAdpcm and WwiseHash.
  
  It is a bank reader plus an offline renderer. The runtime model above (queued events, frame-driven voices, sends, bus FX, Hijack lifetime) is substantially different.
  - **The repair may be split into two batches:** first the control path and signal path to the robot (M6-001, M6-003..M6-017), then the Vorbis decoder port (M6-002).
  - Each gets its own verify. That is the operator's rule about not expanding into multiple rounds, applied here because the comparison shows the layer is substantially different.

## Bounded residuals (read-only questions not settled; none changes shipped behaviour as far as the rows show)

- **Mode 4 fallbacks:** the voice's reaction to a chained source that fails the format check, or to +0x1D8 > 0 at the switch. Shipped mode-4 media always match, with delay 0.
- **Continuous containers with a container child:** PlayAndContinue when its target is itself a container (725225627 → 990835622).
- **Timing details:**
  - the start-notification drain relative to the tick;
  - PBI Term latency after the last sample;
  - whether the type-1 prefetch StartStream succeeds on its first call;
  - the writers of pbi+0x164;
  - exact libvorbis normalisation of the float IMDCT (the factors are read and give ±1.0).
- **What the stack does with these:** each must stay an explicit `unresolved` note on its record. The stack must not claim them, and it must not invent values where they would change output.

## Corrections to the extractor reports

- **gapD D2.3 and D2.8:** the EQ and limiter are registered (manager spot-check, gapC addendum, gapE §0).
- **gapD D1.4:** the 0x0503 PlayAndContinue special case applies to every shipped continuous container (gapG 2.2).
- **gapE 7.3 / Q3:** entry+8 is the STMG default. No voice goes silent (gapF 1.6).
- **gapF 2.4:** src+0x10 bit0 is the StartStream-success latch (gapG 4.1).

## Existing record evidence found too weak or contradicted

- **M6-001:** its field order matches the runtime only on the shipped data, with three conditional branches unread.
- **M6-002:** not NVorbis float. It is a Tremor-lowmem fork with 1 mode bit.
- **M6-003:** the header sample and the diff formula are wrong.
- **M6-004:** it is linear interpolation, not a windowed sinc. Its premise "the resampler does not ship" is false.
- **M6-005:** it lacked the lowercasing and truncation.
- **M6-006:** it was the stack's reading of bank bytes, not the runtime.
- **M6-007:** its "engine hands Wwise a generator" is false, and its avoid/shuffle rules were unverified.
- **M9 records:** those resting on "WwiseRtpc.Evaluate does not apply scaling" or "container semantics BLOCKED_EXTERNAL" are contradicted (gapA 5.3, 3.x). They are for M9's inventory.
- **M3 C17 / M1-042:** "robot_volume shapes the robot audio" is contradicted (gapC Q2). **The SetRobotVolume path still sends SetAudioVolume to the robot (M1 CD27), and that is what actually changes the robot's volume.**

## Appendix A: M6 pass, extractor report

All addresses are libcozmoEngine.so VAs. For .text and .rodata the VA equals the file offset. For .data the file offset is VA − 0x500.

#### 0. Is the Wwise runtime in the APK? YES. It is statically linked into libcozmoEngine.so with its symbols stripped.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| 0.1 | No shipped .so exports or imports AK:: symbols. libcozmoEngine.so exports only `g_pAKPluginList` (0x108D9F4), the Wwise static plug-in registration list. libunity/libmain/libmono have none. | lief dynsym scan of all 28 libs; `.dynsym` of libcozmoEngine | NEW | EXACT_SOURCE |
| 0.2 | The Wwise code is ARM-mode (not Thumb). It sits in the 1.5 MB unnamed region 0x0095E540..0x00AE2E40, has no .ARM.exidx entries, and is reached from Anki Thumb code through `bx pc` veneers at 0xAE2E40.. | gap analysis of dynsym; veneer 0xAE3060 → 0x0099F130 | NEW | EXACT_SOURCE |
| 0.3 | **Version: Wwise 2016.2.** HijackFx::GetPluginInfo writes uBuildVersion = 0x7E002 = (2016<<8)\|2. The bank reader accepts only bank version 0x78 = 120, else error 0x40. All six shipped banks are v120, and SoundbanksInfo.xml says SoundbankVersion="120". The build/subminor number is UNKNOWN. | 0x008DBE44 `movw r2,#0xe002; movt r2,#7`; 0x009B2300 `cmp r3,#0x78; moveq r0,#1; movne r0,#0x40` | NEW | EXACT_SOURCE |
| 0.4 | Other runtime evidence: the OpenSL sink ("AkSink, OpenSL Event %s on %s:" at 0x00FA7CD8, the SL_OBJECT_EVENT_* strings); the TLSF allocator; RoomVerb preset names; the convolution-reverb warning; the static PluginInfo.xml plug-ins; `libOpenSLES.so` in DT_NEEDED. | .rodata 0x00FA7994..0x00FA7F98 | NEW | EXACT_SOURCE |
| 0.5 | Entry points identified. Each is named by its call site and behaviour, not by a symbol. <br>- SoundEngine::Init 0x0099E3EC <br>- GetDefaultInitSettings 0x0099DC68 (uNumSamplesPerFrame default 0x400 at +0x20) <br>- GetDefaultPlatformInitSettings 0x0099DCE8→0x00A570A4 <br>- MusicEngine::Init 0x0097D72C <br>- MemoryMgr::Init 0x00A7AC68, CreatePool 0x00A7AC98, StreamMgr create 0x009604FC <br>- RenderAudio 0x0099F130 (from ProcessAudioQueue, 0x008D88C6) <br>- **PostEvent 0x009A6704** <br>- StopAll 0x009A6064 <br>- SetRTPCValue 0x0099FA24, SetRTPCValueByPlayingID 0x0099FB14, SetSwitch 0x0099FE1C, SetState 0x0099FFE0 | Anki wrappers 0x008D80D8..0x008D8206, 0x008D8D32, 0x008D904E, 0x008D90AA, 0x008D90F6, 0x008D9114, 0x008D912E | NEW | EXACT_SOURCE (the call sites); the internals are RECOVERABLE_GAP |
| 0.6 | **Bank loading.** <br>- ProcessBankHeader 0x009B21F4: reads 8 bytes, requires 'BKHD' (0x009B2240), reads 0x14 bytes, applies an optional XOR with a global key (0x009B2268..0x009B22BC), then the version check. <br>- The chunk dispatch compares INIT/STMG/DIDX/PLAT/ENVS/HIRC/STID/DATA (0x009B7788..0x009B7998). <br>- **HIRC object reader**: 5-byte header {u8 type, u32 size} (0x009B3364..0x009B3380) and a jump table on type−1 (0x009B338C). Handlers: <br>&nbsp;&nbsp;1 State 0x009B3E98, 2 Sound 0x009B3DF4, 3 Action 0x009B3DD0, 4 Event 0x009B3DAC, <br>&nbsp;&nbsp;5 RanSeqCntr 0x009B3CDC, 6 SwitchCntr 0x009B3C10, 7 ActorMixer 0x009B3B00, 8 Bus 0x009B3ADC, <br>&nbsp;&nbsp;9 LayerCntr 0x009B39FC, 10–13 (music types) default 0x009B3EBC, 14 Attenuation 0x009B392C, <br>&nbsp;&nbsp;15 DialogueEvent 0x009B385C, 16/17 0x009B3824, 18 FxShareSet 0x009B376C, 19 FxCustom 0x009B36A4, <br>&nbsp;&nbsp;20 AuxBus 0x009B3664, 21 0x009B3568, 22 0x009B34A8, 23 0x009B33F0. <br>The type-name mapping is inferred from the standard numbering and is consistent with the bank. The handler bodies are not read. | as cited | M6-001 | dispatch EXACT_SOURCE; per-type field readers RECOVERABLE_GAP |
| 0.7 | **Vorbis codebooks are built into the runtime.** <br>- ww2ogg's packed_codebooks_aoTuV_603.bin data blob (71,991 bytes, all 598 books) is byte-identical in .rodata at 0x010053E8..0x01016D1F. <br>- A 599-entry pointer table (598 books plus an end pointer) sits at .data 0x01058290 (file 0x1057D90), reached through GOT slot 0x0104026C. <br>- The decoder that uses it was not located (no pc-relative, GOT−0x20 or mvn access found). | byte compare in scratch; relocations at 0x01058290 | M6-002 | table EXACT_SOURCE; decoder RECOVERABLE_GAP |
| 0.8 | **IMA ADPCM decoder at 0x00A7A194.** Callers are 0x00A72618, 0x00A73EA0 and 0x00A74100. Step table i16 at 0x00FFD650, index table at 0x00FFD708 (= step table + 0xB8). Per 36-byte channel block: <br>- the i16 predictor (+0) is **written as output sample 0** (0x00A7A208 `strh r2,[r0],sb`); <br>- step index at +2; <br>- bytes +4..+0x22 each give two samples, low nibble first (0x00A7A214..0x00A7A324); <br>- byte +0x23 gives **its low nibble only** (0x00A7A334..0x00A7A3B4); <br>- so 64 samples per block = header sample + 63 nibbles; <br>- diff = ((2·(n&7)+1)·step)>>3, sign from bit 3 via negate (0x00A7A220..0x00A7A250), clamp to int16 (0x00A7A260..0x00A7A27C), index clamped 0..88 (0x00A7A2A0..0x00A7A2B0). <br>The output stride is an argument ([sp+0x48]·2), and the channel loop advances the input by an argument per channel (0x00A7A3B8). | as cited | M6-003 | EXACT_SOURCE for the per-block arithmetic; the channel ↔ half mapping is RECOVERABLE_GAP (read the three callers) |
| 0.9 | **RNG (AKRANDOM):** 64-bit LCG seed = seed·6364136223846793005 + 1; the result is (seed_hi >> 1). The seed global is 0x0108D868 (.bss). Seeding: SetSeed 0x0099DB58 uses time(NULL) when its argument is 0 (0x0099DB5C..0x0099DB78). It is called with 0 from 0x009B0210 (an init routine, caller not identified) and with a message value from 0x009AE400 (a SetRandomSeed-style API message). Container code inlines the same LCG, e.g. 0x0098A780..0x0098A7BC followed by `% n` (uidivmod). | 0x009A6C68..0x009A6CA8; as cited | M6-007 | EXACT_SOURCE for the generator and seeding; whether Anki ever calls SetRandomSeed is RECOVERABLE_GAP |
| 0.10 | **FNV-1 GetIDFromString 0x0099DB84.** It copies at most 0x103 bytes including the NUL, lowercases A–Z, then h = 0x811C9DC5; per byte h = h·16777619 (shift-add form), then h ^= c. Called by Anki at 0x008D661E, 0x008D91EA and 0x008D9DFA. | 0x0099DB9C..0x0099DC40 | M6-005 | EXACT_SOURCE (adds lowercasing and truncation) |
| 0.11 | **Resampler (Wwise's own CAkResampler), used by the Hijack plug-in:** <br>- ctor 0x00A46D70; Init(fmt, outRate) 0x00A47038: ratio +0x4C = inRate/outRate (float), +0x3C = 48000/outRate; <br>- SetPitch 0x00A47384: step = (u32)(ratio·2^(cents/1200)·65536 + 0.5), constants 1200.0/65536.0 at 0x00A47520/0x00A47524; <br>- Execute 0x00A47178 dispatches through kernel table 0x0103C0B8 [format/channels + mode·8]; <br>- the mono-float interpolating kernel 0x00A49E40 is **linear interpolation** out = prev + (phase&0xFFFF)/65536·(next−prev) (0x00A49EF0..0x00A49F14, 0x00A49F84..0x00A49FB0), with the last sample carried between buffers (+0x20); <br>- it returns 43 (DataNeeded) or 45 (DataReady) when the output reaches +0x40 frames. | as cited | M6-004 | EXACT_SOURCE |

**Size and feasibility.** Wwise plus its DSP plug-ins take most of the 1.5 MB ARM region (thousands of symbol-less functions). A wholesale port is not feasible.

Small, self-contained, portable internals (well under 1 KB of code each, except the resampler set):
- the ADPCM block decoder;
- the RNG and its seeding;
- the FNV hash;
- CAkResampler: Init, SetPitch, Execute and the kernels actually used.

Moderate (several KB each, read per behaviour):
- random/sequence and switch container selection;
- the HIRC per-type readers (the dispatch table is known);
- the RTPC curve evaluation.

Large (tens of KB and threaded):
- the Vorbis decoder (the codebook table is verbatim);
- the voice pipeline, mixer and bus-FX lifetime;
- the timing of the audio thread and sink;
- the EQ and peak-limiter DSP (also present as plug-ins).

#### A. Production path: audio keyframe → 744 float samples

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| A1 | RobotAudioAnimation ctor: state +0x3C = 0 (Preparing). States: 0 Preparing, 1 LoadingStream, 2 LoadingStreamFrames, 3 AudioFramesReady, 4 AnimationCompleted, 5 AnimationError. | 0x0059620C..0x00596272; names at 0x00596694..0x005966FC, table built at 0x00596460..0x005964E4 | M3 C16 (names were RECOVERABLE) | EXACT_SOURCE |
| A2 | **InitAnimation draws every audio keyframe's alternative up front.** For each RobotAudio keyframe: GetAudioRefIndex(true); if ≥0, GetAudioRef, and if eventId≠0 it pushes a 0x14-byte AnimationEvent {u16 idx(1-based), eventId, keyframe trigger time (kf+0xC), volume (ref+4), state 0}. If ref+0xC is nonzero it sets +0x3D. It then **advances the RobotAudio track to its end** (MoveToNextKeyFrame for each). | 0x0059687E..0x00596914 | M5 C14 interface; NEW | EXACT_SOURCE (what +0x3D is used for is UNKNOWN) |
| A3 | No events → state 4. Otherwise: game object 6 → the OnDevice path (0x00596DC8); else buffer = client vfunc+0x20(gameObj); null → state 5 and error. Then Dispatch::Create(queue, priority 2) goes to +0x18, and vfunc+0x14 (PrepareAnimation) runs. | 0x00596916..0x00596986 | NEW | EXACT_SOURCE |
| A4 | OnRobot PrepareAnimation/Update(state 0): if the buffer is not IsWaitingForReset, BeginBufferingAudioOnRobotMode runs. Update: state 1/2 → UpdateLoading; 3 → UpdateAudioFramesReady; returns the state. | 0x00597EE4..0x00597EFC; 0x00597C64..0x00597C9C | M3 C16 | EXACT_SOURCE |
| A5 | **BeginBuffering:** state 1. For each event: Dispatch::After(queue, delay = event.time − firstEvent.time ms, lambda{this, event, weak_ptr}). So the first event is posted at once on the dispatch thread and the rest at **wall-clock** offsets. | 0x00597F12..0x00597F8E | NEW | EXACT_SOURCE |
| A6 | **Lambda:** if the weak pointer is alive it logs, then under mutex +0x30 sets event.state = 1 and +0x44++. It calls PostCozmoEvent(eventId, gameObj +0x34, callback). If playingId≠0 it calls SetCozmoEventParameter(playingId, RTPC 0xD2687048 "event_volume", event.volume). Then ProcessEvents. | 0x0059818C..0x005982A0; "event_volume" Cozmo.txt:576 | NEW | EXACT_SOURCE |
| A7 | PostCozmoEvent builds an AudioCallbackContext and calls AudioEngineController::PostAudioEvent directly (synchronously, no multiplexer). The wrapper calls **PostEvent(event, gameObj, flags = 1\|(ctx&2)<<1\|(ctx&1)<<3 → EndOfEvent always, Marker/Duration optional, callback 0x008D8D41, cookie ctx)**. | 0x00599E50..0x00599EF0; 0x008D1F46; 0x008D8CE4..0x008D8D32 | NEW | EXACT_SOURCE |
| A8 | PostEvent returns 0 → PostAudioEvent immediately delivers an AudioErrorCallbackInfo (type 4). Nonzero → the playing id goes to ctx+4. | 0x008D1F4A..0x008D1FB6 | NEW | EXACT_SOURCE |
| A9 | HandleCozmoEventCallback: type 3 (Complete, from AK EndOfEvent) → event.state = 2; type 4 (Error) → log and state = 3. Either way +0x48 (callbacks received)++. | 0x00596A94..0x00596B1C | NEW | EXACT_SOURCE |
| A10 | ProcessEvents = RenderAudio(true) (0x0099F130). On Android the samples are rendered by the Wwise audio thread driven by the OpenSL sink. Timing is real time and phone-paced. | 0x00599FE2→0x008D2946→0x008D88C0..0x008D88C6 | NEW | call EXACT_SOURCE; audio-thread scheduling RECOVERABLE_GAP |
| A11 | **Routing.** RobotAudioClient ctor registers {gameObj 7 → plugin 1 → Robot_Bus_1 0x9FA5953C}, {8→2→Bus_2}, {9→3→Bus_3}, {10→4→Bus_4 0x9FA59539}, {6→0→none}. For each: SetGameObjectAuxSendValues(gameObj, [{bus, 1.0}]) and **SetGameObjectOutputBusVolume(gameObj, 0.0)** (dry path muted), then CozmoAudioController::RegisterRobotAudioBuffer(gameObj, pluginId), which maps pluginId → RobotAudioBuffer (map +0x78). | 0x0059962A..0x0059966A; 0x00599952..0x005999A4; bus ids Init.txt:125-128 | NEW | EXACT_SOURCE |
| A12 | **Wwise mix rate.** Anki's SetupConfig does not set the platform sample rate: it sets pools 2 MB/1 MB/1.5 MB, 30 pools and the JavaVM/activity (0x008D8158..0x008D81AC, cfg filled 0x00592C9E..0x00592DDA, 0x005933A2..0x005933C2). The platform default at +0x38 is 0 (0x00A571B0), presumably uSampleRate; the SDK header is not shipped. The sink caches **min(AudioTrack.getNativeOutputSampleRate(3), 48000)** into 0x0108DF90 (0x00A56E80..0x00A56EA4). | as cited | NEW | the link from +0x38==0 to the cached rate is RECOVERABLE_GAP; the actual rate is phone-dependent: **HARDWARE_ONLY** (original device) |
| A13 | Robot_Bus_1 effect chain → the Anki Hijack effect (plugin 0x000112C3, param = robot index). HijackFx vtable 0x010387F4: Init 0x008DBD75, Term 0x008DBDF9, Reset 0x008DBE2F, GetPluginInfo 0x008DBE3B (effect, in-place, synchronous), Execute 0x008DBE55, TimeSkip 0x008DBF41. | RTTI 0x0103881C; vtable dump | NEW | EXACT_SOURCE for the Hijack. The bus's effect order, and whether bus volume/RTPC apply before the effect, is Wwise library semantics: RECOVERABLE_GAP |
| A14 | SetupEnginePlugInFx stores 22320 → fx+0xC and 744 → fx+0x10, and binds the create/process/destroy lambdas. | 0x008DB47C..0x008DB4EA | M3 C3 | EXACT_SOURCE |
| A15 | **Hijack Init:** stores the params. Core init: numChannels = fmt.channelConfig byte (0 → fail 2). It allocates numChannels·4096 bytes (1024 floats per channel), then **CAkResampler::Init(fmt, 22320)** and **SetPitch(0 cents)** (0x008DBF76..0x008DBFB6), then fires the create callback → CozmoAudioController lambda → map lookup(pluginId) → **RobotAudioBuffer::PrepareAudioBuffer**: pushes a new RobotAudioFrameStream stamped with GetCurrentTimeInMilliseconds (wall clock). | 0x008DBD74..0x008DBDA6; 0x00595E78..0x00595ED6; 0x005984E8..0x0059853A | NEW | EXACT_SOURCE |
| A16 | **Hijack Execute (the source of the floats):** loop { resampler.Execute(in, out, max 744 frames) with +0x40 = 744; if the result is 45 (DataReady) or 17 (NoMoreData): process callback(fx, outBuf, validFrames), then reset valid = 0 } while in.uValidFrames≠0. The bus audio is passed through unmodified. **The resampling from the mix rate to 22320 Hz is linear interpolation (A.0.11), with no anti-alias filter.** | 0x008DBFE8..0x008DC03C; 0x00A47178; 0x00A49E40 | M6-004 | EXACT_SOURCE (only the mono-float kernel was read; the bus channel count, which selects the kernel, is RECOVERABLE_GAP from Robot_Bus_1 in the bank) |
| A17 | Process lambda → RobotAudioBuffer::UpdateBuffer(float*, n). If waiting-for-reset (+0x1C) it logs and drops. If the back stream is active it copies n floats into a new vector and pushes it into the back stream. Otherwise it gives the VERIFY "Audio being delivered with no active Audio Streams". | 0x00596148..0x00596168; 0x005985FC..0x005986BA | NEW | EXACT_SOURCE |
| A18 | Hijack Term → destroy callback → CloseAudioBuffer: marks the back stream complete (+8 = 1) and clears +0x1C. | 0x008DBDF8..0x008DBE0A; 0x00595FE0..0x0059603A; 0x0059878C..0x005987FE | NEW | EXACT_SOURCE. When Wwise instantiates or terminates bus FX (bus activity lifetime) is RECOVERABLE_GAP |
| A19 | **UpdateLoading:** <br>- no stream: if IsAnimationDone (callbacks ≥ events and no stream) → state 4. <br>- stream, no data: if complete → PopAudioBufferStream and state 1. <br>- data and not started: when nextEvent.time ≤ (streamTime − start) → started = 1, +0x50 = streamCreated_ms − event.time, state 3. <br>- started: if the event is due, or elapsed ≥ floor(streamCreated − +0x50) → state 3; else if complete → state 1. | 0x00597C9E..0x00597D7E; IsAnimationDone 0x00596B9C | M3 C16 | EXACT_SOURCE |
| A20 | UpdateAudioFramesReady: no data → state 2 (not ready, so the animation stalls). Otherwise it skips (+0x40++) events whose state is 3 (error). | 0x00597D82..0x00597DB2 | M3 C16 | EXACT_SOURCE |
| A21 | PopRobotAudioMessage (state 3 only): PopNextAudioFrameData (front stream). Each float → encodeMuLaw; a short frame is zero-padded to 744. It then advances +0x40 past events with time < elapsed. | 0x00597DB4..0x00597E8E | M3 C5 | EXACT_SOURCE |
| A22 | **No audio from an event:** Wwise never activates Robot_Bus_1, so there is no Hijack instance and no stream. Frames go out as AudioSilence while the state is 1 (1 counts as ready). Once the EndOfEvent or Error callbacks arrive → state 4. | A19, A9, M3 C16 | NEW | EXACT_SOURCE for the engine side |
| A23 | **Abort:** Dispatch::Stop; FlushAudioCallbackQueue; ResetAudioBufferAnimationCompleted (sets waiting-for-reset until the next Close); StopCozmoEvent = StopAll(gameObj) + ProcessEvents; state 4. | 0x0059678E..0x005967B8 | M5-023 interface | EXACT_SOURCE |
| A24 | Robot volume: SetRobotVolume posts RTPC 0x637C1240 "robot_volume" on gameObj 7 (6 if not on-robot). The Wwise RTPC dispatch is SetRTPCValue 0x0099FA24 (curve arg mapped via table 0x00DC9480). Its effect on the samples comes from the bank's RTPC curves plus the RTPC runtime. | 0x00599F62..0x00599F80; 0x008D9070..0x008D90AA | M3 C17 | call EXACT_SOURCE; RTPC semantics RECOVERABLE_GAP (**not** BLOCKED_EXTERNAL) |
| A25 | CozmoAudioController::SetupPlugins also constructs a second HijackAudioPlugIn(22320, 744) whose pointer is not stored, and attaches the three lambdas to it. Each Set*Callback re-runs RegisterPlugin. | 0x005942DA..0x00594354; 0x008DB368..0x008DB3FC | NEW | EXACT_SOURCE (which registration wins is RECOVERABLE_GAP; RegisterPlugin → 0x008DB84C not read) |

#### Existing records contradicted by the source
- **"Wwise runtime semantics are BLOCKED_EXTERNAL" (repo notes, M9, M3 C17's RTPC note):** the runtime is in libcozmoEngine.so (0.1–0.5). Everything it covers is primary source that has not been read yet: RECOVERABLE_GAP, not BLOCKED_EXTERNAL.
- **M6-004:** its authority says the resampler "does not ship in the APK", and it calls a windowed sinc EQUIVALENT. Both are contradicted: the Hijack stage uses Wwise's CAkResampler with 16.16 linear interpolation (0x00A47038, 0x00A47384, 0x00A49E40). The voice-stage resampler (source rate to mix rate) is also in the binary, not read.
- **M6-003:** the runtime decoder (0x00A7A194) emits the header predictor as sample 0 and decodes 63 nibbles; the last byte's high nibble is unused. Its diff is ((2n+1)·step)>>3, not the IMA shift sum. The record's layout arithmetic ("(36−4)·2 = 64 samples") and "the bytes cannot say" are wrong on both counts. For example, step 7, nibble 7 gives 13 in Wwise and 11 in shift-sum IMA.
- **M6-007:** its authority says "the engine hands Wwise a generator (RobotAudioClient::GetRandomGenerator)". That generator only draws the Anki keyframe alternatives (A2, M5 C14). Wwise containers use their own 64-bit LCG, seeded with time(NULL) (0.9). The draws are nondeterministic by construction.
- **M6-002:** "no runtime in the package to check against" is contradicted. The codebook library is byte-identical in .rodata (0.7), and the runtime's own Vorbis decoder is present but not located.

#### Existing records whose evidence is too weak for their status
- **M6-001 (EXACT_SOURCE):** it rests on "the bytes consume exactly". The runtime's per-type HIRC readers (0.6) are the authority for field semantics and were not compared.
- **M6-006 (EXACT_SOURCE):** its container semantics (play mode, continuous, avoid-repeat, weights) are the stack's reading of bank bytes. The runtime's container logic (e.g. 0x0098A6D4) was not read.
- **M6-007 (EXACT_SOURCE):** the "draws" behaviour is unverified against the runtime selection algorithm and RNG.
- **M6-002 / M6-004 as EQUIVALENT_IMPLEMENTATION:** each record's premise that no primary source exists is false, so their classification needs a decision (see Q1).
- **M6-005:** it holds, but it is missing the lowercasing and the 258-character truncation (0.10).

#### Open questions for the manager
1. Every Wwise-runtime item is now RECOVERABLE_GAP. Decide which parts the stack ports from the binary (ADPCM, RNG, resampler and the container selection are small) and which stay as declared policy (Vorbis decoder, mixer, bus DSP, audio-thread timing).
2. **The mix rate is phone-dependent** in the original: min(native, 48000) (A12). This stack has no phone, so the input rate to the Hijack's linear 22320 Hz resampler is a genuine policy choice (COMPATIBILITY_POLICY), probably for the operator.
3. Robot_Bus_1's channel count, which selects the resampler kernel, and whether UpdateBuffer takes only channel 0 of a planar stereo buffer: read from Init.bnk's bus object plus the AkAudioBuffer layout.
4. Timing: in the original, audio frames arrive on the Wwise audio thread in real time and the event posts use wall-clock Dispatch::After. The stack needs a decision on how to model that. It is source-established but not deterministic.

#### NOT DONE (all RECOVERABLE_GAP, in the binary)
- Inside the Wwise runtime, PostEvent → CAkEvent → Action → Play is not traced.
- The random/sequence and switch container algorithms are not read. The RNG is known.
- Not read: switch/state resolution, RTPC curve evaluation, mixer and volume application order, bus FX instantiation lifetime, the voice-stage resampler kernels (int16 and other channel counts), locating the Vorbis decoder, the ADPCM callers (channel ↔ half), the per-type HIRC field readers, RegisterPlugin (0x008DB84C), and the consumer of platform +0x38.

## Appendix B: gap A (control path, containers, RTPC), extractor report

Scratch folder: `...\scratchpad\extract\M6-gapA\`. It holds the tools (arm.py, blx.py for ARM b/bl xrefs, tbl.py for Thumb→ARM blx xrefs, jt.py for jump tables, vt.py, bnk.py and nodes.py for bank parsers using the layout the runtime reads, bus.py) and the disassembly dumps (hirc, actfac, msgq, rs1-4, step, conv, rtpcmgr, pnode, nbp, swinit).

- All addresses are libcozmoEngine.so VAs.
- The last column says whether a row is **L** (library code the stack must port) or **D** (bank data).
- Structure names come from public Wwise usage and are labels only. Every behaviour below is cited to the binary.

#### 1. Control path: PostEvent → event → actions → Play → target node

| step | what the original does | citation | record | class | L/D |
|---|---|---|---|---|---|
| 1.1 | **PostEvent 0x9A6704** has two branches: <br>- With external sources it goes through 0x9A65C0 and releases them on failure. <br>- Otherwise it goes straight to the core **0x9A0EF8**. | 0x9A6740, 0x9A6778, 0x9A67C0 | NEW | EXACT_SOURCE | L |
| 1.2 | **Core PostEvent** proceeds in order: <br>- It looks up the event ID in the event index hash (bucket = id % count). If the ID is not found it **returns 0 (AK_INVALID_PLAYING_ID)**. <br>- It takes a reference on the event and reserves a queue message of **type 1**. The message holds: +0x28 event pointer, +0x30 event ID, +0x14..+0x24 custom params (zeroed when absent), **+0xC playing ID = an atomic ++ of a global counter**, +4 game object, +0x10 an argument. <br>- It registers the playing ID with the callback manager (0xA03108: flags, callback, cookie, event ID). If registration fails it releases the event, turns the message into a no-op (type 0x38) and returns 0. <br>- **Nothing executes on the caller's thread: the event is queued.** | 0x9A0F2C..0x9A0F98, 0x9A0F9C..0x9A0FB4, 0x9A0FE4..0x9A101C, 0x9A103C..0x9A1064 | NEW | EXACT_SOURCE | L |
| 1.3 | **Message pump.** The dispatcher jump table at **0x9AE0B0** covers message types 0..0x37. <br>- Type 1 → 0x9AF244: the game object is resolved by 0xA0C238(msg+4), then **ExecuteEvent 0x9AA3DC**(event, gameObj, playingID, msg+0x10, &custom). <br>- Then 0xA04F54(playingID) decrements the callback manager's play count, which drives the EndOfEvent path. Its internals are not read. <br>- Types 2 and 3 are SetRTPC without and with interpolation (see §5). Type 0x32 is SetRandomSeed (§3). | 0x9AE0A8..0x9AE194; 0x9AF244..0x9AF2B0 | NEW | EXACT_SOURCE (0xA0C238 and 0xA04F54 internals RECOVERABLE_GAP) | L |
| 1.4 | **ExecuteEvent** walks the event's action list (event+0x10, then action+0x10 in bank order). For each action it allocates a 0x38-byte queued action {action, gameObj, playingID +0x28, +0x30, custom params}. <br>- **If (actionType & 1) is set (object scope, e.g. 0x0403, 0x0103, 0x1901) and there is no game object, the action is skipped.** <br>- If the bit is clear (global scope, e.g. 0x0102, 0x1204), the action runs with gameObj = NULL. <br>- Each queued action goes to EnqueueOrExecute 0x9AA0FC. | 0x9AA3E4..0x9AA560 | NEW | EXACT_SOURCE | L |
| 1.5 | **EnqueueOrExecute:** <br>- Increments the playing ID's play count (0xA04EDC) and takes a reference on the action. <br>- **delay = GetDelay(action) 0xA61260**, in samples. <br>- **frames = delay / frameSize** (u16 global, via GOT 0xFFFFFDCC), with the **sub-frame remainder kept at +0xC**. <br>- launch tick = mgr+0x4C + frames. <br>- **frames == 0 → execute now** (vfunc +0x24). <br>- Otherwise the action goes into the pending list sorted by launch tick; a Play action also gets vfunc +0x30 (0xA62978, zeroes an out value). | 0x9AA11C..0x9AA2B8, 0x9AA18C..0x9AA24C | NEW | EXACT_SOURCE (the pending-list drain was not traced) | L |
| 1.6 | **GetDelay 0xA61260:** delay = prop 0x0F (else a table default [g+0x3C]) + ranged 0x0F min + (int)(0.5 + rand/2147483647 · (max−min)), drawing the global LCG. <br>At load time, **prop 0x0F and both ranged bounds are converted from ms to samples** as v·rate/1000 with 64-bit division; rate is the global at GOT 0xFFFFFDDC. | 0xA61284..0xA6138C; load-time conversion 0xA61564..0xA61670 | NEW | EXACT_SOURCE (the rate global's value/source is RECOVERABLE_GAP: find its writer) | L |
| 1.7 | **Action object layout (all types), from SetInitialValues 0xA613B0:** <br>- u32 id, u16 type, **u32 target id**, **u8 isBus** (→ vfunc +0x18: +0x1C / +0x22 bit6) <br>- property bundle: u8 n, n×u8 id, n×u32 value <br>- ranged bundle: u8 m, m×u8 id, m×(min, max) <br>- then **type-specific params via vfunc +0x28** <br>The factory **0xA60C1C** switches on (type & 0xFF00). | 0xA613B8..0xA61480; factory 0xA60C1C..0xA60DE8 | NEW | EXACT_SOURCE | L |
| 1.8 | **Action types in the shipped banks and their factories:** <br>- 0x01 Stop 0xA6651C (seen as 0x0102/0x0103/0x0108) <br>- 0x02 Pause 0xA62910 (0x0202) <br>- 0x03 Resume 0xA643AC (0x0302) <br>- **0x04 Play 0xA62E54 (0x0403, 721 objects)** <br>- 0x12 SetState 0xA65844 (0x1204) <br>- 0x13 SetGameParameter 0xA656B4 (0x1303) <br>- 0x19 SetSwitch 0xA659DC (0x1901) <br>- 0x1E Seek 0xA64500 (0x1E03) <br>- 0x21 0xA63EB0 (0x2103) <br>**Cozmo.bnk contains only 0x0102, 0x0103, 0x0403 and 0x1E03.** <br>Low byte: bit0 = object scope (1.4). The other low-byte bits were not read. | factory cases; counts by bnk.py | NEW | EXACT_SOURCE (the enumeration is D; the per-type execute bodies other than Play are RECOVERABLE_GAP) | D/L |
| 1.9 | **Play params (vfunc +0x28 = 0xA62984):** u8 → fade curve (+0x22 bits 0..4); u32 → **bank ID** (+0x24); sets +0x22 bit5. All 721 shipped Play actions have exactly 5 trailing bytes. <br>**Stop params:** u8 fade curve (0xA79E30), a type-specific vfunc (0xA663C8, which consumes 0 bytes in every shipped Stop), then an exception list: u32 count × {u32 id, u8 isBus} (0xA622A4). <br>**Seek:** u8 relative, f32 value, f32 min, f32 max, u8 snap, then exceptions (0xA64460). | 0xA62984..0xA629C0; 0xA79E30; 0xA622A4..0xA62340; 0xA64460..0xA644B8 | NEW | EXACT_SOURCE (0xA663C8's own reads UNKNOWN) | L |
| 1.10 | **Play execute (vfunc +0x24 = 0xA62D38):** <br>- If prop **0x11 Probability** is present: 0.0 → skip; otherwise draw r = (LCG_hi>>1)/2147483647·100 and **skip when r > prob**. <br>- If absent → 0xA62A1C. <br>**No shipped action carries prop 0x11**, so the draw never happens. | 0xA62D38..0xA62E08; constants 0xA62E10 (2147483647.0), 0xA62E18 (100.0) | NEW | EXACT_SOURCE | L |
| 1.11 | **0xA62A1C:** <br>- Target = GetNodePtr(action+0x1C, isBus), via 0xA6168C → 0x9A7EB0. If it is null → **error 0x0F**. <br>- It builds transition params: **fade-in time = prop 0x10 (ms, not converted) + ranged min + random range** (0xA61110, same LCG), with curve = +0x22 & 0x1F. <br>- It calls **0x9F12E0** (initial delay: prop 0x3B InitialDelay + RTPC(0xA11590) + random range; ≤0 → play now), then the node's **PlayInternal = vfunc +0x128**. <br>Shipped data: in Cozmo.bnk, Play actions have no prop 0x0F (delay) and 6 have prop 0x10 (100..1500 ms fade-in). SFX.bnk has 11 Plays with a delay. | 0xA62A2C..0xA62D14; 0xA61110..0xA61244; 0x9F12E0..0x9F14EC | NEW | call chain EXACT_SOURCE; how the fade-in is applied, and 0x9EE454/0xA616BC, are RECOVERABLE_GAP | L/D |

#### 2. HIRC per-type readers (dispatch 0x9B338C) compared with WwiseHierarchy.cs

| step | what the runtime reads, in order | citation | record | class |
|---|---|---|---|---|
| 2.1 | **Dispatch.** Each handler looks up an existing object by ID, creates it if absent (Create), then calls SetInitialValues: <br>- Sound: Create 0xA1D814, SetInitialValues 0xA1DA08 <br>- Action: factory 0xA60C1C, 0xA613B0 <br>- Event: 0x9CC96C, 0x9CD01C <br>- RanSeq: Create 0xA07114(id, 1), 0xA0828C <br>- Switch: 0xA2D9A8, 0xA2F1D0 <br>- ActorMixer: 0xA66950, 0xA669EC <br>- Layer: 0xA67130, 0xA67558 <br>- Bus: 0x9B2CE8 (not read) <br>**Types in the banks:** Init 1/8/18/19; SFX 2/3/4/7/9/18/19; UI 2/3/4/7/19; Music 3/4/10-13; Dev_Debug 2/3/4/7/19; Cozmo 2-7/9-13/18/19/21/22. **No type 14 (Attenuation) exists in any bank.** | 0x9B3B00..0x9B3DA8, 0x9B3DF4..0x9B3E64, 0x9B3DAC/DD0 | M6-001 | EXACT_SOURCE |
| 2.2 | **Event:** u32 id, **u32 action count**, u32 × action IDs. Each action must already be loaded (else error 2; ID 0 → error 0xE). The actions are chained through action+0x10 in that order. | 0x9CD024..0x9CD120 | M6-001 (Event not covered) | EXACT_SOURCE |
| 2.3 | **NodeBaseParams, common reader 0x9F6EF8:** <br>- FX params (vfunc +0x108) <br>- u8 overrideAttachment (+0x45 b5) <br>- **u32 overrideBus** (≠0 → bus.AddChild) <br>- **u32 directParent** (≠0 → parent.AddChild) <br>- **u8 bits:** b0 → +0x40 b0, b1 → +0x45 b7, b2..b5 → +0x47 b1..b4 <br>- initial params (vfunc +0x104) <br>- positioning (+0x10C) <br>- aux (+0x110) <br>- advanced settings (+0x114) <br>- **u32 state-group count** × {u32 group, u8 sync (+0x28), u16 n × {u32 state, u32 instance}} <br>- **u16 RTPC count** × RTPC (2.5) <br>- then 4 more bytes **only if** header flag +0x64 is set. That flag = BKHD dword 3 ≠ 0 (0x9B2224..0x9B2230). It is **0 in all six banks**. | 0x9F6F18..0x9F7348 | M6-001 | EXACT_SOURCE for the order; the MIDI names of bits 2..5 are labels only |
| 2.4 | **Sub-readers:** <br>- **Positioning 0x9ECF44:** u8 bits. **More bytes follow only if b0 and b3 are both set** (u8, u32 attenuation, …). Shipped bytes are 0xC0 (2877 nodes) and 0xC3 (9 nodes), so one byte each. <br>- **Advanced 0x9ED730:** u8, u8 (&7), u16 (10-bit max instances), u8, u8 = 6 bytes. <br>- **Aux 0x9ED84C:** u8 bits; b3 → aux list. <br>- **Sound source 0x9B9C90:** u32 plugin, u8 stream, u32 source ID, u32 memory size, u8 bits (b0/b1/b3 stored); **if plugin&0xF is 2 or 5: u32 size + size bytes of plugin params**. | as cited | M6-001 | EXACT_SOURCE (the 3D branch and the aux-list length are not fully read) |
| 2.5 | **RTPC entry:** u32 RTPC ID, u8 type, u8 accumulate, **param ID as a varint** (7 bits per byte, bit 0x80 = continue, big-endian accumulation), u32 curve ID, u8 scaling, u16 point count, count × {f32 x, f32 y, u32 interp}. Handed to vfunc +0xF0. | 0x9F7254..0x9F72EC | M6-001 | EXACT_SOURCE |
| 2.6 | **RanSeq 0xA0828C** (after NodeBase): <br>- u16 loop, u16 loopMin, u16 loopMax (**loop 0 forces min/max to 0**) <br>- f32 transition time, f32 min, f32 max <br>- **u16 avoidRepeat** (+0x8E) <br>- u8 transitionMode (+0x90 b0..3) <br>- **u8 randomMode** (+0x90 b4..5) <br>- **u8 mode** (+0x91 b0..2; **1 = sequence** creates playlist 0xA701BC, otherwise random 0xA70194) <br>- u8 bits: **b1→+0x91 b4, b2→b5, b3→b6, b4→b7; b0 is never read** <br>- u32 child count + IDs (AddChild) <br>- **u16 playlist count × {u32 id, s32 weight}**. **Any weight ≠ 50000 sets "using weights" (+0x91 b3).** In random mode a duplicate ID is error 0x23. | 0xA082CC..0xA08438, 0xA07F30, 0xA080CC..0xA0819C | M6-001 | EXACT_SOURCE |
| 2.7 | **Switch 0xA2F1D0:** <br>- u8 groupType (+0x84), u32 group (+0x88), u32 default (+0x8C), u8 continuous validation (+0x47 b5) <br>- u32 child count (+IDs) <br>- u32 switch count × {u32 switch ID, u32 n, n × node ID} <br>- u32 param count × {u32 node, **u8 bits (b0, b1)**, u8 mode (&7), s32 fadeOut, s32 fadeIn} | 0xA2F220..0xA2F4EC, 0xA2F334..0xA2F378 | M6-001 | EXACT_SOURCE |
| 2.8 | **ActorMixer 0xA669EC:** NodeBase, then u32 child count + IDs. **Sound 0xA1DA08:** source (2.4), then NodeBase. | as cited | M6-001 | EXACT_SOURCE |
| 2.9 | **Comparison with WwiseHierarchy.cs:** <br>- Field order matches the runtime for Sound, RanSeq, Switch, ActorMixer, the NodeBase blocks, the 6-byte advanced block and the RTPC entry. <br>- **Equivalent only on the shipped data:** <br>&nbsp;&nbsp;(a) ReadRtpc reads the param ID as one byte (l.338); the runtime reads a varint, so they agree only because every shipped ID is < 0x80. <br>&nbsp;&nbsp;(b) Positioning is read as one byte (l.312-313); the runtime reads more when b0 and b3 are set, which no shipped node has. <br>&nbsp;&nbsp;(c) A source-plugin Sound skips only the u32 size (l.385); the runtime also skips *size* bytes, and all 43 shipped sizes are 0. <br>&nbsp;&nbsp;(d) The BKHD feedback flag is not modelled; it is 0 in all banks. <br>- **Not read:** the Bus reader 0x9B2CE8 (the stack's bus layout rests on bank consumption only), the Layer reader 0xA67558, and the music types (M9). | WwiseHierarchy.cs:287-343, 381-388 | M6-001 | M6-001 holds for these types only as "matches the runtime order, with conditional branches unexercised by the shipped banks" |

#### 3. Random/sequence selection and the RNG

| step | what the original does | citation | record | class | L/D |
|---|---|---|---|---|---|
| 3.1 | **RNG:** a single global 64-bit state (.bss 0x108D868). s = s·0x5851F42D4C957F2D + 1; output = (u32)(s>>32)>>1. The same inline code is used by delay/fade randomization, Probability, the initial delay, node randomizers and the containers. There are 65 inlined sites. | 0xA08A7C..0xA08AC0; list of movw #0xF42D sites (jt scan) | M6-007 | EXACT_SOURCE | L |
| 3.2 | **Seeding, the M6 open item resolved:** SetSeed 0x99DB58(0) → **time(NULL)**. It is called at **0x9B0210** inside 0x9B0200, which is called from **SoundEngine::Init at 0x99EF80** after the audio manager is created (0x99EF54..0x99EF7C). <br>Message 0x32 → SetSeed(msg u32, hi = 0) (0x9AE3F8..0x9AE400). It is posted by the API **SetRandomSeed 0x9A6310** (0x9A6310..0x9A6330). **Anki calls it only when SetupConfig+0x74 ≠ 0** (0x8D813E..0x8D8146). **Cozmo's SetupConfig ctor writes +0x74 = 0** (0x592CC8 `strh.w r4,[sp,#0x20c]`, r4 = 0). No other write happens before the single InitializeAudioEngine call (0x5933CC, its only caller). **So the seed is time(NULL) at init.** | as cited | M6-007 | EXACT_SOURCE | L |
| 3.3 | **PlayInternal 0xA0AFDC:** +0x91 b6 (bank bit3) **clear → step path 0xA0A6FC**. Set → continuous: transitionMode 5 → 0xA09F04, otherwise 0xA0ABC4. | 0xA0AFDC..0xA0B0E8 | M6-006 | EXACT_SOURCE (continuous bodies RECOVERABLE_GAP) | L |
| 3.4 | **Where the state lives (0xA09698 random, 0xA099BC sequence):** <br>- **+0x91 b7 (bank bit4) set → one state shared by all game objects** (+0x78). <br>- Otherwise a sorted per-game-object map (+0x6C/+0x70/+0x74), created on first play. <br>- Random state: ctor 0xA06994(len) sets remaining = counter = len, total = remaining weight = 50000·len; then Init 0xA069DC(avoidRepeat) allocates **"played" and "blocked" bitsets** (+0x1C, +0x20) and the avoid list. With weights in use, total = remaining = playlist total (vfunc +0x34). <br>- Sequence state: 0xA06BB4 sets forward = 1, index = −1. <br>**Shipped:** 437/468 RanSeq have bits 0x12 and 31 have 0x1A. **All have bank bit4 set, so container state is global, shared across robot game objects 7-10.** | 0xA096A0..0xA098E8; 0xA099BC..0xA09A08; 0xA06994..0xA06A7C | M6-007 | EXACT_SOURCE | L/D |
| 3.5 | **SelectPlayable 0xA0A3B4 (step path):** <br>- Playlist length 0 → none. <br>- **Length 1 → item 0 with no RNG draw.** <br>- Sequence → 3.7. Random → SelectRandomly 0xA08A44. <br>- The chosen ID → node (0x9A7EB0). If node vfunc +0x48 says it is not playable, it retries: shuffle and standard-with-avoid do a linear probe (idx+1 mod len, skipping played/blocked, then 0xA08694), up to len tries. <br>80 of 468 Cozmo containers have one item. | 0xA0A3E4..0xA0A6E0 | M6-006 | EXACT_SOURCE | L |
| 3.6 | **SelectRandomly 0xA08A44**, with counter +0xE, remaining items +0xC, remaining weight +8, avoid list +0x10/+0x14. <br>**(a) Counter == 0 → reset:** counter = len; clear the played bits; remaining = len. **Shuffle** (randomMode ≠ 0): remaining weight = total − Σ weight(avoid list). **Both modes:** remaining −= avoid count. <br>&nbsp;&nbsp;With a loop-info argument (continuous only), the loop count is decremented and the play ends at 0 (b0 = enabled, b1 = infinite). The step path passes none, so it always resets. <br>**(b) Unweighted:** k = (LCG>>1) % remaining. Walk the indices counting *eligible* ones and pick the **k-th eligible index (0-based)**. <br>&nbsp;&nbsp;Eligible = not blocked (standard with avoid > 0), anything (standard with avoid 0), or **not played and not blocked** (shuffle). <br>**(c) Weighted** (+0x91 b3): r = (LCG>>1) % remaining weight (unsigned, 0xA06B5C). Pick the first eligible index whose running weight sum exceeds r. <br>**(d) After the pick:** <br>&nbsp;&nbsp;- *Standard:* if not played → set played, counter−−. Then 0xA08694: when avoid > 0, remaining−−, append to the avoid list, set blocked, remaining weight −= w. When the list is longer than **min(avoid, len−1)**, the oldest entry is unblocked and its weight and item are restored. <br>&nbsp;&nbsp;- *Shuffle:* remaining−−, counter−−, remaining weight −= w, set played, append and block. The limit is min(max(avoid, 1), len−1); popping the oldest unblocks it and, if it is not played, restores its item and weight. | 0xA08A44..0xA09124; helpers 0xA06A84 (set played), 0xA06AC4 (played?), 0xA06B04/24/44 (block/unblock/blocked?), 0xA06ADC (reset); 0xA08694..0xA087DC | M6-007 | EXACT_SOURCE | L |
| 3.7 | **Sequence step:** <br>- Forward: idx+1. At len it either reverses when +0x91 b5 (bank bit2) is set (idx−1, direction = backward), or wraps to 0. <br>- Backward: idx−1; at 0 it turns forward and plays 1. <br>- The first play gives 0. | 0xA0A524..0xA0A554, 0xA0A614..0xA0A6E8 | M6-006 | EXACT_SOURCE | L |
| 3.8 | **Bank bit1 (+0x91 b4)** is consulted only on the continuous paths (0xA09C98, 0xA09E38, 0xA0ADC0), where it decides whether the selection state is saved back (0xA095B4). **The step path ignores it.** | as cited | NEW | EXACT_SOURCE (effect detail RECOVERABLE_GAP) | L |
| 3.9 | **Shipped Cozmo.bnk RanSeq values:** <br>- mode 0 random: 453; mode 1 sequence: 15 <br>- randomMode 0 standard: 44; 1 shuffle: 424 <br>- avoidRepeat 1..15 <br>- **weights are used in one container only** (50000/80000) <br>- step 437, continuous 31 (transition modes 1..5; loop 0/1/3) | nodes.py | M6-006 | EXACT_SOURCE | D |

#### 4. Switch containers and state/switch resolution

| step | what the original does | citation | record | class | L/D |
|---|---|---|---|---|---|
| 4.1 | **PlayInternal 0xA2C730:** <br>- Current value = 0xA2BD58(group, key{gameObj, playingID…}). **groupType 1 → state manager 0xA28198:** a global scan (group → current state), unregistered group → 0. **Otherwise → switch manager 0xA347A8:** per group, then per game-object key. <br>- The value's node list is looked up in +0x90. **If there is no match, the default switch (+0x8C) list is used.** If neither exists → 0xA2D054. <br>- **Every node in the list is played.** An empty list returns 1 and plays nothing. <br>- The last switch is recorded per object (0xA0CCF8/0xA0CD78). | 0xA2C738..0xA2C8E4; 0xA2BD58..0xA2BD8C; 0xA28198..0xA281EC | NEW | EXACT_SOURCE for the selection; the switch manager's not-found return (0xA34A84/0xA34B10) and 0xA2D054 are RECOVERABLE_GAP | L |
| 4.2 | Shipped: 21 switch containers, all in Cozmo.bnk. 16 are state-driven (type 1) and 5 switch-driven. All have a nonzero default, and none use continuous validation. | nodes.py | NEW | EXACT_SOURCE | D |

#### 5. RTPC: curves, application, event_volume and robot_volume

| step | what the original does | citation | record | class | L/D |
|---|---|---|---|---|---|
| 5.1 | **SetRTPCValue 0x99FA24**(id, value, gameObj, timeMs, curve, bypass): timeMs == 0 and bypass == 0 → message type 2 (immediate); otherwise type 3 (+0x18 time, +0x1C curve, +0x20 bypass). **ByPlayingID 0x99FB14** resolves the playing ID's game object through 0xA05044 (unknown ID → error 0x1F) and stores the playing ID at +0x14. <br>The type-2 handler calls **0xA1404C**, and from there 0xA13A88, keyed by {gameObj, playingID}. | 0x99FA24..0x99FB08; 0x99FB14..0x99FBF0; 0x9AF1D0..0x9AF23C | NEW | EXACT_SOURCE (the 0xA13A88 value store and key precedence, 0xA17280, are RECOVERABLE_GAP) | L |
| 5.2 | **Anki's arguments:** <br>- PostRobotParameter (robot_volume) → SetParameter with **time 0, curve 0**. <br>- SetCozmoEventParameter (event_volume) → SetParameterWithPlayingId with **time 0, curve 0**. <br>- The wrapper maps Anki curve 1..8 → Wwise [5,3,1,7,6,8,2,0]; anything else → 4. <br>**Both are immediate type-2 messages.** event_volume is posted after PostEvent returns, so it sits behind the event's type-1 message in the same queue. | 0x599F62..0x599F80; 0x599FB2..0x599FC6; 0x8D9070..0x8D90AA, 0x8D90BC..0x8D90F6; table 0xDC9480 | NEW | EXACT_SOURCE (FIFO order of the queue is RECOVERABLE_GAP) | L |
| 5.3 | **Curve evaluation 0xA14E28**(table{pts, n, scaling}, x, hint, &outIdx). The search starts at *hint* (0 at the evaluation sites 0xA1784C and 0xA179A0): <br>- x ≤ pts[i].x → y_i <br>- past the last point → y_last <br>- otherwise segment i with t = (x−x0)/(x1−x0) and interp = pts[i].interp: <br>&nbsp;&nbsp;**4 linear**; **9 constant = y0**; <br>&nbsp;&nbsp;**0 Log3:** y0+(1−(1−t)³)Δ; <br>&nbsp;&nbsp;**1 Sine:** y0+sin(t·π/2)Δ, as the polynomial x·(0.9999966+x²(x²(0.0083063−0.00018364x²)−0.16664828)); <br>&nbsp;&nbsp;**2 Log1:** y0+t(3−t)/2·Δ; <br>&nbsp;&nbsp;**3 InvSCurve:** t≤½: y0+sin(πt)/2·Δ; else y0+(1−sin(π−πt)/2)Δ (polynomial 0.4999983, 0.08332414, 0.0041531627, −9.1818e−5); <br>&nbsp;&nbsp;**5 SCurve:** y0+P((πt)²)Δ, with P = 0.0006967+u(0.2476748+u(−0.0196138+0.00048483u)); <br>&nbsp;&nbsp;**6 Exp1:** y0+t(t+1)/2·Δ; <br>&nbsp;&nbsp;**7 SineRecip:** y1+cos(πt/2)(y0−y1), with the cos polynomial at 0xA152B0..BC; <br>&nbsp;&nbsp;**8 Exp3:** y0+t³Δ. <br>**Then scaling is applied to the result:** <br>- **2 (dB):** y ≥ 0 → **−20·log10(1−y)**; y < 0 → **20·log10(1+y)**; \|y\| > 1 clamps to ±764.616. The log is a fast log2: exponent and mantissa with s = (m−1)/(m+1), (e−127)·ln2 + 2s(1+s²/3), times log10(e). <br>- **3:** 10^y by fast pow (27866352 = 2²³·log2 10, plus the mantissa polynomial 0.6530434+m(0.0208058+0.3251898m)); y < −37 → 0. <br>- **4:** 10^(0.05y), with 0.05y < −37 → 0. <br>- **0:** unchanged. | 0xA14E28..0xA15244; constants 0xA15248..0xA152C0 | NEW | EXACT_SOURCE | L |
| 5.4 | **Accumulation:** <br>- A subscription stores accumulate = the bank byte (+0x28), target type (+0x24; 2 for a node) and the curve list. The value per curve is the RTPC value for the key (0xA17280); if there is none, the default table entry; if none, 0. <br>- **acc == 2 → product** of curve outputs (start 1.0, 0xA17724). **Otherwise → sum** (start 0.0, 0xA17878). <br>- For plug-in targets the result is combined with the base: acc 1 → base + v, acc 2 → base · v, otherwise v. <br>- Node targets (type 2) are only notified (vfunc +4, 0xA1177C) and pull the value later through **0xA11590** (the same acc==2 → multiply, else add). <br>**All shipped RTPC entries have acc = 1.** | 0xA119B4..0xA119EC; 0xA17724..0xA17874; 0xA17878..0xA179C8; 0xA11590..0xA11620; 0xA11624..0xA117C4 | NEW | EXACT_SOURCE (that the default table is filled from STMG is RECOVERABLE_GAP) | L |
| 5.5 | **event_volume 0xD2687048** (bank data): on actor-mixers **62050212 and 682998829 (Cozmo.bnk)** and 121198006 (Dev_Debug), all routed to bus Cozmo_Robot 1723505802. <br>Param **0 (Volume)**, acc 1, **scaling 2**, points **(0, −1.0, Sine) → (1, 0.0)**. <br>Runtime result: **Volume_dB = 20·log10(sin(v·π/2))** (fast approximations). So v = 1 → 0 dB, v = 0.5 → −3.01 dB, v = 0 → −764.6 dB. <br>The STMG default reads 1.0 (Init.bnk offset 0x4AD, bank read only). The top mixer 62050212 also carries Volume −2.0 dB and prop 3 = 15.0. | nodes.py; Init.bnk STMG | M3 C17 / NEW | EXACT_SOURCE (evaluation and data) | D+L |
| 5.6 | **robot_volume 0x637C1240** (bank data): **on no node. It is only on bus Cozmo_Robot 1723505802.** Param **5**, acc 1, **scaling 0**, points **(0, −200, Exp1) → (1, 0)**. <br>Curve result: **−200 + 200·v(v+1)/2 dB**, so v = 1 → 0 dB, v = 0.5 → −125 dB. STMG default 1.0. <br>**The Robot_Bus_1..4 aux buses are children of Master Audio Bus, not of Cozmo_Robot** (Init.bnk parents; Init.txt:122-128). | bus.py; Init.txt | M3 C17 | curve EXACT_SOURCE; **its effect on robot audio is UNKNOWN**, see Q2 | D |

#### 6. Volume and pitch through the hierarchy (partial)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 6.1 | **GetAudioParameters 0x9EF258** (vfunc +0xAC) adds this node's contributions into an output struct: +0 Volume, +8 Pitch, +0xC LPF, +0x10 HPF, … <br>- the state bundle (node+0x24) <br>- the randomizer ranged props (node+0x4C), each drawing the LCG <br>- the base props (node+0x3C) <br>- RTPC through 0xA11590 <br>It then **calls itself recursively on node+0x34 and node+0x38** (the two links set by the NodeBase parent/bus AddChild) and runs the modulator list (+0x50 → 0xA6E848). | 0x9EF2D8..0x9EF52C, 0x9EF57C..0x9EF968, 0x9EFFD4..0x9F00B4 | NEW | **Structure only.** Which link is the parent and which the bus, the exact per-parameter composition, and where the Volume sum becomes gain are RECOVERABLE_GAP (read 0x9EF998..0x9EFFD0 and the AddChild targets) |
| 6.2 | Bus **Voice Volume vs Bus Volume (prop 5)**, and whether a bus's volume and RTPC reach **game-defined aux sends**. | — | NEW | UNKNOWN: mixer and bus pipeline not read |

#### Existing records contradicted by the source

- **WwiseRtpc.Evaluate (WwiseHierarchy.cs:49-78; M9-007/M9-009 rest on it).** "The curve's scaling byte is not applied … interpolating in decibels" is contradicted.
  - For scaling 2 the stored y is in a normalized domain and is converted by ±20·log10(1∓|y|) (0xA14F88..0xA15038).
  - Interp types other than 4 and 9 have exact formulas (5.3), not linear.
  - Example: cozmo_singing_note_off 0x16BEDBEA (points (0,0)→(1,−1), scaling 2) fades to −764.6 dB (silence). It does not "move the level by at most one decibel" (M9-007).
- **M9-022 (BLOCKED_EXTERNAL, container semantics).** The runtime is present. Step-mode random/sequence selection is fully read (§3). It is not "random by weight avoiding the last": it is the k-th eligible item with a blocked list of min(avoid, len−1) entries, shuffle through played bits, and global state for every shipped container.
- **M6-007.** The seed caller is now identified (3.2). The shipped containers share global, time-seeded state (3.4).
- **M6 report 0.9, "caller not identified"** → identified: SoundEngine::Init at 0x99EF80.

#### Existing records whose evidence is too weak for their status

- **M6-001 (EXACT_SOURCE):** the order is confirmed, but three reads (2.9 a-c) are equivalent only on the shipped data. The Bus, Layer and music readers were not compared.
- **M6-006 / M6-007 (EXACT_SOURCE):** they should be re-checked against rows 3.4-3.7 (global state, eligibility counting, the avoid-list limit, the single-item case with no draw, the sequence reverse bit). Continuous playback (31 Cozmo containers) is not read.
- **M3 C17 robot volume:** its effect depends on 6.2 and Q2.

#### Open questions for the manager

1. The RNG is seeded with time(NULL), so draws can only match in distribution, never in sequence. Porting the LCG and the selection algorithm is possible; reproducing the seed is not. This needs a policy decision.
2. **robot_volume** drives Bus Volume on Cozmo_Robot. The robot audio leaves through a game-defined aux send (gain 1.0) to Robot_Bus_N, which sits outside Cozmo_Robot, and the dry output is muted. Whether robot_volume changes what the Hijack receives depends on the unread bus/aux mixer (6.2). This is RECOVERABLE_GAP: read the voice-to-aux send gain computation and the bus volume application. **Do not assume either answer.**
3. The rate global behind the action-delay ms→samples conversion (GOT 0xFFFFFDDC) and the frame size (GOT 0xFFFFFDCC). Both tie back to the M6 A12 mix-rate question.

#### NOT DONE (all RECOVERABLE_GAP, in the binary)

- Pending-action drain and the frame timing of delayed actions
- Fade-in application
- Execute bodies of Stop, Seek and SetState
- 0xA0C238 game-object lookup
- 0xA04F54 / EndOfEvent internals
- Continuous RanSeq (0xA0ABC4, 0xA09F04) and loop/transition behaviour
- Switch-manager not-found paths
- RTPC value-store key precedence (0xA17280, 0xA13A88) and the STMG default reader
- The Bus reader 0x9B2CE8 and the Layer reader 0xA67558
- The full composition in GetAudioParameters and the mixer/bus/aux-send gain path (§6)
- The randomizer's application to pitch (Cozmo has 12 pitch ranges)

## Appendix C: gap B (codecs, signal path), extractor report

All addresses are libcozmoEngine.so VAs. Scratch tools are in `...\scratchpad\extract\M6-gapB\`: `wwise_arm.txt` (annotated ARM dump of 0x95E540..0xAE2E40), `cg.pkl`/`q.py`/`up.py` (ARM call graph), `tx.py` (Thumb data xrefs), `rtpcscan.py`.

The GOT base used by the Wwise code is 0x0104028C, and many table loads go through `[base, #-off]`. That is why M6 could not find the codebook-table user.

#### 1. Vorbis decoder (Wwise's own, statically linked)

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| V1 | **Setup cache.** Decoded setups are kept in a hash keyed by source hdr+0x78, with a refcount (+0x18). A miss allocates 0x1C bytes plus a setup arena of hdr+0x70 bytes, then runs the block-size check and the setup parse. The table rehashes when load exceeds 0.9. | 0x00AB2D74..0x00AB2F58 (0x3F666666 at 0x00AB3110) | M6-002 | EXACT_SOURCE |
| V2 | **Block sizes.** 1<<hdr+0x7C and 1<<hdr+0x7D. Error −0x85 if bs0 < 64, bs0 > bs1 or bs1 > 8192. | 0x00AB6380..0x00AB63DC | M6-002 | EXACT_SOURCE |
| V3 | **Setup header (Wwise-stripped), bits LSB-first** (reader 0x00AB62E0, mask table 0x01005360). <br>- codebook count = read(8)+1; each = read(10) id → pointer table 0x01058290 (GOT 0x0104026C, loaded at 0x00AB6474) → unpack 0x00ABA188; <br>- **no time-domain section**; <br>- floor count = read(6)+1, each 0x24 bytes, **floor type not read** (floor1 parser 0x00AB88D8); <br>- residue count = read(6)+1, 0x1C each (0x00AB6F34); <br>- mapping count = read(6)+1, 0x14 each (0x00AB6788; channel count = hdr+0x48); <br>- mode count = read(6)+1, each {blockflag read(1), mapping read(8)}, error if mapping ≥ count; no window/transform fields. | 0x00AB63E0..0x00AB6780 | M6-002 | EXACT_SOURCE for the layout (per-type field readers only skimmed) |
| V4 | **Packed codebook.** dims read(4), entries read(14), ordered read(1) (then 5-bit runs), codeword-length-length read(3), sparse read(1); lookup type read(1), then min read(32), delta read(32), value bits read(4), sequence read(1). This matches ww2ogg's packed format. | 0x00ABA1B0..0x00ABA4F4 | M6-002 | EXACT_SOURCE (bit widths); dequantisation not read |
| V5 | **Packet framing.** Each audio packet is a u16 size then its data; there is no granule field. The last packet is flagged when the end of data is reached and flag +0x54 is set. Results: 0x2E NoMoreData, 0x2D DataReady, 0x11 when the last flush yields nothing. | 0x00AB7E40..0x00AB8010 | M6-002 | EXACT_SOURCE |
| V6 | **Audio packet.** <br>- The mode number is read with a **hard-coded 1 bit** (`mov r1,#1; bl 0xAB62E0`). <br>- There is no packet-type bit and no prev/next window bits. <br>- blockflag and mapping come from modes[m·2], [m·2+1]; lW is kept at +0x24, W at +0x28. <br>- u16 +0x2C / +0x2E handle start-skip and end-trim. | 0x00AB37FC..0x00AB3934 | M6-002 | EXACT_SOURCE for the reads; which vorb header fields fill +0x2C/+0x2E is RECOVERABLE_GAP |
| V7 | **Decode math is a hybrid, not stock libvorbis.** <br>- Residue and inverse coupling are **integer** (Tremor-style), done with int ops on the vectors (0x00AB6E30..0x00AB6E68). <br>- floor1 inverse2 (render_line with the `hy==(hy&0x7fff)` test and idiv) multiplies `(float)int_residue × table[y]`. <br>- The table at 0x01058BF0 (GOT 0x01040270) is **Tremor's integer FLOOR_fromdB table / 2^15** as float: [0]=229/32768, [255]=65536.0. <br>- It is **not** libvorbis's float table (1.0649863e-07…1.0). Relative differences reach about 0.1–0.2% at low indices. <br>- The inverse MDCT is **float NEON** with Tremor's control flow (`shift=13−log2 n`, presymmetry 0x00AB3D28, butterflies 0x00AB3FCC, entry 0x00AB4E34). Its float trig tables are at GOT −0x58..−0x28. <br>- Windows are **libvorbis float vwin tables** at 0x01054490 (vwin256 starts 5.9139e-05). | 0x00AB915C..0x00AB92C8; 0x00AB4E78..0x00AB4ECC; 0x00AB3588..0x00AB3744 | M6-002 | EXACT_SOURCE for the arithmetic types and tables; overall output scale vs ±1.0 is RECOVERABLE_GAP |
| V8 | **Output.** Windowed overlap-add (0x00AB5A94, float vmul/vadd) writes **planar float**: channel c at out + c·maxFrames. Channel index r3 is moved to the last slot (LFE reorder); for mono and stereo it is effectively identity. | 0x00AB3520..0x00AB36BC | M6-002 | EXACT_SOURCE |

**How this compares with WwiseVorbisRebuilder (M6-002).** The rebuilder writes ilog(modeCount−1) mode bits and decodes with a float, libvorbis-style decoder (NVorbis).
- The runtime reads exactly 1 bit, so the two agree only when modeCount == 2.
- Its floor table and integer residue path differ from libvorbis, so output samples are close but **not bit-identical**.
- The difference is not fixed-point output. It is a Tremor-derived floor table and integer residue feeding a float MDCT.

#### 2. IMA ADPCM callers

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| D1 | Decoder signature: (in, int16* out, nBlocks, inStride = blockAlign, [sp] = channels as the output stride). | 0x00A7A194..0x00A7A3C4 | M6-003 | EXACT_SOURCE |
| D2 | **Channel mapping.** For channel c: in = block + c·0x24, out = buf + c·2, stride = channels. So each block is [ch0 36 B][ch1 36 B] and the output is **interleaved int16**, 64 frames per block. The frame count is rounded down to a multiple of 64 (`lsr #6`). The buffer is channels × u16 global 0x01052440 frames. | 0x00A725F8..0x00A72624 (bank); 0x00A73E88..0x00A73EAC (stream); 0x00A740E0..0x00A7410C (one block from reassembly +0x68) | M6-003 | EXACT_SOURCE |

#### 3. Voice and bus pipeline into the Hijack

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| P1 | **CAkResampler kernel selection.** fmt bits&0x3F is 16 (int16) or 32 (float). Row index = table 0x00FFD450 [0,1,2,2 \| float 4,5,6,6] by channels−1, plus mode·8. Mode is 0 (step 0x10000, bypass), 1 (fixed pitch) or 2 (ramp over 0x400 frames, then back to 1). Kernel table is 0x0103C0B8. | 0x00A47038..0x00A47150; 0x00A47178..0x00A47210; 0x00A47384..0x00A4751C | M6-004 | EXACT_SOURCE |
| P2 | **int16 to float.** <br>- Bypass kernel 0x00A48F5C: sample × 1/32768 (0x38000000). <br>- int16 mono interpolating kernel 0x00A4913C: out = (float)((x0<<16) + (x1−x0)·frac16) × 2^-31, i.e. linear interpolation done in integers. <br>- Step = (u32)(float(inRate/outRate) · powf(2, cents/1200) · 65536 + 0.5). | 0x00A491F0..0x00A4922C (0x30000000 at 0x00A495B8); 0x00A473E0..0x00A4741C | M6-004 (the voice stage is also linear, not sinc) | EXACT_SOURCE for the mono kernels; the stereo int16 kernel 0x00A49634 was not read |
| P3 | **Mix rate.** <br>- Anki never sets platform +0x38 (rate) or +0x44 (round-to-HW, default 1): it writes only +0x34, +0x4C (JavaVM) and +0x50 (activity). <br>- Platform init 0x00A57724: if the JavaVM is set, cache[0x0108DF90] = min(getNativeOutputSampleRate(3), 48000) and cache+4 = atoi(OUTPUT_FRAMES_PER_BUFFER). <br>- If +0x38 == 0 then +0x38 = cache; if that is still 0 then 48000. <br>- The rate is then passed to 0x00A1C75C. | 0x008D8158..0x008D81AC; 0x00A571A0..0x00A571C4; 0x00A56E44..0x00A56EA4, 0x00A57040..0x00A57054; 0x00A57834..0x00A578CC | NEW | EXACT_SOURCE for the logic; the value is **HARDWARE_ONLY** (depends on the phone) |
| P4 | **Samples per frame.** Default 0x400. If 1024 % hwFrames ≠ 0 and round-to-HW is set (the default), it becomes the nearer of floor/ceil(1024/hw)·hw (for example 960 when hw is 240). Otherwise the HW buffer is set to 1024. | 0x00A577B8..0x00A57830 | NEW | EXACT_SOURCE logic; value **HARDWARE_ONLY** |
| P5 | **Robot_Bus_1..4 channel config** (runtime reader 0x009C6420: props, byte, flags, u16 maxInst, u32 channelConfig, with popcount(mask & 0x3FF3F) when type 1). Init.bnk gives 0x00004101: 1 channel, standard, mask 0x4 (front centre). **The buses are mono**, so the Hijack gets one channel and the float-mono kernel 0x00A49E40. UpdateBuffer's copy of the first n floats is therefore the whole signal. | 0x009C64C0..0x009C6500, 0x009C660C..0x009C665C; Init.bnk HIRC object 0x9FA5953C body offset 0x21 | NEW (settles M6 Q3) | EXACT_SOURCE |
| P6 | **Bus FX chain** (reader 0x009C0D08: u8 numFx, u8 bypass bits, then per FX {u8 slot, u32 fxID, u8 isShareSet, 1 byte skipped}). Robot_Bus_1 has 4 slots with bypass 0: <br>- slot 0: EQ ShareSet 0x6767FC1F (plugin 0x690003); <br>- slot 1: EQ ShareSet 0x174901C6 (0x690003); <br>- slot 2: limiter ShareSet 0xDF2230FF (0x6E0003); <br>- slot 3: **Hijack** custom 0x18955E1F (0x112C3, param 1). <br>So **the Hijack is last and sees the output of the EQs and the limiter**. | 0x009C0D80..0x009C0E0C; Init.bnk bytes as cited | NEW | EXACT_SOURCE for the order; whether the mixer applies slots 0..3 in order is RECOVERABLE_GAP (read the node process loop near 0x00A4F754) |
| P7 | **Volume placement.** <br>- Robot_Bus_1 has no RTPCs. <br>- **robot_volume 0x637C1240 appears only on bus Cozmo_Robot 0x66BA9C8A**: param 5, points (0 → −200, 1 → 0), curve 0x0AE93585. <br>- event_volume 0xD2687048 is only on actor-mixers 0x03B2CFA4 and 0x28B5BC2D in Cozmo.bnk. <br>- So event_volume acts at voice level, before the send. robot_volume sits on a sibling bus that is on the muted dry path, not the Robot_Bus_1 aux path. | `rtpcscan.py` over re-analysis/obb/sound_meta; Init.txt:122,126 | M3 C17, M6 A24 | bank facts EXACT_SOURCE; whether aux sends tap before the output-bus volume, and what param 5 means, are RECOVERABLE_GAP |
| P8 | The Hijack's input is float, in place, on a mono buffer at the mix rate. **Resampling to 22320 Hz is float linear** (M6 0.11). The step is round(float(mix/22320)·65536), e.g. about 140938 at 48 kHz. | 0x008DBF76..0x008DBFB6; 0x00A47038, 0x00A47384 | M6-004 | EXACT_SOURCE |
| P9 | Whether sounds have "use game-defined aux sends" enabled, which decides whether the gameObj-7 send reaches Robot_Bus_1 at all, was not read. | — | NEW | UNKNOWN / RECOVERABLE_GAP (Sound/ActorMixer NodeBase aux flags in Cozmo.bnk plus the runtime reader) |

#### 4. Bus-FX lifetime (partial)

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| L1 | **Effect alloc** 0x009CC2AC: looks up the registered id in the 12-byte table 0x0108D9DC, calls create(g allocator 0x0108DA00), then GetPluginInfo (vt+0x10). | 0x009CC2AC..0x009CC348 | NEW | EXACT_SOURCE |
| L2 | **Bus-node insert** 0x00A4E974 (slot array node+0xD8, 0x1C per slot): drops the old slot (0x00A4E7CC, which calls **Term via vt+8** at 0x00A4E810), allocs, then **Init via vt+0x1C** at 0x00A4EB34. Callers: 0x00A4F754, reached from 0x00A41254 / 0x00A41320 / 0x00A4FEF8 (all vtable-called). | as cited | M6 A15/A18 | sites EXACT_SOURCE |
| L3 | **Node teardown** 0x00A4ECE4 → drop. Reached from 0x00A4EEE8, whose callers include the SoundEngine::Init path (0x00A40D44 → 0x0099DCEC) and a large set of LEngine functions (0x00A42210 / 0x00A429F0 / 0x00A43438 ← 0x009D418C). | `up.py 0xa4ed3c 4` | NEW | the trigger conditions (bus goes active or idle) are **RECOVERABLE_GAP** |

#### 5. Registration: which callbacks are live

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| R1 | 0x008DB84C is `std::function::swap`, **not** RegisterPlugin. The M6 A25 address is wrong. | 0x008DB84C..0x008DB8E2 | M6 A25 | EXACT_SOURCE |
| R2 | **RegisterPlugin 0x008DB300** swaps a lambda capturing `this` (vtable 0x010386DC; its operator() 0x008DB94A calls SetupEnginePlugInFx(this, fx) through veneer 0x00AE3090) into the **global std::function 0x0108D110** (GOT 0x0103FF78). **The last caller wins.** | 0x008DB300..0x008DB33C | NEW | EXACT_SOURCE |
| R3 | **Static registration** 0x004DD90C: PluginRegistration at 0x0108D128 {type 3, company 0x12C, id 1, create 0x008DBC71, params 0x008DBD11} is prepended to g_pAKPluginList, and global+0x10 is cleared. The runtime walks the list at 0x009CBECC. | as cited | NEW | EXACT_SOURCE |
| R4 | **HijackFx create** 0x008DBC70: alloc 0xD8, ctor; **if the global is set, call it(fx)**. That binds fx+0x0C = 22320, fx+0x10 = 744 and the create/process/destroy lambdas to the registering plug-in. | 0x008DBC70..0x008DBCB2 | NEW | EXACT_SOURCE |
| R5 | **Order in SetupPlugins.** <br>1. SetupHijackAudioPlugInAndRobotAudioBuffers: plug-in A = new(22320, 744), stored at [iface], RegisterPlugin(A); A has no callbacks. <br>2. Plug-in B = new(22320, 744), not stored. <br>3. SetCreate/SetDestroy/SetProcess on B, each re-registering B. <br>RegisterPlugin has only these four callers, so **B's CozmoAudioController lambdas are live** for every HijackFx created afterwards. | 0x005942C6..0x00594354; 0x008DC3EC..0x008DC41E; RegisterPlugin callers 0x008DB382, 0x008DB3BA, 0x008DB3F2, 0x008DC41E | M6 A25 | EXACT_SOURCE |

#### 6. Timing model

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| T1 | **OpenSL player**: PCM, **int16**, channels, rate·1000, little-endian, on a buffer queue. The callback is 0x00A582F8, primed once, then play state = 3 (playing). | 0x00A58600..0x00A587D8 | NEW | EXACT_SOURCE |
| T2 | **Callback** (paced by the phone's audio output): <br>- if ring frames ≥ HW frames, it enqueues one HW buffer, atomically decrements, and advances mod ring size; <br>- on underrun with an empty queue it enqueues anyway and sets starvation flag +0x20; <br>- when free space ≥ one engine frame (u16 0x01052440) it **signals the audio-thread event** (0x00A40924 on [0x0108D870]+0x54). <br>The audio thread therefore renders frames of uNumSamplesPerFrame at the mix rate on demand (for example 48000/1024 ≈ 46.9 frames/s), and the Hijack emits a 744-sample chunk about every 1.56 engine frames. | 0x00A582F8..0x00A5846C | M6 A10 | EXACT_SOURCE for the model; ring size and buffer count not read; absolute pacing **HARDWARE_ONLY** |

#### Existing records contradicted
- **M6-002**: the runtime decoder is located (V1–V8). It is not stock libvorbis:
  - integer residue and coupling;
  - floor table = Tremor integer / 2^15;
  - a 1-bit mode number;
  - planar float output.
- **M6-004**: the voice stage is also linear interpolation (the int16 kernel works in Q16 integers). No sinc exists anywhere in the path.
- **M6 A25**: the RegisterPlugin address was wrong (it is 0x008DB300). Which registration wins is now settled: B.
- **M3 C17 / M6 A24**: they assume robot_volume shapes the robot audio. The bank puts it only on Cozmo_Robot, not on Robot_Bus_1..4.

#### Existing records whose evidence is too weak
- **M6-003** holds for stereo interleave (D2 confirms "two mono blocks side by side"). Its per-block arithmetic is still contradicted as M6 reported.
- **Any record that assumes 1024 samples per frame or a 48 kHz mix**: both are phone-dependent (P3, P4).

#### Open questions for the manager
1. Do all shipped Vorbis media have modeCount == 2? The runtime assumes 1 mode bit. Checkable from the banks' setup packets.
2. robot_volume sits on Cozmo_Robot (dry path, muted per A11). Settling whether it affects robot audio needs the Wwise aux-send and output-bus-volume semantics (RECOVERABLE_GAP), plus a policy decision on how to treat it meanwhile.
3. Mix rate and frame size are HARDWARE_ONLY. Choosing the stack's input rate to the 22320 Hz linear resampler is a COMPATIBILITY_POLICY decision.

#### NOT DONE (RECOVERABLE_GAP, all in the binary)
- Vorbis: codebook dequantisation, residue decode 0x00AB73F8, overall output gain, and the source of +0x2C/+0x2E.
- The stereo and float kernels other than mono: 0x00A49634, 0x00A4A03C, and the mode-2 set.
- The voice volume, fade and mixer code; aux-send gain application; the order in which the bus FX loop runs.
- The bus-node activate/idle triggers (L2/L3 callers are vtable methods, not yet identified).
- The sink's ring size, buffer count and the audio-thread loop body.
- The Sound/ActorMixer aux-send flags (P9).

## Appendix D: gap C (gain path, bus DSP) with addendum, extractor report

I read primary source only. All addresses are libcozmoEngine.so VAs.

- **Scratch folder:** `...\scratchpad\extract\M6-gapC\`. It holds `r.py` (range print from gapB's `wwise_arm.txt`), `vt.py` (vtable dump), `vcall.py` / `funcs.py` / `fq.py` (virtual-call and co-occurrence search), `gauxsend.py` (effective game-aux flag per sound), `evsub.py`, `nodeinfo.py`, `armctors.txt` (ARM static constructors) and `lim.txt`.
- **Naming:** names come from public Wwise usage and are labels only. Every behaviour is cited to the binary.
- **Offsets:** "pbi" is the playing instance. Its effective-params block is at pbi+0x3C (io). The voice context ctx = pbi+0xC.

#### Q2 answer (gapA open question): robot_volume cannot change what the Hijack receives, on every path read

1. The signal enters Robot_Bus_1 only through the voice's aux-send connection. Its gain is:
   - voice gain × 1.0 × Σ send gains to that bus (0xA447EC..0xA448A8, 0xA4FD10..0xA4FD6C, 0xA597A4..0xA597BC);
   - where send gain = dBToLin(GameAuxSendVolume) × game-object control value (0x9BD36C).
2. The voice gain is built from io Volume. The bus part of io Volume is only bus prop 0 (Voice Volume) plus its param-0 RTPC; bus param 5 is masked off (0x9C1F90 `bic r8,r2,#0x10`).
3. Bus Volume (param 5, driven by robot_volume) goes to only two places:
   - the Cozmo_Robot bus node's own output gain, stored after that bus's FX (0xA4D3A0, 0xA4D994, 0xA4FFB0);
   - or, when the bus is collapsed, the voice's dry OutputBusVolume (0x9FFEB0..0x9FFEBC → pbi+0x64 at 0x9FFDF0..0x9FFE04).
4. The dry path is also multiplied by SetGameObjectOutputBusVolume = 0.0.
5. Robot_Bus_1's parent is Master Audio Bus, not Cozmo_Robot (Init.bnk bus parents).

One further point, not needed for the answer: bus-volume RTPCs are evaluated with an empty RTPC key (0x9C3A48..0x9C3A78). Whether a value set on game object 7 is visible under that key is not established (0xA11590 → 0xA17280 not read).

#### 1. GetAudioParameters (node 0x9EF258; bus 0x9C1F8C; Sound override 0xA1DAB8)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 1.1 | **Link roles.** <br>- NodeBase overrideBus → bus vfunc+0x28 (0x9C181C) → child vfunc+0x20 (0x9F1EE8) stores **node+0x38 = output bus**. <br>- directParent → actor-mixer AddChild 0x981940 → child vfunc+0x1C (0x9F1C40 `str r1,[r0,#0x34]`) stores **node+0x34 = parent**. | 0x9F709C..0x9F70D8, 0x9F7038..0x9F7074; 0x9F1F4C; 0x9F1C40 | gapA 6.1 | EXACT_SOURCE |
| 1.2 | **Recursion.** <br>- If bDoBusCheck ([sp+0xE4]) is set and node+0x38 ≠ 0: read props 0x18 OutputBusVolume → io+0x28, 0x1A → io+0x2C, 0x19 → io+0x30 (each plus its RTPC). Then recurse into the parent (+0x34) **and** the bus (+0x38), both with bDoBusCheck = 0. <br>- Otherwise recurse only into the parent, passing the flag through. <br>- So the lowest node that overrides the bus supplies the output-bus values and the bus chain. | 0x9EFDC8..0x9F0070, 0x9F00C8..0x9F0114 | NEW | EXACT_SOURCE |
| 1.3 | **Per-node sums (paramSelect bits):** <br>- bit1: prop 0 Volume → io+0 <br>- bit2: prop 2 Pitch → io+8 <br>- bit4: prop 3 LPF → io+0xC <br>- bit8: prop 4 HPF → io+0x10 <br>- MakeUpGain (prop 6) → io+0x18 <br>Each adds its RTPC through 0xA11590, keyed by the voice's RTPC key (pbi+0x14). **Everything is summed in dB / cents.** | 0x9F06C8..0x9F0890, 0x9F05F8, 0x9F050C, 0x9F043C | gapA 6.1 | EXACT_SOURCE |
| 1.4 | **Other additive bundles:** <br>- node+0x18 (gated by +0x46 bit0), read by 0x9F9CDC; <br>- node+0x24 (global); <br>- node+0x48 (map keyed by the game object in the RTPC key). <br>All three add ids 0/2/3/4 into the same slots. **MuteRatio (id 0xB) ≠ 1.0 is inserted into the muted map instead.** No Cozmo.bnk node has state groups. Which of these bundles are states and which are runtime-set values is not traced. | 0x9EF2D8..0x9EF528, 0x9EF52C..0x9F037C, 0x9F9CDC.. | gapA called +0x24 "states" (unverified) | arithmetic EXACT_SOURCE; the writers are RECOVERABLE_GAP |
| 1.5 | **Randomizer (ranged props, node+0x4C)** goes into a separate ranges struct, only when that pointer is non-null: Volume → +0, MakeUpGain → +4, Pitch → +8, LPF → +0xC, HPF → +0x10. <br>Value = min + ((LCG_hi>>1)/2147483647.0)·(max−min), computed in double. | 0x9EF57C..0x9EF968 | M6-007 | EXACT_SOURCE |
| 1.6 | **The randomizer is drawn once per voice.** CalcEffectiveParams 0x9FFAD4 passes ranges = pbi+0x118 only while pbi+0x1BC bit0 is clear (0x9FFCF0 `addeq ip,r4,#0x118`). It sets that bit at 0x9FFC30..0x9FFC3C, reached at the end of a normal pass via 0x9FFE64. <br>Afterwards: pbi+0x3C = Volume + range volume; pbi+0x44 = Pitch + range pitch. | 0x9FFD14..0x9FFE0C; 0x9FF3E0..0x9FF3F4 | NEW | EXACT_SOURCE |
| 1.7 | **Game-defined aux (decided once, at the first deciding node):** the first node with node+0x40 bit21 set, or the top node (no parent). <br>- io+0x59 = decided <br>- io+0x54 += prop 0x17 GameAuxSendVolume (+RTPC) <br>- **io+0x58 = node+0x59 bit4 ("use game-defined aux")** <br>**Aux reader 0x9ED84C:** bank bit b0 → +0x40 bit21 (override), b1 → +0x59 bit4 (use), b2 → +0x40 bits22-25 (override user aux), b3 → 4×u32 user aux IDs at +0x54. | 0x9EFC60..0x9EFD6C, 0x9F0390; 0x9ED860..0x9ED8C8 | gapB P9 | EXACT_SOURCE |
| 1.8 | **User aux:** decided the same way (io+0x5A). Props 0x13..0x16 → io+0x34..0x40, and the four IDs are copied to io+0x44..0x50. **No shipped node has aux bit3, so there are no user sends.** | 0x9EF96C..0x9EFC5C | NEW | EXACT_SOURCE |
| 1.9 | **Bus GetAudioParameters:** <br>- It clears paramSelect bit 0x10 (Bus Volume). <br>- It sums bus props 0/2/3/4 plus RTPCs, evaluated under an empty key. <br>- Volume also gets max(bus+0x6C (init −96.3), Σ ducking at +0x8C). <br>- Then it recurses into the parent bus (+0x38). <br>- Master, Cozmo_Robot and Robot_Bus_1 have no prop 0 or 2 (Init.bnk). | 0x9C1F90, 0x9C23F4..0x9C2518, 0x9C22B0..0x9C22F4; 0x9C3658 | NEW | EXACT_SOURCE |
| 1.10 | **Sound override (0xA1DAB8):** after the base call, if the MIDI key byte is ≠ 0xFF, pitch += 100·(key note − value from 0x9FAE18). | 0xA1DAEC..0xA1DB3C | NEW | EXACT_SOURCE (the 0x9FAE18 semantics are RECOVERABLE_GAP) |
| 1.11 | **Where dB becomes gain.** <br>- CalcEffectiveParams sets pbi+0x40 = max(0, Π muted-map ratios × pbi+0x168 × pbi+0x16C). <br>- The voice then sets **voice+0x1C = dBToLin(pbi+0x3C) × pbi+0x40**. <br>- dBToLin is the fast pow: y = 0.05·dB; if y < −37 the gain is 0; otherwise bits = u32(1065353216 + 27866352·y) and gain = float((bits>>23)<<23) × (0.6530434 + m(0.0208058 + 0.3251898m)), where m = mantissa bits \| 0x3F800000. | 0x9FFD84..0x9FFE18; 0xA4B608..0xA4B674 | NEW | EXACT_SOURCE |
| 1.12 | **Pitch to the resampler:** the consumer of pbi+0x44 is not traced. The cents→step formula is gapB P2. | — | M6-004 | RECOVERABLE_GAP |

#### 2. Voice → aux send, and the muted dry path

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 2.1 | **SetGameObjectAuxSendValues:** API 0x9A0354 (≤4 entries) → message 0x12 → handler 0x9AEDE0 → 0xA0BA3C. It stores up to four {busID, value} pairs, **only where busID ≠ 0 and value > 0**, at regobj+0x24. | 0x8D913E→0x9A0354; 0xA0BA3C..0xA0BB50 | M6 A11 | EXACT_SOURCE |
| 2.2 | **SetGameObjectOutputBusVolume:** API 0x9A044C → message 0x13 (+0xC = −1) → 0xA0CB78. It stores **regobj+0x60 = the value (0.0)** and regobj+0x78 = −1. | 0xA0CBE0..0xA0CBEC | M6 A11 | EXACT_SOURCE |
| 2.3 | **Send list (entry 0x9BD368: `ldrb r3,[r0,#0x88]` = io+0x58 "use game aux"):** <br>- If use-game-aux is set and regobj+0x24 ≠ 0: for each game-object entry i, gain_i = dBToLin(ctx+0x84 = io+0x54 GameAuxSendVolume) × regobj control value_i. If gain_i > threshold ([0x1052454]) it emits {bus, gain, type 1}. <br>- User sends: dB volume > threshold ([0x1052450]) → {id, dBToLin(vol), type 2}. <br>- **The send gain does not include Volume, OutputBusVolume or the game object's OutputBusVolume.** | 0x9BD368..0x9BD8B4 | NEW | EXACT_SOURCE |
| 2.4 | **Voice refresh (0xA4B5D8 / 0xA4B93C):** <br>- voice+0x1C = dBToLin(Volume) × pbi+0x40. <br>- **Dry entry gain = regobj+0x60 × dBToLin(io+0x28 OutputBusVolume)**, i.e. 0.0 × … for game object 7. <br>- Sends are built only if 0x9BDA88 is true (use-game-aux, or any user ID), then 0x9BD368 builds the list and 0x9D4228 stores it at voice+0x2C, stride 0x14 {+0 next, +4 prev, +0xC busID}. <br>- 0x9D4108 → 0xA43434 → 0xA4C280 creates a connection to the aux bus's node with conn+0x68 = the send entry (ctor 0xA6F90C `str r6,[r4,#0x68]`). | 0xA4B608..0xA4B75C; 0x9D4568..0x9D4630; 0xA6F990 | NEW | EXACT_SOURCE |
| 2.5 | **Mix gains.** <br>- conn+0xC = voice+0x1C × arg (0xA4BE6C). <br>- conn+0x14 = the dry entry gain if conn+0x68 == 0; **1.0 if it is an aux connection** (0xA597A4..0xA597BC). <br>- The voice mix loop (0xA447EC..0xA448F8) passes, for aux connections, extra = Σ over voice send entries whose busID equals that bus node's ID of {+4 prev, +0 next}. Dry connections get {1, 1}. <br>- The bus ConsumeBuffer 0xA4FBEC multiplies: prev = conn+0x10 × conn+8 × extra[0], next = conn+0x14 × conn+0xC × extra[1], then calls the mixer 0xA45E9C with the conn+0x20/+0x24 matrices. | as cited | NEW | EXACT_SOURCE for the gain product; the mixer ramp (0xA45E9C) and the 2D mono→mono panning matrix (0xA25FF8 via 0xA5975C) are **RECOVERABLE_GAP** |
| 2.6 | **Result for game object 7 → Robot_Bus_1:** input = src × dBToLin(Volume_total) × (mute/fade product) × dBToLin(GameAuxSendVolume) × 1.0 × pan. GameAuxSendVolume is 0 dB in every shipped deciding node (no prop 0x17, no RTPC on it), so the send is unity. | composite | M3 C17, M6 A24 | EXACT_SOURCE except pan |
| 2.7 | **Collapsed buses:** 0x9C54E8 returns 0 (bus not instantiated) when the bus has no FX, no aux list, has a parent, and several flags are clear. The PBI then folds that bus's Bus Volume (0x9C39DC with param 5) into **OutputBusVolume, i.e. the dry path only**. | 0x9FFC04..0x9FFC10, 0x9FFEB0..0x9FFEBC; 0x9C54E8..0x9C5584 | NEW | EXACT_SOURCE |
| 2.8 | **Shipped use-game-aux, resolved by the 1.7 rule** (`gauxsend.py`): <br>- Cozmo.bnk: 1862 sounds under 62050212, 334 under 682998829, 5 under 229678261, 20 under 677877281 (Cozmo_Robot_External): use = 1. <br>- **10 sounds under RanSeq 461334142 (aux byte 1 = override on, use off): use = 0**, so no send and no Robot_Bus_1 signal. Their only event is **Play__Robot_VO__Nurture_Play_Concern_Short** (4217319781). | bank bytes + 0x9EFC60 rule | NEW | EXACT_SOURCE |

#### 3. Bus FX loop and where bus volume is applied

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 3.1 | **FX loop 0xA4FD84.** When bus-node+0x1BC == 1 (ConsumeBuffer sets 4 → 1, 0xA4FC10..0xA4FC24), it runs slots **i = 0, 1, 2, 3 in order**. Slot layout: FX pointer at +0xD8+0x1C·i, flags at +0xE0+0x1C·i, out-of-place buffer at +0x138+0x1C·i. <br>- Skip if the FX pointer is null. <br>- Bypassed = slot bit0 \| bus +0x1B8 bit0. <br>- **Not bypassed:** Execute (vfunc+0x20), in place or out of place; out of place makes the output the current buffer. <br>- **Newly bypassed:** Reset (vfunc+0xC). Slot bit1 records the previous bypass state. <br>So Robot_Bus_1 runs EQ 0x6767FC1F → EQ 0x174901C6 → limiter 0xDF2230FF → Hijack. | 0xA4FE18..0xA4FEB0 | gapB P6 | EXACT_SOURCE (the meaning of other +0x1BC states is RECOVERABLE_GAP) |
| 3.2 | **Bus volume comes after the FX.** GetResultingBuffer 0xA4FEF8 calls the FX loop, then 0xA4D994 computes the bus gain (+0x84 = dBToLin(+0x90), prev +0x80) and copies {prev, next} into the output buffer's +0x10 for the downstream mix. <br>+0x90 = Bus Volume param 5 (init 0xA4D37C → 0x9C39DC(bus, 0, 5); RTPC notify 0xA4D308 adds deltas for param 5). <br>**The Hijack sees its bus's signal before that bus's own volume.** Robot_Bus_1 has no props or RTPCs. | 0xA4FF3C..0xA50008; 0xA4D9E0..0xA4DA78; 0xA4D3A0..0xA4D3B8, 0xA4D354 | M6 A13 | EXACT_SOURCE |

#### 4. DSP plug-ins on Robot_Bus_1

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 4.1 | **Registration:** ARM static constructors prepend {type 3, company 0, id} to g_pAKPluginList. <br>- Peak Limiter (id 0x6E) at 0x4DEB18: create FX 0xAA18F4, params 0xAA2280. <br>- Parametric EQ (id 0x69) at 0x4DEB8C: create FX 0xAA257C, params 0xAA3178. <br>- Pointers come through GOT 0x10401DC/E0/E4/E8. <br>No other RegisterPlugin callers exist (0x99F1D0 has no xrefs; the list is walked at 0x9CBAE0). | 0x4DEB44 `mov lr,#0x6e`; 0x4DEBB8 `mov lr,#0x69` | NEW | EXACT_SOURCE |
| 4.2 | **EQ params (SetParamsBlock 0xAA2E8C, 56 bytes):** 3 × {u32 type, f32 gain dB, f32 freq, f32 Q, u8 on}, then f32 outputLevel dB and u8 processLFE. The field roles are proven by the DSP (4.3). <br>**0x6767FC1F:** <br>- band 1 LowShelf +2.0 dB @835 Hz (Q 2.1 unused) <br>- band 2 Peak −2.5 dB @1359 Hz, Q 4.2 <br>- band 3 Peak −4.0 dB @5091 Hz, Q 1.5 <br>- all bands on; output +1.5 dB <br>**0x174901C6:** <br>- band 1 HighPass @333 Hz, on <br>- band 2 Peak −4.5 @1000 Hz, Q 0.5, **off** <br>- band 3 LowPass @14298 Hz, on <br>- output 0 dB | 0xAA2E94..0xAA2F2C; Init.bnk HIRC type 18 | NEW | EXACT_SOURCE |
| 4.3 | **EQ coefficients (0xAA25E0):** <br>- fs = format rate (fx+0x48); fc = min(freq, 0.45·fs). <br>- **Butterworth low/high pass** (0 LP, 1 HP; Q ignored): LP uses c = 1/tan(π fc/fs), HP uses c = tan(π fc/fs); b0 = 1/(1+√2c+c²). <br>- **RBJ band-pass (2), notch (3) and peaking (6):** w0 = 2π fc/fs, α = sin/(2Q), A = 10^(gain/40). <br>- **RBJ low shelf (4) and high shelf (5)** with S = 1 fixed (the (1/S−1) term is the constant 0.0; Q unused): α = sin·√2/2. <br>- Stored as {b0, b1, b2, −a1, −a2} / a0 at fx+4+20·band. Transcendentals are bionic sinf/cosf/tanf/powf/sqrtf. <br>- **Recomputed only when the band's dirty flag is set; no coefficient smoothing.** | 0xAA2600..0xAA2A80; store 0xAA272C..0xAA276C; dirty 0xAA2AC8..0xAA2D1C | NEW | EXACT_SOURCE |
| 4.4 | **EQ Execute (0xAA2A84):** <br>- For each band that is **on**: direct-form-I biquad 0xAA2324, in place, per planar channel. Accumulation order: y = ((((b2·x2 + x·b0) + b1·x1) + (−a2)·y2) + (−a1)·y1) with vmla. State is {x1, x2, y1, y2} per band per channel (Init 0xAA24B8 allocates numCh·3·16 bytes). <br>- Then the output gain: target = 10^(0.05·outLevel). If it equals the previous gain (fx+0x50) it multiplies (skipped when 1.0); otherwise it ramps linearly over the buffer. <br>- Quirk: the scalar tail after the NEON 4-sample loop restarts the ramp from prev. <br>- Init sets the previous gain to the target, so there is no start ramp. | 0xAA2B00..0xAA2DE0; 0xAA2388..0xAA23B0; 0xAA2C3C..0xAA2C60 | NEW | EXACT_SOURCE |
| 4.5 | **Limiter params (0xAA2084, 22 bytes):** <br>- thr dB → +4 <br>- ratio → +8 <br>- lookahead s → +0x18 <br>- release s → +0xC <br>- output dB → +0x10 as powf(10, 0.05·v) <br>- u8 processLFE → +0x1C <br>- u8 channelLink → +0x1D <br>Defaults (0xAA20FC): −12, 10, 0.01, 0.2, 1.0, 1, 1. <br>**0xDF2230FF: threshold −1.0 dB, ratio 10.8, lookahead 0.009 s, release 0.041 s, output 0 dB, processLFE 0, channelLink 0.** | 0xAA2084..0xAA20F4 | NEW | EXACT_SOURCE |
| 4.6 | **Limiter setup (0xAA19CC):** <br>- L = u32(float(sr)·lookahead) samples. **At 48 kHz this is 431, not 432**, because float(0.009)·48000 truncates. <br>- attack = expf(−2.2/(L/2)); release = expf(−2.2/(sr·release)), recomputed when dirty. <br>- A delay line of numCh·L floats; one detector per channel when channelLink == 0. <br>- Process function for unlinked or mono = 0xAA0EB4; other variants are 0xAA09B8 and 0xAA1464. | 0xAA1A44..0xAA1B84; 0xAA1D4C..0xAA1D74 | NEW | EXACT_SOURCE |
| 4.7 | **Limiter DSP (0xAA0EB4), per sample:** <br>- Write x into the delay line and read the sample from L samples ago (d). <br>- **Peak hold:** if the hold counter is 0 or \|x\| > held peak, then peak = \|x\|, hold = L, over = max(0, 20·log10(peak) − thr) using the fast log (exponent/mantissa, s = (m−1)/(m+1), (e−127)ln2 + 2s(1+s²/3), ×log10 e). Otherwise hold−− and over keeps the held value. <br>- **Envelope:** env = over + c·(env − over), with c = attack when over ≥ env, else release. <br>- **Gain:** gain = fastpow10(0.05·(1/R − 1)·env), with the factor computed in double; out = d·gain. <br>- A first-buffer pre-scan seeds the peak over min(frames, L) samples. <br>- NoMoreData (0x11) extends the output with an L-sample tail and returns 0x2D while the tail remains. <br>- The output-gain ramp is identical to the EQ's (same quirk). | 0xAA10D8..0xAA1400; 0xAA1168..0xAA11C0; 0xAA1E00..0xAA1E9C; 0xAA1C88..0xAA1F60 | NEW | EXACT_SOURCE |
| 4.8 | **Consequences for the Hijack:** its input is delayed by L samples. It is limited against −1 dBFS with ratio 10.8, which is soft (not brick-wall). The last buffers include the limiter tail. Both plug-ins run at the bus mix rate, which is phone-dependent. | composite | M6 A16 | EXACT_SOURCE; the rate is HARDWARE_ONLY |
| 4.9 | **Size and feasibility of a port:** <br>- EQ ≈ 3.5 KB of code: one coefficient routine plus one scalar biquad. <br>- Limiter ≈ 6.6 KB, of which only 0xAA0EB4 (≈1.4 KB) is needed for mono. <br>- Both are small and self-contained. <br>- Bit-exact output also needs bionic libm results and non-fused vmla ordering, which .NET MathF may not match. | — | NEW | port feasible |

#### 5. Fades and the Play action fade-in

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 5.1 | **PBI start with transition time ≠ 0:** pbi+0x168 = 0. It creates a transition {type 0x1000000, start 0.0, end 1.0, time = transParams[0] (ms), curve = transParams[4], linear (not dB), reverse-curve-on-decrease flag = 1} via 0xA36268, or updates an existing one via 0xA366F4. | 0xA0067C..0xA006A0, 0xA00774..0xA00828 | gapA 1.11 | EXACT_SOURCE |
| 5.2 | **Transition setup (0xA35D44):** <br>- Converts start/end from dB when the flag is set. <br>- **Duration in frames = (ms + msPerFrame − 1) / msPerFrame**, with msPerFrame = global 0x1052444 (static initial value 21; its writer was not found). <br>- If start ≥ end and the curve is not 3 or 5, the curve becomes 8 − curve. <br>- Start tick = mgr tick ([0x108D870]+0x4C). | 0xA35D44..0xA35E9C, 0xA35F14..0xA35F24 | NEW | EXACT_SOURCE (the msPerFrame writer is RECOVERABLE_GAP; its value is HARDWARE_ONLY) |
| 5.3 | **Per-frame update (0xA35998):** <br>- t = elapsed frames / duration frames. <br>- The curve switch uses the same shapes as the RTPC curves (gapA 5.3): 0 Log3, 1 Sine, 2 Log1, 3 InvSCurve, 4 linear, 5 SCurve, 6 Exp1, 7 SineRecip, 8 Exp3, default 0. <br>- At the end the value is set to `end` with done = 1. <br>- The value goes to TransitionUpdate 0x9FF41C, which sets pbi+0x168 (types 0x1000000/0x2000000) or pbi+0x16C (0x4000000/0x8000000) and marks the params dirty. <br>- The value then enters the voice gain as a linear factor (1.11). **It is updated once per audio frame.** Within a frame it follows the mixer's prev→next ramp, which is not read. | 0xA35AB8..0xA35CE4; 0x9FF41C..0x9FF494; 0x9FF3C4..0x9FF400 | NEW | EXACT_SOURCE except the intra-frame ramp (RECOVERABLE_GAP, 0xA45E9C) |
| 5.4 | **Shipped fade-ins:** <br>- Cozmo.bnk Play actions: 500 ms curve 4, 100 ms curve 1, 300/200/200 ms curve 4, 1500 ms curve 6. <br>- SFX.bnk: 300 ms with curves 6 and 7. <br>- No ranged fade values. | bank bytes (0xA61110 reader) | gapA 1.11 | EXACT_SOURCE |

#### Existing records contradicted
- **M3 C17 / M6 A24** assume robot_volume shapes robot audio. The runtime routes Bus Volume only to the Cozmo_Robot node output, or to the dry OutputBusVolume when that bus is collapsed. The aux send to Robot_Bus_1 contains neither (2.3–2.7, 3.2).
- **Any model that includes OutputBusVolume or the game object's output-bus volume in the aux send is contradicted** (0x9BD36C, 0xA597A4).
- **Any model in which every Cozmo event reaches the robot is contradicted:** Play__Robot_VO__Nurture_Play_Concern_Short has use-game-aux = 0 (2.8).

#### Existing records whose evidence is too weak
- **gapA's label "node+0x24 = state bundle"** is unverified (1.4).
- **M6 A16** says the Hijack gets bus audio "passed through". It gets EQ, EQ and limiter output, including the limiter's L-sample delay and tail (4.8).

#### Open questions for the manager
1. **Voice LPF:** top mixer 62050212 carries prop 3 LPF = 15. The consumer of io+0xC (pbi+0x48) and whether the voice filter runs before the sends were not found. This may change the Hijack input.
2. **Nurture_Play_Concern_Short:** the original, as read, sends nothing to the robot for this event. The stack needs a decision on how to treat it.
3. **Libm-dependent coefficients** (EQ) and the mix rate (L, fs) cannot be bit-reproduced without the phone. This is a COMPATIBILITY_POLICY decision.

#### NOT DONE (all RECOVERABLE_GAP, in the binary)
- The mixer 0xA45E9C (ramp shape) and the 2D panning matrix 0xA25FF8 for mono → mono (aux and dry).
- The voice LPF/HPF filter stage and the consumer of pbi+0x48/+0x4C.
- The pitch consumer (pbi+0x44 → voice resampler SetPitch).
- The writer and value of msPerFrame (0x1052444).
- The RTPC key-precedence path (0xA11590/0xA17280) for empty-key bus RTPCs.
- The writers of the node+0x24 / +0x48 bundles; 0x9EF0E4 (paramSelect bit 0x40); 0x9FAE18 (MIDI root note).
- The limiter's linked and LFE variants (0xAA1464, 0xAA09B8); not used on Robot_Bus_1.
- The meaning of bus-node states other than 1.

#### Addendum: the EQ and limiter are registered (contradicts gapD D2.3; manager-verified)

**Registration.**
- `.init_array` has two ARM-mode entries: 0x103E560 → 0x4DEB18 (limiter) and 0x103E564 → 0x4DEB8C (EQ). Each loads g_pAKPluginList through GOT 0x103FF7C (→ 0x108D9F4).
- Each prepends a record {next, type 3, company 0, id, create, params}:
  - limiter: id 0x6E, create FX 0xAA18F4, create params 0xAA2280;
  - EQ: id 0x69, create FX 0xAA257C, create params 0xAA3178.
- More ARM constructors in 0x4DE754..0x4DF1A8 register further IDs: 0x6C, 0x7F, 0x76, 0x74, 0x77, 0x78 and others.

**Walk at init.**
- Path: SoundEngine::Init 0x99E3EC → 0x99E668 → 0xA36B08 → 0xA57724 → 0xA57854 → LEngine init 0xA40BC0 → 0xA40C04 → 0x9CBECC → 0x9CBAE0.
- 0x9CBAE0 keys each record as (id<<16) + (company<<4) + (type & 0xF), which gives 0x690003 and 0x6E0003 (0x9CBB34..0x9CBBE4).
- **Result:** Robot_Bus_1 runs EQ 0x6767FC1F, then EQ 0x174901C6, then limiter 0xDF2230FF, then the Hijack, in slot order, when the bus state is 1.
- **The manager confirmed both constructors in `.init_array`.** gapD D2.3 came from a Thumb-only xref scan and is wrong.

## Appendix E: gap D (timing, lifetime), extractor report

**Scratch:** `...\scratchpad\extract\M6-gapD\`. New tools there:
- `d.sh` (address-range slice of gapB `wwise_arm.txt`)
- `r.py` (GOT/reloc value)
- `vt.py` (vtable dump)
- `rtbus.py` (runtime-order bus parser)
- `ven.pkl`

Everything else reuses the gapA/gapB tools. All addresses are libcozmoEngine.so VAs. Public Wwise names are labels only.

#### 1. Rate/frame globals and the pending-action drain

| step | what the original does | citation | classification |
|---|---|---|---|
| D1.1 | **GOT slots.** Base−0x224 (0x01040068) points to **0x0105243C** (u32 mix rate). Base−0x234 (0x01040058) points to **0x01052440** (frame size, read as u16). They are the same struct, +0 and +4. | reloc values (r.py) | EXACT_SOURCE |
| D1.2 | **Only writers:** <br>- SetRate **0xA1C75C**(rate): +0 = rate; +0x10 = (rate·128)/48000 (magic 0x057619F1, >>42); +8 = u32(frame/(rate/1000)); +0xC = u32(0.25·frame·1000/rate). <br>- SetFrame **0xA1C7D4**(n): +4 = n, and recomputes +8/+0xC. <br>Their single callers are 0xA57848 and 0xA57850 in platform init 0xA57724, so the values are fixed for the engine's lifetime. | 0xA1C75C..0xA1C7C8, 0xA1C7D4..0xA1C820; q.py callers | EXACT_SOURCE |
| D1.3 | **Values.** Rate = platform uSampleRate after gapB P3: +0x38 = cache 0x0108DF90 = min(native, 48000), or 0xBB80 = 48000 if there is no JavaVM or the cache is 0 (0xA578B4..0xA578C8). Frame = AkInitSettings+0x20 after the P4 HW rounding (0xA577C0..0xA57830; default 0x400 at 0x99DC90). | 0xA57834..0xA57850 | logic EXACT_SOURCE; the numbers are **HARDWARE_ONLY** (phone) |
| D1.4 | **EnqueueOrExecute 0x9AA0FC:** <br>- frames = delay / F (0x4BE310 = uidiv); queued+0xC = delay − frames·F (the sub-frame remainder, in samples); queued+8 = mgr+0x4C (the tick). <br>- frames == 0 → Execute now (vfunc+0x24), then 0xA04F54(playingID) and free. <br>- Otherwise launch = tick + frames, and the action is inserted into the pending list mgr+0x14/+0x18 (12-byte nodes {next, launch, queued}; free list +0x1C; count +0x28; cap +0x24). The insert walks while launch ≥ node.launch, so **equal ticks stay FIFO**. <br>- With no free node and count ≥ cap, **the action is dropped** (released and freed, with no 0xA04F54). <br>- Type 0x0503 has a special case that no Cozmo action uses. | 0x9AA140..0x9AA16C, 0x9AA18C..0x9AA1E8, 0x9AA314..0x9AA35C, 0x9AA364..0x9AA3C4 | EXACT_SOURCE |
| D1.5 | **Drain 0x9A9F88:** while head.launch ≤ mgr+0x4C: unlink, Execute (vfunc+0x24; Play also gets vfunc+0x30), then 0xA04F54(playingID) if nonzero, then release. | 0x9A9F94..0x9AA0E0 | EXACT_SOURCE |
| D1.6 | **Frame loop Perform 0x9AF8A8 (the audio manager is 0x0108D870).** Each iteration runs in this order: <br>1. messages 0x9ADFD8 (PostEvent → ExecuteEvent → EnqueueOrExecute); <br>2. drain 0x9A9F88; <br>3. if framesToRender > 0: 0xA36AC4(tick+1), 0x9FF308(tick+1), 0x9D3C98, 0x9E6D2C, **LEngine 0xA57FF8**, **PBI-notification flush 0xA38420**, then **mgr+0x4C++**; <br>4. repeat. When the count is 0, the loop exits after one more messages+drain pass. <br>**So an action with delay d launches in the drain that follows the ⌊d/F⌋-th rendered frame after its enqueue, before the next frame renders.** | 0x9AF9F4..0x9AFAB8 | EXACT_SOURCE |
| D1.7 | **Frames per Perform.** <br>- If byte [GOT−0x1D0] == 0: count = 0x9D4778() → 0x9EBE6C(0) (device frames needed). <br>- Otherwise a clock-paced count (elapsed/tickrate·rate/F plus the carried fraction +0x70, capped). <br>The flag's writer and 0x9EBE6C's internals were not read. | 0x9AF900..0x9AF9D0, 0x9AF920..0x9AF9B8 | RECOVERABLE_GAP (flag writer) |
| D1.8 | **Who runs Perform.** <br>- Audio thread 0xA4087C: loop { Perform; wait on event }. The sink signals that event (gapB T2). <br>- Anki's RenderAudio(true) goes 0x99F130 → 0x9AFD10, which renders **synchronously only if init+0x3D < allowSync**; otherwise it signals the thread. <br>- init+0x3D defaults to 1 (0x99DCDC), and Anki's settings writes seen are only +8 and +0x18 (0x8D8188..0x8D8192). **So normally this is the audio thread.** | 0x9AFD18..0x9AFDF4, 0xA408BC..0xA408D4 | EXACT_SOURCE (other writers of init+0x3D not exhaustively excluded) |
| D1.9 | **Use of the remainder.** <br>- Only Play uses it: queued+0xC → the play params' +0x74 (sample offset) at 0xA62BB8/0xA62BC8. <br>- 0x9F12E0 adds InitialDelay (prop 0x3B, **seconds**)·rate, rounded half away from zero, to +0x74 when params+0x7C ≠ 0; otherwise it defers via 0x9EDEB8. <br>- Stop/Seek never read +0xC. | 0xA62BB8, 0x9F14F0..0x9F1544 | EXACT_SOURCE; the voice-side consumer of +0x74 is RECOVERABLE_GAP |
| D1.10 | **Shipped data.** Cozmo.bnk has exactly one delayed action: Stop 0x0103 0x23D2D2C6, prop 0x0F = 200 ms, target 0x2357FDB4. It is the only action of event 3944269316 "Stop__Robot_Sfx__Scan_Loop_Stop". It fires at frame granularity: ⌊200·rate/1000/F⌋ frames, for example 9 frames = 192 ms at 48000/1024. | bnk scan; Cozmo.txt:512 | D EXACT; timing HARDWARE_ONLY |

#### 2. Bus FX (Hijack) lifetime

| step | what the original does | citation | classification |
|---|---|---|---|
| D2.1 | **Creation.** A bus node is created on demand, through GetOrCreateMixBus 0xA43438 → hierarchy 0xA429F0/0xA42754 → CreateMixBus **0xA42210** (alloc 0x1E8 or 0x1D0, ctor 0xA4DFB8/0xA41E48, Init 0xA4F0EC sets state +0x1BC = 4 and connections +0x1C0 = 0), then it is appended to the global bus array 0x0108DF54. <br>An existing node matching {node ctx +0x4C, +0x50, device key +0x28/+0x2C, state ≠ 2} is **reused** and gets +0x1CC bit0 set. <br>The voice→bus connection object (ctor 0xA6F90C via 0xA4C280) does **Connect 0xA4F664 (+0x1C0++)**; its dtors 0xA6F3DC/0xA6F8B4 do **Disconnect 0xA4F6F0 (+0x1C0−−)**. A child bus also connects to its parent. | 0xA43488..0xA43598, 0xA435C4..0xA435E0; 0xA42254..0xA422FC, 0xA42540..0xA4257C; 0xA4F220..0xA4F22C; 0xA6F9CC; 0xA6F40C | EXACT_SOURCE. The aux-send connection policy itself (gapB P9) is RECOVERABLE_GAP |
| D2.2 | **FX instantiation is lazy, at the bus's first GetResultingBuffer 0xA4FEF8.** If (+0x1B8 & 0xC) ≠ 4, it calls SetInsertFx **0xA4F754**(mask 0xF), which runs InsertFx 0xA4E974 per slot: <br>- create 0x9CC2AC; <br>- type-3 check 0x9CC4D8; <br>- the async flag must be 0; <br>- **Init vt+0x1C**; <br>- an out buffer only if the effect is not in-place; <br>- Reset vt+0xC. <br>It then sets +0x1B8 b2. **This happens in the same bus pass that first executes the FX**, so Hijack Init → PrepareAudioBuffer comes just before the first Execute in that frame. | 0xA4FF0C..0xA4FF3C; 0xA4F828..0xA4F8FC; 0xA4E9A8..0xA4EC58 | EXACT_SOURCE |
| D2.3 | **NEW, contradicts gapB P6.** EQ 0x690003 and Peak Limiter 0x6E0003 are **not registered**: <br>- The only static registrations are Anki's Hijack (0x4DD90C) and WavePortal (0x4DD97C). These are the only two refs to g_pAKPluginList (tx.py). <br>- Anki never calls RegisterPlugin 0x99F1D0/0x9CBF64 (no Thumb, veneer or GOT refs). <br>- For unregistered IDs from the INIT chunk, bank load 0x9B4048 calls 0xA57238, which dlopen's `<nativeLibraryDir>/lib<Name>.so` via JNI (0xA57374..; the strings are at 0xFA79DC..0xFA7B84). The return is ignored. **The APK ships no libParametricEQ.so or libAkPeakLimiter.so** (resources/lib/armeabi-v7a). <br>- 0x9CC2AC therefore returns 2 and InsertFx drops the slot (0xA4EA90..0xA4EAA0). <br>**On Robot_Bus_1 only slot 3, the Hijack, is instantiated. The Hijack sees the raw bus mix, not EQ/limiter output.** | as cited; Init.bnk INIT chunk (10 names) | EXACT_SOURCE (code) plus a shipped-artifact absence |
| D2.4 | **FX execute condition.** 0xA4FD84 runs slots 0..3 **only when state +0x1BC == 1**, and runs them in slot order. <br>- In-place: vt+0x20 on bus buffer +0x60; out-of-place when slot+0x138 is set. <br>- Bypass: slot b0 or bus +0x1B8 b0 → Reset once. | 0xA4FD84..0xA4FEF4 | EXACT_SOURCE (answers gapB P6's order question) |
| D2.5 | **State machine.** <br>- Mixing input (0xA4F9E0 child bus, 0xA4FBEC voice) sets +0x68 (eState) = 0x2D, state 4 → 1, and +0x6E = max frames. <br>- ReleaseBuffer **0xA4F36C** (every bus, every frame, in 0xA44C18) sets **state = (+0x68 == 0x11) ? 4 : 1**. Each non-bypassed out-of-place FX slot overrides this with its own state (the last slot wins). Then +0x68 = 0x11 and +0x6E = 0, and the buffer is zeroed. | 0xA4F9EC..0xA4FA18, 0xA4FC00..0xA4FC24; 0xA4F36C..0xA4F4A0 | EXACT_SOURCE |
| D2.6 | **Destruction.** 0xA43F64 runs after the bus pass, from last to first. **The bus is destroyed if state ≠ 1, +0x1C0 == 0 and +0x1CC b0 == 0.** Otherwise it is kept and b0 is cleared. <br>Destruction: Disconnect from parent → dtor 0xA4EED8 → 0xA4ECE4 → 0xA4E7CC per slot → **Term vt+8** (Hijack 0x8DBDF9 → destroy callback → CloseAudioBuffer). | 0xA44028..0xA44060, 0xA43F98..0xA43FD4; 0xA4ED2C..0xA4ED44; 0xA4E7F0..0xA4E818 | EXACT_SOURCE |
| D2.7 | **Frame order inside LEngine 0xA44D4C.** The voice pass 0xA44948 comes first: each voice renders and mixes into its buses (0xA44630 → 0xA4FBEC). A voice whose result is 0x11 is stopped (vt+0x48 0xA533FC, state 2) and **destroyed in the same pass** (0x9D40C4), which destroys its connections. Then the bus pass 0xA44C18 runs, then idle removal. | 0xA44A94..0xA44B30; 0xA44C78..0xA44CC4, 0xA44C6C | EXACT_SOURCE |
| D2.8 | **Hijack tail = one audio frame.** The Hijack Execute (0x8DBFE8) and the resampler kernels write only uValidFrames (+0xE), never eState. The EQ/limiter are absent. <br>- Frame N (the last voice data): bus state 1, Hijack consumes a full frame, bus kept. <br>- Frame N+1: state is still 1, so FX run with uValidFrames 0. **CAkResampler::Execute returns 0x11 immediately on empty input (0xA47178..0xA47188)**, so the Hijack calls the process callback with its partial output (+0x7A frames, possibly 0), then resets. ReleaseBuffer sets state 4. If no connection remains and b0 is clear, the bus is destroyed → Term → CloseAudioBuffer. <br>**Lifetime is per contiguous voice activity on that bus.** It is not per event and not engine lifetime: one shared instance across overlapping events, and a new instance (a new stream) after an idle frame. | 0x8DC004..0x8DC038; 0xA471F0; 0xA49FEC..0xA4A020 | EXACT_SOURCE. Bus-pass gating flag (arg of 0xA44C18 from bytes GOT−0x1CC/−0x1C4, 0xA44DD8..0xA44DF8): writer RECOVERABLE_GAP |
| D2.9 | **NEW, consumer side.** Anki UpdateBuffer 0x5985FC has **no zero-length check**: n = 0 pushes an empty frame (resize(0), 0x59866E..0x5986AA). M6 A21 zero-pads short frames to 744. | as cited | EXACT_SOURCE (the effect on A21 is for the M6 A-rows) |

#### 3. EndOfEvent timing

| step | what the original does | citation | classification |
|---|---|---|---|
| D3.1 | **Playing-ID record** (0xA03108): +0x18 PBI count = 0, **+0x1C action count = 1** (the in-flight event message), +0x40 callback, +0x44 cookie, +0x48 flags. | 0xA03164..0xA031E8 | EXACT_SOURCE |
| D3.2 | **Counters.** <br>- +0x1C: ++ in EnqueueOrExecute (0xA04EDC); −− after each execute (0xA04F54) and after ExecuteEvent in the type-1 handler (0x9AF2AC). <br>- +0x18: ++ in 0xA04D48; −− in 0xA04DE8, from PBI Term 0xA029DC (PBI vtable 0x103B768 +0x10) when pbi+0x140 ≠ 0. | 0xA04F3C..0xA04F44, 0xA04FB4..0xA04FD0, 0xA04DD0..0xA04DDC, 0xA04E48..0xA04E64; 0xA02A44/0xA02CA8 | EXACT_SOURCE |
| D3.3 | **CheckEndOfEvent 0xA03618.** When +0x18 == 0 and +0x1C == 0: <br>- remove the record; <br>- build {cookie, gameObj, playingID, eventID}; <br>- release the mutex; <br>- **call the callback(type 1, &info) synchronously on the calling thread** if flags & 1. | 0xA03618..0xA03648, 0xA03760..0xA037BC | EXACT_SOURCE |
| D3.4 | **PBI teardown is deferred within the frame.** Voice Term vt+0x44 0xA53EA8 → 0xA56414 → 0xA01800 → **0xA38600 queues {pbi, reason 4}**. The flush **0xA38420** (called at 0x9AFA94 right after LEngine) handles reason 4: unlink, 0x9D3470, **PBI vt+0x10 Term** (→ D3.2/D3.3), then vt+4 delete. | 0xA53EC4..0xA53F04; 0xA01800..0xA01814; 0xA38644..0xA38670; 0xA38484..0xA38508 | EXACT_SOURCE |
| D3.5 | **Result.** EndOfEvent fires on the audio thread **in the same frame as the last voice's final samples, after that frame's bus pass**. So the Hijack has already delivered the last full 744-chunks, but **before** frame N+1's partial-chunk flush and CloseAudioBuffer (D2.8). <br>If the event creates no PBI (unregistered game object: 0xA0C238 returns NULL, and object-scope actions are skipped), EndOfEvent fires in the message pass (0x9AF2AC) before any render. <br>A delayed action holds it until the drain executes that action. | D2.7, D3.3, D3.4; 0xA0C238..0xA0C2B4 | EXACT_SOURCE |
| D3.6 | **Anki side.** The callback 0x8D8D40 dispatches immediately to HandleCallback only if ctx+0x38 ≠ 0. Otherwise it queues into the mutex list 0x0108D0D4 (drained elsewhere). For EndOfEvent it also calls 0x9A206C(ctx+4). The value of ctx+0x38 for RobotAudioAnimation's context was not read. | 0x8D8DA2..0x8D8DCA, 0x8D8EAE..0x8D8F30 | RECOVERABLE_GAP (read PostCozmoEvent's AudioCallbackContext ctor; A7/A9 interface) |

#### 4. Continuous random/sequence (partial)

| step | what the original does | citation | classification |
|---|---|---|---|
| D4.1 | **Shipped data, correcting gapA 3.4/3.9.** Cozmo.bnk has **443 step (flags 0x12) and 25 continuous (0x1A)** RanSeq, not 437/31. <br>- Continuous transition modes: 1 ×15, 2 ×7, 4 ×2, 5 ×1. <br>- Loop 1 ×11, loop 0 ×14. <br>- Transition times 10..1000 ms, min/max 0. | nodes.py over all banks | D EXACT_SOURCE |
| D4.2 | **Continuous next-item 0xA0ABC4.** <br>- The per-play continuation 0xA091CC holds the state at +4 and loop info at +8 (s16 count, +0xA b1 = infinite). <br>- Length 0 → end (0x9EE954). Length 1 → no RNG; the count is decremented unless infinite; ≤ 0 → end. <br>- Otherwise sequence 0xA0856C or SelectRandomly 0xA08A44 with the loop-info pointer (gapA 3.6a). <br>- **Bank bit1 = +0x91 b4:** set → a fresh per-play state (0xA06994/0xA069DC, or 0xA06BB4 for sequence) and nothing is saved back. Clear → the state is taken from the global/per-object state (0xA099BC/0xA0729C) and saved back with 0xA095B4. <br>- **All 25 shipped continuous containers have b4 set**, so each play starts fresh. <br>- The chosen node is then played through 0x9F12E0 → PlayInternal vt+0x128. | 0xA0ABD4..0xA0AFC4, 0xA0AE2C..0xA0AE7C | EXACT_SOURCE for this function |
| D4.3 | How the transition modes apply (cross-fade amp/power, sample-accurate, trigger rate via 0xA09F04), where the transition time is applied, and the timing of the next item. | — | **RECOVERABLE_GAP / NOT DONE** (continuation-list and PBI transition code) |

#### 5. Action execute bodies, game-object lookup, RTPC store

| step | what the original does | citation | classification |
|---|---|---|---|
| D5.1 | **Queued action layout** (ExecuteEvent): +4 action, **+0x34 game object** (refcount +0x7C), +0x28 playing ID, **+0x30 = ExecuteEvent's 4th argument** (msg+0x10, the target playing ID; label only), +0x14/+0x1C/+0x20/+0x24 custom parameters. | 0x9AA450..0x9AA4B4 | EXACT_SOURCE (the +0x30 meaning is a label) |
| D5.2 | **Stop Execute is vtable 0x103CED0+0x24 = 0xA663C8, not a param reader.** It switches on type−0x102: <br>- **0x0102/0x0103** → target = GetNodePtr; missing → no-op. Then 0xA79F08(action, 0, gameObj +0x34, +0x30), **then 0x9AB8AC(audioMgr, target, gameObj, +0x30), which clears pending actions for that target**. <br>- 0x104/0x105 → 0xA79FA4 plus 0x9AB8AC(all). <br>- 0x108/0x109 → 0xA7A0DC plus 0x9ABEA4 (exceptions). <br>- Stop params: 0xA79E30 = u8 fade curve, vt+0x2C = 0xA60284 (reads nothing, returns 1), then the exceptions 0xA622A4. **This corrects gapA 1.9.** | 0xA663C8..0xA664E8; 0xA79E30..0xA79E7C; 0xA60284 | EXACT_SOURCE for the dispatch; 0xA79F08 and 0x9AB8AC internals RECOVERABLE_GAP |
| D5.3 | **Seek Execute** (vtable 0x103CCF8+0x24 = 0xA64B14): 0x1E02/0x1E03 → 0xA645C8. <br>- Target missing → error 0xF. <br>- value = +0x30 + (+0x34 + rand·(+0x38 − +0x34)) using the global LCG. Percent (+0x3C b0) stays a float; time is cvt to s32. Snap is +0x3D. <br>- Then target vt+0x4C({type 4, gameObj, +0x30, …}) and release. <br>- **Shipped:** one Seek, 0x3EB3BE48 → 0x094F11ED: percent, value 0, range [−0.67, 0.76], no snap. | 0xA64B14..0xA64B60; 0xA645C8..0xA64700 | EXACT_SOURCE; the node seek handler and its clamping are RECOVERABLE_GAP |
| D5.4 | SetState 0x1204 (vtable 0x103CE00; Execute 0xA657D4, params 0xA65750) is **not in Cozmo.bnk**. | factory; bnk | not read (out of Cozmo use) |
| D5.5 | **Game-object lookup 0xA0C238**(registry, id): hash (bucket = id % count at +0x20, table +0x1C, node {next, id, obj}). Found → obj with refcount +0x7C++ (30-bit). **Not registered → NULL.** | 0xA0C238..0xA0C2B4 | EXACT_SOURCE |
| D5.6 | **RTPC value store 0xA17280.** <br>- Per-RTPC hash entry, then a sorted 0x1C-byte array keyed on element +0x18, searched by binary search. <br>- It falls back to walking for a key-0 (global) element, then to the entry's default (+0x1C if +0x20), then to 0x9E6748 or 1.0 for param 0/7 cases. <br>- The exact key tuple (game object vs playing ID) was not settled. **For Cozmo it does not matter:** event_volume is only ever set per playing ID and robot_volume only per game object (gapA 5.2). | 0xA17280..0xA17420 | RECOVERABLE_GAP (precedence); STMG default reader NOT DONE |

#### 6. Bus and layer readers

| step | what the runtime reads | citation | classification |
|---|---|---|---|
| D6.1 | **Bus: Create 0x9C3620, SetInitialValues vt+0x130 = 0x9C3FFC.** Order: <br>- u32 id, u32 parent (0 → master slots 0x0108D9B0); <br>- **props (u8 n, ids, u32 values) with NO ranged bundle** (0x9C6420); <br>- u8 A (b0 → +0x46 b7, b1 → +0x47 b0); u8 B (b0 0x9F627C, b1 0x9F68D8, b2 +0x47 b6, b3 0x9C62AC); u16 maxInst (10 bits); u32 channelConfig; u8 C (b0 +0x40 \|= 0xE0000, b1 +0xCC b3); <br>- u32 recovery ms → samples via ·rate/1000 (0 if ≤ F), stored at +0x64; f32 maxDuck at +0x6C; <br>- u32 duck count × 18 bytes; <br>- FX 0x9C0D08: u8 n, [u8 bypass, n × {u8 slot, u32 id, u8 share, u8}], then **u32 mixer ID + u8** (vt+0xE0); <br>- u8 → +0x45 b5; <br>- u16 RTPC (varint param); <br>- states 0x9F6E3C; <br>- 4 bytes only if the BKHD flag is set (0x9F7364). | 0x9C3FFC..0x9C4308; 0x9C6420..0x9C6564; 0x9C0D08..0x9C0E14; 0x9F6E3C..0x9F6EEC; 0x9F7364..0x9F7388 | EXACT_SOURCE |
| D6.2 | **Compared with WwiseHierarchy.cs ReadBus (l.563-612).** <br>- The stack reads a ranged bundle and a "positioning" byte (+1 if nonzero) where the runtime reads A and B. <br>- It skips 15 bytes (= maxInst 2 + channelConfig 4 + C 1 + recovery 4 + maxDuck 4 = 15). <br>- **Its l.597 Skip(6) = mixer u32 + u8 + the +0x45 byte.** <br>- It is equivalent **only because A = B = 0 in all 15 shipped buses** (rtbus.py consumes every bus exactly, rem 0). | rtbus.py output | M6-001: EQUIVALENT on shipped data only |
| D6.3 | **0xA67558 is NOT the Layer reader.** It is called from the type-14 (Attenuation) handler 0x9B392C (0x9B39D4/0x9B39E8). **This corrects gapA 2.1.** <br>The real Layer container reader is Create 0x9D273C, SetInitialValues **0x9D24D4**: NodeBase 0x9F6EF8; u32 child count + IDs (vt+0x2C); u32 layer count × layer (create 0xA6D1DC(id), 0xA6D9EC, reader 0xA6DD54); u8 → +0x84. <br>The stack's ReadBlend order matches. All 6 shipped blend containers have 0 layers, so the per-layer body (0xA6DD54) is unexercised and unread. | 0x9B3AA0..0x9B3AD4; 0x9D24D4..0x9D2730 | order EXACT_SOURCE; layer body RECOVERABLE_GAP (not needed on shipped data) |

#### Existing records contradicted
- **gapB P6 / M6 A13 ("Hijack sees EQ+EQ+limiter output"):** the EQ and limiter plug-ins are unregistered and not shipped, so their slots are dropped (D2.3). The bus chain is Hijack only.
- **gapA 3.4 / 3.9 counts:** 443 step / 25 continuous, not 437/31. Continuous loop values are 0/1 only.
- **gapA 1.9:** 0xA663C8 is the Stop *Execute*. The zero-byte reader is 0xA60284.
- **gapA 2.1 / 2.9:** "Layer reader 0xA67558" is the Attenuation reader. The layer reader is 0x9D24D4.
- **M6 A18 "bus activity lifetime RECOVERABLE_GAP":** now settled (D2.6-D2.8). Any model that keeps one Hijack/stream per event or per engine lifetime is contradicted.

#### Records whose evidence is too weak
- **M6 A19/A22:** state transitions depend on EndOfEvent arriving before CloseAudioBuffer and a final partial (possibly zero-length) chunk (D2.8, D2.9, D3.5), plus Anki's queue-or-immediate dispatch (D3.6, unread).
- **M6-001 bus layout:** equivalent on shipped data only (D6.2).

#### Open questions for the manager
1. D3.6: ctx+0x38 for RobotAudioAnimation decides whether the EndOfEvent handling is synchronous or queued. It is Anki-side and readable.
2. D2.8 and D2.9: the one-frame tail plus the zero-length flush chunk changes how many AudioSilence or zero frames the original emits at an animation's end. This needs to go back into the M6 A-rows.
3. D1.3: rate and F are HARDWARE_ONLY, so every delay/frame-granularity result (D1.6, D1.10) is policy-dependent.

#### NOT DONE (all RECOVERABLE_GAP, in the binary)
- Continuous transitions (0xA09F04, the continuation list, cross-fade application; D4.3).
- The +0x74 sample-offset consumer (voice start).
- 0xA79F08 / 0x9AB8AC / 0x9ABEA4 internals (node stop, pending-action purge).
- The Seek node handler.
- RTPC key precedence (D5.6) and the STMG default reader.
- The writers of the Perform/bus-pass gating flags (D1.7, D2.8).
- The layer body 0xA6DD54 (unused by shipped data).
- SetState execute (not used by Cozmo.bnk).

## Appendix F: gap E (filters, mixer, pitch, callbacks), extractor report

**How this was done**
- Scratch folder: `...\scratchpad\extract\M6-gapE\`. It holds `r.py` (range print from gapB's `wwise_arm.txt`), `chain.py` (bank census of pitch/LPF/HPF), and `ia.py` (an `.init_array` plug-in registration emulator).
- Everything else reuses the gapA–D tools.
- All addresses are libcozmoEngine.so VAs. `pbi` is the playing instance. `ctx = pbi+0xC`, so ctx+0x30 = pbi+0x3C (Volume), ctx+0x38 = pbi+0x44 (Pitch), ctx+0x3C = pbi+0x48 (LPF) and ctx+0x40 = pbi+0x4C (HPF).
- Names are labels only.

**Status:** Items 1, 2, 3, 4 and 6 are done. Item 7 is partial. Item 5 is NOT DONE.

#### 0. `.init_array` plug-in registrations (manager-confirmed correction to gapD D2.3)

Every registration prepends a node {+0 next = old `g_pAKPluginList` head, +4 type, +8 company, +0xC id, +0x10 create, +0x14 params} through GOT 0x103FF7C (→ 0x108D9F4).

The scan emulated each ctor (`ia.py`). It found **22 registrations from 20 ARM ctors**, including four past 0x4DEFA0. In the table, full ID = (id<<16)|(company<<4)|type, and a name is given only where Init.bnk's INIT chunk names that full ID.

| ctor (init_array slot) | type | company | id | create / params | INIT name |
|---|---|---|---|---|---|
| 0x4DE744 (0x103E540) | 3 | 0 | 0x7F | 0xA7CCCC / 0xA802C4 | — |
| 0x4DE7B8 (…544) | 3 | 0 | 0x6A | 0xA83B18 / 0xA83AF0 | — |
| 0x4DE82C (…548) | 3 | 0 | 0x76 | 0xA84E0C / 0xA91410 | — |
| 0x4DE8A0 (…54C) | 2 | 0 | 0x65 | 0xA93DB8 / 0xA93C70 | SilenceGenerator |
| 0x4DE8A0 (second node) | 5 | 0 | 0x194 | same create/params | — |
| 0x4DE948 (…550) | 3 | 0 | 0x74 | 0xA94930 / 0xA94E58 | — |
| 0x4DE9BC (…554) | 2 | 0 | 0x77 | 0xA957EC / 0xA97C8C | — |
| 0x4DEA30 (…558) | 2 | 0 | 0x78 | 0xA9A7FC / 0xA9C764 | — |
| 0x4DEAA4 (…55C) | 3 | 0 | 0x6C | 0xAA0538 / 0xAA0808 | AkCompressor |
| 0x4DEB18 (…560) | 3 | 0 | 0x6E | 0xAA18F4 / 0xAA2280 | AkPeakLimiter |
| 0x4DEB8C (…564) | 3 | 0 | 0x69 | 0xAA257C / 0xAA3178 | ParametricEQ |
| 0x4DEC00 (…568) | 3 | 0 | 0x8A | 0xAA32BC / 0xAA5BC4 | AkHarmonizer |
| 0x4DEC74 (…56C) | 3 | 0 | 0x81 | 0xAA9AEC / 0xAA9DE8 | — |
| 0x4DECE8 (…570) | 3 | 0 | 0x7D | 0xAAA380 / 0xAAC1F8 | — |
| 0x4DED5C (…574) | 3 | 0 | 0x8B | 0xAAD314 / 0xAAD498 | — |
| 0x4DEDD0 (…578) | 2 | 0 | 0x66 | 0xAADDE4 / 0xAAD694 | ToneGen |
| 0x4DEDD0 (second node) | 5 | 0 | 0x193 | same create/params | — |
| 0x4DEE78 (…57C) | 1 (codec) | 0 | 4 | +0x18 = 0xAB0274, +0x1C = 0xAB023C; +0x10/+0x14 = 0 | — |
| 0x4DEF2C (…584) | 3 | 0 | 0x82 | 0xABAF48 / 0xABB6E8 | — |
| 0x4DEFA0 (…588) | 2 | 0 | 0x64 | 0xABB7C0 / 0xABB974 | Sine |
| 0x4DF014 (…58C) | 3 | 0 | 0x6D | 0xABCC84 / 0xABCF50 | AkExpander |
| 0x4DF088 (…590) | 3 | 0x100 | 0x6E | 0xABD570 / 0xABD008 | — |
| 0x4DF100 (…594) | 3 | 0x100 | 0x67 | 0xAD3D80 / 0xAD3E00 | — |
| 0x4DF178 (…598) | 2 | 0x105 | 0x1A0 | 0xADB000 / 0xADB618 | — |

- 0x4DEEEC (slot …580) is an atexit ctor, not a registration.
- Anki's Thumb registrations (Hijack 0x4DD90C, WavePortal 0x4DD97C; gapB R3) fall outside the range.
- **All 8 Audiokinetic plug-ins named in the INIT chunk are registered statically.**

**Consequence:** gapD D2.3 is wrong, and so are its D2.8 statements "EQ/limiter absent" and "Hijack sees the raw bus mix". gapC 3.1 / 4.x stand: the Hijack gets EQ → EQ → limiter output.

#### 1. Voice LPF/HPF stage

| step | what the original does | citation | classification |
|---|---|---|---|
| 1.1 | **Composition.** CalcEffectiveParams builds the voice LPF/HPF: <br>- pbi+0x9C = pbi+0x48 (node-chain LPF sum) + pbi+0x124 (randomizer LPF) <br>- pbi+0x48 = pbi+0x9C + pbi+0xA0 <br>- HPF likewise: pbi+0xA4 = +0x4C + +0x128, and pbi+0x4C = that + pbi+0xA8. <br>pbi+0xA0/+0xA8 are zeroed by the ctx ctor. No other direct-offset writer was found. | 0x9FFD14..0x9FFD74; 0x9BC9D8, 0x9BC9E0 | EXACT_SOURCE (writer scan covered direct offsets only) |
| 1.2 | **Per connection** (0xA4BC58). <br>- 2D path: conn+0x50 = ctx+0x3C (LPF), conn+0x58 = ctx+0x40 (HPF), conn+0x54 = conn+0x5C = 0. <br>- 3D path: 0xA5B9D0 sets conn+0x50/+0x58 = max(ctx LPF/HPF, attenuation LPF/HPF) (0xA5C740..0xA5C774). The 3D path is taken when ctx+0xDC bits0-1 ≠ 0, set by 0x9BE0B8. <br>- Outputs start at 100 (0x42C80000): A-LPF = min over connections of conn+0x50, B-LPF = min conn+0x54 (= 0), A-HPF = min conn+0x58, B-HPF = min conn+0x5C (= 0). | 0xA4BE80..0xA4BE9C, 0xA4BD80..0xA4BDAC, 0xA4BEC8..0xA4BF34 | EXACT_SOURCE |
| 1.3 | **Two filter objects per voice.** <br>- Filter A at voice+0x1C0: LPF state at voice+0x340, HPF state at voice+0x350. <br>- Filter B at voice+0x390: LPF at voice+0x510, HPF at voice+0x520. <br>- Targets: A-LPF = clamp(A-LPF, 0, 100) and A-HPF = clamp(A-HPF, 0, 100). B-LPF = clamp(max(B-LPF, pbi+0x68), 0, 100) and B-HPF = clamp(max(B-HPF, pbi+0x6C), 0, 100), where pbi+0x68/+0x6C are io+0x2C/+0x30 (gapC 1.2's output-bus props 0x1A/0x19). <br>- No shipped node has props 0x18/0x19/0x1A, so **B is bypassed for all shipped sounds**. | 0xA550D8..0xA551EC | EXACT_SOURCE |
| 1.4 | **Target setter.** Runs only when the target changes. It first sets current = current + (oldTarget−current)·0.125·steps (the instantaneous value), then target = new and dirty = 1. <br>State struct: +0 current, +4 target, +8 u16 step (0..8), +0xA s8 countdown, +0xB dirty, +0xC first, +0xD bypassed. <br>Ctor: current = target = 0, steps = 8, dirty = first = bypassed = 1. | 0xA55444..0xA55474, 0xA553D8..0xA55408; ctor 0xA764D4..0xA7654C | EXACT_SOURCE |
| 1.5 | **Order within the voice pass** (0xA44630): <br>1. source + pitch node (voice+0x100) <br>2. source-FX slots voice+0x370..0x37C <br>3. **filter A (0xA4C60C → 0xA766B8: LPF 0xA766F0, then HPF 0xA77480)** <br>4. 0xA56E00 (gated on pbi+0x34; not read) <br>5. 0xA548C0 <br>6. **aux-send mixes** (connections with conn+0x68 ≠ 0) <br>7. **then filter B**, before the first dry mix, then the dry mixes. <br>**So filter A shapes the Robot_Bus_1 signal. Filter B is dry-path only.** | 0xA446E0..0xA44700, 0xA447E4..0xA448B4, 0xA448C4..0xA44938 | EXACT_SOURCE |
| 1.6 | **Algorithm: a 2nd-order Butterworth biquad, not one-pole.** <br>- LPF: c = 1/tanf(π·fc/fs), b0 = 1/(1+√2c+c²), b1 = 2b0, b2 = b0, stored −a1 = −2(1−c²)b0, −a2 = −(c²−√2c+1)b0. <br>- HPF: c = tanf(π·fc/fs), b0 = 1/(c²+√2c+1), b1 = −2b0, b2 = b0, −a1 = −2b0(c²−1), −a2 = −(c²−√2c+1)b0. <br>- fs = mix rate global 0x105243C. tanf = 0x4AB038. <br>- Coefficients {b0, b1, b2, −a1, −a2} go at +0x80..+0x90. <br>- Aligned 4-sample blocks use a precomputed NEON block matrix at +0x00..+0x7C. Misaligned head and tail samples use the scalar DF-I form: y = b2·x2 + x·b0 + b1·x1 + (−a2)·y2 + (−a1)·y1. <br>- State is 16 bytes per channel at [+0xA0]. | 0xA7678C..0xA76844; HPF 0xA77500..0xA775D0; scalar 0xA76C50..0xA76C7C | EXACT_SOURCE (the block-matrix entries 0xA767BC..0xA769C4 are not transcribed, and bit-exactness vs sequential needs them) |
| 1.7 | **LPF value → cutoff (0xA7A3D8).** <br>- v < 30: fc = 7000 + (30−v)·433.333344 <br>- else fc = 16.7974434 · fastpow2 with bits = u32(1065353216 + (100−v)·1042939.94), mantissa polynomial 0.653043449 + m(0.0208057724 + 0.325189769m) <br>- then fc = min(fc, 0.45·rate). <br>**HPF (0xA7A4AC) = the same map applied to (100−v).** <br>Examples at 48 kHz: LPF 15 → 13500 Hz; 34 → ≈4949; 49 → ≈1360. HPF 15 → ≈61 Hz; 40 → ≈445. | 0xA7A3D8..0xA7A47C; 0xA7A4AC..0xA7A554 | EXACT_SOURCE |
| 1.8 | **Per-buffer update.** <br>- First apply: current = target, no ramp. <br>- A value ≤ 0.1 means bypassed. While bypassed, the last two input samples are still copied into the history (0xA76D40..0xA76DD0). <br>- On change: steps = 0. Then per chunk of N = floor(rate·128/48000) samples (global 0x105244C, 128 at 48 kHz), steps++ and new coefficients are computed from current + steps/8·(target−current). <br>- After 8 chunks, current = target. <br>- Ramping to ≤ 0.1 keeps the filter running for 4 more buffers (+0xA = 4), then it bypasses. | 0xA76728..0xA76A3C; 0xA76DE8..0xA77010; 0xA77180..0xA771C0; 0xA773FC..0xA77428; 0xA76BD8..0xA76C04; SetRate 0xA1C788..0xA1C794 | EXACT_SOURCE |
| 1.9 | **Shipped data.** <br>- Cozmo.bnk LPF props: 62050212 = 15 (top mixer), switch nodes 34/33/34/20/38/30, sounds 15/13. <br>- There are 25 HPF props (15..80), e.g. under 682998829. No LPF/HPF RTPCs and no ranged LPF/HPF in Cozmo.bnk. <br>- Bus props are only 27/28/29/32, so buses add no LPF/HPF. <br>So each voice's filter-A LPF is the sum of its chain (≥ 15 under 62050212). | chain.py; rtbus.py | EXACT_SOURCE (D) |

#### 2. Mixer 0xA45E9C and panning 0xA25FF8

| step | what the original does | citation | classification |
|---|---|---|---|
| 2.1 | **ConsumeBuffer 0xA4FBEC.** <br>- It zero-pads the voice buffer from uValidFrames to uMaxFrames (per channel) and sets valid = max. <br>- Gains: start = conn+0x10·conn+8·extra[0], end = conn+0x14·conn+0xC·extra[1]. <br>- It calls the mixer with prevMatrix = conn+0x24, nextMatrix = conn+0x20, s16 = bus+0x5C, n = bus+0x58. | 0xA4FC28..0xA4FC78, 0xA4FD10..0xA4FD6C | EXACT_SOURCE |
| 2.2 | **Mixer.** For each input channel i (non-LFE) and output channel j (non-LFE): start_ij = prev[i][j]·startGain, delta_ij = (next[i][j]·endGain − start_ij)·s16. <br>- Matrix row stride = ((outCh+3)/4)·16 bytes. <br>- LFE→LFE is mixed separately when both sides have bit15 set. | 0xA45F18..0xA45F90, 0xA45FD0..0xA4606C | EXACT_SOURCE |
| 2.3 | **Ramp shape (kernel 0xA46668)**: out[k] += in[k]·g(k), where g(k) = start + k·delta. <br>- It is computed as two 4-lane float vectors {start+{0,1,2,3}d} and {+4d}, each advanced by 8d per 8 samples. It works on 8-sample chunks. <br>- delta = 0 → constant gain. start = 0 and delta = 0 → skipped. | 0xA46668..0xA467B0 | EXACT_SOURCE |
| 2.4 | **Ramp length.** bus+0x58 = frames and bus+0x5C = 1/frames (float), from bus Init. So the ramp is linear over one bus frame and reaches `end` at k = n. | 0xA4F16C..0xA4F1AC | EXACT_SOURCE (that the Init argument equals the frame-size global was not traced: CreateMixBus caller arg [sp+0x74]) |
| 2.5 | **Prev/next bookkeeping per refresh.** <br>- The matrix pointers swap (0xA4BE34..0xA4BE58), and prev gains ← last next gains. <br>- New next gain conn+0xC = voice+0x1C·arg. <br>- **On the voice's first update, voice+0xCD bit3 is clear** (cleared at init 0xA54728, set at 0xA55294). Then prev matrix ← next and prev gains ← next gains, so the first frame is not ramped (0xA4BF5C..0xA4BFAC). <br>- conn+0x6C bit2 (the ctor arg = !(voice+0xCD bit0); bit0 is set to 1 at voice init 0xA54710) instead makes prev volume gain = 0 (a fade-in, 0xA4C0D8..0xA4C104). | as cited | EXACT_SOURCE for these instructions; the gating preamble 0xA4BCB0..0xA4BD74 (virtual/muted connections) is RECOVERABLE_GAP |
| 2.6 | **2D pan.** It runs only if conn+0x6C bit0 is set (set by the ctor 0xA6F924); otherwise next = prev. Inputs: panLR01 = (ctx+0xA8+100)·0.005, panFR01 = (ctx+0xAC+100)·0.005, center = ctx+0xB4... <br>More precisely: center = ctx+0xB0/100, and mode = ctx+0xB4. **The mode byte is only ever written as 0 or (v > 0 ? 1 : 0).** | 0xA597A4..0xA5986C; mode writers 0x9BE090..0x9BE0A4, 0x9BE2F4, 0x9BE5F4..0x9BE608, 0x9BE884, 0x9BE95C, 0x9BEBBC | EXACT_SOURCE |
| 2.7 | **Mono → mono.** <br>- Same config type 1 → the standard panner 0xA1F79C. Mono output with mode ≤ 1 → 0xA1FA50. Mono input → row zeroed, then **matrix[0][0] = 1.0**. <br>- Type-0 input into a type-1 output (anonymous → standard) → identity diagonal = 1.0. <br>**So the mono→mono gain is 1.0 regardless of pan, for both aux and dry.** Robot_Bus_1 and Cozmo_Robot are 0x4101. | 0xA2604C..0xA260B0, 0xA260DC..0xA26114; 0xA1F7E4..0xA1F80C, 0xA1FA50..0xA1FA64, 0xA1FE00..0xA1FE1C | EXACT_SOURCE (the voice-side channelConfig type for mono sources was not read; only ambisonic type 2 would differ) |

#### 3. Pitch consumer

| step | what the original does | citation | classification |
|---|---|---|---|
| 3.1 | **pbi+0x44 = Σ pitch cents.** It sums prop 2 plus the pitch RTPC over the node chain and buses (gapC 1.3), plus the randomizer pitch pbi+0x120. | 0x9FFD40, 0x9FFD6C..0x9FFD74 | EXACT_SOURCE |
| 3.2 | **Randomizer.** Each node's ranged bundle is summed into the ranges struct in the order Volume (id 0), Pitch (2), LPF (3), HPF (4) per node, from the leaf upward. Each is min + (float)((double)(LCG_hi>>1)/2147483647.0·(double)(max−min)); a zero range adds min with no draw. It is drawn once per voice (gapC 1.6). <br>Cozmo.bnk has 12 pitch ranges: 305544552, 507337776 (±150); 526693357, 777177819 (0..200); 621275296 (0..75); 103773320, 474102425 (−200..150); 107556628 (−150..200); 642872386 (actor-mixer, ±100, applies to all descendants and adds to child draws); 363020998, 535506817 (±150); 923584800 (−150..250). | 0x9EF57C..0x9EF8A4; 0x9F0B24..0x9F0B3C | EXACT_SOURCE |
| 3.3 | **MIDI key override (0xA1DAB8 / 0x9FAE18).** If the play's MIDI note byte ≠ 0xFF: walk up the parent chain to the first node with +0x47 bit2 (bank NodeBase bit b3) or the top node, and read prop 0x2D (root note; default from global 0x108DB20+0xB4). If that node's +0x47 bit3 (bank bit b4) is set, pitch += 100·(note − root). <br>**Shipped: no node has prop 0x2D, and no node has bank bits b3/b4** (only two Cozmo nodes have bits 0x24). So there is never a MIDI pitch shift. | 0xA1DAF0..0xA1DB40; 0x9FAE20..0x9FAEB4; bank census | EXACT_SOURCE (code + D) |
| 3.4 | **Consumer.** The pitch node (voice+0x100) GetBuffer calls source vt+0x20, which is **0xA5668C = return [src+0xC]+0x44 = pbi+0x44**. All source vtables have it at +0x20: 0x103D6C0, 0x103D740, 0x103D7C0, 0x103D840, 0x103D8C8, 0x103D950, 0x103DA28, 0x103E0B8, 0x103E138. <br>It then calls SetPitch(resampler node+8, cents, interp = ((pbi+0x1BE u16 & 0x380) == 0)). Those bits are cleared in the PBI ctor (0xA002B4..0xA002BC); their setters were not read. <br>This happens on every GetBuffer. | 0xA53150..0xA53180; 0xA5668C; vtable dumps | EXACT_SOURCE (that pitch-node +4 is the voice's source object is inferred from the vtables; its writer was not traced) |
| 3.5 | **SetPitch 0xA47384.** <br>- step = u32((double)(ratio·powf(2, cents/1200)·65536) + 0.5); 0 → (cents > 0 ? 0xFFFFFFFF : 1). <br>- First call, or after a ratio change (+0x57): current = target = step, rampPos = 0x400, immediate. <br>- Same cents: mode 2 if current ≠ target, else mode 0 (step == 0x10000) or mode 1. <br>- Changed cents: if mid-ramp, current += (target−current)·rampPos/1024; then rampPos = 0 and target = new step. If interp == 0 then current = target. | 0xA47384..0xA4751C | EXACT_SOURCE |
| 3.6 | **Ramp kernel (int16 mono mode 2, table 0x103C0B8[16] = 0xA4A2D8).** Per output sample: phase += (current·1024 + diff·(pos0 + inc·(k+1)))>>10, with inc = resampler+0x3C (= 48000/outRate per M6 0.11). It runs until pos reaches 0x400, i.e. about 21.3 ms at any rate. Interpolation: (x0<<16 + (x1−x0)·frac)·2⁻³¹. | 0xA4A35C..0xA4A43C | EXACT_SOURCE (the float and stereo mode-2 kernels 0xA4A958, 0xA4A59C, 0xA4AC04 were not read) |
| 3.7 | Pitch-node Init: the resampler is initialised with the source format (pbi+0x158) and rate voice+0xEC. | 0xA54A50..0xA54A78, 0xA5321C..0xA53240 | EXACT_SOURCE (the writer of voice+0xEC was not traced) |

#### 4. msPerFrame 0x1052444

It is struct 0x105243C +8. Writers: SetRate 0xA1C75C and SetFrame 0xA1C7D4 (gapD D1.2), both called only from platform init. The fields are:

| field | formula | static value (48 kHz, 1024 frames) |
|---|---|---|
| +8 msPerFrame | u32(float(frame) / (float(rate)/1000)), truncated | 21 |
| +0xC | u32(0.25·frame·1000/rate) | 5 |
| +0x10 (the LPF chunk N) | floor(rate·128/48000) | 128 |

gapC's "writer not found" is closed. EXACT_SOURCE for the logic; the values are HARDWARE_ONLY.

#### 6. Anki callback dispatch

| step | what the original does | citation | classification |
|---|---|---|---|
| 6.1 | **PostCozmoEvent.** If a callback is given: new 0x40-byte context, memclr, ctx+0x38 = 0, ctx+0 = 0xFF (all callback types enabled), then the std::function is set at ctx+8 via 0x59A4C2. No callback → ctx = NULL. | 0x599E6A..0x599EB8 | EXACT_SOURCE |
| 6.2 | **AK trampoline 0x8D8D40.** Because ctx+0x38 = 0, every EndOfEvent, Marker and Duration callback is **queued**: {ctx, info*} is pushed into the vector 0x108D0D4 under mutex 0x108D0D0 on the Wwise thread. For EndOfEvent it then calls 0x9A206C(playingID) → callback manager 0xA03540 (not read). | 0x8D8DC0..0x8D8E54, 0x8D8F26..0x8D8F30 | EXACT_SOURCE |
| 6.3 | **Drain 0x8D88CC.** Under the lock it swaps the vector out and unlocks, then calls HandleCallback(ctx, info) for each entry in order. HandleCallback filters on ctx+0 and, for Complete/Error, also runs ctx+0x30. | 0x8D88D4..0x8D8910; 0x8D4A3C..0x8D4A8A | EXACT_SOURCE |
| 6.4 | **Callers of the drain.** <br>- AudioEngineController::Update 0x8D2928: MusicConductor::UpdateTick, then ProcessAudioQueue/RenderAudio, **then drain**. It is reached from AudioMultiplexer::UpdateAudioController, which **CozmoEngine::Update calls at 0x4ED6C4 after the per-state work (e.g. RobotManager::UpdateAllRobots)**. <br>- Also AudioEngineController::FlushCallbackQueue, reached from RobotAudioClient::FlushAudioCallbackQueue (A23 abort), which dispatches immediately. <br>**So RobotAudioAnimation's EndOfEvent/Error handler runs on the engine thread at the end of a CozmoEngine tick, at least one tick after the audio-thread event.** | 0x8D2928..0x8D2942; 0x4ED6BE..0x4ED6C4; 0x599FE8 | EXACT_SOURCE |

#### 7. RTPC lookup precedence (partial)

| step | what the original does | citation | classification |
|---|---|---|---|
| 7.1 | **Voice RTPC key (pbi+0x14):** {gameObj, pbi+0x18 = playing ID (pbi+0x140), +0x1C = MIDI target (0 unless MIDI), +0x20 channel byte, +0x24 note byte (0xFF), +0x28 = the PBI}. <br>The set key (0xA1404C) is {gameObj, playingID, 0, FF, FF, 0}. | 0xA003A4..0xA00400; 0x9BC924..0x9BC94C; 0xA14054..0xA14088 | EXACT_SOURCE |
| 7.2 | **0xA11590** finds the subscription by (target, param) in its object's +0x10/+0x14 hash. acc = 2 → product (0xA17724), otherwise sum (0xA17878). Each curve entry's value comes from 0xA17280(same object +0/+4 hash, rtpcID, param = sub+4, type = sub+0x24, key). | 0xA11590..0xA11618; 0xA178E8..0xA179AC | EXACT_SOURCE |
| 7.3 | **0xA17280 precedence:** <br>1. exact game object (binary search, element 0x1C, key +0x18) <br>2. within that, exact playing ID; otherwise playing ID 0; otherwise the game-object element's own value <br>3. game object not found → search the game-object-0 element <br>4. otherwise the tree-root value (entry+0x1C, valid +0x20). <br>RTPC ID not in the store: type == 1 → default table 0x9E6748([0x108D8DC]); otherwise param 0 or 7 → 1.0 with a skip flag (**the curve adds nothing**); otherwise not found. <br>When not found while the entry exists, 0xA17878 evaluates the curve at x = [entry+8]. That field is written only as 0 at entry creation (0xA13B1C) among the set-path writes. | 0xA17288..0xA17428, 0xA1742C..0xA1771C; 0xA17924..0xA179B0 | lookup EXACT_SOURCE; the tree insertion 0xA152C4 and entry+8 semantics are RECOVERABLE_GAP |
| 7.4 | **Answers.** <br>- event_volume set by playing ID reaches that voice's key {7, P}: **it applies**. <br>- Bus RTPCs are evaluated with key {0, 0, 0, FF, FF, 0} (0x9C3A48..0x9C3A78, via 0x9C3BF4..0x9C3C08). That path goes straight to the tree root (0xA17558 → 0xA173CC), so **values set on game object 7 are not visible** (assuming 0xA152C4 files them under the game-object-7 child). | as cited | partial (see Q3) |

The STMG default reader 0x9E6748 was NOT read.

#### Existing records contradicted
- **gapD D2.3 / D2.8:** EQ and limiter are registered (§0).
- **gapC 1.12 and gapB P2:** the pitch consumer is now traced (§3).
- **gapC 5.2 "msPerFrame writer not found":** it is SetRate/SetFrame (§4).
- **gapA 5.4 "value → default table → 0":** the default table is used only for subscription type 1. For node targets with the RTPC never set, param 0/7 contributes nothing (§7.3).
- **Any model that uses a one-pole voice LPF, or applies the voice LPF only to the dry path, is contradicted** (§1.5–1.6).

#### Records whose evidence is too weak for their status
- **M6 A16 / gapC 4.8:** the Hijack input also passes voice filter A. For all 1862+ sounds under 62050212 that means at least LPF 15 → 13.5 kHz, plus HPF where set.
- **Any first-frame fade-in model:** see 2.5. Its gating preamble is unread.

#### Open questions
1. Port the NEON block formulation of the biquad (0xA767BC..0xA769C4) for bit-exactness, or accept sequential DF-I as equivalent? This is a manager decision.
2. The voice-side mono channelConfig type (pan result 1.0 unless ambisonic) is read nowhere. It is cheap to confirm from the source-format fill of pbi+0x158.
3. **Risk:** once any event_volume value exists, a voice on the 62050212/682998829 subtree without its own playing-ID value may evaluate the curve at x = [entry+8] = 0 → −764 dB (silent). Read 0xA152C4, 0xA157A4 and the writers of entry+8/+0x1C before relying on either outcome.

#### NOT DONE (RECOVERABLE_GAP)
- **Item 5, continuous containers:** only the entry to 0xA09F04 was skimmed (selection via 0xA09C40, a 32-entry history at +0x28/+0x2C, launch through 0x9F12E0). Transition modes 1/2/4/5, xfade scheduling, the continuation list and loop semantics were not read.
- The 0xA4BC58 gating preamble; the voice+0x380 stage 0xA56A7C (gated on pbi+0x34); the pitch-node +4 writer; the writer of voice+0xEC.
- The float and stereo mode-2 pitch kernels; the biquad block-matrix derivation.
- The RTPC tree insert 0xA152C4 and the entry+8/+0x1C semantics; the STMG default reader 0x9E6748.
- The CreateMixBus frames argument.

## Appendix G: gap F (RTPC store, continuous), extractor report

**Setup.** Scratch folder is `...\scratchpad\extract\M6-gapF\`. New tools there: `r.py` (copied from gapE), `stmg.py` (parses the STMG chunk the way 0x9B0B14 reads it), `fmt.py` (census of the embedded media `fmt ` chunks), `fstart.py` and `gotuse.py`. I also reused gapA's `tbl.py`/`blx.py`, gapB's `q.py`/`base.py`/`wwise_arm.txt` and gapC's `nodes.py`/`bnk.py`.

All addresses are VAs in libcozmoEngine.so. Nothing under the repo was written.

**Status:** Item 1 is done. Item 2 is done except the pieces marked RECOVERABLE_GAP. Item 3 is done.

**Headline answers**
- **Item 1: no voice goes silent.** A voice with no value for its own playing ID falls through the lookup to the RTPC's STMG default, which is 1.0, so event_volume gives 0 dB. The "entry+8 = 0 → −764 dB" risk is ruled out. robot_volume set on game object 7 never reaches the bus evaluation.
- **Item 2, transition modes:** 1 is a linear cross-fade, 2 is a sine/cosine cross-fade, 4 is sample-accurate chaining and 5 is a fixed-period trigger. Loop 0 means infinite and loop 1 means one pass.
- **Item 3:** mono pan gain is 1.0 for all shipped mono media, the CreateMixBus frames argument is the frame-size global, and the gating preamble cannot stop a normal voice's aux send.

---

#### 1. RTPC value store, defaults, and the gapE Q3 risk

| step | what the original does | citation | classification |
|---|---|---|---|
| 1.1 | **Entry layout** (0x4C bytes, hash bucket = id % count):<br>- +0 id, +4 next<br>- **+8 default value** (f32)<br>- +0xC ramp type, +0x10 ramp up, +0x14 ramp down<br>- +0x18 value tree (vtbl 0x101C2D8), with root value +0x1C and root-valid +0x20<br>- +0x24/+0x28/+0x2C sorted game-object array (element 0x1C bytes, key at +0x18)<br>A game-object element holds +4 value, +8 valid, +0xC/+0x10/+0x14 a playing-ID sub-array of the same 0x1C shape. | get-or-create 0xA0F07C..0xA0F130; also created at 0xA13AD8..0xA13B54 and 0xA144E8..0xA14564 (all zero +8) | EXACT_SOURCE |
| 1.2 | **How STMG defaults enter the store.** STMG reader 0x9B0B14 reads, in order:<br>- f32 volume threshold, then 0x9A080C(thr, 2)<br>- u16 max voices<br>- state groups<br>- switch→RTPC groups<br>- then `u32 n × {u32 id, f32 value, u32 rampType, f32 up, f32 down, u8 builtin}`<br>For each param: **0xA0F594(mgr, id, value) → entry = 0xA0F07C(id) (get-or-create); `str r4,[r0,#8]`**. 0xA0F5AC then stores the ramp fields at +0xC/+0x10/+0x14. `mgr` = [GOT 0x1040088] = 0x108D908, the same object SetRTPC uses. STMG is dispatched from 0x9B7864 (`cmp r3,sl`, sl = 'STMG'). | 0x9B0EB0..0x9B0FAC; 0xA0F594..0xA0F5A8; 0xA0F5AC..0xA0F5D0; 0x9B7864/0x9B7AC8 | EXACT_SOURCE |
| 1.3 | **Shipped defaults** (Init.bnk STMG at 0x101; 37 params; parse ends 8 bytes short, which the reader consumes as later counts):<br>- **event_volume 0xD2687048 = 1.0**, ramp 0<br>- **robot_volume 0x637C1240 = 1.0**, ramp 0<br>The only other writers of entry+8 are the zero at creation, 0xA0F47C (id 0x83 ← 64.0, init) and 0xA0F660 (no xref, no pointer, no symbol: dead). | `stmg.py`; writer scan of `str …,[rX,#8]` over 0xA0E000..0xA18600 | EXACT_SOURCE (D+L) |
| 1.4 | **Set path** (0xA1404C → 0xA13A88 → 0xA137D8 → 0xA12CA0), key {gameObj, playingID, 0, FF, FF, 0}.<br>- **New game-object element:** created with valid = 0 (0xA12F2C..0xA12F40 or 0xA132E8..0xA13308).<br>- **playingID ≠ 0:** a playing-ID sub-element is inserted (0xA12FE8..0xA1304C). 0xA18E38's leaf path (0xA192F0..0xA1932C) sets **only that sub-element** valid = 1.<br>- **playingID = 0 and all other key fields empty:** the game-object element itself is marked valid (0xA13218..0xA1323C).<br>- **gameObj = 0 and empty key:** the root is marked valid (0xA13200..0xA13210).<br>- **Old value for the transition** = slot, else nearest less-specific valid value, **else entry+8** (0xA13924..0xA1392C; 0xA12E70..0xA12E78). | as cited | EXACT_SOURCE |
| 1.5 | **Ramp** (0xA137D8). Duration = max(caller time, entry ramp):<br>- **type 1:** \|Δ\|/rate·1000 ms (up +0x10 / down +0x14)<br>- **type 2:** (up or down)·1000 ms<br>Time > 0 → transition 0xA0E5E4; otherwise immediate 0xA12CA0. event_volume and robot_volume have ramp 0, and Anki passes time 0 (gapA 5.2), so both **apply immediately**. | 0xA13930..0xA13A60 | EXACT_SOURCE |
| 1.6 | **Lookup 0xA17280**, entry found, key {7, Q, 0, FF, FF, PBI_Q}:<br>- the game-object-7 element is found; Q is absent (0xA175A8)<br>- playing ID is set to 0 and the ID-0 child is looked up (0xA174E8, 0xA15CD0); it is absent<br>- element 7 has valid = 0 (1.4), so fall to root (0xA1746C → 0xA173CC); root is invalid<br>- **return 0**, and the caller evaluates the curve at **x = [entry+8]** (0xA17924..0xA179A0)<br>**So a different voice Q under the same actor-mixer evaluates event_volume at the STMG default 1.0 → 20·log10(sin(π/2)) = 0 dB, not −764 dB.** | 0xA1742C..0xA17554, 0xA173CC..0xA173F4; 0xA17924..0xA179B0 | EXACT_SOURCE |
| 1.7 | **Voice P itself** (key {7, P, 0, FF, FF, PBI_P}):<br>- element 7 → sub-element P (0xA175C8) → key+8 = 0<br>- 0xA0DC04(key+8) is true because key+0x14 = PBI ≠ 0<br>- the empty sub-array (0xA17710) → 0xA17530 → **P's value is used**. | 0xA17514..0xA17554, 0xA0DC04..0xA0DC2C | EXACT_SOURCE |
| 1.8 | **Nothing set at all:** the entry exists (STMG), so the same fallback gives x = 1.0. The branches "RTPC not in store → type 1 → 0x9E6748" and "param 0/7 → 1.0 + skip" are **unreachable for any RTPC listed in STMG**, because STMG creates every such entry at Init.bnk load. | 0xA172EC..0xA17428 plus 1.2 | EXACT_SOURCE |
| 1.9 | **robot_volume set on game object 7** (key {7, 0, 0, FF, FF, 0}):<br>- this marks only element 7 valid (1.4)<br>- bus RTPCs are evaluated with key {0, 0, 0, FF, FF, 0} (0x9C3A48..0x9C3A78, then 0xA11590 at 0x9C3C08)<br>- 0xA17280's gameObj = 0 with an empty key goes straight to root (0xA17558..0xA17594 → 0xA173CC); root is invalid<br>- **x = entry+8 = 1.0 → the robot_volume curve gives 0 dB whatever value is set.**<br>This is the bus path at 0x9C3A48 only; I did not look for any other bus-RTPC evaluation. | as cited | EXACT_SOURCE |
| 1.10 | **0x9E6748 is not an STMG default reader.** It looks the id up in a different manager ([GOT 0x104006C] = 0x108D8E0, hash +0x90, key at node+8, mutex +0x8C), increments a refcount at +0xC, evaluates 0x9DA360 and releases through vfunc +0xC. It is used only for subscription type 1. **All shipped event_volume/robot_volume subscriptions are type 0** (`nodes.py`, `bus.py`). | 0x9E6748..0x9E6808 | EXACT_SOURCE (the manager's identity is unlabelled) |

#### 2. Continuous random/sequence containers

**Shipped data:** 25 continuous containers, all in Cozmo.bnk, all with bits 0x1A:
- **mode 1 (15):** 9 sequences with n=2, loop 1, 100 ms (under 201436547, 399004754, 693270200); 6 random with loop 0, 1000 ms.
- **mode 2 (7):** loop 0, 10–200 ms, including sequence 460890181.
- **mode 4 (2):** 196431345 (random, loop 0), 725225627 (sequence n=2, loop 1), both 1000 ms.
- **mode 5 (1):** 777177819 (n=1, loop 1, 90 ms).
- **Bank bits:** bit1 → +0x91 b4 is set on all 25.

| step | what the original does | citation | classification |
|---|---|---|---|
| 2.1 | **Entry.** PlayInternal 0xA0AFDC: when continuous, it creates ContParams on the stack with a continuation list 0xA69F78 and sets params+0x78. Mode 5 → 0xA09F04; otherwise 0xA0ABC4. | 0xA0B000..0xA0B0E8 | EXACT_SOURCE |
| 2.2 | **Loop info per continuation item** (0xA091CC):<br>- count s16 = 1<br>- flags b0 = (loop ≠ 1), b1 = (loop == 0)<br>- b0 = 1 and b1 = 0 → count = loop + loopMin + round(rand·(loopMax−loopMin)), minimum 1<br>Shipped loopMin/loopMax = 0. **So loop 0 = infinite and loop 1 = a single pass.** | 0xA09230..0xA09264, 0xA09350..0xA093F0 | EXACT_SOURCE |
| 2.3 | **Selection for the next item** (0xA09C40, "next continuous"):<br>- playlist length 0 → null<br>- **length 1:** count ≤ 0 → null (end); otherwise count−− unless infinite, and return item 0<br>- random → 0xA08A44 with loop info<br>- sequence → 0xA0856C with loop info<br>- flag 0 → null (end)<br>**Random:** at a cycle reset (counter +0xE == 0), not enabled → end; infinite → reset; otherwise count−−, and 0 → end (0xA08CA0..0xA08CD4).<br>**Sequence at the wrap:**<br>- ping-pong bit +0x91 b5 → reverse<br>- otherwise idx = 0, then: not enabled → end; infinite → continue; otherwise count−−, and 0 → end (0xA08668..0xA08688, 0xA085E4..0xA08630)<br>**Selection state:** with +0x91 b4 set (all shipped), each play allocates a fresh state and never saves it back (0xA0AC40..0xA0AC54, 0xA0AEB0..0xA0AEF8, 0xA0AF5C..0xA0AFA0; save-back 0xA095B4 is skipped at 0xA0ADBC..0xA0ADC8). A fresh sequence takes its index from the global state +0x78 when present, else −1, so it starts at item 0. | 0xA09C40..0xA09EF4 | EXACT_SOURCE (that +0x78 stays null for these containers is inferred: its only creators, 0xA09698/0xA099BC per gapA 3.4, are not on the b4-set path) |
| 2.4 | **When the next item is chosen and scheduled (modes 1, 2 and 4).**<br>- When the current voice's source starts, 0xA56478 posts the notification "state 3, estimated length" through 0xA01818 → queue 0xA38600. Length = source vfunc+0x34 duration ÷ 2^(pbi+0x44 cents/1200) (0xA564BC..0xA564E4); if src+0x10 bit0 is clear, length = 0.<br>- The LEngine drains the queue at 0xA38420..0xA38480 → PBI 0xA0188C(state 3): +0x1BC \|= 2, then vfunc+0x38, then vfunc+0x18(length).<br>- ContinuousPBI (vtable 0x103D3B0) overrides +0x38 = 0xA6A2DC and +0x18 = 0xA6A580. Both first call **PrepareNextToPlay 0xA6A07C, which selects the next item right then, at the current voice's start.**<br>- PrepareNextToPlay stores the next ID at +0x250 and mode = container +0x90 & 0xF at +0x254 (0xA0776C). For modes 1..3 it stores xfade = 0xA0785C at +0x24C: prop +0x7C + min + rand·(max−min), plus RTPC param 0xF ×1000, clamped ≥ 0, in ms. | 0xA56478..0xA564E4; 0xA01818..0xA0183C; 0xA0188C..0xA018EC; 0xA6A07C..0xA6A2D8; 0xA0785C..0xA07A00 | EXACT_SOURCE |
| 2.5 | **Modes 1 and 2: start time of the next item** (0xA6A580):<br>- only when length ≥ 50 ms and a next item exists<br>- xfade = min(+0x24C, length/2)<br>- a PlayAndContinue action (type 0x503, 0xA6361C) gets prev-PBI = this PBI and time = (int)xfade (0xA63A8C)<br>- delay = round-half-away((length − xfade)·rate/1000) samples (rate = [GOT 0x104006C… 0xFFFFFDDC]); the action is queued with that delay (vfunc+0x20, 0x9AA0FC)<br>**So the next item starts xfade ms before the current voice's estimated end, measured from the current voice's start.**<br>If length < 50 ms or unknown, no action is scheduled; at the PBI end 0xA6ACC0 launches the next item with no delay (0xA6AD64..0xA6AF1C). | 0xA6A5D8..0xA6A760; 0xA6ACC0..0xA6AF1C | EXACT_SOURCE (the delay resolution inside the action manager 0x9AA0FC is RECOVERABLE_GAP) |
| 2.6 | **Cross-fade gains.** PlayAndContinue Execute 0xA62ED4 builds params {time = +0x88, curve = 4, or 1 when the previous PBI's mode is 2}:<br>- **previous PBI:** 0xA01280 → 0xA010E0 builds a transition on +0x168 (play/stop ratio) toward 0.0<br>- **next PBI:** PlayInternal with the same params → 0xA0067C sets +0x168 = 0 and builds a transition to 1.0<br>- **Mirror rule** (0xA35F14): when start ≥ target and the mirror flag is set (it is for both), curve c → 8−c except 3 and 5. So linear (4) stays linear, and Sine 1 → 7<br>- **Evaluation** (0xA35998): value = start + f(t)(target−start); curve 1 = sin(πt/2), 4 = t, 7 = target + cos(πt/2)(start−target)<br>- **Mode 1:** in = t, out = 1−t (linear amplitude). **Mode 2:** in = sin(πt/2), out = cos(πt/2) (constant power). Both use the polynomial approximations at 0xA35B58 and 0xA35C50<br>- Duration = ceil(ms / [0x1052444] msPerFrame) frames (0xA35E60..0xA35E9C); t = (now−start)/frames<br>- **Application:** pbi+0x40 = … × ratio(+0x168) × pause(+0x16C), clamped ≥ 0 (0x9FF3C4..0x9FF400; 0x9FFDC4..0x9FFE0C). voice+0x1C = ctx+0x34 (= pbi+0x40) × 10^(0.05·dB) (0xA4B668..0xA4B674), then ramped linearly per buffer (gapE 2.3–2.5). | 0xA62F10..0xA62F30, 0xA631E4..0xA63204; 0xA01384..0xA0139C; 0xA010E0..0xA01270; 0xA0077C..0xA00824; 0xA35998..0xA35CE4; 0xA35D44..0xA35F24 | EXACT_SOURCE (that `now` is the audio-frame counter is inferred) |
| 2.7 | **Mode 4 (sample-accurate).** 0xA6A2DC runs at the same start notification. When a next item exists it builds and queues the PlayAndContinue immediately with no delay:<br>- action+0x8C = 1 if +0x1BC bit7 (0xA63ABC)<br>- action+0x90 = pbi+0x1C8 (0xA63AA0)<br>At the PBI end, mode 4 takes a separate branch (0xA6AF14 → 0xA6B0D8). **How the new source is chained to start after the last sample of the current one is not read.** | 0xA6A2DC..0xA6A4FC; 0xA6AF04..0xA6AF18 | RECOVERABLE_GAP (read 0xA6B0D8.., and how PBI init consumes action +0x8C/+0x90 through 0xA62ED4 [sp+0xAC]/[sp+0xB8]) |
| 2.8 | **Mode 5 (trigger rate)** 0xA09F04:<br>- first call selects item A; later calls take the pre-selected ID from params+0x120<br>- it then selects the **next** item B ahead of time<br>- **B null → play A and stop scheduling** (0xA0A2B4..0xA0A3B0)<br>- otherwise it plays A as a plain (non-continuous) play, sets +0x120 = B, and schedules re-entry through 0x9EDEB8 after **max(transitionTime/1000 s, 0.022 s** static [0x1052438]) + params+0x74/rate<br>**The period is independent of A's duration and there is no cross-fade.**<br>Shipped 777177819 (n=1, loop 1): the first selection consumes count 1 → 0, the look-ahead returns null, **so it plays once.** | 0xA09F04..0xA0A1C4; 0xA0A168..0xA0A1C0; 0x9EDEB8..0x9EDFE8 | EXACT_SOURCE (the tail of 0x9EDEB8 after 0x9EE000 is not read) |
| 2.9 | **When the continuous play ends.** When 0xA09C40 returns null, PrepareNextToPlay pops the item, clears +0x1FC and sets +0x254 bit6 (0xA6A184..0xA6A208). No next item is scheduled, so the last PBI plays to its end and 0xA6ACC0 finds next = 0 (0xA6ACEC..0xA6AD58). **Loop 0 containers therefore never end on their own.**<br>Example: a shipped n=2, loop 1 sequence plays item 0, then item 1 overlapping by the xfade, then ends when item 1 ends.<br>The EndOfEvent trigger itself (playing-ID count reaching 0, 0xA04F54 / callback manager) is not read. PlayAndContinue takes a reference at +0x12C (0xA6A4D4..0xA6A4EC). | as cited | EXACT_SOURCE for the end of scheduling; the EndOfEvent count is RECOVERABLE_GAP |

#### 3. Small checks

| step | what the original does | citation | classification |
|---|---|---|---|
| 3.1 | **Mono channel config.**<br>- Codec sources copy the fmt chunk: +4 rate → pbi+0x158, and the u32 at **fmt+0x14 → pbi+0x15C..0x15F (AkChannelConfig)** (0xA72E14..0xA72E88, 0xA73B40..0xA73BA4, 0xA75C4C..0xA75CAC)<br>- The voice builds its config from pbi+0x158..+0x163 (0xA538B8, 0xA53AAC; for one branch the source vfunc+0x44 result replaces it at 0xA53AB0..0xA53AC4, not read) into voice+0xF0 (0xA53CA0..0xA53CE0)<br>- voice+0xF0 is the pan input (0xA556F4 → 0xA4BC58 → 0xA5975C → 0xA25FF8)<br>**Shipped media:** every mono item is **0x4101** (1 ch, type 1 standard, FC): Cozmo.bnk 1750+8 Vorbis and 184+36 ADPCM. **So gapE 2.7's mono→mono gain of 1.0 holds.**<br>Cozmo.bnk also holds **18 stereo items (0x3102)** and 46 items without a fmt chunk. These go down a different panner row; whether any is routed to Robot_Bus_1 was not checked. | as cited; `fmt.py` | EXACT_SOURCE (D+L), except the source vfunc+0x44 branch |
| 3.2 | **CreateMixBus frames argument = the frame-size global.** Bus Init 0xA4F0EC takes frames = caller [sp+0].<br>- Caller 0xA41C84: `ldrh sb,[GOT 0x1040058 → 0x1052440]` (0xA41C94..0xA41CA0), then `str sb,[sp]` (0xA41D0C, 0xA41D90)<br>- Caller 0xA42210: `ldrh` from the same GOT 0x1040058 (0xA4230C..0xA42320), then [sp] (0xA42390, 0xA42434)<br>Static value 0x400. | as cited | EXACT_SOURCE (value HARDWARE_ONLY per gapE §4) |
| 3.3 | **0xA4BC58 gating preamble.** Inputs:<br>- A = voice vfunc+0x3C = (pbi+0x1BE & 0x14) ≠ 0; the virtual-voice bits are set at 0xA0212C and 0xA02684<br>- B = voice+0xCD bit3 (first update done)<br>- per-connection bit1 = below volume threshold: conn+0x60 ≤ [0x1052454] (0xA4B068..0xA4B0A4); the threshold is set by the STMG reader through 0x9A080C (Init.bnk −80 dB; the dB→linear store is not read)<br>- per-connection bit2 = the first-frame/fade flag<br>The per-connection gain/pan loop (0xA4BD74..) is skipped only when:<br>&nbsp;&nbsp;(a) B = 0 and every connection is below threshold,<br>&nbsp;&nbsp;(b) B = 0 and A ≠ 0,<br>&nbsp;&nbsp;(c) B = 1 with every connection already carrying bit2, when A ≠ 0 or every connection is below threshold.<br>A skip leaves the previous gains in place; it does not zero them.<br>**For a normal (non-virtual) voice whose send is above −80 dB, the loop always runs, so the preamble cannot stop the aux send.** On the first update (A = 0, B = 0, some connection above threshold) it sets bit2 = arg [sp+0x50] on every connection and then computes (0xA4C03C..0xA4C034). | 0xA4BCB0..0xA4BD74, 0xA4BFEC..0xA4C084; 0xA55E90..0xA55EA8 | EXACT_SOURCE for the gate; the meaning of the output byte [sp+0x54] for the caller is not read |

#### Existing records contradicted
- **gapE 7.3 / Q3.** "entry+8 is written only as 0" is wrong: STMG writes the default there (1.2). The −764 dB risk does not occur (1.6).
- **gapE 7.3.** 0x9E6748 is not the STMG default reader (1.10).
- **gapA 5.4.** "the default table is filled from STMG" is contradicted. STMG defaults live in entry+8 (1.2). The chain is value → entry+8 → curve, not value → table → 0.
- **gapD D5.6** (last paragraph). Its fallback ordering "…then 0x9E6748 or 1.0" applies only to RTPCs that STMG does not list.
- **gapE 2.4 caveat and gapE 2.7 caveat:** both closed (3.2, 3.1).

#### Records whose evidence is too weak
- **Any stack model of continuous containers** that starts the next item at the voice's end for modes 1/2, that uses an equal-power fade-out equal to 1−sin, or that re-selects the next item at transition time instead of at the current voice's start, is contradicted (2.4–2.6).
- **Any treatment of loop 1 as "loop once more" is wrong.** It is a single pass.

#### Open questions / remaining RECOVERABLE_GAPs
1. How mode 4 chains the next source sample-accurately (2.7).
2. The action manager's delay resolution for PlayAndContinue: whether it is sample offset or frame (0x9AA0FC and the pending-action tick).
3. The EndOfEvent counting for continuous plays (0xA04F54 / callback manager 0xA03540).
4. Duration availability: the meaning of src+0x10 bit0 (0xA56484). When it is clear, length is 0 and there is no scheduled cross-fade; the next item starts at the current one's end.
5. The 18 stereo media: whether any sits on a robot-routed path. If one does, the mono→mono gain of 1.0 does not apply to it.

## Appendix H: gap G (chaining, delay, stereo, Vorbis internals), extractor report

**Setup.** Scratch folder is `...\scratchpad\extract\M6-gapG\`. New files there:
- `stereo.py`: stereo media routing
- `cblib.py`: census of the embedded codebook library
- `setup.py`: parses every Vorbis setup packet
- `vorbfmt.py`: census of the fmt extension fields
- listings `cb.txt`, `dec.txt`, `res.txt`, `vss.txt`

I reused gapF `r.py`, gapB `base.py`/`q.py`/`wwise_arm.txt` and gapC `nodes.py`/`bnk.py`. All addresses are VAs in libcozmoEngine.so. Nothing under the repo was written.

**Status:** All six items are answered. The gaps still open are listed at the end.

**Headline answers**
- **Mode 4:** the next item is chained into the same voice as a pending source and starts on the output sample right after the current source's last sample, in the same buffer. There is no overlap, no fade and no delay.
- **PlayAndContinue delay:** it is frame-quantised, but the engine executes it one lookahead frame early and carries F + (delay mod F) samples as the voice-start offset. The first sample is then zero-padded to that exact position.
- **EndOfEvent** fires in the Term of the last item's PBI. A pending PlayAndContinue holds the playing ID's action count, so the event cannot end between items.
- **src+0x10 bit0** is a "StartStream succeeded" latch, not a "duration known" flag.
- **All 18 stereo media are robot-routed.** Into mono Robot_Bus_1 they get 0.70710677 per channel. Eleven of them are the leaves of the mode-4 containers.
- **The Vorbis decoder is a Tremor-lowmem fork** with three deviations. Only one of them (the dropped sequence flag) could matter, and the shipped data never triggers any of the three.

#### 1. Mode-4 (sample-accurate) chaining

| step | what the original does | citation | classification |
|---|---|---|---|
| 1.1 | **Scheduling at the current source's start notification.** 0xA6A2DC runs PrepareNextToPlay (vfunc+0x60) first. If mode == 4 and a next item exists, it creates a 0x503 PlayAndContinue. The ctor zeroes +0x84 (prev PBI), +0x88 (xfade), +0x8C and +0x90. The mode-4 path never calls 0xA63A8C and never calls SetDelay:<br>- no previous PBI<br>- xfade 0<br>- delay 0<br>It sets +0x8C = 1 if pbi+0x1BC bit7, and **+0x90 = pbi+0x1C8**. It enqueues through 0x9AA0FC; frames = 0, so it **executes immediately**. Finally +0x1BC \|= 4 and +0x1FC and +0x250 are cleared. | 0xA6A320..0xA6A338; 0xA636A8..0xA636B4; 0xA6A488..0xA6A504; 0xA6A494..0xA6A4F8; 0x9AA298..0x9AA2B8; 0xA6A430..0xA6A458 | EXACT_SOURCE |
| 1.2 | **Execute 0xA62ED4.** Because +0x84 = 0, there is no fade-out of the previous PBI (it skips 0xA01280). The transition is {time = +0x88 = 0, curve 4}. It sets:<br>- params+0x70 = +0x8C<br>- params+0x74 = pending+0xC<br>- params+0x7C = +0x90 | 0xA62EDC..0xA62F0C; 0xA63104..0xA63158 | EXACT_SOURCE |
| 1.3 | **PBI ctor 0xA000E8.**<br>- pbi+0x1D8 = params+0x74 (the start offset in samples).<br>- If params+0x7C ≠ 0: **pbi+0x1C8 = params+0x7C** (the chain ID) and +0x1BE \|= 8.<br>- Otherwise pbi+0x1C8 = global 0x1052434++. | 0xA002D8/0xA002EC; 0xA00308..0xA0041C | EXACT_SOURCE |
| 1.4 | **PBI Play 0xA0067C.** With transition time 0 there is no fade-in (0xA006A4 path). When flag r2 is set, or +0x1BA&7 == 1, it sets +0x1BC \|= 0x80 and queues start-list type 1 (0x9D3558); otherwise type 0. 0xA4304C does not read the type. | 0xA00684..0xA006F4; 0x9D3558..0x9D35E0 | EXACT_SOURCE. That params+0x70 is the LEngine cmd+0x70 behind r2 (0xA3806C..0xA38094) is not traced: RECOVERABLE_GAP |
| 1.5 | **Voice attach 0xA4304C.** If pbi+0x1C8 ≠ 0, it walks the live voices (list 0x108DF54+0x14, next pointer voice+0xD0) and compares [voice+8]+0x1BC. voice+8 is the PBI+0xC interface, so this reads that PBI's +0x1C8.<br>- **Match:** AddSrc(voice, pbi, **0**), then 0xA01878, return 5.<br>- **No match:** a new 0x540-byte voice and AddSrc(…, 1). | 0xA43058..0xA43128; 0xA43078..0xA430B8 | EXACT_SOURCE |
| 1.6 | **AddSrc 0xA558AC with bActive = 0.** It creates the source (0xA01E24 → factory 0xA562B8), calls StartStream 0xA56650(src, pbi+0x1DC, pbi+0x1E0) and stores **voice+0xD8 = pending source**. | 0xA558C0..0xA5592C | EXACT_SOURCE |
| 1.7 | **The switch.** In the pitch node 0xA52D4C, when the current input is exhausted and the source reported NoMoreData (+0xB8), and voice+0xD8 ≠ 0, it calls 0xA52B90. Then:<br>- **pbi+0x1D8 > 0:** decrement it by round((u16 +0x94 − u16 +0x96)·pbi+0x164) and return 0x11.<br>- **Otherwise:** StartStream must return 1 (0x3F → 0x11). The format must match: +0x15C byte (channel count), +0x15D low nibble (config type) and word +0x15C bits 12..31 (channel mask). The sample rate is not compared. A mismatch returns 0x11.<br>- **On a match:** SwitchToNextSrc 0xA549A0 destroys the current source and sets +0xD4 = +0xD8, voice+8 = the new PBI, sends the start notification 0xA56478 for the new source, sets +0x388 and clears +0x1BE b3. The pitch node re-inits with the new format (0xA47528) and **returns 0x2B (keep filling the same output buffer)**, or 0x2D if it is full.<br>**So the first sample of the next source follows the last sample of the current one directly, in the same buffer.** | 0xA52EBC..0xA52F84; 0xA52B90..0xA52D2C; 0xA549A0..0xA54A28 | EXACT_SOURCE |
| 1.8 | **Shipped mode-4 data.** All leaves of 196431345 and 990835622 (under 725225627) are stereo Vorbis at 48 kHz, config 0x3102, so the format check passes. The delay is 0. | `stereo.py` | EXACT_SOURCE (D) |
| 1.9 | **Fallback at PBI end.** 0xA6AF14 → 0xA6B0D8 sets +0x90 = the chain ID and enqueues with no delay. It is reached only if +0x250 ≠ 0 at PBI end, which cannot happen after 1.1. The delayed variant is 0xA6AFB0, taken when arg r1 or +0x1BC b4 is set. | 0xA6ACEC..0xA6AD20, 0xA6AEF4..0xA6AF74, 0xA6B0D8..0xA6B0E4 | EXACT_SOURCE for the branch; when it fires in practice is UNKNOWN |

#### 2. PlayAndContinue delay resolution

| step | what the original does | citation | classification |
|---|---|---|---|
| 2.1 | **Modes 1/2 set the delay** with action vfunc+0x20(0x0F, delaySamples). 0xA61260 returns prop 0x0F plus ranged 0x0F (min + LCG random × range). | 0xA6A744..0xA6A760; 0xA612A8..0xA6138C | EXACT_SOURCE |
| 2.2 | **EnqueueOrExecute 0x9AA0FC.** F = u16 [0x1052440], q = delay / F (0x4BE310), rem = delay − q·F.<br>- **Type 0x503:** frames = max(0, q − L), with L = [0x108D90C+0x1C]; pending+0xC = delay − frames·F.<br>- frames == 0 → Execute now.<br>- Otherwise launch = mgr+0x4C + frames, inserted in order.<br>- **Other types:** pending+0xC = rem, and launch = tick + q. | 0x9AA140..0x9AA194; 0x9AA24C..0x9AA2B8 | EXACT_SOURCE |
| 2.3 | **L = 1.** SoundEngine::Init copies the 0x4C-byte init settings to 0x108D90C. The default stores 1 at +0x1C. Anki writes only +8, +0x18 (populate 0x8D8158) and +0x14 (0x8D81B0) on the same struct. The name "uContinuousPlaybackLookAhead" comes from SDK knowledge and is only a label. | 0x99E458..0x99E464; 0x99DCA8/0x99DCD4; 0x8D8184..0x8D8192, 0x8D81DC | EXACT_SOURCE |
| 2.4 | **Result.** With q ≥ 1 the action executes **one frame before** its nominal frame, carrying offset = F + (delay mod F). With q ≤ 1 it executes at once and carries the full delay. The offset reaches pbi+0x1D8 through params+0x74 (1.2, 1.3). | as cited | EXACT_SOURCE |
| 2.5 | **Voice-side consumer, first source of a new voice** (update 0xA54F1C). s = round(u16 state+0xC × pbi+0x164). If +0x1D8 ≥ 0 it subtracts s, and **the source starts this frame only if the old value was < s**. In the first buffer (pitch node +0xBA) it zero-fills n = round((+0x1D8 + ratio·F)/ratio) leading frames per channel and sets +0x30 = n. With ratio 1, **n = offset mod frame**, so the first sample lands exactly at the offset. | 0xA55090..0xA550C4, 0xA55228..0xA55244; 0xA53050..0xA5312C | EXACT_SOURCE. pbi+0x164 = 1.0 at ctor 0xA00228; other writers were not traced (RECOVERABLE_GAP) |
| 2.6 | **Not-ready check (NEW).** When StartStream returns 0x3F, 0xA544BC compares +0x1D8 with round((L+1)·F·ratio). With enough lead it keeps waiting; otherwise it sets +0xE8 \|= 1 and calls 0xA0428C. | 0xA544E0..0xA545D4 | EXACT_SOURCE for the branch; what 0xA0428C means is UNKNOWN |
| 2.7 | **Reference point.** The delay counts from the action-manager tick at enqueue. 0xA6A580 uses only length and xfade; the current voice's own sub-frame offset is **not** added. | 0xA6A6D0..0xA6A738 | EXACT_SOURCE. The drain 0xA38420 versus the mgr+0x4C increment is RECOVERABLE_GAP |

#### 3. EndOfEvent for continuous plays

| step | what the original does | citation | classification |
|---|---|---|---|
| 3.1 | **Counters on the playing-ID record.**<br>- +0x18 (PBI count) ++ at 0xA04D48 (from PBI init 0xA02914) and −− at 0xA04DE8 (from PBI Term 0xA02CA8).<br>- +0x1C (action count) ++ at 0xA04EDC at the start of every EnqueueOrExecute (key = pending+0x28), and −− at 0xA04F54 after Execute. | 0xA04DD0..0xA04DDC; 0xA04E48..0xA04E64; 0xA04F3C..0xA04F44; 0x9AA118..0x9AA124; 0x9AA2BC..0x9AA30C | EXACT_SOURCE |
| 3.2 | **Release 0xA03618** runs only when +0x18 == 0 and +0x1C == 0. It unlinks the record. If flags +0x48 bit0 is set, it releases the lock and calls callback +0x40 (type 1) with {cookie +0x44, gameObj +0x24, playingID, eventID +0x20}. Flag 0x400000 → 0xA05934 first. This runs synchronously on the decrementing thread. | 0xA03618..0xA03658, 0xA03760..0xA037BC, 0xA037E8 | EXACT_SOURCE |
| 3.3 | **PlayAndContinue holds the count.** pending+0x28 = pbi+0x140, so a pending 0x503 holds +0x1C. In modes 1/2 it executes before item k ends (delay < length). In mode 4 it executes at once. 0xA6ACC0 enqueues any continuation (0xA6AF74) **before** tail-calling PBI Term (0xA6AD60). **So EndOfEvent fires in the Term of the last item's PBI, and never for loop-0 containers.** | 0xA6A4A0..0xA6A4B0, 0xA6AF1C..0xA6AF2C; 0xA6AD60 | EXACT_SOURCE |
| 3.4 | **Latency of PBI Term** after the last voice sample (the 0xA56414/0xA01800 chain) is not read. | — | RECOVERABLE_GAP |
| 3.5 | **Dropped action.** When the pending list is full, the action is dropped after +0x1C++ and without 0xA04F54, so EndOfEvent never fires. | 0x9AA314..0x9AA35C | EXACT_SOURCE |

#### 4. src+0x10 bit0

| step | what the original does | citation | classification |
|---|---|---|---|
| 4.1 | **The latch.** The base source ctor clears bit0 and bit1. **The only setter is 0xA56650: when vfunc+0x28 (StartStream) returns 1, bit0 is set**, and after that 0xA56650 returns 1 without calling again. All other `strb [src,#0x10]` writers touch only bit1. **So bit0 means "started", not "duration known."** | 0xA5627C..0xA562A8; 0xA56650..0xA56684; 0xA73FC0, 0xA74AF8, 0xA74CA4, 0xA758D0, 0xA75FB0, 0xAB1A40, 0xAB20B8 | EXACT_SOURCE |
| 4.2 | **Duration** is vfunc+0x34 = 0xA72F5C in all six source vtables (0x103D6B8, 0x103D738, 0x103D838, 0x103D948, 0x103E0B0, 0x103E130). It returns 0 when the pbi+0x1B8 loop count is 0 (infinite). | 0xA72F5C..0xA72F84 | EXACT_SOURCE (the loop-weighted formula is only skimmed) |
| 4.3 | **Source type.** The bank loader 0x9B9C90 maps:<br>- stream byte 0 → srcType 3<br>- stream 1 or 2 → srcType 1<br>- +0x14 bit1 = (stream == 1)<br>The factory then picks the class:<br>- **PCM:** type 1 → 0xA76140, type 3 → 0xA72D04<br>- **ADPCM:** type 1 → 0xA74244, type 3 → 0xA72A2C<br>- **Other codecs:** 0x9CC3EC<br>- **type 2:** plugin source 0xA78D10 | 0x9B9CE0..0x9B9DC0; 0xA01E24..0xA01E44; 0xA562B8..0xA56408 | EXACT_SOURCE |
| 4.4 | **Which StartStream can return 0x3F.**<br>- The ADPCM type-3 class 0xA72674 returns only 1 or errors.<br>- The shared PCM/ADPCM type-1 StartStream 0xA7538C can return **0x3F** when buffered data < required.<br>- The Vorbis 0xAB0B20 returns 1, 2, 7 or 0x34. | 0xA72674..0xA7291C; 0xA75628..0xA75670; 0xAB0B84..0xAB0E00 | EXACT_SOURCE for those three; the PCM type-3 class (0xA72D7C) and Vorbis 0xAB22D4 are not read |
| 4.5 | **Shipped sounds by stream byte.**<br>- **Cozmo.bnk:** stream 1 = 333 ADPCM + 1826 Vorbis, **full media in DIDX** (sizes equal); stream 0 = 27 Vorbis; stream 2 = 18 Vorbis with no media in any bank.<br>- **SFX:** 1 / 23 / 73.<br>- **UI:** 4 / 0 / 9. | `nodes.py` census | EXACT_SOURCE (D) |
| 4.6 | **When bit0 is set at the start notification.**<br>- At the chain switch (0xA54A08) bit0 is always set, because the switch requires 0xA56650 == 1.<br>- For the other two call sites (0xA44BF8 after 0xA54A30 == 1, and 0xA544AC from 0x9D3BA0/0x9D3BBC), whether StartStream has always succeeded first is not read. Nor is whether the type-1 prefetch class succeeds on its first call. | 0xA52C18..0xA52C28 | RECOVERABLE_GAP (read the 0xA74C80/0xA74564 prefetch path and the 0xA431A8→0xA544BC order) |

#### 5. The 18 stereo media

| step | what the original does | citation | classification |
|---|---|---|---|
| 5.1 | **All 18 sounds using 0x3102 media sit under robot actor-mixers**, all stream 1:<br>- 11 Vorbis under 196431345 / 990835622 → 725225627 → **62050212**<br>- 7 ADPCM (tag 2) under 407474969, 449556122 and 103358059/617225661/859715230 (via 697684187) → **682998829** | `stereo.py` | EXACT_SOURCE (D) |
| 5.2 | **The Vorbis voice config comes from fmt+0x14:** byte0 → pbi+0x15C, type nibble and mask → +0x15D..+0x15F. | 0xAB0BB0..0xAB0C28 | EXACT_SOURCE |
| 5.3 | **Pan path.** 0xA25FF8 with in 0x3102 and out 0x4101 (same type 1) goes to 0xA1F79C. Mono output with mode ≤ 1 goes to 0xA1FA50. Two non-LFE inputs → 0xA1FB0C. Out mask & 0x737 == 4 → 0xA209BC, which sets matrix[ch][0] = table[bit] for FL and FR. Table 0xFFA970[0] = [1] = **0x3F3504F3 = 0.70710677**. The pan inputs are not used on this path. **So mono = 0.7071·L + 0.7071·R.** | 0xA2604C..0xA26194; 0xA1F7E4..0xA1F80C; 0xA1FA50..0xA1FA54; 0xA1FB0C..0xA1FB24; 0xA1FD14..0xA1FD18; 0xA209BC..0xA20A80 | EXACT_SOURCE (that dry and aux both use this panner relies on gapE 2.7 / gapF 3.1) |

#### 6. Vorbis decoder parts gapB left unread

**Summary.** The decoder is a fork of **Tremor lowmem**, the fixed-point Tremor branch. The codebook struct matches that branch's `codebook` field for field, except that `q_seq` is missing: 0x3C bytes, memset at 0xABA1A8.

| step | what the original does | citation | classification |
|---|---|---|---|
| 6.1 | **Lookup type** is 1 bit. 0 = entry numbers only (dec_type 0); 1 = maptype 1. **There is no maptype 2.** | 0xABA314..0xABA31C | EXACT_SOURCE |
| 6.2 | **Stock Tremor `_float32_unpack`, integer.**<br>- mant = v & 0x1FFFFF<br>- zero mantissa → mant 0, point −9999<br>- otherwise shift left until bit30, point = exp − 788 − shifts, sign by conditional negate<br>- q_min → +0x20/+0x24, q_del → +0x28/+0x2C | 0xABA468..0xABA4D4, 0xABA62C..0xABA658, 0xABA790 | EXACT_SOURCE (stock) |
| 6.3 | **q_bits and q_seq.** q_bits = read(4)+1 at +0x30. **q_seq is read with read(1) and discarded**: it is never stored, and r0 is overwritten at 0xABA538. Then q_del >>= q_bits and q_delp += q_bits, as in stock lowmem. | 0xABA4D8..0xABA520 | EXACT_SOURCE (**deviation: no q_seq**) |
| 6.4 | **Value table and decode type.** quantvals = stock `_book_maptype1_quantvals`. The quantvals × q_bits values go into a u16 alloca. **dec_type is always 1**; Tremor lowmem can choose type 2 here. nodeb/leafw come from (q_bits·dim+8)/8, then `_make_decode_table` 0xAB96EC runs, then q_val is cleared. | 0xABA528..0xABA614; 0xABA684..0xABA784 | EXACT_SOURCE (**deviation: no dec_type 2**) |
| 6.5 | **decode_map 0xAB9BB0.**<br>- Tree walk with nodeb 1, 2 or 4.<br>- v[i] = entry & mask, then entry >>= q_bits.<br>- add = q_min shifted by (point − q_minp).<br>- **v[i] = add + ((v[i]·q_del) >> (point − q_delp))**, or << when that shift is negative.<br>- Plain int32 multiply, **no sequence accumulation**.<br>This is Tremor lowmem `decode_map` dec_type 1, without q_seq. | 0xAB9BB0..0xABA184 | EXACT_SOURCE |
| 6.6 | **Codebook library in the engine.** Table 0x1058290 (GOT 0x104026C) holds **599 books**: 162 without lookup, 437 with lookup 1 and seq 0, and **0 with seq 1**. The runtime only takes book IDs (read(10)), so dropping q_seq changes no shipped decode. | `cblib.py`; 0xAB6474..0xAB64B0 | EXACT_SOURCE (D+L) |
| 6.7 | **Residue setup.** Fields: type read(2), begin/end/grouping read(24) (grouping +1), partitions read(6)+1, groupbook read(8), cascades 3/1/5, books read(8). The struct is {type, stagemasks, stagebooks, begin, end, grouping, u8 partitions, u8 groupbook, u8 stages}, 0x1C bytes, which is Tremor lowmem `vorbis_info_residue`. | 0xAB6F54..0xAB72xx | EXACT_SOURCE |
| 6.8 | **res_inverse 0xAB73F8, types 0 and 1** (branch 0xAB770C). It follows Tremor lowmem:<br>- n = min(end, pcmend/2) − begin<br>- the nonzero channels are compacted<br>- per-channel partword arrays, the channel copy at 0xAB7E24<br>- the class word comes from 0xABA840, split with udiv 0x4BE310 / mls<br>- stage book = stagebooks[(pw<<3)+s] if stagemasks[pw] & (1<<s)<br>- **Both types call decodev_add 0xABAA6C** (a[i++] += v[j]). There is no type test, so type 0 does not get decodevs_add.<br>- point −8, int32 add, no saturation. | 0xAB770C..0xAB7E00; 0xABAA6C..0xABABB4 | EXACT_SOURCE (**deviation for type 0**) |
| 6.9 | **Type 2** (0xAB745C..0xAB7708). max = pcmend·ch/2; it returns early if no channel is nonzero; spp /= ch; beginoff = begin/ch. It calls **decodevv_add 0xABABB8 (book, in, offset, opb, n, −8) with no channel argument. Its channel index toggles 0/1 (`eor r5,#1`), so it is hard-wired to 2 channels.** | 0xAB745C..0xAB76D4; 0xABABB8..0xABAD28 | EXACT_SOURCE (**deviation**) |
| 6.10 | **All 1923 shipped Vorbis media parse cleanly** (fmt size 0x42; Cozmo 1769, Music 126, SFX 24, UI 4):<br>- **mono → residue type 1 only (1762)**<br>- **stereo → type 2 only (161)**<br>- **mode count = 2 for all**<br>So the deviations in 6.8 and 6.9 are inert, and the 1-bit mode read (gapB V6) is right for every shipped file. | `setup.py` | EXACT_SOURCE (D) |
| 6.11 | **Output scale.**<br>- The residue int is real·2^8 (point −8).<br>- The floor table is Tremor's table / 2^15, about libvorbis × 2^16 (Tremor [0] = 229 against libvorbis 1.065e−7·2^31 ≈ 228.7).<br>- The IMDCT stage 0xAB39D8 multiplies its outputs by **q1 = 2^−24** (0x33800000) before storing.<br>2^8 · 2^16 · 2^−24 = 1, so the spectrum returns to libvorbis float scale, and the windows are libvorbis float tables. **So the output is nominally ±1.0 float.** | 0xAB3CF8/0xAB3D00 loaded 0xAB3AB0..0xAB3AB4, applied 0xAB3CB4..0xAB3CDC | EXACT_SOURCE for the factors. That the rest of the float MDCT (×0.5 q0 at 0xAB3A70, trig tables GOT −0x58..−0x28) is normalised like libvorbis `mdct_backward` is RECOVERABLE_GAP (compare the tables or emulate one packet) |
| 6.12 | **+0x2C / +0x2E** (u16 start-skip and end-trim):<br>- **Setter:** 0xAB3244(dsp, skip, trim) stores both and sets +0x1C/+0x20 = −1.<br>- **Header copy:** the Vorbis StartStream 0xAB0B20 needs fmt tag 0xFFFF (else 7). It copies fmt+0x18 → src+0x14 and fmt+0x1C..0x41 → src+0x94..0xB9.<br>- **Normal start: skip = 0.** When pbi+0x1BD b7 (seek) is set: skip = pbi+0x1B4.<br>- **trim = u16 fmt+0x32 if loop count src+0x38 == 1, else u16 fmt+0x26.**<br>- **Loop-back:** skip = u16 fmt+0x24 (src+0x9C) at 0xAB0398..0xAB03C8.<br>- The other class uses +0xC0/+0xC2/+0xCE the same way.<br>- **Consumption (0xAB3884..0xAB3910):** skip drops leading returned samples; on the last packet, end = max(current − trim, returned).<br>- **Shipped:** fmt+0x24 is 0 in 1881 of 1923 media and nonzero in 42; fmt+0x32 takes 867 distinct values. | 0xAB3244..0xAB3260; 0xAB0B98..0xAB0CA8, 0xAB0D5C..0xAB0DF0; 0xAB1150..0xAB1180, 0xAB21F0..0xAB2204; `vorbfmt.py` | EXACT_SOURCE. The condition picking +0x9E or +0xAA on the loop-back path (0xAB0398..0xAB03EC) is only skimmed. SDK field names are labels only |

#### Existing records contradicted
- **gapD D1.4** ("the type 0x0503 special case is used by no Cozmo action") is wrong. 0x503 is the internal PlayAndContinue (created at 0xA6A32C and 0xA6A5F8) that every shipped continuous container uses, so the lookahead special case (2.2) applies to them.
- **gapF 2.4** calls src+0x10 bit0 "duration known." It is the StartStream-success latch (4.1).
- **gapF 2.5** delay resolution is resolved. It is not simply frame + remainder: it executes one frame early with F + remainder (2.2–2.5).
- **gapF 2.7** mode 4 is resolved (§1).
- **gapE 2.7 / gapF 3.1** ("mono gain 1.0 for robot media") is true for mono only. The 18 robot-routed stereo items get 0.7071 per channel (5.3).

#### Records whose evidence is too weak
- **Any model of mode 4** as a cross-fade, as an overlap, or as a new voice is contradicted.
- **Any model of PlayAndContinue** firing at exactly q frames with the remainder mod F, or starting without leading zero-padding, is contradicted.
- **A Vorbis port** that uses a generic-channel decodevv or a type-0 decodevs path matches the original only for shipped data. So does one that applies q_seq; the original ignores it.

#### Open questions / remaining RECOVERABLE_GAPs
1. What the voice does when the chained source takes the 0x11 path (format mismatch, or +0x1D8 > 0 at the switch). UNKNOWN; shipped mode-4 data never triggers it.
2. When the start-notification drain (0xA38420) happens relative to the mgr+0x4C tick.
3. How long after the last sample PBI Term runs, which is the EndOfEvent latency.
4. Whether the type-1 prefetch StartStream returns 1 on its first call for stream-1 sounds, and so whether bit0 is set at the first notification.
5. The writers of pbi+0x164 (the resampling ratio used in the offset conversion).
6. Whether the float MDCT is normalised exactly like libvorbis.
7. Sequence 725225627 has a non-continuous child container (990835622); how a PlayAndContinue behaves when its target is a container has not been traced.
