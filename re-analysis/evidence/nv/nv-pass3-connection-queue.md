All four questions are settled from libcozmoEngine.so, apart from the small leftovers listed at the end. I could not write the report to `...\scratchpad\extract\NV-gap2\report.md`: the harness blocks report files from subagents, so the full report is below. The scratch disassembly and scripts are in `...\scratchpad\extract\NV-gap2\`:
- `hdcp.txt` (HandleDefaultCameraParams) and `scep.txt` (SetCameraExposureParams);
- `uvm.txt`, `uf.txt`, `umisc.txt`, `ulp.txt`, `pw.txt`, `fw.txt`, `bw.txt` (the UpdateAllResults sub-steps);
- `vcinit.txt` (VisionComponent::Init);
- `rdbm_ctor.txt`, `rdbm_rc.txt` (RobotDataBackupManager);
- `nvx.txt`, `nvx2.txt` (NV caller xrefs);
- `sym.py`, `vcstore.py` (helper scripts).

I did not write anything in the repository.

**Key findings:**
- **Q1:** HandleDefaultCameraParams sets exposure, gain and gamma only. It never touches a calibration, so it cannot open Gate B.
- **Q2:** The CameraCalib read is at least the 13th entry in the NV FIFO. The Robot constructor queues 12 reads ahead of it (no writes, no erases). Every other NV caller runs after it.
- **Q3:** The array reader has no cap and no bounds pre-check. An over-counted NVOpResult is kept, with the blob cut short.
- **Q4:** Only UpdateVisionMarkers can make UpdateAllResults report failure, and only when BlockWorld's object creation or update fails.

# NV gap pass 2

## Q1. HandleDefaultCameraParams (robot tag 0xC8)

| step | what the original does | citation | class |
|---|---|---|---|
| 1a Robot handler | Takes Get_defaultCameraParams() and tail-calls VisionComponent::HandleDefaultCameraParams(robot+0x258, msg). It has no time-sync gate and no null check. The veneer resolves to 0x8CB3CC → PLT 0x4AA36C, whose body is **0x657CA0**. | 0x537114..0x53712A | EXACT_SOURCE |
| 1b Fields | DefaultCameraParams is 29 B: f32@0 maxGain, f32@4 gain, u16@8 minExposure, u16@0xA maxExposure, u8[17]@0xC gamma. The Unpack ignores read failures. | Unpack 0x7BF1D6..0x7BF236; Size 0x7BF346 `movs r0,#0x1d` | EXACT_SOURCE |
| 1c Gate | Requires VisionSystem::IsInitialized (`ldrb [r0,#0x58]` 0x6B2CD6). If false: error "…HandleDefaultCameraParams.NotInitialized" and nothing else. | 0x657CA8..0x657CB0 → 0x657D62 | EXACT_SOURCE |
| 1d Range | init = u16 at .data 0x1051054 (InitialExposureTime_ms). If init < msg@8 or init > msg@0xA (unsigned): "BadInitialExposureTime" and **nothing else**. | 0x657CB2..0x657CC2 → 0x657DA2 | EXACT_SOURCE |
| 1e Send | Calls SetCameraSettings(init, msg f@4). If IsExposureValid or IsGainValid fails, it returns silently. If both pass, it: **sends SetCameraParams{f32 g, u16 e, 0}** (reliable, not hot); calls VizManager::SendCameraInfo; calls SetNextCameraParams; broadcasts **CurrentCameraParams{g, e, VC+0x329}** to game. | 0x657CCA; 0x6560E6..0x6560FE; 0x65614A..0x656166; 0x656176..0x6561A4 | EXACT_SOURCE |
| 1f Limits and gamma | Under the mutex at VC+0x4C, calls SetCameraExposureParams(init, msg@8, msg@0xA, gain = f@4, 0.1f, maxGain = f@0, &msg[0xC]). Inside it: SetGammaTable, whose failure only warns; min is replaced by 1 when ≤ 0; it stores VS+0x88 max, +0x8C min, +0x90 0.1, +0x94 maxGain; it calls SetNextCameraParams(init, gain) a second time. That second call logs "Params already requested … Replacing" whenever 1e queued params, and stores +0xA0/+0xA4/+0xA8. The only return is `movs r0,#0`, so the "SetFailed" branch is unreachable. | 0x657CCE..0x657D12; 0x6B937C..0x6B938A; 0x6B93C6..0x6B9404; `strd r8,r0,[r7,#0x88]` 0x6B9416..0x6B9426; 0x6B9496; 0x6B22AA..0x6B230C | EXACT_SOURCE |
| 1h **Calibration** | **Nothing touches the calibration.** VC+0x28 (Camera+4) is written only by Camera::SetCalibration (`str r6,[r4,#4]` 0x85DEFA), which is reached only through VisionComponent::SetCameraCalibration (0x65171C). The only callers of SetCameraCalibration are the NV CameraCalib callback (0x65ADE0) and BehaviorFactoryTest::HandleCameraCalibration (0x5D2C2A). The other Camera::SetCalibration callers set VisionSystem's own camera (0x6B1E5E, 0x6B7876). A scan of every VisionComponent:: method for stores at +0x28 (or strd at +0x24) found only stack stores. **DefaultCameraParams cannot open Gate B.** | as cited | EXACT_SOURCE |
| 1i Timing | The engine never requests DefaultCameraParams. When it arrives is firmware behaviour. | M3 1o | HARDWARE_ONLY |

## Q2. NV operations queued ahead of the CameraCalib read (0x6583FA)

**Complete direct-caller set.** The NV entry points are non-virtual and called through PLT:
- **Read:**
  - 0x51AD0A: RobotDataBackupManager (RDBM)::ReadAllBackupDataFromRobot
  - 0x63CA52: Inventory
  - 0x6467A4: game message NVStorageReadEntry
  - 0x64BEFA: ProgressionUnlock
  - 0x6512F0 and 0x651330: LoadFaceAlbumFromRobot
  - **0x6583FA: VisionComponent RobotConnectionResponse**
  - 0x6944CC: NeedsManager
  - 0x6A5B1E: CozmoExperiments
- **Write:**
  - 0x51B8D8: RestoreRobotFromBackup (game message)
  - 0x5285D6: CameraCalibration (game message)
  - 0x5D1B14: FactoryTest
  - 0x63D456: Inventory
  - 0x6463A0 / 0x64663C: NVStorageWriteEntry (game message)
  - 0x64C58A: Unlocks
  - 0x6573A6 and 0x6573E6: SaveFaceAlbumToRobot
  - 0x6956D4: Needs
  - 0x6A58D4: Lab
- **Erase:**
  - 0x51C15C: WipeRobotGameData (game message)
  - 0x6468BC: NVStorageEraseEntry (game message)
  - 0x657474 and 0x657600: SaveFaceAlbumToRobot
  - 0x698DBC: WipeRobotNeedsData (game message)
- **WipeAll:** 0x6469D2 (game message)
- **WipeFactory:** 0x5D25EC (FactoryTest EndTest)

| step | what the original does | citation | class |
|---|---|---|---|
| 2a | The Robot is constructed at ConnectToRobot (AddRobotConnection, then AddRobot), before any robot message. NV dispatch waits for Gate A (SyncTimeAck plus RobotState), so everything the constructor queues sits in the FIFO at +0xF8. Read queues any valid tag unconditionally. | 0x4ED074/0x4ED07C; 0x513C5C; 0x644E2A..0x644E86 | EXACT_SOURCE |
| 2c **#1 Read 0x182000** (GameUnlocks) | Robot ctor → ProgressionUnlockComponent::Init. Both the config-error path and the normal path reach ReadCurrentUnlocksFromRobot, which calls Read(0x182000, cb, null, false) unconditionally. | 0x51036C; 0x64B924/0x64BA4C → 0x64BA4E; 0x64BEF4..0x64BEFA | EXACT_SOURCE |
| 2d **#2 Read 0x195000** (Inventory) | Robot ctor → InventoryComponent::Init, whose first call is ReadCurrentInventoryFromRobot → Read(0x195000). | 0x51037C; 0x63C946; 0x63CA48..0x63CA52 | EXACT_SOURCE |
| 2e **#3 Read 0x184000**, **#4 Read 0x183000** | Robot ctor → VisionComponent::Init, only if context+8 (DataPlatform) != 0 (0x5103BC). Init reaches the FaceAlbum step only if VisionSystem::Init returns 0 (0x650E3E). FaceAlbum "" or "robot" → LoadFaceAlbumFromRobot. That function calls Read(0x184000, empty std::function, &VC+0x2F4); if that returned true, it calls Read(0x183000, lambda, &VC+0x300). The shipped vision_config.json:31 has `"FaceAlbum" : "robot"`. The non-"robot" branch would instead run EraseAllFaces → SaveFaceAlbumToRobot, which issues NV writes and erases. | 0x650FB4..0x651010; 0x65108A; 0x6512EA..0x651330; 0x651014 | EXACT_SOURCE for the path and the config. Whether VisionSystem::Init (0x6B0658) succeeds in production: RECOVERABLE_GAP |
| 2f | DataPlatform is non-null in production. cozmo_startup does `new DataPlatform` and stores the global `dataPlatform`. That value is passed through StartRun, CozmoInstanceRunner and CozmoEngine into CozmoContext, which stores it at +8. | 0x6659C4..0x6659E6; 0x6661E0..0x6661EA; 0x65B11E; 0x65C006; 0x4EB28C; 0x4EA742 | EXACT_SOURCE |
| 2g **#5..#12 backup reads** | RDBM is embedded at NV+0x80 (0x642860 → ctor 0x6428A4). Its ctor sets +0x58 = 0 (0x519B2C). It then reads `config/engine/backup_config.json` (static string 0x4D6E3C..0x4D6E54) and inserts the "tagsToBackup" values into std::set<u32> at +0x3C (0x519C9A..0x519D1A). The Robot ctor then calls ReadAllBackupDataFromRobot (0x5103D4). With +0x58 == 0, that function walks the set in ascending order and calls Read(tag, lambda, null, false) for each. The tags are **0x180000, 0x181000, 0x182000, 0x183000, 0x184000, 0x194000, 0x195000, 0x196000**. | 0x51AC76; 0x51ACB2..0x51AD0A; backup_config.json | EXACT_SOURCE |
| 2h | RDBM's 0xC9 RobotConnected handler does file I/O only; it makes no NV call. | 0x519B50; 0x51A010..0x51A2F0 | EXACT_SOURCE |
| 2i **#13 CameraCalib** | The mfgId lambda calls SendConnectionResponse (0x52E3A2), which calls IExternalInterface vtable+0x1C. In UiMessageHandler's vtable (0x102FE38) that slot is Broadcast(MessageEngineToGame&&) (0x6625B0). Broadcast calls DeliverToGame, then **emits synchronously** to engine subscribers (0x6625FA → 0x662590 → 0x663C74). So Read(0x80000001) at 0x6583FA runs inside this call. The engine subscribers are RobotEventHandler, TracePrinter and VisionComponent; only VisionComponent issues an NV operation. | 0x52DF38..0x52DF64 | EXACT_SOURCE |
| 2j After the CameraCalib read | Later in the same lambda: Lab Read(0x196000) (0x52E3AA → 0x6A5B1E); then Needs Read(0x194000) (0x52E3B2 → 0x6944CC). If that Read fails, InitAfterReadFromRobotAttempt runs, which may queue the Needs and Lab writes. | 0x52E3A6..0x52E3B2; 0x6943F8..0x694400 | EXACT_SOURCE |
| 2k After the CameraCalib read | Everything Update-driven or callback-driven (the InventoryComponent::Update write at 0x513E94, unlocks, the face-album save, the needs writes) needs Gate A. Gate A needs SyncTime, which RobotEventHandler sends in response to RobotConnectionResponse (M1 CB22/CD18). | as cited | EXACT_SOURCE |
| 2l Unity | Unity senders found: SkillSystem.cs:310-317 reads GameSkillLevels on RobotConnectionResponse; ConnectionFlowController.cs:527-535 reads SavedCubeIDs after the securing screen. Both fire only after Unity receives the response, and the engine queued #13 in the same call that emitted it. | as cited | EXACT_SOURCE for these sites. Unity messages sent before the mfgId arrives (e.g. SetGameBeingPaused, handler 0x698F44 not read): UNKNOWN |
| 2m Consequence | ProcessOnIdleCallbacks returns at once while the deque size (+0x10C) != 0 or the state (+8) != 0. So the CB22 one-shot (Robot+0x2A, ready to stream) fires only after all 13+ queued operations finish. | 0x645B10..0x645B1A | EXACT_SOURCE |
| 2n Timing | How long reads #1..#12 take (NOT_FOUND replies, multi-blob re-requests, 5 s timeouts) depends on the robot. | — | HARDWARE_ONLY |

## Q3. Array reader 0x73213C

| step | what the original does | citation | class |
|---|---|---|---|
| 3a Count | 0x71A8F6 does ReadBytes(u16, 2). If that fails, it returns 0 and leaves the vector untouched. Otherwise it calls 0x73213C with the count. | 0x71A900..0x71A91A | EXACT_SOURCE |
| 3b No cap, no pre-check | The reader clears the vector and calls reserve(raw u16, up to 65535). It never checks the count against the remaining buffer. | 0x732146..0x732156 | EXACT_SOURCE |
| 3c Loop | ReadBytes(1) per element. On the first failure it returns 0 immediately, keeping the bytes already pushed. After the loop it returns (size == count). | 0x732162..0x7321A4 | EXACT_SOURCE |
| 3d ReadBytes | All-or-nothing: on overrun it copies nothing, does not advance and returns 0. | 0x83C09A..0x83C0CA | EXACT_SOURCE |
| 3e Unpack | NVOpResult Unpack ignores every result and returns GetBytesRead. | 0x7CE76A..0x7CE7AC | EXACT_SOURCE |
| 3f Short buffer | ProcessMessages drops a message only when bytes read != length. So an over-counted blob is **kept**, with data = the bytes actually present. A cut inside the header is also kept, with n = 0 and stale or zero fields. An under-count leaves trailing bytes, and the message is dropped. | 0x69D902..0x69D910 | EXACT_SOURCE |

## Q4. UpdateAllResults: what sets sp+0x88

**Structure.**
- If VC+0x1C (VisionSystem) is null, it returns 0 (0x65430C → 0x654AC4).
- Otherwise it loops on CheckMailbox (0x654A6E..0x654A7A). Each result's sub-steps are gated by its mode mask (sp+0xB8/0xB9).
- A non-zero sub-step return logs "UpdateAllResults.LocalHandlerFailed" and sets fp = 1. fp is sticky across iterations and is stored at 0x65486A.
- The return value is (u8)sp+0x88 != 0 (0x654AC0..0x654ACE).

| sub-step | can it fail? | citation |
|---|---|---|
| UpdateVisionMarkers (DetectingMarkers) | **Yes.** It returns BlockWorld::UpdateObservedMarkers(), and every early exit sets r5 = 0 (0x6550A0). UpdateObservedMarkers returns non-zero only when **CreateObjectsFromMarkers** fails (0x624FC4 → 0x6250B6) or **AddAndUpdateObjects** fails (0x62505A → 0x6250B6). Every other path sets r6 = 0 at 0x62521E. | 0x654DF4..0x654DF8; 0x654D60..0x6550AC; 0x624EE8..0x625232. EXACT_SOURCE for the propagation; when those two callees fail: RECOVERABLE_GAP |
| UpdateFaces | No. It returns FaceWorld::Update, whose only return is `movs r0,#0`. | 0x655274; 0x4F54DE |
| PetWorld::Update | No. | 0x50BAAC |
| Motion, OverheadEdges, ToolCode, ComputedCalibration, ImageQuality, LaserPoints | No. Each has a single return of 0. | 0x6553D2, 0x65541C, 0x655498, 0x65554C, 0x65560E, 0x6557EE |
| DebugImagesPresent | It logs an error, but after fp is stored, so it never sets the flag. | 0x654860..0x6549A2 |

So once a calibration is installed, NV Update is skipped on a tick only when a processed result in that tick had DetectingMarkers set and BlockWorld's object creation or update failed.

## Contradictions of the earlier reports
- **NV report, step 1 and step 4 (implicit):** the CameraCalib read is not dispatched on the first ProcessRequest; 12 constructor reads precede it. NV-gap 4b ("whether a write is queued before it is UNKNOWN") is now settled: **no write or erase, but 12 reads**. Ten of them are unconditional; #3/#4 depend on VisionSystem::Init succeeding.
- **NV-gap open question 3:** DefaultCameraParams does **not** install a calibration.
- **NV-gap 1g:** its citation 0x65476E is the ComputedCalibration sub-step, which can never fail. The only live failure source is the markers → BlockWorld path.
- **NV-gap 4k:** settled by Q3. There is no cap, and short buffers are kept with the data truncated.
- **M3-021 (1k/1l/1m):** agrees with the source. One log-only refinement: SetNextCameraParams is called twice, so a "Params already requested … Replacing" warning follows when the first SetCameraSettings succeeded. The CurrentCameraParams third field is VC+0x329, not a constant.

## Records whose evidence is too weak
- Any record that treats the CameraCalib NV read as the first or only NV exchange at connection is not supported. The CB22 streaming-ready one-shot also waits until the whole NV queue drains (2m).

## Open questions
1. Whether VisionSystem::Init (0x6B0658) can fail in production. RECOVERABLE_GAP.
2. Unity game messages sent before the mfgId arrives were not audited exhaustively. UNKNOWN.
3. When CreateObjectsFromMarkers or AddAndUpdateObjects fail was not read. RECOVERABLE_GAP, outside NV scope.
4. How long reads #1..#12 take on our robot. HARDWARE_ONLY. This decides how long the CameraCalib read and the CB22 flag are delayed.
5. These findings change records outside NV: M1-041/CB22 (streaming readiness is tied to the NV queue draining), and the new NV-subsystem records for the connection-time queue.
