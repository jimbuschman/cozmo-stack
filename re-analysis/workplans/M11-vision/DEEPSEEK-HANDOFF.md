# DeepSeek handoff: M11 vision

## Do not implement from this document alone

This package is a research candidate prepared separately from the active M6 checkout. It intentionally changes no production C#, tests, manifest, approved inventory, or `PROJECT_STATE.md`. The manager must integrate and approve the inventory first.

## Exact next milestone

`M11-vision` is the next full unreviewed layer after M6. M10 is not pending research: commit `b85decdcf05b49785dcae94cfbc9a486114b5948` completed and verified its implementation. At this package's baseline, M11 is the first `UNREVIEWED` row in the project layer order.

## Baselines

- Research branch: `codex/m11-research-package`
- Repository baseline: `2ad71a340543d3be32857a8244e90d2d6d889296`
- M10 completion reference: `b85decdcf05b49785dcae94cfbc9a486114b5948`
- Native binary SHA-256: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`
- Shipped vision config SHA-256: `d99a7a00ffbdc07f6cd9834159a65f6c67dd8ce51da598fce6eded9e9312cdde`

## Read order

1. `INVENTORY.md` — proposed frozen IDs, classifications, and boundaries.
2. `NATIVE-EVIDENCE.md` — complete production path and primary-source map.
3. `GAP-ANALYSIS.md` — contradictions, local policies, weak tests, and implementation batches.
4. `ACCEPTANCE.md` — non-circular tests and operator hardware plan.
5. `evidence/native-evidence.json` — reproducible binary/config fingerprint and address snapshot.

## Manager integration checklist

1. Rebase or cherry-pick the research commit after the active M6 work; resolve documentation only.
2. Run a read-only extractor for M11-027 (charger), M11-028 (connected-object lifecycle), and M11-031 (other native modes).
3. Reconcile stable legacy IDs, especially M11-011 and M11-016, into the manifest. Do not silently renumber existing IDs.
4. Write `re-analysis/inventory/M11-vision.md`, attach instruction/file citations, set `source_investigation_exhausted` truthfully, and run the fidelity checker.
5. Present the inventory checkpoint to the operator. Do not implement before approval.

## Implementation order after approval

- Batch A: frame admission, interpolated history, schedules, worker/mailbox, publication order.
- Batch B: marker preprocessing/front end while retaining the exact library/decoder.
- Batch C: camera composition and native-equivalent `solvePnP`; charger geometry.
- Batch D: object association, confirmer, occlusion, cube lifecycle, delocalization.
- Batch E: overhead/map and charger-platform clearing.
- Batch F: motion, image quality/exposure, and resolved remaining modes.

Use the repository roles strictly: extractor produces evidence; implementer changes code only from approved rows and returns `MISSING:` for anything unsettled; verifier attacks the resulting diff. No role performs extraction and implementation in one task.

## Stop conditions

- If a required behavior is absent from the approved inventory: stop with `MISSING:`; do not infer it.
- If OpenCV behavior cannot be reproduced from native outputs: retain an explicit gap or equivalence claim; do not call a plausible optimizer exact.
- If hardware succeeds where provenance remains absent: preserve the prior classification.
- If active M6 changes overlap shared device/vision interfaces: reconcile with the manager rather than overwriting them.

## Expected deliverable after implementation

Report the complete traced production path, primary evidence, root cause/gap, exact code change, manifest impact, regression tests, commit, and remaining uncertainty, then stop.
