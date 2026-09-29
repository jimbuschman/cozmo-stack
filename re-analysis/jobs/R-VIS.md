# Job R-VIS: close the remaining gaps in the vision, manipulation, navigation and faces layers

**Agent:** opencode, as cozmo-manager. **Type:** round 3 (triage, gap extraction, build). Follow [R.md](R.md).
**Needs:** G-M14 DONE. If it isn't, set `WAITING G-M14` and end.

**Subsystems:** M11-vision, M12-manipulation, M13-navigation, M14-faces. Every IMPLEMENTATION_GAP record of these, as the manifest has it when the job starts.

**Notes:** it runs after R-BEH in window 1's chain. The OKAO library (the vendor face engine) is the M14 boundary: its calls are named, never traced inside.

**Pre-extraction:** `re-analysis/research/20260929-R-VIS-pre-extraction.md` (request `requests/20260929-R-VIS-pre-extraction.md`) answers most of this job's missing-source questions ahead of time. If it's there, have `@cozmo-verifier` check its rows first, and send only what it leaves open to `@cozmo-extractor`.

**The pre-extraction is in** (Sonnet, all 13 items). The manager spot-checked:
- M11-013 1.10: the ObjectID is copied from the match, else `SetID()` (0x00620C4E..0x00620C5A);
- 1.6: `InitPose(pose, 2)`, Dirty (0x00625580);
- 1.16: the 10 s warning cooldown (0x00620B1E);
- M11-017: the squared-length test at 0x0067FF5C;
- "AllowUnconnected" occurs nowhere in the engine.

All hold. **Behavioural contradictions to fix, not cleanup:**
- M11-013: there is no switch. An unconnected cube is kept after two matching sightings, with the 10 s warning. Its ObjectID comes from `ObjectID::UniqueIDCounter`, once per unique type; the stack's LIGHTCUBE1..3 => 1..3 has no source. The record becomes IMPLEMENTATION_GAP with item 1 as its evidence.
- M11-007: the engine never sets Known on the first sighting; the first sighting creates no located object.
- M11-017: the clear region is the quad from `ClampQuad` (a triangle or a two-point polygon in the short cases), not the robot-to-ends triangle. The 6.00001 compare is on the squared length.

Its open questions include the QuadTree Insert/Transform semantics (M11-017's outcome depends on them). No record owns them yet. If they're needed for M11-017's whole path, give them a record first (AGENTS.md: a settled record owns its whole production path).
