# Cube pickup and put-down test: operator guide

## What this test is for

Cozmo picks up a cube and puts it down twice. He uses the same controls an outside AI will use:
1. With the cube right in front of him.
2. With the cube further away, so he has to drive to it first.

It checks that the new pickup and put-down controls work on the real robot. It also records one detail the reviewer
needs, the order of the robot's "busy" signal when he puts a cube down.

**Nothing passes or fails during the run.** You answer five short questions at the end, and the recording is reviewed
afterwards.

## Before the first run on this PC

The shipped engine library must be in `resources/lib/armeabi-v7a`, as for the earlier tests. It is never committed.

The cube marker library must also be built on this PC. It is extracted from that engine library by
`python re-analysis/tools/extract_marker_library.py resources/lib/armeabi-v7a/libcozmoEngine.so` (Python required), then rebuilt in.
The test stops at the start with that instruction if it is missing. Cozmo raises his head a little before looking for the cube.

## Setup

1. **The table:** a clear table, well away from the edge, with Cozmo **off** his charger. He moves a little in this
   test.
2. **The cube:** one light cube about **10 cm (4 in)** straight in front of his face, with a marker side facing him.
3. **The other cubes:** keep them out of his view.
4. **The light:** good, even light helps him see the marker.

## Run

Put the PC on Cozmo's Wi-Fi. Then, from the repository root:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m15-cube-hw --resources re-analysis/obb/assets/cozmo_resources
```

It takes about 5 minutes:
1. It connects, which can take up to a minute.
2. It waits until he reports seeing the cube.
3. He picks the cube up. `>>> WATCH` is printed first.
4. He puts it down. `>>> WATCH` is printed first.
5. You move the cube about **25 cm (10 in)** away and a little to one side, still facing him, and press Enter.
6. He drives to it, picks it up and puts it down again.
7. The questions come up.

If he doesn't report seeing the cube within 30 seconds, that round is skipped and recorded. Nudge the cube so a
marker faces him squarely.

## What to send back

Send the folder named at the end, under `re-analysis/acceptance/hardware/`.

## Driving round (opt-in)

The second round, where he drives to a cube further away, runs only with `--with-drive`. It stays off until the far-side approach bug (PICKUP-PREDOCK-SIDE-1) is fixed: on 2026-11-09 it took him to the table edge and he fell off.
