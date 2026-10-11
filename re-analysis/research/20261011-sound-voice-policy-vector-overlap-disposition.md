# Q14-033: vector-store extent model and native width check

Checkpoint839; guard reread before each step; decisions only. Same primary engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. `-vector-overlap-navigation.py/.json` adds only VST1/2/3/4 and VSTM destination/width observations to838's unchanged478 seeds and32-instruction windows. It neither changes838's file nor evaluates vector contents or DSP arithmetic.

Primary `-vector-overlap-native.txt` reopens actualA0B01C: VST1.64 {D16,D17},[SP:40hex]. Two full D registers transfer16bytes beginning atSP. The40hex annotation is alignment, not a displacement. The model checks encodedRn against the decoded base and deliberately ignores the decoder's alignment-bearing mem.disp for these NEON stores. Vector source registers are recognized by numbered D/S/Q names; SP/SB/SL and post-index scalar registers are not counted as vector data. The exact native instruction is an asserted width/destination control, not an invented call/input or a witness that policy fields are overwritten.

The corrected extension adds zero overlap events in these finite windows. It retains all838's10 events at8 stores, four nonexact-starting byte overlaps and five saved frontiers unchanged. This is a bounded model result, not proof that every vector store, alias or writer is absent: different seeds/intervals, Thumb/code boundaries, conditional paths, indexed/composite addresses, branches/calls, writeback propagation and admitted receivers remain unproved. Stop this particular seed/window lead; don't repeat it as a stronger all-writer proof.

Original033 remains PARTIAL/AUDIBLE for indirect/re-init admission and the unrestricted policy-writer aliases. Known VW650/749/758/759/769 and836-838 contracts stay paid.116 CHECKED /24 PARTIAL /0 NOT DONE;140 AUDIBLE /0 INTERNAL unchanged. Q15 stays behindQ14.

out of scope: per-sample vector/DSP arithmetic, backing/storage/I-O and excluded source/payload branches. Only exact destination width and existing decision interfaces were read. Research only; no production, manifest, inventory, PROJECT_STATE or hardware changes.
