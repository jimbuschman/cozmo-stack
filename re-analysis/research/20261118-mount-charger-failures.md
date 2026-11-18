# mount-charger fails three times from just in front of the charger

Observed 2026-11-18 in an operator MCP session (manager notes; observation, not a settled finding).
Session bundle: `re-analysis/acceptance/hardware/20261118-075139-LLM-SESSION/` (not committed).
Operator: "he was almost on it".

## Setup

Cozmo started the session on the charger. The engine added the charger at startup:
`BlockWorld.AddLocatedObject: Adding new active Charger_Basic object and ID=0 ... at (30.0, 0.0, 0.0), in frame Origin1`.
He drove off for a cube pickup, freeplay ran briefly (NeedsSevereLowEnergy), and then freeplay was stopped. The charger
was seen repeatedly before the attempts (`seen object 0 Charger_Basic Known at (~15, ~4..19, ...) yaw=~168..178deg`;
the z values are noisy, -14..+15 mm).

## Attempts (`mount-charger chargerId=0`)

| # | request (UTC / atMs) | result |
|---|---|---|
| 1 | 14:01:09.368 / 569407 | DID_NOT_REACH_PREACTION_POSE (0x04000001) after 9.4 s |
| 2 | 14:01:33.276 / 593315 | NOT_ON_CHARGER (0x04000006) after 20.6 s |
| 3 | 14:02:21.691 / 641730 | DID_NOT_REACH_PREACTION_POSE (0x04000001) after 6.0 s |

- **Attempts 1 and 3:** `[DriveToPose+TurnTowardsObject]` completed SUCCESS (the path was planned, sent and completed;
  VisuallyVerifyObject succeeded), and then `[DriveToObject+MountCharger]` failed with 0x04000001. No log line between
  them says what failed (distance or angle to the pre-action pose). Unknown whether the engine logs one there that the
  stack does not reproduce, or the engine is also silent. To check: DriveToObjectAction's pre-action pose check in the
  binary.
- **Attempt 2:** the robot reached the pre-action pose, turned about 177 deg (TurnInPlace to -7.7 deg), lift to 45 mm,
  and backed up. Then `MountChargerAction.CheckIfDone.PositionForRetry: Turning and mounting the charger failed
  (action result = BackupPitchedTooFar). Driving forward to position for a retry.` After DriveStraight, the action
  ended NOT_ON_CHARGER. The operator saw the robot nearly on the charger.
- MISSING markers hit on the path (log text only): `M8-015 ActionCompleted full ActionResult EnumToString`, `M8-017
  sequential failure full EnumToString` (for 0x04000001, 0x0400000A, 0x04000006); `M13-033`/`M13-034` PrintPath
  output; `M12-024` EraseAllPlannerObstacles visualizer handoff.
- Repeated `Observed active object of type Charger_Basic but it's not connected. Is the battery plugged in?` (stack-log
  lines 5981..7837). Whether this affects the mount path is not established.
- The cube showed as disconnected after attempt 2 (`CubeLightComponent.Update.SetLightsFailed`). Probably unrelated.

## Open questions (not answered)

1. What the pre-action-pose check after DriveToObject's drive compares (distance/angle thresholds), and whether the
   stack's robot pose vs the charger's observed pose (noisy z, yaw ~168..178 deg) feeds it the same as the engine.
2. The BackupPitchedTooFar threshold and the pitch source in MountChargerAction, and why the retry ended
   NOT_ON_CHARGER instead of a second backup.
3. Whether the original would also fail from this physical position (needs a reference run, or a binary trace of the
   checks above).

## After the attempts: placed on the charger by hand

The operator put Cozmo on the charger by hand. The robot's own RobotState (0xF0) status flags show he is charging.
This was decoded from frames.jsonl by the manager's script: Status u32 at payload offset 76 after the tag, with
IsOnCharger bit 12 and IsCharging bit 13 (`Cozmo.Protocol/Clad/MessageExtras.cs:80`).

| session time | battery | onCharger | charging |
|---|---|---|---|
| 3.7 s | 4.236 V | true | true |
| 90.2 s (drove off) | 4.096 V | false | false |
| 676.0 s (placed by hand) | 4.388 V | true | true |
| 733.4 s (latest) | 4.628 V, status 0x00003310 | true | true |

The engine ran `ReactToOnCharger` / `PlacedOnCharger` (`ag_reacttocharger` -> `anim_rtc_react_01`, SUCCESS).

**API gap noted:** the MCP `get_state` reports `isOnCharger` and `batteryVolts`, but not the robot's IS_CHARGING flag
(available in the stack as `CozmoRobot.Charging` / `Sensors.Charging`). Adding it to the C# API needs operator approval.
Not done.

No code changed.
