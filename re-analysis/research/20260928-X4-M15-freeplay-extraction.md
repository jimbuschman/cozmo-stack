# X4 extraction — M15-freeplay

Read-only extraction, 2026-09-28, job X4. Citations are VAs in
`resources/lib/armeabi-v7a/libcozmoEngine.so` (ELF VAs; Thumb). The Ghidra
decompilation (`re-analysis/decomp/libcozmoEngine/`) was a navigation aid only.
The manifest records M15-001..M15-012 (`re-analysis/fidelity_manifest.json`) are
claims; there is no M15 inventory file.

The `0x004a....` THUNK addresses in the record list are 12-byte PLT-style stubs; the
real bodies are cited below (e.g. NeedsManager::Update thunk `0x004a5260` -> body
`0x00695c9c`).

## 1. Production path

| # | step | what the original does | citation | record | classification |
|---|------|------------------------|----------|--------|----------------|
| 1 | NeedsManager construction | `CozmoContext::CozmoContext` calls the ctor (thunk `0x004a4db0` -> body `0x0069210c`); two NeedsState (+0x8, +0x98), NeedsConfig (+0x128), ActionsConfig (+0x19c), LocalNotifications (+0x1b0), DesiredFaceDistortionComponent (+0x3d4) | body `0x0069210c` | NEW | EXACT_SOURCE |
| 2 | NeedsManager::Init call site | `CozmoEngine::Init` passes six `RobotDataLoader` config sections (+0x160/+0x178/+0x190/+0x1a8/+0x1c0/+0x1d8) and the time | `0x004eca0a blx #0x4a50bc` (-> `0x00692574`); args `0x004ec9e6..0x004eca06` | M15-001 (partial) | EXACT_SOURCE |
| 3 | NeedsManager::Init body | NeedsConfig::Init, InitDecay, StarRewardsConfig::Init, ActionsConfig::Init, DesiredFaceDistortionComponent::Init, LocalNotifications::Init, 16 handler registrations, then InitInternal | body `0x00692574` | NEW | EXACT_SOURCE (body) / RECOVERABLE_GAP (which offset is which file) |
| 4 | NeedsManager::Update call site | `CozmoEngine::Update` calls `NeedsManager::Update(now)` every tick | `0x004ed640 blx #0x4a5260` (-> `0x00695c9c`) | M15-001 | EXACT_SOURCE |
| 5 | NeedsManager::Update body | if pause +0x1d5 != 0 return; `LocalNotifications::Update`; if accumulator +0x3b0 <= now: add interval +0x130, `ApplyDecayAllNeeds(robot != 0)`, `SendNeedsStateToGame(1)`, tail `PossiblyWriteToDevice` | `0x00695ca4 ldrb.w r0,[r4,#0x1d5]`; `0x00695cba vldr s0,[r4,#0x3b0]`; `0x00695cc6 bls`; `0x00695cd6 vadd.f32 s0,s0,s2`; `0x00695ce4 blx #0x4bdae8`; `0x00695cec blx #0x4bd9d4` | M15-001 (partial) | EXACT_SOURCE |
| 6 | InitAfterConnection | robot = `RobotManager::GetFirstRobot`; +0x3d0=1, +0x1d4=1 | `0x00694384`; `0x0069438c blx`; `0x00694390 str r0,[r4,#4]`; `0x00694394/98 strb.w` | NEW | EXACT_SOURCE |
| 7 | InitAfterSerialNumberAcquired | keeps previous serial +0x1cc, stores new at +0x34, clears +0x1ca/+0x1c8, `StartReadFromRobot`; on 0 -> `InitAfterReadFromRobotAttempt` | `0x006943a0`; `0x006943ac str.w r2,[r4,#0x1cc]`; `0x006943b0 str r1,[r4,#0x34]`; `0x006943f8/fe blx` | NEW | EXACT_SOURCE |
| 8 | serial-number wiring | `RobotManager::ConnectRobotToNeedsManager(serial)` -> `InitAfterSerialNumberAcquired`; wrapper `RobotInterface::MessageHandler::ConnectRobotToNeedsManager` | `0x0052fadc`; `0x0069dee4` | NEW | RECOVERABLE_GAP (no caller found) |
| 9 | persistence write | `WriteToDevice(bool)` writes a JSON object via `DataPlatform::writeAsJson`: levels, pause flags, star level, seconds timestamp (`system_clock::now()`/1e6) | `0x00693bb0` | M15-004 (partial) | EXACT_SOURCE (write) / RECOVERABLE_GAP (key names in .bss) |
| 10 | needs filename | prefix string + `"_"` + decimal serial + `".json"` | `0x00695224` | M15-004 | EXACT_SOURCE (shape) / RECOVERABLE_GAP (prefix) |
| 11 | decay multipliers | three multipliers init to 1.0; per need take the level and apply the **single** first entry (after a descending sort) whose threshold is <= level | `0x0069c222 mov.w r1,#0x3f800000`; `0x0069c270 vcmpe.f32 s0,s2`; `0x0069c278 bge`; `0x0069c2a4 vmul.f32 s0,s0,s2`; decomp `0069c214.c` lines 43-53; sort `0x00691040` | M15-004 (contradicted) | EXACT_SOURCE |
| 12 | damaged parts | `NumDamagedPartsForRepairLevel` counts the leading run of thresholds >= the Repair level, stopping at the first below | `0x0069ccac`; `0x0069ccc8 vcmpe.f32 s2,s0`; `0x0069ccd0 it mi`/`bxmi lr` | M15-004 | EXACT_SOURCE |
| 13 | activity tree | `ActivityFreeplay::CreateFromConfig` clears the spark map +0x6c, reads `subActivities`, creates each activity, appends keyed by `requireSpark` (+0x4c); reads the four `desiredActivityNames` into +0x68..+0x6b | `0x005ad478`; `0x005ad48a blx`; `0x005ad492 blx`; `0x005ad676 ldr r0,[r0,#0x4c]`; `0x005ad67e blx` | M15-001 | EXACT_SOURCE |
| 14 | activityPriority is dead | parsed with `ParseUint8` and the result is discarded; ordering is the `subActivities` array order | `0x005ad63c blx`; `0x005ad640 ldrb.w r0,[sp,#0x18]` | NEW | EXACT_SOURCE |
| 15 | ActivityFreeplay::Update | delegates to the current activity's virtual +0x20 if one exists, else returns 0 | `0x005ad9d0`; `0x005ad9da ldr r3,[r0,#0x20]` | NEW | EXACT_SOURCE |
| 16 | desired-activity ordering | (a) debug-forced +0x91 differs; (b) requested +0x90 differs; (c) no current; (d) current spark Invalid(0x55) and desired != 0x55; (e) current `WantsToEnd` and pending reward +0x3d8 == 0 | `0x005ae29c`; `0x005ae2ac`; `0x005ae2f4`; `0x005ae34c`; `0x005ae36c blx WantsToEnd`; `0x005ae366 ldrb +0x3d8` | M15-006 | EXACT_SOURCE |
| 17 | interlude / behavior choice | `IActivity::GetDesiredActiveBehavior` calls the activity's virtual +0x30 and may insert an interlude | `0x005ae672 blx #0x4b0fa8`; `0x005b387c` | M15-006 | EXACT_SOURCE |
| 18 | null-pick path | if the activity chose no behavior it is not repicked; if one was chosen but is running +0xa1, or reward pending +0x3d8, or `WantsToEnd`, repick | `0x005ae68c cbz r5,#0x5ae70a`; `0x005ae68e ldrb.w r0,[r5,#0xa1]`; `0x005ae696`; `0x005ae6ac blx` | M15-006 | EXACT_SOURCE |
| 19 | PickNewActivityForSpark | looks up the spark map; iterates in order; asks `WantsToStart` (or `WantsToEnd` for the current when arg==1); first 1 wins; `OnDeselected` old / `OnSelected` new; logs `AIGoalEvaluator.NewGoalSelected` | `0x005adc44` | M15-006 | EXACT_SOURCE |
| 20 | after a pick | `BehaviorManager::SwitchToRequestedSpark`; warn `ActivityNotDesiredSpark` if +0x4c != desired; `FreeplayDataTracker::SetFreeplayPauseFlag(spark != 0x55, 1)` | `0x005ae482 blx`; `0x005ae48e`; `0x005ae4e2..f2 blx #0x4a7b4c` | NEW | EXACT_SOURCE |
| 21 | spark re-selected | if current == desired and +0x65 != 0, log `SparkReselected` and call `PickNewActivityForSpark` with arg 0 | `0x005ae40c..0x005ae464` | NEW | EXACT_SOURCE |
| 22 | desired-from-objects | four-way: face&&cube -> +0x68; face only -> +0x69; neither -> +0x6b; cube only -> +0x6a; stored at +0x90 | `0x005adf4c`; face `0x005adf96`; cube `0x005ae05c`; branches `0x005ae08c/96/a0/a4`; store `0x005ae0aa` | M15-008 | EXACT_SOURCE |
| 23 | WantsToStart | feature gate +0x38; cooldown +0x20 and (duration arg > 0 or startInCooldown +0x28); duration = arg2-arg3; if duration > 0 and <= 2 ticks the cooldown is flat 3.0 s else +0x20; cannot start before lastEnd+cooldown; on pass re-randomise +0x1c + RandDbl(+0x24); mood +0x2c / threshold +0x30; then virtual +8 | `0x005b529c`; `0x005b52a8`; `0x005b52c8 vldr s0,[r6,#0x20]`; `0x005b52ea vsub.f32 s22,s20,s16`; `0x005b5312 vmov.f32 s0,#3.0`; `0x005b531c vadd`; `0x005b53f4 bx r3` | M15-003/005/007 | EXACT_SOURCE |
| 24 | WantsToEnd | two configured durations +0x14/+0x18 against now + passed time; else virtual +0xc | `0x005b5444`; `0x005b5450 vldr s0,[r6,#0x14]`; `0x005b548c vldr s0,[r6,#0x18]`; `0x005b54bc movs r0,#1`; `0x005b54d2 bx r3` | M15-003 | EXACT_SOURCE |
| 25 | IActivityStrategy defaults | cooldown +0x20=-1, last-start +0x14=-1, base +0x1c=-1, randomness +0x24=0, startInCooldown +0x28=0, max duration +0x18=60, mood threshold +0x30=-1, +0x34=-1, gate +0x38=0 | `0x005b4ec8`; `0x005b4edc movt r2,#0xbf80`; `0x005b4f20 movt r1,#0x4270` | M15-003 | EXACT_SOURCE |
| 26 | RandomizeCooldown | +0x20 = +0x1c + RandDbl(+0x24) | `0x005b5408`; `0x005b5412 vldr s16,[r4,#0x1c]`; `0x005b5426 blx RandDbl`; `0x005b543a vstr s0,[r4,#0x20]` | M15-003/005 | EXACT_SOURCE |
| 27 | SetCooldown | +0x1c and +0x20 = arg1; +0x24 = arg2 | `0x005b54e4 strd r1,r1,[r0,#0x1c]`; `0x005b54e8 str r2,[r0,#0x24]` | NEW | EXACT_SOURCE |
| 28 | IActivity::OnSelected | stamps start +0x54; chooser hook +0x24; PublicStateBroadcaster; push driving/idle animations if named; AddEnableRequest; log `robot.freeplay_goal_started`; virtual +0x28 | `0x005b312c`; `0x005b3142 str r1,[this+0x54]` | NEW | EXACT_SOURCE |
| 29 | IActivity::OnDeselected | stamps end +0x58; chooser hook +0x24; remove idle/driving animations; remove info-analyzer request; release SmartDisableReactions locks; SmartRemoveIdleAnimation; clear +0x2c; if NeedsManager +0x3d8 != 0 call `SparksRewardCommunicatedToUser`; log `robot.freeplay_goal_ended`; virtual +0x2c | `0x005b33b8`; `0x005b33ce str r1,[r5,#0x58]`; `0x005b3548 blx #0x4b1dc4` | M15-006 | EXACT_SOURCE |
| 30 | scoring chooser | per behavior `EvaluateScore`; if <=0 skip; if running +0xa1: score += EvaluateY(chooser+0x28, running duration) + 0.1, floored at 0.01; else score += RandDbl; virtual +0x2c; keep the highest; warn if >1 running | `0x0060a3d8`; `0x0060a44a blx EvaluateScore`; `0x0060a466 ldrb +0xa1`; `0x0060a46e/476`; `0x0060a486 vadd`; `0x0060a48a floor`; `0x0060a4e0 blx r8`; `0x0060a52a warn` | M15-002 | EXACT_SOURCE |
| 31 | strict-priority chooser | iterate the behavior vector; if +0xa1 nonzero select it; else if `IsRunnable`==1 select it; else null | `0x0060b23e`; `0x0060b250 ldrb.w r1,[r0,#0xa1]`; `0x0060b258 blx IsRunnable` | M15-002 | EXACT_SOURCE |
| 32 | BehaviorManager pause | `SetCurrentActivity` calls `SetFreeplayPauseFlag(0 GameControl, activity != 1)` | `0x005a106c`; `0x005a1220 blx #0x4a7b4c` | NEW | EXACT_SOURCE |
| 33 | FreeplayDataTracker lifetime | created by `AIComponent` ctor at AIComponent+0x2c; `AIComponent::Update` calls its Update | `0x00569a78`; `0x00569f46 blx #0x4aca48` (-> `0x0056ec1a`) | NEW | EXACT_SOURCE |
| 34 | FreeplayDataTracker ctor/Update | next send +0x18 = now + 30.0 s; Update sends when now >= +0x18 | `0x0056ebd4`; `0x0056ebf6 vmov.f32 s2,#30.0`; `0x0056ec04 vstr s0,[r4,#0x18]`; `0x0056ec2e vcmpe`; `0x0056ec3c SendData` | NEW | EXACT_SOURCE |
| 35 | FreeplayDataTracker::SendData | accumulates nanoseconds while not paused; emits `robot.active_freeplay_time` (<37 s) else errors `DataTooHigh`; resets and sets next send = now + 30.0 | `0x0056ec48` | NEW | EXACT_SOURCE |
| 36 | pause flags | add/erase a flag in the paused tree; names GameControl/Spark/OffTreads/OnCharger at `0x01023554` | `0x0056eebc`; `0x0056eff8`; name table `0x01023554` | NEW | EXACT_SOURCE |
| 37 | pause from off-treads / charger | `Robot::CheckAndUpdateTreadsState` sets flag 2 (OffTreads); `Robot::SetOnChargerPlatform` sets flag 3 (OnCharger) | `0x005121f4 blx #0x4a7b4c`; `0x00511db0 blx #0x4a7b4c` | NEW | EXACT_SOURCE |
| 38 | force flush | `FreeplayDataTracker::ForceUpdate` = SendData, called from the `BehaviorSystemManager` destructor | `0x0056eeb8`; caller `0x005110d4` | NEW | EXACT_SOURCE |
| 39 | needs-action hook | `IBehavior::NeedActionCompleted`: Invalid(0) -> own id +0x68 (from `needsActionID` via `ExtractNeedsActionIDFromConfig`); forwards to `NeedsManager::RegisterNeedsActionCompleted` | `0x005be40c cbnz r1`; `0x005be40e ldr r1,[r0,#0x68]`; `0x005bbae8` | M15-008 | EXACT_SOURCE |
| 40 | feature gate | strategy+0x38 then `CozmoFeatureGate::IsFeatureEnabled`: stringify, lowercase, look up from `config/features.json` | `0x005b52a8..bc`; `0x006a679c`; `0x006a67fe blx`; path `0x00be8449` | M15-007 | EXACT_SOURCE |
| 41 | boredomMultiplier is dead | the string occurs once in the whole .so, in `BehaviorPounceOnMotion`'s ctor; no chooser reads it | `0x005f8384` (only occurrence) | M15-007 | EXACT_SOURCE |
| 42 | cube-in-beacon emotion events | all cubes -> "HikingBroughtLastCubeToBeacon" (29 chars), else "HikingBroughtCubeToBeacon" (25) | `0x005e002c`; `0x005e0038 blx`; `0x005e004a movs r2,#0x1d`; strings `0x5e00cc` / `0x5e00b0` | M15-009 | EXACT_SOURCE |
| 43 | block-moved null location | `GetLocatedObjectByIdHelper`; found -> TurnTowardsPoseAction + 0.5 s WaitAction parallel; not found -> warn and return without starting | `0x00602270`; `0x006022b4 blx`; `0x006022b8 cbz`; `0x00602334..70`; `0x006022fa mov.w r2,#0x3f000000` | M15-010 | EXACT_SOURCE |
| 44 | beacon centred on robot | `SelectNewBeacon` takes the robot's pose and calls `AddBeacon(pose, behaviour+0x128 radius)` | `0x005e5f0c`; `0x005e5f1a`; `0x005e5f28 ldr.w r2,[r4,#0x128]`; `0x005e5f30 blx` (-> `0x0056c39c`) | M15-011 | EXACT_SOURCE |
| 45 | put-down look / tall-stack | `CreateLookAfterPlaceAction`: head+drive parallel, `WaitForImagesAction(robot,2,VisionMode 1)`, trigger 0x199; `CantHandleTallStack` plays trigger 0x1B | `0x005c8174`; `0x005c8242 movs r2,#2`; `0x005c828e movw r2,#0x199`; `0x005ed0f0`; `0x005ed14a movs r2,#0x1b` | M15-012 | EXACT_SOURCE |
| 46 | needs brackets | `UpdateCurNeedsBrackets` 0x0069c12c, `GetNeedBracket` 0x0069cbcc, `IsNeedAtBracket` 0x0069cd80 exist; rules not read | bodies not read | M15-001 (claimed) | RECOVERABLE_GAP: read these against needs_config.json BracketLevel* |
| 47 | pause / disconnect | `SetPaused` 0x00695e04, `OnRobotDisconnected` 0x00695908 exist; not read | as listed | NEW | RECOVERABLE_GAP |
| 48 | config file <-> RobotDataLoader offset | which of +0x160/+0x178/+0x190/+0x1a8/+0x1c0/+0x1d8 is which file is not established | call site `0x004ec9e6..0x004eca0a` | M15-001/004 | RECOVERABLE_GAP |

## 2. Judgement of each existing record

| record | claimed | judgement | evidence |
|--------|---------|-----------|----------|
| M15-001 | EXACT_SOURCE, "NeedsManager update and bracket rules; the shipped activity tree" | **too weak / overclaims** | the tree half is confirmed (`0x005ad478`; map key +0x4c at `0x005ad676`; order preserved). The cited evidence does not cover "NeedsManager update and bracket rules": Update is `0x00695c9c`; brackets `0x0069c12c`/`0x0069cbcc`/`0x0069cd80` are not read. Split it. |
| M15-002 | EXACT_SOURCE, "Scoring and strict-priority choosers..." | confirmed in substance; evidence names a symbol without an address | running bonus `0x0060a466/46e/476/486` (+0.1 at `0x0060a6ec`, floor 0.01 at `0x0060a6f0`); strict priority `0x0060b250/258`. Scoring entry `0x0060a3d8`. |
| M15-003 | EXACT_SOURCE, "Activity start and end rules and the cooldown lifecycle" | confirmed; two citations wrong | WantsToStart `0x005b529c`, WantsToEnd `0x005b5444`. `RandomizeCooldown at 0x005B5336` is the inlined copy; the function is `0x005b5408`. `constructor 0x005B5004` is mid-function; entry `0x005b4ec8`. |
| M15-004 | EXACT_SOURCE, "Needs decay modifiers, damaged parts and persistence" | **contradicted in part / too weak** | decay: single descending bracket (`0x0069c270 vcmpe`, `0x0069c278 bge`, decomp lines 43-53), not "every entry whose threshold the level is at or under". Damaged parts `0x0069ccac` confirmed. Persistence names functions without addresses; the write at `0x00693bb0` exists but its JSON keys are in .bss and were not read. |
| M15-005 | EXACT_SOURCE, "flat 3 s cooldown..." | confirmed | `0x005b5312 vmov.f32 s0,#3.0`; call site `0x005b26fe ldrd r3,r2,[r1,#0x54]`; re-randomise `0x005b5408`. |
| M15-006 | EXACT_SOURCE, "deselection hook and the null-pick path" | confirmed | OnDeselected `0x005b33b8`, pending reward `0x005b3548`; GetDesiredActiveBehaviorInternal `0x005ae29c`, null branch `0x005ae68c -> 0x005ae70a`. |
| M15-007 | EXACT_SOURCE, "feature gate / boredomMultiplier" | confirmed | gate `0x005b52a8..bc`; IsFeatureEnabled `0x006a679c`; path `0x00be8449`; `boredomMultiplier` only at `0x005f8384`. |
| M15-008 | EXACT_SOURCE, "needsActionID hook and desired-from-objects ordering" | confirmed | store `0x005ae0aa`; branches `0x005ae08c/96/a0/a4`; `0x005be40c`; `0x005bbae8`. The long behaviour call-site list was not re-verified line by line. |
| M15-009 | EXACT_SOURCE, "emotion event names..." | confirmed | `0x005e002c`; strings `0x5e00cc`/`0x5e00b0`. |
| M15-010 | EXACT_SOURCE, "block location no longer valid ends the behaviour" | confirmed | `0x00602270`; `0x006022b8 cbz`; null return `0x00602370`. |
| M15-011 | EXACT_SOURCE, "beacon centred on the robot" | confirmed | `0x005e5f0c`; radius `0x005e5f28`; AddBeacon `0x0056c39c`. |
| M15-012 | EXACT_SOURCE, "images waited for / CantHandleTallStack trigger" | confirmed | `0x005c8174`; `0x005c8242 movs r2,#2`; trigger 0x199 `0x005c828e`; trigger 0x1B `0x005ed14a`. |

## 3. Existing records contradicted by the source

- **M15-004** (EXACT_SOURCE): the decay-modifier sentence is wrong. `GetDecayMultipliers`
  `0x0069c214` applies the **single** first entry (descending sort at `0x00691040`) whose
  threshold is <= the level (`0x0069c270 vcmpe.f32 s0,s2`; `0x0069c278 bge`; decomp
  `0069c214.c` lines 43-53), not every entry at or under the level.

## 4. Records whose evidence is too weak to keep their status

- **M15-001:** the title bundles the NeedsManager update/bracket rules with the activity
  tree; only the tree is cited. The update is `0x00695c9c` and the brackets are
  `0x0069c12c`/`0x0069cbcc`/`0x0069cd80`. Split the record before it can stay
  EXACT_SOURCE.
- **M15-002:** substance is confirmed, but the evidence names a symbol with no address
  (the scoring chooser is `0x0060a3d8`). Add the address.
- **M15-003:** two citations are wrong (`0x005b5336` is an inlined copy; `0x005b5004`
  is mid-function). Correct to `0x005b5408` and `0x005b4ec8`.
- **M15-004:** the persistence paragraph names functions with no addresses and the
  JSON key names were not read, so it proves only that a write exists.

## 5. NEW steps no M15 record covers

- **FreeplayDataTracker** (rows 33-38): the 30 s active-freeplay accumulator, its
  `robot.active_freeplay_time` event, its four pause flags and their callers.
- **Pause flags** and their names at `0x01023554`; GameControl from
  `BehaviorManager::SetCurrentActivity` (`0x005a1220`), Spark from
  `GetDesiredActiveBehaviorInternal` (`0x005ae4e2`), OffTreads from
  `Robot::CheckAndUpdateTreadsState` (`0x005121f4`), OnCharger from
  `Robot::SetOnChargerPlatform` (`0x00511db0`).
- **`activityPriority` is parsed and discarded** (`0x005ad63c`/`0x005ad640`). Ordering
  is the JSON array order. If the C# treats it as meaningful, that is a divergence.
- **NeedsManager <-> Robot wiring**: `RobotManager::ConnectRobotToNeedsManager`
  `0x0052fadc`, `RobotInterface::MessageHandler::ConnectRobotToNeedsManager`
  `0x0069dee4` (no caller found).
- **NeedsManager lifecycle**: InitAfterConnection `0x00694384` (call `0x004ed10e`),
  InitAfterSerialNumberAcquired `0x006943a0`, SetPaused `0x00695e04`,
  OnRobotDisconnected `0x00695908`, PossiblyWriteToDevice `0x00695dc4`,
  StartReadFromRobot `0x0069449c`, AttemptReadFromDevice `0x00693690`.
- **NeedsState brackets**: `0x0069c12c`, `0x0069cbcc`, `0x0069cd80` (claimed by
  M15-001, never cited).
- **IActivity::OnSelected** `0x005b312c` and the interlude logic in
  `IActivity::GetDesiredActiveBehavior` `0x005b387c` (M15-006 covers OnDeselected only).
- **Chooser factory / selection chooser**: `BSRunnableChooserFactory::CreateBSRunnableChooser`
  `0x00609c88`; `SelectionBSRunnableChooser::OnSelected/OnDeselected`
  `0x0060af20`/`0x0060af64`.
- **SetCooldown** `0x005b54e4`; **ForceUpdate** from the BehaviorSystemManager
  destructor `0x005110d4`.

## 6. Open questions

1. **Serial trigger:** `ConnectRobotToNeedsManager` `0x0069dee4` has no caller in the
   decomp. Where does the serial that reaches `InitAfterSerialNumberAcquired` come from,
   and on which message? This is the start of M15-004's persistence path.
2. **Config-file mapping:** which RobotDataLoader offset is needs_config,
   needs_decay_config, actions, star rewards, face distortion, local notifications?
3. **NeedsState bracket rules** (M15-001's uncited half): read `0x0069c12c` /
   `0x0069cbcc` / `0x0069cd80` against needs_config.json BracketLevel*.
4. **Persistence format:** the `WriteToDevice` JSON key names and the filename prefix
   are in .bss. Does the C# layout have to match them?
5. **`activityPriority`:** confirm nothing else reads it.
6. **Scoring graph semantics:** the chooser's `scoreBonusForCurrentBehavior` graph at
   chooser+0x28 and `EvaluateScore`'s config were not read.
7. **`BehaviorManager::SetCurrentActivity` first arg:** pause flag 0 (GameControl) is
   set when the HighLevelActivity != 1; confirm the enum meaning.
8. **M15-004's decay wording** must be corrected before that record can stay
   EXACT_SOURCE.

## 7. What could not be established (UNKNOWN)

- The JSON key strings written by `NeedsManager::WriteToDevice` (.bss).
- The filename prefix in `NeedsFilenameFromSerialNumber`.
- The trigger/message that carries the serial to `ConnectRobotToNeedsManager`.
- The bracket thresholds/logic of `NeedsState` (functions exist, bodies not read).
- Whether anything else reads `activityPriority`.
