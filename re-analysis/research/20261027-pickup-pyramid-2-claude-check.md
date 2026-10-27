# PICKUP-PYRAMID-2 Claude check (2026-10-27): PASS; M12-018 settled

Branch `codex-m13-manager-2` (handoff `jobs/trial-m13/HANDOFF-PICKUP-PYRAMID-2.md`, gate candidate d71e12c7).

## Settled

**M12-018** (The dock abort path): IMPLEMENTATION_GAP -> EXACT_SOURCE.

| Step | Binary | C# |
|---|---|---|
| Build | 0x0063BE2A EngineToRobot(AbortDocking&&) | `new AbortDocking()` |
| Send | 0x0063BE32..0x0063BE36 Robot::SendMessage(msg, 1, 0) | `SendMessage(message, reliable: true)` |
| State | none written; 0x0063BE3E ClearCurrent, return the send result 0x0063BE52 | no `_pending`/`_active` writes |
| Live caller | ~AlignWithObjectAction 0x0055745C deselect, 0x00557464..0x00557470 path abort if active, 0x00557474..0x0055747C AbortDocking only when docking is active, then marker/target reset | `NativePickupObjectRunner.WatcherEnding`: same order and gates |

The return differs in type (bool vs Result); no caller reads it on either side.

## Accepted without settlement

M13-020 (its four setters 0x005498A0/0x005499AC/0x00549AE8/0x00549B0C are unbuilt), M12-007/017/019/034/037 and the
named speech, DriveTo and Search branches stay open, as the handoff says. Not re-read here beyond the cleanup order above;
the rest rests on Codex's independent check.
