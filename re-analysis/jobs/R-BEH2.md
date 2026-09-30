# Job R-BEH2: the M7/M8 gap passes R-BEH left

**Agent:** the next free Sonnet worker in Claude Code (operator, 2026-09-29: saving OpenRouter credits), as the manager of this job. Use the `.claude/agents` subagents for the roles, and have the operator push each commit. **Type:** round 3, second pass. Follow [R.md](R.md).
**Needs:** R-BEH DONE. If it isn't, set `WAITING R-BEH` and end.

**First, the audit (2026-09-29):** an independent audit found that 19 of M7/M8's 22 settled records don't hold
(`re-analysis/research/20260929-audit-M7-M8.md`). They're back to IMPLEMENTATION_GAP, each with its defect in
`unresolved`. **Rebuild those first, before anything else.** The worst are:
- the behaviour vtable slots are mislabelled (M8-001: vptr+0x48 is InitInternal), which inverts the running flag;
- `IdleBehavior` is a second, non-production copy of the live idle, and it disables the streamer's own port
  (M7-005, M7-008..M7-010, M7-016, M7-017). The rebuild routes the idle through the streamer's gated
  `UpdateLiveAnimation` port (M5-030) and ProceduralLive on the idle stack, as the engine does. Retire
  IdleBehavior's own generator; don't repair it;
- ReactToImpact is always runnable (M7-003);
- PlayAnim's action parameters, and locked tracks failing the action (M8-005, M8-007);
- BehaviorManager's tick order and the phantom 0x16 behaviour (M8-012).

Several existing tests assert the contradicted behaviour (the report names them). Fix them from the source; don't
keep them. Check the report's rows with the verifier before you build on them, as with any extractor report.

**Subsystems:** M7-behaviour, M8-framework. **Then start from R-BEH's status** (`status/R-BEH.md`), not from a new triage.
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

**Pre-extraction:** Codex's answer is `re-analysis/research/20260929-R-BEH2-pre-extraction.md`. It has already been checked: `re-analysis/research/20260930-R-BEH2-pre-extraction-check.md` lists the corrections. Use the answer's rows **with those corrections applied** (a correction wins over the answer), and extract what either file leaves open or UNKNOWN. That includes the unverified M7-021 reader scan, and the three M7-018 rows nobody checked. Every float goes in as its bit pattern. Sonnet does not settle (CHECKLIST section 6): records end "built, awaiting strong verification".
