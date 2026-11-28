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
| sleep on charger, link drop | PASS (R5 WifiTimeout gone) | Asleep and on the charger from 16:25:21 UTC, with no WifiTimeout. Previously it dropped within 11–25 s (R5). At 16:30:51 (about 5.5 min) came `robot-disconnected reason=SleepPlacedOnCharger`: the engine's scheduled sleep disconnect (`ChargerBehaviors.cs:276-277`, StartIdleTimeout with TimeTilDisconnectionSec), as the original does. The bridge tore down cleanly. **link-reconnect was not exercised**: it covers WifiTimeout only, and none occurred. |

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

## Handoff: proposed work, in priority order

Each item names its source. All are C2 extensions or bridge fixes. None changes the engine baseline.

1. **Go home is unreliable (G1, G2, G3).** It is the most-used extension and failed most today.
   - G1: before any retry, check `isOnCharger`, or wait briefly for the on-charger event after a mount result.
   - G2: dump what the planner sees when the viewing approach fails (`Ready -> Failed`). Check whether map-obstacle-memory's retained InterestingEdge and Cliff entries block the viewing or pre-dock poses. If so, decide what obstacle memory should keep near the charger.
   - G3: the charger search should sweep with the head *down* (the charger is low), not reuse the face-search look-around (`anim_lookinplaceforfaces_*` at +20/+40°).
2. **Keep the MCP session open across a sleep disconnect.** Operator request (2026-11-28): Claude cannot run `/mcp`.
   - Today the engine's scheduled `SleepPlacedOnCharger` disconnect (about 5.5 min) tore the bridge down, and only the operator could reconnect.
   - Proposal: extend C2:link-reconnect so the session stays open after a sleep disconnect, as it does after WifiTimeout, and add a `connect`/`wake` tool that reconnects to the robot on request.
   - Expire actions, origin and face IDs as link-reconnect already does.
3. **Silent bridge process deaths (R2) cannot be recovered by Claude.**
   - Option: run the bridge as a long-lived process over HTTP MCP with a restart watchdog, so a crash doesn't need `/mcp`.
   - Needs a quick test of whether Claude Code re-attaches to an HTTP server after a restart.
   - Root cause of R2 is still unknown. No drops occurred this session.
4. **Explore (E1, E1b, E2).**
   - On a failed leg: record the obstacle, back off, choose another leg.
   - Run until maxSeconds, not three legs.
   - Choose legs with the same obstacle set the native planner uses.
5. **Person-follow (P2).** Keep the target *person*: when following an enrolled identity, a new track recognised as that identity should win over a different new identity (ID 5 today).
6. **Slope-putdown (S1).** Read the pitch the engine itself logged at put-down (ReactToReturnedToTreads `CalibratingHead: <pitch>`), or wait for fresh IMU data; don't read during the NoIMUData gap.
7. **F3, false PetInitialDetection.** Three times again; it interrupts face work. It needs the pet detector's input checked against the binary.

## Notes on driving (for whoever runs the next session)

- The operator wants Claude and Cozmo to work *together*. Use the camera and remembered positions to set Cozmo up (turn toward a known charger, head down, back out of clutter), then let his behaviour finish. Don't fire a high-level tool repeatedly and report failure, and don't hand the job back to the operator.
- Face IDs are stable at about 50 cm with the person facing him, and churn badly at 25–35 cm side-on.
- Freeplay re-enables after drive-off-charger/wake if it was on. Turn it off before scripted tests.
- Charger geometry in state: `knownChargers.relative` is +30 mm ahead when docked, and the charger heading is roughly the docked heading + π. Back on along the charger's axis.
- Reaction locks are lost on any bridge reconnect.

## Still open from earlier sessions

- R2 silent bridge drops: none this session.
- R3, R6, P1, T1, hiccup origin: unchanged.
