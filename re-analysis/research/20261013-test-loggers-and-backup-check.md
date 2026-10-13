# Opus check of R-LOGGERS-BACKUP (2026-10-13)

- **Checked:** `research/20261013-test-loggers-and-backup.md` (Codex), against libcozmoEngine.so. Read-only
  cozmo-extractor.
- **Adopted:** by the manager on 2026-10-13. This file is the checked source for job B-M3M4-18. Use it, not the Codex
  report.
- **Result:** all four items hold. Four omissions are filled below.

## DataPlatform paths (pathToResource 0x0084BE34..0x0084BF16)

| Scope | Base | Suffix | App input (RobotEngineManager.cs:533-536) |
|---|---|---|---|
| 0 | +0x00 DataPlatformFilesPath | "/output" | `Application.persistentDataPath` |
| 1 | +0x24 DataPlatformResourcesPath | none | `PlatformUtil.GetResourcesFolder()` |
| 2 | +0x0C DataPlatformCachePath | none | `Application.temporaryCachePath` (M5-060) |
| 3 | +0x0C | "/gameLogs" | the same |
| 4 | +0x18 DataPlatformExternalPath | none | `temporaryCachePath` |

- **The join:** if the resource is non-empty and doesn't start with '/', a "/" is added, then the resource. A trailing
  slash on the base isn't normalised.
- **The bases** come from cozmo_startup's config keys (0x006658C6..0x006659D4). They are app inputs.

## M7-028: the test behaviours' constructor side effects

- **L1, the directories:** DockingTestSimple (0x005CB04E..0x005CB0A8) and LiftLoadTest (0x005D3F32..0x005D3F8C) each
  build a RollingFileLogger. The path is `<CachePath>/dockingTest` or `<CachePath>/liftLoadTest`, with extension ".log"
  (0x01036950 → 0x00C24294) and size 0x1400000.
- **L2, the constructor:** it calls `CreateDirectory(path, false, true)` (0x0080DFC6..0x0080DFCC), and ignores the
  result.
  - **CreateDirectory** (0x008028F0..0x008029C8):
    - an empty path returns true;
    - otherwise, for each '/' prefix, if `!DirectoryExists` (stat, and the S_IFDIR bit), mkdir(prefix, 0700). A
      failure returns false, with no rollback;
    - the iteration count is bounded at 200;
    - there is no log and no errG.
  - With two containers, that is 4 calls per run, creating at most 2 leaf directories.
- **L3, no files:** no file is opened or written. The filebuf is constructed with no fopen; the destructor closes a
  never-opened file (a null FILE → return 0), and removes nothing (0x0080E090..0x0080E11E, 0x00501120..0x0050113E).
- **L4, the rest of LiftLoadTest's constructor:**
  - +0x11C/+0x120/+0x124 = 0 (0x005D3F20..0x005D3F2E);
  - +0x224 (strh) = 0; +0x21C/+0x220 = 0; +0x218 = 0; +0x219 = 1;
  - subscriptions: E2G 0x35 RobotOffTreadsStateChanged, G2E 0x95 SetLiftLoadTestAsRunnable, and robot DA;
  - no sends and no logs.

  The IBehavior base constructor (0x005D3F18) is not read here.
- **L5, FactoryTestLogger(true)** (0x00671658..0x006716E0): memory only. The bool selects JSON mode: ".json", and the
  root is written on close.

## M15-033 split: RobotDataBackupManager

### The constructor (new RECOVERABLE_GAP record)

1. +0x48 = pathToResource(scope 0, "robotBackups/") = `<FilesPath>/output/robotBackups/`, or "" with no data platform
   (0x00519AB0..0x00519B22). Then +0x58 = 0 and +0x54 = 0.
2. **Only if HasExternalInterface** (0x00519B34):
   - C9 → RobotConnected;
   - G2E 0x6B RestoreRobotFromBackup, 0x6D WipeRobotGameData and 0x6C RequestRobotRestoreData, through helpers
     0x0051A3A0, 0x0051A460 and 0x0051A520. Those bodies are **unread**.
3. **Ungated:** CreateDirectory(+0x48, false, true). On false: Error "RobotDataBackupManager.RestoreFromBackup.CreateDir"
   (0x00519EDC), "Unable to create directory for robot backups" (0x00519F10), errG, and the config read is **skipped**
   (0x00519BE2..0x00519C64).
4. **backup_config.json:**
   - the path is pathToResource(scope 1, "config/engine/backup_config.json") (global 0x01059D70);
   - FileExists, then readAsJson;
   - each name in root["tagsToBackup"] → asUInt → the set<uint> at +0x3C;
   - a read failure is Error "RobotDataBackupManager.Constructor.ReadJson" (0x00BE76DE), "Failed to read
     backup_config.json" (0x00519F54), then errG. A missing file is silent (0x00519BEE..0x00519D8A).

   The shipped file has 8 tags. The stack hard-codes them (CozmoEngine.cs:830).

### RobotConnected on C9 (new IMPLEMENTATION_GAP record; 0x0051A010..0x0051A25C)

- **The key:** to_string(the first wire word of RobotAvailable, serialNumberHead) (0x0051A016..0x0051A022).
- **FileExists**(+0x48 + "statsForBackup.json"): stat == 0 **and** the regular-file bit (0x00803446..0x0080344A).
- **If it exists,** readAsJson (fopen "r"; Reader::parse with comments):
  - a failure logs Error "DataPlatform.readAsJson" (0x0084C2CC), "Failed to read [%s]" with the full path, then errG,
    then a Debug "Json reader errors [%s]" if there are any (0x0084C0CC..0x0084C18C);
  - then the caller logs Error "RobotDataBackupManager.RobotConnected.ReadStatsFailed" (0x0051A308), "Failed to read
    %s" with the bare file name, then errG, and **returns**.
- **The count:**
  - if the file exists: root["ConnectionCount"][key] = uint(asUInt(old) + 1), where null counts as 0;
  - if it doesn't: Value(int 1).
- **writeAsJson**(scope 0, "robotBackups/statsForBackup.json") (0x0084C328..0x0084C4B6):
  1. Info "Unnamed" "DataPlatform.writeAsJson", "writing to %s" (full path);
  2. CreateDirectory(full, strip = true, true). A failure logs Error "Failed to create folder %s", then errG, and
     returns 0;
  3. StyledStreamWriter("\t"), then open(path, 0x10). A failed open returns 0 with no log;
  4. write, close, and return 1.
- **If the write fails:** Error "RobotDataBackupManager.RobotConnected.WriteStatsFailed" (0x00BE7755), "Failed to write
  backup stats file", then errG, and **carry on**.
- **The global basename** (0x01059D80) = key + ".backup". Its reader, WriteBackupFile (0x0051A66C..0x0051A907), is
  unread.
- **No** NV call, send, broadcast or timer. It runs synchronously on every C9, first among the C9 subscribers.
- The JsonCpp throws on a malformed stats file fall under M1-034.
- **The stack** has none of this (NvStorage.cs:259-264, 1036).

## Leads, not findings

- **M3-042:** its evidence says scope 2 + "imu_logs" is "built on the app-supplied persistent path". Scope 2 is the
  **cache** path (+0x0C). Check whether imu_logs really uses scope 2: if it does, the record names the wrong input.
- **M4-011:** its evidence maps scope 4 to "local application data". Scope 4 is +0x18, DataPlatformExternalPath
  (`temporaryCachePath`).

## Unread

- 0x00519F8C..0x0051A00F;
- 0x0051A3A0..0x0051A5EF (the G2E helpers);
- 0x0051A5F0..0x0051A66B (the destructor);
- 0x0051A66C..0x0051A907 (WriteBackupFile);
- 0x0051AC26..0x0051B54F;
- the sink at 0x0051DF34;
- the IBehavior constructor;
- FactoryTestLogger's StartLog and CloseLog bodies.
