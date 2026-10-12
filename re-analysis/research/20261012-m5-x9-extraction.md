# X-M5-9: M5 unowned steps and the two loaders (Opus extractor, 2026-10-12)

- **Basis:** read-only, at HEAD 5a4a2c2d. The scratch emulator ran the shipped `operator[]` (0x0052191B) under
  `re-analysis/tools/emu/emu_elf.py`, and an ARM EHABI LSDA parser was used.
- **Adopted:** by the manager on 2026-10-12, as checked rows for M5 jobs. Where a row says RECOVERABLE_GAP or
  HARDWARE_ONLY, that is its status.
- **"errG + break"** means `_errG` = 1 (GOT 0x0103E790), then `sDebugBreakOnError` only if `_errBreakOnError`
  (GOT 0x0103E78C) is set. That function is `bx lr` at 0x0080DAB4.

## 1. The animation-group file loader (new record M5-050)

| step | the original | citation |
|---|---|---|
| G1 | LoadNonConfigData runs on its own thread (DoNonConfigDataLoading, GOT 0x0103E9C8; CozmoEngine::Update 0x004ED5FA). The order is CollectAnimFiles, LoadAnimationsInternal (LoadTime 0x0051F9FA), **LoadAnimationGroups**, cube, backpack, face, ratio +0.2, …, the file-map clear (0x0051F47C), then ratio 1.0. | 0x0051F3AE..0x0051F3CE; 0x0051F3D2..0x0051F488 |
| G2 | Walks `assets/animationGroups/` (0x0051F858) with the mtime map at loader+0x84. The type-1 lambda appends each full path to map[1] at +0x44. | 0x0051F652..0x0051F67E; 0x00524342..0x0052438C |
| G2a | WalkAnimationDir: `pathToResource(1)`, suffix list {json, bin} (0x0101FF68), recursive full paths. If stat fails, it warns `RobotDataLoader.WalkAnimationDir` "could not get mtime for %s" and skips. A fresh map accepts every file. | 0x00520E66..0x00520F70 |
| G2b | FilesInDirectory: readdir order, unsorted. It takes DT_REG files with a matching suffix and recurses into DT_DIR (not "." or ".."), appending each subtree where it was met. readdir order is HARDWARE_ONLY. | 0x00802CF8..0x00802FF6; 0x00804516 |
| G3 | The job is bind(&LoadAnimationGroupFile, this, _1) (GOT 0x0103E9A8), pushed in map[1] order. map[1] is created even when empty. | 0x0051FC7C..0x0051FD22 |
| G3a | DispatchWorker<3>::Process: q = n>>2 and r = n&3. Up to 3 threads take contiguous runs of q+(r>0); the loader thread runs the rest, then joins them. For 507 files that is 127/127/127/126. Definition order across threads is HARDWARE_ONLY. | 0x00521AFC..0x00521C0C |
| G4 | The abort gate is loader+0x278 (ldrb/tst, which skips the file). It is set to 0 in the constructor (0x0051EF42) and to 1 with dmb in ~RobotDataLoader (0x0051F0D4..0x0051F0E6). | 0x00521D86..0x00521D8E |
| G5 | Builds `Json::Value(null)`, then readAsJson(path). A false result skips the file silently. | 0x00521D98..0x00521DA6 → 0x00521F7A |
| G6 | If `Value::empty()` (null, or an array or object of size 0) is true, the file is skipped silently. | 0x00521DAC..0x00521DB2; 0x008E9FA8..0x008E9FD6 |
| G7 | The name is the basename after the last '/' (or the whole path), then the stem before the last '.' (or the whole basename). | 0x00521DB6..0x00521ED0 |
| G8 | sChanneledInfoF: channel `Animations` (0x00BE5216), key `RobotDataLoader.LoadAnimationGroupFile.LoadingSpecificAnimGroupFromJson` (0x00BE7D1F), format "Loading '%s' from %s" (0x00BE7D0A), arguments (stem, full path), empty kv. It runs **before** the mutex. | 0x00521ED2..0x00521F0A |
| G9 | Locks loader+0x26C, calls AnimationGroupContainer::DefineFromJson(root, stem, canned +0x58) on +0x60, then unlocks. The result is ignored. The same mutex serves clips, cube and backpack. | 0x00521F30..0x00521F4C |
| G10 | AddAnimationGroup: if the name is found, it returns 0 unchanged; otherwise it constructs AnimationGroup(rng +0x28, name) and inserts it. | 0x0058B4A2..0x0058B678 |
| G10a | A duplicate stem redefines the existing group: the name is reassigned (0x0058A5FA) and the entries are cleared only on the array path (0x0058A614..0x0058A63E). The last writer wins, and which one that is is HARDWARE_ONLY. Shipped data has no duplicates. | as cited |
| G11 | A non-zero inner result logs sErrorF `AnimationGroupContainer.DefineAnimationGroupFromJson` (0x0058B9A4), "Failed to define animation group '%s' from Json." (0x0058B9DC), with the name, then errG + break. | 0x0058B8B2..0x0058B916 |
| G12 | A null GetAnimationGroup logs "Could not GetAnimationGroup named '%s'." (0x00BEFD60), errG + break, and returns 1. Natively dead, because G10 always finds or inserts. | 0x0058B918..0x0058B972 |
| G13 | A non-empty root that isn't an object makes `root["Animations"]` throw LogicError inside find (0x008EB058..0x008EB06E), with the mutex held. Nothing catches it, so terminate runs. Unreachable with shipped data. | 0x008E5F14, 0x008EAF7C |
| G14 | The group step has no progress accounting and no timing. | G1 |

## 2. readAsJson diagnostics (new record M5-051, shared by clips and groups)

| step | the original | citation |
|---|---|---|
| G5a | `fopen(path,"r")`, then the default Json::Reader `parse(istream, root, true)`. A missing file is not an exception: failbit makes parse return false. On false: sErrorF `DataPlatform.readAsJson` "Failed to read [%s]" with the path, then errG + break. Then, if `getFormattedErrorMessages` is non-empty, **sChanneledDebugF** channel `Unnamed`, key `DataPlatform.readAsJson`, "Json reader errors [%s]". Returns the parse result. | 0x0084C090..0x0084C15C; strings 0x0084C2CC, 0x0084C2E4, 0x0084C304, 0x00BE3FEC |
| G5b | The reader text is "* Line %d, Column %d\n  <message>\n" plus "See <loc> for detail.\n" (0x008E2E14..1C, 0x00C432DA, 0x00C432ED; messages 0x00C43043..0x00C43297). The per-error text is a RECOVERABLE_GAP: readValue 0x008E09A4 .. decodeUnicodeEscapeSequence 0x008E2814, addError 0x008E0E1E, addErrorAndRecover 0x008E207C, recoverFromError 0x008E1CC4, getLocationLineAndColumn 0x008E2A70. It is debug level, so sink (a) drops it (M3-053). | as cited |
| G5c | Nesting deeper than 1000 throws RuntimeError "Exceeded stackLimit in readValue()." Nothing catches it, so terminate runs. Unreachable with shipped data. | 0x008E09AA..0x008E09B0 → 0x008E0D7A..0x008E0D92 |

## 3. The face-animation loader (new record M5-052; frame consumption stays with M5-013)

| step | the original | citation |
|---|---|---|
| F1 | Runs on the loader thread after the backpack lights. Lazy singleton (0x0105AB00), then ReadFaceAnimationDir(dp, false). The SDK branch is unreachable. | 0x0051F3CE; 0x00520364..0x005203C4 |
| F2 | A null DataPlatform returns. | 0x0058005A..0x00580062 |
| F3 | `pathToResource(1, "assets/faceAnimations/")` (0x005807A0). | 0x00580066..0x00580090 |
| F4 | ListAllDirectories: a DirectoryExists gate, then readdir order excluding "." and "..", unsorted. The order is HARDWARE_ONLY. | 0x005800B0; 0x008040B4..0x008041CE |
| F5 | The job is bind(&LoadAnimationImageFrames, this, root, _1) (GOT 0x0103ED38), run on DispatchWorker<3>, the same as G3a. | 0x005800B4..0x00580126, 0x005805B8 |
| F6 | A directory named like ProceduralAnimName (0x0105AB04) logs sErrorF `FaceAnimationManager.ReadFaceAnimationDir.ReservedName` (0x00BEF59C), "'_PROCEDURAL_' is a reserved face animation name. Ignoring." (0x005807E4), with no arguments, then errG + break. The directory is erased from the list. | 0x005801AE..0x0058040E |
| F7 | If stat fails: sWarningF `FaceAnimationManager.ReadFaceAnimationDir` (0x00BEF5D3), "could not get mtime for %s" (0x005807C8), with the directory path. The directory is erased. | 0x005802B6..0x005804B2 |
| F8 | If the name is absent: create map[name], store the mtime and push the job. If present and stored mtime >= new mtime: skip. Otherwise update and push. | 0x005802FC..0x005805A8 |
| F9 | FilesInDirectory(dir, names only, no filter, non-recursive): every DT_REG file. | 0x0058090E; 0x00802C76; 0x008043EA |
| F10 | `std::sort` with `__less<string>`: byte order, case-sensitive. | 0x00580920 |
| F11 | us = find_last_of('_'), dot = find_last_of('.'). Three checks, in order, each logging sErrorF key `FaceAnimationManager.ReadFaceAnimationDir` with the filename, then errG + break, then **return, abandoning the rest of the animation**: no '.' ("Could not find '.' in frame filename %s", 0x00BEF5FD); no '_' (0x00581204); dot <= us+1 ("Unexpected relative positions for '.' and '_' in frame filename %s", 0x00BEF625). | 0x00580E34..0x00580F6E |
| F12 | `stoi(substr(us+1, dot-us-1), nullptr, 10)`. Only `std::invalid_argument` is caught: sErrorF "Could not get frame number from substring '%s' of filename '%s'." (0x00BEF668), errG + break, return. **out_of_range is not caught, so terminate runs.** libc++ stoi's own parsing rules are a RECOVERABLE_GAP. | 0x00580A0E..0x00580A1C; LSDA pad 0x00580DB0, filter GOT 0x0103ED40; 0x00580DB0..0x00580E32 |
| F13 | n < 0 logs sErrorF "Found negative frame number (%d) for filename '%s'." (0x005811C4), with (n, filename), then errG + break, then return. | 0x00580A22..0x00580A26 → 0x00580B06..0x00580B6A |
| F14 | frames = map[name]. If n >= 2, push empty pairs while size < n-1. The frame is then **always appended at the end**. | 0x00580A2C..0x00580AE4 |
| F15 | `cv::imread(path, IMREAD_GRAYSCALE)`: the Image vtable slot (0x005643F2) returns 1, so the flag is 0. The load succeeds iff the data is non-null and total > 0. PNG decoding inside libopencv_imgcodecs is a RECOVERABLE_GAP. | 0x00580BE6..0x00580C16; 0x0086E75E.. |
| F16 | The image is accepted iff Load == 0, rows == 64 and cols == 128. Otherwise sErrorF "Image in %s is %dx%d instead of %dx%d." (0x00581194), with (path, cols, rows, 128, 64), then errG + break, then **continue to the next file**. | 0x00580C1A..0x00580CF8 |
| F17 | Threshold(0x80), whose meaning is M5-013's. | 0x00580C2C |
| F18 | The frame pair: even rows cleared then RLE, odd rows cleared then RLE (M3-007), then push_back. | 0x00581254..0x005812C4 |
| F19 | No success log and no progress of its own; the +0.2 belongs to G1. | — |
| F20 | `removeInstance` (0x0058001C) has no caller, so the map lives as long as the process. Whether the app ever loads twice is UNKNOWN. | xref scan |

Shipped data: 2 directories and 552 PNGs, all 128×64 grayscale (510 at 8-bit, 42 at 1-bit), numbered from 0 with no
gaps.

## 4. Owners for the steps that had none

**(a) DrivingAnimationHandler teardown (new record M5-053).**
- **Robot destructor:** takes robot+0x24C and nulls it (0x00511524..0x0051152A). It then destroys:
  - the +0x4C vector of shared_ptr<ScopedHandleContainer>, released back to front (0x004EAEA2..0x004EAEBA);
  - the +8 vector<pair<DrivingAnimations,string>> (0x004F090C);
  - then the handler itself (0x00511542).

  The ActionList was destroyed before this (0x0051151C, M8).
- **Handler subscriptions:** only when `HasExternalInterface` (0x004F072C). They are E2G RobotCompletedAction 0x5A
  (slot +0x28, 0x006626CC), then G2E PushDrivingAnimations 0xB5 and RemoveDrivingAnimations 0xB6 (slot +0x2C,
  0x00662750). Each container is a new, sole owner. So teardown releases 0xB6, then 0xB5, then 0x5A, and each release
  is the last one.
- **Streamer SetupHandlers (0x0057A5A8):** subscribes 0xAE, 0xAF, 0xAD and 0xC4, in that order, into +0x1B4.
  ~AnimationStreamer releases them in the order 0xC4, 0xAD, 0xAF, 0xAE (0x0057AF6C). There is no `__add_shared` in
  0x00579F78..0x0057F200.
- **Still open:** the E2G container factory 0x0066411C and its ProtoHandle are a RECOVERABLE_GAP.

**(b) M5-044 tag-B4 last release (0x0057AFD4).**
- It is the last owner. The libc++_shared `__release_shared` at 0x5ED6C decrements +4. If the old value was 0, it calls
  vtable+8 (on_zero_shared, which destroys the container and unlinks it) and decrements weak; if the old weak count was
  0, it calls vtable+0x10. This supersedes TDU's "external, unsettled".
- **Still open:** the unlink happens only if the signal's weak lock succeeds. The ~CozmoEngine member order is a
  RECOVERABLE_GAP.

**(c) M5-008 VERIFY tail (`sVerifyFailedReturnFalse` 0x0080D9A4).**
- It logs `sErrorV(name, {}, "VERIFY(%s): ", …)`, sets `_errG` = 1, with **no** debug break.
- `sDumpCallstack` (0x0080DA08) is a **no-op**: `bx lr` at 0x0080B0A6.
- The flush (0x0080DA24) runs gLoggerProvider vtable+0x14 → MultiLoggerProvider::Flush 0x006673BA →
  DasLoggerProvider::Flush 0x006671F4 → libDAS DASForceFlushNow (libDAS 0x7168). The libDAS body belongs to M3-051.
- It returns 0.

**(d) M5-001.**
- **The main-thread checks are natively unreachable.** The keys are `RobotDataLoadingShouldNotBeOnMainThread` at
  0x0051F3AA and `RobotDataLoader.AnimFileOnMainThread` at 0x00521338, both with the tail in (c).
  - LoadNonConfigData runs only on its own thread.
  - `LoadAnimations` (0x005212E8) is reached only from `RobotManager::ReadAnimationDir` (0x0052F858), which has
    **no caller**.
- **After `getMemberNames` throws** (0x008E6D08):
  - there is no `__cxa_begin_catch` in 0x0051ED30..0x00525800, 0x00586000..0x0058C900 or JsonCpp;
  - so `std::terminate` runs with the default handler (set_terminate exists only in cozmo_shutdown, M3-049);
  - the terminate text is UNKNOWN, as M3-049 records.

**(e) M5-046, Results order for every population.**
- On a fresh map, the order is the **reverse of first insertion**: walk order 0,2,3,1, restricted to the populated
  types. Emulated over all 15 populations (e.g. [0,2] gives [2,0]; [2,3,1] gives [1,3,2]).
- A non-fresh map is **unreachable in production**:
  - LoadNonConfigData runs once, gated at 0x00523580..0x005235CC;
  - the map is cleared at 0x0051F47C;
  - the only second collect is through ReadAnimationDir, which has no caller.

**(f) M5-011's mood: `MoodManager::GetSimpleMood` 0x0067C084 (owner M7).**
- The emotions sit at +0, stride 0x20, with the value at +0x18. Happy is at +0x18; Confident is at +0x78.
- The result starts as Default(2), and later tests override earlier ones:
  - Happy > 0.33f gives Happy(0);
  - Confident < −0.29f gives Sad(1);
  - Happy < −0.33f gives Sad(1);
  - NaN matches no test.

  Constants are at 0x0067C0C0..0x0067C0C8.
- **The group draw's time** is **MoodManager+0x130** (0x0058A95C), written only by MoodManager::Update(t) (0x0067B67E)
  and by Reset and the constructor. It is not BaseStationTimer.

**(g) M5-038 needs M8 records** (all RECOVERABLE_GAP):
- TriggerAnimationAction: ctor 0x00544228, SetAnimGroupFromTrigger 0x0054432C (warning 0x005443AC), Init 0x0054443C
  (sEvent 0x005445BC);
- PlayAnimationAction: ctor 0x00543C08; Init 0x00543F20 (remaps cozmo_sings_custom; SetStreamingAnimation with loops
  +0x84 and interrupt +0x8C; tag 0 sets failed +0x8A; subscribes R2E 0xCA/0xCB/0xD5); CheckIfDone 0x005441C0;
  GetCompletionUnion 0x005441DE; destructor 0x00543DAC (info 0x00543DF2, SetStreamingAnimation 0x00543E26);
- the IAction/IActionRunner base.

## 5. M5-020's app input (manager ruling: out of scope)

- Unity re-seeds with `UnityEngine.Random.InitState(Environment.TickCount)` (StartupManager.cs:337-347).
- UiParticles.cs:205 and DebugDisplayPane.cs:110 re-seed again, and about 39 calls advance the stream.
- The only consumer is CodeLabGame.cs:3094-3107. CodeLab runs in **internal SDK mode** (CodeLabGame.cs:967), which
  AGENTS.md excludes.

## Contradicted or weak evidence

- **M5-044:** `__release_shared` ships in libc++_shared.so and is read in (b).
- **M5-001:** the note "JSON open/read/parse exceptions take readAsJson failure and continue" is wrong. A failed open
  is parse() == false; reader exceptions terminate (G5c).
- **M5-011:** "production clock remains BaseStationTimer seconds" is wrong. It is MoodManager+0x130 (f).
- **M5-020:** the time(NULL) seed is superseded, and the path is CodeLab-only (§5).
- **M5-037:** the "descendant effects UNKNOWN" are read in (a) and (b).
- **The test `M5LastReadsTests.B_M5_4_GroupDefinitionPrecedesBackpackLoadTime`** asserts NoAnimations for `{}`. G6 skips
  it silently.

## Open (for the manager or operator)

1. **Terminate paths, for the operator:** G5c, G13, the F12 out_of_range, and the getMemberNames LogicError. They are
   unreachable with shipped data. Should they come under M1-034, as M3-049 does?
2. The ~CozmoEngine member order, the 0x0066411C factory, the JsonCpp text, libpng/imread, libc++ stoi, libDAS 0xB754
   and readdir order: RECOVERABLE_GAP or HARDWARE_ONLY, as marked above.
