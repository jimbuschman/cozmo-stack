# HANDOFF REFERENCE-SIZE-SLOT-1 (Claude worker, 2026-11-13)

Job 22. Implementation report (worker-local): `.scratch/REFERENCE-SIZE-SLOT-1-impl.md`. Verifier: PASS.

## The change (NativeConfigurationGeometry.Neighbor; M11-105, M13-007)

- **Reference size:** FindObjectOnTopOrUnderneathHelper reads the reference's own size slot +0x2C through
  GetDimInParentFrame<Z> (0x0062606E..0x00626070 -> 0x00557794; axis from the re-rooted refPose's rotation, tbb
  0x005577AA: |3| z, |2| y, else x) for targetZ = z + dim * (onTop ? 0.5 : -0.5) in f32 (0x0062607E..0x006260A0).
  `ReferenceDimZ` now uses `ObservableObject.ObservableSize2C` (as the candidate side does) instead of the cube-only
  Dimensions(); a null size (SDK custom objects only) throws, naming M11-105.
- **Reference footprint (beyond the row's size-only scope; accepted by me):** the reference quad comes from its own
  slot +0x50 at refPose with padding 0 (0x0062604C..0x0062605C), via `TryObservableBoundingQuad50`. Without it a non-cube
  reference threw at the footprint before its size was read. For a cube the result is identical (same Footprint call).
- No live caller passes a non-cube reference today (CanInteract admits families 1/2, 0x0063C65E..0x0063C666; the other
  callers filter to {2,1}), so robot behaviour is unchanged; the engine's per-class path is now in place.

## Tests

`ReferenceSizeSlotNeighborTests` (reach: unit; objects seeded by AddLocatedObject, Neighbor called directly), hand
derived: a charger reference (targetZ 15.5: found at cube z 24 and at the inclusive 52.5, not at 53 — z 24 and 53 flip
with a 44 mm height), a ProxObstacle reference (targetZ 50: found at 87, not at 88 — 87 flips with 44 mm), and cube
regressions. Affected classes 179/179 (implementer), 75/75 (verifier).

## Records

- M13-025 `location` -> NativeConfigurationGeometry.cs (manager-directed); its stale "built locally in Neighbor" note
  corrected by an appended note.
- M11-003: a note for you — evidence[2] cites 0x004E4D8E, the movt is at 0x004E4D90 (evidence is frozen; not edited).
- M11-105, M13-007: build notes and the new test.

## Queued (verifier)

- M13-007 and M11-105 still carry an older sentence saying the reference reads CubeGeometry.SizeOf; the appended note
  supersedes it.
- The axis selection uses the double RotatedParentAxis.Get where the native GetRotatedParentAxis<'Z'> reads f32 (an
  f32 port exists at DockActions.cs:214); a difference is possible only at an exact tie, and it predates this job (the
  candidate side and the old cube path use the same double helper).

No status changed. No record settled.
