# Left-alone Freeplay test — operator guide

## What this test is for

Cozmo is started in Freeplay and left alone. The program records what he chooses to do and everything sent to him.
**Nothing passes or fails during the run.** You answer three questions at the end, and the recording is reviewed
afterwards. This is the first robot run of the whole idle loop: looking for faces, Hiking and NothingToDo.

## Setup (before you start)

1. Put Cozmo on the **floor**, **off the charger**, in a clear flat area at least **1 m** across.
2. Put his cubes well away or out of sight.
3. Keep people and pets out from in front of him if you can. A face he sees can change what he does; that isn't a
   failure, but it makes the run harder to read.
4. Don't touch Cozmo from the start until `TEST FINISHED` is printed. If he heads for an edge or gets stuck, press
   Ctrl+C. The run is still saved.

## Run

PC on Cozmo's Wi-Fi. From the repository root:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m13-idle-hw --resources re-analysis/obb/assets/cozmo_resources
```

It takes about 5 minutes: up to a minute to connect, 4 minutes of Cozmo left alone, then the questions. Each change
of behaviour prints a line as it happens.

## What to send back

The folder named at the end, under `re-analysis/acceptance/hardware/`.
