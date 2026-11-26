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
