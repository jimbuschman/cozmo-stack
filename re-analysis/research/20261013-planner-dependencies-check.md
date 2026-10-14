# Opus check of R-PLAN-DEPS (2026-10-13)

- **Checked:** `research/20261013-planner-dependencies.md` (Codex), against libcozmoEngine.so. Read-only
  cozmo-extractor; helpers and dumps are in `.scratch/pdeps/`.
- **Adopted:** by the manager on 2026-10-13. This file is a checked source for job B-M13-1. Use the Codex report together
  with the corrections below.
- **Result:** the report holds, apart from H10. The linked Codex evidence files were never committed.

## Corrections and additions

- **H10 (contradicted):** on normal completion, ApplyPathSegment returns the **accumulated sub-threshold penalty**
  (s17), not 0. The `vldr s17 = 0.0` at 0x008509F4 is skipped by `b 0x8509F8` at 0x008509F2. So PathIsSafe sums the
  per-segment remainders, and several segments that are each under 1e-5 can fail together.
- **H5:** if count ≤ startIndex, PlanIsSafe returns 1 at once (0x00850C60).
- **H6:** a retained action is pushed with its **old** stored penalty (0x00850CBE).
- **D7:** UpdateObservedMarkers' order is +0x90 timestamp, then ClearOccluders, then AddLiftOccluder, then clear +0x54
  (only when the list is non-empty), then CreateObjectsFromMarkers.
- **G2:** ComputePath's goal loop starts its best at 0.0 (0x004FEAF0), with a `found` flag; it behaves as reported.
- **The F4 == 0 goal loop is unreachable in production:** +0xF4 is written only by the ctor (F4 = 1, 0x004FCCE2). Record
  it as unreachable; don't build it.
- **ApplyMotionProfile's head** has a reverse-speed warning (0x00508BB4..0x00508C0C, when speed < 0 and |profile+0x24| ≤
  1e-5). Its key string is unresolved.
- **DidObjectsChange (+0x54):**
  - its writers are CreateFixedCustomObject (0x0061E2D8), AddAndUpdateObjects (0x006211E6), AddMarkerlessObject
    (0x00622596), ClearLocatedObjectHelper (0x00625E82), and the clear in UpdateObservedMarkers (0x00624FAC);
  - its only reader is HandlePossibleOriginChanges (0x006498DA);
  - the conditions upstream of AddAndUpdateObjects' set (0x00620AD4..0x00621100) are M11's.

## The undefined read (the M1-034 standing rule)

**ApplyPathSegment's turn weight** = den / *(float*)(seg+0x14) (0x008506E4):
- the float's low byte is useShortestDir;
- bytes +0x15..+0x17 are never written (AppendPointTurn 0x0085D424; the Path ctor 0x0085C714), and are copied verbatim
  through AddSegmentsToPath and ApplyMotionProfile;
- so the weight depends on heap contents. It is live whenever a turn sample falls inside a polygon, through
  HandlePlanComplete → slot 0x18 → PathIsSafe.

**The manager rules:** this is a read of undefined memory under the **M1-034 standing rule** (operator, 2026-10-13). The
stack skips that turn segment's penalty contribution (the smallest unit), keeps the running total, logs it visibly
(naming M1-034 and M13-034), and carries on.

## Confirmed as in the report

- **H1–H4:** FindClosestPlanSegmentToPose (discretisation, the exact-match 0.0, strict improvement, per-sample checks;
  debug output off in production).
- **H5–H9:** PlanIsSafe and PathIsSafe (the empty plan, the prefix with check 0, the +0.5 rule, the 40.0 tolerance, the
  printf, the 1e-5 threshold, the Info log, the ignored AppendSegment), and ApplyPathSegment's sampling arithmetic and
  its unknown-kind 1000.0.
- **H11:** xythetaPlan::Append.
- **B1–B7:** AppendToPath (validation first, then the skip and advance), AddSegmentsToPath's merges, the Path ctor, and
  the Append* "Exceeded path size" when +4 > −1 (the capacity at +5 is unread).
- **G1–G5:** the goal loop, which is unreachable.
- **M1–M5:** the ApplyMotionProfile sub-ranges, including the split geometry.
- **N1–N4:** the MinimalAngle compares and logs.

## Unread (other owners)

- PathSegment helpers: GetLength, GetEndPose, GetStartPoint, OffsetStart, DefineLine, SetSpeedProfile
  (0x0085BC00..0x0085C034). Check M13-011.
- ApplyAction (0x00850478..0x0085060C) and FastPolygon::Contains (0x00841BA0..0x00841C6A) (M13-003?).
- MotionPrimitive::Create and RelativePathTurn::ExtendPath.
