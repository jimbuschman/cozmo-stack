# R-BACKUP-REST — RobotDataBackupManager

## Item 1 — the three subscribed game-to-engine handlers

| Step | Original behavior | Primary citation | Record / current classification | Coverage |
|---|---|---|---|---|
| 1.1 | Register 0x6B, 0x6D, 0x6C and retain scoped subscription handles. | 0x0051A3A8..0x0051A3D2; 0x0051A468..0x0051A492; 0x0051A528..0x0051A552 | M15-038 / RECOVERABLE_GAP | Read |
| 1.2 | Bound invoke functions extract the corresponding union member and call the real handlers. | Vtables 0x0101FEA0+0x18 → 0x0051DB7F; 0x0101FEE0+0x18 → 0x0051DBF7; 0x0101FF20+0x18 → 0x0051DC6B; invoke bodies 0x0051DB7E..0x0051DB92, 0x0051DBF6..0x0051DC08, 0x0051DC6A..0x0051DC7C | M15-038 / RECOVERABLE_GAP | Read |
| 1.3 | Restore with pending requests warns and broadcasts RestoreRobotStatus(false,false), then returns. | 0x0051B566..0x0051B5C6; warning call 0x0051B57E | NEW handler obligation / RECOVERABLE_GAP | Read |
| 1.4 | Restore creates the backup directory, strip=false, recursive=true. Failure logs channeled info NoDir and broadcasts (false,false). | 0x0051B5C8..0x0051B5DA; 0x0051B6BC..0x0051B71C | NEW / RECOVERABLE_GAP | Read |
| 1.5 | A nonzero requested robot key is tried as directory + decimal key + ".backup". If FileExists fails, use the automatic selector, just as for key zero. | 0x0051B5E2..0x0051B67C; 0x0051B67E..0x0051B7E8 | NEW / RECOVERABLE_GAP | Read |
| 1.6 | Automatic selector requires a non-null DataPlatform; null logs an error with empty text, sets errG, applies debug-break gate, returns false. | 0x0051BB32..0x0051BB34; 0x0051BC4A..0x0051BC9C | NEW / RECOVERABLE_GAP | Read |
| 1.7 | Selector tests directory + statsForBackup.json; absence warns ReadJson.NoFile and returns false. | 0x0051BB40..0x0051BB64; 0x0051BC9E..0x0051BCD4 | M15-039 boundary; NEW selector / RECOVERABLE_GAP | Read |
| 1.8 | Selector reads stats JSON; failed read logs ReadJson.Failed, sets errG/debug-break gate, returns false. | 0x0051BB68..0x0051BB8E; 0x0051BCD6..0x0051BD36 | NEW / RECOVERABLE_GAP | Read; DataPlatform read logs are the previously checked interface |
| 1.9 | Enumerate ConnectionCount member names; asUInt values compete against an initial maximum of zero. Replace selected name only on unsigned strictly-greater count. | 0x0051BB92..0x0051BC1C, especially 0x0051BBFC `cmp r6,r8`; 0x0051BBFE `bls` | NEW / RECOVERABLE_GAP | Read |
| 1.10 | Empty selected name logs NoFile and errG/debug-break; otherwise append ".backup" and return true. Does not check whether this selected backup actually exists. | 0x0051BC24..0x0051BC48; 0x0051BD46..0x0051BD98 | NEW / RECOVERABLE_GAP | Read |
| 1.11 | Selector failure logs NoBackup, sets errG/debug-break, broadcasts (false,false). | 0x0051B734..0x0051B7AC | NEW / RECOVERABLE_GAP | Read |
| 1.12 | Log chosen basename, then parse directory + basename into committed-data map +0x28. Empty/unreadable file warns and returns false; caller broadcasts (false,false). | 0x0051B7EC..0x0051B84C; 0x0051B918..0x0051B934; 0x0051BED0..0x0051BF9A | NEW / RECOVERABLE_GAP | Read |
| 1.13 | Nonempty parser clears target map, reads little-endian u32 tag, u32 size, copies size payload bytes, advances by size+8 until offset >= input size. Repeated tag overwrites. No tag-set or header/payload bounds validation. | 0x0051BF0A..0x0051BF4C | NEW / RECOVERABLE_GAP | Read; malformed out-of-bounds behavior is not a defined parse failure |
| 1.14 | Successful restore clears pending-data map +0x14; resets pending count and failure flag; sets completed-onboarding=true. Iterate committed unordered-map nodes and queue NV Write(tag,data,size,callback,false), incrementing pending count before each queue operation. | 0x0051B84E..0x0051B914; 0x0051B864 `mov.w r0,#0x100`; 0x0051B868 `strh [fp,#0x54]`; Write call 0x0051B8D8 | NEW / RECOVERABLE_GAP | Read; exact hash iteration evolution remains open below |
| 1.15 | Restore callback first decrements pending count. Every negative result warns, sets failure flag, broadcasts (false,false). A nonnegative result reports success only when captured ordinal == captured total and failure flag is zero. | Vtable 0x0101FD78+0x18 → 0x0051E6AD; 0x0051E6BA..0x0051E6DC; 0x0051E742..0x0051E7AE | NEW / RECOVERABLE_GAP | Read |
| 1.16 | Successful final restore callback logs "Restore complete!", broadcasts (false,true), then calls WriteBackupFile. It does not reload components or disconnect the robot. | 0x0051E6DE..0x0051E740 | NEW / RECOVERABLE_GAP | Read |
| 1.17 | Wipe with pending requests warns and broadcasts (true,false), returns. | 0x0051C04A..0x0051C0A0 | NEW / RECOVERABLE_GAP | Read |
| 1.18 | Otherwise log wipe; clear pending and committed maps; zero failure/onboarding bytes and pending count. Iterate configured set ascending and queue Erase(tag,callback,false), incrementing pending before each request. This is not NV WIPEALL. | 0x0051C0BC..0x0051C194; Erase call 0x0051C15C | NEW / RECOVERABLE_GAP | Read |
| 1.19 | Wipe callback decrements count; each negative result warns, sets failure flag, broadcasts (true,false). Nonnegative captured-final callback with no failure logs "Wipe Complete!" and broadcasts (true,true). No backup-file call on this success branch. | Vtable 0x0101FDB8+0x18 → 0x0051E8DD; 0x0051E8EA..0x0051E9D6 | NEW / RECOVERABLE_GAP | Read |
| 1.20 | RequestRestoreData lists regular-file basenames, nonrecursive, no extension filter, in directory enumeration order. Failed opendir produces an empty list. | 0x0051C250..0x0051C26A; 0x00802CF8..0x00802D30; 0x00802D70..0x00802DA8; 0x00802DE6..0x00802DF0; 0x00802E32..0x00802E54; 0x00802FF4..0x00803004; accept-all predicate 0x008043EA | NEW / RECOVERABLE_GAP | Read |
| 1.21 | Find first occurrence of ".backup" anywhere in each filename, erase from it to end; convert remaining prefix with stoull(base 10,null position), log u64, convert again and append low u32. No sorting/deduplication. | 0x0051C2C2..0x0051C394; stoull calls 0x0051C336, 0x0051C374; u32 store 0x0051C382 | NEW / RECOVERABLE_GAP | Read; external numeric-conversion details are an explicit boundary |
| 1.22 | shouldPromptForRestore = onboarding byte XOR 1; log True/False from the onboarding-byte zero test; broadcast RestoreRobotOptions with collected keys. No NV operations or file writes. | 0x0051C3AA..0x0051C412 | NEW / RECOVERABLE_GAP | Read |
| 1.23 | Unity really sends all three messages, including normal settings restore/wipe flows, not only debug UI. | unity/scripts/csharp/Robot.cs:2053–2063,2078–2082; RobotRestorePane.cs:36,97,105; Cozmo.Settings/SettingsVersionsPanel.cs:82,171,241; DataPersistence/DataPersistencePane.cs:131 | NEW app-input boundary / COMPATIBILITY_POLICY candidate | Read |
| 1.24 | Exact JsonCpp member-name ordering for equal connection counts, shipped unordered-map insertion/rehash ordering for restore, and malformed numeric-file exception behavior across the app boundary. | UNKNOWN at these deeper boundaries; call sites 0x0051BBAC, 0x0051B856..0x0051B914, 0x0051C336/0x0051C374 identified | NEW / RECOVERABLE_GAP (shipped helpers), external conversion boundary as applicable | Not claimed recovered |

This answers `re-analysis/research/requests/20261013-backup-manager-rest.md` under the research README. Native citations refer to the local `resources/lib/armeabi-v7a/libcozmoEngine.so`, 3.4.0-1204. I used Capstone Thumb disassembly with `skipdata=True`, checked callback-vtable words, and decoded the ARM portions of interworking veneers separately. Ghidra files were navigation aids only. “Read” describes this extraction's coverage, not a fidelity-manifest promotion. The classifications above preserve existing records or identify candidate new obligations; no record is settled by this answer.

### Binding and reports

The supplied helpers are subscription wrappers, not the message bodies. The real bodies are:

- RestoreRobotFromBackup: **0x0051B550..0x0051B95D**.
- WipeRobotGameData: **0x0051C038..0x0051C195**.
- RequestRobotRestoreData: **0x0051C240..0x0051C453**.

The invoke functions tail through interworking veneers 0x008CAEFC/0x008CAF0C/0x008CAF1C to PLT 0x004A8644/0x004A865C/0x004A8674. Each veneer is `bx pc`, then ARM `ldr ip,[pc]` / `add pc,ip,pc`; resolving its literal reaches the named PLT entry. This avoids treating Thumb-decoded veneer data as behavior.

RestoreRobotStatus is E2G **0x3D**, payload `(bool isWipe, bool success)`; RestoreRobotOptions is **0x3E**, payload `(bool shouldPromptForRestore, counted u32 robot keys)`. Field meaning/order comes from `unity/scripts/csharp/Anki.Cozmo.ExternalInterface/RestoreRobotStatus.cs:8–36,73–85`, `RestoreRobotOptions.cs:8–40`, and `MessageEngineToGame.cs:75–76`. Native halfword stores establish status values independently: 0 = restore failure, 0x100 = restore success, 1 = wipe failure, 0x101 = wipe success. Each native handler calls Robot::Broadcast; delivery to the C# external-interface API is the project boundary, not a requirement to reproduce Unity dialogs.

The restore callback's success check uses its **captured ordinal and total**, not `pending == 0`. Same for wipe and the read sink. A failure emits a failure status immediately for that tag; later negative tags can emit more. There is no cancellation of the remaining queued NV operations. Wipe's empty configured-set branch sends no final success report. Restore's nonempty unchecked parser normally inserts at least one map entry; no extra completion is synthesized for an empty iteration.

### Complete handler-level log inventory

All channeled lines below use channel **Unnamed**; warning/error lines use the corresponding unchanneled logger. The data vectors supplied to the logger are empty unless the format arguments noted below supply fields.

- Warning `RobotDataBackupManager.RestoreRobotFromBackup`: `Have pending requests so not restoring` (0x0051B574..0x0051B57E).
- Info `RobotDataBackupManager.RestoreFromBackup.NoDir`: `Failed to create backup dir` (0x0051B6CA..0x0051B6D4).
- Error `RobotDataBackupManager.RestoreFromBackup.NoBackup`: `No backup to restore from` (0x0051B73E..0x0051B74A), then errG/debug-break gate.
- Info `RobotDataBackupManager.RestoreFromBackup`: `Using file %s for backup`, selected basename (0x0051B7FC..0x0051B818).
- Error `RobotDataBackupManager.GetFileToUseForBackup.NullDataPlatform`: empty format (0x0051BC50..0x0051BC5C), then errG/debug-break gate.
- Warning `RobotDataBackupManager.GetFileToUseForBackup.ReadJson.NoFile`: `No backup stats file found` (0x0051BCA6..0x0051BCAC).
- Error `RobotDataBackupManager.GetFileToUseForBackup.ReadJson.Failed`: `Failed to read %s`, bare `statsForBackup.json` (0x0051BCE0..0x0051BCF8), then errG/debug-break gate. The called readAsJson also has its own previously checked DataPlatform logs.
- Error `RobotDataBackupManager.GetFileToUseForBackup.NoFile`: `Could not find a backup file to use from statsForBackup.json` (0x0051BD4C..0x0051BD58), then errG/debug-break gate.
- Warning `RobotDataBackupManager.ParseBackupFile.ReadFileFailed`: `Unable to read backup file` (0x0051BF58..0x0051BF5E).
- Info `RobotDataBackupManager.RestoreFromBackup`: `Restore complete!` (0x0051E6EA..0x0051E6F2).
- Warning same restore key: `Writing to tag %s failed with %s`, tag enum name, result enum name (0x0051E74A..0x0051E766).
- Warning `RobotDataBackupManager.WipeRobotGameData`: `Have pending requests so not wiping` (0x0051C056..0x0051C05E).
- Info same wipe key: `Wiping robot game data` (0x0051C0C8..0x0051C0D0), then `Wipe Complete!` on final success (0x0051E91A..0x0051E922).
- Warning same wipe key: `Erase of tag %s failed with %s`, tag/result enum names (0x0051E974..0x0051E990).
- Info `RobotDataBackupManager.RobotConnected.AllBackups`: `Have backup for %llu`, converted u64 key (0x0051C33A..0x0051C348). Despite its name this line belongs to the request handler, not RobotConnected.
- Info `RobotDataBackupManager.HandleRequestRobotRestoreData`: `Will prompt for restore %s`, `True` or `False` (0x0051C3AA..0x0051C3D8).

### File selection and malformed inputs

Automatic selection means **largest connection count**, not latest modification time, largest serial number, or most recent connection. Strict-greater replacement keeps the first enumerated name on a tie. All-zero counts fail selection. Selection does not check the chosen file's existence before returning true; ParseBackupFile handles the subsequent empty read. Requested nonzero key missing on disk silently falls back to the count-based selector.

The parser's nonempty branch has no magic, version, checksum, minimum-eight-byte check, maximum length check, set-membership check, or remaining-payload check. It reads unchecked and can access outside the file buffer. This is source behavior, not a recovered safe rejection rule. The existing M1-034 approvals name other loaders; this report does **not** extend them to malformed backup files or `stoull` failures.

RequestRestoreData matches a substring, so `123.backup.extra` qualifies and becomes prefix `123`. Its converter is called twice; the log retains u64, the returned options retain only the low u32. Exact external conversion/error semantics are not established here. The native body has cleanup/unwind paths rather than a local recovery-to-empty-list catch.

### App-input conclusion

`Robot.RestoreRobotFromBackup`, `WipeRobotGameData`, and `RequestRobotRestoreData` each assign the G2E union member and call `RobotEngineManager.Instance.SendMessage()` (`Robot.cs:2053–2063,2078–2082`). SettingsVersionsPanel requests options at Start, wipes after confirmation, and restores with key **0** after confirmation; RobotRestorePane also allows a selected key, and DataPersistencePane invokes wipe.

Under the approved app-boundary scope, the stack supplies the **request occurrence and restore-key value** from its API/host feature, with a named app-input policy. That does not make the engine-side selection, queueing, callback order, logs, or returned reports discretionary. If no stack feature supplies a given request, its handler is unreachable under that feature set; it is not an SDK-mode path. No manifest disposition is applied here.

## Item 2 — WriteBackupFile

| Step | Original behavior | Primary citation | Record / current classification | Coverage |
|---|---|---|---|---|
| 2.1 | Gate on +0x55 completed-onboarding byte. Zero logs info and returns without serialization or filesystem operation. | 0x0051A674..0x0051A67A; 0x0051A76A..0x0051A7A4 | M15-033 / RECOVERABLE_GAP | Read |
| 2.2 | Nonzero logs "Writing backup file", constructs a byte vector, visits committed-data map's linked nodes starting +0x30. | 0x0051A67C..0x0051A6C0 | M15-033 / RECOVERABLE_GAP | Read |
| 2.3 | Append each node's 4-byte key, 4-byte payload length, then payload. Follow node's next pointer, no sort. | 0x0051A6D2..0x0051A708, insert calls 0x0051A6DE/0x0051A6F2/0x0051A700 | M15-033 / RECOVERABLE_GAP | Read |
| 2.4 | Check process-global basename 0x01059D80. Empty basename logs Error Destructor/"Filename not set", sets errG and debug-break gate, writes nothing. | 0x0051A70A..0x0051A734; 0x0051A7A8..0x0051A7F6 | M15-033; input from M15-039 / IMPLEMENTATION_GAP | Read |
| 2.5 | Concatenate directory +0x48 and global basename, WriteFile(vector,false). Ignore returned bool. | 0x0051A736..0x0051A74A | M15-033 / RECOVERABLE_GAP | Read |
| 2.6 | WriteFile opens binary output mode 0x14; it truncates via fopen "wb". Failed open returns false silently; successful open writes bytes, flushes, closes, returns true even if close failed after setting stream state. | 0x0080385A..0x00803904; mode jump table 0x00538A5E/0x00538A62, entry for 0x14 → 0x00538A92 (`adr` to "wb"), fopen 0x00538AC4 | M15-033 helper / RECOVERABLE_GAP | Read at decision level; stream-buffer internals not expanded |
| 2.7 | Calls occur in destruction, accepted successful write promotion, WipeAll, captured-final backup-read sink, successful final restore callback. | 0x0051A600; 0x0051B1F2; 0x0051B54A → veneer 0x008CAECC → PLT 0x004A8380; 0x0051DFF8; 0x0051E73C | M15-033 plus NEW restore / RECOVERABLE_GAP | Read |
| 2.8 | Exact byte-record ordering across map mutations requires shipped hash-table insertion, erase, rehash policy. | UNKNOWN beyond the native traversal 0x0051A6BC..0x0051A708; map specialization call sites 0x0051DF5C,0x0051B0A0,0x0051BF2A | NEW / RECOVERABLE_GAP | Explicit remaining extraction |

The file is a headerless concatenation:

`u32LE tag | u32LE payloadLength | payload[payloadLength]`, repeated until EOF.

There is no record count, NV wire header, inter-record padding, compression, or JSON wrapper. It serializes **committed map +0x28**, not pending map +0x14 and not the configured set directly. Empty committed map with onboarding true writes a zero-byte file; the parser later rejects that file as unreadable/empty. Node traversal order is observable both in bytes and restore write order; substituting ascending keys would not reproduce this body.

The pathname is `<manager directory><process-global basename>`. The checked constructor establishes `<FilesPath>/output/robotBackups/`; checked RobotConnected sets the global basename to decimal `serialNumberHead` + `.backup`. WriteBackupFile does not derive a key from its Robot. Consequently a later connection can change the global used by another manager's subsequent write; this is a global, not an instance filename. The already checked RobotConnected read-stats failure returns before assigning it, which can leave the old global in use.

Logs are channeled info `Unnamed / RobotDataBackupManager.WriteBackupFile`: `Writing backup file` or `Not completed onboarding not writing backup` (0x0051A688..0x0051A68E and 0x0051A776..0x0051A77E). The empty-name error key is actually **RobotDataBackupManager.Destructor**, even outside destruction, format `Filename not set` (0x0051A7B0..0x0051A7B6). No backup-specific failed-write log follows WriteFile.

## Item 3 — remaining methods, sink, callers and tagsToBackup

| Step | Original behavior | Primary citation | Record / current classification | Coverage |
|---|---|---|---|---|
| 3.1 | IsValidTag performs unsigned membership lookup in configured set +0x3C; returns bool. No NV base normalization or factory-table validity rule here. | 0x0051AC26..0x0051AC60 (`bhs`/`bls` comparisons) | M15-033 / RECOVERABLE_GAP | Read |
| 3.2 | ReadAll refuses while +0x58 != 0, warning "Have pending requests so not reading"; otherwise enumerate the set ascending. | 0x0051AC76..0x0051ACC2; loop 0x0051AD22..0x0051AD40 | M15-033 / RECOVERABLE_GAP | Read |
| 3.3 | Before each Read increment +0x58; capture manager, tag, 1-based ordinal, initial set size. Queue Read(tag,callback,null,false). Does not clear committed map, pending map, onboarding, or failure flag. | 0x0051ACB2..0x0051AD0A; +0x44 total; callback vtable 0x0101FD38 | M15-033 / RECOVERABLE_GAP | Read |
| 3.4 | Backup sink decrements +0x58 first; result >=0 replaces committed map[tag] with returned bytes. Tag 0x181000 unpacks OnboardingData and assigns +0x55. | 0x0051DF3A..0x0051DF8E; OnboardingData::Unpack 0x0078E886..0x0078E8AC | M15-033 / RECOVERABLE_GAP | Read |
| 3.5 | Result -1 logs info TagNotFound with tag name. Other negatives warn ReadFailed with tag name. Neither deletes previous committed data nor changes onboarding. | 0x0051DF4A..0x0051DF52; 0x0051DF90..0x0051DFEA | M15-033 / RECOVERABLE_GAP | Read |
| 3.6 | If captured ordinal == captured total, call WriteBackupFile regardless of read result; empty configured set causes no read and no final sink call. | 0x0051DFEE..0x0051DFFE; 0x0051ACC2 | M15-033 / RECOVERABLE_GAP | Read |
| 3.7 | QueueDataToWrite accepts only IsValidTag; invalid logs debug "Ignoring invalid tag %s[%d]" and returns. | 0x0051AE0E..0x0051AE12; 0x0051AE3E..0x0051AE82 | M15-033 / RECOVERABLE_GAP | Read |
| 3.8 | Accepted data is copied and appended to per-tag vector-of-vectors at map +0x14; no file write or NV call. | 0x0051AE14..0x0051AE3C; slow append 0x0051AE84..0x0051AE90 | M15-033 / RECOVERABLE_GAP | Read |
| 3.9 | WriteDataForTag requires pending-map entry; absent entry logs info and returns. | 0x0051B068..0x0051B082; 0x0051B110..0x0051B158 | M15-033 / RECOVERABLE_GAP | Read |
| 3.10 | If result <0 or isWrite=false, erase the entire pending-map entry and return. Committed data is not removed and no file is written on this branch. | 0x0051B084..0x0051B08A; 0x0051B15A..0x0051B162 | M15-033 / RECOVERABLE_GAP | Read |
| 3.11 | Otherwise copy first pending vector to committed[tag], erase just that vector by shifting the rest forward, delete map entry if exhausted. | 0x0051B08C..0x0051B10E; 0x0051B164..0x0051B1A0 | M15-033 / RECOVERABLE_GAP | Read |
| 3.12 | Successful write to 0x181000 with nonempty committed vector unpacks onboarding, updates +0x55; then call WriteBackupFile for every successful promoted write. | 0x0051B1A2..0x0051B1F2 | M15-033 / RECOVERABLE_GAP | Read |
| 3.13 | WipeAll clears pending and committed maps, then tail-calls WriteBackupFile. Does not clear tags set, failure/onboarding bytes, count, directory, global basename; no NV operation itself. | 0x0051B530..0x0051B54A and resolved veneer 0x008CAECC | M15-033 / RECOVERABLE_GAP | Read |
| 3.14 | Destructor sets its vtable, calls WriteBackupFile before deleting directory, set, committed map, pending map, scoped subscription handles. Deleting destructor then deletes this. | 0x0051A5F4..0x0051A636; deleting wrapper 0x0051A908..0x0051A914 | M15-033 / RECOVERABLE_GAP | Read |
| 3.15 | ReadAll caller is Robot constructor after VisionComponent::Init and before AIComponent::Init; not the RobotConnected C9 callback. | 0x005103BC..0x005103DC, call 0x005103D4 | M15-033, M3-033 boundary / RECOVERABLE_GAP | Read |
| 3.16 | QueueData callers are NVStorageComponent::ProcessRequest: ERASE receives empty vector; WRITE receives a copy of queued whole payload. | 0x00645160..0x00645172; 0x006452A2..0x006452B8 | M15-033, M3-043 boundary / RECOVERABLE_GAP | Read |
| 3.17 | Write-family terminal path runs callback, then WipeAll for op==3 irrespective of result, else WriteDataForTag(normalizedTag,result,op==1), then SetState(0). | 0x006433F0..0x00643420 | M15-033, M3-031/M3-043 boundary / RECOVERABLE_GAP | Read |
| 3.18 | Destruction occurs from NVStorageComponent destruction; constructor exception cleanup also calls its destructor. | 0x00643F48; 0x00642A8C; deleting wrapper 0x0051A90A | M15-033, M3-038 boundary / RECOVERABLE_GAP | Direct callers identified |
| 3.19 | Whole-library caller exhaustiveness beyond direct branches, virtual/indirect callbacks, and the resolved veneers. | UNKNOWN for an exhaustive address-taken/indirect-use census; local vtable routes and .text direct-call scan were checked | M15-033 / RECOVERABLE_GAP | No universal no-other-caller claim |

### State and use of the configured set

- `+0x14`: unordered map `tag → vector<vector<byte>>`, pending payloads in per-tag FIFO order.
- `+0x28`: unordered map `tag → vector<byte>`, committed backup contents. First node at +0x30, count at +0x34.
- `+0x3C`: ordered `set<uint>` configured from tagsToBackup; root/sentinel +0x40, size +0x44.
- `+0x48`: directory string.
- `+0x54`: operation failure flag.
- `+0x55`: completed-onboarding flag.
- `+0x58`: pending read/restore/erase callback count.

The set controls **ReadAll**, **WipeRobotGameData's Erases**, and **QueueDataToWrite membership**. It is not consulted by WriteBackupFile, ParseBackupFile, the restore write loop, RequestRestoreData, or WipeAll. Restore therefore queues tags found in a backup even when they are absent from the configured set; the NV API's own validity rules still apply at that interface. Such a tag has no queued backup mirror from QueueDataToWrite, so the later WriteDataForTag can take its missing-entry log branch.

Successful erase is deliberately in WriteDataForTag's `isWrite == false` branch: it discards pending mirror data, leaving old committed backup data untouched. It does not promote an empty payload. Failed write drops **all** pending mirror vectors for the tag, not just the oldest; later completions can consequently find no entry. Neither behavior should be replaced by a conventional cache-update rule.

Read sink logs are info `Unnamed / RobotDataBackupManager.ReadAllDataFromRobot.TagNotFound`, format `%s`, and warning `RobotDataBackupManager.ReadAllDataFromRobot.ReadFailed`, format `%s`. The callback does not log a successful read itself. Queue invalid-tag debug uses `Unnamed / RobotDataBackupManager.QueueDataToWrite`, format `Ignoring invalid tag %s[%d]`. Missing pending tag info uses `Unnamed / RobotDataBackupManager.WriteDataForTag`, format `Tag %s[%d] doesn't exist in tagDataMap` (native format strings at 0x0051E064/0x0051E06C, 0x0051E02C, 0x0051AEBC/0x0051AEE8, 0x0051B228/0x0051B250 respectively).

The sink function's vtable entry is **0x0101FD50 → 0x0051DF35** (Thumb). It receives pointer, size, and signed NVResult by reference, as shown by the loads at 0x0051DF3A/0x0051DF3E/0x0051DF40. It copies data rather than retaining the NV buffer. The OnboardingData unpack wrapper reads one bool through SafeMessageBuffer at 0x0078E896; the sink initializes the local bool to zero first. No assertion about arbitrary malformed SafeMessageBuffer inputs is added here.

## Item 4 — comparison with the stack

| Step | Native obligation / observed stack behavior | Evidence | Record / current classification | Coverage |
|---|---|---|---|---|
| 4.1 | Stack queues eight ascending backup reads with null explicit sink and default no broadcast. Matches native read tags/order for the checked shipped config. It drops results through NoSink. | Native 0x0051ACB2..0x0051AD40; CozmoEngine.cs:811,815–817,830–832 | M15-033 / RECOVERABLE_GAP | Compared; shipped config contents rely on the prior checked extraction |
| 4.2 | Stack has no manager pending-count gate, captured ordinal/total, committed map, onboarding update, read outcome logs, or final file write. | Native 0x0051AC76..0x0051AD0A and 0x0051DF34..0x0051DFFE; CozmoEngine.cs:811–817 | M15-033 / RECOVERABLE_GAP | Concrete implementation absence |
| 4.3 | Stack supplies queue-write placeholders at ERASE and WRITE dispatch, not the per-tag payload FIFO. | Native 0x00645172,0x006452B8,0x0051AE04..0x0051AE90; NvStorage.cs:727–728,769,789 | M15-033 / RECOVERABLE_GAP | Compared |
| 4.4 | Stack's terminal delivery preserves named callback-before-backup-before-idle boundaries, but the backup calls only emit MISSING. | Native 0x006433F0..0x00643420; NvStorage.cs:1293–1314 and terminal SetState after delivery | M15-033 / RECOVERABLE_GAP | Interface placement compared; not a full NV subsystem re-verification |
| 4.5 | No real backup-manager destructor write/cleanup in stack. | Native 0x0051A600..0x0051A630 and 0x00643F48; NvStorage.cs:Dispose/DestroyLocked | M15-033 / RECOVERABLE_GAP | Compared |
| 4.6 | Stack does not implement these three backup/restore handlers or their reports in Cozmo.Robot. The existence of generic NV APIs is not their implementation. | Native handler bodies above; rg of Cozmo.Robot for RobotDataBackup/RestoreRobotFromBackup/RequestRobotRestoreData/RestoreRobotStatus/RestoreRobotOptions returns only backup comments/placeholders | M15-038 plus NEW / RECOVERABLE_GAP | Compared |
| 4.7 | Constructor config/directory/subscription behavior is missing; eight hard-coded tags are a fixed-config match, not generic tagsToBackup reproduction. | Prior checked constructor 0x00519AB0..0x00519D8A; CozmoEngine.cs:830–832 | M15-038 / RECOVERABLE_GAP | Existing checked evidence retained |
| 4.8 | RobotConnected stats and global basename remain missing in the inspected stack. This request establishes how that basename is consumed. | Prior checked 0x0051A010..0x0051A25C; native consumer 0x0051A70A..0x0051A74A; NvStorage.cs backup placeholders | M15-039 / IMPLEMENTATION_GAP | Existing checked evidence retained; consumer newly read |
| 4.9 | Exact insertion/rehash ordering, JsonCpp tied-name order and externally supplied filesystem enumeration/conversion outcomes are not proved by this comparison. | UNKNOWN boundaries in 1.24, 2.8, 3.19 | NEW / RECOVERABLE_GAP or named host-input boundary | Explicit uncertainty |

The stack already implements the **NV interface scaffolding**: connection reads, ERASE/WRITE mirror-call positions, and terminal callback/backup/idle ordering. It does not implement the backup consumer. In particular, replacing the NoSink callback with only a byte cache would still omit onboarding, failed-read retention, captured-final scheduling, logs, serialized map order, global filename use, and destructor behavior.

The stack's eight constants are `0x180000,0x181000,0x182000,0x183000,0x184000,0x194000,0x195000,0x196000`. The earlier Opus-checked extraction states these are the shipped backup_config.json tags. I did not independently locate that JSON in this checkout's enumerated `re-analysis/obb/` or tracked root-file listing; this is retained as **checked-note authority 6**, not relabeled as fresh asset evidence. Native set-based iteration was independently checked here.

### Current records, disposition and open boundaries

The manifest was read during this task. Its current entries are:

- **M15-033**, title `RobotDataBackupManager: NV write backup, wipe, destruction and the backup reads' sink`, status **RECOVERABLE_GAP**; evidence names QueueDataToWrite 0x0051AE04, WriteDataForTag 0x0051B060, WipeAll 0x0051B530, destructor 0x0051A5F0, sink 0x0051DF34. These are genuinely shipped recoverable paths; this answer reads their bodies and WriteBackupFile. The manifest's “bodies unread” text is a pre-report state, not a contradicted behavior claim.
- **M15-038**, title `RobotDataBackupManager constructor: the robotBackups directory, backup_config.json tagsToBackup, and the HasExternalInterface-gated subscriptions`, status **RECOVERABLE_GAP**; evidence is the checked constructor report and helpers 0x0051A3A0/0x0051A460/0x0051A520. Their binding is now read. Handler behavior is broader than the current constructor title and should not disappear into its subscription evidence.
- **M15-039**, title `RobotDataBackupManager::RobotConnected (C9): statsForBackup.json connection count, read/write logs, the backup basename`, status **IMPLEMENTATION_GAP**; evidence names 0x0051A010..0x0051A25C and global basename assignment. This report confirms the consumer connection; it does not independently re-extract every stats JSON operation or reclassify the record.

No current settled claim is contradicted by these findings, and no status change is made. The three handler paths, automatic selection, unchecked parser, returned reports, and app-input boundaries need explicit ownership before any broader backup/restore claim can be settled. All native method bodies requested are read; **whole production-path exactness is not claimed** while the named helper boundaries remain.

Remaining extraction is bounded: (1) committed-map insertion/rehash/erase order, because it changes backup bytes and restore NV order; (2) JsonCpp getMemberNames order to settle equal-count ties; (3) external numeric conversion and host enumeration input policy, including invalid names and filesystem outcomes; (4) any desired safe policy for malformed backup data, which is not covered by an approval inferred here; (5) exhaustive indirect caller census if required beyond identified production callers. Stream/file helpers were read at their behavior-changing decision interfaces, not every libc++ buffering instruction. No equivalence or bulk source classification is proposed for the unexpanded shipped helper code.

Research only: no implementation, manifest/inventory changes, branches, commits, tests, hardware runs, or uploads. Only this answer file was written.
