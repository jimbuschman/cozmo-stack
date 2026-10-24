# Research request R-M12-PREACTION-WORLD (M12-001, M13-009)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** a builder job that replaces the B-M13-22 MISSING "M12-001/M13-009 downstream derived world constructor
  0x0050DF00..0x0050E04A" so that the charger's (and cubes') stored pre-action elements become world goals the way the
  engine builds them (`research/20261023-b-m13-20-21-22-claude-check.md`, `research/20261023-m12-charger-preaction.md`).
- **Context:** the current `unresolved` of M12-001 and M13-009; `research/overnight/20261014-ov04-m12-038-check.md`
  (pose ctor, distance ctor, derived ctor and MakeUnitLength rows); `research/20260929-R-VIS-M12-gap3-extraction.md`
  Q2.1/Q2.9/Q2.10 with its verify. Run `python re-analysis/tools/known.py <address|name>` before tracing anything.
- **Answer file:** `research/20261024-m12-preaction-world.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **The derived world constructor** (0x0050DF00, 334 bytes): from a stored element and the object's current pose,
   every step that produces the world pose (composition order, the +0x18 distance applied through MakeUnitLength and
   rescale, the parent set at the end), in f32/f64 exactly.
2. **The validity filter** GetCurrentPreActionPoses applies to world results, and the charger's own validity check
   (0x004EA648) if the collection calls it.
3. **The narrow check first:** OV04 already covers the derived ctor and MakeUnitLength; cite those rows and extract
   only what they leave out (for example the charger-specific validity and the parent/root handling).

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
