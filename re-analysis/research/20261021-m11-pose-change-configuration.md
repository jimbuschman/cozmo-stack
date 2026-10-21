# R-M11-POSE-CONFIG — pose-change configuration recipient

Request: `requests/20261021-m11-pose-change-configuration.md`. Examined HEAD06b06760. Independent read-only extraction pending checking. Coverage opened before continuing; OPEN entries replaced below.

| Step | Native range / checked source | Disposition |
| --- | --- | --- |
| SetObjectPoseChanged writes | 616D54..7A;624888..92 | Recovered ID-set insertion and Invalid-only cached-pose erase |
| Shared owner initialization/reach | 61BDB6..BDC6;616B58..C34 | World-owned manager, independent from pose-change queue |
| Update gate / order / reset | 616D7C..DDC,616DDE..EFC | Recovered; current C# unconditional rebuild differs |
| Last-pose cache filter | 616F44..71E0; family tableC777D0 | Only current-origin Block/LightCube, root-pose snapshots |
| Known charger consequence | charger family4;616E2A/616E40→EC4 | Can trigger full update despite being absent from last-pose cache |
| Downstream state | OV10 checked K2–K4;64DCAA..DD76 | Rebuild caches, tallestStackHeight write/signal if changed; subscriber effects UNKNOWN |
| Current production | BlockWorld2378; ManipulationSystem38..42; BlockConfigurations147..159 | SetObjectPoseChanged unbound; no native pending-set/threshold owner |

## Answer and scope

**A Known charger observation matters to this recipient.** SetObjectPoseChanged accepts its ID without a cube-family filter. The update gate finds located objects with family=-1, while its last-check pose cache is populated only for Block/LightCube. A located charger absent from that cache counts as movement and causes configuration recomputation. Its physical pose need not have moved by5mm to take this branch. The recipient is neither a harmless log nor a charger-specific event.

The minimal shared owner needs the pending-ID set, last-check root-pose map, force flag, ordered update/reset and existing configuration-cache recipients. Binding the world callback to an unconditional Update is insufficient. Actual complete container population and downstream public-state ownership remain explicit existing gaps, not filled by this answer.

Primary source: actual instructions in `resources/lib/armeabi-v7a/libcozmoEngine.so`; decomp index for navigation only. Reused:
- R-VIS pre-extraction item4b with corrections in `20260929-R-VIS-verify-pre-items4-5.md`: one tag50 subscription, not multiple subscriptions.
- `20261013-blockworld-flag-and-map-helper.md` checked M11-063 queue flag; OV10 adopted check O7/K2–K4 for membership, pruning, tallest stack and public-state signal.
- B-M13-9's already checked charger contact-observation path; no charger pose extraction repeated.

Fresh selected full ranges: Set616D54..7A; Update616D7C..DDC; threshold616DDE..EFC; dispatch616F00..42; last-pose writer616F44..7130 plus cleanup617132..71E0; owner constructor616B58..C34 (literal pool excluded); Notify64DCAA..DD76; GetLocatedObjectByIdHelper61F3E0..F57E (literal pool excluded). Common map/filter/pose primitives remain their existing records. No whole container/geometry audit.

## Native owner and state table

| Row | Instructions | Recovered behavior |
| --- | --- | --- |
| C1 | 61BDB6 alloc0x7C;61BDC0..C6 | BlockWorld constructs one manager with Robot and stores it at world+94. The charger and cube callbacks reach this same owner |
| C2 | 616B60..7C | Initialize subscription vector, force byte+C=0, last-pose map+10 empty, pending set+1C empty (size+24=0). These are separate from world+68 PoseChange list/world+74 flag |
| C3 | 616B7E..92 | Construct stack container+28, pyramid-base+44, pyramid+60 in order |
| C4 | 616B96..BAE; checked helper616C44/tag32h | If Robot.HasExternalInterface()==1, subscribe one EngineToGame tag50 RobotDelocalized handler; otherwise no subscription. Native handler61723A..3E sets force+C=1 (reused checked writer). This is not charger contact input |
| C5 | 624888..92 | All OnObjectPoseChanged paths pass ObjectID and **current object PoseState** byte+24 to manager+94. Old-state argument controls the separate stacked-pose queue only; gate world+74=0 does not suppress this callback |
| C6 | 616D5C..68 | Insert ID uniquely into pending set+1C, including Known/Dirty/Invalid states. Repeated changes coalesce by ID |
| C7 | 616D6C..74 | If current PoseState==0 (Invalid), erase ID from last-pose map+10 immediately. Nonzero state leaves map unchanged; no Known-only or family check here |
| C8 | 616D82..9C | No pending IDs and force=false: immediate return. Force=true: run update even with empty set. Pending nonempty and force=false: call threshold; run update only if result==1 |
| C9 | 616D9E..DBE | UpdateAllBlockConfigs → PruneFullPyramids(base+44, complete-pyramid vector+64) → UpdateLastConfigCheckBlockPoses → NotifyBroadcasterOfConfigurationManagerUpdate(robot+40), in order |
| C10 | 616DC2..DDC | After either full update or “pending but not moved”, clear force and destroy/reinitialize pending set. No-IDs/no-force early return skips this. Reset occurs **after** Notify, not at entry |
| C11 | 616F00..42 | UpdateAll dispatches concrete container update for types0/1/2 at+28/+44/+60, in order, via Container.Update61682C |
| C12 | 625206..20C | Normal BlockWorld.UpdateObservedMarkers tail calls manager.Update with Robot before UpdateMarkerlessObjects62521A. Checked M11-063 places world+74 clear before this tail; failed earlier frame paths may skip it and retain pending IDs |

PoseState names0=Invalid,1=Known,2=Dirty come from the shipped enum; native C6/C7 only distinguish zero from nonzero. No new state exclusion is inferred.

### Threshold gate

Complete616DDE..EFC:
- Iterate pending ObjectIDs in ordered set order. GetLocatedObjectByIdHelper on robot+34 with family=-1 (616E20..2A).
- Fresh full helper61F3E0..57E builds default filter, adds requested ID, omits family restriction when argument=-1 (61F464..78), then calls existing FindLocatedObjectHelper61EB78 with returnFirst=true at61F488. Default origin mode selects robot's current origin (checked R-VIS item5a/M11 filter). No new Known-only predicate is installed by this wrapper.
- Located object, last-check pose missing:616E36..40→616EC4 marks aggregate moved=true, continues scanning.
- Located object, cached pose present: compare stored pose node+18 with current GetPose using Point3f(5,5,5), Radians f32 **0x3F060A92** (approximately30degrees), existing Pose3d.IsSameAs846EA4 at616E88. Result exactly1 continues; anything else exits true616E92..96→EF0. Reuse pose comparison semantics rather than making a new planar-distance test.
- Located object missing: query membership for config types0/1/2 (616E98..EC2, virtual slot0) and set aggregate moved if any membership is nonzero. Reused checked OV10 K2 resolves these slots to AnyConfigContainsObject/ContainsBlock; it is not an update virtual.
- No pending objects→false; otherwise returns aggregate unless comparison already exited true (616EEC..EFC).

This explains why Invalid erasure alone does not force a rebuild: if the object is gone and no configuration contains it, the gate can remain false. Conversely a located non-cube with no cached pose can force a rebuild.

### Last-check poses and charger case

Full UpdateLastConfigCheckBlockPoses616F44..7130:
- Builds default BlockWorldFilter; flags at filter+6C/+6D zero via616FBC.
- Builds allowed-family set from literal table **0x00C777D0={2,1}**, assigns to filter+3C (616FC6..FDE); enums LightCube=2, Block=1. FindLocatedMatchingObjects62632C called617002 through current-origin default filter. Charger family4 is excluded.
- For each returned block, current object pose is converted **with respect to root** through4DF628 (61704E..50 existing entry,61706C..70 new entry), then assigned into existing map node+18 or inserted (61705A/617098). The old R-VIS short “map[id]=GetPose” description omits this root conversion; use the actual instructions.
- It does not clear the entire map first. Invalid callback removes its entry; other stale-entry/cross-origin effects follow existing map and filters, not a reconstructed dictionary limited to only this frame.

A Known charger supplied by checked B-M13-9 OnObjectPoseChanged enters C6. If still located in current origin and absent from this last-check map, threshold's missing-entry branch marks moved. The named normal cache producer never adds family4. Thus repeated charger notifications can continue to request recomputation even with the same pose. This is a **conditional native path**, not a claim every frame observes a charger, that a foreign-origin lookup succeeds, or that no other untraced writer can populate a non-cube entry.

If the charger is not found in current-origin located lookup, membership decides. No cube-membership inference is needed to prove the positive located/missing-cache case. Whether a missing charger can match an unusual configuration ID remains governed by the actual cache, not an invented blanket “charger never matters” rule.

## Downstream state and observable boundary

Reused OV10 adopted K2–K4:
- Container.Update updates configuration caches; pyramid-base pruning removes ordered-ID bases represented by complete pyramids; GetTallestStack retains first equal maximum, empty yields expired weak reference.
- Notify64DCAA..DCC0 obtains the manager's tallest stack and locks the weak pointer. Compares its height (or0 if expired) to **RobotPublicState+6, tallestStackHeight**,64DCE4..DD0E.
- Equal height returns with no signal. Changed height is written as u8 at64DD24; a zero-header event copies public state fieldwise (omitting padding+7) and dispatches64DD68→64DE5E.
- Checked signal64E656..E69C retains nodes, invokes their function targets; stored vtable+18 dispatch64E6FC is the unclosed subscriber boundary. No direct robot-wire or EngineToGame message constructor in this Notify/signal path. **UNKNOWN:** subscriber effects beyond that boundary, including full C# API forwarding; no game message should be fabricated from the name “Broadcaster”.

This already changes in-scope world/configuration state and potentially public state. A charger-triggered rebuild need not emit a new height event if the height remains unchanged. That conditional silence does not establish that the callback has no effect. Correct timing affects which later configuration/behavior reader sees the rebuilt state.

The complete container population is **not closed by this request**. Existing M12-049 explicitly owns unread Container.Update61682C/CastNewEntryToBestSharedPointer62C68C/cache contents; M12-043 owns separate pyramid-base geometry. Retain those gaps. If a builder needs those bodies before delivering a correct shared owner, the next precise boundary is61682C, not a guessed all-pairs cache.

## Current production comparison

All source paths under `cozmo-stack/src/Cozmo.Robot/`:
- `Vision/BlockWorld.cs`:778 declares SetObjectPoseChanged delegate;2378 invokes it, otherwise2379 emits MISSING once per frame. No production assignment to that delegate found. It faithfully exposes a missing recipient, not an installed owner.
- `Manipulation/ManipulationSystem.cs`:38 binds BlockConfigurationManagerUpdate to Configurations.Update.41..42 also calls Update immediately from ObjectObserved/PoseStateChanged. Those immediate rebuilds are not C5's pending-ID coalescing followed by one gated frame-tail update.
- `Manipulation/BlockConfigurations.cs`:constructor only stores world/clock; no pending-ID set or last-check pose map. ForceUpdate property links EngineRobot.BlockConfigurationForceUpdate when native robot exists; this is a real force-bit binding. However Update147 clears it **at entry**, always rebuilds and emits ConfigurationSeen for newly seen host snapshots; it does not implement C8/C10.
- Existing BuildLocked filters cube types, builds all-pairs snapshots and emits MISSING for native pyramid producers. Cube filtering here cannot justify filtering IDs out of SetObjectPoseChanged: they serve different stages.
- Update159 emits MISSING for full native RobotPublicState/Notify owner. Do not treat host ConfigurationSeen as the checked native public-state signal.
- `Vision/BlockWorld.cs`:1541 invokes frame-tail BlockConfigurationManagerUpdate. That is the actual place to bind the checked gated update, retaining the existing order relative to markerless objects.
- B-M13-9 status records three shared-owner MISSING groups; first is this exact M11-063/M11-037 recipient624892. This answer closes its narrow writer/gate/cache relationship, not the entire charger or configuration subsystem.

Tests found: M11RVisBuildTests1495 uses a counting frame-tail hook; M11SmallReadsBuilderTests cover ForceUpdate linkage/delocalization. Those prove their own joins, not pending-ID/threshold ownership or container production. No tests added or executed by research.

## Build scope, regression oracles and uncertainty

After manager checking, implement the shared pending-ID/last-root-pose/force owner at existing world/manager boundaries. Bind SetObjectPoseChanged before supported observation paths can fire; retain IDs until checked frame-tail Update consumes them. Preserve Invalid erasure and non-Known acceptance, threshold/cached-root comparison, full container call order, notification-before-reset, and checked force input. Replace extra immediate host rebuilds only as that native owner becomes genuinely bound; do not use tests as evidence for the all-pairs candidate.

Independent regression expectations:
1. Real charger observation/contact join reaches actual owner, inserts one ID without immediate rebuild; frame-tail sees a located charger absent from cache and recomputes. Identical second notification can request another update; charger never enters family1/2 comparison cache.
2. Repeated same-ID changes coalesce; Known/Dirty preserve cached pose, Invalid erases it immediately. Missing object triggers rebuild only if a real configuration contains its ID.
3. Cached cube within native IsSameAs threshold does not rebuild; exactly matched native angular/translation boundary fixtures use pose primitive results. Pending set clears even when threshold false.
4. Force=true with empty pending set rebuilds; clear force/pending only after container→prune→last-poses→Notify. No force/no IDs does nothing. Exercise a callback-visible order oracle, not counters mirroring implementation.
5. Native-derived cache fixture verifies root snapshots, current-origin family filter and retention of unvisited entries; no global clear before refresh.
6. Tallest height unchanged emits no public-state event; changed height writes+6 before dispatch. Do not expect an unproven EngineToGame report or certify downstream subscribers.
7. Failed frame before native tail retains pending work for the next eligible Update; pose-change queue flag and configuration pending set remain separate.

Remaining **UNKNOWN** boundaries: complete61682C container population and concrete cache ownership where current M12-049 remains unresolved; downstream public-state subscriber effects64E6FC; full multi-thread callback/owner binding design beyond already checked pose-tree gates; any alternate writer of last-check map outside the named routines. No whole configuration audit, code/tests, policy decision, manifest/inventory changes, approval, hardware or settlement performed.

## Current manifest snapshot

Exact current titles/status/evidence are quoted below. M13-014 is a behavior record whose configuration force input is referenced by the implementation; it does not own the whole configuration manager. M11-063 owns world+74 queue gating, not every configuration callback merely because the call shares its path.

### M13-014

```json
{
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
  ]
}
```

### M11-037

```json
{
  "title": "The BlockWorld frame sequence for observed markers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BlockWorld::UpdateObservedMarkers 0x00624EE8: non-empty path is ClearOccluders 0x00624F98, AddLiftOccluder 0x00624FA4, CreateObjectsFromMarkers 0x00624FC4, AddAndUpdateObjects 0x0062505A, then (on its success) CheckForUnobservedObjects 0x006250CA, UpdatePoseOfStackedObjects 0x006250D0, BlockConfigurationManager::Update 0x0062520C, UpdateMarkerlessObjects 0x0062521A; the empty-list branch 0x625020 does ClearOccluders, AddLiftOccluder, CheckForUnobservedObjects 0x0062504E and skips the stacked-pose update. AddLiftOccluder body 0x6564D8 (raw state at the timestamp, Robot::GetLiftTransformWrtCamera, Transform3d::ApplyTo, Camera::Project3dPoints, Camera::AddOccluder with the transform z scale); BlockConfigurationManager::Update body 0x616D7C (DidAnyObjectsMovePastThreshold gate, UpdateAllBlockConfigs, PruneFullPyramids, UpdateLastConfigCheckBlockPoses, NotifyBroadcasterOfConfigurationManagerUpdate). UpdatePoseOfStackedObjects 0x621794 and UpdateMarkerlessObjects 0x625704 are cross-layer entry points (M10/M12)",
    "re-analysis/research/20260929-R-VIS-verify-pre-items4-5.md (items 4a, 4b PASS)",
    "re-analysis/research/20260929-R-VIS-M11-gap1-extraction.md Q3, Q5; re-analysis/research/20260929-R-VIS-verify-M11-gap1.md (objections 2 and 5 corrected)",
    "0x00507824..0x0050783A, 0x00621D54..0x00621D88, 0x0062206E..0x006220DA, 0x00622106..0x0062218E, 0x006211E6..0x00621214, 0x00625028, 0x0062521A, 0x00620BE2",
    "re-analysis/research/20260929-R-VIS-verify-M11-batchA.md objections 2, 5, 8, 9, 10"
  ]
}
```

### M12-043

```json
{
  "title": "PyramidBase interior geometry: corners and midpoint for the pyramid top placement (GetBoundingQuad<float> corner order)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: corners/midpoint 0x006177F0..0x00617A08, 0x00617A8C..0x00617C32, multimap 0x00618A4C (equal keys keep insertion order, NaN right); unread GetBoundingQuad<float> 0x004E6490..0x004E6598 and Block::GetBoundingQuadXY 0x004E62A2.",
    "re-analysis/research/overnight/20261015-ov45-47-check.md: quad call 0x00617818..0x00617824; GetCanonicalCorners 0x004E60D8..0x004E6146 (8 corners at +-0.5: (-,-,+),(+,-,+),(-,-,-),(+,-,-),(-,+,+),(+,+,+),(-,+,-),(+,+,-)); pad/scale/rotate 0x004E62C0..0x004E6386; minAreaRect points opencv_core 0x00086188..0x0008627A; Quad 0x004E64FE..0x004E655A; centroid 0x004DF7BE..0x004DF84C; sort 0x004E7F24..0x004E7F96, 0x004E8508..0x004E8642; translation 0x004E639A..0x004E63B2; insertion 0x00617826..0x006179BC; midpoint 0x00617B98..0x00617BF6; container 0x006168EC..0x006169C4; builder 0x0061830A..0x006183B4; cache 0x0062C5A2..0x0062C616."
  ]
}
```

### M12-049

```json
{
  "title": "BehaviorBuildPyramid helper callbacks and the pyramid cache contents (Container::Update, CastNewEntryToBestSharedPointer, PruneFullPyramids, PyramidBase::operator==)",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261016-ov69-74-75-77-check.md: callbacks vtables 0x01029068 / 0x010290A8 (built 0x005DC16C..0x005DC17C, passed at 0x005DC298); cache UpdateAllBlockConfigs 0x00616F00 -> Container::Update 0x0061682C -> AddAllConfigsWithObjectToCache 0x0062C56E -> CastNewEntryToBestSharedPointer 0x0062C68C; PruneFullPyramids 0x0062C72E."
  ]
}
```

### M11-063

```json
{
  "title": "BlockWorld+0x74: the writer of the pose-change queue flag that gates OnObjectPoseChanged logs and queue",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-cliff-world-helpers-check.md: OnObjectPoseChanged 0x00624808..0x006248B8 tests byte BlockWorld+0x74 (MultipleChanges 0x006248EC, FirstPoseForObject 0x00624924, PoseChange push); no strb to +0x74 found in 0x00610000..0x00630000.",
    "re-analysis/research/20261013-blockworld-flag-and-map-helper.md Q1: constructor +0x74 = 0 (0x0061BD8A, strb [sl,#0x28] with sl = this+0x4C) and list setup (0x0061BD7C/0x0061BD86); UpdateObservedMarkers clears the PoseChange list (0x00624F78) and sets +0x74 = 1 (0x00624F7E), runs CreateObjectsFromMarkers, AddAndUpdateObjects, CheckForUnobservedObjects, UpdatePoseOfStackedObjects, then clears it (0x006250D4..0x006250D8) before BlockConfigurationManager::Update (0x0062520C) and UpdateMarkerlessObjects (0x0062521A). Reader 0x00624808..0x006248B8: flag 0 -> SetObjectPoseChanged only; flag 1 and queued -> MultipleChanges info (0x006248EC, 0x00BF8F50), not pushed; flag 1 and old 0 -> FirstPoseForObject info (0x00BF8F99, 0x00624924), not pushed; else emplace_back. Consumer UpdatePoseOfStackedObjects 0x00621794..0x00621AF4. Cliffs come from HandleCliffEvent on the same thread before UpdateAllRobots (0x004ED62E before 0x004ED648), so the flag is 0. The earlier note \"no strb to +0x74\" was wrong (da.py stops at undecodable halfwords)."
  ]
}
```
