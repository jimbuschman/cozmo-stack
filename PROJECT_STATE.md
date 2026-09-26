# Project state

Read first in every session. The manager keeps this file current; the process it follows is the Process section of `AGENTS.md`.

## Now

- **M6 (Wwise) implementation in progress — see `re-analysis/M6-STATUS.md` for the full resume record.**
  - Committed on `main`: `7842392` (Batch 1: M6-001, M6-005, M6-018 + the shipped-bank verification
    gap fix), `3827137` (M6-009 part: RTPC curve shapes and scaling), `8b64904` (M6-007 selection).
  - Uncommitted and **unverified**: M6-006 (`WwiseAction.cs`, `WwiseEventRuntime.cs`,
    `WwiseEventRuntimeTests.cs`). Verify before committing.
  - The STMG middle-section layout recovered from native `0x9B0B14` is recorded in `M6-STATUS.md` §7
    and must go back to extraction for a new approval before it is treated as settled.
  - Remaining: M6-006 verify/commit, M6-008, M6-017, the rest of M6-009, then Batches 3–5.
- **PAUSED 2026-09-25 (operator: usage limit).** Three background workers were stopped mid-task:
  - **M10 repair implementer:** partial, uncommitted edits in the working tree (Behavior/*, OffTreads.cs, UnexpectedMovement.cs, Sensors.cs, Motion.cs, CozmoEngine.cs, Conformance/Reactions.cs, tests). Resume it, or re-run it on top of those edits. Do not commit as-is.
  - **M11 extraction:** stopped while writing scratch report `scratchpad/extract/M11/`; re-run.
  - **NV gap pass 3** (the non-factory read path and the other 14 reads' callbacks): re-run. The NV-gap and NV-gap2 reports are saved in the scratchpad.
  - Committed so far today: cf313f8 (M5), fee8724 (M6/M10 frozen), 41b319c (control-check keep-alive). Nothing is waiting on the operator until the NV fix is in.
- **Phase:** M1 is closed apart from named residuals.
  - Batch 3 (the app layer) is 518b730, and the settle pass is 67d4bc2.
  - The device reset on RemoveRobot (M1-025, M1-015) is committed after the commits below. Its first verification failed on two races and on the calibration being kept. Those were fixed, and the re-verify passed.
  - M1 records: 28 EXACT, 1 EQUIVALENT, 3 IMPL_GAP, 9 POLICY, 2 HARDWARE_ONLY.
  - control-check is on main as 4b89c79 plus abea3ce. Its review returned NOT READY: the STATE rate window, ANIM driving without `--allow-drive`, and the OBB preflight. All of these are fixed.
  - The full suite passed 1163/1163 before the commit.
- **Waiting on the operator:**
  - one control-check robot run (see "Next").
- **M3 device: repaired in one batch (2026-09-24).**
  - **Verification:** the verifier ran three passes. The first found 4 blocking items (the trailing silence, circular tests, cancel dropping the buffer, the face-hold interval). The second found 2 more (the live-stream cancel drop and the M5-023 text); the third, on the R1 fix, failed only on its test oracle. All were fixed.
  - **M3 records:** 12 EXACT_SOURCE, 2 forced COMPATIBILITY_POLICY (M3-019, M3-020), 2 HARDWARE_ONLY, and 8 IMPLEMENTATION_GAP, each with a named residual. Most residuals belong to M5.
  - **What changed:**
    - the engine's audio/animation feed (14-frame and byte budgets, FIFO drain, engine-tick driven);
    - the face encoder on the 64×128 canvas;
    - chunks ignored before SyncTimeAck; 3 frames per tick to vision;
    - at connection: SetCameraParams, the NV calibration read that enables vision, and DefaultCameraParams handling.
  - Full suite 1243/1243.
- **M4 control: repaired in one batch (2026-09-25).**
  - **Verification:** three verifier passes, and every blocking item was fixed. The verifier's disassembly also added cited corrections C1..C9, re-approved.
  - **Settled EXACT_SOURCE:** M4-001, 002, 007, 014, 015, 020, 022, 023, plus M2-015 and M1-041. The rest are IMPLEMENTATION_GAP, each with a named residual; most residuals belong to M11 or M12.
  - **What changed:**
    - At SyncTime, AbsoluteLocalizationUpdate {0, frame 0, origin 1}.
    - RobotState acceptance: the pre-origin part is applied for every synced state; the pose and history only for origin 1.
    - Direct-drive track locks (0x9D/0x9E).
    - StopAll sends only 0x3B.
    - Backpack lights: Off every tick, plus the charging configs.
    - Cube WakeUp lights on connect and reconnect.
    - The cliff threshold schedule, the charger platform, PotentialCliff.
    - Tap filtering.
    - Head/lift completion as the engine's actions: no send when in position, success means in position and stopped.
    - App head/lift speeds of 10/20.
  - Full suite 1279/1279.
- **M5 animation: repaired in one batch (2026-09-25).**
  - **Verification:** four verifier passes; every blocking item was fixed (B1..B9, then O1..O3). The verifier's disassembly added cited corrections C1..C4 and Appendix E, re-approved under the standing authorisation.
  - **M5 records:** 18 EXACT_SOURCE, 1 EQUIVALENT_IMPLEMENTATION (M5-003, .NET MathF against bionic libm in the last ULP), 1 COMPATIBILITY_POLICY (M5-020), 1 HARDWARE_ONLY (M5-036), and 15 IMPLEMENTATION_GAP, each with a named residual (mostly M6 audio, M7 mood/degree and M8 behaviour interfaces).
  - **What changed:**
    - the engine's AnimationStreamer port: streaming/idle/live selection, the single idle/live tail (InitStream with tag 255), the keep-alive face stream (a FaceImage every 33 ms, one Start, no End), the neutral-face replay after a cancel, AbortAnimation 0x8D;
    - track layers (face, eye, backpack, audio) with GetFaceHelper, Interpolate, Clip and Combine;
    - the ProceduralFace default and SetFromFlatBuf rules; the renderer on the engine's OpenCV 3.1.0 paths (OpenCv310.cs);
    - the ScanlineDistorter (mt19937 seeded 1), entropy-seeded mt19937 elsewhere (EngineRandom.cs);
    - JSON clips (JsonClipLoader.cs).
  - Full suite 1357/1357.
- **M6 (Wwise) and M10 (derived state): inventories frozen (2026-09-25).** M6 has 18 records (`re-analysis/inventory/M6-wwise-bank.md`, eight extractor passes) and M10 has 13 (`re-analysis/inventory/M10-derived.md`, three passes). All are IMPLEMENTATION_GAP until the compare-and-repair batch. M6's repair may run as two batches: the control and signal path, then the Vorbis port.
- **NV calibration read (found on the second CONTROL run):** the engine sends Length = 1 for the factory tag 0x80000001 (`_maxFactoryEntrySizeTable`), not 1024, and reassembles each reply at index x 1024. M11-011 is contradicted. Two gap passes then found the read is not alone. At connection the engine queues **12 NV reads before the calibration read** (unlocks, inventory, the face album, and 8 backup reads from backup_config.json), then Lab and Needs after it. Ready-to-stream (+0x2A, M1-041 CD20) waits until the whole queue drains. So the stack needs the engine's NV storage component, not just a calibration reader. Other findings:
  - NV dispatch is gated in Robot::Update (Running, the first full state; after a calibration, a BlockWorld failure).
  - robot+0x24 is mfgId word 1, the body hardware version. The operator's robot reports 4, so the engine zeroes all 8 distortion coefficients.
  - DefaultCameraParams never installs a calibration.
  - A read gets no callback on disconnect.
  - A third gap pass on the non-factory read path and the 14 other reads' callbacks is running. After it, the NV records go into M3 as correction C2 (the NV component is device storage). The M2 word@4 "index" rename is a naming cleanup, queued.
- **M2 protocol: repaired in one batch (2026-09-24).**
  - The verifier compared all 42 outbound and all 56 inbound layouts by machine against the engine; all match. Its one blocking item was a manifest overclaim, fixed by policy M2-017 and corrections C1 and C2, then re-approved.
  - M2 records: 15 EXACT_SOURCE, 1 COMPATIBILITY_POLICY (M2-017), 2 IMPLEMENTATION_GAP (M2-002 per-bit storage and M2-015 caller defaults, both M4 interfaces).
  - M1-027 is settled EXACT_SOURCE, so M1 has two IMPLEMENTATION_GAPs left: 029 and 041. It is `re-analysis/inventory/M2-protocol.md`.
  - **Extraction:** three read-only passes (OUT, 42 messages; IN, 56 codecs plus dispatch; the ImageChunk gap).
  - **Records:** M2-001..M2-016. All are IMPLEMENTATION_GAP until the comparison against the code. The seven earlier EXACT_SOURCE claims rested on weak evidence. No RECOVERABLE_GAP remains.
  - **Two contradictions of the current code, confirmed by the manager in the disassembly:**
    - M2-013: the FallingStopped codec reads the timestamp as the duration.
    - M2-014: BlockStatus 1 and 2 are swapped in Docking.cs.
  - The inbound dispatch also settles M1-027's residual interface (M2-010).
  - The comparison and repair run as one batch (operator: "do not expand M2 into another M1-style process unless the comparison shows that it is substantially wrong").
- **Operator decisions:** D1-D4 approved 2026-09-23. On 2026-09-24: the inventory, D5, D6 (fatal behaviour recorded, isolation kept as policy), D7, the corrections C1..C5 and D8 (stop processing a frame after a DisconnectRequest, M1-038) approved; the three repair batches are authorised to run without further checkpoints.
- **Batch 1 done (7aba301). Batch 2a done** (e6f2ed7; receive path: B17 truncation, receive-error counters, partial-header overrun after the header, M1-038); records it repaired are settled in one verified pass at the end of batch 2. **Batch 2b-i done** (8c9f405; clock, construction-time 2 ms scheduler + FIFO executor, posted sends, RobotLink isolation; three verification passes). **Batch 2b-ii done** (0691429; per-address connections, type-3 and timeout delete only that connection, timed-out flag, inbound creation, FinishConnection, posted Start/Stop, per-connection multipart, unconditional Dispose disconnect; two verification passes). **Batch 2c done** (socket B10/B11/B12/B16/B38 with C1's 47817 reopen, the M1-023 reset mechanism, the M1-037 host trigger, M1-001 addressing; verifier PASS on the second pass). Batch 2 complete.
- **Next: the operator's second control-check run (ready 2026-09-25, main 7297809: M2 + M3 + M4 + the control-check update).**
  1. In the Claude session: `! git push origin main`.
  2. On the Cozmo machine, from `cozmo-stack`: `git pull`, then `dotnet run --project src/Cozmo.Conformance -- control-check 172.31.1.1 --obb "<the unpacked OBB dir>" --allow-drive`.
  3. Setup:
     - Cozmo **on the floor**, not a table: after a PotentialCliff the engine turns stop-on-cliff off (M4-019 SC7);
     - off the charger, with about 10 cm clear in front and behind;
     - one cube powered and within about 30 cm.
  4. Copy the `re-analysis/acceptance/hardware/<stamp>-CONTROL/` folder to Downloads.
  5. **What this run answers:**
     - whether the robot echoes origin 1 (M4-021);
     - whether DefaultCameraParams arrives after SetCameraParams{0,0,true} (M3-019/021), and the image brightness;
     - whether cube telemetry arrives after the WakeUp lights (M4-024);
     - the new stream budget with FACE/AUDIO/ANIM;
     - the track locks and the backpack light rate.
  6. M5 (animation) is not in this run; its batch is in progress.

- **Standing authorisations and current plan (operator, 2026-09-24, supersedes the governing plan below where they differ):**
  - **Pushing:** the manager may push to GitHub `origin main` whenever a robot run is ready, so the operator's Cozmo machine can pull it.
  - **Inventories (operator, 2026-09-24, M2):**
    - Source-derived inventories and ordinary source-fidelity decisions need no operator approval. The manager decides them, documents them, freezes the inventory and proceeds.
    - Only two things go to the operator: a deliberate divergence from the engine (a policy), and a source question that is still unresolved and materially affects robot behaviour.
    - No M1-style multi-round process unless a comparison shows a layer is substantially wrong.
  - **Inventory corrections:** the manager may approve them without an operator checkpoint, including running `--approve`. Each stays cited and is recorded here. Genuine policy choices still go to the operator.
  - **Hardware-first plan:**
    1. finish the M1 link check;
    2. convert the direct-control hardware checks to self-judging PASS/FAIL (connect, head/lift/drive, face, audio, animation, cube connection, camera, the animation-stream edge cases);
    3. the operator runs them once;
    4. the manager fixes the failures from the source, including batch 3, the handshake;
    5. one confirming run.
  - **Operator involvement:** two or three robot runs, no design questions. Time box about 2-3 working days to M1 closed and the direct-control basics passing on hardware; otherwise stop and report plainly.
  - **Scope decision (operator, 2026-09-24): every layer, M1 through M15, is in scope.** Nothing is skipped or deferred ("this all seems like its needed. so i don't think we can skip any of it"). Each layer gets the M2-style cycle: inventory, freeze, one comparison-and-repair batch, verify. It escalates to multi-round work only if the comparison shows the layer is substantially wrong.
- **Governing plan (operator, 2026-09-24), in this order:**
  1. Finish batch 2c: commit only if the verifier and the full suite pass; if either fails, fix only the 2c issue and re-verify. No scope growth.
  2. One bounded transport hardware test. Its only purpose is to verify the rewritten socket and connection lifetime on a real robot before batch 3. It is one scripted run that saves a self-contained bundle; the operator runs it. It is not an exploratory campaign.
     - **Operator:** with the PC on the robot's Wi-Fi, run from `cozmo-stack`: `dotnet run --project src/Cozmo.Conformance -- m1-link-check` (default robot 172.31.1.1:5551; about 50 s). Copy back the bundle folder it prints, `re-analysis/acceptance/hardware/<yyyyMMdd-HHmmss>-M1-LINK/`.
  3. One comprehensive extraction pass before batch 3, treated as a closure pass for batch 3's dependencies. It covers every parked MISSING item and every source fact batch 3 needs: the handshake, firmware check, pre-validation gating, idle timeout, robotError handling, disconnect reasons and results, the 60 ms engine tick, and the exact ordering and conditions of setup messages such as SyncTime and InitController.
  4. One operator re-approval checkpoint for the resulting inventory changes. It includes the approved M1-039 Windows UDP policy. No batch 3 code before approval. Anything unrecoverable is marked UNKNOWN or POLICY, never guessed.
  5. Settle the batch 2 records (and implement M1-039) in the same verified pass.
  6. Batch 3: implementer, read-only verifier, focused tests, full suite, commit. No stops between ordinary verifier/fix passes. Return to the operator only for:
     - source evidence that contradicts the approved inventory;
     - a genuinely new policy decision;
     - a fact that is still MISSING or UNKNOWN;
     - a material hardware issue.
  - **Hard boundary:** after batch 3, M1 is closed except for documented HARDWARE_ONLY and COMPATIBILITY_POLICY items. If recoverable or missing source work is still growing after the closure pass, stop and report it; do not open another extraction cycle.
- **Recorded policy: M1-042 (operator, 2026-09-24, option a).** The original's phone app (Unity) sends some post-connect game messages that make the engine talk to the robot. After a Success connection response, this stack sends the app's defaults itself: SetRobotVolume with the stored value (default 1.0), which leads to SetAudioVolume (CD27), and the block pool enabled, which leads to cube connection (CD28). The controller can change either. The idle timeouts (StartIdleTimeout / CancelIdleTimeout, CC11) are app-backgrounding behaviour and are not sent automatically. Other Unity post-connect flows (CD31) are replaced by this stack's API.
- **Recorded policy: M1-040 (operator, 2026-09-24).** Accept every robot firmware (older, newer, factory). The handshake's version check never refuses the robot. The robot's firmware version is logged on every connection and in every hardware bundle, with a warning whenever it is not 2381, the build the protocol was recovered from. Rationale: the operator runs firmware 2457, which the original would refuse as OutdatedApp; firmware issues get fixed later, as long as the issue and the firmware are known.
- **Recorded policy: M1-039.** On Windows, a UDP receive that fails with ConnectionReset (an ICMP port-unreachable response) is treated as no data for that receive attempt. It emits no warning and does not end the rest of the tick's drain; the drain continues, as the original, which never sees the error, would. Other socket errors keep their source-backed handling.

- **Process change (operator, 2026-09-24):** only behavioural or source-fidelity defects, circular tests, and races or deadlocks block a commit. Non-behavioural cleanup is queued under "Cleanup queue" while the checker passes and no status becomes misleading. After a behavioural fix, only the affected diff is re-verified. The full suite runs once, just before the commit. Batches are large, and batch 3 is one batch after the closure pass. See AGENTS.md, Process, step 4.

## Hardware run 20260925-061148-CONTROL (operator, 2026-09-25; main b4a9c9a: M2 + M3 + M4)

**Result: 12 of 12 PASS**, including CUBES (78 ObjectAccel in 3 s). The run was on firmware 2457.

**Observations that answer HARDWARE_ONLY and open questions:**
- **M4-021:** the robot echoes the origin. All 890 RobotStates after AbsoluteLocalizationUpdate {0, frame 0, origin 1} reported origin 1, with 0 origin-rejected warnings.
- **M4-024:** cube telemetry flows. After the connection, the WakeUp lights went out: SetCubeGamma 0x80, then CubeID ×3, then CubeLights. Then StreamObjectAccel gave 78 ObjectAccel, and one ObjectMoved arrived.
  - In the first run there were no lights and no telemetry. The difference supports hypothesis (b), that the missing engine messages were the cause.
  - Which message the robot needs has not been isolated. That would be a hardware experiment, and it is not planned.
- **M3-019 / M3-021:** DefaultCameraParams arrived 27 ms after the connection-time SetCameraParams {0, 0, true}:
  - fields: maxGain 3.984, gain 2.0, exposure 0..67, gamma table;
  - the engine then sent SetCameraSettings {2.0, 16, false};
  - in the first run, which had no SetCameraParams, none arrived.
  - Frame mean luminance is about 14.5/255, a dark room. There is no earlier baseline to compare against.
- **Track locks:** 0x9D {4} before each DriveWheels and 0x9E {4} at each stop, as M4-014 says. There was no PotentialCliff.
- **Backpack Off lights:** 16.7/s each for 0x03 and 0x11 (one per engine tick), with no link trouble.
- **ANIM_CANCEL:** no AbortAnimation 0x8D (the known M5-023 gap).

**Defect found: the NV calibration read.**
- **What happened:** the robot answered NVCommand read 0x80000001 with 9 NV_MORE parts (index field 5,6,7,0,3,2,1,4,15) plus a final NV_OKAY. The stack concatenated 248 bytes; the engine expects 56. It logged SizeMismatch, and no calibration was installed.
- **Status:** vision was enabled, as the engine does on every path, so CAMERA passed. Anything that needs the calibration (markers, M11) would not work.
- **Cause:** the stack's NV reader was never inventoried.
- **Next:** being traced from source (NVStorageComponent) under the hardware-failure workflow.

## Hardware run 20260924-202748-CONTROL (operator, 2026-09-24)

**Result: 11 of 12 PASS.**
- **Passed:** CONNECT, STATE (33.3 Hz), HEAD, LIFT, DRIVE, FACE, AUDIO, ANIM, ANIM_CANCEL, CAMERA (14.7 fps, 320x240 grey) and DISCONNECT.
- **Human verdicts:** the operator answered yes to FACE and AUDIO. They are in `operator-verdicts.json`, added to the bundle after the run.
- **The cube connected** (SetPropSlot, then ObjectConnectionState slot 0). So the outbound cube-connection path works on hardware.

**CUBES FAIL: after StreamObjectAccel no ObjectAccel arrived.** It was classified with the AGENTS.md hardware-failure workflow; the trace is in the session scratch `extract/cubeaccel/report.md`, rows S1..S19.
- **(a) Wrong bytes: ruled out.** The original sends exactly `08 00000000 01`: the object's activeID, which is its slot, sent reliably (0x00635556, 0x00635562).
- **(b) A real omission, now an M4 item:**
  - When a light cube connects, the original plays the "WakeUp" cube-light animation (0x00639D64, trigger 0x26). That sends CubeID 0x10, CubeLights 0x04 and possibly SetCubeGamma 0x0C (SetLights 0x0063A764..0x0063A800). The stack sends none of these.
  - The engine's own stream logic does not depend on them.
- **(c) The robot side is HARDWARE_ONLY and still open.**
  - After connecting, the robot forwarded no cube telemetry at all: no power level, moved, tapped or accel.
  - The cube link dropped once for about 0.5 s while driving.
  - Firmware is 2457. Its engine-to-robot hash differs from 2381's, and 2457 is not shipped.
- **Test-reference gap:** the CUBES check exercises M9-017 (CubeAccelComponent) but does not cite it. M9-017's claim of a robot-side effect is not source-backed. The fix goes into the next control-check update.
- **Next:**
  1. Implement the WakeUp-on-connect path in the M4 batch.
  2. Re-run CUBES. If accel then arrives, (b) is confirmed. If not, the cause is robot-side, and gets its own cube-only probe.

## Hard-boundary report (for the operator; 2026-09-24)

The operator's rule: if recoverable or missing source work is still growing after the closure pass, stop and report instead of opening another extraction cycle. Implementing batch 3 surfaced a few new, small source questions. No new extraction cycle has been opened for them. Their records stay IMPLEMENTATION_GAP, and none of them blocks a robot run.

**Update after the batch 3 verifier:** it answered CD3/CD5 (the catch-up skip adds n x period), B25 (pops until empty) and CD27 (vmul by 65535, vcvt.u32 saturating, low 16 bits) from the rows' own cited ranges. It also answered CD18's "success" (it is the AbsoluteLocalizationUpdate send result). Still open: the AbsoluteLocalizationUpdate frameId/originId values (robot+0x2B0; +0x294 then +0x14) as stack state, and G5.5.

**Where M1 stands after batch 3 (2026-09-24):** 26 EXACT_SOURCE, 1 EQUIVALENT_IMPLEMENTATION, 9 COMPATIBILITY_POLICY, 2 HARDWARE_ONLY, 0 RECOVERABLE_GAP, and 5 IMPLEMENTATION_GAP. Each of the five has one specific residual:
- **M1-025 and M1-015:** RemoveRobot leaves this stack's device objects alive, where the original builds a fresh Robot (CB33/CC27). This is buildable: a device-state reset, an interface to M3/M4.
- **M1-027:** robot-to-engine tags inside 0xB0..0xF5 with no codec here cannot be size-checked (CC35). Needs the M2 protocol layer.
- **M1-029:** jsoncpp's grammar leniency and asUInt of a non-number are not in the rows. They are recoverable from the .so's jsoncpp, but that would be a new extraction cycle.
- **M1-041:** AbsoluteLocalizationUpdate is not sent. Its frameId and originId are pose-frame state, which this stack gets with M11 (BlockWorld). RobotStateHistory::Clear has no owner here yet.

So M1 is closed except these five small, named residuals, the HARDWARE_ONLY and the COMPATIBILITY_POLICY records.

**Update: M1-025 and M1-015 are settled (2026-09-24).**
- **The change:** `CozmoRobot.ResetDevices` runs on `Engine.RobotRemoved`. It returns every M1 device object and the VisionSystem to their as-constructed state, and it ends an in-flight audio Play and an in-flight vision frame, with nothing sent or written after the removal.
- **Tests:** four `M1_025_M1_015_*` tests in EngineAppLayerTests.
- **Residuals outside M1**, named in both records' provenance:
  - The upper-layer Robot components are not reset: Carrying, DockingComponent, PathFollower, BehaviorManager, MoodManager, the Idle and Reactive behaviours, CubeMovedReactionStrategy, MapComponent → MemoryMap, and AIComponent → FreeplaySystem. These belong to M12-M15.
  - The connection-time CameraCalib NV read is not implemented (CD21, 0x006583BA; an M3 interface). The reset now drops a calibration the caller read.
  - Keeping `VisionSystem.Enabled` across a removal is UNKNOWN.
  - The 2 s bound on the vision wait is local; the source joins without a bound (0x0065257C).
- **M1 now has three IMPLEMENTATION_GAP records:** 027, 029 and 041.

**Genuinely new, recoverable from the .so, all small:**
- CD3/CD5: when the tick is 240 ms or more behind, does the whole-period skip add to or replace the +60 ms step?
- B25: does ProcessArrivedMessages work on a snapshot of the arrivals, or pop until empty?
- CD18: the AbsoluteLocalizationUpdate frameId (robot+0x2B0) and originId values. It is currently not sent.
- CD18: what counts as SendSyncTime "success" for the +0x520 stamp.
- CD27: how vol x 65535 converts to u16 (truncation assumed; 1.0 is exact).

**Not recoverable, or external:** G5.5, jsoncpp asUInt of a non-number (a library-semantics detail).

**Outside M1, recorded as inputs for later layers:** the GoToSleep sequence (M5), SetCameraParams (M3), the NV reads (NV subsystem), the block-pool Init timing (M4), and where BehaviorReactToOnCharger writes reason 4 (behaviour layer).

Some items the batch 3 implementer listed as MISSING are answered by frozen rows (for example CC23 and CD20). Those are fixed in the verify pass, not re-extracted.

## Decision notes (operator, 2026-09-24: "just make notes of stuff like this; test later and pick the best choice")

Small behaviour choices are noted here rather than put to the operator. Each keeps its current behaviour until a test settles it.

- **Motor stop on shutdown.** CozmoRobot.Dispose sends StopAllMotors and DriveWheels(0) before disconnecting. The original only sends the DisconnectRequest (B33, CC29). Kept for now as a probable safety behaviour. Test later: does the robot stop on its own when the link drops?

- **SyncTime stamp without AbsoluteLocalizationUpdate (obsolete since the M4 batch, 2026-09-25).** The stack now sends AbsoluteLocalizationUpdate {0, frame 0, origin 1} at SyncTime and stamps +0x520 only when that send succeeds, as the engine does (M1-041, M4-020).

- **Wwise mix rate (M6, 2026-09-24).** In the original the Wwise mix rate is the phone's native output rate, capped at 48000: min(AudioTrack.getNativeOutputSampleRate, 48000), cached at 0x0108DF90 (0x00A56E80..0x00A56EA4). The Hijack plug-in then resamples to 22320 Hz by linear interpolation. This stack has no phone, so it uses 48000, which is what the cap gives on typical phones. This will be recorded as a COMPATIBILITY_POLICY in the M6 inventory. Revisit if a capture from an original phone ever shows another rate.
- **Wwise runtime found (M6 extraction, 2026-09-24).** The Wwise 2016.2 runtime is statically linked into libcozmoEngine.so, as symbol-less ARM code at 0x0095E540..0x00AE2E40. Anything earlier marked BLOCKED_EXTERNAL for "Wwise runtime semantics" is therefore RECOVERABLE_GAP, not blocked, and gets re-classified as each layer (M6, M9) is inventoried.

## Cleanup queue (non-behavioural; fold into the next batch)

- The earlier queue was cleared by batch 4(i): the `using` isolation, TransportConstants LF, T_k2, stale MISSING and verifier-reading comments, and the M1-005 doc.
- LinkCheck (Cozmo.Conformance): its warning histogram matches "sendto failed" by prefix; the message is now "UDPTransport.SendFailed: sendto failed ...". Update the key after batch 3 merges; pass/fail is unaffected.
- From the device-reset re-verify (2026-09-24):
  - Label the 2 s vision removal wait as local, citing 0x0065257C.
  - Label the test's `Enabled`-kept assertion as policy, not a primary-source oracle.
  - Make the release in the frame-in-flight test deterministic; the 300 ms timer is a flake risk.
  - PetWorld has no lock (unreachable: OKAO pets are unavailable).
  - The implementer's note that "ReadCalibrationAsync has no caller" is wrong: six Conformance tools call it. What is missing is the connection-time read.
  - `_generation` in AnimationScheduler has no COMPATIBILITY record.
  - FakePort `Calls` is written without a lock.
- fidelity.py `--check` does not enforce FidelityManifestTests' rule that every non-EXACT record has an `effect`. Align the two.
- From the control-check review, after the run: pin the ANIM wheel figure (-75 mm/s for 264 ms) against the clip itself, and exercise the abort path.
- From the M2 batch verifier (2026-09-24, non-blocking):
  - MessageBase.Parse: the doc comment ("short body → RawRobotMessage") is stale and its catch blocks are dead.
  - PROTOCOL_STATUS.md and MessageCatalog label FallingStopped `hardware_refined`, but the fix is engine-derived.
  - gen_protocol.py `ReadStructArray` does not stop at the first failed element (D10). No inbound message has a counted struct array today.
  - CLAD strings are decoded as UTF-8, where the engine reads i8 chars. Nothing in Cozmo.Robot consumes them.
  - The PrintTrace format id uses the low 16 bits, which is not established. It is used for logs only.
- **For the M12 inventory (behavioural, found while reading consumers):**
  - Docking.cs:353 releases the carried object on BlockPlaced without the engine's success gate (R-P1).
  - Docking.cs:357 treats MovingLiftPostDock as `!= 0`, where the engine compares that byte for equality with IDockAction+0x80 (R-P4).
- For M4: the MessageExtras default arguments (SetHeadAngle 10/10, SetLiftHeight 3/20, RobotLink.cs:100) have no engine counterpart (M2-015). The only callers that rely on them are CozmoRobot.cs:499 (a public API) and Probe.cs:92.
- **For the M5 batch (found by the M3 verifier): done in the M5 batch (2026-09-25).** Kept for the record:
  - After a cancel (SetStreamingAnimation(null)), the engine replays the neutral face (A6, A31). The stack has no neutral replay.
  - StreamLive runs outside an Update, so its timing differs from the engine's (A29, 0x0057D080).
  - A Play then Stop inside one tick: in the engine the live stream continues rather than re-initialising.
  - RobotAnimationSink.Finished sends a BodyStop on cancel and can send one after EndOfAnimation. The stack also sends its own BodyStop before EndOfAnimation. None of these has an engine counterpart (A20, A24).
  - `_lastFace` is resent without the layered face flag (A16(9)).
  - Stream error paths: a failed send re-runs `AudioFramesSent++` and KeyframeFired on retry. A throwing send stays at the front of the buffer and faults every tick.
  - The AbortAnimation 0x8D send (M5-023).
  - The production test does not exercise the budget-stop/refill path.
- **control-check, before the next robot run:**
  - ANIM_CANCEL's "nothing after cancel within 100 ms" criterion and its M5-023 note contradict A25: a cancelled clip's pending frame can go out at the next tick that has budget. Rework the criterion or mark it not judged.
  - CUBES should cite M9-017 and M4-024.
  - CONNECT should record the origin and frame the robot reports (M4-021), and whether DefaultCameraParams 0xC8 arrives (M3-019).
  - CAMERA should record image brightness (M3-019).
- **From the M5 verifier (2026-09-25, non-blocking):**
  - **LiveUpdateFailed:** when UpdateLiveAnimation returns 1 the engine logs and returns (0x0057D086..0x0057D0E6); the code logs and carries on to the tail. Unreachable: an add fails only above 1000 keyframes.
  - **control-check keep-alive: done (2026-09-25).** ANIM's `streamingStops` became `clipStreamEnds`, and ANIM_CANCEL's leftover and End criteria now stop at KeepAliveQuietMs = 400 ms, before the keep-alive block's 0.5 s (A31). `abortAnimationSent` is now a criterion (exactly one 0x8D). The keep-alive stream and the neutral replay are recorded as the observations `keepAliveStream` and `neutralReplay`, and M5-036 is in the run's hardwareOnlyUncertainty.
- **From the M4 verifier (2026-09-25):**
  - **M10-007** is EXACT_SOURCE, but Apply has no production caller. The engine's CheckForUnexpectedMovement calls SetNewPose (0x0063E87E), and through F1..F3 that sends an AbsoluteLocalizationUpdate. Put this in M10-007's `unresolved` when M10 is inventoried; check the config flag kCreateUnexpectedMovementObstacles (0x00C7F620).
  - **SetBodyRadioMode order:** the stack sends it before SetOnCharger's threshold; the engine sends SetOnCharger's threshold (0x00512AB4) before SetBodyRadioMode (0x00512B46). This only matters if both fire in the same state.
  - **Charger-platform clearing:** without Robot::Update's clearing (M11 geometry), a robot that starts on the charger keeps the platform flag after it drives off. Until the flag clears, a PotentialCliff is ignored. The flag clears only on a committed off-treads change.
  - **B5:** callers of PlayLightAnim can still finish in a different order from the one they decided in. No caller off the engine thread exists today.
  - **VisionSystem Stopped path:** done (Q-c).
- Tests recommended by batch 4(i): one pinning the 14 Init tunables against the CA/B13 table (M1-005), and a dedicated R43 test for an unsent seq-0 entry at the front (M1-016).

## Parked: MISSING items (resolved)

Every item parked during batches 1-2c was settled by the closure pass of 2026-09-24. The rows are CA1..CA37, CB1..CB38, CC1..CC36, CD1..CD31 and E1..E9 in `re-analysis/inventory/M1-transport.md`, with corrections C6..C11. Two residuals are HARDWARE_ONLY: M1-033 (whether the robot accepts packed type 7/8/9 frames) and M1-043 (whether a 0-byte read warns). Inputs found for other layers (M3 camera params, M4 cliff threshold, body radio mode, lights, cube connection, the NV subsystem, the M5 sleep animation) are listed in the inventory's "Decisions" section.

## Layer order and review state

**Scope: FINAL (operator, 2026-09-24). Every layer is in scope, with the same cycle for each (see "Scope decision" above).** The order is bottom-up by dependency. M5's Wwise-driven audio is an interface to M6. Vision comes before manipulation and navigation, and the behaviour framework before the behaviours that run on it.

Review state per subsystem is in `re-analysis/fidelity_manifest.json`, and FIDELITY_GAPS.md renders it.

| order | subsystem | tier | review | notes |
| ---: | --- | --- | --- | --- |
| 1 | M1-transport | full | INVENTORY_APPROVED (closure complete) | batches 1-4(i) and 3 committed, settled; M1-LINK passed on the robot; residuals 027/029/041; next: control-check run |
| 2 | M2-protocol | full | INVENTORY_APPROVED (2026-09-24, manager, standing authorisation) | 16 records; FallingStopped and BlockStatus contradictions found |
| 3 | M3-device (camera, display, audio device) | full | INVENTORY_APPROVED (repaired 2026-09-24) | colour camera format is HARDWARE_ONLY; FACE, AUDIO and CAMERA passed on hardware 2026-09-24 |
| 4 | M4-control (motion, sensors, lights, cubes) | full | INVENTORY_APPROVED (repaired 2026-09-25) | second CONTROL run 12/12 PASS, cube telemetry arrives; NV calibration read defect found (fix pending) |
| 5 | M5-animation | full | INVENTORY_APPROVED (2026-09-24, manager) | repaired 2026-09-25; 18 EXACT, 15 IMPL_GAP with residuals; keep-alive and neutral replay not yet on hardware |
| 6 | M6-wwise-bank | full | INVENTORY_APPROVED (2026-09-25, manager) | 18 records; the Wwise 2016.2 runtime is statically linked, so the whole audio path is primary source; MD1 mix-rate policy (M6-018) |
| 7 | M10-derived | full | INVENTORY_APPROVED (2026-09-25, manager) | 13 records; M10-006/007/008 contradicted by the source; MD1 forced policy (M10-013) |
| 8 | M11-vision | full | UNREVIEWED | |
| 9 | M12-manipulation | full | UNREVIEWED | M2-014 (BlockStatus) feeds it |
| 10 | M13-navigation | full | UNREVIEWED | |
| 11 | M14-faces | full | UNREVIEWED | the OKAO boundary |
| 12 | M8-framework | full | UNREVIEWED | |
| 13 | M7-behaviour | full | UNREVIEWED | |
| 14 | M9-wwise-music | full | UNREVIEWED | singing; BLOCKED_EXTERNAL Wwise runtime semantics |
| 15 | M15-freeplay | full | UNREVIEWED | |
| – | tools | – | UNREVIEWED | offline tooling |

## M1 candidate (uncommitted working tree)

- **What it is:** the M1 repair done with ChatGPT on top of `bcab556`, applied from `~/Downloads/m1-repair-review.patch` and then `m1-final-corrections.patch`. It touches 27 files: `ReliableTransport.cs` (the bulk of it), `ReliableConnection.cs`, `CozmoRobot.cs`, `Cozmo.Transport.csproj`, conformance tools and tests, plus the new `TransportRepairTests.cs`.
- **Reported results:** 48/48 focused tests and 999/999 overall. That report comes from the ChatGPT session and the manager has not re-run it. It has had no hardware acceptance.
- **Treatment:** a candidate implementation. The M1 inventory decides what survives. Nothing is discarded or accepted before then.
- The setup work for this process touches none of those 27 files.

## Hardware

- Only the operator's machine reaches a robot. The manager never plans a step that assumes otherwise.
- New runs go to `re-analysis/acceptance/hardware/<yyyymmdd-hhmmss>-<test-id>/` (AGENTS.md, "Hardware runs").
- **Existing tooling, not yet reviewed under this process:**
  - `hardware-test` in `Cozmo.Conformance` asks the operator for a verdict per check, where this process wants the result judged from a machine-readable bundle.
  - Earlier bundles are in `cozmo-stack/re-analysis/acceptance/` and `cozmo-stack/cozmo-acceptance-*.json`.
  - `HardwareCatalog.cs` maps checks to fidelity ids.
  - Each is to be reviewed alongside the layer it tests.

## Legacy documents

`re-analysis/HANDOFF.md`, `NEXT_MILESTONE.md`, the milestone documents and the claims of "complete offline" predate this process. They are notes, the lowest authority. Where they disagree with an approved inventory, the inventory wins.

The user-level agents in `~/.claude/agents/` (`cozmo-m1-transport-auditor`, `cozmo-m2-protocol-auditor`, `cozmo-proof-auditor`, `cozmo-adversarial-reviewer`) come from an earlier salvage audit and point at a stale scratch directory. The process uses the project agents in `.claude/agents/` instead.

## Accepted commits

| commit | subsystem | what it accepted |
| --- | --- | --- |
| 7aba301 | M1-transport | M1-004, M1-011, M1-012, M1-017 settled EXACT_SOURCE with evidence-derived tests; LINK check names M1-033. M1-003 and M1-008 tagged but held (DeleteConnection semantics; clock) |
| e6f2ed7, 8c9f405, 0691429, ed2729e | M1-transport | repair batches 2a, 2b-i, 2b-ii, 2c (receive path; clock/scheduler; connection lifetime; socket/addressing) |
| fef527c, 224accb | M1-transport | m1-link-check; closure pass re-approved; M1-LINK passed on hardware (20260924-112412-M1-LINK) |
| 3af197f | M1-transport | batch 4(i): transport completion, M1-039, 16 records settled |
| 518b730, 67d4bc2 | M1-transport | batch 3 app layer; settle to 26 EXACT_SOURCE |
| 4b89c79, abea3ce | tools | control-check self-judging hardware run, and its review fixes |
| (this commit) | M1-transport | device reset on RemoveRobot; M1-025, M1-015 settled EXACT_SOURCE |

## Open decisions for the operator

- none pending