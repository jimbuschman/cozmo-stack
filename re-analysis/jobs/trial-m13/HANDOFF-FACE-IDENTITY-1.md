# FACE-IDENTITY-1

## Production claims

- Stock vision owns one guest for DT, PT and FR. The recognition constructor starts with count/mode -1 and registration enabled (0x008621DC–0x008621EC). Native album capacity is 1000 users by 4 features (0x00862A20–0x00862A60). Guest handles are tied to their detector owner. Injected detector and recognizer seams retain their prior composition.
- Multi-face admission excludes tracked identities except the enrollment track, clears the set when all are excluded, and preserves the independent shuffle (0x0086D898–0x0086D912). Mode-zero enrollment checks confidence greater than 500, raw pose, minimum eye distance and facing angles (0x0086E074–0x0086E0CC, 0x0086CFE4–0x0086D00E).
- Admission requires a free slot, raw +0x30 zero and either an enrollable face or an existing enrolled record (0x00865A1C–0x00865A94). It retains the image and swaps the PT result (0x0086DBA6–0x0086DBB2). The worker only extracts (0x00862D70, 0x00865034–0x008650D0). The vision pass attempts admission first, consumes any successful previous extraction for its queued track and independently looks up the current detection (0x0086DBA6–0x0086DBC2, 0x00863CF4–0x00863EF4).
- FR identify uses the guest album and two arrays of ten entries (0x00864100, 0x00864222). The adapter also calls the sourced register, feature-copy, verify, count and clear recipients (0x00865CB8, 0x00865E06, 0x00865D94, 0x00865B98, 0x00866B26, 0x00865E16). Ordinary match/registration decisions reuse FaceRecognizer (0x008640E4). The first association stores the track and score (0x0086357A–0x00863598). Moving the same identity erases its old track key (0x00865B74–0x00865B88).
- A first identity change uses the negative ID of the track being looked up (0x0086DBC6–0x0086DBD8). Copying the result before normalizing previous IDs makes this one-shot (0x00863EAE–0x00863F00, 0x00860F0C). The detected face retains DT confidence (0x0086DD12–0x0086DD34). FaceWorld applies the change and its name before observations (0x006551E8–0x00655204, 0x004F4000–0x004F4014). The local 47 projection carries the old and new IDs (0x004F3E54–0x004F3E64).
- The connection NV path restores the paired album and enrollment into the same recognizer (0x0065A876–0x0065A896, 0x00867F54–0x00868366, native restore at 0x0086739A). Equal-capacity albums transfer ownership after checks (0x00868948, 0x0086808C). A successful load reports the installed enrollment list in ID order, with erase first, and does not insert observations (0x00868104–0x0086835C, 0x0065A9D0–0x0065A9D4, 0x0065159C–0x00651612). Owner and removal checks reject a stale load or result.
- Pose reset clears track associations and invalidates queued tracking while retaining the album (0x0086CC34, 0x00864FD8). Retirement joins outstanding extraction before guest release (0x00862852–0x00862866); faulted guests release their machine under M1-034.

## Files and verification

- OkaoFaceDetection.cs and OkaoFaceRecognition.cs preserve the raw DT oracle path and implement the shared guest and native FR calls. NativeOkaoFaceRecognizer.cs is the owner-bound ABI adapter.
- Faces.cs, FaceRecognizer.cs and VisionSystem.cs join async admission, association, reset, retirement, NV restore and API delivery to the stock camera pipeline.
- FaceIdentityTests.cs uses the existing drawn fixture. It restores a shipped-library album through the fake robot's connection NV path and reaches the later-frame named observation and identity change. FaceRecognizerTests.cs also verifies that a completed queued track does not change a different current track. FaceTests.cs, M3DeviceTests.cs and ReportOrderingRegressionTests.cs select the unavailable seam in legacy raw-NV and report-order tests.
- Independent guest probe reproduced a 332-byte synthetic album with SHA-256 af7ea79ea727b395b560b0eb36657d510bbb1b09ff2ac9734f1abd8ced88aac3 for both memory fills. A separately extracted query returned entry 3 and score 1000. The probe and generated album stayed in ignored scratch, except for the synthetic album bytes in the regression fixture.

Reader requested gpt-6.1-sol medium. Builder and reviewer used the configured default. Actual backends unobserved. Independent review rechecked stale owner adoption, loaded-name delivery, the stock export guard and faulted teardown.

## MISSING

- Native extract cadence and concurrent DT/PT execution remain open. The shared machine serializes guest calls. The independent extraction measured about 280–315 ms per feature.
- Positive-ID remap/merge publication, explicit enrollment completion and nondefault enrollment pose/optional EX/SM/GB modes remain open.
- Smaller-capacity album copy and named-only export remain open (0x00866EF0–0x0086723A, 0x00867D10). Stock export signals MISSING and stock save does not issue NV writes. The imported bytes are not presented as a current snapshot.
- Full app observation and loaded-face payloads remain open. Loaded-face ages are read natively at 0x008681DE, 0x008681F4 and 0x0086822E. The existing C# name/ID adapter carries the checked fields.
- M14-008, M14-010, M14-011, M14-012 and M11-055 remain open. No manifest status or approval changes.

The final affected suite passed 91/91, including the unchanged native OKAO oracle, connection NV, raw-transport report ordering, stock camera and face recognizer tests. The stock regression checks name visibility during the ID change, change before the named observation, no repeated ID change, empty restore rejection and no stale NV write. The manager rechecked 7/7 after adding an engine-owned result drain to the owner-removal regression. Removing and replacing the robot in the identity callback prevents the old frame from emitting its named observation. Batch full gate passed at bd4c818e with 7821/7821 tests and zero skips. READY FOR CLAUDE.
