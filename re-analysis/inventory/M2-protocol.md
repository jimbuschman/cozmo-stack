# M2 protocol inventory

**State: approved by the manager on 2026-09-24 under the operator's standing authorisation of that day. The authorisation: source-derived inventories and ordinary source-fidelity decisions need no operator checkpoint; only a deliberate divergence from the engine, or an unresolved source question that materially affects robot behaviour, goes to the operator. MD1..MD6 were reviewed on that basis. None diverges from the engine: MD1, MD2 and MD3 follow the source; MD4 confirms that nothing sends SetAccessoryDiscovery automatically; MD5 and MD6 change no wire behaviour. The inventory is frozen with `python re-analysis/tools/fidelity.py --approve M2-protocol`.**

## Where this comes from

- **Source:** `libcozmoEngine.so` 3.4.0-1204, freshly disassembled with capstone. Also the decompiled Unity C#, as a lower authority that only confirms agreement.
- **Read-only extractor passes:**
  - **OUT** (Appendix A): every engine-to-robot message this stack constructs, 42 messages; rows S1..S7 plus a per-message table and the builder notes.
  - **IN** (Appendix B): every robot-to-engine codec, 56 messages, plus the dispatch; rows D1..D14, RS0..RS14 and R-P1..R-P7.
  - **Gap pass** (Appendix C): the ImageChunk signedness.
- **The C# was never evidence.** It was read only to choose the message set.
- **Manager spot-checks** were re-disassembled, and all matched:
  - FallingStopped Unpack 0x007B0EB6 reads three consecutive 4-byte fields. HandleFallingStopped 0x00535040 logs "timestamp: %u, duration (ms): %u, intensity %.1f" (format string at 0x00535274) from `ldrd r1,r2,[r5]` and `vldr s0,[r5,#8]` (0x0053506C, 0x00535068). It compares [r5+8] as a float with the >1000.0 threshold (0x005350AE..0x005350BA).
  - HandlePickAndPlaceResult stores the +4 byte (0x00533794..0x0053379A). It branches on +6 == 2 (0x005337A4) and == 1 (0x005337A8). The engine's BlockStatus name table at 0x01034984 points to "NO_BLOCK", "BLOCK_PLACED", "BLOCK_PICKED_UP" (0xC2072F, 0xC20738, 0xC20745).

## How to read the statuses

- **IMPLEMENTATION_GAP** means the behaviour is established from the source and the code has not yet been compared with it under this process. Every M2 record starts here, including the seven that were EXACT_SOURCE before this process. They were marked EXACT_SOURCE on evidence that was a directory, a bare symbol or a capture folder, or that proved only part of the claim.
- The comparison against the code (a read-only verifier) is the step after approval. Each record then becomes EXACT_SOURCE or stays a gap until it is built.
- **No RECOVERABLE_GAP remains in M2** once Appendix C is in.

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M2-001 | IMPLEMENTATION_GAP | CLAD wire primitives. Little-endian; the bool writer stores 0/1 and the bool reader stores (byte != 0). ReadBytes is all-or-nothing with no sticky error. Variable arrays carry a u8 or u16 count: on write the count is truncated to its width, and on read a failed element stops the loop. Strings have a u8 count and i8 chars. Fixed arrays have no prefix. | S4, S5, D8, D9, D10, D12, D13 |
| M2-002 | IMPLEMENTATION_GAP | RobotStatusFlag names and values (17), and where UpdateFullRobotState stores each consumed bit | Appendix B §3 status-bit table, RS11 |
| M2-003 | IMPLEMENTATION_GAP | liftAngle is radians and height = 66·sin(angle) + 45, with **no clamp** on the angle-to-height path. The 32..92 clamp belongs only to the height-to-angle direction (0x005170B0). | RS7, Appendix B §4 (M2-003) |
| M2-004 | IMPLEMENTATION_GAP | The SyncTime payload {u32 timestamp, f32 −20.0}. The post-connect sequence around it is M1-041. | Appendix A §1 SyncTime, §2 SendSyncTime |
| M2-005 | IMPLEMENTATION_GAP | The light colour word, 5-5-5 with red at bit 10 and bit 15 from the alpha byte (cube path and backpack path) | Appendix A §2 0x03, §4 colour word |
| M2-006 | IMPLEMENTATION_GAP | The FirmwareVersion 0xEE layout {u16, u16-count u8[]}. The JSON keys build, version, time and sim are parsed from msg+4 (M1 G5.2..G5.6). The engine has no `messageEngineToRobotHash` or `messageRobotToEngineHash`. | Appendix B §2 0xEE, §4 (M2-006) |
| M2-007 | IMPLEMENTATION_GAP | The AbsoluteLocalizationUpdate 0x45 layout {u32 timestamp, u32 frameId, u32 originId, f32 x, f32 y, f32 angle}. The ContainsOriginID send gate belongs to M1-041. | Appendix A §1 0x45, §2 0x45 |
| M2-008 | IMPLEMENTATION_GAP | The outbound union. The tag byte is written first, then the member. Size = 1 + member size. Each typed constructor writes its tag. Single-scalar members are packed inline. | S1, S2, S3, S7 |
| M2-009 | IMPLEMENTATION_GAP | Outbound layouts and sizes of the 42 messages this stack sends: field order, widths, bool vs u8, f32 vs u32 | Appendix A §1 table, §6 |
| M2-010 | IMPLEMENTATION_GAP | Inbound dispatch. A tag switch reads the TBH table at 0x007B1B10. Out-of-range tags, and the 14 in-range tags without a codec (0xCC, 0xDF..0xEB), consume only the tag byte. The other 56 in-range tags are exactly the protocol definition's codecs. This settles the M1-027 interface. | D1..D7 |
| M2-011 | IMPLEMENTATION_GAP | Inbound size rule. A message is kept only when bytes consumed == length, and field read failures are otherwise ignored. So an over-counted u8 array is accepted shorter, trailing bytes drop the message, and some truncated fixed messages pass. | D1, D7, D8, D10, D11, D12 |
| M2-012 | IMPLEMENTATION_GAP | Inbound layouts and sizes of the 56 codecs: field order, widths, bool vs u8. The fields each consumer reads are listed under "Interfaces". | Appendix B §2 table, §6 |
| M2-013 | IMPLEMENTATION_GAP | FallingStopped 0xDE is {u32 timestamp, u32 duration_ms, f32 impactIntensity}. **The current codec contradicts this:** it reads duration@0, intensity@4, field2@8. | Appendix B §2 0xDE, §5; manager spot-check |
| M2-014 | IMPLEMENTATION_GAP | PickAndPlaceResult 0xB8 is {u32, bool success, i8 DockingResult, u8 BlockStatus}, with BlockStatus 0 NO_BLOCK, 1 BLOCK_PLACED, 2 BLOCK_PICKED_UP. **Docking.cs has 1 and 2 swapped.** What the consumer does with each value is M12's. | R-P1, Appendix B §5; manager spot-check |
| M2-015 | IMPLEMENTATION_GAP | Outbound builders copy their callers' speed, acceleration, duration and angle values verbatim, with no defaults and no unit conversion. So the default arguments in MessageExtras (SetHeadAngle 10/10, SetLiftHeight 3/20) have no counterpart at this layer. Which values the original's callers pass is decided in M4. | Appendix A §2 (0x32/0x34/0x35/0x36/0x37/0x39), §3 findings 4-5 |
| M2-016 | IMPLEMENTATION_GAP | ImageChunk 0xF2 field types: frameTimeStamp u32, imageId u32, imageEncoding **u8** (the json's i8 is contradicted), resolution i8, imageChunkCount u8, chunkId u8, u16-count data. chunkDebug is i32 and status is i16, taken from Unity's generated CLAD (MD6). | Appendix C §2, §4 |

## Decisions (the manager's, recorded for audit; the operator may overrule)

- **MD1. Inbound size rule.** The stack reproduces the engine's rule (M2-011) exactly. It is the source behaviour, so no policy is needed.
- **MD2. Protocol definition corrections.** The json's type-only differences get corrected in the generator input: bool vs u8, f32 vs u32, the PrintTrace split, the `native_size` strings and FallingStopped. For bools this changes what a handler sees: the engine reads any non-zero byte as 1 (D9).
- **MD3. Messages the engine sends that the stack never constructs** belong to their owning layer, not M2. Appendix A §3 finding 3 lists them; the backpack TurnSignals 0x11 companion is one. The owning layers are M4 (lights, motion, cliff threshold, radio mode) and M5 (tracks, abort, keyframes).
- **MD4. SetAccessoryDiscovery 0x0A.** The engine has no send site (Appendix A §3 finding 1). The stack offers it only as an explicit caller call (Cubes.cs) and documents that the engine never sends it. The comparison confirms that no production path sends it automatically.
- **MD5. Outbound signedness** (ImageRequest byte 1, BodyMotion words, NVCommand word@4) is not established, but it leaves the wire bytes unchanged for a given value. No record.

- **MD6. ImageChunk chunkDebug and status.**
  - Nothing in the engine or Unity reads either field for behaviour, so the engine cannot settle their signedness.
  - The only shipped declaration is Unity's generated CLAD (`ImageChunk.cs:212,217`: ReadInt32, ReadInt16). It outranks the json, which rests on pycozmo, so those types are used.
  - The choice changes no behaviour.

## Interfaces recorded for later layers (not M2 records)

- **M3:**
  - ImageChunk reassembly (Appendix C §3 R1..R10, handler H1..H5). M3-002 omits R3, R6, R7's timestamp order and R10.
  - FaceImage RLE (Appendix A §2 0x97);
  - EnableColorImages;
  - the connection-time calibration read (M1 CD21).
- **M4:**
  - RobotState interpretation (RS0..RS14): the head-angle calibration gate and out-of-range clamp (RS6), the accel filters (RS8), the gyro drift (RS9), the touch sensor (RS13, not read), and the signed currPathSegment (RS14);
  - the automatic SetBodyRadioMode {1,0} after 16 states without IS_BODY_ACC_MODE;
  - the actionId counter (MovementComponent+8);
  - backpack ms-to-frames `(ms+29)/30` and the TurnSignals companion;
  - MotorActionAck (R-P3);
  - the StreamObjectAccel objectID source.
- **M5:**
  - AnimationState (R-P5);
  - keyframe messages 0x93, 0x94, 0x99;
  - StartOfAnimation buffered, EndOfAnimation direct;
  - AudioSample and AudioSilence.
- **M11 / NV:** NVOpResult (R-P6); the pose frame and origin (RS2, RS3).
- **M12:**
  - PickAndPlaceResult actions (R-P1);
  - MovingLiftPostDock compares a byte with IDockAction+0x80 (R-P4; what +0x80 holds is UNKNOWN);
  - the DockWithObject builder;
  - the PlaceObjectOnGround int-to-float conversion.
- **M13:** PathFollowingEvent (R-P2); the ExecutePath id counter.
- **Every consumer layer:** handlers gate on the time-sync flag robot+0x29 (UpdateFullRobotState, AnimationState, ImageChunk). Each layer's inventory checks its own handlers.

## Existing record evidence found too weak (why every M2 record was reset to IMPLEMENTATION_GAP)

- **M2-001:** its evidence was a directory. The "string length prefixes" part is now covered by D10.
- **M2-002:** its evidence was a bare Unity symbol.
- **M2-003:** the "clamped 32..92" is not on the angle-to-height path.
- **M2-004:** "SetReadyToStreamAnims" had no address, and this pass found no such step. The record also omitted ImageRequest and AbsoluteLocalizationUpdate (now M1-041), and the location was stale.
- **M2-006:** its evidence was a capture folder, and the hash fields are not in the engine.
- **M2-007:** layout supported. The send gate is M1-041's.

## Appendix A: OUT pass (engine to robot), extractor report

The write of report.md was blocked, so this text is the report.

Extractor inventory, read-only. Primary source is `resources/lib/armeabi-v7a/libcozmoEngine.so` (Thumb). Every address below was disassembled in this pass with capstone. The scripts and dumps are in `C:\Windows\TEMP\claude\...\scratchpad\extract\M2-out\`: `cdis.py`, `rng.py`, `tagtab.txt`, `packs.txt`, `fields.txt`, `senders.txt`, `ctortags.txt`, `xref.py`/`calls.pkl` (a BL/BLX immediate xref of all of .text).

#### 0. The set, and the common send path

**The set.** It is every generated engine-to-robot type that `cozmo-stack/src/Cozmo.Robot` or `Cozmo.Transport` constructs, 42 messages in all. Found by grepping `new X`, `new Protocol.X`, and target-typed `X M() => new()` (DockActions.cs:321, Docking.cs:222). The C# was used only to pick the set.

Tags in the set: 03 05 08 0A 0B 25 32 34 35 36 37 39 3B 3C 3D 3E 3F 41 42 43 44 45 48 4A 4B 4C 58 60 64 66 80 81 8E 8F 93 94 97 99 9A 9B 9F A0.

The set is under 60 messages, so all 42 are covered and nothing in it is NOT DONE at the layout level. The builder-level gaps are in section 8, U4.

**Common path (M2 part; the M1 interfaces are only named):**

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| S1 | The tag byte is written first. `EngineToRobot::Pack(SafeMessageBuffer&)` writes the 1-byte union tag at this+0, then dispatches on tag-1 through a `tbh` table of 0xAF entries. The member struct sits at union +4. | 0x007AB6B8 `ldrb r0,[r5]` / 0x007AB6C2 `movs r2,#1` / 0x007AB6C4 `blx WriteBytes`; 0x007AB6CC `cmp r0,#0xae`; 0x007AB6D2 `tbh`, table at 0x007AB6D6 (decoded: tagtab.txt) | M2-001 (part) | EXACT_SOURCE |
| S2 | Single-scalar members are packed inline in the union Pack. 4-byte: 0x007AB834 `ldr r0,[r5,#4]`/`movs r2,#4`. bool: 0x007AB840 `Write<bool>`. 2-byte: 0x007AB84A `ldrh`/`movs r2,#2`. 1-byte: 0x007AB858. Empty: 0x007AB868. Each inline case agrees with that struct's own Pack. | same | NEW | EXACT_SOURCE |
| S3 | Total size = 1 + member Size(). | EngineToRobot::Size 0x007ABB98 `movs r0,#1` | NEW | EXACT_SOURCE |
| S4 | `WriteBytes` memcpy's the native little-endian value (0x0083C084). `Write<bool>` stores the bool byte as it is (0x0083C0DE `strbls r1,[r2]`); a C++ bool is 0/1. | 0x0083C06C, 0x0083C0CC | M2-001 (part) | EXACT_SOURCE |
| S5 | Byte-vector helper 0x0071A890. The u16 count = end-begin, stored with `strh`, so it is truncated with no range check (0x0071A898..0x0071A8A4). Then one WriteBytes(1) per element (0x00732108..0x00732130). The enclosing Size() uses the untruncated length (FaceImage 0x007BD462 = 2+(end-begin)). | 0x0071A890, 0x00732108 | M2-001 (part) | EXACT_SOURCE |
| S6 | `MessageHandler::SendMessage(id,msg,reliable,hot)`: Size (0x0069DD30), a vector, Pack(uchar*,size) (0x0069DD4A). Written≠Size is a failure (0x0069DD4E). Then RobotConnectionManager::SendData (0x0069DD58), the M1 interface. Gates: the connection flag / IsValidConnection (0x0069DD12) and `RobotManager::ShouldFilterMessage(id,tag)` (0x0069DD20); a filtered or not-connected send returns 1 (0x0069DD26). `Robot::SendMessage` 0x0051349C forwards through vtable+0x10 and logs the tag name on a non-zero result (0x005134BC..0x005134FC). | 0x0069DCF6, 0x0051349C | M1 interface | EXACT_SOURCE (interface) |
| S7 | The tag byte is written by each typed union constructor, confirmed for all 42 (ctortags.txt). For example SetHeadlight 0x007A7B34 `movs #0xb` / 0x007A7B38 `strb [r0]`. BackpackLightsMiddle 0x007A7714/0x007A7716. AudioSample 0x007AA434/0x007AA436. | ctortags.txt | NEW | EXACT_SOURCE |

**Type oracle.** Pack gives widths only.
- Float versus int comes from each struct's `operator==`: `vcmp.f32` means f32, integer `cmp` means an integer.
- bool comes from `Write<bool>`.
- Integer signedness cannot be read from Pack or `==`. Where it is not settled by a builder it says "signedness not established". It does not change the bytes for a given bit pattern.

**Flags.** Every direct send in the set uses reliable=1, hot=0 (`Robot::SendMessage` r2/r3 at each builder). The anim-stream messages are returned or buffered via `AnimationStreamer::BufferMessageToSend`, the M5 interface, not traced here.

#### 1. Per-message inventory

Notation: `order:type`, with byte offsets inside the member. "json" means `re-analysis/protocol/cozmo_robot_protocol.json`.

| message | tag | field layout (order:type) | size | citation | json agrees? | record | class |
|---|---|---|---|---|---|---|---|
| BackpackLightsMiddle | 0x03 | LightState[3] (each: u16 onColor, u16 offColor, u8 onFrames, u8 offFrames, u8 transOnFrames, u8 transOffFrames, 16-bit offset), then u8 | 31 | Pack 0x007A42EC (loop 0x007A42F6..0x007A4302, stride 0xA; u8 at +0x1E 0x007A4304); LightState::Pack 0x007CB312; LightState::Size 0x007CB3D2=10; Size 0x007A4354; ctor 0x007A7714 | Layout MATCH. DIFF: `native_size` is the string "variable" where it should be 31 (declared_size 31 is right). field1 has a generated name; the builder writes 0 (0x0063217E). The Unity LightState twin (ushort, ushort, byte×4, short) agrees. | M2-005 (colour only), NEW | EXACT_SOURCE |
| SetPropSlot | 0x05 | u32 factoryId, u8 slot | 5 | Pack 0x007A11A4; Size 0x007A120A; ctor 0x007A788C | MATCH | M4-010, NEW | EXACT_SOURCE |
| StreamObjectAccel | 0x08 | u32 objectID, bool enable | 5 | Pack 0x007B7B44 (Write<bool> 0x007B7B5E); Size 0x007B7BA0; ctor 0x007A7A1E | MATCH. The Unity twin agrees. | NEW | EXACT_SOURCE |
| SetAccessoryDiscovery | 0x0A | bool enable | 1 | Pack 0x007B7548; Size 0x007B7594; ctor 0x007A7AAE | MATCH. The Unity twin agrees. | NEW | EXACT_SOURCE (layout); the engine has no sender (section 3, finding 1) |
| SetHeadlight | 0x0B | bool | 1 | inline 0x007AB840; Pack 0x007A1CA8; Size 0x007A1CF4; ctor 0x007A7B34 | MATCH | NEW | EXACT_SOURCE |
| GetManufacturingInfo | 0x25 | empty | 0 | inline 0x007AB868; ctor 0x007A808C | MATCH | M2-004 | EXACT_SOURCE |
| DriveWheels | 0x32 | f32 lSpeed, f32 rSpeed, f32 lAccel, f32 rAccel | 16 | Pack 0x007A1EF0; Size 0x007A1F6E; eq 0x007A1F72 (4× vcmp.f32); ctor 0x007A82E8 | MATCH. The names hold transitively: HandleMessage<ExternalInterface::DriveWheels> copies the game message's words verbatim (0x0063EE8A..0x0063EE9A → 0x0063F174), and the Unity ExternalInterface/DriveWheels.cs order is l-speed, r-speed, l-accel, r-accel. | NEW | EXACT_SOURCE |
| MoveLift | 0x34 | f32 speed | 4 | inline 0x007AB834; Pack 0x007A2248; eq 0x007A22A2; ctor 0x007A8430 | MATCH | NEW | EXACT_SOURCE |
| MoveHead | 0x35 | f32 speed | 4 | inline 0x007AB834; Pack 0x007A2376; eq 0x007A23D0; ctor 0x007A84B6 | MATCH | NEW | EXACT_SOURCE |
| SetLiftHeight | 0x36 | f32 height, f32 maxSpeed, f32 accel, f32 duration, u8 actionId | 17 | Pack 0x007A24BA; Size 0x007A2548; eq floats 0..0xC; ctor 0x007A8560 | MATCH. actionId is marked uncertain in json; the builder settles it (section 2). | NEW | EXACT_SOURCE |
| SetHeadAngle | 0x37 | f32 angle, f32 maxSpeed, f32 accel, f32 duration, u8 actionId | 17 | Pack 0x007A2670; Size 0x007A26FE; ctor 0x007A8626 | MATCH (as above) | NEW | EXACT_SOURCE |
| SetBodyAngle | 0x39 | f32×4 (angle, maxSpeed, accel, tolerance), u16 numHalfRev, bool isAbsolute, u8 actionId | 20 | Pack 0x007A296A (Write<bool> 0x007A29BE); Size 0x007A2A10; eq 0x007A2A14; ctor 0x007A8770 | DIFF (type only): json has isAbsolute:u8; the engine writes a bool. 1 byte either way. | NEW | EXACT_SOURCE |
| StopAllMotors | 0x3B | empty | 0 | inline 0x007AB868; ctor 0x007A88A8 | MATCH | NEW | EXACT_SOURCE |
| ClearPath | 0x3C | u16 | 2 | inline 0x007AB84A; Pack 0x007A2D38; ctor 0x007A891C | MATCH. The builder always writes 0. | NEW | EXACT_SOURCE |
| AppendPathSegmentLine | 0x3D | f32×4, PathSegmentSpeed{f32×3} | 28 | Pack 0x007A3004; PathSegmentSpeed::Pack 0x007A2E54 (Size 0x007A2EC4=12); eq floats; ctor 0x007A89CE | MATCH | NEW | EXACT_SOURCE |
| AppendPathSegmentArc | 0x3E | f32×5, PathSegmentSpeed | 32 | Pack 0x007A31EA; ctor 0x007A8A9C | MATCH | NEW | EXACT_SOURCE |
| AppendPathSegmentPointTurn | 0x3F | f32×4, PathSegmentSpeed, bool | 29 | Pack 0x007A33EE (Write<bool> 0x007A343C); ctor 0x007A8B6C | DIFF (type only): json has useShortestDirection:u8; the engine writes a bool, and the builder normalises it to 0/1 (0x00507F76..0x00507F7E). | NEW | EXACT_SOURCE |
| ExecutePath | 0x41 | u16 pathId, bool | 3 | Pack 0x007A36EE; Size 0x007A374E; ctor 0x007A8C9C | MATCH | NEW | EXACT_SOURCE |
| DockWithObject | 0x42 | f32, f32 speed, f32 accel, f32 decel, u8 dockAction, bool, u8, u8 dockingMethod, bool | 21 | Pack 0x007C0660 (Write<bool> 0x007C06B4, 0x007C06DC); Size 0x007C071E; eq 0x007C0722 (vcmp.f32 at +0); ctor 0x007A8D56 | DIFF (types only): json has field0 unusedZero:u32; the engine type is f32. The value is 0 either way (r4=0 at 0x0063BB34, stored at 0x0063BB7E), so the bits are identical. field5 and field8 are bool, not u8. | NEW (M12 holds the semantics) | EXACT_SOURCE |
| AbortDocking | 0x43 | empty | 0 | Pack 0x007C0818; ctor 0x007A8DF4 | MATCH | NEW | EXACT_SOURCE |
| PlaceObjectOnGround | 0x44 | f32×6, bool | 25 | Pack 0x007C0926 (Write<bool> 0x007C0986); ctor 0x007A8E98 | DIFF (type only): json has field6:u8; the engine writes a bool. | NEW | EXACT_SOURCE |
| AbsoluteLocalizationUpdate | 0x45 | u32 timestamp, u32 frameId, u32 originId, f32 x, f32 y, f32 angle | 24 | Pack 0x007A384E; Size 0x007A38E8; eq 0x007A38EC (cmp at 0,4,8; vcmp at 0xC..0x14); ctor 0x007A8F60 | MATCH | M2-007, M1-041 | EXACT_SOURCE |
| DockingErrorSignal | 0x48 | u32, f32×4, bool, bool | 22 | Pack 0x007C0B26 (Write<bool> 0x007C0B78, 0x007C0B80); eq 0x007C0BC6; ctor 0x007A91A6 | DIFF (types only): json has field5 and field6 as u8; the engine writes bools. | NEW | EXACT_SOURCE (layout) |
| IMURequest | 0x4A | u32 length_ms | 4 | Pack 0x007C825A; ctor 0x007A92D8 | MATCH. The Unity twin (uint) agrees. | NEW | EXACT_SOURCE |
| SyncTime | 0x4B | u32 timestamp, f32 | 8 | Pack 0x007A3A94; eq 0x007A3AFA (word 0 is cmp; word 1 is vcmp.f32 at 0x007A3B0A); ctor 0x007A9374 | DIFF (type only): json has unknown:u32; the engine type is f32 (value −20.0f). The name is not established. | M2-004 | EXACT_SOURCE |
| ImageRequest | 0x4C | u8 mode, u8 resolution | 2 | Pack 0x007A3BDA; ctor 0x007A9404 | Width MATCH. The signedness of json's i8 is not established. | NEW | EXACT_SOURCE (layout) |
| StartMotorCalibration | 0x58 | bool, bool | 2 | Pack 0x007A1DA6; ctor 0x007A9A3E | MATCH as types. The names head/lift are json-only (U2). | NEW | EXACT_SOURCE (layout) |
| EnableStopOnCliff | 0x60 | bool | 1 | inline 0x007AB840; Pack 0x007A528C; ctor 0x007A9C2C | MATCH | NEW | EXACT_SOURCE |
| SetAudioVolume | 0x64 | u16 level | 2 | inline 0x007AB84A; Pack 0x007A13B8; ctor 0x007A9E50 | MATCH | M1-042, NEW | EXACT_SOURCE |
| EnableColorImages | 0x66 | bool | 1 | inline 0x007AB840; ctor 0x007A9F3C | MATCH | NEW | EXACT_SOURCE |
| RequestCrashReports | 0x80 | u32 | 4 | inline 0x007AB834; Pack 0x007A55C2; eq integer 0x007A561C; ctor 0x007A9FC2 | MATCH | NEW | EXACT_SOURCE |
| NVCommand | 0x81 | u32, 32-bit int, u8, u8, u16 count + u8[] | 12+n | Pack 0x007CE63C (vector through 0x0071A890 at 0x007CE68A); Size 0x007CE6CC; ctor 0x007AA0B2 | Layout MATCH. The signedness of json's `length:i32` is not established. | NEW | EXACT_SOURCE (layout) |
| AudioSample | 0x8E | u8[744], fixed, with no prefix | 744 | Pack 0x007BCD3C (loop 0x007BCD50..0x007BCD6A); Size 0x007BCDAA; ctor 0x007AA434 | MATCH | NEW | EXACT_SOURCE |
| AudioSilence | 0x8F | empty | 0 | Pack 0x007BCE58; ctor 0x007AA4CA | MATCH | NEW | EXACT_SOURCE |
| HeadAngle (anim) | 0x93 | u16 durationMs, i8 angleDeg | 3 | Pack 0x007BCF2C; ctor 0x007AA66E; signedness from the builder's sxtb at 0x004F8C20 | MATCH | NEW (M5/M7-009) | EXACT_SOURCE |
| LiftHeight (anim) | 0x94 | u16 durationMs, u8 heightMm | 3 | Pack 0x007BD064; ctor 0x007AA700; the builder's ldrb (0x004F8F8C) | MATCH | NEW | EXACT_SOURCE |
| FaceImage | 0x97 | u16 count + u8[] | 2+n | Pack 0x007BD416 → 0x0071A890; Size 0x007BD462; ctor 0x007AA8CA | MATCH | NEW (M5-013/M5-019 hold the content) | EXACT_SOURCE |
| BodyMotion | 0x99 | 16-bit speed, 16-bit radius | 4 | Pack 0x007BD6AE; ctor 0x007AAA2C | Width MATCH. The signedness of json's i16 is not established; the builder copies verbatim. | NEW | EXACT_SOURCE (layout) |
| EndOfAnimation | 0x9A | empty | 0 | Pack 0x007BDF6E; ctor 0x007AAAA4 | MATCH | NEW | EXACT_SOURCE |
| StartOfAnimation | 0x9B | u8 anim_id | 1 | Pack 0x007BDE88; ctor 0x007AAB18 | MATCH | NEW | EXACT_SOURCE |
| InitController | 0x9F | empty | 0 | Pack 0x007BDDA2; ctor 0x007AAC9C | MATCH | M2-004 | EXACT_SOURCE |
| SetAppRunID | 0xA0 | 16 bytes, packed as 4 × 32-bit words | 16 | Pack 0x007A5FBA (loop 0x007A5FC6..0x007A5FDC); ctor 0x007AAD28 | MATCH | NEW | EXACT_SOURCE |

All 42 sizes match json's declared_size and native_size. The one exception is the "variable" string on 0x03.

#### 2. Transformations inside the engine's own message builders (no behaviour traced)

- **0x03 BackpackLightsMiddle.** Built in `BodyLightComponent::SetBackpackLightsInternal` 0x00631F08.
  - Colour word for each of the 5 LEDs: `(c>>17)&0x7C00 | (c>>14)&0x3E0 | ubfx(c,11,5)`, plus 0x8000 if `c&0xFF`. Cited at 0x00631F50..0x00631F72 (on) and 0x00631F96..0x00631FBE (off).
  - The periods (ms at +0x28/+0x3C/+0x50/+0x64) become frames as `(ms+29)/30`, unsigned (umull by 0x88888889, >>4: 0x00631FC0..0x00631FD6, 0x00632012.., 0x0063203E.., 0x00632084.., 0x006320C4..). ms = 0xFFFFFFFF gives 0xFF (0x0063202C, 0x006320C0).
  - The offset (+0x78) becomes `(x+29)/30` signed (smmla at 0x00632112 and 0x00632134); −1 gives 0x00FF (0x00632140).
  - LEDs 1, 2 and 3 go to this message. LEDs 0 and 4 go to **BackpackLightsTurnSignals 0x11, which is sent right after in the same call** (0x00631F6C `orr r3,r1,#4` / 0x00631F76 `cmp r3,#4`; sends at 0x00632186 and 0x006321B2).
  - The trailing u8 is 0 (0x0063217E).
  - Only the colour word is covered by M2-005; the rest is NEW. EXACT_SOURCE.
- **0x05 SetPropSlot.** `Robot::ConnectToRequestedObjects` 0x00514A70. slot = loop index 0..4 (0x00514C64..0x00514C6C). Connect sends {factoryId, slot} (0x00514BB4..0x00514BC2); clear sends {0, slot} (0x00514C4A). M4-010. EXACT_SOURCE.
- **0x08 StreamObjectAccel.** enable = 1 in AddListener (0x0063554A) and 0 in RemoveListener (0x00635754). The objectID source (0x00635548..0x00635556) was not traced: RECOVERABLE_GAP.
- **0x0B SetHeadlight.** `BodyLightComponent::SetHeadlight(bool)` 0x00632344 sends the argument, after `VisionComponent::EnableMode(14,on)` (0x00632364).
- **0x32, 0x34, 0x35 (DriveWheels, MoveLift, MoveHead).** Verbatim word copies: helpers 0x0063F174, 0x00640E1C, 0x0063F900, 0x00640C00, 0x0063F6BC, 0x00640ACC. No conversion.
- **0x36 / 0x37 (SetLiftHeight / SetHeadAngle).** `MoveLiftToHeight` 0x00640700 and `MoveHeadToAngle` 0x006407CC.
  - The four float parameters are copied verbatim, in order (0x0064076C, 0x00640838 stm).
  - **The builder has no defaults and no unit conversion.**
  - actionId = ++(u8)MovementComponent+8, also written to the caller's `uchar*` when that is non-null (0x0064070C..0x00640726, 0x006407D8..0x006407F2). EXACT_SOURCE.
- **0x39 SetBodyAngle.** `MovementComponent::TurnInPlace(f,f,f,f,u16,bool,uchar*)` 0x00640898: verbatim, with the id from the same counter (0x006408B0..0x00640940).
- **0x3C ClearPath.** `PathComponent::ClearPath` 0x00649220 writes 0 (0x00649268).
- **0x3D, 0x3E, 0x3F (path segments).** `PathDolerOuter::Dole` 0x00507E4C copies from PathSegment verbatim and normalises the point-turn bool. Sent through the MessageHandler vfunc with reliable=1 and hot=0 (0x00507F4E..0x00508036).
- **0x41 ExecutePath.** `PathComponent::ExecutePath(Path const&, bool)` 0x0064A340: u16 = ++PathComponent+0x42 (0x0064A3C2..0x0064A3CA); bool = the argument (0x0064A34E, 0x0064A42A).
- **0x42 DockWithObject.** 0x0063BD50: field0 = 0 (0x0063BB34). The rest is copied from the arguments (M12).
- **0x44 PlaceObjectOnGround.** 0x00632B90: the first 3 fields are int32 arguments converted with `vcvt.f32.s32` (0x00632BAE..0x00632BEA). The rest is verbatim. NEW.
- **0x45 AbsoluteLocalizationUpdate.** 0x00512734: order as in M2-007, **gated by `PoseOriginList(Robot+0x294)::ContainsOriginID(parentId)`**, and skipped otherwise (0x00512766..0x00512772 `cbz r0,#0x5127c4`).
- **0x4A IMURequest.** `Robot::SendIMURequest` 0x00516B40: verbatim.
- **SendSyncTime 0x0051524C**, called by `Robot::SyncTime` 0x0051521E after `RobotStateHistory::Clear`:
  1. SyncTime{`GetCurrentTimeStamp()` (0x00515266), 0xC1A00000 = −20.0f (0x0051526C/0x00515270)}.
  2. If that send returned 0 (0x00515292 `cbnz`): InitController (0x0051529A).
  3. If that returned 0 (0x005152B2): **ImageRequest{mode 1 (Stream), resolution 4 (QVGA)}** (0x005152F0 `movw r0,#0x401`, 0x005152F6 `strh`).
  4. In every case: `SendAbsLocalizationUpdate(identity pose with parent Robot+0x294→+0x14, timestamp 0, frameId Robot+0x2B0)` (0x0051534A..0x005153AE).
  - The function returns the AbsLocUpdate result, and `Robot::SyncTime` stamps +0x520 only when that result is 0 (0x00515238..0x00515242).
  - "QVGA" is the name for 4 in the json/Unity ImageResolution enum.
- **0x4C ImageRequest (game path).** `CozmoEngine::HandleMessage<SetRobotImageSendMode>` 0x004EDE1C copies 2 bytes and stores mode at Robot+0x340.
- **0x58 StartMotorCalibration.** `MovementComponent::CalibrateMotors(bool a, bool b)` 0x0064059C: a→byte0, b→byte1 (0x006405A0..0x006405DC).
- **0x60 EnableStopOnCliff.** The HandleMessage<EnableStopOnCliff> helper 0x00528034 copies the byte verbatim. The 6 behaviour senders were not read.
- **0x64 SetAudioVolume.** `RobotAudioClient::SetRobotVolume(float)` 0x0059A214: level = low16(`vcvt.u32.f32(vol × 65535.0f)`) (constant 0x477FFF00 at 0x0059A21E; 0x0059A22A vmul; 0x0059A238 vcvt; 0x0059A244 strh). The float is also stored at +0x6C.
- **0x66 EnableColorImages.** `VisionComponent::EnableColorImages(bool)` 0x006582CC: verbatim, and also stored at +0x32A.
- **0xA0 SetAppRunID and 0x80 RequestCrashReports.** Both in `TracePrinter::HandleMessage<RobotConnectionResponse>` 0x0053D388, only when the response's first byte is 0 (0x0053D398).
  - The 16 bytes default to 0xFF (0x0053D39C..0x0053D3A4), are overwritten by `UUIDBytesFromString(DASGetPlatform() vfunc+0xC)` (0x0053D3A8..0x0053D3B8), and are built as raw bytes with `SetAppRunID(uchar const*,16)` (0x0053D41A).
  - If TracePrinter+0x28 ≥ 4, +0x28 is set to 1 and RequestCrashReports{0} is sent (0x0053D3CA..0x0053D3DC; helper 0x0053D318).
  - Not cross-checked against the M1 inventory.
- **0x93 / 0x94 (anim HeadAngle / LiftHeight).** `GetStreamMessage` at 0x004F8C08 and 0x004F8F80.
  - duration = (u16)kf+0xC, a `strh` truncation.
  - value = kf+0x10, or `RandIntInRange(v−var, v+var)` when var (kf+0x11) ≠ 0.
  - Head is signed (sxtb at 0x004F8C20); lift is unsigned.
- **0x99 BodyMotion.** 0x004FBA8C sends either kf+0x12 (when kf+8 == 0) or kf+0x16 (when kf+0x10 is set and kf+8 ≥ kf+0xC), verbatim, or nothing (0x004FBA92..0x004FBAD2).
- **0x97 FaceImage.**
  - `BufferFaceToSend` 0x0057C1FC: `DrawFace` then `FaceAnimationManager::CompressRLE` 0x00581904 (an M3/M5 interface). On RLE failure it logs `sErrorF` and buffers nothing (0x0057C272).
  - `FaceImageKeyFrame::GetStreamMessage` 0x004F9354: type 1 gives [40 3F 00]; type 0 gives [00]; any other type gives the 35-byte constant at 0x00C48FE0 (0x004F9368..0x004F93DA).
- **0x9B StartOfAnimation.** 0x0057C400: id = streamer+0xA0. It is **buffered, not sent directly** (0x0057C424), and +0x71 is set to 1.
- **0x9A EndOfAnimation.** `SendEndOfAnimation` 0x0057C448 sends it directly (0x0057C464..0x0057C476).
- **0x8E AudioSample.** `RobotAudioOutputSource::ProcessTick` 0x0059B9F4 encodes the payload with `encodeMuLaw` (0x0059BA32). On the no-data or muted branch it sends AudioSilence instead (0x0059BA08..0x0059BA1A).

#### 3. Findings

1. **SetAccessoryDiscovery 0x0A has no engine send site.** A BL/BLX xref of all of .text finds no calls to Create (0x007A7A74), the ctor (0x007A7AAE) or Set_ (0x007A7A8E/0x007A7ADA), and there is no PLT stub. An inlined or indirect construction is not excluded. So the engine side is **UNKNOWN / not an established engine behaviour**.
2. **The engine never sends BackpackLightsMiddle alone.** TurnSignals 0x11 always follows it in the same builder. The stack never constructs 0x11. NEW, M4 scope.
3. **Engine-to-robot messages that the engine sends and the stack never constructs.** Owners are the nearest exported symbol, so they are approximate. Full list in senders.txt and the scan.
   - cubes and lights: CubeLights 0x04, SetCubeGamma 0x0C and CubeID 0x10 (all in CubeLightComponent::SetLights, 0x0063A7F4 / 0x0063A77C / 0x0063A7C6);
   - robot state: SetBodyRadioMode 0x07 (UpdateFullRobotState 0x00512B3A), HeadAngleUpdate 0x38 (0x00516AEC), CarryStateUpdate 0x49 (0x00632C62);
   - motion: DriveWheelsCurvature 0x33, TurnInPlaceAtSpeed 0x3A (0x0063F496), EnableMotorPower 0x50 (0x0064067A), Disable/EnableAnimTracks 0x9D/0x9E (MovementComponent::LockTracks/UnlockTracks, 0x0064017C, 0x0063FFA8, 0x0064101E);
   - sensors and camera: SetCliffDetectThreshold 0x54 (0x0063435C), SetCameraParams 0x57 (0x0065615A, 0x00658420);
   - animation: AbortAnimation 0x8D (0x00517DFE), and the RecordHeading 0x91, TurnToRecordedHeading 0x92, Event 0x95 and BackpackLights 0x98 keyframe messages;
   - other: FlashObjectIDs 0x4D, KillBodyCode 0x06, the test and firmware messages.
4. **No builder sets any default for speed or acceleration.** The defaults in the MessageExtras ctors (SetHeadAngle 10/10, SetLiftHeight 3/20) are therefore not supported by any M2 builder. What values the original uses is decided by the M4 callers, which were not traced.
5. **No builder in the set converts angle or speed units.** The only value transforms are those listed in section 2.

#### 4. Helpers (item 4)

- **Colour word (M2-005, `LightState.Rgb`).** The engine expression is confirmed at CubeLightComponent::SendTransitionMessage 0x00638052..0x00638070 (the record's 0x00638056 is inside this range). It is also present in the backpack path at 0x00631F50..0x00631F72 and 0x00631F96..0x00631FBE, which is new evidence. The helper is algebraically the same expression on an RGBA word with R in the top byte and A in the bottom. Not re-checked: WhiteBalanceColor 0x0063A894, and the asset claim that every shipped config has a non-zero alpha.
- **ms→frames `(ms+29)/30`** (unsigned for the periods, signed for the offset, with −1 giving 0xFF). This is an engine builder transform and no record exists for it: **NEW**. The cube builder uses the same 0x88888889 constant (0x00638030) but was not read in full.
- **Angle and speed helpers.** Cozmo.Protocol has none, and the engine builder level has none either.
- **SyncTime.EngineConstant.** It is 0xC1A00000, which is −20.0f, typed f32 by the engine (0x007A3B0A).

#### 5. Existing records (item 5)

- **M2-001** (CLAD reader/writer + string prefixes), EXACT_SOURCE, evidence "re-analysis/protocol". **Too weak.**
  - The evidence is a directory, not a citation.
  - The outbound primitives are now cited as S1..S5 (0x0083C06C, 0x0083C0CC, 0x0071A890 with u16 truncation).
  - No outbound message carries a string, so the "string length prefixes" part of the claim is not supported by anything in the OUT scope.
- **M2-004** (connect sequence + SyncTime word), EXACT_SOURCE. **Partly supported.**
  - Confirmed: GetManufacturingInfo at 0x0052DE6C (MessageHandler vfunc, reliable=1, hot=0 at 0x0052DE70..0x0052DE7C). SyncTime, then InitController only on success (0x00515292). The −20.0f literal.
  - Omitted: SendSyncTime also sends ImageRequest{1,4} and AbsoluteLocalizationUpdate (0x005152EE..0x005153AE), and the function's result gates the +0x520 stamp. The "constant" word is typed f32.
  - Unverified: the "SetReadyToStreamAnims" line has no address. HandleMessage<RobotConnectionResponse> 0x005289AC shows `Robot::SyncTime` at 0x005289D2 and then an NVStorage one-shot callback at 0x00528A6E; this pass saw nothing named SetReadyToStreamAnims.
  - The record's location, CozmoRobot.cs, may be stale: the stack constructs these at CozmoEngine.cs:739-741.
- **M2-005** (colour word), EXACT_SOURCE. **Supported for the word itself.** It does not cover the ms→frames conversion on the same LightState (NEW), nor the TurnSignals companion message.
- **M2-007** (AbsLocUpdate order), EXACT_SOURCE. **Layout and order supported:** 0x0051279E..0x005127A4, with types via eq at 0x007A38EC. The record omits the ContainsOriginID send gate (0x0051276E/0x00512772). The layout claim holds; the send path is incomplete.

#### 6. Records contradicted by source

- **No manifest record is contradicted at the wire level.**
- **The json definition has type-only differences:**
  - SyncTime word1 is f32, not u32.
  - DockWithObject field0 is f32, not u32.
  - These are bool, not u8: SetBodyAngle isAbsolute, PointTurn useShortestDirection, DockWithObject field5 and field8, PlaceObjectOnGround field6, DockingErrorSignal field5 and field6.
  - 0x03 native_size "variable" should be 31.

#### 7. Records whose evidence is too weak for their status

- **M2-001**: see section 5.
- **M2-004**: see section 5. The rest of the SendSyncTime sequence and the SetReadyToStreamAnims line are uncited.

#### 8. Open questions / UNKNOWN

- **U1. Signedness not settled:** ImageRequest byte1, BodyMotion's two words, NVCommand word@4. The wire bytes are unaffected.
- **U2. Field names that are json/pycozmo-only, with no engine citation:**
  - StartMotorCalibration head/lift: the argument order is known, the names are not;
  - ClearPath and ExecutePath "unknown";
  - SyncTime word1;
  - DockWithObject field5/6/8;
  - DockingErrorSignal field5/6;
  - RequestCrashReports field0.
- **U3. SetAccessoryDiscovery:** an inlined or indirect engine send is not excluded.
- **U4. Builders not read (RECOVERABLE_GAP; M3/M5/M12 scope).** Addresses to read:
  - DockingErrorSignal: UpdateDockingErrorSignal 0x0063BE80..0x0063C56E;
  - NVCommand senders: 0x006438BA, 0x0064513A, 0x00645222, 0x00645392, 0x0064593C, 0x00645CEE;
  - AudioSample and AudioSilence in AnimationStreamer: 0x0057C5DE, 0x0057C5FA, 0x0057C9A0, 0x0057C9BC, 0x0057CB92;
  - PopRobotAudioMessage 0x00597E4E;
  - FaceAnimationKeyFrame::GetStreamMessage 0x004F97C8;
  - the StreamObjectAccel objectID source;
  - the StopLift, StopHead and StopBody argument values;
  - the 6 behaviour senders of EnableStopOnCliff;
  - the factory sender of StartMotorCalibration 0x005CFAC2.
- **U5.** TracePrinter+0x28 (the ≥4 gate on RequestCrashReports) is not identified.
- **Manager decisions:**
  - whether to correct the json type-only differences;
  - whether BackpackLightsTurnSignals 0x11 and the other engine-sent messages in finding 3 enter M2 scope or stay with M4/M5.

## Appendix B: IN pass (robot to engine), extractor report

The write of report.md was blocked, so this message is the full report. My scratch scripts and dumps are in `...\scratchpad\extract\M2-in\`: `d.py`, `tbl.py`, `all.py`, `unpacks.txt` and `ufrs.txt`. All addresses are libcozmoEngine.so Thumb virtual addresses.

**Two contradictions to know first** (details in §5):
- **FallingStopped (0xDE):** the json and the generated codec have the fields shifted by one.
- **PickAndPlaceResult blockStatus:** the stack's `BlockStatus` values 1 and 2 are swapped.

#### 0. The set

- **Every codec feeds the size check.** `MessageHandler.TryUnpack` in CozmoEngine.cs runs `GeneratedMessages.Parsers` for every tag that has a codec. So all 56 robot-to-engine codecs in `re-analysis/protocol/cozmo_robot_protocol.json` decide the M1 drop-or-Broadcast outcome, and all 56 are covered below.
- **Consumed subset (marked U):** B4 B5 B6 B8 C0 C2 C4 C5 C6 C9 CD CE CF D0 D1 D2 D7 D9 DE ED EE F0 F1 F2 F3 F4 F5.
- **Not consumed (only names or comments match):** B0–B3, B7, B9–BF, C1, C3, C7, C8, CA, CB, D3–D6, D8, DA–DD, EC, EF.
- **M1 interfaces, not redone here:**
  - CC32/B27: ProcessMessages, 0x69D870..0x69D9FC
  - CC33/CC34: the filter
  - CC12..CC16: robotError
  - CC36: Broadcast
  - CC4/CD23: the time-sync gate (robot+0x29)
  - B29, CB12..CB18, G5.x: the handshake

#### 1. Dispatch and size check

| step | what the original does | citation | record | classification |
|---|---|---|---|---|
| D1 | ProcessMessages builds a fresh RobotToEngine with tag 0xFF and calls `Unpack(const u8*, len)`. If the return value is not the buffer length, it logs an error and drops the message. | 0x69D8F8 (movs r0,#0xff / strb [sp,#0x140]); 0x69D902; 0x69D906..0x69D910 (`subs r0,r1,r0; cmp r5,r0; bne`) | M1-027 (interface) | EXACT_SOURCE |
| D2 | `Unpack(ptr,len)` wraps the bytes in `SafeMessageBuffer(ptr,len,false)` and calls `Unpack(SMB&)`. | 0x7B510A..0x7B511E | NEW | EXACT_SOURCE |
| D3 | The tag is read with `ReadBytes(&t,1)`, initialised to 0xFF. If it differs from the union's current tag, ClearCurrent runs and the member is placement-constructed. No wire effect. | 0x7B1ADC..0x7B1AFC; e.g. 0x7B1BD2 cmp r6,#0xb0 / bne.w 0x7B1E94 | NEW | EXACT_SOURCE |
| D4 | The switch is `subs r0,#0xb0; cmp r0,#0x45; bhi default; tbh`. The TBH table is at **0x7B1B10**: 70 halfwords, target = 0x7B1B10 + 2*entry. Tags below 0xB0 wrap around and take the default. | 0x7B1B04..0x7B1B0C; table 0x7B1B10..0x7B1B9B | M1-027 / CC35 | EXACT_SOURCE |
| D5 | The default at 0x7B1BBE stores the tag and returns GetBytesRead, which is 1. Out-of-range tags take it, and so do the 14 in-range tags with no codec: **0xCC and 0xDF..0xEB**. They behave exactly like CC35. The other 56 in-range tags have codecs, and they are exactly the 56 json entries. **This settles the M1-027 residual.** | TBH entries → 0x7B1BBE; 0x7B1BBE..0x7B1BCC | M1-027 | EXACT_SOURCE |
| D6 | Some codecs are inline in the switch: C2 and C3 read nothing (→0x7B1BB8); C4, CA, CB and D4 do ReadBytes(1) (→0x7B1B9C); DA and DB do Read<bool> (→0x7B1BA4); DD and EC do ReadBytes(4) (→0x7B1BAE). | 0x7B1B9C..0x7B1BB4 | NEW | EXACT_SOURCE |
| D7 | The return value is always GetBytesRead (cursor − base, including the tag byte). Member Unpacks tail-call it through veneer 0x8CDE1C → 0x4BFBDC. Every per-field and helper success flag is ignored. | 0x83C032..0x83C038; 0x7B1BBE..0x7B1BCA | NEW | EXACT_SOURCE |
| D8 | `ReadBytes` is all-or-nothing per call. On overrun it copies nothing, does not advance and returns 0, with **no sticky error**, so later smaller reads can still succeed. | 0x83C09A..0x83C0CA | M2-001 (partial) | EXACT_SOURCE |
| D9 | `Read<bool>` stores (byte != 0), so any non-zero byte becomes 1. | 0x83C0F0..0x83C118 | M2-001 (partial) | EXACT_SOURCE |
| D10 | The variable-length helpers read the count, then the elements one at a time, and stop at the first failed element. The member discards the helper's return value. See the helper list below the table. | as listed below the table | M2-001 (partial) | EXACT_SOURCE |
| D11 | The variable-length size check is only "bytes consumed == length". See the consequences below the table. | derived from D1, D7, D8, D10 | NEW | EXACT_SOURCE (derived) |
| D12 | Fixed arrays have no prefix, and their loops stop at the first failure. | e.g. 0x7D6EB6..0x7D6EDA, 0x7C8852..0x7C886A | M2-001 | EXACT_SOURCE |
| D13 | The buffer is little-endian: fields are memcpy'd natively and the ELF has EI_DATA=1. | 0x83C0B6; .so byte 5 = 0x01 | M2-001 | EXACT_SOURCE |
| D14 | InitRobotMessageComponent subscribes handlers per tag (order below the table). Tags subscribed elsewhere: C6 in the PathComponent ctor (0x648C6E), CD in NVStorageComponent, CF/B0/EC in TracePrinter. | 0x532A9C..0x533030 | NEW (interface) | EXACT_SOURCE for the list; most handlers were not read |

**D10 helpers:**
- u8-count i32[]: 0x73923A → 0x7549D6
- u8-count string, i8 chars read with ldrsb: 0x6C235C → 0x6C39B0
- u8-count u8[]: 0x7A17D8 → 0x73213C
- u16-count u8[]: 0x71A8F6 → 0x73213C
- u8-count u32[]: 0x71283C → 0x730C7C

**D11 consequences:**
- Trailing bytes beyond the count always mismatch, so the message is dropped.
- A count larger than the data present is **accepted with a shorter array** whenever the remaining bytes form whole elements. That is always the case for u8 data: ImageChunk.data, NVOpResult.data and FirmwareVersion.signature.
- A truncated fixed message is normally a mismatch. It is accepted if later, smaller reads happen to consume exactly the leftover bytes (because of D8).

**D14 subscription order:** B1 D2 B8 F3 D0 B4 B5 D7 DD DE B3 D4 C0 C1 F2 F4 BF C7 C2 C3 C9 EE D1 D8, then D3, then ED C8 CE, then (through 0x519F8C) F0 F1 BA BB BC BD B2 B7 DC.

#### 2. Codecs: engine Unpack compared with the json

Notation:
- `4B`, `2B`, `1B` are ReadBytes(n). The Unpack alone does not settle float vs int or signedness; notes say when a consumer or Unity does.
- `bool` is Read<bool>.
- Size is the payload; the wire length is 1 + payload.
- Unity twins are lower authority and only confirm agreement.

| message | tag | field layout | size | citation | json agrees? | record | class |
|---|---|---|---|---|---|---|---|
| PrintTrace | B0 | 4B@0, 2B@4, 1B@6, u8-count i32[]@8 | 7+1+4n | 0x7D4BEA (0x7D4BF6/0x7D4C00/0x7D4C0A; bl 0x73923A) | DIFF: the json splits the first 4B into u16+u16; the engine has one 4-byte field. Bytes identical. | NEW | EXACT_SOURCE |
| PrintText | B1 | 1B, u8-count string@4 | 1+1+n | 0x7D5280 | MATCH | NEW | EXACT |
| MainCycleTimeError | B2 | 4B×4 | 16 | 0x7D4D7C | MATCH | NEW | EXACT |
| GoalPose | B3 | RobotPose(5×4B), bool@0x14 | 21 | 0x7C21F6; 0x7D6A9C; Read<bool> 0x7C2206 | DIFF: bool, not u8 | NEW | EXACT |
| ObjectMoved U | B4 | 4B, 4B, ActiveAccel(3×4B), 1B UpAxis | 21 | 0x7B0124; 0x7B993C; Unity ObjectMoved.cs:109 | MATCH | NEW (M4-009 consumer) | EXACT |
| ObjectStoppedMoving U | B5 | 4B, 4B | 8 | 0x7B02A2; Unity :71 | MATCH | NEW | EXACT |
| ObjectTapped U | B6 | 4B, 4B, 1B, 1B, 1B, 1B | 12 | 0x7B06CA; Unity :135 (u32,u32,u8,u8,i8,i8) | MATCH | NEW | EXACT |
| DataDump | B7 | 4B, u8-count u8[] | 4+1+n | 0x7D511C | MATCH | NEW | EXACT |
| PickAndPlaceResult U | B8 | 4B, bool@4, 1B@5, 1B@6 | 7 | 0x7C1AC6; Read<bool> 0x7C1ADA | DIFF: field1 is bool, not u8 (semantics in R-P1) | NEW | EXACT |
| ObjectTappedFiltered | B9 | 4B, 4B, 1B, 1B | 10 | 0x7B0538 | MATCH | NEW | EXACT |
| RampTraverseStart / BridgeTraverseStart | BA/BC | 4B | 4 | 0x7C1D7C / 0x7C1FC0 | MATCH | NEW | EXACT |
| RampTraverseComplete / BridgeTraverseComplete | BB/BD | 4B, bool | 5 | 0x7C1E88 / 0x7C20CC | DIFF: bool, not u8 | NEW | EXACT |
| TimeProfileStat | BE | 4B, 4B, bool, u8-count string@0xC | 9+1+n | 0x7D4F28 | DIFF: field2 is bool | NEW | EXACT |
| IMUDataChunk | BF | 6×(4B[8]), 1B, 1B, 1B | 195 | 0x7C8844 | MATCH | NEW | EXACT |
| CliffEvent U | C0 | 4B, 1B, bool | 6 | 0x7D2DBA; Unity CliffEvent.cs:87 | MATCH | NEW (M4-008) | EXACT |
| PotentialCliff | C1 | none | 0 | 0x7D2F16 | MATCH | NEW | EXACT |
| SyncTimeAck U / RobotPoked | C2/C3 | none (inline) | 0 | TBH → 0x7B1BB8 | MATCH | NEW | EXACT |
| MotorActionAck U | C4 | 1B inline | 1 | 0x7B1B9C | MATCH (see R-P3) | NEW | EXACT |
| MovingLiftPostDock U | C5 | 1B (ReadBytes, not bool) | 1 | 0x7C1C5A | MATCH on width (see R-P4) | NEW | EXACT |
| PathFollowingEvent U | C6 | 2B, 1B | 3 | 0x7B114C | MATCH on widths; the engine name is pathID, not event_id (R-P2) | NEW | EXACT |
| IMURawDataChunk | C7 | 2B[3], 2B[3], 1B, 1B | 14 | 0x7C9094; Unity :122 | MATCH | NEW | EXACT |
| DefaultCameraParams | C8 | 4B, 4B, 2B, 2B, 1B[17] | 29 | 0x7BF1D6 | MATCH | NEW | EXACT |
| RobotAvailable U | C9 | 4B, 2B | 6 | 0x7B8F26 | MATCH; the engine ignores the payload (M1 CB12) | NEW | EXACT |
| AnimationStarted / AnimationEnded | CA/CB | 1B inline | 1 | 0x7B1B9C | MATCH | NEW | EXACT |
| (no codec) | CC, DF..EB | tag only | – | → 0x7B1BBE | absent from the json (correct) | M1-027 | EXACT |
| NVOpResult U | CD | 4B, 4B, 1B@8, 1B@9, u16-count u8[]@0xC | 10+2+n | 0x7CE76A; 0x71A8F6 | MATCH (R-P6) | NEW (M11-011 interface) | EXACT |
| ObjectPowerLevel U | CE | 4B, 4B, 1B | 9 | 0x7B0888; Unity :87 | MATCH | NEW | EXACT |
| CrashReport U | CF | 4B, 2B, 1B, u8-count 4B[]@8 | 7+1+4n | 0x7D3CCA; 0x71283C | MATCH (the stack only uses it as a trigger, M1 CD22) | NEW | EXACT |
| ObjectConnectionState U | D0 | 4B, 4B, 4B ObjectType, bool@0xC | 13 | 0x7AFE20; Unity :103 (u32,u32,i32,bool) | MATCH | NEW (M4-009) | EXACT |
| MotorCalibration U | D1 | 1B, bool, bool | 3 | 0x7CDF2E; Unity :87 | MATCH | NEW | EXACT |
| FWVersionInfo U | D2 | 4B×3, 1B[16], 1B[16] | 44 | 0x7C32E6 | MATCH (only its arrival matters, M1 B29) | NEW | EXACT |
| DockingStatus | D3 | 4B, 1B | 5 | 0x7C1562 | MATCH | NEW | EXACT |
| RobotStopped | D4 | 1B inline | 1 | 0x7B1B9C | MATCH | NEW | EXACT |
| AnimationEvent | D5 | 4B, 1B, 1B | 6 | 0x7B15EA | MATCH | NEW | EXACT |
| FactoryTestParameter | D6 | 4B | 4 | 0x7C44F2 | MATCH | NEW | EXACT |
| ObjectUpAxisChanged U | D7 | 4B, 4B, 1B | 9 | 0x7B03D4; Unity :87 | MATCH | NEW | EXACT |
| MotorAutoEnabled | D8 | 1B, bool | 2 | 0x7CE080 | MATCH | NEW | EXACT |
| RobotErrorReport U | D9 | 4B, bool | 5 | 0x7D3E5C; Read<bool> 0x7D3E70 | DIFF: bool, not u8 (M1 CC12 already has bool) | M1-027 | EXACT |
| LiftLoad | DA | bool inline | 1 | 0x7B1BA4 | DIFF: bool, not u8 | NEW | EXACT |
| BackpackButton | DB | bool inline | 1 | 0x7B1BA4 | MATCH | NEW | EXACT |
| IMUTemperature | DC | 4B | 4 | 0x7C945E | MATCH | NEW | EXACT |
| FallingStarted | DD | 4B inline | 4 | 0x7B1BAE | MATCH on width | NEW | EXACT |
| **FallingStopped U** | DE | **engine: timestamp u32@0, duration_ms u32@4, impactIntensity f32@8** | 12 | 0x7B0EB6; handler evidence below the table | **CONTRADICTED**: json has duration_ms@0, impactIntensity@4, field2@8, matched to the 8-byte game message (Unity ExternalInterface/FallingStopped.cs:71), the wrong twin | NEW | EXACT |
| WiFiFlashID | EC | 4B inline | 4 | 0x7B1BAE | MATCH | NEW | EXACT |
| ManufacturingID U | ED | 4B×3 | 12 | 0x7B1750 | MATCH (M1 CB18) | M1-028 | EXACT |
| FirmwareVersion U | EE | 2B@0, u16-count u8[]@4 | 2+2+n | 0x7B907E | MATCH on layout; the name "robotId" is not established and the handshake never reads it (M1 G5.2 reads msg+4..+8) | M2-006/M1-029 | EXACT (layout) |
| OTA::Ack | EF | 4B, 2B, 1B | 7 | 0x7B87A2 | MATCH | NEW | EXACT |
| RobotState U | F0 | see §3 | 91 | 0x7D6E24..0x7D6EFC; Unity RobotState.cs:304 | MATCH | M2-002/003 | EXACT |
| AnimationState U | F1 | 4B, 4B, 4B, 1B, 1B, 1B | 15 | 0x7D87B6; Unity AnimationState.cs:135 (u32,i32,i32,u8×3) | MATCH | NEW | EXACT |
| ImageChunk U | F2 | 4B, 4B, 4B, 1B×4, 2B@0x10, u16-count u8[]@0x14 | 18+2+n | 0x7C60B4; 0x71A8F6 | MATCH on widths; signedness not settled (below the table) | NEW (M3-002 interface) | EXACT on widths; signedness RECOVERABLE_GAP (read the loads in EncodedImage::AddChunk) |
| ObjectAvailable U | F3 | 4B, 4B ObjectType, 1B | 9 | 0x7AFCBC; Unity :87 | MATCH; rssi is signed (ldrsb 0x533996, 0x5339B4) | NEW (M4-009) | EXACT |
| ImageImuData U | F4 | 4B×4, 1B | 17 | 0x7C62F6; Unity :119 | MATCH | NEW | EXACT |
| ObjectAccel U | F5 | 4B, 4B, ActiveAccel | 20 | 0x7B09EC; Unity :93 | MATCH | NEW | EXACT |

**FallingStopped handler evidence** (HandleFallingStopped 0x535040):
- The log is "timestamp: %u, duration (ms): %u, intensity %.1f": ldrd r1,r2,[r5] and vldr s0,[r5,#8], 0x535066..0x535084.
- +4 goes to to_string(unsigned) at 0x5350CC.
- The game message {duration_ms, impactIntensity} is built from ldrd [r5,#4] at 0x535186.
- An intensity above 1000.0 triggers NeedsManager action 0x11 (0x5350AA..0x5350C2).

**ImageChunk signedness:** it differs from Unity ImageChunk.cs:208 on three fields, and the engine Unpack does not settle any of them:
- chunkDebug: Unity i32, json u32
- imageEncoding: Unity u8, json i8
- status: Unity i16, json u16

**Json metadata:**
- `native_size` says "variable" for 0xB3, 0xB4, 0xF0 and 0xF5, but those messages are fixed; `declared_size` is correct.
- The json helpers block and the enum widths (ObjectType 4, UpAxis/MotorID/AnimEvent 1) agree with the engine.

#### 3. How the engine interprets the fields the stack consumes

##### RobotState: consumer Robot::UpdateFullRobotState 0x51291C (r5 = message)

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| RS0 | The whole function is skipped while robot+0x29 (time synced) == 0. | 0x51293C..0x512942 → 0x512A8A | M1 CC4/CD23 | EXACT |
| RS1 | timestamp → robot+0x2C, and also to CliffSensorComponent+8. | 0x51294C..0x512954; 0x634032 | NEW | EXACT |
| RS2 | pose_frame_id is compared with robot+0x2B0; the history lookup is GetLastStateWithFrameID. | 0x512CD8..0x512CE0; 0x51307C..0x513088 | NEW (M11) | EXACT (the branches were not read further) |
| RS3 | pose_origin_id → PoseOriginList Contains/GetOriginByID; an unknown id leads to a warning. | 0x512C3E..0x512C54; 0x512ED6 | M11-019 | EXACT |
| RS4 | The pose is Radians(+0x18) about Z, with translation +0xC..+0x14. pitch (+0x1C) → Radians → robot+0x304. | 0x512C5A..0x512C9C; 0x512974..0x512982 | NEW | EXACT |
| RS5 | Wheel speeds are stored raw at robot+0x30C and +0x310. | 0x512B66..0x512B72 | NEW | EXACT |
| RS6 | headAngle goes to SetHeadAngle 0x513358. Detail below the table. | 0x51335E..0x5133E8; constants 0x513444..0x513450 | NEW (M4) | EXACT |
| RS7 | liftAngle → robot+0x300 raw, plus ComputeLiftPose. GetLiftHeight is 66·sinf(+0x300)+45+0, with **no clamp**. | 0x51295E..0x51296E; 0x516F64..0x516F8E; literals 0x516F90/94/98 = 66, 45, 0 | M2-003 | EXACT |
| RS8 | accel → robot+0x360 raw. \|a\| → +0x378. +0x37C = 0.05\|a\| + 0.95·old. +0x380..+0x388 = 0.1a + 0.9·old. | 0x5129A4..0x512A6E; literals 0x512D04..0x512D10 | NEW (M10) | EXACT |
| RS9 | gyro → robot+0x36C raw, then DetectGyroDrift and DetectBias. | 0x5129B0..0x5129BE; 0x512FBA; 0x512FC4 | NEW (M10) | EXACT (callees not read) |
| RS10 | batteryVoltage → robot+0x33C raw. | 0x512B60..0x512B62 | NEW | EXACT |
| RS11 | The whole status word → robot+0x350. | 0x512AD8..0x512ADC | M2-002 | EXACT |
| RS12 | cliffDataRaw: 8-byte copy to CliffSensorComponent+0xE. | 0x63401A..0x634022 | M4-008 | EXACT |
| RS13 | backpackTouchSensorRaw → TouchSensorComponent::Update. | 0x512990..0x512996 | NEW | RECOVERABLE_GAP (body 0x64E75C not read) |
| RS14 | currPathSegment is **signed** (ldrsb) → PathComponent::UpdateCurrentPathSegment(signed char). | 0x51299A..0x5129A0 | NEW | EXACT |

**RS6 detail (head angle):**
- The value is ignored until the head is calibrated. robot+0x314 is written only by SetHeadCalibrated at 0x51519A.
- Below −28° (−0.488692 rad) it is stored as −25° (−0.436332).
- Above 47.5° (0.829031) it is stored as 44.5° (0.776672).
- Both out-of-range cases log the warning Robot.GetCameraHeadPose.HeadAngleOOB.
- An in-range value is stored at robot+0x2FC, and the camera pose is recomputed.

**Status bits.** The names come from the engine's EnumToString at 0x7D57C8 and match all 17 values in Unity RobotStatusFlag.cs:8-25.

| bit | name | engine use | address |
|---|---|---|---|
| 0x1 | IS_MOVING | MovementComponent+9 | 0x63E30A..0x63E31A |
| 0x2 | IS_CARRYING_BLOCK | argument to Delocalize on the treads path | 0x512B98..0x512BA6 |
| 0x4 | IS_PICKING_OR_PLACING | [robot+0x280]+4 | 0x512A96..0x512AA0 |
| 0x8 | IS_PICKED_UP | robot+0x349; also read by the treads classifier | 0x512AA2..0x512AA8; 0x511F04 |
| 0x10 | IS_BODY_ACC_MODE | robot+0x34C, plus an automatic radio-mode send (NEW, below the table) | 0x512ACE..0x512B52 |
| 0x20 | IS_FALLING | the treads classifier | 0x511EC4 |
| 0x100 | LIFT_IN_POS | MovementComponent+0xB = !bit | 0x63E32C..0x63E334 |
| 0x200 | HEAD_IN_POS | MovementComponent+0xA = !bit | 0x63E320..0x63E328 |
| 0x1000 | IS_ON_CHARGER | SetOnCharger(bit) | 0x512AAC..0x512AB4 |
| 0x2000 | IS_CHARGING | robot+0x339 | 0x512AB8..0x512ABE |
| 0x4000 | CLIFF_DETECTED | CliffSensorComponent+6 | 0x634026..0x634030 |
| 0x8000 | ARE_WHEELS_MOVING | MovementComponent+0xC | 0x63E338..0x63E340 |
| 0x10000 | IS_CHARGER_OOS | robot+0x33A (ldrh +0x4E & 1) | 0x512AC2..0x512ACA |

**IS_BODY_ACC_MODE (0x10) behaviour:**
- While the bit is clear, a counter at +0x34D increments.
- At 16 the engine logs a warning, sends **SetBodyRadioMode {0x01,0x00}** reliably, and resets the counter.
- The counter is not reset while the bit is set.
- This is an engine-to-robot send triggered by RobotState.

**Notes on the bits:**
- robot+0x2BC |= 1 when MovementComponent+0xA or +0xC is set (0x512B76..0x512B8E).
- Bits 0x40, 0x80, 0x400 and 0x800 are not consumed in the three functions read; nothing else was searched.
- For each bit, only the storage location is established. What the engine later does with those fields is outside this pass.

##### Other consumed messages

| step | what the original does | citation | record | class |
|---|---|---|---|---|
| R-P1 | PickAndPlaceResult: +4 is the success bool; +5 is DockingResult (signed); +6 is BlockStatus **0 NO_BLOCK, 1 BLOCK_PLACED, 2 BLOCK_PICKED_UP**. Actions below the table. | HandlePickAndPlaceResult 0x533794..0x5337AE; 0x533848..0x533850; 0x533898..0x5338AE; EnumToString(BlockStatus) 0x7C168C, table 0x1034984 | NEW (M12) | EXACT |
| R-P2 | PathFollowingEvent: +0 is u16 pathID, compared with the last sent and received IDs. +2 is PathEventType, with a switch on 0, 1 and 2. | lambda 0x64B370 (vtable 0x102F5C8 slot 6), 0x64B37E..0x64B3DA | NEW (M13) | EXACT |
| R-P3 | MotorActionAck: the byte is compared for equality with the waiting action's +0xDA. | 0x54D408..0x54D414 | NEW | EXACT (owning class not identified) |
| R-P4 | MovingLiftPostDock: the handler compares the byte for equality with IDockAction+0x80; it is **not a bool**. | 0x557C74..0x557C80 ("IDockAction.MovingLiftPostDockHandler") | NEW (M12) | compare EXACT; meaning of +0x80 UNKNOWN (RECOVERABLE_GAP) |
| R-P5 | AnimationState: ignored while +0x29 == 0. Otherwise +4 → robot+0x238, +8 → +0x240, +0xC → +0x348, +0xD → +0x248, and +0xE → SetClientDropCount. The timestamp is unused. | 0x537FCA..0x538020; veneer 0x8CB40C → 0x4AA408 | NEW (M5) | EXACT |
| R-P6 | NVOpResult: +8 is NVOperation (ldrb); +9 is NVResult (**ldrsb**, signed). | 0x642FAE..0x642FDE | M11-011 | EXACT |
| R-P7 | ImageChunk: dropped while +0x29 == 0; otherwise passed to EncodedImage::AddChunk(robot+0x394). | 0x535A7A..0x535A96 | M3-002 | EXACT |

**R-P1 actions:**
- Status 2 with success calls SetDockObjectAsAttachedToLift.
- Status 1 with success calls SetCarriedObjectAsUnattached(false).
- The non-NoBlock paths call EnableMode(1,1).
- The branch compares are `cmp r0,#2 → 0x5337F8` (the "…BlockPickedUp" log) and `cmp #1 → 0x533856` (the "…BlockPlaced" log).

#### 4. Existing records: does their evidence support the full claim?

- **M2-001: partly.** The evidence is a directory (`re-analysis/protocol`) at authority 4, which is not a citation.
  - The engine supports the reader half: D8..D13, including bool = non-zero and skip-on-failure.
  - The writer half was not read in this pass (out of IN scope).
  - As cited, it is too weak for EXACT_SOURCE.
- **M2-002: names and values yes, meanings only as storage locations.** The evidence is a bare Unity symbol, not a citation. The engine supports it through EnumToString 0x7D57C8 plus the bit table in §3.
- **M2-003: supported for angle to height** (RS7; sinf of the raw value confirms radians). **The "clamped 32..92" is not on that path.** It exists only in the inverse, ConvertLiftHeightToLiftAngleRad 0x5170B0:
  - the height is raised to 32 (literal at 0x5170F4);
  - for heights of 92 or more (0x517100) the value 0.712121 is used (0x517104);
  - the result goes through asinf (veneer 0x8CAE7C → 0x4A8128).
  - The record title reads as if angle to height were clamped. RS0 also applies: the value is only stored after sync.
- **M2-006: too weak on its own evidence.** It cites a captures directory (authority 5).
  - The engine source is M1 G5.2..G5.6 (0x52D48C..0x52D5B6): the keys build, version, time and sim, parsed from msg+4.
  - `messageEngineToRobotHash` and `messageRobotToEngineHash` (the stack's FirmwareVersion extras) appear nowhere in libcozmoEngine.so (0 byte hits).

#### 5. Existing records contradicted by the source

- **FallingStopped 0xDE codec (json and generated):** the engine layout is {timestamp u32, duration_ms u32, impactIntensity f32}. Citations: 0x535066..0x535084, 0x5350CC, 0x535186.
  - Sensors.cs builds FallingStoppedReport(DurationMs, ImpactIntensity), so it reads the timestamp as the duration and the duration's bits as a float.
  - No manifest id covers this codec.
- **PickAndPlaceResult blockStatus:** Docking.cs `BlockStatus { BlockPickedUp = 1, BlockPlaced = 2 }` and its comment citing 0x00533781 are swapped. The engine has 1 = PLACED and 2 = PICKED_UP (R-P1). This is an M12 consumer.

#### 6. Evidence too weak to keep its status

- M2-001, M2-002 and M2-006, as in §4.
- The json's FallingStopped "prefix" twin is the wrong type.
- The json's `native_size: variable` on 0xB3, 0xB4, 0xF0 and 0xF5 is wrong; those messages are fixed.

#### 7. Open questions for the manager

1. **D11 size check.** The engine accepts over-counted u8 arrays and some truncated fixed messages. Should the stack reproduce that exact "consumed == length, skip on failure" rule? It is an M1-027/M2-001 decision.
2. **Bool normalisation (D9).** Seven fields are bool in the engine but u8 in the json: B3, B8, BB, BD, BE, D9 and DA.
3. **New M4 behaviour.** RS6 (the head-calibrated gate and the out-of-range clamp) and the automatic SetBodyRadioMode after 16 states without IS_BODY_ACC_MODE.
4. **R-P4.** What IDockAction+0x80 holds: read its writers.
5. **ImageChunk signedness.** Read the loads in EncodedImage::AddChunk.
6. **RS13.** The TouchSensorComponent::Update body at 0x64E75C was not read.
7. **Time-sync gates.** Several handlers gate on robot+0x29 (UpdateFullRobotState, AnimationState, ImageChunk). Handlers I did not read may do the same, and each layer's inventory should check.

NOT DONE: nothing is missing from the codec set; all 56 are covered. Handler semantics beyond §3 were out of scope and were not read.

## Appendix C: gap pass (ImageChunk), extractor report

ImageChunk (0xF2) field signedness and AddChunk reassembly: extraction report

I did not write anything under the repo root. Scratch scripts and dumps are in `C:\Windows\TEMP\claude\...\scratchpad\extract\M2-gap\`: `dr.py` (range disassembler), `scan.py` (xref scan), `unpack.txt`, `handler.txt`, `handler2.txt`, `addchunk.txt`, `pack.txt`, `viz.txt`, `dec_rgb.txt`, `dec_gray.txt` and `xrefs.txt`.

**Short answer**
- **imageEncoding is unsigned (u8).** The engine loads it with ldrb, compares it unsigned, and passes it to `EnumToString(ImageEncoding)` zero-extended. The json `i8` is contradicted; Unity's `byte` agrees.
- **resolution is signed (i8).** The json and Unity agree.
- **chunkDebug and status are UNKNOWN from the engine.** No consumer in the engine or in Unity ever reads either field for behaviour; the engine only copies, re-serialises or equality-compares them. The only shipped type declaration is Unity's generated CLAD twin, which says i32 and i16. The json's u32/u16 rests only on pycozmo.

#### 1. Consumers found (full .text xref scan, `xrefs.txt`)

The only behavioural consumer is `RobotToEngineImplMessaging::HandleImageChunk` at 0x535A64. From there the chunk goes to:
- `EncodedImage::AddChunk` (0x4F1CE0), on robot+0x394;
- `MessageEngineToGame::Set_ImageChunk`, i.e. forwarding to the game;
- `VizManager::SendImageChunk` (0x6C066E).

Everything else is CLAD plumbing: Unpack, Pack, `operator==`, and the Create/Set helpers of RobotToEngine, MessageEngineToGame, MessageGameToEngine and MessageViz.

In Unity, the only consumer is `ImageReceiver.ProcessImageChunk` (`unity/scripts/csharp/ImageReceiver.cs:84-98`). It reads chunkId, imageEncoding, data, resolution and imageChunkCount. It never reads chunkDebug or status.

Unpack at 0x7C60B4 is a series of raw `ReadBytes` calls: 4, 4, 4, 1, 1, 1, 1 and 2 bytes, then a u16-prefixed vector (0x71A8F6: ReadBytes 2, then `ldrh.w r2,[sp,#6]`). It sign-extends nothing, so it says nothing about signedness.
- Wire offsets: ts 0, id 4, debug 8, enc 12, res 13, count 14, chunkId 15, status 16, data count 18, data 20.
- In-memory offsets: +0x0 … +0x10 as listed, and the `std::vector` at +0x14/+0x18.

#### 2. Per-field table

Columns: step | what the original does | citation | record | classification.

- **frameTimeStamp (+0)**
  - Loaded as a 32-bit word. On the last chunk it is stored as the current timestamp and compared unsigned against the previous one: prev ≤ cur passes and equal is allowed; prev > cur raises the TimestampNotIncreasing warning. The handler passes it to `DisplayCameraImage(uint)` and prints it with `%u`.
  - Citation: `0x4F1E28 ldr r3,[r5]`; `0x4F1E2C cmp r1,r3`; `0x4F1E2E bls`; `0x535B20 ldr r1,[r7]`.
  - Record: M3-002 (partial).
  - Signedness: **unsigned**. EXACT_SOURCE.
- **imageId (+4)**
  - 32-bit load, used only for equality: against EncodedImage+0x1C in AddChunk, and against robot+0x344 in the forwarding gate.
  - Citation: `0x4F1D38 ldr r0,[r5,#4]`; `0x4F1D3C cmp r0,r1; bne`; `0x535AB8 ldr r2,[r7,#4]; cmp; beq`.
  - Record: M3-002.
  - Signedness: not behaviour-changing (equality only). Both definitions say u32. EXACT_SOURCE for the use.
- **chunkDebug (+8)**
  - Never read for behaviour. The only touches are `Pack 0x7C61B4 ldr r0,[r5,#8]` (a re-serialise copy), `operator== 0x7C6276/0x7C6278 ldr` (equality), and the VizManager copy `0x6C067C ldm r1!,{r3-r6}`.
  - Unity `ImageChunk.cs:212` reads it with `ReadInt32` and never uses it.
  - Record: NEW (M2).
  - Signedness: **UNKNOWN from the engine**. A word load plus copy or equality can't show signedness, and nothing downstream uses it. The shipped declaration is i32 (Unity generated CLAD). No behavioural effect anywhere.
- **imageEncoding (+0xC)**
  - Loaded with `0x4F1D7A ldrb r1,[r6,#-3]` (r6 = chunk+0xF) and stored at EncodedImage+0x20.
  - JPEGMinimizedGray (8) with a non-zero first data byte becomes 9: `0x4F1D86 ldrb r2,[r0]` (data[0]), then `cmp r2,#0 / movne r2,#9 / cmp r1,#8 / movne r2,r1`, `0x4F1D8C–0x4F1D9C`.
  - Downstream: `DecodeImageHelper 0x4F218E ldrb.w r0,[r5,#0x20]; subs r1,r0,#1; cmp r1,#8; bhi` is an unsigned range check. It then passes r0 **zero-extended, with no sxtb**, to `EnumToString(ImageEncoding)` at 0x4F225E (the same at 0x4F2886/0x4F292C). Compare `0x4F1E56 sxtb` before `EnumToString(ImageResolution)`.
  - The callee at 0x7C5364 (`cmp r0,#9` with no uxtb, then sxtb for the table index) fits a zero-extended u8 parameter.
  - `IsColor 0x4F2102 ldrb; cmp #8; bhi`: any value above 8, 9..255 included, returns true. The tbb table at 0x4F2110 is [8,6,5,5,5,6,5,5,6]: 0 fails VERIFY, 1/5/8 are false, 2/3/4/6/7 are true.
  - Record: M3-002 (encoding rule), NEW (M2 type).
  - Signedness: **unsigned u8**. EXACT_SOURCE for the loads and compares. The declared-type conclusion is inferred from the AAPCS caller-extension convention, which differs between the two enums in the same binary.
- **resolution (+0xD)**
  - `0x4F1D4C ldrb r0,[r5,#0xd]; cmp r0,#4; bne`. If it is not QVGA, `0x4F1E56 sxtb r0,r0` is followed by `EnumToString(ImageResolution)`, which warns "Expecting QVGA resolution, got %s" (EncodedImage.AddChunk.BadResolution) and returns false. If it is QVGA, width 320 and height 240 are stored at +0x14/+0x18 (`0x4F1D52–0x4F1D58`).
  - Record: M3-002.
  - Signedness: **signed i8** (caller sign-extends to the enum parameter). The behavioural compare is equality with 4. EXACT_SOURCE.
- **imageChunkCount (+0xE)**
  - `0x4F1E0C ldrb r3,[r5,#0xe]; subs r7,r3,#1; cmp r7,r8` sets "last chunk". This is 32-bit arithmetic on a zero-extended byte, so a count of 0 gives -1 and never completes, and a count of 0x80 is 128.
  - `0x4F1E1E uxtb r1,r0; cmp r1,r3`: the u8 received counter is compared against the count.
  - The handler repeats the test: `0x535AE4 ldrb r0,[r7,#0xe]; ldrb r1,[r7,#0xf]; subs r0,#1; cmp`.
  - Record: M3-002.
  - Signedness: **unsigned u8**. The zero-extension changes the result for values ≥ 0x80. EXACT_SOURCE.
- **chunkId (+0xF)**
  - `0x4F1DB2 ldrb r3,[r7]` (expected id, +0x21) and `ldrb r1,[r6]`, then `cmp; beq`.
  - The next expected id is `chunkId+1`, stored as a byte (`0x4F1DFE–0x4F1E00`, so it wraps at 255).
  - New-image valid flag = (chunkId == 0), at `0x4F1D5E ldrb r0,[r6,#0xf]!; cmp r0,#0; moveq`.
  - The handler's forwarding gate uses `0x535AB0 ldrb r1,[r7,#0xf]; cbz`.
  - Record: M3-002.
  - Signedness: **unsigned u8**. EXACT_SOURCE.
- **status (+0x10)**
  - Never read for behaviour. The only touches are `Pack 0x7C6202 ldrh r0,[r5,#0x10]` (copy to strh), `operator== 0x7C629E/0x7C62A0 ldrh` (equality) and `Viz 0x6C0680 ldrh r1,[r1]` (copy).
  - Unity `ImageChunk.cs:217` reads it with `ReadInt16` and never uses it.
  - Record: NEW (M2).
  - Signedness: **UNKNOWN from the engine**. The compiler emits ldrh for a 16-bit copy or equality whether the type is signed or not. The shipped declaration is i16 (Unity). No behavioural effect.
- **data (+0x14 vector; wire u16 count)**
  - The payload size is `end-begin`, checked by `0x4F1CEA ldrd; subs; cmp.w r1,#0x4b0; bls`: anything over 1200 bytes, compared unsigned, raises ChunkTooBig and AddChunk returns false.
  - Data is appended only while the image is valid, via `vector::insert` at 0x4F1EB4–0x4F1EBC.
  - Record: M3-002.
  - EXACT_SOURCE.

#### 3. Reassembly rule (M3 interface)

EncodedImage layout, from the constructor at 0x4F1A7E (reached from `Robot::Robot` at 0x51011A for robot+0x394):

| Offset | Meaning | Initial value |
|---|---|---|
| +0..+8 | data vector | cleared (memclr +0..+0x1B) |
| +0xC | current timestamp | 0 |
| +0x10 | previous timestamp | 0 |
| +0x14 / +0x18 | width / height | 0 |
| +0x1C | imageId | 0xFFFFFFFF |
| +0x20 | encoding | 0 |
| +0x21 | expected chunk id | 0 |
| +0x22 | valid flag | 0 |
| +0x23 | u8 chunks-received counter | 0 |

Rows, as step | what the original does | citation | record | classification:

- **R1 Oversize chunk.**
  - Rejected before any state change and returns false. It does **not** invalidate the image in progress. Because the expected id is not advanced, the next chunk then fails the order check and invalidates the image.
  - Citation: 0x4F1CF0–0x4F1D36 (returns via 0x4F1D34 `movs r6,#0`).
  - M3-002. EXACT_SOURCE.
- **R2 New image id** (the id differs from +0x1C). This covers a new id mid-frame.
  - The id is stored first (`0x4F1D4A str r0,[r4,#0x1c]`) and the resolution is checked next.
  - On QVGA:
    - w/h set to 320×240;
    - valid = (chunkId == 0);
    - expected id = 0;
    - encoding set, as in the imageEncoding row;
    - buffer cleared (`0x4F1D9A–0x4F1DA2`, end = begin);
    - `reserve(0x38400)`;
    - counter = 0 (0x4F1DAE).
  - The partial previous frame is discarded without any warning.
  - Citation: 0x4F1D4A–0x4F1DAE.
  - M3-002 / M3-005. EXACT_SOURCE.
- **R3 New id with a non-QVGA resolution.**
  - Warns BadResolution and returns false (0x4F1E4E–0x4F1E68, then 0x4F1D12). By this point +0x1C already holds the new id, but **nothing else is reset**: valid, expected id, counter, buffer and encoding all keep the previous image's state. Later chunks with that id take the same-image path against that stale state.
  - M3-002 **omits this**. EXACT_SOURCE.
- **R4 Order check, applied to every accepted chunk.**
  - If chunkId ≠ expected id, it warns "Expected chunk %d, got chunk %d" (EncodedImage.AddChunk.ChunkOutOfOrder) and clears valid.
  - Either way, expected id = chunkId + 1 (u8) and the counter is incremented (u8).
  - Citation: 0x4F1DB2–0x4F1E08.
  - M3-002. EXACT_SOURCE.
- **R5 Missing chunk** (a gap in the ids).
  - Handled by R4: the image is invalidated, and data is not appended for any later chunk of that id.
  - At the last chunk, 0x4F1EC2–0x4F1EDE takes the channel-info path "Received last chunk of invalidated image" (EncodedImage.AddChunk.IncompleteImage) and returns false.
  - The image does not recover until a new id arrives whose first chunk is 0.
  - M3-002. EXACT_SOURCE.
- **R6 Duplicate chunk.**
  - The same id repeated is not equal to the expected id, so R4 fires and the whole image is invalidated. There is no dedupe.
  - Citation: 0x4F1DB6.
  - NEW detail. EXACT_SOURCE.
- **R7 Last chunk** (chunkCount − 1 == chunkId).
  - The u8 counter must equal chunkCount. If not, it warns "Got last chunk, expected %d chunks but received %d chunks" (UnexpectedNumberOfChunks) and invalidates.
  - Otherwise prev ts = cur ts and cur ts = chunk ts. **The timestamps update even when the check that follows fails.** If prev > cur (unsigned), it warns TimestampNotIncreasing and invalidates.
  - Citation: 0x4F1E0C–0x4F1EAA.
  - M3-002. EXACT_SOURCE.
- **R8 Complete.**
  - The frame is complete when the image is still valid on the last chunk: the data is appended and AddChunk returns 1.
  - A valid chunk that is not the last is appended and returns 0. Any invalid state returns 0.
  - Citation: 0x4F1E16 (r6 = isLast), 0x4F1EAE–0x4F1EC0, 0x4F1EE0.
  - M3-002. EXACT_SOURCE.
- **R9 Maximum size.**
  - Each chunk is capped at 1200 bytes (R1). There is no check on the total; `reserve(0x38400)` is only a capacity hint. The bound comes from the u8 count: at most 255 × 1200.
  - Citation: 0x4F1CF0, 0x4F1DA6.
  - NEW detail. EXACT_SOURCE.
- **R10 Edge cases.**
  - chunkCount == 0 never completes (0x4F1E12).
  - The first chunk after construction carrying id 0xFFFFFFFF takes the same-image path with valid = 0 (constructor at 0x4F1A8A–0x4F1A8E).
  - NEW. EXACT_SOURCE.

**Handler interface (HandleImageChunk 0x535A64)**
- **H1: time-sync gate.** The handler does nothing unless robot+0x29 != 0 (`0x535A7A ldrb.w r0,[r4,#0x29]; beq` to exit).
  - HandleSyncTimeAck sets +0x29 to 1 (`0x5366AA–0x5366AC`); `Robot::SyncTime` (0x515228) and the constructor (0x50FC4A) clear it.
  - NEW for images (M1 CD19 records the flag). EXACT_SOURCE.
- **H2: forward to the game.** The chunk is forwarded whether or not AddChunk accepted it, but only when all of these hold:
  - an external interface exists;
  - robot+0x340 (sendMode) != 0;
  - the tick counter at +0x128 is ≤ 2;
  - either chunkId == 0 (which latches imageId at robot+0x344), or imageId equals the latched id.
  - Citation: 0x535A9C–0x535AE2.
  - NEW. EXACT_SOURCE.
- **H3: SingleShot.** After forwarding a last chunk, if sendMode == 2 (SingleShot, per Unity `ImageSendMode.cs`) it is set to 0.
  - Citation: 0x535AE4–0x535AF8.
  - NEW. EXACT_SOURCE.
- **H4: viz and vision.**
  - Every chunk goes to `VizManager::SendImageChunk` (0x535B0A).
  - When AddChunk returns 1, `DisplayCameraImage(ts)` is called unless the vision component shows processed images only. Then the per-tick cap applies, and the image goes to `VisionComponent::SetNextImage(robot+0x394)`.
  - Citation: 0x535B0E–0x535B9A.
  - M3-005. EXACT_SOURCE.
- **H5: Unity side.** `ImageReceiver` does not check the image id. It restarts its buffer on chunkId == 0, appends every chunk, and finishes on `chunkId == imageChunkCount-1` (int arithmetic).
  - Citation: `ImageReceiver.cs:86-98`.
  - NEW (app interface). EXACT_SOURCE.

#### 4. Existing records and definitions contradicted by the source

- **`cozmo_robot_protocol.json` `image_encoding: i8`** is contradicted by the ldrb and unsigned compares at 0x4F1D7A and 0x4F218E–0x4F2196, and by the zero-extended argument to `EnumToString(ImageEncoding)` at 0x4F225E. Unity's `ImageEncoding : byte` agrees with the engine.
- **`chunk_debug: u32` and `status: u16`** are contradicted by Unity's generated `ImageChunk.cs:212,217` (`ReadInt32`/`ReadInt16`). The engine neither confirms nor refutes either. The json marks both `uncertain:false` with name_source pycozmo, which is not primary.

#### 5. Records whose evidence is too weak or incomplete

M3-002 is correct in what it states, but it leaves out:
- R3: after a bad resolution the image id is already overwritten and nothing is reset;
- that an oversize chunk leaves the expected id un-advanced;
- duplicate chunks invalidate the image;
- the u8 wrap and count-0 cases;
- timestamps update even when the timestamp check fails, and equal timestamps are accepted;
- the constructor state.

Its claim that the field offsets "match this stack's layout" says nothing about signedness.

#### 6. Open questions for the manager

- chunkDebug and status: should Unity's generated-CLAD types (i32/i16) decide the M2 type? Unity sits at authority tier 2 and its CLAD twin at tier 4. Or should they stay UNKNOWN? In both the engine and Unity, the choice changes no behaviour.
- The imageEncoding conclusion rests on the loads and compares plus the AAPCS extension convention. It is not a declared type in a shipped schema; none exists (no .clad file and no reflection string for `chunkDebug` in the .so or the OBB).
