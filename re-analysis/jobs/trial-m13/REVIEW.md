# Claude verdicts (Claude writes; Codex reads)

| Job | Verdict | Main commit / finding |
| --- | --- | --- |
| CM13-01 | MERGED | 1f7567ea |
| CM13-02 | MERGED | 1f7567ea |
| CM13-03 | MERGED | this merge. Spot-checked 0x004FD1D0 and 0x005018E8: match. Full gate on 6139c25b 6275/6275, 0 skipped. The branch gate's KnockOver Abort did not reproduce (30/30 isolated, then the clean full gate); cause still unknown. |

## Open items for Codex to scope

- **R1 (from CM13-03):** `ActionBehavior.RunAction` (FaceBehaviors.cs:20..38) turns any faulted or cancelled action
  task into the supplied Abort and drops the exception. That hid the cause of the KnockOver Abort. Log the exception
  (type, message, stack) before returning Abort. A missing diagnostic that hides a real failure is a defect under
  AGENTS.md; this is the stack's own error handling, so no engine evidence is needed. Small job; do it next.
| R1 | FIX | 6d9ccafd adds `Log` calls in the `ContinueWith` continuation (FaceBehaviors.cs RunAction). That runs on whichever thread completes the task (ExecuteSynchronously; a cancellation or fault can complete it on a pool thread), so `SteppedBehavior.Log` now raises `Step` off the behaviour's thread. `_trace` is locked, but `Step` is invoked outside the lock, and two subscribers append to a plain `List` with no lock: `ManipTool.cs:32` (`log.Add` in `Say`) and `Reactions.cs:178` (`log.Add` in `Line`). Both are hardware tools whose logs go into the bundles. A race is a blocking finding. Fix it so the new diagnostic never raises `Step` concurrently with the behaviour's own lines (your choice of mechanism, e.g. a lock around the whole of Log, or locking the subscribers), add a test that drives a fault/cancel from another thread with a `Step` subscriber attached, and mark READY FOR CLAUDE again. Everything else in R1 is fine. |
| R1 | MERGED | a04bf06c (fix e592d1b2: locked DiagnosticJournal for the ManipTool/Reactions Step subscribers; bundles take a snapshot). Full gate on main 6285/6285, 0 skipped. Supersedes the FIX line above. |

## Operator decisions

- **D1 (operator, 2026-10-26), answers BLOCKED.md "extra configuration APIs": option 1.** Keep the whole public
  block-configuration adapter (`BlockConfigurationManager.ConfigurationSeen`, `FirstSeenSec`, the cached midpoint) as
  a documented stack extension, separate from engine fidelity. Record its host semantics as such (a
  COMPATIBILITY_POLICY-style record naming it a stack extension, decided by the operator 2026-10-26). It must not feed
  native engine state or any behaviour decision: `CubeGameBehaviors.cs:846` currently uses `FirstSeenSec`, so move that
  decision onto the engine's own path (BehaviorOnConfigSeen 0x005DB2D4..0x005DB396: per-type cache count growth,
  stored float stamp, dt < 4.99999 and |stamp - now| < 1e-5). Delete BLOCKED.md and continue the pilot.
| CM13-04 | MERGED | 481ade53 (pushed with this line). Spot-checked ComputeOriginPose 0x00517F6C. Full gate on main 6306/6306, 0 skipped. |

## Manager notes

- **N1 (2026-10-26 evening): hand off the drive-off pilot by your next session's end, not when everything is closed.**
  The pilot read is good, but it now reaches image quality, face candidates, Freeplay facades, mood init branches and
  audio, and no code has reached the branch yet. Under PLAN-BEHAVIOURS.md a finished behaviour is the goal, but a
  handoff that builds what is read and settles what holds beats another day of reading. So: integrate what the builder
  has, run the gate, mark READY FOR CLAUDE with RECORDS TO SETTLE plus exact leftovers. Leftovers become the pilot's
  second job, not a reason to hold the first. Audio (M6/M9) stays last per PROJECT_STATE: record it as a leftover,
  don't read further into it for drive-off.
| DRIVE-OFF-PILOT | FIX | Code and spot-checks PASS (research file follows with the merge): 0x004F0D2C and GetSimpleMood 0x0067C084 match; M7-026 will settle on merge; M8-027 will not (see below). **Not merged:** the full gate here failed twice on a7b80fee merged onto main, each time one R2FloorPlacementTests M15-022 reaction-lock test returning Timeout instead of Success (run 1: M15_022_InitTakesTheReactionLock... at 51 ms; run 2: M15_022_ABareActionTakesTheLock... at 74 ms). Both pass 20/20 alone, so it shows under full-suite load only. The pilot changed the shared Rig (ManipulationSystem.CreateStandalone with the test clock), which these tests use. Find the cause (likely a timeout measured on a clock that advances independently of the pumped rig, or native owners now ticking concurrently); fix it without weakening the assertions; run the full gate twice in a row; mark READY FOR CLAUDE. M8-027: the eight release functions each run an unlink helper that destroys the stored callable, so teardown's effect depends on each callable's destructor; enumerate those before proposing it again. |

- **N2 (operator go-ahead 2026-10-26: "however you think is best"): split the over-broad drive-off records.** After
  the pilot FIX, and before more building: M13-017 is "the whole drive-off behaviour", so it can never settle, and
  M13-040, M13-013 and M12-049 have the same problem. Propose a split in `PROPOSED-SPLIT.md`: each new record is one
  observable piece (for example drive-off's eligibility, its distance check, its completion objective/mood order),
  with title, the evidence addresses it inherits, status, and for each one either "complete now: evidence" or "left:
  one line". Use the source ledger you already have; no new reading. Keep it to the pieces drive-off actually uses.
  Claude reviews it, the operator signs off, and Claude re-approves the subsystems so the complete pieces can settle.
  Mark the row READY FOR CLAUDE when it's pushed.
