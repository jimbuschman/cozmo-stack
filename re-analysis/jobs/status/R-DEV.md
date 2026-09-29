# R-DEV status

CLAIMED opencode (cozmo-manager) 2026-09-29 12:12

## Scope

M1-transport, M2-protocol, M3-device, M4-control: every IMPLEMENTATION_GAP record of these,
plus the R.md policy-review records M4-004 and M1-014's priority clause.

## Triage (2026-09-29)

20 IMPLEMENTATION_GAP records + 2 policy-review corrections. Kind per `R.md`:

### M1-transport (1)
- **M1-029** compare/missing source: jsoncpp grammar (comments, trailing commas) and
  `asUInt` of a non-number. Pre-extraction Part 3 item 11 answers it.

### M2-protocol (1)
- **M2-002** compare/wire: consumers of status bits 0x2/0x4/0x8/0x20. Pre-extraction
  Part 3 item 12 answers it.

### M3-device (8)
- **M3-001** compare/missing: raw cases don't check payload length; encodings 0 and >9;
  case 2 ToGray arithmetic; the JPEG codec (libjpeg 9 inside libopencv_imgcodecs). Items 2, 3.
- **M3-010** missing source: the mu-law segment table values, f32 precision, NaN warning. Item 1.
- **M3-013** cross-layer wiring: audio-animation readiness is the M6 stand-in (always ready);
  depends on the M6 live audio wiring (B-M6b-3/4, in progress).
- **M3-018** compare/missing: RGB dispatch for encodings other than 9; IsColor of 0; codec. Items 2, 3.
- **M3-021** compare/build: the config load can fail so IsInitialized is not always true; the
  initial exposure is the shipped config's 16; pending params applied in VisionSystem::Update (M11). Item 4.
- **M3-032** cross-layer wiring: NV dispatch gated in Robot::Update (M1 state machine).
- **M3-033** cross-layer wiring: the 12 connection reads + CameraCalib + Lab + Needs (M11/M12/M15).
- **M3-034** cross-layer wiring: the reads' callbacks and sinks (M11/M12/M15/lab).

### M4-control (10)
- **M4-003** cross-layer wiring: lift height 32 while carrying -> PlaceObjectOnGroundAction (M12 CarryingComponent).
- **M4-005** cross-layer wiring: the shared action-id counter for TurnInPlace / direct SetHeadAngle (M7/M11).
- **M4-008** compare/fix: the cliff store runs before the origin check. Item 9.
- **M4-009** cross-layer wiring: slot bound; carried-object and [robot+0x280]+0xC broadcast exclusions (M11/M12).
- **M4-010** compare/fix: +0x490 default 0, no writer -> nothing broadcast; ObjectAvailable is gated too (new record). Item 8.
- **M4-012** cross-layer wiring: SetCarriedObjectAsUnattached(true) on lift calibration while carrying (M12).
- **M4-016** compare: the eye-shift block 0x005485F8..0x00548728, dead in this build. Item 5.
- **M4-017** compare/build: EnableMode(14) queues VisionMode LimitedExposure (M11). Item 6.
- **M4-018** compare/build: sleep-flag writers, +0x41 writers, the pop resends the lower pattern, the directory. Item 7.
- **M4-019** compare/build: HandleRobotStopped tag 0xD4, the 150-send path, the Welford removal, the lower_bound walk. Item 10.

### Policy-review corrections (R.md)
- **M4-004** (C): the engine has no calibration gate on direct motion. Move to IMPLEMENTATION_GAP, remove the gate.
- **M1-014** priority clause (C): the engine requests SCHED_RR at 75% (0x008334CC). Build the host equivalent.

## Progress log

- 2026-09-29 12:12 CLAIMED; pushed 12b4c84.
- 2026-09-29 **Verifier check of the pre-extraction**: three passes (M4 items 5..10, M3 items 1..4, M1/M2 items 11..12).
  Substantive rows hold. Citation defects only: E8's range (now 0x00632368..0x00632380), 11q (`blx` at 0x0052EA5E), 11m
  (a nonexistent `rodata_strings.txt` line). No extractor pass needed for those; the corrected addresses are used.
- 2026-09-29 **M4 batch done.** Corrections C10..C13 added to `M4-control.md` and approved. New record M4-025
  (the ObjectAvailable/Unavailable gate). M4-004 moved off COMPATIBILITY_POLICY and the gate removed.
  **Settled EXACT_SOURCE:** M4-003, M4-004, M4-005, M4-008, M4-010, M4-012, M4-016, M4-025.
  **Still IMPLEMENTATION_GAP with a precise residual:** M4-009 (ObjectID-vs-slot and the slot bound are M11),
  M4-017 (the vision-mode queue is M11), M4-018 (SetLocalizedTo and the runtime Delocalize callers are M11),
  M4-019 (the history-add failure signal, the charger-platform clearing and the runtime Delocalize callers are M11;
  H4's behaviour-end/action-cancel are M7/M8). Two gap passes ran (M4 gap1, gap2); no pass 3 needed yet.
  Verifier PASS after two fix rounds; full suite 1943/1943.
  Cross-layer call sites touched: Vision/VisionSystem.cs, Vision/FaceActions.cs, Behavior/ExplorerBehaviors.cs,
  Manipulation/ManipulationSystem.cs, Manipulation/Docking.cs, OffTreads.cs.
- Next: M3 (M3-001/010/013/018/021/032/033/034, including the libjpeg 9 port), then M1 (M1-029) and M2 (M2-002).