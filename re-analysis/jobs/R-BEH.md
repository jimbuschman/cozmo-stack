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
still open: give them to `@cozmo-extractor`.
