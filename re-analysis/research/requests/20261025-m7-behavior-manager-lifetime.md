# Research request R-M7-BEHAVIOR-MANAGER-LIFETIME (M7-014, M15-072)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** a builder job that gives the stack's BehaviorManager (reaction locks) a robot-lifetime owner. Today the
  stack creates it only inside FreeplayStack (`cozmo-stack/src/Cozmo.Robot/Behavior/FreeplayStack.cs:169`). So
  SevereNeeds' DisableReactionsWithLock / RemoveDisableReactionsLock (0x005731A0 / 0x00572D56) are a visible MISSING
  whenever no Freeplay stack exists, for example at the automatic wakeup on connection
  (`research/20261025-b-m15-12-claude-check.md` Q1).
- **Context:** `research/20261025-r-m15-severe-input-claude-check.md`; the current M7-014 and M15-072 `unresolved` in
  `re-analysis/fidelity_manifest.json`. Run `python re-analysis/tools/known.py <address|name>` first.
- **Answer file:** `research/20261025-m7-behavior-manager-lifetime.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **Construction:** who constructs BehaviorManager (ctor 0x005A0864) and when, relative to Robot construction, AI
   construction (robot+0x264) and connection. Which robot offset holds it (SevereNeeds reads robot+0x44)?
2. **Init:** when InitConfiguration (0x005A0DFC) runs, and whether reaction locks can be taken before it. What does
   DisableReactionsWithLock (0x005A27E8) do when called before Init?
3. **Lifetime end:** what destroys it. Is it the same object for the whole robot connection, or rebuilt when the
   behaviour system restarts?
4. **Lock state across that boundary:** if a lock is taken and the behaviour system is then (re)initialised, is the
   lock kept, dropped or re-applied?

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
