# HANDOFF ROLL-MARKER-OBSERVED-1 (Claude worker, 2026-11-13)

Job 21. Census and implementation reports are worker-local: `.scratch/ROLL-MARKER-OBSERVED-1-census.md`,
`.scratch/ROLL-MARKER-OBSERVED-1-impl.md`.

## Result: a rig artifact; nothing to build in the engine path

The whole-stack FreeplayTests roll stops at IDockAction::Init 0x0300001D because the Freeplay rig (ManipRig.OneImage)
feeds the offline `VisionSystem.ProcessImage` a **parentless** camera pose, while the KnownMarker poses are rooted at
the native origin. SetMarkerAsObserved's GetWithRespectTo(marker, camera) gate (0x00876E3E..0x00876E64) then fails, and
the stack withholds the marker timestamps (MarkerObservationTime.cs:39, 74-77).

In the engine, and on the stack's queued frame path, the camera is parented to the historical robot state in the
world origin (GetHistoricalCameraPose 0x00513ABE / 0x00513B16; assigned to each observed-marker copy
0x00654EAC..0x00654F58; origin check 0x00654E56), so the roots match and the gate passes. Production-form ingress tests
(RollFirmwareScene / PopFirmwareScene: RollBlockOnSideNativeTests:69, RollBlockOnSideLowScoreNativeTests:83,
HikingRollCubeNativeTests:114, SparksRollNativeTests:47/187, PopAWheelieNativeTests:117) write the timestamps and reach
DockWithObject.

**Verifier correction (FAIL, fixed):** my first write-up said production frames never carry `HasLegacyFrameInput`.
They do: CopyHistoricalCameraPose starts from the Mat3 `DefaultHeadCamRotation` (NativeDelocalization.cs:33), and the
flag survives. The withhold branch is armed on every frame and does not fire only because the roots match. The test
comment and M11-013 now say so.

**New divergence (proposal MARKER-WITHHOLD-PER-MARKER-1):** when GetWithRespectTo fails, the engine skips only that
KnownMarker (0x00876E64 bne 0x00876F76); the stack withholds the whole selection. Unreachable in production as far as
traced.

## M12-036: first evidence through production-form ingress

`RollBlockOnSideNativeTests.M12_036_ALocatedChargerInsideTheObstacleBandIsAcceptedByTheNativeRollDrive` (reach: unit;
production-form ingress with named substitutions: manager admission, AddLocatedObject seeding, simulated firmware).
A located charger at (-300, 0, 0), inside GetObstacles' Z band by hand derivation (size (96,80,31) from 0x004EA672,
extent [-15.5, 15.5], minZ about 0, maxZ >= 67.7; G2/G3/G9 0x0062C220..0x0062C27E), and the roll still reaches
DockWithObject.
- Negative control: with the charger forced to the old stand-in, NativeObstacles throws and no dock is sent.
- Limit (verifier): it shows that an included charger's quad is built and accepted; inclusion itself rests on the
  derivation (a wrong exclusion would also pass). Observing the list's contents needs a production diagnostic: the
  manager's decision.
- Measured, not traced: the robot's re-rooted z during the drive is -3.8e-06, not 0; recorded in M12-036.

## Records

`unresolved`/`test` only on M11-013 (rig artifact with citations; the correction; the per-marker divergence; an OPEN
untraced item: RollFirmwareScene's unseen-marker timestamps drop from 133 to 0 after the first production frame),
M8-011 (next-stop note), M12-036 (the new test, F1 update). No status changed. No record settled.

## Verifier

FAIL on the flag premise (fixed as above). Queued items fixed: the M12-036 test's reach label is now `unit`, and its
note says inclusion is derived, not observed.
