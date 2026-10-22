# Independent review: R-M13-DRIVEOFF-JOINS (2026-10-22)

**Claude spot-check pending**

Reviewed answer: `research/20261021-m13-driveoff-remaining-joins.md` (HEAD 152c58fa).
Prior Codex/manager review: `research/20261021-m13-driveoff-remaining-check.md`.
Independent re-read of cited instructions in `resources/lib/armeabi-v7a/libcozmoEngine.so` via `re-analysis/tools/known.py` and `disarm.py`. No code, inventory, manifest or queue job changes beyond this file and the QUEUE row status.

## Cited instructions re-read

- Init 0x005C0B18..0x005C0BA6: clears +0x120 at 0x005C0B30; reads robot+0x264 -> AI+0x30 -> component+0x14, compares to 3 (0x005C0B34..0x005C0B3C); when equal pushes handler at robot+0x24C with name+0x40 and triplet at 0x00C6731C (0x97,0x93,0x8F) (0x005C0B40..0x005C0B4A), then sets +0x120 (0x005C0B50). Verified ROM words 0x97,0x93,0x8F. SevereNeedsComponent ctor 0x005729FC..0x00572A14 stores 3 at +0x14 (0x00572A0E); AI ctor 0x00569B3A resolves to it, stores at AI+0x30 (0x00569B3E). Gate is SevereNeeds state, not animation state. Confirmed.
- StopInternal 0x005C0D90..0x005C0DA4: tests +0x120; nonzero forwards name through 0x008CABCC -> PLT 0x004A5818 (RemoveDrivingAnimations); zero returns; does not clear flag. Confirmed.
- Update 0x005C0DA8..0x005C0E32: platform-true/off-treads calls StopActing(false,false) then names WaitForOnTreads (0x005C0DB0..0x005C0DE0), returns 1; platform-true/on-treads re-drives if +0x84==0 (0x005C0E0E..0x005C0E18); platform-false/current-tag-nonzero keeps running (0x005C0DF4..0x005C0DF8); platform-false/tag-zero stamps whiteboard +0x44 and returns 2 (0x005C0DFA..0x005C0E0A), no OffTreads check. Confirmed.
- StopActing 0x005BD34C..0x005BD43A: clears +0x84 before ActionList.Cancel when keepAction=false; post-call clear conditional on tag unchanged. Confirmed.
- Constructor 0x005470F0..0x0054725E: accel 0x43480000, decel 0x43FA0000, +0x88/+0x89 zero, +0x8A one, speed table -80.0f (0xC2A00000) or 100.0f (0x42C80000), nonnegative distance selects 100. Confirmed.
- Completion receiver 0x005BE1E6..0x005BE224: matches tag, clears it, invokes slot 0x80(false), then tests running/callable before invocation. Confirmed.
- M5-063 DrivingAnimationHandler Push/Remove/Init and CozmoAnimations public B5/B6 posted variants: confirmed per existing M5 evidence; internal direct call vs posted game message boundary stands.

## Findings

All D1-D6 rows in the answer hold against the binary. The wait/update matrix and StopActing reorder are supported. The severe-need writer remains genuinely unknown (no live SetSevereNeedExpression/ClearSevereNeedExpression owner found; constructor 3 alone is not proof). The concrete StartActing tag binding / completion subscription (M8-015) remains an open owner. Objective-achieved/mood recipients remain outside this scope.

No contradiction found. The answer's claim that stale "animation state 3" wording should be corrected is supported (gate is SevereNeeds state 3, verified).

## Finish checklist (cozmo-one-job.md)

1. Job done: independent review of R-M13-DRIVEOFF-JOINS (research lane).
2. Engine instructions re-read myself: 0x005C0B18..0x005C0BA6, 0x005C0B30, 0x005C0B34..0x005C0B50, 0x005C0D90..0x005C0DA4, 0x005C0DA8..0x005C0E32, 0x005BD34C..0x005BD43A, 0x005470F0..0x0054725E, 0x005BE1E6..0x005BE224, 0x005729FC..0x00572A14, 0x00569B3A/3E, 0x00C6731C..24, 0x008CABCC, 0x004A5818.
3. Hooks/seams added: none (review only).
4. Anything added that the job didn't ask for: none.
5. Tests: none added (review only).
6. Focused tests: N/A (no code change).
7. Records: none settled. M13-017 and M8-015 remain IMPLEMENTATION_GAP.

No new research queued. No code edited. No record settled.