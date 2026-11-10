# FACE-GUEST-INIT-1

## Claims and evidence

- Shipped video and asynchronous vision construct a FaceTracker after the instantiating diagnostic and before the motion and pet constructors at 0x006B0F56–0x006B0FC8. The wrapper at 0x0086B1E8 and Impl at 0x0086B2A8–0x0086B368 own the recognizer and seeded face ordering. The managed stock initialization stages one shared detector and recognizer and adopts them together on the current owner. The host worker retains its existing task-based scheduling.
- The guest starts with ready clear and the version read at 0x0086B4DE–0x0086B4EA. It allocates common and video DT at 0x0086B598 and 0x0086B974, then applies delay 1, search (2,2,5), direction 0, pose extension (1,1), accuracy 0, angle (1,0x803803), size (48,640), threshold 500 in the order at 0x0086B986, 0x0086BC48, 0x0086BD0E, 0x0086BEA8, 0x0086BF6E, 0x0086B788, 0x0086B908 and 0x0086BA7E.
- DT result, PT handle and confidence 1, two PT results, then EX, SM and GB handle/result pairs are eager at 0x0086BBBC–0x0086BE36. The same guest owns the FR ordinary feature, merge feature and album (1000 users, four samples) at 0x00862A24–0x00862A42, reached from 0x0086BE4A. Only successful FR initialization reaches tracker ready and Success at 0x0086BE4E–0x0086BE68. Startup album work follows this join.
- Native defined failures stop at the failing guest operation and leave the created prefix owned. The wrapper logs InitFailed and sets _errG at 0x0086B374–0x0086B3B6 while outer initialization continues. Later Update logs NotInitialized and returns one at 0x0086D74A–0x0086D7E8, and DetectFaces still processes an empty result at 0x006B36DA–0x006B3800. The managed status distinguishes that result from unavailable host resources and prevents retry through camera or NV.
- On retirement the recognition worker drains before the current album, merge feature and ordinary feature are deleted at 0x0086284C–0x00862928. Remaining live handles follow SM handle/result, GB handle/result, EX result/handle, PT A/B/handle, DT result/handle, and common at 0x0086C5AE–0x0086CA26. A stale owner or initialization epoch cannot publish a captured old frame. Host retirement can defer draining the displaced worker to preserve the engine update thread.

## Files and tests

- Emulation/OkaoFaceDetection.cs, OkaoFaceAttributes.cs and OkaoFaceRecognition.cs provide staged allocation and ordered guest cleanup. Vision/Faces.cs and FaceRecognizer.cs own guest and album lifetimes. Vision/NativeVisionInitialization.cs, VisionImageQuality.cs and VisionSystem.cs carry the stock initialization and frame session.
- NativeVisionInitializationTests exercises startup through the configured robot and verifies face success precedes the NV album request. The PT and FR injected allocation failures reach the startup diagnostics, real NV replies and a real F2 input, then assert processed-empty mode and no creation retry.
- NativeStockFaceInitOracleTests checks the eager guest against the unchanged 19-frame raw oracle at both poison fills. The manager independently ran the shipped ARM exports against the same raw corpus, obtaining identical canonical SHA-256 6fa7b07b972566c217650e5bdd34f43dfa8cbab1d71f356ec3a1c49256210212 at both fills. The manager also ran original FaceRecognizer Init and Shutdown at 0x00862A18 and 0x0086284C, and original unready Update at 0x0086D740. Those checks observed the three FR allocation stops and the unready return before any result delivery.

- NativeStockFaceInitOracleTests injects one returned guest deletion failure and checks the later source-order calls. The supplied-config regression sends a real F2 after reinitialization and proves the retained face session still processes the frame. Builder passed 94/94 affected and 42/42 final focused cases. Manager independently passed 102/102, including robot removal and the unchanged Acapela and native OKAO oracles.

## MISSING

- Alternate face detector and recognition settings, the file album route, full native worker scheduling, and complete VisionSystem recipient teardown remain separate. Host guest faults are reported as unavailable. Some setter failure diagnostics use the operation name; the original ready/error result and constructor failure are preserved. The dedicated active-recognition replacement matrix remains untested. No hardware run was made.

## Records and workers

- M11-059, M11-016, M14-010, M14-011 and M11-055 retain their current fidelity status and approval. Reader used gpt-6.1-sol at medium effort. Builder and reviewer used defaults.
