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
   - builder jobs: `CODEX-BUILDER.md` rules 1–12 (11: MISSING never in the engine log; 12: threads follow the original);
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
| 26 | [B-M3M4-16](B-M3M4-16.md) | done 17466557; Opus check FAIL (research/20261013-b-m3m4-16-check.md), fixes in B-M3M4-19 and B-M3M4-20 |
| 27 | [B-M3M4-17](B-M3M4-17.md) | done b8c595ea; Opus check FAIL (research/20261013-b-m3m4-17-check.md), fixes in B-M3M4-21 |
| 28 | [B-M3M4-18](B-M3M4-18.md) | done 212fec53; Opus check FAIL on scope only (research/20261013-b-m3m4-18-check.md); rest in B-M3M4-22 and B-M15-1 |
| 29 | [B-M5-17](B-M5-17.md) | done b9040943; Opus check FAIL (production drives hang; research/20261013-b-m5-17-check.md), fixes in B-M5-18 |
| 29a | [B-M5-18](B-M5-18.md) | done 5db4343b; Opus check FAIL (drives time out; research/20261013-b-m5-18-check.md), fixes in B-M5-19 |
| 29a2 | [B-M5-19](B-M5-19.md) | done 92278498; drives end in Success; Opus check FAIL on one log line (research/20261013-b-m5-19-check.md), fix in B-M5-20 |
| 29c | [B-M13-1](B-M13-1.md) | done 3becf79a; Opus check FAIL (final-turn regression; research/20261013-b-m13-1-check.md), fixes in B-M13-2 |
| 29c2 | [B-M13-2](B-M13-2.md) | done 57abaaba; Opus check FAIL (plan logs, event levels; research/20261013-b-m13-2-check.md); final turn confirmed; fixes in B-M13-3 |
| 29c3 | [B-M13-3](B-M13-3.md) | done 02fceff6; Opus check FAIL (OldPlanUnsafe on fresh plans, circular test; research/20261014-b-m13-3-check.md); fixes in B-M13-5 |
| 29c4 | [B-M13-5](B-M13-5.md) | done 5e95dc70; Opus check FAIL (fresh plan left the total null; research/20261014-b-m13-5-check.md); fixed in B-M13-6 |
| 29c5 | [B-M13-6](B-M13-6.md) | done by the manager's Claude implementer (Codex out of budget); manager-reviewed |
| 29d | [B-M12-2](B-M12-2.md) | done dae131fe; Opus check PASS (research/20261013-b-m12-2-pass.md); two queued items in B-M5-20 |
| 29b | [B-M12-1](B-M12-1.md) | done d7316d1d; Opus check FAIL on one point (Dole send failures; research/20261013-b-m12-1-check.md), fix in B-M12-2 |
| 30 | [B-M3M4-19](B-M3M4-19.md) | done 985ba775; Opus check PASS (research/20261013-b-m3m4-19-pass.md); follow-up B-M3M4-23 |
| 30a | [B-M3M4-22](B-M3M4-22.md) | done 51b42629; behaviour matches (research/20261013-b-m3m4-22-check.md); test-hygiene cleanup in B-M3M4-24 |
| 31 | [B-POSE-1](B-POSE-1.md) | done e5780c3c; Opus check FAIL (research/20261014-b-pose-1-check.md); fixed in B-POSE-2 (ddf1d511, Claude implementer) |
| 31a0 | [B-POSE-3](B-POSE-3.md) | open, next (pose-tree VERIFYs M11-066/M11-077) |
| 31a | [B-M11-1](B-M11-1.md) | open, after B-POSE-1 (Delocalize in full, UpdateObjectOrigin) |
| 32 | [B-M3M4-21](B-M3M4-21.md) | open, after B-POSE-1 (dangling blended entry, cube-light root, LoadTime, cleanup) |
| 33 | [B-M15-1](B-M15-1.md) | open, after B-M3M4-21 (backup manager: constructor, body, restore, wipe, restore list, app inputs) |
| 34 | [B-M3M4-23](B-M3M4-23.md) | open, after B-M15-1 (every BlockWorld pose mutator outside the frame window) |
| 35 | [B-M3M4-24](B-M3M4-24.md) | open, after B-M3M4-23 (test isolation, MISSING placement, B6 label test) |
| 36 | [B-M3M4-25](B-M3M4-25.md) | open, after B-M3M4-24 (dispatch residue: 0x101CF, owner releases, re-Init order, connect timing) |
| 37 | [B-M5-20](B-M5-20.md) | open, after B-M3M4-25 (empty-name warning, queued items) |
| 38 | [B-M2M15-1](B-M2M15-1.md) | open, after B-M5-20 (UnlockId table and InitConfig, M15-044; handler bodies, M2-023) |
| 39 | [B-M11-2](B-M11-2.md) | open, after B-M2M15-1 (RobotDelocalized recipients M11-072; GetParent/FindRoot M11-076) |
| 40 | [B-M12-3](B-M12-3.md) | open, after B-M11-2 (lift pose M12-027; dock registration M12-034; cube-light callback M4-049; PlaceRel producers M12-028, helper M12-042; approach angle M12-039; PreActionPose fields M12-038/M12-001; docking lifetime M12-040/M12-018) |
| 41 | [B-M13-4](B-M13-4.md) | open, after B-M12-3 (movement/track-layer wrappers M13-021; turn CheckIfDone M13-022; M13-020; TrackObjectAction M13-036; path abort/destruction M13-029, corner sort M13-023, pose destruction M11-077) |
| 42 | [B-M11-3](B-M11-3.md) | open, after B-M13-4 (BlockWorld observation-path rows from OV10: M11-041/013/062/037/007/044/052) |
| 43 | [B-M11-4](B-M11-4.md) | open, after B-M11-3 (vision/history/world/pose/map teardown, M11-055) |
| 44 | [B-M11-5](B-M11-5.md) | open, after B-M11-4 (quad-tree border walk M11-047; seed order = insertion order, operator 2026-10-14) |
| 45 | [B-M11-6](B-M11-6.md) | open, after B-M11-5 (CheckImageQuality M11-080; LimitedExposure M11-057; auto-exposure M3-021) |
| 46 | [B-M11-7](B-M11-7.md) | open, after B-M11-6 (decode exception M11-058; vision Init M11-059; pending camera params M11-060) |
| 47 | [B-M8-1](B-M8-1.md) | open, after B-M11-7 (behaviour teardown M8-016; Hiccup dtor M10-016; CloseLog M7-028) |
| 48 | [B-M8-2](B-M8-2.md) | open, after B-M8-1 (action list/queue M8-015; TRACKS_LOCKED M8-007) |
| 49 | [B-M8-3](B-M8-3.md) | open, after B-M8-2 (compound runtime M8-017; behaviour end request M8-019) |
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
| 13 | [R-LOGGERS-BACKUP](../research/requests/20261013-test-loggers-and-backup.md) | answered; checked (research/20261013-test-loggers-and-backup-check.md) |
| 14 | [R-BACKUP-REST](../research/requests/20261013-backup-manager-rest.md) | answered (Codex on the work PC); checked (research/20261013-backup-manager-rest-check.md) |
| 15 | [R-PLANNERS](../research/requests/20261013-planner-internals.md) | answered; checked (research/20261013-planner-internals-check.md) |
| 16 | [R-DELOC-CALLEES](../research/requests/20261013-delocalize-callees.md) | answered; checked (research/20261013-delocalize-callees-check.md) |
| 17 | [R-PLAN-DEPS](../research/requests/20261013-planner-dependencies.md) | answered; checked (research/20261013-planner-dependencies-check.md) (M13-034) |
| 18 | [R-BACKUP-UNLOCK](../research/requests/20261013-backup-unlock-and-stoull.md) | answered; checked (research/20261013-backup-unlock-and-stoull-check.md) (M15-043) |
| 19 | [R-POSE-REST](../research/requests/20261013-remaining-pose-bodies.md) | answered; checked (research/20261013-remaining-pose-bodies-check.md) (M11-068) |
| 20 | [R-DISPATCH-REST](../research/requests/20261013-dispatch-residue.md) | answered; checked (research/20261013-dispatch-residue-check.md) (M2-020) |
| 21 | [R-QUADTREE](../research/requests/20261013-quadtree-rest.md) | answered (7ba29fbf); checked (research/20261013-quadtree-rest-check.md) |
| 22 | [R-SMALL-1](../research/requests/20261013-small-reads-1.md) | answered (overnight P2); checked (research/20261013-small-reads-1-check.md): M11-072 to build, M11-073 split (M11-076 to build) |
| 23 | [R-SMALL-2](../research/requests/20261013-small-reads-2.md) | answered (overnight P3); checked (research/20261013-small-reads-2-check.md): M15-044 to build, M2-022 split (M2-023 to build) |
