# SPARKS-STACK-1 — approved native target-validity correction

Operator decision2026-10-28: **“Approve corrected contract and fix.”** This authorizes implementing the independently recovered native contract below. Formal M7 inventory/evidence/snapshot/status corrections remain Claude-owned; Codex does not edit them or run approval.

## Frozen contract checked before asking

Current `inventory/M7-behaviour.md` row885 (StackBlocks UpdateInternal005C9844), marked EXACT_SOURCE in its source table, says:

> both valid -> FindObjectOnTopOrUnderneathHelper(top, 15.0f, filter, true); else log "Stopping due to invalid blocks topBlockValid:%d bottomBlockValid:%d" + StopWithoutImmediateRepetitionPenalty return 2

Root re-read the actual file and approved snapshot before acting: M7 approved2026-10-16; inventory SHA256 `557c9920244e01651d2f5075dd4d2603d1d17cd884ef2a610e0c37a5741736b1` matches its snapshot. This is an inventory-row contradiction, not a claim that M7-018's factory record owns the whole derived lifecycle. Current records/common M8 lifecycle citations and narrower M12 parameter/helper records were compared separately.

## Independently confirmed primary contract

- At `005C98BC/005C98BE/005C98C0/005C98C2`, both valid-set lookups succeeding branches directly to `005C9A24`, skipping the neighbor query.
- Top invalid with bottom valid takes the logged invalid-target stop/penalty-suppression return2 path.
- On the bottom-invalid path, `005C9924` onward looks up bottom and top and rejects null objects. `005C99BC/005C99C0` calls `FindObjectOnTopOrUnderneathHelper` with **bottom**,15.0f, the source filter and true; `005C9A1E` requires the returned object pointer to equal the stored top before continuing.

Thus the frozen sentence has the wrong gate and reference object. This changes target validity/retargeting, readiness, outgoing work and possible objective delivery. Extractor A and independent checker B each checked the native instructions before the operator question; no implementation had been released from the contradictory row. The rest of the finite StackBlocks path is recovered and checked in the job's source packet; this decision does not authorize guessed targets, callbacks or placement behavior.

## Exact checkpoint

Asked: “May I build StackBlocks using the corrected native contract? The approved M7 inventory says to run the stack-neighbour check when both targets are valid. Two independent binary checks show it skips that check when both are valid; when the bottom is invalid, it checks for the stored top above the bottom. Official inventory and status corrections would remain with Claude.”

Operator answer: **“Approve corrected contract and fix.”**

The checkpoint follows AGENTS.md's frozen-inventory approval process. Correct native code/source tests may proceed after the complete independent read passes; official changes and whole-record settlement remain separate. No manifest/status/inventory/approval is changed by this document.
