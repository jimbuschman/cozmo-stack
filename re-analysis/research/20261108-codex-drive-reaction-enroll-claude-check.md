# Codex drive, named-reaction and enrollment batch: Claude check (manager, 2026-11-08)

**Verdict: all three PASS and merge (branch tip 9597f2f8). All three are partial by design. No record settled, none
added.**

Codex gate: `scripts/test-full.sh` on e3bed520, 7890/7890, no skips. The first attempt failed one test (the Hiking
Pounce fixture listened to a retired diagnostic); it now listens to the recovered WaitingForPathStart event.

## AI-BODY-DRIVE-CONTROL-1: PASS (partial)

- **Spot-checked:**
  - explicit constructor 0x0054728C..0x00547308: a speed below -1e-5 logs a warning and is negated, then a distance
    below -1e-5 negates the stored speed again (`vnegmi`), as built;
  - destructor 0x00547530..0x0054755E: aborts the shared path whenever it is active, then always tears down the shared
    driving-animation handler, as Codex reports.
- **Internal only:** no public AI drive command yet.
- **Cancellation decision (manager):** the public command keeps the engine's defined behaviour. Cancelling a drive
  aborts the active path and tears down the shared driving animation, as the native destructor does. This needs no
  departure. Cancelling only the drive's own path would be a COMPATIBILITY_POLICY and needs the operator; it is not
  taken.

## VOICE-NAMED-REACTION-1: PASS (partial)

- **Spot-checked:** the transition at 0x0054C5D2..0x0054C5D8 installs the child and stores state 3. On the next tick,
  0x0054C606..0x0054C638 updates the child, returns on a non-zero result, and otherwise sends needs action 0x29 before
  marking the face turned.
- **The named branch stays withheld, visibly.** The shipped AcknowledgeFaceNamed clips post several other audio events
  besides the speech event, so it needs one shared robot audio owner for authored events and speech.

## FACE-ENROLLMENT-CHOREOGRAPHY-1: PASS (partial)

- **Spot-checked:** 0x00863E94..0x00863EAA. When the enrollment target (+0x100) is non-zero, the entry's current and
  previous IDs differ, and the target equals the previous ID, only the target is rewritten to the current ID, as
  built.
- **Still MISSING:** the stock activation and chooser, scan ownership, name assignment, authored speech and music, the
  save callbacks and the final 0x56 report. No public "learn this face" request.

## Next jobs (manager order)

1. **AI-BODY-DRIVE-PUBLIC-1:** a public AI straight-drive request through the checked native path, with the engine's
   cancellation behaviour.
2. **VOICE-SHARED-ROBOT-AUDIO-1** (Codex's proposal).
3. **VOICE-CANNED-NAME-DELIVERY-1** (it depends on 2).
4. **MEET-COZMO-ADMISSION-1.**

## Progress

22 records settled (unchanged), 2 of 178 finished (unchanged), 761 records (unchanged).
