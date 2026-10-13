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
| 9 | [B-M5-7](B-M5-7.md) | done (028f038d); re-verify FAIL (1: non-object JSON root got EmptyFile), manager fixed. Queued: JsonClipLoader.Load is a test helper, mark it |
| 10 | [B-M5-8](B-M5-8.md) | done (9812b4e0, suite 4387/4387); re-verify FAIL (5), see B-M5-9 |
| 11 | [B-M5-9](B-M5-9.md) | done (b302b3e4); re-verify FAIL (4), see B-M5-10 |
| 12 | [B-M3M4-9](B-M3M4-9.md) | done (ae2bd302, suite 4445/4445); re-verify FAIL (5), see B-M3M4-10 |
| 13 | [B-M5-10](B-M5-10.md) | done (f5dac2f0, suite 4471/4471); re-verify FAIL (1: mood cut at the first NUL), manager fixed |
| 14 | [B-M3M4-10](B-M3M4-10.md) | done (70a3484d, suite 4487/4487); re-verify FAIL (2, backpack loader), see B-M3M4-11 |
| 15 | [B-M5-11](B-M5-11.md) | done (119eb2fa); re-verify PASS (2 cleanups queued) |
| 16 | [B-M5-12](B-M5-12.md) | done (b228c542); re-verify FAIL (1: +0x44 += 60 still ran), manager fixed |
| 17 | [B-M3M4-11](B-M3M4-11.md) | done (589824f8, suite 4508/4508); re-verify PASS |
| 18 | [B-M5-13](B-M5-13.md) | done 710751f9; Opus check FAIL on one omission (research/20261012-b-m5-13-check.md) |
| 20 | [B-M5-14](B-M5-14.md) | done d36613ab; Opus check FAIL (research/20261012-b-m5-14-check.md), fixes in B-M5-16 |
| 21 | [B-M3M4-13](B-M3M4-13.md) | done 88da56ad; Opus check FAIL (research/20261012-b-m3m4-13-check.md), fixes in B-M3M4-15 |
| 22 | [B-M5-15](B-M5-15.md) | done 1287c660; Opus check FAIL (research/20261012-b-m5-15-check.md), fixes in B-M5-16 |
| 23 | [B-M3M4-14](B-M3M4-14.md) | done 0d2b5526; Opus check FAIL (research/20261013-b-m3m4-14-check.md), fixes in B-M3M4-17 |
| 24 | [B-M3M4-15](B-M3M4-15.md) | done 9a05e3a0; Opus check FAIL (research/20261013-b-m3m4-15-check.md), fixes in B-M3M4-18 |
| 25 | [B-M5-16](B-M5-16.md) | done a941b3ec; Opus check FAIL (research/20261013-b-m5-16-check.md); M12-024 being read, fixes in B-M5-17 |
| 26 | [B-M3M4-16](B-M3M4-16.md) | open, after B-M5-16 (cliff BlockWorld helpers M11-062, MemoryMap insert M13-030, null-helper MISSINGs) |
| 27 | [B-M3M4-17](B-M3M4-17.md) | open, after B-M3M4-16 (cube-light fixes: per-LED modifier, last-pattern canBeOverridden, shared slot, timer, trigger map, one walk) |
| 28 | [B-M3M4-18](B-M3M4-18.md) | open, after B-M3M4-17 (M2-018 dispatch order, BlockTapFilter warnings, device-ID BOM, test hygiene) |
| 29 | [B-M5-17](B-M5-17.md) | open, after B-M3M4-18 (M12-024 driving lifecycle, handler start side, UnlockTracks miss path, DriveStraight/ITrackAction, activity fixes) |
| 19 | [B-M3M4-12](B-M3M4-12.md) | done 802717ae; Opus check PASS (research/20261012-b-m3m4-12-check.md) |

## Research lane (clone `cozmo-stack-codex`)

| # | job | status |
| --- | --- | --- |
| 1 | [R-M5-047](../research/requests/20261011-m5-047-stack.md) | done (8694b961) |
| 2 | [R-M5-052](../research/requests/20261012-m5-052-face-png.md) | done (70b8ba04); adopted into M5-052: shipped faces decode 1-bit -> 0/255, 8-bit identity |
| 3 | [R-M8-PLAY](../research/requests/20261012-m8-play-animation-actions.md) | done (4c6e6d56); Opus check running |
| 4 | [R-M3-051](../research/requests/20261012-m3-051-libdas-sink-b.md) | done (5f4f04d2); adopted (operator departure M3-051) |
| 5 | [R-M5-TEARDOWN](../research/requests/20261012-m5-teardown-reads.md) | done (cd912037); adopted (M5-055/056/058 to build) |
| 6 | [R-M9-029](../research/requests/20261012-m9-029-custom-song.md) | done (8ccc9ac5); adopted: M9-029 COMPATIBILITY_POLICY (CodeLab only) |
| 7 | [R-M4-CLIFF](../research/requests/20261012-m4-cliff-and-names.md) | answered; checked (research/20261012-m4-cliff-and-names-check.md) |
| 8 | [R-M4-LIGHTS](../research/requests/20261012-m4-cube-lights.md) | answered; checked (research/20261012-m4-cube-lights-check.md) |
| 9 | [R-CLIFF-WORLD](../research/requests/20261012-cliff-world-helpers.md) | answered; checked (research/20261012-cliff-world-helpers-check.md) |
| 10 | [R-PLAY-AREA](../research/requests/20261013-play-area-and-localize-failures.md) | answered; checked (research/20261013-play-area-check.md) |
| 11 | [R-SUBSCRIBERS](../research/requests/20261013-robot-message-subscribers.md) | answered; checked (research/20261013-robot-message-subscribers-check.md) |
| 12 | [R-HANDLERS](../research/requests/20261013-unported-message-handlers.md) | answered; checked (research/20261013-unported-message-handlers-check.md) |
