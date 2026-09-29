# Job B-M6b-4: wire the Wwise runtime into the robot's live audio

**Agent:** Claude Code with Sonnet, in the `cozmo-stack-r` clone on the manager's machine (operator, 2026-09-29), as the
manager of this job. The role agents are Claude Code's `.claude/agents/` (cozmo-extractor, cozmo-implementer,
cozmo-verifier): where this file says `@cozmo-extractor` etc., use those subagents. **Pushes:** this clone can't push
by itself. Commit, then stop and tell the operator to push; carry on once the push is done. Claim first, then commit the
claim and have it pushed before any other work.

**Type:** build. It continues B-M6b-3, which blocked correctly on unread bodies (`re-analysis/jobs/status/B-M6b-3.md`).
**Needs:** B-M6b-3's status to say BLOCKED with that reason, and the extraction below to be in.

**Input:** `re-analysis/research/20260929-M6-live-audio-bodies-extraction.md` (Sonnet), answering B-M6b-3's six unread
bodies. The manager spot-checked:
- the new-voice path calling `0xA42DEC` at 0x00A43178;
- the Layer `+0x84` read at 0x009D0774 (the container's own flag, `fp = this` at 0x009D075C);
- the ActorMixer PlayInternal `0xA667D0` (`mov r0,#0; bx lr`).

All hold. The rest is unchecked. Treat it like any extractor report.

**The bar:** the robot's audio comes from the engine's path. Nothing else counts as done: not a part, and not an
adapter left beside the old path. B-M6b-3's recommended next jobs (2) and (3) are this job.

## How to do it

0. Read `re-analysis/jobs/README.md`, `AGENTS.md`, `re-analysis/jobs/status/B-M6b-3.md` and its commits `b926d00` and
   `ca3a969`. Exact, always; no guesses.
1. **Claim the job:** write `re-analysis/jobs/status/B-M6b-4.md` with `CLAIMED <date time>`, then commit it and have
   it pushed.
2. **Check the citations.** Give `@cozmo-verifier` every row of the report that changes or creates a record, plus a
   sample of at least 20 others. A row that fails goes back to `@cozmo-extractor`. Never keep a row that failed.
3. **The inventory correction:** the next correction in `re-analysis/inventory/M6-wwise-bank.md`, holding the checked
   rows. It includes a **new record** for the playback-limit walker (`node->vt+0x90`, `0x9ED2CC` / `0x9C4F30`): no
   record covers it, and most shipped Cozmo sounds sit under a max-1 kill-oldest mixer. The report's corrections to
   earlier rows and reports (its contradiction sections) are part of it too. Run
   `python re-analysis/tools/fidelity.py --approve M6-wwise-bank`, then `--write`, then `--check`.
4. **Build, in verified batches:**
   - the voice-to-bus connection creation (`0xA42DEC`, `0xA42C60`, `0xA4C280`, the connection constructor);
   - the playback limits;
   - `0x9BEB30`'s context init;
   - the `0x9CD340` format parse;
   - the fade-in;
   - the container PlayInternal bodies for the shipped classes.

   Then **wire it:** an engine-backed animation audio source replaces the old decode-and-mix path in `WwisePlayback`,
   `WwiseAudioSource`, `WwiseSongRenderer` and the AnimationScheduler's audio (see `re-analysis/workplans/M6-plan.md`).
   The conformance tools switch to it too. A `MISSING:` goes to `@cozmo-extractor`; if the answer changes a row, add it
   to the correction and approve again.
5. **Verify each batch:** `@cozmo-verifier` checks the diff. Fix every blocking finding, then re-verify the fixed
   hunks. Wherever a stage is numeric (mixing, gains, resampling), compare it with the engine's own code under
   `re-analysis/tools/emu/`, the way the Vorbis decode was done (`emu_vorbis.py`). Where the emulator can run it, that
   is the oracle.
6. **The records:** settle each M6 record whose whole production path is built and verified. That includes M6-002
   (Vorbis, bit-exact end to end, waiting only on this wiring), M6-023, M6-024 and M6-025, where their paths are
   complete. Every other record keeps IMPLEMENTATION_GAP, with `unresolved` naming exactly what is missing.
7. **The gates:** `fidelity.py --check` passes, the verifier gives PASS, and the full suite passes, before each commit.
8. **Finish:** set `DONE <date time>` with the commits and the records settled, or `BLOCKED <reason>`. A large scope is
   not a reason: commit verified batches and carry on. **In the status file, write the operator's listening check**
   (B-M6b-3's status has a draft): the steps, the robot setup, and where the bundle goes.

**Write scope:**
- `cozmo-stack/src/Cozmo.Robot/Animation/**` and the conformance tool's audio wiring (list the files in the status
  file);
- the matching tests;
- the M6 records and their inventory and `.approved.json`, and `re-analysis/FIDELITY_GAPS.md`;
- `re-analysis/research/*-B-M6b-4-*` and `re-analysis/jobs/status/B-M6b-4.md`.
