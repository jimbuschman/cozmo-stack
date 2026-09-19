# Handoff — 2026-09-19

## Where things stand

| | |
| --- | --- |
| Clean HEAD | `d562f85dc1db5389896d303653f3a8f4ea49ec02` |
| Renderer reconstruction | `bf2ddc2675b60847a5a8f732a1ccbb8ada76a7e1` |
| Tests | **392 passing**, 0 failing, 0 skipped |
| Working tree | clean, pushed |
| Next task | **Source Fidelity Sweep** — not feature development |
| M9 | **NOT STARTED** |

## Milestone status

| Milestone | Status |
| --- | --- |
| M1 transport core | Frozen baseline. No status line of its own in `TRANSPORT_SPEC.md`; treated as frozen by every layer above it, and exercised on hardware by all of M3–M7. |
| M2 protocol catalogue | Frozen baseline, same as M1. `PROTOCOL_STATUS.md` is generated, never hand-edited. |
| M3 device layer | COMPLETE and FROZEN — camera, display and audio verified on hardware 2026-09-18 |
| M4 control layer | COMPLETE and FROZEN 2026-09-18 — sensors, lights, head, lift and wheel drive verified. **Cube acceptance not run**: no cube available. Deferred, not failed. |
| M5 animation and expression | COMPLETE and FROZEN 2026-09-18; procedural-face erratum **closed** 2026-09-19 |
| M6 Wwise audio | COMPLETE and FROZEN 2026-09-18 |
| M7 reactive behaviour and idle | **COMPLETE — HARDWARE VERIFIED — FROZEN** as of 2026-09-19 |
| M8 behaviour inventory and framework | Complete offline. Carries no status line and has no hardware surface of its own; its output feeds `NEXT_MILESTONE.md`. |
| M9 Wwise switch-state audio | **NOT STARTED** |

## What closed this session

### The procedural face renderer is now a port, not an interpretation

`bf2ddc2`. The renderer's geometry was invented — 28x28 eyes at x = 40 and 88 on a 128x32 canvas, with the
source comment admitting they were "chosen so a neutral face fills the panel sensibly". An earlier round had
corrected how the whole-face parameters compose, which was necessary but did nothing about the numbers being
wrong, so hardware still showed a materially wrong face.

`ProceduralFaceDrawer` was disassembled and ported. Every constant with its address is in
[PROCEDURAL_FACE.md](PROCEDURAL_FACE.md); the summary:

| | recovered |
| --- | --- |
| canvas | 128 x 64 — `Image(0x40, 0x80)` in `DrawFace` @ 0x00585B30 |
| eye base centres | x = 32 and 96, from the table at 0x005859DC — **64 px apart, not 48** |
| nominal eye | 30 x 40, half-extents 15 and 20 |
| corners | four `cv::ellipse2Poly` arcs at 10 degree steps; radius under 1 px means a sharp corner |
| lids | quads overshooting the box by a pixel, plus optional bend arcs |
| per-eye transform | `EyeAngle`/`EyeScaleX/Y` about the eye's own origin, then translate |
| left/right | `if (whichEye != 0) x = -x` — one authored eye, mirrored |
| rounding | `roundf`, half **away from zero**, not .NET's half-to-even |

The **(64,32) versus 128x32** question resolved as interlacing rather than a half-height canvas: `DrawFace`
blanks alternate rows after `warpAffine`, parity from `_firstScanLine`, which `InitStream` toggles per
animation. `CompressRLE` still asserts and encodes 64 rows. **The verified 32-row wire codec was not
touched**; the renderer keeps every other canvas row instead.

**Hardware retest passed** — `anim_reacttocliff_pickup_01`, the clip holding the most extreme squash and
stretch in the library. The eyes stay distinct throughout, and the resting face is correct.

### M7 is frozen

Its first hardware run failed with the eyes growing and merging. That was **two independent faults
presenting as one symptom**: idle accumulating state in M7, and the renderer's geometry in M5. Both are
fixed and re-verified. The M7 write-up's original claim that "nothing in this failure implicates the
renderer" was wrong and is corrected in place in `BEHAVIOR_LAYER.md` rather than deleted.

## Still open in the renderer — three named unknowns

These are recorded as unknowns rather than filled in with plausible values. None is a defect; each is a
place where the binary was not made to give up an answer.

1. **Exact corner-radius parameter assignment.** The four arcs, their sweep directions, their centres and
   the parameter block offsets are all certain. What is *not* traced instruction by instruction is which of
   the eight radius parameters feeds which of the four corners. The mapping in use — upper/lower x
   inner/outer, inner towards the nose — is self-consistent with the established mirror, but the register
   plumbing between the radius loads and the `ellipse2Poly` calls is interleaved and was not followed to a
   definite conclusion.

2. **Polygon fill rule.** No `fillPoly` appears in the imports of either `DrawFace` or `DrawEye`, so the
   fill is inlined or lives in a helper that was not located. An even-odd scanline fill is used. For the
   simple closed outlines these polygons form, even-odd and non-zero winding agree, so this is unlikely to
   be visible — but it is not established.

3. **Scanline parity.** The engine blanks alternate rows of its 64-row canvas and still encodes all 64;
   our verified codec carries 32, so the renderer keeps every other row. *Which* parity to keep is the open
   question. `_firstScanLine` is held at 0 rather than alternated, on the reading that the toggle is
   burn-in protection — it sits alongside `GetMaxBlinkSpacingTimeForScreenProtection_ms` — rather than
   geometry.

Closing 1 and 2 needs a finer trace of `DrawEye`. Closing 3 needs either a capture of the stock app's
frames or a hardware experiment comparing both parities.

## Next task: Source Fidelity Sweep

**Not feature development. Not M9.**

The reason is this session. The renderer shipped as "ours, an interpretation" and stayed that way through
two hardware failures, because nothing forced the question of whether the binary could simply answer it.
It could — every constant was recoverable. The same shape of problem is likely elsewhere: values that were
chosen because they looked right, sitting behind a comment that admits it, in code that passes its tests
because the tests were written from the same assumption.

Two specific warnings from this session, both worth carrying into the sweep:

* **`FaceTransformTests` passed identically under the invented geometry and the real geometry.** It only
  asserted that two separate blobs appeared. A test written from the same assumption as the code cannot
  falsify that assumption. Prefer assertions against recovered constants.
* **Fixing a mechanism is not the same as fixing the values it operates on.** The whole-face transform was
  correct after the first round and the face was still wrong.

Deferred items that are *not* part of the sweep and should stay deferred: cube hardware acceptance, enhanced
backpack-light keyframes, pre-rendered `faceAnimations`, group cooldown enforcement, lift 0 mm semantics,
the seven stereo ADPCM music files, and the unresolved music-hierarchy events.

## Hardware commands, for reference

```
dotnet run --project src/Cozmo.Conformance -- anim 172.31.1.1 --assets <dir> --name anim_reacttocliff_pickup_01 --wwise <obb dir>
dotnet run --project src/Cozmo.Conformance -- face-expressions --offline
dotnet run --project src/Cozmo.Conformance -- cubes 172.31.1.1 --acceptance
```

`face-expressions --offline` prints the rendered art with no robot, which is how the reconstruction was
eyeballed during the work.
