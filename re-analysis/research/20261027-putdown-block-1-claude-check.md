# PUTDOWN-BLOCK-1 Claude check (2026-10-27): PASS, nothing settled

Branch `codex-m13-manager-2` (handoff `jobs/trial-m13/HANDOFF-PUTDOWN-BLOCK-1.md`, gate candidate 0ffe53aa).
Spot-checked `NativePutDownBlockBehavior.cs` against 0x005C7F98..0x005C82CC (Capstone Thumb):

| Step | Binary | C# |
|---|---|---|
| Backup draw | 0x005C7FEC..0x005C8002: RandDblInRange(-45.0 (pool), -75.0 (0xC052C000 hi word)); 0x005C801E vcvt.f32.f64 | `(-75-(-45))*u + (-45)`, one f64 draw, one f32 narrowing |
| First drive | 0x005C802C speed 0x42C80000 = 100, flag 1 | `DriveStraightAction(BackupDistance, 100f, true)` |
| Put-down animation | 0x005C8050 trigger 0x19A, 60.0f (0x42700000), loops 1 | `PutDownBlockPutDown`, 1, 60f |
| Score while acting | 0x005C807C 5.0f (0x40A00000) | `IncreaseScoreWhileActing(5f)` |
| Look down | head Radians 0xBEB2B8C2 (-20 deg), tolerance 0x3D0EFA35 (2 deg) 0x005C81A6..0x005C81BE; drive -30.0f (0xC1F00000) 0x005C81EE | match |
| Image wait | 0x005C8242 (2, mode 1) | `NativeWaitForImagesRunner(_m, 2, 1, 0)` |
| Keep-alive and face | 0x005C828E trigger 0x199, 60f; turn PI (0x40490FDB) 0x005C82A0 | match |

The head child's speed arguments (15, 20) and the 0x005C8480..0x005C84D4 release callback were not re-read here;
they rest on Codex's independent check.

## Records

Nothing settles. The handoff proposes a NEW record ("Configured PutDownBlock lifecycle and owned action flow",
0x0059CEE6..0x0059CF04 / 0x005C7F98..0x005C84D4). M15 is an approved subsystem, so a new record needs the operator's
inventory approval before it can be added and settled. M15-012, M13-027 and M14-008 stay IMPLEMENTATION_GAP: each
still has a manual or host-input part the handoff names as open.
