# Robot protocol status

Generated from `protocol/cozmo_robot_protocol.json` by `tools/gen_protocol_status.py`. That JSON is the
canonical definition; the C# codecs in `../cozmo-stack/src/Cozmo.Protocol/Generated/` are generated from it
by `tools/gen_protocol.py` and are never edited by hand.

Evidence order: (1) official decompiled C# CLAD structs, (2) `libcozmoEngine.so` 3.4.0-1204 `Unpack`/`Size`
(authoritative for widths, order and total size), (3) hardware captures from a firmware-2457 robot,
(4) PyCozmo for names only, never over the native layout.

## Totals

| | count |
|---|---|
| official robot messages | **161** (105 engine->robot, 56 robot->engine) |
| fields defined | 404 |
| messages with a complete byte layout | **159** |
| messages with unresolved fields | 2 |
| nested structs | 7 |
| enums carried over from the official C# | 24 |

### Verification status

| status | count | meaning |
|---|---|---|
| hardware verified | 24 | exercised on the firmware-2457 robot: the robot sent it and our codec re-encoded it byte-identically, or the robot demonstrably acted on it |
| capture verified | 4 | seen on the wire in a real session with a consistent length, but no response ties it to robot behaviour |
| statically verified | 60 | layout matches an official C# CLAD struct field for field, or is empty |
| layout known, semantics uncertain | 71 | widths/order from the engine binary; some field names are guesses |
| unresolved | 2 | one or more fields not attributed; the bytes are preserved in a raw tail |
| capture conflict | 0 | observed bytes disagree with the static layout |

### Where field names come from

| source | fields |
|---|---|
| official decompiled C# | 150 |
| generated placeholder | 146 |
| PyCozmo (widths agreed with native) | 65 |
| engine | 34 |
| hardware capture | 9 |

146 of 404 fields still carry a generated placeholder name; 155 fields are flagged uncertain.
Unknown bytes are never invented: a message whose layout does not add up keeps an explicit `unknownTail`
raw field, and placeholder names are `field0`, `field1`, ... so they cannot be mistaken for official ones.

## By subsystem

### Identity / version / logging (21 messages)

10 hardware verified, 9 layout known, semantics uncertain, 2 statically verified

| tag | dir | CLAD type | size | layout | verification | probe safety |
|---|---|---|---|---|---|---|
| `0x01` | E->R | AdjustTimestamp | 4 | native_only | layout known, semantics uncertain | state_change |
| `0x24` | E->R | GetBodySerialNumber | 0 | empty | statically verified | read_only |
| `0x25` | E->R | GetManufacturingInfo | 0 | empty | hardware verified | read_only |
| `0x4B` | E->R | SyncTime | 8 | native_named | hardware verified | state_change |
| `0x80` | E->R | RequestCrashReports | 4 | native_only | hardware verified | read_only |
| `0x82` | E->R | EnableWiFiTelemetry | 0 | empty | statically verified | state_change |
| `0x89` | E->R | DebugSetRTTO | 2 | native_only | layout known, semantics uncertain | destructive |
| `0xA0` | E->R | SetAppRunID | 16 | native_only | layout known, semantics uncertain | state_change |
| `0xA5` | E->R | BodySerialNumber | 4 | native_only | layout known, semantics uncertain | destructive |
| `0xB0` | R->E | PrintTrace | var | hardware_refined | hardware verified | state_change |
| `0xB1` | R->E | PrintText | var | native_only | layout known, semantics uncertain | state_change |
| `0xB2` | R->E | MainCycleTimeError | 16 | native_only | layout known, semantics uncertain | state_change |
| `0xB7` | R->E | DataDump | var | native_only | layout known, semantics uncertain | state_change |
| `0xBE` | R->E | TimeProfileStat | var | native_only | layout known, semantics uncertain | state_change |
| `0xC2` | R->E | SyncTimeAck | 0 | empty | hardware verified | state_change |
| `0xC9` | R->E | RobotAvailable | 6 | hardware_refined | hardware verified | state_change |
| `0xCF` | R->E | CrashReport | var | native_only | hardware verified | state_change |
| `0xD2` | R->E | FWVersionInfo | 44 | native_only | layout known, semantics uncertain | state_change |
| `0xEC` | R->E | WiFiFlashID | 4 | native_only | hardware verified | state_change |
| `0xED` | R->E | ManufacturingID | 12 | native_named | hardware verified | state_change |
| `0xEE` | R->E | FirmwareVersion | var | hardware_refined | hardware verified | state_change |

### Robot state and sensors (20 messages)

10 statically verified, 7 layout known, semantics uncertain, 3 hardware verified

| tag | dir | CLAD type | size | layout | verification | probe safety |
|---|---|---|---|---|---|---|
| `0x4A` | E->R | IMURequest | 4 | exact | hardware verified | read_only |
| `0x4F` | E->R | CheckLiftLoad | 0 | empty | statically verified | read_only |
| `0x52` | E->R | EnterSleepMode | 0 | empty | statically verified | destructive |
| `0x53` | E->R | PowerState | 17 | exact | statically verified | destructive |
| `0x54` | E->R | SetCliffDetectThreshold | 2 | native_only | layout known, semantics uncertain | state_change |
| `0x60` | E->R | EnableStopOnCliff | 1 | exact | statically verified | state_change |
| `0x63` | E->R | EnableBraceWhenFalling | 1 | native_only | layout known, semantics uncertain | state_change |
| `0xBF` | R->E | IMUDataChunk | 195 | exact | statically verified | state_change |
| `0xC0` | R->E | CliffEvent | 6 | exact | statically verified | state_change |
| `0xC1` | R->E | PotentialCliff | 0 | exact | statically verified | state_change |
| `0xC3` | R->E | RobotPoked | 0 | exact | statically verified | state_change |
| `0xC7` | R->E | IMURawDataChunk | 14 | exact | hardware verified | state_change |
| `0xD4` | R->E | RobotStopped | 1 | native_only | layout known, semantics uncertain | state_change |
| `0xD9` | R->E | RobotErrorReport | 5 | native_only | layout known, semantics uncertain | state_change |
| `0xDA` | R->E | LiftLoad | 1 | native_only | layout known, semantics uncertain | state_change |
| `0xDB` | R->E | BackpackButton | 1 | native_named | layout known, semantics uncertain | state_change |
| `0xDC` | R->E | IMUTemperature | 4 | exact | statically verified | state_change |
| `0xDD` | R->E | FallingStarted | 4 | native_named | layout known, semantics uncertain | state_change |
| `0xDE` | R->E | FallingStopped | 12 | prefix | statically verified | state_change |
| `0xF0` | R->E | RobotState | 91 | exact | hardware verified | state_change |

### Head / lift / wheels (19 messages)

12 statically verified, 4 layout known, semantics uncertain, 2 hardware verified, 1 capture verified

| tag | dir | CLAD type | size | layout | verification | probe safety |
|---|---|---|---|---|---|---|
| `0x32` | E->R | DriveWheels | 16 | exact | statically verified | motion |
| `0x33` | E->R | DriveWheelsCurvature | 10 | native_named | layout known, semantics uncertain | motion |
| `0x34` | E->R | MoveLift | 4 | exact | statically verified | motion |
| `0x35` | E->R | MoveHead | 4 | exact | statically verified | motion |
| `0x36` | E->R | SetLiftHeight | 17 | prefix | statically verified | motion |
| `0x37` | E->R | SetHeadAngle | 17 | prefix | hardware verified | motion |
| `0x38` | E->R | HeadAngleUpdate | 4 | native_only | layout known, semantics uncertain | motion |
| `0x39` | E->R | SetBodyAngle | 20 | hardware_refined | statically verified | motion |
| `0x3A` | E->R | TurnInPlaceAtSpeed | 8 | exact | statically verified | motion |
| `0x3B` | E->R | StopAllMotors | 0 | exact | statically verified | motion |
| `0x46` | E->R | StartControllerTestMode | 13 | exact | statically verified | state_change |
| `0x47` | E->R | ControllerGains | 17 | exact | statically verified | state_change |
| `0x50` | E->R | EnableMotorPower | 2 | native_only | layout known, semantics uncertain | state_change |
| `0x51` | E->R | SetMotionModelParams | 4 | exact | statically verified | state_change |
| `0x58` | E->R | StartMotorCalibration | 2 | native_named | layout known, semantics uncertain | motion |
| `0x59` | E->R | RollActionParams | 20 | exact | statically verified | state_change |
| `0xC4` | R->E | MotorActionAck | 1 | native_named | capture verified | state_change |
| `0xD1` | R->E | MotorCalibration | 3 | exact | hardware verified | state_change |
| `0xD8` | R->E | MotorAutoEnabled | 2 | exact | statically verified | state_change |

### LEDs and display (7 messages)

4 layout known, semantics uncertain, 3 capture verified

| tag | dir | CLAD type | size | layout | verification | probe safety |
|---|---|---|---|---|---|---|
| `0x02` | E->R | BackpackSetLayer | 1 | native_only | layout known, semantics uncertain | safe_visible |
| `0x03` | E->R | BackpackLightsMiddle | 31 | native_only | capture verified | safe_visible |
| `0x0B` | E->R | SetHeadlight | 1 | exact | capture verified | safe_visible |
| `0x11` | E->R | BackpackLightsTurnSignals | 21 | native_only | capture verified | safe_visible |
| `0x97` | E->R | FaceImage | var | native_named | layout known, semantics uncertain | safe_visible |
| `0x98` | E->R | BackpackLights | 10 | native_only | layout known, semantics uncertain | safe_visible |
| `0xA3` | E->R | DisplayNumber | 7 | native_only | layout known, semantics uncertain | safe_visible |

### Camera (8 messages)

4 hardware verified, 4 layout known, semantics uncertain

| tag | dir | CLAD type | size | layout | verification | probe safety |
|---|---|---|---|---|---|---|
| `0x4C` | E->R | ImageRequest | 2 | prefix | hardware verified | read_only |
| `0x55` | E->R | EnableReadToolCodeMode | 9 | native_only | layout known, semantics uncertain | state_change |
| `0x57` | E->R | SetCameraParams | 7 | native_named | layout known, semantics uncertain | state_change |
| `0x5A` | E->R | CameraFOVInfo | 8 | native_only | layout known, semantics uncertain | read_only |
| `0x66` | E->R | EnableColorImages | 1 | exact | hardware verified | state_change |
| `0xC8` | R->E | DefaultCameraParams | 29 | native_only | layout known, semantics uncertain | state_change |
| `0xF2` | R->E | ImageChunk | var | native_named | hardware verified | state_change |
| `0xF4` | R->E | ImageImuData | 17 | exact | hardware verified | state_change |

### Audio (4 messages)

2 layout known, semantics uncertain, 2 statically verified

| tag | dir | CLAD type | size | layout | verification | probe safety |
|---|---|---|---|---|---|---|
| `0x64` | E->R | SetAudioVolume | 2 | native_named | layout known, semantics uncertain | state_change |
| `0x65` | E->R | GenerateTestTone | 0 | empty | statically verified | safe_visible |
| `0x8E` | E->R | AudioSample | 744 | native_named | layout known, semantics uncertain | state_change |
| `0x8F` | E->R | AudioSilence | 0 | empty | statically verified | state_change |

### Animation (17 messages)

8 layout known, semantics uncertain, 7 statically verified, 2 hardware verified

| tag | dir | CLAD type | size | layout | verification | probe safety |
|---|---|---|---|---|---|---|
| `0x8D` | E->R | AbortAnimation | 0 | empty | statically verified | motion |
| `0x91` | E->R | RecordHeading | 0 | empty | statically verified | motion |
| `0x92` | E->R | TurnToRecordedHeading | 13 | native_only | layout known, semantics uncertain | motion |
| `0x93` | E->R | HeadAngle | 3 | hardware_refined | statically verified | motion |
| `0x94` | E->R | LiftHeight | 3 | hardware_refined | statically verified | motion |
| `0x95` | E->R | Event | 1 | native_only | layout known, semantics uncertain | state_change |
| `0x96` | E->R | AnimEventToRTIP | 2 | native_only | layout known, semantics uncertain | state_change |
| `0x99` | E->R | BodyMotion | 4 | hardware_refined | statically verified | motion |
| `0x9A` | E->R | EndOfAnimation | 0 | empty | statically verified | motion |
| `0x9B` | E->R | StartOfAnimation | 1 | native_named | layout known, semantics uncertain | motion |
| `0x9D` | E->R | DisableAnimTracks | 1 | native_only | layout known, semantics uncertain | state_change |
| `0x9E` | E->R | EnableAnimTracks | 1 | native_only | layout known, semantics uncertain | state_change |
| `0x9F` | E->R | InitController | 0 | empty | hardware verified | state_change |
| `0xCA` | R->E | AnimationStarted | 1 | native_named | layout known, semantics uncertain | state_change |
| `0xCB` | R->E | AnimationEnded | 1 | native_named | layout known, semantics uncertain | state_change |
| `0xD5` | R->E | AnimationEvent | 6 | prefix | statically verified | state_change |
| `0xF1` | R->E | AnimationState | 15 | exact | hardware verified | state_change |

### Cubes and BLE (24 messages)

19 statically verified, 3 layout known, semantics uncertain, 2 hardware verified

| tag | dir | CLAD type | size | layout | verification | probe safety |
|---|---|---|---|---|---|---|
| `0x04` | E->R | CubeLights | 40 | exact | statically verified | safe_visible |
| `0x05` | E->R | SetPropSlot | 5 | native_named | layout known, semantics uncertain | state_change |
| `0x07` | E->R | SetBodyRadioMode | 2 | exact | statically verified | destructive |
| `0x08` | E->R | StreamObjectAccel | 5 | exact | statically verified | state_change |
| `0x0A` | E->R | SetAccessoryDiscovery | 1 | exact | hardware verified | state_change |
| `0x0C` | E->R | SetCubeGamma | 1 | native_only | layout known, semantics uncertain | state_change |
| `0x10` | E->R | CubeID | 5 | exact | statically verified | safe_visible |
| `0x12` | E->R | SendDTMCommand | 16 | native_only | layout known, semantics uncertain | destructive |
| `0x26` | E->R | SendData | 17 | exact | statically verified | state_change |
| `0x27` | E->R | Disconnect | 4 | exact | statically verified | state_change |
| `0x4D` | E->R | FlashObjectIDs | 4 | exact | statically verified | safe_visible |
| `0x4E` | E->R | ObjectBeingCarried | 5 | exact | statically verified | state_change |
| `0x62` | E->R | ObjectConnectionStateToRobot | 13 | exact | statically verified | state_change |
| `0x86` | E->R | ConnectionState | 4 | exact | statically verified | state_change |
| `0x87` | E->R | DataReceived | 17 | exact | statically verified | state_change |
| `0xB4` | R->E | ObjectMoved | 21 | exact | statically verified | state_change |
| `0xB5` | R->E | ObjectStoppedMoving | 8 | exact | statically verified | state_change |
| `0xB6` | R->E | ObjectTapped | 12 | exact | statically verified | state_change |
| `0xB9` | R->E | ObjectTappedFiltered | 10 | exact | statically verified | state_change |
| `0xCE` | R->E | ObjectPowerLevel | 9 | exact | statically verified | state_change |
| `0xD0` | R->E | ObjectConnectionState | 13 | exact | statically verified | state_change |
| `0xD7` | R->E | ObjectUpAxisChanged | 9 | exact | statically verified | state_change |
| `0xF3` | R->E | ObjectAvailable | 9 | exact | hardware verified | state_change |
| `0xF5` | R->E | ObjectAccel | 20 | exact | statically verified | state_change |

### Localization and navigation (22 messages)

15 layout known, semantics uncertain, 6 statically verified, 1 hardware verified

| tag | dir | CLAD type | size | layout | verification | probe safety |
|---|---|---|---|---|---|---|
| `0x3C` | E->R | ClearPath | 2 | native_named | layout known, semantics uncertain | state_change |
| `0x3D` | E->R | AppendPathSegmentLine | 28 | native_only | layout known, semantics uncertain | motion |
| `0x3E` | E->R | AppendPathSegmentArc | 32 | native_only | layout known, semantics uncertain | motion |
| `0x3F` | E->R | AppendPathSegmentPointTurn | 29 | native_only | layout known, semantics uncertain | motion |
| `0x40` | E->R | TrimPath | 2 | native_named | layout known, semantics uncertain | motion |
| `0x41` | E->R | ExecutePath | 3 | native_named | layout known, semantics uncertain | motion |
| `0x42` | E->R | DockWithObject | 21 | hardware_refined | statically verified | motion |
| `0x43` | E->R | AbortDocking | 0 | empty | statically verified | motion |
| `0x44` | E->R | PlaceObjectOnGround | 25 | hardware_refined | statically verified | motion |
| `0x45` | E->R | AbsoluteLocalizationUpdate | 24 | native_named | hardware verified | state_change |
| `0x48` | E->R | DockingErrorSignal | 22 | prefix | statically verified | state_change |
| `0x49` | E->R | CarryStateUpdate | 1 | exact | statically verified | state_change |
| `0x61` | E->R | ForceDelocalizeSimulatedRobot | 0 | empty | statically verified | destructive |
| `0xB3` | R->E | GoalPose | 21 | native_only | layout known, semantics uncertain | state_change |
| `0xB8` | R->E | PickAndPlaceResult | 7 | native_only | layout known, semantics uncertain | state_change |
| `0xBA` | R->E | RampTraverseStart | 4 | native_only | layout known, semantics uncertain | state_change |
| `0xBB` | R->E | RampTraverseComplete | 5 | native_only | layout known, semantics uncertain | state_change |
| `0xBC` | R->E | BridgeTraverseStart | 4 | native_only | layout known, semantics uncertain | state_change |
| `0xBD` | R->E | BridgeTraverseComplete | 5 | native_only | layout known, semantics uncertain | state_change |
| `0xC5` | R->E | MovingLiftPostDock | 1 | native_only | layout known, semantics uncertain | state_change |
| `0xC6` | R->E | PathFollowingEvent | 3 | native_named | layout known, semantics uncertain | state_change |
| `0xD3` | R->E | DockingStatus | 5 | native_only | layout known, semantics uncertain | state_change |

### Firmware / update / recovery (11 messages)

8 layout known, semantics uncertain, 2 statically verified, 1 unresolved

| tag | dir | CLAD type | size | layout | verification | probe safety |
|---|---|---|---|---|---|---|
| `0x06` | E->R | KillBodyCode | 0 | exact | statically verified | destructive |
| `0x0D` | E->R | BodyEnterOTA | 0 | empty | statically verified | destructive |
| `0x30` | E->R | EnterRecoveryMode | 1 | native_only | layout known, semantics uncertain | destructive |
| `0xA9` | E->R | ShutdownRobot | 32 | native_only | layout known, semantics uncertain | destructive |
| `0xAA` | E->R | AppConnectConfigString | var | partial | unresolved | destructive |
| `0xAB` | E->R | AppConnectConfigFlags | 17 | native_only | layout known, semantics uncertain | destructive |
| `0xAC` | E->R | AppConnectConfigIPInfo | 13 | native_only | layout known, semantics uncertain | destructive |
| `0xAD` | E->R | AppConnectGetRobotIP | 1 | native_only | layout known, semantics uncertain | destructive |
| `0xAE` | E->R | WiFiOff | 1 | native_named | layout known, semantics uncertain | destructive |
| `0xAF` | E->R | Write | 1026 | native_named | layout known, semantics uncertain | destructive |
| `0xEF` | R->E | Ack | 7 | native_named | layout known, semantics uncertain | state_change |

### Factory / debug / storage (8 messages)

7 layout known, semantics uncertain, 1 unresolved

| tag | dir | CLAD type | size | layout | verification | probe safety |
|---|---|---|---|---|---|---|
| `0x0E` | E->R | ReadBodyStorage | 2 | native_only | layout known, semantics uncertain | destructive |
| `0x0F` | E->R | WriteBodyStorage | var | native_only | layout known, semantics uncertain | destructive |
| `0x81` | E->R | NVCommand | var | native_named | layout known, semantics uncertain | destructive |
| `0xA1` | E->R | TestState | var | partial | unresolved | destructive |
| `0xA2` | E->R | EnterFactoryTestMode | 8 | native_only | layout known, semantics uncertain | destructive |
| `0xA4` | E->R | BodyStorageContents | var | native_only | layout known, semantics uncertain | destructive |
| `0xCD` | R->E | NVOpResult | var | native_named | layout known, semantics uncertain | state_change |
| `0xD6` | R->E | FactoryTestParameter | 4 | native_only | layout known, semantics uncertain | state_change |

## What remains unknown

### Messages with unresolved fields

* **0xA1 TestState** (factory_debug_storage, 49 B). names generated; widths from native Unpack native loop bounds [4, 4, 3, 3] could not be attributed uniquely declared 17 B != official Size() 49 B: unresolved fixed array(s); tail kept as raw
  Fields: field0 u32, field1 u32, field2 u16, field3 u16, field4 u16, field5 u8, field6 u8, field7 u8, unknownTail raw tail
* **0xAA AppConnectConfigString** (firmware_update_recovery, 17 B). names generated; widths from native Unpack native loop bounds [16] could not be attributed uniquely declared 2 B != official Size() 17 B: unresolved fixed array(s); tail kept as raw
  Fields: field0 u8, field1 u8, unknownTail raw tail

### Messages whose names are still guesses

These have the right widths and order (from the engine's own `Unpack`) but no official C# struct to name
them, so some field names are placeholders. Sending them is safe; interpreting the fields is not yet.

| tag | CLAD type | subsystem | fields |
|---|---|---|---|
| `0x01` | AdjustTimestamp | identity_version_logging | field0 u32 |
| `0x02` | BackpackSetLayer | leds_display | field0 u8 |
| `0x03` | BackpackLightsMiddle | leds_display | field0 LightState[3], field1 u8 |
| `0x0C` | SetCubeGamma | cubes_ble | field0 u8 |
| `0x0E` | ReadBodyStorage | factory_debug_storage | field0 u8, field1 u8 |
| `0x0F` | WriteBodyStorage | factory_debug_storage | field0 u8, field1 u8[u8 count] |
| `0x11` | BackpackLightsTurnSignals | leds_display | field0 LightState[2], field1 u8 |
| `0x12` | SendDTMCommand | cubes_ble | field0 u32, field1 u32, field2 u32, field3 u32 |
| `0x30` | EnterRecoveryMode | firmware_update_recovery | field0 u8 |
| `0x38` | HeadAngleUpdate | motors | field0 u32 |
| `0x3D` | AppendPathSegmentLine | localization_navigation | field0 u32, field1 u32, field2 u32, field3 u32, field4 PathSegmentSpeed |
| `0x3E` | AppendPathSegmentArc | localization_navigation | field0 u32, field1 u32, field2 u32, field3 u32, field4 u32, field5 PathSegmentSpeed |
| `0x3F` | AppendPathSegmentPointTurn | localization_navigation | field0 u32, field1 u32, field2 u32, field3 u32, field4 PathSegmentSpeed, field5 u8 |
| `0x50` | EnableMotorPower | motors | field0 u8, field1 u8 |
| `0x54` | SetCliffDetectThreshold | robot_state_sensors | field0 u16 |
| `0x55` | EnableReadToolCodeMode | camera | field0 u32, field1 u32, field2 u8 |
| `0x5A` | CameraFOVInfo | camera | field0 u32, field1 u32 |
| `0x63` | EnableBraceWhenFalling | robot_state_sensors | field0 u8 |
| `0x80` | RequestCrashReports | identity_version_logging | field0 u32 |
| `0x89` | DebugSetRTTO | identity_version_logging | field0 u16 |
| `0x92` | TurnToRecordedHeading | animation | field0 u16, field1 u16, field2 u16, field3 u16, field4 u16, field5 u16, field6 u8 |
| `0x95` | Event | animation | field0 u8 |
| `0x96` | AnimEventToRTIP | animation | field0 u8, field1 u8 |
| `0x98` | BackpackLights | leds_display | field0 u16[5] |
| `0x9D` | DisableAnimTracks | animation | field0 u8 |
| `0x9E` | EnableAnimTracks | animation | field0 u8 |
| `0xA0` | SetAppRunID | identity_version_logging | field0 u32[4] |
| `0xA2` | EnterFactoryTestMode | factory_debug_storage | field0 u32, field1 u32 |
| `0xA3` | DisplayNumber | leds_display | field0 u32, field1 u8, field2 u8, field3 u8 |
| `0xA4` | BodyStorageContents | factory_debug_storage | field0 u8, field1 u8, field2 u8[u8 count] |
| `0xA5` | BodySerialNumber | identity_version_logging | field0 u32 |
| `0xA9` | ShutdownRobot | firmware_update_recovery | field0 u32[8] |
| `0xAB` | AppConnectConfigFlags | firmware_update_recovery | field0 u32, field1 u32, field2 u16, field3 u8, field4 u8, field5 u8, field6 u8, field7 u8, field8 u8, field9 u8 |
| `0xAC` | AppConnectConfigIPInfo | firmware_update_recovery | field0 u32, field1 u32, field2 u32, field3 u8 |
| `0xAD` | AppConnectGetRobotIP | firmware_update_recovery | field0 u8 |
| `0xB1` | PrintText | identity_version_logging | field0 u8, field1 string[u8 count] |
| `0xB2` | MainCycleTimeError | identity_version_logging | field0 u32, field1 u32, field2 u32, field3 u32 |
| `0xB3` | GoalPose | localization_navigation | field0 RobotPose, field1 u8 |
| `0xB7` | DataDump | identity_version_logging | field0 u32, field1 u8[u8 count] |
| `0xB8` | PickAndPlaceResult | localization_navigation | field0 u32, field1 u8, field2 u8, field3 u8 |
| `0xBA` | RampTraverseStart | localization_navigation | field0 u32 |
| `0xBB` | RampTraverseComplete | localization_navigation | field0 u32, field1 u8 |
| `0xBC` | BridgeTraverseStart | localization_navigation | field0 u32 |
| `0xBD` | BridgeTraverseComplete | localization_navigation | field0 u32, field1 u8 |
| `0xBE` | TimeProfileStat | identity_version_logging | field0 u32, field1 u32, field2 u8, field3 string[u8 count] |
| `0xC5` | MovingLiftPostDock | localization_navigation | field0 u8 |
| `0xC8` | DefaultCameraParams | camera | field0 u32, field1 u32, field2 u16, field3 u16, field4 u8[17] |
| `0xCF` | CrashReport | identity_version_logging | field0 u32, field1 u16, field2 u8, field3 u32[u8 count] |
| `0xD2` | FWVersionInfo | identity_version_logging | field0 u32, field1 u32, field2 u32, field3 u8[16], field4 u8[16] |
| `0xD3` | DockingStatus | localization_navigation | field0 u32, field1 u8 |
| `0xD4` | RobotStopped | robot_state_sensors | field0 u8 |
| `0xD6` | FactoryTestParameter | factory_debug_storage | field0 u32 |
| `0xD9` | RobotErrorReport | robot_state_sensors | field0 u32, field1 u8 |
| `0xDA` | LiftLoad | robot_state_sensors | field0 u8 |
| `0xEC` | WiFiFlashID | identity_version_logging | field0 u32 |

### Not yet seen on hardware

Every message below has a static layout but has not been exercised on a robot. The `probe` command covers
the safe ones; motion, state-change and destructive messages need an explicit opt-in or stay out of scope.

| subsystem | not yet hardware verified | of which safe to probe |
|---|---|---|
| Identity / version / logging | 11 | 1 |
| Robot state and sensors | 17 | 1 |
| Head / lift / wheels | 17 | 0 |
| LEDs and display | 7 | 7 |
| Camera | 4 | 1 |
| Audio | 4 | 1 |
| Animation | 15 | 0 |
| Cubes and BLE | 22 | 3 |
| Localization and navigation | 21 | 0 |
| Firmware / update / recovery | 11 | 0 |
| Factory / debug / storage | 8 | 0 |

## Firmware compatibility

The reference engine is Anki 3.4.0-1204, whose shipped firmware is 2381. The robot used for verification
runs **2457** (a 2025 Digital Dream Labs build). Its CLAD hashes differ from 2381:

| | engine->robot hash | robot->engine hash |
|---|---|---|
| firmware 2381 (shipped in this APK) | `9e4a965ace4e09d86997b87ba14235d5` | `a259247f16231db440957215baba12ab` |
| firmware 2457 (robot under test) | `fedb4b12f1b5b45456aec1629cd0b8cc` | `5a7211095fc4961407e96b9126658317` |

Every message exercised so far is **layout-compatible across both**, so the stack does not reject a robot
on a hash mismatch: it records both hashes, reports them, and relies on observed per-message compatibility.
If a future firmware does change a layout, the per-message verification status above is where it will show
up, and the definition file can carry firmware-specific variants at that point.

## Reproducing

```
python tools/build_protocol_definition.py <scratch> .        # rebuild the canonical JSON from all evidence
python tools/gen_protocol.py . ../cozmo-stack                # regenerate the C# codecs and catalog
python tools/gen_protocol_status.py .                        # regenerate this document
cd ../cozmo-stack && dotnet test Cozmo.sln                   # sizes, round trips, capture replay
```
