# Codex TrackFace request and pose-queue batch: M11-063 settled EXACT_SOURCE (Claude check, manager, 2026-11-09)

Merged from `codex-m13-manager-2` at b12a1cf0. Codex's gate on 6df5c906 passed 8104/8104.

## AI-BODY-TRACK-FACE-REQUEST-1: PASS

- **The factory at 0x0052ACC0..0x0052ACE6, re-disassembled:**
  - It allocates 0x124 bytes and calls the TrackFace ctor with the 32-bit ID from `[msg]`.
  - Only when the head-only byte `[msg+4]` is set does it call SetMode(1).
  - **SetMode at 0x00564B8C:** mode 0 → mask 5, 1 → mask 1, 2 → mask 4.
- **The stack:** `QueueAiTrackFaceAsync` builds the runner with the default 2° tolerance (0x3D0EFA35), no clamp, no
  companion tag, and mask 5 or 1. That matches.
- **Completion:** variant 3 returns the final signed face ID.
- **Records:** none settled here. The record that owns this action, M14-003, was settled earlier today.

## BLOCKWORLD-POSE-QUEUE-SETTLEMENT-1: PASS; M11-063 → EXACT_SOURCE

**OnObjectPoseChanged (0x00624808..0x006248B8), re-disassembled:**

| Native step | Address |
| --- | --- |
| Flag test, `ldrb [r5,#0x74]` | 0x00624814 |
| ID scan of the list at +0x68 | 0x00624820..0x0062483A |
| Duplicate: log and skip | 0x0062483C..0x0062487A, then 0x00624888 |
| Old state 0 (`cbz r3`): log, no append | 0x0062489C..0x006248B8, then back to 0x00624858 and 0x00624888 |
| Otherwise append | 0x00624884 |
| Every path, then the configuration setter | 0x00624888..0x00624892 |

**The writer:**
- 0x00624F78 clears the list, then 0x00624F7E sets the flag to 1.
- 0x006250CA..0x006250D0 runs the stacked-pose and unobserved recipients.
- 0x006250D8 sets the flag to 0.

**The stack:** `BlockWorld.BroadcastObjectPoseChanged` matches branch for branch:
- the window flag;
- the duplicate test against the queued IDs;
- the Unknown (0) old state logs FirstPoseForObject;
- otherwise it appends a copied pose;
- the setter always runs.

**What the tests exercise:** the frame-window tests run real marker detection. The cliff test uses actual F0/C0
messages with the flag clear.

**Not part of this record (each has its own):** the consumers (M11-037), the failure tail (M11-064), the localization
producer (M11-044) and pose lifetimes (M11-077). No host libm is involved, so the status is EXACT_SOURCE.

## Next jobs

Codex's two proposals in BLOCKED.md are released as queue rows: AI-BODY-TURN-TO-FACE-1, then
DOCK-CORRECTION-CLAMP-SETTLEMENT-1 (M12-019).
