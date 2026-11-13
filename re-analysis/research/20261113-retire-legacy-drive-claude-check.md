# RETIRE-LEGACY-DRIVE-API-1 merged; M12-004 moved to IMPLEMENTATION_GAP (manager, 2026-11-13)

## The job

Worker tip 981cb9ac (job commit fe98dc26, worker full gate 8372/8372). Handoff:
`re-analysis/jobs/trial-m13/HANDOFF-RETIRE-LEGACY-DRIVE-API-1.md`. Operator decision: retire and rebind (BLOCKED.md,
2026-11-13); manager Q3–Q6 in `research/20261113-worker-jobs-10-13-m11-009-claude-check.md`.

**Re-read in the `.so`:**

| Address | What | Matches the handoff |
| --- | --- | --- |
| 0x00626048 | `bl 0x4DF628` (GetWithRespectToRoot), then `ldr r6,[r0,#0x50]` at 0x0062604E: the candidate's vtable +0x50 (GetBoundingQuadXY) | yes: the charger's +0x50 is unread (M13-025), so CHARGER-BOUNDING-QUAD-1 is real |
| 0x005BEE56 | IBehavior::UseSecondClosestPreActionPose entry (`push {r4,r5,r6,lr}`) | yes: M12-004's function; its engine caller is not in the inventory |
| 0x005B8192..0x005B8198 | `ldr r0,[sl,#0x108]; cmp r0,#1; bls` (the attempts boundary) | yes: M12-009's tag moved with NativePickupBlockHelper |

**Verdict: PASS.** Every removed branch was reachable only through the legacy entry, a null native planner (excluded
by H1) or `_ownsNativePoses` false, so the robot runtime's traffic is unchanged. The standalone stack's ten drive
behaviours now bind the native classes through a narrow filter; every other class binds as before.

## M12-004: EXACT_SOURCE → IMPLEMENTATION_GAP

Its only built form was the legacy DockHelper's `DriveToObjectAction.ExcludePoses`, retired with the legacy flow (Q4:
not kept alive for it). No native runner or helper sets an exclusion, so the EXACT_SOURCE claim no longer has code
behind it. The inventory row already read IMPLEMENTATION_GAP; the manifest is brought in line and M12-manipulation is
re-approved (standing authorisation; a weaker claim, cited). Robot traffic did not change: the robot runtime never set
the exclusion. Job USE-SECOND-CLOSEST-NATIVE-1 reads the engine caller of 0x005BEE56 and builds it on the native
pickup helper's retry.

Counts: 289 EXACT / 17 EQUIV (one fewer EXACT). No record settled.

## Follow-ups released

- **CHARGER-BOUNDING-QUAD-1** (worker proposal): with a charger located, the native roll-target search throws on the
  charger's unread GetBoundingQuadXY, on the robot runtime too. A live defect; released.
- **USE-SECOND-CLOSEST-NATIVE-1:** M12-004 above.
- **HARDWARE-CATALOG-T-R-JUDGES-1:** the T (`--knockover`) and R (`--stack`) judges still look for
  `KnockOver success=yes` / `StackBlocks success=yes` (HardwareCatalog.cs:1022), which ManipTool no longer prints. Rewrite
  them to observed, cited signals before the operator runs T or R.
- **Repoints, queued cleanup:** M13-014, M13-015 and M12-012 have no tag at their location; M15-051 has no test.

## For the operator

SparksPickUpCube (PickUpAndPutDownCube) and PyramidRespondPossiblyRoll (RespondPossiblyRoll) have no native class, so
the standalone stack no longer binds them. The robot runtime never bound them. Recorded in M8-011; building them is a
separate decision.
