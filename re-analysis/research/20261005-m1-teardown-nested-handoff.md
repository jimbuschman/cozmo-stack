# M1 teardown addendum: nested behavior, helpers and disconnect notifications

| Coverage | Result |
| --- | --- |
| M1-044 robot-owned upper-layer destruction | BehaviorSystemManager, BehaviorContainer, base IBehavior/IActivity, ActivityFreeplay and AIComponent bodies checked |
| Helper ownership | Stack-vector release order, final-owner dispatch, base IHelper and all six concrete helper destructors checked locally |
| Manager-level disconnect | Needs OnRobotDisconnected, PerfMetric OnRobotDisconnected/Stop and tracker ForceUpdate/SendData checked |
| Whole M1 disconnect/reconnect path | Still incomplete: world/path/vision, shared-reference runtime, dynamic callback destructors and reconnect constructors not exhausted |
| Production implementation and fidelity statuses | Unchanged; no tests, hardware run or commit |

Answers the operator's 2026-10-05 instruction to continue productive M1 research. Follows 20261005-m1-teardown-handoff.md. Scope remains M1-044, not a repository-wide audit. Manager checks this research before inventory integration.

## Original and capture method

APK libcozmoEngine.so SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Thumb instructions read from ELF segments. PLT symbols resolved via relocations. Interworking tail thunks explicitly decoded in ARM mode: bx pc at Thumb stub, ARM ldr ip,[pc] + add pc,ip,pc, with relative target computed from literal. Ghidra used for navigation, not as evidence.

Evidence files: 20261005-m1-teardown-nested-instructions.txt, 20261005-m1-helper-destructor-instructions.txt, 20261005-m1-teardown-nested-tail-instructions.txt. 20261005-m1-capture.py is a read-only capture helper (its range limits are instruction ranges, not proof all called bodies were recovered).

Current manifest records read:

- M1-044: "RemoveRobot's upper-layer teardown: Robot::~Robot aborts all actions and destroys the behaviour, mood, AI/freeplay, path, map, docking, carrying and vision components", IMPLEMENTATION_GAP. Evidence includes 0x005110D4, top-level destructor calls and the stack's ResetDevices counterpart. Current unresolved says named/not built; this report does not settle it.
- M8-011: "The Smart* scope helpers: idle animation, motion profile, track locks, custom light patterns, helper delegation and the reaction locks", IMPLEMENTATION_GAP. Evidence includes SmartDelegateToHelper 0x005BEB10 -> component 0x0056DAD8, robot+0x264 then AI+0x10. Current evidence describes the component/subclasses as unbuilt; current code has the component and base helper, so that historical wording is not a reliable code inventory.
- M15-015: "Freeplay active-time tracker and its four pause sources", IMPLEMENTATION_GAP. Evidence includes SendData 0x0056EC48, constructor 0x0056EBD4 and pause writers.
- M15-016: "NeedsManager pause and disconnect transitions", IMPLEMENTATION_GAP. Evidence includes OnRobotDisconnected 0x00695908, DetectBracketChangeForDas 0x00695958 and connection/game pause paths.

## Checked local inventory

RECOVERED below refers only to each local fact; it is not a manifest status change. Unread shipped nested bodies are RECOVERABLE_GAP at step level.

| Step | Behavior-changing ownership/order fact | Primary instruction citation | Ownership / limit |
| --- | --- | --- | --- |
| N01 | BehaviorSystemManager clears +0x10 and destroys that object's signal-handle vector; clears/deletes virtual +0xC object; clears +8 then destroys BehaviorContainer; clears +4 then releases contained shared owner | 0x5A588C..0x5A58CC | M1-044; member order recovered; virtual +0xC body UNKNOWN |
| N02 | BehaviorContainer destroys behavior shared-pointer tree, restores empty tree, destroys signal-handle vector, then invokes tree destroy again on current root | 0x59C712, stores 0x59C716..71C, 0x59C722, 0x59C72A | M1-044; do not reduce to Stop-current-and-clear-map; shared release callbacks can affect remaining ownership |
| N03 | IBehavior base destructor releases graphs, scorer/object vectors, weak helper ref, lock-name containers, callable storage, virtual owned object +0x38 and signal handles. No direct Stop/StopActing/SmartClear* call appears in the body | 0x5BC76E/776/77E/786; weak 0x5BC78A..790; containers 0x5BC79C/7A8; callable virtual 0x5BC7AC..7C4; owned object 0x5BC7F0..7FC; handles 0x5BC802 | M1-044/M8; base local fact only; concrete behavior destructors still UNKNOWN. Do not claim no nested effects |
| N04 | IActivity base releases its string set and shared owner at +0x30, then nulls/deletes +0x28, +0x24, +0x20 virtual owned objects in that order | 0x5B2FA0/FA8; 0x5B2FAC..FD2 | M1-044; owned object bodies UNKNOWN |
| N05 | ActivityFreeplay destroys its signal handles and nested-activity hash, then tail-calls IActivity destructor | 0x5AD89E, 0x5AD8A6, 0x5AD8B0 -> Thumb 0x8CBD3C -> ARM 0x8CBD40 -> PLT 0x4B135C | M1-044/M15; no direct normal Freeplay Stop call; nested activity types UNKNOWN |
| N06 | AI ctor identifies helper component as AI+0x10 and tracker as AI+0x2C | ctor 0x569AAE..AB2, 0x569B28..B2C | M1-044/M8/M15; recovered ownership assignment; other constructor bodies not exhausted |
| N07 | AI destructor member order is +0x30, +0x2C, +0x28, +0x24, +0x20, +0x1C, +0x18, +0x14, +0x10, +0xC, +8. Members are nulled before destruction | 0x569CB8..0x569DA6 | M1-044; local order recovered, virtual bodies +0x30/+0x18/+0xC UNKNOWN |
| N08 | Helper component destruction destroys failure callable (+0x28 storage, target +0x38), then success callable (+0x10, target +0x20), then helper vector (+4), then factory (+0) | 0x569D38..D4E, 0x569D50..D66, 0x569D6A, 0x569D6E..D7C | M1-044/M8; callable storage virtual slots +0x10/+0x14 by inline/allocated form; targets need concrete callable evidence |
| N09 | Helper vector releases shared owners from last element to first; decrements end BEFORE releasing each owner | 0x56A042..A060; end store 0x56A04C; shared release 0x56A054 | M8-011/M1-044; recovered vector traversal; shared-count runtime still a dependency |
| N10 | IHelper shared control's zero-shared hook dispatches helper deleting destructor slot +4, not Stop method | 0x56E37A..E384, slot load 0x56E380 | M8-011/M1-044; dispatch recovered; concrete helper path checked below |
| N11 | Base helper destruction handles callable storage at +0xD8,+0xC0,+0xA8,+0x80,+0x68; releases delegate shared control +0x64; handles +0x48,+0x30 callables; releases strings | 0x5B5F2A..5FE4; delegate 0x5B5FAE..5FB2; strings 0x5B5FE6..6006 | M8-011; function identified by all six derived destructor tails and IHelper vtable D1 word; no direct IHelper Stop/LogStopEvent call |
| N12 | DriveToHelper destroys pose then base. Pickup/PlaceBlock/PlaceRelObject immediately branch to base. RollBlock destroys extra callable then base. SearchForBlock destroys object-id tree, camera and signal handles then base | Drive 0x5B57C2/57CC; pickup 0x5B7B18; place 0x5B8C94; relative 0x5B8FB8; roll 0x5B99DE..99FA; search 0x5BAE6A/AE72/AE7A/AE84 | M8-011/M1-044; all six local bodies checked. Camera/Pose/signal callable destructors not fully traced; no normal helper Stop call appears in these bodies |
| N13 | Needs disconnect stores system-clock time, resets counter +0x30, force-writes device if byte +0x1D5==0, clears Robot+4, snapshots state timestamp, then forces bracket DAS and sends needs-level DAS | 0x695910..34; calls 0x69592A,0x69593C,0x695944 | M15-016/M1-044; exact WriteToDevice/DAS bodies remain separate dependencies |
| N14 | PerfMetric disconnect calls Stop only if byte +0xA nonzero, then ALWAYS DumpFiles | 0x50B738..73E; tail 0x50B748 -> Thumb 0x8CAD7C -> ARM 0x8CAD80 -> PLT 0x4A7378 | M1-044; DumpFiles body UNKNOWN; do not infer it is an optional no-op |
| N15 | PerfMetric Stop clears +9 if set, emits branch-specific info, then SendStatusToGame in both cases | 0x509722..744; log calls 0x50973E/75A; status 0x509782 | M1-044; logging/status details still need bodies/constants for full implementation |
| N16 | ForceUpdate is a direct SendData tail, not a flag to defer until another tick | 0x56EEB8 -> Thumb 0x8CB76C -> ARM 0x8CB770 -> PLT 0x4ACF10 | M15-015/M1-044; recovered dispatch |
| N17 | SendData accumulates uint64 nanoseconds only with empty pause set (+8==0); nonzero accumulated nanos convert unsigned to double, divide by embedded divisor, call round, convert signed32; seconds <37 emits event, otherwise error path/global error byte/debug-break gate. Clears accumulated amount; stamps resume nanos only if unpaused; always schedules now_seconds_f32+30 | 0x56EC5A..EC84; 0x56ECF2..ED26; event 0x56ED94; error 0x56ED46/ED7C/ED80; reset 0x56EDBA..EDDC | M15-015/M1-044; recovered control/arithmetic order. round is external phone libm; exact host treatment is not assumed |

## IHelper vtable caution

ELF symbol _ZTVN4Anki5Cozmo7IHelperE starts 0x01025B70, with address point 0x01025B78. Raw first destructor word at 0x01025B78 is 0x005B5F21; deleting-destructor word at 0x01025B7C is 0x005B6019. Thumb instruction at 0x005B6018 is a trap. Do not instantiate an invented concrete base IHelper and assume its deleting destructor works. The six concrete helper deleting destructors run their destructor path then operator delete through interworking thunk 0x8CA88C -> ARM 0x8CA890 -> PLT 0x4A40CC. Their Init/Update bodies remain unbuilt/unrecovered independently of the destructor facts here.

## Production comparison and why generic disposal is insufficient

The earlier report established ResetDevices only calls the devices and subscribers. FreeplayStack's RobotRemoved callback still only calls Needs.OnRobotDisconnected (FreeplayStack.cs:246). FreeplayStack.Dispose (line 309) calls tracker ForceUpdate and Manager.Dispose, but that later/manual disposal is not the robot-removal path.

BehaviorManager.Dispose (BehaviorManager.cs:1000) calls normal Stop before strategy disposal. Native base destruction examined in N01..N05 does not establish that as the correct robot-removal substitute; doing so can run normal Stop's smart-lock/profile/idle cleanup at a time when native is releasing ownership. Concrete behavior destructors must be checked before deciding the complete counterpart. Native already ran AbortAll before these destructors (previous report T04).

HelperControl.ReleaseOwner (BehaviorHelperComponent.cs:32) only decrements a count. Native N09/N10 gives the final owner a deleting-destructor dispatch, including release of delegate ownership. Clearing a C# list alone cannot establish the same helper lifetime and weak-reference behavior. This report does NOT propose adding Stop-on-zero: source dispatch is destruction.

Needs.OnRobotDisconnected (Needs.cs:993) has the local transition structure, but its invocation still occurs after ResetDevices, whereas native calls it before Robot::~Robot. N13 also establishes the clock is chrono system_clock, not engine elapsed seconds; whether _clockSec is the matching clock requires caller tracing (UNKNOWN in this bounded report).

## Remaining bounded tasks before claiming M1-044 complete

1. Resolve behavior shared_ptr/tree release and actual concrete behavior/activity deleting destructors reachable from owned containers. Keep callback targets and ownership counts visible; no Stop-all shortcut.
2. Read helper callable destruction/Camera/Pose/signal-handle bodies reached above and verify delegate final-owner cascades. The destructor facts do not implement helper Init/Update or normal Stop.
3. Trace Needs WriteToDevice, bracket/level events and clock caller; PerfMetric DumpFiles/SendStatusToGame; tracker event/error strings and external round semantics. These cannot disappear behind a comment.
4. Continue the previous handoff's world/map/path/vision/animation teardown and reconnect constructor requirements. This report does not cover their complete path.
5. Manager defines/approves any new ownership records before implementation; existing M1-044 remains IMPLEMENTATION_GAP. Report step-level unread bodies as RECOVERABLE_GAP without silently changing manifest flags.

## Later source-derived regression observations

Check tracker flush before AbortAll/destruction; Needs notification before teardown; PerfMetric DumpFiles even when auto-stop is disabled; helper stack owner release last-to-first with end adjusted before reentrant destruction; delegate ownership release and weak-lock failure after final owner; no assumed normal Stop callback during destruction; no old callable/subscription/behavior/map ownership survives reconnect. Callback expectations require the remaining concrete callable/derived destructor extraction.

No code or tests changed. No hardware acceptance. No commit under research-lane rules. The exact change is three evidence transcripts, this report and a read-only capture helper under re-analysis/research/. This reduces the unread nested teardown surface; it does not settle M1-044 or the records depending on it.
