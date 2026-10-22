# Research request R-M13-017-REST (M13-017, M13-024, M13-025)

- **Binary:** libcozmoEngine.so 3.4.0-1204 (and libopencv_imgproc.so where a footprint calls it).
- **Context:** the current `unresolved` of M13-017, M13-024 and M13-025 in `re-analysis/fidelity_manifest.json`;
  `research/20261022-b-m13-10-b-m13-11-claude-check.md`; `research/20261021-m13-017-located-charger.md`.
  Run `python re-analysis/tools/known.py <address|name>` before tracing anything.
- **Answer file:** `research/20261022-m13-017-remaining-footprints.md`.
- **Format:** one coverage table first: step | what the original does | address with the 1-3 instructions | record.
  Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items (only what the drive-off/charger-platform path needs)

1. **GetBoundingQuadXY for Markerless, Ramp and HumanHead objects:** the corner list and size each uses, and how the
   pose is applied (re-rooted first or not). Which of these can actually reach the charger-platform test
   (0x00513CD8..0x00513E2A) or the robot footprint? Say so; don't extract what can't.
2. **The GetBoundingQuad catch-all at 0x004E65C6:** what it catches, what it returns or logs, and whether the
   platform test can reach it with shipped objects.
3. **BlockConfigurationManager::SetObjectPoseChanged (0x00624892)** as the contact-charger observation reaches it:
   what it changes that later feeds robot traffic, behaviour or API events. Narrow check first: does it touch any of
   those? If not, say "no effect" with the evidence.
4. **The M11-074 map continuations and the M12-001/M12-045 cached-pose destructor** on the same path: the same narrow
   check, then extract only what has an effect.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
A claim that an earlier answer is wrong must paste the proving instructions. Extraction only: no code, no manifest edit.
