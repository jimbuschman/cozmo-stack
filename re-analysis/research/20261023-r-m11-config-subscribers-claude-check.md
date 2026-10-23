# Claude check: R-M11-CONFIG-SUBSCRIBERS (row 57, answer 5c3e88f2) — 2026-10-23

Manager spot-check of `research/20261023-m11-config-notify-subscribers.md` (Codex), instructions re-read in
libcozmoEngine.so. **PASS for what the next build needs.** No record settles.

## Re-read

- Registration: BehaviorAudioClient ctor (0x00591E38, 304 bytes) subscribes to Robot+0x40 at 0x00591EF6..0x00591F08
  with the captured client at sp+0x14. Two clients: BehaviorManager ctor 0x005A0912..0x005A091E (stored +0x68) and
  BehaviorSystemManager ctor 0x005A5826..0x005A582C (stored +0x10). Callback window 0x00592B1E..0x00592B22 loads the
  client and passes payload+0xC to HandleRobotPublicStateChange (0x005921EE, 108 bytes). Matches R1-R4, R12.
- H4 stack predicate (0x00592320..0x0059236C): (height > 1) compared with C+0x1A; on change the new predicate is
  stored first, then PostEvent (RobotAudioClient slot +8) with 0xEC3490EC (height >= 2) or 0x3BA32BC6, object 1.
  Matches.
- Notify (0x0064DCAA..0x0064DD76), read in full: the tallest-stack weak pointer is locked; with a stack, dispatch only
  when its height differs from state+6; with none (or expired), dispatch only when state+6 was nonzero, writing 0.
  Unchanged returns at 0x0064DD6C without dispatch. Matches R8 and the groundwork's C6.

## Consequence for the build

The concrete subscribers are the two existing BehaviorAudioClients (M13-038/M13-039), whose handler is already in the
stack (`Behavior/BehaviorAudioClient.cs`). The stack's `BehaviorPublicStateBroadcaster.UpdateStackHeight`
(`Behavior/BehaviorPublicState.cs:89`) already does "return if equal, else store and SendUpdatedState", but nothing
calls it. So the B-M13-15 notify MISSING is replaced by a direct call to that owner: B-M13-18.

Not re-checked here (not needed by B-M13-18): H1-H3 and H5-H15 helpers, the field-copy table, the census scan. They
remain M13-038/039 build material with their own checks.
