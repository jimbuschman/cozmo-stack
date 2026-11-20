# Scope: what "done" contains (draft for the operator, 2026-11-15)

Goal (operator, 2026-11-03): Cozmo is the body for an outside LLM and keeps his own personality. The LLM decides what
he does; the stack does it the way the engine does.

State labels:
- **robot**: seen working on the operator's robot;
- **sim**: built from the binary and passing simulated tests, never (or not recently) run on the robot;
- **partial**: some code, known holes;
- **none**: no code.

The 78 behaviour classes cover all 178 shipped behaviour IDs. States come from the code and the robot bundles in
`re-analysis/acceptance/hardware/`, not from record counts. Drafted in one pass; a state marked sim may turn out
partial when it is run.

## A. Commands and events for the LLM (required)

Robot script: `m15-cube-hw --commands` (built 2026-11-16) runs every command below once and records the events.

| Item | State |
| --- | --- |
| Head, lift, drive straight, turn in place | robot (2026-11-16, runs 20261116-110735 / -111516: engine success and operator yes) |
| Turn to a face, turn to a cube, track a face | turn to a cube: robot (2026-11-16); face turn and track: seen in LLM sessions 2026-11-19/20 (re-analysis/research/20261119-llm-session-2-notes.md, 20261120-llm-session-3/4-notes.md; operator present, observations, not a scripted pass) (turn-to-face SUCCESS with facePoseAvailable; operator saw tracking); scripted check still to run (session_check.py, SESSION-CHECK-1) |
| Play an authored animation | seen with sound in LLM sessions 2026-11-19/20 (re-analysis/research/20261119-llm-session-2-notes.md, 20261120-llm-session-3/4-notes.md; operator present, observations, not a scripted pass) (MajorWin, CodeLabHappy, lift-effort sound heard; the mambo's backing music is the app's CodeLab block, not the animation: MUSIC-AUDIO-1); session_check.py check still to run (SESSION-CHECK-1) |
| Say a sentence in his voice | robot (2026-11-09; heard again in LLM sessions 2026-11-19/20 (re-analysis/research/20261119-llm-session-2-notes.md, 20261120-llm-session-3/4-notes.md; operator present, observations, not a scripted pass)); first sound now ~3 s offline (SPEECH-EMU-SPEED-1, was 9-17 s on the robot), robot check of the faster speech pending |
| Pick up a cube, put it down | robot (2026-11-09; again 2026-11-15, run 20261115-162141) |
| Events: face seen / recognised / lost, cube seen / moved / tapped, object pose lost | every kind seen in LLM sessions 2026-11-19/20 (re-analysis/research/20261119-llm-session-2-notes.md, 20261120-llm-session-3/4-notes.md; operator present, observations, not a scripted pass), pushed live on 2026-11-20 (EVENT-PUSH-1); session_check.py check still to run (SESSION-CHECK-1) |
| Teach, rename and erase a face | teach: worked in LLM sessions 2026-11-19/20 (re-analysis/research/20261119-llm-session-2-notes.md, 20261120-llm-session-3/4-notes.md; operator present, observations, not a scripted pass) (enrolled Jim, then recognised); rename and erase not yet shown on the robot; session_check.py check still to run (SESSION-CHECK-1) |
| Go to a place (QueueAiGotoPoseAsync) | robot (2026-11-16, runs 20261116-110735 / -111516: success in both runs, operator yes) |
| Return to and mount the charger (QueueAiMountChargerAsync) | not passed on the robot: 2026-11-16 docking signals stopped (MOUNT-DOCK-SIGNALS-1); 2026-11-19 four failures matching the engine on a tilted first charger estimate (DOCK-CHARGER-POSE-1, WHEELIE-CHARGER-PATH-1); 2026-11-20 cable snag, 8.7 mm approach miss, two cliff stops (CHARGER-MOUNT-CLIFF-1 reads the engine's docking reaction handling before any further attempt). Docking by plain drives worked twice |
| Roll a cube (QueueAiRollCubeAsync) | robot (2026-11-15, run 20261115-162141: drove to the cube and rolled it) |
| Stack a cube (QueueAiStackOnCubeAsync) | seen in LLM sessions 2026-11-19/20 (re-analysis/research/20261119-llm-session-2-notes.md, 20261120-llm-session-3/4-notes.md; operator present, observations, not a scripted pass) (onto an unpowered cube, success on the second try after the target's pose shifted 32 mm); session_check.py check still to run (SESSION-CHECK-1) |
| Charger seen report (operator 2026-11-15) | seen in LLM sessions 2026-11-19/20 (re-analysis/research/20261119-llm-session-2-notes.md, 20261120-llm-session-3/4-notes.md; operator present, observations, not a scripted pass) (charger-seen events and boxes; detection range ~15-25 cm, DOCK-CHARGER-POSE-1) |
| Freeplay on/off, and returning to Freeplay after a command | on/off seen in LLM sessions 2026-11-19/20 (re-analysis/research/20261119-llm-session-2-notes.md, 20261120-llm-session-3/4-notes.md; operator present, observations, not a scripted pass); return after a command not yet shown (FREEPLAY-LLM-1); `m15-cube-hw --freeplay-control` |
| The connection for an LLM (bridge) | robot (MCP server; LLM sessions 2026-11-18 and 2026-11-19, operator present) |
| Colour camera (operator 2026-11-19: "why wouldn't we want color?") | seen in LLM sessions 2026-11-19/20 (re-analysis/research/20261119-llm-session-2-notes.md, 20261120-llm-session-3/4-notes.md; operator present, observations, not a scripted pass) (encoding 9 colour frames with boxes; COLOR-CAMERA-1) |
| Reaction locks while the LLM does a task, and "wait until accepted" (operator 2026-11-19, F16) | seen in LLM sessions 2026-11-19/20 (re-analysis/research/20261119-llm-session-2-notes.md, 20261120-llm-session-3/4-notes.md; operator present, observations, not a scripted pass) (locks stopped cube interruptions during docking; waitAdmittedMs waited out a pet reaction; REACTION-LOCK-LLM-1) |

## B. Personality (required)

Robot script: `m15-cube-hw --personality` (built 2026-11-15) walks through every group below with the engine's own reports.

| Group | Classes | State |
| --- | --- | --- |
| Freeplay loop, activities, needs, mood | (system) | robot (2026-11-03, 4 min; 15 min check Z2 not run) |
| Physical reactions | ReactToCliff, ReactToPickup, ReactToRobotOnBack/OnFace/OnSide, ReactToRobotShaken, ReactToReturnedToTreads, ReactToImpact, ReactToPlacedOnSlope, ReactToUnexpectedMovement, ReactToMotorCalibration, ReactToOnCharger, ReactToFrustration | robot 2026-11-16 (run 20261116-112021): OnBack, OnSide, OnFace, Shaken, Cliff seen by the operator; ReactToPickup fired but its animation aborted on the M14-006 audio gate (scripts fixed b486fe42), re-run; ReturnedToTreads ran (no animation in the engine, to confirm); sim for the rest |
| Needs | ExpressNeeds, PlayAnimOnNeedsChange, DriveInDesperation, EarnedSparks | sim (DriveInDesperation's cube search built 2026-11-16; untargeted search never succeeds, as in the engine) |
| Idle life | PlayAnim, PlayAnimWithFace, PlayArbitraryAnim, Wait, ExploreLookAroundInPlace, FindFaces, LookForFaceAndCube, VisitInterestingEdge, PounceOnMotion, ThinkAboutBeacons | robot for Pounce, VisitInterestingEdge, FindFaces; sim for the rest |
| Faces | AcknowledgeFace, InteractWithFaces, SearchForFace, DriveToFace, EnrollFace, RespondToRenameFace, ReactToPet | sim |
| Cubes | AcknowledgeObject, ReactToCubeMoved, PickUpCube, PutDownBlock, RollBlock, StackBlocks, DriveOffCharger | robot for pickup, put-down, drive-off, roll (2026-11-16 again); wheelie: engine success 2026-11-16; flip cancelled by a reaction mid-turn (FLIP-REACTION-SHIELD-1); stack: needs a second live cube; ReactToCubeMoved not shown (no cube messages in its window; cube tap has no reaction in the shipped map, to confirm); sim for the rest |

## C. Playful extras (wanted, after A and B)

Commands QueueAiFlipCubeAsync and QueueAiPopAWheelieAsync: sim (built 2026-11-15; rounds in `m15-cube-hw --skills`).
Behaviours PopAWheelie, KnockOverCubes, FistBump, Singing, Dance, CubeLiftWorkout, FireTruckAlarm, BringCubeToBeacon,
RamIntoBlock, Bouncer: sim. PeekABoo, TrackLaser, GuardDog: none or partial.

## C2. Improvements beyond the original (operator 2026-11-19; after A and B)

Operator: "use him as a baseline and then we can improve on some of cozmos abilities/functions rather than staying
locked into what he could do when he was originally written". Each improvement is built on top of the finished
baseline, named, switchable back to the engine's behaviour, and recorded here; the baseline's own tests keep passing.
Candidates so far:
- World map memory: he forgets the charger once it is out of sight or after he is picked up (2026-11-19 session:
  charger dropped from knownChargers after leaving it; objects deleted from old origins at re-localisation).
- Drive on and off the charger by command, including a wake-and-drive-off request during `ReactToOnCharger`
  (operator, 2026-11-19 evening session, F8/F21).

## D. Deferred (not worked unless the operator moves them)

BuildPyramid, BuildPyramidBase, PyramidThankYou, ReactToPyramid, RespondPossiblyRoll, OnConfigSeen,
CheckForStackAtInterval, CantHandleTallStack, ReactToStackOfCubes, ReactToSparked and the Sparks flows,
RequestGameSimple (10 IDs), FeedingEat, FeedingSearchForCube, OnboardingShowCube, LookInPlaceMemoryMap,
ExploreVisitPossibleMarker, PickUpAndPutDownCube.

## E. Excluded

FactoryTest, FactoryCentroidExtractor, LiftLoadTest, DevTurnInPlaceTest, DockingTestSimple (as a behaviour; the
charger mount action stays in A).

## Count

Robot acceptance now comes from ordinary MCP sessions, scored by `re-analysis/tools/session_check.py` against the
engine-sourced round expectations (operator 2026-11-20; combined table
`re-analysis/jobs/trial-m13/session-check-1/combined.md`, four sessions 2026-11-19/20).

**2026-11-20:** 16 command items (A) and 41 behaviour classes (B).
- **A, passed with no unbuilt step on the path (finished):** 5. Events; charger seen; the connection for an LLM;
  colour camera; reaction locks and wait until accepted.
- **A, passed but blocked by one shared MISSING marker** (the action queue's VizManager/SDK-status recipients,
  0x0053F65C..0x0053F6F2; SHARED-QUEUE-MISSING-1): 3. Head/lift/drive/turn; play an authored animation; pick up and
  put down.
- **A, seen but not passed:** 6. Face turn/cube turn/track (cube turn and track not passed); speech (completion
  report fixed 2026-11-20, needs a new session); teach/rename/erase (rename and erase not tried); mount the charger
  (the engine's own results, needs a clear desk); roll; stack.
- **A, not exercised:** go to a place; Freeplay return after a command.
- **B, finished (passed in a session, no MISSING on the path, main path spot-read against the binary 2026-11-20):**
  19 of 41 classes. Freeplay loop; ReactToCliff, Pickup, RobotOnBack, ReturnedToTreads, Impact, UnexpectedMovement,
  MotorCalibration, OnCharger; PlayAnim, FindFaces, LookForFaceAndCube, PounceOnMotion, ThinkAboutBeacons;
  AcknowledgeFace, InteractWithFaces, EnrollFace, ReactToPet; AcknowledgeObject, RollBlock.
- **B, passed but the spot-read found a difference:** ReactToCubeMoved. Its presence branch plays AcknowledgeObject
  without the engine's lift-safe action (0x00602526..0x00602546; CUBEMOVED-PRESENCE-1).
- **B, not exercised:** 21 classes (OnFace, OnSide, Shaken, PlacedOnSlope, Frustration; the four Needs classes;
  PlayAnimWithFace, PlayArbitraryAnim, Wait, ExploreLookAroundInPlace, VisitInterestingEdge; SearchForFace,
  DriveToFace, RespondToRenameFace; PickUpCube, PutDownBlock, StackBlocks, DriveOffCharger).
