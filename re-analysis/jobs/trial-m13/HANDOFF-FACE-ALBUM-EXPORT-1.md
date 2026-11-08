# FACE-ALBUM-EXPORT-1

## Source-backed behavior

- GetSerializedAlbum requires an initialized recognizer (0x00866F06–0x00866F0A). It creates a temporary album with capacity 1000 by 4 (0x00866F7E–0x00866F8E), visits the first ten named faces in ascending face ID (0x00866F90–0x00866FC0, 0x00867046–0x00867068), and checks each owned album entry in ascending raw ID and slots 0–3 (0x00866FD6–0x00867044). It copies only registered features at the same ID and slot via the owned scratch feature (0x00866FE4, 0x00866FFA, 0x0086700C).
- No named faces yields successful empty output (0x0086706A–0x008670F4). Otherwise native size/serialize writes the temporary album (0x008671AE, 0x008671DC). Temporary ownership ends on either normal path. A native nonzero export result propagates as failure (0x0086708E–0x008670B0, 0x00867D1A–0x00867D20). A faulted guest is quarantined and its memory is released with its owner.
- Enrollment serialization runs only after a successful nonempty album (0x00867D5A–0x00867D66). It emits the version and next face ID, then all named entries in ID order (0x00867DD8–0x00867ED4). Its signed microsecond-to-seconds fields, current entry first and remaining entries sorted, use the existing checked writer (0x00860D7C–0x00860EE2). The album's ten-name cap does not truncate the enrollment vector.
- Save takes the vision snapshot and exits on export failure before accessing NV (0x00657168–0x00657198, 0x006575C6). Raw sizes at or above each tag capacity fail before padding (0x006571F0, 0x0065720E). Both-empty erases album 0x184000 first and enqueues enrollment erase 0x183000 only after the first request is accepted (0x00657350–0x0065735E, 0x00657474–0x00657478, 0x006575E6–0x00657600). For writes the same enqueue-result gate is at 0x006573AA–0x006573E6, with four-byte padding. The existing NV writer applies its lower capacity/header rules and emits its preceding erase (0x006444FC).
- The album and enrollment terminal callbacks are 0x0065A478 and 0x0065A6CC for both write and erase. Failure dispatches engine error 3 (0x0065A4C4, 0x0065A718). The report is tag 0x7C with one byte when the external interface exists (0x00517FBE–0x00517FD2, 0x007277B0–0x007277CE, 0x0071A0C2–0x0071A0E2). The engine error flag is set at 0x00518028.
- The stock getter validates the original detector and recognizer before and after export. NV save checks the original robot, recognizer, removal epoch and NV owner across requests and callbacks. Host saves entered from vision callbacks enqueue to the existing game FIFO without synchronously taking the engine lock, avoiding inversion against the vision lock.

## Files and verification

- OkaoFaceRecognition.cs, NativeOkaoFaceRecognizer.cs and FaceRecognizer.cs implement the native temporary-album export and keep the enrollment vector independent. VisionSystem.cs routes the stock getter and real NV owner, while preserving the injected serialization seam. CozmoEngine.cs supplies the lock-safe game FIFO deferral.
- FaceIdentityTests.cs exercises the stock native restore/export, mutable live album, ten-name selection, owner and deferred-save paths using drawn imagery. FaceTests.cs exercises ordered NV requests, failed replies and capacity gates with fake transport.
- Independent shipped-engine probe, rerun by the manager, distinguishes uninitialized failure, successful empty, and invalid-entry failure. With twelve names plus anonymous entries it preserves sparse slots and matches the independently selected 4512-byte album, SHA-256 073343e69d0ffcf1939a0d9c988ceb33b40ee0796e9c9fc5d3ac7b679e8be164. All twelve enrollment records match the independent 440-byte vector, SHA-256 8a50e28e2a6b4b93769478e8d13dd4e9746c1db186ff47a066b1718919b02edd.

## MISSING

- Stock FR remains lazy in the host. Before the stock guest initializes, export follows the native uninitialized failure. Earlier native vision initialization timing remains open.
- Smaller-capacity restore, full named enrollment/completion, native recognition cadence and broader face report fields remain open. A pair with more than ten names retains native export asymmetry and can fail the existing restore consistency check. No whole-record settlement or hardware claim.

Reader requested gpt-6.1-sol medium. Builder and reviewer used configured defaults. Actual backends unobserved.

Final affected suite passed 63/63, including the unchanged native OKAO oracle. The first fixture expected unchanged bytes after recognition had added a feature. The final fixture compares exact bytes before recognition and checks the live export afterward. The focused NV failure test checks local 0x7C/code 3 and second-request admission before the failed reply. Independent review found the save lock inversion and the first competing-owner test's timeout weakness. The corrected test holds the engine owner until the gate callback returns. Batch full gate passed at 412b6832 with 7834/7834 tests and zero skips. READY FOR CLAUDE.
