# Job R-BEH: close the remaining gaps in the behaviour layers

**Agent:** opencode, as cozmo-manager. **Type:** round 3 (triage, gap extraction, build). Follow [R.md](R.md).
**Needs:** G-M15 DONE. If it isn't, set `WAITING G-M15` and end.

**Subsystems:** M7-behaviour, M8-framework, M9-wwise-music, M15-freeplay. Every IMPLEMENTATION_GAP record of these, as the manifest has it when the job starts.

**Notes:** M9-011, M9-027 and M9-028 need the live audio chain (B-M6b-3). If it hasn't landed, leave them open, naming it. M9-013/014/024/025 are RECOVERABLE_GAP; Codex is extracting them (`re-analysis/research/requests/20260929-M9-singing-gaps.md`). If its answer files are there, fold them in as G-M7 did for M7-021.
