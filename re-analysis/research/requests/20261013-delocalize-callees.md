# Research request R-DELOC-CALLEES (M11-070)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-delocalize-and-update-object-origin.md` (its "Unread" section), and the M11-070,
  M11-069, M11-044 and M11-007 records in `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-delocalize-callees.md`.
- **Format:** one coverage table per item, first. Each behaviour-changing step gets an address, or UNKNOWN.
- **Tooling:** use a disassembler that skips undecodable data (capstone with skipdata).

## Items

1. **BlockWorld::AnyRemainingLocalizableObjects(uint originId)** (0x00626EF4). Which objects count, what is filtered
   (family, type, pose state, the carried object, the active state), the iteration order, any early exit, and the
   return value. This decides which old-origin buckets survive a delocalize (DeleteObjectsFromZombieOrigins,
   0x00620956..0x0062095E).
2. **Robot::SetPose** (0x0051502C) and **Robot::AddVisionOnlyStateToHistory** (0x0051513C), as called from SetNewPose
   (0x005126EC..0x00512722) during Delocalize: every write, log and return value. Say which parts the M11-044 record
   already covers.
3. **ObjectPoseConfirmer AddInExistingPose's emplace** (PLT 0x004A6FE8, from 0x00506F48..0x00506F80): the default fields
   of a new entry.
4. **The IExternalInterface vt+0x18** that the RobotDelocalized broadcast calls (0x00510D94): what it does with the
   message.
5. **VizManager::SetText's colour argument** (GOT 0x0103E7DC, 0x00510BF8..0x00510C46). Also say whether VizManager sends
   anything outside a developer visualiser connection (the transport and the gate), so the manager can decide its
   scope.
