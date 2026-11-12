# HANDOFF LIFT-RELATIVE-OBSERVATION-PARENT-1 (Claude worker, 2026-11-11)

The census is `.scratch/LIFT-RELATIVE-OBSERVATION-PARENT-1-census.md`: 17 rows, none UNKNOWN.

## What changed

This is CarryingComponent::SetObjectAsAttachedToLift (0x00632CC4) when a cube is on top of the carried cube. Before this
job, LiftGeometry.cs did two things wrong:
- it threw MISSING M11-066 on a root mismatch;
- it passed a parentless world pose to AddObjectRelativeObservation.

| Step | Address | Stack |
| --- | --- | --- |
| tmp = Pose3d(""); GetWithRespectTo(top pose, carried pose, tmp), on the objects' own nodes, non-throwing | 0x00632F76..0x00632FB6 | `LiftGeometry.SetObjectAsAttachedToLiftCore` |
| Success: info "Unnamed" / "Robot.SetObjectAsAttachedToLift" / "Setting object %d on top of carried object as also being carried." | 0x00632FC0..0x00632FE0 | same |
| SetParent(carried pose); AORO(top, tmp, objSeen = carried) | 0x00633006..0x00633020 | same |
| AORO, in this order: SetLocalizedTo(null) if localized to top; old pose and state; SetPose(pose, -1, Dirty) keeping the parent; broadcast; then entry pose = pose, entry time = objSeen's last-observed time; return 0 | 0x00506D02..0x00506DEC | new `BlockWorld.ApplyObjectRelativeObservation` |
| +0x14 = top ID | 0x0063318C..0x00633190 | same |
| Failure: warning "Robot.SetObjectAsAttachedToLift" / "Found object on top of carried object, but could not get its pose w.r.t. the carried object."; no AORO; +0x14 kept; the attach continues | 0x00633154..0x0063318A | same |

**AORO before this job:** the entry was written before the broadcast, the old pose came from the public snapshot, and
neither SetLocalizedTo nor the entry time was there.

**The broadcast now sees the engine's representation:** the top is parented to the carried cube's node. Before, the top
became its own root during its broadcast.

**Stack-only machinery kept (verifier: no effect on 1–4):**
- `TopWrtCarried`, which Docking reads, is set to tmp.
- AORO sets the top's `NativeCarryingTree`, so its public `Pose` stays a world pose. API payloads read `NativePose`
  (NativeObjectStateReports.cs:15, MapTiming.cs:85-101). Docking already set this flag in the same gate region.
- Docking's later re-parent (Docking.cs:609/:631) now writes identical values. Its order relative to the engine is
  census Q1 and is unchanged.

## Tests

All `reach: unit`.
- **`M12_008_TheCubeOnTopIsObservedParentedToTheCarriedCubesOwnPose`:** checks the parent identity, the Dirty state, the
  world pose (an invariant), the entry parent and time, the top ID and the info log.
- **`M12_008_ACubeOnTopInAnotherRootIsWarnedAboutAndTheAttachContinues`:** a substituted unrelated root, which the
  engine's finder never returns. Checks the warning, that there is no observation, that the ID is kept and that the
  attach succeeds without a throw.
- **`M12_008_TheRobotLocalizedToTheCubeOnTopIsUnlocalized`:** the localization is made by a reflection call to
  `SetLocalizedToCore`.
- **Mutation check:** without the SetLocalizedTo step or the entry time, two of these tests fail.
- **Two existing callers** of the old signature now pass objSeen = the object (declared).

**Results:**
- implementer: 827/827 on the wide filter;
- verifier: 542/542;
- full gate: see the commit.

## Verifier: PASS (no blocking findings)

| # | Finding | Class | Resolution |
| --- | --- | --- | --- |
| 1 | M11-037's private AORO copy (UpdatePoseOfStackedObjects) omits SetLocalizedTo(nullptr) | queued | Recorded on M11-037's own `unresolved`; not built. |
| 2 | Tests still drive `BlockWorld.ApplyRelativeObservation`, which has no production caller now (M3M4X23BuilderTests, M13SetPoseCacheTests, M13ChargerOldPoseFixupTests, M3M4SettlingBuilderTests). The new method follows the same lock discipline | queued | For a later cleanup: re-point them at `ApplyObjectRelativeObservation`, or remove the old method. |
| 3 | The dead nonzero-AORO branch comment is correct | queued | No change. |
| 4 | The stack's finder considers every origin and has no same-root predicate (M13-007, 0x0062BA2C..0x0062BA48). That makes the warning branch, and AORO's unbuilt NotABlockWorldObject branch (0x00506D56..0x00506DA4, visible MISSING, writes nothing), reachable in the stack | observation | Proposed as FIND-ON-TOP-SAME-ROOT-1. |
| 5 | `_topWrtCarried` is never cleared, but it is only used together with `_onTopId`, which is null on every reachable entry | observation | No effect. |
| 6 | NativeCarryingTree (see above) | observation | No effect on 1–4. |

## Records

- **M12-008:** `unresolved` and `test` only. The stale items (b) and (d) are reworded. No settlement.
- **M11-037:** `unresolved` note (finding 1).
