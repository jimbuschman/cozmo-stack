# Job I-M6: turn the M6-wwise-bank extraction into an approved inventory

**Agent:** opencode, as cozmo-manager. **Type:** integration. **Needs:** job X5 DONE. If it isn't, set this job's status to `WAITING X5` and end.

## How to do it

0. Read `re-analysis/jobs/README.md`, `AGENTS.md` and `.opencode/agent/cozmo-manager.md`. Exact, always; no guesses.
1. **Claim the job:** write `re-analysis/jobs/status/I-M6.md` with `CLAIMED <date time>`, then commit and push that file alone.
2. **Read the extraction reports:** `re-analysis/research/*-X5-*extraction*.md`.
3. **Check the citations.** Give `@cozmo-verifier` every row that changes a record's status or contradicts one, plus a sample of at least 20 others. Ask it to open each citation in the `.so` and confirm it says what the row says.
   - A row that fails goes back to `@cozmo-extractor` to be fixed.
   - Never keep a row that failed the check.
4. **Close the gaps.** Rows marked UNKNOWN or RECOVERABLE_GAP go to `@cozmo-extractor` for a gap pass, with the report written to `re-analysis/research/<yyyymmdd>-I-M6-gapN-extraction.md`.
   - Repeat until every row is settled, or is HARDWARE_ONLY or BLOCKED_EXTERNAL with a reason. At most 3 gap passes.
   - Whatever is left stays RECOVERABLE_GAP, with exactly what to read.
5. **The inventory:** `re-analysis/inventory/M6-wwise-bank.md` is already approved. Add the settled rows as the next correction, in a "Corrections after the first freeze" section: C9, or the next free number. Cite every row, and copy the reports into a new appendix. Don't rewrite the approved rows.
6. **The records:** update only the M6 records that the correction changes (M6-002's kernel rows and M6-020): their evidence, `unresolved`, and status. A record the rows settle for building stays IMPLEMENTATION_GAP until job B1 builds it.
7. **Approve it:** `python re-analysis/tools/fidelity.py --approve M6-wwise-bank`, then `--write`, then `--check`. The check must pass.
8. **Commit and push:** commit only the inventory, its `.approved.json`, the manifest, `re-analysis/FIDELITY_GAPS.md`, your gap reports and your status file. Then `git pull --rebase` and push. If the rebase conflicts in the manifest, keep both sides' records, run `--check` again, then push.
9. **Finish:** set the status to `DONE <date time>`, with the record count for each status. If you can't go on, set it to `BLOCKED <reason>`.

**Write scope:** `re-analysis/inventory/M6-wwise-bank.md`, its `.approved.json`, this subsystem's records in the manifest, `re-analysis/FIDELITY_GAPS.md`, `re-analysis/research/*-I-M6-*`, and `re-analysis/jobs/status/I-M6.md`. No code. No other subsystem's records.
