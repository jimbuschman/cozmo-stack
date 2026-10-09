# M1: the final Opus pass

- **Date:** 2026-10-09
- **Verifier:** Opus `cozmo-verifier`, from the manager session, using `20261009-M1-verify-packet/`.

## Result

- **Settled:** M1-024, M1-044 and M1-015. Both earlier fixes are confirmed.
- **Not yet:** M1-050, M1-053, M1-047, M1-048, M1-046 and M1-029. Each record's `unresolved` has the detail.

**M1 isn't ACCEPTED yet.**

## The main finding

**M1-050 was the manager's mistake.** Slot +0x30 of the live external interface is
`UiMessageHandler::OnRobotDisconnected`. The manager re-read it: the ARM_ABS32 relocation at 0x0102FE4C binds that
symbol. All it does is exit SDK mode when SDK mode is active. With SDK mode unsupported, the native effect is nothing,
so the C# public event built on the app-boundary premise has no source and is removed.

## The rest

- **Engine logs not ported:** M1-053 (two), M1-047 (one) and M1-048 (three, inside its own ranges).
- **M1-046:** an omitted IMU file-logging close.
- **M1-029:** the port isn't in the packet. Its non-finite item is unreachable, with the evidence in the record.

## Leads

- **M15:** the C# `Detect` also writes `_prevBrackets`, and two C# callers skip SetPrev (see M1-024).
