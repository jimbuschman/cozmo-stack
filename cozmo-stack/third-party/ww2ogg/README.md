# ww2ogg packed codebooks

`packed_codebooks_aoTuV_603.bin` is vendored from **ww2ogg** by Adam Gashlin (hcs).

| | |
| --- | --- |
| Upstream | https://github.com/hcs64/ww2ogg |
| File | `packed_codebooks_aoTuV_603.bin` |
| Revision | `14ed9b0dd62e815a38702b5f03c57006cbe2501b` (2024-10-12) |
| Retrieved from | `https://github.com/hcs64/ww2ogg/raw/master/packed_codebooks_aoTuV_603.bin` |
| Size | 74,387 bytes |
| SHA-256 | `00a93eab267d281401b1efd54e888a2e183299b9e6c446c48d09f701a89d9d27` |
| Licence | BSD-3-Clause, see `LICENSE` |

Copyright (c) 2002, Xiph.org Foundation; copyright (c) 2009-2016, Adam Gashlin. The licence permits
redistribution in source and binary form provided the copyright notice, the conditions and the disclaimer
are preserved, which `LICENSE` in this directory does.

## What it is, and what it is not

This is **generic Vorbis codec reconstruction data**, not a Cozmo or Anki asset. It is a packed library of
standard Vorbis codebooks derived from aoTuV 6.03. Audiokinetic's Wwise encoder strips the codebooks out of
the Vorbis streams it produces and leaves only 10-bit indices into a library like this one, so the library
is what turns those indices back into a standard Vorbis setup header. Any tool that decodes Wwise Vorbis
needs one; nothing in it is specific to this game.

No Cozmo asset is vendored anywhere in this repository. The `.bnk`, `.wem` and OBB files stay local and are
excluded by `.gitignore`.

## Verifying it

```
sha256sum cozmo-stack/third-party/ww2ogg/packed_codebooks_aoTuV_603.bin
```

must print the SHA-256 above. `WwiseCodebookLibrary` checks the length and the internal offset table when
it loads the file, so a corrupted copy is rejected rather than silently producing noise.

## Format

The file is a concatenation of packed codebooks followed by an offset table. The last four bytes are a
little-endian offset to the start of that table; the table is a little-endian 32-bit offset per codebook,
and codebook *i* runs from `offsets[i]` to `offsets[i+1]`. So the number of usable codebooks is one less
than the number of entries in the table.

## Related dependency

The reconstructed Ogg Vorbis stream is decoded by [NVorbis](https://github.com/NVorbis/NVorbis) 0.10.5,
MIT licensed, copyright (c) 2020 Andrew Ward. It is a managed C# decoder taken as an ordinary NuGet
package, so it is not vendored here.
