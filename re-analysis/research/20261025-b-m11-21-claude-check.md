# Claude check: B-M11-21 (VisionComponent+0xE8 after the handlers; type-10 border stamp bound)

- **Checked:** 04417900 (full suite 263f522f) against `jobs/B-M11-21.md`, row 68 and its check. The manager (Opus) read
  the code diff, the tests and the reader inventory in the status file.
- **Verdict: PASS.** No record settles. M11-035, M11-040 and M11-017 stay IMPLEMENTATION_GAP.

## Supported

- **Tail order** (`VisionSystem.cs`, the end of ProcessFrame):
  - `LastProcessedImageTimestamp = result.Timestamp` now runs in a `finally` after `FrameProcessed`;
  - so every handler, including the Freeplay overhead writer, sees the previous result's +0xE8, as 0x00654652 runs
    before 0x006549AA;
  - a throwing host subscriber still reaches the tail. That matches the native consumer continuing to its tail after a
    handler failure.
- **Synchronous writer:** the overhead writer runs synchronously inside the handler (`RunGated` under FrameGate), so it
  samples the pre-tail value.
- **Getter:** `ImageTimestamp` returns +0xE8 (0x00516EC0 → 0x006527A2), 0 before the first result, as in the native.
  The border call at 0x006803AA now runs. The old MISSING is gone.
- **+0xEC** is left out: no stack reader. The status says so.
- **Rollback (0x00652B36..0x00652B76):** not built, because the stack has no +0xE4 admission owner. It is now a visible
  MISSING in `OnFrame`. `ReportMissing` deduplicates (`SteppedBehavior.cs:134`), so it is reported once, not per frame.
  It doesn't substitute an unrelated clock.
- **Readers:** `PublicationImageTimestamp` and the external subscribers are listed with what they now see. Nothing else
  changed.
- **Tests:**
  - border = previous result and tail = current, production reach;
  - type-10 cells written on the first real capture with stamp 0, production reach;
  - a throwing handler still commits, unit reach.

  The old values are stated (0 border callbacks, the handler clock = current). The expected values come from row 68.

## Manager follow-through

The FakeImageProcessed decision from `20261025-b-m11-19-20-row-71-claude-check.md` is recorded in M11-035's
`unresolved` (not supported; no caller in the engine or app; reversible). `FIDELITY_GAPS.md` was regenerated and
`fidelity.py --check` passes.
