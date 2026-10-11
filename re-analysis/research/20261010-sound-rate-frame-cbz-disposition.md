# Q14 rate/frame navigation: explicit Thumb compare-and-branch

Checkpoint807; guard reread before every step. Engine SHA25602263c07f6bb60f4d7f351a3667dca84fd3e6c0fc6f838e18b5de7cf4e2989e1.

803's FIFO model only recognized direct branches whose mnemonic began with b. Thumb CBZ/CBNZ therefore lost their target successor. This is a concrete navigation defect, not an admitted writer. The new script handles the decoded CBZ/CBNZ IDs before ordinary register invalidation: known tested values choose the zero/nonzero successor; unknown values retain both. Neither instruction changes NZCV. Seeds, 768-byte windows, modeled memory operations and 4096-state limit remain unchanged.

Primary -cbz-native.txt independently reopens shipped audio-control witnesses:5968B6 CBZ R0->596908 skips the event append for a zero event reference;5968FC CBNZ R0->596908 bypasses the first-state publication when already set;596900 CBZ R0->596908 skips publication for zero metadata flag. Existing app-control decisions retain their evidence; this is no new callee extraction.58EED8 CBZ is an additional opcode witness only; out of scope: its temporary backing release and associated allocation, not descended.

Reproducible -cbz-comparison.py/.json compares complete event identities against803. Both process6855 candidate windows and yield310 events, with zero added and zero missing. Capped windows change434->435. Thus no positive new rate/frame writer is supplied. The larger cap count remains visible; no exhaustive successor, caller, global alias or all-input absence claim follows.804's three independently traced stack-alias rate reads remain additional primary reads that this stack-unaware generic model does not enumerate.

Six rate/frame obligations043/092/099/120/124/130 remain PARTIAL/AUDIBLE for their original all-writer/alias and admitted-input proofs. No global immutability or fixed-rate/frame policy is inferred. Coverage116 CHECKED /24 PARTIAL /0 NOT DONE;140 AUDIBLE /0 INTERNAL. Research only; Q15 remains behind Q14. out of scope: per-sample DSP, storage/I-O and census-excluded branches.
