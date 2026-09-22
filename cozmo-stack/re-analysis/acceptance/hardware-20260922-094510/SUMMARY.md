# Cozmo hardware acceptance

* robot: `172.31.1.1`
* started: 2026-09-22 14:45:10Z, not finished
* evidence: `C:\Users\JimBu\source\repos\jimbuschman\cozmo-stack\cozmo-stack\re-analysis\acceptance\hardware-20260922-094510`

**0 passed, 0 failed, 0 inconclusive, 0 unsure, 1 interrupted, 0 blocked, 0 skipped, 39 not run** of 40.

Hardware acceptance records what was observed. It does not change any fidelity record's
status: a check that passes does not upgrade provenance, and one that fails is an
investigation item, not a licence to tune a source-backed constant.

| id | phase | check | status | detail |
| --- | --- | --- | --- | --- |
| LINK | 1. connection and telemetry | Connection, handshake and telemetry | INTERRUPTED | the link to the robot was lost while the check was running |
| D | 1. connection and telemetry | Lift position readout | PENDING |  |
| F | 2. animation controller | Animation timeline | PENDING |  |
| FD | 3. face display | Face display | PENDING |  |
| AUD | 4. audio | Speaker and the audio frame pacing | PENDING |  |
| A | 4. audio | Cozmo sings | PENDING |  |
| A2 | 4. audio | One song from each tempo group | PENDING |  |
| A3 | 4. audio | Sustained singing and a parameter posted mid-note | PENDING |  |
| MOV | 5. basic motion | Head, lift and a short drive | PENDING |  |
| CR1 | 6. idle and live animation | A live body shuffle stops at its duration | PENDING |  |
| IDL | 6. idle and live animation | Idle keeps him alive without wandering | PENDING |  |
| CR3 | 6. idle and live animation | A cancelled animation stops sending | PENDING |  |
| CR2 | 6. idle and live animation | A dropped link ends the animation, not the process | PENDING |  |
| E | 7. camera | Colour camera frames | PENDING |  |
| B | 8. markers, cubes and the world model | Cube telemetry | PENDING |  |
| K | 8. markers, cubes and the world model | Camera calibration and cube localisation | PENDING |  |
| V | 9. the charger | Mount the charger | PENDING |  |
| W | 9. the charger | Drive off the charger | PENDING |  |
| G | 10. robot state and reactions | Off-treads transitions | PENDING |  |
| C | 10. robot state and reactions | Falling then impact | PENDING |  |
| H | 10. robot state and reactions | The derived-state reactions | PENDING |  |
| I | 10. robot state and reactions | StartMotorCalibration honoured | PENDING |  |
| J | 10. robot state and reactions | Unexpected movement while driving | PENDING |  |
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
