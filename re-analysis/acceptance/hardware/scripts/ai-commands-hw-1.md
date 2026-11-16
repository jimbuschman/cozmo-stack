# Outside-AI commands

With the PC on Cozmo's Wi-Fi, put him off his charger on a clear, well-lit surface. Leave room to drive and
turn, well away from any drop. Have one working light cube ready. Use one face in view and follow the prompts.

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m15-cube-hw --commands --resources re-analysis/obb/assets/cozmo_resources --banks re-analysis/obb/sound_meta
```

The harness selects Selection/Wait to keep Freeplay off. Follow the prompts for head, lift, driving, turning,
tracking, animation, speech, and the go-to pose. Before turn-to-face and track-face, the harness raises the
head to 20 degrees. Get down near Cozmo's level in good light, about 30–60 cm in front of him, with your face a
little above his camera. The 20-second action window starts after the first fresh public face report. While
waiting for that report, you can reposition or press Ctrl+C to stop and save the run. Tap and move the cube
when prompted. The face-tracking request is cancelled 10 seconds after it is queued. The active cancellation
code is expected for that step. The time the robot visibly tracks is an observation.

The speech and named enrollment steps use the existing English voice support and local sound banks. The last
steps enter Meet Cozmo, teach one temporary name, then rename and erase only the face successfully enrolled
by this run. If enrollment declines because the face is already known, rename and erase are skipped. The
bundle reports missing speech, targets, enrollment results, and other unrun steps.

`result.json` has each engine result, expected code, source citation and operator answer. Rename and erase
deliver ID/name reports and are observation-only. `events.jsonl` records public face, cube and object-pose
reports with the original payload and capture time, alongside engine logs. Ctrl+C keeps a partial bundle.

Send the folder named at the end for review.

To validate the plan and source record references without a robot:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m15-cube-hw --commands --validate
```
