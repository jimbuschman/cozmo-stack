# Combined MCP session checks

Best evidence per check across the four local sessions. A grouped Scope A row passes when every component passes. Sessions are identified by their bundle timestamps. The marker column includes process-wide diagnostics on a verified selected path. No means no such marker was identified from these captures and the source review.

Across 73 individual checks, 42 passed, 7 were seen without a pass, and 24 were not exercised.

## Scope A

| Scope item | Best verdict | Session evidence per component | MISSING on the selected path |
| --- | --- | --- | --- |
| Head, lift, drive straight, turn in place | PASSED | head: PASSED (20261119-093050); lift: PASSED (20261119-094833); drive-straight: PASSED (20261119-182433); turn-in-place: PASSED (20261119-094833) | Yes — head: 20261119-093050 M2862 (shared terminal queue); lift: 20261119-094833 M2643 (shared terminal queue); drive-straight: 20261119-182433 M2751 (shared terminal queue); turn-in-place: 20261119-094833 M2643 (shared terminal queue) |
| Turn to a face, turn to a cube, track a face | SEEN-NOT-PASSED | turn-to-face: PASSED (20261119-094833); turn-to-cube: SEEN-NOT-PASSED (20261119-094833); track-face: SEEN-NOT-PASSED (20261119-094833) | Yes — turn-to-face: 20261119-094833 M2643 (shared terminal queue); turn-to-cube: 20261119-094833 M9921 |
| Play an authored animation | PASSED | animation: PASSED (20261119-094833) | Yes — animation: 20261119-094833 M2643 (shared terminal queue) |
| Say a sentence in his voice | SEEN-NOT-PASSED | say-text: SEEN-NOT-PASSED (20261119-182433) | No |
| Pick up a cube, put it down | PASSED | pickup: PASSED (20261119-094833); put-down: PASSED (20261119-182433) | Yes — pickup: 20261119-094833 M2643 (shared terminal queue); put-down: 20261119-182433 M6022 (ground union, M1-034) |
| Events | PASSED | face-seen: PASSED (20261119-093050); face-recognised: PASSED (20261119-094833); face-lost: PASSED (20261119-093050); cube-seen: PASSED (20261119-093050); cube-moved: PASSED (20261119-093050); cube-tapped: PASSED (20261119-093050); object-pose-lost: PASSED (20261119-093050) | No |
| Teach, rename and erase a face | SEEN-NOT-PASSED | teach-face: PASSED (20261119-094833); rename-face: NOT EXERCISED; erase-face: NOT EXERCISED | No |
| Go to a place | NOT EXERCISED | go-to-pose: NOT EXERCISED | — |
| Return to and mount the charger | SEEN-NOT-PASSED | mount-charger: SEEN-NOT-PASSED (20261119-094833) | No |
| Roll a cube | SEEN-NOT-PASSED | roll: SEEN-NOT-PASSED (20261119-094833) | Yes — roll: 20261119-094833 M8252 |
| Stack a cube | SEEN-NOT-PASSED | stack: SEEN-NOT-PASSED (20261119-094833) | Yes — stack: 20261119-094833 M11602 |
| Charger seen report | PASSED | charger-seen: PASSED (20261119-094833) | No |
| Freeplay on/off and returning after a command | SEEN-NOT-PASSED | freeplay: SEEN-NOT-PASSED (20261119-182433) | No |
| The connection for an LLM | PASSED | connection: PASSED (20261119-093050) | No |
| Colour camera | PASSED | colour camera: PASSED (20261119-182433) | No |
| Reaction locks and wait until accepted | PASSED | reaction locks: PASSED (20261120-094700); wait until accepted: PASSED (20261120-094700) | No |

## Scope B

| Group and class | Best verdict | Session | MISSING on the selected path |
| --- | --- | --- | --- |
| Freeplay loop, activities, needs, mood / Freeplay loop | PASSED | 20261119-182433 | No |
| Physical reactions / ReactToCliff | PASSED | 20261120-094700 | No |
| Physical reactions / ReactToPickup | PASSED | 20261119-093050 | No |
| Physical reactions / ReactToRobotOnBack | PASSED | 20261119-094833 | No |
| Physical reactions / ReactToRobotOnFace | NOT EXERCISED | — | — |
| Physical reactions / ReactToRobotOnSide | NOT EXERCISED | — | — |
| Physical reactions / ReactToRobotShaken | NOT EXERCISED | — | — |
| Physical reactions / ReactToReturnedToTreads | PASSED | 20261119-093050 | No |
| Physical reactions / ReactToImpact | PASSED | 20261119-093050 | No |
| Physical reactions / ReactToPlacedOnSlope | NOT EXERCISED | — | — |
| Physical reactions / ReactToUnexpectedMovement | PASSED | 20261119-182433 | No |
| Physical reactions / ReactToMotorCalibration | PASSED | 20261119-093050 | No |
| Physical reactions / ReactToOnCharger | PASSED | 20261119-093050 | No |
| Physical reactions / ReactToFrustration | NOT EXERCISED | — | — |
| Needs / ExpressNeeds | NOT EXERCISED | — | — |
| Needs / PlayAnimOnNeedsChange | NOT EXERCISED | — | — |
| Needs / DriveInDesperation | NOT EXERCISED | — | — |
| Needs / EarnedSparks | NOT EXERCISED | — | — |
| Idle life / PlayAnim | PASSED | 20261119-093050 | No |
| Idle life / PlayAnimWithFace | NOT EXERCISED | — | — |
| Idle life / PlayArbitraryAnim | NOT EXERCISED | — | — |
| Idle life / Wait | NOT EXERCISED | — | — |
| Idle life / ExploreLookAroundInPlace | NOT EXERCISED | — | — |
| Idle life / FindFaces | PASSED | 20261119-094833 | No |
| Idle life / LookForFaceAndCube | PASSED | 20261119-182433 | No |
| Idle life / VisitInterestingEdge | NOT EXERCISED | — | — |
| Idle life / PounceOnMotion | PASSED | 20261119-182433 | No |
| Idle life / ThinkAboutBeacons | PASSED | 20261119-182433 | No |
| Faces / AcknowledgeFace | PASSED | 20261119-093050 | No |
| Faces / InteractWithFaces | PASSED | 20261119-182433 | No |
| Faces / SearchForFace | NOT EXERCISED | — | — |
| Faces / DriveToFace | NOT EXERCISED | — | — |
| Faces / EnrollFace | PASSED | 20261119-094833 | No |
| Faces / RespondToRenameFace | NOT EXERCISED | — | — |
| Faces / ReactToPet | PASSED | 20261119-093050 | No |
| Cubes / AcknowledgeObject | PASSED | 20261119-093050 | No |
| Cubes / ReactToCubeMoved | PASSED | 20261119-094833 | No |
| Cubes / PickUpCube | NOT EXERCISED | — | — |
| Cubes / PutDownBlock | NOT EXERCISED | — | — |
| Cubes / RollBlock | PASSED | 20261119-182433 | No |
| Cubes / StackBlocks | NOT EXERCISED | — | — |
| Cubes / DriveOffCharger | NOT EXERCISED | — | — |

For the attribution audit, including MISSING markers reported on other attempts, see [SESSION-CHECK-TRIAGE-1](../HANDOFF-SESSION-CHECK-TRIAGE-1.md).
