# Opus spot check of the builder's overnight run (2026-10-16)

- **Run:** read-only cozmo-verifier, on B-M9-1, B-M15-4, B-M8-5, B-M7-1 and B-M11-10.
- **Adopted:** by the manager.

## Verdicts

| Job | Verdict | Notes |
| --- | --- | --- |
| B-M9-1 | **UNDER-BUILT** | Only the velocity byte and the 0x81 gate were built. R1–R8, E1–E12, F/L and the M9-024 stop were skipped for "no live owner" or "parser unread" reasons, and none of the six named tests exist. Re-done in B-M9-3. |
| B-M15-4 | Effectively DONE with MISSING | ActivationMode, the setters, Enable and the InitExperiments order were checked against the binary. Nit: its manifest test field gained a leading separator. |
| B-M8-5 | Acceptable partial | Item 9 (StartActing deletes the action on its reject paths) wasn't built; it's an architecture gap, not a missing row. Now job B-M8-7. Item 4's finer read (0x0053E6BA / 0x0053E6E4) is correct. |
| B-M7-1 | Effectively DONE | ~MoodManager matches. The new host locks (LogCallbackDeferral gates in Mood.cs and RobotLifetime.cs) need a rule 12 check in the full verify. |
| B-M11-10 | Effectively DONE with MISSING | F1–F7 are built, with tests from 168 emulator runs of the shipped vsnprintf. 7 of the 20 callers are routed; the rest have no caller body. Queued note: DriveStraight and PanAndTilt build their names from a candidate constant. |

- **Rules:** across all five, the manifest is append-only and rule 11 holds.
- **A correction to a checked row:** M9-014 G2 writes V+0xC4 = **101.0f** (0x42CA0000; 0x009FFC88..0x009FFC9C), not 100.0f.

## The pattern, and the fix

1. **"BLOCKED" no longer meant blocked.** The builder marked a job BLOCKED whenever any MISSING remained anywhere.
2. **Real under-building happened when a live owner was missing.**

**The fix:**
- QUEUE.md rule 8 now defines DONE and BLOCKED;
- CODEX-BUILDER rule 8 no longer stops the lane;
- the new rule 13 says an absent owner is not a reason to skip.
