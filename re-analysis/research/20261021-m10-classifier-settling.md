# R-M10-CLASSIFIER — live classifier and sensor foundation comparison

Request: `requests/20261021-m10-classifier-settling.md`. Examined HEAD e0c71224. Independent extraction/read-only comparison, pending manager checking. Coverage opened before tracing.

| Record / step | Native behavior and instruction citation | Production file/line and caller | Existing test / reach | Disposition |
| --- | --- | --- | --- | --- |
| 001 / input and priority | Head-calibrated gate; tick-start u32 ms; pitch/Radians, filtered accel Y, status bits20/8, physical byte; falling→side→face→back→level/pickup/tail (0x00511E00..0x00511FD0,0x00512228..0x0051225C). | `OffTreads.cs`:180–260; live `Sensors.cs`:1000–1018 after IMU/raw copy. Sync admission is `CozmoRobot.cs`:792–800. | DerivedStateTests classifier tests: mostly direct units; TheSensorsRunTheClassifier… uses incoming packets/calibration. | Branch structure supported; six angle constants CONTRADICTED (F1). |
| 001 / candidate and commit | Back stamps now+750, ordinary orientation now, falling/pickup/tail now−250; unsigned t+250<=now then different-state commit. Side cand3/4 retained even after accel sign flips. Falling flag alone can tail back to0 (0x00511F18..0x00511FCC,0x00511FD0..0x0051208E). | `OffTreads.cs`:217–286. Raw robot timestamp only records fall entry/duration; BaseStationTimer controls debounce. | DerivedStateTests OnBack…/OnFace…/FallingNeeds…: direct units. Tests sample center,249/250 and1000ms, not exact bit boundaries. | Supported scheduling; constant inputs fail F1. |
| 001 / precommit cancel | Enter Falling stamps Robot.ts, DAS, ActionList.Cancel(-1) **before committed byte store and state report**; leaving fall DAS then clear start (0x00511FF0..0x0051208E). | OffTreads279–286 invokes ActionListCancel event. No production subscriber found anywhere in `cozmo-stack/src`; actual ActionList exists. | Standalone falling test observes only started/duration, no queue. | UNBUILT live cancel join, F2. Do not repeat “no ActionList exists”. |
| 001 / postcommit consequences | Store355, E2G state report, log; states2..6 detach carried object; OnTreads localizable query and conditional stores2C4=1/2B8=-1; nonzero state clears charger platform; pause flag2 only after commit (0x00512088..0x005121F8). | OffTreads288–320. Sensors ctor binds platform; VisionSystem104–105 binds world query/stores; FreeplayStack253 binds pause. OffTreads.Unattach… has no production assignment; ManipulationSystem57 binds a **different** Sensors property. | Packet calibration/state event test; ReportOrderingRegressionTests platform tests; no actual classifier→carrying detach test found. | Partial supported joins; missing detach is observable F2. Missing optional owners outside those host systems are not proven empty native state (F3). |
| 001 / API ordering | Native RobotOffTreadsStateChanged carries committed new state before carrying/world/platform/pause work (0x00512098..0x005120AA,0x0051210C/192/1F4). App buffer precedes local synchronous consumers under Broadcast. | OffTreads289–291 enqueues classifier event once; Sensors85–88 enqueues alias without second broadcast count, then synchronous internal reactions; platform clear later. | DerivedStateTests sensor integration verifies event values. No single-packet cancellation+carrying+platform ordering oracle found. | Ordinary API/internal split supported; missing cancel/detach cannot be repaired by API-order tests alone. |
| 002 / live call and gates | Movement.Update before origin/history admission at0x00512B5C; physical false returns, nonzero AnimationState tag requires BODY lock, picking/placing returns without reset, status5008 resets timestamp/sums/u8 count (0x0063E398..0x0063E426). | Sensors1040–1044 reads real EngineRobot physical/tag and Motion locks, passes raw packet to UnexpectedMovement.Update; `UnexpectedMovement.cs`:215–221. | RFix2BTests threshold units; DerivedStateTests direct gates. No complete packet-driven threshold matrix found in inspected tests. | Supported live detector input join, separate response boundary. |
| 002 / arithmetic and output | abs-wheel-sum<20 decays; quiet gyro below3E32B8C2: signbit wheels or strict mismatch>0.2 adds1; active opposite sign adds2, same sign guarded decrement. First increment stores robot.ts, sums raw wheels, unsigned-byte fire>10, type0/2 only (0x0063E428..0x0063E5DA,0x0063E674..0x0063E68A). | UnexpectedMovement223–260,299–305. Int count stays within native0..12 through supported Update/reset paths; float products/adds ordered. Respond called only after fire; ordinary host report queued then reset. | RFix2BTests below/equal/above3E32B8C2 literal tests; DerivedStateTests count11/double12, sums, no-negative decay: units. | Supported detector core; M10-007 rewind/obstacle remains UNBUILT, not absorbed here. |
| 009 / obstacle latch | Ctor AI+4=0 and predicate reads it, no sensor/map lookup (0x00569A78..88,0x006143CA..D4). Existing no-writer census is bounded; escaped-alias/unindexed proof UNKNOWN. | `Behavior/AIComponent.cs`:51 false default/internal setter; Behaviors207 reads ctx.AI?.ObstacleDetected. Source-wide assignment search found no production writer. | DerivedStateTests.NothingRaisesTheObstacleStrategyByItself: zero/predicate fixture, not whole-binary absence proof. | C# zero/read supported; universal native negative claim remains UNKNOWN (F4). |
| 010 / physical flag | Ctor0; FirmwareVersion argument=json[sim].isNull; SetPhysicalRobot stores14 then Vision, then audio source (0x0050FC36,0x00536980..98E,0x0051397A..97C). Parsing/failure owner M3-024. | CozmoEngine958–1001. Both classifiers copy this actual flag before each live state; tap also reads it. | RFix2BTests firmware handler and M11RVisBuildTests physical-before-audio: **direct-handler units**. | Bounded flag/gates built. Old uncaught malformed-root unknown superseded by M1-034 isolation, not a new policy. Full BlockFilter.Init is M4-011. |
| 011 / robot falling events | Separate received FallingStarted report; received FallingStopped intensity strictly>1000 triggers needs17, then DAS, then duration/intensity report (0x00534F4C..0x00534F9E,0x005350AA..0x005350C2,0x00535186..0x0053519C). Classifier DAS fall transition is not this event producer. | Sensors938–959; FreeplayStack269–280 binds ordinal17 to real Needs.RegisterNeedsActionCompleted(Fall). Queue host copy before synchronous internal reaction hooks. | M4ControlTests.B8_FallingEvent… real Data/Tick, conservative unit label, injected needs observer; FreeplayTests hard-fall uses supplied OBB Needs owner, unit fixture. | Handler payload/gate/order supported; whole needs availability outside Freeplay remains dependency F3. Log text alone queued. |
| 013 / pre-first-state | Robot allocation without memset and no raw accel/gyro ctor store; filter state is zero; first UFRS copies raw accel/gyro (0x0052EE8E..9C,0x005100E0..FE,0x005129A4..BE). | OffTreads raw fields default0; ResetToConstructed clears raw/filter/candidate and physical/head gates. Sensors835–836/CozmoRobot855 calls real removal reset. | Constructor/reset tests and normal first-packet IMU cases; no dedicated pre-first slope boundary test inspected. | Existing MD1 policy implemented, never native EXACT pre-state contents. Metadata decision F5. |
| 014 / construction and lifetime | Filter: five-pair pools twice, empty tree/string/vector, nonnull-interface subscribe E7,E5,E6,E9,EA,EB. Release reverse handles, string/tree/allocation, read26C before clear44C. Gyro null-before-raw-delete, fresh ctor on replacement (OV23/24 B1–B4;0x00619A2C..9D84,0x0051121A..250,0x00511418..424). | DerivedOwners11–28, CubeConnections173–190; RobotLifetime93–100/122; actual host factory supplies same pool, fresh gyro per EngineRobot. | M10GyroCheckedRowsTests actual EngineRobot construction/RemoveRobot/replace and real hub handle counts; null-interface test supplies explicit gate fixture. | Supported bounded lifetime, not operational filter handlers/Load/Save/Update. Shared retirement M1-046/M5-056. |
| 015 / admission and input | Sync and history/origin/GetLast-frame failures gate tail; treads boundary Delocalize jumps directly to tail. Drift, reload pointer, bias, cliff stats (0x00512FB0..FCE; GetLast failure0x0051307A..3110, treads0x00512B78..BAA). | Sensors1051–1084; actual AddIncomingStateToHistory, UpdateCurrPoseFromHistory and mismatch state, then DetectGyroDriftAndBias468–477. Moving packet bit0, Cliff+5 event mirror, real head calibration/frame/current MainPose; bias receives **raw magnitude**. | M10GyroCheckedRowsTests.LiveSensorTail… now asserts real BiasCount1 and actual history count; RobotStateHistoryAdmissionTests dispatches admission/treads failures. | Later builds CLOSED old no-hook/history join. Scalar/pose and shared cliff-writer proof remain F6, not stale wholesale missing. |
| 015 / drift | Ordered abs(z)>3E32B8C2 rejection; moving/cliff/head!=1; start frame/yaw/baseline/sum/min/max/ts/count1. Stable limit3C8EFA35; u32 elapsed<5001 accumulates. Else prior samples only, wrapped yaw difference>3C0EFA35; warn and latch, clear start only (0x0052C568..770). | RobotGyroDriftDetector23–48, ReadYaw50–56 uses GetCheckedMainPose/native Rotation3d yaw under tree gate. Bit constants, u32 count/time, ordered NaN gates retained. | M10GyroCheckedRowsTests drift5000/5001, zero/wrap/frame, NaN/start reset: **direct detector units** with supplied inputs/pose. | Supported body and actual caller exists; complete live long-window proof still untested, not itself a defect. |
| 015 / bias | hp=f32(f32(rawMag−prev)+hp)*3F4CCCCD; statusbit0/absHP>50 reject, timestamp5001/count signed>=50. Per-axis abs/sign/spread strict tests; event then warning, no break after latch; reset min+inf/max+0; current sample included, next call latches before HP. Filter old*3F733333 + raw*3D4CCCD0 (0x0052C804..0x0052CB24). | RobotGyroDriftDetector59–101. Ctor min0/reset asymmetry preserved. | M10GyroCheckedRowsTests initial suppression, signedcount49/FFFFFFFF, all-axis report, HP50/NaN, next-call latch: units, some seed private detector state. | Supported body. Host policy/source boundaries explicit; no invented correction command/API report. |
| 015 / report formatting | Drift to_string(float) at0x0052C63A supplies $data; bias roundf(degrees*1000) strings at0x0052C90E/926 form value min,max and $data=axis. Warning arguments widen final f32 values. | RobotGyroDriftDetector43–46,82–87 uses checked LibcxxPrintf.FormatFixedFloat. Event-before-warning preserved. | M10GyroCheckedRowsTests literal1.145916 and573.000000,579.000000 values, axis x/y/z; direct units. | Older withheld-text gap CLOSED. Shared phone libm/Radians/yaw provenance remains named, no ADP-1. |
| 017 / raw magnitude | f32 x²,y²,z² then (x²+y²)+z², VSQRT, imported sqrtf only on unordered result, store378 before smoothing (0x005129C0..0x00512A1E). | OffTreads186–202 separates operations, MathF.Sqrt plus NaN fallback. Live Sensors then detector raw input. | M10GyroCheckedRowsTests real packet3,4,12→13; NaN/inf direct unit; DerivedStateTests IMU literal bit tuples through received packets. | Operation order/raw join supported; imported phone sqrtf last-bit/NaN boundary not newly proven (F6). |

## Findings and current production path

There are concrete behavior-changing defects in M10-001: decimal angle constants differ from the actual literals, and two mandatory consequences are unbound. The later gyro/history and formatter builds close older gaps, so repeating “no native history hook” would be wrong. No code/tests/records were changed; no tests, build, emulator or hardware ran.

The bounded live path traced is incoming R2E RobotState → engine time-sync admission → device state/head/motion mirrors → Sensors raw cliff/touch/path inputs → IMU raw magnitude and filters → off-treads classifier and commit consequences → charger/radio state → unexpected-movement detector → moved/localization state → treads-boundary Delocalize or native history admission/current-pose/frame gate → drift → bias → cliff tail. Separate FallingStarted/Stopped messages take their handler paths independently of a classifier commit. Host reports enqueue in native publication order; internal engine consumers stay synchronous. Downstream behavior/reaction/activity algorithms, obstacle rewind and world ownership are explicit handoffs.

Fresh instructions read here cover the entire classifier executable branches/return and cited literal pools, unexpected-movement detector gates/count/rules through fire plus decay tails, gyro ctor and both full bodies through return, raw-IMU magnitude/filter region, native detector call order, and selected falling/physical call-site ranges. Adopted OV23/24 establishes the bounded filter retirement; prior corrected pose/history and formatter reads are reused, not advertised as a new whole M11 audit. Historical tables' rounded numeric descriptions are navigation, not literal values.

### F1 — six off-treads angle constants differ from source

These are defined comparisons that change classification and thus robot/API/downstream state. Current `OffTreads.cs`:54–65 uses decimal spellings; the binary supplies the following exact values:

| Input | Native literal/instructions | Exact native value | Current value |
| --- | --- | --- | --- |
| Face high f32 | movw/movt0x00511E66/6A =3FF5BE0B | 1.919862151145935 | 1.91986f =3FF5BDF9 |
| Face low f32 | movw/movt0x00511EE6/EA =BFB2B8C2 | -1.3962633609771729 | -1.39626f =BFB2B8A6 |
| Level f32 | word0x005122BC =3F490FDB | 0.7853981852531433 | 0.785398f =3F490FD8 |
| Simulated back center f64 | bytes0x005122A0 =3FFAEB8260000000 | 1.6824973821640015 | 1.68250 |
| Physical back center f64 | bytes0x005122A8 =3FF4CDE840000000 | 1.3002703189849854 | 1.30027 |
| Back half-width f64 | bytes0x005122B0 =3FD0C15240000000 | 0.2617993950843811 | 0.261799 |

The centers/half-width are doubles equal to exactly widened source float values; preserve the f64 subtraction/absolute comparison, not just f32 threshold arithmetic. Native face comparisons still use Radians raw-difference and wrapped IsNear, so test the effective tolerance boundary rather than replacing them with plain >/<.

Concrete independent arithmetic witness: physical robot, zero filtered Y, initial candidate OnTreads, pitch **bits3FC7F1E1 =1.562069058418274**, picked-up=true, not falling. Native widened subtraction lies inside the exact back band, so sets candidate OnBack with now+750 and waits to commit at1000ms. Current decimal center+half-width puts it outside; its nonlevel pickup branch immediately commits InAir. Another witness is pitch bits3F84EC9E at the physical lower edge: native inside, current outside. Level bits3F490FDA are <=native45° but >current decimal; a prior back/side/face candidate held picked-up takes the native level→InAir branch but not the current one. These witnesses were calculated from decoded literals and separate binary32 input decoding; no stack test or hardware run is claimed.

The existing center test uses `OffTreadsClassifier.OnBackCentrePhysicalRad` as its input and asserts the center only to five decimals. That is a valid broad branch test, but circular for this constant's exact provenance and incapable of detecting F1. Add literal-bit boundary oracles, not tests derived from the production constants. The 20260930 pre-extraction already warned that the angle pools must be copied as bits; this is a missed implementation obligation, not a new compatibility choice.

### F2 — the classifier's cancel and detach hooks have no live recipient

Current source search for `ActionListCancel` finds only its event declaration and invocation in OffTreads.cs. No production subscription exists. Native0x00512030..38 loads Robot.ActionList and cancels all **before** storing state355 at0x0051208E. The real ActionList is now built and is used by D4 RobotStopped and AbortAll; those other callers do not implement the Falling transition. A RobotState that enters Falling can therefore leave current and pending actions alive, with different traffic/completion order. Wiring this known call belongs to M10-001→M8-015; it needs no new cancellation algorithm.

Likewise, OffTreads.UnattachCarriedObjectIfCarrying is invoked for states2..6 at OffTreads297 but has no assignment in production source. `ManipulationSystem.cs`:57 sets **Sensors.UnattachCarriedObjectIfCarrying**, a different property used by motor handlers and behavior adapters. It does not reach the classifier's property. Native0x00512100..10 tests Carrying+8 !=−1 then calls SetCarriedObjectAsUnattached(true), after the off-treads broadcast and before platform/pause consequences. The current commit can retain carried state instead. Next small job must bind this exact recipient using M12-006's approved semantics, with visible missing-owner diagnosis if unavailable; do not substitute a generic dropped-object event or reinterpret the two properties as one bound path.

Neither omission currently emits a dedicated missing-recipient diagnostic at the classifier call. The report does not turn absent subscribers into evidence of no effect. DAS-only transition logging differences are separate best-effort cleanup; they do not excuse these two state/traffic omissions.

### F3 — consequences whose bindings are present, and their boundary

`VisionSystem.cs`:104–105 installs the actual world localizable query and conditional localization-clear stores; `Sensors.cs`:89 installs platform clear. The latter queues its platform report and then updates threshold/pause hooks in the checked order. `FreeplayStack.cs`:253 installs OffTreads pause flag2, and its OnCharger internal hook handles flag3. These are real recipients in those constructed systems. Do not say all M11/M15 seams remain absent.

However, the classifier treats a null world-query delegate as “nothing localizable” and writes its local2C4/2B8; the native path calls an existing BlockWorld. A host without a VisionSystem is not evidence that the native world is empty. Similarly, pause and hard-fall needs registration are installed by FreeplayStack rather than the basic sensor/EngineRobot constructor. When settling a whole supported path, establish when those owners exist and their intended supported entry; otherwise keep the missing input/recipient explicit. This pass does not decide that a supported engine state/needs update is optional outside freeplay or re-extract M15 algorithms.

M10-011's >1000 gate and needs-before-DAS-before-report are present; the source report payload is duration/intensity, with timestamp retained as extra robot-input context in the C# record. FallingStarted's native timestamp report is separate. Received falling messages remain delivered even if head calibration prevents classifier commits. Handler log wording/key/channel mismatches are queued cleanup; the roundf-intensity and unsigned-duration DAS representation now exists, contrary to the old unresolved sentence.

### F4 — M10-009's bounded evidence versus universal title

The current record explicitly claims “Nothing in this build ever raises the obstacle-detected flag”, with a forty-five-instruction writer heuristic among its evidence. Native initializer and predicate are established. Current C# has no production setter calls and reads the component flag through BehaviorContext, rather than an invented obstacle-world input. Preserve that behavior.

The later `20261007-M10-M15-rows.md` OD1–OD3 and its final negative-proof note explicitly leave escaped aliases, computed-offset stores and unindexed fragments outside their static census. The original M10 inventory also says the global absence check was not independently verified. No positive writer was found here; no evidence supports enabling a reaction from proximity obstacles. But a whole-binary negative is still **UNKNOWN**, not upgraded by a zero-default unit test. Recommend retaining the gap or bounding the claim to the inspected ownership/writer closure before settlement. This is not a request for another broad audit: the narrow remaining question is whether AI+4 escapes to any behavior-changing writer beyond the named component paths. If the manager has later checked closure, attach that evidence rather than relying on the heuristic statement.

### F5 — physical and pre-first-state policy

M10-010's physical flag itself and its classifier/detector gates are built. FirmwareVersion parse failure leaves prior state; null or missing sim means physical; any nonnull sim value means nonphysical, not Json Boolean truthiness. Current handler isolates wrong-kind root throws under M1-034 before stores. Its old “where is the throw caught?” blocker is stale under the standing rule and current M3-049 implementation. Parsing belongs to M3-024; BlockFilter.Init/vision effects remain M4-011/M11-037. A bounded physical-store/gate claim can be settled after those boundaries are accounted for, without reimplementing them here.

M10-013 cannot be EXACT_SOURCE for pre-state raw bits. Existing MD1 zero policy is recorded in the approved inventory and implemented in construction/removal reset. It needs its planned COMPATIBILITY_POLICY classification at the proper approval step, with the native uninitialized window retained as evidence. The standing no-undefined-behavior rule also precludes reproducing heap contents; no new operator decision is needed here. After the first accepted state, raw accel/gyro are copied from the packet; they do not use this policy or smoothed substitutes.

### F6 — gyro/live history and formatter: old gaps closed, remaining limits named

Current Sensors1051–1084 uses actual history admission and frame/counter state. Treads-boundary Delocalize runs before the tail and admits no raw entry; origin/raw/frame-lookup failures skip detectors as sourced; the 101st mismatch path delocalizes/skips the tail. DetectGyroDriftAndBias invokes the real per-Robot detector, in drift/bias order. The optional GyroDriftDetect override is a unit seam, not the default production caller. Its inputs are packet moving bit0 (the earlier native Movement+9 mirror), CliffEvent-derived +5 rather than the packet CLIFF_DETECTED+6 bit, actual head-calibration state, current frame, current native MainPose yaw and raw magnitude. The old B-M10-1/B-M11-10 no-admission notes and code header are stale.

M11-083's formatter is now used for both previously withheld reports. Drift emits Warning with $data angular displacement; bias emits Event value `min,max` with $data axis before Warning. No robot command, pose correction, readiness change or sensor API event is invented from these diagnostic detectors. Formatting cleanup by itself cannot hold a behavioral record open indefinitely.

Supported drift/bias body details include NaN permitting ordered-rejection gates, strict final rejection, timestamp0 sentinel, wrapping u32 elapsed, 5000/5001 boundary, prior-sample drift evaluation, signed bias count, constructor minima0 versus reset+inf/max0, no break after first qualifying bias axis, and current-sample update before the next-call latch. These are intentionally preserved defined behavior, not “repaired” algorithms.

Remaining dependencies are bounded: M11-065 native quaternion/yaw and GetPose invariants, shared Radians and external non-audio libm; M11-051/053/097 admission/origin state; the Cliff+5 writer census, which OV23/24 explicitly describes as heuristic beyond the known ctor and handler. This pass uses that known event producer and does not claim a new complete writer census. Raw magnitude's imported phone sqrtf fallback is not emulated here; current MathF.Sqrt fallback has the same structural NaN branch, but complete phone NaN payload/last-bit behavior remains the existing external arithmetic boundary. No ADP-1 exemption applies.

## Per-record recommendation

| Record | Recommendation | Remaining bounded obligation |
| --- | --- | --- |
| M10-001 | Keep IMPLEMENTATION_GAP; concrete defects, not only seams. | F1 exact constants and F2 live cancel/detach; F3 owner/input lifetime and affected source-oracle tests. |
| M10-002 | Recommend bounded detector core settlement after manager checks current input join. | No new arithmetic defect found for supported detector inputs. Keep M10-006 gates accounted for and M10-007 response/API-side uncertainty explicit; do not settle the whole response. |
| M10-009 | Keep gap for universal negative claim. | F4 exact predicate/default plus bounded no-writer evidence; alias/escape closure or narrower sourced claim. No invented producer. |
| M10-010 | Flag/store/gate slice supported; recommend bounded settlement. | M3-024 input parsing and M4/M11 recipients named, F5. Tests should distinguish direct handler from packet reach. |
| M10-011 | Handler gate/payload/order supported; recommend bounded settlement after owner mapping. | F3 actual Needs owner availability outside supplied Freeplay fixture. Log-only cleanup queued. |
| M10-013 | Recommend the already planned policy classification at approval; never EXACT pre-state bits. | F5 preserve MD1 scope and constructor/removal/first-state boundary. No code change needed from this pass. |
| M10-014 | Recommend settling the checked lifetime slice. | Operational BlockFilter handlers/Load/Save/Update stay separate; actual shared subscription mechanics and ordered Robot retirement remain their existing owners. No evidence requires C++ allocation imitation. |
| M10-015 | Body, live tail and formatter now supported; no repeat “no history hook” build. | F6 shared pose/libm/input closure; manager can settle the bounded detector claim after accounting for these dependencies. New packet-driven long-window regression useful, not proof of a current defect. |
| M10-017 | Ordered raw magnitude and live bias input supported. | F6 retain existing imported sqrtf arithmetic boundary; settle only the supported operation/input slice, not an unevaluated full phone-libm claim. |

Recommendations change no status, evidence, approval, hardware verification or acceptance state. “Not fully tested” is not itself proof of a defect; passing helpers do not establish absent caller joins.

## Regression reach and proposed tests

Read current M10GyroCheckedRowsTests, the classifier/detector/IMU portions of DerivedStateTests, RFix2BTests threshold and firmware-handler portions, M4ControlTests B8 falling report, FreeplayTests hard-fall fixture, and the physical-order unit in M11RVisBuildTests. Reused the current history-admission tests inspected in the preceding pose/history pass. No tests were run.

The IMU bit-tuple assertions and3/4/12→13 packet regression are independent numeric oracles. Gyro threshold cases use literal3E32B8C1/C2/C3/C6; drift5000/5001 and bias signed-count/reset tests have useful independently specified result/timing expectations. Bias tests that seed minima/counts are unit bodies, not natural long-window reach. The firmware test calls HandleFirmwareVersion directly despite its “live robot” prose. The received M4 B8 test injects packet/tick but substitutes a needs observer; its conservative unit label does not prove Needs ownership. The Freeplay hard-fall test binds a real NeedsManager from assets but supplies its host setup. Broad center/orientation tests do not prove exact classifier literals; F1 is the concrete circular-constant weakness.

Proposed small regressions for a separate builder:

1. Literal-bit physical back witnesses3FC7F1E1/3F84EC9E plus their neighbors; simulated center edges separately; level3F490FD8..DB; face effective ±Radians epsilon thresholds around3FF5BE0B/BFB2B8C2. Expected constants must come from the binary table, not OffTreadsClassifier fields. Assert candidate, committed state and wire/API consequences through real calibration/FirmwareVersion/RobotState admission.
2. With actual queued current and pending actions, send a calibrated RobotState entering Falling. Observe current CANCELLED and pending NOT_STARTED completions, native destructor traffic, then OffTreads report, then platform/pause effects. Assert synchronous internal recipients still see the old committed state during precommit cancellation. A direct ActionListCancel subscriber test alone would hide F2.
3. A real carried-object setup entering states2..6 detaches after report publication but before charger clear/pause; entering InAir does not detach. Use the approved M12 recipient and verify retained world/carry state, not a fake delegate invocation.
4. Real engine-clock ticks at249/250 and999/1000, several RobotStates in one tick with differing robot timestamps, u32 wrap and side-candidate sign reversal. Existing direct classifier clock tests remain unit coverage.
5. Real FirmwareVersion inputs before and after states, absent/null/false/non-null sim values, calibration transitions and robot removal/replacement. Verify zero raw pre-state policy then first packet raw values and filter state; no stale physical/head/detector latch leaks.
6. Packet-driven unexpected-movement gates including gyro boundary equality, wheel-sum20, mismatch0.2 equality/adjacent floats, signbit±0, first increment timestamp after decay-to0, physical/tag/body locks and picking-before-reset ordering. Check count10 does not fire, quiet11/active12 do, type1 is never produced. Stop at detector output; no fake successful rewind/obstacle oracle.
7. Packet-driven gyro5000/5001 windows with real history/current-pose, origin/duplicate/GetLast-frame failures, treads-boundary Delocalize and mismatch101. Check drift/bias skipped or reached at the actual tail, event-derived cliff vs packet bit, constructor/reset bias asymmetry, literal report values and latches. Do not set GyroDriftDetect to a success fixture.
8. FallingStopped exactly1000 and next representable larger input: real Needs owner state changes before buffered report; received FallingStarted must still publish with classifier head gate closed. Assert FIFO ordering with an earlier animation report and later platform/off-treads report, while synchronous internal consumers remain at native positions.

## Handoffs and uncertainty

M8-015 owns cancellation/destruction/completion semantics; F2 needs its existing real ActionList recipient. M12-006 owns detach's carrying/world effects. M11 owns world localizable query, pose/origin/admission and GetPose/yaw; those current recipients are present but do not settle every M11 path. M15 owns pause/needs lifetime and algorithms, not an optional interpretation of engine state. M3-024 owns FirmwareVersion JSON; M4 owns calibration, platform, touch/cliff and Movement mirrors. M10-007 owns rewind/absolute localization/collision obstacle and successful-response side; its history query now exists but its response remains unbuilt. M10-003/004/008/016 strategy/resume/Hiccup and concrete reaction bodies were not re-extracted.

Explicit remaining UNKNOWNs are the universal AI+4 writer closure, full Cliff+5 writer closure, host owner availability outside Vision/Freeplay construction, and the existing imported scalar-libm boundary. None becomes a guessed sensor policy, an unsupported-feature exclusion or a no-effect claim. Hardware success was not used as provenance.

## Current primary-record snapshot

Quoted from `re-analysis/fidelity_manifest.json` at examined HEAD. No values changed.

### M10-001

Title: "Off-treads classifier CheckAndUpdateTreadsState: gate, inputs, thresholds, branches, 250 ms debounce commit and every consequence"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "A1..A7 CheckAndUpdateTreadsState 0x511E00..0x5121F8 (thresholds 0x5121FC, 0x5122A0..0x5122BC)",
  "A8..A15 consequences: Falling DAS + ActionList::Cancel(-1) (0x511FF0..0x512084; gap1 3a..3e), RobotOffTreadsStateChanged broadcast 0x512092, OnTreads 0x512112..0x512184, carried 0x512100, SetOnChargerPlatform 0x512188, pause flag 0x5121E2",
  "gap1 5a IMU filter state zeroed (0x5100E0..0x5100FE)",
  "R-ANIM pre-extraction part 2 item 1 O1..O4: Radians operator> 0x84CC90, IsNear 0x84CC0A, operator< 0x84CD12, rescale 0x84C87C"
]
```

### M10-002

Title: "Unexpected-movement detector: gates, wheel/gyro rules and constants, fire at count > 10"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "B1 tail call 0x63E392; B2..B5 gates 0x63E3B4..0x63E426",
  "B6 ctor constants 0x63DAB0..0x63DB00; B7..B11 rules 0x63E428..0x63E5DA",
  "R-ANIM pre-extraction part 2 item 2 B7..B11-a: 0x63E474, 0x63E49C, 0x63E588, 0x63E51C..0x63E53E, 0x63E674, 0x63E5B2"
]
```

### M10-009

Title: "Nothing in this build ever raises the obstacle-detected flag"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "StrategyObstacleDetected 0x006141F8 is a StrategyGeneric whose predicate is the three-instruction function object at 0x006143CA: return *(bool*)(*(void**)(robot + 0x264) + 4)",
  "Robot+0x264 is the AIComponent: Robot::Delocalize calls AIComponent::OnRobotDelocalized on it at 0x00510D72",
  "AIComponent+4 is a plain bool the constructor zeroes at 0x00569A80, and that store is the only byte written at +4 anywhere in AIComponent code",
  "no wide strb to +4 exists in the binary outside MemoryMapData_ProxObstacle's constructor, and no function that fetches the component from Robot+0x264 writes a byte at its +4 within forty-five instructions of doing so",
  "so the strategy never becomes true, and ReactToObstacle - listed by hiking, nothingToDo, playAlone and socialize, with reactToObstacle.json giving it this strategy - never runs"
]
```

### M10-010

Title: "The physical flag robot+0x14: 0 until FirmwareVersion, then json[\"sim\"].isNull(); its gates"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "A4 ctor 0x50FC36; SetPhysicalRobot 0x51397A from HandleFirmwareVersion 0x536980",
  "B2 0x63E3B4..0x63E3B8"
]
```

### M10-011

Title: "Falling events: FallingStarted broadcast; FallingStopped NeedsAction 17 above 1000, DAS, broadcast"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "C1 0x534F4C..0x534F9E",
  "C2 0x5350AA..0x5350C2, 0x535186..0x53519C"
]
```

### M10-013

Title: "Raw accel/gyro before the first RobotState: heap contents in the engine; 0 here (forced policy)"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "gap2 7b: no ctor store to +0x35C..+0x377; operator new(0x530) without memset (0x52EE8E..0x52EE9C)"
]
```

### M10-014

Title: "BlockFilter and gyro-drift lifetime effects"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "0x0051121A..0x0051124C; 0x00511418..0x00511424; 0x0050FE1C..0x0050FE20; BlockFilter+0x44C releases members; gyro+0x27C is raw delete. Classifier/filter state effects UNKNOWN; history is M11 and TapFilter is M4.",
  "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
  "re-analysis/research/overnight/20261014-ov23-24-check.md: B1 0x00510174..0x00510188, ctor 0x00619A2C..0x00619D84 (tags E7,E5,E6,E9,EA,EB via slot +0x2C), gyro 0x0050FE10..0x0050FE20 / 0x0052C53C..0x0052C566; B2 0x0051121A..0x00511250; B3 bind destroy slots 0x0061B778/0x0061B77A; B4 0x00511418..0x00511424."
]
```

### M10-015

Title: "RobotGyroDriftDetector::DetectGyroDrift and DetectBias on each RobotState"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "UpdateFullRobotState: ldr.w r0,[r4,#0x27C]; blx 0x4A7C6C RobotGyroDriftDetector::DetectGyroDrift 0x00512FB4..0x00512FBA and blx 0x4A7C78 DetectBias 0x00512FC4",
  "re-analysis/research/overnight/20261014-ov23-24-check.md: R1 hook 0x00512FB4..0x00512FCE; R2 DetectGyroDrift 0x0052C568..0x0052C770; R3 DetectBias 0x0052C804..0x0052CB24; inputs MovementComponent 0x0063E31A, HandleCliffEvent 0x005356C6..0x005356D4."
]
```

### M10-017

Title: "The raw IMU magnitude at R+0x378 in UpdateFullRobotState: vsqrt((x2+y2)+z2) with the sqrtf fallback on NaN"

Status: `IMPLEMENTATION_GAP`

Evidence:
```json
[
  "re-analysis/research/overnight/20261014-ov23-24-check.md: UFRS 0x005129C0..0x00512A1E (bvc to sqrtf), store R+0x378; R+0x37C smoothing is M7-021."
]
```
