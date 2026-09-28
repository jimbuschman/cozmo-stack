# Extraction: the live path to the engine's audio (app play request -> engine per-frame render)

Read-only extraction. Scope: exactly how the live path reaches the engine's audio, for the M6 build
job to wire it. Authority: libcozmoEngine.so 3.4.0-1204 (`resources/lib/armeabi-v7a/libcozmoEngine.so`,
SHA-256 02263C07...89E1) first; then the decompiled Unity C# under `unity/`. The stack's C# under
`cozmo-stack/src/` was read only to learn which behaviours need an answer; it is not evidence.

All native addresses are VAs. The Wwise runtime is ARM-mode in `0x0095E540..0x00AE2E40`; the Anki code
around it is Thumb. Ghidra files are navigation aids; the instructions below were re-read from the `.so`
with capstone (ARM and Thumb).

## The finding in one line

The app posts a Wwise event as a CLAD `MessageGameToEngine`; the engine queues it to Wwise; the engine's
per-frame tick (`CozmoEngine::Update` -> `AudioMultiplexer::UpdateAudioController` ->
`AudioEngineController::Update`) and the robot-audio lambda both call the same `RenderAudio` wrapper
(`0x0099F130(1)` -> `0x9AFD10`), which signals the Wwise audio thread (semaphore at engine+0x54) or runs
`Perform` (`0x9AF8A8`) synchronously. `Perform` calls the message-pump/render body `0x9ADFD8`. The
robot-audio path (M6-016) is pumped by `RobotAudioClient::ProcessEvents` (`0x00599FE2`), which the A6
lambda calls right after `PostCozmoEvent`.

## The chain (step | what the original does | citation | existing record id or NEW | classification)

### A. App-side play request (Unity)

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| A1 | Play request | `PlaySound.Play()` arms the event; `Update()` calls `GameAudioClient.PostAudioEvent(_AudioEventParameter)`. | `unity/scripts/csharp/Anki.Cozmo.Audio/PlaySound.cs:29,72-82` | NEW | EXACT_SOURCE |
| A2 | Client facade | `GameAudioClient.PostAudioEvent(parameter,...)` -> `UnityAudioClient.Instance.PostEvent(parameter.Event, parameter.GetGameObjectType(), flag, handler)`. UI/SFX/CodeLab variants pick the game object. | `unity/scripts/csharp/Anki.Cozmo.Audio/GameAudioClient.cs:17-45` | NEW | EXACT_SOURCE |
| A3 | Build the CLAD message | `UnityAudioClient.PostEvent` allocates a play id (`_GetPlayId`, ++, skips 0), sets `callbackId = (flag!=EventNone)?id:0`, builds `PostAudioEvent`, sets `_RobotEngineManager.Message.PostAudioEvent` and calls `SendMessage()`. | `unity/scripts/csharp/Anki.Cozmo.Audio/UnityAudioClient.cs:224-237,279-287` | NEW | EXACT_SOURCE |
| A4 | Wire layout | `PostAudioEvent` = `u32 audioEvent, u32 gameObject, u16 callbackId`, `Size = 10`; `MessageAudioClient` tag 0 = PostAudioEvent. | `unity/scripts/csharp/Anki.AudioEngine.Multiplexer/PostAudioEvent.cs:89-107`; `MessageAudioClient.cs:10` | NEW | EXACT_SOURCE |
| A5 | Envelope tag | The envelope is `MessageGameToEngine`, tag `PostAudioEvent = 1` (tags 2..6 are StopAll/GameState/Switch/Parameter/MusicState). | `unity/scripts/csharp/Anki.Cozmo.ExternalInterface/MessageGameToEngine.cs:14-16` | NEW | EXACT_SOURCE |
| A6 | Robot volume (interface) | `SetRobotVolume` is a separate `MessageGameToEngine` tag 104 -> `Robot.SetRobotVolume`; the engine's `SetRobotVolume` posts RTPC `robot_volume`. The actual robot volume is the robot-side `SetAudioVolume` (M1-042). | `unity/scripts/csharp/Robot.cs:1461-1464`; M6 inventory A24 | M1-042, M3 C17 | EXACT_SOURCE (send) / RTPC reach IMPLEMENTATION_GAP |

### B. Engine receives the app message (AudioUnityInput -> multiplexer -> AudioEngineController)

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| B1 | Subscription | `AudioUnityInput` ctor registers handlers for `MessageGameToEngine` tags 1..6 through the external interface (vtable +0x2c); tag value 1..6. | Thumb ctor `0x00591590` (decomp 00591590.c:69,102,135,168,201,234), six subscribe calls, each through the external interface vtable +0x2c | NEW | EXACT_SOURCE |
| B2 | Dispatch by tag | `AudioUnityInput::HandleGameEvents`: switch on the u16 tag; case 1 loads vtable slot +0xc and calls `MessageGameToEngine::Get_PostAudioEvent`. | `0x005919B8..0x005919D4`: `ldrh r0,[r5]; subs r1,r0,#1; cmp r1,#5; tbb`; case-1 body `0x005919CA ldr r0,[r4]`, `0x005919CC ldr r6,[r0,#0xc]`, `0x005919D0 blx 0x4AEEC0`, `0x00591A14 blx r6` | NEW | EXACT_SOURCE |
| B3 | The slot | The AudioUnityInput vtable (`_ZTVN4Anki5Cozmo5Audio15AudioUnityInputE` = 0x1023CA8; vptr = +8) slot +0xc resolves to `AudioMuxInput::HandleMessage(PostAudioEvent)` = `0x008DFC4C`. | vtable reloc `0x1023CBC` -> `_ZN4Anki11AudioEngine11Multiplexer13AudioMuxInput13HandleMessageERKNS1_14PostAudioEventE` = `0x008DFC4C` | NEW | EXACT_SOURCE |
| B4 | To the multiplexer | `AudioMuxInput::HandleMessage(PostAudioEvent)` calls `AudioMultiplexer::ProcessMessage(multiplexer, msg, channel)`. | `0x008DFC4C:0x008DFC5E b.w 0xAE3130` (Thumb->ARM veneer) -> `AudioMultiplexer::ProcessMessage 0x008DED14` | NEW | EXACT_SOURCE |
| B5 | To the controller | `AudioMultiplexer::ProcessMessage(PostAudioEvent)` builds a callback context (if callbackId != 0) and calls `AudioEngineController::PostAudioEvent(event, gameObj, ctx)`. | `0x008DED14:0x008DED92 blx 0x4AF274` -> `AudioEngineController::PostAudioEvent 0x008D1F20` | NEW | EXACT_SOURCE |
| B6 | Wwise PostEvent | `AudioEngineController::PostAudioEvent` -> `FUN_008D8CE4` -> Wwise core `PostEvent 0x009A6704(event, gameObj, flags = 1 \| (ctx&2)<<1 \| (ctx&1)<<3, callback 0x008D8D41, cookie ctx)`. The core **queues** a type-1 message and returns a playing id (atomic ++), or 0 when the event id is unknown. | `0x008D8CE4:0x008D8D32 blx 0x009A6704`; flags at `0x008D8CF8..0x008D8D0C` | M6-006 (PostEvent 0x9A6704) | IMPLEMENTATION_GAP |

### C. The engine tick pumps the controller

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| C1 | Engine tick | `CozmoEngine::Update` (state 3 branch) ends by calling `AudioMultiplexer::UpdateAudioController` when the context's multiplexer is non-null. | `0x004ED4D4:0x004ED6BE ldr r0,[r4,#0x34]; 0x004ED6C0 ldr r0,[r0,#0xc]; 0x004ED6C2 cbz; 0x004ED6C4 blx 0x4A5290` | NEW | EXACT_SOURCE |
| C2 | UpdateAudioController | `AudioMultiplexer::UpdateAudioController()` calls `AudioEngineController::Update()`. | `0x008DF3DA:0x008DF3DC b.w 0xAE3110` (veneer) -> `AudioEngineController::Update 0x008D2928` (decomp 008df3da.c:14) | NEW | EXACT_SOURCE |
| C3 | Controller update | `AudioEngineController::Update` runs `MusicConductor::UpdateTick` then `FUN_008D88C0(this+8, 0)`. | `0x008D2928` (decomp 008d2928.c:21-24) | NEW | EXACT_SOURCE |
| C4 | Render gate | `FUN_008D88C0(flag, 0)`: if `*flag == 0` returns; otherwise tail-calls the RenderAudio veneer with argument 1. | `0x008D88C0` (`0x008D88C6 b.w 0xAE3060` veneer -> `0x0099F130`; decomp 008d88c0.c:11-15) | NEW | EXACT_SOURCE |
| C5 | Callback drain | The same `AudioEngineController::Update` then drains the queued audio callbacks (`AudioCallbackContext::HandleCallback`) under a mutex. This is the end-of-tick drain M6-016 refers to. | `0x008D2928` (decomp 008d2928.c:25-45); `AudioCallbackContext::HandleCallback 0x004D2428` | M6-016 (gapE 6.1..6.4) | IMPLEMENTATION_GAP |

### D. Robot-audio path (M6-016)

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| D1 | Create the animation | `AnimationStreamer::InitStream` calls `RobotAudioClient::CreateAudioAnimation(animation)`. | `0x0057B674:0x0057B7DE ldr.w r0,[r5,#0x1b0]; 0x0057B7E8 blx 0x4ADBF4` | M6-016 (A3) | IMPLEMENTATION_GAP |
| D2 | Per-robot animation object | `CreateAudioAnimation` builds `RobotAudioAnimationOnRobot` (or OnDevice for game object 6). | `0x00599FF0`; ctor `0x00597C20` / `0x005974B0` | M6-016 (A3) | IMPLEMENTATION_GAP |
| D3 | Draw alternatives | `RobotAudioAnimation::InitAnimation` calls `GetAudioRefIndex(true)` per keyframe, `GetAudioRef`, pushes `{u16 idx, eventId, kf+0xC time, ref+4 volume, state 0}`, sets `+0x3D` if `ref+0xC != 0`, and advances the track to its end. | `0x00596814` (A2) | M6-016 (A2), M5 C14 | IMPLEMENTATION_GAP |
| D4 | Post at wall-clock offsets | `BeginBufferingAudioOnRobotMode` sets state 1 and posts each event with `Dispatch::After(queue, event.time - first.time, lambda)`. | `0x00597F00:0x00597F72 blx 0x4AF4D8` (`Util::Dispatch::After`), string "PostAudioEventToRobotDelay" | M6-016 (A5) | IMPLEMENTATION_GAP |
| D5 | The lambda | The posted lambda (function at `0x00597760`, body `0x00597830..`; a second copy at `0x0059818C`/`0x00598260`) sets state 1, `+0x44++`, calls `PostCozmoEvent`, and if the playing id != 0 calls `SetCozmoEventParameter(playingId, 0xD2687048 event_volume, volume)`, then `ProcessEvents`. | `0x0059783C blx 0x4AF4F0` (PostCozmoEvent); `0x00597868 blx 0x4AF4FC` (SetCozmoEventParameter, r2=0xD2687048); `0x0059786E blx 0x4AF508` (ProcessEvents). Second copy: `0x0059826C`, `0x00598298`, `0x0059829E`. | M6-016 (A6) | IMPLEMENTATION_GAP |
| D6 | Post synchronously | `RobotAudioClient::PostCozmoEvent` builds an `AudioCallbackContext` (ctx+0x38 = 0, so the trampoline queues) and calls `AudioEngineController::PostAudioEvent` **directly** (no multiplexer). | `0x00599E50:0x00599F00 blx 0x4AF274` (decomp 00599e50.c:63-64) | M6-016 (A7) | IMPLEMENTATION_GAP |
| D7 | Routing | `RobotAudioClient` ctor registers game objects 7..10 -> plug-ins 1..4 -> `Robot_Bus_1..4` with `SetGameObjectAuxSendValues(gain 1.0)` and `SetGameObjectOutputBusVolume(0.0)`; game object 6 -> plug-in 0/no bus. | `0x005994A0`; M6 inventory A11 (0x0059962A..0x005999A4) | M6-016 (A11) | IMPLEMENTATION_GAP |
| D8 | Pump entry (the answer) | `RobotAudioClient::ProcessEvents` = `AudioEngineController::ProcessAudioQueue(this+0x30)`. | `0x00599FE2:0x00599FEA b 0x008D2946` (decomp 00599fe2.c:13) | M6-016 (A10) | IMPLEMENTATION_GAP |
| D9 | ProcessAudioQueue -> RenderAudio | `ProcessAudioQueue` -> `FUN_008D88C0(this+8, 0)` -> the same RenderAudio veneer. | `0x008D2946:0x008D294A b.w 0x008D88C0` (decomp 008d2946.c:14) | M6-016 (A10) / M6-017 | IMPLEMENTATION_GAP |
| D10 | Handoff to the robot | `AnimationStreamer::UpdateStream` -> `GetAudioToSend` pops the front audio frame (744 bytes, `0x2E8`) from the `RobotAudioClient`'s current animation and buffers it as `AudioSample` (0x8E); `RobotAudioAnimationOnRobot::PopRobotAudioMessage` (state 3) does the mu-law encode and zero-pad to 744. | `0x0057C84C` -> `0x0057C016` (`0x0057C03E mov.w r2,#0x2e8; blx memcpy`); `0x00597DB4` (A21) | M6-016 (A21), M3 C5 | IMPLEMENTATION_GAP |
| D11 | Abort | `AbortAnimation` -> `Dispatch::Stop`; `FlushAudioCallbackQueue`; `ResetAudioBufferAnimationCompleted`; `StopCozmoEvent = StopAll(gameObj)` + `ProcessEvents`; state 4. | `0x0059678E` (A23) | M6-016 (A23) | IMPLEMENTATION_GAP |

### E. Wwise audio thread, RenderAudio and Perform

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| E1 | SoundEngine::Init | `AudioEngineController::InitializeAudioEngine` -> `FUN_008D80D8` -> `FUN_008D81B0` -> `FUN_0099E3EC` (SoundEngine::Init). With default settings it sets the audio-thread-active byte (0x0108D949) = 1 and later calls `FUN_009B0200`. | `0x008D1D1E` -> `0x008D80D8` -> `0x008D81B0:0x008D81FC blx 0x0099E3EC`; flag store `0x0099EAD8 strb r3,[r4,#0xe1]` (r3=1, r4=0x0108D868); thread call `0x0099EF80 bl 0x009B0200` | NEW | EXACT_SOURCE |
| E2 | Start the thread | `FUN_009B0200` checks the flag (`0x009B026C ldrb r3,[ip,#0x3d]`) and, when set, calls `FUN_00A40940(engine+0x54)` with the semaphore at engine+0x54. It also posts a type-0x36 init message. | `0x009B0200:0x009B022C bl 0x9AF778` (type 0x36); `0x009B026C ldrb r3,[ip,#0x3d]; 0x009B0298 add r0,r5,#0x54; 0x009B029C bl 0x00A40940` | NEW | EXACT_SOURCE |
| E3 | pthread_create | `FUN_00A40940` sem_init's the semaphore, re-checks the flag, sets a detached attr and stack size, and `pthread_create(&DAT_0108df50, attr, FUN_00a4087c, sem)`. | `0x00A40954 bl 0x4D6784` (sem_init); `0x00A4096C ldrb r3,[r3,#0x3d]`; `0x00A409F4 bl 0x4A6934` (pthread_create) | NEW | EXACT_SOURCE |
| E4 | Thread loop | `FUN_00A4087C` (the entry): sets thread-local state, then `do { Perform(engine); sem_wait(sem); } while (sem->flag == 0)`. | `0x00A4087C:0x00A408BC bl 0x9AF8A8` (Perform); `0x00A408C8 bl 0x4D679C` (sem_wait); `0x00A408CC ldrb r3,[r4,#4]; 0x00A408D4 beq 0xA408BC` | M6-017 (audio thread 0xA4087C) | IMPLEMENTATION_GAP |
| E5 | RenderAudio veneer | `0x0099F130` is the public RenderAudio: it loads the engine global and tail-branches to `0x9AFD10` with argument 1. | `0x0099F130 ldr r3,[pc,#0xc]; 0x0099F134 mov r1,r0; 0x0099F13C ldr r0,[r3,#8]; 0x0099F140 b 0x9AFD10` | M6-017 (RenderAudio 0x9AFD10) | IMPLEMENTATION_GAP |
| E6 | RenderAudio | `0x9AFD10(engine, 1)`: if the message ring is non-empty, enqueue a type-4 message, wait for the in-flight counter to drain, and ++ a counter; then, if `1 <= thread-active flag`, `sem_post(engine+0x54)` and return, else `Perform(engine)`. | `0x009AFD50 bl 0x9AF778` (type 4); `0x009AFDD4 ldrb r3,[r3,#0x3d]; 0x009AFDDC blo 0x9AFDF0`; `0x009AFDE0 add r0,r5,#0x54; 0x009AFDE4 bl 0x00A40924`; `0x009AFDF4 bl 0x9AF8A8` | M6-017 (RenderAudio 0x9AFD10) | IMPLEMENTATION_GAP |
| E7 | Semaphore post | `FUN_00A40924` is the signal helper: if the thread flag is 0 it does nothing, else `sem_post(sem)`. The sink posts the same semaphore. | `0x00A40924` (decomp 00a40924.c:11-15); sink `0x009E9420:0x009E9440 bl 0x00A40924 (engine+0x54)`; `0x009EBE6C` posts `iRam0109d870+0x54` | NEW | EXACT_SOURCE |
| E8 | Perform | `0x9AF8A8(engine)`: locks the engine mutex, computes the frame budget from the clock and `+0x70`, then loops: `0x9ADFD8(engine, 0, &flag)` (message pump + render body), `0x9A9F88(engine)` (pending-action drain), the render group (`0xA36AC4`, `0x9FF308`, `0x9D3C98`, `0x9E6D2C`, `0xA57FF8`, `0xA38420`), then `+0x4C++` (tick). | `0x9AF8A8`; `0x009AFA08 bl 0x9ADFD8`; `0x009AFA20 bl 0x9A9F88`; render group `0x009AFA64..0x009AFA94`; tick `0x009AFAA0..0x009AFAA8` | M6-017 (Perform order) | IMPLEMENTATION_GAP |
| E9 | Frame body / message pump | `0x9ADFD8(engine, 0, &flag)` is the message pump plus render: the type jump table at `0x9AE0B0` covers 0..0x37; type 1 -> `0x9AF244` -> `ExecuteEvent 0x009AA3DC`. | `0x009ADFD8`; `0x009AE0B0 addls pc,pc,r3,lsl#2`; case 1 `0x009AE0BC b 0x9AF244`; `0x009AF244` | M6-006 (message pump / ExecuteEvent) | IMPLEMENTATION_GAP |
| E10 | Pending-action drain | `0x9A9F88` walks the pending-action list whose launch tick <= `engine+0x4C` and executes each (Play/Stop/Seek), firing `EndOfEvent`/callback bookkeeping. | `0x009A9F88` (decomp 009a9f88.c) | M6-006 (EnqueueOrExecute / drain) | IMPLEMENTATION_GAP |
| E11 | Sink pacing | On Android the OpenSL sink requests a frame and posts the semaphore; the engine never paces itself. The sink's ring size/pacing is phone hardware. | sink posts at `0x009E9420`, `0x009EBE6C`; inventory gapB T2 | M6-017 (sink caller input) / M6-018 | HARDWARE_ONLY (sink pacing) |
| E12 | Frames per Perform | `Perform`'s frame count comes from the clock/frames calculation (`0x9D4778 -> 0x9EBE6C(0)` or the carried fraction at `+0x70`); the writer of the gating flag is unread. | inventory gapD D1.7; `0x009AF92C..0x009AF9B8` | M6-017 (RECOVERABLE_GAP) | RECOVERABLE_GAP |

### F. Sound banks and scenes

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| F1 | Engine setup | `CozmoAudioController` ctor calls `AudioEngineController::InitializeAudioEngine`, `SetupPlugins`, then builds the bank list `Init.bnk, Music.bnk, UI.bnk, SFX.bnk, Cozmo.bnk, Dev_Debug.bnk` and the scene `InitScene`, and calls `RegisterAudioScene` + `LoadAudioScene`. | `0x00592BB0`; `0x005933CC blx 0x4AF0F4`; `0x00593478 blx 0x4AF10C`; bank strings decomp 00592bb0.c:590-646; `0x005935E2 blx 0x4AF124`; `0x005935EA blx 0x4AF130` | NEW (loading call sites); bank parsing M6-001 | EXACT_SOURCE (call sites) |
| F2 | Load scene/bank | `LoadAudioScene 0x008D2EE8` -> `LoadSoundbank 0x008D2FE4`; `AddZipFiles 0x008D1E3E` feeds archives. The banks live in the OBB (`AudioAssets.zip`). | `0x008D2EE8` (LoadAudioScene, decomp 008d2ee8.c:37 `iVar2 = LoadSoundbank(this,pbVar7)`); `0x008D1E3E` | M6-001 / M6-018 | IMPLEMENTATION_GAP |
| F3 | App side | The Unity app has no bank-load call; it ships the banks in the OBB and the engine's `CozmoAudioController` loads them at construction. | no `.bnk`/`LoadSoundbank` reference under `unity/scripts/csharp/` | NEW | EXACT_SOURCE |

## What the stack must call to run the engine's path (interface list, no implementation advice)

Native entry points (the original's production path):
1. App-input: `AudioUnityInput::HandleGameEvents` `0x00591968` (tag 1) -> vtable slot +0xc =
   `AudioMuxInput::HandleMessage(PostAudioEvent)` `0x008DFC4C` -> `AudioMultiplexer::ProcessMessage`
   `0x008DED14` -> `AudioEngineController::PostAudioEvent` `0x008D1F20` -> `FUN_008D8CE4`
   `0x008D8CE4` -> Wwise `PostEvent` `0x009A6704`.
2. Engine tick: `AudioMultiplexer::UpdateAudioController` `0x008DF3DA` ->
   `AudioEngineController::Update` `0x008D2928` -> `FUN_008D88C0` `0x008D88C0` -> RenderAudio veneer
   `0x0099F130` -> `0x9AFD10(engine, 1)`.
3. Robot-audio pump: `RobotAudioClient::ProcessEvents` `0x00599FE2` ->
   `AudioEngineController::ProcessAudioQueue` `0x008D2946` -> `FUN_008D88C0` -> RenderAudio.
4. Audio thread: `SoundEngine::Init` `0x0099E3EC` -> `FUN_009B0200` `0x009B0200` ->
   `FUN_00A40940` `0x00A40940` -> `pthread_create(entry 0x00A4087C, sem engine+0x54)`;
   `0x00A4087C` loops `Perform 0x009AF8A8` then `sem_wait`; `0x00A40924` is `sem_post`.
5. Per-frame render: `0x009AF8A8(engine)` -> `0x9ADFD8(engine, 0, &flag)` (message pump +
   render) -> `0x9A9F88(engine)` (pending-action drain) -> the render group
   `0xA36AC4/0x9FF308/0x9D3C98/0x9E6D2C/0xA57FF8/0xA38420` -> `+0x4C++`.
6. Bank feed: `CozmoAudioController` ctor `0x00592BB0` -> `InitializeAudioEngine` `0x008D1D1E`,
   `LoadAudioScene` `0x008D2EE8` -> `LoadSoundbank` `0x008D2FE4`, `AddZipFiles` `0x008D1E3E`.

Existing stack seams the build job must satisfy (read from `cozmo-stack/src/`, not evidence of the
original):
- `WwiseRobotAudioPath` (M6-016): `IWwiseRobotAudioHost` (routing + buffer lookup),
  `IWwiseRobotAudioEngine` (`PostEvent`, `SetEventVolume`, `RenderAudio`, `StopAll`),
  `IWwiseRobotAudioBuffer`, `IWwiseDispatchFactory`/`IWwiseDispatchQueue`,
  `WwiseRobotAudioRefSelector`, `IWwiseRobotAudioCallback`.
- `WwiseFrameDriver` (M6-017): `IWwiseAudioSink.HasRoomForFrame`, `IWwiseFrameSource.FramesToRender`,
  `IWwiseFrameRender` (`RenderBuses`, `RunLEngine`, `FlushPbiNotifications`), and the M6-006
  `WwiseEventRuntime` (`PumpMessages`, `DrainDueActions`, `AdvanceFrame`).
- The engine tick and the app-input dispatch (B/C above) have no stack seam yet; the `RenderAudio`
  seam in `IWwiseRobotAudioEngine` is the one that must reach `0x0099F130`/`0x9AFD10`.

## Existing records contradicted by the source

- None found. The M6 inventory's A10 (`ProcessEvents = RenderAudio(true) 0x0099F130`) and M6-017's
  `Perform 0x9AF8A8 / audio thread 0xA4087C / RenderAudio 0x9AFD10` all match the instructions.
- The inventory's A12 "the actual rate is phone-dependent: HARDWARE_ONLY" is consistent; M6-018's
  48000 Hz is a forced policy, not a source claim.

## Existing records whose evidence is too weak to keep their status (or whose path is incomplete)

- **M6-017 (IMPLEMENTATION_GAP):** its evidence names the audio thread `0xA4087C`, `Perform` and
  `RenderAudio`, but not the thread's start (`SoundEngine::Init 0x0099E3EC` -> `FUN_009B0200` ->
  `FUN_00A40940` -> `pthread_create`), the semaphore at engine+0x54, or `sem_post 0x00A40924`. Its
  `unresolved` calls the sink a caller input, which is fair, but the thread-start path is part of the
  record's own production path and has no record of its own. Per the manager's "a settled record owns
  its whole production path" rule, a new record for the thread start/semaphore is needed before
  M6-017 can be settled. It is IMPLEMENTATION_GAP today, so no status is yet misleading.
- **M6-016 (IMPLEMENTATION_GAP):** its evidence's "audio-thread scheduling RECOVERABLE_GAP" is now
  partly read (E1-E7). The record's A6 lambda call to `ProcessEvents` is confirmed at
  `0x0059786E`/`0x0059829E`, and A10's `0x00599FE2 -> 0x008D2946 -> 0x008D88C0` is confirmed. The
  record's routing (A11), states (A19/A20), and abort (A23) were not re-read here.
- **M6-018 (IMPLEMENTATION_GAP, forced policy):** the original mix rate is `min(native, 48000)`; the
  stack's 48000 is a policy. The evidence is fine; no change.

## Open questions the manager must decide or send back

1. **How the stack drives the audio thread.** The original starts a real pthread
   (`FUN_00A40940`/`0x00A4087C`) and the OpenSL sink posts the semaphore. The stack has no phone
   sink. The M6-017 `IWwiseAudioSink`/`IWwiseFrameSource` seams ask the caller; the manager must
   decide whether the stack runs a thread or calls `Perform`/`RenderAudio` synchronously, and whether
   that is a COMPATIBILITY_POLICY (no sink) or a build of the original model.
2. **Frames per Perform (D1.7).** The count computation and the writer of the gating flag are still
   unread (M6-017's RECOVERABLE_GAP). The build job cannot pin the frame count from source yet.
3. **The audio-thread-active flag (0x0108D949).** Set during `SoundEngine::Init`; whether it comes
   from the default branch (`0x0099EAD8`) or from the `GetDefaultInitSettings` copy into the settings
   struct is not settled. This affects when `RenderAudio` signals vs renders synchronously.
4. **The app-input dispatch (B1-B5) has no record.** The general (non-animation) audio path
   `AudioUnityInput -> AudioMuxInput -> AudioMultiplexer -> AudioEngineController` is primary source
   but is not in any M6 record; M6-016 only covers `RobotAudioClient::PostCozmoEvent`. It needs its
   own records before the M6 build job can wire the app's SFX/VO/UI path.
5. **The engine tick call site (C1) has no record.** `CozmoEngine::Update -> UpdateAudioController`
   is the engine's per-frame pump; M6-016/M6-017 describe the audio thread and the robot path but not
   this caller. It needs a record (or an explicit note that M6-016 A10 owns it).
6. **Bank/scene loading call sites (F1-F3) have no record.** `CozmoAudioController`'s six-bank list
   and `InitScene` are source-backed but not in the frozen M6 inventory.

## UNKNOWN / not settled

- The exact frames-per-Perform count and the gating-flag writer (D1.7) - tried: decomp of `0x9AF8A8`
  and `0x9EBE6C`; the inventory itself records them unread.
- Whether the thread-active flag is set from the default branch or the settings copy in
  `SoundEngine::Init` - tried: disassembly of `0x0099E3EC` (default branch `0x0099EAD8`) and the
  `FUN_008D81B0` call (`param_6` is a local settings struct, so the memcpy branch is taken); the
  settings' byte at +0x3c was not traced to `GetDefaultInitSettings 0x0099DC68`.
- The OpenSL sink's ring size and pacing (gapB T2) - phone hardware, HARDWARE_ONLY.
