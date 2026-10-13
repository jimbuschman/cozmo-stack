# Research request R-PLAY-AREA (M11-064)

- **Binary:** libcozmoEngine.so 3.4.0-1204.
- **Context:** `research/20261013-blockworld-flag-and-map-helper.md` (Q1 rows 3 and 7), and the M11-064 record in
  `re-analysis/fidelity_manifest.json`.
- **Answer file:** `research/20261013-play-area-and-localize-failures.md`.
- **Format:** one coverage table per item, first. Each behaviour-changing step gets an address, or UNKNOWN.
- **Tooling:** a disassembler that skips undecodable data (for example capstone with skipdata). A linear sweep that
  stops at the first bad halfword misses code.

## Items

1. **The play-area value.** UpdateObservedMarkers emits `robot.play_area_size` "%.2f" every 60 s (0x00624F16..0x00624F32)
   from the current map helper's vt+0x1C.
   - Find the MemoryMap vtable slot +0x1C target (the MemoryMap vtable is 0x0103095C) and read its body completely: what
     area it measures, which node content types it counts, the units, and the traversal order.
   - Include any QuadTree or processor helpers it calls, and any cached value it reads, with who updates that cache.
2. **The LocalizeToObject failure returns** (0x005154B0..0x00515AA4). List every path that returns non-zero: its
   condition, its log (key, format, address), and its return value.
   - For each one, say whether it can happen in normal engine running with a robot connected (not SDK mode), given its
     callers. PotentialObjectsForLocalizingTo::LocalizeRobot reaches it at 0x0050D4D2..0x0050D4D6; read that function's
     own failure logs too (LocalizeFailure 0x0050D54C, CrossFrameLocalizeFailure 0x00BE6450).
   - If any is reachable: BlockWorld+0x74 stays at 1 until the next UpdateObservedMarkers. Say what then happens on the
     next frame (the list is cleared at 0x00624F78 without being consumed).
3. **The dispatch to HandleCliffEvent.** How RobotInterface::MessageHandler::ProcessMessages (0x0069D85C) gets from a
   received robot message to RobotToEngineImplMessaging::HandleCliffEvent. Give the tag test, the lookup or switch, and
   the order relative to other handlers in the same tick.
