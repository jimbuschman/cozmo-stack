# Research request R-M11-FAKE-IMAGE-PROCESSED (M11-035, M11-017)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** the builder job that binds the overhead writer's type-10 border stamp, replacing the visible MISSING
  "Robot::GetLastImageTimeStamp 0x00516EC0 at 0x00680396" (`cozmo-stack/src/Cozmo.Robot/Behavior/FreeplayStack.cs:324`).
  Row 68 found four writers of VisionComponent+0xE8. The fourth, FakeImageProcessed (store at 0x00657C2E), has no known
  caller, so the getter's value cannot yet be called complete.
- **Context:** `research/20261025-m11-last-image-timestamp.md` and its check
  `research/20261025-r-m11-rows-68-70-claude-check.md`. Also the current `unresolved` of M11-035 and M11-017 in
  `re-analysis/fidelity_manifest.json`. Run `python re-analysis/tools/known.py <address|name>` before tracing anything.
- **Answer file:** `research/20261025-m11-fake-image-processed.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **The narrow check first:** is any caller of VisionComponent::FakeImageProcessed (0x00657C24) already extracted and
   checked? Cite it.
2. **Callers:** every way the function is reached:
   - direct BL/BLX to the function and to its PLT stub;
   - vtable or function-pointer slots that hold it (dynamic relocations, `.data.rel.ro`);
   - exported-symbol use.
   For each caller: the engine event that runs it, its thread, and the timestamp and IMU arguments it passes.
3. **Only if a caller is reachable** with the camera streaming normally: when it runs relative to SetNextImage and
   UpdateAllResults (row 68 items 3b/3c), and whether it changes +0xE4 or +0xEC.
4. **If no caller is found:** list exactly what was searched, so the manager can decide whether it is unreachable. Make
   no exclusion claim.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
