# M15 freeplay inventory

This inventory integrates `re-analysis/research/20260928-X4-M15-freeplay-extraction.md`,
gap passes `20260928-I-M15-gap1-extraction.md` and
`20260928-I-M15-gap2-extraction.md`, and the independent raw-instruction check in
`20260928-I-M15-verification.md`. Addresses are Thumb VAs in the shipped
`resources/lib/armeabi-v7a/libcozmoEngine.so`; shipped JSON and Unity enum sources are
identified where used. Decompilation was navigation only.

## Records

| record | production behavior and source rows | status |
|---|---|---|
| M15-001 | NeedsManager construction, six-file configuration, 16 registrations, tick update, and NeedsState bracket calculation (X4 1–5, 46, 48; gap1 config/brackets). | IMPLEMENTATION_GAP |
| M15-002 | Chooser factory plus scoring, strict-priority and selection choosers, including current-behavior graph bonus, random tie noise and running-byte handling (X4 30–31; gap1 scoring graph). | IMPLEMENTATION_GAP |
| M15-003 | Activity-strategy defaults, WantsToStart/WantsToEnd, randomized and explicit cooldown lifecycle (X4 23–27). | IMPLEMENTATION_GAP |
| M15-004 | Needs decay modifiers and damaged-part thresholds; one first matching descending decay bracket is applied (X4 11–12; gap1 decay correction). | IMPLEMENTATION_GAP |
| M15-005 | Flat three-second cooldown when the prior run was positive and no longer than two ticks (X4 23, 26). | IMPLEMENTATION_GAP |
| M15-006 | Activity selection, re-selection, interlude, null-pick, OnSelected and OnDeselected lifecycle, reward-pending behavior and post-pick effects (X4 15–21, 28–29). | IMPLEMENTATION_GAP |
| M15-007 | Feature-gate lookup and the fact that the activity scoring `boredomMultiplier` is dead data (X4 40–41). | IMPLEMENTATION_GAP |
| M15-008 | Desired activity from face/cube presence and behavior-owned needs-action reporting (X4 22, 39). | IMPLEMENTATION_GAP |
| M15-009 | Exact emotion-event names selected when a cube reaches a beacon (X4 42). | IMPLEMENTATION_GAP |
| M15-010 | Missing moved-block location warns and ends without starting a reaction (X4 43). | IMPLEMENTATION_GAP |
| M15-011 | New beacon is centered on the robot pose with configured radius (X4 44). | IMPLEMENTATION_GAP |
| M15-012 | Put-down image wait/action sequence and CantHandleTallStack animation trigger (X4 45). | IMPLEMENTATION_GAP |
| M15-013 | Shipped activity tree construction, desired-name fields, JSON ordering, and parsed-but-discarded `activityPriority` in both activity containers (X4 13–14; gap1 priority). | IMPLEMENTATION_GAP |
| M15-014 | Needs connection and per-serial persistence lifecycle, exact filenames/keys and elapsed-time decay. The serial edge is recovered (C2): the `mfgId` tag-0xED callback calls `ConnectRobotToNeedsManager(serial)`; the wrapper chain, the key-0x194000 read and the resolver are cited. | IMPLEMENTATION_GAP |
| M15-015 | FreeplayDataTracker lifetime, 30-second accumulation/reporting, force flush, and GameControl/Spark/OffTreads/OnCharger pause sources (X4 20, 32–38; gap1 enum). | IMPLEMENTATION_GAP |
| M15-016 | NeedsManager pause and disconnect state transitions, forced/conditional writes, schedule adjustment, notifications and bracket/DAS handling (X4 47; gap1 pause/disconnect). | IMPLEMENTATION_GAP |
| M15-017 | The `NeedsStateOnRobot` robot NV blob (key `0x194000`): the 0x74-byte serialized layout, `Pack`/`Unpack`, and the version 1-4 conversion in `FinishReadFromRobot` (C3, Appendix H). | EXACT_SOURCE |
| M15-018 | The version-0/unsupported robot state blob: the engine reads uninitialised stack fields; forced policy reads zeros and sets the rewrite flag (C3, Appendix H; SD2). | COMPATIBILITY_POLICY |

All source-settled records are `IMPLEMENTATION_GAP` until the implementation phase
compares or builds them. M15-014's live-path `RECOVERABLE_GAP` is closed by correction
C2 (Appendix F): the missing production-path edge and its ordering are recovered, so the
record becomes `IMPLEMENTATION_GAP` (to build). Its whole path — the `mfgId` callback's
needs connection, `InitAfterSerialNumberAcquired`, the key-`0x194000` read with its
callback and immediate fallback, the per-serial filename and the robot/device resolution
and writes — is to be built from the C2 rows. Whether the robot ever omits `mfgId` tag
`0xED` is firmware behaviour and stays HARDWARE_ONLY (C2 §14).

## Decisions

- **SD1 (exactness):** applied throughout. Existing implementations and tests do not
  upgrade provenance; all pre-process EXACT_SOURCE records return to
  IMPLEMENTATION_GAP for comparison.
- **SD2 (split composite claims):** applied to old M15-001 and M15-004. Activity-tree
  construction is M15-013; persistence/wiring is M15-014; pause/disconnect is M15-016.
- **SD3 (production-path ownership):** applied to M15-014. The known persistence body
  is not called settled while the serial-dispatch edge is unknown.
- **SD4 (absence claims):** the dead `activityPriority` and `boredomMultiplier` claims
  are limited to the searches and concrete parse/load sites stated in the reports.

## Existing evidence corrected or rejected

- M15-001 bundled three paths while citing only activity construction. Update and
  brackets now have exact addresses, and the activity tree is split to M15-013.
- M15-002 named the scoring chooser without an address; its body is `0x0060A3D8`.
- M15-003 cited `0x005B5336` and `0x005B5004`; the correct function entries are
  `0x005B5408` and `0x005B4EC8`.
- M15-004 was contradicted: decay does not apply every matching threshold entry. It
  stops at the first match in descending order and applies that one entry. Persistence
  also lacked key and lifecycle evidence and is now isolated in M15-014.

## Appendix A — X4 extraction report (verbatim)

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


## Appendix B — gap pass 1 (verbatim)
# I-M15 gap pass 1 — needs lifecycle, persistence and freeplay configuration

Read-only extractor pass, 2026-09-28. Citations are Thumb VAs in
`resources/lib/armeabi-v7a/libcozmoEngine.so`; decompilation was navigation only.

| open question | recovered result | citation | classification |
|---|---|---|---|
| RobotDataLoader config mapping | `NeedsManager::Init` receives, in order: `needs_config.json` (+0x160), `needs_level_config.json` (+0x178), `needs_action_config.json` (+0x190), `needs_decay_config.json` (+0x1A8), `needs_handlers_config.json` (+0x1C0), and `local_notification_config.json` (+0x1D8). The first four initialise NeedsConfig, StarRewardsConfig, ActionsConfig and decay; the last two initialise desired-face distortion handlers and local notifications. | `RobotDataLoader::LoadConfig` body `0x00522280`; call setup `0x004EC9E6..0x004ECA0A`; `NeedsManager::Init` `0x00692574` | EXACT_SOURCE |
| Needs brackets | When dirty byte +0x88 is set, `UpdateCurNeedsBrackets` updates each need 0..2. It scans that need's shipped threshold vector in order and takes the first index whose threshold is <= the current level, or the last index if none matches; it stores the index in the +0x70 cache and clears dirty. `GetNeedBracket` updates then returns the cached index; an invalid need warns and returns 4. `IsNeedAtBracket` updates then compares; an invalid need errors and returns false. | `0x0069C12C..0x0069C208`; `0x0069CBCC..0x0069CC50`; `0x0069CD80..0x0069CE28`; shipped `needs_config.json` | EXACT_SOURCE |
| Shipped bracket thresholds | Repair: 0.99, 0.60, 0.27, 0. Energy: 0.99, 0.60, 0.21, 0. Play: 0.99, 0.50, 0.14, 0. | shipped `needs_config.json` | EXACT_SOURCE |
| Persistence filename | The fixed filename is `needsState.json`; the per-serial filename is `needsState_` + decimal serial + `.json`. | static initialiser `0x004D8DDC..0x004D8E10`; `NeedsFilenameFromSerialNumber` `0x00695224..0x006952BC` | EXACT_SOURCE |
| Persistence JSON keys | The static initialiser establishes `_StateFileVersion`, `_DateTime`, `_SerialNumber`, `CurNeedLevel`, `PartIsDamaged`, `CurNeedsUnlockLevel`, `NumStarsAwarded`, `NumStarsForNextUnlock`, `TimeCreated`, `TimeLastStarAwarded`, `TimeLastDisconnect`, `TimeLastAppBackgrounded`, `OpenAppAfterDisconnect`, and `ForceNextSong`. | `0x004D8DDC..0x004D9142`; write `0x00693BB0`; read `0x006998B4` | EXACT_SOURCE |
| Pause/disconnect lifecycle | `SetPaused` ignores no transition: on pause it records the pause state/time, sends state, and forces a device write; on unpause it shifts scheduled times by the pause duration. It always updates LocalNotifications and sends the pause state. `OnRobotDisconnected` records time, clears the serial-associated state, writes when not paused, clears the robot pointer, snapshots needs, detects bracket changes and sends DAS. | `0x00695E04..0x00695F7E`; `0x00695908..0x0069594A` | EXACT_SOURCE |
| HighLevelActivity value 1 | Value 1 is `Freeplay`; therefore `BehaviorManager::SetCurrentActivity` pauses GameControl active-freeplay tracking whenever the activity is not Freeplay. | Unity `Anki.Cozmo.HighLevelActivity` enum; engine `0x005A106C..0x005A1224` | EXACT_SOURCE |
| `activityPriority` | Both `ActivityFreeplay::CreateFromConfig` and `ActivityStrictPriority` parse `activityPriority` into a temporary and discard it. Child order remains JSON array order. No load of either temporary feeds selection. | `0x005AD5CC..0x005AD69C`; `0x005B23DC..0x005B252C` | EXACT_SOURCE |
| Scoring graph | `ScoringBSRunnableChooser::ReloadFromConfig` clears chooser+0x28 and reads `scoreBonusForCurrentBehavior` with `GraphEvaluator2d::ReadFromJson`; absent data installs a fallback node. The selection body evaluates that graph at the current behavior's running duration, adds 0.1, and floors the result at 0.01. | `0x00609F8C`; `0x0060A3D8..0x0060A654` | EXACT_SOURCE |
| Decay wording | Each need's modifier list is sorted descending. `GetDecayMultipliers` chooses only the first entry whose threshold is <= current level and applies that entry's multipliers; it does not combine all matching entries. | sort `0x00691040`; `0x0069C214..0x0069C2B0` | EXACT_SOURCE; contradicts old M15-004 wording |

The serial-number trigger remained open and was sent to a second targeted pass.


## Appendix C — gap pass 2 (verbatim)
# I-M15 gap pass 2 — serial-number trigger

Read-only extractor pass, 2026-09-28. The search covered the exported/thunked and
body forms of the relevant functions, all direct Thumb `bl`/`blx` targets in the
executable segments, Ghidra's caller index, and the RobotInterface message-handler
wrapper.

Recovered facts:

- `RobotManager::ConnectRobotToNeedsManager(serial)` tail-calls
  `NeedsManager::InitAfterSerialNumberAcquired(serial)` (`0x0052FADC`).
- `RobotInterface::MessageHandler::ConnectRobotToNeedsManager` is an eight-byte
  wrapper at `0x0069DEE4`; its thunk is `0x004A9B2C`.
- No direct call to the body, thunk, or RobotManager wrapper exists in the executable
  instruction stream, and the decompiler caller index has no caller for the body.

The remaining edge is therefore an indirect/generated message-dispatch registration
or a caller outside this native image. Its message/tag and registration point were not
established. This remains a live-path `RECOVERABLE_GAP`. Exact remaining work: recover
the RobotInterface generated dispatch table or registration data that owns wrapper
`0x0069DEE4`/thunk `0x004A9B2C`, then identify the inbound message field carrying the
serial and its call order relative to robot connection.


## Appendix D — verifier report (verbatim)
# I-M15 verifier report

Read-only verifier pass, 2026-09-28. I opened the cited Thumb instructions directly
in `resources/lib/armeabi-v7a/libcozmoEngine.so`; the checked set includes every row
that changes or contradicts an existing record and more than twenty additional rows.

Checked ranges included `0x004EC9E6..0x004ECA12`, `0x004ED63C..0x004ED644`,
`0x00695C9C..0x00695CF4`, `0x0069C12C..0x0069C208`,
`0x0069CBCC..0x0069CC50`, `0x0069CD80..0x0069CE28`,
`0x0069C214..0x0069C2B0`, `0x0069CCAC..0x0069CCD2`,
`0x005AD478..0x005AD690`, `0x005AD9D0..0x005AD9DE`,
`0x005AE29C..0x005AE380`, `0x005ADC44..0x005ADD40`,
`0x005ADF4C..0x005AE0B0`, `0x005B529C..0x005B53F4`,
`0x005B5444..0x005B54D2`, `0x005B4EC8..0x005B4F20`,
`0x005B5408..0x005B543A`, `0x005B312C..0x005B3180`,
`0x005B33B8..0x005B3560`, `0x0060A3D8..0x0060A550`,
`0x0060B23E..0x0060B280`, `0x005A106C..0x005A1230`,
`0x00569A78..0x00569AA0`, `0x0056EBD4..0x0056EC48`,
`0x0056EC48..0x0056EE20`, `0x0056EEB8..0x0056EFD0`,
`0x005BE40C..0x005BE416`, `0x005E002C..0x005E0080`,
`0x00602270..0x00602370`, `0x005E5F0C..0x005E5F36`,
`0x005C8174..0x005C829C`, and `0x005ED0F0..0x005ED15C`.

Result: the rows are supported by the opened instructions except the extractor's
explicitly identified old M15-004 wording, which is contradicted exactly as reported:
the threshold loop stops at the first match before applying one modifier entry. The
old M15-001 evidence is also insufficient for its bundled claim, and the old M15-002
and M15-003 citations need the corrected entry addresses. No additional unsupported,
contradicted, omitted behavior, circular-test claim, race, or deadlock was found in
the extraction evidence. The serial-dispatch edge remains the stated recoverable gap.

## Appendix E — correction C1 (build pass, 2026-09-29)

Read-only extraction for job B-M15, `2026-09-29` (report `.scratch/m15-build/extract.md`).
Six open questions the build phase raised were read in `resources/lib/armeabi-v7a/libcozmoEngine.so`;
every fact below was checked against the instruction stream, not the decompilation. This corrects
rows 5, 21, 31, 35, 37 and 47.

1. **Row 21 (M15-006) — the "spark re-selected" byte is `BehaviorManager+0x65`, not
   `ActivityFreeplay+0x65`.** `0x005AE40C..0x005AE46E`: the block loads the current spark at
   `BehaviorManager+0x58` and the desired spark at `+0x60`; when they are equal and
   `BehaviorManager+0x65 != 0` it logs `ActivityFreeplay.ChooseNextBehavior.SparkReselected`
   ("Spark re-selected: none behavior will be selected", strings `0x00BF1AD4`/`0x00BF1B08`) and
   calls `PickNewActivityForSpark(..., 0)` (`0x005AE46E`). `+0x65` is set to 1 **only** by an
   `ActivateSpark` message whose `UnlockId == 0x55` (`BehaviorManager::HandleMessage` case tag 0:
   `0x005A3C92..0x005A3C9A`), and cleared by `BehaviorManager::SwitchToRequestedSpark`
   (`0x005A4220`) and the constructor (`0x005A0900`). The fourth argument of
   `PickNewActivityForSpark` is the "ask the current activity `WantsToEnd`" flag: arg 0 means it is
   not asked (`0x005ADC64..0x005ADC74`). Classification EXACT_SOURCE.

2. **Row 5 (M15-001) — `SendNeedsStateToGame`, `PossiblyWriteToDevice`, `ApplyDecayAllNeeds`.**
   - `SendNeedsStateToGame` `0x0069383C` (thunk `0x004BD9D4`) takes a `NeedsActionId`; it refreshes
     the brackets, builds the level/bracket/damaged-part vectors and sends one `MessageEngineToGame`
     carrying a `NeedsState` whose `actionCausingTheUpdate` is the argument. The `Update` call passes
     `1 = Decay` (`unity/scripts/csharp/Anki.Cozmo/NeedsActionId.cs`).
   - `PossiblyWriteToDevice` `0x00695DC4` is a **61 ms rate limiter**: `0x00695DD2/DA` build
     `0x03A2C940 = 61,000,000` ns; if the elapsed time since the stored write time
     (`this+8/+0xC`) is at least that, it stores now and calls `WriteToDevice(this, false)`
     (`0x00695DFA`), otherwise returns.
   - `ApplyDecayAllNeeds` `0x00695CFE` contains **two** skips: a per-need pause flag at `+0x1DC`
     (`0x00695D36`; written by `HandleMessage<SetNeedsPauseStates>` `0x00698918`), and the
     fullness-cooldown deadline at `+0x208` (`0x00695D4C..0x00695D58`, `0x00695D84`; written by
     `NeedsManager::StartFullnessCooldownForNeed` `0x006970AC` = `now + config value`). Both are in
     this loop; the fullness cooldown was already the stack's behaviour and is now cited.
   Classification EXACT_SOURCE.

3. **Row 31 (M15-002) — the selection chooser's message.** `SelectionBSRunnableChooser`'s
   constructor `0x0060A848` subscribes to the RobotInterface/ExternalInterface
   `MessageGameToEngine` dispatch for `ExecuteBehaviorByExecutableType` (tag 0x93) and
   `ExecuteBehaviorByID` (tag 0x94) (`0x0060A8AC..0x0060A93E`). `HandleExecuteBehavior`
   (`0x0060AA58`) resolves the behaviour through `BehaviorManager::FindBehaviorByID`/`ByExecutableType`
   and stores it at chooser `+0x2C` with `numRuns` (message field `+4`) at `+0x3C`
   (`0x0060AA86..0x0060AC30`). So the requested-behaviour setter's caller is the game-message
   dispatch, which this stack does not build. Classification EXACT_SOURCE; the unbuilt layer is the
   game-message dispatch.

4. **Row 35 (M15-015) — the tracker accumulates in `SendData`, not `Update`.**
   `FreeplayDataTracker::Update` `0x0056EC1A` only checks `now >= +0x18` and tail-calls `SendData`.
   `SendData` `0x0056EC48` reads `BaseStationTimer::GetCurrentTimeInNanoSeconds`, and while the pause
   set is empty (`this+8 == 0`, `0x0056EC5A..0x0066`) adds `now - lastTimestamp` (`+0x10/+0x14`) to
   the accumulator (`+0x20/+0x24`), storing the new accumulator and the new last timestamp. It then
   rounds `accum / 1e9`: under 37 (`0x25`) it fires `robot.active_freeplay_time`, else
   `FreeplayDataTracker.SendData.DataTooHigh`; it zeroes the accumulator and sets
   `+0x18 = nowSeconds + 30.0`. The pause set's internal setter (`0x0056EECC`) flushes the running
   segment into the accumulator when pausing, and `ClearFreeplayPauseFlag` `0x0056EFF8` stamps
   `+0x10 = nowNanos` when the set becomes empty. The four flag names at `0x01023554` are
   `GameControl` `0x00BEE313`, `Spark` `0x00BEE31F`, `OffTreads` `0x00BEE325`, `OnCharger`
   `0x00BEE32F`. Classification EXACT_SOURCE.

5. **Row 37 (M15-015) — GameControl is also set at init.** `BehaviorManager::SetCurrentActivity`
   `0x005A120A..0x005A1220` calls `SetFreeplayPauseFlag(tracker, activity != 1, 0)` where the
   `HighLevelActivity` values are 0 Feeding, 1 Freeplay, 2 Selection (Unity
   `Anki.Cozmo.HighLevelActivity`). `BehaviorManager::InitConfiguration` `0x005A0DFC` also calls it
   with flag 0: the null-config branch sets it paused (`0x005A0E48..0x005A0E56`), and the
   config-present branch calls `SetCurrentActivity(2 Selection, 1)` (`0x005A0F46..0x005A0F4C`),
   which also pauses it. So a Freeplay-only stack that never switches high-level activity leaves
   GameControl set unless it explicitly clears it. Classification EXACT_SOURCE.

6. **Row 47 (M15-016) — exact `SetPaused`/`OnRobotDisconnected`.**
   - `SetPaused` `0x00695E04`: if the new state equals `+0x1D5` it logs
     `NeedsManager.SetPaused.Redundant` and returns with no send, write or notification
     (`0x00695E0C..0x00695E12`). Pausing: store `+0x1D5 = 1`, log `Pausing`, write the pause
     timestamp to `+0x1D8 = now`, store `+0x3B4 = +0x3B0 - now`, call `SendNeedsStateToGame(0)`
     (`NoAction`, `0x00695EC2`), `WriteToDevice(this, true)` (`0x00695ECA`). Unpausing: store
     `+0x1D5 = 0`, log `UnPausing`, compute `pauseDuration = now - +0x1D8`, store
     `+0x3B0 = now + +0x3B4`, add `pauseDuration` to each need's `+0x1E4`, `+0x1F0`, and (when
     non-zero) `+0x208`/`+0x1FC`, and to `+0x214`; **no** `SendNeedsStateToGame` and **no**
     `WriteToDevice`. Both non-redundant branches end with `LocalNotifications::SetPaused` and
     `SendNeedsPauseStateToGame` (`0x00695F6C..0x00695F78`).
   - `OnRobotDisconnected` `0x00695908`: write the timestamp to `+0x18/+0x1C`; clear `+0x30`; if not
     paused call `WriteToDevice(this, true)` (`0x00695924..0x0069592A`); clear the robot pointer
     `+4`; snapshot the needs into `+0x1B8`; `DetectBracketChangeForDas(true)` (`0x0069593C`); and
     `SendNeedsLevelsDasEvent("disconnect")` (string `0x0069594C`). There is **no**
     `SendNeedsStateToGame` here.
   Classification EXACT_SOURCE.

## Appendix F — correction C2 (G-M15, 2026-09-29)

Read-only source: Codex's independent extraction
`re-analysis/research/20260929-M15-014-needs-connection-extraction.md` (the manager's
spot-check confirmed the missing edge), checked row by row against
`resources/lib/armeabi-v7a/libcozmoEngine.so` by `@cozmo-verifier`. Two rows failed the
first pass and were re-extracted and re-verified; the corrected facts are below and
supersede the report's rows 3, 4/14 and 11. Citations are Thumb VAs; the Ghidra
decompilation was navigation only.

### C2 production path (the M15-014 rows, checked)

| # | what the original does | citation |
|---|---|---|
| 1 | Startup device read: `NeedsManager::InitInternal` calls `InitReset` (`0x00693450`), clears the device `versionUpdated` out-flag at `+0x1CB` (`0x0069345C`; the flag is passed to `AttemptReadFromDevice` at `0x00693456`/`0x00693466`) and the device-read result at `+0x1C9` (`0x00693462`), stores the attempt's result into `+0x1C9` (`0x0069346C`), sends the default state if the read failed (`0x00693472..0x00693476`) and calls `WriteToDevice(this,true)` unconditionally (`0x0069347A..0x00693480`). It does **not** touch the robot-rewrite flag `+0x1CA`. | `0x00693444..0x0069348E` |
| 2 | `AttemptReadFromDevice` tests the fixed `needsState.json` exists (`0x0069369A..0x006936A0`), calls `ReadFromDevice` (`0x006936A2..0x006936AE`); a successful read snapshots the timestamp, applies elapsed-time decay, sends the needs state and increments the background counter and returns 1 (`0x006936B0..0x0069371E`); missing or failed returns 0 (`0x00693720..0x0069378E`). | `0x00693690..0x00693790` |
| 3 | `ReadFromDevice` clears its out `versionUpdated` Boolean at `0x006998C8`; a failed `readAsJson` (`0x006998E8..0x00699988`) or a state-file version `> 5` (gate `0x00699904` asInt / `0x00699908` / `0x0069990A cmp sb,#6` / `0x0069990E blt`, rejection return 0 at `0x00699986`) returns 0; a supported version loads `_DateTime`, `_SerialNumber` (`0x00699A1C..0x00699A2C`), needs/repair/star fields and version-dependent fields, marks the device NeedsState dirty, updates brackets, sets the out Boolean only where an old format needs rewriting and returns 1 (`0x00699B90..0x00699BB0`). `0x006999B0..0x006999C0` is the `version < 3` timestamp branch (`0x006999B4 cmp.w sb,#3`), not the `>5` gate. | `0x006998B4..0x00699BB8` |
| 4 | Successful-handshake registration: `RobotInitialConnection::OnNotified(0, fw)` sets validation (`0x0052DDD0`), registers the RobotToEngine tag `0xED` callback (`0x0052DE04`/`0x0052DE06`; subscribe `0x0052DE34` → `0x00519F8C`, handle retained by the append `0x0052DE3C` → `0x0052D44C`), and sends EngineToRobot `GetManufacturingInfo` tag `0x25` (`0x0052DE5E..0x0052DE80`; tag immediate `0x007A806E`). The subscription is **persistent, not one-shot**: `0x0052D44C..0x0052D46C` is a pure append, the callback `0x0052E2F8..0x0052E3B8` never unsubscribes, and no explicit unsubscribe path exists; it is released only by RAII when the `RobotInitialConnection` object is destroyed. | `0x0052DE04..0x0052DE40`, `0x0052DE5E..0x0052DE80` |
| 5 | Inbound `mfgId` tag `0xED`: payload is three little-endian u32 (12 bytes). The callback stores word 0 at `RobotInitialConnection+0x24` (serial), word 1 at `+0x28` (hardware version) and the low byte of word 2 at `+0x2C` (body colour, validated as 0/2/3/4). The serial supplied to needs is `mfgId.word0`. | `0x0052E2F8..0x0052E31C`; generated `ManufacturingID` unpack `0x007B1750..0x007B177C`, pack `0x007B17E6..0x007B1820`, size `0x007B1856..0x007B1858` (`0xC`) |
| 6 | The `mfgId` callback sends `SendConnectionResponse` (`0x0052E3A2`), calls `ReadLabAssignmentsFromRobot(serial)` (`0x0052E3AA`), then loads the same serial from `+0x24` and calls `blx #0x004A9B2C` (`0x0052E3AE..0x0052E3B2`), whose body is `RobotInterface::MessageHandler::ConnectRobotToNeedsManager` at `0x0069DEE4`. | `0x0052E39C..0x0052E3B2` |
| 7 | Wrapper chain: `0x0069DEE4 ldr r0,[r0,#0x20]` / `b.w #0x8CDB0C` (veneer) → PLT `0x004BE214` = `RobotManager::ConnectRobotToNeedsManager`; body `0x0052FADC ldr r0,[r0,#0x18]` / `ldr r0,[r0,#0x34]` / `b.w #0x8CB32C` (veneer) → PLT `0x004A9DB4` = `NeedsManager::InitAfterSerialNumberAcquired`. The serial is preserved in `r1` across both hops. | `0x0069DEE4..0x0069DEE8`; `0x0052FADC..0x0052FAE6`; veneers `0x008CDB0C`, `0x008CB32C`; PLTs `0x004BE214`, `0x004A9DB4` |
| 8 | Ordering: `SendConnectionResponse(Success)` runs before both the lab read and the needs connection (`0x0052DF2E..0x0052DF64`); its UI broadcast delivers synchronously (`0x006625C6..0x006625FA`) before the callback proceeds to the lab read and the needs wrapper. | callback `0x0052E39C..0x0052E3B2` |
| 9 | `InitAfterSerialNumberAcquired` copies the prior device-loaded serial `+0x34` to `+0x1CC`, stores the inbound serial at `+0x34`, clears robot-data/robot-rewrite flags `+0x1C8`/`+0x1CA` and calls `StartReadFromRobot`. | `0x006943A0..0x006943F8`; esp. `0x006943A8..0x006943B0`, `0x006943EC..0x006943F8` |
| 10 | `StartReadFromRobot` (body `0x0069449C..0x00694553`) queues an NVStorage read of key `0x194000` on the connected robot's NV component (`0x006944B4..0x006944D0`). Success returns 1 and resolution waits for the callback (`0x006944E6..0x006944EA`); failure logs, clears `+0x3D0`, returns 0 (`0x006944EC..0x0069453E`), and `InitAfterSerialNumberAcquired` immediately calls `InitAfterReadFromRobotAttempt` (`0x006943F8..0x00694402`). The queued/failed value is `NVStorageComponent::Read`'s return (`0x00644E14..0x00644EF7`): 1 = valid tag and queued, 0 = invalid tag only (no absent-component, queue-full or in-flight failure; see C2 §Q2). | `0x006944B4..0x006944D0`, `0x006943F8..0x00694402`, `0x00644E14..0x00644EF7` |
| 11 | Robot-read callback: clears `+0x3D0` (`0x0069BEC0`), calls `FinishReadFromRobot(data,size,result)` (`0x0069BEC6`), stores its Boolean at `+0x1C8` (`0x0069BECA`), always tail-calls `InitAfterReadFromRobotAttempt` (`0x0069BED4` → `0x004BDAA0`). **Corrected return contract** of `FinishReadFromRobot` (`0x00699DB0..0x0069A1B3`): a missing NV item (result `-1`) and any other NV failure (result `< -1`) return 0; a state version `> 5` returns 0 via `sVerifyFailedReturnFalse`; versions 1–4 return 1 and set `+0x1CA = 1` (`0x00699E64`); version 5 returns 1 and does **not** set `+0x1CA`; the unsupported old version 0 also returns **1** and sets `+0x1CA = 1` (debug log `0x00699FFE`, same success tail). The only zero is `0x00699F06`; the success constant is `0x0069A176`. | callback `0x0069BEB2..0x0069BED4`; `0x00699DB0..0x00699F06`, `0x00699E56..0x0069A192`, `0x0069A192..0x0069A1AC` |
| 12 | Per-serial filename and alternate read: `NeedsFilenameFromSerialNumber` reads the serial at `this+0x34` and returns `needsState_` + its unsigned decimal + `.json`. During resolution, if the robot has no data, the startup device read did, and the stored file serial differs from the incoming serial, the manager attempts this alternate per-serial file; a missing/failed alternate is logged as possible for a brand-new robot and resolution continues with a false alternate-read result. | filename `0x00695224..0x006952BA`, serial load `0x0069523A`; alternate condition/read `0x006949FA..0x00694A0A`, `0x00694BCE..0x00694BE4`; failed alternate `0x00694C3A..0x00694C90` |
| 13 | `InitAfterReadFromRobotAttempt` resolves the robot/device copies: neither copy uses current defaults and schedules writes to both (`0x00694B06..0x00694BC8`); robot-only selects robot data for a device write; matching copies compare timestamps and select the newer; mismatched stored serials select robot data and clear the old disconnect/app-background timing fields (`0x00694A4E..0x00694AA6`). When the resolved-device-write flag is set it stamps the selected state with `system_clock::now`, calls `WriteToDevice(this,false)` and marks `+0x1C9` (`0x00694DA2..0x00694E4C`); independently the robot-write flag can call `StartWriteToRobot` at the call site `0x00694E50..0x00694EE4` (body `0x00695494..0x00695763`, C2 §Q1). | `0x0069481C..0x00694D06`; timestamp `0x00694960..0x006949B8`, `0x00694BEA..0x00694CE2` |
| 14 | Missing reply versus zero serial: if the robot never supplies tag `0xED`, the registered callback never runs — there is no successful connection response, no `ConnectRobotToNeedsManager`, no per-serial resolution, timer or retry; only the startup fixed-file state remains. If `mfgId` arrives with word 0 equal to zero there is no zero guard: zero is installed and the per-serial filename is `needsState_0.json`. The app-side consequence of omission is exact; whether/when the robot omits the reply is **HARDWARE_ONLY** firmware behaviour. | `0x0052DE04..0x0052DE80`, `0x0052E2F8..0x0052E3B2`; no timer/retry in `0x0052D168..0x0052E3B8`; unguarded store/pass `0x0052E308..0x0052E3B2`; decimal filename `0x00695224..0x006952BA` |

### C2 record change

- **M15-014** goes from live-path `RECOVERABLE_GAP` to `IMPLEMENTATION_GAP` (to build): the
  missing production-path edge, its ordering, the inbound field and the persistence
  lifecycle are recovered above. Its `unresolved` names the firmware-side `mfgId`-omission
  question (HARDWARE_ONLY) and any part of the path the build leaves unbuilt. The record is
  settled only when its whole path is built and verified.
- **M15-016** is unchanged; its `ConnectToRobot -> InitAfterConnection` seam is settled on
  the same terms if this job completes it.

## Appendix G — correction C2 addendum (G-M15, 2026-09-29)

Two build-phase `MISSING:` items were extracted and checked against
`resources/lib/armeabi-v7a/libcozmoEngine.so`. This corrects C2 rows 10 and 13 and adds the
recovered bodies.

### Q1 — `StartWriteToRobot`

C2 row 13's `0x00694E50..0x00694EE4` is the **call site** inside
`InitAfterReadFromRobotAttempt`, not the body. The body is
`NeedsManager::StartWriteToRobot` `0x00695494..0x00695763`.

Call site (`0x00694E50..0x00694EE4`): only when the robot-write result flag is 1
(`0x00694E50 cmp.w r8,#1` / `0x00694E54 bne`); if `+0x1CA` is set it is cleared to 0 with a
"storage version update" log (`0x00694E56`/`0x00694E62`); `+0x1C8` is read only to pick the
log string and is neither cleared nor written (`0x00694E84..0x00694EA8`); it passes a fresh
`system_clock::now()` and tail-calls `StartWriteToRobot` (`0x00694EDE`/`0x00694EE2`/`0x00694EE4`).

Body (`0x00695494..0x00695763`): returns at once if the connected robot `+4` is null
(`0x006954A6..0x006954AA`); if a read is in progress (`+0x3D0 != 0`) it logs "Aborting writing
needs state to robot, because we are reading needs state from robot" and returns without
queueing or deferring (`0x006954AE..0x006954B2`); stores the passed time_point at
`+0x1C0/+0x1C4` (`0x006954F8`). It builds a fresh `NeedsStateOnRobot` on its own stack — **not**
the robot's stored copy and **not** the `+0x1C8`/`+0x1CA` flags — with version **5**
(`0x00695614`/`0x00695616`; the JSON key is `"version"`, `0x00784988`), `timeLastWritten`
(passed / 1e6), `curNeedLevel[10]` each `level*100000.0+0.5` (`0x00695566..0x006955B0`),
`curNeedsUnlockLevel`/`numStarsAwarded` (`+0x54`/`+0x58`), `partIsDamaged[32]` (`+0x48`),
`timeLastStarAwarded` (`+0x60`), `timeCreated` (`+0x10`), `onboardingStageCompleted`
(`+0x1D0`) and `forceNextSong` (`+0x68`). It sizes it with `NeedsStateOnRobot::Size()`
= `0x74` (116 bytes; `0x007848C8`) and serializes with `NeedsStateOnRobot::Pack(uchar*,uint)`
(`0x0078479E`; PLT `0x4BDB0C`) — not `writeAsJson`. It writes NV key `0x194000` (same base as
the read) through `NVStorageComponent::Write` synchronously (`0x006956C6`, `0x006956D4` →
`0x006444FC`); `Write` returning 0 logs `NeedsManager.StartWriteToRobot.WriteFailed` and sets
`_errG` (`0x006956F0..0x006956FE`), and it never calls `Read` first and has no start/finish
flag pair. The write's terminal `NeedsManager::FinishWriteToRobot` (`0x00699CD8..0x00699D39`,
reached from the functor `0x0069BE4A` → veneer `0x008CDAC0` → PLT `0x004BE01C`) only logs
`FinishWriteToRobot.WriteFailed` and sets `_errG` when the result is `< 0`; a result `>= 0`
is a no-op, and it touches no NeedsManager flag and does not retry.

### Q2 — the NV read queue-success contract

C2 row 10's "if queuing fails" is only an invalid tag. `NVStorageComponent::Read`
(`0x00644E14..0x00644EF7`) returns **1** when `IsValidEntryTag(tag)` holds and the request is
`emplace_back`-ed, and **0** only for an invalid tag (`0x00644E2A`/`0x00644E2E`,
`0x00644E82`/`0x00644E86`, invalid path `0x00644E9C..0x00644EEE`). There is no
absent-robot/absent-component check inside `Read`, no queue-capacity check, no in-flight or
duplicate check and no timeout. `StartReadFromRobot` passes the 5th (broadcast) arg 0
(`0x006944C4`), so an invalid tag warns and returns 0; `0x194000` is valid, so on a connected
robot the call returns 1. A C# port with a `void Read(...)` must expose the validity result to
model this branch.

### C2 addendum record change

- **M15-014**'s evidence adds `NeedsManager::StartWriteToRobot 0x00695494..0x00695763` (call
  site `0x00694E50..0x00694EE4`), `NeedsManager::FinishWriteToRobot 0x00699CD8..0x00699D39`
  and `NVStorageComponent::Read 0x00644E14..0x00644EF7`. The robot write and the read's
  queued/invalid-tag result are now buildable.

## Appendix H — correction C3 (G-M15, 2026-09-29)

The robot NV item at key `0x194000` is a **binary** CLAD struct
`Anki::Cozmo::NeedsStateOnRobot` (written by `StartWriteToRobot`, read by
`FinishReadFromRobot`), not the JSON device file that `ReadFromDevice`/`WriteToDevice`
use. The serialized layout was not owned by any record; it is recovered here and gets
record **M15-017**. The version-0 read path is engine-uninitialised and gets forced
policy **M15-018** (SD2).

### The serialized `NeedsStateOnRobot` layout (version 5, `Size()` = `0x74`)

Little-endian; `Pack`/`Unpack` use the same order; `WriteBytes`/`ReadBytes` are raw
`__aeabi_memcpy` with no byte swap (`0x0083C06C`, `0x0083C09A`). Serialized offsets differ
from in-memory offsets by 4 bytes after the version (the in-memory struct pads `+0x04`).

| ser offset | width | field | citations |
|---|---|---|---|
| `0x00` | 4 | `version` (u32; read side dispatches on byte 0) | Pack `0x007847DA`/`0x007847E4`; Unpack `0x007846C4` |
| `0x04` | 8 | `timeLastWritten` (u64 seconds) | Pack `0x007847EA`; Unpack `0x007846D0` |
| `0x0C` | 40 | `curNeedLevel[10]` (i32 each; `round(level*100000.0)`) | Pack `0x007847FC..0x0078480C`; Unpack `0x007846DC..0x007846F4` |
| `0x34` | 4 | `curNeedsUnlockLevel` (i32) | Pack `0x00784816`; Unpack `0x007846FE` |
| `0x38` | 4 | `numStarsAwarded` (i32) | Pack `0x00784824`; Unpack `0x0078470A` |
| `0x3C` | 32 | `partIsDamaged[32]` (u8 each) | Pack `0x00784832..0x00784840`; Unpack `0x00784718..0x0078472E` |
| `0x5C` | 8 | `timeLastStarAwarded` (u64 seconds) | Pack `0x0078484A`; Unpack `0x00784738` |
| `0x64` | 4 | `onboardingStageCompleted` (i32) | Pack `0x0078485C`; Unpack `0x00784744` |
| `0x68` | 4 | `forceNextSong` (`UnlockId` i32 enum, **not** a bool) | Pack `0x0078486A`; Unpack `0x00784750`; `GetJSON` calls `EnumToString(UnlockId)` `0x00784AAA` |
| `0x6C` | 8 | `timeCreated` (u64 seconds) | Pack `0x00784878`; Unpack `0x0078475C` |
| `0x74` | | total | `Size` `0x007848C8 movs r0,#0x74` |

### Versioned layouts and `FinishReadFromRobot` conversion

`FinishReadFromRobot` reads the version byte (`0x00699DD0 ldrb r3,[r1]`), rejects `> 5`
(`0x00699DD2`/`0x00699DD4`) and dispatches: v5 `0x00699E5A` → `NeedsStateOnRobot::Unpack`
(`0x007846B4`); v1-v4 via `tbb` `0x00699E70` to `_v0N::Unpack` (`0x00785ECC`, `0x00785998`,
`0x007853E8`, `0x00784D8C`); v0 falls to `0x00699FFE`.

| version | `_v0N::Size` | serialized layout | conversion |
|---|---|---|---|
| 5 | `0x74` | full table | none |
| 4 | `0x6C` | prefix + `timeLastStarAwarded` + `onboardingStageCompleted` + `forceNextSong` | copy known; zero `timeCreated` |
| 3 | `0x68` | prefix + `timeLastStarAwarded` + `onboardingStageCompleted` | copy known; zero `forceNextSong`/`timeCreated` |
| 2 | `0x64` | prefix + `timeLastStarAwarded` | copy known; zero `onboardingStageCompleted`/`forceNextSong`/`timeCreated` |
| 1 | `0x5C` | prefix only | copy prefix; zero `timeLastStarAwarded`/`onboardingStageCompleted`/`forceNextSong`/`timeCreated` |

"prefix" = `version`(4)@0, `timeLastWritten`(8)@4, `curNeedLevel[10]`(40)@0x0C,
`curNeedsUnlockLevel`(4)@0x34, `numStarsAwarded`(4)@0x38, `partIsDamaged[32]`(32)@0x3C.
For every version other than 5 (including 0), `+0x1CA` is set (`0x00699E64`) and the
converted struct's version is forced to 5 (`0x00699E86`/`0x00699E88`). After the common
tail (`0x0069A052..`) the fields are scaled and stored: `timeLastWritten` × 1e6 to
`this+0x98`, unlock/stars to `this+0xe4/0xe8`, levels /100000 to the map at `this+0xcc`,
damaged parts to `this+0xd8`, `timeLastStarAwarded` × 1e6 to `this+0xf0`, `forceNextSong`
to `this+0xf8`, `timeCreated` × 1e6 to `this+0xa0`, `onboardingStageCompleted` to
`this+0x1d0`, then `NeedsState::UpdateCurNeedsBrackets` (`0x0069A186`).

### Version 0 (forced policy, SD2)

No unpack variant runs. `0x00699FFE..0x0069A01A` logs `"Version %d found on robot but not
supported"`; `0x0069A03E..0x0069A04E` zero `this+0x60..+0x77`; the struct prefix
`this+0x00..+0x5F` is **never written**, so the common tail reads uninitialised stack. It
still sets `+0x1CA` and returns 1 (`0x0069A176`). No shipped writer emits version 0.
**Policy (M15-018):** the stack reads a version-0 blob as all-zero fields, sets the rewrite
flag and returns success — the safest deterministic value, since the engine's bytes are
undefined.

### C3 record changes

- **M15-017** (new, EXACT_SOURCE): the `NeedsStateOnRobot` serialized layout and the v1-4
  conversion. Evidence: `NeedsStateOnRobot::Size 0x007848C8`, `Pack 0x0078479E`,
  `Unpack 0x007846B4`, `_v01..v04::Unpack 0x00785ECC/0x00785998/0x007853E8/0x00784D8C`,
  `FinishReadFromRobot 0x00699E56..0x0069A192`.
- **M15-018** (new, COMPATIBILITY_POLICY, forced SD2): the version-0/unsupported read path.
- **M15-014**'s evidence adds the blob layout citations; the robot copy has no serial field,
  so the resolver's serial comparison uses `+0x1CC` (device/stored file serial) vs `+0x34`
  (incoming robot serial), not a serial inside the robot blob.

## Appendix I — correction C4 (G-M15, 2026-09-29)

The verifier found the resolver's write scheduling, `PossiblyStartWriteToRobot`, and the NV
write terminal unbuilt or wrong. Extracted and checked against the binary. This replaces the
vague part of C2 row 13 and adds the write terminal.

### I1 — `InitAfterReadFromRobotAttempt` (`0x00694608..0x00694F55`; decision `0x0069481C..0x00694D06`) write scheduling

Inputs: robot copy `+0x1C8`, device copy `+0x1C9`, robot rewrite `+0x1CA`, serials `+0x1CC`
vs `+0x34`. Device-write flag `[sp,#0x18]` (tested `0x00694DA2`); robot-write flag `r8`
(tested `0x00694E50`). DW/RW = the two flags.

| # | state | selected/applied | DW | RW | `SendNeedsStateToGame` | citations |
|---|---|---|---|---|---|---|
| 1 | R=0 D=0 | neither; defaults written to both | 1 | 1 | `0x00694B46` arg **0** | `0x00694830`/`0x006949BC`/`0x00694B06..0x00694B46`/`0x00694BC6`/`0x00694BC8` |
| 2 | R=0 D=1 serials equal | device kept; no apply | 0 | 1 | none | `0x006949C0..0x00694A0A` |
| 3 | R=0 D=1 mismatch, alternate **ok** | alternate per-serial file; **no** robot apply | 1 | 1 | `0x006936C6` arg **1** (in `AttemptReadFromDevice`) | `0x006949FA..0x00694A0A`, `0x00694BCE..0x00694BE8`, `0x00694C92..0x00694ACA` |
| 4 | R=0 D=1 mismatch, alternate **fails** | none; sends `RobotChangedFromLastSession` | 0 | 1 | none | `0x00694C3A..0x00694C90`, `0x00694AC2`, `0x00694AC8`, message `0x00694CA6 b 0x00694AA8` |
| 5 | R=1 D=0 | clears `+0x18/+0x1C` then applies the robot copy; sends `RobotChangedFromLastSession` | 1 | `+0x1CA!=0` | `0x00694B00` arg **1** | `0x00694A0C..0x00694AA6` (clear `0x00694A46..0x00694A4C`), apply `0x00694AD0..0x00694B04` |
| 6 | R=1 D=1 mismatch | robot copy applied; clears `+0x18..+0x24` | 1 | `+0x1CA!=0` | `0x00694B00` arg **1** | `0x00694880..0x0069488A`, `0x00694A4E..0x00694A96` |
| 7 | R=1 D=1 equal, robot newer | robot copy applied | 1 | `+0x1CA!=0` | `0x00694B00` arg **1** | `0x00694960..0x0069496E`, `0x00694972..0x006949B8` |
| 8 | R=1 D=1 equal, device newer | device kept; no apply | 0 | **1** (unconditional) | none | `0x00694BEA..0x00694C38`, `0x00694D02..0x00694D06` |
| 9 | R=1 D=1 equal, timestamps identical | neither; no apply | 0 | `+0x1CA!=0` | none | `0x00694CA8..0x00694CE2`, `0x00694D02..0x00694D06` |

DW is written at `0x00694AC2 str r6,[sp,#0x18]` (cases 3,5,6,7) and `0x00694D06 str r0,[sp,#0x18]`
(cases 1,2,8,9). RW is unconditional 1 at `0x00694BC8` (1,2) and via `sb=1` at `0x00694CA2`
(3,4) and `0x00694C34` (8); otherwise `r8 = (+0x1CA!=0)` (`0x00694A9A..0x00694AA0`,
`0x00694CF2..0x00694CF8`). A device write sets `+0x1C9 = 1` (`0x00694E4C`) and clears `+0x1CB`
with a "storage version update" log (`0x00694DB6`); a robot write clears `+0x1CA` likewise
(`0x00694E62`). Both writes use one `system_clock::now()` captured at `0x00694D9C..0x00694DA0`
(`0x00694E38`, `0x00694EDE`). The robot-copy apply is `FUN_00695870(this+8, this+0x98)`
(`0x00694AD0..0x00694B04`) then `ApplyDecayForTimeSinceLastDeviceWrite(false)`. Cases 3,4,5,6,7
send a `RobotChangedFromLastSession` game message (`0x00694AA8..0x00694AC4`; the alternate branch
converges on `0x00694CA6 b 0x00694AA8` for both its success and failure tails); cases 1,2,8,9 do
not. `AttemptReadFromDevice` on success snapshots the device timestamp, applies decay, sends
`SendNeedsStateToGame(1)` and the background DAS event (`0x006936B0..0x006936D2`).

### I2 — `PossiblyStartWriteToRobot` (`0x00696ECC..0x00696F0D`) and its callers

Returns if the connected robot `+4` is null (`0x00696ED4`). Reads `+0x1C0/+0x1C4` (last robot
write), takes `system_clock::now()` (`0x00696ED8..0x00696EE6`), and compares the elapsed time
against `0x23D2883F` = **600,999,999 ns = 600.999999 s** (`0x00696EE2`/`0x00696EEA`); when the
elapsed time is strictly greater it calls `StartWriteToRobot` (`0x00696F04`). If not overdue it
returns unless `force == 1` (`0x00696F00..0x00696F02`). `StartWriteToRobot` writes `+0x1C0/+0x1C4`
(`0x006954F8`). Callers: `NeedsManager::RegisterNeedsActionCompleted` `0x006960D0` with
`force = false` (`0x00696534..0x00696536`); `NeedsManager::UpdateStarsState` `0x00696AAC` with
`force = true` (`0x00696BDE..0x00696BE2`); `NeedsManager::HandleMessage<RegisterOnboardingComplete>`
`0x006984CC` with `force = true` (`0x00698600..0x00698602`). (The earlier C2/G text that named
`RegisterNeedsActionCompletedInternal` at `0x006960D0` is wrong: that address is
`RegisterNeedsActionCompleted`; `RegisterNeedsActionCompletedInternal` is `0x00696778` and is not
a caller.) The stars-state and onboarding callers are unbuilt layers.

### I3 — `NVStorageComponent::HandleNVOpResult` (`0x00642F8C..0x00643937`) op dispatch and the WRITE terminal

Reply fields: tag, index, op byte (`[sp,#0x40]`), result byte (`[sp,#0x41]`), data vector. Op 0
takes the read header/reassembly path (`0x006430A2..0x006430A6`); ops 1-3 take the pending-write
path (`0x0064304A subs r1,r0,#1` / `cmp r1,#3` / `bhs 0x006430A2`; write path `0x00643054`).
For a single-chunk WRITE reply (`Op=1, Result=0, index=0, empty data`, `+0x1C == 0`): result is
non-negative so no resend (`0x00643062..0x0064306A`); clear `+0x48` (`0x0064306E..0x00643074`);
`+0x1C == 0` falls through to the completion (`0x00643078..0x0064307E`); `WriteSuccess` log;
`BroadcastNVStorageOpResult` when `+0x40 != 0` (`0x00643384..0x0064339E`); the write callback
`std::function<void(NVResult)>` at `this+0x28` is invoked with the **reply's result byte**
(`0x006433A2..0x006433F6`; a successful write delivers 0, and a non-negative result delivers its
own value); `RobotDataBackupManager::WriteDataForTag(..., op==1)` (`0x006433FA..0x00643418`);
`SetState(0)` clears `+0x48`, `+0x1C`, `+0x78` (`0x0064341C..0x00643420`,
`0x00642B0C..0x00642B65`). A negative result resends for `-8..-4` **except `-6`**
(`0x00643194 uxtb r1,r1` / `0x00643196 subs r1,r1,#0xf8` / `0x00643198 cmp r1,#4` /
`0x0064319E beq`) while retries remain; otherwise it logs `WriteOpFailed` for **every** negative
result and clears `+0x48`/`+0x1C` before the same completion (`0x00643194..0x006432E6`). The current stack parses every
reply through the read path, so a successful write completes with `-3` and `FinishWriteToRobot`
logs a failure. M3-030/031 cover the read completion and the read retry set only, not this write
terminal; the write terminal is built here as part of M15-014's robot-write path.

### C4 record change

- **M15-014**'s evidence adds the resolver write-scheduling ranges (`0x00694A0C..0x00694EE4`),
  `PossiblyStartWriteToRobot 0x00696ECC..0x00696F0D` with its callers, and the NV write terminal
  `NVStorageComponent::HandleNVOpResult 0x00642F8C..0x00643937` (write path `0x00643054..0x00643424`).
  Its `unresolved` names the unbuilt stars-state/onboarding callers and the device-file host seam.

### I4 — `NeedsManager::ApplyDecayForTimeSinceLastDeviceWrite(bool)` (`0x00695304..0x00695374`)

Both `AttemptReadFromDevice` (`0x006936BA..0x006936BE`) and the resolver's robot-copy apply
(`0x00694AF4..0x00694AF8`) call it with **`false`** (the unconnected decay table). A third
caller, `NeedsManager::HandleMessage<SetGameBeingPaused>` `0x00698F44`
(`0x006990DE..0x006990E8`: `ldr r1,[r4,#4]` / `cmp r1,#0` / `it ne` / `movne r1,#1` /
`blx 0x4BDA1C`), passes `robot != 0`; that game-message caller is unbuilt (M15-016's gap). Body:
`now_us = system_clock::now()` (`0x0069530E`; `system_clock` is microseconds), `stored_us =
*(int64*)(this+8)` (the current `NeedsState` `DateTime`, `0x00695312`), `elapsed = (now_us -
stored_us) / 1,000,000` as a float (`0x0069531C..0x00695334`). Then for each of the three needs
(index 0,1,2): unconditionally `[+0x1E4+4i] = [+0x3AC] - elapsed` (`0x00695338..0x00695350`), and
**only when `[+0x208+4i] != 0`** subtract elapsed from the fullness deadline `[+0x208+4i]` and the
fullness start `[+0x1FC+4i]` (`0x00695344..0x00695368`). Then tail-call
`ApplyDecayAllNeeds(this, connected)` (`0x00695370..0x00695374`), which selects the unconnected
table at `this+0x17C` for `false` (connected `this+0x164` for `true`; `0x00695D16..0x00695D1E`).
The `[+0x1E4] = [+0x3AC] - elapsed` rewind is what makes each need's `ApplyDecayAllNeeds` elapsed
equal this whole gap. The C# port calls `NeedsState.ApplyDecay(...)` directly with the current
connection flag and no rewind, so both the decay table and the fullness window are wrong on
`Load` and the robot-copy apply.

**Flagged for the integrator (M15-001 scope, not fixed here):** the extractor also showed that
`PossiblyWriteToDevice`'s constant `0x03A2C940` is microseconds, i.e. **61 s**, not 61 ms (the
C1 §2 text and the port's `WriteThrottleSec = 0.061` are wrong by 1000×). That is M15-001's
record and batch.

### C4/I4 record change

- **M15-014**'s evidence adds `NeedsManager::ApplyDecayForTimeSinceLastDeviceWrite 0x00695304..0x00695374`,
  `NeedsManager::ApplyDecayAllNeeds 0x00695CFE` (connected/unconnected tables `0x00695D16..0x00695D1E`)
  and `NeedsState::ApplyDecay 0x0069C3C0`.

## Appendix J — correction C5 (R-M15, 2026-09-29)

Source: `re-analysis/research/20260929-R-M15-M15-freeplay-gap1-extraction.md` (gap pass 1) and
`re-analysis/research/20260929-R-M15-M15-freeplay-gap2-extraction.md` (gap pass 2). The manager
spot-checked the instruction stream at every row below. One extractor row was corrected by the
spot-check: `ReadFromDevice` sets its out Boolean only when the version is **below 5** (the v>=5 path
at 0x00699B8E jumps over the store at 0x00699BA0 to 0x00699BA6), which agrees with C2 row 3, not with
gap pass 2's summary line. Citations are Thumb VAs; the Ghidra decompilation was navigation only.

### J1 — `NeedsManager::InitInternal` 0x00693444..0x00693492 (full body)

`InitReset(this, float=r1, param2=-1, param3=0)` (`mov.w r2,#-1` 0x00693446, `movs r3,#0`
0x0069344A, `blx 0x4bd9bc` 0x00693450); `+0x1CB = 0` (0x0069345C); `+0x1C9 = 0` (0x00693462);
`AttemptReadFromDevice(this, "needsState.json", &+0x1CB)` (0x00693466); `+0x1C9 = (byte)result`
(0x0069346C); if result == 0 `SendNeedsStateToGame(this, 0)` (0x00693476); `WriteToDevice(this, true)`
(0x0069347E); `SendNeedsLevelsDasEvent(this, "app_start")` (literal at 0x0069349C, 0x00693486);
tail branch to `LocalNotifications::Generate(this[0x1B0])` (`b.w 0x8cd8ac` 0x00693492 → body
0x0068CA9C). +0x1CA is not touched.

### J2 — `NeedsManager::InitReset` 0x006934A8..0x006935E0

The onboarding-skipped branch is taken when the 4th argument (r3) is 1 **and** `+0x1C8 != 0`
(0x006934B4..0x006934BE); `InitInternal` passes 0, so it always takes the else branch:
`NeedsState::Init(this+8, this+0x128, r2=-1, &shared_ptr<StarRewardsConfig>, RNG)` (0x0069357E). Then
`+0x3B0 = +0x130 + <the float argument>` (0x0069358A..0x0069359E; the addend is the passed float,
which is the same `BaseStationTimer` time as `+0x3AC`; see J14). The per-need loop (0x006935A2..0x006935CC,
stride 4, three iterations) writes `+0x1E4/+0x1E8/+0x1EC = +0x3AC`, `+0x1FC/+0x200/+0x204 = 0`,
`+0x208/+0x20C/+0x210 = 0`, `+0x214/+0x218/+0x21C = +0x3AC`, and the six pause-flag bytes
`+0x1DC..+0x1E1 = 0`. It does **not** write `+0x1F0`. `__aeabi_memclr4(this+0x244, 0x158)` at 0x006935D6.

### J3 — the device file location is host business

The ctor 0x0069210C builds `this+0x3C4 = DataPlatform::pathToResource(DataPlatform, "nurture/")`
(`"nurt"`/`"ure/"` literals at 0x006921A6..0x006921CE, call 0x006921D8). The fixed filename is the
global `"needsState.json"` at 0x0105C2AC, built by `_INIT_48` 0x004D8DDC..0x004D8E26 from `"needsState"`
and `".json"`. `DeviceHasNeedsState` 0x00699870 concatenates `this+0x3C4 + filename` (0x00699878/0x0069987E)
and tests `FileUtils::FileExists` (0x00699884). `ReadFromDevice` uses `DataPlatform::readAsJson`
(0x006998E4) and `WriteToDevice` uses `DataPlatform::writeAsJson(scope, "nurture/needsState.json", json)`
(0x00693EA0..0x00693EE6, PLT 0x004A6514). The only engine constants are `"nurture/"` and
`"needsState.json"`; the directory resolution is the host DataPlatform. The stack's directory/path is
therefore a host seam, not a divergence.

### J4 — `NeedsManager::AttemptReadFromDevice` 0x00693690..0x00693792

If `DeviceHasNeedsState` is false (0x0069369A/0x006936A0) it logs `"FAILED to FIND file %s on device"`
and returns 0 (0x0069378C). If `ReadFromDevice` != 1 (0x006936A8/0x006936AE) it logs
`"FAILED to read file %s on device"` and returns 0. On success: `+0x1B8/+0x1BC = +8/+0xC` (0x006936B6);
`ApplyDecayForTimeSinceLastDeviceWrite(this, false)` (0x006936BE); `SendNeedsStateToGame(this, 1 = Decay)`
(0x006936C6); `+0x30 += 1` (0x006936CA/0x006936CE); `SendTimeSinceBackgroundedDasEvent(this)` (0x006936D2);
logs `"Successfully read file %s from device"`; returns 1 (0x0069371C).

### J5 — `+0x30` (`OpenAppAfterDisconnect`) and `SendTimeSinceBackgroundedDasEvent`

`+0x30` is an **int counter**, the JSON key `OpenAppAfterDisconnect`: loaded by `ReadFromDevice`
0x00699A1C (`asInt`), written by `WriteToDevice` 0x00693CC6 (`Value(int)`), `+1` on a successful
`AttemptReadFromDevice` (0x006936CE) and on the unpause branch of `HandleMessage<SetGameBeingPaused>`
(0x006990EC/0x006990F2), reset to 0 by `OnRobotDisconnected` (0x00695922). It is the `"$data"` of the
event. `NeedsManager::SendTimeSinceBackgroundedDasEvent` 0x0069770C emits `"needs.app_backgrounded_time"`
(string 0x00697844) with data `("$data" 0x00BE44C1, to_string(+0x30))` and elapsed
`to_string((now - +0x20)/1_000_000)`; it emits nothing when `+0x20|+0x24 == 0` (0x0069771E). The stack's
`_openAppAfterDisconnect` is a bool and must become this int counter.

### J6 — `NeedsManager::ReadFromDevice` 0x006998B4..0x00699BB8 contract

Clears the out Boolean first (0x006998C8). `readAsJson` failure returns 0 (0x006998F8 → 0x00699986).
Version `_StateFileVersion` `>= 6` is rejected (`sVerifyFailedReturnFalse`, 0x0069990E/0x00699924,
return 0). Key set and destination (from `_INIT_48` 0x004D8DDC..0x004D9142): `_DateTime` (×1e6 to
+0x8/+0xC), `_SerialNumber` (asUInt to +0x34), `CurNeedsUnlockLevel`/`NumStarsAwarded`/
`NumStarsForNextUnlock` (asInt to +0x54/+0x58/+0x5C), `CurNeedLevel` (object keyed by
`EnumToString(NeedId)`, asInt/100000.0 into the level map), `PartIsDamaged` (object keyed by
`EnumToString(RepairablePartId)`, asBool), `TimeCreated`/`TimeLastStarAwarded`/`TimeLastDisconnect`/
`TimeLastAppBackgrounded` (×1e6), `OpenAppAfterDisconnect` (asInt), `ForceNextSong`
(`UnlockIdFromString`). Version branches: v<3 zeroes +0x18..+0x30 (0x00699A0C..0x00699A1C); v<=1 zeroes
+0x60/+0x64/+0x68/+0x10/+0x14 (0x00699B18..0x00699B9C); v 2-3 zero `ForceNextSong`/`TimeCreated`
(0x00699B44/0x00699B72); v 4 zeroes `TimeCreated` (0x00699B72); v>=5 loads `TimeCreated` (0x00699B74).
Out Boolean is set **only when version < 5** (0x00699B8E jumps over 0x00699BA0 to 0x00699BA6). Sets
the dirty flag +0x90 (0x00699BA8), calls `NeedsState::UpdateCurNeedsBrackets` (0x00699BB2), returns 1.

### J7 — `NeedsManager::WriteToDevice(bool refreshDateTime)` 0x00693BB0

If `refreshDateTime`, `+8/+0xC = system_clock::now()` (0x00693BC4/0x00693BCC). Writes `_StateFileVersion`
= 5, `_DateTime` = +8/+0xC / 1e6, `TimeCreated`, `TimeLastDisconnect`, `TimeLastAppBackgrounded`
(÷1e6), `OpenAppAfterDisconnect` (int), `_SerialNumber`, `CurNeedsUnlockLevel`, `NumStarsAwarded`,
`NumStarsForNextUnlock`, `CurNeedLevel` (object, `round(level*100000)`), `PartIsDamaged` (object,
bool), `TimeLastStarAwarded` (÷1e6), `ForceNextSong` (`EnumToString(UnlockId)`), through
`DataPlatform::writeAsJson`. A write failure logs `sErrorF` and sets `_errG` (0x00693F08..0x00693F18).

### J8 — `LocalNotifications::Generate` 0x0068CA9C

Feature gate `CozmoFeatureGate::IsFeatureEnabled(FeatureType 0xb)` (0x0068CABC; Unity names it
`LocalNotifications`); when disabled it returns with no effect. When enabled it sends
`MessageEngineToGame(ClearNotificationCache)` and one `CacheNotificationToSchedule` per registered item
to the ExternalInterface, and sets `LocalNotifications+0x18 = NeedsManager[0x3AC] + 60.0`
(0x0068CCC4). It writes nothing back to the NeedsManager and does not touch the device.

### J9 — the per-need `+0x1F0` field

Three floats at stride 4 (`+0x1F0/+0x1F4/+0x1F8`): the clock time a need's **per-need pause began**.
Writers: ctor zero (0x00692264), `HandleMessage<SetNeedsPauseStates>` sets it to `+0x3AC` when a need
becomes paused (0x00698A48) and reads it when the pause is unwound (0x00698A18), `SetPaused` adds the
pause duration (0x00695F2C/0x00695F38/0x00695F40). `InitReset` does **not** write it. Its consumer is
the unbuilt `SetNeedsPauseStates` message (M15-001).

### J10 — the per-need `+0x214` field

Three floats at stride 4 (`+0x214/+0x218/+0x21C`): the clock time a need's **bracket last changed**.
`InitReset` seeds it to `+0x3AC` (0x006935BC); `DetectBracketChangeForDas` 0x00695958 reads it
(0x006959D6), computes the DAS elapsed `now - +0x214` (0x006959FA), and writes `+0x214 = now` only
when `force == 0` (0x00695B98/0x00695BA2); `SetPaused` adds the pause duration (0x00695F5E..0x00695F66).

### J11 — `SetPaused` unpause loop 0x00695F02..0x00695F6A

`elapsed = now - +0x1D8` (0x00695F10); `+0x3B0 = now + +0x3B4` (0x00695F18); for each of three needs
(stride 4): `+0x1E4 += elapsed` and `+0x1F0 += elapsed` always (0x00695F3C/0x00695F40), and
`+0x208 += elapsed` + `+0x1FC += elapsed` only when `+0x208 != 0` (0x00695F44..0x00695F58), and
`+0x214 += elapsed` always (0x00695F66). Then `LocalNotifications::SetPaused` and
`SendNeedsPauseStateToGame` (0x00695F72/0x00695F78).

### J12 — `InitAfterConnection` and `+0x1D4`

`+4 = RobotManager::GetFirstRobot()` (0x00694390), `+0x3D0 = 1` (0x00694394), `+0x1D4 = 1`
(0x00694398). `Update` passes `+4 != 0` as the connected flag (0x00695C9C). `+0x1D4` is cleared by
`NeedsManager::Init` (0x00692652) and read by `LocalNotifications::ShouldBeRegistered` 0x0068D00C
(0x0068D01E) to gate notification conditions 1 and 2; its domain name is UNKNOWN.

### J13 — `CozmoEngine::HandleMessage<ConnectToRobot>` 0x004ED018..0x004ED11C

The already-connected branch returns at 0x004ED118 without `InitAfterConnection`. Otherwise:
`AddRobotConnection` (0x004ED074), `AddRobot(engine, 1)` (0x004ED07C), then
`NeedsManager::InitAfterConnection` at 0x004ED10E **unconditionally** (both the AddRobot-failed and
the success paths reach 0x004ED10A), then `DASPauseUploadingToServer(1)` (0x004ED114). The handler
neither sets nor reads a serial.

### J14 — the Init time (M15-001)

`CozmoEngine::Init` passes `BaseStationTimer::GetCurrentTimeInSeconds()` to `NeedsManager::Init`
(0x004EC9DA..0x004ECA0A); `Init`'s float parameter is held in r8 (0x0069257E) and passed unchanged to
`InitInternal` (0x006926CE) and on to `InitReset` (0x006934B0/0x0069358A), so `+0x3B0 = +0x130 + now`
and `+0x3AC` is the same clock (written by `Update` 0x00695CA8). The stack's
`nextDecay = now + DecayPeriodSeconds` is faithful. `PossiblyWriteToDevice` 0x00695DC4 compares
`system_clock::now() - +8/+0xC` against `0x03A2C940`; the clock and the DateTime are in microseconds
(`ApplyDecayForTimeSinceLastDeviceWrite` divides by 1,000,000 at 0x0069532C), so the throttle is
**61 seconds**, not 61 ms (C4/I4's flag; the stack's `WriteThrottleSec = 0.061` is 1000× too small).

### C5 record changes

- **M15-014**'s evidence adds the J1-J8 citations (`InitInternal` extended to 0x00693492, `InitReset`,
  `DeviceHasNeedsState`, `AttemptReadFromDevice`, `SendTimeSinceBackgroundedDasEvent`,
  `ReadFromDevice`, `WriteToDevice`, `LocalNotifications::Generate`, the `+0x30` counter). Its
  `unresolved` names the stars/onboarding callers and the host directory as the remaining work.
- **M15-016**'s evidence adds J9-J13 (`SetPaused` unpause loop, `DetectBracketChangeForDas`,
  `HandleMessage<SetNeedsPauseStates>`, `InitAfterConnection`, `CozmoEngine::HandleMessage<ConnectToRobot>`).
  Its `unresolved` names the `SetPaused` game-message callers and the unbuilt `+0x1F0`/DAS consumers.
- **M15-001**'s evidence adds J14 (the Init time threading and the 61 s throttle).


## Correction A1 (manager audit, 2026-09-29)

The complete audit (`re-analysis/research/20260929-audit-complete.md`) found that some of this subsystem's settled records do not hold. The manager re-checked the central findings in the binary. Those records go back to IMPLEMENTATION_GAP, each with its defect in `unresolved`, to be rebuilt from the cited source. The report's findings are the rows for the rebuild, subject to the rebuilding job's own citation check.

## Correction A3 (manager, 2026-10-02, the Codex re-audit)

Codex's independent re-audit (`re-analysis/research/20260930-reaudit-sonnet-layers.md`) found settled records here that do not hold. The manager re-checked the cited constants in the binary. M15-004, M15-008, M15-009, M15-011, M15-012, M15-013 and M15-015 go back to IMPLEMENTATION_GAP, each with its defect in `unresolved`, to be rebuilt from the cited source. A PARTIAL verdict demotes too: a settled record must own its whole production path.

## Correction A4 (job R-FIX2, 2026-10-03): the floor-placement path, seven new records

M15-008 and M15-009 depended on a path no record owned (the verifier of R-FIX2 stream B named it). A Claude extractor read it (`research/20261003-R-FIX2-M15-gap1-extraction.md`; every address re-read in the `.so`). New records, all IMPLEMENTATION_GAP (to build), rows in that report:
- **M15-019** FindFreePoseInBeacon (0x005E0378..0x005E0666: frame, grid S = size.x + 10.0f 0x41200000, order, the radius, recent-failure (0x005E0B78: -1, PlaceObjectAt = 2, 100.0f 0x42C80000, pi 0x40490FDB) and obstacle tests). The code comments that call the function "0x005E0378..0x005E139F, about 580 instructions" are wrong: it is about 290; the rest of the range is helpers and lambdas.
- **M15-020** CalculateDirectionalityClosest (0x005E07D8), the FindCubesInBeacon predicate and AIBeacon::IsLocWithinBeacon (0x0059C24C).
- **M15-021** TryToPlaceAt and its callback 0x005E188C (three attempts, the same-pose retry, SetFailedToUse(obj, 2, pose) 0x005E1B56).
- **M15-022** PlaceObjectOnGroundAtPoseAction composition (0x005DFF14) and PlaceObjectOnGroundAction Init/CheckIfDone (message 0x44, a TurnTowardsObject verify action, StopAllMotors). M12-015's evidence covers only the wire message.
- **M15-023** the NoFreePoses branch (0x005DF69C..0x005DF73A) and the IsRunnable cooldown (45.0f hiking, 5.0f sparks).
- **M15-024** the whiteboard failure memory (caps {1, 1, 10, 1}, EntryMatches, the two DidFailToUse call sites: the stack-on filter's 0x005E1D24 uses failure 1, 20.0f, pi/8; Pose3d::IsSameAs_WithAmbiguity 0x00846F3C is unread).
- **M15-025** NeedActionCompleted(0x1F) before the pose search and the floor callback's lack of a needs call.
Other facts from the report: the carried id is released only by HandlePickAndPlaceResult 0x00533780 (BLOCK_PLACED and success) through SetCarriedObjectAsUnattached(false); `AIBeacon+0x10` is an f32 timestamp (M13-006 calls it an int); the 0x005C8190 gate belongs to BehaviorPutDownBlock, not BringCubeToBeacon. Still UNKNOWN (RECOVERABLE_GAP): LightCube's BlockInfo size entry (Block::LookupBlockInfo 0x004E4C8C), Pose3d::IsSameAs_WithAmbiguity, TurnTowardsObjectAction's body, the IAction timeout for the compound.
