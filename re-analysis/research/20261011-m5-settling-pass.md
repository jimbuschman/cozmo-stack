# M5-animation final settling pass (Opus, 2026-10-11)

Read-only cozmo-verifier pass at HEAD 00063637. Every record was quoted from the current manifest. Manager spot-checks
(2026-10-11) held: the loop-exhaust branch 0x0057D25C..0x0057D260 (`movs r0,#0; str r0,[r4,#0x38]; b 0x57d04e`, which
lands on HaveLayersToSend at 0x0057D04E) and TimeUntilCooldownOver 0x0058BA54..0x0058BA7C (f64 sub, vcvt.f32.f64;
absent name gives the literal 0.0f at 0x0058BA80). **Verdict: FAIL.**

**Cross-cutting:** almost every engine log on the M5 path is missing or has an invented key or text. "LOG" below means
the C# log differs from the engine string at the cited address. The exact formats and arguments are extracted in a
follow-up rows file before the build.

## 1. Per record

| id | status (current) | verdict | evidence | to build / fix |
|---|---|---|---|---|
| M5-001 | EXACT_SOURCE | DEFECT + PATH-GAP | (1) LOG key `Animation.DefineFromFlatBuf.AddKeyFrameFailure` (0x00576290), format "Adding <Type> frame %d failed." index only (0x005762C0, 0xBEE9AE..; sErrorF 0x00576104/0x00576146) vs AnimationLibrary.cs:490,507. (2) The engine loads every clip at startup (LoadAnimationsInternal 0x0051F884..); C# parses lazily (AnimationLibrary.cs:383-406). (3) AnimationLibrary.cs:304-305 registers every `.bin` stem as a clip name and :400-401 falls back case-insensitively, then to the file's first clip; 146 of 289 shipped stems are not clip names and the engine's GetAnimation returns null for them. (4) The DeviceAudio step in UpdateStream (0x0057C876..0x0057C8E0) is unbuilt (new record N2). | Port the log; eager load; remove the stem alias and first-clip fallback. |
| M5-002 | EXACT_SOURCE | DEFECT | LOG missing: `ProceduralFace.SetEyeArrayHelper.WrongNumParams` "Unexpected number of parameters for %s array (%lu vs. %lu)" (sWarningF 0x00583808; 0x00583864/0x00583894) vs AnimationLibrary.cs:569-570. | Add the warning. |
| M5-003 | EQUIVALENT_IMPLEMENTATION | DEFECT (log) | Arithmetic holds (0x004F99FC..0x004F9A24). LOG `AnimationStreamer.GetFaceHelper.FramesTooClose` "currentFrameTriggerTime: %d ms, nextFrameTriggerTime: %d, StreamTime: %d" (0x0058CDEA; 0x0058CF50, 0xBEFEEF) vs TrackLayers.cs:766. | Fix the log. |
| M5-004 | EXACT_SOURCE | HOLDS | 0x004F8C08..0x004F8C4A; 0x004F8F80..0x004F8FC4 ↔ AnimationScheduler.cs:1345-1358, CozmoAnimations.cs:62-67. | — |
| M5-005 | IMPLEMENTATION_GAP | SETTLE after one fix | RandIntInRange 0x0082FA96..0x0082FAC6 computes b−a+1 in int32 (`rsb r1,r4,#1; add r1,r2` 0x0082FA9E); EngineRandom.cs:54 uses double. Rest matches. | `unchecked(b - a + 1)` as int. |
| M5-006 | EXACT_SOURCE | DEFECT | Wire holds (0x004FBA8C..0x004FBB0E). LOG missing: clamp infos `BodyMotionKeyFrame.CheckRotationSpeed.PointTurnSpeedExceedsLimit` (0xBE5221), `.CheckStraightSpeed.StraightSpeedExceedsLimit` (0x004FB338), `.CheckTurnSpeed.ArcSpeedExceedsLimit` (0x004FB434) vs AnimationClip.cs:130-140; reject key `BodyMotionKeyFrame.BadRadiusString` (0x004FB6F0) vs AnimationLibrary.cs:644. | Port the 4 logs. |
| M5-007 | EXACT_SOURCE | HOLDS | 0x0057CA86..0x0057CA9C ↔ AnimationScheduler.cs:1272-1279. | — |
| M5-008 | EXACT_SOURCE | DEFECT + PATH-GAP | (1) Loop exhaustion: 0x0057D25C..0x0057D260 jumps to 0x0057D04E (HaveLayersToSend/StreamLayers or flush), skipping the idle test; C# AnimationScheduler.cs:985-1001 falls into NoAnimationPathLocked (:1023) and starts the idle in the same tick. Outside the cited A13 range. (2) LOG: Aborting "Animation %s is interrupting animation %s" (new or "NULL", old) (0xBEEE0F; 0x0057B21C) vs :689; LoopingLiveAnimation via sVerifyFailedReturnFalse (0x0057B2C0; 0xBEEE7F/0xBEEEBC) vs :710. | Fix the branch; port the logs. |
| M5-009 | EXACT_SOURCE | DEFECT | LOG `RobotAudioKeyFrame.SetMembersFromFlatBuf.UnknownProbabilities` "%s: The number of audio event IDs (%i) does not match number of probabilities (%i)" (0x004FA08C/0xBE502C) and `.TotalProbabilitiesTooHigh` "%s: The total probability of all audio events combined exceeds 1.0" (0xBE507F/0xBE50C2) vs AnimationLibrary.cs:594,603. | Port the logs. |
| M5-010 | EXACT_SOURCE | DEFECT | LOG `AnimationStreamer.Constructor.BadNeutralAnimGroup` "Neutral animation group %s for trigger %s is empty or null" (0xBEEC95/0xBEECC7, sErrorF 0x0057A122) vs AnimationScheduler.cs:623; `...MultipleNeutralFaceAnimations` (0x0057A3C8/0xBEED02) vs :629; `...NeutralFaceDataNotFound` "Could not find expected neutral face animation file called %s" (0xBEED52/0x0057A408, 0x0057A234) absent. | Port the logs. |
| M5-011 | IMPLEMENTATION_GAP | DEFECT | (1) Σw in f32 (vadd.f32 0x0058AA60), r narrowed to f32 (0x0058AA92), r −= w in f32 (0x0058AA9E); C# AnimationLibrary.cs:121-129 double. (2) Head window f32: asFloat × 0x3C8EFA35 (0x0058C7A4..0x0058C7BE) vs f32 robot+0x2FC (0x0058AA16..0x0058AA30); C# :31-32,44 double. (3) Time = f32 MoodManager+0x130 (0x0058A95C) widened; C# :426 double. (4) CozmoAnimations.PlayGroup/PlayTrigger (CozmoAnimations.cs:354) and Behavior/AnimationTriggerMap.cs:126 call `g.Choose(_random, mood)`: no clock, no head angle, System.Random. (5) LOG: Found / NoMoodMatch / AllOnCooldown / BackupAnimationFound / NoBackup (info, channel "Animations") and NoAnimation (error) (0xBEFA6D..0xBEFD16) absent; C# invents errors at :423/:430; the engine returns "" silently for a null/empty group (0x0057AE80..0x0057AE8A). | f32 math; route through GetAnimationNameFromGroup; port the logs. |
| M5-012 | EXACT_SOURCE | HOLDS | 0x004F9E18. | — |
| M5-013 | IMPLEMENTATION_GAP | NOT SETTLEABLE | GetStreamMessage/IsDone 0x004F976C..0x004F97AE match. +0x28: ctor memclr4(+0x18, 0x11) zeroes it (0x005642C6..0x005642CC); SetFaceAction::Init (0x00563E48) and the CannedAnimationContainer ctor (0x00587CCC) don't write it. The FlatBuffer/JSON add path is UNKNOWN (new record N7). | — |
| M5-014 | IMPLEMENTATION_GAP | DEFECT | (1) Cooldown is a double at entry+0x10 (0x0058C71E; f64 add 0x0058AABC); C# float (AnimationLibrary.cs:17, 737); not observable on shipped data (9 values, all f32-exact). (2) TimeUntilCooldownOver: 0.0f for an absent name, else (f32)(end−now) (0x0058BA54..0x0058BA7C); C# −∞ and double (:58-60, 151). (3) Backup takes an entry only if left < best, best = FLT_MAX (0x0058ABF6, 0x0058AC20..0x0058AC28); C# :152 takes the first. (4) Window min+(−0.05f), max+0.05f in f32 (0xBD4CCCCD/0x3D4CCCCD, 0x0058AC2A..0x0058AC4C); C# :150 double. Per-candidate info log 0x0058AC7E absent. | Fix (1)-(4); port the log. |
| M5-015 | EXACT_SOURCE | HOLDS | 0x005851A4..0x005851C0 ↔ ProceduralFaceRenderer.cs:245-246. | — |
| M5-016 | EXACT_SOURCE | DEFECT | LOG missing in GetColorOptional: bad type (0x00840310), component <0 or >1 (0x00840398, 0x008403F4, 0x0084044C); AnimationClip.cs:259-274 silent. | Port the warnings. |
| M5-017 | IMPLEMENTATION_GAP | DEFECT | Selection 0x004F9B16..0x004F9BF8 matches. The draw is made by RobotAudioAnimation::InitAnimation (only caller 0x005968A0/0x005968AC) at InitStream for every RobotAudio keyframe to the track's end (0x00596898..0x00596914); C# draws at the trigger (AudioAnimation.Start :1661-1666), interleaved on sRNG, and skips when AudioSource is null. Shared with M6-028 (N5). | Move the draws to InitStream. |
| M5-018 | IMPLEMENTATION_GAP | SETTLE (own span) | 0x0057CC6C..0x0057CC98 ↔ AnimationScheduler.cs:1239-1250; tail 0x0057CCA6 → 0x8CB8AC is M6-027's (2026-10-10 overlap ruling). | Settle with the M6-027 handoff named. |
| M5-019 | EXACT_SOURCE | HOLDS | 0x0064F1EE..0x0064F1FC ↔ TrackLayers.cs:731-735. | — |
| M5-020 | IMPLEMENTATION_GAP | DEFECT | PlayTrigger → PlayGroup → `g.Choose(_random)` (CozmoAnimations.cs:350-371) contradicts its provenance ("then M5-011's group choice"). Seed UNKNOWN (libmono). | Route through GetAnimationNameFromGroup. |
| M5-021 | IMPLEMENTATION_GAP | NOT SETTLEABLE | Point transform (0x0053A8AC..0x0053A90C) ↔ ProceduralFaceRenderer.cs:201-202 holds; tanf/cosf are bionic (MathF, :265-287); the OpenCv310 port was not re-verified. | — |
| M5-022 | IMPLEMENTATION_GAP | DEFECT | SendAbortAnimation (0x00517DE4..0x00517E0A) ↔ Motion.cs:1100 holds. CozmoRobot.Dispose (CozmoRobot.cs:636-646) is a partial AbortAll (no Path.Abort, no AbortDocking) instead of M4-026's (Motion.cs:1089), and sends DriveWheels(0) (CozmoRobot.cs:611), which is no approved departure. | Call M4-026's AbortAll; remove DriveWheels(0). |
| M5-023 | EXACT_SOURCE | DEFECT | Structure holds (0x0057B3F2..0x0057B58A). LOG `AnimationStreamer.Abort` "Tag=%d %s:%s hasFramesLeft=%d audioComplete=%d startSent=%d endSent=%d sendBuffer=%s" (0x0057B526; 0x0057B644/0xBEEF06) vs AnimationScheduler.cs:793; `RobotEventHandler.HandleAnimationAborted.SendingRobotAbortAnimation` after the send (0x00528818; 0xBE8718) absent at CozmoAnimations.cs:176. | Port the logs. |
| M5-024 | EXACT_SOURCE | DEFECT | SendBufferNotEmpty is sChanneledInfoF "Expecting SendBuffer to be empty. Will clear." (0x0057B7A6; 0x0057B874/0x0057B898); C# :825 warning with no text. ClearSendBuffer's per-message debug (0x0057B082) absent. | Fix level/text. |
| M5-025 | EXACT_SOURCE | HOLDS | 0x0057C94E..0x0057CA7A ↔ :1332-1404. | — |
| M5-026 | EXACT_SOURCE | HOLDS | 0x0057CB44..0x0057CBB0; 0x0057C464..0x0057C496; 0x0057C42A ↔ :1286-1321. | — |
| M5-027 | IMPLEMENTATION_GAP | DEFECT | (1) The loop-exhaust branch (as M5-008). (2) LOG `EmptyIdleAnimationFromGroup` (0xBEF1AB/0xBEF1E0, EnumToString(trigger), sets _errG 0x0057D34C), `InvalidIdleAnimationFromGroup` (0xBEF21F/0xBEF256) vs the invented "IdleAnimNotFound" (:1113); success debug (0x0057D2D2) absent; flush "%zu messages" (0xBEF2CC) and `SendBufferedMessagesFailed` (0x0057D196) vs :1033-1035; RemoveIdle `WillNotPopLast` (0xBEEF5B) missing at :905, `NoLockNameFoundInStack` (0xBEF0C8) and `AnimationRemovedFromMiddleOfStack` (0xBEF032) vs :909/:913; PushIdleAnimation log (0x0057BA1C) absent. | Fix the branch; port the logs. |
| M5-028 | EXACT_SOURCE | DEFECT | LOG `AnimationStreamer.KeepFaceAlive.BadBlinkSpacingParams` "Max (%d) must be greater than min (%d)" (max, min) (0x0058D51A; 0x0058D608/0x0058D640) vs TrackLayers.cs:187. | Port the log. |
| M5-029 | EXACT_SOURCE | DEFECT | LOG `ITrackLayerManager.RemovePersistentLayer` (0xBEFFB3) vs TrackLayers.cs:254; EmptyPersistentLayer "Persistent face layer is empty - perhaps live frames were used? (tag=%d)" (0xBEFF6A) missing at :99; `Animation.Track.AddKeyFrameToBack.TooManyFrames` (0xBED991/0xBED873, 0x00564424) absent at StreamAnimation.cs:53-59. | Port the logs. |
| M5-030 | IMPLEMENTATION_GAP | NOT SETTLEABLE | L1..L7 match (:1138-1210); reachable only if ProceduralLive is pushed, whose pusher is unrecovered (:930-933). `Animation.AddKeyFrameToBack.Failed` (0xBED95D) absent. | Recover the pusher. |
| M5-031 | IMPLEMENTATION_GAP | DEFECT | Inherits M5-032's row fraction; DesiredFaceDistortion (M7) unset. | — |
| M5-032 | IMPLEMENTATION_GAP | DEFECT | 1/(max−min) once (vdiv 0x00585E28) then multiply (0x00585E34); ProceduralFaceRenderer.cs:111 divides. cosf/sinf (0x0058503C/0x00585046) are MathF at :165, unrecorded. | inv·(row−min); record libm. |
| M5-033 | EXACT_SOURCE | DEFECT | LOG `EventKeyFrame.UnrecognizedEventName` (0xBE5199) vs AnimationLibrary.cs:626; TurnTo clamp infos (0xBE52D8, 0xBE5324, 0xBE53B5) absent at :657-660. | Port the logs. |
| M5-034 | EXACT_SOURCE | DEFECT | Found: channel "Animations", key `GetResponseForAnimationTrigger.Found`, "%s -> %s" (0x0067088A; 0xBFEC4A/0xBFEC6F) vs AnimationLibrary.cs:205; unknown key `AnimationTriggerResponsesContainer::GetResponse` with `::` (0xBFEBEC) vs :208. | Fix the keys. |
| M5-035 | IMPLEMENTATION_GAP | NOT SETTLEABLE | B1..B3 hold; B4 depends on M5-030. | — |
| M5-036 | HARDWARE_ONLY | unchanged | — | — |
| M5-037 | RECOVERABLE_GAP | rows read | ~AnimationStreamer 0x0057AF48..0x0057AFDA: ClearSendBuffer (buffer dropped unsent, debug per message 0x0057AF58, 0x0057B082); +0x1B4 handle vector destroyed (unsubscribes the SetupHandlers G2E subscriptions, 0x0057AF6C); live anim +0xA8 (0x0057AF74); send list (0x0057AF7C); song notes (0x0057AF84); TLC deleted (0x0057AF8C..0x0057AF98); idle stack (0x0057AFAA); param maps (0x0057AFBE..0x0057AFCC); shared_ptr +0xC released (0x0057AFD0). No sends. Driving-animation handler robot+0x24C (0x00511524..0x00511542): handle vector +0x4C, DrivingAnimations +8; no sends. UNKNOWN: ~TrackLayerComponent internals, the +0xC object. | IMPLEMENTATION_GAP for the read part. |
| M5-038 | RECOVERABLE_GAP | rows read | 0x0052CEA2..0x0052CFBE: CompoundActionSequential; TriggerAnimationAction(robot, trigger, numLoops 1, interruptRunning 1, tracksToLock 0, timeout 0x42700000 = 60.0f, strictCooldown 0) for 0xD2 GoToSleepGetIn, 0xD5 GoToSleepSleeping, 0xD4 GoToSleepOff in that order, each AddAction (vtbl+0x20, 0x0052CEEC/F22/F58); CompoundActionParallel with the sequence (0x0052CF80); MoveLiftToHeightAction(robot, preset 0, 5.0f) (0x0052CFA2..0x0052CFB0, M4-028); returns the parallel. Names: Unity AnimationTrigger.cs indices 210/212/213. | IMPLEMENTATION_GAP. |
| M5-039 | RECOVERABLE_GAP | rows read | robot+0x2A set by HandleRobotConnectionResponse's on-idle lambda (0x0052C3A6; info 0xBE87F5; M1's write); +0x29 by HandleSyncTimeAck (0x005366AC). Robot::Update 0x0051410C..0x00514140: both set before AnimationStreamer::Update(robot+0x60); nonzero gives sWarningF `Robot.Update.AnimationStreamer` "Robot %d had an animation streamer failure (%d)" (0xBE6A13/0xBE6A32). C# Advance returns void (AnimationScheduler.cs:949; CozmoAnimations.cs:246-255). | IMPLEMENTATION_GAP: return results from Update/UpdateStream/StreamLayers; log the warning. |

## 2. New records proposed

- **N1 → M5-040 (RECOVERABLE_GAP):** ProceduralFace ClipWarnFcn (static std::function 0x0105AB20; EnableClippingWarning
  0x00584B48 sets 0x00584A41, which logs sWarningF `ProceduralFace.Clip.OutOfRange` "Value of %f out of range [%f,%f]
  for parameter %s. Clipping." (0x00584A78; 0x00584ACC/0x00584AEC), or the no-op 0x00584BE5; Clip calls it on both clamp
  branches (0x00584880 and the max branch)). UNKNOWN: where it is enabled or disabled.
- **N2 → M5-041 (IMPLEMENTATION_GAP):** the DeviceAudio wall-clock step in UpdateStream (0x0057C876..0x0057C8E0:
  IsTimeToPlay(+0x80, now), PlayOnDevice, MoveToNextKeyFrame).
- **N3 → M5-042 (COMPATIBILITY_POLICY, SDK scope):** AnimationStreamer::Update's VizManager::SetText (0x0057CE92,
  0x0057CEDA, 0x0057D0FC) and CozmoContext::SetSdkStatus(2, name) (0x0057CF2A, 0x0057CF58, 0x0057D11C) on every Update:
  SDK and viz paths not reproduced (operator, 2026-10-09).
- **N4:** folded into M5-039.
- **N5:** InitAnimation's audio-reference draws (0x00596898..0x00596914): M5-017 builds them; M6-028 is named as the
  OnRobot consumer.
- **N6:** the M5 debug-channel logs (idle pick 0x0057D2D2, GetAnimationName 0x0058A9B8, GetAudioRefIndex
  0x004F9BAC/0x004F9C10, ClearSendBuffer 0x0057B082): carried by their owning records' log rows, no separate record.
- **N7 → M5-043 (RECOVERABLE_GAP):** the FaceAnimationKeyFrame +0x28 writers on the FlatBuffer/JSON add path
  (Track::AddKeyFrameToBack(CozmoAnim::FaceAnimation*)), split out of M5-013.

## 3. Tests

No circular tests found in the sample (M5_005_R1, M5_006_*, M5_008_*, M5_009, M5_010_*, M5_011_D5, M5_014_D6/D7,
M5_028_*, M5_029, M5_030, M5_031_*, M5_033_*, AnimationGroupGateTests). Missing coverage: the f32 widths (M5-011/014),
the loop-exhaust branch with an idle on the stack (M5-008/027), and every engine log key and format.

## 4. Manager rulings (2026-10-11)

1. The 13 EXACT_SOURCE records with defects return to IMPLEMENTATION_GAP (as M3/M4 did).
2. M5-021 and M5-032's libm calls (bionic tanf/cosf/sinf) are unshipped system-library code: EQUIVALENT_IMPLEMENTATION is
   allowed for that part by AGENTS.md "Exact, always". The records stay IMPLEMENTATION_GAP for the rest.
3. M5-022: DriveWheels(0) on Dispose is no approved departure; it is removed.
4. M5-037/038/039 move to IMPLEMENTATION_GAP for the parts read; M5-037's ~TrackLayerComponent and +0xC object stay
   named UNKNOWN in its `unresolved`.
5. Before the build, every LOG row is extracted exactly (level, channel, key, format, arguments, gate, errG) into a
   follow-up rows file, as for M3/M4.
