# I-M9 gap pass 2 — LFO waveform consumer

Read-only extractor pass, 2026-09-28. Citations are ARM-mode virtual addresses in
`resources/lib/armeabi-v7a/libcozmoEngine.so`.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| L1 | The type-21 vtable selects `0x009D671C` as its update method. The function reads waveform/type, frequency, depth, smoothing, PWM and phase properties, applies ranged randomization with the Wwise 64-bit LCG, and updates its phase/state block. | type-21 vtable `0x0103B200 = 0x009D671C`; body `0x009D671C..0x009D7727` | M9-025 | EXACT_SOURCE for properties/state |
| L2 | Waveform/type is stored at output `+0x24`; phase/frequency state occupies `+0x34..+0x48`. Type 1 adds a quarter-cycle and type 3 adds a half-cycle before wrapping the phase to `[0,1)`. | stores and phase branches in `0x009D6918..0x009D7727`; specifically type comparisons and phase offsets `0x009D7480..0x009D74BC` | M9-025 | EXACT_SOURCE for phase preparation |
| L3 | The shared voice-instantiation path calls the modulator virtual methods and creates a per-voice state object, but this pass did not identify the later function that converts the prepared phase/type into the actual waveform sample. | `0x009D7FC0..0x009D8137`; `0x009E266C..0x009E2813` | M9-025 | RECOVERABLE_GAP |

Exactly what remains: follow the type-21 per-voice object created at `0x009D7FC0`
(size `0xAC`, vtable `0x0104B268`) through its render/update vtable calls, then map
the reads of the copied LFO state to sine/triangle/square/saw output. The property
map and phase offsets alone do not establish the waveform equation.

M9-025 remains `RECOVERABLE_GAP`.
