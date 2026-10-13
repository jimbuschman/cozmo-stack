# Research request R-DISPATCH-REST (M2-020)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-robot-message-subscribers-check.md`, and the M2-020 record in
  `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-dispatch-residue.md`.
- **Format:** one coverage table per item, first. Each step gets an address, or UNKNOWN.

## Items

1. **Robot ID 0x1CF.** Every caller of the robot-message Broadcast/emitter (0x0069DE18) that passes robot ID 0x1CF, the
   all-signals branch (0x0069DE32..0x0069DE4E). When does it happen?
2. **The early-unsubscribe paths.** For each of the 35 subscribers (the subscriber check's table), where is its scoped
   handle released (a destructor, a reset, or an explicit unsubscribe)? Does that happen during an emit?
3. **Owner or signal destruction during an emit:** what happens if a handler destroys the object that owns another
   subscriber, or the signal itself, mid-emit (0x0069E274..0x0069E2B2, 0x0051D338..0x0051D3A4)?
4. **Load before connect.** Does LoadNonConfigData (and so LoadBehaviors 0x005206BC) always finish before AddRobot can
   build a Robot? Trace the engine loading state and the ConnectToRobot gate.
5. **Other readers of RobotToEngineImplMessaging +0x0C..+0x39** (the firmware-version copy and the mismatch flags).
