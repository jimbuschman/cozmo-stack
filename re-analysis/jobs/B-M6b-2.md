# Job B-M6b-2: the M6-022 voice and bus object model and its DSP composition

**Agent:** opencode, as cozmo-manager, in the main clone. **Type:** build. Part of the B-M6b split (B-M6b blocked, correctly, as too large for one job).

**Needs:** nothing more. It may run while B-M6b-1 is still open (manager, 2026-09-28): the voice and bus engine doesn't depend on the Vorbis decoder's output values.

- **Don't edit** `WwiseVorbisDecode.cs`, `WwiseVorbisImdct.cs` or `WwiseVorbisNative*.cs`: the manager is fixing the decoder there.
- **Test the voice and bus engine** with ADPCM or synthetic sources, not decoded Vorbis output.

**Scope:** replace the seams left in `WwiseVoiceEngine.cs` (427d27a):
- the voice pass;
- the bus pass, which is bus metering, per C12;
- the per-voice DSP chain;
- the four Perform group members, composed from the M6 pieces already built (resampler, voice filter, gain, mixer, bus FX, Hijack, frame model).

The engine can then render a posted event to the robot's output buffer, in tests. It is still not wired into the live path.

## How to do it

0. Read `re-analysis/jobs/README.md`, `AGENTS.md` and `.opencode/agent/cozmo-manager.md`. Exact, always. Read B-M6b's status file and its commits (`8ddc45f` correction C12, `427d27a` the M6-022 skeleton and the M6-017 lifecycle): this job continues that work.
1. **Claim the job:** write `re-analysis/jobs/status/B-M6b-2.md` with `CLAIMED <date time>`, then commit and push that file alone.
2. **One batch** for this job's scope only. `@cozmo-implementer` builds it from the approved M6 inventory (corrections C9..C12). A `MISSING:` goes to `@cozmo-extractor`; if the answer changes a row, add a correction and run `--approve M6-wwise-bank` again.
3. **Verify it:** `@cozmo-verifier` checks the whole diff. Fix every blocking finding, then re-verify the fixed hunks.
4. **The records:** settle each M6 record whose whole path is now built. The rest stay IMPLEMENTATION_GAP, with `unresolved` naming what is missing and which later job builds it.
5. **The gates:**
   - `fidelity.py --check` passes;
   - the verifier gives PASS;
   - the full suite passes, with `AssetPresenceTests` green.
6. **Commit and push** only this job's files; `git pull --rebase`, then push.
7. **Finish:** set `DONE <date time>` with the commit hashes and the records settled, or `BLOCKED <reason>`. If the scope is still too large, build a verified part, commit it, and name the rest precisely.

**Write scope:**
- `cozmo-stack/src/Cozmo.Robot/Animation/**`, and whatever else the scope strictly needs (list it in the status file);
- the matching tests;
- the M6 records in the manifest, plus `re-analysis/FIDELITY_GAPS.md`;
- M6 inventory corrections with their re-approval;
- `re-analysis/research/*-B-M6b-2-*` and `re-analysis/jobs/status/B-M6b-2.md`.
