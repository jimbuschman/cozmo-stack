# Opus check of R-POSE-REST (2026-10-13)

> **Superseded in part (2026-10-14):** `research/20261014-pose-rows-recheck.md` re-checks every row here against the binary and overrides each row it marks CORRECTED. Read it first.

- **Checked:** `research/20261013-remaining-pose-bodies.md` (Codex), against libcozmoEngine.so. Read-only
  cozmo-extractor; helpers are in `.scratch/poserest/`.
- **Adopted:** by the manager on 2026-10-13. This file is a checked source for job B-POSE-1. Use the Codex report together
  with the corrections below.
- **Result:** items 1–6 hold, with these corrections and additions:
  - **GetAngleAroundX/Y with NaN:** an unordered compare takes `ble` (0x0084B3A4, 0x0084B3FE), so the **second** pair is
    chosen, as on a tie. (Codex said the first.)
  - **Radians operator=(float)** (0x0084C846 → rescale 0x0084C87C) rescales **only if the destination's flag is already
    set** (`ldrb [r4,#4]; cbz` at 0x0084C884). Radians(float) sets the flag and rescales.
  - **No engine callers:** UnitQuaternion<double>·Point3d (0x00849F90), IsValid (0x008497D0) and the 0x00849900
    initializer_list constructor have none. GetEulerAngles' x2-only crash is unreachable: the only caller, 0x005D361E,
    passes 0 for all three.
  - **Pose2d(Transform2d)** memclr4s the normal (0x008464D2..0x008464D8). Pose2d(Pose3d) reads the node's rotation and
    +0x20/+0x24 directly, with no world transform.
  - **Transform2d A·B** builds an unused RotationMatrix2d (its trig calls are present). PreComposeWith doubles when A
    aliases B. Invert multiplies the angle by −1.0f with a rescale.
- **Item 7 is settled, and unreachable.**
  - _areUnownedParentsAllowed is false in normal running: it is set only in ~PoseOriginList.
  - No defined execution produces a pose whose node is null:
    - the default PoseBase() (0x00845724) has no callers;
    - every other constructor allocates a node;
    - the copy constructor or Wrap with a null node faults first;
    - operator= installs null only from a null source.
  - So SetParent's null-parent read (0x00844A46..0x00844A52) cannot happen.
  - SetParent's unowned-parent VERIFY with a non-null node remains (0x00844A2A..0x00844A6C).
- **New, open:** GetParent() on a **root** pose. WrapExistingNode increments owners at [node+0x48] with no null guard, so
  GetParent(root) faults reading 0x48 (0x008452BA). It has about 60 call sites, and whether each is guarded
  (HasParent/IsRoot) is unread. If it is reachable, the M1-034 standing rule for crash paths (operator, 2026-10-13)
  covers it.
- **Also confirmed:** the Euler constructor (Robot::SetCameraRotation 0x005137EA); the X/Y angles (live through
  GetRotatedParentAxis); Abs (live through IsRotationSame); ApplyTo(vector) (live through AddLiftOccluder); the
  RotationMatrix2d / TranslateForward / T·p bodies; and node FindRoot's 1000-step LoopBoundOverflow (poseTreeNode.h line
  0x145).
- **Note:** the Codex report cites two extract files that don't exist.
