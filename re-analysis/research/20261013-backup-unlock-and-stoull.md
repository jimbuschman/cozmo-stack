# R-BACKUP-UNLOCK — backup unlock handler and conversion dependencies

Answers `requests/20261013-backup-unlock-and-stoull.md`. Independent extraction; tables precede the detailed results. Addresses are ELF virtual addresses, with Thumb bits removed. `E:` means shipped `libcozmoEngine.so`; `C:` means shipped `libc++_shared.so`. These are proposed evidence for manager verification, not manifest classifications.

## Coverage 1 — RequestUnlockDataFromBackup

| Step | Recovered behavior | Primary address or boundary |
|---|---|---|
| Subscription gate | Constructor skips registration block if context external-interface pointer is null. | E: `0x005257E8..0x005257EE`, skip destination `0x005259B0` |
| Register and retain | Registers game-to-engine tag `0x6E` through slot `+0x2C`; retains scoped handle in constructor's handle vector. | E: `0x00525970`, helper `0x005270E8..0x00527146` |
| Second entry | Template handler forwards message and stored context to the same static backup handler. | E: `0x00529BC0..0x00529BC6`; veneer `0x008CB08C` targets PLT `0x004A92D4` |
| Context rejection | Requires context, external interface at `+4`, and DataPlatform at `+8`; logs error, sets errG and optionally debug-breaks; no status broadcast. | E: `0x0051C5F2..0x0051C60E`, `0x0051C7C8..0x0051C820` |
| Select and parse | Builds persistent resource path `robotBackups/`, selects file, parses local map, searches tag `0x182000`. | E: `0x0051C612..0x0051C6A0` |
| Unpack | Up to 512 raw four-byte reads; first failed read stops, unwritten destination entries remain untouched. Returns consumed bytes. | E: `0x00795E74..0x00795EA4`, `0x00795DA6..0x00795DD6`, ReadBytes `0x0083C09A..0x0083C0CA` |
| Collect | Ignores Unpack result. Collects first nonzero entries up to first zero or 512; preserves order, duplicates and unknown enum values. | E: `0x0051C6AE..0x0051C6E0` |
| Default dependency | Returns global ordered set; if set size zero, calls InitConfig, then returns set regardless of result. | E: `0x0064C09C..0x0064C0C8`; InitConfig `0x0064BB68..0x0064BDB4` |
| Backup success | Logs found file, constructs UnlockStatus with collected vector and fromBackup=true, broadcasts once. | E: `0x0051C6E2..0x0051C75E` |
| Defined fallback | No selected backup, parse failure, or missing tag logs respective Info event, uses default set, also fromBackup=true. | E: `0x0051C822..0x0051C8C4` |
| Concrete +1C target | UiMessageHandler rvalue Broadcast, immediate delivery then timestamped local signal dispatch. | Vtable relocation E: `0x0102FE38` → `0x006625B1`; body `0x006625B0..0x00662628`, dispatch helper `0x00662590..0x006625AE` |
| Cleanup | Clears message; deletes temporary vector storage; frees path/file strings when long; destroys local map; checks stack canary. | E: `0x0051C760..0x0051C7C6`, `0x0051C8C6..0x0051C944`, especially `0x0051C8FE..0x0051C928` |
| Malformed short unlock data | Can read uninitialized stack entries; no safe failure result recovered. Existing M1-034 standing rule applies. | First potentially unwritten read E: `0x0051C6BC` |

## Coverage 2 — std::stoull

| Step | Recovered behavior | Primary address or boundary |
|---|---|---|
| String and base | Takes c_str from short/long string representation; passes caller base unchanged, not hardcoded ten. Backup enumeration supplies ten. | C: `0x000653A8..0x000653CA`; E: calls `0x0051C336`, `0x0051C374` |
| errno protocol | Saves errno, zeroes it, calls strtoull, saves parse errno, restores prior errno before checking errors. | C: `0x000653B4..0x000653E2` |
| Whitespace/sign/digits | No wrapper trimming or validation. Delegated entirely to imported bionic strtoull. | C: `0x000653CA` → PLT `0x000327EC`; phone implementation/version **UNKNOWN**, not shipped |
| Range error | Parse errno==34 takes precedence over no-conversion test; throws std::out_of_range with `stoull: out of range`. | C: `0x000653E4..0x000653E6`, `0x00065428..0x0006545A` |
| No conversion | end pointer equals original c_str: throws std::invalid_argument with `stoull: no conversion`. | C: `0x000653E8..0x000653EC`, `0x0006545E..0x00065490` |
| Success/index | If position pointer nonnull, stores byte offset end−begin as 32-bit size_t. Returns full u64; accepts trailing nonnumeric suffix. | C: `0x000653EE..0x00065422` |
| Other errors/cleanup | No other errno test and no additional u64 range check. Index untouched on throw; exception construction failure frees exception storage; temporaries destroyed and unwind resumed. | C: `0x00065494..0x000654DE` |

## Coverage 3 — __next_prime

| Step | Recovered behavior | Primary address |
|---|---|---|
| Small requests | Unsigned n≤211: lower_bound in 48-entry prime table, including zero. | C: `0x0003B038..0x0003B072`; table `0x0009B480` |
| Overflow guard | n≥0xFFFFFFFC throws std::overflow_error, message `__next_prime overflow`. | C: `0x0003B074..0x0003B078`, `0x0003B622..0x0003B648` |
| Candidate start | q=floor(n/210), remainder=n−210q; lower_bound remainder in wheel residues, candidate=210q+residue. | C: `0x0003B07C..0x0003B0FA`; residues `0x0009B540` |
| Small trial divisors | Table indexes 5..46, primes 11..199. If candidate/divisor < divisor, accept; otherwise reject if product of quotient and divisor equals candidate. | C: `0x0003B0FC..0x0003B11A` |
| Large trial divisors | Starting divisor 211, visits the 210-wheel in increasing order; same quotient test, then exact divisibility test. | C: `0x0003B11C..0x0003B5F8` |
| Rejected candidate | Advance residue index; wrap 48→0 and increment q; repeat tests. | C: `0x0003B0DA..0x0003B0F4`, `0x0003B5FA..0x0003B610` |
| Success | Returns accepted candidate; integer-only, no floating sqrt, randomization or logs. | C: `0x0003B616..0x0003B620` |

## Coverage 4 — JsonCpp conversion and member-name errors

| Value type | asUInt result / exception | Primary address |
|---|---|---|
| null (0) | 0 | E: dispatch `0x008E89C2..0x008E89D4`, return `0x008E8AC8` |
| signed integer (1) | If upper 32 bits zero, low word; otherwise Json::LogicError, `LargestInt out of UInt range`. | E: `0x008E89D6..0x008E8A8A`, isUInt `0x008E8FC0..0x008E8FCC` |
| unsigned integer (2) | Same upper-word test; failure Json::LogicError, `LargestUInt out of UInt range`. | E: `0x008E8A92..0x008E8A9E`, `0x008E8B7E..0x008E8C1E` |
| real (3) | Ordered range 0..4294967295 inclusive; VCVT truncates to u32. Fractional values accepted. NaN/infinity/outside range throw `double out of UInt range`. | E: `0x008E8AA0..0x008E8AC4`, `0x008E8C22..0x008E8CC2` |
| bool (5) | Returns stored byte (valid values 0/1). | E: TBB dispatch `0x008E89CC..0x008E89D4` to `0x008E8A8E` |
| string (4), array (6), object (7) | Json::LogicError, `Value is not convertible to UInt.` No empty-value exception to rule. | E: `0x008E8AD0..0x008E8B7A` |
| getMemberNames(null) | Empty vector | E: `0x008E6B9A..0x008E6BA4`, `0x008E6C38..0x008E6C42` |
| getMemberNames(object) | Tree iteration copies key bytes/length into vector; empty object succeeds. | E: `0x008E6BA6..0x008E6C62` |
| getMemberNames(other) | Even an empty array or empty string throws Json::LogicError, `in Json::Value::getMemberNames(), value must be objectValue`. | E: `0x008E6B9E..0x008E6BA0`, error construction `0x008E6C68..0x008E6D08` |
| Throw type | Allocates Json::LogicError, constructs it from string and calls __cxa_throw with its typeinfo. No DAS event here. | E: `0x008E7A7C..0x008E7A9C` |

## 1. Unlock production path

The constructor test is `[context+4] != 0`, not SDK mode, a connected robot, or valid backup availability. The action registration loop and subsequent helpers, including call `0x00525970`, sit inside that gate. The helper registers tag 0x6E, moves the returned shared handle into the persistent vector (or its slow append), then releases only temporary ownership. The second entry `0x00529BC0` is a forwarding template specialization, not a second parser: it loads context from the handler, passes the message in r0 and context in r1, and tail-calls the static function through a veneer/PLT.

A valid context initializes local unordered_map with max_load_factor 1, then builds its backup directory, selects a filename and parses it. Only result **exactly 1** counts as success for selection and parsing. The lookup key is the immediate `0x182000`; it is not a decompiler string address. The handler's input message has no field-dependent branch here.

`UnlockedIdsList` has 512 u32/UnlockId entries, serialized without an array-count prefix. The byte-pointer wrapper builds a non-owning SafeMessageBuffer, invokes the buffer overload and returns its bytesRead. ReadBytes copies four bytes only when the bounds check succeeds; on failure it neither advances nor fills the destination. The destination at handler stack `sp+0x168` is **not initialized to 512 zero entries** before Unpack. The handler disregards the return value, then reads indices 0..511 until a zero. A complete 2048-byte block is well-defined; extra bytes after it are ignored by this unpacker. A short block is well-defined only insofar as a zero is encountered within the successfully initialized prefix. Without that, the read at `0x0051C6BC` reaches uninitialized stack contents. Do not reinterpret this as a guaranteed default-unlock fallback. Under the current standing M1-034 approval, skip the malformed unlock unit, preserve prior state, and visibly name M1-034/M15-043; manager must record the original address and chosen skip boundary.

On success, entries remain in backup order. There is no sorting, deduplication, enum validation, membership filtering, or limit of 255 in this handler; its native loop permits 512. `UnlockStatus`'s Unity serializer has an 8-bit array count and property limit 255 (`unity/scripts/csharp/Anki.Cozmo.ExternalInterface/UnlockStatus.cs:13..96`); that app-side limit must not be substituted for this native loop. Both success and all three **defined** fallbacks set fromBackup=true. The generated native union constructor `0x00726A0A..0x00726A2C` moves the vector, clears the source vector, writes message tag **0x6D**, and copies this bool to union+0x14.

Fallback defaults come from the process-global ordered set at `0x0105B560`. Its **size**, not an initialization flag, gates InitConfig; a legitimately empty set is attempted again on the next call. InitConfig tries resource-scope 1 `config/engine/unlock_config_nurture.json` (`0x0064BB82..0x0064BBA4`). If the global set is already nonempty it skips repopulation even though the read was attempted. Otherwise a non-null JSON root with array key `defaultUnlocks` is iterated. Each string goes through UnlockIdFromString, then ordered unique insertion (`0x0064BC74..0x0064BCB0`); nonstrings log InvalidDefaultData and continue. Missing/null/wrong-kind defaults log MissingDefaults and return false. Read failure logs UnlockConfigJsonNotFound; errors set errG and conditionally debug-break. GetDefaultUnlocks ignores InitConfig's return and returns the set. The vector constructor at `0x0051C8A6` walks set order. Enum name mapping is a separate generated dependency `0x00794D84`, not an invented hash of strings.

The installed UiMessageHandler vtable address point is `0x0102FE1C`; slot +0x1C is relocation `0x0102FE38`, explicitly naming the rvalue Broadcast at `0x006625B0`. It first calls virtual DeliverToGame at +0x48 with routing id -1 (`0x006625C6..0x006625CE`). Then it constructs an event from the tag, current timer seconds (f32 widened to f64) and moved union, and immediately dispatches the matching local signal through `0x00662590`. If no signal exists, the helper simply returns. This is not BroadcastDeferred. Downstream host/app delivery is the app boundary; an arbitrary replacement external-interface subclass is not assumed to have UiMessageHandler's implementation.

Success log: Info/Unnamed event `RobotDataBackupManager.HandleRequestUnlockDataFromBackup.UnlockData`, `Found unlock data in backup file %s`. Fallback Info/Unnamed events: `.NoBackup` (`No backup, sending default unlock data`), `.FailedToParseBackup` (`Failed to parse backup, sending default unlock data`), `.NoUnlockData` (`No unlock data in backup, sending default unlock data`). Null context emits Error `.NullContext`, sets errG, optional debug break, and returns without a message.

Cleanup is ordinary ownership cleanup, not another unlock operation. It clears the union immediately after synchronous broadcast, frees remaining temporary vector allocations, destroys the long directory string, destroys the long filename string, then local map. The arithmetic at `0x0051C8FE` adjusts a trivial-vector end pointer before delete; it does not erase unlock IDs from a global container or send a second status. Stack-canary mismatch calls __stack_chk_fail.

## 2. Numeric conversion and the system-library boundary

Exact wrapper order is: create diagnostic string `stoull`; obtain c_str; save errno; set errno=0; call imported strtoull(begin,&end,base); save u64 and new errno; restore old errno; test ERANGE; test end==begin; optionally write index; destroy diagnostic temporary; return u64. Error order matters: ERANGE wins even if end==begin. There is no rejection based on returned ULLONG_MAX alone. A successful conversion of that exact value is accepted. Embedded NUL terminates the imported conversion even if C++ string length is longer. Invalid base is not explicitly rejected by the wrapper: the import's end/errno determine which branch follows.

The phone's `strtoull` is not among shipped code; its exact OS revision is **UNKNOWN**. As public primary-source background only, [AOSP bionic android-7.1.2_r39 strtoull, lines 51–105](https://raw.githubusercontent.com/aosp-mirror/platform_bionic/android-7.1.2_r39/libc/upstream-openbsd/lib/libc/stdlib/strtoull.c) skips isspace bytes, consumes an optional sign, and accumulates base-ten digits using cutoff=ULLONG_MAX/10 and cutlim=ULLONG_MAX%10. Overflow saturates, sets ERANGE and continues consuming digits. A representable negative magnitude is unsigned-negated; no digits leaves end at original begin. With base ten, hexadecimal prefixes are not recognized. These semantics explain whitespace/sign/trailing-suffix handling but do **not** identify the operator phone's bionic build. This external system-library body is the legitimate EQUIVALENT_IMPLEMENTATION boundary; the shipped wrapper remains exact.

Consequences under that external contract: `"  +12x"` returns 12 with index 5; `"-1"` returns 18446744073709551615; `"0x10"` returns zero with index 1; `"+"` throws invalid_argument; `"18446744073709551616"` throws out_of_range. No index update occurs on either wrapper throw. Backup enumeration uses null index and base ten twice, then stores low u32 (`0x0051C382`); acceptance of a numeric prefix is therefore relevant even when the complete filename prefix is not decimal digits. Original uncaught conversion failures are covered by current M1-034, unlike the older context report's now-stale statement that further operator approval was needed.

## 3. Complete integer algorithm above 211

All quantities below are unsigned 32-bit size_t, not host 64-bit size_t. Small-prime table at C:`0x9B480` is:

`0,2,3,5,7,11,13,17,19,23,29,31,37,41,43,47,53,59,61,67,71,73,79,83,89,97,101,103,107,109,113,127,131,137,139,149,151,157,163,167,173,179,181,191,193,197,199,211`.

Wheel residues at C:`0x9B540` are:

`1,11,13,17,19,23,29,31,37,41,43,47,53,59,61,67,71,73,79,83,89,97,101,103,107,109,113,121,127,131,137,139,143,149,151,157,163,167,169,173,179,181,187,191,193,197,199,209`.

These are numbers coprime to 210, **not a table of primes**: 121,143,169,187,209 intentionally occur. For n>211 and n<0xFFFFFFFC:

1. Divide n by 210, lower_bound its remainder in the wheel. Form first candidate 210q+residue. The multiply-high sequence at `0x3B07C..0x3B096` implements that exact integer division.
2. Test divisors 11,13,...,199 from small-prime table indexes 5..46. For each d, compute t=floor(candidate/d). If t<d, return candidate. Otherwise if d*t==candidate, reject candidate. This quotient-before-product test avoids computing d*d with overflow.
3. If still unresolved, test wheel divisors 211,221,223,227,... with the same test. The unrolled first cycle uses offsets relative to 211: `0,10,12,16,18,22,28,30,36,40,42,46,52,58,60,66,70,72,78,82,88,96,100,102,106,108,112,120,126,130,136,138,142,148,150,156,162,166,168,172,178,180,186,190,192,196,198,208`; next cycle starts at +210. Composite divisors are intentionally tested. No sqrt is called.
4. On rejection, advance candidate wheel index; at 48 wrap to zero and increment q. Start over at small divisor 11. On acceptance, return current candidate.

Zero requests return zero in the small table. The input guard rejects 0xFFFFFFFC through 0xFFFFFFFF before candidate rounding; it does not replace them with a smaller prime or a power of two. There is no separate dynamic candidate-overflow branch in the loop. The last 32-bit prime, 4294967291, bounds the admissible top requests. Exception construction uses runtime_error base then overflow_error vtable/typeinfo, followed by __cxa_throw; exception-construction failure frees storage and resumes unwinding. This function settles prime rounding; container rehash policy (when it asks for a prime or uses a power-of-two bucket count) remains the caller's separate responsibility.

## 4. JsonCpp exact throw inputs

The value type byte is at Value+8. Integer payloads occupy eight bytes; isUInt for both signed and unsigned types only tests that the upper word is zero, which permits exactly 0..UINT32_MAX and rejects negative signed values. asUInt(real) does **not** call isUInt(real): the latter also tests integralness with modf, whereas asUInt accepts fractions and truncates. Its high-range VFP `bhi` also rejects unordered NaN; the low-range `blt` rejects negatives. Negative zero is accepted and produces zero. No string parsing or truthiness conversion of arrays/objects occurs.

The error text literals are at E:`0x008E8F42` (LargestInt), `0x008E8F00` (LargestUInt), `0x008E8EC4` (double), `0x008E8F80` (not convertible), and `0x008E6DC4` (getMemberNames). The character-sequence calls use lengths 28,29,24,33,59 respectively, excluding each literal's trailing NUL. The constructed diagnostic std::string contains the quoted text without an embedded terminator. All throw Json::LogicError, **not std::invalid_argument or std::out_of_range**. Stream/string allocation failures can also propagate their own exceptions; the helper has no recovery handler that converts failure to zero or an empty vector. Unwind cleanup only destroys temporary streams/strings.

getMemberNames checks type before checking container emptiness: every array throws, including `[]`; every scalar throws, including empty string, false and zero. Only null and object are permitted. An upstream caller that skips getMemberNames for an empty input may avoid it; that is a caller property, not this function's acceptance rule. The object branch reserves map size and iterates its ordered tree, copying each CZString key with stored byte length (metadata >>2), without losing embedded NULs. The existing backup selector consequently relies on JsonCpp key ordering, not file-enumeration order.

## Evidence, manifest impact and limits

Engine SHA-256: `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`. libc++ SHA-256: `8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a`. Local instruction extracts: `20261013-backup-unlock-and-stoull-{handler,json,defaults,send,extra,throw,libcpp,data}.txt`; extraction scripts are beside this report. Ghidra output was navigation only. No shipped code was executed or uploaded. Public source was fetched solely for the unshipped system-library boundary.

Current M15-043 title is **“Backup unread pieces: RequestUnlockDataFromBackup (0x6E) internals, std::stoull (libc++_shared), __next_prime above 211, JsonCpp throw paths”**, status **RECOVERABLE_GAP**, evidence names the prior backup-manager-rest/check reports and the exact ranges listed in this request. This report supplies those bodies for verification; it does not change that record. Remaining uncertainty is the phone bionic build, host/app delivery implementation beyond the external-interface boundary, and manager adoption/testing of the proposed behavior. No production change, test run, branch, commit or push was made. Suggested later regressions: subscription gate; three fallback events and true bool; zero-terminated/full/short unlock blocks; signed/prefix/overflow filename conversions; wheel prime boundaries; all JsonCpp types including NaN, fractional real and empty array.
