# M5 — animation and expression

Status: **hardware-verified** (2026-09-18). `anim_bored_01` connected, calibrated, played through, moved
head, lift and body, changed the face and completed normally on the firmware-2457 robot. Three correctness
faults found by that run were fixed afterwards; see "Faults found on hardware" below.

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
| Body, straight and arc | Implemented as the engine does it: `animBodyMotion` with speed and a 16-bit radius, stopped when the keyframe's duration expires |
| Event | Implemented: raised to the caller |
| Audio | Implemented as a path: streamed on the scheduler tick from a pluggable source. The Wwise bank decoder that would make Cozmo's own sounds available is **not** written, so out of the box the track is silent. |
| Backpack lights | **Not implementable.** The shipping engine never implemented this track from animation assets either; see below. |
| Face animation by name | **Not implemented.** The pre-rendered `faceAnimations` assets are not loaded. |

## The three M5 gaps, closed

Each was blocked on something unknown. All three were resolved by reading the engine rather than guessing,
which changed the answer in every case.

### Body motion, including arcs

The earlier implementation synthesised wheel speeds and refused arcs, on the grounds that converting a
radius to wheel speeds needs the wheel base. **The engine does not do that.** It sends the speed and a
16-bit radius to the robot and lets the firmware do the geometry:

* `BodyMotionKeyFrame::GetStreamMessage` at 0x004FBA8C builds `AnimKeyFrame::BodyMotion`, which is
  `animBodyMotion` 0x99: `{ speed: i16, radius_mm: i16 }`.
* `SetMembersFromFlatBuf` at 0x004FB494 packs the clip's speed and its radius into that pair.
* `ProcessRadiusString` at 0x004FB588 resolves the symbolic tokens.

| Token | Radius sent |
| --- | --- |
| `STRAIGHT` | 32767 (`0x7FFF`) |
| `TURN_IN_PLACE` | 0 |
| `POINT_TURN` | 0 |
| anything containing a digit | `atoi`, clamped to a signed 16-bit range |
| anything else | the engine logs an error and drops the keyframe |

The resolution order matters and is reproduced exactly: a token with any digit is parsed numerically first,
then the two turn tokens, then `STRAIGHT`. Arcs now run, so they are stopped when their duration expires
like any other body move. The protocol definition was updated with this evidence and regenerated, so
`BodyMotion.RadiusMm` is a named field rather than `unknown`.

### Animation audio

The audio path is implemented and runs on the scheduler's own tick, so a sound stays lined up with the face
and the motors. One frame goes out per tick whether or not there is sound, which keeps the robot's buffer
fed, and the stream stops when an animation is cancelled.

What is **not** implemented is the Wwise decoder, and the reason is specific. The keyframes carry event ids.
`SoundbanksInfo.xml` maps an id to an event name and a bank, and `SoundBankIndex` reads that, so the two
events in `anim_bored_01` resolve to `Play__Robot_Sfx__Scrn_Sad_Long` and
`Play__Robot_Vo__Shared_Bored_Sigh_Short`. But that metadata **lists events and files without linking
them**: the event-to-file mapping lives in each bank's HIRC section, which is not parsed. Beyond that, 1987
of the 2214 `.wem` files are Wwise Vorbis, which needs codebook reconstruction and a Vorbis decoder.

So the seam is `IAnimationAudioSource`. `WavAudioSource` lets a caller map their own WAV files to event ids
and hear them play on the timeline today. With no source, the track is silent and the timeline is unchanged.

### Backpack lights

**The shipping engine never implemented this.** `BackpackLightsKeyFrame::SetMembersFromFlatBuf` at
0x004FAAD4 is a stub whose entire body logs:

> The BackpackLightsKeyFrame::SetMembersFromFlatBuf() method still needs to be implemented

and returns failure. The light track of a `.bin` animation therefore does nothing on a retail robot. The
JSON path is implemented and is used by the separate `backpackLightAnimations` assets, which is a different
asset set and a different feature.

Mapping the five float arrays onto the 10-byte wire message would be inventing behaviour Anki never shipped.
The keyframes are decoded and preserved, and reported through `NotImplemented` when reached.

### Animations must be opened on the robot

The first arc test produced no visible motion at all, at settings that should have turned the robot about
115 degrees. Inspecting the transmitted stream showed the body message going out correctly, so the robot was
ignoring it.

The engine brackets every animation. `AnimationStreamer::SendStartOfAnimation` at 0x0057C400 in
libcozmoEngine.so sends `animStartOfAnimation` (0x9B) carrying a one-byte tag before any keyframe, and
`SendEndOfAnimation` closes it afterwards. **Keyframes that arrive outside an open animation are ignored.**

We were never sending either. Face images worked anyway, and head and lift worked because they go out as
the direct `SetHeadAngle` and `SetLiftHeight` commands rather than as animation keyframes, which is why the
gap only showed once body motion moved onto the animation path.

The scheduler now opens each animation with its own tag and closes it, including when one is cancelled or
replaced, so an interrupted animation is never left open. The robot echoes the tag in its `AnimationState`,
so this is checkable rather than assumed: the `anim` command reports whether the robot confirmed the tag it
was given, and warns when it did not.

## Faults found on hardware, and fixed

The first hardware run of `anim_bored_01` played through correctly but rolled backward further than it
should have. `animdump` was written to compare the asset against what the player actually sends, and found
three faults. All three are now fixed and covered by tests.

**Body motion ignored its own duration.** `DriveWheels` runs until countermanded, and the only stop came
from the animation ending. The wheels ran for 800 ms where the asset asked for 264. Unlike head and lift,
whose duration the robot itself honours, a body keyframe has to be stopped explicitly. The scheduler now
does that, because the scheduler owns timing: it records when the keyframe expires and calls `BodyStop` at
that moment, and also when an animation is cancelled or replaced mid-move so a cut-short clip can never
leave the wheels turning. Measured after the fix: 267 ms against 264 asked, and 500 against 495 on
`anim_bored_02`.

**Clips sharing a file were unreachable.** `anim_bored_01.bin` holds both `anim_bored_01` and
`anim_bored_02`, but the library indexed by filename, so the second could not be loaded despite decoding
cleanly. Every clip name inside every file is now registered, and the whole file is cached when any clip in
it is first read.

**The lift is commanded to 0 mm**, below the 32 mm minimum the M4 control layer clamps to, because the
animation path does not clamp. This was left alone deliberately: the asset genuinely asks for 0, the
hardware run performed the lift twitch correctly, and clamping would change behaviour that demonstrably
works. Whether the firmware clamps it or treats 0 as "fully down" is not established.

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
* **The 10-byte `animBackpackLights` layout** is known only as a length. `GetStreamMessage` memcpies ten
  bytes, but since the FlatBuffers path is a stub there is nothing to map onto it.
* **Cooldowns are carried but not enforced.** `CooldownTime_Sec` is in the group data; what the engine does
  with it is not established, so honouring it would be a guess.
* **Lid bend, eye angle and the radius pairs** are read and blended but their exact geometric meaning is
  inferred from the names.

## Tests

251 pass, 72 of them new, all offline.

* **Scheduling** — keyframes fire in order within one frame of their trigger time; a late tick fires
  everything it missed in order; an animation completes exactly once; position and count track the timeline.
* **Cancellation** — stop ends the animation, reports it as cancelled, and later keyframes never fire.
* **Body duration** — a body keyframe stops when its own duration expires rather than when the clip ends,
  using the shape of `anim_bored_01`; cancelling or replacing mid-move stops the body; a zero-length or
  stationary keyframe is not scheduled for a stop.
* **Body encoding** — every radius token the engine understands encodes to the value the engine sends,
  including the `atoi` clamp at both ends of the 16-bit range; an unrecognised token is refused rather than
  guessed at; an arc runs and is stopped like any other move; the wire message carries both fields.
* **Audio** — streamed on the scheduler tick rather than by a pacer of its own; silent but timeline-intact
  with no source; no audio at all for a clip without an audio track; the first alternative that can be
  produced is used; the stream stops when the animation is cancelled; WAV decoding including stereo
  mixdown, resampling and rejection of non-WAV input; event ids resolve to names through the metadata.
* **Lights** — the keyframe is decoded and reported with its data intact, and nothing is invented.
* **Bracketing** — every animation is opened with a non-zero tag and closed again; each gets its own tag;
  cancelling and replacing both close the previous animation before anything else happens.
* **The synthetic arc clip** — body-only, two equal and opposite non-overlapping legs, speed and duration
  clamped at both ends, and each leg stopped when its own duration expires.
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

`anim` has passed: `anim_bored_01` played through on the firmware-2457 robot, moving head, lift and body and
changing the face. `face-expressions` has not been run.

```
dotnet run --project src/Cozmo.Conformance -- animlist <assets-dir> [filter]
dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <dir> --name anim_bored_01
dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <dir> --group ag_bored
dotnet run --project src/Cozmo.Conformance -- face-expressions 172.31.1.1 --seconds 2
```

`animlist` needs no robot. `anim` checks that every keyframe fired and that the timeline ran to the clip's
own length; whether the robot looked right is the operator's call. `face-expressions` prints the art it sent
above each expression so the robot's face can be compared against it directly.
