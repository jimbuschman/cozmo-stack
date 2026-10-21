# M5 startup / cached-neutral extraction — 2026-10-20

Request: manager's bounded investigation of operator bundle
`C:\Users\jbuschman\Downloads\20261020-180933-M5-HW-bde6cd` after the Claude handoff.
Role: read-only extractor. PROJECT_STATE read first. No production/test edits, build,
hardware run, manifest change or commit. Partial HarnessStartup changes are candidates,
not evidence of original behavior.

**Finding:** the bundle proves M5-HW constructed the connected Robot while engine assets
were still loading. Its once-per-Robot neutral lookup failed and stayed cached null.
This is a harness violation of the already-approved M2-021 host connection policy, not
a reason to add a retry to the source-faithful M5 engine. Existing inventories suffice
for waiting on delivered readiness before submitting ConnectToRobot. No new behavior
or operator inventory approval is required for that bounded harness correction.

## Primary source path (every behavior-changing step)

Fresh instructions, including bytes and exact ranges, are in
`20261020-m5-startup-native.txt`; ELF SHA256 is
`02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
Decompiler names are navigation annotations only. C# under cozmo-stack is traced below
as the implementation being examined, never used to establish native behavior.

| Step | Original behavior | Primary evidence | Record / classification |
|---|---|---|---|
| 1 | UI input is processed before engine startup-state dispatch. Thus an early Connect is not held until loading finishes. | CozmoEngine::Update calls UiMessageHandler::Update at0x004ED514; state switch0x004ED5C4..5CC follows. | M2-021 COMPATIBILITY_POLICY; native mechanism EXACT_SOURCE |
| 2 | Loading moves state1 to2; state2 calls DoNonConfigDataLoading. Return exactly1 causes BroadcastAvailableAnimations then SetEngineState(3); progress report follows in same tick. | 0x004ED5D6..5EA; 0x004ED5FA; cmp1 at5FE; broadcast606; movs r1,#3 at60C; SetEngineState60E; progress helper618. | M1-055 EXACT_SOURCE; host inputs M1-058 COMPATIBILITY_POLICY |
| 3 | Nonconfig loader starts a worker when absent; when progress>=1 it joins, sets done, returns1. | 0x00523570 copies progress; 0x0052358E..5C8 checks worker/progress and join, done store5C8; worker creation5E2. LoadNonConfigData0x0051F38C..492 sequentially calls existing animation/group/light/face loaders and finishes progress. | M1-055/M5-001 EXACT_SOURCE |
| 4 | Shipped nonmock app waits for EngineLoadingDataStatus ratio>=1 before finishing loading and entering main scene. Normal connection UI later sends ConnectToRobot. | unity/scripts/csharp/StartupManager.cs:274 subscribes; :322-329 waits; :368-370 assigns ratio; :337-342 gates main scene; :562-564 loads it. ConnectionFlowController.cs:437 -> :670-673; RobotEngineManager.cs:510-528 serializes/sends. Connection UI instantiated by NeedsConnectionManager.cs:225-238 or FirstTimeConnectView.cs:245-252. | M2-021 COMPATIBILITY_POLICY naming this app input. App code authority2; scene-asset placement was not separately reconstructed in this bounded review. Existing approved policy already adopts this normal startup ordering. |
| 5 | Native Connect only checks whether robot1 exists; otherwise AddRobotConnection then immediate AddRobot(1), before handshake results, no engine state/load gate. | 0x004ED020..02C exists gate; 0x004ED074 AddRobotConnection; 0x004ED07C AddRobot; wrapper0x004ED1E8..1F6. | M1-025 EXACT_SOURCE; M2-021 policy boundary |
| 6 | RobotManager creates Robot, whose constructor constructs AnimationStreamer in the Robot. | 0x0052EE92 allocation, 0x0052EE9C Robot ctor; Robot ctor0x0050FCFE..FD06 passes robot+0x60 to streamer ctor. | M1-025 / M5-010 EXACT_SOURCE |
| 7 | Streamer initializes pointers including neutral+0x40 to0, then reads NeutralFace trigger/group once. Null/empty group logs BadNeutralAnimGroup, sets errG, and skips to TLC.Init. | 0x00579FA6..FAA clears +0x34..5F; 0x0057A0D4 mov r2,#0x146, A0D8 trigger lookup; A0E2 group lookup; A0E8/A0F0 branch; A122 sErrorF; A158 errG; A15C/A164 -> A280; A282 TLC.Init. | M5-010 EXACT_SOURCE; inner InvalidName M5-065 EXACT_SOURCE |
| 8 | Valid group uses first clip; its first ProceduralFace becomes reset data, then TLC.Init resets stored layer base. Missing clip bypasses SetResetData. | 0x0057A1C6 GetFirstAnimationName; A1F2 GetAnimation; A1F8 stores neutral pointer; A1FA misses toA214; A1FC..A20E first face and SetResetData; A282 Init. Reset0x0058359C..35B0 checks global reset pointer and is no-op when null; TLC.Init0x0064EDE2 tail calls Reset. | M5-010 EXACT_SOURCE |
| 9 | Later neutral replay uses the cached pointer; it does not repeat library/group lookup. Ordinary keepalive remains available once a stream has run. | 0x0057CF6A..76 last-stream>0; 0x0057CF90..BE elapsed gate; CFC0 tests replay flag; CFC6 loads cached+0x40; CFD2 SetStreamingAnimation; CFD6 clears flag; CFF2 KeepFaceAlive. | M5-010 / M5-028 EXACT_SOURCE |
| 10 | A streaming clip can subsequently overwrite the layer base face, independent of constructor reset data. Thus missing cached neutral does not prove every later layer image is default/wrong. | 0x0064F1EE validates animation face result; F1F6 loads storeFace; F1FA..F200 conditional writeback. | M5-019 EXACT_SOURCE |

The no-retry claim is already explicit in settled M5-010. This check re-opened its
constructor and replay path and traced C# load completion. It does not invent a recovery
path when the app boundary supplies an early input.

## Current manifest claims (not recalled status)

Full verbatim titles/status/authority/evidence/unresolved for ten relevant records are
saved in `20261020-m5-startup-records.json`. Key claims:

- **M2-021 / COMPATIBILITY_POLICY** — “App input: when ConnectToRobot is sent (the app
  waits for EngineLoadingDataStatus ratio >= 1); the engine has no load-before-connect
  gate”. Evidence names Connect0x004ED020..07C, AddRobot0x004ED1E0..1F2,
  loading0x004ED5CC..618/0x00523570..5FA and StartupManager322-341/ConnectionFlowController670-673.
  Current unresolved explicitly adopts IsReadyDelivered as the host wait point
  (manager2026-10-18), while direct ConnectToRobot stays ungated.
- **M5-010 / EXACT_SOURCE** — “Neutral face: the first ProceduralFace keyframe of the
  first clip of ag_neutral_face; reset data and layer base; replayed after
  abort-to-nothing and RemoveIdle”. Evidence A2/A30/A31 and reset/ctor addresses above.
  Current settlement2026-10-19 explicitly closes once-per-Robot binding; B-M5-16 note
  explicitly says “No load-completion retry.”
- **M5-065 / EXACT_SOURCE** — “AnimationGroupContainer::GetAnimationGroup miss:
  InvalidName error, errG, null return (callers: streamer constructor,
  GetAnimationNameFromGroup)”. Evidence const helper0x0058B6F8..B768 and constructor
  call0x0057A0E2 with no empty-name check.
- **M1-055 / EXACT_SOURCE** — “CozmoEngine::Update: BroadcastAvailableAnimations then
  SetEngineState(3) when DoNonConfigDataLoading returns 1”. Evidence0x004ED5F0..60E.
- **M5-019 / EXACT_SOURCE** — “The layer base face is written back only by a streaming
  animation, not idle ones”. Evidence0x0064F154..244, writeback0x0064F1EE.

No current record was found contradicted or needing a fidelity upgrade for this fix.

## Complete C# joins relevant to the failure

1. `CozmoRobot.Create` (CozmoRobot.cs:505-513) constructs the host, invokes
   Options.BeforeStart, then StartProduction and returns. It does not wait for assets.
   Host constructor ConfigureStartupLoaders at:407 binds animation loading.
2. `EngineReadiness.PollNonConfigLoading` (:108-140) starts a background worker calling
   Animations.LoadForEngine, publishes names and progress1 only after it returns;
   polling joins the worker. `AnimationAnnouncements.UpdateCheckedNonConfigLoading`
   (:104-130) broadcasts names then sets3. `CozmoEngine.TickAtCore` (:2103-2109) processes
   queued game messages before this loading path, just as native Update does.
3. `SetEngineState` (`EngineReadiness.cs`:66-85) stores state then queues app delivery.
   Its beforeDispatch sets _hostReady and notifies waiters. IsReadyDelivered (:25)
   requires both IsReady and this delivered flag. `ConnectAsync` (CozmoRobot.cs:536-552)
   subscribes, checks delivered/failure, awaits readiness, then sends ConnectToRobot.
4. Direct `ConnectToRobot` (CozmoEngine.cs:2158-2177) posts without a load gate;
   RobotManager.AddRobot (:1641-1656) constructs EngineRobot. Its lifetime initialization
   invokes Engine.BindRobotLifetime (RobotLifetime.cs:159), bound by CozmoRobot.cs:391-395
   to Animations.BindRobotLifetime. That calls LoadNeutralFace once
   (Animation/CozmoAnimations.cs:221-224).
5. Scheduler.CatalogProvider uses current Library or its concrete empty library
   (CozmoAnimations.cs:175-176). LoadNeutralFace (AnimationScheduler.cs:796-820) caches
   _neutral=null, resolves trigger/group, logs/returns on miss, and initializes TLC.
   `LoadForEngine`/`LoadOnLoaderThread` (CozmoAnimations.cs:364-410) later publish/populate
   Library; neither calls LoadNeutralFace. The late load does not repair the pointer.
6. M5-HW's bundled source calls Create with EngineEvidence in BeforeStart, attaches
   wire/diagnostic handlers, then calls ConnectToRobot immediately. Only afterward does
   it poll connection/time-sync/first-state/ready-to-stream and ClipNames.Count>0 plus
   calibration. These predicates do not repair earlier construction or substitute for
   delivered engine readiness.
7. SaveNeutralReference uses `Scheduler.NeutralFaceAnimation`, not a fresh lookup from
   the loaded library (bundled M5Hw.cs:408). Its missing reference directly reflects
   cached-null neutral. Earlier preliminary suggestion that the SVG used the later
   library was incorrect and is withdrawn.

## Bundle witness: ordering, not speculation

Bundle result commit: `1bd606265157ef133337e634ab7ea023536725b8`.
`events.jsonl` line numbers are one-based; times are host capture milliseconds.

| Line | atMs | Captured event |
|---|---:|---|
| 4 | 3375.1459 | app.engine.state Stopped -> WaitingForUIDevices |
| 5 | 3378.0694 | WaitingForUIDevices -> LoadingData |
| 11 | 3467.2817 | GetAnimationGroup_Const.InvalidName for empty group name |
| 12 | 3467.4510 | AnimationStreamer.Constructor.BadNeutralAnimGroup |
| 1466 | 3811.9637 | loading anim_neutral_eyes_01 from anim_singlepose_01.bin |
| 2157 | 3884.0726 | loading ag_neutral_face.json |
| 2176 | 4098.3268 | LoadingData -> Running |
| 2208 | 4285.5491 | connection-response Success |
| 2503 | 6798.2826 | check-readiness assets=true, calibrated=true |

ABORT and KEEP_ALIVE result.neutralReference both explicitly report the cached
neutral clip/first face unavailable. `errors[]` being empty is not evidence of clean
startup: source engine errors are in events.jsonl, while that array is harness
exceptions. The log ordering and actual null reference establish the startup defect.

## Other harnesses and partial-fix limits

- Unmodified M3M4Hw Create -> Subscribe -> Connect also lacks delivered readiness;
  this is the same reachable policy defect, though no new M3/M4 bundle was judged here.
- ControlCheck is different: Create -> Subscribe -> synchronous LoadAnimations ->
  Connect. LoadAnimations (ControlCheck.cs:764-786) calls Animations.LoadFrom. Therefore
  it lacks the explicit readiness gate but cannot be claimed to have the same proven
  neutral miss merely from its Connect call. Its explicit loader can overlap the
  engine startup loader; this is a concrete additional startup join to account for,
  not an excuse to assert all three paths failed identically.
- M5 logs attach via BeforeStart and capture this connected-Robot constructor error.
  M3M4Hw and ControlCheck currently attach EngineEvidence only after Create returns,
  leaving engine startup logs before subscription unobserved. Even BeforeStart occurs
  after the host CozmoRobot constructor: “every error since object creation” overstates
  coverage. State the actual capture boundary and cover asynchronous engine startup.
- A startup error summary must distinguish logs from exceptions. The missing neutral
  makes this run unsuitable for neutral-dependent acceptance; retaining other observed
  wire facts does not turn the run into a clean acceptance. A blanket displayed claim
  that every frame/behavior was wrong is unsupported.
- Existing partial HarnessStartup waits on the correct approved policy flag. Its
  timeout/cancellation/reporting are harness machinery. This extraction is not its
  implementation verification; no approval is inferred for every line or demotion.

## Effects and remaining uncertainty

Established: early connection, failed one-time neutral initialization, absent cached
neutral/reference, and use of null cached neutral on later replay requests. Keepalive
can still generate layers after explicit playback. Streamed clips can write TLC's base;
no evidence here establishes that every later eye picture is default, absent or wrong.
Actual visible distortions, apparent head behavior and each individual LAYERS failure
need their own captured path and must not be attributed wholesale to this startup bug.

The manager reports DRIVING had FailedTraversingPath, result67108866, and
successfulTraversalCoverage=false; this startup extraction does not explain or change
that separate M12/M13 uncertainty. No new hardware acceptance or manifest settlement.
