# Research request: the R-VIS pre-extraction (M11 vision, M12 manipulation, M13 navigation)

- **Date:** 2026-09-29
- **Requested by:** manager (Claude)
- **Kind:** independent extraction
- **Answer file:** `re-analysis/research/20260929-R-VIS-pre-extraction.md`. Write each part as it finishes, so a partial
  run still leaves finished parts.

## Why this is needed

Round-3 job R-VIS (`re-analysis/jobs/R-VIS.md`) closes the M11..M14 gaps. It starts last, after R-BEH in window 1.
Answering its missing-source questions now takes them off its critical path. R-VIS checks every row before using it.

Follow `re-analysis/research/README.md` and `.opencode/agent/cozmo-extractor.md` exactly: one row per
behaviour-changing step, an address citation or UNKNOWN, and no guesses. The Cozmo engine code is mostly **Thumb**. The
OpenCV libraries (`resources/lib/armeabi-v7a/libopencv_*.so`) may be either mode: check. Cite branch veneers and PLT
hops. Each record's current text is in `re-analysis/fidelity_manifest.json`, and its approved rows are in
`re-analysis/inventory/M11-vision.md`, `M12-manipulation.md` and `M13-navigation.md`. Start from them.

Out of scope: the inside of OKAO (the vendor face engine, M14-010's boundary), and the M14 face-recognizer items (G-M14
has them).

## Part 1: BlockWorld and observed objects (M11)

1. **M11-013 (the policy review: `re-analysis/research/20260929-policy-review.md`):** `BlockWorld::CreateObjectsFromMarkers`
   and the observed-object construction upstream of `AddAndUpdateObjects` 0x00620AD4. Where does an observed object's
   ObjectID come from, and what happens to an observation of a cube that isn't connected?
2. **M11-004:** `FindLocatedObjectHelper` 0x0061EB78. Which object it returns, and its tie-break.
3. **M11-007:** what the caller of `AddVisualObservation` sets as the object's PoseState when it returns false (and
   when true). Map every PoseState write on the observation path.
4. **M11-037:** the `AddLiftOccluder` occluder points (VisionComponent+4), and the BlockConfigurationManager gate
   fields at this+0x24 / this+0xC: their writers and meanings.
5. **M11-038:** `BroadcastLocatedObjectStates` 0x0061E6C0, `BroadcastConnectedObjects` 0x0061E91C and
   `VisionSystem::CheckMailbox` 0x006B2AD4: what each builds and sends, field by field. The message layouts are in the
   CLAD generated serializers (`unity/scripts/csharp/Anki.Cozmo.ExternalInterface/` and similar): cite them.

## Part 2: vision kernels and the vision thread (M11)

6. **M11-017:** `OverheadEdgesDetector::Detect` 0x006ABE34, `GroundPlaneROI` 0x004F7774, and the four MapComponent entry
   points 0x0067F7AC / 0x0067F814 / 0x0067E6B0 / 0x0067E50C. The whole algorithm, with constants, to the map writes.
7. **M11-021:** in the shipped OpenCV, the non-divisible `CLAHE_Impl::apply` branch (`copyMakeBorder`, around
   0x20EC0..0x20F58 in `libopencv_imgproc.so`), and the post-CLAHE `ColumnSum<int,uchar>` body.
8. **M11-033:** the RGB2GRAY kernel the colour branch uses (`cv::cvtColor` in `libopencv_imgproc.so`): its exact
   integer or float arithmetic and rounding.
9. **M11-040:** the VisionComponent processing thread and mailbox: how a frame is handed in, what happens while one is
   in flight, and how results come back to the engine thread.
10. **M11-012 (the policy review):** the code that tests the string "Must be initialized and have calibrated camera to
    Update" (0x00C016DC): the gate on vision updating without a calibration.

## Part 3: manipulation and navigation (M12, M13)

11. **M12-022:** `DriveToObjectAction+0x84` (the 7-argument constructor sets -1.0): its setter and readers, and the
    engine's object+delta goal in `InitHelper`.
12. **M12-020:** `PlaceRelObjectAction::ComputePlaceRelObjectOffsetPoses` 0x005558F4, including the threshold call at
    0x005561A0.
13. **M13-014:** `TurnTowardsLastFacePoseAction` (used at 0x0055B3C4..0x0055B3D8 in IDriveToInteractWithObject): what
    it does, with its arguments and constants.

## Answer format

Per part, the table from `.opencode/agent/cozmo-extractor.md` (`step | what the original does | citation | record |
classification`), then contradictions with the current record or rows, weak evidence, and open questions.
