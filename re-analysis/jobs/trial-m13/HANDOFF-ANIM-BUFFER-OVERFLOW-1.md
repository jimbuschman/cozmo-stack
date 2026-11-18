# ANIM-BUFFER-OVERFLOW-1 — source blocker, not a playback fix

SCOPE.md A: play an authored animation. Tag 13 is still truncated in the recorded run.
This item is **BLOCKED for manager review**, not READY or hardware accepted. No production
behaviour or fidelity classification changed. The native engine admits the critical packet
with the recorded counter inputs; changing the 8192 budget or adding a per-message reserve
would currently be an unsupported departure.

## Bounded source result

The shipped library SHA256 is
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
`known.py` was consulted before the traces below; the disassembly, not its notes, was read.

| Production step | Primary source and result | Stack comparison |
| --- | --- | --- |
| Message sizes | EngineToRobot::Size 0x007ABB90 / 0x007ABE46..0x007ABEA4 / 0x007ABF0A adds the union tag to the member size. AudioSample::Size 0x007BCDAA returns 744, hence **745**, not 747. FaceImage::Size 0x007BD462 returns vector length + 2, then the union adds 1. | StreamSizes and ToBytes use these serialized sizes. Native oracle checks audio/silence/head/lift/face/backpack/body/end/start: 745/1/4/4/56/11/5/1/2 for its selected inputs. |
| Feedback | AnimationState handler 0x00537FD0..0x00538024, after time sync, copies message+4 bytes played to robot+0x238 and message+8 frames played to +0x240; tag goes to +0x248. No tag-based counter reset. | EngineRobot.HandleAnimationState stores the reported counters without resetting them per tag. |
| Allowance | UpdateAmountToSend **entry 0x0057C6F0**, arithmetic 0x0057C6F6..0x0057C7AC: max(played - streamed + 8192, 0), capped at 30000; max(played frames - streamed frames + 14, 0). | StreamSendBuffer.UpdateAmountToSend uses the same lifetime counters and signed/wrapping arithmetic. |
| Send | SendBufferedMessages 0x0057BF60..0x0057C010 obtains actual Size, tests byte/frame allowances, sends reliable, then subtracts size and increments lifetime counters only on success. Audio is (tag & FE) == 8E. | StreamSendBuffer.Drain has these gates and counts; clearing unsent entries does not remove already-streamed bytes. |
| New stream | InitStream 0x0057B674..0x0057B82E: tag +A0, start/stream time, clears nonempty unsent list at 0x0057B7CC, resets start/end flags and creates audio. ClearSendBuffer 0x0057B044..0x0057B0CA deletes queued entries; it never adjusts robot lifetime counters. | InitStreamLocked and ClearSendBuffer follow this rule. Dropped silence/face entries are not sent or counted. |
| Handover | Update 0x0057CFF6..0x0057D034 calls UpdateStream for the selected animation; UpdateStream 0x0057C8E4..0x0057C9D4 refreshes/drains and buffers audio followed by Start once. Neither waits for a matching CA/tag acknowledgement. | Sending Start 13 while robot feedback still says tag 12 is not by itself a recovered stack difference. |
| Normal end | SendEndOfAnimation 0x0057C464..0x0057C496 sends directly, adding serialized size and one frame only on success. | SendEndOfAnimationLocked / SendDirect match. No End was sent before the recorded CB13. |
| Early robot end | EndAnimationHandler 0x0054541C..0x005454F4 checks started +88 and matching tag +8B, decrements loops +84 and sets ended +89. CheckIfDone 0x005441C0 returns 0 (SUCCESS) for ended. No requirement that all authored frames or EndOfAnimation were sent. | TriggerLiftSafeAnimationAction.Ended/CheckIfDone match this API consequence. The native oracle executes both routines and obtains SUCCESS. |
| Abort/replacement | Abort 0x0057B3E0..0x0057B5A2 broadcasts aborted, clears flags at +71/+72 and retires audio. It does not reset stream counters or erase the stream FIFO; later InitStream may erase the unsent tail. | AbortLocked follows the same counter/buffer rule. |

Current manifest (re-read, no edits): M2-008/M2-009 outbound union/layouts,
M3-012 send budgets, M3-013 FIFO/counters, M5-023 abort, M5-024 InitStream
are EXACT_SOURCE. M8-020 PlayAnimationAction whole lifecycle remains IMPLEMENTATION_GAP.
This bounded confirmation does not settle that larger record.

## Capture and native execution

Private input: `20261118-075139-LLM-SESSION/frames.jsonl`,
SHA256 `7ec89bde9b09402497fb9e44dba403f43e0c12ee19fec0d5822a6d92576f3d52`.
No bundle or packet payload was added to git. The tool optionally reads the private capture,
deduplicates reliable resends across sequence wrap, and reconstructs the outbound
8E..9B counter prefix. End 9A counts as an audio frame; Start 9B does not.

At 230847.44 ms, F1 reports played bytes 472279 and played frames 5395.
Through the prior sends at 230829.69 ms, serialized streamed bytes are 479685
and streamed audio/end frames are 5407. The next audio packet is capture line
27390 at **230863.8567 ms**, 745 serialized bytes.

Actual native UpdateAmountToSend computes **786 bytes / 2 frames** allowance.
Actual native SendBufferedMessages admits that AudioSample, returns 0, and leaves
**41 bytes / 1 frame** allowance with lifetime totals **480430 bytes / 5408 frames**.
Only successful Robot::SendMessage, object destruction, typed message accessor and
logging are stubbed. Message Size functions, gates, decrements, list removal and
counter updates run from the shipped library.

The next recorded feedback is at 230883.74 ms: played bytes 479674, frames 5407,
tag 0, followed by CB13. The firmware trace later reports BufferFull [707,747].
The native admission above does **not** establish what those two firmware arguments
mean, physical free space, allocation overhead, ring fragmentation or flush accounting.
In particular, do not claim the difference proves two bytes of overhead per message.

Run:
`python re-analysis/tools/emu/emu_anim_buffer.py --bundle <private-session-directory>`.
With no bundle, the recorded counter vector and native size/completion regression
still run. Both modes passed. The output explicitly retains
`MISSING: firmware buffer allocation and flush semantics; not emulated`.

## What remains

The requested full 229000–249500 ms production/firmware replay with tag 13 untruncated
has **not** been achieved. The critical native gate is reproduced, not the firmware.
Tag 10 and the successful tag 16 are not yet independently reproduced end to end.
No budget change, delayed-start policy, automatic retry, or rewritten SUCCESS result
was introduced. Manager review should determine the next evidence source for the
firmware rejection, or explicitly scope a compatibility policy; do not mark this item
finished on the strength of the native admission test.

Validation: native regression with and without the private input; fidelity check below.
No C# changed since the 8698/8698 full-suite result in HANDOFF-SPURIOUS-PICKUP-1.md
(the intervening merge changes QUEUE.md only). No fresh C# suite is claimed for this
source-blocked handoff.

