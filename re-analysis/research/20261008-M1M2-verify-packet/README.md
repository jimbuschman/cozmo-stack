# M1 + M2 verification packet — 2026-10-08

Prepared for the operator’s request in this chat. Source snapshot `264c61f0bc52399d2cccf0bf0edc1c4f9b68112b` on main. Research-only packet; no new verdicts. Explicit operator authorization to commit/push overrides the ordinary research-lane no-commit rule.

## Coverage

63 current M1/M2 records; 53 included, 10 excluded. Built settled records, built policy/equivalence records, and built IMPLEMENTATION_GAP records with no explicit open MISSING are included. M1-041 and M2-002 retain their current non-MISSING verification uncertainty verbatim; inclusion does not mean complete fidelity. Hardware-only and unbuilt recoverable records are excluded.

| Record | Packet | Quoted lines | Native cited intervals |
|---|---|---:|---:|

| M1-001 | [M1-001.md](M1-001.md) | 5 | 9 |

| M1-002 | [M1-002.md](M1-002.md) | 36 | 55 |

| M1-003 | [M1-003.md](M1-003.md) | 6 | 19 |

| M1-004 | [M1-004.md](M1-004.md) | 3 | 5 |

| M1-005 | [M1-005.md](M1-005.md) | 30 | 40 |

| M1-006 | [M1-006.md](M1-006.md) | 48 | 66 |

| M1-007 | [M1-007.md](M1-007.md) | 8 | 30 |

| M1-008 | [M1-008.md](M1-008.md) | 39 | 27 |

| M1-009 | [M1-009.md](M1-009.md) | 18 | 28 |

| M1-010 | [M1-010.md](M1-010.md) | 10 | 34 |

| M1-011 | [M1-011.md](M1-011.md) | 2 | 1 |

| M1-012 | [M1-012.md](M1-012.md) | 6 | 26 |

| M1-013 | [M1-013.md](M1-013.md) | 1 | 0 |

| M1-014 | [M1-014.md](M1-014.md) | 25 | 15 |

| M1-016 | [M1-016.md](M1-016.md) | 3 | 7 |

| M1-017 | [M1-017.md](M1-017.md) | 3 | 2 |

| M1-018 | [M1-018.md](M1-018.md) | 8 | 19 |

| M1-019 | [M1-019.md](M1-019.md) | 14 | 61 |

| M1-020 | [M1-020.md](M1-020.md) | 4 | 10 |

| M1-021 | [M1-021.md](M1-021.md) | 13 | 60 |

| M1-022 | [M1-022.md](M1-022.md) | 44 | 82 |

| M1-023 | [M1-023.md](M1-023.md) | 16 | 56 |

| M1-026 | [M1-026.md](M1-026.md) | 8 | 25 |

| M1-027 | [M1-027.md](M1-027.md) | 14 | 23 |

| M1-028 | [M1-028.md](M1-028.md) | 158 | 192 |

| M1-030 | [M1-030.md](M1-030.md) | 13 | 25 |

| M1-032 | [M1-032.md](M1-032.md) | 11 | 42 |

| M1-034 | [M1-034.md](M1-034.md) | 25 | 97 |

| M1-035 | [M1-035.md](M1-035.md) | 6 | 21 |

| M1-036 | [M1-036.md](M1-036.md) | 3 | 3 |

| M1-037 | [M1-037.md](M1-037.md) | 3 | 8 |

| M1-038 | [M1-038.md](M1-038.md) | 25 | 28 |

| M1-039 | [M1-039.md](M1-039.md) | 26 | 31 |

| M1-040 | [M1-040.md](M1-040.md) | 24 | 12 |

| M1-041 | [M1-041.md](M1-041.md) | 14 | 47 |

| M1-042 | [M1-042.md](M1-042.md) | 27 | 13 |

| M2-001 | [M2-001.md](M2-001.md) | 73 | 97 |

| M2-002 | [M2-002.md](M2-002.md) | 99 | 94 |

| M2-003 | [M2-003.md](M2-003.md) | 75 | 55 |

| M2-004 | [M2-004.md](M2-004.md) | 105 | 236 |

| M2-005 | [M2-005.md](M2-005.md) | 112 | 240 |

| M2-006 | [M2-006.md](M2-006.md) | 96 | 76 |

| M2-007 | [M2-007.md](M2-007.md) | 105 | 246 |

| M2-008 | [M2-008.md](M2-008.md) | 19 | 39 |

| M2-009 | [M2-009.md](M2-009.md) | 66 | 136 |

| M2-010 | [M2-010.md](M2-010.md) | 34 | 39 |

| M2-011 | [M2-011.md](M2-011.md) | 55 | 60 |

| M2-012 | [M2-012.md](M2-012.md) | 98 | 70 |

| M2-013 | [M2-013.md](M2-013.md) | 87 | 73 |

| M2-014 | [M2-014.md](M2-014.md) | 26 | 24 |

| M2-015 | [M2-015.md](M2-015.md) | 68 | 117 |

| M2-016 | [M2-016.md](M2-016.md) | 78 | 40 |

| M2-017 | [M2-017.md](M2-017.md) | 55 | 60 |

## Excluded records and reasons

Each current excluded record is reproduced in full below so exclusions do not depend on an older report.

### M1-015

Open MISSING in current unresolved: built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled.

```json
{
  "id": "M1-015",
  "subsystem": "M1-transport",
  "title": "Connection timeout 5 s, and how a lost or failed connection is reported",
  "location": "cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
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
  "effect": "a dead link is noticed at a different time, or the game is told the wrong result",
  "provenance": "Manager-adopted checked T1–T9/ownership split (2026-10-05); narrowed M1 interface contract. Higher effects are explicit new records; no settlement.",
  "unresolved": "built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled.",
  "hardware_required": false,
  "live_path": true,
  "test": "TransportRepairTests.M1_015_R19_B22_ATimeoutDeletesTheConnectionSetsTheFlagAndTheTransportGoesOn, TransportRepairTests.M1_015_R19_InSyncModeTheUpdateReportsTheTimeoutButOnlyTheTickSetsTheFlag, EngineAppLayerTests.M1_015_CB33_AfterTheResponseTheGameGetsRobotDisconnected, EngineAppLayerTests.M1_015_CC23_CC24_ADropDuringTheHandshakeIsAFailureAndATimeoutIsReason1, EngineAppLayerTests.M1_015_CC24_ANeverAnsweredConnectIsRejected, EngineAppLayerTests.M1_025_M1_015_CB33_CC26_CC27_RemoveRobotLeavesEveryDeviceAsConstructed, EngineAppLayerTests.M1_025_M1_015_CC27_AfterRemovalASecondConnectStartsFromAFreshRobot, EngineAppLayerTests.M1_025_M1_015_CB33_AFrameInFlightAtTheRemovalLeavesNothingBehind, EngineAppLayerTests.M1_025_M1_015_CB33_APlayRunningAtTheRemovalEndsAndSendsNoMoreFrames, EngineAppLayerTests.CheckedRemoval_ReportsServicesLifetimeAndBookkeepingOrder, EngineAppLayerTests.CheckedRemoval_NullMapValueStillCleansMembership, EngineAppLayerTests.CheckedSleep_FactoryResultSubmittedNowWithZeroRetries"
}
```

### M1-024

Open MISSING in current unresolved: built, awaiting strong verification: B-M1M2 checked D1–D18 (manager adoption in jobs/B-M1M2.md, 2026-10-05): live NeedsManager.ApplyDecayAllNeeds now snapshots f32 multipliers in need order, refreshes brackets before the pass, uses the checked piecewise NeedsState.ApplyDecay with inclusive/ordered threshold walk, elapsed/0x42700000 before rate*multiplier, current-floor crossing, separate f32 multiply/subtract, and min-only clamp; no caller elapsed>0 gate. Deadline zero/unordered conditions and last-decay store retained. D17 repair callback is exposed with visible MISSING logging when absent; its mutation/RNG body remains MISSING, as do global FP environment/NaN payload and outer exception disposition. No settlement. Previous verification: Opus verification of B-CORE2 (2026-10-02, re-analysis/research/20261002-B-CORE2-verify.md): NOT YET. The tick order (0x004ED626..0x004ED648), the f32 argument (0x0084BC72..0x0084BC8C) and the +0x3AC store (0x00695CA8) hold. The decay does not: NeedsState::ApplyDecay (0x0069C3C0, levels in map<NeedId,float>) computes minutes = elapsed/60 first (0x0069C48A), integrates piecewise across the rate thresholds (0x0069C4AC..0x0069C4EE) with level -= minutes*rate (0x0069C544) and a min-only clamp (0x0069C512), and is called with no elapsed>0 gate (0x00695DA4); Needs.cs:782-788 uses one rate at the current level, rate*elapsed/60f, double levels clamped at both ends, and skips elapsed <= 0. The fidelity: M1-024 tag on Needs.cs:762 claims the contradicted decay. Before: built, awaiting strong verification: B-CORE2 batch 5 (width and smaller items). The Needs tick is f32: CozmoEngine.NeedsUpdate is Action<float> and TickAt passes Timer.SecondsF (BaseStationTimer::GetCurrentTimeInSeconds 0x0084BCA8, the float at +0x10 stored after vcvt.f32.f64 0x0084BC80), and NeedsManager.Update(float) stores now at +0x3AC before the pause test (0x00695CA8) and compares and adds in f32 (0x00695CBE, 0x00695CD6). ApplyDecayAllNeeds(connected) reads +0x3AC (0x00695D4C, 0x00695D8A, 0x00695DA8) and the decay is computed in f32 (NeedsState::ApplyDecay 0x0069C48A, 0x0069C4A8..0x0069C4B0); the explicit-now overload is the test seam. NeedsManager.NowSec exposes +0x3AC. EngineAppLayerTests.M1_024_CD10... now expects Timer.SecondsF, and FreeplayTests.TheNeedsManagerUpdatesOnTheEngineTickNotTheFreeplayTick no longer returns early without the OBB. CozmoEngine.cs, Behavior/Needs.cs.

```json
{
  "id": "M1-024",
  "subsystem": "M1-transport",
  "title": "Engine tick 60 ms: arrivals drained FIFO and handed up once per tick",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "B24 CozmoInstanceRunner::Run: 60 ms period 0x3938700 ns (0x0065B3D2/0x0065B3D8), sleep to target, overtime/catchup logs; CozmoEngine::Update state 3 (tbb 0x004ED5D0): UpdateRobotConnection -> MessageHandler::ProcessMessages, then UpdateAllRobots (0x004ED62E, 0x004ED648)",
    "B25 ProcessMessages (0x0069D870) -> RobotConnectionManager::Update (0x0062F1FA) -> ProcessArrivedMessages drains the RCD queue FIFO (0x0062F3EA..0x0062F4BC, tbb 0x0062F434); then PopData until empty, no cap (0x0069D8C0)",
    "B20 RCD::ReceiveData drops the OnConnectRequest marker (0x0062E4BE); PushArrivedMessage maps OnConnected 2, OnDisconnected 3, else data, under the mutex (0x0062E274..0x0062E2C2)",
    "B26 HandleDataMessage: state != 2 drops \"Connection not yet valid\" (0x0062F728); source != robot address drops (0x0062F744)",
    "CD1..CD5: the tick is fixed rate on steady_clock: the first tick runs at once, the next target is the old target + 60 ms, it sleeps only if at least 1 us remains, overrun ticks run back to back, and when 240 ms or more behind it skips whole periods instead of running extra ticks (0x0065B3B8..0x0065B63E)",
    "CD6..CD11: the per-tick order: counters zeroed, UiMessageHandler::Update (game messages dispatched synchronously), then in state 3 BaseStationTimer::UpdateTime, UpdateRobotConnection (all robot-message handlers), NeedsManager::Update, UpdateAllRobots (Robot::Update, then the RobotState broadcast), then the audio controller; every engine timestamp in a tick is the tick start (0x004ED4DE..0x004ED6C4; 0x0084BC38..0x0084BCD4)"
  ],
  "effect": "handlers see messages at other times or in other batches from the app",
  "provenance": "reproduced by the code; settled after batch 3's read-only verification (2026-09-24)",
  "unresolved": "built, awaiting strong verification: B-M1M2 checked D1–D18 (manager adoption in jobs/B-M1M2.md, 2026-10-05): live NeedsManager.ApplyDecayAllNeeds now snapshots f32 multipliers in need order, refreshes brackets before the pass, uses the checked piecewise NeedsState.ApplyDecay with inclusive/ordered threshold walk, elapsed/0x42700000 before rate*multiplier, current-floor crossing, separate f32 multiply/subtract, and min-only clamp; no caller elapsed>0 gate. Deadline zero/unordered conditions and last-decay store retained. D17 repair callback is exposed with visible MISSING logging when absent; its mutation/RNG body remains MISSING, as do global FP environment/NaN payload and outer exception disposition. No settlement. Previous verification: Opus verification of B-CORE2 (2026-10-02, re-analysis/research/20261002-B-CORE2-verify.md): NOT YET. The tick order (0x004ED626..0x004ED648), the f32 argument (0x0084BC72..0x0084BC8C) and the +0x3AC store (0x00695CA8) hold. The decay does not: NeedsState::ApplyDecay (0x0069C3C0, levels in map<NeedId,float>) computes minutes = elapsed/60 first (0x0069C48A), integrates piecewise across the rate thresholds (0x0069C4AC..0x0069C4EE) with level -= minutes*rate (0x0069C544) and a min-only clamp (0x0069C512), and is called with no elapsed>0 gate (0x00695DA4); Needs.cs:782-788 uses one rate at the current level, rate*elapsed/60f, double levels clamped at both ends, and skips elapsed <= 0. The fidelity: M1-024 tag on Needs.cs:762 claims the contradicted decay. Before: built, awaiting strong verification: B-CORE2 batch 5 (width and smaller items). The Needs tick is f32: CozmoEngine.NeedsUpdate is Action<float> and TickAt passes Timer.SecondsF (BaseStationTimer::GetCurrentTimeInSeconds 0x0084BCA8, the float at +0x10 stored after vcvt.f32.f64 0x0084BC80), and NeedsManager.Update(float) stores now at +0x3AC before the pause test (0x00695CA8) and compares and adds in f32 (0x00695CBE, 0x00695CD6). ApplyDecayAllNeeds(connected) reads +0x3AC (0x00695D4C, 0x00695D8A, 0x00695DA8) and the decay is computed in f32 (NeedsState::ApplyDecay 0x0069C48A, 0x0069C4A8..0x0069C4B0); the explicit-now overload is the test seam. NeedsManager.NowSec exposes +0x3AC. EngineAppLayerTests.M1_024_CD10... now expects Timer.SecondsF, and FreeplayTests.TheNeedsManagerUpdatesOnTheEngineTickNotTheFreeplayTick no longer returns early without the OBB. CozmoEngine.cs, Behavior/Needs.cs.",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.M1_024_B20_B25_B26_ArrivalsAreHandledInArrivalOrder, EngineAppLayerTests.M1_024_B25_CD10_ArrivalsReachHandlersOnlyInTheTickDrain, EngineAppLayerTests.M1_024_B26_DataFromAnotherAddressIsDropped, EngineAppLayerTests.M1_024_CD1_CD2_CD3_FirstTickAtOnceThenEveryPeriodAndANonzeroResultStops, EngineAppLayerTests.M1_024_CD3_ItSleepsWholeMicrosecondsAndOnlyFrom1us, EngineAppLayerTests.M1_024_CD3_OverrunTicksRunBackToBack, EngineAppLayerTests.M1_024_CD4_OvertimeIsOnlyLogged, EngineAppLayerTests.M1_024_CD5_AFarBehindLoopSkipsWholePeriodsInsteadOfRunningExtraTicks, EngineAppLayerTests.M1_024_CD7_CD8_GameMessagesRunBeforeTheClockUpdateAndRobotUpdateAfterIt, EngineAppLayerTests.M1_024_CD9_TheEngineClockIsTheTickStart, EngineAppLayerTests.M1_024_CD10_TheNeedsManagerRunsBetweenProcessMessagesAndRobotUpdate, FreeplayTests.TheNeedsManagerUpdatesOnTheEngineTickNotTheFreeplayTick; NeedsDecayCheckedRowsTests; EngineAppLayerTests.M1_024_D1_D18_CheckedDecayRunsThroughTheEngineTick"
}
```

### M1-025

Open MISSING in current unresolved: built, awaiting strong verification: manager-checked E1–E14 in research/20261005-B-M1M2-blockers-extraction.md: live ExitSdkMode game-drain entry, UI edu/unpause and SDK status handoffs, independent communication byte/virtual interface, external-only RCM disconnect before charger-only Movement unlock. Track trees preserve duplicates and erase one owner; existing MA3 outgoing empty-tree mask retained including missing owner. MISSING: nested SDK message recipients, EnterSdkMode/state writers, concrete SDK connection callbacks, telemetry fields and PrintLockState. Teardown lower interfaces are built under correction A2; higher effects are explicit new lifetime gap records; this does not settle the complete path. Previous Opus verification: M1-044 dependency unbuilt.

```json
{
  "id": "M1-025",
  "subsystem": "M1-transport",
  "title": "Connect request from the game, the connected response, and DisconnectCurrent",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
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
  "effect": "a second connect, a response at the wrong time, or a local disconnect is handled differently",
  "provenance": "reproduced by the code; settled after the device-reset change (CozmoRobot.ResetDevices on RemoveRobot, CB33/CC26/CC27) passed its targeted read-only re-verification (2026-09-24). Residuals outside M1 (named, not settled here): (a) the upper-layer Robot components built in Robot::Robot 0x0050FBF1 are not reset on RemoveRobot (DockingSystem Carrying/CarryingComponent, DockingComponent, PathFollower, BehaviorManager, MoodManager, IdleBehavior, ReactiveBehavior, CubeMovedReactionStrategy, MapComponent -> MemoryMap, AIComponent -> FreeplaySystem) - M12-M15; (b) CD21 connection-time NV CameraCalib read (0x006583BA) is not implemented; the reset drops a caller-read calibration - M3 interface; (c) VisionSystem.Enabled kept across a removal is UNKNOWN (the VisionComponent constructor zeroes +0x48..+0x4B, 0x006500EE); (d) the 2 s bound on waiting for the in-flight frame is local (the source joins unbounded, 0x0065257C).",
  "unresolved": "built, awaiting strong verification: manager-checked E1–E14 in research/20261005-B-M1M2-blockers-extraction.md: live ExitSdkMode game-drain entry, UI edu/unpause and SDK status handoffs, independent communication byte/virtual interface, external-only RCM disconnect before charger-only Movement unlock. Track trees preserve duplicates and erase one owner; existing MA3 outgoing empty-tree mask retained including missing owner. MISSING: nested SDK message recipients, EnterSdkMode/state writers, concrete SDK connection callbacks, telemetry fields and PrintLockState. Teardown lower interfaces are built under correction A2; higher effects are explicit new lifetime gap records; this does not settle the complete path. Previous Opus verification: M1-044 dependency unbuilt.",
  "hardware_required": false,
  "live_path": true,
  "test": "TransportRepairTests.B33_DisposeWhileConnectingSendsTheDisconnectRequestThenStops, EngineAppLayerTests.M1_025_B23_TheConnectionResponseMovesState1To2AndResetsTheReason, EngineAppLayerTests.M1_025_B2_CB36_ASecondConnectToRobotIsIgnoredWhileTheRobotExists, EngineAppLayerTests.M1_025_B2_CB4_ConnectToRobotBuildsTheRobotBeforeAnyExchange, EngineAppLayerTests.M1_025_CB38_AConnectionEventFromAnotherAddressIsHandledAsTheRobots, EngineAppLayerTests.M1_025_CB3_ASimulatedRobotUsesPort5552, EngineAppLayerTests.M1_025_CC18_DisconnectReasonValues, EngineAppLayerTests.M1_025_CC23_ClearResetsTheAddressSoALaterDisconnectCurrentSendsNothing, EngineAppLayerTests.M1_025_CC27_NoAutomaticReconnectAndALaterConnectToRobotBuildsAfresh, EngineAppLayerTests.M1_025_CC29_CC30_DisconnectCurrentDisconnectsOnceAndRemovesTheRobot, EngineAppLayerTests.M1_025_CC30_ArrivalsQueuedAfterTheMarkerAreDropped, EngineAppLayerTests.M1_025_M1_015_CB33_CC26_CC27_RemoveRobotLeavesEveryDeviceAsConstructed, EngineAppLayerTests.M1_025_M1_015_CC27_AfterRemovalASecondConnectStartsFromAFreshRobot, EngineAppLayerTests.M1_025_M1_015_CB33_AFrameInFlightAtTheRemovalLeavesNothingBehind, EngineAppLayerTests.M1_025_M1_015_CB33_APlayRunningAtTheRemovalEndsAndSendsNoMoreFrames, EngineAppLayerTests.CheckedSdkExit_IndependentEduAndExternalGates, EngineAppLayerTests.CheckedSdkExit_RemainingModeStillUpdatesCommunicationWithoutEqualitySuppression, EngineAppLayerTests.CheckedSdkExit_ResetHandoffsPrecedeBlockPoolAndConnectedFlagClear, EngineAppLayerTests.CheckedSdkExit_ChargerGateAndOneEntryErase"
}
```

### M1-029

Open MISSING in current unresolved: built, awaiting strong verification: checked firmware byte Reader replaces System.Text.Json on FirmwareHeader.Parse and both live HandleFirmwareVersion paths. Manager-adopted research/20260930-bcore-extractions.md item 6 with research/20261005-B-M1M2-rows-check.md Item 6 corrections winning: whitespace 09/0A/0D/20 (0x008E166E..168C); saved root success ignores suffix errors (0x008E0886..0912); empty last decoded key close (0x008E0FA6..0FBA); comments/byte literals and container order; leading zeros/bare minus/-0; positive int cutoff INT32_MAX and sign-specific 64-bit limits (0x008E1CF2..1ECA); raw bytes, escapes and surrogate low10-bit combination (0x008E1B34..1C9E, 0x008E262A..2896); root counts toward >1000 runtime throw (0x008E09AA..0D92); duplicate replacement and partial mutation. Firmware build/version comparisons preserve byte strings. Built real-number conversion from manager-adopted research/20261005-M1-029-converter-build-rows.md S1-S21/W1: FirmwareJson.Reader.DecodeNumber calls FirmwareJsonDouble.TryConvert, preserving shipped binary64 initial scaling, integer correction and the unadjusted normalized ratio at libc++ 0x0007EE9E. EQUIVALENT_IMPLEMENTATION runtime assumptions (manager/operator 2026-10-05): fix round-to-nearest and C locale decimal point dot; no writer changing either was established on this loader path. These assumptions are limited to external runtime state; shipped numerical behavior remains exact. Regression expectations are shipped-converter emulator fixtures, not values generated by C#. MISSING: real asString formatting, allocation-failure and extreme-length behavior; retained explicit JsonMissingSource for formatting. Exception handling: original firmware-JSON escaping exception destination remains UNKNOWN; retained Isolated/LoadAsync catch-and-log containment is the deliberate operator-approved departure under M1-034 (AGENTS.md Other approved departures, 2026-10-07), not recovered engine behavior. Parse itself propagates typed depth/access errors, not parse-false. Record remains IMPLEMENTATION_GAP and incomplete; Conversion rows adopted and built; remaining MISSINGs still prevent completion. Prior Opus contradictions are repaired only for the checked subset; no claim of complete reader fidelity.

```json
{
  "id": "M1-029",
  "subsystem": "M1-transport",
  "title": "Firmware version check against the shipped firmware header",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
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
  "effect": "a robot is accepted or refused as outdated where the app decides otherwise",
  "provenance": "the M1 candidate implementation (batch 3) compared against re-analysis/research/20260929-R-DEV-pre-extraction.md part 3 item 11 (rows 11a..11r) and re-analysis/inventory/M1-transport.md",
  "unresolved": "built, awaiting strong verification: checked firmware byte Reader replaces System.Text.Json on FirmwareHeader.Parse and both live HandleFirmwareVersion paths. Manager-adopted research/20260930-bcore-extractions.md item 6 with research/20261005-B-M1M2-rows-check.md Item 6 corrections winning: whitespace 09/0A/0D/20 (0x008E166E..168C); saved root success ignores suffix errors (0x008E0886..0912); empty last decoded key close (0x008E0FA6..0FBA); comments/byte literals and container order; leading zeros/bare minus/-0; positive int cutoff INT32_MAX and sign-specific 64-bit limits (0x008E1CF2..1ECA); raw bytes, escapes and surrogate low10-bit combination (0x008E1B34..1C9E, 0x008E262A..2896); root counts toward >1000 runtime throw (0x008E09AA..0D92); duplicate replacement and partial mutation. Firmware build/version comparisons preserve byte strings. Built real-number conversion from manager-adopted research/20261005-M1-029-converter-build-rows.md S1-S21/W1: FirmwareJson.Reader.DecodeNumber calls FirmwareJsonDouble.TryConvert, preserving shipped binary64 initial scaling, integer correction and the unadjusted normalized ratio at libc++ 0x0007EE9E. EQUIVALENT_IMPLEMENTATION runtime assumptions (manager/operator 2026-10-05): fix round-to-nearest and C locale decimal point dot; no writer changing either was established on this loader path. These assumptions are limited to external runtime state; shipped numerical behavior remains exact. Regression expectations are shipped-converter emulator fixtures, not values generated by C#. MISSING: real asString formatting, allocation-failure and extreme-length behavior; retained explicit JsonMissingSource for formatting. Exception handling: original firmware-JSON escaping exception destination remains UNKNOWN; retained Isolated/LoadAsync catch-and-log containment is the deliberate operator-approved departure under M1-034 (AGENTS.md Other approved departures, 2026-10-07), not recovered engine behavior. Parse itself propagates typed depth/access errors, not parse-false. Record remains IMPLEMENTATION_GAP and incomplete; Conversion rows adopted and built; remaining MISSINGs still prevent completion. Prior Opus contradictions are repaired only for the checked subset; no claim of complete reader fidelity.",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.M1_029_G5_10_ASimulatorIsAcceptedWithoutRobotAvailable, EngineAppLayerTests.M1_029_G5_11_G5_12_M1_040_TheVersionOutcomeIsComputedAndThenNotApplied, EngineAppLayerTests.M1_029_G5_14_TheExpectedValuesAreCopiedWhenTheRobotIsAdded, EngineAppLayerTests.M1_029_G5_19_G5_31_AShortOrMissingFileGivesNoHeader, EngineAppLayerTests.M1_029_G5_21_TheShippedHeaderParsesTo2381, EngineAppLayerTests.M1_029_G5_2_G5_4_CB13_FactoryAndUnparsableFirmwareProceedUnderM1_040, EngineAppLayerTests.M1_029_G5_31_WithNoHeaderTheExpectedValuesAre0AndTheRobotIsStillAccepted, EngineAppLayerTests.M1_029_11d_11e_11i_CommentsAnyRootAndTrailingText, EngineAppLayerTests.M1_029_11f_11g_TrailingCommasAreAnError, EngineAppLayerTests.M1_029_11n_AsUIntOfANonNumber, EngineAppLayerTests.M1_029_11q_ANonNumberVersionThrows, EngineAppLayerTests.M1_029_CheckedReaderBranches, EngineAppLayerTests.M1_029_CheckedIntegerTyping, EngineAppLayerTests.M1_029_CheckedUnicodeBytes, EngineAppLayerTests.M1_029_RawBytesAndDuplicateReplacement, EngineAppLayerTests.M1_029_RootCountsTowardsDepthLimit, EngineAppLayerTests.M1_029_CheckedReaderDrivesFirmwareHandshake, EngineAppLayerTests.M1_029_RealConversionUsesShippedBits, FirmwareJsonDoubleTests.ShippedEmulatorCorpusMatchesBitsAndFailureGates, FirmwareJsonDoubleTests.FirmwareReaderUsesShippedConversion, FirmwareJsonDoubleTests.FirmwareReaderRejectsConverterFailure, FirmwareJsonDoubleTests.FirmwareHeaderProductionEntryConvertsDecimalVersion"
}
```

### M1-031

Open MISSING in current unresolved: built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled.

```json
{
  "id": "M1-031",
  "subsystem": "M1-transport",
  "title": "Idle-timeout disconnect",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9; re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "B34 StartIdleTimeout: deadline = now + disconnectTime_s if >= 0, keeping an earlier deadline (0x0052D030..0x0052D066); Cancel sets -1 (0x0052D06C); on expiry Update clears it and calls MessageHandler::Disconnect (0x0052CE6E..0x0052CE98); the reason is not set; driven from unity/scripts/csharp/PauseManager.cs:219, :289, :360",
    "CC1..CC7: the idle component has two deadlines: faceOff (armed only after the first full robot state, robot+0x34E) and disconnect; earliest wins; Cancel sets both to -1; expiry sets 0.0, which blocks re-arming until a Cancel; sleep fires before disconnect in one Update (0x0052CC64..0x0052D0C4; 0x0052CE3C..0x0052CE98)",
    "CC8: the sleep half queues a go-to-sleep animation sequence (0x0052CEA2..0x0052CFBE), an interface to the animation layer (M5); CC10: deadlines are checked once per 60 ms tick in engine state 3",
    "M1 boundary is deadline trigger/timing and factory handoff; action tree/execution belongs to new M5/M4/M8 sleep records in correction A2."
  ],
  "effect": "the link is dropped after a pause at a different time, or never",
  "provenance": "Manager-adopted checked T1–T9/ownership split (2026-10-05); narrowed M1 interface contract. Higher effects are explicit new records; no settlement.",
  "unresolved": "built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled.",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.M1_031_CC3_CC6_TheDisconnectDeadline, EngineAppLayerTests.M1_031_CC6_SleepFiresBeforeDisconnectInOneUpdate, EngineAppLayerTests.M1_031_CC7_AnExpiredDeadlineBlocksReArmingUntilCancel, EngineAppLayerTests.CheckedRemoval_ReportsServicesLifetimeAndBookkeepingOrder, EngineAppLayerTests.CheckedRemoval_NullMapValueStillCleansMembership, EngineAppLayerTests.CheckedSleep_FactoryResultSubmittedNowWithZeroRetries"
}
```

### M1-033

HARDWARE_ONLY; no completed built record to package. whether the robot accepts packed frames of types 7, 8 and 9 from the engine (the link check sent none)

```json
{
  "id": "M1-033",
  "subsystem": "M1-transport",
  "title": "Robot-side transport behaviour",
  "location": "cozmo-stack/src/Cozmo.Transport/RobotLink.cs",
  "effect": "this stack assumes robot behaviour the app package cannot show",
  "provenance": "nothing in the package: the robot firmware is not part of it",
  "authority": "a robot, or a capture of the stock app talking to one",
  "evidence": [
    "part A open question 4: whether the robot sets isReply when it echoes a ping, its own resend timing, and whether it accepts packed frames of types 7, 8 and 9",
    "hardware run re-analysis/acceptance/hardware/20260924-112412-M1-LINK (firmware 2457): the robot sent only type-9 frames (1026); it echoed all 1025 pings with isReply clear and our timestamps; it repeated unacked reliable messages after 24..34 ms (median 33.6 ms). Not observed: whether it accepts packed type 7/8/9 frames from the engine"
  ],
  "status": "HARDWARE_ONLY",
  "unresolved": "whether the robot accepts packed frames of types 7, 8 and 9 from the engine (the link check sent none)",
  "hardware_required": true,
  "live_path": true,
  "test": null,
  "verification": {
    "level": "HARDWARE_VERIFIED",
    "bundles": [
      "re-analysis/acceptance/hardware/20260924-112412-M1-LINK"
    ]
  }
}
```

### M1-043

HARDWARE_ONLY; no completed built record to package. the errno value after a 0-byte recvmsg on the phone

```json
{
  "id": "M1-043",
  "subsystem": "M1-transport",
  "title": "Whether a 0-byte UDP read warns",
  "location": "cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs",
  "effect": "a warning is or is not logged for a 0-byte datagram",
  "provenance": "nothing in the package: it depends on the thread's errno at run time",
  "authority": "a robot run with a 0-byte datagram, or an Android errno trace",
  "evidence": [
    "CA35: a 0-byte read takes the error path and reads the stale errno; it warns iff errno != EAGAIN and reopens on ENOTCONN (0x0083AA98 → 0x0083AAC4..0x0083AB24)"
  ],
  "status": "HARDWARE_ONLY",
  "unresolved": "the errno value after a 0-byte recvmsg on the phone",
  "hardware_required": true,
  "live_path": true,
  "test": null
}
```

### M1-044

Open MISSING in current unresolved: built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled.

```json
{
  "id": "M1-044",
  "subsystem": "M1-transport",
  "title": "RemoveRobot ordered lifetime interfaces, owner nulling and post-destruction membership cleanup",
  "location": "cozmo-stack/src/Cozmo.Robot/RobotLifetime.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9; re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "RemoveRobot T2–T5/T9 0x0052F1BE..0x0052F364: lookup includes null values, external notification/report/session-clear selection, Needs/Perf/DAS before nullable destructor and storage release; map/first-id/RIC erase then $phys/$group clears. C# RobotManager.RemoveRobot and CozmoEngine.NotifyDisconnectServices/DestroyRobot.",
    "Robot lifetime T6–T8 0x005110D4..0x005115F8: destructor event, ForceUpdate, AbortAll; checked ordered nullable owner interfaces, null stores and repeated cleanup predicates; final base/member interface. C# EngineRobot.Lifetime/RobotLifetime.Destroy. Recursive effects are the higher-layer records in inventory correction A2, not ResetDevices."
  ],
  "effect": "behaviour, mood, path, docking, map or freeplay state survives a removal the engine would have destroyed",
  "provenance": "Manager-adopted checked T1–T9/ownership split (2026-10-05); narrowed M1 interface contract. Higher effects are explicit new records; no settlement.",
  "unresolved": "built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled.",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.CheckedRemoval_ReportsServicesLifetimeAndBookkeepingOrder, EngineAppLayerTests.CheckedRemoval_NullMapValueStillCleansMembership, EngineAppLayerTests.CheckedSleep_FactoryResultSubmittedNowWithZeroRetries"
}
```

### M1-045

Open MISSING in current unresolved: built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled.

```json
{
  "id": "M1-045",
  "subsystem": "M1-transport",
  "title": "Idle-timeout go-to-sleep factory handoff and queue submission",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9; re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "RobotIdleTimeoutComponent::Update 0x0052CE54..0x0052CE6A: ldr.w r5,[robot,#0x250] (the ActionList), blx CreateGoToSleepAnimSequence 0x0052CE5E, then ActionList::QueueAction (0x0053D93C) with position 0 and u8 0 at 0x0052CE6A",
    "0x0052CE54..0x0052CE6A: factory result goes to existing Robot ActionList QueueAction position0 NOW, retries byte0; return discarded. C# CozmoEngine.QueueGoToSleep required higher factory interface. Factory and child execution are new M5/M4/M8 sleep records in correction A2."
  ],
  "effect": "a faceOff expiry does not put the robot to sleep",
  "provenance": "Manager-adopted checked T1–T9/ownership split (2026-10-05); narrowed M1 interface contract. Higher effects are explicit new records; no settlement.",
  "unresolved": "built, awaiting strong verification: checked lower M1 interfaces and gates, with recipient effects explicitly split by inventory correction A2 (2026-10-05). MISSING: higher-layer lifetime bindings/recursive recipient effects, external notification/telemetry services, go-to-sleep factory implementation. Retained host ResetDevices remains a higher-layer candidate, not native destructor proof. No record settled.",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.M1_031_CC3_CC6_TheDisconnectDeadline, EngineAppLayerTests.M1_031_CC6_SleepFiresBeforeDisconnectInOneUpdate, EngineAppLayerTests.M1_031_CC7_AnExpiredDeadlineBlocksReArmingUntilCancel, EngineAppLayerTests.CheckedRemoval_ReportsServicesLifetimeAndBookkeepingOrder, EngineAppLayerTests.CheckedRemoval_NullMapValueStillCleansMembership, EngineAppLayerTests.CheckedSleep_FactoryResultSubmittedNowWithZeroRetries"
}
```

### M1-046

Open MISSING in current unresolved: MISSING: Idle subscription vector and RobotToEngineImplMessaging recipient destruction; concrete unsubscribe/callback descendants UNKNOWN. Bind the native owner to its actual higher-layer production component and recover/check the complete recipient path before implementation or settlement.

```json
{
  "id": "M1-046",
  "subsystem": "M1-transport",
  "title": "Channel/member subscription retirement recipients",
  "location": "cozmo-stack/src/Cozmo.Robot/RobotLifetime.cs",
  "status": "RECOVERABLE_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "0x005112F2..0x00511308; Idle subscription vector and RobotToEngineImplMessaging recipient destruction; concrete unsubscribe/callback descendants UNKNOWN.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt"
  ],
  "effect": "A removed Robot or queued sleep action retains state, emits different messages or invokes callbacks in a different order.",
  "provenance": "Cited cross-layer inventory correction authorized by B-M1M2 Rows checked (manager, 2026-10-05). Interfaces/known effects only; no higher-layer build or settlement.",
  "unresolved": "MISSING: Idle subscription vector and RobotToEngineImplMessaging recipient destruction; concrete unsubscribe/callback descendants UNKNOWN. Bind the native owner to its actual higher-layer production component and recover/check the complete recipient path before implementation or settlement.",
  "hardware_required": false,
  "live_path": true,
  "test": ""
}
```

## Reproduction and artifact boundaries

`python re-analysis/research/20261008-M1M2-verify-packet/build_packet.py` packages the checked-out snapshot after the offline test TRX exists. It only writes in this directory. The native ELF is read locally and is not uploaded. Per-record files are self-contained for manifest, rows, diff text, native transcripts, and relevant test/result text. Complete shared-file histories intentionally contain unrelated hunks; they are explicitly labeled as supersets rather than assigned invented record-level causal ownership.

Test command: `DOTNET_PROCESSOR_COUNT=4 dotnet test cozmo-stack/tests/Cozmo.Protocol.Tests/Cozmo.Protocol.Tests.csproj --filter` selecting FrameTests, ConnectionTests, TransportRepairTests, TransportSourceTests, TransportHardeningTests, EngineAppLayerTests, M2ProtocolTests, SourceFidelityTests, LightPackingTests, NativeNumericOracleTests, DerivedStateTests, ManipulationTests, R2FloorPlacementTests, M4ControlTests, HardwareCaptureTests; logger TRX. Results are independently recorded in the supplied TRX. Existing oracle blocked imports remain blocked, not fabricated results.

## Offline run and packet validation

Offline run: 525 passed, 1 failed, 0 not executed. The wider class selection includes one asset-dependent test outside the record-declared method list; its actual failure is retained below. No asset opt-out or result substitution was used.

`Cozmo.Protocol.Tests.R2FloorPlacementTests.TheShippedBindingPlacesTheCubeThroughTheBehaviorManager`: Failed

re-analysis/obb (the shipped config and animation assets) is missing, so the live-entry test cannot run; provide it or set COZMO_TESTS_WITHOUT_ASSETS=1

`validate_packet.py` checks the included/excluded partition, verbatim manifest values and inventory lines, exact Git patch histories, native transcript bytes against the local ELF, citation byte coverage, and declared-test result matching. Machine-readable results are in `validation.json`. These are packaging checks, not an Opus/source-fidelity verdict.
