# Operator decisions

## LEGACY-DRIVE-TO-POSE-NATIVE-1: is the legacy public drive API a supported C# feature? (Claude worker, 2026-11-12)

**The decision:** retire or rebuild the stack's legacy public drive types. AGENTS.md: "Excluding ... a C# API feature
needs the operator."

**The types:**
- the `DriveToPoseAction(ManipulationSystem m, …)` constructor and its legacy Init;
- the legacy `DriveToObjectAction`;
- `DockHelper`;
- the standalone Explorer driver (`ExplorerBehaviors.cs:314`).

**The census** (worker's `.scratch/LEGACY-DRIVE-TO-POSE-NATIVE-1-census.md`, summarised in the job's handoff) found:
- **No production path reaches them.** The robot's behaviours run through `NativeBehaviorRuntime`, which builds only the
  native classes. They drive through the native, engine-matching DriveToPose entry (StartDrivingToPose 0x0064ACC4 with
  the drive-center conversion 0x005135D0). Their only users are:
  - the stack's own tools (`FreeplayStack.Create` for FreeplayTool/CoreChecks, ManipTool, M5Hw.cs:838);
  - the tests;
  - the public constructors.
- **The legacy flow contradicts the engine** (DriveToObjectAction::InitHelper 0x00558FB4):
  - it drives to one reduced goal, where the engine passes all pre-action poses to SetGoals at 0x00559302..0x0055935A;
  - it sets an ignore-obstacle ID, where the engine sets none;
  - it never moves the head;
  - manual speed is always false, where the engine passes +0x8C;
  - it plans from the history main pose toward unconverted, borrowed goals.

  The Explorer driver is not the engine's BehaviorDriveInDesperation flow, which is already built natively as M15-091.
- **What the guard covers:** while these types exist, `NativePathComponent` holds a legacy path on origin-change
  replan/restart (visible MISSING). `BlockWorld.LocalizeToObject` keeps the manager's path-in-flight guard for legacy
  paths. Neither fires in production, because no production path is legacy.

**Options:**
1. **Retire** the legacy public drive types as stack machinery. Move the tools and tests to the native classes; then
   remove the legacy entry, the H6/H7 holds, `LegacyEntryPathInFlight` and the BlockWorld guard. This is a C# API
   removal.
2. **Rebuild** them on the native entry as supported API: all goals, no ignore ID, the head move, manual speed and
   owned goal copies; the type-6 and Explorer goals would need a parent. Then remove the same holds.
3. **Keep** them as they are, documented as non-engine stack tools, with the holds and the guard left in place.

**Meanwhile:** the worker builds only what doesn't depend on this decision (see the job's handoff).

Capability and settlement proposals don't belong here, and they don't wait for approval. Add them to QUEUE.md as rows
and start them, per AUTONOMY.md ("Empty queue: scope the next job yourself"). This file is only for decisions
AGENTS.md reserves to the operator.
