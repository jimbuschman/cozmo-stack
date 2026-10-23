# Research request R-M12-CHARGER-PREACTION (M12-001, M13-009, M13-040)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Feeds:** a builder job that fills the charger's stored pre-action cache, replacing the B-M13-17 MISSING
  "GetCurrentPreActionPoses virtual generator 0x004DF8BA -> Charger.GeneratePreActionPoses 0x004E9FB0"
  (`research/20261023-b-m13-17-claude-check.md`).
- **Context:** the current `unresolved` of M12-001, M13-009 and M13-040 in `re-analysis/fidelity_manifest.json`;
  `research/20261022-m13-017-groundwork.md` rows L1-L2; `research/20261021-m12-predock-cache-lifetime.md`. In the
  stack: `Manipulation/PreActionPose.cs` (GenerateStored) and `Manipulation/ChargerActions.cs` / ChargerGeometry.
  Run `python re-analysis/tools/known.py <address|name>` before tracing anything.
- **Answer file:** `research/20261023-m12-charger-preaction.md`.
- **Format:** one coverage table first: item | CHECKED, PARTIAL or NOT DONE | what the original does | address with
  the 1-3 instructions | record. Every step gets an address with pasted instructions, or UNKNOWN with what you tried.

## Items

1. **Charger::GeneratePreActionPoses** (0x004E9FB0, 374 bytes): for each pre-action type it handles, how many elements
   it pushes into which of the six cache vectors, and in what order.
2. **Each element's construction:** which PreActionPose constructor it calls (0x0050DB14 or another), and every
   argument: the action type, the marker or pose it is built from, the offset pose (translation, rotation, parent),
   any distance or tolerance scalar, all as exact literals or the instructions that compute them.
3. **Compare with the stack's ChargerGeometry** pre-dock pose (M13-009): say which values already match, citing both,
   and which differ.
4. **Narrow check first:** if an earlier checked answer covers any of this (M13-009's docked and pre-dock poses, the
   cube generator's shared constructor rows in M12-001), cite it and don't re-extract.

Thumb rule: symbol values have bit 0 set; take function boundaries only from `known.py` or the decomp index.
Extraction only: no code, no manifest edit.
