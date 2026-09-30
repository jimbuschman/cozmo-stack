# Build and verify checklist

Written after the complete audit of 2026-09-29 (`re-analysis/research/20260929-audit-complete.md`). 84 of 239
settled records didn't hold, and every failure was one of the patterns below. The implementer works through this list
before writing code. The verifier checks every item on every batch, and a failed item blocks the commit.

## 1. The production path

- **Name the entry.** Which engine function does this behaviour live in, and which C# method is its counterpart on the
  live path (constructed and called by `CozmoRobot`, `CozmoEngine`, `FreeplayStack` or `VisionSystem` in production)?
  Put both in the record's evidence.
- **No parallel copy.** Don't add a class that re-implements behaviour an existing live component already carries.
  The M7 audit found `IdleBehavior`, built only by a tool, switching off the streamer's own port of the engine's live
  idle. If the live component is wrong, fix it.
- **Test through the entry.** At least one test drives the behaviour through the live entry, not by calling the
  helper directly.

## 2. The structure between the constants

For every behaviour you build, answer these from the disassembly, not from the C#:
- **Gates:** what does the engine test before and around this path? Flags, states, locks, "is it streaming",
  "is it synced", "is it carrying". The M4, M7 and M11 failures were missing gates.
- **Order:** in what order does the engine call things in this tick, and what runs first? (M8's tick order, and M5's
  clip-end tick.)
- **Where it runs:** from which function, and on which clock? For example, an NV request is sent from
  `NVStorageComponent::Update` after Gate A, not from `Read`.
- **What goes on the wire:** every message the path sends, including the ones around the main one. For example, a
  head move also sends DisableAnimTracks and EnableAnimTracks for its track lock.
- **Failure results:** what the engine does on failure. A locked track fails the action with 0x03000019; it doesn't
  retry.
- **Slot identity:** for a virtual call, resolve the slot from the vtable relocation. The object's vtable pointer
  points 8 bytes into the vtable, so slot = (relocation address - vtable start - 8) / 4. Don't guess from a name.

## 3. Nothing deferred

A settled record owns its whole production path. If part of the path is missing, or only described in a comment, a
provenance note or another record's `unresolved`, the record isn't settled. Give the missing part its own record first
(AGENTS.md).

## 4. Numbers

- **Floats are the engine's bits.** Write an engine constant as its bit pattern: for example,
  `BitConverter.Int32BitsToSingle(unchecked((int)0xBEDF66F3))`, or a named constant with the hex in a comment and a test
  asserting the bits. Don't write a rounded decimal like `-0.436332f`. It was a few ULPs off in M2, M4, M5, M11 and M14.
  The lint in `FidelityLiteralLintTests` flags new long decimal literals in `// fidelity:` files.
- **Width.** If the engine computes in `float` (`vadd.f32`, `sqrtf`), compute in `float`. Don't widen to `double`.
- **Operation order.** Keep the engine's association and operation order: `1/x` then multiply is not `/x`; `a*b+c`
  is not `a+b*c` in float.
- **Integer semantics.** Signed or unsigned compares, truncation toward zero, u8/u16 wrap.

## 5. Tests

- **Expected values come from the binary or the shipped assets,** never from running the implementation. A test whose
  expected value is the code's own constant is circular.
- **Test the failure and edge branches** the engine has: the first sample, zero, the boundary with its strict or
  inclusive compare, NaN.
- **A test that asserts behaviour the audit contradicted is wrong.** Fix it from the source; don't keep it.

## 6. Settling (the rule since 2026-09-29)

- **A job run by a cheap model (DeepSeek, GLM or similar) never settles a record.** It ends with the records built,
  still IMPLEMENTATION_GAP, with `unresolved` starting "built, awaiting strong verification:", followed by what was
  built and the commits.
- **Only an Opus verifier settles:** the manager's `cozmo-verifier` subagents, run from the Opus manager session. It
  checks the batch against this list and the binary, then moves the records to EXACT_SOURCE. A Codex re-audit is a
  cross-check, not a settlement.
- **A Sonnet worker never settles, audits or verifies for settlement (2026-09-30).** Its jobs end like a cheap
  model's: built, still IMPLEMENTATION_GAP, "built, awaiting strong verification:". Its own `cozmo-verifier` passes run
  on Sonnet (the agent pins no model), so they count as the job's internal checks only. Why: Codex's calibration
  (`re-analysis/research/20260929-audit-calibration.md`) failed 7 and part-failed 1 of the 17 M12 records a Sonnet
  audit had called clean.
