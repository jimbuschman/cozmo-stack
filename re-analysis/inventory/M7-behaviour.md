# M7 behaviour inventory (idle, mood, reactions and concrete behaviour data)

**State:** prepared by job I-M7 and frozen with `python re-analysis/tools/fidelity.py --approve M7-behaviour`. Every prior source-fidelity claim starts as IMPLEMENTATION_GAP until the build job compares the live C# path with this inventory. No new equivalence policy is introduced.

## Where this comes from

- **Primary source:** `resources/lib/armeabi-v7a/libcozmoEngine.so` 3.4.0-1204; shipped Unity enums; shipped OBB under `re-analysis/obb/assets/cozmo_resources/`.
- **X3 passes:** parts 1-6 in `re-analysis/research/20260927-X3-M7-behaviour-*` and `20260928-X3-M7-behaviour-*` recover the enum/config corpus, mood path, idle interface, reaction paths, and the engine entry points and control flow for the 79 behaviour classes.
- **I-M7 gap pass 1:** `re-analysis/research/20260928-I-M7-gap1-extraction.md` closes mood output, `BehaviorWait`, `TrackLaser::UpdateInternal`, GuardDog's spread comparison, and factory-camera thresholds; it corrects the failed `ReactToCliff` mood citation.
- **Verification:** every row changing an existing M7 status and 24 additional rows were opened in the shipped ELF. The `0x00604ef4` citation failed and was replaced with `0x00604d70..0x00604d84`; no failed citation is retained.
- **Interfaces not duplicated:** M5 owns the animation streamer, face compositor and complete live-wire lifecycle implementation; M8 owns `IBehavior`, the manager, choosers and `Smart*` helpers; M10 owns derived-state reaction strategies; M12/M13/M14/M15 own manipulation, navigation, face/social and freeplay behaviour bodies respectively; M9 owns singing audio semantics. Their concrete entry points remain in the appendices as inputs to those inventories.
- The existing C# was inspected only to choose record locations. It is never evidence.

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M7-001 | IMPLEMENTATION_GAP | The shipped enum sets: 575 `AnimationTrigger` values plus `Count`, 21 `ReactionTrigger` values plus `Count`/`NoneTrigger`, 79 `BehaviorClass` values and 179 `BehaviorID` values. | X3 part 1 row 1; part 3 existing-record review |
| M7-002 | IMPLEMENTATION_GAP | Shipped maps and config corpus: 573 animation-trigger pairs, 40 cube-trigger pairs, the full 21-entry reaction map, 178 behaviour configs, 76 used classes, and the strict-priority/activity configuration tree. The engine loaders and paths are part of the claim. | X3 part 3 rows 1-8 and 178-config table |
| M7-003 | IMPLEMENTATION_GAP | `ReactToImpact`: FallingStarted/FallingStopped state, intensity threshold 1000, head/lift recalibration with the 5 s wait, trigger `0x1a0`, the 60 s action argument, and state clearing/dispatch. | X3 part 1 row 3; part 5 ReactToImpact; `0x006061f8`, `0x00606348`, `0x00606408` |
| M7-004 | IMPLEMENTATION_GAP | All 30 idle tunables and exact default values from `AnimationStreamer::SetDefaultParams`. | X3 part 1 row 4; `0x0057db40` |
| M7-005 | IMPLEMENTATION_GAP | Blink generation: fixed seven-frame squash table, `GenerateBlink`, and multiplicative `ProceduralFace::Combine`. | X3 part 1 row 5; `0x00585f18`, table `0x00c5aad8`, `0x0058d2ac`, `0x005846a8` |
| M7-006 | IMPLEMENTATION_GAP | Eye shift moves the whole face through `LookAt`, with the engine's bounds and x/y limits. | X3 part 1 row 6; `0x0058d100`, `0x00584158` |
| M7-007 | IMPLEMENTATION_GAP | The eye-dart layer is persistent: it interpolates to its gaze, rewinds/holds, and does not fade back when its duration expires. | X3 part 1 row 7; `0x0058e644`, `0x0058eaa0`, `0x0058cd80` |
| M7-008 | IMPLEMENTATION_GAP | Idle timers are whole milliseconds advanced by the engine's 60 ms update quantum; counter initialization, integer random draws, blocked-track behavior, and movement duration plus separate gap are included. | X3 part 1 row 8; `0x0065b3a8`, `0x0057d444`, `0x0057d650`, `0x0058d388` |
| M7-009 | IMPLEMENTATION_GAP | Idle head and lift are live-animation keyframes, including conversion/current-angle inputs, variability and robot message types. | X3 part 1 row 9; `0x0057d85c`, `0x0057d9c0` |
| M7-010 | IMPLEMENTATION_GAP | Idle body shuffle: exact straight/turn selection, speed/radius/duration, paired eye layer and spacing. | X3 part 1 row 10; `0x0057d6cc..0x0057d978` |
| M7-011 | IMPLEMENTATION_GAP | Reaction cooldowns exist only where the strategy config names one. FrustrationMinor carries 60 s; objective cooldowns and generic strategies remain distinct. | X3 part 1 row 11; part 3 reaction map; `0x0060ee6e..0x0060eed8` |
| M7-012 | IMPLEMENTATION_GAP | Shipped mood schema and runtime: nine emotions, decay graphs/defaults, affectors, action-result event mapping, per-event/default repetition penalties, manager update and nine-value game broadcast. | X3 part 4 rows 1-13, 15; gap1 G1; `mood_config.json`; `0x0067ce6c`, `0x0067b5d4`, `0x0067b724` |
| M7-013 | IMPLEMENTATION_GAP | Mood arithmetic: clamp to [-1,1], exact decay-clock restart conditions, graph endpoint/interpolation rules and `Emotion::Update` ratio; when old graph value is <=1e-5 it multiplies by the raw new reading. | X3 part 1 rows 13-15; part 4 row 14; `0x00679618`, `0x006795a4`, `0x00804bd0` |
| M7-014 | IMPLEMENTATION_GAP | Reaction-lock lifetime and manager semantics, plus the 13 recovered per-class 21-byte lock tables. Locks are acquired through `SmartDisableReactionsWithLock`, held for the reaction, and removed through the matching manager lock name. | X3 part 1 row 16; part 6 lock-table appendix; M8 gap reports; `0x005bce3c`, `0x005a27e8`, `0x005a3a48` |
| M7-015 | IMPLEMENTATION_GAP | Pick-up/off-treads gating uses the engine's robot-state fields and derived classifier path. The prior raw-flag fallback is not accepted as an equivalence claim until the production path is compared. | X3 part 1 row 17; part 6 ReactToPickup; `0x00511e00`, `0x00607750..0x00607d9c` |
| M7-016 | IMPLEMENTATION_GAP | The face carries a stack of named persistent/transient layers; simultaneous blink, eye dart and live-idle turn compose in engine order. | X3 part 1 rows 5-7; `0x0058e644`, `0x005846a8`, `0x0058d3e2`, `0x0058d4be` |
| M7-017 | IMPLEMENTATION_GAP | The complete live-animation wire interface used by idle: live keyframes enter M5's stream opened with tag `0xff`, audio framing precedes the start, frames are budgeted, and takeover/reopen/end suppression follow the engine. M5 owns the streamer implementation; M7 must call it on the same lifecycle. | X3 part 1 row 18; `0x0057d5f8`, `0x0057ce5c`, `0x0057b674`, `0x0057c84c`, `0x0057c400`, `0x0057bf60` |
| M7-018 | IMPLEMENTATION_GAP | Behaviour configuration/factory binding: all 178 shipped IDs map to their declared class, animation list and executable type; the engine class aliases and class-`0x39` `BehaviorWait` construction are included. `BehaviorWait` installs only its vtable over base `IBehavior` and has no `*Internal` override. | X3 part 2 class table; part 3 178-config table; gap1 G2; `BehaviorContainer::CreateBehavior 0x0059c888` |
| M7-019 | IMPLEMENTATION_GAP | M7-owned concrete reaction behaviours: the recovered Init/Update/Stop/Handle state machines for Cliff, Frustration, Impact, MotorCalibration, OnCharger, Pet, Pickup, PlacedOnSlope, Pyramid/stack, ReturnedToTreads, on-back/face/side, shaken, sparked and unexpected movement; acknowledgement behaviours are included at their interfaces to M10/M14. | X3 part 5 reaction rows; part 6 Reactions; corrected Cliff event `0x00604d70..0x00604d84` |
| M7-020 | IMPLEMENTATION_GAP | Mood event production: action-result mappings, `TriggerEmotionEvent`, repetition penalty, per-affector `Emotion::Add`, completion enable set, and output broadcast. | X3 part 4 rows 4-11, 15; gap1 G1; `0x0067b85c`, `0x0067bdca`, `0x0067c770`, `0x0067b724` |
| M7-021 | RECOVERABLE_GAP | Live reaction helper/field semantics not yet named from source: writers/readers of `robot+0x355`, `+0x338`, `+0x300`, `+0x37c`, and helper bodies `0x0055b554` / `0x005c0ca8`. No semantic name or helper effect may be inferred before those paths are read. | gap1 G8 |
| M7-022 | RECOVERABLE_GAP | `DockingTestSimple::UpdateInternal` developer-test state machine. The broad state families are known, but every state/callback/random argument has not been exhaustively enumerated. This is not on the production live path. | X3 part 6 DockingTestSimple; gap1 G7; `0x005cbc10..0x005ccc3f` |

## Decisions

- **SD1 (exact, always).** Applied everywhere. All shipped engine/config behavior is to be transliterated; no EQUIVALENT_IMPLEMENTATION is introduced. The former M7-015 and M7-017 equivalence claims return to IMPLEMENTATION_GAP for full-path comparison.
- **SD2 (engine undefined behavior).** Not applied. No M7 row requires a new deterministic stand-in.
- **SD3 (later-layer ownership).** Manipulation, navigation, faces/social, singing and freeplay per-class rows are preserved in the appendices but owned by M12, M13, M14, M9 and M15. M7 records only their configuration/factory interface; their state machines are not silently claimed complete here.
- **SD4 (housekeeping).** The six X3 reports and gap pass 1 are committed as the immutable extraction inputs and reproduced below.
- **MD1.** M5 owns the streaming/compositor bodies behind M7-005..010, 016 and 017. M7 owns when and how idle/mood/reaction behavior drives those interfaces.
- **MD2.** M7-021 remains a live RECOVERABLE_GAP, so `source_investigation_exhausted` is false. M7-022 is non-live and does not strengthen any production claim.
- **MD3.** The sentinel trigger `0x23f` and `Smart*` helper mechanics remain M8 interfaces; concrete M7 behaviours' uses are recorded in M7-019.

## Existing evidence contradicted or too weak

- **M7-001..014 and M7-016:** all were pre-process EXACT_SOURCE claims. Their cited facts were largely confirmed, but none had been checked against the complete live path, so they become IMPLEMENTATION_GAP until comparison/build.
- **M7-002:** the earlier evidence omitted `assets/AnimationTriggerMap.json`, cube mappings, the 178 configs and activity tree. Part 3 supplies them.
- **M7-003:** the earlier two citations did not prove intensity 1000 or the 5 s recalibration branch. Part 5 supplies `InitInternal` and the missing branches.
- **M7-012:** a config filename alone did not prove parsing, update or output. Part 4 and gap1 G1 supply the runtime path.
- **M7-013:** contradicted in one edge branch. `Emotion::Update` does not leave the value alone when the old graph reading is <=1e-5; it multiplies by the raw new reading (`0x006795f8`).
- **M7-014:** “called in each BehaviorReactToX InitInternal” was not an address citation and overgeneralized. The helper/manager bodies and concrete lock tables replace it.
- **M7-015:** the existing EQUIVALENT_IMPLEMENTATION label depended on a stack fallback rather than a source-faithful complete path. It is now IMPLEMENTATION_GAP.
- **M7-017:** the existing EQUIVALENT_IMPLEMENTATION record described differences while claiming a complete lifecycle. It is now IMPLEMENTATION_GAP pending comparison against the exact M5/M7 interface.
- **X3 part 6 ReactToCliff row:** `0x00604ef4` failed verification. The correct mood-event sequence is `0x00604d70..0x00604d84` (gap1 G6).

## Appendices

The following appendices reproduce the complete extraction inputs verbatim.


## Appendix A - X3 part 1

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


## Appendix B - X3 part 2

# Job X3 - M7-behaviour extraction, part 2: the BehaviourClass inventory

Agent: opencode-w1 (DeepSeek). Read-only extraction. Date: 2026-09-27.

This part enumerates the 79 `BehaviorClass` values the job names and gives, for each, the engine's
own entry points for its run logic: `InitInternal`, `UpdateInternal`, `StopInternal`,
`IsRunnableInternal` and `HandleWhileRunning`. Addresses are the Thumb entry (`dynsym` value, low bit
set) from `resources/lib/armeabi-v7a/libcozmoEngine.so`. A dash means the class does not override
that virtual (it inherits `IBehavior`'s).

**What this part does not do:** it does not read each class body. The job asks for each class's run
logic; that is one `UpdateInternal`/`InitInternal` body per class (79 classes, many hundreds of
instructions each) and is not complete in this pass. Every row below is therefore
`RECOVERABLE_GAP` for its run logic: read the listed `UpdateInternal`/`InitInternal` at that address.
The entry points are real and are the correct things to read.

Method: `BehaviorClass.cs` (the decompiled Unity enum, 79 values) joined against the engine's dynamic
symbol table; the five classes whose C++ name differs from the enum name are mapped by hand:

| BehaviorClass | engine class |
|---|---|
| BringCubeToBeacon | `BehaviorExploreBringCubeToBeacon` |
| PlayAnimWithFace | `BehaviorPlayAnimSequenceWithFace` |
| RequestGameSimple | `BehaviorRequestGameSimple` (`RequestGame_*Internal`) |
| ReactToCubeMoved | `BehaviorAcknowledgeCubeMoved` |
| Wait | `BehaviorWait` (no `*Internal` overrides found) |

Known entry-point facts read in this pass:
- `BehaviorPlayAnimSequence::InitInternal` is at `0x005c0648`; its `StartPlayingAnimations` at
  `0x005c0158` plays the trigger list (M8-005).
- `BehaviorReactToImpact::InitInternal` at `0x006061f8`, `StopInternal` at `0x006063fc`; the trigger
  path is in the part-1 report.
- `BehaviorExploreLookAroundInPlace::InitInternal` at `0x5e2684` is the look-around scan the M15
  classifier names.
- `BehaviorAcknowledgeCubeMoved` is the class behind the `ReactToCubeMoved` reaction and carries the
  full state machine (`TransitionToPlayingSenseReaction`, `TransitionToReactingToBlockAbsence`,
  `TransitionToTurningToLastLocationOfBlock`, `HandleObservedObject`).

## Per-class entry points

| BehaviorClass | InitInternal | UpdateInternal | StopInternal | IsRunnableInternal | HandleWhileRunning |
|---|---|---|---|---|---|
| Bouncer | 0x5f14a0 | 0x5f19a4 | 0x5f1c6c | 0x5f133c | - |
| BringCubeToBeacon | - | - | - | - | - |
| BuildPyramid | 0x5dbefe | 0x5dd01c | - | 0x5dbed0 | - |
| BuildPyramidBase | 0x5dcb56 | 0x5dd01c | - | 0x5dcaa4 | - |
| CantHandleTallStack | 0x5ece3c | - | 0x5ed064 | 0x5ecd64 | - |
| CheckForStackAtInterval | 0x5d7364 | - | 0x5d73ba | 0x5d7328 | - |
| CubeLiftWorkout | 0x5d7eb8 | 0x5d81a0 | 0x5d816c | 0x5d7e88 | - |
| Dance | 0x5ed460 | - | 0x5ed980 | - | - |
| DevTurnInPlaceTest | 0x5ca8fc | - | 0x5cad30 | 0x5ca8f6 | - |
| DockingTestSimple | 0x5cb394 | 0x5cbc10 | 0x5cda38 | 0x5cb384 | 0x5cdc30 |
| DriveInDesperation | 0x5d8eca | 0x5d9062 | 0x5d9060 | 0x5d90ac | - |
| DriveOffCharger | 0x5c0b18 | 0x5c0da8 | 0x5c0d90 | 0x5c0b10 | - |
| DrivePath | 0x5c0f76 | - | - | 0x5c0f68 | - |
| DriveToFace | 0x5da668 | 0x5da8fc | 0x5da93e | 0x5da588 | - |
| EarnedSparks | 0x5daecc | - | 0x5daf70 | 0x5daec2 | - |
| EnrollFace | 0x5fcec0 | 0x5fd968 | 0x5fe2f4 | 0x5fcd00 | 0x5ff55c |
| ExploreLookAroundInPlace | 0x5e2684 | - | - | 0x5e259c | - |
| ExploreVisitPossibleMarker | 0x5e3d8c | - | - | 0x5e3d6c | - |
| ExpressNeeds | 0x5ee67c | - | 0x5ee7d8 | 0x5ee570 | - |
| FactoryCentroidExtractor | 0x5cf8b4 | 0x5cfce0 | 0x5cfcfe | 0x5cf89c | 0x5cfdd0 |
| FactoryTest | 0x5d0c1c | 0x5d2724 | 0x5d27f8 | 0x5d0c0c | 0x5d2808 |
| FeedingEat | 0x5d5904 | 0x5d5b8c | 0x5d5de8 | 0x5d5880 | - |
| FeedingSearchForCube | 0x5d6c84 | 0x5d6d14 | - | 0x5d6c80 | - |
| FindFaces | - | - | - | 0x5c1a34 | - |
| FireTruckAlarm | 0x5dafb4 | - | - | 0x5dafa4 | - |
| FistBump | 0x5f1ed8 | 0x5f1f24 | 0x5f26f8 | 0x5f1ed4 | - |
| GuardDog | 0x5f2cfc | 0x5f2e68 | 0x5f4718 | 0x5f2a9c | 0x5f4d28 |
| InteractWithFaces | 0x5c2154 | 0x5c2354 | 0x5c240e | 0x5c23ac | - |
| KnockOverCubes | 0x5c31a2 | - | 0x5c36f2 | 0x5c314c | 0x5c3ae4 |
| LiftLoadTest | 0x5d410c | 0x5d45c0 | 0x5d4d38 | 0x5d40f0 | 0x5d4e64 |
| LookAround | 0x5c465c | 0x5c58c4 | 0x5c58f4 | 0x5c409a | 0x5c40d8 |
| LookForFaceAndCube | 0x5efd58 | - | 0x5f0878 | 0x5efd54 | 0x5f087c |
| LookInPlaceMemoryMap | 0x5e4ce0 | - | 0x5e4f2a | 0x5e4c6c | - |
| OnConfigSeen | 0x5db3ac | - | - | 0x5db2d4 | - |
| OnboardingShowCube | 0x600c7c | 0x601544 | 0x600e9c | 0x600c78 | 0x6010a8 |
| PeekABoo | 0x5f66e4 | 0x5f69d8 | 0x5f6f20 | 0x5f65ec | - |
| PickUpAndPutDownCube | 0x5db6b4 | - | - | 0x5db68c | - |
| PickUpCube | 0x5c6644 | 0x5c6824 | - | 0x5c6628 | - |
| PlayAnim | 0x5c0648 | - | 0x5bfef8 | 0x5c013a | - |
| PlayAnimOnNeedsChange | - | - | 0x5bfef8 | - | - |
| PlayAnimWithFace | - | - | - | - | - |
| PlayArbitraryAnim | 0x5c0896 | - | 0x5c08aa | 0x5c07be | - |
| PopAWheelie | 0x5c7496 | - | 0x5c76ac | 0x5c7478 | - |
| PounceOnMotion | 0x5f8412 | - | 0x5f8728 | 0x5f83ba | 0x5f9c34 |
| PutDownBlock | 0x5c7fd0 | - | - | 0x5c7fb0 | - |
| PyramidThankYou | 0x5de0cc | - | 0x5de314 | 0x5de078 | - |
| RequestGameSimple | - | - | - | - | - |
| RespondPossiblyRoll | 0x5de4ba | 0x5de5c6 | - | 0x5de4b6 | - |
| RespondToRenameFace | 0x600848 | 0x600a50 | - | 0x60082e | - |
| RollBlock | 0x5c8676 | 0x5c8a54 | 0x5c8c94 | 0x5c8650 | - |
| SearchForFace | 0x5c9182 | 0x5c9294 | - | 0x5c9170 | - |
| Singing | 0x5eeb30 | 0x5ef0c8 | 0x5ef2b0 | 0x5eeb0e | - |
| StackBlocks | 0x5c9502 | 0x5c9844 | 0x5c97f8 | 0x5c9484 | - |
| ThinkAboutBeacons | 0x5e5cdc | - | - | 0x5e5cc2 | - |
| TrackLaser | 0x5fac10 | 0x5fb200 | 0x5fbd14 | 0x5faab0 | - |
| TurnToFace | 0x5ca438 | - | 0x5ca4f8 | 0x5ca36c | - |
| VisitInterestingEdge | 0x5e7018 | 0x5e7418 | 0x5e729c | 0x5e6520 | - |
| Wait | - | - | - | - | - |
| AcknowledgeFace | 0x6028c8 | 0x602928 | 0x6028d2 | 0x6028bc | - |
| AcknowledgeObject | 0x603434 | 0x603524 | 0x60425e | 0x6042c8 | - |
| RamIntoBlock | 0x604764 | - | 0x604990 | 0x604754 | - |
| ReactToCliff | 0x604d50 | 0x605408 | 0x6053fc | 0x604d4c | 0x605580 |
| ReactToCubeMoved | - | - | - | - | - |
| ReactToFrustration | 0x605c0c | - | 0x605d84 | - | - |
| ReactToImpact | 0x6061f8 | - | 0x6063fc | - | - |
| ReactToMotorCalibration | 0x6065f0 | - | - | 0x6065ec | 0x606738 |
| ReactToOnCharger | 0x606c94 | 0x606e5c | 0x606e54 | 0x606c90 | 0x606e70 |
| ReactToPet | 0x606efc | 0x6072c8 | 0x607430 | 0x606ed8 | - |
| ReactToPickup | 0x607750 | 0x607ba8 | 0x607d9c | 0x60774c | - |
| ReactToPlacedOnSlope | 0x607fdc | - | - | 0x607fd8 | - |
| ReactToPyramid | 0x6083fc | - | - | 0x60832c | - |
| ReactToReturnedToTreads | 0x608500 | - | 0x608718 | 0x6084fc | - |
| ReactToRobotOnBack | 0x6087d4 | - | 0x6089fc | 0x6087d0 | - |
| ReactToRobotOnFace | 0x608ab8 | - | 0x608c98 | 0x608ab4 | - |
| ReactToRobotOnSide | 0x608d5c | - | 0x609064 | 0x608d58 | - |
| ReactToRobotShaken | 0x609128 | 0x609204 | 0x609504 | - | - |
| ReactToSparked | 0x6097f8 | - | - | 0x609864 | - |
| ReactToStackOfCubes | 0x60996c | - | - | 0x609898 | - |
| ReactToUnexpectedMovement | 0x609ac0 | - | - | 0x609abc | - |

## Existing records, judged

No existing M7 record names a per-class run-logic path except M7-003 (ReactToImpact, part 1). The
behaviour inventory (`re-analysis/BEHAVIOR_INVENTORY.md`, `behavior_inventory.json`) classifies the
178 configs by what they need, but it is a generated repo note (authority 6), not a source reading,
and its classifications are not evidence. This part does not change any existing record.

## NEW steps no record covers

- N1. Every `BehaviorClass` has a nameable run-logic entry point in the engine; the table above is
  the map from class to those entry points. No M7 record lists them.
- N2. Five enum classes do not share the engine class's name; the mapping is in the table above.
- N3. `BehaviorAcknowledgeCubeMoved` carries an explicit state machine with named transitions; the
  `ReactToCubeMoved` note does not name them.

## Open questions for the manager

- Q1. Per-class run logic is a large follow-up: 79 classes x up to four internal methods. Should the
  manager split it into batches (e.g. by M12/M13/M14/M15 ownership, matching the classifier) and hand
  each batch to a separate X job? This report gives the exact entry points each batch needs.
- Q2. The OBB is absent (see part 1), so the configs that drive these classes cannot be read here.
  The run-logic entry points can still be read from the `.so` without it, but the parameters each
  class reads cannot.


## Appendix C - X3 part 3

# Job X3 - M7-behaviour extraction, part 3: the shipped behaviour configs, maps and activity tree

Agent: opencode-w1 (DeepSeek). Read-only extraction. Date: 2026-09-28.

This part resumes X3 after the unpacked OBB was added to the clone. It reads the authority-3 artifacts
the 2026-09-27 part-1 report could not: the 178 behaviour configs, the reaction-trigger map, the
animation-trigger maps and the activity tree. It does not read per-class run logic (part 2 mapped the
entry points; part 5 does the run logic).

## Sources

- `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/` (behaviour configs, activity
  configs, the reaction map, the activity tree).
- `re-analysis/obb/assets/cozmo_resources/assets/animationGroupMaps/AnimationTriggerMap.json` and
  `.../cubeAnimationGroupMaps/CubeAnimationTriggerMap.json`.
- `resources/lib/armeabi-v7a/libcozmoEngine.so` for the loader addresses and the paths it reads.

The engine's own path strings (read from the `.so`) match the OBB tree exactly:
`config/engine/behaviorSystem/behaviors/` (`0x5208bc`), `config/engine/behaviorSystem/activities/`
(`0x520b58`), `config/engine/behaviorSystem/reactionTrigger_behavior_map.json` (`0x520ce8`),
`config/engine/behaviorSystem/activities_config.json` (`0xbe7e35`),
`config/engine/behaviorSystem/behavior_system_config.json` (`0xbe7ecc`) and
`config/engine/mood_config.json` (`0x523130`).

## Row table

| # | step | what the original does | citation | record | classification |
|---:|---|---|---|---|---|
| 1 | Behaviour configs loaded | `RobotDataLoader::LoadBehaviors` reads every file under `config/engine/behaviorSystem/behaviors/` (the engine's path string is at `0x5208bc`), each a JSON with `behaviorClass` and `behaviorID`. | `0x005206bc RobotDataLoader::LoadBehaviors`; `0x005208bc "config/engine/behaviorSystem/behaviors/"` | M7-002 (related) | EXACT_SOURCE |
| 2 | Config census | 178 config files, all JSON. Every one carries `behaviorClass` (76 distinct) and `behaviorID` (178 distinct). 39 are `Singing`, 15 `PlayAnim`, 10 `RequestGameSimple`, 7 `PlayAnimWithFace`, 6 `ExpressNeeds`, 4 each `FindFaces`/`PounceOnMotion`/`PutDownBlock`/`RollBlock`. The config tree also has `devBehaviors/` (5), `feeding/` (3) + `feeding/feedingAnims/` (8), `freeplay/` (17, with `buildPyramid/`, `hiking/`, `needs/`, `putDownDispatch/`, `requestGame/`, `singing/` 39, `sparkable/` 20, `userInteractive/` 3), `meetCozmo/` (4), `onboarding/` (1), `reactions/` (24), `voiceCommands/` (14 + `howAreYouDoing/` 4). | the tree under `re-analysis/obb/.../behaviorSystem/behaviors/`; the full table below | M7-002 | EXACT_SOURCE |
| 3 | Config keys | The top-level keys are `behaviorClass` and `behaviorID` on all 178; then `displayNameKey` (139), `requiredUnlockId` (76), `audioSwitchGroup`/`audioSwitch` (39 each, all Singing), `animTriggers` (33), `needsActionID` (22), `params` (15), `executableBehaviorType` (12), `zero_block_config`/`one_block_config` and the pickup/place motion profiles (10 and 5, RequestGameSimple), `need`/`needBracket`/`cooldown` (ExpressNeeds), and per-class keys such as `maxNoGroundMotionBeforeBored_*` (PounceOnMotion) and `minTimesPeekBeforeQuit` (PeekABoo). 126 distinct keys in all. | e.g. `.../behaviors/wait.json` (`executableBehaviorType`), `.../freeplay/requestGame/requestCozmoPerforms.json` (block configs), `.../freeplay/pounceOnMotion_socialize.json` | NEW | EXACT_SOURCE |
| 4 | class vs ID | `behaviorClass` selects the engine class; `behaviorID` is the config identity. They differ where several configs share a class: e.g. `Singing_AbaDaba` -> class `Singing`; `FPPeekABoo` -> class `PeekABoo`; `ReactToFrustrationMinor`/`ReactToFrustrationMajor` -> class `ReactToFrustration`; the 39 `Singing_*` and 10 `Request*` IDs. | table below | NEW | EXACT_SOURCE |
| 5 | Reaction map loaded | `RobotDataLoader::LoadReactionTriggerMap` reads `config/engine/behaviorSystem/reactionTrigger_behavior_map.json` (`0x520ce8`) through `DataPlatform::readAsJson`, and on failure logs `"Failed to read '%s'"` via `sErrorF`. `BehaviorManager::InitReactionTriggerMap` iterates the map's array and builds the trigger -> behaviour dispatch. | `0x00520bc8`; `0x00520c2e blx DataPlatform::readAsJson`; `0x00520c54 blx sErrorF`; `0x00520c3c add r2,pc ; r2=0xbe7d67 "Failed to read '%s'"`; `0x005a16e4`; `0x005a16f6` | M7-002 | EXACT_SOURCE |
| 6 | Reaction map contents | 21 entries. `FacePositionUpdated`->`AcknowledgeFace`; `FistBump`->`FistBump` (six objective params); `Hiccup`->`Hiccup` (`hiccupParams`: min/max occurrence 300/3300 s, 5..10 hiccups, 4500..8000 ms spacing, 600 s after cure, unlock `DroneModeGame`); `ObjectPositionUpdated`->`AcknowledgeObject`; `CliffDetected`->`ReactToCliff` (`shouldResumeLast: true`); `CubeMoved`->`ReactToCubeMoved`; `Frustration`->`ReactToFrustrationMinor` (`maxConfidence -0.6`, `cooldownTime_s 60.0`) and `Frustration`->`ReactToFrustrationMajor` (`maxConfidence -0.9`, no cooldown); `MotorCalibration`->`ReactToMotorCalibration` (`shouldResumeLast: true`); `NoPreDockPoses`->`RamIntoBlock`; `PlacedOnCharger`->`ReactToOnCharger` (`strategyType PlacedOnCharger`); `PetInitialDetection`->`ReactToPet`; `RobotFalling`->`ReactToImpact` (`shouldResumeLast: false`); `RobotPickedUp`->`ReactToPickup` (`false`); `RobotPlacedOnSlope`->`ReactToPlacedOnSlope` (`strategyType RobotPlacedOnSlope`); `ReturnedToTreads`->`ReactToReturnedToTreads` (`false`); `RobotOnBack`/`RobotOnFace`/`RobotOnSide` -> the matching `ReactTo*` (`false`); `RobotShaken`->`ReactToRobotShaken` (`strategyType RobotShaken`); `Sparked`->`ReactToSparked`; `UnexpectedMovement`->`ReactToUnexpectedMovement` (`shouldResumeLast: true`). | `config/engine/behaviorSystem/reactionTrigger_behavior_map.json:1-209` | M7-002 / M7-011 | EXACT_SOURCE |
| 7 | AnimationTriggerMap | `assets/animationGroupMaps/AnimationTriggerMap.json` has `Pairs`: 573 entries, each `{CladEvent, AnimName}`; all 573 `CladEvent` names are distinct. This is the shipped trigger -> animation-group map. | `.../assets/animationGroupMaps/AnimationTriggerMap.json:1` | M7-002 | EXACT_SOURCE |
| 8 | CubeAnimationTriggerMap | `assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json` has `Pairs`: 40 entries of the same shape, for cube animations. | `.../assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json` | M7-002 | EXACT_SOURCE |
| 9 | Activity tree (freeplay) | `behavior_system_config.json` names the top activity `StrictPriorityFreeplay` of type `StrictPriority`, with sub-activities in priority order: `Socialize` 11, `Singing` 12, `PlayWithHumans` 13, `BuildPyramid` 14, `PlayAlone` 15, `Hiking` 16, `NothingToDo` 17. | `config/engine/behaviorSystem/behavior_system_config.json:1-33` | NEW | EXACT_SOURCE |
| 10 | Activity definitions | `activities_config.json` defines `Selection` (`BehaviorsOnly`, chooser type `Selection`), `MeetCozmo` (`BehaviorsOnly`, chooser type `Scoring`, six behaviours with flat scores), `Feeding` (`Feeding`, universal chooser type `StrictPriority`, four behaviours), and `Freeplay` (`Freeplay`, sub-activities: 14 Spark activities at priority 0, then needs, then `PutDownDispatch` 10 .. `NothingToDo` 17; `desiredActivityNames` maps face/cube presence to `Socialize`/`PlayAlone`/`Hiking`). | `config/engine/behaviorSystem/activities_config.json:1-170` | NEW | EXACT_SOURCE |
| 11 | Per-activity configs | `behaviorSystem/activities/` holds one file per named activity; the clone ships `putDownDispatch.json` (`BehaviorsOnly`, chooser type `Scoring` with `DriveOffCharger` flat 1000 and `PutDownDispatch_LookForFaceAndCube` flat 1.0 with a `(0.2,0)->(0.2,1)` repetition penalty; `activityStrategy.requiredRecentOnTreadsEventSecs 5.0`). | `config/engine/behaviorSystem/activities/putDownDispatch.json:1-37` | NEW | EXACT_SOURCE |

### The 178 configs

| path under `behaviors/` | behaviorID | behaviorClass | animTriggers |
|---|---|---:|---:|
| `playArbitraryAnim.json` | PlayArbitraryAnim | PlayArbitraryAnim | 0 |
| `wait.json` | Wait | Wait | 0 |
| `devBehaviors/devTurnInPlaceTest.json` | DevTurnInPlaceTest | DevTurnInPlaceTest | 0 |
| `devBehaviors/dockingTestSimple.json` | DockingTestSimple | DockingTestSimple | 0 |
| `devBehaviors/factoryCentroidExtractor.json` | FactoryCentroidExtractor | FactoryCentroidExtractor | 0 |
| `devBehaviors/factoryTest.json` | FactoryTest | FactoryTest | 0 |
| `devBehaviors/liftLoadTest.json` | LiftLoadTest | LiftLoadTest | 0 |
| `feeding/feedingEat.json` | FeedingEat | FeedingEat | 0 |
| `feeding/feedingFindFacesSevere.json` | FeedingFindFacesSevere | FindFaces | 0 |
| `feeding/feedingSearchForCube.json` | FeedingSearchForCube | FeedingSearchForCube | 0 |
| `feeding/feedingAnims/feedingPlayRequestAtFace.json` | FeedingPlayRequestAtFace | PlayAnimWithFace | 1 |
| `feeding/feedingAnims/feedingPlayRequestAtFace_Severe.json` | FeedingPlayRequestAtFace_Severe | PlayAnimWithFace | 1 |
| `feeding/feedingAnims/feedingReactCubeShake.json` | FeedingReactCubeShake | PlayAnim | 1 |
| `feeding/feedingAnims/feedingReactCubeShake_Severe.json` | FeedingReactCubeShake_Severe | PlayAnim | 1 |
| `feeding/feedingAnims/feedingReactFullCube.json` | FeedingReactFullCube | PlayAnim | 1 |
| `feeding/feedingAnims/feedingReactFullCube_Severe.json` | FeedingReactFullCube_Severe | PlayAnim | 1 |
| `feeding/feedingAnims/feedingReactSeeCharged.json` | FeedingReactSeeCharged | PlayAnim | 1 |
| `feeding/feedingAnims/feedingReactSeeCharged_Severe.json` | FeedingReactSeeCharged_Severe | PlayAnim | 1 |
| `freeplay/cantHandleTallStack.json` | CantHandleTallStack | CantHandleTallStack | 0 |
| `freeplay/cubeLiftWorkout.json` | CubeLiftWorkout | CubeLiftWorkout | 0 |
| `freeplay/driveOffCharger.json` | DriveOffCharger | DriveOffCharger | 0 |
| `freeplay/EarnedSparks.json` | EarnedSparks | EarnedSparks | 0 |
| `freeplay/findFaces_socialize.json` | FindFaces_socialize | FindFaces | 0 |
| `freeplay/FPpeekAboo.json` | FPPeekABoo | PeekABoo | 0 |
| `freeplay/interactWithFaces.json` | InteractWithFaces | InteractWithFaces | 0 |
| `freeplay/knockOverCubes.json` | KnockOverCubes | KnockOverCubes | 0 |
| `freeplay/NothingToDo_BoredAnim.json` | NothingToDo_BoredAnim | PlayAnim | 3 |
| `freeplay/NothingToDo_Idle.json` | NothingToDo_Idle | PlayAnim | 1 |
| `freeplay/popAWheelie.json` | PopAWheelie | PopAWheelie | 0 |
| `freeplay/pounceOnMotion_socialize.json` | PounceOnMotion_Socialize | PounceOnMotion | 0 |
| `freeplay/putDownBlock.json` | PutDownBlock | PutDownBlock | 0 |
| `freeplay/putDownBlockNothingToDo.json` | PutDownBlockNothingToDo | PutDownBlock | 0 |
| `freeplay/rollBlockOnSide.json` | RollBlockOnSide | RollBlock | 0 |
| `freeplay/rollBlockOnSideLowScore.json` | RollBlockOnSideLowScore | RollBlock | 0 |
| `freeplay/stackBlocks.json` | StackBlocks | StackBlocks | 0 |
| `freeplay/buildPyramid/buildPyramid.json` | BuildPyramid | BuildPyramid | 0 |
| `freeplay/buildPyramid/buildPyramidBase.json` | BuildPyramidBase | BuildPyramidBase | 0 |
| `freeplay/buildPyramid/pyramidPutDownBlock.json` | PyramidPutDownBlock | PutDownBlock | 0 |
| `freeplay/buildPyramid/pyramidRespondPossiblyRoll.json` | PyramidRespondPossiblyRoll | RespondPossiblyRoll | 0 |
| `freeplay/buildPyramid/pyramidThankYou.json` | PyramidThankYou | PyramidThankYou | 0 |
| `freeplay/buildPyramid/respondToPyramidBase.json` | RespondToPyramidBase | OnConfigSeen | 1 |
| `freeplay/hiking/Hiking_bringCubeToBeacon.json` | Hiking_BringCubeToBeacon | BringCubeToBeacon | 0 |
| `freeplay/hiking/Hiking_driveOffCharger.json` | Hiking_DriveOffCharger | DriveOffCharger | 0 |
| `freeplay/hiking/Hiking_firstLookIntro.json` | Hiking_FirstLookIntro | PlayAnim | 1 |
| `freeplay/hiking/Hiking_firstLookWakeUp.json` | Hiking_FirstLookWakeUp | PlayAnim | 1 |
| `freeplay/hiking/Hiking_lookInPlace360.json` | Hiking_LookInPlace360 | ExploreLookAroundInPlace | 0 |
| `freeplay/hiking/Hiking_lookInPlaceForUnknown.json` | Hiking_LookInPlaceForUnknown | LookInPlaceMemoryMap | 0 |
| `freeplay/hiking/Hiking_pounceOnMotion.json` | Hiking_PounceOnMotion | PounceOnMotion | 0 |
| `freeplay/hiking/Hiking_rollCube.json` | Hiking_RollCube | RollBlock | 0 |
| `freeplay/hiking/Hiking_thinkAboutBeacons.json` | Hiking_ThinkAboutBeacons | ThinkAboutBeacons | 0 |
| `freeplay/hiking/Hiking_visitInterestingEdge.json` | Hiking_VisitInterestingEdge | VisitInterestingEdge | 0 |
| `freeplay/needs/needs_MildLowEnergyRequest.json` | Needs_MildLowEnergyRequest | ExpressNeeds | 1 |
| `freeplay/needs/needs_MildLowPlayRequest.json` | Needs_MildLowPlayRequest | ExpressNeeds | 1 |
| `freeplay/needs/needs_MildLowRepairRequest.json` | Needs_MildLowRepairRequest | ExpressNeeds | 1 |
| `freeplay/needs/needs_SevereLowEnergyForcedGetOut.json` | Needs_SevereLowEnergyForcedGetOut | ExpressNeeds | 1 |
| `freeplay/needs/needs_SevereLowEnergyGetIn.json` | Needs_SevereLowEnergyGetIn | PlayAnimOnNeedsChange | 1 |
| `freeplay/needs/needs_SevereLowEnergyState.json` | Needs_SevereLowEnergyState | DriveInDesperation | 0 |
| `freeplay/needs/needs_SevereLowPlayBored.json` | Needs_SevereLowPlayBored | ExpressNeeds | 3 |
| `freeplay/needs/needs_SevereLowPlayGetIn.json` | Needs_SevereLowPlayGetIn | PlayAnimOnNeedsChange | 1 |
| `freeplay/needs/needs_SevereLowPlayRequest.json` | Needs_SevereLowPlayRequest | ExpressNeeds | 1 |
| `freeplay/needs/needs_SevereLowRepairGetIn.json` | Needs_SevereLowRepairGetIn | PlayAnimOnNeedsChange | 1 |
| `freeplay/needs/needs_SevereLowRepairState.json` | Needs_SevereLowRepairState | DriveInDesperation | 0 |
| `freeplay/needs/needs_Wait.json` | Needs_Wait | Wait | 0 |
| `freeplay/putDownDispatch/PutDownDispatch_LookForFaceAndCube.json` | PutDownDispatch_LookForFaceAndCube | LookForFaceAndCube | 0 |
| `freeplay/requestGame/requestCozmoPerforms.json` | RequestCozmoPerforms | RequestGameSimple | 0 |
| `freeplay/requestGame/requestDroneMode.json` | RequestDroneMode | RequestGameSimple | 0 |
| `freeplay/requestGame/requestKeepAway.json` | RequestKeepAway | RequestGameSimple | 0 |
| `freeplay/requestGame/requestMemoryMatch.json` | RequestMemoryMatch | RequestGameSimple | 0 |
| `freeplay/requestGame/requestSpeedTap.json` | RequestSpeedTap | RequestGameSimple | 0 |
| `freeplay/singing/Singing_AbaDaba.json` | Singing_AbaDaba | Singing | 0 |
| `freeplay/singing/Singing_BeautifulDreamer.json` | Singing_BeautifulDreamer | Singing | 0 |
| `freeplay/singing/Singing_Beethovens5th.json` | Singing_Beethovens5th | Singing | 0 |
| `freeplay/singing/Singing_Bingo.json` | Singing_Bingo | Singing | 0 |
| `freeplay/singing/Singing_BuffaloGals.json` | Singing_BuffaloGals | Singing | 0 |
| `freeplay/singing/Singing_Camptown.json` | Singing_Camptown | Singing | 0 |
| `freeplay/singing/Singing_CanCan1.json` | Singing_CanCan1 | Singing | 0 |
| `freeplay/singing/Singing_CanCan2.json` | Singing_CanCan2 | Singing | 0 |
| `freeplay/singing/Singing_DannyBoy.json` | Singing_DannyBoy | Singing | 0 |
| `freeplay/singing/Singing_EntryOfTheGladiators.json` | Singing_EntryOfTheGladiators | Singing | 0 |
| `freeplay/singing/Singing_FarmerInTheDell.json` | Singing_FarmerInTheDell | Singing | 0 |
| `freeplay/singing/Singing_FrereJacques.json` | Singing_FrereJacques | Singing | 0 |
| `freeplay/singing/Singing_HelloMyBaby.json` | Singing_HelloMyBaby | Singing | 0 |
| `freeplay/singing/Singing_ItsyBitsySpider.json` | Singing_ItsyBitsySpider | Singing | 0 |
| `freeplay/singing/Singing_LaPaloma.json` | Singing_LaPaloma | Singing | 0 |
| `freeplay/singing/Singing_LondonBridge.json` | Singing_LondonBridge | Singing | 0 |
| `freeplay/singing/Singing_MaryHadALittleLamb.json` | Singing_MaryHadALittleLamb | Singing | 0 |
| `freeplay/singing/Singing_MountainKing.json` | Singing_MountainKing | Singing | 0 |
| `freeplay/singing/Singing_MuffinMan.json` | Singing_MuffinMan | Singing | 0 |
| `freeplay/singing/Singing_MulberryBush.json` | Singing_MulberryBush | Singing | 0 |
| `freeplay/singing/Singing_MussIDenn.json` | Singing_MussIDenn | Singing | 0 |
| `freeplay/singing/Singing_OdeToJoy.json` | Singing_OdeToJoy | Singing | 0 |
| `freeplay/singing/Singing_PachebelCanon.json` | Singing_PachebelCanon | Singing | 0 |
| `freeplay/singing/Singing_PopGoesTheWeasel.json` | Singing_PopGoesTheWeasel | Singing | 0 |
| `freeplay/singing/Singing_RowYourBoat.json` | Singing_RowYourBoat | Singing | 0 |
| `freeplay/singing/Singing_Sakura.json` | Singing_Sakura | Singing | 0 |
| `freeplay/singing/Singing_SilveryMoon.json` | Singing_SilveryMoon | Singing | 0 |
| `freeplay/singing/Singing_TakeMeOutToTheBallgame.json` | Singing_TakeMeOutToTheBallgame | Singing | 0 |
| `freeplay/singing/Singing_TaRaRaBoom.json` | Singing_TaRaRaBoom | Singing | 0 |
| `freeplay/singing/Singing_TisketTasket.json` | Singing_TisketTasket | Singing | 0 |
| `freeplay/singing/Singing_Toccata.json` | Singing_Toccata | Singing | 0 |
| `freeplay/singing/Singing_TurkeyInTheStraw.json` | Singing_TurkeyInTheStraw | Singing | 0 |
| `freeplay/singing/Singing_TwinkleTwinkle.json` | Singing_TwinkleTwinkle | Singing | 0 |
| `freeplay/singing/Singing_VivaldiSpring.json` | Singing_VivaldiSpring | Singing | 0 |
| `freeplay/singing/Singing_WaterMusic.json` | Singing_WaterMusic | Singing | 0 |
| `freeplay/singing/Singing_WildAboutHarry.json` | Singing_WildAboutHarry | Singing | 0 |
| `freeplay/singing/Singing_WilliamTell.json` | Singing_WilliamTell | Singing | 0 |
| `freeplay/singing/Singing_YankeeDoodle.json` | Singing_YankeeDoodle | Singing | 0 |
| `freeplay/singing/Singing_YellowRose.json` | Singing_YellowRose | Singing | 0 |
| `freeplay/sparkable/sparksBringCubeToBeacon.json` | SparksBringCubeToBeacon | BringCubeToBeacon | 0 |
| `freeplay/sparkable/sparksCheckForStackAtInterval.json` | SparksCheckForStackAtInterval | CheckForStackAtInterval | 0 |
| `freeplay/sparkable/sparksCubeLiftWorkout.json` | SparksCubeLiftWorkout | CubeLiftWorkout | 0 |
| `freeplay/sparkable/sparksFindFaces.json` | SparksFindFaces | FindFaces | 0 |
| `freeplay/sparkable/sparksFireTruckAlarm.json` | SparksFireTruckAlarm | FireTruckAlarm | 0 |
| `freeplay/sparkable/sparksFistBump.json` | SparksFistBump | FistBump | 0 |
| `freeplay/sparkable/sparksKnockOverCubes.json` | SparksKnockOverCubes | KnockOverCubes | 0 |
| `freeplay/sparkable/sparksLookInPlace.json` | SparksLookInPlace | ExploreLookAroundInPlace | 0 |
| `freeplay/sparkable/sparksPeekAboo.json` | SparksPeekABoo | PeekABoo | 0 |
| `freeplay/sparkable/sparksPickupCube.json` | SparksPickUpCube | PickUpAndPutDownCube | 0 |
| `freeplay/sparkable/sparksPickupSingleCubeForPyramid.json` | SparksPickupSingleCubeForPyramid | PickUpCube | 0 |
| `freeplay/sparkable/sparksPickupSingleCubeToStack.json` | SparksPickupSingleCubeToStack | PickUpCube | 0 |
| `freeplay/sparkable/sparksPopAWheelie.json` | SparksPopAWheelie | PopAWheelie | 0 |
| `freeplay/sparkable/sparksPounceOnMotion.json` | SparksPounceOnMotion | PounceOnMotion | 0 |
| `freeplay/sparkable/sparksPutDownBlock.json` | SparksPutDownBlock | PutDownBlock | 0 |
| `freeplay/sparkable/sparksRollBlock.json` | SparksRollBlock | RollBlock | 0 |
| `freeplay/sparkable/sparksStackBlock.json` | SparksStackBlock | StackBlocks | 0 |
| `freeplay/sparkable/sparksThinkAboutBeacons.json` | SparksThinkAboutBeacons | ThinkAboutBeacons | 0 |
| `freeplay/sparkable/sparksTrackLaser.json` | SparksTrackLaser | TrackLaser | 0 |
| `freeplay/sparkable/sparksVisitPossibleMarker.json` | SparksVisitPossibleMarker | ExploreVisitPossibleMarker | 0 |
| `freeplay/userInteractive/bouncer.json` | Bouncer | Bouncer | 0 |
| `freeplay/userInteractive/fistBump.json` | FistBump | FistBump | 0 |
| `freeplay/userInteractive/guardDog.json` | GuardDog | GuardDog | 0 |
| `meetCozmo/enrollFace.json` | EnrollFace | EnrollFace | 0 |
| `meetCozmo/meetCozmo_findFaces_socialize.json` | MeetCozmo_FindFaces_Socialize | FindFaces | 0 |
| `meetCozmo/meetCozmo_interactWithFaces.json` | MeetCozmo_InteractWithFaces | InteractWithFaces | 0 |
| `meetCozmo/respondToRenameFace.json` | RespondToRenameFace | RespondToRenameFace | 0 |
| `onboarding/onboardingShowCube.json` | OnboardingShowCube | OnboardingShowCube | 0 |
| `reactions/acknowledgeFace.json` | AcknowledgeFace | AcknowledgeFace | 0 |
| `reactions/acknowledgeObject.json` | AcknowledgeObject | AcknowledgeObject | 0 |
| `reactions/hiccup.json` | Hiccup | PlayAnim | 1 |
| `reactions/ramIntoBlock.json` | RamIntoBlock | RamIntoBlock | 0 |
| `reactions/reactToCliff.json` | ReactToCliff | ReactToCliff | 0 |
| `reactions/reactToCubeMoved.json` | ReactToCubeMoved | ReactToCubeMoved | 0 |
| `reactions/reactToFrustrationMajor.json` | ReactToFrustrationMajor | ReactToFrustration | 0 |
| `reactions/reactToFrustrationMinor.json` | ReactToFrustrationMinor | ReactToFrustration | 0 |
| `reactions/reactToImpact.json` | ReactToImpact | ReactToImpact | 0 |
| `reactions/reactToMotorCalibration.json` | ReactToMotorCalibration | ReactToMotorCalibration | 0 |
| `reactions/reactToObstacle.json` | ReactToObstacle | PlayAnim | 1 |
| `reactions/reactToOnCharger.json` | ReactToOnCharger | ReactToOnCharger | 0 |
| `reactions/reactToPet.json` | ReactToPet | ReactToPet | 0 |
| `reactions/reactToPickup.json` | ReactToPickup | ReactToPickup | 0 |
| `reactions/reactToPlacedOnSlope.json` | ReactToPlacedOnSlope | ReactToPlacedOnSlope | 0 |
| `reactions/reactToPyramid.json` | ReactToPyramid | ReactToPyramid | 0 |
| `reactions/reactToReturnedToTreads.json` | ReactToReturnedToTreads | ReactToReturnedToTreads | 0 |
| `reactions/reactToRobotOnBack.json` | ReactToRobotOnBack | ReactToRobotOnBack | 0 |
| `reactions/reactToRobotOnFace.json` | ReactToRobotOnFace | ReactToRobotOnFace | 0 |
| `reactions/reactToRobotOnSide.json` | ReactToRobotOnSide | ReactToRobotOnSide | 0 |
| `reactions/reactToRobotShaken.json` | ReactToRobotShaken | ReactToRobotShaken | 0 |
| `reactions/reactToSparked.json` | ReactToSparked | ReactToSparked | 0 |
| `reactions/reactToStackOfCubes.json` | ReactToStackOfCubes | ReactToStackOfCubes | 0 |
| `reactions/reactToUnexpectedMovement.json` | ReactToUnexpectedMovement | ReactToUnexpectedMovement | 0 |
| `voiceCommands/dance_mambo.json` | Dance_Mambo | Dance | 1 |
| `voiceCommands/VC_AlrightyResponse.json` | VC_AlrightyResponse | PlayAnimWithFace | 1 |
| `voiceCommands/VC_ComeHere.json` | VC_ComeHere | DriveToFace | 0 |
| `voiceCommands/VC_GoToSleep.json` | VC_GoToSleep | ReactToOnCharger | 0 |
| `voiceCommands/VC_PounceOnMotion.json` | VC_PounceOnMotion | PounceOnMotion | 0 |
| `voiceCommands/VC_Refuse_Energy.json` | VC_Refuse_Energy | PlayAnim | 1 |
| `voiceCommands/VC_Refuse_Repair.json` | VC_Refuse_Repair | PlayAnim | 1 |
| `voiceCommands/VC_Refuse_Sparks.json` | VC_Refuse_Sparks | PlayAnim | 1 |
| `voiceCommands/VC_RequestCozmoPerforms.json` | VC_RequestCozmoPerforms | RequestGameSimple | 0 |
| `voiceCommands/VC_RequestDroneMode.json` | VC_RequestDroneMode | RequestGameSimple | 0 |
| `voiceCommands/VC_RequestKeepAway.json` | VC_RequestKeepAway | RequestGameSimple | 0 |
| `voiceCommands/VC_RequestMemoryMatch.json` | VC_RequestMemoryMatch | RequestGameSimple | 0 |
| `voiceCommands/VC_RequestSpeedTap.json` | VC_RequestSpeedTap | RequestGameSimple | 0 |
| `voiceCommands/VC_SearchForFace.json` | VC_SearchForFace | SearchForFace | 0 |
| `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_AllGood.json` | VC_HowAreYouDoing_AllGood | PlayAnimWithFace | 1 |
| `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_Energy.json` | VC_HowAreYouDoing_Energy | PlayAnimWithFace | 1 |
| `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_Play.json` | VC_HowAreYouDoing_Play | PlayAnimWithFace | 1 |
| `voiceCommands/howAreYouDoing/VC_HowAreYouDoing_Repair.json` | VC_HowAreYouDoing_Repair | PlayAnimWithFace | 1 |

## Existing records, judged

- **M7-001** (EXACT_SOURCE, enums): confirmed. The Unity enums are present with the counts the job
  names (AnimationTrigger ~575 values, ReactionTrigger 21, BehaviorClass 79, BehaviorID 179).
- **M7-002** (EXACT_SOURCE, "AnimationTriggerMap.json and reactionTrigger_behavior_map.json are the
  shipped maps"): **now fully confirmed.** Both files are in the OBB, the engine's path strings match,
  and the loaders are real. The record should also name `assets/animationGroupMaps/` and the 573/40
  pair counts.
- **M7-011** (EXACT_SOURCE, reaction cooldowns): the map half is now confirmed from
  `reactionTrigger_behavior_map.json`. `FrustrationMinor` is the only frustration entry with a
  `cooldownTime_s` (60.0); `FrustrationMajor` has none and the code returns 1.0 for a non-positive
  stamp. The FistBump/Hiccup entries carry their own cooldowns.
- **M7-002's** `RobotDataLoader::LoadReactionTriggerMap 0x00520BC8` citation is real; add
  `BehaviorManager::InitReactionTriggerMap 0x005a16e4`.

## NEW steps no record covers

- N1. The 178-config table above (path, ID, class, animTrigger count) - no record lists them.
- N2. `behaviorClass` vs `behaviorID`: 76 classes for 178 IDs; the mapping is in the table.
- N3. The full 21-entry reaction map, including the FistBump objective params and the Hiccup params.
- N4. The animation-trigger maps: 573 pairs and 40 cube pairs.
- N5. The activity tree: `StrictPriorityFreeplay` and the `activities_config.json` activities, plus the
  per-activity `activities/` files and `requiredRecentOnTreadsEventSecs`.
- N6. The engine path strings for every one of these artifacts.

## Open questions for the manager

- Q1. The config table is 178 rows. Does the manager want it as a manifest record (one record per
  behaviour) or one record for the config set?
- Q2. `AnimationTriggerMap.json` is under `assets/`, not `config/engine/`; M7-002's title names it
  without a directory. Correct the record's location?
- Q3. The per-class run logic (part 2's entry points) is still not read. Split into batches, or keep
  RECOVERABLE_GAP?


## Appendix D - X3 part 4

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


## Appendix E - X3 part 5

# Job X3 - M7-behaviour extraction, part 5: per-class run logic (first pass)

Agent: opencode-w1 (DeepSeek). Read-only extraction. Date: 2026-09-28.

This part reads the run logic of a first set of behaviour classes and records exactly what remains.
It is not the whole 79 classes; part 2 has the complete entry-point map, and the "not yet read" list
at the end names the address to read for each remaining class. Nothing here is guessed: a class not
read is listed as RECOVERABLE_GAP with its address, not described.

## Row table

| # | class | what the original does | citation | record | classification |
|---:|---|---|---|---|---|
| 1 | ReactToImpact (completes M7-003) | `InitInternal` takes `SmartDisableReactionsWithLock`, allocates a 5.0 s (`0x40a00000`) wait action and `StartActing(TransitionToPlayingAnim)`. `TransitionToPlayingAnim` only acts when `+0x11e` is set: it allocates a `TriggerAnimationAction(robot, 0x1a0, 1, true, 0, 60.0f, ...)` (trigger `0x1a0`, 60 s). `AlwaysHandle` switches on the engine-to-game tag at `msg+0x10`: tag `0x3b` (`FallingStopped`) sets `+0x11d = 1`, reads the FallingStopped field at `+4`, and sets `+0x11e = 1` only when it is `> 1000.0f` (constant at `0x606470`); tag `0x1e` sets `+0x11c = 1` when `Robot::IsHeadCalibrated()` and `Robot::IsLiftCalibrated()` are both 1; tag `0x3a` clears both `+0x11e` and `+0x11c`. | `0x006061f8`; `0x00606238 movt r3,#0x40a0`; `0x0060635a ldrb.w r0,[r4,#0x11e]`; `0x0060636e movt r0,#0x4270`; `0x0060637e mov.w r2,#0x1a0`; `0x00606384 blx TriggerAnimationAction::TriggerAnimationAction`; `0x00606408`; `0x00606418 beq #0x60642c`; `0x0060641a cmp r0,#0x3a`; `0x00606422 strb.w r0,[r4,#0x11e]`; `0x0060642c movs r0,#1`; `0x00606434 blx Get_FallingStopped`; `0x0060643e vldr s0,[pc,#0x30]` -> `0x606470` = 1000.0; `0x0060644a it gt`; `0x0060644e strb.w r0,[r4,#0x11e]`; `0x00606454 blx Robot::IsHeadCalibrated`; `0x00606460 blx Robot::IsLiftCalibrated`; `0x0060646a strbeq.w r0,[r4,#0x11c]` | M7-003 | EXACT_SOURCE |
| 2 | DriveOffCharger | `InitInternal` takes the reaction lock, sets `+0x120 = 0`, and when the AI process byte (`robot+0x264 -> +0x30 -> +0x14`) is 3 calls `DrivingAnimationHandler::PushDrivingAnimations(..., name)` and sets `+0x120 = 1`. If `robot+0x355` (on-treads/off-treads flag) is set it runs the `"WaitForOnTreads"` sub-behaviour; otherwise `TransitionToDrivingForward`. `UpdateInternal`: if `robot+0x34a` is set and `robot+0x355` is set, `StopActing` and re-run `"WaitForOnTreads"`; if `robot+0x34a` is clear and no current action, it stamps the AI state `robot+0x264->+0x18->+0x44 = now` and returns 2 (done); if `robot+0x355` is clear and no action, `TransitionToDrivingForward`. Returns 1 while acting. | `0x005c0b18`; `0x005c0b30 strb.w r0,[r4,#0x120]`; `0x005c0b3c cmp r0,#3`; `0x005c0b4a blx PushDrivingAnimations`; `0x005c0b54 ldrb.w r0,[r5,#0x355]`; `0x005c0b62 add r1,pc ; r1=0xbf2b55 "WaitForOnTreads"`; `0x005c0b8c blx TransitionToDrivingForward`; `0x005c0da8`; `0x005c0db0 ldrb.w r0,[r5,#0x34a]`; `0x005c0dc4 blx StopActing`; `0x005c0df4 ldr.w r0,[r4,#0x84]`; `0x005c0e08 str r0,[r1,#0x44]`; `0x005c0e0a movs r0,#2` | NEW | EXACT_SOURCE |
| 3 | ExpressNeeds | `InitInternal` builds a `CompoundActionSequential`: first a `TurnTowardsLastFacePoseAction` (constructed from a `TurnTowardsFaceAction` with `pi` radians, i.e. turn around), then for every need action id in the vector at `+0x128..+0x12c` a `TriggerAnimationAction(trigger, 1, true, 0, 60.0f, ...)`, and `StartActing` it. `GetCooldownSec` and `ResumeInternal` exist; `StopInternal` at `0x5ee7d8`. | `0x005ee67c`; `0x005ee6a4 mov.w r0,#0x1d8`; `0x005ee6ae movw r1,#0xfdb` / `0x005ee6b4 movt r1,#0x4049` (`pi`); `0x005ee6d0 blx TurnTowardsFaceAction::TurnTowardsFaceAction`; `0x005ee6f2 ldr.w r7,[r6,#0x128]`; `0x005ee6f8 ldr.w r6,[r6,#0x12c]`; `0x005ee72c blx TriggerAnimationAction::TriggerAnimationAction`; `0x005ee75e blx StartActing` | NEW | EXACT_SOURCE |
| 4 | Singing | `InitInternal` posts the audio switch state (`audioSwitchGroup`/`audioSwitch` from the config, `RobotAudioClient::PostRobotSwitchState`), takes the reaction lock, finds connected cubes through `BlockWorld::FindConnectedActiveMatchingObjects`, attaches a `ShakeListener` to each cube through `CubeAccelComponent::AddListener`, builds a `CompoundActionSequential` of three `TriggerAnimationAction`s, and `StartActing` it. `UpdateInternal` at `0x5ef0c8`, `StopInternal` at `0x5ef2b0` (body not read). | `0x005eeb30`; `0x005eeb4e blx PostRobotSwitchState`; `0x005eeb5e blx SmartDisableReactionsWithLock`; `0x005eec5c blx FindConnectedActiveMatchingObjects`; `0x005eed08 blx ShakeListener::ShakeListener`; `0x005eed56 blx CubeAccelComponent::AddListener`; `0x005eedb2 blx CompoundActionSequential::CompoundActionSequential`; `0x005eede0/0x005eee1a/0x005eee54 blx TriggerAnimationAction`; `0x005eee8c blx StartActing` | NEW | EXACT_SOURCE for `InitInternal`; `UpdateInternal`/`StopInternal` RECOVERABLE_GAP |
| 5 | AcknowledgeCubeMoved (ReactToCubeMoved) | `InitInternal` takes the reaction lock, clears `+0x12c`, and branches on `+0x128`: `1` -> `TransitionToTurningToLastLocationOfBlock`, else `TransitionToPlayingSenseReaction`. `UpdateInternal` (state 1, `+0x12c` set) stops acting, plays a `TriggerLiftSafeAnimationAction(trigger 3, ...)`, sets state `+0x128 = 3`, and calls the `"WaitForOnTreads"` helper; then calls the base `IBehavior::UpdateInternal`. Other transitions: `TransitionToReactingToBlockAbsence` (`0x6026a8`), `HandleObservedObject` (`0x6027d2`), `HandleWhileRunning` (`0x6027a8`). | `0x00602234`; `0x00602242 blx SmartDisableReactionsWithLock`; `0x00602246 ldr.w r0,[r5,#0x128]`; `0x0060224c strb.w r1,[r5,#0x12c]`; `0x00602258 blx TransitionToTurningToLastLocationOfBlock`; `0x00602262 blx TransitionToPlayingSenseReaction`; `0x006024f8`; `0x00602522 blx StopActing`; `0x00602546 blx TriggerLiftSafeAnimationAction`; `0x00602582 str.w r0,[r5,#0x128]`; `0x006025a0 blx IBehavior::UpdateInternal` | NEW | EXACT_SOURCE for the state entry; the remaining transitions RECOVERABLE_GAP |
| 6 | ReactToOnCharger | `InitInternal` takes the reaction lock, registers a going-to-sleep handler (`MessageEngineToGame::MessageEngineToGame(GoingToSleep)`), pushes idle animation trigger `0x23f`, plays a `TriggerLiftSafeAnimationAction(trigger 0x189, ...)`, sets the robot disconnect reason to `4` (`SetRobotDisconnectReason`), and registers the charger callbacks. `UpdateInternal` at `0x606e5c`, `HandleWhileRunning` at `0x606e70`. | `0x00606c94`; `0x00606cb0 blx SmartDisableReactionsWithLock`; `0x00606cc6 movw r2,#0x23f`; `0x00606cca blx SmartPushIdleAnimation`; `0x00606cec movw r2,#0x189`; `0x00606cf2 blx TriggerLiftSafeAnimationAction`; `0x00606d1a movs r1,#4`; `0x00606d20 blx SetRobotDisconnectReason` | NEW | EXACT_SOURCE for `InitInternal`; `UpdateInternal` RECOVERABLE_GAP |
| 7 | PlayAnimSequence | Already recorded: `StartPlayingAnimations`/`StartSequenceLoop` play every trigger in the config's order `num_loops` times. | `0x005c0158`, `0x005c0294` (M8-005) | M8-005 | EXACT_SOURCE |
| 8 | Wait | `BehaviorWait` has no `*Internal` overrides found in the dynamic symbol table; the `wait.json` config carries `"executableBehaviorType": "Wait"`, so it is driven by the executable-behaviour mechanism rather than a class override. The class body was not located. | `.../behaviors/wait.json`; no `BehaviorWait::` symbol | NEW | RECOVERABLE_GAP - locate `BehaviorWait` (or the executable-behaviour factory) |

## Not yet read (entry points from part 2)

These classes have a real entry point but no body was read in this pass. Each is RECOVERABLE_GAP for
its run logic; part 2's table gives the same addresses. Read `InitInternal`/`UpdateInternal`/
`StopInternal` at the address shown.

`Bouncer 0x5f14a0`, `BringCubeToBeacon` (engine `BehaviorExploreBringCubeToBeacon`),
`BuildPyramid 0x5dbefe`, `BuildPyramidBase 0x5dcb56`, `CantHandleTallStack 0x5ece3c`,
`CheckForStackAtInterval 0x5d7364`, `CubeLiftWorkout 0x5d7eb8`, `Dance 0x5ed460`,
`DevTurnInPlaceTest 0x5ca8fc`, `DockingTestSimple 0x5cb394`, `DriveInDesperation 0x5d8eca`,
`DrivePath 0x5c0f76`, `DriveToFace 0x5da668`, `EarnedSparks 0x5daecc`, `EnrollFace 0x5fcec0`,
`ExploreLookAroundInPlace 0x5e2684`, `ExploreVisitPossibleMarker 0x5e3d8c`, `FactoryCentroidExtractor
0x5cf8b4`, `FactoryTest 0x5d0c1c`, `FeedingEat 0x5d5904`, `FeedingSearchForCube 0x5d6c84`,
`FindFaces 0x5c1a34`, `FireTruckAlarm 0x5dafb4`, `FistBump 0x5f1ed8`, `GuardDog 0x5f2cfc`,
`InteractWithFaces 0x5c2154`, `KnockOverCubes 0x5c31a2`, `LiftLoadTest 0x5d410c`, `LookAround
0x5c465c`, `LookForFaceAndCube 0x5efd58`, `LookInPlaceMemoryMap 0x5e4ce0`, `OnConfigSeen 0x5db3ac`,
`OnboardingShowCube 0x600c7c`, `PeekABoo 0x5f66e4`, `PickUpAndPutDownCube 0x5db6b4`, `PickUpCube
0x5c6644`, `PlayAnim 0x5c0648`, `PlayAnimOnNeedsChange`, `PlayAnimWithFace`, `PlayArbitraryAnim
0x5c0896`, `PopAWheelie 0x5c7496`, `PounceOnMotion 0x5f8412`, `PutDownBlock 0x5c7fd0`,
`PyramidThankYou 0x5de0cc`, `RequestGameSimple`, `RespondPossiblyRoll 0x5de4ba`, `RespondToRenameFace
0x600848`, `RollBlock 0x5c8676`, `SearchForFace 0x5c9182`, `StackBlocks 0x5c9502`, `ThinkAboutBeacons
0x5e5cdc`, `TrackLaser 0x5fac10`, `TurnToFace 0x5ca438`, `VisitInterestingEdge 0x5e7018`,
`AcknowledgeFace 0x6028c8`, `AcknowledgeObject 0x603434`, `RamIntoBlock 0x604764`, `ReactToCliff
0x604d50`, `ReactToFrustration 0x605c0c`, `ReactToMotorCalibration 0x6065f0`, `ReactToPet 0x606efc`,
`ReactToPickup 0x607750`, `ReactToPlacedOnSlope 0x607fdc`, `ReactToPyramid 0x6083fc`,
`ReactToReturnedToTreads 0x608500`, `ReactToRobotOnBack 0x6087d4`, `ReactToRobotOnFace 0x608ab8`,
`ReactToRobotOnSide 0x608d5c`, `ReactToRobotShaken 0x609128`, `ReactToSparked 0x6097f8`,
`ReactToStackOfCubes 0x60996c`, `ReactToUnexpectedMovement 0x609ac0`.

Also unread within read classes: `BehaviorSinging::UpdateInternal 0x5ef0c8` and `StopInternal
0x5ef2b0`; `BehaviorReactToOnCharger::UpdateInternal 0x606e5c` and `HandleWhileRunning 0x606e70`;
`BehaviorAcknowledgeCubeMoved::TransitionToTurningToLastLocationOfBlock 0x602270`,
`TransitionToPlayingSenseReaction 0x6023f8`, `TransitionToReactingToBlockAbsence 0x6026a8`,
`HandleObservedObject 0x6027d2`, `HandleWhileRunning 0x6027a8`.

## Existing records, judged

- **M7-003** (EXACT_SOURCE, ReactToImpact): **now complete.** The "impact intensity over 1000"
  (constant `1000.0` at `0x606470`) and the head/lift recalibration completion (`0x606454`) are read;
  the earlier report's untaken branches are settled. The 5 s is the `InitInternal` wait-action timeout
  (`0x40a00000`), and the animation is trigger `0x1a0` with a 60 s argument (`0x4270`). Correct the
  record's evidence to name `InitInternal 0x006061f8` and the two branch addresses.
- No other M7 record claims a per-class run path, so nothing else changes.

## NEW steps no record covers

- N1. Every detailed row above (DriveOffCharger, ExpressNeeds, Singing, AcknowledgeCubeMoved,
  ReactToOnCharger, ReactToImpact's branches).
- N2. The behaviour classes' own `SmartDisableReactionsWithLock` tables (`0xc73d36`, `0xc6f590`,
  `0xc74182`, `0xc72bb2`, `0xc672f0`) differ per class; none is recorded.
- N3. The `"WaitForOnTreads"` sub-behaviour name shared by DriveOffCharger and AcknowledgeCubeMoved.
- N4. `ReactToOnCharger` sets the robot disconnect reason to 4 and pushes idle trigger `0x23f`.

## Open questions for the manager

- Q1. The remaining ~65 classes are the bulk of M7. Split them into batches by M12/M13/M14/M15
  ownership, as part 2 asked, or keep them RECOVERABLE_GAP for the integrator?
- Q2. `BehaviorWait` has no `*Internal` symbol; is it driven by `executableBehaviorType`? A focused
  pass should find the factory.
- Q3. The per-class reaction-lock tables are data addresses; record them per class, or one record?


## Appendix F - X3 part 6

# Job X3 - M7-behaviour extraction, part 6: per-class run logic (remaining classes)

Agent: opencode-w1 (DeepSeek). Read-only extraction. Date: 2026-09-28.

This part completes the per-class run-logic pass begun in part 5. It is the union of three focused
sub-extractions (reactions; freeplay/needs; misc/faces/dev) run for job X3. Every row's citation is
an instruction address in `resources/lib/armeabi-v7a/libcozmoEngine.so` that was disassembled; the
Ghidra pseudo-C was used only to navigate. The responsible worker spot-checked the reaction-lock
table bytes at their `.rodata` addresses, the constants `13000.0` (`0x6094fc`), `14400.0` (`0x5c8b28`),
`100.0` (`0x60841c`, `0x609990`), and a sample of instructions (`AcknowledgeFace::InitInternal
0x6028c8`, `ReactToRobotShaken::UpdateInternal 0x609204`, `RollBlock::UpdateInternal 0x5c8a54`,
`Singing::UpdateInternal 0x5ef188`, `RequestGame_InitInternal 0x5ea6e2`), and they match.

**Correction found:** the part-2 table's `PlayAnim` `InitInternal 0x5c0648` is actually
`BehaviorPlayAnimSequenceWithFace::InitInternal`. The real `BehaviorPlayAnimSequence::InitInternal` is
`0x5c014e` (it calls `StartPlayingAnimations 0x4b2b38`). `0x5c013a` is
`BehaviorPlayAnimSequence::IsRunnableInternal`; `0x5bfef8` is
`BehaviorPlayAnimOnNeedsChange::StopInternal`. `BehaviorPlayAnimSequence` has no `StopInternal` symbol.
Also, `RequestGameSimple`'s virtuals are named `RequestGame_InitInternal` / `RequestGame_UpdateInternal`
/ `RequestGame_StopInternal`, so the part-2 dashes for that class were a naming artefact.

## Reactions

| class | function | what it does | citation | classification |
|---|---|---|---|---|
| AcknowledgeFace | InitInternal | sets `this+0x12C = 1`; returns 0 | `0x6028c8 movs r1,#1` / `0x6028ca strb.w r1,[r0,#0x12c]` | EXACT_SOURCE |
| AcknowledgeFace | UpdateInternal | if `+0x12C` set: clear it and `BeginIteration(robot)`; then tail `IBehavior::UpdateInternal` | `0x60292e ldrb.w r0,[r5,#0x12c]` / `0x602938 strb.w r0,[r5,#0x12c]` / `0x60293e blx 0x4b615c` / `0x60294a b.w 0x8cc12c` | EXACT_SOURCE |
| AcknowledgeFace | StopInternal | walks/destroys the desired-face set at `+0x120..+0x134` | `0x6028d6 add.w r5,r4,#0x134` / `0x602914 blx 0x4a5ce0` | EXACT_SOURCE |
| AcknowledgeFace | IsRunnableInternal | `+0x128 != 0` | `0x6028bc ldr.w r0,[r0,#0x128]` | EXACT_SOURCE |
| AcknowledgeObject | InitInternal | `+0x148 = 1`; if the object at `+0x144` has `[obj+0x18] == -1` calls its vtable `+0x24` and logs; returns 0 | `0x603440 strb.w r1,[r4,#0x148]` / `0x60344c ldr r1,[r1,#0x24]` / `0x60346a blx 0x4a5b84` | EXACT_SOURCE |
| AcknowledgeObject | UpdateInternal | if `+0x148` set: clear it and `BeginIteration`; then tail `IBehavior::UpdateInternal` | `0x60352a ldrb.w r0,[r5,#0x148]` / `0x60353a blx 0x4b6270` / `0x603546 b.w 0x8cc12c` | EXACT_SOURCE |
| AcknowledgeObject | StopInternal | virtual dtors on `+0x158..+0x15C`, `+0x140 = -1`, destroy tree `+0x14C`, `+0x148 = 0` | `0x6042a2 mov.w r0,#-1` / `0x6042a6 str.w r0,[r4,#0x140]` / `0x6042ae blx 0x4a5ce0` / `0x6042c0 strb.w r1,[r4,#0x148]` | EXACT_SOURCE |
| AcknowledgeObject | IsRunnableInternal | `+0x154 != 0` | `0x6042c8 ldr.w r0,[r0,#0x154]` | EXACT_SOURCE |
| RamIntoBlock | InitInternal | `[[robot+0x284]+8] == -1` (not carrying) -> `TransitionToTurningToBlock`, else `TransitionToPuttingDownBlock` | `0x604766 ldr.w r2,[r1,#0x284]` / `0x60476c adds r2,#1` / `0x604770 blx 0x4b63a8` / `0x604776 blx 0x4b63b4` | EXACT_SOURCE |
| RamIntoBlock | StopInternal | `+0x11C = -1` | `0x604990 mov.w r1,#-1` / `0x604994 str.w r1,[r0,#0x11c]` | EXACT_SOURCE |
| RamIntoBlock | IsRunnableInternal | `+0x11C > -1` | `0x604754 ldr.w r1,[r0,#0x11c]` / `0x60475a cmp.w r1,#-1` / `0x60475e it gt` | EXACT_SOURCE |
| ReactToCliff | InitInternal | emotion event `"CliffReact"` at `0x604ef4`; lock table `0xC73746`; dispatch on `+0x11C` (0 main, 1 `+0x120=1` + `TransitionToPlayingCliffReaction`, else `sErrorF`); stores cliff halfword `[robot+0x288+0xC]` into `+0x122`; `StartActing` with `TransitionToPlayingStopReaction` or `TransitionToPlayingCliffReaction` when severity `<2` | `0x604d76 bl 0x4e02b2` / `0x604d84 blx 0x4ab3c8` / `0x604da0 blx 0x4b28ec` / `0x604dca ldrh r0,[r0,#0xc]` / `0x604dce strh.w r0,[r4,#0x122]` / `0x604e82 blx 0x4b6468` | EXACT_SOURCE (severity field meaning UNKNOWN) |
| ReactToCliff | UpdateInternal | if `+0x125` set: clear it and return 2 | `0x605408 ldrb.w r2,[r0,#0x125]` / `0x605410 strb.w r1,[r0,#0x125]` / `0x605414 movs r0,#2` | EXACT_SOURCE |
| ReactToCliff | StopInternal | `+0x11C = 0`, halfword `+0x120 = 0` | `0x6053fc movs r1,#0` / `0x6053fe str.w r1,[r0,#0x11c]` / `0x605402 strh.w r1,[r0,#0x120]` | EXACT_SOURCE |
| ReactToCliff | IsRunnableInternal | always true | `0x604d4c movs r0,#1` | EXACT_SOURCE |
| ReactToCliff | HandleWhileRunning | tag `0x39` ChargerEvent: first byte nonzero -> `+0x125=1`; tag `0x22` CliffEvent: `[obj+4]!=0` and `+0x120==0` -> log, `+0x121=eventByte`, `+0x120=1` | `0x60558a cmp r0,#0x39` / `0x6055f0 strb.w r0,[r4,#0x125]` / `0x605598 ldrb r5,[r0,#4]` / `0x6055da strb.w r5,[r4,#0x121]` / `0x6055de strb.w r0,[r4,#0x120]` | EXACT_SOURCE |
| ReactToCliff | TransitionToPlayingStopReaction `0x604f64` | if `+0x124` set -> `SendFinishedReactToCliffMessage`; else `CompoundActionParallel` of `TriggerLiftSafeAnimationAction(0x19E, 60.0)` plus an action built at `0x55b554` with `0.55`, then `StartActing(TransitionToPlayingCliffReaction)` | `0x604fa2 ldrb.w r0,[r4,#0x124]` / `0x604fe4 mov.w r2,#0x19e` / `0x60502c movt r3,#0x3f0c` / `0x605034 bl 0x55b554` / `0x60506e blx 0x4b6468` | EXACT_SOURCE (`0x55b554` ctor not read) |
| ReactToCliff | TransitionToPlayingCliffReaction `0x6050f0` | plays `"PlayingCliffReaction"` (20 chars at `0x605200`) via `0x5c0ca8`; if `+0xD9` or `+0xD8` -> `TransitionToBackingUp` | `0x605108 bl 0x4e02b2` / `0x605110 bl 0x5c0ca8` / `0x605122 ldrb.w r0,[r4,#0xd9]` / `0x605132 blx 0x4b6480` | EXACT_SOURCE (`0x5c0ca8` not read) |
| ReactToFrustration | InitInternal | `PushDrivingAnimations(handler=[robot+0x24C], animations=0xC73C00, name=this+0x40)`; if `+0x12C == 0x23F` warns "We decided to run the reaction, but there is no valid one. this is a bug" and returns 1, else `TransitionToReaction` | `0x605c20 blx 0x4a5800` / `0x605c28 movw r1,#0x23f` / `0x605c3c blx 0x4a4540` / `0x605c6e blx 0x4b654c` | EXACT_SOURCE (driving-anim list not decoded) |
| ReactToFrustration | StopInternal | `RemoveDrivingAnimations(name=this+0x40)` | `0x605d84 ldr.w r2,[r1,#0x24c]` / `0x605d8e b.w 0x8cabcc` | EXACT_SOURCE |
| ReactToFrustration | TransitionToReaction `0x605cd4` | `TriggerLiftSafeAnimationAction(trigger = +0x12C, 60.0)`, then `StartActing` | `0x605cee ldr.w r2,[r4,#0x12c]` / `0x605d0a blx 0x4a577c` / `0x605d22 blx 0x4b2b5c` | EXACT_SOURCE |
| ReactToMotorCalibration | InitInternal | logs; lock table `0xC74032`; `WaitAction(robot, 5.0f)`; `StartActing` | `0x606612 blx 0x4a4f90` / `0x606642 blx 0x4b28ec` / `0x606652 movt r2,#0x40a0` / `0x60666e blx 0x4b297c` | EXACT_SOURCE |
| ReactToMotorCalibration | IsRunnableInternal | always true | `0x6065ec movs r0,#1` | EXACT_SOURCE |
| ReactToMotorCalibration | HandleWhileRunning | only tag `0x1E`; if `IsHeadCalibrated()` and `IsLiftCalibrated()` -> `StopActing(true,false)`; other events log "Calling HandleWhileRunning with an event we don't care about, this is a bug" | `0x606742 cmp r0,#0x1e` / `0x606748 blx 0x4a9a18` / `0x606752 blx 0x4ab14c` / `0x606798 blx 0x4b2160` / `0x6067ae blx 0x4a4108` | EXACT_SOURCE |
| ReactToPet | InitInternal | logs, `BeginIteration`; returns 0 | `0x606f16 blx 0x4a505c` / `0x606f40 blx 0x4b6690` | EXACT_SOURCE |
| ReactToPet | UpdateInternal | `+0x128==0` -> `EndIteration` return 2; else if `+0x12C <= -1.0f` or `+0x12C >= now` return 1; else `EndIteration` return 2 | `0x6072cc ldr.w r0,[r4,#0x128]` / `0x6072e2 ble` / `0x6072fc bpl` / `0x607300 blx 0x4b66b4` / `0x607304 movs r0,#2` / `0x607308 movs r0,#1` | EXACT_SOURCE |
| ReactToPet | StopInternal | destroy set `+0x11C`, `+0x128=0`, `+0x12C=-1.0f` | `0x60747a movt r0,#0xbf80` / `0x60747e strd r6,r0,[r4,#0x128]` / `0x607486 blx 0x4a5ce0` | EXACT_SOURCE |
| ReactToPet | IsRunnableInternal | `+0x128 != 0` | `0x606ed8 ldr.w r1,[r0,#0x128]` | EXACT_SOURCE |
| ReactToPet | BeginIteration `0x606fa8` | `GetAnimationTrigger(petType)` -> `TriggerAnimationAction(trigger, 60.0)`; `TurnTowardsImagePointAction`; `TrackPetFaceAction` + `SetUpdateTimeout(3.0f)` | `0x607042 blx 0x4b669c` / `0x607106 blx 0x4a9448` / `0x6070e0 blx 0x4a9628` / `0x607118 blx 0x4a955c` / `0x607120 movt r1,#0x4040` / `0x607124 blx 0x4a9568` | EXACT_SOURCE (trigger IDs UNKNOWN) |
| ReactToPickup | InitInternal | `+0x120=0`, `+0x124=1.0f`; `WaitAction(robot, 0.5f)`; `StartActing(StartAnim)` | `0x607758 movt r0,#0x3ff0` / `0x60775e strd r1,r0,[r4,#0x120]` / `0x60776c mov.w r2,#0x3f000000` / `0x607780 blx 0x4b6774` | EXACT_SOURCE |
| ReactToPickup | UpdateInternal | running -> 1; require `robot+0x355==1`, `robot+0x338==0`; after `+0x11C`: `GetCliffDataRaw(0)>>4 <= 0x18` -> `StartAnim`, else `CalibrateMotorAction(robot,1,0)` | `0x607bba ldr.w r0,[r4,#0x84]` / `0x607bc4 ldrb.w r0,[r5,#0x355]` / `0x607bcc ldrb.w r0,[r5,#0x338]` / `0x607c4a cmp r0,#0x18` / `0x607c9a blx 0x4a9304` | EXACT_SOURCE (field meanings UNKNOWN) |
| ReactToPickup | StopInternal | empty | `0x607d9c bx lr` | EXACT_SOURCE |
| ReactToPickup | IsRunnableInternal | always true | `0x60774c movs r0,#1` | EXACT_SOURCE |
| ReactToPickup | StartAnim `0x607820` | face path: trigger `0x1A9`, or `0xE6`; no-face path: `0x185`, or `0x184`; a `SayTextAction` path and a random retry timer `[0x120] + RandDbl(3x,6x)` | `0x607958 movw r2,#0x1a9` / `0x607968 movne r2,#0xe6` / `0x6079a4 movw r2,#0x185` / `0x6079b0 moveq.w r2,#0x184` / `0x607a12 blx 0x4a94e4` / `0x607a9a vmul.f64 d0,d8,d0` | EXACT_SOURCE |
| ReactToPlacedOnSlope | InitInternal | lock table `0xC745E0`; if `now - +0x120 < 10.0` and `+0x11C != 0` -> log pitch + `CalibrateMotorAction(robot,1,0)`; else `TriggerAnimationAction` `0x1A8` / `0x145` / `0x139` (60.0), then `StartActing(CheckPitch)`; `+0x11C=0`, `+0x120=now` | `0x607ffe blx 0x4b28ec` / `0x60800e vmov.f64 d1,#1.000000e+01` / `0x6080d6 movweq r2,#0x145` / `0x6080de movweq r2,#0x139` / `0x6080e8 blx 0x4a9448` / `0x6080f8 blx 0x4b684c` / `0x60810c vstr d8,[r4,#0x120]` | EXACT_SOURCE |
| ReactToPlacedOnSlope | IsRunnableInternal | always true | `0x607fd8 movs r0,#1` | EXACT_SOURCE |
| ReactToPlacedOnSlope | CheckPitch `0x608240` | pitch `> 10.0` -> `+0x11C=1` else 0 | `0x608254 vmov.f32 s0,#1.000000e+01` / `0x608268 movgt r0,#1` / `0x60826a strb.w r0,[r4,#0x11c]` | EXACT_SOURCE |
| ReactToPyramid | InitInternal | `+0x11C = now + 100.0f` | `0x60840c vldr s0,[pc,#0xc]` (100.0 at `0x60841c`) / `0x608416 vstr s0,[r4,#0x11c]` | EXACT_SOURCE |
| ReactToPyramid | IsRunnableInternal | copies Pyramid config `[[robot+0x34]+0x94]+0x64`; false if empty, else `now > +0x11C` | `0x608334 ldr.w r0,[r0,#0x94]` / `0x608338 add.w r1,r0,#0x64` / `0x60835a vcmpe.f32 s0,s2` / `0x608362 bgt` | EXACT_SOURCE |
| ReactToStackOfCubes | InitInternal | `+0x11C = (u32)(now + 100.0f)` | `0x60997c vldr s0,[pc,#0x10]` (100.0 at `0x609990`) / `0x60998a vstr s0,[r4,#0x11c]` | EXACT_SOURCE |
| ReactToStackOfCubes | IsRunnableInternal | copies StackOfCubes `[[robot+0x34]+0x94]+0x2C`; false if empty, else `now > (float)+0x11C` | `0x6098a4 add.w r1,r0,#0x2c` / `0x6098ca vcmpe.f32 s0,s2` / `0x6098d2 bgt` | EXACT_SOURCE |
| ReactToReturnedToTreads | InitInternal | `WaitAction(robot, 0.5f)`; `StartActing(CheckForHighPitch)` | `0x608510 mov.w r2,#0x3f000000` / `0x608524 blx 0x4b690c` | EXACT_SOURCE |
| ReactToReturnedToTreads | StopInternal | empty | `0x608718 bx lr` | EXACT_SOURCE |
| ReactToReturnedToTreads | IsRunnableInternal | always true | `0x6084fc movs r0,#1` | EXACT_SOURCE |
| ReactToReturnedToTreads | CheckForHighPitch `0x6085c4` | `abs(pitch) > 10.0` -> `CalibrateMotorAction(robot,1,0)` | `0x6085e4 vmov.f32 s0,#1.000000e+01` / `0x608658 blx 0x4a9304` | EXACT_SOURCE |
| ReactToRobotOnBack | InitInternal | calls `FlipDownIfNeeded(robot)` | `0x6087d6 blx 0x4b6990` | EXACT_SOURCE |
| ReactToRobotOnBack | FlipDownIfNeeded `0x6087e0` | only if `robot+0x355==2`: `GetCliffDataRaw(0)>>4 <= 0x18` -> `TriggerAnimationAction` `0xCB`/`0xE4` (60.0) then `StartActing(DelayThenFlipDown)`, else `CalibrateMotorAction(robot,1,0)`; `robot+0x355 != 2` tail-calls veneer `0x8cc10c` with `r1=0x21` | `0x6087ea ldrb.w r0,[r5,#0x355]` / `0x608802 cmp r0,#0x18` / `0x608820 movs r2,#0xcb` / `0x608830 movne r2,#0xe4` / `0x608892 blx 0x4a9304` / `0x6088a2 blx 0x4b699c` / `0x60884e b.w 0x8cc10c` | EXACT_SOURCE (veneer target not resolved) |
| ReactToRobotOnBack | StopInternal / IsRunnableInternal | empty / always true | `0x6089fc bx lr` / `0x6087d0 movs r0,#1` | EXACT_SOURCE |
| ReactToRobotOnFace | InitInternal | calls `FlipOverIfNeeded(robot)` | `0x608aba blx 0x4b6a20` | EXACT_SOURCE |
| ReactToRobotOnFace | FlipOverIfNeeded `0x608ac4` | only if `robot+0x355==5`: `TriggerAnimationAction` `0xA6` / `0xA5` (`robot+0x300 < 0`) / `0xE5` (60.0), then `StartActing(DelayThenCheckState)` | `0x608ad2 ldrb.w r0,[r6,#0x355]` / `0x608ade vldr s16,[r6,#0x300]` / `0x608b0e mov.w r2,#0xa6` / `0x608b20 movmi r2,#0xa5` / `0x608b26 movne r2,#0xe5` / `0x608b38 blx 0x4b6a2c` | EXACT_SOURCE |
| ReactToRobotOnFace | StopInternal / IsRunnableInternal | empty / always true | `0x608c98 bx lr` / `0x608ab4 movs r0,#1` | EXACT_SOURCE |
| ReactToRobotOnSide | InitInternal | `+0x11C = -1.0f`; `NeedActionCompleted(0x2F)`; `ReactToBeingOnSide(robot)` | `0x608d62 movt r0,#0xbf80` / `0x608d68 str.w r0,[r5,#0x11c]` / `0x608d70 blx 0x4b3090` / `0x608d78 blx 0x4b6ab0` | EXACT_SOURCE |
| ReactToRobotOnSide | ReactToBeingOnSide `0x608d80` | `robot+0x355==3` -> `0x1A6`; `==4` -> `0x1A7`; else sentinel `0x23F`; `TriggerAnimationAction` (60.0), `StartActing(AskToBeRighted)` | `0x608d8c movw r7,#0x23f` / `0x608d98 moveq.w r7,#0x1a6` / `0x608da0 movweq r7,#0x1a7` / `0x608dca blx 0x4a9448` / `0x608dda blx 0x4b6abc` | EXACT_SOURCE |
| ReactToRobotOnSide | StopInternal / IsRunnableInternal | empty / always true | `0x609064 bx lr` / `0x608d58 movs r0,#1` | EXACT_SOURCE |
| ReactToRobotShaken | InitInternal | lock table `0xC750E0`; if AI process `!= 3` `ClearSevereNeedExpression`; `+0x120=0`, `+0x124=now`, `+0x128=0`, `+0x12C=0`; `TriggerAnimationAction(0x89, 60.0)`; `StartActing`; `+0x11C=0` | `0x609144 blx 0x4b28ec` / `0x609154 blxne 0x4aaedc` / `0x60918a movs r2,#0x89` / `0x60918e blx 0x4a9448` / `0x6091b6 str.w r0,[r4,#0x11c]` | EXACT_SOURCE |
| ReactToRobotShaken | UpdateInternal | `tbb [pc,+0x11C]` over 0..4 (`0x609228`): state 0 tracks max accel `[robot+0x37C]` into `+0x120`, threshold `13000.0` (`0x6094fc`), on reach `+0x11C=1`, `+0x128=now-+0x124`; state 1 `StopActing`, triggers `0x8A` then `0x8B` in a `CompoundActionSequential`, state 2; state 2 `robot+0x355==0` -> state 3, elif no action -> state 4 `+0x12C=4`; state 3 `StopActing`, duration `>5.0`->`0x86`, `>2.5`->`0x87`, else `0x88`, `NeedActionCompleted(0xE/0xF/0x10)`, state 4; state 4 no action -> `BehaviorObjectiveAchieved(0x24,true)` return 2 | `0x609228 tbb [pc,r0]` / `0x609232 vldr s0,[r5,#0x37c]` / `0x609238 vldr s2,[pc,#0x2c0]` (13000.0 at `0x6094fc`) / `0x609350 movs r2,#0x8a` / `0x609374 movs r2,#0x8b` / `0x6092b0 vmov.f32 s0,#5.000000e+00` / `0x6093ba vmov.f32 s0,#2.500000e+00` / `0x6093e0 movs r2,#0x87` / `0x609428 movs r2,#0x88` | EXACT_SOURCE |
| ReactToRobotShaken | StopInternal | logs reaction name indexed by `+0x12C` from table `0x102C990` = `{None, Soft, Medium, Hard, StillPickedUp}`, duration `+0x128*1000`, accel `+0x120*1000` | `0x609510 vldr s0,[pc,#0x224]` (1000.0 at `0x609738`) / `0x60961a ldr.w r2,[r1,r0,lsl #2]` / `0x609624 blx 0x4a50e0` | EXACT_SOURCE |
| ReactToSparked | InitInternal | `TriggerEmotionEvent("SparkPending", now)` | `0x60980c adr r1,#0x44` / `0x60981c bl 0x4e02b2` / `0x609826 blx 0x4ab3c8` | EXACT_SOURCE |
| ReactToSparked | IsRunnableInternal | always true | `0x609864 movs r0,#1` | EXACT_SOURCE |
| ReactToUnexpectedMovement | InitInternal | `TriggerEmotionEvent("ReactToUnexpectedMovement", now)`; `TriggerLiftSafeAnimationAction` `0x1AC` / `0x1AE` / `0x1AD` (60.0), `tracksToLock = 4` when `+0x11C==2` else 0; `StartActing` | `0x609acc adr r1,#0x104` / `0x609ae6 bl 0x4e02b2` / `0x609af4 blx 0x4ab3c8` / `0x609b22 moveq r2,#4` / `0x609b30 mov.w r2,#0x1ac` / `0x609b3c moveq.w r2,#0x1ae` / `0x609b44 movweq r2,#0x1ad` / `0x609b4e blx 0x4a577c` | EXACT_SOURCE |
| ReactToUnexpectedMovement | IsRunnableInternal | always true | `0x609abc movs r0,#1` | EXACT_SOURCE |

## Freeplay / needs / navigation

| class | function | what it does | citation | classification |
|---|---|---|---|---|
| BuildPyramid | InitInternal `0x5dbefe` | `ResetMemberVars`; reads BlockConfigurationManager `robot+0x34->+0x94`; if `+0x15c` and base/top ids differ -> `TransitionToReactingToPyramid`; else top `[+0x128]` vs static `[+0x130]`: differ -> `TransitionToPlacingTopBlock` (top `-1` -> `TransitionToDrivingToTopBlock`); equal -> base `[+0x120]` != -1 -> `TransitionToPlacingBaseBlock` else `TransitionToDrivingToBaseBlock` | `0x5dbf04 blx ResetMemberVars` / `0x5dbf26 blx TransitionToReactingToPyramid` / `0x5dbf56 blx TransitionToPlacingTopBlock` / `0x5dbf60 blx TransitionToDrivingToTopBlock` / `0x5dbf74 blx TransitionToPlacingBaseBlock` / `0x5dbf7e blx TransitionToDrivingToBaseBlock` | EXACT_SOURCE |
| BuildPyramid | UpdateInternal | shared with Base (vtable uses `0x5dd01c`) | `0x5dd01c` | EXACT_SOURCE |
| BuildPyramid | IsRunnableInternal `0x5dbed0` | `UpdatePyramidTargets`; 1 iff base `[+0x120]`, static `[+0x128]`, top `[+0x130]` all != -1 | `0x5dbed4 blx UpdatePyramidTargets` / `0x5dbef2 ldr r1,[r4,#0x130]` / `0x5dbefa movne r0,#1` | EXACT_SOURCE |
| BuildPyramidBase | InitInternal `0x5dcb56` | `[+0x134]=0`, `[+0x138]`/`[+0x13c]=-1.0f`; carrying id `robot+0x284->+8` != -1 -> `TransitionToPlacingBaseBlock` else `TransitionToDrivingToBaseBlock` | `0x5dcb5c movt r2,#0xbf80` / `0x5dcb60 strd r3,r2,[r0,#0x134]` / `0x5dcb72 blx TransitionToPlacingBaseBlock` / `0x5dcb78 blx TransitionToDrivingToBaseBlock` | EXACT_SOURCE |
| BuildPyramidBase | UpdateInternal `0x5dd01c` | reads BlockConfigurationManager `[+0x48/+0x4c/+0x64/+0x68]`; stamps `+0x138`/`+0x13c` when base/static config changes; `StopWithoutImmediateRepetitionPenalty` return 2 when stable for 2.0 s (`1e-5` added) or `+0x15c` set and top==static; else `[+0x134] = (end-begin)>>3` then `IBehavior::UpdateInternal` | `0x5dd096 vmov.f32 s2,#2.0` / `0x5dd0a6 vldr s4,[pc,#0x88]` (1e-5 at `0x5dd130`) / `0x5dd110 blx StopWithoutImmediateRepetitionPenalty` / `0x5dd114 movs r0,#2` / `0x5dd11a subs r0,r1,r2` / `0x5dd12c b.w IBehavior::UpdateInternal` | EXACT_SOURCE |
| BuildPyramidBase | IsRunnableInternal `0x5dcaa4` | `UpdatePyramidTargets`; requires base/static != -1, top == -1, and PyramidBase vector `robot+0x34->+0x94->+0x48` non-empty | `0x5dcaac blx UpdatePyramidTargets` / `0x5dcac6 ldr r0,[r4,#0x130]` / `0x5dcae8 cmp r5,r6` / `0x5dcaec moveq r4,#1` | EXACT_SOURCE |
| CantHandleTallStack | InitInternal `0x5ece3c` | requires weak_ptr `+0x120` and block id `+0x11c`; `GetLocatedObjectByIdHelper`; if found copies `GetPose()` into `+0x128`, `+0x124=1`, `TransitionToLookingUpAndDown`; any missing precondition returns 1 | `0x5ece48 blx shared_weak_count::lock` / `0x5ece5e blx GetLocatedObjectByIdHelper` / `0x5ece64 blx GetPose` / `0x5ece74 strb.w r0,[r6,#0x124]` / `0x5ece7c blx TransitionToLookingUpAndDown` | EXACT_SOURCE |
| CantHandleTallStack | StopInternal `0x5ed064` | tail-calls base stop via vtable `+0x90` | `0x5ed064 ldr r1,[r0]` / `0x5ed066 ldr.w r1,[r1,#0x90]` / `0x5ed06a bx r1` | EXACT_SOURCE |
| CantHandleTallStack | IsRunnableInternal `0x5ecd64` | unlock 5 -> 0; else base vtable `+0x94`; requires weak_ptr `+0x120`, `StackOfCubes +0x11c`, `GetStackHeight() >= +0x134`; if no pose stored -> 1 else `!IsSameAs(stored pose)` | `0x5ecd70 movs r1,#5` / `0x5ecd74 blx IsUnlocked` / `0x5ecd9c blx GetStackHeight` / `0x5ecdc0 blx GetLocatedObjectByIdHelper` / `0x5ece10 blx IsSameAs` / `0x5ece14 eor r5,r0,#1` | EXACT_SOURCE |
| CheckForStackAtInterval | InitInternal `0x5d7364` | helper `[+0x140]->vtable+0x24`; if vector `+0x130 != +0x134` -> `TransitionToSetup` return 0, else 1 | `0x5d7378 blx r1` / `0x5d737a ldrd r0,r1,[r5,#0x130]` / `0x5d7386 blx TransitionToSetup` / `0x5d738e movs r0,#1` | EXACT_SOURCE |
| CheckForStackAtInterval | StopInternal `0x5d73ba` | `+0x13C = -1`; trims the vector end | `0x5d73be mov.w r3,#-1` / `0x5d73c2 str.w r3,[r0,#0x13c]` / `0x5d73dc strne.w r1,[r0,#0x134]` | EXACT_SOURCE |
| CheckForStackAtInterval | IsRunnableInternal `0x5d7328` | 0 while `now <= +0x120`; once elapsed base vtable `+0x90`; 1 iff vector `+0x130 != +0x134` | `0x5d7336 vldr s0,[r4,#0x120]` / `0x5d7354 blx r2` / `0x5d735e movne r6,#1` | EXACT_SOURCE |
| CubeLiftWorkout | InitInternal `0x5d7eb8` | lock table `0xc6b7e0` = CubeMoved, FacePositionUpdated, ObjectPositionUpdated, PetInitialDetection, UnexpectedMovement; `SmartPushIdleAnimation(0x23f)`; `GetCurrentWorkout`, `+0x11c=GetNumStrongLifts`, `+0x120=GetNumWeakLifts`; carrying -> `+0x124=1`, `+0x12c=id`, `TransitionToPostLiftAnim`, else `+0x124=0`, `GetBestObjectForIntention` -> `+0x12c`, `TransitionToPickingUpCube` | `0x5d7ec8 blx SmartDisableReactionsWithLock` / `0x5d7ee6 blx GetNumStrongLifts` / `0x5d7ef2 blx GetNumWeakLifts` / `0x5d7f18 blx TransitionToPostLiftAnim` / `0x5d7f3c blx TransitionToPickingUpCube` | EXACT_SOURCE |
| CubeLiftWorkout | UpdateInternal `0x5d81a0` | if `+0x124` but carrying id -1 -> log and `StopOnNextActionComplete`; then `IBehavior::UpdateInternal` | `0x5d81a8 ldrb.w r0,[r5,#0x124]` / `0x5d81e8 blx sChanneledInfoF` / `0x5d821e blx StopOnNextActionComplete` / `0x5d8226 blx IBehavior::UpdateInternal` | EXACT_SOURCE |
| CubeLiftWorkout | StopInternal `0x5d816c` | `UpdateBroadcastBehaviorStage(4,0)`; `StopLightAnimAndResumePrevious(0x27)` | `0x5d817c blx UpdateBroadcastBehaviorStage` / `0x5d8184 movs r1,#0x27` / `0x5d818e blx StopLightAnimAndResumePrevious` | EXACT_SOURCE |
| CubeLiftWorkout | IsRunnableInternal `0x5d7e88` | 1 if carrying; else `GetBestObjectForIntention` id != -1 | `0x5d7e8c ldr r0,[r1,#0x284]` / `0x5d7ea6 blx GetBestObjectForIntention` / `0x5d7eb0 movne r4,#1` | EXACT_SOURCE |
| EarnedSparks | InitInternal `0x5daecc` | `TriggerLiftSafeAnimationAction(trigger 0xa4, loops 1, true, 0, 60.0f, false)`, `StartActing` | `0x5daeec movt r0,#0x4270` / `0x5daefc movs r2,#0xa4` / `0x5daf00 blx TriggerLiftSafeAnimationAction` / `0x5daf0e blx StartActing` | EXACT_SOURCE |
| EarnedSparks | StopInternal `0x5daf70` | if `robot+0x34->+0x3d8` non-zero -> `SparksRewardCommunicatedToUser` | `0x5daf74 ldrb.w r1,[r0,#0x3d8]` / `0x5daf7c bne.w 0x8cc48c` | EXACT_SOURCE |
| EarnedSparks | IsRunnableInternal `0x5daec2` | returns `robot+0x34->+0x3d8` | `0x5daec6 ldrb.w r0,[r0,#0x3d8]` | EXACT_SOURCE |
| FireTruckAlarm | InitInternal `0x5dafb4` | if `IsDoingCSharpTrick()` -> nothing; else log, `StartCSharpTrick()`, `MessageEngineToGame(CSharpTrickStarted&&)`, `Broadcast` | `0x5dafc6 blx IsDoingCSharpTrick` / `0x5db008 blx StartCSharpTrick` / `0x5db012 blx MessageEngineToGame` / `0x5db01a blx Broadcast` | EXACT_SOURCE |
| FireTruckAlarm | IsRunnableInternal `0x5dafa4` | `!IsDoingCSharpTrick()` | `0x5dafa8 blx IsDoingCSharpTrick` / `0x5dafac eor r0,r0,#1` | EXACT_SOURCE |
| PounceOnMotion | InitInternal `0x5f8412` | `+0x188=0`; `InitHelper`; `TransitionToInitialPounce` | `0x5f8418 strb.w r0,[r5,#0x188]` / `0x5f8420 blx InitHelper` / `0x5f8428 blx TransitionToInitialPounce` | EXACT_SOURCE |
| PounceOnMotion | StopInternal `0x5f8728` | `Cleanup`; if `+0x188` -> `NeedActionCompleted(0)` | `0x5f872c blx Cleanup` / `0x5f873a strb.w r0,[r4,#0x188]` / `0x5f8744 b.w 0x8cc4ac` | EXACT_SOURCE |
| PounceOnMotion | IsRunnableInternal `0x5f83ba` | 1 | `0x5f83ba movs r0,#1` | EXACT_SOURCE |
| PounceOnMotion | HandleWhileRunning `0x5f9c34` | tag `0x49` RobotObservedMotion: updates boredom `+0x16c`; if state `+0x174==6` and motion <= threshold and `+0x130>=1` and `now-+0x12c >= +0x124` resets `+0x130=0`; if motion > threshold and dist `< +0x11c`: `+0x12c=now`, `+0x188=1`, `+0x130++`, `+0x134=dist`, motion ids, `StopActing(true,true)`; else logs; other tags log `...InvalidEvent` | `0x5f9c4c cmp r0,#0x49` / `0x5f9c74 vstrgt s16,[r4,#0x16c]` / `0x5f9cd8 vldr s0,[r4,#0x11c]` / `0x5f9e12 str.w r0,[r4,#0x130]` / `0x5f9e8c strh.w r0,[r4,#0x162]` / `0x5f9e92 blx StopActing` | EXACT_SOURCE |
| PeekABoo | InitInternal `0x5f66e4` | if (`+0xd9` or `+0xd8`) and `+0x168==-1`: `+0x168=0`, `TriggerAnimationAction(0x210)`; else clears map, `+0x134=0`, `+0x130=0`, `+0x120=RandIntInRange`, `SmartPushIdleAnimation(0x23f)`, lock table `0xc70960` = CubeMoved, FacePositionUpdated, FistBump, ObjectPositionUpdated, PetInitialDetection; `+0x155` -> `TransitionToIntroAnim` else `TransitionTurnToFace` | `0x5f6736 mov.w r2,#0x210` / `0x5f678a blx RandIntInRange` / `0x5f6796 movw r2,#0x23f` / `0x5f67a8 blx SmartDisableReactionsWithLock` / `0x5f67b6 blx TransitionToIntroAnim` / `0x5f67c8 blx TransitionTurnToFace` | EXACT_SOURCE (trigger 0x210 meaning UNKNOWN) |
| PeekABoo | UpdateInternal `0x5f69d8` | `UpdateTimestampSets`; `WasFaceHiddenAfterTimestamp`; state `+0x13c==3` hidden -> `StopActing` + `TransitionWaitToSeeFace`; state `4` not hidden -> `StopActing` + `TransitionSeeFaceAfterHiding`; then base | `0x5f69de blx UpdateTimestampSets` / `0x5f69ee blx WasFaceHiddenAfterTimestamp` / `0x5f6a10 blx TransitionWaitToSeeFace` / `0x5f6a26 blx TransitionSeeFaceAfterHiding` / `0x5f6a32 b.w IBehavior::UpdateInternal` | EXACT_SOURCE |
| PeekABoo | StopInternal `0x5f6f20` | `+0x128 = now + +0x158` | `0x5f6f28 blx GetCurrentTimeInSeconds` / `0x5f6f34 vadd.f32 s0,s2,s0` / `0x5f6f38 vstr s0,[r4,#0x128]` | EXACT_SOURCE |
| PeekABoo | IsRunnableInternal `0x5f65ec` | if `robot+0x44->+0x58 == 0x25` and `+0x5c==0` and `+0x168>0` and `now > +0x168`: `+0x168=-1.0f` return 1; else `+0x11c=0`; `now < +0x128` -> 0; else needs `GetInteractionFace()!=0` and `IsFeatureEnabled(4)` | `0x5f6602 cmp r0,#0x25` / `0x5f6614 str [r5,#0x11c]=0` / `0x5f6628 blx GetInteractionFace` / `0x5f6630 movs r1,#4` / `0x5f6638 b.w 0x8cc73c` / `0x5f6662 movs r0,#1` | EXACT_SOURCE |
| PutDownBlock | InitInternal `0x5c7fd0` | lock table `0xc68b20` (all false, disables nothing); `GetRNG` + `RandDblInRange`; `DriveStraightAction(random, 100.0f)`; `TriggerAnimationAction(0x19a)`; `CompoundActionSequential`; `IncreaseScoreWhileActing(5.0f)`; `StartActing(LookDownAtBlock)` | `0x5c7fe2 blx SmartDisableReactionsWithLock` / `0x5c8002 blx RandDblInRange` / `0x5c8034 blx DriveStraightAction` / `0x5c8050 mov.w r2,#0x19a` / `0x5c8076 blx CompoundActionSequential` / `0x5c8084 blx IncreaseScoreWhileActing` / `0x5c8096 blx StartActing` | EXACT_SOURCE |
| PutDownBlock | IsRunnableInternal `0x5c7fb0` | 1 if carrying; else `+0x84 != 0` | `0x5c7fb0 ldr.w r1,[r1,#0x284]` / `0x5c7fbe ldr.w r0,[r0,#0x84]` / `0x5c7fc6 movne r0,#1` | EXACT_SOURCE |
| RollBlock | InitInternal `0x5c8676` | `+0x129=0`; `GetLocatedObjectById(+0x120)`; null -> 1; else `UpdateTargetsUpAxis` + `TransitionToPerformingAction(false)` | `0x5c867e strb.w r0,[r5,#0x129]` / `0x5c868c blx GetLocatedObjectByIdHelper` / `0x5c8696 blx UpdateTargetsUpAxis` / `0x5c86a2 blx TransitionToPerformingAction` | EXACT_SOURCE |
| RollBlock | UpdateInternal `0x5c8a54` | target null or `+0x130!=0` -> base; else pose, `GetRotationMatrix`, `GetRotatedParentAxis<90>`; if axis != `+0x12c`, `ComputeDistanceSQBetween`; if moved^2 > `14400.0f` (`0x5c8b28`): `UpdateTargetsUpAxis`, `StopActing`, `+0x129` -> `TransitionToRollSuccess` else base stop and `+0x124!=-1` -> `TransitionToPerformingAction(false)` | `0x5c8a72 ldr.w r0,[r5,#0x130]` / `0x5c8a96 blx GetRotatedParentAxis<90>` / `0x5c8ac6 vldr s2,[pc,#0x60]` (14400.0 at `0x5c8b28`) / `0x5c8ae2 blx StopActing` / `0x5c8af0 blx TransitionToRollSuccess` / `0x5c8b10 blx TransitionToPerformingAction` | EXACT_SOURCE |
| RollBlock | StopInternal `0x5c8c94` | `+0x124 = -1` | `0x5c8c94 mov.w r1,#-1` / `0x5c8c98 str.w r1,[r0,#0x124]` | EXACT_SOURCE |
| RollBlock | IsRunnableInternal `0x5c8650` | base vtable `+0x90`; 1 if `+0x124!=-1` else `+0x84!=0` | `0x5c8656 ldr.w r2,[r0,#0x90]` / `0x5c865e ldr.w r0,[r4,#0x124]` / `0x5c866a ldr.w r0,[r4,#0x84]` | EXACT_SOURCE |
| StackBlocks | InitInternal `0x5c9502` | carrying id `robot+0x284->+8` == `+0x128` -> `TransitionToStackingBlock` else `TransitionToPickingUpBlock` | `0x5c9504 ldr.w r2,[r1,#0x284]` / `0x5c9508 ldr.w r3,[r0,#0x128]` / `0x5c9512 blx TransitionToStackingBlock` / `0x5c9518 blx TransitionToPickingUpBlock` | EXACT_SOURCE |
| StackBlocks | UpdateInternal `0x5c9844` | unlock 9 or `+0x134` choose intention 4/5; `GetValidObjectsForIntention` top/bottom; `find +0x124`/`+0x12c`; both valid -> `FindObjectOnTopOrUnderneathHelper(top, 15.0f, filter, true)`; else log "Stopping due to invalid blocks topBlockValid:%d bottomBlockValid:%d" + `StopWithoutImmediateRepetitionPenalty` return 2; if `+0x120==1` and `+0x135==0` -> base; else `GetClosestValidBottom`, if changed -> `StopActing`, `+0x135=1`, `+0x130=new`, `TransitionToStackingBlock` | `0x5c9858 blx IsUnlocked` / `0x5c987c blx GetValidObjectsForIntention` / `0x5c991c blx StopWithoutImmediateRepetitionPenalty` / `0x5c99b8 movt r2,#0x4170` / `0x5c9a38 blx IBehavior::UpdateInternal` / `0x5c9a5e blx GetClosestValidBottom` / `0x5c9a72 blx StopActing` / `0x5c9a84 blx TransitionToStackingBlock` | EXACT_SOURCE |
| StackBlocks | StopInternal `0x5c97f8` | `+0x128=-1`, `+0x135=0`, `+0x130=-1` | `0x5c97f8 mov.w r1,#-1` / `0x5c9802 strb.w r2,[r0,#0x135]` / `0x5c9806 str.w r1,[r0,#0x130]` | EXACT_SOURCE |
| StackBlocks | IsRunnableInternal `0x5c9484` | `UpdateTargetBlocks`; 0 if `+0x130==-1` else 1 iff `+0x128!=-1` | `0x5c9488 blx UpdateTargetBlocks` / `0x5c948c ldr.w r0,[r4,#0x130]` / `0x5c94a2 movne r0,#1` | EXACT_SOURCE |
| PickUpCube | InitInternal `0x5c6644` | `+0xd9` or `+0xd8` -> `TransitionToPickingUpCube` else `TransitionToDoingInitialReaction` | `0x5c6646 ldrb.w r2,[r0,#0xd9]` / `0x5c6652 blx TransitionToPickingUpCube` / `0x5c665a blx TransitionToDoingInitialReaction` | EXACT_SOURCE |
| PickUpCube | UpdateInternal `0x5c6824` | iterates `+0x124..+0x128`, `IsObjectPartOfConfigurationType(+0x11c, id)`; if any part -> `StopWithoutImmediateRepetitionPenalty` return 2; else base | `0x5c682c ldrd r7,r4,[r8,#0x124]` / `0x5c6844 blx IsObjectPartOfConfigurationType` / `0x5c685c blx StopWithoutImmediateRepetitionPenalty` / `0x5c6856 b.w IBehavior::UpdateInternal` | EXACT_SOURCE |
| PickUpCube | IsRunnableInternal `0x5c6628` | base vtable `+0x7c`; returns `+0x120!=-1` | `0x5c662e ldr r2,[r0,#0x7c]` / `0x5c6634 ldr.w r0,[r4,#0x120]` / `0x5c6640 movne r0,#1` | EXACT_SOURCE |
| PickUpAndPutDownCube | InitInternal `0x5db6b4` | carrying -> `+0x120=id`, `TransitionToDriveWithCube`; else `GetBestObjectForIntention`, `PickupBlockParamaters`, `CreatePickupBlockHelper`, `SmartDelegateToHelper(TransitionToDriveWithCube)` | `0x5db6d0 blx TransitionToDriveWithCube` / `0x5db70a blx CreatePickupBlockHelper` / `0x5db72e blx SmartDelegateToHelper` | EXACT_SOURCE |
| PickUpAndPutDownCube | IsRunnableInternal `0x5db68c` | `GetBestObjectForIntention` -> `+0x120`; returns `+0x120!=-1` | `0x5db69e blx GetBestObjectForIntention` / `0x5db6ac movne r5,#1` | EXACT_SOURCE |
| PopAWheelie | InitInternal `0x5c7496` | `+0xd9` or `+0xd8` or `+0x120!=+0x128` -> `TransitionToPerformingAction` else `TransitionToReactingToBlock` | `0x5c7498 ldrb.w r2,[r0,#0xd9]` / `0x5c74b0 blx TransitionToPerformingAction` / `0x5c74b8 blx TransitionToReactingToBlock` | EXACT_SOURCE |
| PopAWheelie | StopInternal `0x5c76ac` | tail-calls `ResetBehavior` | `0x5c76ac b.w 0x8cc20c` | EXACT_SOURCE |
| PopAWheelie | IsRunnableInternal `0x5c7478` | base vtable `+0x90`; `+0x120!=-1` | `0x5c747e ldr.w r2,[r0,#0x90]` / `0x5c7486 ldr.w r0,[r4,#0x120]` / `0x5c7492 movne r0,#1` | EXACT_SOURCE |
| RespondPossiblyRoll | InitInternal `0x5de4ba` | clears ObjectID->UpAxis map `+0x134`, `+0x14c=0`; `DetermineNextResponse` | `0x5de4ca str.w r6,[r5,#0x14c]` / `0x5de4ce blx __tree::destroy` / `0x5de4e2 blx DetermineNextResponse` | EXACT_SOURCE |
| RespondPossiblyRoll | UpdateInternal `0x5de5c6` | `find +0x11c` in `+0x134`; found and value `+0x18!=5` -> `StopActing` + `TurnAndRespondNegatively`; clears map; base | `0x5de5d8 blx __tree::find` / `0x5de5ec blx StopActing` / `0x5de5fa blx TurnAndRespondNegatively` / `0x5de61a b.w IBehavior::UpdateInternal` | EXACT_SOURCE |
| RespondPossiblyRoll | IsRunnableInternal `0x5de4b6` | 1 | `0x5de4b6 movs r0,#1` | EXACT_SOURCE |
| OnConfigSeen | InitInternal `0x5db3ac` | `+0x134=0`; `TransitionToPlayAnimationSequence` | `0x5db3b0 str.w r2,[r0,#0x134]` / `0x5db3b4 blx TransitionToPlayAnimationSequence` | EXACT_SOURCE |
| OnConfigSeen | IsRunnableInternal `0x5db2d4` | `dt = now - +0x138`; `+0x138=now`; walks configs, `GetCacheByType`, stamps `+0x13c=now` when count grew; returns `dt < 4.99999f && abs(now - +0x13c) < 1e-5f` | `0x5db2e6 blx GetCurrentTimeInSeconds` / `0x5db316 blx GetCacheByType` / `0x5db35e vldr s4,[pc,#0x48]` (1e-5) / `0x5db37a vldr s2,[pc,#0x28]` (4.99999) / `0x5db396 ands r0,r1` | EXACT_SOURCE |
| ThinkAboutBeacons | InitInternal `0x5e5cdc` | logs name; `SelectNewBeacon`; `AnimationTriggerFromString`; if trigger != `0x23f` -> `TriggerAnimationAction(trigger)` + `StartActing` | `0x5e5d52 blx sChanneledInfoF` / `0x5e5d98 blx SelectNewBeacon` / `0x5e5dda blx AnimationTriggerFromString` / `0x5e5e18 blx TriggerAnimationAction` / `0x5e5e26 blx StartActing` | EXACT_SOURCE |
| ThinkAboutBeacons | IsRunnableInternal `0x5e5cc2` | 1 iff `AIWhiteboard::GetActiveBeacon()==0` | `0x5e5cca blx GetActiveBeacon` / `0x5e5cd4 moveq r1,#1` | EXACT_SOURCE |
| VisitInterestingEdge | InitInternal `0x5e7018` | `AddDisableRequest(1, name)`; `+0x190=0`; `TransitionToS1_MoveToVantagePoint(robot, 0)` | `0x5e702a blx AddDisableRequest` / `0x5e703a blx TransitionToS1_MoveToVantagePoint` | EXACT_SOURCE |
| VisitInterestingEdge | UpdateInternal `0x5e7418` | `tbb` over `+0x190` 0..4 (table `0x5e7428` = `[12,3,8,3,3]`): 0 -> error `InvalidState`/`_errG=1`; 1 -> base; 2 -> `StateUpdate_GatheringAccurateEdge`; 3,4 -> base | `0x5e7424 tbb [pc,r2]` / `0x5e7438 blx StateUpdate_GatheringAccurateEdge` / `0x5e7434 b.w IBehavior::UpdateInternal` / `0x5e744e blx sErrorF` | EXACT_SOURCE |
| VisitInterestingEdge | StopInternal `0x5e729c` | `RemoveDisableRequest(1, name)`; `EraseSegments`; `StopSquintLoop`; releases `+0x178`, clears `+0x174` | `0x5e72b0 blx RemoveDisableRequest` / `0x5e72d2 blx EraseSegments` / `0x5e72e8 blx StopSquintLoop` / `0x5e72fc str.w r5,[r4,#0x174]` | EXACT_SOURCE |
| VisitInterestingEdge | IsRunnableInternal `0x5e6520` | clears viz, `PickGoals`; for each goal `CheckGoalReachable`, `GenerateVantagePoints`; 1 iff a reachable vantage point | `0x5e658c blx PickGoals` / `0x5e6620 blx CheckGoalReachable` / `0x5e6700 blx GenerateVantagePoints` / `0x5e67b0 movne r0,#1` | EXACT_SOURCE for orchestration; callee internals UNKNOWN |
| LookInPlaceMemoryMap | InitInternal `0x5e4ce0` | `+0x164=0`; stores `GetAngleAroundZaxis` into `+0x150`; `assign`s the SectorStatus vector to its size, value 0; `FindAndVisitClosestVisitableSector(robot, 0, 7, 0)` | `0x5e4d08 blx GetAngleAroundZaxis` / `0x5e4d2e blx vector::assign` / `0x5e4d3c blx FindAndVisitClosestVisitableSector` | EXACT_SOURCE |
| LookInPlaceMemoryMap | StopInternal `0x5e4f2a` | no-op | `0x5e4f2a bx lr` | EXACT_SOURCE |
| LookInPlaceMemoryMap | IsRunnableInternal `0x5e4c6c` | 0 if `GetCurrentMemoryMapHelper()==0`; else 1 if every sector pose is farther than `+0x144`^2 + 1e-5 | `0x5e4c7e blx GetCurrentMemoryMapHelper` / `0x5e4cb2 blx ComputeDistanceSQBetween` / `0x5e4ca0 vldr s2,[pc,#0x38]` (1e-5 at `0x5e4cdc`) / `0x5e4ccc movs r0,#1` | EXACT_SOURCE |
| LookForFaceAndCube | InitInternal `0x5efd58` | stores yaw into `+0x17c`; clears tree `+0x18c`, bytes `+0x188`, `+0x184`; `CompoundActionParallel` of `MoveLiftToHeightAction(0, 5.0f)` and `CreateBodyAndHeadTurnAction`; `StartActing(S1_FaceOnLeft)` | `0x5efdde blx GetAngleAroundZaxis` / `0x5efe24 blx CompoundActionParallel` / `0x5efe3e blx MoveLiftToHeightAction` / `0x5efe92 blx CreateBodyAndHeadTurnAction` / `0x5efebc blx StartActing` | EXACT_SOURCE |
| LookForFaceAndCube | StopInternal `0x5f0878` | no-op | `0x5f0878 bx lr` | EXACT_SOURCE |
| LookForFaceAndCube | IsRunnableInternal `0x5efd54` | 1 | `0x5efd54 movs r0,#1` | EXACT_SOURCE |
| LookForFaceAndCube | HandleWhileRunning `0x5f087c` | tag `0x46` RobotObservedFace: if `+0x14d` and `+0x198==0`: find face id in `+0x18c`; <0 or absent -> `CancelActionAndVerifyFace` else `StopBehaviorOnFaceIfNeeded`; not `+0x14d` -> `StopBehaviorOnFaceIfNeeded(face id)` | `0x5f0888 cmp r0,#0x46` / `0x5f08ba blx __tree::find` / `0x5f08d6 b.w 0x8cc69c` / `0x5f08e6 b.w 0x8cc6ac` | EXACT_SOURCE |
| DriveInDesperation | InitInternal `0x5d8eca` | `SmartSetMotionProfile([+0x11c]+4)`; `+0x134=-1`; `TransitionToIdle` | `0x5d8ed8 blx SmartSetMotionProfile` / `0x5d8ee2 str.w r0,[r5,#0x134]` / `0x5d8ee8 blx TransitionToIdle` | EXACT_SOURCE |
| DriveInDesperation | UpdateInternal `0x5d9062` | `robot+0x355` nonzero: `+0x138==0` -> `StopActing`, `+0x138=1`; return 1. zero: `+0x138!=0` -> `+0x134=-1`, `+0x138=0`, `TransitionToIdle`; then base | `0x5d9068 ldrb.w r1,[r5,#0x355]` / `0x5d907a blx StopActing` / `0x5d9080 strb.w r0,[r4,#0x138]` / `0x5d909c blx TransitionToIdle` / `0x5d90a8 b.w IBehavior::UpdateInternal` | EXACT_SOURCE |
| DriveInDesperation | StopInternal `0x5d9060` | no-op | `0x5d9060 bx lr` | EXACT_SOURCE |
| DriveInDesperation | IsRunnableInternal `0x5d90ac` | 1 | `0x5d90ac movs r0,#1` | EXACT_SOURCE |
| DrivePath | InitInternal `0x5c0f76` | `TransitionToFollowingPath` | `0x5c0f78 blx TransitionToFollowingPath` | EXACT_SOURCE |
| DrivePath | IsRunnableInternal `0x5c0f68` | bit 2 of `robot+0x348` | `0x5c0f68 ldrb.w r0,[r1,#0x348]` / `0x5c0f6c ubfx r0,r0,#2,#1` | EXACT_SOURCE |
| DriveToFace | InitInternal `0x5da668` | valid `SmartFaceID +0x124` -> `TransitionToTurningTowardsFace`; else warn "Attempted to init behavior without a vaild face to drive to" return 1 | `0x5da674 blx IsValid` / `0x5da680 blx TransitionToTurningTowardsFace` / `0x5da696 blx sWarningF` / `0x5da6bc movs r0,#1` | EXACT_SOURCE |
| DriveToFace | UpdateInternal `0x5da8fc` | state `+0x11c==3` and timeout `+0x120 < now` -> `StopActing` return 2 | `0x5da90e cmp r1,#3` / `0x5da916 vldr s2,[r4,#0x120]` / `0x5da92a blx StopActing` / `0x5da92e movs r0,#2` | EXACT_SOURCE |
| DriveToFace | StopInternal `0x5da93e` | `SmartFaceID::Reset(this+0x124)` | `0x5da93e add.w r0,r0,#0x124` / `0x5da942 b.w 0x8cc26c` | EXACT_SOURCE |
| DriveToFace | IsRunnableInternal `0x5da588` | `GetLastObservedFace`; in world origin -> `GetFaceIDsObservedSince` -> `GetSmartFaceID`; else `Reset`; returns `IsValid` | `0x5da5cc blx IsPoseInWorldOrigin` / `0x5da5ec blx GetSmartFaceID` / `0x5da606 blx Reset` / `0x5da616 blx IsValid` | EXACT_SOURCE |
| ExploreLookAroundInPlace | InitInternal `0x5e2684` | `SmartSetMotionProfile` when `+0x13c`; clears `+0x1cc`, `+0x1c8`; `+0x123` stores yaw `+0x1f0`; `+0x122` `RandDbl(1.0)` vs `+0x140` -> `+0x1d4`; if `+0x124==0` or `+0x120==0` or carrying -1 -> base stop, else `MoveLiftToHeightAction(0,5.0f)` + `StartActing` | `0x5e26f6 blx SmartSetMotionProfile` / `0x5e273e blx RandDbl` / `0x5e2796 blx MoveLiftToHeightAction` / `0x5e27a2 blx StartActing` | EXACT_SOURCE |
| ExploreLookAroundInPlace | IsRunnableInternal `0x5e259c` | for each pose `+0x1e4..+0x1e8` computes horizontal distance^2; 1 iff no pose closer than `+0x11c` | `0x5e25ee blx GetWithRespectTo` / `0x5e2622 vldr s2,[sb,#0x11c]` / `0x5e2636 movmi r0,#1` | EXACT_SOURCE |
| ExploreVisitPossibleMarker | InitInternal `0x5e3d8c` | picks the nearest possible object; `ApproachPossibleCube(robot, type, pose)`; else `sErrorF` | `0x5e3db4 blx GetTransform` / `0x5e3e88 blx ApproachPossibleCube` / `0x5e3ede movs r2,#1` | EXACT_SOURCE |
| ExploreVisitPossibleMarker | IsRunnableInternal `0x5e3d6c` | 1 iff `GetPossibleObjectsWRTOrigin(+0x11c)` non-empty | `0x5e3d7a blx GetPossibleObjectsWRTOrigin` / `0x5e3d86 movne r0,#1` | EXACT_SOURCE |
| LookAround | InitInternal `0x5c465c` | `ResetSafeRegion` | `0x5c465e blx ResetSafeRegion` | EXACT_SOURCE |
| LookAround | UpdateInternal `0x5c58c4` | `+0x84!=0` -> 1; `+0x11c!=0` -> 2; else base | `0x5c58ca ldr.w r0,[r5,#0x84]` / `0x5c58d0 movs r0,#1` / `0x5c58da movs r0,#2` | EXACT_SOURCE |
| LookAround | StopInternal `0x5c58f4` | `ResetBehavior` | `0x5c58f4 b.w 0x8cc1cc` | EXACT_SOURCE |
| LookAround | IsRunnableInternal `0x5c409a` | 1 | `0x5c409a movs r0,#1` | EXACT_SOURCE |
| LookAround | HandleWhileRunning `0x5c40d8` | tag `0x22` ignored; `0x35` -> `ResetSafeRegion`; `0x45` -> `HandleObjectObserved(...,false,...)`; `0x44` -> `HandleObjectObserved(...,true,...)`; else log `InvalidTag` | `0x5c40fa b.w 0x8cc17c` / `0x5c4108 blx Get_RobotObservedObject` / `0x5c4128 b.w 0x8cc18c` | EXACT_SOURCE |
| TurnToFace | InitInternal `0x5ca438` | valid `SmartFaceID +0x11c` -> `TurnTowardsFaceAction(robot, faceID, pi, false)` + `StartActing`; else 1 | `0x5ca452 blx IsValid` / `0x5ca464 movw r1,#0xfdb` / `0x5ca482 blx TurnTowardsFaceAction` / `0x5ca492 blx StartActing` / `0x5ca4a4 movs r0,#1` | EXACT_SOURCE |
| TurnToFace | StopInternal `0x5ca4f8` | `SmartFaceID::Reset(this+0x11c)` | `0x5ca4f8 add.w r0,r0,#0x11c` / `0x5ca4fc b.w 0x8cc26c` | EXACT_SOURCE |
| TurnToFace | IsRunnableInternal `0x5ca36c` | `GetLastObservedFace`; `GetFaceIDsObservedSince` -> `GetSmartFaceID` into `+0x11c`; returns `IsValid` | `0x5ca3a6 blx GetLastObservedFace` / `0x5ca3c2 blx GetSmartFaceID` / `0x5ca3e0 blx IsValid` | EXACT_SOURCE |
| PyramidThankYou | InitInternal `0x5de0cc` | `CompoundActionSequential`; `GetLastObservedFace`; world origin -> `TurnTowardsFaceAction(0, pi, false)` then `TriggerAnimationAction(0x18)`; else `TurnTowardsObjectAction(+0x120, pi, true, false)` then `TriggerAnimationAction(0x18)`; `StartActing` | `0x5de128 blx IsPoseInWorldOrigin` / `0x5de160 blx TurnTowardsFaceAction` / `0x5de1b6 blx TriggerAnimationAction` / `0x5de208 blx TurnTowardsObjectAction` / `0x5de26c blx StartActing` | EXACT_SOURCE |
| PyramidThankYou | StopInternal `0x5de314` | `+0x120 = -1` | `0x5de314 mov.w r1,#-1` / `0x5de318 str.w r1,[r0,#0x120]` | EXACT_SOURCE |
| PyramidThankYou | IsRunnableInternal `0x5de078` | `HasAnyFaces(0x14,false)` -> 1; else `+0x120>=0` and `GetLocatedObjectByIdHelper(+0x120)` -> 1; else `+0x120=-1` return 0 | `0x5de07e movs r1,#0x14` / `0x5de088 blx HasAnyFaces` / `0x5de0ae blx GetLocatedObjectByIdHelper` / `0x5de0bc str.w r0,[r4,#0x120]` | EXACT_SOURCE |

## Misc / faces / dev

| class | function | what it does | citation | classification |
|---|---|---|---|---|
| Bouncer | InitInternal | lock table `0xc6fb90` = CubeMoved, FacePositionUpdated, FistBump, Frustration, Hiccup, ObjectPositionUpdated, PetInitialDetection, UnexpectedMovement; idle anim `0x23f`; reads display W/H, seeds RNG paddle/ball params; `TransitionToState(1)` | `0x5f14b8 blx SmartDisableReactionsWithLock` / `0x5f14c4 movw r2,#0x23f` / `0x5f15ae blx TransitionToState` | EXACT_SOURCE |
| Bouncer | UpdateInternal | times out at 60.0 s; target face valid, present, image age <= 5000 ms (`0x1389`); state `+0x11c`: 1->(anim 8,2), 2->(0xa,3), 3->(0xe,4), 4->(0x10,5), 5->UpdatePaddle/UpdateBall + collision anim at `0xc6fbbc` + UpdateDisplay, 6->(0xf,7), 7->(0xb,8), 8->(9,9), 9->done(2) | `0x5f19b2 vldr s0,[pc,#0x228]` (60.0 at `0x5f1bdc`) / `0x5f19fe movw r1,#0x1389` / `0x5f1ac6 ldr r0,[r5,#0x11c]` / `0x5f1b86 blx StartAnimation` | EXACT_SOURCE |
| Bouncer | StopInternal | empty | `0x5f1c6c` | EXACT_SOURCE |
| Bouncer | IsRunnableInternal | feature gate 8; >=1 face id and a valid best-face-to-track written to `+0x120` | `0x5f1346 movs r1,#8` / `0x5f134a blx IsFeatureEnabled` / `0x5f1370 blx GetBestFaceToTrack` / `0x5f1392 blx IsValid` | EXACT_SOURCE |
| Dance | InitInternal | lock table `0xc6f1c8` = CubeMoved, FacePositionUpdated, FistBump, ObjectPositionUpdated, PetInitialDetection, UnexpectedMovement; connected cubes (ObjectFamily 2) each get a random cube-anim from trigger `0x28` via `PlayLightAnim`; then `BehaviorPlayAnimSequence::InitInternal` | `0x5ed47c blx SmartDisableReactionsWithLock` / `0x5ed5b8 movs r1,#0x28` / `0x5ed600 blx PlayLightAnim` / `0x5ed670 blx BehaviorPlayAnimSequence::InitInternal` | EXACT_SOURCE |
| Dance | StopInternal | `UpdateBroadcastBehaviorStage(4,0)`; connected cubes play cube-anim `0xa` | `0x5ed99a blx UpdateBroadcastBehaviorStage` / `0x5eda86 movs r0,#0xa` / `0x5edab2 blx PlayLightAnim` | EXACT_SOURCE |
| DevTurnInPlaceTest | InitInternal | clears vector `+0x11c..+0x120`, `+0x128`; locks all reactions (`GetAffectAllArray`); `GenerateTestAction`; `StartActing` | `0x5ca918 blx GetAffectAllArray` / `0x5ca924 blx SmartDisableReactionsWithLock` / `0x5ca930 blx GenerateTestAction` / `0x5ca940 blx StartActing` | EXACT_SOURCE |
| DevTurnInPlaceTest | IsRunnableInternal | always false | `0x5ca8f6 movs r0,#0` | EXACT_SOURCE |
| DevTurnInPlaceTest | StopInternal | empty | `0x5cad30` | EXACT_SOURCE |
| DockingTestSimple | InitInternal | locks all reactions; motion profile and initial pose (250,22 mm); zeroes `+0x11c`, `+0x260`, `+0x25c`, `+0x268`, `+0x26c`; `ActionList::Cancel`; writes banner; `SendSaveImages(1)` | `0x5cb434 blx DisableReactionsWithLock` / `0x5cb48c blx SmartSetMotionProfile` / `0x5cb49a str.w r4,[sl,#0x11c]` / `0x5cb8c0 blx SendSaveImages` | EXACT_SOURCE |
| DockingTestSimple | UpdateInternal | dev loop; counter `+0x260==30` and state != 6 -> "Test Completed Successfully", `PrintStats`, state 6. States: 0 reset; 2 pick a block (`DriveToObjectAction`); 3 `DriveToPickupObjectAction` (docking trigger `0x23f`); further drive/place/repeat. Random offsets `RandDblInRange` | `0x5cbc32 cmp r0,#0x1e` / `0x5cbe82 blx DriveToObjectAction` / `0x5cc27a blx DriveToPickupObjectAction` | RECOVERABLE_GAP - full state machine not exhaustively read |
| DockingTestSimple | StopInternal | removes lock "Docking test simple"; cancels actions; `PrintStats`; `SendSaveImages(0)` | `0x5cda5e blx RemoveDisableReactionsLock` / `0x5cda88 blx ActionList::Cancel` / `0x5cdac2 blx PrintStats` / `0x5cdae6 blx SendSaveImages` | EXACT_SOURCE |
| DockingTestSimple | IsRunnableInternal | `+0x11c == 0` | `0x5cb384 ldr.w r1,[r0,#0x11c]` | EXACT_SOURCE |
| DockingTestSimple | HandleWhileRunning | tag `0x35` OffTreads resets state and re-parents to world origin; `0x44` -> `HandleObservedObject`; `0x5a` -> `HandleActionCompleted`; `0x34` -> `HandleRobotStopped` | `0x5cdc4a blx Get_RobotOffTreadsStateChanged` / `0x5cdcba blx Get_RobotCompletedAction` / `0x5cdce4 blx Get_RobotObservedObject` | EXACT_SOURCE |
| EnrollFace | InitInternal | state `+0x11c`: 4 -> `TransitionToSayingName`; 5/8 -> `TransitionToSavingToRobot`; 7 -> `TransitionToScanningInterrupted`; default sets state 1, `InitEnrollmentSettings`, on bad settings `DisableEnrollment`, else `+0x128=0`, `+0x14f=0`, `+0x140=now`, `+0x144=15.0f`, `FaceWorld::Enroll(0)`, lock table `0xc71b6c` (14 triggers), `TransitionToLookingForFace` | `0x5fceca ldrb r0,[r5,#0x11c]` / `0x5fcfb2 strb r0,[r5,#0x11c]` / `0x5fd10a blx SmartDisableReactionsWithLock` / `0x5fd116 blx TransitionToLookingForFace` | EXACT_SOURCE |
| EnrollFace | UpdateInternal | states 2/3: cancel if enrollment id empty; state 3 `HasTimedOut` -> `ScanningInterrupted` else `UpdateFaceToEnroll`; image gap > 1500 ms -> log + `TransitionToLookingForFace`; count reached -> `AssignNameToFace` + `TransitionToSayingName` | `0x5fd972 ldrb r2,[r5,#0x11c]` / `0x5fda62 blx HasTimedOut` / `0x5fda8a blx AssignNameToFace` / `0x5fda92 blx TransitionToSayingName` | EXACT_SOURCE |
| EnrollFace | StopInternal | `FaceWorld::Enroll(0)`; state `0xb` "Cancelled"; `DisableEnrollment`; on failure erase the new face; `BehaviorObjectiveAchieved(5,1)`; broadcast `FaceEnrollmentCompleted`; state 1 | `0x5fe314 blx FaceWorld::Enroll` / `0x5fe3ee blx DisableEnrollment` / `0x5fe458 blx EraseFace` / `0x5fe468 blx BehaviorObjectiveAchieved` / `0x5fe528 blx Broadcast` | EXACT_SOURCE for failure/broadcast path; per-state cleanup RECOVERABLE_GAP |
| EnrollFace | IsRunnableInternal | true iff name string `+0x17c` non-empty | `0x5fcd00 ldr.w r0,[r0,#0x17c]` | EXACT_SOURCE |
| EnrollFace | HandleWhileRunning | tags `0x22`/`0x35` (and `0x3c`, `0x47` ignored); acting: state 3 -> `StopActing` + `TransitionToEnrolling`; state 2 -> `TransitionToLookingForFace`; else log `UnexpectedEngineToGameTag` | `0x5ff566 cmp r0,#0x3b` / `0x5ff57c ldrb r1,[r5,#0x11c]` / `0x5ff5d4 blx TransitionToLookingForFace` / `0x5ff682 blx StopActing` / `0x5ff68a blx TransitionToEnrolling` | EXACT_SOURCE |
| FactoryCentroidExtractor | IsRunnableInternal | true iff `+0x84==0` and state `+0x11c==0` | `0x5cf8a0 ldr.w r2,[r1,#0x84]` / `0x5cf8a4 cbz r2` | EXACT_SOURCE |
| FactoryCentroidExtractor | InitInternal | starts a "centroids" logger; cancels actions; sets volume; locks all reactions; sends `StartMotorCalibration` (`0x101`); zeroes `+0x11c`/`+0x11e` | `0x5cf9ca blx FactoryTestLogger::StartLog` / `0x5cfaa4 blx DisableReactionsWithLock` / `0x5cfab6 movw r0,#0x101` / `0x5cfac2 blx EngineToRobot(StartMotorCalibration)` | EXACT_SOURCE |
| FactoryCentroidExtractor | UpdateInternal | 1 (done) unless state `+0x11c==0` and both `+0x11e`/`+0x11d` set, then base | `0x5cfce0 ldrb r2,[r0,#0x11c]` / `0x5cfce6 movs r0,#1` | EXACT_SOURCE |
| FactoryCentroidExtractor | StopInternal | clears `+0x11e` and the `+0x11c..0x11d` halfword | `0x5cfcfe movs r1,#0` / `0x5cfd00 strb r1,[r0,#0x11e]` / `0x5cfd04 strh r1,[r0,#0x11c]` | EXACT_SOURCE |
| FactoryCentroidExtractor | HandleWhileRunning | `MotorCalibration` sets `+0x11d=1` on result 3, `+0x11e=1` on result 2, then `TransitionToMovingHead`; `RobotCompletedFactoryDotTest` checks 4 dots/camera pose, sets backpack lights, logs | `0x5cfdf0 blx Get_RobotCompletedFactoryDotTest` / `0x5d0028 blx Get_MotorCalibration` / `0x5d004c blx TransitionToMovingHead` / `0x5d0140 blx SetBackpackLights` | EXACT_SOURCE for dispatch/log; camera-pose thresholds RECOVERABLE_GAP |
| FactoryTest | IsRunnableInternal | `robot+0x1c9 == 0` | `0x5d0c0c` | EXACT_SOURCE |
| FactoryTest | InitInternal | deadline `now+20 s` (`+0x1ac`); clears write list; `ActionList::Cancel`; four target poses from world origin; config `PickupDockingMethod` and `EnableDrivingAnimations=false`; locks all reactions; enables vision modes 0 and 1; `SetAndDisableAutoExposure(3)`; `SetCurrState(0)` | `0x5d0c42 str.w r0,[fp,#0x12c]` / `0x5d0ca0 blx GetWorldOrigin` / `0x5d1090 blx DisableReactionsWithLock` / `0x5d10c4 blx SetAndDisableAutoExposure` / `0x5d10f6 blx SetCurrState` | EXACT_SOURCE |
| FactoryTest | UpdateInternal | logs "Factory test is not enabled" return 2 (stub) | `0x5d2724` / `0x5d2736 blx sErrorF` | EXACT_SOURCE |
| FactoryTest | StopInternal | if state `+0x1c9==0` -> `EndTest(0x25)` | `0x5d27f8` / `0x5d27fc cbz r2` | EXACT_SOURCE |
| FactoryTest | HandleWhileRunning | tag `0x35` off-treads set -> `EndTest(0x26)`; `0x34` RobotStopped -> `EndTest(0xb)` unless state 4; `0x3b` MotorCalibration; `0x44` RobotObservedObject; `0x38` CameraCalibration -> `+0x138` | `0x5d2826 blx Get_RobotOffTreadsStateChanged` / `0x5d2844 blx Get_MotorCalibration` / `0x5d2894 blx Get_CameraCalibration` / `0x5d28ac str.w r2,[r4,#0x138]` | EXACT_SOURCE |
| FeedingEat | IsRunnableInternal | true iff cube id `+0x120` set, `IsCubeBad` false, located object seen (`+0x24==1`); else clears `+0x120` | `0x5d5886 ldr.w r0,[r4,#0x120]` / `0x5d5898 blx IsCubeBad` / `0x5d58b6 blx GetLocatedObjectByIdHelper` / `0x5d58bc ldrb r0,[r0,#0x24]` | EXACT_SOURCE |
| FeedingEat | InitInternal | requires cube located; `+0x124=FLT_MAX`, `+0x128=0`; `MovementListener(0.4,3.0,2.0,100.0)` on the cube; `TransitionToDrivingToFood` | `0x5d5938 str.w r0,[r6,#0x124]` / `0x5d598a blx MovementListener` / `0x5d59be blx AddListener` / `0x5d59e2 blx TransitionToDrivingToFood` | EXACT_SOURCE |
| FeedingEat | UpdateInternal | once `now > +0x124`: `+0x128=1`, `RegisterNeedsActionCompleted(5)`; if acting and off-treads and `now <= deadline` -> `TransitionToReactingToInterruption` | `0x5d5bb6 strb r0,[r5,#0x128]` / `0x5d5bbe blx RegisterNeedsActionCompleted` / `0x5d5bec blx TransitionToReactingToInterruption` | EXACT_SOURCE |
| FeedingEat | StopInternal | completion callbacks if deadline passed; `EnableStopOnCliff(1)`; `RemoveListener`; clears `+0x120` | `0x5d5e8c blx EngineToRobot(EnableStopOnCliff)` / `0x5d5ec2 blx RemoveListener` / `0x5d5ef2 str.w r0,[r4,#0x120]` | EXACT_SOURCE |
| FeedingSearchForCube | IsRunnableInternal | 1 | `0x5d6c80 movs r0,#1` | EXACT_SOURCE |
| FeedingSearchForCube | InitInternal | `TransitionToFirstSearchForFood` | `0x5d6c86 blx TransitionToFirstSearchForFood` | EXACT_SOURCE |
| FeedingSearchForCube | UpdateInternal | states 0/1, `now > +0x120`: `StopActing`; state 0 -> `TransitionToMakeFoodRequest`, else `TransitionToFailedToFindCubeReaction` | `0x5d6d46 blx StopActing` / `0x5d6d54 blx TransitionToFailedToFindCubeReaction` / `0x5d6d5e blx TransitionToMakeFoodRequest` | EXACT_SOURCE |
| FindFaces | IsRunnableInternal | 1 | `0x5c1a34 movs r0,#1` | EXACT_SOURCE |
| FistBump | IsRunnableInternal | 1 | `0x5f1ed4 movs r0,#1` | EXACT_SOURCE |
| FistBump | InitInternal | lock table `0xc6fdc8` = CubeMoved, FacePositionUpdated, ObjectPositionUpdated, PetInitialDetection, RobotFalling, RobotPickedUp, ReturnedToTreads, UnexpectedMovement; idle anim `0x23f`; `+0x11c = 0` | `0x5f1ee6 blx SmartDisableReactionsWithLock` / `0x5f1eee movw r2,#0x23f` / `0x5f1f18 str.w r0,[r5,#0x11c]` | EXACT_SOURCE |
| FistBump | UpdateInternal | state 0->9: `PlaceObjectOnGroundAction`; `TurnTowardsFaceAction(pi)`; wait/pan; triggers around `0xc7`, `0xc6`, `0xc9`, `0xca` (60.0); head/lift disable/settle; `BehaviorObjectiveAchieved(7)`, `ResetTrigger` | `0x5f1fba blx PlaceObjectOnGroundAction` / `0x5f2084 blx Radians(pi)` / `0x5f209c blx TurnTowardsFaceAction` / `0x5f2104 blx TriggerAnimationAction` / `0x5f2288 blx BehaviorObjectiveAchieved` / `0x5f2292 blx ResetTrigger` | EXACT_SOURCE |
| FistBump | StopInternal | `EnableLiftPower(true)`, `EnableHeadPower(true)`, `ResetTrigger(false)` | `0x5f2704 blx EnableLiftPower` / `0x5f270e blx EnableHeadPower` / `0x5f2714 ResetTrigger` | EXACT_SOURCE |
| GuardDog | InitInternal | lock table `0xc6ff06` (same disabled set as Bouncer); clears sCubeData map `+0x120`; for each matching located object inserts sCubeData `{state 5, 0x28}`; `StopAllAnims`; `+0x11c=0`; `+0x148=5` | `0x5f2d0e blx SmartDisableReactionsWithLock` / `0x5f2d60 blx FindLocatedMatchingObjects` / `0x5f2db4 blx StopAllAnims` / `0x5f2d44 strb r0,[r4,#0x148]` | EXACT_SOURCE |
| GuardDog | UpdateInternal | cube result 3 -> state 10 "PlayerSuccess"; result 3 in `+0x140` -> state 7 "Busted"; state 5 timeout 120 s / 20 s -> state 9 "Timeout"; spread > 40 mm updates public stage; states 0..10 drive triggers `0xdb,0xdc,0xdd,0xd8,0xde,0xda,0xd6,0xd7,0xe0,0xdf`, `DriveToPose`/`RetryWrapper(0x23f,2)`, `TurnTowardsPose(pi)`, `DriveStraight`, broadcast `GuardDogEnd` | `0x5f2e98 blx StopActing` / `0x5f3050 blx TriggerAnimationAction` / `0x5f30da blx StartMonitoringCubeMotion` / `0x5f31a0 blx TurnTowardsPoseAction` / `0x5f3300 blx RetryWrapperAction` / `0x5f381a blx Broadcast` | EXACT_SOURCE |
| GuardDog | StopInternal | `StartMonitoringCubeMotion(false)`; `StopAllAnims`; `UpdateBroadcastBehaviorStage(4,0)`; `+0x148=5`; `LogDasEvents`; broadcast `GuardDogEnd{state==0xb}` | `0x5f4730 blx StartMonitoringCubeMotion` / `0x5f4738 blx StopAllAnims` / `0x5f4742 blx UpdateBroadcastBehaviorStage` / `0x5f4766 blx MessageEngineToGame` / `0x5f476e blx Broadcast` | EXACT_SOURCE |
| GuardDog | IsRunnableInternal | feature gate 7; whiteboard `+0x74` 0; exactly 3 matching cubes; each cube's +90 deg parent axis is 3; x/y spread < 100.0 mm | `0x5f2aac movs r1,#7` / `0x5f2ad4 blx FindLocatedMatchingObjects` / `0x5f2ade cmp r0,#0xc` / `0x5f2afe blx GetRotatedParentAxis<90>` / `0x5f2b02 cmp r0,#3` | EXACT_SOURCE for gate/3-cube/axis; 100 mm compare RECOVERABLE_GAP |
| GuardDog | HandleWhileRunning | tag `0x11` -> `HandleObjectUpAxisChanged`; `0x0f` -> `HandleObjectMoved`; `0x0d` -> `HandleObjectConnectionState`; else warn | `0x5f4d42 blx Get_ObjectConnectionState` / `0x5f4d58 blx Get_ObjectUpAxisChanged` / `0x5f4d6e blx Get_ObjectMoved` | EXACT_SOURCE |
| InteractWithFaces | InitInternal | `+0x124 = -1.0f`; no target `+0x11c==0` -> warn "NoValidTarget" return 1; else `TransitionToInitialReaction` | `0x5c215e movt r3,#0xbf80` / `0x5c2164 str.w r3,[r0,#0x124]` / `0x5c216a blx TransitionToInitialReaction` | EXACT_SOURCE |
| InteractWithFaces | UpdateInternal | `now >= +0x124`: `BehaviorObjectiveAchieved(0xc,1)`, `StopActing(true,false)`, `RegisterNeedsActionCompleted(0x2e)` | `0x5c2388 blx BehaviorObjectiveAchieved` / `0x5c2392 blx StopActing` / `0x5c239c blx RegisterNeedsActionCompleted` | EXACT_SOURCE |
| InteractWithFaces | StopInternal | stores last image timestamp into `+0x120` | `0x5c2414 blx GetLastImageTimeStamp` / `0x5c2418 str.w r0,[r4,#0x120]` | EXACT_SOURCE |
| InteractWithFaces | IsRunnableInternal | clears `+0x11c`; `SelectFaceToTrack`; returns `+0x11c != 0` | `0x5c23b2 str.w r0,[r4,#0x11c]` / `0x5c23b8 blx SelectFaceToTrack` / `0x5c23c0 cmp r0,#0` | EXACT_SOURCE |
| LiftLoadTest | IsRunnableInternal | true iff `+0x219` set and (state `+0x11c` OR 2)==2 | `0x5d40f0 ldrb r1,[r0,#0x219]` | EXACT_SOURCE |
| LiftLoadTest | InitInternal | locks all reactions; state `+0x11c=0`; `+0x218=0`; `ActionList::Cancel`; writes banner | `0x5d412e blx GetAffectAllArray` / `0x5d413a blx DisableReactionsWithLock` / `0x5d4152 str.w r4,[fp,#0x11c]` / `0x5d417c blx Write` | EXACT_SOURCE |
| LiftLoadTest | UpdateInternal | counter `+0x21c==0x32` -> "Test Completed Successfully"; else per state builds two-step `MoveLiftToHeightAction` compounds (5.0/10.0/92.0); abort writes "Test Aborted"; `PrintStats`; `+0x21c=0`; `SetCurrState(2)` | `0x5d4700 movt r3,#0x40a0` / `0x5d4718 movt sb,#0x4120` / `0x5d472e movt r2,#0x42b8` / `0x5d470e blx MoveLiftToHeightAction` / `0x5d46a6 blx PrintStats` / `0x5d46ba blx SetCurrState` | EXACT_SOURCE |
| LiftLoadTest | StopInternal | removes lock "LiftLoadTest" | `0x5d4d58 blx RemoveDisableReactionsLock` | EXACT_SOURCE |
| LiftLoadTest | HandleWhileRunning | tag `0x35` off-treads set -> `SetCurrState(1)`; else log invalid tag | `0x5d4e74 blx Get_RobotOffTreadsStateChanged` | EXACT_SOURCE |
| OnboardingShowCube | IsRunnableInternal | 1 | `0x600c78` | EXACT_SOURCE |
| OnboardingShowCube | InitInternal | `PushDrivingAnimations` (name at `0xc72448`); lock table `0xc72454` = CubeMoved, FacePositionUpdated, Frustration, Hiccup, ObjectPositionUpdated, PetInitialDetection; state not 1/4 -> state 5 "WaitForShowCube"; known cube id -> `PlaceObjectOnGroundAction` | `0x600c9c blx PushDrivingAnimations` / `0x600ca8 blx SmartDisableReactionsWithLock` / `0x600d1e blx SetState_internal` / `0x600d44 blx PlaceObjectOnGroundAction` | EXACT_SOURCE |
| OnboardingShowCube | UpdateInternal | while idle: states not in {0,4,8}, `now - start > +0x12c` (300 s) -> `SetState_internal(4,"ErrorFinal")` | `0x601552 ldrb r0,[r5,#0x11c]` / `0x601576 vldr s0,[r5,#0x34]` / `0x601598 add r1,pc "ErrorFinal"` / `0x6015ae blx SetState_internal` | EXACT_SOURCE |
| OnboardingShowCube | StopInternal | `RemoveDrivingAnimations`; `StopLightAnimAndResumePrevious(0x1a, ObjectID -1)` | `0x600eac blx RemoveDrivingAnimations` / `0x600ecc blx StopLightAnimAndResumePrevious` | EXACT_SOURCE |
| OnboardingShowCube | HandleWhileRunning | tag `0x88` ignored; `0x44` -> `HandleObjectObserved`; else `InvalidEvent` | `0x60010be blx Get_RobotObservedObject` / `0x60010e4 blx sErrorF` | EXACT_SOURCE |
| RespondToRenameFace | IsRunnableInternal | name string `+0x11c` non-empty | `0x60082e ldrb r1,[r0,#0x11c]` | EXACT_SOURCE |
| RespondToRenameFace | InitInternal | logs; `SayTextAction`; `SetAnimationTrigger(anim +0x12c)`; `StartActing`; clears name buffer | `0x600884 blx HidePersonallyIdentifiableInfo` / `0x6008d4 blx SayTextAction::SayTextAction` / `0x6008e2 blx SetAnimationTrigger` / `0x6008f0 blx StartActing` | EXACT_SOURCE |
| RespondToRenameFace | UpdateInternal | 2 when no action, else 1 | `0x600a50` | EXACT_SOURCE |
| SearchForFace | IsRunnableInternal | true iff `FaceWorld::HasAnyFaces` false | `0x5c9178 blx HasAnyFaces` | EXACT_SOURCE |
| SearchForFace | InitInternal | faces exist -> 1; else `TransitionToSearchingAnimation` | `0x5c918e blx HasAnyFaces` | EXACT_SOURCE |
| SearchForFace | UpdateInternal | state `+0x11c==0`, a face appears -> `StopActing(false,false)` + `TransitionToFoundFace` | `0x5c92a6 blx HasAnyFaces` / `0x5c92b4 blx StopActing` / `0x5c92bc blx TransitionToFoundFace` | EXACT_SOURCE |
| TrackLaser | IsRunnableInternal | feature gate 9; vision mode `+4` 0; no active objective; already tracking (`+0xd8`/`+0xd9`) -> true; else `+0x198` window test | `0x5faabe blx IsFeatureEnabled` | EXACT_SOURCE for gate/offsets; timing compare RECOVERABLE_GAP |
| TrackLaser | InitInternal | `InitHelper`; no laser and no search -> `TransitionToInitialSearch`; else `+0x1b8=now`, `TransitionToWaitForExposureChange` | `0x5fac16 blx InitHelper` / `0x5fac34 str.w r0,[r5,#0x1b8]` / `0x5fac3c blx TransitionToWaitForExposureChange` | EXACT_SOURCE |
| TrackLaser | StopInternal | if `+0x1ae` set: `BehaviorObjectiveAchieved(0xf,1)`, `NeedActionCompleted(0)`; `Cleanup` | `0x5fbd26 blx BehaviorObjectiveAchieved` / `0x5fbd2e blx NeedActionCompleted` | EXACT_SOURCE |
| TrackLaser | UpdateInternal | state machine (search/exposure/acquire/lock); `ResumeInternal 0x5fbbac`, `AlwaysHandle 0x5fc1d8` | `0x5fb200` | RECOVERABLE_GAP |
| BringCubeToBeacon | IsRunnableInternal | trims candidates `+0x11c..+0x120`; needs active beacon not expired; `FindUsableCubesOutOfBeacons`; for each `DidFailToUse(...,20.0)`; true iff candidates remain | `0x5df13e blx GetActiveBeacon` / `0x5df1a4 blx FindUsableCubesOutOfBeacons` / `0x5df250 blx DidFailToUse` | EXACT_SOURCE |
| BringCubeToBeacon | InitInternal | `+0x12c=-1`; no active objective -> `TransitionToPickUpObject`; else `+0x12c = carried id`, `TransitionToObjectPickedUp` | `0x5df356 str.w r0,[r4,#0x12c]` / `0x5df372 blx TransitionToObjectPickedUp` / `0x5df37e blx TransitionToPickUpObject` | EXACT_SOURCE |
| BringCubeToBeacon | StopInternal | `EraseSegments("BehaviorExploreBringCubeToBeacon.Locations")` | `0x5dfd72 blx EraseSegments` | EXACT_SOURCE |
| PlayAnim | InitInternal `0x5c014e` | `StartPlayingAnimations` | `0x5c0150 blx StartPlayingAnimations` | EXACT_SOURCE (correction) |
| PlayAnim | IsRunnableInternal `0x5c013a` | false if `+0x11c==+0x120`; else vtable `+0x90` (OnNeedsChange gate) | `0x5c013e cmp r2,r3` / `0x5c0148 ldr.w r2,[r2,#0x90]` / `0x5c014c bx r2` | EXACT_SOURCE |
| PlayAnimOnNeedsChange | StopInternal `0x5bfef8` | if `ShouldGetInBePlayed` -> `SetSevereNeedExpression(+0x13c)` | `0x5bfefe blx ShouldGetInBePlayed` / `0x5bff08 ldr.w r0,[r5,#0x264]` | EXACT_SOURCE |
| PlayAnimWithFace | InitInternal `0x5c0648` | `TurnTowardsFaceAction(robot, 0, pi, false)` + `StartActing`; no Update/Stop/IsRunnable override | `0x5c0664 movw r1,#0xfdb` / `0x5c0686 blx TurnTowardsFaceAction` / `0x5c06aa blx StartActing` | EXACT_SOURCE |
| PlayArbitraryAnim | InitInternal `0x5c0896` | `+0x13c=0`; `StartPlayingAnimations` | `0x5c089a strb.w r2,[r0,#0x13c]` / `0x5c089e blx StartPlayingAnimations` | EXACT_SOURCE |
| PlayArbitraryAnim | IsRunnableInternal `0x5c07be` | false if `+0x128 < 0`; else vtable `+0x90` | `0x5c07be ldr.w r2,[r0,#0x128]` / `0x5c07c4 blt` / `0x5c07c6 b.w 0x8cc0fc` | EXACT_SOURCE |
| PlayArbitraryAnim | StopInternal `0x5c08aa` | clears `+0x13c`; trims `+0x11c` vector | `0x5c08b0 strb.w r3,[r0,#0x13c]` / `0x5c08c8 strne.w r1,[r0,#0x120]` | EXACT_SOURCE |
| Singing | UpdateInternal `0x5ef0c8` | sums per-cube accel scores / 3000, clamps 0.5..1.0, smooths `+0x140`, posts audio parameter `0xc20f49df`; tracks the shake-duration event when level crosses 0.1 | `0x5ef188 movt r1,#0xc20f` / `0x5ef18c blx PostRobotParameter` / `0x5ef23c str.w r0,[r7,#0x144]` | EXACT_SOURCE |
| Singing | StopInternal `0x5ef2b0` | posts parameter `0xc20f49df=0`; removes the accel listener per tracked cube; clears the listener vector `+0x12c` | `0x5ef2be movw r1,#0x49df` / `0x5ef2ca blx PostRobotParameter` / `0x5ef2e0 blx RemoveListener` / `0x5ef320 str.w r1,[r5,#0x12c]` | EXACT_SOURCE |
| RequestGameSimple | RequestGame_InitInternal `0x5ea6e2` | if `+0x194` locks reactions (table `0xc6e820` = CubeMoved, FacePositionUpdated, FistBump, ObjectPositionUpdated, PetInitialDetection); `+0x204=FLT_MAX`; `SmartSetMotionProfile`; branches to `TransitionToLookingAtFace` / `TransitionToPlayingInitialAnimation` / `TransitionToDrivingToFace`; else `TriggerAnimationAction(0x1cf, 60.0)` | `0x5ea6e2 blx SmartDisableReactionsWithLock` / `0x5ea73e blx SmartSetMotionProfile` / `0x5ea766 movw r2,#0x1cf` | EXACT_SOURCE |
| RequestGameSimple | RequestGame_UpdateInternal | state `+0x158==0xb`, block found -> `StopActing` + `TransitionToFacingBlock`; `CheckRequestTimeout` -> `StopActing`, `SendDeny`, `TransitionToPlayingDenyAnim`; 2 when idle | `0x5ead40 blx TransitionToFacingBlock` / `0x5ead46 blx CheckRequestTimeout` / `0x5ead5c blx SendDeny` | EXACT_SOURCE |
| RequestGameSimple | RequestGame_StopInternal | state `+0x158==9` -> `SendDeny`; clears `+0x220`, `+0x158`; `StopActing` | `0x5eb28a blx SendDeny` / `0x5eb292 strb.w r0,[r4,#0x220]` / `0x5eb29e blx StopActing` | EXACT_SOURCE |
| RequestGameSimple | CheckRequestTimeout | true iff state `+0x158==9` and `0 <= GetRequestMinDelayComplete_s() <= now` | `0x5eb0c0 blx getInstance` / `0x5eb0d2 blx r1` | EXACT_SOURCE |
| RequestGameSimple | GetRequestMinDelayComplete_s | `-1.0` if `+0x11c<0`; else `(profile+0x14 or 5.0) + +0x11c` | `0x5ec288 vldr s0,[r0,#0x11c]` / `0x5ec2a8 vmoveq.f32 s2,#5.0` / `0x5ec2ac vadd.f32 s0,s2,s0` | EXACT_SOURCE |

## Reaction-lock tables read

The table is 21 `{ReactionTrigger:u8, bool:u8}` entries; `BehaviorManager::DisableReactionsWithLock`
reads `[table + trigger*2 + 1]` and disables only when non-zero (`0x005A283E add r0,r0,r8,lsl #1`;
`0x005A2842 ldrb r0,[r0,#1]`; `0x005A2846 beq`). Trigger ordinals 0..20.

| caller | table | locked triggers (byte=1) |
|---|---|---|
| ReactToCliff Init | `0xC73746` | 1, 2, 8, 20 |
| ReactToMotorCalibration Init | `0xC74032` | 0, 5, 12, 14, 15, 16, 17 |
| ReactToPlacedOnSlope Init | `0xC745E0` | 0, 12, 14 |
| ReactToRobotShaken Init | `0xC750E0` | 0,1,2,3,4,5,8,10,11,12,13,14,15,16,17,20 |
| CubeLiftWorkout Init | `0xc6b7e0` | 1, 2, 8, 10, 20 |
| PeekABoo Init | `0xc70960` | 1, 2, 3, 8, 10 |
| PutDownBlock Init | `0xc68b20` | none |
| FistBump Init | `0xc6fdc8` | 1, 2, 8, 10, 11, 12, 14, 20 |
| Bouncer Init | `0xc6fb90` | 1, 2, 3, 4, 5, 8, 10, 20 |
| GuardDog Init | `0xc6ff06` | 1, 2, 3, 4, 5, 8, 10, 20 |
| Dance Init | `0xc6f1c8` | 1, 2, 3, 8, 10, 20 |
| EnrollFace Init | `0xc71b6c` | 0, 1, 2, 3, 4, 5, 8, 11, 12, 14, 15, 16, 17, 20 |
| OnboardingShowCube Init | `0xc72454` | 1, 2, 3, 4, 5, 8, 10, 20 |

## Existing records, judged

- **M8-005** (PlayAnimSequence): the `InitInternal`/`IsRunnableInternal` addresses above are the real
  ones; the part-2 report's `0x5c0648` was the WithFace class. Correct the citation.
- **M8-008** (head recalibration wait): `ReactToMotorCalibration`'s `0x40A00000` is confirmed.
- **M7-014** (reaction locks held for the reaction): the helper is confirmed; the 13 concrete lock
  tables above are NEW detail and belong to it or to per-class records.
- **M7-011**: the only cooldown-like constants in these classes are the two 100 s re-arm timers
  (`ReactToPyramid 0x60841c`, `ReactToStackOfCubes 0x609990`) and the 10 s slope re-check
  (`0x60800e`); none is a trigger cooldown, so M7-011 is unaffected.
- **M7-015** (`pick-up falls back to the raw status flag`): consistent with `ReactToPickup` reading
  `robot+0x355`/`robot+0x338`; those field meanings remain UNKNOWN.
- No existing M7/M8/M10/M12/M13/M15 record is contradicted.

## NEW steps no record covers

- Every row above. The manager should create records per owner: framework/M8 for the lock tables and
  the `0x23f` sentinel; M10/M7 for the reaction classes; M12 for PickUpCube/PutDownBlock/RollBlock/
  StackBlocks/PickUpAndPutDownCube; M13 for BuildPyramid/Base, CantHandleTallStack,
  CheckForStackAtInterval, CubeLiftWorkout, RespondPossiblyRoll, OnConfigSeen, ThinkAboutBeacons,
  DriveInDesperation, DrivePath, DriveToFace, PopAWheelie; M15 for ExploreLookAroundInPlace,
  ExploreVisitPossibleMarker, LookInPlaceMemoryMap, LookForFaceAndCube, LookAround, TurnToFace,
  PyramidThankYou, EarnedSparks, FireTruckAlarm, PounceOnMotion, PeekABoo; M14 for the face classes.

## Open questions for the manager

- Q1. `robot+0x355` (compared to 0..5 by many reactions), `robot+0x338`, `robot+0x300`,
  `robot+0x37C`, and the AI mode `[[robot+0x264]+0x30]+0x14` decide animation variants. Their enum
  names are UNKNOWN.
- Q2. `0x23f` is one past the 575-entry `AnimationTrigger` enum and is used as a "no idle / no trigger"
  sentinel by several classes. Record it once, or per use?
- Q3. Unread sub-bodies: `DockingTestSimple::UpdateInternal` (dev test), `TrackLaser::UpdateInternal`,
  `FactoryCentroidExtractor`'s camera-pose thresholds, `GuardDog`'s 100 mm spread compare, and the
  Cliff `0x5c0ca8`/`0x55b554` helpers. Decide which are worth a focused pass.
- Q4. The `BEHAVIOR_INVENTORY.md`/`behavior_inventory.json` reasons are authority-6 notes; the
  `RollBlock` reason omits the 14400 moved^2 threshold. Integrator note, not a contradiction.


## Appendix G - I-M7 gap pass 1

# I-M7 gap pass 1: remaining mood and behaviour bodies

Agent: Codex acting as `cozmo-extractor` under job I-M7. Read-only source recovery. Date: 2026-09-28.

Primary evidence is `resources/lib/armeabi-v7a/libcozmoEngine.so`. The Ghidra output was used only to navigate; every claim below was checked against Thumb instructions in the ELF.

| item | what the original does | citation | classification |
| --- | --- | --- | --- |
| G1 Mood output | `MoodManager::SendEmotionsToGame` does nothing when its robot/external-interface pointer at `+0x12c` is null. Otherwise it reserves a vector of nine floats, copies the value at `+0x18 + 0x20*i` for `i=0..8`, constructs `MoodState`, and broadcasts it. | `0x0067b736 ldr.w r0,[r8,#0x12c]`; `0x0067b73c beq 0x67b7e0`; `0x0067b748 movs r1,#9`; loop `0x0067b758..0x0067b77c`; `0x0067b74e add.w r7,r8,#0x18`; `0x0067b778 adds r7,#0x20`; message construction/broadcast `0x0067b78e` / `0x0067b796` | EXACT_SOURCE |
| G2 `BehaviorWait` | `BehaviorWait` has no behavioural override. `BehaviorContainer::CreateBehavior`, class `0x39`, allocates `0x120`, calls the base `IBehavior` constructor, and installs the `BehaviorWait` vtable. No `BehaviorWait::*Internal` symbol or separate body exists. Its runtime behaviour is therefore the base `IBehavior` defaults. | `BehaviorContainer::CreateBehavior 0x0059c888`, class-`0x39` case: allocation/base construction/vtable install; `BehaviorClass.cs` ordinal 57 (`Wait`); dynamic-symbol table has the `BehaviorWait` shared-pointer helpers but no `*Internal` override | EXACT_SOURCE |
| G3 `TrackLaser::UpdateInternal` | Dispatches on state `+0x1c4`. State 4 with `+0x1b4 != 0` stops acting and transitions to wait-for-laser, then shares the state-5 test. State 5 with `+0x198 == 2` logs the confirmed-laser event and transitions to respond-to-laser; otherwise timeout transitions to get-out-bored when `+0x1ac`, else returns 2, and `+0x1b8 + +0x154 < now` rotates to a new watching area. State 7 starts `+0x1bc` on the first non-zero `+0x198`, and sets `+0x1ae` once `now - +0x1bc > +0x190`. States 8/9 do nothing; state 10 returns 2; all other continuing paths return 1. | state dispatch `0x005fb208..0x005fb21e`; state 4 `0x005fb220..0x005fb234`; confirmed laser `0x005fb238..0x005fb27a`; timeouts `0x005fb280..0x005fb31e`; state-7 timer `0x005fb28e..0x005fb2d8`; returns `0x005fb322..0x005fb32a` | EXACT_SOURCE; the semantic names of the offset fields are not recovered, so they remain offset-labelled |
| G4 GuardDog spread | After the feature-7, whiteboard, exactly-three-cubes and up-axis-3 gates reported by X3, it computes the x range and y range of the three cube poses, takes the larger range, and returns true only when it is strictly less than `100.0f`. | coordinate loads `0x005f2b48` / `0x005f2b54`; x min/max and range `0x005f2b90..0x005f2bd0`; y min/max and range `0x005f2be2..0x005f2c2e`; maximum `0x005f2c34..0x005f2c3e`; compare against the literal loaded at `0x005f2c42`, `vcmpe` `0x005f2c46`, true at `0x005f2c50` | EXACT_SOURCE |
| G5 Factory camera-pose thresholds | On `RobotCompletedFactoryDotTest`, missing four dots fails. When camera pose is present, roll and yaw are checked against 5 degrees (`0.08726646` rad); pitch is checked against the reported pitch offset by 4 degrees (`0.06981317`) and the same 5-degree tolerance; x/y/z translations are checked against 5.0. Each exceeded threshold warns and selects the failure backpack-light table; otherwise it selects the success table. | `BehaviorFactoryCentroidExtractor::HandleWhileRunning 0x005cfdd0`; roll block `0x005cff08..0x005cff56`; pitch block `0x005cff58..0x005cffba`; yaw block `0x005cffbc..0x005d000a`; translation blocks `0x005d000c..0x005d00f4`; light selection and `SetBackpackLights` `0x005d0118..0x005d0140` | EXACT_SOURCE |
| G6 `ReactToCliff` citation correction | X3 part 6 cited `0x00604ef4` for the `"CliffReact"` emotion event. That address is outside `InitInternal` and does not contain the call. The actual sequence builds `"CliffReact"`, gets the mood clock, and calls `MoodManager::TriggerEmotionEvent` at the start of `InitInternal`. | `0x00604d70 strd r0,r0,[sp]`; `0x00604d76 bl 0x4e02b2` (builds the string); `0x00604d7a blx 0x4ab3bc` (`MoodManager::GetCurrentTimeInSeconds`); `0x00604d84 blx 0x4ab3c8` (`MoodManager::TriggerEmotionEvent`) | EXACT_SOURCE; replaces the failed X3 citation |
| G7 `DockingTestSimple::UpdateInternal` | The 4,144-byte developer-test state machine is present and the X3 summary correctly identifies its success count, object-drive/pickup, place and repeat families. This pass did not exhaustively recover every branch and callback. | `BehaviorDockingTestSimple::UpdateInternal 0x005cbc10..0x005ccc3f`; remaining work: enumerate every state selected from `+0x11c`, every callback transition, and every random range/action argument in that range | RECOVERABLE_GAP; non-live developer test |
| G8 reaction helper semantics | X3's reaction rows contain source-exact control flow but retain offset-labelled robot fields (`robot+0x355`, `+0x338`, `+0x300`, `+0x37c`) and two helper calls in the cliff path (`0x0055b554`, `0x005c0ca8`). This pass does not assign semantic names without tracing their writers/callers. | remaining work: trace all writers/readers of the four robot offsets and disassemble helper bodies `0x0055b554` and `0x005c0ca8` with their call sites | RECOVERABLE_GAP; live-path naming/helper gap |

## Citation verification

Acting as `cozmo-verifier`, I opened every existing M7 status-changing citation plus 24 additional X3 rows in the ELF. The checked sample included `0x0060635a`, `0x0057db4a`, `0x0060ee8c`, `0x00679622`, `0x006795e6`, `0x00804bd0`, `0x005a283e`, `0x00604d76`, `0x006083fc`, `0x005f2aac`, `0x005fb200`, `0x0067b736`, `0x005eeb4e`, `0x00606cca`, `0x00602522`, `0x00612530`, `0x0060cb84`, `0x0060c04a`, `0x00612a94`, `0x0057d85c`, `0x0058d3e2`, `0x0065b3a8`, and `0x0060b91c`, plus the shipped Unity enum and OBB JSON paths. All supported their report rows except X3 part 6's `0x00604ef4`, corrected by G6. No failed citation is retained.

## Remaining gaps

- G7 is deliberately left RECOVERABLE_GAP because it is a non-live developer test and a full exhaustive reading is still required before claiming its entire state machine.
- G8 remains a live-path RECOVERABLE_GAP. The inventory must therefore leave `source_investigation_exhausted` false and must not convert those semantic names or helper effects into recovered facts.

## Appendix H - B-M7 inventory correction C1 (2026-09-28)

Built by job B-M7. Three `MISSING` rows from the build were answered by a read-only extraction of the shipped `.so` (`resources/lib/armeabi-v7a/libcozmoEngine.so`, 3.4.0-1204); the report is `.scratch/b-m7-missing/report.md`. These are corrections to the frozen rows, not new behaviour claims. The three records M7-005/009/010 are settled on these values; the code already carries them (the earlier X3/I-M7 passes had the values, the row text did not).

### C1a. M7-005 - the blink table (Appendix A row 5)

The table is a static `.rodata` array at **`0x00c5aad8`**, 7 records x 16 bytes = 0x70 bytes, copied verbatim into a heap vector on the first call (`0x00585f86 movs r0,#0x70`; `0x00585f92 ldr.w lr,[pc,#0x300]` -> `0x00c5aad8`; seven `stm` copies). Field layout per record: `+0x00` f32 scale-y multiplier, `+0x04` f32 scale-x multiplier, `+0x08` u32 duration in whole ms, `+0x0c` u8 action.

| # | scale-x | scale-y | duration ms | action |
|---:|---:|---:|---:|---:|
| 0 | 1.05 | 0.85 | 33 | 0 |
| 1 | 1.20 | 0.60 | 33 | 0 |
| 2 | 2.50 | 0.10 | 33 | 0 |
| 3 | 5.00 | 0.05 | 33 | 1 (shut) |
| 4 | 2.00 | 0.15 | 33 | 2 (reopen) |
| 5 | 1.20 | 0.70 | 33 | 3 |
| 6 | 1.00 | 0.90 | 100 | 3 |

Action 1 flips `ProceduralFaceDrawer::_firstScanLine`, sets both eyes' `EyeCenterY` to the mean of the saved centres and zeroes the six lid parameters from the byte list at `0x00c5ab48` = `[16,18,17,13,15,14]` (`0x005861be`, `0x005861e2`, `0x005861f4`). Action 2 restores them. Consumption: `GetNextBlinkFrame 0x00585f18` returns each frame and, on the pass after the last, restores the saved face with duration 33 (`0x00586190 movs r1,#0x21`) and returns false; `GenerateBlink 0x0058d2ac` adds a keyframe for **every** return value, so the emitted track has **eight** keyframes (seven squash + restore), cumulative triggers 33..331 ms. `ProceduralFace::Combine 0x005846a8` multiplies the eye scales (offsets 0xa0/0xa4) and adds the offsets (0xa8/0xac).

**Correction to the record title:** the table is seven frames; the emitted animation is eight keyframes. The manifest title now says so.

### C1b. M7-009 - the idle head and lift draws (Appendix A row 9)

- **Head.** Current angle is `Robot+0x2fc` in radians (`0x0057d83e vldr s2,[r5,#0x2fc]`), converted to degrees by the literal at **`0x0057db2c` = `0x42652ee1` = 57.295780** (`0x0057d838`), then truncated toward zero (`0x0057d84e vcvt.s32.f32`) and stored as a signed byte. Variability is idle parameter 19 `HeadAngleVariability_deg` = 6.0 (`0x40c00000`, read `0x0057d83c`/`0x0057d846`, default `0x0057dc44`). Duration `RandIntInRange(p15, p16)` = `HeadMovementDurationMin_ms` 50 .. `HeadMovementDurationMax_ms` 500 (`0x0057d814`/`0x0057d822`; defaults `0x0057dbca`/`0x0057dbda`). Gap `RandIntInRange(p17, p18)` = `HeadMovementSpacingMin_ms` 250 .. `HeadMovementSpacingMax_ms` 1000 (`0x0057da2c`, defaults `0x0057dc30`/`0x0057dc3a`). Keyframe ctor `HeadAngleKeyFrame::HeadAngleKeyFrame` `0x004f8be4` (`+0x0c` duration, `+0x10` i8 angle_deg, `+0x11` u8 variability_deg); wire `HeadAngleKeyFrame::GetStreamMessage 0x004f8c08` draws `angle +/- variability` and sends **animHeadAngle 0x93** (`u16 durationMs`, `i8 angleDeg`).
- **Lift.** Mean and variability are idle parameters 13 `LiftHeightMean_mm` = 35.0 (`0x420c0000`, `0x0057dc02`) and 14 `LiftHeightVariability_mm` = 8.0 (`0x41000000`, `0x0057dc10`); read `0x0057d9a4`/`0x0057d9ae`. No current-height input. Duration `RandIntInRange(p9, p10)` = `LiftMovementDurationMin_ms` 50 .. `LiftMovementDurationMax_ms` 500 (`0x0057d980`/`0x0057d98e`). Gap `RandIntInRange(p11, p12)` = `LiftMovementSpacingMin_ms` 250 .. `LiftMovementSpacingMax_ms` 2000 (`0x0057da58`, defaults `0x0057dbea`/`0x0057dbf4`). Keyframe ctor `LiftHeightKeyFrame::LiftHeightKeyFrame` `0x004f8f5c` (`+0x0c` duration, `+0x10` u8 height_mm, `+0x11` u8 variability_mm); wire `LiftHeightKeyFrame::GetStreamMessage 0x004f8f80` draws `height +/- variability` and sends **animLiftHeight 0x94** (`u16 durationMs`, `u8 heightMm`).
- **Correction to Appendix A row 9's offsets:** the row says "+0x264, +0x3c8"; those offsets do not occur in `UpdateLiveAnimation`. The real streamer state offsets are `+0x198`/`+0x1a4` (body), `+0x19c`/`+0x1a8` (lift), `+0x1a0`/`+0x1ac` (head). The manifest evidence already cites the correct addresses.

### C1c. M7-010 - the idle body shuffle (Appendix A row 10)

- **Straight vs turn:** `BodyMovementStraightFraction` = parameter 8 = 0.5 (`0x3f000000`, `0x0057dbba`); `RandDblInRange(0,1)` (`0x0057d716`) `<= 0.5` -> straight (`0x0057d750 ble 0x57d8c6`), else turn. So P(straight) = P(turn) = 0.5.
- **Speed:** parameter 7 `BodyMovementSpeedMinMax_mmps` = 10.0 (`0x41200000`, `0x0057dbae`); `RandIntInRange(-10, +10)` (`0x0057d6f0`..`0x0057d70c`). Its sign selects the turn's eye-shift direction.
- **Duration:** `RandIntInRange(p5, p6)` = `BodyMovementDurationMin_ms` 250 .. `BodyMovementDurationMax_ms` 1500 (`0x0057d6ce`, defaults `0x0057db8e`/`0x0057db9a`), stored at `+0x198`.
- **Separate gap:** `RandIntInRange(p3, p4)` = `BodyMovementSpacingMin_ms` 100 .. `BodyMovementSpacingMax_ms` 1000 (`0x0057d95a`, defaults `0x0057db76`/`0x0057db84`), stored at `+0x1a4`.
- **Body keyframe:** `BodyMotionKeyFrame(speed, radius, duration)` `0x004fb170`; radius `0x7fff` straight (`0x0057d8da`) or `0` turn (`0x0057d80e`). Wire `BodyMotionKeyFrame::GetStreamMessage 0x004fba8c` sends **animBodyMotion 0x99** (`i16 speed`, `i16 radius`).
- **Paired turn eye shift** (`TrackLayerComponent::AddOrUpdateEyeShift` `0x0064f3c8`, call `0x0057d7fc`): x = `sign(speed) * RandIntInRange(0, 21)` (`0x0057d758`, `0x0057d7a8`), y = `RandIntInRange(-10, 10)` (`0x0057d766`), duration **33 ms** (`0x0057d7de movs r0,#0x21`), xMax 64.0 (`0x0057d7da`), yMax 32.0 (`0x0057d7d6`), up 1.1 (`0x0057d7ca`), down 0.85 (`0x0057d7c6`), outer 0.1 (`0x0057d798`). The straight branch removes it (`RemoveEyeShift 0x0057d8d2`). There is no other spacing constant for this layer; the body gap at `+0x1a4` is the only "spacing".

### C1d. M7-002 - the reaction map row count

The shipped `reactionTrigger_behavior_map.json` has **22 rows over the 21 triggers** (the `Frustration` trigger appears twice: `ReactToFrustrationMinor` with `cooldownTime_s 60.0` and `ReactToFrustrationMajor` with none). The loader reads all 22; the record's "21-entry reaction map" means the 21 triggers, not 22 rows. No behaviour changes.

### C1e. Records settled on these values

M7-005, M7-009 and M7-010 are settled EXACT_SOURCE on C1a-C1c. The code (`IdleBehavior.BlinkFrames`/`BlinkPose`, `IdleBehavior.Perform` head/lift, `IdleBehavior.TurnEyeShift` and the body draw) matches the values above; the head-angle conversion must use the shipped constant `0x42652ee1` (57.295780) in float, not a recomputed 180/pi.

### C1f. Post-build record status (job B-M7)

The record table at the top of this inventory is the pre-build state. After the B-M7 build and verify:

- **Settled EXACT_SOURCE:** M7-001, M7-002, M7-003, M7-004, M7-005, M7-006, M7-007, M7-008, M7-009, M7-010, M7-011, M7-013, M7-016, M7-017.
- **Left IMPLEMENTATION_GAP (cross-layer, SD3):** M7-012 and M7-020 (the app-facing `MoodState` broadcast and the `ActionList` action-ended caller are not built), M7-014 (the 9 M12/M14/M15 call sites and the 5 built classes whose tables are not among the recovered 13), M7-015 (M10 and M7-021), M7-018 (the M12-M15/FistBump/Hiccup/ReactToSparked class implementations), M7-019 (ReactToCliff needs the `0x55b554`/`0x5c0ca8` helpers = M7-021; ReactToPickup needs M14/SayText; ReactToSparked needs the M15 spark source).
- **Left RECOVERABLE_GAP:** M7-021 (live), M7-022 (non-live).

Two manager-side corrections carried with this pass: M7-005's title now says the table is seven frames and the emitted track eight keyframes; M7-002's manifest evidence now names the correct directories `assets/animationGroupMaps/` and `assets/cubeAnimationGroupMaps/` (Appendix C rows 7-8).

