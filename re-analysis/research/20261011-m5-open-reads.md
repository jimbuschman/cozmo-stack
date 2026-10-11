| Item (requested order) | Status READ/STOP | Rows |
|---|---|---|
| M5-040: EnableClippingWarning callers, values and activation | READ | CW1–CW5. |
| M5-043: FlatBuffer/JSON FaceAnimation keyframe +28 writes | READ | FK1–FK4. |
| M5-044: TrackLayerComponent destruction and streamer+C shared object | READ | TD1–TD8. |
| M5-030: ProceduralLive pusher, callers/triggers/arguments | READ | PL1–PL19. |
| M5-040: abnormal exits and computed/runtime invocations | STOP | CWU |
| M5-043: alternative inputs and later mutations | STOP | FKU |
| M5-044: external ownership bodies / alternate interface | STOP | TDU |
| M5-030: actual production source supplying ProceduralLive | STOP | PLU |

# M5 open reads — 2026-10-11

Read first: PROJECT_STATE.md; `0x20261011-m5-settling-pass.md`; current manifest at 5348fe1. Current records: M5-030, “Live idle (UpdateLiveAnimation): gates, body/lift/head wiggles with their parameters, LiveIdleTurn eye shift, lock and carry checks”, IMPLEMENTATION_GAP, evidence A35 0x0057D5F8..0x0057DA82, gap1 K2/K10/R2–R4 and settling-pass row M5-030; M5-040, “ProceduralFace clip warning: ClipWarnFcn (ProceduralFace.Clip.OutOfRange) and where it is enabled”, RECOVERABLE_GAP, settling-pass N1 at 0x00584B48/0x0105AB20; M5-043, “FaceAnimationKeyFrame +0x28 writers on the FlatBuffer/JSON add path (Track::AddKeyFrameToBack(CozmoAnim::FaceAnimation*))”, RECOVERABLE_GAP, settling-pass M5-013/N7 at 0x005642C6..0x005642CC/0x00563E48/0x00587CCC; M5-044, “~TrackLayerComponent internals and the AnimationStreamer +0xC shared object at teardown”, RECOVERABLE_GAP, settling-pass M5-037 at 0x0057AF8C..0x0057AF98/0x0057AFD0.

Research only; no fidelity claims. Native instructions are primary; symbols/decompiler/C# comments supply navigation only. Unsettled behavior is STOP/UNKNOWN. Offsets/addresses hexadecimal unless stated otherwise.

Primary capture: `0x20261011-m5-open-reads-native.txt`; engine SHA256 02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. Capture endpoints are exclusive; literal pools are data. The decompiler's BSS references can be 0x10000 higher than symbol relocation values; use relocated ELF words, not those pseudo-C addresses.

## 1. M5-040 — clipping warning activation

| Row | Native citation | Recovered behavior |
|---|---|---|
| CW1 | `0x0103E3EC;0x004D73F8..0x004D7426;0x0103ED50` | .init_array entry 0x0103E3EC→0x004D73F9 invokes the static initializer, which installs a callable with target 0x00584A41 (Thumb body 0x00584A40) into ClipWarnFcn, stores its inline target pointer at+10, then registers function destruction at process exit. ELF GOT relocation 0x0103ED50 binds **0x0105AB20**, agreeing with the settling-pass address; pseudo-0xC106AB20 is not the relocated address. Initial state is warning enabled. |
| CW2 | `0x00584B48..0x00584B66` | Argument exactly 1 selects 0x00584A41; every other value selects 0x00584BE5 (body 0x00584BE4, BX LR). Assigns the callable through shared GOT 0x0103ED50. No per-face enable flag. |
| CW3 | `0x0051F89C..0x0051F8A2` → thunk 0x004A88A8 →0x00584B48 | RobotDataLoader::LoadAnimationsInternal passesr0=0 before constructing/queuing its animation-loading worker; warnings off during loading. |
| CW4 | `0x0051F9C4..0x0051F9D2` → thunk 0x004A88A8 →0x00584B48 | After DispatchWorker::Process at 0x0051F9C8 returns, movs r0,1 at 0x0051F9CC then0051F9CE restores warnings before timing/logging and later destruction of worker/local temporaries. This is normal-return order, not exception-safe restoration. |
| CW5 | `0x0058485E..0x005848AC;0x00584A40..0x00584AA0;0x00584ACC;0x00584AEC` | Clip's below-min and above-max branches invoke the same callable with parameter name, original F32 value, minimum and maximum, then replace value with bound. Enabled target widens all three floats toF64 for sWarningF at 0x00584A78: key `ProceduralFace.Clip.OutOfRange`, format `Value of %f out of range [%f,%f] for parameter %s. Clipping.` Disabled target returns without logging. |

Caller inventory: the existing `0x20261008-postevent-callers.py` was run with these concrete targets; `0x20261011-m5-open-reads-callers.txt` records its candidates. Both clipping calls and every M5-030 candidate were reopened in the native companion. The helper also emits its fixed unrelated Wwise targets; those are not extracted here. The native/decompiler symbol index names the ARM PLT thunk 0x004A88A8 as 0x00584B48's caller, and LoadAnimationsInternal as that thunk's caller; the two actual call sites/arguments are CW3/CW4. No caller supplying a setting, debug toggle or per-face option is established.

| Row | Native boundary | STOP/UNKNOWN |
|---|---|---|
| CWU | `0x0051F884;0x00584B48;0x004A88A8` | Abnormal loading-exit restoration and runtime/computed/exported invocations are not established by the recovered direct calls. No inferred toggle. |

## 2. M5-043 — authored FaceAnimation keyframe +28

| Row | Native citation | Recovered write/path |
|---|---|---|
| FK1 | `0x00576678..0x005766DA;0x00577328..0x0057738A;0x00564290..0x005642D2` | FlatBuffer and JSON AddKeyFrameToBack each construct a temporary FaceAnimationKeyFrame through564290. At5642C6/CA/CC clear exactly 11hex bytes startingK+18: includes the **byte K+28=0**. This is the first write in both add paths. |
| FK2 | `0x004F96C4..0x004F9706;0x004F9708..0x004F9762;0x004F8AA4..0x004F8B34;0x004F9434..0x004F94D4;0x004F9574..0x004F9646` | Constructor5642A2..A8 loads GOT 0x0103E9D8 and installs table 0x0101F0B8 (relocated FaceAnimationKeyFrame table base0101F0B0+8); table+C=0x004F9435 and+10=0x004F9709. FlatBuffer writes trigger4 and dispatches concrete member-reader via10; JSON writes trigger4 then member-reader viaC after required triggerTime access. Member readers assign animName atC..14 and callProcess. Process strips path, optionally warns, and writes **word +24=0** at4F963C. Those writes do not overlap28. No authored field changes28 in these bodies. Missing JSON name returns 1; success returns 0. |
| FK3 | `0x00576678..0x005766F8;0x00577328..0x005773A8;0x00579454..0x005794C2` | Reader raw 0 permits AddNewKeyFrameToBack; nonzero skips insertion and destroys temporary. AddNew uses helper then checks physical last/previous trigger times; invalid order logs and removes newly appended node. Neither add wrapper nor order check writes keyframe28. |
| FK4 | `0x005645B0..0x0056462E`, especially `0x00564612..0x00564616` | Node allocation34hex includes links0/4, with keyframe startingnode+8. Native LDRB sourceK+28 then STRB destinationnode+30 = destinationK+28. This is the second write: **copy exact source byte**, therefore0 on the freshly constructed authored add path. Vector copy targets destinationK+18..23; word +24 copy atnode+2C is separate. Node published into list after copies; no blanket-zero node allocation assumed. |

Within these two complete authored-add paths, writes to28 are FK1's constructor clear and FK4's byte-copy. This does not classify every independently supplied keyframe or later streaming mutation.

| Row | Native boundary | STOP/UNKNOWN |
|---|---|---|
| FKU | `0x00576678;0x00577328;0x005645B0` | Other independently supplied keyframes or later streaming writes are outside these two authored-add paths; no nonzero authored field or alternate input is inferred. |


## 3. M5-044 — layer teardown and shared subscription

S denotes the AnimationStreamer; L denotes its TrackLayerComponent. These offsets belong to different objects.

| Row | Native citation | Recovered behavior; sends/logs |
|---|---|---|
| TD1 | `0x0057AF8C..0x0057AF98;0x0064ED8C..0x0064EDE0` | Streamer clears S+60 before calling ~TrackLayerComponent, then frees L. L destructor visits L+10 ProceduralFace, L+C face manager, L+8 backpack manager, L+4 audio manager, in that order. Each pointer is cleared before its nonnull destructor/tree destruction and free. Returns L for outer deletion. No send or logger call in this destructor body. |
| TD2 | `0x0058376A..0x0058378E;0x0064F522..0x0064F5CE;0x0058F070..0x0058F09E;0x005239E6..0x00523A1E;0x00523A5C..0x00523AB0;0x004F8A88..0x004F8AA2` | ProceduralFace releases a nonnull +98 distorter, its vector allocations, and the distorter. Each manager's tree destroys left then right children, frees a heap layer-label string if its tag bit is set, clears its track list, frees the node. AudioSample clear unlinks/frees list nodes; backpack clear additionally runs IKeyFrame destruction; face clear additionally runs ProceduralFace then IKeyFrame destruction. IKeyFrame destruction frees its heap string. These bodies release owned memory and contain no send or log call. |
| TD3 | `0x004EB26E..0x004EB292;0x004EA734..0x004EA75A;0x0065FF30..0x0065FF6E;0x0103F908;0x0102FE48;0x00579F78..0x00579F8A` | Engine constructs UiMessageHandler and passes that same pointer to CozmoContext, whose +4 stores it. Streamer constructor passes context+4 to its HasSettableParameters base constructor. UiMessageHandler installs method table 0x0102FE1C (GOT 0x0103F908→table base0102FE14+8); slot2C at0102FE48→0x00662751, the GameToEngine Subscribe overload. This establishes the recipient for the shipped engine construction path. |
| TD4 | `0x0057A48C..0x0057A4E6;0x0103ED08;0x010239C8..0x010239E4;0x00662750..0x006627AC` | Base constructor initially zeroes S+8/+C. With external interface present it subscribes tag B4 (decimal 180, live-idle parameter messages), with lambda capturing S, through slot2C at 0x0057A4CE. At0057A4D2..DC moves returned shared-pointer pair to **S+8=pointee, S+C=control block**, clearing the temporary pair. Thus S+C is the ownership control block of the parameter subscription. Null interface leaves both zero. |
| TD5 | `0x00662768..0x00662782;0x006648CC..0x0066493C;0x0103F93C;0x01030000..0x01030008;0x004EF088..0x004EF0AC;0x0103E89C;0x0101E8F8..0x0101E908` | Subscribe uses a per-tag signal at interface+38 and constructs a four-byte **Signal::Lib::ScopedHandleContainer**, containing a 0x14-byte GameToEngine ProtoHandle. ProtoHandle table 0x01030000, slots0/4/8→0x00664E75/0x00664E95/0x00664EB9. Shared-pointer factory allocates a 0x10-byte control block, stores pointee at control+C and installs table 0x0101E8F8. Its on-zero-shared slot+8→0x004EF185. Concrete type of S+8 is shared_ptr<ScopedHandleContainer>'s pointee; S+C controls that shared ownership. |
| TD6 | `0x0057AFD0..0x0057AFD4;0x004EF184..0x004EF196;0x004EF1B8..0x004EF1D0;0x00664EB8..0x00664EF2;0x0066365C..0x00663694;0x00663696..0x006636CA;0x00664E94..0x00664EB0` | AtAFD0 loads S+C; nonnullAFD4 invokes external libc++ __release_shared. On last shared release, shipped004EF184 calls ScopedHandleContainer destructor004EF1B8, then frees container. Container calls ProtoHandle slot8 (unlink), then slot4 (deleting destructor). Unlink requires its signal weak ownership to lock successfully and its saved node to still be found in the ring;0x0066365C destroys/nulls the callback and relinks neighbours, then drops the node reference. ProtoHandle deleting destructor releases the weak control and frees itself. No send/log call in these shipped destructor/unlink bodies. |
| TD7 | `0x010239D8..0x010239E0;0x0057E830..0x0057E83A` | Parameter callback's destroy body 0x0057E830 is BX LR; destroy-deallocate0057E832 tail-calls delete. Destroying this captured S pointer does not invoke the message callback, send a final value, or log. |
| TD8 | `0x0057AF58;0x0057B044..0x0057B0CA;0x0057B0FC;0x0057B120;0x00BE3FEC` | Streamer teardown first calls ClearSendBuffer. For each queued unsent message it logs channel `Unnamed`, key `AnimationStreamer.ClearSendBuffer`, format `Clearing unsent %s`, with EngineToRobot tag name; clears/frees each payload and clears list. This is the recovered teardown log preceding TD1/TD6. The body does not dispatch the queued messages. |

| Row | Native boundary | STOP/UNKNOWN |
|---|---|---|
| TDU | `0x0057AFD4;0x004A4EF4;0x0057A4CE` | __release_shared and weak-count locking implementations belong to external libc++; their arithmetic and alternate host-supplied IExternalInterface implementations are not settled by this binary. TD3–TD7 describe the concrete shipped UiMessageHandler recipient and shipped destruction targets, conditional on last ownership release. |

## 4. M5-030 — idle pusher and all recovered native callers

Native caller candidates were obtained with the existing branch-census helper and reopened individually. It covers direct ARM/Thumb calls and the known Thumb-to-ARM veneer, not arbitrary computed/exported invocations. The previous `0x20261004-procedural-live-xrefs.json` agrees with this freshly checked caller list; it is navigation, not substituted evidence. Runtime configuration/message values remain explicit unknowns.

| Row | Native citation | Trigger, arguments and activation |
|---|---|---|
| PL1 | `0x0075E298..0x0075E2AA;0x01032CF0;0x00C18792;0x01032F8C;0x00C1966A;0x0057A01E..0x0057A0B2;0x0057D03A..0x0057D080` | EnumToString table entry 198→`ProceduralLive`;23F→`Count`. Streamer creates the embedded live Animation atS+A8 and marks it live, but initializes idle stack with Count. When no active stream and idle stack top is 198, Update sets live-enabled S+194, selects S+A8 into S+34, calls UpdateLiveAnimation at 0x0057D080. Object construction alone does not push 198. |
| PL2 | `0x0057B914..0x0057B9D6;0x0057BA10;0x0057BA1C;0x0057BA40` | Concrete pusher **AnimationStreamer::PushIdleAnimation(trigger, lockName)**. Appends trigger plus copied string as a 0x10-byte vector element atS+48/4C/50; new element becomes top. Trigger 23F first clears S+64 and S+34. Trigger 198 is accepted without a special push-time branch. After append, info log channel `Animations`, key `AnimationStreamer.PushIdleAnimation`, format `Setting idle animation to '%s'.`, with EnumToString(trigger). Returns raw 0. |
| PL3 | `0x00572E48..0x00572F22;0x00C596C4..0x00C596D0;0x005730FA..0x00573160`, call `0x00573152`→0x004AD2F4 | SevereNeedsComponent::SetSevereNeedExpression(NeedId): matching mapped need1→137, need0→143; missing map entry skips push. Receiver robot from component+4, streamer robot+60; lock `severe_need_component_lock`. Push precedes reaction-lock work. Neither fixed trigger is 198. |
| PL4 | `0x005ACB70..0x005ACCB2`, call `0x005ACC70`→0x004AD2F4 | ActivityFeeding::SetIdleForCurrentStage(Robot&): stage0→B3,1→B4,4→B8/B9,7→B5/B6; other in-range stage→23F/B7; stage>=E→23F. Alternate chosen when `robot+264 → +30 → +14` equals1. If same trigger already owned, return; if changed and owned, first remove old idle. Push(receiver robot+60, selected trigger, activity-name string from virtual slot24); then +A8=1,+A4=trigger. |
| PL5 | `0x005B32BC..0x005B331E`, call `0x005B3304`→0x004AD2F4 | IActivity::SmartPushIdleAnimation(activity, Robot&, supplied trigger): if activity+50 already set, verify-failure log and return. Otherwise lock is static `Activity_` prefix (initializer004D7590..0x004D75C2, literal00C49CD0) plus activity-name via slot24; pushes robot+60 with unchanged trigger, then +50=1. Two native callers: PL6/PL7. |
| PL6 | `0x005B312C..0x005B31BC`, call `0x005B31BC`→0x004B1C08 | IActivity::OnSelected(Robot&): after activity status/driving work, resets ownership+50=0; if stored activity+44!=23F calls PL5(activity, robot, **activity+44**). Selection is the activation; trigger is configured, not a fixed198. |
| PL7 | `0x005B15D0..0x005B1630`, call `0x005B1630`→0x004B1C08 | ActivitySparked::OnSelectedInternal: when behavior-manager+60==55 OR byte+64==0, after driving-idle push calls PL5(activity, robot, **213**), then backpack-light work. |
| PL8 | `0x005B28D8..0x005B2924;0x005B2CA4..0x005B2D24;0x005B2F40` | Writer feeding PL6: IActivity constructor defaults +44=23F; ReadConfig optional `idleAnimTrigger`: absent leaves the constructor default; present empty writes23F at 0x005B2D24; present nonempty converts with AnimationTriggerFromString at 0x005B2D0A and stores result+44 at 0x005B2D0E. This establishes a configurable pusher path. Actual production input 198 is UNKNOWN. |
| PL9 | `0x005BE41C..0x005BE47E`, call `0x005BE464`→0x004AD2F4 | IBehavior::SmartPushIdleAnimation(behavior, Robot&, supplied trigger): already-owned+B0 logs verify failure and returns. Otherwise lock is static `Behavior_` prefix (initializer004D75D4..0x004D7606, literal00C49CE0) plus behavior name at+40; forwards trigger to robot+60, then+B0=1. Eight native callers: PL10–PL17. |
| PL10 | `0x005D7EB8..0x005D7ED8`, call `0x005D7ED4`→0x004B426C | BehaviorCubeLiftWorkout::InitInternal(behavior, robot): after reaction locking, PL9 with trigger **23F**. |
| PL11 | `0x005EC114..0x005EC13C`, call `0x005EC13C`→0x004B426C | BehaviorRequestGameSimple::IdleLoop(behavior, robot): PL9 with **selected config pointer at behavior+200 → word+10**;23F branch leaves23F. Runtime selected value 198 is UNKNOWN. |
| PL12 | `0x005F14A0..0x005F14C8`, call `0x005F14C4`→0x004B426C | BehaviorBouncer::InitInternal(behavior, robot): after reaction locking, PL9 with **23F**. |
| PL13 | `0x005F1ED8..0x005F1EF6`, call `0x005F1EF2`→0x004B426C | BehaviorFistBump::InitInternal(behavior, robot): after reaction locking, PL9 with **23F**. |
| PL14 | `0x005F66F6..0x005F6712;0x005F675E..0x005F679E`, call `0x005F679A`→0x004B426C | BehaviorPeekABoo::InitInternal(behavior, robot): when both+D9 and+D8 are zero OR F32+168 differs from -1, reset bookkeeping, draw integer, then PL9 with **23F**. |
| PL15 | `0x005F8430..0x005F8462`, call `0x005F845E`→0x004B426C | BehaviorPounceOnMotion::InitHelper(behavior, robot): both+D9/+D8 zero, PL9 with **192**, before driving animation setup. |
| PL16 | `0x005FB3E8..0x005FB446`, call `0x005FB442`→0x004B426C | BehaviorTrackLaser::TransitionToRespondToLaser(behavior, robot): bytes+D9,+D8,+1AD all zero, PL9 with **104**, before driving-animation setup. |
| PL17 | `0x00606C94..0x00606CCE`, call `0x00606CCA`→0x004B426C | BehaviorReactToOnCharger::InitInternal(behavior, robot): after reaction locking and external callback setup, PL9 with **23F**, before lift-safe action creation. |
| PL18 | `0x0057A5C2..0x0057A5E4;0x010238B4..0x010238CC;0x0057EC1A..0x0057EC32;0x008CB8DC..0x008CB8E6` | Fifth direct pusher caller is the registered GameToEngine **PushIdleAnimation** callback. SetupHandlers with nonnull interface subscribes tag AE (decimal 174), captures S, installs callable table 0x010238B4 whose operator slot 18→0x0057EC1B. Callback uses event+10 payload getter, loads **payload word0 trigger **, passes **payload+4 lockName**, receiver captured S. Tail branch0057EC32→veneer008CB8DC→0x004AD2F4. Incoming message can supply198; actual producer/value is UNKNOWN. |
| PL19 | `0x004AD2F4;0x004B1C08;0x004B426C;0x00573152;0x005ACC70;0x005B3304;0x005BE464;0x0057EC32` | Caller tally: **5 direct native pusher call sites** (PL3/4/5/9/18); **2 activity-wrapper callers** (PL6/7); **8 behavior-wrapper callers** (PL10–17). Thunks/veneer forward to the concrete bodies. Config reader PL8 is a value writer, not an additional push caller. |

| Row | Native boundary | STOP/UNKNOWN |
|---|---|---|
| PLU | `0x005B2D0E;0x005B31BC;0x005EC13C;0x0057EC32;0x0057D080` | The binary settles the pusher and its argument forwarding, but does not settle a concrete production source supplying 198 to these configurable/message paths. That producer remains UNKNOWN. No implicit automatic ProceduralLive push is invented; computed/exported invocations are not excluded by the direct-call census. |

Completion: 4 bounded reads, 36 address-cited READ rows (CW5 + FK4 + TD8 + PL19), and 4 STOP/UNKNOWN boundary rows. No production code, manifest, inventory or PROJECT_STATE edits; no fidelity claims. Research-only commit/push requested, then stop.
