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

Needs start full and stay full by default (operator decision 2026-11-17). The stack uses the engine's refill and
global needs-pause messages after stored needs load; decay, action deltas/rewards and local notifications pause
together. Add `--needs real` to any hardware command for the original needs behavior. Personality needs steps
temporarily select real mode for their observation window and restore the prior mode afterward. This host input
is M15-112; C6/C9 add no robot NV write (0x00697DA4..0x00698002, 0x00695E04..0x00695F7E).

The shipped engine library must be in `resources/lib/armeabi-v7a`, as for the earlier tests. It is never committed.

The cube marker library must also be built on this PC. It is extracted from that engine library by
`python re-analysis/tools/extract_marker_library.py resources/lib/armeabi-v7a/libcozmoEngine.so` (Python required), then rebuilt in.
The test stops at the start with that instruction if it is missing. Cozmo raises his head a little before looking for the cube.

## Setup

1. **The table:** a clear table, well away from the edge, with Cozmo **off** his charger. He moves a little in this
   test.
2. **The cube:** one light cube about **10 cm (4 in)** straight in front of his face, **sitting upright with its lights
   on top**, and one of its four side markers facing him squarely. Don't lay it on its side: the engine only picks up an
   upright cube, so a tipped cube makes him drive round to its bottom face (that is what sent him to the table edge on
   2026-11-09).
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
5. You move the cube about **25 cm (10 in)** away and a little to one side, still upright with a side marker facing
   him, and press Enter.
6. He drives to it, picks it up and puts it down again.
7. The questions come up.

If he doesn't report seeing the cube within 30 seconds, that round is skipped and recorded. Nudge the cube so a
marker faces him squarely.

## What to send back

Send the folder named at the end, under `re-analysis/acceptance/hardware/`.

## Driving round (opt-in)

The second round, where he drives to a cube further away, runs only with `--with-drive`. On 2026-11-09 the cube was
lying on its side, so the engine's only allowed approach was from the far side; it took him to the table edge and he
fell off (PICKUP-PREDOCK-SIDE-1: the engine's own choice, not a stack bug). Run the direct round with the cube upright
first, and use `--with-drive` only after that pickup works, with a hand ready near the edge.

## Skill rounds (opt-in)

Add `--skills` to run stack, roll, flip, wheelie, go-to, and mount after the pickup rounds. Follow each prompt to reset the upright cube. The flip and wheelie rounds record the action result and wire traffic. Before the go-to round, remove all cubes from the table and clear space for a 20 cm drive and a left turn. The script records the requested and observed poses. The charger round runs last. Clear the
cubes away and put the powered charger 20–40 cm straight ahead, marker facing Cozmo. The script waits for a fresh
located visual observation in the current origin, then records the queued mount result and the robot's contact bit
(M13-008, 0x0054E7B0). It selects the target from the charger E2G 0x44 report in the current BlockWorld origin.
Allow room in front of and behind Cozmo for the pre-dock drive, turn, and reverse.

`charger-observations.jsonl` records the charger reports from the whole skills run (E2G 0x44, pack at
0x007131E2). `docking-error-signals.jsonl` records the outbound corrections with host and robot timestamps,
geometry and raw payload (0x48, M12-014, producer at 0x0063BE80). These are observations. If a file cannot
be written, the other files are still attempted and `write-errors.txt` records the failure. Non-finite numbers
appear as the JSON strings `"NaN"`, `"Infinity"` and `"-Infinity"`.

Before each skill command, the script waits up to 15 seconds for the current reaction to finish and
the engine to enable external actions. The go-to start pose is sampled after the wait. A timeout or a refused
command is recorded as a harness error with no engine result.

Run `m15-cube-hw --skills --validate` to check the planned rounds and record references without a robot.

## Freeplay control (standalone round)

Use `m15-cube-hw --freeplay-control` with the same resource and robot arguments. Set Cozmo on a clear table, well
away from the edge and off his charger. Observe 60 seconds of Freeplay, one queued CozmoSaysSpeakLoop animation,
then 60 more seconds of Freeplay. The bundle includes the action result and the engine's behavior, reaction and
objective reports (0x87, 0x88 and 0x8B) in `events.jsonl`. A reaction can refuse the request through the normal
external-action gate. The script records the outcome and your observations.

Use `m15-cube-hw --freeplay-control --validate` for the offline dry-run.
