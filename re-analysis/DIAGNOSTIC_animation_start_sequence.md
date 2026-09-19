# The native animation start sequence, and why our arc and face broke

All addresses are in `resources/lib/armeabi-v7a/libcozmoEngine.so` from the official 3.4.0 APK.
Message ids are from `re-analysis/protocol/cozmo_robot_protocol.json`.

## Summary

The engine never streams an animation without also streaming audio. **Every streamed animation frame
buffers exactly one audio message — `animAudioSample` (0x8E) when there is sound, `animAudioSilence`
(0x8F) when there is not — and the engine's entire flow control is expressed in audio frames and animation
bytes that the robot reports as *played*.**

Our player only sent audio when the clip had an audio track. `anim_bored_01` and the synthetic arc clip
have no audio track, so once we added `animStartOfAnimation` we opened an animation on the robot and then
fed it nothing. That single difference explains both reported symptoms:

- the arc never moved, because an open animation that is never fed never advances;
- the face stopped displaying, because `animFaceImage` now arrives inside an open animation rather than
  outside one, and outside one is what made it appear immediately in M3.

The bracketing itself was right. What was missing is the audio frame that carries the animation forward.

## Answers to the seven questions

### 1. Exact `animStartOfAnimation` (0x9B) payload and tag semantics

`AnimationStreamer::SendStartOfAnimation()` @ 0x0057C400 takes **no arguments**. It reads a single byte
from `this+0xA0` and constructs `AnimKeyFrame::StartOfAnimation` from it, so the payload is exactly one
byte: the tag. `this+0xA0` is written by `InitStream(Animation*, unsigned char)` @ 0x0057B674 from its
`unsigned char` argument, so the tag is chosen when the animation is initialised, not when the start
message is sent.

`AnimationStreamer::IncrementTagCtr()` @ 0x0057B660 advances the counter at `this+0x70`:

```
      ldrb r1,[r0,#0x70]
loop: adds r2,r1,#1 ; uxtb r1,r1 ; cmp r1,#0xFD ; mov r1,r2 ; bhi loop
      strb r2,[r0,#0x70]
```

It keeps incrementing while the value it came from was above 0xFD, so **0x00 and 0xFF are never stored**.
The valid tag range is 1..0xFE. Our counter used 1..0xFF; that is now corrected.

### 2. Is the start message sent immediately?

No. `SendStartOfAnimation` calls `BufferMessageToSend` @ 0x0057BF2C, which appends to a linked-list FIFO at
`this+0x8C/0x90/0x94`. It then sets `this+0x71 = 1` (a halfword store of 1, which clears `this+0x72` at the
same time). `this+0x71` is the "start already sent" flag.

`SendEndOfAnimation(Robot&)` @ 0x0057C448 is the asymmetric one: it calls
`Robot::SendMessage(msg, true, false)` **directly**, bypassing the buffer, and on success writes the
halfword 0x0100 at `this+0x71`, clearing "started" and setting `this+0x72`.

### 3. Is track-enable / track-lock / ownership state sent separately?

No. Nothing in `InitStream`, `UpdateStream`, `SendStartOfAnimation` or `SendEndOfAnimation` sends
`enableAnimTracks` (0x9E) or `disableAnimTracks` (0x9D). Track selection is expressed purely by which track
messages the engine chooses to buffer on a given frame. `animState.enabledAnimTracks` is a robot-side
bitmask the robot reports; the animation streamer never writes it. **No explicit enable bits are required
to open an animation**, so that was not the cause of the arc failing.

### 4. Does the procedural face go out on a different path once playback is active?

No — same message, different delivery. `AnimationStreamer::BufferFaceToSend(ProceduralFace const&)` @
0x0057C1FC calls `ProceduralFaceDrawer::DrawFace` then `FaceAnimationManager::CompressRLE`, constructs
`AnimKeyFrame::FaceImage` (0x97), and calls `BufferMessageToSend`. It is the **same `animFaceImage` message
we already send**. The only differences are that it goes through the animation send buffer rather than
straight out, and that it is only reached when the face-animation track produced nothing this frame:

```
BufferMessageToSend(faceAnimTrack.GetCurrentStreamingMessage(t0, t))    ; 0x0057CA18
cbnz r0, skip                       ; a real face-animation frame was buffered
if (faceTrack is empty && layeredKeyFrames.hasProceduralFace)
    BufferFaceToSend(proceduralFace)                                    ; 0x0057CA30
```

So a procedural face is only sent when the clip's own face track has nothing to say.

### 5. What does the robot return after start?

`animStarted` (0xCA) and `animEnded` (0xCB), each carrying the one-byte tag, and continuously
`animState` (0xF1), whose 15-byte body is
`timestamp u32, numAnimBytesPlayed i32, numAudioFramesPlayed i32, enabledAnimTracks u8, tag u8,
clientDropCount u8`.

`UpdateAmountToSend(Robot&)` @ 0x0057C6F0 reads four consecutive words at `robot+0x238`:

| offset | meaning |
| --- | --- |
| 0x238 | `numAnimBytesPlayed`, from the robot |
| 0x23C | bytes streamed, incremented by the engine on every send |
| 0x240 | `numAudioFramesPlayed`, from the robot |
| 0x244 | audio frames streamed, incremented by the engine |

and computes

```
bytesBudget = min(8192 - (streamed - played), 30000)
audioBudget = max(14 - (audioStreamed - audioPlayed), 0)
```

The log format string at 0xBEF3D4, `"minBytesFree:%d numBytesStreamed:%d numBytesPlayed:%d"`, confirms the
first line's operands. This is direct confirmation of two constants we had only inferred from hardware:
**the robot's animation receive buffer is 8192 bytes and its audio buffer is 14 frames.**

`SendBufferedMessages(Robot&)` @ 0x0057BF60 drains the FIFO against those two budgets, and classifies a
message as audio with `(tag & 0xFE) == 0x8E` — which is exactly the pair 0x8E `animAudioSample` and 0x8F
`animAudioSilence`. **A silence frame costs an audio slot just like a sample does**, which is only
meaningful if the robot advances its animation on silence frames too.

### 6. Are direct `animFaceImage` messages ignored while an animation is open?

The engine never sends one directly while an animation is open, so its code does not answer this by
construction. What it does establish is that the engine's face frames are ordered inside the animation
stream, behind that frame's audio message, and are subject to the same 8192-byte budget. Our own
observation — the face rendered immediately before bracketing and stopped after it, with no other change —
is consistent with the robot queueing `animFaceImage` against the animation clock once one is open, and
that clock was not advancing because we sent no audio.

This one is inference from our hardware behaviour plus the engine's structure, not a decompiled fact. It is
the weakest claim in this document, and the hardware retest is what settles it.

### 7. Exact order of start, track messages and end

From `UpdateStream(Robot&, Animation*, bool)` @ 0x0057C84C. Per streamed frame:

1. `ShouldProcessAnimationFrame(anim, t0, t)` @ 0x0057CC6C gates the frame. It returns false if the send
   buffer is non-empty (`this+0x94 != 0`), and otherwise tail-calls
   `RobotAudioClient::UpdateAnimationIsReady(t0, t)`. **A frame is only processed when everything
   previously buffered has gone out and the audio client says the robot is ready.**
2. `UpdateAmountToSend(robot)`, then `SendBufferedMessages(robot)` — drain the previous frame's leftovers
   first.
3. `GetAudioToSend(...)`, then `TrackLayerComponent::ApplyLayersToAnim(...)`.
4. Buffer **`animAudioSample` (0x8E) if this frame has audio, otherwise `animAudioSilence` (0x8F)** —
   unconditional, one or the other, every frame (0x0057C992 / 0x0057C9AE).
5. `if (this+0x71 == 0) SendStartOfAnimation()` — buffered here, **after** that frame's audio message.
6. `animHeadAngle` (0x93)
7. `animLiftHeight` (0x94)
8. `animEvent` (0x95)
9. `animFaceImage` (0x97) from the face track, else `BufferFaceToSend(proceduralFace)`
10. `animBackpackLights` (0x98), gated on a flag from `ApplyLayersToAnim`
11. `animBodyMotion` (0x99)
12. `animRecordHeading` (0x91)
13. `animTurnToRecordedHeading` (0x92)
14. `SendBufferedMessages(robot)` — drain.

At the end of the animation: `Animation::HasFramesLeft()`, `RobotAudioClient::AnimationIsComplete()`,
`ClearCurrentAnimation()`, **`SendEndOfAnimation(robot)` sent directly**, then a buffered
`animAudioSilence`, then `SendStartOfAnimation()` again if the animation repeats, then a final drain.

Each track emits at most one message per frame:
`Track<T>::GetCurrentStreamingMessage(t0, t)` computes `t - t0`, checks `IKeyFrame::IsTimeToPlay`, takes the
message, and calls `MoveToNextKeyFrame()`. That matches how our scheduler already fires keyframes.

## What our player did differently

| | engine | ours, before this fix |
| --- | --- | --- |
| audio message per frame | always, sample or silence | only when the clip has an audio track |
| frame pacing | gated on send buffer empty and robot audio room (14 frames, 8192 bytes) | fixed 30 Hz, ungated |
| start message | buffered on the first streamed frame, after that frame's audio | sent at `Play()` time, before anything |
| end message | sent directly, then one silence frame | sent directly, no trailing silence |
| tag range | 1..0xFE | 1..0xFF |
| per-frame order | audio, start, head, lift, event, face, lights, body, heading | clip order |

The first row is the one that breaks playback. The second compounds it: 30 Hz is slower than the robot's
~28.6 ms audio drain, so even a clip with sound would have starved the buffer over time instead of holding
the engine's 14-frame lead.
