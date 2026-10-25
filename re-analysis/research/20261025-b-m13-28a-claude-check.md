# Claude check: B-M13-28a (CORE-007 grades on the overhead writer's own calls)

- **Checked:** ee9d27f9 (full suite recorded 50a5f8fe/00951457) against `jobs/B-M13-28a.md` and the blocking finding in
  `research/20261025-b-m13-28-claude-check.md`. Manager (Opus) read the whole code diff and the new tests.
- **Verdict: PASS.** No record settles; the manifest is unchanged (M11-074 stays IMPLEMENTATION_GAP). Harness and
  diagnostics only; no change to what is sent to the robot or written to the map.

## The blocking finding is closed

- `MemoryMap.OverheadWriteCount` (`Vision/OverheadMapWriter.cs`) is incremented only inside `PrepareAndInsertOverhead`:
  once per clear-quad insert (0x0067FFE0/0x006800C8/0x0068017C), once per prepared type-9 segment insert (0x006802E0;
  `InsertPreparedInterestingEdges` issues exactly one `Tree.Insert` per segment) and once per border call (0x006803AA,
  only when the image stamp is bound). The footprint path (`MapTiming.UpdateRobotPose` → `MemoryMap.cs:187`) does not
  touch it. What is written, and when, is unchanged.
- CORE-007 grades `framesSeen > 0 && framesWithEdges > 0 && overheadWrites > 0`. Content counts are printed as
  observations, labelled by type.
- **Ordering and races.** `Observe` runs from `FrameProcessed`, which fires under `VisionSystem._busy`
  (`VisionSystem.cs:1186`), so the per-map dictionary has no concurrent access. The Freeplay writer subscribed first
  (`FreeplayStack.cs:334`) and runs synchronously under `RunGated(FrameGate, …)` (`LogCallbackDeferral.cs:54`). Its
  writes are visible to the next `Observe`. A write that lands after the last observation is uncounted, so the check
  can only under-report, never pass falsely.
- **Origin changes.** `context.Map` follows `Maps.Current` (`FreeplayStack.cs:133`, `:340`). Deltas are taken per map,
  so a change of origin neither loses earlier counts nor counts anything twice.

## Queued items

- The VisitInterestingEdge MISSING now names 0x0067E6B0/0x0067E71C.
- `MemoryMap.HasContentType` reads `Processor.TypeSets` under `_geometryGate`, as the 0x006817A8 wrapper does (it
  forwards to the tree). It still has no production caller.
- CORE-007 says type-10 border writes cannot happen while the ImageTimestamp join is MISSING.

## Tests

- `FootprintOnlyCannotSatisfyCore007`: production footprint content in the real current map; grader fails with "nothing
  reached the map". The expected value comes from the job's requirement, not from the code.
- The retained overhead-fixture test still passes through the map overload, which now reads the writer counter.
- Old-code proof is documented in the status file: all three new tests fail on 86f8a823.

## Queued (non-blocking)

- Q1: the CORE-007 loop's per-map delta logic is exercised only on hardware; a harness unit test would need a camera
  sequence. Leave it until the next map robot check is written.
