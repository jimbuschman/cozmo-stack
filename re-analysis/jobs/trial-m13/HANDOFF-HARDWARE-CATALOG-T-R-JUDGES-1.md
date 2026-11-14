# HANDOFF HARDWARE-CATALOG-T-R-JUDGES-1 (Claude worker, 2026-11-13)

Job 18, harness only. Census: `.scratch/HARDWARE-CATALOG-T-R-JUDGES-1-census.md`; implementation report:
`.scratch/HARDWARE-CATALOG-T-R-JUDGES-1-impl.md` (worker-local). No hardware script or bundle was created.

## The judges

Both now pass only on the engine's behaviour-objective broadcast (tag 0x8B, one uint32), as U does with PoppedWheelie:

- **T (`--knockover`, SparksKnockOverCubes, M13-014):** objective 13 KnockedOverBlocks. PlayingReaction fires it only
  when the tipped set is non-empty (0x005C3950 ldr [r4,#0x14c]; 0x005C3954 cbz; 0x005C3958..0x005C395C
  BehaviorObjectiveAchieved(13,true)). The empty set plays KnockOverFailure with no objective. The tipped set is fed by
  the 0x11 up-axis broadcast (M4-009, now in T's record list).
- **R (`--stack`, StackBlocks, M15-076):** objective 40 StackedBlock, fired only in TransitionToPlayingFinalAnim
  (0x005C9EA4..0x005C9EA8), which is reached only from the PlaceRel helper's success callback (GOT 0x103F248, loaded
  only at 0x005C95A0). The behaviour never reads the world-model stack; carrying clears only through PlaceRel's own
  Verify (0x03000017 while carrying, 0x005555BA..0x005555E0).
- **Broadcast (M7-019):** only with broadcast flag 1 and an external interface (0x005BE22E..0x005BE246); tag
  0x007285FE. Names from the table at 0x1033140.

The verifier confirmed the stack delivers both under ManipTool: NativeStackBlocksBehavior.cs:121 and
NativeKnockOverCubesBehavior.cs:146-152 report the objectives, BehaviorObjectiveReport.cs delivers 0x8B, and
`EngineRobot.HasExternalInterface` defaults to true with no writer under Conformance.

ManipTool prints `KnockedOverBlocks objective observed=yes|no` / `StackedBlock objective observed=yes|no`; stacks,
tipped, attempts, completed actions, carrying, the world-model stack and NeedsState ids print as `observation:`.

## Records lists

- T: M12-041, M13-014, M13-002, M7-019, M4-009.
- R: M12-041, M15-076, M7-019, M12-009, M12-001, M12-003, M12-005, M12-012, M13-007, M12-028, M12-042. Dropped
  M12-015 and M15-012 (PlaceObjectOnGround is reached only from the failure path).

## Cleanup (QUEUE row)

- `location` repointed: M13-014 -> NativeKnockOverCubesBehavior.cs; M13-015 -> NativePopAWheelieBehavior.cs; M12-012
  -> Manipulation/Docking.cs (also tagged in NativePickupTargetCache.cs and CubeGeometry.cs).
- M15-051 already records that it has no test.
- M7-019: a correction note (the objective is now broadcast by BehaviorObjectiveReport.cs, not only logged).
- NativeStackBlocksBehavior.cs is tagged M15-076.
- `BehaviorObjective` gains KnockedOverBlocks = 13 and StackedBlock = 40 (FromString unchanged).

## Tests

`HardwareRunnerTests.KnockOverAndStackJudgeOnlyTheEngineObjectiveBroadcast` (reach: unit): the objective line alone
decides; observations can't pass or fail it; the old `success=yes` lines fail. HardwareCatalog/HardwareRunner 99/99.

## Verifier: PASS

Two queued items, both fixed: the carry-state wording (PlaceRel's Verify does depend on carrying having cleared) and
M4-009 missing from T's list. Note: no automated test asserts that NativeStackBlocksBehavior's Final() sends 0x8B 40;
the hardware run R exercises it.

No record settled.
