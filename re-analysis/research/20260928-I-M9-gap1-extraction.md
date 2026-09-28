# I-M9 gap pass 1 — envelope runtime

Read-only extractor pass, 2026-09-28. Citations are ARM-mode virtual addresses in
`resources/lib/armeabi-v7a/libcozmoEngine.so`. The decompilation was used only for
navigation.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| E1 | HIRC types 21 and 22 are constructed by the same factory. Type 21 selects the vtable at `0x0103B1E8`; type 22 selects `0x0103B218`. | factory `0x009D7B6C..0x009D7C97`; vtable words `0x0103B1E8..0x0103B23F` | M9-006 | EXACT_SOURCE |
| E2 | The type-22 update method is `0x009D5934`. It reads the envelope property bundle and ranged-property bundle, applies RTPC overrides, advances its stage state and writes the current normalized result at output `+0x18`. | type-22 vtable `0x0103B230 = 0x009D5934`; update `0x009D5934..0x009D6598`; final clamp/store `0x009D6570..0x009D6598` | M9-024 | EXACT_SOURCE for the property/update path |
| E3 | Property 15 is read by a separate type-22 virtual method. Default/0 selects only property value 1; other event states distinguish property values 1 and 2, including MIDI-status bytes `0x80` and `0x90`. | type-22 vtable `0x0103B23C = 0x009D552C`; property-15 scan and branches `0x009D552C..0x009D55F3` | M9-024 | EXACT_SOURCE for the branch, RECOVERABLE_GAP for its production meaning |

The missing fact is still whether this return value causes `cozmo_singing_note_off`
to stop the attached voice. The next extraction must trace the virtual `+0x24` call
from `0x009D7FC0` through the per-voice object constructed by `0x009E266C`, and then
identify the caller that consumes the boolean from `0x009D552C`. Property-name
guessing from an SDK version is not evidence.

No existing citation failed in this pass. M9-024 remains `RECOVERABLE_GAP`.
