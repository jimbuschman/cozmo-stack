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
| Cubes | discovery **observed**; acceptance **not run** | **observed** — a real cube appeared during discovery after being tapped (2026-09-19) | operator report; `cubes --acceptance` not yet run |

### Cubes

Cube support is **code-complete and offline-tested, with hardware acceptance pending**. It is not failed and
not unimplemented. Discovery, connection state, tap, movement, up-axis and battery telemetry are all
implemented and covered by tests driven through the real message path.

On 2026-09-19 **hardware discovery was observed**: a real cube appeared during discovery after being tapped.
That is the first hardware evidence for the cube path and no more than that. Connection state, tap, movement,
up-axis and battery telemetry have not been exercised on a robot, and `cubes 172.31.1.1 --acceptance` has not
been run; full cube telemetry acceptance stays pending.

## M5 — animation and expression

| Capability | Automated | Human | Evidence |
| --- | --- | --- | --- |
| Animation playback | pass | **pass** — `anim_bored_01` played through, moving head, lift and body and changing the face; re-confirmed after the audio-pacing fix | operator report |
| Face during animation | pass | **pass** — displays correctly, and the animation reads right overall | operator report, after the audio-pacing fix |
| Multi-clip indexing | pass | **pass** — `anim_bored_02` loaded and played from the same `.bin` | operator report |
| Procedural expressions | pass | **pass** — the expressions displayed correctly on the robot | operator report |
| Procedural face renderer, reconstructed | pass — 392 tests | **pass** — `anim_reacttocliff_pickup_01` holds through the extreme squash/stretch with the eyes distinct, and the resting face reads correctly | operator report 2026-09-19, commit `bf2ddc2` |
| Animation audio, caller-supplied | **not run** | **not run** | the `WavAudioSource` path; superseded in practice by the shipped library below |
| Arc body motion | pass | **pass** — a visible curved arc, then an equal arc back the other way | operator report, `anim --arc`, after the audio-pacing fix |

### Arc body motion

Verified on 2026-09-18 with `anim --arc`, after the fix in `DIAGNOSTIC_animation_start_sequence.md`. The
robot drove a visible curve and returned along the mirror arc, which is what the synthetic clip asks for.
The operator described the arc as small; the defaults are deliberately conservative (60 mm radius, 30 mm/s,
1 s per leg) and `--arc-radius`, `--arc-speed` and `--arc-seconds` widen it.

This is the check that proves the animation stream itself works end to end, not just the radius encoding:
before the fix the same command moved the robot not at all. Body motion inside an animation is therefore
hardware-verified for both straight and arc radii.

### The face regression is resolved

`anim_bored_01` was re-run after the streamed-silence fix: the face displays correctly again and the
animation reads right overall. The regression introduced by animation bracketing is closed, and both halves
of that fix — body motion and face — are now confirmed on hardware.

That also settles the one claim in `DIAGNOSTIC_animation_start_sequence.md` the engine could not answer by
itself, namely whether the robot holds `animFaceImage` against the animation clock once an animation is
open. The face returning the moment silence frames started flowing, with nothing else changed, says it does.

### The procedural-face erratum is closed

M5 was frozen on 2026-09-18 with one erratum against it: the procedural face **renderer** — not the
parameter model, not the wire codec, not the timeline — was our own construction, and hardware testing of
`anim_reacttocliff_pickup_01` showed it was materially wrong. Two rounds of work were needed. The first
corrected the *mechanism*, applying the whole-face transform over the image as the engine does. The second,
after the same clip still looked wrong on the robot, established that the *geometry* was invented as well:
the eye size, both eye positions and the canvas height were all chosen rather than recovered.

The renderer is now a port of `ProceduralFaceDrawer`, reconstructed from the binary and written up in
[PROCEDURAL_FACE.md](PROCEDURAL_FACE.md). Retested on hardware on 2026-09-19 with the same clip: the eyes
stay distinct through the extreme squash/stretch, and the resting face is correct.

**The erratum is closed.** Three low-level details inside the renderer remain unresolved and are named as
such rather than guessed — the corner-radius parameter assignment, the polygon fill rule and the scanline
parity. None of them is a defect, and the handoff carries them forward.

Nothing in the M5 wire format or timing was reopened by any of this. The face codec stays verified against
the 28 Cozmo-produced byte sequences it was verified against in M3.

**M5 is COMPLETE and FROZEN.** Frozen 2026-09-18; procedural-face erratum closed 2026-09-19.

## M6 — Cozmo's original sound assets

| Capability | Automated | Human | Evidence |
| --- | --- | --- | --- |
| Event resolution through the Wwise banks | pass | n/a | 615 of the 705 events that should play resolve to media; offline, whole-library |
| Wwise Vorbis decoding | pass — 2019 of 2019 rebuilt and decoded | n/a | `wwise --validate`, whole library, all five codebook sets, zero failures |
| ADPCM decoding | pass — 220 of 227 | n/a | same run; the seven refused are stereo |
| Cozmo's own sounds in an animation | pass | **pass** — the original shipped sound played automatically, with no manual WAV mapping | operator report, `anim ... --name anim_bored_01 --wwise <obb dir>` |

### The acceptance run

`anim_bored_01` played the animation, displayed the face correctly, and played its original shipped Cozmo
sound with **no `--audio` mapping supplied**. That is the M6 acceptance target met: the sound came from the
OBB's own Wwise banks and `.wem` files, resolved and decoded by this stack.

**M6 is COMPLETE and FROZEN as of 2026-09-18.**

## M7 — reactive behaviour and idle personality

| Capability | Automated | Human | Evidence |
| --- | --- | --- | --- |
| Idle face: darts and blinks | pass | **pass** — the resting face is correct and stays correct | operator report 2026-09-19 |
| Idle does not accumulate | pass — hundreds of darts, centres and scales bounded | **pass** — no drift or growth observed on the robot | `IdleFaceTests`, plus operator report |
| Reactive dispatch and arbitration | pass | pass | operator report |
| Behaviour execution and hardening fixes | pass — 392 tests | pass | operator report |

### What it took to get here

M7's first hardware run found the eyes growing and merging over time. That turned out to be **two
independent faults that presented as one symptom**, and separating them took two more hardware rounds:

1. **In M7.** `IdleBehavior` read the live face, offset it, and wrote it back, so every dart compounded the
   last. Fixed with an untouched base pose plus time-limited transients.
2. **In M5.** The procedural face renderer's geometry was invented. Fixed by reconstructing
   `ProceduralFaceDrawer` from the binary.

The M7 write-up originally recorded that "nothing in this failure implicates the renderer". That was wrong,
and is corrected in `BEHAVIOR_LAYER.md` rather than quietly edited away: the idle fault was real and was in
M7, but it was not the only fault, and the renderer was also at fault.

**M7 is COMPLETE, HARDWARE VERIFIED and FROZEN as of 2026-09-19.**

## Outstanding

Copy the JSON acceptance records from the machine the runs were made on, and commit them here. Until then
the table above rests on the operator's report rather than on a committed artifact.

One acceptance item remains unrun: **M4 cubes**. Discovery has been observed on hardware (above); the
`--acceptance` run covering the rest of the cube telemetry has not been made. It is deferred, not failed — see
the deferred list in `ANIMATION_LAYER.md`.

## Source Fidelity Sweep, 2026-09-19 - retests passed

The sweep ([SOURCE_FIDELITY_AUDIT.md](SOURCE_FIDELITY_AUDIT.md)) changed what the robot receives in two
frozen milestones. Both retests were run on 2026-09-19 and **passed visually**:

| milestone | what changed on the wire or the display | retest | result |
| --- | --- | --- | --- |
| M5 | head and lift keyframes as `animHeadAngle`/`animLiftHeight` with variability; audio at 22320 Hz; audio alternative chosen by probability; angle interpolation and parameter clipping; resting face from `anim_neutral_eyes_01` | `anim 172.31.1.1 --assets <dir> --name anim_bored_01 --wwise <obb dir>` | **pass** — operator report, visual |
| M7 | blink as the engine's seven-frame squash; dart moving the whole face with the engine's eye shaping; falling reacts on landing with `ReactToImpact` | `behavior 172.31.1.1 --obb <dir> --seconds 60` | **pass** — operator report, visual |

M5 and M7 are hardware re-verified at the sweep's HEAD; the "frozen with errata" status is cleared. As with
the earlier rows, the evidence is operator report and no JSON record is committed. Nothing in M1-M4 or M6
changed on the wire (M3's sample-rate change alters tone pitch by 1.2 % and the resampling of shipped
sounds; the M5 `anim_bored_01` retest covers it).

The reconciliation that followed (audit §10) changed the animation scheduler's behaviour under stalls and
late ticks and the lift-height readout. Neither alters what a normally paced animation sends; both are
offline-verified only.

## M9 — Cozmo sings (2026-09-19): offline complete, hardware pending

| Capability | Automated | Human | Evidence |
| --- | --- | --- | --- |
| Music hierarchy read | pass — 3490 of 3490 objects consume exactly | n/a | `WwiseMusicTests`, `wwise --hierarchy` |
| 39 Singing behaviours resolve to their songs | pass | n/a | `EverySingingBehaviourResolvesToOneMidiSegment` |
| 39 songs render to sound | pass — every note sung, no clipping after the output stage, deterministic | **not run** — a person can listen to the `--render` WAV on a PC; nobody has | `EveryShippedSongRendersCompletely`, `wwise --validate-music` (82 of 83 music events render; the one is the app soundtrack's silent default) |
| Singing behaviour sequence | pass — switch first, reactions held, get-in / tempo / get-out | **not run** | `WwiseSongTests` |
| **Cozmo sings on the robot** | **not run** | **not run** | `sing 172.31.1.1 --obb <dir> --behavior Singing_AbaDaba --acceptance`; `HARDWARE_TEST_PLAN.md` item A |

**M9 is COMPLETE OFFLINE and NOT hardware verified.** The rendering rules are Wwise runtime semantics
taken from public documentation, tabulated with their provenance class in `WWISE_MUSIC.md` §3; the
engine's behaviour logic is read from the binary. The robot run is the one check that needs hardware and
it is not a gate for M10.

## M10 — derived robot state and reactions (2026-09-19): offline complete, hardware pending

| Capability | Automated | Human | Evidence |
| --- | --- | --- | --- |
| Off-treads classifier | pass — every branch and constant pinned; the three committed fw2457 captures replay with the classifier enabled, all states classified, 0 transitions | **not run** | `DerivedStateTests` (classifier section), `TheCapturedRobotStaysOnTreadsThroughTheWholeLog`, `offtreads --replay` |
| Unexpected-movement detector | pass — turn-that-does-not-turn at 11 states, spun-against-command at 6, resets and decay | **not run** | `DerivedStateTests` (detector section) |
| Reaction strategies | pass — state callbacks, latches, shaken 16000, slope timing, frustration cooldown | n/a | `DerivedStateTests` (strategies) |
| Nine reaction behaviours | pass — transitions, waits, recalibration, the shaken machine, on the shipped assets | **not run** | `DerivedStateTests` (behaviours), `EveryTriggerTheDerivedStateReactionsPlayResolves` |
| Reaction dispatch (manager) | pass — switch, resume-last, disabled trigger, reaction lock | n/a | `DerivedStateTests` (manager) |
| Whole-config | pass — 12 registrations match `reactionTrigger_behavior_map.json`; 15 PlayAnim configs load, resolve, and match the inventory; inventory regenerated (67 of 178) | n/a | `EveryRegisteredReactionMatchesTheShippedMap`, `EveryShippedPlayAnimConfigLoadsAndResolves`, `behavior_inventory.py --check` |
| Cube-moved path | pass against a fake locator — cannot fire without one, fires on a located cube moved > 1 s out of view, sense → turn → presence/absence | n/a until vision | `DerivedStateTests` (cube path) |
| **Transitions and reactions on the robot** | **not run** | **not run** | `offtreads` / `reactions … --acceptance`; `HARDWARE_TEST_PLAN.md` items G–J |

**M10 is COMPLETE OFFLINE and NOT hardware verified.** Every threshold is read from the binary
(`DERIVED_STATE.md`); the labelled non-native items are in its §5. The robot runs are the checks that need
hardware and none gates M11.
