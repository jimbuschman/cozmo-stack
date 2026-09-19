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
| Arc body motion | pass | **pass** — a visible curved arc, then an equal arc back the other way | operator report, `anim --arc`, after the audio-pacing fix |

### Arc body motion

Verified on 2026-09-18 with `anim --arc`, after the fix in `DIAGNOSTIC_animation_start_sequence.md`. The
robot drove a visible curve and returned along the mirror arc, which is what the synthetic clip asks for.
The operator described the arc as small; the defaults are deliberately conservative (60 mm radius, 30 mm/s,
1 s per leg) and `--arc-radius`, `--arc-speed` and `--arc-seconds` widen it.

This is the check that proves the animation stream itself works end to end, not just the radius encoding:
before the fix the same command moved the robot not at all. Body motion inside an animation is therefore
hardware-verified for both straight and arc radii.

## Outstanding

**The face during `anim_bored_01` has not been re-checked since the fix.** It stopped displaying when
bracketing was added, and the same missing audio frames explain it, but only the arc half of that fix has
been confirmed on hardware. Running `anim --assets <dir> --name anim_bored_01` and watching the face
settles it.

Copy the JSON acceptance records from the machine the runs were made on, and commit them here. Until then
the table above rests on the operator's report rather than on a committed artifact.
