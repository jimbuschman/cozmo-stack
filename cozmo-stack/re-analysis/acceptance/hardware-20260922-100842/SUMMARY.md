# Cozmo hardware acceptance

* robot: `172.31.1.1`
* started: 2026-09-22 15:08:48Z, not finished
* evidence: `C:\Users\JimBu\source\repos\jimbuschman\cozmo-stack\cozmo-stack\re-analysis\acceptance\hardware-20260922-100842`

**6 passed, 1 failed, 3 inconclusive, 3 unsure, 1 interrupted, 9 blocked, 0 skipped, 17 not run** of 40.

Hardware acceptance records what was observed. It does not change any fidelity record's
status: a check that passes does not upgrade provenance, and one that fails is an
investigation item, not a licence to tune a source-backed constant.

| id | phase | check | status | detail |
| --- | --- | --- | --- | --- |
| LINK | 1. connection and telemetry | Connection, handshake and telemetry | PASSED |  |
| D | 1. connection and telemetry | Lift position readout | UNSURE | not sure what i was supposed to do |
| F | 2. animation controller | Animation timeline | PASSED |  |
| FD | 3. face display | Face display | PASSED |  |
| AUD | 4. audio | Speaker and the audio frame pacing | PASSED |  |
| A | 4. audio | Cozmo sings | PARTIAL | i don't think the tune was right, might need to do another test  |
| A2 | 4. audio | One song from each tempo group | BLOCKED | A (Cozmo sings) was inconclusive, and A2 depends on it |
| A3 | 4. audio | Sustained singing and a parameter posted mid-note | BLOCKED | A (Cozmo sings) was inconclusive, and A3 depends on it |
| MOV | 5. basic motion | Head, lift and a short drive | BLOCKED | D (Lift position readout) has not run yet, and MOV depends on it |
| CR1 | 6. idle and live animation | A live body shuffle stops at its duration | BLOCKED | MOV (Head, lift and a short drive) is blocked, and CR1 depends on it |
| IDL | 6. idle and live animation | Idle keeps him alive without wandering | BLOCKED | CR1 (A live body shuffle stops at its duration) is blocked, and IDL depends on it |
| CR3 | 6. idle and live animation | A cancelled animation stops sending | BLOCKED | MOV (Head, lift and a short drive) is blocked, and CR3 depends on it |
| CR2 | 6. idle and live animation | A dropped link ends the animation, not the process | UNSURE | not sure exactly what was supposed to happen. seemed like it starteed an animation and ended it early? |
| E | 7. camera | Colour camera frames | PARTIAL | picture looks bad, glitched |
| B | 8. markers, cubes and the world model | Cube telemetry | UNSURE | seemed to have seen the cube but didn't connect, idk if this is a pass or not |
| K | 8. markers, cubes and the world model | Camera calibration and cube localisation | BLOCKED | B (Cube telemetry) has not run yet, and K depends on it |
| V | 9. the charger | Mount the charger | BLOCKED | K (Camera calibration and cube localisation) is blocked, and V depends on it |
| W | 9. the charger | Drive off the charger | BLOCKED | V (Mount the charger) is blocked, and W depends on it |
| G | 10. robot state and reactions | Off-treads transitions | PASSED |  |
| C | 10. robot state and reactions | Falling then impact | FAILED | only reacted when picked up, never reacted on being set down or falling |
| H | 10. robot state and reactions | The derived-state reactions | PASSED |  |
| I | 10. robot state and reactions | StartMotorCalibration honoured | PARTIAL | this seemed to run the wrong test |
| J | 10. robot state and reactions | Unexpected movement while driving | INTERRUPTED | stopped part way (emergency stop or Ctrl+C) |
| M | 10. robot state and reactions | The cube reactions with a real cube | PENDING |  |
| CR8 | 10. robot state and reactions | Picking him up changes the frame everything is in | PENDING |  |
| CR7 | 10. robot state and reactions | The ground he looks at reaches the map | PENDING |  |
| L | 11. navigation | SetBodyAngle semantics | PENDING |  |
| Q | 11. navigation | Drive to the pre-dock pose | PENDING |  |
| CR4 | 11. navigation | A late abort clears its own path and nobody else's | PENDING |  |
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
| Y | 15. blocked on this build | The face pipeline with a detector | PENDING |  |

## Investigation items

### A — Cozmo sings

* what it tests: The Wwise switch, the per-note vocal sampler and the singing behaviour's animations
* expected in the room: About 12 seconds of tune between a get-in and a get-out, in his own voice, one note per note, at a sensible level, with no bursts of get-in phrases during the song.
* what was seen: i don't think the tune was right, might need to do another test 
* related fidelity records (unchanged by this result): M9-016
* core-review corrections exercised: CORE-006
* evidence: `tests/A/`

### E — Colour camera frames

* what it tests: The colour path of the camera decoder
* expected in the room: The saved files are colour photographs of the room, not tinted or scrambled.
* what was seen: picture looks bad, glitched
* related fidelity records (unchanged by this result): M3-003
* evidence: `tests/E/`

### C — Falling then impact

* what it tests: The falling and impact reports, and the rule that the reaction waits for the landing
* expected in the room: He reacts on landing, not while falling.
* what was seen: only reacted when picked up, never reacted on being set down or falling
* the tool said: exit 0
* related fidelity records (unchanged by this result): M7-009
* evidence: `tests/C/`

### I — StartMotorCalibration honoured

* what it tests: The motor calibration request and the robot's calibration reports
* expected in the room: The head visibly recalibrates, nodding to its stop, within a few seconds of the request.
* what was seen: this seemed to run the wrong test
* related fidelity records (unchanged by this result): M4-004
* evidence: `tests/I/`


## Blocked

* **A2** One song from each tempo group — A (Cozmo sings) was inconclusive, and A2 depends on it
* **A3** Sustained singing and a parameter posted mid-note — A (Cozmo sings) was inconclusive, and A3 depends on it
* **MOV** Head, lift and a short drive — D (Lift position readout) has not run yet, and MOV depends on it
* **CR1** A live body shuffle stops at its duration — MOV (Head, lift and a short drive) is blocked, and CR1 depends on it
* **IDL** Idle keeps him alive without wandering — CR1 (A live body shuffle stops at its duration) is blocked, and IDL depends on it
* **CR3** A cancelled animation stops sending — MOV (Head, lift and a short drive) is blocked, and CR3 depends on it
* **K** Camera calibration and cube localisation — B (Cube telemetry) has not run yet, and K depends on it
* **V** Mount the charger — K (Camera calibration and cube localisation) is blocked, and V depends on it
* **W** Drive off the charger — V (Mount the charger) is blocked, and W depends on it
