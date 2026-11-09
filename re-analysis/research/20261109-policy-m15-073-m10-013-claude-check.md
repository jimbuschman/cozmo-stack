# M15-073 and M10-013 to COMPATIBILITY_POLICY: Claude check (manager, 2026-11-09)

**Verdict:** both records move to COMPATIBILITY_POLICY, on the operator's approval of 2026-11-09 ("approve M15-073
and M10-013"). M10-derived and M15-freeplay were re-approved and frozen with `fidelity.py --approve`. The other
status changes in those snapshots are records already settled there (M15-080, M15-022 and the earlier M10 one).

## M15-073: NotifyCozmoWakeup, the app input

- **What the stack sends:** NotifyCozmoWakeup (G2E 166), the input the app used to send. It goes out once per
  connection, when Success and `IsReadyDelivered` both hold (CozmoRobot.cs `TryAutomaticWake`), through the same
  subscription the engine uses (SevereNeedsComponent::Init 0x00572A64..0x00572A86, callback 0x005734F2..0x00573504).
- **Host API:** the call `NotifyCozmoWakeup()` stays.
- **History:** built in B-M15-12 and checked in `research/20261025-b-m15-12-claude-check.md`. The operator decided
  the auto-send on 2026-10-25.
- **Not this record:** the wake handler itself is M15-072. The app's wake animation is app flow.

## M10-013: raw accel and gyro before the first RobotState

- **The engine:** it never initialises Robot+0x360..+0x374. The robot object comes from `operator new(0x530)` with no
  memset and no constructor store (0x0052EE8E..0x0052EE9C), so those fields hold leftover heap contents until the
  first RobotState arrives.
- **The stack:** it uses 0 there (MD1, OffTreads.cs). That is the M1-034 standing rule's case of a read of
  uninitialised memory, recorded as a policy.
