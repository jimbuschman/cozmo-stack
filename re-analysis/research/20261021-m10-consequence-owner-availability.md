# R-M10-OWNER-AVAILABILITY — classifier consequence composition

Request: `requests/20261021-m10-consequence-owner-availability.md`. Examined HEAD00a2d322. Coverage opened before comparison. Independent reading; no edits outside this report, no status change.

## Bounded composition table

| Recipient | When real owner exists | Actual binding / route | Removal/replacement | Finding |
| --- | --- | --- | --- | --- |
| Sensors/classifier | Every CozmoRobot construction | CozmoRobot ctor creates CozmoSensors; RouteToDevices→Sensors.Handle→OffTreads.Update on accepted RobotState | ResetDevices resets same sensor/classifier; seams retained | Available before ordinary packets; not dependent on Freeplay |
| World query/localization | Production robot owns LocalizationWorld and internal VisionSystem; OmitVisionForTest is an explicit test seam | VisionSystem installs AnyRemainingLocalizableObjects and ClearLocalizationOnTreads | Vision retained across EngineRobot recreation; Dispose does not clear these delegates | Ordinary world absence premise is stale; classify unbound seam separately |
| Carrying storage | EngineRobot constructs CarryingOwner; CozmoRobot receives it via CarryingOwnerConstructed | DockingSystem captures current CarryingOwner and installs NativeUnset; ManipulationSystem binds Sensors.Unattach... | Docking Dispose clears its NativeUnset only if still installed; captured owner does not rebind to recreated EngineRobot | Storage exists without Manipulation; full detach algorithm recipient does not |
| Classifier detach | Native target is Carrying.SetCarriedObjectAsUnattached(true) | OffTreads.UnattachCarriedObjectIfCarrying has no assignment; different Sensors property is assigned | No recipient to retire at this exact hook | Existing F2 omission remains even with Manipulation constructed |
| Action cancel | Active EngineRobot has real ActionList, independent of host Freeplay | OffTreads.ActionListCancel(-1) event has no production subscriber | A correct binding must resolve current EngineRobot, not capture old ActionList | Owner available; unbound join, not unavailable subsystem |
| Pause flag2 | AI/tracker host owner currently created by FreeplayStack.Create | OffTreads.SetFreeplayPauseFlagOffTreads → captured tracker | Dispose unconditionally nulls shared delegate; old handle can erase newer stack's binding | Available only after host stack creation; replacement unsafe on this field |
| Falling-needs | **Engine initialization already creates NeedsManager**, before RobotManager.Init | Sensors.NeedsActionCompleted event → FreeplayStack onNeedsAction → engine Needs.RegisterNeedsActionCompleted(Fall) | Event handler removed with stack; overlapping stacks both subscribe | Real Needs owner outside Freeplay; missing handler join outside Freeplay, duplicate inside overlap |
| Other host Manipulation callbacks | Explicit public ManipulationSystem construction | Installs sensor detach/world config/motion callbacks | Dispose only planners/follower/Docking; outer callbacks remain | Captured retired recipients can survive; do not confuse Docking's guarded cleanup with full facade cleanup |

## Evidence boundary

Reuse F2/F3 and native instructions in `20261021-m10-classifier-settling.md`; no classifier arithmetic, obstacle alias scan or recipient algorithm repeated. Native consequences remain:
- entering Falling: ActionList.Cancel(-1) before committed byte/report,0x00511FF0..0x0051208E;
- off-treads report before states2..6 carried detach, then world/platform/pause consequences,0x00512088..0x005121F8;
- pause flag2 at0x005121F4;
- separate FallingStopped intensity>1000: Needs action17 before DAS/report,0x005350AA..0x005350C2 and0x00535186..0x0053519C.

Native owners are source-backed engine/Robot components, not Freeplay user-handle lifetime. Existing M1/M15 initialization records and current EngineNeedsOwner bindings establish the relevant current ownership split. This report uses source instructions already recovered for recipients; it compares current host composition rather than re-extracting their algorithms.

## Current entry points and availability

All source paths below are relative to `cozmo-stack/src/`.

**Sensors and packet admission.** `Cozmo.Robot/CozmoRobot.cs` constructor creates Sensors at line300, before exposing the completed robot. Its RouteToDevices lines792ff returns for rejected RobotState and otherwise calls State, Motion, Camera and Sensors in order. Sensors.Handle's classifier call remains after copied IMU/raw state; it is not triggered only by the Freeplay tick. Sensors constructor binds the platform consequence immediately and supplies HostEngine/public event routing. ResetDevices lines840ff calls Sensors.ResetToConstructed on robot removal. OffTreads.ResetToConstructed expressly keeps subscribers and seams; it resets physical/head gates and state rather than replacing the object. Thus host callbacks that capture retired subsystem owners persist unless explicitly replaced/removed.

**World exists in ordinary supported construction.** CozmoRobot.LocalizationWorld is initialized as a field. Constructor lines409–412 constructs its one VisionSystem unless OmitVisionForTest; VisionSystem's constructor is internal, and the public Vision property throws only for that world-less unit-test composition. VisionSystem uses robot.LocalizationWorld, not a fresh world. Lines104–105 bind the classifier's two world seams. VisionSystem.Dispose detaches its packet/frame/calibration/removal subscriptions and conditionally replaces ChargerPlatformUpdate; it does not clear AnyRemainingLocalizableObjects/NothingLocalizableRemainsOnTreads. Therefore an ordinary live robot without Freeplay still has the query recipient. A user-disposed Vision handle leaves world-query delegates installed; that fact is not evidence the native world was retired. Whether public early Vision disposal is supported while the robot continues is an API scope question; do not declare it valid or exclude its effects without the operator.

**Manipulation is explicit host composition.** `Cozmo.Conformance/ManipTool.cs` connects, gets robot.Vision, then constructs ManipulationSystem before performing commands. FreeplayTool similarly constructs it; other basic robot routes need not. `Cozmo.Robot/Manipulation/ManipulationSystem.cs` constructor builds Docking, Configuration manager and Whiteboard and installs callbacks. It binds Sensors.UnattachCarriedObjectIfCarrying at57, not OffTreads.UnattachCarriedObjectIfCarrying. This confirms F2 with an available M12 owner in manipulation tools; creating the owner does not repair the distinct classifier hook.

Engine Carrying storage itself is created with EngineRobot, supplied through CarryingOwnerConstructed (`CozmoRobot.cs`:330) and reused by DockingSystem. Docking constructor captures current DockingOwner and CarryingOwner, assigns NativeUnset, subscribes handlers and installs guarded vision suppliers. Its Dispose removes its own handlers and clears its NativeUnset/suppliers only if still its binding. It does not rebuild those captures on EngineRobot replacement. EngineRobot recreation gives CozmoRobot a fresh CarryingOwner; it does not turn an old Docking facade's captured Carrying property into that fresh object.

**ActionList exists before Freeplay.** The EngineRobot owns the action list created on AddRobot; the earlier F2 read confirmed the classifier event has no source subscriber. Production source search again finds the ActionListCancel declaration/invocation only, with no += subscriber. No legitimate “no action owner available” reason explains this omission. Bind to the live owner at delivery time or to its tracked lifetime, preserving source order; this report changes no binding.

**Needs exists before Freeplay.** `Cozmo.Robot/EngineNeedsOwner.cs` InitializeNeedsOwner is called by CozmoEngine initialization through Experiments.InitBeforeNeeds before RobotManager.Init. It constructs NeedsManager.FromObb, assigns HostEngine/log/timer/RNG, attaches connection reads and registers InitAfterConnection/serial/disconnect/pause/tick recipients. FreeplayStack.Create defaults to robot.Engine.Needs. A different supplied manager goes through UseNeedsForOfflineTest, which rejects production engines. Consequently the old F3 concern must be phrased as **unbound falling event outside Freeplay**, not “no NeedsManager exists outside Freeplay”.

Sensors.Handle(FallingStopped) only invokes NeedsActionCompleted(17) when intensity>1000. FreeplayStack lines276–281 adds the concrete subscriber. Without a stack, the owner exists but this event has no current production subscriber. After stack.Dispose the event handler is removed even though engine Needs remains. Two concurrent stack handles both call the same Needs action; no ownership arbitration/exclusivity check is visible in Create. This is a bounded composition defect lead, not permission to redefine duplicate falls as acceptable.

**Tracker differs from Needs.** FreeplayStack creates a FreeplayDataTracker and AI owner. It seeds its pause set using current off-treads/platform values before clearing GameControl, and installs the off-treads/platform callbacks. That seeding recovers current pause flags, not prior time/history before creation. Its BindNativeLifetime explicitly diagnoses replacement AI/tracker construction unbuilt and refuses to bind the retired host owner to a different EngineRobot. Other tracker/pause delegates remain installed until host Dispose; refusal to bind lifetime is not comprehensive retirement of all callbacks.

## Retirement defects and missing-recipient diagnostics

These are bounded findings about inspected fields, not a new whole lifecycle audit.

1. F2 remains: classifier ActionListCancel and classifier UnattachCarriedObjectIfCarrying silently do nothing without subscribers. Their call sites do not report a dedicated missing recipient. Concrete diagnostics should identify M10-001 with M8-015 or M12-006 and which live recipient is unavailable; no guessed fallback.
2. Outside Freeplay, FallingStopped's real Needs owner is available but action17 is not dispatched. The absent event subscriber is silent. A missing-join diagnostic belongs to M10-011/M15 owner integration, not an assertion that Fall was applied.
3. Pause flag2 is optional only in the current host API shape. No recorded native exclusion makes tracker state optional. If no AI/tracker recipient is supplied, a diagnostic must make this M15-015 gap visible. Unknown intended public-host composition is an operator checkpoint.
4. FreeplayStack off-treads pause cleanup uses unconditional assignment=null. Creating stack A, then B, then disposing A clears B's field. The same file has conditional cleanup for RequestCurrentBehaviorEndImmediately, showing these are distinct policies; do not describe all callbacks as protected by identity.
5. ManipulationSystem.Dispose does not clear Sensors.Unattach..., World.BlockConfigurationManagerUpdate/UnsetCarryingObjects/CarriedPrimaryId, anonymous ObjectObserved/PoseStateChanged handlers or its other outer callbacks. A disposed facade's callbacks can remain captured and act on a later packet/frame. Disposing the old Docking owner can clear NativeUnset while the outer Sensors closure still points at that carrying storage; a later invocation may hit the visible unbound NativeUnset MISSING instead of the prior recipient. Some replacements share CarryingOwner, so exact corruption/state effect depends on replacement sequence; it is not all unconditional.
6. Vision Dispose guards platform-step replacement, but leaves classifier world-query delegates. World is robot-owned, so continued world-query state may be correct under a host worker-stop interpretation; intended public early-disposal semantics is UNKNOWN. There is no need to invent a second world or clear a still-owned robot world solely because its vision worker stopped.
7. Removal resets retained classifier state but keeps seams; old Manipulation/Freeplay captures need owner retirement/reconstruction. Existing Freeplay replacement MISSING is meaningful evidence of that limit; it is not a complete solution.

## Current manifest ownership

Current snapshot00a2d322:
- M10-001, IMPLEMENTATION_GAP: “Off-treads classifier CheckAndUpdateTreadsState: gate, inputs, thresholds, branches, 250 ms debounce commit and every consequence”.
- M10-011, IMPLEMENTATION_GAP: “Falling events: FallingStarted broadcast; FallingStopped NeedsAction 17 above 1000, DAS, broadcast”.
- M8-015, IMPLEMENTATION_GAP: “ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown”.
- M11-044, IMPLEMENTATION_GAP: “PotentialObjectsForLocalizingTo::Insert, UseDiscardedObservation, LocalizeRobot and the confirmed-object pose refresh”.
- M12-006, IMPLEMENTATION_GAP: “Letting go of a carried object leaves it Dirty where the lift left it”.
- M15-001, IMPLEMENTATION_GAP: “NeedsManager initialization, periodic update and need-bracket rules”.
- M15-015, IMPLEMENTATION_GAP: “Freeplay active-time tracker and its four pause sources”.

A binding correction does not certify those whole records. The current construction evidence narrows F3's availability premise; it does not settle recipient algorithms or exempt missing consequences.

## Minimal integration and independent regressions

Minimum composition scope is a robot/engine-owned recipient join, with real lifetime retirement, for cancel/detach/Fall. Reuse existing ActionList, Carrying and Needs owners; avoid making the join conditional on a user requesting Freeplay. Tracker's intended always-available host lifecycle still needs the operator/manager scope decision and actual source owner construction. No new compatibility policy is selected here.

Retire installed callbacks by owner identity, unsubscribe captured events, and reject/rebuild an expired facade on native Robot replacement rather than silently applying old state. This is an integration proposal, not an approved implementation inventory.

Independent regression scenarios, not run:
- Connect with normal production Vision/Needs and no Freeplay; feed accepted calibrated falling states while a real native action is queued. Assert cancellation precedes off-treads report and carrying detach precedes platform/pause state.
- Feed FallingStopped at/below/above1000 through packet routing with no Freeplay; observe actual engine Needs deltas once, before report. Repeating with one stack must not add a second delta.
- Create A then B host handles and dispose A; feed the same transitions. B remains recipient. Dispose B and observe the defined retired state/diagnostic, not a stale callback.
- Remove/recreate EngineRobot under retained Sensors/Vision; the next accepted packet reaches the fresh ActionList/Carrying/Needs composition, never old captures. Source-backed shared-world query remains consistent.
- Separate host composition tests from recipient algorithms. An observer assigned directly to a test delegate proves dispatch order only; actual state/traffic requires the real owners above.

Unknowns: operator-supported early disposal/overlapping facade policy; complete replacement constructors for AI/tracker/Manipulation; recipient algorithms' existing gaps. None authorizes optional native state or a “nothing happens” fallback. No code/tests/hardware, manifest edits, approvals or settlement.
