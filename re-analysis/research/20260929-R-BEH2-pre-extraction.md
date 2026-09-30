# R-BEH2 pre-extraction research

Date: 2026-09-29  
Request: `requests/20260929-R-BEH2-pre-extraction.md`  
Source examined: shipped `resources/lib/armeabi-v7a/libcozmoEngine.so`; repository decompilation was used only as a navigation aid, with conclusions checked against the cited Thumb instructions.

## 1. M8-008 — calibration timeout result and M7 consumers

The native action framework does deliver the terminal `ActionResult` into a `RobotCompletedAction`. The behavior base receives that message, but none of the four M7 behavior-side `CalibrateMotorAction` call sites tests the result. Three install an empty completion function. The fourth installs the `StartActing<BehaviorReactToRobotOnBack>` adapter, and that adapter deliberately discards the `RobotCompletedAction` argument before invoking `DelayThenFlipDown(Robot&)`. Thus `0x03000018` has no result-specific M7 branch in these callers.

| step | what original does | citation | record | classification |
|---|---|---|---|---|
| Timeout terminal value | Once the action timer reaches its timeout, the timeout callback list is invoked with `r1 = 0x03000018`; this becomes the action's terminal result. | `libcozmoEngine.so` `0x00540eea..0x00540efe` (`movw/movt r1, #0x03000018`, then indirect callback invocation) | M8-008 | EXACT_SOURCE |
| Runner retains the result | `IActionRunner::Update` calls the action's update virtual, stores its return at runner `+0x18`, and when it is no longer `0x01000000` calls `PrepForCompletion`. | `0x00540592..0x0054062c` (`blx` vtable `+0x10`; `str r0,[this,#0x18]`; compare with `0x01000000`; completion call) | M8-008 | EXACT_SOURCE |
| Completion-message layout at construction | `GetRobotCompletedActionMessage` passes action tag from `+0x60`, action type from `+0x44`, and result from `+0x18` to the message constructor, together with subaction results and the action-specific union. The result is therefore not lost before behavior dispatch. | `0x00540ae6..0x00540afc` (`ldr r1,[r5,#0x60]`; `ldr r3,[r5,#0x18]`; `ldr r2,[r5,#0x44]`; `bl 0x00540bac`; copy at `0x00540af8..0x00540afc`) | M8-008 | EXACT_SOURCE |
| ActionWatcher copy | `ActionWatcher::ActionEnding` independently builds the same leading fields: tag, action type, result, then queues the `RobotCompletedAction`. | `0x00541bd8..0x00541c00` (`ldr` from runner `+0x60`, `+0x44`, `+0x18`; deque `push_back`) | M8-008 | EXACT_SOURCE |
| Behavior receipt | `IBehavior::HandleActionComplete` compares the message's first word (action tag) with behavior `+0x84`; on match it clears `+0x84`, marks acting false through vtable `+0x80`, and, if the behavior is running and has a callback, passes the whole message pointer to the stored callback. It makes no comparison with the result field. | `0x005be1e6..0x005be224` | M8-008 / M7 behavior framework | EXACT_SOURCE |
| Callback adapter semantics | The `StartActing<BehaviorReactToRobotOnBack>`-style function object's invoke entry immediately overwrites incoming `r1` (the `RobotCompletedAction const&`) with its captured behavior pointer, loads that behavior's robot at `+0x2c`, and invokes the captured `function<void(Robot&)>`. The result is discarded unconditionally. | function-object vtable at `0x010267e0`; invoke entry `0x005bf8f4..0x005bf8fc` (`ldr r1,[r0,#8]`; `add r0,#0x10`; `ldr r1,[r1,#0x2c]`; tail-call) | M8-008 / M7 behavior framework | EXACT_SOURCE |
| ReactToPickup caller | When no action is active, the robot is in air and off charger, its retry time has arrived, and the cliff reading does not choose the pickup animation, it constructs `CalibrateMotorAction(robot,true,false)` and calls ordinary `IBehavior::StartActing` with an empty `function<void(RobotCompletedAction const&)>`. There is no result branch. On a later update, with no action active, the same sensor/time decision is evaluated again. | `0x00607ba8..0x00607cc5`; calibration construction/call at `0x00607c8e..0x00607cb2`; action-in-flight gate at `0x00607bb4..0x00607bbe` | M7-019 / M8-008 | EXACT_SOURCE |
| ReactToPlacedOnSlope caller | Its calibration branch constructs `CalibrateMotorAction(robot,true,false)` and calls ordinary `IBehavior::StartActing` with an empty completion function. It then writes zero to behavior `+0x11c` and current time to `+0x120`; no completion/result callback changes these fields. | `0x006080ea..0x00608140` | M7 behavior / M8-008 | EXACT_SOURCE |
| ReactToReturnedToTreads caller | `CheckForHighPitch` starts calibration with the ordinary `IBehavior::StartActing` overload and an empty completion function when pitch is high. No result branch is installed. | `0x00608636..0x00608660` | M7 behavior / M8-008 | EXACT_SOURCE |
| ReactToRobotOnBack caller | `FlipDownIfNeeded` constructs either an animation action or `CalibrateMotorAction(robot,true,false)`, then starts it through the typed `StartActing` overload with `DelayThenFlipDown`. Both action kinds use the same callback path. | selection and construction `0x00608848..0x0060888c`; callback setup/start `0x0060888e..0x006088aa` | M7 behavior / M8-008 | EXACT_SOURCE |
| RobotOnBack completion branch | `DelayThenFlipDown(Robot&)` has only a robot-state branch: if robot `+0x355 != 2`, it reports objective `0x21`; otherwise it starts a wait action which calls `FlipDownIfNeeded` again. Because its adapter discarded the completion message, success, timeout `0x03000018`, cancellation, and other terminal results all take the same robot-state branch. | `0x006089a8..0x006089e9`, together with adapter `0x005bf8f4..0x005bf8fc` | M7 behavior / M8-008 | EXACT_SOURCE |
| Exhaustive M7 calibration caller set | The shipped binary has four M7 behavior call sites to the `CalibrateMotorAction` constructor: ReactToPickup, ReactToPlacedOnSlope, ReactToReturnedToTreads, and ReactToRobotOnBack. No one of them compares `0x03000018` or any other action result. | constructor thunk xrefs/calls at `0x00607c9a`, `0x00608102`, `0x00608644`, `0x00608870`; non-behavior engine message-handler construction at `0x00525bd6` is outside the M7 caller set | M8-008 | EXACT_SOURCE |

### Contradictions / weak evidence

- The open wording that the calibration timeout result must be exposed to an M7 behavior caller is too strong. The original does package and deliver the result-bearing completion message, but the four M7 calibration callers do not inspect it. The fidelity requirement supported here is terminal action completion plus the caller behavior above, not a result-specific M7 reaction.
- The decompiler's inferred types are not evidence. The field identities and argument flow above come from the listed loads, stores, comparisons and indirect calls.

### Open questions

- Whether the managed stack's generic action-completion path performs the same bookkeeping as native `IBehavior::HandleActionComplete` (clear active action before invoking a callback, and invoke only while the behavior is running) is an implementation comparison, not settled by this extraction.
- The exact ReactToPickup retry arithmetic is intentionally deferred to ordered item 4.

## 2. M7-014 — reaction-lock tables and installation

Each table is 21 consecutive `{ReactionTrigger byte, bool byte}` entries in ordinal order. A value of `1` means that `SmartDisableReactionsWithLock` adds this behavior's lock to that trigger; `0` leaves it alone. The trigger ordinals are:

`0 CliffDetected, 1 CubeMoved, 2 FacePositionUpdated, 3 FistBump, 4 Frustration, 5 Hiccup, 6 MotorCalibration, 7 NoPreDockPoses, 8 ObjectPositionUpdated, 9 PlacedOnCharger, 10 PetInitialDetection, 11 RobotFalling, 12 RobotPickedUp, 13 RobotPlacedOnSlope, 14 ReturnedToTreads, 15 RobotOnBack, 16 RobotOnFace, 17 RobotOnSide, 18 RobotShaken, 19 Sparked, 20 UnexpectedMovement`.

| step | what original does | citation | record | classification |
|---|---|---|---|---|
| Common installation semantics | `SmartDisableReactionsWithLock` appends `"_behaviorLock"` to the behavior name at `this+0x40`, calls `BehaviorManager::DisableReactionsWithLock(lock, table, true)`, and remembers the un-suffixed behavior name in its `+0xa4` set. The final `true` means a currently running reaction named by a `1` entry is stopped. | `0x005bce42..0x005bce62`; manager tests `table[2*trigger+1]` at `0x005a2868..0x005a2872` and stops the current matching trigger at `0x005a28e2..0x005a294e` | M7-014 / M10-004 | EXACT_SOURCE |
| ReactToImpact table | Bytes/pairs at `0x00c73d36..0x00c73d5f`: `1,1,1,1,1,1,1,1,1,0,1,1,1,1,1,1,1,1,0,1,1`. It disables every trigger except **PlacedOnCharger (9)** and **RobotShaken (18)**. | data `0x00c73d36..0x00c73d5f`; address loaded and installed by `BehaviorReactToImpact::InitInternal` at `0x00606200..0x00606216` | M7-014 | EXACT_SOURCE |
| DriveOffCharger table | Bytes/pairs at `0x00c672f0..0x00c67319`: `1,1,1,0,0,1,0,0,1,1,0,0,0,0,0,0,0,0,0,0,1`. It disables **CliffDetected, CubeMoved, FacePositionUpdated, Hiccup, ObjectPositionUpdated, PlacedOnCharger, UnexpectedMovement**. | data `0x00c672f0..0x00c67319`; installed first in `BehaviorDriveOffCharger::InitInternal` at `0x005c0b1c..0x005c0b2a` | M7-014 | EXACT_SOURCE |
| Singing table | Bytes/pairs at `0x00c6f590..0x00c6f5b9`: `0,1,1,0,0,0,0,0,1,0,1,0,0,0,0,0,0,0,0,0,1`. It disables **CubeMoved, FacePositionUpdated, ObjectPositionUpdated, PetInitialDetection, UnexpectedMovement**. | data `0x00c6f590..0x00c6f5b9`; installed near the start of `BehaviorSinging::InitInternal` at `0x005eeb56..0x005eeb5e` | M7-014 | EXACT_SOURCE |
| AcknowledgeCubeMoved table | Bytes/pairs at `0x00c72bb2..0x00c72bdb`: `0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0`. It disables only **ObjectPositionUpdated (8)**; notably, it does not disable CubeMoved itself. | data `0x00c72bb2..0x00c72bdb`; installed first in `BehaviorAcknowledgeCubeMoved::InitInternal` at `0x00602236..0x00602242` | M7-014 | EXACT_SOURCE |
| ReactToOnCharger table | Bytes/pairs at `0x00c74182..0x00c741ab`: `0,1,1,1,1,1,0,1,1,0,1,0,0,0,0,0,0,0,0,1,1`. It disables **CubeMoved, FacePositionUpdated, FistBump, Frustration, Hiccup, NoPreDockPoses, ObjectPositionUpdated, PetInitialDetection, Sparked, UnexpectedMovement**. It does not disable its own PlacedOnCharger trigger. | data `0x00c74182..0x00c741ab`; installed first in `BehaviorReactToOnCharger::InitInternal` at `0x00606c9c..0x00606cb0` | M7-014 | EXACT_SOURCE |
| Automatic removal point | `IBehavior::Stop` calls `SmartRemoveDisableReactionsLock` for every remembered name in the `+0xa4` set. That helper reconstructs the same `name + "_behaviorLock"`, asks the manager to remove it, then erases the remembered name. Therefore these five locks last from their `InitInternal` calls until the behavior base stops them; the individual behaviors do not need matching stop code. | stop loop `0x005bd12a..0x005bd140`; removal helper `0x005bd470..0x005bd4aa` | M7-014 | EXACT_SOURCE |

### Contradictions / weak evidence

- Any representation that treats a `1` as an allow-list entry is reversed. `BehaviorManager::DisableReactionsWithLock` acts only when the value byte is nonzero.
- Any implementation that removes the lock only in an individual behavior's `StopInternal` misses the base-class cleanup path.

### Open questions

- None for the five requested tables, their install points, or their lifetime.

## 3. M7-012 / M7-020 — MoodState output and live action-ended callback

### MoodState, field by field

`MoodState` has one CLAD field: a variable-length vector of `float_32` emotion values. For this producer its length is always nine, and its elements are in `EmotionType` ordinal order:

| wire element | MoodManager source | meaning |
|---:|---:|---|
| 0 | `this+0x018` | Happy |
| 1 | `this+0x038` | Calm |
| 2 | `this+0x058` | Brave |
| 3 | `this+0x078` | Confident |
| 4 | `this+0x098` | Charged |
| 5 | `this+0x0b8` | Excited |
| 6 | `this+0x0d8` | Social |
| 7 | `this+0x0f8` | Winning |
| 8 | `this+0x118` | WantToPlay |

The standalone `MoodState` payload is `uint_8 count` followed immediately by `count` raw four-byte IEEE-754 floats. For this call it is therefore 37 bytes: `09` plus 36 float bytes. Inside `MessageEngineToGame`, the union tag is a preceding `uint_16 0x0061`, so the CLAD union body is 39 bytes before any outer transport framing. There are no timestamps, names, per-field tags, or doubles in this message.

| step | what original does | citation | record | classification |
|---|---|---|---|---|
| Output gate | `SendEmotionsToGame` returns without producing a message when the MoodManager robot pointer at `+0x12c` is null. | `0x0067b736..0x0067b73c` | M7-012 | EXACT_SOURCE |
| Nine source values | It reserves nine floats, starts at `this+0x18`, copies the four-byte value there, advances the source by `0x20`, and repeats while index `< 9`. | `0x0067b73e..0x0067b77c` | M7-012 | EXACT_SOURCE |
| Field identities/order | The shipped `EmotionTypeFromString` table maps ordinals 0..8 to Happy, Calm, Brave, Confident, Charged, Excited, Social, Winning, WantToPlay; Count is 9. | `0x007740e0..0x007742d0` (table construction and names) | M7-012 | EXACT_SOURCE |
| Message creation/broadcast | It copies that vector into a `MoodState`, constructs `MessageEngineToGame(MoodState)`, and calls `Robot::Broadcast` with the robot at MoodManager `+0x12c` and the union message. | `0x0067b77e..0x0067b79c` | M7-012 | EXACT_SOURCE |
| Union tag | `Set_MoodState` clears the previous union member, copies the vector to union storage `+8`, and writes tag `0x0061`. The union packer writes its two-byte tag before dispatching tag `0x61` to the MoodState vector packer. | setter `0x00725ffc..0x00726032`; union tag write/dispatch `0x0072928c..0x00729298`, MoodState case call `0x0072974c..0x00729756` | M7-012 | EXACT_SOURCE |
| MoodState length field | The MoodState pack helper computes `(end-begin)/4`, truncates it to one byte, writes that byte, then serializes the vector. | `0x00704f76..0x00704f9e` | M7-012 | EXACT_SOURCE |
| MoodState float fields | The vector helper walks from begin to end in four-byte steps and calls `SafeMessageBuffer::WriteBytes(...,4)` for every element. The inverse first reads the one-byte count, reserves that many floats, then reads exactly four bytes per float. | pack `0x0070fb56..0x0070fb84`; unpack `0x00704fd6..0x00704ffe` and `0x0070fb86..0x0070fc04` | M7-012 | EXACT_SOURCE |

### Live caller of `HandleActionEnded`

| step | what original does | citation | record | classification |
|---|---|---|---|---|
| Registration condition | `MoodManager::Init` registers the action-ended callback whenever MoodManager `+0x12c` (robot) is non-null. The earlier `HasExternalInterface` test gates a different game-message subscription; it does **not** gate this action callback. | robot null check `0x0067aee8..0x0067aeec`; optional external-interface block `0x0067aeee..0x0067af0a`; callback construction/registration `0x0067af0e..0x0067af38` | M7-020 | EXACT_SOURCE |
| What is registered | The inline `std::function<void(RobotCompletedAction const&)>` captures direct member function `MoodManager::HandleActionEnded`, adjustment 0, and this MoodManager pointer. `MoodManager::Init` passes it to the robot's `ActionList` at robot `+0x250`. | `0x0067af12..0x0067af34`; relocations at `0x0103fa40` (bind vtable) and `0x0103fa44` (`MoodManager::HandleActionEnded`) | M7-020 | EXACT_SOURCE |
| Registration handle | `ActionList` copies the function to its `ActionWatcher`. `ActionWatcher` inserts it into the callback tree under the current integer at watcher `+0x30`, returns that old integer in `r0`, then increments `+0x30`. MoodManager stores the returned handle at `+0x158`. | ActionList bridge `0x0053f888..0x0053f898`; watcher insert/return/increment `0x0054179c..0x005417d2`; store `0x0067af34..0x0067af38` | M7-020 | EXACT_SOURCE |
| Production invocation point | Completed messages accumulated by `ActionWatcher::ActionEnding` are drained by `ActionWatcher::Update`, which `ActionList::Update` calls after updating its action queues. For each queued completion it invokes every all-actions callback, then pops the message. | queue creation/result fields `0x00541bd8..0x00541c00`; ActionList call `0x0053f5dc..0x0053f5e4`; callback loop `0x0054187e..0x005418de` | M7-020 | EXACT_SOURCE |
| Registers and argument | At the live callback call, `r0` points at the stored `std::function` (`callback node+0x18`) and `r1` points at the deque's complete 0x40-byte `RobotCompletedAction`. The bind adapter loads captured member function to `r2`, adjustment to `r3`, replaces `r0` with the captured MoodManager (plus any adjustment), leaves `r1` unchanged, and branches to `r2`. Therefore `HandleActionEnded` receives exactly `r0 = MoodManager*`, `r1 = RobotCompletedAction const*`. | call setup `0x00541896..0x005418ae`; bind adapter `0x0067c2ba..0x0067c2ce` | M7-020 | EXACT_SOURCE |
| Fields consumed | `HandleActionEnded` first looks up the message's action tag at `+0` in its completion-disabled set. Otherwise it forms its event-map key from action type `+4` and the high byte of the 32-bit result at `+8` (the `ActionResultCategory`). | `0x0067b320..0x0067b354` | M7-020 | EXACT_SOURCE |
| Unregistration | The MoodManager destructor passes its saved `+0x158` handle to `ActionList::UnregisterCallback`, then clears the handle. | `0x0067ae18..0x0067ae30` | M7-020 | EXACT_SOURCE |

### Contradictions / weak evidence

- The current gap statement that `HandleActionEnded` lacks a live caller is contradicted by the native production path above. The missing seam is in the managed stack, not in the original: the original registers the callback during `MoodManager::Init` and invokes it from `ActionWatcher::Update` for every completed action.
- A MoodState representation as nine named fields would not be source-faithful on the wire. CLAD carries one counted float vector.

### Open questions

- The outer app transport/framing beyond the `MessageEngineToGame` CLAD union was not part of this item; the exact union tag and payload are settled here.

## 4. M7-019 — ReactToCliff streamline fields and ReactToPickup retry timer

The offsets in the request are on the **behavior object**, not the robot. Both are inherited `IBehavior` bytes.

### ReactToCliff `IBehavior+0xD8/+0xD9`

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| `+0xD9` writer and meaning | `IBehavior::ReadFromJson` passes `this+0xD9` as the destination of optional boolean config key `alwaysStreamline`. The constructor's preceding clear of `this+0xC4..+0xD9` makes the absent-key default false. | zeroing `0x005bbd0c..0x005bbd12`; key construction and destination/call `0x005bc200..0x005bc21e` (`r2=this+0xd9`, `GetValueOptional<bool>`) | M7-019 | EXACT_SOURCE |
| `+0xD8` writer | Every `IBehavior::Init` recomputes this byte from the robot's `BehaviorManager`: if manager `+0x58 == 0x55` (no active spark), write 0; otherwise load manager `+0x5C`, XOR it with 1, and write the result to behavior `+0xD8`. | `0x005bccaa..0x005bccc2` | M7-019 | EXACT_SOURCE |
| Source fields behind `+0xD8` | `BehaviorManager` initializes active spark `+0x58` to `0x55` and its mode byte `+0x5C` to 0. `SetRequestedSpark(id, soft)` stores the requested id at `+0x60` and the boolean at `+0x64`; `SwitchToRequestedSpark` copies them to active `+0x58/+0x5C`. The log's shipped strings establish boolean 1 as `"soft"` and 0 as `"hard"`. Consequently `IBehavior+0xD8` is 1 for a non-null **hard** active spark and 0 for a soft spark or no spark. | init stores `0x005a08e0..0x005a08ee`; requested stores `0x005a3eae..0x005a3eb2`; string selection `0x005a3ebc..0x005a3ec8`, data `0x00bf0b78`=`"soft"`, `0x00bf0b7d`=`"hard"`; active copy `0x005a421c..0x005a4226`; derivation `0x005bccaa..0x005bccc2` | M7-019 | EXACT_SOURCE |
| ReactToCliff branch | `TransitionToPlayingCliffReaction` first records state name `"PlayingCliffReaction"`. If both `alwaysStreamline` (`+0xD9`) and the hard-spark runtime flag (`+0xD8`) are false, it emits `robot.cliff_detected`, chooses animation trigger `0x13D` for AI-state value 0, `0x131` for 1, otherwise `0x19D`, and starts that lift-safe animation. If either streamline byte is true, it skips that event and animation and goes directly to `TransitionToBackingUp`. | state helper `0x00605108..0x00605110`; tests `0x00605122..0x0060512c`; event `0x0060513c..0x00605146`; trigger selection/action `0x00605164..0x006051aa`; direct backup `0x006051b8` | M7-019 | EXACT_SOURCE |

### ReactToPickup `+0x11C/+0x120`

Here `+0x11C` is a single-precision absolute retry deadline and `+0x120` is an eight-byte double retry scale.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Initial values | The constructor writes `+0x11C = 0.0f` and `+0x120 = 1.0` as a double. `InitInternal` resets the double scale to 1.0 again, then starts a 0.5-second wait whose callback is `StartAnim`; it does not reset `+0x11C` there. | constructor `0x00607730..0x0060773c`; init reset `0x00607758..0x0060775e`; wait/callback `0x00607764..0x00607780` | M7-019 | EXACT_SOURCE |
| Random interval | At the end of every `StartAnim`, load scale `x = *(double*)(this+0x120)`, call `RandDblInRange(3.0*x, 6.0*x)`, convert that result to float, and store `+0x11C = (float)now + (float)randomInterval`. Thus the first interval is uniformly selected in `[3,6)` seconds under the RNG routine's normal half-open semantics. | scale/lower/upper construction and call `0x00607a8c..0x00607aae`; clock/add/deadline store `0x00607ab6..0x00607adc` | M7-019 | EXACT_SOURCE |
| Scale growth | After scheduling the deadline, add the double literal `0.33000001311302185` (the promoted float value of 0.33f) to the double scale and write it back. Therefore successive ranges after one, two, three starts are `[3,6)`, approximately `[3.99,7.98)`, `[4.98,9.96)`, and so on; there is no cap or reset until the next `InitInternal`. | add/store `0x00607ac2..0x00607ae0`; literal at `0x00607b98` | M7-019 | EXACT_SOURCE |
| Deadline use | While no action is active, the robot is `InAir`, and charger contacts are false, `UpdateInternal` retries only when `deadline < currentTime` (strict comparison). It then reads cliff channel 0: normalized raw value `raw >> 4 <= 24` calls `StartAnim` again; a larger value starts `CalibrateMotorAction(robot, head=true, lift=false)`. The calibration branch does not itself change either timer field. | gates `0x00607bba..0x00607bcc`; time comparison `0x00607c28..0x00607c38`; cliff threshold/branches `0x00607c3e..0x00607c52`, `0x00607c90..0x00607ca8` | M7-019 | EXACT_SOURCE |

### Contradictions / weak evidence

- The current M7-019 unresolved text calls these `robot+0xD9/+0xD8`; that base is contradicted by the instructions. They are `IBehavior+0xD9/+0xD8` and are read through the ReactToCliff `this` pointer.
- The current inventory shorthand ``[0x120] + RandDbl(3x,6x)`` is incorrect. The deadline is `now + RandDblInRange(3x,6x)` in `+0x11C`; only afterward is the scale in `+0x120` incremented by approximately 0.33.
- A previous inventory note describes the manager mode polarity inconsistently. The binary strings and branches settle it: manager mode 1 is `soft`, mode 0 is `hard`, so the XOR makes behavior `+0xD8` true for a hard active spark.

### Open questions

- None for the requested field writers, meanings, ReactToCliff branch, or retry arithmetic. The M14 face-detector and `SayTextAction` dependencies in the other parts of `StartAnim` remain separate cross-layer implementation work already disclosed by M7-019.

## 5. M7-021 — behavior state-name string at `IBehavior+0x58`

`IBehavior+0x58` is a `std::string` used as a diagnostic state label. The behavior's real state is held elsewhere by each derived class (commonly `+0x11C`); this string is not consulted to select behavior.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Construction | `IBehavior` constructs an empty `std::string` at `+0x58`. | `0x005bbc84..0x005bbc92` | M7-021 | EXACT_SOURCE |
| State-name helper input | Helper `0x005C0CA8` reads behavior ID byte `+0x3C` and converts it to text, reads the old string at `+0x58`, and reads its new-string argument. The branches seen here are only libc++ small-string-versus-heap representation checks. | `0x005c0cb8..0x005c0cda` | M7-021 | EXACT_SOURCE |
| Transition log | It calls `sChanneledInfoF` on channel `Behaviors`, event `Behavior.TransitionToState`, format `Behavior:%s, FromState:%s ToState:%s`. | call setup/call `0x005c0cde..0x005c0cec`; shipped strings `0x00bee2c1`, `0x00bf1d24`, `0x00bf1d3f` | M7-021 | EXACT_SOURCE |
| State-name write | After log-buffer cleanup, it assigns the requested string into `this+0x58`. The helper returns immediately afterward; it has no behavior branch, action, message, or state-enum write. | assignment `0x005c0d12..0x005c0d16`; return `0x005c0d1c` | M7-021 | EXACT_SOURCE |
| Other production readers | A scan of direct `IBehavior+0x58` string accesses finds uses only as diagnostic text: `IBehavior::StartActing` includes it in the already-acting warning; FactoryTest includes it in a failed-result warning; DockingTest and LiftLoadTest include it in state-change logs; EnrollFace includes it in timeout/final-state logs. Destruction reads only the string's allocation flag to free heap storage. None of these reads changes a condition, result, action, message, or transition based on the string contents. | StartActing `0x005bdbd0..0x005bdc00`; FactoryTest `0x005d15d8..0x005d1612`; DockingTest `0x005cd87a..0x005cd8a6`; LiftLoadTest `0x005d4c92..0x005d4cbe`; EnrollFace `0x005fe16a..0x005fe198`, `0x005fe5a0..0x005fe5d0`; destructor `0x005bc7c6..0x005bc7d0` | M7-021 | EXACT_SOURCE |

### Contradictions / weak evidence

- No behavior-changing reader was found. Treating the `+0x58` string as the authoritative behavior state would invent a dependency absent from the original; the helper is a logging/diagnostic facility.
- The current M7-021 record is consistent on this point: its missing `+0x58` store and Info log are non-behavioral fidelity work, not a missing state-machine transition.

### Open questions

- None for whether the string affects behavior. The exhaustive direct-access scan supports log-only use; derived behaviors' actual numeric/enum state fields remain governed by their own records.

## 6. M8-014 — AIWhiteboard beacon rendering and game-message handlers

### Registration and dispatch

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Registration gate | `AIWhiteboard::Init` registers all three handlers only when its robot has an external interface; otherwise it logs the existing no-interface warning and installs none. The returned scoped handles are retained in the vector at whiteboard `+0x08`. | gate/calls `0x0056a39c..0x0056a3c4`; warning `0x0056a3d2..0x0056a3f0`; handle insertion paths `0x0056a444..0x0056a4ba`, `0x0056a504..0x0056a57a`, `0x0056a5c4..0x0056a63a` | M8-014 | EXACT_SOURCE |
| Tag 68 dispatch | Registers union tag `0x44` (68), whose functor gets `MessageEngineToGame` member 68 and calls `HandleMessage<RobotObservedObject>`. | tag setup/subscription `0x0056a44c..0x0056a466`; adapter `0x0056cbc2..0x0056cbd6` | M8-014 | EXACT_SOURCE |
| Tag 69 dispatch | Registers union tag `0x45` (69), whose functor gets member 69 and calls `HandleMessage<RobotObservedPossibleObject>`. | tag setup/subscription `0x0056a50c..0x0056a526`; adapter `0x0056cc3a..0x0056cc56` (the call passes through veneer `0x004accac` to `0x0056c48a`) | M8-014 | EXACT_SOURCE |
| Tag 53 dispatch | Registers union tag `0x35` (53), whose functor gets `RobotOffTreadsStateChanged` and handles its first payload byte. | tag setup/subscription `0x0056a5cc..0x0056a5e6`; adapter `0x0056ccdc..0x0056ccf8` | M8-014 | EXACT_SOURCE |

### Handler bodies

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Observed object (68) | Builds a `Pose3d` from the message pose at `message+0x20` using the robot's pose-origin list, takes object type from `message+0x08`, removes every possible-object entry of that same type whose pose can be expressed relative to the observed pose and whose full 3-D squared distance is at most 2500 (50 mm radius), then redraws the possible-object visualization. | handler `0x0056c448..0x0056c476`; matching/type test `0x0056ad76..0x0056ad88`; relative pose/distance/erase `0x0056adb0..0x0056ae12`; redraw `0x0056c464` | M8-014 | EXACT_SOURCE |
| Possible object initial gates (69) | Builds the same pose/type inputs. `ConsiderNewPossibleObject` accepts only poses whose absolute tilt derived from the rotated parent Z axis is less than the literal `0.17453292` rad (10 degrees), that can be expressed relative to the robot pose, and whose robot-relative Z is at most 30.0. A failed relative-pose conversion logs and stops. | handler `0x0056c48a..0x0056c4b2`; tilt `0x0056c4d2..0x0056c51a`; literal construction `0x0056c502..0x0056c50c`; relative-pose gate `0x0056c54a..0x0056c55c`; Z gate `0x0056c560..0x0056c576` | M8-014 | EXACT_SOURCE |
| Possible object dedupe/world gate (69) | For an accepted pose, first removes nearby possible entries of the same type. It then asks `BlockWorld::FindLocatedClosestMatchingTypeHelper` for an already located matching object using per-axis 50.0 thresholds and a pi-radian angular threshold. Only when none is found does it retain the candidate. | remove `0x0056c57a..0x0056c584`; threshold vector/pi `0x0056c588..0x0056c5a4`; BlockWorld call/result test `0x0056c5a8..0x0056c61a`, `0x0056c670..0x0056c674` | M8-014 | EXACT_SOURCE |
| Possible object retention (69) | The possible-object list is capped at ten: if its current size is at least ten it pops the oldest/front entry, appends the new `{pose,type}`, then redraws the possible-object visualization. | size/cap `0x0056c676..0x0056c682`; append/redraw `0x0056c686..0x0056c692` | M8-014 | EXACT_SOURCE |
| Off-treads change (53) | Reads the message's one-byte `OffTreadsState`. If nonzero, returns. If zero (`OnTreads` in the shipped enum), stores `BaseStationTimer::GetCurrentTimeInSeconds()` at whiteboard `+0x48`. | payload/branch `0x0056cce4..0x0056ccec`; clock/store `0x0056ccee..0x0056ccf6`; `unity/scripts/csharp/Anki.Cozmo/OffTreadsState.cs:3` (OnTreads ordinal 0) | M8-014 | EXACT_SOURCE |

### `UpdateBeaconRender`

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Render namespace reset | Lazily constructs the static name `"AIWhiteboard.UpdateBeaconRender"`, then calls `VizManager::EraseSegments` for that name before drawing. | static init `0x0056aa4c..0x0056aa82`; erase `0x0056aa88..0x0056aa9e` | M8-014 | EXACT_SOURCE |
| Beacon iteration and center | Iterates the contiguous 20-byte `AIBeacon` records from whiteboard `+0x60` (begin) to `+0x64` (end). For each, it resolves the beacon pose transform, copies translation XYZ, and adds 35.0 to Z for the circle center. | bounds/stride `0x0056aaa0..0x0056aaa8`, `0x0056abea..0x0056abf0`; pose/translation `0x0056aad2..0x0056ab18`; Z add `0x0056ab1c..0x0056ab28`, literal 35.0 at `0x0056ac7c` | M8-014 | EXACT_SOURCE |
| Color from failure time | `AIBeacon+0x10` is a float last-failure time: AddBeacon initializes it to 0 and `AIBeacon::FailedToFindLocation` overwrites it with current time. Rendering chooses `NamedColors::DARKGREEN` when `abs(+0x10) < 1e-5`, otherwise `NamedColors::ORANGE`. | init `0x0056c3bc..0x0056c3c4`; writer `0x0059c314..0x0059c322`; test/color selection `0x0056aad6..0x0056ab02`; relocated globals from loads `0x0056aab0` (ORANGE) and `0x0056aae2` (DARKGREEN) | M8-014 | EXACT_SOURCE |
| Three circles | Draws three `DrawXYCircleAsSegments<float>` circles with radii `R`, `R-0.5`, and `R-1.0`, where `R` is beacon `+0x0C`. Every call uses the same center/color, `connectLastToFirst=false`, 8 segments, and final float argument 0.0. | first call/arguments `0x0056ab40..0x0056ab50`; second radius/call `0x0056ab7a..0x0056ab94`; third radius/call `0x0056abbe..0x0056abd8`; constants `0x0056aaac`, `0x0056aab2`; callee signature/body `0x0056c9d8..0x0056cabd` | M8-014 | EXACT_SOURCE |

### Contradictions / weak evidence

- The current M13 inventory calls beacon `+0x10` an `int`; its writer and renderer contradict that typing. It is a float timestamp initialized to `0.0f`, written from the float timer, and compared with VFP floating-point instructions.
- `UpdateBeaconRender` is visualization-only: it erases/draws VizManager segments and does not alter beacon selection or behavior. The three message handlers are not visualization-only: tags 68/69 mutate the possible-object list, and tag 53 updates whiteboard time state.

### Open questions

- The semantic name and downstream readers of whiteboard `+0x48` were not requested here. This item settles exactly when tag 53 writes it.

## 7. M8-013 — Selection chooser ExecuteBehavior handling and message layout

The named `0x0060AC2C` address is the final selected-shared-pointer store inside `HandleExecuteBehavior`; the complete handler begins at `0x0060AA58`.

### Wire layout

Both commands are `MessageGameToEngine` union members with a two-byte union tag followed by a five-byte payload:

| union tag | payload byte 0 | payload bytes 1..4 | total union bytes |
|---:|---|---|---:|
| `0x0093` / 147, `ExecuteBehaviorByExecutableType` | `uint8 ExecutableBehaviorType` | signed `int32 numRuns`, little-endian | 7 |
| `0x0094` / 148, `ExecuteBehaviorByID` | `uint8 BehaviorID` | signed `int32 numRuns`, little-endian | 7 |

The C++ objects pad the integer to in-memory offset `+4`, but that padding is **not serialized**. `numRuns=-1` means unlimited in the chooser logic already recorded by M8-013; zero and positive values are preserved verbatim here.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Union tag | `MessageGameToEngine` serializes its tag as `uint16`; generated values are 147 and 148. | `unity/scripts/csharp/Anki.Cozmo.ExternalInterface/MessageGameToEngine.cs:160`, `:161`, `:8250`; native handler halfword tests `0x0060aa64` and `0x0060ab4e` | M8-013 | EXACT_SOURCE |
| Executable-type payload | Pack/unpack writes one byte from object offset 0, then four bytes from object offset 4. The generated managed serializer identifies these as `ExecutableBehaviorType behaviorType` and signed `int numRuns`, payload size 5. | native pack `0x00738c38..0x00738c68`, unpack `0x00738ba8..0x00738bca`; generated file `ExecuteBehaviorByExecutableType.cs:36`, `:73-74`, `:85-86` | M8-013 | EXACT_SOURCE |
| Behavior-ID payload | Same layout, with byte 0 interpreted as `BehaviorID`. | native pack `0x00738d7c..0x00738dac`, unpack `0x00738cec..0x00738d0e`; generated file `ExecuteBehaviorByID.cs:36`, `:73-74`, `:85-86` | M8-013 | EXACT_SOURCE |

### Production handler

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Live subscriptions | The `SelectionBSRunnableChooser` constructor subscribes the same bound `HandleExecuteBehavior` member callback to game-to-engine tags `0x94` and `0x93` whenever the robot has an external interface, and retains both scoped handles. It still resolves its Wait fallback when no interface exists. | interface gate `0x0060a87a..0x0060a884`; ID subscription `0x0060a898..0x0060a8b4`; executable-type subscription `0x0060a91c..0x0060a938`; Wait resolution `0x0060a984..0x0060a99e` | M8-013 | EXACT_SOURCE |
| Executable-type lookup | For tag `0x93`, gets the payload, calls `BehaviorManager::FindBehaviorByExecutableType(payload[0])`, and stores `payload.numRuns` to chooser `+0x3C` whether or not lookup succeeds. A hit logs the chosen behavior and executable type; a miss warns and selects null. | dispatch/get/find/store `0x0060aa64..0x0060aa88`; hit log `0x0060aa98..0x0060aade`; miss `0x0060ab0e..0x0060ab3c` | M8-013 | EXACT_SOURCE |
| ID lookup | For tag `0x94`, calls `BehaviorManager::FindBehaviorByID(payload[0])` and likewise stores `numRuns` at `+0x3C`. A hit logs the ID; a miss warns `Unknown behavior` and selects null. | dispatch/get/find/store `0x0060ab4e..0x0060ab7a`; miss `0x0060ab84..0x0060abb2`; hit `0x0060abc0..0x0060ab02` | M8-013 | EXACT_SOURCE |
| Unknown tag | Any tag other than `0x93/0x94` logs `SelectionBSRunnableChooser.HandleMessage.UnknownTag`, raises the engine's error/debug-break path, and selects null. This should be unreachable through the two constructor subscriptions. | `0x0060ab50..0x0060ab72` | M8-013 | EXACT_SOURCE |
| Analyzer enable handoff | If the selected behavior pointer differs from current chooser `+0x2C`, it calls `SetProcessEnabled(old,false)` and `SetProcessEnabled(new,true)`. That helper changes `AIInformationAnalyzer` enable requests under key `"SelectionBSRunnableChooser"` only when the behavior is non-null and its process field at behavior `+0x1C` is non-null. Re-selecting the same behavior makes no analyzer change. | identity test/handoff `0x0060abdc..0x0060ac24`; helper `0x0060ae84..0x0060aeea` | M8-013 | EXACT_SOURCE |
| Selected behavior store | Replaces the shared pointer at chooser `+0x2C/+0x30` with the lookup result and performs the required shared-count releases. This is the `0x0060AC2C` site named in the request. The already-recorded `GetDesiredActiveBehavior` then consumes this pointer and `+0x3C` run budget. | `0x0060ac2c..0x0060ac44`; consumer `0x0060ad64..0x0060ae6e` | M8-013 | EXACT_SOURCE |

### Contradictions / weak evidence

- M8-013's unresolved text says this handler/message have no production caller. That is backwards for the native original: the chooser constructor installs the two live external-interface subscriptions. The missing production caller is only in the managed stack.
- The record cites `0x0060AC2C` as though it were the handler entry. It is the final store; the handler is `0x0060AA58..0x0060AC45`.
- Serializing the C++ padding between selector and `numRuns` would produce an incorrect eight-byte payload. CLAD writes exactly five payload bytes.

### Open questions

- None for the two command layouts or their chooser mutation. The broader question in M8-004—whether the default managed BehaviorManager can run entirely on the chooser path—remains outside this item.

## 8. M7-018 — config factory, FistBump, Hiccup and ReactToSparked

### Config-to-object production path

The shipped configurations relevant to this item are not three independent native classes. `FistBump` and `SparksFistBump` both select class ordinal `0x19` (`FistBump`); `Hiccup` selects ordinal `0x26` (`PlayAnim`) with animation trigger `Hiccup`; and `ReactToSparked` selects ordinal `0x4C` (`ReactToSparked`). The timed hiccup/cure logic is in `ReactionTriggerStrategyHiccup`, which mutates the generic `BehaviorPlayAnimSequence` trigger vector before asking whether that behavior can run.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Shipped declarations | The ordinary fist-bump config declares `FistBump`, 3.0 s face search, abort-on-no-face true, and update-last-completion true. The sparkable variant declares the same class, 5.0 s, abort false, and omits update-last-completion (constructor default false). Hiccup declares `PlayAnim` and `animTriggers:["Hiccup"]`. ReactToSparked declares its like-named class. | shipped OBB `config/engine/behaviorSystem/behaviors/freeplay/userInteractive/fistBump.json:1-16`; `freeplay/sparkable/sparksFistBump.json:1-8`; `reactions/hiccup.json:1-7`; `reactions/reactToSparked.json:1-4` | M7-018 | EXACT_SOURCE |
| Class extraction | `ExtractBehaviorClassFromConfig` indexes JSON key `behaviorClass`, accepts it only when it is a string (otherwise uses the empty string), constructs a native string, and calls `BehaviorClassFromString`. | `libcozmoEngine.so` `0x005bba74..0x005bbab4` | M7-018 | EXACT_SOURCE |
| Container load | `BehaviorContainer` walks every ID/config entry. A nonempty config is passed through class extraction and `CreateBehavior`; an empty one warns and is skipped, and a null create result is an error. After a nonempty source map is processed it verifies executable behaviors. | `0x0059c34c..0x0059c428` | M7-018 | EXACT_SOURCE |
| Class dispatch | `CreateBehavior` rejects ordinals above `0x4E`, then uses a 79-way table over ordinals 0..78. Every case allocates the concrete class, passes the same robot and complete JSON config to its constructor, and wraps it in a shared pointer. Unknown/unconstructable classes take the error/null path. | range/table `0x0059c89a..0x0059c8a4`; common success and failure `0x0059d350..0x0059d3fe` | M7-018 | EXACT_SOURCE |
| Three relevant factory cases | Case `0x19` allocates 0x158 bytes and calls `BehaviorFistBump`; case `0x26` allocates 0x140 and calls `BehaviorPlayAnimSequence(...,true)`; case `0x4C` allocates 0x120 and calls `BehaviorReactToSparked`. | `0x0059cc84..0x0059cca2`; `0x0059ce24..0x0059ce44`; `0x0059d2f2..0x0059d310` | M7-018 | EXACT_SOURCE |
| Object identity and registration | The `IBehavior` constructor independently extracts and stores the config's `BehaviorID` at `+0x3C` and class byte at `+0x64`, then parses common fields. `AddToFactory` reads the ID from object `+0x3C`, performs a unique map insertion keyed by that ID, and returns the pointer. | ID/class construction `0x005bbc3e..0x005bbcc0`; common JSON parse call `0x005bbd4c`; registration `0x0059e916..0x0059e99a` | M7-018 | EXACT_SOURCE |

### FistBump state machine

The state below is the 32-bit value at `BehaviorFistBump+0x11C`. Animation names are the generated enum names for the immediate trigger values in the native instructions.

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Constructor/config | Initializes counters/timestamps/snapshots to zero, `abortIfNoFaceFound` to true and `updateLastCompletionTime` to false, then reads optional `maxTimeToLookForFace_s` into `+0x12C`, abort into `+0x130`, and update-last-completion into `+0x148`. | `0x005f1d98..0x005f1e3a`; keys at `0x005f1dd0`, `0x005f1dfc`, `0x005f1e28` | M7-018 | EXACT_SOURCE |
| Init and entry state | Installs the eight-trigger reaction lock table, pushes idle trigger `0x23F`, clears search/retry fields and the off-treads timestamp, and sets state 1 when carried-object ID `[[robot+0x284]+8]` is `-1`, otherwise state 0. IsRunnable always returns true. | `0x005f1ed4..0x005f1f1e`; lock table contents already recorded at `0x00c6fdc8` | M7-018 | EXACT_SOURCE |
| Persistent off-treads exit | While `OffTreadsState != OnTreads`, it stamps `+0x144` on the first tick; after strictly more than 1.0 s it returns terminal update value 2. Returning OnTreads clears that stamp. | `0x005f1f4a..0x005f1f7a`, terminal branch `0x005f1f74 -> 0x005f22a0` | M7-018 | EXACT_SOURCE |
| State 0 — put down carried object | Starts `PlaceObjectOnGroundAction`, then writes state 1. | `0x005f1fb0..0x005f1fca`, state store `0x005f2364..0x005f2366` | M7-018 | EXACT_SOURCE |
| State 1 — turn to last face | Starts `TurnTowardsFaceAction(robot, faceID=0, maxTurn=pi, ...)`, replaces it with the last-face-pose vtable, and sets its fail-if-no-face byte `+0x193` to 1. Its completion callback tests the result: `NO_FACE` (`0x0300000E`) stamps search-start `+0x120` and enters state 2; every other result enters state 3. | construction `0x005f2070..0x005f20ca`; callback `0x005f27ee..0x005f2814` | M7-018 | EXACT_SOURCE |
| State 2 — bounded face search | Searches until `now > +0x120 + maxTime`. A nonzero `GetLastObservedFace(...,true)` result that also satisfies unsigned `(robot+0x2C - result) >> 3 <= 124` starts the same pi-limited last-face turn and enters state 3. Otherwise, when `now > +0x124`, it starts `PanAndTiltAction` using successive pan angles `-0.2617994` and `+0.5235988` rad and tilt `0.6108652` rad, schedules the next scan at `now + RandDblInRange(1.0,2.0)`, and advances the two-entry index without wrapping past its last entry. | timeout `0x005f1fe2..0x005f2006`; face test/turn `0x005f22b4..0x005f2340`; scan `0x005f2382..0x005f24ee`; angle source initialization `0x004d7d10..0x004d7d4e`, data `0x00c6fdc0..0x00c6fdc7` | M7-018 | EXACT_SOURCE |
| Search timeout branch | On timeout, enters state 9 when abort-on-no-face is true; when false it enters state 3 and proceeds without a face. | `0x005f1fe2..0x005f2006` | M7-018 | EXACT_SOURCE |
| State 3 — request | Starts `TriggerAnimationAction(0xC7 FistBumpRequestOnce, 1, 1, 0, 60.0, 0)` and enters state 4. | `0x005f20e2..0x005f2112`; state store `0x005f235a..0x005f2374` | M7-018 | EXACT_SOURCE |
| State 4 — idle/listen pose | Stamps `+0x134=now`, disables lift and head power, starts `TriggerAnimationAction(0xC6 FistBumpIdle, 1, 1, 0, 60.0, 0)`, and enters state 5. | `0x005f200c..0x005f2054`; state store `0x005f2372..0x005f2374` | M7-018 | EXACT_SOURCE |
| State 5 — motor settle | Waits until MovementComponent bytes `+0x0B` and `+0x0A` are both zero, then snapshots robot lift angle `+0x300` into `+0x140` and raw accel-X `+0x360` into `+0x13C`, enters state 6, and warns when settling took more than 0.5 s. | `0x005f212a..0x005f2168` (warning path continues to `0x005f21ac`) | M7-018 | EXACT_SOURCE |
| State 6 — detect bump | A bump is any one of: absolute lift-angle delta greater than `0.008726646` rad (0.5 degrees), absolute gyro-Y `robot+0x370` greater than `0.17453292` rad/s (10 degrees/s), or absolute accel-X delta greater than `4000.0` mm/s². | inline test `0x005f21b2..0x005f2204`; identical helper `CheckForBump` `0x005f2644..0x005f26a8` | M7-018 | EXACT_SOURCE |
| State 6 success | On a bump it stops the idle action, re-enables lift/head power, starts `0xC9 FistBumpSuccess`, and enters state 7. | `0x005f2208..0x005f2258`; state store `0x005f23f6..0x005f23f8` | M7-018 | EXACT_SOURCE |
| State 6 retry/failure | If the current idle action has ended without a bump, it re-enables power and increments `+0x138`. On the first such end it starts `0xC8 FistBumpRequestRetry` and returns to state 4; on the second it starts `0xCA FistBumpLeftHanging` and enters state 8. | `0x005f23fc..0x005f24a2` | M7-018 | EXACT_SOURCE |
| Terminal states 7/8/9 | After the success action, state 7 calls `NeedActionCompleted(NoAction=0)` and reports objectives 8 and 7. After left-hanging, state 8 reports objectives 9 and 7. Abort state 9 reports objective 7 only. All three notify every registered fist-bump listener through `ResetTrigger(updateLastCompletionTime)` and return terminal update value 2. | `0x005f206c..0x005f206e`; `0x005f2270..0x005f22a2`; listener walk `0x005f26b8..0x005f26f6` | M7-018 | EXACT_SOURCE |
| Stop cleanup | Always re-enables lift and head power and calls `ResetTrigger(false)`. | `0x005f26f8..0x005f2714` | M7-018 | EXACT_SOURCE |

### Hiccup: generic animation behavior plus trigger strategy

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| PlayAnim half | `BehaviorPlayAnimSequence` reads every configured `animTriggers` string, converts it to an enum and retains all values except `Count`; it reads `num_loops` with default 1. With one trigger, Init starts one lift-safe animation action directly. Thus the shipped Hiccup config by itself plays `0xE1 Hiccup` once. | constructor `0x005bff24..0x005c004c`; runnable/init `0x005c013a..0x005c0156`; single-trigger path `0x005c0158..0x005c01bc`; shipped Hiccup config cited above | M7-018 / M8-005 | EXACT_SOURCE |
| Strategy config/constants | The reaction-map `hiccupParams` are read into occurrence min/max and converted seconds→milliseconds, hiccup count range, spacing millisecond range, cured delay converted seconds→milliseconds, and unlock ID. Shipped values are 300..3300 s, 5..10 hiccups, 4500..8000 ms spacing, 600 s cure delay, unlock `DroneModeGame`. | parser `0x0060fecc..0x006100c2`; shipped OBB `config/engine/behaviorSystem/reactionTrigger_behavior_map.json:39-64` | M7-018 / M10-003 | EXACT_SOURCE |
| Reset | Chooses total/remaining count uniformly from the configured integer range, chooses the next occurrence and spacing deadline as `nowMs + RandIntInRange(occurrence)`, clears first-hiccup timestamp, broadcasts `RobotHiccupsChanged(false)` if previously active, and clears byte `+0x74` of the component reached through `[[robot+0x264]+0x18]`. | `0x006101a0..0x0061022c` | M7-018 / M10-003 | EXACT_SOURCE |
| Normal trigger gates | `ShouldTriggerBehaviorInternal` first downcasts the mapped behavior to `BehaviorPlayAnimSequence`. Feature gate 6 disabled returns false; a locked unlock ID resets and returns false. It consumes/clears forced byte `+0x3C`. For states 0/1 it requires `now > nextOccurrence`; component field `[[robot+0x264]+0x30]+0x14 <= 1` cures unsuccessfully; otherwise it sets the whiteboard byte, requires `now > spacingDeadline`, schedules a new 4500..8000 ms deadline, and suppresses this attempt when the consumed force byte was set. | `0x00610694..0x006107c0` | M7-018 / M10-003 | EXACT_SOURCE for control flow; component semantic names remain UNKNOWN |
| Individual hiccup selection | On an eligible non-forced attempt it post-decrements remaining. A zero pre-decrement count cures unsuccessfully. Otherwise the first active hiccup broadcasts `RobotHiccupsChanged(true)`. It replaces the PlayAnim trigger vector with `0xE2 HiccupGetIn` when remaining now equals total minus one (the first), else `0xE1 Hiccup`; if runnable, it stamps first-hiccup time once, calls `NeedActionCompleted(0x1A IndividualHiccup)`, and returns true. | count/notification `0x006107c2..0x00610802`; trigger choice `0x0061062c..0x00610664`; vector/runnable/result `0x00610802..0x0061085c` | M7-018 / M10-003 | EXACT_SOURCE |
| Cure states | `CureHiccups(false)` resets, sets state 3 and reports `0x18 HiccupsEndBad`; `CureHiccups(true)` resets, sets state 2, adds 600000 ms to the new occurrence deadline and reports `0x19 HiccupsEndGood`. On the next strategy check, state 2 installs `0xE3 HiccupPlayerCure`, state 3 installs `0xE7 HiccupSelfCure`; if runnable, state returns to 0 and the animation stream keep-face-alive timeout is reset. | cure `0x0061098c..0x006109c4`; state animation selection/run `0x00610728..0x0061077c`; data `0x00c76818=0xE3`, `0x00c7681c=0xE7` | M7-018 / M10-003 | EXACT_SOURCE |
| Off-treads cure transition | Only while trigger 5 is enabled: an OnFace (5) or OnBack (2) event after the occurrence deadline, in state 0, writes 5.0f to `robot+0x220` and enters state 1. A later OnTreads (0) event after the deadline in state 1 calls successful/player cure. | `0x006109cc..0x00610a3e` | M7-018 / M10-003 | EXACT_SOURCE; semantic identity of `robot+0x220` remains UNKNOWN here |
| Overfeeding and disable hooks | Enabled tag `0xA4 NotifyOverfeedingShouldTriggerHiccups` sets both deadlines to now; if the Hiccup trigger is disabled, receipt instead errors. `EnabledStateChanged(false)` sets forced byte `+0x3C`; if overdue and either queried need bracket (IDs 1 or 0) is 3, it calls unsuccessful/self cure. | game handler `0x00610a44..0x00610abc`; enable hook `0x00610af8..0x00610b40` | M7-018 / M10-003 | EXACT_SOURCE for control flow; need/component identities beyond the generated IDs are outside this item |

### ReactToSparked state machine

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| Construction/runnable | It adds no data fields beyond `IBehavior`; its constructor only installs the derived vtable, and `IsRunnableInternal` always returns true. | `0x006097e0..0x006097f0`; `0x00609864..0x00609866` | M7-018 | EXACT_SOURCE |
| Init | Reads current time in seconds, constructs the exact 12-byte event name `SparkPending`, and calls `MoodManager::TriggerEmotionEvent(robot+0x440, name, now)`. | `0x006097f8..0x00609838` | M7-018 | EXACT_SOURCE |
| Completion | It has no derived UpdateInternal or StopInternal. With no action started by Init, inherited `IBehavior::UpdateInternal` returns terminal value 2 immediately (`+0x84` is zero); if an action were active it would return 1. | base body `0x005bda56..0x005bda62`; ReactToSparked entry-point table in current M7 inventory (`Init 0x6097f8`, no Update/Stop) | M7-018 | EXACT_SOURCE |

### Contradictions / weak evidence

- The current M7-018 title/status are `Shipped behaviour configuration and BehaviorClass factory binding` / `IMPLEMENTATION_GAP`. Its unresolved wording says the missing class implementations include **Hiccup**. The shipped source contradicts that as a class claim: there is no separate `BehaviorHiccup` factory case. ID `Hiccup` is a normal `BehaviorPlayAnimSequence`; its stateful schedule/cure behavior belongs to `ReactionTriggerStrategyHiccup`.
- The current FistBump Init row says `+0x11C = 0`. The instructions instead make the initial state conditional: state 1 when carried-object ID is `-1`, state 0 otherwise (`0x005f1f08..0x005f1f18`).
- The existing FistBump inventory row is too compressed for implementation: it omits the `NO_FACE`-only callback branch, exact search angles/timers, bump thresholds, retry count, animation names, and distinct objective/reset outcomes. Those are settled above.
- The existing ReactToSparked rows correctly record Init and always-runnable, but omit that inherited update terminates immediately after the mood event because Init starts no action.
- Generated enum names establish readable names for numeric constants but do not replace the native evidence; the native immediate values and branches are cited in each row.

### Open questions

- The semantic class names of the Hiccup strategy's component paths through `robot+0x264`, and the precise named meaning of `robot+0x220`, remain UNKNOWN in this item. Their offsets, comparisons, writes and resulting state transitions are exact and sufficient to reproduce the control flow without inventing those names.
- This extraction settles only the requested three M7 behaviors and the config-driven construction mechanism. M7-018's other missing M12/M13/M14/M15-owned class implementations remain outside this request.
