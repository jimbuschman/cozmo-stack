# M5 DRIVING: the drive was cancelled by a cliff stop (2026-10-20)

Question from hardware run `20261020-200624-M5-HW-97161e`: DriveToPose (60 mm) ended CancelledWhileRunning after
~1.1 s; no Loop or End driving animation was requested.

**Answer: the cancel is engine-faithful; what triggered it is a probable stack defect (cliff threshold raised early).**
Extractor pass (Claude cozmo-extractor); manager recomputed the travel from the bundle.

## What happened (bundle)

- ~223339 ms: the stack sent SetCliffDetectThreshold 400 ("New cliff detect threshold 400 (old threshold 50)").
- 224290: one cliff sample of 379 (neighbours 874, 879); the firmware stopped for a cliff
  (`ProxSensors.UpdateCliff.StoppingDueToCliff`), sent PathFollowingEvent PATH_INTERRUPTED and RobotStopped (0xD4).
- The stack's RobotStopped handler (`Sensors.cs` ~912-934) ran ActionList.Cancel(-1): drive result 0x02000000
  CANCELLED_WHILE_RUNNING; the start animation completed (cancelled) before the drive.

## Engine-faithful parts (cited by the extractor)

- RobotStopped: Evaluate..., then when cliff-enabled RequestCurrentBehaviorEndImmediately (M8-019 gap; no behaviour in
  this test), ActionList::Cancel(-1), broadcast: 0x0053542E..0x00535480.
- Cancel sets 0x02000000: 0x00540A3C..0x00540A40. DeleteActionAndIter deletes the action before broadcasting its
  completion (0x0053FA78, 0x0053FAB2), so the nested start-animation completion comes first, as in the stack.
- ~DriveToPoseAction -> DrivingAnimationHandler::ActionIsBeingDestroyed: state 5, cancel start/loop/end tags, no Loop or
  End played (0x004F0F98..0x004F0FC0); HandleActionCompleted on a cancelled start does nothing (0x004F0D80..0x004F0DCC).

## The suspect step

The engine sends 400 only after the accumulated drive-centre travel passes 50 mm (M4-019; 0x00512D4E -20 mm offset,
0x00512E7C..0x00512E80 accumulate, 0x00512E8A literal 50.0f at 0x00512D18, 0x00512E9C 400). The robot's own reported
poses (0xF0, frame 0 throughout) give only **5.7 mm** of drive-centre travel from session start to 223339 ms
(25.6 mm by 224300). With the engine's 50, the single 379 sample would not have stopped the robot.
So the 400 did not come from the distance rule; see the root cause below. (An earlier draft of this note blamed the
distance accumulation; the replay disproved that.)

## Root cause (manager replay, 2026-10-20)

Replaying the bundle's inbound states through the stack (`M4ControlTests.Rig`, throwaway test) shows the distance
schedule was correct: 5.6 mm when the threshold became 400. The 400 came from **SetOnChargerPlatform(false)**:
the robot status carried IsOnCharger (bit 12, 0x9391) from the start of the run until the drive moved it off the
contacts (0x8391 at ~223339 ms). Cozmo had been left on the charger. With no located charger in the world, the stack's
Robot::Update platform step takes the no-charger branch (0x00513E26) and clears the platform flag as soon as the contacts
drop, which sends 400. The engine creates the located charger on SetOnCharger's rising edge and holds the flag (and 50)
until the robot footprint leaves the charger quad. That gap is already recorded: **M13-017** (IMPLEMENTATION_GAP,
`unresolved`: "this stack never creates the located charger on SetOnCharger's rising edge"), reported MISSING live.
It belongs to M13. For M5: the harness must check that Cozmo is off the charger before it starts.
