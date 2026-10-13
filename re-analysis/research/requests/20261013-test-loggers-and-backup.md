# Research request R-LOGGERS-BACKUP (M7-028, M15-033)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-unported-message-handlers-check.md` (its "Constructor side effects" section),
  `research/20261013-robot-message-subscribers-check.md`, and the M7-028 and M15-033 records in
  `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-test-loggers-and-backup.md`.
- **Format:** one coverage table per item, first. Each behaviour-changing step gets an address, or UNKNOWN.
- **Tooling:** use a disassembler that skips undecodable data (capstone with skipdata).

## Items

1. **RollingFileLogger.** Its constructor (0x0080DEFC..0x0080E050) is called by DockingTestSimple (0x005CB04E..0x005CB0A8)
   and LiftLoadTest (from 0x005D3F32), with pathToResource(scope 2, "dockingTest" / "liftLoadTest"),
   kDefaultFileExtension and 0x1400000. Give:
   - the full path that pathToResource builds for scope 2;
   - what FileUtils::CreateDirectory (0x0080DFCC) does when the directory already exists, and when it fails;
   - whether the constructor opens or creates any file, or writes anything;
   - what kDefaultFileExtension is;
   - what the destructor does.

   Each behaviour is constructed twice.
2. **LiftLoadTest's constructor,** the rest of it (0x005D3F48..0x005D401A): anything observable besides the logger and
   the DA subscription.
3. **FactoryTestLogger** (constructor 0x00671658, called from FactoryTest's constructor at 0x005D051A with a bool): what
   it creates, opens or writes at construction, and what its bool argument is.
4. **The NVStorage backup manager on robot connection.** It subscribes to tag C9 in the NVStorage constructor
   (0x00519B78; gated on HasExternalInterface at 0x00519B34). Read RobotConnected (0x0051A010) completely: logs, state,
   any NV reads or writes it starts, messages sent, and timing. Say whether the stack's existing NV storage code
   (cozmo-stack/src/Cozmo.Robot/NvStorage.cs) already does any of it.
