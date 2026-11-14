# M13-007-CALLER-CENSUS-1 census (extractor, 2026-11-14)

Engine side disassembled for this job from resources/lib/armeabi-v7a/libcozmoEngine.so (capstone Thumb, PLT names from .rel.plt); re-analysis/decomp
is empty. A Thumb-2 BL/BLX scan of .text for PLT 0x004A7144 / body 0x0062601C finds exactly the eleven sites. This re-checks the earlier census
.scratch/FIND-ON-TOP-SAME-ROOT-1-census.md against the CURRENT code (the C9/C10/C11a fixes have landed since).

## Filter fields (FindLocatedObjectHelper 0x0061EB78, filter copy at sp+0x38)
- onlyLatest +0x6C: 0x0061EB98 ldrb [sp,#0xa4]. originMode +0x6D: 0x0061EBF2 ldrb [sp,#0xa5]; mode 0 = origin == [[robot+0x294]+0x10] (0x0061EBFA..0x0061EC24).
- ignoreOrigins +0x48 (root [sp,#0x84], 0x0061EC0C); allowedOrigins +0x54 (size [sp,#0x94], 0x0061EC30).
- ignoreFamilies +0x30 (root [sp,#0x6c], 0x0061EC60); allowedFamilies +0x3C (size [sp,#0x7c], 0x0061EC7A); signed compares.
- ignoreTypes +0x18 (root [sp,#0x54], 0x0061ECAC); allowedTypes +0x24 (size [sp,#0x64], 0x0061ECC8).
- ignoreIDs +0x00 (root [sp,#0x3c], 0x0061ED12); allowedIDs +0x0C (size [sp,#0x4c], 0x0061ED30); key vs obj[+0x18].
- predicate list +0x60 (first node [sp,#0x9c]; calls 0x0061ED50..0x0061ED70).
- Families 6/7 = MarkerlessObject/CustomObject (EnumToString table 0x01033F50, earlier census). The tables 0x00C650A0 and 0x00C77D98 were dumped: int32 [2, 1].
Every caller builds the default filter inline (sets empty, list empty, strh 0 over +0x6C/+0x6D) and adds only what is listed below.

## Table

| site | engine function | tol | onTop | filter (engine) | result use | stack caller | stack args | verdict | live? |
|---|---|---|---|---|---|---|---|---|---|
| C1 0x00507258 | ObjectPoseConfirmer::MarkObjectUnknown 0x00507128 (filter r4 = sp+0xF0, 0x00507172) | 15 (0x00507252 movt r2,#0x4170) | 1 (0x0050724C) | default (strh r6,[sp,#0x15c] 0x00507212) + ignoreIDs {current r7 ID} (0x00507216..0x00507220, tree r4+0) + ignoreFamilies {6,7} (0x00507224..0x00507240, r8 = r4+0x30 set at 0x005071AA); rebuilt each round with only the current ID | upward walk; non-carried result joins the ID set (0x005072C6); carried: sWarningF 0x00507288, not added, still current (0x0050728E); loops while r7 nonzero (0x0050731C) | BlockWorld.cs:2584 -> private helper BlockWorld.cs:3039-3057 | 15 (StackToleranceMm, BlockWorld.cs:808), onTop true, skip = current ID, ignoreMarkerlessAndCustom default true, current origin, SharesReferenceRoot (P1), first in _located key order | MATCH. The readiness line "BlockWorld.cs:2595 passes no filter" is stale: no explicit filter argument, but the helper defaults are the engine filter. The stack's revisit guard (BlockWorld.cs:2586) has no engine counterpart and fires only on a pose cycle where the engine would loop forever (M1-034 hang rule) | live: MarkObjectUnobserved 0x0050702E, FlipBlockAction::CheckIfDone 0x0055F186; stack BlockWorld.cs:2454, 2550, FlipBlockAction.cs:130, NativeFlipBlockRunner.cs:56 |
| C2 0x00507664 | MarkObjectDirty 0x005075A4, propagate branch (filter r4 = sp+0x14) | 15 (0x0050765A) | 1 (0x00507658) | default (strh 0x0050761A) + ignoreIDs {obj} (0x00507628) + ignoreFamilies {6,7} (sb = r4+0x30, 0x0050762C..0x0050764C) | carried: sChanneledInfoF 0x00507694; else MarkObjectDirty(found, 1) 0x005076C2 | BlockWorld.cs:2521 | 15, true, skip obj, 6/7 excluded, current origin, P1 | MATCH | UNREACHABLE both sides: the external calls pass r2 = 0 (movs r2,#0 at 0x0053413A, 0x00534948, 0x0062354E, 0x0063135E, 0x0063BB48); stack callers BlockWorld.cs:3298, 3626, 3653, 3662 pass false; only the recursion passes true |
| C3 0x0055526E | PlaceRelObjectAction::GetCompletionUnion 0x0055515C (filter fp = sp+0x40) | 15 (0x00555268) | 1 (0x00555262) | default only (0x0055521C..0x0055525C, strh r7,[sp,#0xac]) | ID walk of at most 5 (0x005552BC..0x005552C6); Set_objectInteractionCompleted 0x0055532A, then overwritten by the second Set {[this+0x7C], count 1} (0x0055532E..0x0055533E) | none (M12-050) | n/a | no counterpart; no effect (const helper, result overwritten) | reachable, no effect |
| C4 0x005BA136 | RollBlockHelper::UnableToRollDelegate 0x005BA040 (filter sp+0x30, 0x005BA086) | 30 (0x005BA12C movt r2,#0x41f0) | 1 (0x005BA12A) | default (strh r6,[sp,#0x9c] 0x005BA0F2) + allowedFamilies {2,1} from 0x00C650A0 (r4 0x005BA0AE/0x005BA0C4; inserts 0x005BA0FE..0x005BA10E; __assign_multi into sp+0x30+0x3C at 0x005BA116) | used when the target is not valid for interaction (fp != 1 at 0x005BA13C -> 0x005BA264; r5 tested 0x005BA268) | NativeRollBlockHelper.cs:62-63 -> NativeConfigurationGeometry.Neighbor | 30f, true, AllowedFamilies {2,1}, OriginMode default InRobotFrame (BlockWorld.cs:186), IgnoreIds += obj | MATCH | live |
| C5 0x005C99C0 | BehaviorStackBlocks::UpdateInternal 0x005C9844 (filter r7 = sp+0x14) | 15 (0x005C99B8) | 1 (0x005C99B4) | default only (0x005C994C..0x005C99AE, strh r1,[sp,#0x80]) | reference = object of ID fp+0x12C; result compared with object of ID fp+0x124 (0x005C9A1E); reached only when the fp+0x12C ID is not in its valid set (0x005C98C6) | NativeStackBlocksBehavior.cs:92 | Neighbor(bottomObject, true, 15f, new BlockWorldFilter()) | MATCH (which field is top/bottom is M15-076's, not re-read) | live |
| C6 0x0061929E | StackOfCubes::BuildTallestStackForObject 0x0061907C, UP (filter sp+0x40, r6 0x00619280) | 30 (0x0061928C) | 1 (fp = 1, 0x00619288) | default (strh r6,[sp,#0xac] 0x0061924A) + allowedFamilies {2,1} from 0x00C77D98 (0x00619216/0x00619228; inserts 0x00619254..0x00619264; assigned to sp+0x40+0x3C via [sp,#0x20] = r5+0x3C, 0x00619198/0x00619270) | at most 10 links (0x006192D6); stop at null or found ID == [carry+8] when not -1 (0x006192A8..0x006192B4); bound gives sErrorF 0x006192F6 | NativeConfigurationGeometry.cs:118-125 BuildStack -> Neighbor(current, true) | default tol 30f, true, Filter() {2,1} | MATCH (native) | live (native producer) |
| C7 0x00619358 | same function, DOWN | 30 (0x00619344) | 0 (str r6,[sp], r6 = 0, 0x00619342/0x00619356) | same filter object | at most 10 (0x00619382); stops only at null | NativeConfigurationGeometry.cs:126-132 Neighbor(current, false) | 30f, false, {2,1} | MATCH (native) | live |
| C8 0x0062190C | BlockWorld::UpdatePoseOfStackedObjects 0x00621794 (filter sp+0x70, 0x006217B6) | 15 (0x00621908) | 1 (0x006218FC) | default (strh sl,[sp,#0xdc] 0x006218B0) + ignoreIDs {obj ID, r7 = sb+0x14 at 0x0062184C} (0x006218BC) + ignoreFamilies {6,7} (fp = filter+0x30 at 0x00621814; 0x006218C0..0x006218DC) + predicate obj[+0x1C]+100 < GetLastImageTimeStamp (0x006218E4; lambda 0x00629BAE adds r1,#0x64; cmp; movlo) appended 0x006218F8; the reference is a clone (vtable[0], 0x00621854) given the old pose/state (InitPose 0x00621860) | skip if carried (0x00621926) or its ID is in the BlockWorld+0x68 list (0x00621930..0x0062194C); GetWithRespectTo(top, clone) 0x0062196E; PreComposeWith 0x00621990 | BlockWorld.cs:2964-2966 | 15, true, ignoreId obj.ObjectId, extra c => LastObservedTimestamp+100 < lastImage (unchecked uint), 6/7 excluded (default), referenceOrigin obj.OriginId, current origin, P1 | MATCH. lastImage is read once before the loop (BlockWorld.cs:2948), the engine per entry (0x006218E4): same value within one call | live (any frame with pose changes, 0x006250D0) |
| C9 0x00625E1A | BlockWorld::ClearLocatedObjectHelper 0x00625C78 (filter r6 = sp+0x10) | 15 (0x00625E10) | 1 (0x00625E0C) | default only (0x00625DAA..0x00625E06, strh r0,[sp,#0x7c]) | recursive ClearLocatedObjectHelper(found) 0x00625E7A | BlockWorld.cs:2693 | 15, true, skip obj, ignoreMarkerlessAndCustom false, current origin, P1 | MATCH (the earlier 6/7 mismatch is fixed) | live (DeleteLocatedObjects 0x0061D1CE) |
| C10 0x00632F14 | CarryingComponent::SetObjectAsAttachedToLift 0x00632CC4 (filter sl = sp+0x20) | 15 (0x00632F0C) | 1 (0x00632F08) | default only (0x00632E86..0x00632F02, strh r7,[sp,#0x8c]) | top ID to carry+0x14 etc. (M12-008) | LiftGeometry.cs:146 -> BlockWorld.FindObjectOnTopOrUnderneathDefaultFilter (BlockWorld.cs:3066) | 15 (LiftGeometry.cs:83), true, default (no 6/7), current origin, P1 | MATCH (the earlier all-origins mismatch is fixed) | live (each successful pickup) |
| C11 0x0063C730 | DockingComponent::CanInteractWithObjectHelper 0x0063C654 (filter r8 = sp+8) | 15 (0x0063C728) | 1 (0x0063C724) | default only (0x0063C6C0..0x0063C71E, strh r2,[sp,#0x74]) | returns found == null | (a) Docking.cs:622 FindObjectOnTopOrUnderneathDefaultFilter; (b) NativePickupTargetCache.cs:164 Neighbor | (a) 15 (RestingOnToleranceMm, BlockConfigurations.cs:66), true, default; (b) 15f, true, new BlockWorldFilter() | MATCH for the find in both; caller-side difference in (a), D-1 below | live |

## Differences (none in the eleven sites' tolerance, onTop or filter)

D-1 (boundary: M12-012, the C11 caller, not the find). The engine's CanInteractWithObjectHelper, BEFORE the find, calls
GetWithRespectTo(obj.GetPose(), robot pose (bl 0x004EA398), out) and returns false when it fails (0x0063C6A2..0x0063C6BE: cmp r0,#1; bne 0x0063C69A,
r0 = 0). The stack's Docking.CanInteractWithObjectHelper (Docking.cs:615-623) has no such step; its doc comment says it "always succeeds with one pose
origin", which is no longer true. For an object whose root differs from the robot's: the engine returns false; the stack's find finds nothing (P1
rejects every candidate), so the helper returns true, and CanStackOnTopOfObject / CanPickUpObject (Docking.cs:588, 602) then call
obj.NativePose.WithRespectTo, which throws InvalidOperationException "MISSING: M11-066 caller has not handled unrelated pose roots" (Geometry.cs:402)
for a native pose. Reach: CubeGameBehaviors.cs:965-972 iterates World.LocatedObjects, which is EVERY origin (BlockWorld.cs:1645-1648), filtered by
PoseState Known; DockActions.cs:1044 passes the action target. Whether a Known cube of a non-current origin reaches it is UNKNOWN (the engine's
FindFreeCubeToStackOn 0x005E010C iteration was not read). NativePickupTargetCache.CanInteract (NativePickupTargetCache.cs:161-163) has the step.
Exact change if taken under M12-012: in Docking.CanInteractWithObjectHelper, after the carried compare and before the find, compute
Pose3d.GetWithRespectTo(obj.NativePose, robot main pose, out) and return false when it fails (0x0063C6A2..0x0063C6BE); CanStackOnTopOfObject and
CanPickUpObject then use that out pose (the engine callers use the helper out pose) instead of WithRespectTo.

D-2 (stack host extension D1, no engine call site). BlockConfigurationManager.BuildTallestStackForObject (BlockConfigurations.cs:280-298), via
BuildLocked (:224-231), wired live at ManipulationSystem.cs:67 and invoked at BlockWorld.cs:1898: every origin's located cubes (:226, :282), cubes
rather than families {2,1}, no P1 and the public Pose (FindObjectOnTopOrUnderneath :328-355), down first, MaxStackHeight 8 (:70) not 10, an
ids.Contains stop, no carried stop, no sErrorF. It cites 0x0061929E/0x00619358, but the engine's C6/C7 are reproduced by
NativeConfigurationGeometry.BuildStack. It feeds ConfigurationSeen (BlockConfigurations.cs:221) and OnConfigSeenBehavior.Recent
(CubeGameBehaviors.cs:838). Manager decision: a recorded host extension, or routed to BuildStack.

D-3 (minor). Neighbor throws NativeConfigurationUnavailable for a candidate with a null pose node (Require, NativeConfigurationGeometry.cs:19, 104),
where GetWithRespectTo returns false (0x00845A46..0x00845A58). Whether a located object can have a null node in the stack was not checked.

## Notes
- The readiness example is stale: C1 is BlockWorld.cs:2584 and matches through the helper defaults.
- M13-007's authority still says "refPose is a copy of obj->GetPose()" (contradicted by 0x00626048 bl 0x004DF628; already in unresolved) and does
  not list the per-caller filters; the table above gives each with its addresses.
- Only C3 (no effect, M12-050) has no stack counterpart; C2 is unreachable on both sides.
