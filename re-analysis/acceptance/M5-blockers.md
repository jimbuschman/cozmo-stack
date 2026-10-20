# M5 acceptance: what stands in the way (manager, 2026-10-19)

- **Target:** M5-animation reaches ACCEPTED, under "Done for a layer" in `PROJECT_STATE.md`, judged by AGENTS.md
  "What we reproduce" (2026-10-17).
- **Examined:** `main` at 3799cdcd. Sources: the manifest, `jobs/QUEUE.md`, `jobs/STATUS.md`, the check files named below.
- **Labels:** **[F]** checked against the current files; **[E]** estimate; **[U]** unknown.

## Records

[F] 65 records. 30 EXACT_SOURCE; 8 COMPATIBILITY_POLICY (M5-020, 042, 047, 048, 049, 057, 060, 061); 3
EQUIVALENT_IMPLEMENTATION (M5-003, M5-021, M5-032: bionic libm only); 1 HARDWARE_ONLY (M5-036, verification NONE: no
robot run has settled it); **23 IMPLEMENTATION_GAP**, no RECOVERABLE_GAP.

[F] Every one of the 23 says "built, awaiting strong verification". The last M5 build chain ends in B-M5-22
(30576f80, trigger-response loader), which has **no check yet**; B-M5-21 before it FAILed. Earlier chains end in Opus
PASSes (B-M5-11, B-M5-20). No settling pass has run over the records since the M5 final passes of 2026-10-12, and much
was built after them (B-M5-13..22, B-M3M4-24, B-M3M4-36's world model).

| Group | Records | Known remaining pieces (from `unresolved`; to be confirmed by the pass) |
| --- | --- | --- |
| Clip and asset loading | M5-001, M5-009, M5-013, M5-016, M5-051, M5-052, M5-054, M5-065 | B-M5-22 unchecked (M5-034, M5-051); readAsJson reader-error text G5b (log only, best effort); PNG colour/16-bit decode (no shipped file); M5-013 +0x28 writers 0x004F9770/0x004F97DE [U] |
| Group choice, cooldown | M5-011, M5-014 | SimpleMood input is M7's; cooldown time is M7-026's |
| Streaming, idle, neutral | M5-008, M5-010, M5-027, M5-030, M5-062, M5-038 | M5-010 reopened by the B-M5-13 check (reconnect SetResetData; constructor read) [U whether closed]; M5-008 MISSING verify callstack 0x0080DA08 / provider flush 0x0080DA24; M5-038 factory recipient is M8's (cross-layer split) |
| Driving animations | M5-063 | action-side call sites are M12-024's |
| Teardown | M5-037, M5-044, M5-053, M5-055, M5-059, M5-064 | native handle ownership MISSINGs (0x00511524..0x00511542, 0x0057AF6C, 0x0057AFD4); engine+0x38 type [U]; ~CozmoEngine order is a host input |
| Robot-side | M5-036 | HARDWARE_ONLY: needs the M5 hardware run |

Logs: M5-013's BufferFaceToSend key/text (B-LOG-1, HELD as cleanup) is not on the acceptance path.

## Sequence

**Queue completion (Codex, 2026-10-20):** B-M5-31 implementation8b9bc2d1 bounded PASS (`research/20261020-b-m5-31-check.md`); manager62/62 focused tests and fidelity check pass. All eight B-M5-24..31 reviews and requested cleanup are complete and published. Monitor stops after this handoff is pushed. **M5 acceptance remains blocked:** corrected operator hardware run still required; finite-layer generation/activity, actual blink calls/deadlines, coherent budget admission and conditional teardown origin remain diagnostic gaps. Some corrected checks deliberately remain INCONCLUSIVE. FailedTraversingPath remains unexplained; successful driving must still be seen and judged. No record settlement or verification upgrade.

**Builder watch / B-M5-30 review (Codex, 2026-10-20):** 967af8d8 bounded PASS (`research/20261020-b-m5-30-check.md`); native discard/AbortAll and immutable raw replay checked, manager56/56 focused tests and fidelity check pass. Actual Dispose call bounds, resends and prefix candidates are separated; unknown first-send origins remain INCONCLUSIVE. B-M5-31 claimed;24..30 reviewed. **No M5 acceptance: corrected operator rerun and earlier diagnostic gaps remain; successful driving is outstanding and FailedTraversingPath remains unexplained.**

**Builder watch / B-M5-29 review (Codex, 2026-10-20):** b8a14a8e bounded PASS (`research/20261020-b-m5-29-check.md`); native allowance/send/success/direct-End path supports separating delivery snapshots from admission. Budget compliance remains NOT_JUDGED without coherent gate evidence; real AUDIO remains INCONCLUSIVE. B-M5-27 pending-window cleanup completed. B-M5-30 claimed, then31. **Operator rerun and discriminating budget/blink/finite-layer evidence remain required; no M5 acceptance. Successful driving remains outstanding; FailedTraversingPath is unexplained.**

**Builder watch / B-M5-28 review (Codex, 2026-10-20):** 7362c2da bounded PASS (`research/20261020-b-m5-28-check.md`); native per-call decrement/reset confirmed, real PARAMETERS correctly remains INCONCLUSIVE without actual call/deadline/generation evidence. Encoded pixels and F1 host bounds are diagnostics, not firmware display proof. B-M5-29 claimed, then30..31. **Operator rerun plus discriminating blink/finite-layer evidence remain required; no settlement or M5 acceptance. FailedTraversingPath remains unexplained and successful driving is outstanding.**

**Builder watch / B-M5-27 review (Codex, 2026-10-20):** b23ce509 bounded PASS (`research/20261020-b-m5-27-check.md`); native lock/unlock and neutral selection checked, manager M5Hw45/45 and fidelity check pass. Questions now distinguish expected appearance and marked windows, with post-unlock movement excluded and unexpected/raw answers retained. Nonblocking aborted-window END cleanup queued in PROJECT_STATE. B-M5-28 claimed, then29..31. **No acceptance or verification upgrade: operator rerun, finite-layer discriminating evidence and successful driving remain required.**

**Builder watch / B-M5-26 review (Codex, 2026-10-20):** 99965613 bounded PASS (`research/20261020-b-m5-26-check.md`), published at ee03a489. The finite-layer equality oracle is removed; native output is conditional. Real LAYERS remains INCONCLUSIVE: per-frame flags/random generation timing are unavailable through the allowed public observations. Rerun counts alone cannot close this diagnostic gap. No production defect or new hook is authorized. B-M5-27 claimed; review next, then28..31. **M5 acceptance remains blocked on the operator rerun, and successful driving is still required.**

**Builder watch / B-M5-25 review (Codex, 2026-10-20):** a9334fee bounded PASS (`research/20261020-b-m5-25-check.md`); native abort/drain/neutral instructions checked, ordered raw replay verified, manager M5Hw tests 35/35 and fidelity check pass. Cleanup extended in PROJECT_STATE. B-M5-26 claimed at c472606f; review next after publication, then 27..31. No settlement or verification upgrade. **M5 acceptance remains blocked on the operator rerun; physical stop and exact neutral timing remain unverified. FailedTraversingPath remains unexplained; a successful drive is still required later.**

**Builder watch / B-M5-24 review (Codex, 2026-10-20):** bdad8435 bounded PASS (`research/20261020-b-m5-24-check.md`); fresh native instruction check, manager lifecycle tests8/8, fidelity check706 records. Lifecycle observations retain timing and non-atomic snapshot limits. Cleanup queued in PROJECT_STATE. B-M5-25 claimed at018045c7; review jobs25..31 in order as published. **This is not hardware acceptance: M5 remains blocked on the operator rerun and successful drive coverage remains outstanding.**

**Judgement (Codex temporary manager/verifier, operator-authorized 2026-10-20):**
`research/20261020-m5-hw-1-judgement.md`, with raw replay and fresh native instruction evidence.
**NOT ACCEPTED; corrected harness and operator rerun required.** The first-read leads below are superseded where
the judgement disagrees: ProceduralLive does end/restart FF bursts; the no-End path is the distinct persistent-layer
case. Abort's extra frame precedes wire Abort; finite-layer faces are conditional; Disable1 did reach the robot and
locked head telemetry stayed constant; decoded blink closures are about44.1s apart; delivered audio-counter snapshots
mix epochs. Failed DriveStart need not enter Loop. Teardown was checked during Dispose, including sequence provenance.
Jobs **B-M5-24..31** queue the separate harness root causes, first the live-idle oracle. No proven stack behavior
bug, manifest settlement, firmware conclusion or acceptance follows. Exact finite-glitch counts and the M12/M13
traversal cause remain unverified. See the judgement for each check and the remaining hardware observations.

**Operator follow-up (2026-10-20):** all eight jobs B-M5-24..31 must be published for the builder. B-M5-27 now owns
clear expected-appearance descriptions and marked observation windows for every prompt, including an explanation of
neutral eyes and exclusion of post-unlock movement from LOCKED_HEAD. Preserve unexpected observations; incorrect test
rules do not explain everything the operator saw. FailedTraversingPath remains an unknown M12/M13 cause, and successful
driving remains required coverage for a later run. M5 acceptance stays blocked on the robot rerun.

**First read of run `hardware/20261020-074223-M5-HW-2cc1cf` (manager, 2026-10-20; NOT a judgement, leads only).**
Connected, drive allowed, no run errors. From `result.json`:
- TAGS: starts 1,2,3 with matching echoes (an extra started echo 4). AUDIO: 744-byte framing and ~30 Hz hold, but
  frames-ahead reaches 16 and bytes-ahead 9324 (77 snapshots above 14): check against the C9 budget (M3-012) before
  blaming the snapshot timing. SLEEP and TEARDOWN as expected (teardown still to judge per item 4 below).
- **KEEP_ALIVE live idle: 13 Start-FF and 13 End in 10 s**; source expects Start FF and no End (A29/L8). Strongest lead:
  live idle appears to restart its stream; the operator saw the cancelled clip "continued".
- ABORT: one 0x8D, no End, but 2 faces and 2 audio markers in the 400 ms window (rule allows 1); operator said
  stopped-then-neutral.
- LOCKED_HEAD: head moved although HEAD_TRACK was locked (2 head keyframes, 1 EnableAnimTracks): check whether
  DisableAnimTracks went out at the lock (M4-047) before calling it robot-side (M5-036).
- PARAMETERS: blinks not widely spaced (operator "no") although readback 44000/45000: trace whether the blink path reads
  these parameters (M5-062/M5-028).
- LAYERS: faces 13 vs audio frames 20 (rule: a face per audio frame); operator unclear.
- DRIVING: DriveToPose FailedTraversingPath; default phases not ordered: M12 path, judge separately.
- events.jsonl MISSINGs are from other layers (M11, M1-058 locale); no handler faults.

**Handoff (manager, 2026-10-20; Claude out of usage until Thursday).** The operator ran `m5-hw` (first run) and reported
"honestly, nothing is seeming quite right", answering the neutral-eyes prompts `unclear`. No specifics yet. Whoever
judges the bundle (`acceptance/hardware/<timestamp>-M5-HW-*`):
1. Follow AGENTS.md "Required workflow for every hardware failure" before any code change: trace each failing check's
   production path, classify (implementation bug, source gap, hardware-only unknown, blocked external, test/harness bug).
2. Judge from `frames.jsonl` (raw, authoritative), `messages.jsonl`, `events.jsonl` (startup logs, MISSINGs,
   handler faults, AnimationState counters) and `result.json`, against the source rows in
   `research/20261019-m5-settling-b.md` "Hardware coverage" and each check's `verdictRule`. Operator answers are
   observations, not verdicts.
3. Look first for a common cause (every check looking wrong suggests one): face images reaching the robot at all
   (AnimFaceImage on the wire, ClientDropCount), animation Start/End and robot echoes, handler faults, readiness.
4. TEARDOWN: judge animation traffic between Dispose start and end (ClearSendBuffer 0x0057AF58), not "no data after".
5. Write the judgement as the bundle's README (model: `hardware/20261019-182707-M3M4-HW/README.md`); fixes become
   jobs; M5 is ACCEPTED only when every check is as source and M5-036 is observed.

**Update (2026-10-20): M5 hardware script reviewed** (`hardware/scripts/m5-hw-1.md`, tool `m5-hw`, built by Codex). Approved
for the operator's run after one manager fix: LOCKED_HEAD locked mask 4 (BODY_TRACK) instead of HEAD_TRACK (1). Noted for
judging: TEARDOWN's "no data after Dispose" is near-tautological (the transport is closed); judge the clip's animation
traffic between Dispose start and end (ClearSendBuffer 0x0057AF58) from frames.jsonl instead.

**Update (2026-10-19): B-M5-23 Opus check PASS** (`research/20261019-b-m5-23-check.md`). M5-011, M5-014, M5-030 settled
EXACT_SOURCE (mood and mood clock are M7-026's by the cross-layer rule). M5 holds no gap records; left: M5-036 and the
M5 hardware script (pass B's 11-item list), the operator's run, ACCEPTED.

**Update (2026-10-19, settling passes adopted).** Both FAIL on one defect (`research/20261019-m5-settling-a.md`, `-b.md`):
the group draw (M5-011, M5-014) and live idle (M5-030) read the raw reported head angle with a 0 fallback, where the
engine reads robot+0x2FC (`Motion.HeadAngleRad`, −25° until calibrated). The manager confirmed it in the code.
- **Settled EXACT_SOURCE (20):** M5-001, 008, 009, 010, 013, 016, 027, 037, 038, 044, 051, 052, 053, 054, 055, 059, 062,
  063, 064, 065. Stale `unresolved` text marked superseded; exclusions recorded by the manager (inputs our system never
  produces: non-strict JSON grammar and G5b Debug text, non-shipped PNG/stoi input, B-M1-7 S3); tags added for M5-037/038.
- **Held:** M5-011, M5-014, M5-030: job B-M5-23 (with the queued cleanup). M5-036: the hardware run.
- **Records:** M5-020 corrected to `live_path: true` (the public expression API; operator decision 2026-09-29) and M5
  re-approved; M5-042's SetSdkStatus half checked (SDK-only); M5-061 wording; handoffs noted on M8-015 (timer lead),
  M8-021 (sleep seam), M7-026 (driving mood use).
- **Next:** B-M5-23, its check, settle the three; then the M5 hardware script from pass B's 11-item coverage list.

**Update (2026-10-19): B-M5-22 Opus check PASS** (`research/20261019-b-m5-22-check.md`); six queued items
(log order, a depth-limit reload count, two tests, reach labels, duplicate test entries, ResourceDirectory candidates)
go to the next M5 job. Next: settling pass A.

1. **Now:** the Opus check of B-M5-22 (`research/20261019-b-m5-22-check.md`).
2. Two read-only Opus settling passes at one HEAD, run one after the other (A: loading, group choice, face; B:
   streaming, idle, neutral, driving, teardown, plus the policy confirmation and the hardware coverage list). Output:
   `research/20261019-m5-settling-a.md`, `-b.md`.
3. The manager adopts them: settle what holds, split cross-layer pieces, jobs for what doesn't, research requests for
   the narrow unread pieces.
4. Builds and their checks; settle.
5. The M5 hardware script (M5-036 and the pass-B coverage list); the operator's run; ACCEPTED.
