# BOUNCER-1: procedural face manager lifetime across robot removal

Manager request: the additional concrete UNKNOWN sent to the same BOUNCER-1 source worker, concerning the retained `_PROCEDURAL_` frame, robot removal and named-frame consumers. Requested model/settings: gpt-6.1-sol / medium; actual backend not observed. Read-only native extraction; only this report is written. No code, official records, git, builds, .NET, hardware, network or other agents.

## Answer and scope

**The checked native engine keeps the FaceAnimationManager singleton, including its `_PROCEDURAL_` frame, across ordinary RobotManager::RemoveRobot and subsequent robot construction. Robot destruction is not manager destruction.** The singleton resides at `.bss 0x0105AB00`, reached through the single dynamic GOT relocation at `0x0103E8D8`. Its lazy producers preserve a non-null instance. The engine contains an explicit `removeInstance()` at `0x0058001C`, which destroys the map, deletes the manager and zeros that global, but the bounded binary reference census finds no engine caller of it. This agrees with the earlier, now narrowly checked F20 in `20261012-m5-x9-extraction.md:64`: “removeInstance (0x0058001C) has no caller, so the map lives as long as the process.”

This state can affect reproduced behavior. A later FaceAnimationKeyFrame bearing the same name reads the existing frame count and frame bytes from this singleton, independently of the Robot that originally submitted the image. The frame can become a robot `AnimKeyFrame::FaceImage` message before another SetFaceAction replaces it. No automatic output is implied merely by retention: a later named keyframe must actually be streamed. The normal subsequent SetFaceAction itself clears then replaces the entry before constructing its own named keyframe.

The exact boundary is the ordinary engine robot-removal/reconnection path within one native process. A host operation called `DestroyRobot` or `AnimProcessRobotDisconnect` must not be equated with `FaceAnimationManager::removeInstance` by its name: neither is a native symbol in this binary under those names. Native RemoveRobot invokes Robot::~Robot at `0x0052F2F6`. Process termination, an explicit external invocation of the exported removeInstance symbol, and a complete app reload are distinct boundaries. The latter remains the existing M5-052 uncertainty; this report does not audit app reload activation or another binary's dynamic symbol calls.

## Narrow question before tracing

Does the old entry touch robot traffic, robot behavior, API delivery/readiness, or downstream state? **Yes, conditionally on a later named keyframe.** IsDone uses its count (`0x004F979A..0x004F97AC`), GetStreamMessage uses its bytes (`0x004F9808..0x004F984A`), and the latter constructs an EngineToRobot FaceImage. Therefore retention cannot be dismissed as an internal cache with no observable consumer.

The C# public surface was read solely to identify the question, never as original-engine evidence: `CozmoAnimations.FaceAnimations` is a public FaceAnimationLibrary property (`CozmoAnimations.cs:428`); `FaceAnimationLibrary.Variants(string)` returns the stored pair list (`FaceAnimationLibrary.cs:153`). The scheduler is connected to that same name lookup at `CozmoAnimations.cs:177`, consumed by FramesOf at `AnimationScheduler.cs:1780..1788`. This is a stack exposure of the engine's native stored-frame state. Native GetFrame returns one selected variant at a time, not a public API returning both pairs together. This extraction establishes the native retained content, count and selected-frame contract, not a claim that the original app had an identical `Variants` method. No native robot-removal event clears that content before these named reads.

## Current owning claims, verbatim fields

The following are copied from the current manifest in this worktree at report creation. They are claims checked here, not native authority. No field is changed.

### M1-025

```json
{
  "id": "M1-025",
  "title": "Connect request from the game, the connected response, and DisconnectCurrent",
  "status": "EXACT_SOURCE",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "location": "cozmo-stack/src/Cozmo.Robot/CozmoEngine.cs",
  "evidence": [
    "B2 ConnectToRobot handler: robot 1 exists -> \"Robot already connected\", nothing (0x004ED022, 0x004ED026); else AddRobotConnection (0x004ED074), then AddRobot(1) at once (0x004ED07C)",
    "B23 HandleConnectionResponseMessage: state 1 -> 2 and reason 0 (0x0062F954, 0x0062F958, 0x0062F95E); otherwise \"Got connection response at unexpected time\"",
    "B33 DisconnectCurrent: RT->Disconnect then QueueConnectionDisconnect (0x0062EF38..0x0062EF58); callers RCM dtor 0x0062EE98, fatal robotError 0x0069DACA, MessageHandler::Disconnect 0x0069DCF2, ExitSdkMode lambda 0x0069E1EC..0x0069E1FA [corrected C7]",
    "B35 disconnect reasons: SleepPlacedOnCharger 4 (0x00606D1A), SetRobotDisconnectReason writes the byte (0x0069E01C)",
    "CB2/CB6: ConnectToRobot logs \"Connected to robot!\" before any transport exchange, then NeedsManager::InitAfterConnection and DASPauseUploadingToServer(1); nothing goes to the game (0x004ED074..0x004ED114)",
    "CC18/CC19: RobotDisconnectReason values 0..11 (0x00775BEC, table 0x01033970) and every writer of RCM+0x38; its only reader is the DAS disconnect event (0x0062FAAA), so it changes no behaviour",
    "CC27..CC30: a later ConnectToRobot builds everything afresh once robot 1 is gone; DisconnectCurrent queues type 3 + DeleteConnection and pushes one OnDisconnected marker, handled at the next RCM::Update in FIFO order (0x0062EF32..0x0062EF7A; 0x0062F3BC..0x0062F4BC)",
    "CB38: the app layer does not filter connection events by address; only data is compared against the robot address (0x0062F434; 0x0062F72C..0x0062F748)",
    "E7..E9: ExitSdkMode subscribers run UiMessageHandler, then MessageHandler (reason 6, DisconnectCurrent), then MovementComponent; an EnableAnimTracks it sends is posted behind the Disconnect action, finds no connection and is not sent (0x00660918..0x0063DE96; 0x0069E1DC..0x0069E1FE; CA12, CA14)"
  ],
  "effect": "a second connect, a response at the wrong time, or a local disconnect is handled differently",
  "unresolved": "M1 retains outer ExitSdkMode/disconnect order and gates, including external-exit charger unlock attempt after disconnect is queued (E17 regression). E1-E5 -> M8-018, M7-024, M15-031, M3-039, M4-029/-030 and M11-056; E8 -> M15-032; E9 -> M4-031. E6 remains M1-051; E7 remains M1-052. Exceptions follow approved M1-034. Sources: 20261006-M1M2-missing-triage.md E1-E9; 0x00660918..0x0063DE96; 0x0069E1DC..0x0069E1FE. OV13 check (re-analysis/research/overnight/20261014-ov13-m11-055-check.md): the unbounded vision join (0x0065257C) waits for any in-flight UpdateVisionSystem plus at most one 2 ms sleep; the worker logs Info \"VisionComponent.Processor\" \"Terminated Robot VisionComponent::Processor thread\" before the join returns (0x00651F08..0x006522A8).\nbuilt, awaiting strong verification: B-M11-1 (2026-10-14): Host constructed reset clears new dynamic world origin/root fixtures and diagnostic tail-call counter. Recursive native teardown remains M11-055/077; regression retains full as-constructed comparison. See jobs/status/B-M11-1.md for files/lines, binary ranges, tests and every MISSING.",
  "live_path": true,
  "hardware_required": false
}
```

### M5-013

```json
{
  "id": "M5-013",
  "title": "faceAnimations: one sprite frame per stream frame, empty frames skipped, two RLE variants per image chosen by _firstScanLine, index reset on abort",
  "status": "EXACT_SOURCE",
  "authority": "libcozmoEngine.so 3.4.0-1204; libopencv_imgproc.so 3.1.0 (shipped)",
  "location": "cozmo-stack/src/Cozmo.Robot/Animation/FaceAnimationLibrary.cs",
  "evidence": [
    "C12 0x004F9708..0x004F98C4; FaceAnimationManager 0x00581254..0x005812C4; GetFrame 0x005817B4..0x005817CA",
    "M3 B4",
    "R-ANIM pre-extraction part 1 item 5 5.1..5.8: Threshold 0x00871E98, libopencv_core 0x7A7A2, variant store 0x00581254, GetFrame 0x005817B4, GetStreamMessage 0x004F9812, IsDone 0x004F9770",
    "re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-013 (Opus settling pass, manager-adopted 2026-10-11)."
  ],
  "effect": "sprite faces look different",
  "unresolved": "Settled EXACT_SOURCE 2026-10-19 (re-analysis/research/20261019-m5-settling-a.md, manager-adopted): +0x28 override zero in production (M5-058). Superseded below: B-LOG-1 and \"CompressRLE != 0 unread\" belong to BufferFaceToSend; CompressRLE 0x00581904 errors only off 64x128 (0x00581916..0x0058191C), unreachable from DrawFace. Previous notes: Settling pass 2026-10-11: the FlatBuffer/JSON +0x28 add path is split out to M5-043. The threshold is strictly above 0x80, the variant and lazy index reset are settled, and an unknown name (0 frames) is done at once (C5). The writers of keyframe +0x28 (the IsDone override) are RECOVERABLE_GAP (0x004F9770, 0x004F97DE). B-M5-8 (2026-10-12), built, awaiting strong verification: ordinal case-sensitive name dictionaries and consumed zero-frame misses (0x005816D8, 0x00581790, 0x0058173A). Face-file loader recovery remains X-M5-9. B-M5-9 (2026-10-12), built, awaiting strong verification: typed GetNumFrames warning on exact map miss: Unknown animation requested: %s; lookup/consumption remains M5-013, loader is M5-052. B-M5-10 (2026-10-12), built, awaiting strong verification: Each GetNumFrames miss emits GetAnimationByName.UnknownName then GetNumFrames warnings, repeated by GetStreamMessage IsDone, in native order (0x005813B4, 0x005816DE, 0x00581714, 0x004F97A0, 0x004F97D4; vtable relocation 0x0101F0C0). The override gate still bypasses GetNumFrames. Second M5 pass: the +0x28 writer gap is M5-058. R-M5-TEARDOWN: the +0x28 override is natively always zero (M5-058), so IsDone/GetStreamMessage always go through GetNumFrames/GetFrame. Log-key check (2026-10-16, re-analysis/research/20261016-logkey-sites-check.md L3): BufferFaceToSend error is native sErrorF 0x0057C282 \"ProceduralFaceKeyFrame.GetStreamMessageHelper\" \"Failed to get RLE frame from procedural face.\" then _errG; stack key/text wrong. Fix: B-LOG-1. Condition equivalence with CompressRLE != 0 (0x0057C216) unread.",
  "live_path": true,
  "hardware_required": false
}
```

### M5-052

```json
{
  "id": "M5-052",
  "title": "Face-animation loader: dir walk, mtime gate, reserved name, sorted frames, name/stoi/negative errors that abort the animation, gap fill, grayscale 128x64 gate",
  "status": "EXACT_SOURCE",
  "authority": "libcozmoEngine.so 3.4.0-1204.",
  "location": "cozmo-stack/src/Cozmo.Robot/Animation/FaceAnimationLibrary.cs",
  "evidence": [
    "re-analysis/research/20261012-m5-x9-extraction.md section 3, F1..F20: ReadFaceAnimationDir 0x00580048 (walk 0x005800B0, reserved name 0x005801AE..0x0058040E, stat 0x005802B6..0x005804B2, mtime 0x005802FC..0x005805A8), LoadAnimationImageFrames 0x00580834 (sort 0x00580920, name checks 0x00580E34..0x00580F6E, stoi 0x00580A0E..0x00580A1C with LSDA pad 0x00580DB0, negative 0x00580B06..0x00580B6A, gap fill 0x00580A2C..0x00580AE4, imread grayscale 0x00580BE6..0x00580C16, size gate 0x00580C1A..0x00580CF8, threshold 0x00580C2C); eager at 0x0051F3CE.",
    "re-analysis/research/20261012-m5-052-face-png.md (Codex research, manager spot-checked 2026-10-12: sbfx at imgcodecs 0x0005664E, libpng 1.6.19 at 0x000C27F9, R/G doubles at 0x00017680, setters at 0x000175DE/0x00017636): imread(path, 0) through the statically linked libpng 1.6.19 in libopencv_imgcodecs; all 552 shipped face PNGs are 128x64 colour type 0, non-interlaced, IHDR/IDAT/IEND only (510 at 8-bit, 42 at 1-bit); a 1-bit sample decodes to 0 or 255 (0x00056610..0x00056656); an 8-bit gray sample is unchanged (no gamma, sBIT or scaling); PLTE in a gray file is ignored, alpha is stripped without compositing; the RGB/indexed branch is (9797R+19234G+3737B)>>15, truncating (0x000581AA..0x000581E8), not reached by shipped files."
  ],
  "effect": "Which face frames exist, in what order, and the errors logged while loading them.",
  "unresolved": "Settled EXACT_SOURCE 2026-10-19 (re-analysis/research/20261019-m5-settling-a.md, manager-adopted): Every shipped face file decodes as R-M5-052 says (two directories, 151 and 401 files, name_NNNNN.png from 0). F4 readdir order has no observable effect (frames keyed by name); F20 moot. Not supported: non-shipped PNG colour types/depths/interlace and non-canonical stoi frame numbers, inputs our system never produces (shipped files only), decided by the manager 2026-10-19. Previous notes: Build per F1..F20; replaces the stack lazy, case-insensitive, unchecked loading. HARDWARE_ONLY: directory readdir order (F4). RECOVERABLE_GAP: libopencv PNG decode (F15) and libc++ stoi parsing rules (F12). F12 out_of_range (terminate) is under M1-034 (operator, 2026-10-12). Whether the app loads twice in one process (F20) is UNKNOWN. B-M5-9 (2026-10-12), built, awaiting strong verification: eager face loading after backpack, directory/mtime/reserved gates, byte-wise UTF8 filename order and all DT_REG extensions, abort-animation name/negative/invalid-argument sites, out_of_range file isolation, n-1 gap fill and unconditional append, grayscale size gate with failure continuation, threshold/RLE pairs; host decoder accepts shipped 1/8-bit grayscale; libopencv decode and libc++ stoi noncanonical parsing remain MISSING; repeat app-load activation/process-singleton boundary remains UNKNOWN. R-M5-052 (2026-10-12): F15 is resolved for every shipped face file (1-bit -> 0/255, 8-bit identity); the stack decoder must give those values. Only malformed PNGs and gamma-bearing or 16-bit RGB (no shipped file) stay outside; zlib inflate is the phone library (imported, not shipped). B-M5-10 (2026-10-12), built, awaiting strong verification: A host PNG decoder refusal emits only stack MISSING and skips that frame, rather than inventing a native 0x0 size error. Shipped grayscale support retained; colour/16-bit/interlaced PNG decode remains RECOVERABLE_GAP, no shipped file reaches it (imread 0x00580BE6..0x00580C16, size gate 0x00580C1A..0x00580CF8).\nB-M5-23 (2026-10-19), built, awaiting strong verification: Removed the unconditional startup MISSING over-report covered by this record's existing exclusion. Decoder/PNG/stoi behavior unchanged. Loader-order test now observes the actual deferred backpack load log with the resource root configured and face container absent then, followed by completed decoded face output; it no longer relies on a MISSING as a face-stage event. Asset suite requires local OBB and known clips instead of returning success without data.",
  "live_path": true,
  "hardware_required": false
}
```

### M5-037

```json
{
  "id": "M5-037",
  "title": "Driving-animation and AnimationStreamer teardown",
  "status": "EXACT_SOURCE",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "location": "cozmo-stack/src/Cozmo.Robot/Animation/CozmoAnimations.cs",
  "evidence": [
    "0x00511524..0x0051154A; 0x0057AF48..0x0057AFDA; 0x0057B044..0x0057B0C2; Destroy driving subscriptions/vector and streamer; ClearSendBuffer discards without send. TrackLayer/live/shared descendant effects UNKNOWN; no final flush inferred.",
    "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt",
    "re-analysis/research/20261011-m5-settling-pass.md section 1, row M5-037 (Opus settling pass, manager-adopted 2026-10-11).",
    "re-analysis/research/20261012-m5-x9-extraction.md section 4a: the descendant effects are read; the teardown order is M5-053."
  ],
  "effect": "A removed Robot or queued sleep action retains state, emits different messages or invokes callbacks in a different order.",
  "unresolved": "Settled EXACT_SOURCE 2026-10-19 (re-analysis/research/20261019-m5-settling-b.md, manager-adopted): ~AnimationStreamer 0x0057AF58..0x0057AFD4 re-read; matches Destroy(). Superseded below: the B-M5-1 handle-ownership MISSINGs (B-M5-13/15). Previous notes: built, awaiting strong verification: B-M5-1: bind streamer destructor in robot lifetime, drop buffered messages with TD8 debug then clear managed owned storage; release host waiters without sends/end callbacks; MISSING native driving handle-vector/+8 ownership 0x00511524..0x00511542 and streamer SetupHandlers handle ownership 0x0057AF6C. Previous notes: IMPLEMENTATION_GAP (ruling 4.4): build the teardown order per re-analysis/research/20261011-m5-settling-pass.md row M5-037 (no sends). ~TrackLayerComponent internals and the +0xC object are split out to M5-044. X-M5-9: the driving-handler and streamer handler release order is owned by M5-053. B-M5-9 (2026-10-12), built, awaiting strong verification: driving +0x24C and streamer +0x60 lifetime points now call ordered ownership-release interfaces; handler vectors owned by M5-053, parameter last release M5-044; concrete missing recipients still visible. Second M5 pass: the ~CozmoEngine order is M5-055. B-M5-13 (2026-10-12), built, awaiting strong verification: Default driving and streamer ownership recipients are concrete UI nodes; live Robot lifetime releases driving B6/B5/E2G5A before streamer C4/AD/AF/AE, with hub alive at engine shutdown. The former engine-order/factory MISSINGs are resolved by M5-055/056. Driving +8 animation-vector and subscription callback semantics still lack checked concrete recipients and remain MISSING. B-M3M4-12 (2026-10-12), built, awaiting strong verification: Same ClearSendBuffer lock cleanup; stream destruction still drops pending messages without sends, with source debug order preserved. Read 2026-10-12 (re-analysis/research/20261012-m5-teardown-members.md): the driving +8 vector is a plain vector of {DrivingAnimations, lockName} pairs; its destructor 0x004F090C..0x004F0942 frees strings only: no recipient, so its MISSING closes as no effect. 0x5A is an E2G subscription (slot +0x28); B5/B6 are G2E. The callback bodies are M5-063; AD is M5-061; B4 is M5-062; AE/AF are M5-027/M5-057; C4 is M9-029. B-M5-15 (2026-10-12), built, awaiting strong verification: Removed obsolete driving +8 recipient MISSING; native vector retirement has no effect. Other owners named by tag.",
  "live_path": true,
  "hardware_required": false
}
```

### M5-044

```json
{
  "id": "M5-044",
  "title": "~TrackLayerComponent internals and the AnimationStreamer +0xC shared object at teardown",
  "status": "EXACT_SOURCE",
  "authority": "libcozmoEngine.so 3.4.0-1204.",
  "location": "cozmo-stack/src/Cozmo.Robot/Animation/CozmoAnimations.cs",
  "evidence": [
    "re-analysis/research/20261011-m5-settling-pass.md section 1 row M5-037: TLC delete 0x0057AF8C..0x0057AF98; shared_ptr +0xC release 0x0057AFD0.",
    "re-analysis/research/20261011-m5-settling-pass.md section 5 and re-analysis/research/20261011-m5-open-reads.md (Codex rows, Opus spot-check PASS 2026-10-11): TD1-TD8, TDU: ~TrackLayerComponent 0x0064ED8C frees +0x10, +0xC, +8, +4 in order, no send or log; +0xC is the UiMessageHandler subscription container (TD3-TD6, 0x004EF184); libc++_shared.so __release_shared 0x5ED6C, __release_weak 0x5EDC2, lock 0x5EDE8.",
    "re-analysis/research/20261012-m5-x9-extraction.md section 4b: libc++_shared __release_shared 0x5ED6C (on_zero_shared then weak release); 0x0057AFD4 is the last owner. Supersedes TDU \"external, unsettled\"."
  ],
  "effect": "What the streamer teardown releases beyond M5-037's read order.",
  "unresolved": "Settled EXACT_SOURCE 2026-10-19 (re-analysis/research/20261019-m5-settling-b.md, manager-adopted): TLC delete, parameter maps, then the +0xC release last (0x0057AFD4) match. Superseded below: the B-M5-1 MISSINGs. Previous notes: built, awaiting strong verification: B-M5-1: managed TLC release order face then face/backpack/audio storage, parameter maps last; MISSING concrete tag-B4 shared subscription release 0x0057AFD4 and exact native handle ownership; no final send/callback invented. Previous notes: IMPLEMENTATION_GAP: build the teardown order (no sends, no logs) together with M5-037. X-M5-9: the release is read (section 4b); RECOVERABLE_GAP remains the ~CozmoEngine member order. B-M5-9 (2026-10-12), built, awaiting strong verification: SetupHandlers releases precede live storage; the parameter subscription is released last after parameter maps through its sole-owner interface; shipped libc++ shared/weak release targets reopened; ~CozmoEngine weak-signal lifetime and concrete host recipients remain MISSING. Second M5 pass: the ~CozmoEngine order is M5-055. Second M5 pass: the E2G container factory is M5-056. B-M5-13 (2026-10-12), built, awaiting strong verification: The default tag-B4 subscription now has a concrete G2E node and last-owner release after streamer parameter storage. Engine shutdown keeps the signal alive until this unlink, then destroys the hub. No E2G factory/order MISSING remains; the payload callback recipient is still MISSING. Read 2026-10-12 (re-analysis/research/20261012-m5-teardown-members.md): the B4 recipient is HandleSetParameters 0x0057E860 (M5-062). B-M5-15 (2026-10-12), built, awaiting strong verification: B4 callback concrete under M5-062; AE/AF missing owners M5-027/057, AD M5-061, C4 M9-029.",
  "live_path": true,
  "hardware_required": false
}
```

## Native step inventory

“Established” below means a native fact submitted for manager checking, not a manifest status change. Candidate classification for these narrowly read facts is EXACT_SOURCE. Shared accepted behavior is cited without re-extracting the streamer or raster layers.

| Step | Native behavior / exact interface | Primary citation | Ownership / result |
|---|---|---|---|
| L1 | Singleton pointer is process-global storage, not a Robot member. A non-null pointer is reused. | Dynamic symbol `_singletonInstance = 0x0105AB00`, size 4; its only dynamic relocation is GOT `0x0103E8D8`. Loader `0x00520368..0x00520370`: resolve global, load r5, `cbnz r5,0x00520386`. | Established; lifecycle extension of M5-052 F20, not M5-037's per-Robot streamer. |
| L2 | If absent, loader allocates 0x14 bytes, constructs the manager and publishes it to the global. There is no per-Robot reinitialization of an existing manager. | `0x00520372 movs r0,#0x14`; `0x00520374 operator new`; `0x0052037A FaceAnimationManager::ctor`; `0x00520384 str r5,[r0]`. | Established; same object on later LoadFaceAnimations. |
| L3 | Constructor creates the map and pre-registers the reserved procedural name. This only runs on a null singleton branch. | Reused checked constructor `0x0057FDE4..0x0057FDFC`; name symbol `0x0105AB04`; map operator[] at `0x0057FDFC`. | Established, reused prior BOUNCER supplement. |
| L4 | Loader passes the existing manager to ReadFaceAnimationDir for assets, false for the normal resource scope. SDK's second scope is separate and unreachable under the standing SDK policy. | `0x00520386 ldr r1,[r4,#4]`; `0x00520388 mov r0,r5`; `0x0052038A movs r2,#0`; `0x0052038C ReadFaceAnimationDir`; SDK gate `0x00520390..0x0052039A`. | Established. No claim about whether app activates loading twice. |
| L5 | Resource directory loading does not treat `_PROCEDURAL_` as an asset to overwrite. It compares against ProceduralAnimName, reports the reserved name and erases that directory from its local directory list. | `0x00580186..0x0058019E` resolve ProceduralAnimName; comparison `0x005801AE..0x00580214`; equal branch to `0x00580372`; literal/log `0x0058037A..0x00580386`; directory-list erase/advance `0x005803BE..0x0058040E`. Reuses M5-052 checked F6. | Established. No manager-wide clear is inserted at loader entry (`0x00580048..0x005800BE`). |
| L6 | SetFaceAction reuses the singleton if present, clears the procedural entry and adds its new image before publishing a procedural keyframe. | Reused frozen J1/J2: global lazy branch `0x00563E00..0x00563E1C`; `0x00563E28 ClearAnimation`; `0x00563E38 AddImage`; keyframe `0x00563E48..0x00563E52`. | Established. Old frame survives until this explicit replacement (or explicit removeInstance/process end), not forever after a new SetFace. |
| L7 | RobotManager removal disconnects services, destroys the Robot, frees its storage and erases membership. No manager reset is present in RemoveRobot. | `0x0052F2DC..0x0052F2F0` disconnect services; `0x0052F2F2 ldr r0,[r5,#0x14]`; `0x0052F2F6 Robot::~Robot`; `0x0052F2FA operator delete`; `0x0052F2FE..0x0052F302` erase robot tree; remainder through `0x0052F364` membership/global-label cleanup. | Established. Complete pointer-reference census below bounds the indirect destruction concern. |
| L8 | Robot/streamer teardown is not singleton-map teardown. The streamer's shared +0xC object is the UiMessageHandler subscription container. | Reused accepted M5-037 / M5-044 citations: streamer `0x0057AF48..0x0057AFDA`; subscription shared release `0x0057AFD0`; TLC `0x0064ED8C`. No manager global is referenced in these routines, per bounded singleton census. | Established interface distinction, no full teardown re-audit. |
| L9 | Explicit removeInstance is capable of deleting ALL manager entries and zeroing the pointer, but no checked engine caller invokes it. | `0x0058001C..0x00580024`: load global and null guard; `0x00580028`: map destructor; `0x0058002C`: delete; `0x00580032 movs r1,#0`; `0x00580038 str r1,[r0]`. Caller/reference census below. | Established native capability and absent engine linkage; not an automatic disconnect effect. |
| C1 | IsDone dynamically obtains the same singleton; an existing manager is reused. It queries the keyframe's name at +0xC and compares current index +0x24 against frame count. | `0x004F977A..0x004F9798` singleton lazy branch; `0x004F979A add.w r1,r4,#0xC`; `0x004F97A0 GetNumFrames`; `0x004F97A8 cmp r6,r1`; `0x004F97AA..AC` unsigned >= returns true. Native +0x28 override remains the separately settled M5-058 zero production flag. | Established; M5-013 consumer. |
| C2 | GetStreamMessage resolves the current singleton, then queries name +0xC and index +0x24. It does not capture an owning Robot or a manager generation in the keyframe. | `0x004F97EA..0x004F9806` singleton lazy branch; `0x004F9808 add.w r6,r4,#0xC`; `0x004F980C ldr r2,[r4,#0x24]`; `0x004F9812 GetFrame`. | Established; old entry is eligible on this later name-based pull. |
| C3 | GetFrame looks up name in the manager map, checks index against vector count, then returns one RLE vector from that stored pair according to process _firstScanLine. It has no robot-identity/epoch argument or check. | `0x00581798` map find; `0x0058179E` vector begin/end; count/bounds `0x005817A2..0x005817B2`; `0x005817BC` index stride 24; `0x005817C2` load selector; `0x005817C8 addne r2,#0xC`; `0x005817CA` return-tail; `0x00581878 mov r0,r2`. | Established; M5-013 selected-frame contract. |
| C4 | Nonempty returned data is copied to the keyframe cache, index is incremented, and an EngineToRobot FaceImage message is constructed. Empty data merely advances the index and emits no image. | `0x004F9818..0x004F982A` nonempty test/copy; `0x004F982E..0x004F9832` index++; `0x004F9834..0x004F984A` allocate/FaceImage construction; empty branch `0x004F9860..0x004F9866`. Remaining transport is the accepted M5-013 streamer path. | Established observable effect; no newly extracted wire format. |
| U1 | Full app-reload activation and external callers dynamically invoking exported manager removal. | Not established by this bounded engine question. Existing M5-052 explicitly retains its repeat-app-load uncertainty. | UNKNOWN outside the ordinary robot-removal boundary; no guessed reset. |

## Bounded reference and mutation census

The SO's dynamic symbol table identifies the singleton and its sole GOT relocation. I decoded Thumb PC-literal loads (both 16-bit LDR and 32-bit LDR.W), followed their PC-add expressions, and checked the matching live references. The only real references to this GOT address are:

- `0x004F977A / 0x004F977E`, `0x004F9792 / 0x004F9794`: IsDone load / lazy publish.
- `0x004F97EA / 0x004F97EC`, `0x004F9800 / 0x004F9802`: GetStreamMessage load / lazy publish.
- `0x00520368 / 0x0052036A`, `0x0052037E / 0x00520380`, `0x0052039C / 0x0052039E`, `0x005203B2 / 0x005203B4`: LoadFaceAnimations normal and SDK lazy branches.
- `0x00563E00 / 0x00563E02`, `0x00563E16 / 0x00563E18`: SetFaceAction load / lazy publish.
- `0x0058001C / 0x0058001E`, `0x00580030 / 0x00580034`: removeInstance load / null write.

A first crude sliding-window scan also produced `0x005D75B8 / 0x005D75F4`; instruction checking rejected it: r0 is overwritten in between. Its true first PC-add at `0x005D75C0` resolves ObjectID vtable GOT `0x0103E8A4`, and `0x005D75EC` loads another literal for `0x005D75F4`. It is not a manager reference. The corrected scan stops at the first matching-register PC-add and includes LDR.W.

A `.text` halfword-aligned direct Thumb BL/BLX/B.W target census, including the relevant PLT target addresses, found:

- GetNumFrames: `0x004F97A0 -> 0x004A6034` only.
- GetFrame: `0x004F9812 -> 0x004A6040` only.
- ClearAnimation: `0x00563E28 -> 0x004AC4D8` only.
- removeInstance: no caller to `0x0058001C` and no PLT relocation for that function.

A separate word-aligned ARM branch target scan likewise found no branch to removeInstance. Searching stored `0x0058001D` pointers found only the dynamic-symbol-table value (file offset `0x14318`), not an engine callback/vtable pointer. The global address `0x0105AB00` likewise occurs only as its dynamic-symbol-table value (file offset `0x5898`); code uses its GOT reference. There is no source-supported registration of removeInstance with a robot-disconnect callback or global destructor in this binary. These are bounded singleton/mutator/pull checks, not a general teardown audit.

This rules out a hidden normal RemoveRobot descendant clearing the manager through a second singleton access or a registered removeInstance callback. Existing keyframe readers do not expose a mutable manager to teardown; the only pointer handoff that can mutate the object is its checked asset loader and explicit SetFaceAction mutation. The asset loader's reserved-name path preserves `_PROCEDURAL_` as described above. This report does not claim arbitrary external native code cannot invoke an exported function.

## Record interpretation, contradiction check and remaining uncertainty

No contradiction with the five quoted current records was found. In particular:

- M1-025's fresh-connection wording describes construction of robot-owned state; it is not primary evidence that a process singleton is reset. Extending that wording to the manager would contradict the pointer/lifetime facts above.
- M5-013 owns the named-frame consumer and selected variants; its current claim does not say manager storage is robot-owned.
- M5-052's F20 repeat-load uncertainty is not resolved by the mere existence of a loader routine. The previously noted process lifetime is independently corroborated here; full app activation remains outside this scope.
- M5-037/044 own the stated stream/track/subscription destruction, and do not identify FaceAnimationManager as one of the destroyed objects.

No M1-034 undefined/terminating path is necessary for this lifetime result. The old frame's reuse is defined name-based lookup. Retention is not a fault to be silently skipped.

Exact change: this report only. Fidelity-manifest impact: none applied; manager review owns any integration of the checked lifecycle fact. Regression tests / commit: none run or made by the read-only worker. Remaining uncertainty is the full app reload/external exported-call boundary above, not ordinary same-process robot removal or the named frame reader's access to retained bytes. This report gives native behavior and makes no C# implementation recommendation.

Primary artifact SHA-256: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
