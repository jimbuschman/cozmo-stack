# Opus check of B-POSE-3 (commit 1cc1b6b6, 2026-10-15)

- **Run:** read-only cozmo-verifier, against libcozmoEngine.so.
- **Verdict:** FAIL, with two blocking findings. Everything else matches the binary:
  - all 31 Dev_Assert call sites;
  - the VERIFY sink;
  - RemoveOwner;
  - the owner counts.
- **No false VERIFYs** fire in ordinary rig runs: the full suite was traced, and every hit came from the VERIFY tests.
- **Commands:** `fidelity.py --check` passed (612 records); the focused tests passed 63/63.
- **Adopted:** by the manager on 2026-10-15. The fixes are job B-POSE-4.

## Blocking

1. **IsSameAs_WithAmbiguity, the parent-wrapper branch** (Geometry.cs:379-382).
   - **The native** (0x00846F3D) runs three checks:
     1. Dev_Assert at 0x00846F8A;
     2. Dev_Assert at 0x00846F9E;
     3. **public PoseBase::GetParent()** at 0x00846FAA, which runs its own Dev_Assert (0x00845412) before
        WrapExistingNode;
     4. then GetWithRespectTo(other, parent, …) (0x00846FB4).

     So when `other` has a node and the parent is neither null nor `other`, there are **three** checks. The stack does two.
   - **Fix:** go through the TryGetParent equivalent (the 0x00845412 check, then the wrap). Add a test that expects three
     VERIFYs with an unowned parent.
2. **~PoseOriginList's event** (RobotLifetime.cs:112, NativeDelocalization.cs:11-21).
   - **The native order** (0x00847944):
     1. **sEventF `poseoriginlist.destructor` "%zu" with the origin count [this+0xC]** (0x0084795E; strings 0x00847A18,
        0x00847A34);
     2. save the flag and set it;
     3. reset each origin in key order;
     4. restore the flag;
     5. destroy the tree.
   - **The stack** builds steps 2–5 but never emits the event. Binding the slot also removed the earlier lifetime MISSING,
     so every robot removal silently loses an Event-level entry.
   - **Fix:** emit the event first, at Event level, with the count. The event comes from OV13 item 6 (M11-055) and is now
     built here.
   - **Test:** removing a robot logs `poseoriginlist.destructor` with its origin count, before the origins are reset.

## Queued

3. **AddVisionOnlyState** (NativeVisionOnlyHistory.cs:21) reads `pose.Node?.Parent?.Parent` directly. The native runs
   public HasParent (0x005310B4), then GetParent (0x005310C2), then IsRoot (0x005310C8), then ~PoseBase: three Dev_Assert
   sites. Route it through those helpers. Nothing is lost today.
4. **A test race:** `Pose3d.UnownedParentsAllowed` is process-static. Put the VERIFY-counting tests and the
   robot-removal tests in one serial collection.
5. **A doubled test entry:** M11-066's `test` lists NativePoseBuilderTestsBPose2 twice. Remove the duplicate.
