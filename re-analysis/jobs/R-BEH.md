# Job R-BEH: close the remaining gaps in the behaviour layers

**Agent:** opencode, as cozmo-manager. **Type:** round 3 (triage, gap extraction, build). Follow [R.md](R.md).
**Needs:** G-M15 DONE. If it isn't, set `WAITING G-M15` and end.

**Subsystems:** M7-behaviour, M8-framework, M9-wwise-music, M15-freeplay. Every IMPLEMENTATION_GAP record of these, as the manifest has it when the job starts.

**Notes:** M9-011, M9-027 and M9-028 need the live audio chain (B-M6b-3). If it hasn't landed, leave them open, naming it. M9-013/014/024/025 are RECOVERABLE_GAP; Codex is extracting them (`re-analysis/research/requests/20260929-M9-singing-gaps.md`). Its answers are in: `re-analysis/research/20260929-M9-025-lfo-waveform-extraction.md`,
`20260929-M9-024-note-off-envelope-extraction.md` (with the manager's `20260929-M9-024-default-addendum.md`: property 1
defaults to 1) and `20260929-M9-013-014-midi-routing-velocity-extraction.md`. The manager spot-checked their key
citations. Fold them in the way G-M7 did for M7-021: the verifier checks every row, then a correction, then the build.
**A contradiction to fix:** the M9-024 report shows that property 15 is the modulator's trigger selector (value 2 is
note-off), not stop-playback. The stop gate is property 1. So `WwiseModulatorProp.EnvelopeStopPlayback = 15` is wrong.
Check every settled M9 record that relies on it. M9-013's MusicTrack-to-target hop and M9-024's `voice+0x20` check are
answered in `20260929-M9-leftovers-extraction.md` (Sonnet). The manager spot-checked the `+0x128` call at 0x00A3DDFC,
the owner gate at 0x009E2338, the only owner store at 0x009E27F8 and the note-state allocation at 0x00A3E4BC. All hold.
Two corrections to that report:
- its "Joining the two" section calls `0x00A71138` the tail of `0x00A70D90`. The function index has `0x00A71138` as a
  separate 1008-byte function; `0x00A70D90` is 924 bytes. The link still holds through the call site 0x00A70EF4, which
  is inside `0x00A70D90`;
- it says no `.bnk` is on disk. The banks are inside `re-analysis/obb/assets/cozmo_resources/sound/AudioAssets.zip`,
  and `WwiseSoundLibrary` reads them. So the Cozmo.bnk flag for 110896138 (Part 1 row 7), and whether the singing
  note-offs pair with their note-ons (Part 2 row 12, from the shipped MIDI order), can both be read from the shipped
  assets.

What is still open goes to `@cozmo-extractor`: the report's open questions 1, 3 and 5..7.

**A further correction (from `20260929-M6-live-audio-bodies-extraction.md`, item 6, section 4 point 2; manager-checked):**
at `0x009D0774` the Layer container's `ldrb r4,[fp,#0x84]` reads its own `+0x84` flag (`fp = this`, `0x009D075C
mov fp,r0`), not the play context's MIDI status. The M9 leftovers report's Part 1 row 15, and the M9-013-014 report,
say otherwise. The params MIDI status is read per child on the L copy (`0x009D085C`, `0x009D0CA0`) and inside
`0xA024B8` (`0xA024CC`).
