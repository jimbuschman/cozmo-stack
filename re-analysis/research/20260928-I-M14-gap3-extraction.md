# I-M14 gap pass 3 — recognition/enrollment residual

Role: `cozmo-extractor` (performed by the integration manager). Date: 2026-09-28.

The third bounded pass reopened `FaceRecognizer::RecognizeFace` 0x008640E4..0x00864CD8 and `MergeFaces` 0x008638AC..0x00863B9A in the binary, and followed their direct calls. Gap pass 2's six rows remain supported. The complete recognition/enrollment record cannot be settled without inventing omitted behavior.

| step | what remains | citation / exact next read | record | class |
| --- | --- | --- | --- | --- |
| G3-1 | The complete match/update/register state machine is not instruction-transcribed. The settled entry, 10-result identify, 550 new-user gate, lower-ranked named selection, 675/score-75/600 rule and merge call are only parts of it. | Finish every branch of 0x008640E4..0x00864CD8, especially 0x00864370..0x00864756 and 0x008649F8..0x00864BDA, with the meanings/writers of FaceRecognizer +0xAC, +0xFC, +0x100, +0x104 and +0x108. | NEW recognition record | RECOVERABLE_GAP |
| G3-2 | Registration and updates can change the OKAO album and the Anki enrollment maps; their bodies are not yet transcribed. | Read `FaceRecognizer::RegisterNewUser` 0x00862808, `UpdateExistingAlbumEntry` 0x00862C7C, `GetFaceIDforAlbumEntry` 0x00862B24 and `RemoveUser` 0x008634A4 instruction-by-instruction, including all OKAO calls and error paths. | NEW recognition record | RECOVERABLE_GAP |
| G3-3 | Merge orchestration is known, but the merge policy inside each enrolled entry is not. | Read `EnrolledFaceEntry::MergeWith` 0x008600B4, including its limit argument 5, ordering, evictions and returned removed-entry vector. | NEW recognition record | RECOVERABLE_GAP |
| G3-4 | Serialization's outer pack is known, but the exact field conversion and inverse install are not. | Read `EnrolledFaceEntry::ConvertToEnrolledFaceStorage` 0x00860AE4 and the corresponding deserialize/install callers reached from `VisionSystem::SetSerializedFaceData` 0x00858C74. | NEW recognition/album record | RECOVERABLE_GAP |

After three passes, these are still recoverable from the shipped `.so`; they are not HARDWARE_ONLY or BLOCKED_EXTERNAL. The inventory must keep the recognition/enrollment semantics as a live-path `RECOVERABLE_GAP` and set `source_investigation_exhausted=false`.
