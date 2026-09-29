# Job R-ANIM: close the remaining gaps in the animation and derived-state layers

**Agent:** opencode, as cozmo-manager. **Type:** round 3 (triage, gap extraction, build). Follow [R.md](R.md).
**Needs:** nothing. It runs in window 2, alongside R-DEV in window 3. Both touch the robot hub files (for example `CozmoRobot`, `EngineRobot`): keep your edits there to the wiring call sites, `git pull --rebase` before each batch, and resolve a conflict by keeping both sides' changes, then rerun the suite.

**Subsystems:** M5-animation, M10-derived. Every IMPLEMENTATION_GAP record of these, as the manifest has it when the job starts.

**Notes:** Several M5 gaps are the audio animation, the M6 stand-in for RobotAudioClient's RobotAudioAnimation. Check B-M6b-3's status: if the live audio path has landed, wire it. Otherwise the record stays open, naming B-M6b-3. M10's carrying and pose gaps wait on M11/M12 pieces that now exist.

**Pre-extraction:** `re-analysis/research/20260929-R-ANIM-pre-extraction.md` (request `requests/20260929-R-ANIM-pre-extraction.md`) answers most of this job's missing-source questions ahead of time. If it's there, have `@cozmo-verifier` check its rows first, and send only what it leaves open to `@cozmo-extractor`.

**The pre-extraction is in** (Sonnet). The manager spot-checked:
- M5-006: the radius tokens are compared case-sensitively (`std::string::compare`, then `memcmp`: 0x004FB5A0..0x004FB5B8, 0x004FCB96);
- M5-011: a missing or non-numeric Weight rejects the entry (`Json::Value::isDouble`, 0x0058C56E..0x0058C57A);
- M10-004: CompletelyUnlockAllTracks sends the track index, not the mask (`strb.w r8,[sp,#4]`, 0x0064101A);
- M5-013: the threshold is strictly above 0x80 (libopencv_core `operator>` passes CMP_GT 1 at 0x0007A7B6; `>=` passes 2 at 0x0007A760).

All hold. Several rows **contradict the current records and code**. These are behavioural fixes, not cleanup: M5-001 (the five JSON keyframe readers exist; nothing throws), M5-006, M5-011/M5-014 (no defaults; invalid entries are rejected, and the group loads the rest), M5-013 (threshold and index reset), M5-016 (NamedColors: an unknown name gives DEFAULT). For the head-angle fields of an entry without UseHeadAngle (item 4 rows 4.11/4.12), the original reads uninitialised memory. Treat it like the other undefined-stack-byte records (for example M3-019): name the stack's choice and why, in the record, and put it to the policy review. Don't invent a value silently.
