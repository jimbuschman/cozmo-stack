# Consolidated hardware test plan — pending checks as of M9 (2026-09-19)

Everything below is offline-verified in the repository and waits only for a robot. Run in the order given
(each item is independent; the order puts the cheapest and most informative first). Every command writes
an acceptance JSON when `--acceptance` is given: **commit those files** (`re-analysis/acceptance/`), which
is the process change the fidelity audit asked for.

Setup for all items: robot on its own access point at 172.31.1.1, the OBB unpacked at `<obb>` (the sound
directory needs only `cozmo_resources/sound/AudioAssets.zip`), commands run from `cozmo-stack/`.

| # | check | milestone | command | automated verdict | human verdict |
| --- | --- | --- | --- | --- | --- |
| A | **Cozmo sings** | M9 | `dotnet run --project src/Cozmo.Conformance -- sing 172.31.1.1 --obb <obb> --behavior Singing_AbaDaba --acceptance` | switch posted; get-in, song and get-out animations completed; the song rendered with N notes | he sang a tune for about 12 s between a get-in and a get-out, in his own voice, one note per note, at a sensible level; no bursts of get-in phrases during the song |
| A2 | one song from each tempo group | M9 | as A with `--behavior Singing_Bingo` (100 bpm, cut at 9.8 s by the clip's Stop event) and `--behavior Singing_TwinkleTwinkle` (120 bpm) | as A | as A; the three tempos audibly differ |
| B | **cube telemetry** | M4 | `dotnet run --project src/Cozmo.Conformance -- cubes 172.31.1.1 --acceptance` (tap, roll and lift a cube during the 15 s) | connection state, tap, movement, up-axis and battery reported for the cube | the events match what you did to the cube. Discovery is already hardware-observed (2026-09-19); this completes it |
| C | falling → impact | M7 (D9) | `dotnet run --project src/Cozmo.Conformance -- behavior 172.31.1.1 --obb <obb> --seconds 60`, then drop the robot a few centimetres onto a soft surface | an `Unresolved … waits for the landing` line on the fall and a `ReactToImpact` decision on landing when the impact exceeds 1000 | he reacts on landing, not while falling |
| D | lift readout in radians | M4 (D10) | `dotnet run --project src/Cozmo.Conformance -- sensors 172.31.1.1 --acceptance` with the lift down, then raised by hand | `lift=` prints about −0.198 rad / 32 mm with the lift down and about 0.712 rad / 92 mm raised | the printed millimetres match where the lift is |
| E | colour camera frames | M3 | `dotnet run --project src/Cozmo.Conformance -- camera 172.31.1.1 --color --acceptance` | frames decode | the saved images are colour photographs of the room |
| F | animation timeline under a stall | M5 (D11, D12) | `dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <obb>/assets/cozmo_resources/assets --name anim_bored_01 --wwise <obb>/assets/cozmo_resources/sound` | keyframes fired = keyframes in clip; no stalls reported | the clip plays as before. There is no way to force a stall from the tool; this confirms nothing regressed in the normal case |

Items A and A2 are the M9 acceptance. Items B–F are carried over from the fidelity sweep and its
reconciliation (`SOURCE_FIDELITY_AUDIT.md` §8, §10) and were never gated on M9.

## What A cannot tell you, and what would

The sampler's rules are Wwise runtime semantics taken from public documentation (`WWISE_MUSIC.md` §3):
whether notes sustain and release the way the stock app made them, whether the level is right, and
whether the get-in branch is silent during a song. A recording of the stock app singing the same song
would settle those; if one can be made (the original app still runs on some phones), put it beside the
`--render` WAV of the same song and compare by ear.

## Results

Fill in as run. Until then every row above is "offline-verified, hardware pending", and no milestone's
freeze depends on it: M9 is complete offline, and the next milestone does not use these results.

| # | date | firmware | automated | human | acceptance file |
| --- | --- | --- | --- | --- | --- |
| A | | | | | |
| A2 | | | | | |
| B | | | | | |
| C | | | | | |
| D | | | | | |
| E | | | | | |
| F | | | | | |
