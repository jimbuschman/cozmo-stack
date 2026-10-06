# M1 disconnect teardown: bounded source check and implementation handoff

| Coverage | Result |
| --- | --- |
| M1-015 / M1-025 / M1-044 | Current manifest read; RemoveRobot and Robot destructor instructions checked; selected nested bodies checked |
| AbortAll | Call order, arguments and return aggregation checked; two message-building bodies checked |
| ActionList Clear / Mood destructor | Reentrancy gate and robot-pointer null gate checked |
| BehaviorManager / AnimationStreamer destructors | Direct bodies checked; nested object destruction remains explicitly open |
| Full reconnect production lifecycle | NOT fully recovered or verified by this report |
| Production edits, status changes, hardware runs | None |

Answers the operator's 2026-10-05 request: productive independent research on remaining M1 blockers while M1-029 is being investigated elsewhere. This is a bounded addendum to 20260930-B-CORE-b4-extraction.md, not an implementation inventory approval. Research only; manager must check citations before integration.

## Primary source

Original engine: extracted APK resources/lib/armeabi-v7a/libcozmoEngine.so, SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. ARM Thumb instructions read with Capstone from ELF load segments. Direct PLT names resolved using ELF .plt and relocation order. Evidence transcript: 20261005-m1-teardown-instructions.txt. Ghidra used for navigation only.

## Current claims (not an older audit's recollection)

All four are IMPLEMENTATION_GAP in the current manifest:

- M1-015: "Connection timeout 5 s, and how a lost or failed connection is reported". Evidence includes RemoveRobot, destructor and connection-controller ranges. Current unresolved explicitly defers to unbuilt M1-044.
- M1-025: "Connect request from the game, the connected response, and DisconnectCurrent". Current unresolved names M1-044 AND unbuilt ExitSdkMode E7..E9. Closing teardown alone does not close this record.
- M1-044: "RemoveRobot's upper-layer teardown: Robot::~Robot aborts all actions and destroys the behaviour, mood, AI/freeplay, path, map, docking, carrying and vision components". Current evidence includes 0x005110D4..0x005115F8 and RemoveRobot 0x0052F238..0x0052F364. Current unresolved: named, not built; upper layers, AbortAll, ForceUpdate, PerfMetric, DAS; no test.
- M7-020: "Mood event production, repetition penalty and affector application". Current unresolved names absent ActionList/ActionWatcher/RobotCompletedAction; relevant callback ownership must be preserved in teardown too.

## Checked steps

RECOVERED here means the stated local instruction fact only. It does not classify a whole production record EXACT_SOURCE.

| Step | Original behavior | Primary instructions | Record / coverage |
| --- | --- | --- | --- |
| T01 | When RIC HandleDisconnect returns exactly 1, skip the disconnected broadcast branch, but still notify Needs and execute teardown | 0x52F248 cmp r0,#1; 0x52F24A bne 0x52F29E; 0x52F250 b 0x52F2DC | M1-015; branch recovered; HandleDisconnect internal body not rechecked |
| T02 | Needs notification, PerfMetric notification, DASPauseUploadingToServer(0), Robot destruction, map erase, id/RIC erase, global clears occur in that order | calls 0x52F2E0, 0x52F2E8, 0x52F2EE, 0x52F2F6, 0x52F302, 0x52F350, 0x52F358/360 | M1-044; call order recovered; nested notifications UNKNOWN in this report |
| T03 | Destructor calls FreeplayDataTracker ForceUpdate before AbortAll and before BehaviorManager destruction | 0x511114..0x511120; BehaviorManager call 0x51112C | M1-044; ForceUpdate branches to 0x8CB76C (0x56EEB8); SendData body UNKNOWN here |
| T04 | AbortAll always calls Cancel(type=-1), Path Abort, Dock Abort, SendAbortAnimation, then Movement StopAllMotors. Calls are not short-circuited by earlier return values | 0x511952 mov.w r1,#-1; calls 0x51195A/960/96A/972/97C | M1-044; call order recovered; nested Cancel and Path callees remain gaps |
| T05 | AbortAll return is boolean OR of Path Abort, Dock Abort and SendAbortAnimation results; Cancel and StopAllMotors results are not included | 0x511964/96E/976 save results; 0x511980..988 OR and normalize | M1-044; recovered |
| T06 | Dock Abort constructs AbortDocking, calls Robot SendMessage with r2=1,r3=0, clears message and returns saved send result | 0x63BE2A, 0x63BE32..36, 0x63BE3A..3E, 0x63BE52 | M1-044 / docking interface; SendMessage internals not rechecked |
| T07 | SendAbortAnimation similarly constructs AbortAnimation and sends with r2=1,r3=0, returning saved result | 0x517DFE, 0x517E06..0A, 0x517E0E..12, 0x517E26 | M1-044 / M5 interface; no assumption about delivery after transport close |
| T08 | ActionList Clear refuses reentry when byte +0xC is nonzero; sets byte before destroying queues; restores empty tree fields and clears byte afterward | 0x53D91A..91E; 0x53D928..93A | M1-044 / B-ACTIONS; queue destructor and callbacks UNKNOWN here |
| T09 | Robot destructor clears ActionList, then sets robot+0x250 to null BEFORE its destructor. Vision deletion and Mood destruction come later | 0x511146; 0x511150 str.w r5,[r4,#0x250] (r5=0); 0x511156; 0x51116C; 0x5111B6 | M1-044; recovered |
| T10 | Mood destructor unregisters callback only if callback id, Robot pointer AND Robot+0x250 are nonzero; otherwise jumps past unregister | 0x67AE18..28 cbz gates; unregister 0x67AE2A; id zero store 0x67AE30 | M7-020 / M1-044; on Robot destruction T09 makes unregister skip. Do not invent an unregister call into an already-destroyed ActionList |
| T11 | BehaviorManager destructor frees reaction maps, activity ownership, signal handles, container and current activity ownership. There is no direct IBehavior Stop call in the checked body | 0x5A0D22..0x5A0DF6; container 0x5A0DB2, shared releases 0x5A0D5C/DBE/DD6/DDE | M1-044; nested destructors can still have behavioral effects; NOT proof that no behavior stops anywhere |
| T12 | AnimationStreamer destructor clears its send buffer first, then destroys subscriptions, queued messages, song notes, track layers and live-idle parameter maps | 0x57AF58; 0x57AF70/78/80/88/94; 0x57AFC2/CC | M1-044 / M5; ClearSendBuffer, TrackLayer destructor and 0x52380C internals UNKNOWN here |
| T13 | Movement StopAllMotors has gates: all +0xB8/+0xB9/+0xBA zero OR +0xD4 nonzero skips its direct-drive stop block. Tail call 0x64099C still happens | 0x63FBE2..0x63FBFE; block 0x63FC02..0x63FE0A; tail 0x63FE0E..10 | M1-044 / M4; zero speed/check-lock calls observed with masks 1,2,4; exact nested wire behavior UNKNOWN here. Do not replace with an unconditional guessed stop packet |
| T14 | Path Abort invokes planner slot +0x10 if present, clears shared ownership, calls ClearPath, updates status by old-state predicate, clears +0x46 and destroys pending functions | 0x64914A..56; 0x64915A..68; 0x64916A; 0x649170..88; 0x64918C..B4 | M1-044 / M13; ClearPath and SetDriveToPoseStatus bodies UNKNOWN here |

## Current implementation comparison (source is not inferred from C#)

The live C# chain is RobotConnectionManager.HandleDisconnectMessage -> RobotManager.RemoveRobot -> engine RobotRemoved delegate -> CozmoRobot.ResetDevices -> per-subscriber RobotRemoved callbacks.

- RobotManager.RemoveRobot (CozmoEngine.cs:1559) sets _robot and _ric null before invoking the reset delegate. Native removes map ownership AFTER Robot destruction (T02). Callback visibility of Robot existence is therefore an ordering discrepancy for the manager to investigate, not merely an allocation detail.
- CozmoRobot.ResetDevices (CozmoRobot.cs:668) clears NV, stops/resets animation and resets devices, then calls subscribers. It has no native AbortAll sequence T04, no FreeplayDataTracker flush T03 and no upper-layer destructor ownership sequence T09..T12.
- FreeplayStack's only RobotRemoved subscriber (FreeplayStack.cs:246) calls Needs.OnRobotDisconnected. Thus that notification happens AFTER device resets. Native notification happens BEFORE Robot::~Robot, even when the game broadcast is skipped (T01/T02). The nearby comment claiming matching order checks only broadcast ordering and misses this intervening sequence.
- FreeplayStack.Dispose (FreeplayStack.cs:309) flushes tracker and disposes manager, but is not called from its RobotRemoved subscriber. A manual Dispose later cannot establish the removal production path.
- Existing test M1_025_M1_015_CB33_CC26_CC27_RemoveRobotLeavesEveryDeviceAsConstructed (EngineAppLayerTests.cs:1594) checks device reset and held references. It does not establish upper-layer teardown, AbortAll ordering, tracker flush or absence of stale behavior/mood across reconnect. Its passing result cannot settle M1-044.

These are bounded comparisons, not a proposed reset implementation. Simply calling FreeplayStack.Dispose or adding generic Reset methods is NOT established by the native source: T11 has nested destructor effects, T10 depends on pointer clearing, and T13 has gates.

## Remaining extraction required before an implementation inventory is complete

1. AbortAll: ActionList Cancel(type), queue destruction/callback handling (coordinate with B-ACTIONS), Path ClearPath/status, Movement direct-drive helper and 0x64099C. Trace send failure behavior and actual remaining transport availability at the removal entry.
2. ForceUpdate -> 0x8CB76C SendData and Needs/PerfMetric disconnect bodies; data emission and persistence ordering.
3. BehaviorContainer, owned IBehavior/IActivity destructors, BehaviorSystemManager, AIComponent/helpers, Path/Map/BlockWorld destructor bodies; constructor defaults on reconnect. No generic clear-all substitute.
4. Vision worker join and AnimationStreamer ClearSendBuffer/TrackLayer destruction; verify in-flight callbacks and queued frame ownership across removal.
5. M1-025 ExitSdkMode E7..E9 remains a separate outstanding path even if teardown is finished.

All unread shipped bodies above are RECOVERABLE_GAP at the step level, subject to manager record ownership/approval. No manifest is changed by this report. The manifest's M1 source_investigation_exhausted=true is not sufficient evidence of full coverage: current unresolved text and this table still name unread bodies inside implementation-gap records.

## Regression acceptance observations for the later manager task

Derive tests from checked rows, not from whatever reset implementation is chosen: ordered notifications/flush/abort/ownership removal; both RIC branches; original AbortAll return aggregation; reentrant Clear; Mood callback unregistration gated by Robot+0x250; callback view of robot availability; no stale robot-owned behavior/mood/path/map on a subsequent connection. Nested callback and wire expectations require the remaining extraction before specifying their oracle. No new tests or hardware acceptance were run here.

## Result

Added primary-instruction evidence and a bounded teardown handoff. No inventory/status/code changes and no commit (research-lane rule). Full M1-044 recovery and implementation remain open. This report must not be used to settle M1-015, M1-025 or M1-044 without the missing nested paths and independent verification.
