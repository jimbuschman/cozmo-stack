# FACE-IDENTITY-ERASE-1

## Source path

- G2E 0x89 EraseEnrolledFaceByID carries one signed 32-bit ID (0x74D22E–0x74D236, 0x737F6C–0x737F88). Vision's handler at 0x658070 forwards it to 0x657A60. That recipient locks vision for the stock recognizer operation, unlocks, and returns failure without saving or reporting if the recognizer fails (0x657A68–0x657A90).
- The recognizer finds the exact signed enrolled key. A missing key logs FaceRecognizer.EraseFace.NotFound and returns 1 (0x8677EC–0x867866). A hit removes only the current track mapping, then visits album entries in signed ascending key order, removes each reverse album mapping and calls OKAO clear. Clear failures log and continue, and the enrolled entry is removed at the end (0x865544–0x865584, 0x866AEA–0x866B90).
- Success initiates the existing album save before constructing and broadcasting the erased-face report (0x657A92–0x657ABE). The existing save path sends album tag 0x184000 before enrollment tag 0x183000, with the existing empty-data handling (0x657144–0x6573EA). The report does not wait for either NV reply. Erase changes the recognizer without directly deleting FaceWorld observations or changing enrollment mode.
- E2G 0x57 is a signed ID and a one-byte zero name length (0x72584E–0x725850, 0x715860–0x715888). Robot::Broadcast uses the external-interface gate and virtual slot +0x18 at 0x511DE8. The UI relocation at 0x102FE34 selects the const E2G implementation at 0x662508. It enqueues app delivery at 0x662526 before synchronous local emit at 0x662552. This corrects the sibling report handoffs' reference to the neighboring rvalue overload. Both overloads have the same ordering.
- The separate erase-all request is 0x8A and takes 0x658076 to the erase-all recipient (0x6514FC–0x651538). The single erase continues to serve the existing retained enrollment failure path through its own 0x57 publication.

## Files and tests

- `EnrollmentMessages.cs` sends the public 0x89 request through the game FIFO with its captured original owner. `VisionSystem.cs` registers and retires the host subscription, reuses the stock erase recipient, and starts the captured-owner save before adjacent raw and typed app delivery and the existing local notification.
- `FaceRecognizer.cs` keeps the current-track-only erase, visits signed album IDs in order, and defers diagnostic callbacks until the vision gate releases. `FaceRecognizerTests.cs` injects a clear failure and checks that the signed order and removal continue even with a throwing diagnostic listener.
- `FaceEraseTests.cs` exercises the public FIFO, missing and unnamed identities, stale owner, absent external interface and failed NV completion. It checks the exact report bytes and local/app order, no erase-all or enrollment-completion report, unchanged FaceWorld, and restoration of the survivor from actual captured album and enrollment writes.
- `M5X13BuilderTests.cs` separately verifies the host 0x89 subscription retirement while retaining the existing shutdown order assertions. The affected run passed 266/266, and the final rebuilt focused run passed 7/7. The independent manager run passed 53/53 across the three capability jobs, shutdown and fidelity.

## MISSING

- Erase-all ingress, loaded-name replay and other album persistence paths remain separate. Physical camera and robot NV timing require operator evidence.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and reviewer used the configured default request. Runtime model identity was not observed. One fixture correction accounted for the new host 0x89 unlink without changing the established teardown order. The reader resolved signed album-entry order and the exact const UI broadcast slot. The manager independently verified that ELF relocation and the app/local instructions.

## Records

- M14-011 contains the recognizer erase and album-entry mutation. This job builds the bounded single erase, including lower track/reverse-map/OKAO order. Matching, registration, merging and wider detector and track ownership remain open in that record.
- M14-012 contains album persistence, replay and enrollment completion. This job connects the existing save to the single erase report with exact app/local order. Other replay, file and restore branches remain open.
- M2-022 supplies the external UI dispatch and FIFO/local boundary. The report uses the existing engine delivery mechanism, including suppression when the external interface is absent.
