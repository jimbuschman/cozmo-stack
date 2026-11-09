# MARKER-LIBRARY-DIAG-1

## Claims and evidence

- The stock marker library is linked into the engine. Its getter is 0x0089ED1C and its probe image table starts at 0x00C9BCA8. VisionMarker::Extract at 0x008A0078 consumes the label mappings at 0x008A0130, 0x008A0158 and 0x008A0180.
- The missing local blob is a host resource failure under AGENTS.md. The Cozmo.Robot.csproj extraction can continue after failure, and the live detector records missing-library rejection only per quad. The new stack warning runs once per VisionSystem during actual vision initialization, checks the configured decoder, and names both recovery options. It uses the unfiltered stack log captured by the existing cube harness. The one-shot is claimed before callback dispatch, outside the vision ownership lock. The initialization epoch is rechecked before the old startup can continue.
- EngineRobot invokes the retained vision constructor before the conditional face album NV tail (M11-059, branch 0x00650E3C). Ordinary missing-library startup keeps the initialization result and NV continuation. The warning adds no robot traffic or vision mode changes.

## Files and tests

- Vision/VisionImageQuality.cs adds the startup warning. Vision/MarkerLibrary.cs adds an internal explicit-absence fixture constructor path while preserving the public fallback.
- NativeVisionInitializationTests.CanonicalStartupReportsMissingMarkerLibraryOnceWithoutChangingInit uses the configured VisionDetector and M4Control rig's normal sync path. Three cases cover library present, absent and absent with a reentrant callback. Repeated Init checks the once-only scope. The ordinary absent case checks successful initialization and available NV continuation. The reentrant case checks that nested initialization succeeds under the new epoch.
- Builder affected suite passed 343/343 across vision startup, marker oracle, motion, face identity, app layer and the cube harness. Independent final rebuild passed 224/224 across vision startup, marker oracle and app layer, zero skipped. The batch gate is recorded in QUEUE.md.

## MISSING

- The corrupt-override path is outside this false-availability job. The marker table loader can throw on an invalid SHA before vision startup.
- The operator owns the physical cube rerun.

## Workers

Reader requested gpt-6.1-sol at medium effort. Builder and independent reviewer used the configured default request. Review checked the original startup path, detector identity, once-only scope and callback ownership. The manager requested a simpler fixture constructor and verified the final code with a clean test rebuild.

## Records

- M11-001 owns the native marker type table and nearest-neighbour bytes and is already EXACT_SOURCE. The new diagnostic covers host asset availability. M11-059 owns native vision initialization and the album tail and remains separate. No record settlement is proposed.
