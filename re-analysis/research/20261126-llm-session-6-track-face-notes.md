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

## Charger mount (03:32–03:37 UTC): the charger marker is never detected

He was picked up at 03:30:36, so a new origin; knownChargers was empty. Claude drove him around to the charger's
open end, then placed him square to the marker, about 15–25 cm away and centred in frame. Frames
`imageId` 18546, 18697, 19833 and 20870 are visionProcessed=true.

- **No `charger-seen` events in the whole session**, from 03:33:50 to 03:37 across many views.
- The only charger in BlockWorld was the on-charger placement at connect (stack-log 2439, lastObserved 0).
- Lighting is ruled out:
  - first, dark room: exposure 63–67 ms, gain 3.98 (maximum);
  - then the operator's lamp behind the charger: glare;
  - then the lamp from behind the robot (03:36:30): marker crisp, square, about 40×35 px.

  It was not detected in any of the three.
- Marker detection is enabled (stack-log 2350 DetectingMarkers) and found the cube marker this session (03:24:34
  and 03:25:20).
- In session 5 (2026-11-23, daylight) the same charger was detected at similar sizes and angles (many charger-seen).
- **Lead:** a regression since 2026-11-23 in charger-marker detection or charger object creation. Commits since then
  touch `Vision/VisionSystem.cs`, `Vision/BlockWorld.cs`, `NativeVisionRunners.cs`, `VisionImageQuality.cs` and
  `DockActions.cs` (cd8792af "Restore native dock constructor marker defaults"), and others.
- **Next step:** replay this bundle's frames around 03:36:30 through the stack's marker detector offline, and bisect
  if the session-5 frames still detect the charger.
- The mount itself was not attempted: it needs a located charger.
