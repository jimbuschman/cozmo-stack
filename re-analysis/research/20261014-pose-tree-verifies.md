# Pose-tree VERIFYs, the sb table, the neck and the walk caps (Opus extractor, 2026-10-14)

- **Run:** read-only cozmo-extractor, against libcozmoEngine.so. It settles the items the B-POSE-2 implementer left as
  MISSING and the extractor items of `research/20261014-b-pose-1-check.md`.
- **Adopted:** by the manager on 2026-10-14.

## 1. The sb rotation table (M12-001 / M12-021)

- **How it's built:** a function-local static of `Block::GeneratePreActionPoses`, built lazily under a cxa_guard (guard
  byte 0x010590FC, in .bss) on the first call. No atexit is registered.
- **The entries** are four 0x14-byte RotationVector3d at 0x010590AC + 0x14·sb. Each is `Radians(float literal)` with
  flag 1, about Y_AXIS_3D (0, 1.0f, 0):

  | sb | Radians bits | Source |
  | --- | --- | --- |
  | 0 | 0x00000000 | 0x004E586A |
  | 1 | 0x3FC90FDB | 0x004E5882/0x004E5888 |
  | 2 | 0x40490FDB | 0x004E58A4/0x004E58AA |
  | 3 | **0xBFC90FDB** | 0x004E58C6/0x004E58CC |

  - All four values survive rescale unchanged.
  - MakeUnitLength leaves the axis as it is.
  - Neither the X-axis rule nor the AxisNotUnitLength warning fires.
- **The loop** indexes the table by sb, with fp starting at 0x010590AC and stepping by 0x14 (0x004E5914..0x004E5DAA).
- **Built in B-POSE-2.**

## 2. The neck (M11-003)

- **The construction** (Robot ctor, Pose3d at 0x0050FF6A):

  | Part | Value | Source |
  | --- | --- | --- |
  | Rotation | Radians(0.0f) with flag 1, about Y_AXIS_3D | 0x0050FF24..0x0050FF2C |
  | Translation | (−13, 0, 49) = (0xC1500000, 0, 0x42440000) | |
  | Parent | robot+0x298 | |
  | Name | "RobotNeck" | |

  Through R3 and R4 that gives the **identity quaternion**.
- **It is never modified:**
  - the only `#0x2cc` accesses are the ctor, ~Robot, and two read-only uses (GetHistoricalCameraPose 0x00513B02, and the
    GetWithRespectTo target in ComputeHeadAngleToSeePose 0x00518386);
  - a computed-pointer access isn't excluded by the linear scan, but none was found.
- **The head cam:** the RotationMatrix3d global, (17.52f = 0x418C28F6, 0, −8.0f), parent the neck, name "RobotHeadCam".

## 3. GetWithRespectTo's walk caps (M11-066)

**All three caps (0x1A0, 0x1B6, 0x1D4) are unreachable** through the HasSameRootAs and GetTreeDepth gate, on both
acyclic and cyclic graphs:
- **(a) and (b)** would need d ≥ 1000, which forces the other side to be the root, and their exit fires at k = 998;
- **(c)** meets by iteration 999.

If (a) or (b) ever overflowed, (c) would read a null parent in Rotation3d::PreMultiplyBy (0x0084AC82). That is an M1-034
point, but it has no production path. This assumes no concurrent mutation during a walk; `Dev_GetMutex` exists but isn't
used here.

**The boundary that is reachable (new):** a chain of exactly 1001 nodes passes the gate.
- It logs HasSameRootAs' LoopBoundOverflow (0x107 or 0x10E), and GetTreeDepth's (0x189).
- It still returns the right result.

## 4. Dev_AssertIsValidParentPointer and RemoveOwner (M11-066 / M11-077)

### Dev_Assert(parent, child)

At 0x008460C8..0x00846124.

- **It returns silently** when any of these holds:
  - the parent is null;
  - `_areUnownedParentsAllowed` is set (.bss 0x0105E078);
  - parent->ownerCount (+0x48) ≠ 0.
- **Otherwise it logs a VERIFY:**
  - **Key:** `PoseBase.Dev_AssertIsValidParentPointer.UnownedParent`.
  - **Format:** `VERIFY(%s): Pose %d(%s) has parent %d(%s) which is not owned by any PoseBase wrapper`.
  - **Arguments:** "parent->_ownerCount > 0", child ID, child name, parent ID, parent name.
  - **The sink:** sVerifyFailedReturnFalse, at Error level. It sets the error flag, dumps the call stack ("VERIFY"),
    calls the provider's slot +0x14, and returns 0.
- **The walk then continues.**

### The unowned-parents flag

`_areUnownedParentsAllowed` starts false. Its only writer is `AllowUnownedParents`, called only by ~PoseOriginList,
which sets it to true around destroying the origins and then restores it (0x0084798C, 0x008479CC).

### Owner counts

- **The node ctor** zeroes the ID and the count (0x00845F98). The node copy ctor doesn't copy the count.
- **Increments:** the PoseBase ctors (0x0084577A, 0x00845834, 0x008458CC, 0x00845924) and WrapExistingNode (0x008452BE).
- **The only decrement** is RemoveOwner, called from ~PoseBase and from operator= with a null source.
- **operator= with a non-null source** leaves the count unchanged.
- **Not censused:** inlined Pose3d copies in callers.

### RemoveOwner

At 0x008448B4..0x00844912.

1. **count == 0:** VERIFY `PoseBase.PoseTreeNode.RemoveOwner.ZeroOwners` "VERIFY(%s): " "_ownerCount > 0", with **no
   decrement**.
2. **Otherwise,** decrement. If the result is still non-zero, return.
3. **At 0:** return if the flag is set, or if the ID is 0.
4. **Otherwise:** VERIFY `PoseBase.PoseTreeNode.RemoveOwner.ZeroOwnersWithID` "VERIFY(%s): Pose %s has non-zero ID=%d
   but just had its last owner removed", with "GetID() == 0", the name and the ID.

Only origins have IDs (the only SetID caller is AddOriginWithID, 0x00847B54).

### Dev_Assert call sites

The parent is r0 and the node is r1 in every call.

- **GetWithRespectTo:**
  - walk (a): 0x00845AC8 (old node) and 0x00845AD2 (new node);
  - walk (b): 0x00845B5C and 0x00845B66;
  - walk (c): 0x00845C00 (the `to` side) and 0x00845C0A (the `from` side), on the old nodes.
- **HasSameRootAs:** 0x00844BF2 and 0x00844C66, once per node per chain.
- **HasSameParentAs:**
  - 0x00844D52 checks this side; if it has no parent, return 0 without checking the other side;
  - 0x00844D5E checks the other side;
  - this == other returns 1 with no check.
- **FindRoot:** the public one at 0x00845248. The node one checks twice per step on the same pair (0x008452E4,
  0x008452F2).
- **IsRoot:** 0x00844AAC and 0x00844AC8.
- **GetParent:** 0x00845412 and 0x00845452.
- **Others:**
  - HasParent 0x00844AE2/0x00844AFC; GetRootID 0x00844B24;
  - GetParentString 0x00844E42/0x00844E52/0x00844E84; GetRawParentPtr 0x00844FB8;
  - IsChildOf 0x00844FD0/0x00844FEE; IsParentOf 0x0084500E/0x00845032;
  - ToPoseStruct3d 0x00846D76; IsSameAs_WithAmbiguity 0x00846F8A/0x00846F9E.

### Correction to B-POSE-2's justification

The status file's "native logs nothing at 0x008460C8" is wrong. Dev_Assert does log, under the conditions above.

- **What still stands:** the stack's previous behaviour (a MISSING Warning on every ctor) was wrong, so removing it was
  right.
- **The exact behaviour to port:** these conditional VERIFYs (job B-POSE-3).

## Still unread (new RECOVERABLE_GAP record)

- **Unowned parents in production:** does any flow destroy or null-assign the last wrapper of a pose that still has
  children, outside ~PoseOriginList? That decides whether UnownedParent and NotLastOwner VERIFYs appear on a running
  robot.
- **The origins:** does any origin lose its last wrapper outside ~PoseOriginList (the PoseOriginList erase and rejigger
  paths)?
- **Inline wrapper attaches** that skip the increment, which would make ZeroOwners reachable.
- **Whether any pose tree is mutated from another thread.**
