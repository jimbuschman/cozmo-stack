| Scope | Coverage | Boundary |
|---|---|---|
| Direct production constants/functions in expected arguments | CHECKED | 145 candidates over 151 test/support files, manually separated below from API labels, inputs, test fixtures and higher-layer claims. |
| Boolean bounds / nested default construction / local expected-variable aliases | PARTIAL | Manual searches supplement the direct-expression scan; no compiler-level interprocedural proof for arbitrary helper aliases or runtime-generated assertions. This is not an exhaustive “no other circular assertion” verdict. |
| Engine replacement values for confirmed findings | CHECKED | Native instructions/asset field values below, UNKNOWN for policy-only helpers or uncaptured full image outputs. |

Q7 of `requests/20261006-codex-queue-3.md`, pulled main after Q6. Scope current main only; parked R-FIX3 twin-RNG tests are Q6. Discovery script `20261006-test-expected-scan.py` is explicitly a lexical candidate finder, not a correctness proof. `20261006-Q7-scan.json` records the scanned files and9743 Equal/Same/InRange assertion sites; `20261006-Q7-candidates.json` retains every direct candidate, including exclusions. Broad discovery includes tests for higher layers which reference these components: those are not automatically M3–M5 findings. Current manifest quotes for all104 layer records are in `20261006-M3-M5-test-records.json`. No record is settled.

## Current record quotations for the test claims below
```json
{
  "id": "M3-006",
  "title": "Face canvas 64x128; wire image 128 columns x 64 rows as 32 two-row pairs; 33 ms per stream frame",
  "status": "EXACT_SOURCE",
  "evidence": [
    "B1 Image(64,128) and warpAffine to Size(128,64) (0x00585B3E..0x00585CB2)",
    "B15 from B6, B10, B14: 128 columns x 64 rows, 32 pairs per column",
    "B18 stream time +0x84 += 0x21 = 33 ms per fully sent frame (0x0057CA94..0x0057CA9C)"
  ],
  "test": "M3DeviceTests.M3_006_B1_B15_TheWireImageIs128ColumnsOf64RowsIn32Pairs, M3DeviceTests.M3_013_C15_OneUpdateStreamsFramesUntilTheBudgetStopsTheDrain"
}
```

```json
{
  "id": "M3-008",
  "title": "How the firmware maps pair bits to physical display rows, and the robot playback period",
  "status": "HARDWARE_ONLY",
  "evidence": [
    "engine side: pair bit0 = row 2k, bit1 = row 2k+1 (B10 0x00581A48..0x00581AB0; decoder B14 0x0057FCF0..0x0057FDD8)",
    "firmware images are encrypted (entropy 7.8-8.0 bits/byte) and 2457 is not shipped"
  ],
  "test": null
}
```

```json
{
  "id": "M3-010",
  "title": "encodeMuLaw(float) exactly; no volume scaling; short frames zero-padded",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "C6 encodeMuLaw 0x00597AD8..0x00597B8E, segment table 0x00C5C3F0, 32767.0 at 0x00597C18; NaN warns and gives 0",
    "C5 PopRobotAudioMessage 0x00597DD4..0x00597E4E: zero-pad below 744",
    "C7 no volume scaling (0x00597DFC..0x00597E02)"
  ],
  "test": "M3DeviceTests.M3_010_C6_TheFloatIsClampedScaledAndTruncated, M3DeviceTests.M3_010_C6_SignMagnitudeAndTheLowSegment, M3DeviceTests.M3_010_C5_AShortFrameIsPaddedWith00, M3DeviceTests.M3_010_1i_TheSegmentTableIsTheEngines128Values, M3DeviceTests.M3_010_1b_1d_TheNaNPathLogsAndTheLiteralIs32767_0f"
}
```

```json
{
  "id": "M3-011",
  "title": "22320 Hz, 744 samples per frame",
  "status": "EXACT_SOURCE",
  "evidence": [
    "C3 HijackAudioPlugIn(22320, 744) and SetupHijackAudioPlugInAndRobotAudioBuffers(22320, 744) (0x005942CE..0x005942EA)"
  ],
  "test": "M3DeviceTests.M3_011_C3_22320HzAnd744Samples, DeviceTests.TheSampleRateIsTheEnginesAudioSampleRate"
}
```

```json
{
  "id": "M3-012",
  "title": "The engine send budgets: 14 unplayed audio frames; min(8192 - unplayed bytes, 30000); counters from AnimationState and every send",
  "status": "EXACT_SOURCE",
  "evidence": [
    "C9 UpdateAmountToSend 0x0057C6F6..0x0057C7AC (14 at 0x0057C79E add.w r1,r1,#0xe)",
    "C10 the AnimationState handler writes the played counters (0x00537FD0..0x0053800C)",
    "C11 bytes += EngineToRobot::Size(), frames += 1 for 0x8E/0x8F (0x0057BFB0..0x0057BFCC)",
    "C12 EndOfAnimation counts one frame plus its bytes (0x0057C464..0x0057C496); C13 the ctor zeroes the counters (0x0050FD0A)"
  ],
  "test": "M3DeviceTests.M3_012_C9_TheByteBudget, M3DeviceTests.M3_012_C9_TheAudioBudget, M3DeviceTests.M3_012_C11_C12_WhatEachSendCounts, M3DeviceTests.M3_012_C9_C11_TheByteBudgetBindsSampleFramesAt10, M3DeviceTests.M3_012_C10_ThePlayedCountersComeOnlyFromATimeSyncedAnimationState, M3DeviceTests.M3_012_C14_AFailedSendIsNotCounted"
}
```

```json
{
  "id": "M3-017",
  "title": "Test tones, beeps and sweeps",
  "status": "COMPATIBILITY_POLICY",
  "evidence": [
    "C4: the engine plays audio only through the animation stream (0x0057C016..0x0057C056); test tones have no engine counterpart"
  ],
  "test": "M3DeviceTests.M3_017_C9_APlayNeverSendsPastTheBudget, M3DeviceTests.M3_017_C9_APlayFollowsTheRobotAndKeepsWithinBothBudgets"
}
```

```json
{
  "id": "M4-001",
  "title": "Head angle limits -0.436332..0.776672 rad: command clip in MoveHeadToAngleAction, state-side clamp, -25 deg before calibration",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "MA9 MoveHeadToAngleAction ctor clip with warnings 0x00547F44..0x0054803A",
    "MA22 Robot+0x2FC = -0.436332 in the ctor (0x0051007C..0x00510086)",
    "MA23 RS6 SetHeadAngle 0x0051335E..0x005133E8 (M2 interface)"
  ],
  "test": "M4ControlTests.M4_001_M4_003_MA9_MA11_HeadIsClippedAndCarriesTheAppDefaults, M4ControlTests.M4_001_MA22_RS6_TheHeadAngleIsMinus25UntilCalibratedThenClamped"
}
```

```json
{
  "id": "M4-002",
  "title": "Lift presets 0 LowDock 32 / 1 HighDock 76 / 2 HeightCarry 92 / 3 OutOfFOV -1; clamp to 32..92; a negative height goes to the nearer of 32 and 92",
  "status": "EXACT_SOURCE",
  "evidence": [
    "MA14 preset table 0x00C54684, names 0x00548EBC..0x00548EDC",
    "MA13 MoveLiftToHeightAction::Init clamp 0x0054905E..0x005490F6, negative height 0x005490FA..0x00549140",
    "C5: a negative height picks 32 only when strictly nearer; a tie goes to 92 (0x00549104..0x0054913C)"
  ],
  "test": "M4ControlTests.M4_002_MA13_MA14_LiftClampAndTheNegativeHeightGoesToTheNearerPreset"
}
```

```json
{
  "id": "M4-003",
  "title": "The head and lift API follows the game-message path: caller speed/accel/duration; the original app passes head 10/20 and lift 10/20, duration 0",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "MA10 game SetHeadAngle overwrites +0x90..+0x98 with the message values (0x0052ABE0..0x0052AC24)",
    "MA11 unity/scripts/csharp/Robot.cs:1443-1450 (head 10, 20, 0) and 1638-1646 (lift 10, 20, 0)",
    "MA12 game SetLiftHeight: 32.0 while carrying -> PlaceObjectOnGroundAction (0x0052AC40..0x0052AC9E)",
    "MA9, MA13 action ctor defaults (head 15/20 at 0x00547F14..0x00547F28; lift 10/20 at 0x00548A68..0x00548A78)"
  ],
  "test": "M4ControlTests.M4_001_M4_003_MA9_MA11_HeadIsClippedAndCarriesTheAppDefaults, M4ControlTests.M4_002_MA13_MA14_LiftClampAndTheNegativeHeightGoesToTheNearerPreset, ControlTests.TheHeadAndLiftDefaultsAreTheAppsGamePathValues, M4ControlTests.M4_003_MA_ALockedTrackFailsTheHeadMoveWith03000019, M4ControlTests.M4_003_MA_TheHeadMoveLocksThenUnlocksItsTrack"
}
```

```json
{
  "id": "M5-002",
  "title": "19 procedural-eye parameters and the ProceduralFace default (all 0 except EyeScaleX/Y 1, face scale 1, no distorter); SetFromFlatBuf rules",
  "status": "EXACT_SOURCE",
  "evidence": [
    "C6 SetFromFlatBuf 0x005838D0..0x00583AAC; SetFacePosition 0x00583B20..0x00583BF8",
    "gap3 K4 ProceduralFace() 0x00583660..0x005836A0 (table 0x00C5A970 = {2,3})"
  ],
  "test": "M5AnimationTests.M5_002_K4_TheDefaultFace, M5AnimationTests.M5_002_C6_SetFromFlatBufRules, M5AnimationTests.M5_002_C2_TheCentreIsClampedBeforeTheScaleIsSet"
}
```

```json
{
  "id": "M5-015",
  "title": "Corner radii rx = roundf(p*15), ry = roundf(p*20) into four ellipse2Poly corners; below 1 a point",
  "status": "EXACT_SOURCE",
  "evidence": [
    "gap1 D1 0x0058520E..0x005853C4"
  ],
  "test": "M5AnimationTests.M5_015_D1_ARadiusBelowOneIsTheCornerPoint, ProceduralFaceRendererTests.TheSecondEyeIsTheFirstMirroredSoInnerMeansTheSameOnBoth, ProceduralFaceRendererTests.AZeroRadiusGivesASharpCornerAndANeutralRadiusDoesNot"
}
```

```json
{
  "id": "M5-016",
  "title": "Backpack-lights track: loaded via JSON, colours raw-or-normalised, 0x98 sent every frame while current, LED order Left Front Middle Back Right",
  "status": "EXACT_SOURCE",
  "evidence": [
    "C16 0x005758C8..0x00575CBE",
    "C17 GetColorOptional 0x0084024C..0x0084050C; encoding 0x004FABDC..0x004FAC12",
    "C18 0x004FAC7C..0x004FADB6, 0x004FB0F4..0x004FB11A; 7140 keyframes in 111 shipped clips",
    "R-ANIM pre-extraction part 1 item 6 6.1..6.5: GetByString 0x0083F780, table 0x0105DFE0, GetColorOptional 0x0084024C"
  ],
  "test": "M5AnimationTests.M5_016_C17_C18_TheColourWordsAndTheOrder, M5AnimationTests.M5_016_C18_TheBackpackIsSentEveryFrameWhileCurrent, AnimationGapTests.ALightsKeyframeIsStreamedEveryFrameWhileCurrent, M5AnimationTests.M5_016_6_1_6_3_TheNamedColorsTable, M5AnimationTests.M5_016_6_4_6_5_AStringColourGoesThroughTheTableAndAnUnknownNameIsDefault"
}
```

```json
{
  "id": "M5-027",
  "title": "Idle animations: the idle stack, PushIdle/RemoveIdle, idle InitStream with tag 0xFF, ProceduralLive, the no-animation path",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "A1 0x0057A064..0x0057A0AC",
    "A28 0x0057D03A..0x0057D060, 0x0057D122..0x0057D1E2",
    "A29 0x0057D064..0x0057D448",
    "A30 0x0057B914..0x0057BD6E",
    "R-ANIM pre-extraction part 1 item 9 9a..9c: +0x64 writers 0x00579FC6, 0x0057B926, 0x0057BD66, 0x0057D022, 0x0057D3F6; drain 0x0057D168; HasResponse 0x00670AD0"
  ],
  "test": "M5AnimationTests.M5_027_A1_A30_TheIdleStack, M5AnimationTests.M5_027_9a3_RemoveIdleClearsOnlyOnACountTop, M5AnimationTests.M5_027_A29_AnIdleAnimationInitsWithTag0xFfThenStreams, M5AnimationTests.M5_027_A29_StreamLiveKeyframesGoOutInsideTheUpdate, M5AnimationTests.M5_027_A13_APlayThenStopInOneTickContinuesTheLiveStream, AnimationStreamLifecycleTests, M3DeviceTests.M3_013_A13_A29_A12_WithTheLiveStreamActiveACancelledClipsLeftoversAreDropped, M5AnimationTests.M5_027_B3_AnIdleStreamSetsTheLastStreamTime, M5AnimationTests.M5_027_B1_WithAnIdleOnTopTheLeftoversAreNotFlushed, M5AnimationTests.M5_027_C4_AfterAClipTheLiveIdleIsReinitialisedWithTag255"
}
```

```json
{
  "id": "M5-032",
  "title": "DrawFace: 64x128 canvas, the face transform via shipped cv::warpAffine INTER_NEAREST BORDER_CONSTANT 0, row extent, interlace clearing, scan-line shift",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "E1 0x00585B30..0x00585D9A; E2 0x00585D9C..0x00585E94",
    "gap1 D4, D5",
    "gap3 C1..C6 warpAffine 0x00081850.., invoker 0x0007FC60..0x0008016A, remapNearest 0x00072C38..0x00072D8C (matches stock 3.1.0)",
    "R-ANIM pre-extraction part 1 item 10 10a..10e: GetTransformationMatrix 0x00584FF8, InputArray 0x00585C84, warpAffine imgproc 0x0008193C"
  ],
  "test": "M5AnimationTests.M5_032_E1_E2_D4_TheScanLineParity, M5AnimationTests.M5_032_C1_C6_AFaceTransformGoesThroughWarpAffine, ProceduralFaceRendererTests.AWholeFaceScaleMovesTheEyeCentresAboutTheFaceCentre, M5AnimationTests.M5_032_C2_TheRotatedRowExtentUsesEachEyesCorners, M5AnimationTests.M5_032_10a_10e_TheFaceMatrixIsFloat"
}
```

```json
{
  "id": "M4-017",
  "title": "Backpack lights: priority, Off resent every tick while no source, charging state machine, shared locator, wire conversion, headlight",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "LB1..LB6, LB4a..LB4i as before",
    "E2 0x0063234A..0x00632364 SetHeadlight calls VisionComponent::EnableMode(14, enable) on robot+0x258",
    "E3 VisionMode 14 = LimitedExposure (name table 0x01034230, entry 14 -> 0x00C1DC64)",
    "E4 0x006527AC..0x006527B6 EnableMode tail-calls VisionSystem::SetNextMode (PLT 0x4BA35C)",
    "E5 0x006B2282..0x006B2298 SetNextMode queues (mode,bool) on the deque at VisionSystem+0xB0",
    "E6 0x006B4FB6..0x006B4FE4 VisionSystem::Update drains the deque",
    "E7 0x006B1A76..0x006B1CC6 EnableMode(14) sets/clears mask bit 14 at VisionSystem+0xAC",
    "E8 0x00632368..0x00632380 EngineToRobot(SetHeadlight) then SendMessage reliable, not hot"
  ],
  "test": "M4ControlTests.M4_017_LB1_LB2_LB3_TheOffLightsGoOutEveryTickWhileThereIsNoSource, M4ControlTests.M4_017_LB4_LB4i_LB5_TheChargingStateSharesTheLocatorWithSetBackpack, M4ControlTests.M4_017_LB4g_ChargingOnTheWireFromTheShippedPatterns, ControlTests.BackpackColoursArePackedAndRememberedAsSent"
}
```

```json
{
  "id": "M3-027",
  "title": "NV ProcessRequest READ: the factory/non-factory Length, the reliable send, and the pending-read arm (5 s robot-clock deadline, retry counter 0)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "READ case 0x64503E..0x64507C: factory tag -> Length = _maxFactoryEntrySizeTable[tag]; non-factory -> mov #0x400 at 0x64536A, stored +0xE0 (0x64536E); op 0 -> +0xE4; byte 9 zero",
    "send reliable = 1, hot = 0 (0x645392..0x6453D2); arm 0x6453F8..0x645484: +0x50 = request tag (0x64541E/0x64542A), +0x58 = cb, +0x71 = broadcast, +0x74 = robot+0x2C + 0x1388 (0x64543E), +0x54 = caller vector or a fresh one (0x645448..0x64546E), state 2, +0xF4 = 0"
  ],
  "test": "M3DeviceTests.M3_027_AReadComputesItsLengthFromTheTag"
}
```

```json
{
  "id": "M3-031",
  "title": "NV read retry (7 resends / 8 transmissions, identical resend) and the 5 s timeout (-4, no retry)",
  "status": "IMPLEMENTATION_GAP",
  "evidence": [
    "retry set {-8,-7,-5,-4} (0x6431E6..0x6431FA); ResendLastCommand 0x645C6A..0x645D7A: the counter +0xF4 is 0-based (reset by the send/arm at 0x645484), incremented then compared < +0xF5 = 8 (bhs at 0x645C7C), so 7 resends / 8 transmissions, then ReadOpFailed (0x6431FE..0x643234); the second caller is the write/erase path 0x643194..0x6431A2",
    "timeout state 2: +0x78 set and robot+0x2C > +0x74 -> Update.ReadTimeout, cb(nullptr, 0, -4), SetState(0), no retry (0x64575A..0x6457C0)"
  ],
  "test": "M3DeviceTests.M3_031_ARetryableNegativeResultIsResentThenCompletes, M3DeviceTests.M3_031_TheRobotClockTimeoutDeliversMinusFour, M3DeviceTests.M3_031_TheDeadlineUsesTheSyncedClockAtSend"
}
```

```json
{
  "id": "M3-036",
  "title": "The robot's reply to a factory read with Length = 1, and the non-factory Length = size+16 re-request contract",
  "status": "HARDWARE_ONLY",
  "evidence": [
    "pass 1 step 17: what the robot does with Length = 1 is unknown; the Length = 1024 reply looked like tag offsets from StartTag with 56 bytes at index 0, an inference only",
    "pass 4 open q5: whether the Length = size+16 re-request is answered from index 1 / byte size+16 is firmware behaviour"
  ],
  "test": "M3DeviceTests.M3_036_Hardware"
}
```

## Confirmed implementation-derived expectations

All paths below begin `cozmo-stack/tests/Cozmo.Protocol.Tests/`. A group lists **each assertion line**, not merely a representative call. A circular expected expression may coexist with independent assertions elsewhere; those other tests are not dismissed.

| File:line | Expected source in current test | Engine's independent value / citation |
|---|---|---|
| DeviceTests.cs:72,73,74,75,77 | AnkiMuLaw.Encode(short) used as oracle for Encode(float). Both share the segment table/formula. | `0x00597AD8..0x00597B82`, table0xC5C3F0: float clamp±1, binary32 multiply0x46FFFE00 (32767), signed truncation, no bias/complement. -1/-5 giveFF; +1/+5 give7F. Loop77 requires independently enumerated native float-bit→byte pairs, not the short overload. |
| DeviceTests.cs:146,147,148 | AnkiMuLaw.Encode supplies the last frame's data/padding expectations. | Same encoder: short1000→2F, zero→00; fixed byte literals independently follow native lookup/shift. Padding policy of offline ToFrames is a separate API boundary, not established merely by using Encode twice. |
| DeviceTests.cs:144,162,163,171,205,237 | CozmoAudio frame/rate/interval constants provide expected lengths/rate. | M3-011: sample rate22320=0x5730, sample frame744=0x2E8 (`0x005942B0`, `0x007BC7D8`; RobotAudioBuffer0x5984E8); wire payload AudioSample body744. Half-second at that rate11160. Mathematical interval744/22320 is1/30s, **not** animation timestamp step33ms at0x57CA94..CA9C. Tone/offline API duration is not native scheduling evidence. |
| AnimationGapTests.cs:185,189,212,224 | CozmoAudio.RobotBufferFrames (shared with scheduler) supplies expected paced frame count. | `0x0057C79E` immediate0xE:14, then19 after5 played,17 after3 played. Numeric literals from that gate catch a shared changed budget. |
| M3DeviceTests.cs:828 | ToFrames(pcm).Count used as expected Play send count; Play uses ToFrames. | M3-017 gate0x57C6F0..C7A4,744sample payload. For this helper's600ms synthetic PCM at22320Hz the independent frame arithmetic is ceil(13392/744)=18; this does not certify engine sound rendering/readiness. |
| DeviceTests.cs:344 | RawFrameSize expected from same codec's constant. | Engine face raw fallback0x00592A94..0x00592B88:1024bytes=0x400,128columns×32 paired-row bytes. |
| DeviceTests.cs:436 | FaceBitmapCodec.Encode(image) used to expect bytes sent by the display API, which calls that codec. | Native display encode0x00592974..0x00592B88. Fixed-image expected encoded bytes must be read from native encode / independent capture, not re-encoded by this C#. Exact full byte sequence for this generated image is UNKNOWN in this report. Relative adapter forwarding is useful, but not a codec oracle. |
| DeviceTests.cs:321,323,313,345,387;438 | Expected max payload/inequality and timing bound use production DefaultMaxPayload/MessageOverhead/MinInterval. | The direct CozmoDisplay API is a host policy (Display.cs declares no engine counterpart); M3-008 separately owns firmware physical row mapping/playback period and remains HARDWARE_ONLY: native streamer uses ordered message budget0x57BF60..C010, not this pacing API. Phone/runtime/host policy expected bound has no shipped-engine constant; **UNKNOWN / outside package**, not a recovered native result. Transport frame1406 minus message overhead3 yields host1403, but both must be pinned independently if testing that policy. |
| ControlTests.cs:305,310 | Expected clip limit comes from production CozmoMotion constants. | Head minimum binary32 **0xBEDF66F3**, native0x547F2C..547F4A; lift lower height32mm **0x42000000**, native0x548E04..548E4A. These values must not be read from the very constants that drive clipping. |
| BehaviorFrameworkTests.cs:608 | Expected locked mask from CozmoMotion.LiftTrack. | Native MoveLift ctor0x5489EE required mask2; byte mask head1/lift2/body4. Other named AnimationTrack mask assertions are API-label/relative lock tests; an independent numeric-mask assertion is still required to check encoding. |
| ControlTests.cs:374,375,376,390,391; M4ControlTests.cs:1128 | Expected LightState words computed by production LedColor.Packed, same packer as actual lights. | Native0x004FABDC..0x004FAC12 (RGB555+alpha): redFC00,green83E0,blue801F, off(black alpha255)8000. Gamma/state selection remains a separate test concern. Tuple identity atControlTests377 is an input-retention assertion, not a recovered numeric value. |
| FaceTests.cs:707,708,713,736,743,755;FreeplayTests.cs:1714,1770,1961,2240;VisionTests.cs:767;M3DeviceTests.cs:1428,1699,1760,1883 | Expected NV opcode/nonfactory length/scheduled result from production constants. | `ProcessRequest0x0064504C..0x00645484`: READ0,WRITE1,ERASE2, nonfactory initial length0x400 (not factory tag0x80000001's length1), scheduled request return1. Record path/field values differ by operation; higher-layer callers do not establish lower-layer encoder correctness by reading its constants. |
| M3DeviceTests.cs:2039,2044,2048 | Retry loop bound and expected count both MaxReadResends. | `ResendLastCommand0x00645C6A..0x00645D7A` counter0,7 resends then failure; total8 attempts. At2044 independent expected is afterRead+7; keeping the test loop production-derived can skip precisely the changed erroneous branch. |
| EngineAppLayerTests.cs:1499 | Expected calibration length CameraSettings.CalibrationBytes derives from stack serializer's WireSize. | CameraCalibration factory size0x38=56, comparison VisionComponent callback0x0065AB68..0x0065ABB8 (wire size rounded to4-byte alignment). Factory reader is M3; unpack payload is M11. Pin56 for lower-layer buffer contract. |
| DeviceTests.cs:643,648,774 | Expected minimized encoding tags use production MiniJpeg constants. | EngineImageEncoding shipped generated enum: minimized gray8,color9; dispatch0x004F0684..0x004F0712. Pin raw tags if verifying numeric message encoding. |
| AnimationAssetTests.cs:76 | Pixel vector length from FaceBitmap width/height. | Shipped faceAnimation128×32=4096bytes per unpacked bitmap. Engine face-animation receiver M5-013 and display conversion0x592974; independently assert4096 or decode asset metadata. |
| AnimationAssetTests.cs:182,183 | Eye.ParamCount from same production eye layout. | Constructor/flatbuf loops0x00583660..0x00583808:19 binary32 parameters per eye. |
| M5AnimationTests.cs:1299 | Expected removed-layer/default face constructed by new production ProceduralFacePose. Shared erroneous default survives. | Native ctor0x00583660..0x005836A0;19 words, index2/3=0x3F800000, every other eye word0x00000000; table0xC5A970={2,3}. Use these words, not another instance. |
| KeepAliveTests.cs:89;M5AnimationTests.cs:1179,1201;EngineAppLayerTests.cs:1928 | IdleCount/DefaultAnimLock/LiveAnimationTag expected from scheduler constants that create same state. | Constructor0x00579F78..0x0057A0AC Count0x23F/`default_anim_lock`; idle InitStream tagFF at0x0057D366..D37A. Fixed source-derived values test a shared constant change. |
| M5AnimationTests.cs:2073 | NamedColors.Default expected for unknown case-sensitive string, same map fallback field. | NamedColors init/table0x0083F780:DEFAULT=0xFFCC00FF; lowercase`red` misses. |
| M5AnimationTests.cs:2089,2090,2091,2092,2093 | BackpackColor.Encode called on expected and actual loader path. | Native0x004FABDC..0x004FAC12: respectively801F,83E0,FF20,FC00,FFFF. Flags/alpha and LED reorder must be checked together; read values directly. |
| ProceduralFaceRendererTests.cs:56,57,58,59 | Expected canvas/center computed from production bitmap/canvas constants. | Canvas128×64; face center64/32 binary32 **0x42800000/0x42000000**, renderer0x0058502E..0x005859DC / face-clamp0x00583B20..3BF8. |
| ProceduralFaceRendererTests.cs:110,111,125,199 | Expected eye centers from same production renderer constants. | Nominal centers table0x005859DC={96,32}; y32. Binary32 x32=0x42000000,x96=0x42C00000; bitmap center y16=0x41800000. At199 the rotation remains centered on source32, not a production-defined center. |

## Exclusions and remaining uncertainty

Pure enum **API labels** (AnimationEndReason.Completed, MotionResult.Failed, etc.) do not execute the algorithm under test to calculate expected numeric data; they are contract assertions. Their mapping to engine ordinals/results needs separate independent wire/status assertions, but they are not counted as same-algorithm output oracles here. `new EyeBox(16,1,32,42)` supplies independent input numbers; it is not circular just because the data type lives in production. Assert.Same of a deliberately retained input/reference and expected values computed solely from controlled test input are likewise not automatically circular.

The scan also found M1 connection-response/disconnect labels, M6 Wwise song frame arithmetic and M7 desired-distortion twin generators. Their layers are outside Q7; the M7 twin issue was reported in Q4 and the parked chooser twin in Q6. Test-local literal arrays and checked-in emulator/asset fixtures are not production-computed merely because they have a variable named expected. Conversely, comments calling a helper an “oracle” are not evidence of independence.

**Specific remaining work (PARTIAL):** an exhaustive alias/helper/compiled-assertion provenance analysis, beyond the direct expressions and manual sites above, has not been established. The checked candidate list must not be advertised as all possible circular tests. Full-image encode fixture bytes and native sampled RNG fixtures are UNKNOWN here. No passing suite, source status or apparent output upgrades those gaps. No production tests were changed or run in this research task.
