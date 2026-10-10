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

## Rows checked (manager, 2026-10-10): the remaining records

An Opus row check of `research/20261009-M3M4-remaining-rows.md` reopened every call target, through the PLT/GOT and
the `bx pc` veneers.

- **Adopted as written:** M4-027, M4-028, M4-031, M3-038 and M3-040.
- **M4-026: adopted with corrections.**
  - **A3 (Path.Abort):**
    - after the reverse loop, `0x006491AC..0x006491B4` stores the global `PoseOriginList::UnknownOriginID` (GOT
      0x0103E978) into `[*(this+0x50)]+0x0C`. Build that store;
    - the loop at `0x00649198..0x006491A4` destroys 12-byte inline elements back to front through each one's vtable
      slot 0. That's a destroy in place, with no operator delete.
  - **A10 (UnlockTracks):**
    - on the missing-key path (INFO, then PrintLockState at 0x0063FF76) the track count is reloaded (0x0063FF7A) and
      falls into 0x0063FF7E..0x0063FF88, so a selected track that was **already empty** also adds its bit to the
      EnableAnimTracks mask;
    - the return value is OR'd only on the found-and-erased path (0x0063FF1A..0x0063FF22, count != 0 after the erase).
      A track still locked by others whose key is missing doesn't set it;
    - the send happens only when the u8 mask != 0 (0x0063FF96).
- **M3-039, M4-029 and M4-030 (the SDK recipients): unreachable in this stack.**
  - ResetRobot has two callers: EnterMode (0x0065E1F8, only on first entry, 0x0065E11A..0x0065E17C) and OnDisconnect
    (0x0065E5D0, gated on +0x78 and +0x7C).
  - +0x78 is set only by EnterMode's internal branch (0x0065E172) and by OnConnectionSuccess (0x0065E8D4), which is
    reached only behind IsExternalSdkConnection.
  - M4-029's lift-power dispatch (0x0065DF8C) is unconditional within ResetRobot, so its evidence should say "inside
    ResetRobot", not "under +0x79". It is still reachable only after SDK mode has been entered.
  - **Scope note:** the original also enters *internal* SDK mode from CodeLab (`CodeLabGame.cs:967`) and Edu mode
    (`SettingsEduModePanel.cs:119`). Those are app features the stack doesn't implement (AGENTS.md "Scope: the app
    boundary"), so "SDK mode is not supported" covers them too.
  - Record each of the three as COMPATIBILITY_POLICY with this evidence: unreachable without SDK mode. The shared
    recipient functions stay owned by their own records.

## Opus pass on the first slice (4ae4d32), 2026-10-10: fix round 1

The verdicts:
- **M3-010, M4-009 and M4-019:** their slices are exact, but each record stays open on later pieces.
- **M4-017:** exact apart from item 3 below. The record stays open.
- **M4-016 and M4-018:** each has a defect. Fix these:

1. **M4-016, the timeout log.** After the 0x03000018 result (0x00540E80), the engine checks `ldrb [+0x57]` (0x00540E7C).
   It defaults to 1, from the ctor's +0x55 = 0x10000 store (0x0053FE18..0x0053FE1C). It then calls sWarningF at 0x00540EB6
   with "IAction.Update.TimedOut" (0x00540FE0), format "%s timed out after %.1f seconds." (0x00540FF8), and the timeout
   from slot 0x2C. `IActionRunner.cs:318` doesn't log, and `MoveAction.Log` is never wired, so the base's other
   IActionRunner warnings are silent too. Wire the log and emit it.
   - Pass the engine's "Actions" channel argument.
   - Add tests for the four u16 counters and the eleven-count WaitingForAck/NotInPosition logs, head and lift, at the
     `blo #0xb` boundaries 0x00548620, 0x005487C0, 0x00549446 and 0x00549516.
2. **M4-018, EnableGameLayerOnly.** It logs on entry, before the C13.4 gates: sChanneledInfoF at 0x0063997A, channel
   "CubeLightComponent", event "CubeLightComponent.EnableGameLayerOnly" (0x00639BAC), format "%s game layer only for
   %s" (0x00639BD4).
   - The first %s is "Enabling" (0x00BFAA93) when enable != 0, otherwise "Disabling" (0x00BFAA9C).
   - The second is "all objects" (0x00639B94) when ObjectID+4 == -1, otherwise "object " (0x00639BA0) followed by the
     id (0x00639930..0x00639940).
   - Also: a single object with no ObjectInfo is a no-op (`cmp r7,end; beq` at 0x006399D8..0x006399E0). The C# still
     runs SetObjectLights and the stops; fix that.
   - Replace the circular test at `M4ControlTests.cs:1980`. Show that the held refresh fires (a re-pick or a send) after
     SetLocalizedTo, and add a regression for the disable-all order (`Lights.cs:845-859`, 0x00639AEA..0x00639B32).
3. **M4-017, the manager's decision.** In the engine the NullVisionSystem branch (VisionComponent+0x1C == 0,
   0x006527AC..0x00652808) can't be reached: the VisionComponent ctor always allocates the VisionSystem
   (0x00650184..0x00650196). The stack's optional VisionSystem is a host configuration, not engine state.
   - **When no VisionSystem is attached, don't emit the engine error or set `_errG`** (`Lights.cs:130-136`). Keep the
     requested mode and apply it when a VisionSystem is attached.
   - M11 will make the VisionSystem always present.

Apply rule 10 (every log in the cited ranges). Commit and push.
