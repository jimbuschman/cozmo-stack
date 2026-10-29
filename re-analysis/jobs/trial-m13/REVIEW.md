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
  (line 522: Timeout, expected Success; the rerun was stopped for low memory). Same cause as the M15-022 FIX: since the pilot, the
  shared Rig builds `ManipulationSystem.CreateStandalone(..., ()=>Clock.NowMs/1000.0)`, so pumping the transport
  spends the action's own deadline under full-suite load. Patching tests one at a time (5c25c9a7, 283d6b6e) leaves
  the rest latent. Give the Rig a controlled action clock by default, separate from the transport clock, and let
  only the deadline tests opt into advancing it. Keep every assertion. Run the full gate twice.
| PICKUP-STACK-1 | MERGED | baa24a33. Spot-checked ComputeHeadAngleToSeePose 0x00518344..0x00518600 (NaN branches, write before MaxIterations, exhaustion) and GetAbsoluteHeadAngleToLookAtPose 0x0054B428..0x0054B55A. **Settled: M13-026** (EQUIVALENT_IMPLEMENTATION, libm boundary only). N5 accepted. `research/20261027-pickup-stack-1-claude-check.md`. |
| PUTDOWN-BLOCK-1 | MERGED | 0ffe53aa. Spot-checked 0x005C7F98..0x005C82CC (backup draw -45..-75, speeds, head -20 deg/2 deg, drive -30, image wait 2/mode1, triggers 0x19A/0x199, face PI). Nothing settled: the proposed new lifecycle record waits for operator approval; M15-012/M13-027/M14-008 stay open. `research/20261027-putdown-block-1-claude-check.md`. |
- **PUTDOWN-BLOCK-1 follow-up (2026-10-27):** the operator approved the proposed lifecycle record. Added and settled as
  **M15-075** (IsRunnableInternal, Init, LookDownAtBlock and the release callback); M15-012, M13-027, M12-006 and
  M14-008 stay open and are named in its `unresolved`.

- **N6 (2026-10-27): close the shared children before starting another behaviour.** Each behaviour so far ends with the
  same open records underneath it. Next job after the current one: **M13-027** (TurnTowardsFaceWrapperAction: the
  generic flags and speech) and **M15-012** (the manual image-wait path and the CantHandleTallStack trigger), whole
  records, so both can settle. Then the native pickup children (Pickup/IDock/DriveTo). Propose a new record only where
  no existing record covers the behaviour's own step.
| PICKUP-PYRAMID-2 | MERGED | d71e12c7. Spot-checked AbortDocking 0x0063BE10..0x0063BE5E and the IDock cleanup order 0x00557448..0x005575B4. **Settled: M12-018.** `research/20261027-pickup-pyramid-2-claude-check.md`. N6 still applies: M13-027 and M15-012 next. |
| PICKUP-PYRAMID-3 | MERGED | 8e767209. Spot-checked StopActing 0x005BD3D6..0x005BD3FE (pre-clear only without keep, Cancel always, post compare-clear) and the wait deadline store 0x0054CA1C..0x0054CA36. Nothing proposed or settled; M12-010, M8-001, M8-017 stay open. N6 next. |
| SHARED-CHILDREN-1 | MERGED | eab6e59b. Checked the wrapper ctor 0x0054C7DC..0x0054C8CE, the completion proxy 0x0054F27A..0x0054F3EA, the put-down caller flags 0x005C8264..0x005C82CC, TransitionToDisapointment 0x005ED0F0..0x005ED184 and Update 0x005BDA56. **Settled: M13-027, M15-012.** `research/20261028-shared-children-1-claude-check.md`. N6 continues with the native pickup children (Pickup/IDock/DriveTo). |
| PICKUP-CHILDREN-1 | MERGED | 997e2908 + 5fbe172b. Checked Verify's timed checks 0x00553C70..0x00553D2A with the IsMoving thunks 0x004E3860/0x004E3874 and ctor 0x004E0C84, the native M12-029 setter guard and helper functor, and the PanAndTilt setters 0x005498A0..0x00549B2E. **Settled: M12-029, M13-020.** M12-007's source claim is confirmed (the motion timestamp, not last-observed); its settlement waits for the inventory correction and re-approval. `research/20261028-pickup-children-1-claude-check.md`. |
| KNOCKOVER-CUBES-1 | MERGED | 741cf8bf/ae48b85a. Checked the Flip ctor 0x0055ECA8..0x0055ED12 and lift trigger 0x0055F124..0x0055F16A, and the spark flag chain 0x005A3EB2/0x005A4226/0x005BCCB8 with its literals. **Settled: M13-002.** Confirmed that +0xD8 means a hard spark; M13 inventory Q8 and M13-014's evidence say soft and need an operator-approved correction. `research/20261028-knockover-cubes-1-claude-check.md`. |
| SPARKS-KNOCKOVER-1 | MERGED | 7e3b096a. Checked ActivitySparked OnSelected 0x005B1518..0x005B16D6 (hard condition, driving/idle 0x213/backpack pattern/Started u32, lock order), Update 0x005B186C..0x005B18CC and the end deadline 0x005B18EE..0x005B190E. Nothing proposed or settled; M15-059, M15-006, M15-071 stay open. Startup RobotValidated failure (builder run) kept as UNKNOWN. `research/20261028-sparks-knockover-1-claude-check.md`. |
| SPARKS-STACK-1 | MERGED | 66441408/b5c503c8. Checked the StackBlocks validity gate 0x005C9844..0x005C9A24: both-valid skips the neighbour query; when the bottom is invalid it queries on top of the bottom (15.0f, true) and compares with the stored top. This matches the operator-approved correction and contradicts M7 row 885. Nothing settled. Open operator checkpoints: M7 row 885 correction and re-approval; two proposed new records (Stack lifecycle; PlaceRel Verify/completion with a COMPATIBILITY_POLICY for four unused words). `research/20261028-sparks-stack-1-claude-check.md`. |
| STACK-BLOCKS-1 | MERGED | adc91a08/70c3d660 (Sol medium, before the Terra default). Checked GetRotatedParentAxis<'Z'> 0x005507A0..0x00550854 and the ordinary top/bottom predicates 0x0057052C..0x0057066A (carried identity with local axis; unlock-9 literal with root axis; stack-bottom intention 4 with local axis). Nothing settled. Open: widening M15-076 to the whole StackBlocks class needs the operator. `research/20261028-stack-blocks-1-claude-check.md`. |
| HIKING-ROLL-CUBE-1 | MERGED | d9d84dab/b69ddafd (first Terra-built job: builder/reviewer default, 1 reviewer fix round). Checked Roll UpdateInternal 0x005C8A54..0x005C8B24 (axis change AND distanceSQ > 14400.0f, NaN fails; StopActing(false,false); pre-dock latch to success; refresh, -1 returns 2) and TransitionToPerformingAction 0x005C86E8..0x005C8938 (0 vs PI/2 face-turn via +0xD9/+0xD8, triggers 0x1F6/0x1F7, CreateRollBlockHelper(...,true), score 0.8f, state light 25/false). **Nothing settled.** Three new-scope proposals (Roll own path, PlaceBlockHelper, BehaviorManager state-light setter) await operator. `research/20261028-hiking-roll-cube-1-claude-check.md`. |
| ROLL-BLOCK-ON-SIDE-1 + ROLL-BLOCK-ON-SIDE-LOW-SCORE-1 | MERGED | 253e23c9 (test-only afb731b8/96e9aa12; Terra builder/reviewer, 0 fix rounds). Checked both shipped configs (no rotation/streamline keys), the ctor rotation default Json(true) 0x005C85DC..0x005C85F4, and ReadFromScoredJson flatScore -> +0x100 0x005BC4CA..0x005BC4E0 with parent scores 1.0 (playAlone/socialize) and 7 (playWithHumans). **Nothing settled.** Widening M15-077 to all three RollBlock instances awaits operator. `research/20261028-roll-block-on-side-1-claude-check.md`. |
| SPARKS-ROLL-BLOCK-1 | MERGED | ae6ffa06/3abe05c7. Checked the shared Roll failure filter 0x00570692..0x005706AC (time 30 s, distance 60 mm; the Hiking build had them swapped, my Hiking check missed it), PlaceBlockHelper 0x005B8CB0..0x005B8E68, SetBehaviorStateLights 0x005A4320..0x005A45F2, StopAndPlay 0x006396AC..0x00639722 and MS4 0x005A2114..0x005A226E. **Settled: M12-051, M7-030.** M15-077 waits for the title widening and a Ground completion getter record (operator). `research/20261028-sparks-roll-block-1-claude-check.md`. |
| POP-A-WHEELIE-1 | MERGED | ad24c6c7/b7190188. Checked PopAWheelieAction::Verify 0x0055295C..0x00552A72 (dock action 9 else 0x0300001A; dock success else 0x04000003; Radians pitch < 1 -> 0x0400000A; else 0) and the legacy adapter demotion. **Nothing settled.** M13-015 stays partial (manual/public callers on the legacy adapter). New-scope proposal for the ordinary PopAWheelie configured path awaits operator. `research/20261028-pop-a-wheelie-1-claude-check.md`. |
| SPARKS-POP-A-WHEELIE-1 | MERGED | 858a7470/f3a301e9. Binding-only: factory admits SparksPopAWheelie, native Sparked owner activity 24, objective PoppedWheelie -> 22 (same 0x16 as the ordinary Pop), cited to the native enum tables. **Nothing settled.** M13-041 names only the ordinary instance; widening awaits operator. FIX-KNOCKOVER-RACE-1 is still open and comes first. `research/20261028-sparks-pop-a-wheelie-1-claude-check.md`. |
| FIX-KNOCKOVER-RACE-1 | MERGED | 324e61db/1b93722d. Diagnostic package only: the fault did not recur in 20 targeted runs; the test now saves the full trace, journal and inner exception; no production behaviour changed. Fault stays UNKNOWN. Also checked Pop predicate 0x00570928, get-in 0x005C7496, Select 0x00552730 and completion 0x0055267E. **Settled: M13-041, M12-052.** `research/20261028-fix-knockover-race-1-claude-check.md`. |
