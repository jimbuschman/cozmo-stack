# Claude driving Cozmo through the MCP bridge: session 5 notes, part 1, commands (2026-11-23)

**Status (manager, 2026-11-25):** these notes cover all six parts of the 2026-11-23 session (14:48–15:31 UTC). The
findings were taken up as jobs in `re-analysis/jobs/trial-m13/QUEUE.md`, all MERGED 2026-11-23/24:
- LLM-WAKE-CANCEL-1 (F2, F7)
- TEACH-FACE-FOLLOWUPS-1 (F4, F5)
- CHARGER-MOUNT-REVERSE-1 (F6)
- INTERACT-FACES-NO-FACE-1 (F9)
- PLACED-ON-SLOPE-INPUTS-1 (Q5)
- CUBE-REACTION-GATES-1 (Q1, Q2)
- NEEDS-HOST-STATUS-1 and SEVERE-NEEDS-OWNER-1 (Q6–Q9)
- SESSION5-API-REPORTS-1 (Q4)
- FREEPLAY-ACTIVITY-INPUTS-1 (Singing)
- PATH-PLANNING-ERRORS-1 (F1)

The robot bundles are local only and not in git, because frames.jsonl is over 100 MB:
`re-analysis/acceptance/hardware/20261123-084806-LLM-SESSION/` (part 1) and `20261123-091241-LLM-SESSION/` (parts 2–6).

The operator ran a robot session from 14:48 to 15:10 UTC. Claude drove Cozmo through the Cozmo MCP tools, with channel
push turned on. The operator ran the SCOPE.md A checklist (part 1, about 15 minutes).

- **Bundle:** `re-analysis/acceptance/hardware/20261123-084806-LLM-SESSION/` (`stack-log.txt`, `events.jsonl`,
  `frames.jsonl`).
- **Times:** host UTC from tool replies and channel pushes.
- **Status:** these are observations for the operator, not findings. Nothing here has been checked against the binary.
  Where engine behaviour is involved, the open question is written as a question.

## Results

| Item (SCOPE A) | Result | Evidence |
| --- | --- | --- |
| Go to a place | **SUCCESS** | 14:49:41–45. Target (200, 0, π/2) from the charger. Ended at (205.2, 1.3) with heading 1.5636 (89.6°). Action tag 1 result 0. Hiking drive start/loop/end; the loop cancel is the normal hand-off to the end animation. |
| Roll | **SUCCESS** (2nd try) | Try 1 (cube 1, 14:50:07) failed, see F1. Try 2 (cube 3, 14:50:57–14:51:11): tag 5 result 0, `anim_reacttoblock_lifteffortroll_01`. Cube pose went from upright to on its side (q from (0.61,0,0,-0.79) to (-0.43,-0.56,-0.43,0.56)), and the cube reported `moving` with accel during the roll. Locks held: ObjectPositionUpdated, CubeMoved. |
| Turn to cube | **SUCCESS** | 14:51:21–25. Turned 90° away, then turn-to-cube on cube 3: tag 7 result 0, cube back in view. |
| Track face | **not run** | The operator stopped the call. See F2. |
| Speech | **SUCCESS** (2nd try), operator heard it | Try 1 (14:54:35–40) was cancelled by ReactToPet, see F3. Try 2 (14:54:50–55.8), with PetInitialDetection locked: tag 2 result 0, SayTextAnimation 5.8 s. |
| Teach / rename / erase | **all three confirmed** | Teach "Tester" 14:55:17–45: enrolled id 4, full Meet Cozmo sequence (lookface, sayname_01/02, celebration). Rename to "Buddy" 14:55:50: confirmed; RespondToRenameFace played `anim_meetcozmo_reenrollment_sayname_02` (14:55:51.7–55.0). Erase id 4 14:55:58: confirmed. The operator was not asked whether the names were heard. See F4 and F5. |
| Pick up | **SUCCESS** (after manual positioning) | Cube 1 (unpowered), 15:09:12–18. `anim_reacttoblock_lifteffortpickup_01`; isCarrying true, carriedObjectId 1. |
| Stack | **SUCCESS** | Cube 1 onto cube 3, 15:09:47–15:10:02: action tag 65 (type 25) result 0. Camera shows cube 1 on cube 3; cube 1 pose z = 64 mm. See O3. |
| Mount the charger | **not passed** | Three engine attempts, see F6. He got onto the charger only through manual reverses plus a turn attempt. |

Personality classes seen on the robot for the first time (SCOPE B "not exercised"):
- **ReactToFrustrationMinor**, 14:59:44.5, after a failed pickup: `anim_reacttoblock_frustrated_int1_01`.
- **ReactToFrustrationMajor**, 15:00:14.0: `anim_reacttoblock_frustrated_int2_01`, then drove off with `anim_driving_upset_*`.

Neither has been checked against the engine's frustration thresholds.

## Findings to work (one job each)

**F1. Most failures came from paths into desk clutter (operator: "hes failing because you keep running him into stuff").**
The memory map does not contain the glass jar of clips or the cables, so the planner routes through them.
UnexpectedMovement then cancels the action, which is the engine's reaction to a stall.

| Time | Action | Wheel speeds L/R (mm/s), type/side | Cause (camera) |
| --- | --- | --- | --- |
| 14:50:12.76 | roll cube 1 | 82.6 / 96.6, type 2 side 1 | cable |
| 14:59:16.2 | pickup cube 3 | 60.5 / 125, type 0 side 1 | glass jar |
| 14:59:51.98 | pickup cube 3 | 7.5 / 104.6 | jar again |
| 15:00:12.56 | pickup cube 1 | 145 / 215, type 2 | not seen |
| 15:00:19.1 | frustration drive | 245 / 85 | — |
| 15:08:13.8, 15:08:16.5 | Freeplay PlayAlone pickup of cube 3 | — | charger cable loop |

What worked: drive the robot by hand in short drives, each checked on camera, to a spot square in front of the target
face. Only then call pickup/stack/mount. Every action done that way succeeded.

Open questions:
- Does the original's planner have the same blind spot? Probably yes: only markers and cliffs enter the map.
- Should the MCP guide tell the LLM to pre-position in this way?

**F2. Track face can only be cancelled by aborting the tool call.** There is no cancel tool. An LLM that starts
tracking has to break its own pending call to stop it. That is awkward for the "body for an LLM" goal, and the operator
rejected the call. Needed: a `cancel-action` tool (or tracking with a duration), recorded as a bridge decision.

**F3. False PetInitialDetection.** At 14:54:40.17 ReactToPet started (trigger PetInitialDetection) and cancelled
speech. The operator says there was no pet in the room. The camera at that moment was on the operator, about 45 cm
away. Open question: does the original's pet detector fire on this scene, or is it our port? Replay the frames around
14:54:35–40 through the pet path.

**F4. The face list does not follow rename or erase.** After the rename to "Buddy" (14:55:50) and the erase
(14:55:58), get_state `knownFaces` still showed `{faceId 4, name "Tester", enrolled false}`, at 14:55:58 and again at
15:08:10. The engine-side operations were confirmed. The bridge's host face projection does not apply renamed/erased.

**F5. Teach-face leaves him in the MeetCozmo activity.** After enrollment, activity and highLevelActivity stayed
`MeetCozmo` (MeetCozmo_FindFaces_Socialize / InteractWithFaces). Its behaviours took the motors:
- turn-in-place was cancelled at 14:56:50 (after ~50 s);
- the head move was cancelled at 14:57:01.

Only `freeplay(false)` at 14:57:07 brought it back to Selection/Wait. In the original, leaving the Meet Cozmo screen is
an app action. Question: which game message does the Unity Meet Cozmo flow send on exit, and should teach-face send it
when the enrollment report arrives?

**F6. Charger mount: three engine attempts, none succeeded.**
1. **14:57:21–24, DID_NOT_REACH_PREACTION_POSE.** Pose-check offset (2.2, 40.8) mm against a threshold of
   (42.8, 21.4). During the approach the charger estimate moved: y 43 → 61 → 46.
2. **14:57:29–39, LAST_PICK_AND_PLACE_FAILED.** Firmware docking report `success:false dockingResult:6`;
   `anim_driving_upset_*`.
3. **15:03:21–39, after manual positioning about 18 cm in front and square.** The approach succeeded (firmware docking
   report `success:true dockingResult:2`). The reverse onto the charger stalled: UnexpectedMovement at 15:03:39.6,
   type 0 side 2, L -49 / R -12, so the action was cancelled. Heading was about 6° off the charger axis.

After that he reached the charger by hand:
- manual reverses of 40, 30 and 40 mm;
- a `low-battery` event at 15:05:35 (3.29 V, sag under stall);
- a turn attempt at 15:05:44 that stalled on both sides (L -285 / R 264);
- `on-charger` at 15:05:45.68.

The charger estimate also jumped between single sightings: 14:51:21 (34.2, 0.4) to 14:51:23 (32.6, 43.4, z -5.8), and
q1 -0.099 (tilted) at 15:02:55. This is the same family as DOCK-CHARGER-POSE-1 and CHARGER-MOUNT-CLIFF-1.

Question: does the engine's reverse-onto-charger phase disable UnexpectedMovement, or tolerate it, the way
ReactToOnCharger's lock does? The reverse was cancelled by the reaction.

**F7. No way to wake him on the charger from the bridge (operator: "thats an issue i thought we had already fixed").**
- **What blocked us:** ReactToOnCharger holds action admission. Drive-straight was refused (`not-admitted`; reaction
  PlacedOnCharger) at 15:05:48 and 15:06:41, and `freeplay(true)` did not end the reaction. This matches the engine,
  per CHARGER-REACTION-END-1: the reaction ends only on CancelIdleTimeout (G2E 0x54) or a pickup.
- **What exists but isn't wired:** the engine side is built: `CozmoEngine.CancelIdleTimeout()` at
  `cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs:2412`. Neither `Cozmo.Bridge` nor `Cozmo.Mcp` calls it, so an LLM
  cannot do what the app's wake did (PauseManager.cs:186–192, 365–379), after which Freeplay's DriveOffCharger takes
  him off.
- **Needed:** a `wake` tool that sends CancelIdleTimeout. This is an app input, under the existing host app-input
  policy.

**F8. `freeplay(false)` sent during ReactToOnCharger did not take.**
- At 15:06:52, freeplay(false) was submitted while ReactToOnCharger ran.
- At 15:08:10, after the operator lifted him off, state still showed `freeplay.enabled:true`. Freeplay ran
  PutDownDispatch_LookForFaceAndCube, then PlayAlone, whose pickup of cube 3 drove into the cable twice (F1).
- A second freeplay(false) at about 15:08:17 took effect.

Question: is the setting message dropped while a reaction holds, or did the reply report stale state? The first reply
already showed `enabled:true`.

## Observations (no action yet)

- **O1. Cube identity.** Two cubes were reported 23 mm apart at the start:
  - cube 1 (LIGHTCUBE1, unpowered) at (226.8, 145.9), 14:50:04;
  - cube 3 (LIGHTCUBE2) at (238.4, 125.4), 14:50:06.

  That is less than one cube width. Claude first called cube 1 a ghost. That was wrong: at 14:59:04 both were seen side
  by side. The operator said they added an unpowered cube around 14:58. Which physical cube was "cube 1" at 14:50 is
  unresolved. Cube 5 (LIGHTCUBE3) was seen at z ≈ 143 mm (a shelf).
- **O2. Unprompted cube 3 tap reports** at 14:57:45, 14:58:25 (numTaps 2), 14:59:01 (2), 15:07:39 (2) and
  15:08:27, while the cube lay on its side. Not confirmed whether the operator tapped it.
- **O3. TRACKS_LOCKED inside the stack.** An unnamed animation, tag 3000753 at 15:09:57.8, was refused with
  TRACKS_LOCKED during the place-on-cube. The stack still returned SUCCESS. Is this engine-normal (an animation
  requesting locked tracks during docking)?
- **O4. Relocalization corrected about 30° of drift.** After the bumps, the pose estimate was off by about 30° and
  about 250 mm. Seeing cube 3 relocalized him (poseFrameId 16; a 15 cm drive appeared as about 265 mm). Planned paths
  computed before that were wrong. Re-read state after any bump.
- **O5. Two DID_NOT_REACH_PREACTION_POSE at about 41 mm:** charger Y 40.8, cube X 42.4 (`superseded:true`). Both
  followed estimate shifts during the approach. Possibly a pattern; not traced.

## SCOPE.md impact (proposed; the manager applies it only after the MISSING check and spot-read)

**Newly passed in a session:**
- go to a place (was "not exercised");
- roll (after the earlier robot run);
- turn to cube;
- speech, which confirms the 2026-11-20 completion fix;
- teach/rename/erase (rename and erase newly shown);
- stack (two cubes this time; the earlier stack was on an unpowered target).

**Not passed:** mount the charger; track face (not run).

**B:** ReactToFrustration (minor and major) seen; it was "not exercised".

**The bundle's `stack-log.txt` has not been scanned for MISSING on these paths.** That check comes before any item is
counted finished.

# Part 2: Freeplay with cubes (15:12–15:16 UTC)

- **Bundle:** `re-analysis/acceptance/hardware/20261123-091241-LLM-SESSION/`. A new MCP server connection started
  15:12:46, with Cozmo on the charger, so the event cursors restart at 1.
- **MISSING check:** `stack-log.txt` has 78 MISSING lines. None is the AcknowledgeObject M15-081 / M11-010
  ghost-visibility marker (ACK-STACKED-LOOK-1). The other 77 are not yet sorted by path.

## Results

| Item (SCOPE B) | Result | Evidence |
| --- | --- | --- |
| DriveOffCharger | **ran, SUCCESS** | Freeplay was enabled at 15:12:50 with him on the charger at connection. At 15:12:50.91 it chose DriveOffCharger (activity Socialize, "highest score 1000.09"). `off-charger` came at 15:12:51.29. DriveStraight (type 8) tag 3000001 returned result 0 at 15:12:53.99, with `anim_loco_driving01_start/loop/end`. Distance 158 mm by pose; the engine's figure is 96 + 60 mm (HANDOFF-CHARGER-REACTION-END-1). Then FindFaces_socialize. |
| PutDownBlock | **ran, cube put down** | Claude picked up the powered cube by command (15:13:41), and Freeplay was turned off for that. Before (15:13:48): carrying cube 1, no face or other cube in view. Freeplay on: at 15:13:50.81 it chose PutDownBlock (Socialize, "highest score 0.12"). Cube impact report at 15:13:53.45. Compound action tag 3000113 sub-results [0, CANCELLED, NOT_STARTED]: it was cancelled at 15:13:54.41 by ObjectPositionUpdated → AcknowledgeObject (see Q1). After (15:13:59): isCarrying false; the cube on the floor upright, pose Known, 70 mm in front; heading 74° → 179°. |
| StackBlocks | **ran, SUCCESS** | Two upright cubes in view, no face, Freeplay on at 15:14:55. Freeplay first ran FindFaces_socialize. At 15:15:18.31 it chose StackBlocks ("score of 0.87, so is interrupting … FindFaces_socialize which scored 0.00", activity PlayAlone). Steps: DriveToObject (type 10) FAILED_TRAVERSING_PATH at 15:15:23.99; retry SUCCESS at 15:15:26.27; pickup (type 22) SUCCESS at 15:15:34.07; DriveToObject to cube 3 SUCCESS at 15:15:36.65; TRACKS_LOCKED animation 3000355 at 15:15:38.69 (same as O3); PlaceOnObject (type 25) SUCCESS at 15:15:43.19; `anim_reacttoblock_admirecubetower_01` at 15:15:48.17; behaviour ended at 15:15:48.23. State: cube 1 z = 61.5 mm over cube 3 z = 16.7 mm, same x/y. |
| ReactToCubeMoved | **fired** | The operator lifted the powered top cube off the stack. cube-moved (axisOfAccel 5) at 15:16:05.16; reaction CubeMoved → ReactToCubeMoved at 15:16:05.16, interrupting InteractWithFaces. Two steps (SUCCESS at 15:16:06.29 and 15:16:07.37), then `anim_hiking_lookaround_03` (15:16:07.49–12.71). Reaction ended at 15:16:12.77. The cube was not re-seen. A second move at 15:16:27–28 got no reaction (cooldown? see Q2). |
| AcknowledgeObject (Freeplay on) | **fired, earlier in part 2** | 15:13:06.14: cube 1 seen → ObjectPositionUpdated → AcknowledgeObject → turn-to-object (type 44) SUCCESS at 15:13:08.93 → ended 15:13:08.99. Again at 15:13:54 (after PutDownBlock) and 15:14:14 (second cube placed; Freeplay off then). The stacked-cube branch (ACK-STACKED-LOOK-1) was not exercised. |

## Questions from part 2

- **Q1.** PutDownBlock's follow-up was cut by AcknowledgeObject re-seeing the block it had just put down. Does the
  engine's PutDownBlock lock or suppress ObjectPositionUpdated for its own block, or is the interruption
  engine-normal?
- **Q2.** The second cube move, 22 s after the first, produced no ReactToCubeMoved. Check the engine's per-object
  cooldown for the CubeMoved trigger.
- **Q3.** The powered cube dropped its radio twice (`connected:false`, at 15:12:13 and 15:16:19), then reconnected.
  After the drop, get_state listed a stale `worldObjectId 1` entry (connected false, old stacked pose) next to an
  unbound radio entry (worldObjectId null). Possibly a low cube battery. Is the duplicate entry a host projection
  artefact?
- **Q4.** Pickup/drive completions carry 0xFFFFFFFF in words 1–4: Freeplay DriveToObject and PickupObject, and the
  commanded pickup tag 2 at 15:13:41. Stack and turn-to-object completions carry 0, per the M8-015/M12-052/M15-077
  zero-fill policy. Is the pickup path's 0xFFFFFFFF the engine value, or an unfilled undefined word? The policy names
  turn-to-cube, ground placement and roll only.

## Robot behaviour seen while Freeplay ran (observations)

- Hiking_PounceOnMotion pounced onto the cube at 15:13:15–20.
- FindFaces_socialize found a face at 95 cm (15:15:56).
- InteractWithFaces was running when the cube moved.

**Addendum: AcknowledgeObject after the operator's move.**
- At 15:16:58.61, Freeplay was driving (Hiking); its compound 3000505 was cancelled.
- In the same tick: reaction ObjectPositionUpdated → AcknowledgeObject, for the cube the operator had moved.
- First step SUCCESS at 15:17:00.23.
- Before that, at 15:16:51.42, a Freeplay Hiking drive hit UnexpectedMovement (L 153 / R 57) right after a third
  cube-moved (15:16:50.93), which also got no ReactToCubeMoved (Q2).

# Part 3: physical reactions (15:17–15:21 UTC, same bundle as part 2)

| Round | Result | Evidence |
| --- | --- | --- |
| on-side (more than 15 s) | **fired** | off-treads InAir → OnLeftSide at 15:17:44.87; ReactToRobotOnSide at 15:17:44.87. `anim_reacttocliff_stuckleftside_03` ×2, then the WaitOnSideLoop trigger (shipped clips `anim_speedtap_wait_*`, the same mapping as the 2026-09-22 run log) looped until 15:18:28 (about 44 s). |
| on-face | **fired twice** | OnFace at 15:18:52.79 and 15:18:56.93; ReactToRobotOnFace both times, `anim_reacttocliff_faceplantroll_02`. |
| shaken | **fired** | ReactToRobotShaken at 15:19:05.93: `anim_dizzy_shake_loop_01` → `anim_dizzy_shake_stop_01` → `anim_dizzy_reaction_soft_01` (SUCCESS at 15:19:13.31); reaction ended at 15:19:13.37. |
| frustration | **fired** | ReactToFrustrationMinor at 15:21:12.23 after repeated Freeplay Hiking stalls (UnexpectedMovement at 15:21:07.25 and 15:21:10.67); `anim_reacttoblock_frustrated_int1_01` SUCCESS at 15:21:13.49. Also part 1: minor at 14:59:44, major at 15:00:14. |
| placed-on-slope | **not triggered** | Several placements between 15:19:44 and 15:20:25. Each produced RobotPickedUp → ReturnedToTreads only. See Q5. |

Also fired:
- ReactToRobotOnBack at 15:18:28.61;
- ReactToCliff with the firmware stop at 15:19:33.6 and 15:19:37.5 (`sensors:3`, `stoppedForCliff:true`) during Freeplay Hiking near the desk edge;
- ReactToPickup and ReactToReturnedToTreads on every handling.

**Q5. PlacedOnSlope.** PlacedOnSlopeStrategy.WantsToRun (`ReactionStrategies.cs:251`, cited 0x61455C..0x614688)
needs all of these:
- signed pitch in (+10°, +55°), with no absolute value;
- every gyro axis below 0.01 rad/s for more than 0.4 s;
- picked up now, or less than 1.5 s since;
- OffTreadsState < OnBack.

So only one tilt direction can fire. The operator's placement direction and angle are not recorded. Open:
- which physical tilt (nose up or nose down) gives positive PitchRad on this robot;
- whether quiet-gyro 0.4 s and put-down within 1.5 s can both be met on a hand placement (the gyro is not quiet
  until the hand lets go).

Next step: a placement facing uphill and one facing downhill, each at 20–30°. If neither fires, trace the pitch sign
and the timing against 0x61455C..0x614688. (The engine-log pitch is not in events.jsonl; frames.jsonl is raw.)

Also observed: Freeplay was off from about 15:19:40 to 15:20:24. Claude turned it off to keep him from the desk edge,
so between placements he sat in Wait and looked unresponsive to the operator ("he has stopped responding"). It was
turned back on at 15:20:24.

**Q5 evidence: decoded RobotState around the 15:22:08.99 placement.** The operator says the slope does not work.
Decoded from frames.jsonl with a scratch decoder: State 0xF0, pose pitch at body offset 28, gyro x/y/z at 60, status
at 76. Field order is from `RobotMessages.g.cs` RobotState.Read.

| UTC | pitch deg | max abs gyro (rad/s) | status |
| --- | --- | --- | --- |
| 15:22:08.94 (put down) | 9.0 | 0.381 | 0x8311 |
| 15:22:09.41 | 12.2 | 0.131 | 0x310 |
| 15:22:09.48 | 12.4 | 0.071 | 0x310 |
| 15:22:09.57 | 12.4 | 0.177 | 0x310 |
| 15:22:09.68 | **0.0** | 0.100 | 0x311 |
| 15:22:09.75–10.31 | 0.0 | 1.5–1.9 | 0x311 |
| 15:22:10.56–10.82 (at rest) | 0.0 | 0.017–0.043 | 0x310 |

What this shows:
1. **The pitch was in range** (about 12°, so this direction is positive) for about 0.3 s.
2. **The gyro never went below the 0.01 rad/s quiet threshold**, even at rest on the flat
   (0.017–0.043 rad/s). On this robot the quiet condition looks unreachable with the raw gyro values.
3. **The pitch then read exactly 0.0** from 15:22:09.68 onward while he was still on the slope, with the gyro at
   1.5–1.9 rad/s (ReturnedToTreads' head/lift actions at about that time). Exactly zero is suspicious.

Open, from the binary:
- Is +0x36C..+0x374 (the strategy's gyro) the raw RobotState gyro, or a bias-corrected/filtered value? The
  RobotGyroDriftDetector suggests a bias term.
- What makes the pose pitch exactly 0.0? Is it robot firmware (status bit change 0x8311 → 0x311), or the stack?

Until both are answered, PlacedOnSlope cannot fire on this robot. This is a finding to trace; do not tune the
threshold.

# Part 4: needs (15:23–15:25 UTC, same bundle)

- **15:23:15, set_needs_mode real:** reply keepFull false, loaded true, `applied:false`, paused false.
- **Every set_needs reply** also said `applied:false`, yet needs-changed confirmed each level in the engine
  (Q6).

| Step | Needs-changed (brackets) | Behaviour |
| --- | --- | --- |
| energy 0.20 (15:23:21) | 15:23:22.01: energy Critical | At 15:23:32.21, activity NeedsSevereLowEnergy → Needs_SevereLowEnergyGetIn ("first runnable in priority order"), `anim_energy_getin_01` (15:23:34.67–39.05). At 15:23:39.17, Needs_SevereLowEnergyState: `anim_energy_idlel2_01`, then `anim_energy_drivegetin_01`, `anim_energy_driveloop_01`, `anim_energy_drivegeout_01`. |
| repair 0.26 with energy still 0.20 (15:23:44) | 15:23:45.47: repair and energy both Critical | Stayed in the energy activity (energy has priority?) |
| energy 1, repair 0.26 (15:23:52) | 15:23:52.79: repair Critical only | The energy State ran on until 15:24:07.13 (about 14 s after energy recovered). Then at 15:24:07.19, activity NeedsSevereLowRepair → Needs_SevereLowRepairGetIn, `anim_repair_severe_getin_01` (15:24:09.41–18.29). Then Needs_SevereLowRepairState (loco driving animations). |
| repair 1, play 0.13 (15:24:20) | 15:24:21.17: play Critical | The repair State ran on until 15:24:43.79 (about 23 s after repair recovered). Then at 15:24:43.85, activity NeedsSevereLowPlayGetIn → Needs_SevereLowPlayGetIn; first animations unnamed (null), then `anim_play_getin_01` at 15:24:46.91. |
| all 1 (15:24:50) | 15:24:51.29: all Full | — |

Questions:
- **Q6.** `applied:false` on every needs reply, even though the engine applied the levels. Is it a stale or
  misleading host status?
- **Q7.** get_state `selectedSevereNeed` stayed null throughout, while the severe activities ran. Is the host
  projection missing the engine's selected severe need?
- **Q8.** After a need recovers, the severe State behaviour runs on for 14–23 s before the next one. Check the
  engine's exit condition for the NeedsSevere* activities (a strategy cooldown, or an end-of-behaviour check).
- **Q9.** Energy kept priority over repair while both were Critical. Is that the engine's activity priority order
  in the shipped freeplay config?
- **Q10.** Unnamed (`name:null`) animation starts at the start of the play GetIn.

Needs mode was left in **real** at the end of the session; it started in full. Needs now decay naturally.

# Part 5: faces (15:25–15:28 UTC, same bundle)

**F9. InteractWithFaces does not say the name of a recognised face.**
- face-recognised faceId 1 "Jim" at 15:27:31.37 (16.5 cm) and AcknowledgeFace 15:27:32.39–38.09.
- Freeplay was off and on at 15:27:38. InteractWithFaces started at 15:27:39.05 (Socialize, "face available for
  interaction").
- stack-log 22960..23053: `TurnTowardsLastFacePose` [3001602] failed **NO_FACE**, and the compound [3001604] failed
  NO_FACE. A second TurnTowardsLastFacePose [3001608] followed, then `TurnTowardsPose` [3001613] SUCCESS.
- Then `InteractWithFacesInitialUnnamed` → `ag_freeplay_reacttoface_unidentified` →
  `anim_reacttoface_unidentified_02_head_angle_40` (15:27:42.47–44.27).
- So the face InteractWithFaces selected had no last-face pose 8 s after recognition, and the named path (0xF8
  InteractWithFacesInitialNamed) was never reached.
- Trace: which face ID SelectFaceToTrack returned (a temporary −1/−2 track, or 1), and why
  TurnTowardsLastFacePose found no pose. Same area as the 2026-11-10 face-pose-reader fix.

**Earlier in the same Socialize activity:**
- InteractWithFaces ran once at about 15:15:56 (stack-log 6924–6927), on an unrecognised face 95 cm away
  (InitialUnnamed).
- After that the activity only ran FindFaces. NativeActivityObjectiveOwner phase 2 → 3 on InteractedWithFace
  (`NativeActivityObjectiveOwner.cs:194`, recipient 0x005B07C2): one interaction per Socialize activity. That
  matches the reproduced engine; not a defect.
- AcknowledgeFace ran 15:26:22.97–24.17 after recognition at 15:26:20.93.

**F3 recurrence (false PetInitialDetection):**
- ReactToPet at 15:26:04.49 (`anim_petdetection_shortreaction_02_head_angle_40`) and 15:27:15.77.
- The camera at 15:26:06 showed only the operator's face at about 17 cm, mostly beard. Earlier, at 14:54:40, he was
  also close.
- Lead: the pet detector fires on a close, bearded human face. Compare with the original's pet detector settings.

**Observation:** the Freeplay status at 15:27:38 showed activity **Singing**. BehaviorSinging is a "class not
implemented" MISSING at startup (stack-log 2192). What does the Singing activity run in the stack?

# Part 6: idle with Freeplay on (15:29:11 – 15:31:54 UTC; the robot disconnected)

Behaviours Freeplay chose by itself:

| Activity | Behaviours |
| --- | --- |
| Socialize | Needs_MildLowRepairRequest; InteractWithFaces at 15:29:22, 15:31:13 and 15:31:20; FindFaces_socialize |
| NothingToDo | NothingToDo_BoredAnim at 15:29:24.77 ("Bored", highest score 1.09) |
| Hiking | Hiking_FirstLookIntro (15:29:36, score 9.02), Hiking_PounceOnMotion, Hiking_VisitInterestingEdge |
| Reactions | ReactToCliff at 15:29:51.77 (`sensors:3`, firmware stop) |

Points to follow up:
- **InteractWithFaces repeats NO_FACE (F9).** Every run turned with TurnTowardsLastFacePose → NO_FACE: 15:29:23.75,
  15:29:24.53, 15:31:15.47 and 15:31:16.25.
- **The cube moved with no ReactToCubeMoved.** cube-moved / cube-tapped at 15:30:27, 15:30:53–55, 15:31:04 and
  15:31:33–37; it was seen at z = 136 mm at 15:31:34. Q2 (cooldown).

**F10. The robot disconnected.**
- At 15:31:54.62, `robot-disconnected` reason WifiTimeout (reasonCode 1), **timeSinceLastMsgSec 0**, which does not
  fit a timeout.
- The stack log shows nothing abnormal before `disconnect_reason WifiTimeout` (stack-log 27387). Last activity:
  Hiking_PounceOnMotion `anim_pounce_03`, about 15:31:42, plus the cube-search BlockFilter lines.
- Last battery read 3.87 V at 15:24:38. A low-battery event (3.29 V) at 15:05:35 in part 1, under stall load.
- Open:
  - was the robot powered off or browned out, or did the Wi-Fi drop? (operator to report the robot's state)
  - why does timeSinceLastMsgSec read 0?

**Q11. Repair drained fast.** Repair went from 1.0 (set at 15:24:51) to 0.30 at the disconnect, about 7 minutes,
with repeated Needs_MildLowRepairRequest. There were many cliff stops and UnexpectedMovement stalls in this window.
Check the engine's needs actions for repair decay per event against the shipped needs config.
