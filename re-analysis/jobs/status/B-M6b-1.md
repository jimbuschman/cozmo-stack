BLOCKED 2026-09-28 (manager) the IMDCT is now bit-exact with the engine; the remaining value defect is the window combine 0x00AB5A94 / driver 0x00AB3520

- The manager emulated the engine's own mdct_backward (0x00AB4E34) under Unicorn and compared every phase. Five transliteration defects were fixed; the C# IMDCT is now bit-identical to the engine for all block sizes 256..8192, with a regression test against engine-captured fixtures (WwiseVorbisImdctNativeTests). Tool: re-analysis/tools/emu/.
- End to end, shipped mono media now correlate about 0.6-0.8 with NVorbis (was 0.002), with no lag. The remaining difference is spread evenly over every block, which points to the window combine and overlap.
- WwiseVorbisDecode.Combine itself says its region mapping is UNKNOWN (C13) and folds the long->short negate path 0x00AB5F9C into the common code. That is not source-backed.
- Next (resume this job): emulate the engine's combine 0x00AB5A94 and driver 0x00AB3520 with re-analysis/tools/emu (same method as emu_imdct.py), compare with Combine/WindowOverlap, and fix them to bit-exact. Then re-check the end-to-end correlation (expect about 1.0) and finish the job's gates.
