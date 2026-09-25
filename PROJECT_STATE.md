# Project state

Read first in every session. The manager keeps this file current; the process it follows is the Process section of `AGENTS.md`.

## Now

- **Phase:** M1 is closed apart from named residuals.
  - Batch 3 (the app layer) is 518b730, and the settle pass is 67d4bc2.
  - The device reset on RemoveRobot (M1-025, M1-015) is committed after the commits below. Its first verification failed on two races and on the calibration being kept. Those were fixed, and the re-verify passed.
  - M1 records: 28 EXACT, 1 EQUIVALENT, 3 IMPL_GAP, 9 POLICY, 2 HARDWARE_ONLY.
  - control-check is on main as 4b89c79 plus abea3ce. Its review returned NOT READY: the STATE rate window, ANIM driving without `--allow-drive`, and the OBB preflight. All of these are fixed.
  - The full suite passed 1163/1163 before the commit.
- **Waiting on the operator:**
  - one control-check robot run (see "Next").
- **M2 protocol inventory: approved by the manager on 2026-09-24 under the standing authorisation below, and frozen.** The next step is one comparison-and-repair batch, then a verify. It is `re-analysis/inventory/M2-protocol.md`.
  - **Extraction:** three read-only passes (OUT, 42 messages; IN, 56 codecs plus dispatch; the ImageChunk gap).
  - **Records:** M2-001..M2-016. All are IMPLEMENTATION_GAP until the comparison against the code. The seven earlier EXACT_SOURCE claims rested on weak evidence. No RECOVERABLE_GAP remains.
  - **Two contradictions of the current code, confirmed by the manager in the disassembly:**
    - M2-013: the FallingStopped codec reads the timestamp as the duration.
    - M2-014: BlockStatus 1 and 2 are swapped in Docking.cs.
  - The inbound dispatch also settles M1-027's residual interface (M2-010).
  - The comparison and repair run as one batch (operator: "do not expand M2 into another M1-style process unless the comparison shows that it is substantially wrong").
- **Operator decisions:** D1-D4 approved 2026-09-23. On 2026-09-24: the inventory, D5, D6 (fatal behaviour recorded, isolation kept as policy), D7, the corrections C1..C5 and D8 (stop processing a frame after a DisconnectRequest, M1-038) approved; the three repair batches are authorised to run without further checkpoints.
- **Batch 1 done (7aba301). Batch 2a done** (e6f2ed7; receive path: B17 truncation, receive-error counters, partial-header overrun after the header, M1-038); records it repaired are settled in one verified pass at the end of batch 2. **Batch 2b-i done** (8c9f405; clock, construction-time 2 ms scheduler + FIFO executor, posted sends, RobotLink isolation; three verification passes). **Batch 2b-ii done** (0691429; per-address connections, type-3 and timeout delete only that connection, timed-out flag, inbound creation, FinishConnection, posted Start/Stop, per-connection multipart, unconditional Dispose disconnect; two verification passes). **Batch 2c done** (socket B10/B11/B12/B16/B38 with C1's 47817 reopen, the M1-023 reset mechanism, the M1-037 host trigger, M1-001 addressing; verifier PASS on the second pass). Batch 2 complete.
- **Next: the operator's control-check run.**
  1. In the Claude session: `! git push origin main`.
  2. On the Cozmo machine, from `cozmo-stack`: `git pull`, then `dotnet run --project src/Cozmo.Conformance -- control-check 172.31.1.1 --obb "<the unpacked OBB dir used for earlier hardware-test/behavior runs>" --allow-drive`.
     - `<obb>` is the directory that contains `assets/cozmo_resources/assets/animations/`.
     - If the path is wrong, the tool exits 2 before connecting and prints the path it expected.
  3. Setup: Cozmo off the charger, on the floor or a cliff-safe table, with about 10 cm clear in front and behind. One cube powered and within about 30 cm. The tool prints this and waits for Enter.
  4. Copy the `re-analysis/acceptance/hardware/<stamp>-CONTROL/` folder it prints to Downloads.
  5. The manager then judges the bundle and fixes the failures from the source (AGENTS.md hardware-failure workflow).

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
  - **Scope decision:** the M6-M15 scope is still undecided (see the layer map in the conversation of 2026-09-24). Nothing is deferred yet.
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

- **SyncTime stamp without AbsoluteLocalizationUpdate.** The stack cannot send AbsoluteLocalizationUpdate yet (frameId and originId are not stack state). It still stamps +0x520 where that send would have happened, so the CD19 "SyncTimeAck not received" warning stays meaningful. The alternative is to leave it unset, which silences CD19. Revisit when the ids exist.

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
- Tests recommended by batch 4(i): one pinning the 14 Init tunables against the CA/B13 table (M1-005), and a dedicated R43 test for an unsent seq-0 entry at the front (M1-016).

## Parked: MISSING items (resolved)

Every item parked during batches 1-2c was settled by the closure pass of 2026-09-24. The rows are CA1..CA37, CB1..CB38, CC1..CC36, CD1..CD31 and E1..E9 in `re-analysis/inventory/M1-transport.md`, with corrections C6..C11. Two residuals are HARDWARE_ONLY: M1-033 (whether the robot accepts packed type 7/8/9 frames) and M1-043 (whether a 0-byte read warns). Inputs found for other layers (M3 camera params, M4 cliff threshold, body radio mode, lights, cube connection, the NV subsystem, the M5 sleep animation) are listed in the inventory's "Decisions" section.

## Layer order and review state

**Scope: PROPOSED, NOT FINAL.** The operator asked for a layer map before deciding (2026-09-24). Until then nothing is deferred; the list below is only the proposal.
- **Full rigor (inventory, freeze, repair, verify):** M1 through M5.
- **In target, cheap audit first:** M11 (vision, markers) and M12 (cube manipulation). Each gets one read-only comparison; it escalates to full rigor only where a mismatch affects autonomous control.
- **Deferred:** M6 through M10 and M13 through M15. The exception is any low-level audio or playback primitive that M3 or M5 requires.

Review state per subsystem is in `re-analysis/fidelity_manifest.json`, and FIDELITY_GAPS.md renders it.

| order | subsystem | tier | review | notes |
| ---: | --- | --- | --- | --- |
| 1 | M1-transport | full | INVENTORY_APPROVED (closure complete) | batches 1-4(i) and 3 committed, settled; M1-LINK passed on the robot; residuals 027/029/041; next: control-check run |
| 2 | M2-protocol | full | INVENTORY_APPROVED (2026-09-24, manager, standing authorisation) | 16 records; FallingStopped and BlockStatus contradictions found |
| 3 | M3-device (camera, display, audio device) | full | UNREVIEWED | colour camera format is HARDWARE_ONLY |
| 4 | M4-control (motion, sensors, lights, cubes) | full | UNREVIEWED | the outbound cube-connection path is suspected unrecovered |
| 5 | M5-animation | full | UNREVIEWED | the live-animation wire lifecycle is suspected incomplete |
| 6 | M11-vision, M12-manipulation | audit | UNREVIEWED | escalate only where a mismatch affects autonomous control |
| – | M6-wwise-bank, M9-wwise-music | proposed: deferred (pending decision) | UNREVIEWED | except audio/playback primitives M3/M5 need |
| – | M7, M8, M10, M13, M14, M15 | proposed: deferred (pending decision) | UNREVIEWED | |
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