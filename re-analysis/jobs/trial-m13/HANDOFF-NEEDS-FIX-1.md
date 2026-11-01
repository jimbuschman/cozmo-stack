# NEEDS-FIX-1 — READY FOR CLAUDE

Job 2 of the five ACTION jobs, on top of SCORING-FIX-1 9351d778. Current origin/main 08e9dc34 was fetched and the branch rebased before starting. One batch full gate remains due after the five jobs under AUTONOMY Speed changes.

Changes: Needs.cs, NeedsMutation.cs, EngineNeedsOwner.cs and NeedsFix1Tests.cs. Check6 was used only as a lead; extractor and independent reviewer confirmed the following in the real engine:

- Reject while paused or while the connected-first-robot pointer is absent (0x006960E0..F8). Production initialization now starts with that pointer absent; the existing connection/disconnection paths set/clear it (0x0069211A..1C, 0x00694388..390, 0x00695932). Standalone manager defaults remain an explicitly separate fixture boundary.
- Check each action's f32 deadline, admitting equality and unordered; stamp f32(now + signed cooldown) unless abs(cooldown) < epsilon 0x3727C5AC, so equality and unordered stamp (0x00696146..1DA). InitReset clears the 86 deadline slots (0x006935CE..D6, __aeabi_memclr4 of 0x158 bytes at +0x244).
- Call ApplyDelta in Repair/Energy/Play order even for zero pairs (0x00696886..904); the native random double draw is unconditional, including width zero (0x0069C902..91A). No damage-choice draw was removed or synthesized.
- RepairHead/Lift/Treads clear parts 0/1/2 before deltas (0x006967DE..878). After all three deltas, those repair actions restore Repair to configured maximum if no damaged parts remain (0x00696906..93E). The public real-action path is built; no prediction API or copied state was invented.
- Consume ApplyDelta's true return by starting fullness cooldown (0x006968E2..EE). The return is before Repair cap/floor and does not require the previous level to be below Full. Refresh deadline=f32(now+duration) but preserve a nonzero existing start (0x006970C0..0x00697100). Thus repeated positive fills extend the window.

Source/static review PASS. Affected tests **461/461 PASS, zero skipped** (Needs, M15MutationCheckedRowsTests, M15RestorationCheckedRowsTests, FreeplayTests and EngineAppLayerTests filter); test/shutdown exits 0 and postflight process count 0. Log .scratch/needs-fix-1/affected-1.log. New tests derive expected draw counts, signed epsilon/equality deadlines, part identity/last-part maximum and repeated-fill timing from the cited instructions. Unit tests disclose supplied RNG, clock and damage state. The production-entry test uses the engine-owned Needs, shipped configuration and actual ConnectToRobot dispatch/engine tick through a fake transport; it exercises the public action API before and after connection, without assigning the pointer flag. No hardware claim.

M15-029 and M15-046 remain IMPLEMENTATION_GAP; no whole-record settlement proposed. MISSING: deferred whitelist bytes +0x1DF, prediction entry/state copy and its shared-RNG ownership, broader needs rewards/persistence/messages remain open. The native NeedsState message payload and local strategy recipient are reserved for STRATEGIES-FIX-1 and require their own primary check. The supported immediate action path was changed without claiming those joins complete.

Requested models: needs_read gpt-6.1-sol medium; needs_build and needs_review DEFAULT model/effort overrides omitted. Backend identity unobserved. Reviewer fix rounds 1 (meaningful multi-part/live-owner/signed-epsilon test coverage); production behavioral corrections after review 0, compiler corrections 0. Builder MISSING/questions are listed above; no new operator decision. No status, approval, hardware or main push changes. READY FOR CLAUDE awaits the batch full gate.

## Completed batch gate (2026-10-31)

Full gate **7087/7087 PASS, 0 failed, 0 skipped** at `af83ed19e0856a8234a9742df04ce9be8a109e08`; gate/shutdown0. Logs `.scratch/start-path-fix-batch-gate-2{,-console}.log`. First gate at520ad93c was7084/7087 with three source-contradicted/stale test assertions; those test-only corrections passed76/76 and the full rerun. Production code did not change between those gates.

Effective rebased job commit `3dd034bb` (main8d425383; source-equivalent rebase); older hashes above preserve historical read/test context. Batch test correction `af83ed19`. Only metadata follows this passing tip. Fidelity statuses/evidence/titles/approvals are unchanged; M15-098/100 remain proposals for Claude, no whole M7-019/M15-006 settlement. Hiking code/test remain unchanged on remote parking branchbb5ea84a and are restored next.

Canonical hold resolved by REACTION-CANONICAL-BIND-1. Full canonical-fix batch gate **7099/7099 PASS,0 failed,0 skipped** at `7d3bbc63e197b787e58bac770d34b75453399388`. Logs `.scratch/start-path-fix-batch-gate-4{,-console}.log`; gate/shutdown0. This supersedes the canonical hold for all eight earlier jobs and includes MOOD-FIX-1. Protected statuses/evidence/titles/approvals remain unchanged. No hardware or whole-behavior acceptance claim.
