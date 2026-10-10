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

## JPEG unblocked (manager, 2026-10-10)

Source: `research/20261010-jpeg-defaults-and-save-rows.md`.

**The default Huffman table row: adopted.** The manager re-read libopencv_imgcodecs.so 0x15BA4..0x15BE8: four null
checks on +0xC4, +0xC8, +0xB4 and +0xB8, then `bl 0xE2B8(cinfo, cinfo+0xC4, cinfo+0xB4)`, its result ignored. The
static segment at VA 0x175018 starts `FF C4 01 A2`, with the standard Annex K tables in the order DC0, DC1, AC0, AC1.
- Use the extractor's walk rows: the stop at ≤ 16 bytes remaining; count > 256 or count > remaining − 17 fails;
  selector > 3 fails; alloc_small 0x118; sent_table = 0.
- The copy always takes 256 huffval bytes, so the tail beyond the count is uninitialised stack (U2). Standard libjpeg
  derives its tables from the counted entries only. **Confirm with the emulator oracle** that no decoded output
  depends on those tail bytes. If one does, list it as never-written bytes, under the rule for M3-037.

**EncodedImage::Save is unreachable in the shipped app (manager).**
- It has no call site and no data pointer in libcozmoEngine.so (the extractor's scan).
- No shipped .so imports `_ZNK4Anki5Cozmo12EncodedImage4Save...`. The manager checked every library in
  resources/lib/armeabi-v7a.
- No Unity script references EncodedImage.

It is a dead export. **M3-018's Save part (quality 90, the JPEG encoder) is out of scope as unreachable;** record that
in M3-018's `unresolved` with this evidence. M3-018's decode and IsColor parts stay in scope.

**Build next (Codex or the manager's implementer):**
1. The JPEG decode port (M3-001/M3-018 decode) from the J rows with their corrections, plus the default-table row.
2. Its acceptance oracle: the shipped imdecode, cvtColor and resize running under `re-analysis/tools/emu/` on real
   camera frames plus a synthetic corpus that reaches every J branch. Mats must be byte-identical.

## M3-041 L30 (manager, 2026-10-10)

The IMU logger's number formatter is **shipped** inside libc++_shared.so. `0x82528` is a vsnprintf wrapper, and
`0x813B0` a full printf core. An integer prints as `%ld`; a float as `%.*g` at the stream precision (default 6). It
reads no locale (the decimal point is a literal '.'), and it calls out to the phone only for leaf predicates.

**Plan:** reproduce `%ld` and `%.*g` exactly. Correctly rounded digit generation, round-half-even on the exact binary
value, the `%g` e/f switch (`P > e >= -4`), trailing-zero stripping and a two-digit signed exponent. **Accept it
against an emulator oracle** of the shipped 0x82528, the way the M1-029 converter was, on f32-widened IMU values. Until
that passes, L30 stays RECOVERABLE_GAP.

**Correction:** the shipped core is not correctly rounded. It is a musl-style `fmt_fp` working on base-1e9 limbs, with
the rounding quirks that come with the limb boundaries. The port reproduces it, quirks included.

**Opus check of the build (2026-10-10): FAIL.** The formatter passes the oracle: 4291/4291 rows, the fixture
regenerates byte-identical, and no test is circular. Seven objections:

1. **Blocks.** After a handler close, the filebuf's put area survives. Insertions keep buffering with the state at 0
   until about 4095 bytes are held, and the next open writes those stale bytes at the head of the new file. The C# sets
   state 5 at once, and `M3ImuLogLiveTests.cs:299/304` assert that. Rows are being extracted
   (`research/20261010-m3041-filebuf-rows.md`).
2. **Blocks.** OpeningLogFile and ClosingLogFile are `sChanneledInfoF` with channel "Unnamed". Print them as
   `info: [Unnamed] …`.
3. CreateDirectory is engine code (0x008028AC..0x008029C8): the '/' walk, mkdir 0700, the stat bit test and the
   200-iteration cap. Port it.
4. FileExists tests `S_ISREG`; `File.Exists` doesn't. Port it.
5. **Visibility.** The carry loop at 0x821E4 reads one limb below `a` that is never initialised: stack residue on the
   phone. The port assumes 0. State this boundary in the code, in the fixture's `boundary` field and in `unresolved`.
6. **Visibility.** The host log directory needs a COMPATIBILITY_POLICY record.
7. **Blocks.** The `M3ImuLogStatics` collection runs in parallel with the other users of `EngineErrorState`. Join the
   serial collection.

**Filebuf rows adopted** (`research/20261010-m3041-filebuf-rows.md`). The manager re-read three of them:
- overflow with a null FILE returns −1 at 0x0050176C and touches nothing;
- sync returns at 0x0050147C, skipping the reset block at 0x0050146C, so `__cm_` and the put area survive a close;
- xsputn (0x004E3F06) is a memcpy that never looks at FILE, plus a one-character overflow.

**Pinned:**
- The vtable's bodies are taken to be the engine's own copies, not the libc++_shared exports of the same names.
- `__always_noconv_` is taken to be 1 (`codecvt<char,char,mbstate_t>`).
- How many sputn calls one number makes (num_put) is unread. It only matters when the buffer fills while the file is
  closed.

**Fix round re-checked (Opus, 2026-10-10): PASS.** All seven objections are resolved. The manager applied the queued items: the M3-042 tag on EngineFileUtils, the unreachable no-write-mode sync comment, and the boundaries in M3-041's and M3-042's `unresolved` (the uninitialised limb; Windows `\` components skipping the cap; the Unix lstat fallback). M3-041 is EXACT_SOURCE. Queued, not M3-041's: the stack is inconsistent about empty-format log text (`CliffPickupBehaviors.cs:218` has no `": "`).

## NV write side (manager, 2026-10-10)

Rows `research/20261010-nv-write-dispatch-rows.md` adopted as M3-043. The manager re-read the chunk loop (0x00645816..0x00645920: 0x400/0x3F0 caps, the stores to +0xE8 and +0xE0) and the write timeout (gate +0x48 at 0x006456F4, `bls` at 0x00645706, cb(-4) at 0x00645754).
- **The saved Data (+0xE8) is written only by the chunk loop.** A READ, ERASE or WIPEALL sent after a write carries the last chunk sent. Built per Update, one chunk per call, not all at once at Write time.
- **Never-written bytes** (header 14..15, a first WIPEALL's Length, the padded over-read) go under M3-037's policy.
- **Pinned:** vector-overload callers (unswept narrow `b`/ARM); the M15 backup bodies.

## M4-026/027/028/031 build (Sonnet, 2026-10-10)

Built: 30 new tests (M4AbortSleepTests), all passing. The fidelity check passes. In the shared tree the full suite has two failures: the known slow-motor flake and a load-sensitive M12 test.

**Sent back for extraction** (`research/20261010-m4-movement-leftover-rows.md`):
- the five direct-drive strings at +0xBC..+0xCC;
- the UnlockTracks info channel;
- the LockTracks debug string for each caller;
- the ERobotDriveToPoseStatus name table;
- the lock-tree comparator;
- the MotorActionAcked arguments;
- AnimTrackFlagsToString for 0, 0xFF and bit 7;
- the StopHead/StopBody gates (MA4; M4-015 still has the old per-track gate).

**Pinned:**
- M4-027's Touch (+0x28C) and Cliff (+0x288) teardown, their RollingFileLogger flush/close, and the FaceLayerToRemove/ScopedHandle releases. There are no C# objects behind them yet, so they stay visible MISSING lines.
- The production sleep sequence (M5/M8).
- Other AbortAll callers.

**Queued:**
- CompoundAction.AddAction overwrites the child's Log with a null parent Log (also in QueueHeadAndLiftCompound).

**Accepted:**
- PathComponent as a hook model.
- The teardown bindings.
- Removal running AbortAll, so in-flight actions end Cancelled, as in the engine.

**Opus check of the M4-026/027/028/031 build (2026-10-10): FAIL.** Four objections, each sent back as fix round 2:
1. The PathComponent constructor sets status 4 (Ready) and +0x40 = 0xFF (0x00648B14..0x00648B1C). The build started both at 0.
2. AbortAll, Path.Abort and ClearPath return the engine's Result (1 if any send failed; 0x00511980..0x00511988). The C# had the meaning inverted, and three tests pinned the inverted value.
3. On removal the AbortAll sends fail after the disconnect and log `Robot.SendMessage` warnings (0x0051349C). The build logged none.
4. The PathDoler hook was skipped silently. It must be a visible MISSING line.

**Supported:**
- the A1/A3/A4/A7/A10 orders, row 1b and row 8;
- the comparator and the D1 text;
- the head and lift acks: a repeated ack logs again, because +0xAB is only stored and never tested;
- the unreachable L6/L8 branch;
- the teardown bindings.

**Queued:**
- CozmoMotion's older sends don't go through the SendMessage failure warning;
- PathComponent duplicates PathSender._pathId and DriveActions' status; wiring is M12/M13;
- the higher-layer lock callers pass no debug name (M5/M7/M8);
- the log prefixes are inconsistent.

**Opus check of the Q/N/R/W/U/P/G + M3-043 build (2026-10-10): FAIL.** Three omissions:
1. ConnectToObjects' pre-loop BlockPool log (0x005173A6).
2. The rest of HandleObjectPowerLevel after the U4/U5 arithmetic: the volts log (0x005371CE), the DAS-report gate and log (0x005371FA..0x0053728C), and the broadcast lookup, log and game message (0x00537366..0x005373E4).
3. The constructor's SetState(0) log (0x006428E8).

Items 1 and 2 are being extracted (`research/20261010-power-level-and-connect-log-rows.md`). Item 3, the full OnDisconnected reset of the saved command, and the stale comments are in fix round 2.

The manager corrected three records:
- **M3-043:** a factory-tag Write fails with FactoryTagNotAllowed only; the size check passes, because a limit of 0 becomes 0xFFFFFFF0.
- **M3-032:** the VizManager item is out of scope.
- **M3-037:** it now covers the NV never-written bytes.

**Queued, outside this diff:** M14-012. VisionSystem writes the enrollment even when the album Write failed, but the engine gates it on the return value (`cbz r6` at 0x006573C0). The Request shim returns void.

**M4 fix round 2 re-checked (Opus, 2026-10-10): code supported.** The one blocking item was a circular test. The expected warning type names came from MessageCatalog. The manager replaced them with literals cited from EngineToRobotTagToString (0x007AF8D0, table 0x010343A0): clearPath, abortDocking, abortAnimation, stop. The manager also fixed the PathDoler comment ("built when the context is set", 0x00648B56). M4AbortSleepTests pass, 45/45. **M4-026/027/028/031 are accepted for commit.**

**Queued:**
- a test for the EnableAnimTracks failure warning after a disconnect;
- `ErrorFlagSet` is process-wide.

## JPEG port: Opus check (2026-10-10): FAIL

The oracle holds up:
- a full regeneration is byte-identical;
- a hostile allocator changes no case;
- the stand-ins carry no behaviour;
- the expected values come from the oracle.

**Blocking:**
1. **The LSE marker (0xF8) is unported.** get_lse (imgcodecs 0x22004..0x223DA) sets color_transform at cinfo+0x130, so J73's "nothing sets it" is false. The shipped decoder applies subtract-green, while the port returns an empty Mat. It is reachable through encodings 5/6/7.
2. **A circular test.** It normalised "(null)" to "null".
3. **An invented out-of-memory text.** The real strings ship in libopencv_core.
4. **Rule 10: the engine's own logs.** DecodeImageHelper's UnsupportedEncoding error and BadDecode warning are not emitted, and the live caller logs `frame {id}: {error}` instead.

**Manager rulings:**
- **NULL `%s`:** rendered as bionic's "(null)". That is the phone libc's printf, a boundary of the system library. Remove the normalisation. This also settles M3-026 Q2.
- **Out of memory:** use the shipped message text once extracted. The allocation failure itself remains phone state.
- **Logs:** emit the engine's logs inside the decode helper. The caller's invented line goes, unless a recorded policy owns it.
- **LSE and color_transform:** extract (`research/20261010-jpeg-lse-logs-oom-rows.md`), then port them and extend the corpus.
- **Queued:**
  - pin the library SHAs in the oracle;
  - the wrong `_errG` citation (the stores are at 0x4F22AC/0x4F297A);
  - "no NEON dependence" is overstated;
  - the leftover A25 label;
  - data.zip is 12 MB.
- **cv::Exception from an empty vector:** the engine doesn't catch it (`_Unwind_Resume`). It needs an M11 record before M3-001 settles. **Pinned.**
- **Other OpenCV decoders (PNG/TIFF) for non-JPEG payloads in encodings 5/6/7:** **pinned**, because whether the robot ever sends 5/6/7 is not established.

**NV fixes re-checked (Opus, 2026-10-10): code supported.** The one blocking item was the A12 lookup hook, which was installed only by ManipulationSystem. The manager moved it to VisionSystem's constructor, which owns the BlockWorld. The manager also:
- stopped `Dispose` logging the constructor's SetState line; the destructor has no SetState;
- added the new rows to M4-008's and M4-010's evidence;
- re-approved M4.

The affected tests pass, 538/538. **The Q/N/R/W/U/P/G groups and M3-043 are accepted for commit.**

**Queued:**
- a stack-wide decision on channel prefixes;
- the DAS `$data` sink;
- the SetState line is logged at disconnect, where the engine logs it at the next construction;
- the tautological bits assertion in M4_008_U4_U5;
- no test for `pending = 1`.

**JPEG fix round re-checked (Opus, 2026-10-10): code supported.** All four objections are resolved: LSE, the logs, "(null)" and the out-of-memory text. The oracle reproduces: a full regeneration is byte-identical, and the SHA pins hold.

The one blocking item was the unowned empty-payload line. The manager made it part of policy M3-020: the record and the tag now say so. The manager also corrected the stale StbImageSharp text in the inventory and pinned O8 (an indirect `cv::redirectError`) in M3-001. **M3-001/M3-018 are accepted for commit.**

**Queued:**
- the A25 label at Camera.cs:139-140;
- data.zip is 12.5 MB;
- `VisionSystem.cs:794` logs `frame {id}: {Type}: {msg}` for any exception. That pre-dates this round, and it includes the O5 wrap stub, which a crafted colour JPEG can reach.
