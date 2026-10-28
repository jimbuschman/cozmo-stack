# SPARKS-STACK-1 independent finite source check

2026-10-28. Answers `re-analysis/jobs/trial-m13/SPARKS-STACK-1.md`. Read-only checker; no production/test, official record, inventory, approval, git or hardware changes. Actual model telemetry is unavailable. **Bounded READ PASS as of2026-10-28: whole finite source packet checked; implementation release remains manager-owned.**

The native cache affects chosen robot targets and readiness, so its intentions 5/3 are required. Existing intention0 is not a substitute. Reuse the independently checked generic cache/DefaultBest and shared Sparked lifecycle; recover only the changing finite joins. Primary authority is the shipped `resources/lib/armeabi-v7a/libcozmoEngine.so`, instructions and literal data. known.py was run before new traces. Scratch disassembly is a reproducible aid, not a replacement authority.

## Current records, read before ownership conclusions

Complete current title/status/evidence fields follow. No title or status is rewritten by this report.

```json
{
  "id": "M7-002",
  "title": "The shipped animation/reaction maps, behaviour configs and activity tree",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/obb/assets/cozmo_resources/assets/animationGroupMaps/AnimationTriggerMap.json (573 pairs)",
    "re-analysis/obb/assets/cozmo_resources/assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json (40 pairs)",
    "re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/reactionTrigger_behavior_map.json (22 rows over 21 triggers)",
    "RobotDataLoader::LoadReactionTriggerMap 0x00520bc8",
    "BehaviorManager::InitReactionTriggerMap 0x005a16e4",
    "178 shipped behaviour configs enumerated in re-analysis/inventory/M7-behaviour.md Appendix C"
  ]
}
```

```json
{
  "id": "M7-018",
  "title": "Shipped behaviour configuration and BehaviorClass factory binding",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "178-config table in M7 inventory Appendix C",
    "79-class entry-point table in Appendix B",
    "BehaviorContainer::CreateBehavior 0x0059c888",
    "class 0x39 BehaviorWait case constructs base IBehavior and installs the BehaviorWait vtable"
  ]
}
```

```json
{
  "id": "M7-019",
  "title": "M7 concrete reaction-behaviour state machines",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "reaction-class rows in M7 inventory Appendices E and F",
    "corrected ReactToCliff mood-event sequence 0x00604d70..0x00604d84",
    "ReactToImpact 0x006061f8..0x0060647c",
    "ReactToOnCharger 0x00606c94..0x00606e86",
    "ReactToPickup 0x00607750..0x00607d9c"
  ]
}
```

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
  ]
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
  ]
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
  ]
}
```

```json
{
  "id": "M12-028",
  "title": "The behaviour-owned PlaceRelObjectParameters producers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0055E2CA, 0x0055E3E0, 0x0055EB74, 0x0055EC2A, 0x005B58C2 the installers; 0x005C9588, 0x005DC266, 0x005DCE44 the parameter producers",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q1, Q2 and open questions 2 and 6",
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: rows 1-5. Stack 0x005C9520..0x005C961A; top 0x005DC08C..0x005DC322 (+0x005DD134, 0x005DC78A, 0x005DC7F0..0x005DC9A4); base 0x005DCDA4..0x005DCF38; UpdateBlockPlacementOffsets 0x005DD140..0x005DD3B4; CheckBaseBlockPoseIsFree 0x005DD650..0x005DD8FC with 0x0061F8EC..0x0061F9E0 and 0x00627F92..0x00628064."
  ]
}
```

```json
{
  "id": "M12-034",
  "title": "Unread dock-action handler bodies and the ramp/charger validity overrides",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005516EA, 0x00551750, 0x00551F9C, 0x00550858",
    "re-analysis/research/20260929-R-VIS-verify-M12-gap2.md 'What stays open'",
    "re-analysis/research/overnight/20261014-ov03-m12-034-check.md: build rows 1-4. Registration/lifetime 0x005516DE..0x005517C0, 0x00551EDC..0x00551F66, dtor 0x0055745C..0x005575B2; +0xF0 sources 0x005503D6, 0x005536EE, 0x00554C68..0x00554C94, 0x005565E0; RobotDeletedLocatedObject 0x005524C0..0x00552510 with CheckIfDone 0x005521BE..0x005521C6/0x005522BC/0x005523DC; Charger 0x004EA648; Ramp 0x0050F9AC..0x0050FA9E read and unreachable (ctor callers 0x0050F89A only, inside CloneType)."
  ]
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
  ]
}
```

```json
{
  "id": "M12-042",
  "title": "PlaceRelObjectHelper lifecycle: ctor, Init (pre-action check, DriveTo delegation), StartPlaceRelObject (3-attempt counter), RespondToPlaceRelResult, MarkFailedToStackOrPlace",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: ctor 0x005B8F00..0x005B8F82; Init 0x005B8FD0..0x005B90FE; Start 0x005B9168..0x005B930E; Respond 0x005B94C0..0x005B952E; MarkFailed 0x005B93A8..0x005B9460; invokers 0x005B956A, 0x005B95D6."
  ]
}
```

```json
{
  "id": "M15-008",
  "title": "Desired activity from objects and behavior-owned needs-action reporting",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ActivityFreeplay::CalculateDesiredActivityFromObjects 0x005ADF4C",
    "IBehavior::ExtractNeedsActionIDFromConfig 0x005BBAE8",
    "IBehavior::NeedActionCompleted 0x005BE40C",
    "ActivityGatherCubes::Update 0x005AF2F0"
  ]
}
```

```json
{
  "id": "M15-024",
  "title": "The whiteboard failure memory: tables, caps, FailureInfo and EntryMatches",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "AIWhiteboard::DidFailToUse / SetFailedToUse with a pose: per-object-id tables with caps {1, 1, 10, 1} (PlaceObjectAt keeps the last 10); EntryMatches expiry is strict with 1e-5f slack; id -1 means any object; the floor path's DidFailToUse is 0x005E0B78 (failure 2, id -1, 100.0f, pi) and the stack-on filter's is 0x005E1D24 (failure 1, 20.0f, pi/8)",
    "Pose3d::IsSameAs_WithAmbiguity 0x00846F3C (the per-axis and angle test) is unread: RECOVERABLE_GAP",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ]
}
```

```json
{
  "id": "M15-059",
  "title": "ActivitySparked lifecycle: OnSelected (HardSparkStartedByEngine tag 131), OnDeselected (StopAllAnims), objective/observed, Update/checks/end/interrupt, get-in/out, Complete (meta.upgrade_replay_* events, HardSparkEndedByEngine tag 132)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov65-67-check.md: OnSelected 0x005B1518..0x005B16DC, 0x005B171C; OnDeselected 0x005B1780..0x005B179E; objective/observed 0x005B181E..0x005B1866; Update/check/end/interrupt 0x005B186C..0x005B1A16; get-in/out 0x005B1D18..0x005B1F2A, hard max 0x005B1F94..0x005B2066; Complete 0x005B1A90..0x005B1C2E, 0x005B2094."
  ]
}
```

## Independent finite cache findings for extractor cross-check

| Step | Native source and result |
| --- | --- |
| Actual filter producers | Constructor `0056F724..0056F78E` creates default BlockWorldFilter, family literals `00C589E4={2,1}`, and binds `CanUseAsStackTopNoAxisCheck` at GOT `0103EC90`; saves filter at sp+28. `0056F828..0056F88E` similarly builds bottom-no-axis with families `00C589F4` and GOT `0103EC9C`, sp+20. Table `0056FC54..0056FC68` pairs key3 with bottom sp+20 and key5 with top sp+28; `0056FFF0..0056FFF8` passes table to ConfigureObjectInteractionFilters. |
| Best selector | Both keys3/5 bind actual `DefaultBestObjectFunction` through GOT `0103ECC0`, constructor `0056FD26..0056FD48` / `0056FD84..0056FDA2`. Reuse the prior checked generic algorithm `00570B44`, including actual configuration-stack priority, signed-ID iteration, strict f32 distance/tie order and best-retention; no nearest-upright heuristic. |
| Dependency | `00571040..005710CC` consults actual table `01023570 + intention*8`, recursively ensures dependency entries before target entry. Key3 pair `01023588` points `0105A9B0`, initialized `004D7086..004D709A` with sole literal `00C589C4=5`. Key5 pair `01023598` points empty `0105A998`, initialized `004D7042..004D7058`. Bottom3 therefore refreshes top5 first; top5 has no dependencies. |
| Top5 eligibility | `0057059E..005705CE`: if actual carried scalar (robot+284, +8) !=-1, lookup actual carried pointer and accept only pointer equality with candidate. If no carry, tail veneer `008CB79C` ARM body `008CB7A0..A8` resolves PLT `004ACFC4` (CanPickupNoAxisCheck), reusing checked intention0 eligibility with its actual whiteboard/docking recipients. |
| Bottom3 eligibility | `0057062C..00570630` passes intention5 to `CanUseAsStackBottomHelper` via veneer `008CB7BC` -> PLT `004ACFDC`. Helper `0057154C..005715C0` ensures top5, excludes candidate scalar at +18 equal to top best, calls actual AIWhiteboard DidFailToUse(action1, 3.0f, candidate pose,60.0f, global PI `0105A990` initialized `004D7030..003E`), rejects a match, requires candidate family field +48 ==2, then actual Docking CanStackOnTopOfObject. It does not apply the axis-check predicate from ordinary intention2. |
| Cadence | Entry `005711E4..005712B6`: ordered `abs(f32(last-now)) < f32bits(3727C5AC)` skips; otherwise stores now before clearing/rebuilding unique signed-ascending valid IDs through actual FindLocatedMatchingObjects. Retains previous best if still in valid set (`00571276..00571282`); else invokes installed best function (`00571284..00571292`). Dependency Ensure observes this same per-entry timestamp gate. No new pose/config invalidation caller or per-frame best reset is invented. |
| Current production gap | `cozmo-stack/src/Cozmo.Robot/Behavior/NativePickupTargetCache.cs` explicitly provides intention0 only, with one timestamp/valid set/best and pickup predicate. Actual StackBlocks refresh `005C94A6..005C9500` uses top5 then bottom3 for shipped anyOrientation=true (and still calls progression IsUnlocked9). Separate entry state and predicates/dependency must be built from checked source. |

A must independently check these newly extracted cache joins before their adoption into the combined packet. This checker does not self-certify its extraction.

## Corrections and bounded ownership

Prefreeze B ObjectID interpretation correction, caught by A and verified directly: +124/+12C are top/bottom ObjectID wrapper addresses; +128/+130 their scalar payloads. Constructor stores invalid scalar at `005C93EE/005C93F6`, vptrs `005C9408/005C940C`. `UpdateTargetBlocks` stores bottom scalar+130 at `005C94FA`; placement passes the SAME wrapper+12C at `005C9576`. No separate candidate/placement scalar or extra writer is inferred.

Frozen M7 row885 was contradicted by independently checked native control flow, and the operator explicitly approved the correction. Decision: `re-analysis/jobs/trial-m13/SPARKS-STACK-1-CONTRACT-CORRECTION.md`. Both-valid branches `005C98BC..005C98C2` directly to state gate `005C9A24`, skipping neighbor. Top-invalid/bottom-valid fails; bottom-invalid queries neighbor ON TOP OF BOTTOM (`005C9932` bottom lookup, `005C99BC` bottom argument, `005C99C0` helper) and compares returned pointer with stored top at `005C9A1E`. It can proceed when that physical relationship holds. Formal inventory/approval changes remain Claude-owned.

Independent numerical spot-check: bytes at `005C97EC` are `0000B442` -> f32 bits42B40000 ->90f; `005C97F0` is +0f. `005C973A/005C97A6` load zero/90 and `005C973E..005C9742` pass directly to Radians float assignment. Do not insert a degrees conversion. Recovery veneer ARM `008CC250..0058` arithmetic resolves PLT `004B3678` (TransitionToPickingUpBlock), consistent with A's callback recovery finding, not InitInternal.

Existing M12-028 owns the placement tuple producer, M12-012 the bottom gate, M12-042 helper lifecycle, M15-008 needs-report recipient, M7-002/018 configuration/factory. None of these narrow current titles/evidence owns the full derived StackBlocks state machine. M7-019 names concrete reaction classes, not StackBlocks. One separate unowned whole-derived-lifecycle proposal is warranted if the manager confirms the current scope comparison; no ID/status is invented here. Actual invoked unowned PlaceRel Verify/completion steps are being checked with A and the manager; no complete path claim precedes their resolution. No whole M15-059 or other broad record settlement follows from reuse of finite shared pieces.

## Remaining source check

Await A's coherent finite packet and independent check of the cache joins above. Whole actual caller→factory→owner→cache→pickup/DriveTo/placement helper/actions→retarget/retry→objective40/animation21A/needs43→shared Spark reports/getout/explicit-next cleanup remains to be checked against that packet. Current result PENDING; no tests run or requested by this source checker.

## Finite PlaceRel recipient check (still PENDING)

Current additional narrow owner, read before conclusion:

```json
{
  "id": "M13-007",
  "title": "FindObjectOnTopOrUnderneathHelper: its found-object test and the tolerance at each of its eleven callers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BlockWorld::FindObjectOnTopOrUnderneathHelper(obj, float tolerance, filter, bool) 0x0062601C takes the tolerance as its second argument (r2 captured at 0x00626038 into the lambda at 0x006260CE/0x006260D8), reads the height through GetDimInParentFrame<(char)90> (0x00626070), adds +/-half to the parent Z (0x0062607E..0x006260A0) and calls FindLocatedObjectHelper (0x0062610E)",
    "StackOfCubes::BuildTallestStackForObject passes 30 (0x41F00000 at 0x0061928C and 0x00619344); BlockWorld::UpdatePoseOfStackedObjects 0x00621908, DockingComponent::CanInteractWithObjectHelper 0x0063C728 and CarryingComponent::SetObjectAsAttachedToLift 0x00632F0C pass 15 (0x41700000)",
    "0x0062601C..0x0062619A the helper; 0x0062B9FC..0x0062BB1E the predicate lambda (vtable 0x0102E194): 0x0062BA0E..0x0062BA46 wrt-parent, 0x0062BA50..0x0062BAA2 quad intersection, 0x0062BAC2..0x0062BB0C the height test",
    "the eleven call sites and their tolerance/onTop: 0x00507258, 0x00507664, 0x0055526E, 0x005BA136, 0x005C99C0, 0x0061929E, 0x00619358, 0x0062190C, 0x00625E1A, 0x00632F14, 0x0063C730",
    "the CORRECTION: 'everywhere else 15 mm' is wrong; RollBlockHelper::UnableToRollDelegate passes 30.0 (0x005BA12C movt r2,#0x41f0), and BuildTallestStackForObject's second call (0x00619358) is the underneath search",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q3; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md priority check 4",
    "re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q1; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md Q1 (PASS, with the float-direction omission recorded above)",
    "0x008F6220..0x008F6304, 0x008F6320..0x008F638C, 0x004E03B8..0x004E047E, 0x004DF764..0x004DF7BC",
    "re-analysis/research/20260929-R-VIS-M13-gap3-extraction.md Q6; re-analysis/research/20260929-R-VIS-verify-M13-gap3.md Q6"
  ]
}
```

Current M12-017/025/034/037 titles and complete evidence above cover Init/Select/constructor/offset/handlers/turn setup; they do not own the whole invoked derived PlaceRel Verify or completion payload. M13-007 owns the specific neighbor helper at0055526E, not GetCompletionUnion's whole payload. Manager decides ownership of the newly required unowned action steps; no record number is invented here.

Primary Verify005555B4..0055581E independently agrees with A: selectedDock2/3; unsuccessful native Docking flag yields04000003; still carrying yields03000017. TurnTowardsObject(savedcarried, Radians0,true,false), suppressed tracks: first construction Update5556FE returns an immediate terminal error WITHOUT normal child retirement/clear; only0/RUNNING falls through a SECOND Update55570E. Later terminal retires child; failure clears saved carried located ID (555760..57CE). Successful Dock2 creates suppressed MoveLift(preset0,speed5f), returnsRUNNING; later success returns0. This changes physical turn/lift, failure state and downstream helper/callback progress. Current public PlaceRel Verify at DockActions.cs1013 uses generic result/status Retry and has no such sequence; it cannot serve as recovered native recipient unchanged.

Completion0055515C..00555344 has a source trap. It accumulates top-chain IDs and calls move setter55532A, then ALWAYS calls const setter55533E with targetscalar/count1/false. Independently checked75C3F0..75C436 overwrites all20bytes+u16 when union tag0, so accumulated chain is NOT the final API payload. Serializer75B4F0..75B536 emits five int32 words followed by countbyte and bool regardless count. Meaningful final count1/target/false is known. On the normal targetlocated/filter path, scratchfields55521C/555220/555224 overlap the final source words, including a default empty-tree self-pointer atsp4C (phone stack address sp50). Narrow destructor effects/observable raw projection are still being resolved with A. Do not invent a full target-plus-chain list, initialize the unused words silently, or automatically call defined address-dependent words M1-034. Missingtarget/unsupporteddock paths additionally contain uninitialized data, already covered by the existing explicit undefined-publication guard where proved. No wholepacketPASS precedes resolution or an explicitly recorded boundary/decision.

## Follow-up: reciprocal cache check and finite joins

A independently primary-checked the newly extracted cache constructor families/intentions5/3, dependencies, carried-pointer top predicate, bottom whiteboard/docking predicate, timestamps and retain-best, and confirmed them. The earlier request for A cross-check is fulfilled; this is reciprocal extraction review, not self-certification. Clarification also independently checked: top5 with NO carry tailcalls the SAME CanPickupNoAxisCheck00570398 used for intention0. This includes actual resting-flat10deg via CanPick0063C7F0→CanInteract0063C654; it omits the distinct upright rotated-parent-Z-axis==3 gate00570550..00570584. No changed flatness predicate is authorized.

Additional current title/status/evidence before finite recipient scope conclusions:

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
  ]
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
  ]
}
```

```json
{
  "id": "M11-063",
  "title": "BlockWorld+0x74: the writer of the pose-change queue flag that gates OnObjectPoseChanged logs and queue",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-cliff-world-helpers-check.md: OnObjectPoseChanged 0x00624808..0x006248B8 tests byte BlockWorld+0x74 (MultipleChanges 0x006248EC, FirstPoseForObject 0x00624924, PoseChange push); no strb to +0x74 found in 0x00610000..0x00630000.",
    "re-analysis/research/20261013-blockworld-flag-and-map-helper.md Q1: constructor +0x74 = 0 (0x0061BD8A, strb [sl,#0x28] with sl = this+0x4C) and list setup (0x0061BD7C/0x0061BD86); UpdateObservedMarkers clears the PoseChange list (0x00624F78) and sets +0x74 = 1 (0x00624F7E), runs CreateObjectsFromMarkers, AddAndUpdateObjects, CheckForUnobservedObjects, UpdatePoseOfStackedObjects, then clears it (0x006250D4..0x006250D8) before BlockConfigurationManager::Update (0x0062520C) and UpdateMarkerlessObjects (0x0062521A). Reader 0x00624808..0x006248B8: flag 0 -> SetObjectPoseChanged only; flag 1 and queued -> MultipleChanges info (0x006248EC, 0x00BF8F50), not pushed; flag 1 and old 0 -> FirstPoseForObject info (0x00BF8F99, 0x00624924), not pushed; else emplace_back. Consumer UpdatePoseOfStackedObjects 0x00621794..0x00621AF4. Cliffs come from HandleCliffEvent on the same thread before UpdateAllRobots (0x004ED62E before 0x004ED648), so the flag is 0. The earlier note \"no strb to +0x74\" was wrong (da.py stops at undecodable halfwords)."
  ]
}
```

Zero-offset DriveToHelper primary check PASS:005B584A..005B586E checks actiontype1 and exact A/B zero; actual Stack tuple takes ordinaryDriveToObject005B5870..005B588E, with fourzero stackargs, targetF0 and actual PreActiontypeF8. Approach104false and ForceFCfalse skip SetApproachAngle and forcedpose callback;005B598E..005B599A starts with actual RespondToDriveResult. Nonzero-offset callback machinery is not on this exact tuple; retain it as other-parameter frontier. No whole M12-022/026 settlement follows.

ClearLocatedObjectByIDInCurOrigin0062712C..00627144 independently checked: lookup actual ID/currentorigin/family-1, tailcall actual ClearLocatedObjectHelper(pointer). ARM veneer008CCAB0..008CCAB8 resolves PLT004B7D58. It does not substitute DeleteLocatedObjects or emit an invented RobotDeletedLocatedObject. Reuse existing checked world recipient; no whole M11-063 claim.

Remaining P7 raw-word interpretation is preliminary, not an accepted policy: a payload's reused stack bytes may be uninitialized as object fields despite prior filter-object writes. Prove that distinction from primary construction/typed-field evidence before using the M1-034 undefined-publication guard; otherwise defined address-dependent output needs an explicit decision. The final setter definitely publishes target/count1/false internally and overwrites the chain. Whole verdict remains PENDING final packet/finite questions, no builder release.

## Own retarget and recovery spot-check

Independent native instructions agree with combined S9/S10/S12: `005C9A24..005C9A88` retargets only state1/replacedfalse; a changed closest target calls StopActing(false,false), then sets135, writes bottom130, entersStack, then baseUpdate. `005C9B80..005C9C62` seeds result from currentbottom; missingcurrent or initial conversionfailure retains seed; MAIN-first 3D squared distance; only ordered strict MI updates best/result, signed set iteration and ties preserve prior. `005C9F80..005CA04C` carried recovery constructs actual sequential DriveStraight(-40f bitsC2200000,100f bits42C80000,true), thenPlaceGround; resultless callback `005CA2EA..005CA312` refreshes targets, requires bothvalid, then always Pickup via resolvedPLT004B3678. No-carry recovery does the same directly. No Init substitution or fabricated result-success gate. The supplied image/robot-state/firmware inputs and named inherited world/planner/ground44 frontiers stay explicit.

The final source verdict is still PENDING two finite answers: the actual consumer/effect of own vtable+2C set result, and precise final completion raw-field initialization/publication classification (or recorded decision). All other checked finite rows above may be adopted by A; this report does not release a builder.

## Operator disposition and final packet preparation

Operator2026-10-28 explicitly approved **“Approve zero unused ID slots.”** Parent relayed the exact question/answer: normal PlaceRel final payload contains target/count1/false plus four unused raw words, one a phone-stack address, and serializer emits allfive. Normalize only the four unused ID slots tozero; preserve meaningful fields and full native action/callback/API ordering. This is an explicit compatibility decision, NOT a claim that the normal source bytes were undefined or zero. Narrow approved guard is source-known selectedDock2/3 and actual targetlocated. Missingtarget/unselected/unknownpayload branches retain visible unavailable-publication/MISSING (M1-034 only where original undefined fields are proved); no fabricated target/count/success. Internal scalar/callback progress continues. Formal policy-record/inventory/status work remains Claude-owned; parent decision document pending.

Parent disposition for own getter+2C: exact body/output is recovered, shippedset{0}; no current actual bounded classentry/tick/stop/callback consumer is identified. Retain unknown external framework consumer as named frontier; no guessed name/orientation admission gate or global no-effect claim, no broad scan. Final READ verdict must preserve this bound.

Ownmaps independently checked again from BOTH shipped assets under behaviorSystem/behaviors and behaviorSystem/activities: actualmin0/max80/reps1, strictpriorityarray, StackTwoCubes, StackCube_Sparked, anyOrientationtrue, SoftSparkUpgradeLift, no executable override. Native numericjoins: NeedsEnumToString00782DCC table01033D40 index43/slot01033DEC→00C1C7ED StackCube_Sparked; AnimationEnumToString0075E298 table01032690 index0x205(decimal517)/slot01032EA4→00C19170 SoftSparkUpgradeLift. Activity27slot010330DC→00C19A54, objective40slot010331E0→00C19E0E, unlock10slot010340E8→00C1D931 independently rechecked. No inference from old C# enum values.

All finite questions now have source-backed behavior or explicit manager/operator disposition. Await only coherent final packet/proposal bytes for whole-check and freeze hash; no additional tracing or tests required by this source check.

## Whole-packet bounded READ PASS — 2026-10-28

Independent whole S1–S14/P1–P10/C1–C5 source check **PASS** for the exact configured SparksStackBlock instance, with the operator-approved native-gate correction and unused-ID policy. Reviewed packet SHA256 `080F1615A1494061DBD807A18877A5621A25F492FA467B1A0014371057D8EA3E`; companion proposal SHA256 `5940B7EB0FFB32C622E3A3C4C57355D51A59AE890B5EA62229E53CC302827566`. Actual native SO SHA256 `02263C07F6BB60F4D7F351A3667DCA84FD3E6C0FC6F838E18B5DE7CF4E2989E1`. The parent policy decision-document link/final packet metadata are pending; any subsequent behavioral edit requires checking its affected delta. Earlier PENDING paragraphs are preserved as the dated review trail and superseded by this verdict.

No changing unsupported join or omitted required finite recipient remains in this packet. The source path is actual assets/ID27→factoryclass0x34→shared Sparked owner/objective40→Stack runnable/cache5/3→carry-dependent actual helpers/Drive1/pickup/PlaceRel/ActionList→defined retries, physical verification and one retarget/ground recovery→objective40/optional plain21A/needs43→shared end/getout/reports→explicit supplied post-null next-admission cleanup. Reuse is bounded to the exact source-proved tuples and existing checked shared packets, not the old async/manual adapter. A independently checked B's newly extracted cache joins; B checked A's finite new placement/Drive1/clear/recovery/retarget and setter/serializer joins. The approved normal completion carrier is variant0 with target, fourzero-unused words, count1,false; other unavailable payloads are guarded visibly while defined internal result/callback flow continues.

Preserve exact unnamed getter+2C output{0} and actual +7C forwarding to UpdateTargetBlocks if called. Known prior Pickup0's own runnable explicitly calls its +7C refresh hook; Stack9484 directlyrefreshes. No additional cadence/readiness condition is inferred from external consumers not on the checked bounded path. The source packet names this inherited external framework frontier; it makes no global no-effect claim.

The proposed new record allocations are appropriate for unowned observable derived/action steps. Official titles/scopes remain manager/Claude-owned. In particular, a proposal describing whole BehaviorStackBlocks must retain ordinary intentions4/2/otherconfigs as unresolved unless built; a configured5/3-only job cannot settle that wholeclass scope. No current narrow record is renamed or narrowed. Whole M12-028/025/026/017/034/037, M15-022/059 and other broad records are not settled by this finite source check; M12-042 is only a conditional future whole-title candidate, never established by the zero-offset tuple alone.

Required implementation regressions are stated in A's packet and must reach the actual configured owner/factory/cache/helpers/manager/ActionList, with independent source expectations: hard andsoft full pipeline; carried/pickup branch; timestamp/dependency/previousbest; corrected bothvalid/bottom-invalid relationship; strict-distance retarget cancellation/order; helper attempt/result and actual turn/highlift/failureclear; carried ground recovery; objective→animation→needs order; exact normal/raw/local completion and Spark reports; stop/replacement/unrelatedwork and explicit-next cleanup. Supplied admission/scene/firmware/deselection inputs remain labeled. No callback-only/directcounter test proves the complete path. This source checker ran no tests, built no code, changed no production/official data and sent no hardware commands.

Remaining uncertainty is explicit: raw parent admission and post-null picker/sibling bodies, external getter consumers, ordinary4/2 parameter branches, M8-019 immediate switch, M15-071 reward amount/raw report, upstream vision/calibration/full44/planner/geometry/audio and firmware44 timing retain their existing named frontiers. This is a source packet PASS for the bounded implementation job, not a source classification/status settlement, code verification, full gate, subsystem acceptance or hardware result. Manager owns builder release.

Final freeze confirmation2026-10-28: A reported packet `080F1615A1494061DBD807A18877A5621A25F492FA467B1A0014371057D8EA3E`; B rehashed identical bytes after reading final S14 and proposal clarification. Proposal `5940B7EB0FFB32C622E3A3C4C57355D51A59AE890B5EA62229E53CC302827566` now explicitly proposes the whole **configured SparksStackBlock instance**, with ordinary4/2 outside; the earlier scope caution is addressed. No fullclass settlement is implied. Policy quote/guard is in frozen source packet; a later parent decision-document link is administrative and not needed to establish the operator's explicit authorization already received. Final bounded **READ PASS** on these bytes; no outstanding behavioral source question for this finite configured job. Manager may release the combined implementation within the named boundaries. No additional source reading or test process is needed in this read phase.

Final metadata-only freeze refresh: source `B719A854D016F0EC5A3E78D510C24464E90CCD7E79671D3FDC5B02118EA684E9`; proposal `B11301104F4753F08EA61418B431D0035E410129D92BEEA6C5C6BD66F0489EE8`. Independent comparison against the whole-reviewed source snapshot shows only the policy decision-document link and explicit COMPATIBILITY_POLICY-vs-EXACT_SOURCE wording changed; no behavior row or guard changed. Parent's decision reference is `re-analysis/jobs/trial-m13/SPARKS-STACK-1-COMPLETION-POLICY.md` (file creation still parent-owned; operator quote already directly authorized). **Bounded READ PASS applies to these final hashes.** Normalized unused slots must be classified COMPATIBILITY_POLICY, never EXACT_SOURCE. The preceding freeze hashes are the preserved review trail, superseded by this final pair. No outstanding source blocker.
