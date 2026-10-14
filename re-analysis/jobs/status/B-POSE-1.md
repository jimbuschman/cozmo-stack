BLOCKED — adopted R18 contradicts the required Renormalize target reopening (NaN branch).

Claim: 499721a3. No B-POSE-1 production implementation commit. Metadata/report commit: 07b96e18f29443ee0cf6d6a9d21c9dc15307e19f, pushed to main on 2026-10-14. The initial push was interrupted before completion; the normal retry passed the gate and pushed successfully.
The previous R2 blocker is resolved: the manager's corrected (-pi, pi] row was re-read and used in the parked draft. This is a different blocker.

## Primary evidence and conflict

`research/20261013-pose3d-arithmetic.md:34` (adopted R18) says: "Exactly 1.0, or NaN, passes".
Reopening its required target `libcozmoEngine.so:0x008494E0..0x0084969E` instead shows:

- `0x0084952E..0x00849544`: sqrt row norm; unordered norm calls system sqrtf.
- `0x00849548..0x0084955A`: norm minus one and conditional negate; a NaN stays unordered.
- `0x0084955E`: VCMPE.F32 against the warning threshold; `0x00849562`: VMRS APSR_NZCV,FPSCR.
- `0x00849566`: BPL to `0x008495AA`, the warning/repair branch. Unordered NZCV=0011 has N=0, so BPL is taken. A C# `abs(norm-1) >= threshold` would incorrectly skip that branch on NaN.
- `0x008495C0..0x00849628`: builds the warning row string from three float to_string calls, each followed by a space.
- `0x00849638/0x0084964E/0x0084965A`: Warning key `RotationMatrixBase.Renormalize.BadRowNorm`, format `Norm of row %d = %f! (Expecting near 1.0) Row = [%s]`, call to sWarningF.
- `0x00849686/0x0084968E`: both warning cleanup exits lead back to `0x00849598`; `0x0084959A` calls RenormalizeHelper.
- Helper `0x008499B0..0x00849A10` invokes CV_32F matrix-to-vector Rodrigues at `0x008499E8`, then vector-to-matrix at `0x00849A0A`.

The separately adopted `research/20261013-rodrigues-and-pose-tree.md:25..28` says a NaN/Inf/out-of-range matrix becomes the zero rotation vector and then identity. Its cited library ranges are calib3d `0xF696..0xFB9A` (matrix-to-vector), `0xF28E..0xF478` (vector-to-matrix), wrapper `0x111F8..0x1133E`. This cannot be reconciled with R18's NaN pass without changing a checked row.

The branch check is independent of the C# draft: below threshold has N=1 (BPL not taken); equal or above has N=0 (taken); unordered has N=0 (taken). No engine execution or hardware acceptance was performed.

CODEX-BUILDER rule 2 says "Never build from your own unchecked extraction". Rule 10 requires reopening the row's call target. Therefore neither suppressing NaN's warning nor silently changing R18 is an authorized implementation. The manager/extractor must resolve the checked row, including the warning and repair path. No operator departure is proposed.

## Per-record files, lines and binary ranges

All production locations below are existing, unchanged code. The incomplete drafts were removed from the production source tree and retained only in ignored scratch; they were not compiled, tested or committed as recovered behavior.

| Record | Files/lines and disposition | Binary ranges |
| --- | --- | --- |
| M11-065 | `Vision/Geometry.cs:32..123` Mat3 and `:132..182` Pose3d remain the old candidate. R17/R18 matrix emission/repair blocks the single shared migration. Only append to manifest unresolved. | q normalization `0x00849DB8..0x00849E52`; product `0x00849EF4..0x00849F8E`; f32 point rotation `0x00849C1C..0x00849CF8`; R17 `0x0084AAE0..0x0084ABEC`; R18 `0x008494E0..0x0084969E`; Transform3d `0x0084B92A..0x0084BB98`; IsSameAs `0x00846EA4..0x008471A6` |
| M11-066 | `Vision/Geometry.cs:132..182`, especially WithRespectTo `:151`, unchanged. MISSING: native tree and shared-node write order are not integrated. No manifest change. | GetWithRespectTo `0x00845A34..0x00845E04`; depth `0x00845E8C..0x00845F02`; root conversion `0x004DF628..0x004DF6DC` |
| M11-067 | `Vision/Geometry.cs:91..123` conversions/repair, unchanged. R18 gates its Rodrigues helper. Only append to manifest unresolved. MISSING: native double SVD/Rodrigues and node copy/assignment port. | helper `0x008499B0..0x00849A10`; calib3d `0xF28E..0xFB9A`, `0x111F8..0x1133E`; core double SVD `0x595C0..0x59B56`, hypot `0x56EB0`; copy/assign `0x008458F0..0x00845A2E`; SetParent `0x00844A14..0x00844A7A`; wrap `0x0084529C..0x008452C0` |
| M11-068 | `Vision/Geometry.cs:32..182`, unchanged. Checked remaining bodies re-read with Opus corrections. MISSING: Euler, matrix X/Y/Abs, vector apply, 2d family and ownership wrappers in the shared port. No manifest change. | Euler/X/Y `0x0084AF98..0x0084B422`; Abs `0x0084B51E..0x0084B55C`; vector `0x0065671C..0x0065682E`; Transform2d `0x0084B618..0x0084B928`; Pose2d `0x008463D8..0x0084660C`; GetParent/FindRoot `0x0084520C..0x0084541E`; Radians `0x0084C846..0x0084C936` |
| M11-073 | Out of scope; no reading or implementation. `Vision/Geometry.cs:132` currently has no native GetParent API. Its adopted fatal-site disposition remains pending integration. No manifest change. | checked fatal wrap site `0x008452BA` |

## Job items and remaining uncertainty

Items 1–7: not shipped. Arithmetic/Rodrigues drafts cannot replace only an independent helper copy: CHECKLIST section 1 forbids a parallel port while the live Geometry component remains unchanged. The shared migration must resume after R18 is resolved. All required new focused regressions, the JIT no-FMA check, caller migrations, source-log wiring and call accounting remain MISSING; no old expected test value was changed. No source log or production side effect was added by this blocked batch.

Manifest impact: append-only unresolved notes for M11-065 and M11-067; tests, statuses, titles, authority, evidence, live_path and hardware_required preserved. FIDELITY_GAPS.md regenerated. No verification packet, branch, worktree, force push or hook bypass.

## Validation

`python -I re-analysis/tools/fidelity.py` and `--check`: PASS (587 records). Manifest semantic audit: exactly two append-only unresolved changes, all other fields preserved. `git diff --check`: PASS.

Unchanged-code full suite: PASS, 4,691/4,691, zero skipped, 3m46s. No new pose regression result is claimed. Normal push gate: PASS, records-only suite 786/786, zero skipped, 9s. No hook bypass. Stop the builder loop here under QUEUE rule 8.
