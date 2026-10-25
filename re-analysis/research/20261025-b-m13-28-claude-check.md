# Claude check: B-M13-28 (unwritten legacy ray fallback removed; CORE-007 reads the native tree)

- **Checked:** f73271da (full suite 3d164d99) against `jobs/B-M13-28.md`, `research/20261024-b-m13-27-claude-check.md` Q1-Q4
  and the native ray wrapper. Read-only Opus verifier; manager spot-check of the blocking finding.
- **Verdict: FAIL**, one blocking finding, in the hardware harness only. Production code is supported. Fix-up B-M13-28a.
  The manifest is unchanged; M11-074, M11-046 and M14-007 stay IMPLEMENTATION_GAP.

## Blocking

- **CORE-007 can pass with no overhead write** (`Cozmo.Conformance/CoreChecks.cs:396-397`, `Core007ContentCount` /
  `Core007HasEvidence`).
  - The check counts type 1 ClearOfObstacle as overhead evidence. It reads the current-origin map (`context.Map`).
  - The robot-footprint writer fills that same map with type 1 from the moment the robot connects: 0x0067E424
    `movs r1,#1`, 0x0067E426 `strb.w r1,[sp,#0x54]`, 0x0067E450 `blx r3`, which is map Insert. Manager re-checked these
    in the binary. The stack's path is `CozmoRobot.cs:340-343` → `MapTiming.UpdateRobotPose` → `MemoryMap.cs:187`.
  - So once a frame has edge points, CORE-007 passes even if the overhead writer wrote nothing. The "nothing reached the
    map" failure the job wanted can no longer appear, and the pass text calls footprint cells "overhead leaf cells". That
    is a misleading hardware verdict.
  - `HardwareRunnerTests.cs:1403-1422` uses only an overhead fixture, so it can't see this.

## Queued

- `VisitInterestingEdgeBehavior.cs:31`: the type-10 gap shows only as the StopInternal/Flag lifecycle. The explicit entry
  0x0067E6B0/0x0067E71C is named only in a doc comment (`MemoryMap.cs:281`). The report says otherwise.
- `MemoryMap.cs:153`: `HasContentType` (0x006817A8, `adds r0,#0x10`, which is the tree) still reads the legacy `_regions`.
  This predates the diff and has no production caller.
- CORE-007's type-10 count cannot be non-zero in production while the `ImageTimestamp` join is MISSING
  (`FreeplayStack.cs:322-326`).

## Supported

- **Q1, the tree-only ray answer** (`MemoryMap.cs:206`). The wrapper (0x00681780..0x00681792 fold with `orrs`, then
  `add r0,r7,#0x10` and `b.w 0x008CD78C`) never touches a legacy list. Nothing in production calls
  `FlagQuadAsNotInterestingEdges` or legacy `Insert`, and `Regions`/`HasContentType` have no production readers.
- **Visibility:** the type-6 MISSING still fires (`Vision/BlockWorld.cs:1263`). Type 7 has no invented call site.
- **Q2:** `M13LegacyMapSyncTests.cs:57` compares the wrapper with the tree query (tail call 0x006817A4). It fails on the
  old code for types 6, 7 and 10.
- **Race:** `SourceQuadTree.ContentCounts` takes `_geometryGate`, the same lock as Insert and the ray, and returns a
  detached copy.
- **Disposal** is correct. Focused tests 32/32; fidelity check PASS.
