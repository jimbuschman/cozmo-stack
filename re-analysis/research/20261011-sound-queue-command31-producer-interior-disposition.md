# Q14-104/131 producer-body entry check, checkpoint845

Scope guard reread before each step. This asks whether a branch supplies the original nonzero command1F producer input by entering its body after the initial zero gate. That can change cutoff/control timing and qualifies; no helper body, allocator or callback family is opened.

Primary `20261011-sound-queue-command31-producer-interior-native.txt`, engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1. `-callers.py/.json` enumerates immediate executable-section ARM/Thumb external encodings into exactly9A60B8..6120, excluding the producer's own local branches. The19 resulting encodings are individually checked below; no actual immediate producer entry is supplied by them. Raw aligned entry pointers and exact ELF entry symbols likewise have no match, which is limited navigation evidence, not unreachability.

| Apparent Thumb site | Actual ARM containing word | Primary instruction |
| --- | --- | --- |
| 0x9a59ba | 0x9a59b8 (2d10a0e3) | mov r1, #0x2d |
| 0x9a5a76 | 0x9a5a74 (000051e3) | cmp r1, #0 |
| 0x9a5bc6 | 0x9a5bc4 (013082e2) | add r3, r2, #1 |
| 0x9a5bfa | 0x9a5bf8 (013081e2) | add r3, r1, #1 |
| 0x9a5d82 | 0x9a5d80 (0800a0e1) | mov r0, r8 |
| 0x9a5d9a | 0x9a5d98 (0380a0e1) | mov r8, r3 |
| 0x9a5d9e | 0x9a5d9c (03a0a0e1) | mov sl, r3 |
| 0x9a5db2 | 0x9a5db0 (1a36a0e1) | lsl r3, sl, r6 |
| 0x9a5dca | 0x9a5dc8 (89c1a0e1) | lsl ip, sb, #3 |
| 0x9a5e16 | 0x9a5e14 (000051e1) | cmp r1, r0 |
| 0x9a5e5e | 0x9a5e5c (1eff2fe1) | bx lr |
| 0x9a5e72 | 0x9a5e70 (1eff2fe1) | bx lr |
| 0x9a5e86 | 0x9a5e84 (1eff2fe1) | bx lr |
| 0x9a5fa2 | 0x9a5fa0 (04408fe0) | add r4, pc, r4 |
| 0x9a5fda | 0x9a5fd8 (03308fe0) | add r3, pc, r3 |
| 0x9a65ca | 0x9a65c8 (24219fe5) | ldr r2, [pc, #0x124] |
| 0x9a65e2 | 0x9a65e0 (000092e5) | ldr r0, [r2] |
| 0x9a65fe | 0x9a65fc (046088e5) | str r6, [r8, #4] |
| 0x9a6602 | 0x9a6600 (003088e5) | str r3, [r8] |

Actual source prefixes in the primary capture establish separate ARM entries59A4,5A04,5AFC,5D48,5E50,5E64,5E78,5F98 and65C0 (all9A prefix). Ghidra nearest-owner57D4's124-byte range does not cover59A4/5A04; nearest-owner5E64's16-byte range does not own5E78 or5F98. Fresh PUSH/return/literal/PC-relative boundaries are used rather than extending those stale labels. The5E50/64/78 leaves each end at their own BXLR before literal/separate entry; halfword offsets into those ARM returns do not become Thumb caller evidence.

The preceding actual producer9A6064 ends with POPPC at60B0 and literal60B4;9A60B8 is a separate entry with zero gate before its own PUSH60C0. No preceding-entry fallthrough is inferred from adjacency. PE416C's known external-bundle interface65C0 is used only for instruction-boundary attribution; allocator/source/file descendants remain outside this caller question.

Q14-104/131 remain PARTIAL for nonzero9A60B8 caller and three actual payload values, plus their original unrestricted parent/output writer/alias and other named input proofs.843's pending cancellation→exact partial descriptor→known ordinary bus-root stop recipient remains paid. Zero local input and19 false encoded entries do not prove all supported inputs absent. Stop this finite body-entry lead; do not reopen callback/registration/backing bodies by offset coincidence.

out of scope: source/list backing, allocator/copy/file-resolver implementation and per-sample arithmetic.117 CHECKED (109 native+8scope)/23 PARTIAL/0 NOT DONE;140 AUDIBLE/0 INTERNAL. Research only; no production, fidelity, inventory, PROJECT_STATE, hardware or Q15 change.
