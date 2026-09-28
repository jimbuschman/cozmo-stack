# Parallel jobs

Several opencode (DeepSeek) windows work at once, each in its own clone, each running one chain of jobs from `BOARD.md` unattended with `scripts/run-job.ps1`. They don't talk to each other. They coordinate only through git and the files here. Codex or Claude can take a job or spot-check one, but nothing depends on them.

## Roles

| agent | role |
| --- | --- |
| **X jobs** (extraction) | Read-only; they write reports. |
| **I jobs** (integration) | Turn a layer's reports into its approved inventory and records. |
| **B jobs** (build) | Build a layer from its approved inventory and settle its records. |
| **Claude or Codex** (optional) | A spot-check of a finished job now and then. |

## Rules

1. **One job at a time per agent**, taken from `BOARD.md`. Read the job's file and do exactly that job.
2. **Claim it.** Write `re-analysis/jobs/status/<job>.md` with `CLAIMED <agent> <date time>`, then commit and push that file alone.
3. **Stay in the job's write scope**, which the job file lists.
   - An extraction job writes only its report and its status file.
   - A build job writes only its layer's code and tests, and its own layer's records in the manifest.
   - An integration job may also update tests whose only purpose is to restate a record's status or its listing in the hardware checks: `FidelityManifestTests`, `HardwareCatalogFidelityTests`, `ControlCheckFidelityTests`. It changes them only to match the newly approved status, and names each change in its status file. It changes no behaviour test.
   - Anything else you think should change goes in your report, under "for the integrator".
4. **Committing:**
   - add only your own files (`git add <paths>`, never `git add -A`);
   - `git pull --rebase`, then push;
   - never force-push; never create branches or worktrees;
   - never use `--no-verify`.
5. **Finishing:** set your status file to `DONE <date time>`, with a one-line summary, or to `BLOCKED <reason>`. Push it.
6. **Exact, always** (AGENTS.md). No guesses: an unknown is written as UNKNOWN.

## Sources

- **The engine:** `resources/lib/armeabi-v7a/libcozmoEngine.so`.
- **The Ghidra decompilation:** `re-analysis/decomp/libcozmoEngine/`.
  - It has one pseudo-C file per function; `index.tsv` lists the address, the size and the name.
  - Its addresses are the ELF's own, the same the repo cites.
  - It is a **navigation aid, not evidence.** Cite the instructions in the `.so` and check them there. The decompiler can be wrong, especially on NEON code.
- **The rest:** the Unity code (`unity/`, `sources/`, `smali/`), the assets (`re-analysis/obb/`), and earlier evidence (`re-analysis/evidence/`).

## Clones

- Each agent that **builds** has its own clone, so builds and tests never collide.
- Extraction agents can share one clone, because they never build.
- Every clone runs `git config core.hooksPath scripts/hooks` once. That turns on the push gate, which refuses a push to main unless the fidelity check and the full suite pass.
