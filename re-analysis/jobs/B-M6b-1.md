# Job B-M6b-1: the M6-002 Vorbis packet driver and the source render wrappers

**Agent:** opencode, as cozmo-manager, in the main clone. **Type:** build. Part of the B-M6b split (B-M6b blocked, correctly, as too large for one job).

**Scope:** build the M6-002 Vorbis packet driver (`0x00AB6B14..0x00AB6F20`) around the exact IMDCT (built in B1, c8e45e5), and the source render wrappers, as corrections C11 and C12 give them.

This is self-contained: it decodes a shipped Vorbis source exactly, and it is tested on the shipped banks. Nothing is wired into the live path yet; B-M6b-3 does that.

## How to do it

0. Read `re-analysis/jobs/README.md`, `AGENTS.md` and `.opencode/agent/cozmo-manager.md`. Exact, always. Read B-M6b's status file and its commits (`8ddc45f` correction C12, `427d27a` the M6-022 skeleton and the M6-017 lifecycle): this job continues that work.
1. **Claim the job:** write `re-analysis/jobs/status/B-M6b-1.md` with `CLAIMED <date time>`, then commit and push that file alone.
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
- `re-analysis/research/*-B-M6b-1-*` and `re-analysis/jobs/status/B-M6b-1.md`.
