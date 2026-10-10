# M3/M4 cross-layer splits (extractor draft, 2026-10-10, HEAD f56329c)

Scope: every record of M3-device and M4-control whose status is IMPLEMENTATION_GAP in `re-analysis/fidelity_manifest.json`
at HEAD f56329c (32 records). Inputs: the manifest, `jobs/B-M3M4.md` "Settling pass" and later sections, the code under
`cozmo-stack/src/` (read only to judge the handoff points), and libcozmoEngine.so. Every boundary address below was re-read
in the .so for this draft (Thumb disassembly; PLT stubs resolved through the JUMP_SLOT relocations). Where a claim rests only
on the record's own evidence it says so.

Quotes: title and status are verbatim. Evidence is verbatim unless marked "(abridged)". `unresolved` is quoted only for its
operative sentences (the full text is in the manifest).

KIND: (a) SPLIT, (b) IN-LAYER, (c) HARDWARE. "Settle as-is" judges the narrowed record against the current code: its M3/M4
part must match the binary and the handoff must be a visible interface (a hook or a MISSING line that names the owning record).

## 0. Findings that cut across many records

### 0.1 Robot::SendMessage's failure warning is missing on most robot sends (IN-LAYER, blocks settling)

The engine sends through `Robot::SendMessage` (PLT 0x004A5368), which logs `Robot %d failed to send a message type %s` on every
failure (M4-020's evidence, 0x005134F4/0x0051356C). In the stack only `EngineRobot.Send` (CozmoEngine.cs:1008-1022) emits that
warning; it serves SendSyncTime and, through `SendChecked`, AbortAll. Every other send goes through `CozmoRobot.SendMessage`
(CozmoRobot.cs:455) -> `Engine.SendMessage` -> `MessageHandler.SendMessage` (CozmoEngine.cs:543-549), which returns false silently.

Engine call sites re-read for this draft, all `blx 0x4A5368`:
- SendBufferedMessages (animation stream drain, M3-013): 0x0057BFAA
- VisionComponent::EnableColorImages (M3-023): 0x00658314
- NVStorageComponent request dispatch (M3-026/M3-031/M3-043): 0x0064539E
- BodyLightComponent::SetHeadlight (M4-017): 0x00632380
- DockingComponent::AbortDocking (M4-026, already routed through SendChecked): 0x0063BE36

Affected stack call sites: Camera.cs (`_send`, bound to CozmoRobot.SendMessage at CozmoRobot.cs:244), NvStorage.cs:647, 816, 1083,
Animation/CozmoAnimations.cs:39 and 176, Motion.cs:275, 464, 559, 909, 925, 938, 955, 970, 989, 1264 and the 1048 fallback, Lights.cs
(5 sites), Sensors.cs (4), Cubes.cs (2), CubeAccel.cs (2). The queued item "CozmoMotion's older sends don't go through the
SendMessage failure warning" understates this: it covers M3 as well. Each remaining call site needs its engine sender confirmed
(the five above are confirmed).

### 0.2 Code carrying an M3/M4 tag that belongs to a higher layer (retag before settling)

| Code | Tag now | Engine function | Owner |
|---|---|---|---|
| Actions/IActionRunner.cs:195, 278, 337, 382, 398, 412, 421 (timeout warning +0x57, SetMotionProfile.Unused, AlreadyPrepped, Cancel, SetTag) | M4-016 | IActionRunner/IAction base: 0x00540370..0x0054063E, 0x00540D1C..0x00540F9E, 0x005409DC..0x00540A46, 0x00540098..0x00540192 | M8-015 |
| Motion.cs:1426-1560 `PathComponent` (Abort 0x00649100..0x006491BC, ClearPath 0x00649220..0x006492A0) | M4-026 | PathComponent | M13-029 |
| CozmoEngine.cs:955-968 `ConstructorDelocalize` (a partial Robot::Delocalize: RobotDelocalized, origin add) | M4-020 | Robot::Delocalize 0x00510A24 from the ctor 0x00510324 | M11-019 / M11-053 |
| Vision/VisionSystem.cs:310-335 and :938 (mode-14 queue, drain, EnablingMode/DisablingMode) | M4-017 | VisionSystem::SetNextMode 0x006B2282, EnableMode 0x006B19C0, the Update drain 0x006B4FB6..0x006B4FE4 | NEW M11 (S-6 below) |
| CozmoEngine.cs:1147-1163 Gate B (UpdateAllResults hook and MISSING) | M8-011 | Robot::Update 0x00513C6E..0x00513C9A | the gate is M3-032; the result's source is M11-035 |
| Motion.cs:1340, 1383 (lift destructor, MotorActionAcked) | M4-028 | MoveLiftToHeightAction generic | M4-016 |
| Camera.cs:78-99 `CameraFrame.PresentationJpeg/Save` (StbImageWriteSharp, tool callers only: Conformance Devices.cs:157, VisionTool.cs:171) | M3-018 | EncodedImage::Save, unreachable (B-M3M4 "JPEG unblocked") | NEW M3 policy (I-2 below) |

Outside the 32 (reported, not judged): Actions/ActionList.cs, ActionQueue.cs, ActionWatcher.cs carry `fidelity: M7-020`
("Mood event production, repetition penalty and affector application") although they implement M8-015's ActionList/ActionQueue
rows (research/20261004-actionlist-extraction.md).

### 0.3 Two records claim the same engine code

| Engine range | M3/M4 record | Other record (current title, status) | Proposed owner of the overlap |
|---|---|---|---|
| PopRobotAudioMessage 0x00597DB4..0x00597E8E | M3-010 (C5 0x00597DD4..0x00597E4E) | M6-016 "The engine robot-audio path: alternatives drawn up front, wall-clock posting, event_volume per playing id, routing 7..10 to Robot_Bus_1..4, queued callbacks, states, PopRobotAudioMessage, abort", IMPLEMENTATION_GAP | M3-010: the encode loop 0x00597DFC..0x00597E34 and the AudioSample build 0x00597E42..0x00597E52. M6-016: the state gate (ldrb [+0x3C]; cmp #3, 0x00597DD4..0x00597DDA), PopNextAudioFrameData (0x00597DE0), the event-index tail 0x00597E56..0x00597E8E |
| ShouldProcessAnimationFrame 0x0057CC6C..0x0057CCA6 | M3-013 (C15) | M5-018 "Frames per Update while ShouldProcessAnimationFrame: empty buffer and keyframes left, or audio ready while audio exists", IMPLEMENTATION_GAP | M5-018 |
| HandleFirmwareVersion 0x005368F4..0x0053698E | M3-024 | M10-010 "The physical flag robot+0x14: 0 until FirmwareVersion, then json["sim"].isNull(); its gates", IMPLEMENTATION_GAP | M3-024: the source value up to the SetOutputSource call 0x0053698E. M10-010: the Robot::SetPhysicalRobot call 0x00536980. The shared parse steps (cbz 0x00536932; the LogicError throw 0x008EADC2..0x008EAE64) need one owner: manager |
| +0x48 enabled store 0x0065AE80, callback 0x0065AB68 | M3-022 | M11-049 "The enabled flag: images are dropped until the NV calibration read callback has run", IMPLEMENTATION_GAP | M3-022: the store. M11-049: the readers (the image drop) |
| HandleActiveObjectConnectionState 0x00533B3C..0x00533C34 (LC8a slot > 4 at 0x00533B56) | M4-009 | M11-041 "BlockWorld::AddConnectedActiveObject and its caller HandleActiveObjectConnectionState", IMPLEMENTATION_GAP | M11-041 for AddConnectedActiveObject (activeID >= 5 rejection 0x00623040); manager to decide whether the handler's slot gate stays M4-009 |
| MoveHeadToAngleAction 0x00547E40..0x005488B8 | M4-001 (MA9), M4-016 | M13-022 "TurnInPlaceAction and MoveHeadToAngleAction", IMPLEMENTATION_GAP (a second, not-live C# class in Vision/PanAndTiltActions.cs) | M4-001/M4-016; M13-022 should stop at its call into the action |
| Robot::AbortAll 0x0051194C..0x0051198C | M4-026 | M5-022 "Disconnect and teardown: a failed send only returns; ~Robot AbortAll cancels actions, aborts path and docking, sends AbortAnimation 0x8D and StopAllMotors", IMPLEMENTATION_GAP | M4-026 owns AbortAll's order and result; M5-022 keeps Robot::SendAbortAnimation (called at 0x00511972) and the failed-send behaviour |
| PathComponent::Abort/ClearPath 0x00649100..0x006492A0 | M4-026 (rows A3/A4) | M13-029 "Path/planner abort and destruction", RECOVERABLE_GAP | M13-029 (rows exist: A3/A4 with corrections) |
| Sleep factory 0x0052CEA2..0x0052CFC0 (lift child 0x0052CF8E..0x0052CFB0 inside it) | M4-028 | M5-038 "CreateGoToSleepAnimSequence animation factory boundary", RECOVERABLE_GAP | M4-028: the child ctor call 0x0052CFA2 and the Preset ctor body 0x00548B84. M5-038: the factory. M8-017: AddAction 0x0052CFB0 and execution |
| Gate B 0x00513C6E..0x00513C9A | M3-032 | M8-011 (tag at CozmoEngine.cs:1147; the record's unresolved names 0x00513c6e..0x00513c9a) | M3-032 owns the gate; M11-035 owns UpdateAllResults' result |
| FaceAlbum/FaceEnrollment read callbacks (0x0065A860; ReadFaceEnrollData logs 0x0065A8F0/0x0065A916) | M3-034 | M14-012 "Face album NV persistence, loaded-name replay and enrollment completion", IMPLEMENTATION_GAP | M14-012 |

## 1. Per record

### M3-001

| | |
|---|---|
| Title | JPEG reconstruction headers (gray 324 B, colour 334 B 4:2:2), height/width BE at 0x5E..0x61, 240x320 decode check, frame timestamp = last chunk |
| Status | IMPLEMENTATION_GAP |
| Evidence | "A13 header tables 0x00C48C40 (gray, 0x144 B) and 0x00C48D84 (colour, 0x14E B): SOF0 at 0x59, 1 or 3 components (Y 0x21, Cb/Cr 0x11)"; "A12 MiniToJpegHelper writes height BE at 0x5E/0x5F and width BE at 0x60/0x61 (0x004F31C4..0x004F32CC)"; "A8 gray dispatch tbh 0x004F2898; A11 rows==240, cols==320 else BadDecode (0x004F2CDA..0x004F2D34); ts = EncodedImage+0xC" |
| Unresolved (operative) | "built, awaiting strong verification: B-M3M4 JPEG decode port ... MISSING (not in the inventory): (1) PINNED: other registered OpenCV decoders ...; (2) PINNED, M11: what VisionComponent does with a cv::Exception ...; (3) the J38 timestamp copy into the output Image (+0x3C) has no GrayImage counterpart ...; (4) the EXIF orientation lookup (fopen of the empty filename, J53) is not performed" |
| KIND | (a) SPLIT, plus IN-LAYER remainder |
| Narrowed M3-001 | "Camera frame reconstruction and gray decode: mini-JPEG headers, BE size fields, DecodeImageHelper<Image> dispatch, the libjpeg 9 / OpenCV 3.1 imdecode port, the 240x320 check, DecodeImageHelper's logs." Keeps all three evidence lines plus the JPEG-port rows (jobs/B-M3M4.md "JPEG unblocked", research/20261010-jpeg-lse-logs-oom-rows.md). Handoffs: (i) a cv::Exception leaves DecodeImageHelper at the imdecode call 0x004F28C6, whose landing pad 0x004F2EA0 only cleans up and ends in _Unwind_Resume (M11-058's evidence): owned by M11-058; (ii) the decoded Image, with the +0x3C timestamp copy, goes to VisionSystem::Update(PoseData, ImageCache) 0x006B4C78: owned by M11-033 |
| Higher-layer records | M11-058 exists: "cv::Exception from the image decode: where it is caught and what happens to the frame", RECOVERABLE_GAP; cite it. M11-033 exists: "The image hand-off and the two VisionSystem::Update overloads", IMPLEMENTATION_GAP (its unresolved already asks for "the timestamp copy"); cite it for MISSING (3) |
| In-layer remainder | NEW M3 record (I-1): imdecode's decoder choice for a payload in encodings 5/6/7 that is not a JPEG (findDecoder over the decoders registered in the shipped libopencv_imgcodecs.so), RECOVERABLE_GAP; whether the robot ever sends 5/6/7 is HARDWARE_ONLY unless the robot firmware can be read (not checked here). MISSING (4): fopen("") is a bionic call that fails; recording it as a system-library boundary is enough. O8 (libopencv_java3.so JUMP_SLOT 0x7A9E4, cv::redirectError) stays a pinned in-layer read |
| Settle as-is | No. OpenCvFailed (Vision/Jpeg/EncodedImageDecode.cs:100-104) catches the cv::Exception and returns a failed decode: the handoff is a substitution that a code comment calls "an M11 matter"; nothing at run time names M11-058. Camera.cs:129 says the same in a doc comment only |

### M3-010

| | |
|---|---|
| Title | encodeMuLaw(float) exactly; no volume scaling; short frames zero-padded |
| Status | IMPLEMENTATION_GAP |
| Evidence | "C6 encodeMuLaw 0x00597AD8..0x00597B8E, segment table 0x00C5C3F0, 32767.0 at 0x00597C18; NaN warns and gives 0"; "C5 PopRobotAudioMessage 0x00597DD4..0x00597E4E: zero-pad below 744"; "C7 no volume scaling (0x00597DFC..0x00597E02)" |
| Unresolved (operative) | "MISSING: the actual M6 Wwise rendered-float producer still supplies short[]; the legacy short-source path remains explicitly a candidate, not an exact float producer." |
| KIND | (a) SPLIT |
| Narrowed M3-010 | "encodeMuLaw(float) and the AudioSample frame: per-sample encode, zero pad to 744, no volume." Keeps C6, C7, and C5 limited to 0x00597DEA..0x00597E52: the loop `ldr r0,[r0,r6]; blx 0x4AF5BC` (encodeMuLaw) 0x00597DFC..0x00597E12; the pad test `cmp.w r1,#0x2E8; bhs` 0x00597E16 and memclr 0x00597E34; the AudioSample ctor `blx 0x4ADCB4` 0x00597E4E. Handoff: `blx 0x4AF5B0` = RobotAudioBuffer::PopNextAudioFrameData at 0x00597DE0 (the producer of the float vector), behind the state gate `ldrb.w r0,[fp,#0x3C]; cmp r0,#3; bne` 0x00597DD4..0x00597DDA |
| Higher-layer records | M6-016 (title in 0.3, IMPLEMENTATION_GAP) owns the gate, PopNextAudioFrameData and the tail 0x00597E56..0x00597E8E. The producer filling RobotAudioBuffer is the Hijack: M6-015 "The Hijack plug-in: registration (last caller wins), Init (1024 floats/channel, resampler 22320, pitch 0), Execute to 744-sample chunks, Term", IMPLEMENTATION_GAP. No new record |
| Settle as-is | No. No IAnimationFloatAudioSource implementation exists (WavAudioSource and WwiseAudioSource implement only the short interface: Animation/AnimationAudio.cs:218, Animation/Wwise/WwiseAudioSource.cs:21), so the live encode is AnkiMuLaw.Encode(short) (Animation/AnimationScheduler.cs:1714-1720), which is not the engine's. The handoff is a code comment ("MISSING: the M6 Wwise renderer still supplies shorts", :1678) that names no record and emits nothing |

### M3-013

| | |
|---|---|
| Title | The feed: FIFO drain stopping at the first over-budget message; per Update, one 33 ms frame at a time while the drain completes and the audio animation is ready |
| Status | IMPLEMENTATION_GAP |
| Evidence | "C14 SendBufferedMessages 0x0057BF60..0x0057C010"; "C15 UpdateStream 0x0057C8E4..0x0057CABE, 0x0057CC6C..0x0057CCA6"; "C16 readiness 0x0059A1CC..0x0059A1F6, states per Q5 GetStringForAnimationState 0x00596434" |
| Unresolved (operative) | "NOT BUILT (SD3, MD5, M5-018): ShouldProcessAnimationFrame's audio-animation readiness (C15, C16, A15, Q5) is the M6 stand-in (always ready) ... The readiness sink is M6's live voice/mixer (B-M6b-4)" |
| KIND | (a) SPLIT |
| Narrowed M3-013 | "SendBufferedMessages: FIFO drain under the byte and audio-frame budgets, and the stream counters." Keeps C14 0x0057BF60..0x0057C010: stop when size > bytes (`cmp r4,r1; bhi` 0x0057BF96); stop when frames < isAudio (`cmp r0,r5; blt` 0x0057BF9E; isAudio = (tag & 0xFE) == 0x8E, 0x0057BF8C..0x0057BF94); send `blx 0x4A5368` 0x0057BFAA; counters robot+0x23C/+0x244 0x0057BFBC..0x0057BFCC. The per-Update frame loop (C15 0x0057C8E4..0x0057CABE) is already M5-007/M5-008/M5-026's (the record's own provenance says so) |
| Higher-layer records | M5-018 (title in 0.3, IMPLEMENTATION_GAP) owns ShouldProcessAnimationFrame 0x0057CC6C..0x0057CCA6, including the audio animation's virtual +8 call `blx r3` 0x0057CC98 and the tail `b.w 0x8CB8AC` (veneer to UpdateAnimationIsReady) 0x0057CCA6. M5-007 exists: "Timeline: start + frames built x 33; advances whenever the drain returns OK (a budget stop included); frozen while the buffer is non-empty", EXACT_SOURCE. NEW M6 record S-1 for UpdateAnimationIsReady |
| Settle as-is | No. The drain's send has no Robot::SendMessage failure warning (0.1; Animation/CozmoAnimations.cs:39). With that fixed, the narrowed C14 slice is all that remains; its budgets are clamped at 0 (AnimationScheduler.cs:139-151), so the signed `blt` against a negative budget cannot arise |

### M3-018

| | |
|---|---|
| Title | Colour decode: half-width JPEG, BGR to RGB, cv::resize INTER_LINEAR to 320x240; IsColor; Save at quality 90 |
| Status | IMPLEMENTATION_GAP |
| Evidence | "A9 RGB dispatch tbh 0x004F21AE, case 9 MiniToJpegHelper(h, w/2, 0x00C48D84), imdecode(1), cvtColor(4) (0x004F237E..0x004F2440)"; "A10 Resize -> cv::resize(..., m) with m = 1 = INTER_LINEAR (0x00870486..0x008704D6)"; "A7 IsColor tbb 0x004F2110 (0x004F2102..0x004F211E)"; "A25 EncodedImage::Save quality 90 (movs r2,#0x5a; 0x004F2EFC..0x004F2FA8)" |
| Unresolved (operative) | "EncodedImage::Save (quality 90, the JPEG encoder) is out of scope as unreachable ... CameraFrame.PresentationJpeg still writes through StbImageWriteSharp and its bytes are not the engine's." |
| KIND | (a) SPLIT, plus IN-LAYER |
| Narrowed M3-018 | "Colour decode and IsColor: DecodeImageHelper<ImageRGB> dispatch, imdecode(1), cvtColor 4, the half-width resize, IsColor, the RGB logs." Keeps A9, A10, A7. A25 leaves (unreachable; the title drops "Save at quality 90"). Handoffs: the cv::Exception (M11-058, as for M3-001); the caller VisionSystem::Update 0x006B4B68 (IsColor 0x006B4B7C, DecodeImageRGB 0x006B4B90): M11-033 |
| Higher-layer records | M11-058, M11-033 (quoted under M3-001). No new higher record |
| In-layer remainder | NEW M3 COMPATIBILITY_POLICY (I-2): CameraFrame.PresentationJpeg/Save (Camera.cs:78-99) is a tool convenience with non-engine bytes, called only from Conformance (Devices.cs:157, VisionTool.cs:171); the same shape as M3-044. Its `fidelity: M3-018` tag moves to it. I-1 applies as for M3-001 |
| Settle as-is | No. The same cv::Exception substitution as M3-001, and the M3-018 tag sits on the tool-only Save code |

### M4-001

| | |
|---|---|
| Title | Head angle limits -0.436332..0.776672 rad: command clip in MoveHeadToAngleAction, state-side clamp, -25 deg before calibration |
| Status | IMPLEMENTATION_GAP |
| Evidence | "MA9 MoveHeadToAngleAction ctor clip with warnings 0x00547F44..0x0054803A"; "MA22 Robot+0x2FC = -0.436332 in the ctor (0x0051007C..0x00510086)"; "MA23 RS6 SetHeadAngle 0x0051335E..0x005133E8 (M2 interface)" |
| Unresolved (operative) | "built, awaiting strong verification: B-M3M4 batch 1 (2026-10-06). Checked Opus clip-warning text and degree fields repaired ... No settlement." |
| KIND | (b) IN-LAYER |
| Work | (1) The two clip warnings render %.1f (Motion.cs:749, 755, `{degrees:F1}`) with no `fidelity: M4-032` tag; M4-032 "Phone printf rendering of %f / %.1f / %.2f in engine logs", EQUIVALENT_IMPLEMENTATION, is the boundary to cite. (2) Resolve the duplicate MoveHeadToAngleAction in M13-022 (0.3) |
| Settle as-is | No, for (1) only; then yes on the existing checks (the math found bit-exact on 2026-10-02, the text repaired 2026-10-06) |

### M4-002

| | |
|---|---|
| Title | Lift presets 0 LowDock 32 / 1 HighDock 76 / 2 HeightCarry 92 / 3 OutOfFOV -1; clamp to 32..92; a negative height goes to the nearer of 32 and 92 |
| Status | IMPLEMENTATION_GAP |
| Evidence | "MA14 preset table 0x00C54684, names 0x00548EBC..0x00548EDC"; "MA13 MoveLiftToHeightAction::Init clamp 0x0054905E..0x005490F6, negative height 0x005490FA..0x00549140"; "C5: a negative height picks 32 only when strictly nearer; a tie goes to 92 (0x00549104..0x0054913C)" |
| Unresolved (operative) | "REOPENED: MoveLiftToHeightAction::Init (0x0054903C..0x005490F6, 0x00549104..0x0054913C) does the clamp, the InvalidHeight warning ... and the negative-height preset choice at Init time ... Init's second clamp into [32,92] (0x0054918E..0x005491D2) maps NaN to 32; missing in the C#." Stale: fix round S1 items 4-5 built it and the Opus check found the code supported |
| KIND | (b) IN-LAYER |
| Work | Refresh `unresolved` for S1. Record the unbuilt variability step (S1 queue: "every live caller passes 0 (0x0054914A)") as unreachable on the live path, with its caller list. The MoveLiftToHeight send in Init is M4-016's |
| Settle as-is | Yes, once `unresolved` records S1 and the variability step; its path has no cross-layer handoff |

### M4-003

| | |
|---|---|
| Title | The head and lift API follows the game-message path: caller speed/accel/duration; the original app passes head 10/20 and lift 10/20, duration 0 |
| Status | IMPLEMENTATION_GAP |
| Evidence | "MA10 game SetHeadAngle overwrites +0x90..+0x98 with the message values (0x0052ABE0..0x0052AC24)"; "MA11 unity/scripts/csharp/Robot.cs:1443-1450 (head 10, 20, 0) and 1638-1646 (lift 10, 20, 0)"; "MA12 game SetLiftHeight: 32.0 while carrying -> PlaceObjectOnGroundAction (0x0052AC40..0x0052AC9E)"; "MA9, MA13 action ctor defaults (head 15/20 at 0x00547F14..0x00547F28; lift 10/20 at 0x00548A68..0x00548A78)" |
| Unresolved (operative) | "DEFECT ... the game path's ActionQueue::QueueNow replacement (0x0053E24C..0x0053E35E) is absent, so a repeated game move fails 0x03000019" (stale: Motion.cs:826-830 now queues on the engine ActionList and Actions/ActionQueue.cs:90-94 implements QueueNow) |
| KIND | (a) SPLIT |
| Narrowed M4-003 | "The game SetHeadAngle/SetLiftHeight factories: the message fields into the action, the 32.0-while-carrying switch." Keeps MA10, MA12, MA9/MA13 defaults. Re-read: head `blx 0x4A94FC` MoveHeadToAngleAction ctor 0x0052AC18, then the `ldm/stm` of msg+4..+0xC into action+0x90 0x0052AC1C..0x0052AC24. Lift: `vcmp.f32 s16,s0` against 32.0 0x0052AC54; the carried test `ldr.w r0,[r5,#0x284]; ldr r0,[r0,#8]; adds r0,#1; beq` 0x0052AC5E..0x0052AC66; `blx 0x4A940C` PlaceObjectOnGroundAction ctor 0x0052AC72; otherwise `movt r3,#0x40A0` (5.0) and `blx 0x4A9508` MoveLiftToHeightAction(h, 5.0, 0) 0x0052AC92, then msg+4..+0xC into +0x88..+0x90 0x0052AC96..0x0052AC9E. The factories return the action; queueing is the caller's (ActionList::QueueActionNow 0x0053DCA0) |
| Higher-layer records | M8-015 exists: "ActionList/ActionQueue/IAction lifecycle, cancellation and Robot teardown", RECOVERABLE_GAP (the queueing). PlaceObjectOnGroundAction: M15-022 exists: "PlaceObjectOnGroundAtPoseAction composition and PlaceObjectOnGroundAction Init/CheckIfDone", IMPLEMENTATION_GAP, and M12-015 exists: "PlaceObjectOnGround sends three zero offsets and a constant speed triple", EXACT_SOURCE. The carried ObjectID [robot+0x284]+8 (CarryingComponent): M12-008 exists: "The pose chain that holds a carried object", IMPLEMENTATION_GAP; the manager should confirm it owns CarryingComponent+8 |
| Open | MA11 is an app input (the Unity defaults). Under "Scope: the app boundary" the C# API's 10/20/0 defaults look like a COMPATIBILITY_POLICY naming that input, not part of an EXACT record. Manager decision |
| Settle as-is | No: the stale DEFECT text, the MA11 question, and the carry seam (`IsCarryingObject`/`PlaceObjectOnGroundAsync`, Motion.cs:795) silently runs the lift when it is null |

### M4-008

| | |
|---|---|
| Title | Cliff sensor data (raw values, CLIFF_DETECTED, timestamp, enum names); IMU and cube battery in the engine units |
| Status | IMPLEMENTATION_GAP |
| Evidence | "SC2 UpdateRobotData 0x00634016..0x00634036"; "U1 0x0051293C ldrb [r4,#0x29]; beq.w 0x512A8A (the only gate before the work)"; "U3 0x00512986 ldr.w r0,[r4,#0x288]; 0x0051298C blx UpdateRobotData; origin check ContainsOriginID 0x00512C3E..0x00512C4A runs later"; "U4 0x0063401A..0x00634036 raw cliff to +0xE/+0x12, CLIFF_DETECTED to +6, timestamp to +8"; "re-analysis/research/20261010-power-level-and-connect-log-rows.md A1..A16 and Addendum A13/C1..C7 (manager-adopted 2026-10-10): HandleObjectPowerLevel 0x00537130..0x00537402 - ..." (abridged) |
| Unresolved (operative) | "The title's IMU and cube battery units remain uncited." ... "The DAS event's $data key/value has no sink in this stack (as IHelper's DAS events); visible in the code." |
| KIND | (a) SPLIT (small), plus IN-LAYER |
| Narrowed M4-008 | The title drops "IMU" (raw accel/gyro are M10-012 "IMU filter state starts at 0; raw accel/gyro written only by UFRS", EXACT_SOURCE, and M10-013); "cube battery" is now cited by A1..A16. Handoff: the connected-object lookup at 0x00537360 (GetConnectedActiveObjectByActiveIdHelper 0x0061F720 -> FindConnectedObjectHelper 0x0061F078, per the adopted rows) |
| Higher-layer records | M11-004 exists: "BlockWorld connected-object rule, moving and rotating gates, position-update thresholds", IMPLEMENTATION_GAP; its unresolved lists FindConnectedObjectHelper and "the extra tail ... (0x0061F129..0x0061F2A0)" as open. Cite it |
| Open | The DAS `$data` sink has no record anywhere (no manifest title covers DAS output). A stack-wide record or policy is needed; its layer is the manager's choice |
| Settle as-is | No: the title overclaims; the %.2f renders (Cubes.cs:561) need the M4-032 tag; the no-BlockWorld MISSING (Cubes.cs:589) names the address but not M11-004 |

### M4-009

| | |
|---|---|
| Title | Cube tracking: ObjectAvailable / ObjectConnectionState, Moved / Stopped / UpAxisChanged handling with the charger and carry filters, per-object IsMoving |
| Status | IMPLEMENTATION_GAP |
| Evidence | "S1, S2, CD10a..CD10c, LC8a..LC8e as before"; "C11.1 DockingComponent+0xC is an ObjectID, the object currently being docked with (the dock target): default -1 (0x0063BA1E/0x0063BA2A); only writer DockWithObject (0x0063BAAC/0x0063BAB6); readers include the Moved 0x0053416E/74 and Stopped 0x0053497C/84 exclusions"; "LC8a 0x00533B56 slot > 4 is ignored; AddConnectedActiveObject rejects activeID >= 5 (0x00623040)"; "LC8e SetID gives every ActiveCube of a type the same ObjectID for the process lifetime (0x004EF468..0x004EF52A)" |
| Unresolved (operative) | "MISSING: complete ObjectID versus slot ownership and the <=4 admission in Cubes.Handle remain M11 integration; no duplicate dock field or invented ObjectID introduced." |
| KIND | (a) SPLIT |
| Narrowed M4-009 | "HandleActiveObjectMoved/Stopped/UpAxisChanged: lookup, charger filter, double-tap window, IsMoving on change, the broadcasts." Re-read callees in Moved (0x00533E4C..0x005341BA): BlockWorld::FindLocatedMatchingObjects `blx 0x4A7084` 0x0053401E; CarryingComponent::IsCarryingObject `blx 0x4A6FAC` 0x0053411E and 0x0053417C; ObjectPoseConfirmer::MarkObjectDirty `blx 0x4A7174` 0x0053413C; ObjectMoved build/broadcast `blx 0x4AA108` 0x005341A8, `blx 0x4A5B0C` 0x005341B0; BlockTapFilterComponent::ShouldIgnoreMovementDueToDoubleTap `blx 0x4A7FF0` 0x00534278 (M4-023 "Cube taps: ...", EXACT_SOURCE) |
| Higher-layer records | M11-041 (title in 0.3, IMPLEMENTATION_GAP): AddConnectedActiveObject and its activeID < 5 rule (0x00623040). M11-013 exists: "Observed-object creation upstream of AddAndUpdateObjects: ObjectID assignment, the confirmation gate and the unconnected cube", IMPLEMENTATION_GAP: SetID 0x004EF468..0x004EF526 (LC8e). M11-004 (quoted under M4-008): the located lookups. M11-009 exists: "A cube that reports movement is marked Dirty, unless the robot is carrying it", EXACT_SOURCE: MarkObjectDirty. The dock target DockingComponent+0xC (C11.1): M11-042's unresolved lists "the roles of DockingComponent+0xC" as unread; its writer DockWithObject (0x0063BAAC/0x0063BAB6) is M12's (M12-017 exists: "IDockAction::Init setup, handler registration and the docking squint", IMPLEMENTATION_GAP; the manager confirms, else a NEW M12 RECOVERABLE_GAP) |
| Settle as-is | No. Cubes.cs:522-531 says "NOT REPRODUCED: this stack uses the slot as its BlockWorld ObjectID"; the objectID of every Moved/Stopped/UpAxisChanged broadcast depends on that stand-in, and the comment is not a run-time line naming M11-013/M11-041. The log texts at Cubes.cs:628, 631, 632, 646, 660 were not compared here with the strings at the engine's log calls (sChanneledInfoF 0x00534238/0x005342B2, sWarningF 0x005342FC) |

### M4-010

| | |
|---|---|
| Title | Outbound cube connection: block pool, five slots, SetPropSlot, filter timers, advertisement expiry, slot states, disconnect handling |
| Status | IMPLEMENTATION_GAP |
| Evidence | "CD1..CD9 as before"; "D1 0x005101DE robot+0x490 = 0 at the ctor"; "D2 0x00517DD8 BroadcastAvailableObjects is the whole setter (strb [r0,#0x490]; bx lr)"; "D3/D4 no other store in .text and no caller of the setter anywhere in the shipped engine"; "D5 0x0051419A ObjectUnavailable is broadcast only when +0x490 is set; the node is erased either way (0x00514218)"; "D6 0x0053398E ObjectAvailable is also gated on +0x490 (M4-025)"; "re-analysis/research/20261010-power-level-and-connect-log-rows.md B1..B6 (manager-adopted 2026-10-10): ConnectToObjects pre-loop BlockPool info log 0x005173A6, streams 0x0051720C..0x00517354." |
| Unresolved (operative) | "MISSING: this does not prove full block-filter timers, duplicate-type branch, all slots, expiration/reconnect and diagnostic paths; no unchecked production changes or settlement." |
| KIND | (b) IN-LAYER |
| Work | Live-entry tests for the full claim (the settling pass's "M4-010: live-entry tests for the full connection claim"): the filter timers, the duplicate-type branch, all five slots, expiry/reconnect, the diagnostics, and P19-P22/P28/P29 (the hash table) checked as built. Connect/disconnect into BlockWorld is M4-009/M11-041's |
| Settle as-is | No (tests); also 0.1 for SetPropSlot and the connection sends |

### M3-021

| | |
|---|---|
| Title | Camera exposure and gain: constructor limits, the initial exposure from vision_config.json, DefaultCameraParams handling, the SetCameraSettings range check and send |
| Status | IMPLEMENTATION_GAP |
| Evidence | "1a VisionSystem ctor: max 66, min 1, minGain 0.1, maxGain 4.0, cur 16, gain 2.0 (0x006B002A..0x006B004A); 1b Init changes none (0x006B0658)"; "1f VisionComponent::Init reads ImageQuality.InitialExposureTime_ms into .data 0x01051054 (0x00650DAE..0x00650DCA); vision_config.json:46 = 16"; "1k HandleDefaultCameraParams: no time-sync gate (0x00537114..0x0053712A); needs IsInitialized (0x006B2CD6); min <= init <= max; SetCameraSettings(init, gain) first (0x00657CCA), then SetCameraExposureParams (0x00657D04)"; "1l SetCameraSettings: IsExposureValid/IsGainValid (0x006B9DAA..0x006B9DBE, 0x006B9E80..0x006B9EA8), sends {f32 g, u16 e, false} reliable (0x0065614A..0x00656166)"; "1o the engine never requests DefaultCameraParams" |
| Unresolved (operative) | "(1f) the initial exposure is the constant 16 ... because this stack does not load vision_config.json; (1g) VisionSystem::IsInitialized is taken as always true ... (1d) applying the pending params to the current exposure and gain is VisionSystem::Update's (M11) and is not done." |
| KIND | (a) SPLIT |
| Narrowed M3-021 | "Camera exposure/gain parameters: the VisionSystem ctor limits (1a), HandleDefaultCameraParams (entry 0x00537114, VisionComponent::HandleDefaultCameraParams 0x00657CA0), VisionComponent::SetCameraSettings 0x006560C8 with IsExposureValid/IsGainValid and the SetCameraParams send, VisionSystem::SetNextCameraParams 0x006B229C (queue and warning), VisionSystem::SetCameraExposureParams 0x006B92F0." Keeps 1a, 1k, 1l, 1o (function addresses from the exports). Handoffs: the InitialExposureTime value written by VisionComponent::Init (0x00650DAE..0x00650DCA into .data 0x01051054); VisionSystem::IsInitialized 0x006B2CD6, whose truth comes from VisionSystem::Init 0x006B0658; the consumer of the SetNextCameraParams slot in VisionSystem::Update |
| Higher-layer records | NEW M11 S-3 (VisionComponent::Init and VisionSystem::Init; also M3-033's). NEW M11 S-4 (VisionSystem::Update's application of the pending params, 1d) |
| Settle as-is | No. Camera.cs:579-582 (the constant 16) and :628 (IsInitialized taken as true) are silent stand-ins (doc comments only); SetCameraParams goes out without the 0.1 warning (Camera.cs `_send`) |

### M3-022

| | |
|---|---|
| Title | At connection the NV CameraCalib read is queued; its callback enables vision on every path and on success installs the calibration and starts processing |
| Status | IMPLEMENTATION_GAP |
| Evidence | "1h NVStorageComponent::Read(0x80000001, cb) queued in the connection handler (0x006583E2..0x006583FA); M1 CD21"; "1j callback 0x0065AB68: the Failed, SizeMismatch and Recvd paths all end strb.w #1 at +0x48 (0x0065AE7E/0x0065AE80); distortion zeroed when robot+0x24 <= 6; SetCameraCalibration starts processing (0x0065175E..0x00651766)"; "2a..2f +0x48 is written only by that callback; +0x4B never; +0x49 stays 0" |
| Unresolved (operative) | "DEFECT. The callback order now holds (Camera.cs:998-1021), but the read crosses M3-027's unsettled +0xE8 data" (stale: M3-027 is EXACT_SOURCE since the settling pass, and S1 item 1 added the four callback logs) |
| KIND | (a) SPLIT |
| Narrowed M3-022 | "The connection-time CameraCalib read and its callback: the Failed/SizeMismatch/Recvd logs, the distortion zeroing for body hardware <= 6, the +0x48 store." Re-read handoff: `blx 0x4B3C90` VisionComponent::SetCameraCalibration at 0x0065ADE0, before `strb.w r0,[r5,#0x48]` at 0x0065AE80 |
| Higher-layer records | M11-039 exists: "The calibration-install path into MarkerDetector::Init", IMPLEMENTATION_GAP (VisionSystem::UpdateCameraCalibration 0x006B1E3E ...). M11-049 (title in 0.3): the readers of +0x48 |
| Settle as-is | No. (1) Order: the engine installs (0x0065ADE0) and then stores +0x48 (0x0065AE80); Camera.cs:774-779 sets `VisionEnabled = true` inside the lock and raises `CalibrationInstalled` afterwards, so the enable is visible before the install. Whether a frame can be processed in that window is the verifier's question. (2) The Recvd %f fields (Camera.cs:783-796) call themselves "the phone libc's printf (a boundary)" without the M4-032 tag. (3) The handoff events `CalibrationInstalled`/`VisionEnabledSet` do not name M11-039/M11-049 |

### M3-023

| | |
|---|---|
| Title | EnableColorImages is never sent at connection; it stores and sends the flag; only BehaviorTrackLaser reads it |
| Status | IMPLEMENTATION_GAP |
| Evidence | "A24 EnableColorImages 0x006582CC..0x00658314"; "3a writers 0x00650180, 0x006582D6, 0x00658368, 0x00658F74"; "3b the only reader is BehaviorTrackLaser (0x005FAC64, 0x005FBDAE)" |
| Unresolved (operative) | "NEEDS EXTRACTION. CozmoRobot.StartCamera (CozmoRobot.cs:529-534) sends EnableColorImages and an ImageRequest, and no row covers it." (stale: M3-044 now owns StartCamera as COMPATIBILITY_POLICY) |
| KIND | (a) SPLIT (the reader only), plus IN-LAYER |
| Narrowed M3-023 | The path is unchanged: `strb.w r1,[r0,#0x32A]` 0x006582D6; EngineToRobot(EnableColorImages) `blx 0x4BA824` 0x00658308; `blx 0x4A5368` 0x00658314. The reader is a handoff |
| Higher-layer records | NEW S-10: BehaviorTrackLaser's read of VisionComponent+0x32A (0x005FAC64, 0x005FBDAE) and its effect; M7-behaviour or M15-freeplay (manager); RECOVERABLE_GAP. Nothing in the manifest owns BehaviorTrackLaser (M15-008 only mentions TrackLaser among activities) |
| Settle as-is | No, for 0.1 only (Camera.cs:656-660 sends through CozmoRobot.SendMessage). `unresolved` should also replace the StartCamera text with a pointer to M3-044 |

### M3-024

| | |
|---|---|
| Title | The audio output source comes from the firmware version JSON: "sim" null means physical, play on the robot |
| Status | IMPLEMENTATION_GAP |
| Evidence | "C1 HandleFirmwareVersion 0x00536934..0x0053698E ("sim" at 0x00536A4C)"; "C2 CreateAudioAnimation 0x0059A070..0x0059A0B6" |
| Unresolved (operative) | "CreateAudioAnimation ... is branched at the call site ... Still open: RobotAudioAnimationOnDevice's device playback (Dispatch::After 0x005975A2) and InitAnimation's per-keyframe audio-reference RNG draws (0x00596814) are not built" |
| KIND | (a) SPLIT |
| Narrowed M3-024 | "HandleFirmwareVersion's output source: "sim" isNull gives SetOutputSource(2), otherwise 1." Keeps C1. Re-read handoffs: `blx 0x4AA2C4` Robot::SetPhysicalRobot 0x00536980 (M10-010) and `blx 0x4AA2D0` RobotAudioClient::SetOutputSource 0x0053698E |
| Higher-layer records | NEW M6 S-2: RobotAudioClient::CreateAudioAnimation 0x00599FF0 (C2): the +0x3C source switch (`cmp r0,#0/#2/#1` 0x0059A074..0x0059A07C), the two constructions 0x0059A082..0x0059A0B6, the post-construction state check `ldrb.w r0,[r6,#0x3C]` 0x0059A0BA (states 4/5), the source-0 clear 0x0059A11C..0x0059A12E (per the record, not re-read). M6-016 (title in 0.3) already owns the OnDevice playback and InitAnimation's draws (its evidence: "InitAnimation 0x0059687E..0x00596914"); cite it. M10-010 owns SetPhysicalRobot |
| Open | The parse steps shared with M10-010 (cbz 0x00536932; a non-object root throws Json::LogicError 0x008EADC2..0x008EAE64; catcher unknown, M1-029 row 11o) need one owner. The MISSING at CozmoEngine.cs:865 names neither record |
| Settle as-is | No, until the overlap with M10-010 is resolved and the LogicError catcher has a record |

### M4-016

| | |
|---|---|
| Title | Head and lift move semantics: no send when in position, ack matched by id, completion in position and stopped, tolerances and error codes |
| Status | IMPLEMENTATION_GAP |
| Evidence | "MA9, MA13, MA15..MA17, C1, C6, C10.1 as before"; "C11.3 the lift CheckIfDone body 0x005493F6..0x00549508 contains no eye-shift removal; the head +0xA8 has only three writers, all zero (0x00547F3C, 0x005484B0, 0x00548724), so H4..H6 never execute" |
| Unresolved (operative) | "MISSING: phone printf exceptional-value spelling, rounding and locale for these diagnostic float fields are not established ... Whole record awaits strong verification." |
| KIND | (a) SPLIT |
| Narrowed M4-016 | "MoveHeadToAngleAction / MoveLiftToHeightAction Init, CheckIfDone and the MotorActionAck handlers: no send in position, ack by id, the latch and has-moved bytes, 0x04000004 / 0x03000016, the u16 diagnostic counters." Handoff: IAction::UpdateInternal (0x00540D1C..0x00540F9E) calls the action's Init and CheckIfDone virtuals; the timer, the 30 s timeout slot, the TimedOut warning (+0x57 test 0x00540E7C, sWarningF 0x00540EB6), Cancel, SetTag and PrepForCompletion are IActionRunner's |
| Higher-layer records | M8-015 (quoted under M4-003, RECOVERABLE_GAP). Its unresolved lists "IActionRunner 0x00540370..0x0054063E; first-clock/timeout 0x00540D4A..0x00540E80", rows exist (research/20261004-actionlist-extraction.md) and the code is built (Actions/IActionRunner.cs), so it reads as IMPLEMENTATION_GAP now (manager). The M4-016-tagged lines in 0.2 move to it. The printf boundary is M4-032 |
| Settle as-is | No: the retag (0.2); the queued TolTooSmall citation (S1 queue); the M13-022 duplicate (0.3); the %.1f diagnostics (Motion.cs:1309, 1322) without the M4-032 tag; and 0.1 (Motion.cs:1264 sends MoveHead/MoveLiftToHeight through CozmoRobot.SendMessage; the engine's sender for these two was not re-read here) |

### M4-017

| | |
|---|---|
| Title | Backpack lights: priority, Off resent every tick while no source, charging state machine, shared locator, wire conversion, headlight |
| Status | IMPLEMENTATION_GAP |
| Evidence | "LB1..LB6, LB4a..LB4i as before"; "E2 0x0063234A..0x00632364 SetHeadlight calls VisionComponent::EnableMode(14, enable) on robot+0x258"; "E3 VisionMode 14 = LimitedExposure (name table 0x01034230, entry 14 -> 0x00C1DC64)"; "E4 0x006527AC..0x006527B6 EnableMode tail-calls VisionSystem::SetNextMode (PLT 0x4BA35C)"; "E5 0x006B2282..0x006B2298 SetNextMode queues (mode,bool) on the deque at VisionSystem+0xB0"; "E6 0x006B4FB6..0x006B4FE4 VisionSystem::Update drains the deque"; "E7 0x006B1A76..0x006B1CC6 EnableMode(14) sets/clears mask bit 14 at VisionSystem+0xAC"; "E8 0x00632368..0x00632380 EngineToRobot(SetHeadlight) then SendMessage reliable, not hot" |
| Unresolved (operative) | "MISSING: E9 LimitedExposure reader/effect remains M11; other mode queue producers and their special FaceTracker recipients are outside this bounded mode14 caller slice" (its NullVisionSystem text is stale: fix round 1 item 3 is applied, Lights.cs:396-412) |
| KIND | (a) SPLIT |
| Narrowed M4-017 | "BodyLightComponent and SetHeadlight." Keeps LB1..LB6, LB4a..LB4i, E2, E3, E8. Re-read handoff: `ldr.w r0,[r0,#0x258]; movs r1,#0xE; blx 0x4A91CC` VisionComponent::EnableMode(14, on) at 0x00632350..0x00632364; then SetHeadlight `blx 0x4B8E38` 0x00632374 and `blx 0x4A5368` 0x00632380 |
| Higher-layer records | NEW M11 S-6: the vision-mode request queue (E4..E7). M11-057 exists: "LimitedExposure: the vision system's reader of the exposure limit (M4-017 E9)", RECOVERABLE_GAP. M11-034 exists: "The vision-mode schedule mechanism", EXACT_SOURCE (the schedule only; not SetNextMode/EnableMode) |
| Settle as-is | No: the VisionSystem code tagged M4-017 (0.2) and 0.1 (Lights.cs:411) |

### M4-018

| | |
|---|---|
| Title | Cube lights: WakeUp on connection and reconnect, layers and PlayLightAnim gates, SetObjectLights / SetLEDs, SetCubeGamma, CubeID, CubeLights, white balance, default-layer anims |
| Status | IMPLEMENTATION_GAP |
| Evidence | "S5..S8, LC1..LC8, LC6, C10.3 rows S3..S7, W1..W5, P1..P5, D1..D5 as before"; "C11.2 robot+0x2C4 is the robot-is-localized flag: default 1 (0x0050FF10), 0 by Robot::Delocalize (0x00510A4A), 1 by SetLocalizedTo (0x0051245E, 0x005124EE) and CheckAndUpdateTreadsState (0x0051217C); the RobotDelocalized +0x21 refresh is held while delocalized and applied when it returns to 1 (gate 0x00637930)"; "S3 ..."; "C12.1 ..."; "C12.2 ..."; "C13.1 ..."; "C13.4 ..." (abridged) |
| Unresolved (operative) | "MISSING: complete M11 default carried/visible selection and ObjectID traversal ownership; absent ObjectInfo single-object handling is not established as unreachable from the replacement API; full runtime Delocalize recipients remain M11." |
| KIND | (a) SPLIT |
| Narrowed M4-018 | "CubeLightComponent: layers, PlayLightAnim gates, SetObjectLights/SetLEDs, the SetLights wire group, WakeUp, EnableGameLayerOnly, the +0x2C4 hold." Re-read handoffs: PickNextAnimForDefaultLayer's `blx 0x4A7114` BlockWorld::GetLocatedObjectByIdHelper 0x00637DB4 (Visible needs the located object's +0x24 == 1); SetObjectLights' located lookup `blx 0x4A7114` 0x00637E82 and connected lookup `blx 0x4A6FB8` 0x00637E96; ActiveCube::MakeStateRelativeToXY `blx 0x4B9288` 0x00637EE8. The +0x2C4 writers are other records' |
| Higher-layer records | M11-004 (quoted under M4-008) for the located/connected lookups (the manager should confirm it covers the ById helpers). M11-053 exists: "Robot::Delocalize callees and the frame-id-mismatch Delocalize trigger (Robot+0x2C0 counter)", IMPLEMENTATION_GAP; M11-044 exists: "PotentialObjectsForLocalizingTo::Insert, UseDiscardedObservation, LocalizeRobot and the confirmed-object pose refresh", IMPLEMENTATION_GAP (SetLocalizedTo); M10-001 exists: "Off-treads classifier CheckAndUpdateTreadsState: gate, inputs, thresholds, branches, 250 ms debounce commit and every consequence", IMPLEMENTATION_GAP |
| Settle as-is | No. `IsVisible` (Lights.cs:578-585) is unset in production and the default layer silently picks Connected; the gap is a doc comment. makeRelative (Lights.cs:950) is a stand-in whose MISSING line names "M11" but no record. 0.1 for the CubeLights/CubeID/SetCubeGamma sends |

### M4-019

| | |
|---|---|
| Title | Cliff: sensor defaults, CliffEvent and PotentialCliff handling, EnableStopOnCliff senders, the threshold schedule 50 / 400 / 150 |
| Status | IMPLEMENTATION_GAP |
| Evidence | "SC1..SC8, SC4a..SC4j, SC6, C2, C7, C8 as before"; "H1 HandleRobotStopped is subscribed for robot tag 0xD4 (0x00532B6E, GOT 0x0103EA8C)"; "H2 0x0053539C..0x0053542E body: Get_robotStopped, sEventF, EvaluateCliffSuspiciousnessWhenStopped"; "H3 0x00634840..0x00634846 Evaluate = comp+0x1C := robot+0x2C"; "H4 0x00535432..0x00535480 if the sensor is enabled: RequestCurrentBehaviorEndImmediately, ActionList::Cancel(-1), broadcast RobotStopped"; S1..S8, W1, W3..W6, C12.3, C12.4, C13.2, C13.3 (abridged) |
| Unresolved (operative) | "MISSING: actual M11 AddRobotStateToHistory/GetLastStateWithFrameID failure results, Delocalize/origin/charger geometry; H4 M7 behavior end and M8 Cancel(-1). Rule10 census also identifies DetectGyroDrift at 00512FBA and DetectBias at 00512FC4 without a live counterpart" |
| KIND | (a) SPLIT |
| Narrowed M4-019 | "CliffSensorComponent: defaults, CliffEvent/PotentialCliff, the EnableStopOnCliff senders, the 50/400/150 schedule with its running stats, HandleRobotStopped's Evaluate and broadcast gate." Re-read handoffs in HandleRobotStopped: the gate `ldrb r0,[r0,#4]; cbz` 0x00535436..0x00535438; `blx 0x4AA1C8` BehaviorManager::RequestCurrentBehaviorEndImmediately 0x00535454; `ldr.w r0,[r4,#0x250]; mov.w r1,#-1; blx 0x4A7ABC` ActionList::Cancel(-1) 0x00535466..0x0053546E; the RobotStopped game message `blx 0x4AA1D4` 0x00535478 and Robot::Broadcast 0x00535480. In UpdateFullRobotState: `ldr.w r0,[r4,#0x27C]; blx 0x4A7C6C` RobotGyroDriftDetector::DetectGyroDrift 0x00512FB4..0x00512FBA and `blx 0x4A7C78` DetectBias 0x00512FC4. The history lookups (C12.4) and the charger geometry are other records' |
| Higher-layer records | M8-015 (ActionList::Cancel(-1)). NEW M8 S-7: BehaviorManager::RequestCurrentBehaviorEndImmediately (no record covers its body; M8-012 "BehaviorManager::Update's tick order, behaviour switching, finishing and the reaction gate" is the tick). NEW M10 S-8: RobotGyroDriftDetector::DetectGyroDrift/DetectBias per state (M10-014 "BlockFilter and gyro-drift lifetime effects", RECOVERABLE_GAP, covers only the destruction). M11-051 exists: "RobotStateHistory::AddRawOdomState gates (drop, consecutive-gap reset, parent check, duplicate key)", IMPLEMENTATION_GAP, and M11-053 (quoted under M4-018) for the history failures and Delocalize. M13-009 exists: "The charger's dimensions, its docked pose and its one pre-dock pose", IMPLEMENTATION_GAP (charger geometry) |
| Settle as-is | No. Sensors.cs:795-806 and :91-98 leave Cancel(-1) and RequestCurrentBehaviorEndImmediately unwired with a comment only, although the engine ActionList exists (EngineRobot.ActionList); nothing is emitted at run time. DetectGyroDrift/DetectBias have no hook. 0.1 for the threshold and EnableStopOnCliff sends |

### M4-020

| | |
|---|---|
| Title | RobotState acceptance: time-sync gate, unknown origin ids dropped; origin 1 from the ctor Delocalize; AbsoluteLocalizationUpdate {0, frame 0, origin 1, identity} at SyncTime |
| Status | IMPLEMENTATION_GAP |
| Evidence | "SC4f UFRS gates 0x0051293C..0x00512942, ContainsOriginID 0x00512BE2..0x00512C4A"; "SC4g PoseOriginList next id = 1 (0x0084792C..0x0084793C); UnknownOriginID = 0 (0x00C97B20)"; "Robot::Delocalize 0x00510A24: AddNewOrigin 0x00510A66, SetNewPose 0x00510B4C, SendAbsLocalizationUpdate 0x00510BF4 when time-synced"; "SC4e robot+0x2B0 = 0 (0x0050FF02, 0x0051032C); SC4h SendSyncTime -> SendAbsLocalizationUpdate (0x0051526E..0x005153AE)"; "C3: ..."; "C9: ... (M11 pose-origin interface; the record claims only the connection-time origin 1 and the gate)" (abridged) |
| Unresolved (operative) | "MISSING: higher-layer history/pose recipients are not settled by this log repair." (S1 item 3 has since built the BadOriginID key and format, CozmoEngine.cs:1061) |
| KIND | (a) SPLIT |
| Narrowed M4-020 | "RobotState acceptance gates and SyncTime's AbsoluteLocalizationUpdate." Keeps SC4f, SC4e/SC4h, C3 and the BadOriginID warning. Handoffs: the ctor's call into Robot::Delocalize (call site 0x00510324 per M11-019's evidence), which creates origin 1; PoseOriginList itself (SC4g); the +0x2C6 send of C9 |
| Higher-layer records | M11-019 exists: "A new pose origin in RobotState delocalizes: located objects are forgotten, carried objects move across", IMPLEMENTATION_GAP (its unresolved: "three call sites: the constructor (0x00510324) ... AddNewOrigin is unbuilt"). M11-053 (quoted under M4-018) owns Delocalize's callees |
| Settle as-is | No: `ConstructorDelocalize` (CozmoEngine.cs:955-968) is a partial Delocalize tagged M4-020 (0.2), and the title's "origin 1 from the ctor Delocalize" is an M11 fact |

### M3-025

| | |
|---|---|
| Title | NV entry-tag validity and the size tables: a valid non-factory tag is a _maxSizeTable key; factory tags take their table value |
| Status | IMPLEMENTATION_GAP |
| Evidence | "IsValidEntryTag 0x644148..0x6441A0: (tag-0x180000)>>14 <= 0x1e; tag != 0x198000; tag a multiple of 0x1000; an exact _maxSizeTable key (descent 0x644160..0x64419E)"; "_maxSizeTable (0x4D7F41; InitSizeTable 0x643B48..0x643CE0): ..."; "_maxFactoryEntrySizeTable (0xC81064, 23 keys, pass 4b Q1): ..." (abridged) |
| Unresolved (operative) | "REOPENED: read-reply acceptance contradicts the engine. It accepts only when +0x78 != 0 and +0x50 == base tag (0x006430BA..0x006430CE), else sWarningF at 0x0064318E ..." (fix round S1 item 2 built this; the Opus check found it supported after three manager fixes, including +0x50 = 0x198000 at 0x00642872/0x0064289E) |
| KIND | (b) IN-LAYER |
| Work | Refresh `unresolved` and `test` for S1. No send and no cross-layer callee in its path |
| Settle as-is | Yes, on the S1 Opus check, once `unresolved`/`test` are refreshed |

### M3-026

| | |
|---|---|
| Title | NV Read(): tag validation, the invalid-tag callback (-6), and the FIFO queue with one request in flight |
| Status | IMPLEMENTATION_GAP |
| Evidence | "Read 0x644E2A..0x644EF4: IsValidEntryTag; invalid warns, optionally broadcasts and calls cb(nullptr, 0, -6) (0x644E8A..0x644EEE); valid emplace_back on the deque +0xF8 (0x644E30..0x644E88)"; "ProcessRequest pops the front, one in flight (0x644FF8..0x645022); Robot+0x2C is the clock (pass 2 4a)" |
| Unresolved (operative) | "MISSING: M3-027 dispatched +0xE8 data; null-%s rendering for an unnamed valid tag." (both stale: M3-027 is EXACT_SOURCE; "(null)" is the manager's ruling, applied in S1 item 7 and covered by M4-032) |
| KIND | (b) IN-LAYER |
| Work | Refresh `unresolved`; cite M4-032 for "(null)"; 0.1: the dispatch send `blx 0x4A5368` 0x0064539E has no failure warning in NvStorage.cs:647 |
| Settle as-is | No, for 0.1 only |

### M3-028

| | |
|---|---|
| Title | NV non-factory reads: the OMZC header check and the Length = size+16 re-request |
| Status | IMPLEMENTATION_GAP |
| Evidence | "header gate 0x6430A6..0x6430EE (index 0, non-factory base, +0x79 == 0); <16 bytes -> TooLittleReadData, result -3 (0x6430F2..0x643478); magic != 0x435A4D4F -> InvalidHeader, result -1 (0x6430FC..0x643112); size > max-16 -> InvalidDataSize, result -1 (0x6430FE..0x64311C)"; "fits -> resize to size+16 (0x6438E2..0x643936); otherwise ReadingRestOfData re-requests tag = the reply's tag, op 0, Length = size+16, reliable/not-hot, no re-arm (0x643840..0x6438CA)"; "the fits-path resize at 0x643922 targets the reply vector (sp+0x44), not the pending buffer; ..." (abridged) |
| Unresolved (operative) | "REOPENED: TooLittleReadData (0xBFBB3F), InvalidDataSize (0xBFBA1C) and ReadingRestOfData (0xBFBABA) print X8 against %x formats; InvalidHeader's format unconfirmed" (S1 fixed all four; the manager fixed TooLittleReadData's Expected literal 1024, 0x00643440) |
| KIND | (b) IN-LAYER |
| Work | Refresh `unresolved`; the queued weak InvalidDataSize test (asserts a prefix, could assert 4096). 0.1 for the ReadingRestOfData re-request send (its sender in 0x00643840..0x006438CA was not re-read here) |
| Settle as-is | No, for 0.1 (to confirm per that sender) and the weak test |

### M3-031

| | |
|---|---|
| Title | NV read retry (7 resends / 8 transmissions, identical resend) and the 5 s timeout (-4, no retry) |
| Status | IMPLEMENTATION_GAP |
| Evidence | "retry set {-8,-7,-5,-4} (0x6431E6..0x6431FA); ResendLastCommand 0x645C6A..0x645D7A: the counter +0xF4 is 0-based (reset by the send/arm at 0x645484), incremented then compared < +0xF5 = 8 (bhs at 0x645C7C), so 7 resends / 8 transmissions, then ReadOpFailed (0x6431FE..0x643234); the second caller is the write/erase path 0x643194..0x6431A2"; "timeout state 2: +0x78 set and robot+0x2C > +0x74 -> Update.ReadTimeout, cb(nullptr, 0, -4), SetState(0), no retry (0x64575A..0x6457C0)" |
| Unresolved (operative) | "MISSING: M3-027 +0xE8 copy/content; other write-recipient backup effects belong to M15." |
| KIND | (a) SPLIT (the backup call only), plus IN-LAYER |
| Narrowed M3-031 | The retry/timeout path is unchanged. Handoff: the backup call after a write-family result (W2 as corrected in jobs/B-M3M4.md: WipeAll when op == 3, otherwise WriteDataForTag(tag, result, op == 1)), reached at 0x00643406/0x00643418 per NvStorage.cs:1243-1244 (not re-read here) |
| Higher-layer records | M15-033 exists: "RobotDataBackupManager: NV write backup, wipe, destruction and the backup reads' sink", RECOVERABLE_GAP. Cite it |
| Settle as-is | No: 0.1 (the resends at NvStorage.cs:816, 1083); the MISSING lines say "M15 recipient" and should name M15-033; the +0xE8 text is stale (M3-027 settled) |

### M3-032

| | |
|---|---|
| Title | NV dispatch is gated in Robot::Update (Running state, a first full state after SyncTimeAck, and a passing UpdateAllResults once calibrated) |
| Status | IMPLEMENTATION_GAP |
| Evidence | "CozmoEngine::Update returns early unless engine+0x10 != 0 and UiMessageHandler::Update == 0 (0x4ED4DE..0x4ED51C); UpdateAllRobots runs only in state 3 Running (0x4ED5C4..0x4ED6BE); SyncTime watchdog 5 s (0x513BF6..0x513C5A)"; "Gate A robot+0x34E (0x513C5C..0x513C62) is set only by UpdateFullRobotState when robot+0x29 (0x51293C..0x512948); Gate B vc+0x28 calibration + UpdateAllResults failure returns (0x513C6E..0x513CBC); replies are not gated (0x52F850..0x52F856)" |
| Unresolved (operative) | "MISSING: M11 calibration/UpdateAllResults Gate B; non-running engine states/UI return belongs to M1." |
| KIND | (a) SPLIT |
| Narrowed M3-032 | "The NV dispatch gates in Robot::Update." Re-read: Gate A `ldrb.w r0,[r4,#0x34E]; cmp r0,#0; beq.w 0x513DA6` 0x00513C5C..0x00513C62; VizManager::SendStartRobotUpdate `blx 0x4A7DA4` 0x00513C6A (out of scope, recorded); Gate B `ldr.w r0,[r4,#0x258]; ldr r1,[r0,#0x28]; cbz r1` 0x00513C6E..0x00513C74, `blx 0x4A7DB0` VisionComponent::UpdateAllResults 0x00513C76, `cbz r6` 0x00513C7C, sWarningF `blx 0x4A4540` 0x00513C92, `b 0x514484` 0x00513CBC |
| Higher-layer records | M11-035 exists: "The VisionComponent per-mode result handlers and the frame broadcast", IMPLEMENTATION_GAP (UpdateAllResults 0x006542EC). The calibration pointer [vc+0x28] is installed on M11-039's path. The engine-state part is M1's (ACCEPTED; the manager names the M1 record) |
| Settle as-is | No: the gate's code is tagged M8-011 (CozmoEngine.cs:1147) and its MISSING text (:1162) names no record; it should carry M3-032 with the hook naming M11-035 |

### M3-033

| | |
|---|---|
| Title | At connection the engine queues 12 NV reads, then the CameraCalib read, then Lab and Needs; ready-to-stream waits for the whole queue |
| Status | IMPLEMENTATION_GAP |
| Evidence | "Robot ctor reads #1 ProgressionUnlock 0x182000 (0x64BEFA), #2 Inventory 0x195000 (0x63CA52), #3/#4 FaceAlbum 0x184000/0x183000 (0x6512F0/0x651330), #5..#12 the 8 RDBM backup reads (0x51AD0A)"; "#13 CameraCalib 0x80000001 (0x6583FA) then Lab 0x196000 (0x6A5B1E) and Needs 0x194000 (0x6944CC) in the mfgId lambda (0x52E3A6..0x52E3B2); ProcessOnIdleCallbacks waits for the deque to drain (0x645B10..0x645B1A)" |
| Unresolved (operative) | "DEFECT. VisionSystem::Init gates the FaceAlbum/Enrollment pair (0x006B0658); CozmoEngine.cs:828-844 queues both unconditionally." |
| KIND | (a) SPLIT |
| Narrowed M3-033 | "The connection-time NV read order and ready-to-stream after the queue drains." Re-read handoff for #3/#4: VisionComponent::Init (0x00650D20) calls `blx 0x4BA200` VisionSystem::Init at 0x00650E36; `cmp r6,#0; beq.w 0x650FB4` 0x00650E3C..0x00650E3E. Non-zero: sErrorF 0x00650E58, the debug-break gate 0x00650E90..0x00650E9A, return 0x00650FB0, no album read. Zero: 0x00650FB4, a further branch at 0x00650FE8..0x00650FFC, then `blx 0x4BA23C` VisionComponent::LoadFaceAlbumFromRobot 0x0065108A, whose body (0x006512A0) issues `blx 0x4A83B0` NVStorageComponent::Read at 0x006512F0 (`mov.w r1,#0x184000`) and 0x00651330. The other branch (0x0065100C..0x00651086: EraseAllFaces 0x00651014, BroadcastLoadedNamesAndIDs 0x00651032) is unread |
| Higher-layer records | NEW M11 S-3: VisionComponent::Init / VisionSystem::Init, with the album load-or-erase branch. M14-012 (title in 0.3) owns the album reads and callbacks. Needs: M15-014 exists: "Needs connection and per-serial persistence lifecycle", IMPLEMENTATION_GAP. Lab: NEW M15 S-11. RDBM reads: M15-033 |
| Settle as-is | No: the unconditional FaceAlbum pair contradicts 0x00650E3C..0x00650E3E; it is not a named handoff |

### M3-034

| | |
|---|---|
| Title | The connection reads' callbacks and data sinks (progression, inventory, face album, backup, lab, needs) |
| Status | IMPLEMENTATION_GAP |
| Evidence | "ProgressionUnlock 0x102F6B8+0x14 = 0x64CE34 (defaults on -1, SendUnlockStatus); Inventory 0x102EF3C+0x14 = 0x63D7C4 (+0x104 = 1, Unpack, SendInventoryAllToGame, RequestDefaultSparks on -1)"; "FaceAlbum 0x184000 empty callback fills VC+0x2F4; 0x102F914+0x14 = 0x65A860 consumes it via SetSerializedFaceData + BroadcastLoadedNamesAndIDs; RDBM 0x101FD38+0x14 = 0x51DF34 (map store, OnboardingData for 0x181000, WriteBackupFile after the last); Lab 0x10317B8+0x14 = 0x6A6486 -> 0x6A5C34; Needs 0x1031098+0x14 = 0x69BEB2 -> FinishReadFromRobot + InitAfterReadFromRobotAttempt" |
| Unresolved (operative) | "Still MISSING: the progression, inventory, backup and lab sinks." |
| KIND | (a) SPLIT, entirely |
| Narrowed M3-034 | No M3 part remains: invoking a callback with (data, size, result) is M3-030's (EXACT_SOURCE). Proposal: retire M3-034 into the records below (manager) |
| Higher-layer records | Progression sink 0x0064CE34: NEW M15 S-9a. Inventory sink 0x0063D7C4: NEW M15 S-9b. Face album 0x0065A860 (and the ReadFaceEnrollData logs already built): M14-012. RDBM sink 0x0051DF34: M15-033. Lab sink 0x006A6486 -> 0x006A5C34, which the exports name CozmoExperiments::RestoreLoadedActiveExperiments: NEW M15 S-11. Needs 0x0069BEB2: M15-014 |
| Settle as-is | Not applicable once retired. M3DeviceTests.M3_034_TheConnectionReadCallbacksReachTheirLayers (M3DeviceTests.cs:3115-3119; the Codex review cited the older :2162-2168) accepts the no-op sinks and should go with it |

### M3-038

| | |
|---|---|
| Title | NV component destruction and pending callback lifetime |
| Status | IMPLEMENTATION_GAP |
| Evidence | "0x0050FD8C..0x0050FD90; 0x005112D6..0x005112E4; Virtual NV deletion, pending requests and callbacks. Constructor/store identity is checked; descendant destructor effects UNKNOWN."; "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt" |
| Unresolved (operative) | "MISSING: persistent saved command +0xE8 is the subsequent adopted N build; backup manager destruction 0x00643F48 remains M15; native captured object final-owner bodies remain their producer layers." |
| KIND | (a) SPLIT (the backup destruction), plus IN-LAYER |
| Narrowed M3-038 | "~NVStorageComponent and the drop of queued/active callbacks without invocation." Handoff: the RobotDataBackupManager destruction reached at 0x00643F48 (per the record, not re-read) |
| Higher-layer records | M15-033 (destructor 0x0051A5F0 in its evidence). The captured objects' own destructors belong to their producers (the M11/M14/M15 records above) |
| In-layer remainder | `test` is empty although tests exist (research/20261010-M3M4-NV-lifetime-build-census.md); the "+0xE8" text is stale (M3-043 built the chunk-loop writes; M3-027 settled) |
| Settle as-is | No: empty `test`, stale text |

### M4-026

| | |
|---|---|
| Title | Robot AbortAll ordered control and wire effects |
| Status | IMPLEMENTATION_GAP |
| Evidence | "0x0051194C..0x0051198C; 0x00649268..0x0064927C; 0x0063BE36; 0x00517E0A; 0x006409C2; Cancel type -1, Path Abort, Dock Abort, AbortAnimation, StopAllMotors in order; OR results without short circuit. Nested stop/direct-drive predicates and recursive callbacks remain required."; "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt" |
| Unresolved (operative) | "RECOVERABLE_GAP -> IMPLEMENTATION_GAP: rows adopted with corrections A3 and A10 (manager, 2026-10-10, jobs/B-M3M4.md); recovered, now to build." (stale: built; fix round 2 accepted, M4AbortSleepTests 45/45) |
| KIND | (a) SPLIT |
| Narrowed M4-026 | "Robot::AbortAll: the order and the result." Re-read: `ldr.w r0,[r4,#0x250]; mov.w r1,#-1; blx 0x4A7ABC` ActionList::Cancel 0x00511956..0x0051195A; `ldr r0,[r4,#0x5C]; blx 0x4A7AC8` PathComponent::Abort 0x0051195E..0x00511960; `ldr.w r0,[r4,#0x280]; blx 0x4A7AD4` DockingComponent::AbortDocking 0x00511966..0x0051196A (send-only: 0x0063BE10..0x0063BE36, which is M12-018 "The dock abort path", EXACT_SOURCE); `blx 0x4A7AE0` Robot::SendAbortAnimation 0x00511972; `ldr.w r0,[r4,#0x254]; blx 0x4A7AEC` MovementComponent::StopAllMotors 0x00511978..0x0051197C; `orr.w/orrs/movne r0,#1` 0x00511980..0x00511988 |
| Higher-layer records | M8-015 (Cancel(-1)). M13-029 (title in 0.3, RECOVERABLE_GAP) for PathComponent::Abort/ClearPath: rows A3/A4 are adopted and the code is built, so it reads as IMPLEMENTATION_GAP (manager). M5-022 (SendAbortAnimation; overlap in 0.3). StopAllMotors: M4-015 "StopHead / StopLift / StopBody: unlock preamble, then MoveHead{0}, MoveLift{0}, DriveWheels{0,0,0,0}", EXACT_SOURCE, and research/20261010-m4-movement-leftover-rows.md row 1 |
| Settle as-is | No: PathComponent carries M4-026 (0.2); `test` is empty although M4AbortSleepTests exist; `unresolved` is stale. The AbortAll body itself was found supported by the fix-round-2 Opus check |

### M4-027

| | |
|---|---|
| Title | Sensor/light/movement/CubeAccel teardown and retained host reset candidate |
| Status | IMPLEMENTATION_GAP |
| Evidence | "0x005111EA..0x0051121A; 0x005113E0..0x005113F6; 0x00511428..0x00511488; 0x00511500..0x00511510; Tap filter, Touch/Cliff, CubeAccel, backpack/cube lights and Movement ownership effects. Existing ResetDevices is a candidate, not evidence of these native effects; queues/subscriptions/virtual descendants UNKNOWN."; "Manager-adopted ..."; "S2 ResetDevices transfer: ..." (abridged) |
| Unresolved (operative) | "RECOVERABLE_GAP -> IMPLEMENTATION_GAP: rows adopted (manager, 2026-10-10, jobs/B-M3M4.md); recovered, now to build." (stale: built; B-M3M4 pins the Touch +0x28C and Cliff +0x288 teardown and their RollingFileLogger close as MISSING lines) |
| KIND | (b) IN-LAYER (the pinned parts are M4 components) |
| Work | NEW M4 record I-3: TouchSensorComponent. It is live, not only a teardown matter. UpdateFullRobotState calls `ldr.w r0,[r4,#0x28C]; blx 0x4A7C00` TouchSensorComponent::Update at 0x00512990..0x00512996, right after the cliff UpdateRobotData (0x0051298C). Update (0x0064E75C) stores RobotState+0 at +4 and the u16 at RobotState+0x58 at +8, then tail-calls Log through the veneer 0x008CCFCC; Log (0x0064E76C) returns unless +0x10 (logging) is set; the ctor (0x0064E738) zeroes +4..+0x14; the dtor (0x0064E746) deletes +0xC through its vtable slot 1 (the logger, present only after StartLogging 0x0064EA44). Call sites from a full .text BL/BLX sweep: ctor 0x0050FE6C; dtor 0x00510766, 0x005113E0, 0x0051171A; Update 0x00512996. UNKNOWN: who reads +4/+8, and StartLogging's callers (not swept). The Cliff component's logger close (+0x288, ~CliffSensorComponent 0x00633FF8) belongs to M4-019 or I-3 (manager) |
| Settle as-is | No: the Touch/Cliff MISSING (Motion.cs:1084) names no record; `test` is empty |

### M4-028

| | |
|---|---|
| Title | Go-to-sleep lift child construction and motor effects |
| Status | IMPLEMENTATION_GAP |
| Evidence | "0x0052CF8E..0x0052CFB0; MoveLiftToHeightAction preset0, tolerance f32 0x40A00000; exact child initialization, locking, stop and completion must be verified in the control layer."; "Manager-adopted re-analysis/research/20261005-B-M1M2-blockers-extraction.md T1–T9 and ownership table; instruction companion re-analysis/research/20261005-B-M1M2-blockers-native.txt" |
| Unresolved (operative) | "RECOVERABLE_GAP -> IMPLEMENTATION_GAP: rows adopted (manager, 2026-10-10, jobs/B-M3M4.md); recovered, now to build." (stale) |
| KIND | (a) SPLIT |
| Narrowed M4-028 | "The sleep sequence's lift child: MoveLiftToHeightAction(Preset 0, 5.0f, 0)." Re-read: `movs r0,#0xA4; blx 0x4A42A0` 0x0052CF8E..0x0052CF90; `movs r3,#0; movt r3,#0x40A0; movs r2,#0; blx 0x4A9A48` MoveLiftToHeightAction::MoveLiftToHeightAction(Robot&, Preset, float) 0x0052CF96..0x0052CFA2 (body 0x00548B84; GetPresetHeight 0x00548C14 and GetPresetName 0x00548CD4 from the exports). Handoff: `blx r7` (the parent's vtable +0x20, AddAction) 0x0052CFB0. Init/CheckIfDone/acks are M4-016's and M4-002's |
| Higher-layer records | M8-017 exists: "Go-to-sleep compound execution ordering and failure propagation", RECOVERABLE_GAP (AddAction and execution). M5-038 (title in 0.3): the factory. The production caller is the MISSING at CozmoEngine.cs:2066 ("CreateGoToSleepAnimSequence (CC8, M5 interface) is not implemented") |
| Settle as-is | No: the generic lift lines at Motion.cs:1340 and :1383 carry M4-028 (they are M4-016's); `test` is empty; the evidence should add the Preset ctor body 0x00548B84 it relies on |

### M3-043

| | |
|---|---|
| Title | NV storage write side: Write(), the WRITE/ERASE/WIPEALL dispatch, the chunk loop, the write timeout and write-result handling |
| Status | IMPLEMENTATION_GAP |
| Evidence | "re-analysis/research/20261010-nv-write-dispatch-rows.md WA1..WA15: ..."; "... WB1..WB21: ..."; "... WC1..WC5: ..."; "... WD1..WD9: ..." (abridged) |
| Unresolved (operative) | "Rows adopted (manager, 2026-10-10, jobs/B-M3M4.md "NV write side"); to build. ... Pinned: callers of the vector overload (narrow b tail calls and ARM code unswept); the RobotDataBackupManager bodies (QueueDataToWrite 0x0051AE04, WriteDataForTag 0x0051B060, WipeAll 0x0051B530) are M15 and stand in as visible MISSING lines." (stale "to build": built and accepted for commit) |
| KIND | (a) SPLIT (the backup bodies), plus IN-LAYER |
| Narrowed M3-043 | The write path is unchanged. Handoffs: QueueDataToWrite 0x0051AE04 (MISSING at NvStorage.cs:675), WriteDataForTag/WipeAll after a result (NvStorage.cs:1243-1244). The callers (CameraCalibration handler 0x005285D6, NeedsManager::StartWriteToRobot 0x006956D4, the face-album writes) are their own records' (M14-012, M15-014) |
| Higher-layer records | M15-033 (quoted under M3-031) |
| In-layer remainder | `test` reads "M3DeviceTests NV write-side cases (to add)."; the vector-overload caller sweep is pinned (a .text BL/BLX sweep like the one used for I-3 would settle the Thumb callers; ARM-mode callers need a separate pass) |
| Settle as-is | No: 0.1 (the chunk sends), the `test` field, and MISSING lines that name "M15 recipient" rather than M15-033 |

### M4-031

| | |
|---|---|
| Title | Track-lock diagnostic enumeration |
| Status | IMPLEMENTATION_GAP |
| Evidence | "PrintLockState 0x006410D8..0x006413CA; lock/unlock caller 0x0063FF02..0x0063FF24 (triage E9)." |
| Unresolved (operative) | "RECOVERABLE_GAP -> IMPLEMENTATION_GAP: rows adopted (manager, 2026-10-10, jobs/B-M3M4.md); recovered, now to build." (stale: built; S1 item 6 made the missing debug name visible, Motion.cs:421-433) |
| KIND | (a) SPLIT (the debug names only), plus IN-LAYER |
| Narrowed M4-031 | "PrintLockState and the lock-tree order." Handoff: the debug-name argument comes from each LockTracks caller (research/20261010-m4-movement-leftover-rows.md Row 3: 0x004F0F68 through the helper 0x004F0F4C, called from 0x004F0E00, 0x0054058E, 0x0054BE24, 0x0054EC08; 0x005BE624; 0x00640290; 0x006404EC; the tail at 0x0063F0B8) |
| Higher-layer records | M8-007 exists: "An action's track mask is a lock, taken while it runs", IMPLEMENTATION_GAP (the IActionRunner caller 0x0054058E). M5-035 exists: "The streamer never reads enabledAnimTracks or skips a locked track; locks only through DisableAnimTracks/EnableAnimTracks; the live idle checks MovementComponent locks", IMPLEMENTATION_GAP. The owners of the callers 0x0054BE24, 0x0054EC08, 0x005BE624: UNKNOWN (not identified here) |
| Settle as-is | No, for the stale `unresolved` and `test` ("M4ControlTests PrintLockState diagnostic case after extraction."); the code was not judged here beyond S1 |

## 2. Summary

### 2.1 New records to create

Higher layers (suggested ids follow the current maxima; the manager assigns):
- S-1 M6-027: RobotAudioClient::UpdateAnimationIsReady 0x0059A1CC. No audio animation -> ready; state byte +0x3C 1 or 3 -> ready; 4 or 5 -> ClearCurrentAnimation (`blx 0x4ADBC4` 0x0059A1F0) then ready; otherwise not ready (0x0059A1CC..0x0059A1F6). IMPLEMENTATION_GAP (rows C16/Q5 exist; re-read here). From M3-013.
- S-2 M6-028: RobotAudioClient::CreateAudioAnimation 0x00599FF0: the output-source switch 0x0059A070..0x0059A0B6, the post-construction state check 0x0059A0BA, the source-0 clear 0x0059A11C..0x0059A12E (per the record). IMPLEMENTATION_GAP (rows C2 and R-FIX2). From M3-024.
- S-3 M11-059: VisionComponent::Init and VisionSystem::Init: the config reads (InitialExposureTime_ms 0x00650DAE..0x00650DCA, rows 4a..4l), VisionSystem::Init 0x006B0658 called at 0x00650E36 and its error path 0x00650E42..0x00650FB0, IsInitialized 0x006B2CD6, the album load-or-erase branch 0x00650FB4..0x0065108A. RECOVERABLE_GAP (VisionSystem::Init's body and the branch at 0x00650FE8..0x00650FFC are unread). From M3-021 and M3-033.
- S-4 M11-060: VisionSystem::Update's application of the SetNextCameraParams slot (1d). RECOVERABLE_GAP; the consumer's address is UNKNOWN (read VisionSystem::Update from 0x006B4D5C). From M3-021.
- S-6 M11-061: the vision-mode request queue: VisionComponent::EnableMode 0x006527A8 tail call, VisionSystem::SetNextMode 0x006B2282..0x006B2298 (deque +0xB0), the Update drain 0x006B4FB6..0x006B4FE4, VisionSystem::EnableMode's mask +0xAC 0x006B1A76..0x006B1CC6. IMPLEMENTATION_GAP (rows E4..E7; code built under M4-017). From M4-017.
- S-7 M8-019: BehaviorManager::RequestCurrentBehaviorEndImmediately (called at 0x00535454 from HandleRobotStopped). RECOVERABLE_GAP. From M4-019.
- S-8 M10-015: RobotGyroDriftDetector::DetectGyroDrift / DetectBias per RobotState (calls 0x00512FBA and 0x00512FC4 on robot+0x27C). RECOVERABLE_GAP. From M4-019.
- S-9a M15-034: ProgressionUnlockComponent's NV read callback 0x0064CE34. RECOVERABLE_GAP (only M3-034's summary exists). From M3-034.
- S-9b M15-035: InventoryComponent's NV read callback 0x0063D7C4. RECOVERABLE_GAP. From M3-034.
- S-10 M7-025 (or M15): BehaviorTrackLaser's read of VisionComponent+0x32A (0x005FAC64, 0x005FBDAE). RECOVERABLE_GAP. From M3-023.
- S-11 M15-036: the Lab read: Read(0x196000) at 0x006A5B1E, callback 0x006A6486 -> CozmoExperiments::RestoreLoadedActiveExperiments 0x006A5C34. RECOVERABLE_GAP. From M3-033/M3-034.

Existing higher records to cite instead of creating: M11-058, M11-033, M6-016, M6-015, M5-018, M5-007, M8-015, M8-017, M5-038, M5-022, M13-029, M12-018, M12-015, M15-022, M12-008, M11-004, M11-009, M11-013, M11-041, M11-019, M11-053, M11-044, M10-001, M11-035, M11-039, M11-049, M11-057, M11-051, M13-009, M14-012, M15-014, M15-033, M10-010, M4-015, M4-023, M4-032 (titles and statuses quoted in the rows above).

In-layer:
- I-1 M3-045: imdecode's decoder choice for a non-JPEG payload in encodings 5/6/7 (the shipped libopencv_imgcodecs). RECOVERABLE_GAP; whether the robot ever sends 5/6/7 is HARDWARE_ONLY unless the firmware is readable.
- I-2 M3-046: COMPATIBILITY_POLICY for CameraFrame.PresentationJpeg/Save (tool-only, non-engine JPEG bytes).
- I-3 M4-033: TouchSensorComponent (the live per-state Update at 0x00512996, ctor, dtor, logging). RECOVERABLE_GAP for its readers and StartLogging's callers.
- A record or policy for the DAS `$data` sink (M4-008, and every sEventF); layer to be chosen.
- An owner for the firmware-JSON LogicError catcher (M3-024/M10-010), likely M1 or M3.

### 2.2 Narrowed records ready to settle

- M3-025: yes, once `unresolved`/`test` record fix round S1.
- M4-002: yes, once `unresolved` records S1 and the unreachable variability step.
- M4-001: after one change (the M4-032 tag on the two clip warnings, Motion.cs:749, 755) and once the M13-022 duplicate is resolved.

No other narrowed record is ready as-is; each row above gives the blocking reason.

### 2.3 In-layer work remaining

1. Robot::SendMessage's failure warning on every robot send the engine routes through 0x004A5368 (0.1). Blocks M3-013, M3-021, M3-023, M3-026, M3-028 (sender to confirm), M3-031, M3-043, M4-010, M4-016 (sender to confirm), M4-017, M4-018, M4-019.
2. Retags (0.2): the IActionRunner base -> M8-015; PathComponent -> M13-029; ConstructorDelocalize -> M11-019/M11-053; the VisionSystem mode queue -> S-6; Gate B -> M3-032 (its hook naming M11-035); the generic lift lines -> M4-016; CameraFrame.Save -> I-2.
3. Handoffs to make visible and named: M3-001/M3-018 cv::Exception -> M11-058; the M3-010 short-PCM stand-in -> M6-016/M6-015; M3-021's constant 16 and IsInitialized -> S-3; the M3-022 events -> M11-039/M11-049; the M3-033 FaceAlbum gate -> S-3; M4-009's slot-as-ObjectID -> M11-013/M11-041; M4-018's IsVisible and makeRelative -> M11-004; M4-019's Cancel(-1) -> M8-015, the behaviour end -> S-7, DetectGyroDrift/DetectBias -> S-8; M4-008's FindConnectedObjectHelper MISSING -> M11-004; the NV backup MISSING lines -> M15-033; M4-027's Touch/Cliff -> I-3.
4. M4-032 tags on the %f / %.1f / %.2f renders: Motion.cs:749, 755 (M4-001) and 1309, 1322 (M4-016); Camera.cs:783-796 (M3-022); Cubes.cs:561 (M4-008).
5. M3-022 order: install (0x0065ADE0) before the +0x48 store (0x0065AE80); Camera.cs:774-779 does the reverse.
6. Stale fields: `unresolved` of M3-022, M3-023, M3-025, M3-026, M3-028, M3-031, M3-038, M3-043, M4-002, M4-003, M4-017, M4-026, M4-027, M4-028, M4-031; empty `test` on M3-038, M4-026, M4-027, M4-028; placeholder `test` on M3-043 and M4-031.
7. M4-010: live-entry tests for the full connection claim.
8. M4-016: the queued TolTooSmall citation; the M13-022 duplicate class.
9. M4-008: drop "IMU" from the title (M10-012/M10-013 own raw IMU).
10. M3-001/M3-018: I-1; the O8 cv::redirectError read; fopen("") recorded as a system-library boundary.
11. M3-043: the vector-overload caller sweep.
12. M3-034: retire into S-9a, S-9b, S-11, M14-012, M15-033 and M15-014, with the test that accepts no-op sinks.

### 2.4 Open questions for the manager

- M4-003 MA11: are the app's 10/20/0 defaults an app input (a COMPATIBILITY_POLICY) rather than part of an EXACT record?
- The owner of each overlap in 0.3, in particular M3-024 vs M10-010 (the shared parse) and M4-009 vs M11-041 (the slot gate).
- M8-015 and M13-029 have rows and built code; their RECOVERABLE_GAP status reads as IMPLEMENTATION_GAP.
- Actions/ActionList.cs, ActionQueue.cs and ActionWatcher.cs carry `fidelity: M7-020` but implement M8-015's rows.
- M4-009's dock target DockingComponent+0xC: does M12-017 own DockWithObject's write, or is a new M12 record needed?
- Whether the robot ever sends encodings 5/6/7 (decides I-1's reachability).
