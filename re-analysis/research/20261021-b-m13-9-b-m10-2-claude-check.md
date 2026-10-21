# Claude/Opus check of B-M13-9 and B-M10-2 (2026-10-21, HEAD 2e0a1114)

Independent cozmo-verifier pass (read-only, `.so` instructions read with capstone); manager spot-checked the
B-M13-9 objection (0x00622D24..0x00622D30, 0x004EA5E8..0x004EA5F2, CozmoContext ctor `004ea734.c` line 56).
Focused tests 63/63; `fidelity.py --check` exit 0.

## B-M13-9 (0138af17, 3f54bccc): FAIL, one CONTRADICTED row, no behaviour defect in 1–4

**Objection.** `BlockWorld.cs:256-258, 2313-2315` adds `AddedToWorldTimestamp`, stamped from `State.Latest.Timestamp`
on every AddLocatedObject, citing "insertion stamp 0x00622D24..D30". The engine stores no timestamp there:
`ldr r0,[r8]` (BlockWorld+0 = Robot*), `ldr r0,[r0]` (Robot+0 = CozmoContext*), `ldr r0,[r0,#0x24]` (CozmoContext+0x24 =
VizManager*, ctor 0x004EA734), `str r0,[r1,#0x54]`. obj+0x54 is the object's VizManager pointer, read by
Charger::EraseVisualization (0x004EA5E8..0x004EA5F2) for `VizManager::EraseVizObject` (0x004A4258). Viz is a debug
channel with no effect on 1–4. The false claim is also in M13-040's frozen evidence row 3 and `unresolved`, in
`research/20261021-b-m13-9-check.md:31-35`, and asserted by `M13ContactChargerTests.cs:58-59, 68, 85`; the Missing at
`BlockWorld.cs:2315` reports a step that does not exist.

**Checked and supported:** the contact path end to end (synced RobotState → SetOnCharger on every true input before the
edge gate; ChargerDockPose; current-origin family4/type13 lookup, factory, identity copy or SetID; Known observation,
reparent/flatten, existing/new branch, -1 distance, confirmer; edge order; new-state light rule; shared world; platform
tick unchanged). The run-3 scenario now keeps threshold 50 at 25.6 mm of travel.

**Queued (visibility):** the `reach: production` labels in `M13ContactChargerTests.cs` (:30, :74, :159) substitute the
pose (origin 999 blocks admission; footprint exit via `robot.SetPose`); relabel as unit. M13-017's `unresolved` still
says the observation is unbuilt. DAS on/off-charger events missing (log only). Double `<summary>` at `Sensors.cs:729-734`.

## B-M10-2 (e332207b): PASS

All six classifier literals match the `.so` word for word (0x3FF5BE0B, 0xBFB2B8C2, 0x3FFAEB8260000000,
0x3FF4CDE840000000, 0x3FD0C15240000000, 0x3F490FDB at 0x00511E66, 0x00511EE6, 0x005122A0/A8/B0/BC), with the engine's
f64/f32 compare forms. Tests not circular.

## Correction to the planned robot check

"Threshold 50 holds until the footprint leaves the charger" is not what the engine always does. The one-shot distance
rule (accumulator +0x528 from -1, ctor 0x0050FBF0; 50.0f at 0x00512E8A; 400 at 0x00512E9C, once, +0x52C) sends 400
without a platform gate. Sourced expectation: threshold 50 until the **earlier** of footprint exit (platform event then
400, 0x00511D44..0x00511DCA) or cumulative drive-centre travel over 50 mm. Docked at session start, the distance rule
fires about 44 mm into the drive-off (estimate from the port's geometry: footprint clears after ~86 mm; not checked in
the `.so`). The script must log both triggers and the robot x at each SetCliffDetectThreshold, with vision attached.

## Before M13-017 can settle

M13-040's evidence row corrected and re-approved (VizManager, no effect); M13-017's evidence (+0x34A is the platform
flag, contacts +0x338) and `unresolved` corrected; its open parts built or recorded (PushDrivingAnimations and
WaitForOnTreads, Markerless/Ramp/HumanHead footprints, the 0x004E65C6 catch-all and <3 hull points, the double rotation
cast); shared MISSINGs (SetObjectPoseChanged 0x00624892, M11-074 map continuations, M12-001/045 cached-pose dtor); a
drive-off test through admitted, frame-matched RobotStates.
