# Claude worker: CLAMP-FLAT-NUMERICS-1, INITPOSE-OBSERVED-FLAG-1, DOCK-MARKER-WRT-SKIP-1: Claude check (manager, 2026-11-11)

Merged from `claude-worker` at 95c2c52f. Each job had its own extractor census, build and independent verifier on the
worker's machine:
- CLAMP-FLAT-NUMERICS-1: the full gate passed 8382/8382 on 5a10b116;
- DOCK-MARKER-WRT-SKIP-1: the full gate passed 8386/8386 on e2302601;
- INITPOSE-OBSERVED-FLAG-1: a reading job with no code change.

## CLAMP-FLAT-NUMERICS-1: PASS

The stack's flat clamp was invented double-precision arithmetic. It is now ObservableObject::ClampPoseToFlat
(0x00877330..0x008774EC) in f32, with:
- the root-relative gate and axis choice;
- the Radians IsNear boundary (0x0084CD12/0x0084CC90/0x0084CC0A);
- the 1e-5 early-outs;
- the rotation-only write, which matches the in-place write at 0x008774D0..0x008774E8 checked on 2026-11-11.

The code is shared with docking (M12-019's 0x3F32B8C2). The creation tolerance is the f32 product 5.0f × 0x3C8EFA35 =
0x3DB2B8C2 (0x00625566). An ARM oracle (`re-analysis/tools/emu/emu_clamp_flat.py`) runs the shipped function, and
12 oracle rows plus 13 verifier cases match the stack bit for bit.

One existing expectation was corrected from measurement on the shipped function: a 0x3F32B8C1 tilt does not snap
against 0x3F32B8C2, because of IsNear. M11-006 and M12-019 keep their status; this job had no settlement target.

## INITPOSE-OBSERVED-FLAG-1: PASS (no effect)

obj+0x50 is written:
- by InitPose and SetPose (0x004EF578/0x004EF58A);
- with 0 by every constructor.

It is never read: two independent scans (a linear sweep and a per-symbol decode covering all the load forms) found no
reader. It is recorded on M11-062, with a cross-reference on M11-013, and nothing was built.

## DOCK-MARKER-WRT-SKIP-1: PASS

IDockAction::Init's marker choice (0x00551960..0x00551BE4) now has one stack implementation:
- **the distance:** each marker is placed relative to the robot (0x00551B06..0x00551B12), then f32 d² = (x² + y²) + z²;
- **the comparison:** strict `<` against best², where NaN loses;
- **the winner:** best = sqrtf(d²);
- **a marker that can't be placed:** skipped, with the info log;
- **no winner:** an M1-034 skip, because the engine reads address 0 there.

Before this job, two or more observed markers threw MISSING M11-066. Now the nearer marker is chosen. The
marker-name input for the skip log is unbound (M11-013), so it reports a visible MISSING instead of inventing a name.

**No record settled.**
