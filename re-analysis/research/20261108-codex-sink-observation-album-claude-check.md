# Codex live-sink, AI-observation and album-export batch: Claude check (manager, 2026-11-08)

**Verdict: all three PASS and merge (branch tip 821f8c02). No record settled, none added. VOICE-LIVE-SINK-1 is partial
by design; its MISSING items are in its handoff and stay open.**

Codex gate: `scripts/test-full.sh` on 412b6832, 7834/7834, no skips.

## VOICE-LIVE-SINK-1: PASS (partial)

- **Spot-checked:**
  - 0x009EBCD0..0x009EBCF4 (ARM) calls 0x009EB10C with r0 = 2, r1 = 0: output device ID 2. This corrects the previous
    handoff's "device ID 0".
  - Robot audio cursor 0x00597E80..0x00597E8C advances while the event time is `lo` (strictly below) elapsed, as built.
- **Production effect is small and sourced:** in `WwiseRobotAudioPath`,
  - Preparing polls the reset without falling through in the same update;
  - the offset gate also runs after the event cursor is exhausted;
  - a missing frame no longer advances.
  The buffer seam's default `TryPop` keeps the older implementations unchanged.
- **The native OpenSL host is not wired to production** (`NativeWwiseVoiceHost` and `NativeOpenSlBridge` are used only
  by their tests). Its output matches an independent probe: 100 x 744 frames, and the StopAll case.
- **Still MISSING:** the robot-buffer owner join (0x005984E8, 0x005985FC, 0x0059878C), packet budgets, 0x8E/0x8F/0x9B/0x8D
  delivery and public SayText. No audible speech yet.

## AI-BODY-OBSERVATION-1: PASS

- **Spot-checked:** the completion jump table at 0x0053FED4 (`tbb`, index = type + 2) sends head 0x12 and lift 0x13 to
  0x0053FF0E. The legacy head and lift actions now report completion variant 6. This closes the queued item from the
  last batch.
- **The observation is read-only:** the last published 109-byte state, an owner token and an advisory "can request a
  head action". Admission stays in the native queue.

## FACE-ALBUM-EXPORT-1: PASS

- **Spot-checked:**
  - temporary album 1000 x 4 at 0x00866F80;
  - at most ten named faces, skipping empty names (`cmp r7, #0xa`, the name-length test at 0x00866FAE..0x00866FC0);
  - the enrollment vector is written only when the album is non-empty (0x00867D5A..0x00867D66);
  - sizes at or above capacity fail (`bhs` at 0x006571F0 and 0x0065720E);
  - the second NV write is issued only when the first enqueue returned non-zero (`cbz r6` at 0x006573C0).
- **This replaces the last batch's skipped save:** stock export and save to the robot work again, now through the
  native export instead of writing back the restored bytes.
- **Known asymmetry, recorded in the handoff:** with more than ten names, the native export and restore disagree. That is
  the original's behaviour.

## Next jobs (manager order)

From Codex's BLOCKED.md: VOICE-ROBOT-DELIVERY-1 (Cozmo audible on the robot), then FACE-ENROLLMENT-1, then
AI-BODY-LIFT-CONTROL-1. The M14-006 split (Codex's proposal in HANDOFF-VOICE-LIVE-SINK-1.md) is added by Claude in the
VOICE-ROBOT-DELIVERY-1 check, when the whole speech path can be assigned.

## Progress

22 records settled (unchanged), 2 of 178 finished (unchanged), 761 records (unchanged).
