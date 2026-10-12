# M5 teardown members and subscription recipients (Opus extractor, 2026-10-12)

- **Run:** read-only cozmo-extractor. It followed up the open items in `research/20261012-b-m5-13-check.md`.
- **Adopted:** by the manager on 2026-10-12, for records and job B-M5-15.
- **Method:** the interface slots were identified from the UiMessageHandler vtable relocations (0x0102FE14):
  - +0x28 is Subscribe(EngineToGame);
  - +0x2C is Subscribe(GameToEngine).

  Tag names come from `MessageGameToEngine.cs:103-260` and `MessageEngineToGame.cs:104`.

## AnimationTransfer (engine +0x64)

**Construction.** It is built after DeviceDataManager (0x004EB2CE). It subscribes to G2E 0xF7 TransferFile
(0x0057F322..0x0057F350), then calls **CleanUp(1)** (0x0057F376..0x0057F37A).

**Destruction.** The destructor calls CleanUp(1) again (0x0057FC20..0x0057FC22). This is the first engine member
destroyed after VizManager::Disconnect (0x004EC6C0..0x004EC6CC), so it runs before ~CozmoContext and RemoveRobots.

**CleanUp** (0x0057FA00..0x0057FB88) has no log. It has two steps:
1. **The file.** It builds `pathToResource(scope 2, kCacheAnimFileName)`, where kCacheAnimFileName is
   **"TestAnim.json"** (static initialiser 0x004D7224..0x004D7252, 13 bytes from 0xC49CA0).
   - Scope 2 is DataPlatform+0xC + "/" + the name (0x0084BE82, 0x0084BEF6..0x0084BF16). The "/" is added only if the
     name doesn't start with one.
   - FileExists is stat + S_ISREG (0x0080343E..0x0080344A).
   - If the file is there, DeleteFile → `remove()` (0x00802C64..0x00802C72).
   - This step runs whatever the flag says.
2. **The directory.** It runs only if the flag is 1 **and** the +0x14 string is non-empty.
   - The path is the face-animation directory under `pathToResource(2, "assets/faceAnimations")` + the +0x14 name.
   - If DirectoryExists, RemoveDirectory.
   - Then +0x14 is cleared and +0x20 = 0.
   - Only a TransferFile message sets +0x14, so in scope this step never runs.

**The cache path.** DataPlatform+0xC comes from the config key "DataPlatformCachePath"
(cozmo_startup 0x006658F0 → 0x006659CC). The app sets it to `Application.temporaryCachePath`
(RobotEngineManager.cs:534). This is an app input.

**TransferFile has no sender in scope.**
- No app C# sends it.
- In the engine, its only creators are generated union internals (0x0075185E, 0x0075191E).
- TransferCodeLabFile (0x00663216) has no BL/BLX caller. `b.w` and indirect callers were not scanned, but CodeLab is
  unsupported anyway.

## ~CozmoEngine (0x004EC68C..0x004EC722)

| Order | Member | Effect |
| --- | --- | --- |
| 0 | BaseStationTimer singleton: the global is cleared (0x004EC6A0..0x004EC6B2), then removeInstance (0x0084BBE4..0x0084BC04) | None seen. The instance destructor is unread. |
| 1 | VizManager::Disconnect (0x004EC6BC) | Out of scope (debug visualiser). |
| 2 | +0x64 AnimationTransfer | The TestAnim.json delete; releases the 0xF7 subscription. |
| 3 | +0x50 DasToSdkHandler (G2E 0xE0 GetJsonDasLogsMessage, Init 0x00667EE4) | Unlink only. SDK. |
| 4 | +0x40 DebugConsoleManager (G2E 0xDC..0xDF; Init 0x0066864C, table 0xC86570) | Unlink only. |
| 5 | +0x3C DeviceDataManager (G2E 0xDB RequestDeviceData; 0x0066FEC0..0x0066FEEE) | Unlink and memory. **Not PerfMetric.** |
| 6 | +0x38: virtual delete of a slot the constructor sets to 0 (0x004EB292) | Null on the normal path. No writer found. |
| 7 | +0x34 CozmoContext | Table below. |
| 8 | +0x30 UiMessageHandler (0x0066011C..0x00660192) | M5-055 / M5-056. |
| 9 | +0x18 Json config | None. |
| 10 | +4: the engine's 14 G2E subscriptions (0x004EB5CC..0x004EB61A) and an experiments signal handle (0x004EB622..0x004EB670), released at 0x004EC71C | **After** the hub and the context are gone, so each release is the expired-token no-op (M5-056). Nothing to observe. |

## ~CozmoContext (0x004EAAAC..0x004EAC14)

| Order | Member | Effect |
| --- | --- | --- |
| 1 | RemoveRobots (0x004EAABE) | M5-055. |
| 2 | +0x40 thread-id holder | None. |
| 3 | +0x3C PerfMetric (0x00509434..0x00509478) | None (read to the return). |
| 4 | +0x38 CozmoExperiments (0x004EAAE6..0x004EAB66) | None. Its std::function is destroyed, never invoked. |
| 5 | +0x34 NeedsManager (0x00692444..0x00692572), including StarRewardsConfig and LocalNotifications | No log, event, send or file write. Specifically, no WriteToDevice. |
| 6 | +0x30 GameLogTransferTask (0x006A2E6C) | A virtual delete of +0x10, which is **unread**. |
| 7 | +0x2C DasTransferTask (0x006A26B8) | Subscriptions only. |
| 8 | +0x28 TransferQueueMgr (0x006A3860) | Dispatch::Stop (0x006A3874) stops and joins a queue thread. **Unread.** |
| 9 | +0x24 VizManager (UdpClient close 0x0084DD88) | Out of scope. |
| 10–11 | +0x20 RobotManager, +0x1C RobotDataLoader | M5-055. |
| 12–13 | Locale, RandomGenerator, a map\<string,bool\> | None. |
| 14 | +0xC AudioMultiplexer, present only with an external interface (0x004EA84C) | 0x008DEB08 → CozmoAudioController 0x005946F0 → AudioEngineController 0x008D1C1C. **Unread** past the first level. |

## Subscription recipients

| Tag | Direction / name | Callback | Sender |
| --- | --- | --- | --- |
| 0x5A | E2G RobotCompletedAction (slot +0x28, 0x004F0742) | HandleActionCompleted 0x004F0D78 (body unread) | **The engine** (ActionQueue 0x0053FAAA) |
| 0xB5 | G2E PushDrivingAnimations (0x004F07AE) | PushDrivingAnimations 0x004F0944 (body unread) | App (Robot.cs:1413-1415) |
| 0xB6 | G2E RemoveDrivingAnimations (0x004F081A) | RemoveDrivingAnimations 0x004F0A94 (body unread) | App (Robot.cs:1419-1421) |
| 0xAE / 0xAF | G2E Push/RemoveIdleAnimation (0x0057A5C6, 0x0057A634) | 0x0057B914 / 0x0057BA60 | App. In the engine, only the SDK (SdkStatus). M5-027 / M5-057. |
| 0xAD | G2E ReplayLastAnimation (0x0057A69E) | SetStreamingAnimation(last name +0x54, numLoops = msg[0], interrupt 1) | **No sender found** (SDK or debug) |
| 0xC4 | G2E ReplaceNotesInSong (0x0057A708) | HandleCustomSongNotesPayload 0x0057A890 | CodeLab only. M9-029. |
| 0xB4 | G2E SetLiveIdleAnimationParameters (0x0057A4BA) | HandleSetParameters 0x0057E860 (table below) | App (Robot.cs:663 RobotStartIdle, :1402-1404) |

- **The gates:**
  - 5A, B5 and B6 are registered only if HasExternalInterface() == 1 (0x004F072C..0x004F0732);
  - AE, AF, AD and C4 are skipped if the interface is null (0x0057A5BE);
  - B4 is skipped if the interface is null (0x0057A4AC).
- **HandleSetParameters** (0x0057E860..0x0057E90E):
  - **If names.size == values.size:** when setUnspecifiedToDefault (+0x18) is set, it calls SetDefaultParams
    (0x0057E882..0x0057E88C). Then it calls SetParam for each pair (0x0057E89A..0x0057E8B2).
  - **Otherwise:** sErrorF (0x0057E8D0; the format at 0xBEF4E1 is unread) and errG (0x0057E8F6..0x0057E90A).
- **The driving +8 vector is not a subscription.** It is a vector of {DrivingAnimations, lockName} pairs. Its destructor
  (0x004F090C..0x004F0942) frees the strings and has no callback, log or send.

## Manager decisions

- **The TestAnim.json delete is engine behaviour, so it is built.** The engine deletes that file at construction and at
  destruction (M5-059).
- **The cache path input** (M5-060, COMPATIBILITY_POLICY) is the stack's own cache directory setting.
- **Game messages the stack never sends** (M5-061, COMPATIBILITY_POLICY under the scope rule):
  - TransferFile 0xF7 (SDK/CodeLab);
  - GetJsonDasLogs 0xE0 (SDK);
  - ReplayLastAnimation 0xAD (no sender);
  - RequestDeviceData 0xDB;
  - debug-console 0xDC..0xDF.

  Their subscriptions exist and are torn down; their handlers are unreachable.
- **HandleSetParameters** (B4) is built from the rows above (M5-062).
- **Unread bodies get RECOVERABLE_GAP records:**
  - the DrivingAnimationHandler bodies 5A/B5/B6 (M5-063);
  - the other teardown bodies (M5-064);
  - the AudioMultiplexer chain, under M6.
- **Record corrections:**
  - M5-037/M5-053: the +8 vector has no recipient, and its MISSING closes as "no effect". 0x5A is an E2G subscription.
  - M5-044: the B4 recipient is now read.
  - M5-055: engine +0x3C is DeviceDataManager, and +0x40/+0x50 are DebugConsoleManager/DasToSdkHandler.
