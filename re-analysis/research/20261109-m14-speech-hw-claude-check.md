# M14-SPEECH-HW, the first two robot speech runs: Claude judgment (manager, 2026-11-09)

Bundles (small files committed; frames/messages/events kept by the operator):
- `acceptance/hardware/20261109-062751-M14-SPEECH-HW-b89f4d` (commit 2c47b191)
- `acceptance/hardware/20261109-072323-M14-SPEECH-HW-9dce1a` (commit c669866a)

Hardware evidence never raises provenance. No record status changes.

## Run 1 (b89f4d): slow, choppy speech

- **What the operator heard:** "slow, jizzery", "slow and chunky sounding".
- **All three requests:** admitted, with normal results (0x00000000 for the two played to the end, and 0x02000000 for
  the cancel).
- **Cause, measured from the wire:** the stack sent 0x8E robot-audio messages at 16.6 per second (first sentence) and
  19.1 per second (third), where the robot plays 30. The median gap was 55 ms, with 11 gaps over 100 ms. The robot
  logged `AnimationController.IsReadyToPlay.BufferStarved` 133 times.
- **Root cause, measured offline on the manager PC:**
  - The emulated Wwise render took 8.2 s for 3.95 s of audio.
  - Acapela synthesis took 10.2 s for 3.2 s of audio.
  - Profiling showed the Wwise render path passed instruction budgets to `uc_emu_start`, and a non-zero count makes
    Unicorn count every instruction. Without them the render took 4.3 s.
  - Acapela's 623,000 `memcpy` host round trips per sentence were removed too, without a measurable gain.
- **Fix:** c669866a. Outputs stay bit-exact; the full suite passed 8069/8069.

## Run 2 (9dce1a), after the fix

- **What the operator heard:** "sounds was better, not sure if it was still maybe a LITTLE too slow or not. might have
  been ok."
- **The wire:**
  - Both sentences went out at 28.8 0x8E messages per second, with a 33.5 / 34.1 ms median gap and one gap over
    100 ms each (147 and 171 ms).
  - **No BufferStarved** report from the robot.
- **The near-real-time margin is thin.** The emulated render runs at about real time on these PCs, and a slower PC
  would starve again.
- **Open, separately:** Acapela synthesis still finishes before playback starts, so the first audio arrives 9.4 s and
  6.3 s after the request. Faster emulation, or synthesizing ahead, is a later job.

## The cut-off sentence

The script cancelled after the first 0x8E message, so almost nothing was audible ("all i head was 'i can talk again'",
then "didn't hear it"). The cancel itself returned 0x02000000 both times. The script now waits for about one second
of audio (30 messages) before cancelling, so the next run can hear the cut.
