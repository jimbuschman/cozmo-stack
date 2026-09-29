# M14 face and pet pipeline inventory

**State:** prepared for manager approval on 2026-09-28; M14-011 re-approved 2026-09-29 with Correction C3. All source-backed production records are deliberately `IMPLEMENTATION_GAP` until they are built and verified. M14-006 remains `BLOCKED_EXTERNAL`; M14-011's complete FaceRecognizer path is now instruction-checked (Correction C3) and is a live-path `IMPLEMENTATION_GAP` to build.

## Where this comes from

- **Primary source:** `resources/lib/armeabi-v7a/libcozmoEngine.so` 3.4.0-1204, disassembled directly with LIEF and Capstone in Thumb mode.
- **Shipped supporting source:** `resources/lib/armeabi-v7a/libacattsandroid.so` and `re-analysis/obb/sound_meta/PluginInfo.xml` for the third-party text-to-speech boundary.
- **Passes:** X2's M14 extraction (Appendix A), the integration citation check (Appendix B), detector/album gap pass 1 (Appendix C), FaceRecognizer gap pass 2 (Appendix D), and the final recognition residual pass (Appendix E).
- The Ghidra decompilation was used only for navigation. The C# implementation, tests, manifest and earlier notes were claims, never evidence.

## Records

| record | status | what | rows |
| --- | --- | --- | --- |
| M14-001 | EXACT_SOURCE | TrackedFace geometry and FaceWorld matching/forgetting: the reachable match is the map lookup keyed by the tracked-face id (the pose/overlap loop is dead because `IsRecognitionSupported` returns 1, C2-F7); unnamed faces forgotten after 15000 ms; translation uses the parts eye distance or the box-derived 0.5/0.25/-0.125 construction with a 6.0 floor. | F6, F7, F23, C2-F7, C2-F7b, C2-F7c, C2-F23 |
| M14-002 | EXACT_SOURCE | `IVisuallyVerifyAction` and `TurnTowardsFaceAction` both initialise their required observed-frame count to 10. | existing-record check |
| M14-003 | EXACT_SOURCE | `ITrackAction` runs from `CheckIfDone` each action tick (the 60 ms basestation tick, C2-F6). Constructor tolerances/limits/durations/flags are the cited native values; head and body use separate durations and do not wait for the issued turn. | existing-record check, C2-F6 |
| M14-004 | EXACT_SOURCE | Pet updates enter `PetWorld::Update`; PetInitialDetection keeps already-reacted ids (the reachable `UpdateReactedTo`; `InitReactedTo` is uncalled, C2-F4), requires more than two observations, and enforces the 60 s cooldown. | F29, existing-record check, C2-F4 |
| M14-005 | EXACT_SOURCE | TurnTowardsImagePoint subtracts optical centre, uses the image-timestamp historical pose, calculates head/body `atan2` angles, then runs PanAndTiltAction; failure to obtain history turns nowhere. | existing-record check |
| M14-006 | BLOCKED_EXTERNAL | Text-to-speech voice generation remains outside the recoverable engine path: Acapela and Anki Wave Portal are third-party binaries/models. | existing-record check |
| M14-007 | EXACT_SOURCE | Face interaction's memory-map ray: query 40 mm forward against the eleven-type mask, drive 40 mm at 40 mm/s when clear or back 15 mm when blocked; observable and markerless insertion mappings are as cited. | existing-record check |
| M14-008 | IMPLEMENTATION_GAP | Vision face-result handoff and FaceWorld lifecycle: changed ids first, tracked-face flag, per-face add/update, 15 s forgetting, observed/deleted game broadcasts and deletion cleanup. | F1..F9 |
| M14-009 | IMPLEMENTATION_GAP | Face return/enrollment entry: timestamp/id/world-origin gates in ShouldReturnFace; Enroll selects mode 4 for nonzero id or -1 for zero and forwards to VisionComponent. | F10, F11 |
| M14-010 | IMPLEMENTATION_GAP | FaceTracker/OKAO boundary and Anki-side detector handling: detection/result enumeration, square conversion, parts/expression/smile/gaze order and calls, output validity/scaling, IsEnrollable thresholds, 16 px minimum eye distance, then recognition scheduling. | F12..F22, G1-1..G1-3 |
| M14-011 | IMPLEMENTATION_GAP | FaceRecognizer matching, registration, update, merge, album and serialization semantics. Every branch is now instruction-checked in the `.so` (Correction C3, 24 rows plus callers): RecognizeFace's confident/low-confidence/target/debug/lower-ranked paths, RegisterNewUser, GetNextAlbumEntryToUse, GetFaceIDforAlbumEntry, UpdateExistingAlbumEntry's four-entry enrollment replacement, both RemoveUser overloads, MergeFaces/MergeWith, ConvertToEnrolledFaceStorage and the inverse, enrollment-data deserialize, album restore/consistency/capacity install, and the SetSerializedFaceData chain. The OKAO_FR_* calls are the third-party boundary (M11-016); `EnrolledFaceEntry +0x04`'s source name is UNKNOWN (its merge rule is exact, C3-18). | G2-1..G2-6, G3-1..G3-4, C3-1..C3-24 |
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

## Correction C1: B-M14 extraction of the unsettled paths (2026-09-28)

The B-M14 implementer could not settle M14-001 (F7/F23), M14-008 (F2/F3) and M14-009 (F11) from the rows as first written. A bounded extractor pass reopened `FaceWorld::AddOrUpdateFace` 0x004F4278, `TrackedFace::UpdateTranslation` 0x0087DE24, `TrackedFace::GetIntraEyeDistance` 0x0087DC68, `VisionComponent::UpdateFaces` 0x006551E0, `FaceTracker::Impl::Update` 0x0086D740 and `FaceWorld::Enroll` 0x004F5C9E in the binary with Capstone. All four paths are Anki-side and settle exactly; the report is `.scratch/B-M14/extraction.md`. The following rows correct or extend Appendix A.

| step | what the original does (instruction-level) | citation | record | class |
| --- | --- | --- | --- | --- |
| C1-F7 | The pose-matching loop runs only when `FaceTracker::IsRecognitionSupported()` returns 0. Per known entry it computes the rectangle overlap of the new face box (`param_1+0x20`) against the entry box (`node+0x34` = entry+0x20). If overlap `> 0.5` (the running best, initialised to 0.5) the face is selected and the best overlap advances. Otherwise it computes the **3D** squared distance between the new pose translation and the entry pose translation (transform +0x20,+0x24,+0x28) and selects the face when it is `<= 48400.0` (220^2). Whenever a new candidate is chosen while an earlier one exists, the **earlier** candidate is removed with `RemoveFaceByID` before the candidate pointer is replaced. The distance branch does not compare candidates. | `IsRecognitionSupported` 0x004F43D6 `blx 0x4A5B90`; `s18=0.5` 0x004F441A; `s16=48400.0` 0x004F4428 (word 0x473D1000); overlap 0x004F444C; compare 0x004F4454/0x004F445C; overlap select+remove 0x004F445E..0x004F4478 (remove 0x004F4468); 3D delta 0x004F447A..0x004F44B8; squared sum 0x004F44BA..0x004F44D8; compare 0x004F44DA/0x004F44E4; distance select+remove 0x004F44EA..0x004F44FE (remove 0x004F44F6) | M14-001 | EXACT_SOURCE |
| C1-F23 | The byte at `this+0x30` is not a recognition value: it is set to 1 by `FaceTracker::Impl::DetectFaceParts` on a successful parts/eye detection (Anki-side). Nonzero: load the four eye-centre floats at `this+0x34..+0x40`, call `GetIntraEyeDistance`. `GetIntraEyeDistance` returns `sqrt((x1-x2)^2+(y1-y2)^2) / |cos(this+0xec)|` (roll correction), falling back to 6.0 with a warning when the cosine or distance is below 1e-5. Zero: build two eye points from the box fields +0x20(x),+0x24(y),+0x28(w),+0x2c(h) with 0.5/0.25/-0.125 - A=(x+0.25w, y+0.375h), B=(x+0.75w, y+0.375h) - so the eye distance is `|0.5*w|`, floored at 6.0. Common tail: midpoint = half the sum of the eye centres (or (0,0) in the box branch), ray base (mx,my,1.0), `GetInvCalibrationMatrix`, `MakeUnitLength`, then scale by `(CameraCalibration+4) * 62.0 / eyeDistance` (0x42780000 = 62.0, calibration+4 = x focal length), write the result into the face pose transform +0x20..+0x28, and `SetParent(this+0xf4, Camera+0xc)`. | selector 0x0087DE3C/0x0087DE40; parts branch 0x0087DE42..0x0087DE5E (`ldm` from +0x34 at 0x0087DE46, `blx 0x4CF974` at 0x0087DE52); writer 0x0086CDC8 (`strb.w r0,[sb,#0x30]`); `GetIntraEyeDistance` 0x0087DC68..0x0087DD8A (1e-5 literal 0x3727C5AC at 0x0087DCDE, 6.0 fallback 0x0087DD86); box branch 0x0087DE60..0x0087DEF0; floor 0x0087DF0A/0x0087DF12/0x0087DF1C; common tail 0x0087DF20..0x0087E068 (62.0 at 0x0087E014, `[calibration+4]` at 0x0087E01A, div 0x0087E022, `SetParent` 0x0087E064) | M14-001 | EXACT_SOURCE |
| C1-F3 | The flag byte is **`FaceWorld+0x24`**, not `FaceTracker+0x24`. `VisionComponent::UpdateFaces` sets it to 1 when a tracked-face node has `[node+0x14] > 0` (reached enrollment count): `r0 = [VisionComponent+0x14]` (Robot), `r0 = [Robot+0x38]` (FaceWorld), `strb 1,[r0+0x24]`. It is read by `BehaviorEnrollFace::UpdateInternal` in state 3 (Enrolling). Anki-side, not OKAO-internal. | write 0x00655260 `ldr r0,[r6]`, 0x00655262 `ldr r0,[r0,#0x38]`, 0x00655264 `strb.w r8,[r0,#0x24]`; gate 0x0065521E..0x00655226; read 0x005FD9AE..0x005FD9B6 | M14-008 | EXACT_SOURCE |
| C1-F2 | The `UpdatedFaceID` list is `std::list<UpdatedFaceID>` at `VisionProcessingResult+0x54` (sentinel +0x54, first node +0x58); the value is `{int oldID (+0), int newID (+4), std::string name (+8)}`. `UpdateFaces` walks it first, before the tracked-face flag loop and before `FaceWorld::Update`, calling `FaceWorld::ChangeFaceID` (real address 0x004F3DB4; the row's 0x004BA5E4 is the PLT stub). The list is produced in `FaceTracker::Impl::Update` (0x0086D740) after `FaceRecognizer::GetRecognitionData` (0x0086DBC2) and passed from `VisionSystem::DetectFaces` as `VisionSystem+0x3cc`; it is therefore M14-011's recogniser output carried in the Anki-side vision result. The old/new labelling inside the recogniser is M14-011's G3-1. | walk 0x006551E8 `add.w r7,r4,#0x54`, 0x006551EC `ldr r6,[r4,#0x58]`, loop 0x006551F2..0x00655202, 0x006551FA `blx 0x4BA5E4`; `ChangeFaceID` body 0x004F3DB4; production 0x006B3614 `add.w r3,r5,#0x3cc`, 0x006B361A `blx 0x4BF0CC`; pushes 0x0086DC02 and 0x0086DCC4 | M14-008 | EXACT_SOURCE |
| C1-F11 | The tail-call 0x8CAC3C (Thumb->ARM veneer) lands on PLT 0x4A5D04, i.e. `VisionComponent::SetFaceEnrollmentMode(pose=0, id, mode)`. The chain is VisionComponent 0x00657A58 -> VisionSystem 0x006B3394 -> FaceTracker 0x0086B276 -> FaceTracker::Impl 0x0086E372 -> `FaceRecognizer::SetAllowedEnrollments` 0x008658AC. `SetAllowedEnrollments(mode, id)`: when id==0, cancel (if the current enrollment id `+0x100` is nonzero and `+0x108 > 0`, log and set `+0x5D = 1`) then `+0x108 = mode`, `+0x10c = mode`, `+0x100 = 0`, `+0x104 = 0`; otherwise `+0x108 = mode`, `+0x10c = mode`, `+0x100 = id`, and `+0x104` = the enrolled entry's `+0x20` (or 0 with a "No data for enrollmentID=%d" warning). | 0x004F5C9E..0x004F5CB2; veneer 0x8CAC3C; 0x00657A58; 0x006B3394; 0x0086B276; 0x0086E372; 0x008658AC..0x0086596C (cancel branch 0x008658D8..0x00865932, stores 0x008658B6/0x008658C2, warning 0x00865934) | M14-009 | EXACT_SOURCE |

Corrections to Appendix A: F3's `FaceTracker+0x24` is contradicted and replaced by C1-F3; F7's "removing the worse match" and its 2D wording are replaced by C1-F7; F23's "recognition value" wording and the missing `GetIntraEyeDistance`/62.0-factor detail are replaced by C1-F23; F2 is extended with the `UpdatedFaceID` layout and production site (C1-F2); F11 is extended with the resolved enrollment chain (C1-F11). M14-011's G3-1 remains the owner of the recogniser-side old/new labelling.

## Correction C2: reachable paths after the B-M14 verifier pass (2026-09-28)

The B-M14 verifier found that C1-F7 described a branch the shipped binary can never reach. A second bounded extractor pass reopened `FaceTracker::IsRecognitionSupported` 0x0086B244, the reachable `FaceWorld::AddOrUpdateFace` path 0x004F4278..0x004F47D8, `ReactionTriggerStrategyPetInitialDetection::InitReactedTo` 0x00611FA4, `UpdateReactedTo` 0x00611E1C and the action-list tick chain; its report is `.scratch/B-M14/extraction-c2.md`. These rows correct C1 and Appendix A.

| step | what the original does (instruction-level) | citation | record | class |
| --- | --- | --- | --- | --- |
| C2-F7 | `FaceTracker::IsRecognitionSupported` 0x0086B244 is `movs r0,#1; bx lr` (bytes 01 20 70 47): it returns 1 unconditionally, has no inputs or memory reads, and its only in-DSO caller is `AddOrUpdateFace` at 0x004F43D6 (`blx 0x4A5B90`), where `cbz r0, 0x004F440C` is therefore never taken. The pose/overlap loop 0x004F440C..0x004F4536 is dead, and with it the `FaceWorld+0x10` generated-id path 0x004F4608 and the `Did not find a match by pose, but ID %d already in use` return 1 at 0x004F4712. The reachable match is the `std::map<int,FaceEntry>` at `FaceWorld+4`, searched by `lower_bound` on the **`TrackedFace` id at +0** (0x004F43EC..0x004F4556); a found id takes the update path, an absent one creates a new entry keyed by that same id (0x004F45EA/0x004F45FC) after the `WasRotatingTooFast` gate 0x004F4596. No generated/session id exists on the reachable path. The 220^2 (0x004F4428) and overlap-0.5 instructions remain in the binary but are on the dead branch. | 0x0086B244 `01 20`/`70 47`; only caller 0x004F43D6; dead loop 0x004F440C..0x004F4536; map search 0x004F43DA..0x004F4556; new-entry key 0x004F45EA/0x004F45FC; dead id path 0x004F4608/0x004F4712 | M14-001 | EXACT_SOURCE |
| C2-F7b | Reachable timestamp handling: on the found path, if the new timestamp (`TrackedFace+8`) is `<=` the entry timestamp (`FaceEntry+8`, at node+0x1c) it logs `Face observed before previous observation (%u <= %u)` (0x00BE4BED) at 0x004F473C, sets the delta to 0 and **continues** the update; it does not reject. The below-robot gate (`FaceWorld+0x355 == 0` and posed z < 0) runs before the guard and returns 0. Normal add/update returns 0; only a failed/absent `RobotStateHistory::ComputeAndInsertStateAt` returns nonzero. | 0x004F42F6..0x004F4314 below-robot; 0x004F455C..0x004F4564 timestamp compare; 0x004F473C warning; 0x004F477A `mov.w sl,#0`; 0x004F42B4..0x004F42BC/0x004F4368 return values | M14-001 | EXACT_SOURCE |
| C2-F7c | The `WasRotatingTooFast` gate 0x004F4596 is on the **new-entry path only** (`LAB_004f456e`); the found path 0x004F4558..0x004F456C is not gated. On the found path, when the observation has no parts (`TrackedFace+0x30 == 0`) the local pose's translation is replaced by the **entry's existing translation** (`entry+0x108` → local+0x20, 0x004F4784..0x004F47A8) before the local pose is written back to `entry+0xf4` (0x004F47BE); the rotation stays from the TrackedFace pose. So a no-parts observation of a known face keeps its previous translation. | new-entry gate 0x004F456E..0x004F459E (`blx WasRotatingTooFast` 0x004F4596); found path 0x004F4558..0x004F456C; no-parts translation copy 0x004F477E `ldrb r0,[sb,#0x30]` / 0x004F4782 `cbnz r0,#0x4F47AA` / 0x004F4784..0x004F47A8; pose write 0x004F47BE | M14-001 | EXACT_SOURCE |
| C2-F23 | `GetIntraEyeDistance` 0x0087DC68: `dist = sqrt((eye0.x-eye1.x)^2 + (eye0.y-eye1.y)^2)`; `c = cos(this+0xec)`; the divisor is `1.0` when `|c| < 1e-5` and `c` otherwise (the `|cos|` is only the threshold test; the divide at 0x0087DD8A uses the signed `s16`); when `dist < 1e-5` it warns and returns `6.0 / divisor`, otherwise `dist / divisor`. The box-branch midpoint is `(0,0)`: the eye slots `sp+0x58`/`sp+0x50` are zeroed at entry and written only in the parts branch, and the common tail halves their sum. The stack's `CameraModel.Ray` subtracts the principal point, so the faithful mapping of the engine's `invK*(0,0,1)` is `Ray(pixel (0,0))`, **not** `Ray(principal point)`. The 62.0 factor is `CameraCalibration+4` (x focal length) times 62.0 over the eye distance; the pose is parented to `Camera+0xc`. | 0x0087DC68..0x0087DD8E (dist 0x0087DC9C..0x0087DCB0, cos 0x0087DCD0..0x0087DCDA, threshold/divisor 0x0087DD08..0x0087DD20, divide 0x0087DD8A); box slots 0x0087DE32/0x0087DE38; midpoint 0x0087DF20..0x0087DF60; 62.0 0x0087E014; `[calibration+4]` 0x0087E01A; `SetParent` 0x0087E064 | M14-001 | EXACT_SOURCE |
| C2-F4 | `InitReactedTo` 0x00611FA4 clears the set at `this+0x34` and re-fills it with every current `PetWorld` pet id, but it has **no caller in `libcozmoEngine.so`** (no BL/BLX in `.text`, no relocation, no vtable/pointer slot): UNKNOWN, not called by `EnabledStateChanged`. The reachable set maintenance is `UpdateReactedTo` 0x00611E1C, called from `ShouldTriggerBehaviorInternal` 0x00611AE4 and `BehaviorDidReact` 0x00611E80. The strategy's reachable behaviour is the already-reacted set, the `>2` observation gate and the 60 s cooldown. | `InitReactedTo` 0x00611FA4; no caller (whole-`.text` scan); `UpdateReactedTo` 0x00611E1C (walk `robot+0x3c` = PetWorld, insert `node+0x10`); callers 0x00611AE4/0x00611E80 | M14-004 | EXACT_SOURCE |
| C2-F6 | The action list calls an action's `CheckIfDone` once per basestation tick, and the basestation tick is **60 ms** (`0x03938700` ns), not 33 ms. Chain: `CozmoInstanceRunner::Run` 0x0065B3D2/0x0065B3D8 -> `CozmoInstanceRunner::Update` 0x0065B420 -> `CozmoEngine::Update` 0x0065BB16 -> `RobotManager::UpdateAllRobots` 0x004ED648 -> `Robot::Update` 0x0052F6E4 -> `ActionList::Update` 0x005140BC -> `ActionQueue::Update` 0x0053F598 -> `IActionRunner::Update` 0x0053F628 -> `IAction::UpdateInternal` 0x00540592 -> `ITrackAction::CheckIfDone` (vtable slot 0x01022F1C). The 33 ms figure is the M5 face keep-alive cadence and is not an action tick. | 0x0065B3D2/0x0065B3D8; 0x0065B420; 0x0065BB16; 0x004ED648; 0x0052F6E4; 0x005140BC; 0x0053F598; 0x0053F628; 0x00540592; 0x01022F1C | M14-003 | EXACT_SOURCE |

Corrections to C1/Appendix A: C1-F7's "runs only when IsRecognitionSupported returns 0" is replaced by C2-F7 (the loop is dead; match by id); C1-F23's "|cos|" divisor is corrected by C2-F23 (the divide uses the signed cosine; `|cos|` is only the threshold) and the box midpoint is pixel (0,0); M14-003's cadence is 60 ms, not 33 ms; M14-004's `InitReactedTo` is an uncalled function, not part of the reachable path.

## Correction C3: M14-011 checked against the shipped binary (2026-09-29)

G-M14 checked Codex's independent extraction `re-analysis/research/20260929-M14-011-face-recognizer-extraction.md`
against the `.so`. `cozmo-verifier` opened all 24 extraction rows and all 8 caller rows in
`resources/lib/armeabi-v7a/libcozmoEngine.so`; 6 rows failed and `cozmo-extractor` corrected them, after which
all 6 re-verified PASS. The checked rows are in `re-analysis/research/20260929-G-M14-citation-check.md`.

Address correction (confirmed): the old M14-011 unresolved text named `0x00862808` for `RegisterNewUser`,
`0x00862B24` for `GetFaceIDforAlbumEntry`, `0x00862C7C` for `UpdateExistingAlbumEntry`, `0x008634A4` for
`RemoveUser`, `0x008600B4` for `MergeWith`, `0x00860AE4` for `ConvertToEnrolledFaceStorage` and `0x00858C74`
for `SetSerializedFaceData`. Those are wrong (`0x00862808` is inside `~FaceRecognizer()`); the corrected
symbol-backed bodies are `RegisterNewUser` 0x00865658..0x008657A8, `GetFaceIDforAlbumEntry`
0x00866C1C..0x00866CAC, `UpdateExistingAlbumEntry` 0x00865A98..0x0086600E, `RemoveUser(int)`
0x00865544..0x008655C6, `RemoveUser(iterator)` 0x00866AE0..0x00866B9A, `MergeWith` 0x008616AE..0x0086173A,
`ConvertToEnrolledFaceStorage` 0x00860D7C..0x00860EE0 and `VisionSystem::SetSerializedFaceData`
0x006B9D82..0x006B9D8C.

Three rows were corrected in content, not just citation: C3-2 (an `OKAO_FR_Identify` error returns 0, not 1),
C3-3 (the confident gate lives at 0x00864338..0x008644EC), C3-23 (equal OKAO capacities swap; only strictly
smaller capacities copy). The caller rows for `RegisterNewUser`, `GetFaceIDforAlbumEntry` and
`UpdateExistingAlbumEntry` were corrected to the actual call instructions.

The build boundary: the OKAO_FR_* operations (GetRegisteredUserNum, Identify, RegisterData,
GetRegisteredUsrDataNum, ClearUser, Verify, GetFeatureFromAlbum, ClearData, IsRegistered, RestoreAlbum,
GetAlbumMaxNum, ClearAlbum, DeleteAlbumHandle) are the third-party boundary, like M14-010's detector seam.
The C# keeps the existing `IFaceDetector` pattern: an OKAO face-recognizer seam whose stock implementation
reports unavailable, and the Anki-side state machine above it is what these rows establish.

| step | what the original does | citation | record | class |
| --- | --- | --- | --- | --- |
| C3-1 | `RecognizeFace` initializes `faceID=0` and `score=0`. Failure of `OKAO_FR_GetRegisteredUserNum(album,&count)` returns 1. The empty-album first-user branch calls `RegisterNewUser`, reports score 1000 on success, and otherwise propagates failure. | 0x008640E4..0x00864252 | M14-011 | EXACT_SOURCE |
| C3-2 | `OKAO_FR_Identify(feature,album,10,ids,scores,&count)` is bounded to ten results (`0x00864220 movs r2,#0xa`). An OKAO error (`0x00864298 cmp r7,#0`; `0x0086429A beq 0x00864338` is the success branch) is logged and the function returns 0 (error path ends `0x008642E6 movs r7,#0`, returned by `0x00864330 mov r0,r7`). A returned count above ten (`0x00864236 cmp r3,#0xb`) raises the loop-bound diagnostic and clamps the count to ten (`0x00864294 movs r3,#0xa`). | 0x00864254..0x00864392 | M14-011 | EXACT_SOURCE |
| C3-3 | A confident match requires at least one result (`0x00864338 cmp r3,#1`) and a top score strictly greater than 750 (`0x0086433E` loads `0x2EF`; `0x00864344 cmp r0,r1`). It maps the top album entry to a face ID (`0x0086434E blx GetFaceIDforAlbumEntry`). A zero/missing ID (`0x00864356 beq 0x008644AC`) or absent enrolled-entry record is an error and returns 1 (`0x008644EC movs r7,#1`). | 0x00864338..0x008644EC | M14-011 | EXACT_SOURCE |
| C3-4 | For a valid top match, debug-match data is installed on that enrolled entry: the top match plus unique later face IDs, no more than ten input results (`0x00864676 cmp r6,#0xA`) and no more than two stored debug matches (`0x008645AC cmp r0,#2`). Missing enrollment data for a later debug candidate contributes an empty name but does not fail recognition. | 0x008644F4..0x008646FC | M14-011 | EXACT_SOURCE |
| C3-5 | If the top face is session-only (empty name), there is more than one result, and it is not the active enrollment target, later candidates are examined. The first named candidate over 675 (`0x008647D6 cmp r0,#0x2A4`) starts the lower-ranked-selection path; candidates after it are considered only while their score remains above `min(candidateScore-75,600)` (`0x008647E4 subs r0,#0x4B`; `0x008647E8 cmp r0,#0x258`; `0x008647EE movge r0,#0x258`). A missing later enrolled-entry record logs and stops/continues according to the branch, rather than synthesizing an entry. | 0x008646FC..0x008649F8 | M14-011 | EXACT_SOURCE |
| C3-6 | On the ordinary confident-match path, the selected face ID and score are output and `UpdateExistingAlbumEntry(selectedAlbumEntry, currentFeature)` is called (`0x0086493C`). Update failure is logged but recognition still returns success. If the selected face is the enrollment target and its saved enrollment track differs from the current track, the saved track is replaced with the current track (`0x00864992 ldr r6,[sl,#0xAC]`, `0x00864998 ldr r5,[sl,#0x104]`, `0x008649EE str r0,[sl,#0x104]`). | 0x00864926..0x008649F6; 0x008649EA..0x008649EE | M14-011 | EXACT_SOURCE |
| C3-7 | Below the confident gate, recognition returns success without a face unless enrollment is enabled (`+0x108 != 0`, `0x00864394 cmp r5,#0`) and per-frame registration is allowed (`+0xFC != 0`, `0x0086439A ldrb [sl,#0xFC]`). If no named enrollment target is active for the current track, and no enrollment target ID is set, a new user is added only when there are no results or the top score is below 550 (`0x008643F0 movw r2,#0x225`); success outputs score 1000 (`0x0086444E mov.w r0,#0x3E8`). | 0x00864394..0x00864454; 0x00864A18..0x00864AA8 | M14-011 | EXACT_SOURCE |
| C3-8 | When a named enrollment target (`+0x100`) is active and current track `+0xAC` equals enrollment track `+0x104`, output score starts at zero. The result list is searched for that face ID; if present its score is output and its album entry is updated (`0x00864506 UpdateExistingAlbumEntry`). Whether found or not, output face ID is forced to the enrollment target. Update failure returns 1 (`0x0086450E bne 0x8642E8`); otherwise this path returns success. | 0x008643A2..0x00864562 | M14-011 | EXACT_SOURCE |
| C3-9 | `RegisterNewUser` obtains an album entry from `GetNextAlbumEntryToUse` and a face ID from `GetNextFaceID`. A negative album entry returns 1. Otherwise it calls `OKAO_FR_RegisterData(album,feature,albumEntry,0)`; failure returns 1. Success creates an `EnrolledFaceEntry(faceID,now)`, sets its current track to recognizer `+0xAC`, adds the album entry with `isSessionOnly=true`, maps album-entry to face-ID, inserts the face record, decrements the remaining enrollment count `+0x108` if positive, and returns 0. | 0x00865658..0x008657A8 | M14-011 | EXACT_SOURCE |
| C3-10 | Album-entry allocation is limited to 1000 registered users/indices. It probes for a free index, wrapping after 999. If the album is full it selects the oldest-updated session-only face, removes it, reuses its current album entry, and verifies it was cleared. With no replaceable session-only face, or on the enumerated OKAO errors, it returns -1. | 0x00865178..0x008653D8 | M14-011 | EXACT_SOURCE |
| C3-11 | `GetFaceIDforAlbumEntry` performs a lookup in the album-entry-to-face-ID map at recognizer `+0xE8`; a hit returns the mapped face ID. A miss logs/debug-breaks and returns 0. | 0x00866C1C..0x00866CAC | M14-011 | EXACT_SOURCE |
| C3-12 | `UpdateExistingAlbumEntry` maps album entry to face ID, then face ID to its enrolled entry, updates that album entry's last-seen timestamp to `system_clock::now`, and changes current/previous track IDs when recognizer `+0xAC` differs. It asks `OKAO_FR_GetRegisteredUsrDataNum`; failure returns 1. | 0x00865A98..0x00865C04 | M14-011 | EXACT_SOURCE |
| C3-13 | Adding enrollment data is enabled only when the remaining enrollment count is nonzero and the configured target is zero or this face ID. It additionally requires at least one second since this album entry's previous update (comparison against 999,999 microseconds) and either fewer than four data entries, an unnamed face, or an explicitly targeted enrollment. Otherwise it makes no album mutation and returns 0. | 0x00865C04..0x00865D7C (constant `0x000F423F`) | M14-011 | EXACT_SOURCE |
| C3-14 | With fewer than four OKAO data entries it registers the current feature at index `count`. With four entries, replacement is permitted only for an unnamed face or explicit target: it verifies the new feature against the full user, temporarily removes each of the four existing features in turn, verifies that held-out feature against the remaining three, restores it, and replaces the held-out index having the lowest score only if that score is lower than the new feature's full-user score. Every OKAO get/clear/verify/re-register/register failure returns 1. | 0x00865CA6..0x00865EB6 | M14-011 | EXACT_SOURCE |
| C3-15 | After a successful add/replacement (and also the full/no-replacement continuation), it updates the entry's album-entry timestamp/session flag. The session flag passed is `configuredEnrollmentTarget != faceID`. If enrollment count is positive it is decremented. Success returns 0. | 0x00865EB6..0x00865EEA | M14-011 | EXACT_SOURCE |
| C3-16 | `RemoveUser(faceID)` looks up the enrolled-face map. A hit delegates to iterator removal and returns 0. A missing face logs "UserDoesNotExist" but also returns 0. Iterator removal erases the track mapping, then for every owned album entry erases the album-entry-to-face-ID mapping and calls `OKAO_FR_ClearUser(album,entry)`; clear errors are logged but do not stop removal. Finally it erases the enrolled-face node. | 0x00865544..0x008655C6; 0x00866AE0..0x00866B9A | M14-011 | EXACT_SOURCE |
| C3-17 | `MergeWith(other,limit,removed)` first merges album entries. Each entry is keyed with its last-seen time. There is at most one session-only/current entry: the newer of the two is kept. If the combined non-session entries fit within `limit-1`, all are retained; otherwise non-session entries are ordered by recency and only the newest `limit-1` are retained. Removed entries belonging to `this` are appended to `removed`; entries discarded only from `other` need no clearing from `this`. With M14's caller-supplied limit 5, the result is one session-only entry plus at most four other album entries. | 0x0086173C..0x00861C10; caller passes 5 at 0x008639B8..0x008639C2 | M14-011 | EXACT_SOURCE |
| C3-18 | The scalar merge then copies `other.+4` only when `other.+4 == other.faceID`; copies the other name only if this name is empty; stores the earlier of the two `+0x30` timestamps, the later of the two `+0x28` timestamps, and the maximum `+0x1C` score. It returns 0. The semantic name of field `+4` is not established by these instructions. | 0x008616AE..0x0086173A | M14-011 | EXACT_SOURCE (operation); UNKNOWN (name of `+4`) |
| C3-19 | `ConvertToEnrolledFaceStorage` creates the stored object as: bytes +0..+7 = entry `+0x28` timestamp divided by 1,000,000; +8..+15 = entry `+0x30` timestamp divided by 1,000,000; +0x10 = vector of per-album-entry timestamps in seconds; +0x1C = face ID; +0x20 = parallel vector of album-entry IDs; +0x2C = name. The current/session album entry is emitted first (timestamp zero if -1), followed by every other entry from the map. | 0x00860D7C..0x00860EE0 (`0x000F4240` divisor) | M14-011 | EXACT_SOURCE |
| C3-20 | The inverse `EnrolledFaceEntry(EnrolledFaceStorage)` restores face ID/name, initializes score to 1000, multiplies the two stored seconds values and every parallel last-seen value by 1,000,000, makes the first album ID the current/session entry, and inserts each non--1 album ID into the last-seen map. A vector-length mismatch is warned and no entries are installed; restored future times are clipped to now. | 0x00860728..0x0086091A | M14-011 | EXACT_SOURCE |
| C3-21 | Enrollment-data install rejects fewer than eight bytes, rejects a version prefix other than `0x0002FACE`, reads nextFaceID from bytes 4..7, and repeatedly deserializes packed `EnrolledFaceStorage` records. Deserialization rejects empty/end indices, insufficient bytes, or an unpack count shorter than `Size()`. A malformed record aborts the entire enrollment-data load with return 1. | 0x008611B4..0x008613D8; 0x008684D4..0x0086871C | M14-011 | EXACT_SOURCE |
| C3-22 | Album install rejects a null OKAO common handle and empty album bytes, calls `OKAO_FR_RestoreAlbum(common,data,size,&result)`, and then requires successful OKAO registered-user/data counts. The loaded album and enrollment map must agree: registered-user count equals album-entry LUT size, every LUT entry is registered, every enrollment-owned album entry exists in the LUT, and it maps back to the owning face ID. | 0x00867384..0x008674E2; 0x00863064..0x0086329C | M14-011 | EXACT_SOURCE |
| C3-23 | Install also compares loaded and current OKAO capacities. A loaded album larger than current capacity is rejected. Equal capacities take the swap path (`0x008689F4 cmp r3,r1; it eq; cmpeq lr,ip; bne 0x868AE8`; equality falls through to the "Stop using %p, start using %p" swap at `0x008689FC`). Only strictly smaller capacities are copied entry-by-entry after clearing the current album (`0x00868B2A OKAO_FR_ClearAlbum`), using `OKAO_FR_IsRegistered` (`0x00868BC0`), `GetFeatureFromAlbum` (`0x00868BD4`), and `RegisterData` (`0x00868BE4`); any failure rejects the load. Only after all checks/copying succeed are the enrolled-face map and album-entry LUT installed and the track LUT cleared. | 0x00868874..0x00868C5E | M14-011 | EXACT_SOURCE |
| C3-24 | The public install path is `VisionSystem::SetSerializedFaceData` -> `FaceTracker::SetSerializedData` -> `FaceTracker::Impl::SetSerializedData` -> `FaceRecognizer::SetSerializedData`; that routine performs album restore, enrollment parsing, consistency/capacity install, sets nextFaceID, reports loaded faces, and deletes any temporary album handle. | 0x006B9D82..0x006B9D8C; 0x0086B2A2..0x0086B2AA; 0x0086E388..0x0086E392; 0x00867F54..0x00868366 (calls at 0x00867F74, 0x00867FFE, 0x008680AA) | M14-011 | EXACT_SOURCE |

Callers, checked: `RegisterNewUser` 0x0086419E and 0x00864442; `GetFaceIDforAlbumEntry` 0x0086434E,
0x008643C6, 0x008645BC, 0x0086472C, 0x0086481C; `UpdateExistingAlbumEntry` 0x00864506, 0x0086493C;
`RemoveUser(faceID)` 0x00863B38, 0x008652D0, 0x0086782A; `RemoveUser(iterator)` 0x00865576..0x00865582,
0x008678EE; `MergeWith` 0x008639B8..0x008639C2; `ConvertToEnrolledFaceStorage` 0x00861024..0x0086107A;
the `SetSerializedFaceData` chain as in C3-24.

Still open: UNKNOWN source-level name and external meaning of `EnrolledFaceEntry +0x04` (its merge rule is
exact, C3-18). It must not be given a guessed semantic name.

## Correction C4: M14-011 build gaps closed (2026-09-29)

The G-M14 build of C3 left eight questions the rows did not settle. A bounded extractor pass reopened the
binary and settled all eight; its report is `.scratch/verify-G-M14/report.md`. These rows extend C3.

| step | what the original does (instruction-level) | citation | record | class |
| --- | --- | --- | --- | --- |
| C4-1 | **`EnrolledFaceStorage` wire layout.** One record, no padding: `+0x00` int64 little-endian = entry `+0x28` / 1,000,000; `+0x08` int64 LE = entry `+0x30` / 1,000,000; then `std::vector<long long>` as a **1-byte** element count followed by n1 little-endian int64 values; then `+0x11+8*n1` int32 LE face ID (entry `+0x00`); then `std::vector<int>` as a **1-byte** element count followed by n2 little-endian int32 values; then `std::string` as a **1-byte** length followed by that many raw bytes (no terminator, no encoding conversion). Record size = `23 + 8*n1 + 4*n2 + len`; the vector counts are one byte, so n1,n2 <= 255. The outer enrollment container: bytes `+0..+3` u32 LE `0x0002FACE`, `+4..+7` int32 LE nextFaceID, then zero or more records concatenated with **no record count and no per-record length prefix**; the loop continues while `offset < size-3` and the pre-loop gate requires `size > 11`; `Deserialize` rejects an empty buffer, `offset == size`, `offset + Size() > size`, and an unpack shorter than `Size()`. | Pack 0x00799042 (int64 0x0079904a/0x00799058, vector count 0x007990b2/0x007990c0, elements 0x0079998c/0x007999aa, faceID 0x00799078/0x00799082, id count 0x007391e0/0x007391ee, ids 0x007549b6/0x007549c2, string 0x006c22e4/0x006c22f8/0x006c2316); Size 0x00799138..0x0079915e; Serialize calls 0x00861032/0x00861038/0x00861054; container 0x008684e6/0x00868506/0x00868560/0x00868566/0x00868616/0x00868618; Deserialize guards 0x008611c4..0x00861278 | M14-011 | EXACT_SOURCE |
| C4-2 | **`GetNextFaceID` 0x0086561C.** The counter is `this+0xf4`; it increments, wraps `0` to `1` (`0x00865640 it eq; moveq r0,#1`), and loops while the candidate id is still present in the enrolled-face map at `this+0x110`. The ctor 0x0086216C initializes it to **1** (`0x008621e0 strd r6,r7,[r5,#4]` with r7=1 at `this+0xf4`). `SetSerializedData` stores the parsed nextFaceID into `this+0xf4` only on the success path (`0x008680fc str.w r5,[r6,#0xf4]`), the value coming from container bytes 4..7. | 0x0086561C..0x00865654; ctor 0x0086216C (`0x008621e0`, `0x008621e6`); 0x00867FF4/0x00867FFE/0x008680BE/0x008680FC | M14-011 | EXACT_SOURCE |
| C4-3 | **C3-8's placement.** The active-named-target path is inside the **low-confidence** branch: the confident gate at `0x00864338`/`0x00864344` branches to `0x00864394` when it fails, and the confident body never falls into `0x008643A2`. The low-confidence entry `0x00864394 movs r7,#0` gates on `+0x108 != 0` (`0x00864396 cmp r5,#0`, `0x00864398 beq 0x8642E8`) and `+0xFC != 0` (`0x0086439A ldrb [sl,#0xFC]`, `0x008643A0 beq 0x8642E8`) **before** `0x008643A2`. The path's tail `0x00864562 b 0x8643DC` forces the output id from `+0x100` (`0x008643DC`/`0x008643E0`) and returns 0 via `0x008642E6`; an `UpdateExistingAlbumEntry` failure branches to `0x008644E8` (return 1). | 0x00864338..0x008643A2; 0x00864562; 0x008643DC..0x008643E4; 0x008642E6; 0x0086450E | M14-011 | EXACT_SOURCE |
| C4-4 | **The ≥1 s gate reads `EnrolledFaceEntry+0x30` before the update, not the per-album-entry map node.** `SetAlbumEntryLastSeenTime` 0x008615xx writes the album-entry map node (`0x00861578 strd r2,r3,[r1,#0x18]`), while the gate at `0x00865C04..0x00865C14` loads `now` and **`0x00865C08 ldrd r3,r5,[r6,#0x48]`** — the enrolled-face map node value at `EnrolledFaceEntry+0x30` — and compares `now - value > 999,999` (`0x00865C7A..0x00865C94`). `EnrolledFaceEntry+0x30` is written on the success path by `AddOrUpdateAlbumEntry` (`0x00861532 strd r5,r4,[r6,#0x30]`, reached from `0x00865EB6`), i.e. after the gate. | 0x00865C04..0x00865C14; 0x00865C7A..0x00865C94; 0x00861532; 0x00861578; 0x00865EB6 | M14-011 | EXACT_SOURCE |
| C4-5 | **`MergeFaces` sequence.** After the identical-id short-circuit and the two lookups: (1) `MergeWith(keep, merge, 5, removed)` at `0x008639C0`; (2) walk keep's album-entry map (`entry+0x38`) and set each album-entry LUT entry (`sl+0xE8`) to keepID (`0x00863A40..0x00863A70`); (3) for each id in `removed`, erase the LUT entry and call `OKAO_FR_ClearUser` (`0x00863A98..0x00863ABA`), a failure returning 1 at `0x00863B3E`; (4) walk merge's remaining album-entry map, and for each whose LUT value equals mergeID erase the LUT entry and `OKAO_FR_ClearUser` (`0x00863AC6..0x00863AFA`); (5) destroy merge's album-entry map and set merge's current album entry to -1 (`0x00863B1E..0x00863B32`); (6) call the **full `FaceRecognizer::RemoveUser(mergeID)`** at `0x00863B38` (which erases the track mapping, iterates the now-empty album map, and erases the enrolled-face node), returning 0. | 0x008639B6..0x008639C2; 0x00863A40..0x00863A70; 0x00863A98..0x00863ABA; 0x00863AC6..0x00863AFA; 0x00863B1E..0x00863B38 | M14-011 | EXACT_SOURCE |
| C4-6 | **`GetNextAlbumEntryToUse` probe.** The free-index probe starts at `this+0xf8`, initialized to **0** by the ctor (`0x008621e4`). It runs only when the OKAO registered-user count is below 1000 (`0x008651D2 cmp r0,#0x3E8`). The free test consults **OKAO only**: `OKAO_FR_IsRegistered(album, index, 0, &result)` (`0x00865356..0x0086535E`); the Anki album-entry LUT is not read. A nonzero OKAO return is an error (-1); a free index is returned. The probe advances `index+1` and wraps to 0 when `index > 998` (`0x00865352 movw r8,#0x3E6`; `0x00865368..0x00865374`), for at most 1000 iterations (`0x00865376 cmp r6,#0x3E8`). The album-full path selects the oldest **unnamed** entry (`name length 0`, `0x00865214`), compares `entry+0x30` (`0x00865216`), reuses its current album entry (`0x008652BA..0x008652C8`), calls `RemoveUser` (`0x008652D0`) and verifies with `OKAO_FR_IsRegistered` (`0x008652E2`). | 0x00865182..0x0086537E; ctor 0x008621E4 | M14-011 | EXACT_SOURCE |
| C4-7 | **`SanityCheckBookkeeping` 0x00863064.** (1) `OKAO_FR_GetRegisteredUserNum(handle,&count)` must equal the album-entry-to-face-ID LUT size (`0x00863072..0x00863084`, else log `FaceLibNumEntries=%d, AlbumEntryToFaceIDSize=%zu`); (2) every LUT key must satisfy `OKAO_FR_IsRegistered(handle,key,0,&result)` with a nonzero result (`0x00863086..0x008630A8`); (3) every enrollment-owned album entry must exist in the LUT and its LUT value must equal the owning face id (`0x008630CA..0x00863120`). Any failure logs and returns 1. | 0x00863064..0x0086329C | M14-011 | EXACT_SOURCE |
| C4-8 | **`OKAO_FR_*` call shapes (the seam).** All AAPCS: `GetRegisteredUserNum(album, int* count)`; `Identify(feature, album, 10, int* ids, int* scores, int* count)` (scores and count on the stack); `RegisterData(album, feature, albumEntry, dataIndex)`; `GetRegisteredUsrDataNum(album, albumEntry, int* count)`; `ClearUser(album, albumEntry)`; `Verify(feature, album, albumEntry, int* score)`; `GetFeatureFromAlbum(album, albumEntry, dataIndex, feature*)`; `ClearData(album, albumEntry, dataIndex)`; `IsRegistered(album, albumEntry, dataIndex, int* result)`; `RestoreAlbum(common, const void* data, unsigned size, int* result)`; `GetAlbumMaxNum(album, int* albumMax, int* dataMax)`; `ClearAlbum(album)`; `DeleteAlbumHandle(album)`. | 0x00864100; 0x00864222; 0x00865CB8/0x00868BE4; 0x00865B98; 0x00863ABA/0x00866B26; 0x00865D94/0x00865E28; 0x00865E06/0x00868BD4; 0x00865E16; 0x0086535E/0x008630A0; 0x0086739A; 0x00868948/0x0086896E; 0x00868B2A; 0x0086808C | M14-011 | EXACT_SOURCE |

These rows settle the build's earlier guesses: the CLAD wire widths (1-byte counts), the next-face-id seed
and wrap/skip, the active-target placement and its two preceding gates, the gate's `+0x30` source, the full
`MergeFaces` sequence including `RemoveUser(mergeID)`, the OKAO-only free-index probe, the exact
`SanityCheckBookkeeping` conditions and the seam call shapes.

## Correction C5: M14-011 verifier defects and the write side (2026-09-29)

The verifier pass on the C3/C4 build found five blocking defects and two advance-semantics defects; a
follow-up extractor pass confirmed them in the binary and also extracted the serialize side. These rows
extend C3/C4 and are the authority for the fix.

| step | what the original does (instruction-level) | citation | record | class |
| --- | --- | --- | --- | --- |
| C5-1 | **The empty-album first-user branch tests the enrollment target id `+0x100`, not the track `+0x104`.** The branch is taken only when all three hold, in order: `+0x108 != 0` (`0x00864142`/`0x0086414A`/`0x0086414C beq 0x8641EC`); the OKAO registered-user count `== 0` (`0x0086414E`/`0x00864152 bne 0x8641EC`); **`+0x100 == 0`** (`0x00864146 ldr.w r6,[sl,#0x100]`, `0x00864154 cmp r6,#0`, `0x00864156 bne 0x8641EC`). A nonzero target id jumps to the normal `OKAO_FR_Identify` path at `0x008641EC` and skips the first-user branch. `+0x100` is the target face id (`SetAllowedEnrollments` writes `0x008658C2`, clears `0x0086592E`); `+0x104` is the track (`0x0086596C`, sourced from entry `+0x20` at `0x008658D4`). | 0x00864142..0x00864156, 0x0086419E, 0x00864328..0x00864330; 0x008658C2/0x0086592E/0x0086596C/0x008658D4 | M14-011 | EXACT_SOURCE |
| C5-2 | **A later debug candidate with no enrolled record is inserted, not skipped.** The list holds the top match, then the scan runs `r6 = 1..` while `r6 < count` and the list size `< 2` (so **at most one extra** debug match) and `r6 < 10`. For each: `faceID = GetFaceIDforAlbumEntry(ids[r6])` (0 on a miss); if it equals the **top** match's face id it is skipped (`0x008645C6`/`0x008645C8 beq 0x864674`); otherwise the enrolled map is searched and, on an empty map or a miss, an **empty name** is built (`0x00864618..0x00864628`) and the node is inserted with `faceID` (possibly 0), the candidate's score, and the count incremented (`0x0086462E..0x00864672`). A found record copies its name. No dedup among later candidates (the list is capped at 2). | 0x008645A4..0x008645B0, 0x008645BC..0x008645C8, 0x008645CE/0x008645F0/0x008645F6, 0x00864618..0x00864636, 0x0086466A..0x0086467A; 0x00866C1C | M14-011 | EXACT_SOURCE |
| C5-3 | **The lower-ranked path selects the first named candidate (index 1), and later candidates are only a conflict guard.** Entry requires `count > 1`, the top match's record name empty, and `topFaceID != +0x100`. The scan starts at index 1: `faceID = GetFaceIDforAlbumEntry(ids[1])`; on a miss warn `Missing2ndMatchEnrollmentData` and take the ordinary path (`0x008647D4 b 0x864926`). If the record exists but is unnamed, the guard `[sp,#0x38] != 0` (the top face id, nonzero per C3-3) sends it to the ordinary path (`0x00864778..0x0086477C bne 0x864924`). Score check: `scores[1] < 676` -> ordinary path (`0x008647DC cmp.w r0,#0x2A4` / `0x008647E0 blt.w 0x86491E`). Otherwise `threshold = min(scores[1]-75,600)` computed once (always 600 here) and the selected index (1) is stored (`0x008647F2 strd r6,r0,[sp,#0x2C]`). Later guard from index 2 while `scores[j] > threshold`: same face id -> skip; record missing -> warn `Missing3rdMatchEnrollmentData` and continue; record found (and the first candidate is named) -> **ordinary path** (`0x00864866 cbz r0,0x8648A8` not taken, `0x00864868 b 0x86491E`). Merge path: log `UsingLowerRankedMatch`, `MergeFaces(firstCandidateFaceID, topFaceID)` (`0x00864C2A..0x00864C30`); output face id = first candidate, score = `scores[selected]`, and update album entry `ids[selected]` (`0x00864C74..0x00864C84`, `0x00864932`). | 0x008646FC..0x0086471E; 0x00864720..0x0086478A; 0x008647D6..0x008647F2; 0x008647FC..0x00864868; 0x00864A52; 0x00864C2A..0x00864C30; 0x00864C74..0x00864C84 | M14-011 | EXACT_SOURCE |
| C5-4 | **The capacity compare is over both dimensions.** `GetAlbumMaxNum(current,&currentAlbumMax,&currentDataMax)` (`0x00868948`) and `GetAlbumMaxNum(loaded,&loadedAlbumMax,&loadedDataMax)` (`0x0086896E`). If `loadedAlbumMax > currentAlbumMax` **or** `loadedDataMax > currentDataMax` (`0x00868996 cmp r3,r1`; `0x0086899A cmple lr,ip`; `0x0086899C ble 0x8689F4`) it warns and returns 1. Otherwise equal in **both** -> handle swap (`0x008689F4..0x008689FA`, `0x00868A3E..0x00868A44`); otherwise copy (`0x00868AE8`). | 0x00868942..0x00868948; 0x00868968..0x0086896E; 0x0086898E..0x0086899C; 0x008689E2; 0x008689F4..0x008689FA; 0x00868A3E..0x00868A44 | M14-011 | EXACT_SOURCE |
| C5-5 | **The copy loop iterates the loaded album's full index range and the loaded data's full index range.** After `ClearAlbum(current)` (`0x00868B2A`), the outer loop is `albumIndex = 0..loadedAlbumMax-1` (`0x00868B98..0x00868BB2`, `0x00868BF4..0x00868BFA`) and the inner `dataIndex = 0..loadedDataMax-1` (`0x00868BA0`, `0x00868BAE..0x00868BB2`, `0x00868BEA..0x00868BF0`) — raw indices, **not** the parsed enrollment's album entries. For each pair: `IsRegistered(loaded,albumIndex,dataIndex,&result)` (`0x00868BC0`); free -> advance; registered -> `GetFeatureFromAlbum(loaded,albumIndex,dataIndex,feature)` (`0x00868BD4`) then `RegisterData(current,feature,albumIndex,dataIndex)` (`0x00868BE4`); any error rejects. | 0x00868B28..0x00868B2A; 0x00868B98..0x00868BB2; 0x00868BB4..0x00868BFA; 0x00868BC0/0x00868BD4/0x00868BE4 | M14-011 | EXACT_SOURCE |
| C5-6 | **`GetNextFaceID` leaves `+0xf4` at the returned id.** It loads `id = this+0xf4`, probes the enrolled-face map; if absent, no store and `id` is returned. If present, it increments, wraps 0 to 1, **stores** to `+0xf4`, re-probes, and repeats while still present. So `+0xf4` after the call equals the returned id (the first free id at or after the previous counter, 1..INT_MAX, never 0). | 0x00865620..0x00865654 | M14-011 | EXACT_SOURCE |
| C5-7 | **`GetNextAlbumEntryToUse` leaves `+0xf8` at the returned index.** Default `result=1`; `index = this+0xf8`. Each iteration `IsRegistered(album,index,0,&result)`; a nonzero OKAO return warns and returns -1. `result == 0` (free) jumps **before the store** and returns `index`, leaving `+0xf8` at it. Only `result != 0` (registered) advances `next = (index > 998) ? 0 : index+1` and stores to `+0xf8`. Bounded to 1000 probes, then warns and returns -1. | 0x00865344..0x00865352; 0x0086535E..0x00865366; 0x00865368..0x0086537E; 0x008653B6..0x008653D6 | M14-011 | EXACT_SOURCE |
| C5-8 | **The `+0xdc` track map is populated by `FaceRecognizer::UpdateRecognitionData` 0x00863384**, which emplaces key `this+0xac` (current tracking id) to value = the recognized face id (`0x0086357A`/`0x00863588`). It is erased by `UpdateExistingAlbumEntry`, `RemoveTrackingID` 0x008637F4, `RemoveUser(iterator)` 0x00866AF6 (key = the enrolled entry's `+0x20`) and `ClearAllTrackingData` 0x00864FD8. Whether the writer's key `+0xac` equals the erase key `+0x20` at erase time is not established here. **NEW row, outside C3's scope.** | 0x00863384 (`0x00863390`, `0x0086357A`, `0x00863588`); 0x00866AE0 (`0x00866AEA`/`0x00866AF2`/`0x00866AF6`); 0x008637F4/0x00863874; 0x00865A98; 0x00864FD8 | NEW (M14-011 residual) | EXACT_SOURCE |
| C5-9 | **`GetSerializedEnrollData` 0x00867DD8 clears its output and, with no enrolled faces, writes nothing.** It clears the vector (`0x00867DE2..0x00867DEA`) then gates on the enrolled-face map size `this+0x118`; size 0 branches to the epilogue with the vector empty (`0x00867DF0 cmp r1,#0`, `0x00867DF2 beq 0x867ED0`). So **no enrolled faces -> empty vector, no header.** | 0x00867DE2..0x00867DF2, 0x00867ED0 | M14-011 | EXACT_SOURCE |
| C5-10 | **The writer's 8-byte header is `0x0002FACE` then `this+0xf4`.** Bytes 0..3 are copied in order from the global 0x00C97F30 = `ce fa 02 00` (`0x00867DF4..0x00867E20`); bytes 4..7 from `this+0xf4` (`0x00867E64..0x00867E8A`). | 0x00867DF4..0x00867E20; 0x00C97F30; 0x00867E64..0x00867E8A | M14-011 | EXACT_SOURCE |
| C5-11 | **Records are written only for named entries, in face-id order, with no count/length prefix.** In-order walk of the enrolled-face map `this+0x110..0x114`; the name length at node+0x28 (SSO); length 0 -> **skip** (`0x00867EA8 cbz r1,0x867EB2`); otherwise `EnrolledFaceEntry::Serialize(node+0x18, vector)`. | 0x00867E8C..0x00867EAE, 0x00867EB2..0x00867ECE | M14-011 | EXACT_SOURCE |
| C5-12 | **`GetSerializedData` calls `GetSerializedEnrollData` only when the album vector is non-empty.** `GetSerializedAlbum` first; nonzero warns and returns; on 0, `GetSerializedEnrollData(enrollVec)` only if `album.begin != album.end` (`0x00867D5A..0x00867D66`). | 0x00867D10..0x00867D6A | M14-011 | EXACT_SOURCE |
| C5-13 | **The save path erases both NV tags only when both vectors are empty, album first.** `album.begin==end` **and** `enroll.begin==end` (`0x00657350..0x0065735E`) -> log `EmptyAlbumData`, erase 0x184000 (`0x0065746C`/`0x00657472`), then erase 0x183000 only if that succeeded (`0x006575E6`, `0x006575FA`/`0x00657600`). Otherwise resize both to 4-byte multiples and write 0x184000 first, then 0x183000 (`0x00657360..0x0065737C`, `0x006573A0`, `0x006573E6`). | 0x00657350..0x0065735E, 0x00657418..0x00657434, 0x0065746C..0x00657472, 0x006575E6, 0x006575FA..0x00657600, 0x00657360..0x0065737C, 0x006573A0..0x006573E6 | M14-012 (G1-4) + NEW | EXACT_SOURCE |

The track-map writer C5-8 is a residual of M14-011's production path (`UpdateRecognitionData`); the
`+0xac`/`+0x20` erase-key question is open. C5-12's album gate and C5-13's both-empty condition belong to
M14-012's save flow.

