# R-M14-IMAGE-TURN — historical pose to motion admission

Request: `requests/20261021-m14-image-turn-history-join.md`. HEADb3651f3a. Independent read-only report; coverage opened before comparison. No approval/status change.

## Coverage

| Step | Source / current route | Disposition |
| --- | --- | --- |
| Current record | M14-005 IMPLEMENTATION_GAP, quoted below | Early EXACT_SOURCE inventory rows superseded |
| Image point/timestamp | Native ctor0x0054B5E4..0x0054B5E8; ReactToPet.OnStart | Current caller passes pet rectangle center and observation timestamp, no invented distance |
| Calibration | Native camera+0x28 shared calibration0x005187A6..0x005187BA; Vision.Calibration | Uses actual calibration; missing calibration returns false in host |
| History | flag0 at0x005187F6/FC, query0x00518800; Vision.History→active EngineRobot.PoseHistory | Supported result/flag join; no Latest substitution in target calculation |
| Failure | helper returns nonzero, action0x0054B68C..0x0054B6E2 returns0x03000000 | Current returns false before PanAndTilt; motion suppression present |
| Angles | corrected atan2f0x0051886C/8E, head addition0x00518878, heading0x0051889E/A8 | Binary32 candidate present; libm/Radians limits remain |
| PanAndTilt admission | native0x0054B6EE..0x0054B70E→0x00549C48..0x00549D60 | Native child-action/compound lifecycle; host uses earlier direct executor |
| Current motion | ExplorerBehaviors.PanAndTilt.RunAsync → two direct sends, host settle loop | Action admission/lock/cancel/head completion unsupported by this executor |
| Tests | FaceTests/RFixBatch1Tests Angles only; broader rigs override PanTilt | Not a complete production→wire oracle |

## Finding

The history join itself is present: this live image-turn helper queries the image timestamp with flag=false, uses the returned historical heading/head angle, and emits no motor commands on query failure. Do not replace it with Latest or attribute other face actions' latest-pose use to this calculation.

The remaining production gap is **the handoff into the native PanAndTilt/action lifecycle**. Correct angles plus correct individual command formats do not prove the intervening direct-send executor reproduces native admission, track locks, cancellation, subaction completion or event order. Current M14-005 must remain a gap; its unresolved paragraph is narrower than the current production limitation.

## Current claim

Current manifest M14-005:
- Title: “TurnTowardsImagePoint turns by the angle the pixel subtends”.
- Status: IMPLEMENTATION_GAP.
- Authority: Robot::ComputeTurnTowardsImagePointAngles0x0051879C and TurnTowardsImagePointAction::Init0x0054B664.
- Evidence says the image point is centered using calibration, history is queried at image timestamp, corrected head atan2f0x0051886C/body0x0051888E are added to historical head/heading, and Init writes PanAndTilt+0x114/+0x11C. Failed history warns and turns nowhere.
- Current unresolved: “built, awaiting strong verification (R-FIX, 2026-10-03): ComputeTurnTowardsImagePointAngles in binary32 with atan2f (0x0051886C, 0x0051888E) and operator+(float, Radians) (0x0084C97A). Still open: atan2f is MathF.Atan2 (libm stand-in); the Radians rescale is double code (the engine's 0x0084C87C is f32; identical in range).”

`re-analysis/inventory/M14-faces.md` initial EXACT_SOURCE rows and its early check at line194 precede the later demotion at line422. They are evidence of the native bodies, not current whole-path settlement. Reuse M14 extraction/check plus `20261021-m11-pose-history-settling.md` Consumer/face-actions row. No general history interpolation or pose arithmetic audit repeated.

## Native joins

Fresh reads supplement the adopted bodies only at input/output/admission boundaries.

1. TurnTowardsImagePointAction ctor0x0054B59C builds PanAndTilt with both isAbsolute arguments1 (0x0054B5C4..0x0054B5D2). It copies two input float words and image timestamp into+0x164/+0x168/+0x16C at0x0054B5E4..E8. The action type becomes0x2A. Full normal ctor and unwind through0x0054B640 read; intervening strings/literals are data.

2. Init0x0054B664 reads stored point/timestamp and calls Robot.ComputeTurnTowardsImagePointAngles0x0054B688. Success is result0, not booltrue. Nonzero branch logs warning and returns0x03000000 at0x0054B6DE, never entering PanAndTilt. Success copies returned body/head Radians into+0x114/+0x11C (0x0054B6EE..0x0054B708), then calls PanAndTilt.Init0x0054B70E. Complete Init return/unwind through0x0054B73C read. There is no latest-pose fallback motion in this action.

3. Compute helper gets shared camera calibration at Robot+0x258→+0x28, subtracts center+0xC/+0x10 from copied point, constructs HistRobotState and calls history with interpolation flag0 at0x005187F6/FC and requested timestamp at0x005187FE/0x00518800. On failure it warns and writes current robot heading/head into out arguments (0x0051883E..0x0051885E), **but returns the nonzero failure result**. The outer action suppresses those fallback outputs. Host not writing fallback out-values is therefore not a defect in this bounded caller.

4. Success uses head atan2f(-dv,fy)0x0051886C plus historical head at stack+0x54; body atan2f(-du,fx)0x0051888E plus historical pose heading0x0051889E/0x005188A8. Return0 at0x005188B4. Helper normal/unwind through0x00518918 read; bionic math and Radians arithmetic reuse existing evidence rather than being re-extracted.

5. PanAndTilt.Init0x00549C48..0x00549D60 clears children; constructs TurnInPlaceAction at0x00549C72, applies tolerance/flags/optional speed/acceleration, adds it to the compound at0x00549CC4. It computes absolute or relative head, constructs MoveHeadToAngleAction at0x00549D10 with tolerance, applies fields, adds it at0x00549D3A, then calls IActionRunner.Update on the compound at0x00549D4E. Return normalization0x00549D52..5C treats success/running as an Init success. Both absolute flags were set by the image-action constructor, so its head path uses the computed absolute value, not current+historical head again.

The admitted subactions and generic runner own track/queue/timing/completion behavior. This report does not re-extract them or turn two correct wire structures into evidence for that compound route.

## Current production path

Paths under `cozmo-stack/src/Cozmo.Robot/`.

- `Behavior/FaceBehaviors.cs` ReactToPetBehavior.OnStart chooses a current pet, takes pet.Rect.Center and pet.LastObservedTimestamp and calls TurnTowardsImagePoint.RunAsync via RunFaceAction. Its completion callback then starts the selected reaction animation. ShippedBehaviors.Implementable/Reactions construct ReactToPet when vision is supplied; the full detector/behavior activation ownership remains M14/M8, not certified here.
- `Vision/FaceActions.cs` RunAsync first checks Calibration, then uses HistRobotState and History.ComputeStateAt(imageTimestamp,...,false). It logs and returns false on nonzero result. Angles uses historical.Pose.AngleAroundZ and historical.State.HeadAngle. No range200mm or current-pose substitution enters this calculation.
- `Vision/VisionSystem.cs` History property resolves robot.Engine.Robot.PoseHistory on every access. With no robot it emits a dedicated M11-069 MISSING and returns detached history. Normal connected use shares the actual raw/vision history owner; removed-Robot calls are not silently proven native.
- Calibration is Vision's installed calibration, including camera connection/NV callback; its construction override is a host input. Complete calibration scaling/detector-to-rectangle frame equivalence remains existing M11/M14 evidence dependency; this report does not assume any injected rectangle is a valid detected pet.
- The helper clamps head with HeadGeometry bounds before handing angles to PanAndTilt.RunAsync. Exact clamp placement and head action tolerance/defaults are M13-020/022, not proved equivalent by this report.
- `Behavior/ExplorerBehaviors.cs` PanAndTilt.RunAsync first permits PanTiltOverride. Ordinary path sends SetBodyAngle/TurnInPlace message using shared Motion.NextActionId, then SetHeadAngle with another ID, flushes each, then polls Latest pose and !Moving with DateTime.UtcNow six-second deadline and33ms delay. It can return true from body arrival without checking a head subaction's native completion. Cancellation is tested in the host wait condition; this helper does not submit a native compound/child runner or propagate their cancellation lifecycle.
- **Latest does enter the settle loop**, after the target calculation. It is not used as the image-timestamp target pose. Its flat facade affects completion/readiness semantics and remains a separate unverified join.
- `Vision/PanAndTiltActions.cs` contains the recovered PanAndTiltAction/spec representation. Init requires injected CompoundUpdate and constructs child specs. It is not the live executor above. The current M13-020 unresolved explicitly says its child M13-022 action wiring/CompoundActionParallel production path is not live.

Therefore neither normal helper send success nor a PanTiltOverride success proves native action admission or completion.

## Tests / smallest supported correction proposal

`FaceTests.TurnTowardsImagePointIsTheAngleThePixelSubtends` directly calls Angles for center/quarter-turn examples. `RFixBatch1Tests.M14_005_TheImagePointAnglesAreBinary32` checks fixed numeric output against float emulation with one-ulp libm allowance. These do not run history failure, queue admission, motor reception or action completion. Other ManipRig/Freeplay tests install PanTiltOverride; that is a legitimate bounded fixture, not evidence of live PanAndTilt fidelity. No exact image-turn production→wire test was found in the selected test search. Tests not run.

Smallest correction scope is a source-backed image-action adapter that preserves point/timestamp/flag0/failure semantics and hands success into the existing real PanAndTilt/TurnInPlace/MoveHead action lifecycle once those joins are adopted/built. Resolve the child-spec→live action/compound admission and cancellation/completion recipient gaps, rather than retain a six-second body-only wait as evidence of native behavior. Keep native query failure nonzero/ABORT suppressing all commands. Do not replace the history result with current pose just because the helper computes fallback out-values.

Independent future regression:
- Use two admitted historical states with clearly different headings/head angles, image time selecting the earlier raw state under flag0, actual calibration and a noncenter pixel. Drive the real image action through queue/tick.
- Verify the absolute targets come from that selected state; a later current pose cannot change them. Compare exact boundary/tie selection using existing history oracle.
- Fail history deliberately and assert no child/action motor traffic, with native abort result and diagnostic.
- With valid history, hold a track locked, cancel between ticks, and separately delay head completion after body arrival. Compare native compound admission/results/completions and actual outgoing payload/order, not merely eventual angles.
- Keep a pure angles fixture as arithmetic coverage, but do not count PanTiltOverride or direct Angles invocation as production reach.

Remaining dependencies unreviewed: calibration/image-coordinate frame producer, full ReactToPet/detector lifecycle, atan2f bit parity/Radians rescale, clamp/tolerance/default parameter join, real PanAndTilt child/compound wiring and downstream native motion lifecycle. Current M14-005 remains IMPLEMENTATION_GAP; no larger face-behavior audit or settlement.
