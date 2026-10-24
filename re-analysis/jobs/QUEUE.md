# Job queue (from 2026-10-11)

The manager adds jobs here by committing them. The two Codex windows take them in order without the operator relaying
anything. BOARD.md is history; this file is the live queue.

A job's own state is in `status/<job>.md`. `tools/status_views.py` refreshes the status column: a builder note still
reading `open` once a status file exists is rewritten to that file's state. It also renders `STATUS.md` (implemented
vs verification evidence) and `../FIDELITY_PROGRESS.md`.

## Loop rules (both lanes)

1. `git pull --rebase`.
2. Take the **first** job in your lane whose status is `open`.
   - Builder: there must be no file at `status/<job>.md`.
   - Research: there must be no answer file yet at the path the job names.
3. **Claim it.**
   - Builder: write `status/<job>.md` with `CLAIMED Codex <date>`, then commit and push.
   - Research: no claim step.
4. Run it under its own rules:
   - builder jobs: `CODEX-BUILDER.md` rules 1–15 (11: MISSING never in the engine log; 12: threads follow the original; 13: an absent owner is not a reason to skip; 14: label how each test reaches the behaviour; 15: run the log-key pre-check);
   - research jobs: `research/README.md`, plus the push rule below.
5. Finish it.
   - Builder: write the report into `status/<job>.md`, beginning `DONE` or `BLOCKED`, then commit and push.
   - Research: commit and push research files only.
6. **Repeat from 1.**
7. **When your lane has no open job:** wait 10 minutes (`sleep 600`), pull, and check again. After 6 empty checks
   (an hour), stop and print `QUEUE EMPTY`.
8. **DONE vs BLOCKED** (manager, 2026-10-16).
   - **A job ends DONE** when every item whose rows are checked is built, even if MISSINGs remain. MISSINGs at unread
     calls, at absent owners, or on records outside the job do **not** make a job BLOCKED.
   - **BLOCKED is only for these cases:**
     - (a) a checked row contradicts the binary, so the items that depend on it can't be built;
     - (b) the push gate's tests fail;
     - (c) an item needs an operator decision.
   - **Either way,** push the report and the work, then **continue** with the next open job. The manager handles
     blockers from the report.
   - **The status header** is `DONE (n/m items; k MISSING)` or `BLOCKED (a|b|c: reason)`.
   - **When a push is refused** because main moved: `git pull --rebase` and push again.
   - **When a push is refused by the test gate:** stop and print the reason.
   - Never use `--no-verify`.

**Research push rule:** research jobs commit and push their own files under `re-analysis/research/` (operator,
2026-10-11). This replaces README.md's "no commits" for queued jobs only.

## Builder lane (clone `cozmo-stack-codex2`)

**Current-layer work (manager, 2026-10-23): B-M13-15, B-M13-18, B-M13-16, B-M13-17 in order, one at a time (M13-017 shared pieces from the checked groundwork).** No duplicate builder pickup. B-M11-18 and other HELD rows remain held.

**HOLD (manager, 2026-10-17):** start no builder job whose row reads HELD. Rows marked HELD are not `open` and must not
be claimed, even if they look next. B-M2M15-2 is released (M1+M2 path). The others stay held until the manager
re-scopes them under AGENTS.md "What we reproduce" (2026-10-17). The research lane is not held.

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
| 31a0 | [B-POSE-3](B-POSE-3.md) | done 1cc1b6b6; Opus check FAIL (IsSameAs parent-wrapper check count; poseoriginlist.destructor event; research/20261015-b-pose-3-check.md); fixes in B-POSE-4 |
| 31a1 | [B-POSE-4](B-POSE-4.md) | done d12b58fc; Opus check FAIL (NonFlattenedPose path text; null-node IsSameAs check count; research/20261015-b-pose-4-check.md); fixed in B-POSE-5 fabc3280 (PASS) |
| 31a2 | [B-M15-1b](B-M15-1b.md) | done f5696c57; Opus check PASS (research/20261015-b-m15-1b-pass.md); two wording items in B-M15-3 |
| 31a3 | [B-POSE-5](B-POSE-5.md) | done fabc3280; Opus check PASS (research/20261015-b-pose-5-pass.md); test-comment item in B-M3M4-28 |
| 31a | [B-M11-1](B-M11-1.md) | done a9425dad; Opus check FAIL (research/20261014-b-m11-1-check.md); fixed in B-M11-8 (Claude implementer, four verify rounds) |
| 32 | [B-M3M4-21](B-M3M4-21.md) | done e27e9fbf; Opus check FAIL (dangling blended entry: advance read and shrink marking; research/20261014-b-m3m4-21-check.md); fixes in B-M3M4-26 |
| 32a | [B-M3M4-26](B-M3M4-26.md) | done 316b979a; Opus check PASS (research/20261015-b-m3m4-26-pass.md); cleanups in B-M3M4-27 |
| 33 | [B-M15-1](B-M15-1.md) | done d096ffcb; Opus check FAIL (two log texts; research/20261015-b-m15-1-check.md); fixed in B-M15-1b f5696c57 (PASS) |
| 34 | [B-M3M4-23](B-M3M4-23.md) | done 66f6cfc0; Opus check PASS (research/20261015-b-m3m4-23-pass.md); handler-level races in B-M3M4-28 |
| 35 | [B-M3M4-24](B-M3M4-24.md) | done 70360684; Opus check PASS (research/20261015-b-m3m4-24-pass.md); three host-tool items queued there |
| 35a | [B-M3M4-28](B-M3M4-28.md) | done f0d753e9; Opus check PASS (research/20261015-b-m3m4-28-pass.md); item 7 comment done by the manager |
| 36 | [B-M3M4-25](B-M3M4-25.md) | done f6fc3664; Opus check FAIL (construction-time connection gate throws; research/20261015-b-m3m4-25-check.md); fixes in B-M3M4-29 |
| 37 | [B-M5-20](B-M5-20.md) | done 166a157e; Opus check PASS (research/20261015-b-m5-20-pass.md); queued items in B-M5-21 |
| 36a | [B-M3M4-29](B-M3M4-29.md) | done 37dfcdfd; Opus check PASS (research/20261015-b-m3m4-29-pass.md); hardware tools construct again |
| 36b | [B-M5-21](B-M5-21.md) | done 31e05384; Opus check FAIL (live trigger map single-file; missing members dropped; crash inputs escape; research/20261015-b-m5-21-check.md); fixes in B-M5-22 |
| 36c | [B-M5-22](B-M5-22.md) | done 30576f80; Opus check PASS (research/20261019-b-m5-22-check.md); six queued items for the next M5 job |
| 38 | [B-M2M15-1](B-M2M15-1.md) | BLOCKED per status/B-M2M15-1.md (UnlockId table and InitConfig, M15-044; handler bodies, M2-023) |
| 39 | [B-M11-2](B-M11-2.md) | DONE per status/B-M11-2.md (RobotDelocalized recipients M11-072; GetParent/FindRoot M11-076) |
| 40 | [B-M12-3](B-M12-3.md) | DONE per status/B-M12-3.md (lift pose M12-027; dock registration M12-034; cube-light callback M4-049; PlaceRel producers M12-028, helper M12-042; approach angle M12-039; PreActionPose fields M12-038/M12-001; docking lifetime M12-040/M12-018); Opus check FAIL (retirement race; research/20261016-b-m12-3-check.md), fixes in B-M12-7 |
| 41 | [B-M13-4](B-M13-4.md) | BLOCKED (turn-eye destructor row; bounded part pushed 730ccc23); checked row added (research/20261015-turn-eye-dtor-check.md); finished by B-M13-7 |
| 41a | [B-M13-7](B-M13-7.md) | DONE per status/B-M13-7.md (TurnInPlaceAction live eye layer and destructor cleanup) |
| 42 | [B-M11-3](B-M11-3.md) | BLOCKED (SetMarkerAsObserved 0x00876DA8..0x008770CC has no checked row; bounded part pushed ba4c1211); request OV85; finish job after its check; OV85 checked (research/overnight/20261016-ov85-check.md); finished by B-M11-14 |
| 42a | [B-M11-14](B-M11-14.md) | BLOCKED per status/B-M11-14.md (SetMarkerAsObserved, finishes B-M11-3) |
| 43 | [B-M11-4](B-M11-4.md) | BLOCKED (FaceTracker/FaceRecognizer handle census, M11-077/M11-084 ownership, M11-090 residue; bounded part pushed 64f9d104); finish after those records are read |
| 44 | [B-M11-5](B-M11-5.md) | BLOCKED per status/B-M11-5.md (quad-tree border walk M11-047; seed order = insertion order, operator 2026-10-14) |
| 45 | [B-M11-6](B-M11-6.md) | BLOCKED per status/B-M11-6.md (CheckImageQuality M11-080; LimitedExposure M11-057; auto-exposure M3-021) |
| 46 | [B-M11-7](B-M11-7.md) | BLOCKED per status/B-M11-7.md (decode exception M11-058; vision Init M11-059; pending camera params M11-060) |
| 47 | [B-M8-1](B-M8-1.md) | BLOCKED per status/B-M8-1.md (behaviour teardown M8-016; Hiccup dtor M10-016; CloseLog M7-028) |
| 48 | [B-M8-2](B-M8-2.md) | BLOCKED per status/B-M8-2.md (action list/queue M8-015; TRACKS_LOCKED M8-007) |
| 49 | [B-M8-3](B-M8-3.md) | BLOCKED per status/B-M8-3.md (compound runtime M8-017; behaviour end request M8-019) |
| 50 | [B-M12-4](B-M12-4.md) | BLOCKED per status/B-M12-4.md (native put-down M12-006/M12-045) |
| 51 | [B-M8-4](B-M8-4.md) | BLOCKED per status/B-M8-4.md (DAS animation blacklist load M8-022; M8-025) |
| 52 | [B-M10-1](B-M10-1.md) | BLOCKED per status/B-M10-1.md (BlockFilter/gyro lifetime M10-014; drift/bias detectors M10-015; raw IMU magnitude M10-017) |
| 53 | [B-M3M4-27](B-M3M4-27.md) | DONE; Codex source check PASS (research/20261017-b-m3m4-27-check.md); retrospective reach labels queued in PROJECT_STATE |
| 54 | [B-M15-2](B-M15-2.md) | BLOCKED per status/B-M15-2.md (disconnect/teardown M15-027/028; repair decay M15-029; ApplyDelta M15-046; audio teardown M6-030) |
| 55 | [B-M15-3](B-M15-3.md) | BLOCKED per status/B-M15-3.md (needs exceptional values M15-030; unlock/inventory NV callbacks M15-034/035; ForceSet M15-048) |
| 56 | [B-M14-1](B-M14-1.md) | BLOCKED per status/B-M14-1.md (pet/face-world and speech lifetime M14-013) |
| 57 | [B-M7-1](B-M7-1.md) | BLOCKED per status/B-M7-1.md (mood destruction M7-023) |
| 58 | [B-M7-2](B-M7-2.md) | BLOCKED per status/B-M7-2.md (TrackLaser colour/exposure snapshot and restore M7-025) |
| 59 | [B-M6-1](B-M6-1.md) | BLOCKED per status/B-M6-1.md (audio multiplexer/controller teardown order M6-029) |
| 60 | [B-M11-9](B-M11-9.md) | BLOCKED per status/B-M11-9.md (memory-map producers M11-074; aging M11-075; stream unreachable) |
| 61 | [B-M9-1](B-M9-1.md) | BLOCKED per status/B-M9-1.md (Wwise MIDI note path M9-013; velocity/level M9-014; envelope stop M9-024) |
| 61r | [B-M9-3](B-M9-3.md) | DONE per status/B-M9-3.md (re-do under-built B-M9-1 under rule 13) |
| 61s | [B-M8-7](B-M8-7.md) | DONE per status/B-M8-7.md (StartActing runner ownership, B-M8-5 item 9) |
| 61t0 | [B-M8-8](B-M8-8.md) | DONE per status/B-M8-8.md |
| 61t1 | [B-M12-6](B-M12-6.md) | DONE per status/B-M12-6.md |
| 61t2 | [B-M13-8](B-M13-8.md) | DONE per status/B-M13-8.md |
| 61u | [B-M6-4](B-M6-4.md) | BLOCKED per status/B-M6-4.md (bus state M6-035; Term recipients M6-034; device storage M6-036) |
| 61v | [B-M9-4](B-M9-4.md) | DONE per status/B-M9-4.md (MIDI boundary M9-034; clip-start M9-040; collector M9-035; emitters M9-036; note-off M9-037; live RTPC M9-038) |
| 61w0 | [B-M11-15](B-M11-15.md) | DONE per status/B-M11-15.md |
| 61w1 | [B-M15-7](B-M15-7.md) | CLAIMED per status/B-M15-7.md (Lab activation M15-052; disconnect consumers M15-056; AI teardown M15-045; forwarding M15-055); Opus check FAIL (research/20261017-b-m15-7-check.md), fixes in B-M15-11 |
| 61w2 | [B-M1-2](B-M1-2.md) | CLAIMED per status/B-M1-2.md; Opus check FAIL (research/20261017-b-m1-2-check.md), fixes in B-M1-3 |
| 61w2a | [B-M15-11](B-M15-11.md) | CLAIMED per status/B-M15-11.md (B-M15-7 check FAIL: Lab always enabled; Feeding deselect handle release; ASCII locale); Opus check PASS with queued items (research/20261017-b-m15-11-check.md) |
| 61w3 | [B-M2M15-2](B-M2M15-2.md) | CLAIMED per status/B-M2M15-2.md (released 2026-10-17: M1+M2 path), after B-M15-11 (publish parked B-M2M15-1: M15-044, M2-023; fix PoseTreeVerifyBuilderTests full-run failure); Opus check PASS with queued items, finding 1 raised to blocking (research/20261017-b-m2m15-2-check.md), fix in B-DELIV-1 |
| 61w3a | [B-M1-3](B-M1-3.md) | DONE per status/B-M1-3.md (B-M1-2 check FAIL: dead engine on a supplied config; public carry setter; Init-time broadcast delivered publicly); Opus fix review PASS with queued items (research/20261017-b-m1-3-check.md) |
| 61w3b | [B-M1-4](B-M1-4.md) | DONE per status/B-M1-4.md (engine readiness: StartEngine, loading, ready/failed, state-3 gating; M1-054/055/056/058); Opus check FAIL (2: multi-name startup never ready; 0->1 not observable), research/20261017-b-m1-4-check.md, fix in B-M1-5 |
| 61w3c | [B-DELIV-1](B-DELIV-1.md) | DONE per status/B-DELIV-1.md (app delivery outside engine locks; B-M2M15-2 finding 1); Opus check FAIL (2: inline broadcasts reorder host delivery; production label), research/20261017-b-deliv-1-check.md, fix in B-M1-5 |
| 61w3d | [B-M1-5](B-M1-5.md) | DONE per status/B-M1-5.md (fixes for B-M1-4 and B-DELIV-1; names in stack load order, operator 2026-10-17); Opus fix reviews FAIL (ConnectAsync ready race; three inline broadcasts), research/20261018-b-m1-5-check-readiness.md and -delivery.md, fix in B-M1-6 |
| 61w3e | [B-M1-6](B-M1-6.md) | DONE per status/B-M1-6.md (fixes for B-M1-5: ConnectAsync ready race, last inline broadcasts, full census table); Opus fix reviews FAIL (flaky ready tests, handoff fence; census rows to MISSING), research/20261018-b-m1-6-check-readiness.md and -delivery.md, fix in B-M1-7 |
| 61w3f | [B-M1-7](B-M1-7.md) | DONE per status/B-M1-7.md (fixes for B-M1-6: deterministic ready tests, handoff fence, census gaps recorded); Opus fix review: readiness PASS (T1 queued), delivery closed as manager records (research/20261018-b-m1-7-check-*.md) |
| 61w3g | [B-M1-8](B-M1-8.md) | CLAIMED per status/B-M1-8.md (M1+M2 settling fixes needing no research: NeedsManager::Init at Init, GetBlockPool on connect, tags, M2-022 regression, M2-020/023 cleanups); Opus check FAIL (B1: connection-time needs persistence writes an unsupported record to the robot), research/20261018-b-m1-8-check.md, fix in B-M1-10 |
| 61w3h | [B-M1-9](B-M1-9.md) | CLAIMED per status/B-M1-9.md (OV127 adopted 2026-10-18: LatencyMessage, SetGameBeingPaused input, M2-024 handlers) |
| 61w3i | [B-M1-10](B-M1-10.md) | DONE per status/B-M1-10.md; Opus check FAIL (A1 device-file clock, A2 gutted Lab test; robot gate correct), research/20261018-b-m1-10-check.md, fix in B-M1-11 |
| 61w3j | [B-M1-11](B-M1-11.md) | DONE per status/B-M1-11.md; Opus fix review PASS with queued items (research/20261018-b-m1-11-check.md) |
| 61w3k | [B-M3M4-30](B-M3M4-30.md) | CLAIMED per status/B-M3M4-30.md (M3+M4 settling fixes: M4-041 zero LED state, M4-049 wiring, M4-051 callback, M4-019 cliff-history admission, M3-021 engine-thread camera calls, M4-027 tap filter, tags, reach labels); Opus check FAIL (research/20261018-b-m3m4-30-check.md), fixes in B-M3M4-32 |
| 61w3l | [B-M3M4-31](B-M3M4-31.md) | DONE per status/B-M3M4-31.md (OV128 adopted: M4-052 relative cube lights, M4-051 completion guard, M3-037 short raw skip-and-log); Opus check PASS with queued items (research/20261019-b-m3m4-31-check.md); M4-052 relative success waits on OV129 |
| 61w3m | [B-M3M4-32](B-M3M4-32.md) | DONE per status/B-M3M4-32.md (B-M3M4-30 check fixes: layer re-read after callback, no interleaved Updates, SetPoseStateHelper pose-state callback, labels); Opus check FAIL (research/20261019-b-m3m4-32-check.md), fixes in B-M3M4-34 |
| 61w3n | [B-M3M4-33](B-M3M4-33.md) | DONE per status/B-M3M4-33.md (robot-state history pulled into M3+M4, operator 2026-10-18: admission, queries, interpolation; wires cliff stats/pose, unexpected movement, vision frame pose); Opus check FAIL (research/20261019-b-m3m4-33-check.md), fixes in B-M3M4-34 |
| 61w3o | [B-M3M4-34](B-M3M4-34.md) | DONE per status/B-M3M4-34.md; manager fix review PASS (research/20261019-b-m3m4-34-check.md) |
| 61w3p | [B-M3M4-35](B-M3M4-35.md) | DONE per status/B-M3M4-35.md; manager check PASS (research/20261019-b-m3m4-35-check.md) |
| 61w3q | [B-M3M4-36](B-M3M4-36.md) | DONE per status/B-M3M4-36.md; manager check PASS (research/20261019-b-m3m4-36-check.md); the 16 held M3/M4 records settled |
| 61w3r | [B-M5-23](B-M5-23.md) | done 6f0f18b0; Opus check PASS (research/20261019-b-m5-23-check.md); M5-011/014/030 settled. Was: open — the animation head input is robot+0x2FC (M5-011, M5-014, M5-030); M5 settling cleanup |
| 61w3s0 | [B-M5-24](B-M5-24.md) | done bdad8435; Codex bounded review PASS (research/20261020-b-m5-24-check.md); cleanup queued, operator rerun required |
| 61w3s1 | [B-M5-25](B-M5-25.md) | done a9334fee; Codex bounded review PASS (research/20261020-b-m5-25-check.md); cleanup queued, operator rerun required |
| 61w3s2 | [B-M5-26](B-M5-26.md) | done99965613; Codex bounded review PASS (research/20261020-b-m5-26-check.md); finite-layer diagnostic gap retained, operator rerun required |
| 61w3s3 | [B-M5-27](B-M5-27.md) | done b23ce509; Codex bounded review PASS (research/20261020-b-m5-27-check.md); cleanup queued, operator rerun required |
| 61w3s4 | [B-M5-28](B-M5-28.md) | done7362c2da; Codex bounded review PASS (research/20261020-b-m5-28-check.md); generation diagnostic gap retained, operator rerun required |
| 61w3s5 | [B-M5-29](B-M5-29.md) | done b8a14a8e; Codex bounded review PASS (research/20261020-b-m5-29-check.md); admission diagnostic gap retained, operator rerun required |
| 61w3s6 | [B-M5-30](B-M5-30.md) | done967af8d8; Codex bounded review PASS (research/20261020-b-m5-30-check.md); queue-origin limits retained, operator rerun required |
| 61w3s7 | [B-M5-31](B-M5-31.md) | done8b9bc2d1; Codex bounded review PASS (research/20261020-b-m5-31-check.md);24..31 reviews complete, hardware/diagnostic gaps and successful driving outstanding |
| 61w3s8 | [B-M5-32](B-M5-32.md) | DONE; baseline published with B-M5-33 in c984cc1f; initial review FAIL F1/F2, both closed by research/20261020-b-m5-33-check.md |
| 61w3s9 | [B-M5-33](B-M5-33.md) | done c984cc1f, validation cb0f657c; Codex independent review PASS (research/20261020-b-m5-33-check.md); focused 79/79, builder full suite 5968/5968; operator rerun next |
| 61w3t | [B-M13-9](B-M13-9.md) | DONE 0138af17 + 3f54bccc; independent bounded PASS research/20261021-b-m13-9-check.md, focused53/53; shared-owner gaps and hardware remain; **Claude check FAIL** (false insertion stamp 0x00622D24, a VizManager store), fix B-M13-10 |
| 61w3u | [B-M11-18](B-M11-18.md) | HELD — proposed M11-098 core inventory addition/approval; bounded source review ready, no implementation authorization yet |
| 61w3v | [B-M10-2](B-M10-2.md) | DONE e332207b; bounded PASS research/20261021-b-m10-2-check.md; builder reports focused65/full6019, whole M10 remains open; Claude check PASS research/20261021-b-m13-9-b-m10-2-claude-check.md |
| 61w3w | [B-M13-10](B-M13-10.md) | DONE 7f42b427; Codex review PASS (Claude spot-check pending), focused53/full6019, no records settled; Claude check PASS research/20261022-b-m13-10-b-m13-11-claude-check.md |
| 61w3x | [B-M13-11](B-M13-11.md) | DONE6a8d507f; focused73/full6036 PASS; Codex review, Claude spot-check pending; Claude check PASS research/20261022-b-m13-10-b-m13-11-claude-check.md |
| 61w3y | [B-M13-12](B-M13-12.md) | DONE 7a8f79b5; Claude spot-check PASS research/20261022-b-m13-12-claude-check.md, fix queued B-M13-13 |
| 61w3z | [B-M13-13](B-M13-13.md) | DONE d67b73bd; Claude spot-check: code PASS, tests missing (research/20261022-b-m13-13-claude-check.md) |
| 61w40 | [B-M13-14](B-M13-14.md) | DONE; focused tests pass |
| 61w41 | [B-M13-15](B-M13-15.md) | DONE 7c45c2c9 (status 83ee04e0); full 6060/6060; Claude check PASS (research/20261023-b-m13-15-claude-check.md) |
| 61w41a | [B-M13-18](B-M13-18.md) | DONE 8f562461..76ccce0d; full 6065/6065 here; Claude check PASS (research/20261023-b-m13-18-claude-check.md); the reported blocker was an M3M4ReviewFixTests setup timing flake, fixed |
| 61w42 | [B-M13-16](B-M13-16.md) | DONE ec46eceb (status b5371ef0); full 6078/6078; 6 MISSING; Claude check PASS (research/20261023-b-m13-16-claude-check.md) |
| 61w43 | [B-M13-17](B-M13-17.md) | DONE f6f370bc (status 1b135e7f); full 6084; 3 MISSING; Claude check PASS (research/20261023-b-m13-17-claude-check.md) |
| 61w44 | [B-M13-19](B-M13-19.md) | DONE 07de2898 + fix-up 6343ddf5 (status d71d4338); full 6099/6099; Claude check PASS (research/20261023-b-m13-19-claude-check.md); ReviewFix 15 s stall open |
| 61w45 | [B-M13-20](B-M13-20.md) | DONE 96805ef3 + fix-up d0c1c2b7; Claude check PASS after fix-up (research/20261023-b-m13-20-21-22-claude-check.md) |
| 61w46 | [B-M13-21](B-M13-21.md) | DONE 46a971ba; Claude check PASS (same file) |
| 61w47 | [B-M13-22](B-M13-22.md) | DONE f65b3960 + c11385a2 + fix-up 934fcb7a, merged 9138555c; full 6133; Claude check PASS after fix-up (same file) |
| 61w48 | [B-M13-23](B-M13-23.md) | DONE 0fc1fba1; Claude check PASS (research/20261024-b-m13-23-24-claude-check.md) |
| 61w49 | [B-M13-24](B-M13-24.md) | DONE 33e868fe; Claude check PASS (same file) |
| 61w4a | [B-M13-25](B-M13-25.md) | BLOCKED on B-M13-25-wip: 56 failures, cubes get no pre-action poses (no native parented stored elements); unblocked by B-M13-26 |
| 61w4b | [B-M13-26](B-M13-26.md) | open, on B-M13-25-wip (cube stored pre-action elements through the native constructors) |
| 61w4c | [B-M13-27](B-M13-27.md) | open, after B-M13-26 (stop the legacy all-objects map sync; drop the ray fallback when unneeded) |
| 61w4 | [B-M12-7](B-M12-7.md) | HELD (manager, 2026-10-17: M1+M2 acceptance assessment; not open until released) — was open, after B-M2M15-2 (B-M12-3 check FAIL: dock handler retirement race; C5 queue; 0x4C producer) |
| 61w5 | [B-LOG-1](B-LOG-1.md) | HELD (manager, 2026-10-17: now a cleanup item under AGENTS.md "What we reproduce"; logs are best effort, not on the acceptance path) — was open, after B-M12-7 (six log sites with wrong key/text/channel, found by logkey_check.py) |
| 61x | [B-M15-8](B-M15-8.md) | HELD (manager, 2026-10-17: M1+M2 acceptance assessment; not open until released) — was open, after B-M1-2 (expose approved spark/activity inputs) |
| 61y | [B-M11-16](B-M11-16.md) | HELD (manager, 2026-10-17: M1+M2 acceptance assessment; not open until released) — was open, after B-M15-8 (markerless quad M11-091; VisitInterestingEdge M11-092; re-root fixes) |
| 61z | [B-M15-9](B-M15-9.md) | HELD (manager, 2026-10-17: M1+M2 acceptance assessment; not open until released) — was open, after B-M11-16 (put-down kick-out M15-060; Feeding M15-061/M15-064; Lab residue M15-062) |
| 61za | [B-M9-5](B-M9-5.md) | HELD (manager, 2026-10-17: M1+M2 acceptance assessment; not open until released) — was open, after B-M15-9 (modulator-state lifecycle M9-042; MIDI init/parser M9-039/M9-005; lookup M9-045) |
| 61zb | [B-M6-5](B-M6-5.md) | HELD (manager, 2026-10-17: M1+M2 acceptance assessment; not open until released) — was open, after B-M9-5 (bus residue M6-037; Term music/variant M6-038; stream class M6-039; JNI policy M6-044) |
| 61zc | [B-M11-17](B-M11-17.md) | HELD (manager, 2026-10-17: M1+M2 acceptance assessment; not open until released) — was open, after B-M6-5 (GuardDog starting pose M11-093; footprint quad M11-094) |
| 61zd | [B-M6-6](B-M6-6.md) | HELD (manager, 2026-10-17: M1+M2 acceptance assessment; not open until released) — was open, after B-M11-17 (Term callback contexts and notifications M6-042; N12 decided 2026-10-16: skip and log) |
| 61ze | [B-M15-10](B-M15-10.md) | HELD (manager, 2026-10-17: M1+M2 acceptance assessment; not open until released) — was open, after B-M6-6 (FeedingEat M15-065; tag-53 order M15-066/M15-068) |
| 61zf | [B-M9-6](B-M9-6.md) | HELD (manager, 2026-10-17: M1+M2 acceptance assessment; not open until released) — was open, after B-M15-10 (meter tempo/MIDI reader M9-044; modulator factors M9-043; collector M9-041) |
| 62 | [B-M9-2](B-M9-2.md) | BLOCKED per status/B-M9-2.md (LFO mode 0 M9-025; envelope timing M9-030; note-off M9-031; +0x44 M9-032; rate globals M9-033) |
| 63 | [B-M12-5](B-M12-5.md) | BLOCKED per status/B-M12-5.md (pyramid-base geometry M12-043; PreActionPose +0x14 M12-044) |
| 64 | [B-M8-5](B-M8-5.md) | BLOCKED per status/B-M8-5.md |
| 65 | [B-M11-10](B-M11-10.md) | BLOCKED per status/B-M11-10.md |
| 66 | [B-M15-4](B-M15-4.md) | BLOCKED per status/B-M15-4.md |
| 67 | [B-M11-11](B-M11-11.md) | BLOCKED per status/B-M11-11.md |
| 68 | [B-M8-6](B-M8-6.md) | BLOCKED per status/B-M8-6.md |
| 69 | [B-M11-12](B-M11-12.md) | BLOCKED per status/B-M11-12.md (border consumer M11-079; teardown leaves M11-078; __next_prime tests M4-050; required-process gate M8-013) |
| 70 | [B-M7-3](B-M7-3.md) | BLOCKED per status/B-M7-3.md |
| 71 | [B-M6-2](B-M6-2.md) | BLOCKED per status/B-M6-2.md |
| 72 | [B-M15-5](B-M15-5.md) | BLOCKED per status/B-M15-5.md |
| 73 | [B-M11-13](B-M11-13.md) | DONE per status/B-M11-13.md |
| 74 | [B-M15-6](B-M15-6.md) | BLOCKED per status/B-M15-6.md |
| 75 | [B-M6-3](B-M6-3.md) | BLOCKED per status/B-M6-3.md |
| 19 | [B-M3M4-12](B-M3M4-12.md) | done 802717ae; Opus check PASS (research/20261012-b-m3m4-12-check.md) |

## Research lane (clone `cozmo-stack-codex`)

**Reading hold LIFTED 2026-10-22 (manager): the M13/M11/M8/M10 backlog is reviewed. At most 3–4 open research rows, each queued only for the next build. History of the hold, 2026-10-21 09:30:** Research answers outran review: 20 of jobs 24–50 are
"pending independent review/adoption" and only two builds came out. Until the backlog is cleared:
1. **Queue no new research job.** Finish only the rows already `open` (51–53), then stop reading.
2. **Review the backlog in plan order** (`acceptance/M10-M15-blockers.md`): M13 first (37 R-M13-DRIVEOFF-JOINS, 38
   R-M11-CHARGER-MAP, then M13-017's remaining rows), then M11 foundations (25, 28, 30, 31, 34, 36, 44), then M8
   (26, 29, 32, 33, 40, 43, 46), then M10 (27, 35, 45). Leave M7/M12/M14/M15 answers (39, 41, 42, 47–53) unreviewed
   until their layer's turn.
3. **A review checks the cited instructions in the `.so` itself** and writes `research/<date>-<job>-check.md`. Mark each
   review "Codex review; Claude spot-check pending" while Claude is out of usage. A finding needed by a build gets a
   Claude/Opus check before that build is accepted.
4. **Build only from a reviewed answer,** one builder job at a time, toward finishing M13-017 first. The next robot check
   (dock, drive off, cliff threshold stays 50 until the footprint leaves the charger) waits for M13-017.
5. **Lifting the hold:** when no answer is left pending review in the current layer, return to at most 3–4 open
   reading jobs, each queued only for the next build it feeds.

**Operator, 2026-10-21 (earlier):** keep 3–4 independent reading jobs queued at a time.
First batch: R-M13-017, R-M11-POSE-HISTORY, R-M8-ACTIONLIST,
R-M10-CLASSIFIER (all answered; pending review). Continue with the first open row.
Run one request at a time; after publishing its answer, continue
to the next without waiting for adoption or a build. Each pass reads its own HEAD
and records it. These are independent reports, not permission to settle records or
release builder jobs. Manager review and inventory approval still precede builds.
Prioritize this live queue over overnight builder pre-flight when these jobs are open.

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
| 24 | [R-M13-017](../research/requests/20261021-m13-017-located-charger.md) | checked with A9 correction (new state chooses light trigger), research/20261021-m13-017-located-charger-check.md; contact inventory integrated as M13-040 and B-M13-9 released e48d585b; D1 exhaustive caller census not verified |
| 25 | [R-M11-POSE-HISTORY](../research/requests/20261021-m11-pose-history-settling.md) | answered c419483d; redone independent review 20261022-r-m11-pose-history-check.md (Claude spot-check pending); 6 claims confirmed with disassembled proof; interp/compute/last-frame partially quoted; 170+ claims not reviewed this pass |
| 26 | [R-M8-ACTIONLIST](../research/requests/20261021-m8-actionlist-completion-settling.md) | answered e0c71224; redone independent review 20261022-r-m8-actionlist-check.md (Claude spot-check pending); admission entry + cancel/deletion (tag reentry guard) confirmed by disassembly; tick/runner/completion/delivery/compound claims not reviewed |
| 27 | [R-M10-CLASSIFIER](../research/requests/20261021-m10-classifier-settling.md) | answered 9d1efe80; redone independent review 20261022-r-m10-classifier-check.md (Claude spot-check pending); F1 (6 angle literals) verified byte-exact at 0x00511E66/6A, 0x00511EE6/EA, word 0x005122BC=0x3F490FDB, f64 at 0x005122A0/A8/B0; F2 precommit cancel (ActionList.Cancel(-1) at 0x00512030-38 before committed-byte store 0x0051208E) and postcommit detach/platform-clear (states 2..6 detach 0x00512100-210C, nonzero charger-platform clear 0x005121DC-1F4) confirmed; remaining M10-001/002/007/009/010/011/013/014/015/017 claims not individually disassembled this pass (B-M10-2 already verified M10-002 body) |
| 28 | [R-M11-TIMEOUT](../research/requests/20261021-m11-history-timeout-input.md) | answered 6d43bd2a; redone independent review 20261022-r-m11-history-timeout-check.md (Claude spot-check pending); T1 storage/T2 Init/T3 Configure/T6 history-read (6) confirmed by disassembly; T4/T5/T7 partially verified (T4/T7 not independently re-scanned) |
| 29 | [R-M8-COMPLETION](../research/requests/20261021-m8-animation-completion-payload.md) | answered f395e545; redone independent review 20261022-r-m8-completion-check.md (Claude spot-check pending); Cancel 0x00540AA8 (blx r2 at 0x00540AE4) and Play variant-1 getter 0x0075C544 (cmp r2,#1; bne; adds r0,#4; bl 0x0075C574) confirmed; 0x005441DE range mismatches declared function size (30) — partial contradiction; tick/runner/delivery/compound not reviewed; Claude check: review's Contradicted verdicts overruled, original answer stands (research/20261022-free-model-reviews-claude-check.md) |
| 30 | [R-M11-FRAME-POSE](../research/requests/20261021-m11-frame-pose-assembly.md) | answered46c8a213; redone independent review 20261022-r-m11-frame-pose-check.md (Claude spot-check pending); query gates + VisionPoseData.Set layout + call sites confirmed; calibration/LUT/pose-copy/observable-consumers not reviewed |
| 31 | [R-M11-POSE-SERIAL](../research/requests/20261021-m11-pose-serialization-callers.md) | answeredc334871d; redone independent review 20261022-r-m11-pose-serial-check.md (Claude spot-check pending); inbound+outbound converters (0x00846C70/6D38) and GetRobotState (0x005180E0) confirmed by disassembly; claims 4-16 not reviewed |
| 32 | [R-M8-FEED-SURVIVOR](../research/requests/20261021-m8-feeding-clear-survivor.md) | answered 166a2e6a; redone independent review 20261022-r-m8-feed-survivor-check.md (Claude spot-check pending); Update tail (0x0053F6C6..6D4 delete + sentinel), Cancel tag-reentry (0x0053E690), DeleteActionAndIter saved-successor (0x0053FA86..9C before local5A 0x0053FAB2) confirmed by disassembly; state-2 producer/teardown claims not reviewed |
| 33 | [R-M8-PROFILE](../research/requests/20261021-m8-action-motion-profile-lifetime.md) | answered 5e1b0a13; redone independent review 20261022-r-m8-profile-check.md (Claude spot-check pending); SetForAction 0x0064AC3A..48 + SetCustomMotionProfile 0x0064AB10..ABA6 + Clear 0x0064AC4A..50 + SmartSet 0x005BE518..3E + SmartClear 0x005BD584..A6 + factory tail 0x0052A1D8..214 + StartDriving 0x0064ADD2..DF0 confirmed by disassembly; TurnInPlace/DriveStraight/IDock SetMotionProfile cited offsets are inside SetAccel/SetDecel (contradicted); BehaviorDriveInDesperation cited addr is its dtor (contradicted); Claude check: review's Contradicted verdicts overruled, original answer stands (research/20261022-free-model-reviews-claude-check.md) |
| 34 | [R-M11-HISTORY-INDEX](../research/requests/20261021-m11-computed-history-index.md) | answered bec5a0dc; redone independent review 20261022-r-m11-history-index-check.md (Claude spot-check pending); H1-H13 confirmed by disassembly of ComputeAndInsertStateAt 0x00531B84; initial/reset and live-marker caller not reviewed |
| 35 | [R-M10-OBSTACLE-ALIAS](../research/requests/20261021-m10-obstacle-flag-alias.md) | answered 06b06760; redone independent review 20261022-r-m10-obstacle-alias-check.md (Claude spot-check pending); A1 (AI alloc 0x34 at 0x50FD94, published to Robot+0x264 at 0x50FDA4) and A11 (slot clear at 0x51126A..78 / 0x5114B8..CC before dtor) and AI ctor default byte+4=0 at 0x569A7E/80 + predicate 0x6143CA/CE (no sensor/map lookup) verified by disassembly; A2-A12 child-alias census, escaped-alias closure, universal no-writer claim not instruction-verified (answer already discloses as UNKNOWN) |
| 36 | [R-M11-POSE-CONFIG](../research/requests/20261021-m11-pose-change-configuration.md) | bounded Codex review recorded; redone independent review 20261022-r-m11-pose-config-check.md (Claude spot-check pending); C1/C2/C3/C5/C6/C7/C8/C9/C10/C11/threshold verified by disassembly; C12 and downstream not reviewed; no build release |
| 37 | [R-M13-DRIVEOFF-JOINS](../research/requests/20261021-m13-driveoff-remaining-joins.md) | answered 19b0a313; expanded caller/constructor/cancel review research/20261021-m13-driveoff-remaining-check.md; independent review 20261022-r-m13-driveoff-joins-check.md (Codex review; Claude spot-check pending); citations D1-D6/wait matrix/StopActing/constructor/completion confirmed; severe-need writer and M8-015 tag binding remain open |
| 38 | [R-M11-CHARGER-MAP](../research/requests/20261021-m11-charger-map-add-join.md) | answered 9ff5b508; bounded add/cache/height review research/20261021-m11-charger-map-add-check.md; independent review 20261022-r-m11-charger-map-add-check.md (Codex review; Claude spot-check pending); height defect and family4/type4-add/type5-remove confirmed; cache/valid-valid/owner bindings remain open |
| 39 | [R-M15-SEVERE-INPUT](../research/requests/20261021-m15-severe-need-input.md) | answered 3609db0c; pending independent review/adoption |
| 40 | [R-M8-EXTERNAL-ADMISSION](../research/requests/20261021-m8-external-action-admission.md) | answered b50df484; redone independent review 20261022-r-m8-external-admission-check.md (Claude spot-check pending); E1 (Robot+2C7 init 0), E3-E6 (transition broadcast before flag, old/new both types, flag-write r1 logic, MC+D4 first then Robot+2C7), E8 helper confirmed by disassembly; E2 (BehaviorManager constructor), E7 body (0x5A2270), second tag range, indirect-writer closure not reviewed; record M8-012/M8-007 remain gaps |
| 41 | [R-M7-SDK-BOUNDARY](../research/requests/20261021-m7-sdk-record-boundary.md) | answered 97bdc60f; pending independent review/adoption |
| 42 | [R-M12-CACHE-LIFETIME](../research/requests/20261021-m12-predock-cache-lifetime.md) | answered d257817c; pending independent review/adoption |
| 43 | [R-M8-TEARDOWN-CLOCK](../research/requests/20261021-m8-teardown-timer-readers.md) | answered dc3004dd; redone independent review 20261022-r-m8-teardown-clock-check.md (Claude spot-check pending); engine-remove-before-robots (0x004EC6B4 before 0x004EC702), getInstance/removeInstance exact, Hiccup D1 (threshold+0x2C, bls, SendDasEvent(false), _this clear, tail base) at 0x0061024C, SendDasEvent body (getInstance/timestamp, start+0x54, elapsed, SELF_CURED/PLAYER_CURED selection, sEventF robot.hiccups.ended %s) at 0x006102B8, FeedingEat conditional reader (getInstance, seconds getter, duration+now store at +0x124) at 0x005D62F6 all verified by disassembly; global binding vector, future consumption, intervening UpdateTime, FeedingEat local5A remain UNKNOWN per answer |
| 44 | [R-M11-RETAINED-OWNER](../research/requests/20261021-m11-retained-pose-owner-comparison.md) | answered 00a2d322; redone independent review 20261022-r-m11-retained-owner-check.md (Claude spot-check pending); SetPose 0x004EF590 and CopyWithNewPose 0x00506EF8 confirmed by disassembly (parent through assignment); SetObjectAsAttachedToLift only C half verified; remainder not reviewed |
| 45 | [R-M10-OWNER-AVAILABILITY](../research/requests/20261021-m10-consequence-owner-availability.md) | answered 5f06c9ab; redone independent review 20261022-r-m10-owner-availability-check.md (Claude spot-check pending); FallingStopped gate verified (intensity > 1000 -> RegisterNeedsActionCompleted(17) at 0x005350AA..C2; vcmpe ble cut at 1000 with beq at 0x005350BA); 0x00535186..9C tail/DAS not individually quoted; C# host-ownership composition and NeedsManager construction moment not instruction-verified (authority-2 evidence); Claude check: review's Contradicted verdicts overruled, original answer stands (research/20261022-free-model-reviews-claude-check.md) |
| 46 | [R-M8-NAME-INPUT](../research/requests/20261021-m8-animation-name-input-boundary.md) | answered 80a357a2; redone independent review 20261022-r-m8-name-input-check.md (Claude spot-check pending); Play name input 0x00543C44 (strd empty +0x78/+7C; str caller +0x78; if SSO copy 3 words else bl 0x4E02B2) confirmed, Trigger group selection+copy 0x00544462 (blx #0x4AAE58 then bl #0x4E462A into +0x78; empty → beq 0x544634) confirmed, JSON first-member selection at 0x00588700..66 confirmed; corpus census (997/884) not re-derived; no native length/encoding validation recovered |
| 47 | [R-M15-TRACKER-OWNER](../research/requests/20261021-m15-tracker-owner-join.md) | answered b3651f3a; pending independent review/adoption |
| 48 | [R-M14-IMAGE-TURN](../research/requests/20261021-m14-image-turn-history-join.md) | answered 50138aea; pending independent review/adoption |
| 49 | [R-M7-MOOD-JOIN](../research/requests/20261021-m7-mood-completion-owner-join.md) | answered 3f5a1337; pending independent review/adoption |
| 50 | [R-M14-IMAGE-INPUT](../research/requests/20261021-m14-image-coordinate-input.md) | answered b7b32940; pending independent review/adoption |
| 51 | [R-M12-DETACH-POSE](../research/requests/20261021-m12-detach-pose-join.md) | open |
| 52 | [R-M7-MOOD-INIT](../research/requests/20261021-m7-mood-init-input.md) | answered b19ce1bc; pending independent review/adoption |
| 53 | [R-M14-PET-SIZE](../research/requests/20261021-m14-pet-image-size-boundary.md) | answered d2fa68f3; pending independent review/adoption |
| 54 | [R-M7-MOOD-MISSING](../research/requests/20261021-m7-missing-mood-config-outcome.md) | HELD (reading hold 2026-10-21; M7/M14 wait for their layer) |
| 55 | [R-M14-CALIBRATION-CORPUS](../research/requests/20261021-m14-existing-calibration-corpus.md) | HELD (reading hold 2026-10-21; M7/M14 wait for their layer) |
| 56 | [R-M13-017-REST](../research/requests/20261022-m13-017-remaining-footprints.md) | answered 321c6ac0; **Claude check: central claim wrong** (charger is ActionableObject, not Markerless; research/20261022-r-m13-017-rest-claude-check.md); not usable for a build |
| 57 | [R-M11-CONFIG-SUBSCRIBERS](../research/requests/20261023-m11-config-notify-subscribers.md) | answered 5c3e88f2; Claude check PASS (research/20261023-r-m11-config-subscribers-claude-check.md); feeds B-M13-18 |
| 58 | [R-M13-038-LIFETIME](../research/requests/20261023-m13-038-audio-client-lifetime.md) | answered 528ea827; Claude check PASS (research/20261023-r-m13-038-lifetime-claude-check.md); agrees with the adopted M13-038 result; no new build |
| 59 | [R-M11-TREE-GEOMETRY](../research/requests/20261023-m11-tree-insert-ray-geometry.md) | answered 8c56752b; Claude check PASS (research/20261023-r-m11-tree-geometry-claude-check.md); feeds B-M13-19 |
| 60 | [R-M11-MAP-PRODUCERS](../research/requests/20261023-m11-map-legacy-producers.md) | answered 393bc407; Claude check PASS (research/20261023-r-m11-producers-m12-charger-preaction-claude-check.md); feeds B-M13-20, B-M13-21 |
| 61 | [R-M12-CHARGER-PREACTION](../research/requests/20261023-m12-charger-preaction.md) | answered 475c6ebc; Claude check PASS (same file); feeds B-M13-22 |
| 62 | [R-M11-FILLBORDER](../research/requests/20261024-m11-fillborder.md) | answered 4f539ae5; Claude check PASS (research/20261024-r-m11-fillborder-objectdata-m12-world-claude-check.md); feeds B-M13-23 |
| 63 | [R-M11-OBJECT-DATA](../research/requests/20261024-m11-object-map-data.md) | answered e039f360; Claude check PASS (same file); feeds B-M13-24 |
| 64 | [R-M12-PREACTION-WORLD](../research/requests/20261024-m12-preaction-world.md) | answered f57b04ac; Claude check PASS (same file); feeds B-M13-25 |
