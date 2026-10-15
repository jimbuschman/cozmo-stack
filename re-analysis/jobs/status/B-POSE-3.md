DONE B-POSE-3 — 2026-10-15 — built, awaiting strong verification; pushed implementation commit 1cc1b6b69eb87f152926671c0118855bbb1f245f.

Claim pushed: 2fe064e8. No fidelity classification was changed. M11-066/M11-067/M11-077 test and unresolved strings were appended, preserving all prior text. No hardware run or verification packet.

Checked source: manager-adopted `research/20261014-pose-tree-verifies.md` sections 3–4. Reopened the listed native targets in the local engine, including Dev_Assert 0x008460C8..0x00846138, RemoveOwner 0x008448B4..0x00844914, HasSameParentAs 0x00844D44..0x00844D7C, FindRoot 0x00845218..0x0084537A, GetWithRespectTo 0x00845A34..0x00845DFE, GetTreeDepth 0x00845E8C..0x00845F40, ~PoseOriginList 0x00847944..0x008479DE, AllowUnownedParents 0x00845704..0x0084570E, and VERIFY 0x0080D9A4..0x0080DA34. `sDumpCallstack` 0x0080B0A6 is bx lr; provider slot +0x14 uses the existing force-flush sink. No unchecked extraction was used to extend the adopted rows.

## Per-record production paths

| Record | Files and lines | Native range / change |
| --- | --- | --- |
| M11-066 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:170` | 0x008460C8..0x00846138 conditional Error VERIFY; silent null/allowed/owned gates, exact signed IDs and C-string names; caller continues |
| M11-066 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:183` | 0x00844D44..0x00844D7C identity skips both; null left parent skips right; otherwise left then right |
| M11-066 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:189` | 0x00844BF2/0x00844C66 once per visited node; existing 0x107/0x10E caps unchanged |
| M11-066 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:208` | 0x008452E4/0x008452F2 twice per node-step; public wrapper first checks at 0x00845248 |
| M11-066 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:232` | 0x00844AAC/0x00844AE2 IsRoot/HasParent; node overloads share the same check |
| M11-066 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:288` | 0x00845412/0x00845452 public/node overload validation; existing root M1-034 skip retained |
| M11-066 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:318` | 0x00845AC8/AD2, 0x00845B5C/B66 after PreCompose; c-loop calls HasSameParent, transforms both, checks to then from at 0x00845C00/C0A; caps unchanged |
| M11-066 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:374` | 0x00846F8A/F9E first parent probe and second probe on parent-wrapper branch |
| M11-067 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:236` | 0x0084577A/0x00845834/0x008458CC/0x00845924 constructors initialize a new node count to one; copy does not copy count; existing implementation retained |
| M11-067 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:258` | 0x008452BE WrapExisting increments shared node owner count; existing implementation retained |
| M11-077 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:176` | 0x008448B4..0x00844914 count zero VERIFY/no decrement; decrement otherwise; final-ID VERIFY only when flag clear and ID nonzero |
| M11-077 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:263` | null assignment uses RemoveOwner; non-null assignment still preserves destination owner count, as adopted |
| M11-077 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:404` | existing wrapper release calls RemoveOwner; remaining shared_ptr/control-block/destruction MISSING retained |
| M11-077 | `cozmo-stack/src/Cozmo.Robot/NativeDelocalization.cs:11` | 0x0084798C/0x008479CC save/set/restore AllowUnownedParents around ordered origin unique_ptr release, then clear node storage; existing pose writer gate and deferred callbacks |
| M11-077 | `cozmo-stack/src/Cozmo.Robot/RobotLifetime.cs:112` | 0x005113CC existing ordered lifetime release now binds the origin-list owner; runs on the engine removal path |
| M11-066/M11-077 | `cozmo-stack/src/Cozmo.Robot/Vision/PoseDiagnostics.cs:18` | existing VERIFY sink reused: Error -> error flag -> no-op call-stack target -> bound force flush; MISSING remains on deduplicated stack channel |

## Tests and validation

`PoseTreeVerifyBuilderTests` checks exact UnownedParent payload including C-string truncation, Error level and flush count; owned/null silence; a/b old/new check counts; c-loop to-before-from order including HasSameParent probes; FindRoot double checks; GetParent before wrapping; both RemoveOwner VERIFYs and non-underflow; constructor/copy/wrap/non-null assignment counts; exactly 1001 nodes with both root cap lines and depth line, correct 1000-unit transform and unreachable a/b/c caps absent; live Robot removal invokes origin destruction and restores the flag without ZeroOwnersWithID.

The older `NativePoseBuilderTestsBPose2.PoseMissingNeverEntersTheEngineSourceLog` expectation for an unconditional Dev_Assert MISSING is removed: that helper now has a checked conditional VERIFY. The guard still checks that the remaining destruction MISSING is deduplicated and does not enter the engine stream.

Focused: 38/38 passed (final code, including the owned-walk case and to-side advance before the from-side check). Full suite: 4759/4759 passed, zero skipped, 5m49s; the subsequent pointer-advance ordering correction was rechecked with the affected 38 tests, per the affected-diff rule. Fidelity checker: passes with 609 records, classifications unchanged. The normal push gate reruns the suite on the final commit. A semantic comparison against HEAD confirms only the three test and unresolved fields changed, and every prior string remains a prefix.

## MISSING / remaining uncertainty

- No existing C# counterpart for GetRootID (0x00844B24), GetParentString (0x00844E42/52/84), GetRawParentPtr (0x00844FB8), IsChildOf (0x00844FD0/EE), IsParentOf (0x0084500E/32), or ToPoseStruct3d (0x00846D76). Their enclosing bodies/input contracts are outside the checked builder rows; this job does not invent those APIs or claim their call sites are built.
- Shared_ptr control-block ownership, `_node.use_count()==1`/NotLastOwner, node on-zero-shared/bookkeeping and inlined wrapper lifetime census remain M11-077/M11-084 gaps. Managed node retention and explicit wrapper owner counts are not a full port of native allocation/lifetime. The existing destruction MISSING stays visible on the stack channel.
- M11-084's unowned-parent reachability/inline attaches/origin erase and rejigger paths and cross-thread census are explicitly out of scope. The existing engine pose writer gate is used for origin teardown, with callbacks deferred beyond the gate. No thread is introduced.
- Checked walk caps a/b/c remain logged in production but are unreachable through the existing gates; no artificial test bypasses those gates.

## Rule 10 call accounting

Calls in reopened cited instruction spans are listed below. Existing arithmetic/logger/runtime scaffolding is reused; no native heap bookkeeping is claimed recovered by managed allocation. Ranges include adjacent overloads where the job lists multiple call sites. Unbuilt enclosing APIs and native refcount/storage tails are explicitly marked MISSING.

| Native call address | Counterpart / disposition |
| --- | --- |
| 0x0080D9C2 | Existing PoseDiagnostics.Loop/Error, flag and break gate |
| 0x0080D9E4 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x0080DA02 | Managed string/storage counterpart; MISSING native allocation/control-block bookkeeping (M11-077) |
| 0x0080DA08 | Reopened target bx lr; no engine output |
| 0x0080DA16 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x0080DA28 | PoseDiagnostics.Verify bound provider flush (slot +0x14); conditional when provider exists |
| 0x0080DA62 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x0080DA68 | Native ABI unwind/canary tail; managed exception runtime, no successful-path output |
| 0x008448FA | PoseDiagnostics.Verify: Error, flag, call-stack no-op, provider flush |
| 0x00844AAC | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844AC8 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844AE2 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844AFC | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844B24 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844B52 | Existing PoseDiagnostics.Loop/Error, flag and break gate |
| 0x00844B74 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00844B8C | Existing EngineErrorState.StoreAndMaybeBreak |
| 0x00844BB6 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00844BBC | Native ABI unwind/canary tail; managed exception runtime, no successful-path output |
| 0x00844BF2 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844C20 | Existing PoseDiagnostics.Loop/Error, flag and break gate |
| 0x00844C42 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00844C5A | Existing EngineErrorState.StoreAndMaybeBreak |
| 0x00844C66 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844C94 | Existing PoseDiagnostics.Loop/Error, flag and break gate |
| 0x00844CB6 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00844CCE | Existing EngineErrorState.StoreAndMaybeBreak |
| 0x00844D00 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00844D06 | Native ABI unwind/canary tail; managed exception runtime, no successful-path output |
| 0x00844D52 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844D5E | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844DB6 | MISSING native helper/indirect storage or overload tail #0x4a445c; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844DEA | MISSING native helper/indirect storage or overload tail #0x4a4468; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844DF6 | MISSING native helper/indirect storage or overload tail #0x4a403c; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844E18 | MISSING native helper/indirect storage or overload tail #0x4e4598; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844E26 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00844E34 | MISSING native helper/indirect storage or overload tail #0x4a4474; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844E42 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844E52 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844E6E | MISSING native helper/indirect storage or overload tail #0x4a4474; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844E76 | MISSING native helper/indirect storage or overload tail #0x4a4474; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844E84 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844E8C | MISSING native helper/indirect storage or overload tail #0x4bcd50; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844E94 | MISSING native helper/indirect storage or overload tail #0x4a4474; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844EA2 | MISSING native helper/indirect storage or overload tail #0x4a4474; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844EAA | Managed string/storage counterpart; MISSING native allocation/control-block bookkeeping (M11-077) |
| 0x00844EDA | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00844EEA | MISSING native helper/indirect storage or overload tail #0x4a44a4; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844EF0 | MISSING native helper/indirect storage or overload tail #0x4a44b0; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844F06 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00844F14 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00844F50 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00844F60 | MISSING native helper/indirect storage or overload tail #0x4a44a4; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844F66 | MISSING native helper/indirect storage or overload tail #0x4a44b0; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00844F6C | Native ABI unwind/canary tail; managed exception runtime, no successful-path output |
| 0x00844FB8 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844FD0 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00844FEE | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x0084500E | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00845226 | Managed string/storage counterpart; MISSING native allocation/control-block bookkeeping (M11-077) |
| 0x0084522E | Managed string/storage counterpart; MISSING native allocation/control-block bookkeeping (M11-077) |
| 0x0084523C | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00845248 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00845254 | PoseTreeNode.Root / Pose3d.FindRoot |
| 0x0084525C | Existing Pose3d.WrapExisting; increments owner count |
| 0x00845264 | MISSING M11-077 native control-block counts; explicit wrapper Owners is separate |
| 0x0084526E | Existing Pose3d.WrapExisting; increments owner count |
| 0x0084527C | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x0084528E | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00845294 | Native ABI unwind/canary tail; managed exception runtime, no successful-path output |
| 0x008452A8 | MISSING M11-077 native control-block counts; explicit wrapper Owners is separate |
| 0x008452B4 | MISSING M11-077 native control-block counts; explicit wrapper Owners is separate |
| 0x008452DA | MISSING M11-077 native control-block counts; explicit wrapper Owners is separate |
| 0x008452E4 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x008452F2 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x008453F6 | Managed string/storage counterpart; MISSING native allocation/control-block bookkeeping (M11-077) |
| 0x00845404 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00845412 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x0084541A | Existing Pose3d.WrapExisting; increments owner count |
| 0x00845426 | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x00845438 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x0084543E | Native ABI unwind/canary tail; managed exception runtime, no successful-path output |
| 0x00845452 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x0084546A | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x008458E2 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x008458E8 | Native ABI unwind/canary tail; managed exception runtime, no successful-path output |
| 0x00845908 | Managed string/storage counterpart; MISSING native allocation/control-block bookkeeping (M11-077) |
| 0x00845910 | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x0084591A | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x00845930 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00845936 | Native ABI unwind/canary tail; managed exception runtime, no successful-path output |
| 0x00845950 | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x00845958 | PoseTreeNode.RemoveOwner |
| 0x008459A8 | PoseDiagnostics.Verify: Error, flag, call-stack no-op, provider flush |
| 0x008459B6 | MISSING M11-077 native control-block counts; explicit wrapper Owners is separate |
| 0x008459DA | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x00845A0C | MISSING native helper/indirect storage or overload tail #0x4e462a; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00845A28 | MISSING native helper/indirect storage or overload tail #0x4e8048; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00845A62 | PoseTreeNode.HasSameRoot |
| 0x00845A7A | MISSING native helper/indirect storage or overload tail #0x4a811c; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00845A96 | MISSING native helper/indirect storage or overload tail #0x4a811c; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00845AA8 | Existing PoseTreeNode.Depth; capped result/log unchanged |
| 0x00845AB0 | Existing PoseTreeNode.Depth; capped result/log unchanged |
| 0x00845AC0 | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x00845AC8 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00845AD2 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00845B04 | Existing PoseDiagnostics.Loop/Error, flag and break gate |
| 0x00845B26 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00845B3E | Existing EngineErrorState.StoreAndMaybeBreak |
| 0x00845B54 | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x00845B5C | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00845B66 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00845B96 | Existing PoseDiagnostics.Loop/Error, flag and break gate |
| 0x00845BB8 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00845BD0 | Existing EngineErrorState.StoreAndMaybeBreak |
| 0x00845BE4 | PoseTreeNode.HasSameParent |
| 0x00845BF0 | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x00845BF8 | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x00845C00 | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00845C0A | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00845EBC | Existing PoseDiagnostics.Loop/Error, flag and break gate |
| 0x00845EDE | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00845EF6 | Existing EngineErrorState.StoreAndMaybeBreak |
| 0x00845F22 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00845F28 | Native ABI unwind/canary tail; managed exception runtime, no successful-path output |
| 0x0084611A | PoseDiagnostics.Verify: Error, flag, call-stack no-op, provider flush |
| 0x00846F76 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00846F8A | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00846F9E | PoseTreeNode.AssertParent at built callers; MISSING in absent enclosing APIs listed above |
| 0x00846FAA | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x0084795E | MISSING native helper/indirect storage or overload tail #0x4a4f90; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00847980 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00847984 | Existing Transform3d/quaternion arithmetic, unchanged |
| 0x0084798C | DestroyPoseOriginOwners flag save/set/restore |
| 0x008479AA | DestroyPoseOriginOwners -> Pose3d.Dispose -> RemoveOwner; MISSING remaining native shared_ptr destruction |
| 0x008479CC | DestroyPoseOriginOwners flag save/set/restore |
| 0x008479D6 | Existing Pose3d wrapper arithmetic/lifetime; MISSING remaining native shared storage (M11-077) |
| 0x00847A02 | Managed string/vector storage release; MISSING native shared storage bookkeeping (M11-077) |
| 0x00847A0E | MISSING native helper/indirect storage or overload tail #0x847a38; enclosing path not expanded beyond checked rows (M11-067/M11-077) |
| 0x00847A14 | MISSING native helper/indirect storage or overload tail #0x4e39f8; enclosing path not expanded beyond checked rows (M11-067/M11-077) |

Push gate: 4759/4759 passed, zero skipped (5m20s). Main advanced during the gate; clean rebase and normal push succeeded. The hook reused its byte-identical code/records pass. Pushed implementation: 1cc1b6b69eb87f152926671c0118855bbb1f245f.
