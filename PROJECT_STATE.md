# Project state

Read first in every session. The manager keeps this file current; the process it follows is the Process section of `AGENTS.md`.

## The plan (operator, 2026-10-05): one layer at a time

**Why:** five workers in parallel produced merge conflicts, a long checking queue and no finish line. Nothing has been
ACCEPTED yet.

**The order**, bottom-up. Each layer is finished all the way to ACCEPTED before the next starts:
1. M1 + M2: connecting and protocol.
2. M3 + M4: devices and motor control.
3. M5: animation and the face.
4. M10–M15 and M7/M8: vision, cubes, navigation, behaviours.
5. The sound layers, M6 + M9, **last** (operator, 2026-10-07). Sound already plays, approximately, through the direct
   decoder. Even under ADP-1 its decision rows run to about 140 obligations (queue 5, Q14), so the robot's behaviour
   layers are finished first.

**Done for a layer means** (revised 2026-10-17 under AGENTS.md "What we reproduce"):
- every live-path record is reproduced (its observable behaviour verified against source, EXACT_SOURCE or a confirmed
  EQUIVALENT_IMPLEMENTATION), not supported (a recorded decision naming who decided and why), or HARDWARE_ONLY settled
  by a robot run; every other record is a confirmed policy;
- the production path the layer serves actually runs that way (fewer listed blockers is not acceptance);
- the layer's hardware script has been run by the operator;
- the review state is ACCEPTED.

**The cross-layer rule.** A lower-layer record whose path runs into a higher layer is split. The lower-layer part is
finished and accepted now. The higher-layer part becomes a record in that higher layer, with its own citation, and is
finished when that layer's turn comes. Examples: M1-044 destroys the behaviour layer; M1-045's go-to-sleep runs on the
ActionList.

**Two lanes at a time:**
- **one builder** (DeepSeek, or a Sonnet when the allowance allows);
- **one checker:** Codex finds defects, then a single Opus pass settles the layer.

Work already in flight (B-ACTIONS, B-FACE, B-M6b-4, R-FIX3 and the parked branches) is finished or parked as it
stands. Nothing new starts outside the current layer.

**Audio (operator, 2026-10-05):** ADP-1 in AGENTS.md. Exact decisions, equivalent per-sample DSP, with thresholds measured against the emulator (`jobs/B-ADP-HARNESS.md`). No bulk reclassification.

## Where things stand (2026-10-16)

- **Temporary manager handoff (operator, 2026-10-17):** Codex takes over Claude's manager and independent
  verification duties while Claude is unavailable; the operator has restarted the separate builder and research
  lanes. This supersedes the Claude/Opus-only role restrictions for this handoff, not the evidence standard,
  inventory approval checkpoints, or separation of extraction, implementation and verification. Reviews identify
  the actual verifier as Codex. First bounded review: B-M3M4-27 (788a04b3), the outstanding cleanup check on the
  M1+M2 acceptance path. No subsystem is accepted by this handoff.
  **Ended (operator, 2026-10-17):** Claude resumes manager and Opus verification; Codex returns to the research and
  builder lanes. Builder commits published during the handoff (B-M15-7, 2fc72bc3) still get the Opus verifier.

- **Now (2026-10-18, evening): M1-transport and M2-protocol are ACCEPTED**, the first layers of the project to get
  there (accepted commit 4f184775; bundle `acceptance/hardware/20261018-170546-M1M2-HW`, manager judgement in its
  README). Every check was as source once two tool comparison bugs were set aside (fixed in the tool). The robot has no
  needs record (case 1): the stack writes nothing where the original would write one (B-M1-10 gate, M15-014/M15-030).
  **Next layer: M3 + M4** (devices and motor control), per "The plan". Plan and blockers:
  `re-analysis/acceptance/M3M4-blockers.md` (2026-10-18): both Opus settling passes adopted (FAIL; 10 records settle
  after cleanup); builders B-M3M4-30..33 done; checks: 31 PASS (relative cube lights wait on
  research OV129), 32 and 33 FAIL (2026-10-19), fixes in B-M3M4-34 (review PASS 2026-10-19); OV129 adopted, B-M3M4-35 (marker normal,
  completes relative cube lights) done, check PASS 2026-10-19. **Settle readiness (manager, 2026-10-19; `research/20261019-m3m4-settle.md`):** M3-012, M3-055, M3-060, M4-043, M4-044, M4-047, M4-050 settled EXACT_SOURCE; the other 16 are source-supported but run only with the world model built. **Operator decision (2026-10-19): CozmoRobot always builds its world model (VisionSystem/BlockWorld)**, as the engine does: job B-M3M4-36 done, check PASS (`research/20261019-b-m3m4-36-check.md`); the 16 settled (M4-052 as EQUIVALENT_IMPLEMENTATION: bionic atan2f). **Every M3/M4 record is now settled, policy, equivalent or hardware-only.** Next: the M3+M4 hardware script (pass B's 14 coverage items), the operator's run, then ACCEPTED; B-M3M4-33 built the robot-state history (pulled forward by the operator 2026-10-18); OV128 adopted with corrections
  (`research/overnight/20261018-ov128-check.md`); operator decisions recorded on M3-004/017,
  M4-006/039/040/042.
- **Numbers:** `re-analysis/FIDELITY_PROGRESS.md`, generated from the manifest by
  `python -I re-analysis/tools/status_views.py`. All 15 engine subsystems are INVENTORY_APPROVED; **M1-transport and M2-protocol are ACCEPTED** (2026-10-18).
- **Jobs:** `re-analysis/jobs/STATUS.md` (generated) shows, per builder job, the queue note, the status-file state and any
  verification evidence file. Implemented (status file DONE), verified (a check file exists; read it for the verdict) and
  accepted (subsystem review state) are separate and are never inferred from one another.
- **Lanes as run since 2026-10-11:**
  - one Codex builder (clone `cozmo-stack-codex2`) works `re-analysis/jobs/QUEUE.md` under `jobs/CODEX-BUILDER.md`;
  - research requests are listed in `re-analysis/research/OVERNIGHT-QUEUE.md`. Codex answers them, or the manager runs
    read-only Opus extractors and adopts the answers as checked evidence;
  - the manager writes jobs, adopts checked research, and has each builder commit checked by a read-only Opus verifier.
- **Standard (operator, 2026-10-17):** AGENTS.md "What we reproduce" replaces "Exact, always". Observable behaviour
  (robot wire, robot behaviour, C# API delivery and readiness, downstream state) comes from source; implementation is
  free where the result is the same; logs are best effort; exclusions are recorded decisions; narrow checks before
  full extractions; applied layer by layer, never in bulk.
- **Direction (operator, 2026-10-16; standard superseded 2026-10-17):** reduce repetition. In order:
  1. reconciled, generated state views (this revision);
  2. the next acceptance target and its concrete blockers, wherever they live;
  3. regression tests that reach behaviour through the real production wiring, or say that they don't;
  4. a narrow pilot of a mechanical log check, kept only if it catches real defects cheaply.
- **Manager (operator, 2026-10-17):** Opus stays technical manager; no separate lighter coordinator for now
  (revisit if routine management becomes a substantial share). Fresh manager sessions at natural checkpoints, once
  findings are adopted and this file and `acceptance/M1M2-blockers.md` carry the handoff; the saving is unmeasured.
  Fix reviews resume the original verifier where practical, scoped to the changed code and affected behaviour, with
  all gates kept. Routine bookkeeping uses `status_views.py` and `fidelity.py`.
- **History** up to this revision: `re-analysis/archive/PROJECT_STATE-history-to-20261016.md`.

## Decision notes (operator, 2026-09-24: "just make notes of stuff like this; test later and pick the best choice")

Small behaviour choices are noted here rather than put to the operator. Each keeps its current behaviour until a test settles it.

- **M6-042 N12, decided (operator, 2026-10-16): skip and log.** "generally we don't want to reproduce errors". AGENTS.md's M1-034 standing rule now also covers hangs and deadlocks of the original; defined error handling stays exact.

- **Process guard (manager, 2026-09-26, prompted by the NV correction).** AGENTS.md now carries two manager rules: a settled record owns its whole production path (an unrecorded part gets its own gap record before the claiming record is settled; no deferral to another record, a comment or prose), and a claim about an existing record is checked against the current manifest rather than recalled. Origin: M3-022 was EXACT_SOURCE while the NV wire had no records and the gap lived in `NvStorage.cs`'s summary, and the NV pass 4 report called M11-011 contradicted from an earlier revision of its text.

- **Motor stop on shutdown.** CozmoRobot.Dispose sends StopAllMotors and DriveWheels(0) before disconnecting. The original only sends the DisconnectRequest (B33, CC29). Kept for now as a probable safety behaviour. Test later: does the robot stop on its own when the link drops?

- **SyncTime stamp without AbsoluteLocalizationUpdate (obsolete since the M4 batch, 2026-09-25).** The stack now sends AbsoluteLocalizationUpdate {0, frame 0, origin 1} at SyncTime and stamps +0x520 only when that send succeeds, as the engine does (M1-041, M4-020).

- **Wwise mix rate (M6, 2026-09-24).** In the original the Wwise mix rate is the phone's native output rate, capped at 48000: min(AudioTrack.getNativeOutputSampleRate, 48000), cached at 0x0108DF90 (0x00A56E80..0x00A56EA4). The Hijack plug-in then resamples to 22320 Hz by linear interpolation. This stack has no phone, so it uses 48000, which is what the cap gives on typical phones. This will be recorded as a COMPATIBILITY_POLICY in the M6 inventory. Revisit if a capture from an original phone ever shows another rate.
- **Wwise runtime found (M6 extraction, 2026-09-24).** The Wwise 2016.2 runtime is statically linked into libcozmoEngine.so, as symbol-less ARM code at 0x0095E540..0x00AE2E40. Anything earlier marked BLOCKED_EXTERNAL for "Wwise runtime semantics" is therefore RECOVERABLE_GAP, not blocked, and gets re-classified as each layer (M6, M9) is inventoried.

## Cleanup queue (non-behavioural; fold into the next batch)

- **Flake (2026-10-18):** `NeedsPersistenceGateTests.RealConnectNeverRestoresOrWritesUnbuiltNeedsPersistence(hasV5: False)`
  failed once in the push gate ("connect state 2": no ConnectionState 2 within 10 s after the injected ConnectionResponse,
  `NeedsPersistenceGateTests.cs`:116/177) on a memory-starved machine; passed 3/3 alone and in the next full run
  (5751/5751). Builder to check whether the injected response can race the transport's connect state, or only load. A second gate run (2026-10-18) failed the hasV5 case with the MISSING counted twice: the class read the process-static `SteppedBehavior.MissingReported` outside its serial collection. The manager put it in the "SteppedBehavior missing-report statics" collection (test-only); recheck whether that also explains the timeout.

- **B-M1-11 check (2026-10-18):** name the skipped AttemptReadFromDevice success tail (NeedsState Decay 0x006936C6, +0x30
  0x006936CE, DAS 0x006936D2) in the device MISSING (`Needs.cs`:1386) and on M15-014/M15-030; relabel or rebase the
  `{1,1,1}` expectation in `NeedsPersistenceGateTests.cs`:281,286; cite the restored Lab test method on M15-050.

- **B-M1-9 check (2026-10-18; research/20261018-b-m1-9-check.md):** Q2 the new Impl handlers also reach
  `Engine.DeviceRoute` (`RobotSubscriptions.cs`:201-207; no matching device handler, comment or `routeDevice:false`); Q3
  image-latency update position comment (`CozmoEngine.cs`:1337 vs native 0x00514386); Q4 cite the robot+0x31C writer in
  the BA warning comment (`SettlingMessageHandlers.cs`:21-22). From the B-M1-8 check: Q1 raw-tap re-parse cost and
  drain-time timestamps in Conformance tools (the hardware script takes timestamps accordingly); Q2 GetBlockPool position.

- **B-M2M15-2 Opus check (2026-10-17; research/20261017-b-m2m15-2-check.md):** PASS with queued items; finding 1
  (inline app delivery under the streamer lock) raised to blocking, fixed in B-DELIV-1. Queued: relabel
  `NoRobotDrops…` as unit; the TriggerLiftSafe loop-count re-Init (unreachable); C5's widened B-M12-7 race (B-M12-7);
  the Turn C4 log.

- **B-M1-3 Opus fix review PASS with queued items (2026-10-17; research/20261017-b-m1-3-check.md):** dispose the
  `ReliableTransport` in `CozmoRobot`'s constructor when `new CozmoEngine` throws for a bad config (N1; background
  threads leak, nothing sent). B-M1-2 Q4/Q5 go to the next M1 job.

- **B-M15-11 Opus check PASS with queued items (2026-10-17; research/20261017-b-m15-11-check.md):** relabel
  `LabDefinitionsCheckedRowsTests` (its enable-log part is unit: replayed Init) and correct M15-049's "no replay" note;
  make `VisionConfigCheckedRowsTests.cs`:65 assert something startup-dependent (e.g. the single "Enabling the lab"
  entry). The EngineConfigJson startup stop is M1-054's (OV126).

- **B-M15-7 Opus check FAIL (2026-10-17; research/20261017-b-m15-7-check.md):** three blocking (Lab Init aborts on an
  AB-disable MISSING the adopted evidence settles as always-enabled; Feeding deselect keeps its E2G 0x0D subscription;
  Unicode locale lowercasing). Fixes and queued items in B-M15-11, after B-M1-2. Manager to add: a COMPATIBILITY_POLICY
  record for the locale app input (native Locale enum at context+0x18, 0x006A53D2..0x006A53DC; check Q6).

- **OV124 adopted (manager, 2026-10-17; research/overnight/20261017-ov124-check.md):** after B-M1-2 publishes, fold
  the answer into M1-055's unresolved (engine side: the fresh Init-time broadcast sends zero AnimationAvailable, then
  EndOfMessage(0), then Debug; whether the game receives it is M1-056). New M1-056 (RECOVERABLE_GAP, OV125 queued) and
  M1-057 (0xB1/0xB2/0xB3 unreachable). M1-transport re-approved for the two new records.

- **B-M3M4-27 Codex source check (2026-10-17):** add retrospective `reach:` labels to its changed/new
  M3M4X21/M3M4X17 regressions and per-record coverage to its status report, following
  `re-analysis/research/20261017-b-m3m4-27-check.md`. The label rule postdates the implementation. Source review
  passed the five items; M4-044/M4-045 remain IMPLEMENTATION_GAP and no subsystem is accepted.
  Manager adopted the bounded PASS after fidelity check (690 records) and full suite (5569/5569, zero skipped).

- **From the B-M8-7/B-M8-8 verifier (2026-10-16, PASS):** (1) narrow the file-wide `// fidelity: M8-015` tag in `SteppedBehavior.Actions.cs` (M8-015 cites only 0x005BDC04), or extend the record; (2) B-M8-7 status MISSING group 1 needs addresses (concrete IActionRunner classes exist); (3) `AIWhiteboardHandlers.cs:105,166` raise `PossibleObjectsRedrawn` instead of calling the built render unit (native 0x0056C692; not live); (4) the latent StopActing cancel is recorded on M8-015 and must be built before the owned StartActing is wired.
- **From the M6-003 batch (2026-09-26):** `WwiseAdpcm` clamps the header step index to 0..88, which the source does not (unreachable on all 227 shipped ADPCM media); `WwiseAdpcm.Decode` refuses channels outside 1–2 where the runtime decoder is channel-generic (no shipped file has ≥3); the `WwiseAudioSource` comment still lists "seven stereo ADPCM undecodable"; `fmt+0x14` byte-1 semantics are UNKNOWN (byte 0 == nChannels in all 227 files, so moot). **Flake:** one full-suite run after the M6-003 change reported 1 failure of 1465 that did not reproduce in two subsequent runs; if it recurs, capture the test name.
- **From the NV batch A verifier (2026-09-26, non-blocking):** the stack's `NVStorageOpResult` record orders the fields `(tag, op, result, index)` where the native `BroadcastNVStorageOpResult` is `(tag, result, op, index)`; it is an internal event accessed by name, so no wire impact. The engine's `ReadSuccess`/`ReadEntryNotFound`/`ReadFailed` completion log lines are not emitted. Test coverage: `M3_028_TheRestIsReRequestedWithoutReArming` does not assert the "no re-arm"; `M3_026` does not exercise FIFO/one-in-flight; the timeout test uses a state clock only. When M3-032 is built, re-check the connection read's arm against the gating (a `RobotState` with clock > 5000 arriving before the reply would now time it out).
- **From the M10 test-oracle fix (2026-09-26):** `SteppedBehavior.IsRunnable` (`SteppedBehavior.cs:96-97`) gates on `context.Robot.Animations.Library is not null`, which the engine's `IBehavior::IsRunnableBase` (0x5BD778) does not. It is inert on the live path (the library is loaded in production), it is not claimed by any M10 record, and MD2 defers "the behaviours' IsRunnable" to M8. Decide whether to record or remove it when M8 is inventoried.
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

## Layer order and review state

**Scope: FINAL (operator, 2026-09-24). Every layer is in scope, with the same cycle for each (see "Scope decision" above).** The order is bottom-up by dependency. M5's Wwise-driven audio is an interface to M6. Vision comes before manipulation and navigation, and the behaviour framework before the behaviours that run on it.

Review state per subsystem is in `re-analysis/fidelity_manifest.json`; `re-analysis/FIDELITY_PROGRESS.md` (generated) renders the current states and counts. The order of layers is the plan above.

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
| ce859bf | M1-transport | M1-024 decay bracket snapshot; M1-044/M1-015 warning log level; regression coverage |
| c8af57e | M1/M2 and transferred recipients | Cited manager/policy disposition; four new M1 gap records; re-approved 13 affected inventories |
| 4f184775 | M1-transport, M2-protocol | **ACCEPTED** (2026-10-18): all records settled, policy or equivalent; M1-033 by robot runs; M1M2-HW bundle 20261018-170546 judged as source |

## Open decisions for the operator

- **M6-002 Vorbis IMDCT (correction C8).** The engine's inverse MDCT is a float NEON transform whose per-lane arithmetic and table indexing are RECOVERABLE_GAP (ranges and the 615-float master table are named in `re-analysis/evidence/m6-vorbis/vorbis-imdct.md`). **Decided (operator, 2026-09-27): exact.** Transliterate the lane-level arithmetic and vendor the 2460 table bytes. The RECOVERABLE_GAP rows go back to the extractor. The kernel stays fail-closed until then.
