# PERSONALITY-STEP-SELECT-1

`m15-cube-hw --personality --steps pickup,putdown` selects only those prompts in script order. Validation and execution use the same list derived from the step definitions. Matching is case-insensitive, repeated IDs run once, and empty or unknown selections fail with the available IDs. Omitting the option keeps all steps. Putdown can also be run on its own, with a prompt to lift Cozmo first if he is already on his treads.

The harness build passed. Offline validation printed exactly the two requested steps and all 11 by default. Invalid IDs, empty input and use under `--skills` each returned exit code 2. This is a harness option and requires no engine behavior change.
