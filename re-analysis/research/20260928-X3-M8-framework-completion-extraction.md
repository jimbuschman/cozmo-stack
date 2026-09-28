# Job X3 - M8-framework extraction, completion pass

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
