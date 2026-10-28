# SPARKS-STACK-1 — Claude check

Manager, 2026-10-28. Handoff `jobs/trial-m13/HANDOFF-SPARKS-STACK-1.md` (branch b691cc61; implementation
66441408 integrated as b5c503c8; Codex full gate 6557/6557 on a87ac003). Verdict: **PASS. Nothing settled.**

## Target-validity gate (StackBlocks UpdateInternal 0x005C9844..0x005C9A24)

- It fetches two valid sets: intention 4/5 for the ObjectID at +0x124 and intention 2/3 for +0x12C (5/3 when unlock 9
  is set or +0x134 is set). Both are looked up with `find`.
- **Both found** (0x005C98BC..0x005C98C2): branch straight to 0x005C9A24, with no neighbour query.
- **+0x124 not found, +0x12C found:** the "Stopping due to invalid blocks topBlockValid:%d bottomBlockValid:%d" log,
  then StopWithoutImmediateRepetitionPenalty and return 2.
- **+0x12C not found** (both-invalid included): it looks up the located objects for +0x12C and +0x124 and stops if
  either is null. Otherwise it calls FindObjectOnTopOrUnderneathHelper(obj(+0x12C), 15.0f (0x41700000), filter, true)
  at 0x005C99C0 and requires the result to equal obj(+0x124) (0x005C9A1E); if not, it stops as above.
- With +0x124 = top and +0x12C = bottom, this is the operator-approved corrected contract
  (`jobs/trial-m13/SPARKS-STACK-1-CONTRACT-CORRECTION.md`). It contradicts approved `inventory/M7-behaviour.md`
  row 885 ("both valid -> FindObjectOnTopOrUnderneathHelper(top, ...)"). `NativeStackBlocksBehavior.cs` lines 87–97
  match the binary, including both-invalid taking the neighbour query.

## Operator checkpoints left open

1. Correcting M7 row 885 (and any record evidence quoting it) and re-approving M7-behaviour.
2. Two proposed new records (`jobs/trial-m13/PROPOSAL-SPARKS-STACK-LIFECYCLE.md`): the configured SparksStackBlock
   lifecycle, and the PlaceRel Verify/GetCompletion result. The latter would carry the approved COMPATIBILITY_POLICY
   for the four unused completion words.

Neither is done in this check.

## Not settled

The handoff proposes no existing record. M12-042, M12-028, M12-012, M12-017/025/026/031/034/037, M15-022 and M15-059
keep their broader scope. Ordinary StackBlocks 4/2 is outside this job (STACK-BLOCKS-1 is building it). The earlier
startup and manual Navigation failures stay UNKNOWN.

## Addendum: operator checkpoints done (operator approved here, 2026-10-28)

- **M7 row 885 corrected and M7-behaviour re-approved.** It now has the validity gate as above. Its tail was also
  reversed: with `+0x120 == 1` and `+0x135 == 0` the native code runs GetClosestValidBottom and retargets once
  (0x005C9A28..0x005C9A84), then always calls IBehavior::UpdateInternal (0x005C9A34/0x005C9A38). The row said the
  base update runs in that case and the retarget otherwise. No manifest record quoted the row.
- **New records, both IMPLEMENTATION_GAP:**
  - **M15-076**: the configured SparksStackBlock lifecycle (M15-freeplay re-approved).
  - **M12-050**: PlaceRelObjectAction Verify/GetCompletionUnion (M12-manipulation re-approved). Its `unresolved`
    records the operator's zero-unused-ID-words decision as COMPATIBILITY_POLICY, which moves to its own record when
    the rest settles.
