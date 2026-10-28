# SPARKS-STACK-1 independent changed-diff review

2026-10-28. Manager source acceptance9aaf44eb. Checker B; actual model/effort telemetry unavailable. This report is the authorized build review, separate from the frozen source-reader report. **PENDING coherent builder draft, frozen bytes, independent source/code review and independent regressions. No code PASS or test PASS yet.**

Source contract: `20261028-sparks-stack-1-read.md` SHA256 B719A854D016F0EC5A3E78D510C24464E90CCD7E79671D3FDC5B02118EA684E9; companion proposal B11301104F4753F08EA61418B431D0035E410129D92BEEA6C5C6BD66F0489EE8. Independent source-reader report `20261028-sparks-stack-1-read-codex-check.md` remains frozen. Reuse checked SparkedS/P/H/SF and shared pickup/DriveTo/ActionList recipients under their explicit boundaries. `re-analysis/jobs/trial-m13/SPARKS-STACK-1-CONTRACT-CORRECTION.md` and `SPARKS-STACK-1-COMPLETION-POLICY.md` record operator decisions. The latter normalizes ONLY four unused PlaceRel IDs in explicitselected2/3+targetlocated normal path; meaningful target/count1/false/result/type/tag/order remain exact. Normalized fields are COMPATIBILITY_POLICY, not EXACT_SOURCE.

The review must inspect actual configured factory/owner27→nativecache5/3→helpers/ActionList/docking/turn/highlift/recovery→objective40/optionalplain21A/needs43→Spark ending/getout/typed raw/local APIs and explicit-next cleanup. No manual alias, guessedclosest target, callback-only helper test, rawpayload padding outside the approved producer, new fidelity IDs or partial-record settlement. No production/test edits or tests while builder owns runner; all reviewer writes stay in this report/scratch. Full gate remains manager-owned.

## Current whole-record candidate before assessment

Current full title/status/evidence and unresolved are quoted now and must be reread after the build before any claim about settlement. This quote is baseline context, not a conclusion that the record is built.

```json
{
  "id": "M12-042",
  "title": "PlaceRelObjectHelper lifecycle: ctor, Init (pre-action check, DriveTo delegation), StartPlaceRelObject (3-attempt counter), RespondToPlaceRelResult, MarkFailedToStackOrPlace",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: ctor 0x005B8F00..0x005B8F82; Init 0x005B8FD0..0x005B90FE; Start 0x005B9168..0x005B930E; Respond 0x005B94C0..0x005B952E; MarkFailed 0x005B93A8..0x005B9460; invokers 0x005B956A, 0x005B95D6."
  ],
  "unresolved": "IMPLEMENTATION_GAP: port per the check file (F1-F8, the Respond table, MarkFailed with SetFailedToUse 1 or 2 and the 1-rad-about-Z pose). Dependencies: M12-031 (pre-action check), M12-022 (DriveToHelper), M8-011 (SmartDelegateToHelper), M15-024 (whiteboard). Unread: StopHelperWithoutCallback, IHelper::OnDelegateSuccess/Failure, DriveToHelper body.\n\nB-M12-3 (2026-10-15), built, awaiting strong verification: Bounded checked helper fields, Init precheck, Start counter/precheck/action tuple, Respond table and MarkFailed(1/2 with 1-rad-Z pose) built on the live AI helper component. Existing host action adapter posts the result on the behavior tick. MISSING: DriveToHelper construction/body at 0x005B9038/0x005B91E6 stops the delegate unit; no replacement drive/fake completion. Active-update virtual 0x005B93A4..0x005B93A8 lacks an adopted body row and only retains callback-written status with a visible MISSING. StartActingWithResponseAnim 0x005B927A wrapper remains MISSING, not certified by the async adapter. Existing M8-owned StopHelperWithoutCallback and IHelper delegate-success/failure implementations are reused without new recovery claims."
}
```

## Review and verification ledger

No reviewer tests/build processes have run in this job. Builder owns the runner. Draft and failed attempts must remain in the final audit; passing reruns do not establish the cause of a prior failure or source fidelity. At explicit frozen host release, independently hash every changed C# file before/after, run the saved meaningful affected filter plus any separately justified finite consumer extension, retain exact TRX counters/exit codes/logs, and stop build servers. Existing Sparks/Knock/pickup, manager/base/runtime, helper/ActionList, PlaceRel/M12 and publicNavigation shared consumers require coverage appropriate to the actual changed files. No full-suite run by B.

## Finite builder source question: defaults and marker setup

Current relevant record reread before relying on the shared path:

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
  ]
}
```

A and B independently checked this finite question after known.py preflights. Base005503AE STRH1 atC0 gives C0=1/C1=0; Place ctor clears onlyC0, leaving C1false. C4 defaultsnull and is the optional reaction-lock table pointer, consumed00551870..0055189A, not a wait duration. Actual Place vtable10222F0/addresspoint10222F8 slot44 cell102233C raw0055738B→Thumb0055738A MOVS0/BXLR returns +0f; Check005523BA..005523CA computes f32(now+delay) into90. Slot34 cell102232C→00557382 MOVS0/BXLR returns optional SECOND marker pointerNULL (secondarycode3), not DockingMethod. DockWithObject consumes actual byteBB; exact zero-offset Place sets it0.

With B9true, Init0055182A..0055185A indexes GetPreActionPoses output vector+BC by closest-index+C8 with stride0x1C, reads entry's marker pointer+4, null→03000002, otherwise copies markeru16. The observed-marker selection branch at00551960 belongs to B9false and must not be substituted for this source-required path. SetupTurnAndVerify0055186C precedes InitInternal00551C10 and actual childUpdate00551C1A. This resolves builder field/slot ambiguity without changing the frozen packet or official evidence. No claimed inventory contradiction; no whole M12-017 settlement. Source facts sent to C and parent; A prepares a tiny followup. Build verdict remains PENDING actual coherent diff and frozen regressions.

Finite empty-converter followup independently primary PASS after known.py005B717C: RespondToActionWithAnim checks converter+B8 at005B71DA; empty005B71E4 branches005B7208 and skips converter AND AI/response-director lookup. It copies the response callback005B720E, destroys/clears original callback through005B7228, destroys/clears converter through005B7242, then invokes copied callback005B7252 with saved ORIGINAL raw ActionResult reference and Robot. No response animation or AI lookup is required for this exact Place helper empty converter; actual raw-result callback/clear-before-invoke and reentry behavior remain required. This is not a direct-success bridge and does not settle generic nonempty-converter paths. Facts sent to A/C; frozen source packet unchanged.

SF1–SF3 source supplement independently checked/read and rehashed: `20261028-sparks-stack-1-source-followup.md` SHA256 `FFC28CBECBECEF783AF035CD1BF4F48EF539B2CCBB60147C3F6D0DE34EACDF83`, bounded source PASS. Direct42-byteC55A21 table read agrees onlyindex8=true; common dockActions lock after derivedInit success + preparedUpdate0/RUNNING remains required independently of optionalC4=null. Common rising-latch squint is shared withPlace. No frozen source-reader edits.

## Initial coherent draft review — partial, not frozen

Reviewed worker cache/class/helper subsection: NativePickupTargetCache.cs, NativeStackBlocksBehavior.cs, NativePlaceRelObjectHelper.cs, NativeDriveToHelper.cs and BehaviorHelperComponent.cs factory type1 extension. No concrete behavioral defect identified in that reviewed subsection as currently read. Actual runner/factory/owner/API integration is incomplete and not yet approved; no whole code PASS.

Distinct cache entries0/5/3 preserve ownstamps/IDs/best; bottom3 recursivelyensures5 before ownclockgate, topcarriedpointer and bottomwhiteboard/family2/nativeCanStack predicates agree. Unchanged DefaultBest selector is appropriate for these entries (configurationstack priority, signedvalidIDorder, strictdistance/ties), not an upright/nearest heuristic. The shared intention0 accessors/defaultDrive type0 remain present. Actual native geometry supplies family1/2/currentorigin; CanStack uses checked factor1 while CanPick retainsfactor2. Ordinary4/2 is explicit unavailable, never aliased to5/3.

Class draft follows corrected bothvalid/bottominvalid gate, closestbottom strictdistance/retarget cancellation-before-replacement, source40→optionalplain21A→needs43 order, actualpickup/placement helpers and resultlessground recovery. New placement helper delegates actual type1/zero-offsetDrive, preserves three-attempt counter and raw-response callback. A questioned table case03000010 during code read; independent known-first native005B94C0..005B952E confirms it writes whiteboard70 then RETURNS with no immediate retry, matching draft. This was a check, not a defect or production correction. All other noncancel/nonabort results take native retry. Runner lifetime, physical/wire/API outputs, unavailable handling and shared-consumer regressions remain to be reviewed when coherent/full and frozen. No tests run by B.

## Runner draft findings and finite constructor clarification

Current records reread before ownership/reuse conclusions:

```json
{
  "id": "M15-022",
  "title": "PlaceObjectOnGroundAtPoseAction composition and PlaceObjectOnGroundAction Init/CheckIfDone",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PlaceObjectOnGroundAction Init sends message 0x44 (0, 0, 0, 100, 200, 500 as raw bits), builds a TurnTowardsObject verify action and calls StopAllMotors; CheckIfDone never reads DockingComponent+5, its result is the verify action's (extraction sections 2.3..2.8; construction of PlaceObjectOnGroundAtPoseAction at 0x005DFF14)",
    "M12-015 owns only the wire message of PlaceObjectOnGround (its evidence does not cover the action)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ]
}
```

```json
{
  "id": "M4-028",
  "title": "The sleep sequence's lift child: MoveLiftToHeightAction(Preset 0, 5.0f, 0) construction",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x0052CF8E..0x0052CFB0; MoveLiftToHeightAction preset0, tolerance f32 0x40A00000; exact child initialization, locking, stop and completion must be verified in the control layer.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u2013T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "Re-read: movs r0,#0xA4; blx 0x4A42A0 0x0052CF8E..0x0052CF90; movs r3,#0; movt r3,#0x40A0; movs r2,#0; blx 0x4A9A48 MoveLiftToHeightAction::MoveLiftToHeightAction(Robot&, Preset, float) 0x0052CF96..0x0052CFA2, body 0x00548B84 (GetPresetHeight 0x00548C14, GetPresetName 0x00548CD4, exports). Handoffs: AddAction blx r7 (the parent's vtable +0x20) 0x0052CFB0 and execution: M8-017; the factory 0x0052CEA2..0x0052CFC0: M5-038; Init/CheckIfDone/acks: M4-016 and M4-002"
  ]
}
```

Coherent runner/integration review identified two pretest draft defects, sent to C and parent. Preserve these as actual draft findings, not failed tests:

1. Ground runner seeded an empty completion variant6. Actual PlaceObjectOnGroundAction::GetCompletionUnion00554AE0..00554AFA instead calls concrete variant0 setter, storing savedcarry+7C, count1 andfalse; it leaves the other16IDbytes uninitialized. A and B independently known-first checked instructions. Required correction is defined internal carrier projection plus full-public payload unavailable/M1-034 skip/log, retaining rawscalar/callback delivery. The operator's PlaceRel fourzero-unused policy does NOT extend to Ground. No invented fullzero Ground payload or fallbackvariant6.
2. Place Init added World.SelectedObjectId=target immediately after lookup/beforepreaction. Independent finite IDock Init/Place ctor/InitInternal comparison supplies no selectedobject producer at that position:0055158A..005515CA lookup/cast then0055160C..00551648 preaction and established Select/marker/Setup/tail. The shared checked preaction query does not authorize an early selected-state mutation. Remove the unsupported addition; cleanup clearing selection is not producer evidence. No global no-effect/caller claim is made.

Finite constructor clarification: source main P6 label “speed5f” was an extraction interpretation error, not a new approved-inventory contradiction. Actual MoveLift preset0 constructor00548B84 forwards presetheight and third float5 into floatctor0054899C. The latter stores height78,tolerance7C,variance80; duration88=0,speed8C=10f,accel90=20f. Preset table00C54684 firstpair{0,bits42000000=32f} is LowDock. A+B independently checked these original instructions/currentM4-028; current CreateNativeLiftChild(height,tolerance) call is already correct, so no numerical production fix is needed. Reuse the constructor recipient, not M4-028's sleep-sequence ownership for Place.

Both actual new Place/Ground native CanInterrupt vslot14 cells0102230C/010222D4 point0052B0B3→ThumbMOV0/BXLR; defaulttimeout slots2C cells01022324/010222EC point0052B0C3→Thumb30f. Their NativeVisionRunner inherited false/default30f is source-supported. A reciprocally checked actual cells, not inferred from the five older vision classes.

Normal Place typedvariant0/approvedguard, original raw-result callback, firstdoubleVerifyUpdate, retained firstterminalfailure, later child retirement/failureclear/highlift, activity27/objective40 maps and shared docking optionalmethod/C1 defaults agreed in the read draft. Ground defined projection, extra selectedwrite removal, availability cleanup, physical/API regression and exact freeze remain pending verification. No tests run by B.

Whole-record limit from actual draft: NativePlaceRelObjectHelper explicitly stops at Low/nonzeroA/B/!SkipTransform and nonzeroDrive1 recipients. A source-complete exactStacktuple implementation cannot settle whole currentM12-042 while those live parameter paths remain unbuilt. Preserve its current whole scope/status; no narrowing or partial settlement. Final current-record comparison will be repeated after freeze.

SF4–SF8 source supplement read and independently rehashed: `20261028-sparks-stack-1-source-followup-2.md` SHA256 `F04239051A06930EC4B5E56B9C7B3F0884725AA96A9289054D297AE644BE22AE`, bounded PRIMARY PASS for the finite findings/clarifications above. Additional saved-value check: Ground ctor00554674/0055467C stores -1 to saved7C; Init005547B0 reads actualcarry8 and005547BA stores saved7C BEFORE release call005547BE. Missingcarry branch skips that store, preserving prior savedvalue (constructor-1 on new runner). Completion must use saved7C, never resample live carry/world target.

Current M15-022 title/evidence quote above owns its explicitly named Ground Init/Check/AtPose composition; M8-015 generic watcher/preparation does not own the concrete Ground getter. SF5 explicitly proposes the newly recovered concrete Ground completion/availability producer as an unowned step, without ID/status. The manager must allocate that ownership before any whole configured-caller settlement; do not silently absorb it into a narrow existing record or the earlier PlaceRel-only proposal. No change to official records, operator policy or frozen main packet was made by B. C was notified of both draft defects; affected implementation correction verification remains pending.

## Current M12-012 and M12-031 scope assessment

Current records were reread directly before these conclusions. Their complete title, status, evidence and unresolved text follow.

```json
{
  "id": "M12-012",
  "title": "What makes a cube a valid bottom to stack on: resting flat within ten degrees",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0063C5C4 CanStackOnTopOfObject; 0x0063C654 CanInteractWithObjectHelper",
    "0x0063C66C movw r1,#0xB8C2 + 0x0063C670 movt r1,#0x3E32 = 0.174533 rad = 10 deg; 0x0087751C IsRestingFlat",
    "the 15.0 is owned by M13-007",
    "IsPoseTooHigh 0x00877954: 0x00877978 D=GetDimInParentFrame<'Z'> (0x00557794); 0x00877990 s0=D*f1; 0x00877994 s16=D*f3; 0x00877998 s18=D*f1+f2; 0x008779A0 pose.z ([r0+0x28]); 0x008779AA D*f3+pose.z; 0x008779AE +1e-5 (0x3727C5AC); 0x008779B2 vcmpe; 0x008779BA movmi r0,#1",
    "0x0063C6C0..0x0063C730, 0x0063C78A..0x0063C792, 0x0063C5FE..0x0063C61C, 0x0063C840, 0x0063C8C2..0x0063C8D2",
    "re-analysis/research/20260929-R-VIS-M12-gap3-extraction.md Q7"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): the ten-degree tolerance is binary32 0x3E32B8C2 (0x0063C66C..0x0063C670) with bit-boundary tests; the PoseState check removal and the with-respect-to-robot pose from fix round 3 stand. Still open: the on-top search is M13-007/M13-023; IsRestingFlat (BlockWorld, Math.Acos) and Radians::Rescale are double where the engine is float; CanPickUpObject/CanPickUpObjectFromGround have no C# caller. B-M13-11 (2026-10-21), built, awaiting strong verification: DimInParentFrameZ now selects row2 of the supplied matrix through existing RotatedParentAxis, strict ordered comparisons retaining X/Y/Z on ties, and maps the selected axis to the corresponding object size. Unit regressions cover noncubic charger, unordered/tied components and relative-pose height boundary. No shared map/pose recipient closure. Codex review; Claude spot-check pending."
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

M12-012 remains partial on the reviewed code. Native cache.CanStack supplies the configured Stack path with actual native geometry and factor1 height check, but the existing public Docking.CanInteractWithObjectHelper still uses the legacy BlockConfigurationManager neighbor path and public CanStack retains a History gate. CanPickUpObjectFromGround is not implemented. The old unresolved note about Math.Acos is partly stale: current IsRestingFlat uses MathF.Acos plus Radians wrappers. This report does not repeat the old double-arithmetic statement as a verified current defect or audit the whole math recipient. The new native caller alone does not close all existing public consumers or inherited geometry/math frontiers. Preserve the whole current record and its status; reconsider only if the final code changes these concrete limits.

SF8 interpretation correction: my earlier statement that GetPreActionPoses is a pure query was wrong. Both independent readers refreshed native5508C8: null input and carried-target equality reject before selection; 550988 calls actual World.SelectObject before obstacles5509A0. SelectObject627148 looks up the actual located ID; missing lookup logs/returnsfalse and preserves oldselection, while valid lookup clears the prior ID and stores the requested one at627168. Its return is ignored by the caller. The earlier explicit Init write was too early, but removing it does NOT justify omitting the guarded query-owned producer. Frozen earlier reports remain historical; this correction supersedes that pure-query sentence.

The first implementation correction only wired GetNative into NativePlaceRelRunner.Init. A second concrete missing join was identified: NativePlaceRelObjectHelper.Precheck calls IsAtPreActionPoseWithVisualVerification, whose type1 Compute still used pure Get. Independent known-first555928..555974 confirms native Compute calls the SAME selecting GetPreActionPoses; the type0 helper route also calls that recipient. Native prechecks must carry selection through their real world recipient before obstacles, not rely on a later runner or Drive call. C and parent were notified. The same finite native precheck freshness expression is f32(timestamp)+1000f compared with f32(now); current public helper used double arithmetic, so the new native route must not inherit that mismatch. These corrections are within already owned M12-026/031 and require no invented record or official scope change. Actual fixes and focused regressions remain pending review.

## Frozen-draft review update (2026-10-28, before reviewer host release)

The 19-file C# freeze is now available in the worker at `.scratch/sparks-stack-build/frozen-csharp.sha256`. The builder still owns the runner; this reviewer has not launched tests. Whole-build acceptance remains PENDING independent execution and final byte verification.

SF9/SF10 and SF11 were independently checked and rehashed: source-followup-3 SHA256 `08FE369F10331FC8FC584CD123A92A323DCCF818003F88166201C4190395AA10`; source-followup-4 SHA256 `8F1E3B55E911C7CF685509A031069402EEC95DA0C6492F7AF45CB0D4EB04EFE4`. The final draft now passes native world recipients through both actual Pickup and Place helper prechecks, including type1 Compute and type0 direct selection. Selection occurs after null/carried guards and before obstacles; native freshness uses float32 conversions and addition. Public geometric defaults retain their previous contract. This resolves the previously identified omitted producer and arithmetic join, subject to frozen regression verification.

Ground completion now retains variant0, saved carry ID, count1/false and scalar result while marking the full payload unavailable. It does not reuse the PlaceRel zero-unused policy. The generic unavailable publication diagnostic is supplemented by the original Pickup producer-specific M1-034 diagnostic. No new fidelity ID or official record change was made.

The positive scene supplies fixed cube geometry and external firmware responses. The opaque 44mm cube mask corrects the test renderer's former far-marker penetration through a nearer cube silhouette; images are not generated from the production released pose. The high-dock endpoint and retreat are declared external scene inputs, not recovered firmware trajectory claims. Actual hard/soft carrying and pickup pipelines first passed after this fixture correction; that does not establish a production pickup defect or hardware acceptance.

The retained attempts distinguish helper counter attempts from physical dock commands. Initial failed precheck consumes attempt1, then two failed docks exhaust counter3. The actual failure path marks the whiteboard, dispatches failure through the real AI helper component, and may then have recovery cancelled by the same-frame owner validity check. A separate explicitly supplied response/callback boundary exercises recovery without claiming it is an end-to-end failed-dock trajectory. The final test drains cancellation of an actually owned Place action before supplying the labeled raw response, then uses the real component clear-and-callback phase.

Nested Ground is an ActionList compound child: its completion retires the child and feeds the recovery callback, but it does not independently publish root 0x5A. The former child-publication assertion was a harness error. Standalone Ground root tests separately exercise local completion and the undefined full raw/API guard. No extra child publication was added.

Focused15's hidden-rendered negative Ground case returned the expected 0x0300001D and correctly withheld the undefined payload, but its no-deletion assertion failed. Diagnostic17 retained the concrete producer in `.scratch/sparks-stack-build/negative-vision-producer.log`: MakingUnknown for object0 precedes the later Ground Clear null-pointer diagnostic. Current MarkObjectUnobserved logs that key then directly invokes MarkObjectUnknown(true), which calls DeleteLocatedObjects and delivers LocatedObjectDeleted. Ground's subsequent null Clear returns without deletion. Thus the old no-deletion oracle was invalid for the full hidden-rendered scene; no production repair is indicated. The diagnostic does not prove the whole vision subsystem or settle its owning record.

The separate processed43/no44 negative unit supplies the inherited processed-image boundary explicitly. It exercises actual retained Wait/Verify, failure Clear and root completion while retaining the object; it is not a replacement claim that the hidden-rendered vision path never deletes. Positive Ground continues through the real rendered worker. Preserve focused15 and diagnostic17 as separate attempts and scopes.

Current M11-007 was reread before this classification. Its full current title, status and evidence are quoted below. It remains IMPLEMENTATION_GAP; only the concrete miss-to-delete branch is used here. Current M12-012 and M12-042 remain partial for the limits already recorded above. The two proposed ownership sections remain configured Stack lifecycle and related placement verification/completion leaves; no existing record is partially settled.
```json
{
  "id": "M11-007",
  "title": "Two misses before an object pose is forgotten, and two sightings before one is confirmed",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ObjectPoseConfirmer::MarkObjectUnobserved 0x00506FBC reads the miss count at +0x28 and writes count+1 zeroing +0x24 (strd r2,r1,[r0,#0x24] at 0x00506FE0); the forgetting branch is at 0x00506FDE; the confirming side at 0x00506A04 mirrors it",
    "the two counters reset each other, so the misses have to be consecutive",
    "this stack already used two; what was missing was the counter it rests on",
    "confirming side: AddVisualObservation 0x50684C starts a new entry at count 1; a matching second sighting increments to 2 and calls UpdatePoseInInstance (0x506A04..0x506A26); a mismatching sighting resets the count to 1 (0x506A46..0x506A4E); IsReferencePoseConfirmed is count > 1 (0x506340); IsObjectConfirmedAtObservedPose requires count >= 2 and a matching pose (0x50634C..0x5063B8)",
    "0x00506A26..0x00506A32 UpdatePoseInInstance before AddLocatedObject; 0x00505DE0..0x00505FE2 UpdatePoseInInstance; 0x0050609C..0x00506126 the write helper; 0x00506FBC..0x0050702E MarkObjectUnobserved; 0x00507128 MarkObjectUnknown (no store to obj+0x24 in 0x00507128..0x005075A4; 0x0050612C SetPoseStateHelper)",
    "re-analysis/research/20260929-R-VIS-pre-extraction.md Part 1 item 3 rows 3.1-3.11; re-analysis/research/20260929-R-VIS-verify-pre-items1-3.md (objection 1: LocalizeRobot claim now closed by re-analysis/research/20260929-R-VIS-M11-gap1-extraction.md Q1 and re-analysis/research/20260929-R-VIS-verify-M11-gap1.md; row 3.10(a) corrected there: no Invalid state is set)",
    "0x00507128..0x00507400 MarkObjectUnknown; 0x004E49AC..0x004E4A30 CanBeUsedForLocalization",
    "re-analysis/research/20260929-R-VIS-verify-M11-batchA.md objections 1, 3, 11",
    "0x00507266..0x0050731E, 0x004E49C2..0x004E49DE, 0x004E3844, 0x004EA66E"
  ]
}
```

## Final independent build verdict — 2026-10-28 14:06 CDT

**PASS for the complete changed diff and the exact configured SparksStackBlock path, within the explicitly named source and input boundaries below.** This final section supersedes earlier PENDING statements. No additional blocking unsupported join, circular oracle, race or deadlock was identified in the frozen changes. Passing regressions support this verdict but are not its source authority.

The review covered all 19 frozen C# files, including the actual configured factory/activity binding, manager and Sparked ownership reuse, intention5/3 cache and dependency cadence, target validity/one-time retarget, native helper delegation, real ActionList Place/Pickup/Drive/Ground children, completion watcher ownership, scalar/local versus full public/raw completion, source ordering of objective40/optional animation/needs43, cancellation and cleanup. The source basis is the accepted S/P/C packet, SF1–SF11 followups and the operator's corrected validity contract and guarded unused-slot policy. The extra native helper selection and float32 freshness joins were reviewed after correction; they do not rely on a later runner to supply an earlier producer.

All 19 raw C# hashes independently matched the builder freeze before and after both reviewer runs. Freeze-list file SHA256: `8C2C3739BB160914237BC9F338B0D372691E99CDDADAC6FB7D094F677ECB9446`. The complete per-file expected/actual values are retained in manager `.scratch/sparks-stack-reviewer/pre-run-hashes.json` and worker `.scratch/sparks-stack-reviewer/post-run-hashes.json`. No production or test files were edited by this reviewer.

Independent runs used the already built frozen worker, `dotnet test --no-build --no-restore`, actual local assets, elevated IPC and no asset bypass. They ran sequentially after explicit builder host release.

| Reviewer run | Exact filter artifact in worker | Result | Exit | TRX |
|---|---|---|---|---|
| Original affected filter | `.scratch/sparks-stack-reviewer/affected1.filter.txt` (copied from builder's saved filter) | 1125 passed / 1125 total; 0 failed, skipped/notExecuted, errors, timeouts or aborts; 84s test duration | 0 | `.scratch/sparks-stack-reviewer/affected1.trx` |
| Separate finite consumer extension | `.scratch/sparks-stack-reviewer/consumer-extension1.filter.txt` | 36 passed / 36 total; all adverse counters 0; 1s test duration | 0 | `.scratch/sparks-stack-reviewer/consumer-extension1.trx` |

The extension filter was exactly `FullyQualifiedName~M13PreActionWorldTests|FullyQualifiedName~ManipulationTests|FullyQualifiedName~StandaloneManipulationOwnerTests`. It was justified by the changed shared native pre-action/world selection recipients and the preserved public manipulation defaults; those class names were absent from the original filter. It did not repeat the passed core or broaden to a full suite. The original filter covered new Stack, ordinary Knock/Sparks/Pickup/PutDown, shared children, M7/M8/M12, Navigation, ActionList, behavior/Freeplay and helper consumers. All 22 final Stack cases were included in that run.

Reviewer TRX start/finish: affected1 `2026-10-28T14:03:03.8926454-05:00` through `14:04:29.5523914-05:00`; consumer extension `14:04:42.0798713-05:00` through `14:04:45.1329948-05:00`. Raw logs, exit files and explicit counters are beside each TRX; `results.json` records counters without XML serialization ambiguity. The first display of that JSON serialized the XML counter node as an empty array; it was corrected by explicitly reading numeric attributes. Neither test result changed.

`dotnet build-server shutdown` returned 0 and confirmed both MSBuild and VB/C# compiler servers stopped. Final elevated process inventory explicitly records count0 for dotnet, testhost, VBCSCompiler and MSBuild. A preliminary Get-Process lookup returned shell exit1 because no named processes existed; the subsequent explicit empty inventory completed exit0. This was not a test attempt or failure. No reviewer process remains; the host is released to the manager. No reviewer full suite was run.

### Retained builder attempt audit

All builder attempts remain visible; passing later cases do not erase the failed or empty attempts. Counts below were read directly from the retained TRXs. All listed TRXs have zero notExecuted, error, timeout and abort counters.

| Attempt | Passed / failed / total |
|---|---|
| focused1 | 4 / 4 / 8 |
| focused2 | 4 / 4 / 8 |
| focused3 | 4 / 4 / 8 |
| focused4 | 6 / 2 / 8 |
| diagnostic5 | 0 / 0 / 0 — no tests matched; not a passing regression |
| diagnostic6 | 0 / 2 / 2 |
| focused7 | 8 / 7 / 15 |
| focused8 | 14 / 1 / 15 |
| focused9 | compilation failed (protected StopActing); no TRX/test execution |
| focused10 | 15 / 1 / 16 |
| focused11 | 15 / 2 / 17 |
| focused12 | 16 / 1 / 17 |
| focused13 | 16 / 1 / 17 |
| focused14 | 18 / 1 / 19 |
| focused15 | 20 / 1 / 21 |
| focused16 | 3 / 0 / 3 |
| diagnostic17 | 1 / 0 / 1 |
| affected1 | 1125 / 0 / 1125 |

Earlier sections and the builder ledger retain the changing fixture, callback ownership and publication expectations. In particular, the original hidden-rendered Ground no-deletion expectation is resolved by the retained ambient MakingUnknown producer trace, not by a speculative production patch. The processed43 failure test and standalone/nested Ground publication tests state their distinct supplied input and ownership boundaries. Earlier pipeline logs were not all preserved per attempt before they were overwritten; retained TRXs and runner logs are authoritative for the attempt counters, and a current pipeline trace must not be presented as the trace of an older attempt. Builder artifact location is worker `.scratch/sparks-stack-build/`; reviewer independently extracted counters are in `.scratch/sparks-stack-reviewer/builder-trx-counters.json`.

### Claim boundaries and record impact

This PASS supports the two proposed ownership sections: (1) the whole exact configured SparksStackBlock lifecycle; (2) the related concrete placement Verify/completion leaves, including the Ground completion producer. No fidelity ID, status, inventory, approval or Claude file was created or altered here. The operator/manager owns allocation and settlement.

The PlaceRel normal completion's four unused ID words are COMPATIBILITY_POLICY, with the exact selectedDock2/3 plus located-target guard. They must not be labeled EXACT_SOURCE. Missing/unselected meaningful fields remain unavailable. Ground's uninitialized words retain the M1-034 full-publication guard; the defined scalar/local projection and callback continue. No policy is silently extended between producers.

Whole M12-042, M12-012, M12-031 and the broader existing Init/selection/common-action records remain partial where their own current scope exceeds this configured path. Ordinary intention4/2, nonzero placement offsets, Low/transform variants, broader public geometry/history and inherited raw admission/picker/executable-finder or orientation-hook consumer frontiers are not settled by this change. M11-007 remains open despite the concrete diagnostic branch evidence. Existing broader Sparked/PlayArbitrary records are not declared complete from their reused subset.

The test scene's physical endpoints and firmware response schedule are explicitly supplied host inputs. These offline regressions do not establish exact firmware trajectory, hardware success or acceptance. The manager's final integrated full gate remains pending and is outside this reviewer's test scope. Actual model telemetry was unavailable; no model identity is inferred from agent labels.
