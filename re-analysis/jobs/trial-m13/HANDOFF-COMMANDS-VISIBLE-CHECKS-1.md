# COMMANDS-VISIBLE-CHECKS-1

`m15-cube-hw --commands` now asks the operator to position their face about 20 degrees to one side with both eyes visible. It uses the signed bearing from the fresh public face pose and the robot state in the same origin (E2G 0x46, M14-008; RobotState pack 0x00711970..0x00711A62, M1-053). It waits for an absolute bearing of at least 15 degrees, then prints and records `turn-to-face-bearing` with the signed angle and face timestamp. The 15-degree value is a harness setup choice for visible travel. The action still uses its own turn at 0x0054C500, with the existing head setup and result criteria.

The cube prompt gives a concrete placement of about 25 cm, a little to one side. The waiter now listens for the first new public light-cube report after Enter, using E2G 0x44 (pack 0x007131E2) and the prior 20-second window. The action still uses 0x0054ADD4. A missing cube skips that step.

The demonstration uses `CodeLabHappy` once, from the shipped `assets/animationGroupMaps/AnimationTriggerMap.json`. The `assets/animationGroups/Codelab/ag_codelab_happy.json` group has one clip, `anim_meetcozmo_celebration_02`. The asset test loads the clip and checks its duration and procedural face frames. The animation success criterion stays at 0x005441C0. The hardware run remains for the operator, and no fidelity record was reclassified.
