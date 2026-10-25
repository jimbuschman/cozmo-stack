# Research request R-M13-ROOT-CONTENT-TS (M13-030, M11-048)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** the M13 planner and BlockWorld records. `research/20261025-r-m11-rows-68-70-claude-check.md` Queued Q2:
  +0x38 is the memory map's root-content timestamp, read by the planner and BlockWorld+0x58. Whether the stack
  reproduces it is unchecked. Read M13-030's current title and `unresolved` before starting.
- **Context:** that check and its rows 68-70 answers. Run `python re-analysis/tools/known.py <address|name>` first.
- **Answer file:** `research/20261025-m13-root-content-timestamp.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **Writers of +0x38:** every write. Say which map operations update it (insert, merge, transform, clear) and with
   which clock.
2. **Readers:**
   - the planner reimport at 0x004FD50E;
   - BlockWorld at 0x006211F4, 0x006226F6 and 0x00625E98.

   For each one: what it compares +0x38 with, and what it does on a change (replan, re-import, re-check). Name the
   observable result: robot path messages, an object's state, or an API event.
3. **The stack:** where `cozmo-stack/src` keeps the equivalent value, if anywhere (`Vision/MemoryMap.cs`,
   `NativePathComponent.cs`, `Vision/BlockWorld.cs`), and whether each reader above exists there. Report differences
   only. Don't propose code.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
