# VISION-INIT-SETTLEMENT-1

## Claims and evidence

- VisionComponent clears its own admission byte +0x10 at 0x00650D30 before reading its prefix. It calls VisionSystem::Init at 0x00650E36 only after that prefix succeeds. The inner worker clears +0x58 at 0x006B0678 and sets it at 0x006B141A. Its reader is 0x006B2CD6. A component prefix failure leaves the earlier worker state intact. The managed owner now keeps the two readiness stages separate.
- After worker success, absent, empty or exact robot selector takes the stock NV route at 0x00650FD6–0x0065108A. LoadFaceAlbumFromRobot clears the source buffers at 0x006512C4–0x006512D8 and queues 0x184000 before 0x183000 at 0x006512F0/0x00651330. It emits the Initiated message at 0x006513B0. The component sets +0x10 at 0x006510E2 before Robot constructor backup reads at 0x005103CE–0x005103D4. The component does not wait for the async enrollment result. The managed stock tail carries the original owner and initialization epoch through enqueue, log, latch and album adoption. Supersession before the complete NV pair withholds the ordered tail, while supersession after the pair keeps backup ordering without publishing old component readiness.
- A source-defined worker failure emits VisionComponent.Init.VisionSystemInitFailed at 0x00650E58 and sets _errG at 0x00650E92. The original constructor ignores the result and continues backup reads at 0x005103CE–0x005103D4, with no album path and +0x10 clear. Earlier component prefix failures and unknown host inputs have separate paths. First-state NV drain and streaming readiness keep their existing ownership.
- SetNextImage uses component +0x10 at 0x00652B1C before its remaining frame gates. The decoded worker keeps the original system +0x58 check. Direct supplied initialization and no-RIC robot fixtures explicitly complete their own host adapter without queueing the stock NV pair, and a constructor-phase nested test call cannot expose a component before the owning tail.

## Files and tests

- CozmoEngine.cs and CozmoRobot.cs carry the original initialization attempt through the stock constructor and the explicit no-RIC adapter. Vision/NativeVisionInitialization.cs and Vision/VisionImageQuality.cs keep the component state and report known system failure. Vision/VisionSystem.cs applies component readiness to camera admission.
- The manager called original ARM IsInitialized at 0x006B2CD6 and SetNextImage at 0x00652B04 with all four combinations of the two bytes. The independent fixture held later admission disabled. It observed the original component warning only when +0x10 was clear, independently of +0x58.
- NativeVisionInitializationTests checks the real startup Initiated phase and FIFO read order, original owner publication, staged prefix failure with worker readiness retained and real F2 refused, and the defined system error. The normal camera test checks successful post-publication admission. DriveOffPilotVisionTests keeps explicit synthetic phase input in its direct fixture. Startup membership independently rejects pre-publication frames in the managed stack. The test observes stage state at that point and exercises the actual outer gate after the owner is published.

- The builder passed 278/278 affected tests before the final tail adjustment and 50/50 focused after it. The manager passed 64/64 independent cases. Reentry tests cover a superseded partial album enqueue and a superseded complete pair with backup continuation.

## MISSING

- Nonrobot file-album loading is still withheld. Its native path would erase, save NV and broadcast at 0x006514FC–0x00651538 before attempting the distinct filename loader and loaded-name reports. No partial erase path was introduced. Eager FaceTracker/recognizer initialization, DataPlatform host provisioning, and the component drop-statistics runtime remain separate. The current NV implementation can reject only invalid tags, and the stock album constants are valid. No hardware run was made.

## Workers

- Reader used gpt-6.1-sol at medium effort. Builder and reviewer used defaults. The manager checked the separate native gate reads, worker failure and stock NV terminal ordering.

## Records

- M11-059 owns startup and readiness. M11-033/M11-040 own camera handoff, M3-033/M3-034 own the startup NV sequence and album result, and M15-033 owns backup reads. No fidelity status or approval changed.
