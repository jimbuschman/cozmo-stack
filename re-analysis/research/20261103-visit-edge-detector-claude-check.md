# VISIT-EDGE-DETECTOR-1: Claude check (manager, 2026-11-03)

**Verdict: PASS. M11-101 added and settled (EXACT_SOURCE), M11-102 added (IMPLEMENTATION_GAP), notes on M11-017 and M11-048.
No robot has run this code.**

Extraction `research/20261103-visit-edge-detector-extraction.md` (31 rows: 11 MATCH, 17 DIFFERS, 3 NOT IN STACK, 0 UNKNOWN), by a
Claude extractor. I re-read the kernel bits at 0xC8E0AC and the strict `|v| > 50` upward scan at 0x006AD1D0..0x006AD20A. Built by a
Claude implementer and checked by a separate verifier, who disassembled the points the corpus cannot separate.

## What changed

- `Vision/NativeOverheadEdges.cs`: the grey path of the engine's Detect 0x006ABE34. It adds:
  - the status and visible gates, and the LUT homography (first key at or above the head angle);
  - the ROI quad with ClampQuad, a truncating bounding rect and a half-open clip;
  - the 7x5 filter2D with per-tap f32 multiply-add, round to nearest even and int16 saturation, reading the border from the parent
    image as reflect-101;
  - the fillConvexPoly mask, the bottom-up strict scan, image-to-ground with a reciprocal, a clear report when a hit fails to
    project, and chains of 3 or more points;
  - the frame quad from GetVisibleGroundQuad.
- `OverheadEdges.cs` now calls it. The old double filter and the fitted homography are gone.
- `VisionSystem.cs` feeds Detect the same frame's SetNextImage capture (status, LUT homography, visible byte), as VisionPoseData::Set
  copies them. The processed bit 0x10 is set on every zero return.

## Proof

`re-analysis/tools/emu/emu_overhead_edges.py` runs the shipped Detect under Unicorn with the engine's VisionPoseData, Image and
result-list layouts. Library hashes are enforced. The stand-ins (Tic/Toc, cosf/sinf/sqrtf for the lift pose) cannot change an
output. **286 of 286 fixture cases are bit-exact**, 10 of them from real robot JPEGs, and identical under heap poison 0x00/0xFF
and NEON on/off. The verifier's own sweep of 612 new cases also matched, 612 of 612; it covered 17 head pitches, two calibrations,
three camera positions, roll/yaw and three statuses, including 80 empty rects.

The verifier read these in the binary because the corpus can't separate them:
- the inverse at 0x004F7FB0: f32 determinant, one `vdiv.f32 1/det`, then multiplies, with no per-element divide;
- AddPoint's f32 distance;
- the saturating conversions.

## Corrections made at check

- **NaN handling:** image-to-ground now treats a NaN z as success (0x006AE0E4 `bls`), and a NaN chain distance starts a new chain
  (0x006AE1EE). Both are unreachable in production: the LUT H has a determinant of about -3e6. The inventory's row 24 was wrong on
  this point.
- **Citations:** the processed-bit citation is 0x006B540A..0x006B5416, not 0x006B53FC.
- **Comments:** the inverse comment now cites the binary, not the corpus. Two coverage comments overstated what the corpus reaches
  (the empty rect, a singular H).

## Expectations changed

- The kernel test checks exact bit patterns.
- An abandoned lift-gate frame is now null, because the engine appends nothing.
- Four rig tests now report LIFT_IN_POS and install the LUT, because the engine's status gate `(status & 0x102) == 0x100`
  (0x006AC090) and row 6 require them.

No tolerance was widened.

## Records

- **M11-101, new, EXACT_SOURCE:** the detector, given the lift-in-the-way decision. The return-1 path and the whole-result drop are
  unreachable: lift and camera share the root pd+4. The colour path is inert.
- **M11-102, new, IMPLEMENTATION_GAP:** the lift gate (the Project3dPoint polynomial is unread). It is outside the oracle, which
  runs with a null calibration.
- **M11-017:** evidence corrections; it keeps the map side.
- **M11-048:** a library-name correction.

## Queued

- Add fixture cases with an empty clipped rect and a truly singular H.
- Emit the EmptyRect and BadProjectedZ logs.
- The rect clip should wrap in int32.
- Two tests marked `reach: production` install the LUT directly.
- One vacuous `Assert.All`.
- Pull shared engine-object builders (Pose3d, Image, VisionPoseData) out of the emu oracles into one module.

## Progress

20 records settled across 44 start-path jobs, 2 of 178 finished.
