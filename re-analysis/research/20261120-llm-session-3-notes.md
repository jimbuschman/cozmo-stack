# Live MCP session with the operator's robot, 2026-11-19 evening (UTC 2026-11-20)

Driven by Claude (Opus 5.5) through the `cozmo` MCP server, operator present. Observations for review, not
hardware acceptance. Previous session: `.scratch/20261119-mcp-live-session/NOTES.md` (findings F1..F20).

Stack: main at d2397461 ("camera annotations bound to the frame vision consumed"). Server session evidence:
`re-analysis/acceptance/hardware/20261119-182433-LLM-SESSION/`.
New MCP tools since the morning: `set_camera_color`, `lock-reactions`, `unlock-reactions`. get_state now has
`reactionLocks`; camera metadata now has `isColor`, `encoding`, `visionProcessed`, `latestReceivedImageId`; action
results' `during` now has `sightings` (compact by default).

## Summary for review (written at end of session)

**Confirmed fixed since the morning session (d2397461):** colour camera (encoding 9); face and cube annotation
boxes (F10); upright face landmarks (F12); compact `sightings` / `continuation` in results (F6); the 330 s
charger idle disconnect after pickup (F14, via the PauseManager host replacement); put-down result fields;
turn-to-face report detail; `going-to-sleep` event; `reactionLocks` in state.

**Open, in suggested priority order:**
1. **F23 docking: charger pose frozen/tilted** (sections 9, 11). mount-charger failed 4/4 across both sessions.
   Sightings during an approach carry the identical first (tilted, raised) pose. Manual docking with primitives
   succeeded (section 12), so motion and odometry are fine; the charger pose path is the suspect.
2. **F22 marker detection stops** after an UnexpectedMovement-cancelled turn while faces keep working; it resumed
   only after freeplay/motion (section 8). MISSING M11-091 "markerless" at the moment it stopped.
3. **Speech latency 9-17 s**, growing with text length (sections 7, 8). The single biggest obstacle to Cozmo
   feeling conversational as an LLM body. Compare with the original's TTS timing.
4. **F8/F21 charger reaction** refuses external actions for its whole run and only ends on a following reaction
   (pickup). Operator expects to drive on and off the charger. Read BehaviorReactToOnCharger in the binary
   (class not implemented; gate stand-in).
5. **Charger detection range** much shorter than cubes' (~20 cm vs 27+ cm) and mostly while moving (section 11).
6. **F3 pet detector false positives:** the operator's small blue toy robot triggers PetInitialDetection reliably
   (sections 8, 10) and the reaction holds actions for as long as it stays in view.
7. **Operator wants (low priority):** an MCP tool for backpack and cube lights (section 6).
8. Unexplained: the large turn-sum mismatch in section 1 (a controlled 2 pi turn was ~2.6% over, section 4).

**From the LLM-as-body side (Claude's view, for the MCP design):**
- What worked best: compact results, colour, boxes on frames, the `why` snapshots, `reactionLocks`.
- Still hard: I only perceive by polling. A way to be woken by significant events (reaction started/ended, new
  face/object, contact, disconnect) would remove most of the guessing between turns.
- Refused commands still need manual wait-and-retry (morning section 10 proposal: optional "wait until admitted").
- `visionProcessed` is easy to misread as "vision is off" (section 7 correction); a clearer name or doc line would
  help.

Start state (00:25:20Z): connected, ready, battery 4.02 V, off charger, Selection / Wait, no reaction,
reactionLocks [], needs keepFull. knownFaces: Jim (faceId 1, enrolled, no sighting yet). No cubes, no chargers.

## 1. Colour camera (00:25)

- `set_camera_color enabled=true`: status **confirmed**, report {enabled: true}. (admitted/detail null.)
- img 1386 (00:25:01.93): **isColor true, encoding 9**, 320x240, head -0.427. Colour desk (orange-brown wood) and
  the lift arms. visionProcessed true. Correction to the morning's F-section 1: the "tray edges" were the lift arms.
- head 0.3 SUCCESS; img 1676: operator at keyboard, colour, face in view upper part; faces=[],
  observationReportsAvailable false (face partly cut off at the top; no conclusion on F10 yet).
- Pan: three turn-in-place +1.2 rad (1.5 rad/s), all SUCCESS, relocalizationCount 0. Frames 1736 (fish tank,
  blue curtains, monitor), 1802 (wall of framed pictures, window), 1862 (shelves, figures, cables). All isColor
  true, encoding 9, no boxes (none expected). Each turn's `during` was small (3 events, sightings []): **F6 looks
  improved** for non-sighting periods.
- Colour latency: frames arrive with latestReceivedImageId = imageId + 1 or +2, i.e. the returned frame is the one
  vision consumed, one or two frames behind the newest received.

- **Heading observation:** commanded turns since img 1676 (facing operator) summed to +5.1 rad (3 x 1.2 + 1.5) and
  then -1.2 rad, net +3.9 rad, yet he faced the operator again at img 2447 (operator at about the 1676 heading). A
  net +3.9 rad should have left him ~137 deg away. Either the turns under-rotated on the desk, or one frame was taken
  before a turn settled. All turns reported SUCCESS with relocalizationCount 0. Not judged; a controlled check
  (mark the desk, one 2*pi turn) would settle it.

## 2. Face box and recognition (00:26)

- **F10 looks fixed on main d2397461.** img 2447 (colour, robot ts 97950): green face box rect (148,23,106x106),
  faceId -1, **observationReportsAvailable true**; the face-seen report in the turn's `sightings` has the same
  robot timestamp 97950. Frame saved: img2447-color-face-box.png.
- **F12 looks fixed:** landmarks now upright in image coordinates: eyes y 23..68, nose y 73..90, mouth y 97..118.
- **F6 improved:** action results carry `sightings` grouped by type with a count, first/last cursor and timestamps,
  and only the last payload (e.g. face-seen count 15). read_events also returns grouped sightings.
- 00:26:14.01: face-id-changed -1 -> 1, **face-recognised faceId 1 name "Jim"** (~0.8 s after first sighting);
  reaction 22 -> 2, behavior.AcknowledgeFace; no PetInitialDetection this time (F3 not reproduced on this
  sighting). A second track -2 appeared at the end of the window (rect 192x192, close-up).

## 9. Docking attempt (00:40:4x)

- get_state 00:40:36: charger 0 Dirty, lastObserved 945420 (only the 00:40:16 sighting), 260 mm at -19 deg, though
  in frame in img 15156. Unpowered cube 5 Known and repeatedly seen.
- **F23. mount-charger 0: VISUAL_OBSERVATION_FAILED (50331677)** 00:40:42..00:40:46.88. Planner computed and
  followed a path; final step failed visual verification. subActionResults [0, CANCELLED_WHILE_RUNNING,
  VISUAL_OBSERVATION_FAILED]. During it: cube-seen x6, **charger-seen x2** (robot ts 974360, 974710).
- **Charger sightings carry an identical pose** at 945420, 974360 and 974710: x -53.326637, y 70.12366,
  z 0.32464582, q (-0.09767641, 0.0863431, -0.0022041195, -0.99146324), originId 9, imageX/imageWidth differing
  (73.6 vs 102.3; 153 vs 138). Image rects change, pose does not: the event's pose is not the fresh observation, or
  observations do not update the charger pose. The pose also has a ~10 deg tilt (q1 0.086) for a flat charger.
- Charger detection is sparse compared with cubes (2 vs dozens in the same window). Leads: charger marker
  detection rate; whether charger observations update the world pose (and how the stale tilted pose feeds the
  predock goal); the morning's F20 (FAILED_TRAVERSING_PATH) had the same tilted-first-sighting pattern.

## 10. Moving the unpowered cube (00:41)

- **pickup 5 (unpowered LIGHTCUBE1): SUCCESS** first try (00:41:10..00:41:22), upset-driving anims,
  objectInteraction word0 5. Picking up an unpowered cube works.
- turn +1.4 rad, **put-down: SUCCESS** (00:41:31). Cube 5 now Known ~112 mm ahead.
- **F3 reproduced with the toy:** drive -50 mm CANCELLED at ~00:41:32.3 when PetInitialDetection fired with the
  small blue toy robot in view (img 16112 top-left); anim_petdetection_shortreaction_01_head_angle_-20. Turn and
  drive were not-admitted for **at least 6 s** (still ReactToPet at 00:41:38) while the toy stayed in view. The
  toy is a dependable pet-detector false positive; operator asked to move it.

- Toy moved at ~00:42:00. Reaction ended 00:41:39 (10 -> 22). turn -1.8 rad SUCCESS; **charger now seen
  repeatedly** (x1 during the turn, x5 during head move), **pose updated** (x -65.36, y 54.91, z 4.44; q
  (0.068, -0.043, 0.014, 0.997)), and img 16647 drew a **Charger_Basic box** (worldObjectId 0). F23's
  "identical pose" therefore means the pose updates only sometimes, not never.
- **mount-charger 0, third attempt: FAILED_TRAVERSING_PATH** (00:42:11..00:42:18), same as the morning's F20.
  charger-seen x14 during the attempt; planner computed and followed a plan; subActionResults
  [CANCELLED_WHILE_RUNNING, NOT_STARTED, FAILED_TRAVERSING_PATH].
- **Charger pose quality:** the last sighting's pose is z **17.8 mm**, q (-0.007, **-0.116**, 0.019, 0.993): about
  13 deg tilt and 18 mm above the desk for a charger lying flat; earlier sightings z 0.3..4.4 mm with 5-10 deg
  tilts. Strongest docking lead: the charger marker pose estimate (marker size/geometry for Charger_Basic, or the
  pose filter) is off, so the predock pose/path derived from it fails on traversal. Docking unusable through MCP
  today (3/3 failures across both sessions).

## 11. Docking from a better start (00:43..00:44), operator placed him facing the charger

- Operator set him ~35 cm in front of the charger, clear desk between. Hand placement -> new origin 13, charger
  forgotten.
- **Charger detection range:** stationary at ~35 cm and then ~20-25 cm (img 18174, marker plainly readable):
  **no charger-seen** for 5-10 s each, while unpowered cube 5 at ~27 cm was seen continuously. Charger sightings
  so far came from <= ~19-26 cm and mostly while moving; earlier, 97 consecutive sightings at ~12 cm stationary.
  Lead: effective charger-marker detection range much shorter than cubes' (smaller marker; check the engine's
  charger marker size / detection limits).
- drive +60 mm: charger-seen x3 while moving; charger 188 mm at -16.6 deg; pose z 7.3 mm, q (0.984, 0.008,
  **0.152**, -0.092): ~17 deg tilt again.
- **mount-charger 0, fourth attempt: FAILED_TRAVERSING_PATH** (00:44:06..00:44:09.8).
- **Key observation for F23:** all **12 charger-seen** during this approach (robot ts 1178985..1179945) carry the
  **identical pose** (x 346.12933, y 24.070286, z 7.2878666, q (0.9840385, 0.0075867567, 0.15197617,
  -0.09227142)), the same as the first sighting at 1171005..1171680, while imageX/imageWidth change frame to frame.
  The charger's world pose stays frozen at the first (tilted) estimate through the whole approach. Most likely
  root cause of all four dock failures (F20 morning; F23 x3 tonight): later charger observations do not update
  the charger pose (or the sighting events report the stored pose, and the stored pose is not refined). Check the
  BlockWorld update path for the Charger_Basic family (UpdateObjectPose / pose-state Dirty handling for chargers)
  against the binary.

## 12. Manual docking by the LLM: SUCCESS (00:44:49..00:45:43)

Operator asked whether Claude could dock without mount-charger. Done with primitive actions only:
1. Charger seen straight ahead, 161 mm, bearing -5.4 deg (img 19046, Charger_Basic box drawn).
2. turn -0.094 rad: charger 160.3 mm, bearing 0.76 deg, y 2.1 mm. This time the charger pose updated and was flat
   (q (0.9997, 0, 0, 0.026)), 19 sightings during the turn.
3. turn +pi (1.0 rad/s): charger now behind, x -198.6 mm, y -6.9 mm.
4. Target from the morning's docked state: charger relative x = +30 mm. drive -170 mm at 30 mm/s (no driving
   anim): charger x -27.9.
5. drive -60 mm: CANCELLED at 00:45:31.2 by UnexpectedMovement (climbing the ramp); charger x +24.5, y -9.1.
6. Operator: "almost there". drive -15 mm at 20 mm/s: SUCCESS; battery 3.91 -> **4.43 V** at once.
7. 00:45:40.996 **`going-to-sleep` event** (new MCP event, triggeredFromVoiceCommand false), reaction 22 -> 9,
   behavior ReactToOnCharger, anim_rtc_react_01. get_state 00:45:43: **isOnCharger true, isCharging true**,
   4.45 V, charger snapped to x 30 / y 0. Operator: "nailed it".

Takeaways: the robot's odometry plus one good, flat charger sighting is accurate enough to dock by hand
(final odometry error ~5-10 mm). This supports F23's diagnosis: mount-charger's failures come from the charger
pose (frozen/tilted estimates), not from motion control. Useful baseline for the reviewers.

## 3. On the charger again (00:26:39)

- Operator placed him on the charger: 00:26:39.707 `robot.reaction_trigger: PlacedOnCharger`, behavior
  ReactToOnCharger, anim_rtc_react_01 (1.0 s), then "animation tag 10 (clip unavailable)".
- get_state 00:27:19: isOnCharger/isCharging true, 4.64 V, charger worldObjectId 0 at 30 mm, reaction
  PlacedOnCharger. **New `reactionLocks`:** `ReactToOnCharger_behaviorLock` with triggers CubeMoved,
  FacePositionUpdated, FistBump, Frustration, Hiccup, NoPreDockPoses, ObjectPositionUpdated, PetInitialDetection,
  Sparked, UnexpectedMovement.
- **F8 persists:** drive-straight 150 mm at ~00:27:25: not-admitted, "External actions are disabled by a reaction".
- **Operator: "you should be able to drive on and off the charger."** The refusal comes from the reaction lock while
  ReactToOnCharger runs. Morning log had MISSING on this exact path (BehaviorContainer case 0x59D1B2 ReactToOnCharger
  class not implemented; vtable+0x24 gate stand-in answers true; M7-019). Lead for the job: from the binary, does
  BehaviorReactToOnCharger keep external actions disabled for its whole run, and what ends it (the original app's
  drive-off-charger flows exist: HikingWakeUpOffCharger, OnboardingWakeUpDriveOffCharger triggers)? Operator moved
  him by hand instead.
- **F21. Off the charger, the charger reaction stays.** get_state 00:28:30: isOnCharger false, isCharging false
  (4.05 V), but behavior ReactToOnCharger, reaction PlacedOnCharger, lock held; charger still 30 mm behind;
  head -0.15 not-admitted (NOT_STARTED tag 9). Event feed since 00:27:23 shows **no off-treads change** (no pickup
  detected). Operator moved him by hand, apparently without lifting. So leaving contact alone does not end the
  stand-in reaction; this morning only RobotPickedUp ended it (trigger 9 -> 12). Ties to F8/F21 lead above.
- **F14 risk:** if unchanged, the idle timeout disconnects at 00:26:39.7 + 330 s = **00:32:09.7** even after he
  leaves the charger. Watching for it.

- Operator then picked him up properly: the charger reaction ended (as in the morning, via pickup), charger
  forgotten (delocalized). get_state 00:29:04: **activity Socialize, highLevelActivity Freeplay**, behavior
  PounceOnMotion_Socialize: freeplay is on by default after this reconnect. Three face tracks (Jim 1, 3, 4).
- head -0.15 at 00:29:04 was **admitted and cancelled the freeplay pounce** (anim_pounce_03 aborted, behavior 63 ->
  178). Contrast with the morning's F16 (MeetCozmo held a head command 30 s then cancelled it).
- freeplay off at 00:29:1x: submitted. **Camera `visionProcessed` became false** on every frame afterwards
  (img 5120, 5222, 5299; latestReceivedImageId == imageId): with freeplay off, vision does not process frames.
  Expected? It means no face/cube detection while freeplay is off. Lead: which vision modes the engine schedules
  with freeplay off. Note the morning's cube/face work was also done with freeplay off and detections did arrive,
  so check what changed.

## 4. Full-turn accuracy check (00:29:20..00:29:25)

- Reference img 5222 after a +1.2 rad turn toward the monitor wall. Then **turn-in-place +6.2832 rad** (1.5 rad/s,
  10 rad/s^2): SUCCESS, relocalizationCount 0, ~5.2 s.
- img 5299 vs 5222: monitor right edge x ~248 -> ~302, left edge ~110 -> ~155: scene shifted right by ~45-54 px.
  At ~60 deg horizontal FOV over 320 px (~0.19 deg/px; FOV value assumed, not sourced) that is **~9-10 deg
  over-rotation on one full turn (~2.6%)**. Leftward (positive) turn with scene moving right in the image means
  he ended rotated slightly further left than commanded. Observation only: the turn action's success is judged on
  the robot's own heading; wheel slip on the desk would not show.
- This does not explain the large mismatch in section 1 (~137 deg); that one stays unexplained (possible causes:
  a stale frame, the operator moving, or larger error in 1.2 rad steps).
Files: img5222-turn-before.png, img5299-turn-after-2pi.png.

## 5. F14 re-test: no disconnect (00:32)

- Charger reaction began 00:26:39.7; the morning's behaviour would have disconnected at 00:32:09.7. Waited with
  read_events through 00:32:45: **still connected and ready**, no events. **F14 looks fixed on d2397461.**
- Mechanism (code read, not re-verified against the binary here): `Cozmo.Robot/ChargerSleepReport.cs`
  `ObserveChargerSleepReaction`, a host replacement for the app's PauseManager (PauseManager.cs:186-192, 320-333,
  365-379, 484-491): the first reaction after PlacedOnCharger (here RobotPickedUp) posts EnableCubeSleep(false),
  **DeliverCancelIdleTimeout**, removes the "pause_manager" reaction lock, and **SetEnableFreeplayActivity(true)**
  (Robot.cs:667-670, AllowFreeplayOnResume=true). That also explains section 3's observation that freeplay was on
  after the pickup.
- Consequence for F21: a slide off the charger with no pickup fires no reaction, so neither the cancel nor the
  freeplay resume happens; the charger reaction and its lock stay until some reaction occurs.

## 6. Backpack lights

- Operator asked to change the backpack light colour. **No MCP tool for backpack (or cube) lights** exists; the
  tool list has none. The stack has light code (`Cozmo.Robot/Lights.cs`, backpack-light loader M4-036), so this is
  an MCP capability gap, not an engine gap.
- **Operator (2026-11-19 evening): wanted, low priority.** Add an MCP tool to set backpack lights (and cube lights
  while there), on top of the stack's existing light path. "Not super important but should be there."

## 7. Free exploration (00:33..00:35), freeplay off

- drive-straight 100 mm: **CANCELLED_WHILE_RUNNING** at 00:33:58.24 when the operator put cube 1 in front of him:
  cube-seen -> reaction ObjectPositionUpdated (8) / AcknowledgeObject, anim_reacttoblock_react_short_01_head_angle_-20,
  back to idle 00:34:01.40. why.snapshot showed the planner WaitingToCancelPath.
- **Correction to section 3's visionProcessed note:** frame img 9361 had `visionProcessed: false` while cube-seen
  reports were arriving for that time. So visionProcessed=false does not mean vision is off; it reads as "this
  returned frame was not the one vision consumed". Vision does run with freeplay off. (Field meaning to confirm
  against the MCP code.)
- say-text "Ooh, a cube! Is that for me?": SUCCESS, SayTextAnimation 00:34:17.05..00:34:21.13.
- pickup 1: SUCCESS first try (00:34:21..00:34:30), lift-effort anim; completion now carries an objectInteraction
  payload.
- turn -1.0 rad, **put-down: SUCCESS, now admitted true and detail "Native placement action ended"** (the morning's
  put-down field inconsistency is fixed).
- say-text "There. I'll keep it safe right here.": SUCCESS; call ~00:34:39.7, audio 00:34:49.57: **~10 s speech
  latency persists** (morning: ~9.4 s).
- turn-to-face 1: SUCCESS; report now richer: facePoseAvailable true, faceId 1, requestedFaceId 1, endReason
  "completed", callerCancellationRequested false. Head ended at 0.765 rad looking up at the operator (img 10284,
  colour, backlit three-quarter view, no face box, observationReportsAvailable false).
- say-text "Thanks for the cube, Jim!": SUCCESS, 00:35:11.41..00:35:15.37.

## 8. More exploring and play (00:35..00:38), freeplay off

- animation CodeLabHappy: SUCCESS, `anim_meetcozmo_celebration_02`, no truncation; recognised Jim during it. The
  result's `during` now ends with `continuation: {since, through, excludeTypes:["face-seen"]}`.
- turn +2.0 rad: SUCCESS; close view of a clear plastic box of binder clips (img 11325). say-text "Ooh, shiny
  clips!": SUCCESS.
- **roll 1** (cube he had put down): SUCCESS 00:36:31..00:36:44, anim_reacttoblock_lifteffortroll_01; cube
  quaternion went to (0.131, -0.695, 0.131, 0.695): on a new face.
- animation CodeLabVictory: SUCCESS, `anim_pyramid_success_01`. drive-straight -80 mm: SUCCESS.
- **turn +2.4 rad: CANCELLED_WHILE_RUNNING at 00:36:57.92** by reaction UnexpectedMovement (trigger 20),
  behavior.ReactToUnexpectedMovement, anim_reacttocliff_huh_01. He had turned into the clip box (camera pressed
  against the plastic, img 12032). Looks like correct engine behaviour (blocked wheel motion detected).
- drive -60 mm, turn -1.6 rad, drive +100 mm: all SUCCESS. head 0.5: SUCCESS (its `during` held only
  neutral-eyes anim events, no action-completed: minor).
- img 12203: cube clearly in view (two marker faces), visionProcessed true, **no cube box**,
  observationReportsAvailable false. Possibly the cube on its side reads differently; F10 partly open for cubes.
- say-text "Hey Jim! I rolled the cube and only bumped into one box.": SUCCESS, but **~17 s** from call (~00:37:18)
  to audio (00:37:35.83); playback 7 s. Speech latency appears to grow with text length (short phrases ~9-10 s).
- During it: face -2 -> **new unnamed ID 7** (operator in three-quarter view, not recognised as Jim; a possible
  duplicate identity for the operator). face -1 lost.

- Desk tour 00:37:57..00:38:08 (four turns +1.0, +1.0, +1.2, +1.2 rad, all SUCCESS): cube up close (12940),
  the clip box (12969), the **charger with its marker plainly visible** (img 13053, ~15-20 cm), a small blue toy
  robot in front of a white printer (13079).
- **F22. Charger in plain view but not added to the world.** get_state 00:38:09.8 (1.7 s after img 13053's turn
  completed): knownChargers [] and no charger-seen event in the turn's `during`. The frame was taken right after
  the turn ended; the charger may have been in view for too few processed frames, or charger detection is not
  running. Re-check with a held look at the charger before concluding.
  Also: unnamed face 7 (section 8) no longer in knownFaces.
- **F22 confirmed and widened: marker detection stopped at ~00:36:56; face detection continued.**
  - Last cube-seen of any kind: robot ts 743985 (00:36:56.05), in the turn that was then cancelled at 00:36:57.92
    by ReactToUnexpectedMovement (section 8).
  - After that, **no cube-seen or charger-seen at all**, though cube 1 was plainly in view (img 12203 at 00:37:09,
    img 12940 at 00:37:58), the charger was centred with its marker clear (img 14275, 00:39:28) and held still for
    9 s (read_events through 00:39:37: no events), and a second cube was at the frame edge.
  - knownCubes still shows cube 1 with lastObservedTimestamp 743985, poseState Dirty.
  - Face detection kept working: face-seen x83 during the 00:37:2x speech, face-id-changed -2 -> 7 at 00:37:38.
  - Leads: the marker-detection vision mode being disabled by the UnexpectedMovement reaction or the cancelled
    turn and never restored; or the marker detector thread failing (check stack-log around 00:36:57 for a handler
    fault / MISSING / exception).
  - Session log 00:36:55..00:37:05: at 00:36:57.916 (the cancel) `MISSING: M11-062 Robot::GetPose VERIFY root
    checks` and `MISSING: M11-091 markerless +0x50 body/caller census 0x00502C32`; at 00:36:58.034
    `VisionComponent.SetNextImage.OriginMismatch: Could not get pose data for t=746025 due to origin mismatch.
    Returning OK` (the last of only 6 such lines all session; no Delocalize after 00:30). No exception logged.
    The M11-091 "markerless" MISSING right at the cancel is the most suggestive lead.
  - **Recovery:** freeplay on at ~00:40:14 (behavior RollBlock chosen at once). First marker sighting since
    00:36:56: **charger-seen at 00:40:16.46** (robot ts 945420), ~1 s after the freeplay behavior started driving.
    Cube sightings resumed by 00:40:26.9 (unpowered LIGHTCUBE1, new worldObjectId 5). So detection resumed either
    with freeplay's vision/behavior setup or once he moved; which one is not separated. Lead: with freeplay off and
    the robot stationary after an UnexpectedMovement cancel, marker detection stays off.
  - Freeplay's RollBlock drove ~36 cm toward cube 1's stale (Dirty) pose and pushed into something
    (UnexpectedMovement at 00:40:22.34, action type 10 CANCELLED). Freeplay turned off again at ~00:40:25.
  - img 15156 (00:40:27.9): **orange cube box** for worldObjectId 5 (LIGHTCUBE1, unpowered) on a colour frame;
    the charger beside it has no box (annotation may cover cubes only, or no report for that frame).
- **F3 lead:** PetInitialDetection fired at 00:39:01.27 (ReactToPet, anim_petdetection_shortreaction_01) while he
  faced the small blue toy robot (img 13079). A repeatable non-pet trigger for the pet detector; use it to test
  F3. say-text "Hello, little robot!" was not-admitted during it, then SUCCESS at 00:39:20.

Files: img2447-color-face-box.png, img1386-color-desk.png, img1676-color-operator.png, img1736/1802/1862-color-pan*.png.
