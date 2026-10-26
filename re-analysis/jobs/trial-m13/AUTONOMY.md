# Codex/Claude handshake through git (operator, 2026-10-26)

The operator no longer relays messages. Codex (the other PC) and Claude (this PC) coordinate only through this
folder and the repo. The operator is involved only for hardware runs and for decisions AGENTS.md reserves to them.

## Codex (manager of the work)

- Work on one branch, `codex-m13-manager-2`, rebased on current `origin/main` before each job.
- Take jobs from `QUEUE.md` in order: the first row in state RELEASED or FIX.
  - **FIX:** the Claude check failed. Do that fix first.
  - **Empty queue:** scope the next job yourself, under the standing scope below. Add it as a row and do it.
- Per job:
  - follow the job's own file (or your scoped version), the extraction-then-check-then-build order and AGENTS.md;
  - end with `HANDOFF-<job>.md` (short: changes, addresses, tests, what's open, questions);
  - set the row to READY FOR CLAUDE and push the branch.
- **Don't wait for the Claude check.** Start the next job on top of the branch.
- Never push to main. Never change statuses or approvals, and never accept a subsystem.
- Stop and write `BLOCKED.md` only for a decision AGENTS.md gives to the operator, such as an exclusion of a robot or
  API feature, or a departure. Then continue with any job that doesn't depend on it.

**Standing scope, in order:**
1. M13: the drive-off and cube-avoidance acceptance needs: planner ownership, ComputeOriginPose, the M12-049
   configuration producer, then the remaining M13 IMPLEMENTATION_GAPs and RECOVERABLE_GAPs.
2. Then M6, one record or one tight group per job. ADP-1 applies.

## Claude (reviewer and integrator)

Claude polls `origin/codex-m13-manager-2` for rows marked READY FOR CLAUDE. For each, in order:
- it checks the job against the binary;
- on PASS, it fast-forwards or merges main up to that job, runs the full gate, pushes, and sets the row to MERGED;
- on FAIL, it writes the finding into the row (state FIX), pushes the queue change to main, and does not merge that job
  or any later one.

Claude writes hardware scripts when a batch is worth a robot run, and notifies the operator. The operator's part is
running the scripts and copying back the bundles.

## Usage and hardware (operator, 2026-10-26)

**Robot runs never hold anything up.** A job merges on its Claude check and the full gate alone. Claude writes a
hardware script only when merged work has piled up that changes robot traffic or behaviour, and the operator runs it
when convenient. A failed run becomes a FIX row for the job it points at; nothing else stops.

**Claude usage is the scarce resource, so the reading is Codex's.**
- **Codex** does all extraction, all building, its own independent Codex check, and runs `scripts/test-full.sh` on the
  branch before marking a row READY FOR CLAUDE. Its `HANDOFF-<job>.md` lists each behaviour-changing claim with its
  address, the files changed, the tests and how each reaches the behaviour, and the gate result.
- **Claude** does not redo the extraction. It checks the handoff's claims that change "What we reproduce" 1–4 at their
  cited addresses, looks for circular tests and unrecorded gaps, and stops there. A claim the cited address does not
  settle goes back as FIX with the question; Codex does the reading.
- **Claude reviews in batches:** all rows that are READY FOR CLAUDE at the time, in one pass.

## Where verdicts go, and how Codex sees them (operator, 2026-10-26)

- **Claude writes its verdicts only in `REVIEW.md`** in this folder on main: one line per job, MERGED or FIX with the
  finding. Codex never edits `REVIEW.md`; Claude never edits `QUEUE.md` on Codex's branch. That keeps rebases free of
  conflicts. (This replaces "writes the finding into the row" above.)
- **Codex reads `origin/main:re-analysis/jobs/trial-m13/REVIEW.md`** at the start of every job, after the rebase. A FIX
  line comes before any new job.
- **While Codex is idle** (waiting, or after its scope runs out), it polls `origin/main` with a plain shell loop about
  every 15 minutes, not with model turns.
