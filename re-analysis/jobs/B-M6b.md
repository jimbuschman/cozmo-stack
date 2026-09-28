# Job B-M6b: build the M6 voice engine and wire M6 into the live audio path

**Agent:** opencode, as cozmo-manager, in the main clone. **Type:** build. **Needs:** job I-M6b DONE. If it isn't, set the status to `WAITING I-M6b` and end.

## Scope

This finishes what B1 started. B1 built the exact IMDCT (c8e45e5).

- **Build M6-021**, the live voice and bus engine, and **M6-017**, the frame model, from the approved correction.
- **Build the M6-002 Vorbis packet driver.**
- **Wire the whole M6 runtime into the live audio path:** WwisePlayback, WwiseAudioSource, WwiseSongRenderer and AnimationScheduler's audio. Replace the old decode and mix path as `re-analysis/workplans/M6-plan.md` and `re-analysis/M6-STATUS.md` describe.
- **The layer is done only when the robot's audio comes from the engine's path.**

## How to do it

0. Read `re-analysis/jobs/README.md`, `AGENTS.md` and `.opencode/agent/cozmo-manager.md`. Exact, always.
1. **Claim the job:** write `re-analysis/jobs/status/B-M6b.md` with `CLAIMED <date time>`, then commit and push that file alone.
2. **One batch.** `@cozmo-implementer` builds and wires it. A `MISSING:` goes to `@cozmo-extractor`; if the answer changes a row, add a correction and run `--approve` again.
3. **Verify it:** `@cozmo-verifier` checks the whole diff. Fix every blocking finding, then re-verify the fixed hunks.
4. **The records:** settle each M6 record whose whole path is now built. The rest stay IMPLEMENTATION_GAP, with `unresolved` saying what is missing.
5. **The gates:**
   - `fidelity.py --check` passes;
   - the verifier gives PASS;
   - the full suite passes, with `AssetPresenceTests` green.
6. **Commit and push** only this job's files; `git pull --rebase`, then push.
7. **Finish:** set the status to `DONE <date time>`, with the commit hashes, the records settled, and the listening check the operator should run on the robot. Write the steps; don't wait for the run.

**Write scope:**
- `cozmo-stack/src/Cozmo.Robot/Animation/**`, and whatever else the wiring strictly needs (list it in the status file);
- the matching tests;
- the M6 records in the manifest, plus `re-analysis/FIDELITY_GAPS.md`;
- a correction in the M6 inventory, with its re-approval, if a MISSING changes a row;
- `re-analysis/jobs/status/B-M6b.md`.
