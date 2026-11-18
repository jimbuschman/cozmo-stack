# LLM-SENSES-1

READY FOR CLAUDE on codex-m13-manager-2; Windows main gate remains blocked.
SCOPE.md A: connection for an LLM.
Offline bridge/MCP work only; no hardware acceptance or SCOPE completion claimed.

## Change and path

| Input -> output | Existing authority and boundary |
| --- | --- |
| Camera FrameReceived -> TryDecodeRgb -> PNG -> MCP image content | Camera.cs / EncodedImageDecoder (0x004F2184, 0x004F21AE); existing shipped JPEG decoder reused. StbImageWriteSharp encodes the host PNG; no camera/vision mode changes. |
| Frame timestamp -> head angle, face/cube/charger boxes | Existing RobotStateHistory.At / VisionFrameResult.PoseData; RobotObservedFaceReport and RobotObservedObjectReport. Reports are joined by exact timestamp; no older box substituted. Only scalars/immutable reports retained, bounded to 32 report timestamps. Unavailable head/observations remain null/empty. |
| Action invocation -> existing command -> response.during | Existing bridge command/API dispatch unchanged. Feed cursor captured before dispatch, retained events read after completion. The existing bounded feed reports eviction as gap, rather than claiming a complete history. |
| Clip selection -> robot CA/CB or E2G 0x5F -> feed | Existing AnimationClipInitializedObservation at InitStream 0x0057B674 supplies tag/name. CA/CB are robot start/end; selection alone never emits started. Active/last tag names preserve the playback identity across later selection. Existing LiveNarration uses these same seams. |
| OffTreadsStateChanged -> feed | Sensors.cs public report (engine classification 0x00511E00). Payload keeps from/to strings, InAir and InAir->OnTreads flags; no new physical-state classifier. |
| read_events waitMs -> append signal -> response | Host buffering only. Cursor check and signal registration hold the append lock. Shared asynchronous signal wakes all readers; timeout/cancellation does not cancel other readers; disposal wakes pending waits. 0..30000 ms validation. |
| get_state -> charging and located object/face relative poses | Robot.State.Charging / IsCharging status bit; current-origin BlockWorld objects. Existing Pose3d.GetWithRespectTo(object, MAIN) is queried under engine ownership then world pose gate. Planar hypot and atan2 are host display calculations, not robot control. Root mismatch returns null. |

MCP image content uses type=image, mimeType=image/png, base64 data. Text metadata
also matches structuredContent, following the official MCP 2025-11-25 tools spec:
https://modelcontextprotocol.io/specification/2025-11-25/server/tools#image-content

Cozmo.Bridge now has friend access to existing robot internals so read-only MAIN/
world snapshots use the same ownership and pose gates as writers. No C# control
action, message payload/order, vision algorithm, motion or success result changed.
No fidelity manifest, inventory, PROJECT_STATE.md or SCOPE.md changes.

## Regression and validation

McpServerTests uses its serialized in-process JSON-RPC client for all four features:
PNG decode and annotation pixels, exact-timestamp box exclusion, exposure head,
charging/charger ID and numeric relative distance/bearing, during face/animation/
off-treads events, long-poll wake/timeout/cancellation/concurrent readers and bounds.
The existing complete BridgeCommandTests and BridgeEventTests classes are included.

Focused validation: 47/47 complete bridge/MCP tests pass; fidelity check passes
(763 records unchanged), and git diff --check passes.

Full Windows suite: **8702 passed, 7 failed, 8709 total**, 41.6872 minutes, immutable
compiled snapshot, all 14 private replay cases enabled. All nine MCP cases and
seven charger replay cases pass. The same six fixed-port TransportRepairTests
fail because the operator's live Cozmo MCP owns UDP 47817. The remaining failure
is FaceRecognitionRecordedReplayTests' named 247000..255800 case, the same greeting
timeout reproduced in the preceding charger run and its isolated face rerun.
Those tests do not instantiate this bridge. No failure is suppressed, no timing
limit is changed, and no user MCP process is stopped. A clean Windows full-suite
gate is still required before main.

Local ignored logs: .scratch/llm-senses-focused3.log,
.scratch/llm-senses-fidelity.log, .scratch/m13-senses-full-aabc6dba.log.

Remaining uncertainty: physical client/robot use is for the operator; no new
hardware run was started and no capture bundle is committed.
