# Pilot: a mechanical log-key pre-check (manager, 2026-10-16)

Step 4 of the operator's 2026-10-16 direction (`PROJECT_STATE.md`, "Where things stand"): try a narrow mechanical log
check, and keep it only if it catches real defects cheaply.

## The tool

`re-analysis/tools/logkey_check.py` collects every identifier-like string literal (an event key or a channel) inside a
call whose arguments name an `EngineLogLevel`. It then looks each one up among the binary's NUL-terminated strings,
both in `.rodata` and inline in `.text`. Each literal is classified:

| Class | Meaning |
| --- | --- |
| EXACT | Found as a whole string |
| COMPOSED | Not found, but its dotted prefix is a string, so the engine builds the key at runtime; review it |
| PART | Found only inside a longer string |
| ABSENT | Found nowhere |

It runs in about 10 seconds on the whole stack. `--rev` reads a past commit.

**What it cannot check:**
- level;
- the branch the log sits on;
- order;
- arguments and format text;
- whether the log should exist at all, apart from a key the binary doesn't have.

## Results

**On the current main** (854943c5 plus local commits), 875 key literals were checked:

| Class | Count |
| --- | --- |
| EXACT | 836 |
| COMPOSED | 7 |
| PART | 25 |
| ABSENT | 7 |

Of the 7 ABSENT:
- **1 is a false positive:** `"builtin"`, a decay-graph name.
- **6 are probable real defects** in code already built, and in part already verified.

| Site | Stack key | Binary has | Record |
| --- | --- | --- | --- |
| Sensors.cs:903 | RobotImplMessaging.HandleFallingStarted, "timestamp {x}" | `Robot.HandleFallingStarted.FallingStarted`, "timestamp: %u" (0x00534F64/0x00534F68 adr → 0x535000/0x53502C) | M10-011 |
| CompoundActions.cs:187 | ICompoundAction.GetCompletionUnion.ProxyTagMissing | `ICompoundAction.GetCompletionUnion.InvalidProxyTag` | M7-020 |
| AnimationScheduler.cs:1654 | AnimationStreamer.BufferFaceToSend | text "Failed to get RLE frame from procedural face." (with the period); the key is still to be confirmed | M5-013 |
| Mood.cs:552 | MoodManager.Update.TimeStepTooSmall, "dt …" | `MoodManager.BadTimeStep`, "TimeStep %f (%f-%f) is < %f - clamping!" | M7-013 |
| FaceBehaviors.cs:558, ObjectBehaviors.cs:113 | ReactionTriggerStrategyPositionUpdate.RobotReactedToId | only the mangled symbol; probably an invented log | M10-003 |

An Opus extractor is confirming each native call site before any fix is queued (CODEX-BUILDER rule 2).

**On a historical defect:** B-M15-1 had a wrong key, `RobotDataBackupManager.cs:45`, fixed in f5696c57. The broken
revision d096ffcb shows that key as PART, not ABSENT. The stack builds the key from a prefix and a fragment
(`ReadAllDataFromRobot`), and that fragment occurs inside other real keys. So **the check misses defects in keys the
stack composes itself.**

## Verdict

**Keep it as a cheap pre-check.** It found 6 probable defects that passed earlier reviews, at almost no cost. It is
not a verdict:
- **Builder:** run it before declaring a job DONE. Every ABSENT key is either fixed or justified in the status file.
- **Verifier:** treat ABSENT as a lead to confirm in the binary, as with any other claim.
- **It never certifies a log.** EXACT says nothing about level, branch, order or text. B-M5-19's wrong-branch warning
  would show as EXACT.
- **Possible extension:** the same lookup for format strings, where the stack keeps the native printf template. Most
  stack texts are C# interpolations, so the yield is unknown. Not done.
