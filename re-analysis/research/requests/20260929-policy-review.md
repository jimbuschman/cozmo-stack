# Research request: review of the COMPATIBILITY_POLICY and EQUIVALENT_IMPLEMENTATION records

- **Date:** 2026-09-29
- **Requested by:** manager (Claude)
- **Kind:** independent extraction (a review)
- **Do it after:** `20260929-M9-singing-gaps.md`
- **Answer file:** `re-analysis/research/20260929-policy-review.md`

## Why this is needed

The rule since 2026-09-27 (AGENTS.md, "Exact, always"): whatever ships in the APK or the OBB is reproduced exactly.
EQUIVALENT_IMPLEMENTATION is only for behaviour whose code doesn't ship, such as the phone's system libraries. Many of
the 32 records below predate that rule. Each needs a verdict, backed by the source, on whether it may stay as it is.
The answer feeds the manager's decisions; it changes no record itself.

## The question, for each record

Read the record's current text in `re-analysis/fidelity_manifest.json`, then decide which of these it is:

- **A. Host-side necessity.** The original's behaviour lives in code that doesn't ship (Android, the OS, the phone's
  libraries), or is undefined (stale stack bytes, reads past a buffer), or the record is about this stack's own tools.
  Say which, and cite the source that shows it.
- **B. Operator decision.** The record carries a decision the operator made and recorded (for example M1-040, M1-042,
  M1-038, M1-034; see `PROJECT_STATE.md`, "Recorded policy" and "Operator decisions"). Quote the decision. Don't argue
  with it: say only whether the record's text and code match what was decided.
- **C. Ships and differs.** The engine's own behaviour ships in `libcozmoEngine.so` (or the Unity code or the assets),
  and the record keeps something else. Give what the engine does, with instruction citations, and what exact
  reproduction would take. Mark UNKNOWN wherever the source doesn't settle it.

## The records

| id | status | title |
| --- | --- | --- |
| M1-013 | COMPATIBILITY | Windows high-resolution timer realising the 2 ms and 60 ms periods |
| M1-014 | COMPATIBILITY | Host thread structure that realises the engine threading |
| M1-022 | EQUIVALENT | UDP socket: setup, ephemeral local port, send errors, receive loop, reopen |
| M1-034 | COMPATIBILITY | Handler isolation, a deliberate departure: in the original a handler exception aborts the engine process |
| M1-036 | COMPATIBILITY | Crash reporting after an engine-thread abort |
| M1-037 | COMPATIBILITY | Host trigger for the socket reset |
| M1-038 | COMPATIBILITY | Stop processing a frame after a handled DisconnectRequest sub-message |
| M1-039 | COMPATIBILITY | Windows ICMP port-unreachable on UDP receive is no data |
| M1-040 | COMPATIBILITY | Accept every robot firmware, and log it |
| M1-042 | COMPATIBILITY | The original app's post-connect defaults are sent by this stack |
| M2-017 | COMPATIBILITY | A field whose read failed in a kept malformed message holds 0 / false |
| M3-004 | COMPATIBILITY | Warm-up frames are flagged and delivered like any other |
| M3-017 | COMPATIBILITY | Test tones, beeps and sweeps |
| M3-019 | COMPATIBILITY | The connection-time SetCameraParams stale stack bytes |
| M3-020 | COMPATIBILITY | A payload that is empty or all 0xFF |
| M4-004 | COMPATIBILITY | Motion is gated on calibration here; the engine reacts to it instead |
| M4-006 | COMPATIBILITY | Wheel confirmation tolerance 35 percent / 5 mm per s |
| M5-003 | EQUIVALENT | Face per frame and blending: GetFaceHelper, Interpolate, the Clip table, Combine for layers |
| M5-020 | COMPATIBILITY | Expressions helper faces |
| M6-021 | COMPATIBILITY | The injectable RNG seed seam |
| M8-004 | COMPATIBILITY | Behaviours built in code carry a score of their own; the engine's default is zero |
| M8-008 | COMPATIBILITY | The head recalibration wait: the engine has no timeout, this stack keeps a backstop |
| M8-009 | COMPATIBILITY | Behaviour scope undo order |
| M8-010 | COMPATIBILITY | Behaviour inventory classifier rules |
| M9-016 | COMPATIBILITY | Streaming uses a 66 ms stack lead policy against the original 30000-byte and 14-frame budget |
| M11-012 | COMPATIBILITY | The nominal camera calibration stand-in |
| M11-013 | COMPATIBILITY | AllowUnconnectedObjects switch |
| M12-014 | COMPATIBILITY | The docking error signal's last two bytes |
| TOOL-001 | COMPATIBILITY | Conformance CLI pass and fail criteria |
| TOOL-002 | COMPATIBILITY | The fake robot side answers place docks without a marker signal |
| TOOL-003 | COMPATIBILITY | The --nominal calibration override |
| TOOL-005 | COMPATIBILITY | The hardware acceptance campaign |

## Also check

For each record: does the live production path depend on the policy, or is it only a test or tool seam? A test-only
seam is fine if the live default is the engine's behaviour. Say which it is.

## Out of scope

- Any code, inventory or manifest edit. No branches, commits or pushes.
- Records with other statuses.

## Answer format

One table: `id | verdict (A/B/C) | what the original does, with citation | what the stack does | live path or test
seam | what exact reproduction would need (C only)`. Then a short list of the C records, most important first, and any
record whose text contradicts its code.
