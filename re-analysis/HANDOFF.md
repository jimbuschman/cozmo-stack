# Handoff — 2026-09-19 (after the Source Fidelity Sweep)

## Where things stand

| | |
| --- | --- |
| Previous clean HEAD | `aa8ff21` (M7 frozen; sweep set as the next task) |
| This session | **Source Fidelity Sweep complete** — see [SOURCE_FIDELITY_AUDIT.md](SOURCE_FIDELITY_AUDIT.md) |
| Tests | see the audit's §6 for the count at the end of the sweep; all passing offline |
| M9 | **NOT STARTED**, as instructed. Recommendation: resume after the two hardware retests below |

## Milestone status

| Milestone | Status |
| --- | --- |
| M1 transport core | Frozen. Audited; nothing changed. |
| M2 protocol catalogue | Frozen. `animHeadAngle` (0x93) and `animLiftHeight` (0x94) now carry engine-derived field names and are statically verified; regenerated through the pipeline, no hand edits. |
| M3 device layer | Frozen. **Erratum:** audio sample rate 22050 → 22320 Hz (engine `AnimConstants`). Provenance of the JPEG headers upgraded to native. |
| M4 control layer | Frozen. Head and lift limits confirmed native. Cube acceptance still not run. |
| M5 animation and expression | Frozen, **with errata awaiting hardware**: head/lift keyframes now streamed as animation keyframes with variability; audio alternative chosen by probability; angle interpolation and parameter clipping as the engine does them; resting face taken from the shipped `anim_neutral_eyes_01`. |
| M6 Wwise audio | Frozen. Audited; nothing changed except the resampling target (22320). |
| M7 reactive behaviour and idle | Frozen, **with errata awaiting hardware**: blink and eye dart rebuilt from the engine; the reaction map read from the shipped `reactionTrigger_behavior_map.json` and the behaviour classes; falling now reacts on landing with `ReactToImpact`. |
| M8 behaviour inventory and framework | Complete offline. Audited; nothing changed. |
| M9 Wwise switch-state audio | **NOT STARTED** |

## What the sweep found, in one paragraph

Nine confirmed divergences from the original, all in places where a plausible value had been chosen and
had passed hardware acceptance because acceptance judged the code against itself. Two were wrong facts
(the sample rate; the falling reaction's animation), three were invented mechanisms (the blink, the dart
geometry, the audio-alternative choice), two were invented wire paths (head and lift keyframes as motor
commands with made-up speeds), one was a guessed resting face, and one was a claim that a shipped artifact
did not exist. Every fix carries a regression that names the old behaviour. Thirteen areas remain
inferred and are listed with what would settle each; thirty are labelled local policy.

## Hardware still required

Two commands re-verify what changed. Until they pass, M5 and M7 are "frozen with errata", not
"hardware verified at HEAD":

```
dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <dir> --name anim_bored_01 --wwise <obb dir>
dotnet run --project src/Cozmo.Conformance -- behavior 172.31.1.1 --obb <dir> --seconds 60
```

Expected differences from the last accepted runs are spelled out in the audit's §8: head and lift moving
via `AnimHeadAngle`/`AnimLiftHeight` in the frame log; the sound alternative varying between runs; eyes
wider, shorter and closer together at rest; blinks as a quick squash; darts moving the whole face.

Deferred and unchanged: cube hardware acceptance, enhanced backpack-light keyframes, pre-rendered
`faceAnimations`, group cooldown enforcement (mechanism now recovered and recorded in
`AnimationLibrary.cs`), lift 0 mm semantics, the seven stereo ADPCM files, the music-hierarchy events.

## Open unknowns carried forward

From the audit's §2, the ones most worth a bounded look next:

1. **Eye-dart lifecycle** — ramp-then-hold or snap-then-hold; static reading of the persistent-layer
   replay is ambiguous. A stock-app face capture settles it.
2. **Scanline parity** — now known to be transmitted as the position of the set bit in each run's two
   draw bits; whether the firmware uses it is not established.
3. The renderer's corner-radius assignment and fill rule, unchanged from the last handoff.
