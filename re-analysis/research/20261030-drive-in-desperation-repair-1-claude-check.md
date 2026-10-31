# DRIVE-IN-DESPERATION-REPAIR-1: Claude check (manager, 2026-10-30)

**Verdict: PASS. Merged** from `codex-m13-manager-2` 2fc16cca (full gate on dcc55ec4, 7012 PASS). **Nothing settled.**
Checked by the manager, with no separate verifier: the production change only widens the factory, and the behaviour body
was verified for Energy (`research/20261030-drive-in-desperation-energy-1-claude-check.md`).

- **Config:** the shipped `needs_SevereLowRepairState.json` has useCubes false, idle 5..20 s, the Repair request trigger
  and the custom motion profile.
- **The false branch:** 0x005D9140..48 reads +0x30 of the params at +0x11C; `cbz` skips the cube path. `FromIdle` line 55
  goes straight to NoCubes.
- **NoCubes 0x005D9F14..42:** if rounds >= 1, decrement and drive; otherwise redraw and request. C# lines 70-73 match.
- **Drive callback 0x005DA2DE..F0:** with the flag false it returns, so no search starts. C# line 92 matches.
- **Scope:** BehaviorContainer and NativeBehaviorRuntime only add the Repair ID; the rest is diagnostic text and XML. The
  legacy XML with the swapped units is gone, which closes that queued item from the Energy check.

Admission stays refused (M8-013), so nothing reaches this in production. The DriveInDesperation lifecycle proposal is
the same single record, widened to Repair. It goes to the operator.
