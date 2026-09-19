# M7 — Reactive behaviour and idle personality

Status: **CODE COMPLETE — HARDWARE ACCEPTANCE PENDING.**

Everything below is established offline and covered by tests. Nothing here has been run on a robot, and no
claim about how it behaves physically should be read into it until it has.

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
