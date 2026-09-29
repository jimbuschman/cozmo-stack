# Job G-M7: fold the M7-021 extraction into M7-behaviour and build it

**Agent:** opencode, as cozmo-manager. **Type:** gap follow-up (integration, then build) for one record.
**Needs:** B-M7 DONE. If it isn't, set `WAITING B-M7` and end.

**Record:** M7-021, `Reaction robot-field meanings and cliff helper bodies remain to be recovered`, live-path
RECOVERABLE_GAP, location `cozmo-stack/src/Cozmo.Robot/Behavior/OffTreadsBehaviors.cs`.

**Input:** `re-analysis/research/20260929-M7-021-reaction-fields-extraction.md`, an independent extraction by Codex.
The manager's spot-check (2026-09-29) confirmed:
- the `+0x356 -> +0x355` commit at 0x0051208E;
- the accelerometer filter reading `+0x37C` at 0x00512A06, with the 0.95 literal at 0x00512D08 and 0.05 at
  0x00512D04. The report cites only the 0.05, so check which literal each multiply uses.

The rest is unchecked. Treat it like any extractor report.

## How to do it

0. Read `re-analysis/jobs/README.md`, `AGENTS.md` and `.opencode/agent/cozmo-manager.md`. Exact, always; no guesses.
1. **Claim the job:** write `re-analysis/jobs/status/G-M7.md` with `CLAIMED <date time>`, then commit and push that
   file alone.
2. **Check the citations.** Give `@cozmo-verifier` rows M7-021-1..7, and a sample of at least 20 reader and writer
   addresses from the report's lists. Ask it to open each citation in the `.so` and confirm it says what the row
   says. A row that fails goes back to `@cozmo-extractor` to be fixed. Never keep a row that failed.
3. **The inventory correction:** add the next correction to `re-analysis/inventory/M7-behaviour.md`, holding the
   checked rows. In the manifest, M7-021 becomes IMPLEMENTATION_GAP (to build), with the rows' citations as its
   evidence. Run `python re-analysis/tools/fidelity.py --approve M7-behaviour`, then `--write`, then `--check`.
4. **Build:** `@cozmo-implementer` makes the live reactions read the fields the rows name, with the engine's
   meanings: the seven-valued OffTreadsState, the on-charger flag, the lift angle in radians, and the filtered accel
   magnitude (`+0x37C = 0.05*magnitude + 0.95*old`, from RobotState accel). Where the robot-state code already derives
   one of them, compare it with the rows, and fix it only if it differs. Otherwise build it where the robot state is
   derived. A `MISSING:` goes to `@cozmo-extractor`. If the answer changes a row, add it to the correction and approve
   again.
5. **Verify:** `@cozmo-verifier` checks the whole diff. Fix every blocking finding, then re-verify the fixed hunks.
6. **The record:** settle M7-021 only if its whole production path is built and verified. Otherwise it stays
   IMPLEMENTATION_GAP, with `unresolved` naming exactly what is missing.
7. **The gates:** `fidelity.py --check` passes, the verifier gives PASS, and the full suite passes.
8. **Commit and push** only this job's files; `git pull --rebase`, then push.
9. **Finish:** set `DONE <date time>` with the commit hashes and the record's status, or `BLOCKED <reason>`.

**Write scope:**
- `cozmo-stack/src/Cozmo.Robot/Behavior/**`, the robot-state derivation code the fields need (list each file in the
  status file), and the matching tests;
- M7-021 and M7-behaviour's review block in the manifest. If a robot-state field belongs to another subsystem's
  record, don't edit that record: name it in the status file for the integrator;
- `re-analysis/FIDELITY_GAPS.md`, `re-analysis/inventory/M7-behaviour.md` and its `.approved.json`;
- `re-analysis/research/*-G-M7-*` and `re-analysis/jobs/status/G-M7.md`.
