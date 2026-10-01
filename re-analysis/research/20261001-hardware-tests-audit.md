# Hardware acceptance fidelity-record audit (Q19)

Date: 2026-10-01  
Manifest: `re-analysis/fidelity_manifest.json` at `954c092`  
Code inspected: `Cozmo.Conformance` `LinkCheck`, `ControlCheck`,
`HardwareCatalog`, runner/evidence writers and their fidelity tests; all seven
checked-in bundles below `re-analysis/acceptance/hardware/`.

## Result

The **existence** check passes: all 225 distinct `M*-nnn` strings in the
conformance source name real records.  `LinkCheckFidelityTests`,
`ControlCheckFidelityTests` and `HardwareCatalogFidelityTests` enforce that
shallow property.

The live-path check does **not** pass.  The two self-contained evidence tools
(`link-check` and `control-check`) are substantially better than the campaign
catalog, but even `control-check` omits one hardware-only NV contract from its
connection check.  The 39-check campaign catalog generally names only the
feature at the end of the path.  Each runnable command creates a fresh
production connection, so a prerequisite on `LINK` does not remove that
connection from the check's live path.  Consequently 38 runnable catalog
checks hide the open connection path, and many additionally hide a relevant
robot-side or blocked boundary.

No hardware PASS can settle source provenance.  A script can settle only the
robot-side observation described by a `HARDWARE_ONLY` record.

## Common path omitted by the campaign catalog

Every runnable `HardwareCatalog` command starts a new CLI process and a new
production `CozmoConnection`; it does not reuse the earlier `LINK` process.
At minimum each check therefore should carry the applicable connection rows:

`M1-024, M1-025, M1-026, M1-028, M1-029, M1-030, M1-033, M1-040,
M1-041, M1-042, M3-019, M3-021, M3-022, M3-036, M4-020, M4-021`.

I call this set **CONN** below.  `M3-036` is included because the production
connection performs the calibration NV read; `ControlCheck.CONNECT` says that
it records that read but cites only `M3-021/M3-022`.  `M1-033` is an input to
every exchange.  `M4-021` matters whenever success is judged from RobotState
pose/frame data; merely listing `LINK` as a prerequisite does not expose it in
the later result.

## Self-contained evidence tools

| test | records named now | records it should additionally name | hidden uncertainty |
|---|---|---|---|
| `M1-LINK` | M1-001, 005, 008, 010, 011, 015, 017-022, 032-034 | none for its deliberately transport-only scope | none: M1-033 is emitted as an observation with `verdict:null` and is excluded from the verdict |
| `CONTROL/CONNECT` | M1-024-026, 028-030, 033, 040-042; M3-019, 021, 022; M4-020, 021 | **M3-036** | M3-036 is not in `HardwareOnly` and therefore is absent from `hardwareOnlyUncertainty`, although the check records the NV read |
| `CONTROL/STATE` | M1-024, 033, 041 | M4-020, M4-021 | M4-021 is in the top-level uncertainty list, but not on this check |
| `CONTROL/HEAD` | M4-001, 003-005, 016 | CONN (especially M1-033, M4-020/021) | connection and RobotState frame acceptance |
| `CONTROL/LIFT` | M2-003; M4-002-005, 016 | CONN | connection and RobotState frame acceptance |
| `CONTROL/DRIVE` | M4-006, 007, 014, 019 | CONN | M4-021 affects the pose-distance verdict; it is only top-level |
| `CONTROL/FACE` | M3-006-008, 015 | CONN | M3-008 is correctly listed in `hardwareOnlyUncertainty` and visual judgment is separate |
| `CONTROL/AUDIO` | M1-042; M3-010-014, 017 | CONN | M1-033 is top-level, not check-local |
| `CONTROL/ANIM` | M1-041; M5-001, 004, 006-008, 016, 018, 019, 023, 026, 028, 036 | CONN | M5-036 is correctly an unjudged observation/top-level uncertainty |
| `CONTROL/ANIM_CANCEL` | M5-008, 010, 023, 026, 028, 036 | CONN | M5-036 is correctly unjudged; the PASS stops before the keep-alive block |
| `CONTROL/CUBES` | M1-042; M4-008-011, 018, 023, 024; M9-017 | CONN | M4-024 is disclosed, but the accel presence criterion still depends on it; failure is ambiguous as the note says |
| `CONTROL/CAMERA` | M1-041; M3-001-005, 016 | CONN | M3-016 is correctly excluded from the grey-frame verdict and emitted as an observation |
| `CONTROL/DISCONNECT` | M1-015, 019, 025 | M1-033 and the applicable connection setup records | robot response/silence remains M1-033 |

The checked-in `CONTROL` bundles are historical snapshots and preserve the
manifest statuses and script hashes from their run.  They do expose
M1-033/M3-008/M3-016/M4-021/M4-024/M5-036 in later runs.  They cannot repair
the missing citation in the current source, and their PASS rows must not be
read as source-fidelity findings.

## Campaign catalog, per check

In the “should add” column, **CONN** means the common set above in addition to
the feature-specific IDs shown.  “Named” is the exact current catalog list,
compressed only into ranges.

| check | records named | should add from the live path | hidden uncertainty |
|---|---|---|---|
| LINK | M1-001-012, 033; M2-001,002,004,006 | M1-024-030,040-042; M3-019,021,022,036; M4-020,021 | M1-033 is printed as an ordinary related record, not a structured unjudged observation; M3-036 and M4-021 are absent |
| D | M2-002,003; M4-008 | CONN; M4-002-005,016 | endpoint success depends on the open RobotState path |
| F | M5-001,004,007,008,018; M3-015 | CONN; M5-006,016,019,023,026,028,036 | M5-036 (robot Start/End/leftovers) is invisible |
| FD | M3-006-009 | CONN; M3-015 | M3-008 is named but the runner has no general status-aware uncertainty presentation |
| AUD | M3-010-015 | CONN; M3-017 | audio-buffer behavior is judged through the open connection path |
| A | M9-001,002,004-016 (except 003),018-027; M6-001-007; M3-013,015 | CONN; M9-003,017 where the runtime path posts/streams them | M9-023 is named and `KnownIssue` is good; the four RECOVERABLE_GAP Wwise rows (013,014,024,025) are not surfaced as uncertainty |
| A2 | same as A | CONN; the selected song's exact M9-003/017 path if exercised | same hidden recoverable gaps and M9-023 |
| A3 | A plus M9-003,017 | CONN | M9-023 and RECOVERABLE_GAP 013/014/024/025 remain ordinary IDs rather than explicit uncertainty |
| MOV | M4-001-003,006,007 | CONN; M4-004,005,014,016,019 | pose/frame uncertainty M4-021 hidden |
| CR1 | M5-006,007; M7-010,017 | CONN; M5-028,036 | unbounded Start-without-End semantics M5-036 hidden |
| IDL | M7-004-010,016,017 | CONN; M5 live-animation wire rows including M5-028,036 | M5-036 hidden |
| CR3 | M5-006,008,023 | CONN; M5-026,028,036 | abort and leftovers are exactly M5-036, but it is not named |
| CR2 | M5-022; M1-014,015,025,026 | CONN; M5-026,036 | robot behavior at link loss/animation teardown hidden |
| E | M3-001-005,016,018 | CONN | M3-016 is named but not called out as unjudged; a human can observe it but a grey-frame PASS cannot settle it |
| B | M4-009,010 | CONN; M4-008,011,018,023,024; M9-017 | M4-024 hidden |
| K | M11-001-012 subset listed at line 504 plus M11-018,020 and M2-007 | CONN; M3 camera rows and M4 cube/world rows actually used | M4-021/M4-024 hidden; M11-004/007 are current implementation gaps but appear as ordinary records |
| V | M13-002,008,009,012; M12-001,003,005,013,014 | CONN; camera/marker/world records on which charger pose depends | M4-021 and current M12-001 gap hidden |
| W | M13-009,013 | CONN; motion/state rows | M4-021 hidden |
| G | M10-001,005; M7-015 | CONN; RobotState/off-treads input rows | M10-001 is a current implementation gap but not highlighted |
| C | M7-003; M4-008 | CONN; RobotState cliff/IMU input rows | current M7-003 gap hidden |
| H | M10-001,003-005; M7-001,002,011,014 | CONN | four current implementation gaps are presented like settled evidence |
| I | M4-012,013; M8-008 | CONN | M4-013 is properly named, but its HARDWARE_ONLY status is not made explicit to the operator |
| J | M10-002,006,007 | CONN; drive/state rows | M10-007 and M4-021 hidden as open dependencies |
| M | M11-009; M4-009; M7-001,002,011; M10-003,004 | CONN; M4-010,018,023,024 | cube forwarding M4-024 hidden |
| CR8 | M11-019 | CONN; RobotState/world-frame rows | M4-021 hidden |
| CR7 | M11-017 | CONN; camera and RobotState/world-frame rows | M11-017 is an implementation gap and M4-021 is hidden |
| L | M11-014,015 | CONN; motion/state rows | M4-021 hidden |
| Q | M12-002,011; M13-011 | CONN; localization/world/motion rows | M12-002/011 are implementation gaps; M4-021 hidden |
| CR4 | M12-002 | CONN; path/action lifecycle rows | a current implementation gap is the sole named record |
| X | M13-001,003-005,011; M12-002 | CONN; localization/world rows | M12-002 and M4-021 hidden as open dependencies |
| N | M12-001,003-005,007-009,013,014 | CONN; cube/world/localization rows | M12-001/007/008 are current implementation gaps; M4-024 hidden |
| O | M12-006,015 | CONN; carry-state/world rows | M4-021 hidden |
| P | M12-001,003,005,013,016 | CONN; cube stream/world rows | M12-001 and M4-024 hidden |
| R | M12-001,003,005,009,012,015; M13-007; M15-012 | CONN; cube/world rows | M12-001/012 and M13-007 are implementation gaps; M4-024 hidden |
| S | M13-002; M12-001 | CONN; cube/world/motion rows | M12-001 and M4-024 hidden |
| T | M13-002,014 | CONN; stack/world rows | M13-014 is an implementation gap; M4-024 hidden |
| U | M13-015; M12-003,005 | CONN; cube/world/docking rows | M4-024 hidden |
| Z | M15-001-012; M8-001-003,006,007; M7-011-014; M10-003,004 | CONN plus every camera/world/cube/motion/animation record reached by the selected activities | this is a whole-stack claim with numerous current gaps and M4-021/M4-024/M5-036 omitted; the list cannot support the claimed scope |
| Z2 | same as Z | same as Z plus the time/mood-decay production path | same hidden uncertainty; duration does not improve provenance |
| Y | M14-001-005,007; M11-016 | none while non-runnable | M11-016 is correctly exposed by `BlockedReason`; M14-006 is a separate TTS boundary, not on this face-detector path |

## HARDWARE_ONLY records: what a script can and cannot settle

| record | script coverage | can this package's script settle it? |
|---|---|---|
| M1-033 | `M1-LINK`, CONTROL, and all live catalog checks | **Partly.** Link observations settle only the exact frame types/directions exercised. The current unresolved question—whether the robot accepts packed 7/8/9 from the engine—needs a targeted sender; current scripts send none. |
| M1-043 | none | **No current script.** Needs a phone/kernel run that returns a zero-byte `recvmsg` and records errno; robot acceptance cannot settle it. |
| M3-008 | CONTROL/FACE, catalog FD | **Yes for physical row mapping/playback**, with a timed visual/camera observation. The current catalog prompt checks orientation/steadiness but does not measure playback period. |
| M3-016 | CONTROL/CAMERA, catalog E | **Yes for firmware-2457 output/reaction**, if color is explicitly enabled and raw flags/payloads are retained. A grey-frame PASS does not settle it. |
| M9-023 | catalog A/A2/A3 | **No.** A new reconstructed-stack listening test cannot establish how the stock app sounded. It requires an original-app/robot recording; a run can only compare after that reference exists. |
| M4-013 | catalog I | **Yes.** Observe the robot after the post-connection `StartMotorCalibration`; this is exactly a robot-side effect. |
| M5-036 | CONTROL/ANIM and ANIM_CANCEL; catalog animation tests omit it | **Yes, per exercised case.** Raw outbound/inbound capture plus physical state can settle Abort/leftovers, Start-without-End and echo behavior; locked-track suppression and indefinite keep-alive need separate targeted cases. |
| M4-021 | CONTROL/CONNECT records it; catalog omits it | **Yes.** Send `AbsoluteLocalizationUpdate`, retain subsequent raw RobotState origin/frame fields, and judge that observation explicitly. |
| M4-024 | CONTROL/CUBES records it; catalog cube tests omit it | **Yes.** Vary connection setup and `StreamObjectAccel`, retain raw cube telemetry, and distinguish spontaneous forwarding from command-caused forwarding. |
| M3-036 | no explicit verdict; CONTROL/CONNECT only records the calibration read | **Yes.** Send factory `Length=1` and a sufficiently large non-factory request, retaining requests and replies/re-request sequence. |

The two current `BLOCKED_EXTERNAL` records are not hardware facts:
`M11-016` is correctly attached to blocked check Y; no robot script can recover
the OKAO detector implementation. `M14-006` has no catalog check and a robot
run cannot recover the third-party voice model/plugin semantics.  (The shipped
artifact recheck found package material that warrants revisiting both record
texts, but that is an inventory decision, not a hardware verdict.)

## Required correction before treating the campaign as a fidelity gate

1. Make result generation expand each check to its actual live-path records
   (or explicitly inherit and serialize prerequisite records); do not rely on
   prose prerequisites.
2. Load current manifest status and render every `HARDWARE_ONLY`,
   `BLOCKED_EXTERNAL`, `RECOVERABLE_GAP`, and `IMPLEMENTATION_GAP` separately
   from pass/fail criteria.
3. Add M3-036 to `CONTROL/CONNECT` and its structured hardware-only list.
4. Add targeted checks for the unresolved portions of M1-033, M3-008,
   M3-016, M4-013, M4-021, M4-024, M5-036 and M3-036.  Keep M9-023 explicitly
   dependent on an original-app acoustic reference.
5. Add a test that fails when a catalog command reaches a fidelity-tagged
   production method whose record is absent from the check's serialized
   record closure.  The current tests prove ID existence and hand-picked
   inclusions only; they do not prove live-path completeness.
