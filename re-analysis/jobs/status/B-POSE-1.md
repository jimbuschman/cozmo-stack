BLOCKED — corrected R36's GetTreeDepth overflow log names the wrong source filename.

Claim 0e5a4af1. B-M13-3 was completed and pushed first (implementation 02fceff6; report 496fac5c). B-POSE-1 blocker metadata commit/push: pending. No pose production implementation commit.

## Checked row and required target reopening

Read `research/20261014-pose-rows-recheck.md` first, as the updated job requires; then the older arithmetic/Rodrigues/tree/remaining-body rows under its overrides. The previous R2 and R18 blockers are resolved and were not reused as reasons to stop.

The manager-adopted, CORRECTED R36 setup at `research/20261014-pose-rows-recheck.md:216..217` says GetTreeDepth's LoopBoundOverflow uses **poseBase_impl.h line 0x189**. The required reopened target `libcozmoEngine.so:0x00845E8C..0x00845F02` instead shows:

- `0x00845E90..0x00845E9E`: depth starts at1; each parent transition adds1; old count1000 enters the bound path, returning1001.
- `0x00845EA8`: key `LoopBoundOverflow`.
- `0x00845EAE`: format `%s:%d`.
- `0x00845EB0`: the filename pointer resolves to **0x00C294C9**, containing `../../../../coretech/common/include/anki/common/basestation/math/poseTreeNode.h`.
- `0x00845EB4`: line0x189 (393), stored as the second format argument at0x00845EB8.
- `0x00845EBC`: sErrorF.
- `0x00845EE2..0x00845EFA`: error-flag store and conditional debug-break call.

The filename was also read directly from the ELF bytes at0x00C294C9, independently of the disassembly annotations. It is poseTreeNode.h. The GetWithRespectTo ascent-loop diagnostics using poseBase_impl.h at0x1A0/0x1B6/0x1D4 are different sites; this report does not conflate them with the GetTreeDepth helper.

This is a defined engine Error payload, not a crash or undefined-memory case covered by M1-034. CODEX-BUILDER rule2 forbids building from the builder's own unchecked extraction, and rule10 requires every engine log's exact text/side effects. The builder therefore cannot silently replace the adopted filename. The manager/extractor must correct or resolve R36's setup diagnostic. No new operator departure is requested.

Current owning record, re-read before reporting: **M11-066**, title **"The pose tree: GetWithRespectTo (null/alias checks, depth walk, Tt^-1*Tf, 1000-step LoopBoundOverflow) and GetWithRespectToRoot (rows R35-R37)"**, status **IMPLEMENTATION_GAP**; its evidence expressly names GetTreeDepth0x00845E8C..0x00845F02.

## Per-record production locations and ranges

All production locations below remain unchanged. Draft arithmetic, Rodrigues, matrix and pose-tree replacements were parked in ignored scratch and removed/restored in the production tree. They are not represented as recovered, tested or shipped. No caller migration or original test expectation change was committed.

| Record | Files/lines and disposition | Binary ranges |
| --- | --- | --- |
| M11-065 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:32..123` Mat3 and `:132..182` Pose3d unchanged. MISSING: shared native arithmetic/matrix migration is not shipped. No manifest change. | normalization0x00849DB8..0x00849E52; product0x00849EF4..0x00849F8E; float point0x00849C1C..0x00849CF8; R17/R18 0x0084AAE0..0x0084ABEC /0x008494E0..0x0084969E; Transform3d0x0084B92A..0x0084BB98 |
| M11-066 | `Geometry.cs:132..182`, especially WithRespectTo:151, unchanged. Only append the new blocker to manifest unresolved. MISSING: complete checked tree diagnostic/port pending row resolution. | GetWithRespectTo0x00845A34..0x00845E04; **GetTreeDepth0x00845E8C..0x00845F02**, literal0x00C294C9; root0x004DF628..0x004DF6DC |
| M11-067 | `Geometry.cs:91..123` conversions/repair and `:132` wrapper unchanged. MISSING: source-faithful Rodrigues/SVD and shared-node copy/assignment integration not shipped. No manifest change. | engine helper0x008499B0..0x00849A10; calib3d0xF28E..0xFB9A /0x111F8..0x1133E; core double SVD0x595C0..0x59B56; copy/assignment0x008458F0..0x00845A2E; SetParent0x00844A14..0x00844A7A |
| M11-068 | `Geometry.cs:32..182` unchanged. MISSING: corrected Euler/2d/vector/ownership bodies in the shared port remain unshipped. No manifest change. | Euler/X/Y0x0084AF98..0x0084B47E; vector0x0065671C..0x0065682E; 2d0x0084B618..0x0084B928; parent/root0x0084520C..0x0084541E |
| M11-073 | Out of scope; no reachability extraction or production change. Existing `Geometry.cs:132` has no native parent wrapper. | previously checked fatal wrap0x008452BA |

## Items, calls and remaining uncertainty

Job items1–7 are not shipped. The required numeric/oracle regressions, JIT no-FMA inspection, production caller changes, source-log wiring and complete call accounting remain MISSING. The incomplete draft build had a local C# variable-shadowing error; it never passed compilation, so no pose test result is claimed. The drafts also leave live frame bindings and owner lifecycle/caller copy semantics unverified; they were not used as substitute source evidence.

For the blocking depth target: sErrorF at0x00845EBC has no committed counterpart; its temporary vector cleanup/free at0x00845EDE is managed-lifetime work in a future port; error flag and conditional debug-break tail are likewise not committed for this helper. There is no file/stream open in this range. Native depth count/return1001 are also part of the pending shared port. No engine log or side effect was added by this metadata batch.

Manifest impact: **only append M11-066 unresolved**. All test strings, statuses, titles, evidence, authority, live_path and hardware_required preserved. FIDELITY_GAPS regenerated. No verification packet, research-file edit, branch, worktree, force push or hook bypass. Stop after reporting this BLOCKED job, as authorized for this two-job order.

## Validation

Fidelity regeneration/check: PASS, 595 records. Semantic audit: exactly one append to M11-066 unresolved; all test lists and other record fields preserved. Whitespace check: PASS. Unchanged-code full suite: 4,693/4,693 PASS, zero skipped, 5m05s. Normal push gate: pending. No B-POSE-1 implementation regression result is claimed.
