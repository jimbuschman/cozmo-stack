# Job B-POSE-3: the pose-tree VERIFYs (M11-066, M11-077)

- **Agent:** Codex builder, or the manager's Claude implementer. Follow `jobs/CODEX-BUILDER.md` rules 1–10.
- **Status file:** `status/B-POSE-3.md`.
- **Checked rows:** `research/20261014-pose-tree-verifies.md`, sections 3 and 4.

## Items

1. **Dev_AssertIsValidParentPointer at every listed call site,** in the native order (walk steps check twice; the node
   FindRoot checks twice per step):
   - **silent when:** the parent is null, the unowned-parents flag is set, or the parent's owner count ≠ 0;
   - **otherwise:** the UnownedParent VERIFY with its exact key, format and arguments, through the VERIFY sink (Error,
     the error flag; the call-stack dump and provider call as the stack's VERIFY sink already does).
2. **RemoveOwner:**
   - count 0 → ZeroOwners, with no decrement;
   - at 0 with the flag clear and a non-zero ID → ZeroOwnersWithID;
   - the flag is set only during ~PoseOriginList (AllowUnownedParents save/set/restore).
3. **The owner count:** increments at the PoseBase ctors and WrapExistingNode; operator= with a non-null source leaves it
   unchanged; the node copy ctor doesn't copy it.
4. **The walk caps** (a)/(b)/(c) are unreachable. Keep their logs as built. No test needs to drive them.
5. **Tests:**
   - an unowned parent visited in each walk logs the VERIFY the right number of times;
   - no VERIFY with owned parents;
   - RemoveOwner's two VERIFYs;
   - a chain of exactly 1001 nodes logs HasSameRootAs 0x107/0x10E and GetTreeDepth 0x189, and returns the right result.

## Out of scope

- Status changes.
- M11-084 (unread).

**Manifest:** append only.
