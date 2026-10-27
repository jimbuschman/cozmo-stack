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

- **N3 (2026-10-26 night): the 6d8d4006 handoff's seven proposals, checked against the binary.** These settle when the
  pilot merges (after the M15-022 FIX), together with M7-026:
  - **M13-017 PASS.** ctor 0x005C0980 (96 + extra), IsRunnableInternal 0x005C0B10 (+0x34A), Init 0x005C0B18 (lock,
    severe-needs 3 push, +0x355 wait), TransitionToDrivingForward 0x005C0BB8 (platform gate), Update 0x005C0DA8
    (stop on +0x355, redrive when idle, off-platform float stamp and 2), Stop 0x005C0D90, completion 0x005C0E9C
    (result 0 only: objective 4, then the Robot+0x440 emotion event) all match `ChargerBehaviors.cs`.
  - **M13-040 PASS.** SetOnCharger 0x00511990: family 4 / type 13 located find, else create + connected-ID copy or new
    ID, AddRobotRelativeObservation on every true input, then the +0x338 rising edge (platform, ChargerEvent 1) and the
    store. Matches `Sensors.cs` SetOnCharger and `BlockWorld.ObserveChargerFromContacts`.
  - **M7-023 PASS.** Robot teardown nulls +0x250 (0x00511150) before deleting the mood (0x005111B6) and nulls +0x440
    after; MoodManager dtor 0x0067AE14 gates on +0x158/+0x12C/ActionList, then +0x14C, +0x140, +0x134, +0x120 and the
    nine 0x20 entries from 0x100 down. `MoodLifetime.cs` keeps that order; the live-ActionList branch is unreachable
    from the only caller and reports MISSING.
  - **M11-060 PASS.** Producer 0x006B229C (warn if pending, store +0xA8/+0xA4, set +0xA0); consumer 0x006B50C8..D8
    (copy to +0x98/+0x9C, clear). Matches `Camera.cs` / `CameraQuality.cs`.
  - **M12-043 not settled.** Its own path still reports MISSING: the midpoint recipient (`CubeGameBehaviors.cs:714`,
    0x00617A8C..0x00617C32) and the container/cache joins (`BlockConfigurations.cs:234`). "Midpoint" is in the title.
    Propose it again when those are built.
  - **M8-027 not settled** (see the FIX row).
- **N2 is withdrawn.** I called M13-017 "the whole drive-off behaviour"; its current title is narrower and settles
  as is. Your own-path rule (AUTONOMY.md, operator scope correction) does what N2 asked. No PROPOSED-SPLIT.md needed.
  Next after the FIX: the next behaviour on BEHAVIOURS.md, handed off the same way.
| DRIVE-OFF-PILOT | MERGED | Test fix 5c25c9a7 checked (fixed action clock in the M15-022 lock tests; stronger assertions; test-only). Merged at a7f07e8b. **Settled: M7-026, M13-017, M13-040, M7-023, M11-060** (`research/20261026-driveoff-pilot-claude-check.md`). Not settled: M12-043, M8-027 (N3). |

- **N4 (2026-10-26 night): queued cleanup, do it alongside the next job, no separate handoff.** The full gate on the
  pilot merge failed once (then passed 6362/6362) on
  `ActionBehaviorDiagnosticTests.SharedToolJournalRetainsWorkerDiagnosticsWithConcurrentOrdinaryWritesAndSnapshots(true,true)`:
  TimeoutException at line 200 after 10 s. Four `Task.Run` producers must start while a pool thread is held
  synchronously in the sink, so under full-suite load thread-pool injection can exceed the 10 s `Complete` window.
  Use dedicated threads (or `TaskCreationOptions.LongRunning`) for the producers and the held writer so the test does
  not depend on pool growth; keep every assertion.
| PICKUP-PYRAMID-1 | MERGED | 0936210c. Spot-checked Pickup Verify 0x00553BE0..0x00554056 (the +0xC1 gate is always 1 for pickup, ctor 0x005536F8). **Settled: M15-044** (81 pairs equal the emulated builder; read/InitConfig rows 1-6). N4 test fix accepted. `research/20261027-pickup-pyramid-1-claude-check.md`. Behaviour not finished: native pickup children remain MISSING; next job should close that path. |

- **N5 (2026-10-27): fix the transport-clock coupling once, in the shared Rig, before the next behaviour's handoff.**
  The PICKUP-PYRAMID-1 merge gate failed once on `ManipulationTests.PlaceOnGroundNeedsACarriedObjectAndReleasesIt`
  (line 522: Timeout, expected Success; then passed on rerun). Same cause as the M15-022 FIX: since the pilot, the
  shared Rig builds `ManipulationSystem.CreateStandalone(..., ()=>Clock.NowMs/1000.0)`, so pumping the transport
  spends the action's own deadline under full-suite load. Patching tests one at a time (5c25c9a7, 283d6b6e) leaves
  the rest latent. Give the Rig a controlled action clock by default, separate from the transport clock, and let
  only the deadline tests opt into advancing it. Keep every assertion. Run the full gate twice.
