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

The face checks `acknowledge-face`, `interact-with-faces` and optional `react-to-pet` use a 45-second window
for the named engine behavior. The harness records the matching start and stop transitions. A start earns the
step's pass, and the operator's answer remains an observation. The plan shows `search-for-face` and
`drive-to-face` as MISSING because their configured behavior constructors are unavailable in the stack.

The needs steps `express-needs`, `needs-change-animation` and `drive-in-desperation` temporarily set Energy
to the Warning or Critical bracket through the engine's ForceSetNeedsLevels message. Keep Cozmo off his
charger, on his treads, and give him room to drive. The harness restores the three saved levels when the
observation window ends or you stop the run. `earned-sparks` watches for a genuine pending Freeplay reward.
The engine's level-setting message cannot create one.

`result.json` groups the engine's behavior transition (0x87), reaction transition (0x88), objective (0x8B) and
observed face (0x46) reports by step, with the operator's answer. It also includes cube connection changes (0x0D)
and movement and stop reports (0x0F, 0x10). The old/new fields show which behavior or reaction ended and which began. `events.jsonl` retains startup, between-step reports, and engine diagnostics.
The idle step also records the timed window within its phase. Missing paths reported by the stack appear in the
bundle. An empty report list is also an observation.

Send the folder named at the end for review. The original observation steps have no pass/fail verdict.

To print the plan and validate its record references without a robot:

```powershell
dotnet run --project cozmo-stack/src/Cozmo.Conformance -- m15-cube-hw --personality --validate
```

Add `--steps pickup,putdown` to run just those two steps, in the usual script order. The plan above lists
the available step IDs. You can combine `--steps` with `--validate` to preview a partial run.
