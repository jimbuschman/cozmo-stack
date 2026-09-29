# M8 framework inventory (behaviour lifecycle, scoring, choosers, whiteboard)

**State:** prepared by job I-M8 and frozen with `python re-analysis/tools/fidelity.py --approve M8-framework`. No forced policy is added by this pass; M8-004, M8-008, M8-009 and M8-010 are pre-existing policies and keep their status.

## Where this comes from

- **Source:** `libcozmoEngine.so` 3.4.0-1204 (`resources/lib/armeabi-v7a/libcozmoEngine.so`), plus the shipped OBB (`re-analysis/obb/assets/cozmo_resources/`). Unity C# and the shipped configs were used only as supporting evidence.
- **Read-only extractor passes:**
  - **X3 pass** (2026-09-27, Appendix A): rows 1..39 of `re-analysis/research/20260927-X3-M8-framework-extraction.md` — the production path, the existing-record review and N1..N9.
  - **X3 completion pass** (2026-09-28, Appendix B): rows 1..22 of `re-analysis/research/20260928-X3-M8-framework-completion-extraction.md` — the lifecycle bodies, the `Smart*` helpers, the strict-priority chooser, the activity dispatch, the repetition-penalty config and the running penalty.
  - **Gap pass 1** (Appendix C): A1..A7, B1..B6, C1..C5 — corrections to the rows the verifier failed, and the unread `IsRunnable`/`IsRunnableScored`, `SmartRemoveCustomLightPattern`, `SmartDelegateToHelper`, the four reaction-lock bodies and the `+0x104/+0x108/+0x110/+0x111` semantics.
  - **Gap pass 2** (Appendix D): Q1..Q3 — the `action+0x60 > 0x2dc6c0` tag guard, the `ReactionTrigger` ordinals and the `IBehavior.Init` warning count.
  - **Gap pass 3** (Appendix E): A1, C3, C4c and B5 — the four rows the verifier failed in gap pass 1.
- **Verification.** Four read-only verifier passes opened every status-changing row and a sample of the rest. The first two FAILed rows (2026-09-27 rows 5, 8, 24, 33 and label defects in 23, 32, 28; 2026-09-28 rows 1, 7, 18, 21 and the behavioural claims in 8 and 10); gap pass 1 corrected them. The gap pass 1 verifier FAILed A1, C3 and C4c; gap pass 3 corrected them. No failed row is kept.
- **Interfaces used and not redone:**
  - `M7-014`: the manager side of the reaction locks (`BehaviorManager::DisableReactionsWithLock` / `RemoveDisableReactionsLock`); the four bodies read in gap pass 1 are available to it.
  - `M10-003` / `M10-004`: the reaction-trigger strategies and `CheckReactionTriggerStrategies` (rows 32 and 33 of the X3 pass).
  - `M7` / `M15`: the concrete activity bodies and the per-class run logic (X3 row 28; gap pass 1 A7).
  - `M11` / `M12`: the whiteboard handler bodies and the beacon render.
  - `M1-024`: the engine-tick clock that `BaseStationTimer` exposes.
- **The C# was never evidence.**

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M8-001 | EXACT_SOURCE | The `IBehavior` lifecycle: constructor fields, `Init`, `Update`, `Stop`, `StopActing`, `Resume`, `IsRunnable`, `IsRunnableScored`. <br>- **Constructor:** behaviourID +0x3c, behaviourClass +0x64, needsActionID +0x68; +0x100 (flat) and +0x104 zeroed; +0x108 = 0; +0x10c = 0x29 (invalid objective); +0x110 = +0x111 = 1; graphs at +0xe8 and +0xf4; `ReadFromJson`, `ScoredConstructor`. <br>- **Init:** action-tag scan against 0x2dc6c0 (only engine tags > 0x2dc6c0; `sTagCounter` = 0x2dc6c1; the count is the main queue's current+queued); spark gate +0xd8; clears +0x98; halfword 0x0100 at +0xa0; +0x34 = now; +0xb0 = 0; `vtable+0x48` -> +0xa1/+0x80; `SparkBehaviorDisables` lock at +0x114. <br>- **Update:** `vtable+0xc` unless +0xa0 set and +0x84 zero, then returns 2. <br>- **Stop:** helper stop, +0xa1 = 0, `vtable+0x54`, +0x30 = now, `StopActing(0,0)`, then undo (a) disable-reaction locks while +0xac, (b) idle +0xb0, (c) motion profile +0xc0, (d) track-lock map +0xb4. <br>- **StopActing:** `vtable+0x80(0)`, helper-without-callback, `ActionList::Cancel` on +0x84. <br>- **Resume:** 0 = CliffDetected and 0x14 = UnexpectedMovement count against +0x114, and only from the second do they set +0x118 = now+15 and `TriggerEmotionEvent`; the normal path sets +0xa2, +0x34, calls `vtable+0x4c`, then +0xa1 and the Spark lock. <br>- **IsRunnable / IsRunnableScored / IsRunnableBase.** | X3 1 rows 2, 11, 15-18; X3 2 rows 1-8; gap1 B1/B2/B5/B6, C1a/C1b, C5c/C5d; gap2 Q1/Q2/Q3; gap3 A1/B5 |
| M8-002 | EXACT_SOURCE | The repetition-penalty graph and its suppression window. <br>- **Config:** `mood_config.json:88-99` `defaultRepetitionPenalty` nodes (0.0, 0.0) and (30.0, 1.0). <br>- **Read:** `repetitionPenalty` -> graph +0xe8; missing -> `AddNode(0, 1, true)`. <br>- **Consume:** `EvaluateRepetitionPenalty` 0x005beee6 returns 1 when +0x30 <= 0, else the graph at `now - +0x30`; +0x30 is stamped by `Stop`. <br>- **Suppress:** `StopWithoutImmediateRepetitionPenalty` sets +0x108 = now+1.0; the penalty is applied only when now >= +0x108. | X3 1 rows 21, 23; X3 2 row 21; gap1 B4, C5b |
| M8-003 | EXACT_SOURCE | Scored selection. <br>- **Five keys, not four:** `emotionScorers` -> +0xdc, `flatScore` -> +0x100, `repetitionPenalty` -> +0xe8, `considerThisHasRunForBehaviorObjective` -> +0x10c, **`runningPenalty` -> +0xf4**. <br>- `EvaluateScoreInternal` 0x005beec2: mood scorers if +0xdc non-empty, else the flat score. <br>- `EvaluateScore` 0x005bef60: running = internal + +0x104, times `EvaluateRunningPenalty` when +0x111; non-running applies the repetition penalty when +0x110 and now >= +0x108. <br>- `EvaluateRunningPenalty` 0x005bef22: +0x34 <= 0 -> 1.0, else +0xf4 at now - +0x34. <br>- **Field semantics:** +0x104 running bonus (`IncreaseScoreWhileActing` only while +0x84 set), +0x108 suppression, +0x110/+0x111 enables. | X3 1 rows 19, 20, 23, 24; X3 2 row 22; gap1 A2, A3, A5, C5a-C5d |
| M8-004 | COMPATIBILITY_POLICY | Behaviours built in code carry a score of their own; the engine's default is zero. The 1 and 5 are this stack's, named LOCAL_POLICY. | X3 1 row 15; existing evidence |
| M8-005 | EXACT_SOURCE | `PlayAnim` plays every trigger it lists, in order, `num_loops` times. A single-trigger vector is special-cased; otherwise `StartSequenceLoop` compounds the whole vector per loop. | X3 1 row 38 |
| M8-006 | EXACT_SOURCE | The wants-to-run strategy types and what each tests. Nine types; `InNeedsBracket` -> `IsNeedAtBracket(need, bracket)`; `ExpressNeedsTransition` -> `IsNeedAtBracket(need, Critical)` and not already expressing. | X3 1 row 39 |
| M8-007 | EXACT_SOURCE | An action's track mask is a lock, taken while it runs: `IActionRunner::Update` retries while `AreAnyTracksLocked`, else `LockTracks`; `UnlockTracks` releases; a multiset of owners. | X3 1 row 36 |
| M8-008 | COMPATIBILITY_POLICY | The head recalibration wait: `CalibrateMotorAction::CheckIfDone` waits with no timeout (IAction's +0x74 = -1); this stack keeps a five-second backstop. | X3 1 row 37 |
| M8-009 | COMPATIBILITY_POLICY | Behaviour scope undo order: the engine's `IBehavior::Stop` releases in a fixed order (reaction locks, idle, motion profile, track locks); this stack's `BehaviorScope` releases most-recent-first. The divergence is this stack's. | X3 2 row 6; gap1 B1/B5 |
| M8-010 | COMPATIBILITY_POLICY | Behaviour inventory classifier rules; bookkeeping, not on the live path. | existing evidence |
| M8-011 | IMPLEMENTATION_GAP | The eleven `Smart*` scope helpers. <br>- Idle: `SmartPushIdleAnimation` (fails if +0xb0 already set), `SmartRemoveIdleAnimation` (VERIFY-fails if +0xb0 clear). <br>- Motion profile: `SmartSetMotionProfile` (verify +0xc0 == 0), `SmartClearMotionProfile`. <br>- Tracks: `SmartLockTracks` (new key -> `LockTracks`, duplicate warns), `SmartUnLockTracks`. <br>- Lights: `SmartSetCustomLightPattern` (`PlayLightAnim`, appends +0xd0), `SmartRemoveCustomLightPattern` (`StopLightAnimAndResumePrevious`, erases). <br>- Helper: `SmartDelegateToHelper` (`DelegateToHelper`, weak ref +0xc4/+0xc8). <br>- Reaction locks: `SmartDisableReactionsWithLock` / `SmartRemoveDisableReactionsLock` (`"_behaviorLock"` suffix, per-behaviour set +0xa4); the manager side is M7-014. | X3 2 rows 9-18; gap1 B6, C2, C3, C4a, C4d; gap3 C3 |
| M8-012 | EXACT_SOURCE | `BehaviorManager::Update`'s tick order and switching. <br>- Not-initialised guard; activity tick (`vtable+0x20`); `EnsureRequestGameIsClear`; UI game request (class 0x2e -> +0x220); `CheckReactionTriggerStrategies`; the reaction gate (action list empty, latch +0x4c); scored choice (class 0x16); UI game fallback; `IBehavior::Update` return 0/2 -> `FinishCurrentBehavior`, 1 keeps. <br>- `SwitchToBehaviorBase`: stop, `IsRunnable`, `Init`, and on Init failure clear but still set resume info and send the DAS transition. <br>- `FinishCurrentBehavior`: immediate branch; default class-0x16 resume info. <br>- `SwitchToReactionTrigger`; `TryToResumeBehavior`. | X3 1 rows 2-14; gap1 A1, A4, A6; gap3 A1 |
| M8-013 | IMPLEMENTATION_GAP | The choosers and the activity selection dispatch. <br>- `ScoringBSRunnableChooser`: max score, running bonus, `RandDbl` tie-break, the two logs. <br>- `SelectionBSRunnableChooser`: +0x2c vs +0x34, countdown +0x3c, latch +0x40. <br>- `StrictPriorityBSRunnableChooser`: first running, else first `IsRunnable`, no scoring. <br>- `IActivity::GetDesiredActiveBehavior`: current, else `vtable+0x30`, then the interlude at +0x28 (`vtable+0x28`). <br>- `IActivity::GetDesiredActiveBehaviorInternal`: chooser +0x24, null warns, else `vtable+0x28`. <br>- The six concrete activities' addresses (bodies are M7/M15). | X3 1 rows 24-28; X3 2 rows 19, 20; gap1 A3, A7 |
| M8-014 | IMPLEMENTATION_GAP | `AIWhiteboard`: `Init` registers three external-interface handlers or warns; `Update` is a no-op; `AddBeacon` appends an `AIBeacon` to +0x60 and calls `UpdateBeaconRender`. | X3 1 rows 29-31 |

## Decisions (the manager's, recorded for audit)

- **SD1 (exact, always).** Every behaviour-changing step in this subsystem is reproduced from the shipped `.so` or the shipped OBB; no EQUIVALENT_IMPLEMENTATION is chosen anywhere in M8. The repetition-penalty graph is the shipped `mood_config.json` graph, and the `Smart*` helpers are transliterations of the named functions.
- **SD2 (behaviour the engine leaves undefined).** Not applied: this pass found no uninitialised-memory or undefined-value behaviour in M8. `IBehavior::Init`'s action-tag guard is fully defined (`+0x60` is always written; the guard's boundary is exact).
- **SD3 (work that depends on a layer not built yet).** The cross-layer parts of the M8 path stay IMPLEMENTATION_GAP naming the layer that owns them: the reaction strategies (`M10-003`/`M10-004`), the concrete activities and per-class run logic (`M7`/`M15`), the whiteboard handlers and beacon render (`M11`/`M12`), and the manager side of the reaction locks (`M7-014`).
- **SD4 (housekeeping).** The two X3 reports and the three gap reports are kept under `re-analysis/research/` and reproduced as appendices A..E; the manifest records and `FIDELITY_GAPS.md` are regenerated by `fidelity.py`.
- **MD1. M8-009 stays a policy.** The engine's fixed release order and this stack's most-recent-first release differ; the divergence is deliberate and is stated in the record, with the engine's order as evidence.
- **MD2. The reaction-trigger interface is not duplicated.** X3 rows 32 and 33 (`IReactionTriggerStrategy::ShouldTriggerBehavior`, `ReactionTriggerStrategyGeneric::ShouldTriggerBehaviorInternal`) are owned by `M10-004` / `M10-003` and are not given M8 records. `M8-012` cites the call site in `BehaviorManager::Update`.
- **MD3. `M8-014` is placed on the existing C# whiteboard.** `AIWhiteboard.cs` already exists under `Cozmo.Robot/Manipulation`; no other subsystem's record owns it.
- **MD4. Every M8 record starts as IMPLEMENTATION_GAP** because none of the pre-process `EXACT_SOURCE` claims had been checked against the whole production path. The four pre-existing COMPATIBILITY_POLICY records keep that status.

## Existing record evidence found contradicted or too weak

- **M8-001** (`EXACT_SOURCE`, "Behaviour lifecycle and the Smart* scope helpers"): its evidence was a single citation, `IBehavior::IsRunnableBase 0x005BD778`, which does not evidence `Init`, `Update`, `Stop`, `StopActing`, `Resume` or any `Smart*` helper. Split into **M8-001** (lifecycle, re-evidenced) and **M8-011** (helpers).
- **M8-002** (`EXACT_SOURCE`, "Repetition penalty graph"): its evidence was only the config path. The consumer addresses (`0x005beef6`, `0x005bef0e`), the `+0x30` stamp and the `+0x108` suppression window are added.
- **M8-003** (`EXACT_SOURCE`, scored selection): contradicted. Its text said `ReadFromScoredJson` "reads four things"; the engine reads a fifth key, `runningPenalty` -> graph at `+0xf4`, and `EvaluateRunningPenalty`. The `+0x104`/`+0x108`/`+0x110`/`+0x111` semantics were missing. Corrected.
- **M8-004** (COMPATIBILITY_POLICY): confirmed; the engine's zero default stands.
- **M8-005 / M8-006 / M8-007** (EXACT_SOURCE): the citations resolve to the named symbols, but the bodies were not re-read in this pass, so their status becomes IMPLEMENTATION_GAP pending the comparison. No contradiction found.
- **M8-008** (COMPATIBILITY_POLICY): confirmed.
- **M8-009** (COMPATIBILITY_POLICY): its evidence was empty and its authority said "the engine Smart* destructor order". There is no destructor; the order is `IBehavior::Stop 0x005bd08c`. Re-evidenced.
- **M8-010** (COMPATIBILITY_POLICY): bookkeeping, `live_path` false; unchanged.
- **`M7-014`** (out of this job's write scope, recorded for the integrator): its evidence is one line, "called in each BehaviorReactToX InitInternal". The four reaction-lock bodies read in gap pass 1 (Appendix C, C4a-d) give it real citations; `I-M7` should fold them in.
- **`M10-004`** (out of scope, recorded for the integrator): X3 row 33's `GetCurrentTimeStamp` citation was `0x0060f3f2` (`BaseStationTimer::getInstance`); the correct call is `0x0060f3f6`. The M10 inventory does not cite that address, so no M10 record is wrong, but the M10 report's row should be corrected if it is reused.
## Appendix A: X3 pass, extractor report

re-analysis/research/20260927-X3-M8-framework-extraction.md

﻿# Job X3 - M8-framework extraction

Agent: opencode-w1 (DeepSeek). Read-only extraction. Date: 2026-09-27.

## Scope and sources

The behaviour framework: `IBehavior` lifecycle and its `Smart*` scope helpers, `BehaviorManager`,
the behaviour choosers, the activity tree, the repetition penalty and the whiteboard, plus the
reaction-trigger interface to M10 (interfaces only).

Primary source is `resources/lib/armeabi-v7a/libcozmoEngine.so` (ELF, ARMv7/Thumb). Every citation
below is an address in that file with the instruction that matters. The Ghidra decompilation is not
present in this clone (`re-analysis/decomp/` is gitignored and not regenerated), so the disassembly
was produced directly with `re-analysis/tools/disarm.py` (capstone + lief). Dynamic symbols are
present and rich (33 000+ dynsyms, 3304 naming `Behavior*`), so functions are named.

**Missing artifact, affects this report:** the unpacked OBB is not present in this clone
(`re-analysis/obb/` is empty; no `.obb` file anywhere). Authority-3 items - the behaviour JSON,
`mood_config.json`, `reactionTrigger_behavior_map.json` - therefore cannot be read here. Records
that rest on those files are marked RECOVERABLE_GAP with the exact file to read.

## The production path

`BehaviorManager::Update` is the per-tick entry point. The row table lists every step that changes
behaviour, in the order the engine runs them.

| # | step | what the original does | citation | record | classification |
|---:|---|---|---|---|---|
| 1 | Manager constructed | `BehaviorManager::BehaviorManager(Robot&)` wires the manager to the robot; `IBehavior` derives from `IBSRunnable` (the manager holds `shared_ptr<IBehavior>` in a `BehaviorID` map). | `0x005a0865 push.w {r4,...}`; `0x005bbbca blx IBSRunnable::IBSRunnable` | NEW | EXACT_SOURCE |
| 2 | Update: not initialised | If the manager's init byte at `+0` is zero, logs `"BehaviorManager.Update.NotInitialized"` via `sErrorF` and returns without touching a behaviour. | `0x005a2f70 ldrb r0,[r4]`; `0x005a2f72 cbz`; `0x005a2fea adr "BehaviorManager.Update.NotInitialized"` | NEW | EXACT_SOURCE |
| 3 | Update: activity tick | Calls `GetCurrentActivity()` and then the activity's virtual `Update` (`vtable+0x20`). The activity is ticked before any reaction or behaviour. | `0x005a2f78 blx GetCurrentActivity`; `0x005a2f80 ldr r2,[r1,#0x20]`; `0x005a2f84 blx r2` | NEW | EXACT_SOURCE |
| 4 | Update: request-game clear | If `robot+0x355` is non-zero, or `manager+0x20 == 1`, calls `EnsureRequestGameIsClear()`. | `0x005a2f8e ldrb.w r0,[r5,#0x355]`; `0x005a2f94 ldrb r0,[r4,#0x20]`; `0x005a2f9e blx EnsureRequestGameIsClear` | NEW | EXACT_SOURCE |
| 5 | Update: UI game request | If `manager+0x38` is set, calls `SelectUIRequestGameBehavior()`, clears the flag, and if the previously current behaviour's class is `0x2e` sets byte `+0x220` on the object at `manager+0x30`. | `0x005a2fa2 ldrb r0,[r4,#0x38]`; `0x005a2fac blx SelectUIRequestGameBehavior`; `0x005a2fde cmp r7,#0x2e`; `0x005a305a strbeq.w r1,[r0,#0x220]` | NEW | EXACT_SOURCE |
| 6 | Update: reaction strategies | Calls `CheckReactionTriggerStrategies()`. A non-zero result suppresses the scored choice and the UI-request switch for this tick. | `0x005a3060 blx CheckReactionTriggerStrategies`; `0x005a3068 cbnz r5` | NEW | EXACT_SOURCE |
| 7 | Reaction check gate | The reaction list is only evaluated when the robot's action list (`robot+0x250`) is empty; the manager keeps a latch byte at `+0x4c` ORed with `!IsEmpty`, and returns false (no trigger) when the list is empty. | `0x005a3558 ldr r0,[r5,#4]`; `0x005a355a ldr.w r0,[r0,#0x250]`; `0x005a355e blx ActionList::IsEmpty`; `0x005a3566 eor r0,r0,#1`; `0x005a356c strb.w r0,[r5,#0x4c]` | NEW | EXACT_SOURCE |
| 8 | Update: scored choice | If no reaction fired, nothing occupies `manager+0x30`, and the current behaviour's class is `0x16`, calls `ChooseNextScoredBehaviorAndSwitch()`. | `0x005a306e ldrb r0,[r0,#0x10]`; `0x005a3072 cmp r0,#0x16`; `0x005a3078 blxeq ChooseNextScoredBehaviorAndSwitch` | M8-003 (related) | EXACT_SOURCE |
| 9 | Update: UI game fallback | If no reaction fired and `manager+0x30` holds a behaviour different from the current one, calls `SwitchToUIGameRequestBehavior()`. | `0x005a307e ldr r0,[r4,#0x30]`; `0x005a3082 ldr r1,[r4,#0x1c]`; `0x005a309a blx SwitchToUIGameRequestBehavior` | NEW | EXACT_SOURCE |
| 10 | Update: tick current behaviour | Calls `IBehavior::Update()`. Return `0` logs `"BehaviorManager.Update.FailedUpdate"`; return `2` logs a debug line; both then call `FinishCurrentBehavior(behavior, class != 0x16)`. Return `1` keeps it. | `0x005a30bc blx IBehavior::Update`; `0x005a30c0 cmp r0,#0`; `0x005a30c4 cmp r0,#2`; `0x005a3128 blx FinishCurrentBehavior`; `0x005a3148 "BehaviorManager.Update.FailedUpdate"` | NEW | EXACT_SOURCE |
| 11 | Switch to a behaviour | `SwitchToBehaviorBase`: stops the current behaviour, calls `IBehavior::IsRunnable(robot)` (a false result logs `"BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable"` through `sVerifyFailedReturnFalse`), then `IBehavior::Init()`. An Init failure logs `"BehaviorManager.SetCurrentBehavior.InitFailed"`, clears the behaviour, and still sets the running/resume info and sends the DAS transition. | `0x005a1e6a blx StopAndNullifyCurrentBehavior`; `0x005a1e76 blx IsRunnable`; `0x005a1e88 "BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable"`; `0x005a1e94 blx Init`; `0x005a1eae "BehaviorManager.SetCurrentBehavior.InitFailed"`; `0x005a1f12 SetRunningAndResumeInfo`; `0x005a1f1c SendDasTransitionMessage` | NEW | EXACT_SOURCE |
| 12 | Finish a behaviour | `FinishCurrentBehavior(behavior, immediate)`: when `immediate == 1`, tail-branches to `0x8cbc9c`; otherwise, if `manager+0x30` equals the finishing behaviour, calls `EnsureRequestGameIsClear()`, then switches to a default (class `0x16`) `BehaviorRunningAndResumeInfo`. | `0x005a38ca cmp r2,#1`; `0x005a38d6 b.w 0x8cbc9c`; `0x005a38e6 blxeq EnsureRequestGameIsClear`; `0x005a38f4 movs r0,#0x16`; `0x005a38fe blx SwitchToBehaviorBase` | NEW | EXACT_SOURCE |
| 13 | Reaction switch | `SwitchToReactionTrigger(strategy, behavior)` builds a resume info, calls the strategy's `ShouldTriggerBehavior` (`vtable+8`); only when it returns 1 does it switch. | `0x005a25e4`; `0x005a262a blx r1`; `0x005a262c cmp r0,#1`; `0x005a26da blx SwitchToBehaviorBase` | NEW | EXACT_SOURCE |
| 14 | Resume | `TryToResumeBehavior` bails unless the stored resume head angle differs from a constant and the robot's action list is empty; logs `"Resuming behavior and don't have an action, so setting head angle %f, lift height %f"`. | `0x005a2b48 vldr s0,[pc]`; `0x005a2b60 blx ActionList::IsEmpty`; `0x005a2b72 "Resuming behavior and don't have an action, so setting head angle %f, lift height %f"` | NEW | EXACT_SOURCE |
| 15 | IBehavior fields | Constructor stores behaviourID at `+0x3c`, behaviourClass at `+0x64`, needsActionID at `+0x68`; zeroes `+0x24/+0x28` (behaviour-obj), `+0x100` (flatScore) and `+0x104`; sets `+0x108 = 0`, `+0x10c = 0x29`, `+0x110 = 0x101`; constructs the repetition graph at `+0xe8` and the running-penalty graph at `+0xf4`; calls `ReadFromJson` and `ScoredConstructor`. | `0x005bbd28 strd r8,r8,[r4,#0x100]`; `0x005bbd2c strd r8,r0,[r4,#0x108]`; `0x005bbd30 movw r0,#0x101`; `0x005bbd12/0x005bbd22 blx GraphEvaluator2d::GraphEvaluator2d`; `0x005bbd40 blx ReadFromJson`; `0x005bbe32 blx ScoredConstructor` | M8-004 | EXACT_SOURCE |
| 16 | IBehavior::Init | Logs an info line on the `Behaviors` channel, then walks the robot's action list (`robot+0x250`) comparing an action field to `0x2dc6c0`. The exact field and purpose were not established in this pass. | `0x005bcb54`; `0x005bcb8c blx sChanneledInfoF`; `0x005bcbd0 movw r6,#0xc6c0`; `0x005bcbd4 movt r6,#0x2d` | NEW | RECOVERABLE_GAP - read `IBehavior::Init` `0x005bcb54..0x5bcd80` fully |
| 17 | IsRunnableBase | Running (`+0xa1` non-zero) returns true. Otherwise checks the AI information analyzer's process (`robot+0x264`), the robot state byte `+0x74` against 3 and against the state at `robot+0x264+0x30+0x14`, then the unlock id at `+0x70` (0x55 bypasses the unlock check) via `ProgressionUnlockComponent::IsUnlocked(robot+0x448, id, true)`, then the float timers at `+0x78`. | `0x005bd780 ldrb.w r0,[r4,#0xa1]`; `0x005bd7d8 ldr.w r0,[r5,#0x264]`; `0x005bd7f0 ldr r1,[r1,#0x30]`; `0x005bd806 cmp r1,#0x55`; `0x005bd810 blx IsUnlocked` | M8-001 | EXACT_SOURCE (this function only) |
| 18 | IsRunnable | `IBehavior::IsRunnable` is the public wrapper; `IsRunnableScored` at `0x005bda28` is the scored variant. | `0x005bd751`; `0x005bda29` | NEW | RECOVERABLE_GAP - read both fully |
| 19 | Score dispatch | `EvaluateScore`: while running (`+0xa1`), score = `EvaluateScoreInternal(robot)` + `+0x104`; if `+0x111` is set, multiplied by `EvaluateRunningPenalty()`. Otherwise only a behaviour that `IsRunnableBase` and passes the `vtable+0x50` test scores; the running-score branch uses `vtable+0x88` and `+0x110`/`+0x108`. | `0x005bef60`; `0x005bef7a blx r2`; `0x005bef80 vldr s2,[r4,#0x104]`; `0x005bef8c cbz r0`; `0x005bef90 blx EvaluateRunningPenalty`; `0x005befa2 blx IsRunnableBase`; `0x005befce blx r2`; `0x005befd4 ldrb.w r0,[r4,#0x110]` | M8-003 | EXACT_SOURCE |
| 20 | Score internal | `EvaluateScoreInternal`: if the mood-scorer vector at `+0xdc`/`+0xe0` is non-empty, tail-calls the mood scorer (`0x8cbf8c`, reached with `robot+0x440`); otherwise returns the flat score at `+0x100`. The two are alternatives, not addends. | `0x005beec2 ldr r2,[r0,#0xdc]!`; `0x005beec6 ldr r3,[r0,#4]`; `0x005beeca cmp`; `0x005beecc ldrne.w r1,[r1,#0x440]`; `0x005beed0 bne.w 0x8cbf8c`; `0x005beed4 vldr s0,[r0,#0x24]` | M8-003 | EXACT_SOURCE |
| 21 | Repetition penalty | `EvaluateRepetitionPenalty`: if `+0x30` (the last-run stamp) is <= 0 returns 1.0; else evaluates the graph at `+0xe8` at `now - +0x30` seconds. | `0x005beee6`; `0x005beeea vldr s0,[r4,#0x30]`; `0x005beef6 movle.w r0,#0x3f800000`; `0x005bef0e add.w r0,r4,#0xe8`; `0x005bef1e b.w 0x8cbf9c` | M8-002/M8-003 | EXACT_SOURCE |
| 22 | Running penalty | `EvaluateRunningPenalty`: if `+0x34` <= 0 returns 1.0; else evaluates the graph at `+0xf4` at `now - +0x34`. | `0x005bef22`; `0x005bef26 vldr s0,[r4,#0x34]`; `0x005bef4a add.w r0,r4,#0xf4`; `0x005bef5a b.w 0x8cbf9c` | NEW | EXACT_SOURCE |
| 23 | Scored JSON keys | `ReadFromScoredJson` reads `emotionScorers` -> `MoodScorer::ReadFromJson` at `+0xdc`, `flatScore` -> float at `+0x100`, `repetitionPenalty` -> graph at `+0xe8`, `considerThisHasRunForBehaviorObjective` -> `BehaviorObjectiveFromString` at `+0x10c`, and **`runningPenalty` -> graph at `+0xf4`**. A missing/failed `repetitionPenalty` warns; a graph left empty gets `AddNode(0, 1.0, true)`. | `0x005bc4b2 "emotionScorers"`; `0x005bc4ca "flatScore"`; `0x005bc4e0 str.w r0,[r4,#0x100]`; `0x005bc4ee "repetitionPenalty"`; `0x005bc55e AddNode(0,1,true)`; `0x005bc56a "considerThisHasRunForBehaviorObjective"`; `0x005bc5a4 BehaviorObjectiveFromString`; `0x005bc602 "runningPenalty"`; `0x005bc5f8 add.w r6,r4,#0xf4`; `0x005bc678 AddNode(0,1,true)` | M8-003 (partial) | EXACT_SOURCE |
| 24 | Scored chooser | `ScoringBSRunnableChooser::GetDesiredActiveBehavior` walks its behaviour map, calls `EvaluateScore(robot)` on each, skips scores <= 0, adds a running-duration graph bonus for a running behaviour and a `RandomGenerator::RandDbl` tie-break for a non-running one, and keeps the maximum. Logs `"behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f"` and warns `"Looks like more than one behavior returned IsRunning(). One of them is '%s'"`. | `0x0060a44a blx EvaluateScore`; `0x0060a452 vcmpe s0,#0`; `0x0060a474 blx GraphEvaluator2d::EvaluateY`; `0x0060a4a8 blx RandomGenerator::RandDbl`; `0x0060a5c2 ldr r3,[sp,#0x38]`; `0x0060a5f2 "behavior '%s' has score of %f..."`; `0x0060a550 "Looks like more than one behavior returned IsRunning()..."` | M8-003 | EXACT_SOURCE |
| 25 | Selection chooser | `SelectionBSRunnableChooser::GetDesiredActiveBehavior` chooses between the behaviour at `+0x2c` and the one at `+0x34`, gated by a countdown at `+0x3c` and a latch at `+0x40`; a running behaviour short-circuits the countdown. | `0x0060ad64`; `0x0060ad84 movs r6,#1`; `0x0060adbe subs r1,#1`; `0x0060adc0 str r1,[r5,#0x3c]`; `0x0060adc8 strb.w r0,[r5,#0x40]` | NEW | EXACT_SOURCE |
| 26 | Strict-priority chooser | `StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior` at `0x0060b23e`; not read in this pass. | `0x0060b23e` | NEW | RECOVERABLE_GAP - read it |
| 27 | Activity selection | `IActivity::GetDesiredActiveBehavior` returns the activity's current behaviour when it equals the caller's; otherwise calls the activity's `GetDesiredActiveBehaviorInternal` (`vtable+0x30`) and, when the activity holds an interlude behaviour at `+0x28` and the result differs, calls the interlude's method (`vtable+0x28`). Logs `"Activity %s is inserting interlude %s between behaviors %s and %s"`. | `0x005b387c`; `0x005b38c2 blx r4`; `0x005b38ee add r0,sp,#0x14`; `0x005b38f6 blx sl`; `0x005b399c "Activity %s is inserting interlude %s between behaviors %s and %s"` | NEW | EXACT_SOURCE |
| 28 | Activity dispatch table | `IActivity::GetDesiredActiveBehaviorInternal` at `0x005b3a6c` and the concrete activities (`ActivityFreeplay`, `ActivitySocialize`, `ActivitySparked`, `ActivityStrictPriority`, `ActivityBuildPyramid`, `ActivityFeeding`) each implement it; not read here. | `0x005b3a6d`, `0x005ae29d`, `0x005b023d`, `0x005b1d19`, `0x005b267d` | NEW | RECOVERABLE_GAP - read per activity |
| 29 | Whiteboard init | `AIWhiteboard::Init` registers three external-interface handlers when the robot has an external interface; otherwise warns `"Initialized whiteboard with no external interface. Will miss events."` | `0x0056a394`; `0x0056a39c blx HasExternalInterface`; `0x0056a3b8/0x0056a3be/0x0056a3c4 bl 0x56a444/0x56a504/0x56a5c4`; `0x0056a3d2 "Initialized whiteboard with no external interface. Will miss events."` | NEW | EXACT_SOURCE |
| 30 | Whiteboard update | `AIWhiteboard::Update` is a no-op (`bx lr`). | `0x0056a684 bx lr` | NEW | EXACT_SOURCE |
| 31 | Whiteboard beacon | `AIWhiteboard::AddBeacon(pose, radius)` appends an `AIBeacon` (Pose3d + float at `+0xc`) to the vector at `+0x60` and calls `UpdateBeaconRender()`. | `0x0056c39c`; `0x0056c3c0 vstr s16,[r0,#0xc]`; `0x0056c3ce str r0,[r4,#0x64]`; `0x0056c3de blx UpdateBeaconRender` | NEW | EXACT_SOURCE |
| 32 | Reaction interface | `IReactionTriggerStrategy::ShouldTriggerBehavior`: if the force flag at `+0x29` is set, calls `ShouldTriggerBehaviorInternal` (`vtable+0x24`), then `SetupForceTriggerBehavior` (`vtable+0x20`), clears the flag, and returns true regardless. Otherwise returns `ShouldTriggerBehaviorInternal`'s result. | `0x0060b63a`; `0x0060b646 ldrb.w r2,[r4,#0x29]`; `0x0060b64c ldr r7,[r1,#0x24]`; `0x0060b662 blx r7`; `0x0060b678 ldr r6,[r1,#0x20]`; `0x0060b68a blx r6`; `0x0060b696 strb.w r0,[r4,#0x29]`; `0x0060b69a movs r4,#1` | NEW | EXACT_SOURCE |
| 33 | Generic strategy gate | `ReactionTriggerStrategyGeneric::ShouldTriggerBehaviorInternal`: asserts the strategy's wants-to-run pointer (`+4`) is non-null (`"ReactionTriggerStrategyNoPreDockPoses.ShouldTriggerBehaviorInternal"`), treats a running candidate as runnable, else calls `IBehavior::IsRunnable`; applies a cooldown set at `+0x4c..+0x50` against `+0x3c` and the current timestamp; and ANDs the result with `IWantsToRunStrategy::WantsToRun`. | `0x0060f3bc`; `0x0060f3d6 sVerifyFailedReturnFalse`; `0x0060f3e6 blx IsRunnable`; `0x0060f3f2 blx GetCurrentTimeStamp`; `0x0060f404 ldr r2,[r5,#0x3c]`; `0x0060f448 blx IWantsToRunStrategy::WantsToRun`; `0x0060f44c ands r0,r7` | NEW | EXACT_SOURCE |
| 34 | Reaction locks | `IBehavior::SmartDisableReactionsWithLock` / `BehaviorManager::DisableReactionsWithLock` / `RemoveDisableReactionsLock` / `IBehavior::SmartRemoveDisableReactionsLock` are the per-behaviour lock path. Bodies not read in this pass. | `0x005bce3d`, `0x005a27e9`, `0x005a3a49`, `0x005bd471` | M7-014 | RECOVERABLE_GAP - read the four bodies |
| 35 | Smart scope helpers | The helpers exist: `SmartPushIdleAnimation 0x005be41d`, `SmartSetMotionProfile 0x005be519`, `SmartLockTracks 0x005be5bd`, `SmartUnLockTracks 0x005be6e1`, `SmartSetCustomLightPattern 0x005be7f1`, `SmartRemoveCustomLightPattern 0x005be9b1`, `SmartDelegateToHelper 0x005beb11`, `SmartRemoveIdleAnimation 0x005bd4c9`, `SmartClearMotionProfile 0x005bd585`. Bodies not read here. | as listed | M8-001 | RECOVERABLE_GAP - read each helper body; `Smart*` scope undo order is M8-009 |
| 36 | Action track lock | `IActionRunner::Update` refuses to run an action whose track mask is held and retries on the next tick; `MovementComponent::LockTracks`/`UnlockTracks` maintain a per-track owner multiset. Citation resolves to the named symbols. | `0x00540370`, `0x00640098`, `0x0063fe5c` | M8-007 | citation confirmed; body not re-read in this pass |
| 37 | Motor calibration wait | `CalibrateMotorAction::CheckIfDone` waits until the requested motors report calibrated; `IAction::IAction` writes -1 (no timeout) at `+0x74`. Citation resolves to the named symbols. | `0x00547d38`, `0x00540c44` | M8-008 | citation confirmed; body not re-read in this pass |
| 38 | PlayAnim list | `BehaviorPlayAnimSequence::StartPlayingAnimations` / `StartSequenceLoop` play every trigger in the config's order for `num_loops`. Citation resolves to the named symbols. | `0x005c0158`, `0x005c0294` | M8-005 | citation confirmed; body not re-read in this pass |
| 39 | Wants-to-run strategies | `WantsToRunStrategyFactory::CreateWantsToRunStrategy`, `StrategyInNeedsBracket::WantsToRunInternal`, `StrategyExpressNeedsTransition::WantsToRunInternal`. Citation resolves to the named symbols. | `0x00614710`, `0x006141a0`, `0x006136d8` | M8-006 | citation confirmed; body not re-read in this pass |

## Existing records, judged

The current manifest text was read for each record (2026-09-27).

- **M8-001** (EXACT_SOURCE, "Behaviour lifecycle and the Smart* scope helpers"): **evidence too weak
  for the claim.** Its only evidence is `IBehavior::IsRunnableBase 0x005BD778`. That function is real
  (verified above, row 17) but it is one predicate; it does not evidence the lifecycle (`Init`,
  `Update`, `Stop`, `StopActing`, `Resume`) or any `Smart*` helper. Every one of those is a separate
  function with its own address (rows 15-18, 35). Recommend splitting M8-001 into the lifecycle and
  the helpers, each with its own citations. Status EXACT_SOURCE should not stand for the whole claim.
- **M8-002** (EXACT_SOURCE, "Repetition penalty graph"): evidence is `config/engine/mood_config.json`,
  an OBB file that is **not present in this clone**. The penalty code is confirmed (row 21: graph at
  `+0xe8` evaluated over `now - +0x30`), but the graph's shape rests on the absent config.
  RECOVERABLE_GAP until the OBB is unpacked and `mood_config.json` is read.
- **M8-003** (EXACT_SOURCE, scored selection): **evidence proves only part of the claim.** Confirmed:
  `EvaluateScoreInternal` (`0x005beec2`) uses the mood scorer when the `+0xdc` vector is non-empty and
  otherwise the flat score at `+0x100`; `EvaluateScore` (`0x005bef60`) adds `+0x104` and multiplies by
  `EvaluateRunningPenalty` when `+0x111` is set; the scored chooser takes the maximum (row 24). **But
  the record says `ReadFromScoredJson` "reads four things" and does not mention the fifth key,
  `runningPenalty` -> graph at `+0xf4`** (`0x005bc602`, `0x005bc5f8`, `0x005bc666`), nor
  `EvaluateRunningPenalty` (`0x005bef22`). Add those before keeping EXACT_SOURCE for the whole path.
- **M8-004** (COMPATIBILITY_POLICY): confirmed. The constructor writes zero to `+0x100`/`+0x104`
  (`0x005bbd28 strd r8,r8,[r4,#0x100]`), and `EvaluateScoreInternal` returns that flat score when the
  mood-scorer list is empty. The policy framing (this stack's in-code 1 and 5) is not a source claim.
- **M8-005, M8-006, M8-007, M8-008**: the cited addresses all resolve to the named symbols (checked
  against the dynamic symbol table). The bodies were not re-read in this pass, so their detailed
  claims are neither confirmed nor contradicted here. No change recommended from this pass.
- **M8-009** (COMPATIBILITY_POLICY, "Behaviour scope undo order"): **evidence is empty.** No citation.
  A policy needs the engine behaviour it diverges from. RECOVERABLE_GAP / too weak until the
  `Smart*` bodies establish the order.
- **M8-010** (COMPATIBILITY_POLICY, inventory classifier rules): bookkeeping, `live_path: False`.
  Nothing to verify against the engine; out of the behaviour production path.

## NEW steps no record covers

- N1. `BehaviorManager::Update`'s full ordering, its not-initialised error path, and the two return
  codes of `IBehavior::Update` with their `FinishCurrentBehavior` calls (rows 2-10).
- N2. `BehaviorManager::SwitchToBehaviorBase`'s `IsRunnable` / `Init` gates and the Init-failure path
  that clears the behaviour but still sends the DAS transition (row 11).
- N3. `FinishCurrentBehavior`'s immediate branch and the default class-`0x16` resume info (row 12).
- N4. The reaction gate: reactions are evaluated only when the robot's action list is empty, with the
  latch at `+0x4c` (row 7).
- N5. `IReactionTriggerStrategy::ShouldTriggerBehavior`'s force-trigger path (row 32) and the generic
  strategy's cooldown + wants-to-run gate (row 33).
- N6. `IActivity::GetDesiredActiveBehavior` and the interlude mechanism (row 27).
- N7. `AIWhiteboard::Init`'s three handler registrations, the no-interface warning, the no-op
  `Update`, and `AddBeacon`'s vector + render (rows 29-31).
- N8. `EvaluateRunningPenalty` and the `runningPenalty` key (rows 22-23).
- N9. The chooser variants and the random tie-break in `ScoringBSRunnableChooser` (rows 24-26).

## Open questions for the manager

- Q1. The unpacked OBB is absent from this clone. M8-002 and any M7 config-derived fact cannot be
  read here. Should the manager provide the OBB (or the two config files) before the M7 reports are
  considered complete?
- Q2. M8-001 and M8-009 should be split or re-evidenced before M8 is approved. Is that an M8
  correction, or does it go to the integrator with this report?
- Q3. The `+0x2dc6c0` comparison inside `IBehavior::Init` and the exact semantics of `+0x104`,
  `+0x108`, `+0x110`, `+0x111` are not yet established; they are behaviour-changing for scored
  selection. Mark RECOVERABLE_GAP or send back for a focused pass?

## Appendix B: X3 completion pass, extractor report

re-analysis/research/20260928-X3-M8-framework-completion-extraction.md

﻿# Job X3 - M8-framework extraction, completion pass

Agent: opencode-w1 (DeepSeek). Read-only extraction. Date: 2026-09-28.

This pass resumes X3 after the unpacked OBB and the Ghidra decompilation were added to the clone.
It resolves the M8 rows that the 2026-09-27 report left as RECOVERABLE_GAP and adds the steps that
report did not read: the `IBehavior` lifecycle bodies, every `Smart*` scope helper, the strict-priority
chooser and the activity dispatch, the repetition-penalty config, and the behaviour-scope undo order.
It does not repeat what the earlier report already read.

## Sources

Primary source is `resources/lib/armeabi-v7a/libcozmoEngine.so` (ELF, ARMv7/Thumb). Every citation is
an address in it with the instruction that matters, read with `re-analysis/tools/disarm.py` (capstone +
lief). Dynamic symbols are present and demangled. The OBB is now present at
`re-analysis/obb/assets/cozmo_resources/`; the two config files below are read from it.

## Row table

| # | step | what the original does | citation | record | classification |
|---:|---|---|---|---|---|
| 1 | Init entry | `IBehavior::Init()` logs a channeled info line on `"Behaviors"`, then walks the robot's action list at `robot+0x250` and, for each action, tests the float at `action+0x60` against `0x2dc6c0` (3000000.0f). A hit warns and the walk stops. | `0x005bcb54 push.w {r4,...}`; `0x005bcbc0 ldr r0,[r4,#0x2c]`; `0x005bcbc2 ldr.w ip,[r0,#0x250]`; `0x005bcbd0 movw r6,#0xc6c0`; `0x005bcbd4 movt r6,#0x2d`; `0x005bcbee it hi` / `0x005bcbf0 movhi r5,#1` | M8-001 | EXACT_SOURCE |
| 2 | Init: spark gate | Sets `+0xd8` from the AI component's process: if `robot+0x44->+0x58 != 0x55`, `+0xd8 = !robot+0x44->+0x5c`, else 0. | `0x005bccac ldr r0,[r0,#0x44]`; `0x005bccae ldr r1,[r0,#0x58]`; `0x005bccb0 cmp r1,#0x55`; `0x005bccb4 ldrbne.w r0,[r0,#0x5c]`; `0x005bccb8 eorne r0,r0,#1`; `0x005bccc2 strb.w r0,[r4,#0xd8]` | NEW | EXACT_SOURCE |
| 3 | Init: clear acting + timers | Clears the shared `+0x98` (releases whatever it held), writes halfword `0x0100` at `+0xa0` (so byte `+0xa1`, the running flag, becomes 1 and byte `+0xa0` becomes 0), stamps the running-penalty clock `+0x34 = BaseStationTimer::GetCurrentTimeInSeconds()`, clears the idle-animation flag `+0xb0`, and calls `vtable+0x48` (`IsRunnableInternal`): a non-zero result sets `+0xa1 = 0`, otherwise `+0x80++`. | `0x005bccbe mov.w r1,#0x100`; `0x005bccc6 ldr.w r0,[r4,#0x98]`; `0x005bccca strh.w r1,[r4,#0xa0]`; `0x005bccea blx BaseStationTimer::getInstance`; `0x005bccf6 str r0,[r4,#0x34]`; `0x005bccfa strb.w r6,[r4,#0xb0]`; `0x005bccfe ldr r2,[r2,#0x48]`; `0x005bcd00 blx r2`; `0x005bcd06 strb.w r6,[r4,#0xa1]`; `0x005bcd0c ldr.w r0,[r4,#0x80]` | M8-001 | EXACT_SOURCE |
| 4 | Init: spark reaction lock | If the unlock id at `+0x70` is not `0x55` and equals `robot+0x44->+0x58`, builds the string `"SparkBehaviorDisables"` and calls `SmartDisableReactionsWithLock` with the 21-entry table at `0xc65f90`. Clears `+0x114`. | `0x005bcd16 ldr r0,[r4,#0x70]`; `0x005bcd1c ldr r1,[r4,#0x2c]`; `0x005bcd2e add r1,pc ; r1=0xbf2b0a "SparkBehaviorDisables"`; `0x005bcd3c ldr r2,[pc] ; =0x6a924e` -> `r2=0xc65f90`; `0x005bcd44 blx SmartDisableReactionsWithLock`; `0x005bcd58 str.w r0,[r4,#0x114]` | NEW | EXACT_SOURCE |
| 5 | Update | `IBehavior::Update()` calls `vtable+0xc` (`UpdateInternal`) unless byte `+0xa0` is non-zero and the current-action handle `+0x84` is zero, in which case it returns `2` (the "nothing to do" code the manager logs). | `0x005bd074 ldrb.w r1,[r0,#0xa0]`; `0x005bd078 cbz r1,#0x5bd080`; `0x005bd07a ldr.w r1,[r0,#0x84]`; `0x005bd07e cbz r1,#0x5bd088`; `0x005bd084 ldr r2,[r2,#0xc]`; `0x005bd086 bx r2`; `0x005bd088 movs r0,#2` | M8-001 | EXACT_SOURCE |
| 6 | Stop: order | `IBehavior::Stop()` logs, stops any helper, clears `+0xa1` (not running), calls `vtable+0x54` (`StopInternal`), stamps the last-run clock `+0x30 = now` (this is what `EvaluateRepetitionPenalty` reads), calls `StopActing(0,0)`, then undoes the scope **in this order**: (a) remove every disable-reactions lock while `+0xac != 0`, (b) if `+0xb0` remove the idle animation, (c) if `+0xc0` clear the motion profile, (d) walk the track-lock map at `+0xb4` and `UnlockTracks(mask, name)` each, then destroy the map. | `0x005bd106 ldr r0,[r4]`; `0x005bd10a ldr r1,[r4,#0x2c]`; `0x005bd10c strb.w r2,[r4,#0xa1]`; `0x005bd110 ldr r2,[r0,#0x54]`; `0x005bd114 blx r2`; `0x005bd11a blx BaseStationTimer::GetCurrentTimeInSeconds`; `0x005bd11e str r0,[r4,#0x30]`; `0x005bd126 blx StopActing(bool,bool)`; `0x005bd12c ldr.w r0,[r4,#0xa4]`; `0x005bd136 blx SmartRemoveDisableReactionsLock`; `0x005bd148 blx SmartRemoveIdleAnimation`; `0x005bd158 blx SmartClearMotionProfile`; `0x005bd16c ldrb r1,[r2,#0x1c]`; `0x005bd174 blx MovementComponent::UnlockTracks` | M8-001 / M8-009 | EXACT_SOURCE |
| 7 | StopActing | `StopActing(bool keepAction, bool viaCallback)`: calls `vtable+0x80(0)`; when `viaCallback` is false and a helper is live, logs `"Stopping behavior helper because action stopped without callback"` and calls `StopHelperWithoutCallback`; then, if a current action handle `+0x84` is set, cancels it in `ActionList` (and clears `+0x84` unless `keepAction`). | `0x005bd35e blx r2` (vtable+0x80); `0x005bd39c add r3,pc ; r3=0xbf28d0 "Stopping behavior helper because action stopped without callback"`; `0x005bd3a0 blx sChanneledInfoF`; `0x005bd3d6 blx StopHelperWithoutCallback`; `0x005bd3f0 blx ActionList::Cancel` | M8-001 | EXACT_SOURCE |
| 8 | Resume | `IBehavior::Resume(ReactionTrigger)`: trigger `0x14` (`Count`/invalid) or `0` (`NoneTrigger`) does not resume; it increments `+0x114`, and once that is `>= 1` sets `+0x118 = now + 15.0` and calls `MoodManager::TriggerEmotionEvent(name, now)`, returning 1. Otherwise sets `+0xa2 = 1`, stamps `+0x34 = now`, calls `vtable+0x4c` (`ResumeInternal`), clears `+0xa2`; a zero result sets `+0xa1 = 1` (running) and, when the unlock id equals the AI process, takes the `"SparkBehaviorDisables"` lock; a non-zero result clears `+0xa1`. | `0x005bcf16 cmp r5,#0x14`; `0x005bcf1a cmpne r5,#0`; `0x005bcf1e ldr.w r0,[r4,#0x114]`; `0x005bcf36 vmov.f32 s0,#1.500000e+01`; `0x005bcf4c vstr s0,[r4,#0x118]`; `0x005bcf68 blx MoodManager::TriggerEmotionEvent`; `0x005bcf80 strb.w r5,[r4,#0xa2]`; `0x005bcf94 ldr r2,[r2,#0x4c]`; `0x005bcf96 blx r2`; `0x005bcfa2 strb.w r1,[r4,#0xa1]`; `0x005bcfde blx SmartDisableReactionsWithLock` | M8-001 | EXACT_SOURCE |
| 9 | SmartPushIdleAnimation | If the idle-animation flag `+0xb0` is already set, logs (channel `Behaviors`) and returns 0. Otherwise calls `AnimationStreamer::PushIdleAnimation(trigger, name)` and sets `+0xb0 = 1`, returns 1. | `0x005be424 ldrb.w r0,[r4,#0xb0]`; `0x005be42a cbz r0,#0x5be44c`; `0x005be448 b.w 0x8cbe5c`; `0x005be464 blx AnimationStreamer::PushIdleAnimation`; `0x005be476 movs r0,#1`; `0x005be478 strb.w r0,[r4,#0xb0]` | M8-001 | EXACT_SOURCE |
| 10 | SmartRemoveIdleAnimation | Returns immediately unless `+0xb0` is set; otherwise `AnimationStreamer::RemoveIdleAnimation(name)` and clears `+0xb0`. | `0x005bd4d0 ldrb.w r0,[r4,#0xb0]`; `0x005bd4d4 cbz r0,#0x5bd508`; `0x005bd4ec blx AnimationStreamer::RemoveIdleAnimation`; `0x005bd500 strb.w r0,[r4,#0xb0]` | M8-001 | EXACT_SOURCE |
| 11 | SmartSetMotionProfile | Verifies `+0xc0 == 0` (`sVerifyFailedReturnFalse`), calls `PathComponent::SetCustomMotionProfile(profile)`, sets `+0xc0 = 1`. | `0x005be51e ldrb.w r0,[r4,#0xc0]`; `0x005be52a blx sVerifyFailedReturnFalse`; `0x005be534 blx PathComponent::SetCustomMotionProfile`; `0x005be53a strb.w r0,[r4,#0xc0]` | M8-001 | EXACT_SOURCE |
| 12 | SmartClearMotionProfile | Verifies `+0xc0 != 0`, calls `PathComponent::ClearCustomMotionProfile()`, clears `+0xc0`. | `0x005bd588 ldrb.w r0,[r4,#0xc0]`; `0x005bd58c cbnz r0,#0x5bd598`; `0x005bd594 blx sVerifyFailedReturnFalse`; `0x005bd59c blx PathComponent::ClearCustomMotionProfile`; `0x005bd5a2 strb.w r0,[r4,#0xc0]` | M8-001 | EXACT_SOURCE |
| 13 | SmartLockTracks | Emplaces `(name -> mask)` into the map at `+0xb4`. On a newly inserted key it calls `MovementComponent::LockTracks(mask, name, reason)` and returns 1; on an existing key it warns `"Attempted to lock tracks with key named %s but key already exists"` and returns 0 (no double lock). | `0x005be600 blx __tree::__emplace_unique_key_args`; `0x005be616 cbz r5,#0x5be62c`; `0x005be624 blx MovementComponent::LockTracks`; `0x005be628 movs r0,#1`; `0x005be636 add r2,pc ; r2=0xbf2960 "Attempted to lock tracks with key named %s but key already exists"`; `0x005be648 blx sWarningF` | M8-001 | EXACT_SOURCE |
| 14 | SmartUnLockTracks | Looks the name up in the map at `+0xb4`; if found calls `MovementComponent::UnlockTracks(mask, name)`, erases the entry and returns 1; if absent warns and returns 0. | `0x005be6ee blx __tree::find`; `0x005be6fa beq #0x5be716`; `0x005be700 ldrb r1,[r6,#0x1c]`; `0x005be706 blx MovementComponent::UnlockTracks`; `0x005be70e blx __tree::erase`; `0x005be712 movs r0,#1`; `0x005be716 movs r0,#0` | M8-001 | EXACT_SOURCE |
| 15 | SmartSetCustomLightPattern | If the `ObjectID` is already in the vector at `+0xcc`, logs an `"Unnamed"` channeled info and returns 0. Otherwise calls `CubeLightComponent::PlayLightAnim(objectID, trigger, callback, true, lights, timeout)` and appends the `ObjectID` to the vector at `+0xd0`, returns 1. | `0x005be806 ldr r0,[r6,#0xcc]!`; `0x005be820 cmp r1,r0`; `0x005be822 beq #0x5be866`; `0x005be832 add r0,pc ; r0=0xbe3fec "Unnamed"`; `0x005be862 movs r0,#0`; `0x005be87e blx CubeLightComponent::PlayLightAnim`; `0x005be8cc movs r0,#1` | M8-001 | EXACT_SOURCE |
| 16 | SmartRemoveCustomLightPattern | Looks the `ObjectID` up in the vector at `+0xcc` and, when found and the trigger list is non-empty, walks the triggers and calls the cube-light component to remove each; the entry is then erased. (Full body beyond the prologue was not re-read.) | `0x005be9ba ldrd r7,r0,[r8,#0xcc]`; `0x005be9c2 ldr r1,[r5,#4]`; `0x005be9d4 ldrd r6,r4,[r2]`; `0x005be9dc ldr.w r0,[r8,#0x2c]` | M8-001 | RECOVERABLE_GAP - read `0x005be9b0..0x005beb10` fully |
| 17 | SmartDelegateToHelper | Body not read in this pass. | `0x005beb10` | M8-001 | RECOVERABLE_GAP - read `0x005beb10..0x005bec..` |
| 18 | Reaction lock helpers | `IBehavior::SmartDisableReactionsWithLock` (`0x5bce3c`) and `IBehavior::SmartRemoveDisableReactionsLock` (`0x5bd470`) are the per-behaviour lock path; the manager side is `BehaviorManager::DisableReactionsWithLock` (`0x5a27e9`) / `RemoveDisableReactionsLock` (`0x5a3a49`). The call sites are read (rows 4, 6, 8); the four bodies are not. | as listed | M8-001 / M7-014 | RECOVERABLE_GAP - read the four bodies |
| 19 | Strict-priority chooser | `StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior` walks its behaviour vector at `+0x1c..+0x20`; a behaviour with `+0xa1` (running) non-zero is returned immediately, otherwise the first behaviour for which `IBehavior::IsRunnable(robot) == 1` is returned; if none, returns a null `shared_ptr`. No scoring, no tie-break. | `0x0060b242 ldrd r6,r7,[r1,#0x1c]`; `0x0060b24e ldr r0,[r6]`; `0x0060b250 ldrb.w r1,[r0,#0xa1]`; `0x0060b254 cbnz r1,#0x60b270`; `0x0060b258 blx IBehavior::IsRunnable`; `0x0060b25c cmp r0,#1`; `0x0060b266 movs r0,#0` | NEW | EXACT_SOURCE |
| 20 | Activity chooser dispatch | `IActivity::GetDesiredActiveBehaviorInternal` reads the chooser at `+0x24`; a null chooser warns `"VERIFY(%s): ChooseNextBehaviorInternal called without behavior chooser overwritten"` and returns a null behaviour; otherwise calls the chooser's `vtable+0x28`. | `0x005b3a70 ldr r6,[r1,#0x24]`; `0x005b3a76 cbz r6,#0x5b3aa0`; `0x005b3aa2 add r1,pc ; r1=0xbf2119 "VERIFY(%s): ChooseNextBehaviorInternal called without behavior chooser overwritten"`; `0x005b3a94 blx r7` | NEW | EXACT_SOURCE |
| 21 | Repetition-penalty config | `config/engine/mood_config.json` carries `defaultRepetitionPenalty` with nodes `(0.0, 0.0)` and `(30.0, 1.0)`. `ReadFromScoredJson` reads a behaviour's `repetitionPenalty` graph at `+0xe8`; a missing/failed graph warns and gets the single node `AddNode(0, 1, true)`, i.e. flat 1. `EvaluateRepetitionPenalty` returns 1 when the last-run stamp `+0x30 <= 0`, else evaluates `+0xe8` at `now - +0x30`. | `config/engine/mood_config.json:88-99`; `0x005beef6 movle.w r0,#0x3f800000`; `0x005bef0e add.w r0,r4,#0xe8`; `0x005bef1e b.w 0x8cbf9c` | M8-002 | EXACT_SOURCE |
| 22 | Scored JSON: runningPenalty | `ReadFromScoredJson` reads a **fifth** key besides `emotionScorers`, `flatScore`, `repetitionPenalty` and `considerThisHasRunForBehaviorObjective`: `runningPenalty` into the graph at `+0xf4` (default node `AddNode(0,1,true)` when empty). `EvaluateScore` multiplies by `EvaluateRunningPenalty` when `+0x111` is set; that returns 1 when `+0x34 <= 0`, else evaluates `+0xf4` at `now - +0x34`. | `0x005bc602 "runningPenalty"`; `0x005bc5f8 add.w r6,r4,#0xf4`; `0x005bc678 AddNode(0,1,true)`; `0x005bef22`; `0x005bef26 vldr s0,[r4,#0x34]`; `0x005bef4a add.w r0,r4,#0xf4`; `0x005bef5a b.w 0x8cbf9c` | M8-003 (partial) | EXACT_SOURCE |

## Existing records, judged

- **M8-001** (EXACT_SOURCE, "Behaviour lifecycle and the Smart* scope helpers"): **now confirmed for the
  lifecycle and most helpers**, but its evidence list still names only `IBehavior::IsRunnableBase
  0x005BD778`. That single citation does not evidence `Init`, `Update`, `Stop`, `StopActing`, `Resume`
  or any `Smart*` helper. Add the addresses read above. Two helpers (`SmartRemoveCustomLightPattern`,
  `SmartDelegateToHelper`) and the two reaction-lock bodies are still unread; the record should either
  cite them or split them out as RECOVERABLE_GAP. **Evidence too weak to keep EXACT_SOURCE as stated.**
- **M8-002** (EXACT_SOURCE, "Repetition penalty graph"): **now confirmed.** The shipped graph is
  `defaultRepetitionPenalty` `(0,0) -> (30,1)`; the consumer is `EvaluateRepetitionPenalty`
  (`0x005beef6`, `0x005bef0e`) with the last-run stamp at `+0x30` set by `Stop` (`0x005bd11e`). The
  record's evidence is just the config path; add the two code addresses.
- **M8-003** (EXACT_SOURCE, scored selection): **still proves only part of its claim.** It names
  `runningPenalty` nowhere. Row 22 confirms the key at `+0xf4` and `EvaluateRunningPenalty`. Its own
  text says "ReadFromScoredJson reads four things"; that is wrong. Correct the record before keeping
  EXACT_SOURCE for the whole path.
- **M8-004** (COMPATIBILITY_POLICY): unchanged; the engine default of zero is confirmed by the earlier
  pass.
- **M8-005..M8-008**: unchanged from the earlier pass; nothing here contradicts them.
- **M8-009** (COMPATIBILITY_POLICY, "Behaviour scope undo order"): **now has a source.** `IBehavior::Stop`
  (`0x005bd08c`) fixes the engine's order: helper stop, `StopInternal`, repetition-penalty stamp,
  `StopActing`, then disable-reaction locks, idle animation, motion profile, track locks (row 6). The
  record's `evidence` is empty and its authority is "the engine Smart* destructor order" - it is not a
  destructor, it is `Stop`. Replace the empty evidence and the authority wording; the policy claim
  (this stack releases in a different order) still needs its own text.
- **M8-010**: bookkeeping, out of the production path; unchanged.

## NEW steps no record covers

- N1. `IBehavior::Init`'s action-list scan against `0x2dc6c0`, the spark gate at `+0xd8`, the
  running-flag write, and the `"SparkBehaviorDisables"` lock (rows 1-4).
- N2. `IBehavior::Update`'s `+0xa0`/`+0x84` gate and its return `2` (row 5).
- N3. `IBehavior::Stop`'s exact undo order (row 6) - this is M8-009's missing authority.
- N4. `IBehavior::StopActing`'s helper-without-callback path and `ActionList::Cancel` (row 7).
- N5. `IBehavior::Resume`'s invalid-trigger counter `+0x114`, the `now + 15.0` stamp at `+0x118`, and
  the `TriggerEmotionEvent` call (row 8).
- N6. The strict-priority chooser has no scoring and picks the first runnable (row 19).
- N7. The `runningPenalty` key and `EvaluateRunningPenalty` (row 22) - a partial-evidence correction to
  M8-003.

## Open questions for the manager

- Q1. M8-001's evidence list is one predicate for a lifecycle-plus-nine-helpers claim. Does the manager
  correct it in place with the addresses above, or split the record into lifecycle and helpers?
- Q2. M8-003's title/evidence omit `runningPenalty`. Is that an M8 correction, or an integrator note?
- Q3. `SmartRemoveCustomLightPattern`, `SmartDelegateToHelper` and the four reaction-lock bodies remain
  unread. Send them back for a focused pass, or keep them RECOVERABLE_GAP?

## Appendix C: gap pass 1, extractor report

re-analysis/research/20260928-I-M8-gap1-extraction.md

# M8-framework gap pass 1 — row corrections and remaining RECOVERABLE_GAPs

Agent: cozmo-extractor (read-only). Date: 2026-09-28.
Primary source: `resources/lib/armeabi-v7a/libcozmoEngine.so` (ELF ARMv7/Thumb). Every citation is an address in that file with the instruction at it. `re-analysis/decomp/libcozmoEngine/` was used only to navigate. All functions were re-disassembled from the `.so` with a scratch helper (`.scratch/disasm_addr.py`).

Notes that apply to the whole report:
- Symbol values in the dynamic symbol table carry no Thumb bit; several earlier rows cited `symbol+1`. Where that happened it is called out.
- `IBehavior::Resume(ReactionTrigger)` symbol is `0x005bceac`; `IBehavior::Init` is `0x005bcb54`; `IBehavior::StopActing` is `0x005bd34c`; `IBehavior::SmartRemoveIdleAnimation` is `0x005bd4c8`; `IBehavior::EvaluateRepetitionPenalty` is `0x005beee6`.
- The `0x2dc6c0` immediate is the integer 3,000,000 (`movw r6,#0xc6c0; movt r6,#0x2d`), never a float. 3000000.0f would be `0x4B3709C0`.

## PART A — corrections to `20260927-X3-M8-framework-extraction.md`

| step | what the original does | citation (address + instruction) | record | classification |
|---|---|---|---|---|
| A1 | Row 5, current-behaviour class test against `0x2e`. The report's `0x005a2fde` is `movs r6,#0`; the compare is one instruction later. | `0x005a2fe0 cmp r7,#0x2e` (report cited `0x005a2fde movs r6,#0`); `0x005a2fd6 ldrb.w r7,[r7,#0x64]`; `0x005a305a strbeq.w r1,[r0,#0x220]` | NEW (row 5) | EXACT_SOURCE |
| A2 | Row 8, current-behaviour class test against `0x16`. The report's `0x005a306e` is `ldr r0,[r4,#0x1c]`; the byte load is next. | `0x005a3070 ldrb r0,[r0,#0x10]` (report cited `0x005a306e ldr r0,[r4,#0x1c]`); `0x005a3072 cmp r0,#0x16`; `0x005a3078 blxeq ChooseNextScoredBehaviorAndSwitch` (PLT `0x4b0ed0`) | M8-003 (related) | EXACT_SOURCE |
| A3 | Row 24, scored-chooser info line. The report's `0x0060a5f2` is `ldrb r2,[r0,#0x40]!`; the string pointer load is `0x0060a60a` and the log call `0x0060a61c`. | `0x0060a60a add r3,pc` → `r3=0xbf7775 "behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f"`; `0x0060a61c blx sChanneledInfoF` (PLT `0x4a505c`); report cited `0x0060a5f2` | M8-003 | EXACT_SOURCE |
| A4 | Row 33, generic strategy gate. The report's `0x0060f3f2` is `BaseStationTimer::getInstance`; `GetCurrentTimeStamp` is `0x0060f3f6`. The report's `0x0060f3d6` is `ldr r0,[pc,#0x80]`; the verify call is `0x0060f3de`. | `0x0060f3de blx sVerifyFailedReturnFalse` (PLT `0x4a4c6c`); `0x0060f3f2 blx BaseStationTimer::getInstance` (PLT `0x4a4f6c`); `0x0060f3f6 blx BaseStationTimer::GetCurrentTimeStamp` (PLT `0x4a7b58`); report cited `0x0060f3d6` and `0x0060f3f2` | NEW (row 33) | EXACT_SOURCE |
| A5 | Row 23, `ReadFromScoredJson` labels. `BehaviorObjectiveFromString` is `0x005bc5a0` (report `0x005bc5a4` is the store `str.w r0,[r4,#0x10c]`). `AddNode` is `0x005bc566` (report `0x005bc55e` is `movs r1,#0`). The string load is `0x005bc56e` (report `0x005bc56a` is `ldr r1,[pc]`). | `0x005bc5a0 blx BehaviorObjectiveFromString` (PLT `0x4ad3b4`); `0x005bc566 blx GraphEvaluator2d::AddNode` (PLT `0x4b1ecc`); `0x005bc56e add r1,pc` → `r1=0xbf2b2e "considerThisHasRunForBehaviorObjective"` | M8-003 (partial) | EXACT_SOURCE |
| A6 | Row 32, `IReactionTriggerStrategy::ShouldTriggerBehavior`. `SetupForceTriggerBehavior` is **not** unconditional: after `ShouldTriggerBehaviorInternal` returns, `cbnz r7,#0x60b694` skips the setup call whenever the internal result is non-zero. Corrected wording: "if the force flag at `+0x29` is set, calls `ShouldTriggerBehaviorInternal` (`vtable+0x24`); **only when that returns 0** does it call `SetupForceTriggerBehavior` (`vtable+0x20`); it then clears `+0x29` and returns 1 regardless. If the flag is clear it returns `ShouldTriggerBehaviorInternal`'s result directly." | `0x0060b646 ldrb.w r2,[r4,#0x29]`; `0x0060b64c ldr r7,[r1,#0x24]`; `0x0060b662 blx r7`; `0x0060b66e cbnz r7,#0x60b694`; `0x0060b678 ldr r6,[r1,#0x20]`; `0x0060b68a blx r6`; `0x0060b694 movs r0,#0`; `0x0060b696 strb.w r0,[r4,#0x29]`; `0x0060b69a movs r4,#1` | NEW (row 32) | EXACT_SOURCE |
| A7 | Row 28, the two missing activity implementations. `ActivityBuildPyramid::GetDesiredActiveBehaviorInternal` = `0x005a8854`; `ActivityFeeding::GetDesiredActiveBehaviorInternal` = `0x005abdd8`. The four addresses the report did give carry the Thumb bit; the true entries are `0x005b3a6c` (IActivity), `0x005ae29c` (Freeplay), `0x005b023c` (Socialize), `0x005b1d18` (Sparked), `0x005b267c` (StrictPriority). | `0x005a8854` / `0x005abdd8` (dynamic symbols `Anki::Cozmo::ActivityBuildPyramid::GetDesiredActiveBehaviorInternal`, `ActivityFeeding::...`); corrected entries `0x005b3a6c`, `0x005ae29c`, `0x005b023c`, `0x005b1d18`, `0x005b267c` | NEW (row 28) | EXACT_SOURCE |

## PART B — corrections to `20260928-X3-M8-framework-completion-extraction.md`

| step | what the original does | citation (address + instruction) | record | classification |
|---|---|---|---|---|
| B1 | Row 1. `movt r6,#0x2d` is at `0x005bcbda`; `0x005bcbd4` is `add.w r1,ip,#4`. `0x2dc6c0` is the **integer** 3,000,000, compared with `cmp`/`bhi`, never a float. `action+0x60` is a 32-bit unsigned **action tag**, not a float. The walk: `robot+0x250` is the `ActionList` (`std::map<int, ActionQueue>`); for each map node it reads the queue's current action (`ActionQueue+0` = node+0x14) and, if that did not set the flag, each queued action in the queue's list (node value +8), loads each action's `+0x60`, and sets a flag if `tag > 0x2dc6c0`. If the flag is set it logs `IBehavior.Init.ActionsInQueue` / `"Initializing %s: %zu actions already in queue"` and reports a queue's action count (queue element count plus 1 when a current action is set). **3,000,000 is the tag-counter sentinel, not a timeout or priority**: `sTagCounter` in `.data` at `0x01051020` holds `0x002dc6c1`, `NextIdTag` wraps to `0x2dc6c1` on overflow, and the tag is copied into `+0x60`. | `0x005bcbd0 movw r6,#0xc6c0`; `0x005bcbd4 add.w r1,ip,#4`; `0x005bcbda movt r6,#0x2d`; `0x005bcbe2 ldr r2,[r3,#0x14]`; `0x005bcbe8 ldr r2,[r2,#0x60]`; `0x005bcbec cmp r2,r6`; `0x005bcbee it hi` / `0x005bcbf0 movhi r5,#1`; `0x005bcc06 ldr r5,[r7,#8]`; `0x005bcc08 ldr r5,[r5,#0x60]`; `0x005bcc84 blx sWarningF` (PLT `0x4a4540`) with strings at `0x5bcde4 "IBehavior.Init.ActionsInQueue"` / `0x5bce04 "Initializing %s: %zu actions already in queue"`; `0x005bcd64 ldr r1,[r0,#0x14]` / `0x005bcd66 ldr r0,[r0,#0x20]` (count). Tag evidence: `0x0053fda0 movweq r2,#0xc6c1` / `0x0053fda4 movteq r2,#0x2d` (NextIdTag); `0x0053fe68 str r0,[r7]` with `r7=r5+0x5c`; `0x0053fec6 ldr r0,[r5,#0x5c]` / `0x0053fec8 str r0,[r5,#0x60]`; `sTagCounter` `.data 0x01051020 = 0x002dc6c1`; `0x00540ac4 ldr r1,[r5,#0x60]` then `0x00540ad0 blx ActionWatcher::GetSubActionResults` uses `+0x60` as the action tag | M8-001 | EXACT_SOURCE (the report's "float … 3000000.0f" is wrong) |
| B2 | Row 7. The vtable+0x80 call is `0x005bd360`; `0x005bd35e` is `mov r0,r4`. | `0x005bd35a ldr.w r2,[r0,#0x80]`; `0x005bd35e mov r0,r4`; `0x005bd360 blx r2` (report cited `0x005bd35e`) | M8-001 | EXACT_SOURCE |
| B3 | Row 18. The function entries are even; the cited values carried the Thumb bit. | `0x005a27e8 Anki::Cozmo::BehaviorManager::DisableReactionsWithLock`; `0x005a3a48 Anki::Cozmo::BehaviorManager::RemoveDisableReactionsLock` (report cited `0x005a27e9` / `0x005a3a49`) | M8-001 / M7-014 | EXACT_SOURCE |
| B4 | Row 21. `movle.w r0,#0x3f800000` is `0x005beef8`; `0x005beef6` is `itt le`. | `0x005beef6 itt le`; `0x005beef8 movle.w r0,#0x3f800000`; `0x005bef0e add.w r0,r4,#0xe8`; `0x005bef1e b.w 0x8cbf9c` | M8-002 | EXACT_SOURCE |
| B5 | Row 8, `IBehavior::Resume`. The claim "trigger 0x14 (Count) or 0 (NoneTrigger) does not resume" is contradicted. Exact flow: `cmp r5,#0x14` / `cmpne r5,#0` / `bne 0x5bcf7e` — for any other trigger it goes straight to the normal resume. For 0x14 or 0 it loads `+0x114`, compares it with 1, **increments** `+0x114`, and `blt 0x5bcf7e` — so on the **first** such trigger (`+0x114` was 0, i.e. <1) it **does** take the normal resume path. Only when the pre-increment `+0x114` is >= 1 does it take the special path: `+0x118 = now + 15.0`, build `"TooManyResumesCliffOrMovement"`, `MoodManager::TriggerEmotionEvent(name, now)`, return 1. Normal resume (`0x5bcf7e`): `+0xa2 = 1`; `+0x34 = now`; call `vtable+0x4c` (`ResumeInternal`); clear `+0xa2 = 0`; if `ResumeInternal` returned 0 set `+0xa1 = 1` (running) and, when the unlock id (`+0x70`) is not `0x55` and equals the robot's AI process (`robot+0x44->+0x58`), take the `"SparkBehaviorDisables"` lock via `SmartDisableReactionsWithLock`, returning 0; if `ResumeInternal` returned non-zero set `+0xa1 = 0` and return that non-zero value. `+0xa2` is set before and cleared after the `vtable+0x4c` call; `+0xa1` is written only after it. | `0x005bcf16 cmp r5,#0x14`; `0x005bcf1a cmpne r5,#0`; `0x005bcf1c bne #0x5bcf7e`; `0x005bcf1e ldr.w r0,[r4,#0x114]`; `0x005bcf22 cmp r0,#1`; `0x005bcf28 str.w r1,[r4,#0x114]`; `0x005bcf2c blt #0x5bcf7e`; `0x005bcf36 vmov.f32 s0,#1.500000e+01`; `0x005bcf4c vstr s0,[r4,#0x118]`; `0x005bcf68 blx MoodManager::TriggerEmotionEvent`; `0x005bcf7a movs r0,#1`; `0x005bcf80 strb.w r5,[r4,#0xa2]`; `0x005bcf90 str r0,[r4,#0x34]`; `0x005bcf94 ldr r2,[r2,#0x4c]`; `0x005bcf96 blx r2`; `0x005bcf9c strb.w r1,[r4,#0xa2]`; `0x005bcfa0 beq #0x5bcfa8`; `0x005bcfa2 strb.w r1,[r4,#0xa1]`; `0x005bcfa8 strb.w r5,[r4,#0xa1]`; `0x005bcfde blx SmartDisableReactionsWithLock`; `"TooManyResumesCliffOrMovement"` string at `0x5bd04c` | M8-001 | EXACT_SOURCE |
| B6 | Row 10, `IBehavior::SmartRemoveIdleAnimation`. `cbz r0,#0x5bd508` does **not** return immediately: `0x5bd508` builds the VERIFY-fail message and tail-branches through the veneer to `sVerifyFailedReturnFalse`. Only when `+0xb0` is set does it build `this+0x40 + "_hasSetIdle"`, call `AnimationStreamer::RemoveIdleAnimation(name)`, clear `+0xb0`, and return. Corrected behaviour: "when `+0xb0` is clear it raises `VERIFY(%s): Behavior %s is trying to remove an idle, but none is currently set` (via `sVerifyFailedReturnFalse`); otherwise it removes the idle animation and clears `+0xb0`." | `0x005bd4d0 ldrb.w r0,[r4,#0xb0]`; `0x005bd4d4 cbz r0,#0x5bd508`; `0x005bd508 mov r0,r4`; `0x005bd51e add r1,pc` → `r1=0xbf2911 "VERIFY(%s): Behavior %s is trying to remove an idle, but none is currently set"`; `0x005bd526 b.w 0x8cbe5c`; veneer `0x008cbe5c bx pc` / `0x008cbe60 ldr ip,[pc]` / `0x008cbe64 add pc,ip,pc` → `0x004a4c6c` PLT `sVerifyFailedReturnFalse`; success path `0x005bd4ec blx AnimationStreamer::RemoveIdleAnimation`; `0x005bd500 strb.w r0,[r4,#0xb0]` | M8-001 | EXACT_SOURCE |

## PART C — remaining RECOVERABLE_GAPs closed

| step | what the original does | citation (address + instruction) | record | classification |
|---|---|---|---|---|
| C1a | `IBehavior::IsRunnable(robot)` calls `IsRunnableBase(this->robot)`; if that is not 1 returns 0; otherwise tail-calls `vtable+0x50` with the caller's robot argument. | `0x005bd750 push {r4,r5,r7,lr}`; `0x005bd756 ldr r1,[r5,#0x2c]`; `0x005bd758 blx IsRunnableBase`; `0x005bd75c cmp r0,#1`; `0x005bd760 movne r0,#0`; `0x005bd768 ldr r2,[r0,#0x50]`; `0x005bd770 bx r2` | NEW / M8-001 | EXACT_SOURCE |
| C1b | `IBehavior::IsRunnableScored()` returns 1 when the current time is >= `+0x118`, else 0. `+0x118` is the `"TooManyResumesCliffOrMovement"` suppression timestamp (`now+15`) written by `Resume`. | `0x005bda28 push {r4,lr}`; `0x005bda2c blx BaseStationTimer::getInstance`; `0x005bda30 blx GetCurrentTimeInSeconds`; `0x005bda38 vldr s0,[r4,#0x118]`; `0x005bda3e vcmpe.f32 s2,s0`; `0x005bda46 it pl`; `0x005bda48 movpl r0,#1` | NEW / M8-001 | EXACT_SOURCE |
| C2 | `IBehavior::SmartRemoveCustomLightPattern(objectID, triggers)`: searches the vector at `+0xcc` for an 8-byte element whose `+4` equals `objectID+4`; if not found, logs `sChanneledInfoF("Unnamed","IBehavior.SmartRemoveCustomLightPattern.LightsNotSet", "No custom light pattern is set for object %d", objectID+4)` and returns 0; if found, for each trigger in the argument vector calls `CubeLightComponent::StopLightAnimAndResumePrevious(trigger, objectID)`, then erases the element from the vector (shifting later elements down) and returns 1. | `0x005be9ba ldrd r7,r0,[r8,#0xcc]`; `0x005be9c2 ldr r1,[r5,#4]`; `0x005be9c4 ldr r3,[r7,#4]`; `0x005be9d4 ldrd r6,r4,[r2]`; `0x005be9dc ldr.w r0,[r8,#0x2c]`; `0x005be9e4 ldr.w r0,[r0,#0x270]`; `0x005be9e8 blx StopLightAnimAndResumePrevious`; `0x005bea38..` log branch; `0x005bea4e blx sChanneledInfoF`; `0x005bea34 movs r0,#1`; `0x005bea74 movs r0,#0` | M8-001 | EXACT_SOURCE |
| C3 | `IBehavior::SmartDelegateToHelper(robot, helper, cb1, cb2)`: logs `"Behaviors"/"Behavior requesting to delegate to helper %s"`; if the current helper `+0xc8` is live (`[+0xc8]!=0` and `[+0xc8]+4 != -1`) warns `"Attempted to start a handler while handle already running, stopping running helper"` and calls `StopHelperWithoutCallback()`; then calls `BehaviorHelperComponent::DelegateToHelper` on `[robot+0x264]+0x10`; if it returns 1 stores the helper as a weak ref at `+0xc4`/`+0xc8`; otherwise logs `"Behaviors"/"SmartDelegateToHelper.Failed"/"Failed to delegate to helper"`; returns the DelegateToHelper result. | `0x005beb34 blx operator+`; `0x005beb70 blx sChanneledInfoF`; `0x005beba4 ldr.w r0,[r4,#0xc8]`; `0x005beba8 cbz r0,#0x5bebec`; `0x005bebac adds r0,#1`; `0x005bebae beq #0x5bebec`; `0x005bebb8 add r2,pc` → warning string; `0x005bebe8 blx StopHelperWithoutCallback`; `0x005bebee ldr.w r1,[r0,#0x264]`; `0x005bebf8 ldr r7,[r1,#0x10]`; `0x005bec2c blx BehaviorHelperComponent::DelegateToHelper`; `0x005bec62 cmp r6,#1`; `0x005bec6c blx add_weak`; `0x005bec72 str.w r7,[r4,#0xc4]`; `0x005bec7a str.w r5,[r4,#0xc8]`; `0x005bec8c add r2,pc` → `"SmartDelegateToHelper.Failed"`; `0x005becb2 blx sChanneledInfoF`; `0x005becf4 moveq r0,r6` | M8-001 | EXACT_SOURCE |
| C4a | `IBehavior::SmartDisableReactionsWithLock(lockName, table)`: takes the behavior manager `[robot+0x44]`, appends `"_behaviorLock"` to the name, calls `BehaviorManager::DisableReactionsWithLock(manager, name+"_behaviorLock", table, true)`, then inserts the original name into the per-behavior set at `+0xa4`. | `0x005bce4c ldr r0,[r5,#0x2c]`; `0x005bce52 ldr r7,[r0,#0x44]`; `0x005bce50 add r2,pc` → `"_behaviorLock"`; `0x005bce62 blx BehaviorManager::DisableReactionsWithLock`; `0x005bce74 add.w r1,r5,#0xa4`; `0x005bce7e blx __tree::__emplace_unique_key_args` | M8-001 / M7-014 | EXACT_SOURCE |
| C4b | `BehaviorManager::DisableReactionsWithLock(name, table, stopCurrent)`: walks the reaction-trigger map at `+0x40`; for each trigger whose table entry is true, logs `"ReactionTriggers"/"BehaviorManager.DisableReactionsWithLock.DisablingWithLock"/"Trigger %s is being disabled by %s"`; if the trigger has no existing locks (`+0x28==0`) calls `vtable+0x1c(robot, 0)` on every mapped strategy; if the name is not already present adds a disable lock via `TriggerBehaviorInfo::AddDisableLockToTrigger`; and when `stopCurrent` is true and the manager's current behaviour class equals the trigger, logs `"BehaviorManager.DisableReactionsWithLock"/"Disabling reaction triggers - stopping currently running one"` and switches to the default class-`0x16` behaviour. | `0x005a2818 ldr r5,[r4,#0x40]`; `0x005a2836 blx __tree::find`; `0x005a283c ldr r0,[sp,#0x18]`; `0x005a2842 ldrb r0,[r0,#1]`; `0x005a2870 blx sChanneledInfoF`; `0x005a2896 ldr r0,[r5,#0x28]`; `0x005a28a8 ldr r0,[sl],#0xc` / `0x005a28b0 ldr r3,[r2,#0x1c]` / `0x005a28b4 blx r3`; `0x005a28c8 blx TriggerBehaviorInfo::AddDisableLockToTrigger`; `0x005a28d2..` stop-current branch; `0x005a2924 movs r0,#0x16`; `0x005a292e blx SwitchToBehaviorBase` | M8-001 / M7-014 | EXACT_SOURCE |
| C4c | `BehaviorManager::RemoveDisableReactionsLock(name)`: ORs the manager latch `+0x4c` with `(name == "sdk")` (length-3 compare against `"sdk"`); walks the trigger map; for each trigger that contains the lock logs `"ReactionTriggers"/"BehaviorManager.RemoveDisableReactionsLock.RemovingLock"/"Lock %s is being removed from trigger %s"`, calls `TriggerBehaviorInfo::RemoveDisableLockFromTrigger`, and if the trigger now has no locks (`+0x28==0`) logs `"...ReactionReEnabled"/"No remaining locks on trigger %s"` and calls `vtable+0x1c(robot, 1)` on every mapped strategy. | `0x005a3a66 movs r0,#3`; `0x005a3a76 blx string::compare`; `0x005a3a7a cmp r0,#0`; `0x005a3a84 ldrb.w r0,[sb,#0x4c]`; `0x005a3a8c orrs r0,r6`; `0x005a3a8e strb.w r0,[sb,#0x4c]`; `0x005a3aae blx __tree::find`; `0x005a3ae0 blx sChanneledInfoF`; `0x005a3b10 blx RemoveDisableLockFromTrigger`; `0x005a3b14 ldr r0,[r7,#0x28]`; `0x005a3b34 blx sChanneledInfoF`; `0x005a3b62 ldr r0,[r4],#0xc` / `0x005a3b6c ldr r3,[r2,#0x1c]` / `0x005a3b6e movs r2,#1` / `0x005a3b70 blx r3` | M8-001 / M7-014 | EXACT_SOURCE |
| C4d | `IBehavior::SmartRemoveDisableReactionsLock(lockName)`: takes the manager, appends `"_behaviorLock"`, calls `BehaviorManager::RemoveDisableReactionsLock(manager, name+"_behaviorLock")`, then erases the original name from the per-behavior set at `+0xa4`. | `0x005bd478 ldr r0,[r5,#0x2c]`; `0x005bd480 ldr r6,[r0,#0x44]`; `0x005bd47c add r2,pc` → `"_behaviorLock"`; `0x005bd48c blx BehaviorManager::RemoveDisableReactionsLock`; `0x005bd49e add.w r0,r5,#0xa4`; `0x005bd4a4 blx __tree::__erase_unique` | M8-001 / M7-014 | EXACT_SOURCE |
| C5a | `+0x104` (float): running-score bonus. Constructor zeroes it. `IncreaseScoreWhileActing(float)` adds the argument to it **only when the current-action handle `+0x84` is non-zero**; `ScoredActingStateChanged(bool)` clears it. `EvaluateScore` adds it to `EvaluateRunningScoreInternal` while running. | `0x005bbd28 strd r8,r8,[r4,#0x100]` (`+0x100=0`, `+0x104=0`); `0x005bf02c ldr.w r2,[r0,#0x84]`; `0x005bf030 cbz r2,#0x5bf042`; `0x005bf036 vldr s2,[r0,#0x104]`; `0x005bf03a vadd.f32 s0,s2,s0`; `0x005bf03e vstr s0,[r0,#0x104]`; `0x005bf046 str.w r1,[r0,#0x104]`; read at `0x005bef80 vldr s2,[r4,#0x104]` / `0x005bef88 vadd.f32 s16,s0,s2` | M8-003 | EXACT_SOURCE |
| C5b | `+0x108` (float): repetition-penalty suppression threshold in seconds. Constructor sets it to 0. `StopWithoutImmediateRepetitionPenalty()` sets it to `now + 1.0`. `EvaluateScore`'s non-running branch applies the repetition penalty only when `+0x110` is set and `now >= +0x108` (so for ~1 s after a no-penalty stop the penalty is skipped). | `0x005bbd2c strd r8,r0,[r4,#0x108]` (`r8=0` → `+0x108=0`; `r0=0x29` at `0x005bbd26 movs r0,#0x29` → `+0x10c=0x29`); `0x005beeb0 vmov.f32 s0,#1.000000e+00`; `0x005beeb8 vadd.f32 s0,s2,s0`; `0x005beebc vstr s0,[r4,#0x108]`; read at `0x005befe2 vldr s0,[r4,#0x108]`; `0x005befea vcmpe.f32 s2,s0`; `0x005beff2 bpl #0x5beffe` | M8-003 | EXACT_SOURCE |
| C5c | `+0x110` (byte): enables the repetition-penalty multiplier in the non-running scored branch. Constructor sets `+0x110=1` and `+0x111=1` with one halfword store `0x0101`; no other IBehavior writer was found, so it is a constant enable. `EvaluateScore` returns the raw score when `+0x110==0`. | `0x005bbd30 movw r0,#0x101`; `0x005bbd34 strh.w r0,[r4,#0x110]`; read at `0x005befd4 ldrb.w r0,[r4,#0x110]`; `0x005befd8 cbz r0,#0x5beff4` | M8-003 | EXACT_SOURCE |
| C5d | `+0x111` (byte): enables the running-penalty multiplier in the running branch. Set to 1 by the same constructor halfword; no other IBehavior writer found. `EvaluateScore` returns the raw running score when `+0x111==0`, otherwise multiplies by `EvaluateRunningPenalty()`. | `0x005bbd34 strh.w r0,[r4,#0x110]`; read at `0x005bef84 ldrb.w r0,[r4,#0x111]`; `0x005bef8c cbz r0,#0x5beff4`; `0x005bef90 blx EvaluateRunningPenalty`; `0x005bef98 vmul.f32 s16,s16,s0` | M8-003 | EXACT_SOURCE |

## Existing records contradicted by the source

1. **2026-09-27 row 5** — cited `0x005a2fde cmp r7,#0x2e`. `0x005a2fde` is `movs r6,#0`; the compare is `0x005a2fe0`.
2. **2026-09-27 row 8** — cited `0x005a306e ldrb r0,[r0,#0x10]`. `0x005a306e` is `ldr r0,[r4,#0x1c]`; the byte load is `0x005a3070`.
3. **2026-09-27 row 24** — cited `0x0060a5f2` for the string/log. `0x0060a5f2` is `ldrb r2,[r0,#0x40]!`; the string load is `0x0060a60a` and the log call `0x0060a61c`.
4. **2026-09-27 row 33** — cited `0x0060f3f2` for `GetCurrentTimeStamp` (it is `BaseStationTimer::getInstance`; `GetCurrentTimeStamp` is `0x0060f3f6`) and `0x0060f3d6` for `sVerifyFailedReturnFalse` (it is `ldr r0,[pc,#0x80]`; the call is `0x0060f3de`).
5. **2026-09-27 row 23** — the three labels are each one-to-two instructions early: `BehaviorObjectiveFromString` is `0x005bc5a0` not `0x005bc5a4`; `AddNode` is `0x005bc566` not `0x005bc55e`; the `considerThisHasRunForBehaviorObjective` load is `0x005bc56e` not `0x005bc56a`.
6. **2026-09-27 row 32** — `SetupForceTriggerBehavior` is not called unconditionally; `0x0060b66e cbnz r7,#0x60b694` skips it when `ShouldTriggerBehaviorInternal` returns non-zero.
7. **2026-09-27 row 28** — only four activity addresses were given for six named activities; `ActivityBuildPyramid` `0x005a8854` and `ActivityFeeding` `0x005abdd8` were missing, and the four given addresses carried the Thumb bit.
8. **2026-09-28 row 1** — `0x005bcbd4` is `add.w r1,ip,#4`, not `movt`; the `movt` is `0x005bcbda`. `0x2dc6c0` is an integer 3,000,000 tag-counter sentinel, not `3000000.0f`; `action+0x60` is the action tag, not a float.
9. **2026-09-28 row 7** — `0x005bd35e` is `mov r0,r4`; the `blx r2` (vtable+0x80) is `0x005bd360`.
10. **2026-09-28 row 18** — the cited `0x005a27e9` / `0x005a3a49` carry the Thumb bit; the entries are `0x005a27e8` / `0x005a3a48`.
11. **2026-09-28 row 21** — `0x005beef6` is `itt le`, not the `movle`; the `movle.w r0,#0x3f800000` is `0x005beef8`.
12. **2026-09-28 row 8** — "trigger 0x14 (Count) or 0 (NoneTrigger) does not resume" is contradicted: the first such trigger (`+0x114` was 0) takes the normal resume path; only later ones take the `TooManyResumesCliffOrMovement` path.
13. **2026-09-28 row 10** — "Returns immediately unless `+0xb0` is set" is contradicted: when `+0xb0` is clear it raises a VERIFY failure through `sVerifyFailedReturnFalse`, it does not silently return.

## Existing records whose evidence is too weak to keep their status

- **M8-001** (`EXACT_SOURCE`, "Behaviour lifecycle and the Smart* scope helpers"): its `evidence` list still names only `IBehavior::IsRunnableBase 0x005BD778`. That single predicate does not evidence `Init` (`0x005bcb54`), `Update` (`0x005bd074`), `Stop` (`0x005bd08c`), `StopActing` (`0x005bd34c`), `Resume` (`0x005bceac`), `IsRunnable` (`0x005bd750`), `IsRunnableScored` (`0x005bda28`), `SmartPushIdleAnimation` (`0x005be41c`), `SmartRemoveIdleAnimation` (`0x005bd4c8`), `SmartSetMotionProfile` (`0x005be518`), `SmartClearMotionProfile` (`0x005bd584`), `SmartLockTracks` (`0x005be5bc`), `SmartUnLockTracks` (`0x005be6e0`), `SmartSetCustomLightPattern` (`0x005be7f0`), `SmartRemoveCustomLightPattern` (`0x005be9b0`), `SmartDelegateToHelper` (`0x005beb10`), `SmartDisableReactionsWithLock` (`0x005bce3c`) or `SmartRemoveDisableReactionsLock` (`0x005bd470`). Add those addresses or split the record.
- **M8-003** (`EXACT_SOURCE`, scored selection): its own text says `ReadFromScoredJson` reads four things; the fifth key `runningPenalty` → graph at `+0xf4` (`0x005bc602`, `0x005bc5f8`, `0x005bc678`) and `EvaluateRunningPenalty` (`0x005bef22`) are omitted. Also the `+0x104`/`+0x108`/`+0x110`/`+0x111` semantics above are not in the record.
- **M8-002** (`EXACT_SOURCE`, repetition penalty): its `evidence` is only `config/engine/mood_config.json`. The consumer addresses (`0x005beef6`, `0x005bef0e`) and the `+0x108` suppression threshold should be added; the record does not mention `+0x108` or `StopWithoutImmediateRepetitionPenalty` (`0x005beea0`).
- **M8-009** (`COMPATIBILITY_POLICY`, scope undo order): its `evidence` is still empty; the authority is `IBehavior::Stop` (`0x005bd08c`), whose undo order is (a) disable-reaction locks while `+0xac != 0`, (b) idle animation if `+0xb0`, (c) motion profile if `+0xc0`, (d) track-lock map at `+0xb4`. Replace the empty evidence and the "destructor" wording.

## Open questions for the manager

1. **B1 sentinel meaning vs. loop intent.** I established `+0x60` is the action tag and `0x2dc6c0` is one below the tag counter's initial/wrap value (`sTagCounter` at `0x01051020 = 0x2dc6c1`). Because every real `IActionRunner` tag is `>= 0x2dc6c1`, `tag > 0x2dc6c0` is true for any queued action, so the `IBehavior.Init.ActionsInQueue` warning fires whenever a queue is non-empty. I could not establish a case where `+0x60` is `<= 0x2dc6c0` (i.e. what stale/placeholder object the check is guarding against). If the manager needs that, it is a focused question for a future pass.
2. **`ReactionTrigger` names.** I used the earlier report's labels `0x14 = Count`, `0 = NoneTrigger` for B5; I did not independently read the enum mapping. The numeric control flow is exact; only the two names are unverified.
3. **Init warning count.** The warning site reads one queue's count via a lower-bound on map key 0 (`0x005bcc4c ldr r2,[ip,#4]!` … `0x005bcd66 ldr r0,[r0,#0x20]`). Whether that is the intended "actions in queue" count or an artifact of the compiled loop is not settled by the instructions; the warning is emitted, the count's exact scope is UNKNOWN.
4. **`+0x10c`** (BehaviorObjective) is initialized to `0x29` (the "invalid/none" sentinel used by `ReadFromScoredJson`'s `cmp r0,#0x29` at `0x005bc5ba`) but is outside the four fields C5 asked about; flagged here so the record can name it.

## Appendix D: gap pass 2, extractor report

re-analysis/research/20260928-I-M8-gap2-extraction.md

# M8-framework gap pass 2 — the three open questions settled

Agent: cozmo-extractor (read-only). Date: 2026-09-28.
Primary source: `resources/lib/armeabi-v7a/libcozmoEngine.so` (ELF ARMv7/Thumb). Every citation is an address in that file with the instruction at it. `re-analysis/decomp/libcozmoEngine/` was used only to navigate. Raw addresses were disassembled with the existing scratch helper `.scratch/disasm_addr.py` (copied from `re-analysis/tools/disarm.py`).

Symbols used (dynamic symbol values carry no Thumb bit):
`IActionRunner::IActionRunner` = `0x0053fdb0`; `IActionRunner::NextIdTag` = `0x0053fd94`; `IActionRunner::SetTag` = `0x00540098`; `sTagCounter` = `0x01051020`; `sInUseTagSet` = `0x0105a8f4`; `RobotEventHandler::_gameActionTagCounter` = `0x01061018`; `IBehavior::Init` = `0x005bcb54`; `ActionList::QueueAction` = `0x0053d93c`; `ActionList::QueueActionNow` = `0x0053dca0`; `EnumToString(ReactionTrigger)` = `0x0077065c`; `ReactionTriggerFromString` = `0x00770674`.

---

## Question 1 — `IBehavior::Init`'s `action+0x60 > 0x2dc6c0` guard

**What the original does.** `+0x60` is the action's 32-bit tag. There are exactly two writers:

1. **`IActionRunner::IActionRunner` (0x0053fdb0)** assigns the tag from `sTagCounter`:
   - `0x0053fe26 add.w r7,r5,#0x5c` (r7 = &this+0x5c)
   - `0x0053fe54 ldr.w r0,[fp]` (fp = &sTagCounter; r0 = current counter)
   - `0x0053fe58 adds r1,r0,#1`
   - `0x0053fe5c movweq r1,#0xc6c1` / `0x0053fe60 movteq r1,#0x2d` (on wrap, counter = 0x2dc6c1)
   - `0x0053fe64 str.w r1,[fp]` (sTagCounter = old+1)
   - `0x0053fe68 str r0,[r7]` (this+0x5c = old counter)
   - `0x0053fe72 blx __tree<unsigned int>::__emplace_unique_key_args` into `sInUseTagSet`; `0x0053fe76 ldrb.w r0,[sp,#0x1c]` / `0x0053fe7a cbnz r0,#0x53fec4` retries while the tag is already in use
   - `0x0053fec6 ldr r0,[r5,#0x5c]` / `0x0053fec8 str r0,[r5,#0x60]` (this+0x60 = tag)
   - `sTagCounter` initial value read from `.data` `0x01051020` = `0x002dc6c1` (file offset 0x1050020, bytes `c1 c6 2d 00`).
   So every constructed `IActionRunner` has a tag `>= 0x2dc6c1`; the wrap case assigns `0xffffffff`, which is also `> 0x2dc6c0`.
   `NextIdTag` (`0x0053fd94`: `0x0053fd9a ldr r0,[r1]` / `0x0053fd9c adds r2,r0,#1` / `0x0053fda0 movweq r2,#0xc6c1` / `0x0053fda4 movteq r2,#0x2d` / `0x0053fda8 str r2,[r1]`) returns the current counter and increments the same way, but has **no callers** (Ghidra `// callers: (none)` for `0053fd94.c`); the constructor inlines it. It never writes `+0x60`.

2. **`IActionRunner::SetTag` (0x00540098)** overwrites `+0x60` with a caller-supplied tag:
   - `0x005400a8 ldr r0,[r4,#0x18]` / `0x005400ae cmp.w r0,#0x1000000` / `0x005400b2 bne #0x540102` (a warning path if the state is exactly 0x1000000)
   - `0x00540102 mov r6,r4` / `0x00540104 ldr r0,[r6,#0x60]!` (r6 = &this+0x60)
   - `0x0054011e cbz r5,#0x54013e` — a tag argument of **0 is rejected** (error path, `+0x60` unchanged, returns 0)
   - `0x0054013a str r3,[r6]` (this+0x60 = new tag)
   SetTag is called from exactly three places, all in the game/external message path (Ghidra callee lists; `SetTag@004a8ce0` appears only in `00525a34.c`, `00527950.c`, `00527b74.c`):
   - **`RobotEventHandler::HandleActionEvents` (0x00525a34)** — assigns the tag from `RobotEventHandler::_gameActionTagCounter`:
     - `0x00525a9c ldr r1,[r0]` (r0 = &_gameActionTagCounter; r1 = counter)
     - `0x00525a9e adds r1,#1`
     - `0x00525aa0 cmp r1,r2` with `0x00525a8e movw r2,#0x8480` / `0x00525a96 movt r2,#0x1e` (r2 = 0x1e8480 = 2,000,000)
     - `0x00525aa4 movwhi r1,#0x4241` / `0x00525aa8 movthi r1,#0xf` (on overflow, counter = 0x000f4241 = 1,000,001)
     - `0x00525aac str r1,[r0]` / `0x00525ab0 blx SetTag` (tag = counter, 1..2,000,000)
     - `_gameActionTagCounter` is in `.bss` at `0x01061018` and is zero-initialised, so the first tag is 1.
   - **`RobotEventHandler::HandleMessage<QueueSingleAction>` (0x00527950)** — `0x00527a74 ldr r1,[r4]` / `0x00527a78 blx SetTag`; `r4` is the message (`0x00527956 mov r4,r1`), so the tag is the message's first uint32 field.
   - **`RobotEventHandler::HandleMessage<QueueCompoundAction>` (0x00527b74)** — `0x00527c12 ldr r1,[r4]` / `0x00527c16 blx SetTag`; `0x00527b7c mov r4,r1`; same, the message's first uint32 field.

`ICompoundAction::SetProxyTag` (`0x0054f20c`) writes `this+0x94` (`0x0054f218 str r1,[r5,#0x94]!`) and reads `[child+0x60]` (`0x0054f236 ldr r1,[r4,#0x60]`) only to compare; it does **not** write any action's `+0x60`.

**The guard.** `IBehavior::Init` sets its flag when an action tag is `> 0x2dc6c0`:
- current action: `0x005bcbe2 ldr r2,[r3,#0x14]` / `0x005bcbe8 ldr r2,[r2,#0x60]` / `0x005bcbec cmp r2,r6` (r6 = 0x2dc6c0, built by `0x005bcbd0 movw r6,#0xc6c0` / `0x005bcbda movt r6,#0x2d`) / `0x005bcbee it hi` / `0x005bcbf0 movhi r5,#1`
- queued actions: `0x005bcc06 ldr r5,[r7,#8]` / `0x005bcc08 ldr r5,[r5,#0x60]` / `0x005bcc0a cmp r5,r6` / `0x005bcc10 it hi` / `0x005bcc12 movhi r5,#1`
- the warning is taken when the flag is set: `0x005bcbde lsls r2,r5,#0x1f` / `0x005bcbe0 bne #0x5bcc36`.

**Answer.** Yes, there is a real case. Every engine-constructed `IActionRunner` has a tag `>= 0x2dc6c1`, but an action queued through the game/external path has its tag overwritten by `SetTag` with a value in `[1, 2,000,000]` (the `_gameActionTagCounter` path, `0x00525aac`/`0x00525ab0`) or with the app-supplied first field of a `QueueSingleAction`/`QueueCompoundAction` message (`0x00527a74`/`0x00527a78`, `0x00527c12`/`0x00527c16`). Those tags are `<= 0x2dc6c0`, so they do not set the flag. `+0x60` is never zero (the constructor always writes it; `SetTag` refuses 0 at `0x0054011e`). The guard therefore excludes exactly the externally-tagged actions; the `"Initializing %s: %zu actions already in queue"` warning fires only when at least one action carrying an engine (`sTagCounter`) tag is present in some queue.

| what the original does | citation (address + instruction) | classification |
|---|---|---|
| `+0x60` is written only by the `IActionRunner` constructor (from `+0x5c`, which is the current `sTagCounter`) and by `SetTag`; the constructor's tags are always `>= 0x2dc6c1` | `0x0053fec6 ldr r0,[r5,#0x5c]`; `0x0053fec8 str r0,[r5,#0x60]`; `0x0053fe54 ldr.w r0,[fp]`; `0x0053fe5c movweq r1,#0xc6c1`; `0x0053fe60 movteq r1,#0x2d`; `sTagCounter` `.data 0x01051020 = 0x002dc6c1` | EXACT_SOURCE |
| `SetTag` writes a caller tag to `+0x60`, refuses 0 | `0x00540104 ldr r0,[r6,#0x60]!`; `0x0054011e cbz r5,#0x54013e`; `0x0054013a str r3,[r6]` | EXACT_SOURCE |
| `HandleActionEvents` tags game actions from `_gameActionTagCounter`, which starts at 0 and runs 1..2,000,000 | `0x00525a9c ldr r1,[r0]`; `0x00525a9e adds r1,#1`; `0x00525aac str r1,[r0]`; `0x00525ab0 blx SetTag`; `_gameActionTagCounter` `.bss 0x01061018` = 0 | EXACT_SOURCE |
| `QueueSingleAction` / `QueueCompoundAction` set the tag from the message's first uint32 | `0x00527a74 ldr r1,[r4]`; `0x00527a78 blx SetTag`; `0x00527c12 ldr r1,[r4]`; `0x00527c16 blx SetTag` | EXACT_SOURCE |
| the `Init` guard sets its flag only for tags `> 0x2dc6c0`, i.e. engine tags; externally-tagged actions are excluded | `0x005bcbec cmp r2,r6`; `0x005bcbf0 movhi r5,#1`; `0x005bcc0a cmp r5,r6`; `0x005bcc12 movhi r5,#1`; `0x005bcbde lsls r2,r5,#0x1f`; `0x005bcbe0 bne #0x5bcc36` | EXACT_SOURCE |

---

## Question 2 — `ReactionTrigger` numeric values for 0 and 0x14

**What the original does.** `EnumToString(ReactionTrigger)` (`0x0077065c`) rejects `r0 > 0x16` and otherwise indexes a string-pointer table at `0x10337c0`:
- `0x0077065c cmp r0,#0x16`
- `0x0077065e itt hi` / `0x00770660 movhi r0,#0` / `0x00770662 bxhi lr`
- `0x00770664 ldr r1,[pc,#8]` (literal at `0x00770670` = `0x8c3154`)
- `0x00770666 sxtb r0,r0`
- `0x00770668 add r1,pc` -> `r1 = 0x8c3154 + 0x0077066c = 0x10337c0`
- `0x0077066a ldr.w r0,[r1,r0,lsl #2]`

The table at `0x10337c0` (read from the file; entries are string pointers):

| index | string |
|---|---|
| 0 (0x00) | `0xc1ae42 "CliffDetected"` |
| 1 (0x01) | `0xc1ae50 "CubeMoved"` |
| 2 (0x02) | `0xc1ae5a "FacePositionUpdated"` |
| 3 (0x03) | `0xc19f9d "FistBump"` |
| 4 (0x04) | `0xc1ae6e "Frustration"` |
| 5 (0x05) | `0xc1772d "Hiccup"` |
| 6 (0x06) | `0xc12361 "MotorCalibration"` |
| 7 (0x07) | `0xc1ae7a "NoPreDockPoses"` |
| 8 (0x08) | `0xc1ae89 "ObjectPositionUpdated"` |
| 9 (0x09) | `0xc18691 "PlacedOnCharger"` |
| 10 (0x0a) | `0xc1ae9f "PetInitialDetection"` |
| 11 (0x0b) | `0xc1aeb3 "RobotFalling"` |
| 12 (0x0c) | `0xc1aec0 "RobotPickedUp"` |
| 13 (0x0d) | `0xc1aece "RobotPlacedOnSlope"` |
| 14 (0x0e) | `0xc1aee1 "ReturnedToTreads"` |
| 15 (0x0f) | `0xc1aef2 "RobotOnBack"` |
| 16 (0x10) | `0xc1aefe "RobotOnFace"` |
| 17 (0x11) | `0xc1af0a "RobotOnSide"` |
| 18 (0x12) | `0xc1af16 "RobotShaken"` |
| 19 (0x13) | `0xc104a2 "Sparked"` |
| 20 (0x14) | `0xc1258b "UnexpectedMovement"` |
| 21 (0x15) | `0xc1966a "Count"` |
| 22 (0x16) | `0xc1af22 "NoneTrigger"` |

`ReactionTriggerFromString` (`0x00770674`) builds the same map independently: `0x007706a6 add r1,pc` -> `0xc1ae42 "CliffDetected"` with `0x007706c2 strb.w r6,[sp,#0xc]` (r6=0) gives `CliffDetected = 0`; `0x007708be add r1,pc` -> `0xc1258b "UnexpectedMovement"`; its value store is `0x007708e6 strb.w r0,[sp,#0x14c]` (r0=0x14), so `UnexpectedMovement = 0x14`. `Count` string load `0x007708da add r1,pc` -> `0xc1966a` with value store `0x00770902 strb.w r0,[sp,#0x15c]` (r0=0x15). `NoneTrigger` string load `0x007708f4 add r1,pc` -> `0xc1af22` with value store `0x00770914 strb.w r1,[sp,#0x16c]` (r1=0x16).

**Answer.** `0 = CliffDetected`, `0x14 = UnexpectedMovement`. `Count = 0x15` and `NoneTrigger = 0x16`. The gap-1 pass's labels (`0 = NoneTrigger`, `0x14 = Count`) are wrong; this also makes `Resume`'s `"TooManyResumesCliffOrMovement"` special path (`0x005bcf16 cmp r5,#0x14` / `0x005bcf1a cmpne r5,#0`, string at `0x5bd04c`) name `CliffDetected` and `UnexpectedMovement`, which is consistent.

**The table at `0x1031E20` is not the ReactionTrigger table.** It is the `MessageEngineToGame` tag-name table: entry 0 is `0xc12154 "UiDeviceConnected"`, 1 `0xc10569 "AudioCallback"`, 2 `0xc12166 "AdvertisementRegistrationMsg"`, ... 15 `0xc1226a "ObjectMoved"` ...; those names and ordinals match `re-analysis/protocol/MessageEngineToGame_tags.txt` exactly. The M10 report used it for `MessageEngineToGame` tag names in the reaction-strategy factory, which is correct for that purpose; the ReactionTrigger names come from `0x10337C0`.

| what the original does | citation (address + instruction) | classification |
|---|---|---|
| `EnumToString(ReactionTrigger)` indexes the name table at 0x10337c0, guarded to 0..0x16 | `0x0077065c cmp r0,#0x16`; `0x00770668 add r1,pc`; `0x0077066a ldr.w r0,[r1,r0,lsl #2]` | EXACT_SOURCE |
| entry 0 = "CliffDetected"; entry 0x14 = "UnexpectedMovement"; entry 0x15 = "Count"; entry 0x16 = "NoneTrigger" | table `0x10337c0`: `+0x00 -> 0xc1ae42`, `+0x50 -> 0xc1258b`, `+0x54 -> 0xc1966a`, `+0x58 -> 0xc1af22` | EXACT_SOURCE |
| `ReactionTriggerFromString` independently maps the same names to the same values | `0x007706a6`/`0x007706c2`; `0x007708be`/`0x007708e6`; `0x007708da`/`0x00770902`; `0x007708f4`/`0x00770914` | EXACT_SOURCE |
| `0x1031E20` is the `MessageEngineToGame` tag-name table, not ReactionTrigger | table `0x1031E20`: `+0x00 -> 0xc12154 "UiDeviceConnected"`, `+0x3c -> 0xc1226a "ObjectMoved"` | EXACT_SOURCE |

---

## Question 3 — what the `"Initializing %s: %zu actions already in queue"` number counts

**What the original does.** The warning is emitted once, from `0x005bcc36`, after the outer scan set the flag. Its strings and call:
- `0x005bcc7e adr r0,#0x164` -> `0x5bcde4 "IBehavior.Init.ActionsInQueue"`
- `0x005bcc82 adr r2,#0x180` -> `0x5bce04 "Initializing %s: %zu actions already in queue"`
- `0x005bcc84 blx sWarningF`; the behaviour-name string is in `r3` (`0x005bcc46 addeq.w r3,r8,#1` / `0x005bcc4a ldrne r3,[r4,#0x48]`), and the count is the first vararg on the stack (`0x005bcc7c str r0,[sp]`).

The count is produced by a fresh `find(0)` on the action map, not by the queue that triggered the warning:
- `0x005bcc4c ldr r2,[ip,#4]!` (ip = `[robot+0x250]` = `ActionList*`; now ip = &tree+4, r2 = root)
- `0x005bcc58 cmp r2,#0` / `0x005bcc5a bge #0x5bcc64` (search left for the first key >= 0)
- `0x005bcc74 ldr r1,[r0,#0x10]` / `0x005bcc76 cmp r1,#0` / `0x005bcc78 ble #0x5bcd64` (found only when the key == 0)
- if not found, `0x005bcc7a movs r0,#0` (count 0).

The count itself (`0x005bcd64`):
- `0x005bcd64 ldr r1,[r0,#0x14]` (queue's current action)
- `0x005bcd66 ldr r0,[r0,#0x20]` (queue's `std::list<IActionRunner*>` size; the list sentinel is at node+0x18, `__next_` at +0x1c, size at +0x20 — see `AddConcurrentAction` `0x0053dfb2 add.w r1,r5,#0x18` / `0x0053dfbe ldr r1,[r5,#0x20]` / `0x0053dfc2 adds r0,r1,#1` / `0x0053dfc4 str r0,[r5,#0x20]`)
- `0x005bcd68 cmp r1,#0` / `0x005bcd6a it ne` / `0x005bcd6c addne r0,#1`
- `0x005bcd6e b #0x5bcc7c` (pass the count)

Map key 0 is the main queue: `ActionList::QueueActionNow` (`0x0053dca0`) uses `0x0053dcb4 movs r0,#0` / `0x0053dcb8 str r0,[sp,#0xc]` as the emplace key (`0x0053dccc`), as do `QueueAction` (`0x0053daa4 str r5,[sp,#8]`, r5=0), `QueueActionNext`, `QueueActionAtEnd` and `QueueActionAtFront`. `ActionList::AddConcurrentAction` (`0x0053df2c`) uses keys `>= 1` (`0x0053df4a movs r2,#1` / `0x0053df50 str r2,[sp,#8]`, incremented per occupied slot), so the map can hold more than one queue.

**Answer.** The number is one queue's count: **the current action plus the queued actions of the main queue (map key 0)** — `list.size + (current != 0 ? 1 : 0)`. It is not the sum over all queues. Note the count is looked up for key 0 even when the flag was set by a concurrent queue (key >= 1), and it is 0 when key 0 does not exist.

| what the original does | citation (address + instruction) | classification |
|---|---|---|
| warning strings and call | `0x005bcc7e adr r0,#0x164`; `0x005bcc82 adr r2,#0x180`; `0x005bcc84 blx sWarningF`; `0x5bcde4 "IBehavior.Init.ActionsInQueue"`; `0x5bce04 "Initializing %s: %zu actions already in queue"` | EXACT_SOURCE |
| the count source is a fresh `find(0)` on the action map | `0x005bcc4c ldr r2,[ip,#4]!`; `0x005bcc58 cmp r2,#0`; `0x005bcc74 ldr r1,[r0,#0x10]`; `0x005bcc76 cmp r1,#0`; `0x005bcc78 ble #0x5bcd64` | EXACT_SOURCE |
| count = queue list size + 1 when a current action is set | `0x005bcd64 ldr r1,[r0,#0x14]`; `0x005bcd66 ldr r0,[r0,#0x20]`; `0x005bcd68 cmp r1,#0`; `0x005bcd6c addne r0,#1`; `0x005bcc7c str r0,[sp]` | EXACT_SOURCE |
| map key 0 is the main queue | `0x0053dcb4 movs r0,#0`; `0x0053dcb8 str r0,[sp,#0xc]`; `0x0053dccc blx __emplace_unique_key_args<int,...>` | EXACT_SOURCE |
| concurrent queues use keys >= 1, so "all queues" is a real alternative the code does not take | `0x0053df4a movs r2,#1`; `0x0053df50 str r2,[sp,#8]`; `0x0053df80 cmp r3,r0` / `0x0053df84 addne r2,#1` / `0x0053df86 strne r2,[sp,#8]` | EXACT_SOURCE |

---

## Existing records contradicted or weakened

- **`20260928-I-M8-gap1-extraction.md`, B5** labels `0x14 = Count` and `0 = NoneTrigger`. Contradicted: `0 = CliffDetected`, `0x14 = UnexpectedMovement`, `Count = 0x15`, `NoneTrigger = 0x16` (table `0x10337c0`; `ReactionTriggerFromString` `0x00770674`). The numeric control flow in B5 is unaffected, but the names in the record text must be corrected.
- **M8-001** (`EXACT_SOURCE`, "Behaviour lifecycle and the Smart* scope helpers"): its evidence still names only `IBehavior::IsRunnableBase 0x005BD778`, which does not cover `IBehavior::Init` (`0x005bcb54`) or the rest of the lifecycle. The gap-1 report already flagged this; the tag/guard facts above are additional uncovered behaviour. Status is too weak for the record as written (split or re-evidence).
- **M8-003** (`EXACT_SOURCE`, scored selection): its evidence omits the fifth JSON key `runningPenalty` and the `+0x104`/`+0x108`/`+0x110`/`+0x111` semantics (gap-1 finding). Not touched by this pass.
- **M10-derived** already carries the correct ReactionTrigger ordinals ("0 CliffDetected ... 20 UnexpectedMovement, 21 Count, 22 NoneTrigger (0x16)", `re-analysis/inventory/M10-derived.md` line 320) and correctly cites `EnumToString 0x77065C -> table 0x10337C0`; the M10 factory row's "tag names checked against the engine table 0x1031E20" is correct for `MessageEngineToGame` tags, not ReactionTrigger. No contradiction, but the two tables must not be conflated.

## Open questions for the manager

1. **Q1's guard is now exact but its intent is a design question.** The guard distinguishes engine-tagged actions from externally-tagged ones; whether the warning is *meant* to ignore externally-queued actions cannot be read from the instructions. If the record needs an intent statement, that is the manager's call, not the source's.
2. **Q3's concurrent-queue edge.** The warning can be triggered by a key >= 1 concurrent queue while the count is read from key 0 (possibly 0). Whether any shipped path queues a concurrent action before the main queue exists is not settled here; the counting rule itself is exact.
3. **M8-001's split.** The pass establishes `Init`'s tag/guard and the count, but M8-001's evidence is still only `IsRunnableBase`. Recommend splitting the record or adding the addresses listed in the gap-1 "too weak" section plus `0x005bcb54`, `0x005bcbe8`, `0x005bcc4c`/`0x005bcd64`.

## Appendix E: gap pass 3, extractor report

re-analysis/research/20260928-I-M8-gap3-extraction.md

# M8-framework gap pass 3 — four corrected rows

Agent: cozmo-extractor (read-only). Date: 2026-09-28.
Primary source: `resources/lib/armeabi-v7a/libcozmoEngine.so` (ELF ARMv7/Thumb). Every citation is an address in that file with the instruction at it. Raw addresses were disassembled with the existing scratch helper `.scratch/disasm_addr.py`, started before each IT block so capstone keeps the condition suffixes. `re-analysis/decomp/libcozmoEngine/` was not needed.

Result: the verifier is right on all four items. The three addresses were off by one instruction (A1), by one instruction (C3) and by six bytes into a 4-byte `add.w` (C4c); the enum names were wrong. The gap-2 mapping (`0 = CliffDetected`, `0x14 = UnexpectedMovement`, `0x15 = Count`, `0x16 = NoneTrigger`) is confirmed independently from the table and from `ReactionTriggerFromString`.

| step | what the original does | citation (address + instruction) | record | classification |
|---|---|---|---|---|
| A1 (corrected) | Row 5, current-behaviour class test against `0x2e`. `0x005a2fe0` is the compare (`0x005a2fde` is `movs r6,#0`), and the manager flag store is at `0x005a305c`, the third instruction of the `ittt eq` block that starts at `0x005a3056`; `0x005a305a` is `moveq r1,#1`, not the store. | `0x005a2fe0 cmp r7,#0x2e`; `0x005a2fd6 ldrb.w r7,[r7,#0x64]`; `0x005a3054 cmp r6,#1`; `0x005a3056 ittt eq`; `0x005a3058 ldreq r0,[r4,#0x30]`; `0x005a305a moveq r1,#1`; `0x005a305c strbeq.w r1,[r0,#0x220]` (gap-1 cited `0x005a305a strbeq.w`) | NEW (row 5) | EXACT_SOURCE |
| C3 (corrected) | `IBehavior::SmartDelegateToHelper`: on a successful `DelegateToHelper` the weak ref is taken by `blx __add_weak` at `0x005bec6e`; `0x005bec6c` is `mov r0,r5`. | `0x005bec66 ldrd r7,r5,[r5]`; `0x005bec6a cbz r5,#0x5bec72`; `0x005bec6c mov r0,r5`; `0x005bec6e blx #0x4a5020` (PLT `std::__ndk1::__shared_weak_count::__add_weak()`); `0x005bec72 str.w r7,[r4,#0xc4]`; `0x005bec7a str.w r5,[r4,#0xc8]` (gap-1 cited `0x005bec6c blx add_weak`) | M8-001 | EXACT_SOURCE |
| C4c (corrected) | `BehaviorManager::RemoveDisableReactionsLock`: the `__tree::find` on the trigger map is at `0x005a3aa8`; `0x005a3aae` is the second halfword of the 4-byte `add.w r1,r7,#0x24` at `0x005a3aac`, not a call. | `0x005a3a9e add.w r0,r7,#0x20`; `0x005a3aa4 ldrb.w fp,[r7,#0x10]`; `0x005a3aa8 blx #0x4b0da4` (PLT `std::__ndk1::__tree<...std::__ndk1::basic_string...>::find<...>(... ) const`); `0x005a3aac add.w r1,r7,#0x24`; `0x005a3ab0 cmp r0,r1`; `0x005a3ab2 beq #0x5a3b76` (gap-1 cited `0x005a3aae blx __tree::find`) | M8-001 / M7-014 | EXACT_SOURCE |
| B5 (corrected) | `IBehavior::Resume`: the two special triggers are `0x14 = UnexpectedMovement` and `0 = CliffDetected` (not `Count` / `NoneTrigger`). Numeric control flow unchanged: `cmp r5,#0x14` / `cmpne r5,#0` / `bne 0x5bcf7e`; for those two it reads `+0x114`, and only when the pre-increment value is `>= 1` does it set `+0x118 = now + 15.0`, build `"TooManyResumesCliffOrMovement"` and call `MoodManager::TriggerEmotionEvent`; the first such trigger (`+0x114 == 0`) still takes the normal resume path. The suppression window (`+0x118`) is consumed by `IsRunnableScored` (`0x005bda28`). | table `0x10337c0`: `+0x00 -> 0xc1ae42 "CliffDetected"`, `+0x50 -> 0xc1258b "UnexpectedMovement"`, `+0x54 -> 0xc1966a "Count"`, `+0x58 -> 0xc1af22 "NoneTrigger"`; `0x0077065c cmp r0,#0x16`; `0x00770668 add r1,pc`; `0x0077066a ldr.w r0,[r1,r0,lsl #2]`; `ReactionTriggerFromString 0x00770674`: `0x007706c2 strb.w r6,[sp,#0xc]` (r6=0, `CliffDetected`), `0x007708e6 strb.w r0,[sp,#0x14c]` (r0=0x14, `UnexpectedMovement`), `0x00770902 strb.w r0,[sp,#0x15c]` (r0=0x15, `Count`), `0x00770914 strb.w r1,[sp,#0x16c]` (r1=0x16, `NoneTrigger`); string `0x5bd04c "TooManyResumesCliffOrMovement"` (via `0x005bcf44 adr r1,#0x104` -> `0x005bd04c`); `0x005bcf16 cmp r5,#0x14`; `0x005bcf18 it ne`; `0x005bcf1a cmpne r5,#0` | M8-001 | EXACT_SOURCE |

## Notes on the B5 re-read

- `EnumToString(ReactionTrigger)` (`0x0077065c`) bounds-checks `r0 > 0x16` and otherwise indexes the pointer table at `0x10337c0`; I read the 23 entries directly from the file and they match the gap-2 table exactly.
- `ReactionTriggerFromString` (`0x00770674`) builds the map with a 23-element `initializer_list` of `{std::string, ReactionTrigger}` pairs (16 bytes each). The value byte for entry N is written at `sp+0xc+0x10*N` after the next string is loaded; the four addresses above are the stores for indices 0, 0x14, 0x15 and 0x16. This is independent of the `EnumToString` table and agrees with it.
- `0x5bd04c` is the string address reached by `adr r1,#0x104` at `0x005bcf44`; the `TooManyResumesCliffOrMovement` name is consistent with the corrected trigger names (`CliffDetected` + `UnexpectedMovement`).
- The gap-1 open question 2 ("names unverified") and gap-1 contradicted-record item 12 are now settled by this pass.

## Existing records contradicted by the source

1. **`20260928-I-M8-gap1-extraction.md`, A1** — cited `0x005a305a strbeq.w r1,[r0,#0x220]`. `0x005a305a` is `moveq r1,#1`; the store is `0x005a305c` (inside `ittt eq` at `0x005a3056`).
2. **`20260928-I-M8-gap1-extraction.md`, C3** — cited `0x005bec6c blx add_weak`. `0x005bec6c` is `mov r0,r5`; the call is `0x005bec6e`.
3. **`20260928-I-M8-gap1-extraction.md`, C4c** — cited `0x005a3aae blx __tree::find`. The call is `0x005a3aa8`; `0x005a3aae` is inside `add.w r1,r7,#0x24` at `0x005a3aac`.
4. **`20260928-I-M8-gap1-extraction.md`, B5** — labels `0x14 = Count` and `0 = NoneTrigger` are wrong; they are `0x14 = UnexpectedMovement` and `0 = CliffDetected` (`Count = 0x15`, `NoneTrigger = 0x16`).

## Existing records whose evidence is too weak to keep their status

None new in this pass. The M8-001/M8-003/M8-002/M8-009 evidence weakness flagged in gap 1 stands unchanged; these four corrections do not alter it.

## Open questions for the manager

None. All four items were resolvable from the primary source; no UNKNOWN remains. The only remaining task is to fold these four rows into the gap-1 report (or supersede it) and correct the M8-001 record text that used the old trigger names.

## Correction C1 (job B-M8, 2026-09-28): the gap pass 6 answers

The B-M8 build surfaced six `MISSING` questions; a read-only extractor pass answered them from the `.so` (report at `.scratch/B-M8-missing-extraction.md`). The rows that change are folded into the records M8-001, M8-002, M8-003, M8-011, M8-013 and M8-014, and this inventory is re-approved.

| question | answer | citation | record |
|---|---|---|---|
| Which stop path calls `StopWithoutImmediateRepetitionPenalty`? | None of `IBehavior::Stop`/`StopActing`/`FinishCurrentBehavior`. Its only three callers are `BehaviorPickUpCube::UpdateInternal`, `BehaviorStackBlocks::UpdateInternal` and `BehaviorBuildPyramidBase::UpdateInternal`, each on a "stop this behaviour" branch that then returns 2. Those are M7/M15 concrete behaviours. | `0x005c685c`/`0x005c6860`; `0x005c991c`/`0x005c9920`; `0x005dd110`/`0x005dd114`; PLT `0x004b3384` | M8-001, M8-002 |
| Which behaviours call `IncreaseScoreWhileActing`? | Ten call sites, all concrete behaviours (KnockOverCubes 10.0f, PopAWheelie/RollBlock/StackBlocks 0.8f, PutDownBlock 5.0f, and `FUN_0059ec54` which pairs it with `StartActing`). `ScoredActingStateChanged` (clears +0x104) has **no** engine caller. | `0x005c37a8`, `0x005c78d4`, `0x005c8084`, `0x005c8104`, `0x005c888e`, `0x005c8c08`, `0x005c95ca`, `0x005c9796`, `0x005c9f0c`, `0x0059ec6a`; `0x005bf044` | M8-001, M8-003 |
| `SelectionBSRunnableChooser::GetDesiredActiveBehavior` full algorithm? | +0x2c = the behaviour named by the last `ExecuteBehavior` message; +0x34 = `Wait` (BehaviorID 0xb2); +0x3c = the message's `numRuns` (ctor -1 = unlimited, one decrement per running->stopped edge); +0x40 = the previous call's running-state latch. Returns +0x2c while running/runnable and the budget is not spent, else +0x34 under the same rule (its countdown block only when +0x34 == +0x2c), else null. | `0x0060ad64`..`0x0060ae6f`: `0x0060ad6e`, `0x0060ad96`, `0x0060ada4`..`0x0060adc8`, `0x0060ade2`, `0x0060ae08`, `0x0060ae10`, `0x0060ae64`; ctor `0x0060a87c`/`0x0060a880`/`0x0060a884`; Wait `0x0060a988`/`0x0060a99a` | M8-013 |
| Which chooser consults `IsRunnableScored`? | None. `IsRunnableScored` `0x005bda28` has no engine caller and is not in the `IBehavior` vtable (0x010264e0). The selection chooser uses `IBehavior::IsRunnable` (PLT `0x004b0f54` at `0x0060ad96`/`0x0060ae08`). | `0x005bda28`; `.dynsym st_value 0x005bda29` | M8-001, M8-013 |
| `BehaviorHelperComponent::DelegateToHelper` and `[robot+0x264]+0x10`? | `robot+0x264` is the `AIComponent`; its +0x10 is a `BehaviorHelperComponent`. `DelegateToHelper` body `0x0056dad8..0x0056db37`: clears stack-maintenance vars, copies the two callbacks to +0x10/+0x28, and on an empty helper stack pushes the helper and stores `Robot::GetWorldOriginID` at +0x40, returning 1. No record owns the component or its helper-stack runtime. | `0x0050fda0`/`0x0050fda4`; `0x00569aa4`/`0x00569aae`/`0x00569ab2`; `0x0056dad8`..`0x0056db37`; `0x005bebee`/`0x005bebf8`/`0x005bec2c` | M8-011 |
| `AIWhiteboard::Init`'s three registrations and `UpdateBeaconRender`? | The three registrations are MessageEngineToGame subscriptions: tag 0x44=68 `RobotObservedObject`, 0x45=69 `RobotObservedPossibleObject`, 0x35=53 `RobotOffTreadsStateChanged` (records the time at whiteboard+0x48). `UpdateBeaconRender` `0x0056aa3c..0x0056abff` is a real body drawing the beacons through `VizManager` (EraseSegments, DrawXYCircleAsSegments). Neither the handlers nor `VizManager` is recorded by M11/M12. | `0x0056a3b8`/`0x0056a3be`/`0x0056a3c4`; `0x0056a44c`, `0x0056a50c`, `0x0056a5cc`; `0x0056aa3c`..`0x0056abff`; PLT `0x004acb20`, `0x004acb50` | M8-014 |

**Consequences for the build.** `StopWithoutImmediateRepetitionPenalty` and `IncreaseScoreWhileActing` are called by M7/M15 behaviours, so their call sites stay cross-layer gaps; the M8 records expose the functions but do not call them from `Stop`/the manager. `ScoredActingStateChanged` and `IsRunnableScored` have no engine caller and must not be given one. `SmartDelegateToHelper`'s callee and the whiteboard handlers/render are unowned, so M8-011 and M8-014 cannot settle their whole path until the integrator places those records.

## Build outcome (job B-M8, 2026-09-28)

Built from the frozen inventory and verified by `@cozmo-verifier` (PASS; full suite 1721/1721; `fidelity.py --check` clean).

**Settled EXACT_SOURCE:** M8-001 (lifecycle), M8-002 (repetition graph and the suppression window, applied at the scoring site), M8-003 (scored selection, running bonus/penalties, objective), M8-005 (ordered multi-trigger sequence and `num_loops`), M8-006 (the needs wants-to-run strategies), M8-007 (the per-action track-mask lock, enforced on the motion component with a synchronous release), M8-012 (the manager tick order, switching and finishing).

**Still IMPLEMENTATION_GAP, cross-layer/unowned:** M8-011 (the `SmartDelegateToHelper` callee `BehaviorHelperComponent::DelegateToHelper` 0x0056dad8 and its helper-stack runtime are unowned; the reaction-lock manager side is M7-014), M8-013 (`SelectionChooser.RequestBehavior` has no production caller: `SelectionBSRunnableChooser::HandleExecuteBehavior` 0x0060ac2c and the `ExecuteBehavior` message are M2/M10; the concrete activity bodies are M7/M15), M8-014 (`AIWhiteboard::UpdateBeaconRender` 0x0056aa3c and the three handler bodies are unowned; `VizManager`/M11/M12 do not record them).

**Seams kept, cross-layer:** the AI-process spark inputs and progression/unlock inputs of `IsRunnableBase`; the M7/M15 activity tick (`vtable+0x20`); the DAS transition send, `EnsureRequestGameIsClear` and the `InitFailed` signal; the M10 reaction strategies.

**For the integrator:** the unowned components above need records before their layers can settle M8-011/M8-013/M8-014 — `BehaviorHelperComponent` (AIComponent+0x10), `AIWhiteboard::UpdateBeaconRender` and its three MessageEngineToGame handlers (tags 68/69/53), and `SelectionBSRunnableChooser::HandleExecuteBehavior`.
