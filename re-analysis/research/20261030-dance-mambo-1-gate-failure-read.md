# DANCE-MAMBO-1 full-gate failure: bounded read-only classification

Requested by manager after gate1 at HEAD `b4545be005dc609047673fa5708c501950605549` (2026-10-30). Source reader requested gpt-6.1-sol/medium/forknone; backend unobserved. No code, .NET, git, official records, rerun or hardware changes. PROJECT_STATE was reread first. Primary native binary remains SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. `known.py 0x0057D064` preceded new native instructions; explicit Python313 used.

## Finding

**Confirmed test-harness synchronization defect; the precise schedule/cause of this particular false sample remains UNKNOWN. No production regression or source-recovery gap is established by the saved failure.** `UseEveryDevice` samples `LiveStreamActive` after at most five calls to an offline **engine** tick, but the animation state in this fixture is advanced by a separate animation ticker thread. None of those five engine ticks waits for an animation update, and none calls scheduler.Advance because EngineUpdate returns immediately for a non-production engine. Source establishes that active state is set by streamer Update, not by pushing/appending and not by readiness alone. The harness has no happens-before edge proving even one relevant update before its final assertion. That structural defect is proved by code; whether delayed first update, a ticker failure, an unexpected overwrite or another interleaving caused this occurrence was not captured.

There is no evidence for “the poll missed transient live state.” Do not describe that as root cause. The known live assignment persists as idle state until a distinct writer changes it; the saved log contains no timeline to show any such change. No retry, longer wait, production patch or full-gate rerun is justified as a source-derived fix by this read.

## Actual failing attempt

`.scratch/dance-mambo-full-1.json` records gate commit above, started19:45:44.8873694Z, ended19:54:30.4708445Z, COMPLETED exit1. `.scratch/dance-mambo-full-1-postflight.json` is `[]`. `.scratch/dance-mambo-full-1.log` records6970 passed,1failed,0skipped,6971total and this single failure:

- `EngineAppLayerTests.M1_025_M1_015_CC27_AfterRemovalASecondConnectStartsFromAFreshRobot`,64ms.
- `the live stream never became active; streamingOpen=True`.
- `UseEveryDevice` EngineAppLayerTests.cs2584, called by the test at2659.

At2659, the first connection has succeeded. `rig.Disconnected()` at2660, removal, and second ToSuccess at2668 **have not executed**. This is first-connection setup failure, not an observed failure of reset, removal, reconnect or CC27. Other warnings in the log do not establish a competing live-animation cause.

## Current literal manifest snapshots

Copied from the actual current manifest before classification. The record titles are not paraphrased.

```json
{
  "id": "M1-015",
  "title": "Connection timeout 5 s, and how a lost or failed connection is reported",
  "status": "EXACT_SOURCE",
  "evidence": [
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9; re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "R19 ReliableConnection::HasConnectionTimedOut: now > lastRecv (+0x50) + ConnectionTimeoutInMS (0x00836080..0x0083608C); on timeout ReceiveData(OnDisconnected, 0, addr), delete, Update false (0x00837BCC..0x00837C74); no frame sent",
    "B22 0x00837C50..0x00837C56 OnDisconnected to the receiver; tick lambda sets +0xA1 (0x008383D4..0x008383DC)",
    "B31 HandleDisconnectMessage: +0xA1 -> reason 1 WifiTimeout (0x0062FA34 ldrb.w r0,[r1,#0xa1]); RemoveRobot(id, wasConnecting) -> 2 ConnectionRejected when RCD state was 1 (the transport connect was never answered), else 1 ConnectionFailure, including a drop during the handshake [corrected C6] (0x0062FAEE..0x0062FAF8, 0x0052F23E..0x0052F244)",
    "B32 pending handshake: RobotConnectionResponse(result) and no RobotDisconnected; else RobotDisconnected and $session_id cleared; Robot deleted; the engine does not reconnect (0x0052DCC0..0x0052DD16, 0x0052F248..0x0052F302)",
    "CC23/CB32: HandleDisconnectMessage ignores the message fields, captures the RCD state, writes reason 1 if the timed-out flag is set, emits DAS, resets the reason to 0, clears RCD, then RemoveRobot(id, state == 1) (0x0062FA2E..0x0062FAF8); RIC::HandleDisconnect answers with RobotConnectionResponse {result, 0, 0, -1, -1} unless the response was already sent (0x0052DCC0..0x0052DD1A)",
    "CB33/CC26: RemoveRobot skips the RobotDisconnected broadcast and the $session_id clear when HandleDisconnect answered; either way it deletes the Robot and its RIC (0x0052F248..0x0052F364)",
    "T1–T5: strict timeout/marker/report selection and ordered lifetime handoff only. Component destruction effects belong to the new higher-layer lifetime records in inventory correction A2."
  ],
  "unresolved": "Final M1 Opus pass (2026-10-09, re-analysis/research/20261009-M1-final-opus-pass.md): SETTLED. The report selection and order hold. HandleDisconnect's second gate (external interface +0x14 == null returns 0, 0x0052DCC8..0x0052DCCC) is unreachable: context+4 is the UiMessageHandler built at 0x004EB276 and passed at 0x004EB28C.\nbuilt, awaiting strong verification: B-M11-1 (2026-10-14): Host constructed reset clears new dynamic world origin/root fixtures and diagnostic tail-call counter; native lifecycle not reclassified. See jobs/status/B-M11-1.md for files/lines, binary ranges, tests and every MISSING.\nB-DELIV-1 (2026-10-17), built, awaiting strong verification: RobotDisconnected/RobotErrorPassThrough host reports now use the shared FIFO app buffer, dispatched after the completed tick outside locks; local removal, error processing, payload and wire order unchanged."
}
```

```json
{
  "id": "M1-025",
  "title": "Connect request from the game, the connected response, and DisconnectCurrent",
  "status": "EXACT_SOURCE",
  "evidence": [
    "B2 ConnectToRobot handler: robot 1 exists -> \"Robot already connected\", nothing (0x004ED022, 0x004ED026); else AddRobotConnection (0x004ED074), then AddRobot(1) at once (0x004ED07C)",
    "B23 HandleConnectionResponseMessage: state 1 -> 2 and reason 0 (0x0062F954, 0x0062F958, 0x0062F95E); otherwise \"Got connection response at unexpected time\"",
    "B33 DisconnectCurrent: RT->Disconnect then QueueConnectionDisconnect (0x0062EF38..0x0062EF58); callers RCM dtor 0x0062EE98, fatal robotError 0x0069DACA, MessageHandler::Disconnect 0x0069DCF2, ExitSdkMode lambda 0x0069E1EC..0x0069E1FA [corrected C7]",
    "B35 disconnect reasons: SleepPlacedOnCharger 4 (0x00606D1A), SetRobotDisconnectReason writes the byte (0x0069E01C)",
    "CB2/CB6: ConnectToRobot logs \"Connected to robot!\" before any transport exchange, then NeedsManager::InitAfterConnection and DASPauseUploadingToServer(1); nothing goes to the game (0x004ED074..0x004ED114)",
    "CC18/CC19: RobotDisconnectReason values 0..11 (0x00775BEC, table 0x01033970) and every writer of RCM+0x38; its only reader is the DAS disconnect event (0x0062FAAA), so it changes no behaviour",
    "CC27..CC30: a later ConnectToRobot builds everything afresh once robot 1 is gone; DisconnectCurrent queues type 3 + DeleteConnection and pushes one OnDisconnected marker, handled at the next RCM::Update in FIFO order (0x0062EF32..0x0062EF7A; 0x0062F3BC..0x0062F4BC)",
    "CB38: the app layer does not filter connection events by address; only data is compared against the robot address (0x0062F434; 0x0062F72C..0x0062F748)",
    "E7..E9: ExitSdkMode subscribers run UiMessageHandler, then MessageHandler (reason 6, DisconnectCurrent), then MovementComponent; an EnableAnimTracks it sends is posted behind the Disconnect action, finds no connection and is not sent (0x00660918..0x0063DE96; 0x0069E1DC..0x0069E1FE; CA12, CA14)"
  ],
  "unresolved": "M1 retains outer ExitSdkMode/disconnect order and gates, including external-exit charger unlock attempt after disconnect is queued (E17 regression). E1-E5 -> M8-018, M7-024, M15-031, M3-039, M4-029/-030 and M11-056; E8 -> M15-032; E9 -> M4-031. E6 remains M1-051; E7 remains M1-052. Exceptions follow approved M1-034. Sources: 20261006-M1M2-missing-triage.md E1-E9; 0x00660918..0x0063DE96; 0x0069E1DC..0x0069E1FE. OV13 check (re-analysis/research/overnight/20261014-ov13-m11-055-check.md): the unbounded vision join (0x0065257C) waits for any in-flight UpdateVisionSystem plus at most one 2 ms sleep; the worker logs Info \"VisionComponent.Processor\" \"Terminated Robot VisionComponent::Processor thread\" before the join returns (0x00651F08..0x006522A8).\nbuilt, awaiting strong verification: B-M11-1 (2026-10-14): Host constructed reset clears new dynamic world origin/root fixtures and diagnostic tail-call counter. Recursive native teardown remains M11-055/077; regression retains full as-constructed comparison. See jobs/status/B-M11-1.md for files/lines, binary ranges, tests and every MISSING."
}
```

```json
{
  "id": "M5-027",
  "title": "Idle animations: the idle stack, PushIdle/RemoveIdle, idle InitStream with tag 0xFF, ProceduralLive, the no-animation path",
  "status": "EXACT_SOURCE",
  "evidence": [
    "A1 0x0057A064..0x0057A0AC",
    "A28 0x0057D03A..0x0057D060, 0x0057D122..0x0057D1E2",
    "A29 0x0057D064..0x0057D448",
    "A30 0x0057B914..0x0057BD6E",
    "R-ANIM pre-extraction part 1 item 9 9a..9c: +0x64 writers 0x00579FC6, 0x0057B926, 0x0057BD66, 0x0057D022, 0x0057D3F6; drain 0x0057D168; HasResponse 0x00670AD0",
    "re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-027 (Opus settling pass, manager-adopted 2026-10-11).",
    "re-analysis/research/20261011-m5-fix-rows.md section 14 (Opus fix rows, manager-adopted 2026-10-11)."
  ],
  "unresolved": "Settled EXACT_SOURCE 2026-10-19 (re-analysis/research/20261019-m5-settling-b.md, manager-adopted): Loop exhaustion, no-animation path, ProceduralLive and +0x44 match. Superseded below: \"X3-X5 bypass idle\" (built). The per-connection AE/AF MISSING lines over-report (queued; owners M5-057/M5-061/M9-029). Previous notes: built, awaiting strong verification: B-M5-1: idle pick/error/flush/push/remove log sites, last-match search and >=1000 overflow continuation; empty selection repeats without actually popping; X3-X5 bypass idle. Previous notes: Settling pass 2026-10-11: DEFECT, fix per re-analysis/research/20261011-m5-settling-pass.md row M5-027. R-BEH2 batch 2 (2026-10-03): the StreamLive self-push and short-circuit are removed and the UpdateLiveAnimation S4 error path now returns without the tail; carrying is wired at CozmoAnimations from Motion.IsCarryingObject; GetParam/SetParam run the lazy default-set and SetParam clamps to GetParamRange; DesiredFaceDistortion (S1 step 2) has no source. | earlier: Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. when a clip's loops run out the engine clears +0x38 and branches past the idle-stack test to HaveLayersToSend/StreamLayers (0x0057D25C..0x0057D260), which can send a new StartOfAnimation; the stack runs the idle that tick, so later tags are off by one; the live-idle entry rests on the invented StreamLive seam (M7-017); the UpdateLiveAnimation error path continues where the engine returns. Earlier note: Settled (C5): +0x64 is set to 1 by an idle's InitStream tail; the Count-top flush refreshes the budgets before the drain; HasAnimationForTrigger is HasResponse. B-M5-8 (2026-10-12), built, awaiting strong verification: flush warnings use robot-message count; idle search visits only indices 0..999 and ports LoopBoundOverflow key/error/%s:%d/line 528 and error tail (0x0057D142/0x0057D18E, 0x0057BB3E..0x0057BB9A). B-M5-9 (2026-10-12), built, awaiting strong verification: the no-layers flush gate tests robot-message count +0x94; zero-size host callbacks do not trigger source flush. B-M5-12 (2026-10-12), built, awaiting strong verification: the old-idle-null/unmapped-top branch reports one visible stack diagnostic per skipped idle tail, naming the trigger and the original null dereference (0x0057D212..0x0057D218 -> 0x0057D3EE..0x0057D414, Animation::Init 0x00577BC6). Existing skip and continued streamer operation are retained under the operator-approved M1-034 departure; no engine log, policy invention or status change. The idle path's GetAnimationNameFromGroup lookup logs M5-065 on a miss.\nB-M5-16 built, awaiting strong verification: idle C3 lookup calls the native logging group lookup once; regression exercises mapped trigger -> nonexistent group through Scheduler.Advance. Existing null-idle/factory gaps remain visible."
}
```

```json
{
  "id": "M5-057",
  "title": "PushIdleAnimation(ProceduralLive) as the app-supplied input that starts live animation (app input)",
  "status": "COMPATIBILITY_POLICY",
  "evidence": [
    "re-analysis/research/20261012-m5-settling-2.md: UpdateLiveAnimation 0x0057D5F8..0x0057DA82 runs only when the idle stack top is ProceduralLive, which is pushed through the game-to-engine PushIdleAnimation (tag 0xAE) by the app (M5-030)."
  ],
  "unresolved": "COMPATIBILITY_POLICY (scope rule, operator 2026-10-09): the stack supplies PushIdleAnimation(ProceduralLive) from its own host API; the engine side (M5-030) stays exact."
}
```

```json
{
  "id": "M7-017",
  "title": "The exact live-animation wire lifecycle used by idle behavior",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "AnimationStreamer::UpdateLiveAnimation 0x0057d5f8",
    "Update 0x0057ce5c",
    "InitStream 0x0057b674",
    "UpdateStream 0x0057c84c",
    "SendStartOfAnimation 0x0057c400",
    "SendBufferedMessages 0x0057bf60",
    "DesiredFaceDistortionComponent ctor 0x0063b3be, Init 0x0063b3d4, Params 0x0063b434, GetCurrentDesiredDistortion 0x0063b760 (research/20261004-procedural-live-extraction.md D01-D18)",
    "NeedsManager owns it at +0x3D4 (0x0069210c, 0x00692212); TrackLayerComponent::Update 0x0064ede8 reads it at 0x0057cf7a and compares with 0x3727c5ac before AddGlitch (0x0064edf8..0x0064ee14)",
    "LiveUpdateFailed 0x0057d084..0x0057d0e6 (log 0xbef148/0xbef172, _errG 0x0105DD34)"
  ],
  "unresolved": "Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. B-FACE batches 1-3 (2026-10-04): built, awaiting strong verification. StreamLive no longer pushes ProceduralLive and the 'StreamLive' short-circuit is removed; the gated UpdateLiveAnimation port (M5-030) runs whenever ProceduralLive is on top; the S4 error path returns without the tail and now logs 'AnimationStreamer.Update.LiveUpdateFailed: Failed updating live animation from current robot state.' and sets the process-global _errG (0x0057d084..0x0057d0e6, 0x0105DD34); the idle clock +0x44 is zeroed while streaming and advances 60 per idle tick. The DesiredFaceDistortionComponent is built from D01-D18 (ctor 0x0063b3be, Init 0x0063b3d4, Params 0x0063b434, GetCurrentDesiredDistortion 0x0063b760), owned by NeedsManager at +0x3D4, initialised from needs_handlers_config.json's needsBasedFaceDistortion block with the robot context RNG, and wired through AnimationScheduler.DesiredFaceDistortion; the earlier 'DesiredFaceDistortion has no source' claim is contradicted by the extraction and is corrected here. Still open (out of scope, left as the visible gap): no fixed engine producer pushes 0x198 (PARTIAL in the extraction); it arrives through the PushIdleAnimation message (tag 0xAE, P09/P10) from the app, so it waits on the game-to-engine channel. The config parsers' asFloat of a present non-number key is UNKNOWN (the shipped config has numbers only), and what GraphEvaluator2d::EvaluateY returns for a 0-node graph is UNKNOWN (the C# evaluates a null graph as 0.0; the shipped config is non-empty)."
}
```

## Complete bounded managed/test path

| Step | Actual code path and observation | Consequence / provenance |
|---|---|---|
| F1 fixture | EngineAppLayerTests.cs791..814: Rig creates CozmoRobot.CreateForTest(FakePort,()=>NowNs,...), initialNowNs=1e9. CozmoRobot.cs575..583 calls Engine.StartOffline(autoTick:false). CozmoEngine.cs2052 IsProduction is `_runner is not null`; offline mode has no production runner. | Engine connection/state uses manually advanced engine clock. This is a test seam, not the production engine thread. |
| F2 first success | Rig.ToSuccess lines844..849 calls ToValidated/Connect (Engine.ConnectToRobot, Tick, port Connected, Tick), feeds RobotAvailable/Firmware, Tick, then ManufacturingID/Tick. Test2658 calls that once before UseEveryDevice2659. | First robot exists and handshake succeeded. This attempt never reaches second connect. M1-025 reset/rebuild expectations remain later, unexecuted. |
| F3 setup | UseEveryDevice2533..2561 supplies sync/calibration/AnimationState/object/world/image packets, engine ticks, drains actual NV requests, drives lights/display/audio/motion, adds cube listener. It then PushLiveQuietly2562 and asserts StreamLive(HeadKeyframe(0,100,5,0)) true2563. | These assertions reached and passed before failing at2584. Accepted append proves its `_streaming is not null` refusal did not fire; it does not prove `_idleAnim==_live` or that Advance ran. |
| F4 supplied live input | LiveIdleSeam.cs18..22 sets LockedTracks()=>7 and calls scheduler.PushIdleAnimation(ProceduralLive,"test-live"). AnimationScheduler.cs1138..1147 appends the idle entry; only Count directly clears idle fields. | Push does not set idle animation tolive and does not itself advance. This supplies the app input allowed by M5-057; it does not claim a native automatic ProceduralLive producer. |
| F5 append/start | CozmoAnimations.cs495..499 calls Scheduler.StreamLive(k,NowMs), then StartTicker if accepted and pending. AnimationScheduler.cs1244..1255 only locks, rejects if an explicit streaming animation exists, then appends to_live. HasPendingWork at685..690 includes TopTrigger!=Count. StartTicker638..644 uses a new background Thread when neither EngineDriven nor ManualTicking. | Pending live top means ticker is requested. StartTicker starts a thread; it does not synchronously complete its first update or signal to the caller. |
| F6 animation clock | CozmoAnimations.cs625..627 defines independent ClockMs optional override; absent override offline NowMs is Environment.TickCount64. TickLoop652..665 captures origin and Stopwatch; calls Advance(origin+elapsed) only if robot.AnimationStreamingOpen, then TickObserved at666. | This Rig never sets Animations.ClockMs/ManualTicking. Engine NowNs advancing60ms per tick does not advance the offline scheduler's separate stopwatch. |
| F7 engine ticks | Rig.Tick816 increments NowNs by60ms and calls Engine.Tick synchronously. Robot update CozmoEngine.cs1328..1336 opens streaming from TimeSynced&&ReadyToStream and invokes registered AnimationStreamerUpdate. CozmoRobot.cs381 binds that to Animations.EngineUpdate. CozmoAnimations.cs323..325 returns0 immediately when !EngineDriven. | Thus even five engine ticks while streamingOpen=True do **not** prove one animation update in this fixture. Readiness and idle-pointer equality are distinct facts. |
| F8 active writer | AnimationScheduler.cs1342..1364 NoAnimationPath reads TopTrigger; ProceduralLive sets_liveFlag=true and `_idleAnim=_live`, then calls generator. `LiveStreamActive`695 returns ReferenceEquals under scheduler gate. | One reached no-animation update with live top suffices to set active. Head-keyframe100ms duration is not an idle-state lifetime or polling guarantee. |
| F9 failing observer | EngineAppLayerTests.cs2581..2585 loops at most5: read LiveStreamActive, rig.Tick, repeat; finalassert reads property again. It subscribes neither TickObserved nor Faulted here. Saved message includes only current gate, not producer tickcount/top/idle mutation/error. | A fast loop can finish before the separate producer progresses; it can also fail for another unobserved producer problem. No update acknowledgment means test cannot distinguish them. |
| F10 unused observer | TickSignal.cs7..36 has an animation constructor subscribing TickObserved; Signal sets AutoResetEvent; Until checks predicate and waits for producer signals. This helper is used in the reconnect portion at EngineAppLayerTests.cs2691+, after the point the attempt failed. | Existing observer infrastructure is absent from the failing setup. Its later use cannot synchronize an earlier precondition. This read does not prescribe changing waits. |
| F11 native reset oracle | Test comments cite0x0052F248..0x0052F364 RemoveRobot/component destruction and0x004ED026..0x004ED07C fresh connect. Current M1 records cite those actual entries. | Those primary expectations justify reset/reconnect assertions after removal, not “LiveStreamActive within5 offline engine ticks” before removal. Referenced M1 IDs exist. No erroneous hardware-record reference is involved. |

## Narrow primary-source contract

Actual engine ARM/Thumb at0x0057D03A..4C selects the idle stack top only within the no-streaming Update branch. At0x0057D064 it loads previousidle+34;0x0057D068 compares top with0x198 ProceduralLive;0x0057D070..74 stores liveflag1;0x0057D078 forms this+A8;0x0057D07C writes it toidle+34;0x0057D080 calls UpdateLiveAnimation. This is the exact relevant assignment which LiveStreamActive models. The policy M5-057 supplies the push input and M5-027 owns Update/idle behavior. Their evidence does not promise a host thread scheduling deadline or that a different fixture clock's ticks advance this state.

M7-017 remains IMPLEMENTATION_GAP for its whole live-wire lifecycle; it must not be promoted from this setup or any eventual green rerun. The narrow active-pointer write is nevertheless established by source. Uncertainty in that broader record is not needed to explain the missing test synchronization, and no new source-recovery search is required merely to invent a waiting threshold.

## Classification and minimal next evidence

- **Test/harness defect confirmed:** the setup's five engine ticks do not synchronize the actual offline animation producer; the assertion embeds an unproved scheduling expectation. This is independent of whether the specific attempt's scheduler eventually advanced.
- **Specific occurrence root cause UNKNOWN:** the saved exception has no tick-observer history, scheduler-state transition or Faulted record. A delayed first update is a concrete permitted schedule, not an observed fact. A missed transient state, ticker crash or production lifecycle regression is also not established.
- **Implementation bug/source gap/hardware-only/external blocker:** none established by this failure. No hardware ran. No Dance listener or Dance lifecycle path is on this setup call chain.
- **Manifest impact:** none. Existing accepted M1-015/M1-025 remain correctly named for the later removal/reconnect oracle; their settled status is not challenged by a pre-removal fixture sample. Preserve all existing M7-017 uncertainty.

Minimal **diagnostic-only** proposal before any correction: retain the original wait/loop/assertion and add per-attempt fixture evidence around the existing live push/append and assertion. Capture TickObserved count (subscribe before push), each observed streaming gate plus LiveStreamActive, animation Faulted exceptions, IsTicking, accepted StreamLive result, engine NowNs, monotonic host elapsed time, relevant source logs and sent StartOfAnimation/EndOfAnimation/abort messages. Copy under appropriate existing locks; do not add sleeps/retries, fabricated ticks, successful action results or change assertion thresholds. The final failure should say whether zero producer updates were observed, whether any observed update had active=true, and whether a fault occurred. If stronger scheduler snapshot diagnostics are necessary, keep them read-only and bounded rather than altering behavior.

This preserves the failure and distinguishes starvation-before-first-update from an observed post-update state issue. Only after that evidence may a deterministic producer/clock test change or an actual production fix be justified. No additional gate was run, no rerun requested, and no commit created by reader.
