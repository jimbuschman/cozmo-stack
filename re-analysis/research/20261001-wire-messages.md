# Outbound wire-message audit (Q20)

## Coverage audit (2026-10-01)

| message | coverage | checked basis |
| --- | --- | --- |
| `GetManufacturingInfo` | CHECKED | Generated layout/tag, sole C# construction and native manufacturing-info gate/send site. |
| `SyncTime` | CHECKED | Layout including `-20.0f` bits, sole production builder and native success-chain gate. |
| `InitController` | CHECKED | Layout, automatic connection sender and public resend helper against the sole native connection sender. |
| `ImageRequest` | CHECKED | Layout, connection and explicit start/stop senders against native connection/camera-control sites. |
| `AbsoluteLocalizationUpdate` | CHECKED | Layout and sole send helper/callers against native contains-origin gate and order. |
| `SetCameraParams` | CHECKED | Layout, both C# sends and native default-reply plus connection-time indeterminate-byte paths. |
| `NVCommand` | CHECKED | Layout and every initial/retry/rerequest construction against the native queue paths and open vector source. |
| `SetAppRunID` | CHECKED | Layout, sole C# sender, native literal words and success ordering. |
| `RequestCrashReports` | CHECKED | Layout, initial and follow-up senders, index/order and failure gate. |
| `SetAudioVolume` | CHECKED | Layout, engine default and explicit audio sender against native quantisation/game-message paths. |
| `SetBodyRadioMode` | CHECKED | Layout, sole sender, 16-state gate and counter reset. |
| `DriveWheels` | CHECKED | Layout and every normal, stop, emergency and dispose sender against native motion/shutdown sites. |
| `MoveLift` | CHECKED | Layout, direct-drive and stop senders, epsilon gate and track ordering. |
| `MoveHead` | CHECKED | Layout, direct-drive and stop senders, epsilon gate and track ordering. |
| `SetLiftHeight` | CHECKED | Layout, action construction/send route and action-id ordering. |
| `SetHeadAngle` | CHECKED | Layout plus motion, public helper, explorer and vision senders against native game/internal action paths. |
| `SetBodyAngle` | CHECKED | Layout and all message-construction/send routes against native absolute/relative builders. |
| `StopAllMotors` | CHECKED | Empty layout and every motion/behavior/dispose caller against native unlock-before-stop path. |
| `DisableAnimTracks` | CHECKED | Layout, sole accumulated-mask sender and empty-to-nonempty transition gate. |
| `EnableAnimTracks` | CHECKED | Layout, both normal-mask and release-by-index send paths. |
| `StartMotorCalibration` | CHECKED | Layout, request and stepped-behavior callers through the sole motion sender. |
| `IMURequest` | CHECKED | Layout, sole explicit sender and duration conversion. |
| `EnableStopOnCliff` | CHECKED | Layout and explicit, wheelie-disable/restore and behavior-stop callers. |
| `SetCliffDetectThreshold` | CHECKED | Layout, cache/change gate and sole sender. |
| `BackpackLightsMiddle` | CHECKED | Layout, five-light sender and public three-light helper against native mandatory two-message sequence. |
| `BackpackLightsTurnSignals` | CHECKED | Layout, sole sender and immediate order after middle lights. |
| `SetHeadlight` | CHECKED | Layout and public route through the cached explicit game path. |
| `SetCubeGamma` | CHECKED | Layout, first/change gate and ordering before cube ID/lights. |
| `CubeID` | CHECKED | Layout, sole wire sender and frame conversion/order. |
| `CubeLights` | CHECKED | Layout, sole wire sender and LED order/conversion. |
| `SetPropSlot` | CHECKED | Layout and complete connection/empty-slot state-machine send path. |
| `StreamObjectAccel` | CHECKED | Layout, first-listener/last-listener sends and bool values. |
| `SetAccessoryDiscovery` | CHECKED | Layout, explicit public sender and absence of a native send site. |
| `AbortAnimation` | CHECKED | Empty layout, abort-event and dispose send routes and native abort ordering. |
| `AudioSample` | CHECKED | Fixed layout plus scheduler, policy playback and direct single-frame send routes against native stream sites. |
| `AudioSilence` | CHECKED | Empty layout plus scheduler, raw-face pairing and direct send routes against native stream sites. |
| `RecordHeading` | CHECKED | Layout, scheduler-only sender, eligibility gate and frame position. |
| `TurnToRecordedHeading` | CHECKED | Layout, scheduler-only sender, eligibility gate and frame-last position. |
| `HeadAngle` | CHECKED | Layout, scheduler sink route, signed conversion and frame position. |
| `LiftHeight` | CHECKED | Layout, scheduler sink route and frame position. |
| `Event` | CHECKED | Layout, scheduler sink route, recognized-event gate and frame position. |
| `FaceImage` | CHECKED | Layout plus scheduler and raw-display send routes against native animation-only face paths. |
| `BackpackLights` | CHECKED | Layout, scheduler sink route and frame position. |
| `BodyMotion` | CHECKED | Layout, keyframe/stop send routes, radius gate and frame position. |
| `EndOfAnimation` | CHECKED | Empty layout plus scheduler and public direct helper against native completion-only send site. |
| `StartOfAnimation` | CHECKED | Layout, scheduler-only sender, tag counter and after-audio ordering. |
| `ClearPath` | CHECKED | Layout, start/abort/terminal callers and ordering. |
| `AppendPathSegmentLine` | CHECKED | Layout, sole segment sender and append order. |
| `AppendPathSegmentArc` | CHECKED | Layout, sole segment sender and append order. |
| `AppendPathSegmentPointTurn` | CHECKED | Layout, sole segment sender, bool width and append order. |
| `ExecutePath` | CHECKED | Layout, sole sender and after-all-segments order. |
| `DockWithObject` | CHECKED | Full 21-byte layout, all immediate builders and dock send route. |
| `AbortDocking` | CHECKED | Empty layout, timeout/explicit callers and state-clear order. |
| `PlaceObjectOnGround` | CHECKED | Layout, action callers through the docking sender and carry precondition. |
| `DockingErrorSignal` | CHECKED | Layout, sole sender and native uninitialised final-byte behavior. |
| `EnableColorImages` | CHECKED | Layout, cached change gate and ordering before `ImageRequest`. |

No conclusion below rests on unchecked work.  The audit enumerated every
occurrence of each outbound type under `Cozmo.Robot`, then followed actual
construction values to `SendMessage`, `SendStreamDirect`, or the animation
FIFO.  Non-sending type mentions (state fields, size calculations and action
names) were excluded explicitly.  The result is **44 HOLDS and 12 DEFECT**.

Date: 2026-10-01  
HEAD inspected: `415b9e0`
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

Per message, **44 HOLDS and 12 DEFECT**.  Nine defects are send-site/surface
differences rather than a wrong generated codec.  Three are unavoidable or
currently unresolved source-byte differences.  “Builder” below is the native
member Pack address where Appendix A established one; for later messages it is
the native semantic builder/send range, explicitly labelled.

## 1. Connection path

| message | native builder and exact member layout | C# sender | result |
|---|---|---|---|
| `GetManufacturingInfo` 0x25 | inline empty at 0x007AB868 | `CozmoEngine.cs:1403` | **HOLDS.** Sent only after the firmware/identity stage selects the physical manufacturing-info path. |
| `SyncTime` 0x4B | Pack 0x007A3A94: u32 timestamp, f32 `-20.0` (`0xC1A00000`) | `CozmoEngine.cs:989` | **HOLDS.** The success-response gate and first-send-failure short circuit match 0x0051524C..0x00515270. |
| `InitController` 0x9F | empty, Pack 0x007BDDA2 | `CozmoEngine.cs:990`; `CozmoRobot.cs:448` | **DEFECT (extra send surface).** The production connection path holds: it follows a successful SyncTime. The public `EnableAnimations` helper can resend it at any time; the native xrefs establish only the connection-chain sender at 0x0051529A. |
| `ImageRequest` 0x4C | Pack 0x007A3BDA: u8 mode, u8 resolution | `CozmoEngine.cs:995`; `CozmoRobot.cs:529-536` | **HOLDS** for connection (`Stream,4`, only after InitController succeeds). The explicit start/stop API corresponds to a caller-requested camera command and preserves the same bytes. |
| `AbsoluteLocalizationUpdate` 0x45 | Pack 0x007A384E: u32 timestamp, frame, origin; f32 x,y,angle | `CozmoEngine.cs:1006-1023` | **HOLDS.** Contains-origin gate and field order match 0x00512734..0x0051279E; no padding. |
| `SetCameraParams` 0x57 | Pack 0x007BF456: f32, u16, bool | `Camera.cs:924,986` | **DEFECT (forced policy at connection).** The ordinary DefaultCameraParams reply path holds. At connect the engine sends stale stack bytes 0..5 and bool 1 (0x00658414..0x0065842C); C# deterministically sends f32 `0`, u16 `0`, true. The layout/tag hold, the first six transmitted bytes cannot be source-exact. |
| `NVCommand` 0x81 | Pack 0x007CE63C: u32 tag, i32-width length, u8 op, u8 byte, u16-count bytes | `NvStorage.cs:406-419,570,614,678` | **DEFECT / unsettled send condition.** Layout, reliable send, factory/non-factory Length and retry/rerequest order hold, but M3-027 remains open: native READ copies its `+0xE8` vector at 0x00645386 without the read path establishing who populated it. C# sends empty `Data`, and the current test asserts that guess. |
| `SetAppRunID` 0xA0 | Pack 0x007A5FBA: four u32 words | `CozmoEngine.cs:1159` | **HOLDS.** Four `0xFFFFFFFF` words, then only on success RequestCrashReports, as TracePrinter at 0x0053D388..0x0053D3DC. |
| `RequestCrashReports` 0x80 | inline u32 / Pack 0x007A55C2 | `CozmoEngine.cs:1161,1172` | **HOLDS.** Initial index 0 and subsequent one-based requests preserve the native ordering and failure stop. |
| `SetAudioVolume` 0x64 | inline u16 / Pack 0x007A13B8 | `CozmoEngine.cs:1894`; explicit `Audio.cs:267` | **HOLDS.** The automatic post-connect path uses the native quantisation result unchanged; the explicit audio API supplies the same on-wire u16 to the corresponding caller-requested setting path. |
| `SetBodyRadioMode` 0x07 | semantic builder 0x00512AD0..0x00512B52: u8 mode, u8 channel | `CozmoEngine.cs:1077` | **HOLDS.** Exactly after the 16th consecutive state without `IS_BODY_ACC_MODE`, sends `{1,0}` and resets the counter. |

## 2. Motion, sensors and lights

| message | native builder and exact member layout | C# sender | result |
|---|---|---|---|
| `DriveWheels` 0x32 | Pack 0x007A1EF0: four f32, left/right speed then acceleration | `Motion.cs:392,719`; `CozmoRobot.cs:588` | **DEFECT (extra send site).** Normal direct-drive and StopBody paths hold, including lock-before-command and unlock-before-zero. `EmergencyStop` and `Dispose` additionally send `DriveWheels(0,0,0,0)` after native-equivalent StopAllMotors; the engine shutdown/StopAll path does not. |
| `MoveLift` 0x34 | inline f32 / Pack 0x007A2248 | `Motion.cs:663,708` | **HOLDS.** Direct-drive gates, `abs(speed) < 0x3727C5AC`, track lock/unlock and verbatim value match 0x0063F73A..0x0063F92A. |
| `MoveHead` 0x35 | inline f32 / Pack 0x007A2376 | `Motion.cs:647,697` | **HOLDS.** Same source gate/order as the native head path 0x0063F4F6..0x0063F5D2. |
| `SetLiftHeight` 0x36 | Pack 0x007A24BA: four f32 then u8 action id | `Motion.cs:619` | **HOLDS.** Caller values are copied verbatim and the shared preincrement action id is last. |
| `SetHeadAngle` 0x37 | Pack 0x007A2670: four f32 then u8 action id | `Motion.cs:578`; `CozmoRobot.cs:559-560`; `ExplorerBehaviors.cs:258-262`; `FaceActions.cs:138`; `VisionSystem.cs:1170` | **DEFECT (bypass senders).** `CozmoMotion` holds. The public raw helper sends 10/10/0 and caller/default id without the game handler's action/track lifecycle; the approved M4 source says the game default is 10/20/0. The explorer path carries explicit values and the shared id, but the face/vision shortcuts also hard-code 10/10/0 rather than the engine-internal 15/20/0 default. No recovered row authorizes those 10/10 shortcuts. |
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
| `AudioSample` 0x8E | Pack 0x007BCD3C: fixed 744 bytes, no count | `CozmoAnimations.cs:58`; `Audio.cs:273-288,318-328,437-444` | **DEFECT (extra policy/direct send surfaces).** The animation scheduler holds: exactly one sample or silence per built frame. `CozmoAudio.Play` is the declared M3-017 compatibility policy, and public `SendFrame` can send one sample directly without the native frame-builder gate or per-Update budget. |
| `AudioSilence` 0x8F | empty / Pack 0x007BCE58 | `CozmoAnimations.cs:57`; `Audio.cs:282-288`; `CozmoRobot.cs:240-241` | **DEFECT (extra direct send surfaces).** The scheduler's no-sample branch and position hold. Public `SendSilence` and the raw-display pairing can send it outside a native animation/live-layer frame; the latter belongs to the raw-bitmap API which M3 MD3 says has no engine counterpart. |
| `RecordHeading` 0x91 | empty; frame-builder step 12 | `CozmoAnimations.cs:103` | **HOLDS.** Only an eligible current record-heading keyframe, after body. |
| `TurnToRecordedHeading` 0x92 | s16 offset/speed/accel/decel, u16 tolerance, u8 half-revs, bool shortest (13 bytes) | `CozmoAnimations.cs:110-122` | **HOLDS.** Field order/signed widths and frame-builder-last position match the recovered keyframe body. |
| `HeadAngle` 0x93 | Pack 0x007BCF2C: u16 duration, i8 degrees | `CozmoAnimations.cs:63` | **HOLDS.** Native signed byte and truncation are preserved. |
| `LiftHeight` 0x94 | Pack 0x007BD064: u16 duration, u8 mm | `CozmoAnimations.cs:67` | **HOLDS.** No padding or unit conversion at the serializer. |
| `Event` 0x95 | one u8 | `CozmoAnimations.cs:100` | **HOLDS.** Only a source-recognized AnimEvent gets a wire message; rejected names remain local/no-send. |
| `FaceImage` 0x97 | Pack 0x007BD416: u16 byte count then bytes | `CozmoAnimations.cs:29-32`; scheduler `AnimationScheduler.cs:1376-1411`; raw display `Display.cs:371-378` | **DEFECT (extra raw-display surface).** Count prefix/layout and scheduler order hold, and failed procedural encoding sends nothing. `CozmoDisplay` can send a caller bitmap directly; approved M3 decision MD3 explicitly says that API has no engine counterpart and no fidelity record. |
| `BackpackLights` 0x98 | five u16 words, Left/Front/Middle/Back/Right | `CozmoAnimations.cs:97` | **HOLDS.** Sent every eligible frame after face and before body. |
| `BodyMotion` 0x99 | Pack 0x007BD6AE: i16 speed, i16 radius bits | `CozmoAnimations.cs:87,91` | **HOLDS.** Verbatim keyframe values; own stop is `{0,0x7FFF}` at the first elapsed-duration frame. |
| `EndOfAnimation` 0x9A | empty / Pack 0x007BDF6E | `CozmoAnimations.cs:73`; scheduler `AnimationScheduler.cs:1234-1239`; public helper `CozmoRobot.cs:450-451` | **DEFECT (extra send surface).** The scheduler path holds: direct, not budget-gated, and only after Start, layers/audio complete and FIFO empty. Public `EndAnimation` can emit the tag without any of those native gates. |
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

The twelve defects are:

1. `InitController`: `EnableAnimations` can resend it outside the native
   connection chain.
2. `SetCameraParams`: deterministic zero substitutes for native indeterminate
   connection bytes (documented policy).
3. `NVCommand`: READ `Data` is still an open M3-027 source-recovery gap.
4. `DriveWheels`: the safety helper/dispose adds a zero command absent from the
   engine sequence.
5. `SetHeadAngle`: the public raw helper bypasses the recovered action/track
   send conditions and supplies non-source defaults.
6. `BackpackLightsMiddle`: the public raw helper omits the mandatory following
   TurnSignals message.
7. `SetAccessoryDiscovery`: an explicit C# sender exists although the engine
   has no sender.
8. `AudioSample`: the M3-017 policy player and public single-frame helper send
   outside the native animation frame-builder condition.
9. `AudioSilence`: the public single-frame helper and raw-display pairing send
   outside the native animation/live-layer condition.
10. `FaceImage`: the raw-bitmap API sends outside the native procedural/sprite
    animation paths (M3 MD3 already records that it has no counterpart).
11. `EndOfAnimation`: the public helper bypasses the native Start/audio/FIFO
    completion gates.
12. `DockingErrorSignal`: deterministic zero substitutes for two native
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
