# Consolidated hardware test plan

The campaign, and the program that runs it. Everything here is offline-verified in the repository and waits
only for a robot.

This document and `src/Cozmo.Conformance/HardwareCatalog.cs` are the same campaign. The table below is
generated from the catalog (`hardware-test --plan`), and a test in `HardwareRunnerTests` fails if the two
drift apart, so the code is the thing to edit and this is the thing to regenerate.

## Run it with the runner, not by hand

```
cd cozmo-stack
dotnet run --project src/Cozmo.Conformance -- hardware-test 172.31.1.1 --obb <obb>
```

That is the whole campaign, start to finish. For every check it tells you what subsystem is being tested and
why the check exists, what to set up and what to do while it runs, what the program is about to do and how
long it is allowed, what the software should report and what you should see in the room. Then it runs the
check, shows what the software saw, restates what you should have seen, and asks you to classify it:

```
[P] pass   [F] fail   [U] unsure   [S] skip   [R] run it again   [Q] stop and save
```

Your verdict is recorded separately from the tool's and is never inferred from it: the two disagreeing is
exactly the case worth knowing about (A and A2 are the standing example - the tooling is satisfied and the
song is wrong). **Unsure** is a real answer and is recorded as itself.

### Between sittings

The session is saved after every check, so a stop, a reboot or a flat battery costs at most the check that
was running. A check that was cut short is recorded **interrupted**, not failed, and is picked up again.

```
hardware-test 172.31.1.1 --obb <obb> --resume     carry on where it stopped
hardware-test 172.31.1.1 --rerun V                run one check again, keeping everything else
hardware-test 172.31.1.1 --rerun-failed           run everything that failed, was unsure or was cut short
hardware-test 172.31.1.1 --status                 what has been done, and what is next
hardware-test 172.31.1.1 --export                 write results.json and SUMMARY.md now
hardware-test 172.31.1.1 --only N,O               just those checks, for debugging
hardware-test 172.31.1.1 --from Q                 skip everything before Q
hardware-test 172.31.1.1 --obb <obb> --nominal    allow the stand-in camera calibration (diagnostics only)
hardware-test --list                              the campaign and what each check needs, without a robot
hardware-test --plan                              this document's table, regenerated
```

### Safety

* The campaign is ordered by risk: connection and telemetry first, then the things he does standing still,
  then the things that move him, then the charger, then manipulation, then the long autonomy run.
* Before **every** movement check the runner connects, confirms telemetry is arriving, and waits for the
  head and lift calibration the robot performs on connection. If any of that is not true, the check does not
  start and you are asked whether to check again, skip it, or stop.
* **Ctrl+C is the emergency stop**, at any point: it stops the motors on a connection of its own, ends the
  current check, and saves. Nothing recorded is lost.
* The motors are stopped after every check that moved him.
* A check whose prerequisite failed is recorded **blocked** and never started. `--only`, `--rerun` and
  `--from` are the exception, and then only for a prerequisite that has not run: a failed one still blocks.

### What the campaign does not do

Hardware acceptance validates observable behaviour. It does not upgrade provenance. A check that passes does
not change any fidelity record's status, and a check that fails is an investigation item with evidence
attached - not a licence to tune a source-backed constant until the robot agrees. In particular:

* **M11-005** (`RefineQuadrilateral` sub-pixel refinement) stays an open source gap whatever K reports. K can
  say whether marker behaviour is operationally good enough; it cannot close the gap.
* **V** must not be "fixed" by changing the charger marker's 20 x 27 mm geometry, which is the charger's own.

### Where the evidence goes

One directory per campaign under `re-analysis/acceptance/`, and inside it one directory per check:

```
hardware-<timestamp>/
  results.json          every check: metadata, verdicts, captured evidence, files   (machine-readable)
  SUMMARY.md            the campaign in one page, with the investigation items       (human-readable)
  tests/<ID>/
    record.json         what the check was, what happened, what was kept
    console.log         the whole transcript of the tool's run
    acceptance.json     the tool's own acceptance record, where it writes one
    frames/, frames.log images and raw protocol frames, where the check captures them
```

Each check names the lines worth keeping - docking error signals, path events, streaming counters, map
region counts - and those are lifted into `record.json` with anything that looked like a warning. The full
transcript sits beside them rather than being duplicated into the record. **Commit the run directory**,
which is the process change the fidelity audit asked for.

Setup for all items: robot on its own access point at 172.31.1.1, the OBB unpacked at `<obb>` (the sound
directory needs only `cozmo_resources/sound/AudioAssets.zip`), commands run from `cozmo-stack/`.

## The campaign

The identifiers of the original A-Z plan are unchanged, so results stay traceable to it. The checks added
since carry their own: LINK, FD, AUD, MOV, IDL, A3, Z2, and CR1-CR8 for the core-review corrections.

### 1. connection and telemetry

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| LINK | **Connection, handshake and telemetry** | The UDP transport, the connection handshake, identity and the 30 Hz state stream | - | - | the smoke test reports PASS: connected, identity received, telemetry flowing, no backlog | He sits still, his backpack light is on, and nothing about him changes for 20 seconds. |
| D | **Lift position readout** | RobotState lift angle and the angle-to-height conversion, with battery, cliff and IMU | - | LINK | both acknowledged motion actions complete and telemetry reaches the source-backed low/high endpoints | The two readings it prints back match where the lift actually was: about -0.198 rad / 32 mm down, about 0.712 rad / 92 mm raised. |

### 2. animation controller

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| F | **Animation timeline** | The animation scheduler's keyframe timing against the robot's audio pacing | `--obb`, **he moves** | LINK | exact animation success verdict: all keyframes fired and the timeline ran to length | The clip plays as it always has: no stutter, no truncation, no stuck lift or head. |

### 3. face display

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| FD | **Face display** | The OLED display path: the 128x64 packing and the display message | - | LINK | the tool sends the image and completes without error | A pair of eyes is drawn squarely on the screen, right way up, held steady for the whole time, and it clears afterwards. |

### 4. audio

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| AUD | **Speaker and the audio frame pacing** | The mu-law companding transcribed from the engine and the robot's audio buffer pacing | - | LINK | the tool sends the tone and completes without error | Separate, clean beeps at a steady rate, all the same length, with no crackle between them. |
| A | **Cozmo sings** | The Wwise switch, the per-note vocal sampler and the singing behaviour's animations | `--obb`, **he moves** | AUD | the switch is posted and the get-in, song and get-out animations complete | About 12 seconds of tune between a get-in and a get-out, in his own voice, one note per note, at a sensible level, with no bursts of get-in phrases during the song. |
| A2 | **One song from each tempo group** | The 100 and 120 bpm switch containers and their meter overrides | `--obb`, **he moves** | A | the behaviour runs to completion | Bingo is cut at about 9.8 s by its Stop event, and the tempo audibly differs from A. |
| A3 | **Sustained singing and a parameter posted mid-note** | The streaming render: how far ahead it runs, what the scheduler sends, and what the robot plays | `--obb`, **he moves** | A | the song runs to completion with no underruns reported | Continuous sound with no alternating silence, no note that hangs or repeats, no reverb-like smear, and an audible change when the vibrato is posted. |

### 5. basic motion

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| MOV | **Head, lift and a short drive** | SetHeadAngle, SetLiftHeight, DriveWheels and the robot's own action acknowledgements | **he moves** | LINK, D | every head, lift, wheel and stop action succeeds and the motion tool reports its terminal success marker | The head tilts down and back, the lift rises and lowers, then he drives forward a few centimetres and back, and stops. Nothing keeps moving afterwards. |

### 6. idle and live animation

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| CR1 | **A live body shuffle stops at its duration** | The animation scheduler's live keyframe path and the tick loop that serves its deadline | **he moves** | MOV | the wheels move and are back at zero half a second after the keyframe's duration | He shuffles forward for about a second and stops by himself. He does not keep creeping. |
| IDL | **Idle keeps him alive without wandering** | The idle behaviour: keep-alive blinks, small body shuffles and head moves | `--obb`, **he moves** | CR1 | the idle layer took at least one action of its own ("idle actions taken" is not zero) | He blinks and makes small movements, and he is still roughly where he started after a minute. Nothing repeats mechanically and nothing runs on. |
| CR3 | **A cancelled animation stops sending** | The scheduler's emission gate: a command of a cancelled playback must not get out | `--obb`, **he moves** | F, MOV | playback is active before Stop; no later keyframe fires, the ticker ends and telemetry settles | He stops when the animation is cut and stays stopped. No late twitch of the head, the lift or the wheels. |
| CR2 | **A dropped link ends the animation, not the process** | The animation tick loop's failure boundary when the robot goes away mid-clip | `--obb`, **he moves** | F | playback is active before disconnect; the animation/ticker end and Faulted reports the link loss | He is moving, and at the moment the tool says it is dropping the link he stops where he is and stays stopped - no twitching, no carrying on, no running away. The tool then prints its own verdict and the campaign moves to the next check instead of dying. |

### 7. camera

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| E | **Colour camera frames** | The colour path of the camera decoder | - | LINK | colour frames decode and the local-policy diagnostic JPEG has nominal 320x240 geometry while raw encoded bytes stay half-width | The saved files are colour photographs of the room, not tinted or scrambled. |

### 8. markers, cubes and the world model

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| B | **Cube telemetry** | Cube discovery, connection and the tap, movement, up-axis and battery reports | cube, you handle him | LINK | the production auto pool sends SetPropSlot, a cube connects and cube telemetry is observed | The events printed match what you did, and at least one cube shows as connected. |
| K | **Camera calibration and cube localisation** | The NV calibration read, marker detection and BlockWorld's pose estimate | cube, you handle him | B | the calibration is read from the robot and at least one cube becomes Known with a pose | Marker codes match the faces shown, the printed distance matches a ruler within about 5 percent, and the yaw matches how the cube is turned. |

### 9. the charger

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| V | **Mount the charger** | The charger object, the align dock with the real 20 x 27 mm marker, and the backwards mount | charger, you handle him, **he moves** | K | the charger is located, the align dock runs and the contacts report on charger | He stops in front of the charger, turns around, backs on and the contacts engage: his backpack lights change. A miss drives forward 120 mm and retries. |
| W | **Drive off the charger** | DriveOffChargerContactsAction and the on-charger flag | charger, **he moves** | V | one 156 mm line at 20 mm/s and the on-charger flag clears | He drives forward off the charger at a crawl and stops on his treads. |

### 10. robot state and reactions

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| G | **Off-treads transitions** | The off-treads classifier: on treads, in air, on back, on face, on each side | you handle him | LINK | the classifier enables and reports being in the air and at least one resting state (on his back, face or side) | A transition prints for each handling and nothing prints while he sits still. On-back arrives about a second after laying him down, sides and face after a quarter second. |
| C | **Falling then impact** | The falling and impact reports, and the rule that the reaction waits for the landing | you handle him, `--obb` | G | the falling reaction fires during the window the tool opens for the drop | He reacts when he lands, not while he is in the air. |
| H | **The derived-state reactions** | The reactions built on derived state: on back, on face, on side, slope, shaken, returned to treads | you handle him, `--obb`, **he moves** | G | every reaction the check names fires during its own prompt, and no other reaction can stand in for it | On his back he flips down; on his face he rolls; on a side he asks to be righted; on a slope he reacts then checks his pitch; shaken then set down he acts dizzy. Nothing fires for a state he is not in. |
| I | **StartMotorCalibration honoured** | StartMotorCalibration (0x58) and the MotorCalibration reports the robot answers with | **he moves** | LINK | the head reports calibration started and then finished, after the request and not before it | After ASKING NOW, his head nods down to its stop and comes back, within a few seconds. Nothing else moves. |
| J | **Unexpected movement while driving** | The unexpected-movement detector and its reaction | you handle him, `--obb`, **he moves** | H | the unexpected-movement reaction fires during the window in which the tool drives the wheels | He plays the startled reaction once he is held, and nothing fires while he turns freely. |
| M | **The cube reactions with a real cube** | AcknowledgeObject and ReactToCubeMoved against a real, observed cube | cube, you handle him, `--obb`, **he moves** | K | both named cube reactions fire, each in its own window | He looks at the cube's new place and nods to it. Turned away and hearing it move, he turns back to where it was and reacts to finding or not finding it. |
| CR8 | **Picking him up changes the frame everything is in** | The pose origin in the robot's state, the history's frame, and what the world forgets | cube, you handle him | K | a cube is located before the origin change and that same non-carried object becomes unlocated | Nothing physical to judge beyond the handling itself: the check is whether the software noticed. What you confirm is that you really did lift him and set him down elsewhere. |
| CR7 | **The ground he looks at reaches the map** | The overhead-edge detector, the ground ROI, and the memory map's edge insertion | you handle him | K | frames are processed, edge points are found and the map holds at least one region | Nothing to judge by eye except that he was looking at the edge the whole time; the evidence is in the map's own region counts. |

### 11. navigation

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| L | **SetBodyAngle semantics** | The body-angle message: whether the angle is absolute or relative, and its packing | **he moves** | MOV | the turn matches the request and the tool names the semantics it confirmed | He turns 45 degrees left in place, smoothly, and stops. |
| Q | **Drive to the pre-dock pose** | The path sender, the planner's three segments and DriveToPoseAction | cube, **he moves** | K | the path completes and the drive action succeeds | He turns, drives straight, turns to face the cube and stops about 75 mm from its face. No jerks between segments. |
| CR4 | **A late abort clears its own path and nobody else's** | Path ownership: PathRun, the path id, and the qualified abort | **he moves** | MOV | the late abort sends no clear and the second path completes | He drives the longer path all the way and stops at the end of it, not part way through. |
| X | **The lattice planner on the robot** | The lattice planner's arcs and the firmware's arc segment handling | cube, `--obb`, **he moves** | K, Q | a lattice plan with at least one obstacle, arc segments sent, and the drive succeeds | He curves around the cube in the way and arrives at the pre-dock pose. The arcs are smooth, so the firmware accepted the arc layout. |

### 12. manipulation

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| N | **Pick up a cube** | DockWithObject, the docking error signal and the pick-and-place result | cube, **he moves** | K, Q | the pick-and-place result reports the block picked up | He drives to about 75 mm in front of the face, docks smoothly, lifts the cube and holds it. |
| O | **Place a carried cube on the ground** | PlaceObjectOnGround and the carry state | cube, **he moves** | N | the result reports the block placed and carrying clears | He lowers the lift and backs off the cube, leaving it upright on the floor. |
| P | **Roll a cube** | The roll dock action and the cube's own up-axis report | cube, **he moves** | K | the roll reports success with a change of up axis | He docks and the cube rolls onto another face; the reported up axis changes. |
| R | **Stack two cubes** | The stacking behaviour: pick up, carry, place on top, and the final animation | cube, `--obb`, **he moves** | N, K | the terminal stack result is success and carrying is clear; entering the phases is insufficient | He picks one up, carries it to the other and places it on top. |
| S | **Flip a cube** | The flip pre-action pose at the cube's corner and the lift-driven flip | cube, **he moves** | K | the flip action reports success | He drives at the cube's corner with the lift low, the lift comes up as he reaches it, and the cube tips over his shoulder. He does not stall against it. |
| T | **Knock over a stack** | The knock-over behaviour: the grab attempt, the flip and the success animation | cube, `--obb`, **he moves** | K, S | the stack is recognised and the knock-over reports success | He turns to the stack, drives to about 85 mm, reaches, flips the bottom cube and the stack falls. The success animation plays. |
| U | **Pop a wheelie** | The wheelie dock action, the single retry on a miss, and the cliff-stop re-enable | cube, `--obb`, **he moves** | K | PoppedWheelie appears and the cliff stop is re-enabled on stop | He docks, rides up onto the cube's edge and drops back. A miss plays the realign animation and retries once at most. |

### 13. freeplay

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Z | **Freeplay on the robot** | The whole autonomy stack: needs, activities, choosers, behaviours and reactions | cube, charger, you handle him, `--obb`, **he moves** | K | an activity is selected, at least two behaviours actually start and no fatal activity error occurs | He leaves the charger, looks around, goes to the cube and plays with it, pauses and looks around between games, and after the put-down heads for the cube. Nothing repeats back to back. |

### 14. long run and stability

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Z2 | **Fifteen minutes of freeplay, and the mood decaying through it** | Stability over time, and the mood decay that the behaviour scoring reads | cube, you handle him, `--obb`, **he moves** | Z | behaviours execute into the second half and sampled mood values demonstrably decay | He is still deciding and moving at the end, with no long stalls, no repeated behaviour back to back, and no degradation in his voice or his movement. |

### 15. blocked on this build

| # | check | what it tests | you need | after | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Y | **The face pipeline with a detector** | FaceWorld, the face actions and the face reactions | - | - | not run | BLOCKED_EXTERNAL: VisionSystem.FaceDetector is the OKAO boundary and reports itself unavailable. Attach an IFaceDetector implementation and this check becomes runnable. You will not be asked to perform it. |


## The core-review corrections on hardware

The correction pass produced eleven findings, all of them covered by offline regressions. Nine have a
hardware check, because hardware can observe something the offline test cannot:

| finding | check | what hardware adds |
| --- | --- | --- |
| CORE-001 live body motion stops at its duration | CR1, IDL | whether the wheels actually stop, and whether he stays where he was left |
| CORE-002 a dropped link ends the animation | CR2 | a real link going, not a simulated one |
| CORE-003 a cancellation leaks no commands | CR3 | whether anything reaches the motors after the stop |
| CORE-004 a late abort clears only its own path | CR4 | whether the replacement drive actually completes |
| CORE-005 the charger's real marker geometry | V | the solved distance against a real charger, and whether the marker stays in view |
| CORE-006 streaming audio under back-pressure | A, A2, A3 | chopping, hanging notes and an audible parameter change, which only a listener can judge |
| CORE-007 overhead edges reach the map | CR7 | a real floor under real light |
| CORE-008 the localization origin | CR8 | a delocalization needs hands |
| CORE-009 the mood decays over time | Z, Z2 | fifteen minutes of real running |

Two have no hardware check on purpose. **CORE-010** (a disposed manipulation system stops deciding things)
and **CORE-011** (a public subscriber cannot interrupt the robot's own routing) are internal lifetime and
event-isolation properties: a physical test would be watching for the absence of something, with no way to
tell it apart from an ordinary quiet run. Their offline regressions are the evidence, and no hardware check
pretends otherwise.


## What G and H cannot tell you, and what would

The classifier's thresholds are the engine's; what a run settles is that this robot's IMU units and mounting put
its resting states where the engine expects them (gravity read about 10500, not 9800, in the committed captures,
which the 3000-wide side band still accommodates). If a state never appears, or appears for the wrong handling,
the first place to look is `offtreads` output's pitch and filtered-accel columns against `DERIVED_STATE.md` §2.3,
not the constants.

## What K cannot tell you, and what would

The decoder and the tables are the engine's; what K settles is the LOCAL front end against real optics — whether
the black squares are found at Cozmo's exposure and blur. If markers are missed, `vision --images <saved jpeg>`
prints every quad and why it was rejected, and `--out` writes the dark mask; the first knob is
`QuadDetectorParameters.DarkThresholdMultiplier` (0.75, ours), not the decoder. Pose accuracy against a ruler
tests the calibration read from NV and the head-camera geometry together; a constant offset in X with a good yaw
points at the calibration, a distance error growing with head angle at the camera pose.

## What N–R cannot tell you, and what would

The exchange is the engine's, but three messages carry fields whose meaning was not read and are sent as zero
(`MANIPULATION.md` §4). A dock that never starts, or a `PickAndPlaceResult` with `result` ≠ 0 at once, points at
`DockWithObject`'s fourth float or trailing bytes; the `result` byte printed is the firmware's `DockingResult`,
whose enum this build does not decode, so record it. A path the robot drives but ends off-pose points at the
planner's segment endpoints (the layouts are the engine's; the planner is ours).

## What Z cannot tell you, and what would

The tree, the scores, the penalties and the needs configs are the engine's; the ordering of the
desired-from-objects activity against the priority order, the null-pick switch and the obstacle hook are
INFERRED (`FREEPLAY.md` §3). A robot that never leaves Hiking while a cube is in view points at
`DesiredActivityFromObjects` / the PlayAlone strategy inputs; one that repeats the same behaviour points at the
repetition penalty's clock; one that stalls with "Picked no activity" points at a strategy's cooldown or an
unbound behaviour id (`freeplay --tree` lists them). The needs decay is per minute: a five-minute run shows
almost none of it, so judge the needs activities by forcing a level (`--simulate` does this offline).

## What S–X cannot tell you, and what would

The lattice planner's padding, heuristic and arc reconstruction are labelled (`NAVIGATION.md` §3): a robot that
clips the cube it plans around points at the padding; one that overshoots an arc's end points at the arc layout
or the reconstruction. The charger's pre-dock pose is INFERRED: if the align refuses to start from where he
stops, move him by hand to about 120 mm in front of the marker and rerun; success from there isolates the
pose. Mount success is judged from IS_ON_CHARGER; the firmware's own charger-contact handling (stopping the
backwards drive) is what a real mount depends on and cannot be seen offline.

## What A cannot tell you, and what would

The sampler's rules are Wwise runtime semantics taken from public documentation (`WWISE_MUSIC.md` §3):
whether notes sustain and release the way the stock app made them, whether the level is right, and
whether the get-in branch is silent during a song. A recording of the stock app singing the same song
would settle those; if one can be made (the original app still runs on some phones), put it beside the
`--render` WAV of the same song and compare by ear.

## Results

Filled in by the runner, not by hand: `results.json` and `SUMMARY.md` in the run directory are the record,
and `hardware-test --status` prints the same thing at any time. Until a campaign is run, every check above is
"offline-verified, hardware pending", and no milestone's freeze depends on it.
