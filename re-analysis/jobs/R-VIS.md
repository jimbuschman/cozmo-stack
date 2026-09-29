# Job R-VIS: close the remaining gaps in the vision, manipulation, navigation and faces layers

**Agent:** opencode, as cozmo-manager. **Type:** round 3 (triage, gap extraction, build). Follow [R.md](R.md).
**Needs:** G-M14 DONE. If it isn't, set `WAITING G-M14` and end.

**Subsystems:** M11-vision, M12-manipulation, M13-navigation, M14-faces. Every IMPLEMENTATION_GAP record of these, as the manifest has it when the job starts.

**Notes:** it runs after R-BEH in window 1's chain. The OKAO library (the vendor face engine) is the M14 boundary: its calls are named, never traced inside.

**Pre-extraction:** `re-analysis/research/20260929-R-VIS-pre-extraction.md` (request `requests/20260929-R-VIS-pre-extraction.md`) answers most of this job's missing-source questions ahead of time. If it's there, have `@cozmo-verifier` check its rows first, and send only what it leaves open to `@cozmo-extractor`.
