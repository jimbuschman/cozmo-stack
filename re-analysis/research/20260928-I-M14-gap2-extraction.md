# I-M14 gap pass 2 — FaceRecognizer decision path

Role: `cozmo-extractor` (performed by the integration manager). Date: 2026-09-28. Primary source is `libcozmoEngine.so`, read with Capstone; Ghidra supplied navigation and tentative structure only.

| step | what the original does | citation | record | class |
| --- | --- | --- | --- | --- |
| G2-1 | `RecognizeFace` zeroes both outputs, asks OKAO for registered-user count, and fails on an OKAO error. If enrollment is active, the OKAO album is empty and no enrollment track is set, it registers the first user and returns score 1000 on success. | 0x008640E4 entry; 0x0086410C OKAO_FR_GetRegisteredUserNum; 0x008641AC..0x00864200 RegisterNewUser and output 1000 | NEW recognition record | EXACT_SOURCE for this branch |
| G2-2 | Otherwise it allocates two ten-element integer vectors and asks `OKAO_FR_Identify` for at most ten album-entry ids and scores, with profiler Tic/Toc around the call. More than ten returned results is treated as a loop-bound error. | 0x00864334..0x00864370 | NEW recognition record | EXACT_SOURCE |
| G2-3 | When new-user registration is enabled, there is no enrollment target conflict, and either there is no match or the top score is below 550 (0x226), it registers a new user and reports score 1000. | 0x00864A18..0x00864AA8 (comparison with 0x226 and RegisterNewUser path) | NEW recognition record | EXACT_SOURCE |
| G2-4 | A named lower-ranked match can replace a session-only top match and trigger a merge. The candidate named match must exceed 675 (0x2A3); the continuation threshold is `min(score-75, 600)`. The selected lower-ranked album entry and score become the outputs, then `MergeFaces(namedID, sessionID)` is invoked. | 0x00864756..0x008649F8; 0x00864BDA..0x00864CBA | NEW recognition record | EXACT_SOURCE for the selection/merge call |
| G2-5 | `MergeFaces(keepID, mergeID)` returns success immediately for identical ids; otherwise both enrolled entries must exist. It calls `EnrolledFaceEntry::MergeWith(..., 5, removedEntries)`, remaps retained album entries to keepID, clears removed OKAO users, removes the merged user, and propagates failures. | 0x008638AC..0x00863B9A | NEW recognition record | EXACT_SOURCE for the orchestration |
| G2-6 | `EnrolledFaceEntry::Serialize` converts to the CLAD `EnrolledFaceStorage`, grows the destination vector by the packed size, and packs at the old end. | 0x00861024..0x0086107A | NEW album record | EXACT_SOURCE |

Still open: the behavior-changing bodies of `RegisterNewUser`, `UpdateExistingAlbumEntry`, `EnrolledFaceEntry::MergeWith`, `ConvertToEnrolledFaceStorage`, deserialization/install, and the complete branch-by-branch proof for the middle of `RecognizeFace`. These own live recognition/enrollment behavior and are sent to gap pass 3.
