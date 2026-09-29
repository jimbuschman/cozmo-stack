# M9-024 addendum: the modulator property defaults (0x0108DAA8)

Manager extraction, 2026-09-29. It answers open question 1 of
`20260929-M9-024-note-off-envelope-extraction.md` (the default behind `DAT_0108DAAC`). Treat it like any extractor
report: the citations are to be checked before a row goes into the inventory.

## The writer

The table at `0x0108DAA8` is in `.bss` (section `.bss` 0x01059030, size 0x36F84), so it has no file contents. Its only
writer is the static constructor `.init_array` entry `0x004DDF0C`, which is ARM code (an even entry address):

- `0x004DDF0C ldr r3, [pc, #0x84]` loads the literal `0x00BAFB88` from `0x004DDF98`;
- `0x004DDF18 add r3, pc, r3` gives `0x00BAFB88 + 0x004DDF20 = 0x0108DAA8`.

A scan of the Wwise runtime range 0x0095E540..0x00AE2E40 finds 18 PC-relative references to `0x0108DAA8`, all in the
modulator code 0x009D5538..0x009D9798. All of them are reads.

## The values it stores

| index (property id) | offset | store | value |
| --- | --- | --- | --- |
| 0 | +0x00 | `0x004DDF24` | 0 |
| 1 | +0x04 | `0x004DDF48` | 1 |
| 2 | +0x08 | `0x004DDF64` | 1.0f |
| 3 | +0x0C | `0x004DDF68` | 1.0f |
| 4 | +0x10 | `0x004DDF6C` | 1.0f |
| 5 | +0x14 | `0x004DDF2C` | 0 |
| 6 | +0x18 | `0x004DDF88` | 0 |
| 7 | +0x1C | `0x004DDF8C` | 0 |
| 8 | +0x20 | `0x004DDF90` | 0 |
| 9 | +0x24 | `0x004DDF70` | 100.0f (`0x42C80000`) |
| 10 | +0x28 | `0x004DDF74` | 0.5f |
| 11 | +0x2C | `0x004DDF78` | 200.0f (`0x43480000`) |
| 12 | +0x30 | `0x004DDF7C` | 0.3f (`0x3E99999A`) |
| 13 | +0x34 | `0x004DDF80` | -1.0f (`0xBF800000`) |
| 14 | +0x38 | `0x004DDF84` | 1000.0f (`0x447A0000`) |
| 15 | +0x3C | `0x004DDF50` | 1 |

The readers index the table by property id (for example `(&DAT_0108daa8)[param_2]` in 0x009D9780 and 0x009D81BC; a
navigation aid only).

## What it settles

- **Property 1's default is 1.** `0x009D7EA4` reads `table+4` (`0x009D7EC4 ldr r3, [r3, #4]`) when the modulator
  doesn't store property 1, and no shipped modulator does. So cleanup `0x009E21FC` takes the stop branch
  (`0x009E222C bne 0x9E22E4`).
- **Property 15's default is 1:** ordinary starts only. This agrees with the report's E3 reading of `0x009D552C`.

## Still open for M9-024

Open question 2 of the report: whether `voice+0x20` (the alternate owner) is null on the singing creation path, so
that the direct `0x009F4A64(..., 4)` loop over `voice+0x10` / `+0x14` is the branch taken. That is the last step
before the shipped yes/no answer.
