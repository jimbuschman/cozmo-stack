# Codex tracking, ground-placement, Wwise-selection and face-owner batch: Claude check (manager, 2026-11-08)

**Verdict: all four PASS and merge (branch tip ee6d9fb9). M6-007 settled EXACT_SOURCE. 26 records settled.**

Codex gate: `scripts/test-full.sh` on 9c6719a5, 7983/7983, no skips.

## WWISE-SELECT-PROBE-1: PASS. M6-007 settled EXACT_SOURCE

I re-disassembled SelectPlayable 0xA0A3B4..0xA0A6F0 (ARM) myself.

- **Entry:** length 0 returns none (0xA0A3E8..0xA0A3F0). Length 1 fetches child 0 through the node registry with no
  +0x48 playability call (0xA0A654..0xA0A684).
- **Mode 1, sequence:** steps through the sequence state on every attempt (0xA0A524..0xA0A558, 0xA0A614..0xA0A650,
  0xA0A6A8..0xA0A6F0).
- **Random:** the first candidate comes from SelectRandomly 0xA08A44 (0xA0A4F0).
- **Shuffle** (randomMode 1) leaves the scan flag clear, so each rejection goes back to SelectRandomly and draws again
  (0xA0A49C..0xA0A4CC).
- **Every other random mode** sets the scan flag on the first rejection (0xA0A4A4..0xA0A4BC), then walks idx+1 with
  wrap (0xA0A454..0xA0A464). Eligibility for each scanned index:
  - raw modes 2 and 3 (`[+0x90] & 0x30`): not played (0xA06AC4) and not blocked (0xA06B44), at 0xA0A5C4..0xA0A5EC;
  - with an avoid window (+0x8E): not blocked (0xA0A5F0..0xA0A610);
  - otherwise any index.

  Each eligible index then gets the avoid helper 0xA08694 (0xA0A48C) before the lookup.
- **Each candidate:** a registry lookup at 0x9A7EB0. A missing node is a rejection. A node whose slot +0x48 returns
  false is released through +0x0C (0xA0A5B0..0xA0A5BC). Only a success publishes the index (0xA0A6D4).
- **Attempt limit:** the playlist length, with the first candidate and skipped scan indices both counted
  (0xA0A514..0xA0A520).

`WwiseSelectionState.SelectNext` matches all of it: the same candidate order, eligibility, avoid-helper timing, draw
count and limit. The old guessed `Probe` is gone. The SelectRandomly bodies and the +0x48 slots (Sound tests its source
ID at +0x5C; RanSeq and the other container kinds return a constant true) were checked by Codex executing the
original ARM code under the emulator. I accept that as the oracle.

- **Shipped census:** only raw modes 0 and 1 occur, and every container has the shared-state bit.
- **Seed:** time(NULL), which the stack reproduces as Unix seconds (§3.2).
- **Continuous paths:** M6-008's.
- **Outside what the stack produces:** non-shipped banks, allocation failures and malformed weights.
- **Session split (a boundary, not a gap in this record):** the managed path keeps its own container history and LCG
  apart from the native runtime that plays robot animation audio. That ownership belongs to M9-015 and M6-025.

## TRACK-FACE-CALLERS-1: PASS. No record settled

- **Spot-checked:** the drive-tag stop at 0x00564F36..0x00564F42. When +0x9C is nonzero, FUN_00564A64 tests the tag and
  a released tag ends tracking, as built at NativeTrackFaceRunner.cs:75.
- **Correction to my rescope (bcda972e):** `FreeplayStack.CreateStandalone` gives its factory context only
  BouncerNativeRobot, so both Interact ids still bind the manual `InteractWithFacesBehavior` there. Codex migrated the
  two callers instead of deleting them, which was correct.
- **Removed:** the legacy `TrackFaceAction`. M14-003's location moves to NativeTrackFaceRunner.cs.
- **M14-003 stays open:** the generic stop predicate, enabled sound spacing and eye shift, the generic timeout, other
  modes, the end animation, the wider result mapping and the Init warning. The handoff maps each one.

## PLACE-GROUND-CALLERS-1: PASS. No record settled

- **Spot-checked:** FistBump's ground slot 0x005F1FB0..0x005F1FCA constructs `PlaceObjectOnGroundAction` (0x004A940C)
  and calls `StartActing` with an empty completion, as built.
- **One native ground action** now serves every caller, and the legacy `PlaceObjectOnGroundAction` class is removed.
  The callers are:
  - the internal 32 mm lift (Now, auto tag);
  - the AI lift (AtEnd, external tag);
  - FistBump;
  - manual Ram, Workout, Bring Beacon and Stack;
  - ManipTool.
- **M15-022 stays open:**
  - On a failed marker transform the source returns 0x03000005 (0x0054A3D8, 0x0054A65C..0x0054A6B8, 0x0054A704). The
    managed verify child skips the marker instead. That is a defined result, so it must be built (next jobs).
  - Pose precision and physical busy/release timing remain open.
- **M12-015:** the location moves to GroundPlacement.cs, with the same checked 0x44 payload.

## FACE-RETURN-ORIGIN-OWNER-FIX: PASS

It keeps main's id ≥ 1 bool and ascending order. The public predicate now uses the same owner-checked origin gate as
the list. It also fixes a real lock-order hazard: face-log callbacks ran under the face lock, so a subscriber that
queried could deadlock against a frame writer. M14-009 stays settled.

## Records

| Record | Verdict |
| --- | --- |
| M6-007 | **Settled EXACT_SOURCE** (above). |
| M14-003 | Open: generic ITrackAction items listed in the handoff. |
| M15-022 | Open: the 0x03000005 marker-transform failure is skipped; pose precision. |
| M12-015, M12-052, M14-001, M14-009 | Unchanged. |

## Progress

26 records settled (was 25). Today: M15-080, M6-005, M14-009, M6-007.
