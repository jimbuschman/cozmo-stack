# Independent check: M6 live-audio bodies and the B-M6b-4 C# build

Date: 2026-09-30  
Research lane only; no code, manifest, or inventory change.  
Binary: `libcozmoEngine.so`, SHA-256 `02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1`.  
C# revision: `48f756af8e5207b5325562766baa4a2287ccb559`.

## Verdict rules

- **HOLDS**: the row's behavior-changing claim, cited address, branch direction, order, result and any float word agree with the ARM instructions. For the C# table, all behavior stated by that correction is represented in the inspected component, with any source-unknown callee remaining an explicit required seam.
- **WRONG**: at least one behavior-changing address, gate, order, result, float word, or C# behavior is contradicted or omitted.
- **UNVERIFIABLE**: the row depends on bank census, an unread callee, an absence claim not bounded by the binary, or another fact that `libcozmoEngine.so`/the C# at HEAD cannot establish.

I disassembled the cited ARM ranges directly, followed their callers/branches and checked PC-relative literals as raw words. The source-report verdict is against the binary, not against the later C23-C29 corrections. Consequently, a report row remains WRONG even where a later correction repaired it.

## Result

The Sonnet report is not safe to use verbatim: **155 HOLDS, 23 WRONG, 13 UNVERIFIABLE** across 191 data rows. The important failures are not cosmetic. They include the deferred-start comparison gate (item 1 row 3), connection matching/reparenting (rows 12/15/21), NaN direction and missing gates in the playback-limit path, wrong failure-result/null rules, incomplete AddSrc and fade ordering, four container-body errors, and three incorrect bank censuses.

For the 49 correction rows C24-C29 at HEAD: **35 HOLDS locally, 10 WRONG, 4 UNVERIFIABLE**. The largest exactness defects are:

1. `IWwiseVoiceSource.StartStream()` is boolean, so C27 step 7 cannot preserve a native raw failure result and drops the native `pbi+0x1DC/+0x1E0` arguments.
2. Several source-recovered bodies are still caller-supplied seams (`0xA54A30`, `0xA4F0EC`, `0x9EA23C`) rather than owned implementations.
3. The connection descriptor cannot return native allocation failure 2.
4. The music count variant `0xA381F4` is absent.
5. `AdvanceTickCounters`, `DuckPrePass`, and `NodeCleanup` are optional delegates, so the native frame steps can silently disappear.
6. No production code constructs or wires `WwisePlaybackBridge`, `WwiseVoiceLinker`, `WwisePlaybackLimiter`, or `WwiseVoiceBusPass`; every construction/wiring hit is in tests. Thus the component-local HOLDS rows do **not** establish ownership of the complete production path. M6-025/M6-026 correctly remain `IMPLEMENTATION_GAP`.

## Source report: item 1, per-voice bus and connection creation

| row | verdict | reason when not HOLDS |
|---|---|---|
| 1 | HOLDS | |
| 2 | HOLDS | |
| 3 | **WRONG** | `0xA544BC` applies the rounded-window comparison after StartStream result **1**, not after `0x3F`; the `0x3F` path instead tests the signed offset and includes the bit5/`+0x1F8` dead-PBI gate. |
| 4 | HOLDS | |
| 5 | HOLDS | |
| 6 | HOLDS | |
| 7 | HOLDS | The static caller absence remains explicitly bounded; the stated main-device construction/default mask hold. |
| 8 | HOLDS | |
| 9 | HOLDS | |
| 10 | HOLDS | |
| 11 | HOLDS | |
| 12 | **WRONG** | When both bus pointers are null the key2 comparison is skipped too, and the array count is only masked for its empty test. |
| 13 | HOLDS | |
| 14 | HOLDS | The row's branch/return claims hold; its admitted `0xA4F0EC` body gap is not silently upgraded. |
| 15 | **WRONG** | The new default line is already appended by `0xA42210`; `0xA42754` moves it to index 0, and only the first qualifying main-device line is reparented. |
| 16 | HOLDS | The first found chain line is the one stored; no contradiction to the row's `r3` description. |
| 17 | HOLDS | `1.0f` is exactly `0x3F800000`. |
| 18 | HOLDS | |
| 19 | HOLDS | |
| 20 | HOLDS | |
| 21 | **WRONG** | `+0x1CC` bit0 is set for both a reused and a newly created aux line, not only reuse. |
| 22 | HOLDS | |

## Source report: item 7, Sound special branch and PBI fields

### A. Sound `PlayInternal`

| row | verdict | reason when not HOLDS |
|---|---|---|
| A1 | HOLDS | |
| A2 | HOLDS | |
| A3 | HOLDS | |
| A3a | HOLDS | |
| A3b | HOLDS | |
| A4 | HOLDS | |
| A5 | HOLDS | Literal words are `69.0f=0x428A0000`, `12.0f=0x41400000`, `440.0f=0x43DC0000`; `2.0f` is constructed as `0x40000000`. |
| A6 | HOLDS | |
| A7 | HOLDS | |
| A8 | HOLDS | |
| A9 | **WRONG** | It compresses distinct code-1/code-2 consumer gates and omits behavior-changing branches in the list-B note-off path. |
| A10 | HOLDS | The body holds; the caller is explicitly left as a bounded recoverable gap. |
| A11 | HOLDS | |
| A12 | **UNVERIFIABLE** | This is a six-bank HIRC census, not a fact established by `libcozmoEngine.so`; its stated scan is also deliberately incomplete for other RTPC-list classes. |

### B-D. Context block, PBI fields and `0x9BC90C`

| row | verdict | reason when not HOLDS |
|---|---|---|
| B1 | **UNVERIFIABLE** | It is an absence claim over selected paths and labels the field UNKNOWN; the binary search does not prove no other indirect callee reads/writes it. |
| B2 | HOLDS | |
| B3 | HOLDS | |
| B4 | HOLDS | The copy is exact; the row correctly leaves a reader as a gap. |
| C1 | HOLDS | The native operation is a 32-bit load/store; this row correctly contradicts the byte-sized C# field. |
| C2 | HOLDS | |
| C3 | HOLDS | |
| C4 | HOLDS | |
| C5 | HOLDS | |
| C6 | HOLDS | |
| C7 | HOLDS | |
| C8 | HOLDS | |
| D1 | HOLDS | |
| D2 | HOLDS | |
| D3 | HOLDS | |
| D4 | **WRONG** | `this=pbi+0xC`; the `str [r4,#0xB8]` is therefore `pbi+0xC4`, not `pbi+0xB8`. The literal itself is `101.0f=0x42CA0000`. |
| D5 | HOLDS | |

## Source report: items 2 and 3

### 2A. `0x9BEB30`

| row | verdict | reason when not HOLDS |
|---|---|---|
| A1 | HOLDS | |
| A2 | HOLDS | |
| A3 | HOLDS | |
| A4 | HOLDS | |
| A5 | HOLDS | |
| A6 | HOLDS | |
| A7 | HOLDS | |
| A8 | HOLDS | |
| A9 | **WRONG** | The threshold comparison treats unordered/NaN as **not below**; the report states the opposite. |
| A10 | **WRONG** | It omits the behavior-changing `arg2 <= global` guard. |
| A11 | HOLDS | |
| A12 | **UNVERIFIABLE** | Its 3D/unreachable conclusion depends on an unread branch and bank reachability rather than the cited native body alone. |

### 2B. Playback-limit walker

| row | verdict | reason when not HOLDS |
|---|---|---|
| B1 | HOLDS | |
| B2 | HOLDS | |
| B3 | HOLDS | |
| B4 | **WRONG** | The generic walk omits the `r3`/argument skip gate. It happens not to fire for the stated Sound call, but it changes the claimed body. |
| B5 | HOLDS | |
| B6 | HOLDS | |
| B7 | **WRONG** | The non-zero-max path also subscribes through `0xA19ECC`; the stated order is incomplete. |
| B8 | HOLDS | |
| B9 | HOLDS | |
| B10 | HOLDS | `101.0f` is `0x42CA0000`. |
| B11 | **UNVERIFIABLE** | The fade/kill tail was explicitly unread in this extraction. |
| B12 | **UNVERIFIABLE** | Descending order and tie behavior were inference here, not recovered instructions. |
| B13 | **UNVERIFIABLE** | Per-object limiter internals were not read. |
| B14 | **UNVERIFIABLE** | Bank census, not a `.so` fact. |
| B15 | **UNVERIFIABLE** | The body was not read. |

### 2C. Play tail

| row | verdict | reason when not HOLDS |
|---|---|---|
| C1 | HOLDS | |
| C2 | **WRONG** | When min equals max the native still adds min; the report says the add occurs only when they differ. The float representation of the nominal `2147483647` bound rounds to `0x4F000000`. |
| C3 | HOLDS | |
| C4 | **UNVERIFIABLE** | The tail and the claimed unreachability were not established from the complete native path. |
| C5 | HOLDS | |
| C6 | **WRONG** | The PBI is appended to the global list before the limiter iteration; that behavior-changing order is omitted. |

### Item 3. Media descriptor/refinement

| row | verdict | reason when not HOLDS |
|---|---|---|
| D1 | **WRONG** | The first four output pointers are written unconditionally; they are not nullable as claimed. |
| D2 | HOLDS | |
| D3 | HOLDS | |
| D4 | HOLDS | |
| D5 | HOLDS | |
| D6 | HOLDS | |
| D7 | HOLDS | |
| D8 | **WRONG** | The “no shipped cue” claim is false; the bank census has one Music cue. |
| D9 | HOLDS | |
| E1 | HOLDS | |
| E2 | **WRONG** | The census says six non-RIFF objects; the independent parse yields 46. |
| E3 | HOLDS | |
| E4 | HOLDS | |
| E5 | HOLDS | The stated fetch rule holds; the row appropriately leaves release/mode-1 semantics open. |
| E6 | HOLDS | |

## Source report: items 4 and 5

### Item 4. Fade-in and transition path

| row | verdict | reason when not HOLDS |
|---|---|---|
| 4.1 | HOLDS | |
| 4.2 | HOLDS | |
| 4.3 | HOLDS | `0.0f=0x00000000`; `1.0f=0x3F800000`. |
| 4.4 | HOLDS | |
| 4.5 | HOLDS | |
| 4.6 | HOLDS | |
| 4.7 | HOLDS | |
| 4.8 | HOLDS | |
| 4.9 | HOLDS | |
| 4.10 | HOLDS | |
| 4.11 | HOLDS | |
| 4.12 | HOLDS | |
| 4.13 | HOLDS | |
| 4.14 | HOLDS | |
| 4.15 | HOLDS | |
| 4.16 | HOLDS | |
| 4.17 | HOLDS | |
| 4.18 | HOLDS | |
| 4.19 | HOLDS | |
| 4.20 | HOLDS | |
| 4.21 | HOLDS | |
| 4.22 | HOLDS | |
| 4.23 | **WRONG** | The no-op conclusion holds, but one cited address is wrong (`0x103AEE8`); the request requires every address to hold. |
| 4.24 | HOLDS | |
| 4.25 | **WRONG** | It says seven of nine vtables use `0xA72B14`; the actual census is eight of nine plus a tenth source vtable. The final bit2-store hop was also not reverified. |

### Item 5. Link, pending list and teardown

| row | verdict | reason when not HOLDS |
|---|---|---|
| 5.1 | HOLDS | |
| 5.2 | HOLDS | |
| 5.3 | HOLDS | |
| 5.4 | HOLDS | |
| 5.5 | HOLDS | |
| 5.6 | HOLDS | |
| 5.7 | **WRONG** | There are three bit6 writers, not two; the inherited-parent writer is omitted. |
| 5.8 | HOLDS | |
| 5.9 | HOLDS | |
| 5.10 | HOLDS | |
| 5.11 | HOLDS | |
| 5.12 | HOLDS | |
| 5.13 | **WRONG** | The rounded-window comparison is on result 1, and the complete `0x3F` signed-offset/dead-PBI gating and source-bit side effect are not stated exactly. |
| 5.14 | HOLDS | |
| 5.15 | HOLDS | |
| 5.16 | HOLDS | |
| 5.17 | HOLDS | |
| 5.18 | HOLDS | |
| 5.19 | HOLDS | |
| 5.20 | **UNVERIFIABLE** | The shell calls are visible, but “frees the buffers” depends on unread callees `0xA69A38/0xA69AC8/0xA47360`. |

The direct per-connection pass uses `100.0f=0x42C80000` at `0xA4BD88..0xA4BDAC`. This is distinct from the `101.0f=0x42CA0000` victim/PBI sentinel used elsewhere.

## Source report: item 6, container `PlayInternal` bodies

### RanSeq

| row | verdict | reason when not HOLDS |
|---|---|---|
| R1 | HOLDS | |
| R2 | HOLDS | |
| R3 | HOLDS | |
| R4 | HOLDS | |
| R5 | HOLDS | |
| R6 | HOLDS | |
| R7 | HOLDS | |
| R8 | HOLDS | The null/empty return-2 claim is proved; the undecoded continuation tail remains explicit. |
| R9 | HOLDS | |
| R10 | HOLDS | |
| R11 | HOLDS | |
| R12 | **UNVERIFIABLE** | Bank census, not a `.so` fact. |
| R13 | HOLDS | |
| R14 | HOLDS | |
| R15 | **WRONG** | The snapshot/restore block is not the reported 0x44-byte shape; the cited restore/order is incomplete. |

### Switch

| row | verdict | reason when not HOLDS |
|---|---|---|
| S1 | HOLDS | |
| S2 | HOLDS | |
| S3 | HOLDS | The group/default behavior is exact; key precedence remains explicitly open. |
| S4 | HOLDS | The registration gate/order holds; vtable meanings remain UNKNOWN as stated. |
| S5 | HOLDS | |
| S6 | HOLDS | |
| S7 | HOLDS | The caller comparison/count holds; helper bodies remain explicit gaps. |
| S8 | HOLDS | |
| S9 | HOLDS | |
| S10 | HOLDS | |
| S11 | HOLDS | |
| S12 | HOLDS | |
| S13 | HOLDS | |
| S14 | HOLDS | The launch shape holds; unread continuous helpers remain explicit. |
| S15 | HOLDS | |
| S16 | **UNVERIFIABLE** | Bank census, not a `.so` fact. |

### ActorMixer, Layer and shared helper

| row | verdict | reason when not HOLDS |
|---|---|---|
| ActorMixer A1 | HOLDS | |
| ActorMixer A2 | **UNVERIFIABLE** | Bank census, not a `.so` fact. |
| Layer L1 | HOLDS | |
| Layer L2 | HOLDS | The gate/return is exact; the validation body remains explicitly unread. |
| Layer L3 | HOLDS | |
| Layer L4 | HOLDS | |
| Layer L5 | HOLDS | |
| Layer L6 | HOLDS | |
| Layer L7 | HOLDS | |
| Layer L8 | **WRONG** | The census is wrong: three sound-child layers, not two, and 29 RanSeq children, not 28. |
| C1.1 | HOLDS | |
| C1.2 | HOLDS | |
| C1.3 | **WRONG** | The tenth argument is previous `params+0x114`, not constant 0; additional zero arguments at E5/E6 are omitted. |
| C1.4 | **WRONG** | The stores are conditional, not unconditional as stated. |

## Float-word audit

These are the behavior-changing single-precision words encountered in the checked rows. All C# sites that explicitly use the fast-linear conversion preserve the raw words and non-fused operation order.

| value/use | native word | result |
|---|---:|---|
| 0.0 | `0x00000000` | HOLDS |
| 0.5 | `0x3F000000` | HOLDS |
| -0.5 | `0xBF000000` | HOLDS |
| 1.0 | `0x3F800000` | HOLDS |
| 2.0 | `0x40000000` | HOLDS |
| 12.0 | `0x41400000` | HOLDS |
| 69.0 | `0x428A0000` | HOLDS |
| 100.0 connection minima | `0x42C80000` | HOLDS in native; the C# value is 100f, although its comment incorrectly says `0x42CA0000` |
| 101.0 PBI/limiter sentinel | `0x42CA0000` | HOLDS |
| 440.0 | `0x43DC0000` | HOLDS |
| 0.05 | `0x3D4CCCCD` | HOLDS |
| -37.0 | `0xC2140000` | HOLDS |
| fast-linear scale | `0x4BD49A78` | HOLDS |
| fast-linear exponent bias | `0x4E7E0000` | HOLDS |
| polynomial c2 | `0x3EA67F46` | HOLDS |
| polynomial c1 | `0x3CAA70DE` | HOLDS |
| polynomial c0 | `0x3F272DDB` | HOLDS |
| below threshold, 2^-16 | `0x37800000` | HOLDS; unordered is false |
| nominal 2147483647 converted to float | `0x4F000000` | HOLDS as the rounded IEEE-754 word |

## C24-C29 against C# at HEAD

These verdicts are row-local. A HOLDS here means the inspected component implements the correction's known control flow; it does not cure the missing production composition noted above.

### C24

| row | verdict | C# finding |
|---|---|---|
| C24.1 | HOLDS | `WalkPendingVoices` uses the owner PBI, bit5/`-1` retirement gate, bit7/negative-offset gate, unsigned frame count and native `+0.5/-0.5` truncation rule. |
| C24.2 | **WRONG** | The now-recovered `0xA54A30` body is not implemented; `InitVoiceA54A30` is still a required caller seam and is described as a RECOVERABLE_GAP. |
| C24.3 | **WRONG** | Config selection is present, but the recovered line Init, allocation/failure results and object construction remain `InitLineA4F0EC`, a caller-supplied seam. |
| C24.4 | **WRONG** | Descriptor layout/size/swap order holds, but `Reserve` cannot produce native allocation-failure result 2; it explicitly assumes managed allocation cannot fail. |
| C24.5 | **WRONG** | Key selection/caller scan holds, but recovered `0x9EA23C` find/insert/build/failure behavior is delegated rather than owned. |
| C24.6 | **UNVERIFIABLE** | The known null-bus/counter/order shell is present; `0x9C39DC` and the mix-object behavior remain external seams with no production binding to inspect. |
| C24.7 | HOLDS | Dirty-gated pass 1, unconditional pass 2, result 2/`0x3F`/1 handling, group restart and state dispatch match; source-unknown state bodies throw through required seams. |
| C24.8 | HOLDS | Parentless-device cases, default-line index move, first-line reparent, field alias, failure results and call order match. |
| C24.9 | **WRONG** | Routing fields are accepted as model inputs; the recovered registration/default writer path and its production ownership are absent. |
| C24.10 | HOLDS | Both new/reused source keys start at zero and negative values clamp to zero. |

### C25

| row | verdict | C# finding |
|---|---|---|
| C25.1 | HOLDS | New voice State and output gain start at zero. |
| C25.2 | HOLDS | Live-voice lookup, fallback and default-state behavior match. |
| C25.3 | HOLDS | Pass-1 bit5/`-1` unlink/free/notify order is represented. |
| C25.4 | HOLDS | Owner/current-source stores occur only on the new path; reuse writes Pending and returns. |
| C25.5 | HOLDS | `Word0xF0` starts at zero and takes the PBI source format during init. |
| C25.6 | **WRONG** | The caller's pre-scan is represented, but `0x9EA23C`'s recovered hit-rebuild/miss-append behavior is still not owned. |
| C25.7 | HOLDS | Frame count is `ushort`; device/bus dereference follows the listener-mask gate. |

### C26

| row | verdict | C# finding |
|---|---|---|
| C26.1 | HOLDS | Current-source comparison applies to every node, pending only to type 4; fallback and bit update agree. |
| C26.2 | HOLDS | Non-zero/bit-clear advances to the next node and restarts the group pre-scan there, not at the head. |
| C26.3 | HOLDS | Pass 1 leaves type >1 attached and unfreed. |
| C26.4 | **WRONG** | Superseded by C27; current C# still cannot preserve arbitrary raw StartStream failure codes/arguments. |
| C26.5 | **UNVERIFIABLE** | HEAD requires `SourceFormatWriter15C`, but no production source-class binding exists to verify the overwrite and precise in-vfunc ordering. |
| C26.6 | HOLDS | Unlink/free precedes notification, and descriptor reserve/half-clear occurs only on reinit. |

### C27, complete AddSrc

| step | verdict | C# finding |
|---|---|---|
| 1 | **UNVERIFIABLE** | Factory/refinement bodies remain explicit seams; their production implementation is absent. |
| 2 | HOLDS | Null source notifies/clears and returns 2 before other stores. |
| 3 | HOLDS | Owner registration and `Field154=voice` precede all later work. |
| 4 | HOLDS | New-path cache, 0x4C allocation, failure result 2 and destruction order match. |
| 5 | HOLDS | Gate branches, result 3, conditional `A0228C`, bit clear and skipped StartStream order match. |
| 6 | HOLDS | Reuse-path cached load feeds the shared gate. |
| 7 | **WRONG** | `StartStream()` is boolean. False becomes 0, so raw non-1/non-`0x3F` results are lost; `pbi+0x1DC/+0x1E0` are not passed. The C# source itself marks both unresolved. |
| 8 | HOLDS | For representable codes, failure closes/clears/notifies, destructs, frees and returns in native order. |
| 9 | HOLDS | Reuse stores Pending only and returns the code. |
| 10 | HOLDS | New success stores current source/owner, clears PBI bit3 and returns. |

### C28

| row | verdict | C# finding |
|---|---|---|
| C28.1 | HOLDS | Descending priority, sequence tie direction, eligibility and victim choice match. |
| C28.2 | HOLDS | One-time reason marking, paused/stop-item gates, transition re-aim/direct-zero paths and delayed voice destruction match. |
| C28.3 | HOLDS | Null candidate returns 0; success follows kill. |
| C28.4 | HOLDS | Caller reasons and dead functions are represented consistently. |
| C28.5 | HOLDS | Failure callback/Term and success-tail order match, with unread callees explicit. |
| C28.6 | **WRONG** | The third inline count increment is covered, but the music variant `0xA381F4`/its `n <= max` early success path has no C# implementation. |
| C28.7 | HOLDS | Correct Switch word and subscriber-vtable identities are used as labels/citations, not changed behavior. |
| C28.8 | HOLDS | Parsed advanced/max-instance fields feed the implemented limiter path; the row remains bank authority, not `.so` authority. |

### C29

| row | verdict | C# finding |
|---|---|---|
| C29.1 | HOLDS | Removal, count undo, list/map update, context clear and Term order match the correction. |
| C29.2 | HOLDS | Five failure exits, bit5 split, callback-before-Term and later-scan skip masks match. |
| C29.3 | HOLDS | Context insertion, PBI validity result, limiter inserts, start-list allocation failure and fade tail order match. |
| C29.4 | HOLDS | `below`, byte formula, argument choice and fast-linear constants/order are bit-exact; NaN is not below. |
| C29.5 | HOLDS | Remove/reposition/tie behavior and NaN mid behavior match. |
| C29.6 | **WRONG** | `AdvanceTickCounters`, `DuckPrePass` and `NodeCleanup` are nullable and invoked with `?.Invoke()`. No production assignment exists, so required native frame steps can be silently skipped; only tests wire them. |
| C29.7 | HOLDS | Initial route registers, object-scope stable identity and bus-category flag are represented. |
| C29.8 | **UNVERIFIABLE** | The known acquire/release/re-registration shell is represented, but the intervening Term/re-registration bodies remain required seams without a production binding. |

## Production ownership and test quality

The repository-wide C# reference search found no production construction of the four B-M6b-4 live-audio owners and no production assignment of their behavior-changing seams. The only `new WwisePlaybackBridge`, `new WwiseVoiceLinker`, `new WwisePlaybackLimiter`, and frame-hook assignments are under `cozmo-stack/tests`. Therefore the complete Play -> PBI -> source -> connection -> frame pass -> removal path is not owned at HEAD.

The tests do exercise many exact local gates and orders, including pending-voice timing, linker routing, limiter ordering and float helpers. They do not cure the ownership defect because test fixtures themselves supply the missing composition and seams. Two tests also encode the boolean StartStream abstraction, so they cannot detect loss of an arbitrary native failure code. This is the same calibration failure pattern: source-backed pieces are individually present, but unowned behavior-changing links sit between them.

## Bottom line

- Use C23-C29, not the Sonnet report alone, as the corrected evidence trail.
- Do not settle M6-025 or M6-026 from B-M6b-4 at this HEAD.
- The next bounded work is not another broad extraction: repair/own the exact C24.2/C24.3/C24.5 bodies, the C27 raw StartStream contract and arguments, C28.6's music variant, and C29.6's mandatory production composition; then recheck only those affected paths.
