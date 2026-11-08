# Codex robot-speech, enrollment and lift batch: Claude check (manager, 2026-11-08)

**Verdict: all three PASS and merge (branch tip f147e6b0). No record settled, none added. FACE-ENROLLMENT-1 is partial
by design.**

Codex gate: `scripts/test-full.sh` on 342c6ff1, 7851/7851, no skips.

## VOICE-ROBOT-DELIVERY-1: PASS

- **The route:** `CozmoRobot.SayEnglishSentenceAsync` (direct English Sentence, neutral pitch) goes through the native
  AtEnd admission. It creates the live audio keyframe and animation child, and native Wwise renders through WavePortal and
  Hijack. The stack's existing streamer sends 0x8E/0x8F, 0x9B and the end under the 14-audio/8192-byte budget. Abort
  uses 0x8D.
- **Scoped:** `AnimationScheduler.PlaySpeech` accepts only the one live Sentence clip (event 0xBF0BACE1, time 0,
  volume 1), and the speech recipient is created only for robot-audio source 2. Every other animation keeps the old
  audio path.
- **Platform input:** the native OpenSL queue is paced by real WinMM completions on the PC's default device
  (`WinMmPcmSink`, 48 kHz stereo s16). The robot bus has dry 0, so the PC device receives the silent dry mix. This
  replaces Android's audio callbacks and is an app-boundary platform input. It is recorded with the M14-006 split below.
- **Ready for the operator's first robot speech run.** Codex's plan is in the handoff: a normal run, then a run that
  cancels after the first 0x8E. Packet lengths, order, budget and matching start/end are sourced. Sound and latency are
  observations.

## FACE-ENROLLMENT-1: PASS (partial)

- **Spot-checked:** FaceWorld::Enroll at 0x004F5CA0..0x004F5CA8 uses mode 4 for a nonzero ID and -1 for zero.
- **Built:** eligible updates, the remaining count and the one-shot world flag before observations. Still MISSING: the
  named enrollment behaviour (BehaviorEnrollFace, TrackFaceAction, name speech, save), so there is no public
  "learn this face" call yet.

## AI-BODY-LIFT-CONTROL-1: PASS

- **Spot-checked:** factory 0x0052AC40..0x0052AC76 compares the requested f32 to exactly 32.0. When carrying
  (`[+0x284]+8 != -1`) it allocates the 0x88-byte PlaceObjectOnGroundAction; otherwise it takes the ordinary 0xA4 lift
  action. The AI entry refuses only the exact-32-while-carrying branch, visibly.
- **Reentrancy guards:** ActionQueue and the runner skip a continuation when a host callback replaced or destroyed the
  runner during Update/Init, under M1-034. The native would continue through freed memory.

## Manager items

- **M14-006 split, with the WinMM platform input as its COMPATIBILITY_POLICY input:** to be done by Claude after the
  first robot speech run, so the records can cite hardware observations where relevant.
- **Next jobs (manager order),** from Codex's BLOCKED.md: VOICE-ANIMATION-FIT-1, TRACK-FACE-ACTION-1,
  AI-BODY-PLACEMENT-1.

## Progress

22 records settled (unchanged), 2 of 178 finished (unchanged), 761 records (unchanged).
