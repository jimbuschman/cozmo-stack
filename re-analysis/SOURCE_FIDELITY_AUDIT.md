# Source Fidelity Audit — 2026-09-19

Reconciled later the same day after both hardware retests passed: counts corrected, retests and the cube
observation recorded, and three further questions settled from the binary (§10). Where this document and
the code disagree, the code and its tests are current.

Scope: every hand-written production file in `cozmo-stack/src` through M8 (about 12,500 lines across
`Cozmo.Protocol` hand-written helpers, `Cozmo.Transport`, `Cozmo.Robot` and the `Cozmo.Conformance`
acceptance harness). Generated code (`Generated/*.g.cs`, `AnimationTrigger.g.cs`, `ReactionTrigger.g.cs`)
was audited through the definition it is generated from, not line by line.

Authority order, as instructed: `libcozmoEngine.so` 3.4.0-1204 and its symbols; the decompiled Unity C#;
the shipped OBB assets and configuration; captures and hardware observation; PyCozmo only as corroboration.
Where the binary answers a question, the binary was read. Addresses below are in
`resources/lib/armeabi-v7a/libcozmoEngine.so`; every function named was disassembled with
`re-analysis/tools/disarm.py` or its address-capable variant (session scratch, not tracked) and the
relevant instructions are quoted in the code comments and tests that rest on them.

## Provenance classes

| class | meaning |
| --- | --- |
| NATIVE | read from `libcozmoEngine.so` (disassembly or .rodata) |
| UNITY | read from the decompiled Unity C# |
| ASSET | read from the shipped OBB (assets, config, schema) |
| WIRE | established from robot behaviour on the wire (captures, hardware runs) |
| CORROBORATED | from a secondary source (PyCozmo, public format description, Anki's Vector release) and checked against assets or hardware |
| INFERRED | derived from names, structure or plausibility; the original could in principle answer but was not read, or could not be settled |
| LOCAL_POLICY | a deliberate choice of this stack, labelled as such in the code |
| INVENTED | chosen because it looked right, presented or usable as if native |

## Provenance counts

**120 rows** in the classification table (§5), one per implementation decision or tightly bound group of
decisions, counted after the sweep's fixes and the reconciliation's additions. The first version of this
document said 136 and printed per-class numbers summing to 134; neither was the row count. The numbers below
are computed from the table by a script and sum to the row count.

| class | rows | note |
| --- | ---: | --- |
| NATIVE | 53 | 14 rows marked ✱ arrived here in the sweep (D1–D5, D7–D9 and the four confirmations: head and lift limits, JPEG headers, group weighting, body stop message); 3 marked ✱✱ added by the reconciliation |
| UNITY | 3 |  |
| ASSET | 6 | 2 rows marked ✱ (neutral face; reaction-trigger map) |
| WIRE | 8 |  |
| CORROBORATED | 11 |  |
| INFERRED | 13 | includes the one row still labelled deferred (`faceAnimations`, lift 0 mm) |
| LOCAL_POLICY | 24 | includes the one row labelled deferred (cooldown and head-angle gate) and the reconciliation's frames-per-tick row |
| INVENTED | 2 | `Expressions` (a labelled local convenience) and the M8 behaviour scores (configs carry none) |

A row that names two classes (`WIRE / NATIVE`, `CORROBORATED / LOCAL_POLICY`) is counted under the first; a
row qualified in parentheses (`NATIVE (names)`, `NATIVE, not reproduced`, `LOCAL_POLICY (deferred)`,
`deferred (INFERRED)`) is counted under its class. The open questions behind the INFERRED rows are listed in
§2, which also names two areas (idle body shuffle, colour camera frames) that are not separate rows.

## 1. Confirmed discrepancies

Each of these is a place where the code differed from the recovered original and the original could be
read. All nine are fixed in this sweep, each with a regression that fails against the old behaviour
(§6); three more found by the reconciliation are D10–D12 in §10. The frozen milestones touched are in §7.

| # | where | what the code did | what the original does | evidence |
| --- | --- | --- | --- | --- |
| D1 | `CozmoAudio.SampleRate` (M3) | 22050 Hz, taken from PyCozmo's WAV loader and marked as an assumption | **22320 Hz**. `AnimConstants::AUDIO_SAMPLE_RATE = 22320` and `AUDIO_SAMPLE_SIZE = 744` (EnumToString at 0x007BC7D8 compares 0x5730 and 0x2E8); `CozmoAudioController::SetupPlugins` at 0x005942B0 passes 22320 to the audio plugin twice. 744 samples is then exactly one 30 Hz frame. The 22050 in the binary belongs to text-to-speech only | tone pitch and all resampling were 1.2 % off |
| D2 | `RobotAnimationSink.Head/Lift` (M5) | head and lift keyframes sent as `SetHeadAngle` / `SetLiftHeight` motor commands with invented speed 10/10 and 3/20 | **animation keyframes** `animHeadAngle` (0x93) `{u16 durationTime_ms, i8 angle_deg}` and `animLiftHeight` (0x94) `{u16, u8 height_mm}`: `HeadAngleKeyFrame::GetStreamMessage` 0x004F8C08, `LiftHeightKeyFrame::GetStreamMessage` 0x004F8F80. Protocol definition updated and regenerated; two messages move from layout-known to statically verified | motors moved on hardware, so this survived acceptance |
| D3 | `AnimationScheduler.Dispatch` (M5) | keyframe variability parsed and ignored | `GetStreamMessage` applies `RandIntInRange(value - var, value + var)` at stream time when variability is non-zero | |
| D4 | `AnimationScheduler.StartAudio` (M5/M6) | first alternative that decodes is played, every time | one alternative chosen by probability: `RobotAudioKeyFrame::GetAudioRefIndex(true)` 0x004F9AEC walks cumulative probabilities against `RandDbl(1.0)`; `SetMembersFromFlatBuf` 0x004F9E54 gives `1/n` each when the clip carries no usable probabilities. A local fallback to the other alternatives when the chosen one cannot be decoded is kept and labelled | |
| D5 | `ProceduralFacePose.BlendTo`, `Eye` (M5) | every parameter blended linearly; no clipping | `ProceduralFace::Interpolate` 0x00584290 blends the eye angle and face angle as directions (cos/sin, `atan2f`), floors negative face scales at 0, and clips every eye parameter through `ProceduralFace::Clip` 0x005847A8 whose 16-entry table at 0x00C5A97C bounds lid angles to ±45°, scales to ≥ 0 and radii/lids to 0..1. `SetEyeArrayHelper` 0x00583790 clips on asset load too | |
| D6 | `ProceduralFaceRenderer.Neutral()`, `CozmoFace.Current` (M5/M7) | a guessed resting face: nominal 30×40 box, radii 0.5, marked "ours" | the engine loads its resting face from the animation group mapped to `AnimationTrigger::NeutralFace` (`AnimationStreamer::AnimationStreamer` 0x00579F78, installed via `ProceduralFace::SetResetData` 0x00583550): `ag_neutral_face` → `anim_neutral_eyes_01`. Eyes 1.21× wider, 0.905× shorter, pulled 9–10 px inward, radii 0.5. Now `ProceduralFacePose.ShippedNeutral()`; the nominal box is kept as `Nominal()` for measuring the renderer | the radii guess was right, the rest was not; M7's "resting face reads correctly" was judged against the guess |
| D7 | `IdleBehavior.Blink` (M7) | upper lids shut for 100 ms | a fixed seven-frame squash: `ProceduralFaceDrawer::GetNextBlinkFrame` 0x00585F18 steps through the table at 0x00C5AAD8 — (EyeScaleX×, EyeScaleY×, ms) = (1.05, 0.85, 33) (1.2, 0.6, 33) (2.5, 0.1, 33) (5.0, 0.05, 33) (2.0, 0.15, 33) (1.2, 0.7, 33) (1.0, 0.9, 100), then restores; keyframes at the cumulative times (`FaceLayerManager::GenerateBlink` 0x0058D2AC); combined onto the base by multiplying scales (`ProceduralFace::Combine` 0x005846A8). Lids are never touched | |
| D8 | `IdleBehavior.Dart` (M7) | EyeCenterX shifted horizontally only; both scales of the eye *away* from the dart grown by 0.1 and clamped to 0.92..1.08 | `FaceLayerManager::GenerateEyeShift` 0x0058D100 draws x **and** y in ±EyeDartMaxDistance and calls `ProceduralFace::LookAt` 0x00584158 with xMax = yMax = 5: the **whole face** moves by (x, y) (`SetFacePosition`); only EyeScaleY changes, by a vertical factor 0.85..1.1 (down..up) times (1 ± 0.1·min(1, \|x\|/5)) with the eye on the side looked **towards** larger; looking down converges the eyes by up to 2 px. `EyeDartMinScale/MaxScale` are not read on this path | |
| D9 | `ReactionTable`, `ReactiveBehavior` (M7) | `RobotFalling` → `AnimationTrigger.ReactToFalling`, fired when the falling flag rose; the whole trigger→animation link declared unrecoverable and inferred from names | the OBB ships `config/engine/behaviorSystem/reactionTrigger_behavior_map.json` (loaded by `RobotDataLoader::LoadReactionTriggerMap` 0x00520BC8): `RobotFalling` → behaviour **`ReactToImpact`**; `BehaviorReactToImpact::TransitionToPlayingAnim` 0x00606348 plays **`ReactToImpact`** (0x1A0) only after `FallingStopped` with impact intensity > 1000 (`AlwaysHandle` 0x00606408) and up to 5 s for post-fall recalibration. The other three entries (`ReactToCliff` 0x19D, `ReactToPickup` 0x1A9, `PlacedOnCharger` 0x189) are confirmed from the behaviour classes, which are exported after all. `ReactionEvidence.NameCorrespondence` no longer applies to any default entry | falling was never exercised on hardware |

Two claims in the existing write-ups were also wrong and are corrected in place:

* `BEHAVIOR_LAYER.md` said the `BehaviorReactToX` classes "are not exported" and the reaction map was
  unrecoverable. The classes' constructors, `InitInternal`, `UpdateInternal` and transition functions are
  all dynamic symbols (36 for the four classes), and the map is a shipped JSON file.
* `CozmoAudio` said 22050 Hz "does not correspond" to the observed 28.6 ms drain. The rate is a fact about
  the engine's stream; the drain rate is a separate measurement of the robot and stands.

## 2. Unresolved inferred areas

Named rather than filled in. Each says what would settle it.

| area | current reading | why unresolved | what would settle it |
| --- | --- | --- | --- |
| Eye-dart lifecycle (M7) | snap to the shifted pose, hold for the drawn 50–200 ms duration, return to base | `GenerateEyeShift` puts the duration into the keyframe's trigger-time slot and `AddToPersistentLayer` 0x0058EAA0 schedules it `duration + 33` ms after the previous keyframe, so the shift **ramps** in; but `ITrackLayerManager::ApplyLayersToFrame` 0x0058E644 trims a finished persistent layer to its last keyframe and resets its stream time, and read statically that makes the held pose drop out until re-triggered, which contradicts what Cozmos visibly do. Two readings (ramp-then-hold, or snap-then-hold) remain | a face-frame capture of the stock app idling, or a runtime trace of `ApplyLayersToFrame` |
| Scanline parity (M5) | parity 0 kept; encoder writes `dd = 01` | `CompressRLE` 0x00581904 shows each robot pixel is a *pair* of canvas rows and `dd` is those two rows' bits, so the toggle **is** transmitted; whether the firmware uses the bit position to pick a physical OLED row or lights the pixel for either bit (PyCozmo's reading, hardware-consistent) is not established | a stock-app capture, or sending the same shape with `dd = 01` and `10` and looking at the panel |
| Corner-radius parameter assignment and polygon fill rule (M5) | as recorded in `PROCEDURAL_FACE.md` | unchanged | finer trace of `DrawEye` |
| Idle timer tick (M7) | timers in wall-clock ms | the engine decrements each keep-alive countdown by 60 per `Update` call (`UpdateLiveAnimation` 0x0057D5F8, `KeepFaceAlive` 0x0058D374), consistent with a 60 ms engine tick but not confirmed | read the tick period from `CozmoEngine::Update` 0x004ED4D4 |
| Idle head and lift on the motion API (M7) | `SetHeadAngle(current ± 6°)`, `SetLiftHeight(35 ± 8)` with durations | the engine adds `HeadAngleKeyFrame(currentDeg, variability 6, duration)` and `LiftHeightKeyFrame(35, 8, duration)` to its **live animation** and streams them as 0x93/0x94; same intent, different messages. Not changed because our idle is not an animation stream | build idle on the scheduler as a live clip |
| Idle body shuffle (M7) | decided, not driven | now recovered: speed uniform in ±10 mm/s, duration 250–1500 ms, straight (0x7FFF) with probability `BodyMovementStraightFraction` else turn-in-place (radius 0) with a 33 ms "LiveIdleTurn" eye shift of x = sign(speed)·rand(0..21), y = rand(−10..10) | hardware session with the robot watched; the numbers are in `UpdateLiveAnimation` 0x0057D6CC onward |
| Audio keyframe volume (M5/M6) | linear PCM gain | the engine passes the keyframe volume to Wwise; how Wwise maps it (RTPC curve) was not traced | trace `RobotAudioClient` volume handling |
| Wwise container semantics (M6) | every Sound under the Play target is an alternative; first decodable | Wwise random/sequence containers carry weights and play modes in their own payloads, which are not parsed; the keyframe-level probability (D4) is now honoured, the container-level one is not | parse type-5 container playlists (M9 territory) |
| Head/lift motor command defaults (M4) | speed 10 rad/s, accel 10; lift 3, 20 | PyCozmo's defaults; the engine's `MoveHeadToAngleAction` defaults were not read | disassemble the action constructors |
| `AudioReliable = true` (M3) | audio frames reliable | based on "the engine sends everything reliable"; `SendBufferedMessages` was not checked for the audio path | read `AnimationStreamer::SendBufferedMessages` |
| Blank face `{0x3F, 0x3F}` (M3) | two skip-64-column commands | plausible and decodes to blank; not read from the engine | `CompressRLE` on an empty image gives `0x3F 0x3F` by construction of the skip run, so this is very likely right, but it was not executed |
| Mood clamp ±1, decay-graph edges (M7) | clamp to [-1, 1], flat outside nodes | not read from `MoodManager` | disassemble `MoodManager::UpdateEmotions` |
| M8 scoring | score × repetition penalty, highest wins | structure from exported names; the engine's per-behaviour scoring configs are not parsed | read `IBehavior::ReadFromScoredJson` 0x005BC489 |
| Colour camera frames (M3) | half-width three-component JPEG | never exercised; grayscale only on hardware | a colour capture |

## 3. Local-policy areas

Deliberate choices, labelled in the code, kept:

* Transport shell: Windows 1 ms timer, socket error handling, three-thread event dispatch with handler
  isolation, 5 s connect timeout, `MaxFramePayloadBytes = 1037` (robot-measured by PyCozmo; the engine
  builds up to 1406).
* Camera: 15 warm-up frames discarded (measured 11 on one capture); images older than the newest two dropped.
* Display: run-only RLE encoder, 32 rows per column, refusing images above one frame. The engine's encoder
  also uses skip and repeat commands and falls back to a raw 1024-byte column-mask frame above
  `MAX_FACE_FRAME_SIZE` (recorded in `Display.cs`; the raw path has never been seen accepted by the robot).
* Audio: `TargetInFlight = 10` (under the engine's 14-frame budget), counter-driven pacing from
  `AnimationState`, `Busy` window 200 ms, `PrimeFrames`.
* Motion: action ids cycle 1..255; wheel confirmation tolerance 35 % / 5 mm/s; `StopAll` sends both
  `StopAllMotors` and a zero `DriveWheels`; cliff sensors named by index; IMU left in raw units.
* Animation: a late tick fires every missed keyframe in order; the last face is held after the clip's last
  face keyframe; `Finished` sends one zero `DriveWheels`; an oversize face payload is dropped rather than sent
  in parts; the chosen-alternative fallback in D4.
* Wwise: nearest-sample resampling and channel averaging; stereo ADPCM refused. (Both corrected since: M6-004 and M6-003.)
* Behaviour: caller > reaction > idle arbitration; autonomy off by default; a 5 s per-reaction cooldown
  (**the engine has no such cooldown for cliff, pickup or charger**; its per-trigger cooldowns exist only
  where the shipped map gives one, e.g. 60 s for minor frustration — the label in `BehaviorArbiter` is
  changed from "ours" to say so); idle body movement not driven; head and lift idle through the motion API.
* M8: behaviour scores 1.0 / 5.0 where configs carry none; the inventory classifier's stated rules.
* Conformance CLI: PASS/FAIL criteria are the harness's own; the `camera` probe step is labelled ReadOnly
  but sends `EnableColorImages(false)` and `ImageRequest`, and `cubes` (SafeVisible) sends
  `SetAccessoryDiscovery` — both harmless, both StateChange in the catalog. Labels only; not changed.

## 4. Acceptance-evidence quality

M1 and M2 rest on committed artifacts. Every hardware pass from **M3 through M7** rests on operator report:
`ACCEPTANCE.md` says the per-run JSON records "are not yet in the repository". Specifically:

| milestone | hardware pass | evidence in the repository |
| --- | --- | --- |
| M1 transport | 2026-09-18 | **committed**: two 20 s frame logs and a console transcript in `captures/`, replayed by `HardwareCaptureTests` on every test run |
| M2 protocol | 2026-09-18 | **committed**: `2026-09-18_fw2457_probe.log` and `probe-results.json` |
| M3 device | camera, face, tone 2026-09-18 | operator report; camera frames are replayed from the probe capture offline, the face codec is checked against 28 Cozmo-produced sequences; no `--acceptance` JSON |
| M4 control | 2026-09-18 | operator report; cubes: discovery **observed** on 2026-09-19 (a real cube appeared during discovery after being tapped), the rest of the cube telemetry not yet exercised |
| M5 animation | 2026-09-18/19 | operator report, including the renderer retest of 2026-09-19 and the post-sweep retest of 2026-09-19 (`anim_bored_01 --wwise`, passed visually) |
| M6 Wwise | 2026-09-18 | operator report for the on-robot half; the offline halves are whole-library tests; the M5 retest above covers the 22320 Hz change |
| M7 behaviour | 2026-09-19 | operator report, including the post-sweep retest of 2026-09-19 (`behavior --seconds 60`, passed visually) |

Per the instruction, verified behaviour is **not** downgraded for the missing JSON; the runs happened and
were reported by the operator. What the sweep adds is the observation that the two faults it found in
already-accepted behaviour — the invented resting face (D6) and the blink (D7) — passed hardware acceptance
precisely because the acceptance criterion was the operator's judgement of "reads correctly" against the
renderer's own output. A shape criterion (the face equals the shipped neutral clip's face) would have caught
D6; nothing short of comparing with a stock Cozmo would have caught D7. The recommendation in §9 follows.

## 5. Classification table

Grouped by layer. Items changed by the sweep are marked ✱; items added or changed by the reconciliation
(§10) are marked ✱✱. A row that names two classes is counted under the first.

**M1/M2 — transport and protocol helpers**

| item | class | basis |
| --- | --- | --- |
| UDP 5551 physical / 5552 simulated | NATIVE | `MessageHandler::AddRobotConnection` |
| `COZ\x03` + `RE\x01` 14-byte header, no CRC | NATIVE | `RobotConnectionManager::Init`, `BuildHeader` |
| message types 1..11, always-unreliable set, container types | NATIVE | `IsMessageTypeAlwaysSentUnreliably`, `HandleSubMessage` |
| sequence ids 1..65534, wrap, in-range test | NATIVE | `NextSequenceId`, `IsSequenceIdInRange` |
| all reliable-transport tunables | NATIVE | `ConfigureReliableTransport` (`dis_robotconn.txt`) |
| send / resend / batching algorithm | NATIVE | `ReliableConnection` disassembly, Vector source identical |
| receive path: in-order only, mixed frames still walked | NATIVE | `ReceiveData` |
| 17-byte ping payload | NATIVE | `SendPing` |
| echoed ping (isReply 0) treated as a reply | WIRE | fw2457 capture |
| multipart `[idx][count]` | NATIVE | `HandleSubMessage` |
| 2 ms update cadence | NATIVE | `ReliableTransport::Update` scheduling |
| `MaxFramePayloadBytes` 1037 | CORROBORATED | PyCozmo robot measurement; engine builds 1406 |
| Windows high-resolution timer, tick loop implementation | LOCAL_POLICY | |
| rx socket error handling; three-thread dispatch; handler isolation | LOCAL_POLICY | |
| 5 s connect timeout | LOCAL_POLICY | |
| handshake order (GetManufacturingInfo, SyncTime, InitController) | CORROBORATED | PyCozmo and engine handshake; hardware |
| `RobotStatusFlag` bits | UNITY | `Anki.Cozmo.RobotStatusFlag` |
| `FirmwareVersion` JSON fields | WIRE | fw2457 capture |
| `LightState.Rgb` 5-5-5 packing | CORROBORATED | PyCozmo; LEDs lit on hardware; bit order not confirmed |
| `SetHeadAngle`/`SetLiftHeight` default speed and accel | INFERRED | PyCozmo defaults; engine action defaults not read |
| `RobotState.liftAngle` is an angle in radians; height = 45 + 66·sin(angle) ✱✱ | NATIVE | `Robot::UpdateFullRobotState` 0x0051291C stores RobotState+0x2C into Robot+0x300; `Robot::GetLiftHeight` 0x00516F64 and `ConvertLiftAngleToLiftHeightMM` 0x00516F9C; inverse `ConvertLiftHeightToLiftAngleRad` 0x005170B0 clamps 32..92. The fw2457 capture's values lie in the radian range (`SourceFidelityTests`) |
| CLAD reader/writer, `string[uint_8/16]` | NATIVE | CLAD |

**M3 — device layer**

| item | class | basis |
| --- | --- | --- |
| chunk reassembly: count only on the last chunk | WIRE | capture |
| payload byte 0 is a colour flag | CORROBORATED | PyCozmo; capture |
| JPEG header tables ✱ | NATIVE | byte-identical to 0x00C48C40 / 0x00C48D84 (`MiniGrayToJpeg`, `MiniColorToJpeg`) |
| size field offset 94 | NATIVE | same tables |
| trailing 0xFF strip, byte re-stuffing, EOI | CORROBORATED | PyCozmo transcription; every captured frame Huffman-decodes |
| 15 warm-up frames | LOCAL_POLICY | one capture measured 11 |
| stale image drop | LOCAL_POLICY | |
| `CameraResolutions` table | UNITY | `Anki.Cozmo.ImageResolution` |
| face 128×32 wire image, 128×64 canvas | NATIVE | `CompressRLE`, `DrawFace` |
| RLE decoder | WIRE | 28 Cozmo-produced sequences |
| RLE encoder runs only, `dd` = 01 / 11 at column end | LOCAL_POLICY | engine uses skip/repeat too (recorded) |
| refuse faces above one frame | LOCAL_POLICY | engine falls back to raw 1024 B |
| 33.3 ms face interval | NATIVE | animation tick |
| mu-law codec | NATIVE | `encodeMuLaw` 0x00597AD8 (`dis_mulaw.txt`) |
| 744 samples per frame | NATIVE | `AnimConstants::AUDIO_SAMPLE_SIZE` |
| 22320 Hz ✱ | NATIVE | `AnimConstants::AUDIO_SAMPLE_RATE`; `SetupPlugins` |
| 30 Hz frame interval | NATIVE | |
| 14-frame robot buffer | NATIVE | `UpdateAmountToSend` 0x0057C6F0 |
| `TargetInFlight` 10, counter-paced feed, `Busy` 200 ms, priming | LOCAL_POLICY | measured drain 28.6 ms is WIRE |
| blank face `{0x3F,0x3F}` | INFERRED | see §2 |
| a face frame and an audio frame every tick | NATIVE | `UpdateStream` |
| `InitController` on connect | WIRE / NATIVE | first hardware run; `InitStream` |
| test tones, beeps, sweeps | LOCAL_POLICY | harness signals |
| audio frames reliable | INFERRED | see §2 |

**M4 — control layer**

| item | class | basis |
| --- | --- | --- |
| motion refused until both motors calibrated | WIRE | robot recalibrates on connect |
| action ids 1..255 | LOCAL_POLICY | |
| head limits −25° / 44.5° ✱ | NATIVE | `Robot::SetHeadAngle` 0x00513358 literals |
| lift limits 32 / 92 mm ✱ | NATIVE | preset table at 0x00C54688 (32, 76, 92) |
| wheel speed ceiling 200 | CORROBORATED | PyCozmo; advisory |
| wheel confirmation tolerance | LOCAL_POLICY | |
| `StopAll` = `StopAllMotors` + zero `DriveWheels` | LOCAL_POLICY | |
| success = `MotorActionAck` with the same id | WIRE | |
| cliff sensors by index; IMU raw; cube battery raw; `IS_CHARGER_OOS` raw | LOCAL_POLICY | uncertainty preserved |
| pick-up / charger / falling transitions from flags, first state as baseline | LOCAL_POLICY | |
| cube tracking from `ObjectAvailable` / `ObjectConnectionState` | WIRE | a real cube appeared in discovery on hardware after being tapped (2026-09-19, operator report); connection state, tap, movement, up-axis and battery telemetry not yet exercised on hardware |

**M5 — animation**

| item | class | basis |
| --- | --- | --- |
| FlatBuffers reader; field indices | ASSET | schema order; confirmed by `SetMembersFromFlatBuf` vtable slots for head, lift, body and audio |
| 19 eye parameters and order | NATIVE | .rodata 0x00C1D399 |
| face blends forward towards the next keyframe | NATIVE | `GetInterpolatedFace` 0x004F99E6 |
| angle blending as directions ✱ | NATIVE | `Interpolate` 0x00584290 |
| eye parameter clip ranges ✱ | NATIVE | table 0x00C5A97C |
| face-position clamp to canvas | NATIVE, not reproduced | `SetFacePosition` 0x00583B20 |
| head/lift keyframes as 0x93/0x94 ✱ | NATIVE | `GetStreamMessage` 0x004F8C08 / 0x004F8F80 |
| head/lift variability ✱ | NATIVE | same |
| body radius encoding | NATIVE | `ProcessRadiusString` 0x004FB588 |
| body stop `{0, 0x7FFF}` at duration end ✱ | NATIVE | `BodyMotionKeyFrame` constructors 0x004FB14C / 0x004FB170 set the stop message and flag; `IsDone` 0x004FBAEC |
| `StartOfAnimation` on the first streamed frame; `EndOfAnimation` + trailing silence | NATIVE | `UpdateStream` |
| tag counter skips 0x00 and 0xFF | NATIVE | `IncrementTagCtr` |
| per-frame track order | NATIVE | `UpdateStream` |
| audio budget 14 frames | NATIVE | `UpdateAmountToSend` |
| audio alternative by probability ✱ | NATIVE | `GetAudioRefIndex` 0x004F9AEC |
| fallback to other alternatives on decode failure | LOCAL_POLICY | |
| keyframe volume as linear gain | INFERRED | see §2 |
| lights track not acted on | NATIVE | `BackpackLightsKeyFrame::SetMembersFromFlatBuf` stub |
| `faceAnimations` not loaded; lift 0 mm passed through | deferred (INFERRED) | unchanged |
| group choice by weight, mood fallback to Default | NATIVE | `AnimationGroup::GetAnimationName` 0x0058A970 |
| cooldown and head-angle gate not enforced | LOCAL_POLICY (deferred) | mechanism now recorded in `AnimationLibrary.cs` |
| renderer geometry, transforms, rounding | NATIVE | `PROCEDURAL_FACE.md` |
| radius-corner assignment, fill rule, parity | INFERRED | §2 |
| resting face ✱ | ASSET | `anim_neutral_eyes_01` via `AnimationTrigger::NeutralFace` |
| `Nominal()` test pose | LOCAL_POLICY | measurement pose, labelled |
| `Expressions` | INVENTED, labelled | local convenience, now built on the shipped neutral |
| timeline is a count of streamed frames × 33 ms, frozen while the robot has no room ✱✱ | NATIVE | `UpdateStream` 0x0057C84C adds 33 to the stream time only after a frame is sent (0x0057CA94..9C); `ShouldProcessAnimationFrame` 0x0057CC6C ends the loop without touching it |
| frames per tick: one per 33 ms wall tick, a late tick made up frame by frame within the budget | LOCAL_POLICY | the engine streams to the audio budget on every update regardless of the clock |
| one streaming animation; a newcomer interrupts it or is refused whatever its tracks ✱✱ | NATIVE | `SetStreamingAnimation` 0x0057B174 ("will not interrupt" / "is interrupting" then `Abort`); concurrency only through `TrackLayerComponent` layers and the idle slot |
| last face held after the last face keyframe; `Finished` zero wheels; oversize face dropped | LOCAL_POLICY | |

**M6 — Wwise**

| item | class | basis |
| --- | --- | --- |
| bank chunk and HIRC object layout, event/action/sound offsets | CORROBORATED | public format; six asset cross-checks all exact |
| parent offsets per type | CORROBORATED | scanned from assets, agreement counts recorded |
| all Sounds under a Play target are alternatives; sorted child order | INFERRED | container playlists not parsed |
| Vorbis rebuild, external codebooks, granule computation | CORROBORATED | ww2ogg port; 2019 of 2019 decode |
| IMA ADPCM mono and stereo | CORROBORATED | self-verifying decode; the stereo block is two mono blocks (§20) |
| resampling and mix-down | LOCAL_POLICY | |
| FNV-1 name hash | CORROBORATED | six bank ids |

**M7 — behaviour**

| item | class | basis |
| --- | --- | --- |
| `AnimationTrigger`, `ReactionTrigger` enums | UNITY | decompiled |
| `AnimationTriggerMap.json` | ASSET | |
| reaction trigger → behaviour ✱ | ASSET | `reactionTrigger_behavior_map.json` |
| behaviour → animation trigger ✱ | NATIVE | `BehaviorReactToX` disassembly |
| impact gating > 1000, 5 s recalibration wait ✱ | NATIVE | `BehaviorReactToImpact` |
| reaction sequencing (cliff stop reaction and back-up, pickup repeat, charger sleep) | NATIVE, recorded not built | `ReactionTable.cs` |
| all 30 idle tunables | NATIVE | `SetDefaultParams` 0x0057DB40 |
| idle yields per track | NATIVE | `UpdateLiveAnimation` |
| blink ✱ | NATIVE | table 0x00C5AAD8 |
| dart geometry ✱ | NATIVE | `LookAt` 0x00584158 |
| dart lifecycle | INFERRED | §2 |
| idle head/lift through the motion API | INFERRED | §2 |
| idle body not driven | LOCAL_POLICY | engine numbers recorded |
| idle timers in ms | INFERRED | §2 |
| motion waits `TimeBeforeWiggleMotions`; face does not | NATIVE | `UpdateLiveAnimation` gates on parameter 2 |
| arbitration order; autonomy off; 5 s reaction cooldown | LOCAL_POLICY | engine has none for these reactions |
| mood axes, events, decay curves | ASSET | |
| mood clamp; decay edges | INFERRED | |
| `SmartDisableReactionsWithLock` during a reaction | NATIVE | called in each `InitInternal` |

**M8 — framework**

| item | class | basis |
| --- | --- | --- |
| lifecycle and `Smart*` names | NATIVE (names) | exports |
| scored selection, penalty multiply | INFERRED | structure from names |
| repetition penalty graph | ASSET | `mood_config.json` |
| scores 1.0 / 5.0 | INVENTED, labelled | configs carry none |
| `PlayAnim` uses the first resolving trigger | INFERRED | |
| scope undo order | LOCAL_POLICY | |
| inventory classifier | LOCAL_POLICY | rules stated per row |

## 6. Fixes made and tests

Nine corrective changes, each with a regression that encodes the recovered value and fails against the
previous code (the old value or behaviour is named in each test's comment):

| fix | files | regression |
| --- | --- | --- |
| D1 sample rate | `Audio.cs`, `WwiseAudioSource.cs` | `DeviceTests.TheSampleRateIsTheEnginesAudioSampleRate` (22050 gave 22050 tone samples; now 22320) |
| D2 head/lift keyframes | `cozmo_robot_protocol.json` (0x93, 0x94 named from `GetStreamMessage`), regenerated `RobotMessages.g.cs`, `MessageCatalog.g.cs`, `PROTOCOL_STATUS.md`; `IAnimationSink`, `RobotAnimationSink`, `AnimationScheduler`, `AnimDump` | `AnimationGapTests.HeadAndLiftKeyframesGoOutAsAnimationKeyframesNotMotorCommands` |
| D3 variability | `AnimationScheduler` | `AnimationGapTests.VariabilityIsAppliedToHeadAndLiftKeyframesAtStreamTime` |
| D4 alternative selection | `AnimationScheduler.ChooseAlternative` | `AnimationGapTests.TheAlternativeIsChosenByProbabilityAsTheEngineDoes` (10 cases), `AClipWithProbabilitiesPlaysTheWeightedAlternativeNotTheFirst`, `AnUnproducibleChosenAlternativeFallsBackToTheOthers`; replaces `TheFirstAlternativeThatCanBeProducedIsUsed`, which asserted the old behaviour |
| D5 interpolation and clipping | `ProceduralFace.cs` (`Eye.Clip`, `Eye.FromAsset`, `BlendAngleDeg`), `AnimationLibrary.cs` | `SourceFidelityTests.AnglesInterpolateAsDirectionsNotAsNumbers`, `EyeParametersAreClippedToTheEnginesRanges` (10 cases), `ANegativeFaceScaleBlendsToZero` |
| D6 resting face | `ProceduralFacePose.ShippedNeutral`, `ProceduralFaceRenderer.Nominal`, `CozmoFace`, `Expressions` | `SourceFidelityTests.TheShippedNeutralFaceMatchesTheNeutralEyesClip` (against the OBB, skipped without it), `TheDefaultFaceIsTheShippedNeutralNotTheNominalBox` |
| D7 blink | `IdleBehavior` (`BlinkFrames`, `BlinkPose`) | `SourceFidelityTests.TheBlinkFollowsTheEnginesFrameTable` (10 instants), `TheBlinkIsLayeredOntoTheBaseFace` |
| D8 dart | `IdleBehavior.DartPose` | `SourceFidelityTests.ADartMovesTheWholeFaceAndShapesTheEyesAsLookAtDoes`, `ADartIsNotClampedToTheUnusedMinMaxScaleTunables`; `IdleFaceTests` re-bounded to the engine's own limits (it had encoded the invented 0.92..1.08 clamp) |
| D9 reactions | `ReactionTable`, `ReactiveBehavior`, `Sensors` (`FallingStopped` event) | `SourceFidelityTests.FallingReactsWithReactToImpactNotReactToFalling`, `TheReactionAnimationTriggersHaveTheOrdinalsTheEnginePasses` (5 cases), `TheImpactReactionFiresOnLandingHarderThanTheThresholdAndNotOnFalling` |

Provenance-only changes (comments and documents, no behaviour): `Motion.cs` head/lift limits marked
native with addresses; `MiniJpeg.cs` headers marked native; `Display.cs` records the engine's encoder;
`AnimationLibrary.cs` records the group selection and cooldown mechanism; `BehaviorArbiter.cs` labels the
reaction cooldown as local; `PROCEDURAL_FACE.md`, `ANIMATION_LAYER.md`, `BEHAVIOR_LAYER.md`,
`DEVICE_LAYER.md`, `CONTROL_LAYER.md`, `ACCEPTANCE.md` and `HANDOFF.md` carry errata sections pointing here.

Test suite after the sweep: **440 passed, 0 failed** (`dotnet test Cozmo.sln`, 3 m 40 s; 406 before the sweep);
after the reconciliation: **444 passed, 0 failed (`dotnet test Cozmo.sln`, 3 m 36 s; 440 after the sweep)** (§10). One pre-existing test,
`DeviceTests.DisplaySendsAnimFaceImageAndPacesAtTheAnimationRate`, failed once under a full parallel run
and passed in isolation; it measures a 33 ms sleep with `DateTime.UtcNow` and is timing-sensitive on
Windows. It is unrelated to the sweep and is noted rather than changed.

## 7. Frozen milestones touched

| milestone | change | kind |
| --- | --- | --- |
| M2 protocol | 0x93 and 0x94 field names and semantics; two messages statically verified | definition edit, regenerated through the sanctioned pipeline; no hand edits to generated code |
| M3 device | sample rate | constant |
| M5 animation | head/lift wire path and variability; audio alternative; interpolation and clipping; resting face | wire behaviour changes (D2, D3); on-robot audible change (D1, D4); visible change (D5, D6) |
| M7 behaviour | blink, dart geometry, falling → impact | visible change; reaction change |

None is an aesthetic rewrite. Every change is a confirmed divergence from a read original with a
regression, as instructed.

## 8. Hardware retests — passed

The sweep changed what the robot receives in two frozen milestones. Both retests were run on 2026-09-19 and
**passed visually** (operator report; no JSON record committed, consistent with §4):

| retest | what it covered | result |
| --- | --- | --- |
| `anim 172.31.1.1 --assets <dir> --name anim_bored_01 --wwise <obb dir>` | head and lift keyframes as 0x93/0x94 with variability (D2, D3); audio at 22320 Hz with probability-chosen alternatives (D1, D4) | **passed visually** |
| `behavior 172.31.1.1 --obb <dir> --seconds 60` | resting face, blink and dart (D6, D7, D8) | **passed visually** |

The falling → impact reaction (D9) was not exercised on hardware and is not required for freezing. M5 and
M7 are therefore **frozen and hardware re-verified at the sweep's HEAD**; the errata status is cleared in
`HANDOFF.md` and `ACCEPTANCE.md`. The reconciliation's own changes (§10) alter the scheduler's timing under
stalls and late ticks and the lift-height readout; neither changes what a normally paced animation sends,
and neither has been retested on hardware. They are offline-verified.

Cube acceptance remains **pending**. Hardware discovery has now been observed (a real cube appeared during
discovery after being tapped, 2026-09-19); connection state, tap, movement, up-axis and battery telemetry
have not been exercised on a robot, and `cubes 172.31.1.1 --acceptance` has not been run.

## 9. Recommendation on resuming M9

**M9 may resume.** The two retests that gated it have passed. Everything M9 builds on (M6 bank reading,
the FNV-1 hash, the scheduler's audio path) is offline-verified; D4 and the reconciliation's frame-counted
timeline are the only sweep changes on that path, and M9's switch-container work extends rather than fights
them.

Two process changes are recommended alongside:

* Commit the per-run acceptance JSON. The runs are real; the repository should not have to take that on
  report.
* Where a face or sound is judged "correct" by eye, name the shipped artifact it is being judged against
  (a clip's face keyframe, a `.wem`) in the acceptance table. D6 and D7 passed acceptance because there was
  no such reference.

## 10. Post-sweep reconciliation, 2026-09-19

A bounded pass after the retests: arithmetic and wording in this document, the retest and cube records, and
three questions resolved from the binary. No feature was added; M9 was not started.

### Corrections to this document

* Provenance counts now come from the table and sum to its row count (see "Provenance counts").
* §1 said "all eight" against a list of nine.
* §4 said every hardware pass through M7 rested on operator report; M1 and M2 have committed evidence.

### Discrepancies found and fixed

| # | where | what the code did | what the original does | evidence |
| --- | --- | --- | --- | --- |
| D10 | `RobotState.LiftHeightMm` (M2 helpers), `Sensors.LiftPositionRaw`, `CozmoRobot.LiftHeight` (M4) | returned `liftAngle` unconverted under a millimetre name, while its own comment said the unit was unconfirmed | `liftAngle` is an angle in radians. `Robot::UpdateFullRobotState` 0x0051291C loads RobotState+0x2C (the field after `headAngle` at +0x28) and stores it into Robot+0x300 (0x0051295E..0x0051296A), then calls `ComputeLiftPose` with it; `Robot::GetLiftHeight` 0x00516F64 and `ConvertLiftAngleToLiftHeightMM` 0x00516F9C convert that field with `sinf(angle) * 66 + 45 (+ 0)`; `ConvertLiftHeightToLiftAngleRad` 0x005170B0 raises the height to 32, uses `(h - 45) / 66` through `asinf` below 92 and the constant 0.712121 = (92 − 45)/66 at or above it. The fw2457 capture's 500+ `RobotState` messages carry lift values inside the radian range, not 32..92 | now `LiftAngleRad`, `LiftHeightMm` (converted), `LiftHeightMmFromAngle`, `LiftAngleRadFromHeight`; the raw pass-through and its "unit unknown" comments are gone |
| D11 | `AnimationScheduler.Advance` (M5) | timeline `t = nowMs − startMs`: while the audio budget refused a frame the wall clock ran on, and the first frame after a stall jumped forward, firing every keyframe that had come due in one frame with one audio frame, the audio position (always per frame) falling behind the keyframes | animation time is a count of streamed frames. `UpdateStream` 0x0057C84C passes one stream time (this+0x84) to `GetAudioToSend`, `ApplyLayersToAnim` and every track's `GetCurrentStreamingMessage`, and adds 33 (0x21) to it only after `SendBufferedMessages` succeeds (0x0057CA94..0x0057CA9C, then loops to 0x0057C92E); `ShouldProcessAnimationFrame` 0x0057CC6C returns false, leaving the stream time untouched, while the send buffer (this+0x94) is non-empty or the audio client has no room. `AnimationStreamer::Update` 0x0057CE5C reads the clock only for the keep-alive timers. So the timeline **freezes** during a stall and catches up frame by frame, never by jumping | `FrameStepMs = 33`; `PositionMs = framesStreamed × 33`; a stalled call leaves the timeline where it is; a late call streams the frames the wall clock owes, each against the budget (the count per call is local policy; the engine streams to the budget every update) |
| D12 | `AnimationScheduler.Play` (M5) | `replaceRunning: false` refused only when the new clip's tracks clashed with the running clip's; on disjoint tracks it replaced the running clip anyway | the engine has exactly one streaming animation (`AnimationStreamer` this+0x38). `SetStreamingAnimation` 0x0057B174: with one streaming and `interruptRunning` false, "Already streaming %s, will not interrupt with %s" and nothing changes, whatever the tracks; with it true, "Animation %s is interrupting animation %s", `Abort()`, then `InitStream`. Nothing runs two animations side by side: blinks, eye shifts and squints are `TrackLayerComponent` layers on the streaming animation, the idle animation (this+0x34) streams only when nothing else does, and `MovementComponent::LockTracks` mutes tracks of the one animation rather than sharing them out | `replaceRunning` is now the engine's `interruptRunning`: false refuses whenever anything is running; true interrupts. No production caller passed false |

Neither D11 nor D12 changes what a normally paced animation sends: one frame per 33 ms tick with its audio
message and the keyframes due at that frame time, exactly as before.

### Classification changes

Three NATIVE rows added (✱✱ in §5), one LOCAL_POLICY row split so that the late-tick behaviour is no longer
listed as local policy (it is now the engine's), one LOCAL_POLICY row added for how many frames one tick
streams, and the M4 cube row's basis updated. Totals in "Provenance counts".

### Tests

| fix | regression |
| --- | --- |
| D10 | `SourceFidelityTests.TheLiftAngleIsAnAngleAndConvertsToHeightAsTheEngineDoes` (an angle of 0 read as 0 mm before, now 45), `TheCapturedRobotReportsItsLiftInRadiansNotMillimetres` (500+ captured states inside the radian range) |
| D11 | `AnimationGapTests.TheTimelineFreezesWhileTheRobotHasNoRoomAndResumesWhereItStopped` (old code jumped to 1353 ms and fired seven events in one frame), `ALateTickCatchesUpFrameByFrameNotByJumping` (old code sent 2 audio frames for 4 keyframes); `AnimationTests.ATickThatArrivesLateFiresEverythingItMissedInOrder` now also asserts the frame count |
| D12 | `AnimationTests.AClipOnAFreeTrackIsStillRefusedBecauseOnlyOneAnimationStreamsAtATime` (replaces the test that asserted the old replacement) |

Test suite after the reconciliation: **444 passed, 0 failed (`dotnet test Cozmo.sln`, 3 m 36 s; 440 after the sweep)**.

### Frozen milestones touched

| milestone | change |
| --- | --- |
| M2 helpers / M4 | lift readout renamed and converted (D10); conformance `control` prints angle and height |
| M5 | scheduler timeline and refusal semantics (D11, D12) |

## 11. M10 additions, 2026-09-19

M10 added a body of NATIVE rows that are tabulated in [DERIVED_STATE.md](DERIVED_STATE.md) rather than folded
into §5: the off-treads classifier (every threshold, filter constant and debounce of `Robot::CheckAndUpdateTreadsState`
0x00511E00 and `Robot::UpdateFullRobotState` 0x0051291C), the unexpected-movement detector
(`MovementComponent::CheckForUnexpectedMovement` 0x0063E398 and its constructor), the reaction-strategy factory's
per-trigger rules (0x0060D5A0 and its lambdas), the shaken / slope / frustration wants-to-run strategies, and
eight `BehaviorReactToX` classes transition by transition. Its non-native items are its §5: two LOCAL_POLICY (the
debounce clock source, the 5 s calibration allowance), one LOCAL_POLICY fallback (pick-up on the raw flag until the
classifier is enabled), three INFERRED (the direct-drive gate on the detector, resume-last mechanics, the cube
path's unlocated transition), one DEFERRED (the per-play body-track lock), and the unmodelled needs/hiccup/DAS
systems.

Two earlier readings were corrected from the binary and are recorded in `BEHAVIOR_LAYER.md`'s M10 errata: the
pick-up reaction's trigger (derived InAir, not the raw flag — a change to frozen M7, kept with a labelled
fallback) and `PlayAnimWithFace` (turns to a face first; not a `PlayAnim`). One session note was corrected
against the shipped file: `reactToRobotShaken.json` has `disableByDefault: false`. The inventory tool's rules
were rewritten from the implemented set (`DERIVED_STATE.md` §9); the regenerated inventory is 67 of 178.

Offline evidence added: `DerivedStateTests` (40 tests) and `offtreads --replay` over the three committed fw2457
captures (0 transitions, 0 detections on a robot sitting on its treads; classifier enabled by the robot's own
calibration report). Hardware evidence: none yet; `HARDWARE_TEST_PLAN.md` items G–J.

## 12. M11 additions, 2026-09-19

M11's NATIVE rows are tabulated in [VISION.md](VISION.md) §1–§6 rather than folded into §5: the marker type table,
the nearest-neighbour library and every table it uses (extracted byte for byte, `extract_marker_library.py`
records the addresses and the check), the decoder's algorithm (`GetProbeValues`, `GetNearestNeighbor`, `Extract`),
the front-end parameters (`MarkerDetector::Parameters::Initialize`), the camera's place on the robot
(`Robot::Robot`, `_kDefaultHeadCamRotation`, `GetCameraPose`), the calibration's NV tag and CLAD struct, the cube's
size, marker size, face poses and codes (`Block::LookupBlockInfo`, `Block::AddFace`,
`KnownMarker::_canonicalCorners3d`), BlockWorld's connected-object rule, moving/rotating gates and visibility test,
the position-update strategy's thresholds (80 mm, 45°, 600000 ms), AcknowledgeObject's constants, and the
`SetBodyAngle` packing.

Non-native, all labelled in the code and in `VISION.md`: LOCAL — the front end's pixel algorithms and its 0.75
dark multiplier, PnP numerics, the clustering tolerances (20 mm / 10°), the flat-snap angle (8°), the history
windows, the turn's speed and acceleration, the verification timeout, the synthetic renderer's border width, the
untimestamped-frame fallback; LOCAL_POLICY — the nominal calibration stand-in and the `AllowUnconnectedObjects`
switch, both off the real-robot path unless asked for; INFERRED — the NV request framing and `MORE` chunking,
the two-miss forgetting count, the 10 px minimum marker size, first-sight Known (pose confirmer not transcribed),
Dirty on `ObjectMoved`, the absolute reading of `SetBodyAngle`, AcknowledgeObject's failure paths; DEFERRED — the
occluder list, `IsAnythingBehind`, the stacked-cube search; OUT OF SCOPE — face, pet and motion detection (Omron
OKAO, 194 `OKAO_*` exports; no Anki algorithm exists to transcribe).

Offline evidence added: `VisionTests` (34 tests), `vision --synthetic` (five cubes, 0.2–3.4 mm), `vision --replay`
over the fw2457 probe capture (28 frames, 0 markers, no cube in the room). The inventory tool gained the M11 verdicts
and a class-name rule for manipulation behaviours; regenerated at 69 of 178. Hardware evidence: none yet;
`HARDWARE_TEST_PLAN.md` items K–M.

## 13. M9/M10 corrections and M12 additions, 2026-09-19/20

**Corrections** (`981ce8c`): a Wwise Stop action now ends the streaming sound (`AnimationScheduler.StartAudio`,
`WwiseAudioSource.IsStopEvent` / `StopAffects`); `WWISE_MUSIC.md` no longer equates "Stop resolves to no PCM" with
correct behaviour. Songs are rendered on a worker when the switch is posted (`Prewarm`), keeping seconds of
rendering off the scheduler thread; the final-PCM cache freezing the renderer's random choices is labelled
LOCAL_POLICY and tested. `BehaviorManager` no longer scores away an active reaction, asks IsRunnable before
WantsToRun for latched strategies, disposes replaced strategies and clears stale resume-last state. (**Superseded
in part, 2026-09-20:** "native order" was wrong — see §17 finding 1. The engine asks `ShouldTriggerBehavior`
first and tests the behaviour afterwards; runnable-before-consume is this stack's own rule for latched
strategies and is kept for them, while target-producing strategies now follow the native order.) The frustration
cooldown stamp and test share one clock. DizzyShakeLoop's repeated restarts are recorded as a fidelity gap.

**M12** NATIVE rows are tabulated in [MANIPULATION.md](MANIPULATION.md) §1: the pre-action pose types and
distances, the path segment and ExecutePath packings, DriveToPose / DriveToObject constants, the dock message
order, the docking error signal's formula and gates, the pick-and-place result layout and its effect on
carrying, the IDockAction pre-action box, the pick-up high/low threshold, the behaviour transitions and their
animation triggers, and the helpers' retry structure. Non-native, labelled (§4): LOCAL — the straight-line
planner, timeouts, target selection; INFERRED — unread message fields sent as zero, lift preset heights, the
retry limit, the carried object's release on the put-down animation; DEFERRED — the lattice planner, pick-up
lift-load / accelerometer checks, face turns, non-upright stacking, the search-for-block fallback; NOT
RECOVERED — KnockOverCubes' flip action. Offline evidence: `ManipulationTests` (14) on a fake robot side.
Hardware evidence: none yet; `HARDWARE_TEST_PLAN.md` items N–R. Inventory regenerated at 82 of 178.

## 14. M13 additions and one correction, 2026-09-20

**Added** (`NAVIGATION.md` §1): the lattice planner over `cozmo_mprim.json` (ASSET) with `xythetaEnvironment`'s
obstacle structure (NATIVE interface; padding and penalty INFERRED), `FlipBlockAction` (0x0055EC80, NATIVE
values), the charger (0x004E9B6C, NATIVE geometry), `AlignWithObjectAction` and `MountChargerAction`
(NATIVE constants), `DriveOffChargerContactsAction`, the block configurations (NATIVE rules), `AIWhiteboard`
(NATIVE interface), `WorkoutComponent` (ASSET), and 16 behaviour classes.

**Correction to M12** (`MANIPULATION.md` §2, `DockActionBase`): the M12 dock base ran a
`VisuallyVerifyObjectAction` before every dock. The engine's `IDockAction::SetupTurnAndVerifyAction` uses
`VisuallyVerifyNoObjectAtPoseAction` (0x00569015) for the place actions. The difference matters: from the 40 mm
place-relative pose the camera (neck at z = 49, 4° built-in tilt, head range to −25°) cannot hold a side marker
fully in view, so the M12 rule refused every stack and every pyramid placement on the offline rig, and would
have on the robot. `PlaceRelObjectAction` now checks that no other located object sits within half a cube of
the placement pose and docks on the marker facing the robot. The fake robot side answers place docks without a
marker signal for the same reason (`ManipRig`), which is a test-harness decision, not engine behaviour: the
firmware's `PLACE_LOW` / `PLACE_HIGH` still track the marker when it is visible (item R).

**Tests:** 606 after M13 (578 after M12). **Classification changes:** 25 configs move from
"requires cube manipulation beyond M12", "requires charger/docking", "requires navigation" and "freeplay" to
"implementable with M13"; four remain beyond M13 for faces or the app (Bouncer, PyramidThankYou,
FeedingSearchForCube, FireTruckAlarm).

## 15. M14 additions and the OKAO boundary, 2026-09-20

**Added** (`FACES.md`): `TrackedFace` (NATIVE geometry, 0x0087DE24), `FaceWorld` (NATIVE rules: 220 mm match,
15 s forgetting, rotating-too-fast and below-robot skips), `SmartFaceID`, `PetWorld`, `TurnTowardsPoseAction`,
`TurnTowardsFaceAction`, `TrackFaceAction`, `VisuallyVerifyFaceAction`, seven behaviour classes (14 configs), and
an `ActionBehavior` base now shared by the manipulation behaviours (no behavioural change).

**Boundary recorded:** face and pet detection, parts, expression, smile, gaze and recognition are Omron OKAO
(`FaceTracker::Impl::Update` → `OKAO_DT_*`, `FaceRecognizer`), 177 exports of proprietary code. The stack's
`IFaceDetector` / `IPetDetector` seams hold `OkaoFaceDetector` / `OkaoPetDetector`, which report unavailable. No
replacement algorithm was written. **Classification:** the 14 face configs move from "requires vision/person
detection" to their own row, "implemented on the face pipeline; runnable only with a face detector (OKAO
unavailable)", and are not counted as implementable (107 of 178 unchanged). 16 configs stay under vision
(FindFaces ×3 and LookForFaceAndCube for the M15 look-around base; PeekABoo ×2, FistBump ×2; PounceOnMotion ×3
and TrackLaser for motion / laser detection; EnrollFace, RespondToRenameFace for recognition).

**Tests:** 619 after M14 (606 after M13).

## 16. M15 additions and three corrections, 2026-09-20

**Added** (`FREEPLAY.md`): `NeedsManager` / `NeedsState` (ASSET configs, NATIVE update and bracket rules),
the activity tree loader (ASSET `activities_config.json` + `activities/**`, NATIVE keys from
`ActivityFreeplay::CreateFromConfig` and `IActivityStrategy`), `ScoringChooser` (NATIVE
`ScoringBSRunnableChooser::GetDesiredActiveBehavior` incl. the running bonus and interrupt line),
`StrictPriorityChooser`, `ActivityStrategy` (NATIVE `WantsToStart` / `WantsToEnd` / `RandomizeCooldown`;
`ActivityStrategyNeedBasedCooldown` 0x005B4298 graphs), `FreeplaySystem` (NATIVE flow and log strings of
`ActivityFreeplay::GetDesiredActiveBehaviorInternal` 0x005AE2xx), six behaviour classes (NATIVE transitions
and constants), `PlayAnimBehavior.WantsToRunStrategy`.

**Corrections:** (1) shipped PlayAnim configs can carry `wantsToRunStrategyConfig` (`IBehavior::ReadFromJson`
→ `WantsToRunStrategyFactory`; `IsRunnableBase` 0x005BD778 asks `WantsToRun`): ReactToObstacle is gated on
ObstacleDetected and had been always runnable, which let it hijack every scoring chooser it appears in.
(2) The strategy type strings are the config's `PlayWithHumans` and `NeedBasedCooldown`, not the class names.
(3) A severe-needs activity's desperation drive is always runnable (`IsRunnableInternal` 0x005D90AC returns 1)
and the activity ends only through "wants to end, and behavior finished", a spark, a put-down or the requested
path (+0x90); the refill happens in the app's Feeding activity, outside freeplay.

**Labelled:** the obstacle flag's source (INFERRED / LOCAL). Everything else in this list - the
desired-from-objects ordering, the `needsActionID` hook, the null-pick switch, `boredomMultiplier`, feature
gates, the pyramid strategy's randomness, decay modifiers, damaged parts and persistence - was read and
settled in the passes that followed (§20).

**Tests:** 630 after M15.

## 17. Correction and hardening pass, 2026-09-20 (after M15)

One bounded pass against head `8ccd694`, from an independent review. Each finding was checked against the code
before anything changed, and the smallest source lookup needed was done where native semantics decided the
answer. Twenty-three findings were confirmed and fixed, one was disproved.

### Recovered from the binary during this pass

| what | where | effect |
| --- | --- | --- |
| `CheckReactionTriggerStrategies` order | 0x005A3550: two strategy predicates (vtable +0x0C / +0x10, `return true` on every strategy read), then `IReactionTriggerStrategy::ShouldTriggerBehavior(robot, behavior)` (0x0060B63A — the behaviour is an argument), then `SwitchToReactionTrigger`, whose failure logs "Trigger strategy %s tried to trigger behavior %s, but init failed" | the behaviour's runnability is tested **after** the strategy fills it in, not before |
| `StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior` | 0x0060B23E: reads the candidate's is-running byte (`IBehavior` +0xA1, the one `IsRunnableBase` logs "Behavior %s is already running" from) at 0x0060B250 and selects it **without** calling `IsRunnable` | disproves finding 18's first half |
| `IActivityStrategy::WantsToEnd` | 0x005B5444: `activityCanEndDurationSecs` (+0x14) is a floor, `activityShouldEndDurationSecs` (+0x18) a ceiling, the subclass's `WantsToEndInternal` (vtable +0x0C) decides between them; epsilon 1e-5 | the can-end duration is now used |
| `IActivityStrategy::WantsToStart` cooldown | 0x005B529C: the cooldown applies while +0x20 > 0 and (last end time > 0 or `startInCooldown` +0x28), measured from the last end time (0 when never run); `RandomizeCooldown` runs inline **after** the check passes (0x005B5336), and the constructor seeds +0x20 with `cooldownBaseSecs` (0x005B5004) | the cooldown lifecycle is corrected; the flat-3 s recent-end special case is labelled not reproduced |
| `IBehavior::UseSecondClosestPreActionPose` / `IDockAction::RemoveMatchingPredockPose` | 0x005BEE40 and 0x00551418: re-read the possible poses, and while more than one remains drop the one matching `Pose3d::IsSameAs(pose, (100,100,100), 0.523599)` | the dock retry really changes the approach |
| `MoveLiftToHeightAction::GetPresetName` | 0x00548CD4: 0 LowDock, 1 HighDock, 2 HeightCarry, 3 OutOfFOV, against the table at 0x00C54688 (32, 76, 92, −1) | confirms the sweep's 32 / 76 / 92 and the M12 regression |
| `BehaviorDriveInDesperation::IsRunnableInternal` | 0x005D90AC: `movs r0, #1` | the desperation drive never stops itself; the activity ends by another path |

### Confirmed and fixed

1. **Target-producing strategies were unreachable.** `ITargetPreparingStrategy` (prepare → test the behaviour →
   commit or abandon) reproduces the native order for `CubeMoved` and `ObjectPositionUpdated`; latched
   strategies keep runnable-before-consume, which is this stack's rule, not the engine's.
2. **CubeMoved saw no real sightings.** The strategy now subscribes to `BlockWorld.ObjectObserved` and
   unsubscribes on dispose.
3. **Two reaction dispatchers.** `ReactToImpact` (new `ReactToImpactBehavior`, the 5 s recalibration allowance
   and the >1000 impact gate in its latch) and `ReactToOnCharger` are registered with `BehaviorManager`;
   `ReactiveBehavior` stands down when a manager shares its arbiter (`BehaviorArbiter.ManagerDispatchesReactions`).
4. **Recalibration readiness.** A `CalibStarted` report clears that motor's calibrated flag, and
   `CalibrationComplete` is false while either motor calibrates.
5. **Shutdown motor stop.** `ReliableTransport.FlushPending` drives the send path until everything queued has
   reached the socket; `CozmoRobot.Dispose` waits up to 250 ms and records `ShutdownStopFlushed`.
6. **Wwise on the scheduler thread.** `ProduceMusic` never waits and never renders: an unprepared song starts a
   worker render and plays silent (counted in `UnpreparedMusicEvents`); `SingingBehavior` holds the tempo step
   until its prewarm completes; `WwiseSongRenderer.Render` serialises its random and sequence state.
7. **Disconnected cubes.** `ObjectConnectionState` with `Connected` false marks the object's pose Unknown.
8. **The fw2457 timestamp.** Once the zero-timestamp fallback picks a state, that state's timestamp dates every
   observation in the frame; the camera's raw value is kept in `VisionSystem.LastRawFrameTimestamp`.
9. **Path lifecycle.** `PathFollower.Reserve` registers before the path is sent, terminal events are retained
   for late waiters, cancellation removes the waiter, and `ManipulationSystem.StartPath` returns a `PathRun`
   that clears the path when its wait ends without a terminal event. Every direct path user went through it.
10. **A failed final turn** fails `DriveToObjectAction` (`DidNotReachPreActionPose`, the nearest shipped result;
    reduction labelled) instead of reporting Success.
11. **The dock retry** excludes the pose it just failed from, by the native rule above.
12. **`WaitForImagesAction`** snapshots the frame counter and waits for frames that arrive after it
    (`PutDownBlockBehavior.ImagesToWaitFor` = 2, INFERRED from `acknowledgeObject.json`); the same snapshot bug
    in `CheckForStackAtInterval` is fixed.
13. **Lift presets** are the native 32 / 76 / 92; `FlipBlockAction` now raises to the real 92 mm carry height.
14. **`RobotAnimationSink.Finished`** no longer stops the wheels for every clip: it stops only body motion this
    animation started and nothing has stopped, and with the keyframe's own `BodyMotion` zero.
15. **A lattice-planner failure** returns `PathPlanningFailedAbort` and sends no path unless the same
    environment reports the straight line clear.
16. **`FlipBlockAction`** awaits the carry-height lift before reporting a result.
17. **`BlockConfigurationManager`** builds a whole snapshot and swaps it under a short lock.
18. **(second half)** `FreeplaySystem` refreshes the running behaviour after an activity switch, so a shared
    behaviour id is not treated as already running when the manager has nothing.
19. **One repetition history.** `ScoringChooser` reads and writes the manager's shared `RepetitionPenalty`;
    `FreeplaySystem` no longer records an interrupted behaviour as having run.
20. **Activity durations and cooldowns** follow `WantsToEnd` / `WantsToStart` as recovered above.
21. **`FreeplayStack`** subscribes the put-down re-pick itself and unsubscribes on dispose; the conformance tool
    no longer installs its own.
22. **Live freeplay fails closed** without a camera calibration; `--nominal` is an explicit, labelled override
    recorded in the acceptance file.
23. **`manip --stack / --knockover / --wheelie / --putdown`** load the animation library from `--obb`, wait for
    the number of cubes they need, check for the stack they need, and say exactly what is missing otherwise.
24. **Face pipeline.** `TrackFaceAction` sends the pan and tilt it computed (through `PanAndTilt`) instead of
    re-solving the head angle; `SmartFaceID` is disposable and every face action releases it;
    `FacePositionUpdated` → `AcknowledgeFace` and `PetInitialDetection` → `ReactToPet` are registered with the
    manager (they cannot fire without a detector, which is the OKAO boundary, not missing wiring).

### Disproved

**Finding 18, first half.** `StrictPriorityChooser` treating the running behaviour as eligible is the native
behaviour: `StrictPriorityBSRunnableChooser::GetDesiredActiveBehavior` (0x0060B23E) tests the is-running byte at
+0xA1 and selects without calling `IsRunnable`. The code is unchanged and now cites the address.

### Still labelled after this pass

The obstacle-detected flag's source, `StrategyObstacleDetected`'s writer, the flat-3 s recent-end cooldown case
and its second time argument, `PetInitialDetection`'s strategy class, the put-down image count, and the
`DriveToObjectAction` result for a failed final turn.

**Tests:** 651 (630 before the pass; 21 new in `CorrectionTests.cs`, and six existing tests updated where a
corrected rule changed what they should assert).

## 18. Source-fidelity cleanup, 2026-09-20 (Pass 1 and M9)

A two-pass cleanup run under one rule: **a passing test does not upgrade provenance**. Pass 1 inventoried
the repository; Pass 2 worked M9 and the animation-audio gaps immediately around it, and nothing else.

**This document is no longer the inventory.** [`fidelity_manifest.json`](fidelity_manifest.json) is: one
record per behaviour-affecting decision, with what it rests on, the best available authority, what is
still unresolved and whether hardware is really needed. `tools/fidelity.py` validates it and regenerates
[`FIDELITY_GAPS.md`](FIDELITY_GAPS.md); `FidelityManifestTests` asserts the same rules on every
`dotnet test`. There are two gates, checked in both directions so neither flag can go stale: a subsystem
has exhausted its **source investigation** only when nothing on its live execution path is a
RECOVERABLE_GAP, and its **implementation fidelity** is complete only when nothing there is an
IMPLEMENTATION_GAP. Neither says the behaviour is reproduced; BLOCKED_EXTERNAL and HARDWARE_ONLY records
survive both and are counted separately.
Sections 1 to 17 above stand as the record of how the code got here; where they and the manifest disagree,
the manifest is current.

### The M9 fault, and what it was

Every one of the 42 recordings under the singing sampler's note-on layer is a sustained vowel of 4.2 to
7.4 seconds carrying `Loop = 0`. The notes in the songs are 125 ms to about a second. The renderer let a
looping sound play out a whole iteration after the note was released, so a 187 ms note sounded for 5.79
seconds, about thirty overlapped at any moment, and the sum ran 13.2 dB over full scale — which the local
output stage then quietly pulled back down by 13.2 dB. A note now sounds for as long as it is held, which
is what the loop property, the break-on-note-off bit and the existence of a half-second note-off layer at
−14 dB all ask for. Aba Daba's raw peak went from 149572 to 36984.

### Recovered from the binary and the banks during this pass

| what | where | effect |
| --- | --- | --- |
| Modulator objects (HIRC 21 and 22) | eleven in `Cozmo.bnk`, all consuming exactly; ids 2..8 occur only on LFOs and 9..15 only on envelopes | the vibrato LFO and the note-off envelope are read and applied |
| The two singing bindings | blend 110896138 → Pitch over 0..580 cents; blend 462443456 → Volume over 0..−1 dB; neither target sets the property it drives | one decibel is the whole of the note-off envelope's authority over the level, which is not what WWISE_MUSIC.md said it was |
| MIDI note tracking is off | the node bit vectors: only 0x00, 0x01 and 0x24 occur in any bank, 0x01 on exactly the three nodes that set Priority, 0x24 on exactly the two note layers | no bit is left that could enable it, so this rests on the data rather than on the absence of a root note |
| The robot's bus | `RobotAudioClient::RobotAudioClient` 0x005994A0, 0x0059962A..0x0059966A: game objects 7..10 to plug-in indices 1..4 and buses 0x9FA59539 + 3, 6, 5, 0 | a singing voice leaves through `Robot_Bus_1`, and each bus's Anki Hijack parameter is the same index, so bank and binary agree from two directions |
| That bus's effect chain | `Init.bnk`: MasterCurve EQ, HiLowPass EQ, Peak Limiter (−1 dB, 10.8:1, 9 ms, 41 ms), then the hijack | the output stage is the product's, not a local peak normalisation; every song now leaves it between 30000 and 32000 of full scale |
| The cube shake | `CubeAccelComponent::AddListener` 0x0063547E sends `StreamObjectAccel`; `HighPassFilterListener::UpdateInternal` 0x00636598; `ShakeListener` 0x00636620 and 0x0063679E; `BehaviorSinging::InitInternal` 0x005EECF4 passes 0.5, 2.5, 3.9 | the vibrato has an input; it saturates at a filtered magnitude of about 55 against a start threshold of 3.9 |
| Container play mode | the play-mode bit, set on 12 of 13 sequence containers and 13 of 455 random ones | an event's target is walked with each container's semantics instead of flattened |
| Bus and effect object layouts | all 15 buses and all 89 effects now consume exactly | fourteen object types read, every object consumed exactly |

### Corrections to earlier sections

* **§5, M6, "all Sounds under a Play target are alternatives; sorted child order".** They are not
  alternatives in general: a sequence container whose play mode is continuous plays its items one after
  another, and a switch container follows the switch. The get-in, a random of three two-note phrases each
  drawn from three takes, was coming out as one fixed syllable (manifest M6-006, M6-007).
* **§5, M6, "IMA ADPCM mono; stereo refused".** Recorded here as local policy on the grounds that no robot
  event reaches a stereo file. Eight do: the three effort grunts, the spark launch and the four scan
  sounds are silent. Now a RECOVERABLE_GAP (M6-003), and `wwise --coverage` lists them.
* **§5, M6, "resampling and mix-down | LOCAL_POLICY".** Nearest-sample decimation from 48000 to 22320
  folds everything above 11160 Hz back into the band. That is not a policy, it is a defect; it is now a
  band-limited windowed sinc (M6-004).
* **WWISE_MUSIC.md** said the robot's bus carries a peak limiter and a master compressor. The compressor is
  on `Cozmo_Robot_External`, the bus for the app's spoken text; `Cozmo_Robot` carries no effects at all.

### Left, and named as what it is

M9 holds no RECOVERABLE_GAP on its live path. Four of its open items are in the Wwise runtime, which does
not ship in the APK — verified, not assumed: no `AkSoundEngine`, `CAk*`, `AkModulator` or `Wwise` string
occurs in any `.so`. The largest is **M9-013**: the get-in branch is a child of the MIDI target and carries
no filter of its own, so under the container rules a note reaches it — 41 extra voices on a 42-note song.
Every render reports each branch's share and `--without-branch get-in` renders the other reading, so the
two can be listened to side by side; only a recording of the stock app singing can settle it.

**Tests:** 721 (651 before the pass; 710 before the review in section 19). The runner is capped at two parallel threads: several behaviour rigs
drive against a wall-clock budget while sleeping between ticks, and on a four-core machine the default
parallelism starved them into failing somewhere different on every run.

## 19. The review of `461b900`, and what it found in the fidelity system itself

The pass above produced a manifest that could describe a subsystem as finished while it knowingly did
something the original does not. Three faults, all in the apparatus rather than in the audio.

### 19.1 One word was carrying two questions

`source_complete` meant "no RECOVERABLE_GAP on the live path" — nothing left to read. It said nothing
about whether what had been read was built, so a subsystem could assert it while a dozen recovered
behaviours sat unimplemented. Worse, `COMPATIBILITY_POLICY` had become the place those went: a status
meant for deliberate product decisions was absorbing fidelity work that was merely hard, and
`EQUIVALENT_IMPLEMENTATION` was absorbing substitutions nobody had shown to be equivalent.

`IMPLEMENTATION_GAP` now names them: the native behaviour is established from primary evidence and the
production code knowingly does something else. Nine records moved into it, each read on its own terms —
the RLE skip and repeat commands (M3-007), the animation cooldown and head-angle gate that are parsed and
never consulted (M5-014), the audio fallback that papers over M6-003 (M5-017), the idle head and lift that
go through the motion API instead of a live clip (M7-009), the idle body shuffle that is recovered and not
driven (M7-010), the blanket 5 s reaction cooldown the engine does not have (M7-011), the music event that
renders one of nine Play actions (M9-021), the carried-object pose chain collapsed to one link (M12-008),
and the planner's straight-line fallback (M13-005). Two went the other way, into RECOVERABLE_GAP, because
the original had not in fact been read: what the engine leaves on the screen after a clip (M5-019) and
what each behaviour class waits for during recalibration (M8-008).

Both gates are enforced in both directions, by `tools/fidelity.py --check` and by `FidelityManifestTests`,
and `source_complete` is gone. A test fails if it comes back.

### 19.2 The vibrato could not reach the song

`Prewarm` rendered the whole song and cached its samples; `SetParameter` wrote into a dictionary the
render had already finished reading. The engine posts `Cozmo_Singing_Vibrato` on every tick
(`BehaviorSinging::UpdateInternal` 0x005EF0C8) and the bank binds it to the depth of the LFO on the
sampler's pitch, so a continuously posted parameter met audio that was already decided. The test that
covered it set the parameter *before* rendering and passed.

The song is now rendered a block at a time as it plays, each block reading the parameters as they stand,
on a worker, `WwiseMusicStream.LeadMs` ahead of the playback clock; each voice keeps its own read
position, so a note already sounding picks a shake up mid-note. The scheduler still gets a buffer it can
read without blocking. The regression test prepares a song, begins it, posts the vibrato afterwards and
checks that everything rendered after that point differs and everything before it does not.

The 66 ms lead is this stack's choice and it is stated as one. It is small against the engine's own:
`UpdateAmountToSend` 0x0057C6F0 lets the engine run up to 14 audio frames ahead of what the robot has
played, which at 744 samples and 22320 Hz is 467 ms of audio committed before it is heard. Neither stack
can change audio it has already sent. The unknown LFO waveform stays BLOCKED_EXTERNAL (M9-025) and is now
marked live, because a shake reaches a playing song.

### 19.3 Four M9 records said more than the data supports

Re-auditing every non-EXACT_SOURCE record on M9's live path against the banks, rather than against its
own text, changed four.

* **M9-014, velocity.** "Velocity is ignored" was recorded as an equivalent implementation. The data half
  holds: of the 199 nodes under the MIDI target none carries a velocity range, and the only binding of
  any kind under it is the vibrato modulator. But the songs vary velocity — 119..127 in Aba Daba,
  104..116 in Frère Jacques — so whether Wwise maps velocity to level with nothing asking it to is an
  open question about an audible difference. BLOCKED_EXTERNAL.
* **M9-020, the clip window.** "Barely exercised, because every clip has PlayAt 0 and BeginTrim 0." The
  start of the window is never moved; the end of it drops 1835 notes across the 83 music events, 762 of
  them in William Tell alone, whose MIDI holds a full-length rendition the clip takes twelve seconds of.
  The part that is a runtime judgement — what becomes of a note still held at the end — reaches two notes
  in the whole product, and the segment ends at the same instant, so both readings sound the same.
* **M9-018, the Stop action.** The ancestor half of the rule is this stack's generalisation and no
  shipped event reaches it: the three tempo events play 914766641, 139286641 and 602865028 and the stop
  event targets exactly those three.
* **M9-011, the bus chain.** Now separates what is read — routing, chain order, every setting — from what
  is not, the arithmetic inside each effect, which is M9-026.

`wwise --sampler` and `wwise --validate-music` print these counts, so they are reproducible rather than
recorded once. `--validate-music` also stopped calling `Play__Music__Play` a failure: with no switch set
it selects the switch tree's key-0 path, a one-second segment holding nothing, which is the container
answering correctly.

## 20. The last of the live-path gaps, 2026-09-21

The manifest's live path came down to two records, both in the vision front end. What was settled on the way:

* **The needs-action hook is the behaviour's.** `IBehavior::NeedActionCompleted` 0x005BE40C uses the running
  behaviour's own `needsActionID` when the caller names none, and sixteen behaviours call it - ten reporting
  their configured action, six naming one outright (`PickupCube`, `StackCube`, the three GuardDog results,
  `PlacedOnSide`, `BoredOnSide`, the dizzy tiers). An **activity's** `needsActionID` is read into
  `IActivity+0x1C` by `ReadConfig` and nothing in the build ever loads that word again, so the three shipped
  activity-level ids are dead data and reporting one when the activity ended - which this stack did - is
  something the engine never does (M15-008).
* **An activity is decided twice.** `ActivityFreeplay::GetDesiredActiveBehaviorInternal` tests the forced and
  requested activities, the spark, a missing activity and then `WantsToEnd` before asking the activity for a
  behaviour - and then decides again on the answer: a null pick ends the activity and bars it from the
  re-pick, and a pick that is not the behaviour already running ends it when the strategy wants to end,
  unless a sparks reward is still to be communicated. `IActivity::OnDeselected` hands that reward to the app
  on the way out, which is also where `BehaviorEarnedSparks` does it - on stop, not on start (M15-006).
* **Unexpected movement has a second half.** The robot goes back to where the state history says it was when
  the wheels and the gyro started disagreeing, keeping the heading it drifted to, and a collision obstacle -
  the markerless `CollisionObstacle`, 20 x 54.2 x 67.7 mm - is left 22.1 mm ahead, 55.9 mm behind or 27.1 mm
  to a side, past its own depth and 5 mm of clearance (M10-007).
* **`absLocalizationUpdate` was two-thirds guessed.** Its first word is the vision-only state's timestamp and
  its last is the pose's heading in radians; pycozmo had both as unknown integers, and its `0x80000000` in
  the last word is -0.0 (M2-007, a newly found gap).
* **The memory map is in.** The eleven content types, the family mapping, the cliff and prox insertions, the
  robot's own passage and the ray query the face behaviour makes before driving in - which drives -15 mm
  instead of 40 when the way is not clear (M14-007). Its vision-derived edges are M11-017.
* **Stereo ADPCM.** The 72-byte block is two 36-byte mono blocks side by side; the arithmetic says so before
  the bytes do, and eight Robot_SFX events stop being silent (M6-003).
* **The dark mask was a guess.** The engine binomial-filters the image and marks a pixel dark when
  `(filtered * 0xCCCC) >> 16 > pixel`; this stack had a three-level box pyramid and 0.75. The quad acceptance
  test is now the engine's four rules too, and the 512 recorded as "maximum quads" turned out to be the quad
  symmetry threshold, 2.0 in 8.8 (M11-018).
* **The last IMPLEMENTATION_GAP anywhere.** An event with several Play actions renders all of them (M9-021).

## 21. The corners the engine fits, and the ground it looks at (2026-09-21, later)

Both of the records above are answered, and one narrower one is left in their place.

* **The corner extraction is the engine's.** `TraceNextExteriorBoundary` 0x008C6B18 builds a staircase
  contour out of four extent arrays over the component's bounding box - not a pixel walk - and
  `ExtractLineFitsPeaks` 0x008A5DB8 differentiates a Gaussian of sigma = length / 64, convolves that one
  kernel circularly with the contour and normalises, splits the unit tangents into four with `cv::kmeans`
  seeded by four equal arcs and run with `KMEANS_USE_INITIAL_LABELS` (so nothing is random), fits each
  cluster across whichever extent is wider, intersects all six pairs and keeps the four inside the image,
  and orders them by angle about their centroid. The quad is then permuted 0, 3, 1, 2 into the decoder's
  order, and `IsQuadrilateralReasonable`'s `bool&` turns out to report whether that order needs its middle
  pair exchanged rather than whether the quad is reasonable (M11-005, now down to `RefineQuadrilateral`
  0x008C55E0 alone).
* **The memory map's edges are in.** The ground ROI, the seven-by-five kernel at 0x00C8E020, the threshold
  of 50, the column walk from the bottom up, the homography, the lift gate, the five-millimetre chains, the
  forty-degree runs, the clear triangles and lines, the interesting edges and the border pass that writes
  one off against an obstacle (M11-017, closed).

What remains is the sub-pixel refinement: `VisionMarker::RefineCorners` 0x0089FD98 and
`RefineQuadrilateral` 0x008C55E0, a Gauss-Newton refinement of the marker's homography whose parameters
this stack already has and whose arithmetic it does not (M11-005).
