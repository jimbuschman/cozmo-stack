| Requested item | Coverage | Limit |
|---|---|---|
| Pull first / research lane | CHECKED | Already current1398e06 |
| M10-001 writer and timing | CHECKED | Constructor plus committed classifier state |
| PickedUp M10-003 | CHECKED | Generic predicate, not raw pickup bit |
| FistBump / parameters | CHECKED | Event, cooldown, randomdraw, expiry, reset |
| Hiccup / parameters | CHECKED | Local state machine, downcast, runnable gates and RNG conversion checked; feature/progression/needs interfaces named. Their subsystems are outside this strategy scope. |
| Sparked | CHECKED | ShouldTrigger gates and force-setup IsRunnable forwarding |
| Current touched M10/M7 quotations | CHECKED | Below before any comparison |

# M10 dependencies of M7 reactions — build rows

Answers request2 in the operator's 2026-10-04 message. Primary source resources/lib/armeabi-v7a/libcozmoEngine.so; virtual Thumb addresses, state bit removed. [Native companion](20261004-m10-strategies-native.txt) preserves instruction ranges and raw literals. Decompilation is navigation only. No code/manifest edits or commit.

## Current record quotations

### M7-002

> **title**: The shipped animation/reaction maps, behaviour configs and activity tree

> **status**: IMPLEMENTATION_GAP

> **evidence**: ["re-analysis/obb/assets/cozmo_resources/assets/animationGroupMaps/AnimationTriggerMap.json (573 pairs)", "re-analysis/obb/assets/cozmo_resources/assets/cubeAnimationGroupMaps/CubeAnimationTriggerMap.json (40 pairs)", "re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/reactionTrigger_behavior_map.json (22 rows over 21 triggers)", "RobotDataLoader::LoadReactionTriggerMap 0x00520bc8", "BehaviorManager::InitReactionTriggerMap 0x005a16e4", "178 shipped behaviour configs enumerated in re-analysis/inventory/M7-behaviour.md Appendix C"]

> **unresolved**: Opus verification of R-BEH2 batch 3 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-3.md): NOT YET. The reaction-map parameter blocks are parsed but the strategies are built from literals (stand-in); the failure log lacks the event name RobotDataLoader.ReactionTriggerMap (0x00520D48); the FistBump, Hiccup and Sparked rows are unbound (their M10 strategies are unbuilt). Before: built, awaiting strong verification: RobotDataLoader::LoadBehaviors (0x005206bc) is built (recursive read of config/engine/behaviorSystem/behaviors/, first duplicate ID wins silently; the shipped corpus loads as 178 configs); a missing or empty reaction map registers nothing (no fallback; LoadReactionTriggerMap 0x00520c2e..0x00520c8a). Still open: the map's parameters (genericStrategyParams, frustrationParams, behaviorObjectiveTriggerParams, hiccupParams) are parsed but the strategies are built from literals (M10 owns the strategies; the rows do not settle which parameter feeds which field); the failure line lacks the engine's event name 'RobotDataLoader.ReactionTriggerMap' (0x00520d48); directory enumeration order is platform-dependent (BLOCKED_EXTERNAL, affects log order and duplicate arbitration only).

### M7-014

> **title**: Reaction-lock manager lifetime and concrete per-class lock tables

> **status**: IMPLEMENTATION_GAP

> **evidence**: ["IBehavior::SmartDisableReactionsWithLock 0x005bce3c", "BehaviorManager::DisableReactionsWithLock 0x005a27e8", "BehaviorManager::RemoveDisableReactionsLock 0x005a3a48", "13 concrete 21-entry tables in M7 inventory Appendix F"]

> **unresolved**: Opus verification of R-BEH2 batch 3 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-3.md): NOT YET. Nine classes with a recovered lock table still call Scope.DisableReactions() (CubeGameBehaviors.cs:74/272/405/474/611/740/1064, ManipulationBehaviors.cs:57/108/181/284, ObjectBehaviors.cs:238). New: Behaviors.cs:481-483 (ReactBehavior) falls back to scope.DisableReactions() for a class with no table; the engine only calls SmartDisableReactionsWithLock 0x005BCE3C with a class's own table. The Spark Init/Resume seams are unset; three non-static tables are RECOVERABLE_GAP. Before: built, awaiting strong verification: the six tables (0xC73D36, 0xC672F0, 0xC6F590, 0xC72BB2, 0xC74182 and SparkBehaviorDisables 0xC65F90, bytes read) are installed through SmartDisableReactionsWithLock at the engine's install points for ReactToImpact, DriveOffCharger, Singing, AcknowledgeCubeMoved, ReactToOnCharger and ReactToCliff; Init/Resume SparkBehaviorDisables (0x005bcd16..0x005bcd44, 0x005bcfac..0x005bcfde) behind two seams (UnlockIdentifier, ActiveSparkIdentifier) that are unset in production and report MISSING. Still open: the nine classes whose table is among the 13 recovered but whose code is in R-VIS/M12-M15 files (CubeLiftWorkout, PeekABoo, PutDownBlock, FistBump, Bouncer, GuardDog, Dance, EnrollFace, OnboardingShowCube) still use the arbiter-wide Scope.DisableReactions(); the sites the record omits (KnockOverCubes x2, PopAWheelie, DevTurnInPlaceTest, FeedingEat, BuildPyramid, RequestGameSimple x2, RamIntoBlock, the 4 IActivity::SmartDisable callers); three non-static tables are RECOVERABLE_GAP (IDockAction+0xC4, the severe-need map, a config-copied array).

### M10-001

> **title**: Off-treads classifier CheckAndUpdateTreadsState: gate, inputs, thresholds, branches, 250 ms debounce commit and every consequence

> **status**: IMPLEMENTATION_GAP

> **evidence**: ["A1..A7 CheckAndUpdateTreadsState 0x511E00..0x5121F8 (thresholds 0x5121FC, 0x5122A0..0x5122BC)", "A8..A15 consequences: Falling DAS + ActionList::Cancel(-1) (0x511FF0..0x512084; gap1 3a..3e), RobotOffTreadsStateChanged broadcast 0x512092, OnTreads 0x512112..0x512184, carried 0x512100, SetOnChargerPlatform 0x512188, pause flag 0x5121E2", "gap1 5a IMU filter state zeroed (0x5100E0..0x5100FE)", "R-ANIM pre-extraction part 2 item 1 O1..O4: Radians operator> 0x84CC90, IsNear 0x84CC0A, operator< 0x84CD12, rescale 0x84C87C"]

> **unresolved**: The Radians comparisons are settled (C3): operator> tests the raw diff > 0 and wraps inside IsNear. The M11/M12/M15 consequences are seams.

### M10-003

> **title**: Reaction-strategy factory rules and strategy classes (Cliff, Falling, PickedUp, Shaken, Slope, Frustration, PlacedOnCharger, Sparked, NoPreDockPoses, FistBump, Hiccup, Pet, CubeMoved, FacePositionUpdated, ObjectPositionUpdated)

> **status**: IMPLEMENTATION_GAP

> **evidence**: ["C12 factory switch 0x60D618; C13..C17 Generic, Shaken, Slope, Frustration", "gap1 1 Cliff filter 0x60DC7C..0x60DCA2; gap1 8 strategy predicates", "gap2 1..6 PlacedOnCharger 0x614474..0x6144D0, CubeMoved 0x60C04A..0x60C1E2 / 0x60BEB4, Face 0x60CB84..0x60CE20, position-update base 0x612168..0x612B22, Object 0x6114A0..0x611582", "R-ANIM pre-extraction part 2 item 5.1: EnabledStateChanged +0x1C is a no-op (0x60B73B) for Shaken/Slope/Frustration"]

> **unresolved**: The +0x1C EnabledStateChanged for Shaken, Slope and Frustration is settled as a no-op (C5). The CubeMoved (0x60C03C) and Hiccup (0x610AF8) +0x1C bodies are RECOVERABLE_GAP; FistBump, Sparked, the NoPreDockPoses +0x70 writers and Pet EnabledStateChanged (InitReactedTo) are not built.

### M7-018

> **title**: Shipped behaviour configuration and BehaviorClass factory binding

> **status**: IMPLEMENTATION_GAP

> **evidence**: ["178-config table in M7 inventory Appendix C", "79-class entry-point table in Appendix B", "BehaviorContainer::CreateBehavior 0x0059c888", "class 0x39 BehaviorWait case constructs base IBehavior and installs the BehaviorWait vtable"]

> **unresolved**: Opus verification of R-BEH2 batch 3 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-3.md): NOT YET. 76 of 79 classes report "class not implemented"; the second BehaviorContainer at robot+0x48 is unbuilt (use UNKNOWN); RequestAllBehaviorsList (0x0059C44C) is unbuilt; FistBump's seams are unset in production although SparksFistBump is bound; Behaviors.cs:67 uses Enum.TryParse, which accepts names the engine's map rejects; Implementable() builds duplicate objects. Before: built, awaiting strong verification: the container loop (0x0059c34c..0x0059c428), BehaviorClassFromString (cerr text, 0 = Bouncer), BehaviorIDFromString (cerr text, 0 = AcknowledgeFace; default 'IBeh.NoBehaviorIdSpecified'), AnimationTriggerFromString (cerr text, 0, only Count skipped), AddToFactory, the 79-way table (all 79 rows checked against the binary), FistBump (full state machine incl. the +0x84 gate, state-2 empty-callback turn, wrapped scan index N=2, terminal order, thresholds as bit patterns), ReactToSparked, and Hiccup's PlayAnim half are built. Config-built constructors exist for only PlayAnim, FistBump and ReactToSparked; the other 76 classes report 'class not implemented'. Still open: the second BehaviorContainer (robot+0x48) is not built and its use is UNKNOWN; RequestAllBehaviorsList (0x0059c44c..0x0059c676) is not built; FistBump's seams are MISSING in production (SmartPushIdleAnimation(0x23F = Count), EnableLiftPower/EnableHeadPower bodies, a live PanAndTiltAction (M13-020), BehaviorObjectiveAchieved's body and ordinals 7/8/9, the carry source); SparksFistBump is bound and can run live with those seams unset; ReactionTriggerStrategyFistBump/Hiccup/Sparked (M10) are not built, so the map rows for FistBump, Hiccup and Sparked remain unbound; Implementable() still hand-builds Hiccup, ReactToObstacle and six Feeding* PlayAnim objects that the container also builds (shadowed, dead in production); Behaviors.cs:67 Enum.TryParse is looser than the engine's name map.

## Classifier

| Step | Address | What / values | Gates | Order | Failure / UNKNOWN |
|---|---|---|---|---|---|
| T1 | ctor0x005100E2..EA; classifier0x00511E00 | Initialize committed+355,candidate+356,deadline+358,fallingtime+35C tozero. | Construction | Before telemetry | Only recovered non-classifier initialization |
| T2 |0x00512A4A..0x00512A72;0x00511E1C..0x00511E60| Filter accelY+384 before classifier call. Require calibratedhead+314. Read pitch+304, physicalflag+14,statusmsg+4C FALLING20/PICKED_UP08. Clock BaseStationTimer timestamp.| Calibrated | Robot state handling before reaction consumption | Uncalibrated returns0 |
| T3 |0x00511E66..0x00511F04| side=abs(abs(Y)-9800f)<3000f; back=abs(double(pitch)-physical/simcenter)<=doubletolerance; extreme uses Radians > 0x3FF5BE0B or < 0xBFB2B8C2; level abs(pitch)<=pi/4f.| Ordered tests | Falling,side,face,back,level/pickup | Radians operator > at 0x0084CC90 first checks f32(a-b)>0, then returns !IsNear(a,b,0x3727C5AC); < at 0x0084CD12 swaps operands. IsNear 0x0084CC0A normalizes a and (a-b), then strictly compares abs(delta)<abs(tolerance). rescale dependency 0x0084C87C remains a separate numeric primitive. |
| T4 |0x00511F0E..0x00511FCC;0x00512228..25C| Fallingnewcandidate6 t=now-250; side3/4 stampsnow only enteringpair, positiveY→4; face5 stampsnow; back2 stampsnow+750; level andcandidate>=2→1 stampsnow; pickedUp andcandidate0→1 t=now-250.| Native precedence | Candidate beforecommit | Sidepairchange doesnotrestartdebounce |
| T5 |0x00511F48..98| Tail clearscandidate0,t=now-250 when no pickedUp/side/back/extreme andcandidate!=0.| Else/Falling tail | Aftercandidate branch | Do not run tail on side/face/back branch |
| T6 |0x00511FD0..FEE;0x00512088..08E| Commit+355=candidate onlywhen uint32(t+250)<=now andcommitted!=candidate.| Deadline andchange | Effectiveback1000ms; side/face/level250ms; falling/rawpickupimmediate | Return1 onlycommit,0otherwise; uintwrap retained |
| T7 |0x00511FF0..0x00512084| Enterfalling stampspackettime+2C to+35C, logsDAS,Cancel(-1); leavefalling logsduration, clears+35C.| Fallingtransition | Beforestatebroadcast | DAS differsfromrobotmessages |
| T8 |0x00512092..0x005121F4| BroadcastRobotOffTreadsStateChanged,log; states2..6 detachcarryifpresent; OnTreads worldcheck mayset+2C4=1,+2B8=-1; nonzero clearchargerplatform; freeplaypauseflag2.| Commit | Broadcast then consequences | Carry/world methods subsysteminterfaces |

## Strategies

| Step | Address | What / values | Gates | Order | Failure / UNKNOWN |
|---|---|---|---|---|---|
| P1 |factory0x0060D5A0;predicate0x0060DDCE..DDD8| Trigger12 RobotPickedUp uses Generic, callback LDRB robot+355,compare1.| CommittedInAir | Classifierbeforepredicate | No separate named PickedUp class; rawbit insufficient |
| P2 |0x0060EF2C;0x0060F4A8;0x0060F3BC| genericStrategyParams required; wantsconfigGeneric; predicate copied to wantsstrategy+68. Should requires wantsstrategy, behaviorrunning OR IsRunnable,timeoutgate,then WantsToRun.| Running/runnable andpredicate | Runnablebeforepredicate | Missingwants VERIFYfalse; eventtimeoutmachinery not replacedwithpickup latch |
| B1 |0x0060B63A| Normal invokesinternal. Force+29 invokesinternal; false→setupforce; clearforce; return1regardless.| Force | Internal before optionalsetup | Force canoverride normalfalse |
| F1 |0x0060DFB4;LoadJson0x0060E0A8| Objectiveparammap,latch+44false,deadline+48zero,lastcompletion+4Czero;subscribe8BBehaviorObjectiveAchieved.| Construction/config | Parsebeforesubscribe | MalformedJSON helpersemantics notfullyexpanded |
| F2 |0x0060E8C0..E99A| On8Blookupobjective; iflastcompletion!=0 andfloat32(now-last)<=cooldownskip; RandDbl(1.0double)<double(probabilityf32)→log,latchtrue,deadline=float32(now+expiry).| Objectiveexists,cooldown,strictprobability | One drawbeforelatch | Miss/drawfailuredoesnotlatch |
| F3 |0x0060E7E4..E872| Latched,notrunning,now<=deadline,onTreads→returnIsRunnable. Expiredlogsandclears; runningclears.| Expirybeforetreads | Tick evaluation | Offtreads/runnablefalse withinwindowpreservelatch |
| F4 |0x0060E9FA..EA16| Clearlatch; onlyReset(true)stampscompletionseconds.| Resetbool | Clearbeforestamp | Falseresetdoesnotnewcooldown |
| H1 |ctor0x0060FD50;Parse0x0060FECC| hiccupParamsintegerfields: occurrence/cureseconds multiply1000; count/spacingalreadyinteger.| Construction | Parse→reset | Feature/progression/needs remaininterfaces |
| H2 |Reset0x006101A0..0x0061022E| Drawcount→remaining=total; drawoccurrence→next=spacingNext=now+draw; clearfirsttime+54; ifhashiccups clear/broadcastfalse; clearwhiteboard+74.| Reset | Countdrawbeforefrequencydraw | RNG conversion specified in R12–R13 below; shared generator state is an interface |
| H3 |Should0x00610678..0x006108A6| FindPlayAnimSequenceID/type26; feature6; configuredunlock. LockedResetfalse. Captureforced+3C,clearit.| Feature/unlock | Lookupbeforegates | Downcast failure specified in R11 below |
| H4 |samebody| State<2: next<now; severeneedsbracket>=2 elseCure(false); setwhiteboardflag; spacingNext<now thenadvancebyRNG; forcedsuppressesoccurrence; decrementremaining,old0cures.| Strictdeadlines | Spacingdrawbeforecount/runnable | Nonrunnableconsumescount/interval |
| H5 |samebody;GetHiccupAnim0x0061062C| Firstoccurrencehashiccups/broadcasttrue; setsequenceE2firstelseE1; IsRunnabletrue→stampfirsttimeifzero,NeedActionCompleted1A.| Countoldnonzero,runnable | SequencebeforeIsRunnable | FalserunnablelogsBehaviorNotRunnable |
| H6 |samebody;table0x00C76818/1C| State2→sequenceE3; state3→E7; IsRunnabletrue→state0,ResetKeepFaceAliveLastStreamTimeout.| Curingstate | Sequence,runnable,reset | Falseleavesstate |
| H7 |Cure0x0061098C..9CA| DAS,Reset,state3; true→state2,next+=curedelay,NeedActionCompleted19; false→18.| Cure | Resetbeforecuredelayaddition | Do notaddtoolddeadline |
| H8 |0x006109CC..0x00610A42| Enabledtrigger5 andevent35: state0face5/back2afterdeadline→robot+220=40A00000,state1; state1OnTreadsafterdeadline→Cure(true).| Enabled,now>next | Eventtransition | +220meaningUNKNOWN; exactstore5f |
| H9 |0x00610A44..AC0;0x00610AF8| EnabledG2EtagA4 setsnext=spacingNext=now; disabledlocalbodylogs/_errG. Disabledstatechange setsforced andmaycure whennow>next andneed0or1bracket3.| Enabled/needs | Inputwriters | Upstreamroutingmustnotinventwhichmessagesinvokeit |
| S1 | ctor 0x00613078; force 0x006130F8 | Base strategy/vtable construction. Force setup loads the supplied behavior pointer from [r2] and tail-forwards to IBehavior::IsRunnable via mixed-ISA veneer 0x008CC96C → PLT 0x004B0F54. | Factory trigger 19 | Constructor before ShouldTrigger | No field store in this force wrapper; IsRunnable result is forwarded. |
| S2 |0x006130FE..0x0061316A| RequireCurrentBehaviorTriggeredAsReaction==1; managerrequested+60!=55 and!=+58; rejectcurrenttype+64==3D or4C; then targetIsRunnable.| Allgates | Request,currenttype,runnable | Nullcurrentnotrejectedbytypegate |

## Exact binary literals

| Use | Bits | Address |
|---|---|---|
| -9800f |C6192000|005121FC|
| 3000f |453B8000|00512200|
| Simbackcenter double |3FFAEB8260000000|005122A0|
| Physicalbackcenter double |3FF4CDE840000000|005122A8|
| Backtolerance double |3FD0C15240000000|005122B0|
| Levelthreshold float |3F490FDB|005122BC|
| Hiccuprobot+220 float |40A00000|006109CCbody|

## Shipped map parameters

Source: re-analysis/obb/assets/cozmo_resources/config/engine/behaviorSystem/reactionTrigger_behavior_map.json.

| Objective | Cooldownf32 | Probabilityf32 | Expirationf32 |
|---|---|---|---|
|StackedBlock|43340000|3E4CCCCD|40400000|
|BuiltPyramid|43340000|3F000000|40400000|
|InteractedWithFace|43960000|3F400000|40400000|
|PeekABooSuccess|43960000|3E4CCCCD|40400000|
|PerformedStrongWorkout|43340000|3F000000|40400000|
|PoppedWheelie|43340000|3E4CCCCD|40C00000|

Hiccup occurrence300/3300seconds→300000/3300000ms; count5/10; spacing4500/8000ms; cure600seconds→600000ms; unlockDroneModeGame. These are integer values. PickedUp→ReactToPickup, genericshouldResumeLast=false, debugStrategyName="Trigger Strategy Pickup". Sparked→ReactToSparked,noadditionalparameterblock.

C# mapping: OffTreadsClassifier.Update in OffTreads.cs, Sensors robot-state handler; GenericReactionStrategy in Behavior/ReactionStrategies.cs. FistBump/Hiccup/Sparked concrete strategy integrations remain missing candidates under M10; their behavior classes are not substitutes for their strategy gates. No contradiction of quoted unbuilt/unbound production claims is asserted. No manifeststatuschange,tests,commits. Called feature/progression/needs interfaces are explicit; this does not settle all of M10 or implement its strategies.


Additional native threshold bits: face/extreme upper pitch 0x3FF5BE0B is constructed at 0x00511E66/6A; lower pitch 0xBFB2B8C2 at 0x00511EE6/EA. Radians comparison dead zone 0x3727C5AC is constructed at 0x0084CCAE/B4. The first hiccup animation check is exact: GetHiccupAnim 0x0061062C tests remaining == total-1, returning a one-element sequence {0xE2}; otherwise {0xE1}.

## Shared runnable gates and called interfaces

| Step | Address | What it does | Gates | Order | Failure / float bits | C# host |
|---|---|---|---|---|---|---|
| R01 | 0x005BD750..770 | IsRunnable first invokes IsRunnableBase using behavior+0x2C's robot; only result exactly1 tail-calls virtual+0x50 with the robot supplied by its caller. | Base success | Base before concrete behavior gate | Base false returns0; this wrapper does not bypass the concrete gate for running behavior | Behavior/IBehavior.cs IsRunnable |
| R02 | 0x005BD780..7D2 | IsRunnableBase's running byte+0xA1 nonzero logs AlreadyRunning and returns1 immediately. | Running | Bypasses remaining base gates; R01 still invokes concrete virtual | No failure from this base branch | IBehavior.IsRunnableBase |
| R03 | 0x005BD7D4..7F6 | Required process+0x1C is0 or analyzer IsProcessRunning nonzero; configured needs bracket+0x74 equals3 (wildcard) or current bracket at robot+0x264→+0x30→+0x14. | Not running | Process then bracket | Missing process logs error, sets _errG and optional debug break; failed bracket returns0 | IBehavior.IsRunnableBase / AI analyzer and Needs interfaces |
| R04 | 0x005BD7FA..816 | Get float32 engine seconds, then require unlock+0x70==0x55 or progression IsUnlocked(id,true)==1. | Previous gates | Timestamp before unlock | False unlock returns0 | IBehavior.IsRunnableBase / progression interface |
| R05 | 0x005BD81A..82E; 0x005BD936..95E | If behavior float+0x78 is below negative tolerance, skip; otherwise require last-object-interaction time at AI→+0x18→+0x44 >= negative tolerance, then now <= (duration + lastTime) + positive tolerance using two separate vadd.f32 instructions. | Duration enabled | Before behavior-age gate | Negative/positive binary32 tolerances 0xB727C5AC / 0x3727C5AC at0x005BDA1C/20 | IBehavior.IsRunnableBase |
| R06 | 0x005BD832..862 | Behavior float+0x7C widened to double and compared against exact double negative tolerance. If enabled, compare float32(now - manager+0x50) against float32(duration + positive tolerance). | Duration threshold | After object-interaction gate | Double threshold **0xBEE4F8B588E368F1** at0x005BD998; additive binary32 tolerance0x3727C5AC. Do not replace the double comparison with the rounded f32 threshold | IBehavior.IsRunnableBase |
| R07 | 0x005BD864..89C | Nonzero robot off-treads+0x355 requires virtual+0x20==1; nonzero charger-platform byte+0x34A requires virtual+0x24==1; carrying id robot+0x284→+8 != -1 requires virtual+0x28==1. | Robot states | Treads, charger, carrying | Each rejection returns0; these are behavior allow-state virtual predicates | IBehavior.IsRunnableBase |
| R08 | 0x005BD89E..8AA | Null behavior WantsToRun strategy+0x38 passes; otherwise strategy.WantsToRun(robot) must return exactly1. | Prior gates | Before cooldown | False returns0 | IBehavior.IsRunnableBase / IWantsToRunStrategy |
| R09 | 0x005BD8AC..8C8 | Re-read float32 engine seconds, compare against float32 next-runnable+0x118. Native MOVPL sets true: finite values pass when now>=deadline. | Prior gates | Final base gate | Preserve VFP comparison/PL condition for unordered inputs; this is not elapsed-duration arithmetic | IBehavior.IsRunnableBase |
| R10 | 0x0060B6DE..E4; mixed veneer0x008CBF6C | NeedActionCompleted loads strategy robot+8, robot context, context NeedsManager+0x34, forwards supplied NeedsActionId to RegisterNeedsActionCompleted. | Strategy calls H5/H7 | After sequence/runnable/cure state changes shown above | Needs reward/config interpretation belongs to that named interface, not an invented strategy-side reward | ReactionStrategies concrete Hiccup / NeedsManager |
| R11 | 0x0061048C..576 | FindBehaviorByIDAndDowncast first finds id; checks GetBehaviorClass==requested0x26; assigns shared output only on success. Missing, wrong class or null result VERIFY and returnfalse. Hiccup caller initializes output null and **does not test this return**. | Lookup before feature/unlock | Lookup→class→assignment | Later sequence access can dereference null if shipped registry violates this precondition. No fabricated graceful skip | BehaviorManager.FindBehaviorByIDAndDowncast / Hiccup strategy |
| R12 | 0x0082FA96..AC6 | RandIntInRange(min,max): signed32 span=(1-min)+max; vcvt.f64.s32; GetNextDbl; vmul.f64; vcvt.s32.f64 truncation; signed32 add min. | Configured integer bounds | One GetNextDbl per draw | Preserve conversion/order and wrap semantics; no replacement modulo or extra RNG draw | Animation/EngineRandom.RandIntInRange |
| R13 | 0x0082F9B0..A12; 0x0082FA28..46 | GetNextDbl calls MT operator twice (first low word, second high), converts each unsigned word directly to binary64, computes high*2^32 + low, multiplies2^-64, then multiplies stored range width and adds lower bound. RandDbl(bound) separately multiplies that result by bound. | Shared RNG interface | Two MT draws; no clamp seen | Double literals 0x41F0000000000000 at0x0082FA18, 0x3BF0000000000000 at0x0082FA20. Rounding can differ from an integer-combine then conversion; preserve separate operations | EngineRandom.GetNextDbl/RandDbl |

The feature gate (id6), unlock component (configured DroneModeGame) and NeedsManager calls are strategy boundaries. Their implementations are not replaced with inferred truth values. The local ShouldTrigger branches and state writes are recovered above, including the ignored downcast-return precondition.
