# I-M9 gap pass 3 — MIDI dispatch, RNG conclusion and citation verification

Read-only extractor and adversarial verifier pass, 2026-09-28. Citations are
virtual addresses in `resources/lib/armeabi-v7a/libcozmoEngine.so`; engine code is
Thumb and the Wwise region is ARM.

## MIDI dispatch gap

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| M1 | `0x009B3260` is the HIRC object-loader switch. Its `0x009B3EBC` block is the fallback/custom loader path, and `0x009BBF9C` is a bounded stream-read helper. They do not establish MIDI event routing or velocity-to-level behavior. | object-loader `0x009B3260..0x009B4033`; fallback `0x009B3EBC..0x009B3F64`; reader `0x009BBF9C..0x009BC17B` | M9-013/M9-014 | RECOVERABLE_GAP |
| M2 | `0x00A78D10` initializes a source object and its vtables/default fields; it does not establish the two requested MIDI semantics. | `0x00A78D10..0x00A78DE3` | M9-013/M9-014 | RECOVERABLE_GAP |
| M3 | Wwise owns the selection RNG. It advances the 64-bit LCG `s = s*0x5851F42D4C957F2D + 1` and uses the high word shifted right by one. The old M9-015 provenance that the engine supplies the generator is contradicted. | `0x0098A780..0x0098A7B8` | M9-015 | EXACT_SOURCE for RNG ownership; the full per-play conclusion must be compared during implementation |

Exactly what remains for M9-013/M9-014: identify the runtime object's MIDI-event
entry from the music/actor-mixer vtables reached after HIRC load, trace note-on and
note-off child filtering through the singing MIDI target, and follow the velocity
byte into every voice gain/RTPC input. The three X4 candidates were read and do not
settle either claim. These two records remain `RECOVERABLE_GAP` after the third and
final allowed gap pass.

## Verifier citation check

Every status-changing or contradictory item was opened in the `.so`: M9-007
(`0x00A14F88..0x00A15038`), M9-013/M9-014 (runtime factory and candidates above),
M9-015 (`0x0098A780..0x0098A7B8`), M9-017 (`0x006354E8`,
`0x00635558..0x0063556E`), M9-022 (`0x0098A6D4..0x0098A7B8`), M9-024
(`0x009D552C`, `0x009D5934`), M9-025 (`0x009D671C`) and M9-026
(`0x00AA257C`, `0x00AA18F4`). They support X4's corrections except that X4's
description of `0x009B3EBC` as a possible music handler is rejected: it is an
object-loader fallback and is retained only as a failed search location.

The verifier also opened 21 additional production rows: S1–S21. The checked
instruction ranges were `0x005EE8DC..0x005EEA0F`,
`0x005EEB30..0x005EEB6F`, `0x005EECB0..0x005EED6F`,
`0x005EEDD0..0x005EEE9F`, `0x005EF0C8..0x005EF25F`,
`0x005EF2B0..0x005EF30F`, `0x005EF490..0x005EF4DF`,
`0x00599F60..0x00599FBF`, and `0x00635474..0x006355BF`.
Widths, ordering, constants, first-listener gating, reset behavior and return values
match the report. No other row failed.

Verifier verdict: **PASS**, with the corrected characterization of `0x009B3EBC`
above and with M9-013, M9-014, M9-024 and M9-025 kept visible as
`RECOVERABLE_GAP`.
