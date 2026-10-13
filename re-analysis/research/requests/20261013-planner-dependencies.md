# Research request R-PLAN-DEPS (M13-034)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-planner-internals-check.md` (its "Unread" section), and the M13-034 and M13-033 records
  in `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-planner-dependencies.md`.
- **Format:** one coverage table per item, first. Each behaviour-changing step gets an address, or UNKNOWN. Use a
  disassembler that skips undecodable data.

## Items

1. **BlockWorld::DidObjectsChange** (0x00623020), and every writer of the flag it reads: when it is set, when it is
   cleared, and by whom.
2. **Planning helpers:**
   - FindClosestPlanSegmentToPose (0x00856310);
   - PlanIsSafe (from 0x004FF410) and PathIsSafe (0x00850E34), with ApplyPathSegment (0x00850610);
   - xythetaPlan::Append (0x00851870).
3. **Building the path:**
   - AppendToPath (0x00855D90);
   - Path::AppendLine, AppendPointTurn and AppendSegment (0x0085CE88, 0x0085D3C8, 0x0085D46C): capacity, failure returns,
     and logs.
4. **ComputePath's goal loop** (0x004FE7F6..0x004FE9E2).
5. **ApplyMotionProfile's unread sub-ranges** (0x00508C14..0x00508D90, 0x00508F48..0x0050906E and 0x00509070..0x005090DE).
6. **MinimalAngle's compare operands and logs** (0x00503E7A..0x00503EA2, 0x00504040..0x00504174).
