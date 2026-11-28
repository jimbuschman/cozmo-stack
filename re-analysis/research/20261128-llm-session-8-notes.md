# LLM session 8: C2 extensions on the robot (2026-11-28)

**Operator:** present. **Driver:** Claude, through the Cozmo MCP bridge.
**Bundle:** `re-analysis/acceptance/hardware/20261128-100204-LLM-SESSION/` (kept local; frames.jsonl is large).
**Extensions enabled:** cliff-hold-still, charger-memory/dock/drive-off, slope-putdown, person-follow, map-reanchor, map-obstacle-memory, map-save, go-home, link-reconnect, face-requests, map-explore.

Pass/fail follows engine signals. Everything else is an observation.

## Results

| Test | Result | Evidence |
|---|---|---|
| drive-off-charger | PASS | DriveOffCharger ran; off-charger event; about 160 mm out |
| explore (60 s) | FAIL (bridge) | Run 1: leg 2 jammed on a mug, giving UnexpectedMovement TurnedButStopped, which cancelled the drive, and explore quit. Run 2: PATH_PLANNING_FAILED_ABORT (0x03000013) on leg 1, and explore quit. See E1–E2. |
| come here (VC_ComeHere) | PASS | VC_ComeHere ran (behavior-changed 162) and drove to the face. **R1 (session 7) fixed.** A false ReactToPet delayed the start by about 7 s (F3). |
| look for me (search-for-face) | PASS | FindFaces_socialize; face confirmed about 5 s later |
| how are you doing | PASS | VC_HowAreYouDoing_AllGood played anim_meetcozmo_celebration_02 |
| follow (track-face + C2:person-follow) | PARTIAL | Worked at about 50 cm with a stable ID. Up close it failed; see P2. |
| pickup and put down on a slope | Engine behaviour correct; extension FAIL | A nose-down −13.9° slope is outside the engine's (10, 55)° gate, so the correct result is no reaction. Nose-up 17.8°: the slope extension read pitch 1.1°; see S1. |
| go home | PARTIAL | Run 1: cliff during the 360° scan, so it stopped. Run 2: scan timed out on the slope. Run 3 (Claude found the charger visually first): **docked**, but the tool reported NOT_ON_CHARGER; see G1. Later runs: every route refused; see G2. |
| cliff hold-still | **PASS** | C2:cliff-hold-still held the body through anim_reacttocliff_huh_01 and anim_reacttocliff_edge_01, released on Undetected, then the engine's own retreat DriveStraight ran. The operator confirmed it looked right. **Addresses R4 (session 7 desk fall).** |
| IR headlight (set-headlight) | PASS (observation) | Room light off: exposure climbed to 67 ms (the maximum). Headlight on: it dropped to 15–20 ms. The robot does not report the headlight's state. |
| sleep on charger, link drop | PASS (drop no longer occurs) | Asleep and on the charger 16:25:21 to 16:29:24+ UTC, with no WifiTimeout and the link Connected throughout. Previously it dropped within 11–25 s (R5). **link-reconnect was not exercised**, because no drop occurred. |

## Findings for the code side

- **E1. Explore gives up on the first failed leg.** `CozmoBridge.Explore.cs:141-142` returns on any non-zero leg result: no back-off, no other leg.
- **E1b. Explore ends at three legs** ("three legs completed"), whatever maxSeconds says.
- **E2. Explore's leg chooser and the native planner disagree.** The chooser picked a "clear" leg that the planner refused outright (PATH_PLANNING_FAILED_ABORT) once the scans had filled the map with InterestingEdge segments.
- **P2. Person-follow stays on the wrong identity.** The follow moved −1 → −2 (nearby observation, 33 mm), then −2 → 5 (reported ID change; recognition made a *new* person). Then −3 was recognised as Jim (ID 1), but the follow stayed on 5, because it refuses to cross to a different recognised identity. If the original target was Jim, a new track recognised as Jim should win. Underlying cause: face ID churn up close (T1); at about 50 cm IDs were stable.
- **S1. The slope-putdown extension read a stale pitch.** ReactToReturnedToTreads logged `CalibratingHead: 17.395576` (pitch). At 1.92 s, slope-putdown logged `pitch 1.1 degrees, slope check False`. At the same moment there were `VisionComponent.WasHeadRotatingTooFast.NoIMUData` warnings. Live pitch afterwards: 17.8°. The extension appears to read a pitch source that is stale during the post-put-down head calibration. The native window lost to ReturnedToTreads again (P1).
- **G1. Go home reports failure about 57 ms before the on-charger signal.** The mount's final check returned NOT_ON_CHARGER at 16:18:47.742. The on-charger event came at 16:18:47.799. The C2 retry then ran a viewing approach from on top of the charger and failed path planning. Go home should check isOnCharger (or wait briefly) before retrying.
- **G2. Go home and mount-charger: the planner refuses every route.** From 16:23 the C2 viewing approach failed at once (`Ready -> Failed`, 0x03000013) from three positions, including open desk 15 cm away. Suspect: map-obstacle-memory retained dozens of InterestingEdge segments plus the edge-test cliff, blocking the viewing or pre-dock poses. Needs a look at what the planner saw.
- **G3. Go home's charger search uses a face-search look-around.** Hiking_LookInPlace360 runs `anim_lookinplaceforfaces_*`, mostly at head +20/+40°, so a low charger nearby is easily missed. Twice it timed out with the charger about 2 turns away. A head-down manual sweep found it at once.
- **F3 (again).** False PetInitialDetection, three times this session.
- **Manual back-in.** Claude reversed toward the charger using the charger pose. He snagged on the lip: UnexpectedMovement, side 2. The operator finished the dock by hand.

## Still open from earlier sessions

- R2 silent bridge drops: none this session.
- R3, R6, P1, T1, hiccup origin: unchanged.
