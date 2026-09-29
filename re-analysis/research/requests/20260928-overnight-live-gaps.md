# Research request: overnight live-path gaps (M14-011, M7-021, M15-014)

- **Date:** 2026-09-28
- **Requested by:** manager (Claude)
- **Kind:** independent extraction
- **Answer files** (one per part, written as each part finishes, so a partial night still leaves finished parts):
  - `re-analysis/research/20260929-M14-011-face-recognizer-extraction.md`
  - `re-analysis/research/20260929-M7-021-reaction-fields-extraction.md`
  - `re-analysis/research/20260929-M15-014-needs-connection-extraction.md`

## Why this is needed

These three records are live-path RECOVERABLE_GAPs with no extraction report yet. The build jobs B-M14, B-M7 and B-M15
will reach them, and each can only settle once the source is read. Work the parts in the order below. Follow
`re-analysis/research/README.md` and `.opencode/agent/cozmo-extractor.md` exactly: one row per behaviour-changing step,
an address citation or UNKNOWN, and no guesses.

Tools: `libcozmoEngine.so` is at `resources/lib/armeabi-v7a/libcozmoEngine.so`. The Ghidra decompile under
`re-analysis/decomp/libcozmoEngine/` (with `index.tsv`, addresses at base 0) is a navigation aid, not evidence: cite
the instruction addresses from the disassembly (`re-analysis/tools/emu/so.py`: `dis(start, end)` returns lines).

## Part 1: M14-011, FaceRecognizer matching, registration, update and merge

Record: `FaceRecognizer matching, registration, update and merge semantics`, RECOVERABLE_GAP, location
`cozmo-stack/src/Cozmo.Robot/Vision/Faces.cs`.

1. Every branch of `RecognizeFace` 0x008640E4..0x00864CD8 that the record's evidence doesn't already cover: every
   score threshold and constant, every album/user state it reads or writes, and every return path.
2. `RegisterNewUser` 0x00862808, `GetFaceIDforAlbumEntry` 0x00862B24, `UpdateExistingAlbumEntry` 0x00862C7C and
   `RemoveUser` 0x008634A4: what each reads, writes, allocates and returns, including id assignment and any limits.
3. `EnrolledFaceEntry::MergeWith` 0x008600B4: which fields merge and how (time stamps, scores, names, album entries).
4. `ConvertToEnrolledFaceStorage` 0x00860AE4, and the inverse install from `VisionSystem::SetSerializedFaceData`
   0x00858C74: the stored layout, field by field, and what the install validates or rejects.
5. The callers of each function above, with the addresses, so the production path is complete.

Calls into the OKAO library (the vendor face engine) are the boundary: name the call, its arguments and how its result
is used, but don't trace inside it.

## Part 2: M7-021, the reaction robot fields and cliff helpers

Record: `Reaction robot-field meanings and cliff helper bodies remain to be recovered`, RECOVERABLE_GAP, location
`cozmo-stack/src/Cozmo.Robot/Behavior/OffTreadsBehaviors.cs`.

1. The robot-object offsets +0x355, +0x338, +0x300 and +0x37C, which live reaction rows read (M7 gap1 G8,
   `re-analysis/research/20260928-I-M7-gap1-extraction.md`): every writer and every reader, with addresses, and what
   each field holds (type, units, and the message or sensor that sets it).
2. The helper bodies 0x0055B554 and 0x005C0CA8: full disassembly semantics, and all their callers.

## Part 3: M15-014, needs connection and per-serial persistence

Record: `Needs connection and per-serial persistence lifecycle`, RECOVERABLE_GAP, location
`cozmo-stack/src/Cozmo.Robot/Behavior/Needs.cs`.

1. What calls `RobotInterface::MessageHandler::ConnectRobotToNeedsManager` 0x0069DEE4. The call is generated or
   indirect: find the dispatch table or registration that reaches it, and which inbound message (tag and field) supplies
   the serial number.
2. The ordering at connection: how that call sits relative to `NeedsManager::InitAfterSerialNumberAcquired` 0x006943A0,
   `ReadFromDevice` 0x006998B4 and `WriteToDevice` 0x00693BB0, and what happens when the serial is missing or the read
   fails.

## Already known (don't redo)

- M14-011's current evidence: 0x0086410C..0x00864370 (registered users, first-user score 1000, the ten-result OKAO
  identify), 0x00864A18..0x00864AA8 (the below-550 new-user path), 0x00864756..0x008649F8 and 0x00864BDA..0x00864CBA
  (the >675 lower-ranked named candidate, the min(score-75,600) rule and the merge call), 0x008638AC..0x00863B9A
  (MergeFaces), 0x00861024..0x0086107A (the outer EnrolledFaceEntry pack). Earlier M14 reports:
  `20260927-X2-M14-faces-extraction.md`, `20260928-I-M14-gap{1,2,3}-extraction.md`.
- M15-014's evidence: `NeedsFilenameFromSerialNumber` 0x00695224 and the persistence string initializer 0x004D8DDC.
  Earlier M15 reports: `20260928-X4-M15-freeplay-extraction.md`, `20260928-I-M15-gap{1,2}-extraction.md`.
- The M7 behaviour reports: `20260927-X3-M7-behaviour-part{1,2}-extraction.md`,
  `20260928-X3-M7-behaviour-part{3..6}-*.md`, `20260928-I-M7-gap1-extraction.md`.

## Out of scope

- The inside of OKAO, and anything in the robot firmware.
- Any other record, and any code, inventory or manifest edit. No branches, commits or pushes.

## Answer format

The table from `.opencode/agent/cozmo-extractor.md` (`step | what the original does | citation | record |
classification`), then contradictions with the current record text, weak evidence, and open questions. Anything not
established from the disassembly is UNKNOWN.
