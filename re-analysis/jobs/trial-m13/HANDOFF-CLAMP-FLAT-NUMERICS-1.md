# HANDOFF CLAMP-FLAT-NUMERICS-1 (Claude worker, 2026-11-11)

The census is `.scratch/CLAMP-FLAT-NUMERICS-1-census.md`. Every step has an address, and none is UNKNOWN.

## What changed

| Where | Change |
| --- | --- |
| `BlockWorld.ClampRotationToFlat` (new, shared) | ObservableObject::ClampPoseToFlat 0x00877330..0x008774EC in f32, with the tolerance as a parameter. Each step is cited (see below). The body is the checked docking port, moved without change. |
| `BlockWorld.ClampPoseToFlat` | Replaces the invented double arithmetic: no Orthonormalized and no 1e-9/1e-12 cut-offs. See below. |
| `NativeDockingGeometry.Clamp` | Delegates with 0x3F32B8C2 and returns `local` on false. Docking behaviour is unchanged. |
| `CreationFlatClampAngleRad` | Now the f32 product 5.0f × 0x3C8EFA35 = **0x3DB2B8C2**, not the double 5 × 0.0174533. |
| `re-analysis/tools/emu/emu_clamp_flat.py` (new) | An ARM oracle that runs the shipped 0x00877330 under Unicorn. The tolerance is built by the engine's Radians(float) 0x0084C832, and the libm calls are host stand-ins (M13-023(e)). |

**The new ClampPoseToFlat:**
- The gate and axis choice use the pose's rotation with respect to its root (GetWithRespectToRoot 0x004DF628).
- The correction uses the pose's local rotation.
- On true, only the rotation is replaced, so the parent, translation, name, ID and frame flag are kept (census C19).
- On false, the input is returned unchanged.

**The creation tolerance:** the factor 5.0f comes from vtable+0x1C 0x004E024E (ActiveCube/Charger), the literal 0x3C8EFA35 from 0x006256FC, and the multiply is at 0x00625566.

**The steps, by address:**
- axis: 0x005507A0, strict `>` on ties;
- gate: Radians operator< 0x0084CD12 → operator> 0x0084CC90 with IsNear 0x0084CC0A, so a tilt within 1e-5 of the tolerance is not snapped;
- target: signed zero (−0, −0, −1) for a negative axis;
- dot product: in the order ((c0·t0 + c1·t1) + c2·t2);
- angle: acosf, with no clamp;
- early-outs: 1e-5f (0x3727C5AC at 0x00877518);
- MakeUnitLength: 0x0050E0C0;
- rotation: Rotation3d(angle, axis) 0x0084A506, then q·local;
- write: the 32-byte rotation only, at 0x008774D0..0x008774E8.

## Tests

- **`VisionTests.M11_006_TheClampMatchesTheShippedEngine` (12 oracle rows, compared bit-for-bit):**
  - snap and kept, at 20° and at 0x3DB2B8C2;
  - the IsNear boundary;
  - the 1e-5 early-out;
  - upside down, and on a side;
  - three root→mid→pose chains.
- **`M11_006_TheClampKeepsItsInputsParentAndFrameContext`:** rewritten on an oracle row.
- **`M11_006_TheCreationClampUsesTheEnginesF32FiveDegrees`:** new.
- **`M11_006_TheGateIsTheEnginesRadiansLessThan`:** new.
- **`M11RVisBuildTests.M11_013_TheCreationClampIsFiveDegrees`:** it asserted the stack's own double (circular). It now asserts 0x3DB2B8C2.
- **`M12RFix2StreamATests.M12_019_TheErrorSignalClampIsTheBinary32FortyDegrees`:** it expected a 0x3F32B8C1 tilt to snap against 0x3F32B8C2. The engine's IsNear refuses that, measured on the shipped function: 0x3F32B8C1 → 0, 0x3F32B8C3 → 0, tol − 2e-5 → 1. The test now expects exactly that. M12-019's record and docking's code are not changed.
- All the tests above are `reach: unit`. There is no production-reach test with rotation values (stated in M11-006 `unresolved`).
- **Results:** the implementer's affected classes passed 462 and failed 1 (M12_019, before the fix). The verifier's run passed 271/271. For the full gate, see the commit.

## Verifier: PASS

The verifier re-disassembled every step and re-ran the oracle. It also ran 13 extra engine cases, 11 of them compared with
the stack through a C# harness; all matched bit-for-bit, including the return value.

| Finding | Class | Resolution |
| --- | --- | --- |
| M11-006 `unresolved` still said M12_019 fails | queued | Fixed. |
| M12_019's comment cited emu_clamp_flat.py, but the 40° cases are not in its CASES | queued | Reworded: the cases were measured with that harness in the implementer and verifier runs. |
| The oracle docstring claimed a layout read-back the script doesn't do | queued | Reworded to cite the disassembly check (0x0084499A/0x008449A0, 0x0084A518). |
| Confirmer caller: the engine clamps a `_COPY`-named copy (0x008458F0); the stack keeps the original name | observation | Only the pose name differs, and nothing found reaches 1–4. This predates the job. |
| PrepareObservedCluster: when the camera pose has no parent, the gate uses the stored rotation, not the rotation composed with an identity root | observation | It matters only at the IsNear boundary, and it predates the job. |

## For the manager

**M11-006 evidence (frozen, so not edited):**
- Line 4 ("the shape this stack already had") is contradicted.
- Line 3 cites "0.0174533" without the literal 0x006256FC or the f32 product.

## Records

**M11-006:** no settlement. Status stays IMPLEMENTATION_GAP, and only `test` and `unresolved` changed. Open items:
- no production-reach test;
- the libm stand-ins (M13-023(e));
- the two observations above.
