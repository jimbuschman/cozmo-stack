# Opus fix review #2 of B-M1-6 (86d287d6, record 2bce3a23): delivery side, 2026-10-18

- **Checked:** the delivery verifier, resumed, scoped to its B-M1-5 delivery findings 1 and 3–7, B-M1-6 items 2 and
  5–9, and the public-event census table.
- **Verdict: FAIL.** One blocking finding (in the census table), three queued. The three sites from finding 1 are
  right; M7-019 and items 6–9 supported; no host loses BlockPoolData.
- **Gates:** `fidelity.py --check` exit 0; focused 449/449. Manifest: 11 records, strict appends only.

## Findings

1. **BLOCKING: census rows misclassified as "not a report"; two native host reports with no visible gap.** Each event
   below is the stack's only counterpart of a native E2G broadcast, still raised inline under `_tickLock` (the class of
   ObjectObserved/FaceObserved, correctly MISSING).
   - `Vision/Faces.cs`:293 `FaceIdChanged` (raised :373). Native FaceWorld::ChangeFaceID builds
     RobotChangedObservedFaceID at 0x004F3E5C; local subscribers TrackFaceAction 0x005660B6, BehaviorInteractWithFaces
     0x005C2A48, BehaviorEnrollFace 0x005FF276. M14-008's `unresolved` calls FaceIdChanged an observable event (the
     M11-038 precedent) and its effect says "visible to behaviors and the game": the row contradicts its record. No
     record names RobotChangedObservedFaceID.
   - `Vision/Faces.cs`:501 `PetObserved` (raised :515). Native PetWorld::Update builds RobotObservedPet at 0x0050BA60.
     "No checked row" is MISSING by the job's definition; no record names it.
   - `Vision/BlockWorld.cs`:762 `LocatedObjectDeleted` (raise :2209, commented "RobotDeletedLocatedObject, uint32/Size 4
     API view"). Native BlockWorld::DeleteLocatedObjects builds RobotDeletedLocatedObject at 0x0061D5DC. No record cites
     it (M12-034 mentions it only as a dock action's local subscription).
   - `Vision/BlockWorld.cs`:1096 `PoseStateChanged` (doc :1094-1095 "consumer channel for the pose-state broadcasts";
     :2171 "pose-changed broadcast at 0x0061D5C8"); native also RobotMarkedObjectPoseUnknown at 0x005073B8. Visible in
     M11-038/M11-042, so least serious; still MISSING, not "not a report".

2. **QUEUED: "not a report" rows should name the separate native report.** `Behavior/BehaviorManager.cs`:96 Selected,
   :156 ReactionTriggered (BehaviorTransition 0x005A2580, ReactionTriggerTransition 0x005A2F0E; gap visible at
   BehaviorManager.cs:779-792, M8-019); `Camera.cs`:281 FrameReceived, `VisionSystem.cs`:462 FrameProcessed (ImageChunk,
   RobotProcessedImage; M11-035, M13-020).

3. **QUEUED, pre-existing: motor calibration flag order.** Native: detach 0x00536BC4, SetHeadCalibrated 0x00536BDA,
   SetLiftCalibrated 0x00536BF0, Broadcast 0x00536C0E. Stack sets the flags in `CozmoRobot.cs`:108 before Sensors'
   detach; the comment at `Sensors.cs`:956 names the wrong source. Host order is right; whether detach reads the flags
   is unchecked.

4. **QUEUED: native count.** Item 9 documented; nothing further.

## Checked and supported

- Charger platform `Sensors.cs`:653-668 vs 0x00511D44..0x00511DB0: store 0x00511D6A, gate 0x00511D70, send
  0x00511D8A, threshold 0x00511DA0 (50/400), SetFreeplayPauseFlag(+0x34A,3) 0x00511DB0. Stack matches; the FreeplayStack
  hook (:287) now follows the threshold send (corrects the old order).
- Motor calibration `Sensors.cs`:948-962 vs 0x00536A68..0x00536C18: detach before queue; three copied fields
  (0x00536BF4..0x00536BFE); AutoCalibrationStarted alias uncounted; internal hooks after the queue.
- Loaded faces `VisionSystem.cs`:585-596 vs 0x00651578..0x00651632: erase queued at 0x0065159C then internal hook; per
  face log, queue at 0x00651612, internal hook; value copies.
- M7-019 `CliffPickupBehaviors.cs`:311-318: empty host event at 0x006052D2/0x006052DA (Size 0, 0x00711F76) before the
  synchronous seam; local-emit gap 0x00662590 visible in code and on M7-019.
- BlockPoolData: only subscriber of the now-internal event is the forwarder `Cubes.cs`:192 into the public copied
  `CozmoEngine.BlockPoolData`; no host loses a report.
- Item 6 `["5F","completed","stopped"]` through the real queue/tick (`ReportOrderingRegressionTests.cs`:48); item 7
  docs `Cubes.cs`:216-226; item 8 internal (`AnimationScheduler.cs`:559).
- New production test `CalibrationAndChargerPlatformFollowEarlierReportsWhileInternalHooksStaySynchronous`: packets
  through the real tick, expected values from inputs. Face-replay test honestly `unit`.
- Rest of the census spot-checked against the 156 native E2G types (`MessageEngineToGame(X&&)` constructors). Other
  native types without a public stack event (ObjectTappedFiltered, CarryStateUpdate, FaceEnrollmentCompleted, …) are
  absent rather than misclassified.
