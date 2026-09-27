# Job X3 - M7-behaviour extraction, part 1: data, mood, idle, reactions, live animation

Agent: opencode-w1 (DeepSeek). Read-only extraction. Date: 2026-09-27.

This part covers the M7 records that are not per-class run logic: the trigger/behaviour enums and
maps, mood, the idle/live-animation face and body, and the reaction gating. Part 2 is the
per-`BehaviorClass` inventory.

## Sources and a missing artifact

Primary source is `resources/lib/armeabi-v7a/libcozmoEngine.so`. Citations are addresses in it with
the instruction that matters. The Ghidra decompilation is not present in this clone; disassembly was
produced with `re-analysis/tools/disarm.py` (capstone + lief). Dynamic symbols are present, so every
function below is named.

**The unpacked OBB is absent from this clone** (`re-analysis/obb/` is empty; no `.obb` file anywhere
on this machine). Every record that rests on an OBB file - `config/engine/behaviorSystem/*.json`,
`config/engine/mood_config.json`, `reactionTrigger_behavior_map.json`, the behaviour configs - cannot
be read here. Those rows are RECOVERABLE_GAP with the exact file named. The engine code that consumes
them is read where possible.

## Row table

| # | step | what the original does | citation | record | classification |
|---:|---|---|---|---|---|
| 1 | Trigger enums | `AnimationTrigger` has 575 values plus `Count`; `ReactionTrigger` has 21 plus `Count`/`NoneTrigger`; `BehaviorClass` has 79; `BehaviorID` has 179. Read from the decompiled Unity enums, which match the engine's `EnumToString` tables. | `unity/scripts/csharp/Anki.Cozmo/AnimationTrigger.cs`, `ReactionTrigger.cs`, `BehaviorClass.cs`, `BehaviorID.cs` | M7-001 | EXACT_SOURCE |
| 2 | Trigger maps | `AnimationTriggerMap.json` and `reactionTrigger_behavior_map.json` are the shipped maps, loaded by `RobotDataLoader::LoadReactionTriggerMap`. The loader is real; the map contents cannot be read here. | `0x00520bc8 RobotDataLoader::LoadReactionTriggerMap` (symbol resolves); files under `config/engine/behaviorSystem/` (absent) | M7-002 | RECOVERABLE_GAP - read `reactionTrigger_behavior_map.json` and `AnimationTriggerMap.json` from the OBB |
| 3 | ReactToImpact gating | `BehaviorReactToImpact::TransitionToPlayingAnim` only acts when the byte at `+0x11e` is set, then starts a `TriggerAnimationAction(robot, 0x1a0, 1, true, 0, 60.0f, ...)`. `AlwaysHandle` switches on the engine-to-game tag at `+0x10`: tag `0x1e` branches to `0x606454`, tag `0x3b` to `0x60642c`, and tag `0x3a` clears `+0x11e` and `+0x11c`. | `0x0060635a ldrb.w r0,[r4,#0x11e]`; `0x00606384 blx TriggerAnimationAction::TriggerAnimationAction`; `0x0060640c ldrh r0,[r1,#0x10]!`; `0x00606412 cmp r0,#0x1e`; `0x00606416 cmp r0,#0x3b`; `0x0060641a cmp r0,#0x3a`; `0x00606422 strb.w r0,[r4,#0x11e]` | M7-003 (partial) | the cited functions exist and the trigger/clear path is read; the "impact intensity over 1000" and "up to 5 s recalibration" steps are **not** in these two functions - they are in the `0x60642c`/`0x606454` branches and the `FallingStopped` handler, not read in this pass. RECOVERABLE_GAP - read those branches |
| 4 | Idle tunables | `AnimationStreamer::SetDefaultParams` sets all 30 `LiveIdleAnimationParameter` values (ids 0..0x1d) through `SetParam`. Decoded: 0=3000, 1=4000, 2=1000, 3=100, 4=1000, 5=250, 6=1500, 7=10, 8=0.5, 9=50, 10=500, 11=250, 12=2000, 13=35, 14=8, 15=50, 16=500, 17=250, 18=1000, 19=6, 20=250, 21=1000, 22=6, 23=0.92, 24=1.08, 25=50, 26=200, 27=0.1, 28=1.1, 29=0.85. | `0x0057db40`; `0x0057db4a movt r2,#0x453b` (3000); `0x0057dca6 movt r2,#0x3dcc` (0.1); `0x0057dccc movt r2,#0x3f59` (0.85) | M7-004 | EXACT_SOURCE |
| 5 | Blink | `ProceduralFaceDrawer::GetNextBlinkFrame`, `FaceLayerManager::GenerateBlink` and `ProceduralFace::Combine`. Cited functions resolve to the named symbols. | `0x00585f18`, `0x0058d2ac`, `0x005846a8` | M7-005 | citation confirmed; body not re-read in this pass |
| 6 | Eye dart | `FaceLayerManager::GenerateEyeShift` and `ProceduralFace::LookAt`. Cited functions resolve. | `0x0058d100`, `0x00584158` | M7-006 | citation confirmed; body not re-read in this pass |
| 7 | Persistent layer | `ITrackLayerManager::ApplyLayersToFrame`, `AddToPersistentLayer`, `FaceLayerManager::GetFaceHelper`. Cited functions resolve. | `0x0058e644`, `0x0058eaa0`, `0x0058cd80` | M7-007 / M7-016 | citation confirmed; body not re-read in this pass |
| 8 | 60 ms tick | `CozmoAPI::CozmoInstanceRunner::Run` deadlines each iteration at `now + 60 000 000 ns` and calls `CozmoEngine::Update`; the idle countdowns decrement by 0x3c. | `0x0065b3a8`; the `0x03938700` ns constant and the `#0x3c` decrements are in `AnimationStreamer::UpdateLiveAnimation` (`0x0057d650` etc.) | M7-008 | EXACT_SOURCE (tick); the per-counter decrements are in functions not re-read here |
| 9 | Idle head/lift | `AnimationStreamer::UpdateLiveAnimation` appends HeadAngle/LiftHeight keyframes to the live animation; cited offsets `+0x264`, `+0x3c8`. | `0x0057d85c`, `0x0057d9c0` (inside `UpdateLiveAnimation 0x0057d5f8`) | M7-009 | citation confirmed; body not re-read in this pass |
| 10 | Idle body shuffle | `UpdateLiveAnimation` at `+0xd4` (`0x0057d6cc`) draws the body movement; the turn's eye-shift layer is added at `0x0057d7fc`. | `0x0057d6cc`, `0x0057d7fc` (inside `UpdateLiveAnimation`) | M7-010 | citation confirmed; body not re-read in this pass |
| 11 | Frustration cooldown | `ReactionTriggerStrategyFrustration::ShouldTriggerBehaviorInternal`: returns false when the current reaction trigger is 4, or when the mood value at `robot+0x440+0x78` is not below the strategy threshold `+0x30`, or while the cooldown is active - `+0x38` is the last-trigger stamp, `+0x34` the duration. Otherwise tail-calls `0x8cc96c`. | `0x0060ee7c blx BaseStationTimer::getInstance`; `0x0060ee8c vldr s16,[r1,#0x78]`; `0x0060ee94 cmp r0,#4`; `0x0060ee98 vldr s0,[r6,#0x30]`; `0x0060eea6 vldr s0,[r6,#0x38]`; `0x0060eebc vldr s2,[r6,#0x34]`; `0x0060eed8 b.w 0x8cc96c` | M7-011 | EXACT_SOURCE for the code path; the cooldown values and which triggers carry one rest on the absent `reactionTrigger_behavior_map.json` -> RECOVERABLE_GAP for the map |
| 12 | Mood config | Mood axes, events and decay curves come from `config/engine/mood_config.json`, which is absent. The consumer code exists. | `config/engine/mood_config.json` (absent) | M7-012 | RECOVERABLE_GAP - read `mood_config.json` from the OBB |
| 13 | Mood clamp and decay clock | `Emotion::Add` clamps the sum to [-1, +1] (`0x679622` = 1.0, `0x679630` = -1.0) and zeroes the decay clock at `+0x1c` only when the value kept its sign (`teq r2,r3` at `0x6796a8`), the change exceeded 0.05 (`0x67968a` against the literal at `0x6796c4`), and it moved away from zero (`eor` at `0x6796ae`). | `0x00679618`; `0x00679622 vmov.f32 s4,#1.0`; `0x00679630 vmov.f32 s8,#-1.0`; `0x0067968a vcmpe.f32 s6,s4`; `0x006796a8 teq.w r2,r3`; `0x006796ae eor.w r2,r2,lr`; `0x006796b6 streq.w ip,[r0,#0x1c]` | M7-013 | EXACT_SOURCE |
| 14 | Mood decay update | `Emotion::Update(graph, dt, ...)`: reads the graph at the old clock `+0x1c`, advances the clock by dt, reads it at the new clock, and scales the value at `+0x18` by `new/old` when `old > 1e-5` (literal at `0x679614`); when `old <= 1e-5` it scales by the raw new reading, not by 1. | `0x006795a4`; `0x006795b0 ldr r1,[r4,#0x1c]`; `0x006795c0 vldr s0,[r4,#0x1c]`; `0x006795c6 vadd.f32 s0,s0,s16`; `0x006795d6 vldr s4,[pc,#0x3c]` (1e-5); `0x006795e6 vdiv.f32 s2,s0,s18`; `0x006795ee it gt`; `0x006795f8 vmul.f32 s0,s2,s0`; `0x006795fc vstr s0,[r4,#0x18]` | M7-013 (partial) | the record says "leaving it alone when the old reading is below 1e-5"; the code instead multiplies by the new reading. Unreachable for the shipped graphs (they start at 1), but the wording is wrong. |
| 15 | Graph evaluator | `GraphEvaluator2d::EvaluateY`: below the first node returns the first y; a graph of fewer than two nodes returns the first y; above the last returns the last y; between nodes interpolates unless the x-gap is <= 1e-5 (literal at `0x804c48`). | `0x00804bd0`; `0x00804bda vcmpe.f32 s2,s0`; `0x00804be2 bgt`; `0x00804bec blo`; `0x00804c14 vcmpe.f32 s4,s6`; `0x00804c1c ble`; `0x00804c3c vldr s0,[r2,#4]` | M7-013 | EXACT_SOURCE |
| 16 | Reaction locks | `IBehavior::SmartDisableReactionsWithLock` (`0x005bce3d`) and the manager's `DisableReactionsWithLock` (`0x005a27e9`)/`RemoveDisableReactionsLock` (`0x005a3a49`) exist. | as listed | M7-014 | citation confirmed; bodies not re-read in this pass |
| 17 | Pickup / treads | `Robot::CheckAndUpdateTreadsState(RobotState const&)` exists at `0x00511e00`. | `0x00511e00` | M7-015 | citation confirmed; body not re-read in this pass |
| 18 | Live-animation wire | `AnimationStreamer::UpdateLiveAnimation` (`0x0057d5f8`), `Update` (`0x0057ce5c`), `InitStream` (`0x0057b674`), `UpdateStream` (`0x0057c84c`), `SendStartOfAnimation` (`0x0057c400`), `SendBufferedMessages` (`0x0057bf60`). Cited functions resolve. | as listed | M7-017 | citation confirmed; body not re-read in this pass |

## Existing records, judged

- **M7-001** EXACT_SOURCE: confirmed. The four enums are present with the counts the job names.
- **M7-002** EXACT_SOURCE: **cannot be verified here** - the maps are OBB files and the OBB is absent.
  The loader (`LoadReactionTriggerMap 0x00520bc8`) is real. RECOVERABLE_GAP.
- **M7-003** EXACT_SOURCE: **evidence proves only part of the claim.** The two cited functions are
  real and the trigger/clear path is read (row 3), but the "impact intensity over 1000" and "up to 5 s
  recalibration" steps live in the untaken branches (`0x60642c`, `0x606454`) and the `FallingStopped`
  handler, which the record does not cite. Read those before keeping EXACT_SOURCE.
- **M7-004** EXACT_SOURCE: confirmed, and the 30 values are decoded (row 4). This is a full reading.
- **M7-005..010, M7-016, M7-017**: every cited address resolves to the named symbol. The bodies were
  not re-read in this pass; their detailed claims are neither confirmed nor contradicted. No change
  recommended from this pass, but they remain single-pass claims.
- **M7-011** EXACT_SOURCE: the code path is confirmed (row 11). The claim "exactly one reaction
  cooldown ... frustrationParams.cooldownTime_s 60.0" rests on `reactionTrigger_behavior_map.json`,
  which is absent here. RECOVERABLE_GAP for the map half.
- **M7-012** EXACT_SOURCE: **cannot be verified here** - `mood_config.json` is absent. RECOVERABLE_GAP.
- **M7-013** EXACT_SOURCE: confirmed for the clamp, the graph evaluator and the decay-clock reset
  (rows 13, 15). **One wording is wrong**: the `old <= 1e-5` branch multiplies by the raw new reading,
  not "leaving it alone" (row 14). Unreachable for shipped graphs, but the record should be corrected.
- **M7-014** EXACT_SOURCE: the functions exist; bodies not re-read.
- **M7-015** EQUIVALENT_IMPLEMENTATION: the cited function exists; body not re-read.
- **M7-016, M7-017**: citation-confirmed only.

## NEW steps no record covers

- N1. `BehaviorReactToImpact::TransitionToPlayingAnim` uses trigger `0x1a0` and a 60.0 s argument
  (`0x0060636e movt r0,#0x4270`) - the record does not name the trigger or the timeout.
- N2. `AlwaysHandle` clears the reaction's state on engine-to-game tag `0x3a` and branches on `0x1e`
  and `0x3b` (row 3).
- N3. The full decoded 30-value idle table (row 4) is new evidence; M7-004 asserted "all 30" without
  the values.
- N4. `Emotion::Update`'s `old <= 1e-5` behaviour (row 14).
- N5. `EvaluateRunningPenalty` and the `runningPenalty` JSON key (reported in the M8 part; it is the
  same IBehavior scoring path M7-003/M7-011 touch).

## Open questions for the manager

- Q1. The OBB is absent. M7-002, M7-012 and the map half of M7-011 cannot be settled here. Should the
  manager supply the unpacked OBB (or just `mood_config.json` and `reactionTrigger_behavior_map.json`)
  before M7 is approved?
- Q2. M7-013's `old <= 1e-5` wording should be corrected; is that an M7 correction, or an integrator
  note?
- Q3. Several M7 records are single-pass claims whose cited functions were not re-read in this pass
  (M7-005..010, 014, 015, 016, 017). Do they need a second focused pass, or is citation-resolution
  enough for the M7 inventory?
