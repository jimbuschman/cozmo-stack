# Research request: M1+M2 settling residue (narrow)

- **Date:** 2026-10-18
- **Requested by:** manager
- **Kind:** narrow extraction, five small questions
- **Answer file:** `re-analysis/research/overnight/20261018-ov127-m1m2-settling-narrow.md`

## Why this is needed

M1+M2 is the acceptance target. The Opus settling pass (`research/20261018-m1m2-settling-pass.md`, read it first) left
these source questions open. Re-read the current manifest records named below before answering. Under AGENTS.md "What
we reproduce", answer only what touches robot traffic, robot behaviour, C# API delivery or readiness, or downstream
engine state; say "no such effect" with citations where that is the answer.

## Questions

1. **LatencyMessage (M1-024; operator 2026-10-18: build it).** CozmoEngine::UpdateLatencyInfo (called at 0x004ED64C;
   countdown reset to 10 at 0x004ED88C/0x004ED8AC; LatencyMessage ctor 0x004EDA68; send via ext-interface +0x1C at
   0x004EDA76). Give: the exact gates (`kNetConnStatsUpdate` 0x00C95F41, engine+0x60) and the countdown's initial value
   and decrement point, so the first send tick is exact; every payload field, its type, and where each value comes from
   (which transport/connection statistics, at which offsets, any arithmetic and its float/integer types); anything else
   the function writes or resets. If a source statistic has no stack counterpart, name it.
2. **The unsubscribed Impl handlers (M2-018).** InitRobotMessageComponent 0x00532A7C subscribes tags with no stack
   subscriber. Give the full body effects of:
   - C3 HandleRobotPoked 0x005366FC: the RobotPoked E2G payload (Robot::Broadcast 0x00536754) and anything else written;
   - BA (0x00538088) and BB (0x0053819C): what Robot::SetOnRamp (0x005380D2, 0x005381E6) writes and who reads it
     (robot+0x316 readers; does it change anything sent to the robot or the C# API, or other engine state);
   - B1 HandlePrint, B2 0x005384CC, B7 0x0053866A, BC 0x005382B4, BD 0x005383C0: confirm log-only (no state, no send);
   - B3 HandleGoalPose 0x005352C8: confirm VizManager-only.
   Also: which robot-to-engine message tags these are (names), and whether the robot firmware is documented anywhere in
   the shipped assets or app code as sending them.
3. **LoadRobotConfigs 0x00522280..0x00523042 (M1-054).** A census only: each config it loads (file/key), the field it
   stores into, and whether an existing record owns that consumer (quote the record id) or none does.
4. **Local E2G consumers (M1-055, M1-056, M2-019).** Using the method of the M2-022 answer
   (`research/overnight/20261014-ov44-m2-022.md`), list the engine-local subscribers, if any, to E2G tags 0x5D
   AnimationAvailable, 0x9A EndOfMessage, 0x7B UpdateEngineState, the EngineLoadingDataStatus message sent at
   0x004ED7C0, 0x2A (CLADVersionMismatch), LatencyMessage, and RobotPoked. For each subscriber: what it does.
5. **The state-3 residue in M1-024's range.** UiMessageHandler vtbl+0x44 call at 0x004ED4FC and the ctx+0x24 +0x10
   zeroing at 0x004ED510: what each does, in the narrow sense above.

## Already known (do not redo)

OV124–OV126 and their checks; the B-M1-4..B-M1-7 checks; the settling pass. SupportInfo, UiDeviceAvailable/Connected
and Ping are excluded (M1-056/M1-059). UpdateAudioController (0x004ED6C4) is M6's.

## Answer format

Follow `.opencode/agent/cozmo-extractor.md`: step | what the original does | citation | record | classification.
A coverage table first (question, answered / UNKNOWN, citation).
