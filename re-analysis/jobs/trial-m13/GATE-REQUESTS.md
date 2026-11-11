# Gate requests: the Claude worker runs the manager's full gates (operator, 2026-11-11)

The manager's PC keeps getting its full gate killed for low memory during the compile step. Until that changes, the
Claude worker on the other PC runs the manager's gates and pushes main.

## Between jobs (never in the middle of one), the worker:

1. Runs `git fetch origin`. If `origin/manager-gate` exists and its tip is not already on `origin/main`, carry on;
   otherwise skip all of this.
2. Checks that it is a fast-forward of main: `git merge-base --is-ancestor origin/main origin/manager-gate`. If it is
   not, write that in REVIEW.md on `claude-worker` and skip.
3. Makes sure its own branch is committed and pushed (a clean tree), then runs
   `git checkout --detach origin/manager-gate`.
4. Runs the full gate: `sh scripts/test-full.sh`.
5. **If it passes:** `git push origin HEAD:main`. The pre-push hook accepts it, because this machine just recorded the
   pass for this exact commit. Don't rebase, amend or merge anything; push exactly that commit.
6. **If it fails:** don't fix the manager's code.
   - Look in the test log for the failing tests and their error messages.
   - If a failure only happens under load and you can fix it as a test-harness fix (as the manager has been doing),
     you may do that on `claude-worker` as a separate small job.
   - Otherwise write the test names and messages into REVIEW.md on `claude-worker`, push it, and leave main alone.
7. Runs `git checkout claude-worker` and continues with the job list.

The manager watches `origin/main` and `origin/claude-worker` for the result.
