# PICKUP-STACK-1 — finite native visual subtree contract

Independent READ PASS before code, 2026-10-27: A research_reconciliation, B acceptance_blockers. known.py before each new address/name, binary instructions primary; old checked P1/Q3/Q4/OV07 packets reused. Actual worker model/effort telemetry unavailable. Scope is this pickup child's observable output/state, not concurrent vision/motion. Full pickup remains guarded until its other recipients exist.

| Required behavior | Primary instructions / checked evidence |
|---|---|
| Reuse P1 finite Visual Init/Check: copied ROOT pose/half-size/ignored target/family6; parallel TurnPose(PI)+lift preset3/tolerance5; positioning success consumes transition tick; thereafter always closest-query after each Wait update, match0300001D overrides result. |00569014;005695BE;00569724;P1 read/check|
| Actual MAIN/current-origin conversion for parented target, no History environment; failure is BadPose. Parentless target gets world-origin parent. Turn yaw is actual MAIN. |0054A944..0054AA58;OV07 GetPose004EA398|
| Head is calibrated/clamped Motion.HeadAngleRad, not raw latest packet/history. Reuse actual state routing/owner; source gate314, clamp thresholds BEFA35DD/3F543B67 to BEDF66F3/3F46D3F2. |0051335E..005133E8;M4-001;State.Handle before Motion.Handle|
| Max-turn/atan branches use RadiansGT/LE after ctor normalization; head clamp repeats RadiansGT/LT. Preserve order/epsilon; no raw comparisons or MinMax substitute. |0054AA70/0054AA8A/0054AAE4;0054ABBA/0054ABEE/0054AC0A|
| ComputeHeadAngle uses actual neck2CC; engine camera2D8=(17.52f,0,-8f), copied and rotated by -angle aboutY, default matrix C4A854. Use checked NativePoseArithmetic, not vision camera-height convention. |0051838E;00510FFC..00511066;M11-065 checked R1–34/R38–40|
| Actual camera258+24 installed calibration: retained snapshot rowsU16+0/fyF32+8. Null returns1 and sets error flag, caller uses fallback. Neck conversion06000000 also falls back. Existing NV-install chain reused. |005183E6..00518400;006B1E3E/5E/78;0054AB04/0054AB9A|
| Preserve sequential f32 scalar/pose operations and iteration edge: convergence at k25 WRITES output then returns1;25 nonconverged returns0 WITHOUT writing, so caller's initialized Radians0 survives. Existing C# early return-before-write is a defect. Fallback uses MAIN-relativeXYZ then Radians clamp. |005183A6..D0;00518432..DE;005185B2/B6;0054B428..0054B564;checked Q3|
| PanTilt clears old children first; constructs Turn first, calls real SetTolerance, copiesD8; adds(false,false). Head absolute/Robot2FC-relative, tol150/variation0,9D0/9C126; adds(false,false),embedded56=1,immediateUpdate maps Success/Running to0. |00549C48..00549D60;00549C7C|
| Real owned ActionRunner/Compound/Motion.MoveAction children, one native action/tag; no separately queued motor task, supplied CompoundUpdate, double tag or fake frame. Existing checked motor wire/ack/timer reused. |M4-016/OV07;actual ActionRunner and Motion ownership;internal mechanism otherwise free if outputs equal|
| Visual destruction Prep(position) then Prep(wait); destroy filter/pose; null/delete Wait BEFORE position. Reinit preps/replaces old children; Wait success/replacement/dtor releases retained43; no invented Cancel unsubscribe. TurnPose destroys copied pose then PanTilt Prep/destroys owned compound; Turn dtor removes eye shift through existing checked recipient. |00569460..0056950E;00569624..62;0054D0A4..D6;0054D18C..0054D1FC;00545B94..BD4|

Wait count/timestamp/actual43 publication are already independently checked in P1:0054CA64/0054CB68/0054CC1C/0054DD88; ten mode1 messages strictly timestamp>0, no dedup/fake frames. Cancellation follows existing base state and owned-child disposal, not a guessed early unsubscribe.

Current manifest claims, reread before release (all remain unchanged):

- M13-020 IMPLEMENTATION_GAP, “PanAndTiltAction, TurnTowardsPoseAction::CheckIfDone and WaitForImagesAction”; evidence549C48..D60/54B010..1C/54CA64..54DD88+checkedQ4.
- M13-026 IMPLEMENTATION_GAP, “GetAbsoluteHeadAngleToLookAtPose and Robot::ComputeHeadAngleToSeePose”; evidence54B428..B55A/518344..518780+checkedQ3.
- M12-037 IMPLEMENTATION_GAP, “SetupTurnAndVerifyAction and GetObservedMarkers”; evidence876C50..96/551F9C..552164+checkedQ10. No whole-record closure from this child alone.
- M4-016 EXACT_SOURCE, “MoveHeadToAngleAction / MoveLiftToHeightAction Init, CheckIfDone and the MotorActionAck handlers: no send in position, ack by id, the latch and has-moved bytes, 0x04000004 / 0x03000016, the u16 diagnostic counters”; evidence MA9/13/15..17,C1/C6/C10.1,C11.3/lift493F6..49508.
- M4-001 EXACT_SOURCE, “Head angle limits -0.436332..0.776672 rad: command clip in MoveHeadToAngleAction, state-side clamp, -25 deg before calibration”; evidence MA9/22/23,51335E..E8.
- M11-065 IMPLEMENTATION_GAP, “Pose3d / Rotation3d / Transform3d arithmetic: double unit quaternion, float translation, float point rotation, renormalised composition (rows R1-R34, R38-R40)”; checked arithmetic report/84ACA6→849C1C..CF8/84B92A..BB98. No whole pose-math acceptance.

Remaining: actual Pickup/IDock/first-attempt turn/DriveTo/Search and activity admission retain named MISSING until their own source/build checks; this contract is not a whole pickup release. Build check must exercise actual queued native runners/outputs, distinct from unit inputs; full gate/Claude/hardware remain separate.
