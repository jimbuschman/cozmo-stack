DONE B-POSE-4 — 2026-10-15 — built, awaiting strong verification; implementation push pending.

Claim: 3bcab8ac. Checked source: manager-adopted research/20261015-b-pose-3-check.md, blocking 1–2 and queued 3–5. Reopened the local native call sites and targets; no new unchecked behavior added. No status changes, hardware run or verification packet.

## Per-record files, lines and binary ranges

| Record | File:line | Change / binary range |
| --- | --- | --- |
| M11-065 / M11-066 / M11-076 | `cozmo-stack/src/Cozmo.Robot/Vision/Geometry.cs:382` | IsSameAs parent branch: 0x00846F8A/9E/AA/B4; public GetParent check 0x00845412 and wrap 0x0084541A |
| M11-055 / M11-077 | `cozmo-stack/src/Cozmo.Robot/NativeDelocalization.cs:16` | 0x00847944..0x008479DE; Event 0x0084795E first, count before ordered resets/flag restoration/tree destruction |
| M11-070 / M11-076 | `cozmo-stack/src/Cozmo.Robot/Vision/NativeVisionOnlyHistory.cs:21` | 0x005310A8..0x005310D8; HasParent/GetParent/IsRoot/destructor, same nonflattened result |

Items 1–3 built. IsSameAs now executes three checks before its temporary adds an owner, then the existing GetWithRespectTo and wrapper disposal. ~PoseOriginList emits explicit source Event/null-channel/key poseoriginlist.destructor, format "%zu", origin count. Snapshot under PoseTreeGate; source callback outside it before flag/reset work, avoiding log re-entry while holding the gate. No new thread. AddVisionOnlyState uses the existing helpers and releases its temporary before the nonflattened error/return.

Serial collection: PoseTreeVerifyBuilderTests, NativePoseBuilderTests and NativePoseBuilderTestsBPose2 already use M5 process statics. M15BackupBuilderTests (the other explicit RemoveRobots caller) now uses that same collection; its existing definition disables all concurrent collections. Ordinary fixture disposal therefore cannot suppress VERIFY-count assertions by toggling the process flag concurrently. Existing M5 static isolation is retained.

Removed only the second NativePoseBuilderTestsBPose2 mention in M11-066.test. All unique old references retained. Appended test/unresolved notes in M11-055/065/066/070/076/077; statuses/evidence/frozen fields unchanged.

## Tests

- SameAsParentWrapperPerformsAllThreeChecksBeforeItAddsAnOwner: three exact child/parent VERIFY entries and three flushes, then temporary owner released.
- VisionOnlyFlatteningUsesHasParentGetParentAndIsRootChecks: unowned two-level ancestry yields child, child, parent VERIFYs and no history insert.
- LiveOriginListDestructionSuppressesIdVerifyAndRestoresTheFlag: actual robot removal, two origins; Event level, null channel, text "2" observed with origin still owned, flag unset and no held PoseTreeGate; afterward owner zero, flag restored and no ID VERIFY.
- Existing native pose arithmetic, owner checks and backup removal tests retained.

Focused: 65/65 passed (PoseTreeVerifyBuilderTests, NativePoseBuilderTests, NativePoseBuilderTestsBPose2 and M15BackupBuilderTests). Full suite: 4790/4790 passed, zero skipped, 5m12s. Fidelity check: passed, 615 records; classifications unchanged.

## Rule 10 call accounting

| Calls | Counterpart / disposition |
| --- | --- |
| 0x00846F60; 0x00846F68; conditional 0x00846F76 | Existing managed empty-name string/relative Pose3d constructor and temporary storage cleanup |
| 0x00846F8A/9E | Existing Node.AssertParent retained, in order |
| 0x00846FAA -> 0x008453D4..0x00845444 | Newly routed through existing public TryGetParent |
| 0x00845412 -> Dev_Assert 0x008460C8..0x00846138 | Existing conditional Node.AssertParent; VERIFY sink at 0x0084611A, source Error/flag/force flush retained |
| 0x0084541A -> WrapExisting 0x0084529C..0x008452C2 | Existing managed wrapper, increment node owner count |
| 0x008452A8/B4 | Existing shared-control-block retain/release represented by managed wrapper storage; native use counts remain unclaimed M11-077 |
| 0x008453F6/0x00845404/26/38 | Existing null branch constructor and managed temporary/wrapper cleanup; root fault policy unchanged |
| 0x00846FB4/BC | Existing GetWithRespectTo and using-parent Dispose after it |
| 0x0084795E | New explicit Event/null channel/poseoriginlist.destructor, count formatted invariantly before flag save/set/reset |
| conditional 0x00847980 | Native logger temporary vector storage -> managed log entry storage |
| 0x00847984; 0x0084798C; 0x008479CC | Existing read flag; set true; finally restore saved flag |
| 0x008479AA indirect | Existing ordered unique-owner Pose3d.Dispose. MISSING native shared_ptr/use-count/NotLastOwner and other destructor details remain M11-077; this job does not claim them |
| 0x008479D6 | Existing ordered origin map storage clear after flag restored; native allocator storage not emulated |
| 0x005310B4; 0x005310C2; 0x005310C8; 0x005310D0 | New use of public HasParent, TryGetParent, IsRoot, then temporary Dispose before testing flattened result |
| 0x00844AAC; 0x00844AE2 | Existing wrapper IsRoot/HasParent AssertParent, reopened; conditional VERIFY behavior retained |
| 0x00844AC8; 0x00844AFC | Node overload conditional checks reopened; not extra wrapper calls introduced here |

No new filesystem operations or counters. No MISSING enters the source engine stream. Remaining native ownership/destructor gaps are explicitly retained; this is a bounded correction to the adopted rows, not whole-pose-path settlement.
