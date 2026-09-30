# Verifier report: R-VIS M13 gap pass 2 (re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md)

Verifier: cozmo-verifier (read-only), 2026-09-29. The verifier returned its full report as text; this file is the verifier's working notes plus the manager's transcription of its final verdict.

**VERDICT: FAIL on omissions and on the M13-021 status move only; the report's behavioural claims are all supported** (no wrong literal, branch or width in any row opened).

Objections: (1) the edge test: kmLine2WithLineIntersection 0x008F6220 recomputes each ray direction in float as (p+v)-p (e.g. vadd s8 = p2.x + v2.x at 0x008F624E, vsub at 0x008F626E), the cross is a float converted to double only for the compare (0x008F628A); t, u at 0x008F62AA..0x008F62E2; the Intersects caller builds only start and end-start with kmRay2FillWithEndpoints (0x00514832..0x005148D6); kmSegment2WithSegmentIntersection only wraps the call (0x008F6320). (2) Omitted: TurnInPlaceAction::GetCompletionUnion 0x005469A8 (Set_turnInPlaceCompleted with [this+0xD4]); MoveHeadToAngleAction Preset ctor 0x0054834D and GetPresetHeadAngle 0x00548441. (3) CreateFineTuneAction: the branch where +0x18C is invalid (bne.w 0x54C3E4 at 0x0054C2CA, then SetAction(nullptr), +0x190 = 2), probably dead. (4) MoveHead Init: the +0xA0 write also runs on the 0x03000016 failure path (0x00548574..0x00548586). (5) TurnInPlace Init: +0xAC = 0.5*|+0xA4| at 0x0054620C..0x00546210. (6) The proposed M13-021 move to IMPLEMENTATION_GAP is not supportable (MovementComponent TurnInPlace / MoveHeadToAngle / LockTracks / UnlockTracks bodies, AddOrUpdateEyeShift arguments, items 2 and 5 stay open). (7) Only Block's GetBoundingQuadXY is proven; MarkerlessObject (0x00502C32) and Vision::ObservableObject (0x0087713A) are relocations only; Charger, Ramp, MatPiece, CustomObject, HumanHead unresolved.

Existing-record claims checked and CONFIRMED: M13-014 unresolved (1) id 0 selects GetLastObservedFace; (2) NO_FACE 0x0300000E vs NoFace 0x0300000B (ActionResult.cs); (3) 'verified' = observations while the state is 0 or 1; M13-007's planar stand-in; M13-020 unresolved (4) now read.

Open: GetBoundingQuadXY bodies 0x0087713A / 0x00502C32; cv::minAreaRect; GetAbsoluteHeadAngleToLookAtPose 0x0054B428; ComputeHeadAngleToSeePose; MovementComponent bodies; TurnTowardsFaceWrapperAction 0x0054C7DC; the writers of +0x193 are not all enumerated (a linear sweep missed 0x005C229C).

## Working notes (verbatim)

# g5v report (in progress)
## Q1 progress
- Intersects 0x514800: PASS (4+4 Contains, 4x4 seg tests, ray order P0P2P3P1 both quads, Contains tri (P0,P1,P3),(P0,P2,P3)).
- Contains helper 0x4E03B8: literals 0x4E0480=0xB4000000, 0x4E0484=0x3F800001; strict both; PASS
- kmLine2WithLineIntersection 0x8F6220: cross float, cmp in double vs -1e-4/+1e-4 (0x8F6310/18 verified), PASS w/ nuance (cross is float then cvt to double)
- kmSegment2 0x8F6320: 0<=t<=1, 0<=u<=1 inclusive PASS
- Lambda 0x62B9FC + helper 0x62601C..: refPose copy 0x62603E-48, refQuad slot+0x50 padding r3=0 0x62604C-5C, closure stm/strd 0x6260D4-D8 (targetZ sp+0x70, refPose sp+0x94, refQuad sp+0x74, tol sp+0xA4, onTop sp+0xA3) PASS; z overwrite: x,y re-stored unchanged, z <- ref pose z 0x62BA6C-78 PASS; restore 0x62BA98-9E; eps 0x62BB5C=0x3727C5AC PASS
- Block::GetBoundingQuadXY 4E62A2: size = obj+0x88..0x90 + 2*padding (all three axes), corners vtable+0x28 = Block primary vtable 0x101db24 vptr 0x101db30 slot -> 0x101db58 GetCanonicalCorners reloc; +-0.5 x8 (0x4E60B8); rotate Rotation3d::operator*; xy; GetBoundingQuad; translate by transform +0x20,+0x24 via 0x4E68CC: PASS
- Quadrilateral ctor 4E7E9C stores [0]=A [1]=B [2]=C [3]=D; SortCornersClockwise passes (s0,s3,s1,s2): PASS. atan2(dy,dx) ascending.
## Q2/Q3 progress
- Init 0x54BD66..0x54BEBC: PASS (valid: GetFace null/GetWithRespectTo fail -> 54BE6C silent; invalid: GetLastObservedFace(pose,false) 0 -> silent; WRT fail -> warn BadLastObservedFacePose (strings 0x54BF18/0x54BF4C) -> 54BE6C; 0x193 set -> sChanneledInfoF Actions/TurnTowardsFaceAction.Init.NoFacePose (0xBE99E4/0xBEAB74/0xBEAB9A) return 0x0300000E; clear -> 0x190=3 return 0). Success: LockTracks call bl 0x4F0F4C(r0=[robot+0x254], r1=5, r2=[this+0x60], r3=this+0x48)
- Handler 0x54C050: PASS (state<=1 gate 0x54C062; valid: MatchesFaceID -> 18C=17C; invalid: GetFace(int), WRT robot pose (r0==1), d2=x2+y2+z2 via +0x20/+0x24/+0x28, strict < [0x184] (vcmpe;bpl skip), UpdateSmartFaceToID, store, debug log strings 0x54C208/0x54C234)
- CheckIfDone tbb table {2,99,31,121} at 0x54C514 -> 0x54C518/5DA/552/606 PASS; states 0-3 per report PASS
- CreateFineTuneAction PASS with omission: +0x18C INVALID branch (bne.w 0x54C3E4) -> SetAction(null), state=2, not stated
- Ctor 0x54B755 PASS. +0x193 setter not identified by report: inline strb at 0x5DE170 (const 1), 0x5F20B8 (const 1), 0x5F6942 (behaviour byte [r4+0x154]) all right after TurnTowardsFaceAction(Robot&,int,Radians,bool) -> OPEN item in report
- 0x55B3BC is not a TurnTowardsLastFacePoseAction *ctor* but an inlined construction in a factory function (allocs 0x1d8, vtable then overwritten at 0x55B3CC with 0xae3642-> derived); citation address right, characterization loose.
## Q4 progress
- TurnTowardsPoseAction::Init 0x54A8FC..0x54ACD2: PASS in full (BAD_POSE r7=0x03000005 returns at 0x54A9B8 err path and 0x54A952 WRT fail; HasParent==0 -> info + SetParent(WorldOrigin) 0x54AA4E; pan test operator>(+0x170,Radians 0) then atan2f(y=[+0x24],x=[+0x20]); operator<=(abs, +0x170) -> +0x114 else +0x179=1,r7=0 0x54AC8A; ComputeHeadAngleToSeePose(r3=0x3C23D70A) nonzero -> warn+GetAbsoluteHeadAngleToLookAtPose; clamp 0xBEDF66F3/0x3F46D3F2; +0x11C; PanAndTiltAction::Init)
- PanAndTilt ctor 0x54962C literals verified (0x3DB2B8C2, 0x40A78D36, 0x41200000, 0x41700000, 0x41A00000; 0x140/0x150 = Radians(5deg) from [+0x134]; strh 0 at 0x160/0x161; +0x126=1 at 0x5496B2)
- TurnTowardsPose ctors 0x549F10 / 0x54B344 PASS (pan abs=0, tilt abs=1; +0x178/+0x179 strh)
## Q5 progress
- TurnInPlace ctor 0x5459D4 PASS (0x40A78D36, 0x41200000, 0x41C80000, +0xB0=0x3D0EFA35, +0xD8=1, tag 0xC4)
- Init 0x545FA0 PASS (0x0300000A; AngleTooLarge return 0x03000000 with lit 0x5462E4=0x40C90FDB; TurnInPlace call args; 0x03000016 = 0x0300000A+0xC; RemoveKeepFaceAlive 0x21). Omission (minor): +0xAC = 0.5*|+0xA4| at 0x546208-10.
- IsBodyInPosition 0x546374 PASS (pi 0x5462E8/0x546424, 1e-5 0x546428)
- CheckIfDone 0x54642C PASS (a)-(h); 0x04000004 at 0x546818
- MoveHead ctor 0x547E40 PASS (15.0/20.0 at +0x90/+0x94, +0x98=0,+0x9C=1,+0x9D=0, clamps, tol 2deg lit 0x548338=0x3D0EFA35, variability), IsHeadInPosition PASS, Init 0x548534 PASS (0x03000016; note +0xA0 write executes also on the failure path), CheckIfDone 0x5485CC PASS incl. 0x04000004; ack handlers 0x54D3F8 / 0x54D624 PASS (Get_motorActionAck)
## Q6 progress
- Setters 0x54B978/0x54BA84/0x54BB8C/0x54BC6C PASS (flag check [+0x191], debug event strings, swap into +0x198/+0x1B0, operator= for callback; constant fns 0x54DC1A/0x54DC7E ldr r0,[r0,#4]). Note these are TurnTowardsFaceAction methods; report attributes them to record M13-021 (should be M13-014 per class)
## Additional findings
- OMITTED: kmLine2WithLineIntersection recomputes each direction component as (p+v)-p in float (0x8F626E..0x8F627A) before cross/t/u; report writes plain dx*dy-dy*dx. Cross is float, cvt to double for compare.
- OMITTED: TurnInPlaceAction::GetCompletionUnion 0x5469A8 -> Set_turnInPlaceCompleted{[this+0xD4]}; MoveHeadToAngleAction Preset ctor 0x54834D and GetPresetHeadAngle 0x548441 not read.
- OMITTED: CreateFineTuneAction with +0x18C invalid (0x54C2CA bne.w 0x54C3E4 -> SetAction(null), state=2).
- Init 0x548534: +0xA0 write happens on failure path too (no observable effect).
Final: see handback.
