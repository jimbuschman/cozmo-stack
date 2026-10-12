# Request: M4-038, M4-037, M4-043 and M3-055's two inputs

- **Answer file:** `re-analysis/research/20261012-m4-cliff-and-names.md`.
- **Read first:** the current manifest records M4-037, M4-038, M4-043 and M3-055, and
  `research/20261012-m3m4-settling-2.md`.
- **Rules:**
  - research only;
  - **one coverage table per item, first**;
  - no fidelity claims;
  - nothing leaves this machine.

## 1. M4-038: CliffSensorComponent::ComputeCliffPose and BlockWorld::AddCliff

- ComputeCliffPose (0x00634980; called at 0x005355B4):
  - its inputs: the robot pose, the cliff flags, the sensor geometry constants;
  - the arithmetic, with every constant as its float bits;
  - its failure conditions and return value.
- BlockWorld::AddCliff (0x005355C0): what object it creates or updates, where it stores it, and every log and
  broadcast.
- The two HandleCliffEvent logs that use the result: "Detected at %.3f,%.3f. DetectedFlags = 0x%02X" (0x00535602), and
  ComputeCliffPoseFailed with errG (0x00535682..0x005356BC).

## 2. M4-037: pose-origin root names

- The DifferentIDs warning (0x00534042..0x005340CC) prints two root names, via FindRoot (PLT 0x4A40F0) and GetName
  (PLT 0x4A4C60).
- Read both. How does a pose origin get its name? Which names exist on the live path (the robot's world origins, a
  cube's pose)? What is printed in the case this warning fires?

## 3. M4-043: the Save.Error what() bytes

- BlockFilter::Save catches `ios_base::failure` (0x0061B21C..0x0061B2A4) and logs `what()`.
- In the shipped `resources/lib/armeabi-v7a/libc++_shared.so`, find the exact what() bytes for the failure a failed
  `ofstream` open raises with that exception mask: the `ios_base::failure` constructor, the `system_error` message
  formatting, and the iostream error category message.

## 4. M3-055: two inputs

- The caller chain of DasTransferTask::OnTransferReady (0x6A25FE). What creates a DasTransferTask, and is it
  reachable in the stack's scope?
- The `$player_id` value: sSetGlobal at 0x665FAC (an MD5 hex digest), and the RemoveRobot one at 0x52F2D8. Of what
  input is the MD5 taken, and where does that input come from: an app input or an engine value?

## Report

- Per item: the coverage table, the answer, and what stays UNKNOWN.
