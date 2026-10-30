# REACT-UNEXPECTED-MOVEMENT-1: BehaviorReactToUnexpectedMovement, one whole behaviour (read, 2026-10-29)

**Read.** cozmo-extractor, in a Claude-run job; read-only.
- Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so` 3.4.0-1204.
- RUM = BehaviorReactToUnexpectedMovement, class range 0x00609A58..0x00609C76.

**Checked by Claude (manager) against the .so.** Every row below agrees.
- InitInternal 0x00609B06..0x00609B4E:
  - severe need [[robot+0x264]+0x30]+0x14;
  - side +0x11C, with `cmp r7,#2` / `moveq r2,#4`;
  - trigger 0x1AC, 0x1AE when need == 0, 0x1AD when need == 1;
  - args (1, 1, lock, 0x42700000, 0) to TriggerLiftSafeAnimationAction.
- AlwaysHandle 0x00609BF8..0x00609C0A: Get_UnexpectedMovement byte +5 → +0x11C.
- The +0x28 slot 0x00609C1A returns 1, and StopInternal 0x00609C1E is `bx lr`.
- The completion lambda 0x00609C5A: `movs r1,#0x25; movs r2,#1` → BehaviorObjectiveAchieved.

## Path table

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| D1 | detector entry | MovementComponent::Update tail-calls CheckForUnexpectedMovement on every synced RobotState. | 0x63E30A..0x63E392 | M10-002 | EXACT_SOURCE (M10) |
| D2 | detector gates | The order:<br>1. not physical → return;<br>2. if tag robot+0x248 ≠ 0, BODY must be locked;<br>3. picking or placing → return;<br>4. a status mask 0x5008 → reset and return. | 0x0063E3B4..0x0063E426 | M10-002, M10-006 | EXACT_SOURCE |
| D3 | detector rules | Wheel/gyro accumulation (0.174533, 10, 20.0, 0.2, wheelbase 46.0). | M10 B6..B10 | M10-002 | EXACT_SOURCE (M10) |
| D4 | fire | count > 10; type 2 at 0x63E532, 0 at 0x63E59A. | 0x0063E5B2..0x0063E5DA | M10-002 | EXACT_SOURCE |
| D5 | rewind gate | If IsReactionTriggerEnabled(0x14) is false: no rewind or obstacle, side 0. | 0x0063E604..0x0063E610 | M10-007 | EXACT_SOURCE |
| D6 | side, rewind, obstacle | Side 4 RIGHT, 1 FRONT, 3 LEFT, 2 BACK, 0 on failure; SetNewPose; AddCollisionObstacle. | M10 B13..B16 | M10-007 | EXACT_SOURCE (M10) |
| D7 | broadcast | Every fire sends E2G UnexpectedMovement {ts, type, side} through Robot::Broadcast, then resets. | 0x0063E932..0x0063E962 | M10-007 | EXACT_SOURCE |
| D8 | message | CLAD {u32 timestamp, u8 movementType, u8 movementSide}, size 6; E2G tag 0x3C. | Pack 0x0071251E..0x00712560; Set_ 0x007241EA..0x00724212 | NEW | EXACT_SOURCE |
| D9 | delivery | To the app first, then synchronously to in-engine subscribers. | interface | M11-071 | shared |
| S1 | strategy | Trigger 20: Generic, tag set {0x3C}, no filter. | 0x0060D96A..0x0060DA06 | M10-003 | EXACT_SOURCE |
| S2 | strategy params | shouldResumeLast true; no cooldown or timeout. | reactionTrigger_behavior_map.json:201-208 | M7-002, M10-003, M10-008 | EXACT_SOURCE |
| S3 | strategy logic | Latch on tag 60. It cannot retrigger while RUM is the current reaction; it can interrupt other reactions. | M10 C5, C13, C14 | M10-003, M10-004 | EXACT_SOURCE (M10) |
| S4 | manager walk | After the first action; trigger order; lock tables; StopAllMotors; park when shouldResumeLast. | M10 C3, C4, C7, C9 | M10-004, M10-008, M7-014 | shared |
| F1 | factory | Class 0x4E: new(0x120). The config has no extra keys. | 0x0059D332..0x0059D34C; reactions/reactToUnexpectedMovement.json | M7-018 | EXACT_SOURCE |
| C1 | ctor | IBehavior ctor; +0x11C side = 0; vptr 0x0102CB00; SubscribeToTags {0x3C}. No LoadConfig. | 0x00609A58..0x00609A9A | NEW | EXACT_SOURCE |
| V1 | vtable | 0x0102CAF8. Overrides: +0x28, Init, IsRunnableInternal, Stop (empty), AlwaysHandle +0x5C. Base: Update, +0x20, +0x24, Resume, the handlers. | relocations 0x0102CB00..0x0102CB8C | NEW | EXACT_SOURCE |
| R1 | +0x20 | Base 0x5BF04C returns 0: **not runnable off treads**. | 0x005BD864..0x005BD874 | M8-001 | EXACT_SOURCE |
| R2 | +0x24 | Base 0x59EC12 returns 0: **not runnable while robot+0x34A**. | 0x005BD876..0x005BD886 | M8-001 | EXACT_SOURCE |
| R3 | +0x28 | 0x609C1A returns 1: **runnable while carrying**. | 0x00609C1A | NEW | EXACT_SOURCE |
| R4 | other base gates | Running path; now >= +0x118 (set only by TooManyResumes, P2). | 0x005BD778..0x005BD8CA | M8-001 | shared |
| R5 | IsRunnableInternal | Always true. | 0x00609ABC | M7 inventory :843 | EXACT_SOURCE |
| H1 | event dispatch | HandleEvent always calls AlwaysHandle, whether or not RUM is running. | 0x005BF268..0x005BF290 | M8-001 | EXACT_SOURCE |
| H2 | AlwaysHandle | **+0x11C = movementSide** (byte +5) of the last UnexpectedMovement received. | 0x00609BF8..0x00609C0A | NEW | EXACT_SOURCE |
| H3 | ordering | H2 runs inside the broadcast, before the manager switches, so Init sees the triggering broadcast's side. | D9 + M10 C3 | M11-071, M10-004 | shared |
| I1 | emotion | TriggerEmotionEvent("ReactToUnexpectedMovement", MoodManager time); shipped Confident −0.2. | 0x00609ACC..0x00609AF4 | M7-020 | EXACT_SOURCE call |
| I2 | severe need | [[robot+0x264]+0x30]+0x14 (selected severe need). | 0x00609B06..0x00609B10 | M15-072 | EXACT_SOURCE |
| I3 | trigger | Repair (0) → 0x1AE _Severe_Repair; Energy (1) → 0x1AD _Severe_Energy; otherwise 0x1AC ReactToUnexpectedMovement. | 0x00609B30..0x00609B44 | NEW | EXACT_SOURCE |
| I4 | track lock | tracksToLock = 4 (BODY) iff side == 2 (BACK), else 0. | 0x00609B1E..0x00609B22 | NEW | EXACT_SOURCE |
| I5 | action | TriggerLiftSafeAnimationAction(robot, trigger, 1, interruptRunning true, lock, 60.0f, strictCooldown false). | 0x00609B12..0x00609B4E | M8-005, M8-007, M8-020 | EXACT_SOURCE call |
| I6 | StartActing | The void() overload; Init returns 0. | 0x00609B52..0x00609B92 | M8-001 | EXACT_SOURCE |
| K1 | completion | The wrapper ignores the ActionResult; it runs only if still running with a callback. | 0x005BDFE0..0x005BE02A; 0x005BF790 | M8-001 | EXACT_SOURCE |
| K2 | objective | **BehaviorObjectiveAchieved(0x25 ReactedToUnexpectedMovement, true) on any result.** | 0x00609C5A..0x00609C60 | NEW | EXACT_SOURCE |
| K3 | objective body | E2G BehaviorObjectiveAchieved (tag 0x8B), info log, DAS. | 0x005BE228..0x005BE2E0 | M7-019 | call EXACT; rest RECOVERABLE_GAP |
| U1 | Update | Base: ends when the action ends. | vtable +0xC | M8-001 | shared |
| T1 | Stop | Nothing. | 0x00609C1E | NEW | EXACT_SOURCE |
| P1 | Resume | Base: IsRunnableBase && Internal, then +0xA1 = 1 and Init again (emotion and animation again). | 0x005BDA94..0x005BDAC8 | M8-001 | EXACT_SOURCE |
| P2 | parked behaviour | From the second resume with trigger 0x14: +0x118 = now + 15, TooManyResumesCliffOrMovement, refused. | 0x005BCF16..0x005BCF7C | M8-001, M10-008 | EXACT_SOURCE |
| X1 | absent | No lock, idle, RNG, other E2G or DAS in the RUM bodies. | bodies | NEW | EXACT_SOURCE (absence) |

## Observable result

**Robot**
- When it triggers: motors stop, then the lift-safe reaction clip plays once with a 60 s timeout.
  - The clip is ag_reacttostop, or the severe Repair/Energy variant.
  - The action locks BODY when the side was BACK.
  - It locks LIFT while carrying on treads.
- The parked behaviour then resumes, subject to P2.

**C# API**
- E2G UnexpectedMovement on every fire.
- BehaviorObjectiveAchieved(ReactedToUnexpectedMovement) when the action ends, whatever the result.

**State**
- Confident −0.2 on every Init or resume.
- +0x11C keeps the last side.

**Not runnable** off treads or while robot+0x34A. **Runnable** while carrying.

## C# stack before the build (facts)

The class is `OffTreadsBehaviors.cs:494-518` `ReactToUnexpectedMovementBehavior`, with no fidelity tag.

| Area | Stack now | Engine |
|---|---|---|
| Side | Read from `Sensors.UnexpectedMovement.Last?.Side` | AlwaysHandle field (H2) |
| Trigger | Always 0x1AC | Severe variants missing (I3) |
| BODY lock | On BACK | Matches I4 |
| Action | PlayTrigger, not lift-safe, no 60 s timeout | Lift-safe, 60 s timeout (I5) |
| Objective | Only logged | Missing E2G (K2/K3) |
| Gates +0x20/+0x24 | Not overridden: base returns true with MISSING, so it runs off treads | Return 0 (R1/R2) |
| Gate +0x28 | True, but with MISSING | True (R3) |

These already match: the strategy (`ReactionStrategies.cs:471-482`), the delivery order (`Sensors.cs:1051`) and the
P2 counter (`SteppedBehavior.cs:522-534`).

The extra C# API fields in `UnexpectedMovementReport` (`UnexpectedMovement.cs:30-39`: average speeds, count, start
timestamp) are not in the 6-byte engine message. The manager has to decide on them.

## Contradicted
None.

## Open
1. **K3:** the BehaviorObjectiveAchieved body past 0x005BE2E0 (shared with M7-019).
2. **R2:** the name of robot+0x34A.
3. **I1:** MoodManager time vs BaseStationTimer (M7-020).
4. **I5:** interruptRunning semantics (M8-020).
5. **P1:** whether the stack base Resume re-runs OnStart.
6. **The extra API report fields:** a manager decision.
