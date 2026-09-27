# Parallel jobs

Several agents work at once: Claude, Codex, and DeepSeek through opencode. They don't talk to each other.
They coordinate only through git and the files here.

## Roles

| agent | role |
| --- | --- |
| **Claude** (the integrator) | Owns the shared files: `re-analysis/fidelity_manifest.json`, the inventories under `re-analysis/inventory/`, their approvals, and `PROJECT_STATE.md`. Turns extraction reports into approved inventories, reviews build diffs, and keeps main green. |
| **Codex** | Extraction jobs and independent verification. |
| **DeepSeek via opencode** | Extraction jobs and build jobs. |

## Rules

1. **One job at a time per agent**, taken from `BOARD.md`. Read the job's file and do exactly that job.
2. **Claim it.** Write `re-analysis/jobs/status/<job>.md` with `CLAIMED <agent> <date time>`, then commit and push that file alone.
3. **Stay in the job's write scope**, which the job file lists.
   - An extraction job writes only its report and its status file.
   - A build job writes only its layer's code and tests, and its own layer's records in the manifest.
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
