# M5-HW first-run judgement — 2026-10-20

Temporary manager/verifier: **Codex**, explicitly authorized by the operator until Claude returns.
Bundle: `re-analysis/acceptance/hardware/20261020-074223-M5-HW-2cc1cf`.
Run commit: `c2f2dfbdcd2a80674f96a50fd2eda0802b9f31d6`.
Review checkout: `e4767d022ad2f04d4b445b3efa22d8d748b471d3`.
`git diff c2f2dfbd HEAD -- cozmo-stack/src` is empty.

**Judgement: NOT ACCEPTED; corrected harness and operator rerun required. No record is settled, accepted,
reclassified, or upgraded in verification by this report.** The first-read leads were useful questions, not findings.
In particular, the proposed live-idle restart defect is **not supported**: native ProceduralLive explicitly ends and
reinitializes bursts. Its “no End” hardware oracle is wrong. Fixing the stack to meet that oracle would introduce a
source contradiction.

This review establishes several harness defects, but **no stack implementation bug requiring a behavioral change**.
The jobs below therefore repair the harness. Do not invent stack jobs to satisfy the initial suspicion. Exact
finite-glitch frame counts, physical visual perception and the M12 drive failure remain open as specified below.

## Method and common-cause check

Read AGENTS.md, PROJECT_STATE.md, M5-blockers.md, research/job rules, run result and the production callers before
proposing changes. Separate read-only extractors traced the bounded native paths; the manager compared their
instruction rows to C#, current manifest records and raw packet order. No extractor implemented code. The fresh
instruction companion is `20261020-m5-hw-1-native.txt`; literal-pool fallthrough in a dump is not evidence of executed
instructions. Primary binary SHA256:
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
Current record titles/status/evidence are preserved verbatim in `20261020-m5-hw-1-records.json`.

`20261020-m5-hw-1-replay.py` independently parses raw frame headers and ordered submessages, removes reliable
retransmissions and reproduces **all 38,805 messages** in messages.jsonl, including payloads and timestamps.
Within-frame ordinal matters: timestamps alone cannot order an Abort against preceding data in the same datagram.
References below use one-based lines of frames.jsonl. The script neither connects to a robot nor runs the stack.

Readiness event at 4546.1505 ms reports assets and calibration ready; no run errors or handler faults found.
Face images reach the wire; all 5,670 raw/deduplicated AnimationState messages report ClientDropCount=0.
This excludes a blanket “no face traffic” or reported client-drop explanation, but does not prove correct firmware
display. M5-036 stays HARDWARE_ONLY. No common stack fault has been demonstrated.

## Per-check judgement

| Check | Classification and disposition |
|---|---|
| TAGS | Observed source-compatible tags 1,2,3 with ordered corresponding started/ended echoes. Extra echo4 belongs to later keepalive traffic in the broad window. No identified defect; rerun still required for acceptance. |
| ABORT | **Test/harness bug:** host-call timestamp counts pre-Abort transport backlog as post-Abort leftovers. One pending audio/face frame remains after wire Abort, as permitted. Physical observation “stopped-then-neutral” retained. |
| KEEP_ALIVE | **Test/harness bug:** conflates ProceduralLive bursts with no-idle persistent-layer stream. Thirteen FF Start/End pairs are not themselves a defect. Neutral visual observation “continued” remains unresolved; distinguish movement idle, neutral replay and persistent face tests on rerun. |
| LOCKED_HEAD | **Harness observation/mapping defect; firmware question retained.** Disable reached robot, robot reported head disabled, and head telemetry stayed constant during the locked clip. Operator “no” is not enough to override these observations or settle firmware behavior. |
| AUDIO | **Harness measurement defect:** pairs old delivered F1 AnimationState payloads with later streamed totals. Framing/rate observed; snapshot maxima are not send-admission failures. No proven M3-012 stack bug. |
| LAYERS | **Test/harness bug:** finite glitch does not require face/audio aggregate equality. Native face emission is conditional. Exact 13/20 full trace is not certified; needs discriminating rerun/regression. |
| LIVE_IDLE | Observed valid head/body/lift frames; 11 paired head samples within default ±6 window. Pairing is not exact build-time state. Uncalibrated coverage remains offline only. Restart oracle belongs to KEEP_ALIVE correction. |
| PARAMETERS | **Inconclusive physical observation, harness lacks objective blink evidence.** Writes and downstream consumer match source. Diagnostic decoded closures are about44.1s apart, consistent with settings. No established ignored-parameter stack bug. |
| DRIVING | **Existing cross-layer implementation/source-recovery uncertainty (M12-024/M13); harness phase oracle bug.** FailedTraversingPath does not imply failed M5 handler. Failed/cancelled Start need not launch Loop. No invented planner correction. |
| SLEEP | Observed known fallback/MISSING and no new named sleep play. M8 boundary remains explicit; no new M5 defect or acceptance. |
| TEARDOWN | **Harness coverage defect:** post-Dispose silence is insufficient. Actual interval shows expected abort/stop path, an unsent face discarded, no new face/audio/end flush. Cannot infer all destructor correctness from a closed transport. |

## Traces and checked correction boundaries

### 1. Live idle Start/End: first queued correction, to the harness

Entry: public PushIdleAnimation(ProceduralLive) -> idle stack -> scheduler Advance -> UpdateIdle -> live generator ->
InitStream/UpdateStream -> budgeted sink -> reliable transport -> robot.

Native Update at `0x57D068` compares top with `0x198`; `D074..7C` selects the embedded live animation and enables it;
`D080` calls UpdateLiveAnimation **before** the idle tail. New/different/uninitialized idle calls InitStream(FF) at
`D3EE..412`. Crucially `D414` loads endSent, `D41C` calls HasFramesLeft, `D422..426` tests empty send buffer,
and `D44A -> D3FA` reinitializes FF when the prior burst finished. HasFramesLeft `0x577E48..ECA` tests track iterators,
with no live exemption. UpdateStream `0x57CB44..80` sends End after tracks/audio/FIFO finish; it does not exclude live.
Init `0x57B682..7F8` resets stream flags and creates audio, preserving keepalive layers for live.

C# `AnimationScheduler.UpdateIdleLocked` common tail (around1211), `UpdateStreamLocked` (around1442) and
`StreamAnimation.HasFramesLeft` implement that structure. M5-027 EXACT_SOURCE “Idle animations: the idle stack,
PushIdle/RemoveIdle, idle InitStream with tag 0xFF, ProceduralLive, the no-animation path”, evidence A1/A28..A30,
already owns it; inventory appendix L8 explicitly describes FF bursts.

The distinct persistent-layer path is M5-028: empty idle stack -> StreamLayers; `0x57C530..556` only ends when there
are no layers. A persistent layer can prevent End indefinitely. **Do not equate this with ProceduralLive.**
Queue B-M5-24 to split those scenarios and replace the false oracle. Add M5-028/030/029 coverage where exercised.
The current count alone does not prove every restart time exact.

### 2. Abort boundary

PlayTracked -> scheduler Stop -> SetStreamingAnimation(null) -> Abort -> AnimationAborted -> Robot AbortAnimation
-> reliable0x8D -> future no-animation Update drains retained FIFO -> delayed neutral replay.
Native `0x57B242..24C/B2FC..300` aborts and records neutral replay; `0x57B3F2..426` broadcasts; `B562..58A` clears
start/end and audio **without clearing FIFO**. Consumer `0x5287F0..7FE -> 0x517DDE..7E0A` sends reliable/nonhot Abort.
`0x57D122..174` drains leftovers; `D1BC..1DE` cannot send End with startSent cleared.
`0x57CF6A..CFF2` supplies the last-stream timeout gate and neutral play.
M5-023 EXACT_SOURCE explicitly owns that entire abort/retained-buffer/no-End path (A22..A25); M5-010 owns neutral.

At host cancel7715.5276, the harness starts its window too early. Raw line564 at7734.5489 contains reliable
seq391 audio,392 face,393/394 unrelated messages, **then seq395 Abort**. Line569 at7803.0207 contains
seq396 audio,397 face after Abort. Thus the alleged two leftovers comprise one pre-Abort frame and one post-Abort
frame. No End in the quiet window. C# GradeAbort drops submessage ordinal and counts both.
B-M5-25 must preserve wire order and separately record pre-command backlog. Do not change Stop/drain behavior.

### 3. Locked head

Motion.LockTracks(mask1) -> first-holder insertion -> DisableAnimTracks(1) -> PlayTracked continues to stream heads
-> robot suppression -> harness UnlockTracks -> EnableAnimTracks(1).
Native `0x64014E..158` adds only newly locked bits; `0x64017C..188` sends0x9D reliable/nonhot.
Unlock `0x63FF7A..88/FFA8..B4` emits0x9E for empty sets. InitStream `0x57B674..82C` has no track-enable call.
M5-035 explicitly says streamer does not suppress locked track messages.

Raw line3502: 9D01 at47499.5065; heads at48353.8598 and48760.9942; Enable9E01 at49508.412, **after** the clip.
Inbound F1 enabled tracks becomes254 from47538.0228 (head bit clear); reported headRad is constant
`-0.12468681` throughout the measured locked clip. The single Enable counted by the harness is its own unlock,
not an InitStream re-enable. Prompt occurs after unlock and a wait, so observation is not phase-bounded.
B-M5-27 must bracket lock/play/unlock, preserve F1 mask/head telemetry and add M4-014/M5-035 references.
M4-047 alone describes unlock-miss behavior, not the lock producer. No change to lock semantics is warranted.
Physical movement after unlock and firmware execution detail remain M5-036 questions for rerun.

### 4. Audio counters

F1 handler -> EngineRobot played counters -> sink properties -> RefreshBudget -> FIFO admission -> successful
send accounting -> transport. Native `0x537FE0..FF0` stores played counters; `0x57C6F6..7AC` computes byte allowance
max(0,min(30000,played-streamed+8192)) and frame allowance max(0,played-streamed+14).
`0x57BF96..BFA0` rejects messages exceeding allowance; `BFB0..BFCC` updates budget/counters only after successful
send. End goes directly through `0x57C464..496`, counts one frame/Size and bypasses admission.
Current M3-012 EXACT_SOURCE evidence explicitly covers all four portions.

Production C# follows that chain. However M5Hw's public Message callback reads stream totals at **delivery**;
CozmoEngine.TickAt finally drains app delivery **after** processing received messages and updating the streamer.
Several old F1 payloads therefore get the same newer totals. Example at61115.292..294 ms:
streamFrames1565 paired successively with audioPlayed1549,1550,1551 (16,15,14 ahead).
At60994.957..960, bytes80884 paired with71560,72339,73116 (9324,8545,7768).
The final samples of delivery clusters separated by >2ms have maxima14 frames/7875 bytes. That grouping is a
diagnostic, **not** a new acceptance threshold or proof of every send.

28 samples of745 bytes including tag and144 one-byte silence markers; no malformed audio; approximately29.98Hz;
drop counter unchanged. Queue B-M5-29 for coherent admission/counter-epoch diagnostics, including direct-End
accounting. Do not “fix” the native budget to hide a mixed-epoch observation. M6-042 remains an audio boundary;
this run does not certify Wwise semantics.

### 5. Finite layers

DesiredFaceDistortion pulse -> TLC.Update -> AddGlitch -> separate face/backpack tracks -> ApplyLayersToAnim(null)
-> conditional outputs -> StreamLayers -> FIFO.
Native `0x64EDE8` Update supplies degree; `0x64EFC2/EFD2/EFE4` applies audio/backpack/face.
Null animation skips animation face at `0x64F1D4..1D8`; `F204..22C` ORs the face layer result into face flag.
Stored base face alone does not set it. `0x57C63C` loads that flag, `C640 cbz` skips face send, `C646` buffers it.
GetFaceHelper `0x58CD90..A4` returns false for empty/not-due, `CDCA..CE18` advances without face if next already due.
Distortion first trigger is cumulative duration, at least33 (`0x58D69E..D6D0`); backpack independently spans
trigger/duration0/200,200/60,260/60,320/60,380/60 (`0x58CB8C..CD2A`).

C# TrackLayers.ApplyLayersToAnim and StreamLayersLocked use these flags. Raw seq5854 audio,5855 Start,
5856 backpack precede the first face seq5859. Tail includes backpack/audio with no face. Counts13/20/20 and one
Start/End are **not evidence of an unconditional face-send omission**. M5-029 EXACT_SOURCE owns layer flags;
M5-028/031/032 cover streaming/glitch/rendering. Add M5-029 to harness mapping.
B-M5-26 replaces the aggregate-equality oracle with conditional-output evidence and a separate persistent-face
scenario. Exact random-draw-dependent13/20 count remains unverified, not declared correct merely because it can differ.

### 6. Blink parameters

Public SetLiveIdleAnimationParameters -> G2E B4 -> retained handler -> base parameter setter -> same live parameter
map -> Advance keepalive gate -> TLC -> Face.KeepFaceAlive -> next deadline -> generated blink layer -> face wire.
Native registration `0x57A4BA`; validation/default/pairs `0x57E870..8B2`; **base** setter call `E8A2` bypasses derived
max30000 cap. Base initializes once and stores (`0x57C18E..1EC`). Update passes that same map at `0x57CFEC..CFF2`;
forwarder `0x64F2E6..E8`; consumer keys0/1 `0x58D4D0..506`; RNG deadline `D54C..550`.
Old deadline survives. Timer subtracts60 per keepalive call (`D382..38C`), not elapsed wall time.
C# B4 wiring, SetBaseParam, defaults guard and KeepFaceAlive read the same object. Production engine cadence is60ms,
not the offline30Hz seam; no double-cadence fault found.

Readbacks44000/45000 alone prove too little, but face traffic adds evidence. Diagnostic engine-format decoding of
2298 images from90000..167884ms finds low-lit-pixel clusters at92999.1671 (including a blank frame, raw7294) and
137078.7703..137147.0642 (including a blank frame, raw10560..10565): ~44.08s apart. This diagnostic translates
Display.cs DecodeCanvas, uses an explicitly non-acceptance250-pixel threshold, and does not model firmware playback.
It contradicts the simple “parameters not read” hypothesis. Operator “no” remains an observation.
B-M5-28 captures actual blink generation/deadline and displayed-frame evidence separately from eye darts, retains
pre-existing deadline and actual tick count, and avoids deciding correctness from an unanchored yes/no prompt.
M5-062/M5-028 remain unchanged; no parameter consumer repair justified.

### 7. Driving boundary

DriveToPose on ActionList -> native-style CheckIfDone -> PlayStart -> animation completion -> optional Loop ->
terminal path result -> PlayEnd/cancel current phase -> end completion -> drive reevaluation.
Native `0x4F0D80..8C` requires matching start tag **and result0** before loop (`0x4F0E14..2A`).
Failure tail `0x55B0AA..C8` calls PlayEnd; `0x4F0F00..48` cancels start/loop and starts end unless state3/4/5.
End callback sets state4 (`0x4F0DC8..DD4`), releasing CheckIfDone's state3 wait (`0x55AB60..66`).
Pose mismatch plus matching path IDs yields FailedTraversingPath0x04000002 (`0x55ADA8..AECA`, `B056..58`).

Captured start action result0x02000000 is nonzero; no loop is consequently not a contradiction. End tag18 echo at
170052.4587 precedes successful animation-action completion and drive completion170241.492.
Why pose/path processing produced this drive failure is **UNKNOWN here**, across existing M12-024 IMPLEMENTATION_GAP
and M13 planner/pose boundaries named in the captured MISSINGs. Stop that item; no guessed movement fix.
B-M5-31 makes phase expectations conditional on action result and retains an explicitly unassessed M12 outcome.
Successful traversal must still be observed later; a failed traversal cannot satisfy that coverage.

### 8. Teardown interval and other coverage

Host Dispose -> stop ticker -> context RemoveRobots -> robot AbortAll -> animation destruction/ClearSendBuffer ->
loader/hub cleanup -> transport close. AbortAll precedes the no-send streamer destructor (M4-026/M5-022).
Native streamer `0x57AF58` calls ClearSendBuffer; `0x57B070..B0C2` logs/deletes/clears, not sends.

Dispose interval172516.4529..172549.2551: line13228 contains retransmitted older head data; after sequence
deduplication its only new message is3C0000. Line13230 has43,8D,3B,3C0000 teardown commands.
At172544.5196 the log explicitly says “Clearing unsent animFaceImage”. No new0x97 face,0x8E/8F audio or0x9A End
appears during this interval. The Abort belongs to robot teardown before streamer destruction, not a final flush.
B-M5-30 grades this interval and separates already queued/retransmitted transport data from destructor sends.
Add M4-026/M5-022 references; do not silently redefine all Dispose traffic as forbidden.
TAGS/LIVE_IDLE/SLEEP observations remain limited as in the table; no new corrective job is established for them.

## Queue, manifest and acceptance disposition

Queue **B-M5-24..31**, one per harness root cause, in the order above (24 live lifecycle,25 abort boundary,
26 conditional layers,27 locked-head phase,28 blink observation,29 counter epochs,30 teardown interval,
31 failed-drive phase). Each job requires source-derived regressions and an independent verifier; implementation
is left to the separate builder. No production behavior correction is authorized by these jobs.

Every referenced existing ID was checked in the current manifest; additions above repair missing ownership coverage,
not nonexistent IDs. No manifest/inventory change, no new approval, and no status settlement is made.
Current source evidence does not justify reopening records merely to fit the erroneous hardware rules.
M5 stays INVENTORY_APPROVED and M5-036 stays HARDWARE_ONLY/verification NONE.

Regression in this judgement: raw replay equals all38805 recorded messages; report diagnostics reproduce from the
immutable bundle. `fidelity.py --check` passes (706 records); `git diff --check` passes. Production regression tests
belong to the queued builder jobs; production code is unchanged and the full stack suite was not run. No hardware was run.
Publication: this judgement, its three companion files, handoff updates and B-M5-24..31 are published together in the
documentation commit titled `Judge first M5 hardware run and queue harness corrections`; no implementation commit.
Operator follow-up (2026-10-20): B-M5-27 now requires expected-appearance descriptions and marked observation windows
for every prompt, including explained neutral eyes and explicit exclusion of post-unlock movement. Unexpected physical
observations remain open even when a test rule was wrong. Successful driving still must be observed in a later run;
the FailedTraversingPath cause is not guessed.
Remaining uncertainty:
physical neutral/display/track behavior, exact finite-glitch trace, atomic send admission observations and the
M12/M13 traversal root cause. **Nothing is settled or accepted until the operator reruns the corrected checks.**
