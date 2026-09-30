# B-CORE status

CLAIMED opencode (cozmo-manager) 2026-09-29 13:05

## Scope

Rebuild the connection and device core (M1, M2, M3, M4) from the audit rows. Batches:

1. the NV queue (M3-025, M3-026, M3-027, M3-029, M3-030, M3-031, M3-035, then M3-022 and M3-023);
2. readiness (M1-041, M1-028, M3-033, M3-034);
3. head and lift actions (M4-001, M4-003, M4-016, M4-020, M4-011, M4-025, M4-008, M4-010);
4. the small ones (M2-003, M1-015, M1-024, M1-025, M1-031);
5. R-DEV's own M3/M1/M2 remainder.

Records stay IMPLEMENTATION_GAP; `unresolved` starts "built, awaiting strong verification:".

## Progress log

- 2026-09-29 13:05 CLAIMED.
- 2026-09-29 **Batch 1 (NV queue) done and pushed: dbdc39d.** Read only validates+enqueues; `NvStorage::Update`
  (state 0) pops, sends and arms; a completion sets state 0 only; the deadline uses the synchronised clock
  (`Robot.StoredState`); GetBaseEntryTag's negative branch is `!= 0xC0000000` and positive tags take the size-table
  floor below 0x198000; the header cap is one-shot; the timeout runs the callback only and the final broadcast chunk
  carries the actual result; M3-022 queues the calibration read before SetCameraParams. Verifier PASS (3 queued
  non-blocking nits: one citation fixed, two test-coverage notes). Full suite 1996/1996 (2113/2113 after the rebase).
  Records M3-022/025/026/027/029/030/031/035 remain IMPLEMENTATION_GAP with `unresolved` "built, awaiting strong
  verification".
  - **MISSING (M3-027):** what populates the READ command's data vector `+0xE8` (0x00645386) and its initial value —
    the row names `+0xE8` but not its writer. The command still sends empty data; the test asserts that.
  - **MISSING (M3-023):** what the stack-only `CozmoRobot.StartCamera` must send — the rows settle the engine's
    `EnableColorImages` and the SyncTime `ImageRequest`, but no row defines `StartCamera`. Code unchanged.
- Next: Batch 2 (readiness: M1-041, M1-028, M3-033, M3-034).
- 2026-09-29 **Batch 2 (readiness) done and pushed: ac62741.** The connection-time NV read queue in the engine's
  order (12 constructor reads, CameraCalib, Lab, Needs) so ready-to-stream waits for the whole queue; the
  VisionSystem face-album sink resolves at completion (so it works on the live ConnectAsync-then-VisionSystem order);
  the ImageRequest send result is discarded (M1-041/M4-020); Lab then Needs after SendConnectionResponse (M1-028).
  Verifier: first pass FAIL on the queue-time VisionSystem hook and a dead `ReferenceEquals` cleanup; fixed and
  re-verified PASS. Full suite 2118/2118. Records M1-028/M1-041/M3-033/M3-034/M4-020 remain IMPLEMENTATION_GAP with
  `unresolved` "Built (batch 2)". Missing sinks named: M15 progression/inventory, M12 backup, lab.
  - **Residual MISSING:** whether `ConnectionFaceAlbumResult`/VC+0x2F4 must be cleared when a new connection arms
    #3 (a reconnect could let a new VisionSystem adopt the previous connection's album). Batch 4's RemoveRobot reset
    should cover it.
- Next: Batch 3 (head and lift actions: M4-001, M4-003, M4-016, M4-020, M4-011, M4-025, M4-008, M4-010).
- 2026-09-30 **Batch 3 (head and lift actions) done and pushed: afbb769.** M4-001: `SetHeadAngleAsync`
  rescales the command into (−π, π] first (Radians ctor 0x0084C832 → rescale 0x0084C87C, ceil/loop form) then clips
  against the engine's float bits — min 0xBEDF66F3, max 0x3F46D3F2 (0x00547F44/0x00547FC2), RS6 low 0xBEFA35DD /
  high 0x3F543B67, tolerance 0x3D0EFA35. M4-003: `Motion.RunAsync` takes the action's track lock through the existing
  MovementComponent helpers — `AreAnyTracksLocked(mask)` fails with 0x03000019 (0x00540572..0x0054057C), `LockTracks`
  sends DisableAnimTracks (0x0054058E), the action's end sends EnableAnimTracks (UnlockTracks 0x005408EC); head mask 1
  (0x00547EAC), lift 2 (0x005489EE); the in-position branch takes and releases the lock too. M4-016: strict
  `Radians::IsNear` (<, 0x00548528 → 0x0084CC0A) and the 30.0 s default IAction timeout (0x0052B0C2). M4-011:
  BlockFilter::Init runs from Robot::SetPhysicalRobot(true) (0x0051391E..0x00513954), reached from
  HandleFirmwareVersion, gated on no "sim", via `CozmoEngine.PhysicalRobotSet`; removed from SendAppDefaults. M4-025:
  the +0x490 availability gate is enforced on `CubeDiscovered`. M4-020 and M4-008 are record-text only (no code
  change). Verifier PASS after fixing one blocking finding (M4-020's unresolved claimed "Verified"; restored to
  "built, awaiting strong verification") plus the stale `AreAnyTracksLocked` comment. Full suite 2123/2123 (0 skipped).
  Records M4-001/003/011/016/025 remain IMPLEMENTATION_GAP with `unresolved` "built, awaiting strong verification".
- Next: Batch 4 (the small ones: M2-003, M1-015, M1-024, M1-025, M1-031), then R-DEV's M3/M1/M2 remainder.
- 2026-09-30 **Batch 4a (M2-003, M1-024) done and pushed: 32216ca.** M2-003: `RobotState.LiftAngleRadFromHeight`
  now writes the engine's float bits — `h = (32.0 < heightMm) ? heightMm : 32.0` (NaN → 32, vcmpe 0x005170BC) and
  the ratio `(h-45)/66` unless `h >= 92`, which uses 0x3F364D93 (0x00517104); the old `Math.Max` and `0.712121f`
  (0x3F364D90) are gone; the circular tests are replaced with binary-bit expectations and the NaN/92 edges.
  M1-024: `CozmoEngine.TickAt` calls `NeedsUpdate` between `Handler.ProcessMessages()` and `Robots.Get(1).Update()`
  on `Timer.Seconds` (engine 0x004ED632..0x004ED640), wired from `FreeplayStack.Create`; `FreeplaySystem.Tick` no
  longer calls `Needs.Update`. Verifier PASS (two queued non-blocking notes on M15-001 clock width). Full suite
  2125/2125 (0 skipped). M2-003 and M1-024 remain IMPLEMENTATION_GAP with `unresolved` "built, awaiting strong
  verification".
- 2026-09-30 **Batch 4b in progress (M1-015, M1-025, M1-031).** Extraction (`.scratch/B-CORE-b4/report.md`):
  RemoveRobot's upper-layer reset is the `Robot::~Robot` teardown at 0x0052F2F6 (0x005110D4): `Robot::AbortAll`,
  `FreeplayDataTracker::ForceUpdate`, and the destruction of BehaviourManager (0x0051112C), BehaviourSystemManager
  (0x0051113A), ActionList (0x00511156), VisionComponent (0x0051116C, which also destroys the owned VisionSystem),
  MoodManager (0x005111B6), AIComponent (0x00511276), PathComponent (0x00511284), BlockWorld (0x005112CE),
  MapComponent (0x005114E8), Carrying/Docking (0x00511406/0x00511414), PoseOriginList/TouchSensor/CliffSensor,
  AnimationStreamer (0x0051154A); plus `NeedsManager::OnRobotDisconnected` (0x0052F2E0), `PerfMetric::OnRobotDisconnected`
  (0x0052F2E8), `DASPauseUploadingToServer(0)` (0x0052F2EE), the map/id/RIC erase and the `$session_id`/`$phys`/`$group`
  clears. The engine destroys the whole robot; the stack's `ResetDevices` resets only devices. M1-031: the GoToSleep
  action is `RobotIdleTimeoutComponent::CreateGoToSleepAnimSequence` (0x0052CEA2): a CompoundActionParallel of a
  CompoundActionSequential of TriggerAnimationAction triggers {0xd2, 0xd5, 0xd4} (60.0 s first) plus a
  MoveLiftToHeightAction(preset 0, tol 5.0f); queued by `ActionList::QueueAction` (0x0052CE6A); the only caller is
  the idle Update at 0x0052CE5E. No ActionList/animation-sequence action exists in the stack yet, so the action is
  named as a gap.
- Next: finish batch 4b, then R-DEV's M3/M1/M2 remainder.