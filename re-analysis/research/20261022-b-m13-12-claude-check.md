# Claude spot-check of B-M13-12 (7a8f79b5), manager, 2026-10-22

Instructions re-read in libcozmoEngine.so: InitInternal 0x005C0B2E..0x005C0B54 and UpdateInternal 0x005C0DB0..0x005C0DE0.

**Verdict: PASS on what it claims, with one fix queued (B-M13-13).** No record settles.

Supported:
- Init clears behavior+0x120 (0x005C0B30); reads robot+0x264 -> +0x30 -> +0x14 and compares with 3 (0x005C0B34..0x005C0B3E);
  on 3 calls the handler at robot+0x24C (`ldr.w r0,[r5,#0x24c]`, `blx 0x4a5800`, 0x005C0B44..0x005C0B4A) with the triplet
  at 0x00C6731C (u32 0x97, 0x93, 0x8F) and the behaviour name, then sets +0x120 = 1 (0x005C0B50).
- Update: platform +0x34A set and +0x355 not OnTreads -> StopActing(false,false) at 0x005C0DC4, then the
  "WaitForOnTreads" state (0x005C0DD8..0x005C0DE0). The reorder (StopActing first) matches.
- The SevereNeeds state writer is M15's (R-M15-SEVERE-INPUT, held); while unbound the gate cannot be evaluated and the
  MISSING is visible. That is correct and blocks the push until M15 builds the writer.

Fix (B-M13-13):
1. `PushDrivingAnimations`/`RemoveDrivingAnimations` are left as unbound seams although their owner exists: the
   engine calls the robot's DrivingAnimationHandler directly (robot+0x24C, 0x005C0B4A), and the stack has it
   (`CozmoAnimations.DrivingAnimations`, the M5-063 handler). Bind both seams in production to that handler's direct
   push/remove, not to the public `CozmoAnimations.PushDrivingAnimations`, which posts a game message (0xB5) and so
   runs a tick later. CODEX-BUILDER rule 13: an existing owner is never a reason to leave a MISSING.
2. Remove the three alias properties (`DrivingAnimationsPushed`, `PushedDrivingAnimations`,
   `HasPushedDrivingAnimations`); keep `DrivingAnimationsFlag`.
3. Add a test that would fail on the old code: with the SevereNeeds seam returning 3, Init pushes (0x97,0x93,0x8F) to
   the real handler synchronously and Stop removes it; with 2, nothing is pushed and Stop removes nothing.
