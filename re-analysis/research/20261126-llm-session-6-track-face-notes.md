# Session 6 (2026-11-26 03:17–03:31 UTC): track-face on the robot

- **Bundle:** `re-analysis/acceptance/hardware/20261125-211724-LLM-SESSION/` (local only).
- **Operator result:** "doesn't seem to work, or at least doesn't work well" / "still not doing anything".
- **Status:** observations, not yet checked against the binary.

## The five runs (stack-log TrackFace[4]..[8])

| Tag | Target | Point turns | End | What happened |
| --- | --- | --- | --- | --- |
| 4 | -1 | few | AcknowledgeObject (cube on a shelf at z≈144 mm) | Ended about 16 s after the call, before any visible follow. No locks held. |
| 5 | -4 | **about 20 in 2.6 s** (stack-log 4369..4446) | AcknowledgeFace (FacePositionUpdated not locked) | **It followed.** The target ID stayed observed. |
| 6 | 4 | 1, then none for about 20 s | operator Esc | ID 4 was never observed again. The operator was about 12 cm away; the face came back as new temporary IDs (-8, -9). |
| 7 | -10 | 0 | operator Esc | -10 was recognised as 1 (`immediate_recognition: 1`, stack-log 5056) **before** TrackFace[7] started (5058). The runner subscribed after that change and kept waiting for face -10. |
| 8 | 1 | 1 | robot picked up (ReactToPickup) | During the run only temporary face -13 was seen. Face 1 got no new observation, so there was nothing to follow. |

Locks held for runs 6–8: ObjectPositionUpdated, CubeMoved, PetInitialDetection, FacePositionUpdated.

## Findings

**T1. The tracker follows one face ID, but the face ID changes every few seconds on this robot tonight.**
- Seen: -1, -4, -5→5, -6→4, -8, -9, -10→1, -12→merged 1, -13.
- After each loss the face comes back as a new temporary ID. Recognition as Jim (1) was intermittent. Conditions: at
  night, and the operator was often closer than 20 cm.
- So TrackFace only works while one track survives (run 5).
- To trace: how long the original keeps a face track through brief losses (the FaceTracker and FaceWorld timeouts),
  against ours.

**T2. A stale target ID waits forever.** `NativeTrackFaceRunner.cs:82` follows only RobotChangedObservedFaceID events
that arrive after it subscribes. A change that happened just before the action started (run 7) leaves it waiting for
a face that no longer exists, with no end and no report.
- To trace: what the original TrackFaceAction does with an unknown or merged ID at Init (an error result, or
  running forever).
- Bridge-side options, decided against the engine: resolve the ID through FaceWorld at submit time, or reject an
  unknown ID.

**T3. Commanded tracking has no end without a cancel.** The MCP call stays pending, so the operator has to press Esc.
`cancel-action` exists (LLM-WAKE-CANCEL-1), but an LLM cannot reach it while its own track-face call is blocking.
Consider making track-face return once admitted, with completion in events, or adding a duration.

**T4. The commanded track was interrupted by reactions** (AcknowledgeObject, AcknowledgeFace, Pickup) unless those
triggers were locked. Probably engine behaviour; to confirm against the action's reaction locks.

## Charger mount (03:32–03:42 UTC)

**Correction:** an earlier version of this section blamed a charger-detection regression. That was wrong. The
charger was not detected because Claude viewed it about 30–40° off its axis, with the ramp lip in front of the
marker. Once straight in front (03:38:42), it was detected about 4–7 times per second, before and after the lamp.
Lesson: get square on before concluding the detector is at fault.

| UTC | Result | Cause (`why`) |
| --- | --- | --- |
| 03:38:53 | DID_NOT_REACH_PREACTION_POSE | Position in range, relative yaw −8.0° against the 7.5° limit. |
| 03:39:01, 03:39:13 | VISUAL_OBSERVATION_FAILED | VisuallyVerifyObject saw no charger in its frame window. Afterwards the detection rate at the verify head angle was normal (50 in 9 s), so likely transient. |
| 03:39:55 | FAILED_TRAVERSING_PATH | Arrival rotation 11.0° against 10.0°. |
| 03:40:07, 03:40:25 | DID_NOT_REACH_PREACTION_POSE | Y offset 29 and 37 mm (limit 21). |
| 03:40:40 | FAILED_TRAVERSING_PATH | Path goal z = 58 mm, arrival rotation 13.4° against 10°. |
| 03:41:03 | CANCELLED_WHILE_RUNNING | Docking SUCCESS (dockingResult 2); the reverse up the ramp hit UnexpectedMovement (L −37 / R −20), the reaction cancelled the mount, then ReactToFrustrationMajor drove him away. |
| 03:41:36 | LAST_PICK_AND_PLACE_FAILED | Firmware docking `success:false dockingResult:6`; UnexpectedMovement and Frustration were locked. |

**Success by hand (03:41:53–03:42:08):** turned to face away from the charger (absolute heading −0.248, from the
charger bearing) and reversed 180 mm, then 70 mm, with UnexpectedMovement locked. Two unexpected-movement reports
fired on the climb (03:42:06 L −68/R −42; 03:42:07 L −83/R −51); `on-charger` at 03:42:08, then ReactToOnCharger.

**C1. The charger pose estimate is unstable and often tilted.**
- Between sightings: y ±10 mm, yaw 3–11°, z −15…+12 mm.
- q1 swings between about 0 (flat) and −0.05…−0.106 (6–12° pitch).
- A plan built from a tilted sighting gets an elevated, rotated goal (z 46–58 mm), and the 10° arrival-rotation
  check (DriveToPose) or the 7.5° yaw / 21 mm Y pre-dock check then fails.
- Trace: does the original force the charger to rest flat (ObservableObject rest-flat / BlockWorld pose
  update) before computing pre-dock poses? Same family as DOCK-CHARGER-POSE-1.

**C2. The reverse up the ramp trips UnexpectedMovement.** This is the second session where it happened (session 5
F6). CHARGER-MOUNT-REVERSE-1 bound the dock reaction-lock table (0x00C55A21, one flagged trigger, index 8).
- Check: is UnexpectedMovement the trigger the engine locks during the mount's reverse, and is the lock held for
  the whole backup phase?
- The by-hand climb produced the same reports, so the detector fires on the ramp itself.

**C3. Frustration drove him away mid-retry.** After repeated mount failures ReactToFrustrationMajor ran a
drive-off. Engine-normal per its mood input, but it fights an LLM retry loop.

## Command during Freeplay, then return to Freeplay (03:43–03:45 UTC)

Setup:
- freeplay(true), then `wake` at 03:43:55.
- sleep-cancelled, then PlacedOnCharger → None.
- DriveOffCharger was chosen ("highest score 1000.01"); off-charger at 03:43:55.6.
- **The wake tool works.**

**Turn command: PASS.**
- turn-in-place 90° at about 03:44:46.
- At 03:44:46.86 Freeplay's PounceOnMotion_Socialize was cancelled (action 3000355 CANCELLED) and the behaviour went
  to Wait.
- The turn returned SUCCESS at 03:44:48.84.
- Freeplay resumed by itself: `lastResumedAtEngineSeconds` = 1642.68, new activity Singing →
  Singing_PopGoesTheWeasel.
- Not observed: `pausedForCommand` true during the turn (not polled while it ran).

**Speech command: FAIL (S1).**
- say-text was submitted at about 03:44:10.5, just after Freeplay started InteractWithFaces (03:44:10.44).
- It did **not** pre-empt Freeplay. InteractWithFaces' [TrackFace + ag_pause_idle] kept running for 13 s,
  `pausedForCommand` stayed false, and no SayTextAnimation started.
- When InteractWithFaces completed (objective InteractedWithFace, 03:44:23.94), SayText[1] got its preconditions
  (stack-log 11914). In the same tick the FistBump reaction (triggered by `trigger_fist_bump_response:
  InteractedWithFace`) cancelled it (11919).
- Result CANCELLED_WHILE_RUNNING. Nothing was said.

Lead:
- The bridge's speech path (`SpeechOwner.QueueEnglishSpeech`, `QueueActionGuardedAtEnd`, direct SayTextAction)
  queues at the end of the action list and does not appear to trigger the Freeplay pause for an external command.
- The turn path did trigger it.
- Trace:
  - which engine path the app's SayText takes (the G2E message, its queue position, and the BehaviorManager
    external-action handling that stops the running Freeplay behaviour);
  - whether speech should pause Freeplay as the turn did.

**S1 confirmed (retry 03:46:56–03:47:16).**
- say-text was sent while Freeplay ran FindFaces_socialize (started 03:46:54).
- FindFaces kept running its look-around for about 20 s; no SayTextAnimation started; `pausedForCommand` stayed false.
- The speech ended CANCELLED_WHILE_RUNNING when Freeplay moved on to Hiking_FirstLookIntro (03:47:16).
- Turns pre-empt Freeplay; speech never does.
