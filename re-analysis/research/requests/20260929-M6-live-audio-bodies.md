# Research request: the unread bodies between a created voice and audible output (M6, B-M6b-3's block)

- **Date:** 2026-09-29
- **Requested by:** manager (Claude)
- **Kind:** independent extraction
- **Answer file:** `re-analysis/research/20260929-M6-live-audio-bodies-extraction.md`. Write each item as it finishes.

## Why this is needed

B-M6b-3 built the Play -> PBI -> voice -> source bridge (M6-025, commit `ca3a969`). It then blocked, correctly, because
the rest of the path to audible output runs through engine bodies nobody has read
(`re-analysis/jobs/status/B-M6b-3.md`, "Why it is blocked"). This request reads them. The build that follows wires the
Wwise runtime into the robot's live audio.

Follow `re-analysis/research/README.md` and `.opencode/agent/cozmo-extractor.md` exactly: one row per
behaviour-changing step, an address citation or UNKNOWN, and no guesses. This is the Wwise 2016.2 runtime: **ARM**
code. Cite branch veneers and PLT hops. The M6 inventory (`re-analysis/inventory/M6-wwise-bank.md`, corrections up to
C22) and the B-M6b reports (`re-analysis/research/20260928-B-M6b-*.md`, `20260929-B-M6b-3-bridge-extraction.md`) hold
what is known. Start from them, and don't redo them.

## Items, in order of importance

1. **The per-voice bus/connection creation.** C21 creates a voice, but no row attaches it to a mix bus: the connection
   list at `voice+0x28` and the dry and aux entries. Where are the connections created, when, and with what: the
   target bus, the gains, and how the output bus is chosen from the node hierarchy?
2. **`0x009BEB30`** (the PBI parameter/source init; its result must be 1), **`node->vt+0x90`**, and **`0x00A00618`**
   (writes `pbi+0x1B8` / `+0x1BD`, and calls `0x009FB994` / `0x009FF0D8`).
3. **`0x009CD340`:** the media-format/stream descriptor. That's the `pbi+0x158` source-format word, and the plugin/mode
   refinement.
4. **The fade-in setup:** `0x00A36268`, `0x00A366F4` and `vt+0x50`.
5. **`0x00A42DEC` / `0x009D40C4`:** the voice link and teardown bodies.
6. **The `+0x128` PlayInternal bodies of RanSeq, Switch, ActorMixer and Layer.** Only Sound's is read. Include how each
   chooses its child and reaches the Sound path.
7. **The Sound `PlayInternal` special branch:** `params+0x84 == 0x90` (the MIDI note-on status; see the M9 MIDI
   reports), `params+0x14`, the meanings of PBI `+0x1F8`, `+0x1E4` and `+0x14C`, `0x009BC90C`, and the initialiser of the
   `params+0x28..+0x6B` block.

## Out of scope

Any code, inventory or manifest edit. No branches, commits or pushes.

## Answer format

Per item, the table from `.opencode/agent/cozmo-extractor.md` (`step | what the original does | citation | record |
classification`), then contradictions with the current records, rows or code, weak evidence, and open questions.
