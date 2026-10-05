| Coverage | Status | Result |
| --- | --- | --- |
| Item 6: features and every reader row | CHECKED | Every cited range reopened; three WRONG rows; exception destination UNVERIFIABLE. |
| Item 7.1: every teardown row | CHECKED | Every cited range reopened; three WRONG rows; recursive child effects UNVERIFIABLE. |
| Item 7.2: every sleep row | CHECKED | Tree, constants and deadlines checked; absolute caller exclusivity UNVERIFIABLE. |
| Skipped steps | CHECKED | Added below with instructions or UNKNOWN. |
| M1-031/044/045 split | CHECKED | Explicit lower boundaries and cited higher-layer obligations below. |

# B-M1M2 pre-build row check

Answers the operator's 2026-10-05 request about `re-analysis/jobs/B-M1M2.md` and items 6–7 of `20260930-bcore-extractions.md`. Pulled first: already up to date. Research only; no production, inventory, manifest, approval, tests or commit changes.

CHECKED means the requested comparison was performed, including preserving unresolved assertions as UNVERIFIABLE. It does not mean the entire recursive production path is recovered. HOLDS retains the bounded assertion; WRONG gives corrected build instructions; UNVERIFIABLE must not become an assumed implementation.

Evidence: shipped ARMv7 engine independently decoded from bytes; [instruction companion](20261005-B-M1M2-native.txt) reopens every original cited span and additional callees. Linear dumps contain pools/tables/adjoining functions too: only the executable instructions identified below are evidence. PLT names come from ELF relocations; veneers are followed to their literal targets. Decompiler output was navigation only.

HEAD: `55ae3657f2f0c270804842b944b74aa36d430860`.

SHA-256 `resources/lib/armeabi-v7a/libcozmoEngine.so`: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.

SHA-256 `re-analysis/research/20260930-bcore-extractions.md`: `901ecbbbe600e3336b6a9116917fcb3feab84ecfc95bce40e4b47184a1a84c34`.

SHA-256 `re-analysis/fidelity_manifest.json`: `5aeb86b0d3b1018dfb9efd99c2311de1139a205221c661124b752acdff776830`.

## Current records quoted before corrections or scope changes

### M1-029

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
  "unresolved": "Opus verification of B-CORE (2026-09-30, re-analysis/research/20260930-B-CORE-verify-5-6.md): NOT YET, NEEDS EXTRACTION. (1) nesting depth over 1000 throws in jsoncpp (0x008E09AC cmp #0x3e8, bhi; throwRuntimeError 0x008E0D92), and [reader+0x14] counts the root; the C# returns false, off by one. (2) {\"\":1,} is accepted (0x008E0FBA), the C# rejects it. (3) decodeNumber (0x008E1CF2..) is unread, so AsUInt's int/uint/real typing is assumed ({\"v\":-0} throws in the C#), and M1_029_11n's expected values rest on it. (4) where the engine catches the throw is not established (possibly std::terminate, 0x00677A4E); prose only. (5) System.Text.Json stands in for the shipped jsoncpp and differs on truex, invalid UTF-8, 5garbage, leading zeros and raw control characters: contrary to Exact, always. Rows 11a..11r are not in the inventory. Before: built, awaiting strong verification: the jsoncpp reader behind the firmware check now reproduces the default Features (row 11d: comments accepted, trailing commas and numeric keys rejected, any value type at the root, text after the root ignored, depth over 1000 throws), the accessor sequence (11l/11q), and asUInt (11n: null 0, bool 0/1, string/array/object and a negative or out-of-range real throw Json::LogicError, a real in [0, 4294967295.0] truncates toward zero). The engine's Json::LogicError capture is still not established (row 11o RECOVERABLE_GAP): the robot-message handler is wrapped by CozmoEngine.Isolated and the loader thread logs and leaves the expected values 0/0 (G5.31). Tests: EngineAppLayerTests.M1_029_11d_11e_11i_CommentsAnyRootAndTrailingText, M1_029_11f_11g_TrailingCommasAreAnError, M1_029_11n_AsUIntOfANonNumber, M1_029_11q_ANonNumberVersionThrows",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.M1_029_G5_10_ASimulatorIsAcceptedWithoutRobotAvailable, EngineAppLayerTests.M1_029_G5_11_G5_12_M1_040_TheVersionOutcomeIsComputedAndThenNotApplied, EngineAppLayerTests.M1_029_G5_14_TheExpectedValuesAreCopiedWhenTheRobotIsAdded, EngineAppLayerTests.M1_029_G5_19_G5_31_AShortOrMissingFileGivesNoHeader, EngineAppLayerTests.M1_029_G5_21_TheShippedHeaderParsesTo2381, EngineAppLayerTests.M1_029_G5_2_G5_4_CB13_FactoryAndUnparsableFirmwareProceedUnderM1_040, EngineAppLayerTests.M1_029_G5_31_WithNoHeaderTheExpectedValuesAre0AndTheRobotIsStillAccepted, EngineAppLayerTests.M1_029_11d_11e_11i_CommentsAnyRootAndTrailingText, EngineAppLayerTests.M1_029_11f_11g_TrailingCommasAreAnError, EngineAppLayerTests.M1_029_11n_AsUIntOfANonNumber, EngineAppLayerTests.M1_029_11q_ANonNumberVersionThrows"
}
```

### M1-031

```json
{
  "id": "M1-031",
  "subsystem": "M1-transport",
  "title": "Idle-timeout disconnect",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "B34 StartIdleTimeout: deadline = now + disconnectTime_s if >= 0, keeping an earlier deadline (0x0052D030..0x0052D066); Cancel sets -1 (0x0052D06C); on expiry Update clears it and calls MessageHandler::Disconnect (0x0052CE6E..0x0052CE98); the reason is not set; driven from unity/scripts/csharp/PauseManager.cs:219, :289, :360",
    "CC1..CC7: the idle component has two deadlines: faceOff (armed only after the first full robot state, robot+0x34E) and disconnect; earliest wins; Cancel sets both to -1; expiry sets 0.0, which blocks re-arming until a Cancel; sleep fires before disconnect in one Update (0x0052CC64..0x0052D0C4; 0x0052CE3C..0x0052CE98)",
    "CC8: the sleep half queues a go-to-sleep animation sequence (0x0052CEA2..0x0052CFBE), an interface to the animation layer (M5); CC10: deadlines are checked once per 60 ms tick in engine state 3"
  ],
  "effect": "the link is dropped after a pause at a different time, or never",
  "provenance": "reproduced by the code; settled after batch 3's read-only verification (2026-09-24)",
  "unresolved": "Opus verification of B-CORE (2026-09-30, re-analysis/research/20260930-B-CORE-verify-3-4.md): NOT YET. Its path defers to M1-045, which is not built. Before: Audit 2026-09-29 (re-analysis/research/20260929-audit-complete.md): the settlement did not hold. on the faceOff deadline the engine queues CreateGoToSleepAnimSequence (0x0052CE5A..0x0052CE6A); B-CORE batch 4 gives the action its own record (M1-045) and the stack names it (CozmoEngine.QueueGoToSleep logs MISSING and raises GoToSleepRequested), because no ActionList or animation-sequence action layer exists.",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.M1_031_CC3_CC6_TheDisconnectDeadline, EngineAppLayerTests.M1_031_CC6_SleepFiresBeforeDisconnectInOneUpdate, EngineAppLayerTests.M1_031_CC7_AnExpiredDeadlineBlocksReArmingUntilCancel"
}
```

### M1-044

```json
{
  "id": "M1-044",
  "subsystem": "M1-transport",
  "title": "RemoveRobot's upper-layer teardown: Robot::~Robot aborts all actions and destroys the behaviour, mood, AI/freeplay, path, map, docking, carrying and vision components",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoRobot.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "RemoveRobot 0x0052F2F6 calls Robot::~Robot (0x005110D4) then operator delete (0x0052F2FA); RobotManager::RemoveRobot also calls NeedsManager::OnRobotDisconnected 0x0052F2E0, PerfMetric::OnRobotDisconnected 0x0052F2E8, DASPauseUploadingToServer(0) 0x0052F2EE, erases the map/id/RIC (0x0052F2FE..0x0052F350) and clears $session_id 0x0052F2D8, $phys 0x0052F358 and $group 0x0052F360",
    "Robot::~Robot 0x005110D4: FreeplayDataTracker::ForceUpdate 0x0051111A, Robot::AbortAll 0x00511120; BehaviourManager +0x44 0x0051112C; BehaviourSystemManager +0x48 0x0051113A; ActionList::Clear 0x00511146 then ~ActionList 0x00511156; VisionComponent +0x258 through vtable slot +4 0x0051116C (D0 0x00652794; its D1 0x00652554 joins the processor thread 0x0065257C and destroys the owned VisionSystem 0x0065258E); MoodManager +0x440 0x005111B6; AIComponent +0x264 0x00511276; PathComponent +0x5c 0x00511284; BlockWorld +0x34 0x005112CE; MapComponent +0x25c 0x005114E8; CarryingComponent +0x284 0x00511406 and DockingComponent +0x280 0x00511414; PoseOriginList +0x294 0x005113CC, TouchSensorComponent +0x28c 0x005113E0, CliffSensorComponent +0x288 0x005113F2; AnimationStreamer +0x60 0x0051154A",
    "the stack's counterpart is CozmoRobot.ResetDevices (device ResetToConstructed) plus the RobotRemoved subscribers: VisionSystem.ResetToConstructed (VisionSystem.cs) and FreeplayStack's NeedsManager::OnRobotDisconnected (FreeplayStack.cs, M15-016); the upper-layer components themselves (behaviour/freeplay, mood, AI, path, map, docking, carrying) are M7/M12-M15 and not built in this stack"
  ],
  "effect": "behaviour, mood, path, docking, map or freeplay state survives a removal the engine would have destroyed",
  "provenance": "extracted 2026-09-30 (B-CORE batch 4; re-analysis/research/20260930-B-CORE-b4-extraction.md)",
  "unresolved": "Opus verification of B-CORE (2026-09-30, re-analysis/research/20260930-B-CORE-verify-3-4.md): NOT YET. Named, not built (upper layers, AbortAll, ForceUpdate, PerfMetric, DAS); no test. Before: B-CORE batch 4: the stack's built counterpart is CozmoRobot.ResetDevices (device ResetToConstructed) plus the RobotRemoved subscribers (VisionSystem.ResetToConstructed, FreeplayStack's NeedsManager::OnRobotDisconnected), matching the destructor's vision and needs parts; the upper-layer components (BehaviourManager/freeplay, MoodManager, AIComponent, PathComponent, MapComponent, Docking/Carrying) are M7/M12-M15 and not built, so their reset is named, not built. Awaiting strong verification.",
  "hardware_required": false,
  "live_path": true,
  "test": ""
}
```

### M1-045

```json
{
  "id": "M1-045",
  "subsystem": "M1-transport",
  "title": "The idle-timeout go-to-sleep action: CreateGoToSleepAnimSequence queued on the ActionList",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "RobotIdleTimeoutComponent::Update 0x0052CE54..0x0052CE6A: ldr.w r5,[robot,#0x250] (the ActionList), blx CreateGoToSleepAnimSequence 0x0052CE5E, then ActionList::QueueAction (0x0053D93C) with position 0 and u8 0 at 0x0052CE6A",
    "CreateGoToSleepAnimSequence 0x0052CEA2..0x0052CFC0: CompoundActionParallel(robot) of a CompoundActionSequential of TriggerAnimationAction(trigger 0xd2, args 1,1,0,60.0f,0; 0x0052CECA..0x0052CEDE), trigger 0xd5 (0x0052CF0E), trigger 0xd4 (0x0052CF44), plus MoveLiftToHeightAction(preset 0, tolerance 5.0f; 0x0052CF8E..0x0052CFA2)",
    "the only caller of the factory is 0x0052CE5E (the caller lists of 0x004A9A30 and 0x0052CEA2; a BL/BLX scan found one call)"
  ],
  "effect": "a faceOff expiry does not put the robot to sleep",
  "provenance": "extracted 2026-09-30 (B-CORE batch 4; re-analysis/research/20260930-B-CORE-b4-extraction.md)",
  "unresolved": "Opus verification of B-CORE (2026-09-30, re-analysis/research/20260930-B-CORE-verify-3-4.md): NOT YET. Named, not built; evidence holds (0x0052CE54..0x0052CE6A, QueueAction position 0; triggers 0xd2/0xd5/0xd4; MoveLift preset 0, tolerance 5.0f). Its tests are M1-031's and never exercise the action. Before: B-CORE batch 4: named, not built - this stack has no ActionList or animation-sequence action layer, so CozmoEngine.QueueGoToSleep logs MISSING and raises GoToSleepRequested instead of queuing the action; the engine's exact action tree is in the evidence. An M5/M8 interface; awaiting strong verification.",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.M1_031_CC3_CC6_TheDisconnectDeadline, EngineAppLayerTests.M1_031_CC6_SleepFiresBeforeDisconnectInOneUpdate, EngineAppLayerTests.M1_031_CC7_AnExpiredDeadlineBlocksReArmingUntilCancel"
}
```



## Item 6 — each original row, in order

| Original row | Verdict | Instructions and corrected build behavior |
| --- | --- | --- |
| Feature constructor (preceding prose) | HOLDS | `0x008E0764..076C` writes zero at Reader+60 and word 1 at +5C: feature bytes `01 00 00 00` (comments, non-strict root, no dropped nulls, no numeric keys). Loader calls this constructor at `0x00677D02`, passes collect-comments=1 at `7D0E..12`. Use Reader, not OurReader defaults. |
| Root/trailing input | HOLDS | `0x008E0886` pushes root; `088C` readValue; `0892` saves success; `0896` skipCommentTokens; `08C4..0912` only optional strict-root check; `0912 mov r0,r7` returns saved result without EOF/error-token check. `truex` and `5garbage` succeed. |
| Whitespace/comments | WRONG | Whitespace mask `0x008E166E..168C` is `0x00800013` relative to byte 9: exactly 09/0A/0D/20. Slash dispatch `1780..178A` calls readComment. C-style termination is checked at `0x008E19E0..1A1C`. But “unterminated comments fail” is false **after a valid root**: parse returns saved readValue result at `0912`, ignoring the subsequent malformed token. `1/*` succeeds. `//` can end at EOF without newline (`0x008E188E..18BA`). Required leading/interior malformed comments fail; suffix errors do not globally invalidate a successful root. |
| Literal grammar | HOLDS | Correct true/false/null match addresses are `0x008E16EA..170C`, `171A..173E`, `1792..17B2`; success `17CC..17D4` advances fixed remainder without delimiter test. Original `17A0..183C` alone omits true/false matching. Prefix behavior holds; container delimiters still apply afterward. |
| Object keys/commas | WRONG | `0x008E0FA6 cmp r5,#2` recognizes `}`; `0FAA..0FB8` reads **previous decoded key length**; `0FBA beq 10F2` succeeds if empty, before key reset at `0FBE..0FC4`. Numeric-key gate `0FE2..0FE8` tests disabled +5F. The exception is any object whose **last decoded key is empty**, not exactly a one-member object: `{"a":1,"":2,}` also succeeds; `{"a":1,}` fails. `{}` uses the same initially-empty-key gate. |
| Arrays | HOLDS | Empty `]` peek `0x008E133E..134C`; otherwise element pointer pushed before readValue at `136A..137C`; after comma `13A8..13AA` repeats readValue; disabled +5E gate at `0x008E0A34..0A3A` rejects missing elements. `[]` succeeds; trailing comma fails. Comment-only empty-array omission below. |
| Number token grammar | HOLDS | `0x008E16CE..16D4` dispatches minus/digit. `0x008E191E..1926` consumes arbitrary digits without leading-zero check; dot `192A..1956`; exponent/sign `1958..19A6`. Conversion decides acceptance. Do not replace with standard JSON number grammar. |
| Integer typing | WRONG | Negative magnitude limit `0x8000000000000000` at `0x008E1D1E..1D24`; positive magnitude limit UINT64_MAX. **Positive intValue cutoff is INT32_MAX**, not INT64_MAX: `0x008E1E50 lsrs r1,r6,#31`; `1E52 orr r1,r1,r0,lsl #1`; `1E56 orr ...r0,lsr #31`; `1E5A cbnz r1,1E94`. Type1 store `1E6E..72`, type2 `1EA6..AA`. Positive 0..2147483647 -> type1; 2147483648..UINT64_MAX -> type2. Negative values through INT64_MIN -> type1. |
| `-0` | HOLDS | `0x008E1E02..1E12` negates magnitude; `1E1E orr r0,r0,#1` stores type1. Magnitude zero remains signed integer zero, not real negative zero or throw. |
| Integer overflow/real typing | HOLDS | Non-digit/limit violation branches to `0x008E1E42..1E4C`, double-conversion veneer. `0x008E22F8` istream double extraction; `2306 tst r0,#5` tests bad/fail state; `2398..23B2` stores double/type3 on success. 18446744073709551616 reaches real conversion. Original `1D70..1DA0` fallback citation is incomplete; decisive exit is `1E42`. Exact underlying decimal rounding/locale is UNKNOWN; see additional row. |
| Strings/control bytes/UTF-8 | HOLDS | Actual token scan `0x008E1752..177A`, duplicate helper `182E..1872`, checks only quote/backslash/end. Decode `0x008E1C32..3C` compares quote/backslash, otherwise `1B6C..70` appends raw byte. Escape dispatch `1B86..1C2C`, Unicode call `1BEC`. Raw controls and invalid UTF-8 survive; Unicode quirks below. |
| Nesting limit | HOLDS | `0x008E09AA ldr r0,[r4,#0x14]`; `09AC cmp #0x3E8`; `09B0 bhi 0D7A`; `0D92` throwRuntimeError. Root pushed at `0886`, children before readValue at `10AC..B2` / `1376..7C`. Active value depth1000 allowed; attempted readValue at depth1001 throws. Empty containers do not push nonexistent children. |
| Firmware input bound | HOLDS | `0x00677C44` loaded-byte gate; `7C50..54` length>>11 rejects file data shorter than 0x800; memchr count0x800 `7CF2..F6`; fallback end=start+0x800 `7D14..18`; parse `7D24`; callback only parse==1 `7D28..34`; failure logs `7D3A..7DAA`. First NUL/0x800 bound applies only after minimum full 0x800 data. Short valid JSON alone is rejected. Function itself does not write expected v/time; callback does. |
| Catch/termination | UNVERIFIABLE | Reopened cleanup beyond old range: `0x00677E46..7E72` destroys root/Reader; `7E78` `_Unwind_Resume`. Proxies clean captures and resume at `0x00678760` / `0x00678942`. `0x00677A4E` terminate is in a different firmware-update routine. No catch-to-false found in these bodies. Final escaping-thread EH/personality route UNKNOWN: do not implement RuntimeError/LogicError as ordinary parse-false/log-and-continue. |

### Reader steps item 6 skipped

| Step | Verdict | Behavior, instructions and owner |
| --- | --- | --- |
| Suffix errors and BOM | HOLDS | Saved result `0x008E0892` returned at `0912` ignores token read at `0896`: `true???` and `1/*` succeed. No BOM strip in parse `082C..0916`; leading UTF-8 BOM reaches tokenizer error `17B4`. Whitespace is only the four listed bytes. M1-029. |
| Bare minus | HOLDS | Scanner synthetic initial digit at `0x008E190C` permits empty digit run after minus. Decode skips minus; pointer==end at `1D3C..3E` reaches `1DFA` with magnitude0; `1E02..22` stores type1 zero. `-` alone parses as integer0. M1-029. |
| Duplicate keys | HOLDS | `0x008E10A2` resolveReference finds existing/new member; `10AC..B2` writes through returned pointer. Repeated decoded keys replace prior values. M1-029. |
| Comment-only empty array | HOLDS | Empty-array peek `0x008E1310..1342` skips whitespace only; `[/*x*/]` enters child readValue, skips comment then rejects `]` through disabled dropped-null gate `0x008E0A34..3A`. `{/*x*/}` succeeds because object loop skips comments at `0F96..0FA4` before empty-key gate. M1-029. |
| Unicode hex and surrogate quirks | HOLDS | Four hex bytes required `0x008E2824..26`; accepts only 0-9/a-f/A-F `286C..2896`. High-surrogate test `262A..2638`; requires next literal backslash-u `266A..2678`, decodes hex at `2686`, **does not validate second code unit is low surrogate**; combines low10 bits `269C..26AA`. Standalone low surrogate succeeds via `2638 ->26AE`. M1-029. |
| Unicode byte output | HOLDS | Magnitude-based UTF-8 encoder `0x008E270C..27FE` lacks surrogate rejection: `\uDC00` -> ED B0 80; `\uD800\u0041` -> U+10041 -> F0 90 81 81; `\u0000` emits zero byte with retained string length. Preserve byte strings, not strict Unicode replacement. M1-029. |
| Exact real conversion | UNVERIFIABLE | `0x008E22C8` constructs current locale; `22F8` calls shipped libc++ istream(double); success tests bad/fail state, not whole-token EOF. Stream num_get, decimal rounding/overflow, and actual process locale need further primary extraction. This is shipped code, **RECOVERABLE_GAP**, not EQUIVALENT_IMPLEMENTATION. Item6 alone cannot authorize bit-exact .NET double parsing. |
| Partial root mutation and recovery | HOLDS | Object/array type/state written before children at `0x008E0F30..44`, `12B4..12C4`; failed parsing can leave partial state. Recovery scans until target delimiter/EOF `0x008E1CC4..1CF0`. Do not assume Reader parse is transactional. M1-029. |

## Item 7.1 — every teardown row

| Original row | Verdict | Instructions and corrected build behavior |
| --- | --- | --- |
| RemoveRobot notification prelude | HOLDS | No RIC at `0x0052F238` -> notify `F29E`; RIC call `F23A..F244` passes result1/2 according to bool parameter; return==1 at `F248..F250` skips broadcast/session clear, still reaches Needs/Perf/DAS `F2DC`. Zero float bits `F2BE..C0`; broadcast `F2CC`; session clear `F2D8`; Needs `F2E0`, Perf `F2E8`, DAS(0) `F2EE`. “Always” only after successful robot lookup. |
| Enter destructor | HOLDS | `0x0052F2F2` loads Robot pointer; null gate `F2F4`; dtor `F2F6`, delete `F2FA`; map erase `F302`, id-vector `F306..F34A`, RIC erase `F350`, globals `F358/F360`. Bookkeeping follows even if Robot pointer null. |
| Flush freeplay data | HOLDS | `0x00511114..11A` gets AI+2C -> ForceUpdate. `0x0056EEB8` tail veneer -> SendData `0x0056EC48`; telemetry sEventF `ED94`, no Robot send in checked body. Accumulator reset `EDBA..BC`, next deadline now+30 seconds at `EDD0..DC`, float bits41F00000. |
| AbortAll order | HOLDS | `0x00511952` type=-1; Cancel `195A`, Path Abort `1960`, Dock Abort `196A`, SendAbortAnimation `1972`, StopAll `197C`; OR path/dock/animation results `1980..88`; dtor caller `0x00511120` ignores return. Failure does not short-circuit later calls. |
| Action cancellation effects | WRONG | Cancel-all queue iteration `0x0053DE20..DE54`. Current gets Cancel `0x0053E6A8` then delete `E6B6`; **pending goes directly to delete without Cancel** `E6D8..E6E0`. Capture gated by external interface `0x0053FA3A..40` and result!=0x03000009 `FA44..4E`; dtor `FA78`; erase `FA86..9C`; broadcast `FAB2`. Current Cancel writes0x02000000 at `0x00540A3C..40`, except preserves0x02000001 (`0x005409E2..EC`). Do not report pending actions uniformly Cancelled; use their existing result and suppression gate. Stop-before-unlock still holds (`0x00541146/116C/1192` precede `122A`). |
| Path abort | WRONG | `0x0064916A` calls ClearPath, whose `0x00649268..6A` zeroes u16 payload; `9270` constructs message; flags1/0 `9278/7A`; Robot send `927C`. Dtor calls Abort again `0x00649002`, so **another ClearPath send attempt** occurs. “No robot message” and “second Abort is only cleanup” are false. `0x00649198..91A4` destroys queued Pose3d objects through vtable; it does not execute completion callbacks (“drains callbacks” is also wrong). |
| Docking/animation/motors wire order | HOLDS | Dock send `0x0063BE36`, animation `0x00517E0A`, StopAll `0x006409C2`, all flags1/0, in this order. Missing preceding ClearPath above. Direct-drive cleanup gated: all +B8/+B9/+BA false OR +D4 nonzero skips straight to send (`0x0063FBE2..FBFE`). Otherwise helper calls masks1,2,4,4,4,4 with speed bits00000000 before unconditional send `FE0E..10`. Do not turn conditional ownership cleanup into unconditional unlock. |
| Behavior managers | UNVERIFIABLE | Calls `0x0051112C` then `113A`; direct bodies `0x005A0D22..0DF6`, `0x005A5886..58D2` contain container/shared/subscription cleanup and no direct Robot/game send. Virtual child call `0x005A58AA` and shared child destructors are not exhaustively resolved. Direct facts hold; recursive silence cannot be a no-op build row. |
| ActionList | HOLDS | Clear `0x00511146`, owner null `1150`, dtor `1156`. Clear reentrant guard `0x0053D91A..1E`; clearing flag1 `D928`; destroy tree `D92C`; zero tree/flag `D930..38`. Cancel-all already deletes current and pending subject to deletion guards, not merely active actions. |
| Vision | HOLDS | Close display windows `0x00652562/2566`; run=false at +4A `2570`; thread-handle gate `2574..78`; unbounded join `257C`; VisionSystem dtor `258E`, free `2592`; deleting wrapper `0x00652794..279E`. Direct no-send claim holds; descendants are not proven silent solely by this body. |
| Mood | WRONG | Unregister has callback-id/robot/ActionList null gates `0x0067AE18..28` before `AE2A`. Robot clears ActionList at `0x00511150` **before** Mood dtor `11B6`, so normal Robot teardown skips unregister. Subscription/history cleanup remains. Do not call an already-destroyed queue. |
| AI/freeplay, BlockWorld, Map | UNVERIFIABLE | Calls `0x00511276`, `12CE`, `14E8`; bodies `0x00569CB2..9DAC`, `0x0061CCFC..CDBA`, `0x0067DC00..DC28` reopened. Direct local container/free behavior holds; AI child vtables `9CC2/9D1C/9D8A`, shared BlockWorld objects and Map unique_ptr<INavMap> descendants are not proven silent. |
| Carrying/docking | HOLDS | Owner nulls/null gates then direct delete `0x00511406/1414` in `0x005113FA..1414`; no intervening destructor. Prior abort is separate. |
| AnimationStreamer | HOLDS | `0x0057AF58` ClearSendBuffer; helper `0x0057B070..B0C2` logs/destroys queued messages, **discards without send**. Song vector `AF88`, TrackLayer dtor `AF94`, live maps `AFC2/AFCC`, shared release `AFD4`. Direct body sends nothing; child lifecycle remains higher-layer obligation. |

### Teardown steps item 7.1 skipped

| Order/step | Exact instructions and behavior | Owning layer |
| --- | --- | --- |
| Robot-id gate before prelude | Map search `0x0052F1BE..F1F0`; missing/empty -> warning `F252` and return `F286..F296`, with none of the notifications/destruction/global clears. | M1-044. |
| Dtor event before ForceUpdate | sEventF `0x005110EE`, robot id loaded `10E2`; ForceUpdate follows. | M1 call order; telemetry effect higher record. |
| Path status and queue cleanup | Planner virtual cancel iff +20 non-null `0x0064914A..56`; shared release `915A..64`; ClearPath send via `916A`; old statuses0/1/4 ->4, 2/3 ->5, >4 unchanged (`9170..88`, mask0x13); clear +46; reverse-destroy Pose3d vector `9198..91A4`; timestamp reset `91AC..B4`. | M13 planner/state, M4 wire ClearPath. |
| Completion/watcher lifecycle | Prep once (+55) calls virtual info setter `0x00540756..07BA`; capture id/type/result/subresults/info `0x00540AD0..0AF4`; stop then unlock iff +56==0 and result!=0x02000001 (`0x0054120C..2A`); ActionWatcher::ActionEnding `1238`, callback-list destruction `125C`; game broadcast later `0x0053FAB2`. | M4 queue/lifecycle, M8 watcher/delegation consumers. |
| Between Vision and Mood | History owner +390 destroyed/nulled `0x0051116E..11AC`; Mood follows. | M10-derived. |
| Between Mood and AI | Unlock/progression +448 `0x005111BE..11EA`; tap +450 `11EA..121A`; filter +44C `121A..124C`; pose confirmer +26C `124C..126A`. | M15 progression; M10 tap/filter; M11 pose confirmation. Descendant effects UNKNOWN where unread. |
| After AI | Path dtor `0x00511284`, second ClearPath; pet world +3C `128C..12A2`; face world +38 `12A2..12C6`; BlockWorld `12CE`; +260 virtual delete `12E4`; idle subscription owner +51C `12F2`; robot-to-engine messaging dtor `1304`; active-object table `1310`; inventory +444 `1314..1338`. | M13 path; M14 faces/pets; M11 BlockWorld; M1/M2 idle/message interface; M15 inventory. +260 is NVStorageComponent: ctor `0x0050FD8C`, owner store `FD90`; internal deletion effects UNKNOWN here. |
| Poses/sensors | Pose members `0x00511392..13BE`; PoseOriginList `13CC`; Touch `13E0`; Cliff `13F2`, then carry/dock. | M11 pose/world; M4 sensors; M12 manipulation. |
| Remaining owners before streamer | +27C raw delete `0x00511424`; +278 virtual delete `1436`; backpack `1444..1466`; cube lights `1474..1488`; TextToSpeech dtor `14B0`; Map `14E8`; Movement virtual delete `1510`; driving-animation subscriptions/vector `1530..1542`; streamer `154A`. | M4 lights/motion; speech M6/M9 pending type/body ownership; M5 driving animations. +278 is CubeAccelComponent (ctor `0x0050FE08`, store `FE0C`); +27C is RobotGyroDriftDetector (ctor `FE1C`, store `FE20`). |
| Duplicate cleanup sites | Earlier owner nulls make later AI/Vision/ActionList/Path/behavior/history checks skip duplicated destruction (`0x005114B8..14FE`, `1512..1520`, `154E..158E`, `15E0..15E6`). Base/member dtor `15F0`, return `15F8`. | M1 orchestration; never unconditionally execute duplicate destruction. |
| Needs transitive effects | System time `0x00695910`; stores+18 `591A`, clears+30/+4; WriteToDevice(1) `592A` unless +1D5; DetectBracketChangeForDas(1) `593C`, SendNeedsLevelsDasEvent `5944`. | M15. Full WriteToDevice payload/path UNKNOWN here; do not call whole prelude purely local. |
| Perf transitive effects | Stop iff +A at `0x0050B738..3E`; tail reset-related veneer `B748`. | New higher app telemetry lifecycle record; Stop/reset descendants UNKNOWN here. |

Corrected **attempt order**: action current/pending deletion effects; ClearPath; AbortDocking; AbortAnimation; gated direct-drive cleanup; StopAllMotors; behavior managers; ActionList; Vision and intervening owners; Mood; AI; Path dtor with another ClearPath; remaining owners; Movement destruction; streamer discard. Attempted SendMessage after disconnect does not establish delivery: connection/filter gates still apply.

## Item 7.2 — every sleep row

| Original row | Verdict | Reopened instructions and build behavior |
| --- | --- | --- |
| Arm deadlines | HOLDS | `0x0052CFE4..CFEE` timer already returns f32 seconds (vmov; no integer conversion); Robot+34E `CFF2..F6`; negative/NaN duration gates `D000..04` / `D038..3E`; f32 add `D00E/D048`; -1 equality `D012..1A/D04C..54`; strict earlier replacement `D01C..26/D056..60`. Preserve BF800000 sentinel, not any negative/zero state. |
| Cancel | HOLDS | `0x0052D06C..D072` constructs BF800000 and strd writes both deadlines; return `D076`. |
| Trigger | HOLDS | Positive/<=now `0x0052CE40..52`; consume face to bits00000000 `CE58`; factory `CE5E`; queue position0/retry0 `CE66..6A`; independent disconnect check `CE72..84`; consume zero `CE8A`; handler `CE8C`; Disconnect tail branch `CE98`. Queue result ignored: failure does not rearm deadline. Both expiry attempts ordered sleep before disconnect in same Update. |
| Sequential child | HOLDS | Sequential ctor `0x0052CEB4`; triggers D2/D5/D4 `CED8/CF0E/CF44`; args loop-count1,bool1,u8=0,float42700000,bool0 at `CECA..DE`, `CF04..14`, `CF3A..4A`; AddAction `CEEC/CF22/CF58` in order, each false/false. Official names `unity/scripts/csharp/Anki.Cozmo/AnimationTrigger.cs:215,217,218`. |
| Parallel root | HOLDS | Parallel ctor `0x0052CF6C`; adds sequential first `CF80`; lift preset0/tolerance40A00000 `CF96..CFA2`; lift inserted second `CFB0`; return root `CFBA`. Both AddAction flags false. Tree construction alone does not settle running/failure propagation. |
| Caller set | UNVERIFIABLE | Observed caller `0x0052CE5E` -> PLT4A9A30 -> factory52CEA2. Independent two-byte Thumb BL/BLX/B.W candidate scan of .text found that sole direct caller. No factory trigger/name parameter. Absolute exclusivity also requires indirect references/ARM callers; their absence is not proven. Retain observed caller, not exhaustive call-graph claim. |

### Sleep steps item 7.2 skipped

| Step | Verdict | Behavior/citation and owner |
| --- | --- | --- |
| Initial state/registration | HOLDS | `0x0052CC66/CC72` both deadlines BF800000; external-interface gate `CC78..7E`; registration wrappers `CC8E/CC94`; subscription owner destroyed `0x005112F2`. M1-031/M2 channel boundary. Exact wrapper tags/dispatch descendants not fully reopened: UNKNOWN until channel extraction accepted. |
| Tick placement | HOLDS | Robot::Update timer `0x00513BE2..BEA`, idle **first** `BF2`, before later component updates; ActionList `0x005140BC`, streamer `0x0051411E`. Idle uses passed now `0x0052CE38 vmov s16,r1`. M1 owns deadline/handoff; execution M4, stream M5. No immediate sleep wire send at factory call. |
| Compound flags | HOLDS | Factory virtual slot+20 is AddAction(bool,bool); adapter `0x0054EC8C cmp r3,#1` installs ignore predicate only when true. Factory passes false/false on all five inserts; adapter forwards empty predicate at `ECE2`. M8 child/failure ownership. Compound Update failure propagation remains UNKNOWN here. |
| Preset0 concrete height | HOLDS | Preset ctor `0x00548B92` GetPresetHeight then concrete-height ctor `BA2`; additional float zero on stack `B98..9A`. Table initialization `0x00548C4E..5E` reads `0x00C54684`: preset0 height42000000 (32.0f), preset1 42980000, preset2 42B80000, preset3 BF800000. Tolerance40A00000 (5.0f). M4 lift record; preset0 is not height0. Init/lock/profile/done/failure still need extraction. |
| Construction exceptions | HOLDS | Allocation-cleanup landings `0x0052CFC2..CFDA` delete failed allocation and `_Unwind_Resume`; no deadline rearm/queue fallback. Higher-layer construction/EH obligation. |

## Proposed exact split

These are record **scopes**, not manifest edits. NEW labels are proposed semantic names, not invented numerical IDs; manager allocates IDs after checking existing ownership/collisions. Scope changes require inventory correction. Existing higher records can absorb a step only if they own its entire production path.

| ID | M1: finish now, narrowed claim | Higher-layer remainder |
| --- | --- | --- |
| M1-031 | **Idle deadline state machine and disconnect trigger**: initial -1; message arming/cancel entry; face-only first-state gate; float32 add/earlier selection; consume face before disconnect; call Disconnect without changing reason. `0x0052CC5A..CC9C`, `0x0052CFDE..D076`, `0x0052CE30..CE9C`, idle-first `0x00513BF2`. Ends at explicit sleep/connection service calls. | M5 factory, M4 queue/lift execution, M8 compound behavior. Remove “puts robot to sleep” from its settled scope. |
| M1-044 | **RemoveRobot lookup, notifications, teardown orchestration and bookkeeping**: missing-id exit; RIC-controlled broadcast/session clear; ordered Needs/Perf/DAS calls; dtor prelude event, ForceUpdate, AbortAll; ordered owner release/destroy calls with null gates; Robot delete; map/vector/RIC erase; phys/group clear. `0x0052F1A4..F360`, `0x005110D4..15F8`, `0x0051194C..198C`. Owns ordering/interfaces, not upper effects. | All descendant state/wire effects below. Narrowed M1 scope may finish independently; broad exact recursive Robot teardown may not. |
| M1-045 | **Face-off handoff: fixed sleep root queued NOW/retry0 before disconnect**, consumes deadline first and ignores queue return, `0x0052CE54..CE6A`. Retain as narrowed boundary ID, or explicitly retire it and merge only this caller contract into M1-031 via inventory correction. | Move tree construction/trigger resolution/lift and compound execution to M5/M4/M8. Do not leave two records claiming complete sleep; MISSING/event-only placeholder cannot prove production queue behavior. |

| Proposed NEW record / layer | Scope and citation | Remaining extraction |
| --- | --- | --- |
| M4-control: action queue teardown/completion | Cancel-all `0x0053DE10..DE5C`; current/pending `0x0053E690..E700`; result `0x005409DC..0A44`; capture/destroy/broadcast `0x0053F9E4..FB02`; stop/unlock/watcher `0x00541084..1286`. | Owned concrete action destructors need their records. |
| M4-control: teardown wire orchestration | ClearPath(0) `0x00649268..927C`; Dock `0x0063BE2A..BE36`; animation abort `0x00517DFE..7E0A`; gated direct-drive releases `0x0063FBE2..FE10`; StopAll `0x006409B6..09C2`; caller `0x0051194C..198C`. All sends flags1/0; no failure short-circuit. | M13 interface supplies repeated dtor ClearPath; connection delivery gates must retain separate ownership. |
| M4-control: sensor/light/movement teardown | Touch/Cliff `0x005113E0/13F2`; backpack/cube `0x00511444..1488`; Movement virtual delete `0x00511510`. | Descendant bodies RECOVERABLE_GAP; guessed ResetToConstructed not recovered behavior. |
| M4-control: sleep lift action | `0x0052CF8E..CFB0`; preset ctor `0x00548B84..BD4`; table `0x00C54684` through `0x00548C4E..5E`. | Execution/profile/locks/done/failure unresolved here. |
| M5-animation: fixed sleep factory | Exact parallel(seq(D2,D5,D4),lift) construction/insertion/args `0x0052CEA2..CFBE`. | Trigger-to-clip selection, stream/wire must have explicit M5 ownership. |
| M5-animation: streamer/driving teardown | `0x00511530..154A`; streamer `0x0057AF48..AFDA`; buffer discard `0x0057B044..B0C2`. | TrackLayer/shared descendants recoverable. |
| M8-framework: compound child/failure behavior | Five false/false inserts; adapter `0x0054EC7C..ED1C`; constructors `0x0052CEB4/CF6C`. | Compound Update/failure propagation not established by tree row. |
| M8-framework: behavior-manager/container teardown | Calls `0x0051112C/113A`; bodies `0x005A0D22..0DF6`, `0x005A5886..58D2`. | Virtual/shared behavior/activity destructors unknown. |
| M7-behaviour: Mood disconnect cleanup | `0x0067AE14..AE74`, guarded unregister; call `0x005111B6` after ActionList null `0x00511150`. | Correct normal teardown skips unregister. |
| M10-derived: history/tap/filter destruction | `0x0051116E..11AC`, `0x005111EA..124C`. | Direct owner/container order checked; descendants must be inventoried. |
| M11-vision: Vision/world teardown | Stop/join/destroy `0x00652554..2718`; owner `0x0051116C`; BlockWorld `0x0061CCFC..CDBA`; Map `0x0067DC00..DC28`; pose/confirmer sites `0x0051124C..126A`, `0x00511392..13CC`. | VisionSystem/polymorphic descendants remain recoverable; unbounded join is exact. |
| M12-manipulation: dock/carry teardown | Abort `0x0063BE10..BE5C`; owner raw frees `0x005113FA..1414`. | Wire boundary connects to M4; raw-free facts exact. |
| M13-navigation: planner/path abort/destruction | `0x00649100..91BA`, ClearPath `0x00649220..929E`, dtor `0x00648FFC..9080`, owner `0x00511284`. | Correct repeated send/status mapping supplied; planner virtual-cancel internals unknown. |
| M14-faces: face/pet-world destruction | `0x0051128C..12C6`. | Direct cleanup checked; shared-object effects not established. |
| M15-freeplay: Needs/AI/freeplay disconnect | Needs `0x00695908..594A`; ForceUpdate `0x0056EEB8`, SendData `0x0056EC48..EDE2`; AI `0x00569CB2..9DAC`; progression/inventory `0x005111BE..11EA`, `0x00511314..1338`. | Needs WriteToDevice/AI virtual children recoverable. |
| M15-freeplay: app telemetry disconnect services | Perf `0x0050B734..B748`; DASPauseUploadingToServer(0) call `0x0052F2EC..EE`; Robot event `0x005110EE`. | Perf/DAS descendants require extraction; retain their M1 ordered interface calls. |
| Speech owning layer: M6/M9 pending body check | TextToSpeechComponent dtor `0x005114B0`, before Map/Movement/streamer. | Call exact; actual body not recovered here. Manager must resolve speech ownership; not transport or assumed harmless free. |
| M3-device: NV teardown; M4-control: cube-acceleration teardown; M10-derived: gyro-drift detector teardown | NV ctor/store `0x0050FD8C/FD90`, deletion `0x005112E4`; CubeAccel ctor/store `0x0050FE08/FE0C`, deletion `0x00511436`; GyroDrift ctor/store `0x0050FE1C/FE20`, raw free `0x00511424`. | Types established from native ctor calls/stores; NV/CubeAccel virtual destructor effects remain RECOVERABLE_GAP. Gyro owner site is direct raw deletion. |

M1 can finish the narrowed state/orchestration claims under the cross-layer rule only with explicit new obligations and tests limited to those boundaries. The full Robot destructor and sleep path remain visibly incomplete until higher records are recovered and built. A test observing GoToSleepRequested alone does not establish construction, queue lifecycle, animations or lift movement.

All requested rows were checked. Three parser rows and three teardown rows are WRONG. Recursive teardown silence, final exception disposition and absolute caller exclusion remain UNVERIFIABLE. No fidelity status changed; no implementation/hardware verification performed. This report supplies corrections and split boundaries, not a PASS for the current broad M1-029/044/045 records or an invented exact decimal/EH implementation.
