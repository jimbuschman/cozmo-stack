# R-M11-POSE-SERIAL — supported PoseStruct3d boundaries

Request: `requests/20261021-m11-pose-serialization-callers.md`. Examined HEAD **46c8a213**. Independent native extraction/read-only comparison, pending checking. Coverage was opened before tracing. Production paths below are relative to `cozmo-stack/src/Cozmo.Robot/`.

| Boundary | Native instructions / input and branch | Current production reach | Record | Disposition |
| --- | --- | --- | --- | --- |
| Inbound converter | Pose3d(PoseStruct3d,origins)0x00846C70..D16: f32 quaternion→f64, normalized constructor; copy3 f32 translation words; GetOriginByID(input+1C); parented PoseBase constructor. | Pose3d exposes quaternion/parent primitives, but no PoseStruct3d conversion API. `EngineRobotState.cs`:7 EngineGamePose is an outbound data record, not this constructor. | M11-065/066, origin owner | UNBUILT common input join for supported callers below. Reuse corrected R31 arithmetic; no new scalar extraction. |
| Outbound converter | ToPoseStruct3d0x00846D38..DEE: flatten to root, get root ID, verify membership and parent invariant, write translation, narrow four quaternion doubles to f32, write ID. VERIFYs continue to output. | `Sensors.cs`:340–352 supplies planar quaternion/current OriginId; `EngineRobotState.cs`:105 narrows but lacks native full root validation. | M11-065/066; callerM1-053 | UNBUILT full converter/input supplier. Narrowing itself already exists for M1-053; don't rebuild its checked tick projection. |
| Robot state publication | GetRobotState0x005180E0..18106: robot GetPose then converter with robot+294 origins;32-byte struct copied into output. HasReceivedRobotState gate/tick publication adopted P2a. | `CozmoRobot.cs`:329→Sensors.PublicationPose→PublishRobotState91/105→public RobotStatePublished. | M1-053, supplierM11-065/066 | SUPPORTED live boundary needing full quaternion and root-ID input, not only yaw/current origin. |
| Observed object report | BroadcastObjectObservation61FF7C..FFDE: object.GetPose→converter(origins)→RobotObservedObject→Robot.Broadcast. | `Vision/BlockWorld.cs`:1700 ObjectObserved event; snapshot is ObjectObservation with object pose; `ObjectBehaviors.cs`:54/80–90 subscribes and uses obj.Pose directly. | M11-038; consumerM10-003/M8-014 | UNBUILT native message conversion/typed API join. Do not omit local float conversion merely because app socket is replaced. |
| Located-object states | BroadcastLocatedObjectStates61E6C0..E850 creates closure/filter, enumerates with FindLocatedObjectHelper, broadcasts list; closure628406..8496 converter at843C appends52-byte native state. | `Vision/BlockWorld.cs`:848,2865 / `NativeDelocalizationWorld.cs`:62–63 counted unbuilt broadcast. | M11-038 | UNBUILT supported state-report boundary, also called by sweep/delocalize. A game-request entry alone does not make the whole producer SDK-only. |
| Face report | AddOrUpdateFace4F4B06..B82 converter on stored pose pointer with robot+294 origins, copies pose into RobotObservedFace construction then Broadcast. Input pose producer selected in AddOrUpdateFace; early history/origin gate4F42A4..42F2. | `Vision/Faces.cs`:307–351 AddOrUpdateFace raises FaceObservation from FaceEntry directly; `VisionSystem.cs`:1153 calls it. | M14-008, with M14-001 pose producer | UNBUILT serialized pose/API boundary. Full face estimator/lifecycle remains its owner, not re-extracted here. |
| Whiteboard observed object |56C448..C47A: msg+20→inbound converter(origins); type msg+8; RemovePossibleObjectsMatching then render. | `Behavior/AIWhiteboardHandlers.cs`:102–119 accepts Pose3d directly; `Manipulation/AIWhiteboard.cs`:111–118 subscription seam has no src implementation/writer found. | M8-014 | UNBUILT local object-message subscription/conversion join (tag44). A direct helper unit test is not production reach. |
| Whiteboard possible object |56C48A..C4B6: msg+20→converter; msg+8 type; ConsiderNewPossibleObject. | `AIWhiteboardHandlers.cs`:135 onward exists with separate pose/world gaps; same unwired external seam. | M8-014 | UNBUILT input join. Complete tag45 native producer/caller census UNKNOWN here. |
| Object-position reaction |61153C..1576 tag44/current-behavior guard→HandleObjectObserved6114A0..1526; type-set filter; msg+20→converter6114D4; carried/docking ID match chooses HandleNewObservation overload. | `Behavior/ObjectBehaviors.cs`:54,80–105 uses observed object's original Pose3d for reaction records; real behavior constructed in `Behaviors.cs`:732–733. | M10-003 | Supported recipient, missing float/origin reconstruction. Producer and consumer both require the conversion boundary. |
| Fixed custom object input |61E1EC..E232 receives PoseStruct at message start→converter; reads size floats at+20/+24/+28→CreateFixedCustomObject; result to G2E caller's external reply61E226. | No CreateFixedCustomObject production C# method/message ingress found. | M11-042 custom-world body; SDK resetM11-056 is separate | Host support and exclusive app/SDK provenance UNKNOWN; no new exclusion. Conditional constructor requirement if this feature is supported. |
| Indirect census limit | Resolved PLT4ACC34/C1 and4A5BF0; direct branch scan and known ARM veneer scan find4 input/4 output call sites; callback628406 is in .data.rel.ro vtable102DE74. | Current searches named APIs plus actual call bindings above. | Owning records above | Arbitrary computed/indirect calls or other binaries' dynamic imports not exhaustively closed. Do not call this a universal caller proof. |

## Evidence and bounded reuse

Used `re-analysis/decomp/libcozmoEngine/index.tsv` to locate functions and navigation caller lists, then read actual Thumb instructions in shipped `resources/lib/armeabi-v7a/libcozmoEngine.so`; PLT/GOT and ARM add-PC veneers resolved independently. Reused adopted `20261013-pose3d-arithmetic.md` R31/R32 with `20261014-pose-rows-recheck.md` corrections, plus `20261014-pose-tree-verifies.md` and `20261013-small-reads-1-check.md` for parent guards. These already recover arithmetic; this pass scopes its production boundaries.

Also reused current M1-053 evidence/adopted P2a–P2m (`20261009-M1-053-projection-rows.md`, manager adoption recorded in manifest/jobs/B-M1M2) and `20260929-R-VIS-verify-pre-items4-5.md` checked LocatedObjectStates/closure rows. Earlier notes/verify packets are leads where they have no explicit adoption. F5 in `20261021-m11-pose-history-settling.md` remains pending; it is not proof that every mathematical serialization API is a supported feature. No frame-pose/camera assembly, whole pose-tree audit, code, tests, hardware or settlement was performed.

### Census scope

ELF GOT identifies one converter PLT each:4ACC34→846C70 and4A5BF0→846D38. Decoded `.text` Thumb BL/BLX/B/B.W immediates to body/PLT, then repeated including recognized 16-byte Thumb→ARM PC-relative veneers. Found these call instructions:

- Input: **56C45E,56C4A0,6114D4,61E1FE**.
- Output: **4F4B1A,5180F0,61FF90,62843C**.

Each is read at its caller boundary above. Raw section-word search of exact Thumb body pointers846C71/846D39 found only dynamic symbol table entries; located closure pointer628407 is at `.data.rel.ro`102DE74. Closure setup at61E716..E74E installs address point102DE5C, captures BlockWorld and the output vector, then invokes FindLocatedObjectHelper61E78E; its function slot at102DE74 establishes the indirect **caller of the closure**, whose body directly invokes ToPoseStruct3d. A “no direct caller” decompiler label on628406 would miss this live path.

This is not a proof against computed addresses, unrecognized thunks, alternate decoding/inlined conversions, or imports in every other library. Those remain **UNKNOWN**. No new negative claim about all SDK or app-only callers follows from this finite census. Literal pools/exception cleanup were not treated as extra production functions.

## Converter contracts that callers require

**Inbound** at846C70..D16 reads input{translation[0..8],quaternion[C..18],originID[1C]}. It widens all four quaternion f32 values to f64, calls the normalized UnitQuaternion constructor849D86 and Rotation3d constructor84A656, retains the three translation words, calls PoseOriginList.GetOriginByID with input originID and constructs a pose parented to that returned origin. Name is an empty string. Reuse corrected R31/normalization rows, including zero/nonfinite behavior already owned there; this report doesn't re-extract them.

Missing origin is **not an early “drop message” branch**: GetOriginByID847C20..CD8 looks up the origin, otherwise emits its VERIFY and returns a lazily initialized static default Pose3d. Valid lookup returns the origin entry's stored pose pointer at847C58. On miss, construction still proceeds with the fallback parent. The fallback pose's runtime ID is not established here as equal to the requested ID or zero; do not manufacture that identity. Existing origin-list/pose-constructor owners must provide it if a reachable unsupported-origin input needs building. This is defined native handling, not M1-034 simply because it logs an error.

**Outbound** at846D38..DEE uses GetWithRespectToRoot, obtains that temporary's root ID (or0 when its node is null for the ID query), then ContainsOriginID. UnknownOrigin VERIFY846D60..D68 **continues**. It checks the original source's node/parent, Dev_AssertIsValidParentPointer atD76; when source has a parent, obtains the flattened temporary's parent and compares that parent ID with the chosen root ID. BadParent VERIFYD8E..DA6 also **continues**. It finally copies three f32 translation words and narrows four f64 quaternion components to f32 atDBE/DCC/DD0/DD4, writing rootID atDCA. Do not replace VERIFY by a false return, report readiness failure, or suppress the event. Full null-node crash reaches vldmia at846DB6 on a null temporary node; upstream reachability remains UNKNOWN and M1-034 governs actual fault paths without inventing zeros as source behavior.

The output is32 bytes in schema order **x,y,z,q0,q1,q2,q3,originID**; seven f32 then U32. Quaternion order is scalar-first, matching the checked UnitQuaternion storage and generated `unity/scripts/csharp/Anki/PoseStruct3d.cs`:169–195. App-boundary C# APIs must deliver the same numeric values/origin identity even if no raw bytes are serialized. Native local delivery embeds this narrowed struct too: bypassing encoding bytes is allowed, bypassing float narrowing→widen/normalize→origin-parent lookup is not established equivalent.

## Supported output paths and fields

### RobotStatePublished

Robot.GetRobotState5180D8 calls GetPose4EA398, then ToPoseStruct3d with robot+294 origin list at5180E6..F0; copies all32 bytes into ExternalInterface::RobotState at5180FE..18106. UnknownOriginID0 additionally logs Robot.GetRobotState.BadOriginID at51810C..122, then continues. Existing manager-adopted tick gate only publishes after HasReceivedRobotState; preserve it and the rest of the109-byte projection. The converter's verification is not a readiness gate.

Current production: CozmoRobot329 binds Engine.PublicationPose to Sensors.PublicationPose; VisionSystem91 repeats that binding. Sensors340..352 does use **MainPose.GetWithRespectToRoot**, but then discards its full quaternion and regenerates one from its yaw through EngineLocalizationPose.FromPlanar, choosing **CurrentOriginId** rather than the converter's root ID. EngineRobotState105 already casts returned quaternion components tofloat and packs correct32-byte pose order at24. `NativeRobotStateHistory.PublicationPose`320 is a different convenience property; no live PublicationPose binding to it was found and it cannot be substituted for this supplier.

Therefore the minimum required live change is the source-backed root pose/full quaternion/root ID and origin validation join, not a new publication system. Nonplanar MainPose transforms are supported upstream state, not established absent merely because robot odometry is often planar. M1-053 remains EXACT_SOURCE with supplier obligations explicitly handed to M11; this answer does not reclassify it. Quote its current record before claiming any stronger contradiction; the gap is its existing higher-layer supplier boundary.

### ObjectObserved and LocatedObjectStates

BroadcastObjectObservation61FED8 first projects the object and forms an image rectangle, optionally obtains top-marker orientation for the checked block-kind case, then reads object's ID/state/type fields and GetPose61FF7E. Converter61FF90 uses robot's real origin list. It copies32 bytes into message+20 at61FFB2..FFC6, constructs RobotObservedObject61FFD6 and broadcasts61FFDE. Type-family/top-marker errors can take the existing logged failure branch620026..7A; no new converter failure gate exists. Full projection/error internals remain M11-038, not re-extracted here. Message generated source corroborates pose field32 bytes; schema/whole69-byte report beyond this pose remains that record's obligation.

This is emitted during supported world observations, so it isn't excluded by app-socket replacement. Current BlockWorld emits ObjectObservation directly from the object; its known rectangle/top-face differences are already M11-038 gaps. Both public API view and local recipients need a captured converted pose, not a later read from a mutable world object. This pass does not invent a new object snapshot schema for the other fields.

LocatedObjectStates61E6C0..E850 constructs a filter/output vector, invokes FindLocatedObjectHelper with returnFirstOnlyfalse at61E78E, then constructs list message725472 and Robot.Broadcast61E7B4. Closure628406 gets object.GetPose at842C, origin list from captured BlockWorld's robot at8436, converter843C, then appends a52-byte native record with pose at entry+10 (8452..5A), pose state byte+30 and connection byte+31. Existing checked generated wire state is50 bytes; native padding/layout is not the wire format. Native request entry61E6BC is only one caller; adopted pre-items4–5 also identifies sweep6206E6, and current NativeDelocalizationWorld62 names delocalize6249EC. Hence the entire path cannot be called “SDK only” from its game-request entry. Current broadcast is a counted MISSING; converter ownership is M11-065/066, list carrier/consumer M11-038. Readiness effects of all list recipients remain UNKNOWN here; the API-delivery obligation alone keeps the conversion relevant.

### FaceObserved

At4F4B06..B1A, FaceWorld uses stored face-entry values and a stored pose pointer(sp+3C), robot+294 origins, then calls ToPoseStruct3d. The pose result atsp+50 is supplied to RobotObservedFace construction4F4B62..B72, E2G construction4F4B7A and Broadcast4F4B82. Entry pose was constructed/selected by AddOrUpdateFace, starting with tracked face+F4 copied at4F42CA..D0 and parented to the origin identified by historical robot state4F42C4..42F2; history-query failure/null state exits before this branch4F42BA..C2→4372. Existing later face-entry selection/no-parts/update/recognition gates remain M14-001/008; their complete dataflow to sp+3C is **not newly re-verified in this bounded boundary read**, so do not treat the caller table as whole face-path approval.

Current AddOrUpdateFace307..351 stores a Pose3d on FaceEntry and raises FaceObservation without the32-byte converted pose or captured origin. Existing face result handoff VisionSystem1153 is production; face recognition/runtime gaps remain separate. A full source-backed face report must use the narrowed quaternion/root-ID projection at this boundary; a unit event with a constructed FaceEntry cannot establish native estimator/source fidelity.

## Supported local input paths

### AIWhiteboard handlers

Observed-object tag44 native handler56C448 constructs from message.pose at+20 and robot+294 origins (C454/C458/C45E), passes type+8 and reconstructed pose to RemovePossibleObjectsMatching(C462..468), then render(C46E). This changes possible-object state for later behavior. Possible-object tag45 similarly reconstructs at56C4A0 and passes to ConsiderNewPossibleObject56C4AA: tilt, relative-height and matching/retention decisions use that reconstructed pose, not the pre-broadcast native double quaternion.

The exact tag45 producer/field-source path is **UNKNOWN here**; the located conversion handler does not prove a live full tag45 producer. Treat that join as M8-014's unbuilt input, not an unsupported product decision. For tag44, the supported producer above is direct evidence. Current AIWhiteboardHandlers102/135 take Pose3d; AIWhiteboard.Init111–118 asks IWhiteboardExternalInterface to subscribe. A src search found its interface and property but no implementing class/production assignment; handlers' own header records the seam. Thus these mathematical helper methods cannot vouch for production conversion/delivery. Minimal build must bind real local reports and reconstruct using known origin-list semantics; don't create synthetic receive success or couple the whiteboard directly to original world poses without proving equal observable values.

### ObjectPositionUpdated reaction

61153C handler checks tag44, obtains current behavior and compares its class against strategy byte+54; absent/current-same behavior skips611576 call. HandleObjectObserved6114A0 first looks up message family/type key at+4 in the supported set; absent returns611524 without conversion. Present constructs input pose from message+20 with robot+294 at6114D4, compares message objectID+0C against carried and docking getters, then chooses HandleNewObservation(...,false)611508 or robot-aware overload61151A. Those recipients update observed/reacted poses/time and later reaction decisions. No independent pose readiness gate is added by reconstruction.

Current real strategy is built in Behaviors732–733, subscribes world.ObjectObserved atObjectBehaviors54, checks manager/current class and family, then records **obj.Pose** at90 instead of the native narrowed/reconstructed report pose. Thus exact input conversion is needed for this supported behavior even if no host asks for serialization. Existing reaction math/threshold/tree comparison gaps stay M10-003; this read closes which representation arrives at their input, not their complete algorithm. Use a captured converted report so later object mutation cannot silently change stored observation values.

## App / SDK separation without new exclusions

The four output sites above are engine report/state paths; they are not made out of scope by replacing Unity screens or the network socket. Robot.GetRobotState also has a known non-live developer BehaviorDockingTestSimple caller (M7-022, current M1-053 evidence); that caller doesn't remove the ordinary tick's supported projection.

Fixed custom objects: native HandleMessage<CreateFixedCustomObject>61E1EC takes the message's leading PoseStruct, dimensions+20/+24/+28 and invokes CreateFixedCustomObject61E21A; there is **no SDK-mode test in that handler body**. The helper61E244 first allocates an object, copies pose61E264, then reparents the copied pose to current robot pose's parent61E274..284 before ID/state insertion. This does not cancel the earlier float-quaternion normalization/origin lookup, including defined fallback diagnostics. A complete success/failure/object/response extraction beyond61E232/that reparent slice is outside this request and remains M11-042.

Current C# src has no fixed-custom-object API/handler, and an app C# search found only generated message/union references for this name, not a source-backed ordinary app sender. **Exclusive SDK provenance and operator support decision remain UNKNOWN.** Neither absence in host code nor a game-message wrapper authorizes a new “not supported” classification. M11-056 currently remains live RECOVERABLE_GAP for SDK reset recipients; it is not an existing per-feature exclusion for CreateFixedCustomObject. Manager must reconcile any requested custom-feature scope under the already approved app/SDK boundary, using an actual sender/gate trace. Until then the inbound converter slice is conditionally required for custom support and definitely required by the supported local handlers above. No SDK-only caller has been positively established among this finite set; no blanket exclusion is made.

## Minimal build and independent checks

Recommendation after checking/adoption: build one shared source-backed32-byte pose value/conversion pair using existing checked quaternion/tree/origin primitives. Bind **outbound** at MainPose publication, object observation/list and face report boundaries; bind **inbound** at tag44/45 whiteboard and object-position reaction. Do not repeat scalar arithmetic, replace the whole pose tree, or pretend this settles unbuilt message recipients. Fixed-custom-world input awaits bounded provenance/support disposition; unresolved indirect callers remain explicit. C# can bypass raw packing for local delivery while retaining the exact float conversion and normalization boundary and parent/origin association.

Source-driven regression designs, not tests run:

- MainPose under a known nonidentity origin, with nonplanar unit quaternion(0.5,0.5,0.5,0.5): real tick publication must contain the root-relative transform and f32 projected quaternion/root ID. It must not regenerate yaw-only q. Check exact independently specified seven-float/U32 bytes, not ToPackedBytes roundtrip.
- Known origin7 plus input quaternion(2,0,0,0), translation(1,2,3): inbound result has normalized rotation(1,0,0,0), same translation words and parent equal to origin7's actual node. Unknown origin exercises GetOriginByID's logged fallback and continued construction, not rejection or forced requested-ID parent. Use existing normalization oracle for exceptional values.
- Supported object report→synchronous local handler: choose quaternion components whose float narrowing changes the double value; assert recipients use narrowed→widened/normalized rotation with correct origin, and captured pose survives later object updates. Check both the reaction observation and whiteboard state effects near their existing sourced thresholds, without making candidate serializer its own oracle.
- ToPoseStruct3d unknown-origin/BadParent diagnostics must not suppress otherwise defined publication; don't build an artificial readiness gate. Actual null-node fault scenario uses M1-034 prior-state/visible-skip, not a fabricated zero pose.
- Located-state broadcast through a real supported sweep/delocalize and face report through actual result handoff: compare captured pose values/origin against external fixtures. A manually invoked converter is a unit test, and cannot prove the missing producer/report subscription joins or face estimator.

No tests/builds/hardware were run; this answer changes only its named research file. The manager checks cited rows, assigns supplier/message joins to the current layer inventory and obtains any required evidence approval before building. No status changes, policy exclusions, approvals, release of builder work or settlement follow automatically. Remaining UNKNOWNs are the full tag45 producer, complete face-entry selection proof in this pass, exclusive fixed-custom SDK provenance/support, fallback runtime origin ID and full arbitrary indirect/other-binary caller closure.

## Current record snapshot

Verbatim title/status/evidence fields at examined HEAD, rather than recalled titles. Snapshot statuses are not changed by this report.

```json
{
  "id": "M10-003",
  "title": "Reaction-strategy factory rules and strategy classes (Cliff, Falling, PickedUp, Shaken, Slope, Frustration, PlacedOnCharger, Sparked, NoPreDockPoses, FistBump, Hiccup, Pet, CubeMoved, FacePositionUpdated, ObjectPositionUpdated)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "C12 factory switch 0x60D618; C13..C17 Generic, Shaken, Slope, Frustration",
    "gap1 1 Cliff filter 0x60DC7C..0x60DCA2; gap1 8 strategy predicates",
    "gap2 1..6 PlacedOnCharger 0x614474..0x6144D0, CubeMoved 0x60C04A..0x60C1E2 / 0x60BEB4, Face 0x60CB84..0x60CE20, position-update base 0x612168..0x612B22, Object 0x6114A0..0x611582",
    "R-ANIM pre-extraction part 2 item 5.1: EnabledStateChanged +0x1C is a no-op (0x60B73B) for Shaken/Slope/Frustration"
  ]
}
```

```json
{
  "id": "M14-008",
  "title": "Vision face-result handoff and FaceWorld lifecycle broadcasts",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x006551E8..0x00655202 walks the std::list<UpdatedFaceID> at VisionProcessingResult+0x54 ({int oldID, int newID, string name}) and calls FaceWorld::ChangeFaceID 0x004F3DB4 for each before the rest; 0x00655204..0x0065526C marks FaceWorld+0x24 when a tracked-face node has +0x14>0; 0x00655274 calls FaceWorld::Update.",
    "0x004F5316..0x004F535C calls AddOrUpdateFace per tracked face and 0x004F5380..0x004F54A8 expires unnamed faces after 15000 ms.",
    "0x004F4B7A/0x004F4B82 broadcasts RobotObservedFace; 0x004F3C2C..0x004F3C64 broadcasts RobotDeletedFace, erases visualization and erases the map node."
  ]
}
```

```json
{
  "id": "M11-038",
  "title": "The BlockWorld game-broadcast entry points",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BroadcastObjectObservation 0x0061FED8; BroadcastLocatedObjectStates 0x0061E6C0; BroadcastConnectedObjects 0x0061E91C; VisionSystem::CheckMailbox 0x006B2AD4",
    "re-analysis/research/20260929-R-VIS-verify-pre-items4-5.md (items 5a-5c PASS)",
    "re-analysis/research/20260929-R-VIS-M11-gap1-extraction.md Q6; re-analysis/research/20260929-R-VIS-verify-M11-gap1.md (Q6 PASS)"
  ]
}
```

```json
{
  "id": "M8-014",
  "title": "AIWhiteboard: init handlers, the no-op update and AddBeacon",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "AIWhiteboard::Init 0x0056a394 registers three external-interface handlers when the robot has an external interface (HasExternalInterface 0x0056a39c; three bl at 0x0056a3b8/0x0056a3be/0x0056a3c4 to 0x56a444/0x56a504/0x56a5c4); otherwise it warns \"Initialized whiteboard with no external interface. Will miss events.\" (0x0056a3d2).",
    "AIWhiteboard::Update 0x0056a684 is a no-op (bx lr).",
    "AIWhiteboard::AddBeacon(pose, radius) 0x0056c39c appends an AIBeacon (Pose3d plus a float at +0xc) to the vector at +0x60 (vstr s16,[r0,#0xc] 0x0056c3c0; str r0,[r4,#0x64] 0x0056c3ce) and calls UpdateBeaconRender 0x0056c3de.",
    "AIWhiteboard::Init's three registrations are MessageEngineToGame subscriptions: tag 0x44=68 RobotObservedObject (0x0056a44c; handler functor 0x0056cbd6 -> AIWhiteboard::HandleMessage<RobotObservedObject>), tag 0x45=69 RobotObservedPossibleObject (0x0056a50c; 0x0056cc4e), and tag 0x35=53 RobotOffTreadsStateChanged (0x0056a5cc; records BaseStationTimer::GetCurrentTimeInSeconds at whiteboard+0x48, 0x0056ccf2/0x0056ccf6).",
    "AIWhiteboard::UpdateBeaconRender 0x0056aa3c..0x0056abff is a real 452-byte body, not a bare interface: it erases the previous segments through VizManager::EraseSegments (PLT 0x004acb20) and draws each beacon at +0x60 as three XY circles (VizManager::DrawXYCircleAsSegments<float>, PLT 0x004acb50), loop bound whiteboard+0x64. VizManager and the three handler bodies are not recorded by M11/M12 either; they are unowned."
  ]
}
```

```json
{
  "id": "M1-053",
  "title": "Outgoing RobotState publication from the engine tick",
  "status": "EXACT_SOURCE",
  "evidence": [
    "UpdateAllRobots enters from engine tick 0x004ED648; Robot::Update then HasReceivedRobotState, GetRobotState and external slot +0x1C at 0x0052F6C0..0x0052F7B0; counterpart CozmoEngine.TickAt -> PublishRobotState -> public RobotStatePublished API under the approved app boundary.",
    "A second caller exists: BehaviorDockingTestSimple::UpdateInternal calls GetRobotState at 0x005CC31E, copies state to +0x270 and enters state 3 (0x005CC310..0x005CC32C); this non-live developer behavior belongs to M7-022. Remaining callers are not proven exhaustive. Source: 20261006-M1M2-missing-triage.md P2; reopened libcozmoEngine.so.",
    "Manager adopted P2a-P2m: re-analysis/jobs/B-M1M2.md Rows checked (manager, 2026-10-09); re-analysis/research/20261009-M1-053-projection-rows.md. GetRobotState 0x005180D8..0x00518252; pose struct 0x00846D38..0x00846DEE; quaternion angle 0x0084AA1C..0x0084AAB8; Radians 0x0084C832..0x0084C87C; packed 109-byte layout 0x00711970..0x00711A62. EngineRobotState.cs uses binary64 products followed by binary32 norms and native unordered branch.",
    "Lift projection 0x0051818E..0x0051819E includes final +0.0; status 0x005181BA..0x005181E8 starts +0x350, animation !=0 adds40, FF original|840, carrying!=-1 adds2. IDs/gameStatus use owning CarryingComponent/BlockWorld/MovementComponent getters. Tracking constructor -1 at 0x0063DA86; processed-image getter VisionComponent+0xE8 at 0x006527A2.",
    "Correction: static wrapping u16 no-state counter at 0x0105101C increments before >=11 info/reset at 0x0052F720..0x0052F774, Unnamed/RobotManager.UpdateAllRobots/Not sending robot %d state (none available). UnknownOriginID GOT 0x0103E978 -> 0x00C97B20 uint0; warning Robot.GetRobotState.BadOriginID with empty format 0x00BE3F00 at 0x0051810C..0x00518122 precedes angle/head/lift reads. sinf/atan2f are undefined ELF imports (external phone libm). Source: re-analysis/research/20261009-M1-final-opus-pass.md; reopened targets/literals in re-analysis/research/20261009-M1-final-correction-native.txt"
  ]
}
```

```json
{
  "id": "M11-056",
  "title": "SDK camera and custom-world reset recipients",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "Camera auto/exposure/gain settings 0x0065DEDE..0x0065DEFC; color false 0x0065DF14; undefine markers 0x0065DF32; delete custom objects 0x0065DF4E (triage E4)."
  ]
}
```

```json
{
  "id": "M11-065",
  "title": "Pose3d / Rotation3d / Transform3d arithmetic: double unit quaternion, float translation, float point rotation, renormalised composition (rows R1-R34, R38-R40)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-pose3d-arithmetic.md: rows R1-R34 and R38-R40 (storage 0x0084A656.., Normalize 0x00849DB8..0x00849E52, product 0x00849EF4..0x00849F8E, Rotation3d*Point3f 0x0084ACA6 -> UnitQuaternion<float>* 0x00849C1C..0x00849CF8, GetAngleDiffFrom 0x0084A694..0x0084A746, GetAngleAroundZaxis 0x0084AA1C..0x0084AAB8, GetRotationMatrix 0x0084AAE0..0x0084ABEC, Renormalize 0x008494E0..0x0084969E, Transform3d ops 0x0084B92A..0x0084BB98, Pose3d ctors 0x0084683C..0x00846DEE, PoseBase ops 0x0084505C..0x008451EA, IsSameAs_WithAmbiguity 0x00846EA4..0x008471A6, RotationMatrix3d ops 0x0084B0E4..0x0084B1B4)."
  ]
}
```

```json
{
  "id": "M11-066",
  "title": "The pose tree: GetWithRespectTo (null/alias checks, depth walk, Tt^-1*Tf, 1000-step LoopBoundOverflow) and GetWithRespectToRoot (rows R35-R37)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-pose3d-arithmetic.md: rows R35-R37: GetWithRespectTo 0x00845A34..0x00845DFE (FromEqualsTo warning, chain cases a-d, LoopBoundOverflow poseBase_impl.h lines 0x1A0/0x1B6/0x1D4), GetTreeDepth 0x00845E8C..0x00845F02, HasSameParentAs 0x00844D44..0x00844D7C, GetWithRespectToRoot 0x004DF628..0x004DF6DC."
  ]
}
```
