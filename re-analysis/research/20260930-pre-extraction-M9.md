# R-M9 pre-extraction: Wwise music and singing gaps

- **Date:** 2026-09-30
- **Answers:** `re-analysis/jobs/R-M9.md`
- **Scope:** every current `IMPLEMENTATION_GAP` and `RECOVERABLE_GAP` record in `M9-wwise-music` (21 records).
- **Primary package sources:** `resources/lib/armeabi-v7a/libcozmoEngine.so`; `re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip` and its shipped `Init.bnk`/`Cozmo.bnk`; shipped Unity sources/configs.
- **Discipline:** research only. `UNKNOWN` is not an implementation suggestion. `BLOCKED_EXTERNAL` is used only where code or required data truly does not ship.

The quoted text is the current manifest title/status/evidence/unresolved. The rows fold in the manager-checked M9 gap reports named by R-M9, including the later MIDI-routing, note-off-envelope, LFO-waveform, default-property, and leftovers extractions. Wwise is statically linked into `libcozmoEngine.so`; its behavior is not external merely because it is middleware.

## M9-002

> **Current record:** `IMPLEMENTATION_GAP` — “Singing initialization posts the switch, locks reactions, then runs get-in/tempo/get-out as three TriggerAnimationActions with 60-second per-step timeouts in one sequential compound.” Evidence: 0x005EEB30..0x005EEE9F; 0x005EEDCA; 0x00540D1C; 0x0054F70C. Unresolved: a missing trigger fails `0x0300000C`, but C# skips it; C# also adds an unrecorded render wait.

**C# entry:** `SingingBehavior.StartAsync` / singing initialization.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Init order | 0x005EEB30..0x005EEDC8 | Post switch first; acquire reaction lock; create one sequential compound. | group/state from behavior config. |
| Actions | 0x005EEDD6, 0x005EEE06, 0x005EEE4A; timeout store 0x005EEDCA | Append get-in 0x202, tempo trigger, get-out 0x203. Each TriggerAnimationAction receives its own timeout. | 60.0f=`0x42700000`. |
| Failure | 0x00544444..0x0054445E; 0x00540D1C; 0x0054F70C | Missing trigger -> `0x0300000C`; timeout -> `0x03000018`; sequential compound fails immediately on child failure. No render-completion wait exists. | C# must not skip a failed step. |

## M9-003

> **Current record:** `IMPLEMENTATION_GAP` — “Cube running means, vibrato smoothing/posting, duration log, the +0x84 acting-tag 2/1 return, and stop cleanup.” Evidence: 0x005EF0C8..0x005EF30F; 0x005EF490..0x005EF4DF; 0x005EF240..0x005EF24A. Unresolved: Stop posts literal zero and does not clear smoothed `+0x140`; C# clears it.

**C# entry:** `SingingBehavior.Update` and `Stop`.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Update | 0x005EF0EC..0x005EF17E | Max cube running mean; reset every listener; divide by 3000; clamp 0..1; `new=.5*old+.5*value`; store +0x140. | 3000=`0x453B8000`, .5=`0x3F000000`. |
| Post/return | 0x005EF184..0x005EF18C; 0x005EF240..0x005EF24A | Post RTPC `0xC20F49DF` every tick. Return 2 while acting tag +0x84 is zero, else 1; managers consume 0/1/2. | binary32 RTPC. |
| Stop | 0x005EF2BE..0x005EF2E0 | Post literal 0, then remove listeners. Do not overwrite +0x140, so next run smooths from the previous value. | zero=`0x00000000`. |

## M9-004

> **Current record:** `IMPLEMENTATION_GAP` — “Music switch containers, decision trees, meters and MIDI target.” Evidence: M6 bank inventory; `Cozmo.bnk` objects 914766641, 139286641, 602865028, MIDI target 110896138. Unresolved: target lookup must start at the track and obey override bits; current code starts at segment and ignores table-select behavior.

**C# entry:** `WwiseMusic` bank graph loader/resolver.

| step | address/asset | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Parse graph | `AudioAssets.zip/Cozmo.bnk`, objects above | Parse all switch containers, association tables, decision trees, meters and property 56 target. Preserve HIRC order/flags. | shipped bank is primary data. |
| Effective MIDI target | 0x00A3BDFC..0x00A3BEE4; failure 0x00A3CC48 | Start at MusicTrack; walk ancestors only while the target-override bit says so; property value 0 is failure. Apply table-select flag. | No external dependency; fully in package. |

## M9-005

> **Current record:** `IMPLEMENTATION_GAP` — “Singing MIDI sources use SMF division 9600 and the effective meter tempo.” Evidence: bank MIDI source plug-in `0x00100001`, division `0x2580`. Unresolved: tick-to-time 0x00A3F76C..0x00A3FBC0 and property 55 were unread; existing duration fit is not evidence.

**C# entry:** `WwiseMidi` SMF parse/tick conversion.

| step | address/asset | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Source | `Cozmo.bnk`, plug-in 0x00100001 | Parse embedded SMF; division is 9600 (`0x2580`). Preserve shipped event ordering. | bank header tempo alone is not the runtime tempo. |
| Time conversion | 0x00A3F76C..0x00A3FBC0; property 55 | Runtime combines ticks/division with effective meter/tempo property 55. | Exact property-55 conversion remains `UNKNOWN` until this range is rowed; not BLOCKED_EXTERNAL. |

## M9-006

> **Current record:** `IMPLEMENTATION_GAP` — “HIRC LFO and Envelope payloads and their runtime classes.” Evidence: factory 0x009D7B6C..0x009D7C97; vtables 0x0103B1E8/0x0103B218. Unresolved: property 15 is trigger selector, not stop-playback; stop gate is property 1, default 1.

**C# entry:** `WwiseModulator` HIRC parser and per-voice constructors.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Factory | 0x009D7B6C..0x009D7C97 | Type 21 creates 0x48-byte LFO/vtable 0x0103B1E8; type 22 creates Envelope/vtable 0x0103B218. | payload values from `Cozmo.bnk`. |
| Properties | 0x009D552C..0x009D55F0; 0x009D7EA4 | Property 15 selects trigger state (value 2 note-off). Property 1 is StopPlayback and defaults to 1 when absent. | Rename fields; do not reuse old enum. |

## M9-007

> **Current record:** `IMPLEMENTATION_GAP` — “The note-off envelope binding applies Wwise scaling 2 after its curve.” Evidence: 0x00A14F88..0x00A15038; bank object 381606890/binding 462443456. Unresolved: gate must reject note-on; scaling uses Wwise fast log, not `Math.Log10`.

**C# entry:** `WwiseSongRenderer` binding evaluation.

| step | address/asset | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Trigger | property-15 path above; bank binding | Envelope is instantiated for note-off state only; it must not run from note-on. | curve `(0,0)->(1,-1)` from bank. |
| Scale | 0x00A14F88..0x00A15038 | Apply curve, then scaling 2: negative `-20*fastlog10(1-y)`, positive `20*fastlog10(1+y)`. | 20.0f=`0x41A00000`; exact fast-log polynomial is shipped and must be transcribed, not `Math.Log10`. |

## M9-008

> **Current record:** `IMPLEMENTATION_GAP` — “The vibrato LFO binding depth is driven by the posted cube-shake parameter.” Evidence: 0x009D671C..0x009D7727; post 0x005EF184..0x005EF18C; bank objects 528935089/110896138. Unresolved: depth RTPC is evaluated only at per-voice initialization; C# re-reads every block.

**C# entry:** `WwiseSongRenderer` voice creation/LFO binding.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Post | 0x005EF184..0x005EF18C | Singing posts shake RTPC each behavior tick. | binary32. |
| Bind | 0x009D7FC0..0x009D8137; update 0x009D671C.. | At note/voice creation evaluate depth RTPC and initialize per-voice LFO. Do not re-read depth each render block. | subsequent phase evolves from captured depth/state. |

## M9-009

> **Current record:** `IMPLEMENTATION_GAP` — “Modulator bindings evaluate their curves and accumulate onto the named property.” Evidence: 0x00A6E848..0x00A6F133. Unresolved: delivery of evaluator buffer to voice and per-sample application were UNKNOWN.

**C# entry:** `WwiseSongRenderer` modulator-binding accumulator.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Evaluate | 0x00A6E848..0x00A6F133 | For each binding, evaluate its own curve/scaling and accumulate into the named target-property slot, preserving binding order. | binary32 accumulator. |
| Deliver | downstream voice path | Exact handoff of this buffer and whether each property is block-rate or sample-rate remains `UNKNOWN`; runtime ships, so RECOVERABLE, not external. | Do not assume per-sample application. |

## M9-010

> **Current record:** `IMPLEMENTATION_GAP` — “Singing note-on and note-off layers determine held-note lifetime.” Evidence: M6 bank inventory/singing sampler layers. Unresolved: code-2 PBI vslot +0x1C unread; note-off replays recorded Sound, while C# redraws; fade deferred.

**C# entry:** `WwiseSongRenderer` note table/note-off.

| step | address/asset | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Note-on | shipped `Cozmo.bnk`; selection calls 0x00A3EA3C/0x00A3DDF0 | Select at note fire; store the chosen Sound/node and voice/PBI with the note state. | layer loop/release data from bank. |
| Note-off | 0x00A3E6A8..0x00A3E728 | Look up held note and replay/use the Sound recorded at note-on; no container reselection or new RNG draw. | shipped MIDI order establishes pairings. |
| Lifetime | PBI vslot +0x1C; end path 0x00A3CEB4 | Code-2 entry lifetime/fade result remains `UNKNOWN` until vslot body is rowed. | Recoverable in `.so`, not BLOCKED_EXTERNAL. |

## M9-011

> **Current record:** `IMPLEMENTATION_GAP` — “Singing renders through the shipped Robot_Bus_1 EQ, limiter and Hijack chain.” Evidence: M6 bank inventory; 0x00AA257C/0x00AA18F4. Unresolved: C# runs chain at 22320; engine mixes at 48000 then Hijack resamples to 22320.

**C# entry:** `WwiseBusChain.Process` / live audio bridge.

| step | address/asset | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Graph | `Init.bnk`/`Cozmo.bnk` | Robot_Bus_1 order is EQ -> EQ -> peak limiter -> Hijack. | all settings ship. |
| Rates | Wwise mix path; M6 gapC 4.6/4.8 | Run EQ/limiter at 48000 Hz; Hijack converts to 22320. | 48000/22320 integer rates. No external block. |

## M9-013

> **Current record:** `RECOVERABLE_GAP` — “Whether Wwise routes MIDI notes into the singing sampler get-in branch.” Evidence: 0x009B3260..0x009B4033; 0x009BBF9C..0x009BC17B; 0x00A78D10..0x00A78DE3. Unresolved asks for runtime event trace to target 110896138/branch 403781184.

**C# entry:** `WwiseSongRenderer` MIDI target dispatch.

| step | address/asset | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Track target | `Cozmo.bnk`; 0x00A3DDFC target hop; music execution 0x00A70D90 call 0x00A70EF4 | MusicTrack resolves target 110896138 through the +0x128 call and dispatches MIDI in shipped event order. | target/branch IDs ship in bank. |
| Child filter | Layer path 0x009D075C..0x009D0CA0; 0x00A024B8 | Layer's own +0x84 is not MIDI status. Each child reads MIDI status from its copied params; get-in branch 403781184 receives matching note-on events. | This is now source-settled by leftovers extraction; not BLOCKED_EXTERNAL. |

## M9-014

> **Current record:** `RECOVERABLE_GAP` — “Whether MIDI note velocity implicitly changes voice level.” Evidence: same MIDI runtime ranges and gap3. Unresolved asks to trace velocity with no bank binding.

**C# entry:** `WwiseSongRenderer` MIDI voice initialization/gain.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Event | MIDI dispatch path above | Velocity byte travels in MIDI params into voice/note allocation. | u8 velocity. |
| Gain | per-voice initializer/owner path 0x009E2338..0x009E27F8 | No shipped velocity binding means no implicit velocity-to-level curve was found in the checked path; owner store is the only controlling store. | If final PBI gain consumer is not in the cited leftovers rows, mark that last multiplication `UNKNOWN`, not external. |

## M9-015

> **Current record:** `IMPLEMENTATION_GAP` — “Each play draws container selections afresh using Wwise's own LCG.” Evidence: 0x0098A6D4..0x0098A7B8. Unresolved: C# prewarms whole song; engine draws when each note fires.

**C# entry:** `WwiseAudioSource`/song voice construction.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| RNG | 0x0098A780..0x0098A7B8 | Wwise global state: `s=s*0x5851F42D4C957F2D+1`; output high word >>1. | integer 64-bit exact. |
| Timing | 0x00A3EA3C, 0x00A3DDF0 | Advance/select when each note fires. Do not consume future selections during prewarm. | Playback timing changes later draws. |

## M9-017

> **Current record:** `IMPLEMENTATION_GAP` — “Cube acceleration stream, high-pass filter and shake hysteresis drive singing vibrato.” Evidence: 0x005EECB0..0x005EED6F; 0x00635474..0x006355BF; 0x00636598..0x0063682F. Unresolved: first sample initializes HPF and yields zero; C# filters from zero and test expects wrong value.

**C# entry:** `CubeAccel` shake listener feeding `SingingBehavior`.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Listener/stream | 0x005EECF4..0x005EED56; 0x006354E8/0x00635558..0x0063556E | One listener per cube. First listener for object sends enable stream; later listeners do not. | ctor .5=`0x3F000000`, 2.5=`0x40200000`, 3.9=`0x4079999A`. |
| HPF | init 0x00636578..0x0063658C; update 0x00636598.. | First sample sets prev=x/output=0 only. Later `y=a*(y+x-prev)`, then prev=x, per axis. | preserve binary32 order. |
| Hysteresis | 0x0063679E..0x0063682F | Squared magnitude: start >3.9², continue >2.5²; callback receives squared value. | no sqrt. |

## M9-020

> **Current record:** `IMPLEMENTATION_GAP` — “A music clip uses BeginTrim and length and releases a held note at clip end.” Evidence: bank inventory/X4. Unresolved: clip filtering/release lacks runtime rows; end-of-PBI 0x00A3CEB4 only structural.

**C# entry:** `WwiseSongRenderer` clip window/end.

| step | address/asset | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Window | shipped MusicClip data | Apply BeginTrim/length to event scheduling in source tick/time domain. | exact fields ship in bank. |
| End | 0x00A3CEB4 and note-state path | End-of-PBI releases held note according to runtime note table. Exact fade/return path remains `UNKNOWN` until body is completed. | Recoverable, not external. |

## M9-022

> **Current record:** `IMPLEMENTATION_GAP` — “Wwise container selection uses recovered eligibility, blocked-list, random and sequence algorithms.” Evidence: 0x0098A6D4..0x0098A7B8; 0x00A08A44; 0x00A0A524. Unresolved: C# reruns selection on note-off; engine replays recorded node.

**C# entry:** `WwiseSongRenderer` container pick/note-off.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Pick | 0x0098A6D4..0x0098A7B8; random 0x00A08A44; sequence 0x00A0A524 | Apply eligibility/blocked list, recovered random or sequence state, Wwise LCG, then record chosen node/Sound. | exact integer algorithms ship. |
| Release | 0x00A3E6A8..0x00A3E728 | Note-off reuses recorded selection; it does not advance LCG, avoidance or shuffle state. | C# must remove second pick. |

## M9-024

> **Current record:** `RECOVERABLE_GAP` — “Whether the note-off envelope stops the voice it is attached to.” Evidence: 0x009D552C..0x009D55F3; 0x009D5934..0x009D6598; 0x009D7FC0..0x009D8137. Unresolved asks to trace property 15.

**C# entry:** `WwiseModulator` Envelope voice.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Trigger selector | 0x009D552C..0x009D55F3 | Property 15 selects note-on/off triggering; value 2 selects note-off. It is not StopPlayback. | integer enum. |
| Stop gate | 0x009D7EA4; default addendum | Property 1 is StopPlayback, default 1. Per-voice owner gate `voice+0x20` controls attached ownership. | no external data. |
| Terminal | 0x009D5934..0x009D6598 plus owner path | Envelope stage reaches terminal; with StopPlayback true it stops/releases the owned attached voice through the recovered owner path. | Fold as recovered correction; not BLOCKED_EXTERNAL. |

## M9-025

> **Current record:** `RECOVERABLE_GAP` — “The exact waveform produced by the Wwise LFO between its extrema.” Evidence: 0x009D671C..0x009D7727; 0x009D7FC0..0x009D8137; 0x009E266C..0x009E2813. Unresolved asks for vtable consumer/state equation.

**C# entry:** `WwiseModulator` LFO voice.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Prepare | 0x009D671C..0x009D7727 | Read type/frequency/depth/smoothing/PWM/phase, randomize through LCG, update +0x34..+0x48; type 1 adds quarter cycle, type 3 half cycle, wrap [0,1). | all binary32. |
| Sample | vtable 0x0104B268; downstream 0x009E266C..0x009E2813 | The exact phase/type-to-sample equation remains `UNKNOWN` in current checked rows. Runtime code is present and must be followed; do not call it external or substitute standard waves. | RECOVERABLE_GAP remains. |

## M9-026

> **Current record:** `IMPLEMENTATION_GAP` — “The shipped parametric EQ and peak-limiter arithmetic.” Evidence: 0x00AA257C/0x00AA2A84/0x00AA18F4/0x00AA0EB4. Unresolved: HP/LP association differs by one ULP at 333 Hz; polynomials/limiter release unchecked.

**C# entry:** `WwiseBusChain` EQ and limiter.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| EQ setup/DSP | create 0x00AA257C; coefficients 0x00AA25E0/0x00AA2870..0x00AA2898; execute 0x00AA2A84 | Preserve HP/LP coefficient association and exact binary32 polynomial/order per shipped plug-in. | one-ULP 333 Hz defect proves decimal/formula equivalence is insufficient. |
| Limiter | create 0x00AA18F4; setup 0x00AA19CC; DSP 0x00AA0EB4 | Transcribe detector, attack/release, gain and state update in execution order. | release arithmetic still needs row-level check; `UNKNOWN`, but shipped. |

## M9-027

> **Current record:** `IMPLEMENTATION_GAP` — “Robot_Bus_Eq_HiLowPass behavior at 14298 Hz and the robot output rate.” Evidence: bank inventory; 0x00AA25E0; `Init.bnk` setting. Unresolved: engine EQ runs at 48000, Hijack resamples to 22320; C# runs EQ at 22320.

**C# entry:** `WwiseBusChain` rate plumbing.

| step | address/asset | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| EQ | `Init.bnk`; EQ setup 0x00AA25E0 | Configure 14298 Hz low-pass against 48000 Hz Wwise mix rate; it is in band, not capped to 22320 Nyquist. | setting ships in bank. |
| Output | M6-018 gapC 4.6/4.8 | Process buses at 48000, then Hijack resamples to 22320. | fix depends on M6-017/018 shared live path. |

## M9-028

> **Current record:** `IMPLEMENTATION_GAP` — “RobotAudioClient dispatches singing parameters and switches to game object 7 on-robot or 6 off-robot.” Evidence: 0x00599F60..0x00599FBF. Unresolved: object 7 built; object 6 refused by M6-016 path.

**C# entry:** `AnimationAudio`/`WwiseRobotAudioPath` dispatch.

| step | address | production behavior, gates/order/failure | floats/assets |
|---|---|---|---|
| Dispatch | 0x00599F60..0x00599FBF | On-robot object 7, off-robot object 6. Parameters use vtable +0x18; switches +0x14; transition time/curve zero. | zero transition exact. |
| Missing route | C# M6-016 path | Build object-6 OnDevice route; do not throw/refuse. | shared M6 owner. |

## BLOCKED_EXTERNAL assessment

**None of these 21 records is properly `BLOCKED_EXTERNAL`.** The Wwise runtime, DSP, MIDI/container/modulator code and required banks are shipped in `libcozmoEngine.so` and `AudioAssets.zip`. Remaining `UNKNOWN`s (property-55 time conversion, evaluator-buffer delivery, PBI lifetime/fade, LFO sample equation, exact limiter release, final velocity gain use) are recoverable native-code questions. M9-023, outside this request, remains `HARDWARE_ONLY` because the package contains no stock phone/robot acoustic recording; that absence does not make any runtime record external.

## Build order implied by the paths

1. Fix behavior-level ordering/state: M9-002, M9-003, M9-017.
2. Fix bank graph/MIDI target/time parsing: M9-004, M9-005, M9-013, M9-014.
3. Fix modulator identity/trigger/lifetime: M9-006..010, M9-024, M9-025.
4. Fix selection timing and recorded note-off node: M9-015, M9-020, M9-022.
5. Move the shared bus to 48 kHz, transcribe DSP, then resample: M9-011, M9-026, M9-027.
6. Complete the off-robot object-6 dispatch: M9-028.
