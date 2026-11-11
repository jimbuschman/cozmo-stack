# M14-012 face album NV persistence: Claude check and settlement (manager, 2026-11-11)

Done by Claude while Codex was out of usage:
- **Census:** an extractor census (`.scratch/m14-012-census.md`), every row re-read in the binary.
- **Build:** FACE-ALBUM-SETTLEMENT-2.
- **Verify:** an independent verifier passed the commit but objected to settlement on one case. The manager fixed that case.
- **Test:** the affected face classes re-run, 164/164.

## M14-012 → EXACT_SOURCE

**What was already built and matches** (census rows R2–R29):
- the connection-time NV load (LoadFaceAlbumFromRobot 0x006512A0, the read callback 0x0065A860);
- the replay (one 0x58, then one 0x1C per list entry, 0x00651578);
- the save core with its size checks, erase branch, padded write and NV wrappers (0x00657144, 0x0065A478/0x0065A6CC);
- its callers (EraseFace 0x89, RenameFace 0x8C, the enrollment save);
- FaceEnrollmentCompleted from StopInternal 0x005FE2F4.

**What this job built:**

| Gap | What was built | Native |
| --- | --- | --- |
| G1 | G2E 0x8A EraseAllEnrolledFaces, which the stack had no handler for. Recognizer erase, the ordinary save (the both-empty erase branch), then one 0x58, without waiting for NV; gated on the external interface | Subscribed 0x0065019E/0x006505BC, handler 0x00658076 → 0x006514FC |
| G1, failed face library | The engine has no availability check: the erase fails, the save cannot export (no NV traffic) and the 0x58 is still sent. Added by the manager after the verifier's objection | — |
| G2 | The public host entries (LoadFaceAlbumFromRobot, AdoptFaceAlbum, BroadcastLoadedNamesAndIDs, the injected-recognizer adoption) used to replay FaceWorld names, sometimes after a failed install. They now follow the native read, install and replay, and replay nothing after a failed install | — |
| G2b | The oversize save now uses the source keys AlbumDataTooLarge / EnrollDataTooLarge and sets the error flag, with no NV traffic | 0x00657256..0x006572FE |

**Inventory correction** (M14-faces re-approved under the standing authorisation):
- the callback entry is 0x0065A860, not 0x0065A870;
- evidence item 2 was contradicted: the engine sends one 0x58 before the loop, not one per entry;
- evidence item 3 was incomplete;
- the callback, wrapper and 0x8A rows were added.

## Boundaries recorded in the record

1. **Stack inputs:** supplied non-stock recognizers (the public VisionOkaoRecognizer option), and a guest library that is unavailable for any reason other than a failed init. In that case 0x8A reports a visible MISSING.
2. **Stack-only public entries, kept public pending the operator:** LoadFaceAlbumFromRobot(), AdoptFaceAlbum and BroadcastLoadedNamesAndIDs().
   - They now behave natively, but the engine calls them only from Init and the file paths.
   - So an API caller can trigger a replay at a moment the engine wouldn't. This affects API delivery only.
3. **The shared read/save vectors in the original:** NVStorageComponent::Write copies its data (0x00644726..0x00644734). A save during the pending connection reads could still change what the read callback installs.
   - The stack isolates the buffers. This is a timing race with no effect in the normal order.
4. **G2E 0x8E SaveFaceAlbumToFile:** out of this record. It needs its own record or an operator exclusion.
5. **Oversize reachability:** depends on the stock guest's user capacity, which is unread.

**Records settled in this check: 1. Total: 39.**
