# Session 7 (2026-11-26 15:47–15:54 UTC): retests and new request tools

- **Bundles (local only):** `re-analysis/acceptance/hardware/20261126-094739-LLM-SESSION/` and
  `20261126-095137-LLM-SESSION/` (`095040` is the reconnect in between).
- **Status:** observations, not yet checked against the binary.

## Results

| Test | Result |
| --- | --- |
| DriveOffCharger via request-behavior | PASS (15:48:31). |
| track-face returns at once; cancel-action | PASS. Tag 4 was CANCELLED_WHILE_RUNNING about 1 s after the cancel. |
| track-face actually follows | FAIL (T1 again). The face came back as new ID 4, not 1, at about 20 cm, so target 1 got no observation. The first track (tag 3) was cancelled by AcknowledgeObject for a cube at z 146 mm (T4). |
| say-text during Freeplay (S1 retest) | PASS. InteractWithFaces was cancelled, then Wait, then SayTextAnimation 15:49:16–21. S1 is fixed. |
| VC_SearchForFace | FAIL. "selecting behavior name 'VC_SearchForFace'" was logged, then Wait for more than 20 s, with no log of why. Freeplay was enabled and pausedForCommand. |
| VC_ComeHere | FAIL. Same with Freeplay on, and again with Freeplay off after the reconnect: selected, never started, no reason logged. |
| Hiking_LookInPlace360 (ExploreLookAroundInPlace) | PASS. Ran for more than 30 s of lookinplaceforfaces keepalives. |
| VC_HowAreYouDoing_AllGood (PlayAnimWithFace) | PARTIAL. TurnTowardsFace (type 41) SUCCESS, then anim_meetcozmo_celebration_02. The operator says he did **not** turn toward them. After the reconnect, face 1 had lastObservedTimestamp null, so the turn had no current face pose. |
| PlayArbitraryAnim (CodeLabHappy) | PASS. ag_codelab_happy → anim_meetcozmo_celebration_02, the shipped mapping. |
| InteractWithFaces | Seen on its own when Freeplay started (15:49:07). |
| Spark request, ExpressNeeds | Not reached. |

## Findings

**R1. The VC_ behaviours selected by request never run, and nothing says why.**
- HandleExecuteBehavior (Activities.cs, 0x0060AA58) selects them.
- The chooser then keeps Wait. Their runnable check presumably fails, with no MISSING and no reason.
- Hiking_LookInPlace360, PlayArbitraryAnim, DriveOffCharger and VC_HowAreYouDoing run by the same path.
- Trace the VC_SearchForFace and VC_ComeHere runnable conditions (shipped config, the classes' IsRunnable) against
  ours.
- Missing diagnostic: the request API should report "selected but not runnable: <reason>".

**R2. The bridge process dies silently.** It happened twice:
- 15:50:3x, about 1 s after a second VC_ComeHere request with Freeplay just turned off;
- about 15:53:46, about 1 s after PlayArbitraryAnim ended and the behaviour returned to Wait.

Each time the stack log stops mid-run, result.json stays "running", there is no exception text, and there is no
Windows Application-log entry. A hard process exit (stack overflow, FailFast, or the MCP client killing the
process) is suspected. Next run: capture stderr and the exit code.

**R3. PlayAnimWithFace turns to a stale or absent face.** TurnTowardsFace reports SUCCESS with no current
observation of the face. Check what the original's TurnTowardsFace or VC_HowAreYouDoing does when the face pose is
unknown or stale.

## After the second reconnect (15:55–15:57 UTC)

| Test | Result |
| --- | --- |
| request-spark FistBump | PASS. SparksFistBump ran: sparking_getin, TurnTowardsFace NO_FACE (no face since the reconnect), a search turn, fistbump requestonce, idle, requesttwice, idle, fail_01 (left hanging), sparking_getout, then back to Socialize. The spark's reaction locks were applied. Ask the operator whether a bump was attempted, to tell a missed detection from no attempt. No server drop at the spark's end. |
| ExpressNeeds | PASS. Needs real mode with play 0.3 (Warning bracket is 0.14..0.5 per needs_config.json): Needs_MildLowPlayRequest was chosen in Socialize, "first runnable in priority order", about 10 s later (15:57:29), and completed SUCCESS. Play 0.5 did not trigger it; that is the Normal bracket boundary, as expected. Needs set back to full afterwards. |
| InteractWithFaces | Not re-seen after the reconnect: FindFaces_socialize found no face in about 20 s and dropped to PlayAlone. It was seen at 15:49:07. |

Covered from the original nine: InteractWithFaces, ExpressNeeds, ExploreLookAroundInPlace and PlayArbitraryAnim.
Plus EarnedSparks via the spark path (the Sparks activity ran).

Still failing:
- SearchForFace and DriveToFace: the VC_ requests never run (R1).
- PlayAnimWithFace: does not turn to the face (R3).
- PlacedOnSlope: P1 from session 6.

## R4. He went off the desk edge after a cliff stop (15:58:12–15:58:15 UTC); the operator caught him

Sequence (stack log, 20261126-095137 bundle):
- 15:58:12.38: Freeplay Hiking_PounceOnMotion plays anim_pounce_03 (3.8 s, includes a **body** track).
- 15:58:14.158: firmware `ProxSensors.UpdateCliff.StoppingDueToCliff`, then RobotStopped. The behaviour is forced to
  end, and CliffDetected starts ReactToCliff.
- 15:58:14.655: CliffEvent detected (flags 0x03, both front sensors). ReactToCliff enters PlayingStopReaction.
- 15:58:14.699: it plays `anim_reacttocliff_huh_01` (0.46 s, tracks sound+face+head+lift+**body**).
- 15:58:14.997: `HandleCliffEvent.Undetected`.
- 15:58:15.148: OffTreads InAir (pickedUp). The operator caught him falling off the desk. Then RobotOnFace and the
  face-plant roll in the hand.

Open (trace against the binary before any change):
- Does the original ReactToCliff stop reaction lock the body track, or use a huh animation without body motion, at
  a detected cliff?
- Did the huh animation's body keyframes, or leftover pounce motion, carry him over the edge?
- Is the firmware cliff-stop latch (EnableCliffDetector) released or overridden by the streamed animation's wheel
  commands? The original sends the cliff reaction's animation with the body possibly suppressed while a cliff is
  latched.
- Decode the frames.jsonl wheel speeds and cliff values for 15:58:14.1–15.2 to see which motion took him over.

This is a safety issue: high priority for the next code session.

## R5. The robot disconnects (WifiTimeout) soon after going to sleep on the charger

- **Session 7:** on-charger 15:59:38.28, going-to-sleep, ReactToOnCharger, then robot-disconnected WifiTimeout at
  15:59:49.29 (about 11 s).
- **Session 6:** going-to-sleep 03:53:18, then WifiTimeout 03:53:43 (about 25 s).

Trace:
- what the original's sleep path sends the robot (for example a power-down/GoToSleep message or stopping keepalives);
- whether the engine expects the link to drop there, and how it reports it;
- whether our stack stops sending pings, or the robot powers its radio down on a command we send.
