# Job R-BEH2: the M7/M8 gap passes R-BEH left

**Agent:** opencode, as cozmo-manager. **Type:** round 3, second pass. Follow [R.md](R.md).
**Needs:** R-BEH DONE. If it isn't, set `WAITING R-BEH` and end.

**Subsystems:** M7-behaviour, M8-framework. **Start from R-BEH's status** (`status/R-BEH.md`), not from a new triage.
It lists every record left open and what each needs:
- M7-012, M7-014, M7-015, M7-018, M7-019, M7-020, M7-021;
- M8-011, M8-013, M8-014;
- M8-004's and M8-008's remainders.

**What to do:**
1. For each record whose open part is **missing source**, run the gap passes R.md step 3 allows (at most 3 per
   subsystem), fold the checked rows in as a correction, approve, and build.
2. For each record whose open part is **cross-layer**, check whether that layer's piece exists now:
   - M10: R-ANIM settled several records;
   - M12/M14: R-VIS is building them, so don't touch M11..M14 code; name the dependency;
   - M15: R-M15 is running, so leave M15 code to it.

   Wire it only at the call site, if the piece exists.
3. **M8-008:** R-BEH established the 30.0 s timeout (the default slot 0x0052B0C2) and left the 0x03000018 failure
   result undelivered. Build that result seam, or establish from source that the M7 callers ignore it.

**Files to avoid:** R-VIS (a Sonnet worker) is editing `cozmo-stack/src/Cozmo.Robot/Behavior/FaceBehaviors.cs` and `ManipulationBehaviors.cs`, and `Manipulation/**`. Don't edit them. If a record needs a change there, name it in the status file for R-VIS, and leave that record open.
