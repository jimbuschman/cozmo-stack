# AI-BODY-FACE-ID-REPORTS-1

## Claims and evidence

- FaceWorld::ChangeFaceID searches signed keys at 0x004F3DE0–0x004F3E02. A found entry changes ID/name at 0x004F4000–0x004F4006, attempts a unique destination insert at 0x004F4036, and removes the old key without deletion notification at 0x004F406E–0x004F4070. An existing destination wins, and a same-ID remap removes the original. The found and absent paths both construct and send at 0x004F3E5C–0x004F3E64. The external payload is tag 0x47 (0x00724D22) with signed old ID then signed new ID (0x00713A80–0x00713AAA). The supplied name updates the entry and is absent from the payload.
- FaceWorld::RemoveFace with the true flag constructs tag 0x4B from the iterator key and sends before viz and map erasure (0x004F3C1C–0x004F3C64). The union constructor is 0x00725066 and the signed four-byte face ID is packed at 0x0071427C–0x0071428C. An absent explicit ID and a false publish flag produce no report (0x004F3CD0–0x004F3D50 and 0x004F3C1C). Reentrant local delivery is fenced to the original owner and exact selected entry, with a guard against duplicate publication for the same active removal.
- Robot::Broadcast 0x00511DE8 gates app and local delivery on the original external interface. The UI queues app delivery at 0x00662526 before emitting the synchronous local payload at 0x00662552. Existing NativeChangedFace and NativeDeletedFace consumers keep their values. FaceWorld's public callbacks remain a separate host view.
- Queued FaceWorld::Update applies current observations at 0x004F531E, then reads the prior processed timestamp at 0x004F535E–0x004F5366 before the current result writes it at 0x006549AA. Unnamed expiry compares that prior timestamp unsigned, strictly greater than wrapping last-observed plus 15000 at 0x004F5380–0x004F53AC. It visits faces in signed-key order (0x004F536A, 0x004F54B2–0x004F54DA) and calls RemoveFace(true) at 0x004F54A8. Standalone explicit Update keeps its supplied-time adapter.
- ClearAllFaces visits signed keys, broadcasts each deletion before its erase, then zeroes the last-face timestamp at 0x004F3CA4–0x004F3CCA. The attached public method exposes that explicit native operation. Reset/teardown remain silent. The enrolled rename and erase entry points at 0x00657B24 and 0x00657A60 are separate from this pair of reports.

## Files and tests

- FaceIdReports.cs packs and queues the typed and raw app reports. Vision/Faces.cs, Vision/NativeFaceCache.cs and Vision/VisionSystem.cs bind the original-owner local signal, exact removal and queued expiry.
- FaceIdReportsTests reaches the attached recipients and checks signed raw/typed data, app deferral, local pre-erase, absent/collision/same-ID remaps, original-entry and owner replacement, external suppression, signed clear, and unlocked public callback delivery. The expiry test runs real F2 and the worker with a supplied empty detector and prior clock, using the production drain delegate. It checks strict cutoff, unsigned wrap and signed deletion order. FaceIdentityTests checks real shipped recognizer output raw -1 to 7 before the positive 0x46 report. Affected 222/222 passed, final compatibility subset 104/104, and independent check including unchanged NativeOkaoOracleTests 36/36.

## MISSING

- The additional session-only expiry telemetry at 0x004F53F0–0x004F54A0 uses an auxiliary count outside this job. No stock caller of explicit ClearAllFaces was established. The overlap RemoveFaceByID calls at 0x004F4468/0x004F44F6 remain bypassed on the checked stock recognition route. Wider face tracking, optional observed-face attributes and recognizer cadence remain open.

## Workers

- Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. The manager reopened both packers, the signed traversal, pre-erase broadcast and unsigned expiry. One review correction round deferred host face callbacks out of the face/frame gates and tightened owner reentry checks. Builder raised no unresolved source questions beyond the telemetry and generic lifecycle gaps.

## Records

- M14-008 owns the face result and lifecycle broadcasts. M14-001, M14-009 and M14-011 own adjacent tracking, origin and recognition behavior. No whole record settlement is proposed.
