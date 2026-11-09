# M15-CUBE-HW, the first two cube runs: Claude judgment (manager, 2026-11-09)

Bundles (small files committed; frames, messages and status streams kept by the operator):
- `acceptance/hardware/20261109-080749-M15-CUBE-HW-b4fc8d`
- `acceptance/hardware/20261109-083932-M15-CUBE-HW-014f35`

Hardware evidence never raises provenance. No record status changes.

## Run 1 (b4fc8d): no cube report, so no pickup was attempted

- **What worked:** the camera streamed (8,153 image chunks), vision ran ("ImageQualityGood"), and LIGHTCUBE2 connected.
- **What didn't:** no RobotObservedObject came in.
- **Cause, from the captured frames:** the head stayed at the default -0.42 rad. Rebuilt frames show the cube at the
  image's top edge with the top of its marker cut off. Replaying all 1,335 frames through the detector on the manager
  PC, with the marker library present, finds 0 markers.
- **Script fix (7254ff23):**
  - the public AI head move to -0.15 rad first;
  - stop early without the marker library.
- **Stack defect found on the way:** with no marker library, live vision decodes nothing and says nothing. Queued as
  MARKER-LIBRARY-DIAG-1.

## Run 2 (014f35): the cube is seen, both pickups fail

- **Seen:** 55 cube reports (ObjectId 0, LIGHTCUBE2).
- **First pickup refused** with "External actions are disabled by a reaction". The engine's own AcknowledgeObject
  reaction was running (turn to the cube, `ag_reacttoblock_react_short`), as in the original, so this is correct
  behaviour. The script now retries admission for up to 20 s.
- **Drive pickup cancelled** with 0x02000000 after 6.3 s.
  - The ReactToUnexpectedMovement trigger fired on "TURNED_BUT_STOPPED". From 42.95 s the left wheel was at 0 while
    the right was commanded up to 55 mm/s, the gyro read about 0, and the heading held at 0.806 rad. He was physically
    stuck, so **the reaction was correct**.
  - **The real question is the approach.** The planner's goal was about 442 mm away, with a desired final heading of
    -3.076 rad: a pre-dock pose on the far side of the cube (reported at X = 239), facing back toward the start. He
    drove to x = 434 and stalled there.
  - The engine chooses the closest usable pre-action pose. Why the near side was not used is unknown: it may have been
    rejected, or the stack may have chosen wrongly. Queued as PICKUP-PREDOCK-SIDE-1.
- **Lift-to-32 mm while not carrying:** both put-down phases ran as an ordinary lift move (0x00000000). That is correct,
  because the place branch applies only while carrying (0x0052AC40..0x0052AC76).

## M15-022 hardware observation

Not obtained yet: nothing was carried, so no 0x44 ground placement ran.
