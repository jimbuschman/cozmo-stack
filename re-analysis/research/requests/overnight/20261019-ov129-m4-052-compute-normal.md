# Research request: M4-052, the marker normal behind relative cube lights (narrow)

- **Date:** 2026-10-19
- **Requested by:** manager
- **Kind:** narrow extraction, one question
- **Answer file:** `re-analysis/research/overnight/20261019-ov129-m4-052-compute-normal.md`

## Why this is needed

M3+M4 is the acceptance target. B-M3M4-31 built relative-mode SetObjectLights (M4-052) from OV128 as corrected by
`research/overnight/20261018-ov128-check.md`, but stopped at one unread callee: GetTopMarker (0x004E69B8..0x004E6A7E) gets
each marker's normal from `KnownMarker::ComputeNormal(Pose3d)` at 0x0087E3FA (call 0x004E69F8), which calls
`Get3dCorners(Pose3d)` (call 0x0087E404) and builds the normal from corner differences, not by rotating an axis. Live
relative sends currently stop with a visible MISSING (`jobs/status/B-M3M4-31.md`, "Unbuilt dependency"). Re-read the
current M4-052 record in `re-analysis/fidelity_manifest.json` before answering.

## Question

Give rows, with addresses, for:

1. `KnownMarker::ComputeNormal(Pose3d)` 0x0087E3FA: its inputs, which corners it takes, the f32 operation order of the
   differences and the cross product, whether it normalizes (and how: sqrt, reciprocal, division; zero-length handling),
   and what it returns.
2. `Get3dCorners(Pose3d)` (called at 0x0087E404): where the canonical corners come from (marker size, the corner order and
   signs), how the pose is applied (rotation then translation; which Pose3d/matrix helpers, by address), the f32 widths and
   operation order, and the output layout.
3. Any other callee inside these two bodies that changes the result (name each by address and say whether it is an
   existing record's helper, for example M11-065/066/067's Pose3d operations).
4. Compare with the stack: `cozmo-stack/src/Cozmo.Robot/RelativeCubeLights.cs` (the normal input point) and any existing
   C# Get3dCorners / marker-corner code (search `Vision/`). Say whether an existing helper already matches, bit for bit.

Answer only what changes the normal's value or a failure path. Say UNKNOWN where the binary does not settle a step.
