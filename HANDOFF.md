# Handoff (2026-09-25)

This file exists only so another coding agent can resume the interrupted work safely. The project record is still `PROJECT_STATE.md` (read its **PAUSED** note at the top), `AGENTS.md` (the process), the frozen inventories in `re-analysis/inventory/`, `re-analysis/fidelity_manifest.json`, and git history. Nothing here replaces them.

## 1. Git state when this was written

- **Branch:** `main`
- **HEAD:** `41b319c276bd38aca5b60a8a1963bf57a6b58923`
- **Against origin/main:** 0 behind, **4 ahead**. These commits are not pushed:
  - `41b319c` control-check: ANIM and ANIM_CANCEL judged up to the keep-alive block
  - `fee8724` M6 and M10 inventories approved and frozen
  - `cf313f8` M5 animation repaired in one batch; 18 records settled EXACT_SOURCE
  - `b4e6403` Second CONTROL hardware run (12/12 PASS) and its analysis
- **Working tree:** 17 modified files, no untracked files (`HANDOFF.md` itself aside), nothing staged.

```
 M PROJECT_STATE.md
 M cozmo-stack/src/Cozmo.Conformance/Reactions.cs
 M cozmo-stack/src/Cozmo.Robot/Behavior/BehaviorManager.cs
 M cozmo-stack/src/Cozmo.Robot/Behavior/Behaviors.cs
 M cozmo-stack/src/Cozmo.Robot/Behavior/CubeReactions.cs
 M cozmo-stack/src/Cozmo.Robot/Behavior/FaceBehaviors.cs
 M cozmo-stack/src/Cozmo.Robot/Behavior/FreeplayStack.cs
 M cozmo-stack/src/Cozmo.Robot/Behavior/NoPreDockPosesStrategy.cs
 M cozmo-stack/src/Cozmo.Robot/Behavior/ObjectBehaviors.cs
 M cozmo-stack/src/Cozmo.Robot/Behavior/OffTreadsBehaviors.cs
 M cozmo-stack/src/Cozmo.Robot/Behavior/ReactionStrategies.cs
 M cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs
 M cozmo-stack/src/Cozmo.Robot/Motion.cs
 M cozmo-stack/src/Cozmo.Robot/OffTreads.cs
 M cozmo-stack/src/Cozmo.Robot/Sensors.cs
 M cozmo-stack/src/Cozmo.Robot/UnexpectedMovement.cs
 M cozmo-stack/tests/Cozmo.Protocol.Tests/CorrectionsTests.cs
 (git diff --stat: 17 files, +1574 / -1057)
```

## 2. What each uncommitted change is

- **`PROJECT_STATE.md`** (manager edits; safe to commit on their own):
  - the PAUSED note;
  - the NV calibration-read findings: 12 NV reads queued before the calibration read; ready-to-stream waits for the queue to drain; robot+0x24 is the body hardware version (4 on the operator's robot); DefaultCameraParams never installs a calibration;
  - the control-check queue item marked done.
- **Every other file** is part of the interrupted M10 repair (section 3). It is an **UNVERIFIED CANDIDATE**. Do not commit it, reset it or tidy it before it has been inspected and verified.

## 3. The interrupted M10 repair

- **The task:** the M10-derived compare-and-repair batch (AGENTS.md Process, step 3). The worker compared the existing code with every row of the frozen inventory, keeping what the rows support and repairing or rebuilding what they don't. The batch also included two items from the PROJECT_STATE queue:
  - M10-007's Apply has no production caller;
  - the SetBodyRadioMode order: SetOnCharger's threshold (0x00512AB4) goes before SetBodyRadioMode (0x00512B46).
- **The frozen inventory:**
  - `re-analysis/inventory/M10-derived.md`, snapshot `M10-derived.approved.json`, frozen in `fee8724`;
  - records M10-001..M10-013 in `re-analysis/fidelity_manifest.json`, all still IMPLEMENTATION_GAP.
  - Policy M10-013 (raw IMU before the first state reads 0, MD1) is the only forced choice.
- **The edits made so far:** these are the files listed above. The worker added `// fidelity:` tags for the records below.
  - **M10-001, 005, 012, 013:** OffTreads.cs and Sensors.cs.
  - **M10-002, 006, 007:** UnexpectedMovement.cs and Sensors.cs.
  - **M10-003:** ReactionStrategies.cs, CubeReactions.cs, FaceBehaviors.cs, ObjectBehaviors.cs, NoPreDockPosesStrategy.cs and Behaviors.cs.
  - **M10-004, 007, 008:** BehaviorManager.cs.
  - **M10-010:** CozmoEngine.cs and Sensors.cs.
  - **M10-011:** Sensors.cs.
  - **Test and harness files:** small edits in Motion.cs, FreeplayStack.cs, OffTreadsBehaviors.cs, Conformance/Reactions.cs and CorrectionsTests.cs.
  - **The manifest was not touched:** no record's status, provenance or test field has been changed yet.
- **Where it stopped:** it was editing `DerivedStateTests.cs`, but that file has no changes. It had not yet created the planned `tests/Cozmo.Protocol.Tests/M10DerivedTests.cs`, whose expected values come from the inventory rows.
- **Verified so far:**
  - **Nothing.** No verifier pass, no focused tests, no full suite, and no `fidelity.py --check` after these edits.
  - The only check since the stop was a compile, which the manager ran while writing this file. It found the source projects compiling and **the test project failing to build.**
- **Known problems:**
  1. **The test project does not compile.** `tests/Cozmo.Protocol.Tests/DerivedStateTests.cs(813)` `FakeStrategy` no longer implements the reworked `IReactionTriggerStrategy`. It is missing:
     - `CanInterruptOtherTriggeredBehavior`, `CanInterruptSelf`;
     - `EnabledStateChanged(BehaviorContext, bool)`;
     - `Manager`, `ShouldResumeLast`;
     - `ShouldTriggerBehavior(ReactionContext, IBehavior)`.
  2. **The code has `MISSING:` comments** where the rows don't settle a behaviour. They must end up as `unresolved` text in the manifest, not as guesses:
     - OffTreads.cs: the Radians comparison operators (A5).
     - UnexpectedMovement.cs:
       - B8: whether the +1 paths add l and r to the sums;
       - B9: whether the same-sign decrement is guarded;
       - B15/B16: the AbsoluteLocalizationUpdate after SetNewPose and the obstacle are not performed. There is no ComputeStateAt in this stack.
     - BehaviorManager.cs:
       - C11: the manager+8/+0xC constructor values;
       - CompletelyUnlockAllTracks' body;
       - MoveLiftToHeightAction's defaults;
       - when the first-action gate can't be evaluated.
     - ReactionStrategies.cs:
       - C12: the RobotStopped broadcast;
       - C16: the stamp and gyro order;
       - an EnabledStateChanged that isn't in the rows;
       - the ChargerEvent broadcast.
     - FaceBehaviors.cs: InitReactedTo's body.
     - ObjectBehaviors.cs: 4d, the frame of the {80, 80, 80} test.
  3. **Not yet checked:** whether any other existing test's oracle changed because of these edits.
- **Scratch and evidence being used:** all in the session scratchpad `C:\Windows\TEMP\claude\C--Users-jbuschman-Downloads-com-anki-cozmo-3-4-0-1204-minAPI21-armeabi-v7a--nodpi--apkmirror-com-apk-Decompiler-com\b4cb80cd-acea-45cc-a212-7aecff92c2d6\scratchpad\`. It is a TEMP directory and may not survive; the frozen inventory's appendices carry the same extractor text.
  - `extract\M10\report.md`: the M10 pass, rows A/B/C;
  - `extract\M10-gap\report.md` and `report2.md`: gap passes 1 and 2;
  - `verify-m10\m10_manifest.py` and `m10_header.md`: how the records and the inventory were built;
  - `impl-m10\`: the worker's scratch, empty.
- **Before committing anything, the next agent must:**
  - make the test project compile;
  - add `M10DerivedTests.cs`, with oracles taken from the inventory rows, never from running the code;
  - update the M10 records in the manifest. Settle a record only where the code reproduces every row; the frozen fields can't change;
  - run `python re-analysis/tools/fidelity.py --write` and then `--check`;
  - run the focused tests;
  - get a read-only verifier pass (`.claude/agents/cozmo-verifier.md`) and fix every blocking finding;
  - then run the full suite once and commit.

## 4. Recent completed milestones

- **M5 animation, repaired and verified:** `cf313f8`. Full suite 1357/1357.
- **M6 and M10 inventories frozen:** `fee8724`.
- **Robot-run checks for the keep-alive face stream:** `41b319c`. The ANIM/ANIM_CANCEL criteria stop before the keep-alive block, and M5-036 is listed as hardware-only.

## 5. Other interrupted work, not part of the M10 resume

These are recorded in PROJECT_STATE's PAUSED note.

- **The NV calibration-read fix.** Its reports are in the scratchpad: `extract\NV\report.md`, `extract\NV-gap\report.md` and `extract\NV-gap2\report.md`. Gap pass 3 (`extract\NV-gap3\`, empty) was stopped before it reported.
- **The M11 extraction.** `extract\M11\` is empty; it was stopped before it reported.

## NEXT AGENT: START HERE

1. Check that the git state matches section 1: `git rev-parse HEAD` gives `41b319c…`, `git status --short` shows the same 17 files, and `main` is 4 ahead of origin.
2. Read the PAUSED note at the top of `PROJECT_STATE.md`, then the frozen `re-analysis/inventory/M10-derived.md`.
3. Inspect the uncommitted M10 edits (`git diff`) as an interrupted, unverified candidate. The known problems are in section 3.
4. Resume that exact repair, against the frozen M10 inventory and following section 3's steps. Do not reopen or re-audit M1–M9, and do not change scope.
5. Verify it (the verifier pass, `fidelity.py --check`, then the full suite once) before any commit.
