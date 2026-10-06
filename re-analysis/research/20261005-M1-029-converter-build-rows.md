| Scope item | Coverage | Result |
| --- | --- | --- |
| Source-based raw conversion model, successful allocation | CHECKED | Matched official source, preserving the shipped unadjusted ratio; all 37,802 saved native corpus rows agree on bits, errno and endpoint. |
| Native correction loop expressed as build rows | CHECKED | S1–S21 below specify the reopened decimal/correction blocks, including boundary/tie/loop gates. Manager checking is still required. |
| Normalized num_get conversion wrapper | CHECKED | Bits, errno and state agree on all 37,802 corpus rows, using separately recovered wrapper gates. Locale stage 2 is outside this harness. |
| Four controlled rounding modes | CHECKED | 56 inputs per mode, 224 raw comparisons, zero differences; this is bounded validation, not an exhaustive mode proof. |
| Hex/special inputs in the raw model | CHECKED | All five hex corpus cases and the other special cases match; full raw hex-path extraction/exception-edge proof remains outside these decimal rows. |
| Production loader FPSCR and current C++ locale | PARTIAL | Existing extraction locates the thread and locale path; actual runtime state remains UNKNOWN. |
| Allocation failure, enormous-length counter wrap, FP exception-state equivalence | PARTIAL | Not exercised or settled; successful-allocation fixtures and bounded strings only. |
| Full firmware Reader build readiness and exception destination | PARTIAL | This is a research result, not a production implementation or M1-029 settlement. Formatting and the retained exception MISSING remain separate. |

# M1-029 source-based converter build rows and differential model — 2026-10-05

Answers the operator's “Ok” to deriving a conversion specification from the matched source, explicitly preserving the binary correction order, and testing a research model. Read PROJECT_STATE and the research lane first. Writes are confined to this directory; no production, inventory, manifest, approval, status, commit or push changes.

**Result:** one numerical change to the matched source—retain the normalized quotient computed before exponent-word adjustment—makes the model agree with **every one of 37,802** saved shipped-converter observations on returned binary64 bits, errno and end offset. The checked wrapper model also agrees on bits, errno and state for every row. Unmodified candidate numerical source differs on **4,640** rows. A separate controlled-mode set agrees on 224/224 raw comparisons. This gives a concrete source-based porting basis, not a correctly-rounded-host-parser substitute.

## Authority and current record

C denotes base-zero VA in shipped `resources/lib/armeabi-v7a/libc++_shared.so`, SHA-256 `8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a`. E denotes the shipped engine. The source/prebuilt match is documented in `20261005-M1-029-converter-lineage.md`. The candidate source is authority 6 until each claimed operation is checked against native instructions; passing the model corpus alone does not upgrade provenance.

Current record quoted before narrowing its unresolved conversion:

```json
{
  "id": "M1-029",
  "title": "Firmware version check against the shipped firmware header",
  "status": "IMPLEMENTATION_GAP",
  "authority": "libcozmoEngine.so 3.4.0-1204",
  "evidence": [
    "G5.1..G5.6 HandleFirmwareVersion 0x0052D470: guard (0x0052D478..0x0052D484); JSON parse (0x0052D4BA); FACTORY build path (0x0052D4C2..0x0052D506); v = version, t = time (0x0052D51A..0x0052D536); expected E_v/E_t at +0x1C/+0x20 (0x0052D538); sim flag (0x0052D53E..0x0052D5B6)",
    "G5.9/G5.10 no robotAvailable and not sim -> result 1 (0x0052D7B8..0x0052D850); sim -> 0 (0x0052D7C6)",
    "G5.11 robotDev = v == t, appDev = E_v == E_t; if they differ -> 3 OutdatedFirmware (0x0052D7DE teq.w r0,r1; 0x0052D7E4 movs r0,#3)",
    "G5.12 unsigned: E_v == v -> 0; E_v > v -> 3; E_v < v -> 4 OutdatedApp (0x0052D8DA..0x0052D8E0; 0x0052D9A6..0x0052D9AC); time is not compared again",
    "G5.14/G5.15 E_v/E_t are copied from RobotManager+0x84/+0x88 in AddRobot (0x0052EEF2/0x0052EEF8; ctor 0x0052D196); the RobotManager ctor zeroes them (0x0052E512, 0x0052E516)",
    "G5.16..G5.20 RobotManager::Init -> FirmwareUpdater::LoadHeader starts a loader thread (0x0052E7F8; pthread_create 0x0067692A) that reads config/engine/firmware/cozmo.safe and parses the JSON header in the first 0x800 bytes (0x00677C44..0x00677D34); ParseFirmwareHeader stores version -> +0x84, time -> +0x88 (0x0052EA36..0x0052EA9A)",
    "G5.21/G5.32..G5.37 Scope 1 is DataPlatformResourcesPath = persistentDataPath/cozmo/cozmo_resources (pathToResource 0x0084BE34 table 03 14 22 2f 41; unity/scripts/csharp/PlatformUtil.cs:5-13), extracted from the shipped assets (re-analysis/obb/assets/resources.txt:2075); the shipped header has version 2381, time 1546972025 (re-analysis/obb/assets/cozmo_resources/config/engine/firmware/cozmo.safe offsets 0-445)",
    "G5.22..G5.30 nothing orders the header load before AddRobot: the loader starts in cozmo_startup before the engine thread (0x006661EA, 0x0065B14E), and neither the ConnectToRobot handler nor AddRobot checks the load (0x004ED026..0x004ED1EC; loaded flag +0x18 unread on that path)",
    "G5.31 a missing, short or unparsable file leaves E_v = E_t = 0 for the session (0x006764E4, 0x00677C50..0x00677D28)",
    "G5.38..G5.40 no writer of RobotManager+0x84/+0x88 besides the ctor and ParseFirmwareHeader was found; the scan cannot prove absence (adjusted-base, register-offset, whole-object and untyped accesses are outside it); see decision D7"
  ],
  "unresolved": "built, awaiting strong verification: checked firmware byte Reader replaces System.Text.Json on FirmwareHeader.Parse and both live HandleFirmwareVersion paths. Manager-adopted research/20260930-bcore-extractions.md item 6 with research/20261005-B-M1M2-rows-check.md Item 6 corrections winning: whitespace 09/0A/0D/20 (0x008E166E..168C); saved root success ignores suffix errors (0x008E0886..0912); empty last decoded key close (0x008E0FA6..0FBA); comments/byte literals and container order; leading zeros/bare minus/-0; positive int cutoff INT32_MAX and sign-specific 64-bit limits (0x008E1CF2..1ECA); raw bytes, escapes and surrogate low10-bit combination (0x008E1B34..1C9E, 0x008E262A..2896); root counts toward >1000 runtime throw (0x008E09AA..0D92); duplicate replacement and partial mutation. Firmware build/version comparisons preserve byte strings. MISSING: decimal/exponent/overflow real conversion via current locale and shipped libc++ num_get/rounding (0x008E22C8..23B2) and real asString formatting; these throw explicit JsonMissingSource instead of silently substituting .NET parsing. MISSING: final escaping exception destination; existing Isolated/LoadAsync host containment remains an unbuilt candidate, not recovered engine behavior. Parse itself propagates typed depth/access errors, not parse-false. Record remains IMPLEMENTATION_GAP and incomplete; checked real-conversion rows required before completion. Prior Opus contradictions are repaired only for the checked subset; no claim of complete reader fidelity."
}
```

This report does not contradict that record's IMPLEMENTATION_GAP or its unbuilt whole production path. It supplies the missing numerical research basis; it does not authorize treating other MISSINGs as recovered.

## Research model and reproducibility

The pinned [official NDK 15.2 source](https://android.googlesource.com/toolchain/prebuilts/ndk/r15/+/d0d20f32ed2fb9871e1ea7604a93fd85ddb98367/sources/android/support/src/stdio/strtod.c) has SHA-256 `d49a1dd846fdd9f992cfaca7eafc45d061eed265cbe8bbb1ddefde6df1c96e7e`. `20261005-converter-source-generate.py` verifies that hash, keeps the strtod/bigint prefix and gethex body, and removes the unrelated dtoa formatting bodies. Original licensing notices are retained; their documentation notice is reproduced in the lineage report.

The generator creates three research-only C files:

- `20261005-converter-source-original.c`: numerical source unchanged. Android-only fpmath include is replaced by host fixed-width/system types; public-host headers supply successful allocation, ASCII ctype, errno and mutexes.
- `20261005-converter-source-patched.c`: same source, except ratio saves its quotient immediately after the two b2d calls and returns that saved quotient after the integer high-word edits. This mirrors C `7EE9E..7EECA`.
- `20261005-converter-source-directed.c`: patched model plus a fixture mapping host rounding modes to the native FLT_ROUNDS expression and an explicit mode setter. This supports mode testing; it does not claim the loader enters a particular mode.

Compiler: WSL Ubuntu x86_64 GCC **15.2.0-16ubuntu1**, O2, shared/PIC, `-fno-strict-aliasing -ffp-contract=off -frounding-math`, linked to host pthread/libm. No fast-math or fused arithmetic. The ordinary comparison explicitly selects and checks glibc FE_TONEAREST. The directed model maps FPSCR RMode 0/1/2/3 to nearest/up/down/toward-zero and FLT_ROUNDS 1/2/3/0. All floating operations remain binary64; the host model's success is cross-check evidence, not proof that its compiler will preserve every untested ARM behavior.

Reproduce, from the repository:

```text
python re-analysis/research/20261005-converter-source-generate.py
# In WSL, run the research scripts using the repository's /mnt/c/... absolute path:
sh re-analysis/research/20261005-converter-source-build.sh
python3 re-analysis/research/20261005-converter-source-diff.py
# Native four-mode observations: Windows Python with the local Unicorn dependencies:
python re-analysis/research/20261005-converter-source-mode-oracle.py
# Then in WSL:
python3 re-analysis/research/20261005-converter-source-mode-diff.py
```

The corpus is the committed `20261005-jsoncpp-oracle-cases.jsonl.gz`, generated by the pinned shipped-binary oracle. Native mode observations are in `20261005-converter-source-mode-oracle.json`. Scripts stop on unsupported fixture operations; no invented output fills an oracle error.

## Differential results

| Corpus category | Checked raw rows | Patched differences | Original-source differences |
| --- | ---: | ---: | ---: |
| Random decimal | 30000 | 0 | 3099 |
| Halfway | 2000 | 0 | 622 |
| Negative halfway | 2000 | 0 | 622 |
| Boundary | 1750 | 0 | 91 |
| Long mantissa | 1000 | 0 | 171 |
| Leading zeros | 1000 | 0 | 33 |
| Special | 46 | 0 | 2 |
| fr-FR-labeled input | 6 | 0 | 0 |
| Total | **37802** | **0** | **4640** |

The fr-FR label does not change raw-converter locale; it labels the earlier .NET comparison. This experiment does not exercise C++ num_get's locale-specific normalization.

Normalized wrapper comparison: 37802 checked, zero differences in bits/errno/state. It uses the explicit native endpoint/errno gates, not a host locale parser. All original-source differences are preserved in `20261005-converter-source-original-differences.jsonl.gz`; patched differences are an empty array in `20261005-converter-source-differences.json`. Counts are in the summary JSON.

Examples under FPSCR=0 / initial errno77:

| Input | Shipped / patched | Original candidate source |
| --- | --- | --- |
| `131.e-227` | `113F0886B36F1862`, errno77, endpoint9 | `113F0886B36F1861`, errno77, endpoint9 |
| `1.7976931348623157e308` | `7FEFFFFFFFFFFFFD`, errno77, endpoint22 | `7FEFFFFFFFFFFFFF`, errno77, endpoint22 |
| `1.7976931348623159e308` | `7FEFFFFFFFFFFFFD`, errno77, endpoint22 | `7FF0000000000000`, errno34, endpoint22 |

The last example changes acceptance through the wrapper's ERANGE gate, not merely the least significant returned bit. The numerical anomaly cannot be dismissed as per-sample DSP or host-system policy; it is a shipped M1 converter.

Mode test: 56 distinct strings (24 targeted and 32 deterministic sampled decimals), each in four modes, **224 raw comparisons**, no native instruction-budget errors and zero bits/errno/endpoint differences in any mode. No wrapper-mode or native FPSCR output-flag comparison is claimed by that additional set. `20261005-converter-source-mode-summary.json` and the empty mode-differences JSON record the result.

## Build rows for decimal conversion and correction

These rows extend the earlier R/D/B/Q rows; use the original byte lexer and source widths, not a replacement decimal grammar. Terms: H/L are magnitude high/low words, S is bigint difference sign (candidate below exact value when nonzero), R is current candidate. Source locals bb/bd/bs denote approximate candidate, exact decimal and rounding-boundary bigints. Integer recurrences and bit edits use the source's 32-bit widths; f64 operations round at each stated operation. Address transcript: `20261005-converter-source-spec-native.txt`; literal-pool bytes are not executable instructions.

| Step | C address | What it does | Gates, order, failure results | Width / bits / source association |
| --- | --- | --- | --- | --- |
| S1 | `7E642..7E7C6`, `7E7CA..7E904` | Collects significant decimal digits and decimal exponent; builds initial approximation from first nine plus next seven digits. | Later digits feed the full bigint. Missing exponent digits restore endpoint to e/E. Exponent magnitude caps at19999. | u32 accumulators; e corrected by fractional digits. Keep the checked byte parser from D1–D5. |
| S2 | `7E962..7E9DA`, `7EBCA..7EBFE` | Runs the native short fast path. | nd≤15 and FLT_ROUNDS=1; e=0 returns; e≤22 multiplies, e≥−22 divides. Positive extra gate e≤22+(15−nd) performs multiply by T[15−nd], then T[e−(15−nd)]. | f64 separately rounded; native mode gate `(((FPSCR+00400000)>>22)&3)==1`. |
| S3 | `7E9DA..7EA86` | Slow positive scaling uses adjusted exponent e1=e+nd−min(nd,16). Multiply T[e1&15], then selected large powers for e1>>4. | Remaining exponent (e1&~15)>308 → overflow. Visit lower set bits in ascending table index, reserving final multiplication. | T and large-power exact bytes are in prior extraction/lineage; use raw constants. |
| S4 | `7EA86..7EAD2`, `7EB70..7EBC8` | Before last large multiply, subtract53 from high-word exponent, multiply, then test exponent and restore or max-finite candidate. | New exponent>7CA00000 → overflow; exponent>7C900000 → R=7FEFFFFFFFFFFFFF and continue correction; otherwise add03500000 to H. | Integer word subtraction/addition `03500000`; binary64 multiply is between them. |
| S5 | `7EAD4..7EB6E`, `7EC00..7EC60` | For negative e1, divide by T[abs(e1)&15], multiply lower selected tiny powers, then final tiny power. | abs(e1)>>9 nonzero → zero/ERANGE. Save pre-final R; if final is zero, double saved R then multiply again. If still zero → zero/ERANGE; otherwise set min subnormal candidate and refine. | Doubling is native f64 add R+R; min subnormal `0000000000000001`. Order cannot collapse into a single abstract 10^e. |
| S6 | `7EC62..7ED72` | Reconstructs full decimal bigint bd0; at each iteration copies it into bd and decomposes R into bb, bbe and bbbits. | Successful allocation only in model. bbe is power of2 in R=bb*2^bbe; bb has trailing zero bits removed. | Source s2b/d2b; bigint limbs u32, partial multiply u16. |
| S7 | `7ED96..7EDDC` | Sets correction scales from e and bbe, then removes common positive shifts. | e≥0: bb2=bb5=0, bd2=bd5=e; e<0: bb2=bb5=−e, bd2=bd5=0. Add positive bbe to bb2 or subtract negative bbe from bd2. bs2=bb2. j=bbe+1075 if bbe+bbbits−1<−1022, else54−bbbits. Add j to bb2,bd2; subtract min(bb2,bd2,bs2) if positive. | In native, bbbits is stack38 and bbe stack3C; compare bbe+bbbits against−1021 implements source exponent test. Signed integer arithmetic. |
| S8 | `7EDE0..7EE3C` | Forms scaled candidate, decimal and boundary. | If bb5>0: bs*=5^bb5, bb=bs*bb. Then shift bb by bb2. Multiply bd by5^bd5 then shift bd2; shift bs by bs2. This order identifies each multiplier with the correct operand. | Exact integer operations. Do not associate bd's power with bs or normalize away a rounding boundary. |
| S9 | `7EE3C..7EE62` | Delta=diff(bb,bd); save S and clear Delta sign; compare magnitude with bs. | cmp<0 → S10; cmp==0 → S11; cmp>0 → S14. | Bigint difference sign is separate from magnitude. |
| S10 | `7F0F0..7F10E`, `7F1A8..7F1BE` | Less-than branch usually accepts R; special zero-fraction/S==0 compares Delta shifted left1 with bs. | If S!=0 or L!=0 or H fraction!=0, accept unchanged. Otherwise second compare>0 selects predecessor S12, ≤0 accepts unchanged. | Shift1 corresponds Log2P for IEEE. Two exact comparisons; no host half test. |
| S11 | `7F110..7F154` | Handles exact boundary equality. | S!=0 and whole fraction all ones: H=(H&7FF00000)+00100000,L=0, accept. S==0 and whole fraction zero → predecessor S12. Otherwise even L accepts; odd L→ S13. | Direct carry writes bits before return; no immediate errno write. |
| S12 | `7F1C0..7F1E6` | Writes predecessor across binary exponent boundary. | H=((H&7FF00000)−00100000)|000FFFFF; L=FFFFFFFF; accept. | Preserve u32 word arithmetic; no host nextafter substitution asserted. |
| S13 | `7F186..7F1A6`, `7F262..7F298` | Odd equality adds ULP when S!=0, subtracts it when S==0. | Subtraction reaching exact zero → zero/errno34; other results accept. | ULP helper7FFD8, f64 addition/subtraction. Nonzero subnormal is not automatically an error. |
| S14 | `7EE82..7EECA` | Ratio=b2d(Delta)/b2d(bs), before either integer-exponent word adjustment. | Both approximations are normalized/truncated; quotient computed at7EE9E. Later adjustment writes saved stack words but never recomputes quotient. | **Required binary/source difference.** Return the pre-adjustment quotient; the corrected model makes this explicit. |
| S15 | `7EECA..7EF12`, `7F072..7F0C2` | Chooses aadj magnitude and signed step for small ratio. | ratio≤2: S!=0→aadj=step=1. S==0 with nonzero fraction→aadj=1,step=−1, except min subnormal→underflow. Zero fraction: aadj=0.5 if ratio<1, else ratio*0.5; step=−aadj. | 1=`3FF0000000000000`, 0.5=`3FE0000000000000`, −1=`BFF0000000000000`. |
| S16 | `7EED8..7EF00` | ratio>2: aadj=ratio*0.5, step=+aadj for S!=0 else−aadj; native mode gate can add0.5 to step. | Replacement only when `(((FPSCR+00400000)>>22)&3)==0`, i.e. source FLT_ROUNDS=0. Gate is distinct from S2. | Separate multiplication and addition; no FMA. |
| S17 | `7EF26..7EFEC` | If old exponent=7FE00000, scale R down53 exponent bits; multiply step by ULP(scaledR), add, test overflow, restore or clamp-and-repeat. | If new scaled exponent≥7CA00000 and original R was max finite → Inf/errno34. Otherwise clamp max finite and repeat. Bounded result restores03500000. | High-word edits around ordered f64 multiply then add. |
| S18 | `7EFA2..7EFEA` | Otherwise corrects R using ULP; small exponents adjust step through integer conversion. | If aadj≥1 and old masked exponent≤03400000: step=double(VCVT.s32(aadj+0.5)), negated iff S==0. Then correction=step*ULP(R); R=R+correction. | VCVT signed32 truncation, not current rounding-mode conversion. Out-of-range conversion behavior remains untested; bounded normal successful ratio is <2. |
| S19 | `7EFF2..7F056`, `7F0C4..7F0D8` | Decides whether to repeat refinement. | Changed exponent→repeat. Else fraction=aadj−double(VCVT.s32(aadj)). S==0 with zero new fraction bits: repeat iff fraction≥quarter bound. Other cases repeat iff lower≤fraction≤upper; otherwise accept. | quarter=`3FCFFFFF94A03595`; lower=`3FDFFFFF94A03595`; upper=`3FE0000035AFE535`. Inclusive gates, separately rounded subtract. |
| S20 | `7F058..7F070`, `7F1E8..7F202` | Releases iteration temporaries and loops or finishes. | Repeat frees bb,bd,bs,Delta then returns7ED38. Finish frees bb,bd,bs,bd0,Delta. Free helper skips null/sentinel. | Preserve ownership and order; not a license to invent allocation failure results. |
| S21 | `7F168..7F184`, `7F28A..7F298`, `7F20A..7F250` | Overflow/zero-underflow write errno34; endpoint is stored and magnitude sign applied at return. | Return signed magnitude, including signed zero/Infinity; no-conversion endpoint remains input start. | Infinity=`7FF0000000000000`; sign high bit80000000; result r0/r1. |
| W1 | `5EB98..5EC24` | Wrapper saves errno, clears it, calls conversion; restores saved errno only when new errno=0; checks endpoint and ERANGE. | Empty or endpoint mismatch→zero/state4. Exact endpoint with errno34→retain numeric result/state4. Otherwise state0. Endpoint comparison uses supplied normalized byte end, including any embedded NUL mismatch. | Do not equate raw prefix success with Reader success. Corpus initial errno77 is a fixture, not a production constant. |

S1/S2 and W1 use the previously reopened blocks in the lineage/raw transcripts; S3–S21 were reopened for this report. The separate `20261005-converter-source-hex-ulp-native.txt` reopens gethex `7F3C0..7F910` and ULP `7FFD8..8004C` for navigation/model checks. The five small raw hex probes passing do not constitute a complete hex build specification.

## Proposed C# hosting and remaining boundary

The production trigger is `FirmwareJson.Reader.DecodeNumber` in `cozmo-stack/src/Cozmo.Robot/FirmwareJson.cs:253`, currently throwing JsonMissingSource at line264 when integer decoding falls through. A checked future implementation should keep byte token extraction there and host this numeric helper as a firmware-json conversion dependency. Return numeric bits and explicit failure/consumption state; a .NET Parse success flag is not equivalent to W1. This is a proposed host location, not a code change or new manifest record.

The raw converter receives the output of the locale-normalization stage, not arbitrary original token text. Current C++ global numpunct values/order and loader-thread FPSCR remain UNKNOWN as documented in the validation report. The controlled four-mode model does not choose the production mode for the manager. Allocation failures and extreme counters are not justified by successful-allocation corpus agreement; those pieces remain MISSING until checked or explicitly scoped. Final escaping exceptions and real formatting remain independent unresolved obligations.

Manager checkpoint: check these address/source associations and corrected model before using them to build. The full source algorithm is now a reproducible numerical candidate with zero observed differences; the complete M1-029 production path remains incomplete. No fidelity status changed.
