# Research request R-M15-NEEDS-GET-OUT (M15-072)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** a builder job for the PlayNeedsGetOutAnimIfNeeded action. It is one of the engine's
  ClearSevereNeedExpression callers (dtor 0x0054504C..0x0054505E; CheckIfDone 0x005451C8..0x005451EE) and is absent
  from the stack. Today it is a visible MISSING.
- **Context:**
  - `research/20261021-m15-severe-need-input.md` and its check `research/20261025-r-m15-severe-input-claude-check.md`;
  - `research/20261025-b-m15-12-claude-check.md` Q2.

  The symbols include `ExternalInterface::PlayNeedsGetOutAnimIfNeeded` (Unpack PLT 0x004C44CC), so the app may send it
  as a game message. Run `python re-analysis/tools/known.py <address|name>` first.
- **Answer file:** `research/20261025-m15-needs-get-out-action.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **Who creates the action:** every constructor caller.
   - Include the engine handler for the `PlayNeedsGetOutAnimIfNeeded` game message: its G2E tag and payload fields.
   - Include the app sender in `unity/scripts/csharp` (authority 2): when and why the app sends it.
   - Include any behaviour or activity that creates it.
2. **The action body:**
   - constructor fields (+0x88, +0xB8, +0xC9 and the rest);
   - which animation trigger it picks for which selected need, and when it plays nothing;
   - Init, CheckIfDone (0x005451C8 and around it) and the destructor;
   - the result it returns;
   - the action tag/type and the slot it is queued in.
3. **Trigger 0x23F:** its name from the AnimationTrigger table, and why CheckIfDone excludes it.
4. **Observable effects:** robot messages, game-to-app messages, and state that later feeds behaviours.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
