# R-M11-FRAME-POSE — SetNextImage success continuation

Request: `requests/20261021-m11-frame-pose-assembly.md`. Examined HEAD **f395e545**. Independent native extraction and read-only production comparison, pending checking. Coverage was opened before tracing. Paths in the production column are relative to `cozmo-stack/src/Cozmo.Robot/`.

| Step | Native instructions/literals | Current production file/line and caller | Record | Disposition |
| --- | --- | --- | --- | --- |
| Existing history query/failure gates | Adopted B-M3M4-34/OV128: min(image timestamp,newest raw key), flag1 at0x00652C22..C44; success→CFC; origin mismatch→53158 return0; other failure→5315A return original error. | `Vision/VisionSystem.cs`:751–787 OnFrame→QueryFramePose; History is EngineRobot.PoseHistory. | M11-097; M11-049 restoration note | SUPPORTED checked query/handoff; retain it. No new reinterpretation of query algorithm or timeout writer. |
| Current-frame latest state lookup | CFC constructs temp atsp+B4;0x00652D04 loads robot+2B0; D08 history+390; D0E calls GetLastStateWithFrameID; **no result test** and output not supplied to the next calls. | `Vision/NativeRobotStateHistory.cs`:268–280 helper exists; frame assembly does not call it. EngineRobot.PoseFrameId `CozmoEngine.cs`:1072, `NativeDelocalization.cs`:131. | M11-097 algorithm; image caller M11-033/040 | Caller UNBUILT. Its diagnostics/error flag can occur; its output is not the image-time state. |
| Historical camera pose | 0x00652D12..D1A passes computed HistRobotState atsp+120 and returned time atsp+11C; body0x00513AB0..3B66 copies headcam, rotates −head aboutY, precomposes neck, parents to input state pose. | `Vision/RobotStateHistory.cs`:10–12 derives CameraPose; `Vision/CameraModel.cs`:20–64 uses old headcam translation; `NativeRobotStateHistory.cs`:282–287 flattens robot pose. | M11-003/054 geometry; M11-033 assembly | UNBUILT historical pose carrier/parent boundary; existing algebra recipients remain their checked owners. |
| Homography lookup / producer |0x00652D28 uses computed state head atsp+154; Lookup0x006533C4..34FA; calibration install6516FC..1774; LUT producer651998..1C58. | `VisionSystem.cs`:173–185 installs calibration/Detector.Init only; `OverheadEdges.cs`:128–151 reconstructs from projected points. No LUT lookup/populate implementation found in src. | M11-017 producer/use; M11-033 assembly; ownership decision below | UNBUILT stored LUT, visibility flag and nine-f32 matrix. Failed lookup does not abort SetNextImage. |
| VisionPoseData.Set state fields |0x006AF330..F396: time+0; pose+4; memcpy0x5B state bytes to+10; camera+6C; reparent camera to stored robot; visibility+78; H+7C; IMU deque+E0; tail ClearPoseParent. | Current `VisionPoseData` has timestamp/robot pose/head/lift/motion booleans only; Query success calls host PoseData(time,value). | M11-033/040; existing M11-049 unread-boundary note | UNBUILT full carrier. Exact layout is identification, not mandated C# memory layout. +A0 member not assigned by Set; don't invent a new field value. |
| Pose copy / detach | Assignment845940..59BE→node assignment8459D0..5A2E copies transform/parent/name into destination node; Set tail8CDC2C→4BED18→53055C→8CB36C→4A9E8C→8449BC ClearParent. | Pose types exist; host conversion instead calls GetWithRespectToRoot and creates a new flat pose. | M11-065/066 and existing pose-copy owner | UNBUILT caller semantics. Detach keeps local transform; it is not flatten-to-root. |
| IMU producer / snapshot | Before query: CalculateTimestampForImageIMU at652C16 onVC+B0. Add538B24..B58 creates24-byte entries, cap20, computed time+14 starts0. Calculate538C00..D7E assigns that word. Set deep-assigns deque at6AF38A. | `RobotStateHistory.cs`:17–20 ImuData lacks computed timestamp;33–47 keeps2000ms window. `VisionSystem.cs`:714 feeds it,987 reads live history later. CameraFrame lacks native metadata+18/+1C as such. | M11-004 producer/gates; M11-033/040 capture | UNBUILT full IMU snapshot and input metadata join. Wire semantic names of the two image metadata words remain UNKNOWN here; offsets/algorithm below are established. |
| Component mutex / handoff | Lock652D3A→SetD52→unlockD58; later separate mailbox lock653058..314E. Sync path65304C usesVC+1F8. Processor swaps/copies next→current under mutex65201A..2212 then Update65222E without lock. | Query held under pose-tree gate762–787, then mailbox tuple804–820; worker extracts837–850 and processes later. | M11-040; M11-033 | Existing latest-wins mechanics are separate checked rows. Full snapshot/parent moves UNBUILT; do not claim native capture and image publish are one lock interval. |
| Observable consumers | VisionSystem.UpdatePoseData6B24D0..2706 retains previous/current, re-parents current HistRobotState toVS+F8 at26E8; overhead visibility6ABE5E/6AC086 and H6ABE8A; motion H6AAD5C. | `VisionSystem.cs`:1017 camera,1041 overhead,1153 face,1167 pet; public VisionFrameResult carries PoseData. | M11-033/017; marker/face/motion owners stay separate | Fields feed API/world state, so absence is not a no-effect claim. Full detector/body/exposure code outside this request. |

## Evidence and bounded reuse

Navigation: `re-analysis/decomp/libcozmoEngine/index.tsv`. Evidence: instructions in shipped `resources/lib/armeabi-v7a/libcozmoEngine.so`, resolved through ARM PLT/GOT and Thumb veneers. Read all selected success blocks and required helpers through their normal returns; inspected cleanup where needed. No decompiler text is used as behavioral authority. No native execution, tests or hardware runs were performed.

Reused the adopted `20261019-b-m3m4-34-check.md` PASS for query selection, flag and failure/no-handoff gates, superseding B33's intermediate camera exclusion. Reused `overnight/20261018-ov128-check.md` corrected H10/H11 for GetLastStateWithFrameID. Fresh caller reads establish which input/result is actually used here, rather than extracting the query again. The timeout writer is the separate `20261021-m11-history-timeout-input.md` answer, not part of this read.

Earlier checked extraction already contains useful assembly evidence: `20260929-R-VIS-pre-extraction.md` Part2/item6 rows4/6/7 and item9 row10; `20260929-R-VIS-verify-pre-item6.md` rows4/6/7 PASS; `20260929-R-VIS-verify-pre-items9-10.md` pose-capture and Processor rows. The fresh read confirms these slices, adds the ignored current-frame output and Set's tail parent detach, and exposes their missing production joins. `20261014-pose-tree-verifies.md` adopted neck/headcam producer rows establish the geometry constants. `20261021-m11-pose-history-settling.md` F9 is only a pending lead. No old statement that camera frames always stop or lack the history query describes this HEAD.

## Caller inputs, times and failures

Let `image` be EncodedImage, `computed` the HistRobotState atsp+120, `returnedTime` the U32 atsp+11C, and `latestFrameState` the temp atsp+B4. Those are working names, not new native field names.

- Already checked ComputeStateAt receives min(image[+C], newest raw timestamp), output time atsp+11C, output state atsp+120 and flag1. The continuation receives the **returned time**, not simply the requested/clamped time and not image ID. Note that the current M11-097 evidence string transposes the prose names of the two output stack objects; its own instructions at652C3C/C3E and function ABI establish &time=sp+11C, &state=sp+120. The snapshot quotes that string unchanged; this answer does not edit it.
- On success, construct latestFrameState, then call history.GetLastStateWithFrameID(robot's **current pose frame ID** at+2B0, latestFrameState). Raw then vision-only search, newest-first and their early smaller-frame stop/error behavior remain the OV128-adopted helper. It copies pose and91-byte state on success; failure returns1 after native diagnostics/error-flag handling (531F46..F86 or532032..208C). **Caller ignores both return value and the resulting temporary for assembly.** AtD12 onward it reloads robot, returnedTime and computed; latestFrameState is eventually destroyed at653152/154. Do not use this otherwise-unused output to replace computed, align the frame to current robot frame, add a new rejection on its failure, or claim its diagnostic/error-flag effects are absent. A narrow manager scope decision is needed before omitting the call's diagnostics; no new branch is justified.
- GetHistoricalCameraPose receives computed and returnedTime. Homography lookup receives **computed.State.HeadAngle** atHist+34=sp+154, not live motion head angle nor the unused latestFrameState. Set receives returnedTime, computed, the camera pose temporary, lookup bool, matrix temporary, andVC+B0 IMU history.
- Ground lookup failure merely sets visibility false. The36-byte matrix temporary was zeroed at652D20..D24 before lookup; failure leaves it unchanged. Set still runs and the image still reaches the later route. A failed visibility lookup is not history failure, calibration-null failure, or “drop all modes.”
- History failures take the already adopted no-Set/no-handoff paths. Timestamp bookkeeping and IMU timestamp calculation precede the history query, so those earlier effects are not rolled back. No additional normal-return failure predicate is present in GetHistoricalCameraPose or Set. Null/invalid pose crashes are M1-034 cases, not new successful-state substitutes; full malformed/null upstream reachability is UNKNOWN here.

## Historical camera geometry and parents

GetHistoricalCameraPose0x00513AB0..3B66:

1. Copy RobotHeadCam pose from robot+2D8 into the returned pose (3AB8/3AC2). Read computed head angle atHist+34, toggle sign bit0x80000000 (3AD0/3AD2), construct Radians and RotationVector3d about Y_AXIS_3D, RotateBy on that copied transform (3AF6).
2. PreComposeWith the neck transform obtained from robot+2CC (3B02..3B0E). This produces a camera-local transform in the robot frame. It does not multiply the current main robot/world pose here.
3. SetParent(camera, computed.Pose) at3B16. SetName derives `PoseHistoryCamera_` plus returnedTime (literal0x00513BA4, unsigned conversion at3B1E; matching prior unsigned-to-string call census OV57). Name formatting is diagnostic/best effort; don't use it as geometry or a substitute timestamp.

The adopted Robot constructor constants are neck(-13,0,49), identity rotation, parent robot; headcam translation(17.52f,0,-8.0f), parent neck, initial rotation from the9 floats at0x00C4A854 (M11-003/M11-054). Reuse the existing checked quaternion/transform operations; algebraic rearrangement can change f32 translations. This answer does not re-extract Rodrigues/arithmetic or the shipped camera calibration matrix's implementation.

VisionPoseData.Set assigns the HistRobotState pose into a **destination-owned node**, copies its state, assigns camera into its own destination node and reparents that camera to its stored robot pose. Pose assignment845940/59D0 copies transform/parent/name, rather than aliasing the source wrapper as the destination. After IMU assignment, the tail at6AF396 reaches **HistRobotState::ClearPoseParent** for data+4, eventually PoseBase::ClearParent8449BC..49D8: SetParent(null), with no transform adjustment. Thus after Set the stored robot pose has the computed pose's **local** transform with no parent; stored camera's parent is the stored robot node. The original computed/source node remains parented. Flattening to root before this copy/detach is not equivalent when the origin parent has a nonidentity transform.

Later VisionSystem.UpdatePoseData retains old/current values and, after copying input, sets current HistRobotState parent to VisionSystem+F8 (6B26E0..26E8). The origin-anchor object's complete producer/lifecycle is **UNKNOWN in this bounded read**; preserve the existing pose/origin owner (M11-066/073/076) rather than infer “use current robot origin” or an identity root. RobotState.PoseFrameId/PoseOriginId remain bytes in the captured state and are not overwritten by Set. Current host facade loses that full state and root/frame distinction.

## Full field inventory: no fabricated frame carrier

Native offsets identify what must survive the handoff; C# need not duplicate native allocation/layout.

| VisionPoseData offset | Assignment at Set | Required producer / meaning / uncertainty |
| --- | --- | --- |
| +0 U32 |6AF33E←returnedTime | Actual query output time, distinct from image timestamp still carried by image. |
| +4 Pose3d wrapper |6AF348 copy computed pose; tail clears parent | Computed history pose, local transform, copied parent then detached; source unchanged. |
| +10..+6A |6AF34C..356 memcpy0x5B fromHist+0C | Entire generated RobotState, including its own timestamp, pose frame/origin IDs, head/lift angles, status and other state. Do not substitute boolean-only summaries. Padding+6B is not written by this copy and has no recovered semantic field. |
| +6C Pose3d wrapper |6AF362 copy historical camera;36A SetParent(data+4) | Separately computed local camera transform, parent stored robot. |
| +78 U8 |6AF374 | bool LookupGroundPlaneHomography result, commonly called groundPlaneVisible. |
| +7C..+9F |6AF37C..388 | Nine32-bit float matrix words. Copy even when visibility false; caller temporary then contains zeros. |
| +A0..+DF member |**No write by Set** | Existing GroundPlaneROI object used by GetImageQuad (6ABE86/6AAD60); its base Image is initialized by the carrier constructor659658..65C and copied/swapped later. Complete extra initialization/mutable state semantics **UNKNOWN here**. Do not add a per-frame empty ROI assignment merely from the earlier “empty object” description. |
| +E0 deque |6AF38A→6AF4AC..F530→6AF532..F68E | Independent ordered IMU element copy, not a pointer to later live history. Fields and producers below. |

The full carrier copy0x006B28F0..2A2A copies time, pose/state, camera, visibility, H, ROI/Image metadata/refcounted storage and IMU deque. The move/swap helper0x00659B7C..9DCA swaps all those members and explicitly repairs both camera parents at9DBC/9DC4 to each destination's stored robot. Constructor0x0065960C..9678 builds members then invokes its forwarding swap. The Processor's repeated construct/swap is therefore a parent-aware transfer, not “two shared pose pointers plus a byte array.” Exact malloc/refcount machinery can differ; independently owned state/matrix/IMU and correct pose-parent lifetimes must remain. Full ROI Image implementation stays a named helper boundary.

## Homography lookup and its producer

Lookup0x006533C4..34FA reads the float-keyed tree atVC+31C (root+320,size+324). Empty: sVerifyFailedReturnFalse then false, no output write. For ordered finite inputs, maxKey<head returnsfalse (3404..3414). Otherwise perform lower_bound(head): first key>=head. It also obtains that key's successor and compares f32 abs distances at34C4..34E6, moving to the successor only if distance to lower_bound is strictly larger. With a normal ordered finite lower_bound>=head and successor>lower_bound that move cannot happen; **it is not a nearest-bracket lookup**. Head below minimum selects minimum; exact key selects it; midpoint selects upper key; above maximum returnsfalse. Equal distances keep lower_bound. Native comparisons use VFP `vcmpe`/`bpl`/`it gt`; preserve their flags in an instruction-faithful port. NaN keys are not assumed to form a valid ordered tree; their production reachability/input handling remains UNKNOWN. The fallback warning for failure to find lower_bound is retained in source, not newly declared impossible for every exceptional input.

Producer is calibration change: SetCameraCalibration0x006516FC..1774 calls Camera.SetCalibration; only a changed nonnull pointer reaches mutex-protected VisionSystem.UpdateCameraCalibration, async Start as applicable, then PopulateGroundPlaneHomographyLUT(step **0x3B8EFA35**, nominal0.25deg). Unchanged calibration does not repopulate. Populate0x00651998..1C58 (normal return) starts f32 angle0xBEDF66F3(−25deg), increments with f32 addition by step and loops while<=0x3F46D3F2(44.5deg), stopping earlier when no tested ground corner is in view. It obtains live Robot.GetCameraPose(angle), robot pose expressed with respect to camera, forms M columns R[:,0],R[:,1],translation, then **H=K*M** (651A7C..1ADE→4F83F8). Uses calibration cols/rows and GroundPlaneROI.GetImageQuad; retains an entry if corner0 or corner1 passes Camera.IsWithinFieldOfView(pt,0,0), otherwise logs MaxHeadAngleReached and stops. Copies nine words into map node+14 at651B54..B5E. Read selected producer body including normal cleanup and exception cleanup; no tree clear is present in that body, so don't invent unconditional replacement/clear during repopulation. These rows are already checked in pre-item6; current src has no corresponding LUT producer. Helper internals K/matrix/ROI/FOV are reused checked dependencies, not newly certified whole subsystems.

## IMU fields required by Set

HandleImageImuData0x00535C20..C4C forwards imageGyro raw+0/+4/+8/+C/+10 into VC+B0.AddImuData. Add0x00538B24..B58 stores a **24-byte** record:

- +0 U32 incoming key (image association, not yet computed image-clock time);
- +4/+8/+C three f32 rates;
- +10 U8 incoming value (used in timestamp calculation as an index/line value; do not rely on current C# name Flags);
- +14 U32 computed time, initialized0 at538B34; bytes+11..13 have no semantic field established here.

It pops front only when deque size==20 before pushing. The current2000ms window is a different rule. Before the history query, SetNextImage calls CalculateTimestampForImageIMU(VC+B0, image[+1C], image[+C],65.0f,image[+18]) at652C04..C16; exact roles from instructions are first association key, image timestamp, f32 duration and signed divisor respectively. Source/protocol names and full provenance of image metadata+18/+1C are **UNKNOWN here**, not silently identified as current CameraFrame.ChunkCount or ImageId. CameraFrame currently exposes no retained pair with this proven meaning. A builder must trace those metadata producers before joining these arguments; numeric stand-ins would be unsupported.

The required calculation body538C00..D7E was read fully. Define k=image[+1C], t=image[+C], n=signed(image[+18]), scale=f32(65.0f/f32(n)). Iterate deque in order. For entry.rawKey==k, store entry.computedTime=VFP_u32(f32(f32(t)−f32(f32(n−entry.u8)*scale))) (C62..C7C). For entry.rawKey==u32(k−1) whose computed time is0, and which has a predecessor with nonzero computed time, store VFP_u32(f32(f32(predecessor.time)+f32(f32(entry.u8−predecessor.u8)*scale))) (C82..C8A,D26..D4A). Other entries remain unchanged. Arithmetic is f32, then VFP conversion, not host DateTime or nearest raw-key interpolation. Malformed/divisor-zero upstream inputs and their support policy are **UNKNOWN**, not an authorization to fabricate timestamps.

Set copies the whole ordered deque into carrier+E0 after timestamp calculation, through copy assignment6AF4AC..F530/assignF532..F68E. Current ImuData contains only17 logical bytes and loses computed time; it also reads live Imu at processing rather than that snapshot. This is an input/capture gap, regardless of whether any current caller happens to see the same rates. Full gyro gating/detection remains M11-004 and its own checked rows; this answer does not reimplement it.

## Mutex, routes and downstream effects

The component mutex protects Set into **VC+1F8** (652D34..D58). History query, current-frame query, camera calculation and LUT lookup occur **before** that lock. The camera temporary is destroyed after unlocking at652D5E. Pose assembly occurs before calibration/dot optional decode, paused check and mailbox overwrite/drop-statistics test. Thus later skip/decode failure does not undo an already stored pose value. Use the adopted M11-058/M1-034 decode policy; this answer does not change it.

Synchronous route passes that stored carrier directly to UpdateVisionSystem at65304C. Async route later locks the same mutex again at653058, swaps image bytes+metadata into the single next image slot, then unlocks65314E. There is an unlocked interval between Set and this publish; don't describe the original as one atomic query+image+pose transaction. Current architecture may serialize host mutation differently if observable results match, but needs proof of matching snapshot selection/order, rather than an invented native lock guarantee.

Processor locks65201A, exchanges image slots, transfers next carrierVC+1F8 and currentVC+100 via parent-aware constructors/swaps at65209C..2128, completes cleanup and unlocks652212. Later call65222E uses current carrier/image without the mutex; normal completion clears pending according to the already checked latest-wins/discard-on-completion behavior. No component lock is held across decode or vision processing. Shutdown/lifetime full teardown remains M11-055/077/078.

Downstream scope check, not detector extraction:

- VisionSystem.Update(PoseData,ImageCache) readiness/calibration gates precede UpdatePoseData at6B4D88. UpdatePoseData6B24D0..2706 rotates previous/current carrier values, copies incoming and reparents current stored robot pose to its own anchor; full copied state, camera parent and previous frame survive into detectors.
- OverheadEdges.Detect directly gates on visibility at6ABE5E..64 (grey counterpart6AC086), reads status at+5C, and passes H+7C and ROI+A0 to GetImageQuad at6ABE86..94. Absence changes produced edges/map content; cannot call H/visibility “unnecessary.” Motion ground extraction also uses H+7C/ROI+A0 at6AAD5C..66. Existing checked M11-017/map recipients establish that these results affect world state.
- Current production builds CameraModel from `pd.CameraPose` atVisionSystem.cs1017; overhead1041, faces1153, pets1167 and public VisionFrameResult use its fields. Its public facade has no full RobotState, H, visibility or IMU snapshot. This proves current consumer differences without expanding marker detection, OKAO, whole exposure or the world model.

## Production comparison and remaining builder units

The already restored OnFrame→ComputeStateAt→HandOverFrame must remain working. QueryFramePose currently calls `RobotStateHistory.PoseData(time,value)` on success; that facade gets pose with respect to root and creates a flat Pose3d, reduces state to head/lift/status booleans, and derives camera on demand using HeadGeometry. HeadGeometry still intentionally uses old headcam(17.52,0,17.52), while engine translation(17.52,0,−8) is retained only for a different helper; M11-054 already owns this gap. This is not a new calibration policy or a newly accepted equivalent implementation.

The mailbox tuple captures the facade at ingress, which is useful existing behavior; it does not establish native carrier completeness. GetLastStateWithFrameID has real production use in NativeStateHistoryAdmission, but no image-success caller. No GetHistoricalCameraPose success assembler, LUT producer/lookup or typed full carrier exists in the searched src. Current IMU atVisionSystem.cs714/987 feeds a live reduced list; public CameraFrame.GyroRates is another reduced association and cannot replace the native24-byte deque. Existing `CozmoEngine.cs`:1069 comment saying PoseFrameId has no writer is stale: NativeDelocalization.SetNewPose131 increments it. Do not create a new fixed0 input based on that comment.

Bounded implementation proposal after evidence checking/approval:

1. Add an internal owned frame-pose value with returned time, complete copied HistRobotState, separate camera pose, visibility/H and copied IMU samples. Use existing checked pose arithmetic, copy and parent APIs; detach robot parent without flattening, repair camera parents across moves/copies, retain public/API mapping deliberately. Keep unknown ROI/metadata fields named, not guessed.
2. Bind image success to existing GetLastStateWithFrameID(current EngineRobot.PoseFrameId), without inventing an extra failure rejection. Build historical camera from actual engine headcam/neck inputs; coordinate M11-054 geometry with current estimator dependencies rather than quietly keeping the wrong translation under a faithful assembly claim.
3. Build calibration-driven f32 LUT and lookup, retain visibility=false/zero-matrix continuation. Use prior checked ROI/K/FOV helpers; complete their missing production joins if their source rows aren't implemented. Do not regenerate H from four projected corners and call it the recovered LUT.
4. Trace native image metadata+18/+1C and ROI member initialization/uses as the remaining narrow helper units, then build native24-byte IMU record/cap20/calculated timestamp producer and snapshot. Full sample-gating rewrite is a separate M11-004 task; field capture must not discard what its consumer needs.
5. Preserve async/sync route order and checked no-handoff history failures; join the carrier at real image ingress and worker Update, including prior/current values. Do not stop camera processing while filling the carrier or treat the host ProcessImage overload as production proof.

## Regression designs through production image input

These are independent source-driven expectations, not tests run or a candidate self-roundtrip oracle. Send actual RobotState/ImageImuData and complete image chunks through the dispatcher; install calibration through the real supported calibration input. A scheduling seam may pause the worker but must not replace QueryFramePose/PoseData.

- **Time selection:** send history at100 and200 with distinguishable head/lift/state; image timestamp150, then image250. Assert source-chosen returned history time/state versus image timestamp retained for image processing, exact f32 camera transform, copied frame/origin/status fields. Change history before worker runs; ingress payload stays captured. Existing B34 CameraUsesTheRobotStateCapturedAtHandoff… checks only flat X and image timestamp, not complete assembly.
- **Current-frame lookup independence:** create a newer current EngineRobot pose frame different from computed image frame using native SetNewPose/history sequence. Assert latest-frame query occurs and diagnostics on its miss do not add a new no-handoff branch; camera/state come from computed image-time output. Do not script GetLastStateWithFrameID's return to stand for production query success.
- **Parent/copy test:** use a nonidentity origin parent and nontrivial robot transform. After Set, stored robot local transform matches computed local transform with no parent; source keeps its original parent; camera parent is stored robot and local camera transform matches neck-precomposed historical pose. Move through mailbox/previous-current transfer, dispose temporaries and assert parent identities, transforms and retained state remain valid. An identity-origin-only image test cannot distinguish flatten from detach.
- **LUT cases:** install calibration, inspect source-produced f32 keys/H via independent native instruction/oracle fixtures; test exact key, between keys (upper selected), below min, above max and empty table. Failure setsfalse/zero H but still hands to processing; visibility false suppresses ground-plane result through the native gate, not every vision mode. Reinstall identical pointer versus changed calibration tests actual producer dispatch and no invented table clear.
- **IMU capture:** feed more than20 samples with distinct keys/u8 values/rates; actual image metadata drives current-key timestamps and prior-key predecessor fill. Assert cap20 and exact computed-time words before snapshot. Feed additional IMU/history while worker delayed: frame snapshot remains ordered/unchanged. Include f32/truncation boundary cases with independently calculated literals/native oracle; do not compare to current Imu.WasRotatingTooFast as oracle.
- **No-handoff:** preserve empty-history failure and real frame-mismatch/origin-mismatch fixtures from M3M4ReviewFixTests; assert zero processed frame, zero Set/carrier publication and correct failure diagnostic. Also disabled/no-calibration gates leave no new carrier; verify earlier timestamp/IMU side effects only where source ordering reaches them. Above-max homography and GetLast miss are explicitly not these failure cases.
- **Delivery/world effect:** carry a real frame through worker and successful downstream processing, inspect returned VisionFrameResult/API pose fields and applicable ground-plane/world outputs; assert public callbacks run outside mutation gates as B34 already requires. Keep marker/face source uncertainties visible; a rendered-image success alone does not certify their source fidelity.

## Ownership, unresolved scope and approval

Current M11-049 title is **enabled flag**, not the whole assembly. Its unresolved note is where the restored camera boundary was parked; it does not transfer native history/geometry/LUT ownership automatically. M11-033 owns image handoff/Update overloads, M11-040 owns component storage/thread transfer, M11-097 owns history query results, M11-003/054 own camera geometry, M11-017 owns homography-fed overhead content, M11-004 owns IMU/gates, pose records own node arithmetic/lifetime. Manager should allocate the new assembly rows explicitly in inventory/evidence (or a new bounded record if needed), retaining those joins, before settling a whole-path claim. No manifest/inventory/title/status is changed by this research.

Remaining **UNKNOWN** units: native EncodedImage+18/+1C producer/semantic names; complete GroundPlaneROI+A0 initialization/mutable helper state; full VisionSystem+F8 anchor producer/lifecycle for downstream reparenting; supported exceptional NaN/LUT or malformed image-IMU input reachability; complete detector and teardown paths beyond the cited consumers. Each stays with its owner above, not an invented carrier field or an exclusion. The normal success assembly, exact call arguments, result handling, copies, detach and timing are established by the cited instructions. This answers the bounded request without claiming those separate units built or irrelevant.

No code/tests/manifest/inventory changes, hardware work or settlement. Only this named answer is published. After independent checking, newly frozen source rows require the layer's normal inventory/evidence approval; implementation differences are allowed only with the same observable results.

## Current primary records

Verbatim current title/status/evidence fields at examined HEAD. Some earlier evidence descriptions are stale (notably M11-033's “pose/image guards” names and grey-only assumption); later adopted corrections retain precedence, as discussed above. No stronger claim is based on that stale wording.

```json
{
  "id": "M11-033",
  "title": "The image hand-off and the two VisionSystem::Update overloads",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionComponent::SetNextImage 0x00652B04; Processor 0x00651F08; UpdateVisionSystem(pose, image) 0x00653D30",
    "VisionSystem::Update(PoseData, EncodedImage) 0x006B4B68: IsColor 0x006B4B7C, DecodeImageRGB 0x006B4B90 or DecodeImageGray 0x006B4C02, then Update(PoseData, ImageCache) 0x006B4C78",
    "Update(PoseData, ImageCache) 0x006B4D5C: the pose and image guards 0x006B4D66/0x006B4D70, UpdatePoseData 0x006B4D88, GetGray 0x006B4D94. ImageCache layout: grey Image at entry+0x14, RGB ImageRGB at entry+0x54, grey-valid +0x94, RGB-valid +0x95 (ResetHelper<Image> 0x008744F0, ResetHelper<ImageRGB> 0x0087459E, GetImageHelper 0x0087465C). An RGB entry becomes grey via ImageRGB::FillGray 0x00872998 -> cv::cvtColor(rgb,gray,COLOR_RGB2GRAY=7,0) 0x008729CA (libopencv_imgproc.so 0x2BC78). EncodedImage::IsColor 0x4F2100 (encoding byte at +0x20, tbb table 0x4F2110). For the shipped 320x240 grey camera the colour branch is inert: only the grey member is stored, +0x95 stays 0 and FillGray is never called",
    "re-analysis/research/20260929-R-VIS-pre-extraction.md Part 2 item 8; re-analysis/research/20260929-R-VIS-verify-pre-items7-8.md (rows C1-C11 PASS)",
    "re-analysis/research/20260929-R-VIS-verify-pre-items9-10.md (Processor and the Update overloads)"
  ]
}
```

```json
{
  "id": "M11-040",
  "title": "The VisionComponent mailbox and processor thread",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionComponent::SetNextImage 0x00652B04; Processor 0x00651F08",
    "re-analysis/research/20260929-R-VIS-pre-extraction.md Part 2 item 9; re-analysis/research/20260929-R-VIS-verify-pre-items9-10.md (rows PASS with the corrections listed there)"
  ]
}
```

```json
{
  "id": "M11-049",
  "title": "The enabled flag: images are dropped until the NV calibration read callback has run",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0065AE80 the enabled write; 0x0065AB68 the callback; 0x006583A4..0x00658414 the registration and SetCameraParams; 0x006500EE the ctor",
    "re-analysis/research/20260929-R-VIS-M11-gap1-extraction.md Q7; re-analysis/research/20260929-R-VIS-verify-M11-gap1.md (Q7 PASS, own encoding scan)"
  ]
}
```

```json
{
  "id": "M11-097",
  "title": "RobotStateHistory query and admission results: ComputeStateAt, GetLastStateWithFrameID and AddRobotStateToHistory's result",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261018-m3m4-settling-b.md: no record owned these; consumers M4-019, M4-038, M10-007, M10-015, M11-044, M13-030, M14-005. Prior reads: ComputeStateAt 0x00531784..0x00531808 (research/20260929-R-VIS-pre-extraction.md); AddRobotStateToHistory 0x00512B9C..0x00512F0C (on-ramp 0x00512BE2, ContainsOriginID 0x00512C44, failure 0x00512C38); the C12.4 gate 0x00513088..0x00513110.",
    "re-analysis/research/overnight/20261018-ov128-m3m4-settling-narrow.md, confirmed with corrections by re-analysis/research/overnight/20261018-ov128-check.md: ComputeStateAt vision-only exact key first (H1, 0x00531792..0x005317E6), raw query (H2), vision correction chain (H9, 0x00531808..0x00531AC6; the frame-increase delta accumulates before the stop test 0x00531A7A..0x00531A7E); GetLastStateWithFrameID raw then vision-only, newest first (H10, H11, 0x00531E34..0x00532090); UpdateFullRobotState admission and result paths (A2-A5, A8; 0x00512BAC..0x0051313C, AddRobotStateToHistory 0x00513598..0x005135C0; x, y from the flattened incoming pose; tail returns UpdateCurrPoseFromHistory ^ 1).",
    "re-analysis/research/20261019-b-m3m4-33-check.md, SetNextImage caller re-read by the manager 2026-10-19: r6 = image timestamp [sl,#0xC] (0x00652C22); GetNewestTimeStamp(robot+0x390) (0x00652C2A); `cmp r0,r6; it lo; movlo r6,r0` (0x00652C34..0x00652C3A): the query time is min(imageTs, newest raw key); ComputeStateAt(t, &state sp+0x11C, &time sp+0x120, flag 1) (0x00652C44). Result 0 -> 0x00652CFC (pose data built, frame handed on). Result 0x06000000 -> a log (0x00652C58..0x00652C78), return 0 (0x00653158). Any other result -> a log with the history's sizes and newest/oldest times (0x00652DD6..0x00652E32), return the result (0x0065315A). Neither failure hands the frame on."
  ]
}
```
