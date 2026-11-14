# GET-SIZE-SLOT-RECORD-1: inventory note (records only; Claude worker, 2026-11-13)

Every address below was re-read with capstone/lief on resources/lib/armeabi-v7a/libcozmoEngine.so (scratch scripts
outside the repo). The decomp tree is empty and was not used. Vtable slots were read through their dynamic relocations.

## 1. The per-class size slot (Vision::ObservableObject vtable +0x2C)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| S0 slot identity | Vision::ObservableObject's own vtable (vptr 0x01038194) has __cxa_pure_virtual at +0x2C (0x010381C0) and GetBoundingQuadXY at +0x50 (0x010381E4). In the subclass tables the next slot (+0x30) is GetSameDistanceTolerance (Charger 0x0101E1F4, Markerless 0x0101F348). The name "GetSize" is inferred: no symbol names the slot. It returns a pointer to a Point3f. | vtable 0x0103818C; relocations at 0x010381C0, 0x010381E4 | NEW | EXACT_SOURCE (slot and returns); the name is an inference |
| S1 GetDimInParentFrame<'Z'> | bl GetRotatedParentAxis<'Z'> (0x0055779C); adds r0,#3; cmp r0,#6; tbb. The table bytes at 0x005577AA are [4,10,16,16,16,10,4]. Axis +-3 goes to 0x005577B2 (ldr r1,[r0,#0x2c] at 0x005577B4; blx r1; adds r0,#8, so z). Axis +-2 goes to 0x005577BE (slot at 0x005577C0; +4, so y). Anything else goes to 0x005577CA (slot at 0x005577CC; +0, so x). Then ldr r0,[r0] at 0x005577D2. | 0x00557794..0x005577D4 | NEW (M12-012 and M12-033 cite the axis mapping) | EXACT_SOURCE |
| S2 Neighbor candidate (P5) | Only when Quadrilateral::Intersects (0x0062BA8C) is non-zero (cmp r6,#0 at 0x0062BA98; beq 0x0062BB12 at 0x0062BAA2): the rotation matrix of the candidate pose relative to the root (0x0062BAA6..0x0062BAAE), then blx GetDimInParentFrame<'Z'> at 0x0062BAB6 on the candidate (r0=r5). The result is used in the height test 0x0062BAC2..0x0062BB0E. | 0x0062BA8C..0x0062BB0E | NEW | EXACT_SOURCE |
| S2b Neighbor reference (H4), missing from the queue row | FindObjectOnTopOrUnderneathHelper calls blx GetDimInParentFrame<'Z'> at 0x00626070 on the REFERENCE object (r0=r4) with the re-rooted refPose rotation (0x0062605E..0x0062606C). targetZ = refPose.z + dim*(onTop ? +0.5 : -0.5) at 0x0062607E..0x006260A0. The reference's size comes from slot +0x2C too. | 0x00626060..0x006260A0 | NEW | EXACT_SOURCE |
| S3 GetObstacles predicate (G6) | GetPose, then GetTransform (0x0062C1E4/0x0062C1E8); ldr r0,[r4]; ldr r1,[r0,#0x2c]; blx r1 (0x0062C1EE..0x0062C1F4); Rotation3d::operator*(Point3f) at 0x0062C1FC with the own-pose rotation (not re-rooted); rz = [sp+0x2C] (0x0062C224). The centre z is re-rooted (bl 0x004DF628 at 0x0062C20C; z at 0x0062C216). top/bottom = zc +- 0.5*rz (0x0062C220..0x0062C242), and rz is not abs'ed. | 0x0062C1D4..0x0062C27E | NEW (M12-036 owns the predicate, not the size) | EXACT_SOURCE |
| S4 Block / ActiveCube | Block's Vision sub-vtable (typeinfo at 0x0101DBBC, vptr 0x0101DBC0): +0x2C at 0x0101DBEC = 0x004E3831. ActiveCube's (vptr 0x0101CDEC): +0x2C at 0x0101CE18 = 0x004E3831. Thunk 0x004E3830: ldr r1,[r0]; ldr r1,[r1,#-0x34]; add r0,r1; adds r0,#0x88; bx lr. The non-thunk getter is 0x004E382C (adds r0,#0x88; bx lr). Block::Block C2 0x004E5F50 copies 12 bytes from LookupBlockInfo(type)+0x10 into this+0x88 (0x004E5F7C..0x004E5F8C). The values 44,44,44 are M11-003's (movt #0x4230 confirmed at 0x004E4CD6, 0x004E4D90, 0x004E4E46, 0x004E4EFA). The info+0x10 to builder stack-slot mapping was not re-traced. | 0x004E382C..0x004E383A; 0x004E5F7C..0x004E5F8C | NEW (values: M11-003) | EXACT_SOURCE for the getter; the values are M11-003's |
| S5 Charger | Charger sub-vptr 0x0101E1C4 (typeinfo at 0x0101E1C0): +0x2C at 0x0101E1F0 = 0x004EA673. 0x004EA672: ldr r1,[r0]; ldr r1,[r1,#-0x34]; add r0,r1; adds r0,#0xf0; bx lr. Charger::Charger C1 0x004E9CB0 stores 96 (0x42C00000) at +0xF0 (0x004E9D12/0x004E9D18), 80 (0x42A00000) at +0xF4 (0x004E9D22/0x004E9D26) and 31 (0x41F80000) at +0xF8 (0x004E9D2E/0x004E9D32). Other writers to +0xF0 were not searched. | 0x004EA672..0x004EA67C; 0x004E9D12..0x004E9D32 | NEW | EXACT_SOURCE (ctor values) |
| S6 MarkerlessObject | vptr 0x0101F318: +0x2C at 0x0101F344 = 0x00502DD1. 0x00502DD0: adds r0,#0x58; bx lr. The C2 ctor 0x00502958 sets family 6 (0x00502978/0x0050297C) and type at +0x4C, calls GetSizeByType(type) (0x00502994) and copies 3 words into this+0x58 (0x005029A2..0x005029B0). | 0x00502DD0; 0x00502958..0x005029B0 | NEW (the values are also in M11-091's evidence) | EXACT_SOURCE |
| S7 GetSizeByType | It builds a function-static map<ObjectType,Point3f> once under a guard (0x005026F6..0x0050270C). The map holds three entries: 0xE (10,10,50), 0xF (20,40,50) and 0x10 (20, 54.2, 67.7). The constants are 0x41200000, 0x42480000, 0x41A00000, 0x42200000, and 0x4258CCCD/0x42876666 at 0x00502756..0x00502768. The emplace loop is 0x0050276A..0x0050277A. A hit returns node+0x14 (0x00502860). A miss calls sErrorF("MarkerlessObject.GetSizeByType.UndefinedType", "No size defined for type %s (%d).") (strings 0x005028E4/0x00502914; call 0x005027E8). It then sets the error flag and maybe calls sDebugBreakOnError (0x0050280E..0x00502822), and returns a static (10,10,10) (0x00502840..0x00502852). | 0x005026EC..0x0050286E | NEW (also in M11-091's evidence) | EXACT_SOURCE |
| S8 other +0x2C readers (outside this scope; named only) | A BL/BLX scan for PLT 0x004AB9BC and 0x00557794 found these GetDimInParentFrame<'Z'> callers: IDockAction::SetupTurnAndVerifyAction 0x00552028; BehaviorCheckForStackAtInterval 0x005D757A; BehaviorBuildPyramidBase 0x005DD1A2/0x005DD7E8; BehaviorAcknowledgeObject 0x00603914; PyramidBase 0x006178B8/0x00617B7E/0x00617D7E/0x00617DAE; CheckForCollisionWithRobot 0x00625A00; GetZRotatedPointAboveObjectCenter 0x008775AE; IsRestingAtHeight 0x008776A8; IsPoseTooHigh 0x0087797A. GetDimInParentFrame<'X'> 0x0055770C and <'Y'> 0x00557750 were not read. Direct [vptr,#0x2c] loads other than S3 were not censused. | the scan | NEW | RECOVERABLE_GAP (read the <X>/<Y> bodies and their callers) |

### Stack side (for the location field; not judged)

- ObservableObject.ObservableSize2C is at cozmo-stack/src/Cozmo.Robot/Vision/ObservableMapSlots.cs:12-22. Line 17 is
  tagged "fidelity: M13-025, M12-036"; the class-level tag at line 6 is M11-088. Its readers are
  NativeConfigurationGeometry.CandidateSize (NativeConfigurationGeometry.cs:66) and PreActionPose.cs:737 (GetObstacles).
- Findings for a later build (not fixes):
  - S2b: Neighbor's reference dimension uses Dimensions() (NativeConfigurationGeometry.cs:28-34: CubeGeometry.SizeOf,
    which throws for a non-cube). The engine reads the reference's slot +0x2C at 0x00626070.
  - S7 default: ObservableSize2C returns null for a markerless type with no map entry. The engine logs and returns
    (10,10,10). Only 0xE, 0xF and 0x10 are ever constructed (M11-091), so this cannot be reached with engine inputs.
  - S8: those readers use CubeGeometry.SizeOf (44 for markerless); M13-007 and M13-025 already record this as open.

### Recommendation: a NEW record, not an extension of M13-025

The ownership rule (a settled record owns its whole path) decides it. Slot +0x2C is one shared dispatch read on the
paths of several records:
- M13-007: H4 at 0x00626070 and P5 at 0x0062BAB6;
- M12-036: the G6 predicate at 0x0062C1F0;
- M12-012 and M12-033: IsPoseTooHigh 0x0087797A and GetZRotatedPointAboveObjectCenter 0x008775AE.

M13-025's title and authority are about the +0x50 footprints. Extending it would leave M12-036, M12-012 and M12-033
deferring part of their paths to a footprint record. A per-slot record next to M11-088 (the +0x5C/+0x50 overrides) and
M11-091 (the +0x50 census) follows the existing pattern and lives in the same file.

The proposed record:
- **id:** M11-105. M11-104 is the highest M11 id, and neither M11-105 nor M13-042 appears in the manifest or
  QUEUE.md. M13-042 is the alternative if the manager wants the record in M13.
- **Approval:** M11-vision and M12-manipulation were INVENTORY_APPROVED on 2026-11-13 and M13-navigation on 2026-10-28.
  Either choice therefore needs an inventory change and a re-approval.
- **subsystem:** M11-vision.
- **title:** "ObservableObject vtable +0x2C (size) per class and its readers GetDimInParentFrame<Z> and the GetObstacles
  Z predicate".
- **authority:** libcozmoEngine.so 3.4.0-1204, rows S0-S7 above.
- **evidence:**
  - 0x0103818C (+0x2C pure, +0x50 GetBoundingQuadXY);
  - 0x00557794..0x005577D4 (tbb table at 0x005577AA);
  - 0x00626060..0x006260A0;
  - 0x0062BA8C..0x0062BB0E;
  - 0x0062C1EE..0x0062C242;
  - Block: slot 0x0101DBEC, ActiveCube slot 0x0101CE18 -> 0x004E3830 / 0x004E382C, ctor 0x004E5F7C..0x004E5F8C;
  - Charger: slot 0x0101E1F0 -> 0x004EA672, ctor 0x004E9D12..0x004E9D32;
  - Markerless: slot 0x0101F344 -> 0x00502DD0, ctor 0x00502994..0x005029B0;
  - GetSizeByType: 0x005026EC..0x0050286E, strings 0x005028E4 and 0x00502914.
- **live_path:** true. CanInteract 0x0063C730, StackBlocks 0x005C99C0, RollBlockHelper 0x005BA136 and BuildTallestStack
  reach S2/S2b; the IDockAction GetObstacles path reaches S3.
- **location:** cozmo-stack/src/Cozmo.Robot/Vision/ObservableMapSlots.cs (ObservableSize2C). Its tag currently names
  M13-025 and M12-036, not the new id.
- **status:** IMPLEMENTATION_GAP. The getter is built; S2b and the S8 census are open.
- **unresolved:**
  - the slot name GetSize is inferred;
  - other writers of Charger+0xF0 and Block+0x88 were not searched;
  - the X/Y bodies and the full list of +0x2C readers were not read;
  - an SDK CustomObject has no stack size (unreachable: SDK mode is excluded).

## 2. M13-007 authority correction

Current text (quoted): "the lambda 0x0062B9FC: refPose is a copy of obj->GetPose() (0x0062603E..0x00626048, the object's
own pose in its own parent frame)".

The source does this:
- 0x0062603E is blx GetPose and 0x00626048 is bl 0x004DF628, which is GetWithRespectToRoot. Its log key
  "Pose3d.GetWithRespectToRoot.Failed" is at 0x004DF738.
- If the pose IsRoot, it is assigned (0x004DF65E..0x004DF66A).
- Otherwise it calls FindRoot (0x004DF674) and then GetWithRespectTo(pose, root, out) (0x004DF67E). A failure logs
  sErrorF (0x004DF69C).

Corrected wording: "refPose = GetWithRespectToRoot(obj->GetPose()) (0x0062603E GetPose; 0x00626048 bl 0x004DF628: a root
pose is copied, otherwise FindRoot + GetWithRespectTo; a failure logs Pose3d.GetWithRespectToRoot.Failed). refPose is the
object's pose in its ROOT frame, so refPose.GetParent() in step 1 is the root and step 1 is a same-root test. refQuad
(0x0062604C..0x0062605C), the reference dimZ (0x00626070, slot +0x2C) and targetZ (0x0062607E..0x006260A0) all use this
root-frame pose."

Also stale in M13-007:
- **evidence[0]:** "adds +/-half to the parent Z (0x0062607E..0x006260A0)". The z is refPose's root-frame z (vldr
  [r0,#0x28] at 0x00626098 on the 0x004DF628 output at sp+0x94). Corrected: "adds +/-half to the root-frame Z of refPose".
- **authority:** "the candidate's z is set to refPose's z" is correct, but that z is root-frame.
- **Not stale:** the eleven callers and their tolerances. A BL/BLX scan to PLT 0x004A7144 finds exactly the eleven
  listed sites. movt #0x4170 is at 0x00621908, 0x0063C728 and 0x00632F0C; movt #0x41F0 is at 0x0061928C, 0x00619344
  and 0x005BA12C.

## 3. RollBlockHelper 0x005BA136 family filter

The function is RollBlockHelper::UnableToRollDelegate(Robot&) 0x005BA040:
- 0x005BA064 GetLocatedObjectByIdHelper(this+0xF0, family -1); a null result branches to 0x005BA216.
- 0x005BA082 IsObjectValidForInteraction(0, id).
- An inline default BlockWorldFilter is built at sp+0x30 (bytes +0x6C/+0x6D = 0, strh at 0x005BA0F2).
- A temporary set of ObjectFamily is filled from rodata 0x00C650A0 = {2, 1}: ldr r4,=0x6AAFD8 and add r4,pc at
  0x005BA0AE/0x005BA0C4, emplace loop 0x005BA0FE..0x005BA10E.
- That set is assigned with __assign_multi (0x005BA116) into filter+0x3C (add r0,r1,#0x3c at 0x005BA0E8). No predicate
  or other field is set.
- r2 = 30.0 (movt #0x41F0 at 0x005BA12C), [sp] = 1 = onTop (0x005BA12A/0x005BA130), then blx
  FindObjectOnTopOrUnderneathHelper at 0x005BA136.

filter+0x3C is the ALLOWED-family set. FindLocatedObjectHelper 0x0061EB78 copies the filter to sp+0x38 (bl 0x005C6A88
at 0x0061EB94). The +0x30 set is the ignore set: a hit skips the family (0x0061EC60..0x0061EC78). If the +0x3C set is
non-empty, the family must be in it; a miss branches to 0x0061EE64 (0x0061EC7A..0x0061EC88).

So the on-top search sees families {1, 2} only, every type, the current origin (originMode 0), 30 mm, onTop = true.

The stack matches. NativeRollBlockHelper.cs:62-63 builds AllowedFamilies {2,1} and calls Neighbor(target, true, 30f,
filter), and Neighbor clones that filter and adds the object's id to IgnoreIds (NativeConfigurationGeometry.cs:78).
Families, tolerance and onTop all agree. Nothing for the stack to change on this point.

## 4. Other stale citations on these records

- **M12-036 authority:** "rz = (R*size)[z] (0x0062C246..0x0062C27E)". That range is the four comparisons and the
  exclusion branch. Corrected: "rz = (R_ownpose * size from slot +0x2C).z (0x0062C1EE..0x0062C1FC, read at 0x0062C224);
  zc = re-rooted z (0x0062C200..0x0062C216); top/bottom = zc +- 0.5*rz (0x0062C220..0x0062C242, no abs); exclusion
  0x0062C246..0x0062C27E (NaN kept)".
- **M12-036 authority and evidence:** "0x00626BB5" (GetLocatedObjectBoundingBoxesXY). The entry is 0x00626BB4;
  0x00626BB5 is the Thumb-bit pointer.
- **M13-025 location:** "cozmo-stack/src/Cozmo.Robot/Manipulation/BlockConfigurations.cs". The live +0x50 path is
  ObservableObject.TryObservableBoundingQuad50 (ObservableMapSlots.cs:25) and NativeConfigurationGeometry.CandidateQuad.
  The manager decides.
- **M13-025 unresolved:** "it is built locally in Neighbor (CandidateSize)". CandidateSize now delegates to
  ObservableObject.ObservableSize2C (NativeConfigurationGeometry.cs:66), which GetObstacles (PreActionPose.cs:737) also
  uses.
- **QUEUE row:** "Block 0x005577B4". That is the slot load inside GetDimInParentFrame Z. Block's getter is 0x004E382C
  (virtual thunk 0x004E3830).
- **Side note (M11-003, outside scope):** its evidence cites "0x004E4D8E movt #0x4230". 0x004E4D8E is movs r1,#0; the
  movt is at 0x004E4D90.

## Open questions for the manager

1. Choose the id and subsystem: M11-105 in M11-vision (recommended) or M13-042. Either needs re-approval.
2. Does S2b (the reference-side size at 0x00626070) and the S8 readers go into the new record's live claim now, or stay
   in its unresolved census?
3. Should the items not searched (other writers of Charger+0xF0 and Block+0x88, the X/Y bodies) be settled now as a
   RECOVERABLE_GAP read?
