# HANDOFF LOCALIZE-PRODUCTION-PATH-TEST-1 (Claude worker, 2026-11-11)

This job was built in the same commit as LIVE-OBJECT-POSE-NATIVE-1. That job removed the old fields-only live test,
and this test replaces it. It added no production code beyond LIVE-OBJECT-POSE-NATIVE-1's C1.

## The test

`LocalizeToObjectSameOriginTests.M11_044_ALiveSightingOfAConnectedCubeLocalizesThroughTheNativePath` (reach:
production). It drives real rendered frames through the queued worker:
- ComputeAndInsertKeyedState;
- CreateObjectsFromMarkers;
- Insert;
- LocalizeRobot;
- the robot tick's send.

It uses a connected cube in the current origin.

**What it asserts, each from the engine:**
- **Sighting 1:** it creates the PoseConfirmation and returns false (0x0050689A..0x00506982), and sends no 0x45.
- **Sighting 2:** Count > 1 (0x00506AC2..0x00506ACC), so the object is inserted and localization runs.
  - **S10 (0x0051513C):** the pose frame ID goes up by one, and exactly one vision-only state is added. Its stored
    frame is the new frame.
  - **S15/S16 (0x00515A88; 0x00513CBE..0x00513CCC; SendAbsLocalizationUpdate 0x00514710):** exactly one 0x45, with
    the S10 frame ID, the vision-only state's time and the current origin.
  - **S14 (0x00512458/0x0051245E):** LocalizedToObjectId is the cube's ObjectID, and Robot2C4 is 1.
- **The stored pose:** unflagged, parented, rooted at the current origin, and PoseWorld is this world (census L7..L11,
  section 4).
- **Diagnostics:** no MISSING names LIVE-OBJECT-POSE-NATIVE-1.

**Substitutions:**
- The offline flag is cleared after sighting 1. The verifier measured an identical result with it left set.
- The fixture's sim firmware report and outer calibration. Sim gates only the rolling-shutter shift, which this join
  doesn't use.

## Verifier

This test was covered by the LIVE-OBJECT-POSE-NATIVE-1 verification (Q2, Q3 resolved; see that handoff). The verifier
found it non-circular.

## Records

- **M11-044:** no settlement. This was the "coverage gap, queued" in its `unresolved`; the test now exists. Still open:
  the cross-origin branch, LocalizeToMat and OnRobotRelocalized.
