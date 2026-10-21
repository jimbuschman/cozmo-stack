# R-M11-TIMEOUT — history admission timeout input

Request: `requests/20261021-m11-history-timeout-input.md`. Examined HEAD da6dd692. Independent extraction, pending manager checking. Coverage opened before tracing.

| Step | Native behavior and instruction citation | Current production file/line | Record | Disposition |
| --- | --- | --- | --- | --- |
| T1 storage | Dynamic symbol `_ZN4Anki4Util18ReliableConnection22sConnectionTimeoutInMSE` is an 8-byte object at0x01051F48, bytes `00 00 00 00 00 88 B3 40` = f64 bits40B3880000000000 =5000.0. One ARM_GLOB_DAT relocation at0x0103EA54 targets it. | `Vision/NativeRobotStateHistory.cs`:33 independent process-static f64 initialized5000.0. | M11-051; M1-005 | Supported literal and native shared identity. |
| T2 Init low word | `movs r5,#0`0x0062EFC0; no subsequent r5 assignment before `strd r5,r3,[r7]`0x0062F070. Calls between those instructions preserve r5 under AAPCS. `movw r3,#8800; movt r3,#40B3`0x0062F062/068. r7 is resolved GOT-loaded global address0x0062F058..066. | Same static initializer; no production assignment from an RCM Init adapter found. | M1-005; M11-051 dependency | Supported **both words**: low00000000/high40B38800. No caller/config numeric input. |
| T3 Configure low word | `movs r3,#0`0x0062F0E0; no r3 reassignment before `strd r3,r1,[r6]`0x0062F172. `movw r1,#8800; movt r1,#40B3`0x0062F162/16C; r6 resolves same GOT at154..16A. Full executable body0x0062F0C8..17C has no call or input load governing this value. | No production counterpart found; no field writer beyond static declaration. | M1-005; M11-051 | Supported same5000.0. Configure has no numeric argument or JSON/config read. |
| T4 callers / config | Init's resolved direct/PLT caller found at0x0069D3D6 in MessageHandler::Init(Json::Value const&,RobotManager*,CozmoContext const*), after new RCM/owner replacement0x0069D3BA..D4. Init0x0062EFB0..F084 unconditionally stores the constant. Configure has no direct/PLT BL/BLX/B.W caller found; no address-taking word/relocation found beyond its dynamic symbol value. | CozmoRobot261 constructs ReliableTransport(options); ReliableTransport302 defaults to TransportOptions.EngineDefaults; optional host ConnectionTimeoutMs is separate. | M1-005 | Supported bounded caller proof. The enclosing Init has JSON inputs, but **this timeout has none**. |
| T5 pointer materializations | PC-relative GOT materializations: history0x00530D3A/40; Init0x0062F058/5C; Configure0x0062F154/15C; transport0x00836076/78 and0x00836152/15C. Individually traced uses below. | Source-wide exact-name search: history declaration33/load77 only; tests save/set5000/restore. | M11-051 | Supported bounded five-use census, two stores/three loads; no escape in these uses. General computed-address absence remains UNKNOWN within stated limits. |
| T6 history read | `ldr r0,[r0]`0x00530D46 then `vldr d0,[r0]`D48 loads f64 global. Existing checked negative/clamp/rounding body converts5000.0 to u32 5000; `subs r0,r6,r7; cmp r0,sb; bls`0x00530DB2..B6 admits gap equality. | NativeRobotStateHistory77–82 loads ConnectionTimeoutInMs, clamps/adds0.5/floors, compares u32 delta. | M11-051 | Supported actual numeric input at native default. No need to re-extract the admission body. |
| T7 transport loads / alias | First GOT address loaded into r5 at0x0083607A; used by `vldr d1,[r5]`0x00836088 for lastRecv+timeout. r5 overwritten at0x008360D6 with scalar call result, never passed/stored as pointer. Second address r1 at0x00836160; `vldr d1,[r1]`168 then overwrite r1 at16C; only **value** goes to timeout warning stack0x008361DE. Full executable HasConnectionTimedOut0x00836068..6248 read. | ReliableConnection311 compares `_clock.NowMs > LatestRecvMs + _o.ConnectionTimeoutMs`; separate options object. | M1-015/032; M11-051 dependency | Supported loads only; no global-address escape through either reader. |

## Result

The supported shipped path supplies **exactly f64 5000.0 ms**, both before and after RCM Init. ConfigureReliableTransport writes the identical full value and has no identified caller. No supported config field or caller parameter changes the value. This closes the previous specifically untraced low words. It also supplies a bounded address-use proof, rather than just repeating the old “same high word” assertion.

This is primary extraction, pending independent checking/adoption. No production, tests, manifest, inventory, state or approval was changed; no tests/build/hardware ran. The history admission algorithm is reused from adopted OV128, not broadly re-extracted.

## Native evidence and writer boundaries

**Storage.** ELF bytes at0x01051F48 are not a decimal approximation: little-endian words00000000/40B38800 encode5000.0 exactly. The only dynamic symbol whose value lies in this eight-byte object is the named ReliableConnection static. Its only symbol relocation is ARM_GLOB_DAT at0x0103EA54. No ARM_RELATIVE relocation with stored addend in [1051F48,1051F50) was found. A whole-file raw-word search for1051F48 found only the dynamic symbol's own value at file offsetC088;1051F4C had no match;103EA54 occurred at relocation-entry file offset475D48. These are metadata, not evidence of a code-stored alias.

**Init.** Entire executable body0x0062EFB0..0x0062F084 was read, separating its literal pool beginningF088. r5 is set0 atEFC0; r3 is built40B38800 atF062/068; STRD writes low then high atF070. Earlier r5 stores do not change r5. Intervening calls are ordinary ABI calls and do not supply the value. There is no conditional timeout branch. The final call into the transport object uses r0 as that object and r1 as the function address, not the timeout global pointer. The r7 timeout address is not published to the receiver.

**Configure.** Full executable bodyF0C8..F17C was read, separating its literal poolF180 onward. r3=0 is establishedF0E0 and survives; high word r1=40B38800 is establishedF162/16C; STRD atF172 writes5000.0. This method has no numeric/config argument and makes no call in that executable body. It returns without storing or returning the global pointer. Configure's Thumb address62F0C9 occurs only as its dynamic symbol value (file29EA8); no separate code/data pointer word was found. The accepted M1-005 record already says Configure is dead code; this pass independently found no direct/PLT caller of it.

**Readers.** The history reader loads the address, loads the double and then reuses its argument registers for scalar clamp/rounding work. The transport predicate's two address uses load double values only; they do not pass or retain the address. HasConnectionTimedOut's warning formats the value, not a mutable reference. Its strict transport comparison and history's u32 gap comparison are different consumers of the same native symbol. Their clocks/algorithms are not equated by this report.

**Caller/config closure.** A whole-.text direct branch scan with resolved PLT targets found one Init call, MessageHandler::Init0x0069D3D6; Configure target set contains only62F0C8 and no caller. Init target set is62EFB0 plus PLT4BE10C. The caller constructs/replaces RCM then passes this in r0. Although the enclosing MessageHandler::Init accepts JSON, the complete RCM Init writer ignores any such config for this static value. No shipped app C#/Java/smali mention of sConnectionTimeoutInMS or ConfigureReliableTransport was found. Dynamic-symbol scans of every other shipped armeabi-v7a .so found no import/reference to the timeout symbol. There is no supported configurable timeout input recovered from these paths.

## Census method and explicit limits

Read the existing M1 inventory B13/tunable table and current M1-005/015/032 records, adopted OV128's timeout qualification and preceding pose/history F1 lead. Fresh .so work used local LIEF/Capstone, actual instructions/literals and relocations; decompilation was not evidence. A Thumb `.text` scan (section4D6860, size60CE22), with skip-data enabled, resolved LDR-PC followed by ADD-PC materializations of the GOT/global address. All five matches listedT5 were individually traced. A separate direct/PLT call scan and raw address/relocation/dynamic-symbol searches cross-check the result.

The scan is **not a proof for arbitrary pointer arithmetic**: computed-offset access through a separately materialized segment base, unindexed/ARM-mode fragments, dynamically resolved external mutation, or an address assembled by a different instruction idiom are outside this pattern census. No concrete such alias or supported caller was found. Their universal absence remains **UNKNOWN**, not a claim that “five pattern matches” proves every byte store in the binary. No new broad transport audit was performed. The resolved five uses themselves have no escaped pointer, and the currently supported named input path is fixed by full writer bodies. An independent checker can adopt that bounded result; it must not silently turn it into a mathematical whole-binary no-alias proof.

## Current production comparison

`Vision/NativeRobotStateHistory.cs`:33 declares process-static mutable ConnectionTimeoutInMs=5000.0; :77 reads it. Source-wide search found no other production write. RobotStateHistoryAdmissionTests saves it at:18, assigns5000 at:20 and restores it at:30. Thus its current mutability is a **test-accessible field**, not recovered native config wiring. At the normal default it supplies the sourced numeric value; it is not currently updated by transport construction or RCM configuration. An internal difference in initialization mechanism is permitted when the same value/results are delivered; another runtime setter is not required merely to imitate the native static.

The separate public TransportOptions.ConnectionTimeoutMs (`Cozmo.Transport/TransportConstants.cs`:52) defaults5000.0. CozmoRobot261 passes supplied options to ReliableTransport; its constructor302 retains options or EngineDefaults; ReliableConnection311 uses that instance option. Changing it does not change history's static field. The native symbol is shared, but the native app Init always installs5000.0 and exposes no supported timeout config in this extraction. Therefore a custom host transport timeout has **no sourced native counterpart** here. Do not automatically couple that host override into history and call it recovered behavior. Any supported host override policy belongs with its transport owner; no such policy is created by this report.

## Record impact and regression design

M11-051's current unresolved “low word untraced” can be replaced after checking this report with both exact stores and the bounded no-escape use census. Its stale “global double unread” and old no-admission implementation notes should be reconciled with the later built code. This answers the input lead; it does not settle all raw history gates/indices/pose dependencies. M1-005 already claims the Init tunable5000 and dead Configure; no contradiction found. M1-015/032 retain their established timeout/report path claims. No new policy, exclusion or status change is requested.

Approval: integrating this newly checked input evidence into a frozen inventory/evidence field must follow that layer's normal manager/operator approval process. This research publication itself needs no new approval and changes no frozen record.

Suggested independent regressions for a separate builder, without adding tests here:

1. Decode/assert literal bits40B3880000000000 and the two native low/high word pairs. Do not derive expected5000 from TransportOptions or the history field under test.
2. Real received-state history with a newest timestamp chosen so both5000/5001 deltas pass the separate stale gate: delta5000 admitted,5001 rejected/increments the existing static gap run. Preserve the checked sixth-gap raw-only clear and empty-map skip. The packet timestamp and engine transport clock are separate inputs.
3. Exercise fresh EngineRobot/RCM lifecycle more than once: source default remains5000; no supplied app JSON changes this value. A mutable test setter must be restored so tests cannot imply production configurability.
4. Existing M1 transport regression remains strict `now > lastRecv+5000`, including equality not timing out; do not replace history's unsigned gap rule with that predicate.
5. If custom host TransportOptions timeout remains supported, test/document its actual policy separately. No test should assume native Configure reads that option or that history changes with it without explicit source/policy ownership.

## Current record snapshot

Quoted from the manifest at examined HEAD; no changes.

### M1-005

```json
{
  "title": "Reliable-transport tunables as RobotConnectionManager::Init sets them",
  "status": "EXACT_SOURCE",
  "evidence": [
    "B13 RobotConnectionManager::Init 0x0062EFCE..0x0062F076 sets: MaxPacketsToReSendOnAck 0, MaxPacketsToSendOnSendMessage 1, SendUnreliableMessagesImmediately 0, SendPacketsImmediately 0, SendAckOnReceipt 0, MaxPacketsToReSendOnUpdate 1, TrackAckLatency 1, SendSeparatePingMessages 0, MaxPingRoundTripsToTrack 10 (0x0062F074 movs r3,#0xa), TimeBetweenPingsInMS 33.3 (0x0062F028, 0x4040A666_66666666), MaxTimeSinceLastSend 32.3 (0x0062F03E, 0x40402666_66666666), TimeBetweenResendsInMS 33.3 (0x0062F04E), PacketSeparationIntervalInMS 2.0 (0x0062F056), ConnectionTimeoutInMS 5000.0 (0x0062F068 movt #0x40b3)",
    "ConfigureReliableTransport 0x0062F0C8 writes the identical set but has no BL/BLX/B.W caller in .text (manager scan): the previous evidence cited dead code",
    "unset by Init, so the .data defaults stand: MinExpectedPacketAckTimeMS 1.0 (read 0x0083641C), MaxAckRoundTripsToTrack 100, MaxBytesFreeInAFullPacket 0 (read 0x0083634E)",
    "GetCurrentNetTimeStamp 0x008355F8: double milliseconds from steady_clock (ns / 0x3E8 at 0x00835644, * 0.001 at 0x0083564C)"
  ]
}
```

### M1-015

```json
{
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
  ]
}
```

### M1-032

```json
{
  "title": "A never-answered connection times out 5000 ms after it is created",
  "status": "EXACT_SOURCE",
  "evidence": [
    "G2.1/G2.2 the connection is created by the first type-1 SendMessage (0x00836C5A..0x00836C6E; FindConnection 0x00837098..0x008370C0)",
    "G2.3 the ReliableConnection ctor sets +0x50 = GetCurrentNetTimeStamp() (0x0083592E blx; 0x0083593C vstr d0,[r4,#0x50])",
    "G2.5/G2.6 the only other store to +0x50 is UpdateLastAckedMessage (0x00835B9C), whose only caller is ReceiveData for a valid frame from a known connection (0x00837818)",
    "G2.7/G2.8 timed out when now > +0x50 + 5000.0, strictly (0x00836080..0x0083623E); every ReliableConnection::Update checks it (0x008365A4)",
    "G2.9 detection runs on the 2 ms tick in async mode (M1-020, M1-021); in async mode the 5 s start when the queued connect closure runs SendMessage, not at the Connect() call"
  ]
}
```

### M11-051

```json
{
  "title": "RobotStateHistory::AddRawOdomState gates (drop, consecutive-gap reset, parent check, duplicate key)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00530CF8..0x00530EC4 AddRawOdomState",
    "0x005309D1 CullToWindowSize",
    "re-analysis/research/20260929-R-VIS-verify-M11-batchA4.md"
  ]
}
```
