# M7 — Reactive behaviour and idle personality

Status: **COMPLETE — HARDWARE VERIFIED — FROZEN as of 2026-09-19.**

A correctness pass was run over this layer before acceptance; what it changed is at the end under
"Hardening pass". Acceptance took three hardware rounds, because the first failure had two independent
causes — one here, one in M5's face renderer. Both are fixed and re-verified; see `ACCEPTANCE.md`.

Everything below is established offline and covered by tests, and the behaviour of the idle and reactive
layers has now been confirmed on the firmware-2457 robot.

Built on frozen M1–M6. None of them was reopened.

## 1. The trigger system

Anki's own table, not an invented personality map.

```
state/event -> AnimationTrigger -> AnimationTriggerMap.json -> group -> candidates -> selected clip
```

* **`AnimationTrigger`** is generated from the decompiled `unity/scripts/csharp/Anki.Cozmo/AnimationTrigger.cs`
  — 575 values, declaration order being the ordinal because no explicit values are assigned.
* **`AnimationTriggerMap.json`** from the OBB maps 573 of them to an animation group.
* **Selection** is M5's existing `AnimationGroup.Choose`, so this layer adds no policy.

Measured against the shipped assets with `triggers <obb>`:

```
triggers defined            575
named a group by the map    573  (99.7%)
selected an animation       573  (100.0% of mapped)
and that clip is present    573  (100.0% of selected)
```

Exactly two triggers name no group: `ProceduralLive` and `ReactToMotorCalibration`. The generator
cross-checks on every run that every `CladEvent` in the map is a trigger the enum defines.

## 2. The reactive dispatcher

Four reactions, because four is what this stack can both **detect** and **play**:

| ReactionTrigger | detected by | animation |
| --- | --- | --- |
| `CliffDetected` | `Sensors.CliffDetected` | `ReactToCliff` |
| `RobotPickedUp` | `Sensors.PickedUpChanged` | `ReactToPickup` |
| `PlacedOnCharger` | `Sensors.OnChargerChanged` | `PlacedOnCharger` |
| `RobotFalling` | `RobotStatusFlag.IsFalling` | `ReactToFalling` |

`ReactionTrigger` is generated from Anki's decompiled enum (21 values).

### The uncertainty in the middle, stated plainly

The chain is `robot state -> ReactionTrigger -> AnimationTrigger -> group -> clip`. The first and last
links are established. **The middle one is not.**

In the shipped engine each reaction is a native `BehaviorReactToX` class that holds its animation trigger
in code. Those classes are **not exported**: the dynamic symbol table contains only their `shared_ptr`
deleters, so the deciding constant is not reachable without locating each vtable and disassembling through
it. Their JSON configs under `config/engine/behaviorSystem/behaviors/reactions/` carry only
`behaviorClass` and `behaviorID` — no animation. Config-driven behaviours elsewhere *do* name theirs in an
`animTriggers` field (33 files do), but no reaction behaviour does.

So each entry is tagged `ReactionEvidence.NameCorrespondence` and carries the three Anki artifacts that
agree on it. That is strong evidence and it is still an assumption. `ReactionTable.Entries` can be replaced
wholesale once the native path is read.

### What is deliberately not mapped

Cube reactions (need cubes), face and pet reactions (need vision), orientation reactions — on back, on
face, on side, on slope, returned to treads, shaken, unexpected movement (need IMU classification M4
reports raw but does not do), frustration/sparked/hiccup (need the mood and spark systems), and
`MotorCalibration` (whose animation trigger exists but which the shipped map gives no group, so there is
nothing to play).

Put down and off-charger are detected and reported but play nothing: the shipped `ReactionTrigger` set has
no member for either. Backpack button message `0xDB` parses, but has no shipped reaction or animation, so
none was invented.

## 3. Idle and keep-alive

A reconstruction of `AnimationStreamer::UpdateLiveAnimation`, using the engine's own numbers.

All 30 tunables were recovered by disassembling `AnimationStreamer::SetDefaultParams()` at **0x0057DB40**,
a flat run of `SetParam(index, float)` calls; names and order come from the decompiled
`LiveIdleAnimationParameter` enum. The last, `EyeDartDownMinScale = 0.85`, is set by a **tail call** at
0x0057DCD0 rather than a regular one, which is why a naive scan finds only 29.

| | value |
| --- | --- |
| Blink spacing | 3000–4000 ms |
| Settling time before any movement | 1000 ms |
| Body shuffle speed | 10 mm/s, half straight |
| Lift height | 35 mm ± 8 |
| Head variability | 6° |
| Eye dart | up to 6 px, 50–200 ms, scale 0.92–1.08 |

**Idle yields per track.** `UpdateLiveAnimation` calls `MovementComponent::AreAnyTracksLocked` once per
track group before moving it, so a locked track is skipped while the others carry on. The reconstruction
checks the M5 scheduler's owned tracks the same way: a caller animation moving only the head does not stop
idle blinking.

Timing is taken, not read — `Advance(nowMs)`, as the M5 scheduler does — so the layer is testable offline
and instantly.

**The face is a base pose plus transient layers, not an accumulating pose.** This is how the engine does
it and it is what the first attempt here got wrong. `TrackLayerComponent::AddOrUpdateEyeShift(trackMask,
name, x, y, durationMs, ...)` calls `FaceLayerManager::GenerateEyeShift(..., durationMs,
ProceduralFaceKeyFrame&)` and stores the result through `AddPersistentLayer(name, Track<...>)`, with
`RemoveEyeShift` to take it away; blinks go the same route through `AddBlink`. So a dart is a **named,
time-limited keyframe combined onto a base face that is never itself modified**.

The first implementation read the live face, offset it and wrote it back, so every dart compounded the
last. On hardware the eyes drifted and grew until they merged into one rectangle — see "Hardening pass".

What is reproduced: a stable base pose, offsets measured from it, an explicit duration, and a return to
base when it expires. What is **not** claimed is the exact native use of `EyeDartUpMaxScale` and
`EyeDartDownMinScale`; they bound the result rather than being invented into a formula.

**Body movement is decided and reported but not driven.** 10 mm/s is within the engine's own parameters,
but sending wheel commands to an unattended robot is not something to switch on without watching it.
`--allow-motion` on the `behavior` command enables head and lift.

## 4. Mood

**The finding that bounds this section: mood does not change animation selection in this build.** Every one
of the 1047 entries across the shipped `animationGroups` assets carries `"Mood": "Default"`. There is not a
single mood-specific alternative, so selection is mood-invariant whatever the mood is. No selector was
written to pretend otherwise, and a test asserts the fact so a future build that adds alternatives fails it.

Where mood *does* bear on behaviour is gating which behaviours may run — the decompiled
`CurrentMoodCondition` tests a `SimpleMoodType` against a min and max. That belongs with the behaviour
system and is left to M8.

Recovered as data and no further: `EmotionType` (nine axes), the 11 shipped `emotionevents` files with
their named events and affectors, and `mood_config.json`'s piecewise-linear decay curves (`default` reaches
zero at 150 s; `Social`, `Confident` and `WantToPlay` carry their own). Both config sources contain
C-style comments, which real JSON does not allow, so the loader strips them.

## 5. Arbitration

**Caller beats reaction beats idle.** An application driving the robot directly must never find itself
fighting the idle system, and a reaction must never stamp on something the application started.

* An equal or higher priority **suppresses** rather than interleaves.
* A late completion cannot clear whatever took over.
* Per-reaction cooldowns (5 s by default) stop a flapping sensor retriggering the same animation.
* **Autonomy is off by default.** Connecting to a robot does not make it start moving. A caller request is
  never gated by it.
* Every decision is reported with its reason: `Played`, `Interrupted`, `Suppressed`, `Disabled`,
  `Unresolved`, `Refused`, `OnCooldown`.

## Established facts vs assumptions

**Established** — the 575 animation triggers and their ordinals; the 573 shipped trigger-to-group mappings
and that all resolve to present clips; the 21 reaction triggers; all 30 idle tunables and their values;
that idle yields per track; the nine emotion axes, the shipped emotion events, and the decay curves; that
all 1047 group entries are `Default`.

**Assumed** — that each `ReactionTrigger` plays the identically-named `AnimationTrigger`. Based on Anki's
own naming across three artifacts; not read from the native code, which does not export it.

**Unverified** — everything about how this behaves on a robot.

## Commands

```
dotnet run --project src/Cozmo.Conformance -- triggers <obb-dir> [--trigger <name>] [--mood <m>] [--seed N]
dotnet run --project src/Cozmo.Conformance -- behavior <robot-ip> --obb <dir> [--seconds 60] [--allow-motion]
```

`triggers` needs no robot.


## Hardening pass

A review of HEAD 60b4b01 found seven defects in this layer and two in frozen M5. All are fixed, each with a
regression test; three of those tests were checked against the unfixed code to confirm they actually fail.

### Behaviour layer

* **Idle could not blink without `--allow-motion`.** The flag drove `IdleBehavior.Execute`, which gated the
  face as well as the motors, so the documented no-motion acceptance run had nothing visible to watch.
  `ExecuteMotors` now gates head, lift and body separately; blinks and eye darts always run.
* **Falling was claimed but never watched.** `ReactionTable` mapped `RobotFalling`, but nothing subscribed to
  a falling transition, so it could never fire. `Sensors.FallingChanged` is now derived from
  `RobotStatusFlag.IsFalling` exactly as pick-up and charger are, and the dispatcher subscribes to it.
* **Caller animations were invisible to the arbiter.** The hierarchy claimed caller beats reaction, but the
  arbiter only ever saw requests made through itself — an application calling `robot.Animations.Play` could
  be replaced by a reaction. The arbiter now asks the scheduler whether an animation it did not start is
  running, and treats one as the caller holding the floor.
* **The reaction lock was modelled, not operational.** `BehaviorScope.DisableReactions` recorded a flag and
  nothing consulted it. Scopes now take the lock on the arbiter, which suppresses reactions while any is
  held and restores them when the scope is released.
* **Behaviours leaked their animation.** `Stop` dropped the task and left the robot animating. Behaviours now
  hold the scheduler's generation token for the animation they started, so stopping ends exactly that one and
  is a no-op once something else has replaced it.
* **Reaction work ran on the transport dispatch thread.** Sensor callbacks arrive on the path that also
  carries robot state, so animation selection there stalled telemetry. Reactions are queued onto the
  behaviour layer's own serialized worker; ordering is preserved and a slow consumer no longer blocks state.

### Frozen M5 — two specific defects, not a refactor

Wire and timing semantics are unchanged.

* **Ticker shutdown race.** `TickLoop` broke out of its loop and only then cleared `_running`, so a `Play`
  arriving in that window saw `_running` still true, started no ticker, and left the new animation with
  nothing advancing it. The decision to stop and the clearing now happen under the same lock `StartTicker`
  takes.
* **Dispatch after unlock.** `Advance` collected due keyframes under the lock and dispatched them outside
  it, so an animation replaced in that window received the outgoing clip's keyframes. A generation token is
  now re-checked before each dispatch.

### Elsewhere

* **Motor readiness.** Head and lift calibrate separately and report separate `MotorID`s, but both were
  collapsed into one `CalibratingMotors` flag, so the lift finishing cleared it while the head was still
  moving. They are tracked independently, and motion now requires `CalibrationComplete` rather than merely
  "nothing is calibrating right now" — an arriving `RobotState` before any calibration message is no longer
  mistaken for readiness. The existing test helper had been calibrating only the head, which is what let
  this pass.
* **Transport event isolation.** `Safe()` invoked a multicast event as one call, so a throwing subscriber
  prevented every later subscriber from running. Each subscriber is now invoked individually and a fault is
  counted against its own handler.
* **Wheel confirmation.** `DriveWheelsAsync` accepted any wheel motion as confirmation, so a robot already
  rolling satisfied the check the instant the command was sent — commanding forward while driving backwards
  reported success. Each wheel is now confirmed against the requested speed, with sign and a tolerance that
  allows for ramping.


## Idle face defect, found on hardware

The first hardware acceptance run of the idle layer found a reproducible fault: over time the two eyes
grew and drifted together until they formed one large rectangle, and the distorted face persisted through
reaction testing.

**Cause, in this layer and not in M5.** `IdleBehavior.Dart` read `Face.Current`, applied an offset, and
wrote the result back as the new persistent face. Each dart therefore started from the previous one:
positions random-walked without limit and `EyeScale` gained another `EyeDartOuterEyeScaleIncrease` (0.1)
every time. `Blink` had the same shape, so a blink restored the corrupted pose rather than a stable one.
The dart duration was computed, reported, and then ignored — nothing ever put the face back.

**That was not the whole story, and this paragraph originally said it was.** What stood here was: "The M5
procedural-face renderer was not touched: the earlier `anim_bored_01` face test passed, and nothing in this
failure implicates the renderer." The first half was the right call at the time — the accumulation fault
was genuinely in this layer, and fixing it was necessary. The second half was too strong. `anim_bored_01`
passing only showed the renderer survived *that* clip's poses; it did not clear the renderer generally, and
a later hardware run on `anim_reacttocliff_pickup_01` showed the renderer's geometry was invented and
wrong. Two faults, one symptom.

The renderer has since been reconstructed from `ProceduralFaceDrawer` and re-verified on hardware — see
[PROCEDURAL_FACE.md](PROCEDURAL_FACE.md). The idle fix below stands on its own regardless: it is what stops
the face accumulating, whatever the renderer draws.

**Fix.** Idle now keeps a base pose, captured once, that it never modifies. A dart or blink is a transient
computed from that base with the duration it was given; when the duration expires the base is put back.
Eye centres are clamped to `EyeDartMaxDistancePix` of the base and scales to
`[EyeDartMinScale, EyeDartMaxScale]`, so nothing can accumulate even if the remaining fidelity questions
are answered differently later. When an animation owns the face, idle forgets its base, because what is on
screen is no longer what it recorded.

**Regressions.** Six tests, five of which were confirmed to fail against the original code: eye centres
bounded across hundreds of darts, scales bounded and no larger in the second half of a run than the first,
the base pose intact after transients expire, blinks restoring the stable face, a dart lasting exactly its
duration, and the base being forgotten when something else takes the face.

## Errata from the Source Fidelity Sweep, 2026-09-19

Working in [SOURCE_FIDELITY_AUDIT.md](SOURCE_FIDELITY_AUDIT.md). **Hardware retest required** before M7 is
called re-verified: `behavior 172.31.1.1 --obb <dir> --seconds 60`.

* **The reaction map was never unrecoverable.** Section 2 above says the `BehaviorReactToX` classes are
  not exported and their configs carry no animation, so the trigger-to-animation link had to be inferred
  from names. Both halves are wrong. The OBB ships
  `config/engine/behaviorSystem/reactionTrigger_behavior_map.json` (read by
  `RobotDataLoader::LoadReactionTriggerMap`, 0x00520BC8), which maps every `ReactionTrigger` to a
  `behaviorID`; and the four behaviour classes are exported (36 symbols), each constructing a
  `TriggerAnimationAction` or `TriggerLiftSafeAnimationAction` with an `AnimationTrigger` immediate.
  `ReactionEvidence.NameCorrespondence` applies to no default entry any more.
* **Falling plays `ReactToImpact`, not `ReactToFalling`, and not on falling.** The map sends
  `RobotFalling` to the behaviour `ReactToImpact`; `BehaviorReactToImpact::AlwaysHandle` (0x00606408) arms
  on `FallingStopped` with impact intensity > 1000, `InitInternal` (0x006061F8) waits up to 5 s for the
  post-fall recalibration, and `TransitionToPlayingAnim` (0x00606348) plays `AnimationTrigger 0x1A0 =
  ReactToImpact`. The dispatcher now reacts to the landing (`Sensors.FallingStopped`), gated on the
  threshold; the start of a fall is reported and plays nothing.
* **The other three are confirmed, with their surroundings recorded:** `ReactToCliff` (0x19D, with
  `ReactToCliffDetectorStop` first while the wheels stop, severe-needs variants 0x13D/0x131, and a 60 mm
  back-up at 100 mm/s if still on the cliff); `ReactToPickup` (0x1A9, after 0.5 s, face and pet
  acknowledgements preferred, `HiccupRobotPickedUp` while hiccuping, repeated every 3-6 s while held);
  `PlacedOnCharger` (0x189, then after `timeTilSleepAnimation_s` the idle-timeout component plays
  `GoToSleepGetIn`, `GoToSleepSleeping`, `GoToSleepOff`). The sequencing is not implemented; the animation
  each one plays is.
* **The blink was invented.** Upper lids shut for 100 ms is not what the engine does. Its blink is a
  seven-frame scale table (`ProceduralFaceDrawer::GetNextBlinkFrame` 0x00585F18, table at 0x00C5AAD8):
  EyeScaleX x EyeScaleY multipliers (1.05, 0.85), (1.2, 0.6), (2.5, 0.1), (5.0, 0.05), (2.0, 0.15),
  (1.2, 0.7), (1.0, 0.9) at 33 ms each except the last at 100 ms, then the base restored; 331 ms in all.
  Ported as `IdleBehavior.BlinkFrames`.
* **The dart geometry was invented.** The engine's `GenerateEyeShift` (0x0058D100) draws x and y in
  +/-EyeDartMaxDistance and calls `ProceduralFace::LookAt` (0x00584158) with xMax = yMax = 5: the whole
  face moves by (x, y); only EyeScaleY changes, by a vertical factor from 1.1 (up) to 0.85 (down) times
  (1 +/- 0.1 min(1, |x|/5)) with the eye on the side looked *towards* the larger; looking down turns the
  eyes inward by up to 2 px. `EyeDartMinScale` and `EyeDartMaxScale` are not read on this path. Ported as
  `IdleBehavior.DartPose`. The section above that says the dart shifts `EyeCenterX` and grows the outer eye
  describes the old code.
* **Not settled: the dart's lifecycle.** The engine schedules the shift keyframe `duration + 33` ms after
  the previous one on a persistent layer, so it ramps in; what the persistent layer does once it has run
  out (`ITrackLayerManager::ApplyLayersToFrame` 0x0058E644 trims it to its last keyframe and resets its
  stream time) could not be read to a definite hold-or-drop. The transient-then-return lifecycle above is
  kept and labelled a local reading.
* **Idle head and lift** are streamed by the engine as `HeadAngleKeyFrame(currentDeg, 6, duration)` and
  `LiftHeightKeyFrame(35, 8, duration)` inside its live animation (`UpdateLiveAnimation` 0x0057D5F8);
  ours use the motion API with the same numbers. Recorded, not changed. **Idle body** is now recovered:
  speed uniform in +/-10 mm/s, 250-1500 ms, straight with probability `BodyMovementStraightFraction` else a
  turn in place accompanied by a 33 ms `LiveIdleTurn` eye shift; still not driven.
* **The 5 s reaction cooldown is ours.** The shipped map gives none of these four reactions a cooldown.
