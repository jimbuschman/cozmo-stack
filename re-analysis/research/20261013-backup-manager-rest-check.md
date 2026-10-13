# Opus check of R-BACKUP-REST (2026-10-13)

- **Checked:** `research/20261013-backup-manager-rest.md` (Codex, run on the work PC as gpt-6.1-sol, medium), against
  libcozmoEngine.so and libc++_shared.so. Read-only cozmo-extractor; helpers are in `.scratch/bk/`.
- **Adopted:** by the manager on 2026-10-13. Use the Codex report together with the corrections and additions below.
  Every body the report cites says what it claims.
- **Result:**
  - one handler was omitted (U1/U2);
  - one ordering consequence was missed (R9);
  - one boundary was wrong (stoull ships in libc++_shared.so);
  - two of its UNKNOWNs are now read (the member-name order, and the unordered_map order).

## The handlers (game to engine; sent only by the app, see A1)

- **0x6B RestoreRobotFromBackup** (R1–R9):
  - **R1:** if +0x58 ≠ 0: Warning "RobotDataBackupManager.RestoreRobotFromBackup" "Have pending requests so not
    restoring", broadcast RestoreRobotStatus(0,0), and return;
  - **R2:** CreateDirectory(+0x48). On failure: Info "RestoreFromBackup.NoDir" "Failed to create backup dir", and
    (0,0);
  - **R3:** if key ≠ 0 and `<key>.backup` exists, use it;
  - **R4:** otherwise GetFileToUseForBackup (the selector, 0x0051BB28..0x0051BD98). On failure: Error
    ".RestoreFromBackup.NoBackup" "No backup to restore from", errG, and (0,0);
  - **R5:** Info "Using file %s for backup", then ParseBackupFile (0x0051BED0..0x0051BF98). On failure: (0,0), with no
    log;
  - **R6:** clear the pending map; +0x58 = 0; +0x54 = 0 and +0x55 = 1. If the committed map is empty, return with no
    broadcast;
  - **R7:** for each committed node in list order: +0x58++, then NV Write(tag, data, size, fn{tag, ordinal from 1,
    total}, false);
  - **R8, the callback:**
    - +0x58--;
    - result ≤ −1: Warning "Writing to tag %s failed with %s", +0x54 = 1, and (0,0);
    - otherwise, if this is the last one and +0x54 == 0: Info "Restore complete!", (0,1), then WriteBackupFile.
  - **R9 (new):** HandleNVOpResult runs the callback (0x006433F6) **before** WriteDataForTag (0x00643418). So on the
    last restore write, a tag in the configured set gives WriteBackupFile **twice**.
- **0x6D WipeRobotGameData** (W1–W3):
  - if +0x58 ≠ 0: Warning "Have pending requests so not wiping", (1,0);
  - otherwise Info "Wiping robot game data"; clear both maps; +0x58 = 0; +0x54/+0x55 = 0;
  - for each tag in set +0x3C, ascending: +0x58++, then Erase(tag, fn). An empty set means no erases and no report;
  - no WriteBackupFile, and no file is deleted;
  - **the callback:** +0x58--; on a failure, Warning "Erase of tag %s failed with %s", +0x54 = 1, (1,0); on the
    final one with +0x54 == 0, Info "Wipe Complete!", (1,1).
- **0x6C RequestRobotRestoreData** (Q1, Q2, Q5):
  - FilesInDirectory(+0x48, bare names, no extension filter, not recursive): opendir failure → empty; **readdir
    order**; d_type == 8 only;
  - names shorter than 7 are skipped; cut at the first ".backup" (no match → skip); stoull(s, 10); Info
    "RobotDataBackupManager.RobotConnected.AllBackups" "Have backup for %llu"; push the low u32;
  - then Info "HandleRequestRobotRestoreData" "Will prompt for restore %s" ("True" iff +0x55 == 0), and broadcast
    RestoreRobotOptions{!+0x55, keys}.
- **0x6E RequestUnlockDataFromBackup** (U1/U2, **new**). RobotEventHandler subscribes it (0x005270E8; called at
  0x00525970). It runs the static handler 0x0051C5E4:
  - a null context → Error "...NullContext", errG;
  - otherwise: the selector, then the parser into a local map, then tag 0x182000, then UnlockedIdsList::Unpack, then
    copy the ids up to the first 0 (at most 0x200), then Info "Found unlock data in backup file %s", then UnlockStatus{ids,
    +0xC = 1};
  - on a miss, the NoBackup, FailedToParseBackup or NoUnlockData info, then UnlockStatus{GetDefaultUnlocks(ctx), +0xC =
    1}.

  **Unread:** Unpack (PLT 0x004A8518), GetDefaultUnlocks (PLT 0x004A8554), the IExternalInterface slot +0x1C,
  0x0051C8FE..0x0051C930, the control flow at 0x005257EE..0x00525970, and the second entry at 0x00529BC0.

## WriteBackupFile and the other methods (M15-033)

- **WriteBackupFile:**
  - if +0x55 == 0: Info "WriteBackupFile" "Not completed onboarding not writing backup", and return;
  - otherwise Info "Writing backup file". For each committed node from +0x30: key(4), then len(4), then the data;
  - an empty basename global (it starts empty) → Error "RobotDataBackupManager.Destructor" "Filename not set", errG,
    and no write;
  - otherwise WriteFile(dir + basename, bytes), fopen "wb", byte by byte. The result is ignored, with no log.
- **The committed map's order** (2.6): a libc++ unordered_map with the identity hash. bc starts at 0 and mlf at 1.0;
  the insert rules, growth, and the __next_prime small-primes table are as the check gives them (exact up to 211
  buckets).
- **IsValidTag:** set<uint> +0x3C.
- **ReadAll:**
  - a +0x58 ≠ 0 gate: Warning "Have pending requests so not reading";
  - otherwise, for each tag ascending: +0x58++, then Read(tag, fn{ordinal, total = +0x44}).
- **The read sink:**
  - +0x58--;
  - −1 → Info "TagNotFound"; < −1 → Warning "ReadFailed";
  - ≥ 0 → committed[tag] = bytes; for tag 0x181000, OnboardingData::Unpack → +0x55;
  - on the last one, WriteBackupFile, whatever the result.
- **QueueDataToWrite:** an invalid tag gives Debug "Ignoring invalid tag %s[%d]"; otherwise append to pending.
- **WriteDataForTag:**
  - no entry → Info "Tag %s[%d] doesn't exist in tagDataMap";
  - a failure, or not a write → erase the entry;
  - otherwise commit the front, pop it, and erase the entry if it's empty;
  - for 0x181000, Unpack → +0x55;
  - then WriteBackupFile.
- **WipeAll:** clear both maps, then WriteBackupFile (with onboarding at 1, that writes a zero-byte file).
- **The destructor:** WriteBackupFile, then free everything.

## Boundaries

- **A1:** the four messages are sent only by the app (Robot.cs:2053-2063, 2078-2082; SettingsVersionsPanel.cs:82,
  171, 241; RobotRestorePane.cs:36, 97, 105; DataPersistencePane.cs:131; UnlockablesManager.cs:121). So when they
  occur, and the restore key, are an **app input** (COMPATIBILITY_POLICY).
- **Q3:** `std::stoull` ships in libc++_shared.so (0x00065378..0x00065510), so it is RECOVERABLE. Only bionic strtoull,
  which it calls, is a system library. A throw is uncaught.
- **Q4:** the readdir order is filesystem order, a host input.
- **The member-name order:** getMemberNames walks the std::map in CZString order (memcmp over the shorter length, the
  shorter first). The selector keeps the lexicographically smallest key on a tie (strictly greater replaces,
  0x0051BBFC).

## Open questions for the operator (std::terminate in the original; no approved departure)

- a non-numeric `*.backup*` file name, through stoull;
- a statsForBackup.json whose ConnectionCount isn't an object or isn't numeric;
- a short or oversized backup file in ParseBackupFile.

The M15-039 note that said M1-034 covers statsForBackup.json was wrong: it doesn't. Until the operator rules, the
stack gives a visible MISSING at each of these points.
