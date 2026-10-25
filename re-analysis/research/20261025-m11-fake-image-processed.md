| Item / step | Coverage | What the original does / search result | Address with pasted instructions or UNKNOWN and attempt | Record |
| --- | --- | --- | --- | --- |
| 1. Earlier checked caller | CHECKED search; none established | Row 68 recovered the writer; Claude check confirms it and explicitly leaves callers/thread UNKNOWN. No earlier checked caller found by known.py or report search. | `0x00657C2A: mov sb,r1`; `0x00657C2C: mov r5,r0`; `0x00657C2E: str.w sb,[r5,#0xe8]`. [Row 68/check](20261025-r-m11-rows-68-70-claude-check.md), “Open”. | M11-035; M11-017 consumer |
| 2a. Direct calls / tails | CHECKED bounded search; none found | No immediate Thumb BL/BLX/B.W/B16/conditional branch or ARM B/BL/BLX to function entry in either executable section. No PLT stub exists for this function. | Entry searched **00657C24**, exported Thumb value **00657C25**. UNKNOWN caller/event/thread/arguments: no branch hit in .plt 004A4010..004D685C or .text 004D6860..00AE3682; encodings/search method below. | M11-035 |
| 2b. Relocation / vtable / stored pointer | CHECKED bounded search; none found | No relocation refers to its symbol or address. No raw even/Thumb function-pointer word anywhere in engine except the Thumb value in its own dynsym entry. Thus no pointer slot to report in .data.rel.ro/.local, GOT, data, rodata, or executable literal pools. | Only raw match: file/VA **0002CF38**, value **00657C25**, st_value field in .dynsym. No pasted caller instructions exist; UNKNOWN caller after all 52,521 relocations and all byte offsets searched. | M11-035 |
| 2c. Export definition | CHECKED | Defined global/default-visible STT_FUNC symbol in engine, name below; export is a possible linkage interface, not evidence of invocation. | .dynsym entry **0002CF34**, bytes `30 97 11 00 25 7c 65 00 7a 00 00 00 12 00 0a 00`: st_value=00657C25, st_size=122, info=0x12, shndx=10. Function instructions/boundary as item1. | M11-035 |
| 2d. Shipped exported-symbol consumers | CHECKED bounded search; none found | All 28 shipped .so dynamic-symbol tables: only defining engine symbol, no imported/undefined use. Name text occurs only once across these libraries, in engine .dynstr. App C#/Java/smali search found no use. | UNKNOWN external caller/event/thread/arguments. Name at file/VA **001ABCB4** in .dynstr (FakeImageProcessed substring at001ABCD5); no string consumer address found. Exact search corpus below. | M11-035; app boundary |
| 2e. Runtime-computed or external invocation | PARTIAL / UNKNOWN | Engine imports dlopen/dlsym; no claim that an arbitrarily computed lookup or caller outside shipped artifacts is impossible. PC-relative/computed function addresses and register-indirect dataflow were not exhaustively proven absent. | UNKNOWN: direct branches, symbol imports, relocations, raw pointers, name strings and app-source searches above find no caller; they do not prove universal runtime unreachability. | M11-035 |
| 3. Normal camera ordering, conditional item | NOT DONE: condition not established | No caller reachable during normal camera streaming was found. Therefore no engine event, calling thread, supplied timestamp/IMU values, or ordering relative to SetNextImage/UpdateAllResults can be assigned. Reuse row68's actual camera path. | UNKNOWN normal-stream caller. Existing body directly assigns only E8 at00657C2E, then enters IMU loop; this does not establish an invocation. No new E4/EC timing claim. | M11-035/040; M11-017 |
| 4. Negative evidence and decision boundary | CHECKED search inventory, no exclusion | Search results below are complete for their named mechanisms/corpus. Manager decides whether these justify a live-path decision. No not-supported, no-effect, unreachable classification, fidelity promotion or implementation advice is made. | Concrete writer witness00657C2E plus addresses/corpora above. Universal closure remains UNKNOWN, not inferred from absence of direct calls. | M11-035/017 |

# R-M11-FAKE-IMAGE-PROCESSED — research row 71

Answers [request](requests/20261025-m11-fake-image-processed.md). Initial loop pull/read HEAD **0c176b533e70896d206b54344079d0ed5abc8888**. **No caller found in the specified searches; overall PARTIAL for universal indirect/external closure.** Research only; no code, manifest, inventory, queue, project state, branch or acceptance changes. The explicit request's “if no caller is found” outcome applies; absence is not an exclusion decision.

Primary binary `resources/lib/armeabi-v7a/libcozmoEngine.so`, 3.4.0-1204, freshly computed SHA256 **02263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1**. Ran installed Python313 `known.py 0x00657C24` before tracing. It gives **106-byte** indexed instruction body at00657C24. ELF dynamic symbol size is **122**, including bytes beyond that indexed body; those extra bytes are not silently decoded as an additional executable tail or used to change function bounds. Row68's instruction/IMU body evidence is reused, not re-extracted. [Claude check of row68](20261025-r-m11-rows-68-70-claude-check.md) confirms the E8 store but explicitly does not close callers.

## Exact symbol and relocation findings

The exported name is:

```
_ZN4Anki5Cozmo15VisionComponent18FakeImageProcessedEjRKNSt6__ndk16vectorIKNS0_12ImageImuDataENS2_9allocatorIS5_EEEE
```

Demangles to `Anki::Cozmo::VisionComponent::FakeImageProcessed(unsigned int, std::__ndk1::vector<Anki::Cozmo::ImageImuData const, ...> const&)`. It is GLOBAL, DEFAULT visibility, FUNC, defined in section10, value00657C25. As the checked store shows, r0=this,r1=timestamp,r2=IMU vector reference. **There are no recovered caller-supplied values.** Do not infer that it receives a chunk timestamp or that any specific thread calls it from its name.

Inspected all **35,279 dynamic relocations and 17,242 PLT/GOT relocations**, 52,521 total, through LIEF, testing symbol name and explicit addend against entry/even-or-Thumb address. Also searched every raw byte offset for little-endian `24 7c 65 00` and `25 7c 65 00`, so REL inline-addend slots and unaligned/raw literal occurrences are included, rather than relying on a RELA addend API. Even value: zero hits. Thumb value: **one**, the defining dynsym st_value at0002CF38. No PLT relocation or stub for this symbol. No dynamic vtable/function-pointer slot was found.

Search includes these data regions (VA, file offset, length):

| Section | VA | File offset | Length |
| --- | --- | --- | --- |
| .data.rel.ro.local | 01019200 | 01018200 | 000039E8 |
| .data.rel.ro | 0101CBF0 | 0101BBF0 | 000217C0 |
| .got | 0103E78C | 0103D78C | 00012874 |
| .rodata | 00BE3ED0 | 00BE3ED0 | 00433648 |
| .data | 01051000 | 01050000 | 00008028 |

This is a pointer-slot search, not a proof against addresses constructed by arithmetic, PC-relative ADR/ADD, encoded/compressed address tables, interposition or code loaded later.

## Executable direct-branch search

Searched **all SHF_EXECINSTR sections**, .plt (206,924 bytes) and .text (6,344,226), total6,551,150. At every two-byte alignment, decoded immediate Thumb BL/BLX and unconditional B.W using S/J1/J2 sign-extension and interworking PC alignment; conditional B.W using its separate imm21 layout; short unconditional/conditional B16 using their signed displacements. At every four-byte-aligned VA decoded ARM B/BL and immediate BLX, including the H bit for Thumb target and ARM PC+8. Compared actual target to00657C24; no candidate hits in either mode. Searches were raw-encoding based, so a linear disassembler losing alignment at a literal pool cannot conceal an otherwise matching immediate branch. Scanning both modes can create false candidates; here there were none to validate as real callers. It also does not prove an arbitrary register-indirect BX/BLX has no computed target.

Entry-address search cannot establish use of an interior address; none is claimed. There is no PLT target to add. The complete decomp corpus finds only the function itself and its outgoing callee caller annotations (AddImuData, CalculateTimestampForImageIMU); that result is a navigation cross-check, not instruction evidence for absence.

## Exported-symbol consumer search

Parsed dynamic symbols in **all 28 `resources/lib/**/*.so` files**. Exact mangled-name match exists only as the defining engine symbol. No undefined/import entry in libunity, libmain, libmono, libacattsandroid, libBlueDoveMediaRender, libDAS, the shipped C++ runtimes, libtbb or any shipped OpenCV library. Raw substring `FakeImageProcessed` occurs only once across the same 28 files, in engine's .dynstr, not a second dlsym name literal in a consumer. The full mangled string begins001ABCB4; the substring begins001ABCD5. Both are the **same** string occurrence, not two references.

Case-insensitive text search for `FakeImageProcessed` and `fake.image.processed` in **unity/**, **sources/**, **smali/** over .cs/.java/.smali/.xml/.txt files found no matches. No missing directories or tool errors were returned. This covers recovered app source, not hypothetical unrecovered managed reflection/constructed strings. `dlopen` and `dlsym` are imported by the engine, so “no literal/name match” is not a universal proof about all dynamic lookup behavior. No such lookup targeting this export was found; event/thread/argument provenance remains UNKNOWN.

## Scope and remaining uncertainty

No normal-stream caller found, so item3's conditional ordering extraction does not start. Row68/its check still owns normal SetNextImage rollback and UpdateAllResults's pre-tail border sampling. FakeImageProcessed's **own** known E8 store is not evidence that the normal camera flow takes that path. Conversely a public export and no static call are not enough to silently remove it from the inventory.

The manager can review the explicit negative results and decide whether the shipped-stack input boundary rules it out. This answer makes **no exclusion claim**, no no-effect claim and no claim that generic alias/caller closure is solved. It does not change M11-035/M11-017 status or the row68 report. No runtime/hardware test and no implementation regression test; this is source-search evidence only. Only this report is committed/pushed.
