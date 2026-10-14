# Job B-POSE-2: fixes from the B-POSE-1 check

- **Agent:** the manager's Claude cozmo-implementer (Codex is short of budget). Follow `jobs/CODEX-BUILDER.md` rules
  1–10.
- **Status file:** `status/B-POSE-2.md`.
- **Checked rows:** `research/20261014-b-pose-1-check.md`, with `research/20261014-pose-rows-recheck.md` and the B-POSE-1
  row files.

## Items

- **Blocking 1–4:** the MISSING routing, GetCameraPose (both paths), the block pre-action RotateBy, and the circular
  test.
- **Latent 5–6:** the Radians `IsNear` and `float − Radians`.
- **Queued 7–12.**

Each item's text and citations are in the check file.

## Out of scope

- Status changes.
- The extractor items.

**Manifest:** append to test lists and `unresolved`; never replace.
