# Q14-109/134 native-image key declaration, checkpoint882

Scope guard reread before every step. Original four key words108D9A0..AC only. This pays the image-declaration part of initialization; it does not substitute a cold emulator snapshot for post-initialization runtime values. No initializer, loader, JNI, storage or DSP body is descended into. `-state.py/.json` reads the original ELF32 program, section, REL and dynamic-symbol tables directly with structural checks; containing LOAD sizes independently agree with LIEF. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

| Original step | Primary bytes/metadata | Disposition |
|---|---|---|
| Four-key image extent |Keys[108D9A0,108D9B0);containing PT_LOAD VA1019200,file offset1018200,file size3FE28,memory size76DB4 |File-backed image ends1059028; memory extends108FFB4. All16 key bytes lie in the extra memory extent, not file-backed initializer data. |
| Section declaration |`.bss` SHT_NOBITS=8,VA1059030,size36F84,SHF_WRITE+SHF_ALLOC |The offset field1058028 is not a16-byte key payload to read from the file. The key range is writable; no immutable/read-only claim. |
| Declared initial contents |Actual PT_LOAD file/memory extents above |The four key words are declared zero-fill at image creation, before initialization. This is a format-declaration fact, not a claim that later K0 is zero. |
| Relocation boundary |Raw52,521 SHT_REL records cross-checked against LIEF; no relocation offset within64 bytes of the key range; no R_ARM_COPY records |No nearby recorded patch or copy relocation is supplied as a key initializer. This is not an exhaustive claim about relocation resolver, foreign-module or later code effects. |
| Named dynamic object boundary |No defined dynamic symbol whose point/declared size overlaps key bytes |No named key export/object binding is established by these tables. Hardcoded/computed/escaped/foreign address access remains unexcluded. |
| Later initialization boundary |DT_INIT_ARRAY103E3B0;byte count1F4;125 raw entries retained |Metadata only. These functions are not traced or executed as125 new obligations; their effects, JNI/Init and subsequent key mutations remain open at actual relevant seams. |

The [ELF gABI, program loading §7.2](https://gabi.xinuos.com/elf/07-pheader.html#segment-types) defines a PT_LOAD memory extent beyond its file-backed bytes as zero-filled; §7.5 identifies `.bss` as memory-only NOBITS data. This definition interprets the shipped header bytes. It is not source evidence for a particular phone loader implementation or constructor outcome, and does not upgrade fidelity records.

Exactly open is now post-initialization four-key values and later mutation/escape bound to admitted bank input, including alternate/foreign carriers and any original concrete unpaid J/UD/BA/FS seam. PH640/865/881's six raw banks and local carrier kills do not prove encrypted input unreachable or keys permanently zero. Stop this declaration check; do not repeatedly read zero-filled BSS as a runtime constant or open a generic125-constructor/family audit.

Q14-109/134 remain PARTIAL, AUDIBLE.117 CHECKED (109 native+8 scope)/23 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL. Research only; no production/manifest/inventory/PROJECT_STATE/hardware/Q15 change. The phone loader implementation is outside the shipped-engine-code boundary; only its image-declaration interface is used here. out of scope: storage/I-O and per-sample DSP implementations.
