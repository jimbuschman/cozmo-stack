| Item / native entry | Coverage | Result |
|---|---|---|
| M1-029 shipped real-number converter | CHECKED | Existing shipped-converter fixtures and generator retained; this batch does not replace them. |
| M2-003 lift angle→height, 0x00516F9C | PARTIAL | 19 explicit edge inputs stop at the unsupported phone `sinf` import. No expected value fabricated. |
| M2-003 lift height→angle, 0x005170B0 | PARTIAL | 19 edge inputs stop at phone `asinf`. |
| M3-010 mu-law, 0x00597AD8 | PARTIAL | 19 edge inputs stop at phone `isnanf`; the short C# encoder is never the oracle. |
| M4-001 radians rescale, 0x0084C87C | PARTIAL | 1,047 successful bit comparisons; 1,020 inputs stop at phone `ceilf`. |
| M4-001 IsNear, 0x0084CC0A | PARTIAL | 264 successful comparisons; 760 triples stop at `ceilf`. |
| M4 light packing, 0x004FABDC..0x004FAC10 | PARTIAL | 2,053 native instruction-fragment outputs match; surrounding loader/warning/message behavior is not tested by this fragment. |
| M5-002 ProceduralFace constructor, 0x00583660 | CHECKED | All 43 numeric fields match poisoned-memory construction; native distorter ownership word is excluded from numeric comparison. |
| M5 eye clipping, 0x005847A8 | PARTIAL | 665 parameter/input cases stop at `isnanf`. Guard/allocator setup executes, including the shipped no-warning callback installation. |
| M5 procedural-face interpolation, 0x00584290 | PARTIAL | Three default-pose endpoint cases match; 18 cases stop at `isnanf`. Nondefault pose corpus is MISSING. |
| Shared procedural-face eye box, 0x00584568 | PARTIAL | 258 native cases; two controlled NaN-selection regressions pass after selection fix. Thirteen remaining differing output words are manager DEFECTs. |
| Shared GenerateEyeShift primitive, 0x0058CFC4 | PARTIAL | 19 edge replays stop at `isnanf`; no higher-layer caller traced. Map overload's initialized map/input bridge is MISSING. |
| Shared blink primitive, 0x00585F18 | PARTIAL | Native static initialization reaches `isnanf`; table replay is MISSING. |
| M5 shared mt19937, 0x0082F860 / 0x0082FAC8 | CHECKED | Five seeds, 1,300 consecutive draws each; shipped initialization and draw instructions, including multiple state wraps. |
| M5 GetNextDbl, 0x0082F9B0 | CHECKED | Five seeds, 650 binary64 outputs each; two shipped mt19937 draws per output; no Python RNG implements expected draws. |
| M5 OpenCV ClipLine, imgproc 0x000410DC | CHECKED | 512 complete native calls compare return and both points; division executes the shipped STLport helper at 0x0004EFC0. |
| M3 image RGB→gray, 0x008728D8 | PARTIAL | Replay stops at shipped OpenCV `cv::Mat::create`; cross-library linking/initialized image bridge is MISSING. No Python grayscale model used. |
| Remaining M1–M5 numeric/transform census, renderer/image kernels, head clips, keyframe transforms and RNG range wrappers | PARTIAL | Not exhaustively enumerated or oracle-tested in this batch. Their fixtures/production-entry adapters are MISSING; this report must not be read as coverage of every engine transform. |

Queue 4 Q10, after the required pull at 336aadc. **Partial oracle batch, not complete numeric coverage.**
The queue authorizes emulator-backed production/test/tool changes and own commits, overriding the ordinary research write restrictions. No record is settled and no hardware is run.

## Reproduction and limits

Generator: `tools/emu/emu_m1_m5_numeric.py`; fixture:
`cozmo-stack/tests/Cozmo.Protocol.Tests/Fixtures/m1_m5_numeric_oracle.json`.
Inputs, unsupported calls, generator seed and both artifact hashes are in the fixture.
Engine SHA256: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
The initial Q10 commit's complete native rows total 4,148, including 6,500 mt19937 words and 3,250 double words within their sequence rows. Q11 later adds two explicit color rows, taking this fixture to 4,150.
The blocked-input list contains 2,541 actual failed calls. These are not comparison passes.

Unicorn executes the mapped ELF instructions and resolved engine symbols. The actual shipped `libstlport_shared.so` is additionally mapped/relocated for its `__aeabi_ldivmod` compiler helper; its hash is recorded. Supported external services are memory copying/clearing, successful allocation/deletion, serial C++ guards and exit-callback registration. There is no engine-method substitution and no phone math/division substitution. FPU is enabled, FPSCR starts at zero for each call. Phone entropy is outside this batch: seeds are explicitly nonzero (1, 2, 5489, FFFFFFFF, 80000000). Seed-zero behavior is not claimed tested.

The scalar corpus includes positive/negative zero, infinities, quiet/signaling NaNs, smallest subnormals, largest finite values, both neighbors of ±π and the 10-radian shortcut boundary, plus deterministic random words. Not every edge is executable: the blocked list preserves that distinction.

RGB555 tests execute the actual instruction fragment and stop before 0x004FAC10. Each of four channels walks all 256 byte values, followed by 1,024 random RGBA inputs and five explicit primary/zero colors. This proves the arithmetic fragment, not the complete load/send function. ClipLine uses the build's indirect `Size_` ABI and separate point objects. An initial incorrect direct-Size bridge was discarded and regenerated before comparisons; its outputs are not fixtures or findings.

`NativeNumericOracleTests` drives the existing C# helpers. The broad eye-box corpus is diagnostic, explicitly not a passing regression claim; only its two selected cases gate the selection change. The other complete rows are bit comparisons. The three interpolation rows only compare default poses. Existing M1-029 fixtures remain generated by `emu_jsoncpp_double.py`, not by the C# implementation.

## Current records before the shared-face finding

> M5-002 — "19 procedural-eye parameters and the ProceduralFace default (all 0 except EyeScaleX/Y 1, face scale 1, no distorter); SetFromFlatBuf rules"; EXACT_SOURCE; evidence: "C6 SetFromFlatBuf 0x005838D0..0x00583AAC; SetFacePosition 0x00583B20..0x00583BF8", "gap3 K4 ProceduralFace() 0x00583660..0x005836A0 (table 0x00C5A970 = {2,3})".

> M7-006 — "Eye shift moves the whole face through LookAt with the engine bounds"; EXACT_SOURCE; evidence: "FaceLayerManager::GenerateEyeShift 0x0058d100", "ProceduralFace::LookAt 0x00584158".

The tested routine is the shared ProceduralFace numeric helper. No M7 caller, strategy or tick was followed. Constructor equality does not certify SetFromFlatBuf or every consumer. M7 record text is quoted only to expose the ownership boundary; its state is unchanged.

The two shipped eye-box outputs with a right-eye quiet NaN show that the left term is retained. `MathF.Min/Max` propagated the NaN instead. The C# helper now uses ordered comparisons retaining the left term. Expected values for those regression inputs are native fixture rows, not an independently constructed C# pose result.

**Manager DEFECT:** after that selection change, the broader corpus still has thirteen differing output words: ten NaN sign/payload differences and three finite-versus-infinity differences. Exact input/output words are in `20261007-eye-box-nan-differences.json` (despite its name, it includes all thirteen). Examples: native `7FC00000` versus C# `FFC00000`; native `7FE00001` versus C# `7FC00000`; native `FF800000` versus C# `40F63474`; native `7F800000` versus C# `558FBD05`. Entry 0x00584568, C# `ProceduralFacePose.GetEyeBoundingBox`. Reconstructing the arithmetic association/NaN operations needs a logic review, so these differences are not fixed in this queue. Do not infer that the whole helper now matches.

## Self-review and batch status

CHECKLIST review: existing helpers used; no duplicate production implementation; fixtures execute shipped instructions, have input/artifact provenance and fail on unsupported imports; width is binary32 or binary64 as specified; no manifest/status upgrade. Phone imports, diagnostic cases, nondefault interpolation, entropy and the incomplete census remain explicit MISSING items.

Validation and commit are recorded in `jobs/status/CODEX-Q4.md`. Q11–Q13 must retain these limitations; no subsequent task turns this partial census into exhaustive coverage.
