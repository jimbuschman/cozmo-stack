# Q10 pre-extraction: remaining M6 implementation gaps

## Coverage audit (2026-10-01)

| record | coverage | unchecked basis affecting conclusions |
| --- | --- | --- |
| M6-001 | CHECKED | None. The deliberately unread/unexercised branches are bounded gaps, not treated as recovered. |
| M6-002 | CHECKED | None. Multichannel/LFE and unshipped-size residuals remain explicit UNKNOWNs. |
| M6-004 | CHECKED | None. Every implemented kernel and the unread kernels are distinguished. |
| M6-005 | CHECKED | None. |
| M6-006 | CHECKED | None. The separate PlayInternal/PBI ownership is M6-025. |
| M6-007 | CHECKED | None. |
| M6-008 | CHECKED | None. Unread timing and live Term ownership remain explicit UNKNOWNs. |
| M6-009 | CHECKED | None. Transition evolution and the partly read validity gate remain explicit UNKNOWNs. |
| M6-010 | CHECKED | None. Caller-supplied attenuation/fade bundles remain explicit UNKNOWNs. |
| M6-011 | CHECKED | None. The untranscribed NEON matrix is not claimed bit-exact. |
| M6-012 | CHECKED | None. General panner/LFE and NEON ordering remain explicit UNKNOWNs. |
| M6-013 | CHECKED | None. Unread linked/LFE limiter and coefficient details remain explicit UNKNOWNs. |
| M6-014 | CHECKED | None. Aux policy and hardware gate values remain explicit inputs. |
| M6-015 | CHECKED | None. |
| M6-016 | CHECKED | None. The OnDevice and `robot_volume` residuals are called out. |
| M6-017 | CHECKED | None. Sink pacing is correctly bounded as HARDWARE_ONLY. |
| M6-018 | CHECKED | None. This is a forced policy; phone-dependent values are not claimed known. |
| M6-020 | CHECKED | None. Recoverable raw bodies/consumers are included; stripped names remain external. |
| M6-023 | CHECKED | None. The missing seams and production receiver are explicit. |
| M6-024 | CHECKED | None. Loader call sites are recovered; internals remain explicit UNKNOWN. |

No conclusion in this report rests on unchecked work. `UNKNOWN`, `HARDWARE_ONLY`, and external-name qualifications below identify where the shipped package does not settle a value; they are not work omitted from this audit.

Date: 2026-10-01  
Request: Q10 in `requests/20260930-codex-queue.md`  
Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so` 3.4.0-1204  
Baseline: `origin/main` / `415b9e0` after the required pull

## Scope and central result

At HEAD M6 has 23 `IMPLEMENTATION_GAP` records. The checked live-audio report and its independent check cover M6-022, M6-025 and M6-026, so this report covers the other twenty: M6-001, 002, 004..018, 020, 023 and 024.

The central production-path gap is real. Native production composes:

`Unity/app PostAudioEvent → 0x008D8B74/0x008DED00 → 0x009A6704 → 0x009A0EF8/0x009AA3DC → node PlayInternal → PBI → voice/source → 0x00A44D4C voice and bus render → Robot_Bus FX → Hijack → robot-audio callback`.

C# production instead constructs `CozmoAnimations`' `AnimationScheduler` (`CozmoAnimations.cs:162`); an optional `WwiseAudioSource` constructs `WwiseSongRenderer` (`WwiseAudioSource.cs:43-55`), resolves events directly through `WwisePlayback.Resolve` (`:540`) and decodes/mixes offline. That offline path calls `WwiseBusChain.For`, but no production constructor binds `WwiseAudioInputDispatch`, `WwiseEventRuntime`, `WwisePlaybackBridge`, `WwiseVoiceLinker`, `WwiseVoiceEngine`, `WwiseFrameDriver`, `WwiseBusLifetime`, `WwiseHijackPlugin`, or `WwiseRobotAudioPath` into the native-shaped live graph. Construction hits for those live-graph owners are tests or their own declarations. Therefore component-local tests, and the offline `WwiseBusChain` call, cannot settle the whole live composition.

Common native result words used below are `0x2D` DataReady, `0x2E` DataNeeded, `0x11` NoMoreData, `2` failure, and `0x3F` deferred/not-ready. Common exact float words are `0.0f=0x00000000`, `0.5f=0x3F000000`, `1.0f=0x3F800000`, `2.0f=0x40000000`, `-80.0f=0xC2A00000`, `0.0001f=0x38D1B717`, `0.70710677f=0x3F3504F3`, and `22320.0f=0x46AE6000`.

## M6-001 — bank/HIRC readers

C# entry: `WwiseSoundLibrary.LoadCore` → `WwiseBank.Load` / `WwiseHierarchy` readers.

| step | address | what the engine does | gates/order/failure | floats / unresolved |
|---:|---|---|---|---|
| 1 | `0x009B338C` | dispatches HIRC object type to Event, Action, Sound, RanSeq, Switch, ActorMixer, Bus, Layer or NodeBase readers | object header precedes type body; unknown/short data fails the bank load | integer parsing |
| 2 | `0x009CD01C`, `0x00A613B0`, `0x00A1DA08`, `0x00A0828C`, `0x00A2F1D0`, `0x00A669EC`, `0x009C3FFC`, `0x009D24D4`, `0x009F6EF8` | reads each body in runtime field order | conditional flags control following fields | numeric words remain at file width |
| 3 | `0x009ECF44` | positioning reads beyond the first byte when b0/b3 are set | shipped nodes leave those gates clear | gated body **UNKNOWN**; C# reads only the first byte |
| 4 | Sound source path under `0x00A1DA08` | plugin low nibble 2 or 5 reads `u32 size` then exactly `size` bytes | all 43 shipped sizes are zero | C# skips size but not a nonzero body |
| 5 | `0x009F7254..0x009F72EC` | reads RTPC parameter id as a continuation-bit varint | C# one-byte read agrees only for shipped ids `<0x80` | curve points remain binary32 |
| 6 | `0x009C3FFC..0x009C4308`; `0x009C6420..0x009C6564`, `0x009C0D08..0x009C0E14`, `0x009F6E3C..0x009F6EEC`, `0x009F7364..0x009F7388` | Bus reads props, A/B flags, max instances, channel config, C, recovery/max-duck, ducks, FX/mixer, flag, RTPC, states, then four bytes only when BKHD feedback is on | shipped A/B are zero and feedback is off; C# is shipped-data equivalent only | `maxDuck` raw f32; recovery later uses mix rate |
| 7 | `0x009D24D4..0x009D2730`; per-layer `0x00A6DD54` | real Layer reader consumes NodeBase, children, layer count/bodies, final byte | all six shipped containers have zero layers | outer order recovered; body **UNKNOWN**. `0x00A67558` is Attenuation, not Layer |
| 8 | `0x009B0B14`; consumers under M6-020 | reads STMG header, groups, params and both trailing loops | each count controls exact reads; nonzero counts are accepted | raw float/u32 bits preserved |

Implementation target: retain `WwiseHierarchy.cs`, but add or fail closed on the nonzero positioning, source-plugin, Bus A/B/BKHD, RTPC-varint and Layer-body cases. `EveryHierarchyObjectInTheShippedBanksConsumesExactly` is source/asset-derived for shipped consumption, but cannot exercise absent flag combinations.

## M6-002 — native Vorbis decoder into the live source

C# entries: `WwiseVorbisNative.Decode`, `WwiseVorbisSource`; required production entry is the media-source factory under the M6-025 bridge, not `WwiseAudioSource.DecodeMedia`'s offline decoder.

| step | address | what the engine does | gates/order/failure | floats / unresolved |
|---:|---|---|---|---|
| 1 | `0x00AB63E0`, `0x00ABA188`, table `0x01058290` | parses stripped setup and Wwise library codebooks | rejected setup propagates decoder failure | packed integer fields |
| 2 | `0x00AB7E40 → 0x00AB3780 → 0x00AB6B14` | frames packet, reads one-bit mode, floor, residue, inverse coupling, IMDCT | result `0x2D/0x2E/0x11`; floor post 0 skips neighbour-mask updates at `0x00AB9100` | binary32 value path |
| 3 | `0x00AB4E34`, tables `0x01004640..0x010053DC` | performs native binary32 IMDCT | shipped sizes 256/2048 and 512/1024 | exact native words; unshipped 64/128 sign-of-zero differs |
| 4 | `0x00AB5A94`, windows `0x01054490`, driver `0x00AB3520` | combines windows and saves overlap | prev/current block flags choose regions/sign | binary32, bit-exact in whole-decode emulation |
| 5 | `0x00AB0448` streamed / `0x00AB1550` in-memory | source render calls the decoder and emits through `0x00A73490` | `StartStream` is vt+0x28 `0x00AB0B20/0x00AB22D4`; LFE channel reorder is not built | multichannel (>2) reorder **UNKNOWN** for shipped reachability |

`WwiseVorbisWholeDecodeNativeTests` is a genuine source oracle: it emulates the native decoder and compares samples bit for bit. Ordinary decode tests that compare two C# paths are not sufficient for wiring. Required change is composition: source factory → one of the native-shaped source wrappers → voice render, preserving StartStream args and results.

## M6-004 — resampler

C# entry: `WwiseResampler`; required callers are `WwiseLiveVoice.Resampler` and `WwiseHijackPlugin.Resampler`, replacing the offline `WwiseAudioSource.ToRobotRate/Lanczos` path.

| step | address | what it does | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | `0x00A46D80..0x00A46D88` | constructor sets phase `+0x2C=0x10000` | before format/kernel selection | Q16 phase, one sample |
| 2 | `0x00A47038..0x00A47150`; tables `0x0103C0B8`, `0x00FFD450` | selects int16/float, channel and mode kernel | unsupported combinations fail closed | integer/Q16 state |
| 3 | `0x00A47384..0x00A4751C` | computes pitch step and 0x400-phase-unit ramp | pitch set precedes Execute | `u32(float(inRate/outRate)*powf(2,cents/1200)*65536+0.5)` |
| 4 | `0x00A47178`, kernels `0x00A48F5C/0x00A4913C/0x00A49E40` | executes int16 bypass/interpolation or float mono interpolation | empty `0x11`; target-full `0x2D`; otherwise `0x2E` | `1/32768=0x38000000`; `22320.0f=0x46AE6000` |
| 5 | `0x00A49634`, `0x00A479F4`, `0x00A4A958` | stereo int16 and float bypass/ramp variants are reachable | valid native modes | bodies **UNKNOWN**; no substitute kernel is justified |

The focused tests copy recovered formulas and are source-derived, but they do not prove the production format fields or composition.

## M6-005 — Wwise string hash

C# entry: `WwiseHash.Id` used by library name lookup.

| step | address | behavior | gates/order/failure | width |
|---:|---|---|---|---|
| 1 | `0x0099DB84..0x0099DBD4` | `strncpy` copies `min(strlen+1,0x103)` bytes | copy first | bytes |
| 2 | `0x0099DBD4..0x0099DC04` | ASCII-lowercases then FNV-1 hashes `min(L,0x102)` bytes | NUL is not hashed | u32 wrap |

`WwiseHashTests` uses known source-derived hashes and boundary lengths. No additional native body is needed; the remaining work is verification/settlement, not new wiring.

## M6-006 — event/control path

C# entry: `WwiseEventRuntime.PostEvent`; required live entry is `WwiseAudioInputDispatch` and the animation/audio callers, with `PlaybackBridge` bound.

| step | address | behavior | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | `0x009A6704 → 0x009A0EF8` | PostEvent queues or executes | playing id 0 signals failure | integer |
| 2 | `0x009AE0B0`, `0x009AA3DC` | pump, then ExecuteEvent | event actions stay in bank order | delay values at binary32/native frame width |
| 3 | `0x009AA0FC`, `0x009A9F88`, `0x009AF8A8` | converts delay to frames+remainder, drains due actions, performs them | PlayAndContinue executes one frame early; stable due-order | exact frame/remainder arithmetic |
| 4 | `0x00A62D38/0x00A62A1C`, `0x00A663C8`, `0x00A645C8` | Play, Stop and Seek actions | Play must dispatch node vt+0x128 then bridge into M6-025; failure propagates | seek units/path partly **UNKNOWN** |
| 5 | `0x00A2C730` | resolves Switch PlayInternal | switch/default precedence and registration gates | no guessed fallback |

Current `WwiseEventRuntime` is isolated from `WwiseAudioSource`; its focused tests call the model directly. Expected queue/order values come from inventory rows, but production-path tests are absent.

## M6-007 — random/sequence selection

C# entry: `WwiseSelection` / RanSeq handling in `WwisePlayback`; required live entry is container `PlayInternal` reached from M6-006/M6-025.

| step | address | behavior | gates/order/failure | width |
|---:|---|---|---|---|
| 1 | `0x009A6C68..0x009A6CA8`; seed `0x0099DB58..0x0099DB78` | seeds a shared 64-bit LCG from `time(NULL)` and advances `seed=seed*6364136223846793005+1` | one shared state, draw before selection | u64 wrap |
| 2 | state `0x00A096A0..0x00A09A08`; selection `0x00A08A44..0x00A09124` | filters eligible children, picks k-th/weighted/shuffle entry | avoid list before weight draw; empty returns failure 2 | integer weights |
| 3 | `0x00A0863C..0x00A08630` | sequence wrap / ping-pong endpoint | direction changes at endpoint; shipped ping-pong bit is clear | integer |

Tests derive selection tables from the recovered model but run the C# selector itself; only bank-census expectations come from assets. Live state sharing with PBIs is not tested.

## M6-008 — continuous containers

C# entry: `WwiseContinuous`; required caller is RanSeq/Switch/Layer continuous `PlayInternal` and PBI start/Term.

| step | address | behavior | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | RanSeq continuous bodies near `0x00A0828C` | draws loop count and chooses next at voice start | loop min/max inclusive; selection occurs at start, not post | integer |
| 2 | continuous helpers `0x009EE9C4..0x009EEA5C`, `0x009E8940`, `0x009E8FCC`, `0x009E8F10`, `0x009E91F8` | modes 1/2 cross-fade, mode 4 same-voice continuation, mode 5 trigger-rate | exact helper bodies still **UNKNOWN** | frame/lookahead split **UNKNOWN** |
| 3 | PBI Term / callback path | emits EndOfEvent only after last PBI terminates | start notification and `src+0x10` timing precede termination | no guessed latency |

The class intentionally stops at decisions. Tests are model tests and cannot establish the unread action-manager timing or live Term ownership.

## M6-009 — RTPC store/evaluation

C# entries: `WwiseRtpcStore` and curve evaluator; required consumers are live PBI parameter calculation, voice filters/gain and buses.

| step | address | behavior | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | `0x00A0F678` and STMG entry `+8` | initial lookup uses explicit value, then STMG default | precedence is exact; BuiltIn semantic is **UNKNOWN** | binary32 |
| 2 | curve/evaluator paths under `0x00A13xxx` | applies curve, then scaling; combines by sum/product | curve order before accumulation | per-operation f32 |
| 3 | `0x00A13948..0x00A13A24`, `0x00A1B5FC` | gates positive-duration transition on prior valid value | invalid prior slot must not silently ramp | gate meaning partly **UNKNOWN** |
| 4 | `0x00A0E5E4` | evolves transition value | immediate duration writes now; positive duration evolves per frame | exact evolution **UNKNOWN** |

Focused tests use source-derived curve points but the store is not connected to the live parameter graph.

## M6-010 — gain composition

C# entry: `WwiseGain`; required callers are PBI effective-parameter calculation, voice connection update and bus output.

| step | address | behavior | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | `0x009FFAD4` and GetAudioParameters call chain | gathers node/parent/RTPC/randomiser inputs | links and sums in native order; randomiser once per voice | f32 |
| 2 | `0x00A4DA68..0x00A4DA74`, pool `0x00A15254..0x00A1525C` | fast dB-to-linear polynomial | clamp/mute before conversion | `0x3EA67F46`, `0x3CAA70DE`, `0x3F272DDB` |
| 3 | voice/send path | applies game-defined aux gain and muted dry path | aux before dry; bus volume after FX | mute floor `0xC2A00000`, ratio `0x38D1B717` |
| 4 | Robot_Bus/Hijack | `robot_volume` does not reach Hijack | routing gate | caller attenuation/fade bundles remain **UNKNOWN** |

Tests pin source-derived formulas, but the three literals in `WwiseGain.cs` are rounded wrong (Q9) and no live graph calls the class.

## M6-011 — voice filters

C# entry: `WwiseVoiceFilter`; required position is voice DSP before aux sends, with filter B on dry only.

| step | address | behavior | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | mapping/coefficient path around `0x00A76770..0x00A76A2C` | maps LPF/HPF values to cutoff and coefficients | target change starts ramp unless both current/new ≤0.1 | binary32 |
| 2 | `0x00A767BC..0x00A769C4` | processes aligned blocks with native NEON DF-I order | filter A precedes sends; filter B dry-only | exact lane rounding not transcribed |
| 3 | same range | ramps in eight-step chunks; completed ≤0.1 target filters ramp buffer plus four buffers before bypass | countdown order is behavior-changing | `0.1f=0x3DCCCCCD` |

C# uses a scalar equivalent, not the bit-exact NEON matrix. Its tests are source-formula tests and do not cover native lane rounding or live placement.

## M6-012 — mixer

C# entry: `WwiseMixerConnection`; required caller is the per-connection voice-to-bus pass.

| step | address | behavior | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | `0x00A25FF8`; dispatch `0x00A2604C..0x00A26194` | selects channel matrix | first update does not ramp; connection bit2 can arm fade-in | general panner beyond settled cases **UNKNOWN** |
| 2 | `0x00A1F7E4..0x00A1F80C → 0x00A1FA50 → 0x00A1FB0C..0x00A20A80`, table `0x00FFA970` | adds both stereo channels into mono with equal gain | per-frame ramp after first update | `0x3F3504F3` each |
| 3 | scalar/NEON loop | `g(k)=start+k*delta` | exact 4-lane/8-sample ordering | C# is equivalent, not bit-exact |

Tests take matrices/formula from source, but LFE and general 2D cases fail closed and production never constructs the connection graph.

## M6-013 — Robot_Bus insert FX

C# entry: `WwiseBusChain.For`; required caller is the bus insert-FX slots before Hijack.

| step | address | behavior | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | `.init_array` `0x0103E560→0x004DEB18`, `0x0103E564→0x004DEB8C`; create `0x009CC2AC/0x009CC4D8` | registers and lazily creates Peak Limiter id `0x6E` and Parametric EQ id `0x69` | slot/type/in-place flags gate execution | plugin identity from bank/registry |
| 2 | EQ coefficient `0x00AA25E0`, biquad `0x00AA2324`, execute `0x00AA2A84` | two EQ instances run in slot order | per-channel state, enabled bands only | Butterworth/RBJ arithmetic; exact polynomials partly **UNKNOWN** |
| 3 | limiter setup `0x00AA19CC`, DSP `0x00AA0EB4` | peak limiter follows EQ | detector then output gain; linked `0x00AA1464` and LFE `0x00AA09B8` unread | f32; fast-pow cutoff **UNKNOWN** |
| 4 | FX loop `0x00A4FD84..0x00A4FEF4`; gain `0x00A4FF3C..0x00A50008` | feeds Hijack after FX | Bus Volume is after FX | f32 |

`WwiseBusChain` does not implement the `IWwiseBusInsertFx` live interface and its tests largely assert its own standalone output. The shipped mono/unlinked configuration narrows reachability but does not justify invented arithmetic.

## M6-014 — bus lifetime

C# entry: `WwiseBusLifetime`; required owner is the bus pass in `WwiseVoiceEngine`/`WwiseFrameDriver`.

| step | address | behavior | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | bus creation/reuse path in `0x00A42xxx` | creates a bus on first mix and connections on demand | reuse key/state gates; aux-send policy **UNKNOWN** | integer/ids |
| 2 | lazy FX create | instantiates insert FX only when needed | create before process; creation failure returns native failure | f32 buffers |
| 3 | bus pass / idle removal | after no connections, runs one tail frame including a zero-length chunk, then destroys | order is mix → FX/tail → idle removal | buffer frames u16 |
| 4 | gate writers `0x009EAE18/24`, `0x009EC47C..0x009EC4A8` | derive bus-pass and frames-per-Perform gates from output device | runtime values depend on device state | **HARDWARE_ONLY value**, code path settled |

Standalone lifetime tests are source-derived state-machine tests; they do not bind the FX factory, aux policy, device or live bus list.

## M6-015 — Hijack plug-in

C# entry: `WwiseHijackPlugin`; required caller is Robot_Bus final insert slot and `WwiseRobotAudioPath` callback.

| step | address | behavior | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | plug-in registration | last registration wins | register before Init | callback pointers |
| 2 | Init | allocates persistent `1024 floats * channels`, initialises resampler to 22320, pitch 0 | allocation failure propagates | `22320=0x46AE6000`, `0=0x00000000` |
| 3 | Execute | appends valid frames, resamples and emits 744-sample chunks | retain partial frames; callback only on `0x2D` or `0x11` | binary32 bus input |
| 4 | Term | destroys persistent buffer/resampler | after final callback | no silent drop |

Tests now pin persistent-frame behavior from C10, but they construct the plug-in directly. No live bus installs it.

## M6-016 — robot-audio path

C# entry: `WwiseRobotAudioPath`; required live entries are animation keyframes, behaviors, singing and callback delivery to the robot audio stream.

| step | address | behavior | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | `0x0059687E..0x00596914` | draws every keyframe alternative up front and advances the track to its end | draw precedes wall-clock post | RNG/state widths |
| 2 | lambda `0x0059818C..0x005982A0`; PostCozmoEvent `0x00599E50..0x00599EF0`; core `0x008D8CE4..0x008D8D32` | posts on wall clock with playing id | PostEvent 0 immediately invokes type-4 error | integer ids |
| 3 | RTPC path | applies `event_volume` per playing id | after successful playing id | binary32 dB |
| 4 | `0x0059962A..0x005999A4` | ids 7..10 map to Robot_Bus_1..4, aux gain 1 and dry gain 0 | OnDevice game object 6 path is unbuilt and must fail closed | `1=0x3F800000`, `0=0x00000000` |
| 5 | callback `0x008D8D40`; PopRobotAudioMessage `0x00597DB4..0x00597E8E` | queues callbacks, exposes PopRobotAudioMessage and abort | callback order is source order | mix rate delegated to M6-017/018 |

Current tests drive the standalone state machine. They do not start from `AnimationScheduler.AudioAnimation`, `SingingBehavior`, or the other production callers and therefore do not establish the live route.

## M6-017 — audio-thread frame model

C# entry: `WwiseFrameDriver.PerformFrame`; required owner is a live audio thread/scheduler associated with the engine-backed source.

| step | address | behavior | gates/order/failure | floats |
|---:|---|---|---|---|
| 1 | `SoundEngine::Init → 0x009B0200 → 0x00A40940`, pthread entry `0x00A4087C` | creates audio thread and waits/posts semaphore at engine+0x54 | thread start before frames | integer |
| 2 | Perform path | order is message pump → due-action drain → source/voice render → bus pass → tick advance | no duplicate pump/drain | frames/counts |
| 3 | `0x00A44D4C` | voice pass, bus pass, idle removal | dynamic device gate chooses frames | buffers are binary32 |
| 4 | PBI Term/callback manager | EndOfEvent only after bus pass of last frame | Term before callback; exact post-Term latency **UNKNOWN** | n/a |
| 5 | `0x009EBE6C` device branch | sink supplies frame count/pacing | depends on OpenSL/device | **HARDWARE_ONLY value** |

`WwiseFrameDriverTests` (where present) take order from source but use fake renderers. No production thread owns the driver.

## M6-018 — mix rate and frame size

C# entry: mix/frame constants used by the live driver and Hijack.

| step | address | behavior | gate | bits |
|---:|---|---|---|---|
| 1 | `0x00A56E20`; store `0x00A56E80..0x00A56EA4` to `0x0108DF90` | caches `min(AudioTrack.getNativeOutputSampleRate(3),48000)` | JavaVM/context/native query; missing/zero later falls back to 48000 | integer `48000=0x0000BB80`; only f32 consumers use `0x473B8000` |
| 2 | `0x00A577C0..0x00A57830`; default `0x0099DC90` | rounds/chooses hardware-buffer-derived frame size | phone properties decide original value | `1024=0x00000400` integer |
| 3 | `0x00A57834..0x00A57850`; globals `0x0105243C`, `0x01052440` | commits selected rate and u16 frame size for downstream conversions | before consumers | integer/u16, then downstream f32 arithmetic |

The fixed C# values are the inventory's forced compatibility decision, not recovery of the original phone. Tests asserting 48000/1024 copy C# constants and are circular for phone-dependent behavior; they protect policy but cannot settle fidelity to an unknown device value.

## M6-020 — STMG group items and trailing bodies

C# entry: `WwiseStmg.Parse`.

Current manifest text before correcting its stale title: **“STMG: the group-item field meanings and the two trailing bodies (unread source; unexercised by shipped banks)”**, status `IMPLEMENTATION_GAP`; its current unresolved already incorporates the later extraction: **“Build the C9 raw layouts and consumer behavior in WwiseStmg.cs, including nonzero trailing counts; do not invent names…”**. Thus the title's “unread source” is stale, while the status/unresolved are consistent with the rows below.

| step | address | behavior | gates/order/failure | width |
|---:|---|---|---|---|
| 1 | `0x009B0C9C..0x009B0D0C`; `0x00A27CA4..0x00A27D60` | state item is three u32s; manager matches ordered `(field0,field1)` and stores field2 | reader passes false, suppressing reverse-pair update at `0x00A27D78..0x00A27DD0` | exact u32; `{from,to,transitionMs}` are public labels only |
| 2 | `0x00A325E0..0x00A32914` | switch word 0 becomes curve word 0, `float(index)` word 1, word 2 curve word 2; source word 1 enters parallel id array | count/arrays precede call `0x00A12050` | source word 0 copied bit-for-bit as binary32 |
| 3 | `0x009B0FBC..0x009B127C`; `0x00A3B84C..0x00A3B998` | trailing A is 56 bytes: u32 id, six u16, ten u32; new object copies all bytes to +0x10 | existing id increments refcount; nonzero count loops | raw widths |
| 4 | `0x00A3B180..0x00A3B1E8`, `0x00A3B0F8..0x00A3B17C` | A codes 0..5 write small slots (0..2 booleanize); 6..15 write recovered u32 offsets | numeric dispatch; names absent | integer/raw words |
| 5 | `0x009B12A4..0x009B1410`; `0x00A3BA44..0x00A3BB80` | trailing B is 40 bytes: u32 id plus nine u32; new object copies all bytes to +0x10 | keyed reuse/refcount; nonzero count loops | raw widths |
| 6 | `0x00A3B1EC..0x00A3B268` | B codes 0x10..0x18 write nine recovered offsets | numeric dispatch; names absent | integer/raw words |
| 7 | loops `0x009B0FDC..0x009B1298`, `0x009B12C0..0x009B142C` | accepts every counted entry | shipped trailing counts are zero; switch items are shipped | no extra conversion |

Current C# safely keeps group triples raw, but throws on either nonzero trailing count and contradicts steps 3/5/7. It must implement exact raw layout, keyed reuse/copy and numeric setter behavior; stripped names remain `BLOCKED_EXTERNAL` labels only. Shipped-bank parser tests settle only the zero-count path; nonzero tests must derive expectations from these native rows, not `WwiseStmg.Parse`.

## M6-023 — app audio-input dispatch

C# entry: `WwiseAudioInputDispatch.Handle` → `WwiseEventRuntime.PostEvent`; required production entry is the Unity/native message receive path.

| step | address | behavior | gates/order/failure | width |
|---:|---|---|---|---|
| 1 | Unity PostAudioEvent / `0x008D8B74` region | creates AudioUnityInput message | tag first | packed fields |
| 2 | `0x008DED00` path | AudioMuxInput → AudioMultiplexer → controller | envelope tags 1..7; PostAudioEvent tag 1 | integers |
| 3 | `0x008DED46/4C` | callback context first word `0xFF`; computes flags `1|4|8=13`; null context gives 0 | callback context before core post | u32 |
| 4 | `0x009A6704` | calls core PostEvent with flags/callback/cookie | return 0 is failure | current C# drops flags/callback/cookie |

`WwiseAppAudioInputTests` use source-derived wire bytes and routing, but call `Handle` directly. The production receiver and the core's flags seam are missing.

## M6-024 — bank/scene loading

C# entry: `WwiseSoundLibrary.LoadScene`; required production owner is audio-engine construction before event posting.

| step | address | behavior | gates/order/failure | width |
|---:|---|---|---|---|
| 1 | `0x00592BB0`; calls `0x005933CC`, `0x00593478` | initialises engine/plugins, then constructs `Init, Music, UI, SFX, Cozmo, Dev_Debug` unconditionally | fixed order before scene registration | strings/ids |
| 2 | `0x005935E2`, `0x005935EA` | registers `InitScene`, then loads it | register before load/event use | deeper result propagation **UNKNOWN** |
| 3 | `0x008D2EE8` → `0x008D2FE4`; archives `0x008D1E3E` | LoadAudioScene calls LoadSoundbank; AddZipFiles exposes OBB `AudioAssets.zip` | call-site order visible | loader internals **UNKNOWN** |

Current `LoadScene` applies the six-bank order only if all six are found, a test-compatibility fallback not present in the native constructor. Tests use shipped filenames/assets, but no production construction calls the loader.

## Required integration order

The smallest source-owned integration sequence is:

1. Construct/load `WwiseSoundLibrary` from M6-024 and M6-001 before any post.
2. Construct `WwiseEventRuntime`, `WwisePlaybackBridge`/M6-025, limiter/M6-026, voice linker and voice engine; bind every required seam or fail before accepting a post.
3. Make the node source factory choose the native-shaped ADPCM/Vorbis source; pass the two StartStream arguments and preserve `0x2D/0x2E/0x11/2/0x3F`.
4. Bind RTPC, gain, filters, resampler, mixer, bus lifetime and insert-FX/Hijack in native order.
5. Own the audio thread/frame driver and callback/Term order.
6. Route app/animation/behavior/singing posts through M6-023/M6-016 rather than `WwisePlayback.Resolve`'s offline parallel path.
7. Only then connect the final robot-audio chunks to `AnimationScheduler`/robot streaming.

Anything less leaves the exact standalone pieces separated by an unverified production assumption. Values explicitly marked UNKNOWN above must remain required seams or fail closed; none may receive a plausible default.
