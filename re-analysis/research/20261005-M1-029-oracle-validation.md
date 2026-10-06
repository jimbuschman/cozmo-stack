| Requested follow-up | Coverage | Result |
| --- | --- | --- |
| Independent execution of finite mismatches | CHECKED | QEMU10.2.1 ARM Linux harness agrees with Unicorn on158 distinct inputs, including sampled finite discrepancies and special forms; bits/errno/end/wrapper state all match. |
| Independent numeric validation | CHECKED |116 cases;3,830 executed FP arithmetic/comparison/conversion checks and30,248 bigint helper checks pass with zero skipped checks. |
| Firmware loader thread and parsing callback | CHECKED | pthread entry, file-load callback closure, inline LoadHeaderData and Reader call reopened below. |
| Shipped FP reset on that scoped path | CHECKED | No FPSCR reset appears in reopened entry/callback bodies; existing package-wide ordinary-VMSR scan finds only TBB save/restore slices, not a loader reset. |
| Actual phone thread-entry FPSCR | PARTIAL | Depends on external pthread/host execution state; supplied package does not settle its value. No phone run was performed. |
| Locale acquisition and initialization | CHECKED | Reader constructs the current libc++ global locale; lazy initial global is classic. Setter and package import boundaries reopened/scanned. |
| Actual global facet value at concurrent firmware parsing | PARTIAL | Global mutation/interposition and external execution order remain UNKNOWN; no invariant-culture assumption is authorized. |
| Exact bounded build rows and substitution decision | CHECKED | Confirmed fixed-environment rows below reject a generic correctly-rounded replacement; do not unblock the whole record. |

# M1-029 oracle validation and loader environment — 2026-10-05

Answers “Do it”: independently validate the finite mismatches, trace the loader FP/locale boundary, and reduce confirmed findings to build rows. Research lane only. Production code, inventory, manifest and job status are unchanged; no hardware acceptance started. New files remain in research for manager review; no additional commit or push is made.

**Result:** the finite discrepancy is corroborated by a second execution engine and an independent integer/rational audit. It is no longer merely a Unicorn-only observation. Under the explicit nearest/no-FZ/no-DN fixture, shipped instructions return a non-nearest result for ordinary accepted decimals such as131.e-227. The manager's condition for substituting .NET therefore fails in that environment. This is not a claim that every phone execution environment has been measured.

## Current claim before drawing a conclusion

M1-029 remains “Firmware version check against the shipped firmware header”, **IMPLEMENTATION_GAP**, authority “libcozmoEngine.so 3.4.0-1204”. Its current evidence includes:

> G5.16..G5.20 RobotManager::Init -> FirmwareUpdater::LoadHeader starts a loader thread (0x0052E7F8; pthread_create 0x0067692A) that reads config/engine/firmware/cozmo.safe and parses the JSON header in the first 0x800 bytes (0x00677C44..0x00677D34); ParseFirmwareHeader stores version -> +0x84, time -> +0x88 (0x0052EA36..0x0052EA9A)

Current unresolved explicitly leaves “decimal/exponent/overflow real conversion via current locale and shipped libc++ num_get/rounding” MISSING. The complete claim is captured in the previous task's20261005-jsoncpp-oracle-record.json, reread against the unchanged current manifest. No contradiction or status upgrade of M1-029 is proposed. Prior experiment:20261005-M1-029-oracle-differential.md, committed28bfaa4.

E addresses below belong to libcozmoEngine.so; C addresses to packaged libc++_shared.so. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1; libc++ SHA2568ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a.

## Independent executor and numerical checks

A freestanding ARMv7 softfp Linux executable loads the actual packaged ELF at0x10000000, copies PT_LOAD segments, clears BSS, applies ARM_RELATIVE/ABS32/GLOB_DAT/JUMP_SLOT relocations and resolves explicit numeric imports. It calls BASE+7E571 and BASE+5EB99, resetting the module, allocation pointer, errno and FPSCR before each call. No glibc loader, host decimal parser or host float formatting is used. Integer-only output prints the returned r0/r1 bits and states.

Execution: QEMU ARM user10.2.1, CPUcortex-a9, FPSCR0. Cross compilergcc12.5.0 builds only the fixture; **the converter is still the shipped ARM machine code**. Successful bump allocation, ASCII classification/case comparison, byte memory operations, serial mutexes, guard/errno and the unused __cloc handle are explicit fixture boundaries matching the original experiment. Unknown imports/relocations and allocation exhaustion abort. The ELF is independently relocated, including a nonzero load base; this also checks the original oracle's base0 mapping/relocation assumption for these paths.

Dependencies were downloaded/extracted under `_jsoncpp_qemu_dependencies/`; no WSL system package installation or shipped-asset upload. Source/build instructions:20261005-jsoncpp-qemu.c,20261005-jsoncpp-qemu-run.sh,20261005-jsoncpp-qemu-setup.sh; comparison driver/results:20261005-jsoncpp-qemu-compare.py /20261005-jsoncpp-qemu-results.json. Tool/environment metadata:20261005-jsoncpp-validation-environment.txt. ARM fixture SHA2561fcbb857c456b4ae9d59906bbf05602e4701a40031a656b81c42388be9130575.

The116-case numerical sample includes the initial counterexamples, targeted boundary/ordinary values and100 deterministically sampled prior ordinary bit discrepancies (seed0x102905; duplicates removed). QEMU adds special forms, for158 distinct inputs total. All QEMU results agree exactly with the original oracle on raw bits, errno, consumed UTF-8 byte offset, normalized-wrapper bits/errno and failbit.

Separately,20261005-jsoncpp-independent-fp.py monitors each executed binary64 add/subtract/multiply/divide, comparison and in-range conversion. It computes expectations with exact rational/integer arithmetic, nearest-even remainder comparisons, exact subnormal units, overflow carry, signed-zero handling, exact comparison flags and integer truncation. It validates each actual next-instruction register/flag result. The helper audit also independently checks native bigint multiplication, multiply-add,5-power scaling, left shift, ordered difference/sign, magnitude comparison, binary64 decomposition (coefficient×2^exponent equals the input), and normalized top53-bit approximation/leading-limb bitcount. Helper input magnitudes are captured before mutation, and nested helper returns are tracked.

Results:3,830 FP checks and30,248 bigint checks pass; **zero skipped checks** for the116 selected inputs. Full per-operation/per-helper ledger:20261005-jsoncpp-independent-fp-results.json; console summary:20261005-jsoncpp-independent-fp-summary.txt. These do not validate every ARM instruction/control-flow operation, every FP exception flag or every possible input/mode. QEMU and Unicorn share historical QEMU ancestry; the integer/rational numerical reference is independent of that lineage. These limitations must remain visible rather than calling the whole runtime proven.

| Input | Raw bits in both executors | errno / end offset | Wrapper state | Mathematical nearest-even / .NET |
| --- | --- | --- | --- | --- |
|131.e-227|113F0886B36F1862|77 /9|0|113F0886B36F1861|
|346670056614373788204865159893.67e-122|2CBCECD3D4DB9AA3|77 /38|0|2CBCECD3D4DB9AA4|
|1.7976931348623157e308|7FEFFFFFFFFFFFFD|77 /22|0|7FEFFFFFFFFFFFFF|
|3e-324|0000000000000001|77 /6|0|0000000000000001|

These are fixed-environment converter rows; they do not claim that arbitrary strings bypass the Reader lexer or that the caller always enters FPSCR0. The complete158-case result ledger gives exact offsets for all cases.

## Confirmed numerical build rows

Primary instructions:20261005-jsoncpp-validation-libcpp-native.txt. These rows refine the earlier extraction, not a complete new converter implementation specification.

| Step | Address | What it does / gates | Order, widths and failure | Build boundary |
| --- | --- | --- | --- | --- |
| V1 | C:00081278..00081314 | For positive nonsentinel bigint, extract/shift high limbs and build normalized approximation with forced exponent3FF. Store leading high-limb bitcount through r1. | Top53 bits, truncation rather than generic bigint→double rounding. Result bits constructed with OR30000000/0FF00000. Independently checked on all reached sampled calls. | Shipped numeric helper exact. |
| V2 | C:0007EE82..0007EE9E | Approximate Delta, save at stack50; approximate Boundary, save at stack48; divide existing d8 by d0 into d0 immediately. | Binary64 quotient of the **normalized** approximations. | Exact source order; both execution engines and rational primitive checks corroborate. |
| V3 | C:0007EEA2..0007EEC8 | Calculate signed exponent difference using limb counts and returned leading-bit counts; select stack50 if positive, stack48 otherwise; add absolute difference<<20 to that saved high word. | Integer word edit happens **after** V2's divide. No reload/redivide before V4. | Do not move this adjustment ahead of division to obtain a mathematically improved result. |
| V4 | C:0007EECA..0007EF12 | Compare existing quotient with binary64 4000000000000000 (2); ≤2 selects small-step path. Nonzero difference sign sets magnitude/step1 (3FF0000000000000). Zero sign has separate predecessor/subnormal branches. | A finite normalized-approximation quotient does not incorporate V3's exponent correction. This association explains why a multi-ULP initial error can receive only a one-ULP correction before the exit gate. | Observable shipped behavior, not a correctly-rounded specification. No assumption about the original compiler's intent. |
| V5 | C:0007EF26..0007F056 | Apply signed step×ULP with binary64 arithmetic; top-exponent candidate is scaled by high-word−03500000 and restored after bounded adjustment. Repeat/exit depends on exponent change and the correction magnitude's fractional part. | Exact near-half/quarter literals and branch order remain as previous Q rows;113F…62 and7FE…FD are confirmed outputs under mode0, not guessed edge exceptions. | Generic .NET nearest-even parser cannot substitute for this whole path from the current evidence. |
| V6 | C:0005EB98..0005EC24; E:008E2306 | Normalized end must match; errno34 sets failbit4. Reader rejects stream state masked by5, even when failed conversion produced zero/Inf bits. | Nonzero subnormal is not universally a failure. | Keep conversion bits and acceptance separate. |

V2/V3's instruction order is the concrete source-backed explanation for the discrepancy lead. Independent checks establish primitive arithmetic and bigint inputs on sampled paths; they do not prove an exhaustive closed-form rule for replacing the remaining converter. A sparse table of4638 observed decimal strings would not be an exact implementation of all inputs.

## Loader and environment build rows

Primary transcript:20261005-jsoncpp-loader-independent-native.txt. Relocation records resolve indirect targets, rather than trusting decompiler function addresses.

| Step | Engine/package address | What happens | FP/locale implication / gates |
| --- | --- | --- | --- |
| L1 | E:0052E7DE..0052E7F8 | RobotManager::Init gets updater+0x5C and calls LoadHeader with firmware arguments0,0 and the captured JSON callback. | No FP reset or locale setter in reopened call slice. Startup initialization reaches this before the engine-thread startup described by the existing checked record; full host entry state is outside this report. |
| L2 | E:006768CC..0067692A | Allocate libc++ thread bookkeeping and0x30-byte tuple; tuple+0x4 gets LoadFirmwareFile pointer, +0x8 updater, +0x10 captured updater, +0x18 JSON callback. pthread_create gets null attributes and entry0067861D. | C++ bookkeeping is not a TBB arena task. No shipped FP reset in this construction slice. |
| L3 | E:006768F0..006768FC / GOT0103FA38 | GLOB_DAT relocation identifies LoadFirmwareFile as006764C1. | Raw GOT file word0 is relocated; Ghidra's apparent006864C0 is not trusted as the target. |
| L4 | E:0067862E..00678680 | Install libc++ thread-local bookkeeping, copy captured callback into an allocated void() closure, invoke tuple's LoadFirmwareFile pointer inline. | Reopened proxy has no FPSCR write/locale setter. External TLS helper behavior is a boundary, not inferred. |
| L5 | E:006764C0..006765EA | fopen rb; seek/tell/read/close; on mismatch clear byte-vector length. Set updater+0x18=1 and call supplied void callback iff callable target present. | File and parse run on this same new thread. Error logging/phone libc remain external boundaries. |
| L6 | E vtable0103063C slot+0x18=0067886D; E:0067886C..00678874,008CD630..008CD638 | Void closure loads captured updater+0x8, passes JSON callback at+0x10, tail-jumps via ARM veneer to LoadHeaderData PLT004BC2F4. | No scheduler handoff or FP reset in these blocks. |
| L7 | E:00677C44..00677D34 | Require loaded flag and byte length≥0x800; find NUL in first0x800 bytes; construct Reader/Value, parse; on success invoke captured JSON consumer inline. | Header Reader executes on loader pthread. Failure/short-file behavior remains existing checked rows. |
| L8 | E:008E22BE..008E2306; C:00056828..0005683C | decodeDouble's input stream constructs default locale; C constructor copies current global implementation with shared ownership. | Current **global** facets, not a frozen per-loader classic locale and not .NET CurrentCulture. |
| L9 | C:000567B8..000567FA | Guarded lazy global initialization calls classic() and retains the resulting implementation. Later calls return stored global. | Classic is the initial value, not proof that no concurrent writer ran before header parsing. |
| L10 | C:00056B38..00056BDC | locale::global installs new C++ implementation; named locale other than* also calls external setlocale(6,name). | A shipped setter exists. No package import of this __ndk1 global setter outside its own library was found in dynamic-symbol inventory; indirect/global mutation cannot be excluded by that inventory. |
| L11 | Package ordinary-VMSR scan; TBB slices in earlier extraction | Only five ordinary FPSCR-write candidates were found, all TBB save/restore helpers. No such writer appears in reopened loader bodies. No dynamic imports of fesetround/fesetenv/feupdateenv found across supplied libraries. | Scan scope excludes conditional ARM encodings, dynamic code and external-library operations. Do not turn absence into an actual thread-entry FPSCR value. |
| L12 | External pthread_create at E:0067692A; external host/native entry | Package provides no recovered literal that fixes the loader thread's entering numerical environment. | Actual rounding/FZ/DN value UNKNOWN. A proposed separate external-boundary record may cover phone pthread/host environment; it must not swallow the shipped converter's exact arithmetic. Manager decides taxonomy. |

Locale symbol inventory:20261005-jsoncpp-environment-symbols.json also finds separate GNU/stlport locale-global exports and setlocale imports in those libraries, Mono and Unity. Their C++ globals are not automatically the __ndk1 global. Phone C-locale writers affect external formatting; a call to setlocale alone does not prove this Reader's num_get facets changed. Existing Mono writer and C++ setter source rows remain bounded evidence, not a complete process-order proof.

Important upstream edge: num_get may stop on a character its current facets do not accept, normalize only the consumed prefix, and successfully convert that prefix. Reader checks fail/bad flags rather than requiring stream EOF. Therefore the direct normalized-wrapper failure on literal1,5 in the previous oracle is **not** itself a full-stream num_get result, and current decimal-point/grouping facets remain part of the production path. This report does not supply an observed phone facet configuration or fabricate one.

## Manager handoff

The independent executor and mathematical audits close the **Unicorn-only numerical artifact concern for the sampled fixed-mode paths**. A single independently confirmed ordinary counterexample is enough to reject the proposed correctly-rounded substitution criterion in that environment;158 checked inputs do not prove every possible decimal/mode path.

Adopt/check V1–V6 and L1–L10 as bounded rows, preserving conditions and caller gates. Keep actual host-thread numerical entry state/current global-facet value UNKNOWN pending external-state evidence or a manager-approved boundary record. The shipped converter remains exact; no ADP-1/audio exception applies. Do not replace its normal outputs with .NET merely because .NET is mathematically nearer.

M1-029 stays incomplete: these rows are not a complete general conversion algorithm or full Reader integration, and they do not settle real asString formatting or the final escaping exception destination. This follow-up's research artifacts are ready for manager review. Dependency trees and the compiled fixture remain local tooling; source/scripts/results are the reviewable evidence.
