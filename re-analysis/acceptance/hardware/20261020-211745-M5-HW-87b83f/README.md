# M5-HW run 4 judgement (manager, 2026-10-20)

**The hardware run passes. M5 is not yet ACCEPTED:** the layer's records still need their settling pass
(`acceptance/M5-blockers.md`: 23 IMPLEMENTATION_GAP records "built, awaiting strong verification").

Tool commit b4d37746. Startup clean (readiness 665 ms, 0 error logs). Charger check: OFF_CHARGER.

| Part | Result |
| --- | --- |
| TAGS, ABORT, KEEP_ALIVE, LOCKED_HEAD, AUDIO, PARAMETERS, DRIVING, TEARDOWN | MATCH as printed |
| LAYERS | Printed INCOMPLETE; **regraded MATCH** (below) |
| LIVE_IDLE | OBSERVATION (never graded) |
| SLEEP | KNOWN_GAP (M5-038 / M1-045, M8 recipient) |

**LAYERS regrade.** The lifecycle rule required the stream End (0x9A) to be the last outbound message of any kind in
the window. The backpack-light component's SetBackpackLightsMiddle (0x03) and TurnSignals (0x11) followed it. M5-028's
lifecycle is the animation stream's own (0x8D..0x9B), so the rule now filters to those (fix committed with this
judgement, regression `OtherComponentsSendsAfterTheEndDoNotBreakTheStreamLifecycle`). Every other condition held in
this bundle: 13 face images, all 13 assigned to frames whose StreamLayers face flag was set, 0 missing, 0 unexpected,
20 backpack frames, one Start, one End, no Abort, drained.

**Observations (not graded).** Operator saw the face glitch and a backpack light turn red (LAYERS), small wheel/body
movement (KEEP_ALIVE live idle), no whole-head movement while the head track was locked (LOCKED_HEAD), one blink in
60 s (PARAMETERS; the engine generated blinks on its call-counted timer, `blinkEvidence`).

M5-036 (HARDWARE_ONLY, firmware execution/display) has its run; settling it is part of the records pass.
Run 3 (`20261020-200624-M5-HW-97161e`) and its findings: `research/20261020-m5-*.md`.
