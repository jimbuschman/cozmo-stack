# Outside-AI commands

With the PC on Cozmo's Wi-Fi, put him off his charger on a clear, well-lit surface. Leave room to drive and
turn, well away from any drop. Have one working light cube ready. Use one face in view and follow the prompts.

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m15-cube-hw --commands --resources re-analysis/obb/assets/cozmo_resources --banks re-analysis/obb/sound_meta
```

The harness selects Selection/Wait to keep Freeplay off. Follow the prompts for head, lift, driving, turning,
tracking, animation, speech, and the go-to pose. Before turn-to-face and track-face, the harness raises the
head to 20 degrees. Get down near Cozmo's level in good light, about 30–60 cm in front of him, with your face a
little above his camera. For turn-to-face, start about 20 degrees to either side, with both eyes in view, and
hold still. The harness reports the signed bearing and waits for a fresh face at least 15 degrees off center
before starting the 20-second action window. For tracking, start centered and move slowly in a small arc
while keeping your face fully in view. The harness requests cancellation after 10 seconds and judges the
engine's active cancellation result. Visible tracking is an operator observation.

Keep the cube out of view until turn-to-cube. Then place it upright about 25 cm from Cozmo and about 45
degrees to one side, with a marker facing him. Bring it in only far enough for the marker to enter his
view. The harness waits for a fresh report after you press Enter. Move and tap the cube when prompted.
The animation step plays the shipped `CodeLabHappy` celebration
(`ag_codelab_happy`, `anim_meetcozmo_celebration_02`). Press Ctrl+C to stop and save the run.
Remove all cubes from the table before the go-to-pose prompt.

The speech and named enrollment steps use the existing English voice support and local sound banks. The last
steps enter Meet Cozmo, teach one temporary name, then rename and erase only the face successfully enrolled
by this run. If enrollment declines because the face is already known, rename and erase are skipped. The
bundle reports missing speech, targets, enrollment results, and other unrun steps.

`result.json` has each engine result, expected code, source citation and operator answer. Rename and erase
deliver ID/name reports and are observation-only. `events.jsonl` records the face bearing, public face,
cube and object-pose reports, and processed-image timestamps and modes alongside engine logs. Ctrl+C keeps
a partial bundle.

Send the folder named at the end for review.

Before each command, the script waits up to 15 seconds for the current reaction to finish and
the engine to enable external actions. The go-to start pose is sampled after the wait. A timeout or a refused
command is recorded as a harness error with no engine result. Cancellation accompanied by a new reaction
inside the command's window is an observation, with the reaction named in the bundle. The script waits and
retries that command once. Turn results include the signed angle traversed from the last applicable
TurnInPlace engine log when one is available.

To validate the plan and source record references without a robot:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m15-cube-hw --commands --validate
```
