| Requested coverage | Status | Finding / limit |
|---|---|---|
| Pull first; research lane only | CHECKED | Fast-forward main 91ff5f9 → f626180; writes restricted to research. |
| Streamer construction and actual ProceduralLive selection | CHECKED | Constructor makes a live object but pushes Count; Update selects the embedded live object when top trigger is 0x198. |
| Native push callers: components, activities, behaviors | CHECKED | All recovered direct/PLT/veneer call sites listed below, including configurable arguments. |
| Messages and available Unity push callers | CHECKED | PushIdleAnimation message forwards arbitrary trigger; Unity wrapper and all textual call sites checked. |
| Identify a fixed automatic production producer of 0x198 | PARTIAL | None established in inspected native callers, Unity source or extracted JSON; runtime configurable/message inputs remain UNKNOWN. This is not proof that production never selects it. |
| SetStreamingAnimation callers and distinction from idle selection | CHECKED | Includes name-based message callback discovered through a veneer; none establishes an embedded-live producer. |
| DesiredFaceDistortion ownership and every recovered output writer | CHECKED | Constructor, per-new-tick reset, successful sample; no external degree setter recovered. |
| Distortion values, config, gates, cache, RNG order and float widths | CHECKED | Repair graph, float32 bounds, degree draw before cooldown draw; negative sentinel and per-tick cache. |
| TrackLayerComponent::Update at caller 0x0057CF7A | CHECKED | Actual function 0x0064EDE8 passes sampled degree to AddGlitch, subject to epsilon. |
| M7-005/007/008/009/010/016/017 current-record comparison | CHECKED | Current title/status/evidence quoted before conclusions; no manifest changes. |

# Procedural live producers and desired face distortion — independent extraction

Answers the operator's 2026-10-04 request in this chat; no separate request file was supplied. Research report only, for manager verification. PARTIAL above is an explicit unresolved upstream input, not a recovered automatic pusher. No production behavior, inventory, approval, manifest, test or project-state change is made here.

Primary binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`, SHA256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`, 17,139,336 bytes. Addresses below are virtual addresses in this binary, Thumb state bit removed. Native instructions are retained in [native companion](20261004-procedural-live-native.txt); [xref companion](20261004-procedural-live-xrefs.json) lists 29 decoded call sites after resolving Thumb-to-ARM veneers. Ghidra C files are navigation aids, not the authority. Floats use IEEE-754 bit patterns; hexadecimal triggers are integers, not floats.

## Current manifest quotations — before comparison

These are verbatim current fields from `re-analysis/fidelity_manifest.json` after the pull. They are not recalled descriptions or proposed classifications.

### M7-005

> **title**: Blink is a fixed seven-frame squash table emitted as an eight-keyframe track

> **status**: IMPLEMENTATION_GAP

> **authority**: libcozmoEngine.so 3.4.0-1204

> **evidence**: ["ProceduralFaceDrawer::GetNextBlinkFrame 0x00585f18", "blink table 0x00c5aad8 (7 x 16 bytes: scale-x, scale-y, duration ms, action)", "action byte list 0x00c5ab48 (LowerLidY, LowerLidBend, LowerLidAngle, UpperLidY, UpperLidBend, UpperLidAngle)", "restore frame duration 33 at 0x00586190; FaceLayerManager::GenerateBlink 0x0058d2ac", "ProceduralFace::Combine 0x005846a8"]

> **unresolved**: Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. TrackLayerComponent::Update reads DesiredFaceDistortion every tick in the keep-alive (0x57CF7A; AnimationScheduler.cs:905); unset, the C# silently uses 0 (TrackLayers.cs:609-612). Before: built, awaiting strong verification: the blink table (0xC5AAD8), GenerateBlink, the scanline toggle (0x5861c4), the 7500..30000 spacing fallback and per-frame layer application are the streamer port's (M5), reached through AnimationScheduler.Advance with ProceduralLive on top; IdleBehavior's own generator is retired. Still open: the DesiredFaceDistortion glitch source (TrackLayerComponent::Update, 0x57cf7a) has no component here; see M7-017 for the pusher.

### M7-007

> **title**: The eye dart interpolates to a persistent gaze and does not fade to centre

> **status**: IMPLEMENTATION_GAP

> **authority**: libcozmoEngine.so 3.4.0-1204

> **evidence**: ["ITrackLayerManager::ApplyLayersToFrame 0x0058e644", "AddToPersistentLayer 0x0058eaa0", "FaceLayerManager::GetFaceHelper 0x0058cd80", "KeepFaceAlive persistent dart call 0x0058d3e2"]

> **unresolved**: Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. runs inside the keep-alive layering with DesiredFaceDistortion defaulted; defers to unsettled M7-017. Before: built, awaiting strong verification: the first dart shows nothing until its drawn duration then snaps; layer clock +33 per streamed frame, frozen while held (0x58cd9e, 0x58ce22, 0x58d220, 0x58e688, 0x58e71a..0x58e724); tests drive the streamer. Still open: none known beyond M7-017.

### M7-008

> **title**: Idle timers are integer milliseconds advanced by the engine's 60 ms tick

> **status**: IMPLEMENTATION_GAP

> **authority**: libcozmoEngine.so 3.4.0-1204

> **evidence**: ["CozmoInstanceRunner::Run 0x0065b3a8 uses 0x03938700 ns", "UpdateLiveAnimation decrements at 0x0057d650/0x0057d68a/0x0057d6ba", "KeepFaceAlive decrements at 0x0058d388", "idle clock increments at 0x0057d444"]

> **unresolved**: Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. the streamer's error-flag byte (strb 0x57D0DC) is not modelled; the clock is Environment.TickCount64, not BaseStationTimer's float seconds; nothing pushes ProceduralLive in production, so UpdateLiveAnimation never runs (a RECOVERABLE_GAP with no record). Before: built, awaiting strong verification: UpdateLiveAnimation gates and decrements per the check (flag +0x194, picking/placing and +0x44 < GetParam<int>(2) return with no decrement; movement flag, locked tracks, countdown and (lift) carrying decrement by 60), the 60 ms tick, float32 head angle with the s8 wrap, the lazy default-set in GetParam/SetParam, carrying wired at the call site from Motion.IsCarryingObject (set only when a ManipulationSystem is built). Still open: the engine's global error-flag byte at 0x57d0c8..0x57d0e6 is not modelled; the production clock is Environment.TickCount64, not BaseStationTimer's origin, so float32 quantisation differs after long uptime.

### M7-009

> **title**: Idle head and lift are keyframes of the live animation

> **status**: IMPLEMENTATION_GAP

> **authority**: libcozmoEngine.so 3.4.0-1204

> **evidence**: ["head keyframe 0x0057d85c/append 0x0057d866; lift keyframe 0x0057d9c0/append 0x0057d9ca", "head current angle Robot+0x2fc and degrees constant 0x0057db2c = 0x42652ee1 (57.295780); vcvt.s32.f32 truncation 0x0057d84e", "idle params 13/14 lift mean 35.0/variability 8.0, 9/10 duration 50..500, 11/12 gap 250..2000; 19 head variability 6.0, 15/16 duration 50..500, 17/18 gap 250..1000", "HeadAngleKeyFrame ctor 0x004f8be4 / GetStreamMessage 0x004f8c08 (animHeadAngle 0x93)", "LiftHeightKeyFrame ctor 0x004f8f5c / GetStreamMessage 0x004f8f80 (animLiftHeight 0x94)"]

> **unresolved**: Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. reachable only through UpdateLiveAnimation; defers to M7-008 and M7-017. Before: built, awaiting strong verification: idle head and lift keyframes (multiplier 0x42652ee1, truncation, 35/8/6, parameter indices, order body, lift, head) through the streamer port with the engine's gates. Still open: none known beyond M7-008.

### M7-010

> **title**: Idle body shuffle and its paired turn eye shift

> **status**: IMPLEMENTATION_GAP

> **authority**: AnimationStreamer::UpdateLiveAnimation 0x0057d5f8, read

> **evidence**: ["body decision and draws 0x0057d6cc..0x0057d8f4", "straight fraction 0.5 (param 8) 0x0057dbba; speed RandIntInRange(-10,10) 0x0057d6f0; duration 250..1500 0x0057d6ce; gap 100..1000 0x0057d95a", "BodyMotionKeyFrame 0x004fb170 radius 0x7fff straight / 0 turn; GetStreamMessage 0x004fba8c (animBodyMotion 0x99)", "turn eye layer 0x0057d7fc; x sign(speed)*RandIntInRange(0,21), y RandIntInRange(-10,10), duration 33 ms, 64/32/1.1/0.85/0.1", "straight removal 0x0057d8d2"]

> **unresolved**: Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. reachable only through UpdateLiveAnimation; defers to M7-008 and M7-017. Before: built, awaiting strong verification: body shuffle and the persistent 'LiveIdleTurn' eye shift (AddOrUpdateEyeShift 0x64f3c8, removed by a straight shuffle 0x57d8d2); the dart gate holds while any other layer exists (0x58d3ac..0x58d3c4); the 64/32 arguments are discarded by GenerateEyeShift. Still open: none known beyond M7-008.

### M7-016

> **title**: The idle face composes a stack of named persistent and transient layers

> **status**: IMPLEMENTATION_GAP

> **authority**: libcozmoEngine.so 3.4.0-1204

> **evidence**: ["ITrackLayerManager::ApplyLayersToFrame 0x0058e644", "ProceduralFace::Combine 0x005846a8", "KeepFaceAlive dart 0x0058d3e2 and blink 0x0058d4be"]

> **unresolved**: Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. the keep-alive gates hold (0x57CF6A..0x57CFF2), but the composition runs with DesiredFaceDistortion defaulted and defers to M7-017. Before: built, awaiting strong verification: the idle face is composed per streamed frame by the streamer port (ApplyFaceLayersToAnim), the keep-alive gates are +0x88 > 0, +0x38 == 0 and the idle/timeout gate in float32; the streamer's own generator is the only one. Still open: none known beyond M7-017.

### M7-017

> **title**: The exact live-animation wire lifecycle used by idle behavior

> **status**: IMPLEMENTATION_GAP

> **authority**: libcozmoEngine.so 3.4.0-1204

> **evidence**: ["AnimationStreamer::UpdateLiveAnimation 0x0057d5f8", "Update 0x0057ce5c", "InitStream 0x0057b674", "UpdateStream 0x0057c84c", "SendStartOfAnimation 0x0057c400", "SendBufferedMessages 0x0057bf60"]

> **unresolved**: Opus verification of R-BEH2 batches 1-2 (2026-10-03, re-analysis/research/20261003-R-BEH2-verify-1-2.md): NOT YET. ProceduralLive has no pusher (RECOVERABLE_GAP in prose, no record); DesiredFaceDistortion is MISSING; the engine logs AnimationStreamer.Update.LiveUpdateFailed "Failed updating live animation from current robot state." (0xBEF148/0xBEF172), dropped at AnimationScheduler.cs:995; the error-flag write (0x57D0C8..0x57D0E6) is not modelled. Before: built, awaiting strong verification: StreamLive no longer pushes ProceduralLive and the 'StreamLive' short-circuit is removed; the gated UpdateLiveAnimation port (M5-030) runs whenever ProceduralLive is on top; the S4 error path returns without the tail; the idle clock +0x44 is zeroed while streaming and advances 60 per idle tick. The engine has no pusher of 0x198 (RECOVERABLE_GAP, no record yet: production never reaches the live path with the default Count stack top; tools and tests push through PushIdleAnimation); DesiredFaceDistortion (S1 step 2) has no source (MISSING).

## Build rows: producer and selection path

Columns separate trigger/argument values, gates, sequence and failure behavior. “No failure result” means a void path or ordinary success in this row, not a subsystem-wide guarantee. UNKNOWN identifies evidence the inspected path does not establish.

| Step | Address / source | What it does and values | Gates | Order | Failure results / UNKNOWN |
|---|---|---|---|---|---|
| P01 | 0x00579F78; 0x0057A01E; 0x0057A060 | Construct embedded Animation at streamer+0xA8 named EnumToString(0x198), then SetIsLive(true). | Construction | Before initial stack append | This marks the object live; it does not request idle selection. |
| P02 | 0x0057A038..0x0057A0B2 | Live-enabled +0x194=false; countdown block +0x198 zeroed; idle timeout +0x1C0=0x3F000000 (0.5 s). Append initial Count=0x23F with default lock. | Construction | After live object construction | No automatic 0x198 push. Global 0x01051038 points to 0x00BEF4CF, the string default_anim_lock. |
| P03 | 0x0057B914..0x0057B9D7 | PushIdleAnimation appends (trigger, copied lock string), 16-byte element, to +0x48/+0x4C vector. Count clears idle+0x34 and initialized flag+0x64. Arbitrary 0x198 is accepted. | No trigger-specific exclusion of 0x198 | Append makes new top; selection waits for Update | Ordinary result 0; allocation exceptions not extracted. |
| P04 | 0x0057D03A..0x0057D080 | With no active stream, inspect back of idle stack. Empty/Count takes layers path. Top 0x198 sets +0x194=true at D074 and idle+0x34=this+0xA8 at D07C; calls UpdateLiveAnimation at D080. | Active stream +0x38 null; nonempty stack; top=0x198 | Flag, pointer, generator | This is the recovered direct writer selecting procedural live. It is not SetStreamingAnimation. |
| P05 | 0x0057D084..0x0057D0E6 | If generator fails, log LiveUpdateFailed, set _errG, optionally debug-break, return its result. | UpdateLiveAnimation !=0 | Before shared idle tail | Does not initialize or update stream on that failure tick. |
| P06 | 0x0057D3F0..0x0057D44C | Shared idle tail: InitStream(idle,0xFF) when pointer changed, +0x64 clear, or finished with no buffer; otherwise UpdateStream(idle,false), store current seconds at +0x88. Add integer 60 to idle clock+0x44. | Successful live generation, or ordinary idle path | Generation precedes stream tail | Detailed wire lifecycle is outside this producer extraction; no whole M7-017 settlement follows. |
| P07 | 0x0057CF6A..0x0057CFF2 | When last-stream seconds +0x88 > 0x00000000, TrackLayer Update at CF7A runs first. Then KeepFaceAlive requires no active stream and either idle==embedded live or idle null with float32 now-last > timeout. | +0x88>0; additional keep-alive gates only for KeepFaceAlive | Distortion read precedes no-active-stream test and KeepFaceAlive | Do not require top 0x198 for the distortion getter. It can run while a clip streams. |
| P08 | 0x0057BA60; 0x0057BD60 | RemoveIdleAnimation removes an owned stack entry; underlying top becomes visible. Neutral replay uses neutral pointer+0x40, not live. | Last/default entry protected; unknown lock refused; middle removal warns | Selection restored on subsequent Update | Exact removal matching machinery is existing M5 scope; no new implicit 0x198 push. |
| P09 | 0x0057A5C6; callback vtable 0x010238CC; 0x0057EC1A..0x0057EC32 | SetupHandlers subscribes tag 0xAE=174. Captured streamer, payload getter, trigger from payload+0, string from payload+4; tail-call PushIdleAnimation via 0x008CB8DC→0x004AD2F4. | External interface present; incoming PushIdleAnimation message | Forward payload unchanged, then normal stack/Update path | Can select 0x198. Which runtime sender actually requests it is UNKNOWN. |
| P10 | Unity `Robot.cs:1388..1393`; generated `PushIdleAnimation.cs:91..119`; `MessageGameToEngine.cs:187` | Wrapper prefixes lock with unity_, initializes message and sends. Payload order: Int32 trigger, UInt8 UTF-8 byte length, bytes; union tag 174. | Caller supplies trigger/lock | Message production before native P09 | No literal ProceduralLive at this wrapper. Runtime argument UNKNOWN. |

## Every recovered native push call site

| Step | Address | What / values | Gates and when | Order | Failure / UNKNOWN |
|---|---|---|---|---|---|
| C01 | 0x00573152 in SevereNeedsComponent::SetSevereNeedExpression (0x00572E48) | Push mapped trigger with severe_need_component_lock. Table 0x00C596C4: NeedId 1→0x137; NeedId 0→0x143. | Selected severe need has table entry; expression transition | Component finds trigger then pushes, before reaction lock work | Neither mapped value is 0x198; missing entry skips push. |
| C02 | 0x005ACC70 in ActivityFeeding::SetIdleForCurrentStage (0x005ACB70) | Stage 0→0xB3; 1→0xB4; 4→0xB8/0xB9; 7→0xB5/0xB6; other stage→Count/0xB7 (normal/severe). Stage >=14→Count. | Re-evaluated stage; same trigger already owned returns; severe state chooses alternate | Remove previous owned idle, push using activity name, store ownership/current trigger | No 0x198. |
| C03 | 0x005B3304 in IActivity::SmartPushIdleAnimation (0x005B32BC) | Forward supplied trigger, derived activity lock/name. | Activity ownership flag+0x50 clear | Push then ownership=true | Already owned verifies and returns; doesn't stack another. |
| C04 | 0x005B31AC..0x005B31BC in IActivity::OnSelected (0x005B312C) | Pass activity+0x44 trigger to C03. | On selection, configured trigger !=Count | After driving/status work, before derived selection | Could be 0x198 if supplied config sets it; actual such config UNKNOWN. |
| C05 | 0x005B2870; 0x005B2CBE..0x005B2D24 | Constructor default+0x44=Count. ReadConfig optional idleAnimTrigger string; nonempty converts with AnimationTriggerFromString and stores at 0x005B2D0E; empty stores Count at D024. | Activity config read | Config writer precedes OnSelected reader | No idleAnimTrigger or ProceduralLive occurrence in inspected extracted JSON; dynamically supplied value UNKNOWN. |
| C06 | 0x005B1630 in ActivitySparked::OnSelectedInternal (0x005B1518) | Forward fixed 0x213 to C03. | Robot behavior manager+0x60==0x55 OR byte+0x64==0 | Driving-animation push, idle push, backpack lights | No 0x198. |
| C07 | 0x005BE464 in IBehavior::SmartPushIdleAnimation (0x005BE41C) | Forward trigger with behavior-derived lock/name. | Ownership byte+0xB0 clear | Push then ownership=true | Already owned verifies and returns. |
| C08 | 0x005D7ED4 BehaviorCubeLiftWorkout::InitInternal | C07(Count=0x23F) | Behavior initialization | Init call path | Suppresses normal idle, not procedural selection. |
| C09 | 0x005EC13C BehaviorRequestGameSimple::IdleLoop (0x005EC114) | C07(configPerNumBlocks+0x10), selected through this+0x200. Config key idle_animName loads enum at 0x005EA030. | Enter IdleLoop with selected config | Read selected config then push | Parameterized; could accept 0x198. Shipped JSON names RequestGameSpeedTapIdle0/1, MemoryMatchIdle0/1, KeepAwayIdle0/1, not ProceduralLive. |
| C10 | 0x005F14C4 BehaviorBouncer::InitInternal | C07(Count) | Initialization | Init call path | No 0x198. |
| C11 | 0x005F1EF2 BehaviorFistBump::InitInternal | C07(Count) | Initialization | Init call path | No 0x198. |
| C12 | 0x005F679A BehaviorPeekABoo::InitInternal | C07(Count) | (!byte+0xD9 && !byte+0xD8) OR float+0x168 !=0xBF800000 | Init call path | No 0x198. |
| C13 | 0x005F845E BehaviorPounceOnMotion::InitHelper | C07(0x192) | !byte+0xD9 and !byte+0xD8 | Init helper branch | No 0x198. |
| C14 | 0x005FB442 BehaviorTrackLaser::TransitionToRespondToLaser | C07(0x104) | !byte+0xD9, !byte+0xD8, !byte+0x1AD | Response-state transition | No 0x198. |
| C15 | 0x00606CCA BehaviorReactToOnCharger::InitInternal | C07(Count) | Initialization | After callback setup | No 0x198. |
| C16 | 0x0057EC32 | External message callback P09 | PushIdleAnimation message arrives | Direct forward | Runtime trigger UNKNOWN; no fixed automatic producer established. |
| C17 | 0x0065E104 SdkStatus::EnterMode; 0x0065E2A4 ExitMode | Constructs outgoing-to-engine PushIdleAnimation Count with sdk_mode_obfusc8te owner; exit removes owner. | SDK mode transition | Push suppresses idle for SDK mode | Count is not 0x198; this is message production rather than direct PushIdle call. |
| C18 | 0x005BD08C→0x005BD4C8; 0x005B33B8→0x005B37C0 | Behavior StopHelperWithoutCallback / activity OnDeselected remove their smart-owned idle. | Ownership flag set | Release owned entry exposes earlier stack | Can reveal an earlier 0x198, but does not create it. |

## SetStreamingAnimation call sites

| Step | Address | What / values | Gates / when | Order | Failure / UNKNOWN |
|---|---|---|---|---|---|
| S01 | 0x00543E26 | PlayAnimationAction destructor calls pointer overload with null. | Action teardown | Stop stream | Not a procedural live producer. |
| S02 | 0x00543F96 | PlayAnimationAction::Init passes resolved animation pointer. | Action initialization | Resolve/load before set | No recovered pointer to streamer+0xA8; arbitrary catalog identity is not proof of embedded live. |
| S03 | 0x0057B134..0x0057B16C | Name overload looks up CannedAnimationContainer then calls pointer overload 0x0057B174. | Caller passes name | Lookup, forwarding | A name matching ProceduralLive is not automatically the private embedded object. |
| S04 | 0x0057BD60 | RemoveIdleAnimation uses neutral animation+0x40. | Neutral replay branch | Set neutral | Not live. |
| S05 | 0x0057CFD2 | Update uses neutral animation+0x40, loops=1, true,false. | Keep-alive neutral replay byte+0x73 | Before KeepFaceAlive; clear replay flag | Not live. |
| S06 | 0x0057ED0E..0x0057ED2A | ReplayLastAnimation message callback passes streamer string+0x54 and payload loop count to name overload via 0x008CB8FC→0x004ADFF0; bool=true. | Incoming registered callback | Catalog-name path S03 | No recovered assignment to embedded live here. Payload getter is Get_ReplayLastAnimation at PLT 0x004ADFE4; this replays the stored last animation name. |

The native exported AnimationStreamer methods include SetStreamingAnimation, SetParam, PushIdleAnimation, SetupHandlers, UpdateLiveAnimation and SetDefaultParams. There is no recovered native StreamLive method that pushes 0x198. The C# convenience method is not primary evidence of an engine producer.
## Unity producers inspected

`ProceduralLive` occurs in the available Unity C# only as the enum member (`Anki.Cozmo/AnimationTrigger.cs:413`); there is no named literal push of it. This textual finding cannot resolve serialized Unity scene/prefab enum integers or runtime arguments.

| Step | Source | What / values | Gates / when | Order | Failure / UNKNOWN |
|---|---|---|---|---|---|
| U01 | Cozmo.Hub/NeedsHub.cs:271; NeedsHub_2018.cs:335; DataPersistence/DebugProfile.cs:132 | Count, no-freeplay lock | Hub/debug startup option | Wrapper→message→P09 | Not live. |
| U02 | Cozmo.Challenge.CubePounce/CubePounceStateResetPoint.cs:96,110; CubePounceStatePostPoint.cs:113 | CubePounceIdleLiftUp / CubePounceIdleLiftDown | Reset-point / post-point state | Wrapper path | Not live. |
| U03 | Cozmo.Challenge.DroneMode/DroneModeShowInstructionsState.cs:15 | DroneModeIdle | Instructions state | Wrapper path | Not live. |
| U04 | Cozmo.Challenge.DroneMode/DroneModeTransitionAnimator.cs:230 | Passed animation parameter | Transition idle selected | Wrapper path | Runtime/serialized value UNKNOWN; no fixed 0x198. |
| U05 | Cozmo.Repair.UI/NeedsRepairModal.cs:1404; NeedsRepairModal_2018.cs:1376 | Passed default_anim parameter, needs_repair_idle lock | Repair UI | Wrapper path | Runtime parameter UNKNOWN; no literal ProceduralLive. |
| U06 | CozmoSays/CozmoSaysGame.cs:37; CozmoPerforms/CozmoPerformsGame.cs:33 | CozmoSaysIdle | Game startup | Wrapper path | Not live. |
| U07 | InitialCubesState.cs:78..87; calls at :75,:125; MemoryMatch/ScanForInitialCubeState.cs:339 | GameSetupIdle, initial_cubes_state | !_PushedIdleAnimation; inherited setup calls | Construct direct message, send, mark owned | Not live. |
| U08 | OnboardingManager.cs:351 | OnboardingIdle | Onboarding setup | Wrapper path | Not live. |
| U09 | Onboarding/OnboardingBaseStage.cs:202 | _CustomIdle.Value | Optional custom idle present, stage entry | Wrapper path | Serialized/runtime value UNKNOWN. |
| U10 | IRobot.cs:219; MockRobot.cs:571 | Interface declaration and mock | Not an official-engine producer | None | Excluded from production evidence. |

Available extracted JSON was searched for ProceduralLive and idleAnimTrigger; neither occurs. `idle_animName` does occur in freeplay/requestGame and voiceCommands configs, with the RequestGame*Idle values described in C09. A complete binary Unity asset/PPtr/serialized-enum scan was not performed; an automatic pusher concealed there remains RECOVERABLE_GAP, not proof of no production pusher.

## Build rows: desired face distortion ownership, inputs and output writers

There is no recovered externally assigned “DesiredFaceDistortion float” on AnimationStreamer. TrackLayerComponent obtains a degree by invoking a NeedsManager-owned component. The C# Func<float> seam is a candidate integration interface, not the original storage model.

| Step | Address | What it does / values | Gates | Order | Failure results / UNKNOWN |
|---|---|---|---|---|---|
| D01 | 0x0069210C; 0x00692212 | NeedsManager constructs a 0x18-byte DesiredFaceDistortionComponent and owns it at +0x3D4. | NeedsManager construction | After other needs state/config members | Allocation exceptions not extracted. |
| D02 | 0x0063B3BE..0x0063B3D2 | Component: params+0=null, deadline+4=0xBF800000 (-1), manager+8 reference, RNG+0xC=null, cached degree+0x10=0xBF800000, cached tick+0x14=0. | Construction | Initializes before Init | **Output writer 1:** constructor strd at 0x0063B3CE initializes degree and tick. |
| D03 | 0x00692574; 0x00692620..0x00692638 | NeedsManager::Init reads context+0x14 RNG and calls component Init at 0x0069262C with needs handler config. | RNG nonnull | After needs/decay/reward/actions setup | Null RNG: verify NeedsManager.Init.NoRNG, skip component initialization. |
| D04 | 0x0063B3D4..0x0063B409 | Read needsBasedFaceDistortion; create replacement Params, swap component params, destroy old params; save RNG+0xC. | Init invoked | New Params constructed before old destroyed | Does not reset deadline, cached degree or cached tick. |
| D05 | 0x0063B434..0x0063B518 | Params creates cooldown graph+0, multiplier+4 default 0x00000000; reads cooldown graph and cooldown_range_multiplier via asFloat. | Config parsing | Cooldown before degree | Missing graph verifies; parse failure/empty graph logs, sets _errG, optional debug-break; continues. No invented safe graph. |
| D06 | 0x0063B526..0x0063B5DE | Degree graph+8 and multiplier+0xC default 0x00000000; reads degree and degree_range_multiplier. | Config parsing | After cooldown | **Shipped quirk:** successful degree ReadFromJson at B550 then nonempty check at B558/B55A examines cooldown graph (Params+0), not degree graph. Missing/failed config logs/verifies and continues. |
| D07 | 0x0057CF7A→PLT 0x004ADE34→0x0064EDE8 | Streamer calls TrackLayer Update; context→NeedsManager at context+0x34→component at manager+0x3D4. | Streamer last-stream seconds+0x88>0 | Before keep-alive no-active-stream gate | This tick call is not conditional on idle top 0x198. |
| D08 | 0x0064EDF4→PLT 0x004BA080→0x0063B760 | Invoke GetCurrentDesiredDistortion. | D07 | Before epsilon compare | Single recovered getter call site in native call scan. |
| D09 | 0x0063B76E..0x0063B782; timer vtable 0x01037EC0; GetTickCount 0x0084BCE0 | Timer virtual slot+8 supplies tick count; compare cached tick+0x14. Equal returns cached degree+0x10 immediately. GetTickCount loads timer+0x24. | Same tick | Before params/RNG/pause checks | Not wall-clock milliseconds or Environment.TickCount. Repeated call sees same sample even if paused in between. |
| D10 | 0x0063B784..0x0063B7AA | New tick: cached degree=-1 and cached tick=current via strd at B792. Require params, RNG, !manager paused byte+0x1D5. | New tick | Reset before all gates | **Output writer 2:** B792. Missing params/RNG or paused returns 0xBF800000; no RNG draw, no deadline update. |
| D11 | 0x0069216A..0x00692174; 0x00695E6A | NeedsManager initializes paused=false by clearing block+0x1B8; SetPaused stores bool+0x1D5 when state differs. | Manager creation / pause change | Gate input, not cached-degree writer | Same-tick cached degree is not invalidated by this writer. Other pause-transition behavior outside scope. |
| D12 | 0x0063B7AE..0x0063B7D0 | Get current seconds float32; eligible if deadline<0 or deadline<=now. | Deadline gate | Before needs graph evaluation | Deadline future returns -1 cached for tick. Branches are BMI/BHI after VFP compares; NaN behavior must preserve flags, not assume ordinary comparisons cover it. |
| D13 | 0x0063B7D4..0x0063B800 | GetCurNeedsState, GetNeedLevel(NeedId 0=repair), evaluate degree graph; reject degree <0x3DCCCCCD (0.1f). | Eligible deadline | Degree graph first | Below threshold returns -1, no random draw/deadline update. Equality at 0.1 is admitted. |
| D14 | 0x0063B804..0x0063B83C | h=float32(degree_multiplier*0x3F000000); low=float32(degree*float32(0x3F800000-h)); high=float32(degree*float32(h+0x3F800000)); widen bounds to float64, RandDblInRange. | Degree threshold passed | **First random draw: degree** | No clamp/retry recovered. Float32 round at each instruction. |
| D15 | 0x0063B840..0x0063B884 | Evaluate cooldown graph with same repair level; bounds use cooldown_multiplier and identical float32 operations, widen to float64, RandDblInRange. | After degree draw | **Second random draw: cooldown** | Does not sample cooldown first. |
| D16 | 0x0082FA48..0x0082FA72 | RandDblInRange: low + GetNextDbl()*(high-low), float64 subtract/multiply/add. | Called D14/D15 | Shared RNG consumed sequentially | Exact RNG seed/state and endpoint realization are not reproduced in this report; numerical sample UNKNOWN until RNG state known. |
| D17 | 0x0063B89C; 0x0063B8CE; 0x0063B94C..0x0063B958 | Convert sampled degree and cooldown to float32; cached degree store B950; deadline store B954 = float32(now+sampledCooldown); return degree. | Successful sampling | Log degree then cooldown before final writes | **Output writer 3:** B950. There is no fourth setter/output writer recovered in component functions/callers. Arbitrary aliased memory corruption is not a setter. |
| D18 | 0x0064EDF8..0x0064EE14; literal 0x0064EE18 | Preserve returned float bits in r1; compare degree with 0x3727C5AC (~1e-5f). If <=, return; otherwise tail-call AddGlitch(this,degree) through veneer. | Getter degree above epsilon | Read, compare, AddGlitch | Ghidra's apparent AddGlitch(this,1e-5) is wrong: epsilon is compare-only; argument r1 retains sampled degree. -1 sentinel produces no glitch. |

The output writer inventory is component-local storage +0x10: constructor at B3CE, new-tick reset at B792, successful sample at B950. Init writes params/RNG, Params parsing writes graph/multiplier inputs, NeedsManager pause writes a gate. They must not be conflated with an external degree-setting API.

## Config values and arithmetic evidence

Shipped file: `re-analysis/obb/assets/cozmo_resources/config/engine/needs_handlers_config.json:2..65`. JsonCpp accepts its comments. Node order is retained. GraphEvaluator2d::EvaluateY at 0x00804BD0 evaluates the float32 graph with endpoint behavior and interpolation; the native companion includes the evaluator. No smoothstep or fitted curve is inferred.

| Graph | Asset lines | Nodes (repair, output) as float32 hexadecimal bits |
|---|---|---|
| cooldown, seconds | 3..27 | (00000000,3F400000), (3DCCCCCD,40800000), (3E4CCCCD,41000000), (3E99999A,41200000), (3F000000,41200000) |
| degree | 31..63 | (00000000,40400000), (3DCCCCCD,40000000), (3E4CCCCD,40000000), (3E99999A,3F800000), (3ECCCCCD,3E4CCCCD), (3F19999A,3DCCCCCD), (3F333333,00000000) |
| cooldown multiplier | 29 | 0.3 → 3E99999A |
| degree multiplier | 65 | 0.2 → 3E4CCCCD |

Examples of desired graph values (before sampling): repair 0 gives degree 0x40400000 (3), cooldown 0x3F400000 (0.75 seconds); repair 0.6 gives degree 0x3DCCCCCD (0.1, admitted), cooldown 0x41200000 (10); repair 0.7 gives degree zero (rejected). Bounds are approximately degree ±10% and cooldown ±15%, but those percentages are explanatory: implement the instruction-ordered float32 products above, followed by float64 RNG and float32 conversion. Do not substitute decimal arithmetic or a single wider expression.

## Comparison with the quoted records

| Record | Finding from this extraction | Manifest impact for manager review |
|---|---|---|
| M7-005 | Its “has no component here” observation describes missing C# implementation, not absent native source. D01..D18 recover that component and its sampled glitch input. The native distortion read is before KeepFaceAlive gates, not confined inside KeepFaceAlive. | Evidence for the missing input is now available; do not promote the entire blink/production path based on it. |
| M7-007 | Persistent dart evidence was not contradicted or re-extracted. Its defaulted-distortion dependency now has a source inventory in D01..D18. | Preserve status pending full production-path comparison. |
| M7-008 | The quoted production C# non-reachability is not refuted by merely finding an engine input handler. Native callers accept configured/message triggers, but no fixed automatic 0x198 producer was established. Face-only keep-alive can execute independently of that trigger (P07). | Keep reachability uncertainty visible; do not convert “no fixed producer found” into “engine has no producer.” |
| M7-009 | Live head/lift remain behind P04; distortion's own caller has different gates. | No change to recovered keyframe values; upstream production input remains unresolved. |
| M7-010 | Body shuffle remains behind P04. Face composition is not synonymous with that branch. | No whole idle-body provenance promotion. |
| M7-016 | P07 supports the stated float32 keep-alive gates and refines where distortion sampling occurs. | The missing source/input can be inventoried; composition and wire lifecycle still need their own verification. |
| M7-017 | Any literal claim that DesiredFaceDistortion “has no source” is contradicted by 0x0063B760 and its owning/initializing native path. A claim that it has no C# implementation remains supported by the unset Func seam. “Engine has no pusher of 0x198” is stronger than the bounded evidence: message/config paths accept it; inspected fixed producers use other values. | Manager may replace the source-absence assertion with this extraction, while retaining upstream reachability and full lifecycle gaps. No status is changed here. |

## Existing C# seams inspected (mapping only)

| Original piece | Existing candidate / observation | Limit |
|---|---|---|
| PushIdleAnimation and top-of-stack selection | `cozmo-stack/src/Cozmo.Robot/Animation/AnimationScheduler.cs:884` | Caller/config provenance must precede any added automatic push. |
| Getter-driven distortion | `AnimationScheduler.cs:494`, delegated to `TrackLayers.cs:612`; consumption `TrackLayers.cs:653` | Current unset→0 is not the native cached -1/component model. No code changed. |
| Convenience live keyframe streaming | `CozmoAnimations.cs:342`, `AnimationScheduler.cs:936` | Not a recovered native producer; comments asserting engine absence exceed this report's evidence. |
| Needs-owned Params, RNG, repair-state getter, pause gate and tick cache | NeedsManager-owned native component D01..D17 | Corresponding complete C# component was not found at the inspected animation seam. Do not fill it with a constant degree or invented timer. |

## Search closure, validation and remaining uncertainty

The native call inventory was obtained from defined dynamic symbols and PLT entries, decoding candidate Thumb BL/BLX/B.W instructions across .text at halfword alignment. Thumb-to-ARM veneers of the form `BX PC; LDR ip,[pc]; ADD pc,ip,pc` were resolved before matching. This recovered the PushIdleAnimation and ReplayLastAnimation callbacks that direct PLT-only searches miss. The 29-site companion includes constructor/config/getter calls as well as push/set calls; it is not 29 distinct pushers. Candidate decoded instructions were reviewed at the recorded function addresses. SetIsLive in the streamer constructor and the direct embedded-live idle pointer store in Update are separately inventoried because neither is a call to PushIdleAnimation.

This closes the inspected native API caller inventory, not every conceivable runtime input. UNKNOWN remains for dynamically supplied activity/behavior configuration and Unity serialized/custom idle parameters, a complete binary Unity asset enum scan, and any external sender's actual 0x198 requests. No available inspected fixed caller is evidence for an autonomous ProceduralLive push. No hardware/capture run was used to claim one.

The complete distortion path traced here is streamer Update → TrackLayer Update → NeedsManager component → engine tick cache → params/RNG/pause/deadline gates → repair state → degree graph → degree RNG draw → cooldown graph → cooldown RNG draw → cached degree/deadline writes → threshold → AddGlitch(sampled degree). Its numerical output is state-dependent; actual RNG state and repair level are UNKNOWN for an unobserved run. Graph configuration and branch behavior are recovered, including the degree-parser cooldown-vector quirk.

Validation: re-read native argument registers and output stores; resolved both callback veneers; checked config values/float bit patterns; quoted the seven current manifest records before comparison; checked working-tree scope. Research files only. No regression tests were added or run for this documentation-only extraction. No commit, manifest edit or fidelity approval. Coverage has no NOT DONE entry; the upstream production-producer question remains explicitly PARTIAL.
