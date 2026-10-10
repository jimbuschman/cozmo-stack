# Job B-M3M4: finish M3 and M4

- **Agent:** Codex, as builder (`jobs/CODEX-BUILDER.md`, rules 1–10).
- **Map of the layer:** `research/20261009-M3M4-build-plan.md`.
- **Already built:** the slice built from checked rows (4ae4d32), awaiting strong verification.

## Rows checked (manager, 2026-10-09)

An Opus row check sampled every row group of `research/20261006-M3M4-rows-extraction.md` (R6),
`research/20261009-M3-041-build-rows.md` (L) and `research/20261009-M1-046-imu-reachability.md` (I) against
libcozmoEngine.so, libopencv_imgcodecs.so and libc++_shared.so. The sampled rows held, including their call targets.

**Adopted as build authority, with the corrections listed. A correction wins over the row.**

- **C (M3-023):** adopted. Rename the two C6 labels so they are distinct.
- **N (M3-027):** adopted.
  - N6's UNKNOWN is real: header bytes 14..15 (sp+0x3A..3B) are never written.
  - Read Data can carry those two bytes, so M3-027 and M3-022 can't settle while it stands. Keep it visible: build
    the defined bytes and record the two as never written.
- **R (M3-030):** adopted. R4–R6 weren't reopened, so check them as you build.
- **W (M3-031):** adopted, with a correction. W2 must include the debug log at 0x006433C6 (via 0x004A5B84) before the
  callback. The order is: the broadcast (gated on +0x40), then the callback (gated on +0x38), then the backup call
  (WipeAll when op == 3, otherwise WriteDataForTag(tag, result, op == 1)).
- **U (M4-008):** adopted. All literal bits match.
- **P (M4-010):** adopted. P19–P22/P28/P29 (the hash table) weren't reopened, so check them as you build.
- **H (M4-011):** adopted (light sample).
  - M4-011 is a settled record, so any behaviour change from H rows comes to the manager first.
  - H7–H12 weren't reopened.
- **G (M3-032):** adopted, with a correction. Between the gates, 0x00513C66..0x00513C6A calls
  `VizManager::SendStartRobotUpdate` (0x004A7DA4) through context+0x24. Record it as viz-out-of-scope (debug
  visualisation, app-side) in M3-032's `unresolved`. Don't build it.
- **S (M4-020):** adopted. S2 and S3 defer openly to M11.
- **A (M3-013):** adopted as a boundary only, with a correction. The audio object is at [client+0x1B0]+0x38, not
  client+0x38. Its virtual +8 takes the timing integers, and the readiness call is a tail call. A2 is a lead.
- **B (M3-037):** adopted.
  - B3: the three-word vector is saved, moved and returned (0x006530CC..0x00653136), and no bytes are copied.
  - **Narrow M3-037's COMPATIBILITY_POLICY to the bytes that are genuinely never written.** Everything else follows
    B3 exactly.
- **Q (M3-026):** adopted. Q2, how a null name prints, stays open as a runtime question.
- **F (M4-001):** adopted. F3 is phone runtime.
- **V (M3-021):** nothing to adopt. Both rows point to M11.
- **L (M3-041), L1–L36:** adopted. **L30 stays PARTIAL.** Every float and every filename number goes through the
  packaged libc++ formatter (0x49B64 → 0x82528 → bl 0x813B0), so M3-041 can't settle until that formatter has rows.
  Build everything else.
- **I (M1-046 reachability), I1–I11:** adopted.

**Runtime assumption to record:** the FPSCR rounding mode is round-to-nearest (Android's default; the same decision
as M1-029). It applies to J42's resize setup, and anywhere else the default VFP mode matters.

## JPEG (M3-001, M3-018): held. Don't build yet.

- **The missing row.** When the stream has no Huffman tables (all four slots +0xC4, +0xC8, +0xB4, +0xB8 null), the
  decoder loads OpenCV's default MJPEG tables: libopencv_imgcodecs.so 0x15BA4..0x15BE8, `bl 0xE2B8`. That loader walks
  a static table (count ≤ 256, selector ≤ 3) and allocates via 0x1ED88.
  - The mini headers carry their own tables (engine 0xC48C40, 0xC48D84), so encodings 8 and 9 never reach it.
    Encodings 5, 6 and 7 reach it whenever the input has no tables.
  - **Needed:** extract a row for this branch, plus a byte-for-byte dump of the table that 0xE2B8 reads.
- **Corrections:**
  - J14: the colour-space choice tests num_components (+0x24 == 4) together with the output Mat's channel count
    (ubfx at 0x15BAE). Colour gives RGB with 3 components, or CMYK with 4; gray gives 1, or CMYK with 4.
  - J6: the empty-input error also fires when the data pointer is null (cbz at 0xF6C8).
- **The Save encoder** (M3-018, quality 90) has no rows. Extract them.
- **The method, once the rows are complete:** port the reachable paths from the J rows. Accept them against an
  emulator oracle: the shipped imdecode/cvtColor/resize run under `re-analysis/tools/emu/` on real camera frames plus
  a synthetic corpus of crafted JPEGs that reaches every J branch (scaled IDCTs, progressive, arithmetic, restart,
  default tables, error/longjmp). Require byte-identical Mats. Exclude only bytes that are genuinely never written,
  and say so. ADP-1 does **not** apply to images.

## What Codex does next

1. Build the adopted groups, applying their corrections.
2. Extract the JPEG default-table row and table dump, and the Save encoder rows. Those come to the manager.
3. Then the JPEG port plus its emulator oracle, once the manager adopts those rows.
4. Apply `research/20261009-M3M4-remaining-rows.md` once the manager has checked it.
5. When the layer's build is done, prepare the verification packet (rule 9).
