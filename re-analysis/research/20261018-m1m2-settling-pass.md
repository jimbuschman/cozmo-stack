# M1+M2 settling pass (Opus, read-only), 2026-10-18, HEAD 29eb1064

- **Checked:** M2-018, M2-019, M2-020, M2-022, M2-023, M1-054, M1-055, M1-056 against current code and disassembly
  (every address re-read unless marked "prior check"). `fidelity.py --check` passes (697); focused tests 97/97.
- **Result:** none can be settled today. M2-019 and M2-020 are close; M2-022/M2-023 are cheap. One shared mechanical
  blocker: `fidelity.py` (195-198) needs a `// fidelity: <id>` tag in a settled record's `location` file. M2-019,
  M2-022, M2-023 point at `RobotSubscriptions.cs` (no tag); M1-056 at `CozmoEngine.cs` (its tags are in
  `EngineReadiness.cs`, `AppDelivery.cs`). Add tags or correct `location` (not frozen).

| Record | Verdict | Reason |
| --- | --- | --- |
| M2-018 | NOT YET | Dispatch and checked per-tag order hold; 9 of 37 native Impl Init subscriptions have no stack subscriber, record or MISSING; C3 RobotPoked (API report) and BA/BB SetOnRamp (robot state) are observable |
| M2-019 | SETTLE after tag fix | D2/D3 match 0x00533348..0x005335C4 and 0x00536F1C.. |
| M2-020 | SETTLE after queued cleanup | Wildcard, emitter, Reset/unlink, re-Init and ~Messaging order hold; B0 HandleTrace read: log-only |
| M2-022 | NOT YET (cheap) | Stack matches (no local 0x60 subscriber; Clear before PopData); no tag anywhere, no test |
| M2-023 | NOT YET (cheap) | No-robot drop matches 0x0069D8BC..0x0069D8D8; mid-emit lead unclosed in text; NoRobotDrops test mislabelled; tag |
| M1-054 | NOT YET | NeedsManager::Init (0x004ECA0A) never called at Init (live MISSING `CozmoExperiments.cs`:146); FactoryTestLogger archive has no recorded decision; LoadRobotConfigs census unread; engine-config JSON app input has no policy record. Init tail after 0x004ECA1A now read |
| M1-055 | NOT YET (narrow) | Broadcast, tags, order, state-3 tail match; open: UNKNOWN tag-only local E2G consumer of 0x5D/0x9A; S3 in decision form |
| M1-056 | NOT YET (narrow + tag) | State 1/2, SetEngineState, EngineLoadingDataStatus match; open: local subscriber of 0x4ED7C0's E2G; tag |

## M2-018

- Supported: `RobotSubscriptions.cs`:42-92 tail append / next-after-call / set-callback / removed nodes keep links, as
  0x0051D874..0x0051D8A2 and 0x0069E274..0x0069E2B2 (prior), emitter 0x0069DE18..0x0069DE78. Live: `CozmoEngine.cs`:503-536
  ProcessMessages → Broadcast (:2252-2263) → DeliverMessage. Robot by ID 1 vs GetFirstRobot (0x0069D8D2): same result.
- **OMITTED (blocking).** InitRobotMessageComponent (0x00532A7C) also subscribes, with no stack subscriber
  (`RobotSubscriptions.cs`:133-186) and no record:

  | Tag | Native handler | Effect |
  | --- | --- | --- |
  | B1 | HandlePrint (GOT 0x0103EA60) | log |
  | B3 | HandleGoalPose 0x005352C8 | VizManager DrawPreDockPose only |
  | C3 | HandleRobotPoked 0x005366FC | Info, then E2G RobotPoked via Robot::Broadcast 0x00536754 (API delivery) |
  | BA | lambda vtable 0x01020F90 → 0x00538088 | Info, then Robot::SetOnRamp(true) 0x005380D2 |
  | BB | lambda → 0x0053819C | Info, then SetOnRamp 0x005381E6 |
  | BC / BD | 0x005382B4, 0x005383C0 | Info only |
  | B2 | 0x005384CC | two sWarningF |
  | B7 | 0x0053866A | hex Info |

- **CONTRADICTED:** `CozmoEngine.cs`:1154-1155 "+0x316 SetOnRamp has no writer here" (M1-041/M4-020 code); native
  writers 0x005380D2, 0x005381E6.
- Queued: `M3M4X18BuilderTests.cs`:25 `CheckedPerTagRegistrationOrder` has no reach label (unit: direct
  Engine.Broadcast); omits the table's ED, C4, C0 rows; not circular.

## M2-019

D2/D3 `CheckedMessageHandlers.cs`:21-50 vs 0x00533348..0x005335C4: 44-byte copy 0x0053335E..0x00533376; memcmp
Impl+0x18 vs engine hash, ConvertMessageBufferToString(…,1,true,0x100); warning robot-first (0x00533454); sticky +0x38
(0x0053347C); R→E with +0x39 (0x00533580); build when either flag set (0x00533584..0x005335AE; engineE, engineR, robotE,
robotR); Robot::Broadcast 0x005335BE. Local E2G emit for 0x2A not done; M2-022's census shows no local subscriber
(inferred; census not available as a file). Unit test labelled correctly; hashes from 0x00C51870/0x00C51880.
Blocker: tag/location.

## M2-020

Wildcard `movw/movt #0x101CF` 0x0069D004/0x0069D008, stored MH+0x1C (0x0069D00E); emitter all-signals only if robot ID
== wildcard (0x0069DE32..0x0069DE4E), else robot signal then wildcard (0x0069DE50..0x0069DE74); only Broadcast caller
passes robot 1 (0x0069DABA); `CozmoEngine.cs`:2258-2261 accurate. RetainedSubscription.Replace
(`RobotSubscriptions.cs`:17-18); retirement :179 (prior). **TracePrinter::HandleTrace 0x0053C210..0x0053C488** calls
only Get_trace, to_string, GetName, GetFormatted, one log per level (tbb 0x0053C2DC); sErrorF 0x0053C30A goes straight
to cleanup: log-only, backing "decode and log". EC logs one `%x` where native is "%x%x" (best effort).
Queued: `RobotSubscriptions.cs`:123 ReportMissing prefix "M2-020:" now belongs to M7-027/M12-017; the host raw
`CozmoRobot.Message` tap (`CozmoEngine.cs`:2262 → `CozmoRobot.cs`:364, inline, under `_tickLock`) is the B-M2M15-2
finding-1 re-entrancy class (stack-only API).
Evidence summary to record: "dispatch mechanism 0x0069D004..0x0069D00E, 0x0069DE18..0x0069DE78, 0x0051D338..0x0051D932,
re-Init 0x00543FCA..; B0 HandleTrace log-only 0x0053C210..0x0053C488; EC telemetry only (OV126 r5)".

## M2-022

No local `SubscribeMessage(0x60…)` in src; `AnimationEventReport.cs`:16-18 queues then local-emits with no recipient
(D1/D2, 0x006625BA..0x006625FA). RemoveRobot only in HandleDisconnectMessage inside Rcm.Update before PopData
(`CozmoEngine.cs`:464-471, :502-503), as K4/K5 (0x0062FAF8). Blockers: no tag, no test (cheap production regression:
no 0x60 local subscriber after real startup; no RemoveRobot inside the dispatch loop); state R1-R3 as accepted.

## M2-023

`CozmoEngine.cs`:505-508 = PopData → +0x38 += 1 → GetFirstRobot → beq PopData (0x0069D8BC..0x0069D8D8). Mid-emit lead
answered by adopted evidence: C5 queues InParallel(5) with AddConcurrentAction, no cancel (0x00557C4C..0x00557CFE,
B-M2M15-2 check); stack `DockActionSubscriptions.cs`:53; a disconnect never runs inside an emit (K4/K5). Queued: native
zeroes MH+0x38/+0x3C each tick (`strd r6,r6,[r0,#0x38]` 0x004ED508); stack `ReceivedPacketCount` never reset (test-only
reader). Blockers: tag, relabel `M2M15HandlerBuilderTests.cs`:163-173 (unit), lead closure in text.

## M1-054

Production: CozmoRobot.Create → ctor `CozmoEngine.cs`:1924-1963; with no EngineConfigJson, `InitCheckedHostConfig`
MISSING (`EngineInitialization.cs`:76-80), `InitializeHostUiProcessing(null)` MISSING (`EngineReadiness.cs`:51), then
audio, DAS, vision, experiments, InitBeforeNeeds(null), InitCheckedDasTail, firmware header. Native 0x004EC9CE..
0x004ECA9E: LoadRobotConfigs, InitExperiments 0x004EC9D6, NeedsManager::Init 0x004ECA0A, RobotManager::Init 0x004ECA1A.

1. **Blocking:** NeedsManager::Init at 0x004ECA0A is never called at Init (`CozmoExperiments.cs`:146 MISSING); the
   NeedsManager exists only if a host builds a FreeplayStack (`Needs.cs`:909-933). M15-001 cites 0x004EC9E6..0x004ECA0A
   for the body; M1-054 keeps the call order, which is not kept.
2. **Init tail read:** PerfMetric::Init 0x004ECA22 → 2-byte no-op 0x0050947A; Info `CozmoEngine.Init.Version` "1"
   0x004ECA3A; sEvent `cozmo_engine.init.build_configuration` "SHIPPING" 0x004ECA72; +0x10 = 1 0x004ECA9C; return 0.
   Update refuses while +0x10 == 0 (0x004ED4DE..0x004ED56E); the stack always runs (success path). Two log lines
   missing (best effort).
3. **InitInternal 0x004EDC6C:** AudioUnityInput, then FactoryTestLogger GetNumLogs ≥ 100 → ArchiveLogs → Info/Warning
   (0x004EDC9E..0x004EDD0A), +0x61 = 0. On-device log files only: needs a recorded manager exclusion, not a MISSING.
4. **LoadRobotConfigs 0x00522280..0x00523042:** whether it loads anything no record owns is unread; narrow census.
5. **No COMPATIBILITY_POLICY names the engine-config JSON app input** (M1-058 covers seed, locale, UI, data platform).

Test labels match.

## M1-055

State-2 call 0x004ED5F0..0x004ED60E; BroadcastAvailableAnimations 0x0052E89C..0x0052E916 (null interface returns
first; per-name 0x0052F878; EndOfMessage(0) via 0x0052F918; Debug "Supposedly sent EndOfMessage"); tags 0x5D
(0x00725D0A), 0x9A (0x007291CA), 0x7B (0x007276F8); stack `AnimationAnnouncements.cs`:34-123. :119's false-return gate
is unreachable in production (recipient bound `CozmoEngine.cs`:1941; container set before progress 1,
`EngineReadiness.cs`:134-135). Production test asserts 997 independently counted names. Open: UNKNOWN tag-only local
E2G consumer of 0x5D/0x9A (M2-022's census for those tags, not available as a file); S3 recorded as a branch our system
never produces.

## M1-056

StartEngine 0x004ECBCC..0x004ECC4A and SetEngineState 0x004ECCBC (0x004ECCCE, 0x004ECCD2, 0x004EDC00 at 0x004ECCE0,
sEventF 0x004ECCE4..0x004ECD2A) match `EngineReadiness.cs`:56-85; state 1 0x004ED5D6..0x004ED5EE matches
`CozmoEngine.cs`:2080-2089; state 2 always sends 0x4ED7C0 (one f32, ctor 0x004ED7DE, ext-interface +0x1C); progress
literals 0x3F333333 (0x0051FBD8), 0x3E4CCCCD (0x0051F4C4), 1.0 at 0x0051F480..0x0051F48C match. Open:
PublishLoadingProgress (`EngineReadiness.cs`:142-147) has no local E2G emit (same census question); tag.

## Other records stronger than their evidence

- **M1-024 (EXACT_SOURCE):** its range 0x004ED4DE..0x004ED6C4 includes UpdateLatencyInfo (0x004ED64C), which sends E2G
  LatencyMessage (ctor 0x004EDA68, ext-interface +0x1C at 0x004EDA76) every 11th state-3 tick (countdown reset to 10 at
  0x004ED88C/0x004ED8AC), gated on `kNetConnStatsUpdate` (const 1, .rodata 0x00C95F41) and engine+0x60 == 0 (ctor
  memclr only; no writer found). `CozmoEngine.cs`:2057 drops it in a comment: an exclusion with no recorded decision.
  Also unread in that range: UiMessageHandler vtbl+0x44 call 0x004ED4FC, ctx+0x24 +0x10 zeroing 0x004ED510,
  UpdateAudioController tail 0x004ED6C4 (M6).
- **M1-041/M4-020 (EXACT_SOURCE):** the "SetOnRamp has no writer" text at `CozmoEngine.cs`:1154-1155 is contradicted
  (0x005380D2, 0x005381E6).

## M1-033 (HARDWARE_ONLY) for the script

1. Record the maximum frame size and packed-type mix the live packing path actually sends (M1-006/M1-012, bound 1406),
   so maximum-size frames are observed or explicitly "not exercised".
2. A sequence wrap needs ~65534 reliable messages: record "not exercised" unless the run is long.
3. Log any 0xBA/0xBB/0xC3 arrivals (data for M2-018's narrow check).
