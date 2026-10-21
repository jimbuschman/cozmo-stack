# M5 TEARDOWN: messages on the wire during Dispose (2026-10-20)

Question from hardware run `20261020-200624-M5-HW-97161e`: TEARDOWN was INCONCLUSIVE with no printed reason; new
0x8E/0x97/0x93 messages appeared on the wire during Dispose (226702.65 -> 226772.30 ms).

**Answer: the stack behaved like the engine; the harness grader lacked the evidence to say so.**
Extractor pass (Claude cozmo-extractor); manager checked the bundle timing.

## The bundle

- Last streamer send-gate observation: 226699.50. Dispose request 226701.86, begin 226702.65.
- The last tick queued reliable seq 12751..12757 (8E, 93, 97, 8E, 97, 8E, 93). Sequence numbers are assigned at
  enqueue, in order, so 12754..12757 were queued before Dispose began; only their first wire send fell inside it
  (226703.56, 226737.70), paced by the reliable layer (M1-006).
- Then AbortAll traffic 12758..12762 (3C, 43, 8D, 3B, 3C) and the transport disconnect (type 3, seq 12763) at 226770.14.
- One "AnimationStreamer.ClearSendBuffer: Clearing unsent animFaceImage" marker at 226767.71: the face image the
  last tick could not admit was discarded, not sent.

## The original's order (libcozmoEngine.so)

~CozmoContext calls RemoveRobots at 0x004EAABE; ~Robot calls AbortAll at 0x00511120, then ~AnimationStreamer at
0x0051154A, whose ClearSendBuffer (0x0057B044..0x0057B0B4) logs and deletes each entry without sending. The reliable
transport is destroyed later, through ~RobotManager (0x004EABBE..0x004EABC2 -> 0x0052E6D8) -> ~MessageHandler
(0x0069D04A..0x0069D056) -> ~RobotConnectionManager DisconnectCurrent (0x0062EE98) -> StopClient -> delete
(0x0062EE9C..0x0062EEB8). So already-queued reliable traffic keeps draining until the disconnect, as in the stack.

## Why the grader said INCONCLUSIVE

`M5HwAnalysis.Teardown` counts any first send during Dispose with no enqueue origin as unknown; the harness never passes
`origins`, so this always happens when Dispose follows a tick. It also writes no `missing`/`problems` field, so the
summary printed no reason. Harness fix: derive origins from the send-gate diagnostics (gate observation before
dispose-begin = queued before Dispose), and state the reason when INCONCLUSIVE.

## Open lead (not blocking)

The stack disposes the transport after all of Engine.Dispose, Animations and NvStorage; the original shuts it down
inside ~CozmoContext at ~RobotManager, before the loader (0x004EABD0) and UiMessageHandler (0x004EC704). This bundle
shows no wire traffic from those later steps. No record covers the ~RobotManager -> ~RCM -> StopClient chain as a
teardown-order fact; if it matters it is a NEW RECOVERABLE_GAP (read 0x0052E6F8..0x0052E704, 0x0069D04A..0x0069D056,
0x0062EE88..0x0062EEDC, the RCM ctor's +0x18 store and the RT::Disconnect closure).
