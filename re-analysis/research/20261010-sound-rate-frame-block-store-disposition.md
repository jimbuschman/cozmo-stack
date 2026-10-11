# Q14 rate/frame writer proof: multi-register overlap navigation

Checkpoint770; scope guard reread. Same engine hash; reproducible `20261010-sound-rate-frame-block-store-expansion.py/.txt` extends the existing bounded reference navigation only for STM/VSTM contiguous write ranges. It targets105243C..4F (rate/quantum and related derived words) and108DF90..9B (device settings). ARM/Thumb candidate seed and local 768-byte possible-branch propagation limits remain explicit.

The pass visits4392 candidate windows and emits314 existing scalar/read/GOT/live-register events. Their records are identical to the earlier saved scalar/navigation result, including seventeen known direct stores. It emits no additional block-write-overlap event. Candidate count is not4392 production paths or proof that arbitrary aliases cannot reach these globals.

The new check derives register-list widths (S/general4,D8), IA/IB/DA/DB first address and contiguous byte extent only when the local base is established. Unknown/call-clobbered bases, shifted/indexed inputs, unmodelled stack/copy aliases, relative pointers, predicate/loop bounds and missing seeds remain limitations. NEON structure stores are not modeled by this STM/VSTM extension. Thus this pays this specific additional store form, not the original unrestricted writer/alias census.

Known exact scalar writers and RC654's nineteen actual pointer/ABI handoffs stay paid; DZ659/DL754 preserve separate actual zero input/provider/handler binding questions.043/092/099/120/124/130 remain PARTIAL. No default-positive, zero-divisor absence or selected Android division provider is inferred. Stop this negative lead rather than following storage or sample callees.

107 native+8 scope CHECKED/25 PARTIAL;140 AUDIBLE/0 INTERNAL unchanged. Research only.

out of scope: storage/I-O, allocation backing and per-sample DSP arithmetic. Timing/frame values and native control publication remain exact.
