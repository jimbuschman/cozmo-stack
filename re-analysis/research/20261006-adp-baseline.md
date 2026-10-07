| Job item | Coverage | Result |
|---|---|---|
| Shipped event/state/RTPC/timing reference renderer | PARTIAL | Existing stage harnesses reviewed; end-to-end renderer MISSING. |
| C# production output capture for the same input | PARTIAL | Existing render entry inspected; comparable chunk/timeline adapter MISSING. |
| Structure-first comparator | CHECKED | Built; 6 hand-stream tests pass. |
| Per-case and overall RMS/SNR/max error | CHECKED | Implemented after structure gate; no engine baseline pairs available. |
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

Unicorn is absent from this Python environment. Installing it alone would not establish
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
