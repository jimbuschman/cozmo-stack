# Job R-DEV: close the remaining gaps in the device and control layers

**Agent:** opencode, as cozmo-manager. **Type:** round 3 (triage, gap extraction, build). Follow [R.md](R.md).
**Needs:** nothing. It runs after G-M14 in window 3's chain.

**Subsystems:** M1-transport, M2-protocol, M3-device, M4-control. Every IMPLEMENTATION_GAP record of these, as the manifest has it when the job starts.

**Notes:** Most M4 gaps wait on the carried object (M12's CarryingComponent), which B-M12 built. M3-032..M3-034 are the NV connection queue: the engine state machine (M1), the face album (M11/M14), backup (M12), lab, and needs (M15; see G-M15's status for the mfgId -> needs edge).

**Pre-extraction:** `re-analysis/research/20260929-R-DEV-pre-extraction.md` (request `requests/20260929-R-DEV-pre-extraction.md`) is being written in parallel. Before a gap pass, `git pull` and check whether it's there. If it is, have `@cozmo-verifier` check its rows first, and send only what it leaves open to `@cozmo-extractor`.

**The pre-extraction is in** (Sonnet, all 12 items). The manager spot-checked:
- M3-021 4h/4i: `IsInitialized` reads VisionSystem+0x58, which only the end of a successful Init sets (0x006B2CD6, 0x006B141A);
- M2-002 12b: bit 0x4 is stored at DockingComponent+4 (0x00512A98..0x00512AA0);
- libopencv_imgcodecs carries libjpeg 9 ("Copyright (C) 2013, Thomas G. Lane, Guido Vollbeding").

All hold. Behavioural contradictions to fix, not cleanup:
- M3-001: the raw cases don't check the payload length;
- M3-021: the config load can fail, so IsInitialized is not always true;
- M4-008: the store happens before the origin check;
- M4-010: +0x490 is 0 for the life of the Robot, so neither ObjectAvailable nor ObjectUnavailable is broadcast;
- M4-018: the pop resends the lower animation's pattern, and `+0x41` has writers.

**The JPEG codec (M3-001/M3-018):** the engine decodes through the libjpeg 9 inside `libopencv_imgcodecs.so` (the ISLOW IDCT, fancy upsampling for colour). The stack's StbImageSharp is not that. Exact means porting those routines. Verify them against the library's own code under the emulator (`re-analysis/tools/emu/`, the way the Vorbis IMDCT was done). The grey path (the Y channel only, no upsampling) is the one the shipped camera uses: do it first.
