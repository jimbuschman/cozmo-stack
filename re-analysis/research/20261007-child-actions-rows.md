| Q18 item | Coverage | Build rows / ownership |
|---|---|---|
| Flip, mount, alignment, retry and dock child construction | CHECKED | P1–P5; recursive census below |
| DriveStraight Init/Check/defaults/profile | CHECKED | D1–D6 |
| MoveLift Init/Check/tolerance/ack | CHECKED | L1–L7 |
| MoveHead Init/Check/variation/ack | CHECKED | H1–H4 |
| TurnInPlace Init/Check/ack/eye parameters | CHECKED | T1–T8 |
| DriveToPose Init/Check/planning timer/results | CHECKED | G1–G7 |
| Backup and DriveOffContacts | CHECKED | B1–B3 |
| Align and IDock lifecycle, subscriptions, results | CHECKED | A1–A2,K0–K7 |
| NoObjectAtPose and VerifyObject recursive children | CHECKED | V0–V5 |
| WaitForImages count writer and completion | CHECKED | W1 |
| PanAndTilt and TurnTowardsPose/Object | CHECKED | U0–U3,O0–O2 |
| Generic runner, compounds, stop/unlock and timeout | CHECKED | Existing checked lifecycle report; boundary below |
| Optional TrackObject and TriggerLiftSafeAnimation | CHECKED | O2/K7 construction; M14/M5 execution boundaries |
| C# hosts, current record quotes, primary provenance | CHECKED | Host table and record appendix |

# Q18 — child-action build rows

Research only; answer to queue5 Q18, reordered by operator after Q16/Q17. No production, manifest or inventory change. CHECKED means instruction inspection, not acceptance. Floats are hexadecimal IEEE754 binary32 unless explicitly binary64. Results are integer hexadecimal, never decompiler tiny floats. Offsets are native action-relative unless named otherwise.

Primary: shipped `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Companion `20261007-q18-*-native.txt` files reopen instructions, callback vtables and literal data. Linear disassembly past returns can include pools, strings, tables or unwind code: these are not claimed as executed instructions. The dumper is checked in under research; decompilation index is navigation only.

The census follows FlipBlockAction, MountChargerAction's alignment/turn/mount/retry, IDock's SetupTurnAndVerify, then each constructed compound recursively. NoObjectAtPose adds TurnTowardsPose and lift, then image wait. TurnTowardsPose adds PanAndTilt's turn and head. VerifyObject adds lift and image wait. Optional TrackObject and post-dock TriggerLiftSafeAnimation are recorded at their construction boundary; their M14/M5 bodies are outside the child docking/charger action layer.

## Current records quoted before comparisons

The following are current manifest title/status/evidence/unresolved quotes, captured at this research baseline. No record is settled or contradicted merely by the coverage label. In particular L1 is a native constructor argument clarification; it does not contradict M4-003's distinct game-message speed path. H4 preserves M4-016's explicit zero-writer uncertainty.

```json
{
  "id": "M4-001",
  "title": "Head angle limits -0.436332..0.776672 rad: command clip in MoveHeadToAngleAction, state-side clamp, -25 deg before calibration",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "MA9 MoveHeadToAngleAction ctor clip with warnings 0x00547F44..0x0054803A",
    "MA22 Robot+0x2FC = -0.436332 in the ctor (0x0051007C..0x00510086)",
    "MA23 RS6 SetHeadAngle 0x0051335E..0x005133E8 (M2 interface)"
  ],
  "unresolved": "built, awaiting strong verification: B-M3M4 batch 1 (2026-10-06). Checked Opus clip-warning text and degree fields repaired: getDegrees f32 multiply bits 0x42652EE1 of the RESCALED target before clipping; invariant one-decimal text; limit fields -25.0/44.5. Existing math/gates untouched. Live SetHeadAngleAsync regression includes comma culture and wrapped4rad input. No settlement. Previous checked verification: Opus verification of B-CORE2 (2026-10-02, re-analysis/research/20261002-B-CORE2-verify.md): NOT YET, log text only. The math is bit-exact (rescale 0x0084C87C..0x0084C936 with 0xC0490FDB/0x40490FDB/0x40C90FDB and the vcvt round trip; strict IsNear 0x0084CC0A; operator> 0x0084CC90, operator< 0x0084CD12). The clip warnings are \"Requested head angle (%.1fdeg) less than min head angle (%.1fdeg). Clipping.\" (0xBEA11C) and the max counterpart (0xBEA169), with getDegrees() of the rescaled angle and -25.0/44.5; Motion.cs:568/573 log invented text. Before: built, awaiting strong verification: B-CORE2 batch 4 (head/lift/action timeout). The clip in SetHeadAngleAsync now goes through Anki::operator< (0x0084CD12) / operator> (0x0084CC90..0x0084CCD0): a-b>0 and !IsNear(a,b,1e-5 bits 0x3727C5AC), so a target within 1e-5 past a limit is sent unclipped and without a warning. RescaleRadians keeps the vcvt.s32.f32/vcvt.f32.s32 round trip (0x0084C91E..0x0084C922, ArmFloatToIntToFloat), so +-inf clips with a warning instead of sending NaN and huge finite values keep their magnitude. Tests: M4ControlTests.M4_001_M4_003_MA9_MA11_HeadIsClippedAndCarriesTheAppDefaults, M4ControlTests.M4_001_MA22_RS6_TheHeadAngleIsMinus25UntilCalibratedThenClamped, M4ControlTests.M4_001_M4_016_TheClipUsesTheOneEpsilonNearTest, M4ControlTests.M4_001_M4_016_InfinityAndHugeAnglesClipInsteadOfSendingNaN."
}
```

```json
{
  "id": "M4-002",
  "title": "Lift presets 0 LowDock 32 / 1 HighDock 76 / 2 HeightCarry 92 / 3 OutOfFOV -1; clamp to 32..92; a negative height goes to the nearer of 32 and 92",
  "status": "EXACT_SOURCE",
  "evidence": [
    "MA14 preset table 0x00C54684, names 0x00548EBC..0x00548EDC",
    "MA13 MoveLiftToHeightAction::Init clamp 0x0054905E..0x005490F6, negative height 0x005490FA..0x00549140",
    "C5: a negative height picks 32 only when strictly nearer; a tie goes to 92 (0x00549104..0x0054913C)"
  ],
  "unresolved": null
}
```

```json
{
  "id": "M4-003",
  "title": "The head and lift API follows the game-message path: caller speed/accel/duration; the original app passes head 10/20 and lift 10/20, duration 0",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "MA10 game SetHeadAngle overwrites +0x90..+0x98 with the message values (0x0052ABE0..0x0052AC24)",
    "MA11 unity/scripts/csharp/Robot.cs:1443-1450 (head 10, 20, 0) and 1638-1646 (lift 10, 20, 0)",
    "MA12 game SetLiftHeight: 32.0 while carrying -> PlaceObjectOnGroundAction (0x0052AC40..0x0052AC9E)",
    "MA9, MA13 action ctor defaults (head 15/20 at 0x00547F14..0x00547F28; lift 10/20 at 0x00548A68..0x00548A78)"
  ],
  "unresolved": "Codex review of B-CORE2 (re-analysis/research/20260930-B-CORE2-review.md), checked by the manager's Opus verifier where noted: DEFECT. Stop-before-unlock and the AreAllTracksLockedBy gate hold (Motion.cs:904-919); the game path's ActionQueue::QueueNow replacement (0x0053E24C..0x0053E35E) is absent, so a repeated game move fails 0x03000019 (Motion.cs:831-844). The queue semantics are in Codex's 20260930-bcore-extractions.md (unchecked). Before: built, awaiting strong verification: B-CORE2 batch 4 (head/lift/action timeout). ~IActionRunner's teardown now stops the track before the lock release: RunAsync/UpdateActions stop (gated on AreAllTracksLockedBy(mask, owner) 0x00541138/0x0054115E) then unlock (0x0054120C..0x0054122A), and the lock owner is a per-action tag (the engine's to_string(+0x60), counter 0x0053FE54..0x0053FE68 store 0x0053FEC6) instead of the old constant. Citation corrected: 0x005408EC is IActionRunner::UnlockTracks, called only from the IAction constructor (0x00540CB0) and IAction::Reset (0x00540D02); the action's end release is inline in ~IActionRunner. Still out of scope: the QueueNow part (a repeated game move never gets 0x03000019) awaits Codex extraction. Tests: M4ControlTests.M4_003_MA_ALockedTrackFailsTheHeadMoveWith03000019, M4ControlTests.M4_003_MA_TheHeadMoveLocksThenUnlocksItsTrack, M4ControlTests.M4_016_MA17_TheDefaultActionTimeoutIs30Seconds."
}
```

```json
{
  "id": "M4-005",
  "title": "Action ids: one u8 counter shared by head, lift and body, pre-incremented from 0; ids run 1..255, 0, 1",
  "status": "EXACT_SOURCE",
  "evidence": [
    "MA8 MC+8 = 0 at construction (0x0063DA7C); pre-increment in MoveLiftToHeight 0x0064070C..0x0064071A, MoveHeadToAngle 0x006407D8..0x006407E6, GetNextMotorActionID 0x006406EC..0x006406F4"
  ],
  "unresolved": ""
}
```

```json
{
  "id": "M4-009",
  "title": "Cube tracking: ObjectAvailable / ObjectConnectionState, Moved / Stopped / UpAxisChanged handling with the charger and carry filters, per-object IsMoving",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "S1, S2, CD10a..CD10c, LC8a..LC8e as before",
    "C11.1 DockingComponent+0xC is an ObjectID, the object currently being docked with (the dock target): default -1 (0x0063BA1E/0x0063BA2A); only writer DockWithObject (0x0063BAAC/0x0063BAB6); readers include the Moved 0x0053416E/74 and Stopped 0x0053497C/84 exclusions",
    "LC8a 0x00533B56 slot > 4 is ignored; AddConnectedActiveObject rejects activeID >= 5 (0x00623040)",
    "LC8e SetID gives every ActiveCube of a type the same ObjectID for the process lifetime (0x004EF468..0x004EF52A)"
  ],
  "unresolved": "wire the [robot+0x280]+0xC dock-target exclusion (C11.1) into the Moved/Stopped broadcasts. The slot <= 4 bound is enforced on the connection path but not in Cubes.Handle, which uses the slot as the stack's BlockWorld ObjectID; separating ObjectID from slot is M11 work (LC8e)."
}
```

```json
{
  "id": "M12-001",
  "title": "Pre-action pose types, their per-type ctor distances and the distance threshold",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "dispatch on actionType 0..5: 0x004E5958 cmp r5,#5 / 0x004E595E tbh / table 0x004E5962",
    "type 0 Docking 75.0 (0x004E5A36 movt r1,#0x4296); angle pi/2 (0x004E5998 vadd d10=pi/2); translation {0,-65,-22} (0x004E59B4/0x004E59C0/0x004E59D4)",
    "type 1 PlaceRelative 40.0 (0x004E5ADA movt r1,#0x4220); angle pi/2 (0x004E5A4E); translation {0,-100,-22} (0x004E5A70 movt r0,#0xc2c8 / 0x004E5A7E)",
    "type 2 PlaceOnGround 4th arg 0 (0x004E5B5A/0x004E5B80); angle pi/2 (0x004E5AF4); translation {0,-49,-22} (0x004E5B0C movt r0,#0xc244 / 0x004E5B16)",
    "type 3 Entry: 0x004E5DA2 (loop tail), no pose",
    "type 4 Rolling 75.0 (0x004E5C50 movt r1,#0x4296); angle pi/2 (0x004E5BA6/0x004E5BB4); translation {0,-65,-22} (0x004E5BE2/0x004E5BE8)",
    "type 5 Flipping 4th arg 0 (0x004E5D04/0x004E5D28); angle 3pi/4 (0x004E5C9C vadd d11); translation {dimX/2+56.5771,-56.5771,-22} (0x004E5CC0 vstr s18 / 0x004E5CB8 movt r0,#0xc262 (0xC2624EEF) / 0x004E5CC6 vstr s16)",
    "PreActionPose ctor 0x0050DCFC stores +0x18 at 0x0050DD46; second ctor 0x0050DB14",
    "SetHeightTolerance 0x0050DAA4 writes +0x14 at 0x0050DB06 (0.5773503 at 0x0050DB10)",
    "the +0x18 value consumer: ActionableObject::GetCurrentPreActionPoses 0x004DF850, 0x004DF9DE vldr s28,[sl,#0x18], stride 0x004DFDC4",
    "ComputePreActionPoseDistThreshold 0x00550098: 3-D distance (0x00550102 vldr s0,[r0,#0x20]; 0x00550122 vsqrt.f32); out[0]=2*dist*sin at 0x00550164/0x005501A4, out[1]=dist*sin at 0x005501A8",
    "the only tolerance branch is angleTolerance > Radians(0): 0x005500BC blx 0x4A4528 / 0x005500C0 cmp r0,#0 / 0x005500C2 beq 0x5501AE; operator> 0x0084CC90 with the ~1e-5 epsilon 0x3727C5AC; false writes -1.0f at 0x005501B6; a GetWithRespectTo failure writes -1.0f at 0x005501C0; no floor",
    "Block::LookupBlockInfo symbol 0x004E4C8C; 0x00503DC8 (75.0) is inside MinimalAnglePlanner::ComputeNewPathIfNeeded 0x00503C18 and unrelated",
    "the world-pose ctor PreActionPose 0x0050DF00 (PLT 0x4A4144): 0x0050DF54 vstr s16,[sl,#0x18] (a); 0x0050DF6C Point3::MakeUnitLength 0x0050E0C0 returns |t|; 0x0050DF7C vadd (|t|+b); 0x0050DFE4 operator*(Pose3d,Pose3d) 0x0084782C; SetHeightTolerance 0x0050E036",
    "GetCurrentPreActionPoses 0x004DF850: first ctor call 0x004DF99A; param_7 from 0x00550984 ([robot+0x10], named preDockPoseOffset_mm at 0x0055137A); param_8 = 0 on this path (0x00550A2A); second ctor call 0x004DFBE6 with b = min(dist2, d) (0x004DFB88 vldr s0,[sl,#0x18]; 0x004DFB94 bpl); the 0x004DFCFC..0x004DFD00 store is the param_8==1 viz block (0x004DFC5E)",
    "all three production threshold callers read both outputs with one ldrd (see M12-020)",
    "preDockPoseOffset_mm: 0x00550984 vldr s16,[r7,#0x10] (r7=r1=PreActionPoseInput); IDockAction+0x10 left 0 by IActionRunner 0x0053FDD0 strd r6,r6,[r5,#0x10]; input+0x10 from IDockAction+0xBC (0x0055161A, 0 at ctor 0x005503AA) and DriveToObjectAction+0x88 (ctor 0x005585A4; no setter; direct callers pass 0); offset-0 branch b=min(dist2,element+0x18)",
    "GetCurrentPreActionPoses param_2: 0x00550A00 bl 0x004EA398 -> &(Robot+0x298); FUN_004df628 0x004DF628 = pose.GetWithRespectTo(pose.FindRoot()) (0x004DF674/0x004DF67E); objectPose = ObservableObject::GetPose 0x00876840 = &(ObservableObject+4)",
    "sb rotation: 0x004E5870/0x004E5890/0x004E58B2/0x004E58D4 blx 0x4A47B0 (Y_AXIS_3D); base 0x010590AC (0x004E5876/0x004E587A); angles 0x3FC90FDB/0x40490FDB/0xBFC90FDB; applied with Transform3d::RotateBy 0x0084BB3A (0x004E5A1C/0x004E5AC0/0x004E5B60/0x004E5C36/0x004E5D0A)",
    "Pose3d(angle, Z_AXIS, translation) with the marker as 4th ctor arg (parent): 0x004E59EE strd r0,r6,[sp]; precompute 0x004E592C..0x004E5938 s16=-size[8]/2, s18=size[4]/2+56.5771; PreActionPose ctor re-roots 0x0050DDA2..0x0050DDB2 passedPose.GetWithRespectTo(marker.parent)",
    "0x004DF88A..0x004DF8E8 collection; 0x004DF94A..0x004DF99A filters; 0x004DF9B8..0x004DFBE6 the b computation; 0x004DFC1A..0x004DFC58 the validity push; 0x004DFDD4 the return byte",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q2; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md (2.3 formula, 2.2b)"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): the Flipping corner is binary32 0x42624EEF (X = size.y*0.5f + corner, 0x004E583A..0x004E5934; Y = 0xC2624EEF), the close-enough threshold follows the binary32 sequence (0x00550102..0x00550164: vmul/vadd, vsqrt, sinf, vmul, vadd), the tolerance guard is operator> 0x0084CC90 (strict IsNear, so exactly 1e-5f now passes), OffsetEpsilon is 0x3727C5AC. Still open: the preceding validity filter is M12-036's; ComposeWorld's offset geometry (0x004DFA26..0x004DFB46) and IsRestingFlat are double where the engine is float; sinf/sqrt use MathF (libm stand-in); PreActionPose+0x14 has no reader found (M12-038); the per-object cache is never cleared."
}
```

```json
{
  "id": "M12-003",
  "title": "The dock-message order: CheckIfDone -> DockWithObject -> builder -> send",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x005521AC IDockAction::CheckIfDone; 0x005522AE blx 0x4AB9F8 (DockWithObject)",
    "0x0063BA44 DockingComponent::DockWithObject; 0x0063BD50 builder",
    "0x0063BDB8 blx 0x4B94BC (EngineToRobot(DockWithObject&&)); 0x0063BDC4 blx 0x4A5368 (SendMessage)"
  ],
  "unresolved": "compare Docking.cs with the order above"
}
```

```json
{
  "id": "M12-004",
  "title": "The dock retry drops the pose it just failed from",
  "status": "EXACT_SOURCE",
  "evidence": [
    "IBehavior::UseSecondClosestPreActionPose starts at 0x005BEE56 (0x005BEE40 is asrs r4,r4,#1 mid-function)",
    "0x005BEE66 blx 0x4ABFBC (GetPossiblePoses); 0x005BEE7C asrs r0,r0,#2; 0x005BEE80 cmp r0,#2",
    "0x005BEE88 ldr r0,[r6,#0x2c]; 0x005BEE90 blx 0x4B20A0 (RemoveMatchingPredockPose); 0x005BEE98 cmp r1,#1; 0x005BEE9C strbeq r0,[r4]",
    "IDockAction::RemoveMatchingPredockPose 0x00551418"
  ],
  "unresolved": "compare DockActions.cs with UseSecondClosestPreActionPose 0x005BEE56. Built (R-VIS fix round) as DriveToObjectAction.ExcludePoses in DriveActions.cs: GetPossiblePoses, then RemoveMatchingPredockPose (M12-029) per excluded pose while at least two poses remain (0x005BEE7C..0x005BEE80), the in-position flag cleared when something was removed. The engine holds ONE match pose; DockHelper (M8) accumulates the poses of every failed attempt, so the removal is repeated per pose (a C# generalisation)."
}
```

```json
{
  "id": "M12-005",
  "title": "Every field of DockWithObject",
  "status": "EXACT_SOURCE",
  "evidence": [
    "word 0 = 0: 0x0063BB34 movs r4,#0 / 0x0063BB7E str r4,[sp,#0x1c]",
    "words 1..3 = IDockAction +0xAC/+0xB0/+0xB4, defaults 60/200/500 at the ctor 0x005502D8 (0x42700000/0x43480000/0x43FA0000)",
    "byte 4 = DockAction +0x80; byte 5 = +0x95; byte 6 = +0xBA (0); byte 7 = +0xBB (0; PickupObjectAction 0x005536EA writes 2, RollObjectAction 0x005565D6 writes 0 (the 5 stored at 0x005565DC is the DockAction byte +0x80, not +0xBB)); byte 8 = +0xC1 (0; PickupObjectAction 0x005536F8 writes 1)",
    "the complete list of +0xBB writers (whole-.text scan, re-analysis/research/20260929-R-VIS-verify-M12-gap2.md item 1): 0x005533F6 AlignWithObjectAction case = 2; 0x005536EA PickupObjectAction = 2; 0x00554E12 PlaceRelObjectAction::InitInternal = 3 iff |B'| >= 1e-5 (movpl, so NaN also sets it); 0x005554D0 PlaceRelObjectAction::SelectDockAction = 0; 0x005565D6 RollObjectAction = 0; 0x0055C5A2 SetDockingMethod = its argument; the ctor default is 0 (the 0x100 word at 0x005503AA). Byte 7 (wire offset 0x13) is DockingMethod = [this+0xBB]; the builder 0x0063BD50 writes +0x10 DockAction, +0x11 bool [+0x95], +0x12 retries [+0xBA], +0x13 method [+0xBB], +0x14 bool [+0xC1]; the +0x95 bool is the IDockAction ctor's last bool (0x00550382), read at 0x00552278"
  ],
  "unresolved": "Built (R-VIS fix round): byte 5 = [+0x95] (DockActionBase.DockFlag95; PlaceRelObjectAction sends its ctor bool), byte 7 = [+0xBB] with the writers PlaceRelObjectAction::SelectDockAction 0 then InitInternal 3 iff |B'| >= 1e-5 (post-transform, NaN sets it), PickupObjectAction 2, RollObjectAction 0; AlignWithObjectAction's 2 (0x005533F6) is ChargerActions.cs (M13-016). NOT built: DriveToPickupObjectAction::SetDockingMethod (0x0055C5A2; no such C# class). The +0x95 bool of Pickup/PopAWheelie/Roll (their own third ctor argument) is not modelled: those C# actions send false, and their engine callers' values are not itemised."
}
```

```json
{
  "id": "M12-006",
  "title": "Letting go of a carried object leaves it Dirty where the lift left it",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x006333D4 SetCarriedObjectAsUnattached; 0x00633458 AddRobotRelativeObservation; 0x00633930 DeleteLocatedObjects",
    "BehaviorPutDownBlock passes false: 0x005C84CE",
    "PickupObjectAction::Verify passes true: 0x00553CAA, 0x00553D2A",
    "HandlePickAndPlaceResult 0x00533780: release only when blockStatus==1 (0x005337A8 cmp r0,#1 / 0x005337AA beq) and success!=0 (0x00533898 ldrb r0,[r5,#4] / 0x0053389A cbz 0x5338a6), with bool false (0x005338A0 movs r1,#0 / 0x005338A2 blx 0x4A7B7C SetCarriedObjectAsUnattached)",
    "attach only when blockStatus==2 (0x005337A4/0x005337A6) and success!=0 (0x00533848 ldrb / 0x0053384A cbz), via SetDockObjectAsAttachedToLift (0x00533850 blx 0x4AA06C)",
    "PickAndPlaceResult layout: +4 success (0x00533794 ldrb r1,[r5,#4]), +5 dockingResult (0x005337F8 ldrsb), +6 blockStatus (0x0053379E ldrb r0,[r5,#6])",
    "VisionComponent::EnableMode(1,true) runs unconditionally in the BlockPlaced branch (0x005338AE blx 0x4A91CC)"
  ],
  "unresolved": "compare Docking.cs with SetCarriedObjectAsUnattached 0x006333D4"
}
```

```json
{
  "id": "M12-009",
  "title": "The dock helper allows two attempts",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x005B8050 RespondToPickupResult; 0x005B8192 ldr.w r0,[sl,#0x108]; 0x005B8196 cmp r0,#1; 0x005B8198 bls 0x5b826c",
    "0x005B814A movs r6,#2 (the log literal)",
    "PickupBlockHelper::StartPickupAction 0x005B7B48"
  ],
  "unresolved": "compare ManipulationSystem.cs with RespondToPickupResult 0x005B8050"
}
```

```json
{
  "id": "M12-011",
  "title": "DriveToObjectAction::CheckIfDone: the type-6 tolerance compare and the production std::function check",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00559880 CheckIfDone; 0x005598E2/0x005598E6 form 0x04000001; 0x005599FE mov r4,r8",
    "0x005598B6/0x005598BA form 0x03000004 BadObject",
    "this function does not call ComputePreActionPoseDistThreshold (M12-020)",
    "0x0055989C [this+0x148]==0 returns 0; 0x005598EA cmp r0,#6; 0x00559A86..0x00559AE4 the +0x150 std::function path; flag byte at 0x00559A9C",
    "0x00559998..0x005599BE the type-6 compare; 0x00559B3E -> 0x00559894 BadObject on the failed transform",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1 rows and re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md objection A7 (the record described only the type-6 path)",
    "0x00559A86..0x00559AE6 the non-type-6 CheckIfDone tail (manager read)"
  ],
  "unresolved": "Built: the production path calls the +0x150 function with the in-position flag (now an in/out ref bool, PosesFunction delegate) and maps flag 0 to 0x04000001; the type-6 compare only for ActionType 6; the default function is GetPossiblePoses (M12-029) over M12-031/M12-001/M12-036 (validity cone and obstacles built; UprightOnly is gone); the flip installers are M12-035. CheckIfDone passes the +0x150 function a fresh empty pose vector and an in-position bool of 0 (0x00559A86..0x00559A9C) and maps flag 0 to 0x04000001, non-zero to the function's result, for the default and the flip functions alike (the earlier MISSING and the InPositionFlagAtCheckIfDone inputs are gone). Still open: (c) the drive's goal is a labelled single reduced goal (M12-022). The initial in-position flag is 0 with an empty vector (manager read): supply it; do NOT throw."
}
```

```json
{
  "id": "M12-013",
  "title": "DockingErrorSignal begins with the timestamp, not the geometry",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x0063BE80 UpdateDockingErrorSignal; 0x0063C14A str r6,[sp,#0xa0] (timestamp); 0x0063C15E/0x0063C174/0x0063C180/0x0063C1CE",
    "0x007C0B26 Pack reads words at +0, +4, +8, +0xc, +0x10 then bytes at +0x14, +0x15",
    "the clamp is 40 degrees (M12-019)"
  ],
  "unresolved": "compare Docking.cs with UpdateDockingErrorSignal 0x0063BE80"
}
```

```json
{
  "id": "M12-014",
  "title": "The docking error signal's last two bytes are whatever was on the engine's stack",
  "status": "COMPATIBILITY_POLICY",
  "evidence": [
    "the fill writes five words +0xa0..+0xb3 (0x0063C14A, 0x0063C15E, 0x0063C174, 0x0063C180, 0x0063C1CE); no store to +0xb4/+0xb5",
    "0x007C0B26 reads the bytes at +0x14 and +0x15 and sends them",
    "the engine value is indeterminate, so this stack sends zero (SD2)"
  ],
  "unresolved": ""
}
```

```json
{
  "id": "M12-017",
  "title": "IDockAction::Init setup, handler registration and the docking squint",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005514FC Init; 0x00551598 GetLocatedObjectByIdHelper; 0x00551640 GetPreActionPoses; 0x005516EA movs r3,#0xc5; 0x00551750 movs r3,#0xda",
    "the handler bodies are the action's own state machine",
    "the AddSquint call 0x00552394 is inside IDockAction::CheckIfDone 0x005521AC (body 0x005521AC..0x005523F6), not Init 0x005514FC (body 0x005514FC..0x00551C8A, no call to PLT 0x4ABA04)",
    "AddSquint args: TrackLayerComponent at robot+0xC0 (0x00552362 ldr.w r5,[r0,#0xc0]); name \"DockSquint\" (literal at 0x005524A0, 10 bytes); f1=0x3F866666=1.05 (0x0055237A/0x00552388); f2=0x3EB33333=0.35 (0x0055237E/0x0055238C); f3=0xC1200000=-10.0 (0x00552378/82/86); return at 0x00552398 strb [r4,#0xf4]",
    "TrackLayerComponent::AddSquint 0x0064F370 -> FaceLayerManager::GenerateSquint 0x0058D738 (PLT 0x4BA164) then AddPersistentLayer 0x4AE9E0; GenerateSquint ignores its float args and clips EyeScaleY=0.35, EyeScaleX=1.05, UpperLidAngle=-10.0 on both eyes",
    "GenerateSquint track: two ProceduralFaceKeyFrames, the second at 250 ms (0xFA) with Reset (0x0058D820/0x0058D82C)",
    "0x00551540..0x00551C14 the Init order; 0x00551C0C ldr r1,[r0,#0x30] / 0x00551C10 blx r1 / 0x00551C14 cbnz r4 (InitInternal is the last step; PlaceRelObjectAction vtable 0x010222F0 slot +0x30)",
    "0x00555435..0x005555B5 SelectDockAction",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q6; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q6",
    "0x00551960..0x00551B8C marker choice; 0x00551C0C..0x00551C4C the tail; 0x00529CF4..0x0052AAC6 the wire stores",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q10.1-10.2; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md (10.1, 10.6)"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): DockWithObject is sent first (0x005522AE), the squint is added on the rising edge of IS_PICKING_OR_PLACING after the dock message (0x0055233E..0x00552398: [+0x94] stored only on the prev == 0 path, so at most one add per dock), behind ShouldApplyDockingSquint 0x005524AC ([[[robot+0x264]+0x30]+0x14] != 1, through a seam that returns 3 and reports MISSING); the 0xC5/0xDA handlers are registered at IDockAction::Init (0x005516EA, 0x00551750) for the action's lifetime; DockAsync clears its state in a finally. Still open: the handler bodies (M12-034), the StandIn sub-actions, the first gate [[IDockAction+0xCC]+4] is read only on later RobotState messages (the engine reads it on the dock-start tick), CheckIfDone result 0x03000016, RemoveSquint, the IDockAction constructor default of [+0xF7]."
}
```

```json
{
  "id": "M12-018",
  "title": "The dock abort path",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x0063BE10 AbortDocking; 0x0063BE2A blx 0x4B94C8; 0x0063BE36 blx 0x4A5368"
  ],
  "unresolved": "compare Docking.cs with AbortDocking 0x0063BE10"
}
```

```json
{
  "id": "M12-019",
  "title": "The docking-error-signal clamp is 40 degrees",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0063C182/0x0063C188 form 0x3F32B8C2 (40 deg); 0x0063C194 blx 0x4A6FD0 (ClampPoseToFlat)",
    "distinct from the 20 degrees M11-006 records for the pose-confirmation path (0x3EB2B8C2)"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): the docking-error-signal clamp is binary32 0x3F32B8C2 (0x0063C182..0x0063C194) with a one-ulp test on each side through BlockWorld.ClampPoseToFlat. Still open: the clamp's arithmetic inside BlockWorld is double."
}
```

```json
{
  "id": "M12-020",
  "title": "The production callers of ComputePreActionPoseDistThreshold",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0055ACF0 blx 0x4AB938 (DriveToPoseAction::CheckIfDone)",
    "0x005561A0 blx 0x4AB938 (PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses)",
    "0x00550FF8 blx 0x4AB938 (IDockAction::GetPreActionPoses)",
    "DriveToObjectAction::CheckIfDone 0x00559880 contains no call to 0x4AB938",
    "the formula itself is M12-001",
    "DriveToPoseAction 0x0055ACF0 blx then 0x0055ACF4 ldrd r0,r1,[sp,#0x80] / 0x0055ACF8 strd -> sp+0x8c; used at 0x0055ADA8 IsSameAs 0x4A7060",
    "PlaceRelObjectAction 0x005561A0 blx then 0x005561B0 ldrd r0,r1,[sp,#0x54]; 0x005561B4 r2=100.0; 0x005561BC stm r3!,{r0,r1,r2}; used at 0x005561EA IsSameAs with Radians(0x3E060A92)",
    "IDockAction::GetPreActionPoses 0x00550FF8 blx then 0x00550FFC ldrd r1,r2,[sp,#0x70]; stored at sb+0x20 (0x00551002) and sb+0x24 (0x00551008); positivity checks 0x0055101C/0x00551048; TooFarFromGoal 0x04000001",
    "FlipBlockAction::Init 0x0055EE5E blx 0x4AB950 (GetPreActionPoses); the pre-action check uses the threshold pair out[0]/out[1] (0x00551002/0x00551008), not a 100 mm box",
    "Pose3d::IsSameAs 0x00846EA4 -> IsSameAs_WithAmbiguity 0x00846F3C -> GetAngleDiffFrom 0x0084A694 (full quaternion, acos(2*dot^2-1)); RotationAmbiguities 0x0084B496 is empty (begin=end=0)",
    "Robot::GetHeight 0x00516F0C: sinf; *66.0 (0x00516F54); +45.0 (0x00516F58); +5.0; vcmpe 67.7 (0x00516F60 = 0x42876666)",
    "0x005500EE..0x005500F4 GetWithRespectTo(this=r2, other=r1); 0x00550102..0x00550122 the 3-D norm",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q2 and re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md (A1)"
  ],
  "unresolved": "Argument order is (goal / pre-action pose, object pose, angle) and the norm is 3-D (CubePreActionPoses.DistanceThresholdMm). Caller 1 (DriveToPoseAction.ReadyState, both outputs, z = Robot::GetHeight), caller 2 (PlaceRelObjectOffsetPoses.Filter, {out0, out1, 100.0}, Radians 0.1308997) and caller 3 (IDockAction::GetPreActionPoses, DockPreActionPoses.Evaluate with M12-031) are wired. Still open, so the record stays IMPLEMENTATION_GAP: caller 1's CheckIfDone state machine is not driven by the live drive (M12-023, M12-032); caller 2 has no production caller in this stack (M12-026); caller 3's pose list carries the labelled UprightOnly reduction (M12-001). FlipBlockAction (M13) reaches caller 3 through DockActionBase.IsCloseEnoughToPreActionPose with flag A clear and its own tolerance for both the threshold and the yaw test; its engine flag A and tolerance are not itemised here."
}
```

```json
{
  "id": "M12-021",
  "title": "The block face-def records and the per-face, per-rotation pre-action-pose gate",
  "status": "EXACT_SOURCE",
  "evidence": [
    "record layout {FaceName u32 @0; MarkerType code u32 @4; size 25.0 @8; maskForTypes0And5 u8 @0xC; maskForType4 u8 @0xD}",
    "LIGHTCUBE1 0x00C45C40, LIGHTCUBE2 0x00C45CA0, LIGHTCUBE3 0x00C45D00, GHOST 0x00C45D60",
    "for the three real cubes +0xC = 0x05 for FaceNames 0..3, 0x00 for FaceName 4, 0x0F for FaceName 5; +0xD = 0x0F for all; GHOST 0x0F/0x0F everywhere",
    "types 1 and 2 have no mask test; type 3 produces nothing",
    "the sb rotation table is 0x010590AC..0x010590E8 (stride 0x14), four RotationVector3d about Y_AXIS_3D (PLT 0x4A47B0) at angles 0/pi/2/pi/-pi/2, applied with Transform3d::RotateBy 0x0084BB3A (corrected C-E7/E9; the earlier Z/0x0105B0AC citation was wrong)",
    "Block::Block/AddFace pass 0,0 and ignore the masks (0x004E5FF8/0x004E6010; 0x004E564C)"
  ],
  "unresolved": "compare the C# pre-action pose generation with the face-def masks and the four map entries"
}
```

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

```json
{
  "id": "M12-023",
  "title": "DriveToPoseAction::CheckIfDone: the path-status state machine, tolerance and arrival test",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0055AB4C..0x0055B0CC CheckIfDone; 0x0055AB84 tbh table; 0x006492D6..0x006492E8 and 0x0102F610 the status name table (0 Failed .. 6 WaitingToCancelPathAndSetFailure)",
    "0x0055ACA2..0x0055ACFC tolerance; 0x0055AD7A..0x0055B0A6 arrival test; 0x0055B0AA..0x0055B0CC tail; 0x0055A31E, 0x0055933E, 0x0055962E the +0xC0 writers",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1c; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md priority check 3 (A6: the logged diff is 3-D, log only)"
  ],
  "unresolved": "DriveToPoseAction.CheckIfDone(DriveToPoseTick) builds the full state machine (entry gates, states 0-4, the deadline at +0xB0 from +0xA8 = 4.0, the state-4 tolerance pair, IsSameAs, the path-id rule, the PlayEndAnim tail); RunAsync applies the same state-4 test (ReadyState). Still open, so the record stays IMPLEMENTATION_GAP: CheckIfDone(DriveToPoseTick) has NO production caller: the live drive does not poll it, because the C# has no PathComponent status word and which robot events set states 1..6 is not fully in the inventory (M12-032 RECOVERABLE_GAP). RunAsync's tail is the state-4 test on the path's terminal (Completed) event, and its result for 'finished but outside the tolerance' is 0x04000002 (state 4 with equal path ids, 0x0055B056; earlier code gave 0x04000001): ASSUMPTION, kept visible in the code, that both ids are the completed path's id (+0x42 last sent, +0x44 last reported started, M12-032, and a Completed event follows a Started one); callers that switch on the code (CubeGameBehaviors' PopAWheelie retry on DidNotReachPreActionPose) see the change. The Interrupted event returns FailedTraversingPath without the Ready test although the engine sets status 4 for it too. PlayStartAnim/PlayEndAnim are M12-024 (RECOVERABLE_GAP) and the default handler throws."
}
```

```json
{
  "id": "M12-025",
  "title": "PlaceRelObjectAction: ctor, InitInternal and TransformPlacementOffsetsRelativeObject",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00554BE0..0x00554D0E ctor; 0x00554DCC..0x00554E30 InitInternal; 0x00554E40..0x00555094 the transform; literals 0x3E860A92, 0xBFC90FDB, 0x3FC90FDB, 0xC0490FDB, 0x40490FDB, 0xC1800005",
    "0x0052A6C2..0x0052A754 the wire factory; 0x0055C7D0 ctor, 0x0055C83A ldr sb,[sp,#0xAC], 0x0055C882 cmp sb,#0; 0x0052A6DC movs r1,#1",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q2; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md priority check 1 and objections A3, A4, A5",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q6, Q7; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q6, Q7"
  ],
  "unresolved": "Built as before plus: the LEGACY constructor PlaceRelObjectAction(m, id, onTop) is A = B = 0, transform skipped, +0x95 false; its Offsets are kept apart as raw docking-error-signal offsets and touch nothing else (CheckPreActionPose stays true, OffsetB 0, +0xBB 0, DockingMethod Default), CHOICE because the producers of the callers' arguments are unread (M12-028). What still changes for the legacy callers (CubeGameBehaviors 597/645/1049, ManipulationBehaviors 313): onTop true now needs CanStackOnTopOfObject and otherwise returns 0x03000004 (SelectDockAction, source-backed); not carrying is 0x03000011 (was 0x03000000); the turn/marker/placement-clear flow is the PRE-BATCH stand-in unchanged (turn, marker facing the robot, VerifyNoObjectAtPlacementPose, no visible-marker wait, no 0x0300001D path; fix round 3); the pre-action check (when a caller leaves it on) goes through the validity cone/obstacles. Wire bytes: unchanged (byte 4 by onTop as before, byte 5 false, byte 7 0). Still open, so IMPLEMENTATION_GAP: (a) the pre-dock wire route (DriveToPlaceRelObjectAction 0x0055C7D0) is not built and FromWireMessage has NO production caller; (b) stored-but-unconsumed fields (+0xF0, +0xFC, +0x100, +0xC0 beyond the compound gate, IDockAction types 0x15/0x18/0x19); (d) the transform's exact-equality boundary is tested on the compare helper."
}
```

```json
{
  "id": "M12-026",
  "title": "PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005558F4..0x00556300 the function; 0x005561A0 threshold call; 0x0055E1C6, 0x005B5908..0x005B5918, 0x005B7030..0x005B7054 the callers; 0x005B6F5C..0x005B70DA IsAtPreActionPoseWithVisualVerification",
    "re-analysis/research/20260929-R-VIS-pre-extraction.md Part 3 item 12 (rows 12.1-12.18); re-analysis/research/20260929-R-VIS-verify-pre-items11-13.md (all PASS); re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q2 rows for A,B sources",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q3, Q4; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q3, Q4"
  ],
  "unresolved": "Built as before; IsAtPreActionPoseWithVisualVerification now maps the in-position byte for BOTH branches (0 -> 0x04000001) and for ActionType != 1 calls GetPreActionPoses {flag A 0, tol 0x3E060A92, distance 0, no approach angle}, returning a non-zero result (the throw is gone); Compute/IsAt... take the obstacle provider. Still open: (a) KnownMarker.SizeMm is this stack's field for the marker width (+0x10); (b) the two GetWithRespectTo failure exits are unreachable in a stack with no pose tree; (e) NO production caller: the engine's callers (DriveToPlaceRelObjectAction lambda, DriveToHelper, IsAtPreActionPoseWithVisualVerification) are M8 and not built."
}
```

```json
{
  "id": "M12-027",
  "title": "The lift pose's per-tick update and the BroadcastObjectPoseChanged chain",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "0x005151AC ComputeLiftPose; 0x00506F88 BroadcastObjectPoseChanged; 0x00624808 OnObjectPoseChanged",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q3"
  ],
  "unresolved": "read Robot::ComputeLiftPose 0x005151AC and its callers, and BroadcastObjectPoseChanged 0x00506F88 through to OnObjectPoseChanged 0x00624808. Until then the broadcast is counted (ObjectPoseConfirmerRelative.UnreadBroadcasts, an Interlocked counter) and the lift-held pose is recomposed per frame by DockingSystem.UpdateCarriedObjectPose."
}
```

```json
{
  "id": "M12-029",
  "title": "The default +0x150 pose function, the DriveToHelper functor and RemoveMatchingPredockPose",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005586AC setter; 0x0055DB72, 0x0055DBDE default functors; 0x00558C80 GetPossiblePoses; 0x005B61A2 the DriveToHelper functor; 0x00551418 RemoveMatchingPredockPose",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q9; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q9 (PASS)"
  ],
  "unresolved": "Built: GetPossiblePoses (in/out ref flag, poses from M12-031 with the M12-036 filter), RemoveMatchingPredockPose, the DriveToHelper functor (no production caller: DriveToHelper is M8). Not modelled: the setter's guard. Assumption, visible in the code and trace: when the functor empties the list, DriveToPoseAction fails with 0x03000013. UprightOnly is gone."
}
```

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
  "unresolved": "Built: Clone keeps NOTHING (ObjectID -1 = uint.MaxValue, fresh markers, InitPose Known), so the inner GetPreActionPoses does not answer 0x03000004 and the chain goes to poses and a drive; IsPlacementGoalFree returns true before any query when the carried object is not found, otherwise the default filter's FindLocatedIntersectingObjects (ignore {carried id}; reference quad at +0x16C with padding 0; the candidate's quad padded by [+0x17C]; Intersects; no Z test; current origin; M13-007 footprints, M13-023 planar stand-in for tilted/non-cube pairs); the approach-angle helper 0x00550858 (RotatedParentAxis + table) is built for the Z arms. MISSING: ComputePlacementApproachAngle 0x005504A0's own body (Init step 3, so +0x178 still throws NotSupportedException) and Rotation3d::GetAngleAroundXaxis/Yaxis (the helper's X and Y arms throw by default). Ties in GetRotatedParentAxis: 'X'/'Y' instantiations assumed to read rows 0/1 like the verified 'Z'."
}
```

```json
{
  "id": "M12-031",
  "title": "IDockAction::GetPreActionPoses and SelectObject/GetObstacles",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005508C8..0x00551200",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q10; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q10 (PASS)",
    "0x005B6F5C..0x005B70DA, 0x0055EE10..0x0055EE66",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q9, Q3.7; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md"
  ],
  "unresolved": "Built: GetPreActionPoses over M12-001/M12-036 (validity cone and obstacles via ManipulationSystem.GetObstacles), IsAtPreActionPoseWithVisualVerification maps the byte for both branches (flag A 0, tol 0x3E060A92), FlipBlockAction.Init calls GetPreActionPoses with flag A = the byte +0x140 (ctor default 1), tolerance 0x3DB2B8C2 and acts only on the result code; the contradicting DockActionBase.IsCloseEnoughToPreActionPose is removed. CHOICE: FlipBlockAction.Init's distanceFromMarker (0) and useApproachAngle (false) are not itemised for that caller."
}
```

```json
{
  "id": "M12-032",
  "title": "The PathComponent drive-to-pose status word: its setters and the unread branch conditions",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "0x0064941C, 0x0064937C, 0x00649390, 0x0064B370, 0x0064B61C, 0x006493B4, 0x006494CC",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q8; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q8",
    "0x0064A5AE..0x0064A5B6, 0x004FE8AE..0x004FE8D4, 0x00858F90..0x0085913C, 0x008515F8..0x008516E8, 0x00841BA0..0x00841C68, 0x00846F3C..0x00847198",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q8; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md Q8"
  ],
  "unresolved": "read the four M13 functions' branch conditions and what [+0x42]/[+0x44] are; until then the live drive does not poll CheckIfDone (M12-023)."
}
```

```json
{
  "id": "M12-034",
  "title": "Unread dock-action handler bodies and the ramp/charger validity overrides",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "0x005516EA, 0x00551750, 0x00551F9C, 0x00550858",
    "re-analysis/research/20260929-R-VIS-verify-M12-gap2.md 'What stays open'"
  ],
  "unresolved": "read the handler bodies for tags 0xC5, 0xDA and the external interface, and the ramp and charger IsPreActionPoseValid overrides (0x0050F9AC, 0x004EA648)."
}
```

```json
{
  "id": "M12-035",
  "title": "The flip-block +0x150 installers and DriveAndFlipBlockAction::GetPossiblePoses",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0055E208..0x0055E32A, 0x0055F366..0x0055F464, 0x0055E3B0..0x0055E3F6, 0x0055F4C6..0x0055F52E, 0x0055EB04..0x0055EB8A, 0x0055F592..0x0055F5A4, 0x0055EC04..0x0055EC2E, 0x0055F60E..0x0055F624, 0x0055E438..0x0055E91A",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q3; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md Q3 (PASS)",
    "0x00559A86..0x00559AE6 the non-type-6 CheckIfDone tail (manager read)",
    "0x0055B2C8..0x0055B438 the IDriveToInteractWithObject ctor compound construction and its AddAction flags (manager read)"
  ],
  "unresolved": "Built: DriveAndFlipBlockAction.GetPossiblePoses (request, non-zero result returned, closestOnly pushes the world pose of poses[closestIndex], nearest A / second-nearest B by 3-D robot-relative distance with strict <, one pose -> A, known face -> A if dist(A,face) > dist(B,face) else B, else A if A.y >= B.y else B), L1/L2 (DriveAndFlipBlockAction.PosesFunctionL1L2, ShouldDriveToClosestPreActionPose) and L3/L4 (DriveToFlipBlockPoseAction). INDETERMINATE, not invented: DriveAndFlipBlockAction+0x100 stays null; the helper block over the empty vector leaves *inPos 0 and flip+0x140 = 1 for both readings, which is FlipBlockAction's constructor default, so DriveAndFlipBlockAction no longer sets CheckPreActionPose=false. The null-weak-lock store through 0x140 is a crash and is NOT reproduced (Flip is never null). CHOICE: GetLastObservedFace(pose, true) is mapped to namedOnly (M14's use maps its false to namedOnly false). MISSING: ComputeDistanceSQBetween's body (a non-empty helper vector throws; the empty vector the engine passes returns 0, so the live path does not need it). Consequence built from the records: none of L1..L4 writes CheckIfDone's in-position bool (fresh 0), so the flip drive's DriveToObjectAction ends 0x04000001; IDriveToInteractWithObject adds the drive-and-wait compound with AddAction(inner, true, false) (0x0055B370), so the outer compound IGNORES it: DriveAndFlipBlockAction.RunAsync continues to the two turns (when maxTurn > 0) and the flip and returns the flip's result (the derived ctor adds FlipBlockAction with (false, false), 0x0055E2EE movs r3,#0; 0x0055E2F0: a failed flip fails the compound and is returned). STAND-IN, MISSING (M8): the WaitForLambda action's body (type 0x33, ctor 0x0055B554; the lambda's condition) and ICompoundAction's ignoreFailure handling inside its UpdateInternal are unread: the drive result is simply ignored and the wait is not modelled (trace line 'ignored by the outer compound'). Other IDriveToInteractWithObject subclasses have no C# counterpart and are unchanged. DriveToFlipBlockPoseAction has NO C# caller. Fix round 3 (M13-028, call sites): DriveAndFlipBlockAction.RunAsync adds no drive, wait-lambda or turn actions when the robot carries the object id (0x0055B258..0x0055B264 -> 0x0055B45E; only the flip runs, and its Init answers 0x03000004) and tests maxTurn with Radians::operator> (epsilon 1e-5, 0x0055B38C). Live behaviour: flip timing and distance are M13-028's."
}
```

```json
{
  "id": "M12-036",
  "title": "ActionableObject::IsPreActionPoseValid and BlockWorld::GetObstacles: the pre-action pose filter",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x004DF2C0..0x004DF5F4 IsPreActionPoseValid; 0x004DF61C the literal; 0x00626D44..0x00626E0E GetObstacles; 0x00626BB5, 0x0062C1D4..0x0062C2D6",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q2.4-2.8; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md Q2 and objection 2.8"
  ],
  "unresolved": "Built for cube candidates: PreActionValidity.IsPreActionPoseValid (|R22-1| < 0x3E0930A4 with NaN invalid, the empty-vector shortcut, the swept footprint with 55.9/27.1/10-step and n = (int)floorf((L + 55.9)/10) (L = 0 gives 5), the direction multiplied by 1/len and the centre/left/right points accumulated by +10u per sample as the engine does (0x004DF3C2, 0x004DF3D2, 0x004DF582..0x004DF5DA), the own-ID skip and the own-quad-contains-centroid skip) and PreActionValidity.GetObstacles / ManipulationSystem.GetObstacles (ignore ids, minZ = robot z, maxZ = minZ + Robot::GetHeight, the exclusion predicate, M13-007 footprints). Read, not choices (fix-round-2 verifier): Quadrilateral::ComputeCentroid 0x004DF7BE = (c0 + c2 + c1 + c3) * 0.25 and CarryingComponent::GetCarryingObjects 0x00633BB0 inserts the carried id and the object on top. CHOICE: an obstacle or own footprint that cannot be computed (non-cube, tilted) is the M13-023 planar 22 mm stand-in (flagged IsStandIn, traced). Still open: the Ramp and Charger overrides (M12-034). CORRECTED in fix round 3: floorf, not ceilf; the earlier ceil-based counts in the tests were hand-rederived (n = 19 for the Front sweep, 5 for a zero-length one) and a test distinguishes floor from ceil."
}
```

```json
{
  "id": "M12-037",
  "title": "SetupTurnAndVerifyAction and GetObservedMarkers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00876C50..0x00876C96, 0x00551F9C..0x00552164",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q10; re-analysis/research/20260929-R-VIS-verify-M12-gap3.md Q10 (PASS)"
  ],
  "unresolved": "Built: SetupTurnAndVerifyAction (the compound [VisuallyVerifyNoObjectAtPose (if [+0xC0]; pose (x,y,z+dz), half 0.5*dims, own id ignored), TurnTowardsObject (if [+0xC8]; code [+0xF7] ? ANY_CODE : [+0x82], Radians(0), true, false)]) replacing TurnAndVerifyStandIn, and GetObservedMarkers. The numeric value of Marker::ANY_CODE is not in the inventory, so DockTurnCode.Any is a named case. CHOICES: the marker last-observed time is the object's LastObservedTimestamp for the markers of its last observation and 0 otherwise; GetDimInParentFrame<'X'/'Y'> are assumed to read rows 0/1 like the verified 'Z' (RotatedParentAxis; the older CubeGeometry.DimInParentFrameZ reads a column and is untouched, equal for cubes). The sub-action BODIES (M8 VisuallyVerifyNoObjectAtPoseAction, M13 TurnTowardsObjectAction) stay labelled stand-ins (StandInDockSubActions), so the path is not whole."
}
```

```json
{
  "id": "M12-038",
  "title": "The PreActionPose+0x14 height tolerance reader",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "0x0050DAA4..0x0050DB10",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q2 (UNKNOWN row); re-analysis/research/20260929-R-VIS-verify-M12-gap3.md"
  ],
  "unresolved": "sweep .text for vldr sN,[rX,#0x14] with rX a PreActionPose element (Ramp and Charger IsPreActionPoseValid, other callers)."
}
```

```json
{
  "id": "M13-002",
  "title": "FlipBlockAction's constants, their roles and IAction type",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "FlipBlockAction ctor 0x0055EC80: IAction type 0xF (0x0055ECA8/0x0055ECAA); +0x12C=150.0 (0x43160000), +0x130=20.0 (0x41A00000) at 0x0055ECF6/0x0055ECFA; +0x134=45.0 (0x42340000), +0x138=40.0 (0x42200000), +0x13C=-1 at 0x0055ECFE..0x0055ED0E; +0x140=1 at 0x0055ED10/0x0055ED12",
    "+0x12C is the drive speed: Init 0x0055EF18 loads it into r3 and passes it to DriveStraightAction 0x0055EF2C; the r3 argument is stored at DriveStraightAction+0x7C (0x005472A4)",
    "+0x130 is the drive-past distance: Init computes the robot-to-object distance (vsqrt.f32 0x0055EEFA), adds +0x130 (0x0055EF14/0x0055EF1C) and passes the sum as DriveStraightAction's distance argument 0x0055EF20 (stored at DriveStraightAction+0x78)",
    "+0x134 is the approach lift height: Init 0x0055EF3A loads it into r2 for MoveLiftToHeightAction(45.0, 5.0, 0) 0x0055EF4A (added before the drive)",
    "+0x138 is the lift trigger distance: CheckIfDone 0x0055F124 vldr s0,[r4,#0x138] / vcmpe / bpl 0x0055F130; when the object is closer than it and +0x13C is still -1 it queues MoveLiftToHeightAction(preset 2, speed 5.0) 0x0055F14E and stores that action's id in +0x13C (0x0055F15C) through ActionList::QueueAction position 5 (0x0055F16A)",
    "+0x13C is the queued lift action's id, -1 when none; the destructor cancels it on robot+0x250 ActionList (0x0055ED6C/0x0055ED70/0x0055ED7A)",
    "+0x140 is shouldCheckPreActionPose: Init reads it (0x0055EE10) into the PreActionPoseInput byte at sp+0x64 (0x0055EE1C) passed to IDockAction::GetPreActionPoses with ActionType 5 (0x0055EE14/0x0055EE5E); SetShouldCheckPreActionPose writes it (0x0055EDC2)"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): the 5 passed to MoveLiftToHeightAction(45, 5, 0) is a tolerance: the lift now goes out with the constructor defaults (speed 10, accel 20; 0x00548A6E..0x00548A78); the distance is computed in binary32 (0x0055EED6..0x0055EF1C, 0x0055F0E6..0x0055F10A). Still open: DriveAndFlipBlockAction.GetPossiblePoses (M12-035) still casts a double sum (0x0055E688 unread)."
}
```

```json
{
  "id": "M13-003",
  "title": "Obstacle expansion, per-heading C-space polygons, and the primitive collision test",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ImportBlockworldObstaclesIfNeeded 0x004FD4B8: paddings 7.0/6.0 (0x004FD4EE/0x004FD4F2) or 2.0/1.0 (0x004FD4EA/0x004FD4EC) selected by the function's own first bool argument (r4=r1 0x004FD4D2; cmp 0x004FD4E8; itt ne 0x004FD4F6); callers pass 0 (0x004FD2AC, 0x004FF92A) or 1 (StartPlanning 0x004FEC38)",
    "import log \"robot padding %f, obstacle padding %f , didBlocksChange %d\" (0x004FD51A/0x004FD546); penalty 0.1 (0x3DCCCCCD at 0x004FE0CE); AddObstacleWithExpansion 0x00855528 stores the caller's penalty at pair+0x44 (0x00855612) and calls ExpandCSpace 0x008550E8; ConvexPolygon::RadialExpand 0x004FDF9E",
    "ConvexPolygon::RadialExpand body 0x00841580: for each vertex v, v' = v + d*(v-c)/|v-c| with c the polygon centroid (ComputeCentroid 0x008415D8; per-vertex 0x008415F2..0x00841654), no bisector/cos and no clamp; a negative d only warns (0x00841590/0x00841598)",
    "IsInCollision(State) 0x008515BC converts the grid shorts to mm and tail-calls IsInCollision(State_c) 0x008515F8, which buckets theta as round(theta*env+0x10) mod env+8 (0x00851614..0x0085167A) and returns 1 only for a containing polygon with penalty >= 1000.0 (0x008516D0/0x008516DC); IsInSoftCollision 0x00851708 returns 1 for any containing polygon; GetCollisionPenalty 0x008517B0 returns the first containing polygon's penalty or 0.0 (0x008517C4/0x0085184E)",
    "the per-primitive test is Anki::Planning::SuccessorIterator::Next 0x0085110C: broad phase is the primitive's cached bbox (MotionPrimitive+0x1C..+0x28) against env+0x50 (0x0085123A..0x00851286); turning vs non-turning is decided by end_pose.theta vs MotionPrimitive+1 (0x008512CC/0x008512D0)",
    "non-turning primitive: every intermediate pose is tested (loop 0x0085134A..0x008513A6, stride 0x14) at (start + IntermediatePosition.x_mm, +y_mm) in the end_pose.theta bucket (0x008512E0/0x008512E4); turning primitive: the intermediate poses are walked last to first (0x00851414/0x0085149E) each in its own bucket at IntermediatePosition+0xC (0x00851420/0x00851428)",
    "soft hit (polygon penalty < 1000.0) adds base + penalty*IntermediatePosition+0x10 to the successor cost (non-turning 0x0085137E..0x00851398; turning 0x0085146E..0x00851490); penalty >= 1000.0 is a hard collision and rejects the primitive (0x0085138A/0x0085147A; threshold 1000.0 at 0x00851162); base is 0.0 forward and 1000.0 for a reverse primitive (0x008515B0/0x008515B4)",
    "successor g = soft-collision cost + parent g + MotionPrimitive+4 (0x0085150A/0x00851516/0x0085151A/0x0085151E/0x00851522); the 0.1 penalty means the shipped obstacles are all soft",
    "the soft-collision multiplier at IntermediatePosition+0x10 is defined in MotionPrimitive::Create 0x00853DD0 (0x00853E96..0x00853F4E): for each intermediate pose, dist is the Euclidean distance to the previous intermediate pose already in the vector (0x00853E9E..0x00853EDE) and dtheta is Radians(cur.theta_rads) - Radians(prev.theta_rads) rescaled to [-pi,pi) (0x00853EBE/0x00853EFA/0x00853F0E), giving inverseDist = 1/(env+0x78 * env+0x60 * |dtheta| + dist) (0x00853F1E..0x00853F46, i.e. 1/(|dtheta|/60*24 + dist)); the first intermediate pose has inverseDist 0 (0x00853E96/0x00853E9A)"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): the planner's primitive import, polygon and C-space expansion, collision and cost are binary32 (0x00841580..0x00841654, 0x0085110C..0x008515B4; penalty 0x3DCCCCCD). Still open: the obstacle source is a labelled STAND-IN (ImportBlockWorldObstacles reads BlockWorld and includes the charger; the engine reads the memory map, predicate 0x00502410 keeps content type 3 only); the Pose3d round trip and ComputeOriginPose of the robot quad (0x004FE042..0x004FE08E) are not transliterated; previous-plan reuse (PlanIsSafe) is not built."
}
```

```json
{
  "id": "M13-008",
  "title": "The mount sequence and the reverse onto the charger, with the retry result codes",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ConfigureTurnAndMountAction 0x0054E500: TurnInPlaceAction at atan2 of the vector to the marker (0x0054E52E/0x0054E536), SetMaxSpeed(0x3FDF66F3 = 1.745329 rad/s) at 0x0054E550/0x0054E55A and SetAccel(0x40A78D36 = 5.235988 rad/s^2) at 0x0054E55E/0x0054E568; then DriveStraightAction(-120 mm, 30 mm/s, false) whose vtable becomes BackupOntoChargerAction's (0x0054E5FC..0x0054E618)",
    "BackupOntoChargerAction::CheckIfDone 0x0054E7A8: on contacts (robot+0x338) SetPoseOnCharger and return 0 (0x0054E7B0..0x0054E7BE); pitch below -0.261799 rad (0xBE860A92 at 0x0054E7CC) returns 0x0400000A (0x0054E7DE adds r4,#4); fall-through returns 0x04000006 when DriveStraightAction::CheckIfDone returns 0 (0x0054E7E4..0x0054E7EC)",
    "MountChargerAction::CheckIfDone 0x0054E2D0 reaches the pi/2 comparison (0x3FC90FDB at 0x0054E374) only when the turn-and-mount sub-action FAILED (0x0054E31A skips 0 and 0x1000000); outside the window ConfigureDriveForRetryAction 0x0054E72C runs DriveStraightAction(120, 100) returning 0x04000006 (0x0054E3E8/0x0054E742/0x0054E748); an align failure ends the action (0x0054E2FA)"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): the mount sequence per the binary: the constants and results, the backup (BackupOntoChargerAction 5.0f timeout, 0x0054E97B), BadObject, the lift raise only when strictly below 45 (0x0054E58A..0x0054E5C6) with the 5 as a tolerance, the head tolerance 0x3D0EFA35, the whole-mount 30.0f nested around align/head/lift/turn/reverse (IAction::UpdateInternal 0x00540D90..0x00540DAA), VerifyResult per 0x00553500..0x0055359C, SelectDockAction (0xB for type 1, 0x005534CC..0x005534DE). Still open: SetPoseOnCharger (0x0054E7B8) and the robot+0x334 write (0x0054E11C), the sub-actions run as async polls rather than action-list ticks, TurnInPlace's body, the mount constructor's flag80/flag81 callers; HostDriveStraightTick is not the engine's DriveStraightAction (Init 0x005475D8, CheckIfDone 0x005478C0 unread: reported MISSING)."
}
```

```json
{
  "id": "M13-009",
  "title": "The charger's dimensions, its docked pose and its one pre-dock pose",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Charger::Charger 0x004E9B6C stores 96.0/80.0/31.0 at +0xF0..+0xF8 (0x004E9B98/0x004E9BB4/0x004E9BB0/0x004E9BB8) and adds one marker: id 2 (0x004E9C16), angle -pi/2 about Z (0xBFC90FDB at 0x004E9BBC) at (86,0,22) (0x004E9BD6/0x004E9BE2), size Point2 x=27.0 at sp+0x14 and y=20.0 at sp+0x18, the AddMarker pointer being sp+0x14 (0x004E9C28/0x004E9C30/0x004E9C36/0x004E9C38)",
    "Charger::GetRobotDockedPose 0x004EA1A0 = Pose3d(Radians(pi), Z_AXIS, (30,0,0)) on the charger pose (0x004EA1AC/0x004EA1C6/0x004EA202)",
    "Charger::GeneratePreActionPoses 0x004E9FB0 emits one pose for action types 0 and 1 only (0x004E9FD4/0x004E9FD6): Pose3d(Radians(p.angle + pi/2), Z_AXIS, (p.x, -p.y, -15.5)) parented to the marker (0x004E9FE4/0x004E9FF2/0x004EA012/0x004EA018/0x004EA01E); p is the file-static Pose2d at 0x01059148 initialised (Radians(0), 0.0, 250.0) at 0x004D6BC4/0x004D6BD6"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): the charger literals and pose construction (0x004E9B6C..0x004EA202) with bit-pattern constants and the GeneratePreActionPoses entry. Still open: Pose3d is double (the engine's quaternion round trip is unread); no live caller of the pre-dock generation (M12-034)."
}
```

```json
{
  "id": "M13-012",
  "title": "The mount raises the lift to 45 mm when it is below it, not when it is above",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ConfigureTurnAndMountAction reads Robot::GetLiftHeight (0x0054E58A) and branches past the lift move when the lift is at or above 45.0 (0x42340000 at 0x0054E592; bpl at 0x0054E59E); otherwise MoveLiftToHeightAction(45, 5, 0) (0x0054E5B2..0x0054E5C6)"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): the strict below-45 mount raise inside the engine's sequence with the 5 as a tolerance (see M13-008). Still open: see M13-008."
}
```

```json
{
  "id": "M13-013",
  "title": "DriveOffChargerContactsAction: a drive straight that retries while still on the charger",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "DriveOffChargerContactsAction ctor 0x00558228: DriveStraightAction(10 mm, 20 mm/s, false) (0x00558232/0x00558236/0x0055823E); then +0x44 = 7 (0x00558276/0x00558278), which is IActionRunner's RobotActionType (stored at +0x44 by the IActionRunner ctor 0x0053FDCC) and 7 = DRIVE_OFF_CHARGER_CONTACTS (RobotActionTypeFromString 0x0075A448 maps the string at 0x00C1604E to 7)",
    "in SDK mode only, the constructor clears the required-track lock: CozmoContext::IsInSdkMode 0x0055827C, cmp #1 0x00558280, then IActionRunner::SetTracksToLock(0) 0x00558286/0x00558288; SetTracksToLock 0x00540918 writes the argument to IActionRunner+0x54 only when the action state (+0x18) is 0x2000001 (not started), else warns; it is a local pre-run flag consumed by IActionRunner::Update 0x00540438 (MovementComponent::AreAnyTracksLocked), not a robot message",
    "Init 0x005582D0 copies robot+0x338 into action+0x8B (0x005582D2/0x005582D6) and returns 0 when not on contacts",
    "CheckIfDone 0x005582E4 retries while the drive is still running and fails 0x04000009 if still on the contacts (0x00558344)"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): DriveOffChargerContactsAction as native ctor/init/check on the tick model with the SDK-only SetTracksToLock(0) gate, robot+0x338 capture and 0x04000009 (0x00558228..0x00558344), 30.0f timeout. Still open: SmartRemoveDisableReactionsLock on a non-zero result; DriveStraightAction Init/CheckIfDone and the two-argument constructor defaults (speed 100/-80, accel 0x43480000, decel 0x43FA0000) unread; IsInSdkMode's source; no production caller (only the ManipTool)."
}
```

```json
{
  "id": "M13-014",
  "title": "Knock over a stack: BehaviorKnockOverCubes flow, DriveAndFlipBlockAction and IDriveToInteractWithObject",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorKnockOverCubes body 0x005C2EA0..0x005C3E2A; IsRunnableInternal 0x005C314C needs StackOfCubes::GetStackHeight() >= +0x124 (minimumStackHeight default 3); InitInternal 0x005C31A2 runs the reach unless +0xD9 (alwaysStreamline) or +0xD8 (the soft/hard switch flag) is set (0x005C31B4/0x005C31BA)",
    "TransitionToReachingForBlock 0x005C3254: TurnTowardsObjectAction(max pi), then DriveStraightAction(x-85, 60) when the block x+10 > 85.0, then TriggerLiftSafeAnimationAction(+0x150), then TransitionToKnockingOverStack regardless of result",
    "TransitionToKnockingOverStack 0x005C34A8: TurnTowardsObjectAction(max pi), DriveAndFlipBlockAction with maxTurn = pi/2 on the first attempt (0x005C36BC) and 0.0 once +0x140 > 0 (0x005C36C0); +0xD9 or +0xD8 also forces 0.0 (0x005C34EA..0x005C34F8); WaitAction(0.5); the callback 0x005C3DCE writes AIWhiteboard+0x70 on NoPreActionPoses (0x03000010), re-runs while +0x140 <= 1 on a Retry result and otherwise blind-flips, incrementing +0x140 either way",
    "success goes to TransitionToPlayingReaction 0x005C3908: it sets robot->[+0x34]->[+0x94]->[+0xC] = 1 (0x005C3946..0x005C394E), i.e. the BlockWorld's BlockConfigurationManager dirty flag (BlockWorld+0x94 is the manager; BlockConfigurationManager+0xC is read by its Update 0x00616D84) forcing all block configurations to recompute; the tipped-object set size at +0x14C selects the success trigger +0x15C with BehaviorObjectiveAchieved(0xD, true) and NeedActionCompleted(0) (0x005C3950..0x005C3964) or the failure trigger +0x160 (0x005C396E); when +0xD9 or +0xD8 is set the reaction animation is skipped (0x005C3972/0x005C3978)",
    "IDriveToInteractWithObject 0x0055B1F4 adds TWO actions when maxTurn > 0 (0x0055B37C..0x0055B392): a TurnTowardsLastFacePoseAction (vtable overwritten from TurnTowardsFaceAction at 0x0055B3C4..0x0055B3D8) and a TurnTowardsObjectAction (0x0055B42E/0x0055B43C), both with failure ignored; the trailing float 20.0 is not read by DriveAndFlipBlockAction ctor 0x0055E208",
    "+0xD9 is the JSON config key alwaysStreamline (IBehavior::ReadFromJson 0x005BC208/0x005BC216/0x005BC21E); +0xD8 is computed by IBehavior::Init 0x005BCCAA..0x005BCCC2 as 1 when the behaviour was entered by a soft spark switch and 0 for a hard switch or no current behaviour",
    "0x0055B3C0..0x0055B3CC, 0x005DE160..0x005DE16C, 0x005B7DB2..0x005B7DC2 the vtable overwrite; vtable 0x010204F4 slots 2, 4, 6, 9, 10",
    "0x0054B7B6 the ctor store of +0x193; 0x005C229C, 0x005DE170, 0x005F20B8, 0x005F6942 the behaviour stores",
    "0x0054B978..0x0054B9F4 SetSayNameAnimationTrigger; 0x0054C606..0x0054C61E and 0x0054C2D0..0x0054C2E6 the NeedsManager registrations",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q4; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md objection A8 (the class adds no logic of its own)",
    "re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q2, Q3, Q6; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md Q2/Q3, Q6 (PASS)",
    "0x0055B2C8..0x0055B438 the IDriveToInteractWithObject ctor compound construction and its AddAction flags (manager read)"
  ],
  "unresolved": "BUILT (FaceActions.cs, R-VIS M13 fix round): TurnTowardsLastFacePoseAction = TurnTowardsFaceAction with face id 0; Init (id 0 or the stack's -1 = last observed face, a valid id = FaceWorld.GetFace; no pose: +0x193 set logs NoFacePose and returns 0x0300000E, +0x193 clear sets state 3 and returns SUCCESS, never 0x0300000B); the RobotObservedFace handler (state <= 1, strictly closest 3-D distance^2, a valid id must match) and the verified id = SmartFaceID valid; CheckIfDone states 0-3 (WaitForImages of 10 frames, fine tune with min(|maxTurn|, 0.7853982), NO_FACE on the flag, the final SetTurnedTowardsFace); the four setters install regardless of sayName and log only when it is 0. STILL VISIBLE STUBS: MovementComponent::LockTracks/UnlockTracks (body unread, M13-021: noted in the action's Trace, nothing sent); NeedsManager registrations SeeFace 0x2E / SayName 0x29 (recorded in NeedsActionsRegistered, other layer); the emotion event (EmotionEvent); the SayText / TriggerLiftSafe children (the caller plays Reaction); the state-0 turn and the fine tune run the live executor (FaceTurns / TurnTowardsPose, M11-014/015), not the engine's PanAndTilt compound (M13-020/M13-022 are not connected); the head angle for the pan comes from TurnTowardsPose.HeadAngleToSee (LOCAL numerics), not the unread ComputeHeadAngleToSeePose (M13-021). CHOICES not in the inventory: SmartFaceID.Invalid (-1) is treated like id 0 (other callers pass it); RobotObservedFace is FaceWorld.FaceObserved; the wait also ends after 2 s (an earlier local guard); a cancelled wait returns Cancelled; a missing robot pose is the WithRespectTo failure. The +0xD8 soft-spark-switch source is the M8 framework; TurnTowardsFaceWrapperAction 0x0054C7DC is unread (M13-021). C# callers of FaceActionResult.NoFace (0x0300000B) reviewed: only FaceActions.cs (changed) and two tests (FaceTests, M13RVisBuildTests); behaviours that run the action now see Success where the flag-clear no-face case used to return NoFace: FaceBehaviors.cs:96 PlayAnimWithFaceBehavior.TurnedToFace = (r == Success) is therefore now true when there was no last face (read only at FaceTests.cs:425), and the ExplorerBehaviors callers (lines 294, 393, 445, RunAction with FaceActionResult.Abort as the failure default) ignore the result (the continuation is `_ =>`). Not the whole path. IDriveToInteractWithObject adds the drive-and-wait compound and both turn actions with ignoreFailure = true (manager read, in the authority text): a failed flip drive does not end DriveAndFlipBlockAction; build the compound flow, not an early return. R-VIS M12 fix round 2 (call-site edit in FlipBlockAction.cs, M12-035): DriveAndFlipBlockAction.RunAsync now follows IDriveToInteractWithObject's compound flags: the drive's failure is ignored (AddAction(inner, true, false), 0x0055B370) and the turns and the flip still run; the action's result is the flip's. Stand-in, MISSING (M8): the WaitForLambda body and ICompoundAction's ignoreFailure handling in UpdateInternal."
}
```

```json
{
  "id": "M13-016",
  "title": "AlignWithObjectAction's alignment-type table and pre-action type",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "AlignWithObjectAction ctor body 0x00553370: cmp r6,#3 / bhi 0x005533CC/0x005533E4; tbb [pc,r6] 0x005533E6 with table base 0x005533EA = 02 05 09 0c (TBB scales by 2): type 0 -> 0x005533EE vmov.f32 s16,#6.0; type 1 -> 0x005533F4 flag +0xBB=2; type 2 -> 0x005533FC vmov.f32 s16,#-15.0; type 3 -> 0x00553402 vadd.f32 s16,s0,s2 (argument + -27.0)",
    "clamp: 0x00553414 vldr s0,[pc,#0x68] -> 0x00553480 = 0xC1800005 = -16.000009536743164; vcmpe/it mi/vmovmi at 0x0055341C..0x00553426 set 0.0 when below it",
    "GetPreActionTypeFromAlignmentType 0x005532B8: table at 0x00553360 maps 0->1, 1->0, 2->1, 3->1; invalid type logs and returns 1 (0x005532CC/0x00553320); the result is stored at AlignWithObjectAction+0xFC (0x0055342A) and indexes Anki::Cozmo::PreActionPose::ActionType (passed as that type by DriveToAlignWithObjectAction 0x0055C3A0 to IDriveToInteractWithObject); the values 0..6 are native (Block::GeneratePreActionPoses 0x004E5808 cmp r5,#5 / tbh 0x004E595E), the names have no shipped table (UNKNOWN)",
    "action+0xBB is IDockAction's DockingMethod field: the IDockAction ctor leaves it 0 (0x005503AA), DriveToPickupObjectAction::SetDockingMethod writes it (0x0055C584/0x0055C5A2) and IDockAction::CheckIfDone passes it to DockingComponent::DockWithObject (0x00552288/0x005522AE); DockingMethodFromString 0x007C013C gives BLIND_DOCKING 0, TRACKER_DOCKING 1, HYBRID_DOCKING 2, EVEN_BLINDER_DOCKING 3, so alignment type 1 runs the dock with HYBRID_DOCKING and every other type with BLIND_DOCKING"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): AlignWithObjectAction's table and SelectDockAction, VerifyResult per 0x00553500..0x0055359C, f32 values. Still open: the live Verify inputs ([+0xCC]+4/+5, M12-034) are not built (reported MISSING); IDockAction::CheckIfDone -> DockingComponent::DockWithObject and its failure results are not owned."
}
```

```json
{
  "id": "M13-017",
  "title": "BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ctor 0x005C0980 stores 96.0 + json extraDistanceToDrive_mm at +0x11C (0x005C09D4/0x005C09E2/0x005C09E6); IsRunnableInternal 0x005C0B10 returns robot+0x34A (the on-contacts flag)",
    "InitInternal 0x005C0B18 takes the reaction lock, pushes driving animations when the animation state is 3 (0x005C0B4A) and transitions only when robot+0x355 == 0 (0x005C0B54); +0x355 is the robot's OffTreadsState (OnTreads = 0), written only by Robot::CheckAndUpdateTreadsState 0x00512088/0x0051208E and named by OffTreadsStateFromString 0x0078DF58 (OnTreads 0, InAir 1, OnBack 2, OnLeftSide 3, OnRightSide 4, OnFace 5, Falling 6)",
    "TransitionToDrivingForward 0x005C0BB8 drives +0x11C (0x005C0C02/0x005C0C08)",
    "UpdateInternal 0x005C0DA8: once off the contacts (robot+0x34A == 0) it records the time at robot->[+0x264]->[+0x18]+0x44 and returns 2 (0x005C0DF4..0x005C0E0A); while still on the contacts and robot+0x355 != 0 it calls StopActing(false,false) (0x005C0DB6/0x005C0DC4) and waits; on the contacts with +0x355 == 0 it transitions to driving forward (0x005C0E18)"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX, 2026-10-03): BehaviorDriveOffCharger per the binary: runnable on OnChargerPlatform (+0x34A, 0x005C0B10, 0x005C0DA8), redrives until the platform flag clears, no 5 s timeout, the whiteboard +0x44 stamp is float and written only by the behaviour (0x005C0E02..0x005C0E08; the local FreeplaySystem writer is removed), IsRunnableBase reads it in float (0x005BD826..0x005BD962); the Robot::Update platform step (0x00513CD8..0x00513E2A) clears the flag from the charger and robot quads (minAreaRect built from the validated float32 port). Still open: this stack never creates the located charger on SetOnCharger's rising edge, so live the step takes the no-charger branch and clears the flag as soon as the contacts drop (the engine holds it until the footprint leaves the charger quad: reported MISSING); PushDrivingAnimations and the WaitForOnTreads path; the footprint for Markerless/Ramp/HumanHead; the rotation matrix is cast from a double; the GetBoundingQuad catch-all (0x004E65C6) and hulls of fewer than three points (reported MISSING)."
}
```

```json
{
  "id": "M13-020",
  "title": "PanAndTiltAction, TurnTowardsPoseAction::CheckIfDone and WaitForImagesAction",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00549C48..0x00549D60 PanAndTiltAction::Init; 0x00549D72..0x00549D74 CheckIfDone (veneer 0x008CB51C -> PLT 0x004AA984 IActionRunner::Update)",
    "0x0054B010..0x0054B01C TurnTowardsPoseAction::CheckIfDone; 0x0054CA64..0x0054CBEC, 0x0054CC1C..0x0054CC4C, 0x0054DD88..0x0054DE38 WaitForImagesAction",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q4; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md (Q4 rows verified)",
    "re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q4; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md Q4 (PASS)"
  ],
  "unresolved": "BUILT (PanAndTiltActions.cs): PanAndTiltAction.Init (the relative head angle through Anki::operator+(float, Radians) = EngineRadians.Rescale, M12-033), its constructor defaults, TurnTowardsPoseCompound (ctors 0x00549F10/0x0054B344, InitPose = TurnTowardsPoseAction::Init 0x0054A8FC as far as PanAndTiltAction::Init, in full: BAD_POSE, the parentless re-parent, the pan test and +0x179, the head angle and clamp), CheckIfDone 0x0054B011, WaitForImagesAction. NOT LIVE (live_path is a frozen field and stays true): nothing outside the tests constructs PanAndTiltAction, TurnTowardsPoseCompound.Init, WaitForImagesAction, TurnInPlaceAction or MoveHeadToAngleAction; the live face turn calls only TurnTowardsPoseCompound.InitPose for its pan/head computation and then runs the earlier executor (FaceTurns / TurnTowardsPose, M11-014/015); the face wait counts VisionSystem.FramesProcessed, not RobotProcessedImage messages (M11-035 has no producer). NOT BUILT: the CompoundActionParallel (M8 framework) and the wiring of the M13-022 actions into PanAndTiltAction.Init (its Children are still specs; whether Init applies the tolerance through SetTolerance's 2-degree minimum is not in the inventory); the PanAndTilt setters 0x005498A0..0x00549B30 (the limit SetMaxPanSpeed warns above is not in the inventory). GetAbsoluteHeadAngleToLookAtPose 0x0054B428 and ComputeHeadAngleToSeePose are M13-021 (a missing seam throws NotSupportedException). Not the whole path."
}
```

```json
{
  "id": "M13-021",
  "title": "Unread MovementComponent and TrackLayerComponent bodies next to the turn and head actions",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "0x00549C60..0x00549D2C the two constructions; 0x0054BB8C, 0x0054BC6C, 0x0054BA84",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q4 and open questions",
    "re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q5, open questions; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md objections 2 and 6"
  ],
  "unresolved": "R-FIX2 (2026-10-04): GetAbsoluteHeadAngleToLookAtPose (0x0054B428) and Robot::ComputeHeadAngleToSeePose (0x00518344..0x00518620: the failure results, the 25-pass loop and its unassigned-angle quirk) are now built (a Unicorn run of the shipped instructions reproduces the fallback's expected bits; the converge cases' bits come from a Unicorn run of the real function, with the pose algebra double against the engine's binary32); TurnTowardsPoseAction::Init uses the fallback only on a non-zero result with the engine's warning. Still open: the pose algebra (neck composition, GetCameraPose's pre-multiply, GetInverse) is the stack's double Pose3d narrowed to f32 (reported MISSING); the vision model's head-cam z differs from the engine's -8.0 (M11-054); the no-parent branch (PoseHasParent = false) is test-only; the MovementComponent wire bodies, the eye-shift arguments and the remainder of TurnInPlaceAction::CheckIfDone remain unread. | earlier: read the listed bodies (MovementComponent wire bodies, the eye-shift arguments, the remainder of TurnInPlaceAction::CheckIfDone)."
}
```

```json
{
  "id": "M13-022",
  "title": "TurnInPlaceAction and MoveHeadToAngleAction",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005459D4..0x00546894 TurnInPlaceAction; 0x00547E40..0x005488B8 MoveHeadToAngleAction; 0x0054D3F8, 0x0054D624",
    "re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q5; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md Q5 (PASS)",
    "0x005469A8..0x005469BE, 0x0054644A..0x0054645E, 0x0054834C..0x005483CC, 0x00548440..0x00548454",
    "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q4; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q4"
  ],
  "unresolved": "BUILT as classes (PanAndTiltActions.cs): TurnInPlaceAction (ctor, SetMaxSpeed, SetAccel, SetTolerance, IsOffTreadsStateValid, Init with the result codes 0x0300000A / 0x03000000 / 0x03000016, IsBodyInPosition, CheckIfDone with 0x04000004 and the tail 0x0300000A, the motorActionAck handler) and MoveHeadToAngleAction (ctor clamps, IsHeadInPosition, Init incl. the +0xA0 write on failure, CheckIfDone, the ack handler), with Radians through EngineRadians (M12-033: rescale and the strict IsNear). NOT LIVE and not connected to PanAndTiltAction.Init. The MovementComponent wire bodies (IPanTiltRobot.TurnInPlace / MoveHeadToAngle have no implementation), the eye-shift arguments, GetCompletionUnion 0x005469A8, the MoveHeadToAngleAction Preset constructor and the IAction RNG are M13-021 (RECOVERABLE_GAP). Whether the +0xAC store at 0x0054620C is inside the +0xD8 branch is not in the inventory (made unconditionally; unobservable because +0xD9 is never set here). Not the whole path. Also build GetCompletionUnion (relocalizedCnt) and the Preset constructor and GetPresetHeadAngle."
}
```

```json
{
  "id": "M13-026",
  "title": "GetAbsoluteHeadAngleToLookAtPose and Robot::ComputeHeadAngleToSeePose",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0054B428..0x0054B55A, 0x00518344..0x00518780 (pools 0x00518744, 0x00518780)",
    "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q3; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q3"
  ],
  "unresolved": "build both exactly (float32, double atan2 for atan2f is float in the engine: use the float function), including the 25/26 iteration quirk and the result codes; Robot::GetCameraPose(float) and the Robot members at +0x2CC/+0x258 are M13-023 (d)."
}
```

```json
{
  "id": "M13-027",
  "title": "TurnTowardsFaceWrapperAction",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0054C7DC..0x0054C8FC",
    "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q5; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q5"
  ],
  "unresolved": "build the wrapper; read its callers to confirm the flag names (behaviour layer, M8)."
}
```

```json
{
  "id": "M13-028",
  "title": "FlipBlockAction::Init and DriveAndFlipBlockAction's constructor guards",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0055EED6..0x0055EEFA, 0x0055EF3A..0x0055EF70, 0x0055ECE0, 0x0055EEC6, 0x0055EF7E, 0x0055B258..0x0055B264, 0x0055B38C",
    "re-analysis/research/20260929-R-VIS-verify-M12-fix2.md objections 2, 3, 9",
    "0x0055F074..0x0055F17A FlipBlockAction::CheckIfDone; 0x0055EF3A..0x0055EF4A; 0x00548A54..0x00548A78 (fix-round-3 verifier)",
    "0x0055F074..0x0055F208 CheckIfDone; 0x0055ED54..0x0055EDA0 destructor (round-4 verifier, .scratch/m12v5_cid.txt)"
  ],
  "unresolved": "Codex review of B-CORE2 (re-analysis/research/20260930-B-CORE2-review.md), checked by the manager's Opus verifier where noted: DEFECT. The 5 s stand-in removal holds; the destructor cancels the separately queued carry lift (0x0055ED6C..0x0055ED7A) and removes the reaction lock (0x0055ED88), which FlipBlockAction.cs:188-193 leaves unmodelled; the lift-speed and poll stand-ins remain. Before: BUILT (call-site file FlipBlockAction.cs), fix rounds 3, 4 and 5. Init: the drive distance is the 3-D norm of the object pose with respect to the robot pose plus +0x130 (0x0055EED6..0x0055EEFA); MoveLiftToHeightAction then DriveStraightAction in an embedded sequential compound (the drive starts only after the lift move completes); the initial lift move keeps MotionOutcome.EngineResult (0x04000004 / 0x03000016) as its failure result. CheckIfDone 0x0055F074 (FlipBlockAction.CheckIfDoneTick): every tick the object is looked up with GetLocatedObjectByIdHelper(id, -1) (0x0055F090; the located lookup only, no forgotten objects and no Init-time fallback) before the RUNNING test (0x0055F094); compound RUNNING and the object not located: warn and return 0x03000004 BadObject (0x0055F1C4) and the embedded compound is stopped by the stack's STAND-IN for its destruction in the destructor (0x0055ED68 PrepForCompletion on this+0x80, then ~ICompoundAction 0x0055ED8E; the engine does NOT cancel anything inside CheckIfDone, 0x0055F18C..0x0055F1CA only warns and returns): the compound checks the cancel token as soon as the initial lift wait returns and before it starts the DriveStraightAction, so no path is sent once the flip has ended (the lift wait itself cannot be interrupted); compound not RUNNING with no object (0x0055F17A): warn and return the compound's result without MarkObjectUnknown; not RUNNING with the object: MarkObjectUnknown(obj, true) (0x0055F186) and return the compound's result. Only a RUNNING tick with the object pose wrt the robot pose at a 3-D norm below +0x138 (40, strict, 0x0055F0E0..0x0055F130) and [this+0x13C] == -1 queues the carry-height MoveLiftToHeightAction(preset 2, tolerance 5.0, 0) on the ROBOT'S ActionList (0x0055F160 ldr r0,[r0,#0x250]; 0x0055F164 position 5; 0x0055F16A QueueAction), stores its id at +0x13C and returns RUNNING: the queued lift is NOT part of the embedded compound and the flip NEVER waits on it (the earlier 'the queued lift belongs to the compound' text and the await / LiftFailureResult on it were wrong and are removed); the flip's result is the compound's result only. The destructor (0x0055ED54) cancels the queued lift by id if +0x13C != -1 (ActionList::Cancel(unsigned), 0x0055ED6C..0x0055ED7A) and calls BehaviorManager::RemoveDisableReactionsLock (0x0055ED88). TRACK LOCK (2026-09-30, built, awaiting strong verification): the queued carry lift's byte +0x56 = 1 (0x0055F152..0x0055F154) is the action's suppress-track-locking flag: IActionRunner::Update 0x00540370 loads +0x56 at 0x00540428 and, when it is non-zero, branches (0x00540434 bne.w 0x540592) over AreAnyTracksLocked (0x00540440, the 0x03000019 failure at 0x00540572..0x0054057C) and LockTracks (0x0054058E), and the action's end skips UnlockTracks (0x005408EC ldrb +0x56; cbnz). So the carry lift is sent even while the compound's approach lift move holds the lift track, takes no lock and sends no Disable/EnableAnimTracks. Motion.SetLiftHeightAsync(suppressTrackLocking) models it and FlipBlockAction passes true (the B-CORE batch 3 track lock had made a carry lift queued during the approach fail with 0x03000019 and send nothing, which the engine does not do; FlipBlockAction.QueuedActionField0x56Unmodelled is removed). Tests: M12RVisBuildTests.M13_028_TheCarryLiftRunsWhileTheApproachLiftHoldsTheTrackAndTakesNoLock, M13_028_TheCarryLiftIsQueuedOnlyWithinFortyMillimetresInThreeDimensions, M13_028_TheFlipNeverWaitsOnTheQueuedCarryLift (the rig holds the lift and path: Rig.HoldLift / HoldPath). NOT MODELLED (visible): DisableReactionsWithLock (0x0055EEC6) and IActionRunner::Update (0x0055EF7E) at Init and RemoveDisableReactionsLock in the destructor (M8; FlipBlockAction.M8CallsNotModelled counts 3 per run, the Update being replaced by awaiting the compound); MISSING (a limit of this stack, NOT a source gap): the destructor's ActionList::Cancel of an unfinished queued lift, which the engine does promptly at flip end (the lift is typically still moving, rising 45 -> 92 mm after a trigger only 40 mm out), whereas here it runs on toward 92 mm and, if still not in position when its 30 s engine-clock IAction timeout fires, that timeout's stop-the-lift-if-moving (Motion.cs, M4-015/M4-016) could stop a LATER action's lift; an existing Motion stop/cancel API for a pending wait would be an option but no record supports it, so it is not built: the stack has no handle to cancel a lift move that CozmoMotion.SetLiftHeightAsync is waiting on (it ends by its own completion or its engine-clock IAction timeout, which stops a moving lift, M4-015/M4-016), so the cancel is not done and FlipBlockAction.QueuedLiftCancelsNotModelled counts it. MoveLiftToHeightAction(robot, height, tolerance, variability) takes r3 = 0x40A00000 = 5.0 = the TOLERANCE (0x0055EF3A..0x0055EF4A, 0x0055F148); M13-002's frozen text 'speed 5.0' misread it as a speed (correction recorded here); CozmoMotion.SetLiftHeightAsync already uses the game tolerance 5.0 (GameLiftToleranceMm, M4-016) and has no tolerance argument. MISSING: the ROLES of MoveLiftToHeightAction's +0x8C = 10.0 and +0x90 = 20.0 (ctor 0x0054899C; M10-008) are not confirmed by any record, so the 5 rad/s speed the stack always sent (FlipBlockAction.LiftSpeedRadPerSec) stays as a visible UNSOURCED STAND-IN. B-CORE2 batch 4 (2026-09-30): the 5 s LiftWaitStandIn is REMOVED. The initial 45 mm MoveLiftToHeightAction now uses the engine's default 30.0 s IAction timeout on the engine clock (M4-016; 0x0052B0C2), and when it fires the action fails with 0x03000018 (0x00540E80), so the embedded compound ends with that code and the flip returns it for a timeout (FlipBlockAction.LiftFailureResult; the timeout is no longer reported as ActionResult.Timeout). The carry lift uses the motion component's own 30 s default. UNSOURCED STAND-IN: the 10 ms poll (the engine's tick period is not in the inventory). DriveAndFlipBlockAction adds no drive/wait/turn when the robot carries the object id (warn; the flip then answers 0x03000004) and tests maxTurn with Radians::operator> (1e-5, MaxTurnIsPositive). Live-behaviour changes of the flip: the drive is 1-2 mm longer for a cube at z 22; it starts after the 45 mm lift move; the carry lift is queued only by a RUNNING tick within 40 mm in 3-D, runs with the track lock suppressed (+0x56, below) and is never waited on (a successful drive is no longer turned into a lift failure or a 5 s stall; a carry lift that never completes no longer changes the result); a cube that is no longer located ends a running flip with 0x03000004 (was: the flip kept driving on the Init-time pose); the object is marked Unknown when the compound is done (was: at the raise)."
}
```

```json
{
  "id": "M4-012",
  "title": "StartMotorCalibration is never automatic; byte0 head, byte1 lift; MotorCalibration handling",
  "status": "EXACT_SOURCE",
  "evidence": [
    "MA18 senders: CalibrateMotors (from CalibrateMotorAction::Init) and 0x005CFAC2 only",
    "MA19 CheckIfDone 0x00547D38..0x00547DC2",
    "MA20 HandleMotorCalibration 0x00536BAE..0x00536C0E"
  ],
  "unresolved": " Test gap (Codex re-audit re-analysis/research/20261001-reaudit-M3-M4.md, 2026-10-02; the code holds): the record names no test; one should enter through the public/game calibration action and route MotorCalibration, including the carrying side effect."
}
```

```json
{
  "id": "M4-014",
  "title": "Direct-drive track locks: DisableAnimTracks / EnableAnimTracks around DriveWheels, MoveHead and MoveLift",
  "status": "EXACT_SOURCE",
  "evidence": [
    "MA1 DriveWheels 0x0063ED82..0x0063EE9A, MoveHead 0x0063F4F6..0x0063F5D2; MA1-lift MoveLift 0x0063F73A..0x0063F92A",
    "MA2 DirectDriveCheckSpeedAndLockTracks 0x0063EFB0..0x0063F0B8 (1e-5 at 0x0063F0F8)",
    "MA3 LockTracks 0x006400D0..0x00640188 (0x9D); UnlockTracks 0x0063FE92..0x0063FFB4 (0x9E)"
  ],
  "unresolved": null
}
```

```json
{
  "id": "M4-015",
  "title": "StopHead / StopLift / StopBody: unlock preamble, then MoveHead{0}, MoveLift{0}, DriveWheels{0,0,0,0}",
  "status": "EXACT_SOURCE",
  "evidence": [
    "MA4 0x00640A08..0x00640AF6, 0x00640B3C..0x00640C2A, 0x00640C70..0x00640E4E",
    "MA7 ~IActionRunner stops a moving track (0x0054112E..0x00541192)"
  ],
  "unresolved": null
}
```

```json
{
  "id": "M4-016",
  "title": "Head and lift move semantics: no send when in position, ack matched by id, completion in position and stopped, tolerances and error codes",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "MA9, MA13, MA15..MA17, C1, C6, C10.1 as before",
    "C11.3 the lift CheckIfDone body 0x005493F6..0x00549508 contains no eye-shift removal; the head +0xA8 has only three writers, all zero (0x00547F3C, 0x005484B0, 0x00548724), so H4..H6 never execute"
  ],
  "unresolved": "Codex review of B-CORE2 (re-analysis/research/20260930-B-CORE2-review.md), checked by the manager's Opus verifier where noted: DEFECT, confirmed by the Opus verifier. +0x74 starts negative and is set from the tick clock at the first UpdateInternal (0x00540D52..0x00540D64); Motion.cs:828 stamps it at RunAsync. Codex's \"second precondition-time gate\" (0x00540DAC..0x00540DC0) cannot fire for these actions: slots 0x24/0x28 return 0.0 (0x0052B0BA/0x0052B0BE), slot 0x2C returns 30.0f (0x0052B0C2). Before: built, awaiting strong verification: B-CORE2 batch 4 (head/lift/action timeout). The timeout is now tested on the engine clock (CozmoEngine.Timer.Seconds) from the action's Init/first UpdateInternal (IAction::UpdateInternal 0x00540D4A..0x00540D64) before CheckIfDone, run by Robot::Update's ActionList step (CozmoMotion.UpdateActions, hook 0x00540370/CD12), and fails with EngineResult 0x03000018 (0x00540E80); the wall-clock Task.Delay is gone. Strict IsNear (0x0084CC0A) and the 30.0 s default hold. Tests: M4ControlTests.M4_016_MA15_NothingIsSentWhenAlreadyInPosition, M4ControlTests.M4_016_MA16_MA17_CompletionIsTheAckThenInPositionAndStopped, M4ControlTests.M4_016_MA17_StoppingOutOfPositionIsStoppedMakingProgress, M4ControlTests.M4_016_C1_TheHeadInPositionIsLatched, M4ControlTests.M4_016_R1_PassingTheTargetBeforeTheAckDoesNotLatch, M4ControlTests.M4_016_C6_TheLiftCompletion, M4ControlTests.M4_016_MA17_TheDefaultActionTimeoutIs30Seconds, ControlTests.AnUnacknowledgedActionTimesOutRatherThanReportingSuccess."
}
```

```json
{
  "id": "M4-017",
  "title": "Backpack lights: priority, Off resent every tick while no source, charging state machine, shared locator, wire conversion, headlight",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "LB1..LB6, LB4a..LB4i as before",
    "E2 0x0063234A..0x00632364 SetHeadlight calls VisionComponent::EnableMode(14, enable) on robot+0x258",
    "E3 VisionMode 14 = LimitedExposure (name table 0x01034230, entry 14 -> 0x00C1DC64)",
    "E4 0x006527AC..0x006527B6 EnableMode tail-calls VisionSystem::SetNextMode (PLT 0x4BA35C)",
    "E5 0x006B2282..0x006B2298 SetNextMode queues (mode,bool) on the deque at VisionSystem+0xB0",
    "E6 0x006B4FB6..0x006B4FE4 VisionSystem::Update drains the deque",
    "E7 0x006B1A76..0x006B1CC6 EnableMode(14) sets/clears mask bit 14 at VisionSystem+0xAC",
    "E8 0x00632368..0x00632380 EngineToRobot(SetHeadlight) then SendMessage reliable, not hot"
  ],
  "unresolved": "reproduce the EnableMode(14) call before the send: it queues LimitedExposure, applied at the next VisionSystem::Update (M11). No reader of mask bit 14 was found (E9); M11 owns that."
}
```

```json
{
  "id": "M4-028",
  "title": "Go-to-sleep lift child construction and motor effects",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "0x0052CF8E..0x0052CFB0; MoveLiftToHeightAction preset0, tolerance f32 0x40A00000; exact child initialization, locking, stop and completion must be verified in the control layer.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1â€“T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt"
  ],
  "unresolved": "MISSING: MoveLiftToHeightAction preset0, tolerance f32 0x40A00000; exact child initialization, locking, stop and completion must be verified in the control layer. Bind the native owner to its actual higher-layer production component and recover/check the complete recipient path before implementation or settlement."
}
```

```json
{
  "id": "M12-040",
  "title": "Carrying/docking lifetime after preceding control abort",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "0x0063BE10..0x0063BE5C; 0x005113FA..0x00511414; Dock Abort precedes carrying/docking raw owner deletes; do not add another destructor/send at the raw-delete sites. Remaining owned state/subscription effects UNKNOWN.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1â€“T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt"
  ],
  "unresolved": "MISSING: Dock Abort precedes carrying/docking raw owner deletes; do not add another destructor/send at the raw-delete sites. Remaining owned state/subscription effects UNKNOWN. Bind the native owner to its actual higher-layer production component and recover/check the complete recipient path before implementation or settlement."
}
```

## Build rows

| Step | Instructions (inclusive unless noted) | Behaviour / parameters | Gates and order | Result / boundary |
|---|---|---|---|---|
| P1 | 0x0055EDC8..0x0055EFDA | Flip clears embedded sequential +80; looks up and casts object; obtains type-5 pre-action poses. Constructs drive from binary32 3-D norm plus +130, speed +12C, animation flag 1; lift from +134, tolerance `40A00000`, variation `00000000`. | Allocates drive first, but inserts lift first (55EF58), drive second (55EF70), both AddAction(false,false). Calls embedded Update immediately and discards that result before returning success. | Lookup/cast failure `03000004`; pre-pose result propagated. Pose transform return at 55EEAE is ignored. Geometry / pre-pose implementation M11/M12 boundary. |
| P2 | 0x0054E1C4..0x0054E2AE | Charger alignment sequential +84: AlignWithObject(object, `42F00000`, alignment 3, parent +81); SetSpeed `41F00000`; then head target 0, tolerance `3D0EFA35`, variation 0. | Replace/delete previous compound; +56=1; align before head; both ignore-failure=false and parallel=false. | Child result governed by sequential runner; no standalone robot queue for these children. |
| P3 | 0x0054E458..0x0054E676 | Charger mounting sequential +88: find type-13 charger; construct offset pose x=`41F00000`, y=z=0 in charger frame, precompose and set world parent; atan2 vector yields absolute turn; SetMaxSpeed `3FDF66F3`, SetAccel `40A78D36`; optional lift(45,5,0); backup(-120,30,false), saved parent +80 at child +8B. | Turn first; lift only current lift strictly below `42340000`; backup last; +56=1, ordinary failure propagation. | Missing charger/wrong type `03000004`. Geometry calls are named boundaries, not invented substitutes. |
| P4 | 0x0054E72C..0x0054E770 | Retry child DriveStraight(distance `42F00000`, speed `42C80000`, play animations=false). | Replace/delete +8C; +56=1. | Uses ordinary DriveStraight Init/Check. |
| P5 | 0x00551F9C..0x0055216E | Dock turn/verify sequential +98; +56=1. If +C0, copy object pose, raise z by parent-frame object z dimension; tolerance XYZ half respective dimensions; construct NoObjectAtPose, ignore target ID, +56=1, add(false,false), SetDeleteActionOnCompletion(false). If +C8, construct TurnTowardsObject(object, selected marker or zero if +F7, max angle, visually verify=true, track=false), +56=1, add(false,false). | No-object verification precedes turn. Geometry belongs to observable-object/pose records. | Compound owns children; retained completed child is intentional. Maximum-angle input is `00000000` (552128). |
| D1 | 0x005470F0..0x00547208; 0x00547278..0x00547308 | DriveStraight IAction type 8, body mask 4; +78 distance; +80 accel `43480000`, +84 decel `43FA0000`; +88 fixed-speed=false, +89 path-seen=false, +8A animations=true. Explicit-speed overload sets +88=1, +7C caller speed, +8A caller flag. | Caller speed strictly below `B727C5AC` is negated; distance strictly below same threshold negates effective speed. Near-zero negative speed/distance do not take those branches. | No construction failure result; signed speed feeds AppendLine. Default speed: negative distance `C2A00000`, otherwise `42C80000` (literals 547268/54726C). Explicit-speed compounds override it. |
| D2 | 0x0054759E..0x005475D4 | Motion profile setter returns false for explicit-speed +88. Otherwise +7C=profile+0 for nonnegative distance, or negative profile+24 for negative distance; accel/decel from +4/+8. | Binary32 comparisons and sign change; preserve manual-speed gate. | true only when applied. |
| D3 | 0x005475D8..0x00547626 | Initialize DrivingAnimationHandler first with runner +54, tag +60, +56, final false. Absolute distance strictly below `3727C5AC` sets path-seen +89=1 and returns success. | No path construction for near-zero distance; still initializes animation handler. | `00000000`. |
| D4 | 0x00547628..0x005476D2 | Obtain robot yaw and drive-center XY; endpoint is `(x + distance*cosf(yaw), y + distance*sinf(yaw))`, separate binary32 multiply then add. AppendLine(startXY,endXY,speed,accel,decel); ExecuteCustomPath(path,false). | AppendLine false skips execute; success clears +89 before execute. | AppendLine false `03000013`; nonzero execute `03000016`; otherwise 0. Temporary Path destroyed before returning (547722..730). |
| D5 | 0x005478C6..0x00547938 | Animation handler state 3 returns RUNNING before path checks. LastPathFailed then returns `04000002`. If +89 clear, store HasPathToFollow into it; exactly 1 may start animation when +8A set. | Waiting for first path start returns RUNNING while stored value zero. | `01000000` or path failure. |
| D6 | 0x00547974..0x005479EC | Once path seen, IsActive keeps RUNNING. Inactive path succeeds unless animations enabled and PlayEndAnim returns nonzero. | Start animation precedes active-path test; end animation can defer success. | 0 or RUNNING. Animation execution is M5 boundary. |
| L1 | 0x0054899C..0x00548AB0; 0x00548B84..0x00548BA2 | Lift IAction type `13h`, lift mask 2. Arguments are height +78, **tolerance +7C**, variation +80. +88 duration=0; +8C speed=`41200000`; +90 acceleration=`41A00000`; +95..98 flags clear; subscribe robot tag C4h. Preset constructor delegates with zero variation. | Subscription installed at construction; no speed argument in height/tolerance/variation overload. | Constructor API is not height/speed/variation. Current M4-003 game-message speed parameters remain a separate path. |
| L2 | 0x00549044..0x00549140 | Reset has-moved +98 and sent/ack +95/+96. Nonnegative out-of-range requested height clips to `42000000`..`42B80000`. Negative requested height chooses nearer preset 0 or 2 by strict absolute-distance compare; tie picks preset 2. | Clip requested +78 before target derivation. Binary32 arithmetic and branch predicates as companion, including unordered flags. | No failure for clipping or negative request. |
| L3 | 0x00549142..0x005491D2 | For nonnegative request copy into target +84. If variation +80 > 0, RandDblInRange(-variation,+variation), widening binary32 limits; add target in binary64, narrow once to binary32. Clip target to same bounds. | RNG before angular tolerance calculation; negative request skips variation. | Preserve RNG call even if final clip cancels its numerical effect. |
| L4 | 0x005491D6..0x005492EE | Let t=target +84, h=tolerance +7C; angle a=heightToAngle(t). Lower gap a-heightToAngle(t-h) considered only t-h>32; otherwise FLT_MAX=`7F7FFFFF`. Upper gap heightToAngle(t+h)-a replaces it only t+h<92 and gap smaller. If minimum gap <`3CD67750`, compute heights at a±`3CD67750`; tolerance becomes max(requested +78 − lower height, upper height − requested +78). | **Uses requested +78 for widening, not varied target +84**. Lower/upper conversions are called even where their result later excluded. Strict bounds and compare. All additions/subtractions binary32; converter implementation is M4 geometry boundary. | Clip is warning, not action failure. |
| L5 | 0x00548FEA..0x00549038; 0x005492F2..0x00549338 | Position predicate: abs(target-current lift)<tolerance **and** MC+0B moving=false. Init latches +97; sends only if false: MoveLiftToHeight(target,+8C,+90,+88,&+94 ID). | On successful send +95=1; on failed send no sent latch. | Nonzero send `03000016`; otherwise 0. M4 owns wire serialization/track effects. |
| L6 | 0x0054D748..0x0054D7B2 | C4 callback: only when sent +95; Get_motorActionAck; matching u8 ID +94 sets ack +96=1 after logging. | Unsent and mismatched IDs ignored. | No completion just from ack. |
| L7 | 0x005493F0..0x0054956C | Sent and unacked returns RUNNING before position latch. Otherwise latch +97 from predicate only until true; MC+0B sets has-moved +98. Latched+stopped succeeds, latched+moving RUNNING; not latched+moving RUNNING; not latched+stopped+has-moved `04000004`; never-moved remains RUNNING. | Counters are logging cadence (11 calls), not acknowledgement timeout. Generic IAction timeout precedes this Check. | 0, `01000000`, `04000004`; generic timeout `03000018` owned runner. |
| H1 | 0x00547EAC..0x00547F40; 0x00547F44..0x005480AC | Head IAction type `12h`, head mask 1; target +78, tolerance +80, variation +88. Defaults +90 speed=`41700000`, +94 accel=`41A00000`, +98 duration=0. Clip target to `BEDF66F3`..`3F46D3F2`; tolerance minimum `3D0EFA35`. | These constructor defaults differ from the app's explicit 10/20 game-message arguments; do not silently merge paths. | Warnings/clips, no failure result. |
| H2 | 0x005480B0..0x0054818A | If variation>0, draw binary64 in widened ±variation, narrow to binary32 then Radians += target, clip again to target bounds. | Occurs at construction, unlike lift variation in Init. | No draw for zero variation. |
| H3 | 0x005484F4..0x005485C8 | Position predicate Radians.IsNear(target, robot head angle, tolerance+`3727C5AC`). Init resets sent/ack/has-moved; latches in-position +AC. If out of position MoveHeadToAngle(target,speed,accel,duration,&+A9 ID); successful send +AA=1. | Optional eye-half-distance computation after send when +9C and !+9D. | Nonzero send `03000016`; otherwise 0. |
| H4 | 0x0054D624..0x0054D68E; 0x005485CC..0x005488C0 | Ack requires sent +AA and matching +A9 then sets +AB. Check sent/unacked RUNNING before latch; +AC latches position, MC+0A moving sets +AD; completion matrix matches L7 with head flags. Eye-removal gate !+9D && +A8; remove when latched or within half-angle, then zero +A8. | Native constructor +A8=0; a gate in Check does not prove an eye-shift producer exists. Current M4-016 explicitly records the zero writers. | 0, RUNNING, stopped-making-progress `04000004`. No new head-eye producer claimed. |
| T1 | 0x005459D4..0x00545ADC | Turn IAction type `28h`, body mask4. +88 request, +C0 absolute flag; defaults +78 accel=`40A78D36`, +7C max speed=`41200000`, +80 maximum revolutions=`41C80000`, +B0 tolerance=`3D0EFA35`, variation +B8=0; eye flag +D8=1; subscribe C4h. | +C4/+C8 command speed/accel copies; flags/counters initialized. | Ctor has no action result. |
| T2 | 0x00545EC4..0x00545F1A; 0x00545FA0..0x0054602C | Off-treads valid iff robot+355 ==0. Init invalid returns `0300000A`; valid saves frame ID +D0, resets relocalization +D4 and gets yaw. Nonzero variation draws widened ±variation, narrows once. | Off-treads gate before RNG and command. | Classifier writer M10 boundary. |
| T3 | 0x00546030..0x0054613E | Absolute: target request+variation via Radians; expected distance normalized target-current. Relative: reject abs(request)>maxRevolutions*`40C90FDB`; subtract previously travelled +A8 from request, target current+remaining+variation, expected remaining+variation; copy request sign into command speed. Reset travelled, previous angle, position/sent/ack/has-moved. | Reject before mutating relative request. Position predicate suppresses command. | Oversize relative request `03000000`; otherwise 0 before send. |
| T4 | 0x00546142..0x005461BE | TurnInPlace(target,+C4,+C8,tolerance,positive floor(expected/`40490FDB`) narrowed to u16 or 0, absolute bool,&+DA ID). | Relative floor is converted to unsigned command count after clamping nonpositive to zero; absolute count 0. | Nonzero send `03000016`; zero sets +DB sent. |
| T5 | 0x005461C0..0x00546298 | Optional eye dart after successful send: RemoveKeepFaceAlive(21h); +AC=abs(expected)*`3F000000`; tanf(clamp(expected,`BFC6D3F2`,`3FC6D3F2`))*`418C28F6`*`3F9D89D9`; AddOrUpdateEyeShift("TurnInPlaceEyeDart",&+D9). Raw trailing args 0,84h,`42800000`,`43000000`,`3F8CCCCD`,`3F59999A`,`3DCCCCCD`. | Separate binary32 multiplications. Eye effects execution M5 boundary; do not replace their return token. | Successful Init remains 0. |
| T6 | 0x00546374..0x00546422 | Get yaw to output. Remaining abs(expected−travelled) must <`40490FDB`. IsNear(yaw,target,tolerance+`3727C5AC`); after relocalization, near **or** remaining<abs(tolerance). In either case MC+0C must be stopped. | Strict comparisons; a relocalization does not waive stop gate or the <π initial gate. | Boolean predicate only. |
| T7 | 0x0054D3F8..0x0054D462; 0x0054642C..0x0054689C | Ack sets +DC only if sent +DB and matching +DA. Sent/unacked RUNNING. Frame-ID change increments +D4, updates frame ID and previous yaw. Latch +84 via position; add normalized current−previous yaw into travelled and replace previous yaw. Eye token removed if in-position or abs(travelled)>half expected. | Frame-change bookkeeping before latch/delta; preserve normalized angle operations. | Does not infer physical progress from ack. |
| T8 | 0x0054642C..0x0054689C | MC+0C moving sets +85. In position succeeds; out-of-position stopped after having moved `04000004`; other cases RUNNING. At end off-treads invalid replaces candidate result with `0300000A`. | Off-treads recheck occurs after positional candidate; sent-unacked early-return path bypasses it. | 0 / RUNNING / `04000004` / `0300000A`. |
| G1 | 0x0055A238..0x0055A326; 0x0055A3D4..0x0055A402; 0x0055A414..0x0055A55A | DriveToPose ctor type0Ch; goal unset, vector empty, selected-index shared u8=0; tolerance XYZ `41200000`, angle `3E32B8C2`, planning timeout `40800000`, additional float +AC=`3F800000`, deadline +B0=`BF800000`; +79 head-lowering flag, +A4 planning option. Single-pose constructor overwrites timeout/extra float then SetGoal. | SetGoal only while state +18=`02000001`; copies tolerance then goal vector, sets +78=1. Refusal logs and leaves goal unchanged. | No construction/SetGoal action result. First bool sets head-lowering +79; **second bool** sets planning option +A4 and clears body track mask from4 to0 when nonzero (55A25A..55A26E). |
| G2 | 0x0055A86C..0x0055A976 | DrivingAnimationHandler initialized before checking goal. Reset deadline=-1. Missing goal `0300000F`; transform each stored pose in vector order in-place to robot world origin. | Stop at first failed transform; successful earlier goals remain transformed. | Transform failure `03000005`. |
| G3 | 0x0055A976..0x0055AA68 | Zero shared selected index, StartDrivingToPose(goals,sharedIndex,+A4); failure `03000013`. On success +79 optionally MoveHeadToAngle with target `BE860A92`; failed head send `03000016`. | Planner start precedes head send; no implicit planner rollback on head-send failure in this body. | M13 planner/PathComponent and M4 send implementation are named boundaries. |
| G4 | 0x0055AB4C..0x0055ABDA; 0x0055ABDC..0x0055AD66 | Animation handler state3 first returns RUNNING. Path state0: reset deadline and `03000013`; state1: if deadline negative set binary32 now+timeout and RUNNING; otherwise now<deadline RUNNING, else Abort, deadline=-1, `03000013`. | Native TBH data at 55AB84..8D, not instructions. State2 and >4 remain RUNNING; no arbitrary host timeout. | Timer value and deadline binary32; equality times out. |
| G5 | 0x0055AC06..0x0055ACA0 | State3 plays start animation, resets planning deadline=-1, increments +C4; logs on old counter divisible by10. | Animation call every state3 Check, not once-only action flag. | RUNNING. |
| G6 | 0x0055ACA2..0x0055ADCA | State4 resets deadline, XY thresholds from +90/+94, Z threshold from Robot::GetHeight. +C0 optionally replaces XY via ComputePreActionPoseDistThreshold(selected goal,+B4,+9C). IsSameAs(robot pose,selected goal,XYZ thresholds,angle +9C,outputs). | Selected index is shared planner output, not always first goal. Geometry predicate body belongs M11/M12; preserve interface and thresholds. | Predicate true candidate success. |
| G7 | 0x0055ADCA..0x0055B0D6 | Arrival true ->0; arrival false and last sent/received u16 path IDs equal ->`04000002`; false and IDs differ ->`03000008`. Final tail calls PlayEndAnim unless RUNNING or `03000013`; nonzero animation return defers candidate to RUNNING. | **End animation also runs for arrival failures `04000002` and `03000008`**. Reevaluate candidate each Check; no undocumented saved result. | Integer results: decompiler's tiny floats are incorrect return types. |
| B1 | 0x0054E780..0x0054E7A2; 0x0054E7A8..0x0054E7F2 | Backup subclass uses DriveStraight(distance,speed,false), saved caller bool +8B. Check contacts robot+338 first: present SetPoseOnCharger then success. Otherwise pitch<`BE860A92` ->`0400000A`; else DriveStraight Check, convert success to `04000006`, propagate every nonzero result. | Contact gate before pitch and drive; +8B not read by this Check body. | Inherits DriveStraight Init and destructor. |
| B2 | 0x00558228..0x005582E2; ARM veneer 0x008CB5F0..0x008CB5F4 | DriveOffContacts uses DriveStraight(10=`41200000`,20=`41A00000`,false), changes type to7; SDK mode clears track mask. Init saves current contacts +8B; absent returns0; present tail-jumps via ARM veneer to DriveStraight Init. | Veneer signed PC-relative dispatch resolves to PLT 4AB584, not an independent native Init. | Delegate Init result unchanged. |
| B3 | 0x005582E4..0x0055834C | If initially off contacts success. Otherwise ordinary drive Check; exactly RUNNING propagates; any terminal candidate discarded and current contacts tested. | Contacts absent ->0; contacts present ->`04000009`. | A failed drive can become success when contacts absent. |
| A1 | 0x00553370..0x00553442; 0x005532B8..0x00553324 | Align ctor IDock base; +F8 alignment, +C0=0 (no empty-space verification), +FC preaction type. X offset alignment0=6 (`40C00000`),1=0 and +BB=2,2=−15 (`C1700000`),3=distance−27 (`41D80000`); x<`C1800005` becomes0; Y/Z zero. | Alignment0/1/2/3 maps to pre-action type1/0/1/1 (words553360..55336C). Alignment>=4 logs and returns type1. | Alignment uses IDock Init/Check below. |
| A2 | 0x005534CC..0x005535A2 | SelectDockAction writes +80=`0Bh` iff alignment3, else0Ah. Verify only 0Ah/0Bh: if docking active ->`04000003`; inactive+Path IsActive ->`04000002`; inactive+path inactive+docking success byte+5 ->0; otherwise `04000003`. Other action ->`0300001A`. | Verify does not call body-stop itself; parent checks movement first. | Completion union (553492..4CA) contains object ID, default -1 fields and flag1; it does not encode alignment +F8. |
| K1 | 0x005514FC..0x0055164C | IDock Init resets deadline +90=-1; remove "dockActions" reaction lock then optional "reactionsToSuppress". Lookup/cast object; mark valid light cube +F5. If pre-action-pose checking +B9 enabled, GetPreActionPoses with type from vtable+3C, tolerances +88/+BC and +B9. | Failed pre-pose result propagated; selection vtable+38 must succeed before subscriptions. | Null/cast failure `03000004` (55159C sets SL=03000002;5516D8 adds2). |
| K2 | 0x0055164C..0x005517D4 | SelectDockAction, then register robot C5h and DAh handlers and optional external 4Ch deletion handler; reset +C2=0; markers +82/+84 initialized 3. | Replacing handles releases old ownership. Message serializer/parser itself M2. | Selection nonzero returns its result. |
| K3 | 0x005517D4..0x00551AAF | Without +B9 choose currently observed marker; zero marker list fails; one selected directly; many transformed relative robot, choose minimum 3-D norm with strict compare, ties retain earlier marker; failed transform logged/skipped. With prepose use selected prepose marker and optional second marker vtable+34. | No sorting marker list; set signed16 marker codes before SetupTurnAndVerify. | Empty observed-marker list `0300001D` (551A82); no selected marker `03000002` (551A2C). |
| K4 | 0x00551AAF..0x00551C5E | Setup turn/verify, optional suppress-mask reaction lock; valid cube and !+F6 plays interacting light animation then sets +F6; removes existing squint token; subclass InitInternal vtable+30; immediate embedded compound Update. | Only results 0 or RUNNING install "dockActions" lock and return success; other result propagated. | Behavior reaction lock execution M7, cube-light execution M3/M5 boundary. No substitute recipient wired. |
| K5 | 0x005521AC..0x005523A4 | Object ID -1 ->`03000004`. If verification compound exists Update it: RUNNING returns; failure propagates; success deletes it then DockWithObject(object,speed+AC,accel+B0,decel+B4,markers+82/+84,action+80,offsets+9C/+A0/+A4,flags+95/+BA/+BB/+C1). | Begin dock only after successful verification. Failed dock send `03000016`. Latch saw-docking +94; squint if docking becomes active and AI cube-game state !=1. | DockSquint args `3F866666`, `3EB33333`, `C1200000` (552378..552398), token +F4; M5 execution boundary. |
| K6 | 0x005523A4..0x005523F4 | Wait until docking was observed active, then becomes inactive. Body movement MC+9 keeps RUNNING. Read binary32 now. Head MC+A moving resets deadline=-1; unset deadline sets now+vtable+44 delay. now<deadline RUNNING, else return subclass Verify vtable+40. | Head moving repeatedly shifts verification deadline; body moving avoids timer read. | No blanket docking success based on inactive byte alone. |
| V1 | 0x005695BE..0x00569670 | NoObjectAtPose Init builds parallel [TurnTowardsPose(pose,maxAngle), MoveLift(preset3,tolerance `40A00000`)] at +78; replaces old compound; old WaitForImages PrepForCompletion before replacement; creates wait(number +98,VisionMode1,start timestamp0) at +7C; compound +56=1. | Parallel children list order turn then lift; image wait is separate and begins only after positioning Check succeeds. | Init0. Maximum angle `40490FDB`. |
| V2 | 0x00569724..0x00569838 | With positioning child Update; success PrepForCompletion then null/delete and return RUNNING. Once null, require image child, Update it then FindLocatedObjectClosestTo(pose,tolerance,filter) **even if wait returned failure/RUNNING**; any object overrides candidate to `0300001D`. | Transition consumes one tick; both-null `03000012`. | Object world lookup/filter belongs M11; no invented occupancy predicate. |
| V3 | 0x005687D6..0x00568868; 0x00568968..0x005689EE | IVisuallyVerify Init parallel [MoveLift(preset+7C,tol5), WaitForImages(vtable+30 count,mode+78,timestamp0)], +56=1, then subclass InitInternal. Check HaveSeenObject first ->success; else parallel RUNNING ->RUNNING; **every other parallel result** becomes `0300001D`. | Seeing object takes priority over child failure/remaining image count. | No propagation of motion child's terminal failure at this parent level. |
| V4 | 0x00568B80..0x00568DBA | VerifyObject resets seen +9A; subscribe external44h; marker-accepted +9B=(requested code==0). HaveSeen requires seen; no-code accepts immediately, otherwise lookup object and scan current observed marker codes, latch matching code. | External44h callback 56992E..569952: only when !seen, GetRobotObservedObject payload+C must equal requested ID +94 before seen +9A=1. | Boolean only; IVisuallyVerify supplies result mapping. |
| W1 | 0x0054CA64..0x0054CBF0; 0x0054CC1C..0x0054CC4C | WaitForImages stores count +78, starting timestamp +7C, VisionMode +88; no tracks. Init zero received +8C, subscribe external43h. Check unsigned received<count ->RUNNING; else clear/release handle and success. | Count0 completes on first Check; no arbitrary wall-clock wait. | Callback 54DD88..54DE38: timestamp must be strictly unsigned greater than saved +7C. Mode10h counts every qualifying image; other modes scan processed-mode vector, increment once on first match. Duplicate mode entries do not count twice; repeated qualifying timestamps can count. |
| U1 | 0x00549C48..0x00549D60 | PanAndTilt clears embedded parallel +78; copies runner suppression flag; construct Turn(request +114, absolute +124), tolerance +140, eye flag +126, optional speed/accel; add first(false,false). Head absolute +125 uses +11C else +11C+current head; tolerance +150, variation0; optional profile; add second(false,false). Embedded +56=1 and Update immediately. | Init maps exactly0 or RUNNING to0; any other child result unchanged. | Check 549D72 tail-calls embedded Update. |
| U2 | 0x0054A8FC..0x0054ACD4; 0x0054B010..0x0054B01E | TurnTowardsPose unset pose ->`03000005`; missing parent assigns world origin; otherwise transform pose into robot frame, failed transform ->same result. maxTurn>0 computes atan2(y,x); required abs(angle)>max sets skip +179=1 and returns0. ComputeHeadAngleToSeePose with extra parameter `3C23D70A`; on nonzero use fallback below; clamp target head then PanAndTilt.Init. | maxTurn<=0 suppresses body turn computation. Check skip returns0, else embedded Update. | Pose geometry and camera projection M11 boundary; own fallback arithmetic retained below. |
| U3 | 0x0054B428..0x0054B564 | Fallback binary32: z'=z−49 (`C2440000`); d=sqrt(x*x+y*y)+13 (`41500000`); u=(300−d)/150 (`43960000`,`43160000`), v=(0−z')/−10 (`C1200000`). a=clamp(u,0,1)*`3DB2B8C2`, b=clamp(v,0,1)*`3E060A92`; return Radians(((atan2f(z',d)+a)+b)+`3D8EFA35`). | Native conditional assignments govern unordered values; avoid replacing those with host Math.Clamp NaN assumptions. sqrt instruction with libm fallback only unordered. | Phone libm rounding outside shipped arithmetic; no approximation of constants. |
| O1 | 0x0054A1F0..0x0054A710 | TurnTowardsObject ID−1 requires custom object and same world origin; custom visual verification disabled. Normal ID lookup failure `03000004`. Code0 chooses closest valid marker via GetClosestMarkerPose; specified code enumerates matches, transforms relative robot and strict minimum 3-D norm; first tie retained. | No pose/marker ->`03000002`; transform failure `03000005`; copy selected pose +164, set +178 then TurnTowardsPose.Init; success clears +17A. | Object lookup/geometry native recipient M11; does not infer closest marker from center. |
| O2 | 0x0054ADD4..0x0054AF66 | Update embedded turn unless skip +179; any nonzero propagates. Mark turn finished +17A. If refine +193, Reset(false), clear refine, set refined tolerance from +194 (constructor `3DB2B8C2`) then return RUNNING. Else if visual verify +180 create VerifyObject(ID,code), replace old, +56=1, Update immediately; nonzero propagates. Afterwards Update retained verify again; success may construct TrackObject(ID,true), QueueAction(3,child,0), then return0. | Immediate Verify update can be followed by another Update same Check when it succeeds. Tracking requested +192 and real ID only; custom ID logs/returns0. | TrackObject body is an optional follow-on M14 tracking boundary; parent constructs/queues it, no fake success wiring. |
| K0 | 0x005502D8..0x005503E2; 0x00550416..0x0055045C | IDock base track mask7 or3 when caller bool true; angle tolerance +88=`3E060A92`, deadline -1, +95 caller bool, offsets zero, speed/accel/decel `42700000`/`43480000`/`43FA0000`. +B8 manual=false,+B9 prepose=true,+BA/+BB=0; +BC zero; +C0 empty verification=true,+C1=false,+C2=0,+C4 null,+C8 turn=true; post-lift trigger +F0=23Fh,squint +F4=0. | Speed/accel setters latch +B8; motion-profile setter returns false if latched, else copies profile+18/+1C/+20. Align overrides C0. | Align verify-delay vtable+44 ->55738A returns float bits `00000000`; InitInternal ->557382 returns0. |
| K7 | 0x00557C4C..0x00557D58; 0x00557E82..0x00557E9C | C5 moving-lift-post-dock callback skips sentinel trigger23Fh and requires configured response plus matching dock action +80. Constructs TriggerLiftSafeAnimationAction(trigger +F0,loops1,raw remaining args0,0,`42700000`,0); queues position5,retry0. DA liftLoad callback writes +C2=1 for payload bool true,2 for false. | These are distinct messages: DA is **not** motor-action acknowledgement. | M5 owns TriggerLiftSafeAnimationAction body; this row owns construction/queueing only. |
| U0 | 0x0054962C..0x00549702; 0x00549F10..0x00549F90 | PanAndTilt type14h mask5; caller pan/tilt+114/+11C, absolute booleans+124/+125; eyes+126=true. Defaults pan tolerance/head tolerance `3DB2B8C2`, pan accel `40A78D36`,pan speed `41200000`,head speed `41700000`,head accel `41A00000`; manual profile flags160/161 false. TurnTowardsPose calls pan relative/tilt absolute; stores absolute max turn+170,pose unset178,skip179 false. | Profile copied into command fields; manual zero pan acceleration specially copies speed into command acceleration (549CE2..549D02), otherwise copies explicit acceleration. | Do not exchange caller angle booleans or treat 5deg tolerance as head ctor's separate2deg minimum. |
| V0 | 0x00569014..0x0056909E; 0x005691FA..0x00569240; 0x00569A3A..0x00569A52 | NoObjectAtPose defaults count+98=10; pose copied,tolerance XYZ copied. Filter includes family raw6 (C57A00); appended captured-action predicate accepts object last-observed u32 at+1C >= Robot::GetLastImageTimeStamp(). | Predicate equality accepted, unlike WaitForImages' strict timestamp comparison. Filter-world combination/geometry belongs M11. | Not an all-object/all-history occupancy test. |
| V5 | 0x005686D8..0x0056874C; 0x00568A7C..0x00568B1E | IVisuallyVerify default image count10; VerifyObject passes VisionMode1,LiftPreset3; preserves ID and signed16 marker code,seen/marker-accepted initially false. | Image count read via virtual count getter during Init; base default storage+8C. | No movement speed argument in preset child ctor. |
| O0 | 0x00549DC0..0x00549E90; 0x00549FC8..0x0054A012 | TurnTowardsObject inherits TurnTowardsPose; turn-finished17A false,verify child17C null,verify180 caller,custom object18C null,track192 caller,refine193 true,refined tolerance194=`3DB2B8C2`. Destructor PrepForCompletion verify child, null pointer then delete before inherited teardown. | Refinement is an ordinary initial cycle, not opt-in host retry. | Preserve stop/preparation before deletion. |

| X1 | 0x00547530..0x00547562; 0x0055A5FC..0x0055A660 | DriveStraight destructor aborts Path only if IsActive exactly1, then DrivingAnimationHandler.ActionIsBeingDestroyed, then IAction base. DriveToPose follows same abort gate, erases planner obstacles(true) then(false), notifies animation handler, destroys prepose/shared-index/goals, then base. | Base stop/unlock follows these derived effects. | No implicit successful completion before abort. |
| X2 | 0x00545B94..0x00545BD4; 0x00548478..0x005484C4 | Turn dtor removes eye token only if eye flag and token, then zeroes token/releases callback handle before base. Head token nonzero: when+9D remove via MovementComponent.RemoveFaceLayerWhenHeadMoves(token,63h), otherwise TrackLayer.RemoveEyeShift(token,0); zero token,release callback handle,base. | Preserve recipient distinction and cleanup order. | M5/M4 own removal execution. |
| X3 | 0x00557448..0x005575B8 | Align/IDock cleanup deselects world object; aborts active Path then active Docking; resets docking+10=3,+C=-1; if+F6 stops cube-light trigger18h and resumes previous; RemoveSquint(token,FAh); PrepForCompletion retained verify child; removes dockActions lock only if state!=02000001, removes reactionsToSuppress if+C4; releases handles/children before base. | Destruction can alter other components even before generic stop/unlock. | Recipients are named boundaries, not omitted effects. |
| X4 | 0x0054D1D8..0x0054D1FC; 0x00569460..0x0056950E | PanAndTilt PrepForCompletion embedded parallel then compound destructor then base. NoObjectAtPose prepares positioning child then image child; clears filter/pose; nulls/deletes image child then positioning child then base. | Preparation order differs from deletion order; preserve both. | Generic track unlock remains after stop via base lifecycle. |

## Hosts and layer boundaries

| Piece | C# entry that should own it | Recipient owned elsewhere |
|---|---|---|
| DriveStraight / DriveToPose | `Manipulation/DriveActions.cs` | Path/planner M13, pose predicates M11/M12, driving animations M5 |
| IDock / TurnTowardsObject / lift child | `Manipulation/DockActions.cs` | Movement messages M4/M2; object/marker selection geometry M11; reaction locks M7 |
| Align / charger compounds / backup / contacts | `Manipulation/ChargerActions.cs` | Pose-on-charger M11/M12; contacts/pitch producer M3 |
| Flip parent child inputs | `Manipulation/FlipBlockAction.cs` | Object pre-action-pose M12 |
| PanAndTilt / TurnTowardsPose / Turn / Head / WaitForImages | `Vision/PanAndTiltActions.cs` | Native lifecycle must remain on IActionRunner/compound path; behavior's `PanAndTilt` convenience wrapper alone does not host it |
| NoObjectAtPose / VerifyObject | Action recipients of `DockActions.cs`, using `Vision/PanAndTiltActions.cs` child types | M11 located-object world / filter matching; observation messages M2 |
| Generic update, +56, tracks, compounds | `Actions/IActionRunner.cs`, `CompoundActions.cs`, `ActionList.cs` | [20261004-actionlist-extraction.md](20261004-actionlist-extraction.md), rows L/P/S/R: Init then timeout `03000018` before Check; stop before unlock; retained completed child and ignore-failure predicate |
| Optional follow-on tracking | `Vision` tracking action recipient, M14 | TrackObject execution not expanded into this M12/M13 report |
| Dock lift-safe trigger animation | Animation action recipient, M5 | Trigger/animation execution not expanded here |

No host clock, selected marker, contact state, image count or docking success is invented. Geometry, motor transport and animation execution are named recipients; local parameters/gates/ordering above remain obligations even while recipients await their own layer. Allocation failure uses shipped C++ exception machinery, not an invented action-result mapping. Phone libm is an external numerical dependency; its returned values do not authorize replacing shipped branch conditions or binary32 arithmetic.

Verification: reopened constructors, Init/Check bodies, ack/image/object/dock callback vtables and bodies, default literal pools, DriveToPose TBH targets, ARM veneer and align vtable delay; reviewed rows against those companions. Research introduces no production regression test and changes no fidelity status. Manager checks the rows before building.
