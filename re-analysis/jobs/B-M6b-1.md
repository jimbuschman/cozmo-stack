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

## Resume (2026-09-28, after the first run BLOCKED)

The first run (96b6f9d) built the packet driver and the render wrappers. Its bit consumption per packet matches exactly, but the **sample values are wrong**: correlation about 0.002 against the NVorbis rebuild on shipped media, with no lag peak. That points to a gross error in one value stage, not float noise. This resume finds and fixes it.

1. **Take one shipped mono Vorbis media and its first few audio packets.**
2. **Dump every value stage from this stack's decoder, per channel:**
   - the floor1 curve after inverse1 and inverse2;
   - the residue vectors;
   - after channel coupling;
   - the spectrum going into the IMDCT;
   - the IMDCT output;
   - after the window combine and overlap;
   - the emitted samples.
3. **Dump the same stages from a reference decoder:** NVorbis through the existing rebuild path (instrument it in a test), or libvorbis if that's simpler.
   - **The reference only locates the bug.** The engine's instructions stay the oracle. Expect differences of float noise only: both are float Vorbis.
4. **Find the first stage that diverges** beyond noise. Re-read that stage's cited instructions in the `.so`, starting with floor1 inverse1/inverse2, the IMDCT input and output convention (`0x00AB6EEC..0x00AB6F04`, `0x00AB4E34`) and the window combine (`0x00AB5A94`). Fix it to match the instructions; a correction goes in the inventory if a row was wrong.
5. **Re-run the comparison** until every stage matches the reference within float noise, and the emitted samples correlate at 0.99 or better on several shipped mono and stereo media.
   - Keep that comparison as a diagnostic test named for what it is: a sanity check against a reference, not a fidelity oracle.
   - The fidelity tests keep their oracles from the inventory.
6. **Then** finish as the job says: the gates, commit and push, status `DONE`.

**Agent for the resume:** Codex, in `cozmo-stack-codex`, or opencode window 2. Where the job names `@cozmo-implementer`, `@cozmo-extractor` or `@cozmo-verifier`, Codex does that role itself, following its file in `.opencode/agent/`.
