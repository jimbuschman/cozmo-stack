# Claude worker on the other PC (manager, 2026-11-11)

A second Claude Code session on the operator's other PC does what Codex did: it builds queued jobs on its own branch.
The manager (Claude on this PC) checks each one against the binary, settles records and merges.

**Start of every session:** read `AGENTS.md`, `PROJECT_STATE.md` (newest entries) and this file.

## Branch and git

- **Branch:** `claude-worker`, created from `origin/main`.
- **Keeping up with main:** before each job, `git fetch origin` and **merge** `origin/main` into the branch. Never
  rebase: the manager merges this branch with `--no-ff`, and rebasing replays merged work.
- **Pushing:** only `claude-worker`. Never push to `main`.
- **Never** change a record's status, approval or frozen fields (title, authority, evidence, live_path,
  hardware_required) in `re-analysis/fidelity_manifest.json`. Never run `fidelity.py --approve`. Never accept a
  subsystem. Settling is the manager's job. Adding a test name to a record's `test` field is fine.
- **Hardware:** never commit hardware scripts or bundles, `.so` files, `re-analysis/obb`, sound banks or the
  operator's face photo.

## The jobs, in this order (the localization chain, M11-044)

1. **LIVE-OBJECT-POSE-NATIVE-1:** see its QUEUE.md row and
   `re-analysis/research/20261111-localize-same-origin-claude-check.md` (the addendum). This is what switches
   automatic localization on for a live robot.
2. **LOCALIZE-PRODUCTION-PATH-TEST-1:** see its QUEUE.md row. It is blocked until job 1 lands; the probe that found
   the defect is described in the same check file.
3. **LOCALIZE-CROSS-ORIGIN-1:** see its row; Rejigger, Flatten and the origin updates at 0x0051590C..0x005159B2.
4. **LOCALIZE-TO-MAT-HISTORY-1:** see its row.
5. Your released proposals (done 2026-11-11): CLAMP-FLAT-NUMERICS-1, INITPOSE-OBSERVED-FLAG-1, DOCK-MARKER-WRT-SKIP-1,
   LIFT-RELATIVE-OBSERVATION-PARENT-1, POSE-SNAPSHOT-FLAG-CACHE-1.
6. **PATH-ORIGIN-CHANGE-REPLAN-1** (M12-032: HandlePossibleOriginChanges 0x0064980C → RejiggerTargetsAndReplan
   0x006499D0). When it lands, remove the manager's path-in-flight guard in BlockWorld.LocalizeToObject.
7. **LCO-TAIL-PRODUCTION-TEST-1:** see its QUEUE row.
8. **CARRIED-ONTOP-BINDING-CENSUS-1:** see its QUEUE row and `CENSUS-M11-043.md`.
9. **LEGACY-DRIVE-TO-POSE-NATIVE-1:** your own proposal from PATH-ORIGIN-CHANGE-REPLAN-1. Move the legacy
   `StartDrivingToPose(int, ...)` callers onto the native drive-center entry. When it lands, remove the rest of the
   manager's path-in-flight guard.
10. **DRIVE-TO-OBJECT-MANUAL-SPEED-1:** your proposal; see its QUEUE row (M12-022).
11. **FIND-ON-TOP-SAME-ROOT-1:** your proposal; see its QUEUE row (M13-007).
12. **LOCALIZE-MULTI-PAIR-CROSS-ORIGIN-TEST-1:** your proposal; see its QUEUE row (M11-044).
13. **SETTLEMENT-READINESS-1:** doc only; see its QUEUE row. Do it first if the others are waiting on anything.
14. **RETIRE-LEGACY-DRIVE-API-1:** resume it. The operator chose "retire and rebind" (BLOCKED.md, 2026-11-13); the
    manager's answers to Q3–Q6 are in `research/20261113-worker-jobs-10-13-m11-009-claude-check.md`. Move the M12-009/M12-016
    tags with the code. Hardware tools may change, but never commit hardware scripts or bundles.
15. **CHARGER-ID-WRITER-1:** from SETTLEMENT-READINESS.md (M4-009's one behaviour gap). Add its QUEUE row when you take it.
16. **CHARGER-BOUNDING-QUAD-1:** your proposal; see its QUEUE row (M13-025). A live defect on the robot runtime.
17. **USE-SECOND-CLOSEST-NATIVE-1:** see its QUEUE row (M12-004, now IMPLEMENTATION_GAP).
18. **HARDWARE-CATALOG-T-R-JUDGES-1:** harness only; see its QUEUE row.
19. **ROLL-AFTER-DRIVE-STANDALONE-1:** your proposal; see its QUEUE row (the roll stalls after its drive).
20. **GET-SIZE-SLOT-RECORD-1:** records only; see its QUEUE row.
21. **ROLL-MARKER-OBSERVED-1:** your proposal; see its QUEUE row (the roll's dock stops at IDockAction::Init 0x0300001D).
22. **REFERENCE-SIZE-SLOT-1:** see its QUEUE row (M11-105 / M13-007).

Before job 10, run the pending gate on `origin/manager-gate` (6e111864 or later; GATE-REQUESTS.md). Legacy drive API:
the operator chose to retire it (job 14).

When you start a job, set its QUEUE.md row to `TAKEN (Claude worker, <date>)` and push the branch, so the manager
does not duplicate it. If the list is finished, add a note to REVIEW.md asking for more jobs, and stop.

## Gate requests (operator, 2026-11-11)

Between jobs, run the manager's pending gate first: see `GATE-REQUESTS.md`.

## Per job

1. **Census:** a `cozmo-extractor` subagent writes an inventory to `.scratch/<job>-census.md`: every
   behaviour-changing step with its address, or UNKNOWN; the stack mapping; the records the source contradicts.
   - Before tracing any address, run `python re-analysis/tools/known.py <address|name>`.
   - Disassemble with any armdis-style helper; the decomp is a navigation aid, never evidence.
2. **Decide what the census can't:** you settle only what AGENTS.md lets a manager settle (manager exclusions,
   unreachable branches). Anything else goes to `BLOCKED.md`, then carry on with work that doesn't depend on it.
3. **Build:** a `cozmo-implementer` subagent, from the census. The expected values in tests come from the engine,
   not from the C#.
4. **Verify:** a `cozmo-verifier` subagent, independent. Fix every BLOCKING finding, then re-verify only the changed
   parts.
5. **Test:** run the affected classes, then the full gate: `sh scripts/test-full.sh`. It must pass.
   - Root-cause any failure. A test that fails only under load is still fixed, as a test-harness fix.
   - If the gate is killed for low memory, check `.scratch/test-mem.log`, stop leftover dotnet and testhost
     processes, and rerun once.
6. **Hand off:** write `re-analysis/jobs/trial-m13/HANDOFF-<job>.md`, short:
   - what changed, with the native addresses;
   - the tests and their reach (production or unit);
   - the verifier's findings and how each was resolved;
   - what stays open, and which record or job owns it;
   - **Records:** for each record the job touches, either "propose settlement: <status>, because <the whole path is
     now built>" or the named gaps that block it.
7. **Mark it ready:** set the row to `READY FOR CLAUDE`, commit and push `claude-worker`. Then start the next job
   without waiting for the manager's check.

## What the manager does

- **Watching:** the manager polls `origin/claude-worker` for READY FOR CLAUDE rows.
- **For each job:**
  - re-reads the key addresses in the binary;
  - merges with `--no-ff`;
  - settles the records the job completes, in a `*claude-check*.md` file;
  - runs the gate and pushes main;
  - writes its verdict in REVIEW.md.
- **When a check fails:** the row goes to `FIX` with the reason. Do a FIX row before the next job.

## Usage

- If the 5-hour limit cuts off a subagent, resume that same subagent after the reset; don't start a new one.
- Don't run two builds at once on this machine.
