# M1 verification packet — 2026-10-09

Prepared for the operator’s request in this chat. Source snapshot `75bb3df7a3c43cba882dd831b3a2a8c26d61d0eb` on main. No new verdicts. Explicit operator authorization to commit/push overrides the ordinary research-lane no-commit rule.

## Coverage

53 current M1 records; 48 included, 5 excluded. Built records and policy/equivalence records with no explicit open MISSING are included. Current non-MISSING verification uncertainty is retained verbatim; inclusion does not mean complete fidelity. Hardware-only, unbuilt recoverable records and records awaiting final row approval are excluded. M1-053 detailed payload/supplier rows and M1-046/-047 final extraction are supplied separately for manager check; their builds are not claimed here.

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

| M1-014 | [M1-014.md](M1-014.md) | 25 | 12 |

| M1-015 | [M1-015.md](M1-015.md) | 151 | 155 |

| M1-016 | [M1-016.md](M1-016.md) | 3 | 7 |

| M1-017 | [M1-017.md](M1-017.md) | 3 | 2 |

| M1-018 | [M1-018.md](M1-018.md) | 8 | 19 |

| M1-019 | [M1-019.md](M1-019.md) | 14 | 61 |

| M1-020 | [M1-020.md](M1-020.md) | 4 | 10 |

| M1-021 | [M1-021.md](M1-021.md) | 13 | 60 |

| M1-022 | [M1-022.md](M1-022.md) | 45 | 79 |

| M1-023 | [M1-023.md](M1-023.md) | 10 | 44 |

| M1-024 | [M1-024.md](M1-024.md) | 17 | 58 |

| M1-025 | [M1-025.md](M1-025.md) | 51 | 119 |

| M1-026 | [M1-026.md](M1-026.md) | 9 | 25 |

| M1-027 | [M1-027.md](M1-027.md) | 14 | 23 |

| M1-028 | [M1-028.md](M1-028.md) | 159 | 192 |

| M1-030 | [M1-030.md](M1-030.md) | 13 | 25 |

| M1-031 | [M1-031.md](M1-031.md) | 138 | 135 |

| M1-032 | [M1-032.md](M1-032.md) | 11 | 42 |

| M1-034 | [M1-034.md](M1-034.md) | 25 | 97 |

| M1-035 | [M1-035.md](M1-035.md) | 6 | 21 |

| M1-036 | [M1-036.md](M1-036.md) | 3 | 5 |

| M1-037 | [M1-037.md](M1-037.md) | 2 | 0 |

| M1-038 | [M1-038.md](M1-038.md) | 25 | 29 |

| M1-039 | [M1-039.md](M1-039.md) | 25 | 12 |

| M1-040 | [M1-040.md](M1-040.md) | 24 | 13 |

| M1-041 | [M1-041.md](M1-041.md) | 14 | 38 |

| M1-042 | [M1-042.md](M1-042.md) | 26 | 13 |

| M1-043 | [M1-043.md](M1-043.md) | 3 | 5 |

| M1-044 | [M1-044.md](M1-044.md) | 125 | 123 |

| M1-045 | [M1-045.md](M1-045.md) | 125 | 116 |

| M1-048 | [M1-048.md](M1-048.md) | 10 | 32 |

| M1-049 | [M1-049.md](M1-049.md) | 14 | 20 |

| M1-050 | [M1-050.md](M1-050.md) | 1 | 3 |

| M1-051 | [M1-051.md](M1-051.md) | 3 | 11 |

| M1-052 | [M1-052.md](M1-052.md) | 2 | 7 |

## Excluded records and reasons

Each current excluded record is reproduced in full below so exclusions do not depend on an older report.

### M1-029

Open MISSING in current unresolved: built, awaiting strong verification: J1 finite-real Json::Value::asString now enters the live asString accessor and ports the checked wrapper: precision-17 general format under the approved phone snprintf assumption, append .0 only when neither dot nor lowercase e exists, then comma-to-period rewrite (0x008E48B8..0x008E4902; 0x008EBFAA..0x008EC04C). J7 retains the checked u16 byte count and exercises the maximum reachable 65535-byte Reader input; no fixed token cap was found in the checked Reader/converter slices (0x007B9088..0x007B9092; 0x008E078C..0x008E07F8; 0x008E190A..0x008E19A6; 0x008E2258..0x008E23D6; 0x0007E824..0x0007F202). MISSING: non-finite asString mapping in the useSpecialFloats=0 branch; the formatter body remains the J2 phone-runtime assumption. Also MISSING: allocation failure/extreme-length effects and the final escaping exception destination under M1-034. No settlement; J1/J7 checked subset only.

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
    "G5.38..G5.40 no writer of RobotManager+0x84/+0x88 besides the ctor and ParseFirmwareHeader was found; the scan cannot prove absence (adjusted-base, register-offset, whole-object and untyped accesses are outside it); see decision D7",
    "J1 retained real asString dispatch 0x008E48B8..0x008E4902 and formatting wrapper 0x008EBF7C..0x008EC038; J7 reachable input bound and Reader/converter 0x008E07A4..0x008E1ECA, 0x0007E824..0x0007F202 (20261006-M1M2-missing-triage.md J1/J7).",
    "J1: Json::Value::asString dispatches real type 3 with precision 17/useSpecialFloats=0 (0x008E48B8..0x008E4902); formatter wrapper uses imported snprintf, capacity 0x24, adds .0 after checking dot/lowercase e, then rewrites commas (0x008EBFAA..0x008EC04C). J7: FirmwareVersion JSON length is a u16 (0x007B9088..0x007B9092; 0x0071A908..0x0071A914); bounded Reader number scan and decodeDouble path have no fixed token-size cap in cited routines (0x008E078C..0x008E07F8; 0x008E190A..0x008E19A6; 0x008E1E42..0x008E1E4C; 0x008E2258..0x008E23D6; libc++ converter 0x0007E824..0x0007F202). Final exceptions/allocation-failure effects remain UNKNOWN."
  ],
  "effect": "a robot is accepted or refused as outdated where the app decides otherwise",
  "provenance": "the M1 candidate implementation (batch 3) compared against re-analysis/research/20260929-R-DEV-pre-extraction.md part 3 item 11 (rows 11a..11r) and re-analysis/inventory/M1-transport.md",
  "unresolved": "built, awaiting strong verification: J1 finite-real Json::Value::asString now enters the live asString accessor and ports the checked wrapper: precision-17 general format under the approved phone snprintf assumption, append .0 only when neither dot nor lowercase e exists, then comma-to-period rewrite (0x008E48B8..0x008E4902; 0x008EBFAA..0x008EC04C). J7 retains the checked u16 byte count and exercises the maximum reachable 65535-byte Reader input; no fixed token cap was found in the checked Reader/converter slices (0x007B9088..0x007B9092; 0x008E078C..0x008E07F8; 0x008E190A..0x008E19A6; 0x008E2258..0x008E23D6; 0x0007E824..0x0007F202). MISSING: non-finite asString mapping in the useSpecialFloats=0 branch; the formatter body remains the J2 phone-runtime assumption. Also MISSING: allocation failure/extreme-length effects and the final escaping exception destination under M1-034. No settlement; J1/J7 checked subset only.",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.M1_029_G5_10_ASimulatorIsAcceptedWithoutRobotAvailable, EngineAppLayerTests.M1_029_G5_11_G5_12_M1_040_TheVersionOutcomeIsComputedAndThenNotApplied, EngineAppLayerTests.M1_029_G5_14_TheExpectedValuesAreCopiedWhenTheRobotIsAdded, EngineAppLayerTests.M1_029_G5_19_G5_31_AShortOrMissingFileGivesNoHeader, EngineAppLayerTests.M1_029_G5_21_TheShippedHeaderParsesTo2381, EngineAppLayerTests.M1_029_G5_2_G5_4_CB13_FactoryAndUnparsableFirmwareProceedUnderM1_040, EngineAppLayerTests.M1_029_G5_31_WithNoHeaderTheExpectedValuesAre0AndTheRobotIsStillAccepted, EngineAppLayerTests.M1_029_11d_11e_11i_CommentsAnyRootAndTrailingText, EngineAppLayerTests.M1_029_11f_11g_TrailingCommasAreAnError, EngineAppLayerTests.M1_029_11n_AsUIntOfANonNumber, EngineAppLayerTests.M1_029_11q_ANonNumberVersionThrows, EngineAppLayerTests.M1_029_CheckedReaderBranches, EngineAppLayerTests.M1_029_CheckedIntegerTyping, EngineAppLayerTests.M1_029_CheckedUnicodeBytes, EngineAppLayerTests.M1_029_RawBytesAndDuplicateReplacement, EngineAppLayerTests.M1_029_RootCountsTowardsDepthLimit, EngineAppLayerTests.M1_029_CheckedReaderDrivesFirmwareHandshake, EngineAppLayerTests.M1_029_RealConversionUsesShippedBits, FirmwareJsonDoubleTests.ShippedEmulatorCorpusMatchesBitsAndFailureGates, FirmwareJsonDoubleTests.FirmwareReaderUsesShippedConversion, FirmwareJsonDoubleTests.FirmwareReaderRejectsConverterFailure, FirmwareJsonDoubleTests.FirmwareHeaderProductionEntryConvertsDecimalVersion, EngineAppLayerTests.M1_029_J1_FiniteRealAsStringUsesTheCheckedWrapper, EngineAppLayerTests.M1_029_J7_ReaderAcceptsTheMaximumReachableU16Length"
}
```

### M1-033

HARDWARE_ONLY; no completed built record to package. Settled by the operator's robot run (2026-10-09, firmware 2457, bundle re-analysis/acceptance/hardware/20261009-112625-M1-PACKED-ZERO): the robot accepts packed frames of types 7 (two reliable IMURequests: ACK 3), 8 (two unreliable pings: both timestamps echoed) and 9 (mixed: ACK 3 and the echo), each with a working single-frame control. Observed for these small frames on this firmware; maximum-size frames, other orders and sequence wraparound were not exercised. Before: whether the robot accepts packed frames of types 7, 8 and 9 from the engine (the link check sent none)

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
  "unresolved": "Settled by the operator's robot run (2026-10-09, firmware 2457, bundle re-analysis/acceptance/hardware/20261009-112625-M1-PACKED-ZERO): the robot accepts packed frames of types 7 (two reliable IMURequests: ACK 3), 8 (two unreliable pings: both timestamps echoed) and 9 (mixed: ACK 3 and the echo), each with a working single-frame control. Observed for these small frames on this firmware; maximum-size frames, other orders and sequence wraparound were not exercised. Before: whether the robot accepts packed frames of types 7, 8 and 9 from the engine (the link check sent none)",
  "hardware_required": true,
  "live_path": true,
  "test": null,
  "verification": {
    "level": "HARDWARE_VERIFIED",
    "bundles": [
      "re-analysis/acceptance/hardware/20261009-112625-M1-PACKED-ZERO"
    ]
  }
}
```

### M1-046

RECOVERABLE_GAP; no completed built record to package. RECOVERABLE_GAP: build the confirmed owner/member retirement order for RobotIdleTimeoutComponent (+0x51C) and RobotToEngineImplMessaging (+0x518), including its member/shared-handle vector order. The concrete ScopedHandleContainer virtual +8 unsubscribe target and any behavior-changing descendants remain UNKNOWN; do not invent their effects. Source: 20261006-M1M2-missing-triage.md Q1-Q3.

```json
{
  "id": "M1-046",
  "subsystem": "M1-transport",
  "title": "Channel/member subscription retirement recipients",
  "location": "cozmo-stack/src/Cozmo.Robot/RobotLifetime.cs",
  "status": "RECOVERABLE_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "Robot constructs RobotIdleTimeoutComponent at +0x51C and RobotToEngineImplMessaging at +0x518 (0x0051020A..0x0051022E). Robot dtor clears +0x51C, releases its shared-handle vector, then clears +0x518 and calls RobotToEngineImplMessaging::~ (0x005112E6..0x00511308).",
    "RobotToEngineImplMessaging dtor releases members +0x144/+0x138/+0x110/+0x104, handle vector +0xF8, filebuf/ios, then its base (0x00532A10..0x00532A74). Last shared-handle release invokes virtual slot +8 then deleting-dtor slot +4 (0x004EF184..0x004EF1CC); concrete target and behavior-changing descendants UNKNOWN. (MISSING triage Q1-Q3.)"
  ],
  "effect": "A removed Robot or queued sleep action retains state, emits different messages or invokes callbacks in a different order.",
  "provenance": "Cited cross-layer inventory correction authorized by B-M1M2 Rows checked (manager, 2026-10-05). Interfaces/known effects only; no higher-layer build or settlement.",
  "unresolved": "RECOVERABLE_GAP: build the confirmed owner/member retirement order for RobotIdleTimeoutComponent (+0x51C) and RobotToEngineImplMessaging (+0x518), including its member/shared-handle vector order. The concrete ScopedHandleContainer virtual +8 unsubscribe target and any behavior-changing descendants remain UNKNOWN; do not invent their effects. Source: 20261006-M1M2-missing-triage.md Q1-Q3.",
  "hardware_required": false,
  "live_path": true,
  "test": ""
}
```

### M1-047

Waiting on manager check of final extraction rows; not built.

```json
{
  "id": "M1-047",
  "subsystem": "M1-transport",
  "title": "Transport executor topology and shipped priority request",
  "location": "cozmo-stack/src/Cozmo.Transport/ReliableTransport.cs",
  "status": "IMPLEMENTATION_GAP",
  "effect": "Wrong executor thread count/order or omitted priority changes message timing.",
  "provenance": "C# uses host executor threads and currently does not request native SCHED_RR priority.",
  "authority": "libcozmoEngine.so 3.4.0-1204 executor constructors/priority helper.",
  "evidence": [
    "H1 creates two threads and calls SetThreadPriority on both only for priority !=2 (0x007FBDBA,0x007FBDEC,0x007FBE02..0x007FBE14).",
    "H2 priority3 selects policy2/SCHED_RR and requests min+trunc_f32((max-min)*0.75), bits0x3F400000; EPERM/result1 skips error path (0x008334CC..0x00833532).",
    "H3 FIFO dispatch stays M1-024; T3 M1-021; T4 M1-024."
  ],
  "unresolved": "Compare/build exact thread count/work split and priority gates. Actual host limits/permissions/effect remain M1-014 host policy; dispatch/timing stay M1-010/-021/-024.",
  "hardware_required": false,
  "live_path": true,
  "test": "TransportRepairTests executor topology/priority source-derived cases to add."
}
```

### M1-053

Waiting on manager check of final extraction rows; not built.

```json
{
  "id": "M1-053",
  "subsystem": "M1-transport",
  "title": "Outgoing RobotState publication from the engine tick",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs",
  "status": "IMPLEMENTATION_GAP",
  "effect": "Game can receive RobotState at the wrong tick, before its source gate or from an unproven caller.",
  "provenance": "UpdateAllRobots candidate exists; bounded caller known, exhaustive callers/sink not closed.",
  "authority": "libcozmoEngine.so 3.4.0-1204 UpdateAllRobots/RobotState getter.",
  "evidence": [
    "UpdateAllRobots enters from engine tick 0x004ED648; iterates robots, calls Robot::Update then HasReceivedRobotState, and only when true calls GetRobotState, constructs MessageEngineToGame::RobotState and invokes external slot +0x1C (0x0052F6C0..0x0052F7B0; getter 0x005180D8..0x00518252). External sink remains UNKNOWN.",
    "A second caller exists: BehaviorDockingTestSimple::UpdateInternal calls GetRobotState at 0x005CC31E, copies state to +0x270 and enters state 3 (0x005CC310..0x005CC32C); this non-live developer behavior belongs to M7-022. Remaining callers are not proven exhaustive. Source: 20261006-M1M2-missing-triage.md P2; reopened libcozmoEngine.so."
  ],
  "unresolved": "App-boundary scope rule (operator, 2026-10-09; AGENTS.md \"Scope: the app boundary\"): the engine's UpdateAllRobots gate, projection and publication call stay exact; the game sink is the stack's C# API (a RobotState API event), which replaces the Unity consumer. Buildable now. Before: RECOVERABLE_GAP: build only the checked UpdateAllRobots publication gate/order/projection. The external-interface +0x1C sink and any additional callers remain UNKNOWN; M7-022 owns the non-live BehaviorDockingTestSimple state copy.",
  "hardware_required": false,
  "live_path": true,
  "test": "EngineAppLayerTests.M1_024_CD7_CD8_GameMessagesRunBeforeTheClockUpdateAndRobotUpdateAfterIt plus projection cases after extraction."
}
```

## Reproduction and artifact boundaries

`python re-analysis/research/20261008-M1M2-verify-packet/build_packet.py` packages the checked-out snapshot after the offline test TRX exists. It only writes in this directory. The native ELF is read locally and is not uploaded. Per-record files are self-contained for manifest, rows, diff text, native transcripts, and relevant test/result text. Complete shared-file histories intentionally contain unrelated hunks; they are explicitly labeled as supersets rather than assigned invented record-level causal ownership.

Test command: `dotnet test cozmo-stack/Cozmo.sln --logger trx --results-directory .scratch/m1-api-events`. The supplied TRX records the actual result; existing oracle blocked imports remain blocked.

## Rows awaiting manager check

M1-046/-047: [final extraction rows](../20261009-M1-final-extraction.md) and [native instructions](../20261009-M1-final-extraction-native.txt). M1-053: [detailed projection rows and supplier boundaries](../20261009-M1-053-projection-rows.md) and [native instructions](../20261009-M1-053-projection-native.txt). These are unchecked extraction reports and are not built records in this packet.

## Offline run and packet validation

Offline run: 3960 passed, 0 failed, 0 not executed. The full solution suite is recorded without result substitution.

`validate_packet.py` checks the included/excluded partition, verbatim manifest values and inventory lines, exact Git patch histories, native transcript bytes against the local ELF, citation byte coverage, and declared-test result matching. Machine-readable results are in `validation.json`. These are packaging checks, not an Opus/source-fidelity verdict.
