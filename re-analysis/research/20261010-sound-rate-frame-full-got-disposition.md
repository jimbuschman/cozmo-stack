# Q14 rate/frame pointer navigation: shipped GOT section bounds

Checkpoint808; guard reread before each step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

The original rate/frame model retained guessed GOT bounds103FF00..1040400.805 corrected the optional-input model independently; this pass applies the concrete ELF-derived boundary correction to the original six rate/frame obligations. The independent -full-got-section-witness.json reads raw ELF32 section headers/string table, records .got address103E78C,file offset17029004,size75892,end1051000 and whole-section SHA256028f3d67d2009f718545b453c3cf7b3a3047357e61f38f737226dc9f9fe83766. No .got.plt section is present. The previous1280-byte range covered only part of this75892-byte section.

-full-got-navigation.py keeps807's instruction/value/flag/CBZ/FIFO model and original LOAD data map. It changes eligible GOT raw data reads and literal-derived seed bounds to the actual section. The old MOVW immediate filter deliberately remains unchanged in this isolated correction; it is still a limitation, not an exhaustive MOVW census. Raw file pointer values do not assert the runtime resolver's selected binding or any admitted caller.

Reproducible -full-got-comparison.py/.json compares complete event identities to807:windows6855->20806;caps435->998;events310->310,zero added and zero missing. No positive new writer or pointer escape is established.803's source-backed new scalar reads and804's three source-backed stack-alias reads remain positive independent evidence; the stack-unaware generic model still omits the latter. A no-new-event comparison is not all-writer, alias or runtime-input absence proof.

043/092/099/120/124/130 stay PARTIAL/AUDIBLE for original admitted inputs and complete all-writer/alias boundaries. Coverage116 CHECKED /24 PARTIAL /0 NOT DONE;140 AUDIBLE /0 INTERNAL. Research only; no production,fidelity,inventory,hardware or Q15 changes. out of scope: GOT resolver implementation,storage/I-O,per-sample DSP and census-excluded branches. Section bytes are navigation evidence only; no excluded descendant is traced.
