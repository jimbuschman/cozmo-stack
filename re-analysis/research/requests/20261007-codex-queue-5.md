# Codex standing queue 5 (from 2026-10-07): rows for the future layers

- **Requested by:** manager (Claude)
- **Use:** start only after queue 4 and the manager's M1 message are done. Work through the tasks in order.

## Rules

Queue 3's rules apply:
- depth over speed;
- a coverage table first;
- no task finished while anything in it is NOT DONE;
- research lane, committing and pushing your own answers;
- floats as bits, UNKNOWN where unsettled, quoting each record before contradicting it.

This research waits for the manager's check when its layer's turn comes (PROJECT_STATE, "The plan"), so make each
row easy to check. Give every row the exact instruction range, and keep each answer within its layer.

## The queue

**Q14. Sound under ADP-1.**
- **Scope:** for every item `20261005-adp1-triage.md` classes as KEEP or VERIFY, build rows for the decision logic only.
- **Include:** the event and action order, container choice with the RNG and shuffle state, switches and states, RTPC
  propagation, limits and ducking decisions, routing, the scheduling and timing, and the source lifecycle.
- **Leave out** per-sample DSP arithmetic; that's ADP-1's equivalent part.
- **For VERIFY items,** settle the boundary first.
- **Answer:** `20261007-sound-keep-rows.md`

**Q15. Singing (M9) decisions.**
- **Scope:** the music engine's decision path: segment, playlist and transition selection, MIDI dispatch, note to
  sampler-target selection, note-on/off timing, the envelope and vibrato parameters as values, and the state changes.
- **Not** the oscillator, envelope or filter sample maths (ADP-1).
- **Answer:** `20261007-singing-decision-rows.md`

**Q16. The rest of M10 and M15.**
- **Scope:** every M10-derived and M15-freeplay record that isn't EXACT_SOURCE.
- **Start from** your `20261004-m10-strategies-extraction.md` and the rushed M10/M15 part of
  `20260930-pre-extraction-M5-M10-M15.md`. Complete and check them.
- **Answer:** `20261007-M10-M15-rows.md`

**Q17. The rest of M7 and M8.**
- **Scope:** the M7 and M8 records not built by R-BEH2, plus the open items recorded by the Opus verifications of
  R-BEH2 (`20261003-R-BEH2-verify-*.md`): the game channel wiring, UI requests, SmartDelegateToHelper, and the six
  helper classes (start from your `20261004-helpers-extraction.md`).
- **Answer:** `20261007-M7-M8-rows.md`

**Q18. B-ACTIONS batch 3b's child actions.**
- **Scope:** the bodies of the child actions that the flip, dock and charger compounds build: DriveToPose, TurnInPlace,
  MoveLift, DriveStraight, Dock, TurnTowards, and whichever else they construct.
- **Give** their Init, CheckIfDone, results and parameters, so B-ACTIONS can finish when M12/M13 come up.
- **Answer:** `20261007-child-actions-rows.md`

**Q19. M11–M14 open items.**
- **Scope:** the items `20260930-pre-extraction-R-VIS-2.md` left PARTIAL or NOT DONE, and M11-054's solvePnP and
  corner pipeline. Start from `20261004-solvepnp-extraction.md`.
- **Answer:** `20261007-M11-M14-rows.md`

Stop after Q19 and append a one-paragraph summary to the Q19 answer.

## Amendment (manager, 2026-10-07): order and reachability

- **The order:** sound has moved last in the plan. Do Q16, Q17, Q18 and Q19 first. Q14 is parked at its checkpoint
  (21 CHECKED / 45 PARTIAL / 74 NOT DONE).
- **Before resuming Q14 or Q15, do a reachability census first.** For each open obligation, decide whether any shipped
  input can reach it:
  - the shipped banks (Cozmo, SFX, UI and Music: their objects, properties, actions, RTPC bindings, switches and states);
  - the engine's own call sites into Wwise (which events, switches, states and RTPCs the engine and the Unity app ever
    post, with which values).

  Mark each obligation REACHABLE, citing the bank object or the call site, or UNREACHABLE, citing the census evidence
  that nothing shipped reaches it.
- **Only REACHABLE obligations get rows.** An UNREACHABLE one is listed with its evidence and isn't extracted. That's
  not a loosening of "Exact, always": a path no shipped input can reach can't change what Cozmo does. The manager
  checks the census before it's used.
