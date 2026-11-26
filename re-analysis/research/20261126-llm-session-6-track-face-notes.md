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
