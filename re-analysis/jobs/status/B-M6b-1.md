DONE 2026-09-28 (manager) the Vorbis decode's value stages are fixed: the IMDCT and the window combine are bit-exact with the engine's own code

- IMDCT 0x00AB4E34: five transliteration defects fixed; bit-identical for 256..8192 (WwiseVorbisImdctNativeTests).
- Window combine 0x00AB5A94: rebuilt as the engine's float mdct_unroll_lap; bit-identical for every long/short combination (WwiseVorbisCombineNativeTests). The overlap saves are memcpy only.
- End to end: shipped mono media correlate 0.9987..0.99999 with NVorbis (was 0.002).
- Left in M6-002's unresolved: whole-decode bit-exactness against an emulated native decode, the stereo end-to-end check, the LFE channel reorder (unreachable for mono/stereo).
- Tools: re-analysis/tools/emu/ (emu_imdct.py, emu_combine.py).
