# Opus fix review #3 of B-M1-7 (add63a25, record 2f7bd2aa): delivery side, 2026-10-18

- **Checked:** the delivery verifier, resumed, scoped to its B-M1-6 findings 1–3 (B-M1-7 items 3–5) and a final pass
  over the whole census table.
- **Verdict: FAIL** (one census row). Closed by the manager as records work (below), with no builder round: no code
  changes.
- **Gates:** `fidelity.py --check` exit 0; focused 66/66. Manifest: 9 records, strict appends.

## Findings

1. **BLOCKING: `Behavior/Needs.cs`:828 `SparksRewardAwarded` marked "not a report".** Its own doc (:832-834) says
   SparksRewardCommunicatedToUser sends FreeplaySparksAwarded with the amount at +0x3DC. Native 0x00696E64: store 0 at
   +0x3D8 (0x00696E78), build FreeplaySparksAwarded from +0x3DC (0x00696E82..0x00696E88), E2G Broadcast via external
   interface vtable +0x1C (0x00696E90). No record named it. The stack event has no payload, and returns early when
   `!SparksRewardPending` (:841) where the native body has no flag test (callers 0x005B3548, 0x005DAF7C,
   RegisterNeedsActionCompleted unchecked). Queued uncounted (not observable).
   **Closed:** new M15-071 (RECOVERABLE_GAP); census row corrected in `jobs/status/B-M1-7.md`.
2. **QUEUED: wrong owners on the two vision rows (corrects the verifier's own B-M1-6 finding 2).** RobotProcessedImage
   producer 0x00654A14/Broadcast 0x00654A1C is owned by M11-035 (evidence "builds RobotProcessedImage
   (0x006549A2..0x00654A22)"), not M13-020 (which only subscribes to tag 0x43). ImageChunk E2G producers:
   HandleImageChunk Set_ImageChunk 0x00535AD4, CompressAndSendImage<u8> 0x0064F9F2 and <RGB> 0x0064FEA6; no record
   covered them (M3-002 is reassembly only). **Closed:** new M11-096 (RECOVERABLE_GAP); M11-035 note appended.
3. **QUEUED: M11-062 scope.** Its evidence includes DeleteLocatedObjects 0x0061D0A6..0x0061D602, so it covers
   0x0061D5DC, but its scope is the cliff path; general host delivery of RobotDeletedLocatedObject is to be scoped when
   M11 is worked. Note accurate as written.

## Checked and supported

- Native sites disassembled: RobotChangedObservedFaceID 0x004F3E5C/0x004F3E64; RobotObservedPet 0x0050BA60/0x0050BA68;
  RobotDeletedLocatedObject 0x0061D5DC (after BroadcastObjectPoseChanged 0x0061D5C8)/0x0061D5E4;
  RobotMarkedObjectPoseUnknown 0x005073B8/0x005073C0.
- Owners: M14-008 (authority names ChangeFaceID 0x004F3DB4; effect "visible to behaviors and the game"); M14-004 does
  not own PetWorld::Update output (now M14-014); M11-062; M11-038/M11-042. Notes accurate.
- BehaviorManager rows: BehaviorTransition 0x005A2580, ReactionTriggerTransition 0x005A2F0E; M8-019/M8-001 carry them;
  diagnostic at BehaviorManager.cs:779-792 present.
- Calibration: the built-in detach (`ManipulationSystem.cs`:57-60 → `Docking.cs`:308-332 → Place → UnsetCarrying →
  DeleteLocatedObjects) reads no calibration flag; the flags' only readers are off that path (CozmoRobot.cs:67/611-635,
  Motion.cs:1463, OffTreads.cs:207, OffTreadsBehaviors.cs:227/543, SteppedBehavior.cs:1152). No observable effect.
- Rest of the census against the 156 native E2G types: remaining "not a report" rows have no native counterpart or no
  public stack event (CarryStateUpdate/BlockPickedUp have no E2G constructor; CameraCalibration E2G at 0x00655526
  differs from the NV CalibrationInstalled hook; ObjectAvailable never fires; RobotDelocalized's host report is 0x32).
- Source diff: only the readiness fences, the test-only first-tick hook (null in production, exchanged once) and the
  comment.
