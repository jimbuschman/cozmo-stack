# M10 — derived robot state and the reactions built on it

Read the engine, not guessed: this document records what libcozmoEngine.so 3.4.0-1204 does to turn the
robot's streamed state into the off-treads classification, the shake and slope tests, the unexpected-movement
detector and the reaction strategies that fire on them, and how `cozmo-stack` reproduces each piece. Every
number below has an address. Provenance classes are the audit's: **NATIVE** (read from the binary), **UNITY**
(decompiled enum), **ASSET** (shipped config), **WIRE** (seen in a capture), **INFERRED**, **LOCAL_POLICY**,
**DEFERRED**.

Status: **complete offline, hardware pending** (`HARDWARE_TEST_PLAN.md` items G–J). The M9 hardware acceptance
was not needed to build this and is still pending too.

## 1. What was added

| system | source of truth | implementation | provenance |
| --- | --- | --- | --- |
| Off-treads classifier | `Robot::CheckAndUpdateTreadsState` 0x00511E00; IMU filters in `Robot::UpdateFullRobotState` 0x0051291C; `OffTreadsState` enum | `src/Cozmo.Robot/OffTreads.cs` (`OffTreadsClassifier`), fed by `CozmoSensors` | NATIVE (UNITY for the enum) |
| Unexpected-movement detector | `MovementComponent::CheckForUnexpectedMovement` 0x0063E398; constants in `MovementComponent::MovementComponent` 0x0063DA5C; `UnexpectedMovementType`/`Side` enums | `src/Cozmo.Robot/UnexpectedMovement.cs` | NATIVE; one gate INFERRED (§5) |
| Reaction trigger strategies | `ReactionTriggerStrategyFactory::CreateReactionTriggerStrategy` 0x0060D5A0 and its lambdas; `StrategyGeneric`, `StrategyRobotShaken`, `StrategyRobotPlacedOnSlope`, `ReactionTriggerStrategyFrustration` | `src/Cozmo.Robot/Behavior/ReactionStrategies.cs` | NATIVE |
| Reaction dispatch in the framework | `BehaviorManager::CheckReactionTriggerStrategies` 0x005A3550, `IReactionTriggerStrategy::ShouldTriggerBehavior` 0x0060B63A | `BehaviorManager.AddReaction / CheckReactions / CurrentReactionTrigger` | NATIVE structure; resume-last mechanics INFERRED (§5) |
| Eight reaction behaviours | `BehaviorReactToRobotOnBack/OnFace/OnSide/PlacedOnSlope/ReturnedToTreads/RobotShaken/UnexpectedMovement/MotorCalibration` | `src/Cozmo.Robot/Behavior/OffTreadsBehaviors.cs` on `SteppedBehavior` | NATIVE transitions and constants |
| Frustration (Minor) | `ReactionTriggerStrategyFrustration`, `BehaviorReactToFrustration`, `reactToFrustrationMinor.json` | `ReactToFrustrationBehavior.Minor`, `FrustrationStrategy` | NATIVE + ASSET |
| Cube-moved path | `ReactionTriggerStrategyCubeMoved` 0x0060B810, `ReactionObjectData` 0x0060BC60, `BehaviorAcknowledgeCubeMoved` 0x0060219C | `src/Cozmo.Robot/Behavior/CubeReactions.cs` behind `ICubeLocator` | NATIVE logic; **input pending** — every step needs the cube's located pose |
| Shipped PlayAnim loader | `config/engine/behaviorSystem/behaviors/**` | `PlayAnimBehavior.LoadShipped` (15 configs) | ASSET |
| Conformance | — | `offtreads` (live or `--replay <frame-log>`), `reactions` (M10 acceptance) | — |

Behaviour inventory after M10 (regenerated, `BEHAVIOR_INVENTORY.md`): **67 of 178 implementable** — 19
"M1–M7 now" (15 PlayAnim configs, PlayArbitraryAnim, ReactToCliff, ReactToPickup, ReactToImpact), 39 Singing
(M9), 9 "M10 derived robot state"; plus ReactToCubeMoved "implemented; waits on cube localization". The
config-free set `ShippedBehaviors.Implementable()` builds 20; `ShippedBehaviors.PlayAnims(obb)` and
`ShippedBehaviors.Singing(obb)` build the rest from the OBB. See §9 for the inventory rules that changed and why.

## 2. The off-treads classifier

`Robot::UpdateFullRobotState` (0x0051291C) runs on every `RobotState`. In order: it stores the state
timestamp (`Robot+0x2c`), the head angle, the lift angle (`Robot+0x300`, radians) and the pose pitch
(`Robot+0x304`, `Radians` from `RobotState+0x1c`); then the IMU filters; then `CheckAndUpdateTreadsState`;
then the status bits (`IS_PICKED_UP` → `Robot+0x349`, `IS_PICKING_OR_PLACING` → the carrying component).

### 2.1 IMU filters (0x005129C0..0x00512A6E) — NATIVE

| field | rule | constants |
| --- | --- | --- |
| `Robot+0x378` | \|accel\| of the raw sample | — |
| `Robot+0x37c` | `0.95·old + 0.05·\|accel\|` | 0x3F733333, 0x3D4CCCD0 |
| `Robot+0x380/384/388` | per axis `0.9·old + 0.1·sample` | 0x3F666666, 0x3DCCCCD0 |

Ported as `OffTreadsClassifier.FilteredAccelMagnitude` / `FilteredAccel`; `CozmoSensors.FilteredAccelMagnitude`
exposes the first, which is the value the shake strategies compare.

### 2.2 The gate — NATIVE

`Robot+0x314` must be non-zero (0x00511E1C). It is written only by `Robot::SetHeadCalibrated` (0x0051519A) and
zeroed in the constructor (0x005100A0): the classifier runs once the head has reported a completed calibration.
`CozmoSensors` mirrors `RobotStateTracker.HeadCalibrated` into it. All three committed fw2457 captures carry the
report, so on this hardware it is enabled within the first second (replay in §8).

### 2.3 Inputs and tests — NATIVE

| quantity | source | test | constant (address) |
| --- | --- | --- | --- |
| `falling` | `RobotState.status & 0x20` (IS_FALLING) | — | 0x00511EC4 |
| `pickedUp` | `status & 0x08` | — | 0x00511F04 |
| `onSideAccel` | filtered accel Y (`Robot+0x384`) | `\| \|y\| − 9800 \| < 3000` | −9800 = 0xC6192000 at 0x00511E78; 3000 = 0x453B8000 at 0x00511EB4 |
| `rightSide` | filtered accel Y | `y > 0` → OnRightSide (4), else OnLeftSide (3) | 0x00511EB0..0x00511ECA, 0x00511F22..0x00511F30 |
| `onFacePitch` | pitch | `pitch > 1.91986` (110°) **or** `pitch < −1.39626` (−80°) | 0x3FF5BE0B at 0x00511E66; 0xBFB2B8C2 at 0x00511EE6 |
| `onBackPitch` | pitch | `\|pitch − centre\| ≤ 0.261799` (15°) | f64 0.261799 at 0x005122B0; centre from the table at 0x005122A0 |
| centre | `Robot+0x14` (physical-robot flag, `Robot::SetPhysicalRobot` 0x00513914) | table[0] = **1.6825 rad (96.4°)** simulated, table[1] = **1.30027 rad (74.5°)** physical | 0x00511E84..0x00511E8C |
| level | pitch | `\|pitch\| ≤ 0.785398` (45°) | 0x3F490FDB at 0x0051222C |

The earlier note in this repository that the on-back centre is 96.4° was the simulator's entry; a physical
robot uses 74.5°. `OffTreadsClassifier.IsPhysical` defaults to true.

### 2.4 Decision order — NATIVE (0x00511F04..0x00512258)

1. **falling** → candidate `Falling`; if the candidate was not already Falling its time is `now − 250`
   (immediate). Then the final test (step 6).
2. else **onSideAccel** → a side candidate already set is kept; otherwise candidate `OnRightSide`/`OnLeftSide`,
   time `now`.
3. else **onFacePitch** → candidate `OnFace`, time `now` if it changed.
4. else **onBackPitch** → candidate `OnBack`, time **`now + 750`** (0x2EE at 0x00511FC4) if it changed, so the
   change lands 1000 ms after the pitch enters the band.
5. else (0x00512228): if **level** and the candidate is OnBack, a side, OnFace or Falling → candidate `InAir`,
   time `now`; else if not picked up → keep the candidate; else if the candidate is not OnTreads → keep it; else
   (picked up from treads) → candidate `InAir`, time `now − 250`. Then the final test.
6. **Final test** (0x00511F48, on paths 1 and 5 only): if none of `pickedUp`, `onSideAccel`, `onBackPitch`,
   `onFacePitch` holds and the candidate is not OnTreads → candidate `OnTreads`, time `now − 250`.
7. **Debounce** (0x00511FD0): the state changes when `candidateTime + 250 ≤ now` (0xFA) and the candidate differs
   from the current state.

Consequences worth knowing: picking up from treads is `InAir` on the same state; putting down is `OnTreads` on
the same state; a robot laid on its back never passes through `InAir` on the way down (step 4 precedes step 5)
but does on the way up if it is still held; `IS_FALLING` without `IS_PICKED_UP` and with a level pose is not
Falling at all.

### 2.5 On a change — NATIVE

Entering Falling stores the state timestamp (`Robot+0x35c`), sends DAS `FallingStarted` and cancels the action
list; leaving it sends DAS `FallingStopped` with the duration; then `RobotOffTreadsStateChanged` is broadcast,
the pose-history / localisation bookkeeping runs (not reproduced), `SetOnChargerPlatform(false)` off treads, and
`FreeplayDataTracker::SetFreeplayPauseFlag(state != OnTreads, 2)`. Ported as `StateChanged`, `FallingStarted`,
`FallingStopped` events; `CozmoSensors.OffTreadsStateChanged` is the broadcast.

### 2.6 Clock — LOCAL_POLICY

The engine debounces against `BaseStationTimer::GetCurrentTimeStamp()` (its own clock, ms). We pass the
robot's state timestamp, which is the same millisecond scale and arrives with every state; the difference is at
most one 30 Hz tick. The conformance tool and tests use it; a caller may pass any clock.

## 3. Reaction trigger strategies

`BehaviorManager::CheckReactionTriggerStrategies` (0x005A3550) walks the 22 triggers each tick. For each, the
strategy's `ShouldTriggerBehavior` (0x0060B63A) either honours a forced trigger (the app's debug
`ExecuteReactionTrigger`, not modelled) or calls the strategy's `ShouldTriggerBehaviorInternal`. On a hit the
engine stops all motors, unlocks all tracks and `SwitchToReactionTrigger`. Reproduced as
`BehaviorManager.CheckReactions`, which stops the interrupted behaviour (releasing its scope) and starts the
reaction; a behaviour holding the reaction lock (`SmartDisableReactionsWithLock`, our `BehaviorScope.DisableReactions`)
suppresses the whole check.

The factory (0x0060D5A0, `tbh` at 0x0060D618) builds, per trigger — all NATIVE:

| trigger | engine mechanism | our source |
| --- | --- | --- |
| CliffDetected | generic + relevant events {CliffEvent 34, RobotStopped 52}, filter lambda 0x0060DC76 (enabled, and not already reacting to a cliff unless the captured predicate says so) | latched on `Sensors.CliffDetected` |
| RobotPickedUp | callback lambda 0x0060DDCE: **`OffTreadsState == InAir`** | `Sensors.OffTreadsState == InAir` |
| RobotOnBack | lambda 0x0060DEB2: state == 2 | same |
| RobotOnFace | lambda 0x0060DF16: state == 5 | same |
| RobotOnSide | lambda 0x0060DF7E: `(state − 3) < 2` | OnLeftSide or OnRightSide |
| ReturnedToTreads | relevant event {RobotOffTreadsStateChanged 53}, filter 0x0060DE36: enabled && `treadsState == OnTreads` | latched on a transition to OnTreads |
| RobotFalling | relevant event {FallingStarted 58} with a **3000 ms** window (0xBB8 at 0x0060D838), filter 0x0060DD6E: enabled | M7's `ReactiveBehavior` already handles the fall → impact chain; the strategy is documented, not registered twice |
| MotorCalibration | relevant event {MotorCalibration 30}, filter 0x0060DCFA: **`calibStarted && autoStarted`** | latched on `Sensors.AutoCalibrationStarted` |
| UnexpectedMovement | relevant event {UnexpectedMovement 60}, no filter | latched on `Sensors.UnexpectedMovementDetected` |
| RobotShaken | `ReactionTriggerStrategyRobotShaken` (0x00612FD4) = (forced ‖ runnable) && `StrategyRobotShaken::WantsToRunInternal` (0x006146CC): **`Robot+0x37c > 16000`** | `RobotShakenStrategy` |
| RobotPlacedOnSlope | `ReactionTriggerStrategyRobotPlacedOnSlope` (0x00612EB0) = wants && runnable; `StrategyRobotPlacedOnSlope::WantsToRunInternal` 0x0061455C | `PlacedOnSlopeStrategy` (§3.2) |
| Frustration | `ReactionTriggerStrategyFrustration` (0x0060EE6E) | `FrustrationStrategy` (§3.3) |
| PlacedOnCharger | `StrategyPlacedOnCharger` (0x00614474): latches `ChargerEvent` (tag 0x39), ignores the first 20 s after construction | M7's charger reaction stays on the status flag; the 20 s grace is recorded here, not applied |
| CubeMoved | `ReactionTriggerStrategyCubeMoved` (§6) | `CubeMovedReactionStrategy` |
| FacePositionUpdated, FistBump, Hiccup, NoPreDockPoses, ObjectPositionUpdated, PetInitialDetection, Sparked | purpose-built strategies over vision, objectives, the app | not built |

The message tags come from the decompiled `MessageEngineToGame.Tag` enum; the factory's tag tables sit at
0xC75AC0..0xC75ACB.

### 3.1 The generic latch — NATIVE

`StrategyGeneric` (the wants-to-run object every generic reaction strategy owns): `ConfigureRelevantEvents`
(0x00613780) subscribes to the tags and stores the filter; `AlwaysHandleInternal` (0x00613998) sets the flag at
`+0x80` when a subscribed message passes the filter; `WantsToRunInternal` (0x00613948) returns
`flag || shouldTriggerCallback(robot)` and **clears the flag**. So an event is consumed by the next evaluation.
`LatchedEventStrategy` reproduces this; the RobotFalling window is the reaction strategy's own check
(`ReactionTriggerStrategyGeneric::ShouldTriggerBehaviorInternal` 0x0060F3EC..0x0060F432 over the per-tag timestamp
map at `+0x4c`).

### 3.2 Placed on a slope — NATIVE

Constructor 0x006144FC: `+0x28` = 10.0 (min pitch, deg), `+0x2C` = 55.0 (max), `+0x30` = 0.01 (gyro quiet,
rad/s), `+0x38` = 0.4 s, `+0x40` = 1.5 s. `WantsToRunInternal`: the largest |gyro component| (the raw gyro copy at
`Robot+0x36c..0x374`) above 0.01 stamps `+0x18`; `IS_PICKED_UP` (`Robot+0x349`) stamps `+0x20`. Wants when
pitch ∈ (10°, 55°) **and** quiet for more than 0.4 s **and** (picked up now **or** put down less than 1.5 s ago)
**and** `OffTreadsState < 2` (OnTreads or InAir).

### 3.3 Frustration — NATIVE + ASSET

`LoadJson` 0x0060ED88 reads `frustrationParams.maxConfidence` (`+0x30`) and `cooldownTime_s` (`+0x34`);
`ShouldTriggerBehaviorInternal` 0x0060EE6E: current reaction ≠ Frustration (4), `MoodManager+0x78` (the Confident
axis) `< maxConfidence`, and either no animation has completed (`+0x38 == 0`) or `now − last > cooldown`.
`AnimationComplete` (0x0060EEF4) stamps `+0x38`. The shipped map: Minor −0.6 / 60 s, Major −0.9 / none.

## 4. The unexpected-movement detector — NATIVE

`MovementComponent::CheckForUnexpectedMovement` (0x0063E398), parameters from the constructor (0x0063DA5C):
`+0xA4` = 0.174533 rad/s (10°/s), `+0xB0` = 20 mm/s, `+0xB4` = 0.2 rad/s, `+0xAC` = 10 (count), wheel base 46 mm
(0x42380000 at 0x0063E434).

Gates: physical robot (`Robot+0x14`); not while the engine's `DirectDrive*` track locks are held
(0x0063E3BA..0x0063E3DE — see §5); not while `IS_PICKING_OR_PLACING` (the carrying component's byte at 0x0063E3E2);
`IS_PICKED_UP | IS_ON_CHARGER | CLIFF_DETECTED` (0x5008 at 0x0063E40A) resets the count and sums.

Per state, with `l`, `r` the wheel speeds and `gz` the gyro z:

* `|l| + |r| < 20` → decay the count by one; return.
* `|gz| < 0.174533`: wheels in opposite directions → count += 1, type `TURNED_BUT_STOPPED`; same direction →
  compare `|0.5·(r − l)/46|` with `|gz|`, difference > 0.2 → count += 1 (same type), else nothing.
* `|gz| ≥ 0.174533`: `sign(r − l) ≠ sign(gz)` → count += **2**, type `TURNED_IN_OPPOSITE_DIRECTION`, sums += 2l,
  2r; same sign → decay by one; return.
* the state timestamp at which the count left zero is kept (`+0x94`), the wheel speeds are summed (`+0x98`,
  `+0x9C`).
* count > 10 → the side from the averages (1e-5 threshold): left forward ≠ right forward → `Right` if the left
  wheel is the forward one, else `Left`; both forward → `Front`; else `Back`. The engine then rewinds its pose to
  the start timestamp and adds a collision obstacle 22.1 / 27.1 / 55.9 mm beyond the robot's half-size on that
  side (0x0063E6C0..0x0063E83A) — **DEFERRED**, no world model here — and broadcasts `UnexpectedMovement`
  (timestamp, type, side). `TURNED_IN_SAME_DIRECTION` is never produced by this function.

Ported as `UnexpectedMovementDetector`; `CozmoSensors.UnexpectedMovementDetected` is the broadcast.

## 5. What is not native, and is labelled

| item | class | why |
| --- | --- | --- |
| Debounce clock = robot state timestamp | LOCAL_POLICY | engine uses its own ms clock; same scale, ≤ one tick apart |
| `Sensors.OffTreadsClassifierEnabled` false → pick-up reaction fires on the raw flag | LOCAL_POLICY | the engine's classifier is off until the head calibrates too; the fallback keeps M7's hardware-verified reaction alive if a robot never reports calibration, and the decision line says so |
| Direct-drive gate on the detector = `UnexpectedMovementDetector.Suspended` | INFERRED | the engine checks its `DirectDrive*` track-lock names; this stack has no such lock set, so the caller that drives the wheels directly sets the flag |
| `CalibrateMotorAction` allowance = 5 s | LOCAL_POLICY | the action's timeout was not read; 5 s is the allowance the engine gives the post-fall recalibration |
| `StartMotorCalibration` (0x58) semantics | native_named, hardware-pending | layout known, honoured-by-robot not yet observed (plan item I) |
| Resume-last after a reaction = restart the interrupted behaviour if still runnable | INFERRED | `shouldResumeLast` is read from the shipped map; `BehaviorManager::SwitchToReactionTrigger`'s bookkeeping was not read |
| Per-play track lock (`tracksToLock = 4` = body, on a push from behind) | DEFERRED | the scheduler has no per-play track mask; the behaviour logs it |
| **DizzyShakeLoop played as repeated restarts** | FIDELITY GAP | the engine starts the loop once with `numLoops = 0` and its streamer loops the clip seamlessly; this scheduler plays a clip once, so `ReactToRobotShakenBehavior.LoopShake` restarts it from the completion callback, closing and reopening the animation at every pass (a frame or two of gap per loop). Looping natively would reopen the frozen M5 timeline; recorded, not done |
| Frustration cooldown clock | corrected 2026-09-19 | the strategy's `AnimationComplete` stamp and its `ShouldTrigger` test are now on one clock (`FrustrationStrategy(…, clockSec)`, handed the same clock as the manager by `ShippedReactionStrategies.ForRobot`); before, the behaviour stamped its own millisecond clock while the manager evaluated its seconds |
| Needs-system variants (severe Energy/Repair animations), hiccup variants, needs actions, objectives, DAS | not modelled | those systems do not exist here; the normal-need animation is played and the rest is written to the trace |
| Cube "location no longer valid" → treat as absent | INFERRED | the engine logs it (0x0060233E); its next transition was not read |
| Reaction codes 0 and 4 of the shaken behaviour named `None`, `NotBackOnTreads` | LOCAL naming | values native (`EReaction`), the string table was not decoded |

## 6. The cube-moved path and why it waits

`ReactionTriggerStrategyCubeMoved::AlwaysHandleInternal` (0x0060BEA4) feeds a `ReactionObjectData` per cube from
`ObjectMoved`, `ObjectStoppedMoving`, `ObjectUpAxisChanged`, `RobotObservedObject`. `ShouldTriggerBehaviorInternal`
(0x0060BC80): for each record, `BlockWorld::GetLocatedObjectByIdHelper` — **not located → erase**; located and
`ObjectOutsideIgnoreArea` (> 50 mm from the robot, 0x0060BDF8) and (`ObjectHasMovedLongEnough` — located, moving,
observed since tracking, moving for > 1000 ms, 0x0060BE48 — or `ObjectUpAxisHasChanged`) and **not**
`IsVisibleFrom(camera, 0.785398, …)` → reset the record, hand the id to `BehaviorAcknowledgeCubeMoved` (`+0x124`),
fire. `ObjectStartedMoving` (0x0060C336) itself marks an unlocated cube as not moving.

`BehaviorAcknowledgeCubeMoved`: runnable while it has a target; `InitInternal` plays `CubeMovedSense` (0x72) in
parallel with a 0.5 s wait, then `TransitionToTurningToLastLocationOfBlock` runs `TurnTowardsPoseAction` to the
cube's pose (max π) in parallel with a 0.5 s wait; `UpdateInternal` watches for a `RobotObservedObject` of the
target → `StopActing`, `AcknowledgeObject` (3), `ReactingToBlockPresence`; the turn ending unseen →
`CubeMovedUpset` (0x73), objective achieved.

All of this is transcribed (`CubeMotionTracker`, `CubeMovedReactionStrategy`, `AcknowledgeCubeMovedBehavior`) behind
`ICubeLocator` — located?, distance, visible?, turn towards. **M11 (2026-09-19) implemented it**: `CubeLocator`
over `BlockWorld` (`VISION.md` §5–§6), and the inventory now files `ReactToCubeMoved` as implementable with M11.
The M10 tests against the fake locator stand; `VisionTests` runs the same strategy against the real one.

## 7. The behaviours, transition by transition

All in `OffTreadsBehaviors.cs`, on `SteppedBehavior` (the engine's `StartActing`/`StopActing`/complete-when-idle
shape). Each class's doc comment carries its addresses; the headlines:

* **ReactToRobotOnBack** — while OnBack: cliff sensor 0 raw `>> 4 ≤ 0x18` (raw < 400) → `FlipDownFromBack`
  (0xCB), otherwise recalibrate the head; wait 0.5 s; repeat. Off back → objective, done.
* **ReactToRobotOnFace** — while OnFace: `FacePlantRoll` (0xA5), wait 0.5 s, still on face → `FailedToRightFromFace`
  (0xA7), repeat. The `FacePlantRollArmUp` (0xA6) branch compares the lift **angle in radians** with 45.0
  (0x00608AF0) and is unreachable on this build; reproduced as is.
* **ReactToRobotOnSide** — `ReactToOnLeftSide`/`RightSide` (0x1A6/0x1A7) → `AskToBeRightedLeft`/`Right` (4/5) →
  `WaitOnSideLoop` (0x22B) for 15 s → `NothingToDoBoredIntro`, `Event`, `Outro` (0x149, 0x147, 0x14A) → loop.
* **ReactToPlacedOnSlope** — ran less than 10 s ago and the pitch was still > 10° after → recalibrate the head;
  else `ReactToPerchedOnBlock` (0x1A8), then record whether the pitch is still > 10°.
* **ReactToReturnedToTreads** — wait 0.5 s; |pitch| > 10° → recalibrate the head. No animation.
* **ReactToRobotShaken** — `DizzyShakeLoop` (0x89, loops) while filtered |accel| ≥ 13000; then `DizzyShakeStop` (0x8A)
  + `DizzyStillPickedUp` (0x8B); back on treads → `DizzyReactionHard` (0x86) if the shake lasted > 5 s, `Medium`
  (0x87) if > 2.5 s, else `Soft` (0x88); still off treads once the sequence ends → no reaction.
* **ReactToUnexpectedMovement** — emotion event `ReactToUnexpectedMovement`; `ReactToUnexpectedMovement` (0x1AC)
  with the body track locked when the push came from behind.
* **ReactToMotorCalibration** — reaction lock; wait up to 5 s for both motors to report calibrated; no animation.
* **ReactToFrustration (Minor)** — `FrustratedByFailure`; emotion event `FinishedMinorFrustration`; strategy's
  `AnimationComplete`. Major adds a random `DriveToPoseAction` (150–400 mm, 80–180°) — requires navigation.

## 8. Offline evidence

* `DerivedStateTests` (40 tests): filters and gate; every classifier branch (pick-up immediate, put-down immediate,
  on back at 1000 ms with the physical centre and not the simulated one, both sides at 250 ms, face at 250 ms,
  falling needs a second condition, righting via InAir only while held); detector (turn that does not turn at
  eleven states, spun against the command at six, resets and decays, straight driving quiet); sensors plumbing;
  every strategy; six behaviours on the shipped assets; manager switching, resume-last, disabled triggers and the
  reaction lock; the cube path with and without a locator; whole-config checks (every registration matches
  `reactionTrigger_behavior_map.json` and its `shouldResumeLast`; all 30 animation triggers the reactions can
  ask for resolve to real clips; the emotion events exist; all 15 PlayAnim configs load and resolve and match
  the inventory).
* `offtreads --replay` over the three committed fw2457 captures (robot on its treads, head moving): classifier
  enabled by the robot's own report, 383 / 722 / 730 states classified, **0 transitions, 0 unexpected
  movements**, pitch 0.2..6.2°, filtered |accel| ≈ 10300–10700 (gravity reads about 10500 in this robot's units;
  the 9800 ± 3000 side band accommodates it). Also `TheCapturedRobotStaysOnTreadsThroughTheWholeLog`.
* Whole suite: see `HANDOFF.md`.

## 9. Corrections to earlier milestones and to the inventory

| where | before | now | evidence |
| --- | --- | --- | --- |
| M7 `ReactiveBehavior`, M8 `ReactBehavior("ReactToPickup")` | RobotPickedUp fired on the raw `IS_PICKED_UP` flag | fires on the derived `InAir` state once the classifier runs; the flag path is a labelled fallback until then | factory lambda 0x0060DDCE |
| M8 `PlayAnimBehavior` comment | "reconstructs PlayAnim and PlayAnimWithFace" | PlayAnim only; `BehaviorPlayAnimSequenceWithFace::InitInternal` (0x005C0648) runs a `TurnTowardsFaceAction` (0x005C0686) first | binary |
| Inventory rule | `ReactToImpact` filed as "robot state not yet derived" | M7 implemented it (`FallingStopped` > 1000); filed implementable | `ReactiveBehavior` |
| Inventory rule | 6 `FeedingReact*` PlayAnim configs filed "requires cubes" on a name mention | PlayAnim configs play their trigger and read nothing else; implementable | the class |
| Inventory rule | 7 `PlayAnimWithFace` configs filed by directory or as implementable in earlier rule 9 | requires vision | 0x005C0686 |
| Inventory rule | `ReactToFrustrationMinor/Major`, `ReactToSparked`, `ReactToCubeMoved` all "robot state not yet derived" | Minor implementable (M10); Major requires navigation; Sparked requires the app's spark request; CubeMoved implemented, waits on localization | §3.3, §6, `BehaviorManager::HandleMessage` 0x005A3C6C |
| `reactToRobotShaken.json` | earlier session note said `disableByDefault: true` | the shipped file says `false` | the file |
| This repository's notes | on-back centre 96.4° | 74.5° on a physical robot; 96.4° is the simulator entry | table 0x005122A0, `Robot::SetPhysicalRobot` |

## 10. Hardware pending

`HARDWARE_TEST_PLAN.md` items G (off-treads transitions live), H (the reactions under the manager), I (the
robot honours `StartMotorCalibration`), J (unexpected movement while driving). None gates M11.

## 11. Next

Done: M11 built the vision pipeline's marker slice and `BlockWorld` (`VISION.md`), which made `ICubeLocator` real
and the cube-moved reaction live. What follows is M12, cube manipulation (`HANDOFF.md`).
