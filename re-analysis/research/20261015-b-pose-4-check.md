# Opus check of B-POSE-4 (commit d12b58fc, 2026-10-15)

- **Run:** read-only cozmo-verifier, against libcozmoEngine.so.
- **Verdict:** FAIL, with two blocking findings.
- **Commands:** `fidelity.py --check` passed (621 records); the focused tests passed 80/80.
- **Manifest:** append-only. The one removal, the duplicate test entry in M11-066, was directed by the job.
- **Adopted:** by the manager on 2026-10-15. The manager read GetNamedPathToRoot itself (below).
- **The fixes:** job B-POSE-5.

## Blocking

1. **The NonFlattenedPose text** (`NativeVisionOnlyHistory.cs:27`).
   - **The native argument** is `PoseBase::GetNamedPathToRoot(false)` (0x005311C2..0x005311CA), passed to sErrorF at
     0x005311E6. The key is "RobotStateHistory.AddVisionOnlyState.NonFlattenedPose" (0x005312D0), and the format is
     "Pose object inside pose stamp should be flattened (%s)".
   - **The stack** prints only the pose's own name.
   - **The new record** for GetNamedPathToRoot is below.
2. **IsSameAs with a null `other` node** (`Geometry.cs:379`).
   - **The native:** at 0x00846F84, `cbz r4` skips the first Dev_Assert (0x00846F8A) and the parent == other compare.
     Only 0x00846F9E and GetParent's 0x00845412 run.
   - **The stack** runs `n?.AssertParent()` regardless, so it makes three checks where the native makes two.
   - **Reachable:** with a null-node Pose3d, from Assign of a null source.

## Manager read: GetNamedPathToRoot

- **The wrapper** (PoseBase, 0x00845524..0x00845542):
  - with a null node: the string "NullNode" (0x00C2934F, 8 chars);
  - otherwise: PoseTreeNode::GetNamedPathToRoot(flag).
- **The node walk** (0x00845548..0x0084564C):
  - the result starts empty, with i = 0;
  - **each step:**
    1. if i ≥ 1000 (`cmp #0x3E8`, `bhs`), stop with the overflow log below;
    2. append the node's name (the string at node+0x38); an empty name appends "(UNNAMED)" (0x00C29519, 9 chars);
    3. if flag == 1, append Point3::ToString of node+0x20 (the caller passes false);
    4. node = node+0x30 (the parent); if null, stop;
    5. append " -> " (0x00C29523, 4 chars);
    6. i += 1.
  - **No Dev_Assert or owner check** runs on this walk.
- **The overflow** happens only past 1000 steps:
  - sErrorF, key "LoopBoundOverflow" (0x00BFBFD5);
  - format "%s:%d" (0x00BFBFE7), with "../../../../coretech/common/include/anki/common/basestation/math/poseTreeNode.h"
    (0x00C294C9) and 348 (0x15C);
  - empty data;
  - then `_errG` = 1, and sDebugBreakOnError if it's enabled (0x00845632..0x00845646).

  The partial string is returned.

## Queued (in B-POSE-5)

3. **The split gate in ~PoseOriginList** (`NativeDelocalization.cs:16-17`) is a rule 12 window.
   - **What happens:** the count is taken under PoseTreeGate, the event is emitted, and then the gate is taken again.
     An origin added in between would be torn down without being counted.
   - **The native** has no such window.
   - **The fix:** emit the event first, inside the single gated body. EmitSourceLog already defers the callback through
     LogCallbackDeferral.
4. **M11-070's note** should record the NonFlattenedPose text gap until item 1 is built.

## Checked and supported

- **IsSameAs with a non-null `other` node:**
  - the native order (0x00846F8A, 0x00846F90, 0x00846F9E, 0x00846FA4, 0x00846FAA → 0x00845412 / 0x0084541A, 0x00846FB4,
    0x00846FBC);
  - a failure returns false with no warning.
- **The ~PoseOriginList event:** sEventF at 0x0084795E, before the flag (0x00847984/0x0084798C), with the key, "%zu",
  the [this+0xC] count, and the null channel.
- **AddVisionOnlyState's helper order** (0x005310B4/C2/C8/D0). It adds no VERIFY in ordinary runs.
- **The tests** aren't circular, and the serial collection still disables parallelism.
