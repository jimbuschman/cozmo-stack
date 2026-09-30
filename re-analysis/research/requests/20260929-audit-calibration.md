# Research request: audit calibration (M12, and M5 part A)

- **Date:** 2026-09-29
- **Requested by:** manager (Claude)
- **Kind:** independent extraction (an audit)
- **Do it after:** `20260929-R-BEH2-pre-extraction.md`, if that is still running
- **Answer file:** `re-analysis/research/20260929-audit-calibration.md`

## Why this is needed

Today's complete audit (`re-analysis/research/20260929-audit-complete.md`) re-checked every settled record. Opus auditors
covered most layers and found defects in nearly all of them. After a usage limit, Sonnet auditors covered the rest, at
about half the depth. Two layers came out completely clean under Sonnet:
- M12-manipulation, 17 of 17;
- M5-animation part A, 13 of 13.

That could be real, or the lighter audit missed things. This independent re-audit decides which. Read the report's
"The patterns" section first: those are the failure modes to look for.

## Records in scope

- M12-manipulation: M12-001 M12-002 M12-003 M12-004 M12-005 M12-006 M12-007 M12-009 M12-010 M12-012 M12-013 M12-015
  M12-016 M12-017 M12-018 M12-019 M12-021.
- M5-animation: M5-001 M5-002 M5-004 M5-005 M5-006 M5-007 M5-008 M5-009 M5-010 M5-012 M5-015 M5-016 M5-017.

## For each record

1. Read its entry in `re-analysis/fidelity_manifest.json` and its rows in `re-analysis/inventory/M12-manipulation.md`
   or `M5-animation.md`.
2. Open every cited address in `resources/lib/armeabi-v7a/libcozmoEngine.so` (mostly Thumb; the helper is
   `re-analysis/tools/emu/so.py`). Check that each says what the record claims. Compare every float literal bit for
   bit. For virtual calls, the object's vtable pointer points 8 bytes into the vtable.
3. Read the C# at the record's location and every `// fidelity: <id>` tag. Check constants, order, branches, the
   gates around the path, failure results and what goes on the wire, against the engine.
4. Check that the record owns its whole production path: the code is what the live stack constructs and calls, with
   nothing deferred to a comment or another record.
5. Check whether the test takes its expected values from the source or from the code.

## Out of scope

Any code, inventory or manifest edit. No branches, commits or pushes.

## Answer format

Per record: **HOLDS** (one line on what you checked) or **FAILS / PARTIAL** (each defect: what the engine does, with
the address; what the code does, with file:line; and why it matters). Then one line per layer comparing your result
with the Sonnet audit's (17/17 and 13/13).
