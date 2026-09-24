# Project state

Read first in every session. The manager keeps this file current; the process it follows is the Process section of `AGENTS.md`.

## Now

- **Phase:** M1 closure pass done. The inventory was re-approved by the manager under standing authorisation on 2026-09-24: 43 records, 28 IMPLEMENTATION_GAP, 9 COMPATIBILITY_POLICY (M1-039, M1-040 and M1-042 added), 4 EXACT_SOURCE, 2 HARDWARE_ONLY, 0 RECOVERABLE_GAP. M1-LINK hardware run PASSED (bundle committed).
- **Operator decisions:** D1-D4 approved 2026-09-23. On 2026-09-24: the inventory, D5, D6 (fatal behaviour recorded, isolation kept as policy), D7, the corrections C1..C5 and D8 (stop processing a frame after a DisconnectRequest, M1-038) approved; the three repair batches are authorised to run without further checkpoints.
- **Batch 1 done (7aba301). Batch 2a done** (e6f2ed7; receive path: B17 truncation, receive-error counters, partial-header overrun after the header, M1-038); records it repaired are settled in one verified pass at the end of batch 2. **Batch 2b-i done** (8c9f405; clock, construction-time 2 ms scheduler + FIFO executor, posted sends, RobotLink isolation; three verification passes). **Batch 2b-ii done** (0691429; per-address connections, type-3 and timeout delete only that connection, timed-out flag, inbound creation, FinishConnection, posted Start/Stop, per-connection multipart, unconditional Dispose disconnect; two verification passes). **Batch 2c done** (socket B10/B11/B12/B16/B38 with C1's 47817 reopen, the M1-023 reset mechanism, the M1-037 host trigger, M1-001 addressing; verifier PASS on the second pass). Batch 2 complete.
- **Next:** one large batch. (i) Settle the batch-2 records the code now reproduces, implement M1-039 and clear the cleanup queue (transport files). (ii) Batch 3: the app layer, M1-024..M1-032, M1-040, M1-041 and M1-042 (CozmoRobot / new files). Then a self-judging direct-control hardware run for the operator.

- **Standing authorisations and current plan (operator, 2026-09-24, supersedes the governing plan below where they differ):**
  - **Pushing:** the manager may push to GitHub `origin main` whenever a robot run is ready, so the operator's Cozmo machine can pull it.
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

## Cleanup queue (non-behavioural; fold into the next batch)

- `M1_019_R39_StopSendsNothingClearsTheConnectionsAndClosesTheSocket`: rebinds 47817 after Stop/Start but disposes only at its end (test isolation; give it `using`).
- `TransportConstants.cs` has CRLF line endings (git warns); normalise to LF.
- T_k2 (TransportRepairTests): uses Windows ConnectionReset as its input error and asserts "ReadFailed"; it conflicts with M1-039, so give it a different error when M1-039 is implemented. Its "no row or policy covers" comment is stale.
- With no socket open, every resend counts error 6 and warns each time (the B10 rate-limit gap; resolved by the B10 inventory correction).
- MISSING comments at RT FinishConnection (~:684) and the empty-container case (~:1054) are answered per PROJECT_STATE; update them when the inventory is corrected.

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
| 1 | M1-transport | full | INVENTORY_APPROVED (closure complete) | batches 1-2c committed; M1-LINK passed on the robot; next: settle + batch 3 |
| 2 | M2-protocol | full | UNREVIEWED | |
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

## Open decisions for the operator

- none pending
