# HANDOFF FIND-ON-TOP-SAME-ROOT-1 (Claude worker, 2026-11-12)

The census is the worker's local `.scratch/FIND-ON-TOP-SAME-ROOT-1-census.md`; its summary is below.

## The engine

**FindObjectOnTopOrUnderneathHelper (0x0062601C..0x0062619A)** is reached from 11 call sites through PLT 0x004A7144:
- It copies the caller's filter and adds obj's ID to the ignore set (0x006260AC..0x006260B8).
- It appends predicate 0x0062B9FC and returns the first match of the current-origin walk (originMode 0, 0x0061EBF2..0x0061EC2A).
- The walk runs in (origin, family, type, ID) order (0x0061EB78).

**The predicate, in order:**
- **P1:** GetWithRespectTo(candidate, obj's root). On failure (a different root, or a null node) the candidate is skipped with no log (0x0062BA0E..0x0062BA48).
- **Then the quad test** (x and y only), followed by the height test.

**All of it is in the root frame:**
- refPose = GetWithRespectToRoot (H2, 0x00626048);
- targetZ = refPose.z + dimZ·(±0.5) in f32 (H4, 0x0062605E..0x006260A0);
- the candidate's root-frame z and dims (P5/P6, 0x0062BA4A..0x0062BB0E);
- tolerance + 1e-5f (literal 0x3727C5AC at 0x0062BB5C).

## What changed

| Where | Change |
| --- | --- |
| BlockWorld `FindObjectOnTopOrUnderneathHelper` | P1's same-root test (`SharesReferenceRoot`), current origin only, first match in the walk order. Its geometry stage is the new root-frame `BlockConfigurationManager.FindObjectOnTopOrUnderneathInRootFrame`. New `FindObjectOnTopOrUnderneathDefaultFilter` (the engine's default filter). |
| C1 MarkObjectUnknown, C8 UpdatePoseOfStackedObjects | Use the helper. C8's after-find OriginId check is replaced by P1. |
| C9 ClearLocatedObjectHelper (0x00625E1A) | The default filter only; it no longer excludes families 6/7. |
| C10 lift attach (LiftGeometry.cs, 0x00632F14), C11a docking (Docking.cs, 0x0063C730) | Use the default-filter helper instead of an every-origin, cubes-only search with no same-root test. |
| Old poses (BlockWorld write helper, AddObjectRelativeObservation, ApplyRelativeObservation, DeleteLocatedObjects) | A copy of the object's own native pose, parent kept (GetPose 0x005060CA / copy ctor 0x005060D2; 0x00506D1C/0x00506D2C; 0x0061D290/0x0061D2A6), instead of the flattened public Pose. |
| The M13-023 stand-in MISSING | Once per caller site, with site-specific text. |

**The old-pose fix has two consequences:**
- A once-carried, placed cube carries the cube on top when it is re-observed; before, nothing was found silently.
- A deleted once-carried cube is removed from its origin's memory map (GetRootID 0x0067EC3C); before, it stayed in the map.

**Unchanged:**
- The host BlockConfigurationManager path (host extension D1) still uses the public-Pose finder, searches every origin, has no same-root test and walks differently. It is recorded in M13-007; a separate job is proposed, HOST-CONFIG-SAME-ROOT-1.
- NativeConfigurationGeometry.Neighbor's non-cube throw is recorded against M13-023.

## Tests

**`M13FindOnTopSameRootTests`** (new; all `reach: unit`, with their substitutions labelled):
- C10: a cube in another origin, or under another root, is not attached.
- C10: the first match follows the walk order. This is a guard; the old code also passed it.
- C11: only a cube in the current origin and root blocks stacking.
- C9: the clear search includes markerless objects (exactly one site MISSING).
- **C8, placed:** a carried and placed cube carries the cube on top, through rig frames with the native origin bound.
- **C8, carried stack:** the cube on top follows when the carried cube is re-posed on the lift. The sighting is substituted with the engine-ordered write-helper steps, because the rig cannot show a cube on the lift.
- Delete: a deleted once-carried cube leaves the origin's map.

**Other tests:**
- `M11RVisBuildTests.M11_037_AnObjectOnTopFollowsTheObjectItRestsOnWithTheNativeOriginBound` (new).
- `M12RVisBuildTests.M12_008_ACubeOnTopInAnotherRootIsNotFoundAndTheAttachContinues`, rewritten: the after-find warning branch is unreachable.

**Results:**
- implementer: 1456/1456 on the affected classes;
- verifier: 1162/1162;
- the new tests fail on the reverted code;
- full gate: see the commit.

## Verifier: FAIL, FAIL, then PASS

| Round | Finding | Resolution |
| --- | --- | --- |
| 1 | BLOCKING: C8 got a parentless old pose for any once-carried cube (the flattened public Pose), so P1 now rejected it silently, where before it threw a visible MISSING | Old poses copy the native pose (0x005060CA/0x005060D2) |
| 1 | Queued: the stand-in MISSING repeated on every check, with wrong wording at C10/C11 | Once per site, site text |
| 2 | BLOCKING: a carried stack whose carried cube is seen on the lift. Heights were read in the lift frame, so the cube on top was never found. The engine uses root-frame heights (H2/P5/P6) | Root-frame geometry stage |
| 2 | Found: DeleteLocatedObjects passed the flattened public Pose, so a deleted once-carried cube stayed in the memory map | Fixed (0x0061D290/0x0061D2A6) |
| 2 | Observation: NativeCarryingTree is never cleared after a placement. A second pickup still follows the lift (LiftGeometry.cs:185-193 re-parents through liveLiftPose); its lasting effect is the public-Pose view | Queued in M12-008 |
| 3 | PASS. Observation: the delete notification's pose copy is not disposed (GC) | — |

## Records

`unresolved` and `test` only:

| Record | Note |
| --- | --- |
| M13-007 | The built call sites, the root-frame stage, the old-pose fix, the host-path note, the Neighbor/M13-023 boundary; inventory corrections for the manager (the authority's "refPose … in its own parent frame" contradicts 0x00626048; originMode 0, the walk order and the per-caller filters are missing; the stale minAreaRect/yaw-only text) |
| M12-008 | The lift-attach search; NativeCarryingTree queued |
| M11-007 | C2 is unreachable in both systems: every external MarkObjectDirty call passes propagate=0 (0x0053413A, 0x00534948, 0x0062354E, 0x0063135E, 0x0063BB48) |
| M11-037, M12-012, M11-042, M11-074 | Their changed sites |

No settlement.
