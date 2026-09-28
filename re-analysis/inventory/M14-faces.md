# M14 face and pet pipeline inventory

**State:** prepared for manager approval on 2026-09-28. All source-backed production records are deliberately `IMPLEMENTATION_GAP` until B-M14 compares/builds them. M14-006 remains `BLOCKED_EXTERNAL`; M14-011 remains a live-path `RECOVERABLE_GAP` after three bounded passes.

## Where this comes from

- **Primary source:** `resources/lib/armeabi-v7a/libcozmoEngine.so` 3.4.0-1204, disassembled directly with LIEF and Capstone in Thumb mode.
- **Shipped supporting source:** `resources/lib/armeabi-v7a/libacattsandroid.so` and `re-analysis/obb/sound_meta/PluginInfo.xml` for the third-party text-to-speech boundary.
- **Passes:** X2's M14 extraction (Appendix A), the integration citation check (Appendix B), detector/album gap pass 1 (Appendix C), FaceRecognizer gap pass 2 (Appendix D), and the final recognition residual pass (Appendix E).
- The Ghidra decompilation was used only for navigation. The C# implementation, tests, manifest and earlier notes were claims, never evidence.

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M14-001 | IMPLEMENTATION_GAP | TrackedFace geometry and FaceWorld matching/forgetting: squared distance ≤ 220² plus overlap selection; unnamed faces forgotten after 15000 ms; translation uses recognition eye distance or the box-derived 0.5/0.25/-0.125 construction with a 6.0 floor. | F6, F7, F23 |
| M14-002 | IMPLEMENTATION_GAP | `IVisuallyVerifyAction` and `TurnTowardsFaceAction` both initialise their required observed-frame count to 10. | existing-record check |
| M14-003 | IMPLEMENTATION_GAP | `ITrackAction` runs from `CheckIfDone` each action tick. Constructor tolerances/limits/durations/flags are the cited native values; head and body use separate durations and do not wait for the issued turn. | existing-record check |
| M14-004 | IMPLEMENTATION_GAP | Pet updates enter `PetWorld::Update`; PetInitialDetection keeps already-reacted ids, requires more than two observations, and enforces the 60 s cooldown. | F29, existing-record check |
| M14-005 | IMPLEMENTATION_GAP | TurnTowardsImagePoint subtracts optical centre, uses the image-timestamp historical pose, calculates head/body `atan2` angles, then runs PanAndTiltAction; failure to obtain history turns nowhere. | existing-record check |
| M14-006 | BLOCKED_EXTERNAL | Text-to-speech voice generation remains outside the recoverable engine path: Acapela and Anki Wave Portal are third-party binaries/models. | existing-record check |
| M14-007 | IMPLEMENTATION_GAP | Face interaction's memory-map ray: query 40 mm forward against the eleven-type mask, drive 40 mm at 40 mm/s when clear or back 15 mm when blocked; observable and markerless insertion mappings are as cited. | existing-record check |
| M14-008 | IMPLEMENTATION_GAP | Vision face-result handoff and FaceWorld lifecycle: changed ids first, tracked-face flag, per-face add/update, 15 s forgetting, observed/deleted game broadcasts and deletion cleanup. | F1..F9 |
| M14-009 | IMPLEMENTATION_GAP | Face return/enrollment entry: timestamp/id/world-origin gates in ShouldReturnFace; Enroll selects mode 4 for nonzero id or -1 for zero and forwards to VisionComponent. | F10, F11 |
| M14-010 | IMPLEMENTATION_GAP | FaceTracker/OKAO boundary and Anki-side detector handling: detection/result enumeration, square conversion, parts/expression/smile/gaze order and calls, output validity/scaling, IsEnrollable thresholds, 16 px minimum eye distance, then recognition scheduling. | F12..F22, G1-1..G1-3 |
| M14-011 | RECOVERABLE_GAP | FaceRecognizer matching, registration, update, merge and enrollment-entry semantics. The entry/identify/new-user/lower-ranked merge rules in G2 are settled, but the complete live state machine and helper bodies in G3 are not. | G2-1..G2-6, G3-1..G3-4 |
| M14-012 | IMPLEMENTATION_GAP | Face album interface: read NV 0x184000/0x183000, install under the vision mutex, clear-then-replay loaded-name broadcasts, serialize and size-check both vectors, four-byte padding, ordered write/erase, and FaceEnrollmentCompleted broadcast. | F24..F28, G1-4, G2-6 |

## Decisions

- **SD1 (faithful replacement):** applies only during B-M14 where an external library operation has to be represented. It does not make OKAO or Acapela internals exact. M14-010 records the Anki-side code and explicit OKAO call boundary; M14-006 stays blocked.
- **SD2 (forced compatibility policy):** not applied. No protocol-byte ambiguity is in this inventory.
- **SD3 (dependency ownership):** M3-033 owns the NV wire and M11-016 owns the OKAO third-party boundary. M14-012 owns face-album consumer/producer semantics above the NV interface; M14-010 owns Anki's code around OKAO. M14-004 owns pet reaction semantics while the general reaction framework remains M10/M8.
- **SD4 (exact constants):** applied throughout: 15000 ms, 220², 10 frames, tracker durations/angles, 16/64/128/32 px, 25/45/10 degrees, scale factors 0.01/0.001, recognition cutoffs, NV tags and ordering are recorded exactly rather than rounded or inferred.

## Existing record evidence found contradicted or too weak

- **M14-001:** its two bare evidence strings did not establish geometry or the whole FaceWorld claim. They are replaced by F6/F7/F23 instruction ranges.
- **M14-005:** the first `atan2f` address was mistyped as 0x0054886C; the native call is 0x0051886C.
- **M14-003, M14-004 and M14-007:** X2 did not reread their full bodies. The integration verifier did; their claims passed but still become `IMPLEMENTATION_GAP` under the integration rule.
- **X2 F18/F19:** contradicted on terminology. The 0.01/0.001 values are output scaling factors, not thresholds. G1-1/G1-2 replace those rows.
- **M14-006:** not contradicted and remains `BLOCKED_EXTERNAL`.

## Appendix A: X2 M14-faces extraction report (full text)

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

## Appendix B: I-M14 citation check (full text)

# I-M14 citation check

Role: `cozmo-verifier` (performed by the integration manager). Date: 2026-09-28.
Primary source: `resources/lib/armeabi-v7a/libcozmoEngine.so`, opened directly with LIEF and Capstone in Thumb mode. The Ghidra output was navigation only.

## Existing/status-changing records

| record | verdict | instructions opened |
| --- | --- | --- |
| M14-001 | PASS, evidence replacement required | 0x004F4428 loads 0x473D1000 (48400.0), 0x004F44DA compares squared distance; 0x004F5380 loads 15000 and 0x004F53A4..0x004F53AC compares last-observed+15000; 0x0087DE3C..0x0087DF1C contains the recognition branch, 0.5/0.25/-0.125 construction and 6.0 floor. |
| M14-002 | PASS | 0x0056873E/0x00568742 and 0x0054B798/0x0054B79E store 10. |
| M14-003 | PASS | 0x005646BC..0x00564768 establishes the constructor constants/flags; 0x005650E2..0x00565104 divides the absolute head delta by +0xD0 and calls MoveHeadToAngle with 10000 acceleration. |
| M14-004 | PASS | 0x00611AE4..0x00611CB6 filters known ids and requires count >2; 0x00611DD0..0x00611E14 applies the 60.0 s cooldown; 0x00611E1C..0x00611F28 maintains the reacted-id set and timestamp. |
| M14-005 | PASS with citation typo corrected | The two `atan2f` calls are 0x0051886C and 0x0051888E, after ComputeStateAt at 0x00518800. The old record says 0x0054886C for the first call; that address is a typo. 0x0054B664..0x0054B712 installs the returned pan/tilt angles and enters PanAndTiltAction. |
| M14-006 | PASS as BLOCKED_EXTERNAL | `resources/lib/armeabi-v7a/libacattsandroid.so` is a separate shipped third-party binary; `re-analysis/obb/sound_meta/PluginInfo.xml:6` names the Anki Wave Portal plug-in. The proprietary voice model/plug-in semantics are outside `libcozmoEngine.so`. |
| M14-007 | PASS | 0x005C2420..0x005C24AA constructs the 40 mm ray and negates the memory-map query; 0x005C254E..0x005C258E selects -15 or 40 and speed 40; 0x0068176E..0x006817A4 folds eleven enabled types into a mask; 0x0067F4C0..0x0067F548 maps object families; 0x0067ECFC onward inserts observable objects; 0x00622380..0x0062261A distinguishes markerless object types. |

## X2 production rows sampled

Twenty-nine rows were sampled (more than the required 20). F18 and F19 failed on the word “threshold”; the corrected rows are in gap pass 1. All other listed claims below passed.

| row | verdict | instructions opened |
| --- | --- | --- |
| F1 | PASS | 0x00654504..0x00654514 gates and calls UpdateFaces. |
| F2-F4 | PASS | 0x006551E8..0x0065528C walks changed ids and tracked faces, sets FaceTracker+0x24, calls FaceWorld::Update, warns on nonzero. |
| F5-F6 | PASS | 0x004F5316..0x004F535C calls AddOrUpdateFace per element; 0x004F5380..0x004F53AC performs the unnamed-face 15000 ms expiry test. |
| F7 | PASS | 0x004F4428 and 0x004F44BA..0x004F44E8 compute squared distance and compare to 48400.0; 0x004F444C calls overlap scoring. |
| F8-F9 | PASS | 0x004F4B7A/0x004F4B82 constructs and broadcasts RobotObservedFace; 0x004F3C2C..0x004F3C64 constructs/broadcasts RobotDeletedFace, erases viz, then erases the map node. |
| F10 | PASS | 0x004F55B8..0x004F55E2 implements the timestamp/id gates and world-origin predicate. |
| F11 | PASS | 0x004F5C9E..0x004F5CB2 selects mode 4 or -1 and tail-calls the VisionComponent veneer. |
| F12-F15 | PASS | 0x0086D740..0x0086DBC2 contains detection, result enumeration, square conversion, TrackedFace append, detector order and recognizer calls. |
| F16 | PASS | 0x0086CCC6, 0x0086CD28, 0x0086CD46 and 0x0086CF32 are the four cited OKAO parts calls. |
| F17 | PASS | 0x0086D1F8, 0x0086D24C and 0x0086D2A2 are SetPoint, Estimate and GetResult. |
| F18 | **FAIL** | 0x0086D474..0x0086D4A0 converts OKAO integer outputs, sets validity byte +0xB8, and multiplies the two outputs by 0.01 and 0.001 before storing +0xBC/+0xC0. They are scales, not thresholds or gates. |
| F19 | **FAIL** | 0x0086D604..0x0086D61E stores gaze outputs unscaled when enabled; 0x0086D658..0x0086D680 sets blink validity and multiplies both close ratios by 0.001. Again 0.001 is a scale, not a threshold. |
| F20-F21 | PASS | 0x0086E074..0x0086E1AC contains the stated 16/64/128/32 and 25/45/10 gates; 0x0086E36C returns 0x41800000 (16.0). |
| F22 | PASS | 0x0086DBA6 and 0x0086DBC2 call SetNextFaceToRecognize and GetRecognitionData. |
| F23 | PASS | 0x0087DE3C..0x0087DF1C, as checked for M14-001. |
| F24-F25 | PASS | 0x006512EA/0x00651330 read 0x184000 and 0x183000; 0x0065A876..0x0065A896 locks, installs serialized data, unlocks; the later callback broadcasts loaded names/ids. |
| F26 | PARTIAL, closed in gap pass 1 | 0x0065718C gets serialized data and 0x006571DC..0x0065720E checks both tag capacities. X2 did not transcribe the write calls. |
| F27 | PASS | 0x00651594/0x0065159C broadcasts RobotErasedAllEnrolledFaces first; 0x0065160A/0x00651612 broadcasts each LoadedKnownFace. |
| F28 | PASS | 0x005FE520/0x005FE528 constructs and broadcasts FaceEnrollmentCompleted. |
| F29 | PASS | 0x00655360..0x00655366 forwards the tracked-pet list to PetWorld::Update. |

## Verdict

**FAIL for X2 rows F18 and F19 as written.** The corrected detector-output rows in `20260928-I-M14-gap1-extraction.md` replace them. F26's missing tail is also supplied there. Every other checked row is supported by the cited native instructions.

## Appendix C: I-M14 gap pass 1 (full text)

# I-M14 gap pass 1 — detector outputs and face-album write tail

Role: `cozmo-extractor` (performed by the integration manager). Date: 2026-09-28. Read-only investigation of `libcozmoEngine.so`; Ghidra was navigation only and every result below was checked in the binary with Capstone.

| step | what the original does | citation | record | class |
| --- | --- | --- | --- | --- |
| G1-1 | On successful `OKAO_SM_GetResult`, sets TrackedFace smile-valid (+0xB8) to 1, converts the two signed integer outputs to float, scales the value stored at +0xBC by 0.01 and the value at +0xC0 by 0.001. These are output scale factors, not thresholds. | 0x0086D430 GetResult; 0x0086D474..0x0086D4A0 conversion, validity store, multiplications and stores; literals 0x3C23D70A and 0x3A83126F | NEW detector record | EXACT_SOURCE |
| G1-2 | Gaze and blink are individually enabled by Impl bytes +0x43 and +0x44. Successful gaze output sets +0xC4 and stores the two converted signed integers, unscaled, at +0xC8/+0xCC. Successful eye-close output sets +0xD0 and scales both signed results by 0.001 into +0xD4/+0xD8. | 0x0086D5D0..0x0086D622; 0x0086D622..0x0086D680 | NEW detector record | EXACT_SOURCE |
| G1-3 | Expression gets five signed integer results and, on success, converts each to float and passes it with the five-entry type table to the TrackedFace expression setter. No 0.001/0.01 gate occurs here. | 0x0086D298..0x0086D2FC | NEW detector record | EXACT_SOURCE |
| G1-4 | SaveFaceAlbum obtains two serialized byte vectors under the vision mutex, checks their sizes against NV tags 0x184000 (album) and 0x183000 (enrollment), rounds both vector sizes to a four-byte boundary, then writes album first and enrollment second. It stops before the second write if the first enqueue fails. Empty data uses the corresponding Erase path instead. | 0x00657180..0x0065720E; 0x006573A0..0x006573C0 writes tag 0x184000; 0x006573C2..0x006573EA writes tag 0x183000; 0x0065745A..0x00657474 erases 0x184000 on the empty path (the paired 0x183000 erase follows) | NEW album record | EXACT_SOURCE |

Corrections to X2: F18 and F19's “threshold” wording is rejected and replaced by G1-1/G1-2. F26's untranscribed write tail is closed by G1-4.

Still open for the next pass: `FaceRecognizer::RecognizeFace` (0x008640E4), `MergeFaces` (0x008638AC), and the enrollment-entry helpers they call.

## Appendix D: I-M14 gap pass 2 (full text)

# I-M14 gap pass 2 — FaceRecognizer decision path

Role: `cozmo-extractor` (performed by the integration manager). Date: 2026-09-28. Primary source is `libcozmoEngine.so`, read with Capstone; Ghidra supplied navigation and tentative structure only.

| step | what the original does | citation | record | class |
| --- | --- | --- | --- | --- |
| G2-1 | `RecognizeFace` zeroes both outputs, asks OKAO for registered-user count, and fails on an OKAO error. If enrollment is active, the OKAO album is empty and no enrollment track is set, it registers the first user and returns score 1000 on success. | 0x008640E4 entry; 0x0086410C OKAO_FR_GetRegisteredUserNum; 0x008641AC..0x00864200 RegisterNewUser and output 1000 | NEW recognition record | EXACT_SOURCE for this branch |
| G2-2 | Otherwise it allocates two ten-element integer vectors and asks `OKAO_FR_Identify` for at most ten album-entry ids and scores, with profiler Tic/Toc around the call. More than ten returned results is treated as a loop-bound error. | 0x00864334..0x00864370 | NEW recognition record | EXACT_SOURCE |
| G2-3 | When new-user registration is enabled, there is no enrollment target conflict, and either there is no match or the top score is below 550 (0x226), it registers a new user and reports score 1000. | 0x00864A18..0x00864AA8 (comparison with 0x226 and RegisterNewUser path) | NEW recognition record | EXACT_SOURCE |
| G2-4 | A named lower-ranked match can replace a session-only top match and trigger a merge. The candidate named match must exceed 675 (0x2A3); the continuation threshold is `min(score-75, 600)`. The selected lower-ranked album entry and score become the outputs, then `MergeFaces(namedID, sessionID)` is invoked. | 0x00864756..0x008649F8; 0x00864BDA..0x00864CBA | NEW recognition record | EXACT_SOURCE for the selection/merge call |
| G2-5 | `MergeFaces(keepID, mergeID)` returns success immediately for identical ids; otherwise both enrolled entries must exist. It calls `EnrolledFaceEntry::MergeWith(..., 5, removedEntries)`, remaps retained album entries to keepID, clears removed OKAO users, removes the merged user, and propagates failures. | 0x008638AC..0x00863B9A | NEW recognition record | EXACT_SOURCE for the orchestration |
| G2-6 | `EnrolledFaceEntry::Serialize` converts to the CLAD `EnrolledFaceStorage`, grows the destination vector by the packed size, and packs at the old end. | 0x00861024..0x0086107A | NEW album record | EXACT_SOURCE |

Still open: the behavior-changing bodies of `RegisterNewUser`, `UpdateExistingAlbumEntry`, `EnrolledFaceEntry::MergeWith`, `ConvertToEnrolledFaceStorage`, deserialization/install, and the complete branch-by-branch proof for the middle of `RecognizeFace`. These own live recognition/enrollment behavior and are sent to gap pass 3.

## Appendix E: I-M14 gap pass 3 (full text)

# I-M14 gap pass 3 — recognition/enrollment residual

Role: `cozmo-extractor` (performed by the integration manager). Date: 2026-09-28.

The third bounded pass reopened `FaceRecognizer::RecognizeFace` 0x008640E4..0x00864CD8 and `MergeFaces` 0x008638AC..0x00863B9A in the binary, and followed their direct calls. Gap pass 2's six rows remain supported. The complete recognition/enrollment record cannot be settled without inventing omitted behavior.

| step | what remains | citation / exact next read | record | class |
| --- | --- | --- | --- | --- |
| G3-1 | The complete match/update/register state machine is not instruction-transcribed. The settled entry, 10-result identify, 550 new-user gate, lower-ranked named selection, 675/score-75/600 rule and merge call are only parts of it. | Finish every branch of 0x008640E4..0x00864CD8, especially 0x00864370..0x00864756 and 0x008649F8..0x00864BDA, with the meanings/writers of FaceRecognizer +0xAC, +0xFC, +0x100, +0x104 and +0x108. | NEW recognition record | RECOVERABLE_GAP |
| G3-2 | Registration and updates can change the OKAO album and the Anki enrollment maps; their bodies are not yet transcribed. | Read `FaceRecognizer::RegisterNewUser` 0x00862808, `UpdateExistingAlbumEntry` 0x00862C7C, `GetFaceIDforAlbumEntry` 0x00862B24 and `RemoveUser` 0x008634A4 instruction-by-instruction, including all OKAO calls and error paths. | NEW recognition record | RECOVERABLE_GAP |
| G3-3 | Merge orchestration is known, but the merge policy inside each enrolled entry is not. | Read `EnrolledFaceEntry::MergeWith` 0x008600B4, including its limit argument 5, ordering, evictions and returned removed-entry vector. | NEW recognition record | RECOVERABLE_GAP |
| G3-4 | Serialization's outer pack is known, but the exact field conversion and inverse install are not. | Read `EnrolledFaceEntry::ConvertToEnrolledFaceStorage` 0x00860AE4 and the corresponding deserialize/install callers reached from `VisionSystem::SetSerializedFaceData` 0x00858C74. | NEW recognition/album record | RECOVERABLE_GAP |

After three passes, these are still recoverable from the shipped `.so`; they are not HARDWARE_ONLY or BLOCKED_EXTERNAL. The inventory must keep the recognition/enrollment semantics as a live-path `RECOVERABLE_GAP` and set `source_investigation_exhausted=false`.

