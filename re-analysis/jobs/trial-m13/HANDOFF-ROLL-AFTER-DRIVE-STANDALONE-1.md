# HANDOFF ROLL-AFTER-DRIVE-STANDALONE-1 (Claude worker, 2026-11-13)

Job 19 (my proposal from CHARGER-BOUNDING-QUAD-1). Census and implementation reports are worker-local:
`.scratch/ROLL-AFTER-DRIVE-STANDALONE-1-census.md`, `.scratch/ROLL-AFTER-DRIVE-STANDALONE-1-impl.md` (revision 2).

## Root cause: a standalone-stack wiring defect

The drive's completion was not lost. RollBlockOnSide's completion subscription ran NativeDriveToHelper.Respond, which
stored Complete. But the standalone FreeplayStack's own AIComponent (whose BehaviorHelperComponent the standalone
behaviours delegate to) was never updated: the engine hook ticks only the canonical runtime's AI, and
FreeplayStack.Tick didn't tick it. RETIRE-LEGACY-DRIVE-API-1's rebind exposed this; the standalone AI hook had been
removed earlier (e85f87ed). The canonical runtime was not affected (RollBlockOnSideLowScoreNativeTests pass).

Engine: one AIComponent per Robot (robot+0x264), ticked once per Robot::Update at 0x00513EA8..0x00513EAC
(AIComponent::Update 0x569F24 -> BehaviorHelperComponent::Update at 0x00569F40), after the first-full-state gate
(0x00513C5C) and the UpdateAllResults gate (0x00513C76), before BehaviorManager::Update (0x00513EE6) and
ActionList::Update (0x005140BC).

## The fix

- `CozmoEngine` holds a list of standalone AI updates (`AddStandaloneAIUpdate` / `RemoveStandaloneAIUpdate`, both
  through InvokeEngineRequest). `EngineRobot.Update` runs a snapshot right after the canonical AI tick: on the engine
  thread, under `_tickLock`, after both gates.
- `FreeplayStack.Create` registers its AI; `Dispose` removes it first.
- A first version ticked the AI from `FreeplayStack.Tick` on the caller thread. The verifier failed it: it ran helper
  code outside `_tickLock`, giving an ABBA deadlock with the engine thread (helper gate -> _tickLock vs _tickLock ->
  ActionList gate -> helper gate) and data races, and it skipped both gates. Revision 2 fixes both; the lock order is
  now `_tickLock` -> helper gate -> ActionList gate everywhere.
- No double tick: the canonical branch is unchanged, the standalone Tracker is unset, and the analyzer and severe
  needs are ticked only through this AI. The standalone SevereNeeds has no observable effect (`_selected` stays 3).
  The standalone analyzer now visibly reports MISSING M11-079 (no map binding) when process 1 is enabled: same result.

## Test

`FreeplayTests.TheWholeStackDrivesOffTheChargerAndPlaysWithTheCubeItSees`:
- Production reach: a RobotCompletedAction with ActionType 10 (DRIVE_TO_OBJECT; ctor 0x00558556 movs r3,#0xa; enum
  table 0x01032560) and Result 0, then the roll is queued.
- reach: unit (substituted input: the rig's legacy flat marker poses, `HasLegacyFrameInput`): the roll's dock stops at
  IDockAction::Init 0x0300001D (GetObservedMarkers empty, 0x00551976; 0x00551A82), because SetMarkerAsObserved is
  withheld (MarkerObservationTime.cs:74-77, M11-013). DockWithObject stays pinned absent.
- Negative control: with the engine-side tick removed, the test fails.

Related classes (every FreeplayStack.Create/CreateStandalone user, the roll native tests, HelperStopAndPyramidLoop,
fidelity tests): 538/538.

## Verifier: PASS on revision 2

Queued, not blocking: comments saying the standalone AI runs "before BehaviorManager::Update" hold only for the
canonical manager (the standalone one runs from the host loop; both under `_tickLock`); a registration leak if Create
throws after registering (same pattern as Create's other subscriptions); stale doc text for `AIComponentUpdate` and
the AIComponent class. The robot-reconstruction difference (the list survives a robot rebuild; the engine destroys the
AIComponent with its Robot) is recorded in M8-011.

## Records

`unresolved`/`test` only on M8-011, M12-036, M11-013. No status changed. No record settled.

New proposal: **ROLL-MARKER-OBSERVED-1** (the next stop: rig artifact or an M11-013 defect).
