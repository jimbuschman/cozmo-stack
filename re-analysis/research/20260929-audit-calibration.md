# Independent audit calibration: M12 and M5 part A

- **Date:** 2026-09-29
- **Answers:** `re-analysis/research/requests/20260929-audit-calibration.md`
- **Kind:** independent extraction
- **Prerequisite:** `20260929-R-BEH2-pre-extraction.md` was present and complete before this audit began.
- **Scope discipline:** read-only outside this answer. I did not build or run the C#; per the extractor rules I inspected the tests rather than using them as source evidence.

I re-read the current manifest and approved inventories, opened the cited Thumb code in the shipped `libcozmoEngine.so`, followed the live C# tags and callers, and checked the named tests for source-derived versus implementation-derived expectations. Float comparisons below are IEEE-754 binary32 comparisons, not decimal approximations.

## M12-manipulation

### M12-001 — FAILS

1. **Flipping geometry is not bit-exact.** The engine materialises `0xC2624EEF` at 0x004E5CB2..0x004E5CBC, hence the positive corner magnitude is binary32 `0x42624EEF` = 56.5770835876. `cozmo-stack/src/Cozmo.Robot/Manipulation/PreActionPose.cs:84` instead stores the decimal `56.5771` as a `double`; even conversion of that decimal to binary32 is `0x42624EF3`. The live pose therefore differs before path planning. `ManipulationTests.cs:81` copies the rounded decimal from the implementation, so it cannot detect this defect.
2. **The threshold calculation uses the wrong arithmetic precision.** The engine squares and sums with `vmul.f32`/`vadd.f32`, takes `vsqrt.f32`, calls `sinf`, and multiplies/adds in binary32 at 0x00550102..0x00550164. `PreActionPose.cs:326-338` performs the norm, sine and products in `double`. Near a close-enough boundary this can choose a different branch. `ManipulationTests.cs:138-141` computes its expected value with the same `double` operations, so that test is circular for precision.

### M12-002 — FAILS

The engine increments the `u16` path id and stores it with no zero special case (`ldrh`, `adds #1`, `strh` at 0x0064A3C2..0x0064A3CA), so `0xFFFF` wraps to path id 0. `cozmo-stack/src/Cozmo.Robot/Manipulation/RobotPath.cs:113-114` increments and then changes zero to one. After 65,535 installations the stack sends a different `ExecutePath.pathID` and its event correlation diverges. The path tests exercise successive non-wrapping ids only; none sets up the rollover.

### M12-003 — HOLDS

The live C# path calls `DockingSystem.DockAsync`, constructs the 21-byte message, and sends it in the same CheckIfDone -> DockWithObject -> builder -> send chain cited at 0x005522AE, 0x0063BA44, 0x0063BD50 and 0x0063BDB8..0x0063BDC4 (`DockActions.cs:158-161`; `Docking.cs:250-290`).

### M12-004 — FAILS

The engine calls `RemoveMatchingPredockPose` only when `GetPossiblePoses` contains at least two poses: the count comparison is at 0x005BEE7C..0x005BEE86 and removal is at 0x005BEE88..0x005BEE90. `cozmo-stack/src/Cozmo.Robot/Manipulation/ManipulationSystem.cs:270-274` adds every chosen pose to a blanket exclusion list without the two-pose gate, before even distinguishing drive versus dock failure. With one possible pose, an engine retry may reuse it; the C# retry instead has no pose and can return `NoPreActionPoses`. No named test exercises the one-pose retry boundary.

### M12-005 — HOLDS

The C# builder at `Docking.cs:250-269` preserves the engine's word/byte order, its three profile floats, action/method bytes and both zero fields from 0x0063BB34..0x0063BD50; the live call supplies those fields rather than a test-only copy.

### M12-006 — HOLDS

`Docking.cs:362-375` attaches only a successful `BlockPickedUp` and releases only a successful `BlockPlaced`, matching the two success-gated branches in `HandlePickAndPlaceResult` 0x00533780..0x005338A2; release uses the non-forgetting path that leaves the lift-relative dirty observation.

### M12-007 — FAILS

The engine's `Verify` is deliberately multi-update: when the object is still moving but `now <= firstVerify + 500`, 0x00553C98..0x00553CA0 branches to 0x00554056 and returns `0x01000000` (RUNNING). A later call can then fail the pick-up. `cozmo-stack/src/Cozmo.Robot/Manipulation/DockActions.cs:165-167` calls `Verify` exactly once, and `DockActions.cs:253-288` falls through to `Success` during that 500 ms allowance. Thus the live stack accepts precisely the moving case the engine keeps pending to verify. `PickupVerifyTests.cs:14-18` checks only the three constants, not the repeated-call state machine.

### M12-009 — HOLDS

`DockHelper.RunAsync` executes attempts 1 and 2 only (`ManipulationSystem.cs:267` with default limit 2), matching the literal two-attempt gate at 0x005B814A/0x005B8192; the production helpers use that instance limit.

### M12-010 — FAILS

1. **The stored binary32 constants are replaced by rounded decimal doubles.** The constructor writes `0x3E860A92` and `0x3EB2B8C2` at 0x00546A3E..0x00546A56; turn/head tolerances are `0x3D8EFA35` and `0x3D0EFA35` at 0x00546ED2..0x00546EDA and 0x00546E56..0x00546E60. `cozmo-stack/src/Cozmo.Robot/Manipulation/SearchActions.cs:34-44` uses decimal doubles whose binary32 encodings are respectively `0x3E860A85`, `0x3EB2B8C7`, `0x3D8EFA39`, and `0x3D0EFA39`. The state constants at `SearchActions.cs:168-170` likewise encode `0xBDB2B8C7` and `0x3F490FD8`, not the engine's `0xBDB2B8C2` and `0x3F490FDB`. These values reach the turn targets and tolerances. `SearchForBlockTests.cs:21-31` asserts the same rounded decimals to six places and therefore masks every bit mismatch.
2. **The random stream is not the engine's stream.** Before each `RandDbl`/`RandDblInRange`, the engine fetches the action/context random generator through the virtual call at 0x00546C34, 0x00546C5C, 0x00546C72, 0x00546C98, 0x00546CCA and 0x00546D12. `SearchActions.cs:49-62` instead creates a private `System.Random`, and `SearchActions.cs:78-84,125` draws from it. The algorithm, seed, sharing, and resulting search motion all differ; the test injects another `System.Random` (`SearchForBlockTests.cs:44-47`) and only range-checks its outputs.
3. **State 0 discards the first child's failure.** The engine appends `TurnTowardsObjectAction` first at 0x005BB024 and the nearby search second at 0x005BB03E; `CompoundActionSequential::UpdateInternal` 0x0054F70C propagates a child failure because this sequence installs no ignore-failure predicate. `SearchActions.cs:233-236` awaits the turn but discards its `ActionResult` and always runs the nearby search. A failed initial turn can therefore become a successful search result in C# instead of ending the state as failed.

### M12-012 — FAILS

The engine constructs the ten-degree tolerance as binary32 `0x3E32B8C2` at 0x0063C66C..0x0063C670 and passes that float into `IsRestingFlat` at 0x0063C67A..0x0063C67E. `cozmo-stack/src/Cozmo.Robot/Behavior/ManipulationBehaviors.cs:259-265` uses the `double` decimal 0.174533 (binary32 `0x3E32B8C7`) on the live bottom-cube predicate. This changes the boundary classification. `RestingFlatTests.cs:34-36` rounds the value back to degrees with two-decimal tolerance, so it cannot establish the source literal.

### M12-013 — HOLDS

`Docking.cs:330-356` reads `DockingErrorSignal` with timestamp first and then the geometry/status fields, matching the 0x0063C548 stores and the corrected 22-byte message rather than the similarly named 16-byte visualisation type.

### M12-015 — HOLDS

`DockActions.cs:348-368` selects `PlaceOnGround`, supplies all three placement offsets as zero, and uses the constant 60/200/500 profile, matching 0x00555D58..0x00555DFE and the values consumed by the dock builder.

### M12-016 — HOLDS

The roll production callers set `AttemptLimit = MaxRollAttempts` (3) before `DockHelper.RunAsync`, matching the helper counter comparison with literal 3 at 0x005B9F0E; this is an instance-specific override rather than a change to the two-attempt default.

### M12-017 — PARTIAL

1. **The squint is installed at the wrong point.** In the engine, `CheckIfDone` calls `DockingComponent::DockWithObject` first at 0x005522AE; only on the started-docking branch does it call `AddSquint` at 0x00552394 and store the result at +0xF4. `cozmo-stack/src/Cozmo.Robot/Manipulation/DockActions.cs:137-160` calls `AddDockSquint` before the turn, visual verification, and `DockAsync`. Failed turn/verification paths therefore display a squint the engine never adds, and the squint begins earlier on successful paths.
2. **The 0xC5 handler lifetime is global rather than action-scoped.** The engine registers 0xC5 and 0xDA handlers from `IDockAction::Init` at 0x005516EA and 0x00551750; they belong to the action's state machine. `cozmo-stack/src/Cozmo.Robot/Manipulation/Docking.cs:131-135` subscribes one `OnMessage` for the entire `DockingSystem` lifetime. LiftLoad is gated on an active dock (`Docking.cs:388-394`), but `MovingLiftPostDock` always invokes its public event and merely passes `false` when there is no active action (`Docking.cs:382-386`). A stale/unsolicited 0xC5 is observable in C# when the engine has no dock-action handler. The squint pixel values themselves (1.05f, 0.35f, -10.0f and the 250 ms reset at `DockActions.cs:177-190`) do match 0x00552378..0x00552398 and 0x0058D738..0x0058D82C.

### M12-018 — HOLDS

`Docking.cs:323-328` sends the empty AbortDocking command before cancelling and clearing the pending dock state, matching the abort send path at 0x0063BAEC..0x0063BB24; it is the live cancellation path used by the action.

### M12-019 — FAILS

The engine constructs the clamp as binary32 `0x3F32B8C2` at 0x0063C182..0x0063C188 and passes it to `ClampPoseToFlat` at 0x0063C194. `cozmo-stack/src/Cozmo.Robot/Manipulation/Docking.cs:114,348` uses the `double` decimal 0.698132 (binary32 `0x3F32B8C7`). Error-signal poses within those five ULPs of the 40-degree boundary can be flattened differently.

### M12-021 — HOLDS

The four C# face tables and vector order at `PreActionPose.cs:105-154` match rodata 0x00C45C40/0x00C45CA0/0x00C45D00/0x00C45D60, and the live generation loop at `PreActionPose.cs:201-262` applies +0xC to types 0/5, +0xD to type 4, no gate to 1/2, and no poses to 3, matching 0x004E595E and the inner mask loop.

**M12 comparison:** Sonnet reported 17/17 clean. This audit finds **9 HOLDS, 7 FAILS, 1 PARTIAL**. The clean result was not reproduced.

## M5-animation, part A

### M5-001 — HOLDS

The live FlatBuffer loader at `AnimationLibrary.cs:471-536` follows fields/tracks 0..9 in the engine order and stops at the first rejected keyframe while retaining the prefix; `JsonClipLoader.cs` names a JSON clip by its first top-level key and `AnimationLibrary.Open` scans the two cited directories, matching 0x0057571C..0x00575EFC and the JSON/container paths.

### M5-002 — HOLDS

`ProceduralFace.cs:33-209` has the 19-parameter order, default scale entries, no default distorter, wrong-sized-eye preservation, clipped parameter writes, face-position clamping and negative-scale-to-zero behavior from 0x00583660..0x00584636.

### M5-004 — HOLDS

The live scheduler emits head `{u16 duration,s8 angle}` and lift `{u16 duration,u8 height}` once when due, then advances those tracks, matching the message stores and done behavior at 0x004F8C08..0x004F8CD4 and 0x004F8F80..0x004F9048.

### M5-005 — PARTIAL

The range arithmetic, per-message draw, unchecked byte truncation/wrap, mt19937 state and two-draw `GetNextDbl` all match 0x004F8C08..0x004F9048 and 0x0082F9B0..0x0082FAC6. The entropy-seed path does not: for seed 0 the engine invokes `/dev/urandom` exactly once at 0x0082F898, moves that word into the seed at 0x0082F89C, and immediately initialises mt19937 at 0x0082F8A4—even if the word is zero. `cozmo-stack/src/Cozmo.Robot/Animation/EngineRandom.cs:33-38` loops until the OS returns a nonzero word. An entropy result of zero therefore produces the standard zero-seeded stream in the engine but consumes another entropy word and produces another stream in C#. `M5AnimationTests.M5_005_R1_Mt19937AndGetNextDbl` tests an explicit nonzero seed; the variability tests use injected deterministic seams, so none covers this production seed branch.

### M5-006 — HOLDS

`AnimationClip.cs:49-109`, the JSON/FlatBuffer loaders, and the scheduler reproduce atoi-style radius handling, the point/straight speed clamps, unsigned never-stop representation for negative duration, first message, intervening null frames, and the terminal `{0,0x7FFF}` at 0x004FB170..0x004FBB0E.

### M5-007 — HOLDS

The scheduler's live buffer/drain path advances stream time by `framesBuilt * 33` only after an OK drain (including a budget stop) and builds no new frame while buffered, matching A18/M3 C15 rather than wall-clock progression.

### M5-008 — HOLDS

The production scheduler enforces one stream, refuse-versus-interrupt, cyclic tags 1..0xFE, same-tag loop reinitialisation, zero as infinite loops, live forced to one loop, and replay-last behavior from 0x0057A650..0x0057BF28.

### M5-009 — HOLDS

`AnimationLibrary.cs` reads the low 32 bits of the audio id, defaults volume to 1.0, synthesises equal probabilities only when absent, and rejects count mismatch or cumulative probability above one, matching 0x004F9E54..0x004FA05C.

### M5-010 — HOLDS

`AnimationScheduler.LoadNeutralFace` and `TrackLayerComponent` take the first procedural-face keyframe of the first neutral-group clip, use it as reset/base data, and replay it after abort-to-nothing and RemoveIdle, matching 0x0057A054..0x0057B122 and the reset-data path.

### M5-012 — HOLDS

The selected audio reference's loaded/defaulted volume is passed unchanged into the audio-layer call at `AnimationScheduler.cs:1536-1575`, matching 0x004F9AEC..0x004F9DFC and 0x0057CD32; the Wwise interpretation remains correctly outside this record in M6.

### M5-015 — HOLDS

`ProceduralFaceRenderer.cs` uses binary32 scaling and `RoundF` for `p*15`/`p*20`, emits a point below radius 1, and otherwise builds the four fixed-centre ellipse arcs, matching 0x005839A0..0x00583F1C and the shipped OpenCV call path.

### M5-016 — HOLDS

The FlatBuffer-to-JSON load path, raw-versus-normalised color conversion, alpha/default behavior, five-word Left/Front/Middle/Back/Right order, and every-frame send while current in `AnimationClip.cs`, `JsonClipLoader.cs`, `TrackLayers.cs` and `CozmoAnimations.cs` match 0x005758C8..0x00575CBE, 0x0084024C..0x0084050C and 0x004FAC7C..0x004FB11A.

### M5-017 — PARTIAL

The live audio choice does cast `RandDbl(1)` to float, skips `|p| < 1e-5`, uses inclusive cumulative bounds, and returns silence when no interval matches, as at 0x004F9AEC..0x004F9CBE. However this draw uses the entropy-seeded static keyframe RNG (R2), so the zero-entropy mismatch in M5-005's production `EngineRandom` path (`EngineRandom.cs:24,33-38` versus 0x0082F86A..0x0082F8A4) also changes which audio alternative is selected in that case. The named M5-017 tests inject reproducible nonzero seeds and do not cover the production entropy-zero branch.

**M5 part-A comparison:** Sonnet reported 13/13 clean. This audit finds **11 HOLDS, 0 FAILS, 2 PARTIAL**; both partial records share the same single-call-versus-retry entropy-seeding defect. The clean result was not reproduced, although it was substantially closer than M12.

## Calibration conclusion

The failure patterns were the ones the complete audit warned about: rounded decimal/double substitutions for native floats (M12-001/010/012/019), correct local pieces embedded in a different production state machine (M12-004/007/017), an untested rollover gate (M12-002), and deterministic tests that never enter the production entropy branch (M5-005/017). Several named tests derive their expectations from the same rounded constants or arithmetic used by the implementation; passing them would not establish source fidelity.
