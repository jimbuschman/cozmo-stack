# CUBE-LIFT-WORKOUT-1 score arithmetic widths

Read-only extractor supplement, 2026-10-30. Answers the resumed builder's narrow question about existing double graph/scorer arithmetic versus native rounding. Original read/source-check artifacts remain unchanged. No implementation, official records, git, .NET or hardware changes.

Evidence is the instructions and literal bytes of `resources/lib/armeabi-v7a/libcozmoEngine.so`. Before the trace, `known.py 0x0067C9B8` and `known.py GraphEvaluator2d::EvaluateY` located the exact functions. Existing local `ranges.py` disassembled 0x0067C9B8..0x0067CA86, 0x00804BD0..0x00804C46 and 0x00573B64..0x00573BA8 with PLT resolution. Literal bytes were read directly from the ELF. No decompiler is evidence.

Let f32 denote a binary32 result after each specified operation. The checked finite, shipped graph path is:

| Step | Instructions | Required arithmetic |
|---|---|---|
| Input | 0x0067CA02 loads current emotion into s22; 0x0067C9F0..FC instead loads history and `vsub.f32` when trackDelta is enabled. 0x0067CA10 passes that float to EvaluateY. | Float current emotion; delta subtraction rounds to f32 when used. No shipped workout scorer enables trackDelta. |
| Graph selection | 0x00804BD6..BE2 compares first float X with input; 0x00804BEA..BEC checks count; 0x00804BF0..C04 walks in supplied order until next X >= input. | Before-first and fewer-than-two return first Y; after-last returns last Y. Segment selection uses float comparisons. |
| Segment width | 0x00804C10 `vsub.f32 s4,s4,s2`; 0x00804C14..1C compares with epsilon and branches on <=. | dx=f32(x1-x0). dx<=epsilon returns left Y. Epsilon at 0x00804C48 is bits 0x3727C5AC, approximately 9.999999747378752e-6. |
| Interpolation | 0x00804C1E `vsub.f32`; 0x00804C26 `vdiv.f32`; 0x00804C2E `vsub.f32`; 0x00804C32 `vmul.f32`; 0x00804C36 `vadd.f32`. | t=f32(f32(x-x0)/dx); dy=f32(y1-y0); product=f32(t*dy); y=f32(y0+product). Multiplication and addition are separate; no fused operation. |
| Zero rejection | 0x0067CA3A..4C obtains magnitude through comparison/negation; 0x0067CA50..58 compares and branches on <. | Any abs(y)<epsilon returns +0. Epsilon at 0x0067CAB4 is the same bits 0x3727C5AC. Equality is accepted. |
| Accumulation/mean | Sum starts at +0 literal bits 0x00000000 at 0x0067CAB0. 0x0067CA5A `vadd.f32 s18,s18,s22`; 0x0067CA68..6E converts unsigned entry count through `vcvt.f32.u32`; 0x0067CA72 `vdiv.f32`. | Sum rounds to f32 after every entry in file order. Mean=f32(sum/f32(count)). Empty returns +0. |
| Lift count | 0x00573B86 calls EvaluateEmotionScore; 0x00573B8A calls roundf; 0x00573B92 `vcvt.u32.f32`. | roundf consumes this float mean, followed by unsigned conversion. Supported finite nonnegative shipped values give the ordinary rounded count. No new claim about exceptional conversion inputs. |

GetNumStrongLifts and GetNumWeakLifts select their own scorer and share this helper (0x00573B64..6C and 0x00573B9C..A4). The already checked ShouldPlayEightiesMusic caller also uses the strong-score helper before the zero-versus-roll decision. A graph or sum evaluated in double with only a final float cast does not reproduce the checked sequence of rounding operations, even though many ordinary inputs produce the same count. This supplement establishes widths/order, not complete mood/history/RNG ownership or whole-record closure.
