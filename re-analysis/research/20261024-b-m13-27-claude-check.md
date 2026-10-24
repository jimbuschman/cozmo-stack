# Claude check: B-M13-27 (stop the legacy all-objects map sync)

- **Checked:** `B-M13-27-wip` 91e0b5e9 (implementation) and 88c46297 (report), against `jobs/B-M13-27.md`,
  `research/20261024-m11-object-map-data.md`, `research/20261023-m11-map-legacy-producers.md` and
  `research/20261024-b-m13-23-24-claude-check.md`.
- **Verdict: PASS.** No blocking finding. M11-074 and M14-007 stay IMPLEMENTATION_GAP; no record settles. The manifest
  is not edited.
- **The builder's full-suite failure** (M3M4ReviewFixTests.CameraHandoff…, setup line 38) is a test-setup race, not this
  diff. A ConnectionResponse injected before the transport's posted connect had created the connection was dropped as
  "unconnected source". Fixed on main in 7588e84e (6155/6155).

## Supported

- **SyncFromWorld removed.** The research row says not to replace the native add with an all-objects polygon sync
  (`20261024-m11-object-map-data.md:19`). The native add and removal are live: `BlockWorld.cs:2160`/`:2429` call
  `Maps.UpdateObjectPose`, deletion reaches it through `BroadcastObjectPoseChanged` (`:2295`), and the height gate is
  `IsPoseTooHigh(…,1,15,0)` at `MapTiming.cs:115`. Nothing in src or tests calls SyncFromWorld. Nothing that reads
  `Regions`/`HasContentType` depended on Sync's object regions.
- **FreeplayStack.Tick.** Only the Sync call is gone; it had no side effects. The tick order is unchanged.
- **Ray wrapper.** 0x0068176E..0x006817A4 folds the eleven type/flag pairs and tail-calls the tree at 0x008CD78C. It has
  no polygon-list check, so the tree query alone matches the engine. The region copy under `_gate` runs with the polygon
  test outside the lock, so there is no race.
- **Tests** run the real Tick, BlockWorld add path and face query. The expectations come from the native height gate and
  object add. They are labelled `unit` and everything is disposed.

## Queued (non-blocking)

- **Q1.** `MemoryMap.cs:205-212` keeps the types 6/7/10 legacy fallback. Nothing in `cozmo-stack/src` calls
  `MemoryMap.Insert` or `FlagQuadAsNotInterestingEdges`, so job step 3's condition for removing it is met. The report's
  reason for keeping it is inaccurate. No observable effect: in production the legacy list is empty.
- **Q2.** `M13LegacyMapSyncTests.cs:58-71` restates the host constant {6,7,10}. Its negative cases are inputs that
  production cannot produce, so it is not evidence for either record.
- **Q3 (was there before).** CORE-007 (`Cozmo.Conformance/CoreChecks.cs:369-386`) decides on `map.Regions`, which only
  legacy writers fill. It would report "nothing reached the map" while the overhead writer fills the tree: a misleading
  hardware verdict.
- **Q4 (was there before).** No ReportMissing names the missing ObstacleUnrecognized (type 7) producer.
