# PERSONALITY-HW-1

`m15-cube-hw --personality` uses the existing hardware runtime, camera and evidence bundle. It enables Freeplay and guides the operator through 11 steps from pickup to a 15-minute idle window. Each step stores a yes/no observation and the engine reports in its time range. Reports between steps and on startup remain in the full event stream. Ctrl+C keeps the partial run, including an interrupted step.

The report sources are BehaviorTransition 0x87 (M8-012, 0x005A22B4–0x005A24AC), ReactionTriggerTransition 0x88 (M8-012, 0x005A20AA–0x005A20D6 and 0x005A2EE8), BehaviorObjectiveAchieved 0x8B (M7-019, 0x005BE228 and pack at 0x0071BEAE), and RobotObservedFace 0x46 (M14-008). Old and new IDs remain in the bundle alongside the shipped enum names. The face report records the published name separately from the operator's observation of spoken recognition.

This is an observation harness. The existing face-position reaction source boundary (M10-003 at 0x0060CF70) and any live MISSING markers stay in the report. No engine behavior or manifest classification changed.

The no-network `--personality --validate` dry-run printed all 11 steps and validated 12 record references. The Freeplay and skills modes also validated offline. Operator run and review remain pending. See `re-analysis/acceptance/hardware/scripts/personality-hw-1.md`.
