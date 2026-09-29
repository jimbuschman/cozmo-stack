# M9-013 / M9-014 — MIDI routing and velocity extraction

Read-only extraction from `resources/lib/armeabi-v7a/libcozmoEngine.so`
(3.4.0-1204) and the shipped `Cozmo.bnk`. Addresses are ELF virtual
addresses. Ghidra output was navigation only; instruction claims below were
checked in ARM disassembly.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Music HIRC classes | The generic HIRC loader delegates types 10–13 to the registered music callback. Engine initialization installs `0x00984930`; that callback dispatches type 10 to the MusicSegment factory/reader, 11 to the MusicTrack reader, 12 to the MusicSwitch factory/reader, and 13 to the MusicPlaylist factory/reader. These are real post-load runtime objects, not opaque skipped blobs. | registration `0x0097D778..0x0097D780` -> setter `0x009A5E64..0x009A5E70`; loader callback `0x009B3EBC..0x009B3EEC`; callback switch `0x00984930..0x00984C18` (type branches at `0x00984948..0x00984968`) | M9-013 | EXACT_SOURCE |
| Shipped music source and target | In `Cozmo.bnk`, the singing MusicTracks contain the Wwise-MIDI source (`plugin 0x00100001`), and their inherited music setting names MIDI target 110896138. The post-load instruction path which extracts that inherited target ID and submits each parsed track event to it was not located in this pass. | `Cozmo.bnk` music objects (for example switch 914766641 -> playlist 476453346 -> segment 426278014 -> track 1072657690; property 56 target 110896138) | M9-013 | RECOVERABLE_GAP for this one upstream hop |
| Target object's event method | HIRC type 9 (the target's layer/blend class) uses vtable `0x0103B050`; its event/play slot `+0x128` is `0x009D0758`. The method receives the play context whose MIDI event is at `context+0x84`: status, channel, key and velocity/release velocity are the four bytes `+0x84..+0x87`. | type-9 vtable `0x0103B050` (`+0x128 = 0x009D0758`); entry/copy setup `0x009D0758..0x009D0858`, `0x009D092C..0x009D0A1C` | M9-013/M9-014 | EXACT_SOURCE |
| All target children are visited | `0x009D0758` walks the target's child array `+0x5C` for `+0x60` entries. For each child it copies the play context, looks the child up, applies the common MIDI filter, and on success invokes that child's virtual `+0x128`. It does not randomly choose one of the target's children. | loop bounds/setup `0x009D07F0..0x009D0858`; next child `0x009D092C..0x009D0954`; filter `0x009D0CA4..0x009D0CF8`; child virtual call `0x009D0B90..0x009D0BC4`; loop continuation `0x009D08B8..0x009D0928` | M9-013 | EXACT_SOURCE |
| Common channel/key/velocity filter | `0x009EE454` first checks the MIDI-channel mask (property 53). For note-like events it adds property 47 to the key, saturates to 7 bits, and requires the result within properties 49..50. It adds property 48 to velocity, clamps to 1..127, and requires it within properties 51..52. Missing properties use the runtime defaults. A passing filter writes the adjusted key and velocity back to the event and returns 1; failure returns `0x52`. | channel `0x009EE4B0..0x009EE4D4`; key offset/range `0x009EE540..0x009EE700`; velocity offset/clamp/range and writeback `0x009EE738..0x009EE8D0` | M9-013/M9-014 | EXACT_SOURCE |
| Shipped target-to-get-in result | Object 110896138's child list includes 462443456, 774902407 and **403781184**. Object 403781184 has no key- or velocity-range properties of its own. Therefore, for every note which has reached 110896138 and passes the target's channel mask, the loop attempts 403781184 and the common filter does not reject it on key or velocity. Thus the runtime evidence says **yes for the target-to-branch hop**: 403781184 receives the note event like the other children. | `Cozmo.bnk` HIRC objects 110896138 and 403781184; runtime loop/filter citations in the preceding two rows | M9-013 | EXACT_SOURCE for target -> branch; the music-track -> target hop above remains RECOVERABLE_GAP |
| Velocity's only per-voice publication | Voice construction copies 0x44 bytes of the play context, preserving MIDI status/key/velocity. For a nonzero-velocity `0x90` note-on it converts the velocity byte to float without normalising and publishes it to the RTPC registry under ID `0x81`, with the voice/game-object context. A zero-velocity note-on skips that publication and follows note-off semantics elsewhere. | context copy `0x00A00338..0x00A00380`; note-on test/value conversion/ID `0x81` call `0x00A00420..0x00A0046C`; wrapper `0x00A149C8..0x00A149FC` | M9-014 | EXACT_SOURCE |
| No hidden velocity gain | The copied per-voice velocity byte at `voice+0x1E7` is read only by the `0x81` publication above in the shipped runtime. Voice level is instead built from the node Volume/RTPC/modulator accumulation and converted as `gain = dBToLin(volumeDb) * muteFadeProduct`; the velocity byte is not an unconditional factor in that equation. | sole `voice+0x1E7` read `0x00A00420..0x00A00430`; audio-parameter recursion `0x009EF2D8..0x009F00B4`; final level `0x009FFD84..0x009FFE18`, `0x00A4B608..0x00A4B674` | M9-014 | EXACT_SOURCE |
| Shipped no-binding answer | No node on the shipped 110896138 sampler path has an RTPC whose source is MIDI velocity/ID `0x81`. Consequently publishing velocity finds no bank-authored level curve to apply. For such a bank, velocity contributes **0 dB**, hence a multiplicative gain of **`10^(0.05*0) = 1`**. Notes of different velocity have the same voice level unless some other authored property differs. | `Cozmo.bnk` RTPC lists under 110896138; RTPC registry keyed lookup/update `0x00A14494..0x00A149FC`; dB-to-linear application `0x00A4B608..0x00A4B674` | M9-014 | EXACT_SOURCE (runtime plus shipped-bank negative inventory) |

## Contradictions with the current record text

- M9-014's unresolved either/or can be settled: there is no implicit
  velocity-to-amplitude multiplication. Velocity is an RTPC source, so it changes
  level only when the bank authors a matching binding. The shipped sampler does
  not.
- M9-013 can be narrowed substantially. The target does not select only the
  note-on and note-off layers; its type-9 runtime method visits every child, so
  403781184 is not excluded merely because it is the get-in branch.

## Weak evidence

- The names “MusicSegment”, “MusicTrack”, “MusicSwitch” and “MusicPlaylist” are
  the standard type-number mapping corroborated by exact consumption of the
  shipped bank. The stripped binary has no RTTI names.
- The negative RTPC inventory is strong shipped-asset evidence, but would have to
  be repeated for any different bank before applying the M9-014 conclusion there.

## Open questions

1. The precise post-load function which takes each event decoded by the Wwise-MIDI
   MusicTrack source, resolves inherited property 56, looks up object 110896138,
   and calls its `+0x128` method remains **UNKNOWN**. Until that hop is cited,
   M9-013 as a whole remains RECOVERABLE_GAP even though the target-to-403781184
   branch is now exact.
2. The stripped symbolic name for RTPC ID `0x81` is unavailable. Its behavioural
   identity as note velocity is established by the only producer: the note-on
   event's fourth byte.
