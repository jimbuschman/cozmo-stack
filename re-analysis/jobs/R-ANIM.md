# Job R-ANIM: close the remaining gaps in the animation and derived-state layers

**Agent:** opencode, as cozmo-manager. **Type:** round 3 (triage, gap extraction, build). Follow [R.md](R.md).
**Needs:** nothing. It runs after R-DEV in window 3's chain, so the two never edit the robot code at once.

**Subsystems:** M5-animation, M10-derived. Every IMPLEMENTATION_GAP record of these, as the manifest has it when the job starts.

**Notes:** Several M5 gaps are the audio animation, the M6 stand-in for RobotAudioClient's RobotAudioAnimation. Check B-M6b-3's status: if the live audio path has landed, wire it. Otherwise the record stays open, naming B-M6b-3. M10's carrying and pose gaps wait on M11/M12 pieces that now exist.
