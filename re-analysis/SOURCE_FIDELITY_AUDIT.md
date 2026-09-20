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
* Wwise: nearest-sample resampling and channel averaging; stereo ADPCM refused.
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
| IMA ADPCM mono; stereo refused | CORROBORATED / LOCAL_POLICY | self-verifying decode |
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
WantsToRun (native order), disposes replaced strategies and clears stale resume-last state. The frustration
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
