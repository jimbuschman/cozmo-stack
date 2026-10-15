# Job B-POSE-5: fixes from the check of B-POSE-4; GetNamedPathToRoot (M11-086)

- **Agent:** Codex, as builder. Follow `jobs/CODEX-BUILDER.md` rules 1–12.
- **Status file:** `status/B-POSE-5.md`.
- **Runs after B-M15-1b.**
- **Checked rows:** `research/20261015-b-pose-4-check.md`.

## Items

1. **M11-086:** port GetNamedPathToRoot exactly as in the check file:
   - NullNode;
   - the walk order;
   - "(UNNAMED)";
   - " -> " only between nodes;
   - the optional position;
   - the LoopBoundOverflow sErrorF, then `_errG` and break.
2. **NonFlattenedPose** (`NativeVisionOnlyHistory.cs:27`): the argument is `GetNamedPathToRoot(false)`. Test the full
   "child -> parent -> root" text.
3. **IsSameAs** (`Geometry.cs:379`): with a null `other` node, skip the first Dev_Assert and the parent == other compare
   (0x00846F84 `cbz`). Test that there are two VERIFYs.
4. **~PoseOriginList** (`NativeDelocalization.cs:16-17`): emit the event first, inside the single gated body, with no
   second gate acquisition (rule 12).

## Out of scope

- Status changes.
- Other callers of GetNamedPathToRoot.

**Manifest:** append only.
