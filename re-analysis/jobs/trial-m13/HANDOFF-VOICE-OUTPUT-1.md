# VOICE-OUTPUT-1

## Source checks

- Wwise mode-2 factory 0x00A562B8 selects wrapper 0x00A78D10. StartStream
  looks up the bank SourceId in HIRC19 REG+0xDC (0x00A7898C–0x00A78A24).
  The descriptor's +0x10 plug-in ID selects the creator (0x00A78A58,
  0x00A78A68). The custom bank entry is created and retained at
  0x009B36A4–0x009B3764. Its parameter block is initialized at 0x009CE3B8,
  cloned at 0x009CF644, and passed to the plug-in at 0x00A78B50.
- WavePortal creator transfers the single pending slot at 0x008DD636.
  Init supplies the provider rate at 0x008DDB84 and Reset restarts the
  cursor. On successful StartStream, 0x00A78B7C–0x00A78BA0 copies rate,
  channel word 0x4101, format halfword 0x0120, and format byte low bits 5
  to the PBI. No source initializer for the copied upper bits was found.
- The dispatcher writes capacity at state+0xC (0x00A44794–0x00A447A4).
  0x00A78510 calls WavePortal Execute and copies Scratch08 to Code28.
  A full buffer, including exact end, returns 0x2D. A short buffer is
  zero-padded with valid count set to capacity and returns 0x11
  (0x008DDC5A–0x008DDD20). The zero-capacity branch writes only the result
  (0x00A78520–0x00A78530). The no-plug-in branch sets the buffer defaults
  at 0x00A785A4–0x00A785E0. Pitch intake starts at 0x00A4477C and reads
  0x00A52D4C, with tail at 0x00A52EBC.
- The checked English voice selectors use exact assignment then authored
  default (0x00A2C7E8–0x00A2C844). An exact empty assignment stays empty.
  External language and external name use global state. Voice processing
  uses scoped switch command 8 (0x0099FE50–0x0099FE58, 0x009AEFC4).
  The actual voice containers are noncontinuous. The managed graph's
  Switch creator, playback and scoped state joins remain open.
- Robot audio routes game object 7 to Robot_Bus_1, aux gain 1 and dry 0
  (0x0059962A–0x0059966A). Hijack publishes via 0x00596148→0x005985FC.
  Animation readiness and abort are owned by 0x00597C9E, 0x00597D82,
  0x00597DB4 and 0x0059678E.

## Native feasibility probe

The independent probe used the real memory-bank load at 0x009A35F8, native
cooperative bank drain 0x00A35234, a dummy output selected after an OpenSL
resource error, and no guest audio thread. It loaded the unchanged Init
and English Cozmo banks, handed a test signal to native WavePortal,
posted Sentence on object 7, and observed 31 Hijack callbacks of 744
floats. The probe clock and test signal are controlled inputs. This
result alone does not establish live robot pacing, action lifetime,
or equivalence of host audio services.

Reader requests use gpt-6.1-sol medium. Builder and reviewer use their configured
default. Actual backends unobserved. 

## Build and verification

- WwiseVoiceSources.cs, WwisePlaybackBridge.cs, WwiseRuntimeGraphLoader.cs
  and WwiseWavePortalSource.cs connect the validated descriptor to the
  default mode-2 factory, PBI format, and frame intake. WwiseWavePortalSourceTests.cs
  reaches that factory with an actual shipped HIRC excerpt and tests failure
  before ownership, zero capacity, exact end, and short frames. Uninitialized
  source byte high bits have a zero host representation, with no reader
  established in the managed graph.
- ArmMachine.cs and UnicornNative.cs support bounded cooperative guest-worker
  calls with a full CPU context save and restore. EmulationSafetyTests.cs
  exercises D8 and FPSCR restoration, including the failed-worker path.
- NativeVoiceOracle.cs and NativeVoiceOracleTests.cs live in the test project.
  The managed harness loads the unmodified local banks with the real native
  bank pump. It passes the 70,992-sample Acapela fixture through native
  WavePortal, sets event_volume 0xD2687048 through 0x0099FB14, and verifies
  92 × 744 float samples and the EndOfEvent tuple for object 7/event
  0xBF0BACE1. The independent Python run produced the same SHA-256
  a0a2b7665c88de12899af203a4c754d9e3b7c619483995ea871e0600c4c60533.
  Native callback entry 0x008DC040 carries the FX pointer, samples pointer,
  and count. The host copies before native frame storage is reused.
- The affected bridge/graph/emulator run passed 63/63 before the native
  oracle addition. The final native/source/emulator tests passed 27/27.

## MISSING

The native engine's normal output forces kind 2 and device ID 0
(0x009EBCD0–0x009EBCF4), selecting OpenSL through 0x00A1CD24. The test
harness supplies the resource error so the engine selects its dummy device.
This device periodically rebuilds its output path (0x009EBE94–0x009EBEC8,
0x009EC0C8). The native test's controlled clock is therefore evidence for
this oracle only. A stable host sink, live Wwise and animation clock
ownership, native shutdown, and robot buffer and wire pacing remain open.
The managed live graph's Switch, render, and robot-buffer joins also remain
open. SayText remains gated, including fitted/canned animation and public
admission. No production speech output, hardware or fidelity status is claimed.

Independent review passed after corrections to native buffer writes and worker context restoration. The manager reran the independent Python probe on the Acapela fixture and matched the 92-frame hash and EndOfEvent tuple. First and last frame ticks were 106670 and 3349438 microseconds, and EndOfEvent was 3328104 microseconds on the probe clock.

## Claude follow-up on main 7b6390de

Unicorn 2.1.4 native timeout starts and joins a thread per timed call, and
nested starts share stop and timeout state (uc.c 958–985, 1128–1186).
The vendored Windows DLL measured about 16 ms even for a one-instruction
call. The existing nested-safe block hook now samples the clock every 64
translated blocks, checks the fault latch on every block, and checks time
at call and managed-callback boundaries. Comparable Acapela runs changed
from 12.33–12.59 s to 10.35–10.65 s, with no clear OKAO improvement.
This is host safety and performance work, not real-time timing evidence.

On a missing intent, native 0x0052AB54 allocates without zeroing, the ctor
logs at 0x005610A4, and it reads the uninitialized style at 0x00561186.
The stack skips the request before construction and synthesis, logs M1-034
and M14-006, and leaves the RNG alone. The regression reaches the real
internal admission and verifies no action is queued.

### Proposed M14-006 split for Claude

- SayText action, metadata and traits (0x005602D8, 0x00560F94, 0x00561488).
- Synthesis provider, operation lifecycle and owner teardown
  (0x0069E4C8, 0x0069EF10, 0x0069F430, and the M14-013 boundary).
- Java speech input policy, including the stop/callback and locale inputs.
- WavePortal descriptor, source transfer and refill (0x00A7898C,
  0x008DD636, 0x008DDC5A).
- Live speech rendering, timing and delivery remain implementation work,
  with the M6-016/M6-018 boundaries recorded above.

No manifest or approval edits were made. Batch full gate passed at bd4c818e with 7821/7821 tests and zero skips. READY FOR CLAUDE.
The broader affected group passed 45/45 and the clean rebuild of the safety and voice group passed 38/38 including the final address-hook test. Both emulator fixtures remained bit-exact. Independent follow-up review found no blocking change.
