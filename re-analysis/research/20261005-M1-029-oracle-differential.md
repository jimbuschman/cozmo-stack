| Requested item | Coverage | Result |
| --- | --- | --- |
| Shipped converter oracle: input, bits, errno, return state, end pointer | CHECKED | Raw converter and normalized num_get wrapper execute the pinned packaged ELF; JSONL results report bits, errno, state and UTF-8 byte end offset. |
| Large random-decimal differential corpus | CHECKED | Deterministic seed0x1029; counts below. |
| Halfway/tie cases | CHECKED | Exact adjacent-binary64 midpoints, both signs, generated with enough decimal precision to preserve the rational value. |
| Subnormal boundary, overflow/underflow, long mantissas, huge exponents, leading zeros | CHECKED | Dedicated categories and explicit special cases; all observations retained. |
| inf/nan/hex and locale decimal point | CHECKED | Raw/wrapper probes, invariant/fr-FR .NET comparison, and source-backed Reader restrictions are separated below. |
| Report every difference | CHECKED | Complete JSONL and TSV ledgers include every discrepancy, not only examples. |
| Independent numerical check | CHECKED | Exact decimal-rational nearest-even calculation for every ordinary bit mismatch. |
| Production loader FP/locale, native allocation failure, full Reader execution | PARTIAL | Explicitly outside this controlled converter fixture; no production defaults or whole-path equivalence inferred. |

# M1-029 shipped converter oracle and .NET differential — 2026-10-05

Answers the operator's manager-directed change of approach: test the shipped converter's output rather than port its correction loop instruction by instruction. Explicit authorization permits this research tool and report to be committed. No production, inventory, manifest or job-status changes are included. No hardware run was started.

**Finding:** the proposed “no differences on normal input” condition fails in the controlled shipped-code oracle. This includes finite decimal inputs accepted by the normalized wrapper, not just NaN spelling, hex, locale punctuation or range-error differences. Finite sampling cannot prove equality on all inputs; these counterexamples prevent recommending the .NET parser as an exact replacement from this run.

## Current record, before interpreting the result

Current M1-029 title: **“Firmware version check against the shipped firmware header”**; status **IMPLEMENTATION_GAP**; authority **“libcozmoEngine.so 3.4.0-1204”**. Current evidence includes:

> G5.16..G5.20 RobotManager::Init -> FirmwareUpdater::LoadHeader starts a loader thread (0x0052E7F8; pthread_create 0x0067692A) that reads config/engine/firmware/cozmo.safe and parses the JSON header in the first 0x800 bytes (0x00677C44..0x00677D34); ParseFirmwareHeader stores version -> +0x84, time -> +0x88 (0x0052EA36..0x0052EA9A)

Current unresolved includes:

> MISSING: decimal/exponent/overflow real conversion via current locale and shipped libc++ num_get/rounding (0x008E22C8..23B2) and real asString formatting; these throw explicit JsonMissingSource instead of silently substituting .NET parsing.

The complete current record is captured in `20261005-jsoncpp-oracle-record.json`. It is not contradicted or reclassified here. This research addresses the missing input conversion, not real formatting or the escaping exception destination.

## Oracle and reproduction

`tools/emu/emu_jsoncpp_double.py` loads packaged `resources/lib/armeabi-v7a/libc++_shared.so` at ELF VA0 and applies symbol relocations. It rejects a different binary SHA-256: `8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a`. Engine SHA-256 for Reader instruction checks: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.

Dependencies: Python3.13, lief1.0.0-d05b3499b, Unicorn2.1.4, .NET SDK9.0.201/runtime9.0.3. The oracle runs the packaged instructions; it uses no host decimal parser. Files and assets remain local. Commands from the repository root:

```text
dotnet build re-analysis/tools/emu/jsoncpp_dotnet/jsoncpp_dotnet.csproj
python re-analysis/tools/emu/emu_jsoncpp_double.py -- "131.e-227" "-0.0" "1e309"
python re-analysis/tools/emu/diff_jsoncpp_double.py --out re-analysis/research
python re-analysis/tools/emu/analyze_jsoncpp_double.py re-analysis/research
```

Use an environment containing lief and unicorn2.1.4. This run used the existing research-local Unicorn dependency via PYTHONPATH; the dependency tree is not committed. The converter tool's paths resolve relative to its own file, so an individual oracle run also works from another directory.

| Step | Packaged address / fixture operation | Output and gates | Boundary |
| --- | --- | --- | --- |
| O1 | C:0007E570..0007F3BE; Thumb entry7E571 | Pass NUL-terminated UTF-8 byte string, writable end-pointer slot, opaque locale pointer0. Return r0/r1 is raw binary64; report end pointer as byte offset from input, errno, final FPSCR. | Converter does not read locale pointer on the inspected path. It recognizes dot, not comma; C++ facet normalization happens upstream. |
| O2 | C:0005EB98..0005EC24; Thumb entry5EB99 | Independently rerun normalized-token conversion with expected end=input+byteLength and state destination. Empty normalized token or end mismatch writes zero/failbit4; matching end and errno34 retains converted bits but sets failbit4. | Direct wrapper calls assume token was already normalized. They do not prove num_get would actually produce that token. |
| O3 | E:008E22F8..008E2306,008E2398..008E23D2 | Reader stream extraction checks state mask5; success stores the binary64 and Value type3. Failure does not assign failed wrapper output as successful JSON real. | EOF state2 can accompany success; oracle wrapper state does not synthesize stream EOF. |
| O4 | Oracle reset between **each** O1/O2 call | Restore whole mapped module after relocations; reset all GPRs/D registers, FPSCR0, stack, bump allocator, errno77. Native freelists and cached powers reset with module memory. | Successful allocation fixture. Unknown imports, input/heap exhaustion and instruction-budget exhaustion abort; no guessed result. |
| O5 | External fixtures | ASCII whitespace ctype, ASCII lowercase/strncasecmp; exact byte copies/clear; successful allocator, serial mutexes, guard and errno cell. __cloc returns opaque0, unused by converter. | Phone locale/classification internals and allocation failure are not measured. Production loader-thread FPSCR remains UNKNOWN. |

Oracle integrity checks passed: exact1.5; raw hex6; overflow wrapper fail; comma end offset1; empty wrapper fail; repeated finite conversion unchanged after an intervening overflow call. These check harness integrity, not whole-path source fidelity. Python compilation and .NET helper compilation passed.

## Differential protocol and complete ledgers

The main .NET comparator is the actual `double.Parse(text, NumberStyles.Float, CultureInfo.GetCultureInfo(culture))` in a net9.0 executable. Invariant culture is explicit for ordinary inputs. `NumberStyles.Float` prevents the default overload's extra AllowThousands behavior from obscuring decimal comparisons. Supplementary default-overload probes are in `20261005-jsoncpp-dotnet-default-style.jsonl`: invariant `1,5` becomes15 (402E000000000000) by the default overload; Float rejects it; fr-FR parses it as1.5 (3FF8000000000000). These are separate .NET behavior observations, not shipped lexer rules.

The seed is0x1029. Random decimals have1–70 digits and exponent−400..330; leading decimal-point forms exercise converter acceptance even where Reader entry rejects them. Halfway cases use the exact midpoint between adjacent finite binary64 values with Decimal precision1200, which is sufficient for their finite decimal expansions; both signs are included. Dedicated boundary and long-mantissa cases supplement these.

Artifacts:

- `20261005-jsoncpp-oracle-cases.jsonl.gz`: **every input and both outputs**, not just differences.
- `20261005-jsoncpp-oracle-differences.jsonl`: **every discrepancy**, including full input, category/culture, raw bits/errno/end/FPSCR, normalized-wrapper bits/state, .NET bits/error and reason list.
- `20261005-jsoncpp-oracle-differences.tsv`: complete human-readable ledger, with quoted input and independent exact-nearest-even bits where applicable.
- `20261005-jsoncpp-oracle-summary.json`: counts, seed, binary hash, SDK version and runtime.
- `20261005-jsoncpp-oracle-rational-summary.json`: independent rounding counts and examples.

Difference labels are precise: `raw-bits-or-format` means raw bits differ from .NET bits or .NET rejects; `normalized-acceptance` compares .NET rejection with wrapper failbit; `partial-consumption` means raw end offset is short of the complete UTF-8 input. Any label makes a discrepancy row. Multiple labels can describe one input. No tolerance, NaN canonicalization or signed-zero normalization is applied.

**37,802 completed cases; 9,404 discrepancy rows.** The sum of categories below is exact; bit and acceptance columns can overlap. Runtime542.1 seconds.

| Category | Cases | Any discrepancy | Raw bits/format difference | Wrapper/.NET acceptance difference |
| --- | ---: | ---: | ---: | ---: |
| random-decimal | 30000 | 7246 | 3099 | 4147 |
| halfway | 2000 | 622 | 622 | 0 |
| halfway-negative | 2000 | 622 | 622 | 0 |
| boundary | 1750 | 606 | 91 | 515 |
| long-mantissa | 1000 | 208 | 171 | 37 |
| leading-zero | 1000 | 64 | 33 | 31 |
| special | 46 | 30 | 23 | 16 |
| locale-fr-FR | 6 | 6 | 5 | 6 |

## Exact-rational cross-check

The analyzer parses the decimal value into an exact rational, finds its binary exponent using integer bit lengths/comparisons, divides by the normal/subnormal ULP exactly, and rounds the integer quotient using remainder comparison and even-low-bit tie selection. It handles carry into the next exponent, overflow and signed zero explicitly. This is independent of both Unicorn VFP and .NET's binary64 parser. It is **not** a claim that the shipped code must follow correct-rounding semantics; it identifies which observed result matches mathematical nearest-even.

All **4,638 ordinary bit mismatches** were checked independently: .NET matched exact nearest-even in every one; the oracle output did not. This count excludes special-input and locale-format differences. Examples (all are successful normalized-wrapper results):

| Decimal input | Oracle binary64 | .NET / exact nearest-even |
| --- | --- | --- |
| `131.e-227` | `113F0886B36F1862` | `113F0886B36F1861` |
| `346670056614373788204865159893.67e-122` | `2CBCECD3D4DB9AA3` | `2CBCECD3D4DB9AA4` |
| `-421.3149876955542988998163696724389344594925797647711705551e-74` | `B11DC6A5B4C258C3` | `B11DC6A5B4C258C4` |
| `-352626364254880279899499413276481866637533301249.3437738279e-190` | `A25B85307B8A15D3` | `A25B85307B8A15D4` |
| `-0081637834097.717596377363568e249` | `F5E53D12B0250E88` | `F5E53D12B0250E87` |

The near-maximum probe `1.7976931348623157e308` returns `7FEFFFFFFFFFFFFD` in the oracle versus .NET `7FEFFFFFFFFFFFFF`; this is one of the explicit special cases. The independent rounding analysis rules out a .NET rounding error in the ordinary mismatch set; it does not, by itself, prove that Unicorn perfectly reproduces every native instruction.

## Special-input behavior and jsoncpp boundary

The following table is generated from this run's special/locale observations. State denotes the **direct normalized-token wrapper**, not the full Reader. End offsets count UTF-8 bytes, including the distinction from embedded NUL. Every row's complete .NET result is also in the discrepancy/all-case ledgers.

| Input / culture | Raw bits | errno | End byte offset | Direct normalized wrapper bits / state |
| --- | --- | ---: | ---: | --- |
| `"1.7976931348623157e308"` | `7FEFFFFFFFFFFFFD` | 77 | 22 | `7FEFFFFFFFFFFFFD` / 0 |
| `"1.7976931348623159e308"` | `7FEFFFFFFFFFFFFD` | 77 | 22 | `7FEFFFFFFFFFFFFD` / 0 |
| `"1.8e308"` | `7FF0000000000000` | 34 | 7 | `7FF0000000000000` / 4 |
| `"2.2250738585072013e-308"` | `0010000000000000` | 77 | 23 | `0010000000000000` / 0 |
| `"2.2250738585072014e-308"` | `0010000000000000` | 77 | 23 | `0010000000000000` / 0 |
| `"1e-324"` | `0000000000000000` | 34 | 6 | `0000000000000000` / 4 |
| `"2e-324"` | `0000000000000000` | 34 | 6 | `0000000000000000` / 4 |
| `"3e-324"` | `0000000000000001` | 77 | 6 | `0000000000000001` / 0 |
| `"4e-324"` | `0000000000000001` | 77 | 6 | `0000000000000001` / 0 |
| `"5e-324"` | `0000000000000001` | 77 | 6 | `0000000000000001` / 0 |
| `"0.0"` | `0000000000000000` | 77 | 3 | `0000000000000000` / 0 |
| `"-0.0"` | `8000000000000000` | 77 | 4 | `8000000000000000` / 0 |
| `"1e999999999999"` | `7FF0000000000000` | 34 | 14 | `7FF0000000000000` / 4 |
| `"-1e999999999999"` | `FFF0000000000000` | 34 | 15 | `FFF0000000000000` / 4 |
| `"1e-999999999999"` | `0000000000000000` | 34 | 15 | `0000000000000000` / 4 |
| `"0e99999999999"` | `0000000000000000` | 77 | 13 | `0000000000000000` / 0 |
| `"1e"` | `3FF0000000000000` | 77 | 1 | `0000000000000000` / 4 |
| `"1e+"` | `3FF0000000000000` | 77 | 1 | `0000000000000000` / 4 |
| `"1e-"` | `3FF0000000000000` | 77 | 1 | `0000000000000000` / 4 |
| `"1."` | `3FF0000000000000` | 77 | 2 | `3FF0000000000000` / 0 |
| `".1"` | `3FB999999999999A` | 77 | 2 | `3FB999999999999A` / 0 |
| `"+1.5"` | `3FF8000000000000` | 77 | 4 | `3FF8000000000000` / 0 |
| `" 1.5"` | `3FF8000000000000` | 77 | 4 | `3FF8000000000000` / 0 |
| `"1.5 "` | `3FF8000000000000` | 77 | 3 | `0000000000000000` / 4 |
| `""` | `0000000000000000` | 77 | 0 | `0000000000000000` / 4 |
| `"-"` | `8000000000000000` | 77 | 0 | `0000000000000000` / 4 |
| `"00.1"` | `3FB999999999999A` | 77 | 4 | `3FB999999999999A` / 0 |
| `"nan"` | `7FF8000000000000` | 77 | 3 | `7FF8000000000000` / 0 |
| `"NaN"` | `7FF8000000000000` | 77 | 3 | `7FF8000000000000` / 0 |
| `"-nan"` | `FFF8000000000000` | 77 | 4 | `FFF8000000000000` / 0 |
| `"nan(123)"` | `7FF8000000000000` | 77 | 8 | `7FF8000000000000` / 0 |
| `"nan(0x12)"` | `7FF8000000000000` | 77 | 9 | `7FF8000000000000` / 0 |
| `"nan(foo)"` | `7FF8000000000000` | 77 | 8 | `7FF8000000000000` / 0 |
| `"inf"` | `7FF0000000000000` | 77 | 3 | `7FF0000000000000` / 0 |
| `"-inf"` | `FFF0000000000000` | 77 | 4 | `FFF0000000000000` / 0 |
| `"Infinity"` | `7FF0000000000000` | 77 | 8 | `7FF0000000000000` / 0 |
| `"infinity"` | `7FF0000000000000` | 77 | 8 | `7FF0000000000000` / 0 |
| `"0x1p2"` | `4010000000000000` | 77 | 5 | `4010000000000000` / 0 |
| `"0x1.8p+2"` | `4018000000000000` | 77 | 8 | `4018000000000000` / 0 |
| `"0x1"` | `3FF0000000000000` | 77 | 3 | `3FF0000000000000` / 0 |
| `"0x1p"` | `3FF0000000000000` | 77 | 3 | `0000000000000000` / 4 |
| `"0x0.0000000000001p-1022"` | `0000000000000001` | 77 | 23 | `0000000000000001` / 0 |
| `"1,5"` | `3FF0000000000000` | 77 | 1 | `0000000000000000` / 4 |
| `"1.5junk"` | `3FF8000000000000` | 77 | 3 | `0000000000000000` / 4 |
| `"1\u00002"` | `3FF0000000000000` | 77 | 1 | `0000000000000000` / 4 |
| `"1\u066b5"` | `3FF0000000000000` | 77 | 1 | `0000000000000000` / 4 |
| `"1,5" / fr-FR` | `3FF0000000000000` | 77 | 1 | `0000000000000000` / 4 |
| `"1.5" / fr-FR` | `3FF8000000000000` | 77 | 3 | `3FF8000000000000` / 0 |
| `"1,234" / fr-FR` | `3FF0000000000000` | 77 | 1 | `0000000000000000` / 4 |
| `"1.234" / fr-FR` | `3FF3BE76C8B43958` | 77 | 5 | `3FF3BE76C8B43958` / 0 |
| `"-0,0" / fr-FR` | `8000000000000000` | 77 | 2 | `0000000000000000` / 4 |
| `"1,5e2" / fr-FR` | `3FF0000000000000` | 77 | 1 | `0000000000000000` / 4 |

The interpretation for jsoncpp comes from primary instruction checks in `20261005-jsoncpp-oracle-reader-native.txt` and the prior checked extraction's named libc++ ranges. It is not synthesized by passing arbitrary strings straight to the wrapper:

| Step | Engine/package addresses | Reader/facet gate and result | Order / limit |
| --- | --- | --- | --- |
| J1 | E:008E1660..008E17BE,008E190A..008E19A6 | Reader starts a numeric token only with ASCII minus or0..9; readNumber consumes digits, optional dot/fraction, optional e/E/sign/digits. It has no inf/nan/hex token form. | Raw converter acceptance of these forms is unreachable **as that entire numeric token** through this Reader. |
| J2 | E:008E1CF2..008E1E78 | Integer decoding uses digit range and sign-specific64-bit overflow limits. A dot/exponent or overflow sends the token to decodeDouble. A bare minus takes the signed-integer zero path. | Therefore `-inf`/`-nan` first yield the bare-minus integer token, not a converted Infinity/NaN. |
| J3 | E:008E088C..008E0912 | Root success is saved before skipCommentTokens; non-strict-root operation returns saved success despite trailing tokens/errors. | Isolated root `0x1p2` first yields integer0; `-inf` first yields integer0; `1,5` first yields integer1. This does **not** say an object field followed by these suffixes parses successfully or that the firmware consumer accepts a numeric root. |
| J4 | C:0004930C..000495D2 and classic char numpunct0005AC74/0005AD64 | Upstream num_get uses locale facets to normalize decimal separator to dot and optionally process configured grouping. Classic facet decimal is2E, thousands2C, empty grouping. | Raw locale-specific `1,5` is not automatically mapped to1.5. Reader numeric lexer itself always uses ASCII dot and stops at comma. Production facet mutation remains UNKNOWN. |
| J5 | C:0005EB98..0005EC24; E:008E2306 | Under/overflow errno34 makes wrapper fail; Reader's state-mask5 rejects the resulting real. Nonzero subnormal does not universally set errno34 (3e-324 succeeds in this fixture). | .NET returning0/Infinity without throwing is not the same Reader acceptance. Do not reject all subnormals or accept all zero results by a guessed rule. |
| J6 | E:008E190A..008E19A6 and C:0005EB98..0005EC24 | Incomplete exponent tokens such as1e/1e+ reach the conversion path, which leaves raw end before the token end; wrapper writes zero/failbit4. | Real parse failure, not acceptance of raw prefix1. |

For `inf`, `nan`, uppercase variants, payload forms, leading plus and leading dot, the lexical entry is rejected rather than accepting the raw-converter result. Negative forms and hex forms need J2/J3's prefix qualification. Hex prefix0 followed by discarded suffix is an integer result, not a real result. Standalone token/Reader behavior and object/firmware consumption must not be joined by inference.

## Conclusion and remaining scope

The full requested converter/.NET experiment is supplied and every observed discrepancy is retained. Its normal finite differences fail the proposed substitution criterion under the controlled oracle environment. No production parser replacement, inventory adoption, classification change or acceptance is made here.

The remaining practical boundary is validation of this oracle against another independent ARM execution source if the manager needs a claim about actual phone execution. The exact-rational check validates the reference rounding and earlier exact-rational candidate+ULP check validates one native step; neither substitutes for complete emulator validation. Actual loader-thread FPSCR/facet state, low-memory behavior, real formatting, and final escaping exception destination stay outside this experiment and remain UNKNOWN/MISSING as previously scoped. These limits do not erase measured normal-input counterexamples.

The task's authorized commit contains only the reusable emulator/comparison tools, this report and its result ledgers/source/provenance companions. It does not include the temporary Unicorn installation or earlier unrelated research. Commit identity is reported to the operator after creation; no push was requested.
