# Job R-DEV: close the remaining gaps in the device and control layers

**Agent:** opencode, as cozmo-manager. **Type:** round 3 (triage, gap extraction, build). Follow [R.md](R.md).
**Needs:** nothing. It runs after G-M14 in window 3's chain.

**Subsystems:** M1-transport, M2-protocol, M3-device, M4-control. Every IMPLEMENTATION_GAP record of these, as the manifest has it when the job starts.

**Notes:** Most M4 gaps wait on the carried object (M12's CarryingComponent), which B-M12 built. M3-032..M3-034 are the NV connection queue: the engine state machine (M1), the face album (M11/M14), backup (M12), lab, and needs (M15; see G-M15's status for the mfgId -> needs edge).

**Pre-extraction:** `re-analysis/research/20260929-R-DEV-pre-extraction.md` (request `requests/20260929-R-DEV-pre-extraction.md`) is being written in parallel. Before a gap pass, `git pull` and check whether it's there. If it is, have `@cozmo-verifier` check its rows first, and send only what it leaves open to `@cozmo-extractor`.
