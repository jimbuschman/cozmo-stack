# M5 — animation and expression

Status: **code-complete; hardware acceptance not yet run** (2026-09-18)

Built on the frozen M1 transport, M2 protocol, M3 device layer and M4 control layer. None of them was
reopened and no generated code was hand-edited.

```
robot.Animations.LoadFrom(assetsDir)
robot.Animations.Play("anim_bored_01")     // awaitable; completes, cancelled or replaced
robot.Animations.PlayGroup("ag_bored")
robot.Animations.Stop()
robot.Animations.IsPlaying / Playing / OwnedTracks
robot.Animations.Event                     // named events a clip raises

robot.Face.SetParameters(pose)             // 19 parameters per eye
robot.Face.ShowExpression(Expression.Happy)
robot.Face.HoldExpression(expr, duration)
```

## The assets are data

Cozmo's animations ship as FlatBuffers under `assets/animations`, 289 clips in this build, with the schema
in the `CozmoAnim` namespace. Groups are JSON under `assets/animationGroups`, 507 of them, each naming
clips with a weight, a cooldown and a mood.

The schema is read as data, not guessed at. The reader is a small FlatBuffers table reader written here
rather than a dependency, because only the reading half is needed and the schema is fixed. It bounds-checks
everything: a truncated or malformed file raises rather than returning quietly wrong numbers.

**All 289 shipped clips decode**, 27 face keyframes and 10 head keyframes in `anim_bored_01` alone, and the
whole set exercises every track the schema defines.

## The 19 eye parameters are the engine's, not a guess

The parameter names and their order come from `libcozmoEngine.so` itself: a contiguous block of strings in
.rodata at 0x00C1D399, in this order.

```
EyeCenterX EyeCenterY EyeScaleX EyeScaleY EyeAngle
LowerInnerRadiusX LowerInnerRadiusY UpperInnerRadiusX UpperInnerRadiusY
UpperOuterRadiusX UpperOuterRadiusY LowerOuterRadiusX LowerOuterRadiusY
UpperLidY UpperLidAngle UpperLidBend LowerLidY LowerLidAngle LowerLidBend
```

Every procedural-face keyframe in every shipped clip carries exactly nineteen floats per eye, which confirms
the count and the pairing independently of the names.

## One clock owns the timeline

`AnimationScheduler` is the only thing that decides when anything happens. It holds one clip at a time,
fires keyframes in timeline order, and blends the face between poses. The device classes keep their M3 and
M4 APIs but do not schedule animation: the scheduler calls them.

`Advance(nowMs)` takes the time rather than reading a clock, which is what makes the timeline testable. A
live robot has a 30 Hz thread calling it, with the system timer resolution raised for the same reason the
audio pacer needs it. A tick that arrives late fires everything it missed in order rather than skipping it.

**Face blending interpolates forward.** A face keyframe is a pose to be *at* when its trigger time arrives,
so the pose held now moves toward the next keyframe, not away from the last one. Interpolating backwards
leaves the face frozen until the next keyframe fires and then snaps, which is a step, not an animation. That
was a real bug caught by a test that asserted the eyes shrink steadily rather than in one jump.

## Track ownership

A clip claims every track its keyframes touch for as long as it runs. `Play` with `replaceRunning: false`
returns null rather than starting, when a track it needs is already owned, so a caller can tell "did not
play" from "played and finished". With `replaceRunning: true`, which is the default, the running clip ends
as `Replaced` and the new one takes over. Two animations never interleave.

## What is implemented, and what is not

| Track | State |
| --- | --- |
| Face, procedural | Implemented: parsed, blended, rendered and sent |
| Head | Implemented: angle and duration sent as an animation keyframe |
| Lift | Implemented: height and duration sent |
| Body, straight | Implemented: speed sent as equal wheel speeds |
| Body, arc | **Not implemented.** The schema gives a speed and a radius token, not wheel speeds, and the wheel-base geometry needed to convert is not established. Reported through `NotImplemented` rather than approximated. |
| Event | Implemented: raised to the caller |
| Audio | **Not implemented.** The keyframes carry Wwise event ids into the sound banks, which this milestone does not decode. Ids are preserved for a later milestone; a silence frame goes out so the timeline stays intact. |
| Backpack lights | **Not implemented.** The five arrays are colours but their channel order and scale are not established, so nothing is sent rather than flashing the wrong colour. |
| Face animation by name | **Not implemented.** The pre-rendered `faceAnimations` assets are not loaded. |

## Uncertainties, kept as uncertainties

* **The renderer is ours, not the engine's.** The parameter names, their order and the asset values are all
  authoritative. How the engine's `ProceduralFaceDrawer` turns radii, lid angles and bends into pixels has
  not been disassembled. The renderer here implements what the names and the observed value ranges support:
  a rounded rectangle per eye with lids cutting in at an angle. Expressions read correctly as expressions but
  are not pixel-identical to the retail robot. To reproduce an original exactly, play the clip that contains
  it rather than building a pose by hand.
* **The named expressions are ours too.** `Expression.Happy` and the rest are constructed from what the
  parameter names imply. Anki's own named expressions have not been recovered.
* **`BodyMotion.radius_mm` is a string in the schema**, not a number. The raw token is kept and a numeric
  value offered only when it parses.
* **Cooldowns are carried but not enforced.** `CooldownTime_Sec` is in the group data; what the engine does
  with it is not established, so honouring it would be a guess.
* **Lid bend, eye angle and the radius pairs** are read and blended but their exact geometric meaning is
  inferred from the names.

## Tests

213 pass, 34 of them new, all offline.

* **Scheduling** — keyframes fire in order within one frame of their trigger time; a late tick fires
  everything it missed in order; an animation completes exactly once; position and count track the timeline.
* **Cancellation** — stop ends the animation, reports it as cancelled, and later keyframes never fire.
* **Track ownership** — a clash is refused when asked to be; a replacement ends the first as `Replaced`;
  owned tracks are reported.
* **Face blending** — the eyes shrink steadily across a blend rather than in one jump; the last face is held
  rather than blanked.
* **Procedural face** — nineteen parameters in the engine's order; the wrong count is rejected; blending
  moves every parameter proportionally; a neutral face draws two separate eyes; a closed eye draws nothing;
  every expression renders something that fits one message; a blink closes the eyes; looking left and right
  move opposite ways.
* **Real assets** — all 289 shipped clips decode with keyframes in order and durations that cover them;
  every face keyframe carries nineteen floats per eye; one known clip decodes to counts and values confirmed
  against the reference FlatBuffers implementation; a real clip plays through the scheduler with every
  keyframe firing in order.

The asset tests locate the unpacked OBB and **skip if it is absent**, because the repository does not
redistribute Anki's assets. One test reports which of the two happened, so a machine without the OBB cannot
silently report the asset tests as passing.

## Hardware acceptance

Not yet run.

```
dotnet run --project src/Cozmo.Conformance -- animlist <assets-dir> [filter]
dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <dir> --name anim_bored_01
dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <dir> --group ag_bored
dotnet run --project src/Cozmo.Conformance -- face-expressions 172.31.1.1 --seconds 2
```

`animlist` needs no robot. `anim` checks that every keyframe fired and that the timeline ran to the clip's
own length; whether the robot looked right is the operator's call. `face-expressions` prints the art it sent
above each expression so the robot's face can be compared against it directly.
