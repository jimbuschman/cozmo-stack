# First robot speech test — operator guide

## What this test is for

Cozmo says three short sentences in his own voice, through the shipped speech engine and sound banks running in the
stack:

1. "Hello. I am Cozmo."
2. A longer sentence that the program **cuts off on purpose** once the sound has started.
3. "I can talk again."

**Nothing passes or fails during the run.** You answer five short questions at the end, and the recording is reviewed
afterwards.

## Before the first run on this PC

- The shipped libraries must be in `resources/lib/armeabi-v7a` (libcozmoEngine.so and the Acapela library), as for the
  face tests. They are never committed.
- The sound banks must be in `re-analysis/obb/sound_meta` (Init.bnk and `English(US)/Cozmo.bnk`), from the extracted
  OBB. If they are elsewhere, add `--banks <folder>`.
- The PC's language must be English. Otherwise each sentence reports "not ready" and nothing is said.
- The PC needs a working sound output device. It plays only silence: the PC paces the voice, and the sound comes from
  Cozmo.

## Setup

1. Put Cozmo on his charger, or on a table well away from the edge. He should not drive in this test.
2. Make sure his volume is up and the room is quiet enough to hear him.

## Run

PC on Cozmo's Wi-Fi. From the repository root:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m14-speech-hw --resources re-analysis/obb/assets/cozmo_resources
```

It takes about 3 minutes: up to a minute to connect, then the three sentences, about 2 seconds apart. Each prints
`>>> LISTEN` just before it starts. Then come the questions.

## What to send back

The folder named at the end, under `re-analysis/acceptance/hardware/`.
