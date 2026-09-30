# B-CORE status

CLAIMED opencode (cozmo-manager) 2026-09-29 13:05
DONE opencode (cozmo-manager) 2026-09-30

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
- 2026-09-30 **Batch 4b (M1-015, M1-025, M1-031) done and pushed: 99a51f6.** Record work from the extraction in
  `re-analysis/research/20260930-B-CORE-b4-extraction.md`: two new M1 records — **M1-044** (RemoveRobot's upper-layer
  teardown: `Robot::~Robot` 0x005110D4 destroys behaviour, mood, AI/freeplay, path, map, docking, carrying and vision;
  the stack's built counterparts are the device reset, `VisionSystem.ResetToConstructed` and the NeedsManager
  `OnRobotDisconnected`) and **M1-045** (the idle go-to-sleep action `CreateGoToSleepAnimSequence` 0x0052CEA2, a
  compound of triggers 0xd2/0xd5/0xd4 plus a lift move, queued on the ActionList; named, not built). M1-015, M1-025
  and M1-031 `unresolved` now name them and close the audit's points (the CD21 read is M3-022, the CD16 Lab read is
  M1-028, the engine joins unbounded so the 2 s bound is a stack policy). M1-transport inventory re-approved. No
  behaviour change (one comment). Verifier PASS. Suite 2125/2125.
- Next: R-DEV's own M3/M1/M2 remainder, including the libjpeg 9 grey path (M3-001) if time allows (needs the emulator
  oracle in re-analysis/tools/emu/).
- 2026-09-30 **Batch 5 (M3-010, M1-029, M2-002) done and pushed: bee7266.** M3-010: the segment table is the engine's
  128 bytes ([0]=0,[1]=1,[2..3]=2,[4..7]=3,[8..15]=4,[16..31]=5,[32..63]=6,[64..127]=7), the scaling literal is
  0x46FFFE00 (32767.0f) in single precision with the truncating cast, the lower clamp tests the original input, and
  the NaN path now logs the engine's `sWarningF` on the live `AnimationScheduler.PopFrame` path. M1-029: the jsoncpp
  reader now reproduces the default Features (comments accepted, trailing commas and numeric keys rejected, any root
  type, depth 1000), the accessor sequence, and `asUInt` (null 0, bool 0/1, string/array/object and a negative or
  > 4294967295.0 real throw `JsonLogicError`, a real in [0, 4294967295.0] truncates). M2-002: rows 12a..12k checked,
  no consumer wrong; two tests pin 0x4 and 0x8/0x20. Verifier PASS after fixing one blocking finding (the asUInt real
  bound was 2^32, must be strictly > 4294967295.0 per 0x008E8E38). Full suite 2134/2134 (0 skipped). Records
  M3-010/M1-029/M2-002 remain IMPLEMENTATION_GAP with `unresolved` "built, awaiting strong verification".
- **Remaining R-DEV M3/M1/M2 scope:** M3-001 (the libjpeg 9 grey path and the raw/encoding cases; needs the emulator
  oracle in re-analysis/tools/emu/), M3-018 (the RGB dispatch for encodings other than 9; IsColor of 0; the codec),
  M3-021 (vision_config.json load failure, initial exposure 16, pending params in VisionSystem::Update, M11),
  M3-013 (the audio-animation readiness is the M6 stand-in) and M3-032 (the NV dispatch gate). The M4 remainder
  (M4-009/017/018/019) is M11/M12/M7-dependent. Pre-extraction rows: Part 1 items 2 and 3 (M3-001/M3-018), item 4
  (M3-021); Part 2 items 5..10 (M4); re-analysis/research/20260929-R-DEV-pre-extraction.md.
- 2026-09-30 **Batch 6 (M3-001, M3-018) done and pushed: 23c65ac.** The image-decode dispatch for every encoding value.
  M3-001 (gray `DecodeImageHelper<Image>` 0x004F287C, tbh 0x004F2898): 1 copies `rows*cols` with no length check, 2
  converts with cvtColor code 7 `(4899R + 9617G + 1868B + 8192) >> 14` (coefficients 0xE2AB0), 5/6 imdecode(gray), 7
  plus the 160-column border, 8 the reconstructed gray JPEG, 9 the half-width colour JPEG to gray then INTER_LINEAR,
  and 0/3/4/10..255 the `EncodedImage.DecodeImageRGB.UnsupportedEncoding` default (the literal says RGB even in the gray
  helper; the gray tail's BadDecode string is the RGB one too, 0x004F2CF6 -> 0xBE497F). M3-018 (RGB
  `DecodeImageHelper<ImageRGB>` 0x004F2184, tbh 0x004F21AE): 1 replicates gray to BGR, 2 a straight copy, 5/6/7/8/9
  imdecode + BGR2RGB, 7 borders, 9 resizes, 0/3/4/10..255 the default; `IsColor(0)` logs
  `EncodedImage.IsColor.UnsupportedImageEncoding` with `EnumToString(0) = "NoneImageEncoding"` (pointer table
  0x01034A60) and returns false. New forced policy **M3-037** (SD2): a short raw payload's undefined heap read is
  zero-filled, a long one truncated. Contradicted record text fixed: case 1 is not a length check, and the
  out-of-table encodings share the 3/4 error path. Verifier: first pass FAIL on two diagnostic strings (gray BadDecode
  event name, EnumToString(0)); both fixed and re-verified PASS. Full suite 2144/2144 (0 skipped). M3-001/M3-018 stay
  IMPLEMENTATION_GAP with `unresolved` "built, awaiting strong verification"; the residual is the JPEG entropy decode
  (StbImageSharp in place of OpenCV's libjpeg 9, Part 1 item 3, needs the emulator oracle).
- Next: M3-021 (vision_config load, IsInitialized, initial exposure), M3-032 (the NV dispatch gate), then the libjpeg 9
  grey path if the emulator oracle is built.

## DONE (2026-09-30)

All six batches are built, verified and pushed; the remaining scope is cross-layer (SD3) or needs a new oracle.

**Commits:** dbdc39d (batch 1, NV queue), ac62741 (batch 2, readiness), afbb769 (batch 3, head/lift), 32216ca (batch 4a),
99a51f6 (batch 4b), bee7266 (batch 5), 23c65ac (batch 6, M3-001/M3-018 + M3-037), plus the status commits. Full suite
2144/2144 at batch 6.

**Records built (all still IMPLEMENTATION_GAP, `unresolved` "built, awaiting strong verification"):** M3-022,
M3-025..M3-031, M3-035, M1-028, M1-041, M3-033, M3-034, M4-020, M4-001, M4-003, M4-011, M4-016, M4-025, M2-003,
M1-024, M3-010, M1-029, M2-002, M3-001, M3-018. New records: M1-044, M1-045 (batch 4b) and M3-037 (batch 6, a forced
COMPATIBILITY_POLICY under SD2). A strong verifier settles them.

**What is left, and why (SD3, named in the records' `unresolved`):**
- **M3-001/M3-018 residual:** the JPEG entropy decode is StbImageSharp in place of OpenCV's libjpeg 9 (Part 1 item 3).
  A bit-exact port needs an emulator oracle for libjpeg 9 (idct_islow, h2v1_fancy_upsample, ycc_rgb_convert) in
  `re-analysis/tools/emu/`, which does not exist yet; that is its own extraction/port job.
- **M3-013:** the audio-animation readiness sink is M6's live voice/mixer (B-M6b-4).
- **M3-021:** the vision_config.json load and IsInitialized failure behaviour belong to M11's VisionSystem::Init.
- **M3-032:** Gate A and the SyncTimeAck watchdog are built; Gate B needs M11's UpdateAllResults.
- **M4-009/017/018/019:** M11/M12/M7-dependent (R-DEV's triage).

No robot run is needed from this job. The manager verifies and settles the built records.