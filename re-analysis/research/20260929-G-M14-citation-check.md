# G-M14 — checked M14-011 rows (citation check and corrections)

Role: manager (`opencode`, window 3). Date: 2026-09-29. This is the checked version of
`re-analysis/research/20260929-M14-011-face-recognizer-extraction.md` (Codex), whose 24 extraction rows and
8 caller rows were opened in `resources/lib/armeabi-v7a/libcozmoEngine.so` by `cozmo-verifier` and then
re-corrected by `cozmo-extractor`.

## What was checked

- Every one of the 24 extraction rows and every caller row was disassembled in the `.so` (Thumb) and
  compared with the row text. The Ghidra decompilation was not used as evidence.
- The report's **Address correction** is confirmed: the old record addresses (`0x00862808` for
  `RegisterNewUser`, etc.) are wrong. `0x00862808` is inside `~FaceRecognizer()`. Every new range the
  report names is inside its symbol-backed body (`RegisterNewUser` 0x00865658, `GetFaceIDforAlbumEntry`
  0x00866C1C, `UpdateExistingAlbumEntry` 0x00865A98, the two `RemoveUser` overloads 0x00865544 /
  0x00866AE0, `MergeWith` 0x008616AE, `ConvertToEnrolledFaceStorage` 0x00860D7C,
  `VisionSystem::SetSerializedFaceData` 0x006B9D82).
- The manager's 2026-09-29 spot-check (function addresses, 751/549/676, -75, the 600 cap, score 1000,
  the four-entry limit, the merge limit 5) was confirmed independently.

## First pass: 6 rows failed, and what happened to them

| row | defect | fix |
| --- | --- | --- |
| M14-011-2 | claimed an `OKAO_FR_Identify` error returns 1; it returns 0 (`0x00864298 cmp r7,#0`; `0x0086429A beq 0x00864338` is the success branch; error ends `0x008642E6 movs r7,#0`, returned by `0x00864330 mov r0,r7`) | row rewritten |
| M14-011-3 | cited range `0x00864394..0x008644EC` starts at the low-confidence path and excludes the confident gate | range corrected to `0x00864338..0x008644EC` (`0x00864338 cmp r3,#1`, `0x0086433E` loads `0x2EF`, `0x00864344 cmp r0,r1`, `0x0086434E blx GetFaceIDforAlbumEntry`) |
| M14-011-23 | claimed equal capacities copy entry-by-entry; `0x008689F4 cmp r3,r1 / it eq / cmpeq lr,ip / bne 0x868AE8` means equal capacities **swap**; only strictly smaller capacities copy | row rewritten |
| callers `RegisterNewUser` | empty-album call cited at `0x008641E8`; the call is `0x0086419E` | corrected |
| callers `GetFaceIDforAlbumEntry` | `0x0086480C` is `cmp r0,r2`; the call is `0x0086481C`; the top-match call `0x0086434E` was missing | corrected |
| callers `UpdateExistingAlbumEntry` | `0x00864932..0x00864938` is argument setup; the call is `0x0086493C` | corrected |

The corrected rows were re-verified in the `.so`: **6/6 PASS**. All 24 extraction rows and all caller rows
now pass. `EnrolledFaceEntry +0x04` is left unnamed: its merge rule (row 18) is exact, its source-level
meaning is UNKNOWN.

## The checked rows

These are the rows approved into `re-analysis/inventory/M14-faces.md` as Correction C3. They are the
report's rows with the six corrections above applied.

| step | what the original does | citation | record | classification |
| --- | --- | --- | --- | --- |
| M14-011-1 | `RecognizeFace` initializes `faceID=0` and `score=0`. Failure of `OKAO_FR_GetRegisteredUserNum(album,&count)` returns 1. The already-recorded empty-album first-user branch calls `RegisterNewUser`, reports score 1000 on success, and otherwise propagates failure. | 0x008640E4..0x00864252 | M14-011 | EXACT_SOURCE |
| M14-011-2 | The already-recorded `OKAO_FR_Identify(feature,album,10,ids,scores,&count)` call is bounded to ten results (`0x00864220 movs r2,#0xa`). An OKAO error (`0x00864298 cmp r7,#0`; `0x0086429A beq 0x00864338` is the success branch) is logged and the function returns 0 (the error path ends `0x008642E6 movs r7,#0`, returned by `0x00864330 mov r0,r7`). A returned count above ten (`0x00864236 cmp r3,#0xb`) raises the loop-bound diagnostic and clamps the count to ten (`0x00864294 movs r3,#0xa`) before later loops. | 0x00864254..0x00864392 | M14-011 | EXACT_SOURCE |
| M14-011-3 | A normal confident match requires at least one result (`0x00864338 cmp r3,#1`) and a top score strictly greater than 750 (`0x0086433E` loads `0x2EF`; `0x00864344 cmp r0,r1`). It maps the top album entry to a face ID (`0x0086434E blx GetFaceIDforAlbumEntry`). A zero/missing ID (`0x00864356 beq 0x008644AC`) or absent enrolled-entry record is an error and returns 1 (`0x008644EC movs r7,#1`). | 0x00864338..0x008644EC | M14-011 | EXACT_SOURCE |
| M14-011-4 | For a valid top match, the function installs debug-match data on that enrolled entry: the top match plus unique later face IDs, no more than ten input results (`0x00864676 cmp r6,#0xA`) and no more than two stored debug matches (`0x008645AC cmp r0,#2`). Missing enrollment data for a later debug candidate contributes an empty name but does not fail recognition. | 0x008644F4..0x008646FC | M14-011 | EXACT_SOURCE |
| M14-011-5 | If the top face is session-only (empty name), there is more than one result, and it is not the active enrollment target, later candidates are examined. The first named candidate over 675 (`0x008647D6 cmp r0,#0x2A4`) starts the already-recorded lower-ranked-selection path; candidates after it are considered only while their score remains above `min(candidateScore-75,600)` (`0x008647E4 subs r0,#0x4B`; `0x008647E8 cmp r0,#0x258`; `0x008647EE movge r0,#0x258`). A missing later enrolled-entry record logs and stops/continues according to the branch, rather than synthesizing an entry. | 0x008646FC..0x008649F8 | M14-011 | EXACT_SOURCE |
| M14-011-6 | On the ordinary confident-match path, the selected face ID and score are output and `UpdateExistingAlbumEntry(selectedAlbumEntry, currentFeature)` is called (`0x0086493C`). Update failure is logged but recognition still returns success. If the selected face is the enrollment target and its saved enrollment track differs from the current track, the saved track is replaced with the current track (`0x00864992 ldr r6,[sl,#0xAC]`, `0x00864998 ldr r5,[sl,#0x104]`, `0x008649EE str r0,[sl,#0x104]`). | 0x00864926..0x008649F6; 0x008649EA..0x008649EE | M14-011 | EXACT_SOURCE |
| M14-011-7 | Below the confident gate, recognition returns success without a face unless enrollment is enabled (`+0x108 != 0`, `0x00864394 cmp r5,#0`) and per-frame registration is allowed (`+0xFC != 0`, `0x0086439A ldrb [sl,#0xFC]`). If no named enrollment target is active for the current track, and no enrollment target ID is set, a new user is added only when there are no results or the top score is below 550 (`0x008643F0 movw r2,#0x225`); success outputs score 1000 (`0x0086444E mov.w r0,#0x3E8`). | 0x00864394..0x00864454; 0x00864A18..0x00864AA8 | M14-011 | EXACT_SOURCE |
| M14-011-8 | When a named enrollment target (`+0x100`) is active and current track `+0xAC` equals enrollment track `+0x104`, output score starts at zero. The result list is searched for that face ID; if present its score is output and its album entry is updated (`0x00864506 UpdateExistingAlbumEntry`). Whether found or not, output face ID is forced to the enrollment target. Update failure returns 1 (`0x0086450E bne 0x8642E8`); otherwise this path returns success. | 0x008643A2..0x00864562 | M14-011 | EXACT_SOURCE |
| M14-011-9 | `RegisterNewUser` obtains an album entry from `GetNextAlbumEntryToUse` and a face ID from `GetNextFaceID`. A negative album entry returns 1. Otherwise it calls `OKAO_FR_RegisterData(album,feature,albumEntry,0)`; failure returns 1. Success creates an `EnrolledFaceEntry(faceID,now)`, sets its current track to recognizer `+0xAC`, adds the album entry with `isSessionOnly=true`, maps album-entry to face-ID, inserts the face record, decrements the remaining enrollment count `+0x108` if positive, and returns 0. | 0x00865658..0x008657A8 | M14-011 | EXACT_SOURCE |
| M14-011-10 | Album-entry allocation is limited to 1000 registered users/indices. It probes for a free index, wrapping after 999. If the album is full it selects the oldest-updated session-only face, removes it, reuses its current album entry, and verifies it was cleared. With no replaceable session-only face, or on the enumerated OKAO errors, it returns -1. | 0x00865178..0x008653D8 | M14-011 | EXACT_SOURCE |
| M14-011-11 | `GetFaceIDforAlbumEntry` performs a lookup in the album-entry-to-face-ID map at recognizer `+0xE8`; a hit returns the mapped face ID. A miss logs/debug-breaks and returns 0. | 0x00866C1C..0x00866CAC | M14-011 | EXACT_SOURCE |
| M14-011-12 | `UpdateExistingAlbumEntry` maps album entry to face ID, then face ID to its enrolled entry, updates that album entry's last-seen timestamp to `system_clock::now`, and changes current/previous track IDs when recognizer `+0xAC` differs. It asks `OKAO_FR_GetRegisteredUsrDataNum`; failure returns 1. | 0x00865A98..0x00865C04 | M14-011 | EXACT_SOURCE |
| M14-011-13 | Adding enrollment data is enabled only when the remaining enrollment count is nonzero and the configured target is zero or this face ID. It additionally requires at least one second since this album entry's previous update (comparison against 999,999 microseconds) and either fewer than four data entries, an unnamed face, or an explicitly targeted enrollment. Otherwise it makes no album mutation and returns 0. | 0x00865C04..0x00865D7C (constant `0x000F423F`) | M14-011 | EXACT_SOURCE |
| M14-011-14 | With fewer than four OKAO data entries it registers the current feature at index `count`. With four entries, replacement is permitted only for an unnamed face or explicit target: it verifies the new feature against the full user, temporarily removes each of the four existing features in turn, verifies that held-out feature against the remaining three, restores it, and replaces the held-out index having the lowest score only if that score is lower than the new feature's full-user score. Every OKAO get/clear/verify/re-register/register failure returns 1. | 0x00865CA6..0x00865EB6 | M14-011 | EXACT_SOURCE |
| M14-011-15 | After a successful add/replacement (and also the full/no-replacement continuation), it updates the entry's album-entry timestamp/session flag. The session flag passed is `configuredEnrollmentTarget != faceID`. If enrollment count is positive it is decremented. Success returns 0. | 0x00865EB6..0x00865EEA | M14-011 | EXACT_SOURCE |
| M14-011-16 | `RemoveUser(faceID)` looks up the enrolled-face map. A hit delegates to iterator removal and returns 0. A missing face logs "UserDoesNotExist" but also returns 0. Iterator removal erases the track mapping, then for every owned album entry erases the album-entry-to-face-ID mapping and calls `OKAO_FR_ClearUser(album,entry)`; clear errors are logged but do not stop removal. Finally it erases the enrolled-face node. | 0x00865544..0x008655C6; 0x00866AE0..0x00866B9A | M14-011 | EXACT_SOURCE |
| M14-011-17 | `MergeWith(other,limit,removed)` first merges album entries. Each entry is keyed with its last-seen time. There is at most one session-only/current entry: the newer of the two is kept. If the combined non-session entries fit within `limit-1`, all are retained; otherwise non-session entries are ordered by recency and only the newest `limit-1` are retained. Removed entries belonging to `this` are appended to `removed`; entries discarded only from `other` need no clearing from `this`. With M14's caller-supplied limit 5, the result is one session-only entry plus at most four other album entries. | 0x0086173C..0x00861C10; caller passes 5 at 0x008639B8..0x008639C2 | M14-011 | EXACT_SOURCE |
| M14-011-18 | The scalar merge then copies `other.+4` only when `other.+4 == other.faceID`; copies the other name only if this name is empty; stores the earlier of the two `+0x30` timestamps, the later of the two `+0x28` timestamps, and the maximum `+0x1C` score. It returns 0. The semantic name of field `+4` is not established by these instructions. | 0x008616AE..0x0086173A | M14-011 | EXACT_SOURCE (operation); UNKNOWN (name of `+4`) |
| M14-011-19 | `ConvertToEnrolledFaceStorage` creates the stored object as: bytes +0..+7 = entry `+0x28` timestamp divided by 1,000,000; +8..+15 = entry `+0x30` timestamp divided by 1,000,000; +0x10 = vector of per-album-entry timestamps in seconds; +0x1C = face ID; +0x20 = parallel vector of album-entry IDs; +0x2C = name. The current/session album entry is emitted first (timestamp zero if -1), followed by every other entry from the map. | 0x00860D7C..0x00860EE0 (`0x000F4240` divisor) | M14-011 | EXACT_SOURCE |
| M14-011-20 | The inverse `EnrolledFaceEntry(EnrolledFaceStorage)` restores face ID/name, initializes score to 1000, multiplies the two stored seconds values and every parallel last-seen value by 1,000,000, makes the first album ID the current/session entry, and inserts each non--1 album ID into the last-seen map. A vector-length mismatch is warned and no entries are installed; restored future times are clipped to now. | 0x00860728..0x0086091A | M14-011 | EXACT_SOURCE |
| M14-011-21 | Enrollment-data install rejects fewer than eight bytes, rejects a version prefix other than `0x0002FACE`, reads nextFaceID from bytes 4..7, and repeatedly deserializes packed `EnrolledFaceStorage` records. Deserialization rejects empty/end indices, insufficient bytes, or an unpack count shorter than `Size()`. A malformed record aborts the entire enrollment-data load with return 1. | 0x008611B4..0x008613D8; 0x008684D4..0x0086871C | M14-011 | EXACT_SOURCE |
| M14-011-22 | Album install rejects a null OKAO common handle and empty album bytes, calls `OKAO_FR_RestoreAlbum(common,data,size,&result)`, and then requires successful OKAO registered-user/data counts. The loaded album and enrollment map must agree: registered-user count equals album-entry LUT size, every LUT entry is registered, every enrollment-owned album entry exists in the LUT, and it maps back to the owning face ID. | 0x00867384..0x008674E2; 0x00863064..0x0086329C | M14-011 | EXACT_SOURCE |
| M14-011-23 | Install also compares loaded and current OKAO capacities. A loaded album larger than current capacity is rejected. Equal capacities take the swap path (`0x008689F4 cmp r3,r1; it eq; cmpeq lr,ip; bne 0x868AE8` — equality falls through to the "Stop using %p, start using %p" swap at `0x008689FC`). Only strictly smaller capacities are copied entry-by-entry after clearing the current album (`0x00868B2A OKAO_FR_ClearAlbum`), using `OKAO_FR_IsRegistered` (`0x00868BC0`), `GetFeatureFromAlbum` (`0x00868BD4`), and `RegisterData` (`0x00868BE4`); any failure rejects the load. Only after all checks/copying succeed are the enrolled-face map and album-entry LUT installed and the track LUT cleared. | 0x00868874..0x00868C5E | M14-011 | EXACT_SOURCE |
| M14-011-24 | The public install path is `VisionSystem::SetSerializedFaceData` -> `FaceTracker::SetSerializedData` -> `FaceTracker::Impl::SetSerializedData` -> `FaceRecognizer::SetSerializedData`; that routine performs album restore, enrollment parsing, consistency/capacity install, sets nextFaceID, reports loaded faces, and deletes any temporary album handle. | 0x006B9D82..0x006B9D8C; 0x0086B2A2..0x0086B2AA; 0x0086E388..0x0086E392; 0x00867F54..0x00868366 (calls at 0x00867F74, 0x00867FFE, 0x008680AA) | M14-011 | EXACT_SOURCE |

## The checked caller rows

| callee | production callers in this binary | citation |
| --- | --- | --- |
| `RegisterNewUser` | `RecognizeFace`, for empty-album first user and low-confidence new user | 0x0086419E and 0x00864442 |
| `GetFaceIDforAlbumEntry` | `RecognizeFace`, for top match, target search, and debug/lower-ranked candidates | 0x0086434E, 0x008643C6, 0x008645BC, 0x0086472C, 0x0086481C |
| `UpdateExistingAlbumEntry` | `RecognizeFace`, for active named enrollment and the selected confident match | 0x00864506; 0x0086493C |
| `RemoveUser(faceID)` | `MergeFaces`; `GetNextAlbumEntryToUse` when recycling oldest session-only face; `EraseFace` | 0x00863B38; 0x008652D0; 0x0086782A |
| `RemoveUser(iterator)` | `RemoveUser(faceID)` and `EraseAllFaces` | 0x00865576..0x00865582; 0x008678EE |
| `MergeWith` | `MergeFaces(keepID,mergeID)`, with limit 5 and removed-entry vector | 0x008639B8..0x008639C2 |
| `ConvertToEnrolledFaceStorage` | `EnrolledFaceEntry::Serialize`, before CLAD size/pack | 0x00861024..0x0086107A |
| `SetSerializedFaceData` chain | The public `VisionSystem` forwarding chain listed in M14-011-24; `FaceRecognizer::SetSerializedData` is the behavior-changing endpoint | same citations as M14-011-24 |

## Open question kept

- UNKNOWN: source-level name and external meaning of `EnrolledFaceEntry +0x04`. Its merge rule is exact
  (M14-011-18); it must not be given a guessed semantic name.