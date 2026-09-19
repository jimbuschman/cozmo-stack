# M5 — animation and expression

Status: **COMPLETE and FROZEN** (2026-09-18). `anim_bored_01` connected, calibrated, played through, moved
head, lift and body, changed the face and completed normally on the firmware-2457 robot, and `anim --arc`
drove a visible curve and an equal arc back. Several correctness faults found by those runs were fixed
afterwards; see "Faults found on hardware" and "Opening an animation is not enough" below.

Frozen means the API and the timing model are settled and are not to be reopened unless a specific failure
appears. What is deferred rather than missing is listed under "Deferred" at the end of this document.

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
| Audio | Implemented: one message on every streamed frame, silence when there is nothing to play, which is what advances an animation on the robot. Sounds come from a pluggable source; the Wwise bank decoder that would make Cozmo's own sounds available is **deferred**, so out of the box the frames are silent. |
| Backpack lights | Decoded and carried, not acted on. The shipping engine never drove this track from animation assets either; acting on it is **deferred** because the asset colour encoding is unestablished. See below. |
| Face animation by name | **Deferred.** The pre-rendered `faceAnimations` assets are not loaded, so a clip naming one falls back to the procedural face. |

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

### Opening an animation is not enough: it has to be fed

Adding the bracketing did not make the arc move, and it stopped the face displaying during
`anim_bored_01`. Reading the streamer properly, rather than just `SendStartOfAnimation`, explained both at
once. The full working is in `DIAGNOSTIC_animation_start_sequence.md`; the finding is:

**Every streamed animation frame carries exactly one audio message.** `UpdateStream` at 0x0057C84C buffers
`animAudioSample` (0x8E) when the frame has sound and `animAudioSilence` (0x8F) when it does not, with no
path past it, and `SendBufferedMessages` counts both against the robot's audio budget using
`(tag & 0xFE) == 0x8E`. `UpdateAmountToSend` at 0x0057C6F0 expresses the engine's entire flow control in
those frames: `14 - (streamed - played)` audio frames and `8192 - (streamed - played)` bytes, read from
`animState.numAudioFramesPlayed` and `animState.numAnimBytesPlayed`. The silence frames are not padding —
they are what carries the animation forward on the robot.

Our player only sent audio for clips with an audio track, so `anim_bored_01` and the arc opened an
animation and then fed it nothing. The arc never moved because an unfed animation never advances, and the
face stopped because `animFaceImage` now arrives inside an open animation instead of outside one.

Four things changed as a result, all of them matching what the engine does:

* one audio message per streamed frame, always, silence when there is nothing to play;
* the frame is skipped entirely when the robot has no room, as `ShouldProcessAnimationFrame` does;
* `animStartOfAnimation` is buffered on the first frame that streams, after that frame's audio, rather than
  when the animation is set up — so a clip stopped before it streamed is never opened, and never closed;
* a trailing `animAudioSilence` follows `animEndOfAnimation`, and the tag counter now skips 0x00 and 0xFF
  as `IncrementTagCtr` does.

Keyframes within a frame also go out in the engine's fixed per-track order — head, lift, event, face,
lights, body — rather than in whatever order the clip lists them.

**Confirmed on hardware, both halves.** `anim --arc` now drives a visible curve and an equal arc back,
where before the fix the same command moved the robot not at all — the end-to-end proof that the animation
stream works, and not just that the radius encoding is right. `anim_bored_01` displays its face again and
the animation reads correctly overall, which closes the regression the bracketing introduced.

That also settles the one inference in `DIAGNOSTIC_animation_start_sequence.md` that the engine could not
answer on its own: whether the robot holds `animFaceImage` against the animation clock once an animation is
open. The face returning the moment silence frames started flowing, with nothing else changed, says it
does.

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

* **The renderer is a port of the engine's**, reconstructed from `ProceduralFaceDrawer` and written up in
  [PROCEDURAL_FACE.md](PROCEDURAL_FACE.md): a 128x64 canvas, eyes at x = 32 and 96 with a nominal 30x40 box,
  elliptical corner arcs from `cv::ellipse2Poly`, lid quads with bend arcs, and one whole-face affine about
  (64, 32). Three things in it are still open and are named there rather than guessed: which radius parameter
  feeds which corner, the polygon fill rule, and which scanline parity to keep when going from the engine's
  64 rows to our verified 32-row wire image.
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
* **Audio** — streamed on the scheduler tick rather than by a pacer of its own; one message on every
  streamed frame including clips with no audio track, which stream silence; silent but timeline-intact with
  no source; the first alternative that can be produced is used; the stream stops when the animation is
  cancelled; WAV decoding including stereo mixdown, resampling and rejection of non-WAV input; event ids
  resolve to names through the metadata.
* **Flow control** — streaming stops once 14 frames are outstanding and resumes as the robot reports them
  played; a sink that reports nothing runs unpaced, so offline replay is unaffected.
* **Lights** — the keyframe is decoded and reported with its data intact, and nothing is invented.
* **Bracketing** — an animation is opened on its first streamed frame and not before, behind that frame's
  audio; one stopped before it streamed is never opened and never closed; every animation gets its own tag
  in 1..0xFE; cancelling and replacing both close the previous animation before anything else happens.
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

`anim` has passed: `anim_bored_01` played through on the firmware-2457 robot, moving head, lift and body,
changing the face and reading correctly overall, and `anim --arc` drove a visible curve and an equal arc
back. `face-expressions` has passed.

```
dotnet run --project src/Cozmo.Conformance -- animlist <assets-dir> [filter]
dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <dir> --name anim_bored_01
dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <dir> --group ag_bored
dotnet run --project src/Cozmo.Conformance -- face-expressions 172.31.1.1 --seconds 2
```

`animlist` needs no robot. `anim` checks that every keyframe fired and that the timeline ran to the clip's
own length; whether the robot looked right is the operator's call. `face-expressions` prints the art it sent
above each expression so the robot's face can be compared against it directly.


## Deferred

M5 is frozen with these items open. None of them blocks it: each is a capability that needs evidence we do
not yet have, or hardware we do not yet have, not a defect in what is built. They are recorded here so that
"not implemented" is never mistaken for "overlooked".

| Item | Why it is deferred | What would close it |
| --- | --- | --- |
| ~~Wwise bank and media decoding~~ | **Closed by M6.** Event resolution, all 2019 Wwise Vorbis files and the 220 mono ADPCM files decode from the shipped assets. See `WWISE_AUDIO.md`. | Done. |
| **Enhanced backpack-light keyframes** | The keyframe is decoded and carried with its data intact, but the asset's colour encoding is not established, so acting on it would mean inventing the mapping. The shipping engine never drove this track from animation assets either. | Establish the encoding from the engine's own `BackpackLightsKeyFrame::GetStreamMessage`, or from a capture of the stock app playing a clip that uses the track. |
| ~~**Exact Anki procedural-face fidelity**~~ | **Closed 2026-09-19.** `ProceduralFaceDrawer` was disassembled and ported: canvas, eye geometry, corner arcs, lids, both transforms and `roundf` semantics. Hardware-verified with `anim_reacttocliff_pickup_01`. Three low-level details remain open and are named in `PROCEDURAL_FACE.md` rather than guessed — corner-radius parameter assignment, polygon fill rule, scanline parity. | Done, apart from those three. Closing them needs either a finer trace of `DrawEye`'s register plumbing or face images captured from the stock app to compare against. |
| **Pre-rendered `faceAnimations`** | The OBB's pre-rendered face assets are not loaded, so a clip whose face track names one falls back to the procedural face. | Decode the `faceAnimations` asset format and feed it through the existing face track. |
| **Group cooldown enforcement** | Animation groups carry cooldown and mood fields. Selection honours mood; cooldown is parsed and exposed but not enforced, because how the engine measures and resets it is not established. | Read the engine's group selection to establish the cooldown clock, then enforce it in `AnimationGroup.Choose`. |
| **Lift 0 mm semantics** | What the robot does with a lift height of exactly 0 mm is unresolved: it may mean "lowest position" or "no change". The value is passed through unaltered rather than being reinterpreted. | A hardware experiment, or the engine's own clamping in the lift keyframe path. |
| **M4 cube hardware acceptance** | Cube support is code-complete and offline-tested through the real message path. Hardware discovery has been observed (a real cube appeared during discovery after being tapped, 2026-09-19); connection state, tap, movement, up-axis and battery telemetry have not been exercised on a robot. | `dotnet run --project src/Cozmo.Conformance -- cubes 172.31.1.1 --acceptance` with a cube to hand. |


## The whole-face transform, corrected after hardware

Hardware testing of `anim_reacttocliff_pickup_01` — which deliberately holds extreme squash/stretch poses
such as `FaceScaleX=1.82, FaceScaleY=0.07` — showed the two eyes merging into one large rectangle. The
fault was in this renderer, not in M7.

**What the renderer did.** `FaceScaleX/Y` were applied to each eye's width and height while the eye centres
stayed at their fixed nominal positions of 40 and 88, and `FaceAngle` was added to each eye's own angle. At
`FaceScaleX=1.82` each eye widened from 28 px to about 51 px, so the left spanned 14.5–65.5 and the right
62.5–113.5. They overlapped, which is the rectangle seen on the robot.

Applying the transform over the whole image fixed the mechanism but not the geometry: 40, 88, 28 and 28 were
all invented. The engine's eyes are 64 px apart on a canvas twice as tall, so the renderer was rebuilt from
the binary in full — see [PROCEDURAL_FACE.md](PROCEDURAL_FACE.md).

**What the engine does.** `ProceduralFaceDrawer::DrawFace` draws both eyes at their nominal positions, then
builds one affine with `GetTransformationMatrix(angle, scaleX, scaleY, transX, transY, 64, 32)` and applies
it with `cv::warpAffine` over the whole image. Disassembling that function at 0x00584FF8 gives the matrix
directly:

```
row0:  cos*sx    sin*sy    (1 - cos*sx)*cx - sin*sy*cy + tx
row1: -sin*sx    cos*sy      sin*sx*cx + (1 - cos*sy)*cy + ty
```

which is the familiar rotate-and-scale-about-a-centre form. The call site passes the centre as the
constants 64 and 32 — the centre of the canvas being drawn — rather than anything from the face
parameters, so `FaceCenterX/Y` is the translation applied afterwards.

That answers the three questions directly:

* **`FaceScaleX/Y` scale eye positions as well as eye geometry**, because the centre terms move every
  coordinate away from the canvas centre. At 1.82 the eyes move to about x=20 and x=108: further apart as
  they widen, not overlapping.
* **`FaceAngle` rotates the eye centres around the face centre**, not just each eye in place.
* **`FaceCenterX/Y` is a translation** applied after the centred scale and rotation.

**The fix** applies that same matrix, inverting it per output pixel rather than warping a second buffer,
which is the same result as nearest-neighbour `warpAffine` without the intermediate image. The 19 eye
parameters keep their meanings and the corner and lid behaviour is untouched; only the composition of the
whole-face parameters changed. Wire and timing behaviour are unchanged — this affects what is drawn into a
frame, not when or how frames are sent.

One sign error was caught by a rotation test during the work: the row-1 centre term is `+sin*sx*cx`, not
the row's own `-sin*sx` coefficient. With the wrong sign the face centre does not map to itself and the
vertical half of a rotation cancels out.

**Regressions** use the clip's own poses. Three of them fail against the previous renderer: the 1.82 pose
keeping two separate eyes, a wider face increasing the gap between them, and a face angle moving the eyes
to different heights.

### That fixed the mechanism but not the geometry

Hardware retesting after the above still showed a materially wrong face. The reason is that correcting how
the whole-face parameters compose did nothing about the numbers they compose *over*, and those numbers —
`NominalEyeWidth`/`Height` of 28x28, eye centres at 40 and 88, a 128x32 canvas — were invented. The source
said so: "chosen so a neutral face fills the panel sensibly".

The renderer was therefore rebuilt from `ProceduralFaceDrawer` in full rather than adjusted again. The
canvas is 128x64, the eyes sit at x = 32 and 96 with a nominal 30x40 box, corners are `cv::ellipse2Poly`
arcs, lids are quads with bend arcs, each eye has its own transform about its own origin, and the second
eye is the first mirrored. [PROCEDURAL_FACE.md](PROCEDURAL_FACE.md) has every constant with the address it
came from, and names the three things that could not be recovered.

**Hardware-verified 2026-09-19** with `anim_reacttocliff_pickup_01`: the eyes stay distinct through the
extreme squash/stretch, and the resting face is correct. Commit `bf2ddc2`.

The tests were replaced as well, because the old ones could not have caught this: `FaceTransformTests` only
asserted that two separate blobs appeared, which was true of the invented geometry and the real geometry
alike. `ProceduralFaceRendererTests` measures against the recovered constants instead.

## Errata from the Source Fidelity Sweep, 2026-09-19

Four confirmed divergences from the engine in this frozen layer, all fixed with regressions; the working is
in [SOURCE_FIDELITY_AUDIT.md](SOURCE_FIDELITY_AUDIT.md). **Hardware retest required** before M5 is called
re-verified: `anim ... --name anim_bored_01 --wwise <obb dir>`.

* **Head and lift keyframes were sent as motor commands.** The text above says head and lift "go out as
  the direct `SetHeadAngle` and `SetLiftHeight` commands rather than as animation keyframes". The engine
  does not: `HeadAngleKeyFrame::GetStreamMessage` (0x004F8C08) builds `animHeadAngle` (0x93,
  `{u16 durationTime_ms, i8 angle_deg}`) and `LiftHeightKeyFrame::GetStreamMessage` (0x004F8F80) builds
  `animLiftHeight` (0x94, `{u16, u8 height_mm}`), applying the keyframe's variability with
  `RandIntInRange(value - var, value + var)`. The speed and acceleration the commands carried (10/10 and
  3/20) were invented. The protocol definition now names those two messages' fields from the engine, and
  the sink sends them.
* **Audio alternatives were not chosen by probability.** "The first alternative that can be produced is
  used" was ours. `RobotAudioKeyFrame::GetAudioRef()` (0x004F9E18) picks one by cumulative probability
  from `RandDbl(1.0)` (`GetAudioRefIndex` 0x004F9AEC), with `1/n` each when the clip carries no usable
  probabilities (`SetMembersFromFlatBuf` 0x004F9E54). Ported; falling back to the other alternatives when
  the chosen one cannot be decoded is kept and labelled as ours.
* **Interpolation** blends angles as directions and clips every eye parameter; see `PROCEDURAL_FACE.md`.
* **The named `Neutral` expression was a guess.** The engine's resting face is the `anim_neutral_eyes_01`
  keyframe; see `PROCEDURAL_FACE.md`. `Expression.Neutral` is now that face; the other expressions remain a
  labelled local convenience built on it.

Confirmed rather than changed: the body-motion stop message `{speed 0, radius 0x7FFF}` is exactly what the
engine's `BodyMotionKeyFrame` constructors (0x004FB14C, 0x004FB170) install at +0x16 and send when the
duration elapses; `AnimationGroup::GetAnimationName` (0x0058A970) chooses by weight as `Choose` does. The
group cooldown mechanism is now recovered (per-name cooldown set to `now + CooldownTime_Sec` on selection,
excluded until expiry, all-on-cooldown picks the soonest to expire) and stays deferred as instructed; so
does the `UseHeadAngle` gate three CozmoSays groups carry.
