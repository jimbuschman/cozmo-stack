# COZMO-VOICE-1b

## Independently checked inputs

- English voice Processing (event 3205213409, action 184678442) and Question
  (3777359272, action 126787062) both Play target 677877281. Unprocessed action
  753742139 targets 663268476. The raw action type is 0x0403. All three have
  isBus false and empty fixed/ranged property tables. 0x00A62984 reads fade
  curve 4 (linear) and bank ID 0x8E39A50B. 0x00A62D38 calls 0x00A62A1C
  without a probability draw. 0x00A61110 gives zero default fade time.
- English unprocessed node 70808982 binds two Pitch curves to 0x78DEBD05.
  Curve 0x000EE904 maps -1/0/1 to 0/0/400. Curve 0x27A1BB83 maps -1/0/1 to
  -100/0/420. Accumulation mode 1 sums from f32 zero in array order.
  0x00A11590 dispatches the sum at 0x00A17878, and 0x00A179A8 adds each value.
- Harmonizer creator 0x00AA32BC and parameter constructor 0x00AA5BC4.
  The parameter object Init is 0x00AA5BA0 and SetParamsBlock is 0x00AA5850.
  The shipped 68-byte block contains two 25-byte voice groups, then the
  tail controls. Voice cents use powf(2, cents × 0x3A5A740E), and dB fields
  use powf(10, dB × .05f). Init/Reset/Execute/Term are 0x00AA440C,
  0x00AA42E4, 0x00AA4674 and 0x00AA45A0.
- Expander creator 0x00ABCC84 and parameter constructor 0x00ABCF50.
  Parameter Init 0x00ABCEE8, SetParamsBlock 0x00ABCE7C. The shipped 22-byte
  block is five f32 values and two flags. The fifth value is output dB,
  converted with powf(10, dB × .05f). Init/Reset/Execute/Term are
  0x00ABC278, 0x00ABC1B8, 0x00ABC3C0 and 0x00ABC164.
- Init.bnk has external bus 2476517424, parent 1723505802, effect slot 0
  2313011259 with share-set flag 1 and bypass byte 0.

## Raw bank parse

External-language switch 677877281 defaults to English (0xC9A81C07), routes to bus
2476517424 at -5 dB, and maps English 114912805, German 53142486, Japanese
681861550 and French 315546930. Each language defaults to Name.

| Language | Name, Sentence, Unprocessed branches | Their default WavePortal Sound IDs | FX IDs in slot order |
| --- | --- | --- | --- |
| English | 259796656, 152131813, 70808982 | 96016403, 1036286935, 238863482 | 3538378849, 2157284052, 2814082681, 4014558717 |
| German | 744947571, 398651049, 708936835 | 377429557, 321170621, 144596750 | 1814121665, 3184602036, 3818899289, 4066938653 |
| Japanese | 485805870, 736611133, 703213608 | 350391193, 636294831, 826370165 | 775075376, 1905427213, 871429944, 4065554388 |
| French | 705165251, 1042141457, 519811384 | 120116475, 705682945, 686353943 | 1133333215, 3514404594, 1868673011, 3832251591 |

Main branch slots are Harmonizer, Expander, Compressor and Parametric EQ,
with bypass mask 0 and share-set flags 1. The local unprocessed descriptor
has custom overrides in slots 1–3 and no slot 0 entry. The override byte at 0x009ECAF8 takes the local-only branch at 0x009EEF44–0x009EEF80. Its absent slot 0 returns null at 0x009EF060. Name and Sentence take the parent fallback. Name and Unprocessed
have a SilenceGenerator mock. Sentence has an empty mock assignment. English
Name and Sentence have the zero LFO curve and 0-to-400 cents external pitch.
French Name's additional envelope 870948569 targets Volume, not Pitch.
The separate direct Unprocessed switch 663268476 defaults to WavePortal
Sound 511843007 and has its own -100-to-420 cents curve.

## Independent native DSP corpus

The Python FxEngine ran both effects with real English bank blocks and the native
voice context contract. The context constructor at 0x00A6C22C installs a vtable
whose +0x2C slot resolves to 0x00A6B554 and returns false. The engine processes
the inserted effects in ascending slot order (0x00A44694–0x00A446DC).

The corpus uses mono 48 kHz, channel word 0x4101, five 256-frame blocks with
xorshift32 seeds 7–11, then a 199-valid/256-capacity end buffer with seed 12.
Expander retains the final frame's 199 valid samples and state 0x11. Harmonizer
pads it to 256 and changes state to 0x2D (0x00AA8CF0, 0x00AA9170–0x00AA9198).
This header change is part of the timing contract. The ARM DSP and parameter code runs directly. The phone libm is absent. Host powf and trig stand in for native imports, including dB and cents conversions and FFT setup. Bit equality is established only for the checked corpus.

Reader requested gpt-6.1-sol medium. Builder and reviewer use the configured default.
Actual backends are unobserved. Independent review passed after narrowing the supported format and checking frame headers.

## Build, tests and MISSING

WwiseNativeVoiceEffect.cs runs the engine bodies against the validated bank
blocks and mono 48 kHz voice context. WwiseNativeVoiceEffectTests.cs checks
frame headers and hashes, slot insertion through the standalone Wwise voice
factory seam, unsupported format, and early termination. The final focused
run passed 5/5. The combined run before the final early-termination tests
passed 10/10, including both unchanged bit-exact emulator oracle classes.
The manager reran re-analysis/tools/emu/emu_voice_fx.py locally and reproduced
all 12 hashes and frame headers. It uses the pinned shipped engine and local
Python dependencies. No sound bank was uploaded.

MISSING: the live voice registry, frame/mix/Hijack graph and playback timing
are not connected. The adapter is limited to the checked English blocks,
48 kHz mono, and the tested voice context. Other locale/format/parameter
paths and further tail cases are outside the checked corpus. No status,
approval or hardware claim changes.

Batch full gate on 4487b1fa failed (7801/7803, zero skipped). The old stock-detector availability fixture was corrected and its follow-up passed 61/61. The identical SolvePnP oracle mismatch also reproduces on origin/main a9dbec14. The corrected gate ran on 601b4475 after rebasing onto 1771234b. All 7803 tests passed with zero failures or skips. This bounded handoff is READY FOR CLAUDE.