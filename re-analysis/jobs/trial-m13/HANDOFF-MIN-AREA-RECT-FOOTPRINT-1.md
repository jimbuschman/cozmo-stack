# HANDOFF MIN-AREA-RECT-FOOTPRINT-1 (Claude worker, 2026-11-14)

Job 24. Census and implementation reports are worker-local: `.scratch/MIN-AREA-RECT-FOOTPRINT-1-census.md`,
`.scratch/MIN-AREA-RECT-FOOTPRINT-1-impl.md`. Verifier: PASS.

## The premise was partly out of date

The stack's cube footprint already follows Block::GetBoundingQuadXY (0x004E62A2) for every pose, tilted or not:
re-rooted pose, padded size, rotated canonical corners, cv::minAreaRect (0x004E64FE), RotatedRect::points,
SortCornersClockwise, translation. MinAreaRectF matches imgproc 0x0009C760..0x0009CEAC, Sklansky and RotatedRect::points
instruction for instruction, except one NaN rule (below). So a stand-in never decided a cube pair.

## The change

- **The live on-top search** (BlockConfigurations.FindObjectOnTopOrUnderneathInRootFrame; via BlockWorld for
  MarkObjectDirty, MarkObjectUnknown, ClearLocatedObjectHelper, UpdatePoseOfStackedObjects, and Docking.cs:622 /
  LiftGeometry.cs:146): the reference and candidate quads now come from each object's own slot +0x50 (0x0062604C..
  0x0062605C, 0x0062BA7A..0x0062BA86, padding 0) and their sizes from slot +0x2C (0x00626070; 0x0062BAA4..0x0062BAB6,
  after Intersects), shared with Neighbor through `NativeConfigurationGeometry.DimInParentFrameZ`. Before, a charger or
  markerless object got the PlanarStandIn (and markerless objects a 44 mm size). Cube results are unchanged.
- **PlanarStandIn deleted** everywhere: the dead host BuildTallestStack branch and the DriveActions stand-in (no
  production constructor). `StaticFindLocatedIntersectingObjectsIsEmpty` now reads slot +0x50 quads; it is off the live
  path (the native bring-to-beacon always supplies its own floor test). Two unreachable catches removed.
- **NaN area rule:** MinAreaRectF keeps an area only when `area <= minarea`, matching imgproc's `bhi` at 0x0009CCC8
  (an unordered compare skips the store).

## Tests

`MinAreaRectFootprintTests` (reach: unit): T1 tilted cube on a cube, T2 charger as reference, T3 charger as candidate,
T4 markerless cliff candidate (with heights separating 50 from 44), and the NaN rule. Expected values from the census's
independent port of the cited formulas (re-run by the verifier). The C9 test (M13FindOnTopSameRootTests) now expects the
engine result; two M13RVisBuildTests are renamed to match what they assert. NativeBouncerFactoryTests joins the
"SteppedBehavior missing-report statics" collection (verifier Q1: it read the static MissingReported in parallel).

Affected classes: 476/476 and 635/635 (implementer), 404/404 (verifier).

## Records

`unresolved`/`test` only on M13-024 (stale yaw-only/stand-in text corrected; NaN rule), M13-007 (per-class live search;
stand-in gone), M13-023 (stand-in deleted; libm unchanged), M11-105 (new readers). No status changed. No record settled.

## Queued (verifier)

- BlockConfigurations.cs:544 still carries an unreachable throw text naming an "unported minimum-area-rectangle branch";
  the BlockWorld.cs ~3035 doc names the old method.
- re-analysis/evidence/m13-minarearect/port.py has the same NaN-keep error (recorded in M13-024, not edited).
