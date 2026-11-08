# Codex three-job capability batch: Claude check (manager, 2026-11-07)

**Verdict: all three PASS and merge (branch tip 9378a21b). No record settled, none added. VOICE-OUTPUT-1 and
FACE-IDENTITY-1 are partial by design; their MISSING items are in their handoffs and stay open.**

Codex gate: `scripts/test-full.sh` on bd4c818e, 7821/7821, no skips.

## VOICE-OUTPUT-1: PASS (partial)

- **Spot-checked at the cited addresses:**
  - WavePortal Execute 0x008DDC5A..0x008DDD20: with no source it returns 0x11. Otherwise it copies
    min(available, capacity) samples. A full buffer returns 0x2D; a short one is zero-padded by 0x008DDCD6, which sets
    the valid count to capacity, then returns 0x11. `WwiseWavePortalSource.Render` matches.
  - StartStream success 0x00A78B7C..0x00A78BA0 (ARM): copies source +0x44/+0x48 to PBI +0x158/+0x15C, the halfword
    +0x4C to +0x160 and the byte +0x4E to +0x162, as the handoff says.
- **Native oracle:** the shipped banks and native WavePortal render the Acapela fixture as 92 x 744 floats with the
  EndOfEvent tuple, hash matched by the independent Python probe. This is the oracle for the live sink job.
- **The two queued items from the five-job batch are done:**
  - the deadline is sampled every 64 blocks (fault latch still every block), Acapela 12.4 s to 10.5 s;
  - a failed SayText intent selection now skips under M1-034 (native reads an uninitialised style at 0x00561186).
- **Queued (not blocking):** `ModeForStream` special-cases the WavePortal plug-in ID to mode 2. In 0x009B9CD0..0x009B9DBC a
  source plug-in (low nibble 2) skips the codec mapping, so its mode bits come from the value at entry. Generalise from
  that when another source plug-in is needed; other mode-2 plug-ins are still refused, so nothing is approximated.
- **Still MISSING:** the live host sink and clock, the managed Switch/render/robot-buffer joins and public SayText. No
  audible speech yet.

## AI-BODY-CONTROL-1: PASS

- **Spot-checked:** 0x00527A74..0x00527A8C passes the message's queue position (`ldrb r1, [r4, #5]`) and zero retries
  (`movs r3, #0`) to the queue call. `QueueAiHeadAngleAsync` uses AtEnd with zero retries.
- **The head action is the existing engine action.** The AI goes through native admission (external-actions-disabled
  check, first full state), so reactions and the behaviour system keep priority. Cancellation is checked against the
  exact runner, so a reused tag cannot cancel someone else's action. `SetTag` now reserves atomically.
- **Queued (not blocking, older):** the existing public motion calls still report the legacy unset completion union.
  The AI path reports the native variant 6 (0x0053FF0E). Align them when the observation job exposes completions.

## FACE-IDENTITY-1: PASS (partial)

- **Spot-checked:**
  - FR album capacity 1000 users x 4 features (`mov.w r1, #0x3e8; movs r2, #4` at 0x00862A3C);
  - enrollment requires confidence > 500 (`cmp.w r0, #0x1f4; ble` at 0x0086E080).
- **Concurrency:** the FR worker waits on the detector's lock, so recognition is serialised with detection (recorded
  MISSING). Retirement waits for the worker outside the lock, so there is no deadlock on teardown.
- **Behaviour change:** with the stock recognizer, album export and save to the robot now report MISSING and write
  nothing. Before, the stack wrote back the last restored bytes. Nothing new can be enrolled yet, so nothing is lost;
  the restored album on the robot is untouched.
- **Still MISSING:** native extract cadence, positive-ID merge, explicit enrollment, named-only export. M14-008,
  M14-010, M14-011, M14-012 and M11-055 stay open.

## Next jobs (manager order)

Codex listed three in BLOCKED.md. Order: VOICE-LIVE-SINK-1 (Cozmo speaks aloud), then AI-BODY-OBSERVATION-1, then
FACE-ALBUM-EXPORT-1. The operator may reorder.

## Progress

22 records settled (unchanged), 2 of 178 finished (unchanged), 761 records (unchanged).
