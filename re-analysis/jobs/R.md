# Round 3: closing the remaining gaps, layer group by layer group

Every R job follows this file. Its own file (`R-<group>.md`) names the subsystems and what it needs first.

Each layer has been built once. What is left is its IMPLEMENTATION_GAP records. Each one's `unresolved` text says why it
is still open, and there are four kinds:

- **compare:** the code may already match the approved rows; nobody has confirmed it. (`compare the code against the
  inventory rows`)
- **missing source:** a piece of the path isn't in the rows (`MISSING:`, `not in the rows`, `not read`). It needs
  extraction first.
- **cross-layer wiring:** the path needs another layer's piece, which that layer's build job has since built.
  (Examples: the carried object is M12's CarryingComponent; the connection reads' callbacks are M15's, M11's and
  M12's.)
- **still blocked:** the piece it needs is not built anywhere yet, or is HARDWARE_ONLY or BLOCKED_EXTERNAL.

## The policy review (2026-09-29)

`re-analysis/research/20260929-policy-review.md` reviewed the 32 COMPATIBILITY_POLICY and EQUIVALENT_IMPLEMENTATION
records against "Exact, always" (Sonnet, read-only). The manager spot-checked M8-008 (the IAction timeout -1.0 at
0x00540CA6) and M8-009 (the IBehavior::Stop release order at 0x005BD12C..0x005BD174). Both hold.

Its **C records** ship in the engine and the stack keeps something else. Each job takes the ones in its subsystems:
- a correction that moves the record to IMPLEMENTATION_GAP (or RECOVERABLE_GAP where the review says the source is
  unread), with the review's citations checked by `@cozmo-verifier`;
- `--approve`;
- then build it with the rest.

| job | records |
| --- | --- |
| R-DEV | M4-004 (the engine has no calibration gate on direct motion); M1-014's priority clause (the engine requests SCHED_RR at 75%, 0x008334CC) |
| R-ANIM | M5-020 (invented expressions). **Don't build or remove anything:** whether ShowExpression's extra faces stay is the operator's decision. Name it in the status file. |
| R-BEH | M8-004 (score default 0, chooser-based selection), M8-008 (no timeout on the head recalibration wait), M8-009 (the fixed release order), M9-016 (the render lead: read it, or BLOCKED_EXTERNAL with the reason) |
| R-VIS | M11-013 (the record contradicts the code; it becomes RECOVERABLE_GAP: read `CreateObjectsFromMarkers` and the ObjectID assignment), and M11-012's provenance/evidence text |

The records the review lists as **contradicting their code or incomplete** (its last section) are text fixes. Each
job fixes those in its subsystems as cleanup: no extra verify round, while `--check` passes. A frozen field (for
example M11-013's `live_path`) needs a correction and `--approve`. The A and B records stay as they are. B records are
operator decisions.

## How to do it

0. Read `re-analysis/jobs/README.md`, `AGENTS.md` and `.opencode/agent/cozmo-manager.md`. Exact, always; no guesses.
   A record's current state is what the manifest says now. Re-read it; don't rely on memory or an old report.
1. **Claim the job:** write `re-analysis/jobs/status/<job>.md` with `CLAIMED <date time>`, then commit and push that
   file alone.
2. **Triage.** List every IMPLEMENTATION_GAP record of the job's subsystems, with its kind, in the status file. This
   list is the job's progress record: a later round continues from it.
3. **Missing source:** give `@cozmo-extractor` the exact questions, one gap pass per subsystem, written to
   `re-analysis/research/<yyyymmdd>-<job>-<subsystem>-gapN-extraction.md`. At most 3 passes per subsystem. The checked
   rows become the next correction in that subsystem's inventory. Then run
   `python re-analysis/tools/fidelity.py --approve <subsystem>`, `--write` and `--check`. The standing authorisation
   covers cited corrections. Whatever is left stays open, with exactly what to read.
4. **Build, one batch per subsystem:** `@cozmo-implementer` builds or repairs the compare, missing-source and wiring
   records from the approved rows. A `MISSING:` goes back to step 3. Cross-layer wiring touches the other layer's code
   only at the call site, and the status file lists each such file.
5. **Verify:** `@cozmo-verifier` checks the batch's diff. Fix every blocking finding, then re-verify the fixed hunks.
6. **Settle** each record whose whole production path is now built and verified. Every other record keeps
   IMPLEMENTATION_GAP, with `unresolved` naming exactly what is missing and what would build it. Don't change a status
   to COMPATIBILITY_POLICY or EQUIVALENT_IMPLEMENTATION in this job. If a record looks like one, say so in the status
   file for the policy review.
7. **Commit and push each batch** once `fidelity.py --check` passes, the verifier gives PASS and the full suite passes.
   Commit only this job's files; `git pull --rebase`, then push. If the push gate says origin has moved, pull again and
   push again.
8. **Finish:** set `DONE <date time>` once every record in scope is settled or has a precise `unresolved`. Include the
   commits, the records settled and the records left, with why. Use `BLOCKED <reason>` only when the job can't go on
   at all. A large scope is not a reason: commit verified batches and carry on in the next round.

**Write scope:** the job's subsystems' code and tests; the other layers' code only at a wiring call site (listed in the
status file); those subsystems' records, inventories and `.approved.json` files; `re-analysis/FIDELITY_GAPS.md`;
`re-analysis/research/*-<job>-*`; and `re-analysis/jobs/status/<job>.md`.
