| Job item | Coverage | Result |
|---|---|---|
| Shipped event/state/RTPC/timing reference renderer | PARTIAL | Existing stage harnesses reviewed; end-to-end renderer MISSING. |
| C# production output capture for the same input | PARTIAL | Existing render entry inspected; comparable chunk/timeline adapter MISSING. |
| Structure-first comparator | CHECKED | Built; 6 hand-stream tests pass. |
| Per-case and overall RMS/SNR/max error | CHECKED | Implemented after structure gate; no engine baseline pairs available. |
| Q13 strict ARM mixer component corpus | CHECKED | 128 actual calls; 26,736 float samples; component structure agrees; RMS/max error 0, SNR infinity. Not a full-stream pair. |
| Q13 strict ARM EQ biquad component corpus | CHECKED | 64 actual calls; 3,873 buffer words, 3,574 active samples; sample/history error 0. Not a full-stream pair. |
| Q13 strict ARM LPF/HPF component corpus | CHECKED | 32 actual calls with synthetic stored coefficient blocks; error measured below. Production reachability of those blocks requires VERIFY. |
| Mono Vorbis / stereo Vorbis to mono corpus | PARTIAL | Native decode fixtures identify media; event and complete stream capture MISSING. |
| ADPCM corpus | PARTIAL | Shipped media covered by existing decoder tests; complete engine event capture MISSING. |
| Volume/RTPC / pitch-resampling | PARTIAL | Component oracles exist; complete scheduled render MISSING. |
| LPF/HPF / EQ / limiter / compressor | PARTIAL | Component oracles exist; complete bus-to-output path MISSING. |
| Simultaneous voices / limits-ducking / continuous-crossfade | PARTIAL | Event/control and bus wiring require checked rows and a native renderer. |
| Singing | PARTIAL | Deferred as job permits until the M9 production path can run. |

Queue 3 Q3, `jobs/B-ADP-HARNESS.md`. **BLOCKED after one comparator batch; no equivalence claim.**
The job explicitly permits MISSING for engine stages that cannot yet run. This report
does not reinterpret mocked component execution as a full engine reference, and it
sets no threshold. No fidelity status changed.

The tested tooling is `tools/emu/adp_compare.py`. It validates rate22320, mono s16le,
744-byte chunks (=372 samples), consecutive absolute positions, equal case inputs,
frame counts, boundaries, start/end positions and exact zero/nonzero regions. It
does not align streams. Only a passing structure comparison reaches RMS error,
maximum absolute error and SNR. Overall metrics sum energies/counts before division.
Six tests use hand-specified streams; their results are comparator checks, **not
engine-versus-C# audio measurements**.

## Missing stages and why existing tools do not provide the reference

| Stage | Available checked source / tool | MISSING |
|---|---|---|
| Event to initialized runtime graph | M6 inventory C40/C41; `tools/emu/emu_bankload.py`, `emu_play.py`; `jobs/status/B-M6b-4.md` batch5n | Bank loader/registry and RanSeq/Switch/Layer/State/Attenuation/music recipients have required seams. No complete production LoadBank caller is wired. An event-id-only replay would invent graph delivery. |
| Per-voice tick to source/filter/FX | C41.6: 0x00A55A84, 0x00A55D04, 0x00A55CC4, 0x00A56414, 0x00A56478, 0x00A47528; `emu_v7.py` header | V7 executes its own body but replaces gain update 0x00A4BC58, ducking 0x00A4B4B0, FX creation 0x00A54A30 and context calls with input-dependent stubs. These affect exact decisions/state; they cannot be silently replaced in an ADP reference. |
| Bus FX/aux/ducking to Hijack | B-M6b-4 status5g/5k/5o; 0x008DBD82, 0x008DBAA2, 0x008DBB5E, 0x008DBC36; SetupEnginePlugInFx veneer0x00AE3090 | Bus-to-Hijack and rate/chunk binding remain required seams. Existing output arrays from a decoder or FX Execute do not establish native robot chunk scheduling. |
| C# live render and absolute stream capture | `cozmo-stack/src/Cozmo.Conformance/Wwise.cs:219` calls RenderMusic; WwiseAudioSource/music implementation; B-M6b-4 status5m/5n | Existing offline music renderer returns an aggregate PCM array, not a common event/state/RTPC/timing session with captured absolute 744-byte chunk boundaries. Do not recut that array and claim engine framing. |
| Native decode only | `tools/emu/emu_vorbis.py`: 0x00AB6380, 0x00AB63E0, 0x00AB3264, 0x00AB7E40 | This can provide decoder PCM, with documented allocator/import replacements. It does not execute event selection, RTPC delivery, resampling, mixing, FX, routing or robot framing. |

The existing native decoder fixture `cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/vorbis_native.txt`
names mono media1004941006 (45753 planar float samples) and stereo456481546 (15404 floats).
Those are corpus leads, not selected event sessions or an ADP stream baseline. Each other
corpus category still needs a shipped event/graph whose actual path exercises it, then
both captured renders; choosing a synthetic signal is insufficient for the requested corpus.

Unicorn 2.1.4 is now available through the local research dependencies. That does not establish
the missing downstream code or approved harness rows. No external banks were uploaded,
no robot was used, and no output from a Python model was labelled engine output.

## Measurements

Engine/C# paired cases: **0**. Structure results, RMS/SNR/max error: **NOT MEASURED**.
The hand-stream unit case has reference10/candidate12 for372 samples: RMS2,
max2, SNR13.979400086720375dB; it verifies the comparator arithmetic only.
No baseline threshold can be chosen from it.

Self-review: live render is explicitly MISSING, no parallel production renderer added,
no runtime branch guessed; structural gates precede metrics; no float constant was
ported from a rounded engine value; expected unit-test energies derive independently
from integer streams; no fidelity record is settled. Fidelity check passes (445 records).
Full-suite result and batch commit are recorded in `jobs/status/B-ADP-HARNESS.md`.

## Q13 extension, 2026-10-07

The new strict generator is `tools/emu/emu_adp_strict.py`, using `emu_m1_m5_numeric.Native` to map the shipped ELF and execute ARM. Its mixer entry is `0x00A46668`; no callee, math import or pool allocator is substituted on these calls. Engine SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. FPSCR is reset to zero. Expected words in `cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/adp_strict_mixer.json` are copied from emulated output memory. Inputs are fixed seed `0x20261007`, explicit binary32 source/destination words, gains and increments: ±zero, negative/positive gains, normal and subnormal increments; lengths 8, 16, 32, 64, 128, 1024. These are synthetic component inputs, not shipped event sessions.

`AdpStrictMeasurementsTests` calls the existing WwiseMixKernels.RampAccumulateA46668. It checks component frame length and exact zero/nonzero regions before measuring error. All 128 cases / 26,736 samples pass those component gates. Every measured RMS and maximum absolute error is 0; every SNR is infinity. Per-case measurements are in `20261007-adp-mixer-measurements.json`. No PCM threshold is selected or asserted. This does not verify a routing/voice-limit decision, start time, end time or robot chunk boundary.

Two further pure bodies run without substituted callees: EQ biquad `0x00AA2324` (64 calls) and ready LPF/HPF `0x00A766F0` / `0x00A77480` (32 calls). Fixtures `adp_strict_biquad.json` / `adp_strict_filters.json` contain all input/output words and history. Mono/stereo, zero/one/two/three-frame and block/tail lengths, padding, scalar alignment and bypass are exercised. The C# adapters call the existing private EQ body via reflection or the existing public filter-band Process; they supply the same stored coefficient/history inputs. They do not reconstruct parameter selection, Design, Init or the production graph. No parallel DSP implementation is added.

| Component | Cases | Measured buffer words (active samples) | Overall RMS | Maximum absolute error | Overall SNR dB | Maximum history error |
|---|---:|---:|---:|---:|---|---:|
| Mixer | 128 | 26,736 (26,736) | 0 | 0 | infinity | n/a |
| EQ biquad | 64 | 3,873 (3,574) | 0 | 0 | infinity | 0 |
| LPF/HPF synthetic stored blocks | 32 | 1,212 (1,064) | 0.5078717734546343 | 1.2194648385047913 | 0.762855520887886 | 1.3704183399677277 |

Per-case results are `20261007-adp-biquad-measurements.json` and `20261007-adp-filter-measurements.json`. All component buffer-length and zero/nonzero gates pass. RMS/SNR weight each measured buffer by its word count, including unchanged padding; active counts are listed separately. Filter F blocks are arbitrary finite caller input words, not demonstrated outputs of native Design; **VERIFY their production-reachable coefficient invariants before using these differences to assess equivalence**. These large differences are synthetic component observations, not established audible errors on shipped sounds. No filter code is changed and no threshold is derived from them. All native expected words still come directly from shipped execution.

| Missing baseline path | Coverage | Strict extension / reason it remains missing |
|---|---|---|
| Mixing/gain inner loop | CHECKED | 128 complete shipped kernel calls and C# measurements above. Production mix routing and matrix selection remain outside this component. |
| LPF/HPF | PARTIAL | 32 strict ready-body cases added; stored-block reachability requires VERIFY. Twelve shipped cutoff-map calls at `0x00A7A3D8` / `0x00A7A4AC`, including NaN/infinity, are retained as boundary observations in `20261007-adp-boundary-probes.json`. Full Design/ramp path still imports phone tanf; no strict full-path PCM pair. Cutoff values/choices remain exact, not ADP arithmetic. |
| EQ | PARTIAL | 64 pure biquad cases added. Actual `0x00AA25E0` coefficient call with explicit rate22320 and input record stops at unsupported phone `tanf`. Failed call is retained, without fabricated coefficient output. Existing `emu_eq.py` supplies a libm model; it is not adopted as an execution-only reference. |
| Compressor/limiter | PARTIAL | Existing `emu_comp.py` / `emu_limiter.py` use pool/FX libm adapters. Linked limiter P1/P3 and full bus/ducking selection lack a strict initialized graph. No new complete reference session. |
| Pitch/resampler | PARTIAL | `emu_pitch.py` replaces powf and source/context/filter/notification callees. Complete source-to-output timing remains missing; existing component fixtures do not become production sessions. |
| Mono/stereo Vorbis; ADPCM | PARTIAL | Existing decode evidence retained. No event/source/filter/mix/output capture with native chunk and absolute positions. Decoder output is not relabelled as a rendered event. |
| Simultaneous voices, limits/ducking, crossfade | PARTIAL | Exact decision/control context missing; synthetic mixer buffers cannot settle it. |
| Singing | PARTIAL | No M9 initialized decision/scheduling session; no upper-layer implementation attempted. |

Full engine/C# paired streams remain **0**, with structure/RMS/SNR/max error **NOT MEASURED** for those streams. In particular, the comparator's canonical s16le 744-byte / 372-sample representation has no native robot-framing bridge yet; do not treat that harness schema as verified native wire framing. No existing category is dropped and no record is settled. Q13 advances the component corpus and remains PARTIAL for the complete audio path. Self-review: expected samples only from actual shipped ARM; no audio production edit, no invented timing/routing, no PCM threshold, no equivalent status claim. Validation/publication are logged in `jobs/status/CODEX-Q4.md`.
