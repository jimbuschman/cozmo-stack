# OKAO-RUNTIME-1: Claude check (manager, 2026-11-04)

**Built by the manager directly. The helper agents were out of usage, and the operator asked the manager to take the jobs better
done in-session. No record is settled or tagged. There is no production caller: the stack's `IFaceDetector` seam (Faces.cs,
`OkaoFaceDetector`, still empty) is not wired.**

## What it is

- **`Emulation/OkaoFaceDetection.cs`** runs the shipped OKAO detection, statically linked in libcozmoEngine.so, on the embedded
  runtime from EMU-RUNTIME-1, with FaceTracker::Impl::Init's settings:
  - video mode DT_CreateHandle(co, 5, 10);
  - MV delay 1, search cycle (2, 2, 5), direction mask 0, pose extension (1, 1), accuracy 0;
  - angle (1, 0x803803), size range 48..640, threshold 500.

  Addresses are in `re-analysis/tools/emu/emu_okao.py`. Per frame it calls Detect_GRAY, GetResultCount and GetRawResultInfo, and
  exposes the raw words with the fields the engine reads named.
- **The order of the setup calls is the probe's.** The engine's own ctor order (0x0086B2A8) is not claimed: it is decompiler-only
  and must be read before wiring.
- **Faults:** after an emulation error the instance is faulted.
- **Oracle:** `re-analysis/tools/emu/emu_okao.py` and `gen_okao_fixtures.py`. Pure-Python drawn faces only; no photograph of anyone
  is in the repo.

## Proof

`NativeOkaoOracleTests` feeds 19 frames in order through one detector and compares every return, count and raw result word.
The frames are faceless, then drawn faces at three scales and positions, then faceless again.

- **Bit-exact:** 19 of 19 frames match the oracle, including video-mode acquisition (a face is found on the 2nd frame), the hold
  (a face kept 2 frames after it is gone) and the loss.
- **Memory:** the oracle is identical under heap/stack poison 0x00 and 0xFF.
- **Speed:** about 21 ms per 320x240 frame in C# (the Python probe took 60..160 ms).

Earlier, by hand: the Python probe found the operator's face in a phone photo at three sizes (session note, 2026-11-03), and parts
and recognition ran.

## Before wiring (next job)

1. Read FaceTracker::Impl's ctor (0x0086B2A8) and Update's use of the results instruction by instruction:
   - the shuffle RandomGenerator(seed 1);
   - CenterToSquare;
   - the +0x2C 0x100 test;
   - PT_DetectPoint and FaceDirection;
   - EX/SM/GB;
   - FaceRecognizer::Init 0x00862A18 and ExtractFeatures 0x00865034, asynchronous in the shipped config.
2. Wire `IFaceDetector` with the M14-010 and M14-011 records, then settle them in a check.
3. Find out why the PT confidences read 0.

## Progress

22 records settled, 2 of 178 finished (unchanged).
