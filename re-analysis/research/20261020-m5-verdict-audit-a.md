# M5-HW verdict audit A — 2026-10-20

Read-only audit of current M5Hw.cs / M5HwAnalysis.cs at 4652ecb3. Scope: TAGS, ABORT,
KEEP_ALIVE, LOCKED_HEAD, LAYERS, LIVE_IDLE. No code, manifest, inventory or hardware changes;
no builds/tests. Fresh lief/capstone decoding of the shipped ELF is in 20261020-m5-verdict-audit-a-native.txt alongside this report. Decompiled files were navigation only.

The test path is M5Run.Check -> public PlayTracked/Stop/idle/lock/distortion API ->
existing scheduler/generator/FIFO/transport -> capture and diagnostic join -> helper
analysis -> caller overrides -> JSON result. Every caller override in these six checks
was included. Physical answers are not engine expectations: current M5-036 expressly
leaves firmware reactions and display unresolved.

## Criterion inventory and disposition

| Code | Existing criterion | Primary support and limit | Disposition |
|---|---|---|---|
| M5Hw.cs:117-125 TAGS | First three outbound non-FF Start tags are 1,2,3 | M5-008 tag counter initialized zero (ctor0x579FD0..D4); SetStreamingAnimation increments/skips reserved values0x57B2D4..F6; SendStart copies tag0x57C410..424. StreamLayers also allocates from that counter0x57C60C..624. Therefore first three starts are a bounded outgoing-tag check, not proof that exactly those starts correspond to the three requested clips. | Sourced outgoing-tag check; preserve request/stream attribution limitation. |
| M5Hw.cs:125 TAGS | Each Start gets matching robot Started then Ended echo | M5-036 HARDWARE_ONLY: firmware2457 not shipped. Native consumers establish how the engine processes messages, not whether/when robot sends them. | Observation only; cannot gate engine verdict. |
| M5Hw.cs:358 / Complete347 | Three tickets complete before local timeouts | Engine animation API completion may depend on firmware echoes; local timeout is harness bound. | Capture/run completeness, not engine behavior failure. |
| M5Hw.cs:88-114 ABORT, KEEP_ALIVE | Active prior Start; exactly one valid outbound Abort after cancel | M5-023: Abort with nonzero tag broadcasts0x57B3F2..410; sender0x517DE4..7E0A sets r2=1/r3=0; ctor0x7AA392 movs r1,#0x8d, strb r1,[r0]; empty Abort body Size0x7A47DC movs r0,#0. | Sourced cancellation-send check. Prior active Start/observed command establishes exercised path; absence is incomplete evidence. Reliable retransmissions must count once. |
| M5Hw.cs:104-106 ABORT | No End, no new Start, <=1 face/audio in fixed400ms after wire Abort | M5-023 clears start/end0x57B578, keeps FIFO and later drains0x57D168..174. M5-007/025: one pending built frame because nonempty FIFO refuses next build0x57CC72..7C; frame audio/face0x57C98C..CA30. BUT M5-010/028 neutral replay is gated by now-minus-lastStream>timeout0x57CF90..BE, not wireAbort+500ms. Capture has no native lastStream/generation identity; a fresh neutral/layer stream can occupy this horizon. | Counts/order retained observations; fixed400ms is harness sampling, not an unconditional oracle. Do not claim these counts prove leftovers/early-neutral defects. One valid Abort remains independently judgeable. |
| M5Hw.cs:376 KEEP_ALIVE | Face present between wireAbort+400 and+2000ms | Neutral's source gate above, plus budget/drain timing; no universal host-wall deadline established. | Coverage observation; absence in this horizon is not engine failure. |
| M5Hw.cs:385-386 ABORT and KEEP_ALIVE | SVG exists, clipStopped=stopped and restingEyeAppearance=resembled-reference | M5-010 establishes reset source/neutral request; M5-032 establishes encoded rendering, not firmware visible eye geometry under later layers. M5-036 is HARDWARE_ONLY. SVG is harness aid. | Remove from behavior verdict; keep file availability separately as observation setup, keep all answers. |
| M5Hw.cs:392-407 KEEP_ALIVE | --allow-drive, 10s live interval, >=2 paired FF bursts, bodyObservation=seen | Source gives FF InitStream0x57D3FA..3FE and endSent/noFrames/FIFO0x57D414..426 before restart0x57D44A..44C. Count>=2 and10s are sample coverage, no source fixed burst rate. Body physical visibility depends on firmware, locks, gates, random speed and viewing. | FF ordering is sourced; interval and pair count are coverage; human wiggle answer observation only. |
| Analysis.cs:610-651 LiveBursts | StartFF, End, no nesting, no Abort; final open burst allowed | M5-027 shared idle tail0x57D3EE..44C; M5-030 generation before it. No cancellation in marked harness window. | Sourced lifecycle ordering in captured window. Do not assert restart time. |
| M5Hw.cs:410-440 KEEP_ALIVE | Short priming clip, no explicit play/no idle/persistent dart layer prerequisites; >=10s with faces and no End/Abort, at most one nonFF Start | M5-028/029 StreamLayers0x57C52A..556 only ends after no layers and empty FIFO; startSent gate/tag allocation0x57C606..624; face flag0x57C63C..646. | Source conditional lifecycle check. Prerequisite/window/face-presence failures are capture/coverage failures. Sampled public snapshots are not atomic continuous proof, explicitly recorded already. |
| M5Hw.cs:443-466 / Analysis.cs:447-487 LOCKED_HEAD | Disable(mask1) before clip, no early Enable, own Enable after unlock, outbound head keyframe | M4-014 LockTracks builds mask and sends0x64014E..188; UnlockTracks0x63FF94..B4. M5-035 unconditional head track buffering0x57C9D4..9E2 with no lock branch. | Sourced wire check; command presence is coverage. |
| Analysis.cs:468-472 LOCKED_HEAD | Require robot state count>=1, head telemetry count>=2; distinguish firmware mask enabled/head changed | Native handler stores/reports received values; firmware behavior is M5-036. No engine promise of sample count per wall window. | Samples and physical/mask interpretation observations; missing sampling is capture completeness only, not rejection of otherwise valid lock wire check. |
| M5Hw.cs:466 LOCKED_HEAD | headStayedPut=yes required | M5-036 explicitly owns unknown locked-track suppression. | Observation only. |
| M5Hw.cs:483-505 / Analysis.cs:507-555 LAYERS | Fully joined frame evidence; output one face iff face-active flag, no face iff false | M5-029 null animation branch0x64F1D4..204; layer return OR into flag0x64F222..22C; StreamLayers checks flag0x57C63C..646. | Sourced conditional wire rule. Unknown/unjoined hook evidence must remain incomplete. Hook proves the production decision/output relationship; not independent correctness of random generation. |
| Analysis.cs:535-542 LAYERS | One Start/End, no Abort, last row End; drained, face/backpack seen and assignedFaces==faceCount | M5-028/029 StreamLayers0x57C52A..556/606..624 buffers one Start, ends after layers/FIFO drain. Audio before layered lights and conditional face0x57C5CA..646. | Lifecycle/association sourced conditional rule. Draining, nonempty track presence and finite sample capture are coverage. No exact random frame count/visual shape proven. |
| M5Hw.cs:501-502 LAYERS | Face change/backpack change answers | Already retained without override. M5-036 governs unknown firmware display. | Keep as observations. |
| M5Hw.cs:508-513 / Analysis.cs:845-909 LIVE_IDLE | Head keyframe must be four bytes, paired to prior motion-head state; body and lift present in12s | Native head construction0x57D83E..85C: f32 motionHead*0x42652EE1, vcvt.s32, ctor. GetStreamMessage0x4F8C12..4A: durationu16 and signed angle byte. Gates0x57D606..6BC include movement/locks/carry/timers, so fixed12s does not promise all tracks. | Packet shape is sourced. Track/state presence is coverage; no-head/body/lift is not engine failure. |
| Analysis.cs:863-867 LIVE_IDLE | Count deviations>6deg using nearest delivered state | Native uses build-time head input0x57D83E..842 and per-message variability0x4F8C12..2E. Nearest delivered sample is not that exact state. | Already observations only; retain. |

## Literal primary checks for manager

- 0x57B2D4 ldrb.w r1,[r4,#0x70]; B2D8 adds r0,r1,#1; B2DC cmp r1,#0xfd;
  B2E0 bhi B2D8; B2E6 strb.w r0,[r4,#0x70]; B2EC InitStream thunk.
- 0x57B3F2 ldrb.w r0,[r4,#0xa0]; B3F6 cbz B418; B410 blx r7 broadcast.
- 0x517E06 movs r2,#1; E08 movs r3,#0; E0A SendMessage thunk.
- 0x7AA392 movs r1,#0x8d; A394 strb r1,[r0]; Abort payload Size0x7A47DC movs r0,#0.
- 0x57B578 strh.w r1,[r4,#0x71] with r1=0 clears start/end; no FIFO reset in this tail.
- 0x57CF90 vldr s2,[r4,#0x88]; CF94 vldr s4,[r4,#0x1c0]; CF9A vsub.f32 s0,s0,s2;
  CFA4 vcmpe.f32 s0,s4; CFBE bne CFF6; CFD2 SetStreamingAnimation(neutral).
- 0x57C63C ldrb.w r0,[sp,#0x324]; C640 cbz C64A; C646 BufferFaceToSend.
- 0x57D83E vldr s2,[r5,#0x2fc]; D842 vmul.f32 s16,s2,s0; D84E vcvt.s32.f32 s0,s16.

No manifest record is contradicted by this audit. The mismatch is between the test's
verdict gates and the records' stated limits. M5-036 remains HARDWARE_ONLY; source
wire checks do not settle it. A robot rerun is not performed by this audit.

## Current manifest quotations

The following table is generated from the current manifest, not recalled from older notes.

### M5-008 — EXACT_SOURCE

One streaming animation: refuse or interrupt, tags 1..0xFE returned, loops with the same tag, live forced to 1 loop, ReplayLastAnimation

Evidence: A4 0x0057B17E..0x0057B1B2; A5 0x0057B1DE..0x0057B24A; A6 0x0057B2FC..0x0057B304; A8 0x0057B29C..0x0057B2C8; A9 0x0057B2D4..0x0057B2F6, 0x0057B660..0x0057B672; A13 0x0057CFF6..0x0057D036; A27 0x0057ED0E..0x0057ED2A; re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-008 (Opus settling pass, manager-adopted 2026-10-11).; re-analysis/research/20261011-m5-fix-rows.md section 5 (Opus fix rows, manager-adopted 2026-10-11).; re-analysis/research/20261012-m5-x9-extraction.md section 4c: sVerifyFailedReturnFalse 0x0080D9A4 logs sErrorV "VERIFY(%s): ", sets errG with no debug break; sDumpCallstack 0x0080DA08 is a no-op (0x0080B0A6 bx lr); the flush 0x0080DA24 goes MultiLoggerProvider::Flush 0x006673BA -> DasLoggerProvider::Flush 0x006671F4 -> libDAS DASForceFlushNow (M3-051); returns 0.; re-analysis/research/20261012-m8-play-animation-actions-check.md addition 2: SetStreamingAnimation(null) stores +0x73 = 1 when an old stream existed (0x0057B2FC..0x0057B300), and with an idle streamer (+0x38 == 0) still calls Abort() (0x0057B1DE -> 0x0057B242).; re-analysis/research/20261012-m9-029-custom-song.md check: SetStreamingAnimation stores +0x38 = anim at 0x0057B24A before the custom branch; the live/tag/InitStream tail (0x0057B29C..0x0057B2F6) runs on whatever +0x38 holds; ReplayLastAnimation (A27, 0x0057ED0E) replays by the name at +0x54 through the string overload with custom = false (0x0057B160..0x0057B16C).

### M5-010 — EXACT_SOURCE

Neutral face: the first ProceduralFace keyframe of the first clip of ag_neutral_face; reset data and layer base; replayed after abort-to-nothing and RemoveIdle

Evidence: A2 0x0057A0D4..0x0057A20E, ProceduralFace::Reset 0x0058359C; AnimationTriggerMap.json:1308-1309; A30 0x0057BA60..0x0057BD6E; A31 0x0057CF6A..0x0057CFF2; R-ANIM pre-extraction part 1 item 3 P1..P8: _resetData GOT 0x103ED4C, Reset 0x0058359C, TLC::Init 0x0064EDE2, streamer ctor 0x0057A0D4..0x0057A288, default face 0x00583660; re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-010 (Opus settling pass, manager-adopted 2026-10-11).; re-analysis/research/20261011-m5-fix-rows.md section 7 (Opus fix rows, manager-adopted 2026-10-11).

### M5-023 — EXACT_SOURCE

Abort: AnimationAborted broadcast -> AbortAnimation 0x8D reliable; state cleared; buffer kept and flushed by the next no-animation Update unless InitStream drops it; no EndOfAnimation

Evidence: A22 0x0057B3F2..0x0057B426; A23 0x005276E8..0x0052771A, 0x0052C22E -> 0x00517DDE..0x00517E0A; A24 0x0057B442..0x0057B58A; A25 0x0057D122..0x0057D1DE; R-ANIM pre-extraction part 1 item 8 8a..8c: choice 0x0057B418..0x0057B430, reset 0x0057B562; re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-023 (Opus settling pass, manager-adopted 2026-10-11).; re-analysis/research/20261011-m5-fix-rows.md section 12 (Opus fix rows, manager-adopted 2026-10-11).; re-analysis/research/20261012-m8-play-animation-actions-check.md addition 2: SetStreamingAnimation(null) stores +0x73 = 1 when an old stream existed (0x0057B2FC..0x0057B300), and with an idle streamer (+0x38 == 0) still calls Abort() (0x0057B1DE -> 0x0057B242).

### M5-027 — EXACT_SOURCE

Idle animations: the idle stack, PushIdle/RemoveIdle, idle InitStream with tag 0xFF, ProceduralLive, the no-animation path

Evidence: A1 0x0057A064..0x0057A0AC; A28 0x0057D03A..0x0057D060, 0x0057D122..0x0057D1E2; A29 0x0057D064..0x0057D448; A30 0x0057B914..0x0057BD6E; R-ANIM pre-extraction part 1 item 9 9a..9c: +0x64 writers 0x00579FC6, 0x0057B926, 0x0057BD66, 0x0057D022, 0x0057D3F6; drain 0x0057D168; HasResponse 0x00670AD0; re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-027 (Opus settling pass, manager-adopted 2026-10-11).; re-analysis/research/20261011-m5-fix-rows.md section 14 (Opus fix rows, manager-adopted 2026-10-11).

### M5-028 — EXACT_SOURCE

Keep-alive: timeout 0.5 s, KeepFaceAlive darts and blinks, default params, continuous StreamLayers while a persistent layer exists

Evidence: A31 0x0057CF6A..0x0057CFF2; A32 0x0057A050, 0x0057E010; A33 0x0058D374..0x0058D558; A34 0x0057DB40..0x0057DCD6; gap1 K1..K9 GenerateEyeShift 0x0058D10E.., LookAt 0x00584158..0x0058428A, GetNextBlinkFrame table 0x00C5AAD8; gap2 Q1.7..Q1.10 StreamLayers 0x0057C4F6..0x0057C680; re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-028 (Opus settling pass, manager-adopted 2026-10-11).; re-analysis/research/20261011-m5-fix-rows.md section 15 (Opus fix rows, manager-adopted 2026-10-11).

### M5-029 — EXACT_SOURCE

Track layer manager: tags, AddLayer/AddPersistentLayer, persistent hold, AddToPersistentLayer, the Remove cross-fade, HaveLayersToSend, frame flags

Evidence: gap1 L1..L8 0x0058E630..0x0058EC6C; gap2 Q1.1..Q1.6 0x0064F4FE..0x0064F518, 0x0058E66E..0x0058E77C, 0x0064EFA8..0x0064F22C; gap3 K5 Remove end keyframe; re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-029 (Opus settling pass, manager-adopted 2026-10-11).; re-analysis/research/20261011-m5-fix-rows.md section 16 (Opus fix rows, manager-adopted 2026-10-11).

### M5-030 — EXACT_SOURCE

Live idle (UpdateLiveAnimation): gates, body/lift/head wiggles with their parameters, LiveIdleTurn eye shift, lock and carry checks

Evidence: A35 0x0057D5F8..0x0057DA82; gap1 K2 0x0058CFCE..0x0058D04A; K10 0x0064F3C8..0x0064F498; R2..R4; re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-030 (Opus settling pass, manager-adopted 2026-10-11).; re-analysis/research/20261011-m5-settling-pass.md section 5 and re-analysis/research/20261011-m5-open-reads.md (Codex rows, Opus spot-check PASS 2026-10-11): PL1-PL19, PLU: ProceduralLive 0x198 is pushed only through GameToEngine PushIdleAnimation (tag 174, 0x0057EC1A..0x0057EC32); no fixed native caller or shipped config produces 0x198.; re-analysis/research/20261011-m5-fix-rows.md section 17 (Opus fix rows, manager-adopted 2026-10-11).

### M5-031 — EXACT_SOURCE

Glitch: AddGlitch face and backpack layers, GetNextDistortionFrame table, ScanlineDistorter, per-row shift, AddOffNoise

Evidence: gap1 G1, G3..G11 (0x0064EDE8..0x0064EEFE, 0x0053A9A8..0x0053AB1E, 0x0053A0B0..0x0053A898, 0x00585DFE..0x00585E94); gap2 G1..G7 GenerateGlitchLights 0x0058CB64..0x0058CD2A; re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-031 (Opus settling pass, manager-adopted 2026-10-11).

### M5-032 — EQUIVALENT_IMPLEMENTATION

DrawFace: 64x128 canvas, the face transform via shipped cv::warpAffine INTER_NEAREST BORDER_CONSTANT 0, row extent, interlace clearing, scan-line shift

Evidence: E1 0x00585B30..0x00585D9A; E2 0x00585D9C..0x00585E94; gap1 D4, D5; gap3 C1..C6 warpAffine 0x00081850.., invoker 0x0007FC60..0x0008016A, remapNearest 0x00072C38..0x00072D8C (matches stock 3.1.0); R-ANIM pre-extraction part 1 item 10 10a..10e: GetTransformationMatrix 0x00584FF8, InputArray 0x00585C84, warpAffine imgproc 0x0008193C; re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-032 (Opus settling pass, manager-adopted 2026-10-11).; re-analysis/research/20261011-m5-fix-rows.md section 18 (Opus fix rows, manager-adopted 2026-10-11).

### M5-035 — EXACT_SOURCE

The streamer never reads enabledAnimTracks or skips a locked track; locks only through DisableAnimTracks/EnableAnimTracks; the live idle checks MovementComponent locks

Evidence: B1 0x00537FFC..0x00537FFE; B2 0x00513000..0x0051304E; B3 0x0057C94E..0x0057CA7A; B4 0x0057D636, 0x0057D664, 0x0057D69E; re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-035 (Opus settling pass, manager-adopted 2026-10-11).

### M5-036 — HARDWARE_ONLY

Robot-side animation behaviour: AbortAnimation handling and leftovers, Start without End and the unbounded keep-alive stream, locked-track suppression, animStarted/animEnded echo

Evidence: A23, A25, B3, gap2 Q1.10; robot firmware 2457 is not shipped

### M4-014 — EXACT_SOURCE

Direct-drive track locks: DisableAnimTracks / EnableAnimTracks around DriveWheels, MoveHead and MoveLift

Evidence: MA1 DriveWheels 0x0063ED82..0x0063EE9A, MoveHead 0x0063F4F6..0x0063F5D2; MA1-lift MoveLift 0x0063F73A..0x0063F92A; MA2 DirectDriveCheckSpeedAndLockTracks 0x0063EFB0..0x0063F0B8 (1e-5 at 0x0063F0F8); MA3 LockTracks 0x006400D0..0x00640188 (0x9D); UnlockTracks 0x0063FE92..0x0063FFB4 (0x9E)

### M4-047 — EXACT_SOURCE

MovementComponent::UnlockTracks miss path: the info log, PrintLockState, and EnableAnimTracks re-sent for free tracks

Evidence: re-analysis/research/20261013-m12-024-driving-lifecycle.md: UnlockTracks 0x0063FE5C..0x0063FFD8: per bit, find LockInfo by who (first string only, 0x00642508..0x00642574); miss -> sChanneledInfoF "Unnamed" "MovementComponent.UnlockTracks" (0x640040) "Tracks 0x%x are not currently locked by %s" (0x640060) with the full mask and who, then PrintLockState(); an empty set ORs the bit; mask != 0 sends EnableAnimTracks(mask). Reached by the driving handler's double unlock (PlayEndAnim) and a cancel during the end animation.

### M5-057 — COMPATIBILITY_POLICY

PushIdleAnimation(ProceduralLive) as the app-supplied input that starts live animation (app input)

Evidence: re-analysis/research/20261012-m5-settling-2.md: UpdateLiveAnimation 0x0057D5F8..0x0057DA82 runs only when the idle stack top is ProceduralLive, which is pushed through the game-to-engine PushIdleAnimation (tag 0xAE) by the app (M5-030).

## Follow-up: TAGS freshness qualification

The interactive reference-picture pause alone is not evidence of pre-TAGS keepalive.
Native constructor initializes lastStream=-FLT_MAX (0x579FDE..FE2); the entire
keepalive/TLC block requires lastStream>0 (0x57CF6A..76). Current harness setup calls
SaveNeutralReference (render to SVG), not Play or IdleOn, before TAGS. This bounded
read does not establish a legitimate earlier startup play. Therefore do not report
that pause as a proven counter-consumption bug.

The initial TAGS criterion row is conditional on known fresh allocator state with
no intervening allocation. StreamLayers increments the same counter0x57C60C..624;
a fresh connection by itself is not a general proof that a window starts at tag1.
The current grader only gets Wire(checkStart), so it does not prove that condition
from earlier captured history. In addition, first-three arbitrary Starts are not
attributed to the three requested clips.

An independently source-backed bounded observation is returned-request-tag versus
outbound-Start tag: SetStreamingAnimation returns its allocated counter at0x57B2F2,
and SendStart copies the stream tag at0x57C410..424. Tickets expose the returned tag.
Associating each ticket/request window with its Start establishes that delivery;
any other starts and allocator sequence can remain recorded separately. This does
not claim to verify the entire allocator or to recover firmware echoes.
