# Research request: re-audit the Sonnet-audited layers (M10, M15, M14)

- **Date:** 2026-09-30
- **Requested by:** manager (Claude)
- **Kind:** independent extraction (an audit)
- **Answer file:** `re-analysis/research/20260930-reaudit-sonnet-layers.md`

## Why this is needed

Your calibration (`re-analysis/research/20260929-audit-calibration.md`) showed that the Sonnet audit missed about
half the defects in M12 (Sonnet 17/17 clean, you 9 hold, 7 fail, 1 partial). The manager re-checked your central
addresses and they held; the demotions are Correction A2. The other layers the Sonnet audit covered are therefore
not trusted. This request covers the ones no worker is editing now. M11 is being rebuilt by R-VIS and comes later.

Read the audit report's "The patterns" section (`re-analysis/research/20260929-audit-complete.md`) and your own
calibration conclusion first: those are the failure modes to look for.

## Records in scope (the current EXACT_SOURCE records)

- M10-derived: M10-002 M10-005 M10-006 M10-009 M10-010 M10-011 M10-012.
- M15-freeplay: M15-004 M15-007 M15-008 M15-009 M15-010 M15-011 M15-012 M15-013 M15-015 M15-017.
- M14-faces: M14-001 M14-002 M14-003 M14-004 M14-005.

## For each record

The same five steps as the calibration request (`requests/20260929-audit-calibration.md`):
1. Read its manifest entry and its inventory rows.
2. Open every cited address in `resources/lib/armeabi-v7a/libcozmoEngine.so`. Compare every float literal bit for bit.
   For virtual calls, the vtable pointer points 8 bytes into the vtable.
3. Read the C# at the record's location and every `// fidelity: <id>` tag. Check the constants, order, branches,
   gates, failure results and what goes on the wire.
4. Check that the record owns its whole production path, with nothing deferred to a comment or another record.
5. Check whether the tests take their expected values from the source or from the code.

## Out of scope

Any code, inventory or manifest edit. No branches, commits or pushes.

## Answer format

As in the calibration. Per record: **HOLDS** (one line on what you checked) or **FAILS / PARTIAL** (each defect: what
the engine does, with the address; what the code does, with file:line; and why it matters). Then one line per layer
with the counts.
