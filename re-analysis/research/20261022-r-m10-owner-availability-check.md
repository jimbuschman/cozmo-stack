# Independent review: R-M10-OWNER-AVAILABILITY (row 45, 2026-10-22)

**Claude spot-check pending** (redo with disassembled proof)

Reviewed answer: `research/20261021-m10-consequence-owner-availability.md`. The answer is mostly a C# host-ownership / recipient-availability composition (not native arithmetic); its few instruction citations are the classifier consequence orders already scoped. I re-read them: 0x005350AA..0x005350C2 and 0x00535186..0x0053519C (FallingStopped needs/trigger); 0x00511FF0..0x0051208E and 0x00512088..0x005121F8 (already verified in row-27 redo) for the consequence chain context.

## Build-relevant claims, verified

### 1. FallingStopped (HandleFallingStopped) intensity-gate at 0x005350AA..C2 (needs 17 before DAS/intensity report)

Disassembly 0x00535041 (HandleFallingStopped) region at 0x005350A0..C6:
```
0x005350a0  itt      ne
0x005350a2  addne    r1, r1, r2
0x005350a4  strne    r1, [sp, #0x34]
0x005350a6  blx      #0x4a40cc      ; cleanup (string/span dtor)
0x005350aa  vldr     s0, [pc, #0x1fc]   ; s0 = 1000.0 (literal at 0x005352AC, f32 0x447A0000)
0x005350ae  vldr     s16, [r5, #8]     ; s16 = FallingStopped intensity
0x005350b2  vcmpe.f32 s16, s0
0x005350b6  vmrs     apsr_nzcv, fpscr
0x005350ba  ble      #0x5350ca         ; if intensity <= 1000 skip needs/DAS/report
0x005350bc  ldr      r0, [r4]          ; robot
0x005350be  movs     r1, #0x11         ; 17 = Needs.Fall
0x005350c0  ldr      r0, [r0, #0x34]   ; robot+0x34 (Needs ptr)
0x005350c2  blx      #0x4aa18c         ; Needs::RegisterNeedsActionCompleted(17)
... (then DAS "FallStopped"/RobotCliffEventFinished-equivalent and duration/intensity report; 0x00535186..19C)
```
**Confirmed.** Intensity strictly greater than 1000 gates needs 17 (RegisterNeedsActionCompleted(17)) before the DAS and duration/intensity report downstream. Matches claim "intensity strictly > 1000 triggers needs17, then DAS, then duration/intensity report at 0x005350AA-50C2 and 0x00535186-19C".

(DAS at 0x005350??.. and the report payoad 0x00535186..19C are inside the same 648-byte HandleFallingStopped function and occur after the needs registration; not instruction-quoted line-by-line here — the answer cites 0x00535186..19C. Treat the downstream payload as "consistent with" the recorded claim; flagging for Claude as not fully instruction-verified, though the branch structure supports it.)

### 2. Classifier consequence ordering (0x00511FF0..208E and 0x00512088..21F8) — already instruction-verified in the row-27 redo

0x00511FF0..203C (precommit: Robot.ts stamp, DAS "FallingStarted", ActionList.Cancel(-1) before committed-state store) and 0x00512088..21F8 (E2G RobotOffTreadsStateChanged, states 2..6 carried detach, nonzero charger platform clear with pause flag) were quoted verbatim in the row-27 check file. This answer reuses them as the native boundary; no new disassembly this pass beyond acknowledging.

## C# host-composition claims (not native instruction claims)

The bulk of this answer (rows "Sensors/classifier", "World query/localization", "Carrying storage", "Classifier detach", "Action cancel", "Pause flag2", "Falling-needs", "Other host Manipulation callbacks") describe CozmoRobot construction (`CozmoRobot.cs`:300 / 409-412 / `VisionSystem.cs`:104-105), ManipulationSystem construction (`ManipTool.cs`, `FreeplayTool.cs`, `ManipulationSystem.cs`:constructor/Dispose), EngineRobot recreation and CarryingOwnerConstructed (`CozmoRobot.cs`:330), DockingSystem captures, ActionListCancel event subscriber absence, FreeplayStack.Create pause binding, NeedsManager created during Engine initialization (before RobotManager.Init), etc. These are authority 1/2 (engine/app source) and are properly scoped with explicit UNKNOWN reserves ("Whether public early Vision disposal is supported", "Whether Manipulation ordinary routes need it", "replacement unsafe on this field"). They are not native-instruction claims that can be verified by disassembly of `libcozmoEngine.so`; the few native citations are the (already-verified) consequence orders (point above) plus NeedsManager's existence during Engine init, which corresponds to `libcozmoEngine.so` RobotManager/Needs initialization (not separately instruction-traced this pass; the C# `CozmoEngine.cs` constructs the backend and `EngineRobot.cs` adds robot; native NeedsManager construction occurs inside the engine's RobotManager::Init, which I did not isolate this pass).

## Build-relevant claims NOT reviewed (budget)
- The native NeedsManager construction moment inside RobotManager::Init (the C# Engine construction path calls into the engine) not individually instruction-traced.
- The C# host-ownership composition is documented and internally consistent with the dispatch sites, but no disassembly proof; it is authority 2 (app source) and already carries explicit UNKNOWNs.
- The answer's `Native owners are source-backed engine/Robot components, not Freeplay user-handle lifetime` assertion is itself a source/engine (authority 1/2) claim, not a native instruction claim verifiable by this pass. I did not find a contradiction, but I also did not compile a native construction-order witness beyond the cited consequence orders.
- No code change requested.

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review (redo) of R-M10-OWNER-AVAILABILITY (row 45).
2. Engine instructions re-read myself:
   - FallingStopped gate 0x00535041..5C2 (needs trigger when intensity > 1000): `0x005350AA vldr s0,[pc,#0x1fc]` (1000.0 = 0x447A0000 at 0x005352AC); `0x005350AE vldr s16,[r5,#8]`; `0x005350B2 vcmpe.f32 s16,s0`; `0x005350B6 vmrs apsr_nzcv,fpscr`; `0x005350BA ble #0x5350CA`; `0x005350BC ldr r0,[r4]`; `0x005350BE movs r1,#0x11 (17)`; `0x005350C0 ldr r0,[r0,#0x34]`; `0x005350C2 blx #0x4AA18C` (`Needs::RegisterNeedsActionCompleted`). The 0x00535186..19C DAS/report tail not individually instruction-quoted this pass (flagged).
   - Consequence chain (already verified in row-27 file, not re-disassembled): 0x00511FF0..208E precommit cancel (Robot.ts stamp + FallingStarted DAS + ActionList.Cancel(-1) before committed byte store) and 0x00512088..21F8 postcommit (RobotOffTreadsStateChanged E2G, detach for states 2..6, charger-platform clear + pause flag only on nonzero candidate).
   - Native NeedsManager construction moment inside RobotManager::Init not instruction-isolated this pass; only cited as a current-ownership record (Pick AN CONST via duplicate read-check).
3. Hooks/seams added: none.
4. Anything added that job didn't ask for: none.
5. Tests: none.
6. Focused tests: N/A.
7. Records: none settled. M10-008 (and the linked M10-001 F2 detach hook) remain IMPLEMENTATION_GAP. The answer's F2 claim (missing detach recipient even with ManipulationSystem constructed) is not directly verified by a native instruction this pass; it is recorded as a C# composition observation with explicit unbound seam. No new exclusion created.

No new research queued. No code edited. No record settled. (Row 45 is the final row of the M10 group; only this row completed this session — the user's "2 rows" target cannot be met because it is the last pending redo row; rows 27 and 35 were completed in prior passes this period.)