# Research request: the R-DEV pre-extraction (M1 transport, M2 protocol, M3 device, M4 control)

- **Date:** 2026-09-29
- **Requested by:** manager (Claude)
- **Kind:** independent extraction
- **Answer file:** `re-analysis/research/20260929-R-DEV-pre-extraction.md`. Write each part as it finishes, so a partial
  run still leaves finished parts.

## Why this is needed

Round-3 job R-DEV (`re-analysis/jobs/R-DEV.md`) closes the M1..M4 gaps. It starts soon in window 3, so work the parts
in order; each finished part is usable at once. R-DEV checks every row before using it.

Follow `re-analysis/research/README.md` and `.opencode/agent/cozmo-extractor.md` exactly: one row per
behaviour-changing step, an address citation or UNKNOWN, and no guesses. The Cozmo engine code is mostly **Thumb**;
jsoncpp and the OpenCV libraries may be ARM: check. Cite branch veneers and PLT hops. Each record's current text is in
`re-analysis/fidelity_manifest.json`, and its approved rows are in `re-analysis/inventory/M1-transport.md`,
`M2-protocol.md`, `M3-device.md` and `M4-control.md`. Start from them. The policy review
(`re-analysis/research/20260929-policy-review.md`) covers M4-004 and M1-014; don't redo it.

## Part 1: M3 device

1. **M3-010:** the mu-law segment table at 0x00C5C3F0 (its values), and whether the product with 32767.0 is taken in
   single or double precision (the literal's width and the VFP ops). Also the NaN warning path.
2. **M3-001 / M3-018:** the image-decode dispatch for every encoding value. That covers:
   - A8 case 2 (ToGray of a raw RGB payload: the conversion arithmetic);
   - case 1 with a payload that isn't exactly 320 x 240 bytes;
   - encodings 0 and above 9;
   - the RGB dispatch for encodings other than 9;
   - IsColor of encoding 0.
3. **M3-001 / M3-018, the JPEG codec:** which libjpeg the engine's JPEG decode and encode reach (OpenCV's
   `libopencv_imgcodecs.so` or another). Its version string and its IDCT/upsampling method settings (for example
   `dct_method`, `do_fancy_upsampling`), so the manager can judge whether exact decoding is reachable.
4. **M3-021:** what `vision_config.json` sets for the initial exposure and gain, where the engine loads it, and what
   `VisionSystem::IsInitialized` depends on. The file is in the OBB (`re-analysis/obb/assets/cozmo_resources/config/`).

## Part 2: M4 control

5. **M4-016:** the head's `CheckIfDone` code at 0x005485F8..0x00548728.
6. **M4-017:** `EnableMode(14)`, called before the headlight send: what it is and what it sends.
7. **M4-018:** the cube-sleep flags' writers; the writer of `+0x41` (which releases a timer-0 pattern); what a pop that
   leaves an animation below it on the same layer resends; and which directory the engine reads the cube light
   animations from.
8. **M4-010:** `robot+0x490`, which gates the ObjectUnavailable broadcast: its default and writers.
9. **M4-008:** where `UpdateRobotData` runs in `UpdateFullRobotState`, before or after the origin check.
10. **M4-019:**
    - `HandleRobotStopped`'s tag, and the 150-send path (SC4a..SC4c);
    - the removal step of the 100-sample sliding Welford window;
    - RobotStateHistory's `lower_bound` walk.

## Part 3: M1 and M2

11. **M1-029:** the jsoncpp reader the firmware-header check uses. Does it accept comments and trailing commas (its
    Features settings)? What does `asUInt` do with a non-number?
12. **M2-002:** the consumers of status bits 0x2, 0x4 and 0x8/0x20 (the treads-path Delocalize, `[robot+0x280]+4`, the
    treads classifier): where each is read, so R-DEV can wire them to the layers that now exist.

## Out of scope

- Any code, inventory or manifest edit. No branches, commits or pushes.
- The cross-layer wiring records M3-013, M3-032..M3-034, M4-003, M4-005, M4-009 and M4-012: their source is read, and
  what's left is building them.

## Answer format

Per part, the table from `.opencode/agent/cozmo-extractor.md` (`step | what the original does | citation | record |
classification`), then contradictions with the current record or rows, weak evidence, and open questions.
