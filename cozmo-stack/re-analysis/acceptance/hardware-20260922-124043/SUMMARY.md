# Cozmo hardware acceptance

* robot: `172.31.1.1`
* started: 2026-09-22 17:40:43Z, not finished
* evidence: `C:\Users\JimBu\source\repos\jimbuschman\cozmo-stack\cozmo-stack\re-analysis\acceptance\hardware-20260922-124043`

**10 passed, 2 failed, 1 inconclusive, 6 unsure, 0 interrupted, 1 blocked, 0 skipped, 20 not run** of 40.

Hardware acceptance records what was observed. It does not change any fidelity record's
status: a check that passes does not upgrade provenance, and one that fails is an
investigation item, not a licence to tune a source-backed constant.

| id | phase | check | status | detail |
| --- | --- | --- | --- | --- |
| LINK | 1. connection and telemetry | Connection, handshake and telemetry | PASSED |  |
| D | 1. connection and telemetry | Lift position readout | PASSED |  |
| F | 2. animation controller | Animation timeline | PASSED |  |
| FD | 3. face display | Face display | PASSED |  |
| AUD | 4. audio | Speaker and the audio frame pacing | PASSED |  |
| A | 4. audio | Cozmo sings | UNSURE | not sure if hte tune was correct. speemed like it might have been overlapping hte notes maybe? |
| A2 | 4. audio | One song from each tempo group | PENDING |  |
| A3 | 4. audio | Sustained singing and a parameter posted mid-note | PENDING |  |
| MOV | 5. basic motion | Head, lift and a short drive | PASSED |  |
| CR1 | 6. idle and live animation | A live body shuffle stops at its duration | FAILED | didn't move |
| IDL | 6. idle and live animation | Idle keeps him alive without wandering | PENDING |  |
| CR3 | 6. idle and live animation | A cancelled animation stops sending | UNSURE | not sure exactly what was supposed to happen, seemed like he tried doing an animation and stopped part way tho... |
| CR2 | 6. idle and live animation | A dropped link ends the animation, not the process | UNSURE | same as previous answer, not sure |
| E | 7. camera | Colour camera frames | PARTIAL | garbage pictures.  |
| B | 8. markers, cubes and the world model | Cube telemetry | UNSURE | cubed showed up, didn't connect or anything else |
| K | 8. markers, cubes and the world model | Camera calibration and cube localisation | PENDING |  |
| V | 9. the charger | Mount the charger | PENDING |  |
| W | 9. the charger | Drive off the charger | PENDING |  |
| G | 10. robot state and reactions | Off-treads transitions | PASSED |  |
| C | 10. robot state and reactions | Falling then impact | UNSURE | hard to tell, it might have worked |
| H | 10. robot state and reactions | The derived-state reactions | PASSED |  |
| I | 10. robot state and reactions | StartMotorCalibration honoured | UNSURE | if i manually move the head up, and run this test, it goes down but stays down. never goes back up |
| J | 10. robot state and reactions | Unexpected movement while driving | PASSED |  |
| M | 10. robot state and reactions | The cube reactions with a real cube | PENDING |  |
| CR8 | 10. robot state and reactions | Picking him up changes the frame everything is in | PENDING |  |
| CR7 | 10. robot state and reactions | The ground he looks at reaches the map | PENDING |  |
| L | 11. navigation | SetBodyAngle semantics | PASSED |  |
| Q | 11. navigation | Drive to the pre-dock pose | PENDING |  |
| CR4 | 11. navigation | A late abort clears its own path and nobody else's | FAILED | didn't move |
| X | 11. navigation | The lattice planner on the robot | PENDING |  |
| N | 12. manipulation | Pick up a cube | PENDING |  |
| O | 12. manipulation | Place a carried cube on the ground | PENDING |  |
| P | 12. manipulation | Roll a cube | PENDING |  |
| R | 12. manipulation | Stack two cubes | PENDING |  |
| S | 12. manipulation | Flip a cube | PENDING |  |
| T | 12. manipulation | Knock over a stack | PENDING |  |
| U | 12. manipulation | Pop a wheelie | PENDING |  |
| Z | 13. freeplay | Freeplay on the robot | PENDING |  |
| Z2 | 14. long run and stability | Fifteen minutes of freeplay, and the mood decaying through it | PENDING |  |
| Y | 15. blocked on this build | The face pipeline with a detector | BLOCKED | BLOCKED_EXTERNAL: VisionSystem.FaceDetector is the OKAO boundary and reports itself unavailable. Attach an IFa... |

## Investigation items

### CR1 — A live body shuffle stops at its duration

* what it tests: The animation scheduler's live keyframe path and the tick loop that serves its deadline
* expected in the room: He shuffles forward for about a second and stops by himself. He does not keep creeping.
* what was seen: didn't move
* the tool said: exit 2
* related fidelity records (unchanged by this result): M5-004
* core-review corrections exercised: CORE-001
* evidence: `tests/CR1/`

### E — Colour camera frames

* what it tests: The colour path of the camera decoder
* expected in the room: The saved files are colour photographs of the room, not tinted or scrambled.
* what was seen: garbage pictures. 
* related fidelity records (unchanged by this result): M3-003
* evidence: `tests/E/`

### CR4 — A late abort clears its own path and nobody else's

* what it tests: Path ownership: PathRun, the path id, and the qualified abort
* expected in the room: He drives the longer path all the way and stops at the end of it, not part way through.
* what was seen: didn't move
* the tool said: exit 2
* related fidelity records (unchanged by this result): M12-002
* core-review corrections exercised: CORE-004
* evidence: `tests/CR4/`


## Blocked

* **Y** The face pipeline with a detector — BLOCKED_EXTERNAL: VisionSystem.FaceDetector is the OKAO boundary and reports itself unavailable. Attach an IFaceDetector implementation and this check becomes runnable. You will not be asked to perform it.
