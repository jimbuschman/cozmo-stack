# Live MCP session with the operator's robot, 2026-11-19

Driven by Claude (Opus 5.5) through the `cozmo` MCP server, with the operator present. These are observations for
review, not hardware acceptance: no check here is an engine-sourced pass/fail.

Files in this folder: the four camera frames (`img*.png`, as returned by `get_camera_image`) and the event page
`events-cursor3-212.json` (read_events since=2, cursors 3..212).

Start state (15:31:17Z): connected, ready, battery 4.01 V, off charger, activity Selection, behavior Wait, needs
keepFull (all 1, paused). No faces, cubes or chargers known.

## 1. Camera: grey frames, head moves

| Time (UTC) | Action | Result |
| --- | --- | --- |
| 15:32:07 | get_camera_image annotate | img 4453, 320x240 grey, headAngleRad -0.427. Floor and own lift. No boxes, observationReportsAvailable=false. |
| 15:32:46 | head 0.1 | completed, SUCCESS. |
| 15:32:47 | get_camera_image annotate | img 5043, headAngleRad 0.088. Operator at keyboard, face cut off at top. |
| 15:32:51 | head 0.5 | completed, SUCCESS. |
| 15:32:52 | get_camera_image annotate | img 5112, headAngleRad 0.472. Operator's face clearly in view (three-quarter, backlit by lamp). **faces=[], observationReportsAvailable=false.** |
| 15:36:52 | get_camera_image annotate (cube placed) | img 8687, headAngleRad -0.034. **Cube box drawn** (worldObjectId 0, Block_LIGHTCUBE2, rect 51,62 105x98). observationReportsAvailable=true. |

The head reached the commanded angle each time (exposure angles 0.088 and 0.472 against 0.1 and 0.5).

## 2. Findings for review

**F1. No face box on a frame with a face in view, though faces were detected seconds later.**
img 5112 (15:32:52) had the operator's face in frame and no face box. A read_events wait of 5 s right afterwards
(since=0) returned only the two head action completions. But the event page later read shows `face-seen` faceId -1
starting at robot ts 158650 (15:33:30Z), `face-id-changed` -1 -> 1, sightings to 15:33:31, `face-lost` 1 at 15:33:46,
then a second face -2 seen 15:36:38..15:36:39 and lost 15:36:54. So face detection works; the frame at 15:32:52 may
simply have been before the first detection (about 38 s gap). Open question: why there was no detection for ~38 s
with the face in view: the backlit three-quarter pose, or how often face detection runs. Not established.

**F2. `knownFaces` empty in get_state although faces 1 and -2 had been seen.**
get_state at 15:36:54.93 shows knownFaces=[]. Face -2's face-lost arrived at 15:36:54.66, so it may have been removed
just before. Face 1 was also lost (15:33:46). Open question: does the engine drop a lost face from the world (so empty
is correct), or should state still list recently seen faces? Needs the engine's face-world lifetime, not a guess.

**F3. ReactToPet fired while a human face was being seen.** Operator to confirm whether a pet was in view.
- 15:36:36.97 cube-tapped (numTaps 2).
- 15:36:38.52..39.66 face-seen faceId -2.
- 15:36:39.44 reaction-changed 22 -> 10; behavior-changed 178 -> 80, displayKey `behavior.ReactToPet`.
- 15:36:40.02 animation-started `anim_petdetection_shortreaction_02_head_angle_20`.
- 15:36:42.77 action CANCELLED_WHILE_RUNNING (tag 3000007); reaction 10 -> 8, behavior `behavior.AcknowledgeObject`.
- 15:36:42.84..43.92 animation tag 2, name null; 15:36:43.98 tag 3 started (name null), 15:36:44.28 aborted.
- 15:36:44.28 action VISUAL_OBSERVATION_FAILED (tag 3000010, result 50331677).
- 15:36:44.40 action type 44 SUCCESS; back to behavior 178, reaction 22.
If no pet was present, look at what fed the pet detection. If one was, this is correct behaviour.
The reaction also explains why the head was at -0.034 at 15:36:52 rather than 0.5.

**F4. animation-started / aborted with `name: null`** for tags 2 and 3 in the AcknowledgeObject reaction. The guide
says name may be null when the association is unknown; noted in case the name should be known here.

**F5. AcknowledgeObject ended with VISUAL_OBSERVATION_FAILED** while the cube was being seen continuously
(cube-seen from 15:36:42.77 onward). Possibly correct (cube moved: cube-moved at 15:36:42.96, moving=true), not judged.

**F6. read_events is unusable for an LLM client over a few seconds of cube sighting.** read_events since=2 returned
210 events, 115 KB (152 cube-seen, 36 face-seen in ~15 s of sightings), which overflowed the MCP client's result limit
and had to be read from disk. No server-side filter or limit exists (parameters are only since and waitMs). Usability,
not fidelity: a `types` filter or `limit` would fix it.

**F7. Colour camera not reachable through MCP.** All frames are grey. get_camera_image has no colour option and the
stack has no request that turns colour on (grep finds no EnableColorCamera sender in `cozmo-stack/src`), though
`Camera.cs` / `EncodedImageDecoder.IsColor` decode colour encodings. AGENTS.md lists the colour camera format as
HARDWARE_ONLY. Check whether colour is in SCOPE.md before queueing.

**F3 update:** operator confirms **no pet was present**. ReactToPet fired on a human face (or on something else in
view). Lead to trace: what fed the pet-detection reaction (trigger 10) at 15:36:39.

**F8. PlacedOnCharger reaction holds and blocks every external action for over a minute.**
The operator put him on the charger at about 15:42:15 (cube object-pose-lost at 15:42:14.70).
- get_state at 15:42:23, 15:42:27 and 15:43:17: isOnCharger true, isCharging true (battery 4.67-4.68 V), charger
  worldObjectId 2 created at 30 mm with lastObservedTimestamp 0. behavior ReactToOnCharger, reaction PlacedOnCharger,
  unchanged throughout.
- head 0.45 (15:42:22) and drive-straight 100 mm (15:43:08): both not-admitted, "External actions are disabled by a
  reaction". Event feed shows each as action-completed NOT_STARTED (tags 3, 4).
- **No reaction-changed or behavior-changed event for PlacedOnCharger** appears after cursor 2164 (15:42:14), though
  get_state reports that reaction. Either the change was never published as an event, or state reads it from elsewhere.
Open questions: how long the engine's ReactToOnCharger holds reactions/blocks actions while on the charger (it may be
correct engine behaviour that only ends when he leaves the charger); and why no event announced it.
Camera during this time (img 13704, 15:42:29): desk view, head -0.034, operator leaning in; no boxes.

**F8 update:** the operator lifted him off at 15:43:50: off-treads-changed OnTreads->InAir, reaction-changed
**9** -> 12 (ReactToPickup), so trigger 9 (the charger reaction) held until pickup; behavior id 79 was the charger
behavior. Then ReactToOnTreads (trigger 14) and back to 22 at 15:43:51. The charger dropped out of knownChargers
after leaving it (get_state 15:44:01). The event announcing trigger 9 lies in cursors 1143..2160, not inspected
(too large to read through MCP, see F6).

**F9. Repeated Hiccup reactions right after being put down.** reaction 22->5, behavior.Hiccup:
`anim_hiccup_02` 15:43:58.68..15:44:02.64, then `anim_hiccup_01` 15:44:06.30..15:44:08.46, about 8 s apart, plus an
unnamed animation tag 11 at 15:44:03.48. Each blocks external actions (head at 15:44:07 not-admitted, NOT_STARTED
tag 5). Possibly the engine's hiccup state, not judged; the trigger condition is the lead.

## 4. Face test, operator facing him, ~35-50 cm (15:44)

- 15:44:12.66 head 0.45 SUCCESS (tag 6).
- 15:44:13.74 face-seen faceId -1 within ~1 s; rect ~100x100 at (96,26); 15:44:13.92 face-id-changed -1 -> 2.
- 15:44:17 get_state: behavior AcknowledgeFace, reaction FacePositionUpdated; head moved up to 0.747 (exposure angle
  of img 15304), i.e. he looked up at the operator himself.
- **F2 answered in part:** knownFaces is populated while a face is being seen. Earlier empties followed face-lost.

**F10. Annotation drew no face box on a frame whose timestamp matches a face observation. Likely a real bug.**
img 15304: robotTimestamp **809385**, faces=[], observationReportsAvailable=**false**, operator's face full-frame.
get_state 0.1 s later: knownFaces[faceId -3].lastObservedTimestamp = **809385**, the same robot timestamp. So a face
report for exactly that image exists, but get_camera_image's timestamp matching did not find it. The cube box did
match (img 8687, F-section 1). Lead: the face side of the annotation's report lookup (which reports it indexes, or
the timestamp it keys faces on). Frame saved as img15304-face-no-box.png.

**F11. One person, two face IDs at once.** knownFaces at 15:44:17: faceId -3 (347 mm, bearing -0.4 deg, observed
809385) and faceId 2 (487 mm, bearing -6.0 deg, observed 809115), both unnamed, unenrolled. The engine can hold a
second track before merging; not judged. Watch for a face-id-changed merging them.

**F12. Face landmark layout looks rotated.** face-seen payloads (cursor 2194..2196): mouth points y~60-68 sit above
leftEye y~62-69 and well above nose y~102-130; rightEye at y~88-99. In image coordinates with an upright face the
mouth should be lowest. Might be a coordinate convention (or a misordered field mapping); check the serializer/CLAD
field order against the engine.

**F13. Cube dropped to disconnected.** get_state 15:44:17: the cube shows connected=false, worldObjectId null
(it had been connected since session start). Not investigated; the operator may have moved it out of range.

**F14. The robot disconnected 330 s after being placed on the charger, though he had been taken off it. Highest
priority: it ends the session.**
Session evidence: `re-analysis/acceptance/hardware/20261119-093050-LLM-SESSION/` (events.jsonl, stack-log.txt).
- events.jsonl line 23726/23727, 15:40:07.718: `robot.behavior_transition: ReactToOnCharger`,
  `robot.reaction_trigger: PlacedOnCharger`. (Correction to F8: placement was 15:40:07, not ~15:42:15.)
- 15:43:50 picked up off the charger (RobotPickedUp), on the desk 15:43:50.7.
- events.jsonl line 40131, **15:45:37.739: `info: disconnect_reason SleepPlacedOnCharger`**, exactly 330.0 s after
  the reaction started; then RobotManager.RemoveRobot and the whole Robot teardown (stack-log.txt ~8067 on).
- Code path: `ReactToOnChargerBehavior.OnStart` (`cozmo-stack/src/Cozmo.Robot/Behavior/ChargerBehaviors.cs:257-258`)
  sets Rcm.Reason = SleepPlacedOnCharger and calls `robot.Idle.Start(TimeTilSleepSec=300, TimeTilDisconnectionSec=330)`
  (defaults at :207). `IdleTimeoutComponent.Update` (`CozmoEngine.cs` ~805) then calls Handler.Disconnect at the
  deadline. The only cancel is the CancelIdleTimeout message 0x54 (`RobotSubscriptions.cs:127`), an app-to-engine
  message. Nothing sent it when he left the charger. OnStop of the behavior unsubscribes the cancel handler but does
  not cancel the timeout.
- Lead (not established): in the original the Unity app sends CancelIdleTimeout (and StartIdleTimeout) around the
  charger/sleep flow. The stack replaces the app, so per "Scope: the app boundary" this is an app-supplied input the
  stack must supply from its own host logic, recorded as a COMPATIBILITY_POLICY. Needs: which app code sends 0x54 and
  when (authority 2), and whether the engine itself cancels it anywhere on leaving the charger (authority 1).
  The 300 s face-off deadline (15:45:07.7) would also have queued go-to-sleep; check what that did.
- Also: the MCP event feed shows **no disconnect event**; the last event is 15:45:25.74. get_state just reads
  connected=false. An LLM client has no event telling it the robot went away or why. Usability/API gap.
- Related MISSING markers hit in this run: stack-log 2231 `BehaviorContainer::CreateBehavior case 0x59D1B2
  (ReactToOnCharger ... constructor 0x606A18): class not implemented`; 4823 `ReactToOnChargerBehavior: vtable+0x24
  gate ... not transcribed; this class answers true`; 4824 `M7-019 GoingToSleep E2G 0x2D typed host recipient is not
  bound`. These bear on F8 (the reaction holding and blocking actions for its whole stay on the charger).

**F3 reproduced:** PetInitialDetection fired again at 15:44:14.10, the same instant face-seen began for the operator
(no pet present). Two of two first-face sightings in this session triggered the pet reaction.

**F9 note:** the hiccups also ran before the charger (Hiccup reactions 15:39:57 and 15:40:02) and ended with
`anim_hiccup_selfcure_getout` at 15:44:25..15:44:33.

**F11 resolved:** face-id-changed -2 -> 2 (15:44:15.36), -3 -> 2 (15:44:17.76), -1 -> 2 (15:44:18.66): the extra
tracks merged into face 2 within seconds. Looks normal.

**Pickup at 15:45:23:** RobotPickedUp, then InAir -> **Falling** (15:45:23.70), `anim_reacttocliff_pickup_01`,
MotorCalibration and RobotFalling reactions, back OnTreads 15:45:24.55. Operator to say whether he was dropped or
just set down quickly.

## 5. After MCP restart (new session folder under re-analysis/acceptance/hardware/, started ~15:48)

- 15:48:45 connected, ready, on charger and charging, **no PlacedOnCharger reaction** (he started up on it), charger
  worldObjectId 0 at 30 mm.
- 15:48:48 drive-straight 120 mm @ 50: SUCCESS, with anim_hiking_driving_start_02 / loop_02 (aborted at end, as
  expected) / end_01. Charger then at -88.2 mm, bearing 178.7 deg: ~118 mm driven. isOnCharger false.
- 15:48:56 head 0.45 SUCCESS. 15:48:57.77 face-id-changed -1 -> 1.
- **F10 reproduced:** img 19527 (robotTimestamp 26100) had the operator's face, faces=[],
  observationReportsAvailable=false. The face-seen reports around it are at robot ts 26165, 26235, 26300... (65-70 ms
  apart), none at 26100. In the first case (img 15304) the state's lastObservedTimestamp did equal the image's
  timestamp. So either face reports carry a timestamp one frame after the image they came from, or the matching
  index misses them. Both cases still need the lookup read.
- 15:49:01 turn-to-face faceId 1, max 3.14 rad: **SUCCESS, report.facePoseAvailable=true**, completed in ~1 s.
  He was already roughly facing the operator. Note face-seen payloads during the turn with empty
  leftEye/rightEye/nose/mouth (robot ts 26570..26705, rect 152x152): tracked-only reports, plausibly normal.

- **Operator observation on turn-to-face:** "i never saw him follow my face". Expected: turn-to-face is a single turn
  to the current pose and he was already nearly facing the operator; continuous following is track-face (not yet run).

## 6. Face enrollment "Jim" (15:50:03..15:50:33)

Full tool result saved as `enroll-face-result.json` (333 KB; too large for the MCP client, F6 again: the `during`
page carried 289 events, 185 of them face-seen).
- Result: status completed, engineResult 0 Success, detail "Face enrolled.", report {result 0, id 1, name Jim},
  faceOperation enroll/enrolled faceId 1 name Jim. Operator: "worked".
- Sequence: behavior.LookingForFaces -> enroll behavior (id 12); face-id-changed -1 -> 1 at 15:50:07.49;
  anim_meetcozmo_lookface_getin_head_angle_40, anim_meetcozmo_lookface_02 (aborted 15:50:15.32, when enrollment
  finished scanning), lookface_getout, anim_neutral_eyes_01, anim_meetcozmo_sayname_01/02_head_angle_40,
  anim_meetcozmo_celebration_02_head_angle_40; back to behavior 178 at 15:50:33.32.
- First face-recognised (faceId 1, name Jim) at 15:50:15.35; 71 face-seen reports carried name "Jim" afterwards.
- get_state 15:50:48: knownFaces has faceId 1 name "Jim" enrolled=true.
- **Observation:** after enrollment, activity/highLevelActivity = **MeetCozmo**, behavior
  MeetCozmo_FindFaces_Socialize (it was Selection before). Not judged; check whether the enroll flow should leave the
  activity there or return to the prior freeplay activity.
- **Observation:** knownFaces also holds three unnamed faces at the same time: -5 (556 mm, 20 deg), -4 (341 mm),
  -1 (140 mm). Likely the operator's face re-tracked before recognition; watch whether they merge into 1.
- **Operator to report:** did he speak the name "Jim" aloud during the sayname animations? (TTS/audio check.)

## 7. track-face Jim (faceId 1), ~15:51:10..15:51:33

Session evidence now `re-analysis/acceptance/hardware/20261119-094833-LLM-SESSION/`.
- Result: completed, **engineResult CANCELLED_WHILE_RUNNING**, report.faceId 1, isError. Cancelled by the freeplay
  side, not the caller: at 15:51:33.05 behavior.InteractWithFaces (id 45) ended -> 178 -> LookingForFaces (44); action
  tag 4 (actionType 35, the track) completed CANCELLED_WHILE_RUNNING at 15:51:33.11. Ran ~20 s.
- face-seen/face-recognised name "Jim" throughout; operator asked whether he followed (pending).
- The tool result was again too large for the client (F6): ~100 KB of `during` events.
- Note: while MeetCozmo activity ran, its own behaviors (LookingForFaces, InteractWithFaces,
  MeetCozmo_FindFaces_Socialize) kept selecting; an LLM-driven track competes with them. Whether an external
  track should be cancelled by the activity's next behavior is a question for the engine's action-ownership rules.

**F15. Music keeps playing after enrollment (operator: "the music hasn't stopped playing though").**
stack-log: SetFreeplayMusic 'Selection' (2255) and 'MeetCozmo' (3015/3017) both log
`UpdateActivityMusicState.NoFreeplayMusic`, so the engine's freeplay-music path did not start it. Source not yet
identified. Candidates: an audio event inside the enrollment's animations (anim_meetcozmo_lookface_02 was aborted at
15:50:15.32, stack-log 3450-3456, `audioComplete=1`), or the say-name / celebration audio. Operator to say: robot
speaker or PC, and when it started.
**Operator answer: the music plays through the PC**, not the robot speaker, and was still playing at ~15:53. So the
host-side (phone-equivalent) Wwise output started a music/loop event during or after enrollment and nothing stopped
it. In the original that audio would play on the phone; the app (Unity) owns most music start/stop. Lead: find which
game-object/event was posted to the host output around 15:50:03..15:50:33 (the enroll flow, MeetCozmo activity
entry), and what in the engine or app stops it. Also whether the stack should play phone-side music at all
(app-boundary scope question for the operator).

**Track-face, operator: "i think i saw him tracking me"**, i.e. the head/body followed during the ~20 s it ran.
Observation, not a sourced pass.

## 8. Cube actions (15:53..15:55), cube worldObjectId 1

**F16. With the MeetCozmo activity running, an external head command waited ~30 s and was then cancelled.**
head -0.2 submitted ~15:53:23; result CANCELLED_WHILE_RUNNING (action tag 5) at 15:53:53.51, when
behavior.InteractWithFaces (45) ended -> 178 -> LookingForFaces (44). The activity's face behaviors kept starting
their own actions (anim_reacttoface_unidentified_01_head_angle_40, anim_pause_idle_*). For an LLM body this is the
central control question: how external actions share the robot with freeplay/activities in the engine. Freeplay was
then turned off (15:53:58, `submitted`), after which every command ran at once.
Also: after enrollment the activity stayed MeetCozmo for ~3.5 min until freeplay was turned off.

- Freeplay off: activity Selection, behavior Wait.
- **F10 also hits cubes:** img 24059 (robot ts 332785) has the cube in full view, objects=[],
  observationReportsAvailable=false; the nearest cube-seen is ts 332720, one frame earlier. The working cube box
  (img 8687) remains the only box seen all session.
- poseState stayed **Dirty** after fresh sightings (lastObservedTimestamp 332720) until the pickup; Known after.
  Not judged.
- **pickup 1:** SUCCESS 15:54:11..15:54:24; driving anims, approach sightings, anim_reacttoblock_lifteffortpickup_01.
  State: isCarrying true, carriedObjectId 1, cube relative (31.7, 0, 79.1) mm. Camera then looks past the raised cube.
- **put-down:** SUCCESS; isCarrying false, cube 79 mm ahead at z 20. **API inconsistency:** put-down's result has
  admitted=null, detail=null, ownerRetired=null (other action tools fill these), and its `during` page holds no
  action-completed event.
- **roll 1:** SUCCESS 15:54:38..15:54:46, anim_reacttoblock_lifteffortroll_01. Cube quaternion went from
  (0.847, 0, 0, -0.532) (upright) to (-0.600, -0.600, 0.373, 0.373), topOrientationRad -2.69 -> 2.03: it is on a
  different face, i.e. it rolled. Operator to confirm visually.
- **stack:** not tested at first: only one cube (LIGHTCUBE2) is connected.
- **Unpowered cube (15:56):** operator placed an unpowered LIGHTCUBE1. It appears in knownCubes as
  worldObjectId 3, factoryId null, radioObjectId null, connected false, poseState Known, 144 mm ahead; the annotated
  frame img 26236 drew its box (second box all session, robot ts 479965 matched). Matches BlockWorld.cs:736/2180
  (unconnected observed active object is kept, with a 10 s-cooldown "Is the battery plugged in?" warning).
- turn-to-cube 1 (on its side after the roll): SUCCESS.
- **pickup 1 while on its side: DID_NOT_REACH_PREACTION_POSE (67108865).** why.snapshot: planner Ready ->
  ComputingPath -> CompleteWithPlan -> FollowingPath -> Ready; checkAvailable true but no comparison captured beyond
  the planner states. driving_start anim CANCELLED_WHILE_RUNNING then driving_end. Lead: pre-action poses for a cube
  on its side (topOrientationRad 2.03, quaternion (-0.60, -0.60, 0.37, 0.37)); whether the engine offers pickup
  poses for that orientation. Not judged; operator asked to stand it upright.
- After the operator stood cube 1 upright: reaction CubeMoved (trigger 1) blocked turn-to-cube (not-admitted);
  then a second turn-to-cube was CANCELLED_WHILE_RUNNING by an AcknowledgeObject reaction (trigger 8) at
  15:57:17.46, **with freeplay off**: reactions stay live and interrupt external actions. Expected? Not judged.
- **pickup 1 (upright): SUCCESS** 15:57:22..15:57:34. Driving anims were the `anim_driving_upset_*` set this time
  (earlier: `anim_hiking_driving_*`). Mood-dependent selection? Not judged.
- **stack 1 onto unpowered 3, first try: DID_NOT_REACH_PREACTION_POSE.** why.snapshot.poseCheck:
  offset (22.0, 25.8) mm vs threshold (36.7, 18.3) mm, yaw not tested, inPosition false, `superseded: true`.
  During the approach cube 3's pose jumped from (432.1, 28.0) to (463.6, 36.2) world mm (~32 mm) when seen close
  (robot ts 549225); the check ran against the new pose. Unpowered cubes cannot report being bumped (no radio), so a
  far-pose error and a nudge are indistinguishable here. Whether the native place-on-object action retries itself
  after a pose update is a lead.
- **stack retry: SUCCESS** 15:57:49..15:58:03. Cube 1 now at relative (95.8, -1.4, **65.4**) mm, cube 3 at
  (91.9, 0.2, 18.6) mm: stacked (44 mm cube height + ~3 mm). Camera img 27624 shows the two cubes stacked
  (no box drawn: F10 again). **Stacking onto an unpowered cube works.** Cube 3 became Dirty (its markers covered).
  Inside the stack run: an animation action tag 3000750, animationName "", resolved **TRACKS_LOCKED** (50331673) at
  15:57:58.97; the stack still succeeded. Note for review.
- All three cube action results were 30-110 KB of cube-seen events (F6).

## 9. say-text "Hi Jim! I stacked the cubes!" (15:58:5x..15:59:15)

- Try 1: CANCELLED_WHILE_RUNNING (action tag 1, actionType 32) at 15:58:53.33 when the operator moved cube 1 and
  reaction ObjectPositionUpdated (8) / AcknowledgeObject started. Freeplay off.
- Try 2 (~15:58:55): **not-admitted**, "Speech action was not admitted" (NOT_STARTED tag 2): the reaction was still
  running. Note the detail text differs from the motion tools' "External actions are disabled by a reaction".
- Try 3: **SUCCESS.** Call ~15:59:01.7; `SayTextAnimation` started 15:59:11.04, ended 15:59:15.29. **~9.4 s from
  call to first audio**: TTS generation latency. Operator to say whether it was heard and how it sounded.
- The result was 163 KB (F6): 378 events, almost all cube-seen at ~15 per second for the two stacked cubes.
- **F17. Cube radio identity split.** get_state 15:58:56: knownCubes shows LIGHTCUBE2 factoryId 605720300 with
  connected false and worldObjectId null, and separately worldObjectId 1 LIGHTCUBE2 with factoryId null, connected
  false, pose Known (the cube on top of the stack). The powered cube lost its radio link (or its link to its world
  object) after the operator moved it at 15:58:52 (cube-moved, then object-pose-lost 1). Not investigated.

- Operator: **heard the speech** (try 3).

## 10. Operator question: queue rather than interrupt?

Operator: "i wonder if we should be queueing things rather than interrupting? idk."
Two separate layers:
1. **Engine (reproduced, not designed):** reactions locking out / cancelling external actions, and the action queue
   positions (QueueActionPosition: NOW, NEXT, AT_END, NOW_AND_CLEAR_REMAINING, IN_PARALLEL, present in
   `Cozmo.Robot/Actions/ActionList.cs`). Lead: confirm that today's cancellations (F16, the turn-to-cube and
   say-text cancels by AcknowledgeObject/CubeMoved) follow the native reaction-lock rules and not a stand-in (the
   charger reaction's lock is MISSING, F8), and which queue position the MCP tools submit with.
2. **MCP layer (app replacement, free to design, needs operator decision):** today a refused command returns
   not-admitted and the LLM must poll events and retry (done by hand three times this session). Proposal: an
   optional "wait until admitted (timeout N s)" on action tools, queueing host-side only, no engine change.
   Manager recommendation: build it. Operator to decide.

## 11. Animations and basic motion (16:00..16:01), freeplay off

- **animation MajorWin:** SUCCESS, report.animationName `anim_memorymatch_solo_successgame_player_01`,
  playbackTag 203, truncatedByRobot false, no playbackIssues; 16:00:30.35..16:00:32.09. The clip is in the shipped
  group `assets/animationGroups/MemoryMatch/ag_majorwin.json`.
- **animation CodeLabDancingMambo:** SUCCESS, `anim_dancing_mambo_01`, 16:00:41.45..16:00:59.39 (~18 s), not
  truncated, no issues.
- **lift 92 mm, lift 32 mm:** both SUCCESS. **turn-in-place +pi/2, -pi/2** (2 rad/s, 10 rad/s^2): both SUCCESS,
  relocalizationCount 0.
- **F3 again:** PetInitialDetection at 16:00:08.81 (no pet). Third occurrence.
- **Delocalization chain at 16:00:38** (session events.jsonl): OnTreads -> InAir -> OnTreads -> InAir ... within
  ~0.4 s; Robot.Delocalize to Origin2, Origin3, Origin4 (later 5); "DeleteObjectsFromZombieOrigins: Deleting
  objects from Origin 1"; RobotPickedUp / ReturnedToTreads reactions. Result: knownChargers empty, cubes 1 and 3
  gone, face Jim relative null. Also 15:59:15/19 `VisionComponent.SetNextImage.OriginMismatch` while localizing to
  cube 1, and 16:00:32 `LocalizeRobotToNothing` (cube 1 about to be cleared). Operator to say whether he was picked
  up at 16:00:38; if not, the rapid InAir/OnTreads flips are a lead (cliff/pickup sensing).
- **Operator, ~16:01: "not hearing any music, just fyi".** Pending clarification: either the mambo played without
  its music on the robot speaker (animation audio path; speech audio did work at 15:59:11), or the PC music from F15
  has stopped.
- **F18. Operator: "cozmo isn't making music". The mambo danced in silence.** Session log: both clips were
  played as `sound+face+head+lift+body`; RobotAudioKeyFrame.GetAudioRef selected 8 audio refs for
  anim_dancing_mambo_01 (16:00:41.03) and 3 for MajorWin (16:00:29.87). So the audio keyframes were chosen; the loss
  is later: Wwise event post, the robot audio stream (22320 Hz / 744-byte chunks), or routing. Lead to check first:
  whether the mambo's music event is a robot-speaker event or a phone-side Music-bus event in the original (the F15
  PC music shows host-side music does play, yet nothing was heard from the PC either). Speech (SayText) audio did
  reach the robot at 15:59:11.
  Also: anim_dancing_mambo_01 is authored 15.4 s but ran 17.9 s (16:00:41.45..16:00:59.39). Lead: the streamer
  waiting on audio that never completed, or the timing of the audio stream.
  Operator to say whether MajorWin made sound.
  **Isolation test 16:02:36:** SoundOnlyLiftEffortPickup (`anim_reacttoblock_lifteffortpickup_01`, SUCCESS):
  **operator heard the sound from Cozmo.** So animation audio does reach the robot speaker; the loss is specific to
  the mambo's music (and possibly other music events). Strongest lead: the mambo's music is a phone-side/Music-bus
  Wwise event in the original, which the stack posts to a host output that plays nothing (or nowhere).

## 12. Wheelie (16:03..16:04)

- Cube 1 back: connected true, Known, 165 mm at +12 deg (the F17 split resolved itself after the operator handled
  the cube). Frame img 32783 had the cube, no box (F10).
- **F19. wheelie 1 returned SUCCESS but the robot ended up on his back.** Call ~16:03:57; hiking driving anims;
  last event cube-tapped 16:04:07.73 (lift contact). get_state 16:04:12: behavior **ReactToRobotOnBack**, reaction
  **RobotOnBack**; camera img 33069 shows the ceiling and wall. The native result did not reflect that he fell over.
  Leads: what the native PopAWheelie action checks for success (pitch threshold only?), and whether the stack's
  lift/drive timing in the wheelie overshoots. Not judged; the original may also tip over on some surfaces.

- 16:04:27: righted by the operator, reaction cleared. Charger search for end of session: three +90 deg turns
  (all SUCCESS, relocalizationCount 0), charger not seen (knownChargers empty; it was forgotten at the 16:00:38
  delocalization). Cube 1 dropped to disconnected again (F17).
- **F20. mount-charger 0: FAILED_TRAVERSING_PATH (67108866)** 16:05:23..16:05:28. Operator placed the charger
  in view: charger worldObjectId 0, 124 mm ahead, bearing 1.3 deg, img 34091 (no box, F10). why.snapshot: planner
  Ready -> ComputingPath -> CompleteWithPlan -> FollowingPath -> Ready, no pose comparison captured. Final action
  tag 29 subActionResults [CANCELLED_WHILE_RUNNING, NOT_STARTED, FAILED_TRAVERSING_PATH]. Charger sightings are
  noisy: first one (robot ts 1013145) has a tilted pose (q1 0.11, q2 0.068, z +12.4) and later ones z -10.6 .. -1.5
  mm, x/y shifting ~10 mm. originId is now **7** (origins 6 and 7 came from the wheelie tip-over / righting).
  Lead: why the path following failed at ~124 mm with a plan in hand (path traversal error source), and whether
  the tilted first sighting fed the predock goal.

## 3. What worked

- Connection, readiness, battery and needs status in get_state.
- head action: native SUCCESS, angle confirmed by frame metadata.
- get_camera_image with annotation, including the exact-timestamp cube box.
- Cube: radio connect (before sighting), sighting -> poseState Known, relative pose 167 mm at bearing 7.9 deg,
  cube-tapped and cube-moved events.
- Face detection and face-id-changed (-1 -> 1), face-lost.
