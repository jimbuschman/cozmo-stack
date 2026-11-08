# Codex fitted-voice, TrackFace and placement batch: Claude check (manager, 2026-11-08)

**Verdict: all three PASS and merge (branch tip 5bd556b8). No record settled, none added.**

Codex gate: `scripts/test-full.sh` on 80c933d5, 7869/7869, no skips.

## VOICE-ANIMATION-FIT-1: PASS

- **Spot-checked:** the fit target at 0x0069EA26..0x0069EA4C converts both u32 values to f64, divides, multiplies by
  the literal 1000.0 (0x0069EB30) and narrows once to f32 (`vcvt.f32.f64`), as built.
- **Scoped:** `SayEnglishNameWithAnimationAsync` pairs only Name_Normal with CozmoSaysSpeakLoop and fit enabled. The
  direct Sentence path and the canned callers (pickup, enrollment, fit disabled) are unchanged.
- **Still MISSING:** arbitrary fitted triggers, extra authored audio, shared Wwise mixing, canned name callers and the
  app choreography.

## TRACK-FACE-ACTION-1: PASS

- **Spot-checked:** the head command at 0x005650E2..0x00565104 divides the delta by the speed field ([fp+0xd0]) and
  passes acceleration 0x461C4000 = 10000.0, as built.
- **Scoped:** the native TrackFace runner, used by the final Come Here stage only. Tracking reads the native face
  position under the historical camera; the public face pose is separate. Completion variant 3 carries the face ID.
- **Still MISSING:** the enrollment scan compound, Interact With Faces, the earlier Come Here stages, other tracking
  target types, and direct standalone tracker callers.

## AI-BODY-PLACEMENT-1: PASS

- **Spot-checked:**
  - the constructor at 0x00554638..0x0055469C passes type 0x18 and mask 7;
  - Init at 0x00632A88..0x00632AC4 skips when Carrying+8 is -1, clears Docking+5, passes three zero pose fields, and
    the speed table at 0x00C7CD90 holds the f32 values 100, 200, 500.
- **Code reviewed:** `QueueAiLiftHeightAsync` routes the exact 32 mm carrying request to `NativePlaceGroundRunner`
  only when the original manipulation, docking, behaviour-manager and external-interface owners exist; otherwise it
  refuses visibly. The turn helper's new owner checks stop a retired robot's verification from reaching its successor.
- **Bring Beacon:** the first gate exposed a lost availability guard in the Ground leaf. It was restored; the
  regression and affected suites pass.
- **Withheld as before:** the four undefined completion words (M12-052/M1-034).

## Next jobs (manager order)

From Codex's BLOCKED.md: AI-BODY-DRIVE-CONTROL-1 (the AI can drive), then VOICE-NAMED-REACTION-1, then
FACE-ENROLLMENT-CHOREOGRAPHY-1 (it depends on authored name speech, which the named reaction exercises first).

## Progress

22 records settled (unchanged), 2 of 178 finished (unchanged), 761 records (unchanged).
