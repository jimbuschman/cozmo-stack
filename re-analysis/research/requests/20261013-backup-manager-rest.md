# Research request R-BACKUP-REST (M15-039, M15-033)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-test-loggers-and-backup-check.md`, and the M15-033, M15-038 and M15-039 records in
  `re-analysis/fidelity_manifest.json`. Compare with cozmo-stack/src/Cozmo.Robot/NvStorage.cs and CozmoEngine.cs:811-830.
- **Answer file:** `research/20261013-backup-manager-rest.md`.
- **Format:** one coverage table per item, first. Each behaviour-changing step gets an address, or UNKNOWN.
- **Tooling:** use a disassembler that skips undecodable data (capstone with skipdata).

## Items

1. **The three game-to-engine handlers** that RobotDataBackupManager subscribes when HasExternalInterface is set:
   0x6B RestoreRobotFromBackup, 0x6D WipeRobotGameData and 0x6C RequestRobotRestoreData. Their helpers are 0x0051A3A0,
   0x0051A460 and 0x0051A520. Find each bound handler and read it completely: logs, file reads and writes, NV
   operations, and messages sent back.

   Say whether the app ever sends these messages (search the Unity scripts under unity/scripts/csharp). Under the
   project's scope rule, a message only the app sends becomes an app input.
2. **WriteBackupFile** (0x0051A66C..0x0051A907): what it writes, where (the global basename `<key>.backup`, 0x01059D80),
   the format, and when it is called.
3. **The rest of the backup manager:**
   - IsValidTag, ReadAll, QueueDataToWrite (0x0051AE04), WriteDataForTag (0x0051B060) and WipeAll (0x0051B530), in the
     range 0x0051AC26..0x0051B54F;
   - the destructor (0x0051A5F0..0x0051A66B);
   - the sink (0x0051DF34..).

   For each, give its callers, and how the tagsToBackup set (+0x3C) is used.
4. **The comparison:** which of these the stack already does (NvStorage.cs, and the 8 backup reads at
   CozmoEngine.cs:811), and where it differs.
