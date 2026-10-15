| Requested question | Address or UNKNOWN | Coverage |
| --- | --- | --- |
| ActivateSpark tag/payload, app request/cancel | 0x00757304..30,0x007577C8..0x00757818,0x00757A68..B4; Unity Robot.cs:1682..1704 | Outer tag150 u16, inner tag0 u8, unlock4 bytes; cancellation Count 0x55. |
| Subscription/dispatcher and every HandleMessage case | 0x005A15AC..CE ->vptr1025194 invoke5A5137 ->5A3C6C..0x005A3E34 | All four cases, invalidtag and hard cancel latch read. |
| SetRequestedSpark and all +58..+65 writers | ctor5A08F4..900; setter5A3EB2/B6; case5A3C9A; switch5A4220/24/26; **additional writer5A4FDC/DE** | Direct owner-qualified writer set below. Whole-program arbitrary alias-store exclusion UNKNOWN. |
| SwitchToRequestedSpark/order/logs/game replies | 0x005A4198..422C; Freeplay 5ADC44..DE6A,5AE29C..E762; Sparked5B1518..5B20D6 | Switch is delayed to successful activity change; replies/state sequence below. |
| Timeouts and end conditions | 0x005B18D4..1A16,0x005B1F94..2066,0x005B5444..54DA | f32 thresholds/repetition/reaction/cancel gates read; local state machine mapped. |

# OV67 — ActivateSpark, cancellation and sparked lifecycle

Answers `re-analysis/research/requests/overnight/20261015-ov67-activatespark.md`.
Current M15-006 is **IMPLEMENTATION_GAP**, **Freeplay activity selection and
selected/deselected lifecycle**. Its unresolved text says the inbound ActivateSpark
and requested-activity handlers are unbuilt, leaving SetRequestedSpark/RequestNewActivity
without production callers, and records the operator's 2026-10-15 approval to expose
ActivateSpark as an app input. It also records unbuilt driving/idle/public-state
handoffs. Those are implementation claims, not contradicted merely because native
callers exist. This research edits no code, manifest, inventory or PROJECT_STATE.

Primary binary `resources/lib/armeabi-v7a/libcozmoEngine.so`,3.4.0-1204,
SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
Capstone Thumb skipdata. Handler, setter, switch, subscriptions and the activity
ranges cited below were read completely; tables/pools are data. Native evidence
precedes Unity/CLAD evidence. IMPLEMENTATION_GAP denotes a buildable candidate row,
not independent approval of the whole production path.

B=BehaviorManager; A=ActivitySparked; F=ActivityFreeplay. All offsets hexadecimal.
Unlock IDs are full 32-bit values, not an 8-bit enum or bit flags for this path.
**Count=0x55** is the no-spark sentinel; **Invalid=0** is different. TrackLaser=0x40,
BuildPyramid=3, RollCube=9, PeekABoo=0x25, CozmoSings=0x41. Preserve the sent value;
the ActivateSpark handler does not validate an unlock or charge inventory.

## Inbound path and every manager case

| Step | Original behavior | Citation | Classification / owner |
| --- | --- | --- | --- |
| Wire envelope | MessageGameToEngine tag **150=0x96**, serialized u16 little-endian, then BehaviorManagerMessage (no extra wrapper field), inner union tag **0** u8, then UnlockId i32 little-endian, 4 bytes. Total 7 bytes before outer transport framing. TrackLaser example `96 00 00 40 00 00 00`; cancel `96 00 00 55 00 00 00`. Native union has byte tag at+0/payload+4 due memory alignment; that is not wire padding. | native ActivateSpark Pack0x00757304..30; union Unpack0x007577C8..0x00757818 and Pack0x00757A68..B4; Unity MessageGameToEngine.cs:163,8248..50; BehaviorManagerMessage.cs Pack; ActivateSpark.cs Pack | M15-006 / IMPLEMENTATION_GAP; framing handed to M2 |
| Subscription | InitializeEventHandlers obtains external interface, subscribes via vt+2C to outer tag0x96. Function object captures B, vptr **0x01025194**, invoke+18=Thumb**5A5137**. Subscription shared handle retained in B+6C vector, using append or slow path; temporary handles/functions destroyed after retention. | full 0x005A1314..0x005A1629; specific tag/call0x005A15AC..CE; raw10251AC | M15-006 / IMPLEMENTATION_GAP |
| Event adapter | Load B from capture+4, Get_BehaviorManagerMessage(event+10), tail-call HandleMessage. Getter returns native wrapper whose union is its first member. Handler uses inner byte, not outer tag. | 0x005A5136..0x005A514D ->veneer8CBCFC;0x0074DB6E; handler0x005A3C70 | M15-006 / IMPLEMENTATION_GAP; generic signal dispatch recipient boundary |
| Case0 ActivateSpark | Getter returns union+4. Load unlock32; SetRequestedSpark(unlock,**false**). Reload payload; iff==0x55 writeB+65=1. Any other value leaves65 **unchanged**, even if a preceding cancel set it. No immediate switch/reply. | 0x005A3C80..9E; getter0x0075791C..1E | M15-006 / IMPLEMENTATION_GAP |
| Case1 SparkUnlocked | Get payload unlock32; tail-call SetRequestedSpark(unlock,**true**). Does not set/clear 65. | 0x005A3DB4..C9 ->8CBCAC | M15-006 / IMPLEMENTATION_GAP |
| Case2 DoATrickRequest | RequestATrick(robot AI+24); GetSparkCosts(SparkableThings**1**, fallback count0) returns shipped table value**3**; AddInventoryAmount(type**1**, signed negated cost) unconditionally, even if selector chose nothing. If B+60!=**0** emit `meta.spark_random_trick` with text EnumToString(requested), `$data` unsigned decimal cost. Note condition!=Invalid 0, not!=Count 0x55. No call-result test/refund here. | 0x005A3CA0..0x005A3D5A; event5A3D26; cost0x0077E0CC..DE,table0x00C91424=3 | M15-006 / IMPLEMENTATION_GAP; inventory recipient handoff |
| Case3 PlayAGameRequest | If B+30 game-requestbehavior is null **or** its running byte+A1!=0, set B+38=1. Existing non-running request suppresses new latch. No spark-field write. | 0x005A3DCA..DA | M15-006 / IMPLEMENTATION_GAP |
| Other inner tags | Error key `BehaviorManager.HandleEvent.UnhandledMessageUnionTag`, format `Unexpected tag %u '%s'`, promoted tagbyte and tag-to-string, empty fields, no channel; set global error byte to 1, debug-break only if global break flag. Return otherwise. | call0x005A3D74; globaltail0x005A3D9A..AE | M15-006 / IMPLEMENTATION_GAP |
| Requested setter | Store lowbyte bool input to64 first, then unlock32 to60. Info afterward, channel Behaviors,key `BehaviorManager.SetRequestedSpark`, format `requested %s spark is '%s'`; `soft` if input!=0 else `hard`, unlock name; empty fields. No 65 change/time write/message here. | full 0x005A3EAC..0x005A3F0E; log5A3EE6 | M15-006 / IMPLEMENTATION_GAP |

RequestATrick0x0056E6D0..0x0056E838 builds a temporary vector in source order from
configured (weight,unlock) pairs whose ProgressionUnlock.IsUnlocked(unlock,false)
returns exactly 1. Accumulate signed 32-bit weights with native wrap. None ->selector+C=0,
no setter. One -> store its ID, only ID!=0 calls SetRequestedSpark(id,false). Multiple
->repeated Robot RNG RandInt(totalWeight), choose first cumulative threshold
strictly greater than draw, retry candidate0 or previous ID(selector+C). The loop
counter checks `(counter>>3)<=0x7C` before retry; overflow Error `LoopBoundOverflow`,
`%s:%d`, args `../../../../engine/aiComponent/doATrickSelector.cpp`,74 at56E7F2,
then global error / debug break and proceeds with current candidate. Setter is at56E766.
Exact RandInt internals/configured weight producers remain their own recipient
boundaries; do not replace this with uniform selection or unlimited avoid-repeat.

## Spark fields and delayed switching

| Field | Initial write | Every identified owner-qualified later write |
| --- | --- | --- |
| B+58 activeID u32 |55 at5A08F4 | requested60 copy5A4224; **55 at5A4FDE** when leaving high-level Freeplay |
| B+5C activeSoft u8 |0 at5A08FA | requested64 copy5A4226 |
| B+60 requestedID u32 |55 at5A08F6 | setter5A3EB6; **55 at5A4FDC** when leaving Freeplay |
| B+64 requestedSoft u8 |0 at5A08FC | setter5A3EB2 |
| B+65 cancel/reselect latch u8 |0 at5A0900 |1 only case 0 payload55 at5A3C9A;0 at switch5A4220 |

Setter callers identified: inner case 0 false / case 1 true, RequestATrick56E766false, and
CompleteSparkLogic5B1C0A(55,false). Search of decompilation plus direct native bodies
does not prove exclusion of arbitrary alias/memcpy stores throughout the binary;
that whole-program exclusion remains UNKNOWN. No writes to padding59..5B/5D..5F
were identified. **OV60's writer list omitted 5A4FDC/DE**; its list must not be
treated as exhaustive. This corrects that research note, not an approved manifest.

ActivateHighLevelActivity adapter0x005A4FA2..0x005A4FE0 calls SetCurrentActivity(value,false)
first. If value!=1, clears 60 and 58 to 55 directly, preserving 5C/64/65. If value==1,
only if f32 B+54 ordered negative, stamp now there. It does not callSetRequestedSpark,
so no setter Info. This message is another way app mode changes cancel spark context.

SwitchToRequestedSpark0x005A4198..0x005A422C emits Info **before state writes**,
channel Behaviors,key `BehaviorManager.SwitchToRequestedSpark`, format
`switching active spark from '%s' -> '%s'. From %s -> %s`; args active/requested
unlock names, active/requested soft-or-hard,empty fields, call5A41E8. Then read f32 clock,
store B+50,clear 65,copy60->58,64->5C. It does not clear requested60.

Freeplay picker0x005ADC44..0x005ADE6A finds candidate vector by requested unlock inF+6C,
in vector order. Debug force F+91 wins over request F+90; either matches candidate activity ID.
Otherwise different candidate requires WantsToStart==1; current candidate retained
only if allowReselect==1 and WantsToEnd==0. No candidate ->null. On pointer change:
event thenInfo, old OnDeselected, storeF+80, if new nonnull, set public activity ID then
new OnSelected; return 1. Same pointer returns 0. Missing vector warns and returns 0;
there is no automatic fallback to a different unlock in this function.

Freeplay desired behavior0x005AE29C..0x005AE762: debug force / request change, active55
with requested!=55, or null current activity enters pick; otherwise current behavior /
strategy gates decide whether to repick. When active==requested and 65!=0, log
SparkReselected and allowReselect=0, forcingcurrent candidateoff eligibility. Only
PickNewActivityForSpark return**1** triggers SwitchToRequestedSpark and pause flag
SetFreeplayPauseFlag(desired!=55,1). Thus subactivity OnSelected can run **before**
B+58 is updated and correctly reads B+60/B+64 for its start reply. Null-pick and
same pointer paths must not blindly acknowledge a new active spark. F+90 is cleared
at the normal pick tail. Error if both old/new activity null; no hidden success fallback.

## Sparked state, timeout and game replies

| Step | Original | Citation / classification |
| --- | --- | --- |
| Configuration | minTimeSecs f32->A+90, maxTimeSecs f32->94, numberOfRepetitions ParseUint8 zero extended->9C, optional bool maxTimeoutForActionComplete->98 (constructor default true), objective enum->A0. Success/fail triggersA8/AC default216/210; empty string->23F. Soft start triggerA4 default23F. |0x005B0EAC..0x005B11A6 normal body; stores5B104C/107E/10B0/10DE/1132; IMPLEMENTATION_GAP |
| Selection | Stamp f32 now A+88,zero 8C/objective count, state68=0,B0=0,B4/B5=0; clear observed cube-ID set. If requestedID==55 or requestedSoft==0 (hard): push driving descriptor C624B0 with activity name, SmartPushIdle213, start backpack lights, B5=1, send HardSparkStartedByEngine(requestedID). Then reaction lockC624BC; optional delegate subactivity OnSelected; if requested ID 25, call PeekABooSparkStarted(max94). |full 0x005B1518..16F4; start replywrapper5B171C..175C; IMPLEMENTATION_GAP |
| Objective input | If incoming objective u32 equalsA+A0, incrementA+8C u32; no success bool test in this leaf. RobotObservedObject types 1 or 2 add ObjectID payload+C to observed set A+B8. |0x005B181E..32,0x005B1834..1866; IMPLEMENTATION_GAP |
| Update cadence | CurrentBehaviorTriggeredAsReaction result inverted to flag. State 0/1 plus reaction ->state2. State 2 or 3 ->CheckIfSparkShouldEnd. State 3 with reaction ->CompleteSparkLogic,ResetLightsAndAnimations,state5. Always forward Update to optional delegate subactivity. |0x005B186C..18D0; IMPLEMENTATION_GAP |
| Repetition-end predicate | Read clock once. f32 dmin=f32(f32(start88+min90)+**B727C5AC**[-1e-5]). If ordered dmin<=now and target9C!=0 and signed count8C>=signed target, repetition done=true. |0x005B18EE..1926; IMPLEMENTATION_GAP |
| Wait-for-action max predicate | Only if98!=0: f32 dmax=f32(f32(start88+max94)-eps). If ordered dmax<=now and current behavior nonnull and behavior+70 !=activeID58, timeout predicate=true. Otherwise false. |0x005B1926..1956; IMPLEMENTATION_GAP |
| Enterend | Only state 2 and(repetition done ORtimeout predicate OR cancel65!=0): reset lights/animations; RequestCurrentBehaviorEndOnNextActionComplete; state3; disable reactionswith lock name `finalAnimLockReactions` and array C624E6. |0x005B1956..19AE; IMPLEMENTATION_GAP |
| Newrequest interrupts | Else, if requested60!=55 and(active58!=requested60 OR(active58!=55 and activeSoft5C!=0 and requestedSoft64==0)): RequestCurrentBehaviorEndImmediately(reason`Sparks transition soft spark to soft spark`), state4,B4=1. Preserve literal reason despite soft->hard predicate. |0x005B19B0..1A16; IMPLEMENTATION_GAP |
| Hard max without wait | SelectNextSparkInternalBehavior first gets delegate/base chooser result, thenclock. If98==0 andordered f32(f32(start88+max94)-eps)<=now andold behavior pointer !=new chosen pointer: replace chosen withA+E0 behavior,state3. It does not forcibly replace the same current pointer mid-action. |full 0x005B1F94..2066; IMPLEMENTATION_GAP |
| Get-in/internal | State 0 chooses softA4 when active!=55and soft, else211; SetAnimationTrigger(...,1),return animation behavior A+78,state1. State 1 keeps animation while current running; after finish, state 2 / select internal. State 2 selects internal. |0x005B1D18..1DB8,1E26..46; IMPLEMENTATION_GAP |
| Get-out | State 3: running current whose class byte 64!=39 continues internal chooser. Else if active is soft OR cancel65!=0 set trigger 23F. Hard normal path: count<target adds fail AC iff!=23F andactive!=41; count>=target add success A8 iff!=23F then Mood event`SuccessfulSpark` at current mood time. Append212; SetAnimationTriggers(vector,1); return animation,state4. State 4 keeps animation while current running; after finish, CompleteSparkLogic and return null. State 5/default null. |full 0x005B1D18..1F2A; IMPLEMENTATION_GAP |
| Completion result | success=(target9C==0 ORsignedcount8C>=signed target). Event key cancel if65!=0 orB4!=0; else timeout if !success, else success. On noncancel failure with observed set size C0!=0 emit optional  fail_cubes_observed when activehard/55, then key fail. Alwaysemitresult eventwithactiveunlock nameand$data=`soft`only active!=55and5C!=0else `hard`. |full 0x005B1A90..1C2E; event5B1BC8; IMPLEMENTATION_GAP |
| Completion clearing/reply | Only if 65==0 and B4==0: if active58==requested60 callSetRequestedSpark(55,false),then reload active. Only active55 or hard sends HardSparkEndedByEngine(successu8). Canceled/switched paths do not clear another pending request and do not send this reply here. |0x005B1BEE..1C2E; setter5B1C0A; reply5B1C28; IMPLEMENTATION_GAP |
| Reply layout/order | Started outer E2G tag131(0x83), unlock 4 bytes; Ended tag132(0x84), success 1 byte. Wrappers construct, external interfacevt+1C broadcast,clear. Started occurs after light start and before selection reaction lock. Ended occurs after event and possibly setter Info. |0x005B171C..175C,0x005B2094..20D6; UnityMessageEngineToGame.cs:145..46/HardSpark*classes; IMPLEMENTATION_GAP |

GenericIActivityStrategy.WantsToEnd0x005B5444..54DA uses f32 eps **3727C5AC**. If min14>eps
ordered, read clock and return false when f32(f32(min14+selectedtime)-eps)>now. If max18>eps,
read clock again, return true when f32(f32(max18+selectedtime)+eps)<now ordered. Else vt+C.
Spark strategy constructor 5B4E90..AA overwrites max18 with FLT_MAX **7F7FFFFF**; its
WantsToStartInternal returns 1(5B4EBE),WantsToEndInternal returns 0(5B4EC2). This
strategy wrapper max is distinct from ActivitySparked'smax94; do notconflate them.
Shipped SparksTrackLaser activity has min 0, max 30, wait false, repetitions 1, objective LaserTracked.

## Additional local log contracts

All channeled entries below are Info / channel **Behaviors**,empty fields. Native address
is the call. Event functions are event level; Warning/Error are unchanneled.

| Call / key | Format; args |
| --- | --- |
|5ADCF2 Warning `ActivityFreeplay.PickNewActivityForSpark.NoActivityAvailableForSpark`|`No activity available for spark '%s'`; desired unlock name|
|5ADD72 Event `AIGoalEvaluator.NewGoalSelected`|`Switched goal from '%s' to '%s' (spark '%s')`; old/new activity name(or`no activity`),desired unlock|
|5ADDD4 `ActivityFreeplay.PickNewActivityForSpark.Switched`|`Switched activity from '%s' to '%s'`; old/new names|
|5ADE40 Warning `ActivityFreeplay.PickNewActivityForSpark.NoActivitySelected`|`None of the activities available for spark '%s' wants to run.`; desired unlock|
|5ADD48 `ActivityFreeplay.PickNewActivityForSpark`|`Current activity '%s' is still the best one to run for spark '%s', %s`; current name,desired unlock,`forced from debug`iffF+91==currentIDelse `(not debug forced)`|
|5AE2EE `ActivityFreeplay.ChooseNextBehavior.DebugForcedChange`|`Picking new activity because debug is forcing '%s'`; forced activity name|
|5AE332 `ActivityFreeplay.ChooseNextBehavior.RequestedActivity`|`Picking new activity because '%s' was requested`; requested activity name|
|5AE3DC `ActivityFreeplay.ChooseNextBehavior.NullOrChangeDueToSpark`|`Picking new activity to match spark '%s'`; requested unlock|
|5AE3AE `ActivityFreeplay.ChooseNextBehavior.BehaviorAndActivityEnded`|`Picking new activity because '%s' wants to end, and behavior finished`; current activity name|
|5AE438 `ActivityFreeplay.ChooseNextBehavior.SparkReselected`|`Spark re-selected: none behavior will be selected`;no args|
|5AE4BC Warning `ActivityFreeplay.ChooseNextBehavior.ActivityNotDesiredSpark`|`The selected activity's required spark '%s' does not match the desired '%s'`; selected required unlock,desired unlock|
|5AE57C `ActivityFreeplay.ChooseNextBehavior.NewActivityDidNotChooseBehavior`|`The new activity '%s' picked no behavior. It probably didn't cover the same conditions as the behaviors.`;new name. Native predicate 5AE546..54E is **result!=null AND pickchanged==1** despite wording; preserve instruction predicate.|
|5AE5B8 `ActivityFreeplay.NoActivitySelected`|`Picked no activity`;no args|
|5AE614 Error `ActivityFreeplay.NoActivityAvailableError `|`There was no activity, and no activity was selected`;no args;global error / debug breaktail|
|5AE6DC `ActivityFreeplay.ChooseNextBehavior.NewBehaviorChosenWhileRunning`|`Picking new activity because '%s' wants to end, and behavior finished`;current name|
|5AE736 `ActivityFreeplay.ChooseNextBehavior.NoBehaviorChosenWhileRunning`|`Picking new activity because '%s' chose behavior '%s'. This activity is not allowed to be repicked.`; current name,`(null)`|
|5B1B42 Event `meta.upgrade_replay_fail_cubes_observed`|text active unlock name;$dataunsigned decimal observed set size(the C0 value loaded before to_string)|
|5B1BC8 Event result key|`meta.upgrade_replay_cancel`, `_success`, `_timeout`, or`_fail` as branches above;text active unlock name;$data`soft`/`hard`|

## What the shipped Unity app supplies

Authority2 `unity/scripts/csharp/Robot.cs:1682..1704`: EnableSparkUnlock(id) first
sets its local IsSparked=(id!=Count),SparkUnlockId=id, sends ActivateSpark(id).
StopSparkUnlock callsEnableSparkUnlock(Count), not a separate Cancel message.
SparkEnded clears local state without sending there. `UnlockId.cs` names Count 0x55,
Invalid 0,TrackLaser 0x40. `SparksDetailModal.cs:256..286` sends selected unlock only if
robot exists and AppSparkableData==null, after app inventory/UI cleanup. Otherwise it
runs AppSparkableData/CodeLabCSharpCreator, which is outside SDK/CodeLab scope.
StopSparkTrick at`:359..370` sends Count only if doEngineCleanup and robot.IsSparked.
Its HardSparkEnded handler updates stats/refund/UI and then calls StopSparkTrick; engine
does not inherit the screen/inventory policy. `CozmoSingsDetailModal.cs:76..87` stops
for charger resume then resends selected ID after DriveOffCharger transition.

`Onboarding/OnboardingDoTrickStage.cs:68..83` sendsActivateHighLevelActivity(Freeplay),
EnableCubeSleep(false),ActivateSpark(RollCube9),then music state;SkipPressed sends Count.
GamesView/SparksView random trick buttons call DoRandomSpark (inner2,empty payload);
random game buttons RequestRandomGame(inner3,empty). Robot.cs:1670..1680contain those
send methods. Search found no non-generated Unity SparkUnlocked(inner1) sender; native
handling is defined, but its actual Unity UI production trigger is **UNKNOWN** in this pass.
Music state is a separate outer tag 163; it does not by itself request or cancel a spark.

ActivateSpark is the already approved **COMPATIBILITY_POLICY app input**. The C# API
supplies unlock ID; engine behavior after receipt stays exact. Normal connected Freeplay can
reach this path; not SDK-only. A CodeLabGame24 payload still does not authorize SDK support.

## Build rows, contradictions and remaining uncertainty

Build rows: outer 96 subscription/adapter; inner0..3/default; sentinel 55 vs Invalid 0;
write order and sticky 65; hard/soft setter/switch; high-level mode clear writer; delayed
pick commit and start reply using requested fields; timeout/repetition/action boundary;
completion events / pending request preservation / ended reply; Unity request/cancel policy.
Keep action, inventory, progression, signal transport and light recipients owned by their
records; these ordered call boundaries are not evidence every recipient is built.

No current manifest record contradiction established. M15-006's quoted unbuilt-handler
claim remains an implementation gap. **Correction to the OV60 report:** its identified
writer list missed ActivateHighLevelActivity 5A4FDC/DE. Manager must include this before
claiming all writers. New records: check ownership of ActivateSpark app input policy,
high-level mode clear and Sparked start/end message path; add missing ownership before whole
settlement, without assigning IDs by inference here.

Unread boundaries: generic external interface Subscribe/Broadcast recipient and signal
invocation implementation; outer transport framing/parsing; all arbitrary alias writers;
DoATrick configured weight producer / ProgressionUnlock / RandInt and Inventory mutation;
ActivitySparked::ResetLightsAndAnimations **5B17A2..5B181D** and Smart reaction/drive/idle
callee bodies; full requested-activity callback (**UNKNOWN**, not traced here); full Activity chooser and
specific behavior action bodies; source for particular Unity SparkUnlocked trigger.
ReloadFromConfig normal body was read through 5B11A6;exception cleanup 5B11AA..5B1255 was
not read, so no malformed-config fatal-path closure is claimed. The main explicit request
ranges handler 5A3C6C..3E34,setter5A3EAC..3F0E,switch5A4198..422C,subscriptions5A1314..1629,
Freeplay 5ADC44..DE6A/5AE29C..E762 and Sparked state/end ranges above were fully read.

M1-034: unchecked dependency loads/calls include AI selector 5A3CA2/CA6,Inventory 5A3CC6 and
missing animation behavior 5B1D4A/1E2A; actual malformed construction reachability is not
claimed normal. Apply the standing visible smallest-unit skip only for original terminating/
undefined paths, not for invalid-but-defined unlock values which correctly warn/no-pick.

Suggested regressions: cancel then new request before a switch (sticky 65); Count vs Invalid;
hard->soft/soft->hard/same ID; leaving Freeplay preserves flags but clears IDs; no available
activity does not commit active; OnSelected start reply before Switch; random cost charged
even with empty selection; f32 min/max epsilon and equality; wait false acts at behavior change;
cancel vs normal reply suppression; new pending request survives completion. Research only:
no implementation/hardware tests; native instruction review and whitespace check.
