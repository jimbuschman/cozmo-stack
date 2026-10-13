# Research request R-SUBSCRIBERS (M2-018)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-play-area-check.md` section D, and the M2-018 record in
  `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-robot-message-subscribers.md`.
- **Format:** one coverage table per item, first. Each step gets an address, or UNKNOWN.
- **Tooling:** use a disassembler that skips undecodable data (capstone with skipdata). A linear sweep that stops at
  the first bad halfword misses code.

**Background.** Robot messages are broadcast by tag:
- the robot's own signal runs before the wildcard signal;
- subscribers on a signal run in registration order, synchronously;
- subscription goes through 0x00519F8C (→ 0x0051CD2A → 0x0051D060). HandleCliffEvent registers tag 0xC0 at
  0x00532B80..0x00532B8A.

## Items

1. **Every subscriber.** Enumerate every call site of 0x00519F8C (about 35), including veneers and PLT stubs. For each,
   give:
   - the enclosing function;
   - the tag or tags it subscribes;
   - the handler it binds;
   - whether it subscribes for one robot ID or the wildcard;
   - when it runs: the constructor, Init, a behaviour's OnActivated, or an action's Init. Also say when it unsubscribes,
     if it does.
2. **Registration order at robot connection.** Give the order in which the per-robot subscriptions are made, from
   `Robot::Robot` and the component constructors and inits it runs. For each tag with more than one engine subscriber,
   give the resulting call order.
3. **Behaviours and actions.** IBehavior::SubscribeToTags (0x005BCA4C) and the action subscriptions add subscribers
   later, while they run. Do those ever take a robot-message tag that a component already handles? If they do, say
   which tags, and whether they run before or after the component's handler. A late subscriber appends to the end.
4. **Removal during emit.** If a handler unsubscribes another subscriber, or itself, while a signal is being emitted,
   what happens? See 0x0069E274..0x0069E2B2, which re-reads the sentinel at each step and uses refcounts.
