# HIKING-BRING-CUBE-TO-BEACON-1 — Drive InitHelper identity operands

Read-only response to diagnostic2's unexpected final turn/BAD_OBJECT question. Requested Sol/medium; backend unobserved. PROJECT_STATE first; known.py0x00559370 before native check, then known.py IsCarryingObject before following its exact predicate. Same verified binary/SHA as main Bring read. No code, tests, git or official edits.

**Classification: implementation bug, not an approved-contract contradiction. Two identities have DIFFERENT uses.** The carrying test uses the supplied ActionableObject's ObservableObject ObjectID (the placement clone on the floor path). The final turn, if constructed, uses the action's stored target ID+0x7C (real carried target). Additionally native IsCarryingObject compares BOTH carried slots, without a -1 sentinel guard. For a fresh cloneID=-1 and an empty top slot=-1, the native predicate is true and skips the final turn. A strict CarriedObjectId-only comparison falsely inserts it; using cloneID for the inserted turn is independently wrong.

This corrects my intermediate message that an invalid clone necessarily makes the carrying predicate false. That inference was sent before checking the callee; it is withdrawn. The native two-slot comparison below is the final evidence. No result should be inferred from “invalid” by name.

## Narrow primary table

| Step | Exact operands/value/order | Citation |
|---|---|---|
| ID1 | InitHelper prologue stores this in sb(r9), supplied ActionableObject* in r8. These are distinct owners. |0x00558FC0 `mov sb,r0`;0x00558FC8 `mov r8,r1` |
| ID2 | Load supplied object's vptr, its virtual ObservableObject-base offset from vptr-0xC, add to supplied pointer, then+0x14 to ObjectID object. Pass that to CarryingComponent.IsCarryingObject; robot carrying component is robot+0x284. It does NOT load action+0x7C for this test. |0x00559366 `ldr r0,[r8]`;0x0055936C `ldr r2,[r0,#-0xc]`;0x00559370 carrying;0x00559374 `add r1,r8,r2`;0x00559378 `adds r1,#0x14`;0x0055937A call PLT0x004A6FAC |
| ID3 | IsCarryingObject reads argument ObjectID.value at+4. Compare with carried+8; equal=>1. Otherwise compare with carried+0x14 (top ID), normalize equality to0/1. No check for input -1 or stored -1. |0x00633F88 reads arg+4;0x00633F8A..92 bottom compare;0x00633F94..9E top compare |
| ID4 | Predicate nonzero skips whole final-turn construction. False constructs TurnTowardsObjectAction. That action's ID is explicitly `[this+0x7C]`, copied into a stack ObjectID; it is NOT supplied object's ID. Radians0,true,false. |0x0055937E/380 `cmp r0,#0; bne0x00559436`;0x00559392 `ldr r1,[sb,#0x7c]`;0x00559396 stackvalue;0x0055939E..B6 zero angle/true/false/ctor |
| ID5 | Derived floor Init reads current strict carried ID and writes it into action target+0x7C before lookup. For no carried ID it returns0x03000011. The fresh cloned geometry object's identity remains separate. |0x00559DC6..DCE;0x00559DD0 target store;0x00559DD2..DC lookup |
| ID6 | Derived floor sets clone pose Known then invokes InitHelper(this,clone) and destroys clone afterwards. Base geometry/pose function reads clone, carrying predicate reads clone ID, final turn target reads action ID as ID4. |0x00559F20..2C InitPose;0x00559F30 r0=this;0x00559F32 r1=clone;0x00559F34 call;0x00559F3A..44 destroy |
| ID7 | Current M12-030's already checked clone contract is fresh ObjectID=-1 and fresh markers, not copied real carried identity. This report reuses it; do not repair final-turn behavior by mutating clone ID to the carried ID. |current M12-030 unresolved “Clone keeps NOTHING (ObjectID -1 = uint.MaxValue, fresh markers, InitPose Known)”;checked Q13 and gap3 clone evidence in snapshot below |

Exact predicate examples, with `bottom/top/input` as signed native ID bit patterns:

| bottom | top | input supplied clone/object ID | Predicate / final turn |
|---|---|---|---|
| real cube ID | -1 | -1 | true from TOP comparison; no turn |
| real cube ID | another real cube ID | -1 | false; turn to action+0x7C, not -1 |
| real cube ID | any | same real cube ID | true from bottom; no turn |
| one real cube ID | different real top ID | top ID | true from top; no turn |

This is the defined original comparison, not undefined memory or a terminating fault. No M1-034 skip or exclusion applies to these valid data values.

## Existing C# and scope conclusion

At the read, NativeDriveToObjectRunner.InitHelper uses a strict `CarriedObjectId != obj.ObjectId` check and constructs NativeTurnTowardsObjectRunner with `obj.ObjectId`. For the placement clone, those differ from native ID3 and ID4. The reported diagnostic's DriveToPose→Turn(-1)→BAD_OBJECT is explained by those implementation joins; native ordinary single-carried-cube state reaches only the drive because empty top=-1 matches clone=-1. The native floor derivation deliberately keeps target identity and cloned geometry identity separate.

Existing actual owner is `EngineRobot.CarryingOwner` (DockingCarryingOwners.cs), constructed with the robot and passed to the facade. Its two stored IDs, including null→native0xFFFFFFFF, are required; a detached facade/default carry value cannot settle the predicate. The exact source-compatible identity interfaces are therefore: actual two-slot carrying membership for the supplied object's ObjectID (including -1 equality), and stored Action.ObjectId as the final-turn constructor target. A C# nullable owner input must preserve its native -1 bit pattern for this predicate, including the top slot. Do not simply change both operands to Action.ObjectId: that erases the native clone/target distinction and changes the two-real-carried-objects case.

This does not reopen clone construction, planner, pose math, floor CheckIfDone or Ground. PICKUP-APPROACH row8's shorthand “actualcarryingtarget skips finalturn” was sufficient for the ordinary object==action target profile; it does not establish that these two operands are interchangeable in the derived placement-clone profile. Old Q12.8 explicitly names turn `[this+0x7C]` and is consistent with native. The frozen native-action-interface report already preserves original carried target plus temporary clone; this supplement makes its shared InitHelper operand distinction explicit.

Carrying contract context was checked before the final classification too: current M15-020 unresolved explicitly says “IsCarryingObjectId [+8] or [+0x14], 0x00633F88”; the same record separately says some stack-only filters intentionally keep strict [+8] where their engine filters were unread. Those other filters are not this recovered InitHelper call and cannot justify a strict-only implementation here. M15 inventory Correction A5 already lists0x00633F88 as source. Thus this is not a newly contradictory official carrying contract; it is an omitted concrete consumer binding. Owning current records M12-022/M12-030 and current M15-020 snapshots below were re-read before this classification. They do not prescribe a strict-only carrying test or a turn toward cloneID. M12-024 is the driving-animation lifecycle record, not the owner of these identity operands. No official correction/status change or new departure is requested. Native UNKNOWN answered; implementation and independent affected verification remain manager/builder work. No tests were run by this reader.

## Literal current owning claims

### M12-022

```json
{
  "id": "M12-022",
  "title": "DriveToObjectAction::InitHelper's DriveToPoseAction goal",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005590AE objectPose.GetWithRespectTo(robot.parent, local_5c)",
    "0x005590E6..0x005590FE delta = robot.xy - local_5c.xy; 0x00559100..0x00559160 normalize; 0x005591C2 vldr s2,[sb,#0x84]; 0x005591E4 vmul delta *= distance",
    "0x00559220 vadd goal.y = delta.y + local_5c.y; 0x00559224 goal.x = delta.x + local_5c.x; 0x00559230 goal.z = robot.z",
    "0x00559232 eor r0,r1,#0x80000000 / 0x00559236 eor r1,r2,#0x80000000 / 0x0055923A blx 0x4A4510 (atan2f)",
    "0x00559278 blx 0x4A47E0 Pose3d(yaw, Z_AXIS, goal, parent, name)",
    "the C# uses the chosen pre-action pose's position with the heading toward the object; the engine's +0x84 distance and exact point are unread",
    "0x0055883C/0x0055884C 4-arg ctor stores; 0x005585A0 7-arg ctor strd (r1=-1.0); 0x0052A0EE and 0x0055B6E2 the only 4-arg ctor calls; 0x0052A0BA ldrb [r4,#0x35] usePreDockPose",
    "0x005B7BF8, 0x005B9038, 0x005B91E6, 0x005B9B2E IHelper::CreateDriveToHelper callers; 0x005B584A ldr [r7,#0xf8]; 0x005532BC..0x00553360 the AlignmentType table",
    "0x00559CDE/0x00559CE8 DriveToPlaceCarriedObject ActionType 1 or 2; 0x00554B40/0x00554B42 the only caller passes 1 (so 2)",
    "unity/scripts/csharp/Robot.cs:1580-1585 and SpeedTap/SpeedTapPlayerGoalCozmoSelectCube.cs:97 (goToPreDockPose:true)",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1a-Q1d and re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md (rows N-1..N-8 verified; priority check 2)",
    "re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q5 (the NaN row: contradicted the earlier record text)"
  ],
  "unresolved": "Built: the 7-arg/4-arg constructor split (ActionType at +0x80, +0x84 = -1.0 on the 7-arg path, ActionType 6 and the float on the 4-arg path), the GotoObject factory routes, the type-6 branch of InitHelper (a strictly negative +0x84 -> 0x0300000D; +0, -0 and NaN do not error; the object+delta goal; the already-in-position skip; the DriveToPoseAction with the object pose as +0xB4 / +0xC0 = 1 and Radians 0.174533), DriveToPlaceCarriedObjectAction (M12-030) and its ActionType 2. A NaN +0x84 builds a NaN goal and InitHelper returns no result for it: the C# throws a visible NotSupportedException instead of sending a path (MISSING: what DriveToPoseAction / PathComponent (M13) do with a NaN goal). Still open, so the record stays IMPLEMENTATION_GAP: (a) the other ActionTypes obtain their poses from the +0x150 function (M12-029, built), but the DriveToPoseAction's goal is a labelled single reduced goal (the closest pose's position with the heading toward the object) instead of all the returned poses with their full rotation: this stack's vision can estimate a far cube tilted past the 20-degree flatten clamp (M11-006), and a tilted full-rotation goal can never satisfy the full-rotation IsSameAs arrival test (M12-020); (b) +0x88/+0x13C/+0x140 are settable properties defaulting to 0/false/0 (the callers' values are not itemised) and +0x8C (useManualSpeed) is stored but not plumbed into DriveToPoseAction; Init's cube-light step (12.7) is M10, not modelled; (c) the ActionType-6 entry points FromGotoObject and the 4-arg constructor have NO production caller: nothing in this stack decodes the app's GotoObject message and the shipped app always sends usePreDockPose true, so they are reached only from tests. CORRECTION (R-VIS gap 2): the NaN case does not error."
}
```

### M12-030

```json
{
  "id": "M12-030",
  "title": "DriveToPlaceCarriedObjectAction: Init, CheckIfDone, IsPlacementGoalFree and ComputePlacementApproachAngle",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00559CD4, 0x00559DB8, 0x00559FA4, 0x0055A070",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q13; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q13 (PASS; the angle helper 0x00550858 and its axis table unopened)",
    "0x004E38D4, 0x0087660E, 0x004E0C4C..0x004E0C58, 0x00550904, 0x00627148..0x006271C2, 0x00626870..0x006268D6, 0x0062750E..0x00627530, 0x0055A070..0x0055A1B6, 0x00550858..0x005508C4",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q4-Q6; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md"
  ],
  "unresolved": "Built: Clone keeps NOTHING (ObjectID -1 = uint.MaxValue, fresh markers, InitPose Known), so the inner GetPreActionPoses does not answer 0x03000004 and the chain goes to poses and a drive; IsPlacementGoalFree returns true before any query when the carried object is not found, otherwise the default filter's FindLocatedIntersectingObjects (ignore {carried id}; reference quad at +0x16C with padding 0; the candidate's quad padded by [+0x17C]; Intersects; no Z test; current origin; M13-007 footprints, M13-023 planar stand-in for tilted/non-cube pairs); the approach-angle helper 0x00550858 (RotatedParentAxis + table) is built for the Z arms. MISSING: ComputePlacementApproachAngle 0x005504A0's own body (Init step 3, so +0x178 still throws NotSupportedException) and Rotation3d::GetAngleAroundXaxis/Yaxis (the helper's X and Y arms throw by default). Ties in GetRotatedParentAxis: 'X'/'Y' instantiations assumed to read rows 0/1 like the verified 'Z'. Pose3d read 2026-10-13 (re-analysis/research/20261013-pose3d-arithmetic.md): the arithmetic is M11-065, the tree M11-066, unread pieces M11-067. Rotation3d GetAngleAroundX/Y are read (R15); the PLTs 0x004AB914/0x004AB908 are the RotationMatrix3d versions; check which class the callers use. OV05 check (re-analysis/research/overnight/20261014-ov05-m12-039-check.md): the MISSING \"Rotation3d::GetAngleAroundXaxis/Yaxis\" is RotationMatrix3d's (0x0084B370/0x0084B3CA, owned by M11-068).\n\nB-M12-3 (2026-10-15), built, awaiting strong verification: Correction of obsolete MISSING wording: X/Y methods are RotationMatrix3d, built under M11-068 and now used by the helper. +178 calls checked ComputePlacementApproachAngle, Warning/return on failure, then supplies angle fields to the inner host drive adapter on success. Precondition Warning/Error/errG ported, including lookup using ctor +78 but error ID from current +7C. MISSING SetApproachAngle native IAction tag/state diagnostic owner 0x00558A90..0x00558B3A and full non-Actionable dynamic-cast integration remain; no native action-state/log tag is invented."
}
```

### M15-020

```json
{
  "id": "M15-020",
  "title": "CalculateDirectionalityClosest, the FindCubesInBeacon predicate and AIBeacon::IsLocWithinBeacon's formula",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "CalculateDirectionalityClosest 0x005E07D8: the rotation about Z from the nearest cube in the beacon; AIBeacon::IsLocWithinBeacon 0x0059C24C: x*x + y*y + z*z in f32 against (radius - margin)^2 plus 0x3727C5AC, `ge`; used only by FindCubesInBeacon (with extra 0)",
    "the function at 0x005E06D8..0x005E07B0 (an averaging function) has no caller found (extraction section 8 item 6)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-04): CalculateDirectionalityClosest, the FindCubesInBeacon predicate (0x0056D12C; IsCarryingObjectId [+8] or [+0x14], 0x00633F88, at 0x0056D142), AIBeacon::IsLocWithinBeacon (0x0059C24C: f32 3-D against (radius - margin)^2 + 0x3727C5AC), IsRunnableInternal's candidate filter (0x005DF1BA..0x005DF250: the set {PickUpObject, RollOrPopAWheelie} from the word table 0x00C6D6A0, DidFailToUse(candidate id, set, cooldown, pose, 20.0f, pi/8)), FindUsableCubesOutOfBeacons (0x0056AE58), CanPickUpObject (0x0063C7F0 with the f32 IsPoseTooHigh 0x00877954..0x008779BC, shared with CanStackOnTopOfObject), AreAllCubesInBeacons (0x0056B450). Still open: AreAllCubesInBeacons compares with the BlockWorld map value at +0x48 (0x0056B5C4..0x0056B5F8) whose writers are unread: the stack uses the located LightCube count (reported MISSING); the function at 0x005E06D8..0x005E07B0 has no caller found; the stack-only IsCarrying filters keep the strict [+8] semantics (the engine's filters there are unread)."
}
```
