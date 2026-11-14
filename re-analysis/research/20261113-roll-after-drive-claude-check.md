# ROLL-AFTER-DRIVE-STANDALONE-1 merged (manager, 2026-11-13)

Worker job commit 037ce6ca (full gate 8382/8382; verifier PASS on revision 2). Handoff:
`re-analysis/jobs/trial-m13/HANDOFF-ROLL-AFTER-DRIVE-STANDALONE-1.md`.

**Root cause:** the standalone FreeplayStack's own AIComponent (whose BehaviorHelperComponent its behaviours delegate
to) was never ticked, so a helper's stored completion was never consumed. A stack wiring defect exposed by
RETIRE-LEGACY-DRIVE-API-1's rebind; the canonical runtime was not affected.

**Re-read in the `.so`:**

| Address | What | Matches the fix |
| --- | --- | --- |
| 0x00513EA8..0x00513EAC | Robot::Update: `ldr.w r0,[r4,#0x264]` (the AIComponent), `blx` AIComponent::Update | yes: the standalone AI is ticked at this point |
| 0x00569F3C..0x00569F40 | AIComponent::Update: `ldr r0,[r4,#0x10]` (the BehaviorHelperComponent), `blx` its Update | yes: the helpers run inside the AI tick |

**Threading:** the registration list is changed only through `InvokeEngineRequest`, which runs inline on the engine
thread or under `_tickLock` (CozmoEngine.cs:2437..2439), so a stack disposed from inside its own tick doesn't deadlock;
the tick takes a snapshot. The lock order `_tickLock` → helper gate → ActionList gate is the same everywhere (the
verifier's revision-1 ABBA finding is fixed).

**Verdict: PASS.** The roll is queued after the drive completes (RobotCompletedAction type 10, result 0). No record
settled; no status changed.

**Released:** ROLL-MARKER-OBSERVED-1 (the roll's dock now stops at IDockAction::Init 0x0300001D because the rig's
legacy flat inputs don't set marker observation times: a rig artifact or an M11-013 defect).

**Queued (verifier, not blocking):** comments that place the standalone AI "before BehaviorManager::Update" hold only
for the canonical manager; a registration leak if Create throws after registering; stale doc text on
`AIComponentUpdate`.
