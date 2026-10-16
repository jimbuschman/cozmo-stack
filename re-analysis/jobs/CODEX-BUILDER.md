# Codex as builder (trial from 2026-10-05)

The operator moved development to Codex for a month. The trial is B-M1M2. These rules replace the research-lane rules
(`re-analysis/research/README.md`) when Codex is given a **build** job. A research task still follows the research
rules.

## Setup, once, in the clone

- `git config core.hooksPath scripts/hooks`: the push gate. It runs `fidelity.py --check` and the full suite before a
  push to main.
- `dotnet` must build `cozmo-stack/Cozmo.sln` and run its tests.

## Rules

1. **Read first:** `AGENTS.md`, `PROJECT_STATE.md` ("The plan"), `re-analysis/jobs/CHECKLIST.md` and the job file.
2. **Build only from checked rows.** A checked row is one an Opus verification or the manager confirmed in the binary.
   This means the `unresolved` text that starts "Opus verification", or a research file the manager adopted.
   - **Never build from your own unchecked extraction.** One role does not extract and build in the same work. That
     separation is what keeps guesses out of the code.
   - **When a needed step has no checked row,** stop it as `MISSING:` in the status file and go on with the rest.
3. **One batch at a time**, each kept small enough to review.
4. **Verify each batch before committing it.** Review your own diff against CHECKLIST.md (the live production path,
   gates, order, failure results, floats as the engine's bits at the engine's width, log texts, tests whose expected
   values come from the binary), then run `fidelity.py --check` and the full suite.
5. **You never settle a record.** Records stay IMPLEMENTATION_GAP, with `unresolved` starting "built, awaiting strong
   verification:". A separate Opus pass settles the layer.
6. **Commit and push to main yourself.** If the gate refuses because origin moved, `git pull --rebase` and push again.
   Never force-push. Never skip the hook.
7. **Log each batch in `status/<job>.md`:** the commit, the records touched, and every MISSING.
8. **Finish the job as DONE or BLOCKED,** as defined in QUEUE.md rule 8, then continue with the queue. Don't stop the
   lane.
9. **No verification packets** (manager, 2026-10-11).
   - Don't write `*-verify-packet/` directories. The Opus pass checks the binary itself, and a packet only enlarges the
     diff it reads.
   - Put what the verifier needs in `status/<job>.md`: per record, the files and lines changed and the binary ranges
     they follow.
   - **When the manager runs you on its machine,** `.git` is read-only in your sandbox. The manager pulls, commits and
     pushes. Skip rule 6 and the claim step, and run no git command.
10. **Port every engine log and side effect inside the cited ranges.** The Opus passes found the same misses more
    than once. Before committing, list every call inside each cited instruction range and account for each one in the
    C#:
    - the log calls (`sChanneledInfoF`, `sWarningF`, `sErrorF`, and the error-flag store and debug-break gate that follow
      `sErrorF`), with their exact event name, format string and level;
    - file and stream opens and closes;
    - counters and static state.

    A call with no C# counterpart is either built, or listed as MISSING with its address. Also **reopen the call target
    of every row you build on.** A row can misname a function: M1-024's D1 did.
11. **A MISSING never goes into the engine log stream** (manager, 2026-10-14; B-POSE-1 and B-M11-1 both failed on it).
    Report a MISSING with `SteppedBehavior.ReportMissing` (the stack's own channel, deduplicated). Never call
    `EmitSourceLog`/`EmitEngineLog` or a VERIFY with a "MISSING" key or text. Engine log entries are only what the
    original logs. A guard test enforces this.
12. **Threads follow the original.** Code the original runs on one thread (Robot::Update, the message handlers) must not
    move onto the vision worker or another thread in the stack, unless one gate is shared with every writer of the same
    state. B-M11-1 failed on a race this way.
13. **An absent owner is not a reason to skip** (manager, 2026-10-16; B-M9-1 was under-built this way). Sometimes a
    checked unit's live owner or caller doesn't exist in the stack yet. Then:
    - build the unit exactly from its rows, as production code, not as a test helper;
    - wire it to the nearest existing live entry the rows name;
    - put a `ReportMissing` at the missing owner or join, naming its address;
    - write the job's named tests against the unit.

    "No live owner" and "the callee is unread" are never reasons to leave a checked row unbuilt. List each unbuilt
    checked item in a table, with the specific unchecked row it depends on (record and address). An item without such
    a row counts as under-built, and the job's check fails.
14. **Label how each regression test reaches the behaviour** (manager, 2026-10-16; CHECKLIST.md section 5).
    - **On each new or changed test,** a comment saying `reach: production` or `reach: unit (substitutes …; join
      MISSING at …)`.
    - **In `status/<job>.md`,** per record, the tests that reach it through production. Where there are none, say so,
      with the MISSING join that prevents it.
    - **A unit test with substituted inputs is never reported as covering the production path.**
15. **Run the log-key pre-check before DONE** (manager, 2026-10-16; `research/20261016-logkey-pilot.md`).
    - Run `python -I re-analysis/tools/logkey_check.py cozmo-stack/src/<the files you touched>`.
    - Every ABSENT key in your files is either fixed from a checked row or explained in the status file.
    - EXACT proves only that the key exists. It says nothing about level, branch, order or text.
