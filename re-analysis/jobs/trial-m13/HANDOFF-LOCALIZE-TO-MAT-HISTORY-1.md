# HANDOFF LOCALIZE-TO-MAT-HISTORY-1 (Claude worker, 2026-11-11)

**Verdict: unreachable, so nothing was built.** The census is `.scratch/LOCALIZE-TO-MAT-HISTORY-1-census.md`.
Following the narrow-check rule, it stopped at reachability.

## Evidence

The extractor scanned `resources/lib/armeabi-v7a/libcozmoEngine.so` with its own capstone/lief scripts; `known.py`
can't run because `decomp/` is empty.

- **The function:** `Robot::LocalizeToMat(const MatPiece*, MatPiece*)`, .dynsym
  `_ZN4Anki5Cozmo5Robot13LocalizeToMatEPKNS0_8MatPieceEPS2_`, value 0x00515CBD, size 1968. Its body is
  0x00515CBC..0x0051646C.
  - It contains the job's range 0x00515CBC..0x00516142, GetComputedStateAt at 0x00515CE6, the +0x2C4 reader at
    0x00515F64 and the tail at 0x0051612C..0x00516142.
  - It starts with `push.w`, and LocalizeToObject's literal pool comes before it, so nothing falls through into it.
- **Nothing calls it.** The scan found none of these:
  - a Thumb or ARM branch to 0x00515CBC;
  - a PLT JUMP_SLOT;
  - a GLOB_DAT or ABS32 relocation;
  - a raw 0x00515CBC/0x00515CBD word in any section other than .dynsym;
  - a PC-relative address build;
  - the name in any other shipped `.so`.
- **Positive control:** the same scripts find LocalizeToObject's PLT stub 0x004A7468 and both its recorded callers in
  LocalizeRobot (0x0050D2E4, 0x0050D412). LocalizeRobot calls only LocalizeToObject.

## What changed

| Where | Change |
| --- | --- |
| M11-044 `unresolved` | Records LocalizeToMat as uncalled code: no effect on 1–4, not built. This supersedes its "Still open: the LocalizeToMat tail". |
| M11-044 `unresolved` (two notes) | The notes on GetComputedStateAt's two callers and on the +0x2C4 readers stay, as code facts, with the LocalizeToMat site uncalled. |
| `BlockWorld.cs`, Robot2C4 doc | 0x00515F64 is labelled as uncalled code. |
| Code | No other change. |

## Also affected (not edited)

The following entries point into this uncalled function. The manager may want to annotate them:
- M11-073's history producers 0x005160BC and 0x0051612C;
- `research/20260929-R-DEV-M4-control-gap1-extraction.md` L8;
- `research/overnight/20261018-ov127-m1m2-settling-narrow.md` R6 and R8;
- `inventory/M4-control.md` C11.2.

## Records

**M11-044:** no settlement. The null-O body, the path replan join (PATH-ORIGIN-CHANGE-REPLAN-1) and the LocalizeRobot
NaN order are still open.
