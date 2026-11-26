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
| Cubes | AcknowledgeObject, ReactToCubeMoved, PutDownBlock, RollBlock, StackBlocks, DriveOffCharger | robot for pickup, put-down, drive-off, roll (2026-11-16 again); wheelie: engine success 2026-11-16; flip cancelled by a reaction mid-turn (FLIP-REACTION-SHIELD-1); stack: needs a second live cube; ReactToCubeMoved not shown (no cube messages in its window; cube tap has no reaction in the shipped map, to confirm); sim for the rest |

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
  Operator 2026-11-26: mapping is a main improvement area ("we can do a much better job"). Candidates: a persistent
  room map that survives pickups (re-anchor the old origin when the charger or a known cube is seen again, instead of
  discarding it); the charger remembered permanently, its pose averaged over sightings and held flat (feeds docking);
  longer memory of cliffs, edges and obstacles; the map saved between sessions.
- Charger docking (operator 2026-11-26): if CHARGER-MOUNT-SESSION6-1 shows the engine fails the same way, dock only from
  a flat, square-on, multi-sighting charger estimate and keep reactions locked for the whole reverse climb (the by-hand
  climb in session 6 worked this way).
- Person tracking (operator 2026-11-26): if TRACK-FACE-LLM-2 shows the engine's one-ID tracking is the limit, follow the
  face nearest the last tracked position across face-ID changes.
- Drive on and off the charger by command, including a wake-and-drive-off request during `ReactToOnCharger`
  (operator, 2026-11-19 evening session, F8/F21).

## D. Deferred (not worked unless the operator moves them)

BuildPyramid, BuildPyramidBase, PyramidThankYou, ReactToPyramid, RespondPossiblyRoll, OnConfigSeen,
CheckForStackAtInterval, CantHandleTallStack, ReactToStackOfCubes, ReactToSparked and the Sparks flows,
RequestGameSimple (10 IDs), FeedingEat, FeedingSearchForCube, OnboardingShowCube, LookInPlaceMemoryMap,
ExploreVisitPossibleMarker, PickUpAndPutDownCube.

PickUpCube (operator 2026-11-21): moved here from B. Freeplay chooses it only inside BuildPyramid's build phase
(0x005A888C..0x005A88B2, 0x005A8BB8..0x005A8BE4) or the Sparks stack flow, both deferred, so no ordinary session can
reach it. Commanded pickup stays in A (finished). Revisit with BuildPyramid after A and B, alongside C2.

## E. Excluded

FactoryTest, FactoryCentroidExtractor, LiftLoadTest, DevTurnInPlaceTest, DockingTestSimple (as a behaviour; the
charger mount action stays in A).

## Count

Robot acceptance now comes from ordinary MCP sessions, scored by `re-analysis/tools/session_check.py` against the
engine-sourced round expectations (operator 2026-11-20; combined table
`re-analysis/jobs/trial-m13/session-check-1/combined.md`, four sessions 2026-11-19/20).

**2026-11-26 (sessions 6 and 7, notes `re-analysis/research/20261126-llm-session-6-track-face-notes.md` and
`20261126-llm-session-7-notes.md`; stop rule adopted by the operator 2026-11-26):**
- **A 16 of 16, B 40 of 40, counting items that match the engine.** Under the stop rule, an item whose robot failure
  the original engine shares counts as done; the improvement goes to C2.
  - Passed on the robot in session 7: say-text pre-empts Freeplay, track-face cancel, InteractWithFaces, ExpressNeeds,
    ExploreLookAroundInPlace, PlayArbitraryAnim, EarnedSparks (spark request), DriveOffCharger by request.
  - Done as matching the engine: mount the charger (CHARGER-MOUNT-SESSION6-1), track-face following across face-ID
    changes (TRACK-FACE-LLM-2), PlacedOnSlope (SLOPE-PUTDOWN-RACE-1), SearchForFace and DriveToFace readiness
    (VC-REQUEST-NOT-RUNNABLE-1), PlayAnimWithFace turn without a face pose (PLAYANIMWITHFACE-STALE-FACE-1).
  - Their C2 improvements are queued (C2-* rows), with C2-CLIFF-HOLD-STILL-1 first (session 7 fall off the desk,
    CLIFF-REACTION-OFF-EDGE-1 matches the engine).
  - Open robot issues outside the count: silent bridge drops (now recorded by the supervisor, cause unknown) and the
    disconnect after sleeping on the charger (SLEEP-CHARGER-DISCONNECT-1).

**2026-11-23 (session 5: bundles 20261123-084806 and -091241, notes `Downloads\runs\20261123-llm-session-5-notes.md`,
scored with session_check.py):**
- **Passed on the robot: A 14 of 16, B 31 of 40.**
  - A newly passed: speech, teach/rename/erase, go to a place, roll, stack.
  - A not passed: mount the charger (CHARGER-MOUNT-REVERSE-1); face turn/track (track not run; cancel tool in
    LLM-WAKE-CANCEL-1).
  - B newly passed: OnFace, OnSide, Shaken, Frustration; PlayAnimOnNeedsChange, DriveInDesperation; Wait,
    VisitInterestingEdge; RespondToRenameFace; PutDownBlock, StackBlocks, DriveOffCharger, ReactToCubeMoved,
    AcknowledgeObject (stacked branch not exercised).
  - B reopened: InteractWithFaces (the checker passes the entry, but the named branch was never reached:
    TurnTowardsLastFacePose NO_FACE; INTERACT-FACES-NO-FACE-1).
  - B not passed (9): PlacedOnSlope (PLACED-ON-SLOPE-INPUTS-1), InteractWithFaces, ExpressNeeds, EarnedSparks,
    PlayAnimWithFace, PlayArbitraryAnim, ExploreLookAroundInPlace, SearchForFace, DriveToFace.
- **Finished** additionally needs no MISSING on the path. The 2026-11-23 sweep queued every on-path MISSING site
  (error branches included) as 18 Codex jobs; a passed item becomes finished when the jobs on its path merge and,
  where a job changes behaviour, a session rechecks it. Hit live in session 5 on passed items: reaction gates
  vtable+0x24/+0x28 for Cliff/OnBack/Impact/MotorCalibration (REACTION-GATES-NATIVE-1), Pounce live motion producer
  M11-098/M15-107 and factory guard (IDLE-BEHAVIOUR-INPUTS-1), pyramid container M12-043 in PlayAlone/StackBlocks
  (FREEPLAY-ACTIVITY-INPUTS-1), needs cache M13-039 (SEVERE-NEEDS-OWNER-1), cube lights M11-004/M4-040/M4-018 and
  world checks M11-037/M11-069 (CUBE-WORLD-LIGHTS-1), pickup completion M1-034 (DOCK-ERRORS-1).

**2026-11-21:** 16 command items (A) and 40 behaviour classes (B; PickUpCube moved to D by the operator 2026-11-21).
- **A, finished (passed in a session, no MISSING on the path, main path spot-read):** 9. Events; charger seen; the
  connection for an LLM; colour camera; reaction locks and wait until accepted; head/lift/drive/turn; play an
  authored animation; pick up and put down (the shared action-queue marker closed by SHARED-QUEUE-MISSING-1:
  VizManager send is return-only at 0x006BE610, SDK status only in excluded SDK mode). Freeplay return after a command (2026-11-19 182433 session: head command switched Freeplay to Selection/Wait and the activity resumed on its 0x5A completion; A-UNEXERCISED-READY-1, G2E 0x93 handler 0x0060AAAE..0x0060AAC8 spot-read 2026-11-21; Selection-owner guard M15-031 is diagnostic only).
- **A, seen but not passed:** 6. Face turn/cube turn/track (cube turn and track not passed); speech (completion
  report fixed 2026-11-20, needs a new session); teach/rename/erase (rename and erase not tried); mount the charger
  (the engine's own results, needs a clear desk); roll; stack.
- **A, not exercised:** go to a place (fixture and session round built, A-UNEXERCISED-READY-1).
- **B, finished (passed in a session, no MISSING on the path, main path spot-read against the binary 2026-11-20):**
  18 of 40 classes. Freeplay loop; ReactToCliff, Pickup, RobotOnBack, ReturnedToTreads, Impact, UnexpectedMovement,
  MotorCalibration, OnCharger; PlayAnim, FindFaces, LookForFaceAndCube, PounceOnMotion, ThinkAboutBeacons;
  AcknowledgeFace, InteractWithFaces, EnrollFace, ReactToPet; RollBlock.
- **B, seen but not passed (2026-11-22 path trace):** AcknowledgeObject. Its stacked-cube look hit
  "MISSING: M15-081 ... M11-010 ghost visibility reason" after the reaction (20261120-094700 stack-log 4236); the checker
  did not count it. ACK-STACKED-LOOK-1.
- **B, passed; spot-read difference fixed 2026-11-20 (CUBEMOVED-PRESENCE-1), presence branch to recheck next session:** ReactToCubeMoved. Its presence branch played AcknowledgeObject
  without the engine's lift-safe action (0x00602526..0x00602546; CUBEMOVED-PRESENCE-1), and reaches the same stacked-cube
  look gap (ACK-STACKED-LOOK-1).
- **B, not exercised:** 20 classes (OnFace, OnSide, Shaken, PlacedOnSlope, Frustration; the four Needs classes;
  PlayAnimWithFace, PlayArbitraryAnim, Wait, ExploreLookAroundInPlace, VisitInterestingEdge; SearchForFace,
  DriveToFace, RespondToRenameFace; PutDownBlock, StackBlocks, DriveOffCharger). All 20 now have offline replays
  or fixtures and session rounds (PHYS-REACTIONS-READY-1, NEEDS-PERSONALITY-1, IDLE-LIFE-1, FACES-PERSONALITY-1,
  CUBE-FREEPLAY-READY-1; the rest queued); they need a robot session.
