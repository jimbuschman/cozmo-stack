# M10 derived-state inventory (off-treads, unexpected movement, reaction strategies)

**State:** approved by the manager on 2026-09-25 under the operator's standing authorisation, and frozen with `python re-analysis/tools/fidelity.py --approve M10-derived`. The one forced choice is recorded as policy M10-013.

## Where this comes from

- **Source:** `libcozmoEngine.so` 3.4.0-1204. Unity C# (authority 2) and the shipped behaviour-system configs (authority 3) were used as supporting evidence.
- **Read-only extractor passes:**
  - **M10 pass** (Appendix A): A1..A17 (CheckAndUpdateTreadsState), B1..B18 (CheckForUnexpectedMovement), C1..C17 (falling events, CheckReactionTriggerStrategies, strategy factory and classes).
  - **Gap pass 1** (Appendix B), sections 1..8:
    - 1. the Cliff filter;
    - 2. IBehavior+0xA1 (is running);
    - 3. ActionList::Cancel(−1);
    - 4. IsReactionTriggerEnabled and disable locks;
    - 5. IMU initial values;
    - 6. BaseStationTimer;
    - 7. GetSizeByType;
    - 8. the other strategies.
  - **Gap pass 2** (Appendix C), sections 1..7:
    - 1. PlacedOnCharger;
    - 2. the CubeMoved enable/force and its trigger-8 gate;
    - 3. FacePositionUpdated;
    - 4. ObjectPositionUpdated and the position-update base;
    - 5. the whiteboard id writers;
    - 6. the Frustration mood field (Confident);
    - 7. raw IMU initial values.
- **Interfaces used and not redone:**
  - `M4-control.md`: C3 UFRS order, C8 P6 platform flag, C9 F1..F5 treads Delocalize and +0x2C6, MA1 direct-drive flags, MA21 head calibration.
  - `M5-animation.md`: live idle, keep-alive.
  - `M2-protocol.md`: RS rows, status bits, R-P5.
  - `M1-transport.md`: CD8, the engine tick.
- **The C# was never evidence.**

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M10-001 | IMPLEMENTATION_GAP | The off-treads classifier CheckAndUpdateTreadsState. <br>- **Gate:** runs only when the head is calibrated. <br>- **Inputs:** pitch, filtered accel Y (+0x384, initially 0), IS_FALLING, IS_PICKED_UP, the physical flag (on-back centre 1.30027 physical, 1.68250 sim). <br>- **Branches and thresholds:** side \|\|ay\|−9800\| < 3000; face pitch > 1.91986 or < −1.39626; back \|d\| ≤ 0.261799; level \|pitch\| ≤ 0.785398. <br>- **Debounce:** candidate times, commit when t+250 ≤ now. <br>- **Consequences:** Falling DAS events with ActionList::Cancel(−1) (a wildcard: cancel the current action, delete the queued ones); the RobotOffTreadsStateChanged broadcast; on OnTreads, the BlockWorld localizable check and +0x2C4/+0x2B8; on states 2..6, SetCarriedObjectAsUnattached(true); on anything but OnTreads, SetOnChargerPlatform(false); the OffTreads freeplay pause flag. <br>- Returns 1 on commit, which the M4 C9 treads Delocalize uses. | A1..A16, gap1 3a..3e, 5a |
| M10-002 | IMPLEMENTATION_GAP | Unexpected-movement detector CheckForUnexpectedMovement (tail of MovementComponent::Update, every synced state). <br>- **Gates:** physical; the body lock is required only while AnimationState.tag ≠ 0; not picking/placing; the status mask 0x5008 resets it. <br>- **Rules:** \|l\|+\|r\| < 20 decays. Quiet gyro (< 0.174533): opposite signs, or \|0.5·\|(r−l)/46\|−\|gz\|\| > 0.2, adds 1 with type 0. Active gyro with the opposite sign adds 2 with type 2; the same sign decays. <br>- **Timing:** the first increment stamps the robot timestamp; it fires at count > 10. | B1..B11 |
| M10-003 | IMPLEMENTATION_GAP | The reaction-strategy factory rules per trigger and the strategy classes. <br>- **Factory:** Cliff {34, 52}, with the filter "enabled and not already the running reaction"; MotorCalibration; Falling (WithTimeout 3000); PickedUp; ReturnedToTreads; OnBack/Face/Side; UnexpectedMovement. <br>- **Generic:** latch, WantsToRun, timeout. <br>- **Shaken:** robot+0x37C > 16000. <br>- **PlacedOnSlope, Frustration** (mood Confident < max). <br>- **PlacedOnCharger:** a 20 s deadline from the first call, plus a ChargerEvent latch. <br>- **Sparked, NoPreDockPoses** (whiteboard id). <br>- **FistBump:** objective params. <br>- **Hiccup:** as read; needs and progression inputs are interfaces. <br>- **Pet:** 60 s, more than 2 observations. <br>- **CubeMoved:** gated on trigger 8 being enabled; 50 mm, 1000 ms, 0.785398 visibility; enable/force hooks. <br>- **FacePositionUpdated:** named-first, 300/400 mm, 4 s cooldown. <br>- **ObjectPositionUpdated:** the base with 80 mm / 45° / 600 000 ms, families {LightCube, Block}, carried or docking objects counted as reacted. <br>- Per-class flags (resumeLast, interruptOther, interruptSelf) as tabulated. | C12..C17, gap1 1, 8, gap2 1..6 |
| M10-004 | IMPLEMENTATION_GAP | CheckReactionTriggerStrategies. <br>- **Gate:** sticky: nothing runs until the first action is queued, or the "sdk" lock is removed. <br>- **Order:** triggers in ascending map order, with disable-locked ones skipped. <br>- **Predicates:** CanInterruptOther/Self by current trigger; ShouldTriggerBehavior with the force path. <br>- **On a trigger:** StopAllMotors plus the track-unlock rule; the no-break loop with the multi-switch log. <br>- **Enable state:** IsReactionTriggerEnabled; locks added and removed with EnabledStateChanged per strategy; the game messages with stopCurrent = 1. All 21 triggers are mapped and enabled at startup. | C3..C8, gap1 4a..4j |
| M10-005 | IMPLEMENTATION_GAP | The off-treads debounce clock is BaseStationTimer ms. That is engine tick-start time since the run began: u32(seconds·1000), updated once per 60 ms tick, the same value within a tick. | A2, gap1 6a..6d |
| M10-006 | IMPLEMENTATION_GAP | The unexpected-movement body-lock gate applies only while robot+0x248 (AnimationState.tag) ≠ 0; the other gates are in B2, B4 and B5. | B2..B5 |
| M10-007 | IMPLEMENTATION_GAP | The unexpected-movement response. <br>- **Gate:** IsReactionTriggerEnabled(20). <br>- **History:** ComputeStateAt(first timestamp); an origin mismatch or failure gives side UNKNOWN. <br>- **Side and obstacle:** chosen from the wheel means; obstacle d = 25 (CollisionObstacle 20 + 5): FRONT 47.1, BACK −80.9, LEFT +52.1, RIGHT −52.1. <br>- **Rewind:** SetNewPose(historical pose with the current rotation), then AbsoluteLocalizationUpdate via M4 C9. <br>- **Obstacle:** AddCollisionObstacle, parented to the new pose. <br>- **Always:** the UnexpectedMovement broadcast, then reset. kCreateUnexpectedMovementObstacles has no reader. | B12..B18, gap1 7a..7b |
| M10-008 | IMPLEMENTATION_GAP | Resume after a reaction. <br>- **SwitchToReactionTrigger:** keeps an existing parked resume, or parks the running behaviour (even a reaction) when shouldResumeLast (Generic default 1). <br>- **Unlock rule:** unlocks when tracks are locked and (no direct drive, or MC+0xD4). <br>- **Head and lift:** restored from SetDefaultHeadAndLiftState's values as CompoundActionParallel{MoveHead tol 0.0349066, MoveLift}. | C7, C9..C11 |
| M10-009 | IMPLEMENTATION_GAP | Nothing in this build ever raises the obstacle-detected flag. The predicate is at 0x6143CA and the constructor store at 0x569A80; the global absence is carried over from the existing record. | existing evidence |
| M10-010 | IMPLEMENTATION_GAP | The physical flag robot+0x14 is 0 until FirmwareVersion arrives; then it is set to json["sim"].isNull(). While not physical: the unexpected-movement check does nothing, the on-back centre is the sim value, and the tap filter does not queue (M4 CD10e). | A4, B2 |
| M10-011 | IMPLEMENTATION_GAP | Falling events. HandleFallingStarted broadcasts FallingStarted{ts}. HandleFallingStopped registers NeedsAction 17 when intensity > 1000, emits DAS, and broadcasts FallingStopped{duration, intensity}. | C1, C2 |
| M10-012 | IMPLEMENTATION_GAP | The IMU filter state +0x378..+0x38F starts at 0; the raw accel and gyro are written only by UFRS. | gap1 5a, gap2 7a |
| M10-013 | IMPLEMENTATION_GAP (a forced policy, to build) | The raw accel/gyro (+0x360..+0x374) before the first RobotState. The engine never initialises them, so they hold heap contents. This stack uses 0 (MD1). | gap2 7b |

## Decisions (the manager's, recorded for audit)

- **MD1 (M10-013).** Heap contents cannot be reproduced, so this stack uses 0 before the first state. The window before the first RobotState is short: PlacedOnSlope's gyro test before any state.
- **MD2. Interfaces.**
  - The strategy predicates live here. Their inputs belong to other layers:
    - needs, progression, feature gates and the whiteboard (M15/M7);
    - MoodManager emotions (M7);
    - BlockWorld and FaceWorld (M11/M14);
    - the behaviours' IsRunnable (M8).
  - The whiteboard id writers (gap2 5b) are M7/M12 behaviour helpers.
- **MD3. The sticky reaction gate** (M10-004) is reproduced as the engine has it. The first queued action opens it; the "sdk" lock removal also opens it, and its sender is external (BLOCKED_EXTERNAL for who sends it, exact on the engine side).
- **MD4. M10-005 is exact, not equivalent.** The stack uses the engine-tick BaseStationTimer.

## Existing record evidence found too weak or contradicted

- **M10-006:** contradicted; the body-lock gate applies only while an animation tag is set.
- **M10-008:** contradicted on the unlock rule, the parking and the head/lift source.
- **M10-003:** the Cliff filter was incomplete, and CubeMoved's gate is trigger 8.
- **M10-004:** missed the sticky gate, the locks, map order and the no-break loop.
- **M10-007:** missed the broadcast on every path, the gate, and the value 25.
- **M10-001 and M10-002:** constants confirmed; the consequences and conditions were missing.
- **M10-005:** it was EQUIVALENT on the local clock, and is now settled as BaseStationTimer.

## Appendix A: M10 pass, extractor report

I worked read-only. Scratch is `C:\Windows\TEMP\claude\...\b4cb80cd-...\scratchpad\extract\M10\`. It holds tools copied from M5 (`da.py`, `xref.py`, `scan.py`, `veneer.py`, `vt.py`, `bl.pkl`) and new ones (`vscan.py` scans VLDR/VSTR offsets, `dref.py` finds references to a data address, `plt.py`, `strtab.py`). The dumps are `cuts.txt`, `ufrs.txt`, `cfum.txt`, `crts.txt`, `fact.txt`, `gen.txt`, `ctor.txt`, `bmupd.txt`, `ttr.txt` and `irts.txt`.

**Interfaces used, not redone:**
- M4 C3: UFRS calls CUTS at 0x512A72, before the origin check.
- M4 C8/P6: SetOnChargerPlatform(false) at 0x512192.
- M4 C9/F1..F5: +0x2C6 leads to SendAbsLocalizationUpdate; the treads-path Delocalize is at 0x512A62..0x512BAA.
- M4 MA1: MC+0xB8..0xBA are the direct-drive flags.
- M4 MA21: +0x314 is the head-calibrated gate.
- M2 RS4/RS8/RS9, the status-bit table, and R-P5 (AnimationState).

The C# was read only to choose scope.

#### A. CheckAndUpdateTreadsState (0x511E00, size 0x578)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| A1 | If head-calibrated (robot+0x314) is 0, it returns 0 and nothing changes. | 0x511E1C `ldrb.w r0,[sb,#0x314]`; 0x511E22 `beq.w 0x512206` | M10-001 | EXACT_SOURCE |
| A2 | The clock is `now = BaseStationTimer::GetCurrentTimeStamp()`, which is `u32(seconds·1000)`. The debounce time +0x358 is stamped on this clock. The robot timestamp robot+0x2C (state+0, RS1) is used only for the Falling DAS events and for +0x35C. Where BaseStationTimer is advanced was not read. | 0x511E2A..0x511E36; 0x84BCC0..0x84BCD0 (`vmul d1,d0(1000)`; `vcvt.u32`) | M10-005 | EXACT_SOURCE for the clock identity. Whether it is "equivalent" is the manager's call. |
| A3 | **Inputs:**<br>- pitch = Radians(robot+0x304) (RS4);<br>- s24 = filtered accel **Y**, robot+0x384 (RS8: 0.9·old + 0.1·a.y, 0x512A4A..0x512A5A);<br>- status msg+0x4C: IS_FALLING 0x20 (0x511EC4) and IS_PICKED_UP 0x8 (0x511F04);<br>- robot+0x14, the physical flag (0x511E48).<br>Gyro and filtered \|a\| (+0x37C) are not used. | 0x511E26, 0x511E32..0x511E60 | M10-001 | EXACT_SOURCE |
| A4 | **robot+0x14 (physical)** is 0 in the ctor (0x50FC36). Only Robot::SetPhysicalRobot writes it (0x51397A `strb r4,[r5,#0x14]`). Its only caller is HandleFirmwareVersion 0x536980, with arg = `json["sim"].isNull()` (strings "sim", "RobotIsPhysical" at 0x536A4C/0x536A54). **Before FirmwareVersion arrives:**<br>- the on-back centre is the sim value;<br>- CheckForUnexpectedMovement does nothing (B2). | as cited; xref SetPhysicalRobot | NEW | EXACT_SOURCE |
| A5 | **Derived terms:**<br>- sideDev = \|\|s24\| − 9800\| (literal 0x5121FC = −9800);<br>- onSide = sideDev < 3000 (0x512200);<br>- ySign = s24 > 0. The `it gt` at 0x511EC8 uses the flags of `vcmpe s24,#0` (0x511EB0/0x511EB8), not s16.<br>- d = (double)pitch − (phys ? 1.30027 : 1.68250) (f64 at 0x5122A8 / 0x5122A0, selected at 0x511E80..0x511E8C);<br>- onBack = \|d\| ≤ 0.261799 (0x5122B0);<br>- pitchExtreme = pitch > Rad(1.91986) \|\| pitch < Rad(−1.39626). Anki::operator> 0x84CC90 is diff > 0 && !IsNear(1e-5).<br>- level = \|pitch\| ≤ 0.785398 (0x5122BC). | 0x511E66..0x511F04; 0x512228..0x512238 | M10-001 | EXACT_SOURCE |
| A6 | **Branches** (cand = +0x356, t = +0x358):<br>- **IS_FALLING:** if cand ≠ 6, then t = now−250 and cand = 6. Then tail T.<br>- **Else onSide:** keep cand if it is 3 or 4. Otherwise cand = ySign ? 4 (OnRightSide) : 3 (OnLeftSide), with t = now. No tail.<br>- **Else pitchExtreme:** if cand ≠ 5, then cand = 5 (OnFace) and t = now. No tail.<br>- **Else onBack:** if cand ≠ 2, then cand = 2 (OnBack) and t = now+750. No tail.<br>- **Else:**<br>&nbsp;&nbsp;- level && cand ≥ 2: t = now, cand = 1;<br>&nbsp;&nbsp;- else pickedUp && cand == 0: t = now−250, cand = 1;<br>&nbsp;&nbsp;- else keep. Then tail T.<br>- **Tail T:** if none of {pickedUp, onSide, onBack, pitchExtreme, cand == 0} holds, then cand = 0 and t = now−250. | 0x511F36..0x511F44; 0x511F0E..0x511F34; 0x511F9A..0x511FAC; 0x511FAE..0x511FCC (`addw r0,fp,#0x2ee`); 0x512228..0x51225C; T = 0x511F48..0x511F98 | M10-001 | EXACT_SOURCE |
| A7 | **Commit:**<br>- if t+250 > now (u32), no change;<br>- else if +0x355 == cand, no change;<br>- else +0x355 := +0x356.<br>Effective delays: OnBack 1000 ms; side, face and level-InAir 250 ms; Falling, tail-OnTreads and pickup-InAir immediate. | 0x511FD0..0x511FEE; 0x512088..0x51208E | M10-001 | EXACT_SOURCE |
| A8 | **Entering Falling:**<br>- +0x35C = robot+0x2C;<br>- DAS sEventF "Robot.CheckAndUpdateTreadsState.FallingStarted" "t=%dms";<br>- **ActionList(robot+0x250)::Cancel(−1).** | 0x511FF0..0x51203C | NEW | EXACT_SOURCE. What Cancel's −1 means was not read. |
| A9 | **Leaving Falling:**<br>- DAS "…FallingStopped" "t=%dms, duration=%dms" with (ts, ts − +0x35C);<br>- +0x35C = 0.<br>These are DAS events only. The game FallingStarted/FallingStopped messages come from the robot (C1, C2). | 0x51203E..0x512084 | NEW | EXACT_SOURCE |
| A10 | **On commit:**<br>- broadcast RobotOffTreadsStateChanged{treadsState = new} (ExternalInterface vslot +0x18);<br>- log "Robot.OfftreadsState.TreadStateChanged". | 0x512092..0x5120D0 | M10-001 (events) | EXACT_SOURCE |
| A11 | **New == OnTreads:**<br>- if !BlockWorld(robot+0x34)::AnyRemainingLocalizableObjects(): log "…NoMoreRemainingLocalizableObjects";<br>- robot+0x2C4 = 1, robot+0x2B8 = −1. | 0x512112..0x512184 | NEW (M11 interface) | EXACT_SOURCE for the stores |
| A12 | **New in 2..6:** if CarryingComponent(robot+0x284)+8 ≠ −1, call SetCarriedObjectAsUnattached(true) (PLT 0x4A7B7C). | 0x512100..0x51210C | NEW (M12 interface) | EXACT_SOURCE |
| A13 | **New ≠ OnTreads:** SetOnChargerPlatform(false). | 0x512188..0x512192 | M4 P6 | interface |
| A14 | **Committed:** FreeplayDataTracker(robot+0x264 → +0x2C)::SetFreeplayPauseFlag(new ≠ 0, flag **2 = "OffTreads"**).<br>- SetFreeplayPauseFlag(b, f) is b ? Set : Clear (0x56EEBC).<br>- The flag-name table at 0x1023554 is GameControl, Spark, OffTreads, OnCharger. **This also settles M4 P1's UNKNOWN: flag 3 is "OnCharger".** | 0x5121E2..0x5121F4 | NEW | EXACT_SOURCE |
| A15 | Returns 1 only when committed. UFRS consumes this for the treads-path Delocalize (M4 F5). A Viz SetText also runs on every calibrated call (no robot effect). | 0x5121F8; 0x51219A..0x5121D8 | interface | EXACT_SOURCE |
| A16 | **Writers of +0x355/+0x356/+0x358/+0x35C:** only CUTS, plus the ctor zeroing (0x5100E2..0x5100EA).<br>**Consumers:**<br>- factory lambdas 0x60DDCE/0x60DEB2/0x60DF16/0x60DF7E;<br>- PlacedOnSlope 0x614676;<br>- IBehavior::IsRunnableBase 0x5BD864;<br>- BehaviorManager::Update 0x5A2F8E;<br>- UpdateCliffRunningStats 0x63464A;<br>- Robot::Update 0x513CE2;<br>- GetRobotState 0x518216;<br>- TurnInPlace, EnrollFace and others. | scan.py 0x355,0x356,0x358,0x35C | NEW (interfaces) | EXACT_SOURCE |
| A17 | IMU filter initial values (+0x37C..+0x388 in the Robot ctor). | not read | M10-001 | RECOVERABLE_GAP: read the ctor 0x50FBF0 for those offsets |

#### B. MovementComponent::CheckForUnexpectedMovement (0x63E398, size 0x7A8)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| B1 | MovementComponent::Update (0x63E300) sets MC+9..+0xC from status and then tail-calls CFUM (0x63E392 → veneer → CFUM). Update is called from UFRS 0x512B5C, before the origin check and before the treads Delocalize. | 0x63E30A..0x63E392 | interface | EXACT_SOURCE |
| B2 | If robot+0x14 (physical) is 0, return. | 0x63E3B4..0x63E3B8 | NEW | EXACT_SOURCE |
| B3 | If **robot+0x248 ≠ 0**, the BODY track must be locked or the function returns. The lock test is lock-set size ≠ 0 at MC+0x30+12·2; the layout matches AreAnyTracksLocked 0x63EF88.<br>- **robot+0x248 is AnimationState.tag.** Its writer is 0x53800C `strb.w r0,[r6,#0x248]`, from msg+0xD in the AnimationState handler 0x537FCA (M2 R-P5).<br>- GetRobotState reports it as IS_ANIMATING 0x40, plus IS_ANIMATING_IDLE 0x800 when the value is 0xFF (0x5181C0..0x5181D4).<br>- **With tag 0 the check runs regardless of locks.** | 0x63E3BA..0x63E3DC | M10-006 | EXACT_SOURCE |
| B4 | If [robot+0x280]+4 (IS_PICKING_OR_PLACING mirror, written at 0x512AA0) is set, return with **no reset**. | 0x63E3DE..0x63E3E4 | NEW | EXACT_SOURCE |
| B5 | If status & 0x5008 (IS_PICKED_UP \| IS_ON_CHARGER \| CLIFF_DETECTED): +0x94..+0xA0 = 0, return. | 0x63E406..0x63E426 | NEW | EXACT_SOURCE |
| B6 | **Constants from the ctor:**<br>- +0xA4 = 0.174533 rad/s;<br>- +0xAC = 10 (u8);<br>- +0xB0 = 20.0;<br>- +0xB4 = 0.2;<br>- +0xA8 = 30.0, for which no VLDR or ldr.w reader was found;<br>- wheelbase literal 46.0 at 0x63E7D0.<br>No other writers were found (scan and vscan). | 0x63DAB0..0x63DB00 | M10-002 | EXACT_SOURCE |
| B7 | If \|l\|+\|r\| < 20 (l = msg+0x20, r = msg+0x24): count−− if > 0, then return. | 0x63E428..0x63E49A | NEW | EXACT_SOURCE |
| B8 | **Gyro quiet** (\|gz\| < 0.174533, gz = msg+0x44):<br>- signbit(l) ≠ signbit(r): count +1, type 0;<br>- same sign: if \|0.5·\|(r−l)/46\| − \|gz\|\| > 0.2, count +1, type 0; otherwise no change. | 0x63E49C..0x63E4E6; 0x63E540..0x63E59E; 0x63E686 | NEW | EXACT_SOURCE |
| B9 | **Gyro active:**<br>- signbit(r−l) == signbit(gz): count−− and return;<br>- otherwise count +2, sums += 2l and 2r, type 2. | 0x63E4F0..0x63E53E; 0x63E674..0x63E67E | NEW | EXACT_SOURCE |
| B10 | On the first increment, +0x94 = state.timestamp (robot clock). | 0x63E51C/0x63E58A | NEW | EXACT_SOURCE |
| B11 | Fires when count > 10, with the warning "Unexpected movement detected %s" (the type's name). The type broadcast is only this tick's branch: 0 TURNED_BUT_STOPPED or 2 TURNED_IN_OPPOSITE_DIRECTION. **Type 1 is never produced.** | 0x63E5B2..0x63E5DA; strings 0x63EA70/0x63EAA0 | M10-002 | EXACT_SOURCE |
| B12 | Gate: BehaviorManager::IsReactionTriggerEnabled(20 = UnexpectedMovement). If disabled, there is no rewind and no obstacle, and side = 0 (UNKNOWN). | 0x63E604..0x63E610 → 0x63E680 | NEW | EXACT_SOURCE |
| B13 | RobotStateHistory(robot+0x390)::ComputeStateAt(+0x94, &t, &hist, true).<br>- 0x6000000: info "…PoseHistoryOriginMismatch";<br>- any other non-zero: warning "…PoseHistoryFailure", "Could not get robot pose at t=%u".<br>- In both cases side = UNKNOWN and there is no rewind or obstacle. | 0x63E61C..0x63E672; 0x63E74A..0x63E78E | M10-007 | EXACT_SOURCE |
| B14 | **Side and obstacle offsets** (d = GetSizeByType(0x10)[0] + 5; means L = +0x98/n, R = +0x9C/n; ε = 1e-5):<br>- both > ε: FRONT(1), (d+22.1, 0, 0);<br>- neither: BACK(2), (−55.9−d, 0, 0);<br>- R only: LEFT(3), +π/2, (0, d+27.1, 0);<br>- L only: RIGHT(4), −π/2, (0, −27.1−d, 0).<br>Rotation is set by Pose3d::SetRotation (0x5126BC). Log strings are at 0x63EAC8..0x63EAF0. The GetSizeByType table was **not re-read**. | 0x63E68C..0x63E840 | M10-007 | EXACT_SOURCE for the branches and literals |
| B15 | **Rewind:**<br>1. Copy the hist pose.<br>2. Overwrite its 32-byte Rotation3d with the robot's current rotation (Robot::GetPose, outlined at 0x4EA398; copy loop 0x63E866..0x63E878).<br>3. Robot::SetNewPose (0x63E87E), which is GetWithRespectToRoot (0x4DF628) + SetPose + AddVisionOnlyStateToHistory(robot+0x2C, pose, head +0x2FC, lift +0x300) (0x5126EC..0x51271C). That feeds +0x2C6 → SendAbsLocalizationUpdate (M4 F1..F3).<br>4. A non-zero result logs "Failed to set new pose", and the function continues. | as cited | M10-007 | EXACT_SOURCE |
| B16 | **Obstacle:**<br>1. SetParent(robot pose after SetNewPose) (0x63E8BA..0x63E8C4).<br>2. GetWithRespectToRoot (0x63E8CC).<br>3. Log "Adding obstacle %s robot".<br>4. BlockWorld(robot+0x34)::AddCollisionObstacle (0x63E916). | as cited | M10-007 | EXACT_SOURCE |
| B17 | **On every fire, including the disabled and failed paths:**<br>- broadcast UnexpectedMovement{timestamp = current state.timestamp, movementType, movementSide};<br>- then reset +0x94..+0xA0. | 0x63E932..0x63E962 | NEW | EXACT_SOURCE |
| B18 | **kCreateUnexpectedMovementObstacles (0xC7F620)** is a `.rodata` byte 0x00 (false), at the same address as kDebugTrackLocking (folded constants). **No reader exists:**<br>- no ldr-literal + add pc materialises it;<br>- no GOT entry;<br>- no absolute pointer (the brute-force hit in WhoIsLocking is a false match).<br>The obstacle is therefore gated only by B12 and B13. | dref.py and brute-force scans | NEW | EXACT_SOURCE (absence established by scan) |

#### C. Falling events and reaction strategies (partial)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| C1 | HandleFallingStarted: log, then broadcast game FallingStarted{msg.timestamp}. No time-sync gate and no state change. | 0x534F4C..0x534F9E | NEW | EXACT_SOURCE |
| C2 | **HandleFallingStopped** (fields: ts +0, duration_ms +4, intensity +8, as in M2-013):<br>- if intensity > 1000.0: NeedsManager::RegisterNeedsActionCompleted(17). 17 is "Fall" by Unity ordinal; the engine name was not checked.<br>- DAS "robot.falling_event";<br>- broadcast FallingStopped{duration_ms, impactIntensity}. | 0x5350AA..0x5350C2; 0x535186..0x53519C | NEW | EXACT_SOURCE |
| C3 | **Gate on CheckReactionTriggerStrategies:** manager+0x4C \|= !ActionList.IsEmpty(). If the result is 0, or the map is empty, it returns false and no strategy is consulted.<br>- +0x4C = 0 in the ctor (0x5A08F8).<br>- Its only other writer is RemoveDisableReactionsLock("sdk") (0x5A3A60..0x5A3A8E).<br>- **It is sticky: no reactions until the first action has been queued.** | 0x5A355A..0x5A357A | NEW | EXACT_SOURCE |
| C4 | **Walk order:** std::map<ReactionTrigger, TriggerBehaviorInfo> in ascending trigger order (template at 0x5A1842). Each trigger holds a (strategy, behaviour) vector in JSON order (AddStrategyMapping 0x5A1864). A trigger is skipped while its disable-lock set is non-empty (node+0x28). | 0x5A359A..0x5A35A8, 0x5A37AA..0x5A37CA | M10-004 | EXACT_SOURCE |
| C5 | **Predicates.** cur = manager+0x1C → +0x10 (0x16 = none).<br>- cur none: skip the predicate;<br>- cur == strategy+0x18: vslot +0x10;<br>- otherwise: vslot +0x0C.<br>**Values:**<br>- Generic: +0x0C = byte +0x39 `canInterruptOtherTriggeredBehavior` (default 1; key at 0x60F248..0x60F25A; no map entry sets it). +0x10 = `return 0` (0x60B732).<br>- Shaken and Slope: +0x0C = 1 (0x613072 / 0x612F42), +0x10 = 0.<br>- Frustration: +0x0C = **0** (0x60EF26). | 0x5A35BA..0x5A35DE; vtables 0x102D1E0, 0x102D464, 0x102D424, 0x102D168 | M10-004 | EXACT_SOURCE |
| C6 | ShouldTriggerBehavior: if forced (+0x29), call Internal (+0x24); if that returns false, call SetupForce (+0x20); clear +0x29 and return true. Otherwise return Internal. | 0x60B63A..0x60B6C0 | M10-004 | EXACT_SOURCE |
| C7 | **On a trigger:**<br>1. StopAllMotors.<br>2. If AreAnyTracksLocked(0xFF) && (!(MC+0xB8 \|\| B9 \|\| BA) \|\| MC+0xD4): warn "Some tracks are locked, unlocking them" and CompletelyUnlockAllTracks.<br>MC+0xD4 (and robot+0x2C7) are written by UpdateRobotPropertiesForReaction(b) (0x5A2F54). | 0x5A3610..0x5A3682 | M10-008 | EXACT_SOURCE |
| C8 | SwitchToReactionTrigger; log SwitchingToReaction or FailedToSwitch. **The loop does not break.** A second trigger in the same tick logs "Multiple behaviors switched to in a single basestation tick". It returns true if any switch happened. | 0x5A3686..0x5A37A6 | M10-004 | EXACT_SOURCE |
| C9 | **SwitchToReactionTrigger:**<br>- null behaviour → false.<br>- info = {current = behaviour, trigger = strategy+0x18}.<br>- If vslot +0x08 (shouldResumeLast): resume = the existing resume (manager+0x1C +8) if there is one, **otherwise the running behaviour, reaction or not**.<br>- Otherwise resume = none.<br>- Then SwitchToBehaviorBase(info). | 0x5A25E4..0x5A26DA | M10-008 | EXACT_SOURCE |
| C10 | **vslot +0x08 values:**<br>- Generic: +0x38 `shouldResumeLast`, **default 1** (ctor 0x60F1F8 `movw r0,#0x101; strh`).<br>- If genericStrategyParams is null, the defaults stand (0x60F20E..0x60F214).<br>- Shaken, Slope and Frustration return 0. | 0x60F904; 0x61306E, 0x612F3E, 0x60EF22 | M10-008 | EXACT_SOURCE |
| C11 | **TryToResume head/lift.** manager+8/+0xC are written only by BehaviorManager::SetDefaultHeadAndLiftState:<br>- enable: set both (0x5A1BCC/0x5A1BD0), and move now if the action list is empty;<br>- disable: FLT_MAX (0x5A1D1E..0x5A1D26).<br>It is called from the game message SetDefaultHeadAndLiftState handler (0x5A5042). The restore queues CompoundActionParallel{MoveHeadToAngleAction(tol 0.0349066), MoveLiftToHeightAction}. | 0x5A2BB8..0x5A2C3A | M10-008 | EXACT_SOURCE |
| C12 | **Factory cases** (switch 0x60D618, table 0x60D61C; tag names checked against the engine table 0x1031E20):<br>- **CliffDetected:** {CliffEvent 34, RobotStopped 52}, filter 0x60DC76 (partly read).<br>- **MotorCalibration:** {30}, filter msg+1 && msg+2 (0x60DCFA).<br>- **RobotFalling:** {FallingStarted 58} WithTimeout 3000 (0x60D838 `movw r3,#0xbb8`), filter IsReactionTriggerEnabled(11) (0x60DD6E).<br>- **PickedUp:** +0x355 == 1 (0x60DDCE).<br>- **ReturnedToTreads:** {53}, filter IsReactionTriggerEnabled(14) && state == 0 (0x60DE36..0x60DE56).<br>- **OnBack / OnFace / OnSide:** == 2, == 5, (s−3) < 2.<br>- **UnexpectedMovement:** {60}, no filter.<br>- Trigger byte +0x18 is set when it is still 0x16 (0x60D9FE..0x60DA06). | as cited | M10-003 | EXACT_SOURCE (except the Cliff filter) |
| C13 | **StrategyGeneric:**<br>- AlwaysHandleInternal: if the tag is in the set, latch +0x80 = 1 when there is no filter or the filter returns true. A false filter leaves the latch unchanged.<br>- WantsToRunInternal returns latch \|\| (no events configured && callback(robot)), and clears the latch on every call. | 0x613998..0x6139EC; 0x613948..0x61397E | M10-003 | EXACT_SOURCE |
| C14 | **Generic ShouldTriggerBehaviorInternal:**<br>- runnable = beh+0xA1 \|\| IsRunnable;<br>- timeout: some subscribed tag's last BaseStationTimer ms is > now − timeout. The stamps are taken regardless of the filter (0x60F34C..0x60F3B2).<br>- **WantsToRun is called only when runnable, so the latch survives while the behaviour is not runnable.**<br>- Result = runnable && WantsToRun && timeoutOK. | 0x60F3BC..0x60F454 | M10-003 | EXACT_SOURCE. The meaning of +0xA1 is UNKNOWN. |
| C15 | **RobotShaken:** (beh+0xA1 \|\| IsRunnable) && WantsToRun, where WantsToRun is always called. The wants-to-run test is robot+0x37C > 16000. | 0x612FD4..0x61300C; 0x6146CC..0x6146E0 | NEW | EXACT_SOURCE |
| C16 | **PlacedOnSlope:** WantsToRun, then IsRunnable.<br>- Constants: 10/55 deg, 0.01 rad/s (max of raw gyro +0x36C..+0x374), 0.4 s, 1.5 s.<br>- +0x18 and +0x20 start at 0.0; times are BaseStationTimer seconds.<br>- Result = pitch in (10, 55) && quietFor > 0.4 && (pickedUp \|\| now − lastPicked < 1.5) && +0x355 < 2. | 0x612EB0..0x612ECC; 0x6144FC..0x614554; 0x61455C..0x614688 | NEW | EXACT_SOURCE |
| C17 | **Frustration:** cur ≠ 4 && mood(robot+0x440)+0x78 < maxConfidence && (last ≤ 0 \|\| now − last > cooldown) && IsRunnable. AnimationComplete stamps BaseStationTimer seconds. Which emotion +0x78 is was not read. | 0x60EE6E..0x60EED8; 0x60EEF4 | NEW | EXACT_SOURCE except the emotion (RECOVERABLE_GAP: MoodManager layout) |

**NOT DONE:**
- the remainder of the Cliff filter (0x60DC98);
- the writer and meaning of IBehavior+0xA1;
- the semantics of ActionList::Cancel(−1);
- IsReactionTriggerEnabled 0x5A40B8 and the disable locks;
- SwitchToBehaviorBase (M7/M8);
- the TryToResume failure path beyond its log;
- where BaseStationTimer is advanced;
- DetectGyroDrift/DetectBias (RS9);
- re-reading the GetSizeByType values;
- the other strategy classes (CubeMoved, FistBump, Hiccup, Sparked, PlacedOnCharger, NoPreDockPoses, Pet, Face/ObjectPositionUpdated);
- the global absence check behind M10-009. I verified only its predicate (0x6143CA) and the ctor store (0x569A80).

#### Existing records contradicted by the source
- **M10-006:**
  - The body-lock gate applies only while robot+0x248 (AnimationState.tag) ≠ 0 (B3). The title's "only while the body track is locked" is wrong when no animation is playing.
  - Its open writer is settled: 0x53800C.
  - It omits the gates B2, B4 and B5.
- **M10-008:**
  - (a) Its unlock rule is wrong. The engine also unlocks when none of B8..BA is set (C7).
  - (b) manager+0x1C is {current, resume +8, trigger +0x10}. An existing parked resume is kept, a running reaction can be parked, and without shouldResumeLast the parked behaviour is dropped (C9).
  - (c) The head/lift pair comes from SetDefaultHeadAndLiftState. It is not captured when the reaction interrupts (C11).
  - (d) The restore also moves the lift, in a parallel compound action.

#### Existing records whose evidence is too weak for their status
- **M10-004:** it is live-path EXACT, but the order it records omits C3 (the sticky action gate), the disable-lock skip, the map order, the no-break loop, and the C5 predicate values.
- **M10-007:** it omits:
  - the always-sent UnexpectedMovement broadcast, including side UNKNOWN when the trigger is disabled or the history lookup fails (B12, B13, B17);
  - the accumulator reset;
  - the IsReactionTriggerEnabled gate;
  - the obstacle's parenting to the new pose.
  I did not re-read its GetSizeByType values.
- **M10-001:** the classifier itself is confirmed. Its consequences have no records: A8 (Cancel on Falling), A11, A12, A14, and the physical-flag source A4.
- **M10-002:** the constants are confirmed. Conditions B2..B10 are in no record.
- **M10-005:** the engine clock is BaseStationTimer ms, set once per call (A2). Whether a robot-timestamp debounce is equivalent depends on how often BaseStationTimer advances, which was not read.

#### Open questions for the manager
1. The engine starts non-physical and becomes physical only on a FirmwareVersion without a "sim" key (A4, M3 interface). While non-physical, CFUM does nothing and the on-back centre is 1.6825 rad.
2. Should the C3 sticky "first action queued" gate on all reactions be modelled? What queues the first action is M7/M8.
3. M10-005: is it equivalent, given that BaseStationTimer is engine-tick time?
4. Where TurnInPlace, IsRunnableBase and the other +0x355 consumers belong (A16).

## Appendix B: gap pass 1, extractor report

I worked read-only. The earlier M10 tools were used in place, and the new helpers are in `...\scratchpad\extract\M10-gap\` (`fn.py`, `vtd.py`, `str.py`, `ven.sh`). The dumps there are `irte.txt`, `drwl.txt`, `rdrl.txt`, `bmmsg.txt`, `run.txt`, `gsbt.txt`, `vtables.txt`, `fist.txt`, `hic*.txt`, `pet.txt`, `cube.txt`, `small3.txt` and `stratsyms.txt`. Items 1–7 are done. Item 8 is done for the predicates; the helpers I did not read are marked RECOVERABLE_GAP.

**Strategy vtable layout** (from typeinfo relocations and the dump in `vtables.txt`):

| slot | meaning |
|---|---|
| +0x08 | shouldResumeLast |
| +0x0C | CanInterruptOther |
| +0x10 | CanInterruptSelf |
| +0x18 | AlwaysHandleInternal (E2G) |
| +0x1C | EnabledStateChanged(Robot const&, bool) |
| +0x20 | SetupForceTriggerBehavior |
| +0x24 | ShouldTriggerBehaviorInternal |

Base strategy fields: +4 is the IWantsToRunStrategy*, +8 is the Robot& (ctor 0x60B29C `stm r4,{vt,0,robot}`).

#### 1. CliffDetected filter (lambda 0x60DC76)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 1a | The filter lambda captures the Generic strategy pointer. Its vtable is 0x102CEC8, and operator() is 0x60DC77. | 0x60D6D0..0x60D6E0 (`str sb,[sp,#0x114]`, vtable 0x102CEC8+8); vtable slot +0x18 = 0x60DC77 | M10-003 | EXACT_SOURCE |
| 1b | The filter:<br>- If IsReactionTriggerEnabled(0 = CliffDetected) is false, it returns false.<br>- Else, if GetCurrentReactionTrigger() ≠ 0, it returns true. "None" is 0x16, so it also returns true when no reaction is running.<br>- Else (Cliff itself is running), it returns strategy->vslot+0x10 (CanInterruptSelf). For Generic that is `return 0` (0x60B732). | 0x60DC7C..0x60DCA2; GetCurrentReactionTrigger 0x5A19EC = `[mgr+0x1C]+0x10`; Generic vtable 0x102D1F0 → 0x60B733 | M10-003 | EXACT_SOURCE |
| 1c | **Net effect: Cliff latches only while Cliff is enabled and not already the running reaction.** The filter gates both subscribed tags (CliffEvent 34 and RobotStopped 52), because there is one filter per strategy (C13). | as above | M10-003 | EXACT_SOURCE |

#### 2. IBehavior+0xA1

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 2a | **+0xA1 is "is running".**<br>- Init: `strh 0x100` at +0xA0, so +0xA1 = 1. It is set back to 0 if InitInternal (vslot +0x48) returns a non-zero result.<br>- Resume: +0xA1 = 1 when ResumeInternal (vslot +0x4C) returns 0, else 0.<br>- Stop: +0xA1 = 0.<br>- ResumeInternal: 1 when vslot +0x50 returns 1. | Init 0x5BCCBE..0x5BCD06; Resume 0x5BCF96..0x5BCFA8; Stop 0x5BD10C; 0x5BDAB6 | NEW | EXACT_SOURCE |
| 2b | Readers:<br>- GetRunningDuration returns now − start(+0x34) only while +0xA1 is set, else 0 (0x5BDA68).<br>- IsRunnableBase logs "Behavior %s is already running" and **returns true** when +0xA1 is set. | 0x5BD780..0x5BD7D2 → 0x5BD964 | NEW | EXACT_SOURCE |
| 2c | So the `beh+0xA1 \|\| IsRunnable` short-circuit in C14/C15 treats the behaviour that is already running as runnable. | 0x60F3CA, 0x612FE0 | M10-003 | EXACT_SOURCE |

#### 3. ActionList::Cancel(−1) (A8)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 3a | −1 is RobotActionType::UNKNOWN (COMPOUND = −2, UNKNOWN = −1). ActionQueue::Cancel treats it as a wildcard: `adds r1,r4,#1` skips the type compare. | unity RobotActionType.cs:5–6; 0x53E69E, 0x53E6CC | NEW | EXACT_SOURCE |
| 3b | ActionList::Cancel(type):<br>- If list+0xC (the "currently clearing" flag that Clear sets, 0x53D922..0x53D938) is set, it returns true with no effect.<br>- Otherwise it calls ActionQueue::Cancel on every queue in the map, in ascending slot order, and ORs the results. | 0x53DE18..0x53DE56 | NEW | EXACT_SOURCE |
| 3c | ActionQueue::Cancel:<br>- **Current action**, if the type matches: IActionRunner::Cancel(), then DeleteActionAndIter.<br>- **Every queued action** of a matching type: DeleteActionAndIter only, with no Cancel(). The loop stops early if a delete returns false. | 0x53E69A..0x53E6FA | NEW | EXACT_SOURCE |
| 3d | IActionRunner::Cancel: if the state (+0x18) is NOT_STARTED (0x2000001), nothing happens. Otherwise it logs ("Actions") and sets the state to CANCELLED_WHILE_RUNNING (0x2000000). | 0x5409E4..0x540A40 | NEW | EXACT_SOURCE |
| 3e | DeleteActionAndIter:<br>- Guarded by inserting tag(+0x60) into a set at queue+0x10.<br>- PrepForCompletion.<br>- If the robot has an ExternalInterface and state ≠ INTERRUPTED (0x3000009): builds RobotCompletedAction and broadcasts it.<br>- Deletes the action, unlinks it from the list and erases the tag.<br>So queued, never-started actions complete with their current state (NOT_STARTED). | 0x53F9FA..0x53FAC6 | NEW | EXACT_SOURCE. The message body (GetRobotCompletedActionMessage) was not read; it is an interface. |

#### 4. IsReactionTriggerEnabled and disable locks

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 4a | IsReactionTriggerEnabled(t):<br>- looks t up in the map (header mgr+0x40, root +0x44);<br>- not found: `VERIFY ... Reached the end of the reaction trigger map` and returns **false**;<br>- found: returns node+0x28 (lock-set size) == 0.<br>Node layout: key +0x10, strategy vector +0x14, lock set<string> +0x20/+0x24/+0x28. | 0x5A40B8..0x5A4108 | M10-004 | EXACT_SOURCE |
| 4b | **DisableReactionsWithLock(lock, bool[21], stopCurrent)**, for each map node in ascending order where entries[t].value ≠ 0:<br>- log "BehaviorManager.DisableReactionsWithLock.DisablingWithLock";<br>- if the set was empty, call vslot +0x1C EnabledStateChanged(robot, **false**) on each strategy;<br>- if the lock is not yet present, AddDisableLockToTrigger;<br>- if stopCurrent and t == current trigger: log "Disabling reaction triggers - stopping currently running one" and SwitchToBehaviorBase({null, null, 0x16}).<br>The function also compares the lock with "sdk" but discards the result (dead code). | 0x5A27F6..0x5A2960; "sdk" at 0x5A29AC | NEW | EXACT_SOURCE |
| 4c | **RemoveDisableReactionsLock(lock):**<br>- if lock == "sdk", mgr+0x4C \|= 1 (the sticky C3 flag);<br>- for each node that holds the lock: log, then RemoveDisableLockFromTrigger;<br>- if the set becomes empty: log "…ReactionReEnabled" and call EnabledStateChanged(robot, **true**) on each strategy. | 0x5A3A52..0x5A3B94 | M10-004 (C3) | EXACT_SOURCE |
| 4d | The only callers of AddDisableLockToTrigger and RemoveDisableLockFromTrigger are 4b and 4c. | xref 0x5A28C8, 0x5A3B10 | NEW | EXACT_SOURCE |
| 4e | **Game messages** (InitializeEventHandlers lambdas):<br>- DisableReactionsWithLock: the lock string plus 21 bools at msg+0xC..+0x20 map in order to triggers 0..20, with stopCurrent = **1**.<br>- RemoveDisableReactionsLock: passes straight through.<br>- DisableAllReactionsWithLock: uses table 0xC60D40 (every trigger 1), stopCurrent = 1. | 0x5A4D2A..0x5A4E3E; 0x5A4EA6..0x5A4EBA; 0x5A4F20..0x5A4F3A | NEW | EXACT_SOURCE |
| 4f | **Engine callers that add or remove locks:**<br>- IDockAction::Init;<br>- PlaceObjectOnGroundAction::Init / dtor;<br>- AlignWithObjectAction dtor;<br>- FlipBlockAction::Init / dtor;<br>- SevereNeedsComponent Set/ClearSevereNeedExpression;<br>- SwitchToUIGameRequestBehavior / EnsureRequestGameIsClear;<br>- ActivityBuildPyramid;<br>- IActivity/IBehavior::Smart*;<br>- factory and test behaviours.<br>**None of these is in a ctor or init path, so at startup every mapped trigger is enabled.** | xref list at 0x55189A…0x5D4D58 | NEW | EXACT_SOURCE |
| 4g | All 21 triggers are mapped in the shipped map, so none hits the "not found → false" path. | `re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/reactionTrigger_behavior_map.json` | M10-004 | EXACT_SOURCE (asset) |
| 4h | **App-side locks (Unity):**<br>- "wakeup" (kWakeupTriggers: cliff, falling, pickup, treads, back, face, side and shaken stay enabled) is added in WakeupSequence (ConnectionFlowController.cs:593) and removed in FinishConnectionFlow (:662);<br>- "sim" is added only for a Sim connection (Robot.cs:447);<br>- the pause-manager, minigame and other groups are in ReactionaryBehaviorEnableGroups.cs. | as cited | NEW | EXACT_SOURCE (authority 2) |
| 4i | Who sends the "sdk" lock: no Unity code does. It comes from outside the app (the SDK client). | grep of unity/ | NEW | BLOCKED_EXTERNAL for the sender; the engine side is exact |
| 4j | **EnabledStateChanged (+0x1C) per class:**<br>- Generic clears +0x2A (0x60F910); no reader of +0x2A was found in 0x5A0000..0x616000;<br>- FistBump, NoPreDock, ObjectPos, PlacedOnCharger, Sparked: no-op (0x60B73A);<br>- Pet: InitReactedTo;<br>- Hiccup: see 8;<br>- CubeMoved: 0x60C03C, not read;<br>- Face: 0x60D1A0, not read. | vtables.txt | NEW | EXACT_SOURCE, except CubeMoved and Face (RECOVERABLE_GAP) |

#### 5. IMU filter initial values (A17)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 5a | The Robot ctor calls `__aeabi_memclr8(robot+0x378, 0x18)`, so +0x378..+0x38F (including +0x37C filtered \|a\| and +0x384 filtered a.y) start at **0**. No other ctor store touches them.<br>Consequences:<br>- the first CUTS sideDev = \|0−9800\| = 9800, so the first tick is not OnSide;<br>- RobotShaken starts false.<br>The raw gyro +0x36C..+0x374 was not in this span and was not checked. | 0x5100E0 `movs r1,#0x18`; 0x5100FA..0x5100FE | M10-001 | EXACT_SOURCE |

#### 6. BaseStationTimer (M10-005)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 6a | **Only caller of UpdateTime:** CozmoEngine::Update at 0x4ED626, in engine state 3, once per tick (M1 CD8/CC10). | xref; M1 CD8 | M1-024 | EXACT_SOURCE |
| 6b | **The ns argument** is tickStart − runStart:<br>- runStart = steady_clock::now() at [sp+0x58], taken once;<br>- tickStart is stored at 0x65B70A (`strd r4,r7,[sp,#0x30]`) after each tick's sleep;<br>- the value is round-tripped through /1e9·1e9.<br>So the clock is elapsed time since the run started: 0 on the first tick, then roughly 60 ms steps. The second caller, CozmoAPI::Update(ns) (0x65BA18), passes a host-supplied value. | 0x65B3B6..0x65B420 | NEW | EXACT_SOURCE |
| 6c | **UpdateTime:**<br>- stores ns (+0x18), seconds as a double (+8), seconds as a float (+0x10), dt (+0x20) and tick++ (+0x24);<br>- GetCurrentTimeStamp = u32(double seconds·1000), truncated;<br>- GetCurrentTimeInSeconds is the float.<br>Every read within one tick returns the tick-start value. | 0x84BC38..0x84BCD4 | M1-024 | EXACT_SOURCE |
| 6d | **So the M10-005 debounce clock is quantised to the engine tick.** The CUTS check `t+250 > now` commits on the first tick where now − t ≥ 250. That clock is independent of the robot timestamp. | follows from 6a–6c and A2/A7 | M10-005 | EXACT_SOURCE for the facts; equivalence is the manager's decision |

#### 7. GetSizeByType(0x10) (B14)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| 7a | GetSizeByType builds a static map<ObjectType, Point3f> with three entries:<br>- 0x0E ProxObstacle: {10, 10, 50};<br>- 0x0F CliffDetection: {20, 40, 50};<br>- **0x10 CollisionObstacle: {20, 54.2, 67.7}**.<br>An unknown type logs sErrorF. | 0x50270E..0x502772 (literals 0x41A00000, 0x4258CCCD, 0x42876666); names from unity ObjectType.cs | M10-007 | EXACT_SOURCE |
| 7b | So d = 20 + 5 = **25**, and the obstacle offsets are:<br>- FRONT x = 47.1;<br>- BACK x = −80.9;<br>- LEFT y = +52.1;<br>- RIGHT y = −52.1. | 0x63E6BA..0x63E6D4 + B14 | M10-007 | EXACT_SOURCE |

#### 8. Other strategies (the M10 predicates only)

The flags are given as (shouldResumeLast +8, CanInterruptOther +0xC, CanInterruptSelf +0x10). "IsRunnable" means IBehavior::IsRunnable.

| strategy | ShouldTriggerBehaviorInternal / relevant handlers | citation | class |
|---|---|---|---|
| **PlacedOnCharger** (0,1,0) | If wantsToRun (+4) is null: VERIFY and false. Otherwise it tail-calls IWantsToRunStrategy::WantsToRun(robot); there is **no IsRunnable**. The JSON configures `strategyType: PlacedOnCharger`. That WantsToRun body was not read. | 0x6120D0..0x6120E8; vt 0x61214A/0x61214E | EXACT_SOURCE; WantsToRun(PlacedOnCharger) is RECOVERABLE_GAP |
| **Sparked** (0,1,0) | False unless CurrentBehaviorTriggeredAsReaction() (current trigger ≠ 0x16).<br>False if requestedSpark (mgr+0x60) == 0x55 (none), or activeSpark (mgr+0x58) == requested.<br>False if the current behaviour class (+0x64) is 0x3D ReactToCliff or 0x4C ReactToSparked.<br>Otherwise IsRunnable. SetRequestedSpark writes mgr+0x60/+0x64 (0x5A3EB2). | 0x6130FE..0x613168 | EXACT_SOURCE. That +0x58 is the active spark is inferred from the comparison only. |
| **NoPreDockPoses** (0,1,0) | Looks up RamIntoBlock (class 0x3C). Reads id = [[robot+0x264]+0x18]+0x70; if −1, false. Otherwise it resets that field to −1, sets ram+0x11C = id and returns IsRunnable. | 0x610E32..0x610E82 | EXACT_SOURCE; the writer of the +0x70 field is RECOVERABLE_GAP |
| **FistBump** (0,0,0) | **AlwaysHandle (tag 0x8B BehaviorObjectiveAchieved):**<br>- look up the objective's params {cooldown +0xC, prob +0x10, expiration +0x14};<br>- if lastReset (+0x4C) ≠ 0 and now − last ≤ cooldown, ignore;<br>- else if RandDbl(1.0) < prob: DAS "robot.trigger_fist_bump_response", pending (+0x44) = 1, deadline (+0x48) = now + expiration.<br>**STBI:**<br>- !pending: false;<br>- beh running (+0xA1): pending = 0, false;<br>- now > deadline: DAS "robot.trigger_fist_bump_expired", pending = 0, false;<br>- robot+0x355 ≠ 0: false, pending kept;<br>- else IsRunnable.<br>**ResetTrigger(b):** pending = 0; if b, last = now.<br>Shipped params: StackedBlock 180/0.2/3, BuiltPyramid 180/0.5/3, InteractedWithFace 300/0.75/3, PeekABooSuccess 300/0.2/3, PerformedStrongWorkout 180/0.5/3, PoppedWheelie 180/0.2/6. | 0x60E8C0..0x60E992; 0x60E7E4..0x60E86E; 0x60E9FA; JSON | EXACT_SOURCE |
| **Hiccup** (1,0,0) | **Config (ParseConfig, hiccupParams):**<br>- +0x58/+0x5C occurrence min/max (s → ms);<br>- +0x60/+0x64 number of hiccups;<br>- +0x68/+0x6C spacing (ms);<br>- +0x70 cured delay (s → ms);<br>- +0x74 unlockId.<br>**ResetHiccups:** remaining = total = RandInt(num); next = spacingNext = now + RandInt(freq); +0x54 = 0; if hasHiccups, clear it and broadcast RobotHiccupsChanged{false}; [[robot+0x264]+0x18]+0x74 = 0.<br>**STBI:**<br>- FeatureGate(6) off: false;<br>- unlockId not unlocked: ResetHiccups, false;<br>- latch forced = +0x3C, then +0x3C = 0.<br>**STBI, state +0x40 < 2:**<br>- now ≤ next: false;<br>- [[robot+0x264]+0x30]+0x14 ≤ 1: CureHiccups(false), false;<br>- else set the whiteboard flag; if now ≤ spacingNext, false; else spacingNext = now + RandInt(spacing); if forced, false;<br>- remaining−− (if it was 0: CureHiccups(false), false);<br>- first time: hasHiccups = 1 and broadcast RobotHiccupsChanged{true};<br>- set the PlayAnimSequence anims (trigger 0xE2 on the first hiccup, else 0xE1);<br>- IsRunnable: stamp +0x54, NeedActionCompleted(0x1A), true.<br>**STBI, state 2/3:** set the cure anim table (0xC76818 / 0xC7681C); if IsRunnable, state = 0, ResetKeepFaceAliveLastStreamTimeout, true.<br>**CureHiccups(b):** DAS, Reset, state = 3; if b: state = 2, next += cured delay, NeedActionCompleted(0x19); else NeedActionCompleted(0x18).<br>**E2G RobotOffTreadsStateChanged** (only while Hiccup(5) is enabled):<br>- to OnTreads with now > next and state == 1: CureHiccups(true);<br>- to OnFace/OnBack with now > next and state == 0: robot+0x220 = 5.0f, state = 1.<br>**G2E NotifyOverfeedingShouldTriggerHiccups (0xA4):** next = spacingNext = now, or an error if disabled.<br>**EnabledStateChanged(false):** forced = 1; if now > next and the need bracket of NeedId 1 or 0 == 3: CureHiccups(false). | 0x60FECC..0x6100C2; 0x6101A0..0x610212; 0x610678..0x610868; 0x61098C..0x6109C4; 0x6109CC..0x610A3E; 0x610A44..0x610A6A; 0x610AF8..0x610B40 | EXACT_SOURCE for control flow. Crosses into needs, progression and feature gates; robot+0x220, NeedId/bracket names and the [+0x264] component identities are UNKNOWN. |
| **PetInitialDetection** (1,1,0) | **STBI:**<br>- RecentlyReacted (last +0x40 > −1 and last + **60 s** > now): UpdateReactedTo (add all current pet ids), false;<br>- else, for each pet in [robot+0x3C] with id not in reactedTo and numTimesObserved (node+0x18) > 2 (threshold 3): collect;<br>- none: false;<br>- robot+0x34A (on charger platform): false;<br>- else ReactToPet (class 0x43)+0x11C = targets and IsRunnable.<br>BehaviorDidReact: reset the set, add current and reacted ids, last = now.<br>EnabledStateChanged: InitReactedTo.<br>Ctor: +0x40 = −1. | 0x611AE4..0x611CB6; 0x611DD0; 0x611E1C; 0x611E80..0x611F28; 0x61203A; 0x6117A6 | EXACT_SOURCE |
| **CubeMoved** (1,1,**1**) | **Per-object record:** id +4, moveTs +8, upAxis +0xC (init 7), moving +0xD, observed +0xE, upAxisChanged +0xF.<br>**AlwaysHandle** (only while CubeMoved(8) is enabled):<br>- ObjectMoved (0xF) → StartedMoving: if not moving, moving = 1, ts = msg.ts, axis = msg+0x14; if moving and the axis differs, changed = 1;<br>- ObjectStoppedMoving (0x10): moving = 0;<br>- ObjectUpAxisChanged (0x11): lookup only, no state change;<br>- RobotObservedObject (0x44): if located, clear, observed = 1.<br>**STBI**, over the records in order:<br>- not located: erase;<br>- skip unless the distance to the robot is > **50 mm**;<br>- candidate if (moving && observed && robotTs(+0x2C) − ts > **1000** && ts ≠ 0) or (located && changed && observed);<br>- skip if IsVisibleFrom(camera, **0.785398**, 0, false, 0, 0);<br>- else reset the record, AcknowledgeCubeMoved(0x3E)+0x124 = id, and return running \|\| IsRunnable (first hit only). | 0x60BEA8..0x60BF62; 0x60C336..0x60C37C; 0x60BFC8; 0x60BDF8; 0x60BE48; 0x60BC80..0x60BDDA | EXACT_SOURCE; EnabledStateChanged 0x60C03C and SetupForce are RECOVERABLE_GAP |
| **FacePositionUpdated** (1,1,0) | If the desiredFaces set (+0x58..+0x60) is empty: false. Otherwise AcknowledgeFace(0x3A)+0x120 = set; robot+0x34A: false; else IsRunnable. | 0x60CAB0..0x60CB5E | EXACT_SOURCE for STBI. How the set is filled (HandleFaceObserved 0x60CB84, PositionUpdate base 0x612168..0x612B90) is RECOVERABLE_GAP. |
| **ObjectPositionUpdated** (1,1,0) | False if carrying (robot+0x284 +8 ≠ −1), if [robot+0x280]+4 (picking or placing) is set, or if robot+0x34A. False if !HasDesiredReactionTargets(robot, false). Otherwise GetDesiredReactionTargets → AcknowledgeObject(0x3B)+0x14C, and IsRunnable. | 0x6113E8..0x611470 | EXACT_SOURCE for STBI; the PositionUpdate helpers (GetBestTarget and others) are RECOVERABLE_GAP |

#### Existing records contradicted by the source
- **M10-003 (Cliff):** the filter is not only "enabled". It also rejects while CliffDetected is the current reaction (1b).
- **M10-004:** a trigger missing from the map reports disabled via VERIFY (4a). Disabling through the game message always stops the current reaction if it is that trigger (4b, 4e). Enable/disable calls EnabledStateChanged on every strategy of the trigger, which changes Hiccup, Pet and CubeMoved state (4j).

#### Existing records whose evidence is too weak for their status
- **M10-005:** its clock is now settled (6b–6d). The debounce runs on tick-start time since the run began, quantised to about 60 ms, not on the robot timestamp. Its status should follow the manager's equivalence decision.
- **M10-007:** it had no GetSizeByType value. The value is now settled: d = 25 (7a).
- **M10-001:** the IMU initial values are now settled (5a). The raw gyro +0x36C..+0x374 is still unchecked.

#### Open questions for the manager
1. Hiccup, Sparked and NoPreDockPoses depend on needs, progression, feature-gate and whiteboard state (robot+0x264 components, robot+0x220, NeedId brackets). Should these stay in M10 or be deferred?
2. Remaining RECOVERABLE_GAPs, if they are needed:
   - the PlacedOnCharger WantsToRun body;
   - CubeMoved and Face EnabledStateChanged;
   - the PositionUpdate target selection;
   - the writer of the NoPreDockPoses id (+0x70);
   - Frustration's mood +0x78 (earlier report).
3. The "sdk" lock and the sticky mgr+0x4C gate come from outside the app (4i). Should the stack model the gate as permanently false, which is the stock app's state until the first action is queued (C3)?

## Appendix C: gap pass 2, extractor report

I worked read-only. The new dumps are in `...\scratchpad\extract\M10-gap\`: `cubeesc.txt`, `pu1.txt`, `face1.txt`, `hfo.txt`, `obj.txt`, and `wb70.py` (a scanner for writers of the whiteboard +0x70 field). All seven items are settled from source. Nothing in them is left as a RECOVERABLE_GAP; the only remaining unknown is noted in item 7.

Two things to know first:
- **My earlier label for CubeMoved's gate was wrong.** CubeMoved's event handler checks whether **ObjectPositionUpdated** (trigger 8) is enabled, not CubeMoved (trigger 1).
- I confirmed the ReactionTrigger ordinals from the engine name table (EnumToString 0x77065C → table 0x10337C0): 0 CliffDetected, 1 CubeMoved, 2 FacePositionUpdated, 3 FistBump, 4 Frustration, 5 Hiccup, 6 MotorCalibration, 7 NoPreDockPoses, 8 ObjectPositionUpdated, 9 PlacedOnCharger, 10 PetInitialDetection, 11 RobotFalling, 12 RobotPickedUp, 13 RobotPlacedOnSlope, 14 ReturnedToTreads, 15 RobotOnBack, 16 RobotOnFace, 17 RobotOnSide, 18 RobotShaken, 19 Sparked, 20 UnexpectedMovement, 21 Count, 22 NoneTrigger (0x16).

#### 1. PlacedOnCharger wants-to-run (StrategyPlacedOnCharger)

| step | what the original does | citation | class |
|---|---|---|---|
| 1a | The base IWantsToRunStrategy::WantsToRun just tail-calls vslot +0x10 (WantsToRunInternal). | 0x6134B4..0x6134B8 | EXACT_SOURCE |
| 1b | Ctor: latch (+0x15) = 0, deadline (+0x18) = −1.0. It subscribes to the E2G tag set at 0xC76FD0 = {57 ChargerEvent}. | 0x6143F8..0x614430 | EXACT_SOURCE |
| 1c | Event handler (vtable 0x102D688, slot +0xC): on tag 0x39 ChargerEvent, latch = msg.onCharger. The value is overwritten on every event, so an event with onCharger = false clears the latch. | 0x6144B8..0x6144D0 | EXACT_SOURCE |
| 1d | WantsToRunInternal:<br>- on the first call (deadline < 0), deadline = now + **20.0 s** (BaseStationTimer seconds). The 20 s start from the first call, not from construction;<br>- result = (now ≥ deadline) && latch;<br>- **the latch is cleared on every call**.<br>This is the whole predicate: the shouldTrigger path is just WantsToRun, with no IsRunnable. | 0x614474..0x6144B6 | EXACT_SOURCE |

#### 2. CubeMoved: enable callback and forced trigger

| step | what the original does | citation | class |
|---|---|---|---|
| 2a | **Enable callback, enabled = true:** it builds a BlockWorld filter for the families {LightCube 2, Block 1} (table 0xC759FC) and runs FindLocatedMatchingObjects. For each object whose PoseState (+0x24) == 1 (Known), it takes or creates the object's record and, if the object is located, clears the record and marks it observed. | 0x60C04A..0x60C14C; PoseState byte from SetPoseStateHelper 0x506154 | EXACT_SOURCE |
| 2b | **Enable callback, enabled = false:** it clears every record (moveTs, moving, observed and upAxisChanged all 0). | 0x60C1CC..0x60C1E2 | EXACT_SOURCE |
| 2c | **Forced trigger:** it walks the records in order and erases the ones whose object is no longer located. For the first located one, it clears the record and hands that object id to the behaviour (AcknowledgeCubeMoved +0x124). If none is found, it logs a warning. It has no return value. | 0x60B91C..0x60BA14 | EXACT_SOURCE |
| 2d | **Correction to round 1:** the CubeMoved event handler only runs while trigger **8** (ObjectPositionUpdated) is enabled (`movs r1,#8`). | 0x60BEB4 | EXACT_SOURCE |

#### 3. FacePositionUpdated

| step | what the original does | citation | class |
|---|---|---|---|
| 3a | **Ctor:** calls the position-update base with trigger 2 and sets:<br>- last-reaction time (+0x54) = −1;<br>- desiredFaces set at +0x58;<br>- reactedFaces set at +0x64;<br>- per-face "is close" map at +0x70.<br>It subscribes to the E2G set at 0xC75A40. | 0x60C62E..0x60C69C | EXACT_SOURCE |
| 3b | **Enable callback** (either value): clears desiredFaces. | 0x60D1A0..0x60D1B8 | EXACT_SOURCE |
| 3c | **Event dispatch:** the base handler calls ResetReactionData on tag 50 RobotDelocalized, then the per-class handler.<br>- ResetReactionData loops over the map **by value** and zeroes each copy's reacted time, so it **changes nothing that is stored**.<br>- Face's own handler: tag 0x32 does nothing; tag 0x46 RobotObservedFace goes to the face-observed handler; any other tag logs an error. | 0x612214..0x612236; 0x612238..0x6122C4; 0x60CF60..0x60CF86 | EXACT_SOURCE |
| 3d | **AddDesiredFace:** inserts the face only while trigger 2 is enabled, and returns whether it was inserted. | 0x60CF2C..0x60CF5C | EXACT_SOURCE |
| 3e | **Face-observed handler** (0x60CB84):<br>- it returns if the id < 0 or FaceWorld (robot+0x38) has no such face;<br>- if the face has a name and is not in reactedFaces: AddDesiredFace and log "named for the first time"; mark this observation as handled;<br>- dist = distance(robot pose, face pose); on failure it logs an error and returns;<br>- threshold: face in the close map and currently close → **400 mm**; in the map and not close → **300 mm**; not in the map → **300 mm**, and it is then never "became close";<br>- isClose = dist < threshold;<br>- it calls AddDesiredFace and logs "BehaviorAcknowledgeFace.FaceBecomeClose" only when all four hold: the face was known and not close, it is now close, it was not handled as first-named, and there is no cooldown. Cooldown means last ≥ 0 and now < last + **4.0 s**;<br>- close map[id] = isClose, always. | 0x60CB84..0x60CE20; floats 0x60CF14..0x60CF1C = 300, 300, 400 | EXACT_SOURCE |
| 3f | **FinishedReactingToFace:** the base records the reaction; the id is added to reactedFaces; last (+0x54) = now (seconds). **ClearDesiredTargets:** clears desiredFaces. | 0x60D082..0x60D0A4; 0x60D0B0 | EXACT_SOURCE |
| 3g | The Face shouldTrigger test (round 1) reads only desiredFaces. The base per-target pose map is never filled for faces. | 0x60CAB0 | EXACT_SOURCE |

#### 4. ObjectPositionUpdated and the position-update base

| step | what the original does | citation | class |
|---|---|---|---|
| 4a | **Base ctor:**<br>- angle tolerance (+0x2C) = 0.785398 rad (45°);<br>- time threshold (+0x34) = **600 000 ms**;<br>- distance tolerance (+0x38) = **80.0 mm**;<br>- +0x3C = 30.0 (no VLDR reader of it in 0x60C600..0x613000);<br>- per-target map at +0x40/+0x44/+0x48;<br>- trigger byte (+0x4C) from the ctor argument (Face 2, Object 8).<br>It subscribes to the E2G set at 0xC769C0. | 0x612168..0x6121D4 | EXACT_SOURCE |
| 4b | **Per-target record:** last observed pose (node+0x14), last reacted pose (node+0x20), last observed time (node+0x2C), last reacted time (node+0x30). | as used below | EXACT_SOURCE |
| 4c | **Recording an observation (with enabled flag):** the enabled flag is IsReactionTriggerEnabled(+0x4C).<br>- Existing target: update the observed pose and time. If the flag is false, also copy them into the reacted pose and time, so it counts as already reacted.<br>- New target: add a record with those values and a reacted time of 0; if the flag is false, reacted pose and time = the observation. | 0x612344..0x612366; 0x61236C..0x612448 | EXACT_SOURCE |
| 4d | **Pose-change test (poseHelper):** it expresses the reacted pose relative to the observed pose (GetWithRespectTo). If that fails (different origins), the result is false. Otherwise the result is !IsSameAs(distance {80, 80, 80}, angle 45°), i.e. **moved more than 80 mm or 45°**. | 0x612484..0x6124FA | EXACT_SOURCE |
| 4e | **ShouldReactToTarget(robot, target, anyMode):**<br>- anyMode = true: false if any record with a non-zero reacted time fails poseHelper(this target's observed pose, that record's reacted pose); otherwise true;<br>- anyMode = false: true if reacted time == 0; otherwise poseHelper(observed, reacted) \|\| (lastImageTimeStamp − reacted time > 600 000, as u32). | 0x612530..0x6125B4 | EXACT_SOURCE |
| 4f | HasDesiredReactionTargets means some record passes ShouldReactToTarget. GetDesiredReactionTargets inserts every id that passes. | 0x61261A..0x612662; 0x6125B8..0x612616 | EXACT_SOURCE |
| 4g | **GetBestTarget (0x612668)** is not referenced anywhere in the engine: no caller, no PLT slot, and no pointer except its own `.dynsym` entry. It is on no path. | brute-force search for 0x612669; xref | EXACT_SOURCE (absence) |
| 4h | **RobotReactedToId(robot, id):** reacted pose = observed pose, reacted time = Robot::GetLastImageTimeStamp. If the id is missing, it logs at debug level. | 0x612A94..0x612B22 | EXACT_SOURCE |
| 4i | **Object ctor:** trigger 8; behaviour-class byte (+0x54) = 0x39, which is later replaced by the class of the bound behaviour (BehaviorThatStrategyWillTriggerInternal). | 0x611072..0x6110A4; 0x611674..0x61168C | EXACT_SOURCE |
| 4j | **Object event handler:**<br>- tag 0x32: nothing;<br>- tag 0x44 RobotObservedObject: only if a current behaviour exists and its class (+0x64) ≠ +0x54 → the object-observed handler. **With no current behaviour, observations are dropped**;<br>- any other tag: error. | 0x61153C..0x611582 | EXACT_SOURCE |
| 4k | **Object-observed handler:**<br>- it drops the observation unless msg.objectFamily is in a static set {LightCube, Block} (built at 0x4D7DCA from 0xC76890);<br>- pose = Pose3d(msg.pose, origin list robot+0x294);<br>- if msg.objectID equals the carried object ([robot+0x284]+4) or the ObjectID at [robot+0x280]+8, it records with the enabled flag forced to **false** (counts as reacted);<br>- otherwise it records the observation normally. | 0x6114A0..0x61151E | EXACT_SOURCE; the meaning of the [+0x280]+8 ObjectID is an M12 interface |
| 4l | **Object reaction callbacks:** ReactedToID → RobotReactedToId. ClearDesiredTargets(robot) → RobotReactedToId for every current desired target (anyMode false). | 0x611692; 0x61169C..0x6116E2 | EXACT_SOURCE |

#### 5. NoPreDockPoses id field ([[robot+0x264]+0x18]+0x70)

| step | what the original does | citation | class |
|---|---|---|---|
| 5a | The owning component is the **AIWhiteboard**. Its ctor puts an ObjectID at +0x6C whose value (+0x70) = −1, and the hiccups flag (+0x74) = 0. | 0x56A2DA..0x56A2E8 | EXACT_SOURCE |
| 5b | **Writers of +0x70** (scan over every `ldr.w [..,#0x264]` → `ldr #0x18` → `str #0x70`). Each stores a target object id:<br>- DriveToHelper::DriveToPreActionPose (0x5B5982);<br>- DriveToHelper::RespondToDriveResult (0x5B5CE8), when the result is 0x3000010 **NO_PREACTION_POSES** and helper+0xFC == 0;<br>- PickupBlockHelper::RespondToPickupResult (0x5B8282);<br>- PlaceRelObjectHelper::RespondToPlaceRelResult (0x5B952C);<br>- a BehaviorKnockOverCubes bound callback (0x5C3DEA), on NO_PREACTION_POSES;<br>- a CubeAccelListeners MovementListener lambda (0x5D6A96).<br>The only other access is the reader and reset in the strategy (0x610E60). | wb70.py output; ActionResult.cs:25 | EXACT_SOURCE for the site list. The branch conditions of 0x5B5982, 0x5B8282, 0x5B952C and 0x5D6A96 belong to the M7/M12 behaviour helpers, which your Q1 decision makes named interfaces. |

#### 6. Frustration's mood field (+0x78)

| step | what the original does | citation | class |
|---|---|---|---|
| 6a | The MoodManager (robot+0x440) holds 9 Emotion objects with a 0x20 stride (ctor loop to 0x120). Emotion+0x18 is the value: SetValue writes it, and Add clamps it to [−1, 1]. So +0x78 = emotion 3, whose value is **Confident** (EmotionType: Happy, Calm, Brave, Confident, …). It starts at 0 (Emotion ctor, `strd 0` at +0x18). | 0x67ADB6..0x67ADCA; 0x6796C8; 0x679618..0x67967A; 0x679414; unity EmotionType.cs | EXACT_SOURCE |
| 6b | **Writers:** MoodManager::SetEmotion (0x67BDC0), AddToEmotion / AddToEmotions (0x67BDCA, 0x67BF18, 0x67BFAE), TriggerEmotionEvent (0x67B85C), Update (decay, 0x67B5D4) and Reset. These are mood-subsystem interfaces. | symbols | EXACT_SOURCE (interface) |
| 6c | **Frustration predicate:**<br>- current trigger ≠ 4;<br>- Confident < maxConfidence (+0x30);<br>- last (+0x38) ≤ 0, or now − last > cooldown (+0x34);<br>- then IsRunnable. | 0x60EE86..0x60EED8 | EXACT_SOURCE |

#### 7. Raw accel and gyro (+0x360..+0x374)

| step | what the original does | citation | class |
|---|---|---|---|
| 7a | The **only writer** is UpdateFullRobotState: +0x360..+0x368 = state accel (+0x30), and +0x36C..+0x374 = state gyro (+0x3C), both via `ldm`/`stm` on every RobotState. | 0x5129A4..0x5129BE | EXACT_SOURCE |
| 7b | The Robot ctor has **no store** to +0x35C..+0x377: no str, strd, stm or vstr, and neither memclr covers it (the memclrs are +0x238 for 0x12 bytes and +0x378 for 0x18). The Robot is allocated with `operator new(0x530)` and no memset (RobotManager::AddRobot 0x52EE8E..0x52EE9C). So **the value before the first RobotState is indeterminate** (heap contents). | ctor.txt scan; vscan; 0x52EE8E | EXACT_SOURCE for the absence of an initializer. The value itself is UNKNOWN: whatever was on the heap. |

#### Existing records affected
- **M10-003 / round-1 row 8 (CubeMoved):** the handler's enable gate is ObjectPositionUpdated(8), not CubeMoved (2d).
- **Round-1 row 8 (Face/Object):** now fully specified (3a–4l). GetBestTarget and ResetReactionData have no effect on any path (4g, 3c).

#### Open questions for the manager
1. Item 7b: in the original, PlacedOnSlope's gyro input is heap garbage until the first RobotState. The stack has to choose a value for that window. That is a COMPATIBILITY_POLICY decision, not a recovered fact.
2. The +0x70 writers in 5b are behaviour-helper code (M7/M12). Their exact branch conditions were not read beyond the two NO_PREACTION_POSES sites.
