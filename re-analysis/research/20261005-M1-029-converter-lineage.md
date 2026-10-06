| Requested piece | Coverage | Finding |
| --- | --- | --- |
| Original converter source family | CHECKED | David M. Gay / NetBSD strtod.c revision 1.45.2.1, Android support variant; an official NDK 15.2 source and binary pair was located. |
| Version identification against shipped converter | CHECKED | NDK 15.2.4203891 prebuilt matches the entire raw converter and all bounded helper slices listed below byte for byte. The app's complete library is not identical; its unique build provenance remains UNKNOWN. |
| Candidate source compared with primary instructions | CHECKED | Parser, helper layout, constants, errno/end-pointer wrapper and correction control fingerprints compared below. This is not a full source-to-instruction proof of every branch. |
| Correction-order anomaly | CHECKED | Official NDK 15.2 prebuilt contains the same divide-before-exponent-adjustment instructions. Unmodified source expresses different behavior. |
| Why compilation produced that ordering | PARTIAL | Modern GCC ARM probe does not reproduce it. Exact compiler invocation, optimizer mechanism and any build-time source patch remain UNKNOWN. |
| Whether an unchanged upstream source port is exact | CHECKED | It is not justified: the observed ratio operation order must be reproduced explicitly. No production replacement was built. |

# M1-029 converter source lineage and binary comparison — 2026-10-05

Answers the operator's “Do it” after the proposed source-lineage follow-up. Research only; no production, inventory, manifest, status, approval, commit or push changes. The prior independent execution and mathematical audit are in `20261005-M1-029-oracle-validation.md`; this report establishes source lineage and a much stronger official-prebuilt comparison.

**Finding:** the shipped converter is byte-identical to the bounded converter implementation in the official NDK **15.2.4203891** ARMv7 libc++ prebuilt. That prebuilt includes the same unexpected correction ratio operation order. The publicly paired source is Android's David Gay/NetBSD converter; its mathematical ratio expression does not describe the compiled binary's observed ratio. Use the source as a map and the shipped instructions/oracle as the behavioral authority.

All C addresses below are base-zero virtual addresses in `resources/lib/armeabi-v7a/libc++_shared.so`, SHA-256 `8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a`. E denotes the engine. Ranges in comparison JSON use an exclusive end. Candidate public source is authority 6 until its particular operation is checked against the shipped binary.

## Current record, before narrowing the missing conversion

The current M1-029 title, status and evidence are quoted verbatim. This report does not contradict its gap classification or settle the whole firmware JSON path.

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
  ]
}
```

Full current record snapshot: `20261005-converter-lineage-record.json`.

## Identified official source and prebuilt

Downloaded locally from immutable Google Gitiles commits, with original source license notices retained:

- [NDK 15.2 package version](https://android.googlesource.com/toolchain/prebuilts/ndk/r15/+/d0d20f32ed2fb9871e1ea7604a93fd85ddb98367/source.properties): `Pkg.Revision = 15.2.4203891`.
- [Official ARMv7 libc++ binary](https://android.googlesource.com/toolchain/prebuilts/ndk/r15/+/d0d20f32ed2fb9871e1ea7604a93fd85ddb98367/sources/cxx-stl/llvm-libc++/libs/armeabi-v7a/libc++_shared.so): 677448 bytes, SHA-256 `2559653b6f02237ad8e3613fece91ff4cde48779a7e87e6dc067834b5759ede7`.
- [Paired Android support strtod.c](https://android.googlesource.com/toolchain/prebuilts/ndk/r15/+/d0d20f32ed2fb9871e1ea7604a93fd85ddb98367/sources/android/support/src/stdio/strtod.c): 71837 bytes, SHA-256 `d49a1dd846fdd9f992cfaca7eafc45d061eed265cbe8bbb1ddefde6df1c96e7e`. Header names NetBSD revision `1.45.2.1`, dated 2005-04-19; Android modifications are present. Header identity alone would not prove the app's version.
- [Paired support build file](https://android.googlesource.com/toolchain/prebuilts/ndk/r15/+/d0d20f32ed2fb9871e1ea7604a93fd85ddb98367/sources/android/support/Android.mk) includes `src/stdio/strtod.c` at line 68. This proves inclusion in the candidate build recipe, not the app's exact compile command.
- [Paired libc++ locale header](https://android.googlesource.com/toolchain/prebuilts/ndk/r15/+/d0d20f32ed2fb9871e1ea7604a93fd85ddb98367/sources/cxx-stl/llvm-libc++/include/locale), `__num_get_float` lines 760–785, matches the checked errno/end-pointer wrapper.

The r14 and r15 platform/ndk source candidates have the same strtod.c hash. Thus source hash alone cannot distinguish those releases. Official r14.1 and r16.1 binaries were also downloaded as comparison candidates; neither has the app library's complete hash or size. This is a comparison among the located candidates, not an exhaustive exclusion of all NDK releases/custom builds.

The app's ELF `.comment` contains GCC `4.9.x 20150123 (prerelease)` and Android clang `5.0.300080`; the official r15 prebuilt has identical `.comment` bytes. Mixed comments do not establish which compiler compiled strtod.c. The app's build-id is `95752c27d02e4255fda2f8c70eeace994be66200`; the complete-file hash differs from the official prebuilt. No unique app build invocation is claimed.

## Byte comparisons with the official NDK 15.2 prebuilt

| Shipped C range, end exclusive | What was compared | Result |
| --- | --- | --- |
| `0007E570..0007F3BE` | Entire bounded raw converter, including its contained literal bytes | Identical, SHA-256 `3743ce5cec042b23b470b917ed48119c6a951bde4bff379de9b1ef9fcd0ce537` |
| `0007E4BC..0007E562` | Bigint allocation/release slices | Identical |
| `0007F910..0007FFE8` | Double decomposition, power-of-five, multiply, shift, difference, compare, ULP slices | Identical |
| `00080CA8..00080D4C` | Bigint multiply/add slice | Identical |
| `00081200..00081314` | Trailing-zero and bigint-to-double approximation slices | Identical |
| `0005EB98..0005EC24` | Normalized floating conversion wrapper | Identical |
| `0007EE82..0007EF12` | Critical ratio/correction-choice block | Identical, SHA-256 `5402a44b41f019e48e1be4b802e3abd3227b83c6957bf9538f1a4b9068387f4a` |
| `0009FC88..0009FD90` | Small, large and tiny powers tables | Identical |

Reproduce these eight bounded comparisons with `python re-analysis/research/20261005-converter-lineage-compare.py` (lief required). Both complete binary hashes are pinned; every listed equality is asserted.

The two complete libraries differ in 2086 byte positions. `.text`, `.rodata`, `.data` and build-id differ, while dynamic symbols/strings, relocations, exception tables, GOT and several other sections match. The broader `7E4BC..81314` span has two differing bytes at `80C54` and `811F4`, in literal pools outside the listed raw converter/helper bodies. Those words differ by one; their purpose has not been proved here. Do not generalize the bounded code matches to full-library equivalence. Details and hashes are in `20261005-converter-lineage-source/official-r15-*-comparison.json` and `official-r15-diff-ranges.json`.

## Source-to-instruction rows

Candidate source line numbers below name `official-r15-strtod.c`, unless marked `locale`. Addresses and operation order come from the shipped binary; source correspondence is a checked fingerprint, not permission to replace all remaining UNKNOWNs with source intent.

| Step | Address | What it does | Gates / order / failure | Source correspondence / widths and bits |
| --- | --- | --- | --- | --- |
| L1 | C `7E570..7E642` | Skips whitespace/sign; recognizes case-insensitive infinity and NaN before decimal parsing. | Raw end pointer and errno remain separate from wrapper success; complete Reader grammar is narrower. | Source lines 1593–1655 use fixed dot and ignored NaN payload. Binary canonical NaN `7FF8000000000000`. |
| L2 | C `7E642..7E7C6` | Builds the initial decimal approximation from up to 16 significant digits, retaining the rest for bigint reconstruction. | First accumulator holds nine digits, second up to seven; trailing digits are scanned, not lost from correction. | Source decimal accumulation and DBL_DIG+1 branches; u32 accumulation, then binary64. |
| L3 | C `7E7CA..7E904` | Scans exponent; restores endpoint to e/E when its digits are absent; caps magnitude at 19999. | Exponent sign applied after magnitude collection. | Source lines 1757–1761; literal clamp `00004E1F`. |
| L4 | C `7E904..7E9DA`, `7EBCA..7EBFE` | Forms candidate, tests fast-path gate, performs ordered scaling. | Significant digits ≤15 and native FLT_ROUNDS expression equals 1. Extra positive fast path performs two multiplies. | Source lines 1792 onward; `(((FPSCR+00400000)>>22)&3)==1`; f64, not f32. Production FPSCR still UNKNOWN. |
| L5 | C `7F910..7FFE8`, `80CA8..80D4C` | Bigint decomposition, powers, shifts, difference, comparison and multiply/add implement the correction machinery. | Signed difference uses sign field plus magnitude; helper allocation sentinel is a distinct edge. | Source helper family, 32-bit limbs, 16-bit partial products. Oracle checks covered successful allocation; failure behavior remains separately unverified. |
| L6 | C `81278..81314` | Converts positive bigint to a normalized, truncated double approximation and reports top-limb bit count. | Truncates to top 53 bits; returns value in [1,2) for nonzero positive input. | Source b2d lines 1296 onward. Forced binary64 exponent `3FF`; no integer exponent association included in this returned value. |
| L7 | C `7EE82..7EE9E` | Computes approximations for difference and boundary; **divides the unadjusted normalized approximations**. | First call → d8 → second call → d0 → `vdiv.f64 d0,d8,d0`. | Source ratio lines 1505–1546 supplies the family correspondence but differs in the operation that affects the returned value. |
| L8 | C `7EEA2..7EEC8` | Computes k from bit counts and limb counts; chooses one saved approximation's high word and adds abs(k)<<20. | This happens **after division**; the quotient in d0 is not reloaded or recomputed. | k = ka−kb+32*(a.wds−b.wds). Source changes the corresponding union high word before its final division; its returned quotient therefore includes scaling that this binary quotient does not. |
| L9 | C `7EECA..7EF12` | Selects correction size/sign using that quotient. | Compare with 2; ≤2 uses bounded unit/predecessor cases; >2 halves it and uses difference sign. Rounding-mode branch can add 0.5. | Exact bits: 2=`4000000000000000`, 1=`3FF0000000000000`, 0.5=`3FE0000000000000`. Corresponds to source lines 2049 onward, with L7/L8 binary semantics winning. |
| L10 | C `7EF12..7F0D8`, `7F110..7F1E6` | Applies ULP correction, handles exponent boundaries/ties and determines loop/exit. | Keep rounded operations and low-word parity branches; no correctly-rounded replacement assumption. | Source near-half tests lines 2159–2162 map to `3FDFFFFF94A03595`, `3FE0000035AFE535`, `3FCFFFFF94A03595`. Complete branch specification remains in prior extraction/continuation, still requiring manager checking. |
| L11 | C `7F168..7F184`, `7F28A..7F298`, `7F20A..7F250` | Produces overflow/underflow, endpoint and signed result. | ERANGE=34 on covered overflow/zero-underflow exits; sign applied last; some nonzero subnormals retain errno. | Inf `7FF0000000000000`, sign high bit `80000000`, f64 return r0/r1. |
| L12 | C `5EB98..5EC24` | Normalized wrapper saves errno, clears it, invokes converter, restores old errno only if new errno is zero, and checks endpoint. | Empty or endpoint mismatch → zero/failbit4; ERANGE with matching endpoint → retained numeric result/failbit4. | Paired `locale` lines 760–785 use strtold_l; ARM Android support strtold forwards to double strtod. Order agrees with primary wrapper. |

## Compiler-order diagnostic and its limit

`20261005-converter-ratio-compiler-probe.c` tests both direct union-member branches and a selected unsigned-word pointer. ARM GCC 12.5.0 builds at O2 with strict aliasing enabled and disabled **both retain adjusted values for the division**. Assembly is saved in `20261005-converter-ratio-strict-aliasing.s` and `20261005-converter-ratio-no-strict-aliasing.s`; the reproducible command is in the companion shell file. This is a negative diagnostic, not a recreation of the NDK compiler.

A historical compiler optimization/aliasing interaction is a lead, not a demonstrated root cause. No Android clang 5.0.300080 rebuild was performed, and exact historical flags/source patching are UNKNOWN. Importantly, proving a compiler defect is unnecessary to preserve behavior: both the shipped binary and the official r15 prebuilt contain the same L7–L9 instructions.

## Consequence for the proposed port

The matching source offers a concrete implementation map. An exact port must explicitly encode the binary's **unadjusted approximation ratio** and its subsequent rounded correction operations; it cannot silently use the source's adjusted ratio or .NET's correctly rounded parser. Previous independent execution/math checks establish discrepancies, including `131.e-227`: shipped `113F0886B36F1862`, nearest/.NET `113F0886B36F1861`, successful normalized wrapper under the controlled environment.

This research identifies the source/prebuilt lineage and the critical divergence; it does **not** close M1-029. Remaining manager/build prerequisites include checking the full corrected source mapping, controlled rounding-mode behavior versus actual loader-thread state, current locale normalization, allocation-failure edges, formatting policy, and the deliberately MISSING exception destination. None was filled with a plausible implementation.

Downloaded comparison files remain local under the research directory. No shipped asset was uploaded. Original source licensing notices remain in each source file; the David Gay/AT&T notice is reproduced below as required for supporting documentation.

```text
/****************************************************************
 *
 * The author of this software is David M. Gay.
 *
 * Copyright (c) 1991 by AT&T.
 *
 * Permission to use, copy, modify, and distribute this software for any
 * purpose without fee is hereby granted, provided that this entire notice
 * is included in all copies of any software which is or includes a copy
 * or modification of this software and in all copies of the supporting
 * documentation for such software.
 *
 * THIS SOFTWARE IS BEING PROVIDED "AS IS", WITHOUT ANY EXPRESS OR IMPLIED
 * WARRANTY.  IN PARTICULAR, NEITHER THE AUTHOR NOR AT&T MAKES ANY
 * REPRESENTATION OR WARRANTY OF ANY KIND CONCERNING THE MERCHANTABILITY
 * OF THIS SOFTWARE OR ITS FITNESS FOR ANY PARTICULAR PURPOSE.
 *
 ***************************************************************/

```
