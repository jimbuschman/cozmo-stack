# PUTDOWN-BLOCK-1 Claude check (2026-10-27): PASS; M15-075 settled

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

Second pass, after the operator approved the new record (2026-10-27):

| Step | Binary | C# |
|---|---|---|
| IsRunnableInternal | 0x005C7FB0..0x005C7FC8: carried id != -1 -> true, else this+0x84 != 0 | `RawCarry != uint.MaxValue \|\| HasCurrentAction` |
| Init order | lock 0x005C7FE2, draw, drive, animation, sequential, score 5, StartActing(&LookDownAtBlock) 0x005C8096 | `OnStart` in that order |
| LookDownAtBlock | CreateLookAfterPlaceAction(robot, true) 0x005C80F6, score 5, StartActing with callback 0x005C811C | `LookDown` (the true argument adds the keep-alive face turn, 0x005C8264) |
| Release callback | 0x005C8480..0x005C84D4: still carrying -> sWarningF, SetCarriedObjectAsUnattached(false) | match |
| Head child 15/20 | MoveHeadToAngleAction ctor defaults 0x41700000/0x41A00000 at 0x00547F18..0x00547F20 | `CreateNativeHeadChild(..., 15f, 20f)` |

## Records

**Settled: M15-075** (new, operator-approved 2026-10-27), the PutDownBlock lifecycle. Its `unresolved` names what it
does not cover: the look action's contents (M15-012), the face turn (M13-027), the release's own effect (M12-006) and
the face data (M14-008).

Originally nothing settled. The handoff proposes a NEW record ("Configured PutDownBlock lifecycle and owned action flow",
0x0059CEE6..0x0059CF04 / 0x005C7F98..0x005C84D4). M15 is an approved subsystem, so a new record needs the operator's
inventory approval before it can be added and settled. M15-012, M13-027 and M14-008 stay IMPLEMENTATION_GAP: each
still has a manual or host-input part the handoff names as open.
