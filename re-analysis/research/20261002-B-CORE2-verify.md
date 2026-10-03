# B-CORE2: Opus verification, and a calibration of Codex's review

- **Date:** 2026-10-02
- **Verifies:** B-CORE2 (aeaf977, bd12929, 4d960de, e0f4333, caaef86); code at HEAD 06e9e0f
- **Cross-check:** Codex's review, `20260930-B-CORE2-review.md`
- **Verifier:** Opus `cozmo-verifier`, from the manager session.

## Result

Codex marked 9 records HOLDS. Of those, **2 settle**: M3-025 and M3-035. The other **7 are not yet**: M3-031, M3-026,
M3-030, M1-041, M4-020, M4-001 and M1-024.

The defects Codex missed:
- log texts and formats: tags printed X8 where the engine prints %x, logs missing, and an invented clip-warning text;
- `Robot.SendSyncTime.FailedToSend`, which B-CORE2 deleted although the engine does emit it for a SyncTime or
  InitController failure;
- the +0xE8 dependency on M3-027 in every NV command;
- the Needs decay, which is piecewise per minute in the engine; the C# uses one rate.

Each record's `unresolved` has the detail.

## Codex calibration

- **Its defect claims are mostly real.** It reported 10 DEFECT records. The three the verifier checked:
  - M3-010: confirmed;
  - M4-016: confirmed, but the "second gate" can't fire for these actions;
  - M2-002: confirmed, but the action isn't stuck "forever": the 30 s IAction timeout fails it with 0x03000018.

  The other seven are recorded as Codex's findings, unchecked by Opus.
- **Its HOLDS are not reliable.** 7 of 9 failed, on log text, formats, cross-record dependencies and one real
  arithmetic contradiction.

**Rule from here:** a Codex HOLD never settles a record. A Codex DEFECT is a lead, demoted after the manager
spot-checks it.

## Facts the verifier settled

- **The NV resend count** is 7 resends and 8 transmissions. The counter at +0xF4 is incremented, then compared
  unsigned with +0xF5, which the constructor sets to 8 (0x006428A8/0x006428B8).
- **The action timeout's extra gate is dead for these actions.** For MoveHeadToAngle, MoveLiftToHeight and
  PlaceObjectOnGround, slots 0x24 and 0x28 return 0.0 (0x0052B0BA/0x0052B0BE) and slot 0x2C returns 30.0f
  (0x0052B0C2).
