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
8. **Stop** when the job is DONE, or BLOCKED on a decision the source can't settle. Don't start another job without
   the operator.
9. **When a layer's build is finished** (DONE, or BLOCKED only on items outside the layer), prepare the Opus
   verification packet before stopping. It goes in `re-analysis/research/<date>-<layer>-verify-packet/`, one file per
   record that is built and has no open MISSING. Each file holds:
   - the record's current manifest text;
   - the commits and the exact diff hunks that implement it;
   - the rows it was built from, quoted, with their source file;
   - the native instruction text for every cited address range;
   - any emulator or oracle test that covers it, with its result.

   List the excluded records, and why. Give no verdicts. The packet keeps the Opus pass short: it compares, it doesn't
   rediscover.
10. **Port every engine log and side effect inside the cited ranges.** The Opus passes found the same misses more
    than once. Before committing, list every call inside each cited instruction range and account for each one in the
    C#:
    - the log calls (`sChanneledInfoF`, `sWarningF`, `sErrorF`, and the error-flag store and debug-break gate that follow
      `sErrorF`), with their exact event name, format string and level;
    - file and stream opens and closes;
    - counters and static state.

    A call with no C# counterpart is either built, or listed as MISSING with its address. Also **reopen the call target
    of every row you build on.** A row can misname a function: M1-024's D1 did.
