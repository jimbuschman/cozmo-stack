# B-CORE batch 4 - extraction report

Read-only. Engine: `libcozmoEngine.so` (the extracted copy named in the task). Every address is an ELF VA; a symbol value with the low bit set is the Thumb address, the instruction address is the even value. Ghidra (`re-analysis/decomp/libcozmoEngine/`) was used to navigate; every cited instruction was read from the .so with `.scratch/disarm_range.py`. PLT stubs were resolved through their GOT relocations with LIEF. Nothing in the repo was modified; this report and `.scratch/B-CORE-b4/*` are the only output.

Scope: Q1 (RemoveRobot upper-layer reset, M1-015 / M1-025) and Q2 (idle-timeout GoToSleep action, M1-031). No code proposed.

---

## Q1. RemoveRobot's upper-layer reset (0x0052F238..0x0052F364)

### Q1.1 - the RemoveRobot tail, instruction by instruction

Context: `Anki::Cozmo::RobotManager::RemoveRobot` entry `0x0052F1A4` (Ghidra body 0x0052F1A4..0x0052F365). Before 0x0052F238 it looks the id up in the robot map (0x0052F1BE..0x0052F1E8, warning "Robot %d does not exist. Ignoring." at 0x0052F1F2..0x0052F228) and then looks the id up in the `RobotInitialConnection` hash at `this+0x70`:

| claim | citation | status |
| --- | --- | --- |
| RIC lookup: `find<unsigned int>` on `this+0x70`, result r0 = RIC or 0 | `0x0052F22C add.w r8, r4, #0x70`; `0x0052F230 add r1, sp, #0xc`; `0x0052F232 mov r0, r8`; `0x0052F234 blx #0x4a9c58` -> `_ZNSt6__ndk112__hash_tableI...22RobotInitialConnection...E4findIjE...` | EXACT |
| No RIC found -> go straight to the RobotDisconnected broadcast path | `0x0052F238 cbz r0, #0x52f29e` | EXACT |
| RIC found: result = 1 ConnectionFailure, or 2 ConnectionRejected when `r7` (wasConnecting) != 0; call `RIC::HandleDisconnect(RIC+0xc, result)` | `0x0052F23A movs r1, #1`; `0x0052F23C adds r0, #0xc`; `0x0052F23E cmp r7, #0`; `0x0052F240 it ne`; `0x0052F242 movne r1, #2`; `0x0052F244 blx #0x4a9c64` -> `_ZN4Anki5Cozmo22RobotInitialConnection16HandleDisconnectENS0_21RobotConnectionResultE` (0x0052DCB8) | EXACT |
| If HandleDisconnect returns 1, skip the RobotDisconnected broadcast / `$session_id` clear and go to the notify+teardown block; otherwise fall into the broadcast block | `0x0052F248 cmp r0, #1`; `0x0052F24A bne #0x52f29e`; `0x0052F24C add.w r6, r4, #0x18`; `0x0052F250 b #0x52f2dc` | EXACT |
| Broadcast path: virtual call slot +0x30 on the external interface at `[*(this+0x18)+4]`, passing the id | `0x0052F29E mov r6, r4`; `0x0052F2A0 ldr r1, [sp, #0xc]`; `0x0052F2A2 ldr r0, [r6, #0x18]!`; `0x0052F2A6 ldr r0, [r0, #4]`; `0x0052F2A8 ldr r2, [r0]`; `0x0052F2AA ldr r2, [r2, #0x30]`; `0x0052F2AC blx r2` | EXACT |
| Build `MessageEngineToGame(RobotDisconnected)` with `timeSinceLastMsg_sec = 0.0` | `0x0052F2AE ldr r0, [r6]`; `0x0052F2B6 ldr r7, [r0, #4]`; `0x0052F2B8 ldr r0, [r7]`; `0x0052F2BA ldr.w sl, [r0, #0x1c]`; `0x0052F2BE movs r0, #0`; `0x0052F2C0 str r0, [sp, #8]`; `0x0052F2C2 mov r0, sb`; `0x0052F2C4 blx #0x4a9c70` -> `_ZN4Anki5Cozmo17ExternalInterface19MessageEngineToGameC1EONS1_17RobotDisconnectedE` | EXACT |
| Broadcast it: virtual call slot +0x1c on the same interface | `0x0052F2C8 mov r0, r7`; `0x0052F2CA mov r1, sb`; `0x0052F2CC blx sl` | EXACT |
| Release the built message | `0x0052F2CE add r0, sp, #0x10`; `0x0052F2D0 blx #0x4a5158` -> `_ZN4Anki5Cozmo17ExternalInterface19MessageEngineToGame12ClearCurrentEv` (0x00720228) | EXACT |
| `$session_id` clear | `0x0052F2D4 adr r0, #0x10c` -> "$session_id" at 0x0052F3E4; `0x0052F2D6 movs r1, #0`; `0x0052F2D8 blx #0x4a9b14` -> `_ZN4Anki4Util10sSetGlobalEPKcS2_` (0x0080DA98) | EXACT |
| Notify NeedsManager | `0x0052F2DC ldr r0, [r6]`; `0x0052F2DE ldr r0, [r0, #0x34]`; `0x0052F2E0 blx #0x4a9c7c` -> `_ZN4Anki5Cozmo12NeedsManager19OnRobotDisconnectedEv` (0x00695908) | EXACT |
| Notify PerfMetric | `0x0052F2E4 ldr r0, [r6]`; `0x0052F2E6 ldr r0, [r0, #0x3c]`; `0x0052F2E8 blx #0x4a9c88` -> `_ZN4Anki5Cozmo10PerfMetric19OnRobotDisconnectedEv` (0x0050B734) | EXACT |
| DAS pause: `DASPauseUploadingToServer(0)` | `0x0052F2EC movs r0, #0`; `0x0052F2EE blx #0x4a51a0` -> `DASPauseUploadingToServer` (imported; ELF symbol value 0, provided by the app) | EXACT |
| If the map node has a Robot, destroy it: `Robot::~Robot` then `operator delete` | `0x0052F2F2 ldr r0, [r5, #0x14]`; `0x0052F2F4 cbz r0, #0x52f2fe`; `0x0052F2F6 blx #0x4a9c94` -> `_ZN4Anki5Cozmo5RobotD1Ev` (0x005110D4); `0x0052F2FA blx #0x4a40cc` -> `_ZdlPv` | EXACT |
| Erase the robot from the map | `0x0052F2FE mov r0, r4`; `0x0052F300 mov r1, r5`; `0x0052F302 blx #0x4a9ca0` -> `__tree<..., Robot*>::erase` | EXACT |
| Erase the id from the id vector at `this+0xc..+0x10` (find, then ranged move) | `0x0052F306 ldrd r5, r0, [r4, #0xc]`; `0x0052F30A cmp r5, r0`; `0x0052F30C beq #0x52f34c`; `0x0052F310 ldr r2, [r5]`; `0x0052F312 cmp r2, r1`; loop to 0x0052F31A; `0x0052F32E blx #0x4a6eec` -> `__aeabi_memmove4`; `0x0052F34A str r0, [r4, #0x10]` | EXACT |
| Erase the id from the RIC hash at `this+0x70` | `0x0052F34C add r1, sp, #0xc`; `0x0052F34E mov r0, r8`; `0x0052F350 blx #0x4a9cac` -> `__hash_table<..., RobotInitialConnection...>::__erase_unique<unsigned int>` | EXACT |
| `$phys` clear | `0x0052F354 adr r0, #0x98` -> "$phys" at 0x0052F3F0; `0x0052F356 movs r1, #0`; `0x0052F358 blx #0x4a9b14` -> `Util::sSetGlobal` | EXACT |
| `$group` clear | `0x0052F35C adr r0, #0x98` -> "$group" at 0x0052F3F8; `0x0052F35E movs r1, #0`; `0x0052F360 blx #0x4a9b14` -> `Util::sSetGlobal` | EXACT |
| Return through the shared epilogue (stack-guard check) | `0x0052F364 b #0x52f286`; epilogue `0x0052F286..0x0052F29A` (`__stack_chk_fail` PLT 0x004a4fe4 at 0x0052F29A) | EXACT |

Every PLT target above was resolved from the GOT relocation, not from a name:

| PLT stub | resolved symbol |
| --- | --- |
| 0x004a9c58 | `std::__ndk1::__hash_table<...RobotInitialConnection...>::find<unsigned int>` |
| 0x004a9c64 | `Anki::Cozmo::RobotInitialConnection::HandleDisconnect(RobotConnectionResult)` (0x0052DCB8) |
| 0x004a9c70 | `Anki::Cozmo::ExternalInterface::MessageEngineToGame::MessageEngineToGame(RobotDisconnected&&)` |
| 0x004a5158 | `Anki::Cozmo::ExternalInterface::MessageEngineToGame::ClearCurrent()` (0x00720228) |
| 0x004a9b14 | `Anki::Util::sSetGlobal(char const*, char const*)` (0x0080DA98) |
| 0x004a9c7c | `Anki::Cozmo::NeedsManager::OnRobotDisconnected()` (0x00695908) |
| 0x004a9c88 | `Anki::Cozmo::PerfMetric::OnRobotDisconnected()` (0x0050B734) |
| 0x004a51a0 | `DASPauseUploadingToServer` (imported) |
| 0x004a9c94 | `Anki::Cozmo::Robot::~Robot` D1 (0x005110D4) |
| 0x004a40cc | `operator delete` (`_ZdlPv`) |
| 0x004a9ca0 | `std::__ndk1::__tree<..., Robot*>::erase` |
| 0x004a9cac | `std::__ndk1::__hash_table<...RobotInitialConnection...>::__erase_unique<unsigned int>` |
| 0x004a6eec | `__aeabi_memmove4` |

**What "the upper-layer reset" is.** RemoveRobot has **no separate upper-layer reset function**. Its only action on the Robot is the destructor call at `0x0052F2F6` (PLT 0x004a9c94 -> `Robot::~Robot`, body `0x005110D4..0x005115F8`). The audit's residual (a) list is what that destructor tears down. The destructor's own top-level calls (read at `0x005110D4..0x005115F8`; PLT targets resolved the same way):

| step | citation | status |
| --- | --- | --- |
| Log `robot.destructor`; `FreeplayDataTracker::ForceUpdate`; `Robot::AbortAll` (aborts every action) | `0x005110EE blx #0x4a4f90`; `0x0051111A blx #0x4a7a5c`; `0x00511120 blx #0x4a7a68` | EXACT |
| Destroy `BehaviorManager` (member +0x44) | `0x00511124 ldr r0, [r4, #0x44]`; `0x0051112C blx #0x4a7978` | EXACT |
| Destroy `BehaviorSystemManager` (member +0x48) | `0x00511134 ldr r0, [r4, #0x48]`; `0x0051113A blx #0x4a796c` | EXACT |
| Clear + destroy `ActionList` (member +0x250) | `0x00511146 blx #0x4a7a74` (`ActionList::Clear`); `0x00511156 blx #0x4a7948` (`~ActionList`) | EXACT |
| Destroy `VisionComponent` (member +0x258) through its vtable slot +4 | `0x0051115E ldr.w r0, [r4, #0x258]`; `0x00511168 ldr r1, [r0]`; `0x0051116A ldr r1, [r1, #4]`; `0x0051116C blx r1` | EXACT |
| Destroy `MoodManager` (member +0x440) | `0x005111B0 ldr.w r0, [r4, #0x440]`; `0x005111B6 blx #0x4a7a80` | EXACT |
| Destroy `AIComponent` (member +0x264) | `0x00511276 blx #0x4a7930` (and again at 0x005114B8..0x005114C4) | EXACT |
| Destroy `PathComponent` (member +0x5c) | `0x00511284 blx #0x4a7960` (and again at 0x005114C4) | EXACT |
| Destroy `BlockWorld` (member +0x34) | `0x005112CE blx #0x4a7984` | EXACT |
| Destroy `TextToSpeechComponent` (member +0x268) | `0x005114B0 blx #0x4a7924` | EXACT |
| Destroy `MapComponent` (member +0x25c) | `0x005114DC ldr.w r0, [r4, #0x25c]`; `0x005114E8 blx #0x4a793c` | EXACT |
| Free `CarryingComponent` (member +0x284) and `DockingComponent` (member +0x280) | `0x005113FA ldr.w r0, [r4, #0x284]`; `0x00511406 blx #0x4a40cc`; `0x0051140A ldr.w r0, [r4, #0x280]`; `0x00511414 blx #0x4a40cc` | EXACT |
| Destroy `PoseOriginList` (+0x294), `TouchSensorComponent` (+0x28c), `CliffSensorComponent` (+0x288) | `0x005113CC blx #0x4a78dc`; `0x005113E0 blx #0x4a78e8`; `0x005113F2 blx #0x4a78f4` | EXACT |
| Destroy `AnimationStreamer` (member +0x60) | `0x0051154A blx #0x4a7954` | EXACT |
| FaceWorld (+0x38) and BlockWorld (+0x34) tree teardown; base `FUN_00511098(this+4)` | `0x005115D6 blx #0x4a5af4`; `0x005115E6 blx #0x4a7984`; `0x005115F0 bl #0x00511098` | EXACT |
| `PathFollower`, `IdleBehavior`, `ReactiveBehavior`, `CubeMovedReactionStrategy`, `MapComponent -> MemoryMap`, `AIComponent -> FreeplaySystem` | not separately resolved here; they are sub-objects of `AIComponent` / `BehaviorManager` / `MapComponent`. The three parent destructors are cited above. | UNKNOWN (what their own destructors do) / RECOVERABLE_GAP |

So the engine does reset the upper layer: it destroys the whole Robot, and the destructor aborts all actions, force-updates FreeplayDataTracker, and destroys every component (including behaviour, mood, map, AI/freeplay, path, docking, carrying). The stack's `ResetDevices` only resets devices, which is the gap the audit named. The residual text in M1-025 ("behaviour, mood, docking, path and freeplay state survive RemoveRobot") is describing the **stack**, not the engine: the engine's RemoveRobot destroys them.

### Q1.2 - VisionSystem.Enabled / the VisionComponent +0x48 flag

| claim | citation | status |
| --- | --- | --- |
| The engine has no "VisionSystem.Enabled". The per-robot enabled flag is the `VisionComponent` byte at +0x48; the constructor zeroes +0x48..+0x4B | `0x006500B4 movs r5, #0`; `0x006500E6 str r5, [sl, #0x4c]!` (sl = this); `0x006500EE str r5, [sl, #-0x4]` (this+0x48 = 0) | EXACT |
| +0x48's only writer after the constructor is the NV CameraCalib callback, which stores 1 | `0x0065AE7E movs r0, #1`; `0x0065AE80 strb.w r0, [r5, #0x48]` (the callback body ending at 0x0065AE8C) | EXACT (matches M3-022 / M3 row 2d) |
| The reader is `VisionComponent::SetNextImage`: `+0x48 == 0` -> "not enabled", return 0 | `0x00652B20 ldrb.w r0, [r4, #0x48]`; `0x00652B24 cbz r0, #0x652b9e` | EXACT |
| RemoveRobot itself touches no VisionComponent field; it only calls `Robot::~Robot` | the whole range 0x0052F238..0x0052F364 contains no load/store of +0x48; the only VisionComponent-related action is the destructor at 0x0052F2F6 | EXACT |
| `Robot::~Robot` destroys the VisionComponent through vtable slot +4 = the deleting destructor D0, not a reset of +0x48 | `0x0051115E..0x0051116C`; vtable `_ZTVN4Anki5Cozmo15VisionComponentE` at 0x0102F830, object vptr = 0x0102F838, slot +4 = 0x0102F83C = `_ZN4Anki5Cozmo15VisionComponentD0Ev` (0x00652794) | EXACT |
| `VisionComponent::~VisionComponent` (D1, 0x00652554) does not write +0x48; it joins the processor thread, destroys the owned `VisionSystem` (+0x1c), and frees everything | `0x00652570 strb.w r0, [r4, #0x4a]`; `0x0065257C blx #0x4a6a54` (`std::__ndk1::thread::join`); `0x0065258A ldr r0, [r4, #0x1c]`; `0x0065258E blx #0x4ba338` (`VisionSystem::~VisionSystem`); `0x00652592 blx #0x4a40cc` | EXACT |

Conclusion for Q1.2: the binary **does** settle it. RemoveRobot does not touch any enabled flag; the Robot destructor destroys the whole `VisionComponent` and the `VisionSystem` it owns. There is no engine-side enabled flag that survives a removal, because its owner is freed. "VisionSystem.Enabled kept across a removal" is a **stack-side** object (`cozmo-stack/src/Cozmo.Robot/Vision/VisionSystem.cs`, `Enabled` set at lines 71/106) with no engine counterpart that persists; the engine gives no rule for a persistent app-level VisionSystem. The record's "UNKNOWN" should be resolved to: the engine destroys the component; whether the stack's persistent object must clear `Enabled` is a stack design question, not an engine behaviour.

### Q1.3 - "the local 2 s join bound" and "the CD21 read" / CD16

| term | what it is | citation | owner / recorded? |
| --- | --- | --- | --- |
| local 2 s join bound | A **stack-local** timeout while waiting for the in-flight vision frame at removal. The engine's `VisionComponent::~VisionComponent` calls `std::__ndk1::thread::join` with **no timeout**, so it blocks until the Processor thread ends. | engine: `0x00652574 ldr.w r0, [r4, #0x30c]`; `0x00652578 cbz r0, #0x652580`; `0x0065257A mov r0, r5`; `0x0065257C blx #0x4a6a54` (PLT -> `_ZNSt6__ndk16__thread4joinEv`). stack: M1-015 provenance residual (d), `fidelity_manifest.json` M1-015 / `FIDELITY_GAPS.md:135`. | **Not a manifest record.** The engine behaviour (unbounded join) lives in the VisionComponent destructor, which no M3/M11 record owns. Only M1-015's provenance mentions it. So: effectively unrecorded; the "2 s" is not the engine's. |
| CD21 read | The connection-time NV `Read` of tag 0x80000001 (CameraCalib) queued by `VisionComponent` on connection; its callback installs the calibration and sets +0x48. | `re-analysis/inventory/M1-transport.md:547` (row CD21, owned "M3 interface"); manifest **M3-022** (`evidence`: `0x006583E2..0x006583FA`, callback `0x0065AB68`, `0x0065AE7E/0x0065AE80`). | **Recorded.** M1 row CD21 + M3-022 (IMPLEMENTATION_GAP, built batch 1). The audit's "no record" is wrong for the read itself; the only unrecorded part is the interaction "RemoveRobot drops a caller-read calibration". |
| CD16's Lab read | The mfgId handler queues `ReadLabAssignmentsFromRobot` (0x196000) then `ConnectRobotToNeedsManager`, after `SendConnectionResponse`. | `re-analysis/inventory/M1-transport.md:542` (row CD16, owned **M1-028**); manifest M1-028 (`0x0052E3AA`, `0x0052E3B2`; `0x0052E3AA -> 0x006A5B1E`). | **Recorded.** M1-028 (IMPLEMENTATION_GAP, built batch 2). |

Searches run for Q1.3: read the full M1 inventory rows CD1..CD31 (lines 536..557); read `re-analysis/research/20260929-R-DEV-pre-extraction.md` (no mention of CD16, CD21, RemoveRobot, or a join bound; it covers VisionComponent Init/EnableMode/vision config, M4 cliff, M3 mu-law); grepped the manifest for records mentioning Robot::~Robot / destructor / upper-layer / thread::join / VisionComponent::~ (only M1-015 and M1-025, both in prose, neither owning the behaviour).

---

## Q2. The idle-timeout GoToSleep action (M1-031, row CC6)

### Q2.1 - the two PLT stubs at 0x0052CE5E and 0x0052CE6A

`RobotIdleTimeoutComponent::Update` entry `0x0052CE30` (symbol value 0x0052CE31; the instruction address is even). On the faceOff deadline (`[this+0x10] > 0` and `<= now`) at `0x0052CE3C..0x0052CE58` it clears the deadline and queues the action:

| claim | citation | status |
| --- | --- | --- |
| `0x0052CE5E blx #0x4a9a30` is `RobotIdleTimeoutComponent::CreateGoToSleepAnimSequence(Robot&)`; GOT slot 0x0104209C | `0x0052CE5E blx #0x4a9a30`; PLT 0x004A9A30 -> `_ZN4Anki5Cozmo25RobotIdleTimeoutComponent27CreateGoToSleepAnimSequenceERNS0_5RobotE` (body 0x0052CEA2) | EXACT |
| `0x0052CE6A blx #0x4a5788` is `ActionList::QueueAction(QueueActionPosition, IActionRunner*, u8)`; GOT slot 0x01040A64 | `0x0052CE62 mov r2, r0` (the built action); `0x0052CE64 mov r0, r5` (ActionList from `[robot+0x250]`); `0x0052CE66 movs r1, #0`; `0x0052CE68 movs r3, #0`; `0x0052CE6A blx #0x4a5788`; PLT 0x004A5788 -> `_ZN4Anki5Cozmo10ActionList11QueueActionENS0_19QueueActionPositionEPNS0_13IActionRunnerEh` (0x0053D93C) | EXACT |
| The ActionList comes from `Robot+0x250` | `0x0052CE54 ldr r0, [r4, #0xc]` (the Robot); `0x0052CE5A ldr.w r5, [r0, #0x250]` | EXACT |

The caller passes only the Robot& (r0) to the factory; `r1=0`/`r3=0` are the QueueAction position and the u8 argument, and `r2` is the returned action.

### Q2.2 - what `CreateGoToSleepAnimSequence` builds

Body `0x0052CEA2..0x0052CFC0`. It takes only `Robot&` (r0 = `sb`); there is **no trigger argument and no animation name**. It builds a fixed action tree:

| step | citation | status |
| --- | --- | --- |
| `operator new(0xac)` + `CompoundActionSequential(robot)` | `0x0052CEAA movs r0, #0xac`; `0x0052CEAC blx #0x4a42a0`; `0x0052CEB4 blx #0x4a9184` -> `_ZN4Anki5Cozmo24CompoundActionSequentialC1ERNS0_5RobotE` | EXACT |
| Add `TriggerAnimationAction(robot, trigger 0xd2, 1, 1, 0, 60.0f, 0)` | `0x0052CECA movt r8, #0x4270` (r8 = 0x42700000 = 60.0f); `0x0052CED0 stm.w sp, {r0, r4, r8}` (r0=1, r4=0); `0x0052CED8 movs r2, #0xd2`; `0x0052CEDA movs r3, #1`; `0x0052CEDC str r4, [sp, #0xc]`; `0x0052CEDE blx #0x4a9448` -> `_ZN4Anki5Cozmo22TriggerAnimationActionC1ERNS0_5RobotENS0_16AnimationTriggerEjbhfb` | EXACT |
| Add trigger **0xd5** | `0x0052CF0E movs r2, #0xd5`; `0x0052CF14 blx #0x4a9448` | EXACT |
| Add trigger **0xd4** | `0x0052CF44 movs r2, #0xd4`; `0x0052CF4A blx #0x4a9448` | EXACT |
| `operator new(0x9c)` + `CompoundActionParallel(robot)`; add the sequential | `0x0052CF62 movs r0, #0x9c`; `0x0052CF6C blx #0x4a9178` -> `_ZN4Anki5Cozmo22CompoundActionParallelC1ERNS0_5RobotE`; `0x0052CF80 blx r7` (vtable +0x20, add child) | EXACT |
| Add `MoveLiftToHeightAction(robot, preset 0, 5.0f)` | `0x0052CF8E movs r0, #0xa4`; `0x0052CF9A movt r3, #0x40a0` (0x40A00000 = 5.0f); `0x0052CF9E movs r2, #0`; `0x0052CFA2 blx #0x4a9a48` -> `_ZN4Anki5Cozmo22MoveLiftToHeightActionC1ERNS0_5RobotENS1_6PresetEf` | EXACT |
| Return the parallel action | `0x0052CFBA mov r0, r6` | EXACT |

So the action type is a compound animation action with a **literal trigger list {0xd2, 0xd5, 0xd4}** plus a lift-to-preset-0 move at tolerance 5.0f. The M5 inventory (CC8, `M1-transport.md:498`) maps those triggers to `GoToSleepGetIn`, `GoToSleepSleeping`, `GoToSleepOff`; that mapping is an M5 interface claim, not re-derived here. There is no animation-name argument.

### Q2.3 - other callers of the factory

| claim | citation | status |
| --- | --- | --- |
| The only caller of the `CreateGoToSleepAnimSequence` PLT thunk (0x004A9A30) is `RobotIdleTimeoutComponent::Update` at `0x0052CE5E`; the only caller of the body (0x0052CEA2) is that thunk. No other call site exists. | instruction: `0x0052CE5E blx #0x4a9a30`. Ghidra caller lists: `004a/004a9a30.c` "callers: ...Update@0052ce30"; `0052/0052cea2.c` "callers: ...CreateGoToSleepAnimSequence@004a9a30". A per-function BL/BLX scan of every `index.tsv` function found exactly one call to 0x004A9A30 (0x0052CE5E) and zero direct calls to 0x0052CEA2. | EXACT |

---

## Existing records contradicted by the source

- **M1-025** (`unresolved`): "behaviour, mood, docking, path and freeplay state survive RemoveRobot with no record owning the reset". This describes the **stack**, not the engine. The engine's RemoveRobot destroys the Robot (`0x0052F2F6` -> `Robot::~Robot` 0x005110D4), whose destructor destroys BehaviorManager (`0x0051112C`), MoodManager (`0x005111B6`), AIComponent (`0x00511276`/`0x005114C4`), PathComponent (`0x00511284`), MapComponent (`0x005114E8`), CarryingComponent/DockingComponent (`0x00511406`/`0x00511414`). The record text should not read as if the engine leaves them alive.
- **M1-015** (`unresolved`) / the 2026-09-29 audit line: "the CD21 read ... sit only in provenance prose with no record". Contradicted by the current manifest: CD21 is M1 row `M1-transport.md:547` and manifest **M3-022** owns the read (queued at 0x006583E2..0x006583FA, callback 0x0065AB68, +0x48 writer 0x0065AE80). CD16's Lab read is M1 row `:542` and manifest **M1-028** (0x0052E3AA). The unrecorded part is narrower: the RemoveRobot interaction with an in-flight CD21 read.

## Existing records whose evidence is too weak to keep their status

- **M1-015 / M1-025** (both IMPLEMENTATION_GAP): their provenance residuals (a)-(d) are prose-only and the audit is right that no record owns the upper-layer reset, the VisionSystem.Enabled question, the 2 s join bound, or the CD21 reset interaction. The records should not be settled until those parts get their own records (AGENTS.md "a settled record owns its whole production path"). This report supplies the citations for the upper-layer reset and VisionSystem.Enabled; the 2 s join bound and the CD21-reset interaction remain without an owner.
- **M3-022** (`unresolved`): the manifest text says "built, awaiting strong verification (Batch 1, uncommitted)" while `status/B-CORE.md` records Batch 1 as pushed `dbdc39d`. Text is stale; behaviour evidence holds.

## Open questions for the manager

1. The upper-layer reset (Robot destructor teardown, 0x005110D4) needs its own record(s). Does it belong to M1 (RemoveRobot's path) or to a Robot-lifetime record? The audit put it under M1-015/M1-025; the destructor body is really an M12-M15 interface.
2. `PathFollower`, `IdleBehavior`, `ReactiveBehavior`, `CubeMovedReactionStrategy`, `MemoryMap` and `FreeplaySystem` are not separately resolved in this pass; they are inside `AIComponent`/`BehaviorManager`/`MapComponent` destructors. A follow-up extractor pass should read those sub-destructors if the reset of those sub-objects must be recorded.
3. The VisionSystem.Enabled "UNKNOWN" can be closed on the engine side (the component is destroyed). The manager must decide whether the stack's persistent `VisionSystem.Enabled` is a separate record (it is a stack object, not an engine behaviour).
4. The 2 s join bound is a stack-local policy with no engine counterpart (the engine joins unbounded, 0x0065257C). Does it need a COMPATIBILITY_POLICY record, or should the stack match the unbounded join?
5. M1-031 CC6 is otherwise correct (the queue call and the factory are exactly as cited); the only open item is the M5 trigger-name mapping, which belongs to M5.

