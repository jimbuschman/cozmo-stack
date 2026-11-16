# Personality observations

Put Cozmo off his charger on a clear, well-lit surface. Have one working light cube ready. Use the table edge
with a hand ready to catch him. For the 15-minute idle step, put him on a clear floor or in an enclosed area and
stay out of view while supervising him.

From the repository root with the PC on Cozmo's Wi-Fi:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m15-cube-hw --personality --resources re-analysis/obb/assets/cozmo_resources
```

Follow the prompts for picking him up, setting him down, each of the three off-treads poses, a gentle shake, the
table edge, showing a face, tapping and moving a cube, and the 15-minute idle window. A previously enrolled face
lets you observe whether he responds by name. Each question takes a yes/no observation. Ctrl+C saves a partial run.

`result.json` groups the engine's behavior transition (0x87), reaction transition (0x88), objective (0x8B) and
observed face (0x46) reports by step, with the operator's answer. The old/new fields show which behavior or
reaction ended and which began. `events.jsonl` retains startup, between-step reports, and engine diagnostics.
The idle step also records the timed window within its phase. Missing paths reported by the stack appear in the
bundle. An empty report list is also an observation.

Send the folder named at the end for review. The harness does not assign a pass/fail verdict.

To print the plan and validate its record references without a robot:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m15-cube-hw --personality --validate
```
