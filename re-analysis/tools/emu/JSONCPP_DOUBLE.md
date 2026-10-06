# Shipped jsoncpp real-number oracle

This research harness runs packaged ARMv7 `libc++_shared.so`, pinned to SHA-256
`8ac5090bbd0be7401af5fff6044ded6f1e6ced692fcd3581fa3cb937b519a16a`.
It does not change production code or fidelity classifications.

Dependencies: Python with `lief` and `unicorn==2.1.4`, .NET SDK 9 (comparison only).
The ELF stays local. Paths resolve relative to the tool, not the current directory.

```text
python re-analysis/tools/emu/emu_jsoncpp_double.py -- "0.1" "-0.0" "1e309"
dotnet build re-analysis/tools/emu/jsoncpp_dotnet/jsoncpp_dotnet.csproj
python re-analysis/tools/emu/diff_jsoncpp_double.py --out re-analysis/research
python re-analysis/tools/emu/analyze_jsoncpp_double.py re-analysis/research
```

Oracle stdin accepts JSONL objects `{ "input": "0.1" }`; positional strings use
`--` before negative strings. Output is JSONL. `--fpscr 0x00400000` changes the
controlled FP environment. Each entry independently resets library memory,
registers, errno, allocator and native freelists. The allocation fixture always
succeeds or stops with a fixture exhaustion error; it does not simulate failure.

Entry 0x7E570 returns binary64 in r0/r1 and writes an end pointer; output reports
its UTF-8 byte offset and errno. Entry 0x5EB98 wraps conversion of an already
normalized token; its state is 4 for failbit. Engine Reader considers stream
bits 1 or 4 failure; stream EOF bit2 is separate and is not emulated here.

The **Reader lexer and locale num_get normalization are not executed**. Raw
acceptance of nan/inf/hex is not JSON acceptance. The report supplies the checked
source boundary. ASCII ctype/lowercase/strncasecmp, mutex, allocator, byte-memory
imports, guard/errno and an unused C-locale handle are fixture stubs. Unknown
imports abort. Neither phone C locale nor actual loader-thread FPSCR is inferred.

The differential driver uses seed0x1029, NumberStyles.Float, invariant culture,
and explicit fr-FR cases. The .NET helper also accepts `default_style: true`
to compare the default Float|AllowThousands overload; those supplementary probes
are separate from the strict Float corpus. It writes all observations compressed, every difference
as JSONL, and a summary. The analyzer writes a tabular complete discrepancy ledger
and checks ordinary bit mismatches against exact decimal rational nearest-even
rounding (no host binary64 parser). All result files are research artifacts.
Finite testing never proves equality for every decimal string. A finite mismatch
is enough to fail the proposed equivalence criterion under this fixture.
