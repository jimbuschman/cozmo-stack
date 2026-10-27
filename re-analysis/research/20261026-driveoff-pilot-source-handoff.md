# Drive-off pilot SOURCE handoff ledger — 2026-10-26

Source inventory only. Actual model/reasoning telemetry unavailable. No new tracing, implementation, tests, hardware, manifest/status/approval changes or git operations in this handoff task. Source-recovered joins are not code verification. The whole pilot is **not finished** and no record is declared settled here.

Authoritative packet: [full read](20261026-driveoff-pilot-read.md), [independent whole-read check and refinements](20261026-driveoff-pilot-read-codex-check.md), [current build contract](../jobs/trial-m13/DRIVE-OFF-PILOT.md), [proposed owners/policies](../jobs/trial-m13/DRIVE-OFF-PROPOSED-RECORDS.md). Earlier whole-read PASS covered the then-submitted packet; subsequent wire/input/chooser/audio gaps were explicitly reopened. Later per-unit checks do not erase the STOPs below. Builder execution/diff verification and final tests remain pending.

## Actual bounded graph and source disposition

| Actual join / observables | Newly recovered or clarified in this pilot | Previously checked source reused / exact remaining boundary |
|---|---|---|
| Resource/config→actual Robot-owned behavior/activity→common eligibility/chooser (1–4) | Same Robot+JSON factory DriveOff0059CABC/ctor005C0980, shipped extra60/45 versus missing-key+0; actual common defaults/inputs; BehaviorsOnly Update vslot20 is native no-op; Activity.OnSelected005B3154 unconditionally updates actual activity IDs6/13 (Feeding2), trigger-absent configs add no drive/idle push. | Q16/Q17 config loader, strict enums, RB/ES/GP/CS and manager lifecycle reused. Explicit host activity admission is existing app input, not auto-start authorization. Whole chooser closure is not proved by DriveOff score1000. |
| No-interlude chooser while DriveOff still active after platformfalse (1,2,4) | C6 proves getter005B387C caches interlude, not ordinary winner; chooser reevaluates each tick. Six concrete gates/base-score dispatch read; positive losing candidates consume shared RNG0060A49E..4BC. C7 fully checks typed NONRUNNING PutDownDispatch Look factory0059CD44→005EF804, config write-set, flags0/0/0, constant gate005EFD54, base scores, retained repetition/history; B independently PASS. | Canonical bounded PutDown losing candidate can be built from this inventory. Actual stored common state and raw inputs determine draw count. Selecting/starting Look next remains explicit MISSING, not fake success/score0. General MeetCozmo selector/requests and arbitrary prior-running competitors are not certified by fresh platformtrue rejection. |
| DriveOff→StartActing/ActionList→plain DriveStraight→custom path→robot wire (1,2,4) | Direct source path uses plain DriveStraight, not DriveToPose/lattice. Main-yaw versus ForwardPose XY, source f32 endpoint, constructor100/-80/accel200/decel500, AppendLine/failure, actual raw carrying owner/SetPose literal+0 versus-20, action admission/completion/reentrancy/timing were closed in submitted rows. | Existing path runner, wire, reaction locks/driving animation lifecycle reused only on actual calls. Earlier CM13 planner/lattice recoveries are not direct motor prerequisites. No derived DriveOffChargerContacts retry claim follows from plain straight recovery. |
| Driving animation selection→retry/current name→completion message (1–4) | Actual Mood+130 cached clock/simple mood input, shared RNG owner; retained clip+90 may play on retry while union reports CURRENT selected action-name+78. Plain action union tag6, animation variant1/String8, raw lengths/source serializers checked. | Existing animation/group/streamer/action source reused; current implementation binding/diff still requires review. No claim that all M5/M7 behaviors are settled. |
| Mood/engine clocks/whiteboard (1–4) | Actual owner creation and periodic Robot.Update, initialized resource service versus ResourcesPath missingroot; defined missing/nullJSON model retains default decay graphs but empty event map, watcher tail continues; missing DriveOff event logs and no affect. Frame-captured clock, cached mood130 and robot whiteboard timestamp bindings read. | Previously source-checked Mood arithmetic/history/graphs and needs reused. Generic unsupported event/add-affector faults stay smallest-unit M1-034, never a fabricated successful model. “Previously checked” is not subsystem acceptance. |
| Completion→objective4 API then local recipients→mood (3,4→1,2) | Actual8B/objective4/DAS-name/serializer and delivery route; ActivitySparked filter excludes4 in shipped configuration. Socialize owns a different constructed subscriber; phase94 uninitialized preselection is exact M1-034 skip/log unit; actual selection initializes phase0, defined initialized/deselected/reselected objective4 invariant has no mutation. | Common completion/objective owner filtering reused. New Socialize owning record still requires manager allocation; generic Activity absence/global fake observer/guessed phase0 are not valid ownership. This does not recover other objectives or entire specialized activities. |
| Sensor/contact→charger/platform→world/map/config→public state/audio clients (1–4) | Positive actual ingress bindings, actual rawcarrying owner instead of captured publication facade; source float/pose endpoint and field alias boundaries; normal footprint CV assertion reaches defined catch-all rectangle fallback (EH data checked). | Existing contact/platform/world/map source reused. Config producer is CONDITIONALLY required through contact-triggered world change→frame-tail→tallest/public state→ordered A/B audio world-event outputs, not global tick association and not direct motor prerequisite. Other global background behaviors are not implicitly included. |
| Three native configuration containers→membership/count/tallest (3,4→1) | CM13-05 common producer/instruction inventory:00616F00→0061682C, admission/root/processedID/order/equality/candidate/prune0062C72E; actual constructor-empty vectors/lifetime; FIRST stack neighbor/root flatten; signedMAX first geometry operand versus outputIDs MIN/MAX; rootdim scalar+.5 center helper; DOWN omits UP carry comparison. | Earlier verified geometry/query/root-cache/gate/notify packets reused. Missing input/geometry is UNKNOWN, never sourcefalse/emptycache. Immutable GC snapshots may preserve proven observable data without native retain-count mechanics, but stable ordered values/readiness still mandatory. M12-049 BuildPyramid callbacks remain outside this slice/unclosed. |
| Public config adapter versus native decisions (3,4) | Operator D1, recorded current build contract: WHOLE public Cache/Stacks/Bases/Pyramids/MID/FirstSeenSec/ConfigurationSeen retained as documented stack extension, isolated from ALL native engine/behavior decisions. Actual OnConfigSeen count-growth/stamp logic005DB2D4..396 and constructor state are source-checked. | D1 supersedes stale “policy pending” paragraphs in early main read; no current policy hold here. Preserve public semantics independently, fan-out notifications, native-before-host frame effects, canonical native publisher; unknown native rebuild preserves prior native state/flags/root cache and retries. Proposed compatibility owner has NO official ID yet. Do not claim native per-arrangement firstseen/event or cached MID. |
| Vision worker/mailbox/drain/phase gate/processed image (1,3,4) | Exact per-drain OR failure aggregate/reset, mode8 raw sampled imageMeanU8 C8 (not bool), actual mask/header and ProcessedImage43 serialization timestamp/count/U32modes/rawmean; mode7 nonzero producerResult skips deque, receiver all-normal returns0. | Existing scheduler/mailbox/worker lifetime reused. Unknown quality producer input is not native failure; known handler results do not justify permanentfalse/LastError gate. Other actual enabled handlers/recipients remain their own exact production obligations. |
| Quality result→camera/debounce/error API (1,3,4) | B extraction [quality packet](20261026-driveoff-image-quality-read.md), A IQ-A independent PASS: complete370-byte00655578 handler; F4=1/F5=0/F8=FC=0, auto329=1; globals compiled3000/5000 versus shipped3000/300000; physical/nonzeroquality guard; rawF5 suppression/repeat; LIVE postcallback field reloads; EngineError7C+rawbyte; camera57→pending→CurrentCameraParams2F order. | Existing M3 validation/serialization/pending apply and OV14/16 config reused. PASS is receiver/caller contract only, not actual histogram/ROI/detector input closure. |
| Audio requests→actual object1 control state→later consumer (1,2,4) | A1–A7 + independent B PASS: actual native input/mux/ready gates, Music.bnk two events→1901 SetSwitch zero delay/no extra rangeRNG, direct activity switch, per-object scoped store-before-notify, registration IDs1..10, engine-tail ordering. | **STOP:** current canonical bank/controller/render/control/active observer/schedule binding and actual M9 consumer are not certified by a client delegate/dictionary or by fresh/no-active-voice source fact. No phone-only exclusion, no object7 reroute, no control-only filler implementation. See next section. |

## Hard STOPs / boundaries before a whole supported-pilot claim

- **Audio actual owner and active state:** M13-039 requests must reach actual object1 native-equivalent recipient under M6-001/024 readiness/bank ownership and M6-006/022 scheduling/render lifecycle. Scoped switches feed real observer99D3C8→99D088→9994F0 and future switch queryA347A8. Fresh pending0/no active voice does not exclude admitted later MusicState/active schedules. M9-004 live scheduler/voice remains a real supported source-available implementation boundary, not BLOCKED_EXTERNAL or phone-only. A facade/control-only state store cannot certify outputs1–4. Parent explicitly accepted STOP rather than filler.
- **General MeetCozmo:** Interact gate005C23AC mutates target; selector005C23C8 uses retained last-stop image stamp120, actual FaceWorld set/current-origin checks and Whiteboard0056C110 pose/head/name ranking. Current10sec/most-recent shortcut is unsupported. Actual rename/enrollment request names and prior run/stop lifetime also needed. Six-gate read is not a full general activity implementation. No other face/action-body audit was performed.
- **Feeding:** real Update005ABFDE..C062 precedes desired-behavior/universal chooser and has severe/controller/DAS/cube/stage effects (M15-061). Universal DriveOff-first/non-null getter bypass does not make that Update a no-op. Reuse existing checked U rows when this admission is implemented; otherwise retain explicit specialized-entry boundary, not fabricated genericUpdate.
- **Live vision producer/other handlers:** M11-080/081 exact detection rectangle collection, ROI/clipping and true available producer inputs remain separate. No emptyROI/defaultGood, fake native success, or fabricated native failure. M11-035 other enabled mode handlers/complete current recipient joins cannot be inferred from quality receiver/aggregate/ProcessedImage closure. Existing checked source may be reused when its actual inputs/lifetimes are bound; no whole camera/vision settlement is claimed.
- **Build verification:** actual source-backed production reach, state ownership, wire/event ordering, shared RNG interleaving, staging/retry/snapshot behavior, new tests and independent diff verification remain pending. Source PASS and hardware appearance cannot substitute for these.

## Record-wide candidates versus exact residuals

| Record | Safe handoff conclusion | Residual / forbidden upgrade |
|---|---|---|
| M12-049 | Common configuration container producer/cache/geometry/prune/constructor slice genuinely recovered; existing source reused for actual count/tallest/audio chain. | Title also owns BuildPyramid helper callbacks at01029068/010290A8,005DC16C..17C/005DC298. Those callbacks are not recovered merely by common producer. Keep record-wide RECOVERABLE_GAP until each owned portion is checked/built or separately scoped by manager; no batch status shortcut. |
| M13-013 | Pilot recovered plain DriveStraight defaults/geometry/runner inputs that its historical unresolved also mentioned. | Its title is DERIVED DriveOffChargerContacts retry action00558228..58344; derived contact capture/result/retry/lock cleanup is not the plain DriveOff behavior path. No full record settlement from this pilot. |
| M7-026 | Its specifically named GetSimpleMood0067C084 + cached Mood+130 reads0058A95C/writer0067B67E source inputs are now checked and live attachment is in build scope; possible record-wide candidate after actual code/diff verification. | No subsystem/Mood whole acceptance; Init/resource/update clock/consumer bindings must actually pass. Record still current IMPLEMENTATION_GAP; source recovery is not status upgrade. |
| M8-027 | CURRENT unresolved explicitly says settled in substance: every held release/remove memory-only, no log/message/invocation, nothing behavioral left to build; obsolete captured-finalization diagnostic can be removed once correct host unlink/lifetime confirmed. | Do not reopen allocator/retain-count census or invent a behavioral job. Host detachment/snapshot/lifetime safety still required; record not changed here. |
| M15-059 + proposed Socialize owner | Sparked objective4 filter is a bounded no-op; Socialize is separately source-settled shipped4/no-mutation + preselection M1-034 unit. | Do not put Socialize into Sparked's record or assign an official new ID. Proposed owner in DRIVE-OFF-PROPOSED-RECORDS.md needs manager/Claude allocation/check. |
| Proposed D1 configuration-extension owner | Existing operator2026-10-26 decision covers WHOLE public adapter; preserve semantics, isolate from native/behavior decisions. | No official ID/status allocated. Native semantic evidence cannot be used to label host per-arrangement firstseen/MID/event engine-exact. |
| Proposed typed Look candidate owner | C7 LC1–9 independently PASS for NONRUNNING PutDown losing candidate / true common state / RNG. | No selected Init/Resume/actionbody or general Meet selector claim. Proposed title only, no official ID. |

## CURRENT manifest literal register

The following register was reread from the current manifest for this handoff; it collects the already-scoped path, conditional/downstream dependencies and explicit entry/STOP boundaries above, not a new subsystem audit. Each entry quotes the **entire first evidence item verbatim** plus literal title/status; full evidence/unresolved remain in the manifest and full-read quotations. A title's presence is not a claim its whole production path is in this pilot. Read the scope/residual tables first. No statuses are changed.

```json
{
  "id": "M13-017",
  "title": "BehaviorDriveOffCharger: runnable on the charger, the drive distance and the leaving-the-contacts update",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ctor 0x005C0980 stores 96.0 + json extraDistanceToDrive_mm at +0x11C (0x005C09D4/0x005C09E2/0x005C09E6); IsRunnableInternal 0x005C0B10 returns robot+0x34A (the on-charger-platform flag; contacts are +0x338)"
  ]
}
```

```json
{
  "id": "M13-040",
  "title": "Contact-true charger observation: live ingress, located identity, Known pose and ordered world/contact effects",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "RobotState sync gate 0x0051293C..942, contact bit12 0x00512AAC/AAE and SetOnCharger call 0x00512AB4; SetOnCharger 0x00511990..0x00511C2D observes on every true input before old-contact gate 0x00511BA8/BB0. Contacts +0x338; platform +0x34A."
  ]
}
```

```json
{
  "id": "M12-002",
  "title": "Path segment, ExecutePath and ClearPath packings; the path-motion-profile defaults and their wiring",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PathDolerOuter::Dole 0x00507E4C: 0x00507F40/0x00507F44/0x00507F48 cmp r1,#1/2/3; line 0x00507FE0 blx 0x4A7234; arc 0x00508036 blx 0x4A7240; point turn 0x00507F8E blx 0x4A7228"
  ]
}
```

```json
{
  "id": "M12-032",
  "title": "PathComponent drive-to-pose status machine: fields, SetDriveToPoseStatus, predicates, StartDrivingToPose, Update/UpdatePlanning/HandlePlanComplete, ExecutePath, the 0xC6 PathFollowingEvent handler, the RobotState segment and doling",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0064941C, 0x0064937C, 0x00649390, 0x0064B370, 0x0064B61C, 0x006493B4, 0x006494CC"
  ]
}
```

```json
{
  "id": "M12-024",
  "title": "DrivingAnimationHandler PlayStartAnim / PlayEndAnim",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0055AC08 PlayStartAnim call; 0x0055B0AA..0x0055B0CC PlayEndAnim call and its return handling"
  ]
}
```

```json
{
  "id": "M8-001",
  "title": "The IBehavior lifecycle: constructor fields, Init, Update, Stop, StopActing, Resume, IsRunnable and IsRunnableScored",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::IBehavior 0x005bbc38 stores behaviourID at +0x3c, behaviourClass at +0x64, needsActionID at +0x68; zeroes +0x24/+0x28, the flat score at +0x100 and +0x104 (strd r8,r8,[r4,#0x100] 0x005bbd28); sets +0x108 = 0, +0x10c = 0x29 (invalid behaviour objective), and +0x110 = +0x111 = 1 (movw r0,#0x101 0x005bbd30, strh.w r0,[r4,#0x110] 0x005bbd34); constructs the repetition graph at +0xe8 and the running-penalty graph at +0xf4 (GraphEvaluator2d::GraphEvaluator2d 0x005bbd12/0x005bbd22); calls ReadFromJson 0x005bbd40 and ScoredConstructor 0x005bbe32."
  ]
}
```

```json
{
  "id": "M8-015",
  "title": "ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0053E6A8..0x0053E6E0; 0x005409E2..0x00540A40; 0x00541146..0x00541238; 0x0053FAB2; Current Cancel then delete; pending direct deletion preserves existing result. Cancelled0x02000000 except NotStarted0x02000001; stop before unlock, watcher then broadcast. Existing B-ACTIONS candidate must be checked for full teardown effects and concrete descendants."
  ]
}
```

```json
{
  "id": "M8-007",
  "title": "An action's track mask is a lock, taken while it runs",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Every action carries a track mask at +0x54. IActionRunner::Update 0x00540370 reads it and asks MovementComponent::AreAnyTracksLocked(mask) at 0x00540440: while any of those tracks is held it warns \"Action %s [%d] not running because required tracks are locked\" (0x005404A8) and does not run the action - it stays queued and is tried again on the next tick. Otherwise it locks them through the helper at 0x004F0F4C, which calls MovementComponent::LockTracks(mask, tag, name), and then runs the action."
  ]
}
```

```json
{
  "id": "M8-020",
  "title": "PlayAnimationAction: construction, Init, robot/game animation handlers, CheckIfDone, animationCompleted, destructor streamer stop",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m8-play-animation-actions.md rows C1, C8..C11, P1..P12, D1..D3, L2..L8: ctor 0x00543C08..0x00543CBE (+0x84 loops, +0x8B 0, +0x8C interrupt, +0x90 null, +0x94 timeout; 60.0f -> FLT_MAX only when loops == 0), Init 0x00543F20 (cozmo_sings_custom remap 0x0054417C/0x00544190, lookup robot[0]->+0x20->+0x48, SetStreamingAnimation(robot+0x60, anim, loops, interrupt, flag) -> +0x8B, tag 0 -> failed, 0x03000001; R2E 0xCA/0xCB/0xD5 via 0x00519F8C and E2G 0x5F via external-interface vtable +0x28; callbacks 0x005452F1, 0x0054541D, 0x0054561D, 0x005457F5 and AnimationEvent forward 0x00545750), CheckIfDone 0x005441C0, GetCompletionUnion 0x005441DE (0x0075C544, 0x006C22DC), destructor 0x00543DAC (StillStreaming info 0x00543DF2 on robot+0x98 == +0x90; SetStreamingAnimation(null, 1, true, false) at 0x00543E26; releases +0xB4, +0xAC, +0xA4, +0x9C), vtable 0x01021584 slots +0x1C/+0x20/+0x2C (0x00545210)."
  ]
}
```

```json
{
  "id": "M8-021",
  "title": "TriggerAnimationAction: trigger to group, Init NO_ANIM_NAME paths, the group choice hand-off, the DAS blacklist gate and the robot.play_animation event",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m8-play-animation-actions.md rows T1..T12, D4, L11..L13: ctor 0x00544228, SetAnimGroupFromTrigger 0x0054432C (Has == 1 before Get; empty-group warning TriggerAnimationAction.EmptyAnimGroupNameForTrigger \"Event: %s\" at 0x005443AC), Init 0x0054443C (NO_ANIM_NAME 0x0300000C; GetAnimationNameFromGroup with the robot and strictCooldown; blacklist set walk +0x8C/+0x90; sEvent robot.play_animation, value +0x78, $data trigger + \":\" + group at 0x005445BC), vtable 0x010215BC."
  ]
}
```

```json
{
  "id": "M7-014",
  "title": "Reaction-lock manager lifetime and concrete per-class lock tables",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::SmartDisableReactionsWithLock 0x005bce3c"
  ]
}
```

```json
{
  "id": "M7-012",
  "title": "Mood schema, decay graphs, action-result events, affectors, update and game output",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/obb/assets/cozmo_resources/config/engine/mood_config.json"
  ]
}
```

```json
{
  "id": "M7-013",
  "title": "Mood clamp, graph evaluation and decay-clock restart arithmetic",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Emotion::Add 0x00679618"
  ]
}
```

```json
{
  "id": "M7-020",
  "title": "Mood event production, repetition penalty and affector application",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "MoodManager::TriggerEmotionEvent 0x0067b85c"
  ]
}
```

```json
{
  "id": "M7-023",
  "title": "Mood destruction with already-null ActionList dependency",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0067AE14..0x0067AE74; 0x00511150; 0x005111B6; Callback-id/Robot/ActionList gates precede unregister; normal Robot teardown already nulled ActionList, so skips unregister. Remaining destructor children UNKNOWN."
  ]
}
```

```json
{
  "id": "M7-026",
  "title": "MoodManager::GetSimpleMood and the mood time MoodManager+0x130 (inputs to the animation group draw)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261012-m5-x9-extraction.md section 4f: GetSimpleMood 0x0067C084 (Default; Happy > 0.33f -> Happy; Confident < -0.29f -> Sad; Happy < -0.33f -> Sad; constants 0x0067C0C0..0x0067C0C8); the group draw reads MoodManager+0x130 (0x0058A95C), written only by MoodManager::Update(t) 0x0067B67E and Reset/ctor."
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
  "id": "M15-072",
  "title": "SevereNeedsComponent live selected need (AI+0x30, +0x14): Set/Clear side effects and order, AI-phase Update clear, and its native callers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261021-m15-severe-need-input.md (row 39), checked by re-analysis/research/20261025-r-m15-severe-input-claude-check.md."
  ]
}
```

```json
{
  "id": "M15-073",
  "title": "App input: NotifyCozmoWakeup (G2E 166) to the SevereNeedsComponent wake handler",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261021-m15-severe-need-input.md (row 39), checked by re-analysis/research/20261025-r-m15-severe-input-claude-check.md."
  ]
}
```

```json
{
  "id": "M5-063",
  "title": "DrivingAnimationHandler bodies: HandleActionCompleted (E2G 0x5A), PushDrivingAnimations (G2E 0xB5), RemoveDrivingAnimations (G2E 0xB6)",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/20261012-m5-teardown-members.md: callbacks 0x004F110E -> HandleActionCompleted 0x004F0D78 (engine sender: ActionQueue 0x0053FAAA); 0x004F1186 -> PushDrivingAnimations 0x004F0944; 0x004F120E -> RemoveDrivingAnimations 0x004F0A94; registered only if HasExternalInterface() == 1 (0x004F072C..0x004F0732)."
  ]
}
```

```json
{
  "id": "M4-019",
  "title": "CliffSensorComponent: defaults, CliffEvent/PotentialCliff, the EnableStopOnCliff senders, the 50/400/150 schedule with its running stats, HandleRobotStopped's Evaluate and broadcast gate",
  "status": "EXACT_SOURCE",
  "evidence": [
    "SC1..SC8, SC4a..SC4j, SC6, C2, C7, C8 as before"
  ]
}
```

```json
{
  "id": "M10-005",
  "title": "The off-treads debounce clock is BaseStationTimer ms (engine tick-start time since run start, per tick)",
  "status": "EXACT_SOURCE",
  "evidence": [
    "A2 now = GetCurrentTimeStamp 0x511E2A..0x511E36, 0x84BCC0..0x84BCD0"
  ]
}
```

```json
{
  "id": "M11-037",
  "title": "The BlockWorld frame sequence for observed markers",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BlockWorld::UpdateObservedMarkers 0x00624EE8: non-empty path is ClearOccluders 0x00624F98, AddLiftOccluder 0x00624FA4, CreateObjectsFromMarkers 0x00624FC4, AddAndUpdateObjects 0x0062505A, then (on its success) CheckForUnobservedObjects 0x006250CA, UpdatePoseOfStackedObjects 0x006250D0, BlockConfigurationManager::Update 0x0062520C, UpdateMarkerlessObjects 0x0062521A; the empty-list branch 0x625020 does ClearOccluders, AddLiftOccluder, CheckForUnobservedObjects 0x0062504E and skips the stacked-pose update. AddLiftOccluder body 0x6564D8 (raw state at the timestamp, Robot::GetLiftTransformWrtCamera, Transform3d::ApplyTo, Camera::Project3dPoints, Camera::AddOccluder with the transform z scale); BlockConfigurationManager::Update body 0x616D7C (DidAnyObjectsMovePastThreshold gate, UpdateAllBlockConfigs, PruneFullPyramids, UpdateLastConfigCheckBlockPoses, NotifyBroadcasterOfConfigurationManagerUpdate). UpdatePoseOfStackedObjects 0x621794 and UpdateMarkerlessObjects 0x625704 are cross-layer entry points (M10/M12)"
  ]
}
```

```json
{
  "id": "M12-049",
  "title": "BehaviorBuildPyramid helper callbacks and the pyramid cache contents (Container::Update, CastNewEntryToBestSharedPointer, PruneFullPyramids, PyramidBase::operator==)",
  "status": "RECOVERABLE_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261016-ov69-74-75-77-check.md: callbacks vtables 0x01029068 / 0x010290A8 (built 0x005DC16C..0x005DC17C, passed at 0x005DC298); cache UpdateAllBlockConfigs 0x00616F00 -> Container::Update 0x0061682C -> AddAllConfigsWithObjectToCache 0x0062C56E -> CastNewEntryToBestSharedPointer 0x0062C68C; PruneFullPyramids 0x0062C72E."
  ]
}
```

```json
{
  "id": "M13-038",
  "title": "BehaviorAudioClient instances and lifetime: two clients (BehaviorManager, BehaviorSystemManager) subscribe to the public-state broadcaster; whether both stay subscribed; the re-entrant double ActivateSparkedMusic",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov48-49-check.md: Subscribe 0x00591F08 in the ctor; constructed by BehaviorManager 0x005A0914 and BehaviorSystemManager 0x005A5828, both built in Robot::Robot (0x0050FCAC, 0x0050FCBE); emitter 0x0064DE5E..0x0064DE7A -> 0x0064E656..0x0064E668 (synchronous, skips empty slots); re-entrance via the unlock 0x1D path and UpdateBroadcastBehaviorStage 0x0064DA44..0x0064DB86 (broadcaster state+0 copied at 0x0064DB44..0x0064DB58)."
  ]
}
```

```json
{
  "id": "M13-039",
  "title": "BehaviorAudioClient::HandleRobotPublicStateChange and helpers: world events, round, spark, needs, dim, activity music, GuardDog and Feeding stages, unlock cache; SetSparkedMusicState input",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov48-49-check.md: handler 0x005921EE..0x00592258; helpers 0x0059225C..0x00592720, 0x00591FC4..0x00592190 (round table 0x00C5B65C, activity map 0x00C5B5F0, eighties 0x00573B70..0x00573B9A, 0x00573E30..0x00573E7E); handler lambda 0x01023D98 -> 0x00592B1E -> 0x004AF07C; SetSparkedMusicState (G2E 0xA3) lambda 0x00592A9E..0x00592ABA, client+0xC initial 0 (0x00591E56)."
  ]
}
```

```json
{
  "id": "M12-043",
  "title": "PyramidBase interior geometry: corners and midpoint for the pyramid top placement (GetBoundingQuad<float> corner order)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov02-m12-028-check.md: corners/midpoint 0x006177F0..0x00617A08, 0x00617A8C..0x00617C32, multimap 0x00618A4C (equal keys keep insertion order, NaN right); unread GetBoundingQuad<float> 0x004E6490..0x004E6598 and Block::GetBoundingQuadXY 0x004E62A2."
  ]
}
```

```json
{
  "id": "M13-023",
  "title": "Footprint corner ordering: SortCornersClockwise (centroid, atan2f, __sort4/sort3 ties, Quadrilateral slots) and the cv::Rectangle fallback (empty input, bounds, center); corner writers read and unreachable/out of scope",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00502DD8, 0x004EF78A the corner getters; 0x0084AAE0, 0x008494E0, 0x008499B0 the rotation-matrix path; 0x004E7EFC SortCornersClockwise; 0x004E6778 InitFromPointContainer"
  ]
}
```

```json
{
  "id": "M13-024",
  "title": "The exact footprint: cv::minAreaRect, convexHull, Sklansky_ and RotatedRect::points",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x004E6490..0x004E6570, 0x004E65C6..0x004E67C4, 0x0009C760..0x0009CEAC, 0x00039174..0x005395E0, 0x0003781A..0x00037968, 0x00086188..0x00086276"
  ]
}
```

```json
{
  "id": "M7-002",
  "title": "The shipped animation/reaction maps, behaviour configs and activity tree",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/obb/assets/cozmo_resources/assets/animationGroupMaps/AnimationTriggerMap.json (573 pairs)"
  ]
}
```

```json
{
  "id": "M13-013",
  "title": "DriveOffChargerContactsAction: a drive straight that retries while still on the charger",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "DriveOffChargerContactsAction ctor 0x00558228: DriveStraightAction(10 mm, 20 mm/s, false) (0x00558232/0x00558236/0x0055823E); then +0x44 = 7 (0x00558276/0x00558278), which is IActionRunner's RobotActionType (stored at +0x44 by the IActionRunner ctor 0x0053FDCC) and 7 = DRIVE_OFF_CHARGER_CONTACTS (RobotActionTypeFromString 0x0075A448 maps the string at 0x00C1604E to 7)"
  ]
}
```

```json
{
  "id": "M15-003",
  "title": "Activity start/end predicates and cooldown lifecycle",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IActivityStrategy constructor 0x005B4EC8"
  ]
}
```

```json
{
  "id": "M15-006",
  "title": "Freeplay activity selection and selected/deselected lifecycle",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ActivityFreeplay::PickNewActivityForSpark 0x005ADC44"
  ]
}
```

```json
{
  "id": "M15-013",
  "title": "Activity tree construction, desired names and discarded activityPriority",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ActivityFreeplay::CreateFromConfig 0x005AD478"
  ]
}
```

```json
{
  "id": "M8-002",
  "title": "The repetition penalty graph and its suppression window",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "config/engine/mood_config.json:88-99 ships defaultRepetitionPenalty with the nodes (0.0, 0.0) and (30.0, 1.0)."
  ]
}
```

```json
{
  "id": "M8-003",
  "title": "Scored selection: emotion scorers or the flat score, times the repetition and running penalties",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::ReadFromScoredJson 0x005bc488 reads five keys, not four: \"emotionScorers\" into a MoodScorer at +0xdc, \"flatScore\" into the float at +0x100 (0x005bc4ca/0x005bc4e0), \"repetitionPenalty\" into the graph at +0xe8 (0x005bc4ee), \"considerThisHasRunForBehaviorObjective\" into a behaviour objective at +0x10c through BehaviorObjectiveFromString (call 0x005bc5a0), and \"runningPenalty\" into the graph at +0xf4 (string 0x005bc602; add.w r6,r4,#0xf4 0x005bc5f8; AddNode(0,1,true) 0x005bc678)."
  ]
}
```

```json
{
  "id": "M8-012",
  "title": "BehaviorManager::Update's tick order, behaviour switching, finishing and the reaction gate",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorManager::Update 0x005a2f70 returns immediately when the manager's init byte at +0 is zero, logging \"BehaviorManager.Update.NotInitialized\" (0x005a2f70/0x005a2f72/0x005a2fea)."
  ]
}
```

```json
{
  "id": "M8-013",
  "title": "The behaviour choosers and the activity selection dispatch",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "ScoringBSRunnableChooser::GetDesiredActiveBehavior 0x0060a44a walks its behaviour map, calls EvaluateScore(robot) on each (0x0060a44a), skips scores <= 0 (vcmpe s0,#0 0x0060a452), adds a running-duration graph bonus for a running behaviour (GraphEvaluator2d::EvaluateY 0x0060a474) and a RandomGenerator::RandDbl tie-break for a non-running one (0x0060a4a8), and keeps the maximum. It logs \"behavior '%s' has score of %f, so is interrupting running behavior '%s' which scored %f\" (string 0x0060a60a; sChanneledInfoF 0x0060a61c) and warns \"Looks like more than one behavior returned IsRunning(). One of them is '%s'\" (0x0060a550)."
  ]
}
```

```json
{
  "id": "M8-027",
  "title": "ProtoHandle subscriber-callable census: which callables the 8 ProtoHandle kinds hold at teardown and what destroying them does",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov53-55-check.md: ProtoHandle vtables (8) confirmed; EngineToGame release 0x00664708, unlink 0x006634F4 (nulls the callable, decrements node refcount +0x20, frees at zero 0x0066352E..0x00663560)."
  ]
}
```

```json
{
  "id": "M5-008",
  "title": "One streaming animation: refuse or interrupt, tags 1..0xFE returned, loops with the same tag, live forced to 1 loop, ReplayLastAnimation",
  "status": "EXACT_SOURCE",
  "evidence": [
    "A4 0x0057B17E..0x0057B1B2"
  ]
}
```

```json
{
  "id": "M5-011",
  "title": "Group choice: mood, head window and cooldown filters; RandDbl(sum w) weighted draw; fallback to the Default mood",
  "status": "EXACT_SOURCE",
  "evidence": [
    "D4 0x0058C4C0..0x0058C7BE"
  ]
}
```

```json
{
  "id": "M5-014",
  "title": "Cooldown keyed by clip name across groups; the Default-mood backup rule within +-0.05 rad, else the first entry",
  "status": "EXACT_SOURCE",
  "evidence": [
    "D5 cooldown set on a pick; MoodManager::Update 0x0067B5E6"
  ]
}
```

```json
{
  "id": "M7-003",
  "title": "ReactToImpact FallingStopped, intensity, recalibration and animation state machine",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "BehaviorReactToImpact::InitInternal 0x006061f8"
  ]
}
```

```json
{
  "id": "M7-004",
  "title": "All 30 live-idle tunables and defaults",
  "status": "EXACT_SOURCE",
  "evidence": [
    "SetDefaultParams 0x0057db40..0x0057dcd0; decoded values in M7 inventory Appendix A row 4"
  ]
}
```

```json
{
  "id": "M12-012",
  "title": "What makes a cube a valid bottom to stack on: resting flat within ten degrees",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0063C5C4 CanStackOnTopOfObject; 0x0063C654 CanInteractWithObjectHelper"
  ]
}
```

```json
{
  "id": "M12-033",
  "title": "The object-pose queries the placement path uses: GetZRotatedPointAboveObjectCenter, GetClosestMarkerPose and the Radians comparisons",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x00877574, 0x00877774, 0x0084C87C, 0x0084CC0A, 0x0084CC90, 0x0084AA1C, 0x00557794"
  ]
}
```

```json
{
  "id": "M7-018",
  "title": "Shipped behaviour configuration and BehaviorClass factory binding",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "178-config table in M7 inventory Appendix C"
  ]
}
```

```json
{
  "id": "M7-019",
  "title": "M7 concrete reaction-behaviour state machines",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "reaction-class rows in M7 inventory Appendices E and F"
  ]
}
```

```json
{
  "id": "M5-023",
  "title": "Abort: AnimationAborted broadcast -> AbortAnimation 0x8D reliable; state cleared; buffer kept and flushed by the next no-animation Update unless InitStream drops it; no EndOfAnimation",
  "status": "EXACT_SOURCE",
  "evidence": [
    "A22 0x0057B3F2..0x0057B426"
  ]
}
```

```json
{
  "id": "M2-009",
  "title": "Outbound layouts and sizes of the 42 engine-to-robot messages this stack sends",
  "status": "EXACT_SOURCE",
  "evidence": [
    "the per-message Pack/Size/operator== addresses in re-analysis/inventory/M2-protocol.md Appendix A section 1 (tags 03 05 08 0A 0B 25 32 34 35 36 37 39 3B 3C 3D 3E 3F 41 42 43 44 45 48 4A 4B 4C 58 60 64 66 80 81 8E 8F 93 94 97 99 9A 9B 9F A0)"
  ]
}
```

```json
{
  "id": "M5-034",
  "title": "AnimationTrigger to group name from assets/animationGroupMaps Pairs; first entry wins; unknown gives a warning and \"\"",
  "status": "EXACT_SOURCE",
  "evidence": [
    "gap1 C7 0x00670438..0x0067075C; GetResponse 0x00670810..0x00670920"
  ]
}
```

```json
{
  "id": "M11-035",
  "title": "The VisionComponent per-mode result handlers and the frame broadcast",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionComponent::UpdateAllResults 0x006542EC runs markers 0x006544A2, faces 0x00654510, pets 0x0065457E, motion 0x006545E8, overhead edges 0x00654652, tool code 0x006546BC, computed calibration 0x00654726, image quality 0x00654790, laser points 0x006547FA"
  ]
}
```

```json
{
  "id": "M8-009",
  "title": "Behaviour scope undo order: the engine releases in a fixed order (reaction locks, idle, motion profile, track locks, light patterns)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "IBehavior::Stop 0x005bd08c releases the scope in a fixed order regardless of acquisition order: (a) remove every disable-reactions lock while +0xac != 0 (0x005bd12c ldr.w r0,[r4,#0xa4]; 0x005bd136 blx SmartRemoveDisableReactionsLock; 0x005bd13a/0x005bd13e/0x005bd140), (b) remove the idle animation if +0xb0 (0x005bd142 ldrb.w r0,[r4,#0xb0]; 0x005bd14c blx SmartRemoveIdleAnimation), (c) clear the motion profile if +0xc0 (0x005bd150 ldrb.w r0,[r4,#0xc0]; 0x005bd158 blx SmartClearMotionProfile), (d) walk the track-lock map at +0xb4 and MovementComponent::UnlockTracks(mask, name) each, then destroy the map (0x005bd15c/0x005bd16e/0x005bd174 blx 0x4a5794; 0x005bd19e blx __tree::destroy), then clear the custom light-pattern vector at +0xcc/+0xd0 (0x005bd1a2 ldrd r1,r0,[r4,#0xcc] ... 0x005bd1c6 strne.w r0,[r4,#0xd0])."
  ]
}
```

```json
{
  "id": "M8-011",
  "title": "The Smart* scope helpers: idle animation, motion profile, track locks, custom light patterns, helper delegation and the reaction locks",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "SmartPushIdleAnimation 0x005be41c: if the idle-animation flag +0xb0 is already set it fails through sVerifyFailedReturnFalse (0x005be448 b.w 0x8cbe5c); otherwise AnimationStreamer::PushIdleAnimation(trigger, name) and +0xb0 = 1 (0x005be464..0x005be478)."
  ]
}
```

```json
{
  "id": "M12-040",
  "title": "Carrying/docking lifetime after preceding control abort",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x0063BE10..0x0063BE5C; 0x005113FA..0x00511414; Dock Abort precedes carrying/docking raw owner deletes; do not add another destructor/send at the raw-delete sites. Remaining owned state/subscription effects UNKNOWN."
  ]
}
```

```json
{
  "id": "M1-056",
  "title": "CozmoEngine::Update states 1 and 2: HasDesiredNumUiDevices, SendSupportInfo, SetEngineState(2); the per-tick loading poll and EngineLoadingDataStatus; delivery of the Init-time broadcast with no UI device",
  "status": "EXACT_SOURCE",
  "evidence": [
    "re-analysis/research/overnight/20261017-ov124-check.md: switch 0x004ED5C4 (engine+0x62), tbb 0x004ED5CC; state 1: HasDesiredNumUiDevices == 1 at 0x004ED5D8, SendSupportInfo 0x004ED5E2, SetEngineState(2) 0x004ED5EA; state 2: loading case 0x004ED5F0, then always 0x004ED612..0x004ED618 -> 0x004ED7C0 (EngineLoadingDataStatus ctor 0x004ED7DE, E2G slot +0x1C 0x004ED7E6; context+4 dereferenced without a null check 0x004ED7D2); progress out-parameter written every call before the done test 0x00523576..0x0052357E; UiMessageHandler::Broadcast 0x006625B0 calls [vtbl+0x48] with r2 = -1 (0x006625CA..0x006625CE), then local dispatch 0x006625FA."
  ]
}
```

```json
{
  "id": "M11-070",
  "title": "Delocalize callees: AnyRemainingLocalizableObjects(uint), SetNewPose/SetPose/AddVisionOnlyStateToHistory with GetDefaultRobotState, the AddInExistingPose default entry, and VizManager (no output)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-delocalize-and-update-object-origin.md: \"Unread\" section: 0x0051502C, 0x0051513C, 0x00626EF4, PLT 0x004A6FE8, 0x00510D94, GOT 0x0103E7DC."
  ]
}
```

```json
{
  "id": "M11-074",
  "title": "Memory-map producers and readers not yet read: MapComponent::UpdateRobotPose (explored area), UpdateObjectPose, RemoveObservableObject, FlagInterestingEdgesAsUseless, ReviewInterestingEdges, ClearRobotToMarkers, FindContentIf/FindIf",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-quadtree-rest-check.md: UpdateRobotPose 0x0067E224..0x0067E50B; UpdateObjectPose 0x0067EA90..0x0067ECFB; RemoveObservableObject 0x0067F2C4..0x0067F58B; FlagInterestingEdgesAsUseless 0x0067E778..0x0067E85B; ReviewInterestingEdges/ClearRobotToMarkers 0x0067F58C..0x0067F7AB; FindContentIf 0x00681674..0x006816E7 and QuadTreeNode::FindIf 0x006884FC.."
  ]
}
```

```json
{
  "id": "M11-088",
  "title": "ObservableObject virtual +0x5C / +0x50 overrides per concrete class (map Add/Remove and quad)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov42-44-check.md: base slot 0x004E02AE returns 0; ActiveObject-family slot 0x004E3882 returns 1; CustomObject 0x004F04FA returns byte +0x9C; callers 0x0067EB38..0x0067EC2E (+0x5C), 0x0067ED06..0x0067F0BC (+0x50)."
  ]
}
```

```json
{
  "id": "M11-094",
  "title": "Robot::GetBoundingQuadXY(atPose, pad): static robot footprint quad, padding, 2D rotation by the Z angle, translation (no re-root)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261016-ov109-110-answer.md: 0x00514D74..0x00514ECE (static quad guard 0x01059D48, data 0x01059D28: (22.0999985, -27.1), (22.0999985, 27.1), (-55.9, -27.1), (-55.9, 27.1); front 0x41B0CCCC; pad != 0 adds (pad,-pad),(pad,pad),(-pad,-pad),(-pad,pad); x' = x*m0 + y*m1, y' = x*m2 + y*m3; translate 0x004E68CC). Callers: 0x005F40xx (GuardDog), 0x005C48xx (LookAround), Robot::Update 0x00513E0E/0x005142DE, UpdateMarkerlessObjects 0x006257D6, CheckForCollisionWithRobot 0x00625A82, MapComponent 0x0067E41E/0x0067F60A, LatticePlannerImpl 0x004FE0A4."
  ]
}
```

```json
{
  "id": "M11-097",
  "title": "RobotStateHistory query and admission results: ComputeStateAt, GetLastStateWithFrameID and AddRobotStateToHistory's result",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261018-m3m4-settling-b.md: no record owned these; consumers M4-019, M4-038, M10-007, M10-015, M11-044, M13-030, M14-005. Prior reads: ComputeStateAt 0x00531784..0x00531808 (research/20260929-R-VIS-pre-extraction.md); AddRobotStateToHistory 0x00512B9C..0x00512F0C (on-ramp 0x00512BE2, ContainsOriginID 0x00512C44, failure 0x00512C38); the C12.4 gate 0x00513088..0x00513110."
  ]
}
```

```json
{
  "id": "M13-011",
  "title": "The three path segment messages are a Planning::PathSegment copied field for field",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PathDolerOuter::Dole 0x00507E4C switches on the segment type at PathSegment+0 (1 line, 2 arc, 3 point turn; 0x00507F40/0x00507F44/0x00507F48) and copies words at +4, +8, +0xC, +0x10 (arc adds +0x14), then the speed profile at +0x1C, +0x20, +0x24; a point turn also copies the byte at +0x14 (line 0x00507FB6..0x00507FD2; arc 0x00508008..0x00508028; point turn 0x00507F5E..0x00507F7E)"
  ]
}
```

```json
{
  "id": "M13-034",
  "title": "Planner dependencies: DidObjectsChange (+0x54) and its writers, FindClosestPlanSegmentToPose, PlanIsSafe/PathIsSafe/ApplyPathSegment, xythetaPlan::Append, AppendToPath and Path::Append*, ApplyMotionProfile sub-ranges, MinimalAngle compares (the ComputePath F4==0 goal loop unreachable)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/20261013-planner-internals-check.md (and re-analysis/research/20261013-planner-internals.md): \"Unread\" section: 0x00623020, 0x00855D90, 0x00850E34, 0x00850610, 0x0085CE88, 0x0085D3C8, 0x0085D46C, 0x00851870, 0x00856310, 0x004FE7F6..0x004FE9E2, 0x00508C14..0x00508D90, 0x00508F48..0x0050906E, 0x00509070..0x005090DE, 0x00503E7A..0x00503EA2."
  ]
}
```

```json
{
  "id": "M11-090",
  "title": "Vision/world teardown residue: std::function capture finalizers in the eight ProtoHandle remove routines, StdMatAllocator::deallocate, and whether Ramp/Bridge/FlatMat/Platform/HumanHead/Block_* are ever instantiated",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261015-ov50-52-check.md: remove routines vt+0x10/+0x14 (per OV50); core getDefaultAllocator 0x000815C0 -> 0x00081568; Ramp::Ramp only from Ramp::CloneType 0x0050F89A (direct-call scan; vtable/VTT references not scanned)."
  ]
}
```

```json
{
  "id": "M6-006",
  "title": "Control path: queued PostEvent, ExecuteEvent, EnqueueOrExecute (frames + remainder; PlayAndContinue one frame early), drain order, Play/Stop/Seek execute, switch resolution",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "PostEvent 0x009A6704 / core 0x9A0EF8; pump 0x9AE0B0; ExecuteEvent 0x9AA3DC"
  ]
}
```

```json
{
  "id": "M6-023",
  "title": "The app audio-input dispatch: Unity PostAudioEvent -> AudioUnityInput -> AudioMuxInput -> AudioMultiplexer -> AudioEngineController::PostAudioEvent -> Wwise PostEvent",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "Unity: unity/scripts/csharp/Anki.Cozmo.Audio/PlaySound.cs:29,72-82; GameAudioClient.cs:17-45; UnityAudioClient.cs:224-237,279-287; Anki.AudioEngine.Multiplexer/PostAudioEvent.cs:89-107; Anki.Cozmo.ExternalInterface/MessageGameToEngine.cs:14-16"
  ]
}
```

```json
{
  "id": "M15-061",
  "title": "ActivityFeeding lifecycle beyond the cube-connection subscription, SetCubeLights, and CubeAccel RemoveListener 0x006356AC",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261016-batch-check.md: OnSelectedInternal 0x005AADAC (rest), OnDeselected tail after 0x005ABC46 (UpdateBroadcastBehaviorStage(4,0), sEvent with need level), SetCubeLights, RemoveListener 0x006356AC."
  ]
}
```

```json
{
  "id": "M6-001",
  "title": "Bank and HIRC readers in the runtime field order (Event, Action, Sound, RanSeq, Switch, ActorMixer, Bus, Layer, NodeBase, RTPC varint, STMG)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "HIRC dispatch 0x009B338C; Event 0x9CD01C; Action 0xA613B0 / factory 0xA60C1C; Sound 0xA1DA08; RanSeq 0xA0828C; Switch 0xA2F1D0; ActorMixer 0xA669EC; NodeBase 0x9F6EF8"
  ]
}
```

```json
{
  "id": "M9-004",
  "title": "Music switch containers, decision trees, meters and MIDI target",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/inventory/M6-wwise-bank.md"
  ]
}
```

```json
{
  "id": "M6-024",
  "title": "Bank and scene loading call sites: the CozmoAudioController six-bank list and InitScene, LoadAudioScene/LoadSoundbank, AddZipFiles",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "CozmoAudioController ctor 0x00592BB0; InitializeAudioEngine 0x008D1D1E; RegisterAudioScene 0x005935E2; LoadAudioScene 0x005935EA; banks Init.bnk/Music.bnk/UI.bnk/SFX.bnk/Cozmo.bnk/Dev_Debug.bnk and scene InitScene"
  ]
}
```

```json
{
  "id": "M6-022",
  "title": "The live voice and bus engine: the 0xA57FF8 wrapper and 0xA44D4C render body, the voice pass, the bus pass, idle removal, the per-voice DSP chain, the four Perform group members, the PBI flush, the 0x108DAE8 output-device state and its three gate bytes, and the JNI audio-route poll",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "wrapper 0xA57FF8 -> 0xA57D64 + 0xA44D4C; render body 0xA44D4C pre-loop over the output-device list 0x108DB04 (0xA44D5C..0xA44DA0); throttle 0x108DA9C/0x108D870+0x4c (0xA44DAC..0xA44DD4)"
  ]
}
```

```json
{
  "id": "M14-009",
  "title": "Face return eligibility and enrollment forwarding",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "0x004F55B8..0x004F55E2 rejects a stale entry, optionally rejects id<1, and requires Robot::IsPoseInWorldOrigin on the face pose."
  ]
}
```

```json
{
  "id": "M11-080",
  "title": "CheckImageQuality: ROI weighting and MeterFromDetections, the proposal stage with NaN/boundary rules, the quality 1/2/3 decisions, the ImageQuality statics from vision_config, and Update's mode-7 block",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov14-m11-057-check.md: ROI head 0x006B2D4C..0x006B2F58; proposal 0x006B2F5A..0x006B30AC; statics 0x01051088..0x01051094 loaded at 0x006B07D8..0x006B0E3E (SetAutoExposureParams call 0x006B0A14); Update mode-7 block 0x006B55E2..0x006B5690."
  ]
}
```

```json
{
  "id": "M3-021",
  "title": "Camera exposure/gain parameters: the VisionSystem ctor limits, HandleDefaultCameraParams, SetCameraSettings with IsExposureValid/IsGainValid and the SetCameraParams send, SetNextCameraParams (queue and warning), SetCameraExposureParams",
  "status": "EXACT_SOURCE",
  "evidence": [
    "1a VisionSystem ctor: max 66, min 1, minGain 0.1, maxGain 4.0, cur 16, gain 2.0 (0x006B002A..0x006B004A); 1b Init changes none (0x006B0658)"
  ]
}
```

```json
{
  "id": "M11-033",
  "title": "The image hand-off and the two VisionSystem::Update overloads",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionComponent::SetNextImage 0x00652B04; Processor 0x00651F08; UpdateVisionSystem(pose, image) 0x00653D30"
  ]
}
```

```json
{
  "id": "M11-034",
  "title": "The vision-mode schedule mechanism",
  "status": "EXACT_SOURCE",
  "evidence": [
    "ShouldProcessVisionMode 0x006B5AA4: (1<<mode) & [VisionSystem+0xac], then the front schedule's CheckTimeToProcessAndAdvance 0x006AF1F8"
  ]
}
```

```json
{
  "id": "M11-059",
  "title": "VisionComponent::Init and VisionSystem::Init: the config reads, IsInitialized and the face-album load-or-erase branch",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionComponent::Init 0x00650D20: ImageQuality.InitialExposureTime_ms into .data 0x01051054 (0x00650DAE..0x00650DCA; rows 4a..4l; vision_config.json:46 = 16); VisionSystem::Init (0x006B0658) called at 0x00650E36 (blx 0x4BA200), then cmp r6,#0; beq.w 0x650FB4 (0x00650E3C..0x00650E3E); non-zero: sErrorF 0x00650E58, the debug-break gate 0x00650E90..0x00650E9A, return 0x00650FB0"
  ]
}
```

```json
{
  "id": "M11-060",
  "title": "VisionSystem::Update's application of the pending SetNextCameraParams to the current exposure and gain",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "VisionSystem::SetNextCameraParams 0x006B229C queues the params (M3-021 1d); the consumer in VisionSystem::Update (read from 0x006B4D5C) is UNKNOWN"
  ]
}
```

```json
{
  "id": "M11-081",
  "title": "ImagingPipeline exposure adjustment: ComputeExposureAdjustment (image, image+mask, vector), ImageBrightnessHistogram FillFromImage and ComputePercentile, SetExposureParameters, VisionSystem::SetAutoExposureParams",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "re-analysis/research/overnight/20261014-ov14-m11-057-check.md: ComputeExposureAdjustment 0x0085F748.., 0x0085FA02.., 0x0085FAE4..; FillFromImage 0x008731FC, 0x008733EC; ComputePercentile 0x00873624..0x00873774; SetExposureParameters 0x0085F374; SetAutoExposureParams 0x006B1858."
  ]
}
```

Current `M12-049` full `unresolved` (literal, for residual decisions):

```json
"RECOVERABLE_GAP: read the two callbacks' operator() slots and the cache bodies (check M11-037/M12-042 ownership first). B-M12-6 boundary: BlockConfigurationManager.CopyNativePyramidBaseCache copies an explicitly supplied native strong-cache vector (manager+48), with visible ReportMissing when its native producer is absent; no copy from host all-pairs snapshot. BuildPyramid checked loop installs explicit ReportMissing callback recipients at vtables0x01029068/0x010290A8; no success/failure state transition is guessed. This record remains RECOVERABLE_GAP and those bodies/cache semantics are unbuilt."
```

Current `M13-013` full `unresolved` (literal, for residual decisions):

```json
"built, awaiting strong verification (R-FIX, 2026-10-03): DriveOffChargerContactsAction as native ctor/init/check on the tick model with the SDK-only SetTracksToLock(0) gate, robot+0x338 capture and 0x04000009 (0x00558228..0x00558344), 30.0f timeout. Still open: SmartRemoveDisableReactionsLock on a non-zero result; DriveStraightAction Init/CheckIfDone and the two-argument constructor defaults (speed 100/-80, accel 0x43480000, decel 0x43FA0000) unread; IsInSdkMode's source; no production caller (only the ManipTool). M12-024 read (re-analysis/research/20261013-m12-024-driving-lifecycle.md S1/S2): DriveStraightAction::Init calls handler Init (repeat 0) at 0x005475F6; CheckIfDone 0x005478C0..0x005479EE (state-3 gate, LastPathFailed 0x04000002 without PlayEndAnim, PlayStartAnim once when the path starts if +0x8A, PlayEndAnim at the end, Success after the end animation); destructor 0x00547540..0x00547558. Log strings 0x005478F0..0x005479AE unread.\nbuilt, awaiting strong verification: B-M5-17 existing DriveStraightAction is an ActionRunner using handler Init repeat 0, state-3 hold, failed path retry without end, start once, active hold, end then Success; cleanup IsActive/Abort then shared destroy. Concrete timeout, native LastPathFailed/HasPathToFollow/IsActive and CanInterrupt inputs remain MISSING. Info/debug calls 0x00547904/0x0054794E/0x005479AE remain MISSING, exact strings/arguments unread. Existing geometry path creation is not reclassified; concrete DriveOffChargerContactsAction/native derived result mapping remains incomplete.\nB-M5-17 host cancellation adapter: stored Cancel skips UpdateInternal callbacks; the host task completes at the existing ActionQueue WatcherEnding/destruction boundary, without adding an end animation or changing ActionList order. Regression fixtures now tick ActionList for start, planner failure and cancellation.\nbuilt, awaiting strong verification: B-M5-18 uses native base timeout 30.0f and CanInterrupt=false, ActionList cancellation, real DriveStraight name (13 bytes at 0xBEA0DC), native float |d|<0x3727C5AC early return with started=true/no path. AppendLine/ExecuteCustomPath failure branches 0x005476AE..0x005476D2 are visibly MISSING, not fabricated. Path-follower status/IsActive stand-in is explicit M12-032; geometry candidate and unread diagnostic arguments retained.\nB-M12-1 built, awaiting strong verification: DriveStraight ExecuteCustomPath failure returns 0x03000016; CheckIfDone uses real LastPathFailed/HasPathToFollow/IsActive and cleanup globally aborts active PC. Existing AppendLine/motion-profile gaps remain. OV09 check (re-analysis/research/overnight/20261014-ov09-m13-029-check.md): DriveStraightAction's dtor Abort is gated on PathComponent::IsActive (0x00547540..0x0054754E).\nB-M5-20 (2026-10-15), built, awaiting strong verification: bounded HostDriveStraightTick adapter fix: a nonzero PathRun.Result from StartPath returns 0x03000016 before waiting; a null wait after a successful start retains the prior cancellation mapping. Native ExecuteCustomPath failure return 0x005476C6..0x005476D0. The host adapter remains explicitly unported as a whole; this does not claim full native DriveStraight Init/CheckIfDone.\n\nB-M13-4 (2026-10-15), partial build, awaiting strong verification; BLOCKED: OV09 DriveStraightAction destructor active-status guard confirmed after Path Abort: no extra ClearPath. No change to existing guard or legacy/live drive scheduling and traversal gaps; the test does not certify the complete native DriveStraight lifecycle."
```

Current `M7-026` full `unresolved` (literal, for residual decisions):

```json
"Build with the M7 mood manager; until then M5-011/M5-014 keep a visible MISSING.\nB-M5-19 input boundary: MoodState currently exposes emotions but no recovered GetSimpleMood operation. Default driving selection reports M7-026 once per drive and proceeds to needs/default as explicitly required by B-M5-19; no enum is derived from emotion values and no mood behavior is claimed built. Note 2026-10-19 (re-analysis/research/20261019-m5-settling-b.md): the driving handler's GetSimpleMood use (0x004F0D2C..0x004F0D74, M5-063) is this record's; cite it at approval.\nB-M5-23 (2026-10-19), built, awaiting strong verification: AnimationLibrary SimpleMood and CozmoAnimations MoodManager+0x130 clock diagnostics now name M7-026; reportedMoodClock uses Interlocked.Exchange so concurrent calls emit once per binding. Existing Default mood/BaseStationTimer stand-ins remain unverified and unchanged. No new M7 implementation or status claim."
```

Current `M8-027` full `unresolved` (literal, for residual decisions):

```json
"RECOVERABLE_GAP: enumerate the subscriber callables held by ProtoHandles at teardown and read each destroy body (expected mostly bx lr). B-M8-6 (2026-10-16): UiSubscriptionHub actual nonnull callback unlink and signal-owner destruction visibly report unbuilt native captured-owner finalization0x0066350E/node+20 release0x0066352E..0x00663560 on stack channel, deduplicated per hub after storage lock release. This is not subscriber operator() invocation, a native empty-capture proof, or a complete eight-kind callback census. M8-024 +88 remains separate. Engine shutdown detaches its global missing sink before destruction; CozmoEngine now forwards the already-reported hub gap to its own stack log after hub retirement, so the diagnostic remains visible without reordering destruction or adding a source log. Batch check 2026-10-16 (re-analysis/research/overnight/20261016-batch-check.md): E2G signal mechanism read (dtor 0x006634B8..0x006634E4, unlink 0x006634F4..0x0066352C, node release 0x0066352E..0x00663562, dispatch 0x00663C74..0x00663CB8); the E2G ProtoHandle release 0x00664708 IS reached via the ScopedHandleContainer dtor. Release slots of the 8 kinds: 0x004EF138, 0x0051D8F8, 0x00591550, 0x0064E618, 0x00664708, 0x00664EB8, 0x006A3EFC, 0x0083BED0. The OV81 census (six signal kinds: 23 E2G, 22 G2E, 21 R2E, 1 audio, 1 public-state, 1 AssignmentDef) finds every callable trivial. Open: which of the 9 non-trivial void() callables the void()/Dispatch signals hold. OV87 is not evidence. Read 2026-10-16 (re-analysis/research/overnight/20261016-ov105-107-108-answer.md): SETTLED in substance: no signal holds any of the 10 (not 9) non-trivial void() callables; every release/remove is memory-only with no log, message or invocation. IMPLEMENTATION_GAP only so the existing code is confirmed; nothing behavioural left to build. B-M8-6's \"unbuilt captured-owner finalization\" diagnostic can be removed."
```
