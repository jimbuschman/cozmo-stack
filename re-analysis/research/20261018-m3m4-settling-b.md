# M3+M4 settling pass, part B (Opus, read-only), 2026-10-18, HEAD 797f229e

- **Run:** read-only cozmo-verifier. Interrupted once by an API rate limit and resumed.
- **Checked:** M3-012, M3-021, M3-055, M3-060, M4-019, M4-027, M4-038, M4-047 (addresses re-read with `disarm.py`/capstone);
  the 28 COMPATIBILITY_POLICY / EQUIVALENT_IMPLEMENTATION records; the 7 HARDWARE_ONLY records.
- **Commands:** `fidelity.py --check` (702, exit 0); focused `dotnet test` (M3_012, M3_021, M3M4X10/12/13/15/16, M4_019,
  M4_027, M4_031, M5X17, ReportOrderingRegressionTests, B9_, B5_Camera): 132/132.
- **Overall: FAIL.** 3 settle after queued cleanup; 5 NOT YET.
- **Adoption:** pending the manager (with part A).

## Cross-record finding

No record owns RobotStateHistory's query and admission results (`ComputeStateAt`, `GetLastStateWithFrameID`, the
`AddRobotStateToHistory` failure). They appear only as consumers (M4-019, M4-038, M10-007, M10-015, M11-044, M13-030,
M14-005). The handoff "M11-051/M11-053" is misnamed: M11-051 is "RobotStateHistory::AddRawOdomState gates (drop,
consecutive-gap reset, parent check, duplicate key)"; M11-053 is "Robot::Delocalize callees and the frame-id-mismatch
Delocalize trigger (Robot+0x2C0 counter)".

## Verdicts

| Record | Verdict | Reason |
| --- | --- | --- |
| M3-012 | SETTLE after queued cleanup | Budgets, counters, failed-send rule, three call sites match; `reach:` labels missing |
| M3-021 | NOT YET (narrow) | Source steps match; M11-059/060 handoffs clean, lock gone. Race: public `SetCameraSettings`/`SetAndDisableAutoExposure` produce on the caller's thread into the unlocked pending tuple; the original has engine-thread producers only |
| M3-055 | NOT YET (record only) | Built calls match; remaining MISSING (Configure roots, platform selector) touches none of 1–4; needs a recorded manager exclusion naming M3-052/M3-051/M3-058 |
| M3-060 | SETTLE after queued cleanup | Formats, widths, set-global-then-event order match 0x005367D8.. and 0x00536FB0..0x00537028 |
| M4-019 | NOT YET | (i) In-layer: the stack's `_cliffHistory` admits states the native robot+0x390 history never holds; (ii) the history-result handoff has no owner and lives in a comment |
| M4-027 | NOT YET (narrow, closable) | shared+14 identified: the G2E 0x62 EnableBlockTapFilter subscription handle; one observable edge in M4-023 |
| M4-038 | NOT YET | `ComputeStateAt` MISSING in production, so every detected cliff skips AddCliff and its logs; the record still claims `ComputeStateAt`; split needed |
| M4-047 | SETTLE after queued cleanup | Miss path matches 0x0063FE5C..0x0063FFBA; the PrintLockState MISSING is stale |

## M3-012

- UpdateAmountToSend 0x0057C6F6..0x0057C7AC: `ldm` robot+0x238..0x244; free = played − streamed + 0x2000; overflow Info
  only when streamed < 0 and played ≥ 1 (0x0057C702..0x0057C70E) with (free, streamed, played); free < 0 → warning then 0;
  min(free, 0x7530); max(framesPlayed − framesStreamed + 14, 0) at +0x98/+0x9C. `AnimationScheduler.cs`:143-160 matches.
- AnimationState handler 0x00537FD0..0x0053800C (gate robot+0x29; +0x238, +0x240, +0x348, +0x248) = `CozmoEngine.cs`:915-920.
- Drain 0x0057BF60..0x0057C00A: unsigned size test and `frames < isAudio` equivalent to `AnimationScheduler.cs`:206-207
  (budgets never negative); counted only after a successful send (0x0057BFAE..0x0057BFCC; :209-215). EndOfAnimation
  0x0057C464..0x0057C496 counted on success.
- Callers StreamLayers 0x0057C4F8, UpdateStream 0x0057C8E8, Update 0x0057D16C, each followed by SendBufferedMessages =
  `RefreshBudget()`+`Drain()` at `AnimationScheduler.cs`:1688-1689, :1424-1425, :1237-1238; Update's warning precedes the
  budget (0x0057D142 → 0x0057D16C; :1236-1237). Production always paced (`CozmoAnimations.cs`:57). Tag `AnimationScheduler.cs`:72.
- Queued: no `reach:` labels; `M3_012_C10_...` is production. Expected values from C9/C11; not circular.

## M3-021

- HandleDefaultCameraParams 0x00657CA0..0x00657D12; SetCameraExposureParams 0x006B92F0..0x006B9440; IsExposureValid
  0x006B9DAA..0x006B9DBE (signed); IsGainValid 0x006B9E80..0x006B9EA8 (NaN invalid); SetCameraSettings 0x006560E8..0x006561A4;
  SetNextCameraParams 0x006B229C..0x006B230C (no lock). `Camera.cs`:690-765 matches.
- Handoffs: lock gone (`Camera.cs`:717-726; `CameraQuality.cs`:24-31); IsInitialized/initial exposure visible at
  `Camera.cs`:742 naming M11-059; pending apply M11-060 built at `VisionSystem.cs`:1024. No split needed.
- **Race (blocking class).** Native callers of SetCameraSettings are engine-thread only: UpdateImageQuality 0x006555A2,
  HandleDefaultCameraParams 0x00657CCA, SetAndDisableAutoExposure 0x00657EFA (behaviours 0x005FAD24, 0x005FBDA0,
  0x005D10C4). The stack's public `CameraSettings.SetCameraSettings(ushort, float)` (`Camera.cs`:688) and
  `SetAndDisableAutoExposure` (`CameraQuality.cs`:12) write `_nextExposureMs/_nextGain/_nextCameraParamsPending` on the
  caller's thread while the engine thread's DefaultCameraParams handler (`CozmoRobot.cs`:787) writes the same tuple.
  OV15-17's lock removal assumed one engine-thread producer (`M3M4X10BuilderTests.cs`:116-117).
- Queued: `Camera.cs`:646-649 doc contradicts the MISSING; "auto-exposure 1" provenance contradicted (OV14); frozen test
  names `CameraPendingClearGapIsVisibleAndDoesNotEmitStaleOverwriteWarning` and
  `CameraConcurrentRequestsProduceOneWarningForTheActualPredecessor` assert the opposite of their names.

## M3-055

- Startup order `DasControl.cs`:100-117 matches cozmo_startup (archive config; Enable/Disable(2) 0x00665C1E/0x00665C26
  before DASConfigure 0x00665CD6; DASNativeInit("cozmo") 0x00665F3A; Pause(false); $player_id MD5; flush callback). Connect
  Pause(true) `CozmoEngine.cs`:2169; RemoveRobot $session_id clear + Pause(false) `RobotLifetime.cs`:188-196;
  DisableNetwork(4) `CozmoEngine.cs`:2436 (prior-checked).
- Configure roots 0x00665C2E..0x00665CD6: base + "/DASConfig.json" (M3-052; only in-scope effect is sink (a)'s level filter,
  M3-053); scope 2 "DASLogs" and scope 3 game logs are sink (b) directories (M3-051). None touches 1–4.
- Platform selector 0x00665E9C..0x00665F14: `get("standalone")` (0x00666824) → `asBool`; true → JNI activity →
  `DASPlatform_Android::Init`; false → `InitForUnityPlayer()`. Android-layer machinery; its engine-visible product (device
  ID, vtbl+0x10) is M3-058. Touches none of 1–4.
- Missing: a recorded exclusion in M3-055's `unresolved`; then replace the MISSING at `DasControl.cs`:107.

## M3-060

Head: "0xbeef%04x%08x" (0x005368B4), u16 hwVersion then u32 serial; "$group" 0x00536810; event 0x00536848. Body:
"0xbeef%04x%04x%08x" (0x005370D0), color, bodyHw, serial (0x00536FCC..0x00536FE8); "$phys" 0x00536FF0; event 0x00537028.
`RobotSubscriptions.cs`:141-158 matches. Test unit-level, unlabelled.

## M4-019

- Supported: UpdateCliffDetectThreshold 0x006343B8..0x006344B8 (`Sensors.cs`:462-487); UFRS tail 0x00512FB0..0x00512FD6
  (`Sensors.cs`:1080-1085, :1034-1039); HandleRobotStopped order (`Sensors.cs`:893-915). Prior checks stand for SC1–SC8,
  Increment (0x00634514..), Clear (0x006347A4..), the 50/400 schedule.
- **In-layer (blocking, robot wire).** robot+0x390 is filled by `AddRobotStateToHistory`: on-ramp branch 0x00512BE2 →
  0x00512BEE; otherwise only after `ContainsOriginID` (0x00512C44 → beq 0x00512EC4, origin-reject warning, r6 = 1
  0x00512F0C). Origin-rejected states and add failures (0x00512C38) never enter. `_cliffHistory` (`Sensors.cs`:980-985)
  appends every synced state with cliff data before any origin check, duplicates included. Extra samples can raise
  IncrementSuspiciousCliffCount, which sends a threshold.
- **Handoff gap.** C12.4 gate on add failure and `GetLastStateWithFrameID` failure (0x00513088 → 0x005130EA, 0x00513110)
  exists only as a comment (`Sensors.cs`:1054-1056). `GyroDriftDetect` is never assigned in `src`.
- Dependency: the on-ramp flag +0x316 has no stack writer (native 0x005380D2, 0x005381E6; the M1+M2 pass left it under M2-018,
  since accepted; the manager rechecks its owner).
- Handoffs: drone → M4-039 (clean); Cancel(-1) → M8-015 (clean, wired `Sensors.cs`:912); behaviour end → M8-019 (wired only
  under FreeplayStack `FreeplayStack.cs`:233, else visible MISSING; clean); detectors → M10-015 (hook never assigned);
  history results → no owner.

## M4-027

shared+14 = BlockTapFilterComponent+0x14: ctor clears +4..+0x17 (0x00630862); stores the `shared_ptr<ScopedHandleContainer>`
from the external interface's subscribe (vtbl+0x2C, `blx r7` 0x00630A2E), tag 0x62 (0x00630A14), handler
HandleEnableTapFilter, store 0x00630A3A. So 0x00511206..0x0051120A is the final release of the EnableBlockTapFilter
game-message subscription. Observable only as: a game EnableBlockTapFilter after robot removal reaches nothing; a new
component starts enabled (+0x18 = 1, 0x00630874). Stack: `Cubes.SetBlockTapFilterEnabled` (`Cubes.cs`:392, M4-023) is an
ungated setter; `ResetToConstructed` sets true at removal (`Cubes.cs`:331), so a call between removal and the next robot
carries `false` into the next robot where the original drops it. After a ruling, replace the MISSING at `Cubes.cs`:374
(which mislabels "M2-023").

## M4-038

Title and frozen evidence still claim `ComputeStateAt` on robot+0x390 and AddCliff steps 1–7. Production `ComputeCliffPose`
emits "MISSING: M11-051/M11-053 ComputeStateAt" (`Sensors.cs`:836) and returns null, so every enabled cliff with flags ≠ 0
skips AddCliff (BlockWorld object, pose-state broadcast 0x008CAD1C, `robot.object_located`), the Detected Info (0x005357F4)
and the failure tail. Map insert: M13-030 (covers it) and M11-048 (RECOVERABLE_GAP, FastPolygon/GetOverlapType). Split:
M4-038 keeps flag gate, result branch, caller logs and the M4-side AddCliff call; a new record owns `ComputeStateAt`.
Tests M3M4X13/X16 substitute historical input production cannot produce; unlabelled.

## M4-047

UnlockTracks 0x0063FE5C..0x0063FFBA: per bit `__tree::find`; erase on hit with OR (0x0063FF1A..0x0063FF22); miss → Info then
PrintLockState (0x0063FF76); count re-read, bit ORed (0x0063FF7E..0x0063FF88); EnableAnimTracks when mask ≠ 0
(0x0063FF96..0x0063FFB4). `Motion.cs`:327-353 matches. MISSING at `Motion.cs`:346 is stale: `PrintLockStateLocked()` is
called at :347, body M4-031 (`Motion.cs`:393-406). Queued.

## Test reach

| Record | Production | Unit-only |
| --- | --- | --- |
| M3-012 | `M3_012_C10_...` (unlabelled) | C9/C11/C14/B9/OverflowAndNegative (direct, reflection); `TheByteBudgetBindsSampleFramesAt10` (substituted counters) |
| M3-021 | `M3_021_1l_SetCameraSettingsRangeChecks` (labelled); 1k via `rig.Data`+`Tick` (unlabelled) | X10/X12 (reflection / offline Handle) |
| M3-055 | none | X12/X13/X15 (offline startup, fixture path) |
| M3-060 | none | `NativeHeadBodyEvents...` (direct Broadcast) |
| M4-019 | `ReportOrderingRegressionTests` (labelled) | M4ControlTests (direct handler) |
| M4-027 | none | M4AbortSleepTests |
| M4-038 | none | X13/X16 (substituted history) |
| M4-047 | M5X17 double unlock (unlabelled) | — |

## Policy confirmation

| Record | Legitimate? | Basis |
| --- | --- | --- |
| M3-004 | Needs a recorded decision | Matches source (no discard); warm-up flag is a stack-only API addition, empty `unresolved` |
| M3-017 | Needs a recorded decision | Stack-only tone API sending robot audio through the shared budget, 1 s StallTimeout; no decider |
| M3-019 | Yes | M1-034 standing rule (only byte 6 written, 0x00658416); queued: cite the rule |
| M3-020 | Yes | M1-034 standing rule (`data[-1]`) |
| M3-037 | **No** | 2026-09-30 NEEDS EXTRACTION unresolved: bytes past a short payload are the previous frame's (0x004F1DA2/0x004F1DAA, defined); SetNextImage 0x00652B04 untraced. Trace, or record raw encodings unreachable with evidence beyond M3-050 |
| M3-039, M4-029, M4-030 | Yes | SDK scope rule |
| M3-042 | Yes | App input, manager 2026-10-10 |
| M3-044, M3-046 | Yes | Tool-only, manager 2026-10-10 |
| M3-045, M3-051, M3-054 | Yes | Operator departures |
| M3-049 | Yes | M1-034 |
| M3-052, M3-056, M3-058, M3-059, M3-061, M3-062, M3-063 | Yes | App-boundary scope rule |
| M4-006 | Needs a recorded decision | Stack-only confirmation; confirm it adds no wire traffic (original sends DriveWheels verbatim, 0x0063ED82..) |
| M4-032 | Yes | Log rendering |
| M4-034 | Yes | App input |
| M4-039, M4-040, M4-042 | Basis weak | Excluding a C# API feature (drone mode; cube-light game inputs; pool editing) needs an operator "not supported" decision under 2026-10-17 |

## Hardware coverage for the new M3M4 script

Code on the hardware path changed since the 2026-10-12 run (`Camera.cs` 7 commits, `NvStorage.cs` 7, `CubeAccel.cs` 3,
`CozmoEngine.cs` 39, `Control.cs` 1, `EncodedImageDecode.cs` 1, `M3M4Hw.cs` 1; `Display.cs` 0). Re-check all seven
HARDWARE_ONLY records. Cover: (1) M3-008 face rows and playback; (2) M3-016 colour on/off, encoding and width; (3) M3-050
every chunk's encoding (fix the misparse defect); (4) M3-036 factory read Length 1, single reply; (5) M4-013
StartMotorCalibration after connect; (6) M4-021 origin/frame before and after AbsoluteLocalizationUpdate; (7) M4-024 cube
telemetry around StreamObjectAccel on/off; (8) M3-012 AnimationState counters vs BytesStreamed/FramesStreamed during a long
audio animation, no negative-free warning; (9) M3-021 DefaultCameraParams and the SetCameraParams payload; (10) M4-019
50/400 sends, a stop over an edge (RobotStopped → Cancel → RobotStopped order; 150 send), PotentialCliff → StopAllMotors +
EnableStopOnCliff{0}; (11) M4-038 CliffEvent → visible MISSING and API CliffDetected; (12) M4-047 double unlock → extra
EnableAnimTracks; (13) M4-027 no motor/light sends after disconnect; (14) M3-060/M3-055 head/body DAS events, startup
flush-fail event.

## Manager actions proposed

1. A record owning RobotStateHistory query/admission results; re-point M4-019/M4-038; split M4-038.
2. Builder: S3 cliff-history admission follows robot+0x390 (after the origin check or on ramp, add failures excluded);
   runtime MISSING for the C12.4 gate until the new record lands.
3. Rule on the M3-021 host-thread producer race.
4. Record the M3-055 exclusion; remove the MISSING at `DasControl.cs`:107.
5. M4-027: record the identity; rule the M4-023 no-robot edge; replace the MISSING at `Cubes.cs`:374.
6. Queued: `Motion.cs`:346; `Camera.cs`:646-649; M3-021 provenance; `reach:` labels; decisions for M3-004, M3-017, M4-006;
   operator text for M4-039/040/042; M3-037 extraction.
