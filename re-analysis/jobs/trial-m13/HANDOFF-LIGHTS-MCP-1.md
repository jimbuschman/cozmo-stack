# LIGHTS-MCP-1 handoff

`set-backpack-lights` and `set-cube-lights` take RGB, on/off periods, on/off transitions and an LED mask. The cube tool takes the current connected world object ID. Each MCP client keeps the last values it requested for the other LEDs, like the app. The reply names the game message, all LED values and whether the request went to the backpack light layer or the robot send queue.

| Path | Source to verify |
| --- | --- |
| Backpack | Unity `Robot.cs:407–412,1848–1891,1938–1958` and `LEDId.cs` keep and send left/front/middle/back/right. G2E 0xBA copies 140 bytes at 0x006323C4–0x00632454 and starts source 2 at 0x0063244E. Update chooses from sources 1, 0 and 2 at 0x00631D54–0x00631DDE. 0x00631F3A–0x006321BE packs E2R 0x03 for slots 1–3 and 0x11 for slots 0 and 4. |
| Cube | Unity `ObservableObject.cs:17–26,249–256,351–367` and `Robot.cs:1938–1945` send all four slot patterns. G2E 0xB9 copies the 129-byte fields at 0x0063A03C–0x0063A0DA and dispatches at 0x0063A0D4. The ordinary mode resolves the connected world ID at 0x00637E96 and applies LEDs at 0x00637EDA. 0x004E48B0–0x004E49A2 normalizes periods. 0x0063A654–0x0063A800 handles gamma and sends E2R 0x10 then 0x04 to the radio slot. 0x0063A894–0x0063A98E applies white balance. |

The usual RGB sender supplies zero offsets and, for cubes, zero rotation and relative mode. The tools use those fields. Backpack submission uses the native source priority. Cube submission uses the native direct setter and leaves the current Freeplay animation on its light layer, where a later update can replace the output. The wire tests check both paths, whole-message slot retention, argument errors and disconnected-cube rejection. Fidelity statuses are unchanged.

For the one-minute operator check, connect Cozmo and one cube on a clear desk. Run `python re-analysis/acceptance/hardware/scripts/lights-mcp-hw.py --mcp <cozmo-mcp.dll> --resources <cozmo_resources>` and watch the front and right backpack LEDs, then cube LEDs 0 and 2. The script checks the captured 0x03, 0x11 and slot-selected 0x04 packets against the engine values. LED appearance is an operator observation. Add `--validate` for the no-robot check, which passed locally. Hardware acceptance is pending.

Build `9fd4a9f6` passed all 8,803 Debug tests in 56.64 minutes with the recorded replay fixtures enabled. All 81 focused light, bridge catalogue and MCP tests passed. `fidelity.py --check` passed all 764 records.
