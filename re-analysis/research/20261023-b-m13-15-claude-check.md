# Claude check: B-M13-15 (7c45c2c9, status 83ee04e0) — 2026-10-23

Manager review of the production diff (BlockConfigurations.cs, ManipulationSystem.cs, BlockWorld.cs) and the status
report, instructions re-read in libcozmoEngine.so. **PASS.** No record settles (M11-037, M11-063, M13-040, M13-017
stay IMPLEMENTATION_GAP).

## Re-read

- DidAnyObjectsMovePastThreshold 0x00616E10..0x00616EFC: located-ID lookup 0x00616E2A; cache find == end
  (0x00616E40) -> moved and continue (0x00616EC4..C6); IsSameAs (0x00616E88, 5 mm via movt 0x40A0, angle bits
  0x3F060A92) == 1 continues (0x00616E94), else moved and return (0x00616E96 -> 0x00616EF0); missing object walks the
  configuration types (0x00616E98..0x00616EC2), membership -> moved and continue. The C# `DidPendingObjectsMove`
  matches each branch, including the return-versus-continue difference.
- C2: Invalid is the stack's `PoseState.Unknown = 0`; only it erases the cached pose. Matches `cbnz r6` at 0x00616D6C.
- C3 force bypass (0x00616D86..0x00616D9C) and the reset after notify are as built.
- C6: the notify calls the existing `BehaviorPublicStateBroadcaster.UpdateStackHeight` with the tallest height or 0,
  matching 0x0064DCAA..0x0064DD76 (re-read in `20261023-r-m11-config-subscribers-claude-check.md`).
- Production binding: `World.SetObjectPoseChanged` bound at construction; the two immediate `Update()` subscriptions on
  ObjectObserved/PoseStateChanged are removed, so Update runs only at the frame tail (0x0062520C).

Locks: FrameGate then _updateGate in both entry points; dispatch to the audio clients is synchronous on the same
thread under re-entrant monitors; ConfigurationSeen stays outside the locks. No race or deadlock found.

## Queued (non-blocking)

1. The notify ReportMissing still says "complete subscriber/game-message closure remain unbuilt". The subscribers
   are now known (the two BehaviorAudioClients, already in the stack); B-M13-18 rewords it.
2. B-M13-18's step 1 (call UpdateStackHeight) is already done here; B-M13-18 is reduced to the subscriber tests and
   the wording.
