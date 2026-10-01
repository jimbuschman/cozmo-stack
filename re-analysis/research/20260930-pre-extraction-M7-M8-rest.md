# Q11 — pre-extraction of the M7/M8 records outside R-BEH2's 19-record build set

Date: 2026-10-01  
Binary: shipped ARMv7 `libcozmoEngine.so`  
Scope: the twelve current `IMPLEMENTATION_GAP` records which `R-BEH2.md` explicitly left open, including the unfinished remainders of M8-004 and M8-008. The corrections in `20260930-R-BEH2-pre-extraction-check.md` take precedence over the earlier extraction.

## Result

The binary settles the missing source for all twelve records. It does **not** make their managed production paths complete. The main seams still absent at HEAD are: the app-facing `MoodState` CLAD message; the all-actions completion callback; five exact reaction-lock tables and nine cross-layer installations; config-driven behavior construction and three local behavior bodies; the cliff/pickup/spark production bodies; the behavior-helper component; ExecuteBehavior message dispatch; and whiteboard message/render consumers. M7-015 and the behavior-changing portion of M7-021 are already built; M8-008's M7 callers provably ignore the action result, so it needs ordinary terminal completion, not a result-specific branch.

Float constants below are stated as their native binary32 bit patterns. Integer seconds used as doubles are exact at the stated values.

## M7-012 — Mood schema, decay graphs, action-result events, affectors, update and game output

> Current: `IMPLEMENTATION_GAP`. “The decay graphs, affectors, nine-emotion update, actionResultEmotionEvents map and the nine-value SendEmotionsToGame seam are built ... The engine broadcasts a MoodState message to the app ... this stack has no MoodState protocol type and no app-facing consumer ... so the wire broadcast is not wired.”

| step | address / source | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| Gate and collect | `0x0067b736..0x0067b77c` | Return if MoodManager `+0x12C` robot is null. Otherwise copy exactly nine 32-bit floats from `+0x18 + 0x20*i`, ordinal order Happy, Calm, Brave, Confident, Charged, Excited, Social, Winning, WantToPlay. | `MoodState.SendEmotionsToGame`, `Mood.cs:349-362`, has only an in-process event and no null-interface gate. |
| Wire | `0x0067b77e..0x0067b79c`; setter `0x00725ffc..0x00726032`; pack `0x00704f76..0x00704f9e`, `0x0070fb56..0x0070fb84` | Construct `MoodState`, then `MessageEngineToGame`, then `Robot::Broadcast`. Union tag is `uint16 0x0061`; payload is `uint8 count=9` followed by nine raw float32 values: 37-byte payload, 39 bytes including union tag. Pack failure follows CLAD buffer failure; no alternate semantic result. | Add the protocol type/serializer and connect it to `CozmoEngine.PostGameMessage` (`CozmoEngine.cs:1913`), preserving float32 rather than the current `double` seam. |
| Production order | `MoodManager::Update 0x0067b5d4`, call at `0x0067b6a4` | Update emotions first, broadcast last, once per mood update. | `FreeplaySystem.cs:131-134` has the correct relative order but stops at the local event. |

Tests must use a fixed 39-byte source-derived vector, including nontrivial float bit patterns; subscribing to `EmotionsBroadcast` and comparing `EmotionValues()` to itself is not a wire test.

## M7-014 — reaction-lock manager lifetime and concrete per-class lock tables

> Current: `IMPLEMENTATION_GAP`. “The manager side ... and the 13 recovered tables are built ... Two groups are not wired: (a) 9 cross-layer call sites ...; (b) five built behaviours ... whose exact tables are not recovered ... currently take the arbiter-wide fallback.”

| step | address | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| Install/remove rule | `0x005bce42..0x005bce62`; value test corrected to `0x005a283c..0x005a2846`; cleanup `0x005bd12a..0x005bd140`, `0x005bd470..0x005bd4aa` | Append `_behaviorLock`; for every one of 21 `{trigger,value}` pairs, nonzero disables and may stop a running reaction; remember the original name. Base `IBehavior::Stop` removes every remembered lock. A duplicate lock fails through the manager's verification path. | `BehaviorScope` / `BehaviorManager` (`IBehavior.cs:477-510`, `BehaviorManager.cs:210+`) own the lifetime. First-lock `EnabledStateChanged(robot,false)` at `0x005a28a8..0x005a28b4` is M10-derived row 4b. |
| ReactToImpact | data `0x00c73d36..0x00c73d5f`; install `0x00606200..0x00606216` | Bits by ordinal: `1111111110 11111111011`; only 9 and 18 remain enabled. | Replace fallback in Impact init. |
| DriveOffCharger | `0x00c672f0..0x00c67319`; `0x005c0b1c..0x005c0b2a` | Disable 0,1,2,5,8,9,20. | Wire in the M13 behavior. |
| Singing | `0x00c6f590..0x00c6f5b9`; `0x005eeb56..0x005eeb5e` | Disable 1,2,8,10,20. | Wire in M15 singing. |
| AcknowledgeCubeMoved | `0x00c72bb2..0x00c72bdb`; `0x00602236..0x00602242` | Disable only 8; notably not CubeMoved (1). | Replace fallback. |
| ReactToOnCharger | `0x00c74182..0x00c741ab`; `0x00606c9c..0x00606cb0` | Disable 1,2,3,4,5,7,8,10,19,20; not its own trigger 9. | Replace fallback. |
| Nine known-table users | existing 13 tables; native call sites in their respective class Init bodies | CubeLiftWorkout, PeekABoo, PutDownBlock, FistBump, Bouncer, GuardDog, Dance, EnrollFace and OnboardingShowCube install their recovered class table before starting work and rely on base stop cleanup. | Wire at each M12/M14/M15-owned Init entry; do not substitute `Scope.DisableReactions()` because it over-suppresses zero entries. |

No floats occur in the tables. Tests should compare all 42 bytes per table and exercise acquisition-to-base-stop lifetime; tests that instantiate the same C# `ReactionLockTable` as both input and expected data are circular.

## M7-015 — pickup/off-treads gating

> Current: `IMPLEMENTATION_GAP`. “ReactToPickup gates on robot+0x355/+0x338, whose semantic names are M7-021; the derived classifier is M10-owned.”

| step | address | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| State source | parser `0x0078df58`; constructor `0x005100ea`; commit `0x0051208e` | `robot+0x355` is the seven-valued `OffTreadsState`. | `Sensors.OffTreadsState`; classifier source remains M10-owned. |
| Charger source | init `0x005100c4`; store `0x00511c14`; extract/call `0x00512aae..0x00512ab4` | `robot+0x338` is the on-charger-contacts boolean. | `Sensors.OnChargerContacts`, `Sensors.cs:314+`. |
| Pickup gate | `0x00607ba8..0x00607c08` | Only continue while state is InAir and charger byte is zero. Charger true logs `BehaviorReactToPickup.OnCharger`, starts no action, returns terminal 2. The tests occur before animation/calibration selection. | `Behaviors.cs:445-455,525-535` matches this path. No float. |

This record's M7-owned behavior-changing path is built. It can close once the manifest treats the M10 classifier as its own separately owned path instead of an unresolved part of M7-015.

## M7-018 — shipped configuration and BehaviorClass factory binding

> Current: `IMPLEMENTATION_GAP`. “BehaviorClass (79) and BehaviorID (179) exist and the 178 shipped configs are enumerated, but there is no config-driven factory binding ... missing ... FistBump, Hiccup and ReactToSparked here.”

| step | address / source | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| Config load | OBB behavior JSON; `0x005bba74..0x005bbab4`; `0x0059c34c..0x0059c428` | Read string `behaviorClass`; empty config warns/skips; nonempty config goes to `CreateBehavior`; null construction is an error; verify executable behaviors after the map. | `BehaviorConfigCatalog` enumerates configs, but `FreeplayStack` still binds IDs in code. Add a factory over the shipped configs. |
| Factory | `0x0059c89a..0x0059c8a4`; success/failure `0x0059d350..0x0059d3fe` | Reject ordinal > `0x4E`; 79-way dispatch; pass robot and complete JSON to constructor; wrap shared pointer; unknown/unconstructable returns null/error. | Do not silently fall back to a generic behavior. |
| Relevant cases | `0x0059cc84..0x0059cca2`; `0x0059ce24..0x0059ce44`; `0x0059d2f2..0x0059d310` | `0x19` FistBump (0x158 bytes), `0x26` PlayAnimSequence (0x140), `0x4C` ReactToSparked (0x120). **Hiccup is not a separate class**: its config is PlayAnim with trigger `0xE1`. | Correct the current unresolved wording; implement FistBump and ReactToSparked, reuse PlayAnim for Hiccup. |
| FistBump gates/order | `0x005f1d98..0x005f2714` | Entry state depends on carried-object ID; >1.0 s persistently off-treads terminates. Face turn result `NO_FACE=0x0300000E` alone enters search. Search angles: `-0.2617994f=0xBE860A92`, `+0.5235988f=0x3F060A92`, tilt corrected to `0x3F1C61AA`; timers 1.0/2.0. Actions: request `0xC7`, idle `0xC6`, success `0xC9`, retry `0xC8`, left-hanging `0xCA`, each 60.0f=`0x42700000`. Bump thresholds are `0x3C0EFA35`, `0x3E32B8C2`, and 4000.0f=`0x457A0000`. First ended idle retries; second fails. Stop always restores motor power and resets trigger. | No production C# FistBump body at HEAD. Use the full state machine, not a one-animation stand-in. |
| ReactToSparked | `0x006097e0..0x00609866`; base update `0x005bda56..0x005bda62` | Always runnable; Init triggers mood event exact name `SparkPending` at current seconds; with no action, inherited update returns terminal 2 immediately. | Add body, then M15 spark source must own its trigger. |

The timed hiccup strategy is M10-owned and remains separate. Its shipped ranges are 300..3300 s, 5..10 hiccups, 4500..8000 ms spacing, 600 s cured delay; its 5.0f write is `0x40A00000`. Component semantic names on the `robot+0x264` paths remain UNKNOWN, but the offsets and control flow are settled in the checked extraction.

## M7-019 — concrete reaction state machines

> Current: `IMPLEMENTATION_GAP`. “Built and wired: [listed reactions]. Not built: ReactToCliff ... ReactToPickup ... ReactToSparked ... Existing ReactToCliff/ReactToPickup stand-ins play one animation and are not replaced.”

| step | address | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| Cliff streamline flags | `0x005bbd06`, `0x005bc200..0x005bc21e`, `0x005bccaa..0x005bccc2` | `IBehavior+0xD9` is optional JSON `alwaysStreamline`, default false. `+0xD8` is true only for a non-null **hard** active spark. Either true skips the cliff event/animation and goes directly to backup. | Stand-in lacks this state. |
| Cliff reaction | `0x00605108..0x00605132` | Record state `PlayingCliffReaction`; otherwise emit `robot.cliff_detected`, choose `0x13D` or `0x131` from `[[robot+0x264]+0x30]+0x14` (field name UNKNOWN; other value uses fallback trigger), and start lift-safe animation with callback `TransitionToBackingUp`, parameters `(trigger,1,true,...,60.0f=0x42700000)`. Direct backup call is at corrected `0x00605132`. | Replace one-animation `ReactBehavior` with the actual state machine. |
| Pickup retry | `0x00607820..0x00607d9c` | Face path uses tracked face to select `0x1A9/0xE6`; otherwise pickup/calibration path. After the relevant retry transition, deadline `+0x120 = now + RandDbl(3*x,6*x)` from `+0x11C`; exact arithmetic/order is in the checked extraction. `SayTextAction` is a required primitive. | Requires M14 face source and a result-bearing/terminal action seam; do not invent face availability. |
| Sparked | `0x006097f8..0x00609866` | Trigger `SparkPending`, then terminal 2; runnable true. | Body can be M7, but its live `ReactionTrigger.Sparked` producer is M15. |

The state-name helper is owned below. UNKNOWN remains only the semantic name of the AI-state selector used for the two cliff triggers; its load, values and resulting branch are exact.

## M7-020 — mood event production and action completion

> Current: `IMPLEMENTATION_GAP`. “The ... map, clock, penalty, completion-enable set and nine-value output are built. HandleActionEnded has no live caller ... app-facing MoodState broadcast is not wired.”

| step | address | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| Register | `0x0067aee8..0x0067af38`; bridge `0x0053f888..0x0053f898` | If robot nonnull, independently of external-interface availability, bind `MoodManager::HandleActionEnded` and register it with `ActionList`/`ActionWatcher`; save integer handle at `+0x158`. | Add a production all-actions completion subscription; `Mood.cs:341` currently has no caller. |
| Invoke | `0x00541bd8..0x00541c00`; `0x0053f5dc..0x0053f5e4`; `0x0054187e..0x005418de` | Queue complete 0x40-byte `RobotCompletedAction`; after action queues update, invoke every all-actions callback, then pop. | Wire from the real action runner, not a test-only direct call. |
| Consume | `0x0067b320..0x0067b354`; one-shot suppression tail `0x008cd6ac` | If tag is in `+0x140`, erase it and return. Else key map by action type at `+4` and result category (high byte of result at `+8`), then trigger mapped event. | `Mood.cs:341-345` has the map behavior; ensure suppression erase happens before lookup. |
| Unregister | `0x0067ae18..0x0067ae30` | Destructor unregisters saved handle and clears it. | Subscription lifetime must follow MoodState lifetime. |

First-event time sentinel is `FLT_MAX=0x7F7FFFFF`. Tests must drive a real completed action through the watcher and assert a source-fixed event, not call `HandleActionEnded` directly (the present `M7BehaviorTests.cs:206+` test is a seam test).

## M7-021 — live robot fields, WaitForLambda and state-name helper

> Current: `IMPLEMENTATION_GAP`. “Built: robot+0x338 ... seven-valued OffTreadsState ... lift angle ... filtered accel ... WaitForLambdaAction ... Open: the state-name helper ... and its M7 live users ...”

| step | address | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| State-name store/log | `0x005c0ca8`; read corrected to `0x005bdb20`; name read `0x005bdbd0+`; log `0x005c0d4c..0x005c0d68` | Store the new state string at `IBehavior+0x58`; emit channel `Behaviors`, event `Behavior.TransitionToState`, using behavior name at `+0x48`. This is diagnostic. | No equivalent helper; ReactToCliff and DriveOffCharger use local enums. |
| Live users | `0x00604f90`, `0x00605110`; `0x005c0b74`, `0x005c0be2`, `0x005c0de0` | Cliff and DriveOffCharger call it at transitions. | Add only if exact logging/state introspection is required. |

The current manifest now says a full scan found no behavior-changing `+0x58` reader, but the checker report did not reproduce that scan. Re-run the xref scan during integration. Until then: behavior-changing effect **UNKNOWN**; proven effect is the stored string plus log. The other fields and pickup gate are already built and should not be reopened.

## M8-004 — zero default score and chooser ownership

> Current: `IMPLEMENTATION_GAP`. “Remove the invented in-code scores ... and their use by BehaviorManager.ChooseAndSwitch, and drive selection from the engine's activity chooser ... Blocked on M8-013 ... Whether the default BehaviorManager can run entirely on the chooser path is UNKNOWN.”

| step | address | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| Default/evaluate | `0x005bbd1c..0x005bbd28`; `0x005beec2..0x005beed4` | Write +0x100/+0x104 zero. If mood-scorer vector empty, return +0x100. Optional JSON `flatScore` is the only nonzero writer (`0x005bc488..0x005bc4e0`). Bits are `0x00000000` float32. | Remove PlayAnim 1.0, React/Stepped 5.0, Arbitrary 1.0, Singing 1.0 once production dispatch is converted. |
| Selection | chooser `0x0060a44a`; reaction map paths | Scoring chooser skips score `<=0`; activities/reaction map choose candidates. There is no engine analogue of ranking every code-built behavior. | `BehaviorManager.ChooseAndSwitch` (`BehaviorManager.cs:436+`) is the divergent production owner. |

M8-013 below settles the missing message caller. The remaining UNKNOWN is architectural: trace every current `ChooseAndSwitch` caller before deleting it, and give each one an engine-owned activity/reaction replacement.

## M8-008 — calibration timeout completion

> Current: `IMPLEMENTATION_GAP`. “The 30.0 s timeout is built ... Remaining: the engine fails the action with result 0x03000018 ... WaitUntil carries no action result.”

| step | address | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| Timeout | `0x00540d1c..0x00540f00`; thunk `0x0052b0c2` | Arm from -1.0f=`0xBF800000`; deadline uses 30.0f=`0x41F00000`; on expiry construct terminal `0x03000018` in r5 (`0x00540e80..0x00540e82`), pass it at `0x00540ef4`, return at `0x00540f00`. | `SteppedBehavior.cs:651-674` has 30 s and log but only Boolean success. |
| Four M7 consumers | corrected constructor call sites `0x00607c9a`, `0x0060808c`, `0x00608658`, `0x00608892`; adapter `0x005bf8f4..0x005bf8fc` | Pickup, PlacedOnSlope and ReturnedToTreads use empty callbacks. RobotOnBack adapter discards the entire completion message and branches only on robot state. No caller checks `0x03000018`. | Preserve terminal completion/retry order. A result-specific M7 branch would be invented. |

Therefore the record can be settled without exposing `0x03000018` to these four M7 behaviors if the managed action runner completes them at 30 s with the same ordering. A general result-bearing action API belongs to the framework but is not behaviorally observed by this caller set.

## M8-011 — SmartDelegateToHelper and helper stack

> Current: `IMPLEMENTATION_GAP`. “The ten Smart* helpers ... are built; SmartDelegateToHelper is not ... To build: the component and its vector/callback stack runtime, plus the IHelper subclasses and factory Create* methods.”

| step | address | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| Construct/tick | `0x00569aa4..0x00569ab2`; `0x00569f3c..0x00569f40` | Allocate 0x48-byte component at `AIComponent+0x10`; every AI tick calls Update. | No component exists. `BehaviorScope.SmartDelegateToHelper` throws at `IBehavior.cs:528-531`. |
| Delegate | `0x0056dad8..0x0056db37` | Clear callbacks, copy success then failure callbacks; only an empty stack accepts the helper. Push and snapshot world-origin ID, return 1; nonempty returns 0. | Build exact Boolean failure and callback ordering. |
| Push/update | `0x0056db1e`; `0x0056dc0c..0x0056dc62`; `0x0056de74..0x0056e15c` | Initialize helper before append; append at top; update immediately. Each update handles origin change, delegate success/failure, active update and possible sub-helper push; when top stops, stop(true), pop, resume previous. Empty stack copies the appropriate callback, clears maintenance vars, then invokes it. | Needs component-owned vector, callbacks and current origin. |
| Cancellation/cleanup | `0x0056dcc0..0x0056dcd6`; `0x0056dce6..0x0056dd8c`; `0x0056de26..0x0056de66` | Stop-without-callback succeeds only for bottom helper; clear top down, firstIteration flag only on first stop; 1000-iteration overflow is fatal/logged. Inactive helpers may cancel delegates above them before active update. | Tests must cover nested cancellation and callback non-delivery. |
| Factory | `0x005b54ec`; create entries `0x005b54f0`, `0x005b553c`, `0x005b5588`, `0x005b55c8`, `0x005b5618`, `0x005b5668` | Factory creates DriveTo, PickupBlock, PlaceBlock, PlaceRelObject, RollBlock, SearchForBlock helpers, then `AddHelperToComponent 0x0056da8a` transfers ownership. Bodies are not read in the earlier extraction. | These helper subclasses remain **UNKNOWN** and must be extracted before implementation; do not stub them. |

Layout is factory `+0x00`, vector `+0x04/+0x08/+0x0C`, success `+0x10`, failure `+0x28`, world origin `+0x40`; `+0x44` remains UNKNOWN/padding. No floats in the component runtime.

## M8-013 — chooser dispatch and ExecuteBehavior wire

> Current: `IMPLEMENTATION_GAP`. “The choosers are built. SelectionChooser.RequestBehavior has no production caller because ... HandleExecuteBehavior and the ExecuteBehaviorByID/ByExecutableType message are ... unbuilt.”

| step | address | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| Subscribe | `0x0060a87a..0x0060a93e` | With external interface, subscribe one handler to tag `0x94` ID and `0x93` executable type; retain both handles. Still resolve Wait ID `0xB2` without interface. | Add subscriptions during `SelectionChooser` production construction. |
| Wire | native pack/unpack `0x00738ba8..0x00738dac` | Both payloads are exactly 5 bytes: selector byte, then signed int32 `numRuns`; no C++ padding. | Add/use M2 types, then resolve through current behavior container. |
| Dispatch | corrected: ID `0x0060aa6e..0x0060aa88`; executable `0x0060aaae..0x0060aac8`; unknown `0x0060ab28..0x0060ab7a` | Resolve selector; store `numRuns` even on miss. Miss selects null and warns. Unknown tag logs/errors and selects null. If selection changes, disable old analyzer process then enable new; finally replace shared pointer at `0x0060ac2c..0x0060ac44`. | Call `SelectionChooser.RequestBehavior` (`Activities.cs:363`) from real dispatch, not tests. Analyzer ownership also needs a managed seam. |
| Consume | `0x0060ad64..0x0060ae6e` | Requested behavior, then Wait fallback; decrement run count once on running→stopped edge; -1 unlimited; 0 never returned. | Existing chooser algorithm is built. |

No floats occur in selection dispatch. Tests should inject literal five-byte messages through the real protocol dispatcher and observe chooser state/activity selection; direct `RequestBehavior` tests (`BehaviorFrameworkTests.cs:911+`) are seam-only.

## M8-014 — AIWhiteboard handlers and beacon rendering

> Current: `IMPLEMENTATION_GAP`. “Init's three registrations ... the no-op Update and AddBeacon are built. UpdateBeaconRender ... and the three handler bodies are unowned.”

| step | address | what it does; gates, order and failure | C# entry / disposition |
|---|---|---|---|
| Init | `0x0056a394..0x0056a5c4` | If external interface exists, register tags 68 RobotObservedObject, 69 RobotObservedPossibleObject, 53 RobotOffTreadsStateChanged in that order; otherwise warn. | `AIWhiteboard.cs:69-100` has registrations and tag-53 timestamp seam. |
| Render | `0x0056aa3c..0x0056abff`; Add call `0x0056c3de` | Erase previous VizManager segments, then for every beacon draw three XY circles. Correct constants: 2500=`0x451C4000`; 10°=`0x3E32B8C2`; 50=`0x42480000`; pi=`0x40490FDB`; 35=`0x420C0000`; 1e-5=`0x3727C5AC`; 30=`0x41F00000`; -0.5=`0xBF000000`; -1=`0xBF800000`. Tilt comparison uses `Anki::operator<(Radians,Radians)`. Pose base returned by `0x004ea398` is UNKNOWN. | `AIWhiteboard.cs:117-126` only raises `BeaconRenderUpdated`; it does not reproduce render output. |
| Handlers | functors `0x0056cbd6`, `0x0056cc4e`, `0x0056ccf2..0x0056ccf6` | Tag 53 records current seconds at `+0x48`. Object/possible-object handlers mutate whiteboard state used by beacon/object logic; exact bodies must be kept with their native rows. | Current external interface only subscribes; production message delivery and exact object mutations remain absent. |

The pose-base helper is the only binary-unsettled rendering input and must remain UNKNOWN. A test that merely checks that `BeaconRenderUpdated` fired is not evidence for the VizManager geometry.

## Build ordering and ownership

1. M2 protocol: `MoodState`, `ExecuteBehaviorByID`, `ExecuteBehaviorByExecutableType`, plus dispatch plumbing.
2. M8 framework: helper component/runtime, selection handler, action-watcher completion seam.
3. M7: exact five reaction tables; config factory; FistBump/ReactToSparked; Cliff/Pickup bodies; mood callback and broadcast.
4. Cross-layer owners: M10 classifier/strategies, M12/M14/M15 behavior callers, M14 face source, M15 spark and singing.
5. M11/M12 visualization owner: whiteboard handler bodies/VizManager, preserving the UNKNOWN pose-base identity until extracted.

No record above may become `EXACT_SOURCE` merely because its isolated method tests pass. Settlement requires the listed production caller, wire path, failure behavior and lifecycle to be live.
