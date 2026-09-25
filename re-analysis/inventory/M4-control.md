# M4 control inventory (motion, sensors, lights, cubes)

**State: approved by the manager on 2026-09-24 under the operator's standing authorisation, and frozen with `python re-analysis/tools/fidelity.py --approve M4-control`, then re-approved on 2026-09-25 after corrections C1..C9.** That authorisation covers source-derived inventories and ordinary source-fidelity decisions. Only a deliberate divergence from the engine, or an unresolved source question that materially affects robot behaviour, goes to the operator. The existing divergences M4-004 and M4-006 are kept as they stand and reported to the operator (MD4).

## Where this comes from

- **Source:** `libcozmoEngine.so` 3.4.0-1204, freshly disassembled. The decompiled Unity C# (authority 2, for the values the original app passes) and the shipped assets (authority 3) were also used.
- **Read-only extractor passes:**
  - **M4 pass** (Appendix A): MA1..MA23 (motion), LB1..LB6 and LC1..LC8 (lights), SC1..SC11 (sensors), CD1..CD10 (cubes).
  - **Gap pass** (Appendix B): LB4a..LB4i, SC4a..SC4j, SC6, MA1-lift, CD10a..CD10g, LC8a..LC8e.
  - **Cube accelerometer trace** (Appendix C): S1..S19. This came from the CONTROL hardware run, bundle `re-analysis/acceptance/hardware/20260924-202748-CONTROL/`.
- **Manager spot-check:** Robot::Delocalize (0x00510A24) calls ClearCliffRunningStats (0x00510A5A), then PoseOriginList::AddNewOrigin (0x00510A66), then SetNewPose (0x00510B4C). When time-synced (+0x29) it then calls SendAbsLocalizationUpdate (0x00510BF4). The Robot constructor's Delocalize therefore creates origin 1, since PoseOriginList's next id starts at 1 (SC4g). The frame id is reset to 0 afterwards (SC4e).
- **Interfaces used and not redone:**
  - `re-analysis/inventory/M2-protocol.md`: App. A §2 builders; App. B RS0..RS14 and the status bits.
  - `re-analysis/inventory/M1-transport.md`: CD12, CD24, CD25, CD29 and CD18 (AbsoluteLocalizationUpdate).
- **The C# was never evidence.**

## How to read the statuses

- **IMPLEMENTATION_GAP:** established from source, and to be compared and built.
- **HARDWARE_ONLY:** only the robot can answer it.
- **COMPATIBILITY_POLICY:** a deliberate choice of this stack.
- **No RECOVERABLE_GAP remains in M4.**

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M4-001 | IMPLEMENTATION_GAP | Head angle limits −0.436332..0.776672 rad (−25°..44.5°). Commanded angles are clipped with warnings in MoveHeadToAngleAction. Reported angles are clamped state-side (RS6). Before calibration the head angle reads −25°. | MA9, MA22, MA23 |
| M4-002 | IMPLEMENTATION_GAP | Lift presets: key 0 LowDock 32, 1 HighDock 76, 2 HeightCarry 92, 3 OutOfFOV −1. A height outside [32,92] is clamped with a warning. A negative height goes to whichever of 32 and 92 is nearer the current height. | MA13, MA14 |
| M4-003 | IMPLEMENTATION_GAP | The stack's head and lift API follows the game-message path (MD1). SetHeadAngle and SetLiftHeight take the caller's speed, acceleration and duration. The original app passes head 10 rad/s and 20 rad/s², lift 10 and 20, and duration 0. The action constructors' own defaults (head 15/20, lift 10/20) apply only to engine-internal callers. On the game path, a lift height of exactly 32 mm while carrying becomes PlaceObjectOnGround (M12 interface). | MA9..MA13 |
| M4-004 | COMPATIBILITY_POLICY | Motion is gated on calibration here. The engine has no such gate (MA21); it reacts to calibration instead (MA20). An existing policy, kept (MD4). | MA18..MA21 |
| M4-005 | IMPLEMENTATION_GAP | Action ids come from one u8 counter shared by head, lift and body. It is pre-incremented from 0 and wraps to 0, so ids run 1..255, 0, 1, and so on. | MA8 |
| M4-006 | COMPATIBILITY_POLICY | Wheel confirmation tolerance, 35 % / 5 mm/s. The engine has no counterpart. An existing policy, kept (MD4). | — |
| M4-007 | IMPLEMENTATION_GAP | StopAll first runs the track-unlock preamble for all three tracks, then sends **only** StopAllMotors 0x3B. The engine sends no DriveWheels(0). The engine's callers are listed in MA6. | MA5, MA6 |
| M4-008 | IMPLEMENTATION_GAP | Cliff sensor data: raw values, CLIFF_DETECTED, the timestamp and the enum names. The IMU and cube battery are in the engine's units. | SC2, M2 RS12 |
| M4-009 | IMPLEMENTATION_GAP | Cube tracking. It covers ObjectAvailable and ObjectConnectionState (S1, S2, LC8a..LC8e), and the Moved, Stopped and UpAxisChanged handling with its charger and carry filters and the engine's per-object IsMoving (CD10a..CD10c). A light cube keeps one ObjectID per type for the process lifetime; that is the M11 interface. | S1, S2, CD10a..CD10c, LC8a..LC8e |
| M4-010 | IMPLEMENTATION_GAP | Outbound cube connection: the block pool, five slots, SetPropSlot, the filter timers, advertisement expiry, slot states and disconnect handling. After a slot reset the same cube is never re-requested. | CD1..CD9, S3 |
| M4-011 | EXACT_SOURCE | blockPool.txt persistence and the RSSI tie order (unchanged; not re-read in this pass). | existing evidence |
| M4-012 | IMPLEMENTATION_GAP | StartMotorCalibration is never sent automatically; it goes out only from CalibrateMotorAction and the factory behaviour. Byte 0 is head and byte 1 is lift. On MotorCalibration, a lift that starts calibrating while carrying calls SetCarriedObjectAsUnattached(true). | MA18..MA20 |
| M4-013 | HARDWARE_ONLY | Whether the robot honours StartMotorCalibration after its connection-time calibration (unchanged). | — |
| M4-014 | IMPLEMENTATION_GAP | Direct-drive track locks. DriveWheels, MoveHead and MoveLift are ignored while direct drive is disabled, and ignored when another holder has the track locked. They lock the BODY (4), HEAD (1) or LIFT (2) track while the speed is non-zero, which sends DisableAnimTracks 0x9D, and unlock it at zero, which sends EnableAnimTracks 0x9E. The command itself is then sent verbatim, reliable and not hot. | MA1, MA1-lift, MA2, MA3 |
| M4-015 | IMPLEMENTATION_GAP | StopHead, StopLift and StopBody. Each runs the unlock preamble, then sends MoveHead{0}, MoveLift{0} or DriveWheels{0,0,0,0}, reliable. An action that ends while its track is moving stops that track (M8 interface). | MA4, MA7 |
| M4-016 | IMPLEMENTATION_GAP | Head and lift move semantics. Nothing is sent when the motor is already in position (the head uses tolerance + 1e-5; the lift also needs it not moving). The ack is matched only when the "sent" flag is set and the id is equal. Completion means in position and stopped after the ack. StoppedMakingProgress is 0x04000004 and a send failure 0x03000016. Head tolerance is at least 2°; the lift's angular tolerance is at least 1.5°. Variability is applied and then re-clamped. | MA9, MA13, MA15..MA17 |
| M4-017 | IMPLEMENTATION_GAP | Backpack lights (BodyLightComponent). It runs on every Robot::Update after the first state. The best source is chosen in priority {1,0,2}. The Off lights (every word 0x8000, all frame counts 0) are resent every tick while no source exists. The charging state machine is OffCharger, Charging, Charged, BadCharger; "OffCharger" is added by the engine itself, and the JSON gives "charging", "charged" and "badCharger". One locator is shared with SetBackpackLEDs. Wire conversion: 0x03 carries LEDs 1..3 and 0x11 carries LEDs 0 and 4, with no white balance and frames = (ms+29)/30. The headlight is sent after EnableMode(14). | LB1..LB6, LB4a..LB4i |
| M4-018 | IMPLEMENTATION_GAP | Cube lights (CubeLightComponent). When a light cube connects (and again on every reconnect), the engine plays WakeUp (trigger 0x26, cubeLights/wakeUp.json) on layer 2, subject to the PlayLightAnim gates. SetObjectLights, then SetLEDs with the solid-LED rules and gamma 0x80. SetLights sends SetCubeGamma (first call, and whenever gamma changes), then CubeID{activeID, (rot+29)/30}, then CubeLights (40 bytes, white balance G,B ×0.6 when R≠0), all reliable. Patterns advance on their timers. When WakeUp ends, the default layer-2 anim (Connected / Visible / Carrying / Sleep) takes over. | S5..S8, LC1..LC8, LC6 |
| M4-019 | IMPLEMENTATION_GAP | Cliff. The sensor defaults to enabled, with a threshold cache of 400. On CliffEvent with the sensor disabled and flags≠0, the event is dropped. Game EnableCliffSensor is engine-side only. On PotentialCliff: StopAllMotors plus EnableStopOnCliff{0} (with the platform, drone and SDK exceptions). The EnableStopOnCliff senders are listed in SC8; nothing is sent at connection. Threshold schedule: send 50 on the first accepted state of the current frame, 400 after more than 50 mm, 150 from the suspicious-cliff statistic, 400 again on Delocalize, and 50 or 400 on entering or leaving the charger platform. | SC1..SC8, SC4a..SC4j, SC6 |
| M4-020 | IMPLEMENTATION_GAP | RobotState acceptance. UpdateFullRobotState does nothing until time sync (+0x29). Off a ramp, a state whose origin id is not in the pose-origin list is dropped with a warning. The list starts with origin 1, created by the constructor's Delocalize, and origin 0 is never valid. At SyncTime the engine sends AbsoluteLocalizationUpdate{timestamp 0, frameId robot+0x2B0 = 0, originId = the current origin (1), identity pose}. That settles M1-041's ids. | SC4e..SC4h, manager spot-check, M1 CD18 |
| M4-021 | HARDWARE_ONLY | Whether the robot reports the origin id and frame from AbsoluteLocalizationUpdate in its RobotState. This stack has never sent that message, and all 855 states in the CONTROL run reported origin 0 and frame 0. It decides whether M4-020 accepts any state. | SC4i |
| M4-022 | IMPLEMENTATION_GAP | SetBodyRadioMode {1,0} is sent after 16 consecutive RobotStates without IS_BODY_ACC_MODE, and the counter is then reset. | SC10, M1 CD25 |
| M4-023 | IMPLEMENTATION_GAP | Cube taps (BlockTapFilterComponent). Enabled at construction. A tap with intensity ≤ 60 is dropped. On a physical robot, taps queue for 75 ms and only the strongest is broadcast (the earliest wins a tie). Double tap: a 500 ms window suppresses movement, and the object is marked dirty after it. Game EnableBlockTapFilter toggles the filter. | CD10d..CD10g |
| M4-024 | HARDWARE_ONLY | What makes the robot forward cube telemetry (ObjectAccel, Moved, PowerLevel and the rest) after connection, and the robot-side effect of StreamObjectAccel. On hardware the robot forwarded nothing after the cube connected (the CONTROL run). The engine-side StreamObjectAccel path is M9-017's, and S12 settles its bytes. | S12..S19 |

## Decisions (the manager's, recorded for audit)

- **MD1: the stack's direct-motion API models the game-message path.** Its defaults are the values the original app passes (Unity Robot.cs:1443-1450 and 1638-1646): head 10/20, lift 10/20, duration 0. Internal engine-style callers use the action defaults (MA9, MA13). This settles M2-015's residual: the MessageExtras defaults SetHeadAngle 10/10 and SetLiftHeight 3/20 are replaced by 10/20.
- **MD2: track locks** (MA2, MA3) belong to M4 (MovementComponent). The animation streamer's side of DisableAnimTracks / EnableAnimTracks is M5's.
- **MD3: engine-initiated sends are in scope** (operator: every layer is in scope). These are the Off and charging backpack lights, the cliff thresholds, PotentialCliff, SetBodyRadioMode, cube lights and AbsoluteLocalizationUpdate.
- **MD4: existing policies kept.**
  - M4-004 (the motion calibration gate) and M4-006 (the wheel tolerance) diverge from the engine. They were recorded as policies before this process. Both are reported to the operator as candidates to drop.
  - M4-007's extra DriveWheels(0) is **not** a policy; it is removed. The shutdown decision note (StopAllMotors plus DriveWheels(0) on Dispose) is a separate CozmoRobot.Dispose choice and is unchanged.
- **MD5: M1-041.** M4-020 establishes the AbsoluteLocalizationUpdate ids and the send, so the M4 batch implements the send at SyncTime (the M1 CD18 rows). If RobotStateHistory::Clear is also reproduced, M1-041 can be settled. The M1 "SyncTime stamp without AbsoluteLocalizationUpdate" decision note becomes obsolete.
- **MD6: the risk for the next hardware run.** With M4-020 faithful, if the robot does not echo the origin (M4-021), no RobotState is accepted, and CONNECT's "first full RobotState" criterion fails. That failure is the intended signal: it answers M4-021. It is recorded here so that the run's result is read correctly.

## Existing record evidence found too weak or contradicted

- **M4-005:** contradicted (id 0 is used).
- **M4-003:** the head 15/20 value is the action default, not the production caller's.
- **M4-002:** the preset ids are 0..3, and a negative height does not simply clamp to 32.
- **M4-007:** contradicted (only 0x3B is sent).
- **M4-001:** cited only the state-side clamp.
- **M4-009:** omitted the engine's moved and tap handling.
- **M4-010:** omitted the connection-time cube lights and had no test.
- **M4-005, M4-006 and M4-007:** had empty evidence.

## Corrections after the first freeze (manager, 2026-09-25, from the batch verifier's disassembly; re-approved under the standing authorisation)

- **C1 (M4-016): head move completion** (MoveHeadToAngleAction::CheckIfDone).
  - In-position is latched at +0xAC (0x005485E4..0x005485F4).
  - hasMoved (+0xAD) is set whenever MC+0xA (head moving) is set, before the in-position test (0x0054872E..0x00548734).
  - Success is latched-in-position and not moving.
  - Failure 0x04000004 is raised only when all three hold: not in position, not moving, and hasMoved (0x00548738..0x005488AC).
  - The lift equivalent (0x005493F6..0x00549508) has not been read, so M4-016 stays IMPLEMENTATION_GAP for the lift part.
- **C2 (M4-019): the 50 mm distance for the 400 threshold.**
  - It is the 3-D norm of the translation difference between two poses: MoveRobotPoseForward(pose, −20 mm, or the carry literal) taken before UpdateCurrPoseFromHistory, and the same taken after it (0x00512D48..0x00512D74).
  - It accumulates from the first matching state, because the −1 branch falls through to 0x00512DA6 (0x00512DD2..0x00512EA6).
  - The carry literal and the MoveRobotPoseForward body have not been read. M4-019 therefore stays IMPLEMENTATION_GAP for this part, and the stack must not claim it.
- **C3 (M4-020, M1-041): the first full state is marked before the origin check.** UpdateFullRobotState sets +0x34E right after the +0x29 time-sync gate (0x0051293C..0x00512948: `ldrb [r4,#0x29]; beq; movs r0,#1; strb.w r0,[r4,#0x34e]`). This is ahead of ContainsOriginID (0x00512C3E..0x00512C4A).
  - **Before the origin check, UFRS also stores:**
    - the head angle via RS6 (0x0051295A);
    - the lift angle (0x0051296A);
    - the cliff data, SC2 (0x0051298C);
    - the IMU filter (0x005129A4..0x00512A6E);
    - the treads state (0x00512A72);
    - the status bits and SetOnCharger (0x00512A96..0x00512ADC);
    - MovementComponent::Update (0x00512B5C);
    - SetBodyRadioMode (SC10).
  - **Only after the check** do the pose, history and later steps run.
  - **This corrects MD6.** A robot that does not echo the origin (M4-021) still gets Robot::Update running. It shows up as a "Received RobotState with originID" warning on every state, and as the 50 threshold never being sent. CONNECT's first-full-state criterion does not fail on it.
- **C4 (M4-022): the title's "consecutive" is wrong.** The counter increments while the bit is clear and is not reset while it is set (0x00512AE0 `bne 0x512b56` skips both the increment and the reset). The title is corrected to "after 16 RobotStates without IS_BODY_ACC_MODE (not reset while the bit is set)".
- **C5 (M4-002): an exact tie goes to 92.** For a negative height, 32 is chosen only when the current height is strictly nearer to 32 (0x00549104..0x0054913C: `vcmpe s0,s4; it mi; vmovmi`).

- **C6 (M4-016): lift completion,** from the M4 gap pass 2 (session scratch `extract/M4-gap2/report.md`, rows L1..L6).
  - **Init** clears hasMoved (+0x98) and sent/acked (+0x95/+0x96), and sets inPos (+0x97) = IsLiftInPosition(). Only if the lift is not in position does it send, with a send failure giving 0x03000016 (0x0054904C..0x0054932C).
  - **CheckIfDone:**
    - sent and not acked → Running (0x005493F6..0x00549402);
    - latch in-position (0x00549406..0x00549418);
    - hasMoved = 1 while MC+0xB (0x0054941C..0x00549428);
    - in position: Success if not moving, else Running;
    - not in position: moving → Running; not moving and hasMoved → 0x04000004; otherwise Running (0x0054942C..0x00549508).
  - **C1 correction:** the head latch also comes after the sent-not-acked test (0x005485D8..0x005485E2), and the head body has extra code at 0x005485F8..0x00548728 that has not been read.
- **C7 (M4-019): the 400-threshold distance** (rows D1..D5).
  - The distance is the XY displacement of the drive centre: MoveRobotPoseForward(pose, d) gives (x + d·cosθ, y + d·sinθ, 0) (0x00517ED0..0x00517F3E).
  - d = −20.0 mm when not carrying, and 0.0 when carrying (0x00512D14).
  - It is measured between the pose before UpdateCurrPoseFromHistory and the pose after it, on each state whose frame id matches (0x00512D48..0x00512E80).
  - This supersedes C2's open items.
- **C8 (M4-019, SC4j, SC9): the charger platform** (rows P1..P6).
  - SetOnChargerPlatform(b): new = b ? 1 : (on-contacts +0x338 ≠ 0). On a change it broadcasts RobotOnChargerPlatformEvent and sends 50 (new = 1) or 400 (new = 0) (0x00511D4C..0x00511DB0).
  - It is set true only by SetOnCharger on the first state with IS_ON_CHARGER, when +0x338 was 0 (0x00511BA8..0x00511C0E).
  - SetOnCharger(false) leaves it alone (0x00511A66..0x00511ACC).
  - It is set false by CheckAndUpdateTreadsState when the committed off-treads state changes to a value other than OnTreads (0x00512188..0x00512192), and by Robot::Update when no charger is located or the robot footprint no longer intersects the charger quad (0x00513C5C..0x00513E2A).
  - **The Robot::Update part stays open:** the charger quad (0x0087713A), the dock pose (0x004EA304) and the filter's origin scope are M11 geometry that has not been read.
  - The PotentialCliff platform exception (SC7) reads this flag.
- **C9 (M4-020, M1-041): localization follow-ups** (rows F1..F5).
  - Every AddVisionOnlyStateToHistory sets +0x2C6. Robot::Update then sends SendAbsLocalizationUpdate() and clears it (0x00513CBE..0x00513CCC), so a time-synced Delocalize sends twice.
  - UFRS's treads-path Delocalize fires when the committed off-treads state changes to or from OnTreads (0x00512A62..0x00512BAA). IS_CARRYING_BLOCK is only its argument.
  - These create new pose origins, which is M11's pose-frame interface. They are recorded here, but **M4-020's claim is limited to the connection-time origin 1 and the acceptance gate**.
- **Inventory table note:** the record table above predates C3 and C4. M4-020's and M4-022's rows read with these corrections.
- **Statuses frozen early:** the 2026-09-25 re-approval froze the implementer's EXACT_SOURCE statuses before verification had passed. Any record the verifier finds not ready is fixed before commit (it is not downgraded).

## Appendix A: M4 pass, extractor report

**Method.** libcozmoEngine.so 3.4.0-1204 was disassembled fresh with capstone. The tools and dumps are in `C:\Windows\TEMP\claude\...\b4cb80cd-...\scratchpad\extract\M4\`:
- `da.py`: range disassembler with resync.
- `xref.py`: BL/BLX/veneer xref, reusing the cubeaccel `bl.pkl`.
- `scan.py`: Thumb-2 imm12 load/store offset scanner.
- `vt.py`: vtable reader.
- Dumps: `mc.txt` (MovementComponent), `act.txt` (head and lift actions), `clc.txt` and `pla.txt` (CubeLightComponent), `ufrs.txt`, `rupd.txt`, `conn.txt`, `hconn.txt`, `bf.txt`, `dd.txt`, `cliffh.txt`, `pjp.txt`, `dfj.txt`.

The C# was read only to pick the behaviours.

**Interfaces used, not redone:**
- M2 inventory: App. A §2 builders, §3 finding 3; App. B RS0..RS14, the status-bit table, R-P3.
- M1 inventory: CD12, CD24, CD25, CD29.
- The cubeaccel report, rows S1..S17. Its open items S6, S8 and S17 are settled below as LC2..LC8.

---

#### A. Motion (MovementComponent and the head/lift actions)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| MA1 | **Game MoveHead and DriveWheels handlers.** These are the engine path that corresponds to the stack's direct commands. <br>1. Ignored while MC+0xD4 ("direct drive is disabled") is set. The ctor sets it to 0 at 0x0063DB0C. <br>2. Ignored when the track is locked by someone else and this direct drive does not hold it. <br>3. Otherwise DirectDriveCheckSpeedAndLockTracks runs, then the message is sent verbatim, reliable and not hot. <br>DriveWheels uses flag +0xB8 and BODY mask 4. MoveHead uses +0xB9 and HEAD mask 1. | DriveWheels 0x0063ED82..0x0063EE9A; "WheelsLocked" 0x0063EEA8 (strings 0x0063EF14, 0x0063EF40). MoveHead 0x0063F4F6..0x0063F5D2; "HeadLocked" 0x0063F5EA | NEW | EXACT_SOURCE. MoveLift is RECOVERABLE_GAP: read 0x0063F72C..0x0063F970 (expected +0xBA and mask 2, not read). |
| MA2 | **DirectDriveCheckSpeedAndLockTracks.** <br>- If \|speed\| < 1e-5: flag = 0, then UnlockTracks(mask) if all tracks in the mask are locked. <br>- Otherwise: flag = 1, then LockTracks(mask) if they are not all locked. <br>For DriveWheels the speed is \|l\|+\|r\|. | 0x0063EFB0..0x0063F0B8 (1e-5 at 0x0063F0F8); 0x0063EE32..0x0063EE56 | NEW | EXACT_SOURCE |
| MA3 | **LockTracks.** <br>- For each bit, it inserts a LockInfo. Bits whose lock set reaches size 1 are collected into a mask. <br>- A non-zero mask sends **DisableAnimTracks 0x9D {mask}**, reliable. <br>**UnlockTracks.** <br>- It erases the entry. Bits whose set becomes empty are collected. <br>- It sends **EnableAnimTracks 0x9E {mask}**. <br>Motion.cs never sends 0x9D or 0x9E. | Lock 0x006400D0..0x00640188 (size==1 at 0x0064014E..0x0064015C). Unlock 0x0063FE92..0x0063FFB4 (empty at 0x0063FF7A..0x0063FF88) | NEW | EXACT_SOURCE |
| MA4 | **StopHead, StopLift, StopBody.** <br>- First, if any of +0xB8..+0xBA is set and +0xD4 is 0, the track is unlocked with speed 0 (which may send 0x9E). <br>- Then StopHead sends MoveHead{0}, StopLift sends MoveLift{0}, and StopBody sends DriveWheels{0,0,0,0}. All are reliable and not hot. | 0x00640A08..0x00640AF6; 0x00640B3C..0x00640C2A; 0x00640C70..0x00640E4E | NEW | EXACT_SOURCE |
| MA5 | **StopAllMotors.** It runs the same unlock preamble for all three tracks, then sends **only StopAllMotors 0x3B**, with no DriveWheels. | 0x0063FBD8..0x0063FE10 → 0x0064099C..0x006409C2 | M4-007 | EXACT_SOURCE |
| MA6 | **StopAllMotors callers:** <br>- AbortAll 0x0051197C <br>- StopRobotForSdk 0x005296D6 <br>- PotentialCliff 0x00535972 <br>- PlaceObjectOnGround 0x00554894 <br>- CheckReactionTriggerStrategies 0x005A3616 <br>- the game message 0x0063FBD4 | xref | NEW | EXACT_SOURCE |
| MA7 | **~IActionRunner.** If the head is moving (MC+0xA) and the action's tag holds the head track, it calls StopHead. The same applies to the lift (+0xB, StopLift) and the body (+0xC, StopBody). | 0x0054112E..0x00541192 | NEW (M8 interface) | EXACT_SOURCE |
| MA8 | **Action-id counter.** <br>- MC+8 is 0 at construction (0x0063DA7C `str r0,[r4,#8]`). <br>- It is pre-incremented as a u8 in MoveLiftToHeight 0x0064070C..0x0064071A, MoveHeadToAngle 0x006407D8..0x006407E6, TurnInPlace (M2) and GetNextMotorActionID 0x006406EC..0x006406F4. <br>- The ids run 1..255, **0**, 1, …, so 0 is used once per 256. <br>- Head, lift and body share the counter. | as cited | M4-005 | EXACT_SOURCE |
| MA9 | **MoveHeadToAngleAction ctor.** <br>- Defaults: 15 rad/s, 20 rad/s², duration 0 (0x00547F14..0x00547F28). <br>- The angle is clipped to [−0.436332, 0.776672] with warnings (0x00547F44..0x0054803A). <br>- The tolerance minimum is 2° (0x0054803E..0x005480AC). <br>- A variability random value is added, then the angle is reclamped (0x005480BC..0x0054818A). <br>- It subscribes to 0xC4 (0x005481A0..0x005481BC). | as cited | M4-001, M4-003 | EXACT_SOURCE |
| MA10 | **Game SetHeadAngle.** <br>- It creates MoveHeadToAngleAction(angle, tolerance 0.0349066, variability 0). <br>- It then **overwrites +0x90..+0x98 with the message's max_speed, accel and duration.** | 0x0052ABE0..0x0052AC24 | M4-003 | EXACT_SOURCE |
| MA11 | **Values the original app (Unity) passes.** <br>- Head: speed 10, accel 20, duration 0 whenever the argument is ≤0 (Robot.cs:1443-1450). <br>- Lift: 10, 20, 0 (Robot.cs:1638-1646). <br>- ResetLiftAndHead: lift 10/20, head 15/20 (Robot.cs:1629-1630). | unity/scripts/csharp/Robot.cs | M4-003 | EXACT_SOURCE (authority 2) |
| MA12 | **Game SetLiftHeight.** <br>- If the height is exactly 32.0 and something is carried (CarryingComponent(+0x284)+8 ≠ −1), it runs **PlaceObjectOnGroundAction** instead. <br>- Otherwise it creates MoveLiftToHeightAction(h, tolerance 5.0, variability 0) and copies the message's speed, accel and duration to +0x8C, +0x90 and +0x88. | 0x0052AC40..0x0052AC9E | NEW (M12) | EXACT_SOURCE |
| MA13 | **MoveLiftToHeightAction.** <br>- ctor defaults: 0, 10, 20 (0x00548A68..0x00548A78). <br>- Init, height in [0,∞) but outside [32,92]: warning, then clamp (0x0054905E..0x005490F6). <br>- **Init, height < 0: nearest of preset 0 (32) or preset 2 (92) to the current height** (0x005490FA..0x00549140). <br>- Variability is applied. <br>- The tolerance is clipped so that the angular tolerance is ≥1.5° (0.0261799; 0x005491D6..0x005492EE). | as cited | M4-002, M4-003 | EXACT_SOURCE |
| MA14 | **Lift preset table** at 0x00C54684: key 0 LowDock 32, key 1 HighDock 76, key 2 HeightCarry 92, key 3 OutOfFOV −1. Names at 0x00548EBC..0x00548EDC. | as cited | M4-002 | EXACT_SOURCE |
| MA15 | **Nothing is sent if the motor is already in position.** <br>- Head: IsHeadInPosition is IsNear(robot+0x2FC, target, tolerance+1e-5) (0x005484F4..0x0054852C). When true, Init sets +0xAC and does not call MoveHeadToAngle (0x00548544..0x0054854E). <br>- Lift: IsLiftInPosition is \|target − height\| < tolerance **and** MC+0xB == 0 (0x00548FEA..0x00549034). When true, Init sets +0x97 and sends nothing (0x005492F2..0x005492FE). | as cited | NEW | EXACT_SOURCE |
| MA16 | **Ack matching.** The ack is used only when the action's "sent" flag is set and the ack byte equals the action's id. No other filter applies. <br>- Head lambda 0x0054D624: +0xAA/+0xA9, sets +0xAB. <br>- Lift lambda 0x0054D748: +0x95/+0x94, sets +0x96. <br>- R-P3 (0x0054D3F8, +0xDB/+0xDA) is **TurnInPlaceAction's** (typeinfo string at 0x0054D4BE). | as cited | NEW (settles the R-P3 owner) | EXACT_SOURCE |
| MA17 | **Completion (head).** <br>- Running until acked. <br>- After the ack, success once in position and MC+0xA == 0. <br>- Failure 0x04000004 "StoppedMakingProgress" when it is not in position and has stopped after having moved (+0xAD). <br>- A send failure gives 0x03000016. <br>**Lift:** the same, using +0x98 and MC+0xB. | 0x005485D8..0x005488B6; 0x005493F6..0x00549508; 0x00548574, 0x00549320 | NEW | EXACT_SOURCE. The IAction timeout is M8 and was not read. |
| MA18 | **StartMotorCalibration is never sent automatically.** <br>- Its only senders are CalibrateMotors (called only from CalibrateMotorAction::Init) and BehaviorFactoryCentroidExtractor 0x005CFAC2. <br>- The action is created by the game RobotActionUnion (0x00529D38) and by the behaviours ReactToPickup 0x00607C9A, PlacedOnSlope 0x0060808C, ReturnedToTreads 0x00608658 and RobotOnBack 0x00608892. | xref | M4-012 | EXACT_SOURCE |
| MA19 | **CalibrateMotorAction::CheckIfDone.** Each requested motor needs a calibStarted report seen for it (+0x7A for motor 3, +0x7B for motor 2, set in 0x00547E20..0x00547E3C) and its IsHead/LiftCalibrated. So byte0 is head and byte1 is lift. | 0x00547D38..0x00547DC2 | M4-012 | EXACT_SOURCE |
| MA20 | **HandleMotorCalibration** confirmed as M4-004 describes. When the lift starts calibrating while carrying, it calls SetCarriedObjectAsUnattached(**true**). | 0x00536BAE..0x00536C0E | M4-004 | EXACT_SOURCE |
| MA21 | **No calibration gate anywhere.** <br>- A complete imm12 scan of Robot+0x314/+0x315 finds these readers only: CheckAndUpdateTreadsState 0x00511E1C, IsHeadCalibrated 0x00512378, SetHeadAngle (RS6) 0x0051335E, IsLiftCalibrated 0x005151A6. <br>- Writers: the ctor 0x005100A0 (both 0), and SetHeadCalibrated/SetLiftCalibrated. | scan.py | M4-004 | EXACT_SOURCE |
| MA22 | **Before calibration the head angle reads −25°.** Robot+0x2FC is −0.436332 in the ctor (0x0051007C..0x00510086), and RS6 ignores reported angles until calibration. So the head in-position test (MA15) uses −25° until then. | as cited | NEW | EXACT_SOURCE (derived) |
| MA23 | **State-side head clamp:** RS6 (M2 interface). | M2 App. B | M4-001 | EXACT_SOURCE (interface) |

#### B. Lights

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| LB1 | **BodyLightComponent::Update runs every Robot::Update after the first state** (0x00514470). <br>1. It runs UpdateChargingLightConfig. <br>2. best = the first non-empty source in priority order {1,0,2} (0x00C7C800). <br>3. If best has changed, it applies best, or Off when best is null. <br>4. **If both best and the previous are null, it sends Off again every tick.** | 0x00631D54..0x00631DDE; 0x00631E30..0x00631EB4 | CD29 | EXACT_SOURCE |
| LB2 | **The Off lights.** <br>- Every colour is rev(BLACK). BLACK is bytes 00 00 00 FF at 0x00C9744F, so the colour is 0x000000FF and the word is 0x8000. <br>- All periods and offsets are 0, so every frame count is 0. | 0x0063221C..0x00632260 | NEW | EXACT_SOURCE |
| LB3 | **SetBackpackLightsInternal** sends 0x03 and then 0x11, both reliable (M2 §2). | 0x00632186..0x006321BE | M2-005, NEW | EXACT_SOURCE |
| LB4 | **Charging state.** <br>- On the charger: OOS gives 3; otherwise IS_CHARGING gives 1, else 2. <br>- Off the charger: battery < 3.5 V gives 3, otherwise 0. <br>- The initial state is 0 (0x006319B2). <br>- On a change, StartLooping plays the anim named table[state^2] (0x0102EA18: OffCharger, Charging, Charged, BadCharger) at source 2. | 0x00631B94..0x00631C36 | NEW | EXACT_SOURCE. **Whether the names resolve is RECOVERABLE_GAP:** backpackLightPatterns.json keys are "charging", "charged" and "badCharger", and there is no "OffCharger". Read BackpackLightAnimationContainer::GetAnimation/DefineFromJson. |
| LB5 | **Game SetBackpackLEDs** loops the lights at source 2. | 0x006323C4..0x0063244E | NEW | EXACT_SOURCE |
| LB6 | **Headlight:** EnableMode(14), then the send. The game message tail-calls it. | 0x00632344..0x00632374; 0x00632456 | NEW | EXACT_SOURCE |
| LC1 | **Game-side ObjectConnectionState handler.** <br>- Disconnected: it returns at once. Nothing is sent and nothing is erased. <br>- Connected light cube: it emplaces ObjectInfo{layer 2, gameLayerOnly = comp+0x22 (0 at ctor 0x0063710A)}. An existing key is **not overwritten**. It then calls PlayLightAnim(WakeUp 0x26, layer 2). | 0x00639CE4..0x00639D8A | NEW (S5) | EXACT_SOURCE |
| LC2 | **Item 1: PlayLightAnim gates**, in order: <br>(a) The ObjectInfo must exist, otherwise "InvalidObjectID" and return 0 (0x006382EA..0x006383BA). <br>(b) The top of the layer stack must allow overriding (+0x24 ≠ 0), otherwise "CantBeOverridden" (0x006382FC..0x0063836E). <br>(c) The anim lookup is GetCubeAnimationForTrigger → CubeLightAnimationContainer::GetAnimation; null gives "NoAnimForTrigger" (0x006383C2..0x00638456). <br>(d) The layer rule: gameLayerOnly with layer≠0 refuses as "OnlyGameLayerEnabled", and !gameLayerOnly with layer 0 refuses as "NotPlayingUserAnim" (0x006383DE..0x00638484). <br>(e) A blended anim over a blended anim is refused (0x00638510..0x0063853A). <br>Then it pushes a CurrentAnimInfo: timer = now + the first pattern's duration (+ modifier), and canBeOverridden = the first pattern's (0x006386B8..0x006386F4). <br>(f) If layer > the current layer: "LightsNotSet" and no send. Otherwise current layer := layer, then SendTransitionMessage (game-only, gated by comp+0x20) and SetObjectLights(first pattern) (0x006386F8..0x006387D8). <br>Layers are 0 User/Game, 1 Engine, 2 State (0x0102EE64). | as cited | NEW (settles S6) | EXACT_SOURCE |
| LC3 | **Item 1: WakeUp content.** <br>Trigger 0x26 maps to "wakeUp" (CubeAnimationTriggerMap.json:156-157). The file is cubeLights/wakeUp.json: <br>1. "wakeUp_spin": on [0,255,0,255]×4, off [0,0,0,255]×4, on 10, off 380, transOn 200, transOff 50, offset [0,100,200,300], rotation 0, **1280 ms**, canBeOverridden default true. <br>2. "wakeUp_fadeOut": on green, off black, on 1000, off 3000, transOn 100, transOff 1000, offset 0, rotation 0, **5100 ms**, canBeOverridden false. <br>**There is no loop.** <br>Parser: duration_ms is required, canBeOverridden defaults to 1 (0x00589B5C..0x00589BBE), rotationPeriod_ms is required, and makeRelative is 0 (0x0058948A..0x005894C6). <br>Update advances to the next pattern when its timer expires (0x00637998..0x006379B8, 0x00637B3A..0x00637B66). It pops the anim after the last pattern (0x006379BC..0x006379DA). When the layer is empty: layer := 2 and PickNextAnimForDefaultLayer runs (Update(true) at 0x00514466), choosing, on layer 2: <br>- Sleep or SleepNoFade if the cube-sleep flags are set; <br>- Carrying (0) if the cube is the carried object; <br>- Visible (0x25) if the located object's +0x24 == 1; <br>- otherwise Connected (1). <br>(0x00637D4C..0x00637E02.) <br>The only writer of canBeOverridden is 0x006386F4 (from the first pattern), so fadeOut's false is never applied. | as cited | NEW | EXACT_SOURCE (the asset content is authority 3) |
| LC4 | **Item 2: SetObjectLights.** <br>1. Object lookup: if makeRelative, the located object, else the connected object; dynamic_cast to ActiveCube. If there is none, it sends nothing (0x00637E6C..0x00637EB0, 0x00637EFC). <br>2. ActiveObject::SetLEDs copies each LED, with these rules: <br>- both periods 0: colours 0, periods 0x7FFFFFFF; <br>- on = 0: onColor := offColor, on := 0x7FFFFFFF; <br>- off = 0: offColor := onColor, off := 0x7FFFFFFF. <br>3. **Gamma is set to 0x80** (0x004E48B0..0x004E49A2; RecomputeGamma 0x004E4830 does the same). <br>4. MakeStateRelativeToXY with mode 0 returns (0x004E0DF4 → 0x004E100C). <br>5. SetLights(obj, rotation from ObjectLights+0x70). | as cited | NEW | EXACT_SOURCE |
| LC5 | **Item 2: SetLights.** <br>- LEDs 0..3 go in index order (obj+0xC+0x1C·i, 0x006395F4). <br>- Colour: the word packs R5<<10, G5<<5, B5, plus 0x8000 when alpha≠0, after WhiteBalanceColor. WhiteBalanceColor scales G and B by 0.6 (truncating) when R≠0 (0x0063A894..0x0063A98E; packing 0x0063A654..0x0063A6D8). <br>- On, off, transOn and transOff frames are the u8 of (ms+29)/30, truncated; 0xFFFFFFFF gives 0xFF (0x0063A6DA..0x0063A738). <br>- The offset is i16, signed (x+29)/30 (0x0063A73A..0x0063A75A). <br>- **SetCubeGamma{gamma}** is sent only when gamma ≠ the cache at comp+0x28, which is 0 at ctor 0x00637106. So it goes out on the first SetLights (0x0063A764..0x0063A792). <br>- Then **CubeID{u32 activeID = vbase+0x40, u8 rotation = (ms+29)/30}** (0x0063A794..0x0063A7D2). <br>- Then **CubeLights{40 bytes}** (0x0063A7DC..0x0063A800). <br>- All are reliable, not hot. <br>- A "solid" LED period of 0x7FFFFFFF gives frame byte 0x45. | as cited | NEW (settles S8) | EXACT_SOURCE |
| LC6 | **WakeUp on the wire, derived.** <br>1. On connection: SetCubeGamma{80}, then CubeID{slot, 00}, then CubeLights with per-LED bytes `E0 83 00 80 01 0D 07 02 oo 00`, where oo = 00, 04, 07, 0A. <br>2. After 1280 ms: CubeID{slot, 00} + CubeLights `E0 83 00 80 22 64 04 22 00 00`×4. <br>3. After a further 5100 ms: the default-layer anim. <br>Timing has the granularity of the 60 ms tick. | LC2..LC5 + wakeUp.json | NEW | EXACT_SOURCE (derived) |
| LC7 | **Item 3: delivery of the game-side broadcast.** <br>- Robot::Broadcast returns 0 **without delivering to anyone** if context+4 is null (0x00511DE8..0x00511DF8). <br>- Otherwise it calls vtable+0x18 = UiMessageHandler::Broadcast(E2G const&) 0x00662508 (vtable relocs at 0x0102FE1C..). <br>- That calls DeliverToGame, which packs and sends to each non-null UI connection (0x00661594..0x0066166C). <br>- It **then emits the AnkiEvent synchronously to engine subscribers** (0x00662528..0x006625A8). <br>So WakeUp runs with no game attached, provided the UiMessageHandler exists. The subscription is made only if HasExternalInterface at construction (0x00637114..0x0063713C). | as cited | NEW (settles S17) | EXACT_SOURCE. That context+4 is always set in the shipped engine was not traced (engine-init interface). |
| LC8 | **Item 4: disconnect and reconnect.** <br>- On disconnect: no lights are sent. The ObjectInfo is kept (no erase found in 0x006370E8..0x0063AB00; named calls only). <br>- On reconnect: S1 broadcasts again. The emplace is a no-op if the ObjectInfo exists, and PlayLightAnim(WakeUp) runs again. <br>- If the ObjectID is the same and WakeUp is still on top of layer 2 (overridable, not blended), a second WakeUp is pushed and **sent immediately**. SetCubeGamma is not resent because the cache already holds 0x80. <br>- If the ObjectID is new, it also replays. **So yes, WakeUp is replayed.** | as cited | NEW | EXACT_SOURCE. Whether AddConnectedActiveObject reuses the ObjectID is RECOVERABLE_GAP (M11): read 0x00623426.., 0x00623584.., 0x006236FE.. and RemoveConnectedActiveObject 0x006243A0. |

#### C. Sensors

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| SC1 | **CliffSensorComponent ctor:** enabled (+4) = 1, +5 = 0, threshold cache = 400, raw values 0xFFFF. | 0x00633FA0..0x00633FCE | NEW | EXACT_SOURCE |
| SC2 | **UpdateRobotData:** the raw values go to +0xE, the CLIFF_DETECTED bit to +6, the timestamp to +8. | 0x00634016..0x00634036 | M4-008 | EXACT_SOURCE |
| SC3 | **SendCliffDetectThresholdToRobot(t)** stores t when it differs from the cache, and **always** sends SetCliffDetectThreshold{u16}, reliable. | 0x00634270..0x006342CE; 0x0063433C..0x00634368 | NEW | EXACT_SOURCE |
| SC4 | **Threshold schedule.** <br>- The ctor sets +0x528 = −1.0 and +0x52C = 0 (0x0050FEEA..0x0050FEFC, 0x0051023C). <br>- The logic runs in UpdateFullRobotState for a state whose frame id equals Robot+0x2B0 (0x00512BB4..0x00512BBA, 0x00512D7A) while not on a ramp (+0x316, 0x00512BE2). <br>- The first time, it **sends 50** (0x00512D80..0x00512DA2). <br>- It accumulates \|Δxy\|. After more than 50 mm, and only once, it **sends 400** (0x00512E78..0x00512EA6). <br>- A change of SetOnChargerPlatform sends 50 on the platform and 400 off it (0x00511D66..0x00511DA0). | as cited | CD24 | EXACT_SOURCE. The sends from IncrementSuspiciousCliffCount 0x006345A4 and ClearCliffRunningStats 0x006347F8 are RECOVERABLE_GAP: read 0x00634514..0x00634840. |
| SC5 | **CliffEvent 0xC0.** <br>- If the sensor is enabled: flags≠0 runs ComputeCliffPose and AddCliff. <br>- **If it is disabled and flags≠0, the event is dropped with no broadcast.** <br>- Otherwise +5 = (flags≠0) and it broadcasts to the game. <br>- There is no robot send. | 0x00535548..0x005356E6 | M4-008 | EXACT_SOURCE |
| SC6 | **Game EnableCliffSensor** sets +4, engine-side only. | 0x00527E84.. | NEW | RECOVERABLE_GAP (the store was not reached; read 0x00527EC0..0x00527F18) |
| SC7 | **PotentialCliff 0xC1.** <br>- On the charger platform (+0x34A): ignored. <br>- Drone mode (+0x34B): TriggerLiftSafeAnimationAction with trigger 0x9B, unless the current action type is 0x1B. <br>- Otherwise, when not in SDK mode: **StopAllMotors + EnableStopOnCliff{0}**. <br>- In SDK mode: nothing. | 0x0053582C..0x00535998 | NEW | EXACT_SOURCE |
| SC8 | **EnableStopOnCliff senders:** <br>- the game message (verbatim); <br>- EnableDroneMode, which sets +0x34B = e and sends !e (0x00516BEE..0x00516C0E); <br>- PotentialCliff; <br>- PopAWheelie 0x005C76E4/0x005C7BFE; <br>- FeedingEat 0x005D5E8C/0x005D6166. <br>**Nothing is sent at connection.** | xref | NEW | EXACT_SOURCE. The robot's default is HARDWARE_ONLY. |
| SC9 | **Charger.** SetOnCharger (0x00511990..0x00511C14, from 0x00512AB4) creates or locates the charger and broadcasts ChargerEvent. Its only robot effect is through SetOnChargerPlatform (also called by Robot::Update 0x00513E0E..0x00513E2A). | as cited | NEW (M13) | EXACT_SOURCE for the sends |
| SC10 | **SetBodyRadioMode auto-send** (CD25, 0x00512AD0..0x00512B52). The stack never constructs it. | M2 | NEW | EXACT_SOURCE (interface) |
| SC11 | **Off-treads, falling, pickup and unexpected movement** are M10 interfaces: 0x00511E00, 0x0063E398, 0x00534F3C, 0x00535040. | — | M10 | interface |

#### D. Cubes

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| CD1 | An advertisement is dropped once it is ≥10001 ms old; ObjectUnavailable is broadcast only behind +0x490. | 0x00514174..0x00514224 | M4-010 | EXACT_SOURCE |
| CD2 | **Update order:** BlockFilter 0x0051422A → CheckDisconnected 0x00514230 → ConnectToRequested 0x00514236 → CubeLight::Update(true) 0x00514468 → BodyLight 0x00514470. | as cited | M4-010, NEW | EXACT_SOURCE |
| CD3 | **BlockFilter::Update** runs only when enabled (+0x7C), at most every 2 s, and calls Discovering then Connecting. | 0x0061A79A..0x0061A7E0 | M4-010 | EXACT_SOURCE |
| CD4 | **UpdateConnecting** runs ≥5 s after the last connect. A pooled id that no slot holds is swapped only for a **different** closest cube (RSSI 150). IsConnectedToObject matches the slot's factory id in any state. So **after a slot reset the same cube is never re-requested.** | 0x0061AA8C..0x0061AB88; 0x005179A4..0x005179BE | M4-010 | EXACT_SOURCE |
| CD5 | **ConnectToRequestedObjects**, spot-checked against the M4-010 claims: they match. | 0x00514A88..0x00514C6C | M4-010 | EXACT_SOURCE |
| CD6 | **HandleConnectedToObject.** If the factory id matches: a state other than 1 or 4 logs an error but is handled; the entry is erased from the available map; the state becomes 2. Otherwise the connection is ignored. | 0x005179DA..0x00517AA6 | M4-010 | EXACT_SOURCE |
| CD7 | **HandleDisconnectedFromObject.** State 3: the slot is reset. Otherwise the state becomes 4 with a timestamp. A state other than 2 or 3 logs an error. | 0x00517BD8..0x00517D14 | M4-010 | EXACT_SOURCE |
| CD8 | **CheckDisconnectedObjects** runs at most every 2 s and resets a state-4 slot more than 2 s old. | 0x00514924..0x00514A0E | M4-010 | EXACT_SOURCE |
| CD9 | **The hardware disconnect and reconnect ~0.5 s apart.** <br>- Engine: the slot goes 2 → 4, and no SetPropSlot is sent. The reconnect (state 4 is valid) sets it back to 2 before the reset. BlockWorld then removes and re-adds the object, and WakeUp replays (LC8). <br>- If the gap were over roughly 2–4 s, the engine would not re-request the cube (CD4). <br>- Bundle 20260924-202748-CONTROL shows one SetPropSlot (at 11433) and D0 messages at 13183, 18694 and 19197. So the robot reconnected on its own. | as cited | NEW | EXACT_SOURCE for the engine; the robot-side reconnect is HARDWARE_ONLY (observed once) |
| CD10 | **Moved, Stopped and UpAxis handlers** (0x00533E30, 0x0053461C, 0x00534D50) were not read beyond the active-id lookup. Tapped has no RobotToEngineImplMessaging handler; it goes to BlockTapFilterComponent (M10/M12). The stack's Moving and Taps are its own state. | handler list 0x0053330C..0x00537130 | M4-009 | RECOVERABLE_GAP (read those ranges) |

---

#### Existing records contradicted by the source

- **M4-005 ("ids cycle 1..255").** The engine uses id 0 (MA8), and 0 comes after 255. The stack's comment "0 is left alone because the robot uses it for unsolicited acks" has no engine support. The record's evidence field is empty.
- **M4-003, head default.** 15/20 is only the action-ctor default that engine-internal callers get.
  - On the game path, which is the stack's API equivalent, the caller's values overwrite it (MA10).
  - The original app passes head 10 rad/s and 20 rad/s² (MA11).
  - The lift's 10/20 agrees with both.
  - Also missing from what the stack does: the no-send when already in position (MA15), and completion meaning in position and stopped, not the ack (MA17).
- **M4-002.** The title claim holds, but:
  - the preset ids are 0/1/2 plus 3 = OutOfFOV −1, not 1/2/3;
  - for a negative height the engine chooses the nearer of 32 and 92, while the stack clamps to 32 (MA13);
  - on the game path, exactly 32 mm while carrying becomes PlaceObjectOnGround (MA12).
- **M4-007.** The engine StopAll path sends only 0x3B, after the track unlocks (MA5). The extra DriveWheels(0) is not in the engine. The record is a policy, so it still stands, but its "authority" now has a citation.

#### Existing records whose evidence is too weak

- **M4-001** cites only the state-side clamp (Robot::SetHeadAngle). The command-side clamp the stack applies is MoveHeadToAngleAction 0x00547F44..0x0054803A.
- **M4-003** proves the action default, not the production caller.
- **M4-005, M4-006 and M4-007** have empty evidence. MA2 and MA3 show that direct drive locks the animation tracks and sends 0x9D/0x9E, which the stack does not do.
- **M4-010.**
  - The slot, filter and timer claims check out (CD3..CD8).
  - Its path omits the connection-time cube-light sends (LC1..LC6).
  - It has no regression test.
  - M4-011's Load/Save and hash-order claims were **not** re-verified.
- **M4-008 and M4-009** were not re-verified beyond the offsets used above.
- **M4-004 and M4-012** are supported and strengthened (MA18..MA21).

#### Open questions for the manager

1. **Which original caller do CozmoMotion.SetHeadAngleAsync and SetLiftHeightAsync model?** The game path gives head 10/20 (Unity); the engine action gives 15/20. The same question covers reproducing "no send when in position" and the completion semantics.
2. **Do direct-drive track locks (0x9D/0x9E, MA2..MA4) belong to M4 or M5?** They interact with animation streaming.
3. **Scope of new M4 engine-initiated sends the stack lacks:**
   - the periodic Off backpack lights and the charging configs (LB1..LB4);
   - SetCliffDetectThreshold 50/400 (SC4), which depends on the pose frame id (M1-041/M11);
   - PotentialCliff handling (SC7);
   - SetBodyRadioMode (SC10);
   - the CubeLightComponent: WakeUp on connection, the default-layer anims, SetCubeGamma/CubeID/CubeLights (LC1..LC8).
4. **RECOVERABLE items to send back:**
   - the backpack anim name casing (LB4);
   - the suspicious-cliff threshold sends (SC4);
   - the EnableCliffSensor store (SC6);
   - MoveLift direct-drive (MA1);
   - BlockWorld ObjectID reuse on reconnect (LC8, M11);
   - the Moved/Stopped/UpAxis handlers (CD10).
5. **HARDWARE_ONLY:**
   - the robot's default stop-on-cliff state;
   - the robot's own cube reconnect without SetPropSlot;
   - M4-013 (unchanged).

## Appendix B: gap pass, extractor report

All six RECOVERABLE_GAP items are now settled from libcozmoEngine.so. Nothing was written under the repo root. Scratch dumps are in `C:\Windows\TEMP\claude\...\b4cb80cd-...\scratchpad\extract\M4-gap\`: bpc.txt, addbp.txt, cliff.txt, ufrs.txt, moved.txt, stopped.txt, btf.txt, acao.txt, rcao.txt, robot_all.txt, plus hx.py and vt2.py (a vtable reader that resolves relocations).

Three things you should know first:
- **SC4:** with this stack the first "send 50" never happens under the engine's own logic. Robot state is rejected at the origin check before the frame-id test is reached.
- **LB4:** all four backpack animation names resolve. The engine adds them itself; they are not looked up in the JSON by the table names.
- **LC8:** a light cube keeps the same ObjectID for the whole process lifetime.

#### 1. LB4 – backpack charging lights

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| LB4a | **Name table.** The table at 0x0102EA18 holds [Charged, BadCharger, OffCharger, Charging], not the order the M4 report gave. It is indexed by state^2, so state 0 → OffCharger, 1 → Charging, 2 → Charged, 3 → BadCharger. This agrees with the StateToString table at 0x0102EA28. | relocations at 0x102EA18..0x102EA24; `eor r8,r0,#2` at 0x00631BD0; 0x00631D10 | NEW | EXACT_SOURCE |
| LB4b | **How the names get into the container (DefineFromJson).** <br>1. It adds "OffCharger" itself, with the value GetOffBackpackLights() (the Off lights). <br>2. It then calls AddBackpackLightStateValues three times: ("Charging", json["charging"]), ("Charged", json["charged"]), ("BadCharger", json["badCharger"]). <br>So the container keys are the capitalised engine names. The JSON keys are only read here. | 0x00587558..0x00587638; strings 0x00587678..0x005876B8 | NEW | EXACT_SOURCE |
| LB4c | **Lookup.** GetAnimation tail-calls GetAnimationHelper through veneer 0x8CB97C, which does an unordered_map<string,…> find. That is exact and case-sensitive (hash, then length and memcmp, as in the inlined copy at 0x00586FCE). <br>- No match: error "InvalidName" and return null. <br>- Null in UpdateChargingLightConfig: a warning, nothing plays, but the new state is still stored (+0x38 is written at 0x00631BD8, before the lookup). <br>With the shipped file, all four names resolve. | 0x00587434..0x005874A4; 0x00631C10..0x00631C52 | NEW | EXACT_SOURCE |
| LB4d | **Parsing a JSON entry (AddBackpackLightStateValues).** <br>- Fields: onColors, offColors, onPeriod_ms, offPeriod_ms, transitionOnPeriod_ms, transitionOffPeriod_ms, offset. <br>- Each must have exactly 5 entries. A missing field or wrong size gives the error "Missing member field…" and the entry is not inserted. An existing key is not overwritten. <br>- Colours are floats: byte = u32(f·255), truncated (0x0083F51A..), stored as 0xRRGGBBAA after `rev` (0x005872A6). <br>- Struct layout: on +0, off +0x14, onP +0x28, offP +0x3C, tOn +0x50, tOff +0x64, offset +0x78. | 0x00586D20..0x005870D0; 0x005871B4..0x00587308 | NEW | EXACT_SOURCE |
| LB4e | **Loading.** RobotDataLoader::LoadBackpackLightAnimationFile calls DefineFromJson under a mutex, for the files in "config/engine/lights/backpackLights" (the string is at 0x0051F830). The only shipped file there is backpackLightPatterns.json. | 0x00521D0E..0x00521D42 | NEW | EXACT_SOURCE; authority 3 for the file set |
| LB4f | **Wire conversion (SetBackpackLightsInternal).** No white balance is applied. <br>- Colour word: R5<<10 \| G5<<5 \| B5, plus 0x8000 when alpha≠0. <br>- Frames: u8((ms+29)/30), with 0xFFFFFFFF → 0xFF. <br>- Offset: i16, signed and truncated ((x+29)/30), with −1 → 0x00FF. <br>- LEDs 1, 2, 3 go to 0x03 BackpackLightsMiddle (3 × 10 bytes + u8 0). LEDs 0 and 4 go to 0x11 TurnSignals (2 × 10 bytes + u8 0). Both are reliable, 0x03 first. | 0x00631F3A..0x006321BE | M2-005 | EXACT_SOURCE |
| LB4g | **Charging content on the wire** (derived from LB4b..f and the asset; one LED = 10 bytes: onColor, offColor, on, off, tOn, tOff, offset). The unlit LEDs 0 and 4 are always `00 80 00 80 00 00 00 00 00 00`. <br>- **Charging:** L1 `E0 81 00 80 0A 1E 0A 0A E3 FF`; L2 `E0 81 00 80 1E 0A 0A 0A F7 FF`; L3 `E0 81 E0 81 00 00 0A 0A 00 00`. <br>- **Charged:** L1–L3 `E0 81 E0 81 00 00 00 00 00 00`. <br>- **BadCharger:** L3 `00 FC 00 80 14 14 0A 0A 00 00`; L1 and L2 unlit. | backpackLightPatterns.json:3-33 plus the above | NEW | EXACT_SOURCE (derived); how the robot renders it is HARDWARE_ONLY |
| LB4h | **OffCharger and "no anim" (LB2).** <br>- Every colour is rev(BLACK 00 00 00 FF) = 0x000000FF, so every word is 0x8000. <br>- memclr 0x64 from +0x28 zeroes all periods and offsets. <br>- Every LED is `00 80 00 80 00 00 00 00 00 00`. <br>- The ctor state is 0, so no config exists at connection when off the charger. Update then resends Off on every tick (LB1). <br>- Once OffCharger has been looped at source 2 (for example after leaving the charger), best is non-null and unchanged, so the per-tick Off resend stops. | 0x0063221C..0x00632260; 0x00631D98..0x00631DB8 | NEW | EXACT_SOURCE |
| LB4i | **Shared locator.** StartLoopingBackpackLightsInternal first calls StopLooping(locator), then emplace_front at the source. The charging config, SetBackpackLights and the game SetBackpackLEDs all use one locator (comp+0x24) and source 2. So a charging-state change replaces the game's LEDs, and the reverse. | 0x006322A4; 0x00631C12..0x00631C1C; 0x00631D26; 0x00632432..0x0063244E | NEW | EXACT_SOURCE |

#### 2. SC4 – cliff threshold schedule

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| SC4a | **IncrementSuspiciousCliffCount.** <br>- Returns if the threshold cache (+0xC) is < 151. <br>- Otherwise: count++, then new = cache − 250, clamped up to 150 if ≤ 150 (unsigned compare). Store it, log, SendCliffDetectThresholdToRobot(new), count = 0. <br>- From 400 this sends **150**. <br>- The only values that reach the cache are 400, 50 and 150, so the u16 wrap for a cache of 151..249 is unreachable. | 0x00634514..0x006345AE | CD24 | EXACT_SOURCE |
| SC4b | **Who calls IncrementSuspiciousCliffCount.** Only UpdateCliffDetectThreshold (0x0063448A), which UFRS calls (0x00512FD6). It runs only when: <br>- +0x1C ≠ 0, set to the last state timestamp by EvaluateCliffSuspiciousnessWhenStopped (0x00634840) from HandleRobotStopped (0x0053542E); <br>- and MC+0xC (body moving) is 0. <br>It walks the history from lower_bound(+0x1C) to the end, updating min(cliff[0]) first. It calls Increment when variance +0x3C > 10000 **and** min+15 < cliff[0]. It then clears +0x1C. | 0x006343B8..0x006344B0 | CD24 | EXACT_SOURCE |
| SC4c | **The running statistics (UpdateCliffRunningStats, from UFRS 0x00512FCE).** <br>- A sample is taken only when the body is moving, off-treads state robot+0x355 is 0, and cliff[0] (state+0x50) > the cache. <br>- Sliding window of 100, Welford mean +0x38, M2 +0x40; variance +0x3C = M2/(n−1) once n ≥ 2. | 0x00634630..0x00634724 | NEW | EXACT_SOURCE |
| SC4d | **ClearCliffRunningStats.** <br>- count = 0. <br>- If the cache ≠ 400: cache = 400, event "RestoringCliffDetectThreshold", **send 400**. <br>- Clears the deque, mean, variance and M2. <br>It is called only from Robot::Delocalize (0x00510A5A). Delocalize is called from the Robot ctor (0x00510324, when the cache is 400, so nothing is sent), from UFRS on the treads/carry path (0x00512BA6), and from UFRS after ≥101 mismatched frame ids (0x00512F82..0x00512F96). | 0x006347A4..0x0063480C | CD24 | EXACT_SOURCE |
| SC4e | **The frame-id condition and first send.** <br>- robot+0x2B0 is 0 in the ctor (0x0050FF02, and again at 0x0051032C after the ctor's Delocalize). <br>- Its only other writer is AddVisionOnlyStateToHistory, which does ++ (0x00515150..0x00515152) and is called from SetNewPose, LocalizeToObject and LocalizeToMat. <br>- robot+0x528 = −1.0 and +0x52C = 0 are set only in the ctor, so each happens once per Robot object: "send 50" on the first matching state, and "send 400" once after more than 50 mm. | as cited; 0x00512D7A..0x00512DA2; 0x00512E84..0x00512EA6 | CD24 | EXACT_SOURCE |
| SC4f | **Gates in front of the frame test** (the M4 report's "+0x316 not on a ramp" gate is wrong). <br>- UFRS does nothing unless robot+0x29 (time-synced) is set. It is set to 1 by HandleSyncTimeAck (0x005366AC) and cleared by SyncTime (0x00515228). <br>- +0x316 is SetOnRamp. On a ramp, the state goes to history with a default pose. Off a ramp, **ContainsOriginID(state+8)** must pass (0x00512C3E..0x00512C4A); otherwise the warning "Received RobotState with originID…" and the state is dropped, with no send-50 and no cliff statistics. <br>- Both branches reach the frame compare at 0x00512D7A. | 0x0051293C..0x00512942; 0x00512BE2..0x00512C4A; 0x00512EC4..0x00512F12 | CD24 | EXACT_SOURCE; contradicts the M4 report |
| SC4g | **Origin ids are never 0.** <br>- PoseOriginList ctor: next id = 1 (0x0084792C..0x0084793C). <br>- AddNewOrigin uses and advances it. AddOriginWithID's only caller is AddNewOrigin. <br>- UnknownOriginID = 0 (0x00C97B20). <br>So origin 0 is never in the list. | 0x00847A84..0x00847B6C | NEW (M11 interface) | EXACT_SOURCE |
| SC4h | **What the original sends at connection.** SendSyncTime sends SyncTime, InitController and ImageRequest, then SendAbsLocalizationUpdate. That message carries frame = robot+0x2B0 (0) and origin = the pose parent's id (≥ 1). | 0x0051526E..0x005153AE; 0x00512734..0x005127B6 | M1-041 | EXACT_SOURCE |
| SC4i | **What happens with this stack.** Bundle 20260924-202748-CONTROL (this stack, no AbsLocalizationUpdate sent) has all 855 RobotStates reporting frame 0 and origin 0; I decoded the frame hex. Under the original logic every state would fail the origin check. So the first "send 50" does **not** happen right after connection, and none of SC4a..c would run either. <br>What the robot reports after a real AbsLocalizationUpdate cannot be settled: the repo has no capture of the original app. | frames.jsonl | NEW | HARDWARE_ONLY (robot side; observed once) |
| SC4j | **Charger-platform sends are not gated.** The SetOnChargerPlatform 50/400 sends (via SetOnCharger 0x00512AB4, called before the origin check, and via Robot::Update) do not depend on the origin check. | 0x00512AAC..0x00512AB4 | CD24 | EXACT_SOURCE |

#### 3. SC6 – game EnableCliffSensor

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| SC6 | GetFirstRobot; no robot means return. It logs "Setting to %s", then `strb` msg.enable → CliffSensorComponent(robot+0x288)+4. Nothing is sent. The byte is read by the CliffEvent handler (SC5) and by HandleRobotStopped (0x00535436). | 0x00527E8E..0x00527EEA | NEW | EXACT_SOURCE |

#### 4. MA1 – game MoveLift direct drive

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| MA1-lift | 1. If MC+0xD4 is set: log "Ignoring MoveLift message while direct drive is disabled" and return. <br>2. If flag MC+0xBA is 0: when the lock set for track bit 1 (MC+0x3C, i.e. mask 2) is non-empty, log "LiftLocked" and return. When the flag is set, the lock check is skipped. <br>3. DirectDriveCheckSpeedAndLockTracks(msg.speed, &MC+0xBA, **mask 2**, who = MC+0xC4). <br>4. Send RobotInterface::MoveLift{speed}, reliable, not hot. | 0x0063F73A..0x0063F816; 0x0063F900..0x0063F92A | NEW | EXACT_SOURCE |

#### 5. CD10 – cube moved / stopped / up-axis / tapped

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| CD10a | **Moved (0x00533E30).** Message: ts +0, activeID +4, accel xyz +8..+0x10, upAxis +0x14. <br>1. Look up the connected object by active id. <br>2. If its ID == robot+0x334: log "Charger sending garbage move messages" and stop. <br>3. If BlockTapFilter::ShouldIgnoreMovementDueToDoubleTap: log and stop. <br>4. Otherwise, if not already moving: SetIsMoving(true, ts) on the object (vslots +0x10/+0x14 → bytes +4 and +8, per 0x004E3860..0x004E3880), plus a Viz message. <br>5. For every **located** object with that active id: MarkObjectDirty when it is not carried and its +0x24 == 1; SetIsMoving(true, ts) if it is not moving. <br>6. Broadcast ObjectMoved{ts, objectID, x, y, z, upAxis}, unless the cube is carried or its ID equals [robot+0x280]+0xC. <br>An unknown active id gets a warning and no broadcast. Nothing is sent to the robot. | 0x00533E4C..0x005341BA; 0x005343C8..0x00534406 | M4-009 | EXACT_SOURCE |
| CD10b | **Stopped (0x0053461C).** Message: ts, activeID. <br>- Same lookup and charger filter as Moved. <br>- ShouldIgnore is called, but its result is discarded (r0 is overwritten at 0x00534A62), so Stopped is never filtered. <br>- If moving: SetIsMoving(false, ts). Located copies get the same dirty rule and clear. <br>- Broadcast ObjectStoppedMoving{ts, objectID} with the same carried/[+0x280]+0xC exclusion. <br>No robot send. | 0x00534636..0x00534AA4 | M4-009 | EXACT_SOURCE |
| CD10c | **UpAxisChanged (0x00534D50).** Message: ts, activeID, upAxis. <br>- Unknown id: error and return. <br>- Otherwise: a Viz message, activeID is replaced by the objectID, and ObjectUpAxisChanged is broadcast. <br>No state is stored on the object, there is no charger or carry filter, and no robot send. | 0x00534D66..0x00534E1E | M4-009 | EXACT_SOURCE |
| CD10d | **BlockTapFilterComponent setup.** <br>- ctor: enabled +0x18 = 1. It subscribes robot tags **0xB6** (tapped), **0xB4** (moved) and **0xB5** (stopped), and game tag **0x62** EnableBlockTapFilter, which sets +0x18. <br>- Update runs from Robot::Update (0x00513EA4). | 0x00630874; 0x00630892, 0x00630902, 0x0063097C; 0x00630A14; 0x00631002..0x00631018 | NEW | EXACT_SOURCE |
| CD10e | **Tapped: first filter and queue.** <br>- Message: ts +0, activeID +4, numTaps +8, tapTime +9, s8 +0xA, s8 +0xB. Intensity = b11 − b10. <br>- Intensity ≤ **60**: dropped ("Tap ignored %d <= %d"). <br>- Unknown id: warning and dropped. <br>- If filter enabled **and** robot+0x14 (IsPhysical, set by SetPhysicalRobot from HandleFirmwareVersion 0x00536980) is set: the tap is queued. The first tap in the queue sets deadline = now + **75 ms**. <br>- Otherwise ObjectTapped{ts, objID, b8, b9, b10, b11} is broadcast immediately. <br>- Always ends with CheckForDoubleTap. | 0x00630B2E..0x00630C8C; 0x0051397A | NEW | EXACT_SOURCE |
| CD10f | **Tapped: flush in Update.** Once now > deadline, the queued tap with the greatest intensity is broadcast (strictly greater wins, so the earliest wins a tie), and the queue is cleared. | 0x006310D0..0x0063119E | NEW | EXACT_SOURCE |
| CD10g | **Double tap.** Each object has DoubleTapInfo: +0x18 window end, +0x1C moving, +0x20 ignore-until, +0x24 pending. <br>- BTF Moved sets moving = 1 only if the window end is 0. BTF Stopped sets moving = 0. <br>- CheckForDoubleTap: if moving, clear the window. Else if now < window end: log "Detected double tap" (no broadcast), window = 0, pending = 0. Else: window = ignore = now + **500**, pending = 1. <br>- ShouldIgnore returns ignore-until > now. <br>- Update: for a pending entry past ignore-until, clear pending and MarkObjectDirty every located copy whose +0x24 == 1. <br>- ShouldIgnore is also used by LocalizeToObject (M11) and WasObjectTappedRecently. | 0x00630DC4..0x00630E58; 0x00630EF8..0x00630F86; 0x00631594..0x0063163A; 0x006316B8..0x006316E0; 0x006311A2..0x006313D8 | NEW | EXACT_SOURCE |

M10/M12 consumers of these broadcasts (ReactionTriggerStrategyCubeMoved, BehaviorGuardDog, KnockOverCubes) are interfaces only.

#### 6. LC8 – ObjectID on reconnect

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| LC8a | **HandleActiveObjectConnectionState.** Slot > 4 is ignored. Connected → AddConnectedActiveObject(slot, factoryID, type), then HandleConnectedToObject. Disconnected → RemoveConnectedActiveObject(slot), then HandleDisconnectedFromObject. | 0x00533B56..0x00533CB2 | S1 | EXACT_SOURCE |
| LC8b | **RemoveConnectedActiveObject.** Erases the connected entry whose activeID matches and returns its ID. Located copies with that ID keep their ObjectID but get activeID = −1 and factoryID = 0. | 0x006243C6..0x00624436; 0x00624534; lambda 0x0062A1B0..0x0062A20E | NEW | EXACT_SOURCE |
| LC8c | **AddConnectedActiveObject, early exits.** <br>- activeID ≥ 5: return −1. <br>- Slot already holds the same factory and type: **return the existing ID** (FoundMatchingObjectAtSameSlot). <br>- Slot holds a different factory or type: remove it, then continue. | 0x00623040..0x0062319E | NEW | EXACT_SOURCE |
| LC8d | **AddConnectedActiveObject, the reuse branches.** It creates the object, then: <br>- **(i) located object with the same active id:** same factory, or factory 0 ("NeverConnected", which also rewrites the ids): take that located object's ID (0x00623426.., 0x00623B90.., 0x00623462). A different nonzero factory ("MismatchedFactoryID"): delete those located objects, then SetID. <br>- **(ii) otherwise, located objects of the same type:** rewrite their active and factory ids (0x00623584.. FoundIdenticalObjectOnDifferentSlot; 0x006236FE.. FoundMatchingObjectWithNoActiveID; 0x00623608.. FoundOtherActiveObjectOfSameType), and **take the first one's ID** (0x006237CA..0x006237DE). <br>- **(iii) none:** SetID (vslot +0x24, 0x00623946). <br>Finally the object is inserted into the connected map and its ID returned. | 0x00623396..0x00623A8C | NEW | EXACT_SOURCE |
| LC8e | **SetID for a light cube.** ObservableObject::SetID checks vslot +0x5C, which is 1 for ActiveCube (0x004E3882, via the vtable at 0x0101CDEC). In that case it uses a function-local static map<ObjectType, ObjectID> (0x010591C0): an existing entry is copied (0x004EF524), otherwise ObjectID::Set() (counter++) and insert. **So every ActiveCube of a given type gets the same ObjectID for the process lifetime.** A same-factory reconnect in the same slot therefore gets the same ObjectID on every branch. | 0x004EF468..0x004EF52A; 0x008412D8 | NEW (M11 interface) | EXACT_SOURCE |

#### Existing records contradicted by the source
- **M4 report LB4:** the order it gave for the table at 0x0102EA18 is wrong (LB4a). The state→name result is the same.
- **M4 report SC4:** "runs … while not on a ramp (+0x316)" is wrong. The real gates are the time-sync flag and ContainsOriginID (SC4f).
- **M4 report LC8:** "If the ObjectID is new" cannot happen for light cubes (LC8e).
- **M4-009 (as the M4 report describes it):** the engine keeps its own IsMoving and timestamp per ActiveObject, plus the tap filter (intensity > 60, a 75 ms queue that broadcasts only the strongest tap, and 500 ms double-tap movement suppression). None of this is in the M4-009 evidence.

#### Evidence too weak to keep its status
- **CD24:** its SC4 claims are correct only per accepted state. Whether states are accepted at all depends on M1-041 (AbsLocalizationUpdate) plus the robot's origin echo, which is HARDWARE_ONLY.

#### Open questions
1. After an AbsLocalizationUpdate, does the robot report that origin id and frame in RobotState? No shipped artefact or original-app capture settles this (HARDWARE_ONLY). It decides SC4 and every UFRS-gated behaviour.
2. Four things were not read:
   - which component sits at robot+0x280 (its +4 is IS_PICKING_OR_PLACING) and what the ID at +0xC is;
   - the SetPhysicalRobot argument computed in HandleFirmwareVersion;
   - the CLAD names of the tapped bytes +0xA and +0xB;
   - the robotStopped tag.
3. The engine resends Off lights on every tick at connection when off the charger (LB4h, LB1). Whether that is in scope is your call.

## Appendix C: cube accelerometer trace (hardware failure, CONTROL run), extractor report

Everything here is read-only work. Scripts and dumps are in `C:\Windows\TEMP\claude\...\scratchpad\extract\cubeaccel\`: `da.py` (disassemble an address range), `xref.py` (BL, BLX and B.W call sites, following Thumb-to-ARM veneers), `e2rctors.py`, and the `*.txt` dumps.

##### What the bundle shows on the wire (checked in frames.jsonl)
- **Available:** 0xF3 at t=11182, factory 0x241a8eec, type 2. It is the only advertisement in the whole run.
- **SetPropSlot out** at 11433: `05 EC8E1A24 00`.
- **Connection states:**
  - 0xD0 at 13183: `D0 00000000 EC8E1A24 02000000 01` (connected, slot 0).
  - 0xD0 at 18694: the same with `00`, so the cube disconnected.
  - 0xD0 at 19197: `01` again, sent twice with seq 32 (one retransmit), so it reconnected.
- **StreamObjectAccel out** at 30606: `08 00000000 01`, a reliable type-4 frame. At 33595 the stack sent the same with enable=0.
- **After the first connection the robot sent no cube message of any kind:**
  - no 0xF5, 0xB4..0xB9, 0xCE (ObjectPowerLevel) or 0xD7, anywhere in the run;
  - the only cube traffic was the three 0xD0 connection-state messages.

##### Step table

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| S1 | ObjectConnectionState handler. If objectID (the slot) > 4 it returns. If connected: AddConnectedActiveObject(slot, factoryId, type), then Robot::HandleConnectedToObject, then a broadcast of the game-side ObjectConnectionState (EngineToGame tag 0x0D) carrying the engine ObjectID. | 0x00533B56 `ldr r7,[r0]`; 0x00533B58 `cmp r7,#4; bhi`; 0x00533BD6 bl AddConnectedActiveObject; 0x00533C34 bl HandleConnectedToObject; 0x00533D24/0x00533D2C MessageEngineToGame(ObjectConnectionState), Broadcast; tag 0x0D at 0x00721EDC | M4-009 / M4-010 | EXACT_SOURCE |
| S2 | AddConnectedActiveObject: a slot ≥ 5 is rejected. It creates the cube with CreateActiveObjectByType(type, slot, factoryId). ActiveCube(int activeID, u32 factoryID, type) stores activeID at vbase+0x40 and factoryID at vbase+0x44. No robot send was found in the function. | 0x00623040 `cmp sl,#5`; 0x00623296 bl CreateActiveObjectByType (r1=sl slot, r2=r5 factory); 0x004E0DDE `str r5,[r1,#0x40]`, 0x004E0DE8 `str r4,[r1,#0x44]` | NEW | EXACT_SOURCE for the create branch. Its indirect `blx r1` calls are std::function destructors or object vtable +0xC/+0x24 calls, not sends. The reuse/update-slot branches (0x00623584, 0x006236FE) were not fully read. |
| S3 | Robot::HandleConnectedToObject sends nothing. If the slot's expected factory id matches, it sets the slot state to 2 (Connected), erases the entry from the available map (robot+0x47C) and clears +0x4A8. Otherwise it logs "Ignoring connection…". | 0x005179D6..0x00517AA6 (`movs r0,#2; str r0,[r5]` at 0x00517A9C) | M4-010 | EXACT_SOURCE |
| S4 | CubeAccelComponent subscribes to game-side ObjectConnectionState (tag 0x0D), but its handler only walks the tree and discards the result. Connecting a cube therefore never sends or re-sends StreamObjectAccel. | subscribe 0x00635364 `movs r3,#0xd`; handler 0x00635D6A..0x00635D82 (no store, no call) | NEW | EXACT_SOURCE |
| S5 | CubeLightComponent on game-side ObjectConnectionState: if connected and IsValidLightCube(type), it inserts an ObjectInfo (current layer = 2) and calls PlayLightAnim(objectID, trigger 0x26 = "WakeUp", layer 2). | 0x00639CE4 connected check; 0x00639CF6 IsValidLightCube; 0x00639D08 `movs r0,#2` (layer); 0x00639D64 `movs r0,#0x26`; 0x00639D8A PlayLightAnim. The trigger name comes from the table at 0x01032F90 (entry 38 is "WakeUp"). | NEW | EXACT_SOURCE |
| S6 | PlayLightAnim reaches SetObjectLights immediately when the requested layer is ≤ ObjectInfo's current layer (2 ≤ 2 on a fresh connection). It then sends SendTransitionMessage and SetObjectLights. | gate 0x006386F8..0x00638702 (`cmp r2,ip; ble`); 0x006387C8, 0x006387D8 | NEW | RECOVERABLE_GAP. Read 0x006382FA..0x0063845E: the anim lookup (GetCubeAnimationForTrigger), the "OnlyGameLayerEnabled" branch and the not-enabled branch. Also confirm that "WakeUp" exists in the shipped cube-anim assets. |
| S7 | SetObjectLights(ObjectID, ObjectLights) looks up the connected active object (or the located one when makeRelative is set), dynamic_casts it to ActiveCube, calls SetLEDs and tail-calls SetLights through a veneer. | 0x00637E96 GetConnectedActiveObjectByIdHelper; 0x00637EAA __dynamic_cast; 0x00637EF8 `b.w 0x8CCCDC`, which resolves to the SetLights PLT 0x4B9294 | NEW | EXACT_SOURCE |
| S8 | SetLights sends up to three reliable, not-hot messages, in this order: (1) SetCubeGamma 0x0C, only if the object's gamma (+0x7C) differs from the cached value (component +0x28, constructed as 0), after which the cache is updated; (2) CubeID 0x10 {u32 activeID from vbase+0x40, u8 rotationPeriod in frames}; (3) CubeLights 0x04 (40 bytes). | 0x0063A764..0x0063A792; 0x0063A79C..0x0063A7D2; 0x0063A7F4..0x0063A800. CubeID pack 0x007B764A (u32, u8); tags at 0x007A7BBA (#0xC), 0x007A7E32 (#0x10), 0x007A77D8 (#4) | NEW (M4) | EXACT_SOURCE. Whether gamma is sent on the first call is RECOVERABLE_GAP: read the ActiveObject/ActiveCube default at +0x7C. |
| S9 | The only engine senders of these messages. StreamObjectAccel: CubeAccelComponent AddListener and RemoveListener only. CubeID, CubeLights, SetCubeGamma: SetLights only. SetPropSlot: ConnectToRequestedObjects only. FlashObjectIDs 0x4D: only Robot::SendFlashObjectIDs, which has no BL, B.W or veneer caller and no data reference apart from its dynsym entry, so it is never sent. ObjectConnectionStateToRobot and SetAccessoryDiscovery: no senders. SetBodyRadioMode: see S10. | xref over all BL/BLX/B.W plus veneers (`xref.py`, `e2rctors.py`); FlashObjectIDs at 0x00517E6E | NEW | EXACT_SOURCE (a computed function pointer was not searched beyond the raw-pointer scan) |
| S10 | SetBodyRadioMode {1,0} is sent only after 16 consecutive RobotStates without IS_BODY_ACC_MODE ("Robot.UpdateFullRobotState.BodyNotInAccessoryMode"). In this run the bit was set throughout, so the original would not send it. | 0x00512AD0..0x00512B52 | M1 CD25 | EXACT_SOURCE (already recorded) |
| S11 | Callers of AddListener: the game's StreamObjectAccel handler (RobotEventHandler 0x0052988C, using the game message's engine ObjectID), FeedingCubeController::StartListeningForShake, BehaviorFeedingEat::InitInternal, BehaviorSinging::InitInternal and BehaviorGuardDog::StartMonitoringCubeMotion. Nothing adds a listener on connection. | 0x00529912; 0x005A6350; 0x005D59BE; 0x005EED56; 0x005F45B4 | NEW | EXACT_SOURCE |
| S12 | AddListener (0x00635474): find or create the history for the ObjectID (window 0x32). If its listener set is empty: GetConnectedActiveObjectByIdHelper(ObjectID) via robot+0x34. If found, log "ObjectID %d (activeID %d)" and send StreamObjectAccel{objectID = obj vbase+0x40, the activeID or slot; enable = 1} with SendMessage(reliable=1, hot=0). If not found, warn "CubeAccelComponent.AddListener.InvalidObject" and send nothing, yet the listener is still added, so a later AddListener never sends. Finally window = max(window, arg). | 0x00635496 find; 0x006354E8 `ldr r0,[r6,#0x30]; cmp #0; bne`; 0x006354F4 lookup; 0x00635556 `ldr r0,[r0,#0x40]`; 0x0063554A `movs r1,#1`; 0x00635562/0x0063556E send with r2=1, r3=0; InvalidObject 0x00635584; insert 0x006355BA; window 0x006355BE..0x006355CA | M9-017 (partial) | EXACT_SOURCE. It confirms the manager's hypothesis. |
| S13 | StreamObjectAccel wire layout: tag 0x08, {u32 objectID, bool enable}, size 5. | 0x007A7A1E `movs r1,#8`; Pack 0x007B7B44 (WriteBytes 4, then Write<bool>); Size 0x007B7BA0 `movs r0,#5` | M2 (0x08 row) | EXACT_SOURCE |
| S14 | RemoveListener: erase the listener; if the set is now empty, look up the connected object and send StreamObjectAccel{activeID, 0} (reliable, not hot); the window is reset to 0x32. | 0x006356EE..0x00635784 (`movs r1,#0` 0x00635754; window 0x00635782) | M9-017 | EXACT_SOURCE |
| S15 | Receive path: the constructor subscribes RobotToEngine 0xF5 to ReceiveObjectAccelData, which calls HandleObjectAccel through a veneer. HandleObjectAccel has no time-sync (+0x29) gate. It looks up the connected object by activeID = msg+4 (+0 is the timestamp). If none is found the message is dropped silently. If the history is missing it verify-fails "NoObjectIDInHistory". Otherwise it records the sample, runs the listeners and CullToWindowSize. In both of the last two cases it broadcasts game-side ObjectAccel (with the engine ObjectID) and the Viz state. | 0x0063524C `movs r6,#0xf5`, 0x0063526C subscribe; 0x00635358 `b.w 0x8CCCAC`, which resolves to HandleObjectAccel; 0x00635848 `ldr r1,[r8,#4]`; 0x00635852 ByActiveId lookup; 0x00635858 `beq` drop; 0x00635916 NoObjectIDInHistory; 0x0063593E/0x00635946 broadcast | M2 (0xF5) | EXACT_SOURCE |
| S16 | ObjectMoved 0xB4 and the rest (0xB5, 0xB6, 0xB9, 0xCE, 0xD7) are separate robot-originated events, keyed by activeID. The engine sends nothing to enable them. Their robot-side trigger is not in the engine. None appeared in this run. | HandleActiveObjectMoved 0x00533E30 (M4-009 evidence) | M4-009 | Engine side EXACT_SOURCE; robot side HARDWARE_ONLY |
| S17 | Delivery of the game-side broadcast (Robot::Broadcast) to the engine's own subscribers (the S4/S5 subscriptions made through ExternalInterface vtable +0x28, only when HasExternalInterface). | 0x006352B0..0x006352C6; 0x00637114..0x0063713C | NEW | RECOVERABLE_GAP. Read Robot::Broadcast and the ExternalInterface delivery. |
| S18 | Robot-side effect of StreamObjectAccel, and what makes a robot or cube emit 0xF5. | No shipped primary source. See the lower-authority notes below. | M9-017 | HARDWARE_ONLY / BLOCKED_EXTERNAL (robot/cube firmware) |
| S19 | Whether firmware 2457 keeps tag 0x08 and this layout. | The engine was built against firmware 2381, whose E2R hash (9e4a965a…) differs from 2457's (fedb4b12…). 2457 is not shipped. | M1-040 | BLOCKED_EXTERNAL |

**Lower-authority notes for items 3 and 5:**
- **Generated CLAD in the .so:** BlockMessages::LightCubeMessage is the robot↔cube message set. Its tags are: flashID 0, setCubeID 1, setCubeLights 2, setObjectBeingCarried 3, streamObjectAccel 4, available 5, moved 6, stopped 7, tapped 8, upAxisChanged 9, accel 0xA (constructors at 0x007A036C..0x007A08A6). This suggests the robot relays StreamObjectAccel to the cube and relays the cube's accel back. It is only a schema: it says nothing about when the robot relays.
- **Unity** only has the generated twins: `unity/scripts/csharp/Anki.Cozmo/StreamObjectAccel.cs` (u32 objectID, bool enable, Size 5). No Unity code sends it.
- **pycozmo (a repo reference, not shipped):**
  - its docs claim ObjectAccel arrives "every 30 ms" after StreamObjectAccel, and that a cube "has to be selected first, using the CubeId message" before CubeLights;
  - no pycozmo code exercises the stream.
- **No original-app capture** in `re-analysis/captures/` contains cube accel.
- **Firmware:** `re-analysis/obb/assets/cozmo_resources/config/engine/{firmware,firmware_2214,old_firmware,firmware_2158,firmware_1889,firmware_1859,firmware_1299}/cozmo.safe` hold 2381 and older builds. The bodies are high-entropy (7.99 bits per byte from offset 64 KiB, which I checked). Firmware 2457 is not shipped, so none of these can answer the robot side.

##### Existing records contradicted by the source
- None of the records' behavioural claims is contradicted.

##### Records whose evidence is too weak for their status
- **M9-017 (EXACT_SOURCE):**
  - "AddListener … sends StreamObjectAccel … to turn the cube stream on" asserts a robot-side effect (S18) that no shipped source establishes.
  - Its evidence also leaves out S12's conditions: first listener only, the connected-object lookup, sending the activeID, and adding the listener with no send when the object is not found.
  - The CONTROL CUBES check exercises this path (its accelStream criterion names `CubeAccelStreams.AddListener`) but cites only M1-042 and M4-008..M4-011, not M9-017.
- **M2 inventory §0x08:** it marks the StreamObjectAccel objectID source as RECOVERABLE_GAP. S12 now settles it: the activeID, which is the connection slot.

##### Open questions for the manager
1. S6: the early gates of PlayLightAnim, and whether the "WakeUp" cube anim exists in the shipped assets.
2. S8: whether SetCubeGamma goes out on the first SetLights.
3. S17: how Robot::Broadcast reaches the engine's own subscribers.
4. S18 and S19: robot-side behaviour, and firmware 2457 compatibility. These are not recoverable from shipped artifacts.

##### Conclusion
**(d): the cause is not established.** The evidence:
- **(a) wrong bytes is ruled out by the source.** The original would send exactly `08 00000000 01` as a reliable message:
  - tag 0x08, u32 activeID, bool (S13);
  - the activeID is the connection slot, and the robot reported slot 0 (S1, S2, S12);
  - reliable (S12).
- **(b) there is a real omission, but the engine does not make it a prerequisite.**
  - After a light cube connects, the original immediately plays the WakeUp cube-light anim. That leads to CubeID 0x10 {activeID, rotation} and CubeLights 0x04, possibly preceded by SetCubeGamma 0x0C (S5 to S8). The stack sent none of these.
  - The engine's own streaming logic does not depend on them. AddListener checks only for an empty listener set and a connected BlockWorld object (S12). HandleObjectAccel does not depend on them either (S15).
  - Whether the robot or cube firmware needs a CubeID or CubeLights before it relays accel is robot-side and cannot be established from source (S18).
  - FlashObjectIDs is never sent by the original (S9). SetBodyRadioMode would not have been sent in this run (S10).
- **(c) robot behaviour is HARDWARE_ONLY and open.** The bundle shows the robot forwarded nothing from this cube after connecting: no ObjectPowerLevel, ObjectMoved, Tapped, UpAxis or Accel. It also dropped and re-established the connection once (18694 → 19197).
  - That points to the cube link as a whole, not the accel stream alone, but it does not establish a cause.
  - The robot also runs firmware 2457, whose E2R schema hash differs from the 2381 build the engine targets (S19).
- **What would decide between (b) and (c)** is a robot observation: whether 0xF5 (or any cube telemetry) appears after CubeID and CubeLights have been sent. No shipped artifact can settle it.
