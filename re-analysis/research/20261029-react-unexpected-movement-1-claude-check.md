# REACT-UNEXPECTED-MOVEMENT-1: Claude check (manager, 2026-10-29)

**Job.** A Claude-run job:
1. cozmo-extractor read `research/20261029-react-unexpected-movement-1-read.md`, and the manager checked it against
   the .so;
2. cozmo-implementer built it;
3. cozmo-verifier gave a PASS with 0 blocking findings and 4 queued.

**Verdict: PASS, merged. Nothing settled** (M7-031).

## Build (ReactToUnexpectedMovementBehavior, OffTreadsBehaviors.cs; Behaviors.cs Reactions())
- **Gates.** Not runnable off treads (R1). Not runnable with robot+0x34A (R2), but this is unwired live; see below.
  Runnable while carrying (R3, 0x00609C1A). IsRunnableInternal is true.
- **Side (H2).** The movementSide of the last in-engine UnexpectedMovement broadcast. It starts at 0, is kept across
  runs, and is written whether or not the behaviour runs (AlwaysHandle 0x00609BF8). It is stored before the manager's
  CheckReactions, inside the same engine tick (H3).
- **Init.**
  - The emotion event comes first (I1).
  - The trigger is 0x1AE when the selected severe need is Repair, 0x1AD when it is Energy, otherwise 0x1AC (I3). The
    stack's SevereNeeds.Selected is [[robot+0x264]+0x30]+0x14 per M15-072.
  - BODY is locked iff the side is BACK (I4).
  - One TriggerLiftSafeAnimationAction(1, true, lock, 60.0f, false) is started through the ActionList (I5).
- **Objective.** BehaviorObjectiveAchieved 0x25 on any result (K2, lambda 0x00609C5A). The behaviour ends with its
  action (U1). Stop does nothing (T1). Resume re-runs Init (P1).
- **Removed:** the log-only objective and the PlayTrigger path that was not lift-safe.

## Checked by Claude
Against the .so, before the build:
- Init 0x00609B06..0x00609B4E;
- AlwaysHandle 0x00609BF8..0x00609C0A;
- +0x28 0x00609C1A and Stop 0x00609C1E;
- the lambda 0x00609C5A.

The verifier independently re-confirmed every hunk, the production wiring (InitializeRobotReactions → Reactions(robot)
builds the bound instance) and the absence of circular tests.

## Queued
1. **R2 is unwired on the live path.** Production does not fill RobotState34a for reactions, so the gate is skipped
   with a MISSING. This is written into M7-031's `unresolved`, and it blocks settling.
2. **The broadcast subscription is never removed.** Each reconnect leaves the old instance alive. The callback only
   writes a dead field and runs under the tick lock. Hygiene only; ReactToImpact does the same.
3. **The reactions' completion owner** is not retired in BehaviorManager.Dispose. Stale callbacks are prevented by
   the tag check. This is shared M8 lifetime work.
4. **Test reach labels.** H3 is effectively unit, because the broadcast is substituted. One test has no label.
5. **M15-072's `unresolved` is stale:** it says there is "no selected-need scalar", but AIComponent.cs:77-86 has one.

## Manager decision pending
The C# UnexpectedMovementReport carries extra fields (average speeds, count, start timestamp) that are not in the
6-byte engine message. These go to the operator.
