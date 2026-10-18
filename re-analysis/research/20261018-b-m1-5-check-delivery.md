# Opus fix review of B-M1-5 (fd73bc88, 06b86940): delivery side, 2026-10-18

- **Checked:** the verifier that failed B-DELIV-1 (`research/20261017-b-deliv-1-check.md`), resumed and scoped to its
  findings 1–6, the census and delivery changes. Read at 14ae481e.
- **Verdict: FAIL.** Two blocking (1, 2), five queued. Cases A and B fixed through the real tick; internal consumers
  synchronous and in their original order; every widened site reopened is at a native Broadcast point.
- **Gates:** `fidelity.py --check` exit 0; focused 62/62. Manifest: 29 records, strict appends only.

## Findings

1. **BLOCKING (omitted): host-facing E2G broadcasts still inline,** under `_tickLock`, overtaking earlier queued reports.
   - `Sensors.cs`:663 `OnChargerPlatformChanged` (doc :353 "RobotOnChargerPlatformEvent (C8 P1)"). Native
     Robot::SetOnChargerPlatform 0x00511D44 builds the message at 0x00511D7A, external call 0x00511D8A. Reached from
     RobotState (SetOnCharger rising edge, or OffTreads ClearOnChargerPlatform) inside ProcessMessages. Internal
     subscriber `FreeplayStack.cs`:286 needs the internal/public split.
   - `Sensors.cs`:946 `MotorCalibrationReported`. Native HandleMotorCalibration 0x00536A68 builds MotorCalibration at
     0x00536C06, Robot::Broadcast 0x00536C0E. Shares one delegate with `OffTreadsBehaviors.cs`:227 and
     `ReactionStrategies.cs`:429. The status file set "calibration hooks" aside as not E2G with no decision.
   - `Vision/VisionSystem.cs`:586 `EnrolledFacesErased`, :591 `LoadedFaceName` (doc :519-522 "replacement for
     BroadcastLoadedNamesAndIDs's EngineToGame broadcasts (F27)"). Native 0x00651578: RobotErasedAllEnrolledFaces
     (0x00651594 → Robot::Broadcast 0x0065159C), then LoadedKnownFace (0x0065160A → 0x00651612). Live via the
     connection-time NV album read (`CozmoEngine.cs`:829 → ConnectionFaceAlbumLoaded → AdoptFaceAlbum,
     `VisionSystem.cs`:630) inside ProcessMessages.
   - None is in the census as built or MISSING.

2. **BLOCKING (race, lost wakeup).** Same as `20261018-b-m1-5-check-readiness.md` R1: `EngineReadiness.cs`:66-70
   captures EngineStateChanged subscribers at 7B(3) publication; `_hostReady` turns true at dispatch; ConnectAsync
   (`CozmoRobot.cs`:513-527) subscribes after Create started the runner and checks IsReady once. A subscription after
   capture and a check before the drain leaves `ready.Task` incomplete forever. BeforeStart exists but ConnectAsync
   doesn't use it.

3. **QUEUED (visibility): RobotCliffEventFinished (M7-019).** The host view is supported: SendFinishedReactToCliffMessage
   0x006052BC builds it at 0x006052D2, Robot::Broadcast 0x006052DA; Size() 0x00711F76 is `movs r0,#0` (empty). But the
   removed ReportMissing (old :314) was the only visible sign that the stack has no local E2G emit for this tag
   (native Broadcast also emits locally, 0x00662590). Restore the gap on M7-019 `unresolved`.

4. **QUEUED: ActionCompleted payload.** `ActionCompletionReport.cs`:3 publishes the runner cache
   (`IActionRunner.cs`:267), labelled as such and noted on M8-020. Queue point right (D6 after WatcherEnding,
   `ActionQueue.cs`:258-260). `ReportOrderingRegressionTests.cs`:50 doesn't check its FIFO position.

5. **QUEUED: API identity.** CubeTapped/CubeMoved/TapBroadcast/MovementBroadcast now deliver `Cube.SnapshotForApp()`
   clones (`Cubes.cs`:477-481, :786-790), not the live Cube; undocumented. ConnectionChanged and CubeDiscovered still
   pass live objects inline (~:776), outside the FIFO order (not claimed as E2G).

6. **QUEUED: public local seam.** `AnimationScheduler.AnimationAborted` (:556-559, synchronous, under `_gate`) is
   reachable through public `CozmoAnimations.Scheduler` (`CozmoAnimations.cs`:269). Make internal or document.

7. **QUEUED: counter.** `QueueAppBroadcast` counts unconditionally; native Robot::Broadcast 0x00511DE8 skips when
   context+4 is null (cbz 0x00511DEC). No production reader; nothing observable.

## Checked and supported

- Case A: `Sensors.cs`:907-911 Cancel(-1) → 0x5F queued → RobotStopped queued → RobotStoppedInternal; native Cancel
  0x0053546E then Broadcast 0x00535480; `ReportOrderingRegressionTests.cs`:40-51 production, `["5F","stopped"]`, 0x8D.
- Case B: `TriggerLiftSafeAnimationAction.cs`:103 0x60, then `Cubes.cs`:786-787 tap; test :54-64 `["60","tap"]`.
- Sites reopened: CliffEvent 0x005356DA/0x005356E2; FallingStarted 0x00534F96/0x00534F9E; FallingStopped
  (RegisterNeedsActionCompleted 0x005350C2 before 0x00535194/0x0053519C); ChargerEvent 0x00511BFA/0x00511C0A and
  0x00511AB4/0x00511AC4; OffTreadsStateChanged 0x00512098/0x005120AA before SetOnChargerPlatform 0x00512192;
  UnexpectedMovement 0x0063E946/0x0063E94E; CurrentCameraParams after SendMessage 0x00656166, 0x0065619C/0x006561A4;
  MoodState 0x0067B78E/0x0067B796; NeedsState 0x006939DA/0x006939E2. ConnectionState now queues before the local hub
  (corrects the old order). NV, backup and power rest on earlier checked rows.
- Host/internal split: every pre-fix engine subscriber moved to an `*Internal` hook firing right after the enqueue
  (CliffPickup :90-92, OffTreadsBehaviors :225-226, ReactionStrategies :412/:445/:464/:480, ReactiveBehavior
  :87/:89/:92, FreeplayStack :334). No engine behaviour deferred.
- One count per broadcast; aliases use QueueAppEvent. Old finding 2 relabelled unit; notes 4/5 appended on M1-024.
