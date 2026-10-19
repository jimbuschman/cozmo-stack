# M3+M4 settling pass, part A (Opus, read-only), 2026-10-18, HEAD 797f229e

- **Run:** read-only cozmo-verifier (cubes, block pool, cube lights). Interrupted once by an API rate limit and resumed.
- **Commands:** `fidelity.py --check` passes (702 records). Focused `dotnet test` (PrimeCheckedRows, M3M4X12/13/14/17/21/28,
  M4ControlTests, M11DelocalizationBuilder, CubeDisconnectTailCheckedRows, `EngineAppLayerTests.M4_01*`): 217/217.
- **Overall: FAIL.** M4-041 contradicts source; M4-049 and the M4-018 completion callback are omitted; the M4-011 scope-4
  policy record is missing.
- **Adoption:** pending the manager (with part B).

## Verdicts

| Record | Verdict | Reason |
| --- | --- | --- |
| M4-009 | SETTLE after queued cleanup | Moved/Stopped order re-read 0x00533E4C..0x00534406, matches; M13-008 charger-id writer is a clean handoff; tests unlabelled |
| M4-010 | SETTLE after queued cleanup (prior checks) | Nothing open; CD7 checked by OV61/B-M11-13; addresses not re-read; tests unlabelled |
| M4-011 | NOT YET | Scope-4 host input (blockPool.txt location) has no COMPATIBILITY_POLICY; frozen evidence wrongly says local application data |
| M4-018 | NOT YET | Two unrecorded gaps: the PlayLightAnim completion callback (store 0x0063858C; invoked in Update 0x006379C6..0x00637A48), and relative-mode SetObjectLights (MakeStateRelativeToXY 0x004E0DEC), which M11-004 does not cover |
| M4-037 | SETTLE after queued cleanup | Log text; format 0xBE95E1 matches `Cubes.cs`:828-829; no tag in `Cubes.cs` (its location) |
| M4-041 | NOT YET (in-layer, cheap) | Ctor LED state read: all zero (0x004E0C6E..0x004E0CAC); the MISSING stop at `Lights.cs`:1115-1116 contradicts it and is reachable |
| M4-043 | SETTLE after tag fix | "std::exception" (libc++ 0x7C3C8), key/format 0x0061B368/0x0061B380 match `CubeConnections.cs`:748; no tag |
| M4-044 | SETTLE after queued cleanup (B-M3M4-26/27 PASS) | Only M5-051's reader-error debug bytes remain (log, M5-051's) |
| M4-045 | NOT YET, only through M4-041 | Null-object modifier=false path matches; modifier=true null-object skip to be recorded under M1-034 |
| M4-046 | SETTLE after queued cleanup | Body tests derived independently; modifier=true caller is M15-037's |
| M4-048 | NOT YET (cheap) | Handler order re-read 0x0063A3A6..0x0063A438, matches; no production test |
| M4-049 | NOT YET (in-layer, cheap) | Slot +0xC returns 1 only for ActiveCube and Charger; the stack never calls the callback in production (`BlockWorld.cs`:2347-2348 MISSING) |
| M4-050 | SETTLE after tag fix | NextPrime matches libc++ 0x0003B038; test values from table 0x0009B480; no tag in `ActiveObjectTable.cs` |

## Objections

- **`Lights.cs`:1113-1117 | CONTRADICTED (blocking).** A connected object with no retained LED state stops PlayLightAnim
  with "MISSING: M4-041" when any LED word is zero. The ActiveObject base ctor 0x004E0C6E zeroes LED state: for i = 0..3 at
  +0xC+0x1C·i, ColorRGBA(0) on/off (0x004E0C90, 0x004E0C9A) and memclr4(+8, 20) (0x004E0CA4). GetLEDState returns
  this+0xC+0x1C·i (0x0063956C..0x006395FE), so Blend reads zero, sets the flag (0x00638B86..0x00638B9A) and blends. A new
  ActiveCube is made on every connection (0x00623296). Reachable: the ObjectInfo survives a disconnect; a reconnect inside
  WakeUp's non-overridable 1280+5100 ms window is refused (`M4_018_LC8_AReconnectCannotOverrideRunningWakeUp`); with no
  SetLEDs yet, DanceFadeOff, Flash or SpeedTapWin (`fadeOff_1sec.json`, `flash.json`, `speedTapWin.json`) through the
  public PlayLightAnim stop in the stack where the engine plays.
- **`Vision/BlockWorld.cs`:2347-2348 | OMITTED (blocking).** Every pose-state change logs MISSING M4-049;
  OnActiveObjectPoseStateChanged is called only from a test hook (`Lights.cs`:1043). Engine: BroadcastObjectPoseStateChanged
  0x005062B8..0x005062D6 calls vtable +0xC, then tail-calls 0x008CAD3C. Slot +0xC is 0x004E385D (`movs r0,#1`) in
  vtable(ActiveCube)+0xD4 and vtable(Charger)+0xE0; 0x004E0241 (returns 0) in ObservableObject, CustomObject,
  MarkerlessObject, HumanHead, Block, Block_Cube1x1, Block_2x1, Bridge, FlatMat, MatPiece, Platform, Ramp (full vtable
  scan). Arguments: r2 = new state, r3 = obj+0x24; Visible (0x25) when r3 == 1, else Connected; return when r2 == r3
  (0x00639834..0x0063987A). Matches the stack handler signature `(id, oldState, state)`.
- **`Lights.cs`:1071-1163, 1180+ | OMITTED (blocking for M4-018's claim).** PlayLightAnim's std::function is stored in
  CurrentAnimInfo (0x0063858C); Update copies it, pops the entry, and calls it if non-empty and the guard passes
  (0x564A64 == 1), at 0x006379C6..0x006379F4 and 0x00637A02..0x00637A2E. The stack has no callback parameter or call. No
  record owns it (M4-045 says "remain unclaimed"; no record names TriggerCubeAnimationAction). No stack consumer yet;
  natively actions such as TriggerCubeAnimationAction use it.
- **`Lights.cs`:1530-1531 | OMITTED / handoff not clean.** Relative-mode ObjectLights stops with "MISSING: M4-018 /
  M11-004". Unowned: GetLocatedObjectByIdHelper(id, −1) 0x00637E82, dynamic_cast to ActiveCube, SetLEDs 0x00637EDA,
  MakeStateRelativeToXY 0x004E0DEC called at 0x00637EE8. M11-004 ("BlockWorld connected-object rule, moving and rotating
  gates, position-update thresholds") does not cover it; M15-037 covers only the producer.
- **`CozmoRobot.cs`:431, 570-571 | MANIFEST / policy missing (blocking).** blockPool.txt goes to `Options.BlockPoolPath`
  or `LocalApplicationData/cozmo-stack/blockPool.txt`. Engine: scope 4 = DataPlatform+0x18, DataPlatformExternalPath, set
  by the app to `temporaryCachePath` (`research/20261013-test-loggers-and-backup-check.md`:17, 101-102). Analogues M3-061
  (scope 0), M3-062 (scope 1), M5-060 (scope 2). M4-011's frozen evidence ("the platform's equivalent of that data scope")
  is wrong.
- **`Lights.cs`:1123-1124 | VISIBILITY (queued).** modifier=true with a null object is UB in the engine (heap read
  0x006386DA), reachable through public `PlayLightAnim(..., modifier: true)`. The stack logs MISSING/M15-037 and returns
  false before pushing; the M1-034 standing rule covers it, so the log should name M1-034 and M4-045's `unresolved` record
  the crash point and skip.
- **Manifest test fields | VISIBILITY (queued).** Stale M4-018 names: `M4ControlTests.M4_018_LC8_AReconnectReplaysWakeUpWithoutGamma`,
  `SourceLogMigrationTests.B8_UnmatchedCubePatternAndUnknownPathStatusStayOnTheStackStream`. Almost no cited test has a
  `reach:` label.
- **Tags.** M4-037: none in `Cubes.cs` (tags in `PoseOriginNames.cs`, `BlockWorld.cs`, `CozmoEngine.cs`). M4-043: none
  anywhere. M4-050: none in `ActiveObjectTable.cs` (table tagged M4-010).

## Per record (supported, with citations)

- **M4-009.** 0x00533E96 lookup → 0x00533EA6 obj+0x18 vs robot+0x334 → 0x00534238 info → 0x00534278 double tap →
  0x005343C8..0x00534402 SetIsMoving on change → 0x00533F60 located loop (0x0053401E, DifferentIDs 0x005340CC,
  0x0053411E/0x0053413C) → id −1 0x00534168 → dock-target/carried exclusion 0x0053416E..0x0053418C → Broadcast 0x005341B0.
  `Cubes.cs`:709-731 same order. robot+0x334 starts −1, sole writer MountChargerAction::Init; M13-008 names 0x0054E11C as
  its own (`ChargerActions.cs`:351 ReportMissing): clean. Slot-view MISSING `Cubes.cs`:602 is M11-013/M11-041's.
- **M4-010.** No open item; tests production through `EngineAppLayerTests`; the all-five pool test substitutes replies
  (stated).
- **M4-011.** RemoveObjectFromPersistentPool is M4-042's; Save text M4-043's. Queued: `CubeConnections.cs`:737
  `File.Delete` throwing is caught as Save.Error and skips the write; the engine ignores remove's result.
- **M4-018.** Re-read and supported: gates 0x006382EA..0x006384D6; branch condition 0x00638510..0x0063859A; non-blended
  zero-duration clear 0x006385EA..0x006385F4; timer/offset 0x006386CA..0x006386E8; canBeOverridden from last pattern
  0x006386EC..0x006386F4; LightsNotSet signed compare 0x00638700; SettingLights → layer store → SendTransitionMessage →
  SetObjectLights, timer zeroed on nonzero result 0x00638790..0x006387E0; SetObjectLights null path 0x00637EFC..0x00637F32
  (0x04000000). WhiteBalanceColor 0x0063A894..0x0063A98E: u32(((b/255f)·0.6f)·255f) (0x3B808081, 0x3F19999A, 0x437F0000)
  equals the stack's `(byte)(c.G*0.6f)` (`Lights.cs`:1593-1594) in f32 for all 256 inputs; GetU8 clip cannot fire.
- **M4-050.** libc++ 0x0003B042 `cmp r0,#0xd3` → table 0x0009B480 (0, 2, 3, … 211); `cmn r0,#4` 0x0003B074 overflow.
  `PrimeCheckedRowsTests`: 1..100 from the table, boundaries by independent arithmetic, overflow text 0x3B66C. Unit reach;
  production join is M4-011's RSSI-tie test.
- **M4-041.** StopLignAnim.Result is log only. GetLEDState's other caller 0x0063A642 is game SetObjectLights (M4-040).
- **M4-045.** Null object, modifier=false: Blend logs NullActiveObject, returns 0 (0x00638BAA..0x00638BE8); non-blended push
  of the original list; SettingLights, layer store; SetObjectLights logs NullActiveObject, returns 0x04000000, timer zeroed
  (0x006387DE); returns true. `Lights.cs`:1107-1162 with :1533 matches.
- **M4-046.** `ModifierOrsWordsAdds…` expected values hand-derived from 0x00638AD0..0x00638B1A; `RegrownSlot…` reaches
  modifier=true through the public API. Caller is M15-037's.
- **M4-048.** +0x21 := 1 0x0063A3A8; std::map walk (signed ObjectID); IsCarryingObject 0x0063A3D0; PlayLightAnim(id, 1,
  layer 2, empty, false, zero, 0) 0x0063A400. `Lights.cs`:1007-1013, wired `CozmoRobot.cs`:348,
  `DelocalizationRecipients.cs`:56. Tests call the handler directly or substitute the hook.

## Test reach

| Record | Production | Unit-only (what is substituted) |
| --- | --- | --- |
| M4-009 | `M4_009_CD10a…`, `CubeTelemetryLandsOnTheConnectedCube`, `M4_023_CD10g…`, `B9_CubeConnectionEvents…` (unlabelled) | `M3M4X12…CubeReports…` (labelled), `M3M4X13…DifferentIds…`, `M3M4X28…` |
| M4-010 | `EngineAppLayerTests M4_010_*` (unlabelled) | all-five pool test; some `CubeDisconnectTailCheckedRowsTests` |
| M4-011 | `M4_011_*`, `B11_PoolLoad…` (unlabelled) | `M3M4X12…ParseError…` |
| M4-018 | `M4_018_LC1_LC6…`, `M4_018_LC8_…CannotOverride…`, shipped zero-animation plays | `M3M4X14/17/21` component calls |
| M4-037 | none | `M3M4X13` |
| M4-041 | none for the fresh-object case | `M3M4X14` |
| M4-043 | none | `M3M4X13…SaveFailure…` |
| M4-044 | `M3M4X21…CollectedCubeFiles…` | `M3M4X14…FatalFile…` |
| M4-045 | `M3M4X21…ShippedShorterSlot…` | none |
| M4-046 | `M3M4X21…RegrownSlot…` | `ModifierOrs…` |
| M4-048 | none | `M4ControlTests`:2669/2741; `M11Delocalization` (substituted hook) |
| M4-049 | none (no production join) | direct callback tests |
| M4-050 | M4-011's RSSI-tie test (indirect) | `PrimeCheckedRowsTests` |

## Manager actions proposed

1. Tags: M4-037 in `Cubes.cs` (or move its location to `PoseOriginNames.cs`); M4-043 at `CubeConnections.cs`:732; M4-050
   at `ActiveObjectTable.cs`:211-216.
2. New COMPATIBILITY_POLICY: DataPlatformExternalPath (scope 4; app input temporaryCachePath), naming `BlockPoolPath` and its
   default; correct M4-011's evidence line through re-approval.
3. New record: the PlayLightAnim completion callback (0x0063858C; 0x006379C6..0x00637A48; guard 0x564A64), in-layer or
   handed to the action layer.
4. New record: relative-mode SetObjectLights (0x00637E82, dynamic_cast, MakeStateRelativeToXY 0x004E0DEC); correct M4-018's
   "→ M11-004" line.
5. Build (cheap): M4-041 all-zero ctor state replacing the stop, test reconnect-inside-WakeUp then Flash; M4-049 wire
   `BlockWorld.cs`:2347 for ActiveCube and Charger only, production test; M4-048 production test.
6. Queued: M1-034 wording and `unresolved` entry for the modifier=true null-object skip; stale M4-018 test names; `reach:`
   labels; the File.Delete edge in Save.
