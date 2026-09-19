# Diagnostic: anim_bored_01 on hardware

Reported behaviour: the lift twitches, the head moves, and the robot rolls backward.

Reproduce this for any clip with:

```
dotnet run --project src/Cozmo.Conformance -- animdump <assets-dir> <clip-name> [--out file.txt]
```

No robot is involved. It prints the decoded asset, then drives the **real** `RobotAnimationSink` against an
offline robot and reads every message back out of the transport, so the second half is what the live player
genuinely sends rather than a re-derivation of it.

## Answers

### 1. Does anim_bored_01 contain a backward body-motion command?

**Yes, one.**

```
t=297 ms  duration=264 ms  radius_mm="STRAIGHT"  speed=-75
```

Negative speed is backward. This is the only body keyframe in the clip.

### 2. Is the observed backward movement explained by that keyframe?

**The direction and speed yes, the distance no.** The robot rolls backward about three times further than
the asset asks for.

| | Asset | Player |
| --- | --- | --- |
| Starts | t=297 ms | t=300 ms |
| Stops | t=561 ms | t=1100 ms |
| Duration | **264 ms** | **800 ms** |

The player sends exactly two wheel commands:

```
t=300 ms   DriveWheels left=-75.0 right=-75.0    BACKWARD
t=1100 ms  DriveWheels left=0.0   right=0.0      stop
```

`DriveWheels` runs until countermanded, and nothing countermands it until the animation ends. The stop at
1100 ms comes from `RobotAnimationSink.Finished`, not from the keyframe's duration: **the executor reads
`DurationTimeMs` for head and lift but ignores it for body motion.** At 75 mm/s the difference is roughly
20 mm commanded against 60 mm actually driven.

### 3. If body-motion execution is disabled, why are wheel commands being sent?

**It is not disabled.** Only *arc* motion is unimplemented. `STRAIGHT` maps cleanly onto equal wheel speeds
and has been implemented since M5 landed:

```csharp
if (k.IsStraight) { _robot.Transport.Send(new DriveWheels(k.Speed, k.Speed, 0f, 0f), flush: true); return; }
NotImplemented?.Invoke($"body motion with radius '{k.RadiusRaw}': arc geometry is not established");
```

My M5 report said "arc body motion" was not implemented and the `ANIMATION_LAYER.md` table distinguishes the
two rows, but the summary was easy to read as "body motion is off". It is not, and this clip is a straight
move, so it drives.

### 4. Are the lift and head twitches in the asset at those timestamps?

**Yes, both, and the player reproduces their timing faithfully.**

The lift twitch is two keyframes 99 ms apart:

| Asset | Player sends |
| --- | --- |
| t=396, dur=99, height_mm=**40** | t=400 `SetLiftHeight height=40.0 mm duration=0.099 s` |
| t=495, dur=99, height_mm=**0** | t=500 `SetLiftHeight height=0.0 mm duration=0.099 s` |

Up to 40 mm and back down within 200 ms is the twitch. Note the second one asks for **0 mm**, which is below
the 32 mm minimum lift height the control layer clamps to. The animation path does not clamp, so 0 goes out
raw. Whether the robot clamps it itself is not established.

The head has ten keyframes, all reproduced one for one, drifting down and then jittering:

```
t=0    0°      t=594  -8°     t=924  -14°
t=264  -3°     t=726  -15°    t=957  -14°
t=462  -19°    t=825  -15°    t=1023 -14°
              t=858  -14°
```

The cluster from 825 ms onward moves one degree at a time with durations of 33 to 66 ms, which is the
jitter. That is in the asset, not introduced by the player.

## What the player sends over the whole clip

| Message | Count | Asset keyframes |
| --- | --- | --- |
| `FaceImage` | 34 | 27 face keyframes, re-rendered once per 30 Hz frame and blended between them |
| `SetHeadAngle` | 10 | 10 head keyframes |
| `SetLiftHeight` | 2 | 2 lift keyframes |
| `DriveWheels` | 2 | 1 body keyframe, plus the stop at the end |
| `AudioSilence` | 2 | 2 audio keyframes, sent as silence because the Wwise ids are not decoded |
| nothing | 0 | 12 light keyframes, ignored because the colour encoding is not established |

Every count matches the asset. The keyframe decoding and the timeline are sound; the fault is confined to
how long the body command is left running.

## Faults found

1. **Body motion ignores its own duration.** The executor should stop the wheels at
   `TriggerTimeMs + DurationTimeMs`, not at the end of the animation. This is the cause of the over-long
   backward roll. Not fixed here, as instructed.
2. **The lift is commanded to 0 mm**, below the documented minimum of 32 mm that the M4 control layer
   clamps to. The animation path bypasses that clamp. Whether that is correct is unknown: the asset does ask
   for 0, so clamping might be wrong too.
3. **Clips that share a file are unreachable.** `anim_bored_01.bin` contains two clips, `anim_bored_01` and
   `anim_bored_02`, but the library indexes clips by filename, so `anim_bored_02` cannot be loaded even
   though it decodes. 289 files are indexed; the number of clips inside them is larger.

## Two faults in the diagnostic tool itself, found and fixed while writing it

Worth recording, because both would have produced confidently wrong reports.

The first version counted 60 wheel commands. The offline transport resends any unacked reliable message on
every tick, so the same command appeared dozens of times. Deduplicating by sequence id then went too far the
other way and reported 13 face images instead of 34, because the engine sends only one unacked packet per
update, so with nothing acknowledging anything the queue stalled and later commands never went out at all.
The harness now acknowledges what it sends, exactly as a robot does, and the counts match the asset.
