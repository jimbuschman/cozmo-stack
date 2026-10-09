# Project state

Read first in every session. The manager keeps this file current; the process it follows is the Process section of `AGENTS.md`.

## The plan (operator, 2026-10-05): one layer at a time

**Why:** five workers in parallel produced merge conflicts, a long checking queue and no finish line. Nothing has been
ACCEPTED yet.

**The order**, bottom-up. Each layer is finished all the way to ACCEPTED before the next starts:
1. M1 + M2: connecting and protocol.
2. M3 + M4: devices and motor control.
3. M5: animation and the face.
4. M10–M15 and M7/M8: vision, cubes, navigation, behaviours.
5. The sound layers, M6 + M9, **last** (operator, 2026-10-07). Sound already plays, approximately, through the direct
   decoder. Even under ADP-1 its decision rows run to about 140 obligations (queue 5, Q14), so the robot's behaviour
   layers are finished first.

**Done for a layer means:**
- every record is EXACT_SOURCE, or a policy record that the manager has confirmed legitimate (COMPATIBILITY_POLICY,
  EQUIVALENT_IMPLEMENTATION), or HARDWARE_ONLY settled by a robot run;
- the layer's hardware script has been run by the operator;
- the review state is ACCEPTED.

**The cross-layer rule.** A lower-layer record whose path runs into a higher layer is split. The lower-layer part is
finished and accepted now. The higher-layer part becomes a record in that higher layer, with its own citation, and is
finished when that layer's turn comes. Examples: M1-044 destroys the behaviour layer; M1-045's go-to-sleep runs on the
ActionList.

**Two lanes at a time:**
- **one builder** (DeepSeek, or a Sonnet when the allowance allows);
- **one checker:** Codex finds defects, then a single Opus pass settles the layer.

Work already in flight (B-ACTIONS, B-FACE, B-M6b-4, R-FIX3 and the parked branches) is finished or parked as it
stands. Nothing new starts outside the current layer.

**Audio (operator, 2026-10-05):** ADP-1 in AGENTS.md. Exact decisions, equivalent per-sample DSP, with thresholds measured against the emulator (`jobs/B-ADP-HARNESS.md`). No bulk reclassification.

**2026-10-09: M2's acceptance was reopened by the M1/M2 disposition correction.** The corrected inventories are now re-approved; both M1 and M2 are back at INVENTORY_APPROVED pending the remaining M1 builds and the layer's acceptance run.

**The Codex builder trial passed.** The Opus pass (`research/20261009-M1M2-opus-pass.md`) settled 5 of 8 records (M1-025, 031, 041, 045, M2-002). The defects were few: one came from a misnamed row the manager had approved (M1-024), and one is a log level (M1-044/015). Codex stays the builder.

**M1's remaining work:**
- fix M1-024 (the SetPrevNeedsBrackets snapshot) and the M1-044/015 warning level;
- apply the MISSING and policy dispositions (`jobs/B-M1M2.md`);
- build M1-029's J1/J7, M1-046 and the four new M1 records;
- the operator's packed-frame robot run for M1-033. M1-043 is now EQUIVALENT_IMPLEMENTATION.

**Current layer: M1 + M2**, job `jobs/B-M1M2.md`. It has 70 records after the cited disposition correction: 53 M1 and 17 M2. The 2026-10-09 inventory reapproval moved M2 back from ACCEPTED to INVENTORY_APPROVED. The remaining M1 build list and the M1-033 hardware run are below.

**2026-10-09 resumed M1/M2 build progress (pushed):** the Opus corrections and cited MISSING/policy disposition are applied. `ce859bf` fixes the M1-024 decay snapshot caller and the M1-044/M1-015 warning level. The one-batch cited inventory correction is `3d2018c`, and manager-reopened extraction rows/ownership corrections are `5456b3f`. The checked finite-real M1-029 J1 wrapper and maximum reachable J7 input are built in `21b8738`; J1 non-finite mapping and other listed MISSINGs remain open. M1-048's checked U1-U7 engine decisions are assigned to the existing live UDP implementation in `d42130f`, awaiting strong verification. `0ab7fd8` records the remaining MISSINGs and stop point. No records were settled; M1 and M2 remain INVENTORY_APPROVED, and no hardware acceptance was started. The itemized open boundaries are in `jobs/status/B-M1M2.md`.

## Now (2026-10-02)

- **Codex's answers have been checked and applied.** Codex's DEFECT findings were spot-checked in the binary and held,
  and its HOLDS are not trusted (`research/20261002-B-CORE2-verify.md`). Corrections:
  - A3 (M10/M15/M14, 15 records), A4 (M11, 14), A5 (M3-024), A6 (13 in M13, which the 09-29 audit flagged but never
    demoted) and A7 (M6-003) all go back to IMPLEMENTATION_GAP;
  - M6 correction C30 adopts the Opus-verified live-audio check (`research/20261002-M6-live-audio-verify.md`).
- **B-CORE2 (DeepSeek):** Opus-verified. M3-025 and M3-035 are settled; the defects on the rest are in `unresolved`.
- **Counts:** 129 EXACT_SOURCE, 231 IMPLEMENTATION_GAP, 18 RECOVERABLE_GAP, 10 HARDWARE_ONLY, 2 BLOCKED_EXTERNAL,
  26 COMPATIBILITY_POLICY, 2 EQUIVALENT_IMPLEMENTATION.
- **Next:**
  - Sonnet 1 resumes B-M6b-4 with C30, starting with the C# defects and the production wiring (C30.W);
  - Sonnet 2 starts R-BEH2;
  - Codex works one task per prompt, each answer opening with a coverage table;
  - DeepSeek's next job comes from the checked pre-extractions.

## 2026-09-30, morning (superseded by the section above)

- **Everyone finished overnight.**
  - **B-CORE (DeepSeek, window 3):** DONE. Six batches, pushed: dbdc39d, ac62741, afbb769, 32216ca, 99a51f6,
    bee7266, 23c65ac. About 30 M1..M4 records built; each is IMPLEMENTATION_GAP, "built, awaiting strong verification".
    Named MISSING: M3-027's data vector +0xE8, M3-023's `StartCamera`.
  - **Sonnet 1:** B-M6b-4 batch 4b (the playback-limit walker, M6-026).
  - **Sonnet 2:** R-VIS round 1 (M11 batch A, M12, M13; M12-023..039 and M11-050..053 recorded). Nothing raised to
    EXACT_SOURCE. Its final round of changes had no separate verifier pass. Its open list is in `jobs/status/R-VIS.md`.
  - **Codex:** the audit calibration (`research/20260929-audit-calibration.md`, 6132f73). Its calibration says the R-BEH2
    pre-extraction (`research/20260929-R-BEH2-pre-extraction.md`) is complete, but that file is not committed yet;
    it is still in `cozmo-stack-codex`.
- **The calibration contradicts the Sonnet audit.** M12: 9 hold, 7 fail, 1 partial, against Sonnet's 17 of 17.
  M5 part A: 11 hold, 2 partial. The manager re-checked the central addresses in the binary (path-id wrap
  0x0064A3C2, clamp 0x3F32B8C2, the two-pose gate 0x005BEE80, Verify RUNNING 0x00553CA0, the single urandom read
  0x0082F898). Correction A2: M12-002/007/010/019 and M5-005/017 go back to IMPLEMENTATION_GAP, and the defects are
  added to M12-001/012/017. M12-004 was already rebuilt by R-VIS. M12 and M5 are re-approved.
  - Consequence: the other Sonnet-audited layers (M10, M11, M14, M15) are not trusted until an Opus re-audit.
  - **Rule change (CHECKLIST.md section 6):** Sonnet never settles, audits or verifies for settlement. Only the Opus
    manager's verifiers settle.
- **Merged locally, not pushed.** The manager's repo has Sonnet 1's 4b, R-VIS's three commits, a merge fix and A2 on
  top of B-CORE and Codex. The merge fix: B-CORE's BlockFilter hook is renamed `BlockFilterInit`, because R-VIS used
  `PhysicalRobotSet`, and the calls are in `SetPhysicalRobot`'s order.
  - The six `M13_028_*` flip tests failed on the merge (R-VIS was built before B-CORE's track locks). Sonnet 2
    fixed them from the binary (89a718e): the flip's carry lift sets +0x56 (0x0055F154), and IActionRunner then skips
    AreAnyTracksLocked/LockTracks (0x00540434) and every unlock (0x005408EC, 0x00540264, 0x0054120C). Opus verifier:
    PASS.
  - **Queued (non-blocking):**
    - `M13_028_TheCarryLiftRunsWhileTheApproachLiftHoldsTheTrackAndTakesNoLock` uses the angle `Asin(1.0)`, which
      is 111 mm, not 92, so its "releases nothing" half never reaches the carry lift's end. Use `Asin(47.0/66)`.
      Its lock half cannot tell the old code from the new, because every lock shares one owner key.
    - M4-003 and M13-028 should also cite the +0x56 gates in Interrupt (0x00540264) and ~IActionRunner (0x0054120C).
  - **For the B-CORE verification (possible defect, M4-003):** the engine's LockTracks passes the action's name
    (+0x48) and id (+0x60) as the lock owner (0x00540584..0x0054058A). The C# uses one constant `ActionRunnerWho`
    for every action, so lock ownership between concurrent actions is lost.
- **B-CORE verified by Opus (2026-09-30):** 5 of about 35 records settle: M4-011, M4-025, M2-003, M3-029 and M1-028.
  The rest stay IMPLEMENTATION_GAP, each with its defect in `unresolved`; the reports are
  `research/20260930-B-CORE-verify-{1-2,3-4,5-6}.md`. The main defects:
  - on-idle and ready-to-stream open one Update early;
  - a parallel calibration read, and the Needs read queued late;
  - the head clip is missing its 1e-5 dead zone;
  - the timeout sends its stop and unlock in the wrong order, and has the wrong result and clock;
  - the Needs tick uses double where the engine uses float;
  - System.Text.Json stands in for the shipped jsoncpp;
  - M1-044 and M1-045 are not built.
  Log-only defects block settling too. **For DeepSeek:** its batches were organised and honest about gaps, but most
  records failed strong verification on gates, order and width, so a DeepSeek build is a first pass, not a finish.
- **Counts:** 418 records. 171 EXACT_SOURCE, 189 IMPLEMENTATION_GAP (about 60 of them built and awaiting
  verification), 18 RECOVERABLE_GAP, 10 HARDWARE_ONLY, 2 BLOCKED_EXTERNAL, 26 COMPATIBILITY_POLICY,
  2 EQUIVALENT_IMPLEMENTATION.
- **Next for the manager:**
  - run the suite and push once, then Sonnet 2 pulls and starts R-BEH2 (with the pre-extraction check's corrections);
  - Opus verification of B-CORE's batches, then B-M6b-4 and R-VIS, against CHECKLIST.md and the binary;
  - an Opus re-audit of M10, M11, M14 and M15;
  - once the R-BEH2 pre-extraction is pushed, check it and give R-BEH2 to a Sonnet;
  - the control-check robot run after B-CORE is verified.

## 2026-09-29, evening (superseded by the section above)

- **The complete audit changed the picture.** Every EXACT_SOURCE record (239) was re-checked by independent verifiers:
  **155 hold**. 84 went back to IMPLEMENTATION_GAP, each with its defect in `unresolved` and a correction A1 in its
  inventory. 21 of those (M11, M13, M14) are handed to R-VIS, and R-VIS demotes them itself because it is editing
  those layers. The results, by layer, the five recurring failure patterns and the plan are in
  `re-analysis/research/20260929-audit-complete.md`; the M7/M8 detail is in `20260929-audit-M7-M8.md`.
  - Solid: M1, M2, M5, M10, M12, M15, M6. Weak: M3, M4, M11, M14. Badly off: M7, M8, M9, M13.
  - The Sonnet-audited layers (M5 part A, M10, M11, M12, M14, M15) ran at about half the Opus depth. Codex is
    re-auditing M12 and M5 part A to calibrate (`requests/20260929-audit-calibration.md`).
- **The new job format** (`re-analysis/jobs/CHECKLIST.md`, in every agent's instructions):
  - each job names the production path (the engine function and the C# entry), with no parallel copies;
  - the gates, order and failure results around every path;
  - nothing deferred to a comment;
  - floats as the engine's bits, at the engine's width;
  - tests from the binary;
  - **cheap models never settle records.** A DeepSeek job ends "built, awaiting strong verification", and a Claude
    verifier settles it.
  - `FidelityLiteralLintTests` fails on new 6-8-digit decimal literals in fidelity-tagged code (437 baselined).
- **Workers now:**
  - Sonnet 1 (Claude Code, clone `C:\Users\jbuschman\Downloads\cozmo-stack-r`): B-M6b-4, the live audio. Batch 4a
    is done. Then R-M9 and R-M6.
  - Sonnet 2 (clone `cozmo-stack-r2`): R-VIS, then R-BEH2 (the 19 demoted M7/M8 records, Codex pre-extraction).
  - DeepSeek window 3: B-CORE, the connection and device core (NV queue, readiness, track locks), the first job in the
    new format. Windows 1 and 2 are idle to save OpenRouter credits.
  - Codex: the R-BEH2 pre-extraction, then the audit calibration.
  - Neither Sonnet clone can push. The operator runs `git push origin main` in its window when it asks.
- **Next for the manager:**
  - verify B-CORE's batches against the checklist in the binary, and settle what holds;
  - collect and check the Codex answers;
  - after B-CORE, a control-check robot run (readiness and track locking change what goes on the wire).
- **Tooling:** the push gate skips the suite when this clone already passed it on identical code and records, and
  refuses at once when origin has moved.

## Earlier today (2026-09-29, before the audit; superseded by the section above)

- **Every layer, M1 to M15, is built once and inventory-approved. None is ACCEPTED yet.** The manifest has 371 records:
  - 220 EXACT_SOURCE;
  - 99 IMPLEMENTATION_GAP;
  - 8 RECOVERABLE_GAP, 7 of them on the live path;
  - 10 HARDWARE_ONLY;
  - 2 BLOCKED_EXTERNAL;
  - 30 COMPATIBILITY_POLICY;
  - 2 EQUIVALENT_IMPLEMENTATION.
- **Round 1 of the parallel jobs is DONE** (2026-09-27..29, `re-analysis/jobs/BOARD.md`): X1..X5, I-/B- for M6..M15,
  with B-M6b split in three.
  - **M6-002, Vorbis, is bit-exact with the engine end to end.** Nine shipped sounds, mono and stereo, both block-size
    pairs, match the engine's own decoder sample for sample under emulation (`re-analysis/tools/emu/emu_vorbis.py`,
    `WwiseVorbisWholeDecodeNativeTests`; 115b002/dd7f398). It stays IMPLEMENTATION_GAP until B-M6b-3 wires it live.
  - The emulator tools (`re-analysis/tools/emu/`) run the engine's own ARM code under Unicorn. They are the exact oracle
    for numeric code: IMDCT, window combine and the whole decode so far.
- **Round 2, running:**
  - window 1: G-M7, then G-M15;
  - window 2: B-M6b-3, the live audio wiring. Part 1 is b926d00.
  - window 3: G-M14.
  
  The G jobs fold Codex's checked overnight extractions (M7-021, M15-014, M14-011) into their subsystems and build
  them. The manager spot-checked each report's key citations in the binary first.
- **Round 3, queued:** R-BEH → R-VIS (window 1), R-M6 (window 2), R-DEV → R-ANIM (window 3). It triages and closes
  the 99 IMPLEMENTATION_GAP records ([R.md](re-analysis/jobs/R.md)).
- **Codex:** the four M9 singing gaps (`re-analysis/research/requests/20260929-M9-singing-gaps.md`), then the review
  of the 32 COMPATIBILITY_POLICY / EQUIVALENT_IMPLEMENTATION records against "Exact, always"
  (`20260929-policy-review.md`). The manager decides each review verdict. Operator decisions are flagged, not reversed.
- **Tooling fixes on 2026-09-29:**
  - `run-job.ps1` always gives the opencode CLI its own data directory (`%USERPROFILE%\.opencode-cli`);
  - the push gate refuses at once when origin/main has moved, instead of running the suite first.
- **Operator decision (2026-09-29), M5-020:** no invented faces. The expression API is rebuilt on the shipped Code Lab
  expression mapping (`CodeLabGame.GetAnimationTriggerForScratchIndex`), in R-ANIM.
- **Waiting on the operator, once the jobs land:**
  - the B-M6b-3 listening check (its steps will be in `re-analysis/jobs/status/B-M6b-3.md`);
  - the freeplay acceptance run (steps in `re-analysis/jobs/status/B-M15.md`);
  - the control-check run with `--allow-drive` ("Waiting on the operator" below).
- **After round 3:**
  - the hardware runs that settle the HARDWARE_ONLY records;
  - then each layer's acceptance (review state ACCEPTED).

## Earlier (2026-09-23..09-28; history, superseded by "Now" above)

- **Parallel jobs (2026-09-27). This replaces the single-manager Next list below.**
  - The work is split into three unattended chains on `re-analysis/jobs/BOARD.md`, one per opencode window, each in its own clone:
    - window 1: M8, M7, M9, M15;
    - window 2: the exact IMDCT and closing M6;
    - window 3: M11, M12, M13, M14.
  - Each chain runs extraction (X), integration (I: the approved inventory) and build (B) jobs in order, with `scripts/run-job.ps1`.
  - Job progress lives in `re-analysis/jobs/status/`. Jobs don't edit this file.
  - The push gate is `scripts/hooks/pre-push`; enable it per clone with `git config core.hooksPath scripts/hooks`.
  - The Ghidra decompilation of the engine is in `re-analysis/decomp/libcozmoEngine/` (gitignored; regenerate with `re-analysis/tools/ghidra/decompile.ps1`). It is a navigation aid, not evidence.
  - `scripts/run-manager.ps1` and `scripts/run-extractions.ps1` are removed; `run-job.ps1` replaces both.
- **Main fixed (2026-09-27):** the unverified M11 ecvcs WIP (91c9d64) is reverted on main and kept on branch `m11-wip`. Its evidence (`re-analysis/evidence/m11/ecvcs-extractor.md`) stays on main. M11-018's missing effect is fixed. Full suite 1639/1639.
- **Next (in order; the manager takes the first open item):**
  1. [x] **Fixed (9e38c9a): the two M10 test failures.** Both were stale test oracles, not production defects; the code matches `re-analysis/inventory/M10-derived.md`. `CorrectionTests.TheObjectPositionUpdatedReactionFiresThroughTheManager` recorded a tag-0x44 observation with no current behaviour, which ObjectPositionUpdated drops (M10-003 gap2 4j), so it now establishes a current stand-in before the frame and keeps the real frame→world path. `VisionTests.TheRealLocatorFiresTheCubeMovedReactionOnlyWhenTheCubeIsOutOfSight` evaluated C6 (`running || IsRunnable`, M10-004) against the real behaviour, whose `SteppedBehavior.IsRunnable` needs the animation library the rig does not load; it now passes the established runnable stand-in while still asserting the bound behaviour got the target (CubeMoved STBI, M10-003 gap1 8). Verifier PASS; full suite 1361/1361.
  2. [x] **NV: recorded and (partly) built.** Correction **C2** added the NV storage records M3-025..M3-036 (four extractor passes plus pass 4b) and dropped M3-022's deferral to M11-011; M3-device re-approved (e2bb28e, then C2b). **Batch A built the NV wire core** (a91ee1c): tag/size tables, `Read()`/FIFO/-6, the command build and arm, the non-factory header and re-request, index·1024 reassembly, completion/sink/broadcast, 7 resends and the 5 s timeout, disconnect. M3-025..M3-031 and M3-035 settled EXACT_SOURCE. **Still IMPLEMENTATION_GAP, cross-layer:** M3-032 (the Running-state gate and Gate B need the engine state machine (M1) and the UpdateAllResults failure signal (M11); only Gate A exists), M3-033 (the 12 connection reads are queued by the components that own them — M15 progression/inventory, M11 face album, M12 backup, lab, M7 needs — none built), M3-034 (those reads' callbacks, same layers). Build these with those layers, not before. M3-036 is HARDWARE_ONLY (the robot's reply to Length 1), for the next control-check.
  3. [x] **`m6-wip` verified and merged (7e9676c), branch deleted.** The verifier FAILed the branch first on two blocking source-fidelity defects: the Sound source-plug-in branch only accepted `plugin & 0x0F == 2` (gapA 2.4 needs 2 or 5), and `TheNameHashIsBoundedToTheNativeCopy` asserted the native-undefined `strlen ≥ 0x103` case; both fixed (0bc7aa8), re-verified PASS. `fidelity.py --check` clean; full suite on the merged main 1424/1424. M6-STATUS §7's STMG layout was formalised: the extractor corrected it (a non-zero trailing count is **read**, 56-byte A / 40-byte B bodies, not refused) and it became **M6-019** (EXACT_SOURCE) plus **M6-020** (RECOVERABLE_GAP, not live) and the **M6-021** seed seam (COMPATIBILITY_POLICY) — correction **C1**, M6 re-approved (45bcca5). M6-001's "read exactly" overclaim and M6-005's bound wording were corrected, and M6-006/007/018 locations/tests fixed. **Still open from the verifier (non-blocking/visibility):** M6-006 is a partial, unwired WIP (switch containers, PlayInternal/fade-in, the callback-registration failure unbuilt; the delay-table default and playing-id start are substitutions; the negative-delay clamp and the `0x9ABEA4` inference are unexercised) — do not settle it; M6-006/007/009 and `EvaluateScaled` are unwired, so they stay IMPLEMENTATION_GAP for item 4.
  4. [ ] **Continue M6** from `re-analysis/M6-STATUS.md` and `re-analysis/workplans/M6-plan.md`. Progress: **M6-009's value store** (`d2ec5cf`), **M6-008 continuous containers** (`04c1b19`), **M6-003 IMA ADPCM** (`9c55ab4`, settled EXACT_SOURCE with the stereo media gate) and **M6-004 CAkResampler** (`4173cc0`), **M6-011 voice filter A LPF/HPF** (`c0b87a3`), **M6-012 mixer** (`6b479c4`) and **M6-010 gain** (`777de36`), **M6-014 bus/Hijack lifetime** (`4afd8da`), **M6-015 Hijack** (`acca8da`), **M6-016 robot-audio path** (`2661e91`) and **M6-013 EQ/limiter** (`4e24da3`), plus **M6-017 frame model** (`a209a88`) and the **M6-002 Vorbis** partial + C5/C6/C7 arithmetic (`041e21a`, `a3696f7`, `ce0fa48`, `48f8b51`) are built, each verifier-PASS and full-suite green; corrections C2–C4 recorded. Batches 3 and 4 are complete; next is **M6-017** (the runtime skeleton; depends on batches 3–4), then batch 5, then the final wiring pass. Do not wire the runtime into `WwisePlayback`/`WwiseAudioSource`/`WwiseSongRenderer`/`AnimationScheduler` until the live voice/mixer (batches 3–4) exists.
  5. [ ] **A robot run with `--allow-drive`,** so ANIM (the keep-alive face stream) and DRIVE are exercised. The manager writes the setup steps; the operator runs it.
  6. [ ] **The remaining layers, in the table's order:** M11 vision, M12, M13, M14, M8, M7, M9, M15. Each gets the full cycle. `archive/m11-research-package` holds unreviewed M11 prep docs as leads.
- **Unattended running (2026-09-27, operator: everything in scope, minimal supervision).**
  - `scripts/run-manager.ps1` runs the manager in rounds and restarts it whenever it ends a reply. It stops on NEED_OPERATOR or DONE (read from `.scratch/manager-status.txt`), after `-MaxRounds`, after `-MaxIdleRounds` rounds with no commit, or when a `STOP` file exists at the repo root. Logs are in `.scratch/runner/`.
  - `scripts/run-extractions.ps1` extracts the upcoming layers ahead of the manager. It writes one report per layer to `re-analysis/research/<date>-<subsystem>-extraction.md`, runs alongside the manager, and skips layers already reported.
  - The spending cap is the OpenRouter key's credit limit.
  - The manager's standing decisions SD1–SD4 are in `.opencode/agent/cozmo-manager.md`. Under SD1 (operator, 2026-09-27: "exact. always exact") the **Vorbis IMDCT (M6 C8) is decided: exact.** Transliterate the NEON kernel and vendor its table. EQUIVALENT_IMPLEMENTATION is allowed only for code that doesn't ship (the phone's system libraries). Batches are one per layer, wired into the live path.
- **Tooling (2026-09-26):**
  - The manager runs in opencode (`.opencode/agent/cozmo-manager.md`) with the three role subagents; Codex does research in `re-analysis/research/`.
  - `AssetPresenceTests` fails when the OBB or the marker library is missing. It was added after that absence hid two M10 failures; set `COZMO_TESTS_WITHOUT_ASSETS=1` only for a run that knowingly has no assets.
  - `HANDOFF.md` is removed; this file carries the state.
- **Test runs (2026-09-26).** The full suite takes about 3 minutes, down from 10–12. Idle tests stepped simulated time, but CozmoDisplay paced each face with a real 33 ms sleep; they now use `SimulatedFacePacing`. The four whole-library sweeps are tagged `Category=Exhaustive`.
  - While working, run the focused tests or `dotnet test cozmo-stack/Cozmo.sln --filter "Category!=Exhaustive"` (about 2 minutes).
  - The full suite, Exhaustive included, still runs once before every commit.
  - **Known failures on main since b85decd (M10):** `CorrectionTests.TheObjectPositionUpdatedReactionFiresThroughTheManager` and `VisionTests.TheRealLocatorFiresTheCubeMovedReactionOnlyWhenTheCubeIsOutOfSight`. Both return early when the marker library or the OBB is missing, so they were not running where M10 was committed. They are open M10 defects.
- **Current state (2026-09-26, after the branch cleanup).**
  - **Branches:** `main` is the only line of verified work. Unverified work lives on one named branch at a time and is merged only after the verify step. Unmerged older material is kept as `archive/*` tags, not branches.
  - **Merged on main since the pause:**
    - M10 repair: b85decd. It has 2 failing tests; see "Test runs" above.
    - M3 NV calibration read: 2f01813, 1d2ee2c.
    - control-check DISCONNECT diagnostics: 6f050a3.
    - the M6 plan: `re-analysis/workplans/M6-plan.md`.
    - the test-runtime fix: 22cf9bc.
    - CONTROL bundles 20260925-165740, -165815 and -173447. The -180557 bundle was never committed and is still on the other PC.
  - **The NV fix is incomplete, but its records claim it is done.** M3-022 is EXACT_SOURCE, yet the engine's startup NV queue, the timeout, the retries and the Robot::Update dispatch gating are not built. They are listed only in `NvStorage.cs`'s summary, not as manifest records. So ready-to-stream is set much earlier than in the engine. The evidence is in the NV gap passes. The pass-1 and pass-2 texts are reproduced in the manager's scratchpad and summarised in the NV bullet further down. The fix is an M3 inventory correction with NV records, then the build.
  - **M6 (Wwise), unverified:** branch `m6-wip` holds the 5 M6 commits made on the other PC (7842392 batch 1, 3827137 M6-009 part, 8b64904 M6-007, and two status commits); `re-analysis/M6-STATUS.md` on that branch is its resume record. The M6-006 files (`WwiseAction.cs`, `WwiseEventRuntime.cs`, `WwiseEventRuntimeTests.cs`) exist only in the other PC's working tree and on its local branch `work/m6-006-unverified`. No verifier pass is on record. M6-007 adds an RNG-seed policy with no manifest record, and an STMG layout was recovered outside the frozen inventory and must go back through extraction.
  - **Archived as tags:**
    - `archive/codex-hardware-fixes`: 09-22 Codex WIP, older than the M1 rework;
    - `archive/m11-research-package`: M11 prep docs, unreviewed; its INVENTORY.md is not the frozen inventory;
    - `archive/test-runtime-audit`: the audit behind 22cf9bc.
  - **Not done:** the M11 extraction (never finished) and NV gap pass 3.
- **Phase:** M1 is closed apart from named residuals.
  - Batch 3 (the app layer) is 518b730, and the settle pass is 67d4bc2.
  - The device reset on RemoveRobot (M1-025, M1-015) is committed after the commits below. Its first verification failed on two races and on the calibration being kept. Those were fixed, and the re-verify passed.
  - M1 records: 28 EXACT, 1 EQUIVALENT, 3 IMPL_GAP, 9 POLICY, 2 HARDWARE_ONLY.
  - control-check is on main as 4b89c79 plus abea3ce. Its review returned NOT READY: the STATE rate window, ANIM driving without `--allow-drive`, and the OBB preflight. All of these are fixed.
  - The full suite passed 1163/1163 before the commit.
- **Waiting on the operator:**
  - **One control-check robot run with `--allow-drive`** (Next item 5). Purpose: exercise **ANIM** (the keep-alive face stream) and **DRIVE** with the drive gate open, and answer **M3-036** (does the robot answer a factory NV read with Length = 1?). Steps:
    1. Cozmo **on the floor**, off the charger, about 10 cm clear in front and behind, one cube powered within about 30 cm. PC on the robot's Wi-Fi.
    2. On the Cozmo machine, from `cozmo-stack`: `git pull`, then
       `dotnet run --project src/Cozmo.Conformance -- control-check 172.31.1.1 --obb "<the unpacked OBB dir>" --allow-drive`.
    3. Copy the printed `re-analysis/acceptance/hardware/<stamp>-CONTROL/` bundle to Downloads and tell the manager.
    - What it answers: ANIM keep-alive/neutral behaviour (`keepAliveStream`, `neutralReplay`), the DRIVE tracks with `--allow-drive`, and the M3-036 reply shape. The manager judges the bundle and decides what goes into git.
- **M3 device: repaired in one batch (2026-09-24).**
  - **Verification:** the verifier ran three passes. The first found 4 blocking items (the trailing silence, circular tests, cancel dropping the buffer, the face-hold interval). The second found 2 more (the live-stream cancel drop and the M5-023 text); the third, on the R1 fix, failed only on its test oracle. All were fixed.
  - **M3 records:** 12 EXACT_SOURCE, 2 forced COMPATIBILITY_POLICY (M3-019, M3-020), 2 HARDWARE_ONLY, and 8 IMPLEMENTATION_GAP, each with a named residual. Most residuals belong to M5.
  - **What changed:**
    - the engine's audio/animation feed (14-frame and byte budgets, FIFO drain, engine-tick driven);
    - the face encoder on the 64×128 canvas;
    - chunks ignored before SyncTimeAck; 3 frames per tick to vision;
    - at connection: SetCameraParams, the NV calibration read that enables vision, and DefaultCameraParams handling.
  - Full suite 1243/1243.
- **M4 control: repaired in one batch (2026-09-25).**
  - **Verification:** three verifier passes, and every blocking item was fixed. The verifier's disassembly also added cited corrections C1..C9, re-approved.
  - **Settled EXACT_SOURCE:** M4-001, 002, 007, 014, 015, 020, 022, 023, plus M2-015 and M1-041. The rest are IMPLEMENTATION_GAP, each with a named residual; most residuals belong to M11 or M12.
  - **What changed:**
    - At SyncTime, AbsoluteLocalizationUpdate {0, frame 0, origin 1}.
    - RobotState acceptance: the pre-origin part is applied for every synced state; the pose and history only for origin 1.
    - Direct-drive track locks (0x9D/0x9E).
    - StopAll sends only 0x3B.
    - Backpack lights: Off every tick, plus the charging configs.
    - Cube WakeUp lights on connect and reconnect.
    - The cliff threshold schedule, the charger platform, PotentialCliff.
    - Tap filtering.
    - Head/lift completion as the engine's actions: no send when in position, success means in position and stopped.
    - App head/lift speeds of 10/20.
  - Full suite 1279/1279.
- **M5 animation: repaired in one batch (2026-09-25).**
  - **Verification:** four verifier passes; every blocking item was fixed (B1..B9, then O1..O3). The verifier's disassembly added cited corrections C1..C4 and Appendix E, re-approved under the standing authorisation.
  - **M5 records:** 18 EXACT_SOURCE, 1 EQUIVALENT_IMPLEMENTATION (M5-003, .NET MathF against bionic libm in the last ULP), 1 COMPATIBILITY_POLICY (M5-020), 1 HARDWARE_ONLY (M5-036), and 15 IMPLEMENTATION_GAP, each with a named residual (mostly M6 audio, M7 mood/degree and M8 behaviour interfaces).
  - **What changed:**
    - the engine's AnimationStreamer port: streaming/idle/live selection, the single idle/live tail (InitStream with tag 255), the keep-alive face stream (a FaceImage every 33 ms, one Start, no End), the neutral-face replay after a cancel, AbortAnimation 0x8D;
    - track layers (face, eye, backpack, audio) with GetFaceHelper, Interpolate, Clip and Combine;
    - the ProceduralFace default and SetFromFlatBuf rules; the renderer on the engine's OpenCV 3.1.0 paths (OpenCv310.cs);
    - the ScanlineDistorter (mt19937 seeded 1), entropy-seeded mt19937 elsewhere (EngineRandom.cs);
    - JSON clips (JsonClipLoader.cs).
  - Full suite 1357/1357.
- **M6 (Wwise) and M10 (derived state): inventories frozen (2026-09-25).** M6 has 18 records (`re-analysis/inventory/M6-wwise-bank.md`, eight extractor passes) and M10 has 13 (`re-analysis/inventory/M10-derived.md`, three passes). All are IMPLEMENTATION_GAP until the compare-and-repair batch. M6's repair may run as two batches: the control and signal path, then the Vorbis port.
- **NV calibration read (found on the second CONTROL run):** the engine sends Length = 1 for the factory tag 0x80000001 (`_maxFactoryEntrySizeTable`), not 1024, and reassembles each reply at index x 1024. M11-011 is contradicted. Two gap passes then found the read is not alone. At connection the engine queues **12 NV reads before the calibration read** (unlocks, inventory, the face album, and 8 backup reads from backup_config.json), then Lab and Needs after it. Ready-to-stream (+0x2A, M1-041 CD20) waits until the whole queue drains. So the stack needs the engine's NV storage component, not just a calibration reader. Other findings:
  - NV dispatch is gated in Robot::Update (Running, the first full state; after a calibration, a BlockWorld failure).
  - robot+0x24 is mfgId word 1, the body hardware version. The operator's robot reports 4, so the engine zeroes all 8 distortion coefficients.
  - DefaultCameraParams never installs a calibration.
  - A read gets no callback on disconnect.
  - A third gap pass on the non-factory read path and the 14 other reads' callbacks is running. After it, the NV records go into M3 as correction C2 (the NV component is device storage). The M2 word@4 "index" rename is a naming cleanup, queued.
- **M2 protocol: repaired in one batch (2026-09-24).**
  - The verifier compared all 42 outbound and all 56 inbound layouts by machine against the engine; all match. Its one blocking item was a manifest overclaim, fixed by policy M2-017 and corrections C1 and C2, then re-approved.
  - M2 records: 15 EXACT_SOURCE, 1 COMPATIBILITY_POLICY (M2-017), 2 IMPLEMENTATION_GAP (M2-002 per-bit storage and M2-015 caller defaults, both M4 interfaces).
  - M1-027 is settled EXACT_SOURCE, so M1 has two IMPLEMENTATION_GAPs left: 029 and 041. It is `re-analysis/inventory/M2-protocol.md`.
  - **Extraction:** three read-only passes (OUT, 42 messages; IN, 56 codecs plus dispatch; the ImageChunk gap).
  - **Records:** M2-001..M2-016. All are IMPLEMENTATION_GAP until the comparison against the code. The seven earlier EXACT_SOURCE claims rested on weak evidence. No RECOVERABLE_GAP remains.
  - **Two contradictions of the current code, confirmed by the manager in the disassembly:**
    - M2-013: the FallingStopped codec reads the timestamp as the duration.
    - M2-014: BlockStatus 1 and 2 are swapped in Docking.cs.
  - The inbound dispatch also settles M1-027's residual interface (M2-010).
  - The comparison and repair run as one batch (operator: "do not expand M2 into another M1-style process unless the comparison shows that it is substantially wrong").
- **Operator decisions:** D1-D4 approved 2026-09-23. On 2026-09-24: the inventory, D5, D6 (fatal behaviour recorded, isolation kept as policy), D7, the corrections C1..C5 and D8 (stop processing a frame after a DisconnectRequest, M1-038) approved; the three repair batches are authorised to run without further checkpoints.
- **Batch 1 done (7aba301). Batch 2a done** (e6f2ed7; receive path: B17 truncation, receive-error counters, partial-header overrun after the header, M1-038); records it repaired are settled in one verified pass at the end of batch 2. **Batch 2b-i done** (8c9f405; clock, construction-time 2 ms scheduler + FIFO executor, posted sends, RobotLink isolation; three verification passes). **Batch 2b-ii done** (0691429; per-address connections, type-3 and timeout delete only that connection, timed-out flag, inbound creation, FinishConnection, posted Start/Stop, per-connection multipart, unconditional Dispose disconnect; two verification passes). **Batch 2c done** (socket B10/B11/B12/B16/B38 with C1's 47817 reopen, the M1-023 reset mechanism, the M1-037 host trigger, M1-001 addressing; verifier PASS on the second pass). Batch 2 complete.
- **Next: the operator's second control-check run (ready 2026-09-25, main 7297809: M2 + M3 + M4 + the control-check update).**
  1. In the Claude session: `! git push origin main`.
  2. On the Cozmo machine, from `cozmo-stack`: `git pull`, then `dotnet run --project src/Cozmo.Conformance -- control-check 172.31.1.1 --obb "<the unpacked OBB dir>" --allow-drive`.
  3. Setup:
     - Cozmo **on the floor**, not a table: after a PotentialCliff the engine turns stop-on-cliff off (M4-019 SC7);
     - off the charger, with about 10 cm clear in front and behind;
     - one cube powered and within about 30 cm.
  4. Copy the `re-analysis/acceptance/hardware/<stamp>-CONTROL/` folder to Downloads.
  5. **What this run answers:**
     - whether the robot echoes origin 1 (M4-021);
     - whether DefaultCameraParams arrives after SetCameraParams{0,0,true} (M3-019/021), and the image brightness;
     - whether cube telemetry arrives after the WakeUp lights (M4-024);
     - the new stream budget with FACE/AUDIO/ANIM;
     - the track locks and the backpack light rate.
  6. M5 (animation) is not in this run; its batch is in progress.

- **Standing authorisations and current plan (operator, 2026-09-24, supersedes the governing plan below where they differ):**
  - **Pushing:** the manager may push to GitHub `origin main` whenever a robot run is ready, so the operator's Cozmo machine can pull it.
  - **Inventories (operator, 2026-09-24, M2):**
    - Source-derived inventories and ordinary source-fidelity decisions need no operator approval. The manager decides them, documents them, freezes the inventory and proceeds.
    - Only two things go to the operator: a deliberate divergence from the engine (a policy), and a source question that is still unresolved and materially affects robot behaviour.
    - No M1-style multi-round process unless a comparison shows a layer is substantially wrong.
  - **Inventory corrections:** the manager may approve them without an operator checkpoint, including running `--approve`. Each stays cited and is recorded here. Genuine policy choices still go to the operator.
  - **Hardware-first plan:**
    1. finish the M1 link check;
    2. convert the direct-control hardware checks to self-judging PASS/FAIL (connect, head/lift/drive, face, audio, animation, cube connection, camera, the animation-stream edge cases);
    3. the operator runs them once;
    4. the manager fixes the failures from the source, including batch 3, the handshake;
    5. one confirming run.
  - **Operator involvement:** two or three robot runs, no design questions. Time box about 2-3 working days to M1 closed and the direct-control basics passing on hardware; otherwise stop and report plainly.
  - **Scope decision (operator, 2026-09-24): every layer, M1 through M15, is in scope.** Nothing is skipped or deferred ("this all seems like its needed. so i don't think we can skip any of it"). Each layer gets the M2-style cycle: inventory, freeze, one comparison-and-repair batch, verify. It escalates to multi-round work only if the comparison shows the layer is substantially wrong.
- **Governing plan (operator, 2026-09-24), in this order:**
  1. Finish batch 2c: commit only if the verifier and the full suite pass; if either fails, fix only the 2c issue and re-verify. No scope growth.
  2. One bounded transport hardware test. Its only purpose is to verify the rewritten socket and connection lifetime on a real robot before batch 3. It is one scripted run that saves a self-contained bundle; the operator runs it. It is not an exploratory campaign.
     - **Operator:** with the PC on the robot's Wi-Fi, run from `cozmo-stack`: `dotnet run --project src/Cozmo.Conformance -- m1-link-check` (default robot 172.31.1.1:5551; about 50 s). Copy back the bundle folder it prints, `re-analysis/acceptance/hardware/<yyyyMMdd-HHmmss>-M1-LINK/`.
  3. One comprehensive extraction pass before batch 3, treated as a closure pass for batch 3's dependencies. It covers every parked MISSING item and every source fact batch 3 needs: the handshake, firmware check, pre-validation gating, idle timeout, robotError handling, disconnect reasons and results, the 60 ms engine tick, and the exact ordering and conditions of setup messages such as SyncTime and InitController.
  4. One operator re-approval checkpoint for the resulting inventory changes. It includes the approved M1-039 Windows UDP policy. No batch 3 code before approval. Anything unrecoverable is marked UNKNOWN or POLICY, never guessed.
  5. Settle the batch 2 records (and implement M1-039) in the same verified pass.
  6. Batch 3: implementer, read-only verifier, focused tests, full suite, commit. No stops between ordinary verifier/fix passes. Return to the operator only for:
     - source evidence that contradicts the approved inventory;
     - a genuinely new policy decision;
     - a fact that is still MISSING or UNKNOWN;
     - a material hardware issue.
  - **Hard boundary:** after batch 3, M1 is closed except for documented HARDWARE_ONLY and COMPATIBILITY_POLICY items. If recoverable or missing source work is still growing after the closure pass, stop and report it; do not open another extraction cycle.
- **Recorded policy: M1-042 (operator, 2026-09-24, option a).** The original's phone app (Unity) sends some post-connect game messages that make the engine talk to the robot. After a Success connection response, this stack sends the app's defaults itself: SetRobotVolume with the stored value (default 1.0), which leads to SetAudioVolume (CD27), and the block pool enabled, which leads to cube connection (CD28). The controller can change either. The idle timeouts (StartIdleTimeout / CancelIdleTimeout, CC11) are app-backgrounding behaviour and are not sent automatically. Other Unity post-connect flows (CD31) are replaced by this stack's API.
- **Recorded policy: M1-040 (operator, 2026-09-24).** Accept every robot firmware (older, newer, factory). The handshake's version check never refuses the robot. The robot's firmware version is logged on every connection and in every hardware bundle, with a warning whenever it is not 2381, the build the protocol was recovered from. Rationale: the operator runs firmware 2457, which the original would refuse as OutdatedApp; firmware issues get fixed later, as long as the issue and the firmware are known.
- **Recorded policy: M1-039.** On Windows, a UDP receive that fails with ConnectionReset (an ICMP port-unreachable response) is treated as no data for that receive attempt. It emits no warning and does not end the rest of the tick's drain; the drain continues, as the original, which never sees the error, would. Other socket errors keep their source-backed handling.

- **Process change (operator, 2026-09-24):** only behavioural or source-fidelity defects, circular tests, and races or deadlocks block a commit. Non-behavioural cleanup is queued under "Cleanup queue" while the checker passes and no status becomes misleading. After a behavioural fix, only the affected diff is re-verified. The full suite runs once, just before the commit. Batches are large, and batch 3 is one batch after the closure pass. See AGENTS.md, Process, step 4.

## Hardware run 20260925-061148-CONTROL (operator, 2026-09-25; main b4a9c9a: M2 + M3 + M4)

**Result: 12 of 12 PASS**, including CUBES (78 ObjectAccel in 3 s). The run was on firmware 2457.

**Observations that answer HARDWARE_ONLY and open questions:**
- **M4-021:** the robot echoes the origin. All 890 RobotStates after AbsoluteLocalizationUpdate {0, frame 0, origin 1} reported origin 1, with 0 origin-rejected warnings.
- **M4-024:** cube telemetry flows. After the connection, the WakeUp lights went out: SetCubeGamma 0x80, then CubeID ×3, then CubeLights. Then StreamObjectAccel gave 78 ObjectAccel, and one ObjectMoved arrived.
  - In the first run there were no lights and no telemetry. The difference supports hypothesis (b), that the missing engine messages were the cause.
  - Which message the robot needs has not been isolated. That would be a hardware experiment, and it is not planned.
- **M3-019 / M3-021:** DefaultCameraParams arrived 27 ms after the connection-time SetCameraParams {0, 0, true}:
  - fields: maxGain 3.984, gain 2.0, exposure 0..67, gamma table;
  - the engine then sent SetCameraSettings {2.0, 16, false};
  - in the first run, which had no SetCameraParams, none arrived.
  - Frame mean luminance is about 14.5/255, a dark room. There is no earlier baseline to compare against.
- **Track locks:** 0x9D {4} before each DriveWheels and 0x9E {4} at each stop, as M4-014 says. There was no PotentialCliff.
- **Backpack Off lights:** 16.7/s each for 0x03 and 0x11 (one per engine tick), with no link trouble.
- **ANIM_CANCEL:** no AbortAnimation 0x8D (the known M5-023 gap).

**Defect found: the NV calibration read.**
- **What happened:** the robot answered NVCommand read 0x80000001 with 9 NV_MORE parts (index field 5,6,7,0,3,2,1,4,15) plus a final NV_OKAY. The stack concatenated 248 bytes; the engine expects 56. It logged SizeMismatch, and no calibration was installed.
- **Status:** vision was enabled, as the engine does on every path, so CAMERA passed. Anything that needs the calibration (markers, M11) would not work.
- **Cause:** the stack's NV reader was never inventoried.
- **Next:** being traced from source (NVStorageComponent) under the hardware-failure workflow.

## Hardware run 20260924-202748-CONTROL (operator, 2026-09-24)

**Result: 11 of 12 PASS.**
- **Passed:** CONNECT, STATE (33.3 Hz), HEAD, LIFT, DRIVE, FACE, AUDIO, ANIM, ANIM_CANCEL, CAMERA (14.7 fps, 320x240 grey) and DISCONNECT.
- **Human verdicts:** the operator answered yes to FACE and AUDIO. They are in `operator-verdicts.json`, added to the bundle after the run.
- **The cube connected** (SetPropSlot, then ObjectConnectionState slot 0). So the outbound cube-connection path works on hardware.

**CUBES FAIL: after StreamObjectAccel no ObjectAccel arrived.** It was classified with the AGENTS.md hardware-failure workflow; the trace is in the session scratch `extract/cubeaccel/report.md`, rows S1..S19.
- **(a) Wrong bytes: ruled out.** The original sends exactly `08 00000000 01`: the object's activeID, which is its slot, sent reliably (0x00635556, 0x00635562).
- **(b) A real omission, now an M4 item:**
  - When a light cube connects, the original plays the "WakeUp" cube-light animation (0x00639D64, trigger 0x26). That sends CubeID 0x10, CubeLights 0x04 and possibly SetCubeGamma 0x0C (SetLights 0x0063A764..0x0063A800). The stack sends none of these.
  - The engine's own stream logic does not depend on them.
- **(c) The robot side is HARDWARE_ONLY and still open.**
  - After connecting, the robot forwarded no cube telemetry at all: no power level, moved, tapped or accel.
  - The cube link dropped once for about 0.5 s while driving.
  - Firmware is 2457. Its engine-to-robot hash differs from 2381's, and 2457 is not shipped.
- **Test-reference gap:** the CUBES check exercises M9-017 (CubeAccelComponent) but does not cite it. M9-017's claim of a robot-side effect is not source-backed. The fix goes into the next control-check update.
- **Next:**
  1. Implement the WakeUp-on-connect path in the M4 batch.
  2. Re-run CUBES. If accel then arrives, (b) is confirmed. If not, the cause is robot-side, and gets its own cube-only probe.

## Hard-boundary report (for the operator; 2026-09-24)

The operator's rule: if recoverable or missing source work is still growing after the closure pass, stop and report instead of opening another extraction cycle. Implementing batch 3 surfaced a few new, small source questions. No new extraction cycle has been opened for them. Their records stay IMPLEMENTATION_GAP, and none of them blocks a robot run.

**Update after the batch 3 verifier:** it answered CD3/CD5 (the catch-up skip adds n x period), B25 (pops until empty) and CD27 (vmul by 65535, vcvt.u32 saturating, low 16 bits) from the rows' own cited ranges. It also answered CD18's "success" (it is the AbsoluteLocalizationUpdate send result). Still open: the AbsoluteLocalizationUpdate frameId/originId values (robot+0x2B0; +0x294 then +0x14) as stack state, and G5.5.

**Where M1 stands after batch 3 (2026-09-24):** 26 EXACT_SOURCE, 1 EQUIVALENT_IMPLEMENTATION, 9 COMPATIBILITY_POLICY, 2 HARDWARE_ONLY, 0 RECOVERABLE_GAP, and 5 IMPLEMENTATION_GAP. Each of the five has one specific residual:
- **M1-025 and M1-015:** RemoveRobot leaves this stack's device objects alive, where the original builds a fresh Robot (CB33/CC27). This is buildable: a device-state reset, an interface to M3/M4.
- **M1-027:** robot-to-engine tags inside 0xB0..0xF5 with no codec here cannot be size-checked (CC35). Needs the M2 protocol layer.
- **M1-029:** jsoncpp's grammar leniency and asUInt of a non-number are not in the rows. They are recoverable from the .so's jsoncpp, but that would be a new extraction cycle.
- **M1-041:** AbsoluteLocalizationUpdate is not sent. Its frameId and originId are pose-frame state, which this stack gets with M11 (BlockWorld). RobotStateHistory::Clear has no owner here yet.

So M1 is closed except these five small, named residuals, the HARDWARE_ONLY and the COMPATIBILITY_POLICY records.

**Update: M1-025 and M1-015 are settled (2026-09-24).**
- **The change:** `CozmoRobot.ResetDevices` runs on `Engine.RobotRemoved`. It returns every M1 device object and the VisionSystem to their as-constructed state, and it ends an in-flight audio Play and an in-flight vision frame, with nothing sent or written after the removal.
- **Tests:** four `M1_025_M1_015_*` tests in EngineAppLayerTests.
- **Residuals outside M1**, named in both records' provenance:
  - The upper-layer Robot components are not reset: Carrying, DockingComponent, PathFollower, BehaviorManager, MoodManager, the Idle and Reactive behaviours, CubeMovedReactionStrategy, MapComponent → MemoryMap, and AIComponent → FreeplaySystem. These belong to M12-M15.
  - The connection-time CameraCalib NV read is not implemented (CD21, 0x006583BA; an M3 interface). The reset now drops a calibration the caller read.
  - Keeping `VisionSystem.Enabled` across a removal is UNKNOWN.
  - The 2 s bound on the vision wait is local; the source joins without a bound (0x0065257C).
- **M1 now has three IMPLEMENTATION_GAP records:** 027, 029 and 041.

**Genuinely new, recoverable from the .so, all small:**
- CD3/CD5: when the tick is 240 ms or more behind, does the whole-period skip add to or replace the +60 ms step?
- B25: does ProcessArrivedMessages work on a snapshot of the arrivals, or pop until empty?
- CD18: the AbsoluteLocalizationUpdate frameId (robot+0x2B0) and originId values. It is currently not sent.
- CD18: what counts as SendSyncTime "success" for the +0x520 stamp.
- CD27: how vol x 65535 converts to u16 (truncation assumed; 1.0 is exact).

**Not recoverable, or external:** G5.5, jsoncpp asUInt of a non-number (a library-semantics detail).

**Outside M1, recorded as inputs for later layers:** the GoToSleep sequence (M5), SetCameraParams (M3), the NV reads (NV subsystem), the block-pool Init timing (M4), and where BehaviorReactToOnCharger writes reason 4 (behaviour layer).

Some items the batch 3 implementer listed as MISSING are answered by frozen rows (for example CC23 and CD20). Those are fixed in the verify pass, not re-extracted.

## Decision notes (operator, 2026-09-24: "just make notes of stuff like this; test later and pick the best choice")

Small behaviour choices are noted here rather than put to the operator. Each keeps its current behaviour until a test settles it.

- **Process guard (manager, 2026-09-26, prompted by the NV correction).** AGENTS.md now carries two manager rules: a settled record owns its whole production path (an unrecorded part gets its own gap record before the claiming record is settled; no deferral to another record, a comment or prose), and a claim about an existing record is checked against the current manifest rather than recalled. Origin: M3-022 was EXACT_SOURCE while the NV wire had no records and the gap lived in `NvStorage.cs`'s summary, and the NV pass 4 report called M11-011 contradicted from an earlier revision of its text.

- **Motor stop on shutdown.** CozmoRobot.Dispose sends StopAllMotors and DriveWheels(0) before disconnecting. The original only sends the DisconnectRequest (B33, CC29). Kept for now as a probable safety behaviour. Test later: does the robot stop on its own when the link drops?

- **SyncTime stamp without AbsoluteLocalizationUpdate (obsolete since the M4 batch, 2026-09-25).** The stack now sends AbsoluteLocalizationUpdate {0, frame 0, origin 1} at SyncTime and stamps +0x520 only when that send succeeds, as the engine does (M1-041, M4-020).

- **Wwise mix rate (M6, 2026-09-24).** In the original the Wwise mix rate is the phone's native output rate, capped at 48000: min(AudioTrack.getNativeOutputSampleRate, 48000), cached at 0x0108DF90 (0x00A56E80..0x00A56EA4). The Hijack plug-in then resamples to 22320 Hz by linear interpolation. This stack has no phone, so it uses 48000, which is what the cap gives on typical phones. This will be recorded as a COMPATIBILITY_POLICY in the M6 inventory. Revisit if a capture from an original phone ever shows another rate.
- **Wwise runtime found (M6 extraction, 2026-09-24).** The Wwise 2016.2 runtime is statically linked into libcozmoEngine.so, as symbol-less ARM code at 0x0095E540..0x00AE2E40. Anything earlier marked BLOCKED_EXTERNAL for "Wwise runtime semantics" is therefore RECOVERABLE_GAP, not blocked, and gets re-classified as each layer (M6, M9) is inventoried.

## Cleanup queue (non-behavioural; fold into the next batch)

- **From the M6-003 batch (2026-09-26):** `WwiseAdpcm` clamps the header step index to 0..88, which the source does not (unreachable on all 227 shipped ADPCM media); `WwiseAdpcm.Decode` refuses channels outside 1–2 where the runtime decoder is channel-generic (no shipped file has ≥3); the `WwiseAudioSource` comment still lists "seven stereo ADPCM undecodable"; `fmt+0x14` byte-1 semantics are UNKNOWN (byte 0 == nChannels in all 227 files, so moot). **Flake:** one full-suite run after the M6-003 change reported 1 failure of 1465 that did not reproduce in two subsequent runs; if it recurs, capture the test name.
- **From the NV batch A verifier (2026-09-26, non-blocking):** the stack's `NVStorageOpResult` record orders the fields `(tag, op, result, index)` where the native `BroadcastNVStorageOpResult` is `(tag, result, op, index)`; it is an internal event accessed by name, so no wire impact. The engine's `ReadSuccess`/`ReadEntryNotFound`/`ReadFailed` completion log lines are not emitted. Test coverage: `M3_028_TheRestIsReRequestedWithoutReArming` does not assert the "no re-arm"; `M3_026` does not exercise FIFO/one-in-flight; the timeout test uses a state clock only. When M3-032 is built, re-check the connection read's arm against the gating (a `RobotState` with clock > 5000 arriving before the reply would now time it out).
- **From the M10 test-oracle fix (2026-09-26):** `SteppedBehavior.IsRunnable` (`SteppedBehavior.cs:96-97`) gates on `context.Robot.Animations.Library is not null`, which the engine's `IBehavior::IsRunnableBase` (0x5BD778) does not. It is inert on the live path (the library is loaded in production), it is not claimed by any M10 record, and MD2 defers "the behaviours' IsRunnable" to M8. Decide whether to record or remove it when M8 is inventoried.
- The earlier queue was cleared by batch 4(i): the `using` isolation, TransportConstants LF, T_k2, stale MISSING and verifier-reading comments, and the M1-005 doc.
- LinkCheck (Cozmo.Conformance): its warning histogram matches "sendto failed" by prefix; the message is now "UDPTransport.SendFailed: sendto failed ...". Update the key after batch 3 merges; pass/fail is unaffected.
- From the device-reset re-verify (2026-09-24):
  - Label the 2 s vision removal wait as local, citing 0x0065257C.
  - Label the test's `Enabled`-kept assertion as policy, not a primary-source oracle.
  - Make the release in the frame-in-flight test deterministic; the 300 ms timer is a flake risk.
  - PetWorld has no lock (unreachable: OKAO pets are unavailable).
  - The implementer's note that "ReadCalibrationAsync has no caller" is wrong: six Conformance tools call it. What is missing is the connection-time read.
  - `_generation` in AnimationScheduler has no COMPATIBILITY record.
  - FakePort `Calls` is written without a lock.
- fidelity.py `--check` does not enforce FidelityManifestTests' rule that every non-EXACT record has an `effect`. Align the two.
- From the control-check review, after the run: pin the ANIM wheel figure (-75 mm/s for 264 ms) against the clip itself, and exercise the abort path.
- From the M2 batch verifier (2026-09-24, non-blocking):
  - MessageBase.Parse: the doc comment ("short body → RawRobotMessage") is stale and its catch blocks are dead.
  - PROTOCOL_STATUS.md and MessageCatalog label FallingStopped `hardware_refined`, but the fix is engine-derived.
  - gen_protocol.py `ReadStructArray` does not stop at the first failed element (D10). No inbound message has a counted struct array today.
  - CLAD strings are decoded as UTF-8, where the engine reads i8 chars. Nothing in Cozmo.Robot consumes them.
  - The PrintTrace format id uses the low 16 bits, which is not established. It is used for logs only.
- **For the M12 inventory (behavioural, found while reading consumers):**
  - Docking.cs:353 releases the carried object on BlockPlaced without the engine's success gate (R-P1).
  - Docking.cs:357 treats MovingLiftPostDock as `!= 0`, where the engine compares that byte for equality with IDockAction+0x80 (R-P4).
- For M4: the MessageExtras default arguments (SetHeadAngle 10/10, SetLiftHeight 3/20, RobotLink.cs:100) have no engine counterpart (M2-015). The only callers that rely on them are CozmoRobot.cs:499 (a public API) and Probe.cs:92.
- **For the M5 batch (found by the M3 verifier): done in the M5 batch (2026-09-25).** Kept for the record:
  - After a cancel (SetStreamingAnimation(null)), the engine replays the neutral face (A6, A31). The stack has no neutral replay.
  - StreamLive runs outside an Update, so its timing differs from the engine's (A29, 0x0057D080).
  - A Play then Stop inside one tick: in the engine the live stream continues rather than re-initialising.
  - RobotAnimationSink.Finished sends a BodyStop on cancel and can send one after EndOfAnimation. The stack also sends its own BodyStop before EndOfAnimation. None of these has an engine counterpart (A20, A24).
  - `_lastFace` is resent without the layered face flag (A16(9)).
  - Stream error paths: a failed send re-runs `AudioFramesSent++` and KeyframeFired on retry. A throwing send stays at the front of the buffer and faults every tick.
  - The AbortAnimation 0x8D send (M5-023).
  - The production test does not exercise the budget-stop/refill path.
- **control-check, before the next robot run:**
  - ANIM_CANCEL's "nothing after cancel within 100 ms" criterion and its M5-023 note contradict A25: a cancelled clip's pending frame can go out at the next tick that has budget. Rework the criterion or mark it not judged.
  - CUBES should cite M9-017 and M4-024.
  - CONNECT should record the origin and frame the robot reports (M4-021), and whether DefaultCameraParams 0xC8 arrives (M3-019).
  - CAMERA should record image brightness (M3-019).
- **From the M5 verifier (2026-09-25, non-blocking):**
  - **LiveUpdateFailed:** when UpdateLiveAnimation returns 1 the engine logs and returns (0x0057D086..0x0057D0E6); the code logs and carries on to the tail. Unreachable: an add fails only above 1000 keyframes.
  - **control-check keep-alive: done (2026-09-25).** ANIM's `streamingStops` became `clipStreamEnds`, and ANIM_CANCEL's leftover and End criteria now stop at KeepAliveQuietMs = 400 ms, before the keep-alive block's 0.5 s (A31). `abortAnimationSent` is now a criterion (exactly one 0x8D). The keep-alive stream and the neutral replay are recorded as the observations `keepAliveStream` and `neutralReplay`, and M5-036 is in the run's hardwareOnlyUncertainty.
- **From the M4 verifier (2026-09-25):**
  - **M10-007** is EXACT_SOURCE, but Apply has no production caller. The engine's CheckForUnexpectedMovement calls SetNewPose (0x0063E87E), and through F1..F3 that sends an AbsoluteLocalizationUpdate. Put this in M10-007's `unresolved` when M10 is inventoried; check the config flag kCreateUnexpectedMovementObstacles (0x00C7F620).
  - **SetBodyRadioMode order:** the stack sends it before SetOnCharger's threshold; the engine sends SetOnCharger's threshold (0x00512AB4) before SetBodyRadioMode (0x00512B46). This only matters if both fire in the same state.
  - **Charger-platform clearing:** without Robot::Update's clearing (M11 geometry), a robot that starts on the charger keeps the platform flag after it drives off. Until the flag clears, a PotentialCliff is ignored. The flag clears only on a committed off-treads change.
  - **B5:** callers of PlayLightAnim can still finish in a different order from the one they decided in. No caller off the engine thread exists today.
  - **VisionSystem Stopped path:** done (Q-c).
- Tests recommended by batch 4(i): one pinning the 14 Init tunables against the CA/B13 table (M1-005), and a dedicated R43 test for an unsent seq-0 entry at the front (M1-016).

## Parked: MISSING items (resolved)

Every item parked during batches 1-2c was settled by the closure pass of 2026-09-24. The rows are CA1..CA37, CB1..CB38, CC1..CC36, CD1..CD31 and E1..E9 in `re-analysis/inventory/M1-transport.md`, with corrections C6..C11. Two residuals are HARDWARE_ONLY: M1-033 (whether the robot accepts packed type 7/8/9 frames) and M1-043 (whether a 0-byte read warns). Inputs found for other layers (M3 camera params, M4 cliff threshold, body radio mode, lights, cube connection, the NV subsystem, the M5 sleep animation) are listed in the inventory's "Decisions" section.

## Layer order and review state

**Scope: FINAL (operator, 2026-09-24). Every layer is in scope, with the same cycle for each (see "Scope decision" above).** The order is bottom-up by dependency. M5's Wwise-driven audio is an interface to M6. Vision comes before manipulation and navigation, and the behaviour framework before the behaviours that run on it.

Review state per subsystem is in `re-analysis/fidelity_manifest.json`, and FIDELITY_GAPS.md renders it.

| order | subsystem | tier | review | notes |
| ---: | --- | --- | --- | --- |
| 1 | M1-transport | full | INVENTORY_APPROVED (2026-10-09 disposition correction) | MISSING/policy transfers cited and re-approved; finish remaining M1 build list and M1-033 hardware run |
| 2 | M2-protocol | full | INVENTORY_APPROVED (2026-10-09 disposition correction) | 17 records; re-approved with transferred P1-P4 ownership |
| 3 | M3-device (camera, display, audio device) | full | INVENTORY_APPROVED (repaired 2026-09-24) | colour camera format is HARDWARE_ONLY; FACE, AUDIO and CAMERA passed on hardware 2026-09-24 |
| 4 | M4-control (motion, sensors, lights, cubes) | full | INVENTORY_APPROVED (repaired 2026-09-25) | second CONTROL run 12/12 PASS, cube telemetry arrives; NV calibration read defect found (fix pending) |
| 5 | M5-animation | full | INVENTORY_APPROVED (2026-09-24, manager) | repaired 2026-09-25; 18 EXACT, 15 IMPL_GAP with residuals; keep-alive and neutral replay not yet on hardware |
| 6 | M6-wwise-bank | full | INVENTORY_APPROVED (2026-09-25, manager) | 18 records; the Wwise 2016.2 runtime is statically linked, so the whole audio path is primary source; MD1 mix-rate policy (M6-018) |
| 7 | M10-derived | full | INVENTORY_APPROVED (2026-09-25, manager) | 13 records; M10-006/007/008 contradicted by the source; MD1 forced policy (M10-013) |
| 8 | M11-vision | full | UNREVIEWED | |
| 9 | M12-manipulation | full | UNREVIEWED | M2-014 (BlockStatus) feeds it |
| 10 | M13-navigation | full | UNREVIEWED | |
| 11 | M14-faces | full | UNREVIEWED | the OKAO boundary |
| 12 | M8-framework | full | UNREVIEWED | |
| 13 | M7-behaviour | full | UNREVIEWED | |
| 14 | M9-wwise-music | full | UNREVIEWED | singing; BLOCKED_EXTERNAL Wwise runtime semantics |
| 15 | M15-freeplay | full | UNREVIEWED | |
| – | tools | – | UNREVIEWED | offline tooling |

## M1 candidate (uncommitted working tree)

- **What it is:** the M1 repair done with ChatGPT on top of `bcab556`, applied from `~/Downloads/m1-repair-review.patch` and then `m1-final-corrections.patch`. It touches 27 files: `ReliableTransport.cs` (the bulk of it), `ReliableConnection.cs`, `CozmoRobot.cs`, `Cozmo.Transport.csproj`, conformance tools and tests, plus the new `TransportRepairTests.cs`.
- **Reported results:** 48/48 focused tests and 999/999 overall. That report comes from the ChatGPT session and the manager has not re-run it. It has had no hardware acceptance.
- **Treatment:** a candidate implementation. The M1 inventory decides what survives. Nothing is discarded or accepted before then.
- The setup work for this process touches none of those 27 files.

## Hardware

- Only the operator's machine reaches a robot. The manager never plans a step that assumes otherwise.
- New runs go to `re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-<test-id>/` (AGENTS.md, "Hardware runs").
- **Existing tooling, not yet reviewed under this process:**
  - `hardware-test` in `Cozmo.Conformance` asks the operator for a verdict per check, where this process wants the result judged from a machine-readable bundle.
  - Earlier bundles are in `cozmo-stack/re-analysis/acceptance/` and `cozmo-stack/cozmo-acceptance-*.json`.
  - `HardwareCatalog.cs` maps checks to fidelity ids.
  - Each is to be reviewed alongside the layer it tests.

## Legacy documents

`re-analysis/HANDOFF.md`, `NEXT_MILESTONE.md`, the milestone documents and the claims of "complete offline" predate this process. They are notes, the lowest authority. Where they disagree with an approved inventory, the inventory wins.

The user-level agents in `~/.claude/agents/` (`cozmo-m1-transport-auditor`, `cozmo-m2-protocol-auditor`, `cozmo-proof-auditor`, `cozmo-adversarial-reviewer`) come from an earlier salvage audit and point at a stale scratch directory. The process uses the project agents in `.claude/agents/` instead.

## Accepted commits

| commit | subsystem | what it accepted |
| --- | --- | --- |
| 7aba301 | M1-transport | M1-004, M1-011, M1-012, M1-017 settled EXACT_SOURCE with evidence-derived tests; LINK check names M1-033. M1-003 and M1-008 tagged but held (DeleteConnection semantics; clock) |
| e6f2ed7, 8c9f405, 0691429, ed2729e | M1-transport | repair batches 2a, 2b-i, 2b-ii, 2c (receive path; clock/scheduler; connection lifetime; socket/addressing) |
| fef527c, 224accb | M1-transport | m1-link-check; closure pass re-approved; M1-LINK passed on hardware (20260924-112412-M1-LINK) |
| 3af197f | M1-transport | batch 4(i): transport completion, M1-039, 16 records settled |
| 518b730, 67d4bc2 | M1-transport | batch 3 app layer; settle to 26 EXACT_SOURCE |
| 4b89c79, abea3ce | tools | control-check self-judging hardware run, and its review fixes |
| (this commit) | M1-transport | device reset on RemoveRobot; M1-025, M1-015 settled EXACT_SOURCE |
| ce859bf | M1-transport | M1-024 decay bracket snapshot; M1-044/M1-015 warning log level; regression coverage |
| c8af57e | M1/M2 and transferred recipients | Cited manager/policy disposition; four new M1 gap records; re-approved 13 affected inventories |

## Open decisions for the operator

- **M6-002 Vorbis IMDCT (correction C8).** The engine's inverse MDCT is a float NEON transform whose per-lane arithmetic and table indexing are RECOVERABLE_GAP (ranges and the 615-float master table are named in `re-analysis/evidence/m6-vorbis/vorbis-imdct.md`). **Decided (operator, 2026-09-27): exact.** Transliterate the lane-level arithmetic and vendor the 2460 table bytes. The RECOVERABLE_GAP rows go back to the extractor. The kernel stays fail-closed until then.
