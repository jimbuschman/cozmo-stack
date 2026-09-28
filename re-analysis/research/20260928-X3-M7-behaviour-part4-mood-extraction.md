# Job X3 - M7-behaviour extraction, part 4: mood and the idle/keep-alive interface

Agent: opencode-w1 (DeepSeek). Read-only extraction. Date: 2026-09-28.

This part resumes X3 after the unpacked OBB was added. It reads the mood system end to end from the
shipped `mood_config.json` and the engine's `MoodManager`/`StaticMoodData`/`Emotion`/`EmotionEvent`,
and names the idle/keep-alive interface to M5. Per-class run logic is part 5.

## Sources

- `re-analysis/obb/assets/cozmo_resources/config/engine/mood_config.json` (now present; the engine's
  path string is `config/engine/mood_config.json` at `0x523130`).
- `resources/lib/armeabi-v7a/libcozmoEngine.so`: `MoodManager`, `StaticMoodData`, `Emotion`,
  `EmotionEvent`, `EmotionAffector`, `MoodScorer`.

## The shipped mood config

`mood_config.json` has four parts:

- `decayGraphs` - four entries. `default` has nodes `(0,1) (10,1) (30,0.9) (75,0.6) (150,0)`;
  `WantToPlay` has only `(0,1)` (no decay); `Social` has `(0,1) (10,1) (20,0.6) (60,0.2) (100,0)`;
  `Confident` has `(0,1) (30,1) (60,0.5) (70,0)`. Every emotion without its own entry falls back to
  `default` (`0x67d0ca` VerifyDecayGraph, `0x67d0e2` SetDecayGraph for each unset type 0..8).
- `eventMapper.emotionEvents` - **empty**. So no emotion event is defined in this config; every
  `TriggerEmotionEvent` name not found in the map adds nothing.
- `defaultRepetitionPenalty` - `(0.0,0.0) (30.0,1.0)`.
- `actionResultEmotionEvents` - 12 action types, each mapping a result category to an event name:
  `DRIVE_TO_POSE`/`DRIVE_TO_OBJECT`/`DRIVE_STRAIGHT`/`DRIVE_TO_PLACE_CARRIED_OBJECT`/`DRIVE_TO_FLIP_BLOCK_POSE`
  map `RETRY -> DrivingActionFailedWithRetry` and `ABORT -> DrivingActionFailedWithAbort`;
  `PICK_AND_PLACE_INCOMPLETE`/`PICKUP_OBJECT_LOW`/`PICKUP_OBJECT_HIGH` map `SUCCESS -> PickupSucceeded`,
  `RETRY -> PickingOrPlacingActionFailedWithRetry`, `ABORT -> PickingOrPlacingActionFailedWithAbort`;
  `PLACE_OBJECT_HIGH` maps `SUCCESS -> StackSucceeded` plus the two failures; `ROLL_OBJECT_LOW` maps
  the two failures only (comment: no success anim, the helper handles it); `POP_A_WHEELIE` maps
  `SUCCESS -> RollSucceeded` plus the two failures; `FLIP_BLOCK` maps `SUCCESS -> FlipBlockSucceeded`.

## Row table

| # | step | what the original does | citation | record | classification |
|---:|---|---|---|---|---|
| 1 | Mood config loaded | `MoodManager::Init(config)` calls `StaticMoodData::Init(config)` (singleton at `0x105bfa8`), then `LoadActionCompletedEventMap(config["actionResultEmotionEvents"])`, then registers `HandleActionEnded` on the action list through `ActionList::RegisterActionEndedCallbackForAllActions` and stores the handle at `+0x158`. | `0x0067aebc`; `0x0067aed4 blx StaticMoodData::Init`; `0x0067aee4 blx LoadActionCompletedEventMap`; `0x0067aef0 blx Robot::HasExternalInterface`; `0x0067af34 blx ActionList::RegisterActionEndedCallbackForAllActions`; `0x0067af38 str.w r0,[r4,#0x158]` | M7-012 | EXACT_SOURCE |
| 2 | Static mood data parsed | `StaticMoodData::ReadFromJson` clears the event mapper and the default penalty graph, reads `decayGraphs` (warns `"Missing 'decayGraphs' entry"` when null), for each entry reads the graph, and if `emotionType != "default"` resolves it through `EmotionTypeFromString` and `SetDecayGraph`; an unknown type warns `"DecayGraph %u failed to read - unknown type name '%s'"`. Any unset type then gets the `default` graph. | `0x0067ce6c`; `0x0067ce9c add r1,pc ; r1=0xbff689 "decayGraphs"`; `0x0067cf52 blx GraphEvaluator2d::ReadFromJson`; `0x0067cf82 addw r1,pc,#0x440 ; r1=0x67d3c4 "default"`; `0x0067cfb0 blx EmotionTypeFromString`; `0x0067cfd2 blx SetDecayGraph`; `0x0067d0e2 blx SetDecayGraph` | M7-012 | EXACT_SOURCE |
| 3 | Event mapper + default penalty | Reads `eventMapper` through `EmotionEventMapper::ReadFromJson` (warns `"EventMapper '%s' failed to read"` when it fails), then `defaultRepetitionPenalty` through `GraphEvaluator2d::ReadFromJson`; if that fails it warns `"'%s' failed to read"`, and if the graph is still empty it warns `"'%s' missing or bad - defaulting to no penalty"` and calls `GraphEvaluator2d::AddNode`. | `0x0067d0ee add r1,pc ; r1=0xbff695 "eventMapper"`; `0x0067d108 blx EmotionEventMapper::ReadFromJson`; `0x0067d112 add r1,pc ; r1=0xbff6a1 "defaultRepetitionPenalty"`; `0x0067d128 blx GraphEvaluator2d::ReadFromJson`; `0x0067d174 add r0,pc ; r0=0xbff64f "StaticMoodData.ReadFromJson.EmptyDefaultRepetitionPenalty"`; `0x0067d1b4 blx GraphEvaluator2d::AddNode` | M7-012 / M8-002 | EXACT_SOURCE |
| 4 | Action-completed map | `LoadActionCompletedEventMap` walks `actionResultEmotionEvents`: the outer key is a `RobotActionType` (`RobotActionTypeFromString`), the inner key an `ActionResultCategory` (`ActionResultCategoryFromString`), the value the event name. It stores `(actionType, category) -> eventName` in a map at `+0x134` and logs `"Loaded mood reactions for %zu (actionType, resultCategory) pairs"` on channel `Mood`. | `0x0067afb4`; `0x0067b026 blx RobotActionTypeFromString`; `0x0067b0a0 blx ActionResultCategoryFromString`; `0x0067b14e blx __tree::__emplace_unique_key_args`; `0x0067b1a0 sChanneledInfoF "Loaded mood reactions for %zu (actionType, resultCategory) pairs"` | NEW | EXACT_SOURCE |
| 5 | Action ended -> event | `HandleActionEnded(action)`: if the action id is in the "mood event on completion disabled" set at `+0x140`, it erases the id and returns (no event). Otherwise it looks up `(actionType, resultCategory)` in the map at `+0x134`; on a hit it logs `"Reacting to action of type '%s' completion with category '%s' by triggering event '%s'"` on channel `Mood` and calls `TriggerEmotionEvent(eventName, BaseStationTimer::GetCurrentTimeInSeconds())`. | `0x0067b318`; `0x0067b344 blx __tree::find`; `0x0067b3aa blx __tree::find`; `0x0067b3ec blx sChanneledDebugF`; `0x0067b40a blx TriggerEmotionEvent` | NEW | EXACT_SOURCE |
| 6 | Event -> emotion affector | `TriggerEmotionEvent(name, now)`: `EmotionEventMapper::FindEvent(name)`; a miss returns 0. On a hit it logs, computes the elapsed time via `UpdateLatestEventTimeAndGetTimeElapsedInSeconds(name, now)`, calls `EmotionEvent::CalculateRepetitionPenalty(elapsed)`, then walks the event's affector vector at `+0`: each 8-byte affector is `{EmotionType, float value}`, and it calls `Emotion::Add(type, penalty * value)` for each. | `0x0067b85c`; `0x0067b874 blx FindEvent`; `0x0067b8d8 blx UpdateLatestEventTimeAndGetTimeElapsedInSeconds`; `0x0067b8e0 blx EmotionEvent::CalculateRepetitionPenalty`; `0x0067b8e4 ldrd r5,r4,[r4]`; `0x0067b8f0 vldr s0,[r5,#4]`; `0x0067b8f4 ldrb r0,[r5]`; `0x0067b8f6 vmul.f32 s0,s16,s0`; `0x0067b902 blx Emotion::Add`; `0x0067b906 adds r5,#8` | M7-012 | EXACT_SOURCE |
| 7 | Affector schema | `EmotionAffector::ReadFromJson` reads `emotionType` (a string resolved by `EmotionTypeFromString`; unknown/absent becomes type 9 and warns) and `value` (`asFloat`). `EmotionEvent::ReadFromJson` reads `name` and `emotionAffectors` (warns `"Missing '%s' entry"` / `"EmotionEvent.ReadFromJson.MissingValue"` when absent) and clears its repetition graph at `+0x18`. | `0x00679910`; `0x0067991c add r1,pc ; r1=0xbff1e0 "emotionType"`; `0x00679928 add r1,pc ; r1=0xbff1ec "value"`; `0x00679964 blx EmotionTypeFromString`; `0x006799e8 blx Json::Value::asFloat`; `0x00679bc0`; `0x00679c10 add r1,pc ; r1=0xbff2a8 "name"`; `0x00679c38 add r1,pc ; r1=0xbff2ad "emotionAffectors"`; `0x00679c58 add r3,pc ; r3=0xbff2ad "emotionAffectors"` | M7-012 | EXACT_SOURCE |
| 8 | Event repetition penalty | `EmotionEvent::CalculateRepetitionPenalty(elapsed)` tail-calls `GraphEvaluator2d::EvaluateY` on the event's graph at `+0x18`. | `0x00679bb8 adds r0,#0x18`; `0x00679bba b.w 0x8cbf9c` | M7-012 | EXACT_SOURCE |
| 9 | Last-event clock | `UpdateLatestEventTimeAndGetTimeElapsedInSeconds(name, now)` emplace-inserts `name -> now` into the map at `+0x120`; if the key is new it returns `FLT_MAX` (`3.4028235e38`), otherwise it returns `now - oldTime` and stores `now`. So the first trigger of an event has no repetition penalty. | `0x0067be48`; `0x0067be80 add.w r1,r6,#0x120`; `0x0067be8a blx __tree::__emplace_unique_key_args`; `0x0067bea4 vldr s0,[pc,#0x30]`; `0x0067beaa vldr s0,[r4,#0x1c]`; `0x0067beae vstr s16,[r4,#0x1c]`; `0x0067beb2 vsub.f32 s0,s16,s0` | NEW | EXACT_SOURCE |
| 10 | Penalty selection | `UpdateEventTimeAndCalculateRepetitionPenalty(name, now)`: elapsed = the call above; if `FindEvent(name)` hits, use the event's own graph (`EmotionEvent::CalculateRepetitionPenalty`); otherwise evaluate the `defaultRepetitionPenalty` graph at `+0x78`. | `0x0067bedc`; `0x0067bee0 blx UpdateLatestEventTimeAndGetTimeElapsedInSeconds`; `0x0067beee blx FindEvent`; `0x0067befa b.w EmotionEvent::CalculateRepetitionPenalty`; `0x0067bf04 adds r0,#0x78`; `0x0067bf0a b.w 0x8cbf9c` | M8-002 | EXACT_SOURCE |
| 11 | AddToEmotion | `AddToEmotion(type, value, name, now)`: elapsed/penalty = `UpdateEventTimeAndCalculateRepetitionPenalty(name, now)`; then `Emotion::Add(Emotion at this + type*0x20, penalty * value)`. | `0x0067bdca`; `0x0067bdfc blx UpdateEventTimeAndCalculateRepetitionPenalty`; `0x0067be16 vmul.f32 s0,s18,s16`; `0x0067be1a add.w r0,r5,r8,lsl #5`; `0x0067be22 blx Emotion::Add` | M7-012 | EXACT_SOURCE |
| 12 | Mood update | `MoodManager::Update(dt)` clamps/derives the step from the wall clock (`+0x130` last time, `0x3f1a36e2` = 1e-4 warning floor), then loops the **nine** emotion types (0..8), calling `Emotion::Update(StaticMoodData::GetDecayGraph(type), dt, step)` on each `Emotion` at `this + type*0x20`, and finally `SendEmotionsToGame()`. | `0x0067b5d4`; `0x0067b5e6 vldr s0,[r8,#0x130]`; `0x0067b622 movw r0,#0x36e2` / `0x0067b62a movt r0,#0x3f1a`; `0x0067b682 uxtb r1,r5`; `0x0067b686 blx StaticMoodData::GetDecayGraph`; `0x0067b696 blx Emotion::Update`; `0x0067b69a adds r5,#1`; `0x0067b69c adds r4,#0x20`; `0x0067b69e cmp r5,#9`; `0x0067b6a4 blx SendEmotionsToGame` | M7-012 / M7-013 | EXACT_SOURCE |
| 13 | Decay graph table | `StaticMoodData::GetDecayGraph(type)` returns `this + type*12`; the nine graphs are 12 bytes apart. | `0x0067d714 add.w r1,r1,r1,lsl #1`; `0x0067d718 add.w r0,r0,r1,lsl #2`; `0x0067d71c bx lr` | NEW | EXACT_SOURCE |
| 14 | Emotion clamp and clock | (From the earlier pass, re-confirmed here.) `Emotion::Add` clamps to `[-1,+1]` and resets the decay clock `+0x1c` only when the value kept its sign, the change exceeded 0.05 and it moved away from zero. `Emotion::Update` reads the graph at the old clock, advances by dt, reads at the new clock and scales `+0x18` by `new/old` when `old > 1e-5`. | `0x00679618`; `0x00679622 vmov.f32 s4,#1.0`; `0x00679630 vmov.f32 s8,#-1.0`; `0x006796a8 teq.w r2,r3`; `0x006795a4`; `0x006795d6 vldr s4,[pc,#0x3c]` (1e-5); `0x006795e6 vdiv.f32 s2,s0,s18`; `0x006795fc vstr s0,[r4,#0x18]` | M7-013 | EXACT_SOURCE |
| 15 | Mood -> game | `SendEmotionsToGame` (the last step of `MoodManager::Update`) sends the nine emotion values out to the app. Its body was not re-read in this pass. | `0x0067b724`; call site `0x0067b6a4` | NEW | RECOVERABLE_GAP - read `0x0067b724` |
| 16 | Idle/keep-alive interface to M5 | The idle system is the M5 `AnimationStreamer`'s idle path: `SetDefaultParams` (`0x0057db40`, 30 tunables, M7-004), `UpdateLiveAnimation` (`0x0057d5f8`, the 60 ms counters and head/lift keyframes, M7-008/009/010), `PushIdleAnimation`/`RemoveIdleAnimation` (`0x4ad2f4`/`0x4a92a4`, driven by `IBehavior::SmartPushIdleAnimation`/`SmartRemoveIdleAnimation`, rows 9-10 of the M8 completion report), and the persistent face layers (`AddToPersistentLayer` `0x0058eaa0`, M7-007/016). The keep-alive face stream itself is M5's (a `FaceImage` every 33 ms). | as listed | M7-004 / M7-008 / M7-009 / M7-010 / M7-016 / M7-017 | interface only; bodies owned by M5 |

## Existing records, judged

- **M7-012** (EXACT_SOURCE, "Mood axes, events and decay curves"): **now fully confirmed.** The shipped
  `mood_config.json` is read; `StaticMoodData::ReadFromJson` parses `decayGraphs`, `eventMapper` and
  `defaultRepetitionPenalty`; `MoodManager::Update` advances the nine emotions against their graphs.
  Add the code addresses above.
- **M7-013** (EXACT_SOURCE, "the clamp, the decay curve outside its nodes, and what restarts the decay
  clock"): confirmed for the clamp and the graph evaluator. **One wording is wrong** (carried from the
  earlier pass): the `old <= 1e-5` branch multiplies by the raw new reading, not "leaving it alone"
  (`0x006795f8 vmul.f32 s0,s2,s0` when the compare is `gt`). Unreachable for the shipped graphs (they
  start at y=1, so old is 1), but correct the record.
- **M8-002** (EXACT_SOURCE, repetition penalty graph): **now fully confirmed**, both the per-behaviour
  graph at `+0xe8` and the mood system's `defaultRepetitionPenalty` at `StaticMoodData+0x78`
  (row 10), plus the per-event graph (`EmotionEvent+0x18`, row 8).
- **M7-011** (reaction cooldowns): the frustration cooldown is confirmed from the map (part 3) and the
  strategy code; the mood-event repetition penalty is a separate mechanism (rows 9-10) and should not be
  conflated with it.

## NEW steps no record covers

- N1. `actionResultEmotionEvents`: the 12 action-type/result-category -> event-name mappings and
  `HandleActionEnded` (rows 4-5). No record covers this.
- N2. The event -> affector path: `TriggerEmotionEvent` -> `CalculateRepetitionPenalty` -> one
  `Emotion::Add` per 8-byte affector (row 6).
- N3. `EmotionAffector` schema (`emotionType`, `value`) and the type-9 unknown warning (row 7).
- N4. The last-event clock and its `FLT_MAX` first-trigger result (row 9).
- N5. `AddToEmotion`'s `penalty * value` (row 11).
- N6. `StaticMoodData::GetDecayGraph`'s 12-byte stride (row 13).
- N7. `SendEmotionsToGame` is the mood system's output step and is unread (row 15).
- N8. The `eventMapper.emotionEvents` array is **empty** in the shipped config, so no named event is
  defined there; only `actionResultEmotionEvents` names events.

## Open questions for the manager

- Q1. `SendEmotionsToGame` is the only unread mood body. Read it, or record it as a gap?
- Q2. `MoodManager::SetEnableMoodEventOnCompletion` / the set at `+0x140` (`HandleActionEnded`'s
  early-out) is not covered by any M7 record. New record, or fold into M7-012?
- Q3. M7-013's `old <= 1e-5` wording correction - M7 correction or integrator note?
