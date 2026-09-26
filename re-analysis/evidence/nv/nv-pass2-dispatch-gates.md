All four questions are settled from libcozmoEngine.so, apart from the small leftovers listed at the end. I could not write the report to `...\scratchpad\extract\NV-gap\report.md`: the harness blocks report files from subagents, so the full report is below. The scratch outputs are in `...\scratchpad\extract\NV-gap\`: `ru.txt` (Robot::Update disassembly), `uar.txt` (UpdateAllResults), `clad.txt` (the three serializers) and `fgrep.py` (per-function instruction grep). I did not write anything in the repository.

**Key findings:**
- **Q1:** NVStorageComponent::Update is skipped on a tick in four cases:
  - the engine is not in the Running state;
  - no SyncTimeAck plus a following RobotState has been handled yet;
  - once a calibration exists, UpdateAllResults fails;
  - CozmoEngine::Update itself returns early.
- **Replies are not gated:** NVOpResult reassembly and the read callback run in the message pump, before UpdateAllRobots in the same tick.
- **Q2:** Robot+0x24 is word 1 of mfgId 0xED, the body hardware version. It starts at −1. The latest bundles show bodyHw=4 on your robot, so the original would zero all 8 distortion coefficients.
- **Q3:** Robot+0 is the CozmoContext's own `this`, so it is never null. InitSizeTable runs and 0xCD is subscribed.
- **Q4:** On disconnect or destruction, a queued or pending read gets no callback at all. The engine ignores the SendMessage result. The C# serializers match the wire byte for byte; only field names differ.

# NV CameraCalib read path: gap pass

## Q1. What gates NVStorageComponent::Update (called at 0x51416A)

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| 1a Engine tick | CozmoEngine::Update returns with no robot update in two cases: engine+0x10 == 0 (logs "Cannot update CozmoEngine before it is initialized."), or UiMessageHandler::Update() returns non-zero (logs an error). | 0x4ED4DE..0x4ED4E4 → 0x4ED56E (string 0xBE463E); 0x4ED514..0x4ED51C | NEW | EXACT_SOURCE |
| 1b Engine state | `tbb` on engine+0x62. **3 (Running): UpdateTime, UpdateRobotConnection, NeedsManager::Update, UpdateAllRobots.** 4 (UpdatingFirmware): UpdateRobotConnection and UpdateFirmware only, no UpdateAllRobots. States 0, 1 and 2 never call UpdateAllRobots. Values above 4 are an error. | 0x4ED5C4..0x4ED5CC; table at 0x4ED5D0 → 0x4ED6BE/5D6/5F0/61E/654; 0x4ED648; unity/scripts/csharp/Anki.Cozmo/EngineState.cs:3-10 | NEW | EXACT_SOURCE |
| 1c UpdateAllRobots | Calls Robot::Update on every robot in the map, with no condition. | 0x52F6D4..0x52F6E4 | NEW | EXACT_SOURCE |
| 1d Sync-time watchdog | If robot+0x520 > 0 and now > it + 5.0 s: warning "Robot.Update.SyncTimeAckNotReceived", then +0x520 = 0. This does not return. | 0x513BF6..0x513C5A; string 0x514618 | NEW | EXACT_SOURCE |
| 1e **Gate A** | If robot+0x34E == 0: debug "Waiting for first full robot state to be handled", then **return 0**. NV Update is skipped. | 0x513C5C..0x513C62 → 0x513DA6 → 0x514482 | NEW | EXACT_SOURCE |
| 1f What sets +0x34E | The ctor writes 0 (0x5100BA `str.w r1(0x100),[r5,#0x34b]`; r4 = 0 at 0x510080). The only writer of 1 is UpdateFullRobotState. It writes it only if robot+0x29 != 0 (0x51293C..0x512948). The same guard covers robot+0x2C = RobotState word 0 (0x512954), the clock the NV timeout uses. robot+0x29 is 0 in the ctor (0x50FC4A), cleared by SyncTime (0x515228), and set to 1 by HandleSyncTimeAck (0x5366AC; syncTimeAck is 0xC2 per the CLAD catalog). | as cited | NEW | EXACT_SOURCE |
| 1g **Gate B** | vc = robot+0x258. If vc+0x28 != 0 it calls UpdateAllResults(). A non-zero result logs "Robot.Update.VisionComponentUpdateFail" and **returns** (NV skipped). vc+0x24 is a Vision::Camera (ctor 0x6500BE..0x6500E0), and Camera+4 is its calibration pointer (SetCalibration stores it at 0x85DEFA). **So Gate B applies only after a calibration is installed.** UpdateAllResults returns bool(sp+0x88), which is set when a sub-update fails (0x654AC0..0x654ACE; 0x65476E; 0x65486A). | 0x513C6E..0x513CBC; string 0x514640 | NEW | EXACT_SOURCE for the gate. The full list of failing sub-steps is RECOVERABLE_GAP (0x6542EC..0x654AE4, outside NV scope). |
| 1h Non-gating branches | SendAbsLocalizationUpdate; the charger check; a global counter at 0x1051010 that skips only BehaviorManager::Update (jumps to 0x513FBA); an ActionList::Update failure, which only logs (0x5140C0); AnimationStreamer::Update, which runs only if +0x29 && +0x2A and whose failure only logs (0x51410C..0x514162). None of these returns. | as cited | NEW | EXACT_SOURCE |
| 1i Replies not gated | UpdateRobotConnection is RobotManager+0x60 → vtable+0xC = MessageHandler::ProcessMessages. It runs in states 3 and 4, before UpdateAllRobots. HandleNVOpResult (reassembly and callback) is therefore not held back by Gates A or B. The gates hold back only ProcessRequest (the send) and the timeout check. | 0x52F850..0x52F856; vtable 0x10310D0 (entry 0x10310E4 = 0x69D85D); 0x4ED62E, 0x4ED658 | NEW | EXACT_SOURCE |
| 1j NV Update body | State 0: ProcessRequest, then a tail call to ProcessOnIdleCallbacks (veneer 0x8CCE6C → 0x4B9A20). State 1: the write path. State 2: if +0x78 == 0, "Update.NoReadPending" and SetState(0); else the timeout check. Any other state: "Update.InvalidState" and SetState(0). ProcessRequest does nothing while the deque size (+0x10C) is 0. | 0x6456BC..0x6456EC; 0x64575A → 0x64598C (string 0x645A88); 0x6457C2; 0x644FEE..0x644FF4 | NEW | EXACT_SOURCE |

## Q2. What Robot+0x24 holds

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| 2a Init | Robot ctor: +0x20 = 0, **+0x24 = −1**, +0x28 = 0xFF. | 0x50FC30..0x50FC46 (`strd r0(0),r1(-1),[r5,#0x20]`) | NEW | EXACT_SOURCE |
| 2b Writer | HandleRobotSetBodyID loads mfgId words w0, w1, w2. It builds `$phys` = "0xbeef%04x%04x%08x" (w2, w1, w0), **stores +0x24 = w1** and +0x1C = w0, then calls SetBodyColor(w2), which stores +0x28 only if the low byte is in {0,2,3,4}. | 0x536FC2..0x536FCC; format string 0x5370D0; **0x537052 `str r6,[r4,#0x24]`**; 0x518960..0x518970 | M1 E6 (agrees) | EXACT_SOURCE |
| 2c Message | CLAD **ManufacturingID, mfgId 0xED**. Unpack reads three 4-byte fields. Each Robot subscribes in InitRobotMessageComponent (`movs r1,#0xed`), which the Robot ctor calls. | 0x7B1750..0x7B1772; 0x532CD0..0x532CDA; 0x510348 | M2 MfgId | EXACT_SOURCE |
| 2d Meaning | w1 is the value the connection lambda broadcasts to Unity as RobotConnectionResponse.**bodyHWVersion** (int32). It is the body hardware version, a small integer with no unit. | M1 CB18/CB19; unity/.../RobotConnectionResponse.cs:54,124 | — | EXACT_SOURCE |
| 2e Ordering | The ConnectToRobot handler calls AddRobotConnection then AddRobot, so the Robot and its 0xED subscription exist before any robot message (0x4ED074, 0x4ED07C). The first mfgId both sets +0x24 and triggers RobotConnectionResponse, which only queues the Read. The callback runs on a later NVOpResult, so it sees w1 (−1 only if no mfgId reached this Robot). | as cited; M1 E1..E6 | NEW | EXACT_SOURCE |
| 2f Callback gate | `[[VC+0x14]+0x24]`, where VC+0x14 = Robot& (0x6500BA) and the lambda captures VC `this` (0x6583C6). Then `cmp r0,#6; bgt` (signed). If ≤ 6: "VisionComponent.ReadCameraCalibration.IgnoringDistCoeffs" and memclr8(dist, 0x20), which zeroes all 8 coefficients. | 0x65AD54..0x65AD5A; string 0xBFE376; 0x65AD98..0x65AD9C | M3-022 / 1j | EXACT_SOURCE |
| 2g Your robot | The bundles record `ManufacturingID serial=0x0240e889 bodyHw=4 color=0`. **With w1 = 4 the original zeroes all distortion coefficients.** robotAvailable's "revision 5" is a different field; the gate reads mfgId w1. | re-analysis/acceptance/hardware/20260925-061148-CONTROL/events.jsonl:19; 20260924-202748-CONTROL/events.jsonl:21 | — | HARDWARE_ONLY (robot value seen in our stack's capture, not an official-app capture) |

## Q3. The NVStorageComponent ctor context

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| 3a Robot+0 | Robot ctor(robotID r1, context r2): `str sl(=r2),[r6],#4`, so Robot+0 = CozmoContext*. Robot+0x10 = robotID. | 0x50FBFC; 0x50FC0C; 0x50FC38 | NEW | EXACT_SOURCE |
| 3b Source | The only caller of the Robot ctor is RobotManager::AddRobot, which passes `ldr r2,[r4,#0x18]`. The RobotManager ctor stores its context argument there (0x52E486, 0x52E4A2). The only construction of RobotManager is in the CozmoContext ctor, with r1 = the context's own `this` (0x4EA7A4..0x4EA7AC; stored at context+0x20). | 0x52EE8E..0x52EE9C | NEW | EXACT_SOURCE |
| 3c Non-null | The context is a constructed object's `this`, so it is non-null. **InitSizeTable (0x6429D0) is reached.** The null branch (0x6428EE → 0x642972) only logs a warning and skips it. | 0x6428EC | NEW | EXACT_SOURCE |
| 3d 0xCD subscribed | context+0x20 (RobotManager) → +0x60, a MessageHandler that the RobotManager ctor creates (0x52E4E8..0x52E4FC). It subscribes robotID + **0xCD** → HandleNVOpResult (0x519F8C → 0x51CD2A) and stores the handle at +0x128. The five game-message subscriptions happen only if context+4 (IExternalInterface) != 0. | 0x642918..0x64294C (`movs r7,#0xcd` 0x64292C); 0x6428F0..0x642914 | NEW | EXACT_SOURCE |

## Q4. Other NV state and paths the read touches

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| 4a FIFO | Read emplace_backs onto the deque at +0xF8. ProcessRequest takes the front element and pop_fronts it after arming. One operation is in flight at a time. | 0x644E82; 0x644FF8..0x645022; 0x64547E | NEW | EXACT_SOURCE |
| 4b Behind a write | A Read queued behind a write/erase waits until state 1 ends (completion, "Update.WriteTimeout" when robot+0x2C > +0x44 with write-cb(−4), or "NoDataToWrite"). | 0x6456F4..0x645758; 0x6459A2 | NEW | EXACT_SOURCE for the mechanism. Whether production queues an NV write before the CameraCalib read is UNKNOWN (not traced). |
| 4c Send result ignored | The result of Robot::SendMessage(reliable = 1, hot = 0) is not tested. The read is armed and the state becomes 2 anyway. Only the 5 s robot-clock timeout recovers a lost send. | 0x64539E..0x6453A2; 0x6453F8..0x645476 | NEW | EXACT_SOURCE |
| 4d Owned buffer | A previously owned +0x54 vector is freed if +0x70 is set. A caller vector is cleared and +0x70 = 0. With a null caller vector (the VisionComponent passes nullptr), a new vector is made and +0x70 = 1. | 0x6453F8..0x64541A; 0x645448..0x64546E | NEW | EXACT_SOURCE |
| 4e Disconnect | RobotConnectionManager::HandleDisconnectMessage → RemoveRobot → ~Robot → nulls Robot+0x260 and calls its virtual delete. | 0x62FAF8; 0x52F2F6; 0x5112D6..0x5112E4 | NEW | EXACT_SOURCE |
| 4f Destroyed mid-read | ~NVStorageComponent releases the handles at +0x128 and destroys the deques at +0x110 and +0xF8 plus the +0xE8 buffer. For the pending block at +0x50 (0x643E80) it frees +0x54 if owned and destroys the std::function at +0x58 **without invoking it**. **Neither a queued nor a pending read gets a callback; no NV_TIMEOUT is delivered.** | 0x643EC4..0x643F8C; 0x643E80..0x643EC2 | NEW | EXACT_SOURCE |
| 4g States stop, Robot alive | The timeout needs robot+0x2C to advance, which needs RobotState with +0x29 set, and needs Robot::Update past Gate B. Otherwise the read never completes. | 0x64576A; 1f/1g | NEW | EXACT_SOURCE |
| 4h NVCommand wire | u32 tag, u32 @4 (Length), u8 op, u8 @9, u16 count + bytes. Size = 12 + n. | Pack 0x7CE63C..0x7CE692; 0x71A890..0x71A8C0; 0x7CE6CC | M2 NVCommand | EXACT_SOURCE |
| 4i NVOpResult wire | 4 tag, 4 @4, 1 op, 1 result, u16 count + bytes. | Unpack 0x7CE76A..0x7CE7AC; 0x71A8F6..0x71A91E | M2 NVOpResult | EXACT_SOURCE |
| 4j C# cross-check | In RobotMessages.g.cs, NVCommand (lines 1897..1927), NVOpResult (3354..3384) and ManufacturingID (3808..3829) have the same order and widths, so the bytes are identical. Only names and signedness differ: word@4 is `int Length` in both classes, though the engine logs NVCommand word@4 with %u and uses NVOpResult word@4 as a blob index. NVCommand byte 9 is `Unknown`. `sbyte Result` matches `ldrsb` at 0x642FDE. | as cited | — | EXACT_SOURCE (wire); names are not native |
| 4k Array length cap | The vector reader at 0x73213C was not read, so whether Unpack caps n is unknown. | 0x71A914 → 0x73213C | NEW | RECOVERABLE_GAP |

## Existing claims contradicted or settled
- Earlier NV report, open question 5: "context non-null is inferred" is now **proven** (3a..3d).
- Earlier NV report, step 4 (RECOVERABLE_GAP) is settled, and it was incomplete. NV dispatch also needs EngineState == Running, a SyncTimeAck followed by a RobotState, and, once a calibration exists, no VisionComponentUpdateFail. Replies are not gated.
- Earlier NV report, step 16 / open question 4 ("robot+0x24 unknown") is settled: it is mfgId (0xED) word 1, the body hardware version, initialized to −1. The report's remark that the +inf distCoeffs[6] "is installed as-is when robot+0x24 > 6" is true, but it does not apply to your robot (bodyHw = 4). There the original zeroes all 8 coefficients.
- No M1 record is contradicted. M1 E6 is confirmed at 0x537052 and 0x537054.

## Evidence too weak for the status claimed
- Any record that treats the NV read as ending in either "completes" or "times out with NV_TIMEOUT" is wrong. The original gives no callback on disconnect or destruction (4f), and none while RobotState is absent (4g).

## Open questions
1. Whether the production connect flow queues an NV write or erase ahead of the CameraCalib read (4b) was not traced.
2. The full list of UpdateAllResults failure sub-steps (Gate B) was not read. It matters only after a calibration is installed.
3. Adjacent and not traced: HandleDefaultCameraParams (robot tag **0xC8**, 0x537114) tail-calls VisionComponent::HandleDefaultCameraParams (veneer 0x8CB3CC → 0x4AA36C). This is a second robot-sourced camera-parameter path, outside NV. Whether it installs a calibration (which would open Gate B and change what vision uses) is UNKNOWN.
4. The array-length handling at 0x73213C (4k) was not read.
