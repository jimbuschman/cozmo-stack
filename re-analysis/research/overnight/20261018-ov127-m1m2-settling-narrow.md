| Question | Coverage | Citation / next read |
| --- | --- | --- |
| 1. Latency payload, gates and first tick | ANSWERED at publication/statistic-source boundary; upstream image statistic production and exhaustive global-writer closure UNKNOWN | Full UpdateLatencyInfo executable body 0x004ED828..0x004EDAB2, static word 0x01051004 = 10, payload/source table below. First send is eligible call 11, shared static countdown. |
| 2. Missing Impl subscriptions and ramp readers | ANSWERED for named bodies and identified ramp readers; firmware emission documentation UNKNOWN | C3 0x005366FC..0x005367AC; SetOnRamp 0x005164EC..0x005167B8; readers 0x00512BE2 and 0x00515E7A; remaining bodies listed below. |
| 3. LoadRobotConfigs census | ANSWERED for every read/store in requested range and its two-file helper; some consumer ownership joins UNKNOWN | Full 0x00522280..0x00523042 read; helper 0x00522074..0x005221F8. Fourteen direct JSON destinations, two helper destinations, feature and experiment initializers. |
| 4. Local E2G subscribers | UNKNOWN exhaustive closure; no positive requested-tag recipient established | Fresh Thumb scan finds 32 SubscribeToTags calls and 96 structural virtual +0x28 sites; thirty fixed initializer sets re-read. Exact limits and additional parameterized helpers below. |
| 5. Tick preamble residue | ANSWERED writes/targets; absence of all downstream diagnostic-counter readers UNKNOWN | UI slot +0x44 -> 0x00663420 zeroes +0xF8/+0xFC; ctx+0x24 is VizManager, store zeroes its +0x10. No robot send/app report in either reset body. |

OV127, answer to `requests/overnight/20261018-ov127-m1m2-settling-narrow.md`, 2026-10-18.
Coverage table first; native `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
Thumb addresses normalized; ARM PLT veneers resolved through GOT relocations, including rotated immediates.
Instructions and literal bytes are evidence; no decompiler output is cited. No production, record or inventory edit,
no tests, no hardware run. Research remains authority 6 pending manager checking. Classifications below identify
owning gaps; they do not promote records or claim a whole production path recovered.

## Current record boundary

Re-read current titles, statuses and evidence before this extraction. M1-061 is **RECOVERABLE_GAP**,
"CozmoEngine::Update state-3 residue: UpdateLatencyInfo's LatencyMessage, the UiMessageHandler vtbl+0x44 call and
the ctx+0x24 +0x10 zeroing"; evidence is the split from the 20261018 settling pass and the operator's build decision.
M2-024 is **RECOVERABLE_GAP**, "RobotToEngineImplMessaging Init subscriptions the stack lacks: B1, B2, B3, B7, BA,
BB, BC, BD, C3"; evidence summarizes the settling pass, not recovered payloads/reader closure.
M2-018, M2-019, M1-054, M1-055 and M1-056 remain **IMPLEMENTATION_GAP**. Their current unresolved explicitly
assigns these questions to OV127. M1-024, M1-041 and M4-020 remain **EXACT_SOURCE**, but the new M1-061/M2-024
splits mean this report must not treat their larger titles as proof of these missing steps.

## Q1. LatencyMessage

| Step | What the original does | Citation | Record | Classification |
| --- | --- | --- | --- | --- |
| L1 | State 3 updates robot connection, needs and all robots, then calls UpdateLatencyInfo; not a constructor/startup report. | 0x004ED61E..0x004ED652, actual call 0x004ED64E | M1-061 / M1-024 boundary | RECOVERABLE_GAP; this call/order read |
| L2 | If byte kNetConnStatsUpdate is zero, return. Shipped byte is 1 at 0x00C95F41. If engine+0x60 is nonzero, return. Neither skipped call decrements the countdown. | GOT 0x0103E870; 0x004ED836..0x004ED854 | M1-061 | RECOVERABLE_GAP; gates read |
| L3 | Static u32 word at 0x01051004 has initialized .data value 10. Nonzero -> subtract 1, store, return. Zero -> build/send path, first resetting it to 10. It is not an engine or robot member. | 0x004ED858..0x004ED868; reset 0x004ED88C/0x004ED8AC; ELF .data bytes 0A 00 00 00 | M1-061 | RECOVERABLE_GAP; cadence read |
| L4 | Therefore the first send is eligible invocation 11 and then every 11 eligible calls. State-0/1/2/4 ticks do not call it; disabled gates do not advance it. The static is shared across engine instances; constructing another engine does not give a new countdown. | L1-L3, static address rather than this-relative access | M1-061 | RECOVERABLE_GAP; derived directly from read control flow |
| L5 | Construct tag 0x77, copying 88 payload bytes; send through context external interface +0x1C, then destroy temporary. The external pointer is dereferenced without a null check. | 0x004EDA68..0x004EDA7A; ctor 0x00727442..0x00727456 (`r2=0x58`, tag=0x77) | M1-061 | RECOVERABLE_GAP; report boundary read |
| L6 | No statistics are cleared by this function. Writes besides its temporary packet are the countdown and guarded initialization of the fallback statistic at 0x01059180 (guard 0x010591B8). | 0x004ED8E4..0x004ED930; rest of full body through unwind tail 0x004EDAB2 | M1-061 | RECOVERABLE_GAP; full function read |

First-send count is **eligible calls**, not an unconditional wall-clock interval. With one normally ticking engine,
11 state-3 calls are 11 tick opportunities; the first call is after that tick's robot work. The +0x60 default-zero
construction/no-writer observation remains the prior settling check; this answer establishes its gate, not a new
universal writer proof.

### Complete payload mapping

Offsets below are within the **88-byte body**, excluding the union tag. All 22 fields are IEEE binary32, serialized
little-endian; groups are six `TimingInfo {avgTime_ms,minTime_ms,maxTime_ms}` followed by
`CurrentTimingInfo {avgTime_ms,minTime_ms,maxTime_ms,currentTime_ms}`. Native Pack calls at
0x0071988A..0x007198D8 use object offsets 0,0xC,0x18,0x24,0x30,0x3C,0x48. App generated definitions corroborate
the names in `unity/scripts/csharp/Anki.Cozmo.ExternalInterface/{LatencyMessage,TimingInfo,CurrentTimingInfo}.cs`.

| Body offsets / names | Exact sources and arithmetic | Publication instructions |
| --- | --- | --- |
| 0x00/04/08 wifiLatency avg/min/max | Raw f32 words gNetStat2LatencyAvg @0x0105DEA4, gNetStat4LatencyMin @0x0105DEAC, gNetStat5LatencyMax @0x0105DEB0. No integer-to-float conversion. | Loads 0x004ED8AE..0x004ED8CC; stores 0x004EDA02..0x004EDA06 |
| 0x0C/10/14 extSendQueueTime avg/min/max | Raw f32 words gNetStat7ExtQueuedAvg_ms @0x0105DEB8, gNetStat8ExtQueuedMin_ms @0x0105DEBC, gNetStat9ExtQueuedMax_ms @0x0105DEC0. | 0x004ED86A..0x004ED8C0; 0x004EDA06..0x004EDA10 |
| 0x18/1C/20 sendQueueTime avg/min/max | Raw f32 words gNetStatAQueuedAvg_ms @0x0105DEC4, gNetStatBQueuedMin_ms @0x0105DEC8, gNetStatCQueuedMax_ms @0x0105DECC. | 0x004ED86A..0x004ED894; 0x004EDA12..0x004EDA20 |
| 0x24/28/2C recvQueueTime avg/min/max | MessageHandler from engine.ctx+0x20 -> +0x60, GetQueuedTimes_ms -> RCM. Selected statistic's f64 +0x20 mean, +0x10 min, +8 max, each `vcvt.f32.f64`. | Getter call 0x004ED8D4, loads 0x004ED8D8..0x004ED8E0, converts 0x004ED932..0x004ED93C, stores 0x004EDA34..0x004EDA3C |
| 0x30/34/38 unityEngineLatency avg/min/max | UI GetLatencyStats(0), same f64 offsets mean/min/max -> f32. Type 0 is Direct. | Call 0x004ED99E, loads/converts 0x004ED9C0..0x004ED9DE, stores 0x004EDA40..0x004EDA48 |
| 0x3C/40/44 sdkEngineLatency avg/min/max | UI GetLatencyStats(1) and (2), UDP/TCP. Compare returned statistics' f64 counts at +0x18. Select UDP only if its count is strictly greater; otherwise TCP (including tie/unordered). Selected +0x20/+0x10/+8 -> f32. | Calls 0x004ED9A8/0x004ED9B2; compare/select 0x004ED9B6..0x004ED9E4; 0x004ED9E6..0x004EDA30 and stores 0x004EDA4C..0x004EDA54 |
| 0x48/4C/50 imageLatency avg/min/max | First robot, if any. Select recent image statistic at Robot+0x400 only if its f64 count (Robot+0x418) > first window count (Robot+0x3E0); otherwise Robot+0x3C8. Selected +0x20/+0x10/+8 -> f32. No *1000 here. | GetFirstRobot 0x004ED940; selection 0x004ED94A..0x004ED98A; loads 0x004ED9F2..0x004ED9FA, converts/stores 0x004EDA24..0x004EDA64 |
| 0x54 imageLatency currentTime_ms | With robot: load f64 Robot+0x3B8 and +0x3C0; choose +0x3B8 only when +0x3C0 is less (`VMOVMI`), otherwise +0x3C0. Multiply in f64 by 1000.0 (literal 0x004EDAB8); only then convert to f32. Without robot: 0.0f at 0x004EDB0C. | 0x004ED946..0x004ED972; fallback 0x004ED990..0x004ED998; store 0x004EDA60 |

`GetQueuedTimes_ms` chain is fully read: MessageHandler 0x0069DED4..0x0069DEDC loads its RCM at +0x24;
RCM 0x0062FE14..0x0062FE34 chooses the second window only when count at +0x90 > count at +0x58, returning
RCM+0x78 instead of +0x40. UI `GetLatencyStats` 0x00663350..0x006633DE checks type <=2 and nonnull slot
UI+4+4*type; similarly picks comms+0x40 instead of +8 when second count at comms+0x58 > first at +0x20.
These are **sample counts**, not which sample is newer or which latency is larger.

The underlying statistic layout is f64 sum +0, max +8, min +0x10, count +0x18, mean +0x20, prior mean +0x28,
and sum-of-squares update +0x30. Native accumulator update 0x0062F6A2..0x0062F70E establishes this: add sample
and count, compare/update max/min, then mean += (sample-oldMean)/newCount. RecentStatsAccumulator ctor and
window-update code 0x0082FB64..0x0082FCBA establish two 0x38-byte windows. This read does not substitute
the stack's LastPingRoundTripMs for these aggregates.

Missing/null UI slots use the static empty statistic at 0x0105B628, guarded at 0x0105B660
(0x00663382..0x006633DE). No-robot image stats use 0x01059180. Both initialize mean/count/sum to zero,
max to -DBL_MAX and min to +DBL_MAX. Their float conversions therefore preserve mean=0, min=+infinity,
max=-infinity. Returning an all-zero TimingInfo would change the payload.

**Statistic provenance limits:** the nine transport globals are .bss (initially zero). A fresh bounded Thumb GOT-ref
scan found only the nine loads in this function; ReliableTransport::UpdateNetStats at 0x00837D54 is `bx lr`.
This supports zero-initialized globals on the checked paths; a universal proof excluding ARM/computed/direct-address
writers remains UNKNOWN. Do not populate them with plausible ping statistics. For image current latency,
Robot::Update writes +0x3B8 from max(0, f64(now_f32)-VisionComponent+0x130) at 0x00514352..0x00514386;
constructor zeroes +0x3B8/+0x3C0 at 0x00510120/0x00510124. Upstream +0x3C0 and image-window sample writers
have not been closed in this answer. Their exact remaining producer addresses are UNKNOWN.

**Stack counterpart check:** ReliableConnection has per-message queued timestamps and LastPingRoundTripMs, but
the bounded searches in `Cozmo.Transport`, `CozmoEngine` and `Camera` did not identify the native nine global
aggregates, RCM two-window receive statistic, UI Direct/UDP/TCP two-window accumulators, or Robot's image latency
accumulators as live report owners. These are missing source-statistic joins, not permission to derive new averages.
SDK execution remains excluded; the native LatencyMessage's SDK fields nevertheless contain the values above.

## Q2. Impl handlers and ramp state

| Step | What the original does | Citation | Record | Classification |
| --- | --- | --- | --- | --- |
| H1 C3 | Log Info `Robot.HandleRobotPoked`, then E2G RobotPoked tag 0x33, **empty body**. Does not read robot input payload or write a persistent robot field. | Full 0x005366FC..0x005367AC; Broadcast 0x00536754; union ctor 0x00723D86..0x00723D8A only writes tag; zero-size body 0x00711D00 | M2-024 | RECOVERABLE_GAP; body read |
| H2 BA | Log ramp traversal started, then SetOnRamp(true); input payload ignored by this lambda, including timestamp. Return value ignored. | 0x00538088..0x00538100, call 0x005380D2 | M2-024 | RECOVERABLE_GAP; body read |
| H3 BB | Log ramp traversal completed, then SetOnRamp(false); input completion payload ignored by this lambda. Return value ignored. | 0x0053819C..0x00538214, call 0x005381E6 | M2-024 | RECOVERABLE_GAP; body read |
| H4 BC/BD | Log started/completed traversing bridge. No setter, persistent state write, send or E2G report. Temporary vector allocation/free only, including unwind cleanup. | Full executable bodies 0x005382B4..0x00538320 and 0x005383C0..0x0053842C | M2-024 | RECOVERABLE_GAP; log-only established |
| H5 B2 | If payload word +0 nonzero: Warning Robot.MainCycleTooLong with robot ID, +0 count and +0xC average. If payload +8 nonzero: Warning Robot.MainCycleTooLate with robot ID, +8 count and +4 average. No persistent store, send or error-flag setter. | 0x005384CC..0x00538590; logger calls 0x005384FA/0x0053853E, not sErrorF | M2-024 | RECOVERABLE_GAP; log-only established |
| H6 B7 | Get DataDump, format its vector bytes as hex into temporary stack storage, Info RobotMessageHandler.ProcessMessage.MessageDataDump with robot ID, vector size and hex. No persistent state/send. | Thunk 0x0053866A -> full 0x00538694..0x00538772; FormatBytesAsHex 0x005386CE, log 0x005386FA | M2-024 | RECOVERABLE_GAP; log-only at handler boundary |
| H7 B1 | Get PrintText, print `ROBOT-PRINT (%d): %s` with robot ID and string; no persistent state or send. | 0x0053330C..0x0053332C; Thumb/ARM veneer 0x008CB3BC..0x008CB3C8 -> printf PLT 0x004A5A88 | M2-024 | RECOVERABLE_GAP; print-only established |
| H8 B3 | Build temporary goal Pose3d, call VizManager::DrawPreDockPose id 100, RED for nonzero payload byte +0x14 else GREEN, destroy temporaries. No robot pose setter. | 0x005352C8..0x00535378, calls 0x0053533C/0x0053534C; DrawPreDockPose 0x006BE98C..0x006BEA24 | M2-024 | RECOVERABLE_GAP; Viz-only established |

B3 ultimately enters Viz DrawObject; the previously checked Viz terminals, re-read here, are `bx lr` at
0x006BE610 and 0x006C05EC. This supports no robot/API output from B3, with temporary geometry and optional
ID-range warning. It is not a general claim that all pose-setting routines have no effect.

Native union constructors establish the names/directions independently of the stack's generated catalog:
B1 PrintText (0x007B2B54), B2 MainCycleTimeError (0x007B2C40), B3 GoalPose (0x007B2D04), B7 DataDump
(0x007B3026), BA RampTraverseStart (0x007B3232), BB RampTraverseComplete (0x007B32CE), BC BridgeTraverseStart
(0x007B335E), BD BridgeTraverseComplete (0x007B33FA), C3 RobotInterface::RobotPoked (0x007B37DE).
The stack catalog spells the ramp/bridge names Started/Completed; those are host names, not extra message tags.

### SetOnRamp is not a Boolean-only assignment

Full executable body 0x005164EC..0x005167B8 (unwind tails included):

| Step | What the original does | Citation | Record | Classification |
| --- | --- | --- | --- | --- |
| R1 | If requested bool equals Robot+0x316, return 0 with no lookup/write. | 0x005164FA..0x00516504 | M2-024 | RECOVERABLE_GAP; read |
| R2 | Look up the stored ramp ObjectID at Robot+0x318 in BlockWorld, family 3; dynamic-cast to Ramp. Missing/wrong type warns Robot.SetOnRamp.NoRampWithID and returns 1 **without changing +0x316**. Warning prints field +0x31C. | 0x00516506..0x00516530; 0x005165D8..0x00516618 | M2-024 | RECOVERABLE_GAP; read |
| R3 | Entering: copy current Robot pose translation into f32 +0x320/+0x324/+0x328, log transition; then store requested byte to +0x316 and return 0. | 0x00516536..0x00516560; store 0x005165D2 | M2-024, M11 pose dependency | RECOVERABLE_GAP; caller effects read |
| R4 | Leaving: byte +0x32C selects Ramp::GetPostAscentPose(48.0f) for 0, GetPostDescentPose(48.0f) for 1; another value warns UnexpectedRampDirection and returns 1 without flag change. The valid pose is copied and passed to SetNewPose. | 0x00516626..0x00516682; literal 0x42400000; invalid 0x005166C2..0x005166D6 | M2-024, M11 pose dependency | RECOVERABLE_GAP; caller effects read |
| R5 | SetNewPose nonzero warns and returns its nonzero result, retaining flag/direction. Success sets +0x32C=2, queries history's newest timestamp for the log, logs new pose/yaw, then stores false to +0x316 and returns 0. | 0x00516684..0x005166C0; 0x005166D8..0x0051676C ->0x005165B0/0x005165D2 | M2-024 | RECOVERABLE_GAP; read |
| R6 | Constructor sets +0x316=0. Direct-offset Thumb census found two non-setter readers: UpdateFullRobotState and LocalizeToMat. | 0x0051009A; 0x00512BE2; 0x00515E7A | M2-024, M1-041/M4-020 leads | RECOVERABLE_GAP; bounded reader census |
| R7 | UpdateFullRobotState: first constructs a temporary Pose3d from an empty name at sp+0x44. When on-ramp, passes that fresh default-constructed pose and incoming RobotState to AddRobotStateToHistory, skipping the normal origin-ID/pose-reconstruction branch at 0x00512C3E; on success joins current-pose update at 0x00512D1C. Do not substitute the normal reconstructed incoming-odometry pose on this branch. | Construction 0x00512BAC..0x00512BD0; 0x00512BE2..0x00512BF8; normal branch through 0x00512D02; join 0x00512D1C..0x00512D74 | M2-024 / M4-020 / M11 history | RECOVERABLE_GAP; branch read |
| R8 | LocalizeToMat: on-ramp bypasses the off-ramp rotation-axis validation, mat-height adjustment and yaw-only rotation reconstruction. Both paths join at 0x00515F4C and continue localization. | 0x00515E7A..0x00515F4C | M2-024 / M11 localization | RECOVERABLE_GAP; branch read |

Consequently BA/BB affect downstream localization/history/robot pose; "no effect" or simply assigning true/false
would be unsupported. Full Ramp::GetPost*Pose geometry, all SetNewPose downstream behavior, and alternative
computed/indirect accesses to +0x316 are not newly recovered here; their existing pose owners remain dependencies.
The direct branch trace also reaches UpdateCurrPoseFromHistory and conditional cliff-threshold traffic
(0x00512DA2,0x00512EA0); no assertion of a fixed unconditional send on every ramp notification is made.

**Firmware documentation:** a text search of shipped app/asset sources for these names found generated E2G
RobotPoked definitions, not firmware sender documentation. The OBB contains `config/engine/firmware*/cozmo.safe`
and `old_firmware/cozmo.safe` binary artifacts. Their presence and native receiver subscriptions prove neither
which firmware sends BA/BB/C3 nor its send conditions. Those firmware emission conditions, including firmware 2457,
remain UNKNOWN; no hardware run or firmware extraction was performed. The request asks for documentation, not
an assumption that a subscribed tag must be sent by the operator's robot.

## Q3. LoadRobotConfigs census

The **entire requested instruction range** 0x00522280..0x00523042 was read in consecutive windows, with boundary
instructions re-read where a 32-bit instruction straddled a window. DataPlatform null at +4 returns before all
loads (0x00522286..0x0052228A). Reads use scope 1. There is no per-key extraction in this routine: each destination
is a complete Json::Value. Paths below begin `config/` under shipped resources. Consumer ownership is distinguished
from source support for this loader store; a related subsystem is not proof the field's entire consumer is settled.

| Step / file | Destination and native citation | Existing consumer record / current status |
| --- | --- | --- |
| C1 engine/mood_config.json | loader+0xD0; path literal 0x00523130; read 0x005222EC..0x005222F8 | M7-012 "Mood schema, decay graphs, action-result events, affectors, update and game output" / IMPLEMENTATION_GAP. |
| C2 engine/behaviorSystem/activities_config.json | +0xE8; literal 0x00BE7E35; read 0x005223C2..0x005223D2 | M15-013 "Activity tree construction, desired names and discarded activityPriority" / IMPLEMENTATION_GAP. |
| C3 engine/behaviorSystem/behavior_system_config.json | +0x100; literal 0x00BE7ECC; read 0x005224A6..0x005224B6 | Related activity tree/chooser M15-013/M15-002; M7 inventory also names this root activity file. Exact full field-consumer ownership join UNKNOWN; do not substitute M7-018's individual behavior-config factory claim. |
| C4 engine/behaviorSystem/workout_config.json | +0x148; literal 0x00BE7F2E; read 0x0052258A..0x0052259A | M13-010 "The workouts run in file order, the last one repeats, and ShouldPlayEightiesMusic rolls against the mood score" / IMPLEMENTATION_GAP; evidence includes WorkoutConfig::InitConfiguration 0x005736E8. |
| C5 engine/vision_config.json | +0x118; literal 0x00BE7FC0; read 0x00522672..0x0052267C | M11-059 Vision Init; M11-082 "Vision config loading: JsonCpp getMemberNames order (InitialVisionModes EnableMode order) and RobotDataLoader::LoadRobotConfigs' readAsJson failure branch" / IMPLEMENTATION_GAP. |
| C6 engine/game_request_weights.json | +0x238; helper call 0x005226EE -> read 0x0052208E..0x0052209E; literal 0x005221FC | Related M15-057 (PlayAGameRequest), but no explicit weight-file consumer record identified in current manifest search. Exact consumer ownership UNKNOWN; no no-effect claim. |
| C7 engine/do_a_trick_weights.json | +0x250; helper read 0x0052212C..0x0052213C; literal 0x00BE7DA7 | Related M15-057 (RequestATrick) but no explicit weight-file consumer record identified. Exact ownership UNKNOWN. |
| C8 engine/needs_config.json | +0x160; prefix 0x00C49B50 + .json; read 0x00522784..0x0052278E | M15-001 "NeedsManager initialization, periodic update and need-bracket rules" / IMPLEMENTATION_GAP. |
| C9 engine/needs_level_config.json | +0x178; prefix 0x00C49B70 + .json; read 0x00522892..0x0052289C | M15-001; existing M15 inventory config mapping names this field and StarRewardsConfig consumer. |
| C10 engine/needs_action_config.json | +0x190; prefix 0x00C49B98 + .json; read 0x005229A0..0x005229AA | M15-001; existing M15 inventory maps it to ActionsConfig. |
| C11 engine/needs_decay_config.json | +0x1A8; prefix 0x00C49BC0 + .json; read 0x00522AAE..0x00522AB8 | M15-001/M15-004 "Needs decay modifiers and damaged-part thresholds" / IMPLEMENTATION_GAP. |
| C12 engine/needs_handlers_config.json | +0x1C0; literal 0x00BE81F6; read 0x00522B8A..0x00522B94 | M15-001; inventory mapping names desired-face distortion handlers. |
| C13 engine/local_notification_config.json | +0x1D8; literal 0x00BE8296; read 0x00522C66..0x00522C70 | M15-001; inventory mapping names local-notification initialization. |
| C14 engine/das_event_config.json | +0x220; literal 0x00BE833B; read 0x00522D42..0x00522D4C | M8-022 "LoadDasBlacklistedAnimationTriggers and its configuration source" / IMPLEMENTATION_GAP. Evidence already owns this read and manager handoff. |
| C15 engine/tts_config.json | +0x1F0; literal 0x00BE83C5; read 0x00522E1A..0x00522E28 | No explicit TTS-config consumer owner identified in current manifest search for tts_config/TextToSpeech/TTSConfig. Ownership UNKNOWN, not established absent or no effect. |
| C16 features.json | Temporary read string -> CozmoFeatureGate::Init on context+0x10; literal 0x00BE8449, path 0x00522EBC, ReadFile 0x00522ED2, Init 0x00522EDC | M15-007 "Activity feature gate and dead boredomMultiplier scoring data" / EXACT_SOURCE. This census does not strengthen all feature consumers. |
| C17 experiments.json | Temporary read string -> context+0x38 object's embedded AnkiLab at +8; literal 0x00BE845E; ReadFile 0x00522F38, Load 0x00522F44 | M15-049 "Lab experiment definitions: AnkiLab::Load (experiments.json) and the InitExperiments enable tail" / IMPLEMENTATION_GAP. |
| C18 engine/inventory_config.json | +0x208; literal 0x00BE8476; read 0x00522FC6..0x00522FD0 | M15-035 owns inventory callback/sink, not an explicit config read claim in its evidence. The exact config-consumer ownership is UNKNOWN; do not settle it from the NV record title. |

C1-C15/C18 are loader Json::Value stores; classification at this startup boundary remains M1-054's
IMPLEMENTATION_GAP. Consumer records quoted above were re-read for title/status/evidence; no record or inventory
changed. Consumer bodies were not re-extracted because the request is a census only.

Failure census: each read failure logs and sets the global error flag via the `_errG` stores in the function.
Activities +0xE8, behavior system +0x100 and workout +0x148 additionally call Json::Value::clear
(0x00522444,0x00522528,0x0052260C). Both weight-helper failures also clear their destination
(0x00522104,0x005221A4). The other direct JSON failures do not add a rollback/clear call here. The feature and
experiment reads pass their returned strings to Init/Load; this function does not parse their fields. It continues
through the census rather than returning a per-file failure result. No new fidelity status is inferred from logs.

## Q4. Local E2G consumers: bounded negative result, not an absence proof

| Step | What the original does / scan result | Citation | Record | Classification |
| --- | --- | --- | --- | --- |
| S1 | E2G Subscribe reads a u16 tag, obtains that tag's signal and attaches callback; Broadcast emits its local signal inline after app handoff. | Full 0x006626CC..0x00662746; dispatch 0x00662590..0x006625AE; emitter 0x00663C74..0x00663CB8 | M1-055/056, M2-019, M1-061/M2-024 | RECOVERABLE_GAP for concrete recipient closure; mechanism read |
| S2 | Message tags are 5D AnimationAvailable, 9A EndOfMessage, 7B UpdateEngineState, **2E EngineLoadingDataStatus**, 2A EngineRobotCLADVersionMismatch, **77 LatencyMessage**, **33 RobotPoked**. | Prior checked constructors 0x00725D0A/0x007291CA/0x007276F8; newly read constructors 0x007239F4,0x00727442,0x00723D86; M2-019's current evidence for 2A | Same | Payload getters alone would miss tag-only subscribers |
| S3 | Fresh linear Thumb scan 0x004E0000..0x008CB000 finds 32 direct E2G SubscribeToTags calls, matching OV44's addresses; thirty fixed initializer sets re-read from binary and exclude every requested tag. Two sites forward caller-owned sets. | First 0x005C1F42, last 0x00614430; parameterized 0x0060F5D8/0x006137B6; matrix below | Same | RECOVERABLE_GAP; bounded evidence |
| S4 | No direct BL/BLX/B/B.W to the requested payload getters was found in that Thumb scan. | Getter symbols include 0x007239FE,0x00727458,0x00723D8C; prior checked 5D/9A/7B getters | Same | Does not close tag-only/indirect callbacks |
| S5 | Structural scan (BLX register whose vtable +0x28 load is in preceding 14 instructions) finds 96 sites. These include subscriptions, parameterized wrappers, unrelated object/stream calls and type-erased helpers. | Native scan reproduced OV44's 96 sites; additional helper examples below | Same | Exhaustive concrete subscriber list UNKNOWN |

Static initializer addresses and actual u16 tag sets, all re-read here:

```
C67960:47; C67CB0:11; C68020:44,45,35,22; C68370:44;
C69694:5A,44,0F,34,35; C6A290:1E,59;
C6A45C:44,4C,0F,19,34,35,1E,28; C6AAE4:35; C6AD10:44;
C6DBC0:35; C6E530:46,4B; C6EF80:32; C6F850:46; C6FF00:0D,0F,11;
C71100:49,22,35; C715D0:4A,43; C71B60:3C,47,35,22;
C72442:88,44; C72BB0:44; C73740:22,34,39; C73D30:3A,3B,1E;
C74030:1E; C751A0:3C; C759F0:0F,10,44,11,32; C75A40:46;
C76710:8B; C76812:35; C76898:44; C769C0:32; C76FD0:39.
```

The prior generic factory's fixed bytes at 0x00C75AC0..0x00C75ACB were also re-read:
22,34 / 1E / 3A / 35 / 3C. They introduce none of the seven tags. The native virtual Subscribe entry is concrete
UI+0x28, distinct from G2E+0x2C. A shared numerical tag in another union is not an E2G recipient.

**Important unresolved call form:** additional type-erased helper sites 0x0052771A,0x005277DA,0x0052789A,
0x0053B42A,0x0053B4EA,0x0053CECE,0x0063538E load the requested tag from their incoming r3 and pass its
stack u16 at +6 to the virtual call. Other such sites include 0x005AD7EE,0x005AFED2,0x005B1336/0x005B13F6,
0x005B3F86,0x005B4476,0x005B48E2. A nearest exported symbol name (often the previous function) is not a
receiver/type proof for these anonymous helpers. Their full call-site/relocation/parameter closure is still
UNKNOWN. The action sites in OV44 also include parameterized IVisuallyVerifyAction (0x005688CA), whose two
known callers pass 44/46; that is not proof of every possible indirect caller.

Thus, separately for **each of 5D,9A,7B,2E,2A,77,33**: positive subscriber established = none in the checked
fixed sets; exhaustive subscriber list = **UNKNOWN**; subscriber effects = **UNKNOWN until a positive recipient
is found**. This answer does not assert "no local subscriber", "no effect", or that the local emit may be deleted.
Remaining scan limits also include tail BX, ARM-state sites, vtable loads more than 14 instructions away,
computed offsets and concrete direct Subscribe calls outside the matched pattern. These are recoverable source
gaps; neither a passing C# test nor the previous scan turns them into absence evidence.

## Q5. Tick preamble residue

| Step | What the original does | Citation | Record | Classification |
| --- | --- | --- | --- | --- |
| T1 | Every initialized tick, before UI Update/state switch, calls UI vtable +0x44. Concrete slot 0x0102FE60 holds 0x00663421 (Thumb), body writes two zero u32s to UI+0xF8/+0xFC and returns. | 0x004ED4F8..0x004ED4FE; **0x00663420..0x00663426** | M1-061 | RECOVERABLE_GAP; target/body read |
| T2 | +0xF8 increments on processed game message; +0xFC increments on DeliverToGame publication. Getters return these words. The reset does not clear the app queue, statistics windows or subscriber map. | +F8 0x00661FF0/0x00661FF8; +FC 0x006615C4/0x006615CA; getters 0x00663414..0x0066341E | M1-061 | RECOVERABLE_GAP; field accesses established |
| T3 | Separately zero MessageHandler +0x38/+0x3C via context+0x20 -> +0x60. Then load context+0x24 (VizManager) and zero its word +0x10. This also precedes UI Update, not only the state-3 branch. | 0x004ED500..0x004ED514; VizManager ctor 0x006BE5BC..0x006BE604 | M1-061 | RECOVERABLE_GAP; writes/owner read |
| T4 | The requested reset bodies emit no report or robot packet and make no further call. Viz drawing paths set byte +0x10; Viz send terminals return immediately. | UI reset above; Viz stores 0x006BE7B0/0x006BE978/0x006BEA72/0x006BECA2; terminals 0x006BE610,0x006C05EC | M1-061 | RECOVERABLE_GAP; narrow no-send fact |

The two operations are preamble bookkeeping, not latency accumulator resets. For the stronger conclusion "no
downstream observable effect anywhere", the consumer closure of the UI count getters and out-of-class accesses
to VizManager+0x10 remains UNKNOWN here. The bounded Viz scan includes unrelated global/table accesses at +0x10;
they cannot be called field readers merely because the displacement matches. No unsupported robot/API exclusion
is inferred. The checked Viz no-output result is retained, but does not prove that arbitrary diagnostic counters
may be reused as traffic-control state.

## Handoff and remaining uncertainty

The complete publication path traced here is state-3 tick -> eligible shared countdown -> source statistics ->
88-byte LatencyMessage/tag77 -> external-interface Broadcast boundary; its concrete local recipient closure is
still UNKNOWN. C3 -> empty tag33 and the named log/Viz handler bodies are read. BA/BB -> SetOnRamp -> identified
history/localization branches are read at their consumer boundaries; downstream pose primitives remain owned
dependencies. The LoadRobotConfigs census covers every requested normal-path read and its weight helper.

No current record is silently promoted. The settling pass's older "SetOnRamp has no writer" assumption remains
contradicted by BA/BB; **the current manifest already records that contradiction and splits M2-024**, so this is
not a new finding against an earlier version of M1-041/M4-020. The primary record-impact is recoverable detail for
M1-061/M2-024, plus explicit unclosed joins in M1-054/055/056 and M2-019.

Regression tests: none run or added (extraction only). No implementation change, no hardware acceptance. Remaining
UNKNOWNs are exact, not filled by analogy: global-stat writer closure; +0x3C0/image-window sample producer closure;
all seven local subscriber inventories and their effects; the listed config consumer ownership joins; full
counter/Viz-reader closure; firmware sender conditions. The successful subanswers above remain usable independently
of those gaps. Commit/push contains this answer only.
