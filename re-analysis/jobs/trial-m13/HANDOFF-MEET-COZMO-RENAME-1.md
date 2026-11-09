# MEET-COZMO-RENAME-1

## Identity

- The host request mirrors G2E 0x8C UpdateEnrolledFaceByID, which carries a signed ID and old/new names with one-byte UTF-8 lengths (0x738284–0x7382B6, 0x6C22DC–0x6C2326). Vision unwraps the request at 0x6582C0–0x6582C8 and calls the recognizer under its mutex (0x657B40–0x657B62).
- The recognizer finds the signed enrolled key, compares the old name byte for byte, and changes only the name. Missing ID and old-name mismatch return failure. An exact match permits an empty, equal, or duplicate new name (0x867A24–0x867B70). The result uses the retained enrolled ID and assigned name (0x867BDE–0x867C4C). Later recognizer observations carry the name into FaceWorld.
- Vision unlocks, starts the ordinary face-album save and broadcasts the result immediately, without waiting for the NV reply (0x657B66–0x657B82). Failure emits no rename report or save. The typed E2G 0x1D report has the signed enrolled ID and new name (0x722BEA–0x722C0C, 0x79B9EA–0x79BA12). The 0x657B7C call resolves through PLT 0x4A5B0C to the external-interface gate at 0x511DE8. The UI rvalue broadcast enqueues for the app before synchronous local emission (0x6625B0–0x6625FA).

## Meet response

- RespondToRenameFace is a separate configured recipient of 0x8C (0x59CF66, 0x600694–0x600744). The inactive handler stages the requested ID and name even if Vision failed. A later inactive request overwrites both. An active response ignores the speech part of a second request while Vision continues to process identity (0x5BF1D8–0x5BF200, 0x60080C–0x60082C, vtable 0x102B460).
- A nonempty name passes the local runnable predicate (0x60082E–0x600844). The state gates at 0x5BF04C, 0x59EC12 and 0x600ABC return zero. Meet uses the shipped weighted chooser, with rename weight 3 and enrollment weight 2. Existing action and behavior transitions determine whether and when an enrollment yields.
- Init eagerly constructs SayTextAction with Name_Normal intent 3, applies the configured rename trigger and mask 0, and starts it with an empty callback (0x600884–0x6008F0). Init clears the staged string and ID even if admission declines (0x60095C–0x600976). Update completes after the retained action clears (0x600A50–0x600A5C). The voice uses the same existing provider, authored group and lift-safe animation path, with duration then pitch draws before admission (0x561100–0x5611DC, 0x56147E–0x561486, 0x5616D6–0x561754, 0x544714–0x544732).

## Files and tests

- `EnrollmentMessages.cs` admits the typed request on the captured owner and publishes the raw and typed report. `FaceRecognizer.cs` compares and updates the retained enrolled row. `VisionSystem.cs` scopes mutation, save and report to the retained recognizer and robot.
- `BehaviorContainer.cs`, `NativeBehaviorRuntime.cs` and `NativeRespondToRenameFace.cs` bind the configured candidate and its separate request subscription. The host consumes the admitted payload before callbacks can stop it, so cancellation does not replay the old name. A request staged after cancellation survives.
- `MeetRenameTests.cs` sends the public request and checks signed ID and UTF-8 report, failed identity with independent speech staging, reentrant log/stop, old-owner rejection, report before NV replies and restoration from the actual two write payloads. The real Meet chooser keeps enrollment and rename candidates, and the retained rename action reaches the authored clip and non-silent robot audio. A second request during that action updates identity without replaying speech.
- `FaceIdentityTests.cs` shares the stock retained album fixture. `M7BatchThreeBTests.cs` verifies the factory catalog includes the new supported class. The affected suite passed 167/167 and the final rebuilt rename cases passed 7/7. The independent manager run passed 53/53 across rename, Ground, pickup, pickup voice and fidelity checks.

- The full-gate fixture correction in `M5X13BuilderTests.cs` checks that the host vision rename subscription unlinks once before robot teardown while preserving the existing ordered checks. The focused gate correction run passed 11/11.

## MISSING

- Native registration order between the two independent 0x8C recipients remains unverified. The Vision save-attempt/report order and the later Meet chooser admission are established.
- A live stock-camera observation after rename, interruption during provider opening or playback, and preemption and resume of an already running enrollment were not exercised by this job. The checked chooser retains both candidates and the existing transitions. Physical camera and audio timing require operator evidence.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and reviewer used the configured default request. Runtime model identity was not observed. Review corrected the recognizer diagnostic's callback isolation. Manager review corrected eager-stop replay, accepted disposal and owner-scoped save. The builder's runnable-gate question was resolved by the three literal-zero native returns listed above. Manager disassembly resolved the report's external gate and local order.

## Records

- M7-018 owns configured construction and M7-019 owns the larger set of concrete behavior state machines. This job adds the retained rename responder and leaves other classes to their own work.
- M8-001 and M8-003 supply retained behavior and scoring. M15-031 supplies the Meet chooser and enrollment arbitration. Other Meet activities and enrollment stages retain their separate scope.
- M14-011 owns enrolled identity and persistence. This job adds the old-name-matched rename and typed report. Other enrolled-face operations and lower NV behavior retain their separate scope.
- M14-006 supplies the authored speech action and provider lifecycle. The rename call uses Name_Normal and mask 0. Shared audio and physical timing remain separate.
