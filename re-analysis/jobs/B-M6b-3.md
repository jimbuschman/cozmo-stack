# Job B-M6b-3: wire the M6 runtime into the live audio path, plus M6-023 and M6-024

**Agent:** opencode, as cozmo-manager, in the main clone. **Type:** build. Part of the B-M6b split (B-M6b blocked, correctly, as too large for one job).

**Needs:** B-M6b-2 DONE. If it isn't, set `WAITING B-M6b-2` and end.

**Scope:** wire the engine into WwisePlayback, WwiseAudioSource, WwiseBusChain, WwiseSongRenderer and AnimationScheduler's audio, replacing the old decode and mix path (see `re-analysis/workplans/M6-plan.md` and `re-analysis/M6-STATUS.md`), and build M6-023 and M6-024.

**The layer is done only when the robot's audio comes from the engine's path.** In the status file, write the listening check the operator should run on the robot. Write the steps; don't wait for the run.

## How to do it

0. Read `re-analysis/jobs/README.md`, `AGENTS.md` and `.opencode/agent/cozmo-manager.md`. Exact, always. Read B-M6b's status file and its commits (`8ddc45f` correction C12, `427d27a` the M6-022 skeleton and the M6-017 lifecycle): this job continues that work.
1. **Claim the job:** write `re-analysis/jobs/status/B-M6b-3.md` with `CLAIMED <date time>`, then commit and push that file alone.
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
- `re-analysis/research/*-B-M6b-3-*` and `re-analysis/jobs/status/B-M6b-3.md`.
