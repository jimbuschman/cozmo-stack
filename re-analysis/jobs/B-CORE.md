# Job B-CORE: rebuild the connection and device core

**Agent:** opencode (DeepSeek or another cheap model), as cozmo-manager. **Type:** build. The first job in the new format:
the production path is named, `CHECKLIST.md` applies, and **this job never settles a record** (checklist section 6).
It supersedes R-DEV's remainder: R-DEV stopped after its M4 batch, and the 2026-09-29 audit then demoted the core
records.

**Why this first:** every robot session and every motion goes through this code. Readiness, the NV queue and the track
locks decide what goes on the wire from the first second of a connection.

## Before you start

1. Read `re-analysis/jobs/README.md`, `AGENTS.md`, `.opencode/agent/cozmo-manager.md` and
   **`re-analysis/jobs/CHECKLIST.md`**. Give the checklist to `@cozmo-implementer` and `@cozmo-verifier` with every
   batch.
2. Your rows are the audit findings: `re-analysis/research/20260929-audit-complete.md` (M1, M3 and M4 rows), and each
   record's `unresolved` text in the manifest. These cite addresses, and the manager checked the central ones in the
   binary. Also use `re-analysis/research/20260929-R-DEV-pre-extraction.md` and R-DEV's status
   (`status/R-DEV.md`, which says what it built). Have `@cozmo-verifier` check any row you build on that the manager
   didn't check. A `MISSING:` goes to `@cozmo-extractor`.
3. **Claim the job:** write `status/B-CORE.md` with `CLAIMED <date time>`, then commit and push it alone.

## The batches, with their production paths

**Batch 1: the NV queue (M3-025, M3-026, M3-027, M3-029, M3-030, M3-031, M3-035, then M3-022 and M3-023).**
- Engine:
  - `NVStorageComponent::Read` 0x00644E14 only validates and queues (emplace_back 0x00644E82);
  - `NVStorageComponent::Update` sends the queued request, in state 0 (ProcessRequest, 0x006456BC..0x006456CC);
  - `Robot::Update` calls it at 0x0051416A, after Gate A (0x00513C5C);
  - a completion only calls `SetState(0)` (0x006437EA), so the next request goes out on the next Update.
- C#: `NvStorageComponent.Read` (NvStorage.cs) must only queue. `NvStorageComponent.Update` sends. Check that the call
  from `CozmoEngine.cs` (around line 1011) sits where the engine's does, after the sync gate. The deadline and the clock
  are the synced robot+0x2C (M3-031). Fix `GetBaseEntryTag` (M3-025, 0x006442B2, 0x00644228..0x00644338).
- Contradicted tests to fix from the source: `M3DeviceTests` lines 961-980, 1105, 1118-1130 and 1378-1397 (per the
  audit).

**Batch 2: readiness (M1-041, M1-028, M3-033, M3-034).**
- Engine: ready-to-stream (+0x2A) is set by the NV on-idle callback after the whole connection queue drains: the 12
  constructor reads, CameraCalib, Lab 0x196000 (0x0052E3AA → 0x006A5B1E) and Needs. The ImageRequest send result is
  discarded (0x0051530C).
- C#: `CozmoEngine.cs` near line 1671, `robot.NvOnIdle(() => robot.ReadyToStream = true)`. Queue the engine's reads in
  its order (M3-033). Wire each callback to its layer where the layer exists (M3-034). Where it doesn't, the read still
  queues and completes, and `unresolved` names the missing sink. Remove the early return after a failed ImageRequest
  send (M1-041 / M4-020).
- Fix the contradicted test at `EngineAppLayerTests.cs:1006-1021`.

**Batch 3: head and lift actions (M4-001, M4-003, M4-016, M4-020, M4-011, M4-025, M4-008, M4-010).**
- Engine:
  - the game-path moves run through IActionRunner::Update (0x00540440 onward);
  - it fails with 0x03000019 if the action's tracks are locked (0x00540572..0x0054057C), else it locks them
    (0x0054058E, sending DisableAnimTracks), and unlocks them at the end (0x005408EC);
  - the head mask is 1 (0x00547EAC) and the lift mask 2 (0x005489EE);
  - the timeout is the 30.0 default slot (0x0052B0C2);
  - the angle is rescaled to (-pi, pi] first (0x0084C832 → 0x0084C87C);
  - IsHeadInPosition uses the strict Radians::IsNear (0x00548528);
  - BlockFilter::Init is gated on SetPhysicalRobot(true) (0x0051391E..0x00513954).
- C#: `Motion.cs` RunAsync (around lines 667-709) and the head/lift constants (lines 123-128). **Write the engine's
  float bits:**
  - head min 0xBEDF66F3 and max 0x3F46D3F2;
  - RS6 0xBEFA35DD and 0x3F543B67;
  - tolerance 0x3D0EFA35.

  The lint (`FidelityLiteralLintTests`) lists the old decimals in its baseline. Remove each line as you fix it.
- Fix the circular test at `M4ControlTests.cs:470-476`.

**Batch 4: the small ones (M2-003, M1-015, M1-024, M1-025, M1-031).**
- M2-003: the constant 0x3F364D93 and the NaN path.
- M1-024: NeedsManager::Update's position in the engine tick (0x004ED640).
- M1-015 / M1-025: give the RemoveRobot upper-layer reset its own records, and build what exists.
- M1-031: queue the GoToSleep sequence (0x0052CE5A..0x0052CE6A) if the action exists; otherwise name it.

Then R-DEV's own M3/M1/M2 remainder (see `status/R-DEV.md`), including the libjpeg 9 grey path (M3-001) if time
allows. That port needs the emulator oracle in `re-analysis/tools/emu/`.

## Each batch

- `@cozmo-implementer` builds from the rows, with the checklist.
- `@cozmo-verifier` checks the diff against the checklist, item by item. Fix every blocking finding.
- `fidelity.py --check` and the full suite pass. Commit, `git pull --rebase`, push.
- **Records:** leave them IMPLEMENTATION_GAP. Set `unresolved` to "built, awaiting strong verification: <what was
  built> (<commit>)". Don't settle.

## Finish

Set `DONE <date time>` with the commits and the records built, or `BLOCKED <reason>`. A large scope is not a reason:
commit verified batches and carry on in the next round. The manager then verifies and settles.

**Write scope:**
- `cozmo-stack/src/Cozmo.Robot/**` (NvStorage, CozmoEngine, CozmoRobot, Motion, Cubes, Sensors, Camera), the matching
  tests, and `tests/Cozmo.Protocol.Tests/Fixtures/float_literal_baseline.txt` (removals only);
- the M1..M4 records and their inventories and `.approved.json`, and `re-analysis/FIDELITY_GAPS.md`;
- `re-analysis/research/*-B-CORE-*` and `re-analysis/jobs/status/B-CORE.md`.
