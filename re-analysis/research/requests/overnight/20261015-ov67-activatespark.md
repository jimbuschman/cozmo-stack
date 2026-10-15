# Overnight research request OV67 (M15-006: the ActivateSpark handler)

- **Binary:** libcozmoEngine.so 3.4.0-1204, plus the shipped config under the OBB and the Unity scripts (authority 2) for
  what the app sends.
- **Record:** M15-006, "Freeplay activity selection and selected/deselected lifecycle". Its subsystem is M15-freeplay and
  its status is IMPLEMENTATION_GAP. Re-read it in `re-analysis/fidelity_manifest.json` before starting.
- **Why:** the operator decided on 2026-10-15 that the stack's API passes the app's spark request (ActivateSpark) to the
  engine handler. The handler is unbuilt; the stack only has the seam `FreeplaySystem.SetRequestedSpark`.
- **Answer file:** `research/overnight/20261015-ov67-activatespark.md`.
- **Format:**
  - one coverage table first;
  - then the detail rows: step, what the original does, the citation, and the classification.
- **Tooling:** use capstone with skipdata.
- **Rules:** `re-analysis/research/README.md`. Research only. The project scope is in AGENTS.md:
  - SDK and CodeLab paths are unsupported;
  - app-only inputs become COMPATIBILITY_POLICY;
  - crash paths fall under the M1-034 standing rule.

## The task

Trace the whole production path of the game-to-engine ActivateSpark message, and anything else the app sends to request
or cancel a spark:
- the tag and the payload layout;
- the subscription and the dispatcher;
- BehaviorManager's HandleMessage, every case, including case tag 0 at 0x005A3C92..0x005A3C9A (the 0x55 invalid
  latch);
- SetRequestedSpark 0x005A3EAC;
- every write to BehaviorManager +0x58..+0x65;
- SwitchToRequestedSpark 0x005A4220;
- the logs, the messages sent back to the game, and the order;
- the timeout and the sparks' end conditions;
- what the Unity app sends, and when (the payload values it uses).

For each step, give the exact branch conditions, the constants, the logs (level, channel, key, format, arguments,
addresses), the state writes and the messages.

**End with:**
- the build rows;
- any existing records the source contradicts (quote them);
- the new records needed;
- the unread ranges.
