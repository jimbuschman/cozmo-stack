| Original finding (Q7 coordinates unless noted) | Coverage | Disposition / remaining reason |
|---|---|---|
| DeviceTests:72,73,74,75,77 — float/short mu-law twin | PARTIAL | Unchanged; real 0x00597AD8 calls stop at phone `isnanf`. No short-overload expected values adopted as native output. |
| DeviceTests:146,147,148 — audio frame data/padding encoder | PARTIAL | Unchanged for the same import; offline padding is also a host API boundary. |
| DeviceTests:144,162,163,171,205,237 — frame/rate/interval constants | PARTIAL | Constructor/stream scheduling reference capture is missing; native literals alone are not an emulator fixture under this queue. |
| AnimationGapTests:185,189,212,224 — buffer budget | CHECKED | Replaced with fields returned by complete native UpdateAmountToSend, 0x0057C6F0. |
| M3DeviceTests:828 — ToFrames used to expect Play count | PARTIAL | Unchanged; offline Play has no shipped counterpart for this duration/count policy. |
| DeviceTests:344 — RawFrameSize as expected length | CHECKED | Replaced by native noise-case output length from CompressRLE. |
| DeviceTests:436 — codec used to expect display payload | CHECKED | Input bitmap and exact expected bytes both come from the replay fixture; actual still traverses Display.Show. |
| DeviceTests:321,323,313,345,387,438 — payload/pacing bounds | PARTIAL | Unchanged; these direct-display host policies have no native oracle. |
| ControlTests:305,310 — head/lift clip constants | PARTIAL | Unchanged; native action-constructor/state bridge missing, and inverse lift reaches phone `asinf`. No copied constants supplied as expected outputs. |
| BehaviorFrameworkTests:608 — lift track mask | CHECKED | HIGHER-LAYER test excluded under the M1–M5 boundary; unchanged. |
| ControlTests:374,375,376,390,391; M4ControlTests:1128 — light packer as expected | CHECKED | Replaced by shipped RGB555 fragment fixtures with independent literal RGBA inputs. |
| FaceTests:707,708,713,736,743,755; FreeplayTests:1714,1770,1961,2240; VisionTests:767 — NV constants | CHECKED | HIGHER-LAYER callers excluded; unchanged. |
| M3DeviceTests:1428,1699,1760,1883 — lower-layer NV constants | PARTIAL | Unchanged; initialized NV queue/Robot/MessageHandler capture bridge missing. Do not replace the path with mock engine sends. |
| M3DeviceTests:2039,2044,2048 — retry limit as oracle and loop bound | PARTIAL | Unchanged; complete native retry/update state capture missing. |
| EngineAppLayerTests:1499 — calibration size alias | PARTIAL | Unchanged; native factory-read bridge missing; camera unpack caller is M11 and was not followed. |
| DeviceTests:643,648,774 — minimized-encoding tag constants | PARTIAL | Unchanged; native dispatch fixture bridge missing. |
| AnimationAssetTests:76 — pixel-vector dimensions | PARTIAL | Unchanged; native decoded pixel-vector output fixture missing. Fixture input dimensions are not promoted into emulator output. |
| AnimationAssetTests:182,183 — Eye.ParamCount alias | CHECKED | Expected length now derives from native constructor eye words. |
| M5AnimationTests:1299 — default pose constructed by same implementation | CHECKED | Expected eye array now reads native constructor words. |
| KeepAliveTests:89; M5AnimationTests:1179,1201; EngineAppLayerTests:1928 — scheduler defaults/tag | PARTIAL | Unchanged; full initialized streamer/InitStream bridge missing. A direct literal read is reserved for Q12, not invented as a Q11 execution result. |
| M5AnimationTests:2073 — NamedColors.Default as fallback oracle | CHECKED | Replaced by actual native GetByString("red") output, including native initialization and miss path. |
| M5AnimationTests:2089,2090,2091,2092,2093 — backpack encode twin | CHECKED | Replaced by real RGBA→packed instruction-fragment outputs; LED ordering assertions retained. |
| ProceduralFaceRendererTests:56,57,58,59 — canvas/center aliases | PARTIAL | Unchanged; initialized native drawer/OpenCV image bridge missing. |
| ProceduralFaceRendererTests:110,111,125,199 — eye-center aliases | PARTIAL | Unchanged for the same reason; coordinate constants are not runtime renderer fixtures. |
| Opus M1/M2 example EngineAppLayerTests:370 | PARTIAL | Historical coordinate superseded. It now belongs to arrival-order setup. Related clock/deadline tests remain partly implementation-derived; native UpdateTime requires phone `__aeabi_ul2d`, absent from all shipped-library exported definitions searched. |

Queue 4 Q11, pulled after d12ab42. **Partial replacement batch; the table explicitly leaves remaining circular assertions in place.** No record is settled. Original coordinates above identify Q7 findings, not guaranteed post-edit line numbers.

## Native fixtures and regression entries

`tools/emu/emu_q11_transforms.py` writes `Fixtures/q11_transforms_oracle.json`:
30 complete UpdateAmountToSend calls (0x0057C6F0..0x0057C7B2), five complete CompressRLE calls (0x00581904..0x00581BAC), and six complete NamedColors::GetByString calls (0x0083F780). There are **41 native rows, zero failed calls** in this fixture. Inputs and expected fields/bytes are recorded. No C# produces an expected value.

Budget input is a separate Robot counter block: played bytes, zero streamed bytes, played frames, zero streamed frames at Robot+0x238. Result is the two words written to streamer+0x98/+0x9C. These are component counter fixtures, not a complete engine tick capture. Negative/wrap boundaries are recorded as unsigned input bits and passed to C# as unchecked signed words.

Compression inputs are explicit borrowed 64×128 one-byte image headers, row stride128, and an initially empty output vector. Fixture cases: blank, filled, an explicit test pattern, alternating columns and deterministic binary noise. Native results have respectively 2, 3, 216, 128 and 1024 bytes. The source 128×32 bitmaps are also recorded; placing them on even canvas rows is an explicit input adapter, not evidence that firmware lighting parity has been resolved. M3-008's hardware uncertainty is unchanged. `NativeQ11TransformTests` compares exact native bytes through both EncodeCanvas and the existing bitmap adapter. DeviceTests' display assertion then compares the same bytes after Display.Show; it no longer calls Encode to calculate its expected message.

NamedColors calls use actual native short-string objects and return a reference to four native RGBA bytes. Known names and lowercase `red` are replayed; the miss returns native `ffcc00ff`. The host only interprets those bytes in the C# RGBA word convention. Native map/string logic is executed; the added `memcmp` service is a byte-memory external primitive, not a replacement color lookup. No engine helper is mocked.

The existing Q10 generator additionally records explicit off-black and DEFAULT RGBA inputs, making color fixture lookup complete for these assertions (4,150 rows now, versus 4,148 in the initial Q10 commit). `NativeOracleFixtures` is a fixture reader; its expected color, default-eye and budget accessors do not call production methods. Three fields that remain production-derived are not hidden by this helper.

## Citation correction before relying on Q7

Q7 said: "Native display encode0x00592974..0x00592B88." That citation is WRONG: 0x00592974 is a return tail in an audio behavior-stage map insertion, and 0x00592988 is that tree's find-leaf helper. The exported shipped compression entry is 0x00581904. This is a correction to the research report, not a contradiction of the current manifest.

Current record, read before correction:

> M3-007 — "CompressRLE exactly: skip, repeat, runs over row pairs, the trailing-run rule, raw fallback at >= 1024 bytes"; EXACT_SOURCE. Evidence: "B6 requires 64x128 (0x00581912..0x0058197A)"; "B7 u64 column masks, non-zero is lit (0x0058197C..0x005819E8)"; "B8 skip 0b00nnnnnn (0x00581B0A..0x00581B42); B9 repeat 0x40|k (0x00581A06..0x00581A46)"; "B10 run 0x80|((len-1)<<2)|pair (0x00581A48..0x00581AB0); B11 trailing run always at c==127 or pair!=0 (0x00581AB2..0x00581AE2)"; "B12 raw fallback when the RLE is >= 1024 bytes (cmp.w r1,r0,lsr #10; 0x00581B76..0x00581BA0)".

The manifest already has the correct ranges; it is not modified. The five replayed cases do not establish every malformed input, allocation failure, or every RLE boundary sequence. The source-defined nonzero pixel semantics are exercised here only by binary pixel values. No full production-path status is upgraded.

## M1/M2 remaining circle

The old Opus site is cited in `20260930-B-CORE-verify-3-4.md`, item7. Current EngineAppLayerTests:416 pins independent seconds; :401/:407 uses before/after production seconds for a relative-clock assertion; :1622/:1626/:1629 still uses production SecondsF to derive deadline expectations. The latter is still a partial numeric clock/deadline oracle. Exact native clock outputs are MISSING because `BaseStationTimer::UpdateTime` 0x0084BC38 calls external `__aeabi_ul2d`. The complete shipped converter scan found no exported definition for it. No .NET or Python uint64→double model was injected into that call.

Self-review: existing live-entry tests retained, expected computations removed only where actual native output exists, unsupported paths listed above, no hardware invoked, no status/evidence changes. Validation and commit are logged in `jobs/status/CODEX-Q4.md`.
