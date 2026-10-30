# WAIT-1: Claude check (manager, 2026-10-29)

**Job.** A Claude-run job. The cozmo-extractor read is `research/20261029-wait-1-read.md`, and the manager checked
it against the .so (the stubs 0x0059EC00..0x0059EC3C and the vtable slots at 0x010242B0). The build is the
manager's own: a tiny diff with no implementer.

**Verdict: PASS. Nothing settled.** Record M15-083 was added with operator approval.

## Build
- **`WaitBehavior` (ExplorerBehaviors.cs):**
  - RunnableGate20 → true (R1, 0x0059EC0E);
  - RunnableGate24 → false (R2, base 0x0059EC12);
  - RunnableGate28 → true (R3, 0x0059EC16);
  - OnStart does nothing (I1, 0x0059EC34); the invented "waiting" log is removed;
  - KeepsRunningWithoutAction stays true (U1, 0x0059EC00);
  - IsRunnableInternal, Resume and Stop stay the base ones, which match R4, P1 and S1.
- **`FreeplayStack.cs:133` (the build with no vision):** now also builds `Wait`, since wait.json ships (C3), so the
  Selection chooser's fallback (A2) exists there too.
- **Tests (`WaitNativeTests`):** expectations come from the stubs and the shipped configs.
  - Both configs load through the production loader.
  - The gates: off treads and carrying are runnable; robot+0x34A is not.
  - No action is queued, and Update stays Running.
  - 157/157 of the Wait, Freeplay, Sparked and Selection tests passed.

## Manifest
- M15-083 added.
- M7 inventory row 8 and Q2 corrected: BehaviorWait does override.
- M7 and M15 re-approved.

## Not settled, because
- Every test is unit or production-loader only. No test enters the severe-needs StrictPriority chooser.
- Open: the ActivityFeeding keys 4/7 (M15-061); the executable-type lookup (no G2E channel); the name of robot+0x34A.
