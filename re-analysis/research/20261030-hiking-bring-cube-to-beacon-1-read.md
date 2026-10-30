# HIKING-BRING-CUBE-TO-BEACON-1 — bounded own-path extraction

Request: `re-analysis/jobs/trial-m13/HIKING-BRING-CUBE-TO-BEACON-1.md`. Target only the shipped `Hiking_BringCubeToBeacon` / `BringCubeToBeacon` class (`BehaviorExploreBringCubeToBeacon` native name). Requested reader gpt-6.1-sol / medium; actual backend unobserved. No implementation, official record, approval, state, git, .NET or hardware operation performed.

Primary binary SHA-256 checked: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`, `resources/lib/armeabi-v7a/libcozmoEngine.so`. The decomp index exists but the requested bodies are absent on this machine; instructions were read directly with Capstone/LIEF using explicit Python313. `known.py` was used before class/routine tracing. Existing floor-placement and runnable-gate reads are reused below at their precise boundaries, not repeated as new discoveries. No source closure is inferred from the C# implementation.

**Known annotation conflict, reported to manager before any dependent build:** current M13-006 is EXACT_SOURCE and its evidence/approved M13-navigation N25 calls beacon+0x10 “an int”. It is a binary32 seconds stamp. Native producer 0x0059C318/31C calls BaseStationTimer seconds and 0x0059C320 stores r0 at +0x10; Bring's 0x005DF148 loads it into s16 and 0x005DF14C..194 use f32 abs/add/compare. Current M15-023 already explicitly records the f32 stamp. Approved M15-freeplay inventory Correction A4 (line846) already says “+0x10 is an f32 time stamp” and explicitly notes M13-006 calls it an int. The live C# AIBeacon already holds a float. This is a pre-existing cross-record annotation conflict, not a newly discovered unapproved behavioral contract or a blocker to following approved M15-023. Defer the official M13 wording correction to Claude. The exact current records are appended below.

## Evidence reuse and boundary

- `research/20261029-think-about-beacons-1-read.md` is a Claude-run read with manager spot-check, **not an independent-verifier PASS** (its Claude-check explicitly queues that review). Reuse only its established beacon producer: same robot-owned AIWhiteboard, root-relative main pose, radius175, failure stamp0. Independently checked producer call sequence 0x005E5F12..30 here: `[robot+0x264]+0x18`, Robot::GetPose, GetWithRespectToRoot, AddBeacon. No Think lifecycle re-extraction.
- `research/20261003-R-FIX2-M15-gap1-extraction.md`, sections1–5, covers existing floor search, placement children, callbacks and failure memory. Current M15-019..026 explicitly own these paths and their outstanding seams. Its old UNKNOWN list is not treated as current: current manifest records include subsequent pose/math/verify reads. Below “shared read” means this retained primary-address contract, not a newly settled record.
- `research/20261007-q17-runnable-rows.md` RV39 covers the three vtable gates; directly verified vtable0x0102957C and +0x28's `movs r0,#1; bx lr` at0x005E0DF0..F2. Generic IsRunnableBase/StartActing/StopActing/chooser machinery remains M8; no generic helper audit.

## Whole configured path

“EXACT_SOURCE” below labels the recovered step only, not the completeness/status of its owning production record. Shared boundaries retain their existing gaps.

| Step | Original behavior, inputs/order/result | Primary address/file | Record boundary; extraction class |
|---|---|---|---|
| A1 | Factory class1 constructs native Bring object, allocation0x138; shared container owns it. | factory case0x0059C968; ctor0x005DEFB0 | M7-018; existing checked factory contract |
| A2 | Config ID Hiking_BringCubeToBeacon, class BringCubeToBeacon, displayNameKey behavior.BringCubeToBeacon; params recentFailureCooldown_sec45.0. No own animation/needs/timeout key. | `obb/assets/cozmo_resources/config/engine/behaviorSystem/behaviors/freeplay/hiking/Hiking_bringCubeToBeacon.json`:9–17 | M7-001/002; EXACT_SOURCE asset |
| A3 | Parent Hiking BehaviorsOnly uses Scoring; this is ordered entry7, flatScore4.0. Current-behavior score bonus1.0. Higher-scoring Think entry6 has8.0 and creates the beacon when none exists. Hiking driving start/loop/end triggers and activity analyzer belong to the parent, not this class. | `.../activities/freeplay/hiking.json`:1–17,54–64; source-map R025 | M8-003/M15-008 shared admission; EXACT_SOURCE asset inputs |
| C1 | Base IBehavior ctor, own candidate ObjectInfo vector empty(+0x11C/+0x120/+0x124), own ObjectID at+0x128 with value-1 at+0x12C; installs derived vptr. No own LoadConfig call in this constructor. |0x005DEFB2 base call;0x005DEFBE..DA stores| M15-026 for target/vector; own ctor/config NEW if no existing lifecycle owner; EXACT_SOURCE |
| C2 | LoadConfig parses float `recentFailureCooldown_sec`, stores binary32+0x130. |0x005DF082..90; key literal0x005DF0E0 | M15-023/026 shared input; EXACT_SOURCE |
| G1 | Vtable +0x20 returns0: cannot run off treads; +0x24 returns0: cannot run with robot+0x34A; +0x28 returns1: may run while carrying. Other gates are base defaults; shipped config does not add overrides. | vtable0x010295A4/A8/AC;0x005BF04C,0x0059EC12,0x005E0DF0 | M8-001 shared gates; EXACT_SOURCE |
| R1 | Every IsRunnableInternal call clears its cached ObjectInfo vector first. It takes whiteboard from the robot AI component, reads active beacon; absent => false. |0x005DF10C..42,0x005DF144 beq0x005DF2D8 | M15-020/026; EXACT_SOURCE |
| R2 | Read beacon stamp t=f32+0x10. If abs(t)<1e-5 skip cooldown. Otherwise reject iff f32(f32(t+cooldown)-1e-5)>f32(timer seconds). Epsilon bits3727C5AC/B727C5AC; native conditions also determine NaN behavior, not host double arithmetic. |0x005DF148..194, literals0x005DF334/338 | M15-023; EXACT_SOURCE |
| R3 | Call FindUsableCubesOutOfBeacons(vec). Its vec is cleared; no beacons yields empty. If strict carried ID+8!=-1, lookup it and return only that ObjectInfo if located; missing lookup logs and leaves empty. It does not add a nearest cube or use failure memory itself. |0x005DF1A4;0x0056AE6C..F2; missing-object branch0x0056AEBA→AFC2 | M15-020 shared producer; primary entry/strict-carried branch checked |
| R4 | Otherwise its family filter is Block/LightCube. Per-object predicate first CanPickUpObject; then rejects if within ANY beacon at margin0; appends ObjectInfo{ID,family}. Current origin/filter iteration is the existing BlockWorld contract, not distance sorting. |0x0056AEF4..B0AE; predicate0x0056D028..A8 (`CanPickUpObject`0x0056D03A; beacon0x0056D05A) | M15-020 shared read; iteration/current-origin readiness seam retained |
| R5 | For each usable ObjectInfo, current family-aware located lookup; missing skipped. DidFailToUse(id,set{0,3},cooldown,object pose,20.0f,pi/8) true skips; else copy ObjectInfo into own vector. No target selection yet. Runnable iff own vector nonempty. |0x005DF1E2..294; table0x00C6D6A0 words0,3; params0x005DF222..250;0x005DF29C..A6 | M15-020/024; EXACT_SOURCE call/ordering |
| I1 | Init clears target to-1. If strict carrying present, target=own cached vector[0].id and transition directly to placement. Otherwise TransitionToPickUpObject(robot,attempt1). It does NOT rebuild a missing candidate vector or rerun eligibility here. |0x005DF350..37E | M15-026; EXACT_SOURCE |
| I2 | Init result0 if action pointer+0x84 nonnull. Else read active beacon stamp and timer; returns1 when abs(f32(now-stamp))>=1e-5 (native PL includes unordered), else0. This lets NoFreePoses stamped at this instant start successfully with no action. |0x005DF382..3DA; compare/PL0x005DF3C6..D0 | M15-026; EXACT_SOURCE; no fake completion |
| P1 | PickUp transition errors on empty cached vector (NoCandidates). If target already set, reuse it unchanged on retry. Otherwise Robot::GetPose is the reference; current located lookup per cached ID/family; failed lookup or GetWithRespectTo yields FLT_MAX distance. |0x005DF8DE..EC;0x005DF9C6;0x005DFA08..16;0x005DFACC..B38 | M15-026; EXACT_SOURCE |
| P2 | Select by binary32 ((x*x+y*y)+z*z), initial FLT_MAX, replace iff best-1e-5>d2. Strict tie behavior preserves earlier candidate. If none beats FLT_MAX, InvalidCandidates; else stores ID. This is not planar distance and not nearest cached object reference. |0x005DFB3A..B5E;0x005DFB66..84;0x005DFB94..BB0;FLT_MAX0x005DFCDC | M15-026; EXACT_SOURCE |
| P3 | Start real DriveToPickupObjectAction(robot,target,false,0.0f,false,Radians0,false,trigger0x23F), callback captures behavior/robot/attempt. No extra own timeout or pretend success. |0x005DF95E..9B2, ctor0x004A93B8 | M15-026 plus shared native IDrive/Pickup children; EXACT_SOURCE call; child's remaining gaps retained |
| P4 | Completion category=result>>24. 0 logs PickedUp then transitions. 4 with strict carriedID==own target transitions regardless attempt; else attempt<=2 signed retries same target with attempt+1; spent retries fail. 3 logs NoRetry and fails. Other categories do nothing. Failure looks up target and if present SetFailedToUse(object,PickUpObject0), using its own pose. |0x005E0FD8..FF6;0x005E106A..84;0x005E10F0..F4;0x005E1166..1186;0x005E118C..1210;spent tail0x005E121A | M15-026/024; EXACT_SOURCE own callback; logs reused existing read |
| O1 | Placement transition erases debug locations first; requires strict carried ID equals own target and !=-1; else diagnostic and return. Lookup own target any family; absent diagnostic and return. Active beacon absent => return with no action. |0x005DF3F2..460;error branches0x005DF4F8..59A | M15-008/026; EXACT_SOURCE |
| S1 | Try stacking first. Require ProgressionUnlockComponent IsUnlocked(10,true) exactly1. Filter family Block/LightCube; derive margin from carried object's ROOT-frame X dimension+10.0f; first matching located object wins. |0x005E00FE..114;0x005E01BC..210;0x005E0240..246 | M15-008/020 shared dependency; EXACT_SOURCE call/ordering; no default-unlocked claim |
| S2 | Stack predicate: reject identical carried pointer; require pose-state byte1; reject recent StackOnObject1 failure using cooldown/current candidate pose/20mm/pi8; require beacon membership with margin above; then CanStackOnTopOfObject. |0x005E1CC6..D50 | M15-020/024; EXACT_SOURCE order; progression/docking/shared world contracts remain dependencies |
| S3 | Match => log; TryToStackOn(targetID,1); return, with NO PickupCube need report. Construct DriveToPlaceOnObjectAction(robot,stack target,false,0.0f,false,Radians0,false), StartActing callback captures stack target ID by value and attempt. |0x005DF4EA..F6;0x005DFDEC..E4A | M15-008; EXACT_SOURCE own call; native child dependency retained |
| S4 | Stack callback: category0 => NeedActionCompleted(0x2A) THEN FireEmotionEvents. Category4 retries same stack target iff still carrying own picked-up target and attempt<=2, else common failure. Category3 common failure. Other categories return doing nothing. Common failure lookup uses captured STACK target and SetFailedToUse(object,StackOnObject1). |0x005E1434..44A;0x005E14B4..BE;0x005E14C4..EA;retry0x005E1568;0x005E1660..1680 | M15-008/024; EXACT_SOURCE; picked-up target versus stack target must not be conflated |
| F1 | No stack target => NeedActionCompleted(PickupCube0x1F) BEFORE searching floor, independent of its result. FindFreePoseInBeacon(carried,beacon,robot,out,cooldown). |0x005DF59C..5E0 | M15-025; EXACT_SOURCE |
| F2 | Frame: nearest in-beacon cube's root Z rotation if available; else planar robot→beacon heading acos/sign; degenerate direction angle0. Beacon translation copied to root, robot uses checked main pose; candidates parented to current WorldOrigin. Dimensions come from real carried object virtual size, not host mock size. |0x005E0378..580;0x005E07D8..93A;candidate0x005E09E0..A9E | M15-019/020 shared read; pose/libm/current-origin seams retained |
| F3 | S=size.x+10, N=trunc(radius/S); i0,-1..-N then1..N; each j0,-1,+1..-N,+N. Write out candidate BEFORE tests; first accepted wins. Tests radius^2 strict; then failure2 anyID -1 cooldown/100mm/pi; then current-origin intersecting-object query (ignore carried ID,10mm padding,no Z test). |0x005E055A..626;0x005E09E0..D2A | M15-019/024 shared read; no new floor-grid recovery |
| F4 | Accepted=>log, TryToPlaceAt(pose,1). Action is PlaceObjectOnGroundAtPoseAction(robot,pose,false,false,true,10.0f); callback OWNS copied pose and attempt. Its ordered native children: DriveToPlaceCarriedObjectAction then PlaceObjectOnGroundAction, proxy latter's tag. |0x005DF5E4..696;0x005DFEF4..FF80;shared ctor0x00554AFC..BAC | M15-021/022; EXACT_SOURCE own call; shared children retained |
| F5 | Not accepted=>stamp active beacon failure time first, update debug render, log NoFreePoses, trigger HikingNoLocationAtBeacon using MoodManager time, return with no action. |0x005DF69C..73C;0x0056C3F0..400;stamp0x0059C314..322 | M15-023; EXACT_SOURCE |
| F6 | Floor callback category0 => log then FireEmotionEvents, NO needs call. Category4 iff carrying own target and attempt<=2 => same copied pose/attempt+1 (no fresh search). Otherwise any nonzero category logs failure and if own target still located SetFailedToUse(object,PlaceObjectAt2,copied candidate pose). No new action after final failure. |0x005E188C..8A8;success0x005E1916;retry0x005E191C..946,0x005E19EE..9FC;failure0x005E1B32..56 | M15-021/025/024; EXACT_SOURCE own callback |
| K1 | FireEmotionEvents calls AreAllCubesInBeacons. True => HikingBroughtLastCubeToBeacon; otherwise HikingBroughtCubeToBeacon. Uses robot+0x440 mood and MoodManager current seconds. No direct objective report in this routine. |0x005E002C..96;strings0x005E00CC,0x005E00B0 | M15-009/020; EXACT_SOURCE selector/call |
| K2 | AreAllCubesInBeacons false if no beacon or strict carrying. Otherwise counts Known Block/LightCube objects within any beacon and compares with BlockWorld family2 counter/map value (missing entry=0). This is NOT established as located-LightCube count. |0x0056B450..61A;0x0056B462..476;map0x0056B5C4..5F8 | M15-020 shared read; counter producer UNKNOWN remains |
| U1 | No own Update override. Base returns1 while action pointer+0x84 exists,2 otherwise. Callback transitions can install the next action before next update. No own timer completion. |vtable0x01029590;0x005BDA56..62 | M8-001 shared; EXACT_SOURCE |
| U2 | Resume uses base: re-run IsRunnableBase then derived IsRunnableInternal (refreshing cache); if either not exactly1 return1; else set+0xA1 and call InitInternal. Resume is neither unconditional refusal nor an action-resume shortcut. |vtable0x010295D0;0x005BDA94..C8 | M8-001 + M15-026; EXACT_SOURCE |
| T1 | Own Stop only erases debug locations; does not clear beacons, failure memory, cached vector, target or report needs. Generic stop/action cancellation belongs to IBehavior owner. Destructor destroys vector then base. |0x005DFD50..86;0x005DF020..3C | own Stop NEW if no lifecycle owner; M8-001 shared stop; EXACT_SOURCE |
| B1 | Shared beacon state is robot AIComponent+0x18 whiteboard, not per behavior. GetActiveBeacon is vector begin (oldest), null iff empty. Think's producer uses same owner; Bring never manufactures/moves/removes a beacon itself. |0x005DF136..142;0x0056C404..41E;producer0x005E5F12..30 | M13-006/M15-011/084; EXACT_SOURCE owner join; f32 follows approved M15-023/Correction A4 |
| B2 | Delocalization AI recipient gets this whiteboard, removes possible objects from zombie maps, updates possible-object rendering, then ClearAllBeacons. It does NOT clear object-failure tables. Clearing invalidates active beacon and affects Bring admission/placement; no saved pointer is captured in action callbacks (they fetch active again). |0x00569F4E;0x0056A686..69A;veneer0x008CB6EC..F8 resolves PLT0x004ACAFC; placement0x005DF454..60 | M11-069/M8-014 shared boundary; actual production hookup MISSING |

The routine has no own SmartDisable/reaction-lock call, own animation request, movement wire send, direct objective/engine-to-game message or local timeout beyond its selected child actions. The native child results and carrying/located state are required inputs, not values supplied by elapsed host time. Needs0x1F/0x2A and mood events are real downstream effects; their shared sinks retain current records/gaps.

## Specific gaps and handoff limits

1. **MISSING — actual native input/owner joins:** this class must consume the native robot's AIWhiteboard, checked main pose/current origin, strict carrying state, current family-aware object lookup, unlock10 and native child action/result owners. The existing facade's host pose fallback, rebuilding candidates in Start, treated-as-unlocked fallback, substitute DockHelper actions, generic Scope.DisableReactions and per-object sorting are implementation facts, not recovered behavior. This report does not authorize a fabricated bridge or sibling behavior enablement.
2. **MISSING — delocalization whiteboard recipient:** current `CozmoRobot.cs` component hook reports AIComponent M11-069 MISSING; `AIWhiteboard.OnRobotDelocalized` has no production caller and currently also clears failure tables. Native narrow body above clears beacons, not failures. Do not simply wire that current method and silently erase failure memory. Removal of zombie possible objects/rendering are shared interfaces, not a reason to audit the whole possible-object subsystem here.
3. **MISSING — AreAllCubesInBeacons counter:** BlockWorld map's family2 value writer/identity is not established by this read. Current M15-020 explicitly retains it; a located-count substitute cannot claim the last-cube event is recovered. Narrow producer investigation may be a follow-up; no broad BlockWorld audit performed.
4. **Shared known gaps, no fresh closure:** floor geometry's libm/pose arithmetic and native virtual dimensions, FindLocatedObjectHelper iteration/current-origin input, DriveToPickupObjectAction/IDrive child omissions, DriveToPlaceOnObject/PlaceRel native joining, placement verification/firmware state transitions, mood/needs/report sinks. Check current official records, not old comments saying a child is unbuilt; previous jobs have built some children. These are named dependencies, not permission to rerun those finished reads.
5. **Fault policy:** bypassing required IsRunnable cache can make carried Init read vector[0] unchecked; losing active beacon before Init's no-action tail can dereference null at0x005DF396. Native has no supported empty-vector fallback or fake beacon. M1-034 applies if a real supported host path reaches such native fault: record owning scope/address and smallest visible skip. No new departure requested. Normal admission must preserve source cache/owner ordering.
6. **Official records:** M15-026 includes real DriveToPickup body; M15-019/020 include shared geometry/world predicates; M15-008 includes other activities. None can settle merely because own call sites are correct. M15-009's selector is read but counter semantics stay open. M15-023/025 have bounded own call sequences but need actual production binding/verification; this reader does not settle them. A coherent NEW own lifecycle proposal, if needed, should cover constructor/config, gates, cached eligibility, dispatch/init result, update/resume/stop without absorbing the already owned shared children. No new ID proposed or added here.
7. **Source wording correction:** M15-011 currently says “full copied robot pose”; producer0x005E5F24 is GetWithRespectToRoot. It preserves full pose transform w.r.t. root, not the original parent attachment. This is the checked Think clarification, rechecked only at the producer Bring depends on. M13-006's int error and approved-inventory counterpart remain explicit rather than silently rewritten.

## Procedure and questions

Known searches performed for BringCubeToBeacon,0x005E09E0,0x0056AE58,0x0056B200,0x005E0FD8,0x005E142C,0x005E188C,0x0056A686,0x0059C314,0x005E0DF0,0x00569F4E. The0x0056D028 helper was read as the already documented FindUsable predicate under that routine's known search; its exact-address known search was run afterwards, so the exact-address-first order was not met for that local helper. No evidence or previous read was lost; this procedural exception is recorded rather than represented as perfect compliance. No network, hardware, build, tests or git operation. The report is the sole output.

Questions for manager/source check: choose the bounded release with explicit MISSING joins; retain the family2 counter producer as UNKNOWN unless a narrow already-read contract supplies it. Official cross-record wording cleanup is deferred to Claude; the approved M15-023/Correction A4 f32 contract supplies this behavior without a fresh operator checkpoint. No feature exclusion or invented result is requested.

## Current literal manifest snapshots

These are read from the manager worktree's current manifest, before coverage/closure claims above; they are claims, not primary evidence. All title/status/evidence/unresolved fields are reproduced verbatim below. No official status is modified.

### M13-006

```json
{
  "id": "M13-006",
  "title": "The whiteboard keeps a list of beacons and the active one is the first",
  "status": "EXACT_SOURCE",
  "evidence": [
    "AIWhiteboard::AddBeacon 0x0056C39C appends a 20-byte AIBeacon (Pose3d, a float radius at +0xC, an int at +0x10) to the vector at whiteboard+0x60, growing it through __emplace_back_slow_path (0x0056C3A8/0x0056C3D2)",
    "ClearAllBeacons 0x0056AA08 walks it back (0x0056AA10 subs r0,#0x14)",
    "GetActiveBeacon 0x0056C404 compares begin/end and returns begin, or null when equal (0x0056C408/0x0056C40C); the active beacon is the oldest still standing",
    "FailedToFindLocationInBeacon 0x0056C3F0 forwards to AIBeacon::FailedToFindLocation"
  ],
  "unresolved": ""
}
```

### M15-008

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
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-04): TransitionToObjectPickedUp in the engine's order (carried-object check, lookup, active-beacon check, FindFreeCubeToStackOn 0x005DF46A, TryToStackOn 0x005DF4F2, then NeedActionCompleted PickupCube 0x005DF59C), AIBeacon::IsLocWithinBeacon, the stack-callback StackCompleted per lambda 0x005E142C (category 0 StackCube 0x2A then FireEmotionEvents; category 4 retries only while carried == candidate and attempt <= 2; category 3 and a failed retry test log and SetFailedToUse(target, 1); every other category does nothing); the floor branch is now built (M15-019..M15-025). Still open: DriveToPlaceOnObjectAction (0x005DFE0E) is unbuilt: the stack branch runs the old PlaceRelObjectHelper as a labelled stand-in; ProgressionUnlockComponent::IsUnlocked(StackTwoCubes = 10) has no component (a live-path default: treated as unlocked); ActivityGatherCubes (Update 0x005AF27C, NeedActionCompleted(0x14) at 0x005AF2F0, its gate 0x005AF2A8..0x005AF2AE and the one-shot flag at +0x78) and the PeekABoo/TrackLaser/GuardDog behaviours are not built; the retry-taken branch of StackCompleted has no test. OV36 check (re-analysis/research/overnight/20261015-ov36-37-check.md): TrackLaser's colour snapshot/restore is M7-025 (to build); the rest is M7-029."
}
```

### M15-009

```json
{
  "id": "M15-009",
  "title": "Emotion event names fired when a cube is placed in a beacon",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorExploreBringCubeToBeacon::FireEmotionEvents 0x005E002C"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-04): the local free-pose search and the invented stand pose are removed; FireEmotionEvents (0x005E002C..0x005E0096) is kept; the route to it is built (M15-019..M15-025: FindFreePoseInBeacon, TryToPlaceAt, PlaceObjectOnGroundAction, the NoFreePoses branch, the failure memory). Still open: the floor path's open items are in those records; the pick-up phase is M15-026."
}
```

### M15-011

```json
{
  "id": "M15-011",
  "title": "Freeplay beacon is centered on the robot pose",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorThinkAboutBeacons::SelectNewBeacon 0x005E5F0C",
    "AIWhiteboard::AddBeacon 0x0056C39C"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-03): SelectNewBeacon passes the full copied robot pose (z and rotation included) to AddBeacon (0x005E5F1A..0x005E5F30). Still open: none known."
}
```

### M15-019

```json
{
  "id": "M15-019",
  "title": "FindFreePoseInBeacon: the candidate frame, grid, order and the three tests",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorExploreBringCubeToBeacon::FindFreePoseInBeacon 0x005E0378..0x005E0666 (static: r0 object, r1 beacon, r2 robot, r3 &out, float on the stack; the rest of 0x005E0378..0x005E139F is helpers and lambdas); frame = rotation about Z from the nearest cube in the beacon (CalculateDirectionalityClosest 0x005E07D8), else the robot-to-beacon heading via acosf, else angle 0",
    "grid S = size.x + 10.0f (0x41200000), N = trunc(radius / S); order i = 0, -1..-N, then 1..N; for each i, j = 0, -1, +1, ..., -N, +N; candidate = beacon translation + R*(S*i, S*j, 0), parent = world origin; *out is written before any test",
    "tests in order: (1) squared distance of the rotated offset > radius^2 rejects (strict; AIBeacon::IsLocWithinBeacon is not used here); (2) DidFailToUse(-1, PlaceObjectAt = 2, cooldown, pose, 100.0f 0x42C80000, pi 0x40490FDB) (call 0x005E0B78); (3) FindLocatedIntersectingObjects with the carried id ignored, padding 0x41200000, no Z test; returns 1 on the first free candidate",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-04): FindFreePoseInBeacon (0x005E0378..0x005E0666) in binary32: the frame (FindCubesInBeacon, CalculateDirectionalityClosest 0x005E07D8, else the robot-to-beacon heading via acosf, else 0), S = 44.0f + 10.0f (the LightCube BlockInfo entries, Block::LookupBlockInfo 0x004E4C8C, all 0x42300000), N = trunc(radius/S), the i/j candidate order, candidate = beacon translation + R*(S*i, S*j, 0) through UnitQuaternion<float>::operator* 0x00849C1C with *out written before any test, the three tests (radius strict; DidFailToUse(-1, 2, cooldown, pose, 100.0f, pi) via 0x005E0B78; FindLocatedIntersectingObjects with the carried id ignored, padding 0x41200000). Still open: MathF Cos/Sin/Acos/Atan2 stand in for bionic cosf/sinf/acosf/atan2f (the expected bits come from Python libm, not bionic); the stack's double Pose3d/Mat3 are narrowed to f32 (the engine's Rotation3d::GetRotationMatrix Renormalize 0x008494E0 is unread); FindLocatedObjectHelper's iteration order is assumed (family, type, id); BlockWorld::ClearLocatedObjectByIDInCurOrigin (after a failed verify) is not built; a null robot pose ends the behaviour (an invented gate, reported MISSING). Pose3d read 2026-10-13 (re-analysis/research/20261013-pose3d-arithmetic.md): the arithmetic is M11-065, the tree M11-066, unread pieces M11-067. Renormalize 0x008494E0 is now read (R18); only the cv::Rodrigues helper stays open (M11-067)."
}
```

### M15-020

```json
{
  "id": "M15-020",
  "title": "CalculateDirectionalityClosest, the FindCubesInBeacon predicate and AIBeacon::IsLocWithinBeacon's formula",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "CalculateDirectionalityClosest 0x005E07D8: the rotation about Z from the nearest cube in the beacon; AIBeacon::IsLocWithinBeacon 0x0059C24C: x*x + y*y + z*z in f32 against (radius - margin)^2 plus 0x3727C5AC, `ge`; used only by FindCubesInBeacon (with extra 0)",
    "the function at 0x005E06D8..0x005E07B0 (an averaging function) has no caller found (extraction section 8 item 6)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-04): CalculateDirectionalityClosest, the FindCubesInBeacon predicate (0x0056D12C; IsCarryingObjectId [+8] or [+0x14], 0x00633F88, at 0x0056D142), AIBeacon::IsLocWithinBeacon (0x0059C24C: f32 3-D against (radius - margin)^2 + 0x3727C5AC), IsRunnableInternal's candidate filter (0x005DF1BA..0x005DF250: the set {PickUpObject, RollOrPopAWheelie} from the word table 0x00C6D6A0, DidFailToUse(candidate id, set, cooldown, pose, 20.0f, pi/8)), FindUsableCubesOutOfBeacons (0x0056AE58), CanPickUpObject (0x0063C7F0 with the f32 IsPoseTooHigh 0x00877954..0x008779BC, shared with CanStackOnTopOfObject), AreAllCubesInBeacons (0x0056B450). Still open: AreAllCubesInBeacons compares with the BlockWorld map value at +0x48 (0x0056B5C4..0x0056B5F8) whose writers are unread: the stack uses the located LightCube count (reported MISSING); the function at 0x005E06D8..0x005E07B0 has no caller found; the stack-only IsCarrying filters keep the strict [+8] semantics (the engine's filters there are unread)."
}
```

### M15-021

```json
{
  "id": "M15-021",
  "title": "TryToPlaceAt and its callback: arguments, attempt counter, the same-pose retry and the failure tail",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "TryToPlaceAt builds a CompoundActionSequential of DriveToPlaceCarriedObjectAction(placeOnGround = 1, +0x178 = 0, +0x179 = 1, padding 10.0f) and PlaceObjectOnGroundAction; callback 0x005E188C: success -> FireEmotionEvents (no NeedActionCompleted); a RETRY category retries with the same pose only while still carrying the target and attempt <= 2 (3 attempts); categories 1, 2, 3 or a non-retryable RETRY end in SetFailedToUse(obj, 2, candidate pose) (0x005E1B56)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-04): TryToPlaceAt (0x005DFEF4) and the callback 0x005E188C: three attempts, the same-pose retry only while carrying the target and attempt <= 2, success calls FireEmotionEvents with no needs call, every non-zero non-4 category and a non-retryable RETRY end in SetFailedToUse(obj, 2, candidate pose) (0x005E1B56) after the log; the compound's default RetriesRemain is 0 (0x00540836), so a sub-action's RETRY ends it with that result. Still open: the compound's own timeout slot is not read; DriveToPlaceCarriedObjectAction extends DriveToObjectAction whose own IAction timeout slot is unread (M12-030's C# has none)."
}
```

### M15-022

```json
{
  "id": "M15-022",
  "title": "PlaceObjectOnGroundAtPoseAction composition and PlaceObjectOnGroundAction Init/CheckIfDone",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PlaceObjectOnGroundAction Init sends message 0x44 (0, 0, 0, 100, 200, 500 as raw bits), builds a TurnTowardsObject verify action and calls StopAllMotors; CheckIfDone never reads DockingComponent+5, its result is the verify action's (extraction sections 2.3..2.8; construction of PlaceObjectOnGroundAtPoseAction at 0x005DFF14)",
    "M12-015 owns only the wire message of PlaceObjectOnGround (its evidence does not cover the action)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-04): PlaceObjectOnGroundAction as the engine's one class: Init (0x00554794) sends message 0x44 then builds the verify action, StopAllMotors, the reaction lock 'placeOnGroundAction' (table 0x00C55A21, only trigger 8 set) removed in the destructor (0x005546D0) when the state is not NOT_STARTED; CheckIfDone (0x005549B0) gates on the IS_PICKING_OR_PLACING latch, never reads DockingComponent+5, the result is the verify action's; the 30.0f timeout slot (0x0052B0C2); the verify child is TurnTowardsObjectAction built from Init 0x0054A1F0 and CheckIfDone 0x0054ADD4 with the visual-verify byte clear (the marker choice with vsqrt.f32; the head-angle fallback GetAbsoluteHeadAngleToLookAtPose 0x0054B428 and ComputeHeadAngleToSeePose 0x00518344 built, confirmed by a Unicorn run of the shipped instructions; the head-cam z used there is the engine's -8.0: see M11-054); every caller takes the lock through ManipulationSystem.ReactionLocks when a freeplay stack exists. Still open: TurnTowardsObjectAction's TrackObjectAction branch (+0x192 set, 0x0054AE08) and its unreachable VisuallyVerifyObjectAction branch (0x0054AED8) are unbuilt, the single RUNNING tick (0x0054AE40..0x0054AEB8) is not modelled, a marker whose with-respect-to fails (0x03000005) is not coded; ComputeHeadAngleToSeePose's pose algebra is the stack's double Pose3d narrowed to f32; M12-015 owns only the wire message; whether the firmware raises IS_PICKING_OR_PLACING for message 0x44, and its order against PickAndPlaceResult, is HARDWARE_ONLY; callers without a manager (ManipTool, Motion with no stack) do not take the lock (reported MISSING)."
}
```

### M15-023

```json
{
  "id": "M15-023",
  "title": "The NoFreePoses branch: FailedToFindLocationInBeacon stamp, the log, the 'HikingNoLocationAtBeacon' event and the IsRunnable cooldown",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x005DF69C..0x005DF73A: AIBeacon::FailedToFindLocation (0x0059C314..0x0059C322) stamps [beacon+0x10] (an f32 time) with BaseStationTimer::GetCurrentTimeInSeconds(); then the 'all poses failed' log, then the mood event 'HikingNoLocationAtBeacon' (24 chars), no action started; IsRunnableInternal (vldr s16,[r0,#0x10] 0x005DF148) then blocks for recentFailureCooldown_sec (45.0f hiking, 5.0f sparks)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-04): the NoFreePoses branch (0x005DF69C..0x005DF73A): AIBeacon::FailedToFindLocation stamps an f32 time (0x0059C314..0x0059C322; the stack's AIBeacon now holds it as FailedToFindLocationTimeSec), the log, the mood event 'HikingNoLocationAtBeacon', no action started; IsRunnableInternal's cooldown in f32 (45.0f hiking, 5.0f sparks; 0x005DF148..0x005DF194). Still open: other readers of [beacon+0x10] (e.g. BehaviorThinkAboutBeacons) were not scanned.\nB-M5-16 fixture correction: the NoFreePoses/needs-order regression enters the checked already-carried Init branch 0x005DF364..0x005DF372; it no longer claims to complete a pickup drive through the unbuilt M12-024 lifecycle. All placement-stage expected ordering/timestamp/no-action assertions remain."
}
```

### M15-024

```json
{
  "id": "M15-024",
  "title": "The whiteboard failure memory: tables, caps, FailureInfo and EntryMatches",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "AIWhiteboard::DidFailToUse / SetFailedToUse with a pose: per-object-id tables with caps {1, 1, 10, 1} (PlaceObjectAt keeps the last 10); EntryMatches expiry is strict with 1e-5f slack; id -1 means any object; the floor path's DidFailToUse is 0x005E0B78 (failure 2, id -1, 100.0f, pi) and the stack-on filter's is 0x005E1D24 (failure 1, 20.0f, pi/8)",
    "Pose3d::IsSameAs_WithAmbiguity 0x00846F3C (the per-axis and angle test) is unread: RECOVERABLE_GAP",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-04): the whiteboard failure memory: four per-object tables with caps {1, 1, 10, 1} (SetFailedToUse 0x0056B6D0, table 0x0056B8A4), FindMatchingEntry 0x0056BF98 and EntryMatches 0x0056C070 (strict expiry with the 1e-5f slack, id -1 = any object), Pose3d::IsSameAs_WithAmbiguity 0x00846F3C (spherical translation test when the thresholds are equal within 1e-5f; radians >= pi accepts any rotation; the angle from GetAngleDiffFrom 0x0084A694 = acos(2c*c - 1) with c in double, NaN -> -1), the set form DidFailToUse 0x0056BAB4, the stack-on filter's call (0x005E1D24: failure 1, 20.0f, pi/8). Still open: IsSameAs_WithAmbiguity's non-uniform-threshold branch is not built (no caller here reaches it); the stack's one-origin model makes the parent-chain step an identity; the SetFailedToUse 'Removed/Added failure' log lines are not reproduced. Pose3d read 2026-10-13 (re-analysis/research/20261013-pose3d-arithmetic.md): the arithmetic is M11-065, the tree M11-066, unread pieces M11-067. IsSameAs_WithAmbiguity is now read (R38): spherical when the three thresholds are equal, otherwise a box in this pose's rotated frame."
}
```

### M15-025

```json
{
  "id": "M15-025",
  "title": "NeedActionCompleted(0x1F) before the pose search, and the floor callback's lack of any needs call",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorExploreBringCubeToBeacon floor branch: IBehavior::NeedActionCompleted(NeedsActionId 0x1F) BEFORE the pose search, whatever the search returns (0x005DF59C movs r1,#0x1f, 0x005DF5A0; 0x005DF59C..0x005DF5E0)",
    "the floor callback's category 0 path: log 'Successfully placed cube', FireEmotionEvents, no NeedActionCompleted, no SetFailedToUse (0x005E18AC..0x005E191A)",
    "extraction report re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ],
  "unresolved": "built, awaiting strong verification (R-FIX2, 2026-10-04): NeedActionCompleted(PickupCube 0x1F) before the pose search whatever it returns (0x005DF59C..0x005DF5E0); the floor callback makes no needs call. Still open: none known beyond M15-021.\nB-M5-16 fixture correction: the NoFreePoses/needs-order regression enters the checked already-carried Init branch 0x005DF364..0x005DF372; it no longer claims to complete a pickup drive through the unbuilt M12-024 lifecycle. All placement-stage expected ordering/timestamp/no-action assertions remain."
}
```

### M15-026

```json
{
  "id": "M15-026",
  "title": "BehaviorExploreBringCubeToBeacon's pick-up phase: InitInternal, TransitionToPickUpObject, DriveToPickupObjectAction and the completion lambda",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorExploreBringCubeToBeacon::InitInternal 0x005DF348: [this+0x12C] = -1; carrying ([[robot+0x284]+8] != -1) -> [this+0x12C] = vector[0].id (0x005DF364..0x005DF36C) and TransitionToObjectPickedUp directly (0x005DF372), else TransitionToPickUpObject(robot, 1) (0x005DF37C); the result is non-zero when no action started and |now - [beacon+0x10]| >= 1e-5f (0x005DF382..0x005DF3DA)",
    "TransitionToPickUpObject 0x005DF8C0..0x005DFBB0: with [this+0x12C] != -1 the retry path 0x005DF8E6 logs '.TransitionToPickUpObject.Retry'; otherwise the selection (GetCandidate 0x005DFDA4; pose with respect to the robot; f32 (x*x + y*y) + z*z with no sqrt, FLT_MAX 0x005DFCDC, replace when best + (-1e-5f 0xB727C5AC) > d2; best >= FLT_MAX takes the error path 0x005DFA18, an empty vector 0x005DF9C6); the action: 0x100-byte DriveToPickupObjectAction (ctor PLT 0x004A93B8, ObjectID at this+0x128, trigger 0x23F) started through IBehavior::StartActing (0x4B01C8) with the completion lambda 0x005E0FD8..0x005E1220: category 0 'Picked up' -> TransitionToObjectPickedUp; category 4 with the target carried -> RetryOk; category 4 attempt <= 2 (signed, 0x005E118E) -> RetryMaybe 'tries out of 3' -> TransitionToPickUpObject(attempt + 1) (0x005E1210); category 4 spent -> Fail; category 3 -> NoRetry; failures -> SetFailedToUse(obj, PickUpObject) 2-argument form (0x005E1166..0x005E1186); categories 1 and 2 do nothing",
    "re-analysis/research/20261003-R-FIX2-M15-gap1-extraction.md"
  ],
  "unresolved": "R-FIX2 (2026-10-04): new record for the pick-up phase the floor-placement batch rebuilt (InitInternal, TransitionToPickUpObject's selection and retry path, the completion lambda: built from the extraction and the verifier's reads). Not built: DriveToPickupObjectAction's own body (the IDriveToInteractWithObject extras: the face turn, the name animation, trigger 0x23F): the stack runs one attempt of its own drive-to-pre-action-pose plus PickupObjectAction (DockHelper{AttemptLimit = 1, SearchOnFailure = false}), reported MISSING; GetCandidate's reuse of the IsRunnable vector."
}
```

### M8-014

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
  ],
  "unresolved": "Opus verification of R-BEH2 batch 3 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-3.md): NOT YET. AIWhiteboardHandlers is constructed nowhere in production; its handlers use MISSING seams (GetPose 0x004EA398, 0x004DF628, FindLocatedClosestMatchingTypeHelper, VizManager); the +0x48 timestamp is double (AIWhiteboardHandlers.cs:171); AIBeacon lacks the +0x10 float. The constants match their bits. Before: built, awaiting strong verification: AIWhiteboardHandlers (new file; AIWhiteboard.cs is R-VIS's): tag 68 (squared-distance 2500.0 removal), tag 69 (tilt through Radians operator< against 0x3E32B8C2 using the row-2 component with the largest |value| then acosf as GetRotatedParentAxis<'Z'> 0x5507a0..0x550854 does, robot-relative Z <= 30.0, removal, FindLocatedClosestMatchingType with 50.0/pi and a zero-initialised BlockWorldFilter, the ten-entry cap), tag 53 (stores (float)seconds at +0x48) and UpdateBeaconRender (three circles at z + 35.0, radii R, R-0.5, R-1.0, DARKGREEN/ORANGE by abs(+0x10) < 1e-5). Still open: the whiteboard handlers and the renderer are not attached anywhere in production; the engine's Robot::GetPose() (0x004EA398, ConsiderNewPossibleObject 0x0056c54c), the beacon-pose helper 0x004DF628, BlockWorld::FindLocatedClosestMatchingTypeHelper's body, the beacon's float last-failure time (AIBeacon has an int count) and the VizManager sink (M11/M12) are seams that report MISSING once and skip; the failed-relative-pose log text; R-VIS: hold the possible-object list and the float +0x48 timestamp on the whiteboard, give AIBeacon the +0x10 float, make Init's subscriptions deliver the three handlers; the manifest location still points at AIWhiteboard.cs. UpdatePossibleObjectRender 0x0056a6d4 and RemovePossibleObjectsMatching 0x0056ad68 are unread.\nB-M11-8 verifier round 2 (2026-10-14): AIWhiteboard.ConsiderNewPossibleObject: the native log at 0x0056c54a..0x0056c55c is unread, so no engine entry is emitted; the gap goes to SteppedBehavior.ReportMissing.\nB-M11-8 verifier round 3 (2026-10-14): the round-2 note above is withdrawn; the log is read (research/20261002-R-BEH2-M8-gap1-extraction.md row 6.2): a failed GetWithRespectTo(robot) (0x0056c55c beq.w 0x56c698) emits sWarningF with an empty data vector, key \"AIWhiteboard.PossibleObject.NoRelPose\" (0x0056c77c), text \"Couldnt get pose WRT to robot\" (0x0056c7a4), no varargs (0x0056c6a6). AIWhiteboardHandlers emits exactly that Warning (channel null) through its source sink; no MISSING."
}
```

### M11-069

```json
{
  "id": "M11-069",
  "title": "BlockWorld::UpdateObjectOrigin: bucket lookup, same-root gate, FindRoot re-key with overwrite, AddInExistingPose, cascaded erase; and Robot::Delocalize's full sequence (old-origin capture, pose resets, logs, carried set, OnRobotDelocalized tail broadcast)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-delocalize-and-update-object-origin.md: U1-U13 (0x0062013C..0x00620420) and D1-D21 (0x00510A24..0x00510DB4), DeleteObjectsFromZombieOrigins 0x00620920..0x00620A04, OnRobotDelocalized tail call BroadcastLocatedObjectStates 0x006249EC."
  ],
  "unresolved": "IMPLEMENTATION_GAP: build (job B-M11-1) after the pose tree (M11-066, job B-POSE-1): the move decision is HasSameRootAs/FindRoot on the tree.\nB-M3M4-19 B2 withdrawn per checked job. Live OnRobotDelocalized visibly names M11-069 and B-M11-1 before retaining the existing incomplete path; no AddNewOrigin/fresh-map implementation or status change. The RobotDelocalized broadcast (D21) has in-engine subscribers: M11-071 (order and BlockConfigurationManager flag), M4-048 (cube lights); the list is M11-072.\nbuilt, awaiting strong verification: B-M11-1 (2026-10-14): D1-D21 and U1-U13 valid-tree sequence built: fresh origin, reset shared poses, SetNewPose/history, keyed logs, old-origin carried set, pointer-root gate, overwrite re-key/confirmer/cascaded erase, zombie keep/delete, fresh map, selection reset and tail broadcast call. Tail payload M11-038 and component bodies remain visibly MISSING; no settlement. See jobs/status/B-M11-1.md for files/lines, binary ranges, tests and every MISSING.\nB-M11-8 (2026-10-14; fixes from re-analysis/research/20261014-b-m11-1-check.md; see jobs/status/B-M11-8.md): item 1: the FaceWorld hook binds no observable body (FaceWorld::OnRobotDelocalized 0x004F55E4..0x004F563A erases viz objects only); faces survive a delocalize. Item 4: D3..D17 run under EngineRobot.PoseTreeGate, shared with ComputeLiftPose and DockingSystem.UpdateCarriedObjectPose/SetObjectAsAttachedToLift (the native runs them on one thread). Item 5: VisionSystem.History reads Robot+0x390 (EngineRobot.PoseHistory) from the current robot on every use; with no robot a detached history is used with a visible MISSING. Item 6: the AI/Behavior/Movement hook MISSINGs, the ctor main/forward pose-name MISSING and the BroadcastLocatedObjectStates MISSING go to SteppedBehavior.ReportMissing, not the engine log. Queued: RobotStateHistory::Clear after the ctor Delocalize (0x00510328/0x00510330); pivot/lift names RobotLiftBase (0x0050FFF8 -> 0x005109CC) and RobotLift (0x0051004A -> 0x005109DC); +0x2C0 reset on both Delocalize callers (0x00512B9C, 0x00512F88); dead ConstructorMapOrigin/OnRobotDelocalizedMapOrigin/DelocalizeOnTreadBoundary removed. Still MISSING: the main (+0x298) and forward (+0x2A4) pose names; the AI/Behavior/Movement OnRobotDelocalized bodies.\nB-M11-8 follow-up: no log callback (LogEntry, stack log, display, MISSING channel, DAS call observer) runs while PoseTreeGate is held; LogCallbackDeferral queues them on the holding thread and runs them in order after release. Entries are built at emission.\nB-M3M4-23 (2026-10-15), built, awaiting strong verification: Delocalize takes FrameGate before its PoseTreeGate section; native origin-rekey and zombie cleanup roots share the same frame gate. Native source notifications/keep predicates run outside _gate, with map snapshots and writes protected. Existing native order retained; no new recipient implementation claimed."
}
```

### M15-084

```json
{
  "id": "M15-084",
  "title": "BehaviorThinkAboutBeacons lifecycle: ctor/LoadConfig (newAreaAnimTrigger, beaconRadius_mm), runnable gates (not off treads, with robot+0x34A or while carrying; runnable iff no beacon), Init (beacon at the robot pose w.r.t. its root with the configured radius, then a plain 60 s trigger animation with no callback, skipped for an empty or unknown trigger), empty Stop",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Factory class 0x35 0x0059D006..0x0059D024; ctor 0x005E5AF8..0x005E5B2C; LoadConfig 0x005E5B6A..0x005E5BFA (newAreaAnimTrigger string +0x11C, beaconRadius_mm f32 +0x128); Hiking_thinkAboutBeacons.json:13-16 (175), sparksThinkAboutBeacons.json:12-15 (75).",
    "Vtable 0x01029A28: +0x28 0x005E5F4C returns 0; base +0x20 0x005BF04C and +0x24 0x0059EC12 return 0; IsRunnableInternal 0x005E5CC2..0x005E5CD8 (GetActiveBeacon == null).",
    "InitInternal: SelectNewBeacon 0x005E5D98 (0x005E5F0C..0x005E5F3C: Robot::GetPose 0x004EA398, GetWithRespectToRoot 0x004DF628, AddBeacon 0x0056C39C with radius); trigger empty or AnimationTriggerFromString == 0x23F -> no action (0x005E5D9C..0x005E5DF4); TriggerAnimationAction(trigger, 1, true, 0, 60.0f, false) 0x005E5DF6..0x005E5E18; StartActing with an empty callback 0x005E5E1C..0x005E5E3C; returns 0.",
    "re-analysis/research/20261029-think-about-beacons-1-read.md"
  ],
  "unresolved": "Being built in THINK-ABOUT-BEACONS-1; not settled. Beacon clearing is outside this record and unbuilt in the stack: AIWhiteboard::OnRobotDelocalized 0x0056A686 (reached from Robot::Delocalize) and ActivityGatherCubes::OnDeselectedInternal 0x005AF830 clear the beacons, but the stack's AIWhiteboard.OnRobotDelocalized has no caller, so after the first beacon TAB never becomes runnable again in production. Owned elsewhere: AddBeacon and the whiteboard (M13-006, M8-014, M15-011), the base gates (M8-001), the animation action (M8-020, M8-021), selection (M8-003, M15-008)."
}
```
