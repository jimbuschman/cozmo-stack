# Handoff — 2026-09-19 (M9 complete offline)

## Where things stand

| | |
| --- | --- |
| Latest commits | M9 increment 1: `bfe2ed7`, `2e8e243`; increment 2: this commit |
| M9 | **COMPLETE OFFLINE.** The 39 Singing behaviours resolve, render and run; hardware acceptance pending — `HARDWARE_TEST_PLAN.md` item A. See [WWISE_MUSIC.md](WWISE_MUSIC.md) |
| Tests | **480 passed, 0 failed (`dotnet test Cozmo.sln`, 3 m 41 s; 462 after M9 increment 1)**, all passing offline |
| Hardware | nothing new has been run since the two post-sweep retests; the consolidated plan is [HARDWARE_TEST_PLAN.md](HARDWARE_TEST_PLAN.md) |
| Next | **M10 may begin now**; it does not depend on the pending hardware results (see "Next task") |

## Milestone status

| Milestone | Status |
| --- | --- |
| M1 transport core | Frozen. |
| M2 protocol catalogue | Frozen. `RobotState` helpers read `liftAngle` as radians (D10). |
| M3 device layer | Frozen. 22320 Hz; JPEG headers native. Colour camera frames never exercised on hardware (plan item E). |
| M4 control layer | Frozen. Cubes: discovery hardware-observed; telemetry acceptance pending (plan item B). |
| M5 animation and expression | Frozen, hardware re-verified 2026-09-19. Scheduler timeline frame-counted (D11), single-clip refusal (D12): offline-verified, normal case covered by plan item F. |
| M6 Wwise audio | Frozen. Two M9 corrections recorded in its errata (switch-container parent; source-plug-in sounds; 584 decodable events, not 615). |
| M7 reactive behaviour and idle | Frozen, hardware re-verified 2026-09-19. Falling → impact never exercised (plan item C). |
| M8 behaviour inventory and framework | Complete offline. Inventory regenerated after M9: **44 of 178 behaviours implementable** (5 from M1–M7, 39 Singing from M9). |
| M9 Wwise switch-state audio | **Complete offline; hardware pending.** Hierarchy reader (3490 of 3490 objects exact), music resolver, MIDI reader, per-note vocal sampler, `IAudioSwitchStates` seam, `SingingBehavior`, `sing` acceptance command, `wwise --render / --validate-music`. |

## What M9 established

The engine's `BehaviorSinging` posts the behaviour's switch and plays get-in, tempo and get-out
animations; the tempo animation's audio event targets a music switch container keyed by the Cozmo_Sings
switch ids; each leaf is one segment holding one MIDI clip (9600 ticks per beat, tempo from the nearest
meter override); the notes go to a blend container of Cozmo's own per-note recordings. All of that is
read from the engine, the enums and the banks. What is **applied** to it — how a note plays through the
sampler, and an output stage standing in for the robot bus limiter — is Wwise runtime behaviour taken
from public documentation or our own labelled policy, tabulated in `WWISE_MUSIC.md` §3. The vibrato LFO
and note-off envelope are read but not interpreted (deferred).

## Hardware

Consolidated in [HARDWARE_TEST_PLAN.md](HARDWARE_TEST_PLAN.md): A/A2 Cozmo sings (M9), B cube telemetry
(M4), C falling → impact (M7), D lift readout (M4), E colour camera (M3), F animation timeline (M5).
Commit the acceptance JSON files when run.

Deferred and unchanged: enhanced backpack-light keyframes, pre-rendered `faceAnimations`, group cooldown
enforcement (mechanism recorded in `AnimationLibrary.cs`), lift 0 mm semantics, the seven stereo ADPCM
files, the app's soundtrack (`Play__Music__Play`), Code Lab.

## Open unknowns carried forward

1. **Eye-dart lifecycle** (M7) — ramp-then-hold or snap-then-hold; a stock-app face capture settles it.
2. **Scanline parity** (M5) — transmitted; whether the firmware uses it is not established.
3. Renderer corner-radius assignment and fill rule (M5).
4. **Frames per engine update** (M5) — the engine streams to the audio budget; we stream one frame per tick.
5. **Sampler semantics** (M9) — sustain/release, the get-in branch, note tracking: a stock-app recording of
   one song beside our `--render` WAV would settle them.
6. **Modulators** (M9) — LFO and envelope objects (types 21, 22): field semantics unread.

## Next task

**M10: derived robot state and the cube reactions**, chosen from the regenerated inventory
(`NEXT_MILESTONE.md`), and not gated on any pending hardware result:

* **11 behaviours** need "robot state not yet derived": `ReactToRobotOnBack`, `OnFace`, `OnSide`,
  `PlacedOnSlope`, `ReturnedToTreads`, `RobotShaken`, `UnexpectedMovement` and friends. The engine derives
  these in `Robot::UpdateFullRobotState` and the `OffTreadsState` classifier from the IMU the robot already
  streams (M4 reports it raw). Read the classifier from the binary (thresholds, debounce) and reproduce it;
  the committed fw2457 capture and the fixtures give offline IMU data to test against.
* The **cube reactions** (`ReactToCubeMoved`, `AcknowledgeObject`, tap-driven behaviours) run on the cube
  message path M4 already implements and discovery has been observed on hardware; their engine classes are
  exported and can be read the same way `BehaviorReactToX` were. Cube *acceptance* (plan item B) is
  needed to freeze them, not to build them.

Start from the engine, not from guesses: disassemble first (`re-analysis/tools/disarm.py`; the session
scratch disassembler was `engdis.py`, described in the memory notes), and read `BEHAVIOR_LAYER.md` and
`SOURCE_FIDELITY_AUDIT.md` §2 before writing code.
