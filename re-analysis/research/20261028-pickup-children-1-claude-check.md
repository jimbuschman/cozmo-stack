# PICKUP-CHILDREN-1 — Claude check

Manager, 2026-10-28. Handoff `jobs/trial-m13/HANDOFF-PICKUP-CHILDREN-1.md` (branch bc801bea; implementation 997e2908,
teardown expectations 5fbe172b; Codex full gate 6487/6487). Verdict: **PASS. Settled: M12-029, M13-020 (EXACT_SOURCE).
M12-007: source claim confirmed, settlement held for the inventory correction (below).**

Records re-read on the branch manifest before acting: M12-007 "The two timed checks a pick-up is verified against",
M12-029 "The default +0x150 pose function, the DriveToHelper functor and RemoveMatchingPredockPose", M13-020
"PanAndTiltAction, TurnTowardsPoseAction::CheckIfDone and WaitForImagesAction", all IMPLEMENTATION_GAP.

## M12-007: the second timed check uses the motion timestamp

- 0x00553C88..0x00553C8E: virtual call `[vtbl+0x10]` (IsMoving) with out pointer sp+0x48, on an object with
  `[obj+0x40] >= 0` (0x00553C82..0x00553C86).
- Moving: 0x00553C98..0x00553CA0, `now <= firstVerify(+0x10C) + [+0x118]` stays running, else unattach (true).
- Not moving: 0x00553D0A..0x00553D22 picks +0x120 if `[+0x80]` else +0x11C, adds it to **[sp+0x48], the IsMoving
  timestamp**, and unattaches when `firstVerify > ts + timeout`.
- ActiveObject thunks: IsMoving 0x004E3860 (writes `[base+8]` to *out when non-null, returns byte `[base+4]`);
  SetIsMoving 0x004E3874 (stores both); the ctor zeroes both at 0x004E0C84/0x004E0C86.
- The C# change in `PickupObjectAction` replaces `target.LastObservedTimestamp` with the motion timestamp.

The record's frozen authority ("the last-observed time against a dock-action timeout") and approved inventory row
`M12-manipulation.md` row 27 (and M8 row 175 per the handoff) state the wrong operand. The operator approved the
corrected contract on Codex's side (`PICKUP-CHILDREN-1-CONTRACT-CORRECTION.md`). Settling needs the inventory and
authority corrected and the subsystem re-approved, which is an operator checkpoint, so it is not done in this check.

## M12-029 (native path)

Setter guard on the native runner (TriedToSetWhileRunning while Running); DriveToHelper functor (NativeDriveToHelper)
calls GetPossiblePoses, removes the single helper pose with RemoveMatchingPredockPose and clears in-position when it
removed one. The public `ExcludePoses` adapter (DriveActions.cs) removes one pose per failed attempt, a disclosed C#
generalisation owned by M12-004; it is outside this settlement. Location moved to NativeDriveToHelper.cs (tagged).

## M13-020 setters

- SetMaxPanSpeed 0x005498A0: `v == 0` restores `[+0x12C]` into +0x148 and leaves +0x160; `|v| > 5.2359877`
  (0x40A78D36) warns and `bfi` keeps the sign with the 0x40A78D36 magnitude (0x00549924..0x0054992C); then +0x160 = 1,
  store. NaN takes `ble` and is stored unclamped; the C# `Abs(NaN) > x` is false, so the same.
- SetPanAccel 0x005499AC, SetMaxTiltSpeed 0x00549AE8, SetTiltAccel 0x00549B0C: zero restores +0x130/+0x138/+0x13C,
  otherwise the flag (+0x160 / +0x161 / +0x161) and the value. The C# matches.

## Not settled

M12-007 (above); M12-010/011/017/019/022/034/037, M13-033/034, M13-014, M14-006 as listed in the handoff's leftovers.
