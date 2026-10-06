| Requested record | Coverage | Residuals considered |
|---|---|---|
| M1-015 | CHECKED | shared lifetime/service/factory/reset residuals |
| M1-024 | CHECKED | repair mutation/RNG, FP/NaN, outer exceptions |
| M1-025 | CHECKED | nested recipients, entry/state writers, connection callbacks, telemetry, lock diagnostics |
| M1-029 | CHECKED | formatting, allocation, extreme length, exception destination |
| M1-031 | CHECKED | shared lifetime/service/factory/reset residuals |
| M1-041 | CHECKED | implicit on-idle/ready residual (no literal MISSING) |
| M1-044 | CHECKED | shared lifetime/service/factory/reset residuals |
| M1-045 | CHECKED | shared lifetime/service/factory/reset residuals |
| M2-002 | CHECKED | implicit remaining uncertainty: delocalize, state callers, action lifecycle, polling/clock |
| M1-046 | CHECKED | subscription vector, messaging retirement, callback descendants |

# M1/M2 remaining-item ownership triage — 2026-10-06

Answers the operator’s 2026-10-06 request. Pull returned Already up to date. CHECKED denotes triage of every named residual, not recovery of every callee or approval to build. No upper-layer destructor internals were extended. No manifest, inventory, production code or fidelity status is changed; no commit is made in this research lane.

Categories: **HIGHER-LAYER** transfers recipient semantics to the named layer; **PHONE-RUNTIME** is only the identified unshipped runtime/state boundary; **M1-EXTRACT** retains shipped transport/app glue or shared support still requiring recovery/checking; **UNREACHABLE** is limited to the specifically bounded operation stated. Already extracted but unbuilt M1 slices are identified explicitly, rather than represented as unread code. Proposed NEW names are descriptive proposals, not allocated IDs.

Primary evidence: `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`; packaged `libc++_shared.so`, SHA-256 `8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a`. Fresh bounded instruction transcripts: [native boundaries](20261006-M1M2-triage-native.txt), [additional boundaries](20261006-M1M2-triage-extra-native.txt). Previously checked rows: [D/E/T extraction](20261005-B-M1M2-blockers-extraction.md), [reader/teardown check](20261005-B-M1M2-rows-check.md), [real formatter extraction](20261005-M1-029-real-extraction.md). Earlier reports are leads at authority 6; their addresses identify primary code for manager checking. Ranges below using those earlier reports are boundary citations, not a claim to have reopened every function in this task.

## Current records quoted before the proposals

The following quotes are the current title, status, evidence and unresolved, taken after the pull. Historical paragraphs in unresolved do not override its current leading disposition.

### M1-015

```json
{
  "id": "M1-015",
  "title": "Connection timeout 5 s, and how a lost or failed connection is reported",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1â€“T9; re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "R19 ReliableConnection::HasConnectionTimedOut: now > lastRecv (+0x50) + ConnectionTimeoutInMS (0x00836080..0x0083608C); on timeout ReceiveData(OnDisconnected, 0, addr), delete, Update false (0x00837BCC..0x00837C74); no frame sent",
    "B22 0x00837C50..0x00837C56 OnDisconnected to the receiver; tick lambda sets +0xA1 (0x008383D4..0x008383DC)",
    "B31 HandleDisconnectMessage: +0xA1 -> reason 1 WifiTimeout (0x0062FA34 ldrb.w r0,[r1,#0xa1]); RemoveRobot(id, wasConnecting) -> 2 ConnectionRejected when RCD state was 1 (the transport connect was never answered), else 1 ConnectionFailure, including a drop during the handshake [corrected C6] (0x0062FAEE..0x0062FAF8, 0x0052F23E..0x0052F244)",
    "B32 pending handshake: RobotConnectionResponse(result) and no RobotDisconnected; else RobotDisconnected and $session_id cleared; Robot deleted; the engine does not reconnect (0x0052DCC0..0x0052DD16, 0x0052F248..0x0052F302)",
    "CC23/CB32: HandleDisconnectMessage ignores the message fields, captures the RCD state, writes reason 1 if the timed-out flag is set, emits DAS, resets the reason to 0, clears RCD, then RemoveRobot(id, state == 1) (0x0062FA2E..0x0062FAF8); RIC::HandleDisconnect answers with RobotConnectionResponse {result, 0, 0, -1, -1} unless the response was already sent (0x0052DCC0..0x0052DD1A)",
    "CB33/CC26: RemoveRobot skips the RobotDisconnected broadcast and the $session_id clear when HandleDisconnect answered; either way it deletes the Robot and its RIC (0x0052F248..0x0052F364)",
    "T1â€“T5: strict timeout/marker/report selection and ordered lifetime handoff only. Component destruction effects belong to the new higher-layer lifetime records in inventory correction A2."
  ],
  "unresolved": "built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled."
}
```

### M1-024

```json
{
  "id": "M1-024",
  "title": "Engine tick 60 ms: arrivals drained FIFO and handed up once per tick",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "B24 CozmoInstanceRunner::Run: 60 ms period 0x3938700 ns (0x0065B3D2/0x0065B3D8), sleep to target, overtime/catchup logs; CozmoEngine::Update state 3 (tbb 0x004ED5D0): UpdateRobotConnection -> MessageHandler::ProcessMessages, then UpdateAllRobots (0x004ED62E, 0x004ED648)",
    "B25 ProcessMessages (0x0069D870) -> RobotConnectionManager::Update (0x0062F1FA) -> ProcessArrivedMessages drains the RCD queue FIFO (0x0062F3EA..0x0062F4BC, tbb 0x0062F434); then PopData until empty, no cap (0x0069D8C0)",
    "B20 RCD::ReceiveData drops the OnConnectRequest marker (0x0062E4BE); PushArrivedMessage maps OnConnected 2, OnDisconnected 3, else data, under the mutex (0x0062E274..0x0062E2C2)",
    "B26 HandleDataMessage: state != 2 drops \"Connection not yet valid\" (0x0062F728); source != robot address drops (0x0062F744)",
    "CD1..CD5: the tick is fixed rate on steady_clock: the first tick runs at once, the next target is the old target + 60 ms, it sleeps only if at least 1 us remains, overrun ticks run back to back, and when 240 ms or more behind it skips whole periods instead of running extra ticks (0x0065B3B8..0x0065B63E)",
    "CD6..CD11: the per-tick order: counters zeroed, UiMessageHandler::Update (game messages dispatched synchronously), then in state 3 BaseStationTimer::UpdateTime, UpdateRobotConnection (all robot-message handlers), NeedsManager::Update, UpdateAllRobots (Robot::Update, then the RobotState broadcast), then the audio controller; every engine timestamp in a tick is the tick start (0x004ED4DE..0x004ED6C4; 0x0084BC38..0x0084BCD4)"
  ],
  "unresolved": "built, awaiting strong verification: B-M1M2 checked D1â€“D18 (manager adoption in jobs/B-M1M2.md, 2026-10-05): live NeedsManager.ApplyDecayAllNeeds now snapshots f32 multipliers in need order, refreshes brackets before the pass, uses the checked piecewise NeedsState.ApplyDecay with inclusive/ordered threshold walk, elapsed/0x42700000 before rate*multiplier, current-floor crossing, separate f32 multiply/subtract, and min-only clamp; no caller elapsed>0 gate. Deadline zero/unordered conditions and last-decay store retained. D17 repair callback is exposed with visible MISSING logging when absent; its mutation/RNG body remains MISSING, as do global FP environment/NaN payload and outer exception disposition. No settlement. Previous verification: Opus verification of B-CORE2 (2026-10-02, re-analysis/research/20261002-B-CORE2-verify.md): NOT YET. The tick order (0x004ED626..0x004ED648), the f32 argument (0x0084BC72..0x0084BC8C) and the +0x3AC store (0x00695CA8) hold. The decay does not: NeedsState::ApplyDecay (0x0069C3C0, levels in map<NeedId,float>) computes minutes = elapsed/60 first (0x0069C48A), integrates piecewise across the rate thresholds (0x0069C4AC..0x0069C4EE) with level -= minutes*rate (0x0069C544) and a min-only clamp (0x0069C512), and is called with no elapsed>0 gate (0x00695DA4); Needs.cs:782-788 uses one rate at the current level, rate*elapsed/60f, double levels clamped at both ends, and skips elapsed <= 0. The fidelity: M1-024 tag on Needs.cs:762 claims the contradicted decay. Before: built, awaiting strong verification: B-CORE2 batch 5 (width and smaller items). The Needs tick is f32: CozmoEngine.NeedsUpdate is Action<float> and TickAt passes Timer.SecondsF (BaseStationTimer::GetCurrentTimeInSeconds 0x0084BCA8, the float at +0x10 stored after vcvt.f32.f64 0x0084BC80), and NeedsManager.Update(float) stores now at +0x3AC before the pause test (0x00695CA8) and compares and adds in f32 (0x00695CBE, 0x00695CD6). ApplyDecayAllNeeds(connected) reads +0x3AC (0x00695D4C, 0x00695D8A, 0x00695DA8) and the decay is computed in f32 (NeedsState::ApplyDecay 0x0069C48A, 0x0069C4A8..0x0069C4B0); the explicit-now overload is the test seam. NeedsManager.NowSec exposes +0x3AC. EngineAppLayerTests.M1_024_CD10... now expects Timer.SecondsF, and FreeplayTests.TheNeedsManagerUpdatesOnTheEngineTickNotTheFreeplayTick no longer returns early without the OBB. CozmoEngine.cs, Behavior/Needs.cs."
}
```

### M1-025

```json
{
  "id": "M1-025",
  "title": "Connect request from the game, the connected response, and DisconnectCurrent",
  "status": "IMPLEMENTATION_GAP",
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
  "unresolved": "built, awaiting strong verification: manager-checked E1â€“E14 in research/20261005-B-M1M2-blockers-extraction.md: live ExitSdkMode game-drain entry, UI edu/unpause and SDK status handoffs, independent communication byte/virtual interface, external-only RCM disconnect before charger-only Movement unlock. Track trees preserve duplicates and erase one owner; existing MA3 outgoing empty-tree mask retained including missing owner. MISSING: nested SDK message recipients, EnterSdkMode/state writers, concrete SDK connection callbacks, telemetry fields and PrintLockState. Teardown lower interfaces are built under correction A2; higher effects are explicit new lifetime gap records; this does not settle the complete path. Previous Opus verification: M1-044 dependency unbuilt."
}
```

### M1-029

```json
{
  "id": "M1-029",
  "title": "Firmware version check against the shipped firmware header",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "G5.1..G5.6 HandleFirmwareVersion 0x0052D470: guard (0x0052D478..0x0052D484); JSON parse (0x0052D4BA); FACTORY build path (0x0052D4C2..0x0052D506); v = version, t = time (0x0052D51A..0x0052D536); expected E_v/E_t at +0x1C/+0x20 (0x0052D538); sim flag (0x0052D53E..0x0052D5B6)",
    "G5.9/G5.10 no robotAvailable and not sim -> result 1 (0x0052D7B8..0x0052D850); sim -> 0 (0x0052D7C6)",
    "G5.11 robotDev = v == t, appDev = E_v == E_t; if they differ -> 3 OutdatedFirmware (0x0052D7DE teq.w r0,r1; 0x0052D7E4 movs r0,#3)",
    "G5.12 unsigned: E_v == v -> 0; E_v > v -> 3; E_v < v -> 4 OutdatedApp (0x0052D8DA..0x0052D8E0; 0x0052D9A6..0x0052D9AC); time is not compared again",
    "G5.14/G5.15 E_v/E_t are copied from RobotManager+0x84/+0x88 in AddRobot (0x0052EEF2/0x0052EEF8; ctor 0x0052D196); the RobotManager ctor zeroes them (0x0052E512, 0x0052E516)",
    "G5.16..G5.20 RobotManager::Init -> FirmwareUpdater::LoadHeader starts a loader thread (0x0052E7F8; pthread_create 0x0067692A) that reads config/engine/firmware/cozmo.safe and parses the JSON header in the first 0x800 bytes (0x00677C44..0x00677D34); ParseFirmwareHeader stores version -> +0x84, time -> +0x88 (0x0052EA36..0x0052EA9A)",
    "G5.21/G5.32..G5.37 Scope 1 is DataPlatformResourcesPath = persistentDataPath/cozmo/cozmo_resources (pathToResource 0x0084BE34 table 03 14 22 2f 41; unity/scripts/csharp/PlatformUtil.cs:5-13), extracted from the shipped assets (re-analysis/obb/assets/resources.txt:2075); the shipped header has version 2381, time 1546972025 (re-analysis/obb/assets/cozmo_resources/config/engine/firmware/cozmo.safe offsets 0-445)",
    "G5.22..G5.30 nothing orders the header load before AddRobot: the loader starts in cozmo_startup before the engine thread (0x006661EA, 0x0065B14E), and neither the ConnectToRobot handler nor AddRobot checks the load (0x004ED026..0x004ED1EC; loaded flag +0x18 unread on that path)",
    "G5.31 a missing, short or unparsable file leaves E_v = E_t = 0 for the session (0x006764E4, 0x00677C50..0x00677D28)",
    "G5.38..G5.40 no writer of RobotManager+0x84/+0x88 besides the ctor and ParseFirmwareHeader was found; the scan cannot prove absence (adjusted-base, register-offset, whole-object and untyped accesses are outside it); see decision D7"
  ],
  "unresolved": "built, awaiting strong verification: checked firmware byte Reader replaces System.Text.Json on FirmwareHeader.Parse and both live HandleFirmwareVersion paths. Manager-adopted research/20260930-bcore-extractions.md item 6 with research/20261005-B-M1M2-rows-check.md Item 6 corrections winning: whitespace 09/0A/0D/20 (0x008E166E..168C); saved root success ignores suffix errors (0x008E0886..0912); empty last decoded key close (0x008E0FA6..0FBA); comments/byte literals and container order; leading zeros/bare minus/-0; positive int cutoff INT32_MAX and sign-specific 64-bit limits (0x008E1CF2..1ECA); raw bytes, escapes and surrogate low10-bit combination (0x008E1B34..1C9E, 0x008E262A..2896); root counts toward >1000 runtime throw (0x008E09AA..0D92); duplicate replacement and partial mutation. Firmware build/version comparisons preserve byte strings. Built real-number conversion from manager-adopted research/20261005-M1-029-converter-build-rows.md S1-S21/W1: FirmwareJson.Reader.DecodeNumber calls FirmwareJsonDouble.TryConvert, preserving shipped binary64 initial scaling, integer correction and the unadjusted normalized ratio at libc++ 0x0007EE9E. EQUIVALENT_IMPLEMENTATION runtime assumptions (manager/operator 2026-10-05): fix round-to-nearest and C locale decimal point dot; no writer changing either was established on this loader path. These assumptions are limited to external runtime state; shipped numerical behavior remains exact. Regression expectations are shipped-converter emulator fixtures, not values generated by C#. MISSING: real asString formatting, allocation-failure and extreme-length behavior; retained explicit JsonMissingSource for formatting. MISSING: final escaping exception destination; existing Isolated/LoadAsync host containment remains an unbuilt candidate, not recovered engine behavior. Parse itself propagates typed depth/access errors, not parse-false. Record remains IMPLEMENTATION_GAP and incomplete; Conversion rows adopted and built; remaining MISSINGs still prevent completion. Prior Opus contradictions are repaired only for the checked subset; no claim of complete reader fidelity."
}
```

### M1-031

```json
{
  "id": "M1-031",
  "title": "Idle-timeout disconnect",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1â€“T9; re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "B34 StartIdleTimeout: deadline = now + disconnectTime_s if >= 0, keeping an earlier deadline (0x0052D030..0x0052D066); Cancel sets -1 (0x0052D06C); on expiry Update clears it and calls MessageHandler::Disconnect (0x0052CE6E..0x0052CE98); the reason is not set; driven from unity/scripts/csharp/PauseManager.cs:219, :289, :360",
    "CC1..CC7: the idle component has two deadlines: faceOff (armed only after the first full robot state, robot+0x34E) and disconnect; earliest wins; Cancel sets both to -1; expiry sets 0.0, which blocks re-arming until a Cancel; sleep fires before disconnect in one Update (0x0052CC64..0x0052D0C4; 0x0052CE3C..0x0052CE98)",
    "CC8: the sleep half queues a go-to-sleep animation sequence (0x0052CEA2..0x0052CFBE), an interface to the animation layer (M5); CC10: deadlines are checked once per 60 ms tick in engine state 3",
    "M1 boundary is deadline trigger/timing and factory handoff; action tree/execution belongs to new M5/M4/M8 sleep records in correction A2."
  ],
  "unresolved": "built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled."
}
```

### M1-041

```json
{
  "id": "M1-041",
  "title": "Robot initialisation after a Success connection response, and the gates it opens",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "CD17/CB21: the Success RobotConnectionResponse reaches, synchronously and in this order: RobotEventHandler, VisionComponent, TracePrinter (0x006625C6..0x006625FA; 0x00663C74..0x00663CA8; 0x005259AC, 0x006501E4, 0x0053BCBA)",
    "CD18/CB22/CB23: RobotEventHandler (result 0) calls Robot::SyncTime: +0x29 = 0, history clear, then SyncTime {u32 BaseStationTimer ms, 0xC1A00000} reliable; only if that was sent, InitController; only if that was sent, ImageRequest {Stream, QVGA} and AbsoluteLocalizationUpdate {0, frameId, originId, 0, 0, 0}; a failed send warns and stops (0x005289BA..0x005289D2; 0x0051521E..0x005153AE)",
    "CD19: SyncTime is never retried; after 5.0 s without SyncTimeAck it warns \"SyncTimeAckNotReceived\" (0x00513BF6..0x00513C5A); SyncTimeAck sets +0x29 = 1 (0x005366A4..0x005366AC)",
    "CD20: RobotEventHandler then sets ready-to-stream (+0x2A) through an NVStorage on-idle callback, which runs once the NV request queue is empty (0x00528A5A..0x00528A6E; 0x00645C20..0x00645C32)",
    "CD22: TracePrinter sends SetAppRunID (16-byte UUID, 0xFF unless a platform id exists), then RequestCrashReports{0}, each crash report received asking for the next index up to 3 (0x0053D398..0x0053D430; 0x0053CA88..0x0053CA9E)",
    "CD23/CD12: robot state is dropped until time sync (0x0051293C..0x0051294E); Robot::Update does nothing past the idle component until the first full state is handled (+0x34E); the AnimationStreamer runs only when synced and ready to stream (0x00513BF2..0x00514470)",
    "CD30: nothing sends SendHeadAngleUpdate, setAccessoryDiscovery or SetRobotImageSendMode on this path (BL scan)"
  ],
  "unresolved": "built, awaiting strong verification: B-M1M2 trial batch 1 (2026-10-05). Restored Robot.SendSyncTime.FailedToSend for SyncTime or InitController failure only (Opus checked 0x005152C4); short-circuit preserves the send order. Restored the four checked info logs in the live connection-response / NV-idle / SyncTimeAck entries. Self-review reopened the cited addresses: SendingSyncTime is AFTER Robot::SyncTime (0x005289D2 then 0x005289EC); QueueingSetReadyToStreamAnims is before callback registration (0x00528A34 then 0x00528A6E); the callback stores +0x2A BEFORE SettingReadyToStreamAnims (0x0052C3A6 then 0x0052C3B6); HandleSyncTimeAck logs before its two stores (0x0053667E then 0x005366A6/AC). No wire, clock or readiness changes. EngineAppLayerTests exercises the live mfgId and NV drain, both early failure branches, and the negative ImageRequest/AbsoluteLocalizationUpdate branches. No settlement. Prior verifier text: Opus verification of B-CORE2 (2026-10-02, re-analysis/research/20261002-B-CORE2-verify.md): NOT YET. The on-idle tick and the f32 deadline are fixed. Defects: Robot.SendSyncTime.FailedToSend (0x005152C4, name 0x00515458) is emitted when the SyncTime or InitController send fails, and B-CORE2 batch 4 deleted it (CozmoEngine.cs:989-990); the info logs RobotEventHandler.HandleRobotConnectionResponse.SendingSyncTime (0x005289EC), ...QueueingSetReadyToStreamAnims (0x00528A34), ...SettingReadyToStreamAnims in the on-idle lambda (0x0052C3B6) and Robot.HandleSyncTimeAck (0x0053667E) are missing. Before: built, awaiting strong verification: B-CORE2 batch 5 (width and smaller items). The SyncTime-ack deadline is f32: SyncTimeSentAt is the +0x520 float, set from Timer.SecondsF, and Robot.Update compares now > SyncTimeSentAt + 5.0f in single precision (0x00513C02..0x00513C14: vldr s0,[r6]; vadd.f32 s0,s0,#5.0; vcmpe.f32 s16,s0). CozmoEngine.cs; EngineAppLayerTests.M1_041_CD19_TheSyncTimeAckDeadlineIsComparedInF32. The on-idle/ready-to-stream residual stays as recorded for batch 1."
}
```

### M1-044

```json
{
  "id": "M1-044",
  "title": "RemoveRobot ordered lifetime interfaces, owner nulling and post-destruction membership cleanup",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1â€“T9; re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "RemoveRobot T2â€“T5/T9 0x0052F1BE..0x0052F364: lookup includes null values, external notification/report/session-clear selection, Needs/Perf/DAS before nullable destructor and storage release; map/first-id/RIC erase then $phys/$group clears. C# RobotManager.RemoveRobot and CozmoEngine.NotifyDisconnectServices/DestroyRobot.",
    "Robot lifetime T6â€“T8 0x005110D4..0x005115F8: destructor event, ForceUpdate, AbortAll; checked ordered nullable owner interfaces, null stores and repeated cleanup predicates; final base/member interface. C# EngineRobot.Lifetime/RobotLifetime.Destroy. Recursive effects are the higher-layer records in inventory correction A2, not ResetDevices."
  ],
  "unresolved": "built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled."
}
```

### M1-045

```json
{
  "id": "M1-045",
  "title": "Idle-timeout go-to-sleep factory handoff and queue submission",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1â€“T9; re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "RobotIdleTimeoutComponent::Update 0x0052CE54..0x0052CE6A: ldr.w r5,[robot,#0x250] (the ActionList), blx CreateGoToSleepAnimSequence 0x0052CE5E, then ActionList::QueueAction (0x0053D93C) with position 0 and u8 0 at 0x0052CE6A",
    "0x0052CE54..0x0052CE6A: factory result goes to existing Robot ActionList QueueAction position0 NOW, retries byte0; return discarded. C# CozmoEngine.QueueGoToSleep required higher factory interface. Factory and child execution are new M5/M4/M8 sleep records in correction A2."
  ],
  "unresolved": "built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled."
}
```

### M2-002

```json
{
  "id": "M2-002",
  "title": "RobotStatusFlag names and values, and where the engine stores each consumed bit",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "EnumToString(RobotStatusFlag) 0x007D57C8: 17 names and values, agreeing with Unity RobotStatusFlag.cs:8-25",
    "RS11 the whole status word -> robot+0x350 (0x00512AD8..0x00512ADC)",
    "bit storage: 0x1 MovementComponent+9 (0x0063E30A); 0x2 Delocalize argument (0x00512B98); 0x4 [robot+0x280]+4 (0x00512A96); 0x8 robot+0x349 (0x00512AA2); 0x10 robot+0x34C (0x00512ACE); 0x20 treads classifier (0x00511EC4); 0x100 MovementComponent+0xB = !bit (0x0063E32C); 0x200 +0xA = !bit (0x0063E320); 0x1000 SetOnCharger (0x00512AAC); 0x2000 robot+0x339 (0x00512AB8); 0x4000 CliffSensorComponent+6 (0x00634026); 0x8000 MovementComponent+0xC (0x0063E338); 0x10000 robot+0x33A (0x00512AC2)"
  ],
  "unresolved": "built, awaiting strong verification: B-M1M2 trial batch 1 (2026-10-05) regression and stale-text correction. The Opus-confirmed PlaceObjectOnGround defects were already corrected by cf36fb5 (R-FIX2 batch C): no 10-second dock timeout or BlockPlaced completion; unlatched/placing/moving gates remain RUNNING, and CheckIfDone returns the verify result. Added live RunAsync regression for BlockPlaced with a clear latch: still running at 0x41EFFFFF seconds and timeout 0x03000018 at 0x41F00000 (30.0f). Extended the existing raised-latch test with MovementComponent+9 set after bit 0x4 clears. The earlier unresolved description of the local dock-result bridge is obsolete for this code. Remaining uncertainty: the 0x2 Delocalize argument and GetRobotState callers remain open; the full queue-driven lifecycle and the existing RunAsync polling/clock bridge are not settled by these tests. No settlement. Historical verifier text (its old line numbers and local bridge describe the pre-cf36fb5 code): Codex review of B-CORE2 (re-analysis/research/20260930-B-CORE2-review.md), checked by the manager's Opus verifier where noted: DEFECT, confirmed by the Opus verifier with a correction. The engine latches +0x84 from DockingComponent+4 (0x005549B8..0x005549C2) and stays RUNNING while the latch is clear (0x005549D6) or MovementComponent+9 is set (0x005549E0), until the 30.0 s IAction timeout fails it with 0x03000018; DockActions.cs:915-921 completes an unlatched action. Also: the C# has its own 10 s dock timeout (DockActions.cs:902), and the engine's result is the face-and-verify sub-action's (0x005549E6..0x005549F4), not BlockPlaced. Before: built, awaiting strong verification: B-CORE2 batch 5 (width and smaller items). PlaceObjectOnGroundAction::CheckIfDone's status gate is now in RunAsync (DockActions.cs): while the robot's status bit 0x4 (IsPickingOrPlacing, stored at DockingComponent+4, 0x00512A96) is set the action latches its +0x84 (0x005549C0) and keeps running; with the bit clear a clear latch also keeps running (0x005549D6); with the latch set, MovementComponent+9 (status bit 0x1, 0x0063E30A) must clear (0x005549D8..0x005549E0) before the engine runs its face-and-verify sub-action. The stack has no per-tick action list, so RunAsync polls the gate; for a robot that never reports the bit it completes with the dock result (a LOCAL bridge, noted in the code). ManipulationTests.M2_002_PlaceObjectOnGroundWaitsForThePickingOrPlacingStatusGate. Also still open from before: the 0x2 Delocalize argument and who calls GetRobotState."
}
```

### M1-046

```json
{
  "id": "M1-046",
  "title": "Channel/member subscription retirement recipients",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "0x005112F2..0x00511308; Idle subscription vector and RobotToEngineImplMessaging recipient destruction; concrete unsubscribe/callback descendants UNKNOWN.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1â€“T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt"
  ],
  "unresolved": "MISSING: Idle subscription vector and RobotToEngineImplMessaging recipient destruction; concrete unsubscribe/callback descendants UNKNOWN. Bind the native owner to its actual higher-layer production component and recover/check the complete recipient path before implementation or settlement."
}
```

### M4-003

```json
{
  "id": "M4-003",
  "title": "The head and lift API follows the game-message path: caller speed/accel/duration; the original app passes head 10/20 and lift 10/20, duration 0",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "MA10 game SetHeadAngle overwrites +0x90..+0x98 with the message values (0x0052ABE0..0x0052AC24)",
    "MA11 unity/scripts/csharp/Robot.cs:1443-1450 (head 10, 20, 0) and 1638-1646 (lift 10, 20, 0)",
    "MA12 game SetLiftHeight: 32.0 while carrying -> PlaceObjectOnGroundAction (0x0052AC40..0x0052AC9E)",
    "MA9, MA13 action ctor defaults (head 15/20 at 0x00547F14..0x00547F28; lift 10/20 at 0x00548A68..0x00548A78)"
  ],
  "unresolved": "Codex review of B-CORE2 (re-analysis/research/20260930-B-CORE2-review.md), checked by the manager's Opus verifier where noted: DEFECT. Stop-before-unlock and the AreAllTracksLockedBy gate hold (Motion.cs:904-919); the game path's ActionQueue::QueueNow replacement (0x0053E24C..0x0053E35E) is absent, so a repeated game move fails 0x03000019 (Motion.cs:831-844). The queue semantics are in Codex's 20260930-bcore-extractions.md (unchecked). Before: built, awaiting strong verification: B-CORE2 batch 4 (head/lift/action timeout). ~IActionRunner's teardown now stops the track before the lock release: RunAsync/UpdateActions stop (gated on AreAllTracksLockedBy(mask, owner) 0x00541138/0x0054115E) then unlock (0x0054120C..0x0054122A), and the lock owner is a per-action tag (the engine's to_string(+0x60), counter 0x0053FE54..0x0053FE68 store 0x0053FEC6) instead of the old constant. Citation corrected: 0x005408EC is IActionRunner::UnlockTracks, called only from the IAction constructor (0x00540CB0) and IAction::Reset (0x00540D02); the action's end release is inline in ~IActionRunner. Still out of scope: the QueueNow part (a repeated game move never gets 0x03000019) awaits Codex extraction. Tests: M4ControlTests.M4_003_MA_ALockedTrackFailsTheHeadMoveWith03000019, M4ControlTests.M4_003_MA_TheHeadMoveLocksThenUnlocksItsTrack, M4ControlTests.M4_016_MA17_TheDefaultActionTimeoutIs30Seconds."
}
```

### M15-004

```json
{
  "id": "M15-004",
  "title": "Needs decay modifiers and damaged-part thresholds",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "NeedsState::GetDecayMultipliers 0x0069C214",
    "decay modifier descending sort 0x00691040",
    "NeedsState::NumDamagedPartsForRepairLevel 0x0069CCAC",
    "needs_decay_config.json",
    "needs_config.json"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): GetDecayMultipliers (0x0069C214: start 1.0f, f32 compare bge 0x0069C278, vmul.f32) and NumDamagedPartsForRepairLevel (0x0069CCAC) in binary32; the model is one entry per JSON entry (threshold + affected pairs; the outer scan stops at the FIRST entry with level >= threshold and applies its whole pair vector once). Still open: NeedsState levels stay double (narrowed to float at the two functions); a NaN level would match in the C# but never in the engine."
}
```

### M4-016

```json
{
  "id": "M4-016",
  "title": "Head and lift move semantics: no send when in position, ack matched by id, completion in position and stopped, tolerances and error codes",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "MA9, MA13, MA15..MA17, C1, C6, C10.1 as before",
    "C11.3 the lift CheckIfDone body 0x005493F6..0x00549508 contains no eye-shift removal; the head +0xA8 has only three writers, all zero (0x00547F3C, 0x005484B0, 0x00548724), so H4..H6 never execute"
  ],
  "unresolved": "Codex review of B-CORE2 (re-analysis/research/20260930-B-CORE2-review.md), checked by the manager's Opus verifier where noted: DEFECT, confirmed by the Opus verifier. +0x74 starts negative and is set from the tick clock at the first UpdateInternal (0x00540D52..0x00540D64); Motion.cs:828 stamps it at RunAsync. Codex's \"second precondition-time gate\" (0x00540DAC..0x00540DC0) cannot fire for these actions: slots 0x24/0x28 return 0.0 (0x0052B0BA/0x0052B0BE), slot 0x2C returns 30.0f (0x0052B0C2). Before: built, awaiting strong verification: B-CORE2 batch 4 (head/lift/action timeout). The timeout is now tested on the engine clock (CozmoEngine.Timer.Seconds) from the action's Init/first UpdateInternal (IAction::UpdateInternal 0x00540D4A..0x00540D64) before CheckIfDone, run by Robot::Update's ActionList step (CozmoMotion.UpdateActions, hook 0x00540370/CD12), and fails with EngineResult 0x03000018 (0x00540E80); the wall-clock Task.Delay is gone. Strict IsNear (0x0084CC0A) and the 30.0 s default hold. Tests: M4ControlTests.M4_016_MA15_NothingIsSentWhenAlreadyInPosition, M4ControlTests.M4_016_MA16_MA17_CompletionIsTheAckThenInPositionAndStopped, M4ControlTests.M4_016_MA17_StoppingOutOfPositionIsStoppedMakingProgress, M4ControlTests.M4_016_C1_TheHeadInPositionIsLatched, M4ControlTests.M4_016_R1_PassingTheTargetBeforeTheAckDoesNotLatch, M4ControlTests.M4_016_C6_TheLiftCompletion, M4ControlTests.M4_016_MA17_TheDefaultActionTimeoutIs30Seconds, ControlTests.AnUnacknowledgedActionTimesOutRatherThanReportingSuccess."
}
```

### M3-035

```json
{
  "id": "M3-035",
  "title": "An NV read gets no callback on disconnect or destruction, and its timeout needs a live RobotState clock",
  "status": "EXACT_SOURCE",
  "evidence": [
    "~NVStorageComponent frees +0x54 and destroys the +0x58 function without invoking it (0x643E80..0x643F8C); no callback for a queued or pending read on disconnect (pass 2 4e/4f)",
    "the timeout needs robot+0x2C to advance (0x64576A), which needs a RobotState with +0x29 and Robot::Update past Gate B (pass 2 4g)"
  ],
  "unresolved": "Opus verification of B-CORE2 (2026-10-02, re-analysis/research/20261002-B-CORE2-verify.md): SETTLED. ~NVStorageComponent destroys +0x58 through slots 0x10/0x14 (0x00643EB4..0x00643EBE) and never calls operator() (0x18); ~Robot reaches NV only through that destructor; the test drives transport removal -> ResetDevices (CozmoRobot.cs:670)."
}
```

### M11-053

```json
{
  "id": "M11-053",
  "title": "Robot::Delocalize callees and the frame-id-mismatch Delocalize trigger (Robot+0x2C0 counter)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00512D7A..0x00512F96 UpdateFullRobotState mismatch counter",
    "0x00510A24..0x00510D98 Robot::Delocalize",
    "re-analysis/research/20260929-R-VIS-verify-M11-batchA5.md"
  ],
  "unresolved": "NOT BUILT: the frame-id-mismatch counter and its Delocalize, the Delocalize callees other than BlockWorld (FaceWorld, AI, BehaviorManager, MovementComponent, Viz, SendAbsLocalizationUpdate), origin allocation (AddNewOrigin), so RobotDelocalized carries the old origin id. Faces.OnRobotDelocalized has no caller. The cliff schedule is skipped for the trigger state (round 5)."
}
```

### M15-028

```json
{
  "id": "M15-028",
  "title": "App telemetry/context/member lifetime recipients",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "0x0050B734..0x0050B748; 0x0052F2EE; 0x005110EE; 0x0051155C..0x005115A2; 0x005115F0; Perf stops if+A; DAS unpause, destructor event, context/string/robot-member retirement and base/member tail. Perf/DAS recipient fields and recursive effects UNKNOWN; M1 owns calls/order only.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt"
  ],
  "unresolved": "MISSING: Perf stops if+A; DAS unpause, destructor event, context/string/robot-member retirement and base/member tail. Perf/DAS recipient fields and recursive effects UNKNOWN; M1 owns calls/order only. Bind the native owner to its actual higher-layer production component and recover/check the complete recipient path before implementation or settlement."
}
```

## Shared residuals: M1-015, M1-031, M1-044, M1-045

Every S row applies to every occurrence of the shared unresolved paragraph in these four records. This is ownership, not four separate missing implementations. The lower record retains its ordered invocation contract. A shared paragraph must not cause a disconnect-only record to claim that every recipient is on its own live path.

| Row / missing piece | Classification | Layer / proposed ownership | Primary boundary and reason |
|---|---|---|---|
| S1 higher-layer lifetime bindings: NV | HIGHER-LAYER | M3-device, existing M3-038; retain M3-035’s specific callback-discard contract | Robot ownership/delete boundary `005112D6..005112E4`; recipient identity `0050FD8C..0050FD90`. No new duplicate record needed; this triage does not reread its destructor. |
| S2 control/sensor/light/movement/cube-accel bindings; retained ResetDevices candidate | HIGHER-LAYER | M4-control, existing M4-026/027; ResetDevices mapping amendment to M4-027 | AbortAll `0051194C..0051198C`; component boundaries `005111EA..0051121A`, `005113E0..005113F6`, `00511428..00511488`, `00511500..00511510`. A managed blanket ResetDevices is a candidate, not proof of equivalent native ownership. |
| S3 animation bindings | HIGHER-LAYER | M5-animation, existing M5-037 | Robot boundary `00511524..0051154A`; retained streamer recipient citations `0057AF48..0057AFDA`, `0057B044..0057B0C2`. Ordering belongs to M1; retirement effects to M5. |
| S4 mood bindings | HIGHER-LAYER | M7-reactions, existing M7-023 | Robot null action-list store `00511150`, mood invocation `005111B6`; recipient entry `0067AE14`. Preserve cross-component nulling order; no recursive extension here. |
| S5 action queue and behavior container bindings | HIGHER-LAYER | M8-framework, existing M8-015/016 | Queue cancellation boundary `0053E6A8..0053E6E0`; behavior calls `0051112C`, `0051113A`. Child cancellation/results and behavior retirement are not socket teardown. |
| S6 classifier/filter/gyro bindings | HIGHER-LAYER | M10-strategies, existing M10-014 | Robot member retirement `0051121A..0051124C`, `00511418..00511424`; construction identity `0050FE1C..0050FE20`. |
| S7 vision/history/world/pose/map bindings | HIGHER-LAYER | M11-vision, existing M11-055 | Robot boundaries `0051115E..005111AC`, `0051124C..0051126A`, `005112CE`, `00511310..005113DE`. Thread join remains a shipped component obligation; not wholesale PHONE-RUNTIME. |
| S8 carrying/docking bindings | HIGHER-LAYER | M12-manipulation, existing M12-040 | After AbortAll, Robot `005113FA..00511414`; recipient entry `0063BE10`. |
| S9 planner bindings | HIGHER-LAYER | M13-navigation, existing M13-029 | Robot `0051127E..00511288`; recipient entries `00649100`, `00649220`. |
| S10 pet/face/speech bindings | HIGHER-LAYER | M14-social, existing M14-013 | Robot `0051128C..005112C6`, `005114A6..005114B4`. |
| S11 needs/AI/progression/inventory bindings | HIGHER-LAYER | M15-freeplay, existing M15-027 | Needs OnDisconnect `00695908..0069594A`, Robot `005111BE..005111EA`, `00511314..00511338`; ForceUpdate handoff `0056EEB8`. |
| S12 telemetry/context lifetime service effects | HIGHER-LAYER | M15-freeplay, existing M15-028 | Perf conditional stop `0050B734..0050B748`, DAS unpause `0052F2EE`, destructor event `005110EE`, members `0051155C..005115A2`, tail `005115F0`. These app telemetry call/field decisions ship; they are not automatically phone policy. |
| S13 external notification delivery, concrete external-interface sink | M1-EXTRACT | M1: proposed NEW External-interface disconnect/report delivery | RemoveRobot `0052F29E..0052F2EE`: virtual slot `+0x30` at `0052F2AC`, then notification construction/delivery and session handling. Concrete sink/subscribers UNKNOWN here. Virtual dispatch is shipped code, not proof that the behavior is outside the package. Separate any subsequently identified UI recipient into its own layer. |
| S14 go-to-sleep factory implementation and compound children | HIGHER-LAYER | M5-038 (animation factory), M4-028 (lift), M8-017 (compound); existing IDs | Caller `0052CE5E`; factory `0052CEA2..0052CFC0`, lift boundary `0052CF8E..0052CFB0`. M1 retains QueueAction NOW, retry count zero and call order. Child selection/order/failure belong to these records. Lift tolerance bits `40A00000`; factory duration bits `42700000`. |
| S15 generic channel/member subscription retirement | M1-EXTRACT | Existing M1-046, see Q rows | The shared phrase recursive recipients does not relocate messaging ownership. Robot `005112F2..00511308` crosses actual channel/member retirement. |

## M1-024: needs callback, FP state and escaping exceptions

| Row / missing piece | Classification | Layer / proposed ownership | Primary boundary and reason |
|---|---|---|---|
| D1 D17 repair mutation and RNG | HIGHER-LAYER | M15-freeplay, proposed NEW Repair damage mutation and random-selection contract, alongside M15-004 | `0069C522` stores the f32 result; `0069C528` sets dirty byte `+0x88`; `0069C52E..0069C536` calls PossiblyDamageParts only for need id zero, with r1=1. Recipient entry/range `0069C5D8..0069C77A`; body intentionally not extended. M15-004 currently describes multipliers/count thresholds, not this mutation/RNG contract. |
| D2 global initial FP control state | PHONE-RUNTIME | Candidate external execution-environment assumption for needs f32 operations; not ADP-1 | Initial thread FPSCR rounding/DN/FZ is phone OS/hardware state, not a value supplied by ApplyDecay `0069C3C0..0069C560`. Its f32 opcodes and ordering ship and remain exact. Bounded caller has no FP-state write; a complete application-side writer search is not established by this task, so adoption is conditional on that check, not an assertion no writer exists. |
| D3 NaN payload/production of NaN operands | HIGHER-LAYER | M15-freeplay: proposed NEW Needs binary32 exceptional-value propagation; M15-001/004 input ownership | `0069C48A`, `0069C4A8..0069C4EE`, `0069C512..0069C544` are shipped comparisons, arithmetic, clamp and stores. Input bits and non-default NaN handling cannot all be externalized as FP environment. Payload/result under a fixed runtime mode still requires source/oracle checking; UNKNOWN. Only mode selection is D2. |
| D4 outer exception disposition | M1-EXTRACT | M1: proposed NEW Engine tick escaping-exception boundary | Tick caller `004ED4DE..004ED6C4`, thread runner `0065B3A8..0065B63E` and their unwind/LSDA/landing pads. Invocation order alone does not establish catch/terminate behavior. Recover shipped EH before separating eventual OS thread/process termination. |

## M1-025: SDK reset and state transitions

| Row / missing piece | Classification | Layer / proposed ownership | Primary boundary and reason |
|---|---|---|---|
| E1 nested SDK reset recipient orchestration | HIGHER-LAYER | M8-framework: proposed NEW SDK reset recipient dispatch contract, with child contracts E2–E5 | ResetRobot `0065DCFC..0065DF98` invokes external-interface slot `+0x0C` repeatedly. M1 keeps the outer ExitSdkMode order/gates. Receiver effects and behavior activation are separate; actual subscriber targets require verification, not inferred from message names. |
| E2 RemoveDisableReactionsLock("sdk"), ActivateHighLevelActivity(1) | HIGHER-LAYER | M7-reactions proposed NEW SDK reaction-lock recipient; M15-freeplay proposed NEW SDK activity activation recipient | Dispatch sites `0065DD9A`, `0065DDBA`. Those behavioral state changes are outside M1 transport. Concrete receiver bodies UNKNOWN. |
| E3 EnableCubeSleep(1,1), EnableLightStates(false,-1), EnableLiftPower(true) | HIGHER-LAYER | M3-device proposed NEW SDK cube-sleep recipient; M4-control proposed NEW SDK light/lift-power reset recipients | Dispatch `0065DEA2`, `0065DECA`, `0065DF8C`. Cube/light sends require SDK byte +0x79 nonzero; ordinary external ExitMode clears it first. Skipped on that branch is not globally UNREACHABLE: ResetRobot has other shipped callers/state. |
| E4 camera settings, color images, custom marker/object deletion | HIGHER-LAYER | M11-vision proposed NEW SDK camera/custom-world reset recipients | `0065DEDE..0065DEFC` settings (auto true, exposure zero, gain bits `00000000`); `0065DF14` color false; `0065DF32` undefine custom markers; `0065DF4E` delete custom objects. Preserve send order; message names alone do not settle handlers. |
| E5 StopRobotForSdk | HIGHER-LAYER | M4-control proposed NEW SDK stop-recipient action/control contract | Dispatch `0065DF6A`; actual receiver/action effects UNKNOWN here. It is a shipped message handoff, not permission to substitute host StopAll. |
| E6 EnterSdkMode and SDK connection/state writers | M1-EXTRACT | M1: proposed NEW SDK mode entry and lifecycle state writers | SDK EnterMode `0065E104..0065E224`, ExitMode `0065E2A4..0065E43E`, OnDisconnect `0065E4D8..0065E620`, OnConnectionSuccess `0065E6E0..0065E940`, UiMessageHandler OnEnter `0066150C..00661538`; UI `+0xE1` and communication `+0x88` are independent bytes. Complete writers/callers UNKNOWN, not phone state. |
| E7 concrete SDK connection callbacks | M1-EXTRACT | M1: proposed NEW SDK connection callback virtual targets | Communication change `00663298..006632C4` invokes slot `+0x24` with old/new values; helper `0065FE16..0065FE24` selects UDP/TCP indices with mask6. Actual vtable targets UNKNOWN. Transport routing/callback semantics ship. |
| E8 SDK telemetry fields | HIGHER-LAYER | M15-freeplay proposed NEW SDK transition telemetry field contract; M15-028 covers lifetime adjunct only | Entry/exit lifecycle boundaries `0065E104..0065E224`, `0065E2A4..0065E43E`, UI exit `00661546..00661566`. Specific field construction/address sites UNKNOWN; no bulk claim that M15-028 already owns all SDK telemetry. |
| E9 PrintLockState | HIGHER-LAYER | M4-control proposed NEW Track-lock diagnostic enumeration | Function `006410D8..006413CA`; lock/unlock caller `0063FF02..0063FF24`. Tree traversal, ownership names and diagnostic ordering are shipped control behavior. M4-003/016 cover motor action semantics, not this whole diagnostic recipient. |

## M1-029: split shipped wrappers from runtime services

| Row / missing piece | Classification | Layer / proposed ownership | Primary boundary and reason |
|---|---|---|---|
| J1 real asString dispatch and formatting wrapper | M1-EXTRACT | M1-029 retained; checked formatter rows F1–F8 exist, so remaining build/check work rather than a wholly unread function | `008E48B8..008E4902` real dispatch; formatter `008EBF7C..008EC038`. Precision17; capacity36; suffix .0 decision before comma-to-dot rewrite; nonfinite string choices are shipped. Live robot handler parses then calls asString at `0052D4DA` without checking field type. A JSON real in build reaches this path. Do not label the whole formatter UNREACHABLE from usual asset types. |
| J2 imported sprintf/snprintf/__isfinite body and formatting runtime state | PHONE-RUNTIME | Candidate M1-029 formatting-runtime assumption, explicitly separate from J1 | Imports at `008EBF98..008EBFBC`; bodies are undefined engine imports, supplied by phone libc rather than the packaged libc++ converter. Formatting algorithm/runtime locale behavior of these bodies does not ship in these artifacts. The app’s precision, cap, literal handling and comma rewrite do ship. Existing converter C-locale/nearest decision does not automatically approve every formatter assumption. |
| J3 real formatting on ParseFirmwareHeader integer-access subpath only | UNREACHABLE | No new record for this excluded sub-operation | `0052EA28..0052EAF4` obtains version/time via asUInt. That bounded file-header function does not call real asString. This local exclusion does not exclude J1 on the robot-supplied handler. |
| J4 allocation success/failure availability | PHONE-RUNTIME | Candidate memory-availability runtime boundary | Converter malloc/free call wrappers in packaged libc++ `0007E4BC..0007E516`, `0007E52C..0007E560` import phone allocation; available heap and whether allocation fails are runtime state. Code deciding what to do after failure is J5, not external. |
| J5 allocation-failure branch results, sentinel handling, Reader/string allocation exceptions | M1-EXTRACT | M1-029 support path; proposed NEW Firmware JSON allocation-failure contract if split needed | Packaged libc++ `0007E4BC..0007E516`, sentinel data `0009FD90`, failure use `0007EE66..0007EE80`, context `0007EC62..0007F202`; engine Reader `008E07A4..008E1ECA` and value/access/formatter allocation paths. Successful converter fixtures do not prove failure branches. Full mixed failure/throw path UNKNOWN; packaged libc++ throw behavior is shipped. |
| J6 enormous-input integer-counter wrap requiring more than 32-bit length | UNREACHABLE | Only such length-dependent branches on the two bounded firmware-input entry paths | File header max prefix0x800 (LoadFirmwareHeader `00677C3C..00677DE4`, prior checked loader rows); FirmwareVersion Unpack `007B908E..007B9092` calls `0071A8F6`, reads TWO count bytes at `0071A900..0071A904`, zero-extends via ldrh `0071A90C`, forwards count. Robot JSON length at most65535. This excludes >32-bit-length input, not long mantissas/exponents, deep nesting, allocation failure, or other general JSON users. |
| J7 extreme lengths within the reachable bound | M1-EXTRACT | M1-029 retained | Reader `008E07A4..008E1ECA`, packaged converter `0007E824..0007F202`; up to65535 robot JSON bytes still permits long numbers and depth-limit exceptions. Only specifically impossible counter-width cases are J6; no blanket extreme-length waiver. Converter successful long-input coverage already adopted; remaining Reader/string failure behavior stays UNKNOWN. |
| J8 final escaping exception destination | M1-EXTRACT | Proposed NEW Firmware JSON escaping-exception disposition, shared with D4 at tick boundary | Reader throw/access `008E09AA..008E0D92`; robot handler `0052D470..0052DA09`; header accessor `0052EA28..0052EAF4`; loader/proxy `00677C3C..00677DE4`, `0067861C..006786ED`, runner D4. Follow shipped unwinding/landing pads first. Eventual phone termination is not evidence for the entire path being external. |

Round-to-nearest and converter decimal point C/. are already adopted EQUIVALENT_IMPLEMENTATION assumptions in the current quoted record, not additional MISSING items. This report neither changes them nor treats packaged libc++ conversion as an external body.

## M1-041, M1-046 and M2-002

| Row / missing piece | Classification | Layer / proposed ownership | Primary boundary and reason |
|---|---|---|---|
| N1 M1-041 implicit on-idle callback execution / NV-idle predicate | HIGHER-LAYER | M3-device proposed NEW NV idle callback scheduling contract (or extend existing NV queue record after checking its exact claim) | Registration `00528A5A..00528A6E`; invocation boundary `00645C20..00645C32`; lambda `0052C3A6` stores ready before log `0052C3B6`. M3-035 governs discarded read callbacks, not all on-idle scheduling. No new lower M1 protocol required by this predicate alone. |
| N2 M1-041 ready-to-stream consumer lifecycle | HIGHER-LAYER | M5-animation proposed NEW Ready-to-stream animation lifecycle gate | Ready-byte store `0052C3A6`; SyncTimeAck `0053667E..005366AC`; current M1-041 already claims store/log and first-synced-state boundary. Full downstream streaming semantics belong to M5. Its old unresolved residual is not proof the now-built M1 store is absent. Exact downstream target span UNKNOWN in this bounded triage. |
| Q1 M1-046 Idle subscription vector retirement | M1-EXTRACT | Existing M1-046; amend its owner wording if necessary | Robot `005112F2..00511308` retires subscription/member storage. Generic scoped-handle destruction and unsubscribe targets UNKNOWN. Higher-layer recipient transfer is conditional on identifying a concrete subscriber, not on the word Idle. |
| Q2 M1-046 RobotToEngineImplMessaging retirement | M1-EXTRACT | Existing M1-046: concrete transport/channel owner | `00532A10..00532A78`: destroy map/tree members +0x144,+0x138,+0x110,+0x104; shared scoped-handle vector +0xF8 at `00532A48`; filebuf/ios retirement `00532A64..00532A6A`; tail at `00532A74`. This actual robot-message channel owner is not an upper behavior component. Scoped-handle deletion/unsubscribe descendants still UNKNOWN. |
| Q3 M1-046 behavior-changing subscriber descendants after retirement | M1-EXTRACT | M1-046 pending target identification; only identified upper targets get proposed per-layer recipient records | Caller ranges Q1/Q2 establish an unresolved dispatch boundary, not the final owner. Cannot name a fictitious higher-layer record for an unidentified callback. Concrete target/range UNKNOWN; retain M1 extraction to resolve, then split. |
| P1 M2-002 status0x2 Delocalize argument | HIGHER-LAYER | M11-vision, M11-053 amendment: explicit status/tread-boundary argument row | `00512B78..00512BA6`, call at `00512B98`; Delocalize `00510A24..00510D98`. M11-053 already explicitly describes this trigger bypass in its authority text, although its title highlights frame mismatch. Preserve actual boolean/argument computation and downstream ownership; no duplicate full Delocalize record. |
| P2 M2-002 GetRobotState callers / engine-to-game projection | M1-EXTRACT | M1 app tick/dispatch: proposed NEW Outgoing RobotState publication contract; M11 owns pose-derived inputs | `005180D8..00518252` getter; concrete caller `0052F6C0..0052F7B0` UpdateAllRobots updates each robot, gates HasReceivedRobotState, gets snapshot, constructs MessageEngineToGame then invokes external slot+0x1C. Tick entry `004ED648`. This is M1 glue still requiring caller/sink closure, not M2 raw status bit decoding. Other callers UNKNOWN; bounded caller found does not prove exhaustive callers. |
| P3 M2-002 complete queue-driven lifecycle | HIGHER-LAYER | M8-framework proposed NEW ActionList/ActionQueue and IAction lifecycle contract; M4-003/016 motor children, M8-015 cancellation/destruction adjunct | ActionList Update `0053F580..0053F5F2` calls ActionQueue Update `0053F5F4..0053F70C`; IActionRunner Update `00540370..0054063E`; IAction first-clock/timeout `00540D4A..00540E80`. Existing M8-012 is BehaviorManager tick/switching, NOT ActionQueue lifecycle; cannot silently assign full queue to it. Results/track locks/child order remain exact. |
| P4 M2-002 RunAsync polling and clock bridge | HIGHER-LAYER | Same proposed M8 queue/lifecycle record, linked to M4-016 and manipulation child ownership | Native action tick boundaries P3 and action CheckIfDone `005549B8..005549F4`, timeout bits `41F00000` (30.0f), result `03000018`. Host async scheduling is not proof that shipped action scheduling is PHONE-RUNTIME. Replace/validate bridge against native tick lifecycle; tests of local polling alone do not establish source fidelity. |

## Manager disposition

All explicit MISSING phrases and the implicit open residuals in M1-041/M2-002 are covered above. The shared paragraph is expanded once with all four origins named. No whole record is discarded or externalized. In particular, real formatting is reachable on the robot firmware JSON handler, channel retirement remains M1-owned until actual subscriber targets are identified, and SDK mode writers are shipped engine state.

Unknown callback targets, SDK telemetry field sites, complete application FP writers and final exception destinations are deliberately retained as gaps. The next extractor tasks are bounded by the cited M1 entry/caller ranges; this report does not request upper-layer destructor expansion. HIGHER-LAYER proposals identify boundary contracts only, with UNKNOWN recipient internals where not established. Manager checking is still required before inventory changes, build rows or settlement.
