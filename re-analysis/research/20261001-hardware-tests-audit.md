# Hardware acceptance fidelity-record audit (Q19)

## Coverage audit (2026-10-01)

| test/item | coverage | checked basis |
| --- | --- | --- |
| self-contained `M1-LINK` | CHECKED | `LinkCheck.CitedRecords`, result writer, transport-only scope and `LinkCheckFidelityTests` traced. |
| self-contained `CONTROL/CONNECT` | CHECKED | Check definition, connection run, recorded NV/calibration observations and uncertainty writer traced. |
| self-contained `CONTROL/STATE` | CHECKED | Check definition, RobotState rate/battery criterion and production state path traced. |
| self-contained `CONTROL/HEAD` | CHECKED | Check definition, command send and RobotState feedback path traced. |
| self-contained `CONTROL/LIFT` | CHECKED | Check definition, calibration prerequisite, command send and feedback path traced. |
| self-contained `CONTROL/DRIVE` | CHECKED | Check definition, stop-on-cliff prerequisite, drive sends and pose/state verdict traced. |
| self-contained `CONTROL/FACE` | CHECKED | Check definition, face send, counters and human observation traced. |
| self-contained `CONTROL/AUDIO` | CHECKED | Check definition, audio send and robot counter verdict traced. |
| self-contained `CONTROL/ANIM` | CHECKED | Check definition, asset load, stream lifecycle and keep-alive observation traced. |
| self-contained `CONTROL/ANIM_CANCEL` | CHECKED | Check definition, cancel/abort send, buffered-frame allowance and keep-alive observation traced. |
| self-contained `CONTROL/CUBES` | CHECKED | Check definition, cube connection/stream requests and telemetry verdict traced. |
| self-contained `CONTROL/CAMERA` | CHECKED | Check definition, stream open, image decode/save and grey-frame verdict traced. |
| self-contained `CONTROL/DISCONNECT` | CHECKED | Check definition, disconnect send, post-dispose silence and bounded return traced. |
| catalog `LINK` | CHECKED | Exact `FidelityRecords`, command and fresh-process connection path traced. |
| catalog `D` | CHECKED | Exact records, sensors command and lift/state live path traced. |
| catalog `F` | CHECKED | Exact records, animation command and stream live path traced. |
| catalog `FD` | CHECKED | Exact records, face command and display path traced. |
| catalog `AUD` | CHECKED | Exact records, tone command and audio path traced. |
| catalog `A` | CHECKED | Exact records, sing command and selected Wwise/live-audio path traced. |
| catalog `A2` | CHECKED | Exact records, selected-song command and Wwise/live-audio path traced. |
| catalog `A3` | CHECKED | Exact records, sustained/vibrato command and Wwise/live-audio path traced. |
| catalog `MOV` | CHECKED | Exact records, drive command and state/pose path traced. |
| catalog `CR1` | CHECKED | Exact records, CORE-001 command and live-body stream path traced. |
| catalog `IDL` | CHECKED | Exact records, behavior command and idle/live-animation path traced. |
| catalog `CR3` | CHECKED | Exact records, CORE-003 command and cancellation path traced. |
| catalog `CR2` | CHECKED | Exact records, CORE-002 command and link-loss teardown path traced. |
| catalog `E` | CHECKED | Exact records, color-camera command and frame path traced. |
| catalog `B` | CHECKED | Exact records, cubes command and connection/telemetry path traced. |
| catalog `K` | CHECKED | Exact records, calibration/localization command and camera/world path traced. |
| catalog `V` | CHECKED | Exact records, mount command and charger/localization path traced. |
| catalog `W` | CHECKED | Exact records, drive-off command and motion/state path traced. |
| catalog `G` | CHECKED | Exact records, off-treads command and state-derived path traced. |
| catalog `C` | CHECKED | Exact records, falling reaction command and state/reaction path traced. |
| catalog `H` | CHECKED | Exact records, derived-reaction command and state/reaction path traced. |
| catalog `I` | CHECKED | Exact records, calibration command and robot-side calibration boundary traced. |
| catalog `J` | CHECKED | Exact records, unexpected-movement command and drive/state path traced. |
| catalog `M` | CHECKED | Exact records, cube-reaction command and cube/world path traced. |
| catalog `CR8` | CHECKED | Exact records, CORE-008 command and frame-change path traced. |
| catalog `CR7` | CHECKED | Exact records, CORE-007 command and ground-map path traced. |
| catalog `L` | CHECKED | Exact records, body-angle command and motion/state path traced. |
| catalog `Q` | CHECKED | Exact records, drive-to command and localization/planning path traced. |
| catalog `CR4` | CHECKED | Exact records, CORE-004 command and action/path lifecycle traced. |
| catalog `X` | CHECKED | Exact records, obstacle drive-to command and planner/world path traced. |
| catalog `N` | CHECKED | Exact records, pickup command and cube/world/manipulation path traced. |
| catalog `O` | CHECKED | Exact records, put-down command and carry/world path traced. |
| catalog `P` | CHECKED | Exact records, roll command and cube/world path traced. |
| catalog `R` | CHECKED | Exact records, stack command and cube/world path traced. |
| catalog `S` | CHECKED | Exact records, flip command and cube/world path traced. |
| catalog `T` | CHECKED | Exact records, knock-over command and stack/world path traced. |
| catalog `U` | CHECKED | Exact records, wheelie command and cube/docking path traced. |
| catalog `Z` | CHECKED | Exact records, freeplay command and reached whole-stack paths traced. |
| catalog `Z2` | CHECKED | Exact records, long-run freeplay command and time/mood path traced. |
| catalog `Y` | CHECKED | Exact records and non-runnable `BlockedReason` traced against current manifest status; no live command is executed. |
| HARDWARE_ONLY `M1-033` | CHECKED | Current scripts' sent/received frame observations compared with the record's exact unresolved question. |
| HARDWARE_ONLY `M1-043` | CHECKED | No current script induces or records the required zero-byte phone `recvmsg`/errno case. |
| HARDWARE_ONLY `M3-008` | CHECKED | FACE/FD observables compared with physical-row and playback-period unknowns. |
| HARDWARE_ONLY `M3-016` | CHECKED | CAMERA/E flags, retained frames and verdict compared with the color-format unknown. |
| HARDWARE_ONLY `M9-023` | CHECKED | A/A2/A3 output compared with the required stock-app acoustic reference. |
| HARDWARE_ONLY `M4-013` | CHECKED | Catalog I's post-connect calibration action and observable robot effect traced. |
| HARDWARE_ONLY `M5-036` | CHECKED | CONTROL animation observations and catalog animation omissions compared with every listed robot-side unknown. |
| HARDWARE_ONLY `M4-021` | CHECKED | CONNECT's origin/frame capture and every pose-based catalog verdict traced. |
| HARDWARE_ONLY `M4-024` | CHECKED | CUBES telemetry capture and all cube-dependent catalog checks traced. |
| HARDWARE_ONLY `M3-036` | CHECKED | CONNECT's NV-read capture compared with the two request/reply cases named by the record. |
| BLOCKED_EXTERNAL `M11-016` | CHECKED | Catalog Y's cited record and blocked result presentation traced. |
| BLOCKED_EXTERNAL `M14-006` | CHECKED | No current hardware test reaches or cites the TTS plug-in/model boundary. |
| bundle `20260924-112412-M1-LINK` | CHECKED | Result, environment, counters and frame/event payload inventory inspected as a historical script snapshot. |
| bundle `20260924-202748-CONTROL` | CHECKED | Result/environment, frames/events, images and operator-verdict payload inventory inspected. |
| bundle `20260925-061148-CONTROL` | CHECKED | Result/environment, frames/events and images inspected as a historical script snapshot. |
| bundle `20260925-165740-CONTROL` | CHECKED | Result/environment, frames/events and images inspected as a historical script snapshot. |
| bundle `20260925-165815-CONTROL` | CHECKED | Result/environment, frames/events and images inspected as a historical script snapshot. |
| bundle `20260925-173447-CONTROL` | CHECKED | Result/environment, frames/events and images inspected as a historical script snapshot. |
| bundle `20260925-180557-CONTROL` | CHECKED | Result/environment, frames/events and images inspected as a historical script snapshot. |

No conclusion below rests on unchecked work.  `CHECKED` means the current
test definition, command/criterion implementation, result serialization,
manifest status and reached production entry points were compared.  It does
not mean a hardware run was repeated or that a hardware observation upgrades
source provenance.

Date: 2026-10-01  
Manifest: `re-analysis/fidelity_manifest.json` at `415b9e0`
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
connection check.  The 40-check campaign catalog generally names only the
feature at the end of the path.  Each runnable command creates a fresh
production connection, so a prerequisite on `LINK` does not remove that
connection from the check's live path.  Consequently 38 runnable catalog
checks other than `LINK` hide the open connection path (`LINK` itself names
only its narrower handshake rows), and many additionally hide a relevant
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
