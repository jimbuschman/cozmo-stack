# Job B-POSE-4: fixes from the B-POSE-3 check

- **Agent:** Codex, as builder. Follow `jobs/CODEX-BUILDER.md` rules 1–12.
- **Status file:** `status/B-POSE-4.md`.
- **Runs next, after the job in progress.**
- **Checked rows:** `research/20261015-b-pose-3-check.md`.

## Items

1. **IsSameAs_WithAmbiguity's parent-wrapper branch:** three Dev_Assert checks (0x00846F8A, 0x00846F9E, and public
   GetParent's 0x00845412), then GetWithRespectTo. Test: three VERIFYs with an unowned parent.
2. **~PoseOriginList:** sEventF `poseoriginlist.destructor` "%zu" with the origin count, **first** (0x0084795E). Then the
   flag, the resets, the restore and the tree. Test: robot removal.
3. **Queued:**
   - AddVisionOnlyState through HasParent, GetParent and IsRoot (0x005310B4, 0x005310C2, 0x005310C8);
   - one serial collection for the tests that touch `UnownedParentsAllowed`;
   - remove the duplicate `test` entry on M11-066.

## Out of scope

Status changes.

**Manifest:** append only, except removing the duplicate entry.
