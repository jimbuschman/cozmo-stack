# The "LiveIdleTurn" eye shift outlives live idle (2026-10-20)

Question from hardware run `20261020-200624-M5-HW-97161e`: after the harness's live idle ended, the face layer list
held only "LiveIdleTurn" for the rest of the run, and KEEP_ALIVE (persistent face) and LAYERS could not start.

**Answer: the original does the same. Not a stack bug; the harness preconditions were wrong.**
Extractor pass (Claude cozmo-extractor), manager spot-checked the starred rows against the `.so`.

| Fact | Citation |
| --- | --- |
| The live eye-shift tag is a byte at streamer+0x1C4; only 4 accesses exist: constructor zero, add, read and clear on remove | 0x0057A044, 0x0057D7EE, 0x0057D8C6, 0x0057D8D6 |
| UpdateLiveAnimation returns at once when the live flag +0x194 is clear, so nothing removes the layer after live idle ends | 0x0057D606..0x0057D60C |
| \* The only removal: a STRAIGHT body shuffle inside a live tick, RemoveEyeShift(tag, 0) then tag = 0 | 0x0057D8C6..0x0057D8D6 |
| \* InitStream of a non-live clip calls only RemoveKeepFaceAlive(99), no eye-shift removal | 0x0057B7EC..0x0057B7F8 |
| The layer is persistent (AddPersistentLayer); 33 ms is the ramp-in keyframe time, not a lifetime | 0x0064F412..0x0064F476 |
| Persistent non-empty layers are rewound and held, never erased by ApplyLayersToFrame | 0x0058E66E..0x0058E69E |
| HaveLayersToSend = face layer map non-empty, so it stays true while the layer exists | 0x0064F4FA..0x0064F518, 0x0058ED18 |
| \* KeepFaceAlive adds a dart only when there are no layers, or the only layer is its own dart, so no dart while LiveIdleTurn lingers | 0x0058D3B2..0x0058D3C4 |

Other RemoveEyeShift/RemovePersistentLayer callers (TurnInPlace 0x545BBC/0x5465C2, MoveHeadToAngle
0x5484AA/0x54871E, ITrackAction 0x5647D6, MovementComponent 0x63E37A, RemoveKeepFaceAlive 0x0058CFB8) use their own
tags, never streamer+0x1C4.

The stack matches: `AnimationScheduler.cs` lines 1406, 1424, 1429; `TrackLayers.cs` lines 31, 296-299.
Records M5-028, M5-029, M5-030 are consistent; M7-010's `unresolved` already describes the linger.

**Consequence for the M5 harness:** after live idle, whether the face drains is random (it depends on whether the last
body shuffle was a turn; straight fraction 0.5). KEEP_ALIVE's persistent-face window and LAYERS must run before any
live idle, or record the lingering layer as the engine-faithful state instead of failing their precondition.
