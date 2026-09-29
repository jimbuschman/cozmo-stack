# Research request: the two M9 leftovers (M9-013's upstream hop, M9-024's owner check)

- **Date:** 2026-09-29
- **Requested by:** manager (Claude)
- **Kind:** independent extraction
- **Answer file:** `re-analysis/research/20260929-M9-leftovers-extraction.md`

## Why this is needed

Codex's M9 answers settled most of M9-013 and M9-024, and each has exactly one step left. R-BEH folds them in. Follow
`re-analysis/research/README.md` and `.opencode/agent/cozmo-extractor.md` exactly: one row per behaviour-changing step,
an address citation or UNKNOWN, and no guesses. This is the Wwise runtime: ARM code. Cite branch veneers and PLT hops.

## Questions

1. **M9-013, the music-track-to-target hop.** The shipped singing MusicTracks carry the Wwise-MIDI source
   (`plugin 0x00100001`), and their inherited music setting (property 56) names MIDI target 110896138. Find the
   post-load function that:
   - takes each event decoded by that source;
   - resolves the inherited property 56;
   - looks up object 110896138;
   - calls its `+0x128` method (`0x009D0758` for type 9).

   Name every step with its addresses, from the MIDI source's event output to that call.
   Known: `20260929-M9-013-014-midi-routing-velocity-extraction.md`, rows 1..6.
2. **M9-024, the owner field on the singing path.** Cleanup `0x009E21FC` takes the direct attached-voice stop loop
   (`0x009F4A64(..., 4)` over `voice+0x10` / `+0x14`) only when `voice+0x20` is null. Is `voice+0x20` null on the
   per-voice modulator objects created for the singing sampler's voices? Trace the writers of `voice+0x20` from the
   creator `0x009D7FC0` and the initialiser `0x009E266C`, and the singing voice creation path.
   Known: `20260929-M9-024-note-off-envelope-extraction.md` and `20260929-M9-024-default-addendum.md` (property 1
   defaults to 1, so the stop branch is reached).

## Out of scope

Any other record, and any code, inventory or manifest edit. No branches, commits or pushes.

## Answer format

The table from `.opencode/agent/cozmo-extractor.md` (`step | what the original does | citation | record |
classification`), then contradictions, weak evidence, and open questions.
