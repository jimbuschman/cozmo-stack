# X2 — M14-faces extraction (read-only)

Job: re-analysis/jobs/X2.md, scope 2. Subsystem: M14-faces — the face pipeline up to and
around the OKAO boundary, TrackedFace geometry, FaceWorld matching and forgetting, face
enrolment and the album, and the game broadcasts.
Agent: opencode (DeepSeek), window 3. Date: 2026-09-27.

Binary: `resources/lib/armeabi-v7a/libcozmoEngine.so`. Addresses are file VAs, disassembled
with capstone in Thumb mode; branch targets are annotated with the dynamic symbol or PLT
entry they land on.

## 0. Method, and the OKAO boundary

- The Ghidra decompilation is absent in this clone; I worked from the `.so` directly.
- **OKAO is statically linked into `libcozmoEngine.so` and is imported through the PLT.**
  There are exactly **177 `OKAO_*` dynamic symbols** (imports), covering `OKAO_CO_*`
  (common/version), `OKAO_DT_*` (face detection), `OKAO_PT_*` (parts), `OKAO_EX_*`
  (expression), `OKAO_SM_*` (smile), `OKAO_GB_*` (gaze/blink), `OKAO_FR_*` (recognition),
  the memory helpers (`OKAO_Get*Memory*`, `OKAO_Set*MemoryArea`) and the image converters.
  The boundary is therefore a clean list of 177 imported symbols: **every `blx` to an
  `OKAO_*` PLT entry is third-party; everything Anki-side around them is recoverable.**
  This confirms M11-016's "177 OKAO_* exports" and M14-006's Acapela boundary is separate.
- The Anki-side face pipeline is: `VisionComponent::UpdateFaces` -> `FaceWorld` (matching,
  forgetting, broadcast) and `FaceTracker` -> `FaceTracker::Impl::Update` (the OKAO calls)
  -> `TrackedFace`. Enrolment is `BehaviorEnrollFace` -> `FaceWorld::Enroll` ->
  `VisionComponent`/`FaceTracker`/`FaceRecognizer`.

## 1. Production path

| # | step | what the original does | citation | record | class |
|---|---|---|---|---|---|
| F1 | Vision result hand-off | `VisionComponent::UpdateAllResults` dispatches the per-mode handlers; faces are the second handler. | 0x006542EC (`UpdateAllResults`); 0x00654510 `blx 0x4BA488` (`VisionComponent::UpdateFaces`) | NEW | EXACT_SOURCE (entry) |
| F2 | Face handler | `VisionComponent::UpdateFaces(result)` first walks the `UpdatedFaceID` list and calls `FaceWorld::ChangeFaceID` for each. | 0x006551E0; 0x006551E8 `add.w r7,r4,#0x54`; 0x006551F2 `blx 0x4BA5E4 FaceWorld::ChangeFaceID` | NEW | EXACT_SOURCE |
| F3 | Face count / tracker flag | It then walks the `TrackedFace` list, logs the count, and sets a byte at `FaceTracker+0x24` when a face is present. | 0x00655204 `ldr r7,[r4,#0x34]`; 0x00655222 `ldr r0,[r7,#0x14]`; 0x00655260 `ldr r0,[r6]`; 0x00655264 `strb.w r8,[r0,#0x24]` | NEW | EXACT_SOURCE |
| F4 | FaceWorld update | It calls `FaceWorld::Update(trackedFaces)` and warns when it returns non-zero. | 0x00655274 `blx 0x4BA5F0`; 0x0065527A `cbz r4`; 0x0065528C `sWarningF` | NEW | EXACT_SOURCE |
| F5 | FaceWorld::Update | For each tracked face it calls `AddOrUpdateFace`; a non-zero result logs a warning. | 0x004F5316 `add.w r7,r6,#8`; 0x004F531E `blx 0x4A5C5C`; 0x004F5322 `cbz r0`; 0x004F5332 `sWarningF` | M14-001 | EXACT_SOURCE |
| F6 | 15 s forgetting | It then iterates its face map and removes an entry whose last-observed time is more than **15000 ms** behind the last processed image timestamp, when the entry has no ID. | 0x004F5380 `movw r4,#0x3A98` (15000); 0x004F53A4 `ldr.w r0,[sl,#0x1C]`; 0x004F53A8 `adds r1,r0,r4`; 0x004F53AA `cmp r5,r1`; 0x004F53AC `bls`; 0x004F54A8 `blx 0x4A5B24 FaceWorld::RemoveFace(...,bool)` | M14-001 | EXACT_SOURCE |
| F7 | 220 mm match | `FaceWorld::AddOrUpdateFace` compares the new face's position with each known face using a squared-distance threshold **220^2 = 48400** and an overlap score, removing the worse match. | 0x004F4428 `vldr s16,[pc,#0x30C]` -> word 0x473D1000 (48400.0); 0x004F44DA `vcmpe.f32 s0,s16`; 0x004F44E4 `bls`; 0x004F444C `Rectangle<float>::ComputeOverlapScore`; 0x004F4468 `RemoveFaceByID` | M14-001 | EXACT_SOURCE |
| F8 | Face broadcast | On a successful add/update it constructs and broadcasts `RobotObservedFace`. | 0x004F4B7A `blx 0x4A5C14 MessageEngineToGame(RobotObservedFace&&)`; 0x004F4B82 `blx 0x4A5B0C Robot::Broadcast` | NEW | EXACT_SOURCE |
| F9 | Face deletion broadcast | `FaceWorld::RemoveFace` erases the visualization and broadcasts `RobotDeletedFace`. | 0x004F3C2C `blx 0x4A5B00 MessageEngineToGame(RobotDeletedFace&&)`; 0x004F3C34 `Broadcast`; 0x004F3C56 `VizManager::EraseVizObject`; 0x004F3C64 `map::erase` | NEW | EXACT_SOURCE |
| F10 | ShouldReturnFace | `FaceWorld::ShouldReturnFace(entry, time, bool)`: rejects when `entry+8 < time`; when the bool is set also rejects `entry+0 < 1`; then requires `Robot::IsPoseInWorldOrigin(entry+0xF4)`. | 0x004F55B8; 0x004F55B8 `ldr.w ip,[r1,#8]`; 0x004F55BC `cmp ip,r2`; 0x004F55BE `blo`; 0x004F55C0/0x004F55C4; 0x004F55D0 `blx 0x4A5C80` | NEW | EXACT_SOURCE |
| F11 | Enrol entry | `FaceWorld::Enroll(int)` forwards to the `VisionComponent` at `robot+0x258`, passing the id and a mode value (4 when id != 0, else -1). | 0x004F5C9E; 0x004F5CA4 `cmp r2,#0`; 0x004F5CA8 `moveq.w r3,#-1`; 0x004F5CAE `ldr.w r0,[r0,#0x258]`; 0x004F5CB2 `b.w 0x8CAC3C` | NEW | EXACT_SOURCE (tail-call; the callee is a veneer) |
| F12 | FaceTracker entry | `FaceTracker::Update` forwards to `FaceTracker::Impl::Update`; the constructor reads a JSON config. | 0x0086B232 `ldr r0,[r0]`; 0x0086B234 `b.w 0x8D174C`; 0x0086B1E8 (ctor) | NEW | EXACT_SOURCE (entry) |
| F13 | OKAO detection | `FaceTracker::Impl::Update` calls `OKAO_DT_Detect_GRAY`, then `OKAO_DT_GetResultCount` and `OKAO_DT_GetRawResultInfo` per detection. | 0x0086D776 `blx 0x4CF8D8`; 0x0086D7FC `blx 0x4CF8E4`; 0x0086D898/0x0086D958 `blx 0x4CF8F0` | M11-016 | EXACT_SOURCE (boundary) |
| F14 | Square conversion / face build | For each detection it calls `OKAO_CO_ConvertCenterToSquare`, then appends a `TrackedFace`. | 0x0086D998 `blx 0x4CF35C`; 0x0086D964 `list<TrackedFace>::emplace_back` | NEW | EXACT_SOURCE |
| F15 | Parts / expression / smile / gaze | It then calls, in order, `DetectFaceParts`, `EstimateExpression`, `DetectSmile`, `DetectGazeAndBlink`, each wrapped by a profiler Tic/Toc. | 0x0086DA04; 0x0086DA36; 0x0086DAA8; 0x0086DB20 | NEW | EXACT_SOURCE |
| F16 | DetectFaceParts | `OKAO_PT_SetPositionFromHandle`, `OKAO_PT_DetectPoint_GRAY`, `OKAO_PT_GetResult`, `OKAO_PT_GetFaceDirection`; an Anki-side angle constant 0x3C8EFA35. | 0x0086CCC6; 0x0086CD28; 0x0086CD46; 0x0086CF32; 0x0086CF78 `vldr s16,[pc,#0x188]` | NEW | EXACT_SOURCE (calls); the per-point thresholds are not transcribed |
| F17 | EstimateExpression | `OKAO_EX_SetPointFromHandle`, `OKAO_EX_Estimate_GRAY`, `OKAO_EX_GetResult`. | 0x0086D1F8; 0x0086D24C; 0x0086D2A2 | NEW | EXACT_SOURCE (calls) |
| F18 | DetectSmile | `OKAO_SM_SetPointFromHandle`, `OKAO_SM_Estimate`, `OKAO_SM_GetResult`, then Anki-side thresholds 0.001 (0x3A83126F) and 0.01 (0x3C23D70A). | 0x0086D3DA; 0x0086D404; 0x0086D430; 0x0086D482/0x0086D48A | NEW | EXACT_SOURCE (calls); the threshold use is not fully read |
| F19 | DetectGazeAndBlink | `OKAO_GB_SetPointFromHandle`, `OKAO_GB_Estimate`, `OKAO_GB_GetGazeDirection`, `OKAO_GB_GetEyeCloseRatio`, then a 0.001 threshold. | 0x0086D562; 0x0086D58C; 0x0086D5E6; 0x0086D638; 0x0086D666 `vldr s4,[pc,#0xD4]` (0x3A83126F) | NEW | EXACT_SOURCE (calls) |
| F20 | IsEnrollable | `FaceTracker::Impl::IsEnrollable(detection, face)` gates on the intra-eye distance (16, 64, 128, 32 px) and on angles (25, 45, 10 degrees). | 0x0086E074; 0x0086E08A `GetIntraEyeDistance`; 0x0086E0B2 (16.0); 0x0086E0CE (0x42800000 64.0); 0x0086E0DE (0x43000000 128.0); 0x0086E0F4/0x0086E12C (0x42000000 32.0); 0x0086E150 (25.0); 0x0086E16E (0x42340000 45.0); 0x0086E1A4 (10.0) | NEW | EXACT_SOURCE |
| F21 | Min eye distance | `FaceTracker::Impl::GetMinEyeDistanceForEnrollment` returns **16.0** (0x41800000). | 0x0086E36C `mov.w r0,#0x41800000` | NEW | EXACT_SOURCE |
| F22 | Recognition | After the detectors it calls `FaceRecognizer::SetNextFaceToRecognize` and `FaceRecognizer::GetRecognitionData`. | 0x0086DBA6 `blx 0x4CF950`; 0x0086DBC2 `blx 0x4CF95C` | NEW | EXACT_SOURCE (entry) |
| F23 | TrackedFace geometry | `TrackedFace::UpdateTranslation(camera)`: when the face has a recognition value it uses `GetIntraEyeDistance`; otherwise it derives a translation from the bounding box (+0x20..+0x2C) with the factors 0.5, 0.25, -0.125, and clamps the eye distance to a minimum of 6.0. | 0x0087DE24; 0x0087DE3C `ldrb.w r0,[sl,#0x30]`; 0x0087DE52 `GetIntraEyeDistance`; 0x0087DE60/0x0087DE68/0x0087DE74 (0.5/0.25/-0.125); 0x0087DF0A `vmov.f32 s0,#6.0`; 0x0087DF12 `vcmpe`; 0x0087DF1C `vmovmi` | M14-001 (partial) | EXACT_SOURCE |
| F24 | Album read (M3 interface) | `VisionComponent::LoadFaceAlbumFromRobot` reads NV tag **0x184000** (and 0x183000 for the second part) through `NVStorageComponent::Read`; this is the M3-033 connection read. | 0x006512A0; 0x006512EA `mov.w r1,#0x184000`; 0x006512F0 `blx 0x4A83B0 NVStorageComponent::Read`; 0x00651330 `blx 0x4A83B0` (0x183000) | M3-033 (interface) | EXACT_SOURCE (call site) |
| F25 | Album consumer | The read callback calls `VisionSystem::SetSerializedFaceData(..., loadedFaces)` under the vision mutex, then `VisionComponent::BroadcastLoadedNamesAndIDs`. | 0x0065A876 `mutex::lock`; 0x0065A88E `blx 0x4BA9C8 SetSerializedFaceData`; 0x0065A896 `mutex::unlock`; 0x0065A9D4 `blx 0x4BA224 BroadcastLoadedNamesAndIDs` | M3-033 (interface) | EXACT_SOURCE |
| F26 | Album save | `VisionComponent::SaveFaceAlbumToRobot` gets the serialized data (`VisionSystem::GetSerializedFaceData`), asks `NVStorageComponent::GetMaxSizeForEntryTag(0x184000)`, and writes it. | 0x00657144; 0x0065718C `GetSerializedFaceData`; 0x006571DC `mov.w r1,#0x184000`; 0x006571E4 `GetMaxSizeForEntryTag` | NEW | EXACT_SOURCE (call site); the write call is not transcribed |
| F27 | Loaded-names broadcast | `VisionComponent::BroadcastLoadedNamesAndIDs` first broadcasts `RobotErasedAllEnrolledFaces` (to clear the app's state), then one `LoadedKnownFace` per entry. | 0x00651578; 0x00651594 `blx 0x4BA260 MessageEngineToGame(RobotErasedAllEnrolledFaces&&)`; 0x0065159C `Broadcast`; 0x0065160A `blx 0x4BA26C MessageEngineToGame(LoadedKnownFace&&)`; 0x00651612 `Broadcast` | NEW | EXACT_SOURCE |
| F28 | Enrolment completion | `BehaviorEnrollFace::StopInternal` constructs and broadcasts `FaceEnrollmentCompleted`. | 0x005FE2F4 (`StopInternal`); 0x005FE520 `blx 0x4B5E44 MessageEngineToGame(FaceEnrollmentCompleted&&)` | NEW | EXACT_SOURCE |
| F29 | Pets | `VisionComponent::UpdatePets` calls `PetWorld::Update(list<TrackedPet>)`. | 0x00655360 (`UpdatePets`); 0x0050B7F4 (`PetWorld::Update`) | M14-004 (interface) | EXACT_SOURCE (call site) |

## 2. Existing records judged

### Confirmed against the source (keep status)

- **M14-002** (EXACT_SOURCE): confirmed. `IVisuallyVerifyAction` writes `#0xa` at `+0x8C`
  (0x0056873E `movs r1,#0xa` / 0x00568742 `str.w r1,[r4,#0x8C]`) and `TurnTowardsFaceAction`
  writes it at `+0x188` (0x0054B798 `movs r1,#0xa` / 0x0054B79E `str.w r1,[r4,#0x188]`).
- **M14-005** (EXACT_SOURCE): confirmed. `ComputeTurnTowardsImagePointAngles` calls `atan2f`
  twice (0x0051886C, 0x0051888E) and `RobotStateHistory::ComputeStateAt` (0x00518800).
- **M14-006** (BLOCKED_EXTERNAL): unchanged; the Acapela boundary is a separate third-party
  library and is not in this `.so`.
- **M11-016** (BLOCKED_EXTERNAL): the "177 OKAO_* exports" is confirmed exactly (177 imports).
- **M14-001** (EXACT_SOURCE): the 220 mm match (48400 = 220^2 at 0x004F4428) and the 15 s
  forgetting (0x3A98 = 15000 at 0x004F5380) are confirmed, and `TrackedFace::UpdateTranslation`
  exists (F23). The record's evidence is nonetheless weak (below).

### Evidence too weak for the stated status

- **M14-001** (EXACT_SOURCE). `evidence` is two bare strings — "TrackedFace 0x0087DE24" and
  "FaceWorld 220 mm match, 15 s forgetting" — with no instructions. The behaviour is real and
  now cited (F6/F7/F23), but the record as written does not carry its own evidence. Replace
  the evidence with the instructions above.
- **M14-003**, **M14-004**, **M14-007** (all EXACT_SOURCE) are detailed and instruction-level,
  but I did not re-read their whole bodies in this pass; they are not contradicted and should
  be re-verified by the integrator against their cited ranges.

### Partial evidence

- **M14-001** claims "TrackedFace geometry, FaceWorld match and forgetting rules". Its evidence
  proves none of the geometry; the geometry is F23. It also does not cover
  `FaceWorld::ShouldReturnFace` (F10) or the `RobotObservedFace`/`RobotDeletedFace` broadcasts
  (F8/F9), which have no M14 record.

### No existing record

- The whole OKAO-call pipeline (F13–F22) has no M14 record. M11-016 records the boundary as
  BLOCKED_EXTERNAL but does not name the individual calls or the Anki-side thresholds
  (`IsEnrollable`, `GetMinEyeDistanceForEnrollment`, the smile/gaze thresholds).
- The album read/write tags and the consumer chain (F24–F27) are named only as M3-033's
  interface; M14 has no record for the consumer semantics.

## 3. NEW steps (no existing record)

- **N1** (F8/F9): the `RobotObservedFace` and `RobotDeletedFace` broadcasts and their sites.
- **N2** (F10): `FaceWorld::ShouldReturnFace` and its world-origin test.
- **N3** (F11): `FaceWorld::Enroll`'s forward to `VisionComponent` (mode 4 / -1).
- **N4** (F13–F22): the OKAO call order and the Anki-side thresholds in `IsEnrollable`
  (16/64/128/32 px; 25/45/10 degrees) and `GetMinEyeDistanceForEnrollment` (16.0), plus the
  smile/gaze 0.001/0.01 thresholds.
- **N5** (F23): the `TrackedFace` translation factors (0.5/0.25/-0.125) and the 6.0 eye-distance
  floor.
- **N6** (F24–F27): the face album read/write through NV 0x184000/0x183000 and the
  `RobotErasedAllEnrolledFaces`-then-`LoadedKnownFace` broadcast order.
- **N7** (F28): `BehaviorEnrollFace::StopInternal`'s `FaceEnrollmentCompleted` broadcast.

## 4. Open questions for the manager / integrator

1. **M14 has no records for the OKAO pipeline or the album consumer.** The scope asked for
   "the face pipeline up to and around the OKAO boundary" and "face enrolment and the album".
   New records are needed for F13–F27; the integrator should decide which become M14 records
   and which stay M3-033 interfaces.
2. **M14-001's evidence** must be replaced with instructions (F6/F7/F23) or it should be
   downgraded.
3. **The `FaceRecognizer` matching/merge/album semantics** (0x008640E4 `RecognizeFace`,
   0x008638AC `MergeFaces`, 0x00861024 `EnrolledFaceEntry::Serialize`) are not read here; they
   are the largest unread part of M14.
4. **The smile/gaze/expression threshold use** (how the 0.001/0.01 values gate the stored
   expression) is not transcribed.
5. **M14-007's memory-map claims** were not re-read in this pass.

*Read-only extraction. Nothing outside `.scratch/X2/` and this report file was changed.*

