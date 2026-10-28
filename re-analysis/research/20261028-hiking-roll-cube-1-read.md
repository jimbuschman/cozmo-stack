# HIKING-ROLL-CUBE-1 — native own-path source packet

2026-10-28. Answers `jobs/trial-m13/HIKING-ROLL-CUBE-1.md`. Read PROJECT_STATE.md, AGENTS.md, AUTONOMY.md, the job and research instructions first. Reader requested `gpt-6.1-sol`, medium; actual runtime telemetry unavailable. Read-only extraction; no implementation, tests, hardware or git mutations. Authority: local `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Known.py preceded native traces. Addresses/instructions below came directly from ELF; absent decomp bodies were not used as evidence. Scratch native dumps: `.scratch/hiking-native.txt`, `.scratch/hiking-cache.txt`, `.scratch/hiking-callers.txt`. Independent reader's primary confirmation is companion `20261028-hiking-roll-cube-1-read-codex-check.md`.

## Binding correction

The shipped file has NO `isBlockRotationImportant`. Ctor `5C85DC movs r1,#1` constructs JSON true, `5C85EA get`, `5C85F0 asBool`, `5C85F4 strb [this+128]`; key bytes at `5C8634` are `isBlockRotationImportant`. Therefore Hiking_RollCube uses intention **7**, the AXIS-CHECK roll entry. The legacy manual registration with false is a candidate implementation error, not source. Intention6 is only the alternate class branch. No frozen owning record was found to claim the legacy false.

## Reused checked production contracts

Reuse, without repeating the shared bodies: PICKUP-PYRAMID-1/2/3 and PICKUP-CHILDREN-1 helper/base/manager/ActionList/typed proxy and native world/carry/DriveTo/Search/PanTilt/motor/visual/IDock abort and completion paths; KNOCKOVER-CUBES-1's IDriveToInteractWithObject, face/turn/lambda/profile/actions and actual hard-spark flag; STACK-BLOCKS-1 and source-followup's common target selection/carry safety/geometry/lift/cache/stop ownership. Their named MISSING inputs remain MISSING here. Same action class does not excuse checking this caller's construction arguments and virtual recipients. The tables below supply those new joins.

## Class, target and caller boundary

Each fully read row is a source finding (EXACT_SOURCE proposed evidence), not an official status change. Missing boundaries are explicitly identified below.

| Step | Native observable decision/state | Primary citation | Owner |
|---|---|---|---|
| H1 config/factory | Shipped RollBlock/Hiking_RollCube, needsActionID RollACube, displayNameKey behavior.RollBlock, requiredUnlockId RollCube; default rotation important=true. Factory allocation138, ctor5C8598. | `obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/hiking/Hiking_rollCube.json`; factory59CF86; ctor5C85A2/5C85DC..F4 | M7-018 + M15-044; new Roll lifecycle beyond factory |
| H2 ctor/runnable | Target124=-1; halfword128=1 (129=false); axis12C=1; state130=0; Robot11C retained. IsRunnable invokes virtual90 (UpdateTargetBlock) then true if ID124!=-1 OR base action84!=null. |5C85B0..CC;5C8654..74; virtual recipient5C8CA8 | new Roll owner; shared M8-001 |
| H3 actual target | Read128; false=>intention6, true=>7; GetBestObjectForIntention; copy returned ID to124. Cache7 uses families2/1, AxisCheck filter and RollBlockBestObjectFunction. |5C8CAE..C8;56F9B4/56F9F4..FA18;56FC72..7C;56FDE4..FDFC | new Roll owner; generic cache reuse |
| H4 filter | AxisCheck first root-flattens pose (4DF628), then NoAxisCheck. NoAxisCheck rejects failure kind3 at current object pose within60s/30mm/PI; only pose states1/2; resting flat within0x3E32B8C2 (10deg); object WRT actual MAIN must transform; reject IsPoseTooHigh(2,15,.5); reject membership in stack of height>=3. AxisCheck then requires rotated parent Z axis !=+3 on flattened pose. |5707F0..83C;570676..6D8;570704..73C;57074C..7C4; angle pointer105A990 initialized PI at4D7034..703E | new Roll own cache join; shared WB/geometry recipients |
| H5 picker | Iterate valid IDs; retain candidates whose located object exists, CanPickUpObject=true and its own pose parent Z axis!=+3. If retained set nonempty use it; otherwise use original set. Delegate selected set to checked DefaultBestObjectFunction. |570876..8C2;5708E8..902 | new Roll owner; common DefaultBest reused |
| H6 Init | Clear129; lookup target. Missing=>return1. Else snapshot up-axis from object's own pose rotation; TransitionToPerformingAction(false); return0. |5C867A..AC;5C86B4..6DE | new Roll owner |
| H7 performing | State130=0, state-name PerformingAction; invalidID warns and returns. max face angle PI/2 normally,0 when D9 alwaysStreamline OR D8 active hard spark. Roll parameters: callback to set129, named trigger1F6, unnamed1F7, maxAngle chosen. Factory CreateRollBlockHelper(target, **true**, params), SmartDelegate success/failure callbacks; IncreaseScoreWhileActing(.8); set state cube light target trigger19, false custom flag, SetBehaviorStateLights(...,false). |5C8712..1A;5C872C..740;5C8744..76E and literal5C8A18 PI/2,5C8A1C zero;5C87F0..8814;5C8846..50;5C8884..88E;5C88A6..88CE | new Roll owner; shared Smart/helper/lights |
| H8 callback owners | Preaction callback sets129=true; successful helper callback transitions RollSuccess; failure marks current target whiteboard failure3. Retained callbacks capture this, not external scene booleans. |vtable1027858 operator5C8D6E;1027898 operator5C8DD6 through ARM8CC23C=>PLT4B35B8;10278D8 operator5C8E3C | new Roll owner |
| H9 live update | Only when target exists and state130=0: compare current own-pose up-axis with saved12C. On changed axis, ComputeDistanceSQBetween(actual MAIN,target) must succeed and distanceSQ **>14400**; refresh axis, StopActing(false,false). If129 true =>RollSuccess; else invoke target update, validID=>Performing(false), invalid=>return2. Remaining path base UpdateInternal. This is robot distance, not displacement from initial target pose. |5C8A5E..AA0;5C8AA2..AD2;literal5C8B28=14400;5C8AD4..B22 | new Roll owner |
| H10 success | state130=1/state-name CelebratingRoll; Mood event RollSucceeded at Mood clock; update saved axis. UnlessD9/D8, construct TriggerAnimation1FB, loops1/interrupt=true, timeout60, tailfalse; StartActing with empty callback. Then IncreaseScoreWhileActing(.8), objective2(true), NeedActionCompleted(0) (configured needs ID resolves through base). No unconditional Finish invented here. |5C8B52..B8E;strings5C8C70/80;5C8BA0..BD8;5C8BFE..C1A | new Roll owner; M7-012/020/M15-008 reused recipients |
| H11 stop/reset/destruction | StopInternal and ResetBehavior each set target124=-1. No extra Roll resume override: base Resume uses derived Init as checked shared lifecycle. Derived destructor calls base destructor then delete; helper cancellation/retirement remains actual shared owner. |5C8C94..CA6;class vtable/current symbol census;5C8CDA..CE4 | new Roll owner + M8-001/009/011/015/016 |
| H12 Hiking parent | Asset config is BehaviorsOnly, Scoring, roll flat8.5, current-behavior bonus graph1, Simple duration60/cooldown15; HikingDriving start/loop/end and CalculateInterestingRegions; interlude strict priority list. These inputs can change admission, animation and interruption. Do not guarantee selection or run a host timer from asset alone. |activities/freeplay/hiking.json; common activity constructor5A7180->IActivity; existing checked Simple/Scoring/base evidence | M7-002/M8-003/006/012/013; parent runtime availability MISSING where actual producers absent |
| H13 Gather parent | SparksGatherCubes is Sparked, max240/min0/repetitions1/objectiveGatheredCubes; delegate GatherCubes StrictPriority lists DriveOffCharger,KnockOverCubes,**Hiking_RollCube**, then others. Gather.Update checks actual WB.AreAllCubesInBeacons; then finish lights/objective0B/needs14 or per-cube progress/freeplay lights. These can change traffic while Roll runs; parent is not an invented direct Sparked owner for Roll. OnSelected clears actual beacons; OnDeselected clears beacons and78=false. |activities/sparks/sparksGatherCubes.json;5AF250..274;5AF2A0..2FC;5AF2FE..386;5AF818..846 | M15-008/M15-059/M8-013; full parent admission/delegate/cache/light inputs remain visible MISSING |

## Roll helper and its real actions

|Step|Native behavior|Primary citation|Owner|
|---|---|---|---|
| R1 construction/init | Copy target toF0/F4, callbackF8, triggers110/114, maxAngle118, bool rollToUpright121; first120=true; attempt124=0. Init resets count0/firsttrue, DetermineAppropriateAction; Update returns status1C; ShouldCancelDelegates=false. |5B9938..996C;5B9A12..2E;5B9D80 | new Roll owner, shared IHelper |
| R2 carry and eligibility | If actual raw carrying ID !=-1, DelegateToPutDown. Else located target absent=>status0. CanActionRollObject forwards Docking.CanPickUpObjectFromGround (same shared CanInteract and height(.5,10,0) contract); false=>UnableToRollDelegate. |5B9A44..68;5B9A74..8E;5B9B6C..BB0;5B9BB2..BCC;55673C through Thumb/ARM8CB5AC->PLT4ABB48->63C880..8D6 | new Roll owner; shared docking validity |
| R3 preaction and drive | Call IsAtPreActionPoseWithVisualVerification(target,type4,0,0). Native zero =>StartRollingAction; NONZERO=>CreateDriveToHelper(type4,forcefalse,angle0,false,offset0/0). If rollToUpright and approach lookup succeeds, set angle/useAngle in drive params. Drive success=>StartRollingAction; failure=>DetermineAppropriateAction (actual callbacks, not fabricated success). |5B9ACE..AE6;5B9AFA..B16;5B9B18..2E;5B9BD4..BDC;closures5BA81A/5BA886 | new Roll owner + M12-029/shared DriveTo helper |
| R4 putdown | CreatePlaceBlockHelper; success stores120=true, determines next action and returns1; failure returns0. Shared place helper/action remains native, no arbitrary carry-clear. |5B9D98..DA6;5B9DBC/DF2 callback tables10262B4/10262F4;operators5BAA82..92/5BAAEA..AEC | new Roll owner/shared Ground path |
| R5 three attempts | On StartRollingAction if count>=3, MarkTargetAsFailedToRoll then status0. Otherwise increment count BEFORE action construction, first120=false. This count is helper attempts, not DockWithObject retry byte or actual messages. |5B9F0A..28 | M12-016 EXACT_SOURCE only this limit; broader behavior new owner |
| R6 concrete composite | Construct DriveToRollObject(target,false,0,false,Radians0,true). If121 true RollToUpright. Copy optional before-dock callback to actionE0; set sayName110/noName114/maxAngle118; StartActingWithResponseAnim(RespondToRollingResult,mapper). |5B9F2C..52;5B9F56..62;5B9F62..7A;5B9F92..FD6 | new Roll owner + existing shared IDrive |
| R7 response/retry | Mapper result04000003=>1, success0=>2, other=>0. Generic response animation mechanism reused as checked. Own Respond: CANCELLED02000000 leaves status; success0=>status2; every other result calls StartRollingAction. Failed target mark uses WB kind3 if still located. |5BAC3E..54;5BA7C8..DA;ARM8CBF3C=>PLT4B267C;5BA764..78C | new Roll owner + M12-016 |
| R8 recovery fallback | Lookup target; absent=>status0. Evaluate cache intention0 validity, find object-on-top with30mm and families2/1. If target can be picked up, create PickupBlockHelper(target); else if object-on-top exists create PickupBlockHelper(top); else log no interaction and retain status unchanged (NO status0 store). Pickup success=>DelegateToPutDown, failure=>0. Exact default pickup params trigger23F/angle0/two boolfalse. |5BA060..86;5BA0FE..136;5BA13A..216;5BA264..330;5BA332..37E;callbacks5BA8F2/5BA956/5BA9BA/5BAA1E | new Roll owner; M13-007 owns30 tolerance; shared Pickup/Ground |
| R9 DriveToRoll ctor | Base IDriveToInteractWithObject(type4,distance0f,callerfalse,angle0f,false,Radians0,true); construct RollObjectAction(target,caller last dock boolfalse), AddDockAction(false); retain weak pointer and proxy tag to dock. RollToUpright only before started and valid ID; approach lookup true=>SetApproachAngle. |55C9E6..CA18;55CA4C..7A;55CA7E..CAA6;55CAF8..CB26 | new Roll owner; shared IDrive M13-014 |
| R10 upright angle | GetObstacles(padding0), target lookup, only family1/2 and actual Block cast; GetCurrentPreActionPoses(types{4},empty markers,actual MAIN,obstacles,null,0,false). Choose first preaction pose whose marker code equals Block face5 marker; ComputeVectorBetween(object pose, chosen preaction pose), atan2f(y,x). Empty/no bottom/invalid=>false, leaving unrestricted approach. No nearest-angle guess. |55CC36..8E;55CC9A..CCD6;dataC560C0=4;55CCEA..CD18;55CEAC..CECE;55CD46..DD4/55CE62..CEA2 | new Roll owner; shared preaction geometry; phone atan2f rounding remains MISSING; no extension of the separate head-only M13-026 departure |
| R11 Roll ctor | IDockAction type1F, nameRollObject, bool last callerfalse. Save oldPoseF8;104=108=0;10C=BE860A92(-15deg). MethodBB=0; DockAction80=5 (RollLow); lift animationF0=20A. DeepRoll setter chooses5/6; EnableRollWithoutDock sets base flags but configured path never calls it. |556540..57C;55659C..5E4;556630..;5566B0..710 | new Roll owner; M12-005 owns payload fields |
| R12 SelectDockAction | Store initial object pose WRT MAIN parent (failure03000004); Block cast required, else03000004. Save top marker104. Object.z-robot.z >33.85 =>03000004. If carrying ID!=-1=>03000017, else success0. No axis proof inferred from firmware. |5568B8..8F0;556916..926;556952..96E;literal556B08=42076666;5569FC..A50 | new Roll owner |
| R13 Verify guards | Only DockAction5..7 else0300001A; docking+5 false=>04000003; carryingID!=-1=>03000017. Otherwise construct retained sequential[MoveHead(angle-15deg,tolerance2deg,accel0),VisuallyVerifyObject(target,saved topMarkerCode)] with child56=true. Immediately Update once, if Running Update again in same call; Running=>01000000. Terminal destroy child; success=>0. Failure EnableDeepRoll(true), return0300001D. |556B7C..C64;556C66..D30;556D6C..DCE | new Roll owner; shared head/visual runners |
| R14 dock/wire/handlers | Actual Roll goes through same IDock Init/Check/cleanup, markers, squint, locks and C5/DA subscriptions; DockWithObject occurs only after turn/verify prefix and dock state. Payload21bytes uses action5/method0/default60,200,500/retries0/constructor boolfalse/C1false unless checked profile changes defaults. Real C5/DA/RobotState/path input must feed native handlers; helper attempt increment is never an outbound message. |55657C caller;shared checked IDock5514FC/5521AC;Docking builder63BD50;pack7C0660;M12-003/005/013/018;P2/CHILDREN reports | existing full records; shared named gaps unchanged |
| R15 completion/API | GetCompletionUnion always ends with SECOND Set_objectInteractionCompleted: target written atsp18, halfword1 atsp2C (count1,boolfalse); four IDs sp1C..28 untouched on all successful branches. Setter75C400..410 copies22bytes, including those undefined words, overwriting prior5567DC union. Preserve typed kind0, defined target/count1/false and native scalar/callback/proxy flow. Full-public22byte payload is unavailable under M1-034; no Roll zero padding/PlaceRel policy. |556744..7EE, specifically5567E0..EE;75C3F0..414;native ActionList/local5A/API path reused P1/P2 and Stack | new Roll owner + M1-034 smallest-unit guard; independent confirmation required |

## Boundaries and proposals

- Recovery no-interaction frontier: `5BA332..37E` logs then cleanup/return; `IHelper::InitializeOnStack 5B6710..14` set status1, and `RollBlockHelper::UpdateWhileActiveInternal 5B9D80` only returns it. There is no Roll-specific timeout/action in that branch. Preserve the defined unchanged status; a stable scene may wait indefinitely, but external class axis/pose retarget can still change it, so an unconditional deadlock is not proved and no failure result is invented. Root disposition, if any, is recorded separately.


- MISSING parent admission/competition/readiness: actual Hiking scoring/strategy/interlude/progression/unlock/drive-anim ownership and SparksGatherCubes/Gather delegate/whiteboard beacon/light inputs must be bound only where checked shared production owners exist. Asset files do not prove a winner or create readiness. Gather's extra light/broadcast/needs bodies were identified narrowly above, not newly certified here. No sibling behavior is included.
- Shared unimplemented inputs remain visible: speech/face/vision frame availability, planner/foreign pose ownership, full public report inputs, image/firmware event inputs, and same checked DriveTo/Search/Ground gaps. No physical scene mutation from action output, fake completion, manufactured planner goals or timer defaults.
- Own class has no fresh timing loop: uses actual base Update/Mood clock/helper/action lifecycle and existing animation scheduler. Exact native defined errors stay defined errors. Unknown firmware timing remains M12-041, not a success oracle.
- Whole existing M12-016 already EXACT_SOURCE remains exactly the three-attempt owner. This packet provides its concrete caller and counter-versus-wire proof; it does not expand its title/claim to the helper. No current existing whole record is newly completed solely by Roll: M7-018, M8 base/helper/compound records, M13-014 IDrive and shared M12 records still own larger unrelated paths. Do not narrow them to settle a fragment.
- Proposed NEW one coherent Roll own-path record: BehaviorRollBlock (including target cache7 and picker), RollBlockHelper recovery/delegation, DriveToRollObjectAction and RollObjectAction selection/verification/typed completion. This is currently unowned beyond isolated M12-016 and existing shared bodies. Its `unresolved` must name GetCompletionUnion5567E0..EE / setter75C400..410 undefined unused-ID reads and M1-034 smallest full-public payload skip, while preserving native defined fields/local callbacks; phone atan2f rounding remains a named MISSING boundary unless a separately approved exact/equivalent implementation already applies; M13-026 head-only approval is not extended. Allocation/status/approval belongs to manager/Claude, not this research edit.
- Root standing-rule disposition (2026-10-28): after independent confirmation of four undefined Roll words, guard only affected full-public payload publication, preserve defined target/count1/false and scalar/native callbacks; visibly name M1-034 and owning/proposed Roll record. No new defined departure and no operator checkpoint claimed.

## Current manifest quotations before ownership reliance

The following are full current title/status/evidence snapshots, not remembered prior revisions and not edits.

```json
{
  "id": "M12-016",
  "title": "A roll gets three attempts",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x005B9EF8 StartRollingAction; 0x005B9F0A ldr.w r0,[r4,#0x124]; 0x005B9F0E cmp r0,#3; 0x005B9F10 blo 0x5b9f20; 0x005B9F16 blx 0x4B26AC (MarkTargetAsFailedToRoll)",
    "there is no charger helper in the build; the charger mount's one attempt is M13-008"
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
  "id": "M15-044",
  "title": "UnlockId name-to-value table and InitConfig details: UnlockIdFromString map builder; the root state after a failed readAsJson",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261013-backup-unlock-and-stoull-check.md: UnlockIdFromString static map builder E:0x00794D84..0x007957AC (undecoded); DataPlatform::readAsJson failure root state (PLT E:0x004A64E4) unread.",
    "re-analysis/research/20261013-small-reads-2-check.md: M15-044 section. UnlockIdFromString map 0x00794D84..0x00795834 (81 pairs, emulated; std::cerr on a miss); readAsJson 0x0084BFD4..0x0084C206; JsonCpp Reader 0x008E075A..0x008E12EC; InitConfig 0x0064BB68..0x0064BDB4; callers 0x0064B89E..0x0064B8C4, 0x0064C09C..0x0064C0C8."
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
  "id": "M15-059",
  "title": "ActivitySparked lifecycle: OnSelected (HardSparkStartedByEngine tag 131), OnDeselected (StopAllAnims), objective/observed, Update/checks/end/interrupt, get-in/out, Complete (meta.upgrade_replay_* events, HardSparkEndedByEngine tag 132)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov65-67-check.md: OnSelected 0x005B1518..0x005B16DC, 0x005B171C; OnDeselected 0x005B1780..0x005B179E; objective/observed 0x005B181E..0x005B1866; Update/check/end/interrupt 0x005B186C..0x005B1A16; get-in/out 0x005B1D18..0x005B1F2A, hard max 0x005B1F94..0x005B2066; Complete 0x005B1A90..0x005B1C2E, 0x005B2094."
  ]
}
```

```json
{
  "id": "M8-001",
  "title": "The IBehavior lifecycle: constructor fields, Init, Update, Stop, StopActing, Resume, IsRunnable and IsRunnableScored",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::IBehavior 0x005bbc38 stores behaviourID at +0x3c, behaviourClass at +0x64, needsActionID at +0x68; zeroes +0x24/+0x28, the flat score at +0x100 and +0x104 (strd r8,r8,[r4,#0x100] 0x005bbd28); sets +0x108 = 0, +0x10c = 0x29 (invalid behaviour objective), and +0x110 = +0x111 = 1 (movw r0,#0x101 0x005bbd30, strh.w r0,[r4,#0x110] 0x005bbd34); constructs the repetition graph at +0xe8 and the running-penalty graph at +0xf4 (GraphEvaluator2d::GraphEvaluator2d 0x005bbd12/0x005bbd22); calls ReadFromJson 0x005bbd40 and ScoredConstructor 0x005bbe32.",
    "IBehavior::Init 0x005bcb54 walks the robot's ActionList at robot+0x250. action+0x60 is the action's 32-bit tag; the guard sets its flag when a tag is > 0x2dc6c0 (movw r6,#0xc6c0 0x005bcbd0 / movt r6,#0x2d 0x005bcbda; cmp r2,r6 0x005bcbec; movhi r5,#1 0x005bcbf0). sTagCounter at 0x01051020 starts at 0x002dc6c1, so every engine-constructed action tag is >= 0x2dc6c1, while an action tagged through the game path (IActionRunner::SetTag 0x00540098, called from HandleActionEvents 0x00525ab0 and the QueueSingleAction/QueueCompoundAction handlers 0x00527a78/0x00527c16) carries a tag <= 0x2dc6c0. The warning IBehavior.Init.ActionsInQueue at 0x005bcc84 fires only when at least one engine-tagged action is present; its count is the main queue's (map key 0) current action plus queued actions (ldr r1,[r0,#0x14] 0x005bcd64, ldr r0,[r0,#0x20] 0x005bcd66, addne r0,#1 0x005bcd6c).",
    "IBehavior::Init spark gate +0xd8: ldr r0,[r0,#0x44] 0x005bccac; if [r0+0x58] != 0x55 then +0xd8 = ! [r0+0x5c] else 0 (0x005bccb0..0x005bccc2). It then clears +0x98, writes halfword 0x0100 at +0xa0 (so +0xa1 = running = 1, +0xa0 = 0), stamps the running-penalty clock +0x34 = BaseStationTimer::GetCurrentTimeInSeconds(), clears the idle-animation flag +0xb0, and calls vtable+0x48 (IsRunnableInternal): non-zero clears +0xa1, otherwise +0x80++ (0x005bccbe..0x005bcd0c). If the unlock id +0x70 is not 0x55 and equals robot+0x44->0x58 it takes the SparkBehaviorDisables lock through SmartDisableReactionsWithLock and clears +0x114 (0x005bcd16..0x005bcd58).",
    "IBehavior::Update 0x005bd074 calls vtable+0xc (UpdateInternal) unless byte +0xa0 is set and the current-action handle +0x84 is zero, in which case it returns 2 (ldrb.w r1,[r0,#0xa0] 0x005bd074; cbz r1 0x005bd078; ldr.w r1,[r0,#0x84] 0x005bd07a; cbz r1 0x005bd07e; movs r0,#2 0x005bd088).",
    "IBehavior::Stop 0x005bd08c logs, stops any helper, clears +0xa1 (0x005bd10c), calls vtable+0x54 (StopInternal, 0x005bd110/0x005bd114), stamps the last-run clock +0x30 = BaseStationTimer::GetCurrentTimeInSeconds() (0x005bd11a/0x005bd11e), calls StopActing(0,0) (0x005bd126), then undoes the scope in this order: (a) remove disable-reactions locks while +0xac != 0 (0x005bd12c/0x005bd136), (b) remove the idle animation if +0xb0 (0x005bd148), (c) clear the motion profile if +0xc0 (0x005bd158), (d) walk the track-lock map at +0xb4 and MovementComponent::UnlockTracks(mask, name) each, then destroy the map (0x005bd16c/0x005bd174).",
    "IBehavior::StopActing 0x005bd34c calls vtable+0x80(0) (blx r2 0x005bd360); when viaCallback is false and a helper is live it logs \"Stopping behavior helper because action stopped without callback\" and calls StopHelperWithoutCallback (0x005bd39c/0x005bd3d6); then, if +0x84 is set, cancels it in ActionList (0x005bd3f0) and clears +0x84 unless keepAction.",
    "IBehavior::Resume 0x005bceac: for any trigger other than 0 (CliffDetected) or 0x14 (UnexpectedMovement) it takes the normal resume path (cmp r5,#0x14 0x005bcf16; cmpne r5,#0 0x005bcf1a; bne 0x5bcf7e 0x005bcf1c). For those two it reads +0x114, increments it and takes the normal path while the pre-increment value is < 1 (ldr.w r0,[r4,#0x114] 0x005bcf1e; cmp r0,#1 0x005bcf22; str.w r1,[r4,#0x114] 0x005bcf28; blt 0x5bcf7e 0x005bcf2c); otherwise it sets +0x118 = now + 15.0, builds \"TooManyResumesCliffOrMovement\" and calls MoodManager::TriggerEmotionEvent(name, now), returning 1 (0x005bcf36..0x005bcf7a). The normal path sets +0xa2 = 1, stamps +0x34 = now, calls vtable+0x4c (ResumeInternal), clears +0xa2; on a zero result sets +0xa1 = 1 and, when +0x70 != 0x55 and equals robot+0x44->0x58, takes the SparkBehaviorDisables lock; on a non-zero result clears +0xa1 (0x005bcf80..0x005bcfde).",
    "IBehavior::IsRunnable 0x005bd750 calls IsRunnableBase(this->robot); a result other than 1 returns 0, otherwise it tail-calls vtable+0x50 with the caller's robot (0x005bd756..0x005bd770). IBehavior::IsRunnableScored 0x005bda28 returns 1 when the current time is >= the suppression stamp +0x118, else 0 (0x005bda38/0x005bda3e/0x005bda46/0x005bda48). IBehavior::IsRunnableBase 0x005bd778: +0xa1 (running) returns true; otherwise the AI process (robot+0x264), the robot state byte +0x74 against 3, the unlock id +0x70 (0x55 bypasses) through ProgressionUnlockComponent::IsUnlocked(robot+0x448, id, true) (0x005bd810), and the float timers at +0x78.",
    "ReactionTrigger ordinals (table 0x10337c0; EnumToString 0x0077065c): 0 = CliffDetected, 0x14 = UnexpectedMovement, 0x15 = Count, 0x16 = NoneTrigger (ReactionTriggerFromString 0x00770674 confirms the same mapping).",
    "StopWithoutImmediateRepetitionPenalty 0x005beea0 has exactly three callers, all concrete-behaviour UpdateInternal methods, none of them IBehavior::Stop/StopActing/FinishCurrentBehavior: BehaviorPickUpCube::UpdateInternal (blx #0x4b3384 0x005c685c, then movs r0,#2 0x005c6860), BehaviorStackBlocks::UpdateInternal (0x005c991c/0x005c9920), BehaviorBuildPyramidBase::UpdateInternal (0x005dd110/0x005dd114). Those behaviours are M7/M15, not M8.",
    "IncreaseScoreWhileActing 0x005bf02c has ten callers, all concrete behaviours (KnockOverCubes, PopAWheelie, PutDownBlock, RollBlock, StackBlocks and the FUN_0059ec54 helper that pairs it with StartActing); ScoredActingStateChanged 0x005bf044 (clears +0x104) has no engine caller at all (exhaustive bl/blx and data-word scan). IBehavior::IsRunnableScored 0x005bda28 likewise has no engine caller and is not in the IBehavior vtable (0x010264e0); only its .dynsym st_value 0x005bda29 exists."
  ]
}
```

```json
{
  "id": "M8-003",
  "title": "Scored selection: emotion scorers or the flat score, times the repetition and running penalties",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::ReadFromScoredJson 0x005bc488 reads five keys, not four: \"emotionScorers\" into a MoodScorer at +0xdc, \"flatScore\" into the float at +0x100 (0x005bc4ca/0x005bc4e0), \"repetitionPenalty\" into the graph at +0xe8 (0x005bc4ee), \"considerThisHasRunForBehaviorObjective\" into a behaviour objective at +0x10c through BehaviorObjectiveFromString (call 0x005bc5a0), and \"runningPenalty\" into the graph at +0xf4 (string 0x005bc602; add.w r6,r4,#0xf4 0x005bc5f8; AddNode(0,1,true) 0x005bc678).",
    "IBehavior::EvaluateScoreInternal 0x005beec2: if the MoodScorer vector at +0xdc/+0xe0 is non-empty it tail-calls MoodScorer::EvaluateEmotionScore (0x8cbf8c, with robot+0x440); otherwise it returns the flat score at +0x100 (vldr s0,[r0,#0x24] after the pre-index 0x005beed4). The two are alternatives, not addends.",
    "MoodScorer::EvaluateEmotionScore 0x0067c9b8 walks the scorers, evaluates each one's graph at its emotion's value or at the value minus the value sixty ticks ago when trackDelta is set (Emotion::GetHistoryValueTicksAgo(60) 0x0067c9f4), and returns the mean (0x0067ca6e..0x0067ca72); a result within 1e-05 of zero ends the walk and the score is zero (0x0067ca50).",
    "IBehavior::EvaluateScore 0x005bef60: while running it uses EvaluateScoreInternal plus the float at +0x104 (vldr s2,[r4,#0x104] 0x005bef80; vadd.f32 s16,s0,s2 0x005bef88) and multiplies by EvaluateRunningPenalty when +0x111 is set (ldrb.w r0,[r4,#0x111] 0x005bef84; cbz 0x005bef8c; blx EvaluateRunningPenalty 0x005bef90; vmul.f32 0x005bef98). Otherwise it scores only a behaviour that IsRunnableBase and passes the vtable+0x50 test, applies the repetition penalty only when +0x110 is set and now >= +0x108 (ldrb.w r0,[r4,#0x110] 0x005befd4; cbz 0x005befd8).",
    "IBehavior::EvaluateRunningPenalty 0x005bef22: returns 1.0 when +0x34 <= 0, otherwise evaluates the graph at +0xf4 at now - +0x34 (vldr s0,[r4,#0x34] 0x005bef26; add.w r0,r4,#0xf4 0x005bef4a; b.w 0x8cbf9c 0x005bef5a).",
    "The four constructor fields: +0x104 is the running-score bonus (IncreaseScoreWhileActing 0x005bf02c adds to it only while the current-action handle +0x84 is non-zero; ScoredActingStateChanged 0x005bf046 clears it); +0x108 is the repetition-penalty suppression threshold; +0x110 enables the repetition penalty; +0x111 enables the running penalty (both set to 1 by the halfword 0x0101 at 0x005bbd34, with no other writer).",
    "EmotionScorer::ReadFromJson 0x0067aabc names emotionType, scoreGraph and trackDelta. No shipped behaviour or activity config carries an emotionScorers block, so every scored behaviour in the app is scored by its flatScore alone.",
    "IncreaseScoreWhileActing 0x005bf02c is called only from concrete behaviour transitions (ten call sites, M7/M15); ScoredActingStateChanged 0x005bf044 has no engine caller, so +0x104 is never cleared in-engine."
  ]
}
```

```json
{
  "id": "M8-006",
  "title": "The wants-to-run strategy types, and what each one tests",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "The nine WantsToRunStrategyType values are the table EnumToString 0x007716A4 indexes at 0x01033820: Invalid, AlwaysRun, ExpressNeedsTransition, Generic, InNeedsBracket, ObstacleDetected, PlacedOnCharger, RobotPlacedOnSlope, RobotShaken. CreateWantsToRunStrategy 0x00614710 dispatches on that value through a tbb and constructs the matching Strategy class.",
    "StrategyInNeedsBracket::WantsToRunInternal 0x006141A0 is one call: NeedsManager::GetCurNeedsStateMutable() then NeedsState::IsNeedAtBracket(need, bracket), the pair held at strategy+0x18 and +0x1C from the config's need and needBracket.",
    "StrategyExpressNeedsTransition::WantsToRunInternal 0x006136D8 asks IsNeedAtBracket(need, Critical) - the literal 3 at 0x006136EC, Critical being index 3 of the NeedBracketId table at 0x01033EE0 (Full, Normal, Warning, Critical) - and then returns false when the need equals the one the AI component is already expressing (0x006136FC). Both are implemented here now.",
    "Of the shipped configs only reactToObstacle.json gives a behaviour a strategy, and it is ObstacleDetected, whose flag nothing in this build ever sets. The needs strategies appear on activity configs (M7/M15), and PlacedOnCharger, RobotPlacedOnSlope and RobotShaken appear in reactionTrigger_behavior_map.json (M10)."
  ]
}
```

```json
{
  "id": "M8-009",
  "title": "Behaviour scope undo order: the engine releases in a fixed order (reaction locks, idle, motion profile, track locks, light patterns)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::Stop 0x005bd08c releases the scope in a fixed order regardless of acquisition order: (a) remove every disable-reactions lock while +0xac != 0 (0x005bd12c ldr.w r0,[r4,#0xa4]; 0x005bd136 blx SmartRemoveDisableReactionsLock; 0x005bd13a/0x005bd13e/0x005bd140), (b) remove the idle animation if +0xb0 (0x005bd142 ldrb.w r0,[r4,#0xb0]; 0x005bd14c blx SmartRemoveIdleAnimation), (c) clear the motion profile if +0xc0 (0x005bd150 ldrb.w r0,[r4,#0xc0]; 0x005bd158 blx SmartClearMotionProfile), (d) walk the track-lock map at +0xb4 and MovementComponent::UnlockTracks(mask, name) each, then destroy the map (0x005bd15c/0x005bd16e/0x005bd174 blx 0x4a5794; 0x005bd19e blx __tree::destroy), then clear the custom light-pattern vector at +0xcc/+0xd0 (0x005bd1a2 ldrd r1,r0,[r4,#0xcc] ... 0x005bd1c6 strne.w r0,[r4,#0xd0]).",
    "This stack's BehaviorScope groups its undos by the same categories and releases them in that order (IBehavior.cs, BehaviorScope.Dispose): reaction locks (SmartDisableReactionsWithLock/DisableReactions), idle (SmartPushIdleAnimation), motion profile (SmartSetMotionProfile), track locks (LockTracks/SmartLockTracks), custom light patterns (SmartSetCustomLightPattern). OnRelease hooks have no engine counterpart and run last. Within a category the undos run most-recent-first, which is required by the track-lock undos' saved-mask restore; the engine unlocks each map entry independently, so only the category order is observable.",
    "The policy review of 2026-09-29 (verdict C) found the divergence; @cozmo-verifier re-read all five release sites and the light-pattern tail (0x005bd1a2..0x005bd1c6), which the review had not read past 0x5bd180."
  ]
}
```

```json
{
  "id": "M8-011",
  "title": "The Smart* scope helpers: idle animation, motion profile, track locks, custom light patterns, helper delegation and the reaction locks",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "SmartPushIdleAnimation 0x005be41c: if the idle-animation flag +0xb0 is already set it fails through sVerifyFailedReturnFalse (0x005be448 b.w 0x8cbe5c); otherwise AnimationStreamer::PushIdleAnimation(trigger, name) and +0xb0 = 1 (0x005be464..0x005be478).",
    "SmartRemoveIdleAnimation 0x005bd4c8: when +0xb0 is clear it raises VERIFY(%s): Behavior %s is trying to remove an idle, but none is currently set through sVerifyFailedReturnFalse (cbz r0,#0x5bd508 0x005bd4d4; string 0x005bd51e; b.w 0x8cbe5c 0x005bd526); otherwise AnimationStreamer::RemoveIdleAnimation(name) and +0xb0 = 0 (0x005bd4ec/0x005bd500).",
    "SmartSetMotionProfile 0x005be518 verifies +0xc0 == 0 (sVerifyFailedReturnFalse 0x005be52a), calls PathComponent::SetCustomMotionProfile (0x005be534) and sets +0xc0 = 1. SmartClearMotionProfile 0x005bd584 verifies +0xc0 != 0, calls PathComponent::ClearCustomMotionProfile (0x005bd59c) and clears +0xc0.",
    "SmartLockTracks 0x005be5bc emplaces (name -> mask) into the map at +0xb4; on a new key it calls MovementComponent::LockTracks(mask, name, reason) and returns 1; on an existing key it warns \"Attempted to lock tracks with key named %s but key already exists\" and returns 0 (0x005be600/0x005be624/0x005be636). SmartUnLockTracks 0x005be6e0 looks the name up, calls MovementComponent::UnlockTracks(mask, name), erases the entry and returns 1, or warns and returns 0 (0x005be6ee..0x005be716).",
    "SmartSetCustomLightPattern 0x005be7f0: if the ObjectID is already in the vector at +0xcc it logs on the \"Unnamed\" channel and returns 0; otherwise CubeLightComponent::PlayLightAnim(objectID, trigger, callback, true, lights, timeout) and appends the ObjectID to the vector at +0xd0, returning 1 (0x005be806..0x005be8cc).",
    "SmartRemoveCustomLightPattern 0x005be9b0 searches the vector at +0xcc for the element whose +4 equals objectID+4; not found logs \"No custom light pattern is set for object %d\" and returns 0 (0x005bea4e/0x005bea74); found calls CubeLightComponent::StopLightAnimAndResumePrevious(trigger, objectID) for each trigger and erases the element, returning 1 (0x005be9e8/0x005bea34).",
    "SmartDelegateToHelper 0x005beb10 logs on the \"Behaviors\" channel, warns and calls StopHelperWithoutCallback() when the current helper +0xc8 is already live (0x005beba4/0x005bebe8), then calls BehaviorHelperComponent::DelegateToHelper on [robot+0x264]+0x10 (0x005bec2c); on success it stores the helper as a weak ref at +0xc4/+0xc8 through __add_weak at 0x005bec6e, otherwise logs \"SmartDelegateToHelper.Failed\" (0x005bec8c/0x005becb2).",
    "SmartDisableReactionsWithLock 0x005bce3c appends \"_behaviorLock\" to the name, calls BehaviorManager::DisableReactionsWithLock(manager, name+\"_behaviorLock\", table, true) (0x005bce62) and inserts the original name into the per-behaviour set at +0xa4 (0x005bce7e). SmartRemoveDisableReactionsLock 0x005bd470 appends the same suffix, calls BehaviorManager::RemoveDisableReactionsLock (0x005bd48c) and erases the name from +0xa4 (0x005bd4a4). The manager side is M7-014.",
    "SmartDelegateToHelper 0x005beb10 calls BehaviorHelperComponent::DelegateToHelper, whose body is 0x0056dad8..0x0056db37 (PLT 0x004b29d0) at AIComponent+0x10 ([robot+0x264]+0x10, AIComponent constructed at 0x00569aae/0x00569ab2): it clears stack-maintenance vars, copies the two callbacks to +0x10/+0x28, and on an empty helper stack pushes the helper and stores Robot::GetWorldOriginID at +0x40, returning 1. R-BEH C2 read the component and its helper-stack runtime in full (report re-analysis/research/20260929-R-BEH-M8-framework-gap1-extraction.md); the component and the M7/M15 helper subclasses are still unbuilt, so the record stays IMPLEMENTATION_GAP."
  ]
}
```

```json
{
  "id": "M8-012",
  "title": "BehaviorManager::Update's tick order, behaviour switching, finishing and the reaction gate",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorManager::Update 0x005a2f70 returns immediately when the manager's init byte at +0 is zero, logging \"BehaviorManager.Update.NotInitialized\" (0x005a2f70/0x005a2f72/0x005a2fea).",
    "It ticks the current activity first: GetCurrentActivity then the activity's virtual Update at vtable+0x20 (0x005a2f78/0x005a2f80/0x005a2f84).",
    "If robot+0x355 is non-zero, or manager+0x20 == 1, it calls EnsureRequestGameIsClear (0x005a2f8e/0x005a2f94/0x005a2f9e).",
    "If manager+0x38 is set it calls SelectUIRequestGameBehavior, clears the flag, and when the previous current behaviour's class is 0x2e (cmp r7,#0x2e at 0x005a2fe0) sets byte +0x220 on the object at manager+0x30 (strbeq.w r1,[r0,#0x220] 0x005a305c).",
    "It calls CheckReactionTriggerStrategies 0x005a3060; a non-zero result suppresses the scored choice and the UI-request switch for this tick (cbnz r5 0x005a3068).",
    "The reaction list is evaluated only when the robot's action list robot+0x250 is empty: the manager latch at +0x4c is ORed with !IsEmpty and CheckReactionTriggerStrategies returns false when the list is empty (0x005a3558..0x005a356c).",
    "If no reaction fired, nothing occupies manager+0x30, and the current behaviour's class is 0x16, it calls ChooseNextScoredBehaviorAndSwitch (ldrb r0,[r0,#0x10] 0x005a3070; cmp r0,#0x16 0x005a3072; blxeq 0x005a3078).",
    "If no reaction fired and manager+0x30 holds a behaviour different from the current one, it calls SwitchToUIGameRequestBehavior (0x005a307e/0x005a3082/0x005a309a).",
    "It ticks the current behaviour with IBehavior::Update (0x005a30bc). Return 0 logs \"BehaviorManager.Update.FailedUpdate\" (0x005a3148) and return 2 logs a debug line; both then call FinishCurrentBehavior(behavior, class != 0x16) (0x005a3128). Return 1 keeps it (cmp r0,#0 0x005a30c0; cmp r0,#2 0x005a30c4).",
    "SwitchToBehaviorBase 0x005a1e6a stops the current behaviour (StopAndNullifyCurrentBehavior), calls IBehavior::IsRunnable(robot) and logs \"BehaviorManager.SwitchToBehaviorBase.BehaviorNotRunnable\" on false, then IBehavior::Init (0x005a1e94); an Init failure logs \"BehaviorManager.SetCurrentBehavior.InitFailed\" (0x005a1eae), clears the behaviour, and still sets the running/resume info (SetRunningAndResumeInfo 0x005a1f12) and sends the DAS transition (SendDasTransitionMessage 0x005a1f1c).",
    "FinishCurrentBehavior 0x005a38ca: immediate == 1 tail-branches to 0x8cbc9c; otherwise, if manager+0x30 equals the finishing behaviour, it calls EnsureRequestGameIsClear (0x005a38e6) and switches to a default class-0x16 BehaviourRunningAndResumeInfo (movs r0,#0x16 0x005a38f4; SwitchToBehaviorBase 0x005a38fe).",
    "SwitchToReactionTrigger 0x005a25e4 builds a resume info and calls the strategy's ShouldTriggerBehavior (vtable+8) (0x005a262a); only when it returns 1 does it switch (cmp r0,#1 0x005a262c; SwitchToBehaviorBase 0x005a26da).",
    "TryToResumeBehavior 0x005a2b48 bails unless the stored resume head angle differs from a constant and the robot's action list is empty (vldr s0,[pc] 0x005a2b48; ActionList::IsEmpty 0x005a2b60), logging \"Resuming behavior and don't have an action, so setting head angle %f, lift height %f\" (0x005a2b72)."
  ]
}
```

```json
{
  "id": "M8-013",
  "title": "The behaviour choosers and the activity selection dispatch",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060a44a walks its behaviour map, calls EvaluateScore(robot) on each (0x0060a44a), skips scores <= 0 (vcmpe s0,#0 0x0060a452), adds a running-duration graph bonus for a running behaviour (GraphEvaluator2d::EvaluateY 0x0060a474) and a RandomGenerator::RandDbl tie-break for a non-running one (0x0060a4a8), and keeps the maximum. It logs \"behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f\" (string 0x0060a60a; sChanneledInfoF 0x0060a61c) and warns \"Looks like more than one behavior returned IsRunning(). One of them is '%s'\" (0x0060a550).",
    "SelectionBSRunnableChooser::GetDesiredActiveBehavior 0x0060ad64 chooses between the behaviour at +0x2c and the one at +0x34, gated by a countdown at +0x3c and a latch at +0x40; a running behaviour short-circuits the countdown (movs r6,#1 0x0060ad84; subs r1,#1 0x0060adbe; str r1,[r5,#0x3c] 0x0060adc0; strb.w r0,[r5,#0x40] 0x0060adc8).",
    "StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior 0x0060b23e walks its behaviour vector at +0x1c..+0x20; a behaviour with +0xa1 (running) non-zero is returned immediately, otherwise the first behaviour for which IBehavior::IsRunnable(robot) == 1 is returned; if none, it returns a null shared_ptr (0x0060b242..0x0060b266). No scoring and no tie-break.",
    "IActivity::GetDesiredActiveBehavior 0x005b387c returns the activity's current behaviour when it equals the caller's; otherwise it calls the activity's GetDesiredActiveBehaviorInternal at vtable+0x30 (0x005b38c2) and, when the activity holds an interlude behaviour at +0x28 and the result differs, calls the interlude's method at vtable+0x28 (0x005b38f6), logging \"Activity %s is inserting interlude %s between behaviors %s and %s\" (0x005b399c).",
    "IActivity::GetDesiredActiveBehaviorInternal 0x005b3a6c reads the chooser at +0x24; a null chooser warns \"VERIFY(%s): ChooseNextBehaviorInternal called without behavior chooser overwritten\" and returns a null behaviour; otherwise it calls the chooser's vtable+0x28 (0x005b3a70/0x005b3aa2/0x005b3a94).",
    "The concrete activities implement GetDesiredActiveBehaviorInternal: ActivityFreeplay 0x005ae29c, ActivitySocialize 0x005b023c, ActivitySparked 0x005b1d18, ActivityStrictPriority 0x005b267c, ActivityBuildPyramid 0x005a8854, ActivityFeeding 0x005abdd8. Their bodies belong to M7/M15.",
    "SelectionBSRunnableChooser::GetDesiredActiveBehavior 0x0060ad64 full algorithm: +0x2c is the behaviour named by the last ExecuteBehaviorByID/ByExecutableType game message (set 0x0060ac2c/0x0060ac30), +0x34 is the Wait behaviour (BehaviorID 0xb2 resolved in the ctor 0x0060a988/0x0060a99a), +0x3c is the message's numRuns (ctor writes -1 unlimited, 0x0060a880/0x0060a884; message stores 0x0060aa88/0x0060aac8), +0x40 is a byte latch holding the previous call's running state (0x0060adc8/0x0060ae3a). It returns +0x2c while it is running or IsRunnable (0x0060ad96) and numRuns has not run out, decrementing +0x3c once per running->stopped edge; otherwise the same for +0x34 (0x0060ae08) but its countdown block runs only when +0x34 == +0x2c (0x0060ae10/0x0060ae14); otherwise a null shared_ptr (0x0060ae64).",
    "The selection chooser calls IBehavior::IsRunnable (PLT 0x004b0f54), not IsRunnableScored; IsRunnableScored has no engine caller."
  ]
}
```

```json
{
  "id": "M8-015",
  "title": "ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0053E6A8..0x0053E6E0; 0x005409E2..0x00540A40; 0x00541146..0x00541238; 0x0053FAB2; Current Cancel then delete; pending direct deletion preserves existing result. Cancelled0x02000000 except NotStarted0x02000001; stop before unlock, watcher then broadcast. Existing B-ACTIONS candidate must be checked for full teardown effects and concrete descendants.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u00e2\u20ac\u201cT9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "P3/P4 full ActionList/ActionQueue/IAction lifecycle: ActionList 0x0053F580..0x0053F5F2, ActionQueue 0x0053F5F4..0x0053F70C, IActionRunner 0x00540370..0x0054063E, clock/timeout 0x00540D4A..0x00540E80, CheckIfDone 0x005549B8..0x005549F4 (20261006-M1M2-missing-triage.md P3/P4).",
    "re-analysis/research/20261012-m8-play-animation-actions.md rows C2..C7, L14..L23, D5, S2..S4 and re-analysis/research/20261012-m8-play-animation-actions-check.md (the +0x57 gate on L17/L18/L20, the DeleteActionAndIter guard set at queue+0x10, ActionWatcher Start/EndUpdating 0x00540386/0x0054057E/0x00540630, the UpdateInternal status string 0x00540D46..0x00540F1C).",
    "re-analysis/research/overnight/20261014-ov18-m8-015-check.md: L1-L9 0x00540370..0x0054063A, 0x00540D1C..0x00540F9C, 0x00540750..0x005407C0, 0x005409DC..0x00540A44; Q1-Q7 0x0053F580..0x0053F708, 0x0053DCE4..0x0053DD92, 0x0053E24C..0x0053E2F6, 0x0053DF2C..0x0053DFC8, 0x0053DE10..0x0053DE5C, 0x0053E690..0x0053E700; D1-D6, D12 0x0053F9E4..0x0053FB06, 0x00541084..0x00541286, 0x00541BB4..0x00541C04, 0x0054187E..0x005418DE; T2 List Clear 0x0053D916..0x0053D93A, 0x0053F938..0x0053F950, 0x0053FB5C..0x0053FB82; QueueAction front end 0x0053D93C..0x0053DAC8; position table 0x0053DA24..0x0053DB70.",
    "re-analysis/research/overnight/20261015-ov56-58-check.md: teardown mechanics from OV56: ~Robot order 0x005110EE..0x00511156; AbortAll 0x00511952..0x0051198C (0x0063BE2A..0x0063BE36, 0x00517DFE..0x00517E0A); ActionList::Cancel clearing gate and slot order 0x0053DE18..0x0053DE54; ActionQueue::Cancel 0x0053E69A..0x0053E6FA (early exit 0x0053E6E4); QueueNow/QueueAtEnd 0x0053E25A..0x0053E36E; DeleteActionAndIter 0x0053F9E4..0x0053FAB2 (re-entrancy set, INTERRUPTED/no-interface gates); result codes 0x0053FDBA..0x0053FDD4, 0x005409E2..0x00540A40; Clear lock-out 0x0053D91A..0x0053D938, 0x0053DCEA..0x0053DD00, 0x0053DCAA, 0x0053DF40, 0x0053F93C, 0x0053F970..0x0053F9AC; ~ActionList double Clear 0x0053D8F6..0x0053D90E; IBehavior StartActing reject-delete 0x005BDC04, Init failure 0x005BCCC6..0x005BCD06."
  ]
}
```

```json
{
  "id": "M8-016",
  "title": "Behavior-container destruction and virtual children",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0051112C; 0x0051113A; 0x005A0D22..0x005A0DF6; 0x005A5886..0x005A58D2; BehaviorManager/BehaviorSystem shared/container destruction; virtual child call0x005A58AA and recursive effects UNKNOWN.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u00e2\u20ac\u201cT9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "re-analysis/research/overnight/20261014-ov19-m8-016-check.md: build rows (~Robot slots 0x00511124..0x00511142; BSM ctor 0x005A57E6..0x005A5832 and dtor 0x005A5886..0x005A58D2; BehaviorManager dtor 0x005A0D22..0x005A0DF6; container 0x0059C704..0x0059C730; trees 0x005A479C..0x005A486A; IActivity 0x005B2F8C..0x005B2FEC; Freeplay hash 0x005AED50..0x005AEDBC; IBehavior 0x005BC75C..0x005BC81E; IWantsToRun 0x006134C4, 0x00613C1C)."
  ]
}
```

```json
{
  "id": "M8-017",
  "title": "The compound action runtime: ICompoundAction, CompoundActionSequential and CompoundActionParallel (construction, AddAction naming, update/result tables, retry, completion store, teardown), as used by the go-to-sleep sequence and other compounds",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0052CEA2..0x0052CFC0; Parallel/sequential child execution, ignore-failure predicate and completion/track interactions remain higher framework obligations; M1 owns only factory handoff and NOW/retries0 submission.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1\u00e2\u20ac\u201cT9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "re-analysis/research/20261012-m8-play-animation-actions-check.md addition 4..5: AddAction(IActionRunner*, bool, bool) 0x0054EC7C -> AddAction(function, bool) 0x0054ED90 (empty predicate; child inherits +0x57; compound name appended); the trailing boolean is never loaded.",
    "re-analysis/research/overnight/20261014-ov20-21-check.md: C1 0x0052CECA..0x0052CFA2; C2 0x0054E990..0x0054EA86; C3 0x0053FDB8..0x0053FE1C; C4 0x0054EC7C..0x0054EEAC; C5 0x0054F4F8..0x0054F54C; S1-S8 0x0054F70C..0x0054F992, 0x0054F5A0..0x0054F696, 0x0054F560..0x0054F59E; P1-P5 0x0054FAB0..0x0054FBCC, 0x0054EC56..0x0054EC7A; D1-D4 0x0054F050..0x0054F130, 0x0054F170..0x0054F1BC, 0x0054F270..0x0054F3EA, 0x0054EB78..0x0054EC2A, 0x0054FC2C..0x0054FC66; the seven log sites; AlreadyPrepped call sites 0x0054F0C0, 0x0054FB3C."
  ]
}
```

```json
{
  "id": "M13-014",
  "title": "Knock over a stack: BehaviorKnockOverCubes flow, DriveAndFlipBlockAction and IDriveToInteractWithObject",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorKnockOverCubes body 0x005C2EA0..0x005C3E2A; IsRunnableInternal 0x005C314C needs StackOfCubes::GetStackHeight() >= +0x124 (minimumStackHeight default 3); InitInternal 0x005C31A2 runs the reach unless +0xD9 (alwaysStreamline) or +0xD8 (the soft/hard switch flag) is set (0x005C31B4/0x005C31BA)",
    "TransitionToReachingForBlock 0x005C3254: TurnTowardsObjectAction(max pi), then DriveStraightAction(x-85, 60) when the block x+10 > 85.0, then TriggerLiftSafeAnimationAction(+0x150), then TransitionToKnockingOverStack regardless of result",
    "TransitionToKnockingOverStack 0x005C34A8: TurnTowardsObjectAction(max pi), DriveAndFlipBlockAction with maxTurn = pi/2 on the first attempt (0x005C36BC) and 0.0 once +0x140 > 0 (0x005C36C0); +0xD9 or +0xD8 also forces 0.0 (0x005C34EA..0x005C34F8); WaitAction(0.5); the callback 0x005C3DCE writes AIWhiteboard+0x70 on NoPreActionPoses (0x03000010), re-runs while +0x140 <= 1 on a Retry result and otherwise blind-flips, incrementing +0x140 either way",
    "success goes to TransitionToPlayingReaction 0x005C3908: it sets robot->[+0x34]->[+0x94]->[+0xC] = 1 (0x005C3946..0x005C394E), i.e. the BlockWorld's BlockConfigurationManager dirty flag (BlockWorld+0x94 is the manager; BlockConfigurationManager+0xC is read by its Update 0x00616D84) forcing all block configurations to recompute; the tipped-object set size at +0x14C selects the success trigger +0x15C with BehaviorObjectiveAchieved(0xD, true) and NeedActionCompleted(0) (0x005C3950..0x005C3964) or the failure trigger +0x160 (0x005C396E); when +0xD9 or +0xD8 is set the reaction animation is skipped (0x005C3972/0x005C3978)",
    "IDriveToInteractWithObject 0x0055B1F4 adds TWO actions when maxTurn > 0 (0x0055B37C..0x0055B392): a TurnTowardsLastFacePoseAction (vtable overwritten from TurnTowardsFaceAction at 0x0055B3C4..0x0055B3D8) and a TurnTowardsObjectAction (0x0055B42E/0x0055B43C), both with failure ignored; the trailing float 20.0 is not read by DriveAndFlipBlockAction ctor 0x0055E208",
    "+0xD9 is the JSON config key alwaysStreamline (IBehavior::ReadFromJson 0x005BC208/0x005BC216/0x005BC21E); +0xD8 is computed by IBehavior::Init 0x005BCCAA..0x005BCCC2 as 1 when the behaviour was entered by a hard spark switch and 0 for a soft switch or no current behaviour (corrected operator 2026-10-28: SetRequestedSpark 0x005A3ED4..0x005A3ED8 maps a non-zero bool to \"soft\" 0x00BF0B78 and zero to \"hard\" 0x00BF0B7D)",
    "0x0055B3C0..0x0055B3CC, 0x005DE160..0x005DE16C, 0x005B7DB2..0x005B7DC2 the vtable overwrite; vtable 0x010204F4 slots 2, 4, 6, 9, 10",
    "0x0054B7B6 the ctor store of +0x193; 0x005C229C, 0x005DE170, 0x005F20B8, 0x005F6942 the behaviour stores",
    "0x0054B978..0x0054B9F4 SetSayNameAnimationTrigger; 0x0054C606..0x0054C61E and 0x0054C2D0..0x0054C2E6 the NeedsManager registrations",
    "re-analysis/research/20260929-R-VIS-M12-M13-gap1-extraction.md Q4; re-analysis/research/20260929-R-VIS-verify-M12-M13-gap1.md objection A8 (the class adds no logic of its own)",
    "re-analysis/research/20260929-R-VIS-M13-gap2-extraction.md Q2, Q3, Q6; re-analysis/research/20260929-R-VIS-verify-M13-gap2.md Q2/Q3, Q6 (PASS)",
    "0x0055B2C8..0x0055B438 the IDriveToInteractWithObject ctor compound construction and its AddAction flags (manager read)"
  ]
}
```

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

```json
{
  "id": "M12-003",
  "title": "The dock-message order: CheckIfDone -> DockWithObject -> builder -> send",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x005521AC IDockAction::CheckIfDone; 0x005522AE blx 0x4AB9F8 (DockWithObject)",
    "0x0063BA44 DockingComponent::DockWithObject; 0x0063BD50 builder",
    "0x0063BDB8 blx 0x4B94BC (EngineToRobot(DockWithObject&&)); 0x0063BDC4 blx 0x4A5368 (SendMessage)"
  ]
}
```

```json
{
  "id": "M12-005",
  "title": "Every field of DockWithObject",
  "status": "EXACT_SOURCE",
  "evidence": [
    "word 0 = 0: 0x0063BB34 movs r4,#0 / 0x0063BB7E str r4,[sp,#0x1c]",
    "words 1..3 = IDockAction +0xAC/+0xB0/+0xB4, defaults 60/200/500 at the ctor 0x005502D8 (0x42700000/0x43480000/0x43FA0000)",
    "byte 4 = DockAction +0x80; byte 5 = +0x95; byte 6 = +0xBA (0); byte 7 = +0xBB (0; PickupObjectAction 0x005536EA writes 2, RollObjectAction 0x005565D6 writes 0 (the 5 stored at 0x005565DC is the DockAction byte +0x80, not +0xBB)); byte 8 = +0xC1 (0; PickupObjectAction 0x005536F8 writes 1)",
    "the complete list of +0xBB writers (whole-.text scan, re-analysis/research/20260929-R-VIS-verify-M12-gap2.md item 1): 0x005533F6 AlignWithObjectAction case = 2; 0x005536EA PickupObjectAction = 2; 0x00554E12 PlaceRelObjectAction::InitInternal = 3 iff |B'| >= 1e-5 (movpl, so NaN also sets it); 0x005554D0 PlaceRelObjectAction::SelectDockAction = 0; 0x005565D6 RollObjectAction = 0; 0x0055C5A2 SetDockingMethod = its argument; the ctor default is 0 (the 0x100 word at 0x005503AA). Byte 7 (wire offset 0x13) is DockingMethod = [this+0xBB]; the builder 0x0063BD50 writes +0x10 DockAction, +0x11 bool [+0x95], +0x12 retries [+0xBA], +0x13 method [+0xBB], +0x14 bool [+0xC1]; the +0x95 bool is the IDockAction ctor's last bool (0x00550382), read at 0x00552278"
  ]
}
```

```json
{
  "id": "M12-013",
  "title": "DockingErrorSignal begins with the timestamp, not the geometry",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x0063BE80 UpdateDockingErrorSignal; 0x0063C14A str r6,[sp,#0xa0] (timestamp); 0x0063C15E/0x0063C174/0x0063C180/0x0063C1CE",
    "0x007C0B26 Pack reads words at +0, +4, +8, +0xc, +0x10 then bytes at +0x14, +0x15",
    "the clamp is 40 degrees (M12-019)"
  ]
}
```

```json
{
  "id": "M12-018",
  "title": "The dock abort path",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x0063BE10 AbortDocking; 0x0063BE2A blx 0x4B94C8; 0x0063BE36 blx 0x4A5368"
  ]
}
```

```json
{
  "id": "M12-029",
  "title": "The default +0x150 pose function, the DriveToHelper functor and RemoveMatchingPredockPose",
  "status": "EXACT_SOURCE",
  "evidence": [
    "0x005586AC setter; 0x0055DB72, 0x0055DBDE default functors; 0x00558C80 GetPossiblePoses; 0x005B61A2 the DriveToHelper functor; 0x00551418 RemoveMatchingPredockPose",
    "re-analysis/research/20260929-R-VIS-M12-gap2-extraction.md Q9; re-analysis/research/20260929-R-VIS-verify-M12-gap2.md Q9 (PASS)"
  ]
}
```

```json
{
  "id": "M12-041",
  "title": "Which PathFollowingEvent (0xC6) the robot sends for a cleared or replaced path, and when",
  "status": "HARDWARE_ONLY",
  "evidence": [
    "re-analysis/research/20261013-m12-032-path-status.md: P31: statuses 5 and 6 leave only through the cancelled-path helper (an event with id == +0x4A, 0x0064B620..0x0064B6A0) or the 1 s SentUnreceivedPath abort (0x006494EC..0x00649580); the robot side is firmware, with no capture of 0xC6."
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
  "id": "M7-012",
  "title": "Mood schema, decay graphs, action-result events, affectors, update and game output",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/obb/assets/cozmo_resources/config/engine/mood_config.json",
    "StaticMoodData::ReadFromJson 0x0067ce6c",
    "MoodManager::Update 0x0067b5d4",
    "MoodManager::SendEmotionsToGame 0x0067b724..0x0067b7f8"
  ]
}
```

```json
{
  "id": "M7-020",
  "title": "Mood event production, repetition penalty and affector application",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "MoodManager::TriggerEmotionEvent 0x0067b85c",
    "MoodManager::AddToEmotion 0x0067bdca",
    "MoodManager::HandleActionEnded 0x0067c770",
    "EmotionAffector::ReadFromJson 0x00679910",
    "MoodManager::SendEmotionsToGame 0x0067b724"
  ]
}
```

```json
{
  "id": "M1-034",
  "title": "Handler isolation, a deliberate departure: in the original a handler exception aborts the engine process",
  "status": "COMPATIBILITY_POLICY",
  "evidence": [
    "G3.1..G3.7 the dispatch path from the signal emit to ProcessArrivedMessages and ProcessMessages has only cleanup landing pads that end in _Unwind_Resume (LSDAs 0x00B3F07C, 0x00B3F124, 0x00B3EF3C, 0x00B28A14); the only catch(...) pads go to 0x004E39F8 = __cxa_begin_catch; std::terminate (0x004E39FA, 0x004E39FE)",
    "G3.8 no frame up to the engine thread top catches (CozmoEngine::Update 0x004ED62E, CozmoInstanceRunner::Run 0x0065B420, __thread_proxy 0x0065C280)",
    "G3.10..G3.14 the EH runtime is the shipped libc++_shared.so (NEEDED order libc, libm, libc++_shared); __cxa_throw with no handler calls std::__terminate with the default handler (libc++_shared.so 0x0007A948..0x0007A98C, 0x0007B20C, reloc 0x000A6010 -> 0x00068B7D)",
    "G3.15/G3.16 the default handler writes \"terminating with uncaught exception of type T: what()\" to stderr and aborts (libc++_shared.so 0x00068B84..0x00068C4A, abort_message 0x00068AA2..0x00068ACE)",
    "G3.17 the engine never installs another terminate handler: its only set_terminate call is set_terminate(nullptr) in cozmo_shutdown (0x00667862..0x00667864)"
  ]
}
```
