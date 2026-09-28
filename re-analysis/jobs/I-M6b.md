# Job I-M6b: approve the M6 voice engine (new M6-021, amended M6-017)

**Agent:** opencode, as cozmo-manager. **Type:** integration. **Needs:** B1's voice-engine extraction, `re-analysis/research/20260927-B1-voice-engine-extraction.md` (rows B1-V1..V30).

## Why

B1 built the exact IMDCT, but it can't wire M6 into the live audio path: no approved record owns the live voice and bus graph. B1's extraction identifies that graph as a new record, M6-021, plus an amendment to M6-017.

## How to do it

0. Read `re-analysis/jobs/README.md`, `AGENTS.md` and `.opencode/agent/cozmo-manager.md`. Exact, always.
1. **Claim the job:** write `re-analysis/jobs/status/I-M6b.md` with `CLAIMED <date time>`, then commit and push that file alone.
2. **Check the citations.** Give `@cozmo-verifier` every row B1-V1..V30, and ask it to open each citation in the `.so` and confirm it says what the row says. A row that fails goes back to `@cozmo-extractor` to be fixed.
3. **Close the gaps.** UNKNOWN and RECOVERABLE_GAP rows go to `@cozmo-extractor` for a gap pass, with the report in `re-analysis/research/<yyyymmdd>-I-M6b-gapN-extraction.md`. At most 3 passes.
   - Also include what B1 said remains: the M6-002 Vorbis packet driver `0x00AB6B14..0x00AB6F20`, and exactly how the live path reaches WwisePlayback, WwiseAudioSource, WwiseSongRenderer and AnimationScheduler's audio.
4. **The correction:** add the next correction number in `re-analysis/inventory/M6-wwise-bank.md`'s "Corrections after the first freeze", with the checked rows:
   - a new record, **M6-021: the live voice and bus engine**;
   - the **M6-017** amendment;
   - the packet-driver rows under M6-002.
   Copy the reports into an appendix.
5. **The records:** add M6-021 (IMPLEMENTATION_GAP, with all its fields), and update M6-017 and M6-002. Each record whose rows are now settled stays IMPLEMENTATION_GAP until the build.
6. **Approve it:** `python re-analysis/tools/fidelity.py --approve M6-wwise-bank`, then `--write`, then `--check`.
7. **Commit and push** only those files and your status file; `git pull --rebase`, then push.
8. **Finish:** set `DONE <date time>` with the record counts, or `BLOCKED <reason>`.

**Write scope:** `re-analysis/inventory/M6-wwise-bank.md`, its `.approved.json`, the M6 records in the manifest, `re-analysis/FIDELITY_GAPS.md`, `re-analysis/research/*-I-M6b-*`, and `re-analysis/jobs/status/I-M6b.md`.
