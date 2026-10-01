# Outbound wire-message audit (Q20)

Date: 2026-10-01  
HEAD inspected: `954c092`  
Primary source: `libcozmoEngine.so` 3.4.0-1204.

## Scope and result

I enumerated the generated engine-to-robot classes that are actually
constructed by `Cozmo.Robot`, including target-typed constructors, then traced
each construction to `MessageHandler.SendMessage` or the animation stream
buffer.  There are **56 messages**, not the 42 in the original M2 comparison:
the later M3/M4/M5 builds added fourteen engine-sent types.

The common union format **HOLDS** for all 56: one tag byte, then an unpadded
little-endian member (`EngineToRobot::Pack` 0x007AB6B8, total size
`1 + member.Size()` at 0x007ABB98).  The C# common implementation is
`Cozmo.Protocol/Clad/MessageBase.cs:16-42`; the generated members are in
`Generated/RobotMessages.g.cs`.  No C# serializer inserts CLR alignment or
padding.

Per message, **49 HOLDS and 7 DEFECT**.  Five defects are send-site/surface
differences rather than a wrong generated codec.  Two are unavoidable or
currently unresolved source-byte differences.  “Builder” below is the native
member Pack address where Appendix A established one; for later messages it is
the native semantic builder/send range, explicitly labelled.

## 1. Connection path

| message | native builder and exact member layout | C# sender | result |
|---|---|---|---|
| `GetManufacturingInfo` 0x25 | inline empty at 0x007AB868 | `CozmoEngine.cs:1403` | **HOLDS.** Sent only after the firmware/identity stage selects the physical manufacturing-info path. |
| `SyncTime` 0x4B | Pack 0x007A3A94: u32 timestamp, f32 `-20.0` (`0xC1A00000`) | `CozmoEngine.cs:989` | **HOLDS.** The success-response gate and first-send-failure short circuit match 0x0051524C..0x00515270. |
| `InitController` 0x9F | empty, Pack 0x007BDDA2 | `CozmoEngine.cs:990` | **HOLDS** on the production connection path: it follows a successful SyncTime. `CozmoRobot.cs:448` also offers an explicit resend, which is an extra caller but does not alter automatic startup. |
| `ImageRequest` 0x4C | Pack 0x007A3BDA: u8 mode, u8 resolution | `CozmoEngine.cs:995`; `CozmoRobot.cs:529-536` | **HOLDS** for connection (`Stream,4`, only after InitController succeeds). The explicit start/stop API corresponds to a caller-requested camera command and preserves the same bytes. |
| `AbsoluteLocalizationUpdate` 0x45 | Pack 0x007A384E: u32 timestamp, frame, origin; f32 x,y,angle | `CozmoEngine.cs:1006-1023` | **HOLDS.** Contains-origin gate and field order match 0x00512734..0x0051279E; no padding. |
| `SetCameraParams` 0x57 | Pack 0x007BF456: f32, u16, bool | `Camera.cs:924,986` | **DEFECT (forced policy at connection).** The ordinary DefaultCameraParams reply path holds. At connect the engine sends stale stack bytes 0..5 and bool 1 (0x00658414..0x0065842C); C# deterministically sends f32 `0`, u16 `0`, true. The layout/tag hold, the first six transmitted bytes cannot be source-exact. |
| `NVCommand` 0x81 | Pack 0x007CE63C: u32 tag, i32-width length, u8 op, u8 byte, u16-count bytes | `NvStorage.cs:406-419,570,614,678` | **DEFECT / unsettled send condition.** Layout, reliable send, factory/non-factory Length and retry/rerequest order hold, but M3-027 remains open: native READ copies its `+0xE8` vector at 0x00645386 without the read path establishing who populated it. C# sends empty `Data`, and the current test asserts that guess. |
| `SetAppRunID` 0xA0 | Pack 0x007A5FBA: four u32 words | `CozmoEngine.cs:1159` | **HOLDS.** Four `0xFFFFFFFF` words, then only on success RequestCrashReports, as TracePrinter at 0x0053D388..0x0053D3DC. |
| `RequestCrashReports` 0x80 | inline u32 / Pack 0x007A55C2 | `CozmoEngine.cs:1161,1172` | **HOLDS.** Initial index 0 and subsequent one-based requests preserve the native ordering and failure stop. |
| `SetAudioVolume` 0x64 | inline u16 / Pack 0x007A13B8 | `CozmoEngine.cs:1894` | **HOLDS.** The native quantisation result is sent unchanged. |
| `SetBodyRadioMode` 0x07 | semantic builder 0x00512AD0..0x00512B52: u8 mode, u8 channel | `CozmoEngine.cs:1077` | **HOLDS.** Exactly after the 16th consecutive state without `IS_BODY_ACC_MODE`, sends `{1,0}` and resets the counter. |

## 2. Motion, sensors and lights

| message | native builder and exact member layout | C# sender | result |
|---|---|---|---|
| `DriveWheels` 0x32 | Pack 0x007A1EF0: four f32, left/right speed then acceleration | `Motion.cs:392,719`; `CozmoRobot.cs:588` | **DEFECT (extra send site).** Normal direct-drive and StopBody paths hold, including lock-before-command and unlock-before-zero. `EmergencyStop` and `Dispose` additionally send `DriveWheels(0,0,0,0)` after native-equivalent StopAllMotors; the engine shutdown/StopAll path does not. |
| `MoveLift` 0x34 | inline f32 / Pack 0x007A2248 | `Motion.cs:663,708` | **HOLDS.** Direct-drive gates, `abs(speed) < 0x3727C5AC`, track lock/unlock and verbatim value match 0x0063F73A..0x0063F92A. |
| `MoveHead` 0x35 | inline f32 / Pack 0x007A2376 | `Motion.cs:647,697` | **HOLDS.** Same source gate/order as the native head path 0x0063F4F6..0x0063F5D2. |
| `SetLiftHeight` 0x36 | Pack 0x007A24BA: four f32 then u8 action id | `Motion.cs:619` | **HOLDS.** Caller values are copied verbatim and the shared preincrement action id is last. |
| `SetHeadAngle` 0x37 | Pack 0x007A2670: four f32 then u8 action id | `Motion.cs:578`; `CozmoRobot.cs:559-560` | **DEFECT (bypass API).** `CozmoMotion` holds. The public raw helper sends default 10/10/0 and id 1 without the engine game handler's action/track lifecycle; M2-015 explicitly found no native defaults at this layer. |
| `SetBodyAngle` 0x39 | Pack 0x007A296A: four f32, u16 half-revs, bool absolute, u8 action id | `VisionSystem.cs:1077-1108` | **HOLDS** at its wire builder: exact bool byte, no padding, shared action id; absolute/relative builders preserve caller values. Higher-level M13 behavior defects do not change this message's immediate send contract. |
| `StopAllMotors` 0x3B | inline empty at 0x007AB868 | `Motion.cs:733` | **HOLDS.** Releases owned tracks first, then tag 0x3B, with no native zero-wheel companion. The extra companion is reported on `DriveWheels`. |
| `DisableAnimTracks` 0x9D | one u8; semantic sender 0x006400D0..0x00640188 | `Motion.cs:223` | **HOLDS.** One accumulated mask containing only bits whose lock set changed empty→nonempty. |
| `EnableAnimTracks` 0x9E | one u8; semantic sender 0x0063FE92..0x0063FFB4 | `Motion.cs:239,307` | **HOLDS.** Normal unlock sends the accumulated emptied-bit mask; the separate release-by-index path intentionally sends its source index, matching the recovered interruption path. |
| `StartMotorCalibration` 0x58 | Pack 0x007A1DA6: bool head, bool lift | `Motion.cs:676` | **HOLDS.** Only explicit calibration actions send; byte identity/order and result gate match 0x00547D38..0x00547DC2. |
| `IMURequest` 0x4A | inline u32 / Pack 0x007C825A | `Sensors.cs:259` | **HOLDS.** Duration is converted to u32 milliseconds and sent on explicit request. |
| `EnableStopOnCliff` 0x60 | inline bool / Pack 0x007A528C | `Sensors.cs:281,629` | **HOLDS.** Explicit setting and wheelie disable/restore paths use a canonical bool byte. |
| `SetCliffDetectThreshold` 0x54 | u16; semantic builder 0x00634270..0x00634368 | `Sensors.cs:515` | **HOLDS.** Cache comparison precedes reliable send; values are copied verbatim. |
| `BackpackLightsMiddle` 0x03 | Pack 0x007A42EC: 3×10-byte LightState then u8 zero | `Lights.cs:275`; `CozmoRobot.cs:563-564` | **DEFECT (extra incomplete sender).** `Lights.cs` correctly follows it immediately with 0x11. The public three-light helper sends 0x03 alone, while native `SetBackpackLightsInternal` always sends 0x03 then 0x11 (0x00632186..0x006321BE). |
| `BackpackLightsTurnSignals` 0x11 | 2×10-byte LightState then u8 zero; semantic builder 0x00631F6C..0x006321B2 | `Lights.cs:276` | **HOLDS.** It immediately follows 0x03 in the production five-light path. |
| `SetHeadlight` 0x0B | inline bool / Pack 0x007A1CA8 | `Lights.cs:371` | **HOLDS.** Reliable explicit game path, canonical bool. |
| `SetCubeGamma` 0x0C | u8; semantic builder 0x0063A764..0x0063A792 | `Lights.cs:946` | **HOLDS.** Only on first/change, before CubeID and CubeLights. |
| `CubeID` 0x10 | Pack 0x007B764A: u32 object id, u8 rotation frames | `Lights.cs:947` | **HOLDS.** `(ms+29)/30` byte follows object id and precedes CubeLights. |
| `CubeLights` 0x04 | four 10-byte LightStates; semantic send 0x0063A7F4..0x0063A800 | `Lights.cs:948` | **HOLDS.** 40 bytes, LED 0..3 order, white-balance/frame/offset conversion already occurs before serialization. |
| `SetPropSlot` 0x05 | Pack 0x007A11A4: u32 factory id, u8 slot | `CubeConnections.cs:326-332` | **HOLDS.** Only the recovered connection/empty-slot state machine sends it. |
| `StreamObjectAccel` 0x08 | Pack 0x007B7B44: u32 object id, bool enable | `CubeAccel.cs:119,134` | **HOLDS.** First listener sends true; last listener sends false. |
| `SetAccessoryDiscovery` 0x0A | Pack 0x007B7548: bool enable | `Cubes.cs:216-222` | **DEFECT (explicit unsupported surface).** Layout holds and no automatic path sends it, but the public method does. Whole-text native xrefs find no send site at all; therefore there is no source condition corresponding to a caller invoking this API. |

## 3. Animation streaming

The native frame builder is 0x0057C94E..0x0057CA7A.  Its order is audio,
Start (once), head, lift, event, face animation/procedural face, backpack,
body, RecordHeading, TurnToRecordedHeading.  C# reproduces that order in
`AnimationScheduler.cs:1244-1321`; its bounded FIFO and direct End path were
checked separately against 0x0057BF60 and 0x0057C3B0..0x0057C496.

| message | native builder and exact member layout | C# sender | result |
|---|---|---|---|
| `AbortAnimation` 0x8D | empty; semantic sender 0x00517DE4..0x00517E30 | `CozmoAnimations.cs:172`; `CozmoRobot.cs:619` | **HOLDS.** Cancellation/disposal sends after stream cancellation and before StopAllMotors; no End is substituted. |
| `AudioSample` 0x8E | Pack 0x007BCD3C: fixed 744 bytes, no count | `CozmoAnimations.cs:58` | **HOLDS.** Exactly one sample or silence per built frame; only rendered 744-byte frames become samples. |
| `AudioSilence` 0x8F | empty / Pack 0x007BCE58 | `CozmoAnimations.cs:57` | **HOLDS.** Chosen only when the frame has no sample; also participates in the source frame order. |
| `RecordHeading` 0x91 | empty; frame-builder step 12 | `CozmoAnimations.cs:103` | **HOLDS.** Only an eligible current record-heading keyframe, after body. |
| `TurnToRecordedHeading` 0x92 | s16 offset/speed/accel/decel, u16 tolerance, u8 half-revs, bool shortest (13 bytes) | `CozmoAnimations.cs:110-122` | **HOLDS.** Field order/signed widths and frame-builder-last position match the recovered keyframe body. |
| `HeadAngle` 0x93 | Pack 0x007BCF2C: u16 duration, i8 degrees | `CozmoAnimations.cs:63` | **HOLDS.** Native signed byte and truncation are preserved. |
| `LiftHeight` 0x94 | Pack 0x007BD064: u16 duration, u8 mm | `CozmoAnimations.cs:67` | **HOLDS.** No padding or unit conversion at the serializer. |
| `Event` 0x95 | one u8 | `CozmoAnimations.cs:100` | **HOLDS.** Only a source-recognized AnimEvent gets a wire message; rejected names remain local/no-send. |
| `FaceImage` 0x97 | Pack 0x007BD416: u16 byte count then bytes | `CozmoAnimations.cs:29-32`; `Display.cs:375` | **HOLDS.** Count prefix/layout and stream order hold; a failed face encoding sends nothing. |
| `BackpackLights` 0x98 | five u16 words, Left/Front/Middle/Back/Right | `CozmoAnimations.cs:97` | **HOLDS.** Sent every eligible frame after face and before body. |
| `BodyMotion` 0x99 | Pack 0x007BD6AE: i16 speed, i16 radius bits | `CozmoAnimations.cs:87,91` | **HOLDS.** Verbatim keyframe values; own stop is `{0,0x7FFF}` at the first elapsed-duration frame. |
| `EndOfAnimation` 0x9A | empty / Pack 0x007BDF6E | `CozmoAnimations.cs:73`; scheduler direct send `AnimationScheduler.cs:1236` | **HOLDS.** Direct, not budget-gated; only after Start, layers/audio complete and FIFO empty. |
| `StartOfAnimation` 0x9B | Pack 0x007BDE88: u8 id | `CozmoAnimations.cs:70`; scheduler `AnimationScheduler.cs:1221-1226` | **HOLDS.** Buffered once after that frame's audio, with the source counter/tag. |

## 4. Paths, docking and the remaining direct messages

| message | native builder and exact member layout | C# sender | result |
|---|---|---|---|
| `ClearPath` 0x3C | inline u16 / Pack 0x007A2D38 | `RobotPath.cs:116,148,165` | **HOLDS.** Field is native literal 0; sent before append/execute and on abort/terminal cleanup. |
| `AppendPathSegmentLine` 0x3D | Pack 0x007A3004: four f32 then three-f32 speed profile | `RobotPath.cs:122-126` | **HOLDS.** 28 bytes, source segment order. |
| `AppendPathSegmentArc` 0x3E | Pack 0x007A31EA: five f32 then three-f32 speed profile | `RobotPath.cs:127-131` | **HOLDS.** 32 bytes, source segment order. |
| `AppendPathSegmentPointTurn` 0x3F | Pack 0x007A33EE: four f32, three-f32 speed profile, bool shortest | `RobotPath.cs:132-136` | **HOLDS.** 29 bytes; bool is canonical 0/1. |
| `ExecutePath` 0x41 | Pack 0x007A36EE: u16 path id, bool | `RobotPath.cs:139` | **HOLDS.** Follows all segment appends; current id is sent unchanged. |
| `DockWithObject` 0x42 | Pack 0x007C0660: f32 zero/speed/accel/decel, u8 action, bool, u8, u8 method, bool | `Docking.cs:329-343,368` | **HOLDS** at the immediate sender. All 21 member bytes and bool widths match 0x0063BB56..0x0063BD50. Higher-level M12/M13 action gaps are outside this message builder. |
| `AbortDocking` 0x43 | empty / Pack 0x007C0818 | `Docking.cs:377,407` | **HOLDS.** Timeout/explicit abort paths clear pending state after the send. |
| `PlaceObjectOnGround` 0x44 | Pack 0x007C0926: six f32 then bool | `DockActions.cs:857-864`; `Docking.cs:396` | **HOLDS.** `{0,0,0,100,200,500,bool}` and the send-after-carry precondition match 0x00632A88. |
| `DockingErrorSignal` 0x48 | Pack 0x007C0B26: u32 timestamp, four f32, bool, bool | `Docking.cs:411-437` | **DEFECT (forced policy).** Tag/order/geometry hold. Native `UpdateDockingErrorSignal` never initializes final bytes +0x14/+0x15 but Pack sends them; C# sends false,false. M12-014 correctly classifies this as a compatibility policy. |
| `EnableColorImages` 0x66 | inline bool | `Camera.cs:879-892` via `CozmoRobot.StartCamera` | **HOLDS** for the explicit camera-control path: setting is cached and a changed canonical bool is sent before ImageRequest. The engine does not send it merely because connection completed, and neither does C#. |

## Defects and test quality

The seven defects are:

1. `SetCameraParams`: deterministic zero substitutes for native indeterminate
   connection bytes (documented policy).
2. `NVCommand`: READ `Data` is still an open M3-027 source-recovery gap.
3. `DriveWheels`: the safety helper/dispose adds a zero command absent from the
   engine sequence.
4. `SetHeadAngle`: the public raw helper bypasses the recovered action/track
   send conditions and supplies non-source defaults.
5. `BackpackLightsMiddle`: the public raw helper omits the mandatory following
   TurnSignals message.
6. `SetAccessoryDiscovery`: an explicit C# sender exists although the engine
   has no sender.
7. `DockingErrorSignal`: deterministic zero substitutes for two native
   indeterminate bytes (documented policy).

The independent M2 tests prove tag/layout/size for the original 42 (`M2-009`),
including literal bytes and native type assertions.  Later message tests are
uneven: M4 direct-motion and track-order tests, animation wire-order tests,
and manipulation literal-byte tests are source-derived; generic
Pack→Parse→Pack tests are circular for layout.  There is no single test that
enumerates the **current 56-message production set**, and the original
`M2_009_OutboundSizesAreTheEngines` still describes and enumerates 42.  A
non-circular regression should freeze all 56 native tag/member sizes and the
send-site exceptions above; otherwise a new sender can bypass the M2 audit
without failing the purported exhaustive test.
