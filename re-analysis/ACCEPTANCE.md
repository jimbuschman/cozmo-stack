# Hardware acceptance record

Robot: Cozmo hardware 1.5, head serial `0x41d04d9d`, firmware **2457** (a 2025 Digital Dream Labs build).
All runs on 2026-09-18, from a Windows laptop on the robot's own access point, with no Android app, no
`libcozmoEngine.so` and no Python anywhere in the runtime path.

## How to read this

Each acceptance command splits its verdict in two. **Automated** is what the robot itself reported and the
tool checked. **Human** is what only a person in the room can judge: whether a picture looks like the room,
whether a tone sounds like a note, whether an LED lit. The tool never claims the second.

The per-run JSON records the commands now write are **not yet in the repository**: the runs below predate
that output, or were made on a machine whose files have not been copied across. The results are recorded
here as reported by the operator, which is weaker evidence than a committed artifact and is marked as such.

## M3 — device layer

| Capability | Automated | Human | Evidence |
| --- | --- | --- | --- |
| Camera | pass | **pass** — saved files are photographs from the robot's point of view | operator report; frames also replayed offline and Huffman-decoded in `DeviceTests` |
| Face display | pass | **pass** — the pattern on the face matched the printed art | operator report; codec verified offline against 28 Cozmo-produced byte sequences |
| Audio | pass | **pass** — a clean, unbroken tone | operator report; took five faults to reach, see `DEVICE_LAYER.md` |

## M4 — control layer

| Capability | Automated | Human | Evidence |
| --- | --- | --- | --- |
| Sensors and state | pass | pass | operator report |
| Lights | n/a — the robot reports nothing about its LEDs | **pass** | operator report |
| Head and lift motion | pass — the robot acknowledged each action by its id | pass | operator report |
| Wheel drive | pass — confirmed from the wheel speeds the robot reported | **pass** — moved forward and back correctly | operator report, run with `--allow-drive` |
| Stop-on-cliff | enabled before any wheel motion | n/a | operator report |
| Cubes | **not run** | **not run** | no cube was available |

### Cubes

Cube support is **code-complete and offline-tested, with hardware acceptance pending**. It is not failed and
not unimplemented. Discovery, connection state, tap, movement, up-axis and battery telemetry are all
implemented and covered by tests driven through the real message path. What is missing is only a cube to
point the robot at. Run `cubes 172.31.1.1 --acceptance` when one is to hand.

## M5 — animation and expression

| Capability | Automated | Human | Evidence |
| --- | --- | --- | --- |
| Animation playback | pass | **pass** — `anim_bored_01` played through, moving head, lift and body and changing the face | operator report, after the body-duration fix |
| Multi-clip indexing | pass | **pass** — `anim_bored_02` loaded and played from the same `.bin` | operator report |
| Procedural expressions | pass | **pass** — the expressions displayed correctly on the robot | operator report |
| Animation audio | **not run** | **not run** | implemented as a path; silent without a caller-supplied audio source |
| Arc body motion | **not run** | **not run** | implemented from the engine's own encoding; no shipped clip in this build uses an arc |

## Outstanding

Copy the JSON acceptance records from the machine the runs were made on, and commit them here. Until then
the table above rests on the operator's report rather than on a committed artifact.
