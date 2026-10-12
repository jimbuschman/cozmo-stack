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
| 4 | [B-M5-2](B-M5-2.md) | done (9af65077); re-verify FAIL (1: duplicate type-3 Results), see B-M5-4 |
| 5 | [B-M5-3](B-M5-3.md) | done (8816bab0); re-verify FAIL (2: manifest tests on wrong record, fixed by manager; string colours), see B-M5-5 |
| 6 | [B-M5-4](B-M5-4.md) | done (ac0581ad, manager suite 4338/4338); re-verify FAIL (1: backpack walk warnings twice), see B-M5-6 |
| 7 | [B-M5-5](B-M5-5.md) | done (244828ac); re-verify FAIL (2: OFFWHITE alpha, fixed by manager; JSON empty() gate), see B-M5-7 |
| 8 | [B-M5-6](B-M5-6.md) | done (d9b59e53); re-verify PASS. Queued: M4-036 unresolved must say the constructor loads the backpack file list from its own walk, not the collect's type-3 vector (0x0052013C) |
| 9 | [B-M5-7](B-M5-7.md) | running (manager's machine) |

## Research lane (clone `cozmo-stack-codex`)

| # | job | status |
| --- | --- | --- |
| 1 | [R-M5-047](../research/requests/20261011-m5-047-stack.md) | done (8694b961) |
