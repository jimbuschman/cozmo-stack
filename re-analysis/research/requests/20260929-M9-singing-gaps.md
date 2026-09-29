# Research request: the four M9 singing gaps (M9-025, M9-024, M9-013, M9-014)

- **Date:** 2026-09-29
- **Requested by:** manager (Claude)
- **Kind:** independent extraction
- **Answer files** (one per part, written as each part finishes):
  - `re-analysis/research/20260929-M9-025-lfo-waveform-extraction.md`
  - `re-analysis/research/20260929-M9-024-note-off-envelope-extraction.md`
  - `re-analysis/research/20260929-M9-013-014-midi-routing-velocity-extraction.md`

## Why this is needed

These are the last live-path RECOVERABLE_GAPs that no job is working on. They are the Wwise runtime semantics behind
Cozmo's singing (the M9 layer, `cozmo-stack/src/Cozmo.Robot/Animation/Wwise/`). Three gap passes
(`20260928-I-M9-gap{1,2,3}-extraction.md`) left them open. Work the parts in the order below. Follow
`re-analysis/research/README.md` and `.opencode/agent/cozmo-extractor.md` exactly: one row per behaviour-changing step,
an address citation or UNKNOWN, and no guesses.

This part of the engine (the Wwise 2016.2 runtime, about 0x0095E540..0x00AE2E40) is **ARM** code, not Thumb, unlike the
Cozmo code in the last request. The branch veneers and PLT hops apply the same way: cite them.

## Part 1: M9-025, the LFO waveform

Record: `The exact waveform produced by the Wwise LFO between its extrema`, location
`cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseModulator.cs`. Its unresolved text: follow the type-21 per-voice
object created at 0x009D7FC0 through vtable 0x0104B268, and map the reads of LFO state +0x34..+0x48 to the waveform
sample equation.

**Lead (check it, don't assume it):** `re-analysis/research/20260928-B-M6b-modulator-evaluator.md` (from the M6 voice
engine build) recovered a modulator evaluator at 0x009E2BD0 / 0x009E52F8. It found five shapes selected by
`source+0x24` (sine 0, triangle 1, square 2, ...), with the switch at 0x009E3488 and
`y[n] = gain*shape(phase)*c1 - y[n-1]*c2` at 0x009E3490..0x009E34A4. The M9 gap2 rows say the type-21 LFO stores its
waveform type at output `+0x24` (L2).

1. Is the object the evaluator reads the type-21 LFO's per-voice state, created at 0x009D7FC0? Trace the pointer from
   the creation through vtable 0x0104B268 to the evaluator's reads.
2. Map every read of the LFO state `+0x34..+0x48` (and `+0x24`) to its use in the per-sample equation. That includes
   the phase advance and wrap, and each shape's formula (all five), with the constants.
3. Is the shape output transformed any further (depth, offset, polarity, PWM) before it reaches the modulated
   parameter? Where?

## Part 2: M9-024, the note-off envelope

Record: `Whether the note-off envelope stops the voice it is attached to`, location
`cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseModulator.cs`. Known (gap2 E2/E3): the type-22 update 0x009D5934,
and the property-15 method 0x009D552C, which distinguishes property values 1 and 2, including MIDI status 0x80/0x90.

1. What calls 0x009D552C at run time, and with which event?
2. Trace its result through the per-voice object. When the envelope finishes after a note-off, does the voice stop?
   Name the stop call and its condition, or show that nothing stops it.

## Part 3: M9-013 and M9-014, MIDI routing and velocity

Records: `Whether Wwise routes MIDI notes into the singing sampler get-in branch` (M9-013) and `Whether MIDI note
velocity implicitly changes voice level` (M9-014), location
`cozmo-stack/src/Cozmo.Robot/Animation/Wwise/WwiseSongRenderer.cs`. Known (gap3 M1/M2): the HIRC loader 0x009B3260 and
the source init 0x00A78D10 don't settle either. Gap3 names candidates around 0x00A14F88..0x00A15038.

1. The runtime MIDI-event entry: how a MIDI note-on from the music track reaches the actor-mixer hierarchy. Name the
   entry, the post-load vtables and the child filtering.
2. With the shipped banks, does MIDI target 110896138 route notes into branch 403781184 (the singing sampler's get-in
   branch)? Trace it with those ids.
3. The velocity byte: follow it from the event entry through every per-voice gain and RTPC input, for a bank that sets
   no velocity binding. Does it change the voice level, and by what formula?

## Already known (don't redo)

- The M9 reports: `20260928-X4-M9-wwise-music-extraction.md`, `20260928-I-M9-gap{1,2,3}-extraction.md`, and the M9
  inventory `re-analysis/inventory/M9-wwise-music.md`.
- The M6 voice-engine reports: `20260928-B-M6b-modulator-evaluator.md`, `20260928-B-M6b-voice-callees.md`,
  `20260928-B-M6b-source-classes.md`, `20260928-B-M6b-2-*.md`.

## Out of scope

- Any other record, and any code, inventory or manifest edit. No branches, commits or pushes.

## Answer format

The table from `.opencode/agent/cozmo-extractor.md` (`step | what the original does | citation | record |
classification`), then contradictions with the current record text, weak evidence, and open questions. Anything not
established from the disassembly is UNKNOWN.
