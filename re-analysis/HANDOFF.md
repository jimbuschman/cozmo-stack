# Handoff — 2026-09-19 (after the Source Fidelity Sweep, its hardware retests and the reconciliation)

## Where things stand

| | |
| --- | --- |
| Sweep commits | `dbdfa29`..`d483fd2` (five commits, pushed) — see [SOURCE_FIDELITY_AUDIT.md](SOURCE_FIDELITY_AUDIT.md) |
| Hardware retests | **both passed visually on 2026-09-19** (audit §8); M5 and M7 re-verified, errata cleared |
| Reconciliation | this commit — audit §10: counts and wording corrected, retests and cube observation recorded, D10–D12 fixed |
| Tests | **444 passed, 0 failed (`dotnet test Cozmo.sln`, 3 m 36 s; 440 after the sweep)**, all passing offline |
| M9 | **NOT STARTED**, as instructed. The gate has been passed: **M9 may begin** |

## Milestone status

| Milestone | Status |
| --- | --- |
| M1 transport core | Frozen. Audited; nothing changed. |
| M2 protocol catalogue | Frozen. `animHeadAngle` (0x93) and `animLiftHeight` (0x94) carry engine-derived field names and are statically verified. The hand-written `RobotState` helpers now read `liftAngle` as radians and convert to millimetres with the engine's `45 + 66 sin(angle)` (D10). |
| M3 device layer | Frozen. Audio sample rate 22320 Hz (engine `AnimConstants`); JPEG headers native. |
| M4 control layer | Frozen. Head and lift limits native. `Sensors.LiftPositionRaw` became `LiftAngleRad` + `LiftHeightMm`. **Cubes:** hardware discovery observed on 2026-09-19 (a real cube appeared during discovery after being tapped); connection, tap, movement, up-axis and battery telemetry acceptance still pending. |
| M5 animation and expression | **Frozen, hardware re-verified 2026-09-19** (`anim_bored_01 --wwise`, visual). Reconciliation: the scheduler's timeline is now a count of streamed frames × 33 ms as in the engine, frozen while the robot has no room and caught up frame by frame (D11); a second clip is refused while anything streams unless it interrupts, as `SetStreamingAnimation` does (D12). Both offline-verified; neither changes a normally paced stream. |
| M6 Wwise audio | Frozen. Resampling target 22320 Hz; covered by the M5 retest. |
| M7 reactive behaviour and idle | **Frozen, hardware re-verified 2026-09-19** (`behavior --seconds 60`, visual): shipped resting face, engine blink table, `LookAt` dart geometry, `ReactToImpact` on landing. |
| M8 behaviour inventory and framework | Complete offline. Audited; nothing changed. |
| M9 Wwise switch-state audio | **NOT STARTED — cleared to start** |

## What the sweep and reconciliation found, in one paragraph

Nine confirmed divergences in the sweep (audit §1) and three in the reconciliation (audit §10), all in places
where a plausible value or mechanism had been chosen and had passed acceptance because acceptance judged
the code against itself: the sample rate, the falling reaction's animation, the blink, the dart geometry,
the audio-alternative choice, head and lift keyframes as motor commands, the resting face, a claim that a
shipped artifact did not exist; then the lift unit, a wall-clock animation timeline that jumped after
stalls, and a second-clip rule the engine does not have. Every fix carries a regression that names the old
behaviour. The INFERRED rows of the audit's §5 remain the open list; §2 says what would settle each.

## Hardware

Both post-sweep retests passed visually (operator report; no JSON record committed):

```
dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <dir> --name anim_bored_01 --wwise <obb dir>
dotnet run --project src/Cozmo.Conformance -- behavior 172.31.1.1 --obb <dir> --seconds 60
```

Not yet on hardware: the reconciliation's scheduler changes (D11, D12: only visible under a stall or a late
tick), the lift-height readout (D10: `control 172.31.1.1` now prints `lift=<rad>rad/<mm>mm`; with the lift
down expect about −0.198 rad / 32 mm), the falling → impact reaction (D9), and `cubes --acceptance`.

Deferred and unchanged: full cube telemetry acceptance, enhanced backpack-light keyframes, pre-rendered
`faceAnimations`, group cooldown enforcement (mechanism recorded in `AnimationLibrary.cs`), lift 0 mm
semantics, the seven stereo ADPCM files, the music-hierarchy events.

## Open unknowns carried forward

From the audit's §2, the ones most worth a bounded look next:

1. **Eye-dart lifecycle** — ramp-then-hold or snap-then-hold; static reading of the persistent-layer
   replay is ambiguous. A stock-app face capture settles it.
2. **Scanline parity** — transmitted as the position of the set bit in each run's two draw bits; whether
   the firmware uses it is not established.
3. The renderer's corner-radius assignment and fill rule, unchanged.
4. **Frames per engine update** — the engine streams to the audio budget on every update regardless of the
   clock; this stack streams one frame per 33 ms tick and makes up late ticks. Recorded as local policy; a
   wire capture of the stock app's burst pattern would show whether it matters on the robot.

## Next task

Begin **M9** (Wwise switch-state audio) on top of `AnimationScheduler.StartAudio` and the M6 bank reader.
Read `ANIMATION_LAYER.md` §Wwise and the audit's §2 rows on Wwise container semantics and keyframe volume
first; both are M9 territory.
