# M9-025 extraction — LFO per-voice waveform

Independent read-only extraction for
`re-analysis/research/requests/20260929-M9-singing-gaps.md`, part 1. All code
citations are ARM-mode VAs in the shipped `libcozmoEngine.so`; Ghidra output was
used only for navigation.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| LFO runtime vtable | The type-21 HIRC object's vtable begins at `0x0103B1E8`; its +0x18 method is the LFO state builder `0x009D671C` and its +0x1C method is the shared per-voice creator `0x009D7FC0`. The request's `0x0104B268` address is not this vtable. | vtable words `0x0103B1E8..0x0103B208` (`+0x18 = 0x009D671C`, `+0x1C = 0x009D7FC0`) | M9-025 | EXACT_SOURCE |
| Type-21 per-voice allocation | The creator tests the modulator's virtual eligibility method, selects class kind 0, allocates `0xAC` bytes, runs the common constructor, installs the relocated derived vtable, clears +0x90..+0xA4 and +0xA8, then initializes the object through `0x009E266C`. The GOT entry used at `0x009D80E4..0x009D80FC` contains `0x0103B260`, so the installed vptr is `0x0103B268`, not `0x0104B268`. | `0x009D7FC0..0x009D7FFC`; kind/size `0x009D8058..0x009D8068`, `0x009D80C4..0x009D80DC`; vptr/state stores `0x009D80E0..0x009D8110`; init `0x009D8114..0x009D8124`; GOT word `[0x01040124] = 0x0103B260`; vtable `0x0103B268..0x0103B27C` | M9-025 | EXACT_SOURCE |
| Parent pointer and output block | Initializer `0x009E266C` stores the type-21 parent at per-voice +0x08. It invokes the per-voice vtable +0x0C method (`0x009E2818`), which returns per-voice +0x5C, then invokes the parent's +0x18 method. For type 21 that is `0x009D671C(parent, perVoice+0x5C, perVoice)`. Thus the LFO output/state block is exactly the type-21 per-voice object's +0x5C block. | parent store `0x009E26A8`; derived call `0x009E2774..0x009E278C`; parent +0x18 call `0x009E278C..0x009E279C`; `0x009E2818..0x009E281C`; type-21 vtable `0x0103B200` | M9-025 | EXACT_SOURCE |
| Field correspondence | Relative to the LFO output block (+0x5C), waveform is +0x24 (= per-voice +0x80), frequency is +0x28 (= +0x84), smoothing/filter input is +0x2C (= +0x88), phase/depth ramp input is +0x30 (= +0x8C), and oscillator carry is +0x34..+0x48 (= +0x90..+0xA4): coefficient 1, coefficient 2, previous output, phase, phase increment, and prior waveform selector. | type-21 stores `0x009D6918..0x009D74BC`; evaluator control reads `0x009E2EBC..0x009E2F00`; carry copy/reads `0x009E2E38..0x009E2EB4`; record stores `0x009E2EC4..0x009E2F18` | M9-025 | EXACT_SOURCE for offsets and uses; the friendly property names remain inference from arithmetic |
| Per-voice registration with evaluator | The evaluator walks its live voice list, reads the modulator kind through `[[voice+0x08]+0x10]`, and kind 0 enters the `0x4C`-byte LFO-record path. It allocates/reuses a `0x28`-byte carry slot, writes that pointer at voice +0x30, restores its six words +0x10..+0x24 into voice +0x90..+0xA4, then copies them into record +0x34..+0x48. This is the direct pointer chain from the type-21 per-voice object to the evaluator. | voice walk `0x009E2C4C..0x009E2D9C`; kind read `0x009E2CE0..0x009E2CF8`; carry allocation/assignment `0x009E31F4..0x009E32E0`; restore `0x009E2E38..0x009E2E94`; record copy `0x009E2EC4..0x009E2F18` | M9-025 | EXACT_SOURCE |
| Coefficients and increment | When dirty byte per-voice +0xA8 is set, frequency (+0x84) is limited against 48000 Hz and smoothing (+0x88) is transformed with `log`, `exp`, `cos`, and `sqrt` into `c1` at +0x94 and `c2` at +0x98. Phase increment is `min(frequency/48000,1)` at +0xA0, multiplied by `2*pi` for waveform 0. A waveform change converts the saved phase between radians and cycles using `2*pi` or `1/(2*pi)`, saves the new selector at +0xA4, and clears dirty. | `0x009E35C0..0x009E36B4`; constants 48000 and 24000 loaded at `0x009E2F90..0x009E2FA8`; `2*pi` / reciprocal conversion `0x009E3680..0x009E36A8`, `0x009E51B0` | M9-025 | EXACT_SOURCE |
| Phase advance and wrap | Every produced sample advances the persistent phase by the stored increment. Sine uses radians and wraps by subtracting `2*pi`; the other four shapes use cycles and wrap by subtracting 1.0. The phase floor used to derive chunk boundaries is `9.9999999e-09`. | shared setup `0x009E36B8..0x009E36D0`; per-shape loops `0x009E36D4..0x009E3780`, `0x009E3E1C..0x009E3EC8`, `0x009E3ECC..0x009E4008`, `0x009E4024..0x009E4168`, `0x009E416C..0x009E4358` | M9-025 | EXACT_SOURCE |
| Shape 0 — sine | The sine is unipolar. It folds phase into a first-quadrant argument and evaluates `shape = 0.5 * (1 + x*(0.9999966 - 0.16664828*x^2 + 0.008306325*x^4 - 0.00018363654*x^6))`, with the appropriate signs in the other quadrants. It is 0.5 at zero and 1.0 at `pi/2`. | switch target `0x009E3494 -> 0x009E416C`; folds `0x009E4174..0x009E4358`; polynomial `0x009E41E0..0x009E4210`; constants at `0x009E2FAC`, `0x009E3310`, `0x009E3324`, `0x009E400C..0x009E401C` | M9-025 | EXACT_SOURCE |
| Shape 1 — triangle | In cycle phase `p`, the shape is `2*p` for the first half and `2*(1-p)` for the second half. | switch target `0x009E3498 -> 0x009E4024`; first half `0x009E4090..0x009E409C`; second half `0x009E4118..0x009E4120` | M9-025 | EXACT_SOURCE |
| Shape 2 — square | The square is 1.0 for the first half-cycle and 0.0 for the second. No variable PWM threshold is applied in this waveform consumer. | switch target `0x009E349C -> 0x009E3ECC`; high `0x009E3F34..0x009E3F38`; low `0x009E3EDC`, `0x009E3FB0` | M9-025 | EXACT_SOURCE |
| Shape 3 — saw up | The shape is cycle phase `p`. | switch target `0x009E34A0 -> 0x009E3E1C`; sample arithmetic `0x009E3E84..0x009E3E8C` | M9-025 | EXACT_SOURCE |
| Shape 4 — saw down | The shape is `1-p`. | switch target `0x009E34A4 -> 0x009E36D4`; sample arithmetic `0x009E373C..0x009E3748` | M9-025 | EXACT_SOURCE |
| Final sample transform | All five shapes feed the same recurrence: `y[n] = gain[n] * shape(phase[n]) * c1 - y[n-1] * c2`. `gain[n]` is ramped per sample from the per-voice control values copied into the record; the result is written to the evaluator's float buffer and the last output/phase/coefficient state is retained in the `0x28`-byte carry slot. There is no later bipolar remap, DC offset, polarity inversion, or PWM-duty operation inside this evaluator. | switch `0x009E3488..0x009E34A4`; scalar recurrences in each case above; gain-ramp setup `0x009E3450..0x009E3488`; record/carry layouts `0x009E2EC4..0x009E2F24`; carry writeback `0x009E37D0..0x009E37F4` | M9-025 | EXACT_SOURCE for evaluator output |
| Delivery beyond evaluator | The evaluator exposes the generated float buffer through its chunk/state records to the voice engine. The exact later arithmetic that combines this buffer with each destination Wwise parameter was not re-traced in this bounded pass; therefore no claim is made that a destination outside this evaluator cannot add its own scaling. | buffer allocation/record attachment `0x009E2F4C..0x009E315C`, evaluation `0x009E337C..0x009E37F4`; downstream destination consumer UNKNOWN | M9-025 | RECOVERABLE_GAP: trace consumers of the evaluator chunk float-buffer pointer after `0x009E2BD0` |

## Contradictions with the current record text

- The unresolved text names vtable `0x0104B268`. The actual relocated vtable
  installed by `0x009D7FC0` is `0x0103B268`; `0x0104B268` belongs to unrelated
  relocated application data.
- The current record says the per-voice waveform consumer is unknown. The complete
  edge is now established: type-21 parent → `0x009D7FC0` object → +0x5C output
  block → kind-0 record → `0x009E2BD0` waveform switch and sample loops.

## Weak evidence

- Names such as “frequency,” “smoothing,” “depth,” and “coefficient” are semantic
  labels inferred from the arithmetic. The offset flow and equations are primary
  evidence; those friendly names are not exported symbols.
- The earlier M6 report's shape arithmetic was checked against the ARM instructions
  in this pass. Its public-feature identification as an LFO is consistent with the
  type-21 path, but that report alone was not treated as evidence.

## Open questions

- The requested waveform and its transforms through the evaluator are settled.
- If M9-025 is intended to own destination-specific modulation after the generated
  float buffer, the consumers of the chunk buffer after `0x009E2BD0` still need a
  separate bounded trace. That downstream edge is UNKNOWN here.
