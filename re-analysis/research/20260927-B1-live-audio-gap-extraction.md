# B1 live-audio gap extraction: pass 1

Scope: the two bounded `MISSING` items found before B1 wiring: the M6-010
send thresholds and M6-015 cross-frame Hijack accumulation. Primary source is
`resources/lib/armeabi-v7a/libcozmoEngine.so`. Existing inventory prose was
used only to identify addresses; every conclusion below was re-read from the
binary.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| B1-G1 | The STMG reader passes its first binary32 value and priority `2` to `0x009A080C`. That setter accepts values in `[-96.3,0]`, stores the raw dB value in the global reached at `0x009A08E4`, computes both `powf(10, value*0.05)` and the runtime fast-power approximation, and stores their maximum in the global reached at `0x009A08F8`. For shipped Init.bnk value `-80`, the raw user-send threshold is `-80.0f` and the game-send/connection threshold is the binary32 `0x38D1B717` (`0.0001f`). | reader `0x009B0B20..0x009B0B4C`; range checks `0x009A080C..0x009A0830`; priority/store `0x009A0834..0x009A0860`; `powf` call `0x009A086C..0x009A0874`; fast-power path `0x009A0878..0x009A08CC`; stores `0x009A08D0..0x009A08FC`; consumers `0x009BD37C..0x009BD404` and `0x009BD5F0..0x009BD614` | M6-010 | EXACT_SOURCE for the setter and stored values; `powf` is phone-system-library behavior |
| B1-G2 | Hijack Init allocates `channels*4096` bytes and constructs one persistent output `AkAudioBuffer`: data pointer at core `+0x6C`, channel count at `+0x70`, max frames at `+0x78`, and valid frames at `+0x7A`, initially zero. Execute passes that same buffer to `CAkResampler::Execute` on every call. The resampler advances `uValidFrames`; Execute resets it to zero only after result `0x2D` (DataReady) or `0x11` (NoMoreData) has invoked the process callback. A partial result therefore remains in the same buffer across engine frames. Empty input returns `0x11`, causing the accumulated partial count (including zero) to be delivered once and then reset. | allocation/layout `0x008DBF76..0x008DBFB6`; reset `0x008DBFC2..0x008DBFCA`; Execute persistent-buffer setup/call `0x008DBFE8..0x008DC016`; result tests, callback and reset `0x008DC016..0x008DC034`; loop while input remains `0x008DC034..0x008DC038`; empty-input return `0x00A47178..0x00A47188`; linear kernel valid-frame stores `0x00A49FEC..0x00A4A020` | M6-015 | EXACT_SOURCE |

## Existing record judgement

- M6-010 remains `IMPLEMENTATION_GAP`, but its send-threshold caller input is
  now settled. The threshold used at `0x009BD368` for game-defined sends is
  the linear global; the user-send comparisons use the raw dB global.
- M6-015 remains `IMPLEMENTATION_GAP`, but the claimed cross-frame behavior is
  now settled. `WwiseHijackFx.Execute` currently flushes a partial result at
  the end of every non-empty call and therefore contradicts the native
  persistent `uValidFrames` behavior.

## Still open in B1

This pass does not settle or implement the M6-017 live PBI/voice construction
path or the remaining native SIMD arithmetic named by M6-011, M6-012 and
M6-013. Those are separate bounded extraction items; no behavior for them is
inferred here.
