# R-LOGGERS-BACKUP — test loggers and connection-time backup

## Item 1 — coverage: RollingFileLogger

| Step | Original behavior | Citation | Record / classification |
|---|---|---|---|
| Docking constructor input | Scope 2, resource dockingTest, default extension, null queue, size 0x1400000; logger at behavior+0x154 | 0x005CB04E..0x005CB0A8 | M7-028 / RECOVERABLE_GAP; native read |
| Lift constructor input | Scope 2, resource liftLoadTest, same extension/null queue/size; logger at behavior+0x128 | 0x005D3F32..0x005D3F8C | M7-028; native read |
| Scope 2 base | Copy DataPlatform string at +0x0C, then append slash and resource; no output/gameLogs suffix | 0x0084BE82..0x0084BE9A; 0x0084BED8..0x0084BF18 | M7-028; native read |
| Base origin | +0x0C is constructor's second string, supplied from configuration DataPlatformCachePath; official app supplies Application.temporaryCachePath | DataPlatform ctor 0x0084BD14..0x0084BD38; engine initialization 0x006658EE..0x00665912 / 0x006659CA..0x006659D4; RobotEngineManager.cs:534 | M7-028; native + app evidence |
| Full paths | DataPlatformCachePath + /dockingTest and + /liftLoadTest. Device-specific absolute cache root is a host/platform input, not a shipped literal | Same path/input citations; actual phone root UNKNOWN here | M7-028; boundary explicit |
| Default extension | kDefaultFileExtension is pointer at 0x01036950 to .log at 0x00C24294 | ELF symbol/data relocation; caller dereferences 0x005CB07C..0x005CB084 / 0x005D3F5C..0x005D3F6A | M7-028; native read |
| Logger state | Store supplied queue +4; own queue +8=null; path +0x0C; extension +0x18; current filename +0x24 empty; limit +0x30=0x1400000; +0x34=0; construct unopened ofstream | 0x0080DEFC..0x0080DFC2; filebuf ctor 0x00537E48..0x00537EDC | M7-028; native read |
| Directory request | CreateDirectory(path, false, true): whole path is directory; create parents | 0x0080DFC6..0x0080DFCC | M7-028; native read |
| Existing directory | Test each prefix with DirectoryExists; skip mkdir for existing prefixes; ordinary existing full directory returns true | 0x00802910..0x0080297C; stat/mode test 0x00802824..0x0080284C | M7-028; native read |
| Missing directory | For each missing prefix call mkdir(prefix, 0700). Existing directories' permissions are not changed | 0x00802930..0x0080294A: mode 0x1C0 | M7-028; native read |
| Failure | First nonzero mkdir result returns false; no retry, log, errno-specific recovery or error-global write. Parent prefixes already created remain | 0x0080294A → 0x008029A6..0x008029C8 | M7-028; native read |
| Iteration limit | Prefix walk bounded at 200; hitting bound returns false | 0x0080295E..0x0080297C / 0x008029B6..0x008029C8 | M7-028; native read |
| Constructor response to failure | Ignore directory helper result; return logger object. No file open/create/write, filename generation or logger Write call | 0x0080DFCC..0x0080DFD4; entire ctor | M7-028; native read |
| Destructor | Stop/release own queue if nonnull; close filebuf, set stream failbit on null close result; destroy stream/filebuf and strings; release queue member again | 0x0080E090..0x0080E11E; queue helper 0x00801E70..0x00801E86 | M7-028; native read |
| Never-used logger destructor | Filebuf FILE* remains null: close returns null without file IO. No file/directory deletion or creation | FILE* init 0x00537E82; close 0x0050111C..0x0050113E; whole logger dtor | M7-028; native read |
| Double construction | Two directory-attempt sequences per behavior; shared directory path. Successful first creation makes second attempt encounter existing directories | Adopted subscribers check, Robot ctor 0x0050FCAC / 0x0050FCBE; constructors above | M7-028; checked context + consequence |

## Item 2 — coverage: remainder of LiftLoadTest constructor

| Step | Original behavior / observable boundary | Citation | Record / classification |
|---|---|---|---|
| Temporary cleanup | After logger construction, destroy temporary extension/path/resource strings | 0x005D3F90..0x005D3FB6 | M7-028 / RECOVERABLE_GAP; native read |
| Instance initialization | +0x224/+0x225=0; uint32 +0x21C/+0x220=0; +0x218=0 and +0x219=1 | 0x005D3FBA..0x005D3FCC | M7-028; native read |
| Engine→game subscription | Subscribe set containing ushort tag 0x35, RobotOffTreadsStateChanged | Tag at 0x00C6AAE4; set insert 0x005D3FDC; subscribe 0x005D3FE4 | M7-028; native read; original serializer name below |
| Game→engine subscription | Subscribe set containing ushort tag 0x95, SetLiftLoadTestAsRunnable | Tag at 0x00C6AAE6; set insert 0x005D4004; subscribe 0x005D400C | M7-028; native read |
| Raw DA subscription | Read Robot ID at +0x10; subscribe byte tag DA | 0x005D401A..0x005D4038; byte 0x00C6AAE8 | M7-028; native read; contextual boundary |
| Ownership | Retain handles through distinct IBehavior overloads; destroy temporary tag sets after registration | 0x005D3FEE / 0x005D4016 / 0x005D4040; overload bodies below | M7-028; native read |
| Immediate outputs | No constructor log, app broadcast, robot send, test-start action or logger Write in this derived constructor, apart from directory attempt in item 1 | Entire derived body 0x005D3F10..0x005D4048 | M7-028; native read; base ctor separate |
| Later inbound effects | G→E 0x95 AlwaysHandle sets +0x219=1 even while inactive; E→G 0x35 active handler can set state on off-treads notification | 0x005D4F30..0x005D4F80; 0x005D4E64..0x005D4ECA | M7-028; native read; delivery is later, not constructor send |
| Base-constructor boundary | IBehavior constructor call precedes requested range; its full configuration/event side effects are not re-extracted here | 0x005D3F18 → IBehavior ctor; UNKNOWN for exhaustive base-ctor effects in this pass | M7-028; RECOVERABLE_GAP boundary |

## Item 3 — coverage: FactoryTestLogger constructor

| Step | Original behavior | Citation | Record / classification |
|---|---|---|---|
| Caller argument | FactoryTest constructs embedded logger at +0x240 with bool true (1), twice in checked construction path | 0x005D0516..0x005D051A, especially MOVS r1,#1 at 0x005D0518 | M7-028 / RECOVERABLE_GAP; native read |
| Empty names | Construct empty std::strings at logger+0 and +0x0C | 0x00671658..0x00671682 | M7-028; native read |
| Stream | Construct unopened ofstream at +0x18, filebuf at +0x1C; no filename/open invocation | 0x00671684..0x006716CC; filebuf ctor 0x00537E48..0x00537EDC | M7-028; native read |
| JSON/state | Construct null Json::Value at +0xD0; store bool at +0xE8 | 0x006716D0..0x006716E0 | M7-028; native read |
| Bool meaning | JSON-versus-text output mode: true chooses .json, false .txt in later StartLog; true serializes accumulated Json::Value in later CloseLog | 0x00671C90..0x00671CA4; 0x00671806..0x00671812 | M7-028; native read |
| Constructor file effects | No directory creation, file open/create/write, log emission, StartLog or CloseLog. Allocates/initializes memory and stream state only | Complete body 0x00671658..0x006716E2 and filebuf ctor | M7-028; native read |

## Item 4 — coverage: RobotDataBackupManager::RobotConnected

| Step | Original behavior | Citation | Record / classification |
|---|---|---|---|
| Subscription gate | NVStorage embeds manager; HasExternalInterface==1 registers C9/RobotConnected | 0x006428A4; 0x00519B34..0x00519B78 | M15-033 / RECOVERABLE_GAP; native read |
| Owner backup directory | Manager+0x48 stores scope-0 pathToResource(robotBackups/): files root + /output/robotBackups/ | 0x00519AC8..0x00519B14; scope 0 at 0x0084BE44..0x0084BE62 | M15-033; native read |
| Connection key | Get robotAvailable, convert its first uint32 payload word to unsigned decimal string. This is payload data, not the subscription Robot ID | 0x0051A016..0x0051A022 | M15-033; native read |
| Stats names | statsForBackup.json global 0x01059D50; ConnectionCount global 0x01059D60; current backup filename global 0x01059D80 | Initializer 0x004D6DC0..0x004D6E86; literal strings 0x00C49AF0 / 0x00C49B08 | M15-033; native read |
| Existence check | FileExists(manager path + stats filename); stat failure returns false, with no distinct error branch in caller | 0x0051A02E..0x0051A056; FileExists 0x0080342A..0x00803450 | M15-033; native read |
| Existing file read | readAsJson(absolute filename, local null JSON root); read must return true to continue | 0x0051A058..0x0051A086; reader 0x0084C01C..0x0084C206 | M15-033; native read |
| Read failure | Error ReadStatsFailed / Failed to read %s, filename-only argument; _errG=1, optional debug break; return without stats write or filename replacement | 0x0051A1FC..0x0051A25C → 0x0051A1E4; log 0x0051A21C | M15-033; native read |
| Existing JSON count | localRoot[ConnectionCount][decimalKey] = asUInt(old value)+1, unsigned 32-bit wrap | 0x0051A08A..0x0051A0BE | M15-033; native read |
| Missing stats file | localRoot[ConnectionCount][decimalKey] = integer 1 | 0x0051A0C4..0x0051A0E4 | M15-033; native read |
| File write | writeAsJson(scope=0, robotBackups/statsForBackup.json, entire modified root), synchronously | 0x0051A0EA..0x0051A132; scope zero at 0x0051A0F8 | M15-033; native read |
| Write helper log/directory | Info Unnamed/DataPlatform.writeAsJson, writing to %s; CreateDirectory(full filename,true,true) for parent directories | 0x0084C346..0x0084C39A | M15-033; native read |
| Write helper file IO | Open output stream in mode 0x10 → fopen(...,"w"); serialize styled JSON with tab indentation; close; return true if opened, even after null close result | 0x0084C3AC..0x0084C454; open 0x0084C5F4..0x0084C628 → 0x00538A3C..0x00538AC4 | M15-033; native read |
| Write helper failure | Parent creation failure logs Error Failed to create folder %s, sets _errG and optionally breaks; returns false. Open failure returns false without that folder log | 0x0084C426..0x0084C42A; 0x0084C456..0x0084C4B4 | M15-033; native read |
| Caller write failure | Error WriteStatsFailed / Failed to write backup stats file; _errG=1, optional break; continue to replace current backup filename | 0x0051A152..0x0051A19E → 0x0051A1A2 | M15-033; native read |
| Global filename update | Replace shared global std::string 0x01059D80 with decimalKey + .backup; no backup file created by this assignment | 0x0051A1A2..0x0051A1E2; suffix 0x00BE778C | M15-033; native read |
| Cleanup/errors | Destroy local JSON/string objects; exception landing pads unwind. No rollback/retry/fallback for JSON conversion/index exceptions in this handler | 0x0051A1E4..0x0051A1FA; 0x0051A25E..0x0051A2F0 | M15-033; native read; JSON conversion boundary below |
| NV operations / messages | No NVStorage Read/Write/Wipe, robot-message send, app broadcast, restore, timer or delayed callback initiated by RobotConnected | Complete body 0x0051A010..0x0051A25C and filesystem callees | M15-033; native read |
| Timing | Runs synchronously on each delivered C9, before Impl HeadID handler and RIC; no once-only guard | Body above; adopted subscriber check's C9 order and emitter 0x0069E274..0x0069E2B2 | M15-033; native + checked context |
| Stack comparison | NvStorage constructor attaches OnMessage; OnMessage accepts only NVOpResult. Connection-count JSON handling/global backup basename absent; other backup hooks emit MISSING logs | NvStorage.cs:258, :265, :728, :1036, :1313 | M15-033; current stack read, not source authority for original |

## Evidence and boundaries

Answers `requests/20261013-test-loggers-and-backup.md` against `resources/lib/armeabi-v7a/libcozmoEngine.so` 3.4.0-1204, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. Code addresses are ELF virtual addresses, with Thumb bit removed. LIEF maps file offsets and resolves symbols/relocations; Capstone uses `skipdata=True`. Decompilation is navigation only. Data, jump tables and literal pools displayed by a linear dump are not instructions supporting claims.

The adopted `20261013-unported-message-handlers-check.md` and `20261013-robot-message-subscribers-check.md` supply the double-container construction and registration context. Their corrected ordering takes precedence over the earlier unreviewed Codex subscriber report. This request does not repeat the whole loader/connect-gating audit, nor extract the entire M15 backup/restore/write subsystem. Current M7-028 and M15-033 classifications remain RECOVERABLE_GAP; “native read” describes evidence recovered here, not a manifest promotion.

## Item 1 details

The **full source-established directory paths** are:

```text
Application.temporaryCachePath + "/dockingTest"
Application.temporaryCachePath + "/liftLoadTest"
```

Equivalently, use the exact `DataPlatformCachePath` string supplied to the engine. App input is in `unity/scripts/csharp/RobotEngineManager.cs:531`–535; native initializer reads the FilesPath and CachePath keys at 0x006658C8/0x006658F0 (strings 0x00666794/0x006667AC) and passes them as the first/second DataPlatform constructor arguments at 0x006659CA..0x006659D4. The constructor stores its second argument at +0x0C. Scope 2 uses that member directly. There is **no extra `/cozmo`, `/output` or `/gameLogs` component** on these paths. A configured trailing slash is not normalized: pathToResource appends another slash because the resource begins with a letter.

An absolute Android path such as a guessed internal/external cache directory cannot be recovered from this shipped engine/app input expression alone. The actual `Application.temporaryCachePath` is platform/runtime data, **UNKNOWN for the operator's phone here**. The source-faithful engine input boundary is the configured cache-root string. No hardware or filesystem probe was performed.

Default extension is **`.log`**, including the dot. It is retained in the logger, but **not appended to the constructor's directory path**. The size argument is **0x01400000 = 20,971,520 bytes = 20 MiB**, retained as a rolling limit; construction does not allocate/create a log file of that size.

The FileUtils call is exactly `CreateDirectory(path, false, true)`. The false argument prevents stripping the last component as a filename. The true argument walks prefixes, starting with the first slash search after position 0; it tests each prefix then creates missing prefixes with `mkdir(..., 0x1C0)`. Existing prefixes are not recreated, truncated, removed or chmodded. DirectoryExists uses stat and extracts the directory bit from mode byte at offset 0x11 (0x00802838..0x00802848); no errno-specific distinction is made on failed stat.

On a mkdir failure, already-created parent directories are not rolled back. The helper returns false silently; the RollingFileLogger constructor ignores it at 0x0080DFD0 by replacing r0 with `this`. A race where stat reports absent and mkdir returns EEXIST takes the same false path; there is no special success treatment for that errno. The 200-iteration guard can also produce false. These are filesystem decisions, not constructor logs or global error-flag changes.

The embedded basic_filebuf constructor initializes its FILE* to null (cleared region starting +0x34 includes +0x40, 0x00537E76..0x00537E82), initializes locale/buffers, and calls setbuf; there is no fopen. RollingFileLogger constructs empty current-filename state and never calls GetNextFileName, RollLogFile or Write in this constructor. Thus normal connection-time double construction may create **two directories total**, with **four CreateDirectory attempts**, but creates no `.log` files and writes no log contents.

The destructor stops/releases an **owned queue at logger+8**, not the supplied queue pointer at +4 (0x0080E0A4..0x0080E0A6 → 0x00801E70). These constructors leave owned queue null, so that path is a no-op for a never-used logger. It then closes filebuf at +0x3C, sets stream failbit 4 if close returned null (0x0080E0B4..0x0080E0C8), destroys filebuf/ios state, frees current filename/path/extension strings, and revisits queue cleanup. A logger used later may flush/close its already-open file; a never-used logger has no FILE* and close returns null without file IO at 0x00501120..0x0050113E. Neither destruction path removes the directory or rolls/creates a new file. The deleting destructor wrapper at 0x0080E15C additionally releases object memory; embedded behavior cleanup uses its normal destructor.

## Item 2 details

The requested post-logger range first releases temporary strings, then initializes state: receipt/load bytes +0x224/+0x225 to zero, total/load counters +0x21C/+0x220 to zero, state byte +0x218 to zero, and **runnable flag +0x219 to one**. The last pair is one little-endian halfword store of 0x0100 at 0x005D3FCC, not both bytes set to one.

Before raw DA registration it makes **two additional, distinct subscriptions**, each using a singleton ordered set:

- **E→G 0x0035**, RobotOffTreadsStateChanged. Constant at 0x00C6AAE4; call at 0x005D3FE4. This overload subscribes to the app-facing engine event interface, not the raw robot signal.
- **G→E 0x0095**, SetLiftLoadTestAsRunnable. Constant at 0x00C6AAE6; call at 0x005D400C. This takes app input and does not send that input itself.

The original serializers corroborate the names at `unity/scripts/csharp/Anki.Cozmo.ExternalInterface/MessageEngineToGame.cs:67` and `MessageGameToEngine.cs:162`. Both subscriptions are retained by the base IBehavior overloads; destruction of each temporary set does not unsubscribe the installed callback. The raw DA set follows, then the constructor returns. No active-state gate is used for registering these sets.

These are observable future routing/state choices, not immediate constructor-generated messages. Later G→E AlwaysHandle at 0x005D4F30 handles 0x95 by setting +0x219=1 at 0x005D4F3A; that handler is separate from the raw DA virtual slots. Later active E→G handler at 0x005D4E64 checks tag 0x35 and can enter state 1 when its off-treads payload is nonzero. Exact event-handler bodies were extracted in the earlier handler request and checked by the adopted report. The rest of this derived constructor contains no log emission, file write or robot/app send.

The **base IBehavior construction at 0x005D3F18** is outside the requested “rest of it” range. Exhaustive base configuration/input side effects are **UNKNOWN in this pass**; the absence of sends/logs asserted here applies to the read derived constructor, not a blanket claim about all IBehavior construction.

## Item 3 details

The bool passed at FactoryTest's call **0x005D051A is true**, from `movs r1,#1` at 0x005D0518. FactoryTestLogger stores it as one byte at logger+0xE8, after constructing its two empty strings, closed file stream and a **null** Json::Value (+0xD0, Json::ValueType=0).

Its meaning is **JSON output mode**. In later StartLog, +0xE8 selects `.json` versus `.txt` at 0x00671C90..0x00671CA4. In later CloseLog, when an actual file is open, a nonzero +0xE8 calls Json::operator<< on accumulated JSON at +0xD0 (0x00671806..0x00671812) before closing. Those later operations are not performed by the constructor.

The complete constructor **0x00671658..0x006716E2** makes no FileUtils call, does not resolve a path, call StartLog, open or write a file, create a directory, or emit a log. Its ofstream's underlying FILE* is null, as established by the same shipped filebuf constructor used above. Two FactoryTest instances therefore create two in-memory logger objects with JSON mode enabled; they create **no factory-test file at Robot construction**.

## Item 4 details — exact synchronous callback

The backup manager's directory member is **scope 0**, not the loggers' scope 2. Native constructor writes scope=0 and resolves `robotBackups/` into member +0x48 at 0x00519AC8..0x00519B14. Scope 0 uses DataPlatform's first string and appends `/output` (0x0084BE44..0x0084BE62). The app supplies this first string as `DataPlatformFilesPath = Application.persistentDataPath`. Therefore the stats path on the ordinary platform-backed branch is:

```text
DataPlatformFilesPath + "/output/robotBackups/statsForBackup.json"
```

The manager initially builds that absolute name by plain concatenation of +0x48 with global `statsForBackup.json`. The callback's key is the **first uint32 word of robotAvailable payload**, converted by unsigned `to_string`; no cached Robot serial/ID is consulted for this conversion. Treat it as the received identifier word. It is distinct from the ID used to choose the per-robot subscription signal.

If FileExists returns true, read that entire file as JSON. On success, fetch `root["ConnectionCount"][decimalIdentifier]`, convert the old value with Json::Value::asUInt, increment with native 32-bit ADD, and replace that member with a Json unsigned value. A missing/null count converts through the JSON API rather than a separate caller branch. On the FileExists-false branch, create local null JSON and assign **signed integer 1** to that same nested member. Other existing root/count-map members survive the successful read branch because the whole root is modified and serialized.

Write the whole root via **writeAsJson(scope=0, relative name `robotBackups/statsForBackup.json`)** at **0x0051A132**. This repeats scope path resolution, synchronously logs and ensures parent directories, opens with mode 0x10 (the shipped filebuf jump-table case retains literal `w` at 0x00538A50 and reaches fopen at 0x00538AC4), writes styled JSON using **tab indentation** (0x0084C3AC..0x0084C3B8), closes and returns. It overwrites the stats file, not an append-only entry. It does not use a temporary-file rename, a worker queue, a timer or a robot NV request.

Finally replace **global** string **0x01059D80** with `decimalIdentifier + ".backup"`. This is a shared basename, not a manager-local field; the later callback to reach it replaces the previous value. There is no fopen/write for this `.backup` file in RobotConnected. A stats read failure jumps out before this assignment, preserving the previous global filename; a stats write failure logs but still performs the assignment.

### All logging/error branches reachable through this callback

All structured-data vectors at these logging sites are empty. Error APIs have no channel argument; the channeled Info/Debug APIs explicitly use **Unnamed**.

1. **Reader failure, helper:** DataPlatform::readAsJson logs **Error** at **0x0084C0E6**, key **`DataPlatform.readAsJson`** at **0x0084C2CC**, format **`Failed to read [%s]`** at **0x0084C2E4**, argument full stats filename. Sets `_errG=1` at **0x0084C11C**, conditionally calls sDebugBreakOnError at **0x0084C120**. If formatted parser errors are nonempty, **Debug** at **0x0084C15C**, same key and channel **Unnamed**, format **`Json reader errors [%s]`** at **0x0084C304**, argument the reader's formatted error string. Reader closes/cleans its local stream and returns the parse boolean (0x0084C1FC..0x0084C202); there is no retry.
2. **Reader failure, caller:** **Error** at **0x0051A21C**, key **`RobotDataBackupManager.RobotConnected.ReadStatsFailed`** at **0x0051A308**, format **`Failed to read %s`** at **0x00BE7743**, argument **`statsForBackup.json` alone**, not the full path. Set `_errG=1` at **0x0051A252**, optional break at **0x0051A258**, then cleanup/return. This produces both helper and caller error calls on parse-false input; no file write/count commit/backup-basename replacement follows.
3. **Every attempted stats write, helper:** **Info** at **0x0084C36E**, channel **Unnamed**, key **`DataPlatform.writeAsJson`** at **0x0084C588**, format **`writing to %s`** at **0x0084C5A4**, argument resolved full stats filename. This occurs before the parent-directory attempt and fopen.
4. **Parent creation failure, helper:** **Error** at **0x0084C472**, key **`DataPlatform.writeAsJson`**, format **`Failed to create folder %s`** at **0x0084C5B4**, argument the **full filename passed to CreateDirectory**, despite the format saying folder. Set `_errG=1` at **0x0084C4A6**, optional break at **0x0084C4AC**, return false. A file-open failure instead returns false at **0x0084C4B4** without this directory-error log.
5. **Write false, caller:** **Error** at **0x0051A164**, key **`RobotDataBackupManager.RobotConnected.WriteStatsFailed`** at **0x00BE7755**, format **`Failed to write backup stats file`** at **0x0051A35C**, **no variadic arguments**. Set `_errG=1` at **0x0051A19A**, optional break at **0x0051A19E**; continue to basename replacement.

The handler's successful path has no direct log, but has the write helper's Info call. Neither failed FileExists nor CreateDirectory inside RollingFileLogger creates these backup-manager logs; they are distinct paths.

writeAsJson returns true once the file opened and its write/close sequence completed normally. It sets stream failbit when close returns null, but still returns **1** at **0x0084C452**. There is no caller-visible boolean test for serialized byte count, stream write failure or successful durable storage. Filesystem/libc results and bytes are not measured by this research pass.

Validly parsed but incompatible JSON structure/count types can throw through operator[]/asUInt; the caller does not convert those exceptions into ReadStatsFailed or WriteStatsFailed. Its landing pads destroy temporaries and resume unwinding at **0x0051A2F0**. Exact general Json::Value conversion/coercion rules and all possible malformed-count exception texts are **UNKNOWN in this bounded pass**; the established caller operation is asUInt followed by uint32 increment. The existing M1-034 handler-isolation departure governs surviving a handler exception in the stack; it is not evidence of native recovery here.

The callback does **not** mutate NVStorage's request queue, start a backup NV read, send an NV write/wipe, restore saved robot data, send a robot message, broadcast to the app or schedule a delay. Its IO is local filesystem read/write. It runs on **each delivered C9**, without a once-only flag, synchronously before the Impl HeadID and initial-connection handlers according to the adopted C9 order. Consequently the statistics write and filename replacement are completed (or the error path taken) before those later callbacks execute. Time spent in filesystem IO delays that same emission; no numeric timing interval is specified.

### Comparison with current stack NV storage

`cozmo-stack/src/Cozmo.Robot/NvStorage.cs` does **not** perform this connection callback:

- Constructor **lines 258–265** initializes component state and subscribes to `robot.Message`, with no backup-manager construction or stats-file handling.
- **Line 1036** filters that subscription to `NVOpResult` only; C9/RobotAvailable does not enter its result path.
- **Line 728** records `MISSING: RobotDataBackupManager::QueueDataToWrite ... the backup manager is not built` for other NV write paths.
- **Lines 1313–1314** record corresponding missing WipeAll/WriteDataForTag calls after write-family completion. **Lines 1025–1028** explicitly defer backup-manager destruction to M15.

The existing generic NV queue/read/write implementation is a different production path. It neither maintains the connection-count JSON nor sets this shared backup basename. No code or manifest change was made to close that difference.

## Manifest disposition, remaining uncertainty and artifacts

Current **M7-028** is **RECOVERABLE_GAP**, titled “Dev test behaviour constructor side effects: RollingFileLogger directories (dockingTest, liftLoadTest) and FactoryTestLogger”. Its evidence names the two RollingFileLogger constructors, CreateDirectory and FactoryTestLogger call. This report confirms directory-attempt effects, resolves the `.log` extension and cache-root input, distinguishes closed streams from created files, and supplies the remaining LiftLoad subscriptions/state and FactoryTest JSON-mode argument. No current claim is contradicted.

Current **M15-033** is **RECOVERABLE_GAP**, titled “RobotDataBackupManager: NV write backup, wipe, destruction and the backup reads' sink”. Its evidence lists QueueDataToWrite **0x0051AE04**, WriteDataForTag **0x0051B060**, WipeAll **0x0051B530**, destructor **0x0051A5F0**, and read sink **0x0051DF34**; its unresolved text also names this C9 callback **0x0051A010**. The connection callback is now traced to its local statistics/file effects. The other named backup bodies/sink remain outside this request and unread here; this report does not settle the whole record.

Remaining boundaries: actual host-supplied absolute cache/files roots; loader/connect gating already left open by the adopted check; exhaustive IBehavior base-constructor effects; general malformed-count JSON conversion rules; later rolling-file writes/rotation beyond constructor/destructor and FactoryTestLogger's later StartLog/CloseLog beyond the mode evidence used here. None is filled with a guessed implementation or used to promote a fidelity status.

Supporting research files: `20261013-test-loggers-and-backup-disasm.py`, `-native.txt`, `-bodies.txt`, `-callees.txt`, `-platform-inputs.txt`, `-filebuf.txt`, `-modes.txt`, `-path-check.txt`, `-final-focus.txt`, `-buffer-check.txt`. All native reads are local, static disassembly; no shipped code was executed or uploaded.

Research only: no branches, worktrees, commits, pushes, implementation edits, inventory/manifest/PROJECT_STATE changes, builds/tests, hardware runs or network contacts. Regression test and commit: not applicable. Manager verification of these citations is still required before inventory use.
