# Robot animation buffer full ends an animation early; the API reports SUCCESS

Observed 2026-11-18 in an operator MCP session (manager notes; observation, not a settled finding).
Session bundle: `re-analysis/acceptance/hardware/20261118-075139-LLM-SESSION/` (not committed).

## Symptom

`animation CodeLabHappy` (group `ag_codelab_happy` -> `anim_meetcozmo_celebration_02`) returned SUCCESS after 0.55 s,
but nothing visible played on the robot. The same call repeated 15 s later played in full (3.6 s).

## Evidence (from the bundle)

- Tag 13 (failed play), events.jsonl / stack-log.txt:
  - `Animation.Init.SendBufferNotEmpty: Expecting SendBuffer to be empty. Will clear.` (unsent animAudioSilence and
    animFaceImage from the previous stream cleared), then `RemovePersistentLayer: KeepAliveEyeDart`.
  - Out StartOfAnimation(0x0d) at atMs 230395; robot AnimStarted(0x0d) only at 230801 (robot still playing tag 12).
  - Robot trace `AnimationController.BufferKeyFrame.BufferFull fmt#6 [707, 747]` at 230913, then robot AnimEnded(0x0d).
    The stack never sent EndOfAnimation for tag 13.
  - Engine: `PlayAnimation.EndAnimationHandler: tag 13 ended` -> action SUCCESS -> `AnimationStreamer.Abort ...
    hasFramesLeft=1 endSent=0`.
  - Robot AnimationState NumAnimBytesPlayed jumped 472279 -> 479674 (+7395) at the end: the buffer appears to be
    flushed and counted as played.
- Tag 10 (atMs ~204213, an engine-initiated animation just after turn-to-face) shows the same pattern:
  `SendBufferNotEmpty` clear, `BufferFull [6, 13]`, robot AnimEnded with no EndOfAnimation sent.
- Tag 16 (successful repeat) had no BufferFull trace.
- No `NegativeMinBytesFreeInRobot` / `BytesStreamedOverflow` logs in the session. A rough decode of frames.jsonl
  (manager's script, deduped reliable seqs, bytes of outbound 0x8E..0x9B) puts unplayed bytes at about 7400 just
  before the overflow: under the 8192 budget of M3-012. So the stack believed it was in budget while the robot
  reported a full buffer.

## Open questions (not answered)

1. Does the stack's streamed-byte counter or its handling of the cleared send buffer / previous stream (tag 12)
   differ from the engine (AnimationStreamer::SendBufferedMessages 0x0057BF60, UpdateAmountToSend 0x0057C6F6,
   Animation::Init send-buffer clear, the AnimationState handler 0x00537FD0)? Or would the original engine overflow
   the same way?
2. The meaning of the BufferFull trace arguments and the robot's buffer accounting (firmware; HARDWARE_ONLY).
3. API consequence: the robot's AnimEnded for the tag completes PlayAnimation as SUCCESS even though most frames were
   never played. This matches the engine's handler path as logged, but should be confirmed against the binary.

No code changed.
