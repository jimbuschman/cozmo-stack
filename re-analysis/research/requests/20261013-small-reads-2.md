# Research request R-SMALL-2 (M15-044, M2-022)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-backup-unlock-and-stoull-check.md` and `research/20261013-dispatch-residue-check.md`,
  and the M15-044 and M2-022 records in `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-small-reads-2.md`.
- **Format:** one coverage table per item, first. Each step gets an address, or UNKNOWN.

## Items

1. **The UnlockId name → value table.** Decode UnlockIdFromString's static map builder (0x00794D84..0x007957AC) into
   name/value pairs, and cross-check them against Unity UnlockId.cs. Also read DataPlatform::readAsJson's failure path
   (PLT 0x004A64E4): what root it leaves after a failed read.
2. **The robot-message handlers that might destroy another subscriber mid-emit.** Read the C4 handlers (Turn, Head,
   Lift), the IDock C5 and DA handlers, the PlayAnimationAction CA, CB and D5 handlers, and the FirmwareUpdater EF
   handler. For each, say whether it can synchronously destroy another subscriber of the same tag, or the owner of one.
   Also answer:
   - whether IActionRunner calls Init again on a live action;
   - where the ConnectToRobot game-message subscription is registered (0x004EE94A → PLT 0x004A544C);
   - whether ~Robot's r5 at 0x0051120E is the BlockTapFilter;
   - what ProcessMessages does with no robot (0x0069D8D8).
