# Codex five-job batch: Claude check (manager, 2026-11-07)

**Verdict: all five PASS and merge (branch tip e34a3a09). No record settled. One record added: M4-053 COMPATIBILITY_POLICY
(cube reconnect). Three jobs are partial by design; their MISSING items are in their handoffs and stay open.**

Codex gate: `scripts/test-full.sh` on 601b4475 (rebased onto 1771234b), 7803/7803. The earlier 7801/7803 failure was the
host-libm fixture of NativeSolvePnPOracleTests, fixed on main in 1771234b (the fixture now replays the oracle's libm results).

## EMU-SAFETY-1: PASS

- **bionic sscanf:** checked against `libc/stdio/vfscanf.cpp` at the cited revision 74866e16. The input-failure tail is
  `if (nassigned == 0) nassigned = -1` (lines 725-729). Width and `*` are accepted in either order (the `again:` switch). The
  literal and conversion input checks go to input_failure on an empty stream (129, 305, 317). `%c` reads short (405: `r == 0`
  only fails). ScanfSubset matches each of these.
- **bionic atoi:** `(int)strtol` on ARM32 saturates and sets ERANGE. Built so.
- **Watchdog, fault latch, finalizer:** host containment under M1-034. Correct: nested calls share the deadline, a fault
  makes later calls fail clearly, and Dispose refuses to run mid-call.
- **Queued (performance, not blocking):** the deadline is checked by a UC_HOOK_BLOCK managed callback on every basic block.
  Measured on this PC: OKAO went from 21 to 30 ms per frame (NativeOkaoOracleTests), and Acapela synthesis of the test
  phrase from 10 to 15 s. Unicorn's own `uc_emu_start` timeout argument (microseconds) or a cheaper check would remove it.

## CUBE-RECONNECT-POLICY-1: PASS; M4-053 added

- The hook is in CheckDisconnectedObjects right after the native reset. It passes ConnectToObjects the slots' current ids,
  with the expired id in its own slot, so per 0x00517404 only that slot gets a request. Other pending requests are untouched.
  Then the engine's own path runs: the request waits for an advertisement (`_available`, 0x00514B00) and the matching
  reply (0x005179C0). A connected cube is removed from `_available` (0x00517A98), so a fresh advertisement is required.
- **Recorded as M4-053**, COMPATIBILITY_POLICY: the operator decided on 2026-11-04 and the route was chosen 2026-11-07. It is
  on by default; `ReconnectPooledCubes=false` gives engine-only behaviour. Tagged in CubeConnections.cs; M4-control inventory
  row added and re-approved under the standing authorisation for cited inventory corrections.
- **Open, hardware:** in robot run 4 the cube came back with a connected report and no new advertisement. This route does not
  recover that case. A robot run will show whether a dropped cube advertises again.

## COZMO-VOICE-1a: PASS (partial)

- **Spot-checked at the cited addresses:**
  - the speech rate is converted with `vcvt.s32.f32` at 0x006A0442, so NaN becomes 0 on ARM;
  - style 2 takes the question-mark branch (`cmp r2, #2` at 0x0069EF1C, then the string append at 0x0069EF2A).
- **Existing audio is unchanged:** only plug-in 0x006412C2 (WavePortal) becomes a WwisePlaySourcePlugin node. Other source
  plug-ins keep the old problem path. The speech provider loads on first use, not at robot start.
- **No production effect yet:** the action stops at the output join with a visible MISSING, and no audio sample is sent.
- **Queued (not blocking, internal path only):** in `QueueSayTextIntentForNativePath`, a failed `TrySelect` logs and then still
  queues the action with default selection values. Check what the engine does there before the public command uses it.
- **M14-006 split:** proposed by Codex and not done here. It goes with the job that builds the output join (VOICE-OUTPUT-1).

## COZMO-VOICE-1b: PASS (partial)

- The Harmonizer and Expander run as the shipped code in the embedded runtime, against an emulator oracle
  (`emu_voice_fx.py`). No production caller yet; the production voice graph is VOICE-OUTPUT-1.

## FACE-WIRE-1: PASS (partial)

- **The multi-face shuffle,** checked instruction by instruction at 0x0086D8E6..0x0086D912:
  - it is skipped below two faces;
  - it makes count-1 draws of RandInt(remaining) and swaps the front element with front+j.
  `OkaoFaceDetection.ShuffleIndices` is the same.
- **The FaceWorld thresholds** 0x3F060A92 and 0x3E32B8C2 are 30 degrees and 10 degrees in radians, as the handoff says.
- **Live effect:** with the shipped libraries present, anonymous faces are now detected on face-mode frames. They are used
  for metering and the world path. Recognition, enrollment and the full engine-to-game face payload are still MISSING.
  M14-010 and M14-011 stay IMPLEMENTATION_GAP.
- The robot machine needs `resources/lib/armeabi-v7a` for this. Without it the stock detector reports unavailable, as before.

## Progress

22 records settled (unchanged), 2 of 178 finished (unchanged), 761 records (M4-053 added).
