# Claude driving Cozmo through the MCP bridge: session notes (2026-11-20)

The operator ran a robot session, 15:47–15:58 UTC. Claude used the Cozmo MCP tools with channel push turned on
(`--channel`, `--dangerously-load-development-channels server:cozmo`).

These are **observations for the operator**, not findings. None of them was checked against the binary. Where engine
behaviour is involved, the open question is written as a question.

## 1. Channel push: works

- Pushed without polling, in cursor order, with `gap:false` throughout:
  - cube-tapped, cube-moved, cube-seen;
  - off-treads-changed (pickup, put-down, OnBack);
  - face-seen, face-recognised;
  - reaction-changed, action-completed, unexpected-movement, cliff-detected;
  - charger-seen, on-charger, going-to-sleep.
- Bursts arrive fine. Three cube events within 1.7 s came through as separate pushes, in order.
- Non-significant events are held back as designed and can be read with `read_events`. Examples:
  - off-treads InAir→Falling→OnTreads (`putDown:false`);
  - repeat face sightings on a held track;
  - animation start and end.

### Filtering notes (judgement calls, not defects)

- **A drop is not pushed.** Falling, and landing from a fall, are `significant:false`, so an LLM is never told it was
  dropped, only that it was picked up. Is that intended?
- **Faces are re-pushed under new temporary IDs.** One person (the operator) produced face-seen −1, −2 and −3 within
  about 50 s, then face-recognised 1. Each time the track is lost and found again, it is pushed again. That is about
  one push every few seconds while someone moves near him.
  - First recognition took 3.4 s from face-seen −1 to face-recognised 1.
  - A later re-acquisition took 0.24 s from −3 to 1.
- **reaction-changed carries only trigger numbers** (22→2, 22→8, 22→0, 22→20, 22→9). An LLM can't name the reaction
  without a lookup table. get_state does name the reaction (`"reaction":"CliffDetected"`, `"PlacedOnCharger"`). Could
  the push payload name it too?
- **cliff-detected `sensors:3`:** the meaning isn't known. Is it a bitmask of the front-left and front-right sensors?

## 2. Reactions interrupting tool actions (all looked reasonable)

| When (UTC) | My action | Interrupted by |
|---|---|---|
| 15:49:05 | head 0.35 rad | ReactToOnTreads (after a put-down) → CANCELLED_WHILE_RUNNING; a retry worked |
| 15:50:16 | say-text | PetInitialDetection running; `waitAdmittedMs` waited 6.5 s, then it spoke |
| 15:50:58 | CodeLabHappy animation | cube 1 seen → trigger 8 → cancelled |
| 15:52:33 | drive −60 mm | cube 0 seen → AcknowledgeObject → cancelled |
| 15:57:42 | mount-charger | cube 5 (Block_LIGHTCUBE1) seen → trigger 8 → cancelled |

Did the PetInitialDetection at 15:50 come from the operator's face or beard? No pet was present.

`waitAdmittedMs` worked well. `lock-reactions` on
ObjectPositionUpdated/CubeMoved/FacePositionUpdated/PetInitialDetection stopped the cube interruptions during
docking.

## 3. mount-charger: five attempts, none finished on its own

Charger: world object 4, Charger_Basic. It sits among cables on the operator's desk.

1. **15:57:22: unexpected-movement during the reverse.** Left tread −79 mm/s, right −16 mm/s, side 2. Likely a cable
   held the right tread. ReactToUnexpectedMovement → CANCELLED_WHILE_RUNNING.
2. **15:57:34: DID_NOT_REACH_PREACTION_POSE.** The robot was 8.67 mm outside the approach box on Y (offset 30.1 mm,
   threshold 21.4 mm).
3. **15:57:42:** cube-reaction cancel (see section 2).
4. **15:58:09: cliff-detected, sensors 3, during the mount.** ReactToCliff → cancelled. Reactions to seeing things
   were locked at the time; the cliff reaction was not.
5. **15:58:17: cliff-detected again,** sensors 3, about 5 s into the next attempt.

The operator then said "back up a little more". A plain drive-straight of −30 mm reached the contacts:
- on-charger fired at 15:58:24;
- battery read 4.39 V with isCharging true;
- going-to-sleep followed.

**Open question:** the two cliff stops next to the charger. Possible causes:
- the robot really was near the table edge (the camera showed table surface, head −0.33);
- the cliff sensors read the black charger plastic as a drop.

This is unknown. How does the engine's MountCharger / docking path treat cliff reactions? Does the original
suppress them near the charger? That needs the binary (known.py first) before anything is changed.

## 4. Object identity oddities

- At 15:50:58 cube-seen reported objectId 1, type 3, z≈143 mm.
- At 15:57:42 cube-seen reported objectId 5, type 1 (Block_LIGHTCUBE1, not radio-connected). It was the top cube of a
  two-cube stack on cube 0.

Was the same physical cube given two world IDs (1 and 5)? Possibly an origin or identity change between the two
sightings. Not investigated.

## 5. Notes for "Cozmo as the body for an LLM" (the main goal)

- **Seeing its own lift.** At head −0.2 the lift's edge sits at the bottom-right of every frame. Claude spent two turns
  chasing it as an object. Could get_camera_image say whether the lift is in view (from the lift height and head
  angle)? That's a convenience, not engine behaviour.
- **Small, distant markers.** The charger was ~30 cm away and in frame, but its marker was too small to detect. Claude
  missed it and called it a "power brick". Only driving within ~15 cm got a detection. An LLM needs to know that
  "nothing detected" does not mean "nothing there".
- **Plain drives play animations.** The hiking drive start and end animations play on a plain drive-straight
  (`shouldPlayAnimation` defaults to true). That's nice for personality, but it adds about 1–1.5 s per move.
- **Disconnect after sleep:** robot-disconnected arrived at 15:59:34 (reason WifiTimeout, code 1), about 70 s after
  going-to-sleep on the charger. Is that the expected sleep behaviour, or a dropped link?
- **Cliff test passed (15:52:49).** A drive toward the table edge stopped at the lip, ReactToCliff ran
  (huh → edgeliftup), and a backup afterwards worked.
