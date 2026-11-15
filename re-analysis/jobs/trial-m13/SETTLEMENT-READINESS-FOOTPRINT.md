# FOOTPRINT-GROUP-SETTLEMENT-READINESS-1 (Claude worker, 2026-11-14)

Doc only. Records re-read in the current `re-analysis/fidelity_manifest.json` (HEAD ed68babd), with the handoffs
FIND-ON-TOP-SAME-ROOT-1, CHARGER-BOUNDING-QUAD-1, REFERENCE-SIZE-SLOT-1, MIN-AREA-RECT-FOOTPRINT-1, CARRY-TREE-FLAG-RESET-1,
CAN-INTERACT-ROOT-CHECK-1 and the notes INVENTORY-NOTE-GET-SIZE-SLOT-RECORD-1 / INVENTORY-NOTE-M13-007-CALLER-CENSUS-1.
Addresses marked **re-read** were disassembled for this job (capstone on `resources/lib/armeabi-v7a/libcozmoEngine.so` and
`libopencv_imgproc.so`; PLT names from `.rel.plt`; `re-analysis/decomp` is empty). Everything else is quoted from records,
handoffs or checks: a lead, not a proof. Code was read at the current working tree.

Key: **B** behaviour gap on the record's own path. **IC** inventory/citation correction (manager). **H** part owned by
another record that must settle first. **V** visibility / test reach. **S** stale text (cleanup, not blocking).
**L** log text only (never blocks).

Subsystem review: M11-vision INVENTORY_APPROVED 2026-11-14; M12-manipulation 2026-11-13; M13-navigation 2026-11-13.

---

## Findings that change earlier notes (re-read for this job)

1. **The f32-vs-double axis-selection tie is not a behaviour difference.** GetRotatedParentAxis<'Z'> 0x005507A0..0x00550854
   reads row 2 as f32 through SmallMatrix(2,i) (0x005507B4..0x005507C6), compares |y|>|x| then |z|>|best| with strict
   vcmpe.f32 + gt (0x005507E8, 0x0055082E), signs by > 0 (0x005507F0, 0x005507FE, 0x0055081E), and on unordered keeps the
   earlier axis (movle r0,r1 at 0x0055083C). The stack's RotatedParentAxis.Get (DockActions.cs:223-231) compares in double,
   but Mat3 stores float[] _m (Geometry.cs:36) and widens exactly in its indexer (Geometry.cs:49), so every comparison, tie
   and NaN gives the f32 result. The queued REFERENCE-SIZE-SLOT-1 verifier note is closed by read (S).
2. **What feeds that axis test is M11-065/M11-067's.** H4 (0x0062605E..0x00626070), P5 (0x0062BAA4..0x0062BAB6) and
   IsRestingFlat (0x0087752E..0x00877544) all pass Rotation3d::GetRotationMatrix (PLT 0x4A4054 -> 0x0084AAE0), which ends in
   blx RotationMatrixBase<3>::Renormalize (0x0084ABE0). So the M11-065 R17/R18 / M11-067 (Rodrigues) dependency is not only
   the charger quad's: it decides the size axis for every non-cubic object (charger, markerless) and the IsRestingFlat dot
   value. For a 44^3 cube the chosen axis does not change the dimension. The stack implements it (Rotation3d.Matrix,
   NativePoseArithmetic.cs:99-104 -> Mat3.Orthonormalized, Geometry.cs:86-103); its settlement is M11-065/M11-067's.
3. **A root reference pose faults in the engine; the stack says it "finds nothing".** The lambda's P1 calls refPose.GetParent()
   (0x0062BA2C..0x0062BA30, PLT 0x4A74D4 -> 0x008453D4). GetParent passes &node->parent (0x0084540E) to WrapExistingNode
   (PLT 0x4CE150 -> 0x0084529C), which reads [parent+0x48] at 0x008452BA: for a root refPose (copied at 0x004DF664..0x004DF66A)
   the parent is null, a null read. M11-076 already lists 0x0062BA30 as an undefined-tree crash point. Contradicted: the
   M13-007 unresolved ("A root reference pose ... finds nothing: refPose.GetParent() is null") and the doc of
   BlockWorld.SharesReferenceRoot (BlockWorld.cs ~3090-3100, "the call fails (0x00845A46..0x00845A58)"). Under the M1-034
   standing rule the stack must skip and log visibly naming M1-034 and the owning record. BlockWorld returns false silently;
   NativeConfigurationGeometry.Neighbor uses reference.FindRoot() instead of GetParent, so for a root reference it treats the
   reference itself as the parent and CAN find a candidate. The fault needs at least one candidate past the filters. Reach: no
   production writer of a root object pose was shown (UNKNOWN).
4. **IsRestingFlat with a null pose node also faults in the engine** (open item from CAN-INTERACT-ROOT-CHECK-1).
   0x0087751C: GetWithRespectToRoot (0x00877528 -> 0x004DF628); IsRoot on a null node returns 1 (0x00844AA4 cbz ->
   0x00844ABC); the root branch assigns with operator= (0x004DF66A -> 0x00845940), whose null-source path stores node = null
   into the destination (0x0084594A cbz -> 0x00845956 ... 0x008459B0 strd r1,r1,[r4,#4] with r1 = 0); GetTransform returns
   [this+4] (0x0084499A) = null; GetRotationMatrix loads the quaternion with vldmia r1 (0x0084AAEA) from address 0. Not read:
   the body of PLT 0x4CE114 called on the destination node at 0x00845958. An M1-034 crash point, not defined behaviour.
5. **A second markerless +0x50 producer is live.** BlockWorld.MarkerlessQuad / QuadFor (BlockWorld.cs:1596-1603) use
   ObjectFootprint.MarkerlessBoundingQuadXY (BlockConfigurations.cs:864-882): the public Pose, no re-root, the rotation
   matrix rows, and a ReportMissing naming M11-091 on every call. MarkerlessObject::GetBoundingQuadXY 0x00502C32 re-roots
   (0x00502C50) and rotates through Rotation3d::operator* (M13-025 authority). The slot path TryNativeMarkerlessBoundingQuadXY
   (BlockConfigurations.cs:851-862) does it the engine's way. The two can differ in the last bits.
6. **Ramp / HumanHead construction (M13-025 reach).** A Thumb-2 BL/BLX scan of .text (direct targets and their PLT stubs)
   finds the Ramp C1 constructor (0x0050E900, PLT 0x4A7588) called only at 0x0050F89A in Ramp::CloneType 0x0050F890, and
   HumanHead C2 (0x004F8550, PLT 0x4A5EF0) only at 0x004F8A16, in an unnamed function at 0x004F8A08 that allocates 0x74
   bytes and passes [this+0x4C] (the CloneType shape). Inlined construction (code loading the vtable GOT slots 0x0103E8C0
   HumanHead, 0x0103E950 Ramp) was NOT traced: my literal scan for those was too noisy to use. UNKNOWN until done.

---

## M13-023: IMPLEMENTATION_GAP

Title: "Footprint corner ordering: SortCornersClockwise (centroid, atan2f, __sort4/sort3 ties, Quadrilateral slots) and the
cv::Rectangle fallback (empty input, bounds, center); corner writers read and unreachable/out of scope".
Unresolved (key quotes): "read the MatPiece +0x7C and CustomObject +0x64 corner-list writers ..., cv::Rodrigues,
SortCornersClockwise's tie order and InitFromPointContainer; the libm equivalence needs a policy decision"; "(e) answered:
bionic libm does not ship, so its calls are EQUIVALENT_IMPLEMENTATION"; "Part (a), the corner writers, is read and not built:
MatPiece/FlatMat/Bridge/Platform are never constructed; CustomObject is reached only through game-to-engine messages no Unity
code sends (SDK-only ...)"; B-M13-4 "partial build, awaiting strong verification; BLOCKED"; MIN-AREA-RECT-FOOTPRINT-1 "the
planar stand-in ... is deleted".

Re-read: SortCornersClockwise 0x004E7EFC..0x004E7F9A: ComputeCentroid 0x004E7F16; vsub.f32 p - c per coordinate 0x004E7F44;
atan2f(dy, dx) 0x004E7F4E..0x004E7F52 (r0 = [sp+0xC] = dy, r1 = [sp+8] = dx); libc++ __sort<CompareFirst> 0x004E7F76;
Quadrilateral ctor with (s0, s3, s1, s2) 0x004E7F7A..0x004E7F96. Stack SortCornersClockwise / SortFourAngles /
SortQuadrilateral (BlockConfigurations.cs:913-946) match; the centroid order ((c0+c2)+c1)+c3 is ov08 S1's, not re-read.
Fallback: catch at 0x004E65C6 -> InitFromPointContainer call 0x004E6778 (PLT 0x4A48F4; body 0x004E81B4, symbol size 172)
= MinAreaRectF.ComputeBoundingQuadRectangle + RectangleFallback (BlockConfigurations.cs:582-611).

| Item | Kind | Address | Closing job |
| --- | --- | --- | --- |
| (a) MatPiece/FlatMat/Bridge/Platform corner writers: never constructed (ov08: no constructor caller outside CloneType). Needs a recorded unreachable claim with that citation | IC (decision record) | 0x00502DD8..0x00502ECE, 0x004F6B58..0x004F70C0 | manager at settlement |
| (a) CustomObject corners: SDK-only. Needs "not supported: SDK mode, operator 2026-10-09" in unresolved | IC (decision record) | 0x004EF790..0x004F0340, 0x0061E1EC.. | manager at settlement |
| (b) cv::Rodrigues is on neither the sort nor the fallback; it is M11-067's and feeds the base quad (M13-025). Move it out of this claim | IC | 0x008499B0, 0x0084ABE0 | manager |
| (d) Robot::GetCameraPose(float), Robot +0x2CC/+0x258 (M13-021/M11-054): no link to 0x004E7EFC or 0x004E81B4 found; relevance UNKNOWN | IC | UNKNOWN | manager: drop or justify |
| Strong verification of the B-M13-4 sort tree and R1-R3 (built, "awaiting strong verification") | V | 0x004E8260, 0x004E85BE, 0x004E81B4..0x004E825E | NEW FOOTPRINT-CORE-CLAUDE-CHECK-1 |
| Tests: unit only (M13RVisBuildTests, M13CheckedRowsBuilderTests, ...). FreeplayTests (production) reaches the sort through the charger quad but asserts nothing about order | V | n/a | FOOTPRINT-CORE-CLAUDE-CHECK-1 decides whether unit suffices |

Stale: the first unresolved sentence ("read ... tie order and InitFromPointContainer"), superseded by ov08 and B-M13-4;
"BLOCKED" in B-M13-4 (it blocked on the GetBoundingQuad catch, now built; see M13-024); evidence[0] "0x004E6778
InitFromPointContainer" is the call site, the body is 0x004E81B4 (IC, minor).
Proposed: **EQUIVALENT_IMPLEMENTATION** (atan2f at 0x004E7F52 is bionic libm, M13-023(e)); everything else exact.

## M13-024: IMPLEMENTATION_GAP

Title: "The exact footprint: cv::minAreaRect, convexHull, Sklansky_ and RotatedRect::points".
Unresolved (key quotes): "build the port ... replacing both the exact-rectangle shortcut and the planar stand-in for cube
candidates"; B-M13-11 "Full GetBoundingQuad exception/catch integration, sort ties and shared numerical/owner boundaries remain
open"; MIN-AREA-RECT-FOOTPRINT-1 "M9 fixed: the area keep ... bhi at imgproc 0x0009CCC8 ... Still open: the convexHull body
(0x00039174) and the incrementing Sklansky copy (0x0003796C..) were not re-read by the census; the order of exactly-equal hull
input points (std::sort vs Array.Sort: sign of zero only); libm per M13-023(e)".

Re-read: imgproc 0x0009CCC0 vcmpe.f32 s13,s22; 0x0009CCC8 bhi skips the store on greater or unordered = stack
KeepsArea(area, minarea) => area <= minarea (BlockConfigurations.cs:789, used at :763). The cv::convexHull symbol is
0x00039175 size 1364, so its body is 0x00039174..0x000396C8.

| Item | Kind | Address | Closing job |
| --- | --- | --- | --- |
| convexHull body and the incrementing Sklansky_ instantiation not read against MinAreaRectF; only the 2026-09-29 emulation of 270 inputs for port.py, which has the NaN error below, so NaN was not in that corpus | B (RECOVERABLE) | imgproc 0x00039174..0x000396C8; 0x0003796C.. | NEW MIN-AREA-RECT-HULL-READ-1 |
| Equal-point order: libc++ std::sort with CHullCmpPoints vs Array.Sort (BlockConfigurations.cs:661); differs only between +0 and -0 points. Record no effect with evidence, or port the sort | B (minor) | inside 0x00039174.. | MIN-AREA-RECT-HULL-READ-1 |
| The engine catch is catch-all (LSDA 0x004E64F4..0x004E6502); the stack catches only OrientationException (BlockConfigurations.cs:582-591). Whether any other throw (another CV_Assert, allocation) is reachable with 8 CV_32FC2 points is UNKNOWN | UNKNOWN | imgproc 0x0009C760..0x0009CEAC, 0x00039174..0x000396C8 | MIN-AREA-RECT-HULL-READ-1 |
| evidence[1] cites port.py, whose line 105 stores on unordered ("if not (area>minarea)"), contrary to 0x0009CCC8 | IC | port.py:105 | manager: annotate the evidence line |
| evidence[0] range "0x00039174..0x005395E0" is a typo (the body ends 0x000396C8) | IC | as stated | manager |
| Catch log: stack "GetBoundingQuad: OpenCV(-215) ..." (BlockConfigurations.cs:587) vs engine GetBoundingQuad.CvMinAreaRectFailed.COZMO-1916 / "cvPoints.size=%zu%s" | L | 0x004E65C6.. | none |
| GetBoundingQuad<float> 0x004E6490 calls SortCornersClockwise and the fallback | H (M13-023) | 0x004E6490..0x004E6570 | M13-023 first |
| Strong verification of the port | V | n/a | FOOTPRINT-CORE-CLAUDE-CHECK-1, after HULL-READ |
| Tests: MinAreaRectFootprintTests and builders, all unit | V | n/a | same |

Stale: provenance ("the C# uses the planar stand-in ... an exact-rectangle footprint only for yaw-only cubes"; not a frozen
field); the first unresolved paragraph; B-M13-4/B-M13-11 "catch integration remains open" (the orientation != 0 path is built,
BlockConfigurations.cs:582-591; only the UNKNOWN row above remains).
Proposed: **EQUIVALENT_IMPLEMENTATION** (double atan2 in the calipers and cos/sin in RotatedRect::points 0x00086188 are bionic
libm; sqrt is correctly rounded on both sides).

## M11-105: IMPLEMENTATION_GAP

Title: "ObservableObject vtable +0x2C (the object's size) per class, and its readers GetDimInParentFrame<Z> and the GetObstacles Z predicate".
Unresolved (key quotes): "(2) other GetDimInParentFrame<Z> readers (IDockAction::SetupTurnAndVerifyAction 0x00552028,
BehaviorCheckForStackAtInterval 0x005D757A, BuildPyramidBase 0x005DD1A2/0x005DD7E8, AcknowledgeObject 0x00603914, PyramidBase
0x006178B8/0x00617B7E/0x00617D7E/0x00617DAE, CheckForCollisionWithRobot 0x00625A00, GetZRotatedPointAboveObjectCenter 0x008775AE,
IsRestingAtHeight 0x008776A8, IsPoseTooHigh 0x0087797A) use CubeGeometry.SizeOf (44 for markerless) in the stack; GetDimInParentFrame<X>
0x0055770C and <Y> 0x00557750 are unread; (3) other writers of Charger+0xF0 and Block+0x88 were not searched; (4) the GetSizeByType
miss default (10,10,10) is unreachable ... the stack returns null there; (5) an SDK CustomObject has no size".

Re-read: slot bodies 0x004E382C adds r0,#0x88 (thunk 0x004E3830); 0x004EA672..0x004EA67C (+0xF0 after the thunk adjust);
0x00502DD0 adds r0,#0x58. Vtable words 0x0101DBEC and 0x0101CE18 = 0x004E3831, 0x0101E1F0 = 0x004EA673, 0x0101F344 =
0x00502DD1 (R_ARM_RELATIVE). GetDimInParentFrame<Z> 0x00557794..0x005577D4: axis + 3, tbb bytes 04 0A 10 10 10 0A 04 at
0x005577AA: +-3 -> +8, +-2 -> +4, else +0. Stack ObservableSize2C (ObservableMapSlots.cs:18-22) and DimInParentFrameZ
(NativeConfigurationGeometry.cs, shared by Neighbor and the BlockWorld helper) match; see Finding 1.

| Item | Kind | Address | Closing job |
| --- | --- | --- | --- |
| (2) Ten other readers use CubeGeometry.SizeOf (CubeGeometry.cs:150: the charger size, else 44). Wrong only where a markerless object reaches the reader; reach per reader not censused | B (UNKNOWN reach) | the ten sites listed | NEW SIZE-SLOT-READERS-CENSUS-1 |
| (3) Other writers of Charger+0xF0 and Block+0x88 not searched | B (RECOVERABLE) | UNKNOWN | SIZE-SLOT-READERS-CENSUS-1 |
| (4) GetSizeByType miss (10,10,10): record it as a branch our system never produces (only 0xE/0xF/0x10 are constructed, M11-091) | IC (decision) | 0x00502840..0x00502852 | manager |
| (5) SDK CustomObject: "not supported: SDK mode, operator 2026-10-09" | IC (decision) | n/a | manager |
| The rotation the axis is chosen from: GetRotationMatrix + Renormalize (Finding 2) | H (M11-065 R17/R18, M11-067) | 0x0084AAE0, 0x0084ABE0 | their settlement (no queue row) |
| GetDimInParentFrame<X>/<Y> are outside the title: drop from unresolved or name a reader on this path | IC | 0x0055770C, 0x00557750 | manager |
| Tests: ReferenceSizeSlotNeighborTests, ChargerBoundingQuadNeighborTests, ChargerObstacleListTests, MinAreaRectFootprintTests, all unit | V | n/a | manager: unit may suffice for a per-class table |

Stale: the f32/double axis tie (Finding 1); the older sentence that the reference reads CubeGeometry.SizeOf (REFERENCE-SIZE-SLOT-1 verifier queue).
Proposed: **EXACT_SOURCE** (no libm on its own path).

## M13-025: IMPLEMENTATION_GAP

Title: "GetBoundingQuadXY for every class: Markerless, the base ObservableObject version and the canonical corner lists".
Unresolved (key quotes): "build the class footprints for Markerless, Charger, Ramp, HumanHead; the MatPiece/CustomObject corner
lists and the numeric result of the base version (cv::Rodrigues renormalisation) stay open in M13-023"; "Footprint.GetBoundingQuadXY
throws for a cube that is not yaw-only"; CHARGER-BOUNDING-QUAD-1 "built, awaiting strong verification ... OPEN: CubeGeometry.SizeOf
returns 44 for markerless types"; REFERENCE-SIZE-SLOT-1 cleanup "that slot is owned by M11-105, not this record".

Re-read: Block::GetBoundingQuadXY 0x004E62A2: re-root bl 0x4DF628 at 0x004E62C0; Rotation3d::operator*(Point3f) (PLT 0x4A4864)
at 0x004E635A; GetBoundingQuad<float> 0x004E6396; translate bl 0x4E68CC at 0x004E63B2. Base 0x0087713A: GetTransform and
GetRotationMatrix 0x00877158/0x00877160 (so Renormalize), __signbitf x3 at 0x008771B8/0x008771DA/0x008771FC, GetBoundingQuad<float>
0x008772B6, translate 0x008772D2; no re-root (the stack ObservableObjectBoundingQuadXY, BlockConfigurations.cs:886-907, uses
atPose directly: match).

| Item | Kind | Address | Closing job |
| --- | --- | --- | --- |
| Second markerless producer (Finding 5): BlockWorld.cs:1596-1603 -> MarkerlessBoundingQuadXY (BlockConfigurations.cs:864-882) differs from 0x00502C32 (no re-root, matrix rows, public Pose, a MISSING per call) | B | 0x00502C32..0x00502D42, 0x00502C50 | NEW MARKERLESS-QUAD-SINGLE-PATH-1 |
| Ramp (corners 0x0050E2D0) and HumanHead (0x004F8454) quads unbuilt (TryObservableBoundingQuad50 reports MISSING). Direct calls suggest clone-only construction (Finding 6); inlined construction not excluded | B (UNKNOWN reach) | 0x0050F89A, 0x004F8A16; GOT 0x0103E8C0, 0x0103E950 | SIZE-SLOT-READERS-CENSUS-1 (finish the reach proof, then an exclusion or a build) |
| Charger base quad numerics through GetRotationMatrix + Renormalize; Block and Markerless through operator* and the re-root | H (M11-065 R7/R8/R17/R18, M11-067, M11-066) | 0x00877160, 0x0084ABE0, 0x004E635A, 0x004E62C0 | their settlement |
| Per-class dispatch of slot +0x50 | H (M11-088) | ObservableMapSlots.cs:25-45 | M11-088 |
| GetBoundingQuad<float> and the sort | H (M13-024, M13-023) | 0x004E6490 | those first |
| Tests: ChargerBoundingQuadNeighborTests, HikingBringBeaconCandidatesTests (unit); FreeplayTests (production) asserts only that a charger at (30,0,0) yaw pi does not block a cube at x=260 (FreeplayTests.cs:1981-1983) | V | n/a | NEW FIND-ON-TOP-PRODUCTION-TEST-1 |

Stale: "Footprint.GetBoundingQuadXY throws for a cube that is not yaw-only (BlockConfigurations.cs:400-402)" (every cube pose is
computed, :550-563); "Not touched (MIN-AREA-RECT-FOOTPRINT-1): the PlanarStandIn sites" (deleted); "OPEN: CubeGeometry.SizeOf
returns 44 for markerless" (that is M11-105 item 2); the dead catch(NotSupportedException) in the charger branch of
TryObservableBoundingQuad50 (ObservableMapSlots.cs:29-33) and its "branch unported" MISSING text (no producer remains);
Footprint.GetBoundingQuadXY's throw texts (BlockConfigurations.cs:542/544; verifier queue).
Proposed: **EQUIVALENT_IMPLEMENTATION** (inherits libm through M13-023/M13-024; its own arithmetic has none).

## M13-007: IMPLEMENTATION_GAP

Title: "FindObjectOnTopOrUnderneathHelper: its found-object test and the tolerance at each of its eleven callers".
Unresolved (key quotes): "Authority and evidence[0] corrected (manager, 2026-11-13) ... refPose is re-rooted"; the R-VIS
"NOT BUILT: cv::minAreaRect" and "LABELLED STAND-IN" text; FIND-ON-TOP-SAME-ROOT-1 "A root reference pose (no parent, not a
host-fixture flat pose) finds nothing"; "SECOND IMPLEMENTATION: NativeConfigurationGeometry.Neighbor"; "HOST EXTENSION NOT CHANGED";
M13-007-CALLER-CENSUS-1 "all eleven call sites match the engine in tolerance, onTop and filter"; CAN-INTERACT-ROOT-CHECK-1
"Neighbor's predicate no longer calls Require(candidate.NativePose)".

Re-read: helper 0x0062601C..0x006260DC: GetPose 0x0062603E; GetWithRespectToRoot 0x00626048; reference slot +0x50 padding 0
0x0062604C..0x0062605C; GetTransform/GetRotationMatrix 0x00626060/0x00626068; GetDimInParentFrame<Z> 0x00626070; target =
z + dim*(onTop ? 0.5 : -0.5) in f32 0x0062607E..0x006260A0; ignore-ID insert 0x006260AC..0x006260B8; closure 0x006260C0..0x006260D8.
Lambda 0x0062B9FC..0x0062BB1E: GetParent 0x0062BA30; GetWithRespectTo 0x0062BA3A, 0 -> false 0x0062BA48; candidate z replaced
0x0062BA74..0x0062BA78; candidate slot +0x50 0x0062BA86; Intersects (PLT 0x4A7DEC) 0x0062BA8C; z restored 0x0062BA9A; size only
after the intersect 0x0062BAB6; |target - (cz + dim*s)| against tol + 1e-5 with vcmpe + ge (NaN false) 0x0062BAF4..0x0062BB0E.
Stack: the BlockWorld helper (BlockWorld.cs:3049-3068) -> FindObjectOnTopOrUnderneathInRootFrame (BlockConfigurations.cs:373-410),
and Neighbor (NativeConfigurationGeometry.cs), match on the cube, charger and markerless paths except the root-reference case.
The BlockWorld side computes the candidate quad at the candidate's own z, not the reference z: no effect, since the Block,
Markerless and base quads read only x, y of the translation (0x004E63B2, 0x008772D2).

| Item | Kind | Address | Closing job |
| --- | --- | --- | --- |
| Owned parts: corner sort, minAreaRect, per-class quads, size slot | H (M13-023, M13-024, M13-025, M11-105) | as in those records | settle those first |
| Pose tree (GetWithRespectToRoot 0x004DF628, GetWithRespectTo), the GetParent root fault, the rotation matrix, slot dispatch, the walk and filter (FindLocatedObjectHelper 0x0061EB78: originMode 0, first match in (origin, family, type, ID) order) | H (M11-066, M11-076, M11-065/M11-067, M11-088, M11-004) | 0x004DF628, 0x008453D4, 0x0084AAE0, 0x0061EB78 | their settlement (no queue rows) |
| Root reference pose (Finding 3): the engine reads null at 0x008452BA via 0x0062BA30; BlockWorld returns false silently, Neighbor uses FindRoot and can find a candidate; no M1-034 log in either | IC (unresolved and code doc contradicted) + V (missing M1-034 diagnostic) + B (minor: the two implementations disagree) | 0x0062BA2C..0x0062BA30, 0x008453D4..0x0084541A, 0x0084529C..0x008452BA | NEW ROOT-REFERENCE-M1034-1 |
| Two implementations of 0x0062601C (BlockWorld helper and Neighbor), no differential test; Neighbor's no-filter default is {2,1} (every caller passes a filter, per the census) | V | 0x0062601C, 0x0062B9FC | NEW FIND-ON-TOP-TWIN-DIFF-TEST-1 (or merge the two) |
| The authority omits originMode 0 (0x0061EBF2..0x0061EC2A), the first-match walk order and the per-caller filters (C1/C2 6/7 + ID, C4/C6/C7 {2,1} at 0x00C650A0/0x00C77D98, C8 predicate 0x00629BAE), all in INVENTORY-NOTE-M13-007-CALLER-CENSUS-1.md. Its sentence "Only the Block ... override is read; the other classes are M13-023" is stale (M13-025) | IC | as stated | manager, next approval |
| C1 revisit guard (BlockWorld.cs:2595) is silent and has no engine counterpart. Unreachable: each step raises z by at least dimRef/2 + dimCand/2 - 15 > 0 for every family C1 admits (6/7 excluded). Record it so; no code | IC | 0x00507258..0x0050731C | manager |
| Host extension D1 (BlockConfigurationManager.BuildTallestStackForObject, tagged at BlockConfigurations.cs:327, feeds ConfigurationSeen) | H (decision) | no engine site | existing HOST-CONFIG-SAME-ROOT-1 (operator) |
| Tests: unit except FreeplayTests (production; only P4 false for a charger). No production test of a found result (C10 attach with a cube on top, C11 refusal, C8 follow) | V | n/a | NEW FIND-ON-TOP-PRODUCTION-TEST-1 |

Stale: "NOT BUILT: cv::minAreaRect (M13-023, RECOVERABLE_GAP)", "LABELLED STAND-IN THAT DIFFERS FROM THE ENGINE", "Footprint.GetBoundingQuadXY
itself remains a visible stub", "Not the whole path until minAreaRect is read", "STILL OPEN: the heights and dimensions ... come from the
legacy finder" (built by the re-verify fix), "INVENTORY CORRECTION FLAGGED: the authority's 'refPose is a copy ...'" and "The authority
text's 'refPose is a copy' remains for the manager" (the authority already reads GetWithRespectToRoot, corrected 2026-11-13), "OPEN
(manager): that per-class size ... is not in this record's evidence" (now M11-105), "OPEN: CubeGeometry.SizeOf returns 44 for markerless"
(replaced on this path by MIN-AREA-RECT-FOOTPRINT-1), "a non-cube candidate its walk reaches throws NativeConfigurationUnavailable ...
that boundary is M13-023's" (Neighbor now uses the slots), "The readiness-list example (C1 no filter) is stale", and the
root-reference sentence (Finding 3, contradicted rather than stale).
Proposed: **EQUIVALENT_IMPLEMENTATION** (libm only through M13-023/M13-024; the helper and lambda use none).

## M12-012: IMPLEMENTATION_GAP

Title: "What makes a cube a valid bottom to stack on: resting flat within ten degrees".
Unresolved (key quotes): "Still open: the on-top search is M13-007/M13-023; IsRestingFlat (BlockWorld, Math.Acos) and
Radians::Rescale are double where the engine is float; CanPickUpObject/CanPickUpObjectFromGround have no C# caller"; "The location
ManipulationBehaviors.cs holds no M12-012 tag any more: the manager should repoint the location"; CAN-INTERACT-ROOT-CHECK-1
"Stack-only steps with no engine counterpart ... (a) false when _vision.History.Latest is null; (b) with Engine.Robot null the robot
pose is a parentless copy ... Still open: an object whose pose node is null reaches IsRestingFlat ... (reported MISSING)".

Re-read: IsRestingFlat 0x0087751C..0x00877562: GetWithRespectToRoot 0x00877528, GetTransform 0x0087752E, GetRotationMatrix
0x00877536, GetRotatedParentAxis<Z> with its out value 0x00877544, clear sign 0x0087754A, acosf 0x0087754E, Radians 0x00877556,
operator< 0x0087755C. The stack IsRestingFlat (BlockWorld.cs:466-473) follows it in f32 (MathF.Acos; f32 Radians,
NativeRadians.cs:12-14 and 35).

| Item | Kind | Address | Closing job |
| --- | --- | --- | --- |
| The C11 on-top find | H (M13-007) | 0x0063C730 | M13-007 first |
| IsRestingFlat's matrix; IsPoseTooHigh's dimension (CubeGeometry.DimInParentFrameZ; cubes only behind the family 1/2 gate 0x0063C65E..0x0063C666, so 44 is right); the pose tree; the carried ID | H (M11-065/M11-067, M11-105 item 2, M11-066, M12-008) | 0x00877536, 0x0087797A, 0x0063C6B6, 0x0063C686..0x0063C698 | their settlement |
| Stack-only gates (a) History.Latest null -> false, (b) Engine.Robot null -> parentless copy (Docking.cs:630-632). The engine has neither (Robot::GetPose 0x004EA398 always). Production reach of (a) UNKNOWN; (b) is host/test only | IC (decision, or evidence of unreachability) | 0x0063C6A4..0x0063C6BE | manager; evidence can come from FIND-ON-TOP-PRODUCTION-TEST-1 |
| Null pose node (Finding 4): an engine crash point (0x0084AAEA). The stack throws in Pose3d.RequireNode; whether that is a visible M1-034 skip naming M12-012 is UNKNOWN. No production writer of a null node shown | IC (record the crash point) + V | 0x00877528, 0x004DF66A, 0x008459B0, 0x0084499A, 0x0084AAEA | ROOT-REFERENCE-M1034-1 (same M1-034 handling) |
| Two implementations of CanInteractWithObjectHelper: Docking.cs:624-642 (BlockWorld helper) and NativePickupTargetCache.cs:155-166 (Neighbor; also tagged M12-012; throws for a null carry owner). No differential test | V | 0x0063C654..0x0063C794 | FIND-ON-TOP-TWIN-DIFF-TEST-1 |
| Tests: RestingFlatTests, M12RVisBuildTests, CanInteractRootCheckTests, M13FindOnTopSameRootTests: unit. StackBlocksNativeTests / SparksStackNativeTests exercise CanStack through the behaviours but are not named in the record (their reach labels not checked) | V | n/a | FIND-ON-TOP-PRODUCTION-TEST-1 |

Stale: "IsRestingFlat (BlockWorld, Math.Acos) and Radians::Rescale are double" (both f32 now); "the manager should repoint the
location" (the location is Docking.cs); "CanPickUpObject/CanPickUpObjectFromGround have no C# caller" (CanPickUpObject is in
Docking; FromGround is NativePickupTargetCache.CanInteract(.5, 10, 0)); "with M13-023's stand-in for tilted/non-cube pairs" in the
Docking.cs:609-615 doc (the stand-in is deleted; cleanup).
Proposed: **EQUIVALENT_IMPLEMENTATION** (acosf at 0x0087754E is bionic libm).

---

## Dependency order (ownership rule)

Outside the group, each still IMPLEMENTATION_GAP and with no queue row: M11-065 (operator*, GetRotationMatrix R17/R18),
M11-067 (Rodrigues inside Renormalize), M11-066 (pose tree), M11-076 (GetParent root fault), M11-004 (the
FindLocatedObjectHelper walk 0x0061EB78), M11-088 (slot +0x50 dispatch), M12-008 (carried ID).

1. M13-023: needs only the libm policy (already answered).
2. M13-024, after M13-023 (GetBoundingQuad<float> calls the sort and the fallback).
3. M11-105, after M11-065/M11-067; independent of 1-2.
4. M13-025, after M13-024, M13-023, M11-065/M11-067, M11-066, M11-088.
5. M13-007, after M13-025, M13-024, M13-023, M11-105, M11-066, M11-076, M11-004, M11-088.
6. M12-012, after M13-007, M11-105, M11-065/M11-067, M11-066, M12-008.

## Ranking (least remaining first)

1. **M13-023**: built and matching on the re-read; only decision records, two ICs and a Claude check.
2. **M13-024**: one unread body (convexHull / incrementing Sklansky), the +-0 sort order, the catch-scope UNKNOWN, two ICs; then the Claude check.
3. **M11-105**: the reader census and the writer search, two decision records; waits on M11-065/M11-067.
4. **M13-025**: the second markerless producer, the Ramp/HumanHead reach proof; waits on 1-2, M11-065/066/067, M11-088.
5. **M13-007**: its own path matches; remaining are the root-reference fault, the twin-implementation test, the IC list,
   HOST-CONFIG-SAME-ROOT-1, a production test, and all of 1-4 plus M11-004/M11-076.
6. **M12-012**: few items of its own (the stack-only gates decision, the null-node M1-034), but it inherits all of M13-007.

## Proposed jobs

| Id | Scope | Records |
| --- | --- | --- |
| MIN-AREA-RECT-HULL-READ-1 (NEW) | Read convexHull 0x00039174..0x000396C8 and the incrementing Sklansky_ against MinAreaRectF; settle the +-0 sort order and whether any throw besides orientation != 0 is reachable | M13-024 |
| FOOTPRINT-CORE-CLAUDE-CHECK-1 (NEW) | Strong verification of the sort tree, R1-R3 and the minAreaRect port; settle M13-023, then M13-024 | M13-023, M13-024 |
| SIZE-SLOT-READERS-CENSUS-1 (NEW) | For each of the ten GetDimInParentFrame<Z> readers, which classes reach it; other writers of +0xF0/+0x88; finish the Ramp/HumanHead construction proof (users of GOT 0x0103E8C0/0x0103E950) | M11-105, M13-025 |
| MARKERLESS-QUAD-SINGLE-PATH-1 (NEW) | Route BlockWorld.MarkerlessQuad/QuadFor (BlockWorld.cs:1596-1603) through the slot +0x50 markerless path (0x00502C32), or prove them identical | M13-025 (M11-062/M11-091) |
| ROOT-REFERENCE-M1034-1 (NEW) | Make the root-reference P1 fault (0x0062BA30 -> 0x008452BA) and the null-node IsRestingFlat fault (0x0084AAEA) M1-034 skips with a visible log, the same in both implementations | M13-007, M12-012 |
| FIND-ON-TOP-TWIN-DIFF-TEST-1 (NEW) | Differential test: BlockWorld helper vs Neighbor, and Docking vs NativePickupTargetCache CanInteract, over cube/charger/markerless/tilted/other-root cases | M13-007, M12-012 |
| FIND-ON-TOP-PRODUCTION-TEST-1 (NEW) | A production-entry test that observes a found-on-top result (C10 attach with a cube on top, or C11 refusal) | M13-007, M12-012, M13-025 |
| HOST-CONFIG-SAME-ROOT-1 (existing, operator) | Decide the host extension D1 | M13-007 |
