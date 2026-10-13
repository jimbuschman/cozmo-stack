# Research request R-PLANNERS (M13-033)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-m12-032-path-status.md` (rows P6, P9, P10, P11, P27, P29, and its "Unread" section), and
  the M13-033 and M13-018 records in `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-planner-internals.md`.
- **Format:** one coverage table per item, first. Each behaviour-changing step gets an address, or UNKNOWN.
- **Tooling:** use a disassembler that skips undecodable data (capstone with skipdata).

## Items

1. **SelectPlannerHelper** (0x0064A5BA..0x0064AA18): how the planner is chosen (lattice, FaceAndApproach, MinimalAngle,
   or another), what is written to [+0x20]/[+0x24] and to the fallback [+0x28]/[+0x2C], every condition, and every log.
2. **The LatticePlanner entry points the PathComponent calls:**
   - ComputePath (0x004FE688);
   - ComputeNewPathIfNeeded (0x004FEB00): its return codes 0/1/other, and when each happens;
   - StopPlanning (0x004FD1A2);
   - CheckIsPathSafe (0x004FF936);
   - CheckPlanningStatus, which returns [[this+0x130]+0xF8]: every writer of that field (the worker thread included),
     and the values it takes.

   Say how the worker thread is started, signalled and joined, and what the main thread sees each tick.
3. **FaceAndApproachPlanner and MinimalAnglePlanner:** ComputePath(start, goal) through vtbl+0x2C, called from
   0x00508A80..0x00508A8E. Give their return codes, and the path they build: segment types, speeds, and the order of
   arithmetic.
4. **IPathPlanner:** ComputeClosestGoalPose (0x005087AC), GetCompletePath, and ApplyMotionProfile (0x00508AE4).
5. **vtbl+0x20:** the slot HandlePlanComplete tests (0x00649F86..0x00649FBE). The lattice planner returns 1 (0x00500AC2)
   and the base returns 0 (0x004F1A08). Name it, and say what it means.
6. **The comparison:** what the stack's planner does today (cozmo-stack/src/Cozmo.Robot/Manipulation/LatticePlanner.cs
   and its callers), and where it differs.
