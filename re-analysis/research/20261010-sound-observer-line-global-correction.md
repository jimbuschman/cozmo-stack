# Q14-028/029: observer line-array address correction

Checkpoint787; scope guard reread. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. This resolves a concrete conflict between OR613 parameter propagation and NTHREAD1/MNREG3 thread publication, rather than launching another writer census. Primary `20261010-sound-observer-line-global-correction-native.txt`; exact address equations in `-correction-witness.json`.

The prior OR613 research claim was: “Walk live global line array108DF4C.” That address is wrong. AtA40F30, ARM PC isA40F38, plus literal0064D01C gives108DF54. The LDM atA40F34 reads array base108DF54 and count108DF58; physical entries are4-byte line pointers. This correction changes the claimed global identity, not the actual key48 and C0bit2 behavior.

A40F6C's independent initial equation isA40F74+8+0064CFD8=108DF54. Its saved reload address isA40F98+8+0064CFB4=108DF54. Following the live line20 callback, A40FD8 reloads that descriptor's base/count before continuing from the retained physical cursor. Meter propagation therefore uses the same corrected array, not the thread word. Raw thirdword low5/C1 mutation and descending eligible meter construction MC667 remain supported.

The audio-thread equation remainsA408AC+8+0064D698=108DF4C. A408B0 stores the pthread_self result there before9D4054 and Perform9AF8A8. Existing NTHREAD1 and Q15 MNREG3 are correct; no thread identity is reinterpreted as an array pointer, and no Q15 coverage is settled by this correction. Registry owner108D868+F4 is likewise unchanged.

The affected OR613 note is corrected in place and annotated. Original028/029 remain PARTIAL for admitted wrapper/key/callback/raw-thirdword and callback-body inputs; this resolves an erroneous address association, not the remaining admission proof.107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only: no production, fidelity or hardware change.

out of scope: registry/array backing and phone thread/mutex implementation, plus per-sample meter arithmetic. No callee descent follows those boundaries.
