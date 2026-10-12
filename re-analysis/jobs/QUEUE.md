# Job queue (from 2026-10-11)

The manager adds jobs here by committing them. The two Codex windows take them in order without the operator relaying
anything. BOARD.md is history; this file is the live queue.

## Loop rules (both lanes)

1. `git pull --rebase`.
2. Take the **first** job in your lane whose status is `open`.
   - Builder: there must be no file at `status/<job>.md`.
   - Research: there must be no answer file yet at the path the job names.
3. **Claim it.**
   - Builder: write `status/<job>.md` with `CLAIMED Codex <date>`, then commit and push.
   - Research: no claim step.
4. Run it under its own rules:
   - builder jobs: `CODEX-BUILDER.md` rules 1–10;
   - research jobs: `research/README.md`, plus the push rule below.
5. Finish it.
   - Builder: write the report into `status/<job>.md`, beginning `DONE` or `BLOCKED`, then commit and push.
   - Research: commit and push research files only.
6. **Repeat from 1.**
7. **When your lane has no open job:** wait 10 minutes (`sleep 600`), pull, and check again. After 6 empty checks
   (an hour), stop and print `QUEUE EMPTY`.
8. **When a job ends BLOCKED, or a push is refused by the gate:** stop and print the reason. Never use `--no-verify`.

**Research push rule:** research jobs commit and push their own files under `re-analysis/research/` (operator,
2026-10-11). This replaces README.md's "no commits" for queued jobs only.

## Builder lane (clone `cozmo-stack-codex2`)

| # | job | status |
| --- | --- | --- |
| 1 | [B-M3M4-6](B-M3M4-6.md) | done (5d62bbb1); re-verify FAIL, see B-M3M4-7 |
| 2 | [B-M3M4-7](B-M3M4-7.md) | done (ceb4df28); re-verify FAIL (1), see B-M3M4-8 |
| 3 | [B-M3M4-8](B-M3M4-8.md) | done (c0b50b8d); re-verify FAIL (1: UnlockAllTracks %s), manager fixed in 7157aa38 |
| 4 | [B-M5-2](B-M5-2.md) | open |
| 5 | [B-M5-3](B-M5-3.md) | open |

## Research lane (clone `cozmo-stack-codex`)

| # | job | status |
| --- | --- | --- |
| 1 | [R-M5-047](../research/requests/20261011-m5-047-stack.md) | done (8694b961) |
