# Verifier report: R-VIS M12 gap pass 2 (re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md)

Verifier: cozmo-verifier (read-only), 2026-09-29. The verifier returned its full report as text; this file is the verifier's working notes plus the manager's transcription of its final verdict and corrections.

**VERDICT: PASS WITH CORRECTIONS.** All 14 questions hold on the disassembly; three readings are wrong and three claims about existing records are stale or wrong.

Wrong readings:
1. Q6.16 (+0xBB writers): RollObjectAction stores 0 in +0xBB (0x005565C8 movs r0,#0; 0x005565D6 strb.w r0,[r4,#0xbb]); the 5 is stored two instructions later at 0x005565DC into +0x80 (DockAction). Whole-.text scan of `strb ...,[rX,#0xbb]` (0x400000-0x900000): 0x5533F6 Align = 2; 0x5536EA Pickup = 2; 0x554E12 PlaceRel InitInternal = 3 iff |B'| >= 1e-5; 0x5554D0 PlaceRel SelectDockAction = 0; 0x5565D6 Roll = 0; 0x55C5A2 SetDockingMethod = its argument; ctor default 0 (the 0x100 word at 0x5503AA).
2. Q6.9 marker choice B: the distance uses Robot::GetPose() (0x00551B06..0x00551B12 bl 0x4ea398, GetWithRespectTo(markerPose, robotPose)), not the robot pose's GetParent; it is 3-D against a FLT_MAX-initialised best (strict smaller wins, literal 0x551DD4); branch B rejoins at 0x00551844 so the NullDockMarker check (0x03000002) and [this+0x82] = *(u16*)marker also run on it.
3. Q4.1: the binary DOES name the fields: CameraCalibration::CreateJson 0x0085F16C writes "nrows" (ldrh [c]), "ncols" ([c+2]), "focalLength_x" ([c+4]), "focalLength_y" (+8), "center_x" (+0xC), "center_y" (+0x10), "skew" (+0x14).

Omissions / minor: Q8.5 omits site 0x0064941C; Q4.9 a second no-advance path (0x0055616E -> 0x00556200 -> 0x00556252 -> 0x005562D8); Q6.20 the -16.000009 literal is at 0x00550484; Q9.6 IsSameAs receiver is the vector element; Q7.1 the wire passes A = [msg+0x30], B = 0.0.

Existing records: M12-022's "NaN or negative -> 0x0300000D" CONFIRMED wrong (0x00558FE2 vcmpe s0,#0; 0x00558FEA bpl 0x0055905C taken for NaN, +0 and -0; only a strictly negative value errors). M12-026's "12.18 returns the GetPreActionPoses result" is a false positive (true). M12-005's writer list is incomplete and the Roll clause wrong. M12-017 too weak. M12-025 '[+0xBB]=3 if |B| >= eps' ambiguous: the binary tests B' (0x00554DDC transform, then 0x00554DE0 vldr s0,[r4,#0x10c]). M12-028: the bl 0x5586AC sites are exactly 0x5585F8, 0x55889C, 0x55C8A8, 0x55E2CA, 0x55E3E0, 0x55EB74, 0x55EC2A, 0x5B58C2.

Agreements with the earlier M12 diff verifier: InitInternal at the end of Init (0x00551C0C); it tests B'; final +0xBB = 3 iff |B'| >= eps; NaN row contradicted; TransformPlacement strict raw compare; IsNear strict and wrap-aware (the C# uses <=); marker size KnownMarker+0x10.

Open: the 0xC5 / 0xDA handler bodies; SetupTurnAndVerifyAction 0x00551F9C; DrivingAnimationHandler (M12-024); CubeLightComponent (M10); PathComponent branch conditions (M13); the angle helper 0x00550858; flip-block installer bodies; PlaceRelObjectParameters producers.

## Working notes (verbatim)

# g6v report (in progress)

## Progress notes (verified so far)
- Q1 PASS (0x877574 disasm), Q2 PASS (0x877774), Q3 PASS (rescale 0x84c87c, IsNear 0x84cc0a, > 0x84cc90, GetAngleAroundZaxis 0x84aa1c), Q4 PASS (CameraCalibration CreateJson 0x85f16c has literal names nrows(+0) ncols(+2) focalLength_x(+4)), Q5 PASS.
- Q6 order PASS; small errors: 6.9 says robot GetParent frame -- code passes Robot::GetPose (0x551b06..0x551b12 bl 0x4ea398, no GetParent); branch B joins at 0x551844 so +0x82 also stored there.
- Q6.16-6.20 PASS (SelectDockAction 0x555434.., InitInternal 0x554dcc, ctor 0x5502d8; DockWithObject arg->wire: 0x63bb56..0x63bb90 + builder 0x63bd50: +0x13 = DockingMethod = [+0xBB]). Q7 PASS (ctor 0x55c7d0, IDrive ctor 0x55b1f4, AddDockAction 0x55b7ac, lambda1 0x55de34, callers scan: 3 bl/blx to PLT 0x4abfd4).
- Q8 PASS on verified items: handler 0x64b370/helper 0x64b61c, UpdateCurrentPathSegment 0x6493b4, Update 0x6494cc, predicates masks, SetStatus call-site scan (my own scan of 21 sites matches the 8.5 list; the 8.5 list omits site 0x64941c (=8.4 seg<0 -> status 1, covered by 8.4). 0x649C4C/0x649390/0x64937C are within functions that tail-call the setter through veneer 0x8ccf0c).
- Q9 PASS (installer scan finds exactly 8 bl sites to 0x5586ac; GetPossiblePoses 0x558c80; RemoveMatchingPredockPose 0x551418 -- receiver of IsSameAs is the vector element, extractor wrote match.IsSameAs(p): cosmetic).
- Q10 PASS (0x5508c8..0x551200 fully read: errors 0x03000004/0x03000010/0x03000005/0x04000001, approach filter literal 0x3F490F33 at 0x550dc4, FLT_MAX at 0x550dc8, -1e-5 at 0x5513b0, threshold arg order r1=pose r2=objPose r3=Radians).
- Q11 PASS (0x632e94..0x632f14 filter layout, FindLocatedObjectHelper 0x61eb78 origin-mode byte), Q12.1-12.7 PASS (ctor 0x55a238, Init 0x55a86c, StartDrivingToPose returns, 7-arg ctor 0x55850c, InitHelper 0x558fc0..0x559458).
- Q13 PASS (ctor 0x559cd4, factories 0x52a4c0/0x554afc, Init 0x559db8, CheckIfDone 0x559fa4, IsPlacementGoalFree 0x55a070; ComputePlacementApproachAngle result codes 0x11/0x04/0x05/0x0B verified; angle helper 0x550858 not opened).
- Q11-Q14 PASS. Corrections found: Roll writes +0xBB=0 (0x5565D6), 5 goes to +0x80 (0x5565DC); Init branch B uses Robot::GetPose (not parent) and also stores +0x82; CameraCalibration field names exist in CreateJson 0x85F16C.
- Full final report delivered via SubagentHandback (too long for a single shell heredoc).
