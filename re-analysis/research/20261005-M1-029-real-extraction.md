| Requested missing piece | Coverage | Result |
| --- | --- | --- |
| Reader → shipped double extractor, virtual slot and owning artifact | CHECKED | Engine templates dispatch to the double facet in packaged `libc++_shared.so`. |
| Numeric token normalization, consumed input and failure state | CHECKED | Byte normalization precedes conversion; exact end-pointer and errno gates recovered. |
| Decimal accumulator and fast-path gates/width/order | CHECKED | First 16 significant digits, binary64 operations and FPSCR gate recovered below. |
| Complete slow decimal correction, all subnormal/tie/overflow cases | PARTIAL | Main body and helper bodies reopened; instruction transcript supplied. A fully checked numerical specification is still missing. |
| Tie/predecessor branches, tiny-power literals, FP writers | CHECKED | Continuation Q8–Q20, exact table bytes and five bounded TBB save/restore slices; complete path remains PARTIAL. |
| Locale acquisition, classic facet values and production locale writers | PARTIAL | Initial C++ global/classic path and a shipped Mono C-locale writer found. Exhaustive production mutation/order proof remains UNKNOWN. |
| Real `asString`: precision, finite/nonfinite gates, output edits | CHECKED | Precision 17, `%g`, fixed capacity 36, `.0` suffix and comma replacement order recovered. |
| Decimal formatting arithmetic and its package boundary | CHECKED | Actual formatting is an undefined `snprintf` import from phone libc, not a body in this engine or packaged libc++. No replacement algorithm is asserted. |
| Controlled native conversion probes, floats as bit patterns | PARTIAL | Real shipped conversion executed; near-DBL_MAX anomaly is not yet a reliable oracle. |
| Final exception destination | PARTIAL | Intentionally retained MISSING as requested; this task does not replace the prior exception-table UNKNOWN. |

# M1-029 missing real conversion and formatting extraction — 2026-10-05

Answers the operator's “Go ahead and do that”: the separate research follow-up to the partial reader build `2167663` / publication log `76387d9`. Pulled first (already up to date). Research lane only: no production changes, manifest/inventory changes, tests, commits or pushes.

This is an extractor report requiring manager verification. CHECKED means the named bounded slice was reopened in primary instructions; it does not settle M1-029 or mean the complete reader is ready to build. In particular, **the full numerical conversion blocker is not closed** by the wrapper rows or a set of successful probes.

All addresses are ELF virtual addresses at base zero. `E:` means `resources/lib/armeabi-v7a/libcozmoEngine.so`; `C:` means packaged `resources/lib/armeabi-v7a/libc++_shared.so`; `M:` means packaged `resources/lib/armeabi-v7a/libmono.so`. Do not confuse identically numbered engine and libc++ addresses. Thumb function symbols may have an odd address; row addresses name the even instruction address.

Primary binaries:

- E SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.
- C SHA-256 `8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a`.

Companion `20261005-jsoncpp-real-native.txt` contains reopened instructions, PLT/GOT names and literal words; `20261005-jsoncpp-native.py` reproduces selected range reads. Linear disassembly includes literal pools/jump-table bytes, which are data, **not instructions to port**. Rows below identify executable blocks. Ghidra files were navigation only.

## Current record, quoted before any change in its claims

The current record is quoted verbatim below. This extraction narrows some MISSINGs; it does not contradict its IMPLEMENTATION_GAP status or its statement that the whole reader is incomplete.

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


## R: real-token extraction and conversion wrapper

Rows are in execution order. Every row belongs to M1-029 unless explicitly proposed as a new external dependency. “Source-backed slice” is a proposed row classification, not a manifest settlement.

| Step | Address | What the original does | Gates, order and failure result | Width / exact constants | Classification |
| --- | --- | --- | --- | --- | --- |
| R1 | E `008E1E42..1E4C`, `008E2258..2276` | Integer conversion's nondigit/limit exit invokes `Reader::decodeDouble`; copies exactly token `[start,end)` into an owned string. Initializes the destination temporary real to zero. | This is after integer decoding, not a fresh standard-JSON grammar check. Decimal/exponent and sign-specific integer overflow reach it. | Temporary zero is binary64 `0000000000000000`. | Source-backed slice. |
| R2 | E `008E2296..22F0`; C `0003BE10..3BE48` | Constructs an input stringstream and initializes its ios_base. C writes flags `0x1002`, precision 6, width 0, good state for a non-null buffer; initializes locale in the stream and streambuf. | Buffer is input mode 8. The formatter's precision 17 below is unrelated to this input precision. | flags include skipws; construction order ios_base → streambuf locale → buffer installation. | Source-backed slice. |
| R3 | E `008E22F4..22F8` → veneer `004CAA30` → E `007F18A8..18BE`, sentry E `0061B92C..BA28` | Calls double extraction. Sentry first requires a good stream, flushes a tied output if present, and under skipws skips bytes classified as space by the stream's ctype facet. | Sentry(false) uses the skipws branch. Empty/exhausted input changes stream state; false sentry bypasses num_get. JSON's number-token start is already minus/digit, so ordinary firmware number tokens have no leading whitespace here. | Stream state at ios_base+`0x10`; input buffer +`0x18`; tied output +`0x48`. | Source-backed slice. |
| R4 | E `007F18C0..18F4`; C vtable relocation `000A2854` | Reads the stream locale, resolves `num_get<char,istreambuf_iterator>::id`, and calls facet vtable slot +`0x2C` with streambuf begin iterator, null end iterator, ios_base, state pointer and double destination. | Concrete classic facet's vptr is vtable `A2820+8`; slot +2C is relocation `A2854` naming double `do_get`, symbol `00046ABD`. Double veneer tail-calls `00046AC0`. | Slot 11 from the address point, not from the vtable symbol start. | Source-backed slice. |
| R5 | C `0004930C..49388` | Stage-2 prep obtains ctype and numpunct from **the stream's locale**, widens the 32-byte source alphabet, obtains decimal point, thousands separator and grouping. | Alphabet data at C `0009B980`: `0123456789abcdefABCDEFxX+-pPiInN` then NUL. This is libc++ extraction's grammar, not Json::Reader's scanner grammar. | Classic char decimal `2E`, thousands `2C`, empty grouping; see L rows. | Source-backed slice. |
| R6 | C `00046AEE..46B32` | Allocates an initially 10-byte normalized character buffer; grouping-count pointer begins at the 40-u32 local array. Initializes decimal/grouping-permitted byte to 1 and exponent marker to ASCII `E` (`45`). | Buffer grows when output pointer reaches current size. This growth does not truncate long number tokens. | Growth doubles size at `46B90..98`, then resizes to capacity-1 at `46BA8..BB4`; no f32 conversion. | Source-backed slice. |
| R7 | C `000493CC..4942C` | Decimal separator is normalized to ASCII `.` and flips the permitted byte false. Records a grouping count when grouping is enabled and space remains in the group-count array. | A second decimal point or a decimal after exponent reaches return -1 (`4948C`), causing extraction to stop **before consuming that byte**. | Group-array capacity compare is byte offset `0x9F`, i.e. at most 40 u32 entries. | Source-backed slice. |
| R8 | C `0004942E..49468` | Thousands separator is accepted without emitting it only while grouping remains permitted and grouping string is nonempty; records/clears the current digit count. | Empty classic grouping means comma is not a thousands separator for this extraction. General grouping validation is R12. | Group counters are u32; character operations are u8. | Source-backed slice. |
| R9 | C `0004946A..4953E` | Finds the current byte in the widened 32-character alphabet, outputs the corresponding narrow source byte, and tracks exponent/sign/digit state. `x/X` changes exponent marker to `P`; signs require empty output or a previous matching exponent marker. Matching exponent sets its high bit and disables decimal/grouping. | Alphabet miss or disallowed sign returns -1, otherwise 0. The Json scanner never supplies initial `inf/nan`, hex mantissas or a second exponent to this path; do not broaden Reader grammar just because the facet handles them. | Sign indices 24/25; x/X indices 22/23. Low seven bits of exponent marker and previous character masked with `5F` gate sign-after-exponent. | Source-backed slice. |
| R10 | C `00046BCC..46C28` | Peeks an input byte, calls stage2_float_loop, advances input only if return==0, repeats. | Nonzero means stop; the unaccepted byte remains in the stream. End comparison/underflow normalize iterator to null. | Raw char is zero-extended before the helper call. | Source-backed slice. |
| R11 | C `00046C2A..46C64` → C `0005EB98..5EC24` | Converts normalized `[begin,outputPointer)` with `__num_get_float<double>`. Empty buffer gives zero and failbit 4. Otherwise saves old errno, sets errno=0, obtains C-locale handle, calls `82EC0`, saves resulting errno, restores old errno only if new errno==0. | Exact failure gates: converter end pointer != normalized end → zero/failbit4; if end matches but new errno==`0x22` → preserve converter value/failbit4. Other errno values are not generically converted to failure by this wrapper. | Binary64, not float. `82EC0: b.w 7E570` is **packaged code**, not an undefined strtod import. | Source-backed slice. |
| R12 | C `00046C68..46C70` → `00051C5C..51D0E` | Validates grouping after writing the converted double. Empty grouping returns immediately. Nonempty grouping reverses collected group lengths, compares full groups with grouping bytes (repeating final byte), and bounds the leading group. | Mismatch writes state=4, not a guessed retry or numeric clamp. For an ordinary classic-locale JSON numeric token there is no group separator and grouping is empty. | Exact last-group compare: `51CFC ldr`, `51D00 subs #1`, `51D02 cmp`, `51D04 blo` accepts leading count ≤ grouping byte. | Source-backed slice. |
| R13 | C `00046C74..46D02`; E `007F18F6..190A` | Facet sets eofbit2 when returned iterator equals end; destroys temporary buffers. Extractor releases locale, ORs collected state with existing stream state, calls ios_base::clear. | EOF is not itself a parse failure. Stream exceptions follow exception mask; final caller/thread exception destination remains MISSING. | State OR precedes clear. | Source-backed slice; outer EH UNKNOWN. |
| R14 | E `008E22FC..230A`, `008E2398..23D6` | Reader tests badbit1/failbit4 via `tst #5`. If clear, stores the double, swaps old owned value into temporary, stores type3 while retaining high flags, zeros old member metadata, destroys replaced value, returns true. | Does **not** require EOF. This does not negate R11's **normalized-buffer** end-pointer comparison: different layers test different endpoints. | Type store `(oldFlags & FE00) | 3`; `vldr/vstr d0` preserves all 64 bits. | Source-backed slice. |
| R15 | E `008E230C..2396` | On mask5 failure builds `'<original token>' is not a number.`, calls addError, releases temporary strings, returns false. | Destination Json::Value is not assigned this failed temporary. General object/array recovery belongs to the previously checked reader rows. | Suffix text is 18 bytes. No real zero/Infinity is accepted merely because the converter returned it on an ERANGE path. | Source-backed slice. |

The C# counterpart under investigation is `FirmwareJson.Reader.DecodeNumber`; existing `FirmwareHeader.Parse` and the two `HandleFirmwareVersion` handlers own the live callers. This names the host entry, not an implementation proposal.

## D: shipped decimal converter — established boundaries and still-open numerical specification

`C:0007E570..0007F3BE` is the converter behind `82EC0`. C's dynamic dependencies are phone `libc.so`, `libm.so`, `libdl.so`, but there is **no undefined strtod/strtod_l import on this path**. Reading a libc++ symbol as “external to the engine” did not establish that it was outside the APK.

| Step | Address | What the original does | Gates, order / failure | Width / bits | Classification |
| --- | --- | --- | --- | --- | --- |
| D1 | C `0007E59A..E5CC` | Uses imported `_ctype_` space bit, skips leading classified spaces, accepts optional plus/minus and stores a separate negative flag. | This direct converter grammar is wider than the enclosing number scanner. If no conversion, end pointer is restored to input begin. | Space test is table byte shifted left 28 → MI (bit3); character index u8. | Source-backed slice; system ctype table values external. |
| D2 | C `0007E642..E6D0`, `0007E732..E748` | Skips initial zeros, counts significant digits, accumulates first nine decimal digits in a u32 then up to seven more in a second u32; scans remaining digits without adding them to the approximation. | Remaining digits are **not discarded from the later exact bigint reconstruction**. No integer-result type is returned here. | Integer recurrence multiply10/adddigit; first approximation uses at most 16 digits. | Source-backed slice. |
| D3 | C `0007E6D0..E706`, `0007E796..E7C6`, `0007F29A..F32C` | Walks decimal fraction, accounting for leading/trailing zero runs and positions for the later bigint reconstruction. | Total decimal exponent is corrected by fractional digit count; all-zero input retains a zero result. | u32 digit accumulators; signed exponent arithmetic. Detailed register flow in transcript. | Source-backed bounded walk; all extreme-length counter behavior not specified here. |
| D4 | C `0007E7CA..E880`, `0007E8DE..E904` | Accepts e/E and optional sign; consumes exponent digits; clamps exponent magnitude to decimal 19999 if more than eight digit positions or magnitude is too large. | Missing exponent digits restores conversion endpoint to the e/E position. Therefore R11 rejects normalized `1e`/`1e+` by endpoint mismatch even though direct converter can return prefix 1. | Clamp `movw #4E1F`; compare against `4E4F` is accumulator before subtracting ASCII30. | Source-backed slice. |
| D5 | C `0007E904..E960` | Builds binary64 approximation: convert first u32 to double; for ≥10 accumulated digits multiply by shipped power-of-ten then add second u32 converted to double. | Order is convert → vmul.f64 → convert second → vadd.f64. | Constants are table T below. Never f32. | Source-backed slice. |
| D6 | C `0007E962..E9DA` | Fast path only with ≤15 significant digits and `(((FPSCR+00400000)>>22)&3)==1`; zero exponent returns directly; positive exponent≤22 uses multiply; negative exponent≥-22 uses divide. | This explicitly depends on current rounding mode. It does not authorize a universally hardcoded “parse with nearest-even” path. | `vmrs FPSCR`; f64 multiply/divide; sign applied later. | Source-backed gate; production FPSCR UNKNOWN. |
| D7 | C `0007E9D0..E9D6`, `0007EBCA..EBFE` | Additional fast positive case when exponent≤37-digitCount: multiply by 10^(15-digitCount), then multiply by 10^(exponent-(15-digitCount)). | Two rounded multiplies in this order; not a single abstract power expression. | Both f64 using the shipped T table. | Source-backed slice. |
| D8 | C `0007E9DA..EAD2` | General positive scaling multiplies low four exponent bits via T, then chosen powers for exponent>>4; final scale stage lowers exponent bits before multiplying and restores/clamps candidate for overflow handling. | `(exponent & ~15)>308` exits to overflow. Near top of finite range may start a max-finite candidate then enter correction. | Binary64; high-word exponent adjustment `03500000`; masks/limits in transcript. | PARTIAL numerical specification: branch body reopened, not reduced to a complete proven port row. |
| D9 | C `0007EAD4..EB6E`, `0007EC00..EC60` | General negative scaling divides by T remainder, multiplies by selected tiny powers; if scaled result is zero, doubles the pre-scale value, repeats multiplication, then either starts min-subnormal candidate or exits to underflow. | Exponent magnitude with nonzero `>>9` exits underflow. A positive nonzero subnormal is not automatically treated as ERANGE. | f64 order and constants matter; no generic “all subnormals fail” rule. | PARTIAL numerical specification. |
| D10 | C `0007EC62..ED72` | Allocates bigint, reconstructs complete decimal significand with multiply10/adddigit, including digits beyond the initial approximation; copies significand for each correction iteration. | Allocation helpers return a sentinel on malloc failure; its downstream numerical effects need independent checking. | Limb u32, half-limb u16 multiply/carry; helper B1. | Source-backed bounded reconstruction; allocation-failure outcome UNKNOWN. |
| D11 | C `0007ED72..EE64` | Turns approximate double into integer significand/exponent; constructs/scales bigint representations with powers of five and left shifts; forms signed difference and compares its magnitude with the rounding boundary. | Comparison<0 reaches `7F0F0`; comparison==0 reaches `7F110`; >0 enters correction ratio path. | Exact integer limbs, then f64 ratio; tie control is not a mere host-double parse. | PARTIAL: integer helper operations below are identified, complete association/rounding proof still open. |
| D12 | C `0007EE66..F0D8`, `0007F110..F1E6` | Uses normalized bigint-to-double approximations, divides to estimate correction, adjusts candidate by ULP with f64 operations, examines candidate exponent/significand and low bit on equality, may repeat the loop. | `7F152 lsls lowWord,#31` → odd significand adjustment. Underflow/power-of-two edges take separate branches. Do not generalize this fragment to every input or every FPSCR mode. | Helpers B2; constants `3FDFFFFF94A03595`, `3FE0000035AFE535`, `3FCFFFFF94A03595` are near-half/quarter bounds, **not rounded decimal substitutes**. | PARTIAL; complete slow correction semantics not established by this report. |
| D13 | C `0007F168..F184`, `0007F28A..F298` | Overflow writes errno34 and positive Infinity magnitude; underflow exit writes zero magnitude and errno34. | R11 subsequently makes both parse failures. No guessed maximum-finite clamp as successful Reader result. | Infinity `7FF0000000000000`; zero `0000000000000000`. | Source-backed exit slices. |
| D14 | C `0007F20A..F250` | Writes end pointer if non-null; applies negative flag by XORing the magnitude high word with `80000000`; returns binary64 through r0/r1. | Sign application is after magnitude conversion and also applies to zero and failure-result Infinity. | Negative zero `8000000000000000`; negative Infinity `FFF0000000000000`. | Source-backed slice. |

### B: bigint/helper family actually reached

These functions are packaged implementation obligations, not phone-system numeric parsing. The transcript contains their bodies. Operations identified below are supported by their load/store/arithmetic patterns, but the **composition of B2 inside D11/D12 remains PARTIAL**.

| Step | C address | Operation and relevant instructions | Order / result |
| --- | --- | --- | --- |
| B1a | `0007E4BC..E516`, `0007E52C..E560` | Classed bigint allocation/free-list under pthread mutex. Alloc size `14 + (1<<class)*4`; fields +4 class, +8 capacity, +C sign, +10 active length, limbs +14. Null malloc returns sentinel. | New/reused node has sign and active length zeroed. Release skips null/sentinel, pushes to class free list. |
| B1b | `00080CA8..80D4A` | `B = B*m + addend`, little-endian u32 limbs. Each limb is multiplied as two u16 halves; low product receives previous carry; high product receives low>>16. | Writes limb then carry; nonzero final carry appends/grows node, copying sign/length/limbs before freeing old node. |
| B2a | `0007F910..F9EC` | Binary64 significand to bigint; extracts exponent bits20..30, adds hidden bit for normal numbers, removes trailing zero bits using `81200`, returns integer exponent/bit count. | Normal exponent computed from encoded exponent + removedZeros -1075; subnormal uses removedZeros-1074. |
| B2b | `0007F9FC..FB18` | Power-of-five multiplication uses small remainder powers then cached repeated-square bigints; calls `7FB1C`. | The cache/lock path is an allocator optimization; the multiplication and power association are numeric behavior. |
| B2c | `0007FB1C..FCE8` | Bigint multiplication, accumulating half-word products/carries, allocating output by length and trimming leading zero limbs. | Exact u16/u32 operations; not floating-point multiplication. |
| B2d | `0007FCEC..FDD8` | Left shift: zero whole-word prefix, shift limbs by `k&31`, carry high bits to next limb, adjust active length and free old node. | Source tests nonzero final carry before extending length. |
| B2e | `0007FDE8..FF7C` | Ordered signed bigint difference: compares operands, subtracts smaller from larger with borrow, stores difference sign and trims high zero limbs. | Difference sign is inspected at D12 and influences correction direction. |
| B2f | `0007FF80..FFD4` | Bigint magnitude compare: active lengths first, then high limbs down. | Returns negative, zero or positive; D11 branches on all three. |
| B2g | `0007FFD8..8002C` | ULP construction from binary64 exponent with separate subnormal handling. | Its exact output and D12's exponent-edge association must remain with the still-open numerical specification. |
| B2h | `00080C60..80CA4`, `00081200..81274` | Leading-zero count and trailing-zero removal/count helpers. | Used before significand/exponent composition. |
| B2i | `00081278..81318` | Bigint-to-double approximation: leading-limb normalization, takes bits from succeeding limbs and builds binary64 mantissa with exponent field ORs `30000000`/`0FF00000`. | This is a truncated approximation plus a returned exponent/bit count; D12 divides approximations. Do not substitute a general bigint→double conversion without checking its rounding. |

### T: shipped power table used by D5–D9

Table T begins at C `0009FC88`. Values are binary64 words (little-endian in file). The first table is indexed by decimal exponent 0..22, in address order:

```text
0  3FF0000000000000    1  4024000000000000    2  4059000000000000
3  408F400000000000    4  40C3880000000000    5  40F86A0000000000
6  412E848000000000    7  416312D000000000    8  4197D78400000000
9  41CDCD6500000000   10  4202A05F20000000   11  42374876E8000000
12 426D1A94A2000000   13  42A2309CE5400000   14  42D6BCC41E900000
15 430C6BF526340000   16  4341C37937E08000   17  4376345785D8A000
18 43ABC16D674EC800   19  43E158E460913D00   20  4415AF1D78B58C40
21 444B1AE4D6E2EF50   22  4480F0CF064DD592
```

At C `0009FD40`, the selected large positive powers are, in address order:
`4341C37937E08000`, `4693B8B5B5056E17`, `4D384F03E93FF9F5`, `5A827748F9301D32`, `75154FDD7F73BF3C`.
These are file data, not values generated by the C# implementation. Complete tiny-power/scaling and FPSCR behavior remains part of D8/D9's PARTIAL specification; this table alone cannot authorize a replacement parser.


## L: locale ownership and what the package establishes

| Step | Address | What the original does | Gates / timing / failure | Classification |
| --- | --- | --- | --- | --- |
| L1 | E `008E22C8`; C `00056828..5683C` | Default locale constructor obtains C++ `locale::__global()`, copies its implementation pointer and increments shared ownership. | It does not simply ask the phone for its UI language on every number. | Source-backed slice. |
| L2 | C `000567B8..567FA`, `000558F8..5593E` | Lazy C++ global initialization obtains `locale::classic()` and retains it, under guard acquire/release. Classic locale constructs its implementation at `54A34`. | Applies when the lazy global has not already been set. Dynamic global replacement is a separate path (`locale::global` symbol `56B39`); the production writer/order proof is UNKNOWN. | Source-backed initialization; production persistence PARTIAL. |
| L3 | C `0005AC74..AC90`, `0005AD64..AD7E` | Classic char numpunct stores byte pair `2E 2C` at +8 (decimal point / thousands separator), zeroes grouping string, and returns these fields through virtual methods. | This proves the **classic facet's values**, not that every stream in production necessarily has that facet forever. | Source-backed slice. |
| L4 | C `000473A8..473E8`, `0005EBC6..EBCE` | `__num_get_float` obtains `__cloc()`, whose lazy initializer calls packaged `7D6E0` with mask `1FBF` and literal `C`. Converter veneer goes to `7E570`. | Decimal converter entry does not consume its locale-handle argument; it uses imported ctype/lowercase tables for general lexical paths. The normalized numeric token is already prepared using the stream facet. | Source-backed boundary; system tables/context remain explicit inputs. |
| L5 | E undefined `snprintf`, `sprintf`, `__isfinite` imports; M `000BAF04..BAF14`, string M `002D879C` | Real formatting uses C runtime functions. A packaged Mono path passes category6 and empty string to `setlocale`: it can initialize the C runtime locale from the environment. | M `0019F87C..F888` calls category6 with null string, a **query**, not a writer. The Mono writer's ordering relative to engine loading and all other package writers is not traced here. Unity also has an undefined setlocale symbol; its call path remains UNKNOWN. | PARTIAL process C-locale investigation. |
| L6 | C dependencies + E dynamic imports | Phone libc/libm provide actual imported formatting/classification functions. Their implementations are not present in the artifacts inspected here; packaged libc++ owns the input conversion body. | C and C++ locale globals are separate; a C `setlocale` writer is not proof that the C++ num_get facet changed. Conversely classic C++ input is not proof that `snprintf` uses dot punctuation. | Proposed NEW external-boundary record, for manager review; no manifest reclassification. |

No result here applies ADP-1 to JSON arithmetic. The packaged decimal converter is exact work under “Exact, always.” Only a genuinely outside-package dependency is eligible for a separately documented host/system mapping. This report supplies the boundary; it does not approve a policy or an invariant-locale substitution.

## F: exact real-to-string call and post-processing

Live host entry under investigation: `Json.AsStringBytes`, used by the firmware `build == FACTORY` and `version` prefix checks. `Value::asString` may run on a real even though normal shipped firmware uses integer `version/time` and a string `build`. Do not omit a type branch because the usual asset does not exercise it.

| Step | E address | Original operation | Gates, order and failure result | Width / bits / strings | Classification |
| --- | --- | --- | --- | --- | --- |
| F1 | `008E48B8..48C0`, `008E48F2..4902` | Type switch selects real case, loads binary64 d0, calls `008EBF7C` with useSpecialFloats=0 and precision=17. | Type3 only; signed/unsigned integers take distinct `valueToString` branches. | `movs r1,#0`; `movs r2,#11` (hex17). | Source-backed slice. |
| F2 | `008EBF98..BFA4`, data `008EC070` | Builds the format string using `sprintf(formatBuffer,"%%.%dg",precision)`, then calls imported `__isfinite` with saved real. | Format construction precedes finite classification. | For this entry: `%.17g\0`, six bytes; binary64 preserved in d8. | Source-backed call/parameter/order slice; libc formatting body external. |
| F3 | `008EBFAA..BFBC` | Finite branch calls `snprintf(output,0x24,format,double)`, retaining its return count. | Output capacity is 36 bytes including NUL. It is not a C# general-format call with an assumed equivalent exponent spelling. | Variadic double is passed at stack+0; count r5. | Source-backed call slice; libc arithmetic/spelling dependency external. |
| F4 | `008EBFBE..BFE6` | Checks output for ASCII dot first, then lowercase e; only if **both absent** appends bytes `.0\0` at strlen position. | Runs before comma replacement. No uppercase-E search; no numeric reparsing. | `movw #302E` stores `.0`; final NUL at +2. Count r5 is not increased after append. | Source-backed slice. |
| F5 | `008EBFE8..C018` | Nonfinite branch compares real with itself (VS identifies NaN), then with zero (MI negative Infinity); flags select a literal and call snprintf. | For asString, useSpecialFloats=0 chooses JSON-compatible literals below. For nonzero flags the alternate literals are used. Those other callers are outside this request. | NaN → `null` at `00C4342C`; +Inf → `1e+9999` at `008EC078`; -Inf → `-1e+9999` at `008EC08C`. Alternate `NaN`, `Infinity`, `-Infinity`. | Source-backed slice. |
| F6 | `008EC01E..C034` | If snprintf return count≥1, scans exactly that count and replaces every ASCII comma with ASCII dot. | This is **after** F4's append decision. It is not general locale normalization and does not remove grouping or multibyte punctuation. The source does not cap the scan to the output capacity. | Compare byte `2C`, store `2E`. | Source-backed slice. |
| F7 | `008EC036..C04C` | Initializes returned owned string from output buffer using strlen. | Includes any `.0` append; does not use original snprintf count as string length. | Byte string, not a Unicode replacement decoder. | Source-backed slice. |
| F8 | `008E4906..4912` → `008E8780..8882`; `008E48E2..48EE` → `008E888C..8904` | Signed/unsigned integers convert independently: repeatedly divide magnitude by ten using u64 quotient/remainder, prepend ASCII digits, add minus for negative signed value. Signed minimum has explicit magnitude `8000000000000000` branch. | Do-while emits one digit for zero. No `.0`, `%g`, or locale decimal point. These are not the real formatting dependency. | Remainder OR `30`; minus `2D`. | Source-backed slice. |

A consequence of the actual **order**, not an observed production-locale claim: if the external snprintf supplies `1,5` with no dot/e, F4 appends `.0`, and F6 then yields `1.5.0`. It would be incorrect to silently replace the source with “normalize locale then append .0.” Whether such punctuation is reachable during firmware handling remains L5/L6 PARTIAL.

The exact `%g` argument, precision, capacity, special-value choices and post-processing are shipped behavior. The **phone's snprintf implementation** is the separately bounded external dependency. The report makes no claim that .NET `G17` has identical spelling/rounding, and no claim that an external-equivalence policy has already been approved for it.

## Native conversion probes and limits

Research script `20261005-jsoncpp-real-probe.py` runs **C:0005EB98**, including its errno/end-pointer wrapper and real packaged converter/helper bodies, under Unicorn 2.1.4. It does not invoke Python/.NET decimal parsing.

Controlled inputs: ASCII C ctype/lowercase tables; FPSCR=0; guard word; successful bump allocator; serial no-op mutex lock/unlock; byte-exact memcpy/memclr; an errno cell initially77. The opaque `__cloc` handle is stubbed because this decimal converter does not read it. **Actual stream-facet normalization, stream state/EOF, process locale, low-memory behavior and production FP startup are not emulated by this fixture.** Results below are therefore limited experiments, not proof of the complete live path. The script checks that the native wrapper returned rather than exhausting the instruction budget.

`20261005-jsoncpp-real-probes.txt` stores all raw output. Examples:

| Normalized input | Returned binary64 bits | State written | errno after | Interpretation within this controlled fixture |
| --- | --- | --- | --- | --- |
| `1.5` | `3FF8000000000000` | 0 | 77 | Exact finite conversion; saved errno restored. |
| `-0.0` | `8000000000000000` | 0 | 77 | Real negative zero; distinct from integer `-0` in the Reader. |
| `1.` | `3FF0000000000000` | 0 | 77 | Decimal with no following fraction digit succeeds. |
| `1e`, `1e+` | `0000000000000000` | 4 | 77 | End-pointer mismatch; wrapper substitutes zero and failure state. |
| `18446744073709551616` | `43F0000000000000` | 0 | 77 | Integer-overflow fallback can successfully produce a real. |
| `-9223372036854775809` | `C3E0000000000000` | 0 | 77 | Negative integer limit fallback; binary64 rounds in this fixture. |
| `1e309` | `7FF0000000000000` | 4 | 34 | R14 rejects this conversion. Returned Inf is not a successful JSON real. |
| `-1e309` | `FFF0000000000000` | 4 | 34 | Sign applies to overflow magnitude before wrapper's failure gate. |
| `1e-400` | `0000000000000000` | 4 | 34 | R14 rejects underflow. |
| `-1e-400` | `8000000000000000` | 4 | 34 | Negative failed zero. |
| `1e-324`, `2e-324` | `0000000000000000` | 4 | 34 | Underflow to zero fails in these cases. |
| `3e-324`, `4e-324`, `5e-324` | `0000000000000001` | 0 | 77 | These nonzero subnormal results succeed; do not copy host strtod ERANGE assumptions. |
| `2.2250738585072014e-308` | `0010000000000000` | 0 | 77 | Minimum normal fixture case. |
| `0.1` | `3FB999999999999A` | 0 | 77 | Ordinary rounded fraction fixture. |
| `9007199254740993.0` | `4340000000000000` | 0 | 77 | Fixture rounds to representable integer. |
| `1.00000000000000011102230246251565404236316680908203125` | `3FF0000000000000` | 0 | 77 | A half-way case under controlled FPSCR; does not settle all ties/modes. |

**UNVERIFIABLE near-maximum oracle:** the fixture returns `7FEFFFFFFFFFFFFD` for both `1.7976931348623157e308` and `1.7976931348623159e308`. Repeating these in a fresh fixture gave the same result; `1.8e308` gave Inf/failbit4. This surprising result is **not asserted as a proven engine defect**, and must not become an expected C# value before checking the emulator's VFP behavior, relocation/data assumptions and D8/D12's native trace independently. Neither “always correctly rounded host parse” nor “the engine definitely has this anomaly” follows from the current evidence.

### Q: correction-loop ordering exposed by the near-maximum trace

Additional primary instruction rows, not proof of the whole numerical path. Trace companion: `20261005-jsoncpp-real-trace.txt`, reproduced with probe argument `--trace`.

| Step | C address | Actual order and gates | Controlled trace / limit |
| --- | --- | --- | --- |
| Q1 | `0007EE82..EE9E` | Approximate difference as d8, save at stack50; approximate boundary as d0, save at stack48; **immediately divide d8 by d0 into d0**. | Quotient bits `3FF7AC6B26715BB5`. |
| Q2 | `0007EEA2..EEC8` | After division, compute exponent difference from limb counts and helper bit counts; select stack50 for positive difference, stack48 otherwise; add absolute difference<<20 to that saved double's high word. | Saved approximation changes **after** quotient calculation. No reload/redivide of the adjusted approximation before Q3. Do not rearrange this order into a mathematically preferred ratio. |
| Q3 | `0007EECA..EF12` | Compare existing d0 with `4000000000000000` (2). If ≤2, set d13=1; nonzero difference-sign uses +1 step. Zero-sign has additional subnormal/power-of-two gates at `7F072..F0C2`. | Trace takes ≤2/nonzero-sign, so d13/d15 both become `3FF0000000000000`. |
| Q4 | `0007EED8..EF00` | If quotient>2, d13=d0×`3FE0000000000000`; signed step=d0×`BFE0000000000000`, overwritten by d13 for nonzero sign. Compute signedStep+0.5, conditionally use it under the FPSCR-derived gate. | Preserve separate vmul/vadd and predicate; directed modes remain unvalidated. |
| Q5 | `0007EF26..EFEC` | Candidate exponent mask `7FF00000` equal `7FE00000` takes scaled correction: highWord−`03500000`; obtain ULP; multiply signedStep×ULP; add to scaled candidate; check exponent; restore highWord+`03500000` on bounded branch. | Candidate `7FEFFFFFFFFFFFFC` → scaled `7C9FFFFFFFFFFFFC` → `7C9FFFFFFFFFFFFD` → restored `7FEFFFFFFFFFFFFD`. |
| Q6 | `0007EFF2..F056` | Changed candidate exponent loops. Otherwise extract fractional part of d13 via vcvt/subtract, with extra zero-sign/power-of-two gate; compare with shipped near-half bounds. | Fraction zero is below `3FDFFFFF94A03595`, so trace exits at `7F0D8` instead of refining again. |
| Q7 | `0007F110..F1E6`, `0007F0F0..F10E` | Exact bigint equality and less-than boundary take different branches. Equality checks sign, exponent/fraction carry, then low significand bit at `7F152..F154`; odd cases add/subtract ULP. Less-than also has sign/power-of-two handling. | Complete normal/subnormal/zero/carry/directed-mode semantic specification remains PARTIAL. |

The primary **Q1→Q2→Q3 order** and native trace support a specific lead for the anomaly. They do not establish phone execution, complete VFP emulation fidelity or every input's behavior. Neither adopting the near-max bits as an oracle nor replacing this order with textbook strtod is authorized by this report alone.

Reproduction needs the project's usual Python lief/capstone packages and Unicorn 2.1.4. A temporary research-local Unicorn installation remains at `_jsoncpp_probe_dependencies/`: automatic approval review rejected its cleanup as 'blocked by policy' without a more specific reason. It is tooling, not part of the report to adopt or commit. The script/output remain. Run the script with that directory on Python's module search path, or with Unicorn installed in the normal emulator environment.

## Continuation — correction branches and execution environment

Answers the operator's “Can you do that?” follow-up. New companion: `20261005-jsoncpp-correction-continuation-native.txt`. All following converter addresses are in packaged libc++. Local names: H=stack2C, L=stack30 (candidate high/low words), S=r8 (difference sign captured at7EE4A), Delta=r6 and Boundary=sb at7EE54. Hexadecimal masks below are integer bit patterns. These are checked instruction slices, not a claim that the complete converter is settled.

| Step | Address | Gate and ordered operation | Result / limit |
| --- | --- | --- | --- |
| Q8 | `0007EE48..EE62` | Save bigint difference sign in S, clear its sign field, compare magnitude with Boundary. Signed result≤−1 takes Q9; zero takes Q10; positive takes Q1. | Three separate branches. |
| Q9 | `0007F0F0..F10E` | Less-than: `(L OR S)!=0` exits unchanged. Otherwise nonzero `(H AND 000FFFFF)` exits; zero selects Q12. | Both sign and both fraction-word gates matter. |
| Q10 | `0007F110..F154` | Equality/S!=0: if high fraction=000FFFFF and u32 L+1=0, write H=`(H AND 7FF00000)+00100000`, L=0, exit. Other nonzero-sign cases take parity check. S==0 with zero whole fraction selects Q13; otherwise parity check. | L bit0 zero exits unchanged; odd selects Q11. Direct carry write does not immediately set errno. |
| Q11 | `0007F186..F1A6`, `0007F262..F278` | Build d8 from L,H, obtain ULP with7FFD8; S!=0 adds ULP, S==0 subtracts it; store result. Subtraction result exactly zero selects Q14. | Binary64 operations; nonzero subnormal alone does not imply errno34. |
| Q12 | `0007F1A8..F1BE` | Shift Delta left one through7FCEC, compare with Boundary; result>0 selects predecessor write, ≤0 exits unchanged. | Second exact bigint comparison, not an f64 half test. |
| Q13 | `0007F1C0..F1E6` | Equality zero-fraction/S==0 and Q12 predecessor paths write H=`((oldH AND 7FF00000)-00100000) OR 000FFFFF`, L=FFFFFFFF. | Bit-level predecessor across exponent boundary. |
| Q14 | `0007F256..F260`, `0007F27A..F298` | Capture live allocation pointers or initialize early-path pointers to zero; write zero candidate, call __errno and store22 (decimal34); cleanup/sign-return. | Wrapper fails conversion through R14. |
| Q15 | `0007F072..F094` | Small-ratio/S==0 loads r0=H,r1=L. L!=0 sets d13=1,d15=−1, except L==1 AND H==0 selects Q14. | Stack words here are high then low; not binary64 register-pair order. |
| Q16 | `0007F096..F0C2` | L==0 defaults to d13=1,d15=−1; nonzero high fraction returns to correction. Zero high fraction computes d13=quotient×0.5, replaces with0.5 if quotient<1, and negates into d15. | Preserve separate arithmetic/predicates. |
| Q17 | `0007EED8..EF00` | Ratio>2 step+0.5 replacement gate is `(((FPSCR+00400000)>>22)&3)==0`. | Distinct from nearest-mode fast-path gate. |
| Q18 | `0007EFA2..EFC8` | d13≥1 AND masked exponent≤03400000: add0.5, convert signed32 by VCVT, convert back f64; negate iff S==0. | Exact VCVT semantics remain required, not an arbitrary C# cast. |
| Q19 | `0007EFF2..F056`, `0007F0C4..F0D8` | Changed exponent mask repeats. Otherwise fractional part=d13−double(VCVT.s32(d13)); S==0 and zero new fraction bits uses quarter bound3FCFFFFF94A03595 (≥ repeats). Other cases repeat iff lower≤fraction≤upper, bounds3FDFFFFF94A03595/3FE0000035AFE535. | Keep inclusive comparisons and literal bits. |
| Q20 | `0007F058..F070`, `0007F1E8..F202` | Repeat frees stack24,sl,sb,stack18 in that order and returns7ED38. Final cleanup frees stack24,sl,sb,r4,r6 in that order. | Helper skips sentinel; upstream allocation failures still PARTIAL. |

Tiny-power data C:0009FD68, consecutive binary64 words: `3C9CD2B297D889BC 3949F623D5A8A733 32A50FFD44F4A73D 255BBA08CF8C979D 0AC8062864AC6F43`. C:7EB38..7EB6C selects powers by successive exponent bits and multiplies in increasing table order; C:7EC04..7EC18 loads and multiplies the final indexed entry separately. C:7EC20..7EC60 tests zero, retries by doubling the **pre-final** value before multiplication; nonzero retry forces raw candidate0000000000000001, zero retry takes Q14. Table bytes and this bounded slice are CHECKED; all D8/D9 context remains PARTIAL.

Allocator failure sentinel C:0009FD90 has first six u32 words zero. C:7EE66..7EE80 explicitly checks Boundary/Delta against it and substitutes preloaded d9 for the approximation ratio. This is a handled sentinel branch, not evidence for a generic allocation exception. Full failure combinations remain UNKNOWN.

`20261005-jsoncpp-fp-scan.py` scans executable LOAD bytes of supplied ARMv7 libraries for ordinary Thumb/ARM VMSR encodings and decodes candidates. Five FPSCR writer candidates were found, all in libtbb.so; their surrounding instructions were reopened. The scan does not prove absence of conditional ARM encodings, dynamic/external code or alternate control mechanisms.

| Step | TBB address | Checked behavior | Production gap |
| --- | --- | --- | --- |
| FP1 | `0000E40C..E42E` | cpu_ctl_env_helper destructor compares four-byte snapshots; if unequal E422 writes saved FPSCR, then destroys both holders. | Loader-thread reachability/value UNKNOWN. |
| FP2 | `0000F760..F794` | Capture current FPSCR, compare with context; copy context environment and F790 writes it before nested-arena entry. | Context value/order on firmware path UNKNOWN. |
| FP3 | `0000F7F2..F820` | Exception path compares environments; mismatch F814 restores saved word before copying holder. | Firmware-path reachability UNKNOWN. |
| FP4 | `000156B2..156D6` | Compare snapshot with context, copy changed context, write its word at156D2. | Context provenance UNKNOWN. |
| FP5 | `000156D8..156FC` | If snapshots differ,156EC writes saved word then copies environment holder. | Loader-thread ownership/order UNKNOWN. |

Mode measurements are saved in `20261005-jsoncpp-real-mode-probes.txt`; script option `--modes` varies controlled FPSCR. These are fixture results, not production defaults or independent Unicorn validation. Near-max input yields7FEFFFFFFFFFFFFD at0,7FEFFFFFFFFFFFFF at00400000,7FEFFFFFFFFFFFFA at00800000/00C00000. Minimum-normal decimal yields0010000000000000 at0/00400000,000FFFFFFFFFFFFE at00800000/00C00000, zero at01000000 (FZ). That FZ zero returns fail0/errno77, whereas3e-324 atFZ returns fail4/errno34. Thus neither mode invariance nor “zero always sets range error” is supported.

Additional locale writer C:00056B38..56BDC (`locale::global`): retain old locale for returned value, retain new implementation, release global old reference, **install new global pointer before checking its name**. Exact one-byte name `*` skips setlocale. Other names reach56BD0 r0=6 /56BD2 call imported setlocale(6,name); return is unused. This shipped function can change both C++ facets and C runtime locale. It does not prove the firmware path calls it; callers/order remain UNKNOWN. No current manifest contradiction is asserted.

An independent exact-rational check (`20261005-jsoncpp-rational-check.py` / `.txt`) validates the single nearest-mode candidate-plus-ULP operation and exponent restore:7C9FFFFFFFFFFFFC →7C9FFFFFFFFFFFFD →7FEFFFFFFFFFFFFD. It uses integer fractions and explicit nearest-even rounding, not Unicorn or a host decimal parser. This narrows the anomaly investigation but does **not** validate its initial candidate, approximation helpers, ratio or production FP mode.

The new checked slices reduce the missing work, but a complete converter specification, independent near-max validation, allocation-failure composition and loader-thread locale/FPSCR provenance remain PARTIAL. Manager review is still required before building.

## Manager handoff: what is and is not ready

- Recheck/adopt R1–R15, L1–L4's bounded initialization/facet slices, F1–F8, integer formatting and the numeric failure gates against the companion's **named binaries**. They close several precise reader/formatter MISSINGs without guessing a decimal library.
- Keep M1-029 IMPLEMENTATION_GAP. This report does **not** supply a complete checked numerical specification for D8–D12. Reopen the central correction loop `C:0007ED72..0007F1E6`, its B2 helpers, complete tiny-power constants/scaling, and directed/FZ FP modes; independently establish the near-DBL_MAX probe before using it as an oracle.
- Establish actual C++ global facet mutations and FP initialization on the loader thread; establish C-locale initialization/writers before treating formatting punctuation as fixed. Classic initialization alone is not a complete production proof.
- Keep allocation-failure/sentinel behavior UNKNOWN until its downstream effects are traced. Fixture malloc success is not a recovered failure rule.
- Phone `snprintf`/classification/ctype tables can receive a **proposed separate external-boundary record** with E call addresses and exact parameters/post-processing. Only the manager can classify/approve that boundary; it cannot swallow the shipped libc++ converter.
- Final exception destination remains MISSING by the operator's instruction. Nothing here selects ordinary parse-false/log-and-continue or termination for an escaping RuntimeError/LogicError.

No whole-record contradiction or status change is proposed. The quoted record is appropriately incomplete. The most consequential newly established boundary is that **input conversion is packaged code, while the formatter's snprintf is imported phone code**. Partial rows and controlled probes must not be joined into a complete-reader claim.
