# Re-audit of Sonnet-only EXACT_SOURCE records (M10, M15, M14)

Answers the operator request of 2026-09-30, using the five-step method and answer format of
`requests/20260929-audit-calibration.md`.

I read each current manifest record and its inventory rows, reopened every manifest-cited native function/range in
`resources/lib/armeabi-v7a/libcozmoEngine.so`, compared binary32 literals at their native width, followed every
`// fidelity:` tag for the record through the constructed C# path, and inspected the named tests. Ghidra output was
used only to navigate; the instruction addresses below were checked in the ELF. I did not run tests, because this is
the read-only research/extraction lane.

Verdict meaning follows the calibration request: **HOLDS** means the complete claim checked here is source-backed;
**FAILS** is a direct behavioral contradiction; **PARTIAL** means the cited narrow fact is right but the settled
record does not own an exact complete production path.

## M10-derived

### M10-002 — FAILS

- The engine constructor stores the gyro threshold as binary32 `0x3E32B8C2` at
  `0x0063DAB0 movw r1,#0xB8C2` / `0x0063DAB6 movt r1,#0x3E32`. The live comparison is
  `vcmpe.f32` at `0x0063E4B6..0x0063E4C2`. `UnexpectedMovement.cs:136` instead uses
  `0.174533f`, binary32 `0x3E32B8C7`. Inputs in the five-ULP interval take different quiet/active branches and can
  change the counter increment, type, and eventual `count > 10` report.
- The remaining claimed detector structure does match the native range: physical/tag-lock/picking/reset gates
  `0x0063E3B4..0x0063E426`, quiet and active rules `0x0063E428..0x0063E5DA`, guarded decrement and sums
  `0x0063E51C..0x0063E588`, and fire/reset at `0x0063E5B2..0x0063E68C` correspond to
  `UnexpectedMovement.cs:208-250`. The tests exercise ordinary values and copy the rounded constant indirectly;
  neither named test probes the native threshold bits or the boundary.

### M10-005 — HOLDS

`Robot::CheckAndUpdateTreadsState` obtains `BaseStationTimer::GetCurrentTimeStamp` at `0x00511E2A..0x00511E36`,
the timer computes `u32(seconds*1000)` at `0x0084BCC0..0x0084BCD0`, and its only update is the tick-start elapsed
time path (`0x0065B3B6..0x0065B420`, `0x004ED626`); `Sensors.cs:780-788` passes
`Engine.Timer.TimeStampMs`, whose value is updated once at tick start. The manifest's generic `DerivedStateTests`
label is not a focused oracle, but the production clock path itself holds.

### M10-006 — HOLDS

The native `AnimationState.tag != 0` test and conditional BODY-lock return at `0x0063E3BA..0x0063E3DC` match
`UnexpectedMovement.cs:210-213`; `Sensors.cs:804-811` supplies the live animation tag and BODY lock. The named test
derives its expected gate from the source rule rather than from a duplicate implementation.

### M10-009 — FAILS

- The source claim is a constant-false production fact: AIComponent+4 is zeroed at `0x00569A80`; the
  `StrategyObstacleDetected` predicate at `0x006143CA` only reads it, and the cited whole-binary write scan found no
  setter. The C# record location instead exposes a public writable seam,
  `BehaviorContext.ObstacleDetected` (`IBehavior.cs:45-59`), and the strategy consumes it. The repository itself
  demonstrates a raising path at `DerivedStateTests.cs:1443` by assigning `() => true`. Therefore the current title
  “Nothing in this build ever raises” is false of the C# build, even though the default `FreeplayStack` leaves it
  null.
- `NothingRaisesTheObstacleStrategyByItself` only asserts the default null value (`DerivedStateTests.cs:396-407`);
  it does not prove the absence of writers and does not guard the source property the record claims.

### M10-010 — PARTIAL

- The normal shipped-message path holds: the Robot constructor's zero at `0x0050FC36`, the only setter at
  `0x0051397A`, and `HandleFirmwareVersion` passing `json["sim"].isNull()` at `0x00536980` match
  `CozmoEngine.cs:897-903,927-936`; the physical gates are fed live to off-treads and unexpected movement.
- The same handler explicitly defers its parse-failure and non-object-root results as `MISSING`
  (`CozmoEngine.cs:907-925`). Those are behavior-changing failure branches in the production entry point, but the
  M10-010 evidence and tests do not establish what the engine does there. Consequently the valid-object fact holds,
  but the handler path is not completely owned by this EXACT_SOURCE record.

### M10-011 — PARTIAL

- Native `HandleFallingStopped` compares the binary32 intensity with 1000 at `0x005350AA..0x005350C2`, directly
  calls `NeedsManager::RegisterNeedsActionCompleted(17)`, emits DAS, and only then broadcasts FallingStopped at
  `0x00535186..0x0053519C`. The broadcast ordering in `Sensors.cs:735-749` is shaped correctly, but the needs call is
  only `NeedsActionCompleted?.Invoke(17)` (`Sensors.cs:746`). Its declaration explicitly says “Nothing in this stack
  subscribes yet” (`Sensors.cs:118-124`), and a whole-source search finds no subscription. Thus every hard-fall needs
  reward is dropped on the live path.
- The manifest names `DerivedStateTests.FallingNeedsTheFlagAndSomethingElse`, which tests the off-treads classifier,
  not these engine-to-game messages. `TheImpactBehaviourRecordsTheAlwaysHandleTags` checks the reaction's local
  flags but never asserts a NeedsManager update. Neither test catches the missing consumer.

### M10-012 — HOLDS

The constructor clears exactly the six binary32 filter words at Robot+0x378..+0x38F
(`0x005100E0..0x005100FE`), and the raw accel/gyro stores occur in UFRS at
`0x005129A4..0x005129BE`; `OffTreads.cs:83-96,143-182` starts the filter fields at zero and feeds raw values only from
the RobotState update (apart from lifecycle reset to the constructed state). The named filter test uses the recovered
coefficients; this record's narrower initialization/writer claim holds.

**M10 comparison with the Sonnet audit:** Sonnet reported 7/7. This re-audit finds **3 HOLDS, 2 FAILS, and 2
PARTIAL**. The missed patterns are one rounded binary32 threshold, one public stand-in for a native constant-false
flag, one explicitly untraced failure path, and one source-backed producer whose live consumer is absent.

## M15-freeplay

### M15-004 — FAILS

- `NeedsState::GetDecayMultipliers` is binary32 end to end: level and threshold are compared with `vcmpe.f32` at
  `0x0069C270`, and multipliers use `vmul.f32` at `0x0069C2A4`. `NumDamagedPartsForRepairLevel` likewise compares
  binary32 values at `0x0069CCC4..0x0069CCD2`. The C# configs, levels, thresholds, multiplier, and damaged-part
  argument are all `double` (`Needs.cs:104-108,115,125-135,156-158,166-179,340-364`). A JSON decimal is therefore
  compared at binary64 rather than after the engine's binary32 conversion. Values around any configured threshold
  can select a different modifier bracket or damaged-part count.
- The first-descending-match control flow itself is correct. The test (`FreeplayTests.cs:345-380`) constructs another
  double table and checks values far from a floating boundary to three decimals; its expected values come from the
  C#-width model, not from native float bit patterns.

### M15-007 — HOLDS

`IActivityStrategy::WantsToStart` checks the feature gate first at `0x005B52A8..0x005B52BC`,
`CozmoFeatureGate::IsFeatureEnabled` is the cited lookup at `0x006A679C`, and the sole `boredomMultiplier` string use
is the `BehaviorPounceOnMotion` constructor (`0x005F7F91..0x005F8384`), not a chooser read. `Activities.cs:576-586`
and the live `FeatureGates.Load` at `FreeplayStack.cs:114-120` match. The named test uses the shipped feature config
and checks both enabled and disabled cases.

### M15-008 — FAILS

- The four face/cube choices in `CalculateDesiredActivityFromObjects` (`0x005AE08C..0x005AE0AA`) match
  `FreeplaySystem.cs:112-119`.
- The behavior-owned needs path does not. Native `TransitionToObjectPickedUp` takes the stack branch at
  `0x005DF4EA..0x005DF4F2` **before** the floor-placement `PickupCube` call at
  `0x005DF59C..0x005DF5A0`. C# calls `NeedActionCompleted("PickupCube")` before deciding whether it will stack or
  floor-place (`CubeGameBehaviors.cs:1030-1042`), so the stack route reports a reward the engine does not report
  there. The C# stack-success callback then reports no `StackCube` action (`CubeGameBehaviors.cs:1045-1055`).
- Native `ActivityGatherCubes::Update` directly registers `GatherCubes` at
  `0x005AF2EC..0x005AF2F8` once all cubes are in beacons; no corresponding activity or call exists in the C# path.
  Additional native call sites named by the source comment—FistBump, PeekABoo, TrackLaser and GuardDog—have no C#
  behavior call sites. The implementation has only the calls found by `BehaviorNeedsActions.Complete`/the helper,
  despite its own `Needs.cs:17-32` claiming all sixteen.
- The named test validates config extraction and the helper in isolation (`FreeplayTests.cs:129-163`); it never
  executes the native call-site set, the explorer's stack branch, or GatherCubes. Its “behaviours report” conclusion
  is therefore broader than its assertions.

### M15-009 — PARTIAL

- The narrow selector holds: `FireEmotionEvents` calls `AreAllCubesInBeacons` and selects the 29-byte
  `HikingBroughtLastCubeToBeacon` or 25-byte `HikingBroughtCubeToBeacon` strings at
  `0x005E002C..0x005E0096`; `CubeGameBehaviors.cs:1092-1099` uses those exact names and condition after a successful
  floor placement.
- The settled tag is on the entire `BringCubeToBeaconBehavior` (`CubeGameBehaviors.cs:980`), but the route that
  determines whether that source-backed event is reached contains an admitted `LOCAL` free-pose search
  (`CubeGameBehaviors.cs:1058-1071`) and an invented stand-pose calculation
  (`CubeGameBehaviors.cs:1074-1089`). Those change placement success, timing, and therefore whether the event fires.
  No record owns these substitutions. The record proves event names inside an unverified larger path, not the live
  event behavior.
- `NavigationTests.ThinkAboutBeaconsThenBringCubeToBeaconPlacesTheCubeInside` asserts placement but never asserts an
  emotion event; `TheBeaconAndStackConstantsAreTheEnginesOwn` only copies the two strings.

### M15-010 — HOLDS

`GetLocatedObjectByIdHelper` returns null at `0x006022B4..0x006022B8`, the engine warns and reaches the no-action
return at `0x00602370`; base `IBehavior::UpdateInternal` returns complete when its action pointer is null
(`0x005BDA56..0x005BDA62`). `CubeReactions.cs:316-330` warns and `Finish()`es without starting a turn or reaction.
The manifest's `BehaviorTests` label is too broad to be useful, but the production failure result itself holds.

### M15-011 — FAILS

- `SelectNewBeacon` gets the Robot pose at `0x005E5F1A`, invokes the Pose3d copy at
  `0x005E5F1E..0x005E5F24`, loads the configured radius at `0x005E5F28`, and passes the complete copied pose to
  `AIWhiteboard::AddBeacon` at `0x005E5F30`. C# reconstructs a planar pose instead
  (`CubeGameBehaviors.cs:961-964`): it discards pitch/roll and the pose hierarchy by using only
  `Mat3.AboutZ(robot.AngleAroundZ)`, and forcibly replaces translation Z with zero. Beacon coordinates differ for a
  non-planar or parented Robot pose.
- The named navigation test starts from an identity, Z=0 robot pose and checks only the radius and later placement
  (`NavigationTests.cs:536-558`), so it masks every discarded component.

### M15-012 — PARTIAL

- The two highlighted integers hold: `WaitForImagesAction(...,2,VisionMode 1)` is built at
  `0x005C8234..0x005C824A`, and CantHandleTallStack uses trigger `0x1B` at `0x005ED148..0x005ED14E`.
- The record effect claims the post-put-down action sequence, and that path is not exact. Native uses head angle
  binary32 `0xBEB2B8C2` and tolerance `0x3D0EFA35` at `0x005C81A6..0x005C81BE`; C# sends rounded
  `-0.349066f` (`0xBEB2B8C7`) at `ManipulationBehaviors.cs:135`. Native places the head and -30 mm drive in one
  `CompoundActionParallel` (`0x005C8196..0x005C822A`); C# starts the head command without awaiting it and sequences
  only the drive (`ManipulationBehaviors.cs:135-142`). Native optionally appends the 0x199 keep-alive wrapped in
  `TurnTowardsFaceWrapperAction` at `0x005C8264..0x005C82CC`; C# explicitly skips that turn as `DEFERRED`
  (`ManipulationBehaviors.cs:145`). The earlier release is also labeled `INFERRED` at lines 114-117.
- The action test accepts a `1e-4` head-angle tolerance and explicitly passes with the deferred path
  (`ManipulationTests.cs:503-520`). The constants test is source-derived only for 2 and 0x1B; neither proves the
  claimed sequence.

### M15-013 — PARTIAL

- The source-backed construction facts hold: both activity constructors parse and discard `activityPriority`
  (`0x005AD5CC..0x005AD69C`, `0x005B23DC..0x005B252C`), and `Activities.cs:760-820` preserves the shipped JSON
  array order and desired-name fields.
- A second M15-013 tag covers live `PickNewActivity` (`FreeplaySystem.cs:319-345`). Its own documentation says the
  exact interleaving is `INFERRED` (lines 312-317), and the implementation invents a `KeepsPriority` partition that
  moves spark/needs activities ahead of the desired object-derived activity (lines 327-333). Neither manifest
  evidence function establishes that selection algorithm. Thus the loader portion holds, but the behavior-changing
  production ordering claimed by the record remains UNKNOWN.
- `FreeplayTests` asserts the shipped JSON order and zeroed C# `Priority` values (`FreeplayTests.cs:937-970`); it does
  not compare `PickNewActivity` against `ActivityFreeplay::GetDesiredActiveBehaviorInternal`.

### M15-015 — FAILS

- Native accumulated time and resume timestamps are integer nanoseconds: `SendData` obtains the u64 clock at
  `0x0056EC50..0x0056EC5C` and uses 64-bit subtract/add at `0x0056EC66..0x0056EC84`; pause does the same at
  `0x0056EED6..0x0056EF14`, and resume stores a fresh u64 nanosecond value in `ClearFreeplayPauseFlag`
  (`0x0056F082` onward). Only the next-send deadline is binary32 seconds (`0x0056EBF2..0x0056EC04`). C# represents
  all three as binary64 seconds (`FreeplayDataTracker.cs:34-44,83-91,123-146`). Segment accumulation and rounding
  can therefore differ at sub-second/report boundaries; this is not the engine's numeric path.
- The live tracker is also attached after the freeplay stack is created and only subscribes to future OffTreads and
  OnCharger transitions (`FreeplayStack.cs:157-168`). It seeds neither the current off-treads state nor
  `Sensors.OnChargerPlatform`; if either condition already holds, native AIComponent's tracker has received the
  Robot transitions, while the C# tracker begins unpaused. `GameControl` is unconditionally cleared as an asserted
  “equivalent end state,” rather than being driven by the native `SetCurrentActivity` lifecycle.
- The tests drive the same double clock at whole-second values (`FreeplayTests.cs:439-505`) and construct the tracker
  directly, so they cannot detect nanosecond-width differences or missed initial live pause state.

### M15-017 — HOLDS

The 0x74 size (`0x007848C8`), Pack order (`0x007847D2..0x00784878`), Unpack order
(`0x007846B4..0x0078475C`), v1-v4 sizes/unpackers (`0x0078606E/0x00785ECC`,
`0x00785B5A/0x00785998`, `0x007855C4/0x007853E8`, `0x00784F82/0x00784D8C`), and conversion dispatch
(`0x00699DD0..0x0069A192`) match `Needs.cs:371-449` field-for-field, including the 32 one-byte booleans and zeroed
missing tails. The tests include hand-built blobs at native offsets (`FreeplayTests.cs:1477-1500,1643-1715`), so
the important layout oracle is independent of `Pack` rather than only circular round-trip coverage.

**M15 comparison with the Sonnet audit:** Sonnet reported 10/10. This re-audit finds **3 HOLDS, 4 FAILS, and 3
PARTIAL**. The missed patterns are binary32 widened to double, incomplete needs-action call sites, exact event
strings embedded in a local placement algorithm, a flattened Pose3d, a deferred action tail, an explicitly inferred
selection order, and a nanosecond tracker replaced by double seconds with incomplete live initialization.

## M14-faces

### M14-001 — FAILS

- The native geometry is binary32 throughout. `GetIntraEyeDistance` and `UpdateTranslation`
  (`0x0087DC68..0x0087DDB0`, `0x0087DE24..0x0087E068`) use `vsub/vmul/vadd/vdiv.f32`, binary32
  `sqrt`/cos results, the `0x3727C5AC` epsilon, the 6.0 floor, and `0x42780000` (62.0). Rectangle fields and
  overlap are `Rectangle<float>`. C# makes rectangles, eye points, roll, camera calibration, every intermediate,
  epsilon comparison, and translation binary64 (`Faces.cs:12-22,31,73-77,98-104,124-149`). Threshold decisions
  and final coordinates can differ bit-for-bit from the engine.
- The reachable id lookup, new-entry-only rotating gate, timestamp-regression continuation, preserved translation
  for a no-parts known face, and 15000 ms expiry do otherwise match `0x004F43D6..0x004F47BE` and
  `0x004F5380..0x004F54A8`. The tests assert rounded constants or ordinary geometry and never compare a native
  binary32 result; `Assert.Equal(1e-5, ...)` copies the C# literal.

### M14-002 — HOLDS

`TurnTowardsFaceAction` stores 10 at +0x188 (`0x0054B798..0x0054B79E`) and
`IVisuallyVerifyAction` stores 10 at +0x8C (`0x0056873E`); `FaceActions.cs:126-134,343-355` uses the same frame
count for both. `FaceTests.TheFaceActionConstantsAreTheEnginesOwn` directly asserts this source-derived integer.

### M14-003 — FAILS

- Three native constants are not bit-exact in C#: pan/tilt tolerance is `0x3D0EFA35` at
  `0x005646BC..0x005646D8`, but `0.0349066` would encode as `0x3D0EFA39`; maximum head angle is
  `0x3F46D3F2` at `0x005646DC..0x005646E8`, but `0.776672` rounds to `0x3F46D3FA`; the sound threshold is
  `0x3E32B8C2` at `0x00564722..0x0056473E`, while `0.174533` rounds to `0x3E32B8C7`. C# stores all of them as
  `double` (`FaceActions.cs:220-256`) and performs tracking calculations in binary64.
- Native tracking is `CheckIfDone` on the action-list tick; it has no self-timer. C# runs an independent wall-clock
  loop (`DateTime.UtcNow`) and `Task.Delay(60, CancellationToken.None)` at `FaceActions.cs:293-327`. It is neither
  synchronized to the engine tick nor cancelled during the delay. The source issues a command and returns to the
  action list; this loop may recompute/send at different moments or once after cancellation.
- C# also clamps both derived speeds to an invented minimum 0.01 (`FaceActions.cs:321-322`); the cited native path
  computes `abs(delta)/duration` at `0x005650E2..0x00565104` with no such lower bound. The constants test only
  repeats six-decimal values and the nominal 60 number; it does not execute tick scheduling, boundary values, or
  near-zero speed.

### M14-004 — FAILS

- Native `RecentlyReacted` holds its timestamp as binary32 and computes `last + 60.0f` with `vadd.f32` before
  comparing it to the binary32 BaseStationTimer result (`0x00611DD0..0x00611E14`). C# stores `_lastReactedSec`
  and takes `nowSec` as `double` (`FaceBehaviors.cs:596-605,626,631-638,648`); live registration supplies
  `robot.Engine.Timer.Seconds`, also double (`Behaviors.cs:740`). The cooldown boundary therefore does not reproduce
  the engine's float rounding.
- The reacted-id set, `TimesObserved >= 3`, charger gate, and reachable `UpdateReactedTo` behavior otherwise match
  `0x00611AE4..0x00611F28`. The test uses hand-picked whole seconds 11 and 71 and manually calls
  `BehaviorDidReact`; it cannot detect the float boundary or prove live clock width.

### M14-005 — FAILS

- `ComputeTurnTowardsImagePointAngles` subtracts and stores binary32 deltas at
  `0x005187CC..0x005187E6`, calls `atan2f` for head/body at `0x0051886C` and `0x0051888E`, and adds the
  historical binary32 angles with `vadd.f32` (for example `0x00518870..0x0051887C`). C# uses binary64
  calibration/state values and `Math.Atan2` throughout (`FaceActions.cs:59-64,72-82`). Even when the eventual wire
  message casts to float, rounding only once at the end is not equivalent to the engine's float subtraction,
  transcendental result, and addition.
- The failure-to-obtain-history branch does match: native warns and turns nowhere after `ComputeStateAt` fails at
  `0x00518800..0x00518862`; C# logs and returns false at `FaceActions.cs:74-79`. The test covers only center and
  ±45-degree canonical inputs with approximate comparisons, and never checks native float bits or the failure path.

**M14 comparison with the Sonnet audit:** Sonnet reported 5/5. This re-audit finds **1 HOLD and 4 FAILS**. All four
failures include native binary32 work widened to binary64; M14-003 additionally replaces action-tick execution with
an independent wall-clock loop and adds an unsupported speed clamp.

## Result

Across the 22 requested current EXACT_SOURCE records, this audit finds **7 HOLDS, 10 FAILS, and 5 PARTIAL**. The
Sonnet audit's combined result was 22/22. The dominant misses are the same ones found by the calibration: decimal
literals accepted without their native bits, float code widened to double, a narrow source-backed fact used to
settle a larger inferred/deferred path, production seams with no consumer, and tests that copy C# constants or stay
far from the source-sensitive boundary.
