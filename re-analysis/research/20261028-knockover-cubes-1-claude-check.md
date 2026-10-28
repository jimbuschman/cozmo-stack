# KNOCKOVER-CUBES-1 — Claude check

Manager, 2026-10-28. Handoff `jobs/trial-m13/HANDOFF-KNOCKOVER-CUBES-1.md` (branch 74216d25; implementation
741cf8bf/ae48b85a; Codex full gate 6513/6513 on bc3a2254). Verdict: **PASS. Settled: M13-002 (EXACT_SOURCE).**

Record re-read on the branch manifest: M13-002 "FlipBlockAction's constants, their roles and IAction type",
IMPLEMENTATION_GAP.

## M13-002

- Ctor: IAction type 0xF (0x0055ECA8); `strd` 150.0/20.0 into +0x12C/+0x130 (0x0055ECFA); `stm` 45.0/40.0/-1 into
  +0x134..+0x13C (0x0055ED0E); +0x140 = 1 (0x0055ED10). `NativeFlipBlockRunner` matches.
- CheckIfDone 0x0055F124..0x0055F16A: `vcmpe dist, [+0x138]`, `bpl` skips (so NaN skips); +0x13C == -1;
  MoveLiftToHeightAction(robot, 2, 5.0f); `[+0x56] = 1`; tag stored in +0x13C; `QueueAction(5, lift, 0)`.
  QueueActionPosition 5 is `InParallel` in the C# enum. The runner matches, including the suppressed track locking.
- Settled with its location moved to `NativeFlipBlockRunner.cs` (tagged). The evidence's "speed 5.0" is the
  tolerance argument (recorded in the settlement note; evidence is frozen).

## Spark flag +0xD8: the approved M13 inventory is reversed (confirmed)

- SetRequestedSpark 0x005A3EB2 stores the bool at requested+0x64; 0x005A3ED4..0x005A3ED8 picks r1 when non-zero.
  The literal pools resolve r1 -> 0xBF0B78 "soft" and r2 -> 0xBF0B7D "hard".
- SwitchToRequestedSpark copies +0x64 to active+0x5C (0x005A421A/0x005A4226).
- IBehavior::Init 0x005BCCAE..0x005BCCC2: spark 0x55 (none) gives +0xD8 = 0, otherwise +0xD8 = +0x5C XOR 1.

So +0xD8 = 1 means an active **hard** spark. Approved `inventory/M13-navigation.md` Q8 and M13-014's evidence say
soft. The operator approved the corrected contract on Codex's side; the formal inventory/evidence correction and
re-approval of M13-navigation are an operator checkpoint and are not done in this check. M13-014 is not proposed
for settlement in any case (its public/manual path is still partial).

## Not settled

M13-014, M13-028, M12-035 (partial, per the handoff); M13-033/034; M11-007/038/042. The earlier manual Navigation
KnockOver fault was not reproduced and has no established cause; it stays a named uncertainty.
