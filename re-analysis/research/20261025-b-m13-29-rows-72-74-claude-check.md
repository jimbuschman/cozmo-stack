# Claude check: B-M13-29, rows 72, 73, 74

The manager (Opus) re-disassembled the key instructions of each answer from `resources/lib/armeabi-v7a/libcozmoEngine.so`
and read the B-M13-29 diff. **All four PASS.** No record settles.

## B-M13-29 (a3673eeb..0c6335a2): PASS

- Only diagnostics changed:
  - the type-7 MISSING at the markerless-family mapping (`MemoryMap.cs:40`);
  - the explored-area label (`:107`);
  - a citation in `M13LegacyMapSyncTests.cs`.
- Items 1 and 2 were already done by B-M13-28 (f73271da). The builder checked this and changed nothing executable.
  Mapping results, ray answers and tree writes are unchanged.

## Row 72, R-M7-BEHAVIOR-MANAGER-LIFETIME: PASS

| Claim | Instructions |
| --- | --- |
| Robot ctor allocates and constructs the manager, stored at robot+0x44 | 0x0050FCA0 `movs r0,#0x88`; 0x0050FCAC BehaviorManager ctor; 0x0050FCB0 `str r4,[r5,#0x44]` |
| AI comes later | 0x0050FDA0 AIComponent ctor; 0x0050FDA4 `str.w r4,[r5,#0x264]` |
| Both inits run in the Robot ctor, before AI Init | 0x00510388 InitConfiguration(config+0xE8); 0x00510396 InitReactionTriggerMap(config+0x130); 0x005103DC AIComponent::Init |
| Initialized byte | ctor 0x005A0874 `strb r5,[r4]` (0); InitConfiguration tail 0x005A0F70 `strb r0,[r4]` |
| Disable on an empty map returns | 0x005A281A..0x005A2824: begin == sentinel (manager+0x44) → `beq.w 0x5A2964` |
| Robot dtor nulls +0x44, then destroys the manager | 0x00511126..0x0051112C |

**Consequence for the stack.** In the engine the manager, with its reaction map populated, exists from Robot
construction. So SevereNeeds' Set at the connection wakeup always reaches real reaction entries. The stack builds its
manager only in FreeplayStack, and that is the B-M15-12 Q1 gap. The fix is a robot-lifetime manager (B-M7-4).

## Row 73, R-M15-NEEDS-GET-OUT: PASS

| Claim | Instructions |
| --- | --- |
| Only constructor call: factory adapter, 0xCC bytes | 0x0052A868 `movs r0,#0xcc`; 0x0052A872 ctor |
| Registration row: union 20 / G2E 27, adapters | 0x01020280: `14 00 00 00 14 00 1b 00 fd 5e 52 00 11 5f 52 00 00 00 00 00` |
| ctor: TriggerAnimationAction(Count 0x23F, loops 1); +0xC9 = 0 | 0x00544FA2 `movw r2,#0x23f`; 0x00544FA6 `movs r3,#1`; 0x00544FAE `strb.w r5,[r4,#0xc9]` |
| Init reads the selected need; above 3 → return 0 | 0x005450B4..0x005450BE |
| TBB `07 02 05 10` | Repair → 0x005450D6 with 0x141; Energy → 0x005450CC with 0x135; Play → 0x005450D2 with 0x13A; none → 0x005450E8 |
| Check result | ended +0x89 → 0; else failed +0x8A ? 0x03000001 : 0x01000000 (0x005451F2..0x0054520A) |

- **Who creates it:** only external game messages (QueueSingleAction union 20, or direct G2E 27). No engine behaviour or
  activity creates it.
- **When the app sends it** (authority 2): the needs hub before a challenge, the feed button during a severe-energy
  quick snack, and SDK enabling.
- **Under the app-boundary rule:** the action is engine behaviour that the app starts. The stack provides it as a C#
  API action (manager decision 2026-10-25; new record M15-074). The app's hub, feed and SDK flows aren't reproduced.
  SDK mode is unsupported anyway.

## Row 74, R-M13-ROOT-CONTENT-TS: PASS (PARTIAL writer census, as stated)

| Claim | Instructions |
| --- | --- |
| Getter = root data Last | 0x00681934 `ldr.w r0,[r0,#0xdc]`; 0x00681938 `ldr r0,[r0,#0xc]`; `bx lr` |
| Map ctor seeds First = Last = Robot+0x2C | 0x00681384 `ldr r3,[r2,#0x2c]`; 0x00681390 `strd r3,r3,[sp,#8]`; 0x00681396 QuadTree ctor |
| Insert copies incoming Last before the override decision | 0x00686D38 `ldr r0,[r0,#0xc]`; 0x00686D3A `str r0,[r1,#0xc]` |
| Planner import gate | 0x0068193x getter via vtable +0x38 (0x004FD50E); 0x004FD512 cached +0x104; `cmp; blo` import; `cmp r4,#0; beq` import; else skip. 0x004FD5CE caches the sampled value |

Two stack differences are confirmed by reading the stack:

1. `MemoryMap` seeds its root data with 0, where the engine uses the last image timestamp.
2. `LatticePlanner.ImportBlockWorldObstacles` (`:775`) rebuilds on every call, with no +0x104 cache or gate.

Both go to B-M13-30.
