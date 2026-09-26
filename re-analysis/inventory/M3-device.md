# M3 device inventory (camera, display, audio device)

**State: approved by the manager on 2026-09-24 under the operator's standing authorisation. Source-derived inventories and ordinary source-fidelity decisions need no operator checkpoint; only a deliberate divergence from the engine, or an unresolved source question that materially affects robot behaviour, goes to the operator. Frozen with `python re-analysis/tools/fidelity.py --approve M3-device`, and re-approved after correction C1.** The forced policies M3-019 and M3-020 are recorded under Decisions. They will be observed in the next hardware run and are not a reason to stop.

## Where this comes from

- **Source:** `libcozmoEngine.so` 3.4.0-1204, freshly disassembled. The decompiled Unity C# and the shipped assets were used as lower authority.
- **Read-only extractor passes:**
  - **M3 pass** (Appendix A): rows A1..A26 (camera), B1..B19 (display), C1..C18 (audio device).
  - **Gap pass** (Appendix B): rows 1a..1q (exposure limits, the connection order, SetCameraParams), 2a..2f (VisionComponent flags), 3a..3b (the colour flag), Q4 (DefaultCameraParams types) and Q5 (audio animation state names).
- **Interfaces used and not redone:**
  - `re-analysis/inventory/M2-protocol.md`: Appendix C (ImageChunk reassembly R1..R10 and handler H1..H5), and Appendix A §2 (the FaceImage and AudioSample builders).
  - `re-analysis/inventory/M1-transport.md`: CD21 (the connection-time NV CameraCalib read) and CD27 (SetAudioVolume).
- **The C# was never evidence.**
- **Hardware context:** bundle `re-analysis/acceptance/hardware/20260924-202748-CONTROL/`.
  - FACE, AUDIO and CAMERA passed on firmware 2457. The operator confirmed the face pattern and the two beeps.
  - In that run the stack sent neither SetCameraParams nor the NV calibration read, and the robot sent no DefaultCameraParams (0xC8).

## How to read the statuses

- **IMPLEMENTATION_GAP:** established from source, and not yet compared against the code. The comparison-and-repair batch settles each one.
- **HARDWARE_ONLY:** only the robot can answer it. The firmware images in the OBB are encrypted (entropy 7.8–8.0 bits/byte), and 2457 is not shipped.
- **COMPATIBILITY_POLICY:** a deliberate or forced choice of this stack.
- **No RECOVERABLE_GAP remains in M3.**

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M3-001 | IMPLEMENTATION_GAP | JPEG reconstruction headers. Gray is 324 bytes with one DQT, a 1-component SOF0 and one DHT. Colour is 334 bytes with a 3-component 4:2:2 SOF0 and a shared DHT. Height and width are written big-endian at 0x5E..0x61. A decoded frame must be 240×320, otherwise BadDecode. The frame timestamp is the last chunk's. | A8, A11, A12, A13 |
| M3-002 | IMPLEMENTATION_GAP | Image chunk reassembly exactly as EncodedImage::AddChunk. Chunks are ignored before SyncTimeAck. The rules cover oversize chunks, a new id, a new id with a bad resolution, order, duplicates, the last chunk, the timestamp order and the edge cases. | A1, A2, M2 App. C R1..R10, H1 |
| M3-003 | IMPLEMENTATION_GAP | MiniToJpegHelper. It strips trailing 0xFF, drops the first (flag) byte, stuffs 0x00 after each 0xFF, and appends FF D9. It skips the copy when fewer than 2 bytes remain. | A12 |
| M3-004 | COMPATIBILITY_POLICY | Warm-up frames are flagged and delivered like any other. The engine has no warm-up discard: its only drops are A1 and A3..A6. | A15 |
| M3-005 | IMPLEMENTATION_GAP | At most 3 completed images per event time are handed on to vision; the 4th and later are dropped with a warning. Only one EncodedImage is assembled at a time. Vision's own gates and its latest-wins mailbox are M11's (A4..A6). | A3, A4..A6 (interface) |
| M3-006 | IMPLEMENTATION_GAP | Face canvas is 64 rows × 128 columns. The wire image is 128 columns × 64 rows, sent as 32 two-row pairs. Each fully sent stream frame advances stream time by 33 ms. | B1, B15, B18 |
| M3-007 | IMPLEMENTATION_GAP | CompressRLE exactly. It needs a 64×128 image. Columns are u64 masks where any non-zero pixel is lit. Opcodes: skip 0b00nnnnnn, repeat 0x40\|k, and run 0x80\|((len−1)<<2)\|pair. A trailing run is always emitted at c==127 or when pair≠0. The raw fallback of 128 LE u64 masks applies when the RLE is **≥1024** bytes. | B6..B12 |
| M3-008 | HARDWARE_ONLY | How the firmware maps pair bits to physical display rows, and the robot's playback period. On the engine side, pair bit0 is row 2k and bit1 is row 2k+1 (B10, B14). | B10, B14, B18 |
| M3-009 | IMPLEMENTATION_GAP | A blank canvas encodes as {0x3F, 0x3F}. | B13 |
| M3-010 | IMPLEMENTATION_GAP | encodeMuLaw(float) exactly. NaN gives 0. The sample is clamped to ±1 and scaled by 32767 with truncation, then goes through the segment table 0x00C5C3F0 into sign, exponent and mantissa. No volume scaling is applied on this path. A frame shorter than 744 samples is zero-padded. | C5, C6, C7 |
| M3-011 | IMPLEMENTATION_GAP | 22320 Hz and 744 samples per frame; 30 Hz is derived from these. | C3 |
| M3-012 | IMPLEMENTATION_GAP | The engine's send budgets. Audio: max(14 − (framesStreamed − framesPlayed), 0). Bytes: min(8192 − (streamed − played), 30000), where a negative value warns and becomes 0. The played counters come only from AnimationState. Streamed counters: bytes += EngineToRobot::Size() for every send, and frames += 1 for 0x8E/0x8F and for EndOfAnimation. The robot's real buffer size is firmware (HARDWARE_ONLY, noted). | C9, C10, C11, C12, C13, C18 |
| M3-013 | IMPLEMENTATION_GAP | The feed. SendBufferedMessages drains in FIFO order and stops at the first message over the byte budget, or at an audio message when the audio budget is 0. Per engine Update, UpdateStream refreshes the budgets, flushes the leftovers, then loops building one 33 ms frame at a time for as long as the drain completes and the audio animation is ready. **It replaces the earlier TargetInFlight 10 / 200 ms / priming model, which is not the engine's.** | C14, C15, C16, Q5 |
| M3-014 | IMPLEMENTATION_GAP | Every streamed message, audio frames and EndOfAnimation included, is sent reliable and not hot. EndOfAnimation is sent directly and is not budget-gated. | C12, C14 |
| M3-015 | IMPLEMENTATION_GAP | Exactly one audio message per stream frame: AudioSample with 744 mu-law bytes, or AudioSilence. The face keyframe message goes first; a procedural face is only built when none is buffered. Several frames can go per engine Update. | B16, C4, C15 |
| M3-016 | HARDWARE_ONLY | Whether firmware 2457 emits colour frames in this format, and how the robot reacts to EnableColorImages. | A14, 3b |
| M3-017 | COMPATIBILITY_POLICY | Test tones, beeps and sweeps. No engine counterpart: the engine plays audio only through the animation stream. | C4 |
| M3-018 | IMPLEMENTATION_GAP | Colour decode. Encoding 9 is a JPEG built with the colour header at half width (160×240). It is decoded as BGR and converted to RGB, then resized to 320×240 with `cv::resize` INTER_LINEAR. IsColor is true for 2,3,4,6,7,9 and above 8. EncodedImage::Save writes colour at quality 90. | A7, A9, A10, A13, A25 |
| M3-019 | IMPLEMENTATION_GAP (a forced policy, to build; COMPATIBILITY_POLICY once built) | The connection-time SetCameraParams. Its f32 @0 and u16 @4 are stale stack bytes in the engine; this stack sends 0.0 and 0. The bool @6 is 1, as in the engine. It is sent reliable and not hot, right after the calibration read is queued. See MD1. | 1h, 1i, 1p, A18 |
| M3-020 | IMPLEMENTATION_GAP (a forced policy, to build; COMPATIBILITY_POLICY once built) | A payload that is empty or entirely 0xFF. The engine's trailing-0xFF strip has no lower bound and reads data[−1], which is undefined. This stack does not read out of bounds and treats such a frame as a decode failure. | A12 |
| M3-021 | IMPLEMENTATION_GAP | Camera exposure and gain. VisionSystem starts with limits of exposure 1..66 and gain 0.1..4.0, current exposure 16 and gain 2.0. VisionComponent::Init reads the initial exposure from vision_config.json ImageQuality.InitialExposureTime_ms, which is 16. On DefaultCameraParams (no time-sync gate, and vision must be initialised): if min ≤ 16 ≤ max, SetCameraSettings(16, gain) runs first, checked against the **constructor** limits. Only then are the robot's limits and gamma installed. SetCameraSettings sends SetCameraParams{f32 gain, u16 exposure, false} only when both values are in range. The engine never requests DefaultCameraParams. | A19..A22, 1a..1g, 1k..1n |
| M3-022 | IMPLEMENTATION_GAP | At connection (response byte 0) the engine queues the NV CameraCalib read (tag 0x80000001). Its callback sets vision **enabled** (+0x48 = 1) on all three paths: success, NV failure and size mismatch. On success it also installs the calibration, zeroing the distortion coefficients when robot+0x24 ≤ 6, and starts processing. Vision is never enabled any other way. The NV wire exchange is the NV subsystem's. | A17, 1h, 1j, 2a..2f, M1 CD21 |
| M3-023 | IMPLEMENTATION_GAP | EnableColorImages is never sent at connection. It stores the flag and sends it, reliable and not hot. The flag's only reader is BehaviorTrackLaser, which saves and restores it. Decoding follows the image's encoding, not the flag. | A24, 3a, 3b |
| M3-024 | IMPLEMENTATION_GAP | The audio output source comes from the firmware version JSON. If "sim" is null, the robot is physical and plays on the robot (source 2). Otherwise source 1. | C1, C2 |

## Decisions (the manager's, recorded for audit)

- **MD1: M3-019, the connection-time SetCameraParams bytes (a forced policy).**
  - The engine sends 6 uninitialised stack bytes, so there is no faithful value.
  - This stack sends zeros with the bool set to 1. Sending the message at all is the engine's behaviour; only the undefined bytes are chosen.
  - What the robot does with them is HARDWARE_ONLY, and so is whether this message is what makes the robot report DefaultCameraParams. The next hardware run records both: whether 0xC8 arrives, and the image brightness.
- **MD2: M3-020 (a forced policy).** The engine behaviour is undefined memory access, which cannot be reproduced.
- **MD3: the stack's raw-bitmap face API** (Display.Hold, used by the FACE hardware check) has no engine counterpart. The engine draws only procedural and sprite faces (M5). It stays as an explicit caller API and has no record, like SetAccessoryDiscovery (M2 MD4). The encoder it feeds (M3-007) is the engine's.
- **MD4: M2 correction C3.** DefaultCameraParams 0xC8 fields @0 and @4 are f32 (maxGain and gain), not u32. Evidence: operator== `vcmp.f32` at 0x007BF352 and 0x007BF364, and the handler's `vldr` at 0x00657CE0 (gap pass Q4). It is applied through the M2 generator inputs, and the M2 inventory is re-approved.
- **MD5: layer ownership.**
  - M5: the interlacing and scanline-parity toggle (B2..B4), face streaming outside animations and keep-alive (B17), and the audio animation readiness states (C16, Q5).
  - M11: the VisionComponent gates and mailbox (A4..A6), the auto-exposure algorithm (UpdateImageQuality, 1n) and the colour flag's behaviour consumer.
  - M6: the Wwise RTPC that SetRobotVolume also posts (C17).

## Existing record evidence found too weak or contradicted (why the records were rewritten)

- **M3-004:** its evidence (a decode in SetNextImage, and only two drops) was contradicted by A4..A6.
- **M3-006:** the "128x32" wire image and the 33.3 ms interval were contradicted by B15 and B18.
- **M3-007:** ">1024" and the missing c==127 rule were contradicted by B11 and B12.
- **M3-008:** "dd written as 01" is not what the engine does (B10, B14).
- **M3-013:** its model is not the engine's (C9, C15).
- **M3-016:** the decode geometry is source-settled (M3-018); only the robot's emission is hardware.
- **M3-018:** "interpolation not recovered" is contradicted by A10.
- **M3-005:** "15 fps" and "30 Hz tick" are not from the source.
- **M3-001, M3-010, M3-011, M3-012, M3-014, M3-015:** their citations were partial.

## Corrections after the first freeze (manager, 2026-09-24)

- **C1:** M3-019 and M3-020 were built in the M3 batch as the forced policies MD1 and MD2, and the batch verifier confirmed the code matches them. The checker only lets an IMPLEMENTATION_GAP move to EXACT_SOURCE or EQUIVALENT_IMPLEMENTATION, so both records were set to COMPATIBILITY_POLICY here and the inventory was re-approved.

## Appendix A: M3 pass, extractor report

I wrote nothing under the repo root. Scratch files are in `C:\Windows\TEMP\claude\...\b4cb80cd-...\scratchpad\extract\M3\`:
- tools: `dr.py`, `cdis.py`, `xr.py` (BL xref), `pcref.py` / `pcrefs.pkl` (pc-relative refs), `full.txt` (full .text disassembly)
- dumps: `dec_gray.txt`, `dec_rgb.txt`, `setnext.txt`, `rle.txt`, `drawface.txt`, `ustream.txt`, `update.txt`, `vsctor.txt`, `vsinit.txt`

All addresses are in `resources/lib/armeabi-v7a/libcozmoEngine.so`. I used these as interfaces and did not redo them: M2 Appendix C (R1..R10, H1..H5), M2 Appendix A §2, M1 CD21/CD27.

Findings that change existing records:
- **M3-006:** the face wire image is 128×64, not 128×32. The "32" is the number of 2-row pairs per column.
- **M3-007:** the raw face fallback triggers at ≥1024 bytes, not >1024.
- **M3-008:** the engine does its own interlacing and alternates the lit row parity. It never writes a fixed pair value.
- **M3-004:** the SetNextImage evidence is wrong. SetNextImage does not decode frames, and it drops frames for many more reasons than the record lists.
- **M3-018:** the colour interpolation is recovered. It is `cv::resize` with INTER_LINEAR.
- **M3-016:** colour decode geometry is settled by source. Only what the robot emits is hardware-only.
- **M3-013:** the engine's feed rule is fully in the source (14 unplayed frames, no 200 ms window, no TargetInFlight 10).
- **Interface correction:** the live audio encoder is `RobotAudioAnimationOnRobot::PopRobotAudioMessage` → `Audio::encodeMuLaw(float)`. `RobotAudioOutputSource::ProcessTick`, which M2 Appendix A §2 names, has no direct call site.

#### A. Camera

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| A1 | Image chunks are ignored until SyncTimeAck: HandleImageChunk exits unless robot+0x29. Frames that arrive before time sync never reach AddChunk or vision. | 0x00535A7A `ldrb.w r0,[r4,#0x29]; beq 0x535B9E` | NEW (App. C H1) | EXACT_SOURCE |
| A2 | Reassembly follows App. C R1..R10. | App. C | M3-002 | EXACT_SOURCE (interface) |
| A3 | Per-tick cap. The counter is at RobotToEngineImplMessaging+0x128 and is keyed to the event time (double [event+0]) against +0x130. A new time resets it to 0. Within the same time the counter is incremented, and the image passes while count<3. So at most 3 completed images per event time reach SetNextImage; the 4th and later are dropped with a warning. Only completed images count. | 0x00535B0E `cmp sb,#1`; 0x00535B28..0x00535B52 `vcmp.f64 / str #0,[r5,#0x128] / adds r3,r0,#1; cmp r3,#3; blo` | M3-005 | EXACT_SOURCE |
| A4 | VisionComponent::SetNextImage gates, in this order. (1) +0x10==0: NotInitialized warning, return 1. (2) +0x48==0: "not enabled" info, return 0. (3) If last t (+0xE4)≠0 and t<last: UnexpectedTimeStamp "Current:%u Last:%u", clear +0xE4/+0xE8, return 1; otherwise +0xF0=t−last and +0xE4=t. (4) Camera calibration +0x28 null: "VisionComponent.Update.NoCamCalib", return 1. (5) ComputeStateAt(min(t,newest)): 0x6000000 gives OriginMismatch and return 0; any other failure gives StateHistoryFail and returns the code. (6) VisionPoseData::Set under mutex +0x4C. (7) Only in calibration-image (+0xC8) or factory-dot (+0x328) mode: WasRotatingTooFast, then DecodeImageGray on the spot. (8) Paused (+0x4B): "Vision: <PAUSED>", image not queued. (9) Synchronous (+0x49): UpdateVisionSystem directly. (10) Otherwise a one-slot mailbox at +0x74..+0x97 under mutex +0x4C. An unprocessed image still in the slot is logged "DroppedFrame … not yet processed" and replaced (latest wins); a stat is kept at 0x653E30. | 0x00652B1C..0x00652BFE, 0x00652CA4..0x00652CBA, 0x00652C1A..0x00652C54, 0x00652DD6..0x00652E32, 0x00652D34..0x00652D58, 0x00652D62..0x00652F5C, 0x0065300E..0x0065304C, 0x00653056..0x006530C8 | NEW | EXACT_SOURCE |
| A5 | Mailbox swap. robot+0x394 (the EncodedImage AddChunk uses) receives the old slot's data vector and header words (+0xC..+0x23). The exception is +0x10 (prev ts), which is set to the new image's value. The image id at +0x1C therefore becomes the old slot's id. | 0x006530CC..0x00653148 | NEW | EXACT_SOURCE |
| A6 | Decoding happens in VisionSystem::Update(pose, EncodedImage), not in SetNextImage. If IsColor: DecodeImageRGB then ImageCache::Reset(RGB); otherwise DecodeImageGray then Reset(gray). On decode failure it returns without calling Update(pose, cache). | 0x006B4B7C..0x006B4C78 | NEW | EXACT_SOURCE (caller/thread is M11) |
| A7 | IsColor is true for encodings 2,3,4,6,7,9 and any value >8. It is false for 1, 5 and 8, and encoding 0 fails a VERIFY. | 0x004F2102..0x004F211E, tbb table 0x004F2110 | NEW | EXACT_SOURCE |
| A8 | Gray decode dispatch (tbh at 0x004F2898). 1: copy. 2: ToGray. 3/4: UnsupportedEncoding error. 5/6: imdecode(flags 0). 7: imdecode(0) then copyMakeBorder(0,0,160,160,CONSTANT 0). 8: MiniToJpegHelper(h=+0x18, w=+0x14, table 0xC48C40, 0x144 bytes) then imdecode(0). 9: MiniToJpegHelper(h, w/2, 0xC48D84, 0x14E) then imdecode(0) then Resize(h, w, 1). | targets 0x004F2BC0/2AEC/2924/28AE/2C2C/2988/2A2E; 0x004F29A8, 0x004F2A56, 0x004F2AD2 | M3-001, M3-003; dispatch NEW | EXACT_SOURCE |
| A9 | RGB decode dispatch (tbh at 0x004F21AE). JPEG cases use imdecode(flags 1) then cvtColor(4 = BGR2RGB). Case 9: MiniToJpegHelper(h, w/2, 0xC48D84), imdecode(1), cvtColor(4), then `ImageBase<RGB>::Resize(h, w, 1)`. | 0x004F21D6, 0x004F224C, 0x004F237E..0x004F2440 | M3-016 | EXACT_SOURCE |
| A10 | Resize(rows, cols, m) calls `cv::resize(src, dst, Size(cols, rows), 0, 0, m)`. The ResizeMethod is passed through unchanged, so m=1 is cv::INTER_LINEAR. | 0x00870486..0x008704D6 (`str r3,[sp,#0x10]`) | M3-018 (contradicts) | EXACT_SOURCE (OpenCV lib semantics) |
| A11 | After decode, rows must equal +0x18 (240) and cols +0x14 (320). Otherwise BadDecode "Failed to decode %dx%d … Got %dx%d" and return 1. On success, image ts (+0x3C) = EncodedImage+0xC (the ts of the last chunk). | gray 0x004F2CDA..0x004F2D34; RGB 0x004F2658..0x004F26BA | NEW | EXACT_SOURCE |
| A12 | MiniToJpegHelper: reserve(hdr + 2·n); insert the header; write height BE at 0x5E/0x5F and width BE at 0x60/0x61. It strips trailing 0xFF with no lower bound, so an empty or all-0xFF payload reads data[-1]. If fewer than 2 bytes remain it skips the copy. It copies data[1..n−1] (the flag byte is dropped for gray too), stuffing 0x00 after each 0xFF, then appends FF D9. | 0x004F31C4..0x004F32CC | M3-003 (+ out-of-bounds edge NEW) | EXACT_SOURCE |
| A13 | Header tables. Gray: 324 B, one DQT, SOF0 at 0x59 with 1 component (sampling 0x11), one 210-B DHT, SOS at 0x13A. Colour: 334 B, SOF0 at 0x59 with 3 components (Y 0x21 = 2×1, Cb 0x11, Cr 0x11, all quant table 0), one shared DHT, SOS with 3 components. So colour is a 4:2:2 JPEG patched to 160×240. | tables 0x00C48C40 / 0x00C48D84 (sha256 prefixes c44b69c9f614304a / 106a14ba4dd23964) | M3-001, M3-016 | EXACT_SOURCE |
| A14 | Colour flag: chunk 0 with data[0]≠0 and encoding 8 becomes 9. | 0x004F1D8C (App. C) | M3-002 | EXACT_SOURCE |
| A15 | There is no warm-up discard on the engine path. A1 and A3–A6 are the only drops. | above | M3-004 | stack policy (evidence text wrong, see below) |
| A16 | ImageRequest {Stream 1, QVGA 4} is sent at SyncTime. | 0x005152F0 (M2 App. A) | M1 interface | EXACT_SOURCE |
| A17 | At connection (response byte 0), the VisionComponent queues NV Read(tag 0x80000001, cb). The callback checks size == MakeWordAligned(CameraCalibration::Size()) and warns otherwise. On a match it unpacks, does make_shared<Vision::CameraCalibration>, and calls SetCameraCalibration, which satisfies A4(4). | 0x006583BA..0x006583FA; cb 0x0065AB68 (0x0065AB9A..0x0065ADE0) | M1 CD21 interface | EXACT_SOURCE (NV wire is outside M3) |
| A18 | At connection it also sends SetCameraParams {bytes 0..5 = uninitialised stack sp+8..sp+0xD, byte6 = 1}, reliable=1, hot=0. | 0x00658414..0x0065842C | M1 CD21 | EXACT_SOURCE for the bytes; robot meaning HARDWARE_ONLY (firmware .safe images are encrypted: entropy 7.8–8.0 bits/byte after the JSON header) |
| A19 | DefaultCameraParams handler. Requires VisionSystem::IsInitialized, else error. The static initial exposure u16 at .data 0x01051054 (=16) must lie in [msg+8, msg+0xA]; otherwise BadInitialExposureTime and nothing is sent. Then it calls SetCameraSettings(16, gain = f32 msg+4), and under the mutex SetCameraExposureParams(16, min=msg+8, max=msg+0xA, curGain=msg+4, minGain=0.1f, maxGain=f32 msg+0, gamma=msg+0xC[17]). Failure gives the SetFailed error. | 0x00657CA0..0x00657D34 | NEW | EXACT_SOURCE |
| A20 | SetCameraSettings(e, g) runs only if min(+0x8C) ≤ e ≤ max(+0x88) and +0x90 ≤ g ≤ +0x94. It then sends SetCameraParams {f32 g @0, u16 e @4, byte6 0} reliable, not hot; calls VizManager::SendCameraInfo and VisionSystem::SetNextCameraParams(e, g); and broadcasts CurrentCameraParams {g, e, autoExp +0x329}. | 0x006560E8..0x006561AA; 0x006B9DA4..0x006B9DBE; 0x006B9E78..0x006B9EA8 | NEW | EXACT_SOURCE |
| A21 | The VisionSystem constructor sets the limits: max exposure 66, min 1, minGain 0.1, maxGain 4.0, current exposure 16, current gain 2.0. So the first SetCameraSettings is checked against these. | 0x006B002A..0x006B004A | NEW | EXACT_SOURCE; whether VisionSystem::Init (0x006B0658) changes them is RECOVERABLE_GAP |
| A22 | SetCameraExposureParams stores max at +0x88, min at +0x8C (1 if ≤0), and gains at +0x90/+0x94. It calls SetGammaTable, then SetNextCameraParams(cur, curGain). | 0x006B93C6..0x006B9426 | NEW | EXACT_SOURCE (tail after 0x6B9426 not read) |
| A23 | Later exposure changes. UpdateImageQuality calls SetCameraSettings(result+0xC, +0x10) when +0x329 is set. The game SetCameraSettings message sets +0x329 and SetNextMode(7), and if autoExposure is 0 calls SetCameraSettings(u16 +2, f32 +4). Also EnableAutoExposure and SetAndDisableAutoExposure. | 0x00655596..0x006555A2; 0x0065837A..0x0065839E; 0x00657EE8; 0x00657EF6 | NEW (M11 interface) | EXACT_SOURCE for the sends |
| A24 | EnableColorImages(b) stores b at +0x32A and sends EnableColorImages{b} reliable, not hot. Callers are BehaviorTrackLaser Init and Cleanup and the game handler. It is never sent at connection, and Unity never sends it. | 0x006582CC..0x00658314; 0x005FAD2E, 0x005FBDB6, 0x0065835C; Unity grep | NEW | EXACT_SOURCE; the consumer of +0x32A is RECOVERABLE_GAP |
| A25 | EncodedImage::Save. Encoding 9: RGB decode (A9/A10), then ImageRGB::Save(path, quality 90). Encoding 8: the reconstructed JPEG is written raw. Any other encoding: raw bytes. | 0x004F2EFC..0x004F2FA8 (`movs r2,#0x5a`) | M3-018 | EXACT_SOURCE |
| A26 | Unity viewer: MinimizedColorToJpeg(h, w/2) then Texture2D.LoadImage. It does no interpolation itself. | unity/scripts/csharp/ImageReceiver.cs:84-160 | M3-018 | EXACT_SOURCE (app tier) |

#### B. Display

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| B1 | Canvas is Image(64 rows, 128 cols), filled with 0. Eyes are drawn, then warpAffine to Size(128, 64). | 0x00585B3E..0x00585B52, 0x00585C96..0x00585CB2 | M3-006 | EXACT_SOURCE (drawing is M5) |
| B2 | Interlacing is done in the drawer. Within the eye extent [top, bottom), clamped to 0..63, every second row is cleared (memclr 128 bytes, step 2). The cleared parity equals ProceduralFaceDrawer::_firstScanLine (0: even rows cleared; 1: odd rows cleared). | 0x00585D9C..0x00585DFC | NEW (bears on M3-008) | EXACT_SOURCE |
| B3 | AnimationStreamer::InitStream toggles both _firstScanLine statics (.bss 0x0105AB48 and 0x0105AB10, x=1−x). It does so when the animation is null, when its name differs from the last streamed name (+0x1C8), or when now + lastKeyFrameEnd − lastToggle(+0x1D4) > GetMaxBlinkSpacingTimeForScreenProtection_ms. It then sets +0x1D4 = now. Both start at 0 (.bss). | 0x0057B6A2..0x0057B738, 0x0057B804..0x0057B828 | NEW (M5 interface) | EXACT_SOURCE |
| B4 | Sprite face frames: FaceAnimationManager::GetFrame chooses one of two stored RLE variants per frame (+0xC) according to its _firstScanLine. | 0x005817B4..0x005817CA | NEW (M5) | EXACT_SOURCE |
| B5 | BufferFaceToSend: DrawFace, then CompressRLE. A nonzero result logs sErrorF and buffers nothing. Otherwise EngineToRobot(FaceImage{bytes}) is passed to BufferMessageToSend. | 0x0057C20C..0x0057C2D8 | M3-006/007 | EXACT_SOURCE |
| B6 | CompressRLE requires a 64×128 image. Otherwise sErrorF and return 1, so no face is sent. | 0x00581912..0x0058197A | NEW | EXACT_SOURCE |
| B7 | It builds 128 u64 column masks. Bit r of mask[c] is set if pixel(r,c)≠0; any non-zero value counts as lit. | 0x0058197C..0x005819E8 | M3-007 | EXACT_SOURCE |
| B8 | Skip opcode: an empty column emits 0b00nnnnnn, where n is the number of further empty columns (limits c+n ≤ 127 and n ≤ 63). It covers n+1 columns. | 0x00581B0A..0x00581B42 | M3-007 | EXACT_SOURCE |
| B9 | Repeat opcode: a column equal to the previous one (c>0) emits 0x40\|k, where k further columns equal it (same limits). It covers k+1 columns. | 0x00581A06..0x00581A46, 0x00581B54 | M3-007 | EXACT_SOURCE |
| B10 | Otherwise runs over 32 row pairs. Pair p holds bit0 = row 2k and bit1 = row 2k+1. Each run is the byte 0x80\|((len−1)<<2)\|p, len 1..32. | 0x00581A48..0x00581AB0 | M3-007 | EXACT_SOURCE |
| B11 | Trailing run: always emitted if c==127 or p≠0. A trailing p=0 run is dropped only when the next column is empty or equal to this one. | 0x00581AB2..0x00581AE2 (`cmp r5,#0x7f; bge`) | M3-007 (omits the c==127 case) | EXACT_SOURCE |
| B12 | Raw fallback when the RLE is ≥1024 bytes: resize to 1024 and copy the 128 LE u64 column masks (bit r = row r). | 0x00581B76..0x00581BA0 (`cmp.w r1,r0,lsr #10`) | M3-007 (">1024" is contradicted) | EXACT_SOURCE |
| B13 | A blank canvas encodes as {0x3F, 0x3F}. | follows from B8 | M3-009 | EXACT_SOURCE |
| B14 | The engine's reference decoder, DrawFaceRLE, treats size==1024 as raw. Otherwise FaceDisplayDecode(src, 64, 128) handles skip and repeat as in B8/B9. A run adds table[len−1]·p << row (table 0x00C5A650 = 1, 5, 0x15, …) and advances row by 2·len until row ≥ 64. A non-run byte ends the column without being consumed. | 0x00581C98..0x00581CC4; 0x0057FCF0..0x0057FDD8 | NEW | EXACT_SOURCE (engine model; firmware HARDWARE_ONLY) |
| B15 | So the FaceImage payload is 128 columns × 64 rows, sent as 32 two-row pairs per column. | B6, B10, B14 | M3-006 (contradicted) | EXACT_SOURCE |
| B16 | In each animation frame of UpdateStream, the FaceAnimationKeyFrame message goes first. BufferFaceToSend runs only if no such message was buffered, the face-animation track is empty, and the layers carry a procedural face. | 0x0057CA0A..0x0057CA30 | M3-015 | EXACT_SOURCE (M5 interface) |
| B17 | Faces outside animations: Update calls StreamLayers when there is no animation and HaveLayersToSend. KeepFaceAlive runs after idle > +0x1C0 s since the last stream (+0x88). StreamLayers is a full stream (StartOfAnimation, audio or silence, lights, face, then EndOfAnimation). | 0x0057D04E..0x0057D05C; 0x0057CF6A..0x0057CFF2; 0x0057C4E0..0x0057C680 | NEW (M5 "live/keep-alive") | EXACT_SOURCE (structure only) |
| B18 | Each fully sent frame advances stream time +0x84 by 0x21 = 33 ms. | 0x0057CA94..0x0057CA9C; 0x0057C656..0x0057C65C | M3-006 ("33.3") | EXACT_SOURCE; robot playback period HARDWARE_ONLY |
| B19 | FaceImageKeyFrame::GetStreamMessage constants. | 0x004F9354 (M2 App. A) | M5 interface | not re-traced |

#### C. Audio device

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| C1 | HandleFirmwareVersion parses the firmware JSON. If `json["sim"].isNull()`, it calls SetPhysicalRobot(true) and SetOutputSource(2 = PlayOnRobot); otherwise output source 1. Unity never sends SetRobotAudioOutputSource. | 0x00536934..0x0053698E ("sim" at 0x00536A4C) | NEW | EXACT_SOURCE |
| C2 | CreateAudioAnimation: source 2 gives RobotAudioAnimationOnRobot (GameObjectType 7), 1 gives OnDevice (6), 0 gives none. | 0x0059A070..0x0059A0B6 | NEW (M5/M6) | EXACT_SOURCE |
| C3 | HijackAudioPlugIn(22320, 744) and SetupHijackAudioPlugInAndRobotAudioBuffers(22320, 744). 30 Hz is only derived (22320/744). | 0x005942CE..0x005942EA | M3-011 | EXACT_SOURCE |
| C4 | Exactly one audio message per frame. GetAudioToSend calls audioAnim vfunc+0xC and copies 0x2E8 (744) bytes into an AudioSample; if there is none, AudioSilence is sent. Same in StreamLayers. | 0x0057C016..0x0057C056; 0x0057C94E..0x0057C9C4; 0x0057C5CA..0x0057C602 | M3-015 | EXACT_SOURCE |
| C5 | PopRobotAudioMessage works only in state 3. PopNextAudioFrameData returning null means no sample. Otherwise each float goes through encodeMuLaw. A frame shorter than 744 samples is zero-padded (0x00 = silence in this codec). A frame longer than 744 is not bounded (0x2EC stack buffer). | 0x00597DD4..0x00597E4E | NEW | EXACT_SOURCE |
| C6 | encodeMuLaw(float): NaN warns and gives 0. s = f≤−1 ? −32767 : trunc(min(f,1)·32767). mag = s^(s>>15). exp = seg[mag>>8]. mant = (mag>>8)==0 ? mag>>4 : (mag>>(exp+3))&0xF. byte = (s<0 ? 0x80 : 0)\|exp<<4\|mant. | 0x00597AD8..0x00597B8E; table 0x00C5C3F0; 32767.0 at 0x00597C18 | M3-010 | EXACT_SOURCE |
| C7 | No volume scaling on this path. | 0x00597DFC..0x00597E02 | NEW | EXACT_SOURCE |
| C8 | RobotAudioOutputSource (ProcessTick/encodeMuLaw: volume × constant, no clamp, table 0x00C5CB40) has no BL call to its constructor 0x0059B9D8, and its vtable GOT 0x0103EDE4 is referenced only in that constructor. AudioMixingConsole's constructor and ProcessFrame have no callers. The global encodeMuLaw(short\*) at 0x004E4C34 has no callers. | BL scan and pc-ref scan | contradicts the M2 App. A §2 interface | EXACT_SOURCE within direct calls (indirect calls not excluded) |
| C9 | UpdateAmountToSend. Byte budget = min(8192 − (streamed − played), 30000); a negative value warns and becomes 0. Audio budget = max(14 − (framesStreamed − framesPlayed), 0). Counters: +0x238 bytes played, +0x23C bytes streamed, +0x240 frames played, +0x244 frames streamed. | 0x0057C6F6..0x0057C7AC | M3-012, M3-014 | EXACT_SOURCE |
| C10 | The AnimationState handler (gated by +0x29) is the only writer of the played counters: +0x238 = numAnimBytesPlayed, +0x240 = numAudioFramesPlayed. It also writes +0x348 = enabledAnimTracks and +0x248 = tag. | 0x00537FD0..0x0053800C | NEW | EXACT_SOURCE |
| C11 | Each successful send adds EngineToRobot::Size() to bytes streamed (Size starts at 1 for the tag) and adds 1 to frames streamed for tags 0x8E/0x8F. | 0x0057BFB0..0x0057BFCC; 0x007ABB98 | M3-014 | EXACT_SOURCE (per-member Size values RECOVERABLE) |
| C12 | EndOfAnimation is sent directly, reliable, not budget-gated, and counts as one audio frame plus its bytes. | 0x0057C464..0x0057C496 | NEW | EXACT_SOURCE |
| C13 | The Robot constructor zeroes 0x12 bytes at +0x238. | 0x0050FD0A..0x0050FD10 | NEW | EXACT_SOURCE |
| C14 | SendBufferedMessages drains in FIFO order and stops (returns 0) at the first message over the byte budget, or an audio message when the audio budget is 0. Every message is sent with SendMessage(reliable=1, hot=0). A send error is returned. | 0x0057BF60..0x0057C010 | M3-014 | EXACT_SOURCE |
| C15 | Per engine Update, UpdateStream refreshes the budgets and flushes leftovers. It then loops while ShouldProcessAnimationFrame (buffer empty and audio anim ready), building one frame and draining after each. If the drain completes, it adds 33 ms and continues; otherwise it stops. Several frames can go per Update. | 0x0057C8E4..0x0057CABE; 0x0057CC6C..0x0057CCA6 | M3-013/015 | EXACT_SOURCE |
| C16 | Readiness gate. UpdateAnimationIsReady is ready when there is no animation or the state is in {1,3,4,5}; states 4/5 also clear the animation. OnRobot Update: states 1/2 go to UpdateLoading; state 3 goes to UpdateAudioFramesReady, where "no data" means state 2, i.e. not ready. | 0x0059A1CC..0x0059A1F6; 0x00597C64..0x00597DB2 | NEW (M5/M6) | EXACT_SOURCE (state names at 0x00596434 RECOVERABLE) |
| C17 | SetRobotVolume(v) stores +0x6C, sends SetAudioVolume{u16 vcvt.u32(v·65535)} reliable, not hot, then posts Wwise RTPC 0x637C1240 (Unity VolumeType.Robot) with v. | 0x0059A21E..0x0059A278; VolumeType.cs:9 | M1 CD27, NEW | EXACT_SOURCE for the sends; the RTPC's effect on samples is BLOCKED_EXTERNAL (Wwise, M6) |
| C18 | 14 is the engine's budget; the robot's real buffer size is firmware. | 0x0057C79E `add.w r1,r1,#0xe` | M3-012 | EXACT_SOURCE (engine); HARDWARE_ONLY (firmware) |

#### Existing records contradicted by the source

- **M3-004 (evidence):**
  - "SetNextImage decodes it on the spot" is false. It decodes only in calibration or factory mode (A4(7)); normal decoding is in VisionSystem::Update (A6).
  - "The only drops are invalid images and the per-tick cap" is false. The other drops are A1, A4 (1)–(5) and (8), and the mailbox replacement (A4(10)).
  - "No warm-up in the engine" still holds.
- **M3-006:** "wire image 128x32" is contradicted: the wire is 128×64 as 32 pairs (B15). "33.3 ms" is not in the engine; its step is 33 ms (B18).
- **M3-007:** the raw fallback is ≥1024, not >1024 (B12). The trailing-run rule leaves out the always-emit case at c==127 (B11).
- **M3-008:** the engine writes no fixed pair value. Each pair is two canvas rows (B10, B14), and the lit parity alternates through _firstScanLine (B2–B4). Only the firmware's physical row mapping stays HARDWARE_ONLY.
- **M3-013:** the cited UpdateAmountToSend gives a 14-frame budget with a per-Update drain (C9, C15). It has no TargetInFlight 10, no 200 ms window and no priming. The only priming-like step is C16.
- **M3-016:** the HARDWARE_ONLY label overstates the gap. The engine's decode geometry is source-settled (A9, A10, A13); only the robot's emission is hardware.
- **M3-018:** "interpolation not recovered" is contradicted. The engine uses cv::resize INTER_LINEAR (A10), and Save writes at quality 90 (A25).
- **M2 App. A §2 interface:** RobotAudioOutputSource::ProcessTick is not the live AudioSample path (C1, C4, C5, C8).
- **json DefaultCameraParams field0/field1 (u32):** the engine loads them with vldr as floats, maxGain and gain (A19). This belongs to M2.

#### Records whose evidence is too weak

- **M3-005:** its third bullet ("15 fps", "30 Hz tick") is not source. M1 records a 60 ms engine tick, and the camera rate is hardware.
- **M3-006:** its evidence is the bare symbol "DrawFace".
- **M3-001:** it cites only the table addresses (A13 adds the layout).
- **M3-010:** the encoder is cited without the live path. C1/C4/C5 now supply it.
- **M3-011:** "30 Hz" is derived. I did not verify the EnumToString 0x007BC7D8 citation.
- **M3-012:** it proves the engine's budget, not the robot's buffer.
- **M3-015:** it cites only a bare function address. "Tick" should read as the 33 ms stream frame, and several frames can go per engine Update.
- **M3-014:** correct, but it leaves out the EndOfAnimation counting (C12).

#### Open questions

1. **Connection-time SetCameraParams (A18):** its first 6 bytes are uninitialised stack and byte 6 is 1. What the stack sends here is a policy decision, because the robot's meaning of these bytes is HARDWARE_ONLY (the firmware images are encrypted).
2. **Firmware-side facts, all HARDWARE_ONLY:** the physical row mapping of pair bits, the robot's playback period, its real audio buffer size, and whether firmware 2457 emits colour frames in this format.
3. **RECOVERABLE_GAP, VisionComponent defaults:** the initial values of +0x48 (enabled), +0x49 (synchronous) and +0x4B (paused). To read: the constructor 0x006500A8, Init 0x00650D20 and Start 0x006517A8. SetIsSynchronous 0x00651D6C has no BL callers.
4. **RECOVERABLE_GAP, exposure limits:** whether VisionSystem::Init 0x006B0658 changes the constructor limits (A21).
5. **RECOVERABLE_GAP, other gaps:**
   - the consumer of +0x32A;
   - the RobotAudioAnimation state names;
   - the per-member values of EngineToRobot::Size (M2);
   - the mailbox consumer (M11).
6. **Layer ownership (manager's call):** rows B2–B4, B16–B17, C2 and C16 sit on the M5/M6 boundary. The manager decides which records they become.

## Appendix B: gap pass, extractor report

I wrote nothing under the repo root. My scripts and dumps are in `...\scratchpad\extract\M3-gap\`:
- scripts: `vsw.py`, `vcw.py`, `own.py`, `addst.py`
- dumps: `conn.txt`, `vc_dcp.txt`, `vcctor.txt`, `vcinit.txt`, `vcstart.txt`, `nvcb.txt`, `scep.txt`, `sae.txt`, `em.txt`, `clad.txt`, `rawst.txt`

All addresses are in `resources/lib/armeabi-v7a/libcozmoEngine.so`. No existing manifest record covers these steps; the only interface they touch is M1 CD21. Every row below is **NEW** unless it names an A-row from the M3 report.

**The finding that changes the most:** the "enabled" flag (VisionComponent +0x48) is set to 1 by exactly one writer, the NV camera-calibration read callback. It sets it on all three of that callback's paths, including NV failure and size mismatch. The "paused" flag (+0x4B) is never written after the constructor.

#### Q1. Exposure limits, connection order, and SetCameraParams byte 6

| step | what the original does | citation | classification |
|---|---|---|---|
| 1a | The VisionSystem constructor sets these fields: +0x84 ImagingPipeline*, +0x88 maxExp 66, +0x8C minExp 1, +0x90 minGain 0.1f, +0x94 maxGain 4.0f, +0x98 curExp 16, +0x9C curGain 2.0f. | 0x006B002A `movw r2,#0xcccd`, 0x006B002E `movs r0,#0x42`, 0x006B0030 `strd r6,r0,[r4,#0x84]`, 0x006B004A `stm r0!,{r1,r2,r3,r5,r7}` (r0=this+0x8C) | EXACT_SOURCE (A21) |
| 1b | **VisionSystem::Init does not change the limits.** It stores only to +0x338, +0x58, +0x328 (0x006B0674..7C), +0x30/+0x38 (0x6B0F16/2A), +0x2FC, +0x30C, +0x350 and +0x58=1 (0x006B141A). Its callees write elsewhere. SetAutoExposureParams writes only into ImagingPipeline (+0x84 → `ImagingPipeline::SetExposureParameters`, 0x006B1868/0x006B187A). EnableMode calls only FaceTracker toggles and logs. The virtual calls at 0x6B13FC and 0x6B140E go to the cv::CLAHE object at +0x348/+0x34C (created by `cv::createCLAHE` at 0x006B017C). SetProfileGroupName acts on the Profiler base. | vsinit dump; `sae.txt`; `em.txt` | EXACT_SOURCE |
| 1c | The only writers of +0x88..+0x94 in any VisionSystem function (and in the region 0x6A0000–0x6C8000) are the constructor and SetCameraExposureParams. The latter does `strd r8,r0,[r7,#0x88]` (max, then min, forced to 1 if ≤0), `vstr s20,[r7,#0x90]` (minGain) and `vstr s16,[r7,#0x94]` (maxGain). | 0x006B9416..0x006B941E; region scan | EXACT_SOURCE (scan scope: direct immediate stores) |
| 1d | +0x98/+0x9C (current exposure/gain) are written only in VisionSystem::Update. It copies the pending +0xA4/+0xA8 when +0xA0 is set, then clears +0xA0. SetNextCameraParams only queues a value in +0xA0..+0xA8, and warns "OverwritingPreviousParams" if one is already pending. | 0x006B50C8..0x006B50D8; 0x006B22AA, 0x006B2304..0x006B230C | EXACT_SOURCE |
| 1e | **Values in effect when the first DefaultCameraParams arrives:** max 66, min 1, minGain 0.1, maxGain 4.0 (constructor values). The current exposure is 16 and the current gain is 2.0, unless a queued SetNextCameraParams was already applied by Update. That queue does not affect the limits. | 1a–1d | EXACT_SOURCE |
| 1f | The "initial exposure" static at .data 0x01051054 is .data-initialised to 16. VisionComponent::Init overwrites it from `json["ImageQuality"]["InitialExposureTime_ms"]` (GetValueOptional<u16>). The shipped `vision_config.json:46` holds 16. That JSON is loaded by RobotDataLoader::LoadRobotConfigs into loader+0x118 and passed to VisionComponent::Init from the Robot constructor. | 0x00650DAE (key string at 0x651238), 0x00650DC4, 0x00650DCA; 0x0052263A/0x0052267C; 0x005103C6/0x005103CA | EXACT_SOURCE |
| 1g | VisionComponent::Init sets +0x10=0 on entry and +0x10=1 on success. It calls VisionSystem::Init (via +0x1C) only after the three ImageQuality keys and the PerformanceLogging key are read successfully. | 0x00650D30, 0x00650E36, 0x006510E2 | EXACT_SOURCE |
| 1h | **Connection handler** (VisionComponent, RobotConnectionResponse, `ldrb [r1]==0` at 0x006583BA). It first builds a std::function capturing `this` (vtable 0x0102F94C stored at 0x006583D2, `this` at 0x006583C6), then calls `NVStorageComponent::Read(0x80000001, cb, nullptr, false)`. That call only queues the read; the NV wire request is outside M3. Afterwards it sends SetCameraParams reliable=1, hot=0. | 0x006583E2..0x006583FA; 0x00658414..0x0065842C | EXACT_SOURCE |
| 1i | **Bytes 0..5 are uninitialised.** Before the send, the function's only stack stores are at sp+0x448, sp+0x434, sp+0x440, sp+0x430 and sp+0 (`str r0,[sp]` at 0x006583F2, NV Read's 5th argument), plus `strb r0,[sp,#0xe]` = 1 (0x00658416). No callee receives a pointer into sp+8..sp+0xD, except `EngineToRobot(SetCameraParams&&)` at 0x00658420, which reads from it. That constructor copies 8 bytes, sp+8..sp+0xF (0x007A99A0 `ldrd r2,r1,[r1]`). Callee frames sit below sp. So f32@0 and u16@4 are stale stack contents, byte 6 = 1, and byte 7 (padding) is not packed. | `conn.txt` | EXACT_SOURCE; robot meaning HARDWARE_ONLY |
| 1j | **NV callback** (operator() at 0x0065AB68, vtable slot 6). On NVResult≠0 (NV_OKAY=0, Unity NVResult.cs:5) it logs "ReadCameraCalibration.Failed". On a size mismatch it logs "SizeMismatch". On success it unpacks and logs "…Recvd". If robot+0x24 ≤ 6 it zeroes the distortion coefficients ("IgnoringDistCoeffs"). It then calls make_shared and SetCameraCalibration, which calls Start() when +0x49==0. **All three paths end at `strb.w r0,[r5,#0x48]` with r0=1.** | 0x0065AB74..0x0065AB92, 0x0065ABA4, 0x0065AD54..0x0065AD9C, 0x0065ADE0, 0x0065AE7E/0x0065AE80; SetCameraCalibration 0x0065175E..0x00651766 | EXACT_SOURCE |
| 1k | **DefaultCameraParams handler.** RobotToEngineImplMessaging::HandleDefaultCameraParams does *not* check +0x29 (no time-sync gate). It tail-calls VisionComponent::HandleDefaultCameraParams on robot+0x258 through a veneer at 0x008CB3CC. That handler requires VisionSystem::IsInitialized (+0x58, `ldrb [r0,#0x58]` at 0x006B2CD6). It requires `msg+8 ≤ init ≤ msg+0xA`. It then calls **SetCameraSettings(init, f32 msg+4) first** (0x00657CCA). **Only after that**, under mutex +0x4C, it calls SetCameraExposureParams(init, msg+8, msg+0xA, msg+4, 0.1f, msg+0, &msg[0xC]) (0x00657D04). So the first SetCameraSettings is range-checked against the constructor limits in 1e. | 0x00537114..0x0053712A; 0x00657CA8..0x00657D04 | EXACT_SOURCE |
| 1l | SetCameraSettings checks with VisionSystem::IsExposureValid (`ldrd r1,r0,[r0,#0x88]`; valid when max≥e and min≤e, else warns "Exposure %dms not in range") and IsGainValid (`vcmpe` against +0x94 and +0x90; NaN counts as invalid). It then sends {f32 g, u16 e, byte6 0}, reliable, not hot. So on the first DefaultCameraParams, a gain outside [0.1, 4.0] sends no SetCameraParams, though the new limits are still installed. | 0x006B9DAA..0x006B9DBE; 0x006B9E80..0x006B9EA8; 0x0065614A..0x00656166 | EXACT_SOURCE |
| 1m | A19's "SetFailed" error path is unreachable. SetCameraExposureParams has one normal return, `movs r0,#0` (0x006B9496), and a SetGammaTable failure only warns (0x006B93A0). Its tail after 0x6B9426 logs info and returns 0. | `scep.txt` | EXACT_SOURCE (corrects the A19/A22 tails) |
| 1n | Another possible earlier sender of SetCameraSettings is UpdateImageQuality. Auto-exposure (+0x329) starts as **1** (constructor `strh #0x100` at +0x328, 0x0065017C). The call runs only when robot+0x14 is set, the result's +8 is set and +0x329 is set, and only once images are being processed (needs +0x48=1, calibration, SyncTimeAck). If that happens before DefaultCameraParams, it is checked against the same constructor limits. | 0x0065558C..0x006555A2 | EXACT_SOURCE (whether it happens first depends on timing) |
| 1o | **Order the engine enforces:** Robot constructor (1a, 1f, 1g), then the connection handler (NV Read queued, then SetCameraParams{junk, 1}). The NV callback (enable, then Start) and DefaultCameraParams (1k) each happen whenever their message arrives. **The engine never requests DefaultCameraParams**: no sender exists, and the handler is purely reactive. Whether it comes before or after the NV result, and whether the connection-time SetCameraParams triggers it, is firmware behaviour. | above | UNKNOWN in engine source → HARDWARE_ONLY (firmware encrypted, per A18); the repo's captures come from our own stack, not the official app |
| 1p | **SetCameraParams CLAD (tag 0x57).** Size()=7 (0x007BF4BC). Pack: WriteBytes(f32 @0, 4), WriteBytes(u16 @4, 2), `Write<bool>(byte @6)` (0x007BF456..0x007BF47A). operator==: `vcmp.f32` @0, `ldrh`/cmp @4, `ldrb`/cmp @6 (0x007BF4C2..0x007BF4EA). The buffer constructor defaults byte 6 to 0 (0x007BF3AC). So byte 6 is a **bool**. | `clad.txt` | EXACT_SOURCE (type) |
| 1q | Byte 6's field *name* is not in the native binary (CLAD carries no names) and **there is no Unity twin**: RobotInterface messages are not in `unity/`. The closest app-tier analogues differ in layout. G2E SetCameraSettings has {bool enableAutoExposure; u16 exposure_ms; f32 gain} (SetCameraSettings.cs:8-12). E2G CurrentCameraParams has {f32 cameraGain; u16 exposure_ms; bool autoExposureEnabled} (CurrentCameraParams.cs:8-12), which the engine fills with +0x329 at 0x00656188. The name "auto_exposure_enabled" comes only from PyCozmo, which is not primary. | Unity files cited | name UNKNOWN (type EXACT_SOURCE) |

#### Q2. VisionComponent +0x48 (enabled), +0x49 (synchronous), +0x4B (paused)

| step | what the original does | citation | classification |
|---|---|---|---|
| 2a | The constructor clears +0x48..+0x4B together with one word store of r5=0: `str r5,[sl,#0x4c]!` then `str r5,[sl,#-4]` (raw bytes `4a f8 4c 5f 0a f1 04 04 4a f8 04 5c`). All four start at 0. | 0x006500E6, 0x006500EE (r5=0 since 0x006500B4) | EXACT_SOURCE |
| 2b | Init (0x00650D20) writes none of +0x48..+0x4B; it writes only +0x10, +0x98 and +0x9C. | `vcinit.txt` | EXACT_SOURCE |
| 2c | Start (0x006517A8) requires calibration (+0x28), otherwise errors "Camera calibration must be set…". If the thread is already running (+0x4A) it stops and joins it, then sets +0x4A=1 and spawns Processor. It only reads +0x4B, for the log line "(paused:%d)". | 0x006517AE..0x006518C2 | EXACT_SOURCE |
| 2d | **+0x48's only writer after the constructor is the NV callback (1j), which sets it to 1 unconditionally.** In practice the connection handler enables vision once the NV read completes, whether it succeeds or fails. On failure or mismatch no calibration is set, so SetNextImage still stops at the NoCamCalib gate (A4(4)). | raw-byte scan of every `strb.w Rt,[Rn,#0x48]`: the only VisionComponent hit is 0x0065AE80 (misattributed to `vector<Point3f>::__emplace_back` because the lambda is not exported) | EXACT_SOURCE (the scan covers strb.w imm12, str/strh/strd forms covering the byte, add-then-store and register-offset stores) |
| 2e | **+0x4B has no writer after the constructor.** There are zero `strb.w [Rn,#0x4b]` encodings in .text, and no strh/str/strd covering +0x4B in VisionComponent code. Its readers are Start (log only), Processor (0x00652218) and SetNextImage (0x0065300E/0x0065302C). So in this build "paused" is always 0 and A4(8) never triggers. | `rawst.txt`, `own.py "#0x4b\]"` | EXACT_SOURCE (memcpy-style writes not excluded) |
| 2f | +0x49's writers are the constructor (0) and SetIsSynchronous (0x00651DC0, 0x00651E36). SetIsSynchronous has no direct or PLT callers (PLT stub 0x004BA7F4 is uncalled), and its address appears only in .dynsym. So +0x49 stays 0: frames take the asynchronous mailbox path (A4(10)), and SetCameraCalibration starts the thread. | xr.py; raw search for 0x00651D6D; 0x0065175E | EXACT_SOURCE (reflective or indirect calls not excluded) |

#### Q3. What +0x32A (EnableColorImages flag) is used for

| step | what the original does | citation | classification |
|---|---|---|---|
| 3a | Writers: the constructor (0, at 0x00650180); EnableColorImages(b) (0x006582D6, followed by the send helper 0x006582E8 which sends EnableColorImages{b}, reliable, not hot); the exported G2E handler (0x00658368); and an unexported G2E tag-119 lambda (0x00658F74, which also calls 0x006582E8). | as cited | EXACT_SOURCE |
| 3b | **Its only reader is BehaviorTrackLaser::InitHelper**, which saves the old value at behaviour+0x1C8 (`ldrb.w r0,[r0,#0x32a]` at 0x005FAC64, `strb.w r0,[r4,#0x1c8]` at 0x005FAC68) before calling EnableColorImages(1) (0x005FAD2E). Cleanup restores it with EnableColorImages(saved) (`ldrb.w r1,[r4,#0x1c8]` at 0x005FBDAE, call at 0x005FBDB6). Nothing in VisionComponent or VisionSystem reads it: no ldrb, ldrh or ldr covering +0x32A. Decoding is driven by the image's encoding (A7/A14), not by this flag. | `own.py` scans for #0x328..#0x32a and a raw ldrb.w scan | EXACT_SOURCE on the engine side; the robot's reaction is HARDWARE_ONLY |

#### Q4. DefaultCameraParams (0xC8) field types

| field | evidence | type |
|---|---|---|
| @0 | Unpack ReadBytes(4) (0x007BF1E2); operator== `vcmp.f32 s2,s0` (0x007BF352); the handler does `vldr s0,[r4]`, which becomes maxGain (0x00657CE0) | **f32** |
| @4 | Unpack ReadBytes(4); operator== `vcmp.f32` (0x007BF364); the handler does `vldr s2,[r4,#4]` for curGain and `ldr r2,[r4,#4]` as the float argument (softfp) to SetCameraSettings (0x00657CC4) | **f32** |
| @8 | Unpack ReadBytes(2) (0x007BF1F8); operator== `ldrh`/`cmp` (0x007BF36E); handler `ldrh` (0x00657CB8) = minExposure | **u16** |
| @0xA | Unpack ReadBytes(2); operator== `ldrh`/`cmp` (0x007BF376); handler `ldrh` = maxExposure | **u16** |
| @0xC | Unpack loop of 17 × ReadBytes(1) (0x007BF212..0x007BF22C); operator== byte loop from 0xC to 0x1D (0x007BF37E..0x007BF394) = gamma | **u8[17]** |

Size()=0x1D=29 (0x007BF346). The protocol JSON's u32 for field0 and field1 is **contradicted**; both are f32. This is EXACT_SOURCE and belongs to M2.

#### Q5. RobotAudioAnimation state names

GetStringForAnimationState (0x00596434) builds a static vector of 7 strings in index order and returns `vec[state]` (0x005965EA, `add r1,r4,r4,lsl#1` then ×4). The strings come from adr literals at 0x00596694..0x005966FC:
- 0 = Preparing
- 1 = LoadingStream
- 2 = LoadingStreamFrames
- 3 = AudioFramesReady
- 4 = AnimationCompleted
- 5 = AnimationError
- 6 = AnimationStateCount

So C16 reads as follows. It is "ready" in LoadingStream, AudioFramesReady, AnimationCompleted and AnimationError; the last two also clear the animation (0x0059A1D4..0x0059A1F0). It is not ready in Preparing or LoadingStreamFrames. In OnRobot::Update, LoadingStream and LoadingStreamFrames go to UpdateLoading. Preparing calls vfunc+0x18 unless the buffer IsWaitingForReset (0x00597C76..0x00597C86). EXACT_SOURCE.

#### Records contradicted by the source
- **A19 (M3 report):** "Failure gives the SetFailed error" is structurally present but unreachable (1m).
- **A18/A20 ordering assumption:** the first SetCameraSettings is checked against the *constructor* limits, not the robot's (1k).
- **A4(8) "Paused":** unreachable in this build (2e).
- **A20:** the range check goes through IsExposureValid/IsGainValid, not inline code; the behaviour matches, but the citation should name 0x006B9DA4/0x006B9E78.
- **The protocol JSON's DefaultCameraParams field0/field1 as u32** (Q4).

#### Evidence too weak to keep its current status
None beyond the above. Open question 3 from the M3 report (VisionComponent defaults) and open question 4 (exposure limits) are now closed by source.

#### Open questions for the manager
1. The timing and trigger of DefaultCameraParams, and its order relative to the NV-read result, are firmware behaviour and HARDWARE_ONLY (1o).
2. Byte 6's name: the source settles only "bool". The manager decides whether to adopt the app-tier analogue name (autoExposureEnabled/enableAutoExposure) as a label.
3. Scan limits: the "no writer" results for +0x4B, +0x49 and +0x88..+0x94 rest on direct-store encodings, add-then-store within 12 instructions, and register-offset stores. memcpy-style or pointer-escaped writes are not excluded. No such escape of these addresses was seen.
4. robot+0x24 ≤ 6 (distortion coefficients ignored) and robot+0x14 (the UpdateImageQuality gate) are robot fields outside M3. I did not identify what they hold.

## Correction C2 (manager, 2026-09-26): the NV storage component (device storage)

**Why.** M3-022 was settled EXACT_SOURCE for the connection-time NV CameraCalib read while only the callback existed. The engine's NV wire — the startup queue, the dispatch gating, the 5 s timeout, the retries, the non-factory header and reassembly, and no callback on disconnect — had **no records at all**; the gap lived only in `NvStorage.cs`'s summary and readiness-to-stream was set far earlier than the engine's. Under the operator's standing authorisation for inventory corrections, and the rule that a settled record owns its whole path, the NV component gets its own records here. `M3-022` keeps its callback claims and no longer defers its wire to another record.

**Source.** Four read-only extractor passes over `libcozmoEngine.so` 3.4.0-1204: pass 1 (the factory read, `re-analysis/evidence/nv/nv-pass1-calibration-read.md`), pass 2 (the dispatch gates, `.../nv-pass2-dispatch-gates.md`), pass 3 (the connection queue, `.../nv-pass3-connection-queue.md`) and pass 4 (the non-factory path and the reads' callbacks, `.scratch/nv-pass4/report.md`). Pass 4 corrects two pass-1/3 imprecisions: the addresses in pass 3 section 2c/2d are Read() *call sites*, not the callback bodies (the callback bodies are the lambda `operator()`s cited below); and `IsValidEntryTag`/`GetBaseEntryTag`/`InitSizeTable` span wider ranges than pass 1 quoted (0x644145..0x6441D8, 0x6441F8..0x6443F4, 0x643B48..0x643EC4). A fifth check: **M11-011 was called contradicted by an earlier pass, but the current record already reads Length = 1 and assemble-by-index, so it is not contradicted.** The NV wire records now live in M3; M11-011's NV-request portion is a naming/ownership cleanup for the M11 inventory, not a reclassification.

**The records added (all IMPLEMENTATION_GAP to build, unless stated):**

| record | what | rows |
| --- | --- | --- |
| M3-025 | NV entry-tag validity and the two size tables. `IsValidEntryTag` (0x644148..0x6441A0): `(tag−0x180000)>>14 ≤ 0x1e`, tag ≠ 0x198000, tag a multiple of 0x1000, and an exact `_maxSizeTable` key. `_maxSizeTable` (0x4D7F41, InitSizeTable 0x643B48..0x643CE0) values: 0x180000..0x183000 → 0x1000, 0x184000 → 0x10000, 0x194000..0x197000 → 0x1000, 0x198000 → 0x64000, plus 0xDE000 → 0x30 and 0xDE030 → 0x1DFD0. _maxFactoryEntrySizeTable (0xC81064, 23 keys; pass 4b lists them): 0x80000000..0x80000008, 0x80000010..0x80000012 and 0xC0000000/1/4 → 1; 0x80010000..0x80060000, 0x80100000, 0x80110000 → 0xFFFF; IsFactoryEntryTag 0x6440D4..0x64412C is exact membership in those 23 keys. `GetMaxSizeForEntryTag` 0x643FC8..0x64404E; `GetBaseEntryTag` 0x6441F8..0x6443F4. | 1a, 1b, 1c (pass 4), 1-1..1-6 (pass 4b) |
| M3-026 | `NVStorageComponent::Read` and the FIFO. Read 0x644E2A..0x644EF4: an invalid tag warns, optionally broadcasts and calls the callback `(nullptr, 0, −6)`; a valid tag is emplaced on the deque at +0xF8. One operation in flight; ProcessRequest pops the front (pass 2 4a). | 1h-4, 4a (pass 1/2) |
| M3-027 | `ProcessRequest` READ: the NVCommand build and arm. Factory tag → Length = `_maxFactoryEntrySizeTable[tag]` (0x64504C..0x64507C); **non-factory → Length = 0x400** (0x64536A); op 0, byte 9 zero. Sent reliable = 1, hot = 0. Arm at 0x6453F8..0x645484: +0x50 = the request tag, +0x58 = cb, +0x71 = the broadcast flag, +0x74 = robot+0x2C + **5000**, +0x54 = the caller's vector or a fresh owned one, state 2, retry +0xF4 = 0. | 1d (pass 4), 5..8 (pass 1) |
| M3-028 | The **non-factory header** and re-request. At index 0 for a non-factory base with +0x79 == 0 (0x6430A6..0x6430EE): the reply must be ≥ 16 bytes else "TooLittleReadData" and result −3 (0x6430F2..0x643478); u32[0] must be magic **0x435A4D4F** else "InvalidHeader" and result −1 (0x6430FC..0x643112); u32[8] = total size must be ≤ max−16 else "InvalidDataSize" and result −1 (0x6430FE..0x64311C). If it fits, resize the reply vector to total+16 (a shrink/no-op) so the reassembly copy is bounded at total (pass 4b Q3); otherwise "ReadingRestOfData" and re-request the rest: tag = the reply's tag, op = 0, **Length = size + 16**, reliable, not hot, with no re-arm (+0x50/+0x74/+0x78/state unchanged, +0x79 stays 1) (0x643840..0x6438CA). | 1e (pass 4), 3-1..3-3 (pass 4b) |
| M3-029 | **Reassembly** and the array reader. Offset = index·1024 − hdr, where hdr = 16 for index > 0 on a non-factory base and 0 otherwise; blob 0's source skips the 16-byte header so the delivered buffer has no header (0x643538..0x643594). The buffer is resized (zero-filled) only when shorter; duplicates overwrite; no per-index bookkeeping (0x643574). For a non-factory entry that fits in one blob the delivered buffer is exactly the header total, with the 16-byte header skipped and no zero tail (0x643926..0x643594; pass 4b Q3). Each applied blob re-arms the timeout to robot+0x2C + 5000 (0x64359A..0x6435AE). The inbound array reader 0x73213C has no cap and a short blob is kept (pass 3 Q3). | 1f (pass 4), 3-4..3-7 (pass 4b), 3a..3f (pass 3) |
| M3-030 | **Completion, the callback/vector sink and the broadcast.** MORE(3) keeps waiting; −1 → "ReadEntryNotFound"; 0 → "ReadSuccess"; other negatives → "ReadFailed" (0x643600..0x643692). The `std::function` +0x58 is invoked `(buffer.data, buffer.size, result)` only when non-empty; an empty function means the caller's +0x54 vector was the sink and only it is filled (0x6436B6..0x643714; 2c-2). When +0x71 is set it re-chunks the buffer into 0x400 blocks: `BroadcastNVStorageOpResult(tag, op 0, result = 3 per non-final chunk / 0 for the final, index byte, data)` (0x643718..0x6437D4). Then SetState(0) clears +0x48/+0x1C/+0x78 (0x6437EA). | 1g (pass 4), 2c-2, 13 (pass 1) |
| M3-031 | **Retry and timeout.** A negative result is retried only for {−8, −7, −5, −4}; `ResendLastCommand` (0x645C6A..0x645D7A) resends the identical +0xDC command: the counter +0xF4 is 0-based (reset by the send/arm at 0x645484), incremented then compared `< +0xF5 = 8` (`bhs`), so **7 resends / 8 transmissions**; then "ReadOpFailed" and completion with the original result (0x6431E6..0x643206; the second caller is the write/erase path at 0x643194..0x6431A2). The state-2 timeout fires when +0x78 is set and robot+0x2C > +0x74: "Update.ReadTimeout", callback `(nullptr, 0, −4)`, SetState(0); **no retry on timeout** (0x64575A..0x6457C0). | 1h-1..1h-3 (pass 4), 14/15 (pass 1) |
| M3-032 | **Dispatch gating in `Robot::Update`** (called at 0x51416A). CozmoEngine::Update only updates robots in state 3 (Running) and only after UiMessageHandler::Update returns 0 (0x4ED4DE..0x4ED5CC); a 5 s SyncTimeAck watchdog warns but does not return (0x513BF6..0x513C5A); Gate A: robot+0x34E == 0 (no first full RobotState after SyncTimeAck) returns and skips NV (0x513C5C..0x513C62); Gate B: once a calibration exists, a failing UpdateAllResults returns and skips NV (0x513C6E..0x513CBC). Replies are handled in the message pump, before UpdateAllRobots, so they are not gated. | 1a..1j (pass 2) |
| M3-033 | **The connection-time NV queue.** The Robot constructor queues 12 reads before the CameraCalib read — ProgressionUnlock 0x182000, Inventory 0x195000, FaceAlbum 0x184000 and 0x183000, and the 8 backup reads 0x180000/0x181000/0x182000/0x183000/0x184000/0x194000/0x195000/0x196000 — then the CameraCalib read, then Lab 0x196000 and Needs 0x194000. Ready-to-stream (the CB22 one-shot, robot+0x2A) fires only after the whole queue drains (ProcessOnIdleCallbacks returns while the deque is non-empty). | 2a..2m (pass 3) |
| M3-034 | **The queued reads' callbacks and data sinks** (interfaces to the higher layers). ProgressionUnlock (vtable 0x102F6B8 slot +0x14 = 0x64CE34): on ≥ 0 unpack `UnlockedIdsList` and install ids, else on −1 install the built-in defaults and notify the game. Inventory (0x102EF3C +0x14 = 0x63D7C4): sets Inventory+0x104 = 1, unpacks, `SendInventoryAllToGame`, and requests default sparks on −1. FaceAlbum 0x184000 (empty callback) fills VC+0x2F4; 0x183000 (0x102F914 +0x14 = 0x65A860) calls `SetSerializedFaceData(VC+0x2F4, VC+0x300, …)` and `BroadcastLoadedNamesAndIDs`. RDBM (0x101FD38 +0x14 = 0x51DF34): stores each (tag, bytes) in a map, unpacks `OnboardingData` for 0x181000, and writes the backup file after the last read. Lab (0x10317B8 +0x14 = 0x6A6486 → 0x6A5C34): unpacks and restores active experiments. Needs (0x1031098 +0x14 = 0x69BEB2): `FinishReadFromRobot` (version-gated unpack), then `InitAfterReadFromRobotAttempt`. | 2a..2g (pass 4), 2c..2j (pass 3) |
| M3-035 | **No callback on disconnect or destruction, and the timeout needs the state clock.** `~NVStorageComponent` frees/destroys the queued and pending requests without invoking their functions (0x643E80..0x643F8C); the timeout clock robot+0x2C only advances with a RobotState, so a stopped robot never times a read out either. (This part is already built in `NvStorageComponent.OnDisconnected`; it needs confirmation, not a new mechanism.) | 4e..4g (pass 2) |
| M3-036 | **HARDWARE_ONLY:** the robot's reply to a factory read with Length = 1, and the wire contract of the non-factory `Length = size + 16` re-request (does the robot resend from index 1 / from byte size+16?). The firmware bodies are encrypted and no official-app NV capture exists. The one bundle shows only how the robot answered Length = 1024 (indices 0..7 and 15). | 17 (pass 1), open q5 (pass 4) |

**M3-022 provenance fix.** Replace "the NV wire exchange is a separate component (NvStorage.cs, recorded as M11-011)" with a pointer to M3-025..M3-036. Its callback claims are unchanged and confirmed by pass 4 Q3.

The `// fidelity:` tags for M3-025..M3-036 are added when the build batch settles each record (the checker requires a tag only for a settled record, or at acceptance).

**Status after the batch-A build (2026-09-26).** M3-025, M3-026, M3-027, M3-028, M3-029, M3-030, M3-031 and M3-035 are settled **EXACT_SOURCE**: the NV wire core in `NvStorage.cs` reproduces them, independently verified (verifier PASS on the batch and on the two correction hunks). M3-032 (dispatch gating), M3-033 (the connection queue) and M3-034 (the read callbacks) remain IMPLEMENTATION_GAP for the next batch. M3-036 stays HARDWARE_ONLY.
