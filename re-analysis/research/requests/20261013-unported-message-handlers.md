# Research request R-HANDLERS (M2-019, M7-027)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-robot-message-subscribers-check.md`, and the M2-019 and M7-027 records in
  `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-unported-message-handlers.md`.
- **Format:** one coverage table per item, first. Each behaviour-changing step gets an address, or UNKNOWN.
- **Tooling:** use a disassembler that skips undecodable data (capstone with skipdata). A linear sweep that stops at
  the first bad halfword misses code.

## Items

1. **RobotToEngineImplMessaging::HandleFWVersionInfo** (bound at 0x00532AC4 for tag D2; body 0x00533348). Read it
   completely:
   - every log (level, channel, key, format, arguments, with addresses);
   - every state write (which object, which offset, what value);
   - anything it sends, broadcasts or forwards;
   - its error paths.
2. **RobotToEngineImplMessaging::HandleDockingStatus** (bound at 0x00532C84 for tag D3; body 0x00536F1C). The same, in
   full. Say how often it logs: on every message, or only on some.
3. **The dev test behaviours' robot-message handlers.** FactoryTest, DockingTestSimple and LiftLoadTest are each built
   twice at Robot construction, and subscribe in their constructors.
   - **FactoryTest:** D6 (0x005D0864..), F3 (0x005D0930..), B8 (0x005D0998..), EE (0x005D067A and 0x005D09A0..) and D2
     (0x005D0BD4). Read each one completely: logs, state writes, and whether it checks that the behaviour is active.
   - **DockingTestSimple:** the D3 handler (0x005CB35C..0x005CB382), and its constructor gate inputs (ctx+0x20 and its
     +0x60, at 0x005CB18C/0x005CB190): what those objects are, and whether they are non-null in a normal engine.
   - **LiftLoadTest:** its DA handling through the raw IBehavior overload. That is virtual +0x60, then +0x6C or +0x78
     chosen by +0xA1 (0x005BF2F8..0x005BF320). Find LiftLoadTest's own targets for those slots, and read them.
   - For each one, say what is observable while the behaviour is not running: logs, state that a later run would read,
     and messages sent.
