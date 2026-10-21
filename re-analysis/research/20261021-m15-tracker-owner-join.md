# R-M15-TRACKER-OWNER — construction, admission and retirement

Request: `requests/20261021-m15-tracker-owner-join.md`. HEAD80a357a2. Independent read-only report; coverage opened before tracing. No status change.

## Coverage

| Step | Source / host | Result |
| --- | --- | --- |
| Robot constructs AI | 0x0050FD94..0x0050FDA4 | Unconditional allocation/constructor then Robot+0x264 store |
| AI constructs tracker | 0x00569B1E..0x00569B2C; adopted member map | Unconditional0x28 allocation, ctor, AI+0x2C store |
| Tracker initial state | 0x0056EBD4..0x0056EC18 | Empty pause set, zero accumulated/resume time, deadline now+30; no Freeplay admission test |
| Packet admission | Adopted M1 CB2/CB5/CB6 plus sensor report | Complete Robot ctor precedes registration/use; no app-Freeplay creation dependency |
| Update | AI0x00569F24..0x00569F4C; Robot0x00513EAC | Robot tick after native gates, not user Freeplay.Tick |
| Final ForceUpdate | 0x00511114..0x0051111A; adopted OV25–27 | Unconditional at Robot destructor head after event, before AbortAll |
| Storage retirement | AI+2C direct delete in adopted0x00569CB2..0x00569DAC | No second ForceUpdate; old allocation ends with old Robot |
| Current EngineRobot/AI join | RobotLifetime / FreeplayStack / AIComponent | Late facade tracker, optional binding, wrong update entry; fresh replacement missing |
| Minimal proposal | Per-native-Robot tracker owner, current pause routing, real tick/teardown | Needs manager/inventory adoption; no overlapping-public-handle policy selected |

## Native owner chain

Reuse adopted `re-analysis/inventory/M15-freeplay.md` tracker rows35/37 and constructor/update evidence, plus `overnight/20261014-ov25-26-27-check.md` teardown. No SendData arithmetic or four pause-input extraction repeated.

The previously unresolved narrow construction join was freshly read:
- Robot constructor0x0050FD94 loads size0x34, allocates at0x0050FD96, passes Robot to AIComponent ctor0x0050FDA0 and stores its pointer into Robot+0x264 at0x0050FDA4. There is no selected-activity/app-Freeplay condition in this block.
- AI ctor begins0x00569A78, stores Robot at+0 and creates children. Tracker block0x00569B1E loads size0x28;0x00569B20 allocates;0x00569B28 calls FreeplayDataTracker ctor0x0056EBD4;0x00569B2C stores pointer+0x2C. The ordinary ctor continues to SevereNeeds at+0x30 and returns0x00569B44. Entire normal construction sequence0x00569A78..0x00569B46 was inspected; recursive child algorithms/unwind recipient recovery are not claimed.
- Tracker ctor clears accumulator+0x20/+0x24, pause tree/count, resume+0x10/+0x14 and deadline+0x18, then gets singleton seconds and writes now+30.0f at0x0056EC04. Normal/unwind body0x0056EBD4..0x0056EC1A was read. It does not initialize itself from an app-Freeplay handle or wait for a first off-treads event.

Thus **“Freeplay” names the measured activity, not the native component's construction lifetime**.

## Before accepted sensor packets

Reuse M1 transport inventory:
- CB2 ConnectToRobot does AddRobotConnection then AddRobot, before transport exchange;0x004ED074/0x004ED07C.
- CB5 RobotManager.AddRobot constructs a new0x530-byte Robot, then inserts it/maps/list and RIC;0x0052EB7A..0x0052EBB2 and0x0052EE8E..0x0052EF3A.
- CB6 engine AddRobot then registers audio input/multiplexer;0x004ED1EC..0x004ED28C.

The full nested constructor therefore completes before this Robot is registered for ordinary packet handling. This is the relevant admission guarantee, not an assumption that merely having a host Sensors object proves native AI construction. OffTreads' native consequence reads the already constructed AI tracker. Native defined allocation failure handling is outside this normal path; no guessed null/failure implementation proposed.

Current CozmoRobot constructs retained Sensors before completing host construction, and packets route through admitted EngineRobot state as the preceding owner-availability report documents. EngineRobot construction currently does not create a tracker. Thus the native construction guarantee is still missing for that recipient even though host Sensors is already available.

## Update and retirement

AI::Update0x00569F24..0x00569F4C calls:
analyzer, whiteboard, SevereNeeds, helper, then loads+0x2C at0x00569F44 and calls tracker.Update at0x00569F46. Robot::Update calls AI at0x00513EAC after first-full-state/vision-result gates, before behavior manager and ActionList. There is no app-Freeplay-handle condition. This is update scheduling only; adopted tracker Update/SendData body remains unchanged.

Fresh ~Robot head read0x005110D4..0x0051111E confirms:
destructor event0x005110EE, then Robot+0x264 load0x00511114, tracker+0x2C load0x00511118, ForceUpdate0x0051111A, then AbortAll at0x00511120 (adopted call). It is not gated by whether any user created/disposed a Freeplay facade.

Later Robot+0x264 is nulled/deleted in the adopted lifetime sequence; AI destroys+0x30 then+0x2C and remaining direct children in its checked null-before-delete order. Tracker storage release destroys its pause container; it does not force a second report. There is one tracker allocation per native Robot and a new one on replacement, not a process-global tracker or a reusable retired allocation.

Engine destruction's earlier timer removal is the separate R-M8-TEARDOWN-CLOCK boundary. This report does not choose a clock-reset policy or change tracker arithmetic based on it.

## Current C# comparison

Paths under `cozmo-stack/src/Cozmo.Robot/`.

- `Behavior/FreeplayStack.cs` Create constructs tracker with engine nanosecond delegate, creates host AI and binds tracker.DestroyStorage to AI child+0x2C. It seeds pause state late. The already recovered scalar tracker implementation and this clock input are real; they do not supply constructor-time availability.
- BindNativeLifetime binds tracker.ForceUpdate to RobotLifetime offset-2 and AI.DestroyDirect to+0x264. It runs for current Robot if present and subscribes for future construction. A different subsequent EngineRobot explicitly emits “replacement AI/Freeplay owner construction is not built” and refuses rebinding. This correctly avoids pretending retired AI is fresh; it leaves the new Robot's recipient missing.
- `RobotLifetime.cs` Destroy has Member(-1,event), Member(-2,ForceUpdate), Member(-3,AbortAll), then later AI release. InitLifetime binds existing native/host recipients and invokes Engine.BindRobotLifetime. Without FreeplayStack no tracker callback is supplied at-2 and no AI owner at+0x264; existing meaningful lifetime MISSING remains.
- `CozmoEngine.cs` EngineRobot.Update invokes Engine.AIComponentUpdate at the native AI position if supplied. FreeplayStack installs this as ai.Update(robot).
- `Behavior/AIComponent.cs` Update currently calls Analyzer.Update then Helpers.Update only. It does not call tracker.Update; its minimal AI class does not own a tracker property.
- Instead `FreeplayStack.Tick` calls DataTracker.Update itself before Freeplay.Tick. User-driven cadence/omitted Tick can change deadline/report scheduling from the robot-tick contract. Moving the constructor alone would not repair this join.
- OffTreads pause delegate and OnCharger subscription capture the late tracker. Host Freeplay.Dispose removes these callbacks and calls Manager.Dispose; it does not force tracker itself. Avoiding a second host-dispose force is correct for native retirement. But the native Robot callback remains bound and storage lifetime is separate; host handle disposal cannot redefine the native tracker into an optional component.
- Multiple/retired facade callback conflicts were reported in `20261021-m10-consequence-owner-availability.md`, pending check. This report does not choose their public lifetime policy.

The missing owner is **EngineRobot's AI/tracker construction/update/retirement join**, rather than a missing tracker arithmetic implementation. A host facade may access an engine-owned component without owning its lifetime; the source establishes which lifetime must continue independently of the facade.

## One minimal owner-join proposal

After checking/adoption/approval, give each fresh EngineRobot a tracker at the native AI construction boundary, using engine cached timer and adopted ctor state. The bounded AI representation can own that child without claiming all AI algorithms built. Route existing native pause consequences to that current tracker from construction onward; retain actual activity initialization/selection transitions rather than reseeding just because a user creates Freeplay.

Tick it once at the native AI tracker step, with the same Robot gates and order. Bind its final ForceUpdate at Robot destructor head and storage retirement at AI+0x2C, once each. A new EngineRobot gets a new allocation/state/deadline; old callbacks are retired with their owning Robot. FreeplayStack exposes/reuses it and leaves its lifetime/update independent of facade Create/Dispose.

This is a concrete integration proposal, not inventory approval or a decision that overlapping user Freeplay handles are supported. No need to reproduce containers/thread machinery where the same events, timing/state and failure diagnostics result. Native time/reporting must not be silently dropped because it is chiefly telemetry; any scoped logging treatment belongs to the manager/operator rules, not an inferred exclusion here.

Independent future regression (not run): construct/connect a robot without a Freeplay handle; feed off-treads/platform/activity transitions through real packet/owner routes, advance accepted robot ticks across deadline, then remove Robot. Observe native tracker state and report schedule, final force before AbortAll and no second force on AI retirement. Create/dispose facade without removing Robot and show no tracker reset/update duplication. Recreate Robot and prove fresh tracker identity/deadline with no old callback mutation. Compare against adopted native event/state oracle, not a fake destructor that reads a test clock.

Remaining UNKNOWNs/approval needs: complete AI owner graph beyond tracker; supported host behavior-manager/activity owner before Freeplay construction; failure/partial-construction retirement beyond adopted bodies; public overlapping/early-disposal semantics. Manager must check findings and update/approve affected inventory before build. Operator input is needed only for a new public API/scope policy, not to infer that native tracker exists.

Research only; no tests, hardware, policy decision, manifest or production edits.

## Current records/evidence (80a357a2)

M15-015 — IMPLEMENTATION_GAP — Freeplay active-time tracker and its four pause sources

> FreeplayDataTracker constructor 0x0056EBD4

> FreeplayDataTracker::SendData 0x0056EC48

> FreeplayDataTracker::SetFreeplayPauseFlag 0x0056EEBC

> BehaviorManager::SetCurrentActivity 0x005A106C

> Robot::CheckAndUpdateTreadsState 0x005121F4

> Robot::SetOnChargerPlatform 0x00511DB0

> unity/scripts/csharp/Anki.Cozmo/HighLevelActivity.cs

M15-027 — IMPLEMENTATION_GAP — Disconnect persistence, ForceUpdate and AI/progression/inventory destruction

> 0x00695908..0x0069594A; 0x0056EEB8; 0x0056EC48..0x0056EDE2; 0x00569CB2..0x00569DAC; 0x005111BE..0x005111EA; 0x00511314..0x00511338; Needs stores time/connection state and writes device unless paused; DAS brackets/levels. ForceUpdate telemetry, AI children, progression/inventory are higher effects; payloads and recursive children UNKNOWN. Live Needs disconnect hook runs at T4 before teardown.

> Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1â€“T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt

> re-analysis/research/overnight/20261014-ov25-26-27-check.md: removal order 0x0052F2C4..0x0052F2FA; ~Robot head 0x005110D4..0x00511120; progression 0x005111BE..0x005111EE; inventory 0x00511314..0x00511338; AIComponent slots 0x00569CB2..0x00569DAC, explicit null 0x0051126A..0x0051127A.

M15-045 — IMPLEMENTATION_GAP — AIComponent teardown recursive recipients: BehaviorHelper IHelper types and callables, ObjectInteraction entries and ProcessInfo callables, BorderSegment release, SevereNeeds/RequestGame/AIWhiteboard handle inventories, Workout/ObjectInteraction/Whiteboard/Analyzer inner bodies

> re-analysis/research/overnight/20261014-ov25-26-27-check.md: 0x00569D38..0x00569D66; 0x0056A0AE..; 0x00569FD2..; 0x00569F9A; the inner bodies named.

> re-analysis/research/overnight/20261016-batch-check.md: member map 0x00569A88..0x00569B3E; BehaviorHelperComponent 0x00569D30..0x00569D7E; ~vector<BorderSegment> 0x00569F86..0x00569FB8; ProcessInfo tree 0x00569FBA..0x0056A006; ObjectInteractionCacheEntry 0x0056A06E..0x0056A13E; SevereNeeds 0x00572A1C..0x00572A36; AIWhiteboard 0x0056A32C..0x0056A37C; RequestGame 0x0056A1DC..0x0056A1FA; Workout 0x0056A140..0x0056A1D8.
