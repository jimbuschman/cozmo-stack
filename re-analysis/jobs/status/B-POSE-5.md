DONE B-POSE-5 — 2026-10-15 — built checked rows, awaiting strong verification; optional position formatter MISSING; implementation push pending.

Claim 1dc4544b. Checked source: manager-adopted research/20261015-b-pose-4-check.md. No status/evidence/frozen-field changes, hardware run or verification packet. Builder rule 2 stops the unchecked formatter unit while the other checked rows are built.

## Per-record files, lines and binary ranges

| Record | File:line | Native range / change |
| --- | --- | --- |
| M11-086 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:216` | 0x00845548..0x0084564C; name-only walk and overflow; optional formatter gap below |
| M11-086 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:253` | 0x00845524..0x00845542; NullNode wrapper |
| M11-070 / M11-086 | `cozmo-stack/src/Cozmo.Robot/Vision/NativeVisionOnlyHistory.cs:29` | 0x005311CA -> 0x005311E6; full named path as %s argument |
| M11-065 / M11-066 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:399` | 0x00846F84 cbz skips first check/comparison; second 0x00846F9E and GetParent 0x00845412 retained |
| M11-055 / M11-077 | `cozmo-stack/src/Cozmo.Robot/NativeDelocalization.cs:16` | 0x0084795E event/count first in one body; 0x00847984/8C flag, ordered reset, restore 0x008479CC and tree destroy 0x008479D6 |

## Items / remaining unit

1. Name-only wrapper/walk built, with no parent-owner check. Empty name becomes (UNNAMED), parent separator follows only a non-null parent, and the next top-of-loop cap check can return a trailing separator. Exactly 1000 nodes with a null final parent returns without overflow; a 1001-node chain appends 1000 names and their final separator, then logs Error LoopBoundOverflow with poseTreeNode.h:348, sets _errG and runs the existing debug-break gate. Name append uses its explicit length, not C-string truncation.

MISSING: optional position formatting at 0x008455B4 -> Point<3,float>::ToString 0x00846138. The adopted row establishes the call/input/order, but supplies no checked formatting-body behavior. The existing Vec3.ToString is merely a candidate and was not treated as recovered. The true branch on a non-null node reports M11-086 on the stack channel and stops with NotSupportedException, returning no guessed string. The checked production caller passes false and is fully served. The wrapper's NullNode result precedes this optional call, even for true. No native record is settled and no equivalent formatter is invented.

2. NonFlattenedPose now includes the complete child -> parent -> root path. The %s argument applies C-string termination after building the explicit-length path, retaining native handling of an embedded NUL. Error key, level and flag/break helper unchanged.
3. Null other.Node skips first check/parent comparison. The parent-wrapper branch still gives two Dev_Assert VERIFYs, followed by the existing GetWithRespectTo.NullInputPose VERIFY and false. That callee VERIFY is separate from the two parent checks. The non-null branch's three checks remain unchanged. The legacy unverified flattened branch is also excluded for a null other node.
4. Count/Event emitted at the start of one existing LogCallbackDeferral.RunGated(PoseTreeGate) body, before flag/reset/restore/tree clear. There is no count snapshot outside and no second acquisition. Engine entry construction happens at the first source call; only subscriber delivery is deferred. The callback observes completed owner release, restored flag and no held pose gate. No thread added.

## Tests and manifest

Five new cases in PoseTreeVerifyBuilderTests: ordered named path/unnamed/null/no-owner-check/embedded-NUL; 1000 and 1001-node boundaries with exact returned partial string and sErrorF/flag (no VERIFY force flush); explicit optional-formatter MISSING/stop with no engine entry; null other-node two-parent-check regression. Extended the existing vision-only test to assert the full source text. Updated removal test for single-body deferred delivery while preserving its count/type/flag checks. Tests stay in the existing globally isolated M5 process statics collection.

Focused: 66/66 passed across PoseTreeVerifyBuilderTests, NativePoseBuilderTests/BPose2 and M11DelocalizationBuilderTests/B8Tests. Full suite: 4821/4821 passed, zero skipped, 4m49s. Fidelity check: passed, 622 records; classifications unchanged.

Manifest tests/unresolved appended only for M11-086/070/065/066/055/077. Prior text/classifications retained. M11-086 explicitly remains partial, with the optional formatter unbuilt; its implementation gap is not hidden behind the name-only caller's correctness.

## Rule 10 call accounting

Reopened the checked sites and direct targets locally: named-walk wrapper/node, append helper 0x004E8048 (pointer plus length), GetParent/Dev_Assert, Point3 formatter entry (identity only, no unchecked formatting port), overflow tail, history caller and origin destructor. Existing logger targets/flag/debug gate reused.

| Calls / state | Counterpart / disposition |
| --- | --- |
| 0x0084552A | Wrapper -> new node.GetNamedPathToRoot, false built; true formatter unit MISSING |
| 0x0084553E -> 0x004E02B2 | Managed NullNode string of length 8 |
| 0x00845598 / 0x008455A4 -> 0x004E8048 | Managed explicit-length name append / literal (UNNAMED), length 9 |
| 0x008455B4 -> 0x00846138 | MISSING checked Point3<float>::ToString body; visible stack stop on non-null true requests |
| 0x008455CE / conditional 0x008455DC | Native optional formatter output append and temporary free belong to that same MISSING unit; no guessed output or native allocation behavior claimed |
| 0x008455EA | Append literal separator, length 4, only after a non-null parent |
| 0x0084560C | Existing PoseDiagnostics.Loop -> explicit Error/null channel/LoopBoundOverflow, full file:348 |
| 0x0084562E | Managed error-log temporary storage cleanup |
| 0x00845632..0x00845646 | Existing StoreAndMaybeBreak: flag then enabled debug call; native target is bx lr. No VERIFY force flush added |
| 0x005311CA / 0x005311E6 / conditional 0x005311F4 | New name-only helper argument -> existing Error source sink/flag/break; temporary storage managed |
| 0x00846F8A | Existing first AssertParent, now gated by other.Node non-null as 0x00846F84 requires |
| 0x00846F9E / 0x00846FAA -> 0x00845412/1A | Existing second check/public GetParent check then managed wrapper ownership |
| 0x00846FB4 / 0x00846FBC | Existing GetWithRespectTo; false return without extra warning; using-parent Dispose |
| 0x0084795E / conditional 0x00847980 | Existing explicit Event/null channel/poseoriginlist.destructor, count captured now inside one body; managed logger temporary cleanup |
| 0x00847984 / 0x0084798C / 0x008479CC | Existing flag read, set true, finally restore inside that one body |
| 0x008479AA indirect / 0x008479D6 | Existing ascending origin Dispose and map clear after flag restore. MISSING native shared-control-block/NotLastOwner/destructor internals remain M11-077, unclaimed |

No new file/stream operations, counters or global state. Other GetNamedPathToRoot callers remain out of scope. No MISSING is emitted into the engine source stream.
