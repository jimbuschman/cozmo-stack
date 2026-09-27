# Job X3 - M8-framework extraction

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
